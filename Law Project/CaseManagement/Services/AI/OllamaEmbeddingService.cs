using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MVCCaseManagement.Models.AI;

namespace MVCCaseManagement.Services.AI
{
    /// <summary>
    /// Implementation of IEmbeddingService using local Ollama instance with resilient local feature-vector fallback.
    /// Ensures 100% data privacy and offline capability with no cloud dependencies.
    /// </summary>
    public class OllamaEmbeddingService : IEmbeddingService
    {
        private readonly HttpClient _httpClient;
        private readonly AIOptions _options;
        private readonly ILogger<OllamaEmbeddingService> _logger;

        private const int FallbackDimensions = 128;
        private static readonly ConcurrentDictionary<string, float[]> EmbeddingCache = new(StringComparer.OrdinalIgnoreCase);
        private static volatile EmbeddingProviderInfo _providerInfo = new()
        {
            ProviderName = "Local Ollama Engine (qwen2.5:7b)",
            IsUsingFallback = false,
            Dimensions = 128,
            LastCheckedUtc = DateTime.UtcNow,
            DiagnosticMessage = "Initialized"
        };

        public OllamaEmbeddingService(
            HttpClient httpClient,
            IOptions<AIOptions> options,
            ILogger<OllamaEmbeddingService> logger)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _options = options?.Value ?? new AIOptions();
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public EmbeddingProviderInfo GetProviderInfo() => _providerInfo;

        public void ClearCache()
        {
            EmbeddingCache.Clear();
            _logger.LogInformation("Embedding vector cache cleared.");
        }

        public async Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return new float[FallbackDimensions];
            }

            string clean = text.Trim();
            string cacheKey = clean.Length > 256 ? clean.Substring(0, 256) : clean;
            if (EmbeddingCache.TryGetValue(cacheKey, out var cachedVector))
            {
                return cachedVector;
            }

            // 1. Attempt to generate embedding via local Ollama API
            string baseUrl = (_options.BaseUrl ?? "http://127.0.0.1:11434").TrimEnd('/');
            string embeddingEndpoint = $"{baseUrl}/api/embeddings";
            string model = !string.IsNullOrWhiteSpace(_options.Model) ? _options.Model : "qwen2.5:7b";

            try
            {
                var payload = new
                {
                    model,
                    prompt = clean.Length > 2000 ? clean.Substring(0, 2000) : clean
                };

                var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(TimeSpan.FromSeconds(2)); // Rapid 2-second timeout before falling back

                var response = await _httpClient.PostAsync(embeddingEndpoint, content, cts.Token);
                if (response.IsSuccessStatusCode)
                {
                    string json = await response.Content.ReadAsStringAsync(cts.Token);
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("embedding", out var embeddingEl) && embeddingEl.ValueKind == JsonValueKind.Array)
                    {
                        var list = new List<float>();
                        foreach (var item in embeddingEl.EnumerateArray())
                        {
                            list.Add(item.GetSingle());
                        }

                        var normalized = NormalizeVector(list.ToArray());
                        EmbeddingCache[cacheKey] = normalized;

                        _providerInfo = new EmbeddingProviderInfo
                        {
                            ProviderName = $"Local Ollama Engine ({model})",
                            IsUsingFallback = false,
                            Dimensions = normalized.Length,
                            LastCheckedUtc = DateTime.UtcNow,
                            DiagnosticMessage = $"Online: neural embedding endpoint {embeddingEndpoint} operational."
                        };

                        return normalized;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Ollama embeddings endpoint ({Endpoint}) unavailable or timed out. Falling back to deterministic {Dim}-d feature hashing vectorizer.", embeddingEndpoint, FallbackDimensions);
                _providerInfo = new EmbeddingProviderInfo
                {
                    ProviderName = "Deterministic Subword Feature Hashing (Fallback)",
                    IsUsingFallback = true,
                    Dimensions = FallbackDimensions,
                    LastCheckedUtc = DateTime.UtcNow,
                    DiagnosticMessage = $"Fallback active: {ex.Message}"
                };
            }

            // 2. Fallback: Deterministic L2-normalized Subword Feature Vectorizer
            var localVector = GenerateDeterministicFeatureVector(clean, FallbackDimensions);
            EmbeddingCache[cacheKey] = localVector;
            return localVector;
        }

        public float ComputeCosineSimilarity(float[] vectorA, float[] vectorB)
        {
            if (vectorA == null || vectorB == null || vectorA.Length == 0 || vectorB.Length == 0)
                return 0f;

            int len = Math.Min(vectorA.Length, vectorB.Length);
            float dotProduct = 0f;
            float normA = 0f;
            float normB = 0f;

            for (int i = 0; i < len; i++)
            {
                dotProduct += vectorA[i] * vectorB[i];
                normA += vectorA[i] * vectorA[i];
                normB += vectorB[i] * vectorB[i];
            }

            if (normA <= 0f || normB <= 0f) return 0f;

            float similarity = dotProduct / ((float)Math.Sqrt(normA) * (float)Math.Sqrt(normB));
            // Clamp to [-1.0, 1.0] to prevent floating point inaccuracies
            return Math.Max(-1.0f, Math.Min(1.0f, similarity));
        }

        public async Task<List<(T Item, double HybridScore)>> HybridRerankAsync<T>(
            string query,
            List<T> items,
            Func<T, string> textSelector,
            Func<T, double> lexicalScoreSelector,
            double semanticWeight = 0.6,
            CancellationToken cancellationToken = default)
        {
            if (items == null || items.Count == 0)
                return new List<(T Item, double HybridScore)>();

            var queryVector = await GenerateEmbeddingAsync(query, cancellationToken);
            var results = new List<(T Item, double HybridScore)>();

            foreach (var item in items)
            {
                cancellationToken.ThrowIfCancellationRequested();

                string itemText = textSelector(item) ?? string.Empty;
                var itemVector = await GenerateEmbeddingAsync(itemText, cancellationToken);

                float cosineSim = ComputeCosineSimilarity(queryVector, itemVector);
                // Map cosine similarity [-1, 1] to [0, 1] normalized space
                double normalizedSemanticScore = Math.Max(0.0, (cosineSim + 1.0) / 2.0);

                double lexicalScore = lexicalScoreSelector(item);
                // Normalize lexical score to [0, 1] ceiling of 3.0
                double normalizedLexicalScore = Math.Min(1.0, lexicalScore / 3.0);

                double hybridScore = (semanticWeight * normalizedSemanticScore) + ((1.0 - semanticWeight) * normalizedLexicalScore);
                results.Add((item, hybridScore));
            }

            return results.OrderByDescending(r => r.HybridScore).ToList();
        }

        private static float[] GenerateDeterministicFeatureVector(string text, int dimensions)
        {
            var vector = new float[dimensions];
            if (string.IsNullOrWhiteSpace(text)) return vector;

            string normalized = text.ToLowerInvariant();
            var tokens = normalized.Split(new[] { ' ', ',', '.', ';', ':', '/', '-', '(', ')' }, StringSplitOptions.RemoveEmptyEntries);

            // N-gram and word feature hashing
            foreach (var token in tokens)
            {
                if (token.Length < 2) continue;

                // Word level hash
                int h = Math.Abs(token.GetHashCode()) % dimensions;
                vector[h] += 1.0f;

                // Character tri-gram hashes for subword semantic capture
                for (int i = 0; i <= token.Length - 3; i++)
                {
                    string trigram = token.Substring(i, 3);
                    int th = Math.Abs(trigram.GetHashCode()) % dimensions;
                    vector[th] += 0.5f;
                }
            }

            return NormalizeVector(vector);
        }

        private static float[] NormalizeVector(float[] vector)
        {
            float sumSquares = 0f;
            for (int i = 0; i < vector.Length; i++)
            {
                sumSquares += vector[i] * vector[i];
            }

            if (sumSquares <= 0f) return vector;

            float norm = (float)Math.Sqrt(sumSquares);
            var normalized = new float[vector.Length];
            for (int i = 0; i < vector.Length; i++)
            {
                normalized[i] = vector[i] / norm;
            }

            return normalized;
        }
    }
}
