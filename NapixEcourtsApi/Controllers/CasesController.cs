using Microsoft.AspNetCore.Mvc;
using NapixEcourtsApi.Models;
using NapixEcourtsApi.Services;

namespace NapixEcourtsApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CasesController : ControllerBase
{
    private readonly INapixEcourtsClient _client;
    private readonly INapixAuthService _authService;
    private readonly ILogger<CasesController> _logger;

    public CasesController(INapixEcourtsClient client, INapixAuthService authService, ILogger<CasesController> logger)
    {
        _client = client;
        _authService = authService;
        _logger = logger;
    }

    /// <summary>
    /// POST /api/cases/token/regenerate — clears cached token and fetches a brand-new OAuth2 Bearer token from NAPIX.
    /// </summary>
    [HttpPost("token/regenerate")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> RegenerateToken(CancellationToken cancellationToken)
    {
        try
        {
            _authService.InvalidateToken();
            var token = await _authService.GetAccessTokenAsync(cancellationToken);
            return Ok(new
            {
                success = true,
                message = "Successfully fetched a fresh OAuth2 Bearer token from NAPIX gateway.",
                tokenPreview = token[..Math.Min(25, token.Length)] + "...",
                tokenLength = token.Length,
                fetchedAtUtc = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to regenerate NAPIX OAuth token");
            return StatusCode(StatusCodes.Status502BadGateway, new
            {
                success = false,
                error = ex.Message
            });
        }
    }

    /// <summary>
    /// GET /api/cases/{cnr} — comprehensive case details:
    /// basic details, all hearings, all interim orders & judgments with direct PDF download URLs, and the raw payload.
    /// </summary>
    [HttpGet("{cnr}")]
    [ProducesResponseType(typeof(CaseFullDetails), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> GetByCnr(string cnr, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _client.GetFullCaseDetailsAsync(cnr, cancellationToken);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (NapixNotFoundException ex)
        {
            _logger.LogInformation("NAPIX CNR not found: {Cnr} - {Msg}", cnr, ex.Message);
            return NotFound(new { error = ex.Message, cnr });
        }
        catch (NapixApiException ex)
        {
            _logger.LogError(ex, "NAPIX CNR lookup failed for {Cnr}", cnr);
            return StatusCode(StatusCodes.Status502BadGateway, new { error = ex.Message });
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Network/HTTP error communicating with NAPIX for {Cnr}", cnr);
            return StatusCode(StatusCodes.Status502BadGateway, new { error = $"Network error calling NAPIX: {ex.Message}" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during CNR lookup for {Cnr}", cnr);
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = ex.Message, type = ex.GetType().Name });
        }
    }

    /// <summary>GET /api/cases/{cnr}/raw — complete unadulterated decrypted JSON document directly from eCourts.</summary>
    [HttpGet("{cnr}/raw")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> GetRawByCnr(string cnr, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _client.GetRawCaseDetailsAsync(cnr, cancellationToken);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (NapixNotFoundException ex)
        {
            _logger.LogInformation("NAPIX raw CNR not found: {Cnr} - {Msg}", cnr, ex.Message);
            return NotFound(new { error = ex.Message, cnr });
        }
        catch (NapixApiException ex)
        {
            _logger.LogError(ex, "NAPIX raw CNR lookup failed for {Cnr}", cnr);
            return StatusCode(StatusCodes.Status502BadGateway, new { error = ex.Message });
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Network/HTTP error communicating with NAPIX for {Cnr}", cnr);
            return StatusCode(StatusCodes.Status502BadGateway, new { error = $"Network error calling NAPIX: {ex.Message}" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during raw CNR lookup for {Cnr}", cnr);
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = ex.Message, type = ex.GetType().Name });
        }
    }

    /// <summary>GET /api/cases/{cnr}/orders-zip — downloads all interim & final orders/judgments as a single ZIP archive.</summary>
    [HttpGet("{cnr}/orders-zip")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> DownloadOrdersZip(string cnr, CancellationToken cancellationToken)
    {
        try
        {
            var (zipBytes, count) = await _client.DownloadAllOrdersZipAsync(cnr, cancellationToken);
            if (count == 0)
            {
                return NotFound(new { message = $"No interim orders or judgments found for CNR {cnr}." });
            }
            return File(zipBytes, "application/zip", $"{cnr}_all_orders.zip");
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (NapixNotFoundException ex)
        {
            _logger.LogInformation("NAPIX orders-zip not found: {Cnr} - {Msg}", cnr, ex.Message);
            return NotFound(new { error = ex.Message, cnr });
        }
        catch (NapixApiException ex)
        {
            _logger.LogError(ex, "NAPIX orders-zip lookup failed for {Cnr}", cnr);
            return StatusCode(StatusCodes.Status502BadGateway, new { error = ex.Message });
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Network/HTTP error communicating with NAPIX for {Cnr}", cnr);
            return StatusCode(StatusCodes.Status502BadGateway, new { error = $"Network error calling NAPIX: {ex.Message}" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error creating orders-zip for {Cnr}", cnr);
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = ex.Message, type = ex.GetType().Name });
        }
    }

    /// <summary>POST /api/cases/current-status — bulk lightweight status for up to 500 CNRs.</summary>
    [HttpPost("current-status")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> GetCurrentStatus(
        [FromBody] CurrentStatusRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _client.GetCurrentStatusAsync(request.Cnrs, cancellationToken);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (NapixApiException ex)
        {
            _logger.LogError(ex, "NAPIX current-status lookup failed");
            return StatusCode(StatusCodes.Status502BadGateway, new { error = ex.Message });
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Network/HTTP error communicating with NAPIX current-status");
            return StatusCode(StatusCodes.Status502BadGateway, new { error = $"Network error calling NAPIX: {ex.Message}" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during current-status lookup");
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = ex.Message, type = ex.GetType().Name });
        }
    }

    /// <summary>GET /api/cases/{cnr}/business?date=yyyy-MM-dd — business transacted on a given date.</summary>
    [HttpGet("{cnr}/business")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> GetShowBusiness(
        string cnr, [FromQuery] DateOnly date, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _client.GetShowBusinessAsync(cnr, date, cancellationToken);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (NapixNotFoundException ex)
        {
            _logger.LogInformation("NAPIX business not found for {Cnr}: {Msg}", cnr, ex.Message);
            return NotFound(new { error = ex.Message, cnr });
        }
        catch (NapixApiException ex)
        {
            _logger.LogError(ex, "NAPIX show-business lookup failed for {Cnr}", cnr);
            return StatusCode(StatusCodes.Status502BadGateway, new { error = ex.Message });
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Network/HTTP error communicating with NAPIX show-business for {Cnr}", cnr);
            return StatusCode(StatusCodes.Status502BadGateway, new { error = $"Network error calling NAPIX: {ex.Message}" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during show-business lookup for {Cnr}", cnr);
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = ex.Message, type = ex.GetType().Name });
        }
    }

    /// <summary>
    /// GET /api/cases/{cnr}/order?orderNo=1&amp;date=yyyy-MM-dd — order/judgment as a PDF file.
    /// If orderNo or date is omitted, automatically auto-detects and downloads the first available order/judgment for the case.
    /// </summary>
    [HttpGet("{cnr}/order")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> GetOrderPdf(
        string cnr, [FromQuery] string? orderNo, [FromQuery] DateOnly? date, CancellationToken cancellationToken)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(orderNo) || date is null)
            {
                var fullCase = await _client.GetFullCaseDetailsAsync(cnr, cancellationToken);
                var firstOrder = fullCase.FinalOrdersAndJudgments.FirstOrDefault() 
                              ?? fullCase.InterimOrders.FirstOrDefault();

                if (firstOrder == null || string.IsNullOrEmpty(firstOrder.OrderNumber) || string.IsNullOrEmpty(firstOrder.OrderDate))
                {
                    return NotFound(new { error = $"No orders or judgments recorded for CNR '{cnr}'." });
                }

                orderNo = firstOrder.OrderNumber;
                if (!TryParseDate(firstOrder.OrderDate, out var parsedDate))
                {
                    return BadRequest(new { error = $"Could not parse recorded order date '{firstOrder.OrderDate}'." });
                }
                date = parsedDate;
            }

            var pdfBytes = await _client.GetOrderPdfAsync(cnr, orderNo, date.Value, cancellationToken);
            Response.Headers["Content-Disposition"] = $"inline; filename=\"{cnr}_order_{orderNo}.pdf\"";
            return File(pdfBytes, "application/pdf");
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (NapixNotFoundException ex)
        {
            _logger.LogInformation("NAPIX order not found for {Cnr}: {Msg}", cnr, ex.Message);
            return NotFound(new { error = ex.Message, cnr });
        }
        catch (NapixApiException ex)
        {
            _logger.LogError(ex, "NAPIX order lookup failed for {Cnr}", cnr);
            return StatusCode(StatusCodes.Status502BadGateway, new { error = ex.Message });
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Network/HTTP error communicating with NAPIX order for {Cnr}", cnr);
            return StatusCode(StatusCodes.Status502BadGateway, new { error = $"Network error calling NAPIX: {ex.Message}" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during order lookup for {Cnr}", cnr);
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = ex.Message, type = ex.GetType().Name });
        }
    }

    /// <summary>GET /api/cases/{cnr}/view — interactive in-browser viewer page to view orders and judgments.</summary>
    [HttpGet("{cnr}/view")]
    [Produces("text/html")]
    public async Task<IActionResult> ViewOrderPage(
        string cnr, [FromQuery] string? orderNo, [FromQuery] DateOnly? date, CancellationToken cancellationToken)
    {
        try
        {
            var fullCase = await _client.GetFullCaseDetailsAsync(cnr, cancellationToken);
            var b = fullCase.BasicInfo;
            var allOrders = fullCase.InterimOrders.Concat(fullCase.FinalOrdersAndJudgments).ToList();

            var selectedOrder = allOrders.FirstOrDefault(o => o.OrderNumber == orderNo) 
                             ?? fullCase.FinalOrdersAndJudgments.FirstOrDefault() 
                             ?? fullCase.InterimOrders.FirstOrDefault();

            var pdfSrc = selectedOrder?.ViewUrl ?? $"/api/cases/{cnr}/order";

            var orderButtonsHtml = string.Join("", allOrders.Select(o =>
                $"<a href=\"/api/cases/{cnr}/view?orderNo={Uri.EscapeDataString(o.OrderNumber ?? "")}\" style=\"display:inline-block;margin:4px 6px;padding:6px 12px;background:{(o == selectedOrder ? "#2563eb" : "#334155")};color:#fff;border-radius:6px;text-decoration:none;font-size:13px;font-weight:600;\">{o.OrderType} #{o.OrderNumber} ({o.OrderDate})</a>"));

            var html = $@"<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8"">
    <title>Case {cnr} — Order / Judgment Viewer</title>
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <style>
        * {{ box-sizing: border-box; margin: 0; padding: 0; }}
        body {{ font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; background: #0f172a; color: #f8fafc; display: flex; flex-direction: column; height: 100vh; overflow: hidden; }}
        header {{ background: #1e293b; padding: 12px 20px; border-bottom: 1px solid #334155; display: flex; flex-wrap: wrap; justify-content: space-between; align-items: center; }}
        .title {{ font-size: 16px; font-weight: 700; color: #38bdf8; }}
        .meta {{ font-size: 13px; color: #94a3b8; margin-top: 4px; }}
        .orders-bar {{ background: #0f172a; padding: 8px 20px; border-bottom: 1px solid #1e293b; overflow-x: auto; white-space: nowrap; }}
        iframe {{ flex: 1; width: 100%; height: 100%; border: none; background: #334155; }}
    </style>
</head>
<body>
    <header>
        <div>
            <div class=""title"">
                CNR: {cnr} — {b?.CaseTypeName} {b?.RegistrationNumber}/{b?.RegistrationYear}
                <span style=""background:{(fullCase.CourtType == "High Court" ? "#8b5cf6" : "#0284c7")};color:#fff;font-size:11px;font-weight:700;padding:2px 8px;border-radius:9999px;margin-left:8px;text-transform:uppercase;"">{fullCase.CourtType}</span>
            </div>
            <div class=""meta""><b>Court:</b> {b?.CourtEstablishmentName} | <b>Parties:</b> {b?.PetitionerName} vs. {b?.RespondentName}</div>
        </div>
        <div>
            <a href=""/api/cases/{cnr}"" target=""_blank"" style=""color:#38bdf8;text-decoration:none;font-size:13px;font-weight:600;margin-left:16px;"">Case JSON &rarr;</a>
            <a href=""/swagger"" style=""color:#94a3b8;text-decoration:none;font-size:13px;font-weight:600;margin-left:16px;"">&larr; Swagger</a>
        </div>
    </header>
    {(allOrders.Count > 1 ? $"<div class=\"orders-bar\">{orderButtonsHtml}</div>" : "")}
    <iframe src=""{pdfSrc}""></iframe>
</body>
</html>";

            return Content(html, "text/html");
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error loading order viewer: {ex.Message}");
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
}
