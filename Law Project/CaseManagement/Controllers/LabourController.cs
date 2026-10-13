using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MVCCaseManagement.Common;
using MVCCaseManagement.DAL;
using MVCCaseManagement.Models;
using MVCCaseManagement.Models.Audit;
using MVCCaseManagement.Services.Audit;
using System.Security.Claims;

namespace MVCCaseManagement.Controllers
{
    [Authorize]
    public class LabourController : Controller
    {
        private readonly ILabourRepository _labourRepo;
        private readonly ILabourEPRepository _labourEPRepo;
        private readonly IArisingApplicationRepository _arisingRepo;
        private readonly IMasterRepository _masterRepo;
        private readonly ICaseRepository _caseRepo;
        private readonly IECourtsRepository _ecourtsRepo;
        private readonly ICaseNotingRepository _notingRepo;
        private readonly ICaseActivityLogger _activityLogger;
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<LabourController> _logger;

        public LabourController(ILabourRepository labourRepo, ILabourEPRepository labourEPRepo, IArisingApplicationRepository arisingRepo, IMasterRepository masterRepo, ICaseRepository caseRepo, IECourtsRepository ecourtsRepo, ICaseNotingRepository notingRepo, ICaseActivityLogger activityLogger, IWebHostEnvironment env, ILogger<LabourController> logger)
        {
            _labourRepo = labourRepo;
            _labourEPRepo = labourEPRepo;
            _arisingRepo = arisingRepo;
            _masterRepo = masterRepo;
            _caseRepo = caseRepo;
            _ecourtsRepo = ecourtsRepo;
            _notingRepo = notingRepo;
            _activityLogger = activityLogger;
            _env = env;
            _logger = logger;
        }

        [HttpGet]
        public IActionResult HighCourtCauseList()
        {
            return View();
        }



        [HttpGet]
        public IActionResult GetTrackedHighCourtCases()
        {
            var divIdString = User.FindFirst("DivisionID")?.Value;
            bool isCentralOffice = string.IsNullOrEmpty(divIdString) || divIdString == "0" || divIdString == "5";
            int divisionId = int.TryParse(divIdString, out int id) ? id : 0;
            
            var cases = _labourRepo.GetConnectedHighCourtCases(isCentralOffice ? 0 : divisionId);
            
            // Map to rich JSON object for the frontend tracker & analysis dashboard
            var result = cases.Select(c => new {
                caseId = c.CaseID,
                caseNumber = c.CaseNumber ?? "",
                caseYear = c.CaseYear?.ToString() ?? "",
                divisionName = c.DivisionName ?? "Central Office",
                petitioner = c.PetitionerName ?? "Workman / Corporation",
                respondent = "NWKRTC / Corporation",
                advocate = c.CO_WP_AdvocateName ?? c.CO_WA_AdvocateName ?? c.AdvocateName ?? "Not Assigned",
                
                // High Court Writ Petition
                wpCaseNo = c.CO_WP_CaseNumber,
                wpYear = c.CO_WP_Year?.ToString(),
                wpCnr = c.CO_WP_CNRNumber,
                wpBench = c.CO_WP_HighCourtBench ?? "High Court",
                wpStatus = c.CO_WP_Status ?? "Active WP",
                wpAdvocate = c.CO_WP_AdvocateName,
                
                // High Court Writ Appeal
                waCaseNo = c.CO_WA_CaseNumber,
                waYear = c.CO_WA_Year?.ToString(),
                waCnr = c.CO_WA_CNRNumber,
                waBench = c.CO_WA_HighCourtBench ?? "High Court",
                waStatus = c.CO_WA_Status ?? "Active WA",
                waAdvocate = c.CO_WA_AdvocateName,
                
                // Claimant Writ Petition
                claimantCaseNo = c.CO_Claimant_CaseNumber,
                claimantYear = c.CO_Claimant_CaseYear?.ToString(),
                claimantCnr = c.CO_Claimant_CNRNumber,
                claimantStatus = c.CO_Claimant_CaseStatus ?? "Active Petition",
                
                // Service Matter WP
                serviceWpNo = c.CO_Service_WPNumber,
                serviceWpYear = c.CO_Service_WPYear?.ToString(),
                serviceStatus = c.CO_Service_Status ?? "Active WP",
                
                // Primary CNR if High Court
                mainCnr = (c.CNRNumber != null && c.CNRNumber.StartsWith("KAHC", StringComparison.OrdinalIgnoreCase)) ? c.CNRNumber : null,
                
                // Saved Database Hearing Info
                dbHearingDate = c.NextHearingDate?.ToString("yyyy-MM-dd"),
                dbHearingDateDisplay = c.NextHearingDate?.ToString("dd-MMM-yyyy"),
                dbStage = c.CaseStatus ?? "Pending",
                courtName = c.CourtName ?? "Labour Court / High Court"
            });
            
            return Json(new { success = true, data = result });
        }

        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> SaveLiveHearingDate(int caseId, string? cnrNumber, string? hearingDate, string? stage, string? courtHall)
        {
            if (caseId <= 0)
                return Json(new { success = false, message = "Invalid Case ID" });

            DateTime? parsedDate = null;
            if (!string.IsNullOrWhiteSpace(hearingDate) && 
                !hearingDate.Equals("Not Available", StringComparison.OrdinalIgnoreCase) && 
                !hearingDate.Equals("Disposed", StringComparison.OrdinalIgnoreCase) &&
                !hearingDate.Equals("Pending", StringComparison.OrdinalIgnoreCase) &&
                !hearingDate.Equals("Date Pending", StringComparison.OrdinalIgnoreCase))
            {
                if (DateTime.TryParse(hearingDate, out var dt))
                    parsedDate = dt;
            }

            int userId = int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int uid) ? uid : 0;
            bool updated = _labourRepo.UpdateLiveSyncInfo(caseId, parsedDate, stage, courtHall, userId);
            if (updated)
            {
                _ = _activityLogger.LogSubEntityActionAsync("LABOUR", caseId, $"Case #{caseId}", "HEARING_SYNCED", $"Live hearing date synced to {hearingDate} (Stage: {stage})");
            }

            // Also register in eCourts background tracking if valid CNR provided
            if (!string.IsNullOrWhiteSpace(cnrNumber))
            {
                string cleanCnr = cnrNumber.Trim().ToUpperInvariant();
                if (cleanCnr.Length == 16)
                {
                    try
                    {
                        var dbCase = _labourRepo.GetCaseById(caseId) ?? _labourRepo.GetServiceMatterById(caseId);
                        await _ecourtsRepo.SaveTrackedCaseAsync(new MVCCaseManagement.DAL.TrackedCaseModel
                        {
                            CNRNumber = cleanCnr,
                            EstCode = cleanCnr.Substring(0, 6),
                            CaseTypeCode = "WP",
                            RegNo = dbCase?.CaseNumber ?? "",
                            RegYear = dbCase?.CaseYear ?? DateTime.Now.Year
                        });
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to register background tracking for CNR {CNR}", cleanCnr);
                    }
                }
            }

            return Json(new { success = true, updated, hearingDate = parsedDate?.ToString("yyyy-MM-dd"), stage });
        }

        public async Task<IActionResult> Index()
        {
            var divIdString = User.FindFirst("DivisionID")?.Value;
            bool isCentralOffice = string.IsNullOrEmpty(divIdString) || divIdString == "0" || divIdString == "5";
            int divisionId = int.TryParse(divIdString, out int id) ? id : 0;
            
            var stats = _labourRepo.GetDashboardStats(isCentralOffice ? 0 : divisionId);
            
            // Show today's cases by default
            ViewBag.Stats = stats;
            DateTime today = DateTime.Today;
            ViewBag.SelectedDate = today.ToString("yyyy-MM-dd");
            var cases = _labourRepo.GetCasesByHearingDate(today, isCentralOffice ? 0 : divisionId).ToList();
            var arising = _arisingRepo.GetByHearingDate(today, isCentralOffice ? 0 : divisionId).ToList();
            
            // Map Arising to LabourCase for dashboard display
            foreach (var a in arising)
            {
                cases.Add(new LabourCase
                {
                    CaseID = a.ArisingID,
                    CaseNumber = a.CaseNumber,
                    CaseYear = a.CaseYear,
                    PetitionerName = a.PetitionerName ?? "",
                    Designation = a.Designation,
                    CaseType = a.CaseType,
                    CourtName = a.CourtName,
                    CaseStatus = a.CaseStatus,
                    NextHearingDate = a.NextHearingDate,
                    IsArisingApplication = true
                });
            }
            
            var recentCases = cases.OrderBy(c => c.NextHearingDate).ToList();
            
            // Notification Logic for Labour Dashboard
            if (!isCentralOffice && divisionId != 0)
            {
                ViewBag.IncomingTransfers = _labourRepo.GetRecentTransfers(divisionId);
                ViewBag.ReinstatementNotifications = _labourRepo.GetReinstatementNotifications(divisionId);
            }

            ViewBag.Stats = stats;
            ViewBag.IsCentralOffice = isCentralOffice;
            ViewBag.RecentCases = recentCases; // Pass as ViewBag for flexibility with Partial
            return View(); // Don't pass model to View() as it expects different type potentially, or we switch to ViewModel
        }

        [HttpGet]
        public IActionResult GetCasesByDate(string date, string? endDate = null)
        {
            if (!DateTime.TryParse(date, out DateTime hearingDate))
            {
                return BadRequest("Invalid date format.");
            }

            DateTime? endDateParsed = null;
            if (!string.IsNullOrEmpty(endDate) && DateTime.TryParse(endDate, out DateTime endDateVal))
            {
                endDateParsed = endDateVal;
            }

            var divIdString = User.FindFirst("DivisionID")?.Value;
            bool isCentralOffice = string.IsNullOrEmpty(divIdString) || divIdString == "0" || divIdString == "5";
            int divisionId = int.TryParse(divIdString, out int id) ? id : 0;

            var casesList = _labourRepo.GetCasesByHearingDate(hearingDate, isCentralOffice ? 0 : divisionId, endDateParsed).ToList();
            var arisingList = _arisingRepo.GetByHearingDate(hearingDate, isCentralOffice ? 0 : divisionId, endDateParsed).ToList();

            foreach (var a in arisingList)
            {
                casesList.Add(new LabourCase
                {
                    CaseID = a.ArisingID,
                    CaseNumber = a.CaseNumber,
                    CaseYear = a.CaseYear,
                    PetitionerName = a.PetitionerName ?? "",
                    Designation = a.Designation,
                    CaseType = a.CaseType,
                    CourtName = a.CourtName,
                    CaseStatus = a.CaseStatus,
                    NextHearingDate = a.NextHearingDate,
                    IsArisingApplication = true
                });
            }

            var combinedCases = casesList.OrderBy(c => c.NextHearingDate).ToList();

            return PartialView("_LabourDashboardHearings", combinedCases);
        }

        public IActionResult CaseList(int page = 1, string search = "", string status = "all")
        {
            int pageSize = 10;
            var divIdString = User.FindFirst("DivisionID")?.Value;
            bool isCentralOffice = string.IsNullOrEmpty(divIdString) || divIdString == "0" || divIdString == "5";
            int divisionId = int.TryParse(divIdString, out int id) ? id : 0;

            // REQ: Search should always be global across all statuses if a search term is provided
            if (!string.IsNullOrEmpty(search))
            {
                status = "all";
            }
            // Auto-redirect to Inbox ONLY if no specific status is requested and not searching
            else if (isCentralOffice && !Request.Query.ContainsKey("status") && status == "all") 
            {
                if (User.IsInRole("CLO") || User.IsInRole("Dy CLO") || User.IsInRole("DyCLO"))
                {
                    status = "PendingDecision";
                }
                else if (User.IsInRole("MD"))
                {
                    status = "PendingCompetentAuthority";
                }
                else
                {
                    status = "SentToCO";
                }
            }

            if (status == "ArisingApplication")
            {
                var arisingCases = _arisingRepo.GetAll(isCentralOffice ? 0 : divisionId, page, pageSize, search);
                var arisingTotalCount = _arisingRepo.GetTotalCount(isCentralOffice ? 0 : divisionId, search);

                ViewBag.CurrentPage = page;
                ViewBag.TotalPages = (int)Math.Ceiling((double)arisingTotalCount / pageSize);
                ViewBag.CurrentSearch = search;
                ViewBag.CurrentStatus = status;
                ViewBag.IsCentralOffice = isCentralOffice;

                return View("ArisingCaseList", arisingCases);
            }

            var cases = _labourRepo.GetAllCases(isCentralOffice ? 0 : divisionId, page, pageSize, search, status);
            var totalCount = _labourRepo.GetTotalCaseCount(isCentralOffice ? 0 : divisionId, search, status);

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = (int)Math.Ceiling((double)totalCount / pageSize);
            ViewBag.CurrentSearch = search;
            ViewBag.CurrentStatus = status;
            ViewBag.IsCentralOffice = isCentralOffice;

            return View(cases);
        }

        [HttpGet]
        public IActionResult Create()
        {
            PopulateDropdowns();
            var divisionId = HttpContext.Session.GetInt32("DivisionID") ?? 0;
            var model = new LabourCase { CaseYear = DateTime.Now.Year, DivisionID = divisionId };
            return View(model);
        }

        [HttpGet]
        public IActionResult CreateArisingApplication()
        {
            PopulateDropdowns();
            var divisionId = HttpContext.Session.GetInt32("DivisionID") ?? 0;
            
            var arisingModel = new ArisingApplication 
            { 
                CaseYear = DateTime.Now.Year, 
                DivisionID = divisionId,
                CaseType = "Arising Application"
            };
            return View("CreateArisingApplication", arisingModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(LabourCase model)
        {
            int userId = HttpContext.Session.GetInt32(SessionKeys.UserID) ?? 0;
            
            // DivisionID is now selected from dropdown in view, no longer auto-assigned from session
            model.CreatedBy = userId;

            // Handle File Upload
            if (model.HistorySheetFile != null)
            {
                model.DE_HistorySheetPath = SaveFile(model.HistorySheetFile, "history_sheets");
            }
            if (model.CaseType != null && model.CaseType.Contains("Arising"))
            {
                model.IsArisingApplication = true;
            }

            if (model.JudgmentCopyFile != null)
            {
                model.JudgmentCopyPath = SaveFile(model.JudgmentCopyFile, "judgments");
            }
            if (model.Against_PunishmentCopyFile != null)
            {
                model.Against_PunishmentCopyPath = SaveFile(model.Against_PunishmentCopyFile, "punishments");
            }
            if (model.ClaimPetitionFile != null)
            {
                model.ClaimPetitionPath = SaveFile(model.ClaimPetitionFile, "claims");
            }
            if (model.LokAdalatDocumentFile != null)
            {
                model.LokAdalatDocumentPath = SaveFile(model.LokAdalatDocumentFile, "LokAdalat");
            }
            
            // Proceed regardless of Validation state as per requirement "DO NOT KEEP ANY FIELD COMPLUSORY"
            // if (ModelState.IsValid) 
            {
                try
                {
                    _labourRepo.SaveCase(model);
                    _ = _activityLogger.LogCaseCreatedAsync("Labour", model.CaseID, model.CaseNumber ?? $"Labour-{model.CaseID}", null, model.OtherCourtDetails, model, $"Labour Case {model.CaseNumber} registered");
                    TempData["SuccessMessage"] = "Labour case registered successfully!";
                    return RedirectToAction("Index");
                }
                catch (Exception ex)
                {
                    // Log extended error info for debugging
                    var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                    string detailedError = ex.Message + (errors.Any() ? " | Validation Ignore Warning: " + string.Join(", ", errors) : "");
                    
                    ModelState.AddModelError("", "Save Failed: " + ex.Message); // Show simple message to user
                    TempData["ErrorMessage"] = "Failed to save case. " + ex.Message;
                }
            }
            // else { ... } Removed else block to prevent blocking

            PopulateDropdowns();
            return View(model);
        }

        /// <summary>
        /// POST: Save a new Arising Application to the separate LABOUR_ARISING_APPLICATIONS table.
        /// Uses ACID transaction via repository.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CreateArisingApplication(ArisingApplication model)
        {
            int userId = HttpContext.Session.GetInt32(SessionKeys.UserID) ?? 0;
            model.CreatedBy = userId;

            // If DivisionID not set from form, use session
            if (model.DivisionID == 0)
            {
                model.DivisionID = HttpContext.Session.GetInt32(SessionKeys.DivisionID) ?? 0;
            }

            // Resolve ParentCaseID from parent case number + year if provided
            if (!string.IsNullOrEmpty(model.Parent_CaseNumber) && model.Parent_CaseYear.HasValue)
            {
                var parentCase = _labourRepo.GetCaseByNumber(model.Parent_CaseNumber, model.Parent_CaseYear.Value);
                if (parentCase != null)
                {
                    model.ParentCaseID = parentCase.CaseID;
                    model.Parent_CourtID = parentCase.CourtID;
                    model.Parent_CourtName = parentCase.CourtName;
                    model.Parent_CaseType = parentCase.CaseType;
                    model.Parent_CaseStatus = parentCase.CaseStatus;
                    model.Parent_PetitionerName = parentCase.PetitionerName;
                }
            }

            // File Uploads
            if (model.JudgmentCopyFile != null)
            {
                model.JudgmentCopyPath = SaveFile(model.JudgmentCopyFile, "Labour_Arising/Judgments");
            }
            if (model.HistorySheetFile != null)
            {
                model.DE_HistorySheetPath = SaveFile(model.HistorySheetFile, "Labour_Arising/HistorySheets");
            }
            if (model.LokAdalatDocumentFile != null)
            {
                model.LokAdalatDocumentPath = SaveFile(model.LokAdalatDocumentFile, "Labour_Arising/Settlements");
            }
            if (model.ClaimPetitionFile != null)
            {
                model.ClaimPetitionPath = SaveFile(model.ClaimPetitionFile, "Labour_Arising/Claims");
            }

            try
            {
                int arisingId = _arisingRepo.SaveArisingApplication(model);
                _ = _activityLogger.LogCaseCreatedAsync("Arising", arisingId > 0 ? arisingId : model.ArisingID, model.CaseNumber ?? $"Arising-{arisingId}", null, model.Parent_CourtName, model, $"Arising Application {model.CaseNumber} registered");
                TempData["SuccessMessage"] = "Arising Application registered successfully!";
                return RedirectToAction("ArisingApplication");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Save Failed: " + ex.Message);
                TempData["ErrorMessage"] = "Failed to save arising application. " + ex.Message;
            }

            PopulateDropdowns();
            return View(model);
        }

        /// <summary>
        /// GET: List of all Arising Applications with pagination and search.
        /// </summary>
        [HttpGet]
        public IActionResult ArisingApplication(int pageNumber = 1, int pageSize = 10, string? search = null)
        {
            var divIdString = User.FindFirst("DivisionID")?.Value;
            bool isCentralOffice = string.IsNullOrEmpty(divIdString) || divIdString == "0" || divIdString == "5";
            int divisionId = int.TryParse(divIdString, out int id) ? id : 0;
            
            // For Central Office, we want to see ALL forwarded applications (passed as division 0 to repo)
            int repoDivisionId = isCentralOffice ? 0 : divisionId;

            var items = _arisingRepo.GetAll(repoDivisionId, pageNumber, pageSize, search);
            int total = _arisingRepo.GetTotalCount(repoDivisionId, search);

            ViewBag.TotalCount = total;
            ViewBag.DivisionID = divisionId;
            ViewBag.PageNumber = pageNumber;
            ViewBag.PageSize = pageSize;
            ViewBag.Search = search;

            return View(items);
        }



        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ActionArisingApplication(ArisingApplication model)
        {
            var divIdString = User.FindFirst("DivisionID")?.Value;
            int sessDivId = HttpContext.Session.GetInt32(SessionKeys.DivisionID) ?? -1;
            bool isCOUser = string.IsNullOrEmpty(divIdString) || divIdString == "0" || divIdString == "5" 
                        || sessDivId == 0 || sessDivId == 5 
                        || User.IsInRole("MD") || User.IsInRole("CLO") || User.IsInRole("CentralOffice") || User.IsInRole("CO") || User.IsInRole("Admin");

            if (!isCOUser)
            {
                return Forbid();
            }

            var existing = _arisingRepo.GetById(model.ArisingID);
            if (existing == null) return NotFound();

            int userId = HttpContext.Session.GetInt32(SessionKeys.UserID) ?? 0;
            existing.ModifiedBy = userId;
            
            // Handle File Uploads
            if (model.CO_WP_JudgmentCopyFile != null)
            {
                existing.CO_WP_JudgmentCopyPath = SaveFile(model.CO_WP_JudgmentCopyFile, "Labour/WPJudgments");
            }
            if (model.WAStayOrderFile != null)
            {
                existing.CO_WA_StayOrderPath = SaveFile(model.WAStayOrderFile, "Labour/WAStays");
            }
            if (model.StayOrderFile != null)
            {
                existing.CO_WP_StayOrderPath = SaveFile(model.StayOrderFile, "stay_orders");
            }
            if (model.ClosedDocumentFile != null)
            {
                existing.CO_ClosedDocumentPath = SaveFile(model.ClosedDocumentFile, "Labour/ClosedDocs");
            }
            if (model.ReinstatementStayOrderFile != null)
            {
                existing.CO_Reinstatement_StayOrderPath = SaveFile(model.ReinstatementStayOrderFile, "Labour/ReinstatementStays");
            }
            if (model.ReinstatementApprovalFile != null)
            {
                existing.CO_Reinstatement_ApprovalCopyPath = SaveFile(model.ReinstatementApprovalFile, "Labour/Reinstatements");
            }

            // Merge Action fields into existing DB record to preserve original case data
            existing.CO_FeasibilityReceived = model.CO_FeasibilityReceived;
            existing.CO_FeasibilityDate = model.CO_FeasibilityDate;
            existing.CO_ActionTaken = model.CO_ActionTaken;
            existing.CO_ApprovalOutwardNo = model.CO_ApprovalOutwardNo;
            existing.CO_ApprovalDate = model.CO_ApprovalDate;

            // WP Corp
            existing.CO_WP_CaseStatus_Option = model.CO_WP_CaseStatus_Option;
            existing.CO_WP_CaseNumber = model.CO_WP_CaseNumber;
            existing.CO_WP_Year = model.CO_WP_Year;
            existing.CO_WP_HighCourtBench = model.CO_WP_HighCourtBench;
            existing.CO_WP_EntrustmentNo = model.CO_WP_EntrustmentNo;
            existing.CO_WP_EntrustmentDate = model.CO_WP_EntrustmentDate;
            existing.CO_WP_AdvocateName = model.CO_WP_AdvocateName;
            existing.CO_WP_StayGranted = model.CO_WP_StayGranted;
            existing.CO_WP_StayApprovalNo = model.CO_WP_StayApprovalNo;
            existing.CO_WP_StayNature = model.CO_WP_StayNature;
            existing.CO_WP_StayDate = model.CO_WP_StayDate;
            existing.CO_WP_StayRemark = model.CO_WP_StayRemark;
            existing.CO_WP_Status = model.CO_WP_Status;
            existing.CO_WP_ActionTaken = model.CO_WP_ActionTaken;
            existing.CO_WP_Outcome = model.CO_WP_Outcome;
            existing.CO_WP_OutcomeRemark = model.CO_WP_OutcomeRemark;
            existing.CO_WP_OutcomeOutwardNo = model.CO_WP_OutcomeOutwardNo;
            existing.CO_WP_OutcomeOutwardDate = model.CO_WP_OutcomeOutwardDate;

            // Workman Reinstatement
            existing.CO_IsWorkmanReinstated = model.CO_IsWorkmanReinstated;
            existing.CO_ReinstatedSubjectToWP = model.CO_ReinstatedSubjectToWP;
            existing.CO_Reinstatement_StayGranted = model.CO_Reinstatement_StayGranted;
            existing.CO_Reinstatement_StayApprovalNo = model.CO_Reinstatement_StayApprovalNo;
            existing.CO_Reinstatement_StayNature = model.CO_Reinstatement_StayNature;
            existing.CO_Reinstatement_StayDate = model.CO_Reinstatement_StayDate;
            existing.CO_Reinstatement_StayRemark = model.CO_Reinstatement_StayRemark;
            existing.CO_ReinstatementApprovalIssued = model.CO_ReinstatementApprovalIssued;
            existing.CO_ReinstatementApprovalDate = model.CO_ReinstatementApprovalDate;
            existing.CO_Reinstatement_ApprovalNo = model.CO_Reinstatement_ApprovalNo;

            // WA (Writ Appeal)
            existing.CO_WA_CaseStatus_Option = model.CO_WA_CaseStatus_Option;
            existing.CO_WA_CaseNumber = model.CO_WA_CaseNumber;
            existing.CO_WA_Year = model.CO_WA_Year;
            existing.CO_WA_HighCourtBench = model.CO_WA_HighCourtBench;
            existing.CO_WA_EntrustmentNo = model.CO_WA_EntrustmentNo;
            existing.CO_WA_EntrustmentDate = model.CO_WA_EntrustmentDate;
            existing.CO_WA_AdvocateName = model.CO_WA_AdvocateName;
            existing.CO_WA_StayGranted = model.CO_WA_StayGranted;
            existing.CO_WA_StayApprovalNo = model.CO_WA_StayApprovalNo;
            existing.CO_WA_StayNature = model.CO_WA_StayNature;
            existing.CO_WA_StayDate = model.CO_WA_StayDate;
            existing.CO_WA_StayRemark = model.CO_WA_StayRemark;
            existing.CO_WA_Status = model.CO_WA_Status;
            existing.CO_WA_ActionTaken = model.CO_WA_ActionTaken;
            existing.CO_WA_Outcome = model.CO_WA_Outcome;
            existing.CO_WA_OutcomeRemark = model.CO_WA_OutcomeRemark;
            existing.CO_WA_OutcomeOutwardNo = model.CO_WA_OutcomeOutwardNo;
            existing.CO_WA_OutcomeOutwardDate = model.CO_WA_OutcomeOutwardDate;

            // CO Disposal
            existing.CO_Disposal_Nature = model.CO_Disposal_Nature;
            existing.CO_Disposal_CommSentToDivision = model.CO_Disposal_CommSentToDivision;
            existing.CO_Disposal_OutwardNo = model.CO_Disposal_OutwardNo;
            existing.CO_Disposal_Date = model.CO_Disposal_Date;
            existing.CO_Disposal_Decision = model.CO_Disposal_Decision;
            existing.CO_Disposal_ApprovalOutwardNo = model.CO_Disposal_ApprovalOutwardNo;
            existing.CO_Disposal_ApprovalDate = model.CO_Disposal_ApprovalDate;

            // Further Appeal
            existing.CO_FurtherAppeal_Status_Option = model.CO_FurtherAppeal_Status_Option;
            existing.CO_FurtherAppeal_CaseNumber = model.CO_FurtherAppeal_CaseNumber;
            existing.CO_FurtherAppeal_Year = model.CO_FurtherAppeal_Year;
            existing.CO_FurtherAppeal_EntrustmentNo = model.CO_FurtherAppeal_EntrustmentNo;
            existing.CO_FurtherAppeal_EntrustmentDate = model.CO_FurtherAppeal_EntrustmentDate;
            existing.CO_FurtherAppeal_AdvocateName = model.CO_FurtherAppeal_AdvocateName;
            existing.CO_FurtherAppeal_CaseStatus = model.CO_FurtherAppeal_CaseStatus;
            existing.CO_FurtherAppeal_DisposalOutwardNo = model.CO_FurtherAppeal_DisposalOutwardNo;
            existing.CO_FurtherAppeal_DisposalDate = model.CO_FurtherAppeal_DisposalDate;

            // Remarks & Opinions
            if (!string.IsNullOrEmpty(model.CO_Remarks)) existing.CO_Remarks = model.CO_Remarks;
            if (!string.IsNullOrEmpty(model.Opinion_CLO)) existing.Opinion_CLO = model.Opinion_CLO;
            if (!string.IsNullOrEmpty(model.Remarks)) existing.Remarks = model.Remarks;

            // Re-resolve parent case connection if provided
            if (!string.IsNullOrEmpty(model.Parent_CaseNumber) && model.Parent_CaseYear.HasValue)
            {
                var parentCase = _labourRepo.GetCaseByNumber(model.Parent_CaseNumber, model.Parent_CaseYear.Value);
                if (parentCase != null)
                {
                    existing.ParentCaseID = parentCase.CaseID;
                }
            }

            try
            {
                _arisingRepo.UpdateArisingApplication(existing);
                _ = _activityLogger.LogCaseUpdatedAsync("ARISING", existing.ArisingID, $"Arising {existing.CaseNumber}/{existing.CaseYear}", null, existing.CourtName, null, existing, $"Central Office Action recorded on Arising Application #{existing.CaseNumber}/{existing.CaseYear}");
                TempData["SuccessMessage"] = "Action recorded successfully.";
                return RedirectToAction("ArisingApplication");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Action Failed: " + ex.Message);
                PopulateDropdowns();
                return View("ActionArisingApplication", model);
            }
        }

        /// <summary>
        /// GET: Form to register a brand new Arising Application.
        /// </summary>
        [HttpGet]
        public IActionResult EditArisingApplication(int id)
        {
            var model = _arisingRepo.GetById(id);
            if (model == null) return NotFound();

            int divisionId = HttpContext.Session.GetInt32(SessionKeys.DivisionID) ?? -1;
            bool isCO = (divisionId == 0 || divisionId == 5);

            if (!isCO && model.SentToCO == true)
            {
                TempData["ErrorMessage"] = "This Arising Application has been forwarded to Central Office and cannot be edited by Division users.";
                return RedirectToAction("DetailsArisingApplication", new { id });
            }

            PopulateDropdowns();
            return View(model);
        }

        /// <summary>
        /// POST: Update an existing Arising Application with ACID transaction.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult EditArisingApplication(ArisingApplication model)
        {
            int divisionId = HttpContext.Session.GetInt32(SessionKeys.DivisionID) ?? -1;
            bool isCO = (divisionId == 0 || divisionId == 5);

            var existing = _arisingRepo.GetById(model.ArisingID);
            if (!isCO && existing != null && existing.SentToCO == true)
            {
                TempData["ErrorMessage"] = "This Arising Application has been forwarded to Central Office and cannot be edited by Division users.";
                return RedirectToAction("DetailsArisingApplication", new { id = model.ArisingID });
            }
            int userId = HttpContext.Session.GetInt32(SessionKeys.UserID) ?? 0;
            model.ModifiedBy = userId;

            // Handle File Uploads
            if (model.JudgmentCopyFile != null)
            {
                model.JudgmentCopyPath = SaveFile(model.JudgmentCopyFile, "Labour_Arising/Judgments");
            }
            if (model.HistorySheetFile != null)
            {
                model.DE_HistorySheetPath = SaveFile(model.HistorySheetFile, "Labour_Arising/HistorySheets");
            }
            if (model.LokAdalatDocumentFile != null)
            {
                model.LokAdalatDocumentPath = SaveFile(model.LokAdalatDocumentFile, "Labour_Arising/Settlements");
            }
            if (model.CO_WP_JudgmentCopyFile != null)
            {
                model.CO_WP_JudgmentCopyPath = SaveFile(model.CO_WP_JudgmentCopyFile, "Labour/WPJudgments");
            }
            if (model.ClaimPetitionFile != null)
            {
                model.ClaimPetitionPath = SaveFile(model.ClaimPetitionFile, "Labour_Arising/Claims");
            }

            // Preserve existing file paths and Central Office action data if not submitted in form
            if (existing != null)
            {
                if (model.JudgmentCopyFile == null) model.JudgmentCopyPath = existing.JudgmentCopyPath;
                if (model.HistorySheetFile == null) model.DE_HistorySheetPath = existing.DE_HistorySheetPath;
                if (model.LokAdalatDocumentFile == null) model.LokAdalatDocumentPath = existing.LokAdalatDocumentPath;
                if (model.CO_WP_JudgmentCopyFile == null) model.CO_WP_JudgmentCopyPath = existing.CO_WP_JudgmentCopyPath;
                if (model.ClaimPetitionFile == null) model.ClaimPetitionPath = existing.ClaimPetitionPath;
                if (string.IsNullOrEmpty(model.CO_ClosedDocumentPath)) model.CO_ClosedDocumentPath = existing.CO_ClosedDocumentPath;
                if (string.IsNullOrEmpty(model.CO_WP_StayOrderPath)) model.CO_WP_StayOrderPath = existing.CO_WP_StayOrderPath;
                if (string.IsNullOrEmpty(model.CO_WA_StayOrderPath)) model.CO_WA_StayOrderPath = existing.CO_WA_StayOrderPath;
                if (string.IsNullOrEmpty(model.CO_Reinstatement_StayOrderPath)) model.CO_Reinstatement_StayOrderPath = existing.CO_Reinstatement_StayOrderPath;
                if (string.IsNullOrEmpty(model.CO_Reinstatement_ApprovalCopyPath)) model.CO_Reinstatement_ApprovalCopyPath = existing.CO_Reinstatement_ApprovalCopyPath;

                if (model.CO_FeasibilityReceived == null) model.CO_FeasibilityReceived = existing.CO_FeasibilityReceived;
                if (model.CO_FeasibilityDate == null) model.CO_FeasibilityDate = existing.CO_FeasibilityDate;
                if (string.IsNullOrEmpty(model.CO_ActionTaken)) model.CO_ActionTaken = existing.CO_ActionTaken;
                if (string.IsNullOrEmpty(model.CO_ApprovalOutwardNo)) model.CO_ApprovalOutwardNo = existing.CO_ApprovalOutwardNo;
                if (model.CO_ApprovalDate == null) model.CO_ApprovalDate = existing.CO_ApprovalDate;

                if (string.IsNullOrEmpty(model.CO_WP_CaseNumber))
                {
                    model.CO_WP_CaseStatus_Option = existing.CO_WP_CaseStatus_Option;
                    model.CO_WP_CaseNumber = existing.CO_WP_CaseNumber;
                    model.CO_WP_Year = existing.CO_WP_Year;
                    model.CO_WP_HighCourtBench = existing.CO_WP_HighCourtBench;
                    model.CO_WP_EntrustmentNo = existing.CO_WP_EntrustmentNo;
                    model.CO_WP_EntrustmentDate = existing.CO_WP_EntrustmentDate;
                    model.CO_WP_AdvocateName = existing.CO_WP_AdvocateName;
                    model.CO_WP_StayGranted = existing.CO_WP_StayGranted;
                    model.CO_WP_StayApprovalNo = existing.CO_WP_StayApprovalNo;
                    model.CO_WP_StayNature = existing.CO_WP_StayNature;
                    model.CO_WP_StayDate = existing.CO_WP_StayDate;
                    model.CO_WP_StayRemark = existing.CO_WP_StayRemark;
                    model.CO_WP_Status = existing.CO_WP_Status;
                    model.CO_WP_Outcome = existing.CO_WP_Outcome;
                    model.CO_WP_OutcomeRemark = existing.CO_WP_OutcomeRemark;
                    model.CO_WP_OutcomeOutwardNo = existing.CO_WP_OutcomeOutwardNo;
                    model.CO_WP_OutcomeOutwardDate = existing.CO_WP_OutcomeOutwardDate;
                    model.CO_WP_ActionTaken = existing.CO_WP_ActionTaken;
                }

                if (string.IsNullOrEmpty(model.CO_WA_CaseNumber))
                {
                    model.CO_WA_CaseStatus_Option = existing.CO_WA_CaseStatus_Option;
                    model.CO_WA_CaseNumber = existing.CO_WA_CaseNumber;
                    model.CO_WA_Year = existing.CO_WA_Year;
                    model.CO_WA_HighCourtBench = existing.CO_WA_HighCourtBench;
                    model.CO_WA_EntrustmentNo = existing.CO_WA_EntrustmentNo;
                    model.CO_WA_EntrustmentDate = existing.CO_WA_EntrustmentDate;
                    model.CO_WA_AdvocateName = existing.CO_WA_AdvocateName;
                    model.CO_WA_StayGranted = existing.CO_WA_StayGranted;
                    model.CO_WA_StayApprovalNo = existing.CO_WA_StayApprovalNo;
                    model.CO_WA_StayNature = existing.CO_WA_StayNature;
                    model.CO_WA_StayDate = existing.CO_WA_StayDate;
                    model.CO_WA_StayRemark = existing.CO_WA_StayRemark;
                    model.CO_WA_Status = existing.CO_WA_Status;
                    model.CO_WA_Outcome = existing.CO_WA_Outcome;
                    model.CO_WA_OutcomeRemark = existing.CO_WA_OutcomeRemark;
                    model.CO_WA_OutcomeOutwardNo = existing.CO_WA_OutcomeOutwardNo;
                    model.CO_WA_OutcomeOutwardDate = existing.CO_WA_OutcomeOutwardDate;
                    model.CO_WA_ActionTaken = existing.CO_WA_ActionTaken;
                }

                if (!model.CO_IsWorkmanReinstated && existing.CO_IsWorkmanReinstated) model.CO_IsWorkmanReinstated = existing.CO_IsWorkmanReinstated;
                if (string.IsNullOrEmpty(model.CO_ReinstatedSubjectToWP)) model.CO_ReinstatedSubjectToWP = existing.CO_ReinstatedSubjectToWP;
                if (model.CO_Reinstatement_StayGranted == null) model.CO_Reinstatement_StayGranted = existing.CO_Reinstatement_StayGranted;
                if (string.IsNullOrEmpty(model.CO_Reinstatement_StayApprovalNo)) model.CO_Reinstatement_StayApprovalNo = existing.CO_Reinstatement_StayApprovalNo;
                if (string.IsNullOrEmpty(model.CO_Reinstatement_StayNature)) model.CO_Reinstatement_StayNature = existing.CO_Reinstatement_StayNature;
                if (model.CO_Reinstatement_StayDate == null) model.CO_Reinstatement_StayDate = existing.CO_Reinstatement_StayDate;
                if (string.IsNullOrEmpty(model.CO_Reinstatement_StayRemark)) model.CO_Reinstatement_StayRemark = existing.CO_Reinstatement_StayRemark;
                if (model.CO_ReinstatementApprovalIssued == null) model.CO_ReinstatementApprovalIssued = existing.CO_ReinstatementApprovalIssued;
                if (model.CO_ReinstatementApprovalDate == null) model.CO_ReinstatementApprovalDate = existing.CO_ReinstatementApprovalDate;
                if (string.IsNullOrEmpty(model.CO_Reinstatement_ApprovalNo)) model.CO_Reinstatement_ApprovalNo = existing.CO_Reinstatement_ApprovalNo;

                if (string.IsNullOrEmpty(model.CO_Disposal_Decision))
                {
                    model.CO_Disposal_Nature = existing.CO_Disposal_Nature;
                    model.CO_Disposal_CommSentToDivision = existing.CO_Disposal_CommSentToDivision;
                    model.CO_Disposal_OutwardNo = existing.CO_Disposal_OutwardNo;
                    model.CO_Disposal_Date = existing.CO_Disposal_Date;
                    model.CO_Disposal_Decision = existing.CO_Disposal_Decision;
                    model.CO_Disposal_ApprovalOutwardNo = existing.CO_Disposal_ApprovalOutwardNo;
                    model.CO_Disposal_ApprovalDate = existing.CO_Disposal_ApprovalDate;
                }

                if (string.IsNullOrEmpty(model.CO_FurtherAppeal_CaseNumber))
                {
                    model.CO_FurtherAppeal_Status_Option = existing.CO_FurtherAppeal_Status_Option;
                    model.CO_FurtherAppeal_CaseNumber = existing.CO_FurtherAppeal_CaseNumber;
                    model.CO_FurtherAppeal_Year = existing.CO_FurtherAppeal_Year;
                    model.CO_FurtherAppeal_EntrustmentNo = existing.CO_FurtherAppeal_EntrustmentNo;
                    model.CO_FurtherAppeal_EntrustmentDate = existing.CO_FurtherAppeal_EntrustmentDate;
                    model.CO_FurtherAppeal_AdvocateName = existing.CO_FurtherAppeal_AdvocateName;
                    model.CO_FurtherAppeal_CaseStatus = existing.CO_FurtherAppeal_CaseStatus;
                    model.CO_FurtherAppeal_DisposalOutwardNo = existing.CO_FurtherAppeal_DisposalOutwardNo;
                    model.CO_FurtherAppeal_DisposalDate = existing.CO_FurtherAppeal_DisposalDate;
                }

                if (string.IsNullOrEmpty(model.ActionTaken_LO)) model.ActionTaken_LO = existing.ActionTaken_LO;
                if (model.ApprovalDate_LO == null) model.ApprovalDate_LO = existing.ApprovalDate_LO;
                if (string.IsNullOrEmpty(model.Opinion_LO)) model.Opinion_LO = existing.Opinion_LO;
                if (string.IsNullOrEmpty(model.ActionTaken_DyCLO)) model.ActionTaken_DyCLO = existing.ActionTaken_DyCLO;
                if (model.ApprovalDate_DyCLO == null) model.ApprovalDate_DyCLO = existing.ApprovalDate_DyCLO;
                if (string.IsNullOrEmpty(model.Opinion_DyCLO)) model.Opinion_DyCLO = existing.Opinion_DyCLO;
                if (string.IsNullOrEmpty(model.ActionTaken_CLO)) model.ActionTaken_CLO = existing.ActionTaken_CLO;
                if (model.ApprovalDate_CLO == null) model.ApprovalDate_CLO = existing.ApprovalDate_CLO;
                if (string.IsNullOrEmpty(model.Opinion_CLO)) model.Opinion_CLO = existing.Opinion_CLO;
                if (string.IsNullOrEmpty(model.ActionTaken_MD)) model.ActionTaken_MD = existing.ActionTaken_MD;
                if (model.ApprovalDate_MD == null) model.ApprovalDate_MD = existing.ApprovalDate_MD;
                if (string.IsNullOrEmpty(model.Opinion_MD)) model.Opinion_MD = existing.Opinion_MD;
            }

            // Re-resolve parent case connection
            if (!string.IsNullOrEmpty(model.Parent_CaseNumber) && model.Parent_CaseYear.HasValue)
            {
                var parentCase = _labourRepo.GetCaseByNumber(model.Parent_CaseNumber, model.Parent_CaseYear.Value);
                if (parentCase != null)
                {
                    model.ParentCaseID = parentCase.CaseID;
                    model.Parent_CourtID = parentCase.CourtID;
                    model.Parent_CourtName = parentCase.CourtName;
                    model.Parent_CaseType = parentCase.CaseType;
                    model.Parent_CaseStatus = parentCase.CaseStatus;
                    model.Parent_PetitionerName = parentCase.PetitionerName;
                }
            }

            try
            {
                _arisingRepo.UpdateArisingApplication(model);
                _ = _activityLogger.LogCaseUpdatedAsync("ARISING", model.ArisingID, $"Arising {model.CaseNumber}/{model.CaseYear}", null, model.CourtName, existing, model, $"Updated Arising Application #{model.CaseNumber}/{model.CaseYear}");
                TempData["SuccessMessage"] = "Arising Application updated successfully!";
                return RedirectToAction("ArisingApplication");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Update Failed: " + ex.Message);
                TempData["ErrorMessage"] = "Failed to update arising application. " + ex.Message;
            }

            PopulateDropdowns();
            return View(model);
        }
        
        [HttpGet]
        public IActionResult Edit(int id)
        {
            var model = _labourRepo.GetCaseById(id);
            if (model == null) return NotFound();

            // Division ownership guard — prevent cross-division tampering
            int myDiv = HttpContext.Session.GetInt32(SessionKeys.DivisionID) ?? 0;
            bool isCO = (myDiv == 0 || myDiv == 5);
            if (!isCO && model.DivisionID != myDiv)
            {
                TempData["ErrorMessage"] = "Access Denied: You can only edit cases from your own division.";
                return RedirectToAction("Index");
            }

            // Lock guard — prevent division users from editing once sent to CO
            if (!isCO && model.SentToCO == true)
            {
                TempData["ErrorMessage"] = "This case has been sent to the Central Office and is now locked for editing.";
                return RedirectToAction("CaseList");
            }
            
            PopulateDropdowns();
            return View(model);
        }

        [HttpPost]
        public IActionResult DeleteArisingApplication(int id)
        {
            var app = _arisingRepo.GetById(id);
            _arisingRepo.DeleteArisingApplication(id);
            _ = _activityLogger.LogCaseDeletedAsync("Arising", id, app?.CaseNumber ?? $"Arising-{id}", null, $"Deleted Arising Application {app?.CaseNumber}");
            TempData["SuccessMessage"] = "Arising Application deleted successfully.";
            return RedirectToAction("ArisingApplication");
        }

        [HttpGet]
        public IActionResult DetailsArisingApplication(int id)
        {
            var model = _arisingRepo.GetById(id);
            if (model == null) return NotFound();

            var divIdString = User.FindFirst("DivisionID")?.Value;
            int sessDivId = HttpContext.Session.GetInt32(SessionKeys.DivisionID) ?? -1;
            var roleUpper = (User.FindFirst(ClaimTypes.Role)?.Value ?? "").ToUpperInvariant();
            bool isCO = string.IsNullOrEmpty(divIdString) || divIdString == "0" || divIdString == "5" 
                        || sessDivId == 0 || sessDivId == 5 
                        || roleUpper.Contains("MD") || roleUpper.Contains("CLO") || roleUpper.Contains("LO") 
                        || roleUpper.Contains("ADMIN") || roleUpper.Contains("CENTRAL");

            ViewBag.IsCentralOffice = isCO;

            // Fetch linked Parent Labour Case if available
            LabourCase? parentCase = null;
            if (model.ParentCaseID.HasValue && model.ParentCaseID.Value > 0)
            {
                parentCase = _labourRepo.GetCaseById(model.ParentCaseID.Value);
            }

            if (parentCase == null && !string.IsNullOrWhiteSpace(model.Parent_CaseNumber))
            {
                var cases = _labourRepo.GetCases(0, model.Parent_CaseNumber);
                if (model.Parent_CaseYear.HasValue)
                {
                    parentCase = cases.FirstOrDefault(c => c.CaseYear == model.Parent_CaseYear.Value);
                }
                if (parentCase == null)
                {
                    parentCase = cases.FirstOrDefault();
                }
            }

            ViewBag.LinkedParentCase = parentCase;

            // Fetch Connected EP Details for Arising Application / Parent Case
            int pCaseId = parentCase?.CaseID ?? (model.ParentCaseID ?? 0);
            var connectedEPs = _labourEPRepo.GetEPsByArisingApplication(pCaseId, model.CaseNumber).ToList();
            ViewBag.ConnectedEPs = connectedEPs;

            // Auto-fetch/extract Award Amount if not explicitly set
            if (!model.AwardAmount.HasValue || model.AwardAmount.Value <= 0)
            {
                string text = $"{model.FavorRemark} {model.AwardDetails} {model.ClaimDetails} {parentCase?.AwardDetails} {parentCase?.FavorRemark}";
                var match = System.Text.RegularExpressions.Regex.Match(text, @"(?:RS\.?|INR|₹)?\s*([0-9]{1,3}(?:,[0-9]{2,3})*(?:\.[0-9]{1,2})?|[0-9]{4,})(?:\s*/-)?", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                if (match.Success && match.Groups.Count > 1)
                {
                    string numStr = match.Groups[1].Value.Replace(",", "");
                    if (decimal.TryParse(numStr, out decimal parsedAmt) && parsedAmt > 0)
                    {
                        model.AwardAmount = parsedAmt;
                        _arisingRepo.UpdateAwardAmount(model.ArisingID, parsedAmt);
                    }
                }
            }

            ViewBag.CaseNotings = isCO ? _notingRepo.GetNotings("ARISING", id) : new List<MVCCaseManagement.Models.CaseNoting>();
            return View(model);
        }

        [HttpGet]
        public IActionResult Details(int id)
        {
            var model = _labourRepo.GetCaseById(id);
            if (model == null) return NotFound();

            var divIdString = User.FindFirst("DivisionID")?.Value;
            var roleUpper = (User.FindFirst(ClaimTypes.Role)?.Value ?? "").ToUpperInvariant();
            bool isCO = string.IsNullOrEmpty(divIdString) || divIdString == "0" || divIdString == "5"
                        || roleUpper.Contains("MD") || roleUpper.Contains("CLO") || roleUpper.Contains("LO")
                        || roleUpper.Contains("ADMIN") || roleUpper.Contains("CENTRAL");
            int userDivId = int.TryParse(divIdString, out int parsedId) ? parsedId : -1;

            // IDOR Protection: Non-CO users can only view their own division's cases
            if (!isCO && model.DivisionID != userDivId)
            {
                _logger.LogWarning("Unauthorized access attempt to Labour Case ID {CaseID} by User ID {UserID} from Division {UserDivID}", id, User.FindFirstValue(ClaimTypes.NameIdentifier), userDivId);
                TempData["ErrorMessage"] = "Access Denied: You cannot view cases from other divisions.";
                return RedirectToAction("Index");
            }

            ViewBag.IsCentralOffice = isCO;
            ViewBag.IsCLO = User.IsInRole("CLO") || User.IsInRole("Dy CLO") || User.IsInRole("DyCLO");
            ViewBag.CanEdit = !isCO && (model.SentToCO != true);

            if (isCO && model.SentToCO == true && !model.IsViewedByCO)
            {
                _labourRepo.MarkAsViewedByCO(id);
            }
            
            if (isCO)
            {
                _labourRepo.MarkCaseAsViewed(id); // Keep existing tracking table logic as well
            }
            
            // Fetch connected EPs
            model.ConnectedEPs = _labourEPRepo.GetEPsByCaseId(id).ToList();

            // Fetch Linked Arising Applications
            var linkedArisingApps = _arisingRepo.GetByParentCaseId(id, model.CaseNumber, model.CaseYear).ToList();
            ViewBag.LinkedArisingApplications = linkedArisingApps;

            ViewBag.CaseNotings = isCO ? _notingRepo.GetNotings("LABOUR", id) : new List<MVCCaseManagement.Models.CaseNoting>();
            return View(model);
        }

        [HttpPost]
        [IgnoreAntiforgeryToken]
        public IActionResult AddCaseNoting([FromBody] AddCaseNotingDto dto)
        {
            var divIdString = User.FindFirst("DivisionID")?.Value;
            var roleUpper = (User.FindFirst(ClaimTypes.Role)?.Value ?? "").ToUpperInvariant();
            bool isCentralOffice = string.IsNullOrEmpty(divIdString) || divIdString == "0" || divIdString == "5"
                                   || roleUpper.Contains("MD") || roleUpper.Contains("CLO") || roleUpper.Contains("LO")
                                   || roleUpper.Contains("ADMIN") || roleUpper.Contains("CENTRAL");

            if (!isCentralOffice)
            {
                return Json(new { success = false, message = "Division logins are not authorized to use case noting features." });
            }

            if (dto == null || string.IsNullOrWhiteSpace(dto.NotingText))
            {
                return Json(new { success = false, message = "Noting text cannot be empty." });
            }

            var username = User.Identity?.Name ?? "User";
            var fullName = User.FindFirst("FullName")?.Value;
            var role = User.FindFirst(ClaimTypes.Role)?.Value ?? "Officer";
            var division = User.FindFirst("DivisionName")?.Value;

            var noting = new CaseNoting
            {
                CaseType = string.IsNullOrEmpty(dto.CaseType) ? "LABOUR" : dto.CaseType.ToUpperInvariant(),
                CaseID = dto.CaseID,
                NotingText = dto.NotingText.Trim(),
                CreatedByUsername = username,
                CreatedByName = fullName,
                CreatedByRole = role,
                CreatedByDivision = division,
                CreatedDate = DateTime.Now,
                IsActive = true
            };

            int newId = _notingRepo.AddNoting(noting);
            if (newId > 0)
            {
                noting.NotingID = newId;
                _ = _activityLogger.LogSubEntityActionAsync("Labour", dto.CaseID, $"Labour-{dto.CaseID}", "NOTING_ADDED", $"Case noting added by {fullName} ({role}): {dto.NotingText}", dto);
                return Json(new 
                { 
                    success = true, 
                    message = "Noting saved successfully.",
                    noting = noting,
                    formattedDate = noting.CreatedDate.ToString("dd MMM yyyy, hh:mm tt")
                });
            }

            return Json(new { success = false, message = "Failed to save noting." });
        }

        [HttpPost]
        [IgnoreAntiforgeryToken]
        public IActionResult DeleteCaseNoting([FromBody] DeleteCaseNotingDto dto)
        {
            var divIdString = User.FindFirst("DivisionID")?.Value;
            var roleUpper = (User.FindFirst(ClaimTypes.Role)?.Value ?? "").ToUpperInvariant();
            bool isCentralOffice = string.IsNullOrEmpty(divIdString) || divIdString == "0" || divIdString == "5"
                                   || roleUpper.Contains("MD") || roleUpper.Contains("CLO") || roleUpper.Contains("LO")
                                   || roleUpper.Contains("ADMIN") || roleUpper.Contains("CENTRAL");

            if (!isCentralOffice)
            {
                return Json(new { success = false, message = "Division logins are not authorized to delete case notings." });
            }

            if (dto == null || dto.NotingID <= 0)
            {
                return Json(new { success = false, message = "Invalid noting ID." });
            }

            var existing = _notingRepo.GetNotingById(dto.NotingID);
            if (existing == null || !existing.IsActive)
            {
                return Json(new { success = false, message = "Noting not found or already deleted." });
            }

            var currentUsername = User.Identity?.Name ?? "";

            bool isCreator = existing.CreatedByUsername.Equals(currentUsername, StringComparison.OrdinalIgnoreCase);
            bool isPrivileged = roleUpper.Contains("MD") || roleUpper.Contains("CLO") || roleUpper.Contains("ADMIN");

            if (!isCreator && !isPrivileged)
            {
                return Json(new { success = false, message = "You are not authorized to delete this noting point." });
            }

            bool deleted = _notingRepo.DeleteNoting(dto.NotingID);
            if (deleted)
            {
                _ = _activityLogger.LogSubEntityActionAsync("Labour", existing.CaseID, $"Labour-{existing.CaseID}", "NOTING_DELETED", $"Case noting #{dto.NotingID} deleted: {existing.NotingText}", dto);
            }
            return Json(new { success = deleted, message = deleted ? "Noting deleted successfully." : "Failed to delete noting." });
        }

        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> UpdateCnr(int caseId, string? cnrNumber)
        {
            var divIdString = User.FindFirst("DivisionID")?.Value;
            bool isCO = string.IsNullOrEmpty(divIdString) || divIdString == "0" || divIdString == "5";
            int userDivId = int.TryParse(divIdString, out int parsedId) ? parsedId : -1;

            var dbCase = _labourRepo.GetCaseById(caseId) ?? _labourRepo.GetServiceMatterById(caseId);
            if (dbCase == null)
            {
                return Json(new { success = false, message = "Labour case not found." });
            }

            // IDOR Protection: Non-CO users can only update their own division's cases
            if (!isCO && dbCase.DivisionID != userDivId && !User.IsInRole("Admin"))
            {
                return Json(new { success = false, message = "Access denied: You cannot edit cases from other divisions." });
            }

            string? cleanCnr = cnrNumber?.Trim().ToUpperInvariant();
            if (!string.IsNullOrEmpty(cleanCnr))
            {
                if (cleanCnr.Length != 16)
                {
                    return Json(new { success = false, message = "CNR Number must be exactly 16 alphanumeric characters." });
                }
            }
            else
            {
                cleanCnr = null;
            }

            string? estCode = !string.IsNullOrEmpty(cleanCnr) && cleanCnr.Length >= 6 ? cleanCnr.Substring(0, 6) : null;
            int userId = int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int uid) ? uid : 0;

            bool updated = _labourRepo.UpdateCNR(caseId, cleanCnr, estCode, userId);

            if (updated)
            {
                _ = _activityLogger.LogSubEntityActionAsync("LABOUR", caseId, $"Labour {dbCase.CaseNumber}/{dbCase.CaseYear}", "CNR_UPDATED", string.IsNullOrEmpty(cleanCnr) ? "Unlinked CNR Number" : $"Updated CNR to {cleanCnr}", new { CNRNumber = cleanCnr, EstCode = estCode });

                if (!string.IsNullOrEmpty(cleanCnr))
                {
                    try
                    {
                        await _ecourtsRepo.SaveTrackedCaseAsync(new MVCCaseManagement.DAL.TrackedCaseModel
                        {
                            CNRNumber = cleanCnr,
                            EstCode = estCode ?? "",
                            CaseTypeCode = dbCase.CaseTypeCode ?? "47",
                            RegNo = dbCase.CaseNumber ?? "",
                            RegYear = dbCase.CaseYear ?? DateTime.Now.Year
                        });
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to register tracked case for Labour CNR {CNR}", cleanCnr);
                    }
                }

                return Json(new { 
                    success = true, 
                    message = string.IsNullOrEmpty(cleanCnr) ? "CNR Number unlinked successfully." : $"CNR Number successfully updated to {cleanCnr}.",
                    cnrNumber = cleanCnr ?? "",
                    estCode = estCode ?? ""
                });
            }

            return Json(new { success = false, message = "Failed to update CNR Number in database." });
        }

        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> UpdateAppealCnr(int caseId, string appealType, string? cnrNumber)
        {
            var divIdString = User.FindFirst("DivisionID")?.Value;
            bool isCO = string.IsNullOrEmpty(divIdString) || divIdString == "0" || divIdString == "5";
            int userDivId = int.TryParse(divIdString, out int parsedId) ? parsedId : -1;

            var dbCase = _labourRepo.GetCaseById(caseId) ?? _labourRepo.GetServiceMatterById(caseId);
            if (dbCase == null)
            {
                return Json(new { success = false, message = "Labour case not found." });
            }

            if (!isCO && dbCase.DivisionID != userDivId && !User.IsInRole("Admin"))
            {
                return Json(new { success = false, message = "Access denied: You cannot edit cases from other divisions." });
            }

            string? cleanCnr = cnrNumber?.Trim().ToUpperInvariant();
            if (!string.IsNullOrEmpty(cleanCnr))
            {
                if (cleanCnr.Length != 16)
                {
                    return Json(new { success = false, message = "CNR Number must be exactly 16 alphanumeric characters." });
                }
            }
            else
            {
                cleanCnr = null;
            }

            int userId = int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int uid) ? uid : 0;
            string normalizedType = (appealType ?? "").Trim().ToUpperInvariant();

            bool updated = _labourRepo.UpdateAppealCNR(caseId, normalizedType, cleanCnr, userId);

            if (updated)
            {
                _ = _activityLogger.LogSubEntityActionAsync("LABOUR", caseId, $"Labour {dbCase.CaseNumber}/{dbCase.CaseYear}", "APPEAL_CNR_UPDATED", string.IsNullOrEmpty(cleanCnr) ? $"Unlinked {normalizedType} CNR" : $"Updated {normalizedType} CNR to {cleanCnr}", new { AppealType = normalizedType, CNRNumber = cleanCnr });

                if (!string.IsNullOrEmpty(cleanCnr))
                {
                    try
                    {
                        await _ecourtsRepo.SaveTrackedCaseAsync(new MVCCaseManagement.DAL.TrackedCaseModel
                        {
                            CNRNumber = cleanCnr,
                            EstCode = cleanCnr.Length >= 6 ? cleanCnr.Substring(0, 6) : "KAHC01",
                            CaseTypeCode = normalizedType.Contains("WA") ? "WA" : "WP",
                            RegNo = dbCase.CaseNumber ?? "",
                            RegYear = dbCase.CaseYear ?? DateTime.Now.Year
                        });
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to register tracked case for Labour Appeal CNR {CNR}", cleanCnr);
                    }
                }

                return Json(new
                {
                    success = true,
                    message = string.IsNullOrEmpty(cleanCnr) ? "Appeal CNR unlinked successfully." : $"Appeal CNR successfully updated to {cleanCnr}.",
                    cnrNumber = cleanCnr ?? "",
                    appealType = normalizedType
                });
            }

            return Json(new { success = false, message = "Failed to update Appeal CNR in database." });
        }

        [HttpGet]
        public IActionResult ServiceMatterDetails(int id)
        {
            var model = _labourRepo.GetServiceMatterById(id);
            if (model == null) return NotFound();

            var divIdString = User.FindFirst("DivisionID")?.Value;
            bool isCO = string.IsNullOrEmpty(divIdString) || divIdString == "0" || divIdString == "5";
            int userDivId = int.TryParse(divIdString, out int parsedId) ? parsedId : -1;

            if (!isCO && model.DivisionID != userDivId)
            {
                _logger.LogWarning("Unauthorized access attempt to Service Matter ID {ServiceID} by User ID {UserID}", id, User.FindFirstValue(ClaimTypes.NameIdentifier));
                return RedirectToAction("Index");
            }

            ViewBag.IsCentralOffice = isCO;
            ViewBag.IsCLO = User.IsInRole("CLO") || User.IsInRole("Dy CLO") || User.IsInRole("DyCLO");
            ViewBag.CanEdit = !isCO; // Same logic as Labour cases
            ViewBag.IsServiceMatter = true;

            // Use the ServiceID as CaseID for links in Details view if they expect standard ID
            if (model.CaseID == 0) model.CaseID = model.ServiceID ?? 0;

            return View("Details", model);
        }

        [HttpGet]
        public IActionResult Action(int id, bool isArising = false)
        {
            if (isArising)
            {
                var model = _arisingRepo.GetById(id);
                if (model == null) return NotFound();

                var divIdString = User.FindFirst("DivisionID")?.Value;
                int sessDivId = HttpContext.Session.GetInt32(SessionKeys.DivisionID) ?? -1;
                bool isCOUser = string.IsNullOrEmpty(divIdString) || divIdString == "0" || divIdString == "5" 
                            || sessDivId == 0 || sessDivId == 5 
                            || User.IsInRole("MD") || User.IsInRole("CLO") || User.IsInRole("CentralOffice") || User.IsInRole("CO") || User.IsInRole("Admin");

                if (!isCOUser)
                {
                    TempData["ErrorMessage"] = "Access Denied: Only Central Office can perform this action.";
                    return RedirectToAction("DetailsArisingApplication", new { id });
                }

                PopulateDropdowns();
                return View("ActionArisingApplication", model);
            }
            else
            {
                var model = _labourRepo.GetCaseById(id);
                if (model == null)
                {
                    var svcModel = _labourRepo.GetServiceMatterById(id);
                    if (svcModel != null)
                    {
                        return RedirectToAction("ManageServiceMatter", new { id = svcModel.ServiceID ?? id });
                    }
                    return NotFound();
                }

                var divClaim = User.FindFirst("DivisionID")?.Value;
                int divisionId = int.TryParse(divClaim, out int parsedDiv) ? parsedDiv : (HttpContext.Session.GetInt32(SessionKeys.DivisionID) ?? 0);
                bool isCO = (divisionId == 0 || divisionId == 5 || User.IsInRole("CentralOffice") || User.IsInRole("CO") || User.IsInRole("CLO") || User.IsInRole("MD") || User.IsInRole("Admin"));
                if (!isCO)
                {
                    TempData["ErrorMessage"] = "Access Denied: Only Central Office can perform this action.";
                    return RedirectToAction("Details", new { id });
                }

                PopulateDropdowns();
                return View(model);
            }
        }

        // ==========================================
        // ==========================================
        // PER-ROLE ACTIONS: LO, Dy CLO, CLO, MD
        // Supports Standard Labour Cases and Service Matters
        // ==========================================

        [HttpGet]
        [Authorize]
        public IActionResult LOAction(int id, bool isService = false, bool isArising = false)
        {
            if (isArising) return RedirectToAction("ArisingLOAction", new { id });

            LabourCase? model = null;
            if (isService)
            {
                model = _labourRepo.GetServiceMatterById(id);
            }
            else
            {
                model = _labourRepo.GetCaseById(id) ?? _labourRepo.GetServiceMatterById(id);
            }
            if (model == null) return NotFound();

            bool isAuthorized = User.IsInRole("LO") || User.IsInRole("Admin");
            int divisionId = HttpContext.Session.GetInt32(SessionKeys.DivisionID) ?? 0;
            bool isServiceMatter = isService || model.CaseType == "Service Matter" || (model.ServiceID.HasValue && model.ServiceID.Value > 0 && model.CaseID == 0);

            if (!isAuthorized && divisionId != 0 && divisionId != 5)
            {
                TempData["ErrorMessage"] = "Access Denied: Only Law Officer can record this action.";
                if (isServiceMatter)
                    return RedirectToAction("ServiceMatterDetails", new { id = model.ServiceID ?? model.CaseID });
                return RedirectToAction("Details", new { id });
            }

            ViewBag.IsServiceMatter = isServiceMatter;
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public IActionResult LOAction(LabourCase model, bool isService = false)
        {
            bool isAuthorized = User.IsInRole("LO") || User.IsInRole("Admin");
            int divisionId = HttpContext.Session.GetInt32(SessionKeys.DivisionID) ?? 0;
            bool isServiceMatter = isService || model.CaseType == "Service Matter" || (model.ServiceID.HasValue && model.ServiceID.Value > 0);

            if (!isAuthorized && divisionId != 0 && divisionId != 5)
            {
                TempData["ErrorMessage"] = "Access Denied: Only Law Officer can record this action.";
                if (isServiceMatter)
                    return RedirectToAction("ServiceMatterDetails", new { id = model.ServiceID ?? model.CaseID });
                return RedirectToAction("Details", new { id = model.CaseID });
            }

            int userId = HttpContext.Session.GetInt32(SessionKeys.UserID) ?? 0;

            if (isServiceMatter)
            {
                int svcId = (model.ServiceID.HasValue && model.ServiceID.Value > 0) ? model.ServiceID.Value : model.CaseID;
                _labourRepo.UpdateServiceMatterRoleAction(svcId, "LO", model.ActionTaken_LO, model.ApprovalDate_LO, model.Opinion_LO, userId);
                _ = _activityLogger.LogSubEntityActionAsync("LABOUR", svcId, $"Service Matter #{svcId}", "CENTRAL_OFFICER_REVIEW", $"Law Officer (LO) action recorded: {model.ActionTaken_LO}", new { Action = model.ActionTaken_LO, ApprovalDate = model.ApprovalDate_LO, Opinion = model.Opinion_LO });
                TempData["SuccessMessage"] = "Law Officer action recorded successfully.";
                return RedirectToAction("ServiceMatterDetails", new { id = svcId });
            }

            var dbCase = _labourRepo.GetCaseById(model.CaseID);
            if (dbCase == null) return NotFound();

            dbCase.ActionTaken_LO = model.ActionTaken_LO;
            dbCase.ApprovalDate_LO = model.ApprovalDate_LO;
            dbCase.Opinion_LO = model.Opinion_LO;
            dbCase.ModifiedBy = userId;

            _labourRepo.UpdateCase(dbCase);
            _ = _activityLogger.LogSubEntityActionAsync("LABOUR", model.CaseID, $"Labour {dbCase.CaseNumber}/{dbCase.CaseYear}", "CENTRAL_OFFICER_REVIEW", $"Law Officer (LO) proposal recorded: {model.ActionTaken_LO}", new { Action = model.ActionTaken_LO, ApprovalDate = model.ApprovalDate_LO, Opinion = model.Opinion_LO });

            TempData["SuccessMessage"] = "Law Officer action recorded successfully.";
            return RedirectToAction("Details", new { id = model.CaseID });
        }

        [HttpGet]
        [Authorize]
        public IActionResult DyCLOAction(int id, bool isService = false, bool isArising = false)
        {
            if (isArising) return RedirectToAction("ArisingDyCLOAction", new { id });

            LabourCase? model = null;
            if (isService)
            {
                model = _labourRepo.GetServiceMatterById(id);
            }
            else
            {
                model = _labourRepo.GetCaseById(id) ?? _labourRepo.GetServiceMatterById(id);
            }
            if (model == null) return NotFound();

            bool isAuthorized = User.IsInRole("Dy CLO") || User.IsInRole("DyCLO") || User.IsInRole("Admin");
            int divisionId = HttpContext.Session.GetInt32(SessionKeys.DivisionID) ?? 0;
            bool isServiceMatter = isService || model.CaseType == "Service Matter" || (model.ServiceID.HasValue && model.ServiceID.Value > 0 && model.CaseID == 0);

            if (!isAuthorized && divisionId != 0 && divisionId != 5)
            {
                TempData["ErrorMessage"] = "Access Denied: Only Deputy Chief Law Officer can record this action.";
                if (isServiceMatter)
                    return RedirectToAction("ServiceMatterDetails", new { id = model.ServiceID ?? model.CaseID });
                return RedirectToAction("Details", new { id });
            }

            ViewBag.IsServiceMatter = isServiceMatter;
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public IActionResult DyCLOAction(LabourCase model, bool isService = false)
        {
            bool isAuthorized = User.IsInRole("Dy CLO") || User.IsInRole("DyCLO") || User.IsInRole("Admin");
            int divisionId = HttpContext.Session.GetInt32(SessionKeys.DivisionID) ?? 0;
            bool isServiceMatter = isService || model.CaseType == "Service Matter" || (model.ServiceID.HasValue && model.ServiceID.Value > 0);

            if (!isAuthorized && divisionId != 0 && divisionId != 5)
            {
                TempData["ErrorMessage"] = "Access Denied: Only Deputy Chief Law Officer can record this action.";
                if (isServiceMatter)
                    return RedirectToAction("ServiceMatterDetails", new { id = model.ServiceID ?? model.CaseID });
                return RedirectToAction("Details", new { id = model.CaseID });
            }

            int userId = HttpContext.Session.GetInt32(SessionKeys.UserID) ?? 0;

            if (isServiceMatter)
            {
                int svcId = (model.ServiceID.HasValue && model.ServiceID.Value > 0) ? model.ServiceID.Value : model.CaseID;
                _labourRepo.UpdateServiceMatterRoleAction(svcId, "Dy CLO", model.ActionTaken_DyCLO, model.ApprovalDate_DyCLO, model.Opinion_DyCLO, userId);
                _ = _activityLogger.LogSubEntityActionAsync("LABOUR", svcId, $"Service Matter #{svcId}", "CENTRAL_OFFICER_REVIEW", $"Deputy CLO review recorded: {model.ActionTaken_DyCLO}", new { Action = model.ActionTaken_DyCLO, ApprovalDate = model.ApprovalDate_DyCLO, Opinion = model.Opinion_DyCLO });
                TempData["SuccessMessage"] = "Deputy Chief Law Officer action recorded successfully.";
                return RedirectToAction("ServiceMatterDetails", new { id = svcId });
            }

            var dbCase = _labourRepo.GetCaseById(model.CaseID);
            if (dbCase == null) return NotFound();

            dbCase.ActionTaken_DyCLO = model.ActionTaken_DyCLO;
            dbCase.ApprovalDate_DyCLO = model.ApprovalDate_DyCLO;
            dbCase.Opinion_DyCLO = model.Opinion_DyCLO;
            dbCase.ModifiedBy = userId;

            _labourRepo.UpdateCase(dbCase);
            _ = _activityLogger.LogSubEntityActionAsync("LABOUR", model.CaseID, $"Labour {dbCase.CaseNumber}/{dbCase.CaseYear}", "CENTRAL_OFFICER_REVIEW", $"Deputy CLO review recorded: {model.ActionTaken_DyCLO}", new { Action = model.ActionTaken_DyCLO, ApprovalDate = model.ApprovalDate_DyCLO, Opinion = model.Opinion_DyCLO });

            TempData["SuccessMessage"] = "Deputy Chief Law Officer action recorded successfully.";
            return RedirectToAction("Details", new { id = model.CaseID });
        }

        [HttpGet]
        [Authorize]
        public IActionResult CLOAction(int id, bool isService = false, bool isArising = false)
        {
            if (isArising) return RedirectToAction("ArisingCLOAction", new { id });

            LabourCase? model = null;
            if (isService)
            {
                model = _labourRepo.GetServiceMatterById(id);
            }
            else
            {
                model = _labourRepo.GetCaseById(id) ?? _labourRepo.GetServiceMatterById(id);
            }
            if (model == null) return NotFound();

            int divisionId = HttpContext.Session.GetInt32(SessionKeys.DivisionID) ?? 0;
            bool isAuthorized = User.IsInRole("CLO") || User.IsInRole("Dy CLO") || User.IsInRole("DyCLO") || User.IsInRole("Admin");
            bool isServiceMatter = isService || model.CaseType == "Service Matter" || (model.ServiceID.HasValue && model.ServiceID.Value > 0 && model.CaseID == 0);

            if (!isAuthorized && divisionId != 0 && divisionId != 5)
            {
                TempData["ErrorMessage"] = "Access Denied: Only CLO can record this action.";
                if (isServiceMatter)
                    return RedirectToAction("ServiceMatterDetails", new { id = model.ServiceID ?? model.CaseID });
                return RedirectToAction("Details", new { id });
            }

            ViewBag.IsServiceMatter = isServiceMatter;
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public IActionResult CLOAction(LabourCase model, bool isService = false)
        {
            _logger.LogInformation("Processing CLOAction for Labour Case ID: {CaseID}", model.CaseID);
            
            int divisionId = HttpContext.Session.GetInt32(SessionKeys.DivisionID) ?? 0;
            bool isAuthorized = User.IsInRole("CLO") || User.IsInRole("Dy CLO") || User.IsInRole("DyCLO") || User.IsInRole("Admin");
            bool isServiceMatter = isService || model.CaseType == "Service Matter" || (model.ServiceID.HasValue && model.ServiceID.Value > 0);

            if (!isAuthorized && divisionId != 0 && divisionId != 5)
            {
                TempData["ErrorMessage"] = "Access Denied: Only Central Office / CLO can perform this action.";
                if (isServiceMatter)
                    return RedirectToAction("ServiceMatterDetails", new { id = model.ServiceID ?? model.CaseID });
                return RedirectToAction("Details", new { id = model.CaseID });
            }

            int userId = HttpContext.Session.GetInt32(SessionKeys.UserID) ?? 0;
            string actionVal = !string.IsNullOrEmpty(model.ActionTaken_CLO) ? model.ActionTaken_CLO : model.CO_Disposal_Decision;
            DateTime? approvalDateVal = model.ApprovalDate_CLO ?? model.CO_ApprovalDate;

            if (isServiceMatter)
            {
                int svcId = (model.ServiceID.HasValue && model.ServiceID.Value > 0) ? model.ServiceID.Value : model.CaseID;
                _labourRepo.UpdateServiceMatterRoleAction(svcId, "CLO", actionVal, approvalDateVal, model.Opinion_CLO, userId);
                _ = _activityLogger.LogSubEntityActionAsync("LABOUR", svcId, $"Service Matter #{svcId}", "CENTRAL_OFFICER_REVIEW", $"Chief Law Officer (CLO) decision: {actionVal}", new { Action = actionVal, ApprovalDate = approvalDateVal, Opinion = model.Opinion_CLO });
                TempData["SuccessMessage"] = "Chief Law Officer action recorded successfully.";
                return RedirectToAction("ServiceMatterDetails", new { id = svcId });
            }

            var dbCase = _labourRepo.GetCaseById(model.CaseID);
            if (dbCase == null)
            {
                _logger.LogWarning("Labour Case not found for CLOAction: ID {CaseID}", model.CaseID);
                return NotFound();
            }

            dbCase.ActionTaken_CLO = actionVal;
            dbCase.ApprovalDate_CLO = approvalDateVal;
            dbCase.Opinion_CLO = model.Opinion_CLO;

            // Backward compat
            dbCase.CO_Disposal_Decision = actionVal;
            dbCase.CO_ApprovalDate = approvalDateVal;
            if (actionVal == "Approved")
            {
                dbCase.CO_ActionTaken = "Pending before Competent Authority";
            }
            else if (actionVal == "Rejected")
            {
                dbCase.CO_ActionTaken = "CLOSED";
            }

            dbCase.ModifiedBy = userId;
            _labourRepo.UpdateCase(dbCase);
            _ = _activityLogger.LogSubEntityActionAsync("LABOUR", model.CaseID, $"Labour {dbCase.CaseNumber}/{dbCase.CaseYear}", "CENTRAL_OFFICER_REVIEW", $"Chief Law Officer (CLO) decision: {actionVal}", new { Action = actionVal, ApprovalDate = approvalDateVal, Opinion = model.Opinion_CLO });

            TempData["SuccessMessage"] = "Chief Law Officer action recorded successfully.";
            return RedirectToAction("Details", new { id = model.CaseID });
        }

        [HttpGet]
        [Authorize]
        public IActionResult MDAction(int id, bool isService = false, bool isArising = false)
        {
            if (isArising) return RedirectToAction("ArisingMDAction", new { id });

            LabourCase? model = null;
            if (isService)
            {
                model = _labourRepo.GetServiceMatterById(id);
            }
            else
            {
                model = _labourRepo.GetCaseById(id) ?? _labourRepo.GetServiceMatterById(id);
            }
            if (model == null) return NotFound();

            bool isAuthorized = User.IsInRole("MD") || User.IsInRole("Admin");
            int divisionId = HttpContext.Session.GetInt32(SessionKeys.DivisionID) ?? 0;
            bool isServiceMatter = isService || model.CaseType == "Service Matter" || (model.ServiceID.HasValue && model.ServiceID.Value > 0 && model.CaseID == 0);

            if (!isAuthorized && divisionId != 0 && divisionId != 5)
            {
                TempData["ErrorMessage"] = "Access Denied: Only Managing Director can record this action.";
                if (isServiceMatter)
                    return RedirectToAction("ServiceMatterDetails", new { id = model.ServiceID ?? model.CaseID });
                return RedirectToAction("Details", new { id });
            }

            ViewBag.IsServiceMatter = isServiceMatter;
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public IActionResult MDAction(LabourCase model, bool isService = false)
        {
            bool isAuthorized = User.IsInRole("MD") || User.IsInRole("Admin");
            int divisionId = HttpContext.Session.GetInt32(SessionKeys.DivisionID) ?? 0;
            bool isServiceMatter = isService || model.CaseType == "Service Matter" || (model.ServiceID.HasValue && model.ServiceID.Value > 0);

            if (!isAuthorized && divisionId != 0 && divisionId != 5)
            {
                TempData["ErrorMessage"] = "Access Denied: Only Managing Director can record this action.";
                if (isServiceMatter)
                    return RedirectToAction("ServiceMatterDetails", new { id = model.ServiceID ?? model.CaseID });
                return RedirectToAction("Details", new { id = model.CaseID });
            }

            int userId = HttpContext.Session.GetInt32(SessionKeys.UserID) ?? 0;

            if (isServiceMatter)
            {
                int svcId = (model.ServiceID.HasValue && model.ServiceID.Value > 0) ? model.ServiceID.Value : model.CaseID;
                _labourRepo.UpdateServiceMatterRoleAction(svcId, "MD", model.ActionTaken_MD, model.ApprovalDate_MD, model.Opinion_MD, userId);
                _ = _activityLogger.LogSubEntityActionAsync("LABOUR", svcId, $"Service Matter #{svcId}", "CENTRAL_OFFICER_REVIEW", $"Managing Director (MD) sanction: {model.ActionTaken_MD}", new { Action = model.ActionTaken_MD, ApprovalDate = model.ApprovalDate_MD, Opinion = model.Opinion_MD });
                TempData["SuccessMessage"] = "Managing Director action recorded successfully.";
                return RedirectToAction("ServiceMatterDetails", new { id = svcId });
            }

            var dbCase = _labourRepo.GetCaseById(model.CaseID);
            if (dbCase == null) return NotFound();

            dbCase.ActionTaken_MD = model.ActionTaken_MD;
            dbCase.ApprovalDate_MD = model.ApprovalDate_MD;
            dbCase.Opinion_MD = model.Opinion_MD;

            // Update CO_ActionTaken if MD decides
            if (model.ActionTaken_MD == "Approved")
            {
                dbCase.CO_ActionTaken = "APPROVED";
            }
            else if (model.ActionTaken_MD == "Rejected")
            {
                dbCase.CO_ActionTaken = "REJECTED";
            }

            dbCase.ModifiedBy = userId;
            _labourRepo.UpdateCase(dbCase);
            _ = _activityLogger.LogSubEntityActionAsync("LABOUR", model.CaseID, $"Labour {dbCase.CaseNumber}/{dbCase.CaseYear}", "CENTRAL_OFFICER_REVIEW", $"Managing Director (MD) sanction: {model.ActionTaken_MD}", new { Action = model.ActionTaken_MD, ApprovalDate = model.ApprovalDate_MD, Opinion = model.Opinion_MD });

            TempData["SuccessMessage"] = "Managing Director action recorded successfully.";
            return RedirectToAction("Details", new { id = model.CaseID });
        }

        // ==========================================
        // PER-ROLE ACTIONS FOR ARISING APPLICATIONS: LO, Dy CLO, CLO, MD
        // ==========================================

        [HttpGet]
        [Authorize]
        public IActionResult ArisingLOAction(int id)
        {
            var model = _arisingRepo.GetById(id);
            if (model == null) return NotFound();

            bool isAuthorized = User.IsInRole("LO") || User.IsInRole("Admin");
            var divIdString = User.FindFirst("DivisionID")?.Value;
            int sessDivId = HttpContext.Session.GetInt32(SessionKeys.DivisionID) ?? -1;
            bool isCOUser = string.IsNullOrEmpty(divIdString) || divIdString == "0" || divIdString == "5" || sessDivId == 0 || sessDivId == 5;

            if (!isAuthorized && !isCOUser)
            {
                TempData["ErrorMessage"] = "Access Denied: Only Law Officer can record this action.";
                return RedirectToAction("DetailsArisingApplication", new { id });
            }

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public IActionResult ArisingLOAction(ArisingApplication model)
        {
            bool isAuthorized = User.IsInRole("LO") || User.IsInRole("Admin");
            var divIdString = User.FindFirst("DivisionID")?.Value;
            int sessDivId = HttpContext.Session.GetInt32(SessionKeys.DivisionID) ?? -1;
            bool isCOUser = string.IsNullOrEmpty(divIdString) || divIdString == "0" || divIdString == "5" || sessDivId == 0 || sessDivId == 5;

            if (!isAuthorized && !isCOUser)
            {
                TempData["ErrorMessage"] = "Access Denied: Only Law Officer can record this action.";
                return RedirectToAction("DetailsArisingApplication", new { id = model.ArisingID });
            }

            int userId = HttpContext.Session.GetInt32(SessionKeys.UserID) ?? 0;
            _arisingRepo.UpdateArisingRoleAction(model.ArisingID, "LO", model.ActionTaken_LO, model.ApprovalDate_LO, model.Opinion_LO, userId);
            _ = _activityLogger.LogSubEntityActionAsync("ARISING", model.ArisingID, $"Arising #{model.ArisingID}", "CENTRAL_OFFICER_REVIEW", $"Law Officer (LO) action: {model.ActionTaken_LO}", new { Action = model.ActionTaken_LO, ApprovalDate = model.ApprovalDate_LO, Opinion = model.Opinion_LO });

            TempData["SuccessMessage"] = "Law Officer action recorded successfully.";
            return RedirectToAction("DetailsArisingApplication", new { id = model.ArisingID });
        }

        [HttpGet]
        [Authorize]
        public IActionResult ArisingDyCLOAction(int id)
        {
            var model = _arisingRepo.GetById(id);
            if (model == null) return NotFound();

            bool isAuthorized = User.IsInRole("Dy CLO") || User.IsInRole("DyCLO") || User.IsInRole("Admin");
            var divIdString = User.FindFirst("DivisionID")?.Value;
            int sessDivId = HttpContext.Session.GetInt32(SessionKeys.DivisionID) ?? -1;
            bool isCOUser = string.IsNullOrEmpty(divIdString) || divIdString == "0" || divIdString == "5" || sessDivId == 0 || sessDivId == 5;

            if (!isAuthorized && !isCOUser)
            {
                TempData["ErrorMessage"] = "Access Denied: Only Deputy Chief Law Officer can record this action.";
                return RedirectToAction("DetailsArisingApplication", new { id });
            }

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public IActionResult ArisingDyCLOAction(ArisingApplication model)
        {
            bool isAuthorized = User.IsInRole("Dy CLO") || User.IsInRole("DyCLO") || User.IsInRole("Admin");
            var divIdString = User.FindFirst("DivisionID")?.Value;
            int sessDivId = HttpContext.Session.GetInt32(SessionKeys.DivisionID) ?? -1;
            bool isCOUser = string.IsNullOrEmpty(divIdString) || divIdString == "0" || divIdString == "5" || sessDivId == 0 || sessDivId == 5;

            if (!isAuthorized && !isCOUser)
            {
                TempData["ErrorMessage"] = "Access Denied: Only Deputy Chief Law Officer can record this action.";
                return RedirectToAction("DetailsArisingApplication", new { id = model.ArisingID });
            }

            int userId = HttpContext.Session.GetInt32(SessionKeys.UserID) ?? 0;
            _arisingRepo.UpdateArisingRoleAction(model.ArisingID, "Dy CLO", model.ActionTaken_DyCLO, model.ApprovalDate_DyCLO, model.Opinion_DyCLO, userId);
            _ = _activityLogger.LogSubEntityActionAsync("ARISING", model.ArisingID, $"Arising #{model.ArisingID}", "CENTRAL_OFFICER_REVIEW", $"Deputy CLO review: {model.ActionTaken_DyCLO}", new { Action = model.ActionTaken_DyCLO, ApprovalDate = model.ApprovalDate_DyCLO, Opinion = model.Opinion_DyCLO });

            TempData["SuccessMessage"] = "Deputy Chief Law Officer action recorded successfully.";
            return RedirectToAction("DetailsArisingApplication", new { id = model.ArisingID });
        }

        [HttpGet]
        [Authorize]
        public IActionResult ArisingCLOAction(int id)
        {
            var model = _arisingRepo.GetById(id);
            if (model == null) return NotFound();

            bool isAuthorized = User.IsInRole("CLO") || User.IsInRole("Dy CLO") || User.IsInRole("DyCLO") || User.IsInRole("Admin");
            var divIdString = User.FindFirst("DivisionID")?.Value;
            int sessDivId = HttpContext.Session.GetInt32(SessionKeys.DivisionID) ?? -1;
            bool isCOUser = string.IsNullOrEmpty(divIdString) || divIdString == "0" || divIdString == "5" || sessDivId == 0 || sessDivId == 5;

            if (!isAuthorized && !isCOUser)
            {
                TempData["ErrorMessage"] = "Access Denied: Only Chief Law Officer can record this action.";
                return RedirectToAction("DetailsArisingApplication", new { id });
            }

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public IActionResult ArisingCLOAction(ArisingApplication model)
        {
            bool isAuthorized = User.IsInRole("CLO") || User.IsInRole("Dy CLO") || User.IsInRole("DyCLO") || User.IsInRole("Admin");
            var divIdString = User.FindFirst("DivisionID")?.Value;
            int sessDivId = HttpContext.Session.GetInt32(SessionKeys.DivisionID) ?? -1;
            bool isCOUser = string.IsNullOrEmpty(divIdString) || divIdString == "0" || divIdString == "5" || sessDivId == 0 || sessDivId == 5;

            if (!isAuthorized && !isCOUser)
            {
                TempData["ErrorMessage"] = "Access Denied: Only Chief Law Officer can record this action.";
                return RedirectToAction("DetailsArisingApplication", new { id = model.ArisingID });
            }

            int userId = HttpContext.Session.GetInt32(SessionKeys.UserID) ?? 0;
            string actionVal = !string.IsNullOrEmpty(model.ActionTaken_CLO) ? model.ActionTaken_CLO : model.CO_Disposal_Decision;
            DateTime? approvalDateVal = model.ApprovalDate_CLO ?? model.CO_ApprovalDate;

            _arisingRepo.UpdateArisingRoleAction(model.ArisingID, "CLO", actionVal, approvalDateVal, model.Opinion_CLO, userId);
            _ = _activityLogger.LogSubEntityActionAsync("ARISING", model.ArisingID, $"Arising #{model.ArisingID}", "CENTRAL_OFFICER_REVIEW", $"Chief Law Officer (CLO) decision: {actionVal}", new { Action = actionVal, ApprovalDate = approvalDateVal, Opinion = model.Opinion_CLO });

            TempData["SuccessMessage"] = "Chief Law Officer action recorded successfully.";
            return RedirectToAction("DetailsArisingApplication", new { id = model.ArisingID });
        }

        [HttpGet]
        [Authorize]
        public IActionResult ArisingMDAction(int id)
        {
            var model = _arisingRepo.GetById(id);
            if (model == null) return NotFound();

            bool isAuthorized = User.IsInRole("MD") || User.IsInRole("Admin");
            var divIdString = User.FindFirst("DivisionID")?.Value;
            int sessDivId = HttpContext.Session.GetInt32(SessionKeys.DivisionID) ?? -1;
            bool isCOUser = string.IsNullOrEmpty(divIdString) || divIdString == "0" || divIdString == "5" || sessDivId == 0 || sessDivId == 5;

            if (!isAuthorized && !isCOUser)
            {
                TempData["ErrorMessage"] = "Access Denied: Only Managing Director can record this action.";
                return RedirectToAction("DetailsArisingApplication", new { id });
            }

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public IActionResult ArisingMDAction(ArisingApplication model)
        {
            bool isAuthorized = User.IsInRole("MD") || User.IsInRole("Admin");
            var divIdString = User.FindFirst("DivisionID")?.Value;
            int sessDivId = HttpContext.Session.GetInt32(SessionKeys.DivisionID) ?? -1;
            bool isCOUser = string.IsNullOrEmpty(divIdString) || divIdString == "0" || divIdString == "5" || sessDivId == 0 || sessDivId == 5;

            if (!isAuthorized && !isCOUser)
            {
                TempData["ErrorMessage"] = "Access Denied: Only Managing Director can record this action.";
                return RedirectToAction("DetailsArisingApplication", new { id = model.ArisingID });
            }

            int userId = HttpContext.Session.GetInt32(SessionKeys.UserID) ?? 0;
            _arisingRepo.UpdateArisingRoleAction(model.ArisingID, "MD", model.ActionTaken_MD, model.ApprovalDate_MD, model.Opinion_MD, userId);
            _ = _activityLogger.LogSubEntityActionAsync("ARISING", model.ArisingID, $"Arising #{model.ArisingID}", "CENTRAL_OFFICER_REVIEW", $"Managing Director (MD) sanction: {model.ActionTaken_MD}", new { Action = model.ActionTaken_MD, ApprovalDate = model.ApprovalDate_MD, Opinion = model.Opinion_MD });

            TempData["SuccessMessage"] = "Managing Director action recorded successfully.";
            return RedirectToAction("DetailsArisingApplication", new { id = model.ArisingID });
        }

        [HttpGet]
        public IActionResult StayCompliance(int id)
        {
            var model = _labourRepo.GetCaseById(id);
            if (model == null) return NotFound();
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public IActionResult StayCompliance(LabourCase model)
        {
            _logger.LogInformation("Processing StayCompliance update for Labour Case ID: {CaseID}", model.CaseID);

            int divisionId = HttpContext.Session.GetInt32(SessionKeys.DivisionID) ?? 0;
            if (divisionId != 0 && divisionId != 5)
            {
                TempData["ErrorMessage"] = "Access Denied: Only Central Office can perform this action.";
                return RedirectToAction("Details", new { id = model.CaseID });
            }

            var existing = _labourRepo.GetCaseById(model.CaseID);
            if (existing == null)
            {
                _logger.LogWarning("Labour Case not found for StayCompliance: ID {CaseID}", model.CaseID);
                return NotFound();
            }

            if (model.StayComplianceFile != null)
            {
                existing.CO_StayComplianceFilePath = SaveFile(model.StayComplianceFile, "Labour/Compliance");
            }
            if (model.ClosedDocumentFile != null)
            {
                existing.CO_ClosedDocumentPath = SaveFile(model.ClosedDocumentFile, "Labour/Closed");
            }
            existing.CO_StayComplianceRemark = model.CO_StayComplianceRemark;
            existing.ModifiedBy = HttpContext.Session.GetInt32(SessionKeys.UserID);

            _labourRepo.UpdateCase(existing);
            _ = _activityLogger.LogSubEntityActionAsync("LABOUR", model.CaseID, $"Labour {existing.CaseNumber}/{existing.CaseYear}", "STAY_COMPLIANCE_UPDATED", "Stay compliance details and orders updated", new { Remark = model.CO_StayComplianceRemark });
            TempData["SuccessMessage"] = "Stay compliance details updated successfully.";
            return RedirectToAction("CaseList", new { status = "InterimStayCompliance" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public IActionResult Action(LabourCase model)
        {
            _logger.LogInformation("Processing Action for Labour Case ID: {CaseID}", model.CaseID);
            
            var divClaim = User.FindFirst("DivisionID")?.Value;
            int divisionId = int.TryParse(divClaim, out int parsedDiv) ? parsedDiv : (HttpContext.Session.GetInt32(SessionKeys.DivisionID) ?? 0);
            bool isCO = (divisionId == 0 || divisionId == 5 || User.IsInRole("CentralOffice") || User.IsInRole("CO") || User.IsInRole("CLO") || User.IsInRole("MD") || User.IsInRole("Admin"));
            if (!isCO)
            {
                _logger.LogWarning("Access Denied: User in Division {DivisionID} attempted Central Office action.", divisionId);
                TempData["ErrorMessage"] = "Access Denied: Only Central Office can perform this action.";
                return RedirectToAction("Details", new { id = model.CaseID });
            }

            // Proceed regardless of Validation state as per requirement "DO NOT KEEP ANY FIELD COMPLUSORY"
            // if (ModelState.IsValid)
            {
                int userId = HttpContext.Session.GetInt32(SessionKeys.UserID) ?? 0;
                
                // Fetch existing to preserve other fields
                var dbCase = _labourRepo.GetCaseById(model.CaseID);
                if (dbCase == null) return NotFound();

                // Merge ACTION fields from model -> dbCase
                dbCase.ModifiedBy = userId;
                
                // 1. Feasibility
                dbCase.CO_FeasibilityReceived = model.CO_FeasibilityReceived;
                dbCase.CO_FeasibilityDate = model.CO_FeasibilityDate;
                dbCase.CO_ActionTaken = model.CO_ActionTaken;
                dbCase.CO_ApprovalOutwardNo = model.CO_ApprovalOutwardNo;
                dbCase.CO_ApprovalDate = model.CO_ApprovalDate;
                // CO_ClosedDocumentPath is handled in file upload section below

                // 2. WP (Corp)
                dbCase.CO_WP_CNRNumber = model.CO_WP_CNRNumber;
                dbCase.CO_WP_CaseStatus_Option = model.CO_WP_CaseStatus_Option;
                dbCase.CO_WP_CaseNumber = model.CO_WP_CaseNumber;
                dbCase.CO_WP_Year = model.CO_WP_Year;
                dbCase.CO_WP_HighCourtBench = model.CO_WP_HighCourtBench;
                dbCase.CO_WP_EntrustmentNo = model.CO_WP_EntrustmentNo;
                dbCase.CO_WP_EntrustmentDate = model.CO_WP_EntrustmentDate;
                dbCase.CO_WP_AdvocateName = model.CO_WP_AdvocateName;
                dbCase.CO_WP_StayGranted = model.CO_WP_StayGranted;
                dbCase.CO_WP_StayApprovalNo = model.CO_WP_StayApprovalNo;
                dbCase.CO_WP_StayNature = model.CO_WP_StayNature;
                dbCase.CO_WP_StayDate = model.CO_WP_StayDate;
                dbCase.CO_WP_StayRemark = model.CO_WP_StayRemark;
                dbCase.CO_WP_Status = model.CO_WP_Status;
                dbCase.CO_WP_ActionTaken = model.CO_WP_ActionTaken;
                dbCase.CO_WP_Outcome = model.CO_WP_Outcome;
                dbCase.CO_WP_OutcomeRemark = model.CO_WP_OutcomeRemark;
                dbCase.CO_WP_OutcomeOutwardNo = model.CO_WP_OutcomeOutwardNo;
                dbCase.CO_WP_OutcomeOutwardDate = model.CO_WP_OutcomeOutwardDate;

                // 2a. WA (Writ Appeal)
                dbCase.CO_WA_CNRNumber = model.CO_WA_CNRNumber;
                dbCase.CO_WA_CaseStatus_Option = model.CO_WA_CaseStatus_Option;
                dbCase.CO_WA_CaseNumber = model.CO_WA_CaseNumber;
                dbCase.CO_WA_Year = model.CO_WA_Year;
                dbCase.CO_WA_HighCourtBench = model.CO_WA_HighCourtBench;
                dbCase.CO_WA_EntrustmentNo = model.CO_WA_EntrustmentNo;
                dbCase.CO_WA_EntrustmentDate = model.CO_WA_EntrustmentDate;
                dbCase.CO_WA_AdvocateName = model.CO_WA_AdvocateName;
                dbCase.CO_WA_StayGranted = model.CO_WA_StayGranted;
                dbCase.CO_WA_StayApprovalNo = model.CO_WA_StayApprovalNo;
                dbCase.CO_WA_StayNature = model.CO_WA_StayNature;
                dbCase.CO_WA_StayDate = model.CO_WA_StayDate;
                dbCase.CO_WA_StayRemark = model.CO_WA_StayRemark;
                dbCase.CO_WA_Status = model.CO_WA_Status;
                dbCase.CO_WA_ActionTaken = model.CO_WA_ActionTaken;
                dbCase.CO_WA_Outcome = model.CO_WA_Outcome;
                dbCase.CO_WA_OutcomeRemark = model.CO_WA_OutcomeRemark;
                dbCase.CO_WA_OutcomeOutwardNo = model.CO_WA_OutcomeOutwardNo;
                dbCase.CO_WA_OutcomeOutwardDate = model.CO_WA_OutcomeOutwardDate;

                // 3. Reinstatement
                dbCase.CO_IsWorkmanReinstated = model.CO_IsWorkmanReinstated;
                dbCase.CO_ReinstatedSubjectToWP = model.CO_ReinstatedSubjectToWP;
                dbCase.CO_Reinstatement_StayGranted = model.CO_Reinstatement_StayGranted;
                dbCase.CO_Reinstatement_StayApprovalNo = model.CO_Reinstatement_StayApprovalNo;
                dbCase.CO_Reinstatement_StayNature = model.CO_Reinstatement_StayNature;
                dbCase.CO_Reinstatement_StayDate = model.CO_Reinstatement_StayDate;
                dbCase.CO_Reinstatement_StayRemark = model.CO_Reinstatement_StayRemark;
                dbCase.CO_ReinstatementApprovalIssued = model.CO_ReinstatementApprovalIssued;
                dbCase.CO_ReinstatementApprovalDate = model.CO_ReinstatementApprovalDate;
                dbCase.CO_Reinstatement_ApprovalNo = model.CO_Reinstatement_ApprovalNo;

                // 5. Connected Cases
                dbCase.ConnectedCases = model.ConnectedCases; // Action controls this
                dbCase.CO_OverallCaseStatus = model.CO_OverallCaseStatus;

                // 6. Disposal Outcome (CO)
                dbCase.DisposalResult = model.DisposalResult;
                dbCase.CO_Disposal_Nature = model.CO_Disposal_Nature;
                dbCase.CO_Disposal_CommSentToDivision = model.CO_Disposal_CommSentToDivision;
                dbCase.CO_Disposal_OutwardNo = model.CO_Disposal_OutwardNo;
                dbCase.CO_Disposal_Date = model.CO_Disposal_Date;
                dbCase.CO_Disposal_Decision = model.CO_Disposal_Decision;
                dbCase.CO_Disposal_ApprovalOutwardNo = model.CO_Disposal_ApprovalOutwardNo;
                dbCase.CO_Disposal_ApprovalDate = model.CO_Disposal_ApprovalDate;

                // 7. Further Appeal
                dbCase.CO_FurtherAppeal_Status_Option = model.CO_FurtherAppeal_Status_Option;
                dbCase.CO_FurtherAppeal_CaseNumber = model.CO_FurtherAppeal_CaseNumber;
                dbCase.CO_FurtherAppeal_Year = model.CO_FurtherAppeal_Year;
                dbCase.CO_FurtherAppeal_EntrustmentNo = model.CO_FurtherAppeal_EntrustmentNo;
                dbCase.CO_FurtherAppeal_EntrustmentDate = model.CO_FurtherAppeal_EntrustmentDate;
                dbCase.CO_FurtherAppeal_AdvocateName = model.CO_FurtherAppeal_AdvocateName;
                dbCase.CO_FurtherAppeal_CaseStatus = model.CO_FurtherAppeal_CaseStatus;
                dbCase.CO_FurtherAppeal_DisposalOutwardNo = model.CO_FurtherAppeal_DisposalOutwardNo;
                dbCase.CO_FurtherAppeal_DisposalDate = model.CO_FurtherAppeal_DisposalDate;

                // 8. Claimant Appeal
                dbCase.CO_Claimant_CNRNumber = model.CO_Claimant_CNRNumber;
                dbCase.CO_Claimant_DivisionName = model.CO_Claimant_DivisionName;
                dbCase.CO_Claimant_ArisingOutOf = model.CO_Claimant_ArisingOutOf;
                dbCase.CO_Claimant_Court = model.CO_Claimant_Court;
                dbCase.CO_Claimant_CaseNumber = model.CO_Claimant_CaseNumber;
                dbCase.CO_Claimant_CaseYear = model.CO_Claimant_CaseYear;
                dbCase.CO_Claimant_HighCourtBench = model.CO_Claimant_HighCourtBench;
                dbCase.CO_Claimant_OriginalCaseStatus = model.CO_Claimant_OriginalCaseStatus;
                dbCase.CO_Claimant_IsConnected = model.CO_Claimant_IsConnected;
                dbCase.ClaimantConnectedCases = model.ClaimantConnectedCases;
                dbCase.CO_Claimant_EntrustmentNo = model.CO_Claimant_EntrustmentNo;
                dbCase.CO_Claimant_EntrustmentDate = model.CO_Claimant_EntrustmentDate;
                dbCase.CO_Claimant_AdvocateName = model.CO_Claimant_AdvocateName;
                dbCase.CO_Claimant_CaseStatus = model.CO_Claimant_CaseStatus;

                // 9. Service Matters
                dbCase.CO_Service_Division = model.CO_Service_Division;
                dbCase.CO_Service_WPNumber = model.CO_Service_WPNumber;
                dbCase.CO_Service_WPYear = model.CO_Service_WPYear;
                dbCase.CO_Service_PetitionerName = model.CO_Service_PetitionerName;
                dbCase.CO_Service_CaseNature = model.CO_Service_CaseNature;
                dbCase.CO_Service_Prayer = model.CO_Service_Prayer;
                dbCase.CO_Service_IsEmployee = model.CO_Service_IsEmployee;
                dbCase.CO_Service_StayGranted = model.CO_Service_StayGranted;
                dbCase.CO_Service_StayVacateFiled = model.CO_Service_StayVacateFiled;
                dbCase.CO_Service_StayCompliance = model.CO_Service_StayCompliance;
                dbCase.CO_Service_ApprovalOutwardNo = model.CO_Service_ApprovalOutwardNo;
                dbCase.CO_Service_ApprovalDate = model.CO_Service_ApprovalDate;
                dbCase.CO_Service_Status = model.CO_Service_Status;
                dbCase.CO_Service_DisposalDate = model.CO_Service_DisposalDate;
                dbCase.CO_Service_ActionTaken = model.CO_Service_ActionTaken;
                dbCase.CO_Service_ApprovalSentDetails = model.CO_Service_ApprovalSentDetails;
                dbCase.CO_Service_OutwardNo = model.CO_Service_OutwardNo;
                dbCase.CO_Service_OutwardDate = model.CO_Service_OutwardDate;
                dbCase.CO_Service_AppealFiledBefore = model.CO_Service_AppealFiledBefore;
                dbCase.CO_Service_AppealType = model.CO_Service_AppealType;
                dbCase.CO_Service_AppealEntrustmentDate = model.CO_Service_AppealEntrustmentDate;
                dbCase.CO_Service_AppealAdvocate = model.CO_Service_AppealAdvocate;
                dbCase.CO_Service_AppealStatus = model.CO_Service_AppealStatus;
                dbCase.CO_Service_EntrustmentNo = model.CO_Service_EntrustmentNo;
                dbCase.CO_Service_EntrustmentDate = model.CO_Service_EntrustmentDate;

                // 10. SLP (Supreme Court)
                dbCase.IsClaimantSCPending = model.IsClaimantSCPending;
                dbCase.ClaimantSCDiaryNumber = model.ClaimantSCDiaryNumber;
                dbCase.ClaimantSCYear = model.ClaimantSCYear;
                dbCase.ClaimantSCNumber = model.ClaimantSCNumber;
                dbCase.ClaimantSLPYear = model.ClaimantSLPYear;
                dbCase.ClaimantSCFiledBy = model.ClaimantSCFiledBy;
                dbCase.ClaimantSCEntrustmentNo = model.ClaimantSCEntrustmentNo;
                dbCase.ClaimantSCEntrustmentDate = model.ClaimantSCEntrustmentDate;
                dbCase.ClaimantSCAdvocate = model.ClaimantSCAdvocate;
                dbCase.ClaimantSCStatus = model.ClaimantSCStatus;
                dbCase.ClaimantSCOutcome = model.ClaimantSCOutcome;
                dbCase.ClaimantSCActionTaken = model.ClaimantSCActionTaken;
                dbCase.ClaimantSCClosureNo = model.ClaimantSCClosureNo;
                dbCase.ClaimantSCClosureDate = model.ClaimantSCClosureDate;

                dbCase.CO_StayComplianceRemark = model.CO_StayComplianceRemark;
                dbCase.CO_StayComplianceDate = model.CO_StayComplianceDate;
                dbCase.Opinion_CLO = model.Opinion_CLO;

                // Handle File Upload - Update path in dbCase if file provided
                if (model.HistorySheetFile != null)
                {
                    dbCase.DE_HistorySheetPath = SaveFile(model.HistorySheetFile, "history_sheets");
                }
                if (model.JudgmentCopyFile != null)
                {
                    dbCase.JudgmentCopyPath = SaveFile(model.JudgmentCopyFile, "judgments");
                }
                if (model.JudgmentCopyFile2 != null)
                {
                    dbCase.JudgmentCopyPath2 = SaveFile(model.JudgmentCopyFile2, "judgments");
                }
                if (model.CO_Service_PetitionCopyFile != null)
                {
                    dbCase.CO_Service_PetitionCopyPath = SaveFile(model.CO_Service_PetitionCopyFile, "petitions");
                }
                if (model.StayOrderFile != null)
                {
                    dbCase.CO_WP_StayOrderPath = SaveFile(model.StayOrderFile, "Labour/Stays");
                }
                if (model.WAStayOrderFile != null)
                {
                    dbCase.CO_WA_StayOrderPath = SaveFile(model.WAStayOrderFile, "Labour/WAStays");
                }
                if (model.CO_WP_JudgmentCopyFile != null)
                {
                    dbCase.CO_WP_JudgmentCopyPath = SaveFile(model.CO_WP_JudgmentCopyFile, "Labour/WPJudgments");
                }
                 if (model.ReinstatementStayOrderFile != null)
                {
                    dbCase.CO_Reinstatement_StayOrderPath = SaveFile(model.ReinstatementStayOrderFile, "Labour/ReinstatementStays");
                }
                if (model.ReinstatementApprovalFile != null)
                {
                    dbCase.CO_Reinstatement_ApprovalCopyPath = SaveFile(model.ReinstatementApprovalFile, "Labour/ReinstatementApproval");
                }
                if (model.StayComplianceFile != null)
                {
                    dbCase.CO_StayComplianceFilePath = SaveFile(model.StayComplianceFile, "Labour/StayCompliance");
                }
                if (model.ClosedDocumentFile != null)
                {
                    dbCase.CO_ClosedDocumentPath = SaveFile(model.ClosedDocumentFile, "Labour/Closed");
                }
                
                // Ensure DivisionID is preserved or fixed
                if (dbCase.DivisionID == 0)
                {
                     // Attempt repair for bad data
                     int sessionDiv = HttpContext.Session.GetInt32(SessionKeys.DivisionID) ?? 0;
                     if (sessionDiv != 0 && sessionDiv != 5) dbCase.DivisionID = sessionDiv;
                }

                if (dbCase.CO_IsWorkmanReinstated == true && 
                    dbCase.CO_ReinstatementApprovalDate != null && 
                    !string.IsNullOrEmpty(dbCase.CO_Reinstatement_ApprovalNo) && 
                    !string.IsNullOrEmpty(dbCase.CO_Reinstatement_ApprovalCopyPath))
                {
                    dbCase.IsReinstatementViewed = false;
                }

                try
                {
                    _labourRepo.UpdateCase(dbCase);
                    _ = _activityLogger.LogCaseUpdatedAsync("LABOUR", dbCase.CaseID, $"Labour {dbCase.CaseNumber}/{dbCase.CaseYear}", null, dbCase.CourtName, null, dbCase, $"Central Office Action updated on Labour Case #{dbCase.CaseNumber}/{dbCase.CaseYear}");
                    TempData["SuccessMessage"] = "Labour case action updated successfully!";
                    return RedirectToAction("Details", new { id = dbCase.CaseID });
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", "Database error: " + ex.Message);
                    TempData["ErrorMessage"] = "Failed to update action.";
                }
            }
            /*
            else
            {
                TempData["ErrorMessage"] = "Validation failed. Please check the marked fields.";
            }
            */
            PopulateDropdowns();
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Edit(LabourCase model)
        {
            // Proceed regardless of Validation state as per requirement "DO NOT KEEP ANY FIELD COMPLUSORY"
            // if (ModelState.IsValid)
            {
                  var divClaim = User.FindFirst("DivisionID")?.Value;
                  int userDiv = int.TryParse(divClaim, out int parsedDiv) ? parsedDiv : (HttpContext.Session.GetInt32(SessionKeys.DivisionID) ?? 0);
                  bool isCO = (userDiv == 0 || userDiv == 5 || User.IsInRole("CentralOffice") || User.IsInRole("CO") || User.IsInRole("CLO") || User.IsInRole("MD") || User.IsInRole("Admin"));

                  var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                  int userId = int.TryParse(userIdClaim, out int parsedUid) ? parsedUid : (HttpContext.Session.GetInt32(SessionKeys.UserID) ?? 0);
                  
                  // Fetch existing to preserve Action fields / Connected cases
                  var dbCase = _labourRepo.GetCaseById(model.CaseID);
                  if (dbCase == null) return NotFound();
                  var oldCase = _labourRepo.GetCaseById(model.CaseID);

                  // IDOR check: Division users can only edit their own division's cases
                  if (!isCO && dbCase.DivisionID != userDiv)
                  {
                      _logger.LogWarning("IDOR attempt: User from division {UserDiv} tried to edit Labour case {CaseID} (Division {CaseDiv})",
                          userDiv, model.CaseID, dbCase.DivisionID);
                      return Forbid();
                  }

                  // Lock guard — prevent division users from editing once sent to CO
                  if (!isCO && dbCase.SentToCO == true)
                  {
                      TempData["ErrorMessage"] = "Access Denied: This case is locked as it has already been sent to the Central Office.";
                      return RedirectToAction("CaseList");
                  }

                 // Merge BASIC inputs -> dbCase
                 dbCase.ModifiedBy = userId;
                 
                 // Basic
                 dbCase.CaseStatus = model.CaseStatus;
                 dbCase.CaseType = model.CaseType;
                 dbCase.CaseNumber = model.CaseNumber;
                 dbCase.CaseYear = model.CaseYear;
                 dbCase.CourtID = model.CourtID;
                 dbCase.OtherCourtDetails = model.OtherCourtDetails;
                 dbCase.CNRNumber = model.CNRNumber;
                 dbCase.EstCode = model.EstCode;
                 dbCase.CaseTypeCode = model.CaseTypeCode;
                 
                 // Nature/Employee
                 dbCase.NatureOfCase = model.NatureOfCase;
                 dbCase.WorkingStatus = model.WorkingStatus;
                 dbCase.LegalRepresentativeName = model.LegalRepresentativeName;
                 dbCase.NatureOfMisconduct = model.NatureOfMisconduct;
                 dbCase.PetitionerName = model.PetitionerName;
                 dbCase.Designation = model.Designation;
                 dbCase.EntrustmentNo = model.EntrustmentNo;
                 dbCase.EntrustmentDate = model.EntrustmentDate;
                 dbCase.IsDoubleClaim = model.IsDoubleClaim;
                 dbCase.DoubleClaimRemarks = model.DoubleClaimRemarks;
                 dbCase.AdvocateID = model.AdvocateID;
                 dbCase.AdvocateName = model.AdvocateName;
                 dbCase.EmployeeNo = model.EmployeeNo;
                 dbCase.PFNumber = model.PFNumber;
                 dbCase.IsWorkman = model.IsWorkman;
                 dbCase.IsWorkmanRemark = model.IsWorkmanRemark;

                 if (model.CaseType != null && model.CaseType.Contains("Arising"))
                 {
                     dbCase.IsArisingApplication = true;
                 }
                 else
                 {
                     dbCase.IsArisingApplication = false;
                 }

                 // Progress
                 dbCase.CurrentStage = model.CurrentStage;
                 dbCase.SerialApp_CaseNumber = model.SerialApp_CaseNumber;
                 dbCase.SerialApp_CurrentStage = model.SerialApp_CurrentStage;
                 dbCase.CaseHistory = model.CaseHistory;
                 dbCase.NextHearingDate = model.NextHearingDate;
                 
                 dbCase.IsCOApprovalRequired = model.IsCOApprovalRequired;
                 dbCase.COApproval_OutwardNo = model.COApproval_OutwardNo;
                 dbCase.COApproval_OutwardDate = model.COApproval_OutwardDate;

                 dbCase.IsDocumentSent = model.IsDocumentSent;
                 dbCase.DocumentSent_OutwardNo = model.DocumentSent_OutwardNo;
                 dbCase.DocumentSent_OutwardDate = model.DocumentSent_OutwardDate;

                 dbCase.IsObjectionFiled = model.IsObjectionFiled;
                 dbCase.ObjectionFiled_OutwardNo = model.ObjectionFiled_OutwardNo;
                 dbCase.ObjectionFiled_OutwardDate = model.ObjectionFiled_OutwardDate;
                 
                 dbCase.IsEnquiryOfficerEvidence = model.IsEnquiryOfficerEvidence;
                 dbCase.IsReporterEvidence = model.IsReporterEvidence;
                 dbCase.IsOtherEvidence = model.IsOtherEvidence;

                 // dbCase.EvidenceList = model.EvidenceList; // Edit controls evidence - DEPRECATED

                 // Disposal
                 dbCase.DisposalMode = model.DisposalMode;
                 dbCase.DisposalDate = model.DisposalDate;
                 dbCase.DisposalResult = model.DisposalResult;
                 dbCase.FavorRemark = model.FavorRemark;
                  dbCase.FavorOutwardDate = model.FavorOutwardDate;
                 
                 // Lok Adalat & Against Logic
                 dbCase.LokAdalat_COApprovalRequired = model.LokAdalat_COApprovalRequired;
                 dbCase.LokAdalat_OutwardNo = model.LokAdalat_OutwardNo;
                 dbCase.LokAdalat_Date = model.LokAdalat_Date;

                 dbCase.Against_CaseCategory = model.Against_CaseCategory;
                 dbCase.Against_BriefFacts = model.Against_BriefFacts;
                 dbCase.Against_PunishmentImposed = model.Against_PunishmentImposed;
                 dbCase.Against_PunishmentNo = model.Against_PunishmentNo;
                 dbCase.Against_PunishmentDate = model.Against_PunishmentDate;
                 dbCase.Against_PunishmentCopyPath = model.Against_PunishmentCopyPath;
                  dbCase.ClaimPetitionPath = model.ClaimPetitionPath;

                 if (model.Against_PunishmentCopyFile != null)
                 {
                     dbCase.Against_PunishmentCopyPath = SaveFile(model.Against_PunishmentCopyFile, "punishments");
                 }

                 // DE
                 dbCase.DE_HistorySheet = model.DE_HistorySheet;
                 dbCase.DE_ObjectionsFiled = model.DE_ObjectionsFiled;
                 dbCase.DE_ObjectionsRemark = model.DE_ObjectionsRemark;
                 dbCase.DE_DocumentsMarked = model.DE_DocumentsMarked;
                 dbCase.DE_DocumentsRemark = model.DE_DocumentsRemark;
                 dbCase.DE_Order = model.DE_Order;
                 dbCase.ChargesStatus = model.ChargesStatus;
                 dbCase.TerminalBenefitsPaid = model.TerminalBenefitsPaid;
                 dbCase.SerialApplicationDetails = model.SerialApplicationDetails;
                 dbCase.IsRepeatDismissal = model.IsRepeatDismissal;
                 dbCase.RepeatDismissalRemark = model.RepeatDismissalRemark;
                 dbCase.HasAppealDetails = model.HasAppealDetails;
                 dbCase.AppealDetailsRemark = model.AppealDetailsRemark;
                  dbCase.IsFiledWithinLimitation = model.IsFiledWithinLimitation;
                  dbCase.LimitationRemark = model.LimitationRemark;
                  dbCase.IsDelayCondoned = model.IsDelayCondoned;
                  dbCase.DelayCondonationRemark = model.DelayCondonationRemark;
                 dbCase.DE_EO_Name = model.DE_EO_Name;
                 dbCase.DE_EO_Designation = model.DE_EO_Designation;
                  dbCase.DE_EO_IsBasedOnDocuments = model.DE_EO_IsBasedOnDocuments;
                  dbCase.DE_EO_BasedOnDocsRemark = model.DE_EO_BasedOnDocsRemark;
                 dbCase.DE_Reporter_Name = model.DE_Reporter_Name;
                 dbCase.DE_Reporter_Designation = model.DE_Reporter_Designation;
                 dbCase.DE_Reporter_IsBasedOnDocuments = model.DE_Reporter_IsBasedOnDocuments;
                 dbCase.DE_Reporter_BasedOnDocsRemark = model.DE_Reporter_BasedOnDocsRemark;
                 dbCase.DE_Other_Name = model.DE_Other_Name;
                 dbCase.DE_Other_Designation = model.DE_Other_Designation;
                 dbCase.DE_Other_IsBasedOnDocuments = model.DE_Other_IsBasedOnDocuments;
                 dbCase.DE_Other_BasedOnDocsRemark = model.DE_Other_BasedOnDocsRemark;

                 dbCase.ClaimFiledOn = model.ClaimFiledOn;
                 dbCase.DelayInFiling = model.DelayInFiling;
                 dbCase.ClaimDetails = model.ClaimDetails;

                 // CC & Opinions
                 dbCase.CC_CaseDisposedDate = model.CC_CaseDisposedDate;
                 dbCase.CC_PublicationDate = model.CC_PublicationDate;
                 dbCase.CC_AppliedDate = model.CC_AppliedDate;
                 dbCase.CC_IssuedDate = model.CC_IssuedDate;
                 dbCase.CC_DeliveredDate = model.CC_DeliveredDate;
                 dbCase.CC_ReceivedDate = model.CC_ReceivedDate;
                 dbCase.CC_Remarks = model.CC_Remarks;

                 dbCase.AwardDetails = model.AwardDetails;
                 dbCase.Opinion_Advocate = model.Opinion_Advocate;
                 dbCase.Opinion_LO = model.Opinion_LO;
                 dbCase.Opinion_DC = model.Opinion_DC;
                  dbCase.Opinion_CLO = model.Opinion_CLO;
                  dbCase.FavorOutwardDate = model.FavorOutwardDate;

                 dbCase.SentToCO = model.SentToCO;
                 dbCase.CO_OutwardNo = model.CO_OutwardNo;
                 dbCase.CO_OutwardDate = model.CO_OutwardDate;
                 dbCase.CO_Remarks = model.CO_Remarks;
                 
                 // Arising
                 dbCase.Arising_OriginalCaseNumber = model.Arising_OriginalCaseNumber;
                 dbCase.Arising_OriginalCaseYear = model.Arising_OriginalCaseYear;
                 dbCase.Arising_OriginalCourt = model.Arising_OriginalCourt;
                 dbCase.Arising_CurrentStatus = model.Arising_CurrentStatus;
                 dbCase.Arising_ApplicationStatus = model.Arising_ApplicationStatus;


                 // Handle File Upload
                 if (model.HistorySheetFile != null)
                 {
                     dbCase.DE_HistorySheetPath = SaveFile(model.HistorySheetFile, "history_sheets");
                 }
                 if (model.JudgmentCopyFile != null)
                 {
                     dbCase.JudgmentCopyPath = SaveFile(model.JudgmentCopyFile, "judgments");
                 }
                 if (model.JudgmentCopyFile2 != null)
                 {
                     dbCase.JudgmentCopyPath2 = SaveFile(model.JudgmentCopyFile2, "judgments");
                 }
                 if (model.LokAdalatDocumentFile != null)
                 {
                     dbCase.LokAdalatDocumentPath = SaveFile(model.LokAdalatDocumentFile, "LokAdalat");
                  }
                  if (model.ClaimPetitionFile != null)
                  {
                      dbCase.ClaimPetitionPath = SaveFile(model.ClaimPetitionFile, "claims");
                  }

                  if (model.StayOrderFile != null)
                  {
                      dbCase.CO_WP_StayOrderPath = SaveFile(model.StayOrderFile, "stay_orders");
                  }
                  dbCase.CO_WP_StayRemark = model.CO_WP_StayRemark;
                  if (model.CO_WP_JudgmentCopyFile != null)
                  {
                      dbCase.CO_WP_JudgmentCopyPath = SaveFile(model.CO_WP_JudgmentCopyFile, "Labour/WPJudgments");
                  }

                  // Preserve all Central Office Action and Higher Court appeal tracking fields
                  // (These are managed through dedicated Central Office workflow pages and should not be wiped by base case edits)
                  if (model.IsViewedByCO) dbCase.IsViewedByCO = true;

                  // Handle Additional CO/Action File Uploads (Preserve/Update)
                  if (model.WAStayOrderFile != null) dbCase.CO_WA_StayOrderPath = SaveFile(model.WAStayOrderFile, "Labour/WAStay");
                  if (model.ReinstatementStayOrderFile != null) dbCase.CO_Reinstatement_StayOrderPath = SaveFile(model.ReinstatementStayOrderFile, "Labour/ReinstatementStay");
                  if (model.ReinstatementApprovalFile != null) dbCase.CO_Reinstatement_ApprovalCopyPath = SaveFile(model.ReinstatementApprovalFile, "Labour/ReinstatementApproval");
                  if (model.CO_Claimant_PetitionCopyFile != null) dbCase.CO_Claimant_PetitionCopyPath = SaveFile(model.CO_Claimant_PetitionCopyFile, "Labour/ClaimantPetition");
                  if (model.CO_Service_PetitionCopyFile != null) dbCase.CO_Service_PetitionCopyPath = SaveFile(model.CO_Service_PetitionCopyFile, "Labour/ServicePetition");
                  if (model.CO_Service_ApprovalCopyFile != null) dbCase.CO_Service_ApprovalCopyPath = SaveFile(model.CO_Service_ApprovalCopyFile, "Labour/ServiceApproval");
                  if (model.StayComplianceFile != null) dbCase.CO_StayComplianceFilePath = SaveFile(model.StayComplianceFile, "Labour/StayCompliance");
                  if (model.ClosedDocumentFile != null) dbCase.CO_ClosedDocumentPath = SaveFile(model.ClosedDocumentFile, "Labour/Closed");
                  
                 // DivisionID Logic
                 if (model.DivisionID != 0)
                 {
                     dbCase.DivisionID = model.DivisionID;
                 }
                 else if (dbCase.DivisionID == 0)
                 {
                      int sessionDiv = HttpContext.Session.GetInt32(SessionKeys.DivisionID) ?? 0;
                      if (sessionDiv != 0 && sessionDiv != 5) dbCase.DivisionID = sessionDiv;
                 }
                 
                try
                {
                    _labourRepo.UpdateCase(dbCase);
                    _ = _activityLogger.LogCaseUpdatedAsync("Labour", dbCase.CaseID, dbCase.CaseNumber ?? $"Labour-{dbCase.CaseID}", null, dbCase.OtherCourtDetails, oldCase, dbCase, $"Labour Case {dbCase.CaseNumber} updated");
                    TempData["SuccessMessage"] = "Labour case updated successfully!";
                    return RedirectToAction("Index");
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", "Database error: " + ex.Message);
                    TempData["ErrorMessage"] = "Failed to update case.";
                }
            }
            /*
            else
            {
                TempData["ErrorMessage"] = "Validation failed. Please check the marked fields.";
            }
            */
            PopulateDropdowns();
            return View(model);
        }

        // Allowed document upload extensions (whitelist)
        private static readonly HashSet<string> _allowedExtensions =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { ".pdf", ".doc", ".docx", ".jpg", ".jpeg", ".png", ".tiff", ".bmp" };

        private string SaveFile(IFormFile file, string subFolder, string? existingFilePath = null)
        {
            if (file == null || file.Length == 0) return existingFilePath ?? string.Empty;

            // Security: validate extension against whitelist
            string ext = Path.GetExtension(file.FileName);
            if (!_allowedExtensions.Contains(ext))
            {
                throw new InvalidOperationException(
                    $"File type '{ext}' is not allowed. Only PDF, Word, and image files are permitted.");
            }

            // Security: limit individual file size to 20 MB
            const long maxBytes = 20 * 1024 * 1024;
            if (file.Length > maxBytes)
                throw new InvalidOperationException("File size exceeds the 20 MB limit.");

            if (!string.IsNullOrEmpty(existingFilePath))
            {
                string oldFullPath = Path.Combine(_env.WebRootPath, "uploads", existingFilePath.TrimStart('/').Replace("uploads/", "").Replace("uploads\\\\", ""));
                if (System.IO.File.Exists(oldFullPath))
                {
                    try { System.IO.File.Delete(oldFullPath); } catch { /* non-critical */ }
                }
            }

            string uploadsFolder = Path.Combine(_env.WebRootPath, "uploads", subFolder);
            if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

            // Use only GUID + extension (no original filename) to prevent path traversal
            string uniqueFileName = Guid.NewGuid().ToString() + ext;
            string filePath = Path.Combine(uploadsFolder, uniqueFileName);

            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                file.CopyTo(fileStream);
            }
            return "/uploads/" + subFolder + "/" + uniqueFileName;
        }

        private void PopulateDropdowns()
        {
            ViewBag.Courts = _labourRepo.GetAllCourts();
            ViewBag.Advocates = _labourRepo.GetAllAdvocates();
            ViewBag.Divisions = _masterRepo.GetAllDivisions();
        }

        [HttpGet]
        [Route("/Labour/SearchArisingCase")]
        public IActionResult SearchArisingCase(string caseNo, int year, string? court = null, string? caseType = null)
        {
            try
            {
                if (string.IsNullOrEmpty(caseNo)) return Json(new { success = false, message = "Case Number is required." });

                var c = _labourRepo.GetCaseByNumber(caseNo, year, court, caseType);
                if (c == null) return Json(new { success = false, message = "No matching Labour Case found in the archive." });

                var connections = _labourRepo.GetConnectedCasesByCaseId(c.CaseID).Select(cc => new {
                    cc.CaseDetails,
                    cc.CaseType,
                    cc.CurrentStatus
                }).ToList();

                string cleanNo = (caseNo ?? "").Replace("WP", "", StringComparison.OrdinalIgnoreCase)
                                               .Replace("WA", "", StringComparison.OrdinalIgnoreCase)
                                               .Replace(" ", "")
                                               .Replace(".", "")
                                               .Replace("/", "")
                                               .Trim();

                string retCaseNo = c.CaseNumber ?? "";
                int retCaseYear = c.CaseYear.GetValueOrDefault();
                string retCourt = c.CourtName ?? "N/A";
                string retCaseType = c.CaseType ?? "N/A";

                if (!string.IsNullOrEmpty(c.CO_WP_CaseNumber) && (string.IsNullOrEmpty(cleanNo) || c.CO_WP_CaseNumber.Replace(" ", "").Contains(cleanNo)))
                {
                    retCaseNo = c.CO_WP_CaseNumber;
                    retCaseYear = c.CO_WP_Year.HasValue && c.CO_WP_Year.Value > 0 ? c.CO_WP_Year.Value : c.CaseYear.GetValueOrDefault();
                    retCourt = !string.IsNullOrEmpty(c.CO_WP_HighCourtBench) ? c.CO_WP_HighCourtBench : (c.CourtName ?? "High Court");
                    retCaseType = "Writ Petition (WP)";
                }
                else if (!string.IsNullOrEmpty(c.CO_Claimant_CaseNumber) && (string.IsNullOrEmpty(cleanNo) || c.CO_Claimant_CaseNumber.Replace(" ", "").Contains(cleanNo)))
                {
                    retCaseNo = c.CO_Claimant_CaseNumber;
                    retCaseYear = c.CO_Claimant_CaseYear.HasValue && c.CO_Claimant_CaseYear.Value > 0 ? c.CO_Claimant_CaseYear.Value : c.CaseYear.GetValueOrDefault();
                    retCourt = !string.IsNullOrEmpty(c.CO_Claimant_HighCourtBench) ? c.CO_Claimant_HighCourtBench : (!string.IsNullOrEmpty(c.CO_Claimant_Court) ? c.CO_Claimant_Court : (c.CourtName ?? "High Court"));
                    retCaseType = !string.IsNullOrEmpty(c.CaseType) ? c.CaseType : "Claimant Appeal/WP";
                }
                else if (!string.IsNullOrEmpty(c.Arising_OriginalCaseNumber) && (string.IsNullOrEmpty(cleanNo) || c.Arising_OriginalCaseNumber.Replace(" ", "").Contains(cleanNo)))
                {
                    retCaseNo = c.Arising_OriginalCaseNumber;
                    retCaseYear = c.Arising_OriginalCaseYear.HasValue && c.Arising_OriginalCaseYear.Value > 0 ? c.Arising_OriginalCaseYear.Value : c.CaseYear.GetValueOrDefault();
                    retCourt = !string.IsNullOrEmpty(c.Arising_OriginalCourt) ? c.Arising_OriginalCourt : (c.CourtName ?? "N/A");
                }

                return Json(new
                {
                    success = true,
                    caseId = c.CaseID,
                    caseNumber = retCaseNo,
                    caseYear = retCaseYear,
                    caseType = retCaseType,
                    caseStatus = c.CaseStatus ?? "N/A",
                    courtName = retCourt,
                    divisionId = c.DivisionID,
                    divisionName = c.DivisionName ?? "N/A",
                    petitionerName = c.PetitionerName ?? "N/A",
                    employeeNo = c.EmployeeNo ?? "N/A",
                    designation = c.Designation ?? "N/A",
                    workingStatus = c.WorkingStatus ?? "N/A",
                    natureOfCase = c.NatureOfCase ?? "N/A",
                    natureOfMisconduct = c.NatureOfMisconduct ?? "N/A",
                    advocateName = c.AdvocateName ?? "N/A",
                    currentStage = c.CurrentStage ?? "N/A",
                    nextHearingDate = c.NextHearingDate?.ToString("dd-MMM-yyyy") ?? "N/A",
                    disposalMode = c.DisposalMode ?? "N/A",
                    disposalDate = c.DisposalDate?.ToString("dd-MMM-yyyy") ?? "N/A",
                    disposalResult = c.DisposalResult ?? "N/A",
                    awardDetails = c.AwardDetails ?? "N/A",
                    entrustmentNo = c.EntrustmentNo ?? "N/A",
                    entrustmentDate = c.EntrustmentDate?.ToString("dd-MMM-yyyy") ?? "N/A",
                    
                    isFiledWithinLimitation = c.IsFiledWithinLimitation?.ToString() ?? "N/A",
                    limitationRemark = c.LimitationRemark ?? "N/A",
                    isDelayCondoned = c.IsDelayCondoned?.ToString() ?? "N/A",
                    delayCondonationRemark = c.DelayCondonationRemark ?? "N/A",

                    isDocumentSent = c.IsDocumentSent ? "Yes" : "No",
                    documentSent_OutwardNo = c.DocumentSent_OutwardNo ?? "N/A",
                    documentSent_OutwardDate = c.DocumentSent_OutwardDate?.ToString("dd-MMM-yyyy") ?? "N/A",
                    isObjectionFiled = c.IsObjectionFiled ? "Yes" : "No",
                    objectionFiled_OutwardNo = c.ObjectionFiled_OutwardNo ?? "N/A",
                    objectionFiled_OutwardDate = c.ObjectionFiled_OutwardDate?.ToString("dd-MMM-yyyy") ?? "N/A",
                    evidenceTypes = (c.IsEnquiryOfficerEvidence ? "Enquiry Officer, " : "") + 
                                    (c.IsReporterEvidence ? "Reporter, " : "") + 
                                    (c.IsOtherEvidence ? "Other" : ""),

                    cc_CaseDisposedDate = c.CC_CaseDisposedDate?.ToString("dd-MMM-yyyy") ?? "N/A",
                    cc_PublicationDate = c.CC_PublicationDate?.ToString("dd-MMM-yyyy") ?? "N/A",
                    cc_AppliedDate = c.CC_AppliedDate?.ToString("dd-MMM-yyyy") ?? "N/A",
                    cc_IssuedDate = c.CC_IssuedDate?.ToString("dd-MMM-yyyy") ?? "N/A",
                    cc_ReceivedDate = c.CC_ReceivedDate?.ToString("dd-MMM-yyyy") ?? "N/A",
                    cc_Remarks = c.CC_Remarks ?? "N/A",

                    opinion_Advocate = c.Opinion_Advocate ?? "N/A",
                    opinion_LO = c.Opinion_LO ?? "N/A",
                    opinion_DC = c.Opinion_DC ?? "N/A",
                    opinion_CLO = c.Opinion_CLO ?? "N/A",

                    against_CaseCategory = c.Against_CaseCategory ?? "N/A",
                    against_BriefFacts = c.Against_BriefFacts ?? "N/A",
                    against_PunishmentImposed = c.Against_PunishmentImposed ?? "N/A",
                    against_PunishmentNo = c.Against_PunishmentNo ?? "N/A",
                    against_PunishmentDate = c.Against_PunishmentDate?.ToString("dd-MMM-yyyy") ?? "N/A",

                    sentToCO = c.SentToCO == true ? "Yes" : "No",
                    co_OutwardNo = c.CO_OutwardNo ?? "N/A",
                    co_OutwardDate = c.CO_OutwardDate?.ToString("dd-MMM-yyyy") ?? "N/A",
                    co_Remarks = c.CO_Remarks ?? "N/A",

                    otherCourtDetails = c.OtherCourtDetails ?? "",
                    isDoubleClaim = c.IsDoubleClaim ? "Yes" : "No",
                    doubleClaimRemarks = c.DoubleClaimRemarks ?? "",
                    lokAdalat_COApprovalRequired = c.LokAdalat_COApprovalRequired ? "Yes" : "No",
                    lokAdalat_OutwardNo = c.LokAdalat_OutwardNo ?? "N/A",
                    lokAdalat_Date = c.LokAdalat_Date?.ToString("dd-MMM-yyyy") ?? "N/A",
                    
                    connectedCases = connections
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Server Error: " + ex.Message });
            }
        }

        [HttpGet]
        [Route("/Transfer")]
        [Route("/Labour/Transfer")]
        public IActionResult Transfer()
        {
            var divIdString = User.FindFirstValue("DivisionID");
            int divisionId = int.TryParse(divIdString, out int id) ? id : 0;
            var divName = User.FindFirstValue("DivisionName");

            ViewBag.Divisions = _masterRepo.GetAllDivisions();

            var model = new TransferViewModel
            {
                FromDivisionID = divisionId,
                FromDivisionName = divName
            };

            return View(model);
        }

        [HttpGet]
        [Route("/Labour/SearchTransferCase")]
        [Route("/Transfer/SearchTransferCase")]
        public IActionResult SearchTransferCase(string mvcNo, int year)
        {
            if (string.IsNullOrEmpty(mvcNo)) return Json(new { success = false, message = "Case Number is required" });

            var caseModel = _labourRepo.GetCaseByNumber(mvcNo, year);
            if (caseModel == null) return Json(new { success = false, message = "Labour Case not found" });

            return Json(new {
                success = true,
                caseId = caseModel.CaseID,
                petitioner = caseModel.PetitionerName ?? "N/A",
                employeeNo = caseModel.EmployeeNo ?? "N/A",
                courtName = caseModel.CourtName ?? "N/A",
                workingStatus = caseModel.WorkingStatus ?? "N/A",
                currentDivision = caseModel.DivisionName,
                currentDivisionId = caseModel.DivisionID
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Route("/Labour/ProcessTransfer")]
        [Route("/Transfer/ProcessTransfer")]
        public IActionResult ProcessTransfer(TransferViewModel model)
        {
            if (model.CaseID == 0)
            {
                TempData["ErrorMessage"] = "Please search and select a valid case first.";
                return RedirectToAction("Transfer");
            }

            if (model.FromDivisionID == model.ToDivisionID)
            {
                TempData["ErrorMessage"] = "Cannot transfer to the same division.";
                return RedirectToAction("Transfer");
            }

            var success = _labourRepo.TransferCase(model.CaseID, model.ToDivisionID, model.TransferRemarks ?? "");
            if (success)
            {
                var targetDiv = _masterRepo.GetAllDivisions().FirstOrDefault(d => d.DivisionID == model.ToDivisionID);
                var srcDiv = _masterRepo.GetAllDivisions().FirstOrDefault(d => d.DivisionID == model.FromDivisionID);
                var c = _labourRepo.GetCaseById(model.CaseID);
                _ = _activityLogger.LogCaseTransferredAsync("LABOUR", model.CaseID, $"Labour {c?.CaseNumber}/{c?.CaseYear}", model.FromDivisionID, model.ToDivisionID, srcDiv?.DivisionNameEnglish ?? "Source Division", targetDiv?.DivisionNameEnglish ?? "Target Division", model.TransferRemarks);
                TempData["SuccessMessage"] = $"Labour Case successfully transferred to {targetDiv?.DivisionNameEnglish}.";
                return RedirectToAction("Transfer");
            }
            else
            {
                TempData["ErrorMessage"] = "Error occurred during transfer. Please try again.";
                return RedirectToAction("Transfer");
            }
        }
        [HttpPost]
        [IgnoreAntiforgeryToken]
        [Route("Labour/DismissTransferNotification")]
        public IActionResult DismissTransferNotification(int caseId)
        {
            try
            {
                _labourRepo.MarkTransferAsViewed(caseId);
                return Json(new { success = true });
            }
            catch
            {
                return Json(new { success = false, message = "An error occurred. Please try again." });
            }
        }

        [HttpPost]
        [IgnoreAntiforgeryToken]
        [Route("Labour/DismissReinstatementNotification")]
        public IActionResult DismissReinstatementNotification(int caseId)
        {
            try
            {
                _labourRepo.MarkReinstatementAsViewed(caseId);
                return Json(new { success = true });
            }
            catch
            {
                return Json(new { success = false, message = "An error occurred. Please try again." });
            }
        }

        // --- SERVICE MATTERS ---

        public IActionResult ServiceMatters()
        {
            var divIdString = User.FindFirst("DivisionID")?.Value;
            int sessDivId = HttpContext.Session.GetInt32(SessionKeys.DivisionID) ?? 0;
            bool isCentralOffice = string.IsNullOrEmpty(divIdString) || divIdString == "0" || divIdString == "5" || sessDivId == 0 || sessDivId == 5;
            int divisionId = isCentralOffice ? 0 : (int.TryParse(divIdString, out int id) ? id : sessDivId);

            var cases = _labourRepo.GetServiceMatters(divisionId);
            return View(cases);
        }

        public IActionResult ManageServiceMatter(int? id)
        {
            LabourCase model;
            if (id.HasValue && id.Value > 0)
            {
                model = _labourRepo.GetServiceMatterById(id.Value) ?? new LabourCase { CaseType = "Service Matter" };
            }
            else
            {
                model = new LabourCase { CaseType = "Service Matter", DivisionID = HttpContext.Session.GetInt32(SessionKeys.DivisionID) ?? 0 };
            }
            
            ViewBag.Divisions = _masterRepo.GetAllDivisions();
            ViewBag.Advocates = _masterRepo.GetHighCourtAdvocates();
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ManageServiceMatter(LabourCase model)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(model.CO_Service_WPNumber))
                {
                    ViewBag.Error = "WP Number is required.";
                    ViewBag.Divisions = _masterRepo.GetAllDivisions();
                    ViewBag.Advocates = _masterRepo.GetHighCourtAdvocates();
                    return View(model);
                }

                if (model.DivisionID <= 0)
                {
                    model.DivisionID = HttpContext.Session.GetInt32(SessionKeys.DivisionID) ?? 0;
                }

                // If editing, preserve existing uploaded document paths if no new file is selected
                if (model.ServiceID.HasValue && model.ServiceID > 0)
                {
                    var existing = _labourRepo.GetServiceMatterById(model.ServiceID.Value);
                    if (existing != null)
                    {
                        if (model.DivisionID <= 0) model.DivisionID = existing.DivisionID;
                        if (string.IsNullOrEmpty(model.CO_Service_PetitionCopyPath)) model.CO_Service_PetitionCopyPath = existing.CO_Service_PetitionCopyPath;
                        if (string.IsNullOrEmpty(model.JudgmentCopyPath)) model.JudgmentCopyPath = existing.JudgmentCopyPath;
                        if (string.IsNullOrEmpty(model.CO_Service_ApprovalCopyPath)) model.CO_Service_ApprovalCopyPath = existing.CO_Service_ApprovalCopyPath;
                        if (string.IsNullOrEmpty(model.CO_WA_StayOrderPath)) model.CO_WA_StayOrderPath = existing.CO_WA_StayOrderPath;
                    }
                }

                // Handle File Uploads securely via SaveFile helper
                if (model.CO_Service_PetitionCopyFile != null)
                {
                    model.CO_Service_PetitionCopyPath = SaveFile(model.CO_Service_PetitionCopyFile, "petitions");
                }
                
                if (model.JudgmentCopyFile != null)
                {
                    model.JudgmentCopyPath = SaveFile(model.JudgmentCopyFile, "judgments");
                }
                
                if (model.CO_Service_ApprovalCopyFile != null)
                {
                    model.CO_Service_ApprovalCopyPath = SaveFile(model.CO_Service_ApprovalCopyFile, "approvals");
                }

                if (model.WAStayOrderFile != null)
                {
                    model.CO_WA_StayOrderPath = SaveFile(model.WAStayOrderFile, "wa_stays");
                }

                if (model.ServiceID.HasValue && model.ServiceID > 0)
                {
                    model.ModifiedBy = HttpContext.Session.GetInt32(SessionKeys.UserID);
                    _labourRepo.UpdateServiceMatter(model);
                    _ = _activityLogger.LogCaseUpdatedAsync("LABOUR", model.ServiceID.Value, $"WP {model.CO_Service_WPNumber}/{model.CO_Service_WPYear}", null, "High Court", null, model, $"Updated Service Matter WP #{model.CO_Service_WPNumber}/{model.CO_Service_WPYear}");
                    TempData["SuccessMessage"] = "Service Matter Updated Successfully";
                }
                else
                {
                    model.CreatedBy = HttpContext.Session.GetInt32(SessionKeys.UserID);
                    model.CaseType = "Service Matter";
                    _labourRepo.SaveServiceMatter(model);
                    _ = _activityLogger.LogCaseCreatedAsync("LABOUR", model.ServiceID ?? 0, $"WP {model.CO_Service_WPNumber}/{model.CO_Service_WPYear}", null, "High Court", model, $"Registered new Service Matter WP #{model.CO_Service_WPNumber}/{model.CO_Service_WPYear}");
                    TempData["SuccessMessage"] = "Service Matter Registered Successfully";
                }
                return RedirectToAction("ServiceMatters");
            }
            catch (Exception ex)
            {
                ViewBag.Error = "Error saving service matter: " + ex.Message;
                ViewBag.Divisions = _masterRepo.GetAllDivisions();
                ViewBag.Advocates = _masterRepo.GetHighCourtAdvocates();
                return View(model);
            }
        }
        // --- CLAIMANT APPEALS ---

        public IActionResult ClaimantAppeals()
        {
            var divIdString = User.FindFirst("DivisionID")?.Value;
            int sessDivId = HttpContext.Session.GetInt32(SessionKeys.DivisionID) ?? 0;
            bool isCentralOffice = string.IsNullOrEmpty(divIdString) || divIdString == "0" || divIdString == "5" || sessDivId == 0 || sessDivId == 5;
            int divisionId = isCentralOffice ? 0 : (int.TryParse(divIdString, out int id) ? id : sessDivId);

            var cases = _labourRepo.GetCases(divisionId)
                .Where(c => c.CaseType != "Claimant Writ Appeal" 
                         && c.CaseType != "Claimant WA" 
                         && c.CaseType != "CCC Cases"
                         && c.CaseType != "CCC"
                         && c.CaseType != "Civil Contempt Petition"
                         && c.CaseType != "Civil Contempt of Court"
                         && (c.CaseType == "Claimant Appeal" 
                             || c.CaseType == "Claimant Writ Petition" 
                             || c.CaseType == "Claimant WP"
                             || (!string.IsNullOrEmpty(c.CO_Claimant_CaseNumber) && c.CaseType != "CCC Cases" && c.CaseType != "CCC")))
                .OrderByDescending(c => c.CreatedDate)
                .ToList();
            return View(cases);
        }

        public IActionResult ClaimantSLPs()
        {
            int divisionId = HttpContext.Session.GetInt32(SessionKeys.DivisionID) ?? 0;
            var cases = _labourRepo.GetCases(divisionId)
                .Where(c => !string.IsNullOrEmpty(c.ClaimantSCNumber) || !string.IsNullOrEmpty(c.ClaimantSCDiaryNumber) || c.IsClaimantSCPending)
                .OrderByDescending(c => c.CreatedDate)
                .ToList();
            return View(cases);
        }

        public IActionResult ManageClaimantSLP(int? id)
        {
            LabourCase model;
            if (id.HasValue && id.Value > 0)
            {
                model = _labourRepo.GetCaseById(id.Value) ?? new LabourCase { CaseType = "Claimant SLP" };
            }
            else
            {
                model = new LabourCase { CaseType = "Claimant SLP", DivisionID = HttpContext.Session.GetInt32(SessionKeys.DivisionID) ?? 0 };
            }
            PopulateDropdowns();
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ManageClaimantSLP(LabourCase model)
        {
            try
            {
                if (model.JudgmentCopyFile != null)
                {
                    model.JudgmentCopyPath = SaveFile(model.JudgmentCopyFile, "Labour/Judgments");
                }

                if (model.CaseID > 0)
                {
                    _labourRepo.UpdateCase(model);
                    _ = _activityLogger.LogCaseUpdatedAsync("LABOUR", model.CaseID, $"SLP {model.ClaimantSCNumber ?? model.CaseNumber}", null, "Supreme Court", null, model, $"Updated Claimant SLP #{model.ClaimantSCNumber ?? model.CaseNumber}");
                    TempData["SuccessMessage"] = "SLP Details Updated Successfully";
                }
                else
                {
                    model.CaseType = "Claimant SLP";
                    _labourRepo.SaveCase(model);
                    _ = _activityLogger.LogCaseCreatedAsync("LABOUR", model.CaseID, $"SLP {model.ClaimantSCNumber ?? model.CaseNumber}", null, "Supreme Court", model, $"Registered new Claimant SLP #{model.ClaimantSCNumber ?? model.CaseNumber}");
                    TempData["SuccessMessage"] = "SLP Registered Successfully";
                }
                return RedirectToAction("ClaimantSLPs");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Error: " + ex.Message);
                PopulateDropdowns();
                return View(model);
            }
        }

        public IActionResult ManageClaimantAppeal(int? id)
        {
            LabourCase model;
            if (id.HasValue && id.Value > 0)
            {
                model = _labourRepo.GetCaseById(id.Value) ?? new LabourCase { CaseType = "Claimant Appeal" };
            }
            else
            {
                model = new LabourCase { CaseType = "Claimant Appeal", DivisionID = HttpContext.Session.GetInt32(SessionKeys.DivisionID) ?? 0 };
            }
            PopulateDropdowns();
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ManageClaimantAppeal(LabourCase model)
        {
            try
            {
                if (model.DivisionID <= 0)
                {
                    model.DivisionID = HttpContext.Session.GetInt32(SessionKeys.DivisionID) ?? 0;
                }

                if (model.CO_Claimant_PetitionCopyFile != null)
                {
                    model.CO_Claimant_PetitionCopyPath = SaveFile(model.CO_Claimant_PetitionCopyFile, "Labour/ClaimantAppeals");
                }

                if (model.JudgmentCopyFile != null)
                {
                    model.JudgmentCopyPath = SaveFile(model.JudgmentCopyFile, "Labour/Judgments");
                }

                if (string.IsNullOrEmpty(model.CaseNumber) && !string.IsNullOrEmpty(model.CO_Claimant_CaseNumber))
                {
                    model.CaseNumber = model.CO_Claimant_CaseNumber;
                }
                if ((!model.CaseYear.HasValue || model.CaseYear == 0) && model.CO_Claimant_CaseYear.HasValue)
                {
                    model.CaseYear = model.CO_Claimant_CaseYear;
                }

                if (model.CaseID > 0)
                {
                    _labourRepo.UpdateCase(model);
                    _ = _activityLogger.LogCaseUpdatedAsync("LABOUR", model.CaseID, $"Claimant {model.CaseNumber}/{model.CaseYear}", null, model.CourtName, null, model, $"Updated Claimant Petition #{model.CaseNumber}/{model.CaseYear}");
                    TempData["SuccessMessage"] = "Claimant Petition Updated Successfully";
                }
                else
                {
                    if (string.IsNullOrEmpty(model.CaseType))
                    {
                        model.CaseType = "Claimant Appeal";
                    }
                    _labourRepo.SaveCase(model);
                    _ = _activityLogger.LogCaseCreatedAsync("LABOUR", model.CaseID, $"Claimant {model.CaseNumber}/{model.CaseYear}", null, model.CourtName, model, $"Registered new Claimant Petition #{model.CaseNumber}/{model.CaseYear}");
                    TempData["SuccessMessage"] = "Claimant Petition Registered Successfully";
                }
                return RedirectToAction("ClaimantAppeals");
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error: " + ex.Message;
                PopulateDropdowns();
                return View(model);
            }
        }

        // --- CCC CASES (Civil Contempt of Court) ---

        public IActionResult CCCCases()
        {
            var divIdString = User.FindFirst("DivisionID")?.Value;
            int sessDivId = HttpContext.Session.GetInt32(SessionKeys.DivisionID) ?? 0;
            bool isCentralOffice = string.IsNullOrEmpty(divIdString) || divIdString == "0" || divIdString == "5" || sessDivId == 0 || sessDivId == 5;
            int divisionId = isCentralOffice ? 0 : (int.TryParse(divIdString, out int id) ? id : sessDivId);

            var cases = _labourRepo.GetCases(divisionId)
                .Where(c => c.CaseType == "CCC Cases" 
                         || c.CaseType == "CCC" 
                         || c.CaseType == "Civil Contempt Petition" 
                         || c.CaseType == "Civil Contempt of Court")
                .OrderByDescending(c => c.CreatedDate)
                .ToList();
            return View(cases);
        }

        public IActionResult ManageCCCCase(int? id)
        {
            LabourCase model;
            if (id.HasValue && id.Value > 0)
            {
                model = _labourRepo.GetCaseById(id.Value) ?? new LabourCase { CaseType = "CCC Cases" };
            }
            else
            {
                model = new LabourCase { CaseType = "CCC Cases", DivisionID = HttpContext.Session.GetInt32(SessionKeys.DivisionID) ?? 0 };
            }
            PopulateDropdowns();
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ManageCCCCase(LabourCase model)
        {
            try
            {
                if (model.DivisionID <= 0)
                {
                    model.DivisionID = HttpContext.Session.GetInt32(SessionKeys.DivisionID) ?? 0;
                }

                if (model.CO_Claimant_PetitionCopyFile != null)
                {
                    model.CO_Claimant_PetitionCopyPath = SaveFile(model.CO_Claimant_PetitionCopyFile, "Labour/CCCCases");
                }

                if (model.JudgmentCopyFile != null)
                {
                    model.JudgmentCopyPath = SaveFile(model.JudgmentCopyFile, "Labour/Judgments");
                }

                if (string.IsNullOrEmpty(model.CaseNumber) && !string.IsNullOrEmpty(model.CO_Claimant_CaseNumber))
                {
                    model.CaseNumber = model.CO_Claimant_CaseNumber;
                }
                if ((!model.CaseYear.HasValue || model.CaseYear == 0) && model.CO_Claimant_CaseYear.HasValue)
                {
                    model.CaseYear = model.CO_Claimant_CaseYear;
                }

                model.CaseType = "CCC Cases";

                if (model.CaseID > 0)
                {
                    _labourRepo.UpdateCase(model);
                    _ = _activityLogger.LogCaseUpdatedAsync("LABOUR", model.CaseID, $"CCC {model.CaseNumber}/{model.CaseYear}", null, "High Court", null, model, $"Updated Contempt of Court (CCC) Case #{model.CaseNumber}/{model.CaseYear}");
                    TempData["SuccessMessage"] = "CCC Case Updated Successfully";
                }
                else
                {
                    _labourRepo.SaveCase(model);
                    _ = _activityLogger.LogCaseCreatedAsync("LABOUR", model.CaseID, $"CCC {model.CaseNumber}/{model.CaseYear}", null, "High Court", model, $"Registered new Contempt of Court (CCC) Case #{model.CaseNumber}/{model.CaseYear}");
                    TempData["SuccessMessage"] = "CCC Case Registered Successfully";
                }
                return RedirectToAction("CCCCases");
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error: " + ex.Message;
                PopulateDropdowns();
                return View(model);
            }
        }

        // --- REVIEW PETITIONS (Review Petition Filing) ---

        public IActionResult ReviewPetitions()
        {
            var divIdString = User.FindFirst("DivisionID")?.Value;
            int sessDivId = HttpContext.Session.GetInt32(SessionKeys.DivisionID) ?? 0;
            bool isCentralOffice = string.IsNullOrEmpty(divIdString) || divIdString == "0" || divIdString == "5" || sessDivId == 0 || sessDivId == 5;
            int divisionId = isCentralOffice ? 0 : (int.TryParse(divIdString, out int id) ? id : sessDivId);

            var cases = _labourRepo.GetCases(divisionId)
                .Where(c => c.CaseType == "Review Petition" 
                         || c.CaseType == "Review Petitions" 
                         || c.CaseType == "RP"
                         || c.CaseType == "Review Petition Filing")
                .OrderByDescending(c => c.CreatedDate)
                .ToList();
            return View(cases);
        }

        public IActionResult ManageReviewPetition(int? id)
        {
            LabourCase model;
            if (id.HasValue && id.Value > 0)
            {
                model = _labourRepo.GetCaseById(id.Value) ?? new LabourCase { CaseType = "Review Petition" };
            }
            else
            {
                model = new LabourCase { CaseType = "Review Petition", DivisionID = HttpContext.Session.GetInt32(SessionKeys.DivisionID) ?? 0 };
            }
            PopulateDropdowns();
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ManageReviewPetition(LabourCase model)
        {
            try
            {
                if (model.DivisionID <= 0)
                {
                    model.DivisionID = HttpContext.Session.GetInt32(SessionKeys.DivisionID) ?? 0;
                }

                if (model.CO_Claimant_PetitionCopyFile != null)
                {
                    model.CO_Claimant_PetitionCopyPath = SaveFile(model.CO_Claimant_PetitionCopyFile, "Labour/ReviewPetitions");
                }

                if (model.JudgmentCopyFile != null)
                {
                    model.JudgmentCopyPath = SaveFile(model.JudgmentCopyFile, "Labour/Judgments");
                }

                if (string.IsNullOrEmpty(model.CaseNumber) && !string.IsNullOrEmpty(model.CO_Claimant_CaseNumber))
                {
                    model.CaseNumber = model.CO_Claimant_CaseNumber;
                }
                if ((!model.CaseYear.HasValue || model.CaseYear == 0) && model.CO_Claimant_CaseYear.HasValue)
                {
                    model.CaseYear = model.CO_Claimant_CaseYear;
                }

                model.CaseType = "Review Petition";

                if (model.CaseID > 0)
                {
                    _labourRepo.UpdateCase(model);
                    _ = _activityLogger.LogCaseUpdatedAsync("LABOUR", model.CaseID, $"RP {model.CaseNumber}/{model.CaseYear}", null, "High Court", null, model, $"Updated Review Petition #{model.CaseNumber}/{model.CaseYear}");
                    TempData["SuccessMessage"] = "Review Petition Updated Successfully";
                }
                else
                {
                    _labourRepo.SaveCase(model);
                    _ = _activityLogger.LogCaseCreatedAsync("LABOUR", model.CaseID, $"RP {model.CaseNumber}/{model.CaseYear}", null, "High Court", model, $"Registered new Review Petition #{model.CaseNumber}/{model.CaseYear}");
                    TempData["SuccessMessage"] = "Review Petition Registered Successfully";
                }
                return RedirectToAction("ReviewPetitions");
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error: " + ex.Message;
                PopulateDropdowns();
                return View(model);
            }
        }

        public IActionResult GetCaseDetailsPartial(int id)
        {
            var model = _labourRepo.GetCaseById(id);
            if (model == null) return NotFound();

            int divisionId = HttpContext.Session.GetInt32(SessionKeys.DivisionID) ?? -1;
            bool isCO = (divisionId == 0 || divisionId == 5);
            ViewBag.IsCentralOffice = isCO;

            return PartialView("_LabourCaseDetailsPartial", model);
        }

        // --- CLAIMANT WRIT APPEALS ---

        public IActionResult ClaimantWritAppeals()
        {
            int divisionId = HttpContext.Session.GetInt32(SessionKeys.DivisionID) ?? 0;
            var cases = _labourRepo.GetCases(divisionId).Where(c => c.CaseType == "Claimant Writ Appeal").OrderByDescending(c => c.CreatedDate).ToList();
            return View(cases);
        }

        public IActionResult ManageClaimantWritAppeal(int? id)
        {
            LabourCase model;
            if (id.HasValue && id.Value > 0)
            {
                model = _labourRepo.GetCaseById(id.Value) ?? new LabourCase { CaseType = "Claimant Writ Appeal" };
            }
            else
            {
                model = new LabourCase { CaseType = "Claimant Writ Appeal", DivisionID = HttpContext.Session.GetInt32(SessionKeys.DivisionID) ?? 0 };
            }
            PopulateDropdowns();
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ManageClaimantWritAppeal(LabourCase model)
        {
            try
            {
                if (model.CO_Claimant_PetitionCopyFile != null)
                {
                    model.CO_Claimant_PetitionCopyPath = SaveFile(model.CO_Claimant_PetitionCopyFile, "Labour/ClaimantAppeals");
                }

                if (model.CaseID > 0)
                {
                    _labourRepo.UpdateCase(model);
                    _ = _activityLogger.LogCaseUpdatedAsync("LABOUR", model.CaseID, $"WA {model.CaseNumber}/{model.CaseYear}", null, "High Court", null, model, $"Updated Claimant Writ Appeal #{model.CaseNumber}/{model.CaseYear}");
                    TempData["SuccessMessage"] = "Claimant Writ Appeal Updated Successfully";
                }
                else
                {
                    model.CaseType = "Claimant Writ Appeal";
                    _labourRepo.SaveCase(model);
                    _ = _activityLogger.LogCaseCreatedAsync("LABOUR", model.CaseID, $"WA {model.CaseNumber}/{model.CaseYear}", null, "High Court", model, $"Registered new Claimant Writ Appeal #{model.CaseNumber}/{model.CaseYear}");
                    TempData["SuccessMessage"] = "Claimant Writ Appeal Registered Successfully";
                }
                return RedirectToAction("ClaimantWritAppeals");
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error: " + ex.Message;
                PopulateDropdowns();
                return View(model);
            }
        }
        [HttpPost]
        public IActionResult UploadReinstatementDocument(int caseId, string documentName, IFormFile documentFile)
        {
            try
            {
                if (documentFile == null || documentFile.Length == 0)
                {
                    return Json(new { success = false, message = "Please select a file." });
                }

                if (string.IsNullOrEmpty(documentName))
                {
                    documentName = Path.GetFileNameWithoutExtension(documentFile.FileName);
                }

                string path = SaveFile(documentFile, "Labour/ReinstatementDocuments");
                
                var doc = new LabourReinstatementDocument
                {
                    CaseID = caseId,
                    DocumentName = documentName,
                    DocumentPath = path,
                    UploadedDate = DateTime.Now
                };

                _labourRepo.AddReinstatedDocument(doc);
                _ = _activityLogger.LogSubEntityActionAsync("LABOUR", caseId, $"Labour #{caseId}", "DOCUMENT_UPLOADED", $"Uploaded reinstatement document: {documentName}");

                return Json(new { success = true, message = "Document uploaded successfully." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error: " + ex.Message });
            }
        }

        [HttpPost]
        public IActionResult UploadReinstatementDocuments(int caseId, List<string> documentNames, List<IFormFile> documentFiles)
        {
            try
            {
                if (documentFiles == null || !documentFiles.Any())
                {
                    return Json(new { success = false, message = "Please select at least one document to upload." });
                }

                int count = 0;
                for (int i = 0; i < documentFiles.Count; i++)
                {
                    var file = documentFiles[i];
                    if (file == null || file.Length == 0) continue;

                    string name = (documentNames != null && i < documentNames.Count && !string.IsNullOrWhiteSpace(documentNames[i]))
                        ? documentNames[i]
                        : Path.GetFileNameWithoutExtension(file.FileName);

                    string path = SaveFile(file, "Labour/ReinstatementDocuments");

                    var doc = new LabourReinstatementDocument
                    {
                        CaseID = caseId,
                        DocumentName = name,
                        DocumentPath = path,
                        UploadedDate = DateTime.Now
                    };

                    _labourRepo.AddReinstatedDocument(doc);
                    count++;
                }

                if (count == 0)
                {
                    return Json(new { success = false, message = "No valid files were uploaded." });
                }

                _ = _activityLogger.LogSubEntityActionAsync("LABOUR", caseId, $"Labour #{caseId}", "DOCUMENT_UPLOADED", $"Uploaded {count} reinstatement document(s)");
                return Json(new { success = true, message = $"{count} document(s) uploaded successfully." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error: " + ex.Message });
            }
        }

        [HttpPost]
        public IActionResult UploadCaseDocuments(int caseId, IFormFile? judgmentCopyFile, IFormFile? punishmentCopyFile, List<string>? documentNames, List<IFormFile>? documentFiles)
        {
            try
            {
                var caseObj = _labourRepo.GetCaseById(caseId);
                if (caseObj == null)
                {
                    return Json(new { success = false, message = "Case not found." });
                }

                int count = 0;
                if (judgmentCopyFile != null && judgmentCopyFile.Length > 0)
                {
                    string jPath = SaveFile(judgmentCopyFile, "Labour/Judgments");
                    caseObj.JudgmentCopyPath = jPath;
                    count++;
                }

                if (punishmentCopyFile != null && punishmentCopyFile.Length > 0)
                {
                    string pPath = SaveFile(punishmentCopyFile, "Labour/Punishments");
                    caseObj.Against_PunishmentCopyPath = pPath;
                    count++;
                }

                if (count > 0)
                {
                    _labourRepo.UpdateCase(caseObj);
                }

                if (documentFiles != null && documentFiles.Any())
                {
                    for (int i = 0; i < documentFiles.Count; i++)
                    {
                        var file = documentFiles[i];
                        if (file == null || file.Length == 0) continue;

                        string name = (documentNames != null && i < documentNames.Count && !string.IsNullOrWhiteSpace(documentNames[i]))
                            ? documentNames[i]
                            : Path.GetFileNameWithoutExtension(file.FileName);

                        string path = SaveFile(file, "Labour/CaseDocuments");

                        var doc = new LabourReinstatementDocument
                        {
                            CaseID = caseId,
                            DocumentName = name,
                            DocumentPath = path,
                            UploadedDate = DateTime.Now
                        };

                        _labourRepo.AddReinstatedDocument(doc);
                        count++;
                    }
                }

                if (count == 0)
                {
                    return Json(new { success = false, message = "Please select at least one document to upload." });
                }

                _ = _activityLogger.LogSubEntityActionAsync("LABOUR", caseId, $"Labour {caseObj.CaseNumber}/{caseObj.CaseYear}", "DOCUMENT_UPLOADED", $"Uploaded {count} case document(s)");
                return Json(new { success = true, message = $"{count} document(s) uploaded successfully." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error: " + ex.Message });
            }
        }

        [HttpPost]
        public IActionResult UploadCaseDocumentWithRemark(int caseId, string? documentName, string? remarks, IFormFile? documentFile)
        {
            try
            {
                if (caseId <= 0)
                {
                    return Json(new { success = false, message = "Invalid case ID." });
                }

                if (documentFile == null || documentFile.Length == 0)
                {
                    return Json(new { success = false, message = "Please select a document file to upload." });
                }

                string fileName = Path.GetFileNameWithoutExtension(documentFile.FileName);
                string title = !string.IsNullOrWhiteSpace(documentName) ? documentName.Trim() : fileName;
                string finalDocName = !string.IsNullOrWhiteSpace(remarks) ? $"{title} | Remark: {remarks.Trim()}" : title;

                string filePath = SaveFile(documentFile, "Labour/CaseDocuments");

                var doc = new LabourReinstatementDocument
                {
                    CaseID = caseId,
                    DocumentName = finalDocName,
                    DocumentPath = filePath,
                    UploadedDate = DateTime.Now
                };

                _labourRepo.AddReinstatedDocument(doc);
                _ = _activityLogger.LogSubEntityActionAsync("LABOUR", caseId, $"Labour #{caseId}", "DOCUMENT_UPLOADED", $"Uploaded document: {finalDocName}");

                return Json(new { success = true, message = "Document uploaded successfully." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error uploading document: " + ex.Message });
            }
        }

        [HttpPost]
        public IActionResult AddArisingPayment(int arisingId, decimal amount, DateTime paymentDate, string? chequeNumber, DateTime? chequeDate, string? remarks)
        {
            try
            {
                if (arisingId <= 0 || amount <= 0)
                {
                    return Json(new { success = false, message = "Invalid payment parameters." });
                }

                var payment = new ArisingApplicationPayment
                {
                    ArisingID = arisingId,
                    Amount = amount,
                    PaymentDate = paymentDate != default ? paymentDate : DateTime.Now,
                    ChequeNumber = chequeNumber,
                    ChequeDate = chequeDate,
                    Remarks = remarks
                };

                _arisingRepo.AddPayment(payment);
                _ = _activityLogger.LogSubEntityActionAsync("ARISING", arisingId, $"Arising #{arisingId}", "PAYMENT_ADDED", $"Recorded payment of ₹{amount:N2} (Cheque: {chequeNumber})", payment);
                return Json(new { success = true, message = "Payment recorded successfully." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error saving payment: " + ex.Message });
            }
        }

        [HttpPost]
        public IActionResult DeleteArisingPayment(int paymentId)
        {
            try
            {
                bool deleted = _arisingRepo.DeletePayment(paymentId);
                if (deleted)
                {
                    _ = _activityLogger.LogSubEntityActionAsync("ARISING", 0, "Arising Payment", "PAYMENT_DELETED", $"Deleted arising payment record #{paymentId}");
                }
                return Json(new { success = deleted, message = deleted ? "Payment deleted." : "Payment not found." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error: " + ex.Message });
            }
        }

        [HttpPost]
        public IActionResult UpdateArisingAwardAmount(int arisingId, decimal awardAmount)
        {
            try
            {
                bool updated = _arisingRepo.UpdateAwardAmount(arisingId, awardAmount);
                if (updated)
                {
                    _ = _activityLogger.LogSubEntityActionAsync("ARISING", arisingId, $"Arising #{arisingId}", "AWARD_AMOUNT_UPDATED", $"Updated award amount to ₹{awardAmount:N2}");
                }
                return Json(new { success = updated, message = updated ? "Award amount updated." : "Failed to update award amount." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error: " + ex.Message });
            }
        }
    }
}
