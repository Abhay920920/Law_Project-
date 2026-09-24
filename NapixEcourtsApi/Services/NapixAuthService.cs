using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Options;
using NapixEcourtsApi.Configuration;

namespace NapixEcourtsApi.Services;

public interface INapixAuthService
{
    /// <summary>Returns a valid bearer token, fetching a new one only if the cached one expired.</summary>
    Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default);

    /// <summary>Invalidates the cached token to force a fresh fetch on next call.</summary>
    void InvalidateToken();
}

public class NapixAuthService : INapixAuthService
{
    private readonly HttpClient _httpClient;
    private readonly NapixOptions _options;
    private readonly SemaphoreSlim _lock = new(1, 1);

    private string? _cachedToken;
    private DateTimeOffset _expiresAtUtc = DateTimeOffset.MinValue;

    // Refresh a little early so a long-running request never gets caught mid-expiry.
    private static readonly TimeSpan SafetyMargin = TimeSpan.FromSeconds(30);

    public NapixAuthService(HttpClient httpClient, IOptions<NapixOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public void InvalidateToken()
    {
        _cachedToken = null;
        _expiresAtUtc = DateTimeOffset.MinValue;
    }

    public async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        if (_cachedToken is not null && DateTimeOffset.UtcNow < _expiresAtUtc - SafetyMargin)
        {
            return _cachedToken;
        }

        await _lock.WaitAsync(cancellationToken);
        try
        {
            // Re-check after acquiring the lock in case another request already refreshed it.
            if (_cachedToken is not null && DateTimeOffset.UtcNow < _expiresAtUtc - SafetyMargin)
            {
                return _cachedToken;
            }

            HttpResponseMessage? response = null;
            string body = string.Empty;
            Exception? lastEx = null;

            for (int attempt = 1; attempt <= 3; attempt++)
            {
                try
                {
                    using var request = new HttpRequestMessage(HttpMethod.Post, _options.TokenUrl);

                    var credentials = Convert.ToBase64String(
                        System.Text.Encoding.ASCII.GetBytes($"{_options.ClientId}:{_options.ClientSecret}"));
                    request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
                    request.Headers.Add("X-IBM-Client-Id", _options.ClientId);
                    if (!string.IsNullOrWhiteSpace(_options.ClientSecret))
                    {
                        request.Headers.Add("X-IBM-Client-Secret", _options.ClientSecret);
                    }

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
                throw new NapixApiException(
                    $"NAPIX token request failed ({(int)(response?.StatusCode ?? 0)}): {body}", lastEx);
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

                _cachedToken = accessToken;
                _expiresAtUtc = DateTimeOffset.UtcNow.AddSeconds(expiresIn);

                return _cachedToken;
            }
        }
        finally
        {
            _lock.Release();
        }
    }
}

public class NapixApiException : Exception
{
    public NapixApiException(string message) : base(message) { }
    public NapixApiException(string message, Exception? inner = null) : base(message, inner) { }
}

public class NapixNotFoundException : NapixApiException
{
    public NapixNotFoundException(string message) : base(message) { }
    public NapixNotFoundException(string message, Exception? inner = null) : base(message, inner) { }
}


