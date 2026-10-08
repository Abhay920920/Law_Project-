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

    /// <summary>POST /api/cases/token/regenerate — clears cached token and fetches fresh OAuth2 Bearer token.</summary>
    [HttpPost("token/regenerate")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public Task<IActionResult> RegenerateToken([FromQuery] string? module, CancellationToken cancellationToken) =>
        ExecuteSafeAsync(async () =>
        {
            _authService.InvalidateToken(module);
            var token = await _authService.GetAccessTokenAsync(module, cancellationToken);
            return (IActionResult)Ok(new
            {
                success = true,
                module = module ?? "MVC",
                message = $"Successfully fetched fresh OAuth2 Bearer token from NAPIX gateway for {module ?? "MVC"}.",
                tokenPreview = token[..Math.Min(25, token.Length)] + "...",
                tokenLength = token.Length,
                fetchedAtUtc = DateTime.UtcNow
            });
        });

    /// <summary>GET /api/cases/{cnr} — comprehensive case details with parsed hearings and orders.</summary>
    [HttpGet("{cnr}")]
    [ProducesResponseType(typeof(CaseFullDetails), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public Task<IActionResult> GetByCnr(string cnr, [FromQuery] string? module, CancellationToken cancellationToken) =>
        ExecuteSafeAsync(async () => Ok(await _client.GetFullCaseDetailsAsync(cnr, module, cancellationToken)), cnr);

    /// <summary>GET /api/cases/{cnr}/raw — complete unadulterated decrypted JSON document directly from eCourts.</summary>
    [HttpGet("{cnr}/raw")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public Task<IActionResult> GetRawByCnr(string cnr, [FromQuery] string? module, CancellationToken cancellationToken) =>
        ExecuteSafeAsync(async () => Ok(await _client.GetRawCaseDetailsAsync(cnr, module, cancellationToken)), cnr);

    /// <summary>GET /api/cases/{cnr}/orders-zip — downloads all interim & final orders/judgments as a ZIP archive.</summary>
    [HttpGet("{cnr}/orders-zip")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public Task<IActionResult> DownloadOrdersZip(string cnr, [FromQuery] string? module, CancellationToken cancellationToken) =>
        ExecuteSafeAsync(async () =>
        {
            var (zipBytes, count) = await _client.DownloadAllOrdersZipAsync(cnr, module, cancellationToken);
            return count == 0
                ? (IActionResult)NotFound(new { message = $"No interim orders or judgments found for CNR {cnr}." })
                : File(zipBytes, "application/zip", $"{cnr}_all_orders.zip");
        }, cnr);

    /// <summary>POST /api/cases/current-status — bulk lightweight status for up to 500 CNRs.</summary>
    [HttpPost("current-status")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public Task<IActionResult> GetCurrentStatus([FromBody] CurrentStatusRequest request, [FromQuery] string? module, CancellationToken cancellationToken) =>
        ExecuteSafeAsync(async () => Ok(await _client.GetCurrentStatusAsync(request.Cnrs, module, cancellationToken)));

    /// <summary>GET /api/cases/{cnr}/business?date=yyyy-MM-dd — business transacted on a given date.</summary>
    [HttpGet("{cnr}/business")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public Task<IActionResult> GetShowBusiness(string cnr, [FromQuery] DateOnly date, [FromQuery] string? module, CancellationToken cancellationToken) =>
        ExecuteSafeAsync(async () => Ok(await _client.GetShowBusinessAsync(cnr, date, module, cancellationToken)), cnr);

    /// <summary>
    /// GET /api/cases/{cnr}/order?orderNo=1&amp;date=yyyy-MM-dd — order/judgment as PDF.
    /// If orderNo or date is omitted, auto-detects first available order/judgment.
    /// </summary>
    [HttpGet("{cnr}/order")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public Task<IActionResult> GetOrderPdf(string cnr, [FromQuery] string? orderNo, [FromQuery] string? date, [FromQuery] string? module, CancellationToken cancellationToken) =>
        ExecuteSafeAsync(async () =>
        {
            DateOnly? parsedOrderDate = null;
            if (!string.IsNullOrWhiteSpace(date) && TryParseDate(date, out var dVal))
                parsedOrderDate = dVal;

            if (string.IsNullOrWhiteSpace(orderNo) || parsedOrderDate is null)
            {
                var fullCase = await _client.GetFullCaseDetailsAsync(cnr, module, cancellationToken);
                var allOrders = fullCase.FinalOrdersAndJudgments.Concat(fullCase.InterimOrders).ToList();
                var targetOrder = (!string.IsNullOrWhiteSpace(orderNo)
                    ? allOrders.FirstOrDefault(o => string.Equals(o.OrderNumber, orderNo, StringComparison.OrdinalIgnoreCase))
                    : null) ?? allOrders.FirstOrDefault();

                if (targetOrder == null || string.IsNullOrEmpty(targetOrder.OrderNumber) || string.IsNullOrEmpty(targetOrder.OrderDate))
                    return (IActionResult)NotFound(new { error = $"No orders or judgments recorded for CNR '{cnr}'." });

                orderNo ??= targetOrder.OrderNumber;
                if (parsedOrderDate is null)
                {
                    if (!TryParseDate(targetOrder.OrderDate, out var pDate))
                        return BadRequest(new { error = $"Could not parse recorded order date '{targetOrder.OrderDate}'." });
                    parsedOrderDate = pDate;
                }
            }

            var pdfBytes = await _client.GetOrderPdfAsync(cnr, orderNo, parsedOrderDate.Value, module, cancellationToken);
            Response.Headers["Content-Disposition"] = $"inline; filename=\"{cnr}_order_{orderNo}.pdf\"";
            return File(pdfBytes, "application/pdf");
        }, cnr);

    /// <summary>GET /api/cases/{cnr}/view — interactive in-browser viewer page for orders and judgments.</summary>
    [HttpGet("{cnr}/view")]
    [Produces("text/html")]
    public async Task<IActionResult> ViewOrderPage(string cnr, [FromQuery] string? orderNo, [FromQuery] DateOnly? date, [FromQuery] string? module, CancellationToken cancellationToken)
    {
        try
        {
            var fullCase = await _client.GetFullCaseDetailsAsync(cnr, module, cancellationToken);
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

    private async Task<IActionResult> ExecuteSafeAsync(Func<Task<IActionResult>> action, string? cnr = null)
    {
        try
        {
            return await action();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (NapixNotFoundException ex)
        {
            _logger.LogInformation("NAPIX record not found: {Cnr} - {Msg}", cnr, ex.Message);
            return NotFound(new { error = ex.Message, cnr });
        }
        catch (NapixApiException ex)
        {
            _logger.LogError(ex, "NAPIX API error for {Cnr}", cnr);
            return StatusCode(StatusCodes.Status502BadGateway, new { error = ex.Message });
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Network error communicating with NAPIX for {Cnr}", cnr);
            return StatusCode(StatusCodes.Status502BadGateway, new { error = $"Network error calling NAPIX: {ex.Message}" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error for {Cnr}", cnr);
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = ex.Message, type = ex.GetType().Name });
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
}
