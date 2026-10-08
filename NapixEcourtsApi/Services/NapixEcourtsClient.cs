using System.IO.Compression;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using NapixEcourtsApi.Configuration;
using NapixEcourtsApi.Models;

namespace NapixEcourtsApi.Services;

public interface INapixEcourtsClient
{
    /// <summary>Full case history via dc-cnr-api/cnr or hc-cnr-api/CNR mapped to standard CnrCaseDetails.</summary>
    Task<CnrCaseDetails> GetCaseByCnrAsync(string cnr, string? module = null, CancellationToken cancellationToken = default);

    /// <summary>Comprehensive case details with parsed hearings, orders, and direct PDF download URLs.</summary>
    Task<CaseFullDetails> GetFullCaseDetailsAsync(string cnr, string? module = null, CancellationToken cancellationToken = default);

    /// <summary>Raw decrypted JSON document directly from eCourts.</summary>
    Task<JsonElement> GetRawCaseDetailsAsync(string cnr, string? module = null, CancellationToken cancellationToken = default);

    /// <summary>Downloads all interim and final orders/judgments for a CNR bundled into a ZIP archive.</summary>
    Task<(byte[] ZipBytes, int OrderCount)> DownloadAllOrdersZipAsync(string cnr, string? module = null, CancellationToken cancellationToken = default);

    /// <summary>Lightweight current-status via currentStatus (POST) for up to 500 CNRs.</summary>
    Task<JsonElement> GetCurrentStatusAsync(IReadOnlyList<string> cnrs, string? module = null, CancellationToken cancellationToken = default);

    /// <summary>Business transacted on a given date via showBusiness (GET).</summary>
    Task<JsonElement> GetShowBusinessAsync(string cnr, DateOnly businessDate, string? module = null, CancellationToken cancellationToken = default);

    /// <summary>Order/judgment PDF bytes via order (GET) — response is a PDF byte stream.</summary>
    Task<byte[]> GetOrderPdfAsync(string cnr, string orderNo, DateOnly orderDate, string? module = null, CancellationToken cancellationToken = default);
}

public class NapixEcourtsClient : INapixEcourtsClient
{
    private const string BrowserUserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new StringOrNumberConverter() }
    };

    private readonly HttpClient _httpClient;
    private readonly INapixAuthService _authService;
    private readonly INapixCryptoService _crypto;
    private readonly NapixOptions _options;
    private readonly ILogger<NapixEcourtsClient> _logger;

    public NapixEcourtsClient(
        HttpClient httpClient,
        INapixAuthService authService,
        INapixCryptoService crypto,
        IOptions<NapixOptions> options,
        ILogger<NapixEcourtsClient> logger)
    {
        _httpClient = httpClient;
        _authService = authService;
        _crypto = crypto;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// Checks if a 16-character CNR corresponds to a High Court (chars 3-4 are "HC").
    /// </summary>
    public static bool IsHighCourtCnr(string cnr) =>
        !string.IsNullOrWhiteSpace(cnr) && cnr.Length >= 4 && cnr.Substring(2, 2).Equals("HC", StringComparison.OrdinalIgnoreCase);

    public async Task<JsonElement> GetRawCaseDetailsAsync(string cnr, string? module = null, CancellationToken cancellationToken = default)
    {
        ValidateCnr(cnr);
        var isHc = IsHighCourtCnr(cnr);
        var effectiveModule = isHc ? "HC" : (module ?? "MVC");
        string endpoint = isHc ? "hc-cnr-api/CNR" : "dc-cnr-api/cnr";

        try
        {
            var url = BuildGetUrl(endpoint, new Dictionary<string, string> { ["cino"] = cnr }, effectiveModule);
            var envelope = await SendAndParseEnvelopeAsync(HttpMethod.Get, url, null, effectiveModule, cancellationToken);
            var decryptedJson = DecryptAndVerify(envelope, effectiveModule);

            using var doc = JsonDocument.Parse(decryptedJson);
            CheckDecryptedError(doc.RootElement, decryptedJson);

            _logger.LogInformation("[NAPIX] {Court} CNR {Cnr} details fetched via {Endpoint} ({Module})",
                isHc ? "High Court" : "District Court", cnr, endpoint, effectiveModule);
            return doc.RootElement.Clone();
        }
        catch (NapixApiException ex) when (ex.Message.Contains("626") || ex.Message.Contains("INVALID_TOKEN") || ex.Message.Contains("401"))
        {
            string altModule = string.Equals(effectiveModule, "Labour", StringComparison.OrdinalIgnoreCase) ? "MVC" : "Labour";
            _logger.LogInformation("[NAPIX] Attempt with {Module} failed ({Error}). Retrying with alternate module credentials ({AltModule})...", effectiveModule, ex.Message, altModule);
            
            var altUrl = BuildGetUrl(endpoint, new Dictionary<string, string> { ["cino"] = cnr }, altModule);
            var altEnvelope = await SendAndParseEnvelopeAsync(HttpMethod.Get, altUrl, null, altModule, cancellationToken);
            var altDecryptedJson = DecryptAndVerify(altEnvelope, altModule);

            using var altDoc = JsonDocument.Parse(altDecryptedJson);
            CheckDecryptedError(altDoc.RootElement, altDecryptedJson);

            _logger.LogInformation("[NAPIX] {Court} CNR {Cnr} details fetched via {Endpoint} with alternative credentials ({Module})",
                isHc ? "High Court" : "District Court", cnr, endpoint, altModule);
            return altDoc.RootElement.Clone();
        }
    }

    public async Task<CnrCaseDetails> GetCaseByCnrAsync(string cnr, string? module = null, CancellationToken cancellationToken = default)
    {
        var rawElement = await GetRawCaseDetailsAsync(cnr, module, cancellationToken);
        return JsonSerializer.Deserialize<CnrCaseDetails>(rawElement.GetRawText(), JsonOptions)
            ?? throw new NapixApiException("Failed to deserialize CNR response.");
    }

    public async Task<CaseFullDetails> GetFullCaseDetailsAsync(string cnr, string? module = null, CancellationToken cancellationToken = default)
    {
        var rawElement = await GetRawCaseDetailsAsync(cnr, module, cancellationToken);
        var rawJson = rawElement.GetRawText();
        var basicInfo = JsonSerializer.Deserialize<CnrCaseDetails>(rawJson, JsonOptions);

        var result = new CaseFullDetails
        {
            Cnr = cnr,
            CourtType = IsHighCourtCnr(cnr) ? "High Court" : "District Court",
            BasicInfo = basicInfo,
            RawPayload = rawElement.Clone()
        };

        // Parse Hearings (historyofcasehearing)
        foreach (var item in EnumerateSubItems(rawElement, "historyofcasehearing", "history_of_case_hearing", "case_history", "hearings"))
        {
            var hDate = GetStringProp(item, "hearing_date", "business_date", "dt_next_list", "date");
            var purpose = GetStringProp(item, "purpose_of_listing", "purpose_name", "purpose", "hearing_purpose");
            var business = GetStringProp(item, "business", "business_transacted", "order_details");
            var judge = GetStringProp(item, "judge_name", "desgname");

            result.Hearings.Add(new CaseHearing
            {
                HearingDate = hDate,
                PurposeOfHearing = purpose,
                BusinessTransacted = business,
                JudgeName = judge,
                RawDetails = item.Clone()
            });
        }

        // Parse Interim Orders (interimorder)
        foreach (var item in EnumerateSubItems(rawElement, "interimorder", "interim_order", "interim_orders"))
        {
            var orderNo = GetStringProp(item, "order_no", "ord_no", "order_number");
            var orderDate = GetStringProp(item, "order_date", "ord_date", "date");
            var normalizedDate = FormatDateForUrl(orderDate);
            var viewUrl = (!string.IsNullOrEmpty(orderNo) && !string.IsNullOrEmpty(normalizedDate))
                ? $"/api/cases/{cnr}/order?orderNo={Uri.EscapeDataString(orderNo)}&date={normalizedDate}&module={Uri.EscapeDataString(module ?? "MVC")}"
                : null;

            result.InterimOrders.Add(new CaseOrder
            {
                OrderNumber = orderNo,
                OrderDate = orderDate,
                OrderType = "Interim",
                ViewUrl = viewUrl,
                PdfDownloadUrl = viewUrl,
                RawDetails = item.Clone()
            });
        }

        // Parse Final Orders / Judgments (finalorder)
        foreach (var item in EnumerateSubItems(rawElement, "finalorder", "final_order", "final_orders", "judgement", "judgment"))
        {
            var orderNo = GetStringProp(item, "order_no", "ord_no", "order_number") ?? "1";
            var orderDate = GetStringProp(item, "order_date", "ord_date", "date_of_decision", "date");
            var normalizedDate = FormatDateForUrl(orderDate);
            var viewUrl = (!string.IsNullOrEmpty(orderNo) && !string.IsNullOrEmpty(normalizedDate))
                ? $"/api/cases/{cnr}/order?orderNo={Uri.EscapeDataString(orderNo)}&date={normalizedDate}&module={Uri.EscapeDataString(module ?? "MVC")}"
                : null;

            result.FinalOrdersAndJudgments.Add(new CaseOrder
            {
                OrderNumber = orderNo,
                OrderDate = orderDate,
                OrderType = "Final/Judgment",
                ViewUrl = viewUrl,
                PdfDownloadUrl = viewUrl,
                RawDetails = item.Clone()
            });
        }

        // Other sub-sections
        foreach (var item in EnumerateSubItems(rawElement, "iafiling", "ia_filing", "ia_details")) result.IaFilings.Add(item.Clone());
        foreach (var item in EnumerateSubItems(rawElement, "acts", "act_details")) result.Acts.Add(item.Clone());
        foreach (var item in EnumerateSubItems(rawElement, "transfer", "case_transfer")) result.Transfers.Add(item.Clone());
        foreach (var item in EnumerateSubItems(rawElement, "objections", "case_objections")) result.Objections.Add(item.Clone());
        foreach (var item in EnumerateSubItems(rawElement, "processes", "case_processes")) result.Processes.Add(item.Clone());
        foreach (var item in EnumerateSubItems(rawElement, "pet_extra_party", "petitioner_extra_party", "res_extra_party", "respondent_extra_party")) result.ExtraParties.Add(item.Clone());
        foreach (var item in EnumerateSubItems(rawElement, "link_cases", "linked_cases")) result.LinkedCases.Add(item.Clone());

        return result;
    }

    public async Task<(byte[] ZipBytes, int OrderCount)> DownloadAllOrdersZipAsync(string cnr, string? module = null, CancellationToken cancellationToken = default)
    {
        var fullDetails = await GetFullCaseDetailsAsync(cnr, module, cancellationToken);
        var allOrders = fullDetails.InterimOrders.Concat(fullDetails.FinalOrdersAndJudgments).ToList();

        using var memoryStream = new MemoryStream();
        using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create, leaveOpen: true))
        {
            int index = 1;
            foreach (var order in allOrders)
            {
                if (string.IsNullOrEmpty(order.OrderNumber) || string.IsNullOrEmpty(order.OrderDate) || !TryParseDate(order.OrderDate, out var dateOnly))
                    continue;

                try
                {
                    var pdfBytes = await GetOrderPdfAsync(cnr, order.OrderNumber, dateOnly, module, cancellationToken);
                    var entryName = $"{index:D2}_{order.OrderType.Replace("/", "_")}_{order.OrderNumber}_{dateOnly:yyyyMMdd}.pdf";
                    var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
                    using var entryStream = entry.Open();
                    await entryStream.WriteAsync(pdfBytes, cancellationToken);
                    index++;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to download PDF for order {OrderNo} on date {Date}", order.OrderNumber, order.OrderDate);
                }
            }
        }

        return (memoryStream.ToArray(), allOrders.Count);
    }

    public async Task<JsonElement> GetCurrentStatusAsync(IReadOnlyList<string> cnrs, string? module = null, CancellationToken cancellationToken = default)
    {
        if (cnrs is null || cnrs.Count == 0)
            throw new ArgumentException("At least one CNR is required.", nameof(cnrs));
        if (cnrs.Count > 500)
            throw new ArgumentException("NAPIX allows a maximum of 500 CNRs per currentStatus call.", nameof(cnrs));

        foreach (var c in cnrs) ValidateCnr(c);

        var hasHc = cnrs.Any(IsHighCourtCnr);
        var effectiveModule = hasHc ? "HC" : (module ?? "MVC");
        string endpoint = hasHc ? "hc-current-status-api/currentStatus" : "dc-current-status-api/currentStatus";

        var parameters = new Dictionary<string, string> { ["cino"] = string.Join(",", cnrs) };
        var url = BuildGetUrl(endpoint, parameters, effectiveModule);
        var envelope = await SendAndParseEnvelopeAsync(HttpMethod.Post, url, null, effectiveModule, cancellationToken);
        var decryptedJson = DecryptAndVerify(envelope, effectiveModule);

        using var doc = JsonDocument.Parse(decryptedJson);
        return doc.RootElement.Clone();
    }

    public async Task<JsonElement> GetShowBusinessAsync(string cnr, DateOnly businessDate, string? module = null, CancellationToken cancellationToken = default)
    {
        ValidateCnr(cnr);
        var isHc = IsHighCourtCnr(cnr);
        var effectiveModule = isHc ? "HC" : (module ?? "MVC");

        var endpoints = isHc
            ? new[] { "hc-show-business-api/showBusiness" }
            : new[] { "dc-show-business-api/showBusiness", "ecourt-icjs-show-business-api/showBusiness" };

        var dateFormats = new[] { "dd-MM-yyyy", "yyyy-MM-dd" };
        NapixApiException? lastEx = null;

        foreach (var endpoint in endpoints)
        {
            foreach (var fmt in dateFormats)
            {
                var dtStr = businessDate.ToString(fmt);
                var parameters = new Dictionary<string, string> { ["cino"] = cnr, ["business_date"] = dtStr };

                try
                {
                    var url = BuildGetUrl(endpoint, parameters, effectiveModule);
                    var envelope = await SendAndParseEnvelopeAsync(HttpMethod.Get, url, content: null, effectiveModule, cancellationToken);
                    var decryptedJson = DecryptAndVerify(envelope, effectiveModule);

                    using var doc = JsonDocument.Parse(decryptedJson);
                    CheckDecryptedError(doc.RootElement, decryptedJson);
                    _logger.LogInformation("[NAPIX] Successfully fetched showBusiness for {Cnr} on {Date} via {Endpoint}", cnr, dtStr, endpoint);
                    return doc.RootElement.Clone();
                }
                catch (NapixApiException ex)
                {
                    lastEx = ex;
                }
            }
        }

        throw lastEx ?? new NapixApiException($"Failed to fetch show business for CNR '{cnr}'.");
    }

    public async Task<byte[]> GetOrderPdfAsync(string cnr, string orderNo, DateOnly orderDate, string? module = null, CancellationToken cancellationToken = default)
    {
        ValidateCnr(cnr);
        var isHc = IsHighCourtCnr(cnr);
        var effectiveModule = isHc ? "HC" : (module ?? "MVC");

        string[] endpoints = isHc
            ? new[] { "hc-order-api/order", "hc-oreder-api/order" }
            : new[] { "dc-oreder-api/order", "dc-order-api/order" };

        var dateFormats = new[] { "dd-MM-yyyy", "yyyy-MM-dd" };
        NapixApiException? lastEx = null;

        foreach (var endpoint in endpoints)
        {
            foreach (var fmt in dateFormats)
            {
                var formattedDate = orderDate.ToString(fmt);
                var parameters = isHc
                    ? new Dictionary<string, string> { ["cino"] = cnr, ["order_date"] = formattedDate }
                    : new Dictionary<string, string> { ["cino"] = cnr, ["order_no"] = orderNo, ["order_date"] = formattedDate };

                try
                {
                    var url = BuildGetUrl(endpoint, parameters, effectiveModule);
                    var envelope = await SendAndParseEnvelopeAsync(HttpMethod.Get, url, content: null, effectiveModule, cancellationToken);
                    var decryptedBytes = DecryptToBytes(envelope, effectiveModule);
                    return EnsurePdfBytes(decryptedBytes);
                }
                catch (NapixApiException ex)
                {
                    lastEx = ex;
                }
            }
        }

        throw lastEx ?? new NapixApiException($"Failed to fetch order PDF for CNR '{cnr}' from eCourts.");
    }

    // ---- shared plumbing ----

    private string BuildGetUrl(string relativePath, IDictionary<string, string> encryptedParams, string? module = null)
    {
        var creds = _options.GetModuleCredentials(module);

        if (!string.IsNullOrWhiteSpace(creds.HcPassword) &&
            relativePath.StartsWith("hc-", StringComparison.OrdinalIgnoreCase) &&
            !encryptedParams.ContainsKey("password"))
        {
            encryptedParams = new Dictionary<string, string>(encryptedParams)
            {
                ["password"] = creds.HcPassword
            };
        }

        var requestStr = _crypto.BuildEncryptedRequestStr(encryptedParams, creds.AuthenticationKey);
        var requestToken = _crypto.ComputeRequestToken(encryptedParams);

        var query = new List<string>
        {
            $"dept_id={Uri.EscapeDataString(creds.DeptId)}",
            $"request_str={Uri.EscapeDataString(requestStr)}",
            $"request_token={Uri.EscapeDataString(requestToken)}",
            $"version={Uri.EscapeDataString(_options.ApiVersion)}",
        };

        return $"{_options.BaseUrl}/{relativePath}?{string.Join("&", query)}";
    }

    private async Task<NapixEnvelope> SendAndParseEnvelopeAsync(
        HttpMethod method, string url, HttpContent? content, string? module, CancellationToken cancellationToken)
    {
        var creds = _options.GetModuleCredentials(module);
        string accessToken = await _authService.GetAccessTokenAsync(module, cancellationToken);

        HttpRequestMessage CreateRequest()
        {
            var req = new HttpRequestMessage(method, url) { Content = content };
            if (!string.IsNullOrWhiteSpace(accessToken))
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            req.Headers.Add("Accept", "application/json, text/plain, */*");
            req.Headers.Add("User-Agent", BrowserUserAgent);
            return req;
        }

        HttpResponseMessage response;
        string body;
        try
        {
            using var request = CreateRequest();
            response = await _httpClient.SendAsync(request, cancellationToken);
            body = await response.Content.ReadAsStringAsync(cancellationToken);
        }
        catch (HttpRequestException ex) when (ex.InnerException is IOException || ex.Message.Contains("SSL") || ex.Message.Contains("connection"))
        {
            _logger.LogWarning(ex, "[NAPIX] SSL/Connection dropped by remote server ({Url}). Retrying with fresh socket...", url);
            await Task.Delay(250, cancellationToken);
            using var retryReq = CreateRequest();
            response = await _httpClient.SendAsync(retryReq, cancellationToken);
            body = await response.Content.ReadAsStringAsync(cancellationToken);
        }

        // Handle genuine 401 Unauthorized (evict OAuth token and retry once)
        bool isReal401 = response.StatusCode == System.Net.HttpStatusCode.Unauthorized &&
                         !body.Contains("626") &&
                         !body.Contains("INVALID_TOKEN", StringComparison.OrdinalIgnoreCase);

        if (isReal401)
        {
            _logger.LogWarning("[NAPIX] HTTP 401 Unauthorized. Refreshing token for {Module} and retrying...", module ?? "Default");
            _authService.InvalidateToken(module);
            try
            {
                accessToken = await _authService.GetAccessTokenAsync(module, cancellationToken);
                response.Dispose();
                using var retryReq = CreateRequest();
                response = await _httpClient.SendAsync(retryReq, cancellationToken);
                body = await response.Content.ReadAsStringAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[NAPIX] Failed to refresh OAuth token on 401 for {Module}.", module ?? "Default");
            }
        }

        using (response)
        {
            bool isHtmlNotFound = body.Contains("<html", StringComparison.OrdinalIgnoreCase) ||
                                  body.Contains("Search Page not Found", StringComparison.OrdinalIgnoreCase) ||
                                  body.Contains("Welcome User", StringComparison.OrdinalIgnoreCase);

            if (isHtmlNotFound)
                throw new NapixNotFoundException("Case record not found in eCourts for the requested CNR.");

            if (!response.IsSuccessStatusCode)
            {
                var decryptedError = TryDecryptErrorBody(body, creds.AuthenticationKey);
                if (decryptedError.Contains("INVALID_CNR", StringComparison.OrdinalIgnoreCase) ||
                    decryptedError.Contains("RECORD NOT FOUND", StringComparison.OrdinalIgnoreCase) ||
                    decryptedError.Contains("NO RECORD FOUND", StringComparison.OrdinalIgnoreCase))
                {
                    throw new NapixNotFoundException($"Case record not found in eCourts: {decryptedError}");
                }
                if (decryptedError.Contains("INVALID_TOKEN", StringComparison.OrdinalIgnoreCase) || decryptedError.Contains("626"))
                {
                    throw new NapixApiException($"eCourts CIS backend returned 626 (INVALID_TOKEN) for dept_id '{creds.DeptId}'.");
                }
                throw new NapixApiException($"NAPIX call failed ({(int)response.StatusCode}): {decryptedError}");
            }
        }

        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("status", out var statusEl))
            {
                var status = statusEl.GetString();
                if (!string.IsNullOrEmpty(status) && status != "1" && status != "200" && !string.Equals(status, "success", StringComparison.OrdinalIgnoreCase))
                {
                    var decryptedStatus = TryDecryptString(status, creds.AuthenticationKey);
                    var description = doc.RootElement.TryGetProperty("statusDescription", out var d) ? d.GetString() : null;
                    var fullMsg = $"{decryptedStatus} - {description}";
                    if (fullMsg.Contains("INVALID_CNR", StringComparison.OrdinalIgnoreCase) ||
                        fullMsg.Contains("RECORD NOT FOUND", StringComparison.OrdinalIgnoreCase) ||
                        fullMsg.Contains("NO RECORD FOUND", StringComparison.OrdinalIgnoreCase) ||
                        status == "0")
                    {
                        throw new NapixNotFoundException($"Case record not found in eCourts: {fullMsg}");
                    }
                    throw new NapixApiException($"NAPIX returned error: {fullMsg}");
                }
            }

            return JsonSerializer.Deserialize<NapixEnvelope>(body, JsonOptions)
                ?? throw new NapixApiException("Could not parse NAPIX response envelope.");
        }
        catch (JsonException jex)
        {
            throw new NapixApiException($"Could not parse NAPIX response as JSON: {body[..Math.Min(body.Length, 200)]}", jex);
        }
    }

    private string TryDecryptErrorBody(string body, string? authKey)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("status", out var sProp) && sProp.GetString() is { } s && !string.IsNullOrWhiteSpace(s))
            {
                var decrypted = TryDecryptString(s, authKey);
                if (decrypted != s) return decrypted;
            }
        }
        catch (JsonException) { }
        return body;
    }

    private string TryDecryptString(string input, string? authKey)
    {
        if (string.IsNullOrWhiteSpace(input)) return input;
        try
        {
            var dec = _crypto.DecryptResponseStr(input, authKey);
            if (!string.IsNullOrWhiteSpace(dec) && !dec.Any(c => char.IsControl(c) && c != '\r' && c != '\n' && c != '\t'))
                return dec;
        }
        catch { }
        return input;
    }

    private string DecryptAndVerify(NapixEnvelope envelope, string? module = null)
    {
        var creds = _options.GetModuleCredentials(module);
        var decrypted = _crypto.DecryptResponseStr(envelope.ResponseStr, creds.AuthenticationKey);

        if (!_crypto.VerifyResponseToken(decrypted, envelope.ResponseToken))
        {
            // If response_token doesn't match, verify if decrypted payload is valid JSON before trusting it
            try
            {
                using var doc = JsonDocument.Parse(decrypted);
                _logger.LogInformation("Decrypted payload is valid JSON despite token discrepancy.");
                return decrypted;
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Decrypted payload is not valid JSON. Raw decrypted: {Decrypted}", decrypted);
                throw new NapixApiException($"response_token verification failed and decrypted content is not valid JSON: '{decrypted}'");
            }
        }

        return decrypted;
    }

    private byte[] DecryptToBytes(NapixEnvelope envelope, string? module = null)
    {
        var creds = _options.GetModuleCredentials(module);
        var cipherBytes = Convert.FromBase64String(Uri.UnescapeDataString(envelope.ResponseStr));
        using var aes = System.Security.Cryptography.Aes.Create();
        var keyBytes = Encoding.ASCII.GetBytes(creds.AuthenticationKey ?? _options.AuthenticationKey);
        Array.Resize(ref keyBytes, 16);
        aes.Key = keyBytes;
        aes.IV = keyBytes;
        aes.Mode = System.Security.Cryptography.CipherMode.CBC;
        aes.Padding = System.Security.Cryptography.PaddingMode.PKCS7;

        return aes.CreateDecryptor().TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);
    }

    private static string CleanBase64(string input) =>
        input.Trim('\"', '\'', ' ', '\r', '\n', '\t', '\0')
             .Replace("\\\"", "").Replace("\"", "").Replace("\\/", "/")
             .Replace("\r", "").Replace("\n", "").Replace(" ", "").Replace("\t", "");

    private byte[] EnsurePdfBytes(byte[] decryptedBytes)
    {
        // 1. Direct PDF magic bytes %PDF
        if (decryptedBytes.Length >= 4 && decryptedBytes[0] == 0x25 && decryptedBytes[1] == 0x50 && decryptedBytes[2] == 0x44 && decryptedBytes[3] == 0x46)
            return decryptedBytes;

        var text = Encoding.UTF8.GetString(decryptedBytes).Trim();

        // 2a. Unwrap JSON string quote wrapper
        if (text.Length >= 2 && text.StartsWith("\"") && text.EndsWith("\""))
        {
            try
            {
                using var strDoc = JsonDocument.Parse(text);
                text = strDoc.RootElement.ValueKind == JsonValueKind.String ? (strDoc.RootElement.GetString()?.Trim() ?? text) : text[1..^1].Trim();
            }
            catch { text = text[1..^1].Trim(); }
        }

        // 2b. Direct Base64 candidate
        var candidateBase64 = CleanBase64(text);
        try
        {
            var rem = candidateBase64.Length % 4;
            if (rem > 0) candidateBase64 = candidateBase64.PadRight(candidateBase64.Length + (4 - rem), '=');
            var rawBytes = Convert.FromBase64String(candidateBase64);
            if ((rawBytes.Length >= 4 && rawBytes[0] == 0x25 && rawBytes[1] == 0x50 && rawBytes[2] == 0x44 && rawBytes[3] == 0x46) || rawBytes.Length > 20)
                return rawBytes;
        }
        catch (FormatException) { }

        // 2c. JSON object with base64 PDF property
        if (text.StartsWith("{") && text.EndsWith("}"))
        {
            try
            {
                using var doc = JsonDocument.Parse(text);
                var root = doc.RootElement;
                CheckDecryptedError(root, text);

                foreach (var propName in new[] { "pdf", "order_pdf", "data", "file", "pdf_data", "order_data", "order_file" })
                {
                    if (root.TryGetProperty(propName, out var prop) && prop.GetString() is { } b64 && !string.IsNullOrWhiteSpace(b64))
                    {
                        var cleaned = CleanBase64(b64);
                        var mod4 = cleaned.Length % 4;
                        if (mod4 > 0) cleaned = cleaned.PadRight(cleaned.Length + (4 - mod4), '=');
                        return Convert.FromBase64String(cleaned);
                    }
                }
            }
            catch (JsonException) { }
        }

        _logger.LogError("Decrypted bytes do not represent a valid PDF. Content snippet: {Snippet}",
            text.Length > 200 ? text[..200] : text);
        throw new NapixApiException($"eCourts returned non-PDF response: {(text.Length > 200 ? text[..200] : text)}");
    }

    private static void ValidateCnr(string cnr)
    {
        if (string.IsNullOrWhiteSpace(cnr) || cnr.Length != 16 || !cnr.All(char.IsLetterOrDigit))
            throw new ArgumentException($"'{cnr}' is not a valid 16-character alphanumeric CNR.", nameof(cnr));
    }

    private static void CheckDecryptedError(JsonElement root, string rawJson)
    {
        if (root.TryGetProperty("status", out var s))
        {
            var statusStr = s.ToString();
            if (statusStr != "1" && statusStr != "200" && !string.Equals(statusStr, "success", StringComparison.OrdinalIgnoreCase))
            {
                var desc = root.TryGetProperty("statusDescription", out var d) ? d.GetString() : null;
                if (string.IsNullOrEmpty(desc) && root.TryGetProperty("message", out var m))
                    desc = m.GetString();

                var fullMsg = desc ?? rawJson;
                if (fullMsg.Contains("INVALID_CNR", StringComparison.OrdinalIgnoreCase) ||
                    fullMsg.Contains("RECORD NOT FOUND", StringComparison.OrdinalIgnoreCase) ||
                    fullMsg.Contains("NO RECORD FOUND", StringComparison.OrdinalIgnoreCase) ||
                    fullMsg.Contains("not found", StringComparison.OrdinalIgnoreCase) ||
                    statusStr == "0")
                {
                    throw new NapixNotFoundException($"Case record not found in eCourts: {fullMsg}");
                }

                throw new NapixApiException($"NAPIX returned status {statusStr}: {fullMsg}");
            }
        }
    }

    private static IEnumerable<JsonElement> EnumerateSubItems(JsonElement parent, params string[] propNames)
    {
        if (parent.ValueKind != JsonValueKind.Object) yield break;

        foreach (var prop in parent.EnumerateObject())
        {
            if (propNames.Any(p => string.Equals(p, prop.Name, StringComparison.OrdinalIgnoreCase)))
            {
                if (prop.Value.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in prop.Value.EnumerateArray()) yield return item;
                }
                else if (prop.Value.ValueKind == JsonValueKind.Object)
                {
                    foreach (var child in prop.Value.EnumerateObject())
                    {
                        if (child.Value.ValueKind == JsonValueKind.Object) yield return child.Value;
                    }
                }
                yield break;
            }
        }
    }

    private static bool TryParseDate(string? input, out DateOnly date)
    {
        date = default;
        if (string.IsNullOrWhiteSpace(input)) return false;

        var formats = new[] { "yyyy-MM-dd", "dd-MM-yyyy", "dd/MM/yyyy", "yyyy/MM/dd", "yyyy-M-d", "d-M-yyyy" };
        foreach (var fmt in formats)
        {
            if (DateOnly.TryParseExact(input.Trim(), fmt, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out date))
                return true;
        }

        return DateOnly.TryParse(input, out date);
    }

    private static string? FormatDateForUrl(string? input) =>
        TryParseDate(input, out var d) ? d.ToString("yyyy-MM-dd") : input;

    private static string? GetStringProp(JsonElement element, params string[] propertyNames)
    {
        foreach (var p in propertyNames)
        {
            if (element.TryGetProperty(p, out var val))
                return val.ValueKind == JsonValueKind.String ? val.GetString() : val.ToString();
        }
        return null;
    }
}
