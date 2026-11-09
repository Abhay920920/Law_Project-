using System;
using System.Security.Claims;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using MVCCaseManagement.DAL;
using MVCCaseManagement.Models.Audit;
using MVCCaseManagement.Services;
using Microsoft.AspNetCore.Http;
using MVCCaseManagement.Common;
using MVCCaseManagement.Services.Audit;

namespace MVCCaseManagement.Controllers
{
    [Authorize]
    public class ActivityLogController : Controller
    {
        private readonly ICaseActivityLogger _activityLogger;
        private readonly IMasterRepository _masterRepo;
        private readonly ILogger<ActivityLogController> _logger;
        private readonly IOrganizationSettingsService? _orgSettings;

        public ActivityLogController(
            ICaseActivityLogger activityLogger,
            IMasterRepository masterRepo,
            ILogger<ActivityLogController> logger,
            IOrganizationSettingsService? orgSettings = null)
        {
            _activityLogger = activityLogger;
            _masterRepo = masterRepo;
            _logger = logger;
            _orgSettings = orgSettings;
        }

        private (bool isCentralOffice, int? divisionId) ResolveUserDivisionScope()
        {
            var divIdString = User.FindFirstValue("DivisionID");
            bool isCentralOffice = divIdString == "0" || divIdString == "5" 
                || User.IsInRole("Admin") || User.IsInRole("CLO") || User.IsInRole("Dy CLO") || User.IsInRole("DyCLO") || User.IsInRole("MD") || User.IsInRole("CO");

            if (isCentralOffice)
            {
                return (true, null);
            }

            if (int.TryParse(divIdString, out int userDivId) && userDivId > 0)
            {
                return (false, userDivId);
            }

            int? sessDiv = HttpContext.Session.GetInt32(SessionKeys.DivisionID);
            if (sessDiv.HasValue && sessDiv.Value > 0)
            {
                if (sessDiv.Value == 5) return (true, null);
                return (false, sessDiv.Value);
            }

            // Fail-closed security: unassigned / unauthenticated division access denied
            return (false, -1);
        }

        public async Task<IActionResult> Index(ActivityLogFilter filter, CancellationToken cancellationToken)
        {
            filter ??= new ActivityLogFilter();

            var scope = ResolveUserDivisionScope();
            if (!scope.isCentralOffice)
            {
                if (!scope.divisionId.HasValue || scope.divisionId <= 0)
                {
                    return Forbid();
                }
                filter.DivisionID = scope.divisionId;
                ViewBag.IsDivisionRestricted = true;
            }
            else
            {
                ViewBag.IsDivisionRestricted = false;
            }

            ViewBag.Divisions = _masterRepo.GetAllDivisions();
            var model = await _activityLogger.GetLogSheetAsync(filter, cancellationToken);

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> GetDiff(long id, CancellationToken cancellationToken)
        {
            var scope = ResolveUserDivisionScope();
            if (!scope.isCentralOffice && (!scope.divisionId.HasValue || scope.divisionId <= 0))
            {
                return Forbid();
            }

            var diffs = await _activityLogger.GetLogDiffAsync(id, scope.isCentralOffice ? null : scope.divisionId, cancellationToken);
            return Json(diffs);
        }

        [HttpGet]
        public async Task<IActionResult> ExportCsv(ActivityLogFilter filter, CancellationToken cancellationToken)
        {
            filter ??= new ActivityLogFilter();
            filter.Page = 1;
            filter.PageSize = 5000; // Export up to 5,000 records

            var scope = ResolveUserDivisionScope();
            if (!scope.isCentralOffice)
            {
                if (!scope.divisionId.HasValue || scope.divisionId <= 0)
                {
                    return Forbid();
                }
                filter.DivisionID = scope.divisionId;
            }

            var model = await _activityLogger.GetLogSheetAsync(filter, cancellationToken);

            var sb = new StringBuilder();
            sb.AppendLine("LogID,Timestamp,Username,FullName,Role,Division,IpAddress,MacAddress,Module,CaseNumber,VehicleNo,ActionType,Summary,Modifications");

            foreach (var log in model.Logs)
            {
                sb.AppendLine(string.Format("\"{0}\",\"{1:yyyy-MM-dd HH:mm:ss}\",\"{2}\",\"{3}\",\"{4}\",\"{5}\",\"{6}\",\"{7}\",\"{8}\",\"{9}\",\"{10}\",\"{11}\",\"{12}\",\"{13}\"",
                    log.LogID,
                    log.Timestamp,
                    EscapeCsv(log.Username),
                    EscapeCsv(log.UserFullName ?? ""),
                    EscapeCsv(log.UserRole ?? ""),
                    EscapeCsv(log.DivisionName ?? ""),
                    EscapeCsv(log.IpAddress ?? ""),
                    EscapeCsv(log.MacAddress ?? ""),
                    EscapeCsv(log.Module),
                    EscapeCsv(log.CaseNumber),
                    EscapeCsv(log.VehicleNo ?? ""),
                    EscapeCsv(log.ActionType),
                    EscapeCsv(log.ActionSummary),
                    EscapeCsv(log.ChangedFieldsSummary ?? "")
                ));
            }

            byte[] bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
            var orgShort = _orgSettings?.GetCurrentSettings()?.OrganizationShortName ?? "NWKRTC";
            string fileName = $"{orgShort}_Case_Activity_Logs_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
            return File(bytes, "text/csv; charset=utf-8", fileName);
        }

        private static string EscapeCsv(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            return value.Replace("\"", "\"\"");
        }
    }
}
