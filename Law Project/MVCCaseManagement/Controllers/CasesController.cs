using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using MVCCaseManagement.Utils;

namespace MVCCaseManagement.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class CasesController : ControllerBase
    {
        private readonly IECourtsNapixService _napixService;
        private readonly ILogger<CasesController> _logger;

        public CasesController(IECourtsNapixService napixService, ILogger<CasesController> logger)
        {
            _napixService = napixService;
            _logger = logger;
        }

        /// <summary>
        /// GET /api/cases/{cnr} — Case details including parsed status and orders.
        /// </summary>
        [HttpGet("{cnr}")]
        public async Task<IActionResult> GetByCnr(string cnr)
        {
            string clean = (cnr ?? "").Trim().ToUpperInvariant();
            if (clean.Length != 16)
            {
                return BadRequest(new { error = $"Invalid CNR '{clean}' ({clean.Length} characters). A valid eCourts CNR must be exactly 16 characters (e.g. KADW200035292024)." });
            }

            bool isHc = clean.StartsWith("KAHC", StringComparison.OrdinalIgnoreCase) || 
                        (clean.Length >= 4 && clean.Substring(2, 2).Equals("HC", StringComparison.OrdinalIgnoreCase));

            var rawElement = await _napixService.GetCnrDetailsAsync(clean, isHc);
            if (!rawElement.HasValue)
            {
                return NotFound(new { error = $"Case details not found for CNR '{clean}'." });
            }

            return Ok(new
            {
                cnr = clean,
                courtType = isHc ? "High Court" : "District Court",
                data = rawElement.Value
            });
        }

        /// <summary>
        /// GET /api/cases/{cnr}/raw — Complete unadulterated decrypted JSON document directly from eCourts.
        /// </summary>
        [HttpGet("{cnr}/raw")]
        public async Task<IActionResult> GetRawByCnr(string cnr)
        {
            string clean = (cnr ?? "").Trim().ToUpperInvariant();
            if (clean.Length != 16)
            {
                return BadRequest(new { error = $"Invalid CNR '{clean}' ({clean.Length} characters). A valid eCourts CNR must be exactly 16 characters (e.g. KADW200035292024)." });
            }

            bool isHc = clean.StartsWith("KAHC", StringComparison.OrdinalIgnoreCase) || 
                        (clean.Length >= 4 && clean.Substring(2, 2).Equals("HC", StringComparison.OrdinalIgnoreCase));

            var rawElement = await _napixService.GetCnrDetailsAsync(clean, isHc);
            if (!rawElement.HasValue)
            {
                return NotFound(new { error = $"Raw case details not found for CNR '{clean}'." });
            }

            return Ok(rawElement.Value);
        }

        /// <summary>
        /// GET /api/cases/{cnr}/order?orderNo=1&date=yyyy-MM-dd — Order/judgment streamed directly as an inline PDF.
        /// </summary>
        [HttpGet("{cnr}/order")]
        public async Task<IActionResult> GetOrderPdf(string cnr, [FromQuery] string? orderNo = "1", [FromQuery] string? date = null)
        {
            if (string.IsNullOrWhiteSpace(cnr))
            {
                return BadRequest(new { error = "CNR is required." });
            }

            string clean = cnr.Trim().ToUpperInvariant();
            bool isHc = clean.StartsWith("KAHC", StringComparison.OrdinalIgnoreCase) || 
                        (clean.Length >= 4 && clean.Substring(2, 2).Equals("HC", StringComparison.OrdinalIgnoreCase));

            // Auto-detect order date if not specified
            if (string.IsNullOrWhiteSpace(date))
            {
                var rawDetails = await _napixService.GetCnrDetailsAsync(clean, isHc);
                if (rawDetails.HasValue)
                {
                    date = ExtractFirstOrderDate(rawDetails.Value);
                }
            }

            if (string.IsNullOrWhiteSpace(date))
            {
                date = DateTime.Today.ToString("yyyy-MM-dd");
            }

            var pdfBytes = await _napixService.GetOrderPdfBytesAsync(clean, orderNo ?? "1", date, isHc);
            if (pdfBytes != null && pdfBytes.Length > 0)
            {
                Response.Headers["Content-Disposition"] = $"inline; filename=\"{clean}_order_{orderNo}.pdf\"";
                return File(pdfBytes, "application/pdf");
            }

            return NotFound(new { error = $"Order PDF not available for CNR '{clean}' on date '{date}'." });
        }

        /// <summary>
        /// GET /api/cases/{cnr}/view — Interactive in-browser viewer page embedding the order in an iframe.
        /// </summary>
        [HttpGet("{cnr}/view")]
        public async Task<IActionResult> ViewOrderPage(string cnr, [FromQuery] string? orderNo = "1", [FromQuery] string? date = null)
        {
            if (string.IsNullOrWhiteSpace(cnr))
            {
                return BadRequest("CNR is required.");
            }

            string clean = cnr.Trim().ToUpperInvariant();
            bool isHc = clean.StartsWith("KAHC", StringComparison.OrdinalIgnoreCase) || 
                        (clean.Length >= 4 && clean.Substring(2, 2).Equals("HC", StringComparison.OrdinalIgnoreCase));

            string? detectedDate = date;
            if (string.IsNullOrWhiteSpace(detectedDate))
            {
                var rawDetails = await _napixService.GetCnrDetailsAsync(clean, isHc);
                if (rawDetails.HasValue)
                {
                    detectedDate = ExtractFirstOrderDate(rawDetails.Value);
                }
            }

            string pdfSrc = $"/api/cases/{clean}/order?orderNo={Uri.EscapeDataString(orderNo ?? "1")}" + 
                            (!string.IsNullOrEmpty(detectedDate) ? $"&date={Uri.EscapeDataString(detectedDate)}" : "");

            string courtBadge = isHc ? "High Court" : "District Court";
            string badgeColor = isHc ? "#8b5cf6" : "#0284c7";

            string html = $@"<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8"">
    <title>CNR {clean} — Court Order / Judgment Viewer</title>
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <style>
        * {{ box-sizing: border-box; margin: 0; padding: 0; }}
        body {{ font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; background: #0f172a; color: #f8fafc; display: flex; flex-direction: column; height: 100vh; overflow: hidden; }}
        header {{ background: #1e293b; padding: 12px 20px; border-bottom: 1px solid #334155; display: flex; flex-wrap: wrap; justify-content: space-between; align-items: center; }}
        .title {{ font-size: 16px; font-weight: 700; color: #38bdf8; }}
        .badge {{ background: {badgeColor}; color: #fff; font-size: 11px; font-weight: 700; padding: 3px 10px; border-radius: 9999px; margin-left: 8px; text-transform: uppercase; }}
        .meta {{ font-size: 13px; color: #94a3b8; margin-top: 4px; }}
        .actions a {{ display: inline-block; margin-left: 12px; padding: 6px 14px; background: #334155; color: #38bdf8; text-decoration: none; font-size: 13px; font-weight: 600; border-radius: 6px; }}
        .actions a:hover {{ background: #475569; }}
        iframe {{ flex: 1; width: 100%; height: 100%; border: none; background: #1e293b; }}
    </style>
</head>
<body>
    <header>
        <div>
            <div class=""title"">
                CNR: {clean} <span class=""badge"">{courtBadge}</span>
            </div>
            <div class=""meta"">e-Courts Official Live Order / Judgment Viewer</div>
        </div>
        <div class=""actions"">
            <a href=""{pdfSrc}"" target=""_blank"" download>⬇ Download PDF</a>
            <a href=""/api/cases/{clean}/raw"" target=""_blank"">Raw JSON &rarr;</a>
        </div>
    </header>
    <iframe src=""{pdfSrc}"" allowfullscreen></iframe>
</body>
</html>";

            return Content(html, "text/html");
        }

        private static string? ExtractFirstOrderDate(JsonElement root)
        {
            if (root.ValueKind != JsonValueKind.Object) return null;

            string[] orderKeys = { "finalorder", "final_order", "interimorder", "interim_order", "orders", "order_details" };
            foreach (var key in orderKeys)
            {
                if (root.TryGetProperty(key, out var prop))
                {
                    if (prop.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var item in prop.EnumerateArray())
                        {
                            if (item.ValueKind == JsonValueKind.Object)
                            {
                                foreach (var dKey in new[] { "order_date", "ord_date", "date_of_decision", "date" })
                                {
                                    if (item.TryGetProperty(dKey, out var dProp) && dProp.ValueKind == JsonValueKind.String)
                                    {
                                        string? val = dProp.GetString();
                                        if (!string.IsNullOrWhiteSpace(val)) return val.Trim();
                                    }
                                }
                            }
                        }
                    }
                }
            }

            foreach (var dKey in new[] { "decision_date", "next_date", "filing_date", "dt_regis" })
            {
                if (root.TryGetProperty(dKey, out var dProp) && dProp.ValueKind == JsonValueKind.String)
                {
                    string? val = dProp.GetString();
                    if (!string.IsNullOrWhiteSpace(val)) return val.Trim();
                }
            }

            return null;
        }
    }
}
