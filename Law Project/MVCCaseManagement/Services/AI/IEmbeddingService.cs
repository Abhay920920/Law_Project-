using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MVCCaseManagement.Services.AI
{
    /// <summary>
    /// Service for generating dense vector embeddings, computing cosine similarity,
    /// and executing hybrid reranking (semantic similarity + lexical BM25).
    /// </summary>
    public interface IEmbeddingService
    {
        /// <summary>
        /// Generates a normalized vector embedding for the input text.
        /// Uses local Ollama embedding models if reachable, with automatic fallback
        /// to local high-dimensional feature hashing to guarantee 100% uptime and determinism.
        /// </summary>
        Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default);

        /// <summary>
        /// Calculates the cosine similarity between two unit/normalized vectors (range -1.0 to 1.0).
        /// </summary>
        float ComputeCosineSimilarity(float[] vectorA, float[] vectorB);

        /// <summary>
        /// Performs hybrid reranking combining semantic cosine similarity and lexical token match scores.
        /// Final Score = (semanticWeight * SemanticScore) + ((1 - semanticWeight) * LexicalScore).
        /// </summary>
        Task<List<(T Item, double HybridScore)>> HybridRerankAsync<T>(
            string query,
            List<T> items,
            Func<T, string> textSelector,
            Func<T, double> lexicalScoreSelector,
            double semanticWeight = 0.6,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves observable runtime diagnostics about the active embedding provider and fallback status.
        /// </summary>
        EmbeddingProviderInfo GetProviderInfo();

        /// <summary>
        /// Invalidates and clears the in-memory vector cache to simulate application restarts or cache purges.
        /// </summary>
        void ClearCache();
    }

    /// <summary>
    /// Runtime diagnostics regarding embedding model health and fallback status.
    /// </summary>
    public class EmbeddingProviderInfo
    {
        public string ProviderName { get; set; } = "Local Ollama Engine (qwen2.5:7b)";
        public bool IsUsingFallback { get; set; }
        public int Dimensions { get; set; } = 128;
        public DateTime LastCheckedUtc { get; set; } = DateTime.UtcNow;
        public string? DiagnosticMessage { get; set; }
    }
}
