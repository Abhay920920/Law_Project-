using System;
using System.Collections.Generic;
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

        private readonly string _apiKey;
        private readonly string _secretKey;
        private readonly string _deptId;
        private readonly string _hmacKey;
        private readonly string _authKey;
        private readonly string _iv;
        private readonly string _version;
        private readonly string _gatewayUrl;
        private readonly string _tokenUrl;

        public ECourtsNapixService(
            HttpClient httpClient,
            IMemoryCache cache,
            IConfiguration config,
            ILogger<ECourtsNapixService> logger)
        {
            _httpClient = httpClient;
            _cache = cache;
            _config = config;
            _logger = logger;

            _apiKey = _config["eCourts:ApiKey"] ?? Environment.GetEnvironmentVariable("ECOURTS__APIKEY") ?? "";
            _secretKey = _config["eCourts:SecretKey"] ?? Environment.GetEnvironmentVariable("ECOURTS__SECRETKEY") ?? "";
            _deptId = _config["eCourts:DeptId"] ?? Environment.GetEnvironmentVariable("ECOURTS__DEPTID") ?? "clonwkrtc";
            _hmacKey = _config["eCourts:HmacKey"] ?? Environment.GetEnvironmentVariable("ECOURTS__HMACKEY") ?? "";
            _authKey = _config["eCourts:AuthKey"] ?? Environment.GetEnvironmentVariable("ECOURTS__AUTHKEY") ?? "";
            _iv = _config["eCourts:IV"] ?? Environment.GetEnvironmentVariable("ECOURTS__IV") ?? "";
            _version = _config["eCourts:Version"] ?? Environment.GetEnvironmentVariable("ECOURTS__VERSION") ?? "v1.0";
            _gatewayUrl = _config["eCourts:GatewayUrl"] ?? Environment.GetEnvironmentVariable("ECOURTS__GATEWAYURL") ?? "https://delhigw.napix.gov.in/nic/ecourts";
            _tokenUrl = _config["eCourts:OAuthTokenUrl"] ?? Environment.GetEnvironmentVariable("ECOURTS__OAUTHTOKENURL") ?? "https://delhigw.napix.gov.in/nic/ecourts/oauth2/token";
        }

        public async Task<string> GetAccessTokenAsync()
        {
            try
            {
                if (_cache.TryGetValue("eCourts_OAuth_Token", out string? cachedToken) && !string.IsNullOrWhiteSpace(cachedToken))
                {
                    return cachedToken;
                }

                string freshToken = await FetchFreshTokenDirectAsync();
                if (!string.IsNullOrWhiteSpace(freshToken))
                {
                    _cache.Set("eCourts_OAuth_Token", freshToken, TimeSpan.FromMinutes(50));
                    return freshToken;
                }

                return string.Empty;
            }
            catch (ObjectDisposedException)
            {
                // Fallback direct fetch if memory cache is disposed on host shutdown
                return await FetchFreshTokenDirectAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Unable to connect to eCourts NAPIX Gateway for OAuth token: {ex.Message}");
                return string.Empty;
            }
        }

        private async Task<string> FetchFreshTokenDirectAsync()
        {
            try
            {
                var authHeader = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_apiKey}:{_secretKey}"));
                using var request = new HttpRequestMessage(HttpMethod.Post, _tokenUrl);
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", authHeader);
                request.Headers.Add("X-IBM-Client-Id", _apiKey);
                if (!string.IsNullOrWhiteSpace(_secretKey))
                {
                    request.Headers.Add("X-IBM-Client-Secret", _secretKey);
                }

                request.Content = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("grant_type", "client_credentials"),
                    new KeyValuePair<string, string>("scope", "napix"),
                    new KeyValuePair<string, string>("client_id", _apiKey),
                    new KeyValuePair<string, string>("client_secret", _secretKey)
                });

                var response = await _httpClient.SendAsync(request);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning($"eCourts NAPIX OAuth token request failed with status: {response.StatusCode}");
                    return string.Empty;
                }

                var jsonStr = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(jsonStr);
                return doc.RootElement.GetProperty("access_token").GetString() ?? string.Empty;
            }
            catch (ObjectDisposedException)
            {
                // Silent return on host shutdown
                return string.Empty;
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Direct OAuth token fetch failed: {ex.Message}");
                return string.Empty;
            }
        }

        public async Task<JsonElement?> QueryApiAsync(string endpointBasePath, string endpointAction, string pipeParameters)
        {
            try
            {
                var token = await GetAccessTokenAsync();
                if (string.IsNullOrEmpty(token))
                {
                    return null;
                }

                // Step 1: Encrypt plaintext payload via AES-128-CBC
                string encryptedBase64 = AesEncrypt(pipeParameters, _authKey, _iv);
                string requestStr = Uri.EscapeDataString(encryptedBase64);

                // Step 2: Generate HMAC-SHA256 signature
                string requestToken = HmacSha256(pipeParameters, _hmacKey);

                // Step 3: Construct request URL
                string requestUrl = $"{_gatewayUrl}/{endpointBasePath}/{endpointAction}?" +
                                    $"dept_id={Uri.EscapeDataString(_deptId)}&" +
                                    $"request_str={requestStr}&" +
                                    $"request_token={requestToken}&" +
                                    $"version={_version}";

                using var request = new HttpRequestMessage(HttpMethod.Get, requestUrl);
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                request.Headers.Add("X-IBM-Client-Id", _apiKey);
                if (!string.IsNullOrWhiteSpace(_secretKey))
                {
                    request.Headers.Add("X-IBM-Client-Secret", _secretKey);
                }

                HttpResponseMessage response;
                try
                {
                    response = await _httpClient.SendAsync(request);
                }
                catch (Exception ex) when (ex is HttpRequestException || ex is System.IO.IOException)
                {
                    _logger.LogWarning("[NAPIX] Transient connection reset for {Endpoint}/{Action}. Retrying in 400ms...", endpointBasePath, endpointAction);
                    await Task.Delay(400);
                    using var retryReq = new HttpRequestMessage(HttpMethod.Get, requestUrl);
                    retryReq.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                    retryReq.Headers.Add("X-IBM-Client-Id", _apiKey);
                    if (!string.IsNullOrWhiteSpace(_secretKey)) retryReq.Headers.Add("X-IBM-Client-Secret", _secretKey);
                    response = await _httpClient.SendAsync(retryReq);
                }

                var jsonResp = await response.Content.ReadAsStringAsync();

                if (string.IsNullOrWhiteSpace(jsonResp)) return null;

                // Handle expired or invalid token by evicting cache and retrying once
                if (jsonResp.Contains("INVALID_TOKEN", StringComparison.OrdinalIgnoreCase) || response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    _logger.LogWarning($"[NAPIX] Token expired or invalid for {endpointBasePath}/{endpointAction}. Refreshing token and retrying...");
                    _cache.Remove("eCourts_OAuth_Token");

                    string freshToken = await FetchFreshTokenDirectAsync();
                    if (!string.IsNullOrEmpty(freshToken))
                    {
                        _cache.Set("eCourts_OAuth_Token", freshToken, TimeSpan.FromMinutes(50));
                        using var retryReq = new HttpRequestMessage(HttpMethod.Get, requestUrl);
                        retryReq.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", freshToken);
                        retryReq.Headers.Add("X-IBM-Client-Id", _apiKey);
                        if (!string.IsNullOrWhiteSpace(_secretKey))
                        {
                            retryReq.Headers.Add("X-IBM-Client-Secret", _secretKey);
                        }

                        response = await _httpClient.SendAsync(retryReq);
                        jsonResp = await response.Content.ReadAsStringAsync();
                    }
                }

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning($"NAPIX API call to {endpointBasePath}/{endpointAction} status: {response.StatusCode}. Body: {jsonResp}");
                    try { System.IO.File.AppendAllText("napix_log.txt", $"[HTTP {response.StatusCode}] {endpointBasePath}/{endpointAction} : {jsonResp}\n"); } catch { }
                }

                // Handle PHP print_r non-JSON error format returned by some eCourts servers (e.g. INVALID_CNR)
                if (jsonResp.TrimStart().StartsWith("Array"))
                {
                    _logger.LogWarning($"NAPIX API returned non-JSON Array response for {endpointBasePath}/{endpointAction}: {jsonResp.Replace("\r", "").Replace("\n", " ")}");
                    return null;
                }

                JsonDocument doc;
                try
                {
                    doc = JsonDocument.Parse(jsonResp);
                }
                catch (System.Text.Json.JsonException)
                {
                    _logger.LogWarning($"Failed to parse NAPIX response as JSON for {endpointBasePath}/{endpointAction}. Raw response: {jsonResp}");
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
                                    string decryptedJson = AesDecrypt(valStr, _authKey, _iv);
                                    _logger.LogInformation($"[NAPIX] Decrypted response_str from {endpointBasePath}/{endpointAction}: {decryptedJson.Substring(0, Math.Min(500, decryptedJson.Length))}");
                                    return JsonDocument.Parse(decryptedJson).RootElement.Clone();
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogWarning(ex, $"[NAPIX] Failed to decrypt response_str from {endpointBasePath}/{endpointAction}");
                                }
                            }
                        }

                        // Check encrypted payload properties (including 'status' or 'response_str') except httpCode/httpMessage
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
                                        string decryptedJson = AesDecrypt(valStr, _authKey, _iv);
                                        if (!string.IsNullOrWhiteSpace(decryptedJson))
                                        {
                                            string trimmed = decryptedJson.TrimStart();
                                            if (trimmed.StartsWith("{") || trimmed.StartsWith("["))
                                            {
                                                _logger.LogInformation($"[NAPIX] Decrypted property '{prop.Name}' from {endpointBasePath}/{endpointAction}: {decryptedJson.Substring(0, Math.Min(500, decryptedJson.Length))}");
                                                return JsonDocument.Parse(decryptedJson).RootElement.Clone();
                                            }
                                            else
                                            {
                                                _logger.LogInformation($"[NAPIX] Decrypted property '{prop.Name}' as plain text from {endpointBasePath}/{endpointAction}: {decryptedJson}");
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
                _logger.LogError(ex, $"Exception in NAPIX API query: {endpointBasePath}/{endpointAction}");
                return null;
            }
        }

        public async Task<JsonElement?> QueryCurrentStatusPostAsync(string cnr, bool isHighCourt = false)
        {
            try
            {
                var token = await GetAccessTokenAsync();
                if (string.IsNullOrEmpty(token)) return null;

                string clean = cnr.Trim().ToUpperInvariant();
                string endpoint = isHighCourt ? "hc-current-status-api/currentStatus" : "dc-current-status-api/currentStatus";
                // Build envelope with empty payload (authenticates request origin)
                string requestStr = AesEncrypt("", _authKey, _iv);
                string requestToken = HmacSha256("", _hmacKey);

                // NAPIX OpenAPI gateway requires envelope params in query string for IBM API Connect validation
                string queryParams = $"dept_id={Uri.EscapeDataString(_deptId)}&request_str={Uri.EscapeDataString(requestStr)}&request_token={Uri.EscapeDataString(requestToken)}&version={Uri.EscapeDataString(_version)}";
                string url = $"{_gatewayUrl}/{endpoint}?{queryParams}";

                var formData = new Dictionary<string, string>
                {
                    ["dept_id"] = _deptId,
                    ["request_str"] = requestStr,
                    ["request_token"] = requestToken,
                    ["version"] = _version,
                    ["cnr_list"] = clean
                };

                using var request = new HttpRequestMessage(HttpMethod.Post, url)
                {
                    Content = new FormUrlEncodedContent(formData)
                };
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                request.Headers.Add("X-IBM-Client-Id", _apiKey);
                if (!string.IsNullOrWhiteSpace(_secretKey)) request.Headers.Add("X-IBM-Client-Secret", _secretKey);
                request.Headers.Add("Accept", "application/json, text/plain, */*");
                request.Headers.Add("User-Agent", "Mozilla/5.0 (compatible; eCourts-Client/1.0)");

                var response = await _httpClient.SendAsync(request);
                var jsonResp = await response.Content.ReadAsStringAsync();

                if (string.IsNullOrWhiteSpace(jsonResp)) return null;

                // Handle expired token by refreshing and retrying once
                if (jsonResp.Contains("INVALID_TOKEN", StringComparison.OrdinalIgnoreCase) || response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    _logger.LogWarning($"[NAPIX] Token expired or invalid for {endpoint}. Refreshing token and retrying...");
                    _cache.Remove("eCourts_OAuth_Token");

                    string freshToken = await FetchFreshTokenDirectAsync();
                    if (!string.IsNullOrEmpty(freshToken))
                    {
                        using var retryReq = new HttpRequestMessage(HttpMethod.Post, url)
                        {
                            Content = new FormUrlEncodedContent(formData)
                        };
                        retryReq.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", freshToken);
                        retryReq.Headers.Add("X-IBM-Client-Id", _apiKey);
                        if (!string.IsNullOrWhiteSpace(_secretKey)) retryReq.Headers.Add("X-IBM-Client-Secret", _secretKey);
                        retryReq.Headers.Add("Accept", "application/json, text/plain, */*");
                        retryReq.Headers.Add("User-Agent", "Mozilla/5.0 (compatible; eCourts-Client/1.0)");

                        response = await _httpClient.SendAsync(retryReq);
                        jsonResp = await response.Content.ReadAsStringAsync();
                    }
                }

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning($"[NAPIX] currentStatus POST failed with HTTP {response.StatusCode}: {jsonResp}");
                    return null;
                }

                string trimmed = (jsonResp ?? "").Trim();
                if (!trimmed.StartsWith("{") && !trimmed.StartsWith("["))
                {
                    _logger.LogWarning($"[NAPIX] Non-JSON response for currentStatus ({clean}): {jsonResp}");
                    return null;
                }

                using var doc = JsonDocument.Parse(jsonResp);
                if (doc.RootElement.TryGetProperty("response_str", out var respProp) && respProp.ValueKind == JsonValueKind.String)
                {
                    string cipher = respProp.GetString() ?? "";
                    if (!string.IsNullOrEmpty(cipher))
                    {
                        string decrypted = AesDecrypt(cipher, _authKey, _iv);
                        if (!string.IsNullOrWhiteSpace(decrypted))
                        {
                            _logger.LogInformation($"[NAPIX] Successfully decrypted currentStatus response for {clean}");
                            using var parsedDoc = JsonDocument.Parse(decrypted);
                            return parsedDoc.RootElement.Clone();
                        }
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, $"[NAPIX] Exception querying currentStatus for {cnr}");
                return null;
            }
        }

        public async Task<JsonElement?> GetStatesAsync(bool isHighCourt = false)
        {
            if (isHighCourt)
            {
                return await QueryApiAsync("hc-state-api", "state", "");
            }
            // Primary: official dc-state-api/state (fallback to legacy if needed)
            var res = await QueryApiAsync("dc-state-api", "state", "");
            if (res.HasValue && !IsErrorJson(res.Value)) return res;
            return await QueryApiAsync("ecourt-icjs-state-master-api", "StateMaster", "");
        }

        public async Task<JsonElement?> GetDistrictsAsync(string stateCode, bool isHighCourt = false)
        {
            string paramsStr = $"state_code={stateCode}";
            if (isHighCourt)
            {
                return await QueryApiAsync("hc-district-api", "district", paramsStr);
            }
            // Primary: official dc-district-api/district (fallback to legacy if needed)
            var res = await QueryApiAsync("dc-district-api", "district", paramsStr);
            if (res.HasValue && !IsErrorJson(res.Value)) return res;
            return await QueryApiAsync("ecourt-icjs-district-master-api", "DistrictMaster", paramsStr);
        }

        public async Task<JsonElement?> GetCourtComplexesAsync(string stateCode, string distCode)
        {
            string paramsStr = $"state_code={stateCode}|dist_code={distCode}";
            // Primary: official dc-court-complex-api/courtComplex
            var res = await QueryApiAsync("dc-court-complex-api", "courtComplex", paramsStr);
            if (res.HasValue && !IsErrorJson(res.Value)) return res;
            return await QueryApiAsync("ecourt-icjs-court-complex-api", "CourtComplex", paramsStr);
        }

        public async Task<JsonElement?> GetHighCourtBenchesAsync(string stateCode)
        {
            string paramsStr = $"state_code={stateCode}";
            return await QueryApiAsync("hc-bench-master-api", "bench", paramsStr);
        }

        public async Task<JsonElement?> GetCaseTypesAsync(string? estCode, bool isHighCourt = false, string? stateCode = null, string? distCode = null, string? type = "all")
        {
            if (isHighCourt)
            {
                var pList = new List<string>();
                if (!string.IsNullOrWhiteSpace(estCode)) pList.Add($"est_code={estCode.Trim()}");
                if (!string.IsNullOrWhiteSpace(stateCode)) pList.Add($"state_code={stateCode.Trim()}");
                return await QueryApiAsync("hc-case-type-master-api", "casetypemaster", string.Join("|", pList));
            }
            else
            {
                var pList = new List<string>();
                if (!string.IsNullOrWhiteSpace(estCode)) pList.Add($"est_code={estCode.Trim()}");
                if (!string.IsNullOrWhiteSpace(stateCode)) pList.Add($"state_code={stateCode.Trim()}");
                if (!string.IsNullOrWhiteSpace(distCode)) pList.Add($"dist_code={distCode.Trim()}");
                if (!string.IsNullOrWhiteSpace(type)) pList.Add($"type={type.Trim()}");
                string paramsStr = string.Join("|", pList);

                // Primary: official dc-casetype-master-api/caseTypeMaster
                var res = await QueryApiAsync("dc-casetype-master-api", "caseTypeMaster", paramsStr);
                if (res.HasValue && !IsErrorJson(res.Value)) return res;
                return await QueryApiAsync("ecourt-icjs-casetype-master-api", "caseTypeMaster", paramsStr);
            }
        }

        public async Task<string?> DiscoverCnrAsync(string estCode, string caseType, string regNo, string regYear, bool isHighCourt = false)
        {
            // ── Strategy 1: Direct NAPIX with official endpoint specifications
            // High Court: hc-case-search-api/casesearch
            // District Court: dc-case-number-api/caseSearch (official) & ecourt-icjs-case-number-api fallback
            string basePath = isHighCourt ? "hc-case-search-api" : "dc-case-number-api";
            string action   = isHighCourt ? "casesearch"        : "caseSearch";

            var codesToTry = new List<string>();
            codesToTry.Add(caseType);

            if (isHighCourt)
            {
                var typesElement = await GetCaseTypesAsync(estCode, true, null, null, null);
                if (typesElement.HasValue && typesElement.Value.ValueKind == JsonValueKind.Array)
                {
                    foreach (var c in typesElement.Value.EnumerateArray())
                    {
                        if (c.TryGetProperty("type_name", out var tName) && c.TryGetProperty("case_type", out var tCode))
                        {
                            string name = tName.GetString() ?? "";
                            if (name.Contains("MFA", StringComparison.OrdinalIgnoreCase) ||
                                name.Contains("MV",  StringComparison.OrdinalIgnoreCase) ||
                                name.Contains("MOTOR", StringComparison.OrdinalIgnoreCase))
                            {
                                string tc = tCode.GetString() ?? "";
                                if (!string.IsNullOrEmpty(tc) && !codesToTry.Contains(tc))
                                    codesToTry.Add(tc);
                            }
                        }
                    }
                }
            }

            foreach (var fb in new[] { "47", "83", "MVC", "MFA", "ID", "LCA", "EP", "MVD", "MFA-MV" })
                if (!codesToTry.Contains(fb)) codesToTry.Add(fb);

            foreach (var code in codesToTry)
            {
                if (string.IsNullOrWhiteSpace(code)) continue;

                string paramsStr = $"est_code={estCode.Trim()}|case_type={code.Trim()}|reg_no={regNo.Trim()}|reg_year={regYear.Trim()}";
                _logger.LogDebug($"[DiscoverCNR] Trying: {basePath}/{action} params={paramsStr}");
                var result = await QueryApiAsync(basePath, action, paramsStr);

                string? foundCnr = ExtractCnrFromElement(result);
                if (!string.IsNullOrEmpty(foundCnr))
                    return foundCnr;

                // Fallback attempt with legacy action name if dc-case-number-api/caseSearch was empty
                if (!isHighCourt)
                {
                    var fallbackRes = await QueryApiAsync("ecourt-icjs-case-number-api", "caseNumber", paramsStr);
                    foundCnr = ExtractCnrFromElement(fallbackRes);
                    if (!string.IsNullOrEmpty(foundCnr)) return foundCnr;
                }
            }

            // ── Strategy 2: Call the local PHP proxy (api.php) if direct NAPIX returned nothing
            string? cnrFromPhp = await DiscoverCnrViaPhpProxyAsync(estCode, caseType, regNo, regYear, isHighCourt);
            if (!string.IsNullOrEmpty(cnrFromPhp))
            {
                _logger.LogInformation($"CNR discovered via PHP proxy: {cnrFromPhp}");
                return cnrFromPhp;
            }

            return null;
        }

        public async Task<string?> DiscoverCnrByFirAsync(string estCode, string policeStationCode, string firNo, string firYear)
        {
            if (string.IsNullOrWhiteSpace(estCode) || string.IsNullOrWhiteSpace(firNo) || string.IsNullOrWhiteSpace(firYear))
                return null;

            try
            {
                string psCode = string.IsNullOrWhiteSpace(policeStationCode) ? "1" : policeStationCode.Trim();
                string paramsStr = $"est_code={estCode.Trim()}|police_station_code={psCode}|fir_no={firNo.Trim()}|fir_year={firYear.Trim()}";
                _logger.LogInformation("[DiscoverCnrByFir] Querying dc-fir-number/firNumber: {Params}", paramsStr);

                var result = await QueryApiAsync("dc-fir-number", "firNumber", paramsStr);
                return ExtractCnrFromElement(result);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[DiscoverCnrByFir] Exception querying FIR number for {FirNo}/{FirYear}", firNo, firYear);
                return null;
            }
        }

        public async Task<string?> DiscoverCnrByPartyNameAsync(string estCode, string partyName, string regYear, string pendDisp = "P", bool isHighCourt = false)
        {
            if (string.IsNullOrWhiteSpace(estCode) || string.IsNullOrWhiteSpace(partyName))
                return null;

            try
            {
                string basePath = isHighCourt ? "hc-party-name-api" : "dc-party-name-api";
                string action   = isHighCourt ? "partyname"        : "partyName";
                string y = string.IsNullOrWhiteSpace(regYear) ? DateTime.Today.Year.ToString() : regYear.Trim();
                string p = string.IsNullOrWhiteSpace(pendDisp) ? "P" : pendDisp.Trim();

                string paramsStr = $"est_code={estCode.Trim()}|petres_name={Uri.EscapeDataString(partyName.Trim())}|reg_year={y}|pend_disp={p}";
                _logger.LogInformation("[DiscoverCnrByParty] Querying {Base}/{Action}: {Params}", basePath, action, paramsStr);

                var result = await QueryApiAsync(basePath, action, paramsStr);
                return ExtractCnrFromElement(result);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[DiscoverCnrByParty] Exception querying party name for {Party}", partyName);
                return null;
            }
        }

        /// <summary>
        /// Calls the local XAMPP PHP proxy (api.php) which already handles NAPIX
        /// authentication, AES-128-CBC encryption and case-type discovery correctly.
        /// Returns the CNR string if found, or null.
        /// </summary>
        private async Task<string?> DiscoverCnrViaPhpProxyAsync(string estCode, string caseType, string regNo, string regYear, bool isHighCourt = false)
        {
            try
            {
                // Build URL pointing to the local PHP proxy
                string phpUrl = $"http://localhost/ecourts/api.php" +
                    $"?search_mode=case_number" +
                    $"&est_code={Uri.EscapeDataString(estCode.Trim())}" +
                    $"&case_type={Uri.EscapeDataString(caseType.Trim())}" +
                    $"&reg_no={Uri.EscapeDataString(regNo.Trim())}" +
                    $"&reg_year={Uri.EscapeDataString(regYear.Trim())}" +
                    $"&court_type={(isHighCourt ? "high_court" : "district_court")}";

                _logger.LogInformation($"[PhpProxy] Calling: {phpUrl}");

                using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(20));
                var phpResp = await _httpClient.GetStringAsync(phpUrl, cts.Token);

                _logger.LogInformation($"[PhpProxy] Raw response: {phpResp}");

                using var doc = JsonDocument.Parse(phpResp);
                var root = doc.RootElement;

                // PHP proxy returns { success: true, data: [...] } or { success: true, cino: "..." }
                if (root.TryGetProperty("success", out var succ) && succ.GetBoolean())
                {
                    // Try direct cino/cnr fields
                    string? cnr = ExtractCnrFromElement(root);
                    if (!string.IsNullOrEmpty(cnr)) return cnr;

                    // Try inside data array
                    if (root.TryGetProperty("data", out var dataEl))
                    {
                        if (dataEl.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var item in dataEl.EnumerateArray())
                            {
                                cnr = ExtractCnrFromElement(item);
                                if (!string.IsNullOrEmpty(cnr)) return cnr;
                            }
                        }
                        else
                        {
                            cnr = ExtractCnrFromElement(dataEl);
                            if (!string.IsNullOrEmpty(cnr)) return cnr;
                        }
                    }
                }
                else if (root.TryGetProperty("error", out var errProp))
                {
                    _logger.LogWarning($"[PhpProxy] Error: {errProp.GetString()}");
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[PhpProxy] PHP proxy call failed — will fall back to direct NAPIX.");
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

        public async Task<JsonElement?> GetCnrDetailsAsync(string cnrNumber, bool isHighCourt = false)
        {
            if (string.IsNullOrWhiteSpace(cnrNumber)) return null;

            // Sanitize: strip non-alphanumeric characters (e.g. leading '=', quotes, spaces)
            string clean = System.Text.RegularExpressions.Regex.Replace(cnrNumber.Trim(), @"[^A-Za-z0-9]", "").ToUpperInvariant();
            if (clean.Length != 16)
            {
                _logger.LogWarning("[NAPIX] Skipping lookup: CNR '{Clean}' has {Length} characters. Valid eCourts CNR must be exactly 16 characters (e.g. KADW200035292024).", clean, clean.Length);
                return null;
            }

            if (!isHighCourt && (clean.StartsWith("KAHC", StringComparison.OrdinalIgnoreCase) || (clean.Length >= 4 && clean.Substring(2, 2).Equals("HC", StringComparison.OrdinalIgnoreCase))))
            {
                isHighCourt = true;
            }

            // ── 1. Try configured NapixEcourtsService Microservice first (port 5050)
            var microserviceUrl = _config["NapixEcourtsService:BaseUrl"] ?? "http://localhost:5050";
            bool isMicroserviceOffline = _cache.TryGetValue("NAPIX_MICROSERVICE_OFFLINE", out bool off) && off;

            if (!isMicroserviceOffline && !string.IsNullOrWhiteSpace(microserviceUrl) && !microserviceUrl.Contains(":5000"))
            {
                try
                {
                    using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(5));
                    var microResponse = await _httpClient.GetAsync($"{microserviceUrl.TrimEnd('/')}/api/cases/{clean}/raw", cts.Token);
                    if (microResponse.IsSuccessStatusCode)
                    {
                        var json = await microResponse.Content.ReadAsStringAsync(cts.Token);
                        using var doc = JsonDocument.Parse(json);
                        _logger.LogInformation("[NAPIX] Successfully fetched live CNR {CNR} via NapixEcourtsService ({Url})", clean, microserviceUrl);
                        return doc.RootElement.Clone();
                    }
                    else
                    {
                        var errBody = await microResponse.Content.ReadAsStringAsync(cts.Token);
                        if (errBody.Contains("INVALID_TOKEN", StringComparison.OrdinalIgnoreCase) || microResponse.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                        {
                            _logger.LogWarning("[NAPIX] Microservice reported INVALID_TOKEN for {CNR}. Regenerating token and retrying...", clean);
                            try
                            {
                                var refreshRes = await _httpClient.PostAsync($"{microserviceUrl.TrimEnd('/')}/api/auth/token/refresh", null, cts.Token);
                                if (refreshRes.IsSuccessStatusCode)
                                {
                                    var retryResp = await _httpClient.GetAsync($"{microserviceUrl.TrimEnd('/')}/api/cases/{clean}/raw", cts.Token);
                                    if (retryResp.IsSuccessStatusCode)
                                    {
                                        var jsonRetry = await retryResp.Content.ReadAsStringAsync(cts.Token);
                                        using var doc = JsonDocument.Parse(jsonRetry);
                                        _logger.LogInformation("[NAPIX] Successfully fetched live CNR {CNR} via NapixEcourtsService after token refresh ({Url})", clean, microserviceUrl);
                                        return doc.RootElement.Clone();
                                    }
                                }
                            }
                            catch (Exception rex)
                            {
                                _logger.LogWarning(rex, "[NAPIX] Token refresh retry failed on microservice");
                            }
                        }
                    }
                }
                catch (Exception mex)
                {
                    _cache.Set("NAPIX_MICROSERVICE_OFFLINE", true, TimeSpan.FromMinutes(5));
                    _logger.LogWarning(mex, "[NAPIX] Microservice call failed — tripped circuit breaker for 5 min, proceeding to direct gateway");
                }
            }

            // ── 2. Direct NAPIX Gateway Call with Strict Case-Sensitivity (Primary live endpoint)
            // High Court: hc-cnr-api/CNR (must be uppercase /CNR)
            // District Court: dc-cnr-api/cnr (must be lowercase /cnr)
            string basePath = isHighCourt ? "hc-cnr-api" : "dc-cnr-api";
            string action = isHighCourt ? "CNR" : "cnr";

            // Primary query: cino={CNR}
            var res = await QueryApiAsync(basePath, action, $"cino={clean}");
            if (res.HasValue && !IsErrorJson(res.Value)) return res;

            // Fallback to opposite court type once (e.g. if CNR belongs to DC instead of HC or vice-versa)
            string oppBasePath = isHighCourt ? "dc-cnr-api" : "hc-cnr-api";
            string oppAction = isHighCourt ? "cnr" : "CNR";
            var oppRes = await QueryApiAsync(oppBasePath, oppAction, $"cino={clean}");
            if (oppRes.HasValue && !IsErrorJson(oppRes.Value)) return oppRes;

            // ── 3. Alternate currentStatus POST Call fallback if available
            var currentStatusRes = await QueryCurrentStatusPostAsync(clean, isHighCourt);
            if (currentStatusRes.HasValue && !IsErrorJson(currentStatusRes.Value))
            {
                _logger.LogInformation("[NAPIX] Successfully fetched live CNR {CNR} via direct currentStatus POST", clean);
                return currentStatusRes;
            }

            var oppCurrentStatusRes = await QueryCurrentStatusPostAsync(clean, !isHighCourt);
            if (oppCurrentStatusRes.HasValue && !IsErrorJson(oppCurrentStatusRes.Value))
            {
                _logger.LogInformation("[NAPIX] Successfully fetched live CNR {CNR} via alternate currentStatus POST", clean);
                return oppCurrentStatusRes;
            }

            return null;
        }

        /// <summary>
        /// Decodes state_code and dist_code from an eCourts CNR number across Indian States and Karnataka.
        /// Format: [STATE_2_LETTERS][DIST_PREFIX][EST_SUFFIX][6-CASE][4-YEAR]
        /// e.g. KAUKA20006722017 -> state_code=29/6, dist_code=19 (Uttara Kannada)
        /// e.g. KADW200035312025 -> state_code=29/6, dist_code=1 (Dharwad)
        /// </summary>
        private static void DecodeCnrCodes(string cnr, out string stateCode, out string distCode)
        {
            stateCode = "29"; distCode = "";
            if (string.IsNullOrWhiteSpace(cnr) || cnr.Length < 4) return;

            string stPrefix = cnr.Substring(0, 2).ToUpper();
            stateCode = stPrefix switch
            {
                "KA" => "29", // Karnataka (NAPIX=29, CIS=6)
                "MH" => "27", // Maharashtra
                "DL" => "7",  // Delhi
                "TN" => "33", // Tamil Nadu
                "KL" => "32", // Kerala
                "AP" => "28", // Andhra Pradesh
                "TS" => "36", // Telangana
                "GJ" => "24", // Gujarat
                "RJ" => "8",  // Rajasthan
                "UP" => "9",  // Uttar Pradesh
                "WB" => "19", // West Bengal
                _ => "29"
            };

            if (stPrefix == "KA")
            {
                string prefix = cnr.Length >= 5 ? cnr.Substring(2, Math.Min(3, cnr.Length - 2)).ToUpper() : "";
                string prefix2 = cnr.Length >= 4 ? cnr.Substring(2, 2).ToUpper() : "";

                if (prefix2 == "DW" || prefix.StartsWith("DW") || prefix.StartsWith("DH"))
                {
                    distCode = "1"; // Dharwad (KADW)
                }
                else if (prefix2 == "BG" || prefix.StartsWith("BAG"))
                {
                    distCode = "4"; // Bagalkot
                }
                else if (prefix2 == "HS" || prefix.StartsWith("HAS"))
                {
                    distCode = "29"; // Hassan
                }
                else if (prefix2 == "BA" || prefix.StartsWith("BAL"))
                {
                    distCode = "31"; // Ballari / Bellary
                }
                else if (prefix2 == "VN" || prefix.StartsWith("VJN"))
                {
                    distCode = "30"; // Vijayanagara
                }
                else
                {
                    distCode = prefix switch
                    {
                        "UKA" or "UKN" or "UK" => "19", // Uttara Kannada
                        "UDU" or "UD" => "25", // Udupi
                        "DAG" or "DVG" or "DV" => "9",  // Davanagere
                        "MYS" or "MY" => "14", // Mysuru
                        "BNG" or "BLN" or "BN" => "2", // Bengaluru Rural
                        "BLR" or "BLU" or "BL" => "3", // Bengaluru Urban
                        "BEL" or "BLG" or "BE" => "5", // Belagavi
                        "BID" or "BI" => "6",  // Bidar
                        "CKM" or "CHM" => "7",  // Chamarajanagara
                        "CHK" or "CKB" or "CB" => "8",  // Chikkaballapura
                        "CHI" or "CKG" or "CM" => "10", // Chikkamagaluru
                        "CIT" or "CTA" or "CT" => "11", // Chitradurga
                        "DKS" or "DKN" or "DK" => "12", // Dakshina Kannada
                        "GAD" or "GA" => "13", // Gadag
                        "KAR" or "KLG" or "KL" or "GL" => "15", // Kalaburagi
                        "KOD" or "MDG" or "KD" => "16", // Kodagu
                        "KOL" or "KL" => "17", // Kolar
                        "KOP" or "KP" => "18", // Koppal
                        "MAN" or "MN" => "20", // Mandya
                        "RAI" or "RC" => "21", // Raichur
                        "RAM" or "RM" => "22", // Ramanagara
                        "SHI" or "SMG" or "SH" => "23", // Shivamogga
                        "TUM" or "TBL" or "TM" => "24", // Tumakuru
                        "VIJ" or "VJP" or "BJ" => "26", // Vijayapura
                        "YAD" or "YD" => "27", // Yadgir
                        "HAV" or "HV" => "28", // Haveri
                        _ => ""
                    };
                }
            }
        }

        public async Task<JsonElement?> GetCauselistAsync(string estCode, string courtNo, string causelistDate, string type = "civil", bool isHighCourt = false)
        {
            if (isHighCourt)
            {
                string hcParams = $"est_code={estCode}|court_no={courtNo}|causelist_date={causelistDate}";
                return await QueryApiAsync("hc-causelist-details-api", "CauselistDetails", hcParams);
            }
            else
            {
                string distParams = $"est_code={estCode}|court_no={courtNo}|causelist_date={causelistDate}|type={type}";
                // Primary: official dc-causelist-api/causeList
                var res = await QueryApiAsync("dc-causelist-api", "causeList", distParams);
                if (res.HasValue && !IsErrorJson(res.Value)) return res;
                return await QueryApiAsync("ecourt-icjs-cause-list-api", "CauseList", distParams);
            }
        }

        public async Task<JsonElement?> GetCaseBusinessAsync(string cnrNumber, string date, bool isHighCourt = false)
        {
            if (string.IsNullOrWhiteSpace(cnrNumber)) return null;

            string clean = cnrNumber.Trim().ToUpperInvariant();
            if (!isHighCourt && (clean.StartsWith("KAHC", StringComparison.OrdinalIgnoreCase) || clean.Contains("HC")))
            {
                isHighCourt = true;
            }

            var dateVariants = GetDateVariants(date);
            string endpoint = isHighCourt ? "hc-show-business-api" : "dc-show-business-api";

            foreach (var dt in dateVariants)
            {
                var combinations = new[]
                {
                    $"cino={clean}|date={dt}",
                    $"cnr_number={clean}|date={dt}",
                    $"cino={clean}|business_date={dt}",
                    $"cnr_number={clean}|business_date={dt}"
                };

                foreach (var pipe in combinations)
                {
                    var res = await QueryApiAsync(endpoint, "showBusiness", pipe);
                    if (res.HasValue && !IsErrorJson(res.Value))
                    {
                        _logger.LogInformation("[NAPIX] Successfully fetched live case business for {CNR} on date {Date} via {Endpoint}", clean, dt, endpoint);
                        return res;
                    }
                }
            }

            string altEndpoint = isHighCourt ? "dc-show-business-api" : "hc-show-business-api";
            foreach (var dt in dateVariants)
            {
                var res = await QueryApiAsync(altEndpoint, "showBusiness", $"cino={clean}|date={dt}");
                if (res.HasValue && !IsErrorJson(res.Value)) return res;
            }

            return null;
        }

        public async Task<JsonElement?> GetOrdersAsync(string cnrNumber, bool isHighCourt = false, List<string>? candidateDates = null)
        {
            if (string.IsNullOrWhiteSpace(cnrNumber)) return null;

            string clean = cnrNumber.Trim().ToUpperInvariant();
            if (!isHighCourt && (clean.StartsWith("KAHC", StringComparison.OrdinalIgnoreCase) || clean.Contains("HC")))
            {
                isHighCourt = true;
            }

            // Decode state_code and dist_code from CNR prefix
            DecodeCnrCodes(clean, out string stateCode, out string distCode);

            // Strategy 1: Fetch from CNR API first — the CNR response embeds case details and hearing dates
            var cnrResponse = await GetCnrDetailsAsync(clean, isHighCourt);
            if (cnrResponse.HasValue && !IsErrorJson(cnrResponse.Value))
            {
                bool hasOrders = HasOrdersEmbedded(cnrResponse.Value);
                if (hasOrders)
                {
                    _logger.LogInformation($"[GetOrdersAsync] Found embedded orders in CNR response for {clean}");
                    return cnrResponse;
                }
            }

            // Collect candidate hearing/order dates from CNR history if available
            var allDates = new List<string>();
            if (cnrResponse.HasValue)
            {
                void CollectDates(JsonElement elem, int depth)
                {
                    if (depth > 6) return;
                    if (elem.ValueKind == JsonValueKind.Object)
                    {
                        foreach (var prop in elem.EnumerateObject())
                        {
                            string pName = prop.Name.ToLowerInvariant();
                            if (pName.Contains("date"))
                            {
                                if (prop.Value.ValueKind == JsonValueKind.String)
                                {
                                    string dStr = prop.Value.GetString()?.Trim() ?? "";
                                    if (!string.IsNullOrWhiteSpace(dStr) && dStr.Length >= 8 &&
                                        !dStr.Equals("Pending", StringComparison.OrdinalIgnoreCase) &&
                                        !dStr.Equals("—", StringComparison.OrdinalIgnoreCase) &&
                                        !allDates.Contains(dStr))
                                    {
                                        allDates.Add(dStr);
                                    }
                                }
                            }
                            CollectDates(prop.Value, depth + 1);
                        }
                    }
                    else if (elem.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var item in elem.EnumerateArray()) CollectDates(item, depth + 1);
                    }
                }
                CollectDates(cnrResponse.Value, 0);
            }

            // Merge externally-supplied candidate dates (from local DB history) with NAPIX-extracted dates
            if (candidateDates != null)
            {
                foreach (var dt in candidateDates)
                {
                    if (!string.IsNullOrWhiteSpace(dt) && !allDates.Contains(dt))
                        allDates.Add(dt);
                }
            }

            // Expand candidate dates into all standard date formats (YYYY-MM-DD, DD-MM-YYYY, DD/MM/YYYY)
            var expandedDates = new List<string>();
            foreach (var dt in allDates)
            {
                foreach (var variant in GetDateVariants(dt))
                {
                    if (!expandedDates.Contains(variant)) expandedDates.Add(variant);
                }
            }

            _logger.LogInformation($"[GetOrdersAsync] {clean}: {expandedDates.Count} candidate date variants: [{string.Join(", ", expandedDates)}]");

            // Strategy 2: Query dedicated High Court & District Court order/judgment endpoints matching OpenAPI specs
            if (isHighCourt)
            {
                // 1. Try hc-order-api/order with cino and order_date
                foreach (var dt in expandedDates)
                {
                    var res = await QueryApiAsync("hc-order-api", "order", $"cino={clean}|order_date={dt}");
                    if (res.HasValue && !IsErrorJson(res.Value)) return res;

                    var res2 = await QueryApiAsync("hc-order-api", "order", $"cnr_number={clean}|order_date={dt}");
                    if (res2.HasValue && !IsErrorJson(res2.Value)) return res2;
                }

                // 2. Try hc-show-business-api/showBusiness with cino and date
                foreach (var dt in expandedDates)
                {
                    var res = await QueryApiAsync("hc-show-business-api", "showBusiness", $"cino={clean}|date={dt}");
                    if (res.HasValue && !IsErrorJson(res.Value)) return res;
                }

                // 3. Try hc-order-api and hc-view-citation-order without date
                string[] paramNames = new[] { "cino", "cnr_number", "cnr" };
                (string path, string action)[] hcEndpoints = new[] {
                    ("hc-order-api", "order"),
                    ("hc-view-citation-order", "viewCitationOrder")
                };

                foreach (var (path, action) in hcEndpoints)
                {
                    foreach (var paramName in paramNames)
                    {
                        var res = await QueryApiAsync(path, action, $"{paramName}={clean}");
                        if (res.HasValue && !IsErrorJson(res.Value)) return res;
                    }
                }
            }
            else
            {
                // Query district court orders using official dc-oreder-api/order (with official NAPIX typo)
                foreach (var dt in expandedDates)
                {
                    var res = await QueryApiAsync("dc-oreder-api", "order", $"cino={clean}|order_date={dt}");
                    if (res.HasValue && !IsErrorJson(res.Value)) return res;

                    var res2 = await QueryApiAsync("dc-order-api", "order", $"cino={clean}|order_date={dt}");
                    if (res2.HasValue && !IsErrorJson(res2.Value)) return res2;
                }

                // Try dc-show-business-api
                foreach (var dt in expandedDates)
                {
                    var res = await QueryApiAsync("dc-show-business-api", "showBusiness", $"cino={clean}|date={dt}");
                    if (res.HasValue && !IsErrorJson(res.Value)) return res;
                }
            }

            // Strategy 3: Return the CNR response even if explicit orders array wasn't found separately
            if (cnrResponse.HasValue) return cnrResponse;

            return null;
        }

        public async Task<byte[]?> GetOrderPdfBytesAsync(string cnrNumber, string orderNo, string orderDate, bool isHighCourt = false)
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

            // 1. Try NapixEcourtsService microservice first if reachable (port 5050)
            var microserviceUrl = _config["NapixEcourtsService:BaseUrl"] ?? "http://localhost:5050";
            bool isMicroserviceOffline = _cache.TryGetValue("NAPIX_MICROSERVICE_OFFLINE", out bool off) && off;

            if (!isMicroserviceOffline && !string.IsNullOrWhiteSpace(microserviceUrl) && !microserviceUrl.Contains(":5000"))
            {
                try
                {
                    using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(5));
                    string url = $"{microserviceUrl.TrimEnd('/')}/api/cases/{clean}/order?orderNo={Uri.EscapeDataString(cleanOrderNo)}&date={Uri.EscapeDataString(orderDate ?? "")}";
                    var resp = await _httpClient.GetAsync(url, cts.Token);
                    if (resp.IsSuccessStatusCode)
                    {
                        var pdfBytes = await resp.Content.ReadAsByteArrayAsync(cts.Token);
                        var validPdf = EnsurePdfBytes(pdfBytes);
                        if (validPdf != null) return validPdf;
                    }
                }
                catch (Exception)
                {
                    _cache.Set("NAPIX_MICROSERVICE_OFFLINE", true, TimeSpan.FromMinutes(5));
                }
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
                ? new[] { "hc-order-api/order", "hc-oreder-api/order", "dc-oreder-api/order", "dc-order-api/order" }
                : new[] { "dc-oreder-api/order", "dc-order-api/order", "ecourt-icjs-show-order-api/showOrder", "hc-order-api/order" };

            var token = await GetAccessTokenAsync();
            if (string.IsNullOrEmpty(token)) return null;

            foreach (var ep in endpoints)
            {
                foreach (var dt in dateVariants)
                {
                    var paramCombinations = new List<string>
                    {
                        $"cino={clean}|order_no={cleanOrderNo}|order_date={dt}",
                        $"cino={clean}|order_date={dt}|order_no={cleanOrderNo}",
                        $"cnr_number={clean}|order_no={cleanOrderNo}|order_date={dt}"
                    };

                    foreach (var pipe in paramCombinations)
                    {
                        try
                        {
                            string encryptedBase64 = AesEncrypt(pipe, _authKey, _iv);
                            string requestStr = Uri.EscapeDataString(encryptedBase64);
                            string requestToken = HmacSha256(pipe, _hmacKey);

                            string requestUrl = $"{_gatewayUrl}/{ep}?" +
                                                $"dept_id={Uri.EscapeDataString(_deptId)}&" +
                                                $"request_str={requestStr}&" +
                                                $"request_token={requestToken}&" +
                                                $"version={_version}";

                            using var req = new HttpRequestMessage(HttpMethod.Get, requestUrl);
                            req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                            req.Headers.Add("X-IBM-Client-Id", _apiKey);
                            if (!string.IsNullOrWhiteSpace(_secretKey)) req.Headers.Add("X-IBM-Client-Secret", _secretKey);

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
                                    }
                                }
                                catch { }

                                if (!string.IsNullOrWhiteSpace(responseStr))
                                {
                                    var decryptedBytes = AesDecryptToBytes(responseStr, _authKey, _iv);
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

        private bool IsErrorJson(JsonElement root)
        {
            if (root.ValueKind != JsonValueKind.Object) return false;

            if (root.TryGetProperty("status_code", out var codeProp))
            {
                string code = codeProp.GetString() ?? codeProp.GetRawText();
                if (!string.IsNullOrEmpty(code) && code != "200" && code != "1") return true;
            }

            if (root.TryGetProperty("status", out var statusProp) && statusProp.ValueKind == JsonValueKind.String)
            {
                string status = statusProp.GetString() ?? "";
                if (status.StartsWith("wh+", StringComparison.OrdinalIgnoreCase) || status.Contains("SXudd") || status.Contains("wh+SXudd"))
                {
                    return true;
                }
                if (status.Length >= 20 && !status.StartsWith("{") && !status.StartsWith("["))
                {
                    try
                    {
                        string dec = AesDecrypt(status, _authKey, _iv);
                        if (!string.IsNullOrWhiteSpace(dec)) status = dec;
                    }
                    catch { }
                }

                if (status.Equals("INVALID_CNR", StringComparison.OrdinalIgnoreCase) ||
                    status.Equals("RECORD NOT FOUND", StringComparison.OrdinalIgnoreCase) ||
                    status.Equals("NO DATA", StringComparison.OrdinalIgnoreCase) ||
                    status.Contains("INVALID_CNR", StringComparison.OrdinalIgnoreCase) ||
                    status.Contains("RECORD NOT FOUND", StringComparison.OrdinalIgnoreCase) ||
                    status.StartsWith("INVALID", StringComparison.OrdinalIgnoreCase) ||
                    status.StartsWith("FAIL", StringComparison.OrdinalIgnoreCase) ||
                    status.StartsWith("ERROR", StringComparison.OrdinalIgnoreCase) ||
                    status.StartsWith("wh+", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            if (root.TryGetProperty("httpCode", out var httpCodeProp))
            {
                string code = httpCodeProp.GetString() ?? httpCodeProp.GetRawText();
                if (!string.IsNullOrEmpty(code) && code != "200") return true;
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

        public async Task<JsonElement?> GetHighCourtCauselistBenchesAsync(string estCode, string causelistDate)
        {
            if (string.IsNullOrWhiteSpace(estCode)) estCode = "KAHC01";
            var dateVariants = GetDateVariants(causelistDate);

            foreach (var d in dateVariants)
            {
                string paramsStr = $"est_code={estCode}|causelist_date={d}";
                var res = await QueryApiAsync("hc-causelist-bench-api", "causelistbench", paramsStr);
                if (res.HasValue && !IsErrorJson(res.Value)) return res;
            }

            var fallback = await QueryApiAsync("hc-causelist-bench-api", "causelistbench", $"est_code={estCode}");
            if (fallback.HasValue && !IsErrorJson(fallback.Value)) return fallback;

            return await GetHighCourtBenchMasterAsync(estCode);
        }

        public async Task<JsonElement?> GetHighCourtCauselistDetailsAsync(string estCode, string benchId, string causelistDate)
        {
            if (string.IsNullOrWhiteSpace(estCode)) estCode = "KAHC01";
            var dateVariants = GetDateVariants(causelistDate);

            foreach (var d in dateVariants)
            {
                string paramsStr = !string.IsNullOrWhiteSpace(benchId) && benchId != "ALL"
                    ? $"est_code={estCode}|bench_id={benchId}|causelist_date={d}"
                    : $"est_code={estCode}|causelist_date={d}";

                var res = await QueryApiAsync("hc-causelist-details-api", "causelistdetails", paramsStr);
                if (res.HasValue && !IsErrorJson(res.Value)) return res;

                if (!string.IsNullOrWhiteSpace(benchId) && benchId != "ALL")
                {
                    res = await QueryApiAsync("hc-causelist-details-api", "causelistdetails", $"est_code={estCode}|causelist_date={d}");
                    if (res.HasValue && !IsErrorJson(res.Value)) return res;
                }

                res = await QueryApiAsync("hc-show-causelist-api", "showcauselist", paramsStr);
                if (res.HasValue && !IsErrorJson(res.Value)) return res;
            }

            return null;
        }

        public async Task<JsonElement?> GetHighCourtShowCauselistAsync(string estCode, string benchId, string causelistId, string causelistDate)
        {
            if (string.IsNullOrWhiteSpace(estCode)) estCode = "KAHC01";
            string paramsStr = $"est_code={estCode}|bench_id={benchId}|causelist_id={causelistId}|causelist_date={causelistDate}";
            return await QueryApiAsync("hc-show-causelist-api", "showcauselist", paramsStr);
        }

        public async Task<JsonElement?> GetHighCourtBenchMasterAsync(string estCode)
        {
            if (string.IsNullOrWhiteSpace(estCode)) estCode = "KAHC01";
            var res = await QueryApiAsync("hc-bench-master-api", "bench", $"est_code={estCode}");
            if (res.HasValue && !IsErrorJson(res.Value)) return res;
            return await QueryApiAsync("hc-bench-master-api", "bench", "state_code=29");
        }



        #region Cryptographic Helpers
        private static byte[] NormalizeKey(string keyStr)
        {
            byte[] raw = Encoding.UTF8.GetBytes(keyStr ?? "");
            if (raw.Length == 16 || raw.Length == 24 || raw.Length == 32) return raw;
            byte[] normalized = new byte[16];
            Array.Copy(raw, normalized, Math.Min(raw.Length, 16));
            return normalized;
        }

        private static byte[] NormalizeIV(string ivStr)
        {
            byte[] raw = Encoding.UTF8.GetBytes(ivStr ?? "");
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

            using var aes = Aes.Create();
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            aes.Key = NormalizeKey(keyStr);
            aes.IV = NormalizeIV(ivStr);

            using var decryptor = aes.CreateDecryptor();
            byte[] cipherBytes = Convert.FromBase64String(clean);
            byte[] decryptedBytes = decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);
            return Encoding.UTF8.GetString(decryptedBytes);
        }

        private static byte[] AesDecryptToBytes(string cipherTextBase64, string keyStr, string ivStr)
        {
            if (string.IsNullOrWhiteSpace(cipherTextBase64)) return Array.Empty<byte>();
            string clean = cipherTextBase64.Trim().Replace("\\/", "/").Replace("-", "+").Replace("_", "/");
            int mod4 = clean.Length % 4;
            if (mod4 > 0) clean += new string('=', 4 - mod4);

            using var aes = Aes.Create();
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            aes.Key = NormalizeKey(keyStr);
            aes.IV = NormalizeIV(ivStr);

            using var decryptor = aes.CreateDecryptor();
            byte[] cipherBytes = Convert.FromBase64String(clean);
            return decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);
        }

        private static string HmacSha256(string message, string keyStr)
        {
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(keyStr));
            byte[] hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(message));
            var sb = new StringBuilder(hash.Length * 2);
            foreach (byte b in hash)
            {
                sb.Append(b.ToString("x2"));
            }
            return sb.ToString();
        }
        #endregion
    }
}
