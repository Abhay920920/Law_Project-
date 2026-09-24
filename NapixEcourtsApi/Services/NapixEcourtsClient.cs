using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Options;
using NapixEcourtsApi.Configuration;
using NapixEcourtsApi.Models;

namespace NapixEcourtsApi.Services;

public interface INapixEcourtsClient
{
    /// <summary>Full case history via dc-cnr-api/cnr (GET) mapped to standard CnrCaseDetails.</summary>
    Task<CnrCaseDetails> GetCaseByCnrAsync(string cnr, CancellationToken cancellationToken = default);

    /// <summary>
    /// Comprehensive case details: basic info, all parsed hearing dates, all interim/final orders
    /// with direct PDF download URLs, and the unadulterated raw response.
    /// </summary>
    Task<CaseFullDetails> GetFullCaseDetailsAsync(string cnr, CancellationToken cancellationToken = default);

    /// <summary>Raw decrypted JSON document directly from eCourts.</summary>
    Task<JsonElement> GetRawCaseDetailsAsync(string cnr, CancellationToken cancellationToken = default);

    /// <summary>Downloads all interim and final orders/judgments for a CNR bundled into a ZIP archive.</summary>
    Task<(byte[] ZipBytes, int OrderCount)> DownloadAllOrdersZipAsync(string cnr, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lightweight current-status via dc-current-status-api/currentStatus (POST).
    /// Accepts up to 500 CNRs per call per NAPIX docs.
    /// </summary>
    Task<JsonElement> GetCurrentStatusAsync(IReadOnlyList<string> cnrs, CancellationToken cancellationToken = default);

    /// <summary>Business transacted on a given date via dc-show-business-api/showBusiness (GET).</summary>
    Task<JsonElement> GetShowBusinessAsync(string cnr, DateOnly businessDate, CancellationToken cancellationToken = default);

    /// <summary>Order/judgment PDF bytes via dc-oreder-api/order (GET) — response is a PDF, not JSON.</summary>
    Task<byte[]> GetOrderPdfAsync(string cnr, string orderNo, DateOnly orderDate, CancellationToken cancellationToken = default);
}

public class NapixEcourtsClient : INapixEcourtsClient
{
    private readonly HttpClient _httpClient;
    private readonly INapixAuthService _authService;
    private readonly INapixCryptoService _crypto;
    private readonly NapixOptions _options;
    private readonly ILogger<NapixEcourtsClient> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new StringOrNumberConverter() }
    };

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
    /// Checks if a 16-character CNR corresponds to a High Court.
    /// In the Indian eCourts scheme, characters 3 and 4 are "HC" for all High Courts (e.g., KAHC..., DLHC..., MCHC...).
    /// </summary>
    public static bool IsHighCourtCnr(string cnr)
    {
        if (string.IsNullOrWhiteSpace(cnr) || cnr.Length < 4)
            return false;
        return cnr.Substring(2, 2).Equals("HC", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<JsonElement> GetRawCaseDetailsAsync(string cnr, CancellationToken cancellationToken = default)
    {
        ValidateCnr(cnr);

        var isHc = IsHighCourtCnr(cnr);
        Exception? lastException = null;

        // ── NAPIX endpoint + parameter strategy ──────────────────────────────────────────
        // Subscribed products (confirmed on NAPIX portal at dev.napix.gov.in):
        //   • District Court Case Status API  → dc-current-status-api/currentStatus
        //   • High Court Case Status API      → hc-current-status-api/currentStatus
        //
        // Key findings from gateway responses:
        //   • currentStatus endpoints: require "cnr_list" parameter, POST accepted (400 → valid params missing = right endpoint)
        //   • CNR endpoints: use "cino" parameter, GET with query-string
        //   • HC endpoint returns 405/HTML for GET → accept POST with form body
        //   • 404 "No resources match" = endpoint path wrong or not subscribed
        //   • 400 "required parameters missing" = endpoint EXISTS, just wrong param name
        //
        // Order: try the SUBSCRIBED endpoints first with correct params before fallbacks.
        // ─────────────────────────────────────────────────────────────────────────────────

        // ─────────────────────────────────────────────────────────────────────────
        // NAPIX currentStatus POST body structure (discovered from live testing):
        //   dept_id     = clo@ksrtc.org              (plain)
        //   request_str = AES(empty payload)          (encrypted envelope, no CNR inside)
        //   request_token = HMAC(empty payload)       (signature of empty string)
        //   version     = v1.0                        (plain)
        //   cnr_list    = KABK020000652026            (plain, separate from request_str!)
        //
        // cnr_list is a plain top-level form field — NOT inside the encrypted request_str.
        // ─────────────────────────────────────────────────────────────────────────

        // Strategy pairs: (endpoint, method). cnr_list is always plain in form body.
        var endpointStrategies = isHc
            ? new (string Endpoint, HttpMethod Method)[]
            {
                ("hc-current-status-api/currentStatus", HttpMethod.Post),
                ("dc-current-status-api/currentStatus", HttpMethod.Post),  // fallback
                ("hc-cnr-api/CNR",                      HttpMethod.Get),
            }
            : new (string Endpoint, HttpMethod Method)[]
            {
                ("dc-current-status-api/currentStatus", HttpMethod.Post),
                ("hc-current-status-api/currentStatus", HttpMethod.Post),  // fallback
                ("dc-cnr-api/cnr",                      HttpMethod.Get),
            };

        foreach (var (endpoint, method) in endpointStrategies)
        {
            try
            {
                HttpContent? formContent = null;
                string url;

                bool isCurrentStatus = endpoint.Contains("current-status-api", StringComparison.OrdinalIgnoreCase);
                bool isCnrApi = endpoint.Contains("-cnr-api", StringComparison.OrdinalIgnoreCase);

                if (method == HttpMethod.Post && isCurrentStatus)
                {
                    // currentStatus POST: envelope params in query string (spec: in query)
                    url = BuildPostUrl(endpoint, new Dictionary<string, string> { ["cino"] = cnr });
                    formContent = null;
                }
                else if (method == HttpMethod.Get && isCnrApi)
                {
                    // CNR API: cino={CNR} goes inside the encrypted request_str (query string GET)
                    url = BuildGetUrl(endpoint, new Dictionary<string, string> { ["cino"] = cnr });
                }
                else
                {
                    url = BuildGetUrl(endpoint, new Dictionary<string, string> { ["cino"] = cnr });
                }

                var envelope = await SendAndParseEnvelopeAsync(method, url, formContent, cancellationToken);
                var decryptedJson = DecryptAndVerify(envelope);

                using var doc = JsonDocument.Parse(decryptedJson);
                CheckDecryptedError(doc.RootElement, decryptedJson);

                _logger.LogInformation("[NAPIX] ✅ CNR {Cnr} found via {Method} {Endpoint}", cnr, method.Method, endpoint);
                return doc.RootElement.Clone();
            }
            catch (NapixNotFoundException ex)
            {
                lastException = ex;
                _logger.LogInformation("[NAPIX] Not found on {Method} {Endpoint}: {Msg}", method.Method, endpoint, ex.Message);
            }
            catch (NapixApiException ex)
            {
                lastException = ex;
                _logger.LogInformation("[NAPIX] Failed {Method} {Endpoint}: {Msg}", method.Method, endpoint, ex.Message);
            }
        }

        throw lastException ?? new NapixNotFoundException($"Case details not found for CNR '{cnr}' from eCourts.");
    }

    public async Task<CnrCaseDetails> GetCaseByCnrAsync(string cnr, CancellationToken cancellationToken = default)
    {
        var rawElement = await GetRawCaseDetailsAsync(cnr, cancellationToken);
        var caseDetails = JsonSerializer.Deserialize<CnrCaseDetails>(rawElement.GetRawText(), JsonOptions)
            ?? throw new NapixApiException("Failed to deserialize CNR response.");
        return caseDetails;
    }

    public async Task<CaseFullDetails> GetFullCaseDetailsAsync(string cnr, CancellationToken cancellationToken = default)
    {
        var rawElement = await GetRawCaseDetailsAsync(cnr, cancellationToken);
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
            var hDate = GetStringProp(item, "hearing_date");
            if (string.IsNullOrWhiteSpace(hDate))
                hDate = GetStringProp(item, "business_date", "dt_next_list", "date");

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
                ? $"/api/cases/{cnr}/order?orderNo={Uri.EscapeDataString(orderNo)}&date={normalizedDate}"
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
                ? $"/api/cases/{cnr}/order?orderNo={Uri.EscapeDataString(orderNo)}&date={normalizedDate}"
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

        // Parse IA Filings, Acts, Transfers, Objections, Processes, Extra Parties, Linked Cases
        foreach (var item in EnumerateSubItems(rawElement, "iafiling", "ia_filing", "ia_details"))
            result.IaFilings.Add(item.Clone());

        foreach (var item in EnumerateSubItems(rawElement, "acts", "act_details"))
            result.Acts.Add(item.Clone());

        foreach (var item in EnumerateSubItems(rawElement, "transfer", "case_transfer"))
            result.Transfers.Add(item.Clone());

        foreach (var item in EnumerateSubItems(rawElement, "objections", "case_objections"))
            result.Objections.Add(item.Clone());

        foreach (var item in EnumerateSubItems(rawElement, "processes", "case_processes"))
            result.Processes.Add(item.Clone());

        foreach (var item in EnumerateSubItems(rawElement, "pet_extra_party", "petitioner_extra_party"))
            result.ExtraParties.Add(item.Clone());

        foreach (var item in EnumerateSubItems(rawElement, "res_extra_party", "respondent_extra_party"))
            result.ExtraParties.Add(item.Clone());

        foreach (var item in EnumerateSubItems(rawElement, "link_cases", "linked_cases"))
            result.LinkedCases.Add(item.Clone());

        return result;
    }

    public async Task<(byte[] ZipBytes, int OrderCount)> DownloadAllOrdersZipAsync(string cnr, CancellationToken cancellationToken = default)
    {
        var fullDetails = await GetFullCaseDetailsAsync(cnr, cancellationToken);
        var allOrders = fullDetails.InterimOrders.Concat(fullDetails.FinalOrdersAndJudgments).ToList();

        using var memoryStream = new MemoryStream();
        using (var archive = new System.IO.Compression.ZipArchive(memoryStream, System.IO.Compression.ZipArchiveMode.Create, leaveOpen: true))
        {
            int index = 1;
            foreach (var order in allOrders)
            {
                if (string.IsNullOrEmpty(order.OrderNumber) || string.IsNullOrEmpty(order.OrderDate))
                    continue;

                if (!TryParseDate(order.OrderDate, out var dateOnly))
                    continue;

                try
                {
                    var pdfBytes = await GetOrderPdfAsync(cnr, order.OrderNumber, dateOnly, cancellationToken);
                    var entryName = $"{index:D2}_{order.OrderType.Replace("/", "_")}_{order.OrderNumber}_{dateOnly:yyyyMMdd}.pdf";
                    var entry = archive.CreateEntry(entryName, System.IO.Compression.CompressionLevel.Optimal);
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

    public async Task<JsonElement> GetCurrentStatusAsync(IReadOnlyList<string> cnrs, CancellationToken cancellationToken = default)
    {
        if (cnrs is null || cnrs.Count == 0)
            throw new ArgumentException("At least one CNR is required.", nameof(cnrs));
        if (cnrs.Count > 500)
            throw new ArgumentException("NAPIX allows a maximum of 500 CNRs per currentStatus call.", nameof(cnrs));

        foreach (var c in cnrs) ValidateCnr(c);

        var hasHc = cnrs.Any(IsHighCourtCnr);
        var endpoints = hasHc
            ? new[] { "hc-current-status-api/currentStatus", "dc-current-status-api/currentStatus" }
            : new[] { "dc-current-status-api/currentStatus", "hc-current-status-api/currentStatus" };

        var parameters = new Dictionary<string, string> { ["cino"] = string.Join(",", cnrs) };
        var requestStr = _crypto.BuildEncryptedRequestStr(parameters);
        var requestToken = _crypto.ComputeRequestToken(parameters);

        NapixApiException? lastEx = null;
        foreach (var endpoint in endpoints)
        {
            try
            {
                var url = BuildPostUrl(endpoint, parameters);
                var envelope = await SendAndParseEnvelopeAsync(HttpMethod.Post, url, null, cancellationToken);
                var decryptedJson = DecryptAndVerify(envelope);

                using var doc = JsonDocument.Parse(decryptedJson);
                return doc.RootElement.Clone();
            }
            catch (NapixApiException ex)
            {
                lastEx = ex;
                _logger.LogInformation("Current status via '{Endpoint}' failed: {Msg}", endpoint, ex.Message);
            }
        }

        throw lastEx ?? new NapixApiException("Failed to retrieve current status from NAPIX.");
    }

    public async Task<JsonElement> GetShowBusinessAsync(string cnr, DateOnly businessDate, CancellationToken cancellationToken = default)
    {
        ValidateCnr(cnr);

        var isHc = IsHighCourtCnr(cnr);
        var endpoints = isHc
            ? new[] { "hc-show-business-api/showBusiness", "dc-show-business-api/showBusiness" }
            : new[] { "dc-show-business-api/showBusiness", "hc-show-business-api/showBusiness" };

        var parameters = new Dictionary<string, string>
        {
            ["cino"] = cnr,
            ["business_date"] = businessDate.ToString("yyyy-MM-dd"),
        };

        NapixApiException? lastEx = null;
        foreach (var endpoint in endpoints)
        {
            try
            {
                var url = BuildGetUrl(endpoint, parameters);
                var envelope = await SendAndParseEnvelopeAsync(HttpMethod.Get, url, content: null, cancellationToken);
                var decryptedJson = DecryptAndVerify(envelope);

                using var doc = JsonDocument.Parse(decryptedJson);
                return doc.RootElement.Clone();
            }
            catch (NapixApiException ex)
            {
                lastEx = ex;
                _logger.LogInformation("Show business via '{Endpoint}' failed: {Msg}", endpoint, ex.Message);
            }
        }

        throw lastEx ?? new NapixApiException($"Failed to fetch show business for CNR '{cnr}'.");
    }

    public async Task<byte[]> GetOrderPdfAsync(string cnr, string orderNo, DateOnly orderDate, CancellationToken cancellationToken = default)
    {
        ValidateCnr(cnr);

        var isHc = IsHighCourtCnr(cnr);
        var endpoints = isHc
            ? new[] { "hc-oreder-api/order", "hc-order-api/order", "dc-oreder-api/order", "dc-order-api/order", "ecourt-icjs-show-order-api/showOrder" }
            : new[] { "dc-oreder-api/order", "dc-order-api/order", "ecourt-icjs-show-order-api/showOrder", "hc-oreder-api/order", "hc-order-api/order" };

        var dateFormats = new[] { "yyyy-MM-dd", "dd-MM-yyyy" };
        NapixApiException? lastEx = null;

        foreach (var endpoint in endpoints)
        {
            foreach (var fmt in dateFormats)
            {
                var formattedDate = orderDate.ToString(fmt);
                try
                {
                    var parameters = new Dictionary<string, string>
                    {
                        ["cino"] = cnr,
                        ["order_no"] = orderNo,
                        ["order_date"] = formattedDate,
                    };
                    var url = BuildGetUrl(endpoint, parameters);
                    var envelope = await SendAndParseEnvelopeAsync(HttpMethod.Get, url, content: null, cancellationToken);
                    var decryptedBytes = DecryptToBytes(envelope);
                    return EnsurePdfBytes(decryptedBytes);
                }
                catch (NapixApiException ex)
                {
                    lastEx = ex;
                    _logger.LogInformation("Order lookup via '{Endpoint}' with '{Fmt}' failed: {Msg}", endpoint, fmt, ex.Message);
                }

                // Also try alternate parameter ordering (cino, order_date, order_no)
                try
                {
                    var altParams = new Dictionary<string, string>
                    {
                        ["cino"] = cnr,
                        ["order_date"] = formattedDate,
                        ["order_no"] = orderNo,
                    };
                    var url = BuildGetUrl(endpoint, altParams);
                    var envelope = await SendAndParseEnvelopeAsync(HttpMethod.Get, url, content: null, cancellationToken);
                    var decryptedBytes = DecryptToBytes(envelope);
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

    private string BuildGetUrl(string relativePath, IDictionary<string, string> encryptedParams)
    {
        var requestStr = _crypto.BuildEncryptedRequestStr(encryptedParams);
        var requestToken = _crypto.ComputeRequestToken(encryptedParams);

        var query = new List<string>
        {
            $"dept_id={Uri.EscapeDataString(_options.DeptId)}",
            $"request_str={Uri.EscapeDataString(requestStr)}",
            $"request_token={Uri.EscapeDataString(requestToken)}",
            $"version={Uri.EscapeDataString(_options.ApiVersion)}",
        };

        return $"{_options.BaseUrl}/{relativePath}?{string.Join("&", query)}";
    }

    /// <summary>
    /// Builds the base URL for POST requests — NAPIX expects all envelope params in the
    /// query string even for POST (spec: dept_id/request_str/request_token/version all
    /// "in: query"). Equivalent to BuildGetUrl but kept separate for readability.
    /// </summary>
    private string BuildPostUrl(string relativePath, IDictionary<string, string> parameters)
        => BuildGetUrl(relativePath, parameters);

    /// <summary>
    /// Builds a form-encoded HttpContent with dept_id, encrypted request_str, request_token, and version.
    /// Used for POST-based NAPIX endpoints (e.g. HC CNR API which rejects GET).
    /// </summary>
    private FormUrlEncodedContent BuildFormContent(IDictionary<string, string> parameters)
    {
        var requestStr = _crypto.BuildEncryptedRequestStr(parameters);
        var requestToken = _crypto.ComputeRequestToken(parameters);

        return new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["dept_id"] = _options.DeptId,
            ["request_str"] = requestStr,
            ["request_token"] = requestToken,
            ["version"] = _options.ApiVersion,
        });
    }

    /// <summary>
    /// Builds form content for the currentStatus POST endpoint.
    /// NAPIX currentStatus uses a two-level param structure:
    ///   • The standard NAPIX envelope (dept_id / request_str / request_token / version)
    ///     is built from an EMPTY parameter dict — it authenticates the dept, nothing else.
    ///   • cnr_list is passed as a separate PLAIN (unencrypted) form field alongside the envelope.
    /// This was determined empirically: encrypting the CNR inside request_str always returns
    /// 400 "required parameters missing", while the plain cnr_list field is what the gateway reads.
    /// </summary>
    private FormUrlEncodedContent BuildCurrentStatusFormContent(string cnr)
    {
        // Build envelope from empty params (just authenticates the request origin)
        var emptyParams = new Dictionary<string, string>();
        var requestStr = _crypto.BuildEncryptedRequestStr(emptyParams);
        var requestToken = _crypto.ComputeRequestToken(emptyParams);

        return new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["dept_id"]      = _options.DeptId,
            ["request_str"]  = requestStr,
            ["request_token"]= requestToken,
            ["version"]      = _options.ApiVersion,
            ["cnr_list"]     = cnr,   // plain, unencrypted — gateway reads this directly
        });
    }

    private async Task<NapixEnvelope> SendAndParseEnvelopeAsync(
        HttpMethod method, string url, HttpContent? content, CancellationToken cancellationToken)
    {
        var accessToken = await _authService.GetAccessTokenAsync(cancellationToken);

        using var request = new HttpRequestMessage(method, url) { Content = content };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.Add("X-IBM-Client-Id", _options.ClientId);
        if (!string.IsNullOrWhiteSpace(_options.ClientSecret))
        {
            request.Headers.Add("X-IBM-Client-Secret", _options.ClientSecret);
        }
        request.Headers.Add("Accept", "application/json, text/plain, */*");
        request.Headers.Add("User-Agent", "Mozilla/5.0 (compatible; eCourts-Client/1.0)");

        HttpResponseMessage response;
        string body;
        try
        {
            response = await _httpClient.SendAsync(request, cancellationToken);
            body = await response.Content.ReadAsStringAsync(cancellationToken);
        }
        catch (HttpRequestException ex) when (ex.InnerException is System.IO.IOException || ex.Message.Contains("SSL") || ex.Message.Contains("connection"))
        {
            _logger.LogWarning(ex, "[NAPIX] SSL/Connection dropped by remote server ({Url}). Retrying with fresh socket...", url);
            await Task.Delay(250, cancellationToken);
            using var retryReq = new HttpRequestMessage(method, url) { Content = content };
            retryReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            retryReq.Headers.Add("X-IBM-Client-Id", _options.ClientId);
            if (!string.IsNullOrWhiteSpace(_options.ClientSecret))
            {
                retryReq.Headers.Add("X-IBM-Client-Secret", _options.ClientSecret);
            }
            retryReq.Headers.Add("Accept", "application/json, text/plain, */*");
            retryReq.Headers.Add("User-Agent", "Mozilla/5.0 (compatible; eCourts-Client/1.0)");

            response = await _httpClient.SendAsync(retryReq, cancellationToken);
            body = await response.Content.ReadAsStringAsync(cancellationToken);
        }

        _logger.LogDebug("[NAPIX] {Method} {Url} → HTTP {Status}. Body snippet: {Snippet}",
            method.Method, url, (int)response.StatusCode,
            body.Length > 300 ? body[..300] : body);

        bool isHtmlNotFoundPage = body.Contains("<html", StringComparison.OrdinalIgnoreCase) || 
                                  body.Contains("Search Page not Found", StringComparison.OrdinalIgnoreCase) ||
                                  body.Contains("Welcome User", StringComparison.OrdinalIgnoreCase);

        bool isEncryptedNotFound = body.Contains("INVALID_CNR", StringComparison.OrdinalIgnoreCase) ||
                                   body.Contains("RECORD NOT FOUND", StringComparison.OrdinalIgnoreCase) ||
                                   body.Contains("NO RECORD FOUND", StringComparison.OrdinalIgnoreCase);

        // If we received HTTP 405 with an HTML "Search Page not Found" page, the gateway is rejecting
        // our GET request. This happens for HC endpoints that require POST with form body.
        // Propagate this as a NapixApiException so the caller's POST variant gets a chance.
        if ((int)response.StatusCode == 405)
        {
            if (isHtmlNotFoundPage)
            {
                // Log the body to help diagnose endpoint/method issues
                _logger.LogInformation(
                    "[NAPIX] {Method} {Url} → 405 with HTML page. Caller should retry with POST form body.",
                    method.Method, url);
                throw new NapixApiException($"NAPIX {method.Method} returned 405 (HTML not-found page — try POST form body).");
            }

            // Real 405 with JSON "Method Not Allowed" — propagate so the caller retries with opposite method
            throw new NapixApiException($"NAPIX call returned 405: {body[..Math.Min(body.Length, 200)]}");
        }

        // Handle expired token by invalidating and retrying once
        string previewError = (!response.IsSuccessStatusCode && !isHtmlNotFoundPage) ? TryDecryptErrorBody(body) : "";
        bool isTokenExpired = response.StatusCode == System.Net.HttpStatusCode.Unauthorized || 
                              body.Contains("INVALID_TOKEN", StringComparison.OrdinalIgnoreCase) ||
                              previewError.Contains("INVALID_TOKEN", StringComparison.OrdinalIgnoreCase);

        if (!response.IsSuccessStatusCode && isTokenExpired)
        {
            _logger.LogWarning("[NAPIX] {Status} (INVALID_TOKEN detected) — token expired. Invalidating and retrying once.", response.StatusCode);
            _authService.InvalidateToken();
            var freshToken = await _authService.GetAccessTokenAsync(cancellationToken);

            using var retryReq = new HttpRequestMessage(method, url) { Content = content };
            retryReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", freshToken);
            retryReq.Headers.Add("X-IBM-Client-Id", _options.ClientId);
            if (!string.IsNullOrWhiteSpace(_options.ClientSecret))
            {
                retryReq.Headers.Add("X-IBM-Client-Secret", _options.ClientSecret);
            }
            retryReq.Headers.Add("Accept", "application/json, text/plain, */*");
            retryReq.Headers.Add("User-Agent", "Mozilla/5.0 (compatible; eCourts-Client/1.0)");

            response.Dispose();
            response = await _httpClient.SendAsync(retryReq, cancellationToken);
            body = await response.Content.ReadAsStringAsync(cancellationToken);

            isHtmlNotFoundPage = body.Contains("<html", StringComparison.OrdinalIgnoreCase) || 
                                 body.Contains("Search Page not Found", StringComparison.OrdinalIgnoreCase) ||
                                 body.Contains("Welcome User", StringComparison.OrdinalIgnoreCase);
            isEncryptedNotFound = body.Contains("INVALID_CNR", StringComparison.OrdinalIgnoreCase) ||
                                  body.Contains("RECORD NOT FOUND", StringComparison.OrdinalIgnoreCase) ||
                                  body.Contains("NO RECORD FOUND", StringComparison.OrdinalIgnoreCase);
        }

        using (response)
        {
            if (isHtmlNotFoundPage)
            {
                throw new NapixNotFoundException("Case record not found in eCourts for the requested CNR.");
            }

            if (isEncryptedNotFound)
            {
                throw new NapixNotFoundException($"Case record not found in eCourts (plaintext body): {body[..Math.Min(body.Length, 200)]}");
            }

            if (!response.IsSuccessStatusCode)
            {
                var decryptedError = TryDecryptErrorBody(body);
                if (decryptedError.Contains("INVALID_CNR", StringComparison.OrdinalIgnoreCase) ||
                    decryptedError.Contains("RECORD NOT FOUND", StringComparison.OrdinalIgnoreCase) ||
                    decryptedError.Contains("NO RECORD FOUND", StringComparison.OrdinalIgnoreCase) ||
                    decryptedError.Contains("Search Page not Found", StringComparison.OrdinalIgnoreCase))
                {
                    throw new NapixNotFoundException($"Case record not found in eCourts: {decryptedError}");
                }
                throw new NapixApiException($"NAPIX call failed ({(int)response.StatusCode}): {decryptedError}");
            }
        }

        if (body.TrimStart().StartsWith("Array", StringComparison.OrdinalIgnoreCase) || 
            (body.Contains("INVALID_TOKEN", StringComparison.OrdinalIgnoreCase) && !body.TrimStart().StartsWith("{")))
        {
            if (body.Contains("INVALID_TOKEN", StringComparison.OrdinalIgnoreCase) || body.Contains("626"))
            {
                throw new NapixApiException("NAPIX returned error: {\"status_code\":\"626\",\"status\":\"INVALID_TOKEN\"}");
            }
            throw new NapixApiException($"NAPIX returned non-JSON response: {body[..Math.Min(body.Length, 200)]}");
        }

        NapixEnvelope? envelope = null;
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("status", out var statusEl))
            {
                var status = statusEl.GetString();
                if (!string.IsNullOrEmpty(status) && status != "1" && status != "200" && !string.Equals(status, "success", StringComparison.OrdinalIgnoreCase))
                {
                    var decryptedStatus = TryDecryptString(status);
                    var description = doc.RootElement.TryGetProperty("statusDescription", out var d)
                        ? d.GetString() : null;
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

            envelope = JsonSerializer.Deserialize<NapixEnvelope>(body, JsonOptions);
        }
        catch (JsonException jex)
        {
            _logger.LogWarning(jex, "[NAPIX] Failed to parse JSON response: {Body}", body[..Math.Min(body.Length, 300)]);
            throw new NapixApiException($"Could not parse NAPIX response as JSON: {body[..Math.Min(body.Length, 200)]}");
        }

        return envelope ?? throw new NapixApiException("Could not parse NAPIX response envelope.");
    }

    private string TryDecryptErrorBody(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("status", out var sProp) && sProp.GetString() is { } s && !string.IsNullOrWhiteSpace(s))
            {
                var decrypted = TryDecryptString(s);
                if (decrypted != s)
                    return decrypted;
            }
        }
        catch (JsonException) { }

        return body;
    }

    private string TryDecryptString(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return input;

        // 1. Try decrypting with configured AuthKey
        try
        {
            var dec = _crypto.DecryptResponseStr(input);
            if (!string.IsNullOrWhiteSpace(dec) && !dec.Any(c => char.IsControl(c) && c != '\r' && c != '\n' && c != '\t'))
                return dec;
        }
        catch { }

        // 2. Try decrypting with 16 zero bytes (used by eCourts for system error payloads like INVALID_CNR)
        try
        {
            var zeroBytes = new byte[16];
            using var aes = System.Security.Cryptography.Aes.Create();
            aes.Key = zeroBytes;
            aes.IV = zeroBytes;
            aes.Mode = System.Security.Cryptography.CipherMode.CBC;
            aes.Padding = System.Security.Cryptography.PaddingMode.PKCS7;

            var clean = input.Trim().Replace("\\/", "/").Replace("-", "+").Replace("_", "/");
            var mod4 = clean.Length % 4;
            if (mod4 > 0) clean += new string('=', 4 - mod4);

            var cipherBytes = Convert.FromBase64String(clean);
            using var decryptor = aes.CreateDecryptor();
            var plainBytes = decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);
            var dec = System.Text.Encoding.UTF8.GetString(plainBytes);
            if (!string.IsNullOrWhiteSpace(dec))
                return dec;
        }
        catch { }

        return input;
    }

    private string DecryptAndVerify(NapixEnvelope envelope)
    {
        var decrypted = _crypto.DecryptResponseStr(envelope.ResponseStr);

        if (!_crypto.VerifyResponseToken(decrypted, envelope.ResponseToken))
        {
            var expectedOnDecrypted = _crypto.ComputeHmacHex(decrypted);
            var expectedOnCipher = _crypto.ComputeHmacHex(envelope.ResponseStr);
            var expectedOnTrimmed = _crypto.ComputeHmacHex(decrypted.Trim());

            _logger.LogWarning(
                "NAPIX response_token mismatch. Received: '{Received}', ExpectedOnDecrypted: '{ExpD}', ExpectedOnCipher: '{ExpC}', ExpectedOnTrimmed: '{ExpT}'. Decrypted length: {Len}",
                envelope.ResponseToken, expectedOnDecrypted, expectedOnCipher, expectedOnTrimmed, decrypted.Length);

            // Check if decrypted payload is valid JSON
            try
            {
                using var doc = JsonDocument.Parse(decrypted);
                _logger.LogInformation("Decrypted payload is valid JSON. Continuing despite token discrepancy: {Payload}", decrypted);
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

    private byte[] DecryptToBytes(NapixEnvelope envelope)
    {
        // Order API's response_str decrypts to raw PDF bytes rather than UTF-8 JSON text,
        // so this bypasses the DecryptResponseStr(string) UTF8 path and decrypts to bytes directly.
        var cipherBytes = Convert.FromBase64String(Uri.UnescapeDataString(envelope.ResponseStr));
        using var aes = System.Security.Cryptography.Aes.Create();
        var keyBytes = System.Text.Encoding.ASCII.GetBytes(_options.AuthenticationKey);
        Array.Resize(ref keyBytes, 16);
        aes.Key = keyBytes;
        aes.IV = keyBytes;
        aes.Mode = System.Security.Cryptography.CipherMode.CBC;
        aes.Padding = System.Security.Cryptography.PaddingMode.PKCS7;

        using var decryptor = aes.CreateDecryptor();
        return decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);
    }

    private byte[] EnsurePdfBytes(byte[] decryptedBytes)
    {
        // 1. If it already starts with '%PDF' (0x25, 0x50, 0x44, 0x46), it is a valid raw PDF!
        if (decryptedBytes.Length >= 4 &&
            decryptedBytes[0] == 0x25 &&
            decryptedBytes[1] == 0x50 &&
            decryptedBytes[2] == 0x44 &&
            decryptedBytes[3] == 0x46)
        {
            return decryptedBytes;
        }

        // 2. Otherwise, treat it as text (could be Base64, JSON string, JSON object, or error)
        var text = System.Text.Encoding.UTF8.GetString(decryptedBytes).Trim();

        // 2a. If enclosed in JSON double-quotes ("..."), unwrap it to get the raw string content
        if (text.Length >= 2 && text.StartsWith("\"") && text.EndsWith("\""))
        {
            try
            {
                using var strDoc = JsonDocument.Parse(text);
                if (strDoc.RootElement.ValueKind == JsonValueKind.String)
                {
                    text = strDoc.RootElement.GetString()?.Trim() ?? text;
                }
                else
                {
                    text = text.Substring(1, text.Length - 2).Trim();
                }
            }
            catch
            {
                text = text.Substring(1, text.Length - 2).Trim();
            }
        }

        // 2b. Attempt to clean and parse Base64 directly
        // Base64 from eCourts may contain quotes, \r, \n, escaped quotes, or whitespace
        var candidateBase64 = text.Trim('\"', '\'', ' ', '\r', '\n', '\t', '\0')
                                  .Replace("\\\"", "")
                                  .Replace("\"", "")
                                  .Replace("\\/", "/")
                                  .Replace("\\r", "")
                                  .Replace("\\n", "")
                                  .Replace("\r", "")
                                  .Replace("\n", "")
                                  .Replace(" ", "")
                                  .Replace("\t", "");

        try
        {
            var rem = candidateBase64.Length % 4;
            if (rem > 0)
            {
                candidateBase64 = candidateBase64.PadRight(candidateBase64.Length + (4 - rem), '=');
            }

            var rawBytes = Convert.FromBase64String(candidateBase64);
            if (rawBytes.Length >= 4 &&
                rawBytes[0] == 0x25 &&
                rawBytes[1] == 0x50 &&
                rawBytes[2] == 0x44 &&
                rawBytes[3] == 0x46)
            {
                return rawBytes;
            }
            if (rawBytes.Length > 20)
            {
                return rawBytes;
            }
        }
        catch (FormatException)
        {
            // Not raw base64, continue to JSON object check
        }

        // 2c. Check if it's a JSON object containing properties
        if (text.StartsWith("{") && text.EndsWith("}"))
        {
            try
            {
                using var doc = JsonDocument.Parse(text);
                var root = doc.RootElement;

                // Check for error in JSON
                if (root.TryGetProperty("status", out var s))
                {
                    var statusStr = s.ToString();
                    if (statusStr != "1" && statusStr != "200" && !string.Equals(statusStr, "success", StringComparison.OrdinalIgnoreCase))
                    {
                        var desc = root.TryGetProperty("statusDescription", out var d) ? d.GetString() : null;
                        if (string.IsNullOrEmpty(desc) && root.TryGetProperty("message", out var m))
                            desc = m.GetString();
                        throw new NapixApiException($"eCourts Order API returned error status {statusStr}: {desc ?? text}");
                    }
                }

                // Look for PDF base64 property
                foreach (var propName in new[] { "pdf", "order_pdf", "data", "file", "pdf_data", "order_data", "order_file" })
                {
                    if (root.TryGetProperty(propName, out var prop) && prop.GetString() is { } b64 && !string.IsNullOrWhiteSpace(b64))
                    {
                        var cleaned = b64.Trim('\"', '\'', ' ', '\r', '\n', '\t', '\0')
                                         .Replace("\\\"", "")
                                         .Replace("\"", "")
                                         .Replace("\\/", "/")
                                         .Replace("\\r", "")
                                         .Replace("\\n", "")
                                         .Replace("\r", "")
                                         .Replace("\n", "")
                                         .Replace(" ", "")
                                         .Replace("\t", "");
                        var mod4 = cleaned.Length % 4;
                        if (mod4 > 0) cleaned = cleaned.PadRight(cleaned.Length + (4 - mod4), '=');
                        return Convert.FromBase64String(cleaned);
                    }
                }
            }
            catch (JsonException)
            {
                // Not valid JSON
            }
        }

        // 2d. If neither, log what came back and throw NapixApiException
        _logger.LogError("Decrypted bytes do not represent a valid PDF. Content snippet: {Snippet}",
            text.Length > 200 ? text[..200] : text);
        throw new NapixApiException($"eCourts returned non-PDF response: {(text.Length > 200 ? text[..200] : text)}");
    }

    private static void ValidateCnr(string cnr)
    {
        if (string.IsNullOrWhiteSpace(cnr) || cnr.Length != 16 || !cnr.All(char.IsLetterOrDigit))
        {
            throw new ArgumentException($"'{cnr}' is not a valid 16-character alphanumeric CNR.", nameof(cnr));
        }
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
        foreach (var propName in propNames)
        {
            if (parent.TryGetProperty(propName, out var prop))
            {
                if (prop.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in prop.EnumerateArray())
                        yield return item;
                }
                else if (prop.ValueKind == JsonValueKind.Object)
                {
                    foreach (var child in prop.EnumerateObject())
                    {
                        if (child.Value.ValueKind == JsonValueKind.Object)
                            yield return child.Value;
                    }
                }
                yield break;
            }
        }

        // Case-insensitive fallback
        if (parent.ValueKind == JsonValueKind.Object)
        {
            foreach (var child in parent.EnumerateObject())
            {
                if (propNames.Any(p => string.Equals(p, child.Name, StringComparison.OrdinalIgnoreCase)))
                {
                    if (child.Value.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var item in child.Value.EnumerateArray())
                            yield return item;
                    }
                    else if (child.Value.ValueKind == JsonValueKind.Object)
                    {
                        foreach (var subChild in child.Value.EnumerateObject())
                        {
                            if (subChild.Value.ValueKind == JsonValueKind.Object)
                                yield return subChild.Value;
                        }
                    }
                    yield break;
                }
            }
        }
    }

    private static bool TryParseDate(string? input, out DateOnly date)
    {
        date = default;
        if (string.IsNullOrWhiteSpace(input))
            return false;

        var formats = new[] { "yyyy-MM-dd", "dd-MM-yyyy", "dd/MM/yyyy", "yyyy/MM/dd", "yyyy-M-d", "d-M-yyyy" };
        foreach (var fmt in formats)
        {
            if (DateOnly.TryParseExact(input.Trim(), fmt, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out date))
                return true;
        }

        return DateOnly.TryParse(input, out date);
    }

    private static string? FormatDateForUrl(string? input)
    {
        if (TryParseDate(input, out var d))
            return d.ToString("yyyy-MM-dd");
        return input;
    }

    private static string? GetStringProp(JsonElement element, params string[] propertyNames)
    {
        foreach (var p in propertyNames)
        {
            if (element.TryGetProperty(p, out var val))
            {
                return val.ValueKind switch
                {
                    JsonValueKind.String => val.GetString(),
                    JsonValueKind.Number => val.ToString(),
                    _ => val.ToString()
                };
            }
        }
        return null;
    }
}
