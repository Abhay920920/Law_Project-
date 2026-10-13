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
using MVCCaseManagement.Services.Audit;

namespace MVCCaseManagement.Controllers
{
    [Authorize]
    public class ActivityLogController : Controller
    {
        private readonly ICaseActivityLogger _activityLogger;
        private readonly IMasterRepository _masterRepo;
        private readonly ILogger<ActivityLogController> _logger;

        public ActivityLogController(
            ICaseActivityLogger activityLogger,
            IMasterRepository masterRepo,
            ILogger<ActivityLogController> logger)
        {
            _activityLogger = activityLogger;
            _masterRepo = masterRepo;
            _logger = logger;
        }

        public async Task<IActionResult> Index(ActivityLogFilter filter, CancellationToken cancellationToken)
        {
            filter ??= new ActivityLogFilter();

            // Division-level scoping for non-HQ users
            var divIdString = User.FindFirstValue("DivisionID");
            bool isCentralOffice = string.IsNullOrEmpty(divIdString) || divIdString == "0" || divIdString == "5" 
                || User.IsInRole("Admin") || User.IsInRole("CLO") || User.IsInRole("Dy CLO") || User.IsInRole("DyCLO") || User.IsInRole("MD") || User.IsInRole("CO");

            if (!isCentralOffice && int.TryParse(divIdString, out int userDivId))
            {
                filter.DivisionID = userDivId;
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
            var diffs = await _activityLogger.GetLogDiffAsync(id, cancellationToken);
            return Json(diffs);
        }

        [HttpGet]
        public async Task<IActionResult> ExportCsv(ActivityLogFilter filter, CancellationToken cancellationToken)
        {
            filter ??= new ActivityLogFilter();
            filter.Page = 1;
            filter.PageSize = 5000; // Export up to 5,000 records

            var divIdString = User.FindFirstValue("DivisionID");
            bool isCentralOffice = string.IsNullOrEmpty(divIdString) || divIdString == "0" || divIdString == "5" 
                || User.IsInRole("Admin") || User.IsInRole("CLO") || User.IsInRole("Dy CLO") || User.IsInRole("DyCLO") || User.IsInRole("MD") || User.IsInRole("CO");

            if (!isCentralOffice && int.TryParse(divIdString, out int userDivId))
            {
                filter.DivisionID = userDivId;
            }

            var model = await _activityLogger.GetLogSheetAsync(filter, cancellationToken);

            var sb = new StringBuilder();
            sb.AppendLine("LogID,Timestamp,Username,FullName,Role,Division,Module,CaseNumber,VehicleNo,ActionType,Summary,Modifications");

            foreach (var log in model.Logs)
            {
                sb.AppendLine(string.Format("\"{0}\",\"{1:yyyy-MM-dd HH:mm:ss}\",\"{2}\",\"{3}\",\"{4}\",\"{5}\",\"{6}\",\"{7}\",\"{8}\",\"{9}\",\"{10}\",\"{11}\"",
                    log.LogID,
                    log.Timestamp,
                    EscapeCsv(log.Username),
                    EscapeCsv(log.UserFullName ?? ""),
                    EscapeCsv(log.UserRole ?? ""),
                    EscapeCsv(log.DivisionName ?? ""),
                    EscapeCsv(log.Module),
                    EscapeCsv(log.CaseNumber),
                    EscapeCsv(log.VehicleNo ?? ""),
                    EscapeCsv(log.ActionType),
                    EscapeCsv(log.ActionSummary),
                    EscapeCsv(log.ChangedFieldsSummary ?? "")
                ));
            }

            byte[] bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
            string fileName = $"NWKRTC_Case_Activity_Logs_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
            return File(bytes, "text/csv; charset=utf-8", fileName);
        }

        private static string EscapeCsv(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            return value.Replace("\"", "\"\"");
        }
    }
}
