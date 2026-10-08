using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Options;
using NapixEcourtsApi.Configuration;

namespace NapixEcourtsApi.Services;

public interface INapixAuthService
{
    /// <summary>Returns a valid bearer token for the default/MVC module.</summary>
    Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default);

    /// <summary>Returns a valid bearer token for the specified module ("MVC" or "Labour").</summary>
    Task<string> GetAccessTokenAsync(string? module, CancellationToken cancellationToken = default);

    /// <summary>Invalidates cached token for a specific module or all modules.</summary>
    void InvalidateToken(string? module = null);
}

public class NapixAuthService : INapixAuthService
{
    private readonly HttpClient _httpClient;
    private readonly NapixOptions _options;

    private class ModuleTokenState
    {
        public readonly SemaphoreSlim Lock = new(1, 1);
        public string? CachedToken;
        public DateTimeOffset ExpiresAtUtc = DateTimeOffset.MinValue;
    }

    private readonly ConcurrentDictionary<string, ModuleTokenState> _states = new(StringComparer.OrdinalIgnoreCase);

    // Refresh a little early so a long-running request never gets caught mid-expiry.
    private static readonly TimeSpan SafetyMargin = TimeSpan.FromSeconds(30);

    public NapixAuthService(HttpClient httpClient, IOptions<NapixOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    private string NormalizeModule(string? module)
    {
        if (string.IsNullOrWhiteSpace(module)) return "MVC";
        return module.Trim().Equals("Labour", StringComparison.OrdinalIgnoreCase) ? "Labour" : "MVC";
    }

    public void InvalidateToken(string? module = null)
    {
        if (string.IsNullOrWhiteSpace(module))
        {
            foreach (var state in _states.Values)
            {
                state.CachedToken = null;
                state.ExpiresAtUtc = DateTimeOffset.MinValue;
            }
        }
        else
        {
            var norm = NormalizeModule(module);
            if (_states.TryGetValue(norm, out var state))
            {
                state.CachedToken = null;
                state.ExpiresAtUtc = DateTimeOffset.MinValue;
            }
        }
    }

    public Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default)
        => GetAccessTokenAsync("MVC", cancellationToken);

    public async Task<string> GetAccessTokenAsync(string? module, CancellationToken cancellationToken = default)
    {
        string norm = NormalizeModule(module);
        var creds = _options.GetModuleCredentials(norm);
        var state = _states.GetOrAdd(norm, _ => new ModuleTokenState());

        if (state.CachedToken is not null && DateTimeOffset.UtcNow < state.ExpiresAtUtc - SafetyMargin)
        {
            return state.CachedToken;
        }

        await state.Lock.WaitAsync(cancellationToken);
        try
        {
            // Re-check after acquiring the lock
            if (state.CachedToken is not null && DateTimeOffset.UtcNow < state.ExpiresAtUtc - SafetyMargin)
            {
                return state.CachedToken;
            }

            HttpResponseMessage? response = null;
            string body = string.Empty;
            Exception? lastEx = null;

            for (int attempt = 1; attempt <= 3; attempt++)
            {
                try
                {
                    // As per NAPIX Subscriber Manual (Annexure A & Section 14):
                    // Token URL: https://delhigw.napix.gov.in/nic/ecourts//oauth2/token
                    // Headers: ONLY Basic Auth and Content-Type: application/x-www-form-urlencoded
                    // Body: ONLY grant_type=client_credentials and scope=napix
                    string tokenUrl = _options.TokenUrl;
                    if (!tokenUrl.Contains("//oauth2"))
                    {
                        tokenUrl = tokenUrl.Replace("/oauth2", "//oauth2");
                    }

                    using var request = new HttpRequestMessage(HttpMethod.Post, tokenUrl);

                    var credentials = Convert.ToBase64String(
                        System.Text.Encoding.ASCII.GetBytes($"{creds.ClientId}:{creds.ClientSecret}"));
                    request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
                    request.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
                    request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                    request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
                    {
                        ["grant_type"] = "client_credentials",
                        ["scope"] = "napix",
                    });

                    response?.Dispose();
                    response = await _httpClient.SendAsync(request, cancellationToken);
                    body = await response.Content.ReadAsStringAsync(cancellationToken);
                    if (response.IsSuccessStatusCode)
                    {
                        lastEx = null;
                        break;
                    }
                }
                catch (Exception ex) when (attempt < 3 && !cancellationToken.IsCancellationRequested)
                {
                    lastEx = ex;
                    await Task.Delay(300 * attempt, cancellationToken);
                }
            }

            if (response == null || !response.IsSuccessStatusCode)
            {
                var msg = $"NAPIX token request failed for {norm} ({(int)(response?.StatusCode ?? 0)}): {body}";
                throw lastEx != null ? new NapixApiException(msg, lastEx) : new NapixApiException(msg);
            }

            using (response)
            {
                using var doc = JsonDocument.Parse(body);
                var root = doc.RootElement;

                var accessToken = root.GetProperty("access_token").GetString()
                    ?? throw new NapixApiException("Token response did not contain access_token.");
                var expiresIn = root.TryGetProperty("expires_in", out var expiresInEl)
                    ? expiresInEl.GetInt32()
                    : 3600;

                state.CachedToken = accessToken;
                state.ExpiresAtUtc = DateTimeOffset.UtcNow.AddSeconds(expiresIn);

                return state.CachedToken;
            }
        }
        finally
        {
            state.Lock.Release();
        }
    }
}

public class NapixApiException : Exception
{
    public NapixApiException(string message) : base(message) { }
    public NapixApiException(string message, Exception inner) : base(message, inner) { }
}

public class NapixNotFoundException : NapixApiException
{
    public NapixNotFoundException(string message) : base(message) { }
    public NapixNotFoundException(string message, Exception inner) : base(message, inner) { }
}

