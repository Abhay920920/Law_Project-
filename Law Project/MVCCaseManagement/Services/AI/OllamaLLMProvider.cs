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

                var payload = new
                {
                    model = selectedModel,
                    messages = ollamaMessages,
                    stream = false,
                    options = new
                    {
                        temperature = request.Temperature,
                        num_predict = request.MaxTokens > 0 ? Math.Min(request.MaxTokens, 768) : 512,
                        num_ctx = 3072
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

                string text = string.Empty;
                if (root.TryGetProperty("message", out var msgElement) &&
                    msgElement.TryGetProperty("content", out var contentElement))
                {
                    text = contentElement.GetString() ?? string.Empty;
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
