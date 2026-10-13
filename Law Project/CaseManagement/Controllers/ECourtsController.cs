using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MVCCaseManagement.DAL;
using MVCCaseManagement.Models;
using MVCCaseManagement.Utils;
using System.Security.Claims;

namespace MVCCaseManagement.Controllers
{
    [Authorize]
    public class ECourtsController : Controller
    {
        private readonly IECourtsNapixService _napixService;
        private readonly IECourtsRepository _ecourtsRepo;
        private readonly ICaseRepository _caseRepo;
        private readonly DBHelper _db;
        private readonly IConfiguration _config;
        private readonly Microsoft.Extensions.Logging.ILogger<ECourtsController> _logger;
        private readonly NapixSyncEngine _syncEngine;

        public ECourtsController(
            IECourtsNapixService napixService,
            IECourtsRepository ecourtsRepo,
            ICaseRepository caseRepo,
            DBHelper db,
            IConfiguration config,
            Microsoft.Extensions.Logging.ILogger<ECourtsController> logger,
            NapixSyncEngine syncEngine)
        {
            _napixService = napixService;
            _ecourtsRepo = ecourtsRepo;
            _caseRepo = caseRepo;
            _db = db;
            _config = config;
            _logger = logger;
            _syncEngine = syncEngine;
        }

        // POST / GET: /ECourts/QueueCaseSync
        [HttpPost, HttpGet]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> QueueCaseSync(int caseId = 0, string? cnrNumber = null, string module = "MVC")
        {
            if (caseId <= 0 && string.IsNullOrWhiteSpace(cnrNumber))
            {
                return Json(new { success = false, message = "Case ID or CNR number is required." });
            }

            string cleanModule = NapixQuotaService.NormalizeModule(module);
            string cleanCnr = (cnrNumber ?? "").Trim().ToUpperInvariant();

            // If CNR wasn't provided in form, resolve from DB
            if (string.IsNullOrWhiteSpace(cleanCnr) && caseId > 0)
            {
                using var conn = _db.GetConnection();
                string table = cleanModule.Equals("OtherCourts", StringComparison.OrdinalIgnoreCase) 
                    ? "OTHER_CASES" 
                    : cleanModule.Equals("Labour", StringComparison.OrdinalIgnoreCase) 
                        ? "LABOUR_CASES" 
                        : "MVC_CASES";
                cleanCnr = conn.ExecuteScalar<string>($"SELECT CNRNumber FROM {table} WHERE CaseID = @caseId", new { caseId }) ?? "";
            }

            if (string.IsNullOrWhiteSpace(cleanCnr))
            {
                return Json(new { success = false, message = "Case has no CNR Number linked. Link a CNR before synchronizing." });
            }

            // Rule 6: Manual requests receive highest priority (Priority = 1) and must use persistent queue
            var (queued, message, queueId) = await _syncEngine.EnqueueCaseAsync(cleanModule, caseId, cleanCnr, priority: 1);

            return Json(new
            {
                success = queued,
                message = message,
                queueId = queueId,
                caseId = caseId,
                cnrNumber = cleanCnr,
                module = cleanModule,
                status = "Queued"
            });
        }

        // GET: /ECourts/GetQueueStatus
        [HttpGet]
        public async Task<IActionResult> GetQueueStatus(long? queueId, int? caseId, string? cnrNumber, string module = "MVC")
        {
            string cleanModule = NapixQuotaService.NormalizeModule(module);
            using var conn = _db.GetConnection();

            string queueSql = queueId.HasValue && queueId.Value > 0
                ? "SELECT TOP 1 QueueId, Status, LastError, ProcessedAt, AttemptCount FROM dbo.NAPIX_SYNC_QUEUE WHERE QueueId = @queueId"
                : "SELECT TOP 1 QueueId, Status, LastError, ProcessedAt, AttemptCount FROM dbo.NAPIX_SYNC_QUEUE WHERE Module = @cleanModule AND (CaseId = @caseId OR CNRNumber = @cnrNumber) ORDER BY QueueId DESC";

            var qRecord = await conn.QueryFirstOrDefaultAsync(queueSql, new { queueId, cleanModule, caseId, cnrNumber });

            if (qRecord == null)
            {
                return Json(new { success = false, message = "No queue record found." });
            }

            string tableName = cleanModule.Equals("OtherCourts", StringComparison.OrdinalIgnoreCase)
                ? "OTHER_CASES"
                : cleanModule.Equals("Labour", StringComparison.OrdinalIgnoreCase)
                    ? "LABOUR_CASES"
                    : "dbo.MVC_CASES";
            string dateCol = cleanModule.Equals("OtherCourts", StringComparison.OrdinalIgnoreCase)
                ? "NextDateOfHearing"
                : "NextHearingDate";
            string caseSql = $"SELECT LastNapixSyncAt, LastNapixSyncStatus, LastNapixSyncError, PendDispStatus, {dateCol} AS NextHearingDate, EstName, ECourtsStage, ECourtsCourtNo, ECourtsJudge FROM {tableName} WHERE CaseID = @caseId OR (CNRNumber = @cnrNumber AND @cnrNumber IS NOT NULL AND @cnrNumber <> '')";

            var caseData = await conn.QueryFirstOrDefaultAsync(caseSql, new { caseId, cnrNumber });

            return Json(new
            {
                success = true,
                queueId = qRecord.QueueId,
                status = qRecord.Status,
                error = qRecord.LastError,
                processedAt = qRecord.ProcessedAt,
                attempts = qRecord.AttemptCount,
                caseData = caseData != null ? new
                {
                    lastSyncAt = caseData.LastNapixSyncAt,
                    syncStatus = caseData.LastNapixSyncStatus,
                    syncError = caseData.LastNapixSyncError,
                    pendDispStatus = caseData.PendDispStatus,
                    nextHearingDate = caseData.NextHearingDate != null ? ((DateTime)caseData.NextHearingDate).ToString("yyyy-MM-dd") : null,
                    estName = caseData.EstName,
                    stage = caseData.ECourtsStage,
                    courtNo = caseData.ECourtsCourtNo,
                    judge = caseData.ECourtsJudge
                } : null
            });
        }

        // GET: /ECourts/HighCourtCauseList
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> HighCourtCauseList(string? bench = null, string? division = null, string? appealType = null, string? q = null, DateTime? date = null)
        {
            var rawItems = await _ecourtsRepo.GetHighCourtCauseListAsync();
            var itemsList = rawItems.ToList();

            var vm = new HighCourtCauseListViewModel
            {
                SelectedDate = date ?? DateTime.Today,
                SelectedBench = bench,
                SelectedDivision = division,
                SelectedAppealType = appealType,
                SearchQuery = q,
                TotalRegisteredCases = itemsList.Count,
                SyncedCnrCount = itemsList.Count(i => !string.IsNullOrEmpty(i.CNRNumber)),
                ListedTodayCount = itemsList.Count(i => i.NextHearingDate.HasValue && i.NextHearingDate.Value.Date == DateTime.Today),
                ListedThisWeekCount = itemsList.Count(i => i.NextHearingDate.HasValue && i.NextHearingDate.Value.Date >= DateTime.Today && i.NextHearingDate.Value.Date <= DateTime.Today.AddDays(7))
            };

            vm.Divisions = itemsList.Select(i => i.DivisionName).Where(d => !string.IsNullOrEmpty(d)).Distinct().OrderBy(d => d).ToList()!;
            vm.Divisions.Insert(0, "All");

            var filtered = itemsList.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(bench) && bench != "All")
            {
                filtered = filtered.Where(i => i.HighCourtBench.Equals(bench, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(division) && division != "All")
            {
                filtered = filtered.Where(i => i.DivisionName != null && i.DivisionName.Equals(division, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(appealType) && appealType != "All")
            {
                filtered = filtered.Where(i => i.AppealType.Equals(appealType, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(q))
            {
                string term = q.Trim().ToLowerInvariant();
                filtered = filtered.Where(i =>
                    (i.MFANumber != null && i.MFANumber.ToLowerInvariant().Contains(term)) ||
                    (i.CNRNumber != null && i.CNRNumber.ToLowerInvariant().Contains(term)) ||
                    (i.ArisingMVCNo != null && i.ArisingMVCNo.ToLowerInvariant().Contains(term)) ||
                    (i.PetitionerName != null && i.PetitionerName.ToLowerInvariant().Contains(term)) ||
                    (i.RespondentName != null && i.RespondentName.ToLowerInvariant().Contains(term))
                );
            }

            vm.Items = filtered.ToList();
            return View(vm);
        }

        // GET: /ECourts/GetHighCourtCauselistBenches?estCode=KAHC01&causelistDate=13-08-2026
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> GetHighCourtCauselistBenches(string estCode = "KAHC01", string? causelistDate = null)
        {
            string dateStr = string.IsNullOrWhiteSpace(causelistDate) ? DateTime.Today.ToString("dd-MM-yyyy") : causelistDate;
            var result = await _napixService.GetHighCourtCauselistBenchesAsync(estCode, dateStr);
            if (result.HasValue) return Json(new { success = true, data = result.Value });
            return Json(new { success = false, message = "Unable to fetch High Court Causelist Benches." });
        }

        // GET: /ECourts/GetHighCourtCauselistDetails?estCode=KAHC01&benchId=...&causelistDate=13-08-2026
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> GetHighCourtCauselistDetails(string estCode = "KAHC01", string benchId = "", string? causelistDate = null)
        {
            string dateStr = string.IsNullOrWhiteSpace(causelistDate) ? DateTime.Today.ToString("dd-MM-yyyy") : causelistDate;
            var result = await _napixService.GetHighCourtCauselistDetailsAsync(estCode, benchId, dateStr);
            if (result.HasValue) return Json(new { success = true, data = result.Value });
            return Json(new { success = false, message = "Unable to fetch High Court Causelist Details." });
        }

        // GET: /ECourts/GetHighCourtShowCauselist?estCode=KAHC01&benchId=...&causelistId=...&causelistDate=13-08-2026
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> GetHighCourtShowCauselist(string estCode = "KAHC01", string benchId = "", string causelistId = "", string? causelistDate = null)
        {
            string dateStr = string.IsNullOrWhiteSpace(causelistDate) ? DateTime.Today.ToString("dd-MM-yyyy") : causelistDate;
            var result = await _napixService.GetHighCourtShowCauselistAsync(estCode, benchId, causelistId, dateStr);
            if (result.HasValue) return Json(new { success = true, data = result.Value });
            return Json(new { success = false, message = "Unable to fetch High Court Cause List." });
        }

        // GET: /ECourts/GetCaseBusiness?cnrNumber=...&date=...&isHighCourt=false
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> GetCaseBusiness(string cnrNumber, string date, bool isHighCourt = false)
        {
            if (string.IsNullOrWhiteSpace(cnrNumber))
            {
                return Json(new { success = false, message = "CNR Number is required." });
            }

            string clean = cnrNumber.Trim().ToUpperInvariant();
            if (!isHighCourt && (clean.StartsWith("KAHC", StringComparison.OrdinalIgnoreCase) || clean.Contains("HC")))
            {
                isHighCourt = true;
            }

            string effectiveModule = isHighCourt ? "HC" : "MVC";

            try
            {
                var result = await _napixService.GetCaseBusinessAsync(clean, date, isHighCourt, effectiveModule);
                if (result.HasValue && IsValidJsonData(result))
                {
                    return Json(new { success = true, data = result.Value });
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Live NAPIX GetCaseBusiness failed for CNR {CNR} on date {Date}", clean, date);
            }

            var lastErr = _napixService.GetLastModuleError(effectiveModule);
            string msg = !string.IsNullOrWhiteSpace(lastErr)
                ? $"eCourts NAPIX Live API: {lastErr}"
                : $"No live court proceedings returned by eCourts NAPIX API for date {date}.";
            return Json(new { success = false, message = msg });
        }

        // GET: /ECourts/GetStates
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> GetStates(bool isHighCourt = false)
        {
            try
            {
                var result = await _napixService.GetStatesAsync(isHighCourt);
                if (result.HasValue && IsValidJsonData(result))
                {
                    return Json(new { success = true, data = result.Value });
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Live NAPIX GetStatesAsync call failed — serving National Master Roster states.");
            }

            return Json(new { success = true, data = GetMasterStates() });
        }

        // GET: /ECourts/GetDistricts?stateCode=...
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> GetDistricts(string stateCode, bool isHighCourt = false)
        {
            if (string.IsNullOrWhiteSpace(stateCode))
            {
                stateCode = "29";
            }

            try
            {
                var result = await _napixService.GetDistrictsAsync(stateCode, isHighCourt);
                if (result.HasValue && IsValidJsonData(result))
                {
                    return Json(new { success = true, data = result.Value });
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Live NAPIX GetDistrictsAsync call failed for StateCode {StateCode} — serving Master Roster districts.", stateCode);
            }

            return Json(new { success = true, data = GetMasterDistricts(stateCode) });
        }

        // GET: /ECourts/GetCourtComplexes?stateCode=...&distCode=...
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> GetCourtComplexes(string stateCode, string distCode)
        {
            if (string.IsNullOrWhiteSpace(stateCode)) stateCode = "29";
            if (string.IsNullOrWhiteSpace(distCode)) distCode = "22";

            try
            {
                var result = await _napixService.GetCourtComplexesAsync(stateCode, distCode);
                if (result.HasValue && IsValidJsonData(result))
                {
                    return Json(new { success = true, data = result.Value });
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Live NAPIX GetCourtComplexesAsync call failed — serving Master Roster court establishments.");
            }

            return Json(new { success = true, data = GetMasterEstablishments(stateCode, distCode) });
        }

        // GET: /ECourts/GetHighCourtBenches?stateCode=...
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> GetHighCourtBenches(string stateCode)
        {
            if (string.IsNullOrWhiteSpace(stateCode)) stateCode = "29";

            try
            {
                var result = await _napixService.GetHighCourtBenchesAsync(stateCode);
                if (result.HasValue && IsValidJsonData(result))
                {
                    return Json(new { success = true, data = result.Value });
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Live NAPIX GetHighCourtBenchesAsync call failed — serving Master Roster benches.");
            }

            var masterBenches = new List<object>
            {
                new { bench_id = "1", bench_name = "Principal Bench, Bengaluru" },
                new { bench_id = "2", bench_name = "Dharwad Bench" },
                new { bench_id = "3", bench_name = "Kalaburagi Bench" }
            };
            return Json(new { success = true, data = masterBenches });
        }

        private static bool IsValidJsonData(JsonElement? element)
        {
            if (!element.HasValue) return false;
            var root = element.Value;
            if (root.ValueKind == JsonValueKind.Null || root.ValueKind == JsonValueKind.Undefined) return false;

            if (root.ValueKind == JsonValueKind.Object)
            {
                if (root.TryGetProperty("status_code", out var codeProp))
                {
                    string code = codeProp.GetString() ?? codeProp.GetRawText();
                    if (!string.IsNullOrEmpty(code) && code != "200") return false;
                }
                if (root.TryGetProperty("status", out var statusProp))
                {
                    string status = statusProp.GetString() ?? "";
                    if (status.Contains("INVALID", StringComparison.OrdinalIgnoreCase) ||
                        status.Contains("FAIL", StringComparison.OrdinalIgnoreCase) ||
                        status.Contains("ERROR", StringComparison.OrdinalIgnoreCase))
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        private static List<object> GetMasterStates()
        {
            return new List<object>
            {
                new { state_code = "29", state_name = "Karnataka" },
                new { state_code = "27", state_name = "Maharashtra" },
                new { state_code = "26", state_name = "Delhi" },
                new { state_code = "09", state_name = "Tamil Nadu" },
                new { state_code = "17", state_name = "Kerala" },
                new { state_code = "02", state_name = "Andhra Pradesh" },
                new { state_code = "33", state_name = "Telangana" },
                new { state_code = "28", state_name = "Goa" },
                new { state_code = "24", state_name = "Gujarat" },
                new { state_code = "08", state_name = "Rajasthan" },
                new { state_code = "15", state_name = "Uttar Pradesh" },
                new { state_code = "16", state_name = "West Bengal" },
                new { state_code = "14", state_name = "Madhya Pradesh" },
                new { state_code = "11", state_name = "Bihar" },
                new { state_code = "10", state_name = "Odisha" },
                new { state_code = "06", state_name = "Punjab" },
                new { state_code = "07", state_name = "Haryana" },
                new { state_code = "05", state_name = "Himachal Pradesh" },
                new { state_code = "30", state_name = "Jharkhand" },
                new { state_code = "18", state_name = "Meghalaya" },
                new { state_code = "19", state_name = "Manipur" },
                new { state_code = "20", state_name = "Tripura" }
            };
        }

        private static List<object> GetMasterDistricts(string stateCode)
        {
            if (stateCode == "29" || string.IsNullOrEmpty(stateCode))
            {
                return new List<object>
                {
                    new { dist_code = "22", dist_name = "Dharwad" },
                    new { dist_code = "15", dist_name = "Belagavi (Belgaum)" },
                    new { dist_code = "23", dist_name = "Uttara Kannada (Karwar)" },
                    new { dist_code = "21", dist_name = "Gadag" },
                    new { dist_code = "24", dist_name = "Haveri" },
                    new { dist_code = "17", dist_name = "Vijayapura (Bijapur)" },
                    new { dist_code = "16", dist_name = "Bagalkot" },
                    new { dist_code = "44", dist_name = "Kalaburagi (Gulbarga)" },
                    new { dist_code = "45", dist_name = "Bidar" },
                    new { dist_code = "42", dist_name = "Raichur" },
                    new { dist_code = "43", dist_name = "Koppal" },
                    new { dist_code = "41", dist_name = "Yadgir" },
                    new { dist_code = "25", dist_name = "Vijayanagara / Ballari" },
                    new { dist_code = "26", dist_name = "Chitradurga" },
                    new { dist_code = "27", dist_name = "Davanagere" },
                    new { dist_code = "28", dist_name = "Shivamogga" },
                    new { dist_code = "29", dist_name = "Udupi" },
                    new { dist_code = "30", dist_name = "Chikkamagaluru" },
                    new { dist_code = "31", dist_name = "Tumakuru" },
                    new { dist_code = "32", dist_name = "Bengaluru Urban" },
                    new { dist_code = "33", dist_name = "Mysuru" },
                    new { dist_code = "34", dist_name = "Mandya" },
                    new { dist_code = "35", dist_name = "Hassan" },
                    new { dist_code = "36", dist_name = "Kodagu" },
                    new { dist_code = "37", dist_name = "Chamarajanagara" },
                    new { dist_code = "38", dist_name = "Ramanagara" },
                    new { dist_code = "39", dist_name = "Chikkaballapura" },
                    new { dist_code = "40", dist_name = "Kolar" }
                };
            }

            return new List<object>
            {
                new { dist_code = "01", dist_name = "Central District" },
                new { dist_code = "02", dist_name = "North District" },
                new { dist_code = "03", dist_name = "South District" }
            };
        }

        private static List<object> GetMasterEstablishments(string stateCode, string distCode)
        {
            return new List<object>
            {
                new { est_code = "KADW01", court_est_name = "Principal District & Sessions Court, Dharwad [KADW01]" },
                new { est_code = "KADW02", court_est_name = "Addl Senior Civil Judge & JMFC, Hubballi [KADW02]" },
                new { est_code = "KABG01", court_est_name = "Principal District & Sessions Court, Belagavi [KABG01]" },
                new { est_code = "KAUK01", court_est_name = "Principal District & Sessions Court, Karwar [KAUK01]" },
                new { est_code = "KAGD01", court_est_name = "Principal District & Sessions Court, Gadag [KAGD01]" },
                new { est_code = "KAHA01", court_est_name = "Principal District & Sessions Court, Haveri [KAHA01]" },
                new { est_code = "KABJ01", court_est_name = "Principal District & Sessions Court, Vijayapura [KABJ01]" },
                new { est_code = "KABK01", court_est_name = "Principal District & Sessions Court, Bagalkot [KABK01]" },
                new { est_code = "KAHC01", court_est_name = "High Court of Karnataka, Principal Bench Bengaluru [KAHC01]" },
                new { est_code = "KAHC02", court_est_name = "High Court of Karnataka, Dharwad Bench [KAHC02]" },
                new { est_code = "KAHC03", court_est_name = "High Court of Karnataka, Kalaburagi Bench [KAHC03]" }
            };
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> DebugApiTypes(string estCode)
        {
            var types = await _napixService.GetCaseTypesAsync(estCode, true, null, null, "all");
            if (types.HasValue) return Content(types.Value.GetRawText(), "application/json");
            return Content("null");
        }

        // GET: /ECourts/GetCaseTypes?estCode=...&stateCode=...&distCode=...
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> GetCaseTypes(string? estCode, string? stateCode = null, string? distCode = null, bool isHighCourt = false)
        {
            try
            {
                var resultList = new List<object>();
                var addedCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                string cleanEstCode = !string.IsNullOrWhiteSpace(estCode) ? estCode.Trim() : "";

                // 1. Fast single live NAPIX Gateway query for specified establishment
                if (!string.IsNullOrEmpty(cleanEstCode))
                {
                    // 1. Live Gateway query for Civil case types (MVC, MACT, KID, LCA, OS, RA, EP, etc.)
                    var civilResult = await _napixService.GetCaseTypesAsync(cleanEstCode, isHighCourt, stateCode, distCode, "civil");
                    ExtractCaseTypesFromElement(civilResult, resultList, addedCodes);

                    // 2. Live Gateway query for All case types
                    var allResult = await _napixService.GetCaseTypesAsync(cleanEstCode, isHighCourt, stateCode, distCode, "all");
                    ExtractCaseTypesFromElement(allResult, resultList, addedCodes);
                }
                else if (!string.IsNullOrWhiteSpace(stateCode) && !string.IsNullOrWhiteSpace(distCode))
                {
                    var distCivil = await _napixService.GetCaseTypesAsync(null, isHighCourt, stateCode.Trim(), distCode.Trim(), "civil");
                    ExtractCaseTypesFromElement(distCivil, resultList, addedCodes);

                    var distAll = await _napixService.GetCaseTypesAsync(null, isHighCourt, stateCode.Trim(), distCode.Trim(), "all");
                    ExtractCaseTypesFromElement(distAll, resultList, addedCodes);
                }

                // 3. Port 78 Master Portal Case Types Roster (Identical to C:\xampp\htdocs\ecourts\api.php implementation)
                var master78 = new[]
                {
                    new { case_type_code = "3", type_name = "AA - Arbitration Application [Code: 3]" },
                    new { case_type_code = "1", type_name = "A.C. - Arbitration Cases [Code: 1]" },
                    new { case_type_code = "81", type_name = "AIR Misc. - ACCIDENT INFORMATION REPORT [Code: 81]" },
                    new { case_type_code = "6", type_name = "Appl.10(4)(A) - Application u/s 10(4)(A) [Code: 6]" },
                    new { case_type_code = "7", type_name = "Appl.33(2)b - Application u/s 33(2)b [Code: 7]" },
                    new { case_type_code = "8", type_name = "APPL.33(C)(2) [Code: 8]" },
                    new { case_type_code = "10", type_name = "APPLN - Application for Wakf Board [Code: 10]" },
                    new { case_type_code = "11", type_name = "Appln.u/s 33 A of I.D. Act [Code: 11]" },
                    new { case_type_code = "9", type_name = "Appl.u/s 11 - Application u/s 11 [Code: 9]" },
                    new { case_type_code = "2", type_name = "A.S. - Arbitration Suits [Code: 2]" },
                    new { case_type_code = "15", type_name = "Caveat [Code: 15]" },
                    new { case_type_code = "12", type_name = "C.C. - CRIMINAL CASES [Code: 12]" },
                    new { case_type_code = "80", type_name = "CIVIL APPEAL [Code: 80]" },
                    new { case_type_code = "13", type_name = "C.O.A. - Company Applications [Code: 13]" },
                    new { case_type_code = "87", type_name = "Complaint - Appln U/Sec 33-A of I D Act [Code: 87]" },
                    new { case_type_code = "14", type_name = "C.O.P. - Company Petitions [Code: 14]" },
                    new { case_type_code = "16", type_name = "Cr - Crime Case [Code: 16]" },
                    new { case_type_code = "17", type_name = "CRL.A - CRIMINAL MISC.APPEAL CASES [Code: 17]" },
                    new { case_type_code = "18", type_name = "CRL.M.A. - CRIMINAL MISC.APPEAL [Code: 18]" },
                    new { case_type_code = "19", type_name = "Crl.Misc. - CRIMINAL MISC.CASES [Code: 19]" },
                    new { case_type_code = "20", type_name = "CRL.R.P. - CRIMINAL REVISION PETITIONS [Code: 20]" },
                    new { case_type_code = "21", type_name = "E.A.T. - EDUCATION APPELLATE TRIBUNAL [Code: 21]" },
                    new { case_type_code = "86", type_name = "ECA - Employees Compensation Application [Code: 86]" },
                    new { case_type_code = "22", type_name = "ELEC.C - ELECTION PETITIONS [Code: 22]" },
                    new { case_type_code = "23", type_name = "EX - Execution Petition Under Order [Code: 23]" },
                    new { case_type_code = "24", type_name = "Ex.A. - Execution Appeals [Code: 24]" },
                    new { case_type_code = "26", type_name = "FDP - FINAL DECREE PROCEEDINGS [Code: 26]" },
                    new { case_type_code = "28", type_name = "G and W.C. - Guardian and Wards Cases [Code: 28]" },
                    new { case_type_code = "27", type_name = "G and WC - Appointment Of Guardian [Code: 27]" },
                    new { case_type_code = "29", type_name = "H.R.C. - House Rent Control Cases [Code: 29]" },
                    new { case_type_code = "30", type_name = "H.R.C.A. - House Rent Control Appeals [Code: 30]" },
                    new { case_type_code = "31", type_name = "I.C. - Insolvency Cases [Code: 31]" },
                    new { case_type_code = "34", type_name = "IDact-S10 - Under Sec 10 of ID Act [Code: 34]" },
                    new { case_type_code = "35", type_name = "IDact-S10(1)(d) - Industrial Disputes [Code: 35]" },
                    new { case_type_code = "32", type_name = "IDact-S.33 - Serial Applications Under Sec 33 [Code: 32]" },
                    new { case_type_code = "36", type_name = "IDact-S33(2)(b) - Approval Application [Code: 36]" },
                    new { case_type_code = "37", type_name = "IDact-S33(A) - Under Sec 33(A) of ID Act [Code: 37]" },
                    new { case_type_code = "33", type_name = "IDact-S.A - Complaints under Sec.A [Code: 33]" },
                    new { case_type_code = "38", type_name = "IID.1947,Sec.32 (A) [Code: 38]" },
                    new { case_type_code = "39", type_name = "Ind Emp-SO Act - Appeal under Industrial Employment [Code: 39]" },
                    new { case_type_code = "40", type_name = "J.C. - JUVENILE CASES [Code: 40]" },
                    new { case_type_code = "83", type_name = "KID - KARNATAKA INDUSTRIAL DISPUTE ACT [Code: 83]" },
                    new { case_type_code = "49", type_name = "LABOUR Misc - Miscellaneous Cases [Code: 49]" },
                    new { case_type_code = "42", type_name = "L.A.C. - Land Acquisition Cases [Code: 42]" },
                    new { case_type_code = "44", type_name = "LAC(APPL) - LAND ACQUISITION APPEAL [Code: 44]" },
                    new { case_type_code = "78", type_name = "L.D. - LONG CAUSE [Code: 78]" },
                    new { case_type_code = "43", type_name = "L.P.C. - LONG PENDING CASES [Code: 43]" },
                    new { case_type_code = "45", type_name = "M.A. - Miscellanuous Appeals [Code: 45]" },
                    new { case_type_code = "48", type_name = "MA(EAT) - Appeal Under Education Act [Code: 48]" },
                    new { case_type_code = "85", type_name = "MAT - Medical Appeal Tribunal [Code: 85]" },
                    new { case_type_code = "46", type_name = "M.C. - MATRIMONIAL CASES [Code: 46]" },
                    new { case_type_code = "50", type_name = "Misc.Appln. - Miscelleneous Application [Code: 50]" },
                    new { case_type_code = "47", type_name = "M.V.C. - ACCIDENT CLAIM CASES U/R MOTOR [Code: 47]" },
                    new { case_type_code = "51", type_name = "O.L. - OTHER LAW CASES [Code: 51]" },
                    new { case_type_code = "52", type_name = "O.S. - Original Suit [Code: 52]" },
                    new { case_type_code = "53", type_name = "P and SC - Probate and Succession Cases [Code: 53]" },
                    new { case_type_code = "54", type_name = "P and SCert - Petition For Succession Certificate [Code: 54]" },
                    new { case_type_code = "55", type_name = "P.C.R. - PRIVATE COMPLAINT REGISTER [Code: 55]" },
                    new { case_type_code = "56", type_name = "P.MIS. - Petition Filed Indigent Person [Code: 56]" },
                    new { case_type_code = "57", type_name = "R.A. - Regular Appeals [Code: 57]" },
                    new { case_type_code = "58", type_name = "R.C.(E) - CRIMINAL APPEAL [Code: 58]" },
                    new { case_type_code = "84", type_name = "Ref - Reference case [Code: 84]" },
                    new { case_type_code = "5", type_name = "Ref 10.1.C OF ID ACT - Reference cases [Code: 5]" },
                    new { case_type_code = "60", type_name = "REV - Revision Petitions [Code: 60]" },
                    new { case_type_code = "59", type_name = "R.E.V. (RENT) - Revision Petition Under Rent Control [Code: 59]" },
                    new { case_type_code = "62", type_name = "SC - SESSION CASES [Code: 62]" },
                    new { case_type_code = "61", type_name = "Small Cause - Small Cause Suit [Code: 61]" },
                    new { case_type_code = "63", type_name = "SPL.C - SPECIAL CASES [Code: 63]" },
                    new { case_type_code = "64", type_name = "SPL.C(A.C.Act) - SPECIAL CASES Anti Corruption [Code: 64]" },
                    new { case_type_code = "73", type_name = "Spl.Case IPC PCR - Special Case(IPC and PCR) [Code: 73]" },
                    new { case_type_code = "72", type_name = "SPL.CaseKPTCL - Special Case KPTCL [Code: 72]" },
                    new { case_type_code = "65", type_name = "SPL.C(CBI) - CBI CASES [Code: 65]" },
                    new { case_type_code = "66", type_name = "SPL.C(Corruption) - SPECIAL CASES (CORRUPTION) [Code: 66]" },
                    new { case_type_code = "67", type_name = "SPL.C(E.C.Act) - Special Essential Commodities [Code: 67]" },
                    new { case_type_code = "68", type_name = "SPL.CElect. - SPECIAL ELECT. CASES [Code: 68]" },
                    new { case_type_code = "69", type_name = "SPL.C(N.D.P.S.) - SPECIAL CASES (N.D.P.S. ACT) [Code: 69]" },
                    new { case_type_code = "70", type_name = "SPL.C(SC/ST Act) - SPECIAL CASES (SC/ST) PA.ACT [Code: 70]" },
                    new { case_type_code = "71", type_name = "SPL.C(SVC) - SVC CASES [Code: 71]" }
                };

                foreach (var m in master78)
                {
                    string key = $"{m.case_type_code}_{m.type_name}".ToLowerInvariant();
                    if (!addedCodes.Contains(key))
                    {
                        addedCodes.Add(key);
                        resultList.Add(m);
                    }
                }

                return Json(new { success = true, data = resultList });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetCaseTypes endpoint.");
                return Json(new { success = false, message = "Error loading case types: " + ex.Message });
            }
        }

        private static void ExtractCaseTypesFromElement(JsonElement? element, List<object> resultList, HashSet<string> addedCodes)
        {
            if (!element.HasValue) return;

            var root = element.Value;
            JsonElement itemsElem = root;

            if (root.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in root.EnumerateObject())
                {
                    if (prop.Value.ValueKind == JsonValueKind.Array)
                    {
                        itemsElem = prop.Value;
                        break;
                    }
                }
            }

            if (itemsElem.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in itemsElem.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.Object)
                    {
                        string? code = null;
                        string? name = null;

                        if (item.TryGetProperty("case_type_code", out var ctcProp)) code = ctcProp.GetString();
                        else if (item.TryGetProperty("case_type", out var ctProp)) code = ctProp.ToString();
                        else if (item.TryGetProperty("national_casetype_code", out var nctProp)) code = nctProp.ToString();
                        else if (item.TryGetProperty("code", out var cdProp)) code = cdProp.ToString();

                        if (item.TryGetProperty("type_name", out var tnProp)) name = tnProp.GetString();
                        else if (item.TryGetProperty("case_type_name", out var ctnProp)) name = ctnProp.GetString();
                        else if (item.TryGetProperty("name", out var nmProp)) name = nmProp.GetString();

                        if (!string.IsNullOrWhiteSpace(code))
                        {
                            string cleanCode = code.Trim();
                            string cleanName = string.IsNullOrWhiteSpace(name) ? cleanCode : name.Trim();
                            string key = $"{cleanCode}_{cleanName}".ToLowerInvariant();
                            if (!addedCodes.Contains(key))
                            {
                                addedCodes.Add(key);
                                resultList.Add(new { case_type_code = cleanCode, type_name = $"{cleanName} [Code: {cleanCode}]" });
                            }
                        }
                    }
                }
            }
        }

        private static List<string> ExtractEstCodesFromComplexes(JsonElement complexes)
        {
            var list = new List<string>();
            if (complexes.ValueKind != JsonValueKind.Object) return list;

            foreach (var complexProp in complexes.EnumerateObject())
            {
                if (complexProp.Value.ValueKind == JsonValueKind.Object)
                {
                    foreach (var innerProp in complexProp.Value.EnumerateObject())
                    {
                        if (innerProp.Name.StartsWith("establishment", StringComparison.OrdinalIgnoreCase) &&
                            innerProp.Value.ValueKind == JsonValueKind.Object &&
                            innerProp.Value.TryGetProperty("est_code", out var codeElem))
                        {
                            string? codeStr = codeElem.GetString();
                            if (!string.IsNullOrWhiteSpace(codeStr))
                            {
                                list.Add(codeStr.Trim());
                            }
                        }
                    }
                }
            }
            return list;
        }

        // GET: /ECourts/AutoDiscoverCNR?estCode=...&caseType=...&regNo=...&regYear=...
        [HttpGet]
        public async Task<IActionResult> AutoDiscoverCNR(string estCode, string caseType, string regNo, string regYear, bool isHighCourt = false, int? caseId = null, string? firNo = null, string? firYear = null, string? policeStation = null, string? partyName = null)
        {
            if (string.IsNullOrWhiteSpace(estCode) && !string.IsNullOrWhiteSpace(regNo) && caseId.HasValue && caseId.Value > 0)
            {
                var mCase = _caseRepo.GetCaseById(caseId.Value);
                if (mCase != null)
                {
                    estCode = !string.IsNullOrWhiteSpace(mCase.EstCode) ? mCase.EstCode : "KABK01";
                    caseType = !string.IsNullOrWhiteSpace(mCase.CaseTypeCode) ? mCase.CaseTypeCode : (!string.IsNullOrWhiteSpace(mCase.CaseType) && mCase.CaseType != "Pending" ? mCase.CaseType : "47");
                    if (string.IsNullOrWhiteSpace(regNo)) regNo = mCase.MVCNo.ToString();
                    if (string.IsNullOrWhiteSpace(regYear)) regYear = mCase.MVCYear.ToString();
                    if (string.IsNullOrWhiteSpace(firYear)) firYear = mCase.MVCYear.ToString();
                    if (string.IsNullOrWhiteSpace(partyName) && mCase.Petitioners != null && mCase.Petitioners.Count > 0)
                    {
                        partyName = mCase.Petitioners.FirstOrDefault(p => !string.IsNullOrWhiteSpace(p.PetitionerName))?.PetitionerName;
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(estCode) || string.IsNullOrWhiteSpace(caseType) ||
                string.IsNullOrWhiteSpace(regNo) || string.IsNullOrWhiteSpace(regYear))
            {
                return Json(new { success = false, message = "All case parameters (estCode, caseType, regNo, regYear) are required." });
            }

            // Strategy 1: Search by case registration number & year via dc-case-number-api
            string? cnr = await _napixService.DiscoverCnrAsync(estCode, caseType, regNo, regYear, isHighCourt);

            // Strategy 2: Search by FIR number if available on the accident case via dc-fir-number
            if (string.IsNullOrEmpty(cnr) && !string.IsNullOrWhiteSpace(firNo) && !string.IsNullOrWhiteSpace(firYear))
            {
                cnr = await _napixService.DiscoverCnrByFirAsync(estCode, policeStation ?? "1", firNo, firYear);
            }

            // Strategy 3: Search by Party Name (NWKRTC or Claimant) via dc-party-name-api
            if (string.IsNullOrEmpty(cnr))
            {
                string searchParty = !string.IsNullOrWhiteSpace(partyName) ? partyName : "NWKRTC";
                cnr = await _napixService.DiscoverCnrByPartyNameAsync(estCode, searchParty, regYear, "P", isHighCourt);
            }

            if (!string.IsNullOrEmpty(cnr))
            {
                string cleanCnr = cnr.Trim().ToUpperInvariant();

                // Persist into TRACKED_CASES
                if (int.TryParse(regYear, out int year))
                {
                    await _ecourtsRepo.SaveTrackedCaseAsync(new MVCCaseManagement.DAL.TrackedCaseModel
                    {
                        CNRNumber = cleanCnr,
                        EstCode = estCode,
                        CaseTypeCode = caseType,
                        RegNo = regNo,
                        RegYear = year,
                        IsHighCourt = isHighCourt
                    });
                }

                // If caseId is provided, immediately link CNR to database
                if (caseId.HasValue && caseId.Value > 0)
                {
                    using var conn = _db.GetConnection();
                    await conn.ExecuteAsync("UPDATE MVC_CASES SET CNRNumber = @cleanCnr, EstCode = @estCode, CaseTypeCode = @caseType WHERE CaseID = @caseId",
                        new { cleanCnr, estCode, caseType, caseId = caseId.Value });
                }

                return Json(new { success = true, cnr = cleanCnr, message = "CNR number successfully discovered & linked." });
            }

            return Json(new { success = false, message = "Case not found in eCourts live gateway." });
        }

        // POST: /ECourts/AutoLinkCnrInline
        [HttpPost]
        public async Task<IActionResult> AutoLinkCnrInline(int caseId, string? cnr = null, string module = "MVC")
        {
            if (caseId <= 0) return Json(new { success = false, message = "Invalid Case ID." });

            try
            {
                string? cleanCnr = cnr?.Trim().ToUpperInvariant();
                string? strategyUsed = null;

                if (string.IsNullOrEmpty(cleanCnr))
                {
                    // Look up case from DB to run discovery
                    if (module.Equals("MVC", StringComparison.OrdinalIgnoreCase))
                    {
                        var mCase = _caseRepo.GetCaseById(caseId);
                        if (mCase == null) return Json(new { success = false, message = "Case not found." });

                        string estCode = !string.IsNullOrWhiteSpace(mCase.EstCode) ? mCase.EstCode : "KABK01";
                        string caseType = !string.IsNullOrWhiteSpace(mCase.CaseTypeCode) ? mCase.CaseTypeCode : "47";
                        string rNo = mCase.MVCNo.ToString();
                        string rYr = mCase.MVCYear.ToString();

                        cleanCnr = await _napixService.DiscoverCnrAsync(estCode, caseType, rNo, rYr, false);
                        if (!string.IsNullOrEmpty(cleanCnr)) strategyUsed = "Case Registration Number";

                        if (string.IsNullOrEmpty(cleanCnr) && mCase.Petitioners != null && mCase.Petitioners.Count > 0)
                        {
                            var firstPet = mCase.Petitioners.FirstOrDefault(p => !string.IsNullOrWhiteSpace(p.PetitionerName))?.PetitionerName;
                            if (!string.IsNullOrWhiteSpace(firstPet))
                            {
                                cleanCnr = await _napixService.DiscoverCnrByPartyNameAsync(estCode, firstPet, rYr);
                                if (!string.IsNullOrEmpty(cleanCnr)) strategyUsed = "Petitioner Name (" + firstPet + ")";
                            }
                        }
                    }
                    else if (module.Equals("Labour", StringComparison.OrdinalIgnoreCase))
                    {
                        using var qConn = _db.GetConnection();
                        var lCase = await qConn.QueryFirstOrDefaultAsync<dynamic>("SELECT CaseID, CaseNumber, CaseYear, EstCode, PetitionerName, CaseType FROM LABOUR_CASES WHERE CaseID = @caseId", new { caseId });
                        if (lCase != null)
                        {
                            string estCode = !string.IsNullOrWhiteSpace((string?)lCase.EstCode) ? (string)lCase.EstCode : "KABK01";
                            string caseType = "47";
                            string rNo = (string?)lCase.CaseNumber ?? "";
                            string rYr = lCase.CaseYear != null ? lCase.CaseYear.ToString() : DateTime.Today.Year.ToString();

                            if (!string.IsNullOrEmpty(rNo))
                            {
                                cleanCnr = await _napixService.DiscoverCnrAsync(estCode, caseType, rNo, rYr, false, module: "Labour");
                                if (!string.IsNullOrEmpty(cleanCnr)) strategyUsed = "Case Registration Number";
                            }

                            if (string.IsNullOrEmpty(cleanCnr) && !string.IsNullOrWhiteSpace((string?)lCase.PetitionerName))
                            {
                                string pet = (string)lCase.PetitionerName;
                                cleanCnr = await _napixService.DiscoverCnrByPartyNameAsync(estCode, pet, rYr, module: "Labour");
                                if (!string.IsNullOrEmpty(cleanCnr)) strategyUsed = "Petitioner Name (" + pet + ")";
                            }
                        }
                    }
                    else if (module.StartsWith("Labour_WP", StringComparison.OrdinalIgnoreCase))
                    {
                        using var qConn = _db.GetConnection();
                        var lCase = await qConn.QueryFirstOrDefaultAsync<dynamic>("SELECT CaseID, CO_WP_CaseNumber, CO_WP_Year, CO_WP_HighCourtBench, PetitionerName FROM LABOUR_CASES WHERE CaseID = @caseId", new { caseId });
                        if (lCase != null)
                        {
                            string bench = (string?)lCase.CO_WP_HighCourtBench ?? "Bengaluru";
                            string estCode = bench.Contains("Dharwad", StringComparison.OrdinalIgnoreCase) ? "KAHC02" :
                                             bench.Contains("Kalaburagi", StringComparison.OrdinalIgnoreCase) || bench.Contains("Gulbarga", StringComparison.OrdinalIgnoreCase) ? "KAHC03" : "KAHC01";
                            string rNo = (string?)lCase.CO_WP_CaseNumber ?? "";
                            string rYr = lCase.CO_WP_Year != null ? lCase.CO_WP_Year.ToString() : DateTime.Today.Year.ToString();
                            if (!string.IsNullOrEmpty(rNo))
                            {
                                cleanCnr = await _napixService.DiscoverCnrAsync(estCode, "WP", rNo, rYr, true, module: "Labour");
                                if (!string.IsNullOrEmpty(cleanCnr)) strategyUsed = "High Court WP Case No";
                            }
                        }
                    }
                    else if (module.StartsWith("Labour_WA", StringComparison.OrdinalIgnoreCase))
                    {
                        using var qConn = _db.GetConnection();
                        var lCase = await qConn.QueryFirstOrDefaultAsync<dynamic>("SELECT CaseID, CO_WA_CaseNumber, CO_WA_Year, CO_WA_HighCourtBench, PetitionerName FROM LABOUR_CASES WHERE CaseID = @caseId", new { caseId });
                        if (lCase != null)
                        {
                            string bench = (string?)lCase.CO_WA_HighCourtBench ?? "Bengaluru";
                            string estCode = bench.Contains("Dharwad", StringComparison.OrdinalIgnoreCase) ? "KAHC02" :
                                             bench.Contains("Kalaburagi", StringComparison.OrdinalIgnoreCase) || bench.Contains("Gulbarga", StringComparison.OrdinalIgnoreCase) ? "KAHC03" : "KAHC01";
                            string rNo = (string?)lCase.CO_WA_CaseNumber ?? "";
                            string rYr = lCase.CO_WA_Year != null ? lCase.CO_WA_Year.ToString() : DateTime.Today.Year.ToString();
                            if (!string.IsNullOrEmpty(rNo))
                            {
                                cleanCnr = await _napixService.DiscoverCnrAsync(estCode, "WA", rNo, rYr, true, module: "Labour");
                                if (!string.IsNullOrEmpty(cleanCnr)) strategyUsed = "High Court WA Case No";
                            }
                        }
                    }
                    else if (module.StartsWith("Labour_Claimant", StringComparison.OrdinalIgnoreCase))
                    {
                        using var qConn = _db.GetConnection();
                        var lCase = await qConn.QueryFirstOrDefaultAsync<dynamic>("SELECT CaseID, CO_Claimant_CaseNumber, CO_Claimant_CaseYear, CO_Claimant_HighCourtBench, PetitionerName FROM LABOUR_CASES WHERE CaseID = @caseId", new { caseId });
                        if (lCase != null)
                        {
                            string bench = (string?)lCase.CO_Claimant_HighCourtBench ?? "Bengaluru";
                            string estCode = bench.Contains("Dharwad", StringComparison.OrdinalIgnoreCase) ? "KAHC02" :
                                             bench.Contains("Kalaburagi", StringComparison.OrdinalIgnoreCase) || bench.Contains("Gulbarga", StringComparison.OrdinalIgnoreCase) ? "KAHC03" : "KAHC01";
                            string rNo = (string?)lCase.CO_Claimant_CaseNumber ?? "";
                            string rYr = lCase.CO_Claimant_CaseYear != null ? lCase.CO_Claimant_CaseYear.ToString() : DateTime.Today.Year.ToString();
                            if (!string.IsNullOrEmpty(rNo))
                            {
                                cleanCnr = await _napixService.DiscoverCnrAsync(estCode, "WP", rNo, rYr, true, module: "Labour");
                                if (!string.IsNullOrEmpty(cleanCnr)) strategyUsed = "Claimant WP Case No";
                            }
                        }
                    }
                }

                if (string.IsNullOrEmpty(cleanCnr))
                {
                    return Json(new { success = false, message = "Could not discover CNR automatically. Please specify CNR manually." });
                }

                using var conn = _db.GetConnection();
                if (module.Equals("MVC", StringComparison.OrdinalIgnoreCase))
                {
                    await conn.ExecuteAsync("UPDATE MVC_CASES SET CNRNumber = @cleanCnr WHERE CaseID = @caseId", new { cleanCnr, caseId });
                }
                else if (module.Equals("Labour", StringComparison.OrdinalIgnoreCase))
                {
                    await conn.ExecuteAsync("UPDATE LABOUR_CASES SET CNRNumber = @cleanCnr, ModifiedDate = GETDATE() WHERE CaseID = @caseId; UPDATE LABOUR_SERVICE_MATTERS SET CNRNumber = @cleanCnr, ModifiedDate = GETDATE() WHERE ServiceID = @caseId OR CaseID = @caseId;", new { cleanCnr, caseId });
                }
                else if (module.Equals("Labour_WP", StringComparison.OrdinalIgnoreCase))
                {
                    await conn.ExecuteAsync("UPDATE LABOUR_CASES SET CO_WP_CNRNumber = @cleanCnr, ModifiedDate = GETDATE() WHERE CaseID = @caseId", new { cleanCnr, caseId });
                }
                else if (module.Equals("Labour_WA", StringComparison.OrdinalIgnoreCase))
                {
                    await conn.ExecuteAsync("UPDATE LABOUR_CASES SET CO_WA_CNRNumber = @cleanCnr, ModifiedDate = GETDATE() WHERE CaseID = @caseId", new { cleanCnr, caseId });
                }
                else if (module.Equals("Labour_Claimant", StringComparison.OrdinalIgnoreCase))
                {
                    await conn.ExecuteAsync("UPDATE LABOUR_CASES SET CO_Claimant_CNRNumber = @cleanCnr, ModifiedDate = GETDATE() WHERE CaseID = @caseId", new { cleanCnr, caseId });
                }

                return Json(new { success = true, cnr = cleanCnr, strategy = strategyUsed, message = $"CNR {cleanCnr} successfully linked to case!" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error linking CNR inline for case {CaseId}", caseId);
                return Json(new { success = false, message = ex.Message });
            }
        }

        // GET: /ECourts/GetCnrDetails?cnrNumber=...&caseId=...&appealId=...&module=...
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> GetCnrDetails(string cnrNumber, int? caseId = null, bool isHighCourt = false, string? appealId = null, string? module = null)
        {
            if (string.IsNullOrWhiteSpace(module))
            {
                var referer = Request.Headers["Referer"].ToString();
                if (referer.Contains("/Labour", StringComparison.OrdinalIgnoreCase) ||
                    (!string.IsNullOrEmpty(appealId) && (appealId.StartsWith("corp_", StringComparison.OrdinalIgnoreCase) || appealId.Contains("claimant_wp", StringComparison.OrdinalIgnoreCase) || appealId.Contains("labour", StringComparison.OrdinalIgnoreCase))))
                {
                    module = "Labour";
                }
                else if (referer.Contains("/OtherCourts", StringComparison.OrdinalIgnoreCase) || referer.Contains("/Home/Other", StringComparison.OrdinalIgnoreCase) || referer.Contains("/Home/OS", StringComparison.OrdinalIgnoreCase) || referer.Contains("/Home/ECA", StringComparison.OrdinalIgnoreCase) || referer.Contains("/Home/PSC", StringComparison.OrdinalIgnoreCase) || referer.Contains("/Home/CC", StringComparison.OrdinalIgnoreCase) || referer.Contains("/Home/LAC", StringComparison.OrdinalIgnoreCase) || referer.Contains("/Home/Consumer", StringComparison.OrdinalIgnoreCase))
                {
                    module = "OtherCourts";
                }
                else
                {
                    module = "MVC";
                }
            }
            string norm = NapixQuotaService.NormalizeModule(module);

            bool isLabour = norm.Equals("Labour", StringComparison.OrdinalIgnoreCase);
            bool isOtherCourts = norm.Equals("OtherCourts", StringComparison.OrdinalIgnoreCase);

            if (string.IsNullOrWhiteSpace(cnrNumber) && (!caseId.HasValue || caseId.Value <= 0))
            {
                return Json(new { success = false, message = "CNR Number or Case ID is required." });
            }

            if (string.IsNullOrWhiteSpace(cnrNumber) && caseId.HasValue && caseId.Value > 0)
            {
                if (isOtherCourts)
                {
                    try
                    {
                        using var oConn = _db.GetConnection();
                        cnrNumber = oConn.QueryFirstOrDefault<string>("SELECT CNRNumber FROM OTHER_CASES WHERE CaseID = @caseId", new { caseId = caseId.Value });
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error resolving Other Courts CNR for Case ID {CaseID}", caseId.Value);
                    }
                }
                else if (isLabour)
                {
                    try
                    {
                        using var lConn = _db.GetConnection();
                        var lCase = lConn.QueryFirstOrDefault("SELECT TOP 1 * FROM LABOUR_CASES WHERE CaseID = @caseId", new { caseId = caseId.Value });
                        if (lCase != null)
                        {
                            cnrNumber = new[] { (string?)lCase.CO_WP_CNRNumber, (string?)lCase.CO_WA_CNRNumber, (string?)lCase.CO_Claimant_CNRNumber, (string?)lCase.CNRNumber }
                                .FirstOrDefault(s => !string.IsNullOrWhiteSpace(s))?.Trim();
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error resolving Labour CNR for Case ID {CaseID}", caseId.Value);
                    }
                }
                else
                {
                    try
                    {
                        var linkedCase = _caseRepo.GetCaseById(caseId.Value);
                        if (linkedCase != null)
                        {
                            if (!string.IsNullOrWhiteSpace(linkedCase.CNRNumber))
                            {
                                cnrNumber = linkedCase.CNRNumber;
                            }
                            else
                            {
                                string estCode = !string.IsNullOrWhiteSpace(linkedCase.EstCode) ? linkedCase.EstCode : "KABK01";
                                string cType = !string.IsNullOrWhiteSpace(linkedCase.CaseTypeCode) ? linkedCase.CaseTypeCode : "47";
                                string rNo = Convert.ToString(linkedCase.MVCNo) ?? "";
                                string rYr = Convert.ToString(linkedCase.MVCYear) ?? "";
                                if (!string.IsNullOrEmpty(rNo) && !string.IsNullOrEmpty(rYr))
                                {
                                    string? discovered = await _napixService.DiscoverCnrAsync(estCode, cType, rNo, rYr, isHighCourt, module: norm);
                                    if (!string.IsNullOrEmpty(discovered))
                                    {
                                        cnrNumber = discovered;
                                        using var updConn = _db.GetConnection();
                                        updConn.Execute("UPDATE MVC_CASES SET CNRNumber = @discovered, EstCode = COALESCE(NULLIF(EstCode, ''), @estCode) WHERE CaseID = @caseId",
                                            new { discovered, estCode, caseId = caseId.Value });
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error resolving or auto-discovering CNR for Case ID {CaseID}", caseId.Value);
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(cnrNumber))
            {
                return Json(new { success = false, message = "CNR Number could not be resolved for this case. Please verify Case No, Year, and court establishment." });
            }

            string cleanCnr = cnrNumber.Trim().ToUpperInvariant();
            bool isHc = isHighCourt || cleanCnr.StartsWith("HC", StringComparison.OrdinalIgnoreCase) || (cleanCnr.Length >= 4 && cleanCnr.Substring(2, 2).Equals("HC", StringComparison.OrdinalIgnoreCase));

            List<Dictionary<string, string?>> historyList = new List<Dictionary<string, string?>>();
            List<Dictionary<string, string?>> ordersList = new List<Dictionary<string, string?>>();
            List<Dictionary<string, string?>> iaList = new List<Dictionary<string, string?>>();
            List<Dictionary<string, string?>> processList = new List<Dictionary<string, string?>>();
            List<Dictionary<string, string?>> actsList = new List<Dictionary<string, string?>>();
            List<Dictionary<string, string?>> extraPartiesList = new List<Dictionary<string, string?>>();
            List<Dictionary<string, string?>> objectionsList = new List<Dictionary<string, string?>>();
            List<Dictionary<string, string?>> transfersList = new List<Dictionary<string, string?>>();
            List<Dictionary<string, string?>> linkedCasesList = new List<Dictionary<string, string?>>();
            dynamic? parsed = null;
            JsonElement? rawResult = null;

            int? numericAppealId = null;
            if (!string.IsNullOrEmpty(appealId))
            {
                var cleanAid = appealId.Replace("_claimant", "").Trim();
                if (int.TryParse(cleanAid, out var parsedAid)) numericAppealId = parsedAid;
            }

            if (!isLabour && numericAppealId.HasValue && numericAppealId.Value > 0)
            {
                try
                {
                    using var conn = _db.GetConnection();
                    var app = conn.QueryFirstOrDefault("SELECT CaseID FROM APPEAL_DETAILS WHERE AppealID = @appealId", new { appealId = numericAppealId.Value });
                    if (app != null)
                    {
                        if (!caseId.HasValue || caseId.Value <= 0) caseId = Convert.ToInt32(app.CaseID);
                    }
                }
                catch { }
            }

            // Early resolve caseId from cleanCnr if not supplied by caller
            if (!caseId.HasValue || caseId.Value <= 0)
            {
                try
                {
                    using var conn = _db.GetConnection();
                    if (isLabour)
                    {
                        int lid = conn.ExecuteScalar<int>(@"
                            SELECT TOP 1 CaseID FROM LABOUR_CASES 
                            WHERE (REPLACE(REPLACE(CO_WP_CNRNumber, '-', ''), ' ', '') = @cleanCnr AND LEN(CO_WP_CNRNumber) >= 10)
                               OR (REPLACE(REPLACE(CO_WA_CNRNumber, '-', ''), ' ', '') = @cleanCnr AND LEN(CO_WA_CNRNumber) >= 10)
                               OR (REPLACE(REPLACE(CO_Claimant_CNRNumber, '-', ''), ' ', '') = @cleanCnr AND LEN(CO_Claimant_CNRNumber) >= 10)
                               OR (REPLACE(REPLACE(CNRNumber, '-', ''), ' ', '') = @cleanCnr AND LEN(CNRNumber) >= 10)
                            ORDER BY CaseID DESC", new { cleanCnr });
                        if (lid > 0) caseId = lid;
                    }
                    else
                    {
                        if (isHc)
                        {
                            int aid = conn.ExecuteScalar<int>(@"
                                SELECT TOP 1 CaseID FROM APPEAL_DETAILS 
                                WHERE (REPLACE(REPLACE(CorpMFACNRNumber, '-', ''), ' ', '') = @cleanCnr AND LEN(CorpMFACNRNumber) >= 10)
                                   OR (REPLACE(REPLACE(ClaimantMFACNRNumber, '-', ''), ' ', '') = @cleanCnr AND LEN(ClaimantMFACNRNumber) >= 10)
                                   OR (REPLACE(REPLACE(CNRNumber, '-', ''), ' ', '') = @cleanCnr AND LEN(CNRNumber) >= 10)
                                ORDER BY AppealID DESC", new { cleanCnr });
                            if (aid > 0) caseId = aid;
                        }
                        if (!caseId.HasValue || caseId.Value <= 0)
                        {
                            int cid = conn.ExecuteScalar<int>(@"
                                SELECT TOP 1 CaseID FROM MVC_CASES 
                                WHERE REPLACE(REPLACE(CNRNumber, '-', ''), ' ', '') = @cleanCnr AND LEN(CNRNumber) >= 10
                                ORDER BY CASE WHEN TRY_CAST(RIGHT(@cleanCnr, 4) AS INT) = MVCYear THEN 0 ELSE 1 END, CaseID DESC", 
                                new { cleanCnr });
                            if (cid > 0) caseId = cid;
                        }
                        if (!caseId.HasValue || caseId.Value <= 0)
                        {
                            int aid = conn.ExecuteScalar<int>(@"
                                SELECT TOP 1 CaseID FROM APPEAL_DETAILS 
                                WHERE (REPLACE(REPLACE(CorpMFACNRNumber, '-', ''), ' ', '') = @cleanCnr AND LEN(CorpMFACNRNumber) >= 10)
                                   OR (REPLACE(REPLACE(ClaimantMFACNRNumber, '-', ''), ' ', '') = @cleanCnr AND LEN(ClaimantMFACNRNumber) >= 10)
                                   OR (REPLACE(REPLACE(CNRNumber, '-', ''), ' ', '') = @cleanCnr AND LEN(CNRNumber) >= 10)
                                ORDER BY AppealID DESC", new { cleanCnr });
                            if (aid > 0) { caseId = aid; isHc = true; }
                        }
                    }
                }
                catch { }
            }

            var result = await _napixService.GetCnrDetailsAsync(cleanCnr, isHc, module: norm);

            // Auto-heal 15-character truncated CNR numbers (e.g. missing 4th digit of year)
            if (!result.HasValue && cleanCnr.Length == 15)
            {
                try
                {
                    MVCCaseViewModel? linkedCase = null;
                    if (caseId.HasValue && caseId.Value > 0) linkedCase = _caseRepo.GetCaseById(caseId.Value);

                    List<string> candidateCnrs = new List<string>();
                    if (linkedCase != null && linkedCase.MVCYear >= 2000)
                    {
                        candidateCnrs.Add(cleanCnr + (linkedCase.MVCYear % 10));
                    }
                    if (cleanCnr.EndsWith("202", StringComparison.OrdinalIgnoreCase))
                    {
                        foreach (var yDigit in new[] { "6", "5", "4", "3", "2", "1", "0" })
                        {
                            var cand = cleanCnr + yDigit;
                            if (!candidateCnrs.Contains(cand)) candidateCnrs.Add(cand);
                        }
                    }

                    foreach (var candCnr in candidateCnrs)
                    {
                        var candResult = await _napixService.GetCnrDetailsAsync(candCnr, isHc, module: norm);
                        if (candResult.HasValue)
                        {
                            result = candResult;
                            cleanCnr = candCnr;
                            _logger.LogInformation("[GetCnrDetails] Auto-healed 15-character CNR to {RepairedCNR}", cleanCnr);

                            if (caseId.HasValue && caseId.Value > 0)
                            {
                                using var updConn = _db.GetConnection();
                                string est = cleanCnr.Length >= 6 ? cleanCnr.Substring(0, 6) : "KABK01";
                                updConn.Execute("UPDATE MVC_CASES SET CNRNumber = @repairedCnr, EstCode = COALESCE(NULLIF(EstCode, ''), @est) WHERE CaseID = @caseId", 
                                    new { repairedCnr = cleanCnr, est, caseId = caseId.Value });
                            }
                            break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Exception during 15-character CNR auto-repair attempt for {CNR}", cleanCnr);
                }
            }

            // If direct CNR lookup returned no result and we don't already have a valid 16-character CNR, try discovering CNR via case number/year/est
            if (!result.HasValue && (string.IsNullOrWhiteSpace(cleanCnr) || cleanCnr.Length != 16))
            {
                try
                {
                    MVCCaseViewModel? mvcCaseForDisc = null;
                    if (caseId.HasValue && caseId.Value > 0)
                    {
                        mvcCaseForDisc = _caseRepo.GetCaseById(caseId.Value);
                    }
                    if (mvcCaseForDisc == null && cleanCnr.Length >= 6)
                    {
                        using var discConn = _db.GetConnection();
                        int foundDiscId = discConn.ExecuteScalar<int>(@"
                            SELECT TOP 1 CaseID FROM MVC_CASES 
                            WHERE CNRNumber = @cleanCnr 
                            OR EstCode + RIGHT('000000' + CAST(MVCNo AS VARCHAR), 6) + CAST(MVCYear AS VARCHAR) = @cleanCnr", 
                            new { cleanCnr });
                        if (foundDiscId > 0) mvcCaseForDisc = _caseRepo.GetCaseById(foundDiscId);
                    }

                    if (mvcCaseForDisc != null)
                    {
                        string estCode = !string.IsNullOrWhiteSpace(mvcCaseForDisc.EstCode) 
                            ? mvcCaseForDisc.EstCode 
                            : (cleanCnr.Length >= 6 ? cleanCnr.Substring(0, 6) : "KABK01");
                        string cType = !string.IsNullOrWhiteSpace(mvcCaseForDisc.CaseTypeCode) 
                            ? mvcCaseForDisc.CaseTypeCode 
                            : (!string.IsNullOrWhiteSpace(mvcCaseForDisc.CaseType) && 
                               !mvcCaseForDisc.CaseType.Equals("Pending", StringComparison.OrdinalIgnoreCase) && 
                               !mvcCaseForDisc.CaseType.Equals("Disposed", StringComparison.OrdinalIgnoreCase) 
                                ? mvcCaseForDisc.CaseType 
                                : (isHc ? "MFA" : "47"));
                        string rNo = Convert.ToString(mvcCaseForDisc.MVCNo) ?? "";
                        string rYr = Convert.ToString(mvcCaseForDisc.MVCYear) ?? "";
                        if (!string.IsNullOrEmpty(rNo) && !string.IsNullOrEmpty(rYr))
                        {
                            string? discovered = await _napixService.DiscoverCnrAsync(estCode, cType, rNo, rYr, isHc, module: norm);
                            if (!string.IsNullOrEmpty(discovered))
                            {
                                string discClean = discovered.Trim().ToUpperInvariant();
                                var discResult = await _napixService.GetCnrDetailsAsync(discClean, isHc, module: norm);
                                if (discResult.HasValue)
                                {
                                    result = discResult;
                                    cleanCnr = discClean;
                                    if (caseId.HasValue && caseId.Value > 0)
                                    {
                                        using var updConn = _db.GetConnection();
                                        updConn.Execute("UPDATE MVC_CASES SET CNRNumber = @discClean WHERE CaseID = @caseId", 
                                            new { discClean, caseId = caseId.Value });
                                    }
                                }
                            }
                        }
                    }
                }
                catch { }
            }

            if (result.HasValue)
            {
                rawResult = result.Value;
                parsed = ExtractCnrSummary(result.Value);
                historyList = ExtractHistoryArray(result.Value);
                ordersList = ExtractOrdersArray(result.Value);
                iaList = ExtractIaArray(result.Value);
                processList = ExtractProcessArray(result.Value);
                actsList = ExtractActsArray(result.Value);
                extraPartiesList = ExtractExtraPartiesArray(result.Value);
                objectionsList = ExtractObjectionsArray(result.Value);
                transfersList = ExtractTransfersArray(result.Value);
                linkedCasesList = ExtractLinkedCasesArray(result.Value);

                try
                {
                    string? liveCourtHall = parsed?.court_no_judge ?? parsed?.court_no;
                    string? liveStage = parsed?.stage;
                    string? nextDateStr = parsed?.next_hearing_date;
                    DateTime? nextDt = null;

                    string[] dateFormats = { "dd-MM-yyyy", "yyyy-MM-dd", "d-M-yyyy", "dd/MM/yyyy", "d/M/yyyy", "yyyy/MM/dd" };
                    if (!string.IsNullOrWhiteSpace(nextDateStr) &&
                        DateTime.TryParseExact(nextDateStr.Trim(), dateFormats, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var dValExact))
                    {
                        nextDt = dValExact;
                    }
                    else if (!string.IsNullOrWhiteSpace(nextDateStr) && DateTime.TryParse(nextDateStr, out var dVal))
                    {
                        nextDt = dVal;
                    }

                    if (!nextDt.HasValue && historyList.Count > 0)
                    {
                        foreach (var h in historyList)
                        {
                            foreach (var key in new[] { "next_date", "business_date", "hearing_date", "date" })
                            {
                                if (h.TryGetValue(key, out var candDate) && !string.IsNullOrWhiteSpace(candDate))
                                {
                                    if (DateTime.TryParseExact(candDate.Trim(), dateFormats, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var parsedCand))
                                    {
                                        if (!nextDt.HasValue || parsedCand > nextDt.Value)
                                            nextDt = parsedCand;
                                    }
                                    else if (DateTime.TryParse(candDate, out var parsedCandFallback))
                                    {
                                        if (!nextDt.HasValue || parsedCandFallback > nextDt.Value)
                                            nextDt = parsedCandFallback;
                                    }
                                }
                            }
                        }
                    }

                    DateTime? decDt = null;
                    if (!string.IsNullOrWhiteSpace(parsed?.decision_date) &&
                        !parsed.decision_date.Equals("Pending", StringComparison.OrdinalIgnoreCase) &&
                        !parsed.decision_date.Equals("—"))
                    {
                        if (DateTime.TryParseExact(parsed.decision_date.Trim(), dateFormats, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out DateTime decValExact))
                        {
                            decDt = decValExact;
                        }
                        else if (DateTime.TryParse(parsed.decision_date, out DateTime decVal))
                        {
                            decDt = decVal;
                        }
                    }

                    if (string.Equals(nextDateStr, "Disposed", StringComparison.OrdinalIgnoreCase) || 
                        string.Equals(parsed?.case_status, "Disposed", StringComparison.OrdinalIgnoreCase) ||
                        decDt.HasValue)
                    {
                        if (string.IsNullOrWhiteSpace(liveStage) || liveStage == "—") liveStage = "Disposed";
                    }

                    await _ecourtsRepo.SyncLiveCaseDataAsync(cleanCnr, nextDt, liveStage, liveCourtHall, parsed?.case_status, caseId, null, numericAppealId, decDt, parsed?.establishment_name);
                }
                catch { }
            }

            // dc-cnr-api embeds all interim and final orders in the response payload.
            // Orders are extracted directly into ordersList via ExtractOrdersArray(result.Value).

            // Only synthesize an order entry for the final disposal/judgment date (not routine hearings)
            // Routine hearing dates (EVIDENCE, NOTICE, SUMMONS, etc.) do NOT have downloadable orders
            if (parsed != null && !string.IsNullOrWhiteSpace(parsed.decision_date) &&
                !parsed.decision_date.Equals("Pending", StringComparison.OrdinalIgnoreCase) &&
                !parsed.decision_date.Equals("—"))
            {
                string decDate = parsed.decision_date;
                bool hasDecOrder = ordersList.Any(o =>
                    (o.GetValueOrDefault("order_date") == decDate || o.GetValueOrDefault("date") == decDate));
                if (!hasDecOrder)
                {
                    string orderNo2 = (ordersList.Count + 1).ToString();
                    string disposalTitle = !string.IsNullOrWhiteSpace(parsed.nature_of_disposal) ? parsed.nature_of_disposal : "Final Judgment / Award";
                    string orderPdfProxy = $"/ECourts/ViewOrderPdf?cnr={cleanCnr}&orderNo={orderNo2}&date={Uri.EscapeDataString(decDate)}&title={Uri.EscapeDataString(disposalTitle)}";
                    ordersList.Add(new Dictionary<string, string?>
                    {
                        { "order_number", orderNo2 },
                        { "order_no", orderNo2 },
                        { "order_date", decDate },
                        { "order_details", disposalTitle },
                        { "order_type", "Judgment / Final Order Copy" },
                        { "pdf_path", orderPdfProxy },
                        { "order_pdf_url", orderPdfProxy },
                        { "pdf_url", orderPdfProxy },
                        { "judge_name", parsed.court_no_judge ?? parsed.judge ?? "Presiding Officer" }
                    });
                }
            }

            CnrSummaryModel? summaryObj = parsed as CnrSummaryModel;
            bool hasValidSummaryData = summaryObj != null &&
                (!string.IsNullOrWhiteSpace(summaryObj.petitioner) ||
                 (!string.IsNullOrWhiteSpace(summaryObj.cnr_number) && summaryObj.cnr_number != cleanCnr) ||
                 !string.IsNullOrWhiteSpace(summaryObj.filing_date) ||
                 !string.IsNullOrWhiteSpace(summaryObj.first_hearing_date) ||
                 !string.IsNullOrWhiteSpace(summaryObj.stage) ||
                 !string.IsNullOrWhiteSpace(summaryObj.court_no_judge) ||
                 !string.IsNullOrWhiteSpace(summaryObj.judge));

            if (!hasValidSummaryData && historyList.Count == 0 && ordersList.Count == 0)
            {
                var lastErr = _napixService.GetLastModuleError(norm);
                string msg = !string.IsNullOrWhiteSpace(lastErr)
                    ? $"eCourts NAPIX Live API: {lastErr}"
                    : $"No live records found on eCourts Gateway for CNR: {cleanCnr}.";
                return Json(new {
                    success = false,
                    cnr = cleanCnr,
                    message = msg,
                    gateway_status = lastErr,
                    orders = new List<object>(),
                    history = new List<object>(),
                    ia_filings = new List<object>(),
                    processes = new List<object>()
                });
            }

            return Json(new {
                success = true,
                cnr = cleanCnr,
                data = parsed,
                raw = rawResult,
                history = historyList,
                orders = ordersList,
                ia_filings = iaList,
                processes = processList,
                acts = actsList,
                extra_parties = extraPartiesList,
                objections = objectionsList,
                transfers = transfersList,
                linked_cases = linkedCasesList,
                message = "Live eCourts CNR details, history, and orders fetched successfully from NAPIX API."
            });
        }

        // GET: /ECourts/GetCnrRaw?cnrNumber=...&isHighCourt=...&module=... — returns the raw NAPIX response
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> GetCnrRaw(string cnrNumber, bool isHighCourt = false, string? module = null)
        {
            if (string.IsNullOrWhiteSpace(cnrNumber))
                return Json(new { success = false, message = "CNR required." });

            if (string.IsNullOrWhiteSpace(module))
            {
                var referer = Request.Headers["Referer"].ToString();
                module = referer.Contains("/Labour", StringComparison.OrdinalIgnoreCase) ? "Labour" :
                         (referer.Contains("/OtherCourts", StringComparison.OrdinalIgnoreCase) || referer.Contains("/Home/Other", StringComparison.OrdinalIgnoreCase)) ? "OtherCourts" : "MVC";
            }
            string norm = NapixQuotaService.NormalizeModule(module);

            string cleanCnr = cnrNumber.Trim().ToUpperInvariant();
            bool isHc = isHighCourt || cleanCnr.StartsWith("HC", StringComparison.OrdinalIgnoreCase) || (cleanCnr.Length >= 4 && cleanCnr.Substring(2, 2).Equals("HC", StringComparison.OrdinalIgnoreCase));
            var result = await _napixService.GetCnrDetailsAsync(cleanCnr, isHc, module: norm);
            if (result.HasValue)
            {
                var historyItems = ExtractHistoryArray(result.Value);
                var ordersItems = ExtractOrdersArray(result.Value);
                string decryptedStatus = "";
                if (result.Value.ValueKind == System.Text.Json.JsonValueKind.Object && result.Value.TryGetProperty("status", out var stProp))
                {
                    decryptedStatus = stProp.GetString() ?? "";
                }
                return Json(new {
                    success = true,
                    raw_json = result.Value.ToString(),
                    decrypted_status = decryptedStatus,
                    history_count = historyItems.Count,
                    history = historyItems,
                    orders_count = ordersItems.Count,
                    orders = ordersItems
                });
            }

            var lastErr = _napixService.GetLastModuleError(norm);
            return Json(new {
                success = false,
                cnr = cleanCnr,
                module = norm,
                diagnostic = lastErr ?? "No live record returned by eCourts Gateway."
            });
        }

        public class CnrSummaryModel
        {
            public string? cnr_number { get; set; }
            public string? case_type { get; set; }
            public string? case_number { get; set; }
            public string? registration_date { get; set; }
            public string? petitioner_advocate { get; set; }
            public string? respondent_advocate { get; set; }
            public string? establishment_name { get; set; }
            public string? next_hearing_date { get; set; }
            public string? first_hearing_date { get; set; }
            public string? stage { get; set; }
            public string? petitioner { get; set; }
            public string? respondent { get; set; }
            public string? judge { get; set; }
            public string? court_no { get; set; }
            public string? court_no_judge { get; set; }
            public string? filing_date { get; set; }
            public string? decision_date { get; set; }
            public string? nature_of_disposal { get; set; }
            public string? case_status { get; set; }
            public string? transfer_est { get; set; }
            public string? transfer_date { get; set; }
            public string? transfer_cino { get; set; }
            public string? extra_party { get; set; }
        }



        /// <summary>
        /// Deeply traverses the raw NAPIX CNR JSON response and extracts
        /// the LATEST next_hearing_date, court_stage, petitioner, respondent, judge.
        /// Collects ALL candidate dates and returns the maximum to avoid returning
        /// the first_hearing_date instead of the actual next/upcoming hearing date.
        /// </summary>
        private static CnrSummaryModel ExtractCnrSummary(System.Text.Json.JsonElement root)
        {
            // Collect ALL candidates for next hearing date — we'll take the LATEST
            var nextDateCandidates = new List<string>();
            string? stage = null;
            string? lastStage = null;  // stage from last history entry
            string? petitioner = null;
            string? respondent = null;
            string? petitionerAdv = null;
            string? respondentAdv = null;
            string? judge = null;
            string? coram = null;
            string? courtNo = null;
            string? courtNoJudge = null;  // e.g. "293-I ADDL SENIOR CIVIL JUDGE AND JMFC HUBLI"
            string? filingDate = null;
            string? firstHearingDate = null;
            string? decisionDate = null;
            string? natureOfDisposal = null;
            string? caseStatus = null;
            string? transferEst = null;   // e.g. "PRL. DISTRICT AND SESSIONS JUDGE"
            string? transferDate = null;  // transfer date
            string? transferCino = null;  // original CNR from transferred establishment

            string? extraParty = null;
            string? cnrNumber = null;
            string? caseType = null;
            string? caseNumber = null;
            string? regDate = null;
            string? estName = null;

            // Keys that specifically mean "next scheduled hearing" — NOT first/filing
            string[] nextDateKeys = { "date_next_list", "date_last_list", "next_date", "next_hearing_date", "next_date_str", "nextDate", "next_hearing", "nxtDate", "nxt_date", "NEXT_DATE", "nextHearingDate", "next_date_of_hearing", "nxt_hearing_date" };

            // Keys explicitly excluded from next date (these are past/filing dates)
            string[] excludeDateKeys = { "first_hearing_date", "filing_date", "registration_date", "reg_date", "date_of_filing", "decision_date", "date_of_decision", "disposal_date", "first_date", "date_first_list", "dt_regis" };

            // Keys for hearing_date in history arrays — collect for max
            string[] historyDateKeys = { "hearing_date", "date", "srno_date", "hist_date", "cause_date", "next_date", "business_date" };

            string[] stageKeys = { "purpose_name", "stage", "court_stage", "purposeName", "case_stage", "stage_name", "stageDesc", "STAGE", "case_status_desc", "next_purpose" };
            string[] petitionerKeys = { "pet_name", "petitioner_name", "petitioner", "party1", "plaintiff", "appellant", "applicant", "plaintiff_name", "compl_name", "complainant", "claimant", "claimant_name", "workman", "workman_name", "pet_name_address", "party_1", "first_party", "party_name", "litigant_name" };
            string[] respondentKeys = { "res_name", "respondent_name", "respondent", "party2", "defendant", "resp_name", "opposite_party", "defendant_name", "resp_name_address", "party_2", "second_party", "management", "management_name" };
            string[] petAdvKeys = { "pet_adv", "petitioner_advocate", "pet_advocate", "advocate" };
            string[] resAdvKeys = { "res_adv", "respondent_advocate", "res_advocate" };
            string[] judgeKeys = { "desgname", "judge_name", "judge", "presiding_officer", "court_judge", "judicial_officer", "court_name", "court_no_judge", "court_number_and_judge" };
            string[] courtNoKeys = { "court_no", "court_number", "courtNo", "court_id" };
            string[] courtNoJudgeKeys = { "court_no_judge", "court_number_and_judge", "court_and_judge", "court_no_and_judge", "judge_court", "court_info" };
            string[] filingKeys = { "date_of_filing", "filing_date", "filing_dt", "dt_of_filing", "date_filing", "filingDate", "FILING_DATE", "fil_date", "date_of_filling", "filling_date", "dateoffiling", "date_fil" };
            string[] regKeys = { "dt_regis", "registration_date", "reg_date", "reg_dt", "date_of_reg", "regDate", "registrationDate", "REG_DATE", "date_of_registration", "reg_date_str", "dateofregistration", "reg_yr" };
            string[] firstHearingKeys = { "date_first_list", "first_hearing_date", "first_date", "first_date_of_hearing", "first_hearing_dt", "dt_first_list", "first_list_date" };
            string[] decisionKeys = { "date_of_decision", "decision_date", "disposal_date", "dt_decision", "dt_disposal", "decisionDate", "disposalDate", "date_of_disposal", "DECISION_DATE", "disp_date", "decision_dt", "disposal_dt", "dateofdecision", "dateofdisposal" };
            string[] natureDisposalKeys = { "nature_of_disposal", "disp_name", "disposal_nature", "nature_of_disp", "disposal_type" };
            string[] statusKeys = { "pend_disp", "case_status", "status", "current_status" };
            string[] transferEstKeys = { "transfer_est", "transferred_est", "transfer_from_est", "transferred_establishment", "from_est", "prev_est", "old_establishment", "transfer_from" };
            string[] transferDateKeys = { "transfer_date", "date_of_transfer", "transferred_date", "xfer_date" };
            string[] transferCinoKeys = { "transfer_cino", "prev_cino", "old_cino", "original_cno", "from_cino" };
            string[] extraPartyKeys = { "res_extra_party", "pet_extra_party", "extra_party", "extra_parties", "insurance_company", "insurance", "respondent_extra_party", "petitioner_extra_party" };

            // History array/object keys — we scan these for the latest hearing_date entry
            string[] historyArrayKeys = { "historyofcasehearing", "history", "history_details", "case_history", "hearing_history", "hist_details", "causelist_history", "hearings" };

            bool IsValidDate(string? s)
            {
                return !string.IsNullOrWhiteSpace(s) &&
                       (DateTime.TryParse(s, out _) ||
                        System.Text.RegularExpressions.Regex.IsMatch(s.Trim(), @"^\d{2}[-/]\d{2}[-/]\d{4}$") ||
                        System.Text.RegularExpressions.Regex.IsMatch(s.Trim(), @"^\d{4}[-/]\d{2}[-/]\d{2}$"));
            }

            void ScanHistoryArrays(System.Text.Json.JsonElement elem, int depth)
            {
                if (depth > 8) return;
                if (elem.ValueKind == System.Text.Json.JsonValueKind.Object)
                {
                    foreach (var prop in elem.EnumerateObject())
                    {
                        string key = prop.Name;
                        if (Array.Exists(historyArrayKeys, k => k.Equals(key, StringComparison.OrdinalIgnoreCase)))
                        {
                            IEnumerable<JsonElement> items = prop.Value.ValueKind == JsonValueKind.Array
                                ? prop.Value.EnumerateArray()
                                : prop.Value.ValueKind == JsonValueKind.Object
                                    ? prop.Value.EnumerateObject().Select(p => p.Value)
                                    : Array.Empty<JsonElement>();

                            foreach (var histItem in items)
                            {
                                if (histItem.ValueKind == System.Text.Json.JsonValueKind.Object)
                                {
                                    foreach (var sk in stageKeys)
                                    {
                                        if (histItem.TryGetProperty(sk, out var sp) && !string.IsNullOrWhiteSpace(sp.GetString()))
                                        {
                                            lastStage = sp.GetString()!.Trim();
                                            break;
                                        }
                                    }
                                    foreach (var dk in historyDateKeys)
                                    {
                                        if (histItem.TryGetProperty(dk, out var dp))
                                        {
                                            var dval = dp.ValueKind == System.Text.Json.JsonValueKind.String ? dp.GetString()?.Trim() : null;
                                            if (IsValidDate(dval)) nextDateCandidates.Add(dval!);
                                            break;
                                        }
                                    }
                                }
                            }
                        }
                        else
                        {
                            ScanHistoryArrays(prop.Value, depth + 1);
                        }
                    }
                }
                else if (elem.ValueKind == System.Text.Json.JsonValueKind.Array)
                {
                    foreach (var item in elem.EnumerateArray())
                        ScanHistoryArrays(item, depth + 1);
                }
            }

            void ScanElement(System.Text.Json.JsonElement elem, int depth)
            {
                if (depth > 8) return;
                if (elem.ValueKind == System.Text.Json.JsonValueKind.Object)
                {
                    foreach (var prop in elem.EnumerateObject())
                    {
                        string key = prop.Name;

                        bool isExcluded = Array.Exists(excludeDateKeys, k => k.Equals(key, StringComparison.OrdinalIgnoreCase));

                        string? val = prop.Value.ValueKind == System.Text.Json.JsonValueKind.String
                            ? prop.Value.GetString()?.Trim()
                            : null;

                        if (cnrNumber == null && (key.Equals("cino", StringComparison.OrdinalIgnoreCase) || key.Equals("cnr", StringComparison.OrdinalIgnoreCase) || key.Equals("cnr_number", StringComparison.OrdinalIgnoreCase)) && !string.IsNullOrWhiteSpace(val))
                            cnrNumber = val;

                        if (caseType == null && (key.Equals("type_name", StringComparison.OrdinalIgnoreCase) || key.Equals("case_type", StringComparison.OrdinalIgnoreCase)) && !string.IsNullOrWhiteSpace(val))
                            caseType = val;

                        if (caseNumber == null && (key.Equals("reg_no", StringComparison.OrdinalIgnoreCase) || key.Equals("case_number", StringComparison.OrdinalIgnoreCase)) && !string.IsNullOrWhiteSpace(val))
                            caseNumber = val;

                        if (regDate == null && Array.Exists(regKeys, k => k.Equals(key, StringComparison.OrdinalIgnoreCase)) && !string.IsNullOrWhiteSpace(val))
                            regDate = val;

                        if (estName == null && (key.Equals("court_est_name", StringComparison.OrdinalIgnoreCase) || key.Equals("establishment_name", StringComparison.OrdinalIgnoreCase)) && !string.IsNullOrWhiteSpace(val))
                            estName = val;

                        if (coram == null && (key.Equals("coram", StringComparison.OrdinalIgnoreCase) || key.Equals("bench_coram", StringComparison.OrdinalIgnoreCase)) && !string.IsNullOrWhiteSpace(val))
                            coram = val;

                        if (extraParty == null && Array.Exists(extraPartyKeys, k => k.Equals(key, StringComparison.OrdinalIgnoreCase)))
                        {
                            if (prop.Value.ValueKind == JsonValueKind.String)
                                extraParty = prop.Value.GetString()?.Trim();
                            else if (prop.Value.ValueKind == JsonValueKind.Object)
                            {
                                var partyList = new List<string>();
                                foreach (var subProp in prop.Value.EnumerateObject())
                                {
                                    var strVal = subProp.Value.GetString()?.Trim();
                                    if (!string.IsNullOrWhiteSpace(strVal)) partyList.Add(strVal);
                                }
                                if (partyList.Count > 0) extraParty = string.Join(", ", partyList);
                            }
                        }

                        if (!string.IsNullOrWhiteSpace(val) && !isExcluded)
                        {
                            if (Array.Exists(nextDateKeys, k => k.Equals(key, StringComparison.OrdinalIgnoreCase)) && IsValidDate(val))
                                nextDateCandidates.Add(val);

                            if (stage == null && Array.Exists(stageKeys, k => k.Equals(key, StringComparison.OrdinalIgnoreCase))) stage = val;
                            if (petitioner == null && Array.Exists(petitionerKeys, k => k.Equals(key, StringComparison.OrdinalIgnoreCase))) petitioner = val;
                            if (respondent == null && Array.Exists(respondentKeys, k => k.Equals(key, StringComparison.OrdinalIgnoreCase))) respondent = val;
                            if (petitionerAdv == null && Array.Exists(petAdvKeys, k => k.Equals(key, StringComparison.OrdinalIgnoreCase))) petitionerAdv = val;
                            if (respondentAdv == null && Array.Exists(resAdvKeys, k => k.Equals(key, StringComparison.OrdinalIgnoreCase))) respondentAdv = val;
                            if (judge == null && Array.Exists(judgeKeys, k => k.Equals(key, StringComparison.OrdinalIgnoreCase))) judge = val;
                            if (courtNo == null && Array.Exists(courtNoKeys, k => k.Equals(key, StringComparison.OrdinalIgnoreCase))) courtNo = val;
                            if (courtNoJudge == null && Array.Exists(courtNoJudgeKeys, k => k.Equals(key, StringComparison.OrdinalIgnoreCase))) courtNoJudge = val;
                            if (caseStatus == null && Array.Exists(statusKeys, k => k.Equals(key, StringComparison.OrdinalIgnoreCase))) caseStatus = val;
                            if (transferEst == null && Array.Exists(transferEstKeys, k => k.Equals(key, StringComparison.OrdinalIgnoreCase))) transferEst = val;
                            if (transferDate == null && Array.Exists(transferDateKeys, k => k.Equals(key, StringComparison.OrdinalIgnoreCase))) transferDate = val;
                            if (transferCino == null && Array.Exists(transferCinoKeys, k => k.Equals(key, StringComparison.OrdinalIgnoreCase))) transferCino = val;
                        }

                        if (!string.IsNullOrWhiteSpace(val))
                        {
                            if (filingDate == null && Array.Exists(filingKeys, k => k.Equals(key, StringComparison.OrdinalIgnoreCase))) filingDate = val;
                            if (firstHearingDate == null && Array.Exists(firstHearingKeys, k => k.Equals(key, StringComparison.OrdinalIgnoreCase))) firstHearingDate = val;
                            if (decisionDate == null && Array.Exists(decisionKeys, k => k.Equals(key, StringComparison.OrdinalIgnoreCase))) decisionDate = val;
                            if (natureOfDisposal == null && Array.Exists(natureDisposalKeys, k => k.Equals(key, StringComparison.OrdinalIgnoreCase))) natureOfDisposal = val;
                        }

                        ScanElement(prop.Value, depth + 1);
                    }
                }
                else if (elem.ValueKind == System.Text.Json.JsonValueKind.Array)
                {
                    foreach (var item in elem.EnumerateArray())
                        ScanElement(item, depth + 1);
                }
            }

            ScanHistoryArrays(root, 0);
            ScanElement(root, 0);

            if (!string.IsNullOrWhiteSpace(coram))
            {
                judge = coram;
                courtNoJudge = coram;
            }

            string? latestNextDate = null;
            DateTime latestDt = DateTime.MinValue;
            foreach (var candidate in nextDateCandidates)
            {
                if (DateTime.TryParse(candidate, out DateTime dt) && dt > latestDt)
                {
                    latestDt = dt;
                    latestNextDate = candidate;
                }
                else if (DateTime.TryParseExact(candidate,
                    new[] { "dd-MM-yyyy", "d-M-yyyy", "dd/MM/yyyy", "d/M/yyyy" },
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out DateTime dt2) && dt2 > latestDt)
                {
                    latestDt = dt2;
                    latestNextDate = candidate;
                }
            }

            string? effectiveStage = lastStage ?? stage;

            string? effectiveCourtNoJudge = courtNoJudge
                ?? (courtNo != null && judge != null ? $"{courtNo} — {judge}" : courtNo ?? judge);

            string cleanStatus = IsValidStatusString(caseStatus)
                ? (caseStatus == "P" ? "Pending" : caseStatus == "D" ? "Disposed" : caseStatus!)
                : (!string.IsNullOrWhiteSpace(decisionDate) || (effectiveStage != null && effectiveStage.Contains("DISPOSED", StringComparison.OrdinalIgnoreCase)) ? "Disposed" : "Pending");

            if ((cleanStatus != null && cleanStatus.Contains("Disposed", StringComparison.OrdinalIgnoreCase)) ||
                (effectiveStage != null && effectiveStage.Contains("DISPOSED", StringComparison.OrdinalIgnoreCase)))
            {
                if (string.IsNullOrWhiteSpace(decisionDate))
                {
                    decisionDate = latestNextDate;
                }
                latestNextDate = "Disposed";
            }
            else if (string.IsNullOrWhiteSpace(decisionDate))
            {
                decisionDate = "Pending";
            }

            if (string.IsNullOrWhiteSpace(filingDate) && !string.IsNullOrWhiteSpace(regDate))
            {
                filingDate = regDate;
            }
            else if (string.IsNullOrWhiteSpace(regDate) && !string.IsNullOrWhiteSpace(filingDate))
            {
                regDate = filingDate;
            }

            return new CnrSummaryModel
            {
                cnr_number = cnrNumber,
                case_type = caseType,
                case_number = caseNumber,
                registration_date = regDate,
                establishment_name = estName,
                next_hearing_date = latestNextDate,
                first_hearing_date = firstHearingDate,
                stage = effectiveStage,
                petitioner = petitioner,
                respondent = respondent,
                petitioner_advocate = petitionerAdv,
                respondent_advocate = respondentAdv,
                judge = judge,
                court_no = courtNo,
                court_no_judge = effectiveCourtNoJudge,
                filing_date = filingDate,
                decision_date = decisionDate,
                nature_of_disposal = natureOfDisposal,
                case_status = cleanStatus,
                transfer_est = transferEst,
                transfer_date = transferDate,
                transfer_cino = transferCino,
                extra_party = extraParty
            };
        }

        /// <summary>
        /// Walks the raw NAPIX JsonElement and extracts all hearing history rows.
        /// Handles Arrays, Objects, stringified JSON strings, and dicts (historyofcasehearing).
        /// </summary>
        private static List<Dictionary<string, string?>> ExtractHistoryArray(System.Text.Json.JsonElement root)
        {
            var result = new List<Dictionary<string, string?>>();
            string[] histKeys = {
                "historyofcasehearing", "history", "history_details", "case_history", "hearing_history",
                "hist_details", "causelist_history", "hearings", "businessDetails",
                "case_hearings", "caseHistory", "CaseHistory", "HistoryDetails",
                "business_details", "hist", "hearing_dates", "case_dates",
                "case_business", "cases_business"
            };

            void ExtractFromItem(JsonElement item)
            {
                if (item.ValueKind == JsonValueKind.Object)
                {
                    var row = new Dictionary<string, string?>();
                    foreach (var p in item.EnumerateObject())
                    {
                        string val = p.Value.ValueKind == JsonValueKind.String
                            ? (p.Value.GetString() ?? "") : p.Value.ToString();
                        row[p.Name] = val;
                    }

                    string bDate = row.GetValueOrDefault("business_date") ?? 
                                   row.GetValueOrDefault("cause_date") ?? 
                                   row.GetValueOrDefault("dt_business") ?? 
                                   row.GetValueOrDefault("hist_date") ?? 
                                   row.GetValueOrDefault("srno_date") ?? "";

                    string nDate = row.GetValueOrDefault("hearing_date") ?? 
                                   row.GetValueOrDefault("next_date") ?? 
                                   row.GetValueOrDefault("next_hearing_date") ?? 
                                   row.GetValueOrDefault("nxt_date") ?? 
                                   row.GetValueOrDefault("date_next_list") ?? 
                                   row.GetValueOrDefault("next_date_of_hearing") ?? "";

                    string pPose = row.GetValueOrDefault("purpose_of_listing") ?? 
                                   row.GetValueOrDefault("next_purpose") ?? 
                                   row.GetValueOrDefault("purpose_name") ?? 
                                   row.GetValueOrDefault("purpose") ?? 
                                   row.GetValueOrDefault("court_stage") ?? 
                                   row.GetValueOrDefault("stage") ?? 
                                   row.GetValueOrDefault("purpose_of_hearing") ?? "";

                    string bNess = row.GetValueOrDefault("business") ??
                                   row.GetValueOrDefault("business_details") ??
                                   row.GetValueOrDefault("proceedings") ??
                                   row.GetValueOrDefault("roznama") ??
                                   row.GetValueOrDefault("order_business") ?? "";

                    string jName = row.GetValueOrDefault("desgname") ?? 
                                   row.GetValueOrDefault("court_no_judge") ?? 
                                   row.GetValueOrDefault("judge_name") ?? 
                                   row.GetValueOrDefault("presiding_officer") ?? 
                                   row.GetValueOrDefault("court_judge") ?? 
                                   row.GetValueOrDefault("judge") ?? "";

                    if (!string.IsNullOrEmpty(bDate)) row["business_date"] = bDate;
                    if (!string.IsNullOrEmpty(nDate)) row["next_date"] = nDate;
                    if (!string.IsNullOrEmpty(pPose)) row["next_purpose"] = pPose;
                    if (!string.IsNullOrEmpty(bNess)) row["business"] = bNess;
                    if (!string.IsNullOrEmpty(jName)) row["judge"] = jName;

                    if (row.Count > 0) result.Add(row);
                }
                else if (item.ValueKind == JsonValueKind.String)
                {
                    string str = item.GetString()?.Trim() ?? "";
                    if ((str.StartsWith("[") && str.EndsWith("]")) || (str.StartsWith("{") && str.EndsWith("}")))
                    {
                        try
                        {
                            using var doc = JsonDocument.Parse(str);
                            if (doc.RootElement.ValueKind == JsonValueKind.Array)
                                foreach (var sub in doc.RootElement.EnumerateArray()) ExtractFromItem(sub);
                            else if (doc.RootElement.ValueKind == JsonValueKind.Object)
                                ExtractFromItem(doc.RootElement);
                        }
                        catch { }
                    }
                }
            }

            void ScanElement(System.Text.Json.JsonElement elem, int depth)
            {
                if (depth > 10) return;
                if (elem.ValueKind == JsonValueKind.Object)
                {
                    foreach (var prop in elem.EnumerateObject())
                    {
                        string pName = prop.Name.ToLowerInvariant();
                        bool isHistKey = Array.Exists(histKeys, k => pName == k || pName.Contains(k)) ||
                                         pName.Contains("hist") || pName.Contains("hearing") || pName.Contains("business");
                        if (isHistKey)
                        {
                            if (prop.Value.ValueKind == JsonValueKind.Array)
                            {
                                foreach (var item in prop.Value.EnumerateArray()) ExtractFromItem(item);
                            }
                            else if (prop.Value.ValueKind == JsonValueKind.Object)
                            {
                                foreach (var child in prop.Value.EnumerateObject()) ExtractFromItem(child.Value);
                            }
                            else if (prop.Value.ValueKind == JsonValueKind.String)
                            {
                                ExtractFromItem(prop.Value);
                            }
                        }
                        else
                        {
                            if (prop.Value.ValueKind == JsonValueKind.String)
                            {
                                string str = prop.Value.GetString()?.Trim() ?? "";
                                if ((str.StartsWith("[") && str.EndsWith("]")) || (str.StartsWith("{") && str.EndsWith("}")))
                                {
                                    try
                                    {
                                        using var doc = JsonDocument.Parse(str);
                                        ScanElement(doc.RootElement, depth + 1);
                                    }
                                    catch { }
                                }
                            }
                            else
                            {
                                ScanElement(prop.Value, depth + 1);
                            }
                        }
                    }
                }
                else if (elem.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in elem.EnumerateArray()) ScanElement(item, depth + 1);
                }
            }

            ScanElement(root, 0);

            // Phase 2: Fallback — find candidate objects with history field signatures
            if (result.Count == 0)
            {
                void ScanFallback(System.Text.Json.JsonElement elem, int depth)
                {
                    if (depth > 10) return;
                    if (elem.ValueKind == JsonValueKind.Object)
                    {
                        foreach (var prop in elem.EnumerateObject())
                        {
                            if (prop.Value.ValueKind == JsonValueKind.Array || prop.Value.ValueKind == JsonValueKind.Object)
                            {
                                IEnumerable<JsonElement> items = prop.Value.ValueKind == JsonValueKind.Array
                                    ? prop.Value.EnumerateArray()
                                    : prop.Value.EnumerateObject().Select(p => p.Value);

                                var candidates = new List<Dictionary<string, string?>>();
                                bool looksLikeHistory = false;
                                foreach (var item in items)
                                {
                                    if (item.ValueKind == JsonValueKind.Object)
                                    {
                                        var row = new Dictionary<string, string?>();
                                        foreach (var p in item.EnumerateObject())
                                        {
                                            row[p.Name] = p.Value.ValueKind == JsonValueKind.String
                                                ? p.Value.GetString() : p.Value.ToString();
                                            string kl = p.Name.ToLowerInvariant();
                                            if (kl is "business_date" or "next_date" or "next_purpose" or "srno" or "sr_no" or "cause_date" or "purpose_name" or "hearing_date")
                                                looksLikeHistory = true;
                                        }
                                        candidates.Add(row);
                                    }
                                }
                                if (looksLikeHistory && candidates.Count > 0)
                                    result.AddRange(candidates);
                                else
                                    ScanFallback(prop.Value, depth + 1);
                            }
                        }
                    }
                    else if (elem.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var item in elem.EnumerateArray()) ScanFallback(item, depth + 1);
                    }
                }
                ScanFallback(root, 0);
            }

            return result;
        }

        /// <summary>
        /// Walks the raw NAPIX JsonElement and extracts all court order/judgment rows.
        /// Handles Arrays, Objects, stringified JSON strings, and nested dicts.
        /// </summary>
        private static List<Dictionary<string, string?>> ExtractOrdersArray(System.Text.Json.JsonElement root)
        {
            var result = new List<Dictionary<string, string?>>();
            string[] orderKeys = {
                "interimorder", "finalorder", "orders", "order_details", "orders_details", "case_orders",
                "judgements", "judgments", "judgment_details", "order_list", "final_order", "final_orders",
                "final_judgement", "final_judgment", "disposal_order", "disposalorder", "orderdetails",
                "caseOrders", "judgment_copy", "order_copy", "order_pdf", "judgment",
                "judg_details", "interim_order", "interim_orders", "disposal_judgement", "judgment_pdf",
                "hearing_orders", "proceedings",
                "order_number", "order_info", "order_data", "judgment_data", "order_pdf_url", "orders_list"
            };

            string? globalStateCode = null, globalDistCode = null, globalCourtCode = null;
            string? globalEstCode = null, globalCino = null;

            void CaptureGlobals(JsonElement elem, int d)
            {
                if (d > 5 || elem.ValueKind != JsonValueKind.Object) return;
                foreach (var prop in elem.EnumerateObject())
                {
                    string kl = prop.Name.ToLowerInvariant();
                    string val = prop.Value.ValueKind == JsonValueKind.String ? (prop.Value.GetString() ?? "") : "";
                    if (!string.IsNullOrWhiteSpace(val))
                    {
                        if (kl is "state_code" or "statecode") globalStateCode ??= val;
                        else if (kl is "dist_code" or "distcode" or "district_code") globalDistCode ??= val;
                        else if (kl is "court_code" or "courtcode") globalCourtCode ??= val;
                        else if (kl is "est_code" or "estcode" or "establishment_code") globalEstCode ??= val;
                        else if (kl is "cino" or "cnr" or "cnr_number") globalCino ??= val;
                    }
                    if (prop.Value.ValueKind == JsonValueKind.Object) CaptureGlobals(prop.Value, d + 1);
                }
            }
            CaptureGlobals(root, 0);

            void ExtractFromItem(JsonElement item)
            {
                if (item.ValueKind == JsonValueKind.Object)
                {
                    var row = new Dictionary<string, string?>();
                    foreach (var p in item.EnumerateObject())
                    {
                        string val = p.Value.ValueKind == JsonValueKind.String
                            ? (p.Value.GetString() ?? "") : p.Value.ToString();
                        row[p.Name] = val;

                        string kl = p.Name.ToLowerInvariant();
                        if (!row.ContainsKey("order_no") && kl is "order_no" or "sr_no" or "srno" or "serial_no" or "orderno" or "order_number" or "order_num") row["order_no"] = val;
                        if (!row.ContainsKey("order_date") && kl is "order_date" or "date" or "business_date" or "cause_date" or "hearing_date" or "dt_order" or "orderdate" or "order_dt" or "reg_date" or "dt_regis") row["order_date"] = val;
                        if (!row.ContainsKey("order_details") && kl is "order_details" or "purpose_name" or "description" or "remarks" or "judgment_text" or "order_text" or "cause_title" or "orderdetail" or "order_detail" or "order_type" or "type" or "title" or "order_title" or "nature_of_disposal") row["order_details"] = val;
                        if (!row.ContainsKey("judge_name") && kl is "order_judge_name" or "judge_name" or "judge" or "presiding_officer" or "desgname") row["judge_name"] = val;
                        if (!row.ContainsKey("pdf_path") && kl is "pdf_path" or "pdf_file" or "order_pdf" or "filepath" or "file_path" or "pdf_name" or "filename" or "pdf_url" or "order_url" or "document_path" or "order_file" or "doc_path" or "display_pdf" or "link" or "url" or "download_url") row["pdf_path"] = val;
                    }

                    if (!row.ContainsKey("judge_name") && (row.ContainsKey("order_judge_name") || row.ContainsKey("judge") || row.ContainsKey("desgname")))
                    {
                        row["judge_name"] = row.GetValueOrDefault("order_judge_name") ?? row.GetValueOrDefault("judge") ?? row.GetValueOrDefault("desgname");
                    }

                    if (!string.IsNullOrWhiteSpace(globalStateCode)) row["state_code"] = row.GetValueOrDefault("state_code") ?? globalStateCode;
                    if (!string.IsNullOrWhiteSpace(globalDistCode)) row["dist_code"] = row.GetValueOrDefault("dist_code") ?? globalDistCode;
                    if (!string.IsNullOrWhiteSpace(globalCourtCode)) row["court_code"] = row.GetValueOrDefault("court_code") ?? globalCourtCode;
                    if (!string.IsNullOrWhiteSpace(globalEstCode)) row["est_code"] = row.GetValueOrDefault("est_code") ?? globalEstCode;
                    if (!string.IsNullOrWhiteSpace(globalCino)) row["cino"] = row.GetValueOrDefault("cino") ?? globalCino;

                    if (!row.ContainsKey("order_details") || string.IsNullOrWhiteSpace(row["order_details"]))
                    {
                        row["order_details"] = row.GetValueOrDefault("order_type") ?? row.GetValueOrDefault("title") ?? row.GetValueOrDefault("purpose_name") ?? "Court Order / Judgment";
                    }

                    // Auto-generate proxy URL to stream PDF via ViewOrderPdf if missing
                    string oDate = row.GetValueOrDefault("order_date") ?? "";
                    string oNo = row.GetValueOrDefault("order_no") ?? row.GetValueOrDefault("order_number") ?? "";
                    if (string.IsNullOrWhiteSpace(oNo) || oNo.Length > 8 || oNo.Equals(globalCino, StringComparison.OrdinalIgnoreCase) || !int.TryParse(oNo, out _))
                    {
                        oNo = (result.Count + 1).ToString();
                    }
                    row["order_no"] = oNo;
                    row["order_number"] = oNo;
                    string oTitle = row.GetValueOrDefault("order_details") ?? "Court Order";
                    string oCnr = row.GetValueOrDefault("cino") ?? globalCino ?? "";

                    if (string.IsNullOrWhiteSpace(row.GetValueOrDefault("pdf_path")) && (!string.IsNullOrEmpty(oCnr) || !string.IsNullOrEmpty(oDate)))
                    {
                        string proxy = $"/ECourts/ViewOrderPdf?cnr={Uri.EscapeDataString(oCnr)}&orderNo={Uri.EscapeDataString(oNo)}&date={Uri.EscapeDataString(oDate)}&title={Uri.EscapeDataString(oTitle)}";
                        row["pdf_path"] = proxy;
                        row["order_pdf_url"] = proxy;
                        row["pdf_url"] = proxy;
                    }

                    bool hasData = !string.IsNullOrWhiteSpace(row.GetValueOrDefault("order_date"))
                        || !string.IsNullOrWhiteSpace(row.GetValueOrDefault("order_no"))
                        || !string.IsNullOrWhiteSpace(row.GetValueOrDefault("pdf_path"))
                        || !string.IsNullOrWhiteSpace(row.GetValueOrDefault("order_details"));
                    if (hasData) result.Add(row);
                }
                else if (item.ValueKind == JsonValueKind.String)
                {
                    string str = item.GetString()?.Trim() ?? "";
                    if ((str.StartsWith("[") && str.EndsWith("]")) || (str.StartsWith("{") && str.EndsWith("}")))
                    {
                        try
                        {
                            using var doc = JsonDocument.Parse(str);
                            if (doc.RootElement.ValueKind == JsonValueKind.Array)
                                foreach (var sub in doc.RootElement.EnumerateArray()) ExtractFromItem(sub);
                            else if (doc.RootElement.ValueKind == JsonValueKind.Object)
                                ExtractFromItem(doc.RootElement);
                        }
                        catch { }
                    }
                }
            }

            void Scan(JsonElement elem, int depth)
            {
                if (depth > 10) return;
                if (elem.ValueKind == JsonValueKind.Object)
                {
                    foreach (var prop in elem.EnumerateObject())
                    {
                        string pName = prop.Name.ToLowerInvariant();
                        bool isOrderKey = Array.Exists(orderKeys, k => pName == k || pName.Contains(k)) ||
                                          pName.Contains("order") || pName.Contains("judg") || pName.Contains("interim");
                        if (isOrderKey)
                        {
                            if (prop.Value.ValueKind == JsonValueKind.Array)
                                foreach (var item in prop.Value.EnumerateArray()) ExtractFromItem(item);
                            else if (prop.Value.ValueKind == JsonValueKind.Object)
                            {
                                bool isNestedDict = prop.Value.EnumerateObject().Any(p => p.Value.ValueKind == JsonValueKind.Object);
                                if (isNestedDict)
                                    foreach (var child in prop.Value.EnumerateObject())
                                    { if (child.Value.ValueKind == JsonValueKind.Object) ExtractFromItem(child.Value); }
                                else ExtractFromItem(prop.Value);
                            }
                            else if (prop.Value.ValueKind == JsonValueKind.String)
                            {
                                ExtractFromItem(prop.Value);
                            }
                        }
                        else
                        {
                            if (prop.Value.ValueKind == JsonValueKind.String)
                            {
                                string str = prop.Value.GetString()?.Trim() ?? "";
                                if ((str.StartsWith("[") && str.EndsWith("]")) || (str.StartsWith("{") && str.EndsWith("}")))
                                {
                                    try
                                    {
                                        using var doc = JsonDocument.Parse(str);
                                        Scan(doc.RootElement, depth + 1);
                                    }
                                    catch { }
                                }
                            }
                            else
                            {
                                Scan(prop.Value, depth + 1);
                            }
                        }
                    }
                }
                else if (elem.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in elem.EnumerateArray()) Scan(item, depth + 1);
                }
            }

            Scan(root, 0);
            return result;
        }

        private static string JsonValToString(JsonElement v) =>
            v.ValueKind switch
            {
                JsonValueKind.String => v.GetString() ?? "",
                JsonValueKind.Number => v.TryGetInt64(out var l) ? l.ToString() : v.GetDouble().ToString(System.Globalization.CultureInfo.InvariantCulture),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                JsonValueKind.Null => "",
                _ => v.ToString()
            };

        private static List<Dictionary<string, string?>> ExtractIaArray(System.Text.Json.JsonElement root)
        {
            var result = new List<Dictionary<string, string?>>();
            string[] iaKeys = { "iafiling", "ia_filing", "ia_details", "ia", "ias" };

            void Scan(JsonElement elem, int depth)
            {
                if (depth > 8) return;
                if (elem.ValueKind == JsonValueKind.Object)
                {
                    foreach (var prop in elem.EnumerateObject())
                    {
                        if (Array.Exists(iaKeys, k => k.Equals(prop.Name, StringComparison.OrdinalIgnoreCase)))
                        {
                            if (prop.Value.ValueKind == JsonValueKind.Array)
                            {
                                foreach (var item in prop.Value.EnumerateArray())
                                {
                                    if (item.ValueKind == JsonValueKind.Object)
                                    {
                                        var row = item.EnumerateObject().ToDictionary(p => p.Name, p => (string?)JsonValToString(p.Value));
                                        result.Add(row);
                                    }
                                }
                            }
                            else if (prop.Value.ValueKind == JsonValueKind.Object)
                            {
                                foreach (var child in prop.Value.EnumerateObject())
                                {
                                    if (child.Value.ValueKind == JsonValueKind.Object)
                                    {
                                        var row = child.Value.EnumerateObject().ToDictionary(p => p.Name, p => (string?)JsonValToString(p.Value));
                                        result.Add(row);
                                    }
                                }
                            }
                        }
                        else
                        {
                            Scan(prop.Value, depth + 1);
                        }
                    }
                }
                else if (elem.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in elem.EnumerateArray()) Scan(item, depth + 1);
                }
            }

            Scan(root, 0);
            return result;
        }

        private static List<Dictionary<string, string?>> ExtractProcessArray(System.Text.Json.JsonElement root)
        {
            var result = new List<Dictionary<string, string?>>();
            string[] procKeys = { "processes", "case_processes", "process", "process_details", "notice_details" };

            void Scan(JsonElement elem, int depth)
            {
                if (depth > 8) return;
                if (elem.ValueKind == JsonValueKind.Object)
                {
                    foreach (var prop in elem.EnumerateObject())
                    {
                        if (Array.Exists(procKeys, k => k.Equals(prop.Name, StringComparison.OrdinalIgnoreCase)))
                        {
                            if (prop.Value.ValueKind == JsonValueKind.Array)
                            {
                                foreach (var item in prop.Value.EnumerateArray())
                                {
                                    if (item.ValueKind == JsonValueKind.Object)
                                    {
                                        var row = item.EnumerateObject().ToDictionary(p => p.Name, p => (string?)JsonValToString(p.Value));
                                        result.Add(row);
                                    }
                                }
                            }
                            else if (prop.Value.ValueKind == JsonValueKind.Object)
                            {
                                foreach (var child in prop.Value.EnumerateObject())
                                {
                                    if (child.Value.ValueKind == JsonValueKind.Object)
                                    {
                                        var row = child.Value.EnumerateObject().ToDictionary(p => p.Name, p => (string?)JsonValToString(p.Value));
                                        result.Add(row);
                                    }
                                }
                            }
                        }
                        else
                        {
                            Scan(prop.Value, depth + 1);
                        }
                    }
                }
                else if (elem.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in elem.EnumerateArray()) Scan(item, depth + 1);
                }
            }

            Scan(root, 0);
            return result;
        }

        private static List<Dictionary<string, string?>> ExtractActsArray(System.Text.Json.JsonElement root)
        {
            var result = new List<Dictionary<string, string?>>();
            string[] keys = { "acts", "act_details", "act" };

            void Scan(JsonElement elem, int depth)
            {
                if (depth > 8) return;
                if (elem.ValueKind == JsonValueKind.Object)
                {
                    foreach (var prop in elem.EnumerateObject())
                    {
                        if (Array.Exists(keys, k => k.Equals(prop.Name, StringComparison.OrdinalIgnoreCase)))
                        {
                            if (prop.Value.ValueKind == JsonValueKind.Array)
                            {
                                foreach (var item in prop.Value.EnumerateArray())
                                {
                                    if (item.ValueKind == JsonValueKind.Object)
                                        result.Add(item.EnumerateObject().ToDictionary(p => p.Name, p => (string?)JsonValToString(p.Value)));
                                }
                            }
                            else if (prop.Value.ValueKind == JsonValueKind.Object)
                            {
                                foreach (var child in prop.Value.EnumerateObject())
                                {
                                    if (child.Value.ValueKind == JsonValueKind.Object)
                                        result.Add(child.Value.EnumerateObject().ToDictionary(p => p.Name, p => (string?)JsonValToString(p.Value)));
                                }
                            }
                        }
                        else Scan(prop.Value, depth + 1);
                    }
                }
                else if (elem.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in elem.EnumerateArray()) Scan(item, depth + 1);
                }
            }

            Scan(root, 0);
            return result;
        }

        private static List<Dictionary<string, string?>> ExtractExtraPartiesArray(System.Text.Json.JsonElement root)
        {
            var result = new List<Dictionary<string, string?>>();
            string[] keys = { "pet_extra_party", "petitioner_extra_party", "res_extra_party", "respondent_extra_party", "extra_party", "extra_parties" };

            void Scan(JsonElement elem, int depth)
            {
                if (depth > 8) return;
                if (elem.ValueKind == JsonValueKind.Object)
                {
                    foreach (var prop in elem.EnumerateObject())
                    {
                        if (Array.Exists(keys, k => k.Equals(prop.Name, StringComparison.OrdinalIgnoreCase)))
                        {
                            if (prop.Value.ValueKind == JsonValueKind.Array)
                            {
                                foreach (var item in prop.Value.EnumerateArray())
                                {
                                    if (item.ValueKind == JsonValueKind.Object)
                                    {
                                        var d = item.EnumerateObject().ToDictionary(p => p.Name, p => (string?)JsonValToString(p.Value));
                                        d["party_type"] = prop.Name.Contains("pet") ? "Petitioner" : "Respondent";
                                        result.Add(d);
                                    }
                                }
                            }
                            else if (prop.Value.ValueKind == JsonValueKind.Object)
                            {
                                foreach (var child in prop.Value.EnumerateObject())
                                {
                                    if (child.Value.ValueKind == JsonValueKind.Object)
                                    {
                                        var d = child.Value.EnumerateObject().ToDictionary(p => p.Name, p => (string?)JsonValToString(p.Value));
                                        d["party_type"] = prop.Name.Contains("pet") ? "Petitioner" : "Respondent";
                                        result.Add(d);
                                    }
                                }
                            }
                        }
                        else Scan(prop.Value, depth + 1);
                    }
                }
                else if (elem.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in elem.EnumerateArray()) Scan(item, depth + 1);
                }
            }

            Scan(root, 0);
            return result;
        }

        private static List<Dictionary<string, string?>> ExtractObjectionsArray(System.Text.Json.JsonElement root)
        {
            var result = new List<Dictionary<string, string?>>();
            string[] keys = { "objections", "case_objections", "objection" };

            void Scan(JsonElement elem, int depth)
            {
                if (depth > 8) return;
                if (elem.ValueKind == JsonValueKind.Object)
                {
                    foreach (var prop in elem.EnumerateObject())
                    {
                        if (Array.Exists(keys, k => k.Equals(prop.Name, StringComparison.OrdinalIgnoreCase)))
                        {
                            if (prop.Value.ValueKind == JsonValueKind.Array)
                            {
                                foreach (var item in prop.Value.EnumerateArray())
                                {
                                    if (item.ValueKind == JsonValueKind.Object)
                                        result.Add(item.EnumerateObject().ToDictionary(p => p.Name, p => (string?)JsonValToString(p.Value)));
                                }
                            }
                            else if (prop.Value.ValueKind == JsonValueKind.Object)
                            {
                                foreach (var child in prop.Value.EnumerateObject())
                                {
                                    if (child.Value.ValueKind == JsonValueKind.Object)
                                        result.Add(child.Value.EnumerateObject().ToDictionary(p => p.Name, p => (string?)JsonValToString(p.Value)));
                                }
                            }
                        }
                        else Scan(prop.Value, depth + 1);
                    }
                }
                else if (elem.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in elem.EnumerateArray()) Scan(item, depth + 1);
                }
            }

            Scan(root, 0);
            return result;
        }

        private static List<Dictionary<string, string?>> ExtractTransfersArray(System.Text.Json.JsonElement root)
        {
            var result = new List<Dictionary<string, string?>>();
            string[] keys = { "transfer", "case_transfer", "transfers" };

            void Scan(JsonElement elem, int depth)
            {
                if (depth > 8) return;
                if (elem.ValueKind == JsonValueKind.Object)
                {
                    foreach (var prop in elem.EnumerateObject())
                    {
                        if (Array.Exists(keys, k => k.Equals(prop.Name, StringComparison.OrdinalIgnoreCase)))
                        {
                            if (prop.Value.ValueKind == JsonValueKind.Array)
                            {
                                foreach (var item in prop.Value.EnumerateArray())
                                {
                                    if (item.ValueKind == JsonValueKind.Object)
                                        result.Add(item.EnumerateObject().ToDictionary(p => p.Name, p => (string?)JsonValToString(p.Value)));
                                }
                            }
                            else if (prop.Value.ValueKind == JsonValueKind.Object)
                            {
                                foreach (var child in prop.Value.EnumerateObject())
                                {
                                    if (child.Value.ValueKind == JsonValueKind.Object)
                                        result.Add(child.Value.EnumerateObject().ToDictionary(p => p.Name, p => (string?)JsonValToString(p.Value)));
                                }
                            }
                        }
                        else Scan(prop.Value, depth + 1);
                    }
                }
                else if (elem.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in elem.EnumerateArray()) Scan(item, depth + 1);
                }
            }

            Scan(root, 0);
            return result;
        }

        private static List<Dictionary<string, string?>> ExtractLinkedCasesArray(System.Text.Json.JsonElement root)
        {
            var result = new List<Dictionary<string, string?>>();
            string[] keys = { "link_cases", "linked_cases", "connected_cases" };

            void Scan(JsonElement elem, int depth)
            {
                if (depth > 8) return;
                if (elem.ValueKind == JsonValueKind.Object)
                {
                    foreach (var prop in elem.EnumerateObject())
                    {
                        if (Array.Exists(keys, k => k.Equals(prop.Name, StringComparison.OrdinalIgnoreCase)))
                        {
                            if (prop.Value.ValueKind == JsonValueKind.Array)
                            {
                                foreach (var item in prop.Value.EnumerateArray())
                                {
                                    if (item.ValueKind == JsonValueKind.Object)
                                        result.Add(item.EnumerateObject().ToDictionary(p => p.Name, p => (string?)JsonValToString(p.Value)));
                                }
                            }
                            else if (prop.Value.ValueKind == JsonValueKind.Object)
                            {
                                foreach (var child in prop.Value.EnumerateObject())
                                {
                                    if (child.Value.ValueKind == JsonValueKind.Object)
                                        result.Add(child.Value.EnumerateObject().ToDictionary(p => p.Name, p => (string?)JsonValToString(p.Value)));
                                }
                            }
                        }
                        else Scan(prop.Value, depth + 1);
                    }
                }
                else if (elem.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in elem.EnumerateArray()) Scan(item, depth + 1);
                }
            }

            Scan(root, 0);
            return result;
        }

        // GET: /ECourts/GetOrders?cnrNumber=...&caseId=...&isHighCourt=...&module=...
        [AllowAnonymous]
        [HttpGet]
        public async Task<IActionResult> GetOrders(string? cnrNumber = null, int? caseId = null, bool isHighCourt = false, string? module = null)
        {
            string cnr = cnrNumber ?? "";
            if (string.IsNullOrWhiteSpace(cnr) && caseId.HasValue && caseId.Value > 0)
            {
                using var conn = _db.GetConnection();
                cnr = conn.ExecuteScalar<string>("SELECT CNRNumber FROM MVC_CASES WHERE CaseID = @caseId", new { caseId = caseId.Value }) ?? "";
                if (string.IsNullOrWhiteSpace(cnr))
                {
                    cnr = conn.ExecuteScalar<string>("SELECT CorpMFACNRNumber FROM APPEAL_DETAILS WHERE CaseID = @caseId", new { caseId = caseId.Value }) ?? "";
                }
            }

            if (string.IsNullOrWhiteSpace(cnr))
                return Json(new { success = true, data = Array.Empty<object>(), orders = Array.Empty<object>() });

            try
            {
                var liveRes = await LiveOrders(cnr, module ?? (isHighCourt ? "HC" : "MVC"));
                if (liveRes is JsonResult jr && jr.Value != null)
                {
                    var ordProp = jr.Value.GetType().GetProperty("orders")?.GetValue(jr.Value, null);
                    return Json(new { success = true, data = ordProp ?? Array.Empty<object>(), orders = ordProp ?? Array.Empty<object>() });
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "GetOrders fetch handled for CNR {CNR}", cnr);
            }

            return Json(new { success = true, data = Array.Empty<object>(), orders = Array.Empty<object>() });
        }

        // GET: /ECourts/GetCauseList?estCode=...&courtNo=...&causelistDate=...&type=civil
        [HttpGet]
        public async Task<IActionResult> GetCauseList(string estCode, string courtNo, string causelistDate, string type = "civil", bool isHighCourt = false)
        {
            if (string.IsNullOrWhiteSpace(estCode) || string.IsNullOrWhiteSpace(courtNo) || string.IsNullOrWhiteSpace(causelistDate))
            {
                return Json(new { success = false, message = "Establishment Code, Court No., and Cause List Date are required." });
            }

            if (!DateTime.TryParse(causelistDate, out DateTime parsedDate))
            {
                return Json(new { success = false, message = "Invalid date format. Expected YYYY-MM-DD." });
            }

            try
            {
                // 1. Check local DB cache first
                try
                {
                    var localCauselist = await _ecourtsRepo.GetCauselistAsync(estCode, courtNo, parsedDate, type);
                    if (localCauselist != null && localCauselist.Items != null && localCauselist.Items.Count > 0)
                    {
                        return Json(new { success = true, source = "local", data = localCauselist });
                    }
                }
                catch (Exception dbEx)
                {
                    _logger.LogWarning(dbEx, "Local causelist cache query failed. Proceeding to live gateway.");
                }

                // 2. Query Live NAPIX Gateway
                var liveData = await _napixService.GetCauselistAsync(estCode, courtNo, causelistDate, type, isHighCourt);
                if (liveData.HasValue)
                {
                    return Json(new { success = true, source = "live", data = liveData.Value });
                }

                return Json(new { success = false, message = "No Cause List available from eCourts Gateway for the selected establishment and date." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetCauseList for estCode: {EstCode}, courtNo: {CourtNo}, date: {Date}", estCode, courtNo, causelistDate);
                return Json(new { success = false, message = "Error communicating with eCourts Gateway: " + ex.Message });
            }
        }


        private class CnrFullDetails
        {
            public string CnrNumber { get; set; } = "";
            public string CourtAndJudge { get; set; } = "";
            public string EstablishmentName { get; set; } = "";
            public string CaseNoAndYear { get; set; } = "";
            public string Petitioner { get; set; } = "";
            public string Respondent { get; set; } = "";
            public string Stage { get; set; } = "";
            public string FilingDate { get; set; } = "";
            public string FirstHearingDate { get; set; } = "";
            public string NextHearingDate { get; set; } = "";
            public string DecisionDate { get; set; } = "";
            public string CaseStatus { get; set; } = "";
        }

        private static bool IsValidStatusString(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return false;
            string t = s.Trim();
            if (t.Contains("INVALID", StringComparison.OrdinalIgnoreCase) ||
                t.Contains("TOKEN", StringComparison.OrdinalIgnoreCase) ||
                t.Contains("FAIL", StringComparison.OrdinalIgnoreCase) ||
                t.Contains("ERROR", StringComparison.OrdinalIgnoreCase) ||
                t.StartsWith("wh+", StringComparison.OrdinalIgnoreCase)) return false;
            if (t.Length > 20 && (t.Contains("+") || t.Contains("/") || t.Contains("=") || !t.Contains(" "))) return false;
            return true;
        }

        private static CnrFullDetails ExtractFullCnrData(System.Text.Json.JsonElement root)
        {
            var details = new CnrFullDetails();
            var flatMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            void Walk(System.Text.Json.JsonElement elem, string prefix)
            {
                if (elem.ValueKind == System.Text.Json.JsonValueKind.Object)
                {
                    foreach (var prop in elem.EnumerateObject())
                    {
                        string key = string.IsNullOrEmpty(prefix) ? prop.Name : $"{prefix}.{prop.Name}";
                        if (prop.Value.ValueKind == System.Text.Json.JsonValueKind.String)
                        {
                            string val = prop.Value.GetString()?.Trim() ?? "";
                            if (!string.IsNullOrEmpty(val))
                            {
                                flatMap[prop.Name] = val;
                                flatMap[key] = val;
                            }
                        }
                        else if (prop.Value.ValueKind == System.Text.Json.JsonValueKind.Object ||
                                 prop.Value.ValueKind == System.Text.Json.JsonValueKind.Array)
                        {
                            Walk(prop.Value, key);
                        }
                    }
                }
                else if (elem.ValueKind == System.Text.Json.JsonValueKind.Array)
                {
                    int i = 0;
                    foreach (var item in elem.EnumerateArray())
                    {
                        Walk(item, $"{prefix}[{i++}]");
                    }
                }
            }

            Walk(root, "");

            string FindVal(params string[] keyNames)
            {
                foreach (var k in keyNames)
                {
                    if (flatMap.TryGetValue(k, out var v) && !string.IsNullOrWhiteSpace(v))
                        return v;
                }
                foreach (var kvp in flatMap)
                {
                    foreach (var k in keyNames)
                    {
                        if (kvp.Key.Equals(k, StringComparison.OrdinalIgnoreCase) ||
                            kvp.Key.EndsWith("." + k, StringComparison.OrdinalIgnoreCase))
                        {
                            if (!string.IsNullOrWhiteSpace(kvp.Value)) return kvp.Value;
                        }
                    }
                }
                return "";
            }

            details.CnrNumber = FindVal("cino", "cnr", "cnr_number", "cnrNo", "cino_no");

            string cJudge = FindVal("court_number_and_judge", "court_no_judge", "court_number_judge", "desgname", "judge_name", "presiding_officer", "court_judge", "judge", "judicial_officer");
            string cNo = FindVal("court_no", "court_number", "courtNo");
            if (!string.IsNullOrEmpty(cNo) && !string.IsNullOrEmpty(cJudge) && !cJudge.Contains(cNo))
                details.CourtAndJudge = $"{cNo} — {cJudge}";
            else
                details.CourtAndJudge = cJudge;

            details.EstablishmentName = FindVal("establishment_name", "est_name", "court_name", "court_establishment", "district_name", "court_complex_name");

            string lcCaseNo = FindVal("lower_court_caseno", "case_no", "caseno", "case_number");
            if (!string.IsNullOrEmpty(lcCaseNo))
            {
                details.CaseNoAndYear = lcCaseNo;
            }
            else
            {
                string cType = FindVal("case_type", "type_name", "casetype");
                string rNo = FindVal("reg_no", "registration_no");
                string rYr = FindVal("reg_year", "registration_year", "case_year");
                if (!string.IsNullOrEmpty(rNo))
                    details.CaseNoAndYear = $"{cType} {rNo} / {rYr}".Trim();
            }

            details.Petitioner = FindVal("petitioner_and_advocate", "pet_name", "petitioner_name", "petitioner", "party1", "plaintiff", "appellant", "claimant", "complainant");
            details.Respondent = FindVal("respondent_and_advocate", "res_name", "respondent_name", "respondent", "party2", "defendant", "resp_name");
            details.Stage = FindVal("purpose_name", "stage", "court_stage", "purposeName", "case_stage", "next_purpose");
            details.FilingDate = FindVal("date_of_filing", "filing_date", "registration_date", "reg_date", "dt_regis");
            details.FirstHearingDate = FindVal("first_hearing_date", "date_first_list", "dt_first_list", "first_date");
            details.NextHearingDate = FindVal("date_next_list", "next_date", "next_hearing_date", "nextDate");
            details.DecisionDate = FindVal("decision_date", "date_of_decision", "disposal_date", "dt_decision");

            string rawStatus = FindVal("case_status", "nature_of_disposal", "nature_disposal", "status", "pend_disp", "current_status");
            if (!string.IsNullOrEmpty(rawStatus) && IsValidStatusString(rawStatus))
            {
                if (rawStatus.Equals("P", StringComparison.OrdinalIgnoreCase)) details.CaseStatus = "Pending";
                else if (rawStatus.Equals("D", StringComparison.OrdinalIgnoreCase)) details.CaseStatus = "Disposed";
                else details.CaseStatus = rawStatus;
            }

            return details;
        }

        // GET: /ECourts/ViewOrderPdf?url=...&cnr=...&title=...&date=...&caseId=...&orderNo=...&module=...
        [AllowAnonymous]
        [HttpGet]
        public async Task<IActionResult> ViewOrderPdf(string? url = null, string? cnr = null, string? title = null, string? date = null, int? caseId = null, string? orderNo = null, string? module = null)
        {
            if (string.IsNullOrWhiteSpace(module))
            {
                var referer = Request.Headers["Referer"].ToString();
                module = referer.Contains("/Labour", StringComparison.OrdinalIgnoreCase) ? "Labour" : "MVC";
            }
            string norm = NapixQuotaService.NormalizeModule(module);

            string targetCnr = !string.IsNullOrWhiteSpace(cnr) ? cnr.Trim().ToUpperInvariant() : "";
            if (targetCnr.Contains("PENDING") || targetCnr == "N/A" || targetCnr == "UNDEFINED" || targetCnr == "NULL")
            {
                targetCnr = "";
            }

            string decodedUrl = !string.IsNullOrWhiteSpace(url) ? System.Net.WebUtility.UrlDecode(url).Trim() : "";

            // Extract caseId from url if url is like "/Case/Details/123"
            if (!caseId.HasValue && !string.IsNullOrWhiteSpace(decodedUrl))
            {
                var match = System.Text.RegularExpressions.Regex.Match(decodedUrl, @"/Case/Details/(\d+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                if (match.Success && int.TryParse(match.Groups[1].Value, out int extractedId))
                {
                    caseId = extractedId;
                }
            }

            // Extract query parameters from decodedUrl if cnr, orderNo, date were missing or blank
            if (!string.IsNullOrWhiteSpace(decodedUrl) && decodedUrl.Contains('?'))
            {
                try
                {
                    var qIndex = decodedUrl.IndexOf('?');
                    var query = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(decodedUrl.Substring(qIndex));
                    if (string.IsNullOrWhiteSpace(targetCnr))
                    {
                        if (query.TryGetValue("cino", out var cinoVal)) targetCnr = cinoVal.ToString().Trim().ToUpperInvariant();
                        else if (query.TryGetValue("cnr", out var cnrVal)) targetCnr = cnrVal.ToString().Trim().ToUpperInvariant();
                        else if (query.TryGetValue("cnr_number", out var cnrVal2)) targetCnr = cnrVal2.ToString().Trim().ToUpperInvariant();
                    }
                    if (string.IsNullOrWhiteSpace(orderNo))
                    {
                        if (query.TryGetValue("order_no", out var oNoVal)) orderNo = oNoVal.ToString().Trim();
                        else if (query.TryGetValue("orderNo", out var oNoVal2)) orderNo = oNoVal2.ToString().Trim();
                        else if (query.TryGetValue("sr_no", out var oNoVal3)) orderNo = oNoVal3.ToString().Trim();
                    }
                    if (string.IsNullOrWhiteSpace(date))
                    {
                        if (query.TryGetValue("order_date", out var oDtVal)) date = oDtVal.ToString().Trim();
                        else if (query.TryGetValue("date", out var oDtVal2)) date = oDtVal2.ToString().Trim();
                        else if (query.TryGetValue("hearing_date", out var oDtVal3)) date = oDtVal3.ToString().Trim();
                    }
                    if (string.IsNullOrWhiteSpace(title))
                    {
                        if (query.TryGetValue("title", out var tVal)) title = tVal.ToString().Trim();
                        else if (query.TryGetValue("order_type", out var tVal2)) title = tVal2.ToString().Trim();
                    }
                }
                catch { }
            }

            // 1. If base64 PDF
            if (decodedUrl.StartsWith("data:application/pdf;base64,", StringComparison.OrdinalIgnoreCase))
            {
                var base64Data = decodedUrl.Substring("data:application/pdf;base64,".Length);
                byte[] pdfBytes = Convert.FromBase64String(base64Data);
                Response.Headers.Append("Content-Disposition", $"inline; filename=\"eCourts_Order_{targetCnr}_{(orderNo ?? "1")}.pdf\"");
                return File(pdfBytes, "application/pdf");
            }

            // 2. Try physical file if local path in wwwroot
            if (!string.IsNullOrWhiteSpace(decodedUrl) && !decodedUrl.StartsWith("/Case/Details", StringComparison.OrdinalIgnoreCase) && !decodedUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    string webRoot = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "wwwroot"));
                    string contentRoot = Path.GetFullPath(Directory.GetCurrentDirectory());
                    string relativePath = decodedUrl.TrimStart('/', '\\');
                    string fullPath = Path.GetFullPath(Path.Combine(webRoot, relativePath));
                    
                    if (fullPath.StartsWith(webRoot, StringComparison.OrdinalIgnoreCase) || fullPath.StartsWith(contentRoot, StringComparison.OrdinalIgnoreCase))
                    {
                        if (System.IO.File.Exists(fullPath))
                        {
                            Response.Headers.Append("Content-Disposition", $"inline; filename=\"eCourts_Order_{targetCnr}_{(orderNo ?? "1")}.pdf\"");
                            return PhysicalFile(fullPath, "application/pdf");
                        }
                    }
                }
                catch { }
            }

            // 0. Direct NAPIX gateway via ECourtsNapixService (encrypted AES call)
            if (!string.IsNullOrWhiteSpace(targetCnr) && targetCnr.Length >= 10)
            {
                try
                {
                    bool isHc = targetCnr.StartsWith("KAHC", StringComparison.OrdinalIgnoreCase) || (targetCnr.Length >= 4 && targetCnr.Substring(2, 2).Equals("HC", StringComparison.OrdinalIgnoreCase));
                    var pdfBytes = await _napixService.GetOrderPdfBytesAsync(targetCnr, orderNo ?? "1", date ?? "", isHc, module: norm);
                    if (pdfBytes != null && pdfBytes.Length > 100 && pdfBytes[0] == 0x25 && pdfBytes[1] == 0x50 && pdfBytes[2] == 0x44 && pdfBytes[3] == 0x46)
                    {
                        Response.Headers.Append("Content-Disposition", $"inline; filename=\"eCourts_Order_{targetCnr}_{(orderNo ?? "1")}.pdf\"");
                        return File(pdfBytes, "application/pdf");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogInformation(ex, "[ViewOrderPdf] Direct NAPIX gateway order PDF fetch: {Msg}", ex.Message);
                }
            }

            // If decodedUrl is a relative e-Courts portal path (e.g. display_pdf.php?..., cases/..., pdf_viewer.php...)
            if (!string.IsNullOrWhiteSpace(decodedUrl) && 
                !decodedUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && 
                !decodedUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase) && 
                !decodedUrl.StartsWith("data:", StringComparison.OrdinalIgnoreCase) && 
                !decodedUrl.StartsWith("/Case/Details", StringComparison.OrdinalIgnoreCase) && 
                !decodedUrl.StartsWith("/uploads/", StringComparison.OrdinalIgnoreCase) &&
                !decodedUrl.StartsWith("uploads/", StringComparison.OrdinalIgnoreCase) &&
                (decodedUrl.Contains(".php") || decodedUrl.Contains("display_pdf") || decodedUrl.Contains("pdf_viewer") || (decodedUrl.Contains("cases/") && !decodedUrl.Contains("uploads"))))
            {
                bool isHc = targetCnr.StartsWith("KAHC", StringComparison.OrdinalIgnoreCase) || decodedUrl.Contains("hcservices");
                string baseDomain = isHc ? "https://hcservices.ecourts.gov.in/hcservices/" : "https://services.ecourts.gov.in/ecourtIndia_v6/";
                decodedUrl = baseDomain + decodedUrl.TrimStart('/', '\\');
            }

            // Direct HTTP / HTTPS link — validate with UrlSecurityValidator (SSRF protection)
            if ((decodedUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                 decodedUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) &&
                !decodedUrl.Contains("/Case/Details", StringComparison.OrdinalIgnoreCase))
            {
                if (UrlSecurityValidator.IsSafeExternalUrl(decodedUrl, out var safeUri))
                {
                    try
                    {
                        using var handler = new System.Net.Http.SocketsHttpHandler
                        {
                            AllowAutoRedirect = true,
                            MaxAutomaticRedirections = 3
                        };
                        using var client = new System.Net.Http.HttpClient(handler);
                        client.Timeout = TimeSpan.FromSeconds(8);
                        client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36");
                        client.DefaultRequestHeaders.Add("Accept", "application/pdf,*/*;q=0.9");
                        client.DefaultRequestHeaders.Add("Referer", safeUri.Host.Contains("hcservices")
                            ? "https://hcservices.ecourts.gov.in/"
                            : "https://services.ecourts.gov.in/");
                        
                        var response = await client.GetAsync(safeUri);

                        if (response.IsSuccessStatusCode)
                        {
                            var bytes = await response.Content.ReadAsByteArrayAsync();
                            string contentType = response.Content.Headers.ContentType?.MediaType ?? "application/pdf";
                            if (bytes.Length > 100 && (contentType.Contains("pdf", StringComparison.OrdinalIgnoreCase) || (bytes[0] == 0x25 && bytes[1] == 0x50 && bytes[2] == 0x44 && bytes[3] == 0x46)))
                            {
                                Response.Headers.Append("Content-Disposition", $"inline; filename=\"eCourts_Order_{targetCnr}_{(orderNo ?? "1")}.pdf\"");
                                return File(bytes, "application/pdf");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to proxy fetch PDF from safe URL {Url}", decodedUrl);
                    }
                }
                else
                {
                    _logger.LogWarning("Blocked potentially unsafe external URL in ViewOrderPdf: {Url}", decodedUrl);
                }

                // NOTE: Do not return BadRequest here. If direct GET to portal fails (session/cookie/captcha required),
                // fall through gracefully to live NAPIX queries, candidate URLs, DB uploads, and the official notice card!
            }

            // 4. Check if Labour case or look up case from local DB
            bool isLabour = norm.Equals("Labour", StringComparison.OrdinalIgnoreCase);
            dynamic? labourCase = null;
            if (isLabour || (!string.IsNullOrWhiteSpace(targetCnr) && targetCnr.Length >= 10))
            {
                using var lConn = _db.GetConnection();
                if (isLabour && caseId.HasValue && caseId.Value > 0)
                {
                    labourCase = lConn.QueryFirstOrDefault("SELECT TOP 1 * FROM LABOUR_CASES WHERE CaseID = @caseId", new { caseId = caseId.Value });
                }
                if (labourCase == null && !string.IsNullOrWhiteSpace(targetCnr))
                {
                    string cleanTargetCnr = targetCnr.Replace("-", "").Replace(" ", "");
                    labourCase = lConn.QueryFirstOrDefault(@"
                        SELECT TOP 1 * FROM LABOUR_CASES 
                        WHERE (REPLACE(REPLACE(CO_WP_CNRNumber, '-', ''), ' ', '') = @cleanTargetCnr AND LEN(CO_WP_CNRNumber) >= 10)
                           OR (REPLACE(REPLACE(CO_WA_CNRNumber, '-', ''), ' ', '') = @cleanTargetCnr AND LEN(CO_WA_CNRNumber) >= 10)
                           OR (REPLACE(REPLACE(CO_Claimant_CNRNumber, '-', ''), ' ', '') = @cleanTargetCnr AND LEN(CO_Claimant_CNRNumber) >= 10)
                           OR (REPLACE(REPLACE(CNRNumber, '-', ''), ' ', '') = @cleanTargetCnr AND LEN(CNRNumber) >= 10)
                        ORDER BY CaseID DESC", new { cleanTargetCnr });
                }
                if (labourCase != null)
                {
                    isLabour = true;
                }
            }

            if (isLabour)
            {
                try
                {
                    using var localConn = _db.GetConnection();
                    var candidatePaths = new List<string?>();

                    // 1. Check CASE_ORDERS table for any locally uploaded signed order PDFs for this CNR
                    if (!string.IsNullOrWhiteSpace(targetCnr))
                    {
                        string cleanTargetCnr = targetCnr.Replace("-", "").Replace(" ", "");
                        var caseOrderFiles = localConn.Query<string>(@"
                            SELECT LocalPdfPath FROM CASE_ORDERS 
                            WHERE (REPLACE(REPLACE(CNRNumber, '-', ''), ' ', '') = @cleanTargetCnr OR CNRNumber = @targetCnr)
                              AND LocalPdfPath IS NOT NULL AND LocalPdfPath <> ''
                            ORDER BY OrderID DESC", new { targetCnr, cleanTargetCnr });
                        foreach (var cof in caseOrderFiles) candidatePaths.Add(cof);
                    }

                    // 2. Select matching document from labourCase based on title / orderNo
                    if (labourCase != null)
                    {
                        string tLower = (title ?? "").ToLowerInvariant();
                        if (tLower.Contains("stay"))
                        {
                            candidatePaths.Add(Convert.ToString(labourCase.CO_WP_StayOrderPath));
                            candidatePaths.Add(Convert.ToString(labourCase.CO_WA_StayOrderPath));
                            candidatePaths.Add(Convert.ToString(labourCase.CO_Reinstatement_StayOrderPath));
                        }
                        else if (tLower.Contains("wp") || tLower.Contains("high court") || tLower.Contains("final judgment"))
                        {
                            candidatePaths.Add(Convert.ToString(labourCase.CO_WP_JudgmentCopyPath));
                            candidatePaths.Add(Convert.ToString(labourCase.JudgmentCopyPath));
                            candidatePaths.Add(Convert.ToString(labourCase.JudgmentCopyPath2));
                        }
                        else if (tLower.Contains("petition") || tLower.Contains("claim"))
                        {
                            candidatePaths.Add(Convert.ToString(labourCase.ClaimPetitionPath));
                        }
                        else if (tLower.Contains("labour") || tLower.Contains("award"))
                        {
                            candidatePaths.Add(Convert.ToString(labourCase.JudgmentCopyPath));
                            candidatePaths.Add(Convert.ToString(labourCase.JudgmentCopyPath2));
                            candidatePaths.Add(Convert.ToString(labourCase.CO_WP_JudgmentCopyPath));
                        }
                        else
                        {
                            // Priority by order number if supplied
                            if (orderNo == "1")
                            {
                                candidatePaths.Add(Convert.ToString(labourCase.CO_WP_StayOrderPath));
                                candidatePaths.Add(Convert.ToString(labourCase.CO_WP_JudgmentCopyPath));
                                candidatePaths.Add(Convert.ToString(labourCase.JudgmentCopyPath));
                            }
                            else if (orderNo == "2")
                            {
                                candidatePaths.Add(Convert.ToString(labourCase.CO_WP_JudgmentCopyPath));
                                candidatePaths.Add(Convert.ToString(labourCase.CO_WA_StayOrderPath));
                                candidatePaths.Add(Convert.ToString(labourCase.JudgmentCopyPath2));
                            }
                            else
                            {
                                candidatePaths.Add(Convert.ToString(labourCase.CO_WP_JudgmentCopyPath));
                                candidatePaths.Add(Convert.ToString(labourCase.CO_WP_StayOrderPath));
                                candidatePaths.Add(Convert.ToString(labourCase.JudgmentCopyPath));
                                candidatePaths.Add(Convert.ToString(labourCase.CO_WA_StayOrderPath));
                                candidatePaths.Add(Convert.ToString(labourCase.CO_Reinstatement_StayOrderPath));
                                candidatePaths.Add(Convert.ToString(labourCase.JudgmentCopyPath2));
                                candidatePaths.Add(Convert.ToString(labourCase.ClaimPetitionPath));
                            }
                        }
                    }

                    string webRoot = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
                    if (!Directory.Exists(webRoot)) webRoot = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "wwwroot");

                    foreach (var rel in candidatePaths)
                    {
                        if (string.IsNullOrWhiteSpace(rel)) continue;
                        string full = Path.Combine(webRoot, rel.TrimStart('/', '\\'));
                        if (System.IO.File.Exists(full) && full.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                        {
                            _logger.LogInformation("[ViewOrderPdf] Serving Labour authentic PDF immediately: {Path}", full);
                            Response.Headers.Append("Content-Disposition", $"inline; filename=\"eCourts_Order_{targetCnr ?? "document"}.pdf\"");
                            return PhysicalFile(full, "application/pdf");
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[ViewOrderPdf] Error checking local Labour documents for CNR {CNR}", targetCnr);
                }
            }

            MVCCaseViewModel? mvcCase = null;
            if (!isLabour)
            {
                if (caseId.HasValue && caseId.Value > 0)
                {
                    mvcCase = _caseRepo.GetCaseById(caseId.Value);
                }

                if (mvcCase == null && !string.IsNullOrWhiteSpace(targetCnr))
                {
                    using var conn = _db.GetConnection();
                    string cleanTargetCnr = targetCnr.Replace("-", "").Replace(" ", "");
                    int foundId = conn.ExecuteScalar<int>(@"
                        SELECT TOP 1 CaseID FROM MVC_CASES 
                        WHERE (REPLACE(REPLACE(CNRNumber, '-', ''), ' ', '') = @cleanTargetCnr AND LEN(CNRNumber) >= 10)
                           OR (CNRNumber = @targetCnr AND LEN(CNRNumber) >= 10)
                           OR EstCode + RIGHT('000000' + CAST(MVCNo AS VARCHAR), 6) + CAST(MVCYear AS VARCHAR) = @cleanTargetCnr", 
                        new { targetCnr, cleanTargetCnr });

                    if (foundId == 0)
                    {
                        foundId = conn.ExecuteScalar<int>(@"
                            SELECT TOP 1 CaseID FROM APPEAL_DETAILS 
                            WHERE (REPLACE(REPLACE(CorpMFACNRNumber, '-', ''), ' ', '') = @cleanTargetCnr AND LEN(CorpMFACNRNumber) >= 10)
                               OR (REPLACE(REPLACE(ClaimantMFACNRNumber, '-', ''), ' ', '') = @cleanTargetCnr AND LEN(ClaimantMFACNRNumber) >= 10)
                               OR (REPLACE(REPLACE(CNRNumber, '-', ''), ' ', '') = @cleanTargetCnr AND LEN(CNRNumber) >= 10)
                               OR (CorpMFACNRNumber = @targetCnr AND LEN(CorpMFACNRNumber) >= 10)
                               OR (ClaimantMFACNRNumber = @targetCnr AND LEN(ClaimantMFACNRNumber) >= 10)", 
                            new { targetCnr, cleanTargetCnr });
                    }

                    if (foundId == 0)
                    {
                        var tracked = conn.QueryFirstOrDefault(@"
                            SELECT RegNo, RegYear FROM TRACKED_CASES 
                            WHERE (REPLACE(REPLACE(CNRNumber, '-', ''), ' ', '') = @cleanTargetCnr AND LEN(CNRNumber) >= 10)
                               OR (CNRNumber = @targetCnr AND LEN(CNRNumber) >= 10)", new { targetCnr, cleanTargetCnr });

                        if (tracked != null)
                        {
                            string rNo = Convert.ToString(tracked.RegNo) ?? "";
                            int rYr = Convert.ToInt32(tracked.RegYear);
                            foundId = conn.ExecuteScalar<int>(@"
                                SELECT TOP 1 CaseID FROM MVC_CASES 
                                WHERE (MVCNo = @rNo OR MVCNo = CAST(@rNoInt AS VARCHAR)) AND MVCYear = @rYr", 
                                new { rNo, rNoInt = int.TryParse(rNo, out int pNo) ? pNo : 0, rYr });
                        }
                    }

                    if (foundId > 0)
                    {
                        mvcCase = _caseRepo.GetCaseById(foundId);
                    }
                }

                // Ensure mvcCase and targetCnr are in sync and updated in the DB
                if (mvcCase != null)
                {
                    using var conn = _db.GetConnection();
                    if (!string.IsNullOrWhiteSpace(targetCnr) && (string.IsNullOrWhiteSpace(mvcCase.CNRNumber) || mvcCase.CNRNumber != targetCnr))
                    {
                        if (!targetCnr.StartsWith("KAHC", StringComparison.OrdinalIgnoreCase))
                        {
                            mvcCase.CNRNumber = targetCnr;
                            try
                            {
                                conn.Execute("UPDATE MVC_CASES SET CNRNumber = @targetCnr WHERE CaseID = @caseId", new { targetCnr, caseId = mvcCase.CaseID });
                            }
                            catch { }
                        }
                    }
                    else if (string.IsNullOrWhiteSpace(targetCnr))
                    {
                        if (!string.IsNullOrWhiteSpace(mvcCase.CNRNumber))
                        {
                            targetCnr = mvcCase.CNRNumber.Trim().ToUpperInvariant();
                        }
                        else
                        {
                            try
                            {
                                string trackedCnr = conn.ExecuteScalar<string>(@"
                                    SELECT TOP 1 CNRNumber FROM TRACKED_CASES 
                                    WHERE (RegNo = @mvcNo OR RegNo = CAST(@mvcNoInt AS VARCHAR) OR RegNo LIKE '%' + @mvcNo + '%') 
                                    AND RegYear = @mvcYear AND CNRNumber IS NOT NULL AND CNRNumber <> ''", 
                                    new { mvcNo = mvcCase.MVCNo, mvcNoInt = int.TryParse(mvcCase.MVCNo, out int pNo) ? pNo : 0, mvcYear = mvcCase.MVCYear }) ?? "";

                                if (!string.IsNullOrWhiteSpace(trackedCnr))
                                {
                                    targetCnr = trackedCnr.Trim().ToUpperInvariant();
                                    mvcCase.CNRNumber = targetCnr;
                                    conn.Execute("UPDATE MVC_CASES SET CNRNumber = @targetCnr WHERE CaseID = @caseId", new { targetCnr, caseId = mvcCase.CaseID });
                                }
                            }
                            catch { }
                        }
                    }
                }

                // 4b. Immediate Local Check: If signed order or judgment PDF exists in local DB or wwwroot/uploads, serve it immediately (<10ms)
                if (mvcCase != null || !string.IsNullOrWhiteSpace(targetCnr))
                {
                    try
                    {
                        using var localConn = _db.GetConnection();
                        dynamic? adverseAward = null;
                        if (mvcCase != null)
                        {
                            try { adverseAward = localConn.QueryFirstOrDefault("SELECT TOP 1 * FROM MVC_CASE_ADVERSE_DETAILS WHERE CaseID = @caseId", new { caseId = mvcCase.CaseID }); } catch { }
                        }

                        dynamic? appealDetail = null;
                        if (mvcCase != null)
                        {
                            try { appealDetail = localConn.QueryFirstOrDefault("SELECT TOP 1 * FROM APPEAL_DETAILS WHERE CaseID = @caseId", new { caseId = mvcCase.CaseID }); } catch { }
                        }

                        bool isHcTarget = (!string.IsNullOrWhiteSpace(targetCnr) && targetCnr.StartsWith("KAHC", StringComparison.OrdinalIgnoreCase)) || (!string.IsNullOrWhiteSpace(title) && (title.Contains("MFA", StringComparison.OrdinalIgnoreCase) || title.Contains("High Court", StringComparison.OrdinalIgnoreCase) || title.Contains("Appeal", StringComparison.OrdinalIgnoreCase)));

                        var candidatePaths = new List<string?>();

                        // 1. Check CASE_ORDERS table for any locally uploaded signed order PDFs for this CNR
                        if (!string.IsNullOrWhiteSpace(targetCnr))
                        {
                            try
                            {
                                string cleanTargetCnr = targetCnr.Replace("-", "").Replace(" ", "");
                                var caseOrderFiles = localConn.Query<string>(@"
                                    SELECT LocalPdfPath FROM CASE_ORDERS 
                                    WHERE (REPLACE(REPLACE(CNRNumber, '-', ''), ' ', '') = @cleanTargetCnr OR CNRNumber = @targetCnr)
                                      AND LocalPdfPath IS NOT NULL AND LocalPdfPath <> ''
                                    ORDER BY OrderID DESC", new { targetCnr, cleanTargetCnr });
                                foreach (var cof in caseOrderFiles) candidatePaths.Add(cof);
                            }
                            catch { }
                        }

                        if (isHcTarget)
                        {
                            candidatePaths.Add(Convert.ToString(appealDetail?.MFAJudgmentCopyPath));
                            candidatePaths.Add(Convert.ToString(appealDetail?.CorpMFAActionTakenPath));
                            candidatePaths.Add(Convert.ToString(appealDetail?.InitialActionPath1));
                            candidatePaths.Add(Convert.ToString(appealDetail?.InitialActionPath2));
                            candidatePaths.Add(Convert.ToString(appealDetail?.StayOrderPath1));
                            candidatePaths.Add(Convert.ToString(appealDetail?.StayOrderPath2));
                            candidatePaths.Add(Convert.ToString(appealDetail?.ComplianceLetterPath));
                            candidatePaths.Add(Convert.ToString(appealDetail?.ClaimantSCJudgmentPath));
                            candidatePaths.Add(adverseAward?.AdverseJudgmentUploadPath);
                        }
                        else
                        {
                            candidatePaths.Add(adverseAward?.AdverseJudgmentUploadPath);
                            if (mvcCase != null) candidatePaths.Add(mvcCase.InterimOrderFilePath);
                            candidatePaths.Add(adverseAward?.DelayCondonedOrderPath);
                            candidatePaths.Add(adverseAward?.TR18UploadPath);
                            candidatePaths.Add(adverseAward?.PunishmentOrderUploadPath);
                            candidatePaths.Add(adverseAward?.DelayApplicationPath);
                            candidatePaths.Add(Convert.ToString(appealDetail?.MFAJudgmentCopyPath));
                            candidatePaths.Add(Convert.ToString(appealDetail?.CorpMFAActionTakenPath));
                        }

                        string webRoot = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
                        if (!Directory.Exists(webRoot)) webRoot = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "wwwroot");

                        foreach (var rel in candidatePaths)
                        {
                            if (string.IsNullOrWhiteSpace(rel)) continue;
                            string full = Path.Combine(webRoot, rel.TrimStart('/', '\\'));
                            if (System.IO.File.Exists(full) && full.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                            {
                                _logger.LogInformation("[ViewOrderPdf] Serving local authentic PDF immediately: {Path}", full);
                                Response.Headers.Append("Content-Disposition", $"inline; filename=\"eCourts_Order_{targetCnr ?? "document"}.pdf\"");
                                return PhysicalFile(full, "application/pdf");
                            }
                        }

                        // Check uploads directory for files matching case number / year
                        string uploadsDir = Path.Combine(webRoot, "uploads");
                        if (Directory.Exists(uploadsDir) && mvcCase != null)
                        {
                            string rawMvcNo = mvcCase.MVCNo ?? "";
                            string mvcDigits = System.Text.RegularExpressions.Regex.Match(rawMvcNo, @"\d+").Value;
                            string mvcYear = Convert.ToString(mvcCase.MVCYear);

                            if (!string.IsNullOrWhiteSpace(mvcDigits) && mvcDigits.Length >= 2)
                            {
                                var pdfFiles = Directory.GetFiles(uploadsDir, "*.pdf", SearchOption.AllDirectories);
                                foreach (var pf in pdfFiles)
                                {
                                    string fn = Path.GetFileName(pf);
                                    if (fn.Contains(mvcDigits, StringComparison.OrdinalIgnoreCase) &&
                                        (string.IsNullOrWhiteSpace(mvcYear) || fn.Contains(mvcYear, StringComparison.OrdinalIgnoreCase)))
                                    {
                                        _logger.LogInformation("[ViewOrderPdf] Found matching case award in uploads: {Path}", pf);
                                        Response.Headers.Append("Content-Disposition", $"inline; filename=\"eCourts_Order_{targetCnr ?? "document"}.pdf\"");
                                        return PhysicalFile(pf, "application/pdf");
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "Early local PDF check error");
                    }
                }
            }

            // 5. Query eCourts NAPIX API for live details by CNR number (or auto-discover)
            var apiDetails = new CnrFullDetails();
            System.Text.Json.JsonElement? liveCnr = null;

            if (!string.IsNullOrWhiteSpace(targetCnr) && targetCnr != "N/A" && targetCnr.Length >= 10)
            {
                try
                {
                    bool isHc = targetCnr.StartsWith("KAHC", StringComparison.OrdinalIgnoreCase);
                    liveCnr = await _napixService.GetCnrDetailsAsync(targetCnr, isHc, module: norm);
                    if (liveCnr.HasValue)
                    {
                        apiDetails = ExtractFullCnrData(liveCnr.Value);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Live NAPIX CNR query failed in ViewOrderPdf for CNR {CNR}", targetCnr);
                }
            }
            else if (mvcCase != null && !string.IsNullOrWhiteSpace(mvcCase.EstCode) && !string.IsNullOrWhiteSpace(mvcCase.MVCNo) && mvcCase.MVCYear > 0)
            {
                try
                {
                    string? discCnr = await _napixService.DiscoverCnrAsync(mvcCase.EstCode, "MVC", mvcCase.MVCNo, mvcCase.MVCYear.ToString(), false, module: norm);
                    if (!string.IsNullOrWhiteSpace(discCnr))
                    {
                        targetCnr = discCnr.Trim().ToUpperInvariant();
                        mvcCase.CNRNumber = targetCnr;
                        liveCnr = await _napixService.GetCnrDetailsAsync(targetCnr, false, module: norm);
                        if (liveCnr.HasValue)
                        {
                            apiDetails = ExtractFullCnrData(liveCnr.Value);
                        }
                    }
                }
                catch { }
            }

            bool isHcCase = targetCnr.StartsWith("KAHC", StringComparison.OrdinalIgnoreCase);

            dynamic? appRecord = null;
            if (mvcCase != null)
            {
                using var conn = _db.GetConnection();
                appRecord = conn.QueryFirstOrDefault(@"
                    SELECT TOP 1 * FROM APPEAL_DETAILS 
                    WHERE CaseID = @caseId AND (CorpMFACNRNumber = @targetCnr OR ClaimantMFACNRNumber = @targetCnr OR CorpMFACNRNumber IS NOT NULL OR ClaimantMFACNRNumber IS NOT NULL)", 
                    new { caseId = mvcCase.CaseID, targetCnr });

                if (!liveCnr.HasValue && appRecord != null)
                {
                    string? hcCnr = Convert.ToString(appRecord.ClaimantMFACNRNumber) ?? Convert.ToString(appRecord.CorpMFACNRNumber);
                    if (!string.IsNullOrWhiteSpace(hcCnr) && hcCnr.Trim().ToUpperInvariant() != targetCnr)
                    {
                        try
                        {
                            string cleanHcCnr = hcCnr.Trim().ToUpperInvariant();
                            liveCnr = await _napixService.GetCnrDetailsAsync(cleanHcCnr, true, module: norm);
                            if (liveCnr.HasValue)
                            {
                                apiDetails = ExtractFullCnrData(liveCnr.Value);
                                targetCnr = cleanHcCnr;
                                isHcCase = true;
                            }
                        }
                        catch { }
                    }
                }
            }

            string displayCnr = (!string.IsNullOrWhiteSpace(apiDetails.CnrNumber) && IsValidStatusString(apiDetails.CnrNumber))
                ? apiDetails.CnrNumber
                : (!string.IsNullOrWhiteSpace(targetCnr) ? targetCnr : (!string.IsNullOrWhiteSpace(mvcCase?.CNRNumber) ? mvcCase.CNRNumber : "CNR Pending eCourts Sync"));

            string displayCourt = (!string.IsNullOrWhiteSpace(apiDetails.CourtAndJudge) && IsValidStatusString(apiDetails.CourtAndJudge))
                ? apiDetails.CourtAndJudge
                : (isLabour && labourCase != null
                    ? (isHcCase ? (!string.IsNullOrWhiteSpace((string?)labourCase.CO_WP_HighCourtBench) ? $"HIGH COURT OF KARNATAKA, {((string)labourCase.CO_WP_HighCourtBench).ToUpper()} BENCH" : "HIGH COURT OF KARNATAKA") : (!string.IsNullOrWhiteSpace((string?)labourCase.CourtName) ? (string)labourCase.CourtName : "LABOUR COURT / INDUSTRIAL TRIBUNAL"))
                    : (isHcCase
                        ? $"HIGH COURT OF KARNATAKA, {((appRecord != null && !string.IsNullOrWhiteSpace(Convert.ToString(appRecord.HighCourtBench))) ? Convert.ToString(appRecord.HighCourtBench).ToUpper() : "DHARWAD")} BENCH"
                        : (!string.IsNullOrWhiteSpace(mvcCase?.MACTName) 
                            ? mvcCase.MACTName 
                            : (!string.IsNullOrWhiteSpace(mvcCase?.DivisionName) 
                                ? $"MACT COURT, {mvcCase.DivisionName.ToUpper()} DIVISION" 
                                : "MOTOR ACCIDENT CLAIMS TRIBUNAL (MACT)"))));

            string displayEst = (!string.IsNullOrWhiteSpace(apiDetails.EstablishmentName) && IsValidStatusString(apiDetails.EstablishmentName))
                ? apiDetails.EstablishmentName
                : (isLabour && labourCase != null
                    ? (isHcCase ? "HIGH COURT OF KARNATAKA" : (!string.IsNullOrWhiteSpace((string?)labourCase.CourtName) ? (string)labourCase.CourtName : "LABOUR COURT ESTABLISHMENT"))
                    : (isHcCase
                        ? "HIGH COURT OF KARNATAKA"
                        : (!string.IsNullOrWhiteSpace(mvcCase?.DivisionName) 
                            ? $"{mvcCase.DivisionName.ToUpper()} MACT ESTABLISHMENT" 
                            : (!string.IsNullOrWhiteSpace(mvcCase?.MACTName) ? mvcCase.MACTName : "MACT COURT ESTABLISHMENT"))));

            string displayCaseNo = (!string.IsNullOrWhiteSpace(apiDetails.CaseNoAndYear) && IsValidStatusString(apiDetails.CaseNoAndYear))
                ? apiDetails.CaseNoAndYear
                : (isLabour && labourCase != null
                    ? (isHcCase ? (!string.IsNullOrWhiteSpace((string?)labourCase.CO_WP_CaseNumber) ? $"WP No. {labourCase.CO_WP_CaseNumber} / {labourCase.CO_WP_Year}" : "High Court WP") : $"{labourCase.CaseType ?? "Ref / ID"} No. {labourCase.CaseNumber} / {labourCase.CaseYear}")
                    : (appRecord != null && (!string.IsNullOrWhiteSpace(Convert.ToString(appRecord.CorpMFANumber)) || !string.IsNullOrWhiteSpace(Convert.ToString(appRecord.ClaimantMFANumber)))
                        ? $"MFA No. {(Convert.ToString(appRecord.CorpMFANumber) ?? Convert.ToString(appRecord.ClaimantMFANumber))} / {(appRecord.CorpMFAYear ?? appRecord.ClaimantMFAYear ?? 2019)} (MVC No. {mvcCase?.MVCNo} / {mvcCase?.MVCYear})"
                        : (mvcCase != null && !string.IsNullOrWhiteSpace(mvcCase.MVCNo) 
                            ? $"MVC No. {mvcCase.MVCNo} / {mvcCase.MVCYear}" 
                            : "MVC Claim Petition Record")));

            string displayPetitioner = (!string.IsNullOrWhiteSpace(apiDetails.Petitioner) && IsValidStatusString(apiDetails.Petitioner))
                ? apiDetails.Petitioner
                : (isLabour && labourCase != null
                    ? (isHcCase ? "The Managing Director, NWKRTC" : (!string.IsNullOrWhiteSpace((string?)labourCase.WorkmanName) ? (string)labourCase.WorkmanName : "Workman / Petitioner"))
                    : (mvcCase?.Petitioners?.FirstOrDefault(p => !string.IsNullOrWhiteSpace(p.PetitionerName))?.PetitionerName 
                        ?? "Petitioner / Claimant"));

            string displayRespondent = (!string.IsNullOrWhiteSpace(apiDetails.Respondent) && IsValidStatusString(apiDetails.Respondent))
                ? apiDetails.Respondent
                : (isLabour && labourCase != null
                    ? (isHcCase ? (!string.IsNullOrWhiteSpace((string?)labourCase.WorkmanName) ? (string)labourCase.WorkmanName : "Workman / Respondent") : "The Managing Director, NWKRTC")
                    : (mvcCase?.Respondents?.FirstOrDefault(r => !string.IsNullOrWhiteSpace(r.RespondentName))?.RespondentName 
                        ?? "THE MANAGING DIRECTOR, NWKRTC"));

            string displayStage = (!string.IsNullOrWhiteSpace(apiDetails.Stage) && IsValidStatusString(apiDetails.Stage))
                ? apiDetails.Stage
                : (isLabour && labourCase != null
                    ? (!string.IsNullOrWhiteSpace((string?)labourCase.CurrentStage) ? (string)labourCase.CurrentStage : "HEARING / PROCEEDINGS")
                    : (appRecord != null && !string.IsNullOrWhiteSpace(Convert.ToString(appRecord.CorpMFAStage))
                        ? Convert.ToString(appRecord.CorpMFAStage)
                        : (!string.IsNullOrWhiteSpace(mvcCase?.CurrentStage) ? mvcCase.CurrentStage : "HEARING / PROCEEDINGS")));

            string displayStatus = (!string.IsNullOrWhiteSpace(apiDetails.CaseStatus) && IsValidStatusString(apiDetails.CaseStatus))
                ? apiDetails.CaseStatus
                : (isLabour && labourCase != null
                    ? (isHcCase ? (labourCase.CO_WP_Status ?? "Pending") : (labourCase.CurrentStatus ?? labourCase.Arising_CurrentStatus ?? "Pending"))
                    : (!string.IsNullOrWhiteSpace(mvcCase?.CaseType) ? mvcCase.CaseType : "Pending / Under Hearing"));

            string docTitle = !string.IsNullOrWhiteSpace(title) ? title : "Court Order / Judgment Record";
            string docDate = !string.IsNullOrWhiteSpace(date) ? date : DateTime.Now.ToString("yyyy-MM-dd");

            // 5b. Extract orders from the live NAPIX CNR response (orders are embedded in CNR payload)
            //     Then try to server-side download the PDF using the pdf_path from NAPIX
            string? apiOrderText = null;
            var ordersList = new List<Dictionary<string, string?>>();

            // Primary: use already-fetched liveCnr (avoids a second network call)
            if (liveCnr.HasValue)
            {
                var listFromCnr = ExtractOrdersArray(liveCnr.Value);
                if (listFromCnr != null && listFromCnr.Count > 0) ordersList.AddRange(listFromCnr);
            }

            // Secondary: call GetOrdersAsync which now also tries CNR-first + dedicated endpoints
            if (ordersList.Count == 0 && !string.IsNullOrWhiteSpace(targetCnr) && targetCnr.Length >= 10)
            {
                try
                {
                    var liveOrdersJson = await _napixService.GetOrdersAsync(targetCnr, isHcCase);
                    if (liveOrdersJson.HasValue)
                    {
                        var list = ExtractOrdersArray(liveOrdersJson.Value);
                        if (list != null && list.Count > 0) ordersList.AddRange(list);
                    }
                }
                catch { }

                // Also check HC MFA CNR from APPEAL_DETAILS
                if (appRecord != null)
                {
                    string? hcCnrFromDb = Convert.ToString(appRecord.CorpMFACNRNumber) ?? Convert.ToString(appRecord.ClaimantMFACNRNumber);
                    if (!string.IsNullOrWhiteSpace(hcCnrFromDb) && hcCnrFromDb.Trim().ToUpperInvariant() != targetCnr)
                    {
                        try
                        {
                            var hcOrdersJson = await _napixService.GetOrdersAsync(hcCnrFromDb.Trim().ToUpperInvariant(), true);
                            if (hcOrdersJson.HasValue)
                            {
                                var list = ExtractOrdersArray(hcOrdersJson.Value);
                                if (list != null && list.Count > 0) ordersList.AddRange(list);
                            }
                        }
                        catch { }
                    }
                }
            }

            // 5b-2. NJDG public endpoint fallback (fast probe with 800ms timeout)
            if (ordersList.Count == 0 && !string.IsNullOrWhiteSpace(targetCnr) && targetCnr.Length >= 10)
            {
                try
                {
                    using var njdgHandler = new System.Net.Http.HttpClientHandler { AllowAutoRedirect = true, ServerCertificateCustomValidationCallback = (_, _, _, _) => true };
                    using var njdgClient = new System.Net.Http.HttpClient(njdgHandler);
                    njdgClient.Timeout = TimeSpan.FromMilliseconds(6000);
                    njdgClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
                    njdgClient.DefaultRequestHeaders.Add("Accept", "application/json,*/*");
                    njdgClient.DefaultRequestHeaders.Add("Referer", "https://services.ecourts.gov.in/");
                    njdgClient.DefaultRequestHeaders.Add("X-Requested-With", "XMLHttpRequest");

                    string primaryNjdgUrl = isHcCase
                        ? $"https://hcservices.ecourts.gov.in/hcservices/cases/display_pdf.php?cino={Uri.EscapeDataString(targetCnr)}"
                        : $"https://services.ecourts.gov.in/ecourtIndia_v6/cases/display_pdf.php?cino={Uri.EscapeDataString(targetCnr)}&state_code=6&dist_code=19";

                    try
                    {
                        var njdgResp = await njdgClient.GetAsync(primaryNjdgUrl);
                        if (njdgResp.IsSuccessStatusCode)
                        {
                            var rawBytes2 = await njdgResp.Content.ReadAsByteArrayAsync();
                            if (rawBytes2.Length > 4 && rawBytes2[0] == 0x25 && rawBytes2[1] == 0x50 && rawBytes2[2] == 0x44 && rawBytes2[3] == 0x46)
                            {
                                Response.Headers.Append("Content-Disposition", $"inline; filename=\"eCourts_Judgment_{displayCnr}.pdf\"");
                                return File(rawBytes2, "application/pdf");
                            }
                            string njdgBody = System.Text.Encoding.UTF8.GetString(rawBytes2);
                            if (njdgBody.TrimStart().StartsWith("{") || njdgBody.TrimStart().StartsWith("["))
                            {
                                try
                                {
                                    using var njdgDoc = System.Text.Json.JsonDocument.Parse(njdgBody);
                                    var njdgOrders = ExtractOrdersArray(njdgDoc.RootElement.Clone());
                                    if (njdgOrders.Count > 0) ordersList.AddRange(njdgOrders);
                                }
                                catch { }
                            }
                        }
                    }
                    catch { }
                }
                catch { }
            }

            // 5c. Find the target order matching requested date/title
            Dictionary<string, string?>? targetOrderDict = null;
            if (ordersList.Count > 0)
            {
                targetOrderDict = ordersList.FirstOrDefault(o =>
                    (!string.IsNullOrWhiteSpace(date) && o.Values.Any(v => v != null && v.Contains(date))) ||
                    (!string.IsNullOrWhiteSpace(title) && o.Values.Any(v => v != null && v.Contains(title, StringComparison.OrdinalIgnoreCase)))
                ) ?? ordersList.OrderByDescending(o => o.GetValueOrDefault("order_no") ?? "0").First();

                // Capture order text if present
                foreach (var kvp in targetOrderDict)
                {
                    string kl = kvp.Key.ToLowerInvariant();
                    if ((kl.Contains("order_text") || kl.Contains("judgment_text") || kl.Contains("order_copy") || kl.Contains("order_detail"))
                        && !string.IsNullOrWhiteSpace(kvp.Value))
                    {
                        apiOrderText = kvp.Value;
                        break;
                    }
                }

                // 5c-1. Retry authentic NAPIX PDF fetch using specific order_no and order_date extracted from live order record
                string extractedOrderNo = targetOrderDict.GetValueOrDefault("order_no") ?? targetOrderDict.GetValueOrDefault("order_number") ?? targetOrderDict.GetValueOrDefault("sr_no") ?? orderNo ?? "1";
                string extractedOrderDate = targetOrderDict.GetValueOrDefault("order_date") ?? targetOrderDict.GetValueOrDefault("date") ?? date ?? "";
                if (!string.IsNullOrWhiteSpace(extractedOrderNo) && (extractedOrderNo.Length > 8 || extractedOrderNo.Equals(displayCnr, StringComparison.OrdinalIgnoreCase) || !int.TryParse(extractedOrderNo, out _)))
                {
                    extractedOrderNo = orderNo ?? "1";
                }
                if (!string.IsNullOrWhiteSpace(extractedOrderNo) || !string.IsNullOrWhiteSpace(extractedOrderDate))
                {
                    try
                    {
                        var retryPdfBytes = await _napixService.GetOrderPdfBytesAsync(displayCnr, extractedOrderNo, extractedOrderDate, isHcCase);
                        if (retryPdfBytes != null && retryPdfBytes.Length > 100 && retryPdfBytes[0] == 0x25 && retryPdfBytes[1] == 0x50 && retryPdfBytes[2] == 0x44 && retryPdfBytes[3] == 0x46)
                        {
                            Response.Headers.Append("Content-Disposition", $"inline; filename=\"eCourts_Order_{displayCnr}_{extractedOrderNo}.pdf\"");
                            return File(retryPdfBytes, "application/pdf");
                        }
                    }
                    catch { }
                }

                // 5d. Try server-side PDF download using pdf_path from NAPIX order
                string? rawPdfPath = targetOrderDict.GetValueOrDefault("pdf_path");
                string? stateCode = targetOrderDict.GetValueOrDefault("state_code") ?? "6";
                string? distCode = targetOrderDict.GetValueOrDefault("dist_code") ?? "19";
                string? estCodeVal = targetOrderDict.GetValueOrDefault("est_code") ?? targetOrderDict.GetValueOrDefault("est_code");
                string? cinoVal = targetOrderDict.GetValueOrDefault("cino") ?? displayCnr;

                // Build candidate PDF fetch URLs (eCourts uses various URL patterns depending on court type)
                var candidateUrls = new List<string>();

                // If pdf_path from NAPIX looks like a base64 string
                string? liveBase64 = null;
                foreach (var kvp in targetOrderDict)
                {
                    string kl = kvp.Key.ToLowerInvariant();
                    string v = kvp.Value ?? "";
                    if (string.IsNullOrWhiteSpace(v)) continue;
                    if (kl.Contains("base64") || kl.Contains("pdf_content") || kl.Contains("binary")) { liveBase64 = v; break; }
                    if ((kl.Contains("url") || kl.Contains("link")) && (v.StartsWith("http://") || v.StartsWith("https://")))
                        candidateUrls.Add(v);
                }

                // If rawPdfPath is a local uploads path, serve it immediately!
                if (!string.IsNullOrWhiteSpace(rawPdfPath) && rawPdfPath.StartsWith("/uploads/", StringComparison.OrdinalIgnoreCase))
                {
                    string webRoot = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
                    string fullLocal = Path.Combine(webRoot, rawPdfPath.TrimStart('/', '\\'));
                    if (System.IO.File.Exists(fullLocal))
                    {
                        Response.Headers.Append("Content-Disposition", $"inline; filename=\"eCourts_Order_{displayCnr}.pdf\"");
                        return PhysicalFile(fullLocal, "application/pdf");
                    }
                }

                if (!string.IsNullOrWhiteSpace(rawPdfPath) && !rawPdfPath.StartsWith("/ECourts/", StringComparison.OrdinalIgnoreCase) && !rawPdfPath.StartsWith("/"))
                {
                    string p = rawPdfPath.Trim().TrimStart('/', '\\');
                    string pEncoded = Uri.EscapeDataString(p);

                    if (rawPdfPath.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                    {
                        candidateUrls.Insert(0, rawPdfPath);
                    }
                    else if (isHcCase)
                    {
                        // High Court PDF URL patterns
                        candidateUrls.Add($"https://hcservices.ecourts.gov.in/hcservices/{p}");
                        candidateUrls.Add($"https://hcservices.ecourts.gov.in/hcservices/display_pdf.php?pdf_path={pEncoded}&cino={cinoVal}");
                    }
                    else
                    {
                        // District Court PDF URL patterns
                        candidateUrls.Add($"https://services.ecourts.gov.in/ecourtIndia_v6/{p}");
                        candidateUrls.Add($"https://services.ecourts.gov.in/ecourtIndia_v6/display_pdf.php?pdf_path={pEncoded}&cino={cinoVal}&state_code={stateCode}&dist_code={distCode}");
                    }
                }

                // Test each candidate URL server-side
                if (candidateUrls.Count > 0)
                {
                    try
                    {
                        using var handler = new System.Net.Http.HttpClientHandler
                        {
                            AllowAutoRedirect = true,
                            ServerCertificateCustomValidationCallback = (_, _, _, _) => true
                        };
                        using var client = new System.Net.Http.HttpClient(handler);
                        client.Timeout = TimeSpan.FromSeconds(8);
                        client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36");
                        client.DefaultRequestHeaders.Add("Accept", "application/pdf,*/*;q=0.9");
                        client.DefaultRequestHeaders.Add("Referer", isHcCase
                            ? "https://hcservices.ecourts.gov.in/"
                            : "https://services.ecourts.gov.in/");

                        foreach (var testUrl in candidateUrls.Distinct())
                        {
                            try
                            {
                                _logger.LogInformation("[ViewOrderPdf] Attempting PDF fetch: {Url}", testUrl);
                                var resp = await client.GetAsync(testUrl);
                                if (resp.IsSuccessStatusCode)
                                {
                                    var bytes = await resp.Content.ReadAsByteArrayAsync();
                                    // Validate PDF magic bytes: %PDF
                                    if (bytes.Length > 100 && bytes[0] == 0x25 && bytes[1] == 0x50 && bytes[2] == 0x44 && bytes[3] == 0x46)
                                    {
                                        _logger.LogInformation("[ViewOrderPdf] PDF successfully fetched from {Url}", testUrl);
                                        Response.Headers.Append("Content-Disposition", $"inline; filename=\"eCourts_Judgment_{displayCnr}.pdf\"");
                                        return File(bytes, "application/pdf");
                                    }
                                }
                            }
                            catch { }
                        }
                    }
                    catch { }
                }

                // If live Base64 PDF, decode and serve
                if (!string.IsNullOrWhiteSpace(liveBase64))
                {
                    try
                    {
                        if (liveBase64.StartsWith("data:application/pdf;base64,", StringComparison.OrdinalIgnoreCase))
                            liveBase64 = liveBase64.Substring("data:application/pdf;base64,".Length);
                        byte[] pdfBytes = Convert.FromBase64String(liveBase64);
                        if (pdfBytes.Length > 100 && pdfBytes[0] == 0x25 && pdfBytes[1] == 0x50 && pdfBytes[2] == 0x44 && pdfBytes[3] == 0x46)
                        {
                            Response.Headers.Append("Content-Disposition", $"inline; filename=\"eCourts_Judgment_{displayCnr}.pdf\"");
                            return File(pdfBytes, "application/pdf");
                        }
                    }
                    catch { }
                }
            }

            // 5e. Check local database upload fields and wwwroot/uploads directory (MVC cases only)
            if (!isLabour && mvcCase != null)
            {
                try
                {
                    using var conn = _db.GetConnection();
                    dynamic? adverseAward = null;
                    try
                    {
                        adverseAward = conn.QueryFirstOrDefault(@"
                            SELECT TOP 1 * FROM MVC_CASE_ADVERSE_DETAILS WHERE CaseID = @caseId",
                            new { caseId = mvcCase.CaseID });
                    }
                    catch { }

                    var appealDetail = conn.QueryFirstOrDefault(@"
                        SELECT TOP 1 * FROM APPEAL_DETAILS WHERE CaseID = @caseId",
                        new { caseId = mvcCase.CaseID });

                    bool isHcTarget = isHcCase || (!string.IsNullOrWhiteSpace(targetCnr) && targetCnr.StartsWith("KAHC", StringComparison.OrdinalIgnoreCase)) || (!string.IsNullOrWhiteSpace(title) && (title.Contains("MFA", StringComparison.OrdinalIgnoreCase) || title.Contains("High Court", StringComparison.OrdinalIgnoreCase) || title.Contains("Appeal", StringComparison.OrdinalIgnoreCase)));

                    var candidatePaths = new List<string?>();

                    // Check CASE_ORDERS table for any locally uploaded signed order PDFs for this CNR
                    try
                    {
                        string cnrToQuery = !string.IsNullOrWhiteSpace(targetCnr) ? targetCnr : displayCnr;
                        if (!string.IsNullOrWhiteSpace(cnrToQuery))
                        {
                            var caseOrderFiles = conn.Query<string>(@"
                                SELECT LocalPdfPath FROM CASE_ORDERS 
                                WHERE (CNRNumber = @cnrToQuery OR CNRNumber = @displayCnr) AND LocalPdfPath IS NOT NULL AND LocalPdfPath <> ''
                                ORDER BY OrderID DESC", new { cnrToQuery, displayCnr });
                            foreach (var cof in caseOrderFiles)
                            {
                                candidatePaths.Add(cof);
                            }
                        }
                    }
                    catch { }

                    if (isHcTarget)
                    {
                        candidatePaths.Add(Convert.ToString(appealDetail?.MFAJudgmentCopyPath));
                        candidatePaths.Add(Convert.ToString(appealDetail?.CorpMFAActionTakenPath));
                        candidatePaths.Add(Convert.ToString(appealDetail?.InitialActionPath1));
                        candidatePaths.Add(Convert.ToString(appealDetail?.InitialActionPath2));
                        candidatePaths.Add(Convert.ToString(appealDetail?.StayOrderPath1));
                        candidatePaths.Add(Convert.ToString(appealDetail?.StayOrderPath2));
                        candidatePaths.Add(Convert.ToString(appealDetail?.ComplianceLetterPath));
                        candidatePaths.Add(Convert.ToString(appealDetail?.ClaimantSCJudgmentPath));
                        candidatePaths.Add(adverseAward?.AdverseJudgmentUploadPath);
                    }
                    else
                    {
                        candidatePaths.Add(adverseAward?.AdverseJudgmentUploadPath);
                        candidatePaths.Add(mvcCase.InterimOrderFilePath);
                        candidatePaths.Add(adverseAward?.DelayCondonedOrderPath);
                        candidatePaths.Add(adverseAward?.TR18UploadPath);
                        candidatePaths.Add(adverseAward?.PunishmentOrderUploadPath);
                        candidatePaths.Add(adverseAward?.DelayApplicationPath);
                        candidatePaths.Add(Convert.ToString(appealDetail?.MFAJudgmentCopyPath));
                        candidatePaths.Add(Convert.ToString(appealDetail?.CorpMFAActionTakenPath));
                    }

                    string webRoot = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
                    if (!Directory.Exists(webRoot)) webRoot = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "wwwroot");

                    foreach (var rel in candidatePaths)
                    {
                        if (string.IsNullOrWhiteSpace(rel)) continue;
                        string full = Path.Combine(webRoot, rel.TrimStart('/', '\\'));
                        if (System.IO.File.Exists(full) && full.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                        {
                            Response.Headers.Append("Content-Disposition", $"inline; filename=\"eCourts_Judgment_{displayCnr}.pdf\"");
                            return PhysicalFile(full, "application/pdf");
                        }
                    }

                    string uploadsDir = Path.Combine(webRoot, "uploads");
                    if (Directory.Exists(uploadsDir))
                    {
                        string rawMvcNo = mvcCase.MVCNo ?? "";
                        string mvcDigits = System.Text.RegularExpressions.Regex.Match(rawMvcNo, @"\d+").Value;
                        string mvcYear = Convert.ToString(mvcCase.MVCYear);

                        string rawMfaNo = appRecord != null ? (Convert.ToString(appRecord.CorpMFANumber) ?? Convert.ToString(appRecord.ClaimantMFANumber) ?? "") : "";
                        string mfaDigits = System.Text.RegularExpressions.Regex.Match(rawMfaNo, @"\d+").Value;

                        var allPdfs = Directory.GetFiles(uploadsDir, "*.pdf", SearchOption.AllDirectories);

                        var matchedFiles = allPdfs.Where(f =>
                        {
                            string fileName = Path.GetFileName(f);
                            if (!string.IsNullOrWhiteSpace(targetCnr) && fileName.Contains(targetCnr, StringComparison.OrdinalIgnoreCase)) return true;
                            if (!string.IsNullOrWhiteSpace(mfaDigits) && mfaDigits.Length >= 3 && fileName.Contains(mfaDigits)) return true;
                            if (fileName.Contains("mfa_judgment", StringComparison.OrdinalIgnoreCase)) return true;
                            if (!string.IsNullOrWhiteSpace(mvcDigits) && fileName.Contains(mvcDigits) && (!string.IsNullOrWhiteSpace(mvcYear) && fileName.Contains(mvcYear))) return true;
                            if (!string.IsNullOrWhiteSpace(rawMvcNo) && fileName.Contains(rawMvcNo, StringComparison.OrdinalIgnoreCase)) return true;
                            return false;
                        }).ToList();

                        if (matchedFiles.Count > 0)
                        {
                            string foundFile = matchedFiles.First();
                            Response.Headers.Append("Content-Disposition", $"inline; filename=\"eCourts_Judgment_{displayCnr}.pdf\"");
                            return PhysicalFile(foundFile, "application/pdf");
                        }
                    }
                }
                catch { }
            }

            // 6. If no authentic PDF document could be retrieved from NAPIX or local storage:
            string encodedCnr = System.Net.WebUtility.HtmlEncode(displayCnr);
            string encodedTitle = System.Net.WebUtility.HtmlEncode(docTitle);
            string encodedDate = System.Net.WebUtility.HtmlEncode(docDate);
            string encodedCourt = System.Net.WebUtility.HtmlEncode(displayCourt);
            string cleanOrderDisplay = !string.IsNullOrWhiteSpace(orderNo) && int.TryParse(orderNo, out _) ? $"#{orderNo}" : (!string.IsNullOrWhiteSpace(orderNo) ? orderNo : "1");

            // Deep-link: HC portal CNR search takes the user directly to the case page.
            // The HC portal URL pattern is: main.php#cino=<CNR>
            string portalUrl = isHcCase
                ? $"https://hcservices.ecourts.gov.in/hcservices/main.php#{Uri.EscapeDataString(displayCnr)}"
                : $"https://services.ecourts.gov.in/ecourtIndia_v6/cases/case_status.php?cino={Uri.EscapeDataString(displayCnr)}";
            string portalLabel = isHcCase ? "View Case on High Court Portal" : "View Case on eCourts Portal";

            string unavailableHtml = $@"<!DOCTYPE html>
<html lang='en'>
<head>
    <meta charset='utf-8' />
    <meta name='viewport' content='width=device-width, initial-scale=1.0' />
    <title>Order PDF Not Available - {encodedCnr}</title>
    <link href='https://cdn.jsdelivr.net/npm/bootstrap@5.3.3/dist/css/bootstrap.min.css' rel='stylesheet'>
    <link href='https://cdn.jsdelivr.net/npm/bootstrap-icons@1.11.3/font/bootstrap-icons.css' rel='stylesheet'>
    <style>
        body {{ background-color: #0f172a; color: #f8fafc; font-family: 'Public Sans', system-ui, -apple-system, sans-serif; display: flex; align-items: center; justify-content: center; min-height: 100vh; margin: 0; padding: 1.5rem; }}
        .notice-card {{ background: #1e293b; border: 1px solid #334155; border-radius: 16px; padding: 2.5rem; max-width: 580px; width: 100%; box-shadow: 0 20px 40px rgba(0,0,0,0.3); text-align: center; }}
        .meta-pill {{ background: rgba(255,255,255,0.06); border: 1px solid rgba(255,255,255,0.1); border-radius: 8px; padding: 0.4rem 0.85rem; font-size: 0.85rem; color: #94a3b8; display: inline-block; margin: 0.25rem; }}
    </style>
</head>
<body>
    <div class='notice-card'>
        <div class='mb-3'>
            <i class='bi bi-file-earmark-x text-warning' style='font-size: 3.5rem;'></i>
        </div>
        <h5 class='fw-bold text-white mb-2'>Order PDF Not Available</h5>
        <p class='text-secondary small mb-4'>
            The official digital PDF copy has not been uploaded by the court clerk on the e-Courts NAPIX portal for this proceeding, or the court registry has not made it publicly accessible.
        </p>

        <div class='mb-4 text-start p-3 rounded' style='background: rgba(15,23,42,0.6); border: 1px solid #334155;'>
            <div class='small text-muted mb-1'><strong class='text-white'>{encodedCourt}</strong></div>
            <div class='extra-small text-secondary mb-2'>{encodedTitle} (Order {cleanOrderDisplay})</div>
            <div class='d-flex flex-wrap gap-1'>
                <span class='meta-pill'><i class='bi bi-hash me-1 text-primary'></i>CNR: <strong class='text-white'>{encodedCnr}</strong></span>
                <span class='meta-pill'><i class='bi bi-calendar3 me-1 text-danger'></i>Date: <strong class='text-white'>{encodedDate}</strong></span>
            </div>
        </div>

        <div class='d-flex justify-content-center gap-2 flex-wrap'>
            {(!string.IsNullOrWhiteSpace(decodedUrl) && decodedUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? $@"
            <a href='{System.Net.WebUtility.HtmlEncode(decodedUrl)}' target='_blank' rel='noopener noreferrer' class='btn btn-outline-primary rounded-pill px-4 fw-bold shadow-sm'>
                <i class='bi bi-file-earmark-arrow-down me-1'></i> Open Document on Court Server
            </a>" : "")}
            <a href='{portalUrl}' target='_blank' rel='noopener noreferrer' class='btn btn-outline-danger rounded-pill px-4 fw-bold shadow-sm'>
                <i class='bi bi-box-arrow-up-right me-1'></i> {portalLabel}
            </a>
            <form action='/ECourts/UploadOrderPdf' method='post' enctype='multipart/form-data' class='d-inline'>
                <input type='hidden' name='cnr' value='{encodedCnr}' />
                <input type='hidden' name='title' value='{encodedTitle}' />
                <input type='hidden' name='date' value='{encodedDate}' />
                <input type='hidden' name='caseId' value='{(caseId.HasValue ? caseId.Value : 0)}' />
                <input type='file' name='pdfFile' id='uploadPdfDoc' accept='.pdf' class='d-none' onchange='this.form.submit()' />
                <button type='button' onclick='document.getElementById(""uploadPdfDoc"").click()' class='btn btn-warning rounded-pill px-4 fw-bold shadow-sm text-dark'>
                    <i class='bi bi-upload me-1'></i> Upload Scanned PDF Copy
                </button>
            </form>
        </div>
    </div>
</body>
</html>";

            return Content(unavailableHtml, "text/html; charset=utf-8");
        }

        // POST: /ECourts/UploadOrderPdf
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UploadOrderPdf(IFormFile pdfFile, string cnr, string? title = null, string? date = null, int? caseId = null)
        {
            if (pdfFile == null || pdfFile.Length == 0) return BadRequest("Please select a valid PDF file.");
            if (pdfFile.Length > 20 * 1024 * 1024) return BadRequest("File size exceeds the 20MB limit.");

            string ext = Path.GetExtension(pdfFile.FileName).ToLowerInvariant();
            if (ext != ".pdf") return BadRequest("Only PDF files are permitted.");

            // Validate PDF Magic bytes (%PDF / 0x25 0x50 0x44 0x46)
            using (var streamCheck = pdfFile.OpenReadStream())
            {
                byte[] header = new byte[4];
                int read = await streamCheck.ReadAsync(header.AsMemory(0, 4));
                if (read < 4 || header[0] != 0x25 || header[1] != 0x50 || header[2] != 0x44 || header[3] != 0x46)
                {
                    return BadRequest("Invalid file signature: Content is not a legitimate PDF document.");
                }
            }

            // IDOR & Authorization check: verify user has permission for this caseId
            if (caseId.HasValue && caseId.Value > 0)
            {
                var existingCase = _caseRepo.GetCaseById(caseId.Value);
                if (existingCase != null)
                {
                    var divIdString = User.FindFirstValue("DivisionID");
                    int userDivId = int.TryParse(divIdString, out int pId) ? pId : 0;
                    bool isCentral = userDivId == 0 || userDivId == 5 || User.IsInRole("Admin");
                    if (!isCentral && existingCase.DivisionID != userDivId)
                    {
                        return Forbid();
                    }
                }
            }

            string cleanCnr = !string.IsNullOrWhiteSpace(cnr) ? System.Text.RegularExpressions.Regex.Replace(cnr.Trim(), @"[^A-Za-z0-9]", "").ToUpperInvariant() : "ORDER";
            string uploadsDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "orders");
            if (!Directory.Exists(uploadsDir)) Directory.CreateDirectory(uploadsDir);

            string fileName = $"{cleanCnr}_{Guid.NewGuid():N}.pdf";
            string filePath = Path.Combine(uploadsDir, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await pdfFile.CopyToAsync(stream);
            }

            string relativePath = $"/uploads/orders/{fileName}";

            try
            {
                using var conn = _db.GetConnection();

                // If caseId is not specified, resolve it from APPEAL_DETAILS or MVC_CASES by cleanCnr
                if (!caseId.HasValue || caseId.Value <= 0)
                {
                    caseId = conn.QueryFirstOrDefault<int?>(
                        @"SELECT TOP 1 CaseID FROM APPEAL_DETAILS 
                          WHERE CorpMFACNRNumber = @cleanCnr OR ClaimantMFACNRNumber = @cleanCnr OR CNRNumber = @cleanCnr",
                        new { cleanCnr });
                    if (!caseId.HasValue || caseId.Value <= 0)
                    {
                        caseId = conn.QueryFirstOrDefault<int?>(
                            "SELECT TOP 1 CaseID FROM MVC_CASES WHERE CNRNumber = @cleanCnr",
                            new { cleanCnr });
                    }
                }

                if (caseId.HasValue && caseId.Value > 0)
                {
                    conn.Execute(@"
                        IF EXISTS (SELECT 1 FROM APPEAL_DETAILS WHERE CaseID = @caseId)
                        BEGIN
                            IF @orderTitle LIKE '%Stay%'
                                UPDATE APPEAL_DETAILS SET StayOrderPath1 = COALESCE(StayOrderPath1, @relativePath) WHERE CaseID = @caseId;
                            ELSE
                                UPDATE APPEAL_DETAILS SET MFAJudgmentCopyPath = COALESCE(MFAJudgmentCopyPath, @relativePath) WHERE CaseID = @caseId;
                        END
                        IF EXISTS (SELECT 1 FROM MVC_CASE_ADVERSE_DETAILS WHERE CaseID = @caseId)
                            UPDATE MVC_CASE_ADVERSE_DETAILS SET AdverseJudgmentUploadPath = COALESCE(AdverseJudgmentUploadPath, @relativePath) WHERE CaseID = @caseId;
                        IF EXISTS (SELECT 1 FROM MVC_CASES WHERE CaseID = @caseId)
                            UPDATE MVC_CASES SET FavorJudgmentPath = COALESCE(FavorJudgmentPath, @relativePath) WHERE CaseID = @caseId;
                    ", new { caseId = caseId.Value, relativePath, orderTitle = !string.IsNullOrWhiteSpace(title) ? title : "" });
                }

                conn.Execute(@"
                    IF NOT EXISTS (SELECT 1 FROM CASE_ORDERS WHERE CNRNumber = @cleanCnr AND (LocalPdfPath = @relativePath OR ExternalUrl = @relativePath))
                    BEGIN
                        INSERT INTO CASE_ORDERS (CNRNumber, OrderDate, OrderType, LocalPdfPath, CreatedDate)
                        VALUES (@cleanCnr, @orderDt, @orderTitle, @relativePath, GETDATE());
                    END
                ", new { cleanCnr, orderDt = !string.IsNullOrWhiteSpace(date) ? date : DateTime.Now.ToString("yyyy-MM-dd"), orderTitle = !string.IsNullOrWhiteSpace(title) ? title : "Uploaded Court Order", relativePath });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating order PDF path for CaseID {CaseID}", caseId);
            }

            return RedirectToAction("ViewOrderPdf", new { cnr = cleanCnr, title, date, caseId, url = relativePath });
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> DownloadOrderPdf(string? pdfPath = null, string? cnr = null, bool isHc = false, string? orderNo = null, string? date = null, string? module = null, int? caseId = null, string? title = null)
        {
            if (string.IsNullOrWhiteSpace(module))
            {
                var referer = Request.Headers["Referer"].ToString();
                module = referer.Contains("/Labour", StringComparison.OrdinalIgnoreCase) ? "Labour" : "MVC";
            }
            string norm = NapixQuotaService.NormalizeModule(module);
            bool isLabour = norm.Equals("Labour", StringComparison.OrdinalIgnoreCase);

            string webRoot = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
            if (!Directory.Exists(webRoot)) webRoot = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "wwwroot");

            // 1. If physical file path in wwwroot is provided directly, serve immediately
            if (!string.IsNullOrWhiteSpace(pdfPath) && !pdfPath.StartsWith("http", StringComparison.OrdinalIgnoreCase) && !pdfPath.StartsWith("/ECourts/ViewOrderPdf", StringComparison.OrdinalIgnoreCase))
            {
                string directRelPath = pdfPath.Trim().TrimStart('/', '\\');
                string localFile = Path.Combine(webRoot, directRelPath);
                if (System.IO.File.Exists(localFile) && localFile.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                {
                    Response.Headers.Append("Content-Disposition", $"attachment; filename=\"{Path.GetFileName(localFile)}\"");
                    return PhysicalFile(localFile, "application/pdf");
                }
            }

            // 2. For Labour cases, check authentic uploaded documents in LABOUR_CASES
            if (isLabour && (caseId.HasValue && caseId.Value > 0 || !string.IsNullOrWhiteSpace(cnr)))
            {
                try
                {
                    using var lConn = _db.GetConnection();
                    dynamic? labourCase = null;
                    if (caseId.HasValue && caseId.Value > 0)
                    {
                        labourCase = lConn.QueryFirstOrDefault("SELECT TOP 1 * FROM LABOUR_CASES WHERE CaseID = @caseId", new { caseId = caseId.Value });
                    }
                    if (labourCase == null && !string.IsNullOrWhiteSpace(cnr))
                    {
                        string cleanC = cnr.Trim().Replace("-", "").Replace(" ", "");
                        labourCase = lConn.QueryFirstOrDefault(@"
                            SELECT TOP 1 * FROM LABOUR_CASES 
                            WHERE (REPLACE(REPLACE(CO_WP_CNRNumber, '-', ''), ' ', '') = @cleanC AND LEN(CO_WP_CNRNumber) >= 10)
                               OR (REPLACE(REPLACE(CO_WA_CNRNumber, '-', ''), ' ', '') = @cleanC AND LEN(CO_WA_CNRNumber) >= 10)
                               OR (REPLACE(REPLACE(CO_Claimant_CNRNumber, '-', ''), ' ', '') = @cleanC AND LEN(CO_Claimant_CNRNumber) >= 10)
                               OR (REPLACE(REPLACE(CNRNumber, '-', ''), ' ', '') = @cleanC AND LEN(CNRNumber) >= 10)
                            ORDER BY CaseID DESC", new { cleanC });
                    }

                    if (labourCase != null)
                    {
                        var candidatePaths = new List<string?>();
                        string tLower = (title ?? "").ToLowerInvariant();
                        if (tLower.Contains("stay"))
                        {
                            candidatePaths.Add(Convert.ToString(labourCase.CO_WP_StayOrderPath));
                            candidatePaths.Add(Convert.ToString(labourCase.CO_WA_StayOrderPath));
                            candidatePaths.Add(Convert.ToString(labourCase.CO_Reinstatement_StayOrderPath));
                        }
                        else if (tLower.Contains("wp") || tLower.Contains("high court"))
                        {
                            candidatePaths.Add(Convert.ToString(labourCase.CO_WP_JudgmentCopyPath));
                            candidatePaths.Add(Convert.ToString(labourCase.JudgmentCopyPath));
                        }
                        else
                        {
                            if (orderNo == "1")
                            {
                                candidatePaths.Add(Convert.ToString(labourCase.CO_WP_StayOrderPath));
                                candidatePaths.Add(Convert.ToString(labourCase.CO_WP_JudgmentCopyPath));
                                candidatePaths.Add(Convert.ToString(labourCase.JudgmentCopyPath));
                            }
                            else if (orderNo == "2")
                            {
                                candidatePaths.Add(Convert.ToString(labourCase.CO_WP_JudgmentCopyPath));
                                candidatePaths.Add(Convert.ToString(labourCase.CO_WA_StayOrderPath));
                                candidatePaths.Add(Convert.ToString(labourCase.JudgmentCopyPath2));
                            }
                            else
                            {
                                candidatePaths.Add(Convert.ToString(labourCase.CO_WP_JudgmentCopyPath));
                                candidatePaths.Add(Convert.ToString(labourCase.CO_WP_StayOrderPath));
                                candidatePaths.Add(Convert.ToString(labourCase.JudgmentCopyPath));
                            }
                        }

                        foreach (var rel in candidatePaths)
                        {
                            if (string.IsNullOrWhiteSpace(rel)) continue;
                            string full = Path.Combine(webRoot, rel.TrimStart('/', '\\'));
                            if (System.IO.File.Exists(full) && full.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                            {
                                Response.Headers.Append("Content-Disposition", $"attachment; filename=\"{Path.GetFileName(full)}\"");
                                return PhysicalFile(full, "application/pdf");
                            }
                        }
                    }
                }
                catch { }
            }

            // 3. Query NAPIX / eCourts Gateway for live orders
            if (!string.IsNullOrWhiteSpace(cnr))
            {
                string cleanCnr = cnr.Trim().ToUpperInvariant();
                bool isHcCase = isHc || cleanCnr.StartsWith("KAHC", StringComparison.OrdinalIgnoreCase) || (cleanCnr.Length >= 4 && cleanCnr.Substring(2, 2).Equals("HC", StringComparison.OrdinalIgnoreCase));
                try
                {
                    var napixPdf = await _napixService.GetOrderPdfBytesAsync(cleanCnr, orderNo ?? "1", date ?? "", isHcCase, module: norm);
                    if (napixPdf != null && napixPdf.Length > 100 && napixPdf[0] == 0x25 && napixPdf[1] == 0x50 && napixPdf[2] == 0x44 && napixPdf[3] == 0x46)
                    {
                        Response.Headers.Append("Content-Disposition", $"attachment; filename=\"eCourts_Order_{cleanCnr}_{(orderNo ?? "1")}.pdf\"");
                        return File(napixPdf, "application/pdf");
                    }
                }
                catch { }
            }

            if (!string.IsNullOrWhiteSpace(pdfPath) && pdfPath.StartsWith("/ECourts/ViewOrderPdf", StringComparison.OrdinalIgnoreCase))
            {
                return Redirect(pdfPath);
            }

            if (string.IsNullOrWhiteSpace(pdfPath))
            {
                return NotFound("Authentic PDF order copy is not yet uploaded or published for this date on eCourts.");
            }

            string p = pdfPath.Trim().TrimStart('/', '\\');
            string pEncoded = Uri.EscapeDataString(p);
            string cinoParam = Uri.EscapeDataString(cnr ?? "");

            var candidateUrls = new List<string>();

            if (pdfPath.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                candidateUrls.Add(pdfPath);
            }
            else if (isHc)
            {
                candidateUrls.Add($"https://hcservices.ecourts.gov.in/hcservices/{p}");
                candidateUrls.Add($"https://hcservices.ecourts.gov.in/{p}");
                candidateUrls.Add($"https://hcservices.ecourts.gov.in/hcservices/cases/{p}");
                candidateUrls.Add($"https://hcservices.ecourts.gov.in/hcservices/display_pdf.php?pdf_path={pEncoded}&cino={cinoParam}");
            }
            else
            {
                candidateUrls.Add($"https://services.ecourts.gov.in/ecourtIndia_v6/{p}");
                candidateUrls.Add($"https://services.ecourts.gov.in/ecourtIndia_v6/cases/{p}");
                candidateUrls.Add($"https://services.ecourts.gov.in/{p}");
                candidateUrls.Add($"https://services.ecourts.gov.in/ecourtIndia_v6/display_pdf.php?pdf_path={pEncoded}&cino={cinoParam}");
                candidateUrls.Add($"https://services.ecourts.gov.in/ecourtIndia_v6/display_pdf.php?filename={pEncoded}&cino={cinoParam}");
            }

            try
            {
                using var handler = new System.Net.Http.HttpClientHandler
                {
                    AllowAutoRedirect = true,
                    ServerCertificateCustomValidationCallback = (_, _, _, _) => true
                };
                using var client = new System.Net.Http.HttpClient(handler);
                client.Timeout = TimeSpan.FromSeconds(20);
                client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36");
                client.DefaultRequestHeaders.Add("Accept", "application/pdf,*/*");
                client.DefaultRequestHeaders.Add("Referer", isHc
                    ? "https://hcservices.ecourts.gov.in/"
                    : "https://services.ecourts.gov.in/");

                foreach (var url in candidateUrls)
                {
                    try
                    {
                        _logger.LogInformation("[DownloadOrderPdf] Trying: {Url}", url);
                        var resp = await client.GetAsync(url);
                        if (resp.IsSuccessStatusCode)
                        {
                            var bytes = await resp.Content.ReadAsByteArrayAsync();
                            if (bytes.Length > 100 && bytes[0] == 0x25 && bytes[1] == 0x50 && bytes[2] == 0x44 && bytes[3] == 0x46)
                            {
                                _logger.LogInformation("[DownloadOrderPdf] PDF served from: {Url}", url);
                                Response.Headers.Append("Content-Disposition", $"inline; filename=\"eCourts_Order_{cnr}.pdf\"");
                                return File(bytes, "application/pdf");
                            }
                        }
                    }
                    catch { }
                }
            }
            catch { }

            return NotFound("PDF could not be retrieved from eCourts servers at this time. The file may require court-portal authentication.");
        }

        // GET: /ECourts/DownloadAllOrdersZip?cnr=...
        [Authorize]
        [HttpGet]
        public async Task<IActionResult> DownloadAllOrdersZip(string cnr, string? module = null)
        {
            if (string.IsNullOrWhiteSpace(cnr)) return BadRequest("CNR number is required.");
            string cleanCnr = System.Text.RegularExpressions.Regex.Replace(cnr.Trim(), @"[^A-Za-z0-9]", "").ToUpperInvariant();
            if (cleanCnr.Length != 16) return BadRequest("Invalid CNR: must be 16 alphanumeric characters.");

            bool isHc = cleanCnr.Length >= 4 && cleanCnr.Substring(2, 2).Equals("HC", StringComparison.OrdinalIgnoreCase);
            string norm = NapixQuotaService.NormalizeModule(module ?? "MVC");

            var liveOrders = await _napixService.GetOrdersAsync(cleanCnr, isHc, null, norm)
                ?? await _napixService.GetCnrDetailsAsync(cleanCnr, isHc, norm);

            if (!liveOrders.HasValue) return NotFound($"No orders found for CNR {cleanCnr}.");

            var ordersList = ExtractOrdersArray(liveOrders.Value);
            if (ordersList == null || ordersList.Count == 0) return NotFound($"No downloadable orders found for CNR {cleanCnr}.");

            using var memoryStream = new System.IO.MemoryStream();
            int count = 0;
            using (var archive = new System.IO.Compression.ZipArchive(memoryStream, System.IO.Compression.ZipArchiveMode.Create, leaveOpen: true))
            {
                int idx = 1;
                foreach (var order in ordersList)
                {
                    string orderNo = order.GetValueOrDefault("order_no") ?? idx.ToString();
                    string orderDate = order.GetValueOrDefault("order_date") ?? "";
                    string orderType = order.GetValueOrDefault("order_type") ?? "Order";

                    try
                    {
                        var pdfBytes = await _napixService.GetOrderPdfBytesAsync(cleanCnr, orderNo, orderDate, isHc, norm);
                        if (pdfBytes != null && pdfBytes.Length > 100 && pdfBytes[0] == 0x25 && pdfBytes[1] == 0x50 && pdfBytes[2] == 0x44 && pdfBytes[3] == 0x46)
                        {
                            string safeType = System.Text.RegularExpressions.Regex.Replace(orderType, @"[^a-zA-Z0-9_-]", "_");
                            var entry = archive.CreateEntry($"{idx:D2}_{safeType}_{orderNo}.pdf", System.IO.Compression.CompressionLevel.Optimal);
                            using var entryStream = entry.Open();
                            await entryStream.WriteAsync(pdfBytes);
                            idx++;
                            count++;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "[NAPIX] Failed to bundle PDF for CNR {CNR} order {OrderNo}", cleanCnr, orderNo);
                    }
                }
            }

            if (count == 0) return NotFound($"Could not retrieve valid PDF documents for CNR {cleanCnr}.");
            return File(memoryStream.ToArray(), "application/zip", $"{cleanCnr}_all_orders.zip");
        }

        // GET: /ECourts/LiveOrders?cnr=KAHC020060752019&module=MVC
        // Returns all orders, judgments, IA filings from NAPIX as JSON — used by the front-end to render real court documents
        [AllowAnonymous]
        [HttpGet]
        public async Task<IActionResult> LiveOrders(string cnr, string? module = null, int? caseId = null, bool refresh = false)
        {
            if (string.IsNullOrWhiteSpace(cnr))
                return BadRequest(new { error = "CNR number is required." });

            string cleanCnr = System.Text.RegularExpressions.Regex.Replace(cnr.Trim(), @"[^A-Za-z0-9]", "").ToUpperInvariant();
            if (cleanCnr.Length != 16)
                return BadRequest(new { error = $"Invalid CNR '{cnr}': must be 16 alphanumeric characters." });

            if (refresh)
            {
                _napixService.ResetCircuitBreaker();
            }

            string norm = NapixQuotaService.NormalizeModule(module ?? "MVC");
            bool isHc = cleanCnr.Length >= 4 && cleanCnr.Substring(2, 2).Equals("HC", StringComparison.OrdinalIgnoreCase);


            // 2. Fallback: direct NAPIX service (GetCnrDetailsAsync + GetOrdersAsync)
            try
            {
                var liveCnr = await _napixService.GetCnrDetailsAsync(cleanCnr, isHc, module: norm);
                var orders = new List<object>();

                if (liveCnr.HasValue)
                {
                    var fromCnr = ExtractOrdersArray(liveCnr.Value);
                    if (fromCnr != null) foreach (var o in fromCnr) { o["source"] = "napix_direct"; orders.Add(o); }
                }

                if (orders.Count == 0)
                {
                    var liveOrders = await _napixService.GetOrdersAsync(cleanCnr, isHc);
                    if (liveOrders.HasValue)
                    {
                        var fromOrders = ExtractOrdersArray(liveOrders.Value);
                        if (fromOrders != null) foreach (var o in fromOrders) { o["source"] = "napix_orders_api"; orders.Add(o); }
                    }
                }

                string? errorMsg = _napixService.GetLastModuleError(norm);

                return Json(new
                {
                    success = orders.Count > 0,
                    cnr = cleanCnr,
                    courtType = isHc ? "High Court" : "District Court",
                    source = "napix_direct",
                    orders = orders,
                    gatewayError = errorMsg,
                    message = orders.Count > 0 
                        ? "Live orders fetched successfully from NAPIX API." 
                        : (!string.IsNullOrWhiteSpace(errorMsg) ? $"eCourts NAPIX API: {errorMsg}" : $"No live orders returned by eCourts NAPIX API for CNR {cleanCnr}.")
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[LiveOrders] Direct NAPIX fallback failed for {CNR}", cleanCnr);
                return StatusCode(502, new { error = $"eCourts gateway unavailable: {ex.Message}", cnr = cleanCnr });
            }
        }



        // GET: /ECourts/GetNapixCaseJson?cnr=...&module=MVC
        // Returns raw decrypted NAPIX JSON for the CNR — useful for debugging and full data inspection
        [AllowAnonymous]
        [HttpGet]
        public async Task<IActionResult> GetNapixCaseJson(string cnr, string? module = null)
        {
            if (string.IsNullOrWhiteSpace(cnr))
                return BadRequest(new { error = "CNR number is required." });

            string cleanCnr = System.Text.RegularExpressions.Regex.Replace(cnr.Trim(), @"[^A-Za-z0-9]", "").ToUpperInvariant();
            string norm = NapixQuotaService.NormalizeModule(module ?? "MVC");
            bool isHc = cleanCnr.Length >= 4 && cleanCnr.Substring(2, 2).Equals("HC", StringComparison.OrdinalIgnoreCase);


            // Direct gateway
            try
            {
                var data = await _napixService.GetCnrDetailsAsync(cleanCnr, isHc, module: norm);
                if (data.HasValue)
                    return Content(data.Value.GetRawText(), "application/json");
            }
            catch (Exception ex)
            {
                return StatusCode(502, new { error = ex.Message, cnr = cleanCnr });
            }

            return NotFound(new { error = $"Case not found in eCourts for CNR '{cleanCnr}'.", cnr = cleanCnr });
        }
    }
}
