using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MVCCaseManagement.Models.AI;

namespace MVCCaseManagement.Services.AI
{
    /// <summary>
    /// Primary LLM Provider: Ollama running local open-weight models (e.g. Qwen 2.5, DeepSeek, Nemotron).
    /// Operates entirely on-premises, keeping sensitive judicial and corporate data local.
    /// </summary>
    public class OllamaLLMProvider : ILLMProvider
    {
        private readonly HttpClient _httpClient;
        private readonly AIOptions _options;
        private readonly ILogger<OllamaLLMProvider> _logger;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        public string ProviderName => "Ollama";

        public OllamaLLMProvider(
            HttpClient httpClient,
            IOptions<AIOptions> options,
            ILogger<OllamaLLMProvider> logger)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _options = options?.Value ?? new AIOptions();
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            int timeoutSec = Math.Max(15, _options.TimeoutSeconds);
            if (_httpClient.Timeout != TimeSpan.FromSeconds(timeoutSec))
            {
                _httpClient.Timeout = TimeSpan.FromSeconds(timeoutSec);
            }
        }

        public async Task<LLMResponse> GenerateAsync(LLMRequest request, CancellationToken cancellationToken = default)
        {
            var sw = Stopwatch.StartNew();
            string selectedModel = !string.IsNullOrWhiteSpace(request.ModelOverride) ? request.ModelOverride : _options.Model;

            try
            {
                string baseUrl = (_options.BaseUrl ?? "http://127.0.0.1:11434").TrimEnd('/');
                string chatEndpoint = $"{baseUrl}/api/chat";

                // Format messages for Ollama Chat API
                var ollamaMessages = new List<object>();

                if (!string.IsNullOrWhiteSpace(request.SystemPrompt))
                {
                    ollamaMessages.Add(new { role = "system", content = request.SystemPrompt });
                }

                foreach (var msg in request.Messages.Where(m => !string.IsNullOrWhiteSpace(m.MessageText)))
                {
                    string role = string.Equals(msg.Role, "assistant", StringComparison.OrdinalIgnoreCase)
                        ? "assistant"
                        : "user";
                    ollamaMessages.Add(new { role, content = msg.MessageText });
                }

                // Pre-flight: estimate token count (1 token ≈ 4 chars) and warn if near context limit.
                int estimatedChars = (request.SystemPrompt?.Length ?? 0)
                    + request.Messages.Sum(m => m.MessageText?.Length ?? 0);
                const int MaxContextChars = 24000; // ~6000 tokens at 4 chars/token, safe for 8192 ctx
                if (estimatedChars > MaxContextChars)
                {
                    _logger.LogWarning(
                        "Ollama prompt for model {Model} is very large ({Chars} chars, ~{Tokens} tokens estimated). " +
                        "This may exceed the model context window and produce an empty response.",
                        selectedModel, estimatedChars, estimatedChars / 4);
                }

                var payload = new
                {
                    model = selectedModel,
                    messages = ollamaMessages,
                    stream = false,
                    options = new
                    {
                        temperature = request.Temperature > 0 ? request.Temperature : 0.2,
                        num_predict = request.MaxTokens > 0 ? request.MaxTokens : 1024,
                        num_ctx = 4096,  // Optimized from 8192 to accelerate prompt prefill and reduce RAM/VRAM load
                        top_p = 0.9,
                        repeat_penalty = 1.1
                    }
                };

                string jsonPayload = JsonSerializer.Serialize(payload, JsonOptions);
                using var httpContent = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

                _logger.LogInformation("Dispatching LLM query to local Ollama server at {Endpoint} for model {Model}", chatEndpoint, selectedModel);

                using var response = await _httpClient.PostAsync(chatEndpoint, httpContent, cancellationToken);
                string responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

                sw.Stop();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Ollama responded with HTTP {StatusCode}: {ResponseBody}", response.StatusCode, responseBody);
                    return new LLMResponse
                    {
                        Success = false,
                        Provider = ProviderName,
                        Model = selectedModel,
                        ExecutionTimeMs = (int)sw.ElapsedMilliseconds,
                        ErrorMessage = $"Ollama returned HTTP {(int)response.StatusCode} ({response.ReasonPhrase}): {responseBody}"
                    };
                }

                using var doc = JsonDocument.Parse(responseBody);
                var root = doc.RootElement;

                // Ollama can return HTTP 200 with {"error": "..."} when the model fails to produce output.
                // This happens when the context window is exceeded, the model produces empty output,
                // or the model encounters a generation error (e.g. "model output must contain either
                // output text or tool calls, these cannot both be empty").
                if (root.TryGetProperty("error", out var errorElement))
                {
                    string ollamaError = errorElement.GetString() ?? "Unknown Ollama model error";
                    _logger.LogWarning("Ollama returned HTTP 200 but with an error payload for model {Model}: {Error}", selectedModel, ollamaError);
                    return new LLMResponse
                    {
                        Success = false,
                        Provider = ProviderName,
                        Model = selectedModel,
                        ExecutionTimeMs = (int)sw.ElapsedMilliseconds,
                        ErrorMessage = $"Ollama model error: {ollamaError}"
                    };
                }

                string text = string.Empty;
                if (root.TryGetProperty("message", out var msgElement) &&
                    msgElement.TryGetProperty("content", out var contentElement))
                {
                    text = contentElement.GetString() ?? string.Empty;
                }

                // Guard: model produced an empty response without an error key.
                // Treat as a soft failure so the caller can use the offline fallback.
                if (string.IsNullOrWhiteSpace(text))
                {
                    _logger.LogWarning("Ollama returned HTTP 200 but model produced empty content for model {Model}. Raw body (first 300 chars): {Raw}",
                        selectedModel, responseBody.Length > 300 ? responseBody.Substring(0, 300) : responseBody);
                    return new LLMResponse
                    {
                        Success = false,
                        Provider = ProviderName,
                        Model = selectedModel,
                        ExecutionTimeMs = (int)sw.ElapsedMilliseconds,
                        ErrorMessage = $"Local Ollama model ({selectedModel}) produced an empty response. The prompt may be too long for the model's context window, or the model stalled. The offline legal intelligence engine will be used instead."
                    };
                }

                int totalTokens = 0;
                if (root.TryGetProperty("eval_count", out var evalCountElement))
                {
                    totalTokens = evalCountElement.GetInt32();
                }

                return new LLMResponse
                {
                    Success = true,
                    Content = text.Trim(),
                    Provider = ProviderName,
                    Model = selectedModel,
                    TotalTokens = totalTokens,
                    ExecutionTimeMs = (int)sw.ElapsedMilliseconds
                };
            }
            catch (HttpRequestException ex)
            {
                sw.Stop();
                _logger.LogWarning(ex, "Ollama HTTP connectivity error on model {Model}: {Message}", selectedModel, ex.Message);
                return new LLMResponse
                {
                    Success = false,
                    Provider = ProviderName,
                    Model = selectedModel,
                    ExecutionTimeMs = (int)sw.ElapsedMilliseconds,
                    ErrorMessage = $"Local Ollama service unreachable at {_options.BaseUrl} ({ex.Message}). Ensure the Ollama service is running."
                };
            }
            catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
            {
                sw.Stop();
                _logger.LogWarning("Ollama request timed out after {Elapsed}ms for model {Model}", sw.ElapsedMilliseconds, selectedModel);
                return new LLMResponse
                {
                    Success = false,
                    Provider = ProviderName,
                    Model = selectedModel,
                    ExecutionTimeMs = (int)sw.ElapsedMilliseconds,
                    ErrorMessage = $"Ollama request timed out after {_options.TimeoutSeconds} seconds. Consider allocating more server resources or selecting a smaller model."
                };
            }
            catch (Exception ex)
            {
                sw.Stop();
                _logger.LogError(ex, "Unexpected error executing Ollama inference on model {Model}", selectedModel);
                return new LLMResponse
                {
                    Success = false,
                    Provider = ProviderName,
                    Model = selectedModel,
                    ExecutionTimeMs = (int)sw.ElapsedMilliseconds,
                    ErrorMessage = $"Inference engine error: {ex.Message}"
                };
            }
        }

        public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                string baseUrl = (_options.BaseUrl ?? "http://127.0.0.1:11434").TrimEnd('/');
                string versionEndpoint = $"{baseUrl}/api/version";

                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(TimeSpan.FromSeconds(3)); // fast 3-second ping

                using var response = await _httpClient.GetAsync(versionEndpoint, cts.Token);
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }
    }
}
