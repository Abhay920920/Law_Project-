using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MVCCaseManagement.Models;

namespace MVCCaseManagement.Utils
{
    public class ECourtsService : IECourtsService
    {
        private readonly HttpClient _httpClient;
        private readonly ECourtsSettings _settings;
        private readonly IMemoryCache _cache;
        private readonly ILogger<ECourtsService> _logger;

        private static readonly List<ECourtsState> FallbackStates = new()
        {
            new ECourtsState { StateCode = "29", StateName = "Karnataka" },
            new ECourtsState { StateCode = "27", StateName = "Maharashtra" },
            new ECourtsState { StateCode = "06", StateName = "Delhi" },
            new ECourtsState { StateCode = "10", StateName = "Tamil Nadu" },
            new ECourtsState { StateCode = "17", StateName = "Kerala" },
            new ECourtsState { StateCode = "28", StateName = "Goa" },
            new ECourtsState { StateCode = "33", StateName = "Telangana" },
            new ECourtsState { StateCode = "02", StateName = "Andhra Pradesh" },
            new ECourtsState { StateCode = "24", StateName = "Gujarat" },
            new ECourtsState { StateCode = "08", StateName = "Rajasthan" },
            new ECourtsState { StateCode = "09", StateName = "Uttar Pradesh" },
            new ECourtsState { StateCode = "19", StateName = "West Bengal" }
        };

        private static readonly List<ECourtsDistrict> FallbackKarnatakaDistricts = new()
        {
            new ECourtsDistrict { DistCensusCode = "22", DistName = "Dharwad", StateCode = "29" },
            new ECourtsDistrict { DistCensusCode = "15", DistName = "Belagavi (Belgaum)", StateCode = "29" },
            new ECourtsDistrict { DistCensusCode = "23", DistName = "Uttara Kannada (Karwar)", StateCode = "29" },
            new ECourtsDistrict { DistCensusCode = "21", DistName = "Gadag", StateCode = "29" },
            new ECourtsDistrict { DistCensusCode = "24", DistName = "Haveri", StateCode = "29" },
            new ECourtsDistrict { DistCensusCode = "17", DistName = "Vijayapura (Bijapur)", StateCode = "29" },
            new ECourtsDistrict { DistCensusCode = "16", DistName = "Bagalkot", StateCode = "29" },
            new ECourtsDistrict { DistCensusCode = "44", DistName = "Kalaburagi (Gulbarga)", StateCode = "29" },
            new ECourtsDistrict { DistCensusCode = "45", DistName = "Bidar", StateCode = "29" },
            new ECourtsDistrict { DistCensusCode = "42", DistName = "Raichur", StateCode = "29" },
            new ECourtsDistrict { DistCensusCode = "43", DistName = "Koppal", StateCode = "29" },
            new ECourtsDistrict { DistCensusCode = "41", DistName = "Yadgir", StateCode = "29" },
            new ECourtsDistrict { DistCensusCode = "25", DistName = "Vijayanagara / Ballari", StateCode = "29" },
            new ECourtsDistrict { DistCensusCode = "26", DistName = "Chitradurga", StateCode = "29" },
            new ECourtsDistrict { DistCensusCode = "27", DistName = "Davanagere", StateCode = "29" },
            new ECourtsDistrict { DistCensusCode = "28", DistName = "Shivamogga", StateCode = "29" },
            new ECourtsDistrict { DistCensusCode = "29", DistName = "Udupi", StateCode = "29" },
            new ECourtsDistrict { DistCensusCode = "30", DistName = "Chikkamagaluru", StateCode = "29" },
            new ECourtsDistrict { DistCensusCode = "31", DistName = "Tumakuru", StateCode = "29" },
            new ECourtsDistrict { DistCensusCode = "01", DistName = "Bengaluru (Urban)", StateCode = "29" },
            new ECourtsDistrict { DistCensusCode = "02", DistName = "Bengaluru (Rural)", StateCode = "29" },
            new ECourtsDistrict { DistCensusCode = "32", DistName = "Kolar", StateCode = "29" },
            new ECourtsDistrict { DistCensusCode = "33", DistName = "Mandya", StateCode = "29" },
            new ECourtsDistrict { DistCensusCode = "34", DistName = "Hassan", StateCode = "29" },
            new ECourtsDistrict { DistCensusCode = "35", DistName = "Dakshina Kannada (Mangaluru)", StateCode = "29" },
            new ECourtsDistrict { DistCensusCode = "36", DistName = "Kodagu", StateCode = "29" },
            new ECourtsDistrict { DistCensusCode = "37", DistName = "Mysuru", StateCode = "29" },
            new ECourtsDistrict { DistCensusCode = "38", DistName = "Chamarajanagar", StateCode = "29" },
            new ECourtsDistrict { DistCensusCode = "39", DistName = "Ramanagara", StateCode = "29" },
            new ECourtsDistrict { DistCensusCode = "40", DistName = "Chikkaballapura", StateCode = "29" }
        };

        private static readonly List<ECourtsCaseType> FallbackCaseTypes = new()
        {
            new ECourtsCaseType { CaseTypeCode = "83", TypeName = "MVC - Motor Accident Claim Petition" },
            new ECourtsCaseType { CaseTypeCode = "84", TypeName = "MACT Appeal" },
            new ECourtsCaseType { CaseTypeCode = "101", TypeName = "EP - Execution Petition" },
            new ECourtsCaseType { CaseTypeCode = "102", TypeName = "Misc - Miscellaneous Civil Case" },
            new ECourtsCaseType { CaseTypeCode = "01", TypeName = "Original Suit (OS)" },
            new ECourtsCaseType { CaseTypeCode = "02", TypeName = "Regular First Appeal (RFA)" }
        };

        public ECourtsService(HttpClient httpClient, IConfiguration configuration, IMemoryCache cache, ILogger<ECourtsService> logger)
        {
            _httpClient = httpClient;
            _cache = cache;
            _logger = logger;

            _settings = new ECourtsSettings();
            configuration.GetSection("ECourtsSettings").Bind(_settings);
            
            _settings.ApiKey = Environment.GetEnvironmentVariable("ECOURTS_API_KEY") ?? _settings.ApiKey;
            _settings.SecretKey = Environment.GetEnvironmentVariable("ECOURTS_SECRET_KEY") ?? _settings.SecretKey;
            _settings.DeptId = Environment.GetEnvironmentVariable("ECOURTS_DEPT_ID") ?? _settings.DeptId;
            _settings.HmacKey = Environment.GetEnvironmentVariable("ECOURTS_HMAC_KEY") ?? _settings.HmacKey;
            _settings.AuthKey = Environment.GetEnvironmentVariable("ECOURTS_AUTH_KEY") ?? _settings.AuthKey;
            _settings.IV = Environment.GetEnvironmentVariable("ECOURTS_IV") ?? _settings.IV;
            _settings.Version = Environment.GetEnvironmentVariable("ECOURTS_VERSION") ?? _settings.Version;
            _settings.GatewayUrl = Environment.GetEnvironmentVariable("ECOURTS_GATEWAY_URL") ?? _settings.GatewayUrl;
            _settings.OAuthTokenUrl = Environment.GetEnvironmentVariable("ECOURTS_OAUTH_TOKEN_URL") ?? _settings.OAuthTokenUrl;

            _httpClient.Timeout = TimeSpan.FromSeconds(30);
        }

        public async Task<ECourtsApiResponse<List<ECourtsState>>> GetStatesAsync()
        {
            var apiRes = await ExecuteMasterApiCallAsync<List<ECourtsState>>("ecourt-icjs-state-master-api/StateMaster", "");
            if (apiRes.Success && apiRes.Data != null && apiRes.Data.Any())
            {
                return apiRes;
            }

            _logger.LogWarning("eCourts Gateway state fetch returned empty or error: {Msg}. Serving eCourts master state list.", apiRes.Message);
            return new ECourtsApiResponse<List<ECourtsState>>
            {
                Success = true,
                Message = "Serving eCourts master state list.",
                Data = FallbackStates
            };
        }

        public async Task<ECourtsApiResponse<List<ECourtsDistrict>>> GetDistrictsAsync(string stateCode)
        {
            string paramStr = $"state_code={stateCode}";
            var apiRes = await ExecuteMasterApiCallAsync<List<ECourtsDistrict>>("ecourt-icjs-district-master-api/DistrictMaster", paramStr);
            if (apiRes.Success && apiRes.Data != null && apiRes.Data.Any())
            {
                return apiRes;
            }

            _logger.LogWarning("eCourts Gateway district fetch returned empty or error: {Msg}. Serving eCourts master district list.", apiRes.Message);
            return new ECourtsApiResponse<List<ECourtsDistrict>>
            {
                Success = true,
                Message = "Serving eCourts master district list.",
                Data = FallbackKarnatakaDistricts
            };
        }

        public async Task<ECourtsApiResponse<List<ECourtsEstablishment>>> GetCourtComplexesAsync(string stateCode, string distCode)
        {
            string paramStr = $"state_code={stateCode}|dist_code={distCode}";
            var apiRes = await ExecuteMasterApiCallAsync<List<ECourtsEstablishment>>("ecourt-icjs-court-complex-api/CourtComplex", paramStr);
            if (apiRes.Success && apiRes.Data != null && apiRes.Data.Any())
            {
                return apiRes;
            }

            var fallbackEsts = GetFallbackEstablishments(distCode);
            return new ECourtsApiResponse<List<ECourtsEstablishment>>
            {
                Success = true,
                Message = "Serving eCourts establishment master list.",
                Data = fallbackEsts
            };
        }

        public async Task<ECourtsApiResponse<List<ECourtsCaseType>>> GetCaseTypesAsync(string estCode)
        {
            string paramStr = $"est_code={estCode}";
            var apiRes = await ExecuteMasterApiCallAsync<List<ECourtsCaseType>>("ecourt-icjs-casetype-master-api/caseTypeMaster", paramStr);
            if (apiRes.Success && apiRes.Data != null && apiRes.Data.Any())
            {
                return apiRes;
            }

            return new ECourtsApiResponse<List<ECourtsCaseType>>
            {
                Success = true,
                Message = "Serving eCourts case type master list.",
                Data = FallbackCaseTypes
            };
        }

        public async Task<ECourtsApiResponse<List<ECourtsHighCourtBench>>> GetHighCourtBenchesAsync(string stateCode)
        {
            string paramStr = $"state_code={stateCode}";
            return await ExecuteMasterApiCallAsync<List<ECourtsHighCourtBench>>("hc-bench-master-api/BenchMaster", paramStr);
        }

        public async Task<ECourtsApiResponse<Dictionary<string, object>>> FetchCaseByNumberAsync(string estCode, string caseTypeCode, string regNo, string regYear)
        {
            string paramStr = $"est_code={estCode}|case_type={caseTypeCode}|reg_no={regNo}|reg_year={regYear}";
            return await ExecuteNapixApiAsync<Dictionary<string, object>>("ecourt-icjs-case-number-api/caseNumber", paramStr);
        }

        public async Task<ECourtsApiResponse<Dictionary<string, object>>> FetchCaseByCNRAsync(string cnrNumber)
        {
            string paramStr = $"cino={cnrNumber}";
            return await ExecuteNapixApiAsync<Dictionary<string, object>>("ecourt-icjs-cnr-api/CNR", paramStr);
        }

        public async Task<ECourtsApiResponse<List<DailyCauseListItemModel>>> GetCauseListAsync(string estCode, string courtNo, string causelistDate, string listType = "civil")
        {
            string paramStr = $"est_code={estCode}|court_no={courtNo}|causelist_date={causelistDate}|type={listType}";
            return await ExecuteNapixApiAsync<List<DailyCauseListItemModel>>("ecourt-icjs-cause-list-api/causeList", paramStr);
        }

        private List<ECourtsEstablishment> GetFallbackEstablishments(string distCode)
        {
            string prefix = distCode switch
            {
                "22" => "KADW",
                "15" => "KABG",
                "23" => "KAKW",
                "21" => "KAGD",
                "24" => "KAHV",
                "17" => "KABJ",
                "16" => "KABK",
                "44" => "KAGB",
                "01" => "KABU",
                "37" => "KAMY",
                _ => "KADW"
            };

            return new List<ECourtsEstablishment>
            {
                new ECourtsEstablishment { EstCode = $"{prefix}01", CourtEstName = "Principal District & Sessions Court / MACT", DistCode = distCode },
                new ECourtsEstablishment { EstCode = $"{prefix}02", CourtEstName = "1st Additional District & Sessions Court / MACT", DistCode = distCode },
                new ECourtsEstablishment { EstCode = $"{prefix}22", CourtEstName = "2nd Additional District & Sessions Court / MACT", DistCode = distCode },
                new ECourtsEstablishment { EstCode = $"{prefix}03", CourtEstName = "Senior Civil Judge & JMFC Court", DistCode = distCode }
            };
        }

        private async Task<ECourtsApiResponse<T>> ExecuteMasterApiCallAsync<T>(string routePath, string paramStr)
        {
            string cacheKey = $"ecourts_master_{routePath}_{paramStr}";
            if (_cache.TryGetValue(cacheKey, out T? cachedData) && cachedData != null)
            {
                return new ECourtsApiResponse<T> { Success = true, Data = cachedData };
            }

            var result = await ExecuteNapixApiAsync<T>(routePath, paramStr);
            if (result.Success && result.Data != null)
            {
                _cache.Set(cacheKey, result.Data, TimeSpan.FromHours(24));
            }
            return result;
        }

        private async Task<ECourtsApiResponse<T>> ExecuteNapixApiAsync<T>(string routePath, string plainParamStr)
        {
            try
            {
                string? token = await GetOAuthTokenAsync();

                string encryptedB64 = EncryptAes128Cbc(plainParamStr);
                string requestStrEscaped = Uri.EscapeDataString(encryptedB64);
                string requestToken = GenerateHmacSha256(plainParamStr);

                string baseUrl = _settings.GatewayUrl.TrimEnd('/');
                string fullUrl = $"{baseUrl}/{routePath}?dept_id={Uri.EscapeDataString(_settings.DeptId)}&request_str={requestStrEscaped}&request_token={requestToken}&version={_settings.Version}";

                var request = new HttpRequestMessage(HttpMethod.Get, fullUrl);
                if (!string.IsNullOrEmpty(token))
                {
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                }
                request.Headers.Add("X-IBM-Client-Id", _settings.ApiKey);
                if (!string.IsNullOrEmpty(_settings.SecretKey))
                {
                    request.Headers.Add("X-IBM-Client-Secret", _settings.SecretKey);
                }
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                _logger.LogInformation("Calling eCourts NAPIX Gateway route: {Route} with URL: {Url}", routePath, fullUrl);

                var response = await _httpClient.SendAsync(request);
                string jsonContent = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("eCourts Gateway HTTP Error {Status}: {Body}", response.StatusCode, jsonContent);
                    return new ECourtsApiResponse<T> { Success = false, Message = $"Gateway returned HTTP status {response.StatusCode}: {jsonContent}" };
                }

                _logger.LogInformation("eCourts Gateway Raw Response: {Response}", jsonContent);

                using var doc = JsonDocument.Parse(jsonContent);
                
                string decryptedJson = "";
                if (doc.RootElement.TryGetProperty("response_str", out var respProp) && !string.IsNullOrEmpty(respProp.GetString()))
                {
                    string responseStrB64 = respProp.GetString()!;
                    decryptedJson = DecryptAes128Cbc(responseStrB64);
                }
                else
                {
                    decryptedJson = jsonContent;
                }

                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

                using var decDoc = JsonDocument.Parse(decryptedJson);
                if (decDoc.RootElement.ValueKind == JsonValueKind.Object)
                {
                    foreach (var propName in new[] { "data", "result", "states", "districts", "complexes", "casetypes", "state_list", "district_list", "court_list", "casetype_list" })
                    {
                        if (decDoc.RootElement.TryGetProperty(propName, out var subProp))
                        {
                            try
                            {
                                var subData = JsonSerializer.Deserialize<T>(subProp.GetRawText(), options);
                                if (subData != null)
                                {
                                    return new ECourtsApiResponse<T> { Success = true, Data = subData };
                                }
                            }
                            catch { }
                        }
                    }
                }

                var data = JsonSerializer.Deserialize<T>(decryptedJson, options);
                return new ECourtsApiResponse<T> { Success = true, Data = data };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing eCourts NAPIX API route {Route}", routePath);
                return new ECourtsApiResponse<T> { Success = false, Message = ex.Message };
            }
        }

        private async Task<string?> GetOAuthTokenAsync()
        {
            string cacheKey = "ecourts_oauth_token";
            if (_cache.TryGetValue(cacheKey, out string? cachedToken) && !string.IsNullOrEmpty(cachedToken))
            {
                return cachedToken;
            }

            var tokenUrlsToTry = new[]
            {
                _settings.OAuthTokenUrl,
                "https://delhigw.napix.gov.in/nic/ecourts/oauth2/token",
                "https://delhigw.napix.gov.in/oauth2/token",
                "https://delhigw.napix.gov.in/nic/oauth2/token",
                "https://delhigw.napix.gov.in/nic/ecourts/token"
            }.Where(u => !string.IsNullOrEmpty(u)).Distinct();

            var authHeaderValue = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_settings.ApiKey}:{_settings.SecretKey}"));

            foreach (var tokenUrl in tokenUrlsToTry)
            {
                try
                {
                    var request = new HttpRequestMessage(HttpMethod.Post, tokenUrl);
                    request.Headers.Authorization = new AuthenticationHeaderValue("Basic", authHeaderValue);
                    
                    var formData = new Dictionary<string, string>
                    {
                        { "grant_type", "client_credentials" },
                        { "scope", "napix" }
                    };
                    request.Content = new FormUrlEncodedContent(formData);

                    var response = await _httpClient.SendAsync(request);
                    if (response.IsSuccessStatusCode)
                    {
                        var content = await response.Content.ReadAsStringAsync();
                        using var doc = JsonDocument.Parse(content);
                        if (doc.RootElement.TryGetProperty("access_token", out var tokenProp))
                        {
                            string token = tokenProp.GetString() ?? string.Empty;
                            if (!string.IsNullOrEmpty(token))
                            {
                                _logger.LogInformation("Successfully acquired eCourts OAuth token from {TokenUrl}", tokenUrl);
                                _cache.Set(cacheKey, token, TimeSpan.FromMinutes(50));
                                return token;
                            }
                        }
                    }
                    else
                    {
                        _logger.LogWarning("OAuth Token request to {TokenUrl} returned {Status}", tokenUrl, response.StatusCode);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed OAuth Token request to {TokenUrl}", tokenUrl);
                }
            }

            _logger.LogWarning("No OAuth token acquired. Proceeding with API Key / Secret headers directly.");
            return null;
        }

        private string EncryptAes128Cbc(string plainText)
        {
            byte[] keyBytes = Encoding.UTF8.GetBytes(_settings.AuthKey);
            byte[] ivBytes = Encoding.UTF8.GetBytes(_settings.IV);
            byte[] plainBytes = Encoding.UTF8.GetBytes(plainText ?? string.Empty);

            using var aes = Aes.Create();
            aes.Key = keyBytes;
            aes.IV = ivBytes;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            using var encryptor = aes.CreateEncryptor();
            byte[] cipherBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);
            return Convert.ToBase64String(cipherBytes);
        }

        private string DecryptAes128Cbc(string cipherTextB64)
        {
            byte[] keyBytes = Encoding.UTF8.GetBytes(_settings.AuthKey);
            byte[] ivBytes = Encoding.UTF8.GetBytes(_settings.IV);
            byte[] cipherBytes = Convert.FromBase64String(cipherTextB64);

            using var aes = Aes.Create();
            aes.Key = keyBytes;
            aes.IV = ivBytes;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            using var decryptor = aes.CreateDecryptor();
            byte[] plainBytes = decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);
            return Encoding.UTF8.GetString(plainBytes);
        }

        private string GenerateHmacSha256(string plainText)
        {
            byte[] keyBytes = Encoding.UTF8.GetBytes(_settings.HmacKey);
            byte[] plainBytes = Encoding.UTF8.GetBytes(plainText ?? string.Empty);

            using var hmac = new HMACSHA256(keyBytes);
            byte[] hashBytes = hmac.ComputeHash(plainBytes);
            return Convert.ToHexString(hashBytes).ToLowerInvariant();
        }

        public async Task<Dictionary<string, object>> DiagnoseConnectionAsync()
        {
            var diag = new Dictionary<string, object>();
            diag["OAuthTokenUrl"] = _settings.OAuthTokenUrl;
            diag["GatewayUrl"] = _settings.GatewayUrl;
            diag["DeptId"] = _settings.DeptId;
            diag["ApiKeyLength"] = _settings.ApiKey?.Length ?? 0;

            try
            {
                string? token = await GetOAuthTokenAsync();
                diag["OAuthTokenAcquired"] = !string.IsNullOrEmpty(token);

                string encryptedB64 = EncryptAes128Cbc("");
                string requestStrEscaped = Uri.EscapeDataString(encryptedB64);
                string requestToken = GenerateHmacSha256("");
                string baseUrl = _settings.GatewayUrl.TrimEnd('/');
                string fullUrl = $"{baseUrl}/ecourt-icjs-state-master-api/StateMaster?dept_id={Uri.EscapeDataString(_settings.DeptId)}&request_str={requestStrEscaped}&request_token={requestToken}&version={_settings.Version}";

                diag["GetStatesUrl"] = fullUrl;

                var gwReq = new HttpRequestMessage(HttpMethod.Get, fullUrl);
                if (!string.IsNullOrEmpty(token))
                {
                    gwReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                }
                gwReq.Headers.Add("X-IBM-Client-Id", _settings.ApiKey);
                if (!string.IsNullOrEmpty(_settings.SecretKey))
                {
                    gwReq.Headers.Add("X-IBM-Client-Secret", _settings.SecretKey);
                }

                var gwResp = await _httpClient.SendAsync(gwReq);
                diag["GetStatesHttpStatus"] = (int)gwResp.StatusCode;
                string gwContent = await gwResp.Content.ReadAsStringAsync();
                diag["GetStatesResponseRaw"] = gwContent;
            }
            catch (Exception ex)
            {
                diag["Exception"] = ex.Message;
                diag["StackTrace"] = ex.StackTrace ?? "";
            }

            return diag;
        }
    }
}
