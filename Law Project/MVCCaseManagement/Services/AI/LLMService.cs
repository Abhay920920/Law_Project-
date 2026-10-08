using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MVCCaseManagement.DAL;
using MVCCaseManagement.Models.AI;

namespace MVCCaseManagement.Services.AI
{
    public class LLMService : ILLMService, IDisposable
    {
        private readonly IEnumerable<ILLMProvider> _providers;
        private readonly AIOptions _options;
        private readonly NyayaPathaOptions _nyayaPathaOptions;
        private readonly DBHelper _db;
        private readonly IConfiguration _configuration;
        private readonly ILogger<LLMService> _logger;
        private readonly SemaphoreSlim _concurrencySemaphore;
        private int _activeRequests = 0;

        public LLMService(
            IEnumerable<ILLMProvider> providers,
            IOptions<AIOptions> options,
            IOptions<NyayaPathaOptions> nyayaPathaOptions,
            ILogger<LLMService> logger)
            : this(providers, options, nyayaPathaOptions, null, null, logger)
        {
        }

        public LLMService(
            IEnumerable<ILLMProvider> providers,
            IOptions<AIOptions> options,
            IOptions<NyayaPathaOptions> nyayaPathaOptions,
            DBHelper? db,
            IConfiguration? configuration,
            ILogger<LLMService> logger)
        {
            _providers = providers ?? throw new ArgumentNullException(nameof(providers));
            _options = options?.Value ?? new AIOptions();
            _nyayaPathaOptions = nyayaPathaOptions?.Value ?? new NyayaPathaOptions();
            _db = db;
            _configuration = configuration;
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            int maxConcurrent = Math.Max(1, _options.MaxConcurrentRequests > 0 ? _options.MaxConcurrentRequests : 5);
            _concurrencySemaphore = new SemaphoreSlim(maxConcurrent, maxConcurrent);
        }

        public AIOptions GetCurrentOptions() => _options;

        public async Task<LLMResponse> GenerateAsync(LLMRequest request, CancellationToken cancellationToken = default)
        {
            if (request == null)
            {
                return new LLMResponse { Success = false, ErrorMessage = "LLM request payload cannot be null." };
            }

            // 1. Kill Switch Check
            if (!_options.Enabled || !_nyayaPathaOptions.IsEnabled)
            {
                _logger.LogWarning("LLM generation request rejected: AI Kill Switch is engaged.");
                return new LLMResponse
                {
                    Success = false,
                    ErrorMessage = "Nyaya Patha AI Assistant is currently deactivated in system settings."
                };
            }

            // 2. Input Length Rate Limiting / DoS Protection
            int totalInputLength = (request.SystemPrompt?.Length ?? 0) +
                                   request.Messages.Sum(m => m.MessageText?.Length ?? 0);
            if (_options.MaxInputLength > 0 && totalInputLength > (_options.MaxInputLength * 10)) // Context allows up to 10x query length
            {
                _logger.LogWarning("LLM generation request rejected: Total input length ({Length}) exceeds safe maximum.", totalInputLength);
                return new LLMResponse
                {
                    Success = false,
                    ErrorMessage = "Context payload exceeds maximum allowable length. Please refine your query or active case documents."
                };
            }

            // 3. Concurrency Throttling (Semaphore)
            bool acquired = await _concurrencySemaphore.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);
            if (!acquired)
            {
                _logger.LogWarning("AI generation rejected due to concurrency saturation. Max concurrent requests: {Max}", _options.MaxConcurrentRequests);
                return new LLMResponse
                {
                    Success = false,
                    ErrorMessage = "The AI assistant is currently processing maximum legal research requests. Please try again in a few moments."
                };
            }

            Interlocked.Increment(ref _activeRequests);

            try
            {
                // 4. Resolve Primary Provider
                string primaryName = _options.Provider ?? "Ollama";
                var primaryProvider = _providers.FirstOrDefault(p =>
                    string.Equals(p.ProviderName, primaryName, StringComparison.OrdinalIgnoreCase))
                    ?? _providers.FirstOrDefault(p => p.ProviderName == "Ollama")
                    ?? _providers.FirstOrDefault();

                if (primaryProvider == null)
                {
                    return new LLMResponse
                    {
                        Success = false,
                        ErrorMessage = $"No LLM provider available for configured provider '{primaryName}'."
                    };
                }

                _logger.LogInformation("Executing legal research inference using primary provider {Provider}", primaryProvider.ProviderName);
                var response = await primaryProvider.GenerateAsync(request, cancellationToken);

                if (response.Success)
                {
                    return response;
                }

                // 5. Fallback Provider Handling (if enabled and primary failed)
                if (!string.IsNullOrWhiteSpace(_options.FallbackProvider) &&
                    !string.Equals(_options.FallbackProvider, primaryProvider.ProviderName, StringComparison.OrdinalIgnoreCase))
                {
                    var fallbackProvider = _providers.FirstOrDefault(p =>
                        string.Equals(p.ProviderName, _options.FallbackProvider, StringComparison.OrdinalIgnoreCase));

                    if (fallbackProvider != null)
                    {
                        // Enforce External Provider Transmission Security Policy
                        if ((fallbackProvider.ProviderName == "OpenRouter" || fallbackProvider.ProviderName == "Claude") &&
                            !_options.AllowExternalProviders)
                        {
                            _logger.LogWarning("Fallback provider {Fallback} skipped: AI:AllowExternalProviders is false.", fallbackProvider.ProviderName);
                            return response; // Return primary response with explanation
                        }

                        _logger.LogWarning("Primary provider {Primary} failed ({Error}). Invoking configured fallback provider {Fallback}.",
                            primaryProvider.ProviderName, response.ErrorMessage, fallbackProvider.ProviderName);

                        var fallbackResponse = await fallbackProvider.GenerateAsync(request, cancellationToken);
                        if (fallbackResponse.Success)
                        {
                            fallbackResponse.IsFallback = true;
                            return fallbackResponse;
                        }
                    }
                }

                return response;
            }
            finally
            {
                Interlocked.Decrement(ref _activeRequests);
                _concurrencySemaphore.Release();
            }
        }

        public async Task<AIHealthReportDto> CheckHealthAsync(CancellationToken cancellationToken = default)
        {
            var report = new AIHealthReportDto
            {
                IsEnabled = _options.Enabled && _nyayaPathaOptions.IsEnabled,
                Provider = _options.Provider ?? "Ollama",
                ConfiguredModel = _options.Model ?? "qwen2.5:14b",
                AllowExternalProviders = _options.AllowExternalProviders,
                ActiveConcurrentRequests = _activeRequests
            };

            if (!report.IsEnabled)
            {
                report.Status = "Disabled";
                report.Message = "Nyaya Patha AI is currently disabled in system configuration (Kill switch engaged).";
                return report;
            }

            // 1. Check Primary LLM Provider Reachability
            var primary = _providers.FirstOrDefault(p =>
                string.Equals(p.ProviderName, report.Provider, StringComparison.OrdinalIgnoreCase));

            if (primary != null)
            {
                report.IsProviderReachable = await primary.IsAvailableAsync(cancellationToken);
            }
            else
            {
                report.IsProviderReachable = false;
            }

            // 2. Check Database Connectivity
            try
            {
                using var conn = _db.GetConnection();
                await conn.OpenAsync(cancellationToken);
                using var cmd = new SqlCommand("SELECT 1", conn);
                var scalar = await cmd.ExecuteScalarAsync(cancellationToken);
                report.IsDatabaseConnected = (scalar != null && Convert.ToInt32(scalar) == 1);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "AI Health check: Database probe failed.");
                report.IsDatabaseConnected = false;
            }

            // 3. Check e-Courts Configuration
            string deptId = _configuration["eCourts:DeptId"] ?? string.Empty;
            string gateway = _configuration["eCourts:GatewayUrl"] ?? string.Empty;
            report.IsECourtsConfigured = !string.IsNullOrWhiteSpace(deptId) && !string.IsNullOrWhiteSpace(gateway);

            // Determine Overall Status
            if (report.IsProviderReachable && report.IsDatabaseConnected)
            {
                report.Status = "Healthy";
                report.Message = $"Nyaya Patha AI operational with {report.Provider} ({report.ConfiguredModel}).";
            }
            else if (report.IsDatabaseConnected && !report.IsProviderReachable)
            {
                report.Status = "Degraded";
                report.Message = $"{report.Provider} inference service is unreachable at configured endpoint. Verified database records remain operational.";
            }
            else
            {
                report.Status = "Unhealthy";
                report.Message = "Core database or AI dependencies are unavailable.";
            }

            return report;
        }

        public void Dispose()
        {
            _concurrencySemaphore?.Dispose();
        }
    }
}
