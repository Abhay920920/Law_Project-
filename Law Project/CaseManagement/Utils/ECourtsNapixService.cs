using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace MVCCaseManagement.Utils
{
    public class ECourtsNapixService : IECourtsNapixService
    {
        private readonly HttpClient _httpClient;
        private readonly IMemoryCache _cache;
        private readonly IConfiguration _config;
        private readonly ILogger<ECourtsNapixService> _logger;
        private readonly NapixQuotaService _quota;
        private readonly System.Collections.Concurrent.ConcurrentDictionary<string, string> _lastModuleErrors = new(StringComparer.OrdinalIgnoreCase);

        public string? GetLastModuleError(string module = "MVC") =>
            _lastModuleErrors.TryGetValue(NapixQuotaService.NormalizeModule(module), out var err) ? err : null;

        public void ResetCircuitBreaker()
        {
            _cache.Remove("eCourts_OAuth_Token_MVC");
            _cache.Remove("eCourts_OAuth_Token_Labour");
            _cache.Remove("eCourts_OAuth_Token_HC");
            _lastModuleErrors.Clear();
            _logger.LogInformation("[NAPIX] OAuth tokens and module error caches manually reset.");
        }

        private class ModuleNapixConfig
        {
            public string ApiKey { get; init; } = "";
            public string SecretKey { get; init; } = "";
            public string DeptId { get; init; } = "clonwkrtc";
            public string HmacKey { get; init; } = "15081947";
            public string AuthKey { get; init; } = "";
            public string IV { get; init; } = "";
            public string Iv => IV;
            public string Version { get; init; } = "v1.0";
            public string GatewayUrl { get; init; } = "https://delhigw.napix.gov.in/nic/ecourts";
            public string TokenUrl { get; init; } = "https://delhigw.napix.gov.in/nic/ecourts/oauth2/token";
        }

        private ModuleNapixConfig GetConfigForModule(string module)
        {
            string norm = NapixQuotaService.NormalizeModule(module);
            var sec = _config.GetSection($"eCourts:{norm}");

            string apiKey    = sec["ApiKey"] ?? _config["eCourts:ApiKey"] ?? Environment.GetEnvironmentVariable($"ECOURTS_{norm.ToUpperInvariant()}_APIKEY") ?? Environment.GetEnvironmentVariable("ECOURTS__APIKEY") ?? "";
            string secretKey = sec["SecretKey"] ?? _config["eCourts:SecretKey"] ?? Environment.GetEnvironmentVariable($"ECOURTS_{norm.ToUpperInvariant()}_SECRETKEY") ?? Environment.GetEnvironmentVariable("ECOURTS__SECRETKEY") ?? "";
            string deptId    = sec["DeptId"] ?? _config["eCourts:DeptId"] ?? "clonwkrtc";
            string hmacKey   = sec["HmacKey"] ?? _config["eCourts:HmacKey"] ?? "15081947";
            string authKey   = sec["AuthKey"] ?? _config["eCourts:AuthKey"] ?? "";
            string iv        = sec["IV"] ?? _config["eCourts:IV"] ?? authKey;
            string version   = sec["Version"] ?? _config["eCourts:Version"] ?? "v1.0";
            string gateway   = sec["GatewayUrl"] ?? _config["eCourts:GatewayUrl"] ?? "https://delhigw.napix.gov.in/nic/ecourts";
            string tokenUrl  = sec["OAuthTokenUrl"] ?? _config["eCourts:OAuthTokenUrl"] ?? "https://delhigw.napix.gov.in/nic/ecourts/oauth2/token";

            return new ModuleNapixConfig
            {
                ApiKey = apiKey,
                SecretKey = secretKey,
                DeptId = deptId,
                HmacKey = hmacKey,
                AuthKey = authKey,
                IV = iv,
                Version = version,
                GatewayUrl = gateway,
                TokenUrl = tokenUrl
            };
        }

        public ECourtsNapixService(
            HttpClient httpClient,
            IMemoryCache cache,
            IConfiguration config,
            ILogger<ECourtsNapixService> logger,
            NapixQuotaService quota)
        {
            _httpClient = httpClient;
            _cache = cache;
            _config = config;
            _logger = logger;
            _quota  = quota;

            if (!_httpClient.DefaultRequestHeaders.Contains("User-Agent"))
            {
                _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36");
            }
        }

        public async Task<string> GetAccessTokenAsync(string module = "MVC")
        {
            string norm = NapixQuotaService.NormalizeModule(module);
            string cacheKey = $"eCourts_OAuth_Token_{norm}";
            try
            {
                if (_cache.TryGetValue(cacheKey, out string? cachedToken) && !string.IsNullOrWhiteSpace(cachedToken))
                    return cachedToken;

                string freshToken = await FetchFreshTokenDirectAsync(norm);
                if (!string.IsNullOrWhiteSpace(freshToken))
                {
                    _cache.Set(cacheKey, freshToken, TimeSpan.FromMinutes(50));
                    return freshToken;
                }

                return string.Empty;
            }
            catch (ObjectDisposedException)
            {
                return await FetchFreshTokenDirectAsync(norm);
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Unable to connect to eCourts NAPIX Gateway for OAuth token ({norm}): {ex.Message}");
                return string.Empty;
            }
        }

        private async Task<string> FetchFreshTokenDirectAsync(string module = "MVC")
        {
            var cfg = GetConfigForModule(module);
            try
            {
                var authHeader = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{cfg.ApiKey}:{cfg.SecretKey}"));

                // As specified in NAPIX Subscriber Manual (Annexure A & Section 14/15/16):
                // Token URL contains //oauth2/token; fallback to single slash /oauth2/token if needed
                var tokenUrls = new[]
                {
                    cfg.TokenUrl.Contains("//oauth2") ? cfg.TokenUrl : cfg.TokenUrl.Replace("/oauth2", "//oauth2"),
                    cfg.TokenUrl.Replace("//oauth2", "/oauth2")
                }.Distinct();

                foreach (var tokenUrl in tokenUrls)
                {
                    using var request = new HttpRequestMessage(HttpMethod.Post, tokenUrl);
                    request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", authHeader);
                    request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
                    request.Headers.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36");

                    // Per NAPIX Subscriber Manual, body contains ONLY grant_type and scope
                    request.Content = new FormUrlEncodedContent(new[]
                    {
                        new KeyValuePair<string, string>("grant_type", "client_credentials"),
                        new KeyValuePair<string, string>("scope", "napix")
                    });

                    var response = await _httpClient.SendAsync(request);
                    var jsonStr = await response.Content.ReadAsStringAsync();

                    if (response.IsSuccessStatusCode)
                    {
                        using var doc = JsonDocument.Parse(jsonStr);
                        string token = doc.RootElement.GetProperty("access_token").GetString() ?? string.Empty;
                        if (!string.IsNullOrWhiteSpace(token))
                        {
                            _logger.LogInformation($"[NAPIX] Successfully acquired OAuth access token for {NapixQuotaService.NormalizeModule(module)} from {tokenUrl}");
                            return token;
                        }
                    }
                    else
                    {
                        _logger.LogWarning($"eCourts NAPIX OAuth token request to {tokenUrl} failed ({NapixQuotaService.NormalizeModule(module)}) with status {response.StatusCode}: {jsonStr}");
                    }
                }

                if (!string.Equals(NapixQuotaService.NormalizeModule(module), "MVC", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogWarning($"[NAPIX] Module '{module}' OAuth credentials failed. Gracefully falling back to active MVC credentials.");
                    return await FetchFreshTokenDirectAsync("MVC");
                }

                return string.Empty;
            }
            catch (ObjectDisposedException)
            {
                return string.Empty;
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Direct OAuth token fetch failed ({NapixQuotaService.NormalizeModule(module)}): {ex.Message}");
                return string.Empty;
            }
        }

        public async Task<JsonElement?> QueryApiAsync(string endpointBasePath, string endpointAction, string pipeParameters, string module = "MVC")
        {
            string norm = NapixQuotaService.NormalizeModule(module);
            var cfg = GetConfigForModule(norm);


            var _sw = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                var token = await GetAccessTokenAsync(norm);
                if (string.IsNullOrEmpty(token))
                {
                    return null;
                }

                // Step 1: Encrypt plaintext payload via AES-128-CBC
                string encryptedBase64 = AesEncrypt(pipeParameters, cfg.AuthKey, cfg.IV);
                string requestStr = Uri.EscapeDataString(encryptedBase64);

                // Step 2: Generate HMAC-SHA256 signature
                string requestToken = HmacSha256(pipeParameters, cfg.HmacKey);

                // Step 3: Construct request URL
                string requestUrl = $"{cfg.GatewayUrl}/{endpointBasePath}/{endpointAction}?" +
                                    $"dept_id={Uri.EscapeDataString(cfg.DeptId)}&" +
                                    $"request_str={requestStr}&" +
                                    $"request_token={requestToken}&" +
                                    $"version={cfg.Version}";

                bool isPostEndpoint = endpointBasePath.Contains("current-status-api", StringComparison.OrdinalIgnoreCase);
                var httpMethod = isPostEndpoint ? HttpMethod.Post : HttpMethod.Get;

                using var request = new HttpRequestMessage(httpMethod, requestUrl);
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
                request.Headers.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36");

                HttpResponseMessage response;
                try
                {
                    response = await _httpClient.SendAsync(request);
                }
                catch (Exception ex) when (ex is HttpRequestException || ex is System.IO.IOException)
                {
                    _logger.LogWarning("[NAPIX] Transient connection reset for {Endpoint}/{Action} ({Module}). Retrying in 400ms...", endpointBasePath, endpointAction, norm);
                    await Task.Delay(400);
                    using var retryReq = new HttpRequestMessage(httpMethod, requestUrl);
                    retryReq.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                    retryReq.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
                    retryReq.Headers.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36");
                    response = await _httpClient.SendAsync(retryReq);
                }

                var jsonResp = await response.Content.ReadAsStringAsync();
                _sw.Stop();

                // ── Log the call to quota tracker ───────────────────────────
                string? cnrHint = ExtractCnrHintFromParams(pipeParameters);
                _quota.TrackCall(
                    $"{endpointBasePath}/{endpointAction}",
                    (int)response.StatusCode,
                    response.IsSuccessStatusCode,
                    null,
                    cnrHint,
                    (int)_sw.ElapsedMilliseconds,
                    norm);

                if (string.IsNullOrWhiteSpace(jsonResp)) return null;

                // Handle genuine HTTP 401 Unauthorized by evicting OAuth token and retrying once
                // Note: Do NOT evict on 626 / INVALID_TOKEN which is eCourts HMAC signature verification failure
                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized &&
                    !jsonResp.Contains("626") &&
                    !jsonResp.Contains("INVALID_TOKEN", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogWarning($"[NAPIX] Genuine 401 Unauthorized for {endpointBasePath}/{endpointAction} ({norm}). Refreshing token and retrying once...");
                    string cacheKey = $"eCourts_OAuth_Token_{norm}";
                    _cache.Remove(cacheKey);

                    string freshToken = await FetchFreshTokenDirectAsync(norm);
                    if (!string.IsNullOrEmpty(freshToken))
                    {
                        _cache.Set(cacheKey, freshToken, TimeSpan.FromMinutes(50));
                        using var retryReq = new HttpRequestMessage(httpMethod, requestUrl);
                        retryReq.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", freshToken);
                        retryReq.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
                        retryReq.Headers.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36");

                        response = await _httpClient.SendAsync(retryReq);
                        jsonResp = await response.Content.ReadAsStringAsync();
                    }
                }

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning($"NAPIX API call to {endpointBasePath}/{endpointAction} ({norm}) status: {response.StatusCode}. Body: {jsonResp}");

                    string decryptedErr = "";
                    try
                    {
                        using var errDoc = JsonDocument.Parse(jsonResp);
                        if (errDoc.RootElement.TryGetProperty("status", out var stEl) && stEl.ValueKind == JsonValueKind.String)
                        {
                            decryptedErr = AesDecrypt(stEl.GetString() ?? "", cfg.AuthKey, cfg.IV);
                        }
                    }
                    catch { }

                    if (decryptedErr.Contains("626") || decryptedErr.Contains("INVALID_TOKEN") || jsonResp.Contains("626"))
                    {
                        _lastModuleErrors[norm] = $"626 (INVALID_TOKEN): eCourts CIS backend rejected request token for dept_id '{cfg.DeptId}'.";
                    }
                    else if (jsonResp.Contains("URL Open error") || jsonResp.Contains("Could not connect to endpoint"))
                    {
                        _lastModuleErrors[norm] = "500 (GATEWAY_BACKEND_OFFLINE): NAPIX Gateway could not connect to eCourts CIS backend server at NIC (URL Open error).";
                    }
                    else if (response.StatusCode == System.Net.HttpStatusCode.InternalServerError)
                    {
                        _lastModuleErrors[norm] = $"500 (GATEWAY_ERROR): {jsonResp}";
                    }
                    else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                    {
                        _lastModuleErrors[norm] = "401 (UNAUTHORIZED): NAPIX Gateway rejected OAuth token.";
                    }
                    else if (response.StatusCode == System.Net.HttpStatusCode.BadRequest)
                    {
                        _lastModuleErrors[norm] = !string.IsNullOrEmpty(decryptedErr)
                            ? $"400 (BAD_REQUEST): {decryptedErr}"
                            : $"400 (BAD_REQUEST): {jsonResp}";
                    }
                }

                if (jsonResp.TrimStart().StartsWith("Array", StringComparison.OrdinalIgnoreCase))
                {
                    var codeMatch = System.Text.RegularExpressions.Regex.Match(jsonResp, @"\[status_code\]\s*=>\s*([^\s\]]+)");
                    var statusMatch = System.Text.RegularExpressions.Regex.Match(jsonResp, @"\[status\]\s*=>\s*([^\s\]]+)");
                    string statusCode = codeMatch.Success ? codeMatch.Groups[1].Value : "626";
                    string statusText = statusMatch.Success ? statusMatch.Groups[1].Value : "INVALID_TOKEN";
                    string syntheticJson = $"{{\"status_code\":\"{statusCode}\",\"status\":\"{statusText}\"}}";
                    _logger.LogWarning($"NAPIX API returned PHP Array response for {endpointBasePath}/{endpointAction} ({norm}): parsed as {syntheticJson}");
                    using var tempDoc = JsonDocument.Parse(syntheticJson);
                    return tempDoc.RootElement.Clone();
                }

                JsonDocument doc;
                try
                {
                    doc = JsonDocument.Parse(jsonResp);
                }
                catch (System.Text.Json.JsonException)
                {
                    _logger.LogWarning($"Failed to parse NAPIX response as JSON for {endpointBasePath}/{endpointAction} ({norm}). Raw response: {jsonResp}");
                    return null;
                }

                using (doc)
                {
                    if (doc.RootElement.ValueKind == JsonValueKind.Object)
                    {
                        if (doc.RootElement.TryGetProperty("response_str", out var respProp) && respProp.ValueKind == JsonValueKind.String)
                        {
                            string valStr = respProp.GetString() ?? "";
                            if (!string.IsNullOrEmpty(valStr))
                            {
                                try
                                {
                                    string decryptedJson = AesDecrypt(valStr, cfg.AuthKey, cfg.IV);
                                    _logger.LogInformation($"[NAPIX] Decrypted response_str from {endpointBasePath}/{endpointAction} ({norm}): {decryptedJson.Substring(0, Math.Min(500, decryptedJson.Length))}");
                                    return JsonDocument.Parse(decryptedJson).RootElement.Clone();
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogWarning(ex, $"[NAPIX] Failed to decrypt response_str from {endpointBasePath}/{endpointAction} ({norm})");
                                }
                            }
                        }

                        foreach (var prop in doc.RootElement.EnumerateObject())
                        {
                            if (prop.Name.Equals("httpCode", StringComparison.OrdinalIgnoreCase) ||
                                prop.Name.Equals("httpMessage", StringComparison.OrdinalIgnoreCase) ||
                                prop.Name.Equals("moreInformation", StringComparison.OrdinalIgnoreCase))
                                continue;

                            if (prop.Value.ValueKind == JsonValueKind.String)
                            {
                                string valStr = prop.Value.GetString() ?? "";
                                if (!string.IsNullOrEmpty(valStr) && valStr.Length > 20)
                                {
                                    try
                                    {
                                        string decryptedJson = AesDecrypt(valStr, cfg.AuthKey, cfg.IV);
                                        if (!string.IsNullOrWhiteSpace(decryptedJson))
                                        {
                                            string trimmed = decryptedJson.TrimStart();
                                            if (trimmed.StartsWith("{") || trimmed.StartsWith("["))
                                            {
                                                _logger.LogInformation($"[NAPIX] Decrypted property '{prop.Name}' from {endpointBasePath}/{endpointAction} ({norm}): {decryptedJson.Substring(0, Math.Min(500, decryptedJson.Length))}");
                                                return JsonDocument.Parse(decryptedJson).RootElement.Clone();
                                            }
                                            else
                                            {
                                                _logger.LogInformation($"[NAPIX] Decrypted property '{prop.Name}' as plain text from {endpointBasePath}/{endpointAction} ({norm}): {decryptedJson}");
                                                using var tempDoc = JsonDocument.Parse($"{{\"status\": \"{decryptedJson.Replace("\"", "\\\"").Replace("\r", "").Replace("\n", " ")}\"}}");
                                                return tempDoc.RootElement.Clone();
                                            }
                                        }
                                    }
                                    catch { }
                                }
                            }
                        }
                    }

                    if (!response.IsSuccessStatusCode) return null;

                    return doc.RootElement.Clone();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Exception in NAPIX API query ({norm}): {endpointBasePath}/{endpointAction}");
                return null;
            }
        }

        private static JsonElement? ExtractCaseFromCurrentStatus(JsonElement root, string cnr)
        {
            if (root.ValueKind == JsonValueKind.Object)
            {
                // If it's already an unnested case object with cino
                if (root.TryGetProperty("cino", out var cinoProp) && !string.IsNullOrWhiteSpace(cinoProp.GetString()))
                {
                    return root;
                }

                // Check nested case1, case2, ... or data properties
                foreach (var prop in root.EnumerateObject())
                {
                    if (prop.Value.ValueKind == JsonValueKind.Object)
                    {
                        if (prop.Value.TryGetProperty("cino", out var childCino))
                        {
                            string? cinoVal = childCino.GetString();
                            if (string.Equals(cinoVal, cnr, StringComparison.OrdinalIgnoreCase))
                            {
                                return prop.Value;
                            }
                        }
                    }
                }

                // If no exact cino match found, but there's a "case1" property, return that
                if (root.TryGetProperty("case1", out var case1) && case1.ValueKind == JsonValueKind.Object)
                {
                    return case1;
                }
            }
            else if (root.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in root.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.Object && item.TryGetProperty("cino", out var itemCino))
                    {
                        if (string.Equals(itemCino.GetString(), cnr, StringComparison.OrdinalIgnoreCase))
                        {
                            return item;
                        }
                    }
                }
                if (root.GetArrayLength() > 0 && root[0].ValueKind == JsonValueKind.Object)
                {
                    return root[0];
                }
            }

            return root;
        }

        public async Task<JsonElement?> GetCurrentStatusAsync(string cnr, bool isHighCourt = false, string module = "MVC")
        {
            string norm = NapixQuotaService.NormalizeModule(module);
            var cfg = GetConfigForModule(norm);
            var sw = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                var token = await GetAccessTokenAsync(norm);
                if (string.IsNullOrEmpty(token)) return null;

                string clean = System.Text.RegularExpressions.Regex.Replace(cnr.Trim(), @"[^A-Za-z0-9]", "").ToUpperInvariant();
                string endpoint = isHighCourt ? "hc-current-status-api/currentStatus" : "dc-current-status-api/currentStatus";

                // Official NAPIX spec: single pipe cino={CNR} or comma-separated list
                string plainPayload = $"cino={clean}";
                string requestStr = AesEncrypt(plainPayload, cfg.AuthKey, cfg.IV);
                string requestToken = HmacSha256(plainPayload, cfg.HmacKey);

                HttpResponseMessage? response = null;
                string jsonResp = string.Empty;

                // Strategy 1: Standard POST with FormUrlEncoded body containing ONLY the 4 documented parameters
                try
                {
                    var formData = new Dictionary<string, string>
                    {
                        ["dept_id"] = cfg.DeptId,
                        ["request_str"] = requestStr,
                        ["request_token"] = requestToken,
                        ["version"] = cfg.Version
                    };

                    string postUrl = $"{cfg.GatewayUrl}/{endpoint}";
                    using var request = new HttpRequestMessage(HttpMethod.Post, postUrl)
                    {
                        Content = new FormUrlEncodedContent(formData)
                    };
                    request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                    request.Headers.Add("Accept", "application/json, text/plain, */*");
                    request.Headers.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36");

                    response = await _httpClient.SendAsync(request);
                    jsonResp = await response.Content.ReadAsStringAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[NAPIX] Strategy 1 POST form body threw exception for {Endpoint} ({CNR})", endpoint, clean);
                }

                // Strategy 2: If Strategy 1 returned error/empty/626, try POST with parameters in query string (OpenAPI spec specifies "in: query")
                if (response == null || !response.IsSuccessStatusCode || string.IsNullOrWhiteSpace(jsonResp) || jsonResp.Contains("626") || jsonResp.Contains("INVALID_TOKEN"))
                {
                    try
                    {
                        string queryParams = $"dept_id={Uri.EscapeDataString(cfg.DeptId)}&request_str={Uri.EscapeDataString(requestStr)}&request_token={Uri.EscapeDataString(requestToken)}&version={Uri.EscapeDataString(cfg.Version)}";
                        string queryUrl = $"{cfg.GatewayUrl}/{endpoint}?{queryParams}";

                        using var queryReq = new HttpRequestMessage(HttpMethod.Post, queryUrl);
                        queryReq.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                        queryReq.Headers.Add("Accept", "application/json, text/plain, */*");
                        queryReq.Headers.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36");

                        var qResp = await _httpClient.SendAsync(queryReq);
                        var qJson = await qResp.Content.ReadAsStringAsync();
                        if (qResp.IsSuccessStatusCode && !string.IsNullOrWhiteSpace(qJson) && !qJson.Contains("626") && !qJson.Contains("INVALID_TOKEN"))
                        {
                            response = qResp;
                            jsonResp = qJson;
                        }
                    }
                    catch { }
                }

                // Strategy 3: GET with query string parameters fallback
                if (response == null || !response.IsSuccessStatusCode || string.IsNullOrWhiteSpace(jsonResp) || jsonResp.Contains("626") || jsonResp.Contains("INVALID_TOKEN"))
                {
                    try
                    {
                        string queryParams = $"dept_id={Uri.EscapeDataString(cfg.DeptId)}&request_str={Uri.EscapeDataString(requestStr)}&request_token={Uri.EscapeDataString(requestToken)}&version={Uri.EscapeDataString(cfg.Version)}";
                        string queryUrl = $"{cfg.GatewayUrl}/{endpoint}?{queryParams}";

                        using var getReq = new HttpRequestMessage(HttpMethod.Get, queryUrl);
                        getReq.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                        getReq.Headers.Add("Accept", "application/json, text/plain, */*");
                        getReq.Headers.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36");

                        var gResp = await _httpClient.SendAsync(getReq);
                        var gJson = await gResp.Content.ReadAsStringAsync();
                        if (gResp.IsSuccessStatusCode && !string.IsNullOrWhiteSpace(gJson) && !gJson.Contains("626") && !gJson.Contains("INVALID_TOKEN"))
                        {
                            response = gResp;
                            jsonResp = gJson;
                        }
                    }
                    catch { }
                }

                sw.Stop();
                if (response != null)
                {
                    _quota.TrackCall(endpoint, (int)response.StatusCode, response.IsSuccessStatusCode, null, clean, (int)sw.ElapsedMilliseconds, norm);
                }

                if (string.IsNullOrWhiteSpace(jsonResp)) return null;

                // Handle 401 Unauthorized token retry
                if (response?.StatusCode == System.Net.HttpStatusCode.Unauthorized &&
                    !jsonResp.Contains("626") &&
                    !jsonResp.Contains("INVALID_TOKEN", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogWarning("[NAPIX] Genuine 401 Unauthorized for {Endpoint} ({Module}). Refreshing token and retrying...", endpoint, norm);
                    string cacheKey = $"eCourts_OAuth_Token_{norm}";
                    _cache.Remove(cacheKey);

                    string freshToken = await FetchFreshTokenDirectAsync(norm);
                    if (!string.IsNullOrEmpty(freshToken))
                    {
                        var formData = new Dictionary<string, string>
                        {
                            ["dept_id"] = cfg.DeptId,
                            ["request_str"] = requestStr,
                            ["request_token"] = requestToken,
                            ["version"] = cfg.Version
                        };

                        using var retryReq = new HttpRequestMessage(HttpMethod.Post, $"{cfg.GatewayUrl}/{endpoint}")
                        {
                            Content = new FormUrlEncodedContent(formData)
                        };
                        retryReq.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", freshToken);
                        retryReq.Headers.Add("Accept", "application/json, text/plain, */*");
                        retryReq.Headers.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36");

                        response = await _httpClient.SendAsync(retryReq);
                        jsonResp = await response.Content.ReadAsStringAsync();
                    }
                }

                if (response == null || !response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("[NAPIX] currentStatus call failed with HTTP {Status} ({Module}): {Body}", response?.StatusCode, norm, jsonResp);
                    return null;
                }

                string trimmed = jsonResp.Trim();
                if (!trimmed.StartsWith("{") && !trimmed.StartsWith("["))
                {
                    _logger.LogWarning("[NAPIX] Non-JSON response for currentStatus ({CNR}, {Module}): {Body}", clean, norm, jsonResp);
                    return null;
                }

                using var doc = JsonDocument.Parse(jsonResp);
                if (doc.RootElement.TryGetProperty("response_str", out var respProp) && respProp.ValueKind == JsonValueKind.String)
                {
                    string cipher = respProp.GetString() ?? "";
                    if (!string.IsNullOrEmpty(cipher))
                    {
                        string decrypted = AesDecrypt(cipher, cfg.AuthKey, cfg.IV);
                        if (!string.IsNullOrWhiteSpace(decrypted))
                        {
                            _logger.LogInformation("[NAPIX] Successfully decrypted currentStatus response for {CNR} ({Module})", clean, norm);
                            using var parsedDoc = JsonDocument.Parse(decrypted);
                            var caseElem = ExtractCaseFromCurrentStatus(parsedDoc.RootElement, clean);
                            return caseElem?.Clone();
                        }
                    }
                }

                // If returned unencrypted direct JSON or root error
                if (!IsErrorJson(doc.RootElement, norm))
                {
                    var caseElem = ExtractCaseFromCurrentStatus(doc.RootElement, clean);
                    return caseElem?.Clone();
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[NAPIX] Exception querying currentStatus for {CNR} ({Module})", cnr, norm);
                return null;
            }
        }

        public async Task<JsonElement?> GetStatesAsync(bool isHighCourt = false, string module = "MVC")
        {
            if (isHighCourt)
            {
                return await QueryApiAsync("hc-state-api", "state", "", module: module);
            }
            return await QueryApiAsync("dc-state-api", "state", "", module: module);
        }

        public async Task<JsonElement?> GetDistrictsAsync(string stateCode, bool isHighCourt = false, string module = "MVC")
        {
            string paramsStr = $"state_code={stateCode}";
            if (isHighCourt)
            {
                return await QueryApiAsync("hc-district-api", "district", paramsStr, module: module);
            }
            return await QueryApiAsync("dc-district-api", "district", paramsStr, module: module);
        }

        public async Task<JsonElement?> GetCourtComplexesAsync(string stateCode, string distCode, string module = "MVC")
        {
            string paramsStr = $"state_code={stateCode}|dist_code={distCode}";
            return await QueryApiAsync("dc-court-complex-api", "courtComplex", paramsStr, module: module);
        }

        public async Task<JsonElement?> GetHighCourtBenchesAsync(string stateCode, string module = "MVC")
        {
            string paramsStr = $"state_code={stateCode}";
            return await QueryApiAsync("hc-bench-master-api", "bench", paramsStr, module: module);
        }

        public async Task<JsonElement?> GetCaseTypesAsync(string? estCode, bool isHighCourt = false, string? stateCode = null, string? distCode = null, string? type = "all", string module = "MVC")
        {
            if (isHighCourt)
            {
                var pList = new List<string>();
                if (!string.IsNullOrWhiteSpace(estCode)) pList.Add($"est_code={estCode.Trim()}");
                if (!string.IsNullOrWhiteSpace(stateCode)) pList.Add($"state_code={stateCode.Trim()}");
                return await QueryApiAsync("hc-case-type-master-api", "casetypemaster", string.Join("|", pList), module: module);
            }
            else
            {
                var pList = new List<string>();
                if (!string.IsNullOrWhiteSpace(estCode)) pList.Add($"est_code={estCode.Trim()}");
                if (!string.IsNullOrWhiteSpace(stateCode)) pList.Add($"state_code={stateCode.Trim()}");
                if (!string.IsNullOrWhiteSpace(distCode)) pList.Add($"dist_code={distCode.Trim()}");
                if (!string.IsNullOrWhiteSpace(type)) pList.Add($"type={type.Trim()}");
                string paramsStr = string.Join("|", pList);

                return await QueryApiAsync("dc-casetype-master-api", "casetypeMaster", paramsStr, module: module);
            }
        }

        public async Task<string?> DiscoverCnrAsync(string estCode, string caseType, string regNo, string regYear, bool isHighCourt = false, string module = "MVC")
        {
            string basePath = isHighCourt ? "hc-case-search-api" : "dc-case-number-api";
            string action   = isHighCourt ? "casesearch"        : "caseSearch";

            var codesToTry = new List<string>();
            if (!string.IsNullOrWhiteSpace(caseType) &&
                !caseType.Equals("Pending", StringComparison.OrdinalIgnoreCase) &&
                !caseType.Equals("Disposed", StringComparison.OrdinalIgnoreCase) &&
                !caseType.Equals("Active", StringComparison.OrdinalIgnoreCase) &&
                !caseType.Equals("Closed", StringComparison.OrdinalIgnoreCase))
            {
                codesToTry.Add(caseType.Trim());
            }

            if (isHighCourt)
            {
                var typesElement = await GetCaseTypesAsync(estCode, true, null, null, null, module: module);
                if (typesElement.HasValue && typesElement.Value.ValueKind == JsonValueKind.Array)
                {
                    foreach (var c in typesElement.Value.EnumerateArray())
                    {
                        if (c.TryGetProperty("type_name", out var tName) && c.TryGetProperty("case_type", out var tCode))
                        {
                            string name = tName.GetString() ?? "";
                            if (name.Contains("MFA", StringComparison.OrdinalIgnoreCase) ||
                                name.Contains("MV",  StringComparison.OrdinalIgnoreCase) ||
                                name.Contains("MOTOR", StringComparison.OrdinalIgnoreCase) ||
                                name.Contains("WRIT", StringComparison.OrdinalIgnoreCase) ||
                                name.Contains("WP", StringComparison.OrdinalIgnoreCase) ||
                                name.Contains("WA", StringComparison.OrdinalIgnoreCase))
                            {
                                string tc = tCode.GetString() ?? "";
                                if (!string.IsNullOrEmpty(tc) && !codesToTry.Contains(tc))
                                    codesToTry.Add(tc);
                            }
                        }
                    }
                }

                foreach (var fb in new[] { "MFA", "WP", "WA", "47", "83", "MFA-MV" })
                    if (!codesToTry.Contains(fb)) codesToTry.Add(fb);
            }
            else
            {
                foreach (var fb in new[] { "47", "83", "MVC", "ID", "LCA", "EP", "MVD", "101" })
                    if (!codesToTry.Contains(fb)) codesToTry.Add(fb);
            }

            foreach (var code in codesToTry)
            {
                if (string.IsNullOrWhiteSpace(code)) continue;

                string paramsStr = $"est_code={estCode.Trim()}|case_type={code.Trim()}|reg_no={regNo.Trim()}|reg_year={regYear.Trim()}";
                _logger.LogDebug($"[DiscoverCNR] Trying: {basePath}/{action} params={paramsStr} module={module}");
                var result = await QueryApiAsync(basePath, action, paramsStr, module: module);

                string? foundCnr = ExtractCnrFromElement(result);
                if (!string.IsNullOrEmpty(foundCnr))
                    return foundCnr;
            }

            return null;
        }

        public async Task<string?> DiscoverCnrByFirAsync(string estCode, string policeStationCode, string firNo, string firYear, string module = "MVC")
        {
            if (string.IsNullOrWhiteSpace(estCode) || string.IsNullOrWhiteSpace(firNo) || string.IsNullOrWhiteSpace(firYear))
                return null;

            try
            {
                string psCode = string.IsNullOrWhiteSpace(policeStationCode) ? "1" : policeStationCode.Trim();
                string paramsStr = $"est_code={estCode.Trim()}|police_station_code={psCode}|fir_no={firNo.Trim()}|fir_year={firYear.Trim()}";
                _logger.LogInformation("[DiscoverCnrByFir] Querying dc-fir-number-api/firNumber: {Params} ({Module})", paramsStr, module);

                var result = await QueryApiAsync("dc-fir-number-api", "firNumber", paramsStr, module: module);
                if (!result.HasValue || IsErrorJson(result.Value, module))
                {
                    result = await QueryApiAsync("dc-fir-number", "firNumber", paramsStr, module: module);
                }
                return ExtractCnrFromElement(result);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[DiscoverCnrByFir] Exception querying FIR number for {FirNo}/{FirYear} ({Module})", firNo, firYear, module);
                return null;
            }
        }

        public async Task<string?> DiscoverCnrByPartyNameAsync(string estCode, string partyName, string regYear, string pendDisp = "P", bool isHighCourt = false, string module = "MVC")
        {
            if (string.IsNullOrWhiteSpace(estCode) || string.IsNullOrWhiteSpace(partyName))
                return null;

            try
            {
                string basePath = isHighCourt ? "hc-party-name-api" : "dc-party-name-api";
                string action   = isHighCourt ? "partyname"        : "partyName";
                string y = string.IsNullOrWhiteSpace(regYear) ? DateTime.Today.Year.ToString() : regYear.Trim();
                string p = string.IsNullOrWhiteSpace(pendDisp) ? "P" : pendDisp.Trim();

                string paramsStr = isHighCourt
                    ? $"est_code={estCode.Trim()}|petres_name={Uri.EscapeDataString(partyName.Trim())}|reg_year={y}|pend_disp={p}"
                    : $"est_code={estCode.Trim()}|pend_disp={p}|litigant_name={Uri.EscapeDataString(partyName.Trim())}|reg_year={y}";

                _logger.LogInformation("[DiscoverCnrByParty] Querying {Base}/{Action}: {Params} ({Module})", basePath, action, paramsStr, module);

                var result = await QueryApiAsync(basePath, action, paramsStr, module: module);
                string? cnr = ExtractCnrFromElement(result);
                if (!string.IsNullOrEmpty(cnr)) return cnr;

                // Fallback for DC if backend accepts petres_name
                if (!isHighCourt)
                {
                    string altParams = $"est_code={estCode.Trim()}|petres_name={Uri.EscapeDataString(partyName.Trim())}|reg_year={y}|pend_disp={p}";
                    var altRes = await QueryApiAsync(basePath, action, altParams, module: module);
                    return ExtractCnrFromElement(altRes);
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[DiscoverCnrByParty] Exception querying party name for {Party} ({Module})", partyName, module);
                return null;
            }
        }


        private static string? ExtractCnrFromElement(JsonElement? element)
        {
            if (!element.HasValue) return null;

            var root = element.Value;

            // ── If root is an Array, recurse into each item
            if (root.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in root.EnumerateArray())
                {
                    var found = ExtractCnrFromElement(item);
                    if (!string.IsNullOrEmpty(found)) return found;
                }
                return null;
            }

            if (root.ValueKind == JsonValueKind.Object)
            {
                // Check direct CNR-like properties
                string[] cnrKeys = new[] { "cino", "cnr", "cnr_number", "cnrNo", "cino_no", "case_cino", "filing_no" };
                foreach (var key in cnrKeys)
                {
                    if (root.TryGetProperty(key, out var prop))
                    {
                        var val = prop.GetString();
                        // CNRs are typically 16 alphanumeric chars; tolerate minor variance
                        if (!string.IsNullOrWhiteSpace(val) && val.Length >= 12)
                            return val.Trim();
                    }
                }

                // Check nested arrays by common key names
                string[] arrayKeys = new[] { "casenos", "cases", "casetype", "data", "results", "caseList", "caseDetails" };
                foreach (var aKey in arrayKeys)
                {
                    if (root.TryGetProperty(aKey, out var arrProp))
                    {
                        if (arrProp.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var item in arrProp.EnumerateArray())
                            {
                                var found = ExtractCnrFromElement(item);
                                if (!string.IsNullOrEmpty(found)) return found;
                            }
                        }
                        else if (arrProp.ValueKind == JsonValueKind.Object)
                        {
                            var found = ExtractCnrFromElement(arrProp);
                            if (!string.IsNullOrEmpty(found)) return found;
                        }
                    }
                }
            }

            return null;
        }

        public async Task<JsonElement?> GetCnrDetailsAsync(string cnrNumber, bool isHighCourt = false, string module = "MVC")
        {
            if (string.IsNullOrWhiteSpace(cnrNumber)) return null;

            string clean = System.Text.RegularExpressions.Regex.Replace(cnrNumber.Trim(), @"[^A-Za-z0-9]", "").ToUpperInvariant();
            if (clean.Length != 16)
            {
                _logger.LogWarning("[NAPIX] Skipping lookup: CNR '{Clean}' has {Length} characters. Valid eCourts CNR must be exactly 16 characters (e.g. KADW200035292024).", clean, clean.Length);
                return null;
            }

            bool isDefinitiveHc = clean.StartsWith("HC", StringComparison.OrdinalIgnoreCase) ||
                                  (clean.Length >= 4 && clean.Substring(2, 2).Equals("HC", StringComparison.OrdinalIgnoreCase));
            if (!isHighCourt && isDefinitiveHc)
            {
                isHighCourt = true;
            }

            string norm = NapixQuotaService.NormalizeModule(module);
            _lastModuleErrors.TryRemove(norm, out _);

            string basePath = isHighCourt ? "hc-cnr-api" : "dc-cnr-api";
            string action = isHighCourt ? "CNR" : "cnr";

            _logger.LogInformation("[NAPIX] Querying {Court} CNR API: {Base}/{Action} for {CNR} ({Module})",
                isHighCourt ? "High Court" : "District Court", basePath, action, clean, module);

            // Attempt 1: Query with requested module
            var res = await QueryApiAsync(basePath, action, $"cino={clean}", module: module);
            if (res.HasValue && !IsErrorJson(res.Value, norm))
            {
                _logger.LogInformation("[NAPIX] Successfully fetched {Court} CNR details from {Base}/{Action} for {CNR}",
                    isHighCourt ? "High Court" : "District Court", basePath, action, clean);
                return res;
            }

            // If Labour credentials failed authentication, fall back to MVC credentials
            if (string.Equals(norm, "Labour", StringComparison.OrdinalIgnoreCase))
            {
                string? lastErr = GetLastModuleError(norm);
                bool isAuthOrGatewayErr = string.IsNullOrEmpty(lastErr) || lastErr.Contains("401") || lastErr.Contains("UNAUTHORIZED", StringComparison.OrdinalIgnoreCase);
                if (isAuthOrGatewayErr)
                {
                    _logger.LogInformation("[NAPIX] Labour module attempt for {CNR} returned auth/empty. Retrying with active MVC credentials...", clean);
                    var altRes = await QueryApiAsync(basePath, action, $"cino={clean}", module: "MVC");
                    if (altRes.HasValue && !IsErrorJson(altRes.Value, "MVC"))
                    {
                        _logger.LogInformation("[NAPIX] Successfully fetched {Court} CNR details for {CNR} using active MVC credentials",
                            isHighCourt ? "High Court" : "District Court", clean);
                        return altRes;
                    }
                }
            }

            // Cross-tier fallback ONLY if court tier was not definitively indicated by CNR prefix
            if (!isDefinitiveHc)
            {
                string altBasePath = isHighCourt ? "dc-cnr-api" : "hc-cnr-api";
                string altAction = isHighCourt ? "cnr" : "CNR";
                _logger.LogInformation("[NAPIX] Attempting cross-tier lookup {AltBase}/{AltAction} for {CNR}...", altBasePath, altAction, clean);
                var altTierRes = await QueryApiAsync(altBasePath, altAction, $"cino={clean}", module: module);
                if (altTierRes.HasValue && !IsErrorJson(altTierRes.Value, norm))
                {
                    _logger.LogInformation("[NAPIX] Successfully fetched CNR details from alternative tier {AltBase}/{AltAction} for {CNR}", altBasePath, altAction, clean);
                    return altTierRes;
                }
            }

            _logger.LogWarning("[NAPIX] CNR lookup for {CNR} returned error/empty.", clean);
            return null;
        }

        private static void DecodeCnrCodes(string cnr, out string stateCode, out string distCode)
        {
            stateCode = "29"; distCode = "";
            if (string.IsNullOrWhiteSpace(cnr) || cnr.Length < 4) return;

            string stPrefix = cnr.Substring(0, 2).ToUpper();
            stateCode = stPrefix switch
            {
                "KA" => "29",
                "MH" => "27",
                "DL" => "7",
                "TN" => "33",
                "KL" => "32",
                "AP" => "28",
                "TS" => "36",
                "GJ" => "24",
                "RJ" => "8",
                "UP" => "9",
                "WB" => "19",
                _ => "29"
            };

            if (stPrefix == "KA")
            {
                string prefix = cnr.Length >= 5 ? cnr.Substring(2, Math.Min(3, cnr.Length - 2)).ToUpper() : "";
                string prefix2 = cnr.Length >= 4 ? cnr.Substring(2, 2).ToUpper() : "";

                if (prefix2 == "DW" || prefix.StartsWith("DW") || prefix.StartsWith("DH"))
                {
                    distCode = "1";
                }
                else if (prefix2 == "BG" || prefix.StartsWith("BAG"))
                {
                    distCode = "4";
                }
                else if (prefix2 == "HS" || prefix.StartsWith("HAS"))
                {
                    distCode = "29";
                }
                else if (prefix2 == "BA" || prefix.StartsWith("BAL"))
                {
                    distCode = "31";
                }
                else if (prefix2 == "VN" || prefix.StartsWith("VJN"))
                {
                    distCode = "30";
                }
                else
                {
                    distCode = prefix switch
                    {
                        "UKA" or "UKN" or "UK" => "19",
                        "UDU" or "UD" => "25",
                        "DAG" or "DVG" or "DV" => "9",
                        "MYS" or "MY" => "14",
                        "BNG" or "BLN" or "BN" => "2",
                        "BLR" or "BLU" or "BL" => "3",
                        "BEL" or "BLG" or "BE" => "5",
                        "BID" or "BI" => "6",
                        "CKM" or "CHM" => "7",
                        "CHK" or "CKB" or "CB" => "8",
                        "CHI" or "CKG" or "CM" => "10",
                        "CIT" or "CTA" or "CT" => "11",
                        "DKS" or "DKN" or "DK" => "12",
                        "GAD" or "GA" => "13",
                        "KAR" or "KLG" or "KL" or "GL" => "15",
                        "KOD" or "MDG" or "KD" => "16",
                        "KOL" or "KL" => "17",
                        "KOP" or "KP" => "18",
                        "MAN" or "MN" => "20",
                        "RAI" or "RC" => "21",
                        "RAM" or "RM" => "22",
                        "SHI" or "SMG" or "SH" => "23",
                        "TUM" or "TBL" or "TM" => "24",
                        "VIJ" or "VJP" or "BJ" => "26",
                        "YAD" or "YD" => "27",
                        "HAV" or "HV" => "28",
                        _ => ""
                    };
                }
            }
        }

        public async Task<JsonElement?> GetCauselistAsync(string estCode, string courtNo, string causelistDate, string type = "civil", bool isHighCourt = false, string module = "MVC")
        {
            if (isHighCourt)
            {
                string hcParams = $"est_code={estCode}|court_no={courtNo}|causelist_date={causelistDate}";
                return await QueryApiAsync("hc-causelist-details-api", "causelistdetails", hcParams, module: module);
            }
            else
            {
                string distParams = $"est_code={estCode}|court_no={courtNo}|causelist_date={causelistDate}|type={type}";
                return await QueryApiAsync("dc-causelist-api", "causelist", distParams, module: module);
            }
        }

        public async Task<JsonElement?> GetCaseBusinessAsync(string cnrNumber, string date, bool isHighCourt = false, string module = "MVC")
        {
            if (string.IsNullOrWhiteSpace(cnrNumber)) return null;

            string clean = cnrNumber.Trim().ToUpperInvariant();
            if (!isHighCourt && (clean.StartsWith("KAHC", StringComparison.OrdinalIgnoreCase) || (clean.Length >= 4 && clean.Substring(2, 2).Equals("HC", StringComparison.OrdinalIgnoreCase))))
                isHighCourt = true;

            string effectiveModule = isHighCourt ? "HC" : (string.IsNullOrWhiteSpace(module) ? "MVC" : module);
            var dateVariants = GetDateVariants(date);

            // Try correct endpoint first with official parameters only: cino + business_date
            string endpoint = isHighCourt ? "hc-show-business-api" : "dc-show-business-api";
            foreach (var dt in dateVariants)
            {
                var res = await QueryApiAsync(endpoint, "showBusiness", $"cino={clean}|business_date={dt}", module: effectiveModule);
                if (res.HasValue && !IsErrorJson(res.Value, effectiveModule))
                {
                    _logger.LogInformation("[NAPIX] showBusiness: {CNR} on {Date} via {Endpoint} ({Module})", clean, dt, endpoint, effectiveModule);
                    return res;
                }
                // 626 = dept-level auth failure — retrying other params/endpoints won't help
                string? lastErr = GetLastModuleError(effectiveModule);
                if (lastErr != null && lastErr.Contains("626"))
                {
                    _logger.LogWarning("[NAPIX] showBusiness: eCourts CIS returned 626 INVALID_TOKEN for dept_id '{DeptId}'. Aborting retries.", GetConfigForModule(effectiveModule).DeptId);
                    return null;
                }
            }

            return null;
        }

        public async Task<JsonElement?> GetOrdersAsync(string cnrNumber, bool isHighCourt = false, List<string>? candidateDates = null, string module = "MVC")
        {
            if (string.IsNullOrWhiteSpace(cnrNumber)) return null;

            string clean = cnrNumber.Trim().ToUpperInvariant();
            if (!isHighCourt && (clean.StartsWith("KAHC", StringComparison.OrdinalIgnoreCase) || clean.Contains("HC")))
            {
                isHighCourt = true;
            }

            // Both hc-cnr-api/CNR and dc-cnr-api/cnr are all-in-one endpoints embedding all orders and judgments
            var cnrResponse = await GetCnrDetailsAsync(clean, isHighCourt, module: module);
            if (cnrResponse.HasValue && !IsErrorJson(cnrResponse.Value))
            {
                _logger.LogInformation("[GetOrdersAsync] Returning orders from CNR response for {CNR} ({Module})", clean, module);
                return cnrResponse;
            }

            return null;
        }

        public async Task<byte[]?> GetOrderPdfBytesAsync(string cnrNumber, string orderNo, string orderDate, bool isHighCourt = false, string module = "MVC")
        {
            if (string.IsNullOrWhiteSpace(cnrNumber)) return null;
            string clean = System.Text.RegularExpressions.Regex.Replace(cnrNumber.Trim(), @"[^A-Za-z0-9]", "").ToUpperInvariant();
            if (clean.Length != 16)
            {
                _logger.LogWarning("[NAPIX] Order PDF fetch skipped: CNR '{Clean}' has {Length} characters (must be 16).", clean, clean.Length);
                return null;
            }
            if (!isHighCourt && (clean.StartsWith("KAHC", StringComparison.OrdinalIgnoreCase) || (clean.Length >= 4 && clean.Substring(2, 2).Equals("HC", StringComparison.OrdinalIgnoreCase))))
            {
                isHighCourt = true;
            }

            string cleanOrderNo = string.IsNullOrWhiteSpace(orderNo) ? "1" : orderNo.Trim();
            if (int.TryParse(cleanOrderNo, out int parsedNo) && parsedNo > 0)
            {
                cleanOrderNo = parsedNo.ToString();
            }


            // 2. Prepare date variants for official NAPIX API calls
            var dateVariants = new List<string>();
            if (!string.IsNullOrWhiteSpace(orderDate))
            {
                string dtClean = orderDate.Trim();
                dateVariants.Add(dtClean);

                var m = System.Text.RegularExpressions.Regex.Match(dtClean, @"^(\d{1,2})[-/](\d{1,2})[-/](\d{4})$");
                if (m.Success)
                {
                    string day = m.Groups[1].Value.PadLeft(2, '0');
                    string month = m.Groups[2].Value.PadLeft(2, '0');
                    string year = m.Groups[3].Value;
                    string dmy = $"{day}-{month}-{year}";
                    string ymd = $"{year}-{month}-{day}";
                    if (!dateVariants.Contains(dmy)) dateVariants.Add(dmy);
                    if (!dateVariants.Contains(ymd)) dateVariants.Add(ymd);
                }
                else if (DateTime.TryParse(dtClean, out var dtParsed))
                {
                    string dmy = dtParsed.ToString("dd-MM-yyyy");
                    string ymd = dtParsed.ToString("yyyy-MM-dd");
                    if (!dateVariants.Contains(dmy)) dateVariants.Add(dmy);
                    if (!dateVariants.Contains(ymd)) dateVariants.Add(ymd);
                }
            }
            else
            {
                dateVariants.Add("");
            }

            // 3. Direct NAPIX Gateway Call
            string[] endpoints = isHighCourt
                ? new[] { "hc-order-api/order", "hc-oreder-api/order" }
                : new[] { "dc-oreder-api/order", "dc-order-api/order" };

            string effectiveModule = isHighCourt ? "HC" : module;
            var token = await GetAccessTokenAsync(effectiveModule);
            if (string.IsNullOrEmpty(token)) return null;
            var cfg = GetConfigForModule(effectiveModule);

            foreach (var ep in endpoints)
            {
                foreach (var dt in dateVariants)
                {
                    var paramCombinations = new List<string>();
                    if (isHighCourt)
                    {
                        // High Court OpenAPI spec: cino={CNR}|order_date={date} (no order_no parameter)
                        if (!string.IsNullOrWhiteSpace(dt))
                        {
                            paramCombinations.Add($"cino={clean}|order_date={dt}");
                            paramCombinations.Add($"cino={clean}|order_no={cleanOrderNo}|order_date={dt}");
                            paramCombinations.Add($"cnr_number={clean}|order_date={dt}");
                        }
                        else
                        {
                            paramCombinations.Add($"cino={clean}");
                            paramCombinations.Add($"cino={clean}|order_no={cleanOrderNo}");
                            paramCombinations.Add($"cnr_number={clean}");
                        }
                    }
                    else
                    {
                        // District Court OpenAPI spec: cino={CNR}|order_no={no}|order_date={date}
                        if (!string.IsNullOrWhiteSpace(dt))
                        {
                            paramCombinations.Add($"cino={clean}|order_no={cleanOrderNo}|order_date={dt}");
                            paramCombinations.Add($"cino={clean}|order_date={dt}|order_no={cleanOrderNo}");
                            paramCombinations.Add($"cnr_number={clean}|order_no={cleanOrderNo}|order_date={dt}");
                        }
                        else
                        {
                            paramCombinations.Add($"cino={clean}|order_no={cleanOrderNo}");
                            paramCombinations.Add($"cino={clean}");
                        }
                    }

                    foreach (var pipe in paramCombinations)
                    {
                        try
                        {
                            string encryptedBase64 = AesEncrypt(pipe, cfg.AuthKey, cfg.Iv);
                            string requestStr = Uri.EscapeDataString(encryptedBase64);
                            string requestToken = HmacSha256(pipe, cfg.HmacKey);

                            string requestUrl = $"{cfg.GatewayUrl}/{ep}?" +
                                                $"dept_id={Uri.EscapeDataString(cfg.DeptId)}&" +
                                                $"request_str={requestStr}&" +
                                                $"request_token={requestToken}&" +
                                                $"version={cfg.Version}";

                            using var req = new HttpRequestMessage(HttpMethod.Get, requestUrl);
                            req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                            req.Headers.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36");
                            req.Headers.Add("X-IBM-Client-Id", cfg.ApiKey);
                            // Note: Do NOT send X-IBM-Client-Secret per NAPIX OpenAPI securityDefinitions (causes 400/500 gateway rejection)

                            using var res = await _httpClient.SendAsync(req);
                            if (res.IsSuccessStatusCode)
                            {
                                var rawBytes = await res.Content.ReadAsByteArrayAsync();

                                if (rawBytes.Length >= 4 && rawBytes[0] == 0x25 && rawBytes[1] == 0x50 && rawBytes[2] == 0x44 && rawBytes[3] == 0x46)
                                {
                                    _logger.LogInformation("[NAPIX] Successfully fetched direct raw PDF from {Ep} for {Cnr}", ep, clean);
                                    return rawBytes;
                                }

                                string bodyText = Encoding.UTF8.GetString(rawBytes);
                                string? responseStr = null;
                                try
                                {
                                    using var doc = JsonDocument.Parse(bodyText);
                                    if (doc.RootElement.TryGetProperty("response_str", out var rsProp))
                                    {
                                        responseStr = rsProp.GetString();
                                    }
                                    else if (doc.RootElement.TryGetProperty("status", out var stProp))
                                    {
                                        string? st = stProp.GetString();
                                        if (st == "0" || st == "INVALID_CNR" || st == "RECORD NOT FOUND")
                                        {
                                            continue;
                                        }
                                        if (st == "INVALID_TOKEN")
                                        {
                                            _logger.LogInformation("[NAPIX] Order query received INVALID_TOKEN from {Ep} - trying next variant.", ep);
                                            continue;
                                        }
                                        if (st != null && st.Length > 20)
                                        {
                                            string? decErr = AesDecrypt(st, cfg.AuthKey, cfg.Iv);
                                            if (decErr != null && (decErr.Contains("626") || decErr.Contains("INVALID_TOKEN")))
                                            {
                                                _logger.LogInformation("[NAPIX] Order query received encrypted 626/INVALID_TOKEN from {Ep} - aborting loop.", ep);
                                                return null;
                                            }
                                        }
                                    }
                                    if (doc.RootElement.TryGetProperty("status_code", out var scProp))
                                    {
                                        string? sc = scProp.GetString();
                                        if (sc == "626")
                                        {
                                            _logger.LogInformation("[NAPIX] Order query received 626 from {Ep} - aborting loop.", ep);
                                            return null;
                                        }
                                    }
                                }
                                catch { }

                                if (!string.IsNullOrWhiteSpace(responseStr))
                                {
                                    var decryptedBytes = AesDecryptToBytes(responseStr, cfg.AuthKey, cfg.Iv);
                                    var pdfBytes = EnsurePdfBytes(decryptedBytes);
                                    if (pdfBytes != null)
                                    {
                                        _logger.LogInformation("[NAPIX] Successfully decrypted PDF bytes from {Ep} for {Cnr}", ep, clean);
                                        return pdfBytes;
                                    }
                                }
                                else
                                {
                                    var pdfBytes = EnsurePdfBytes(rawBytes);
                                    if (pdfBytes != null) return pdfBytes;
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogDebug(ex, "[NAPIX] Error querying order endpoint {Ep} for {Cnr}", ep, clean);
                        }
                    }
                }
            }

            return null;
        }

        private static byte[]? EnsurePdfBytes(byte[]? decryptedBytes)
        {
            if (decryptedBytes == null || decryptedBytes.Length == 0) return null;

            // 1. Direct %PDF magic bytes
            if (decryptedBytes.Length >= 4 &&
                decryptedBytes[0] == 0x25 &&
                decryptedBytes[1] == 0x50 &&
                decryptedBytes[2] == 0x44 &&
                decryptedBytes[3] == 0x46)
            {
                return decryptedBytes;
            }

            // 2. Treat as UTF-8 string (Base64, JSON string, or JSON object)
            try
            {
                string text = Encoding.UTF8.GetString(decryptedBytes).Trim();
                if (string.IsNullOrWhiteSpace(text)) return null;

                // 2a. Unquote string if wrapped in quotes
                if (text.Length >= 2 && text.StartsWith("\"") && text.EndsWith("\""))
                {
                    try
                    {
                        using var doc = JsonDocument.Parse(text);
                        if (doc.RootElement.ValueKind == JsonValueKind.String)
                            text = doc.RootElement.GetString()?.Trim() ?? text;
                        else
                            text = text.Substring(1, text.Length - 2).Trim();
                    }
                    catch
                    {
                        text = text.Substring(1, text.Length - 2).Trim();
                    }
                }

                // 2b. Check if it's a JSON object containing properties like pdf, order_pdf, data, etc.
                if (text.StartsWith("{") && text.EndsWith("}"))
                {
                    try
                    {
                        using var doc = JsonDocument.Parse(text);
                        var root = doc.RootElement;
                        foreach (var prop in new[] { "pdf", "order_pdf", "data", "file", "pdf_data", "order_data", "order_file", "pdf_content" })
                        {
                            if (root.TryGetProperty(prop, out var p) && p.GetString() is { } b64 && !string.IsNullOrWhiteSpace(b64))
                            {
                                var parsed = TryDecodeBase64Pdf(b64);
                                if (parsed != null) return parsed;
                            }
                        }
                    }
                    catch { }
                }

                // 2c. Direct Base64 string decode
                var directB64 = TryDecodeBase64Pdf(text);
                if (directB64 != null) return directB64;
            }
            catch { }

            return null;
        }

        private static byte[]? TryDecodeBase64Pdf(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return null;
            string cleaned = input.Trim('\"', '\'', ' ', '\r', '\n', '\t', '\0')
                                  .Replace("\\\"", "")
                                  .Replace("\"", "")
                                  .Replace("\\/", "/")
                                  .Replace("\\r", "")
                                  .Replace("\\n", "")
                                  .Replace("\r", "")
                                  .Replace("\n", "")
                                  .Replace(" ", "")
                                  .Replace("\t", "");

            if (cleaned.StartsWith("data:application/pdf;base64,", StringComparison.OrdinalIgnoreCase))
            {
                cleaned = cleaned.Substring("data:application/pdf;base64,".Length);
            }

            int mod4 = cleaned.Length % 4;
            if (mod4 > 0) cleaned = cleaned.PadRight(cleaned.Length + (4 - mod4), '=');

            try
            {
                byte[] bytes = Convert.FromBase64String(cleaned);
                if (bytes.Length >= 4 && bytes[0] == 0x25 && bytes[1] == 0x50 && bytes[2] == 0x44 && bytes[3] == 0x46)
                {
                    return bytes;
                }
                if (bytes.Length > 20)
                {
                    return bytes;
                }
            }
            catch { }

            return null;
        }

        /// <summary>
        /// Check if a NAPIX JSON element contains an embedded orders array under any known key.
        /// </summary>
        private static bool HasOrdersEmbedded(JsonElement root)
        {
            string[] orderKeys = {
                "orders", "order_details", "orders_details", "case_orders", "interimorder", "finalorder",
                "judgements", "judgments", "order_list", "final_order", "orderdetails", "judgment",
                "history_of_case_hearings", "case_hearings", "interim_order", "interim_orders",
                "historyofcasehearing", "case_history", "history_details", "hearing_history",
                "businessdetails", "business_details", "judgment_copy", "order_copy", "hearing_orders", "proceedings"
            };

            bool Found(JsonElement elem, int depth)
            {
                if (depth > 8) return false;
                if (elem.ValueKind == JsonValueKind.Object)
                {
                    foreach (var prop in elem.EnumerateObject())
                    {
                        if (Array.Exists(orderKeys, k => k.Equals(prop.Name, StringComparison.OrdinalIgnoreCase)))
                        {
                            if (prop.Value.ValueKind == JsonValueKind.Array && prop.Value.GetArrayLength() > 0)
                                return true;
                            if (prop.Value.ValueKind == JsonValueKind.Object)
                                return true;
                        }
                        if (Found(prop.Value, depth + 1)) return true;
                    }
                }
                else if (elem.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in elem.EnumerateArray())
                        if (Found(item, depth + 1)) return true;
                }
                return false;
            }

            return Found(root, 0);
        }

        private bool IsErrorJson(JsonElement root, string module = "MVC")
        {
            if (root.ValueKind != JsonValueKind.Object) return false;

            string norm = NapixQuotaService.NormalizeModule(module);

            if (root.TryGetProperty("status_code", out var codeProp))
            {
                string code = codeProp.GetString() ?? codeProp.GetRawText();
                if (!string.IsNullOrEmpty(code) && code != "200" && code != "1")
                {
                    string statusMsg = root.TryGetProperty("status", out var s) ? (s.GetString() ?? "") : "";
                    if (code == "626" || statusMsg.Contains("INVALID_TOKEN", StringComparison.OrdinalIgnoreCase))
                    {
                        _lastModuleErrors[norm] = "626 (INVALID_TOKEN): eCourts CIS backend rejected the request parameter or token format.";
                    }
                    else if (code == "628" || statusMsg.Contains("RECORD NOT FOUND", StringComparison.OrdinalIgnoreCase))
                    {
                        _lastModuleErrors[norm] = "628 (RECORD_NOT_FOUND): Record is not available in eCourts for this CNR.";
                    }
                    else if (code == "600" || statusMsg.Contains("INVALID_CNR", StringComparison.OrdinalIgnoreCase))
                    {
                        _lastModuleErrors[norm] = "600 (INVALID_CNR): The CNR number format was rejected by eCourts.";
                    }
                    else
                    {
                        _lastModuleErrors[norm] = $"{code}: {statusMsg}";
                    }
                    return true;
                }
            }

            if (root.TryGetProperty("status", out var statusProp) && statusProp.ValueKind == JsonValueKind.String)
            {
                string status = statusProp.GetString() ?? "";
                if (status.StartsWith("wh+", StringComparison.OrdinalIgnoreCase) || status.Contains("SXudd") || status.Contains("wh+SXudd"))
                {
                    _lastModuleErrors[norm] = "626 (INVALID_TOKEN): eCourts CIS backend rejected the request parameter or token format.";
                    return true;
                }
                if (status.Length >= 20 && !status.StartsWith("{") && !status.StartsWith("["))
                {
                    try
                    {
                        var cfg = GetConfigForModule(module);
                        string dec = AesDecrypt(status, cfg.AuthKey, cfg.Iv);
                        if (!string.IsNullOrWhiteSpace(dec)) status = dec;
                    }
                    catch { }
                }

                if (status.Contains("626") || status.Contains("INVALID_TOKEN", StringComparison.OrdinalIgnoreCase))
                {
                    _lastModuleErrors[norm] = "626 (INVALID_TOKEN): eCourts CIS backend rejected the request parameter or token format.";
                    return true;
                }

                if (status.Equals("INVALID_CNR", StringComparison.OrdinalIgnoreCase) ||
                    status.Equals("RECORD NOT FOUND", StringComparison.OrdinalIgnoreCase) ||
                    status.Equals("NO DATA", StringComparison.OrdinalIgnoreCase) ||
                    status.Contains("INVALID_CNR", StringComparison.OrdinalIgnoreCase) ||
                    status.Contains("RECORD NOT FOUND", StringComparison.OrdinalIgnoreCase) ||
                    status.StartsWith("INVALID", StringComparison.OrdinalIgnoreCase) ||
                    status.StartsWith("FAIL", StringComparison.OrdinalIgnoreCase) ||
                    status.StartsWith("ERROR", StringComparison.OrdinalIgnoreCase))
                {
                    _lastModuleErrors[norm] = status;
                    return true;
                }
            }

            if (root.TryGetProperty("httpCode", out var httpCodeProp))
            {
                string code = httpCodeProp.GetString() ?? httpCodeProp.GetRawText();
                if (!string.IsNullOrEmpty(code) && code != "200")
                {
                    string info = root.TryGetProperty("moreInformation", out var m) ? (m.GetString() ?? "") : "";
                    _lastModuleErrors[norm] = $"HTTP {code}: {info}";
                    return true;
                }
            }

            int propCount = 0;
            foreach (var prop in root.EnumerateObject())
            {
                propCount++;
                string name = prop.Name.ToLowerInvariant();
                if (name is "cino" or "cnr" or "cnr_number" or "petitioner" or "pet_name" or "respondent" or "res_name" or 
                    "history_of_case_hearings" or "case_hearings" or "historyofcasehearing" or "orders" or "interimorder" or "finalorder" or 
                    "case_details" or "details" or "case_type" or "registration_date" or "case_number" or "reg_no" or "fil_no" or 
                    "court_name" or "est_name" or "dt_regis" or "next_date" or "business_date" or "purpose_name" or "srno" or "sr_no" or
                    "status" or "response_str" or "data" or "result" or "response")
                {
                    return false;
                }
            }

            return propCount == 0;
        }

        private static List<string> GetDateVariants(string inputDate)
        {
            var list = new List<string>();
            if (string.IsNullOrWhiteSpace(inputDate))
            {
                var today = DateTime.Today;
                list.Add(today.ToString("yyyy-MM-dd"));
                list.Add(today.ToString("dd-MM-yyyy"));
                list.Add(today.ToString("dd/MM/yyyy"));
                return list;
            }

            string clean = inputDate.Trim();
            list.Add(clean);

            if (DateTime.TryParse(clean, out var dt))
            {
                string f1 = dt.ToString("yyyy-MM-dd");
                string f2 = dt.ToString("dd-MM-yyyy");
                string f3 = dt.ToString("dd/MM/yyyy");

                if (!list.Contains(f1)) list.Add(f1);
                if (!list.Contains(f2)) list.Add(f2);
                if (!list.Contains(f3)) list.Add(f3);
            }
            return list;
        }

        public async Task<JsonElement?> GetHighCourtCauselistBenchesAsync(string estCode, string causelistDate, string module = "MVC")
        {
            if (string.IsNullOrWhiteSpace(estCode)) estCode = "KAHC01";
            var dateVariants = GetDateVariants(causelistDate);

            foreach (var d in dateVariants)
            {
                string paramsStr = $"est_code={estCode}|causelist_date={d}";
                var res = await QueryApiAsync("hc-causelist-bench-api", "causelistbench", paramsStr, module: module);
                if (res.HasValue && !IsErrorJson(res.Value)) return res;
            }

            var fallback = await QueryApiAsync("hc-causelist-bench-api", "causelistbench", $"est_code={estCode}", module: module);
            if (fallback.HasValue && !IsErrorJson(fallback.Value)) return fallback;

            return await GetHighCourtBenchMasterAsync(estCode, module: module);
        }

        public async Task<JsonElement?> GetHighCourtCauselistDetailsAsync(string estCode, string benchId, string causelistDate, string module = "MVC")
        {
            if (string.IsNullOrWhiteSpace(estCode)) estCode = "KAHC01";
            var dateVariants = GetDateVariants(causelistDate);

            foreach (var d in dateVariants)
            {
                string paramsStr = !string.IsNullOrWhiteSpace(benchId) && benchId != "ALL"
                    ? $"est_code={estCode}|bench_id={benchId}|causelist_date={d}"
                    : $"est_code={estCode}|causelist_date={d}";

                var res = await QueryApiAsync("hc-causelist-details-api", "causelistdetails", paramsStr, module: module);
                if (res.HasValue && !IsErrorJson(res.Value)) return res;

                if (!string.IsNullOrWhiteSpace(benchId) && benchId != "ALL")
                {
                    res = await QueryApiAsync("hc-causelist-details-api", "causelistdetails", $"est_code={estCode}|causelist_date={d}", module: module);
                    if (res.HasValue && !IsErrorJson(res.Value)) return res;
                }

                res = await QueryApiAsync("hc-show-causelist-api", "showcauselist", paramsStr, module: module);
                if (res.HasValue && !IsErrorJson(res.Value)) return res;
            }

            return null;
        }

        public async Task<JsonElement?> GetHighCourtShowCauselistAsync(string estCode, string benchId, string causelistId, string causelistDate, string module = "MVC")
        {
            if (string.IsNullOrWhiteSpace(estCode)) estCode = "KAHC01";
            string paramsStr = $"est_code={estCode}|bench_id={benchId}|causelist_id={causelistId}|causelist_date={causelistDate}";
            return await QueryApiAsync("hc-show-causelist-api", "showcauselist", paramsStr, module: module);
        }

        public async Task<JsonElement?> GetHighCourtBenchMasterAsync(string estCode, string module = "MVC")
        {
            if (string.IsNullOrWhiteSpace(estCode)) estCode = "KAHC01";
            var res = await QueryApiAsync("hc-bench-master-api", "bench", $"est_code={estCode}", module: module);
            if (res.HasValue && !IsErrorJson(res.Value)) return res;
            return await QueryApiAsync("hc-bench-master-api", "bench", "state_code=29", module: module);
        }



        #region Cryptographic Helpers
        private static byte[] NormalizeKey(string keyStr)
        {
            // NAPIX spec (section 15.2.1): Encoding.ASCII.GetBytes(authenticationKey)
            byte[] raw = Encoding.ASCII.GetBytes(keyStr ?? "");
            if (raw.Length == 16 || raw.Length == 24 || raw.Length == 32) return raw;
            byte[] normalized = new byte[16];
            Array.Copy(raw, normalized, Math.Min(raw.Length, 16));
            return normalized;
        }

        private static byte[] NormalizeIV(string ivStr)
        {
            // NAPIX spec (section 15.2.2): Encoding.ASCII.GetBytes(ivString)
            byte[] raw = Encoding.ASCII.GetBytes(ivStr ?? "");
            if (raw.Length == 16) return raw;
            byte[] normalized = new byte[16];
            Array.Copy(raw, normalized, Math.Min(raw.Length, 16));
            return normalized;
        }

        private static string AesEncrypt(string plainText, string keyStr, string ivStr)
        {
            using var aes = Aes.Create();
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            aes.Key = NormalizeKey(keyStr);
            aes.IV = NormalizeIV(ivStr);

            using var encryptor = aes.CreateEncryptor();
            byte[] plainBytes = Encoding.UTF8.GetBytes(plainText ?? "");
            byte[] encryptedBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);
            return Convert.ToBase64String(encryptedBytes);
        }

        private static string AesDecrypt(string cipherTextBase64, string keyStr, string ivStr)
        {
            if (string.IsNullOrWhiteSpace(cipherTextBase64)) return "";
            string clean = cipherTextBase64.Trim().Replace("\\/", "/").Replace("-", "+").Replace("_", "/");
            int mod4 = clean.Length % 4;
            if (mod4 > 0) clean += new string('=', 4 - mod4);

            byte[] cipherBytes;
            try
            {
                cipherBytes = Convert.FromBase64String(clean);
            }
            catch
            {
                return "";
            }

            var keyNorm = NormalizeKey(keyStr);
            var ivNorm = NormalizeIV(ivStr);
            var zeroIv = new byte[16];
            var zeroKey = new byte[16];

            // List of (key, IV) pairs to attempt in priority order:
            // 1. Configured AuthKey with AuthKey as IV (NIC eCourts standard specification & proven live behavior)
            // 2. Configured AuthKey with 16 zero-bytes IV
            // 3. 16 zero-bytes Key with 16 zero-bytes IV (NIC eCourts 626/INVALID_TOKEN error responses)
            var attempts = new List<(byte[] Key, byte[] IV)>
            {
                (keyNorm, ivNorm),
                (keyNorm, zeroIv),
                (zeroKey, zeroIv)
            };

            string bestPlainText = "";

            foreach (var (k, iv) in attempts)
            {
                try
                {
                    using var aes = Aes.Create();
                    aes.Mode = CipherMode.CBC;
                    aes.Padding = PaddingMode.PKCS7;
                    aes.Key = k;
                    aes.IV = iv;

                    using var decryptor = aes.CreateDecryptor();
                    byte[] decryptedBytes = decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);
                    string text = Encoding.UTF8.GetString(decryptedBytes);
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        string trimmed = text.Trim();
                        // If it successfully decrypted into valid JSON or typical eCourts response, return immediately
                        if (trimmed.StartsWith("{") || trimmed.StartsWith("["))
                        {
                            return text;
                        }
                        if (string.IsNullOrEmpty(bestPlainText) && !trimmed.Contains("\0"))
                        {
                            bestPlainText = text;
                        }
                    }
                }
                catch
                {
                    // Continue to next decryption strategy
                }
            }

            return bestPlainText;
        }

        private static byte[] AesDecryptToBytes(string cipherTextBase64, string keyStr, string ivStr)
        {
            if (string.IsNullOrWhiteSpace(cipherTextBase64)) return Array.Empty<byte>();
            string clean = cipherTextBase64.Trim().Replace("\\/", "/").Replace("-", "+").Replace("_", "/");
            int mod4 = clean.Length % 4;
            if (mod4 > 0) clean += new string('=', 4 - mod4);

            byte[] cipherBytes;
            try
            {
                cipherBytes = Convert.FromBase64String(clean);
            }
            catch
            {
                return Array.Empty<byte>();
            }

            var keyNorm = NormalizeKey(keyStr);
            var ivNorm = NormalizeIV(ivStr);
            var zeroIv = new byte[16];
            var zeroKey = new byte[16];

            var attempts = new List<(byte[] Key, byte[] IV)>
            {
                (keyNorm, ivNorm),
                (keyNorm, zeroIv),
                (zeroKey, zeroIv)
            };

            foreach (var (k, iv) in attempts)
            {
                try
                {
                    using var aes = Aes.Create();
                    aes.Mode = CipherMode.CBC;
                    aes.Padding = PaddingMode.PKCS7;
                    aes.Key = k;
                    aes.IV = iv;

                    using var decryptor = aes.CreateDecryptor();
                    byte[] bytes = decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);
                    if (bytes != null && bytes.Length > 0)
                    {
                        return bytes;
                    }
                }
                catch
                {
                    // Continue to next decryption strategy
                }
            }

            return Array.Empty<byte>();
        }

        private static string HmacSha256(string message, string keyStr)
        {
            // NAPIX spec (section 15.2.3): key = ASCII bytes, data = UTF8 bytes
            using var hmac = new HMACSHA256(Encoding.ASCII.GetBytes(keyStr));
            byte[] hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(message));
            var sb = new StringBuilder(hash.Length * 2);
            foreach (byte b in hash)
            {
                sb.Append(b.ToString("x2"));
            }
            return sb.ToString();
        }

        /// <summary>
        /// Extracts a CNR/cino hint from the pipe-delimited parameter string
        /// so the quota tracker can associate calls with a specific case.
        /// </summary>
        private static string? ExtractCnrHintFromParams(string pipeParameters)
        {
            if (string.IsNullOrWhiteSpace(pipeParameters)) return null;
            foreach (var segment in pipeParameters.Split('|'))
            {
                var eq = segment.IndexOf('=');
                if (eq <= 0) continue;
                var key = segment[..eq].Trim().ToLowerInvariant();
                if (key is "cino" or "cnr_number" or "cnr")
                    return segment[(eq + 1)..].Trim();
            }
            return null;
        }
        #endregion
    }
}
