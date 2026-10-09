using Microsoft.AspNetCore.Mvc;
using MVCCaseManagement.Models;
using MVCCaseManagement.DAL;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using MVCCaseManagement.Services.Audit;

namespace MVCCaseManagement.Controllers
{
    [Authorize]
    public class AppealController : Controller
    {
        private readonly ICaseRepository _caseRepo;
        private readonly IAppealRepository _appealRepo;
        private readonly IMasterRepository _masterRepo;
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<AppealController> _logger;
        private readonly ICaseActivityLogger _activityLogger;

        public AppealController(
            ICaseRepository caseRepo, 
            IAppealRepository appealRepo, 
            IMasterRepository masterRepo, 
            IWebHostEnvironment environment, 
            ILogger<AppealController> logger,
            ICaseActivityLogger activityLogger)
        {
            _caseRepo = caseRepo;
            _appealRepo = appealRepo;
            _masterRepo = masterRepo;
            _environment = environment;
            _logger = logger;
            _activityLogger = activityLogger;
        }

        [HttpGet]
        public IActionResult GetHighCourtAdvocates(string benchCode)
        {
            var advocates = _caseRepo.GetHighCourtAdvocatesByBench(benchCode);
            return Json(advocates);
        }

        [HttpGet]
        public IActionResult Register()
        {
            ViewBag.MACTs = _masterRepo.GetAllMACTs() ?? new List<MACT>();
            ViewBag.Divisions = _masterRepo.GetAllDivisions() ?? new List<Division>();
            ViewBag.HCAdvocates = _masterRepo.GetHighCourtAdvocates() ?? new List<HighCourtAdvocate>();
            
            var model = new AppealViewModel();
            return View(model);
        }

        [HttpGet]
        public IActionResult SLPRegistration()
        {
            ViewBag.MACTs = _masterRepo.GetAllMACTs() ?? new List<MACT>();
            ViewBag.Divisions = _masterRepo.GetAllDivisions() ?? new List<Division>();
            ViewBag.HCAdvocates = _masterRepo.GetHighCourtAdvocates() ?? new List<HighCourtAdvocate>();

            var model = new AppealViewModel();
            return View(model);
        }

        [HttpGet]
        public IActionResult GetMVCDetails(string? mvcNo, int? mvcYear, int? mactId)
        {
            if (string.IsNullOrEmpty(mvcNo) || !mvcYear.HasValue || !mactId.HasValue)
                return BadRequest("Invalid parameters");

            var mvcCase = _caseRepo.GetCaseByMVCDetails(mvcNo, mvcYear.Value, mactId.Value);
            if (mvcCase == null) return NotFound();
            
            return Json(mvcCase);
        }

        [HttpGet]
        public IActionResult GetByMFADetails(string? mfaNo, int? mfaYear)
        {
            if (string.IsNullOrEmpty(mfaNo) || !mfaYear.HasValue)
                return BadRequest("Invalid parameters");

            var appeal = _appealRepo.GetAppealByMFADetails(mfaNo, mfaYear.Value);
            if (appeal == null) return NotFound();

            // Fetch MVC Case details for the header/context
            var mvcCase = _caseRepo.GetCaseById(appeal.CaseID);
            
            // Return combined data (MVC + Appeal)
            // mvcCase.LinkedAppeal already contains the appeal data because GetCaseById fetches it.
            return Json(mvcCase);
        }

        [HttpGet]
        public IActionResult Manage(int id)
        {
            // Fetch Divisions for dropdown  
            IEnumerable<Division> divisions = _masterRepo.GetAllDivisions() ?? new List<Division>();
            ViewBag.Divisions = divisions;

            // Fetch HC Advocates
            IEnumerable<HighCourtAdvocate> hcAdvocates = _masterRepo.GetHighCourtAdvocates() ?? new List<HighCourtAdvocate>();
            ViewBag.HCAdvocates = hcAdvocates;
            
            // Fetch basic case info to display in header
            var caseDetails = _caseRepo.GetCaseById(id);
            if (caseDetails == null) return NotFound();

            // Try Fetch existing Appeal Details
            var model = _appealRepo.GetAppealByCaseId(id);

            if (model == null)
            {
                // Create New if not exists
                model = new AppealViewModel
                {
                    CaseID = id,
                    FeasibilityReceived = false, 
                    StayGranted = false
                };
            }
            
            // Populating display fields from Case
            model.MVCNo = caseDetails.MVCNo ?? "";
            model.MVCYear = caseDetails.MVCYear;
            model.DivisionName = caseDetails.DivisionName ?? "";

            // Auto-populate Claimant Appeal fields from MVC Registration data (if empty)
            if (string.IsNullOrEmpty(model.ClaimantMVCNumber))
            {
                model.ClaimantMVCNumber = caseDetails.MVCNo;
                model.ClaimantMVCYear = caseDetails.MVCYear;
                model.ClaimantDivisionID = caseDetails.DivisionID;
                
                // Priority: Disposal Result (e.g., "Against") -> Current Stage -> "Pending"
                model.ClaimantMVCCurrentStatus = !string.IsNullOrEmpty(caseDetails.DisposalResult) 
                                                 ? caseDetails.DisposalResult 
                                                 : (caseDetails.CurrentStage ?? "Pending");
            }



            // Auto-populate Advocate (Corporation Advocate in MACT -> Corp Advocate in HC, often different but requested to map)
            if (string.IsNullOrEmpty(model.CorpMFAAdvocate) && !string.IsNullOrEmpty(caseDetails.AdvocateName))
            {
                model.CorpMFAAdvocate = caseDetails.AdvocateName;
            }

            // Auto-populate Connected Cases from MACT to Appeal if Appeal Connected list is empty
            if ((model.ConnectedCases == null || !model.ConnectedCases.Any()) && caseDetails.ConnectedCases != null && caseDetails.ConnectedCases.Any())
            {
                if (model.ConnectedCases == null) model.ConnectedCases = new List<ConnectedAppeal>();

                foreach (var mvcConn in caseDetails.ConnectedCases)
                {
                    model.ConnectedCases.Add(new ConnectedAppeal
                    {
                        ConnectedMVCNo = mvcConn.ConnectedMVCNo,
                        // MFA Number and FiledBy will need to be entered manually
                        Status = mvcConn.Status ?? "Pending"
                    });
                }
            }

            // Auto-populate High Court CNR number if empty
            if (string.IsNullOrEmpty(model.CorpMFACNRNumber) && !string.IsNullOrEmpty(caseDetails.CNRNumber) && 
                (caseDetails.CNRNumber.StartsWith("KAHC", StringComparison.OrdinalIgnoreCase) || caseDetails.CNRNumber.Contains("HC")))
            {
                model.CorpMFACNRNumber = caseDetails.CNRNumber;
            }

            if (string.IsNullOrEmpty(model.HighCourtBench))
            {
                model.HighCourtBench = "Dharwad";
            }

            // Handle Custom Bench for "Others" option
            var validBenches = new[] { "Dharwad", "Kalaburagi", "Bangalore", "", null };
            if (!validBenches.Contains(model.HighCourtBench))
            {
                model.OtherHighCourtBench = model.HighCourtBench;
                model.HighCourtBench = "Others";
            }

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Save(AppealViewModel model)
        {
            if(ModelState.IsValid)
            {
                if (model.StayOrderFile1 != null) model.StayOrderPath1 = SaveFile(model.StayOrderFile1, "appeals/stay");
                if (model.StayOrderFile2 != null) model.StayOrderPath2 = SaveFile(model.StayOrderFile2, "appeals/stay");

                if (model.InitialActionFile1 != null) model.InitialActionPath1 = SaveFile(model.InitialActionFile1, "appeals/initial");
                if (model.InitialActionFile2 != null) model.InitialActionPath2 = SaveFile(model.InitialActionFile2, "appeals/initial");
                if (model.ApprovalCopyFile != null) model.ApprovalCopyPath = SaveFile(model.ApprovalCopyFile, "appeals/approval");
                if (model.MFAJudgmentCopyFile != null) model.MFAJudgmentCopyPath = SaveFile(model.MFAJudgmentCopyFile, "appeals/mfa_judgment");
                if (model.ComplianceLetterFile != null) model.ComplianceLetterPath = SaveFile(model.ComplianceLetterFile, "appeals/compliance");
                if (model.CorpMFAActionTakenFile != null) model.CorpMFAActionTakenPath = SaveFile(model.CorpMFAActionTakenFile, "appeals/outcome_approval");

                // Handle "Others" Bench
                if (model.HighCourtBench == "Others" && !string.IsNullOrEmpty(model.OtherHighCourtBench))
                {
                    model.HighCourtBench = model.OtherHighCourtBench;
                }

                // Normalize and sanitize High Court Corporation MFA CNR
                if (!string.IsNullOrWhiteSpace(model.CorpMFACNRNumber))
                {
                    model.CorpMFACNRNumber = model.CorpMFACNRNumber.Trim().ToUpperInvariant();
                }

                // Preserve existing unsubmitted fields (e.g. Claimant MFA details, existing files)
                var existing = _appealRepo.GetAppealByCaseId(model.CaseID);
                if (existing != null)
                {
                    if (string.IsNullOrEmpty(model.CorpMFACNRNumber) && !string.IsNullOrEmpty(existing.CorpMFACNRNumber))
                    {
                        model.CorpMFACNRNumber = existing.CorpMFACNRNumber;
                    }
                    if (!model.CorpMFANextHearingDate.HasValue && existing.CorpMFANextHearingDate.HasValue)
                    {
                        model.CorpMFANextHearingDate = existing.CorpMFANextHearingDate;
                    }
                    if (string.IsNullOrEmpty(model.CorpMFAStage) && !string.IsNullOrEmpty(existing.CorpMFAStage))
                    {
                        model.CorpMFAStage = existing.CorpMFAStage;
                    }

                    if (string.IsNullOrEmpty(model.StayOrderPath1)) model.StayOrderPath1 = existing.StayOrderPath1;
                    if (string.IsNullOrEmpty(model.StayOrderPath2)) model.StayOrderPath2 = existing.StayOrderPath2;
                    if (string.IsNullOrEmpty(model.InitialActionPath1)) model.InitialActionPath1 = existing.InitialActionPath1;
                    if (string.IsNullOrEmpty(model.InitialActionPath2)) model.InitialActionPath2 = existing.InitialActionPath2;
                    if (string.IsNullOrEmpty(model.ApprovalCopyPath)) model.ApprovalCopyPath = existing.ApprovalCopyPath;
                    if (string.IsNullOrEmpty(model.MFAJudgmentCopyPath)) model.MFAJudgmentCopyPath = existing.MFAJudgmentCopyPath;
                    if (string.IsNullOrEmpty(model.ComplianceLetterPath)) model.ComplianceLetterPath = existing.ComplianceLetterPath;
                    if (string.IsNullOrEmpty(model.CorpMFAActionTakenPath)) model.CorpMFAActionTakenPath = existing.CorpMFAActionTakenPath;

                    // Preserve Claimant MFA fields
                    if (string.IsNullOrEmpty(model.ClaimantMFANumber)) model.ClaimantMFANumber = existing.ClaimantMFANumber;
                    if (!model.ClaimantMFAYear.HasValue) model.ClaimantMFAYear = existing.ClaimantMFAYear;
                    if (string.IsNullOrEmpty(model.ClaimantMFACNRNumber)) model.ClaimantMFACNRNumber = existing.ClaimantMFACNRNumber;
                    if (string.IsNullOrEmpty(model.ClaimantMFAEntrustmentNo)) model.ClaimantMFAEntrustmentNo = existing.ClaimantMFAEntrustmentNo;
                    if (!model.ClaimantMFAEntrustmentDate.HasValue) model.ClaimantMFAEntrustmentDate = existing.ClaimantMFAEntrustmentDate;
                    if (string.IsNullOrEmpty(model.ClaimantMFAAdvocate)) model.ClaimantMFAAdvocate = existing.ClaimantMFAAdvocate;
                    if (string.IsNullOrEmpty(model.ClaimantMFAStatus)) model.ClaimantMFAStatus = existing.ClaimantMFAStatus;
                    if (string.IsNullOrEmpty(model.ClaimantMFADecision)) model.ClaimantMFADecision = existing.ClaimantMFADecision;
                    if (string.IsNullOrEmpty(model.ClaimantActionTaken)) model.ClaimantActionTaken = existing.ClaimantActionTaken;
                    if (string.IsNullOrEmpty(model.ClaimantApprovalNo)) model.ClaimantApprovalNo = existing.ClaimantApprovalNo;
                    if (!model.ClaimantApprovalDate.HasValue) model.ClaimantApprovalDate = existing.ClaimantApprovalDate;
                    if (string.IsNullOrEmpty(model.ClaimantMFARemarks)) model.ClaimantMFARemarks = existing.ClaimantMFARemarks;

                    // Preserve Corp SC & Compliance
                    if (string.IsNullOrEmpty(model.CorpSCNumber)) model.CorpSCNumber = existing.CorpSCNumber;
                    if (!model.CorpSCYear.HasValue) model.CorpSCYear = existing.CorpSCYear;
                    if (string.IsNullOrEmpty(model.CorpSCEntrustmentNo)) model.CorpSCEntrustmentNo = existing.CorpSCEntrustmentNo;
                    if (!model.CorpSCEntrustmentDate.HasValue) model.CorpSCEntrustmentDate = existing.CorpSCEntrustmentDate;
                    if (string.IsNullOrEmpty(model.CorpSCAdvocate)) model.CorpSCAdvocate = existing.CorpSCAdvocate;
                    if (string.IsNullOrEmpty(model.CorpSCStatus)) model.CorpSCStatus = existing.CorpSCStatus;

                    if (string.IsNullOrEmpty(model.FinalComplianceStatus)) model.FinalComplianceStatus = existing.FinalComplianceStatus;
                    if (!model.AmountDeposited.HasValue) model.AmountDeposited = existing.AmountDeposited;
                    if (!model.FinalComplianceDate.HasValue) model.FinalComplianceDate = existing.FinalComplianceDate;
                    if (string.IsNullOrEmpty(model.FinalRemarks)) model.FinalRemarks = existing.FinalRemarks;

                    // Preserve CLO Opinion and Feasibility Remarks across role saves
                    if (string.IsNullOrEmpty(model.Opinion_CLO)) model.Opinion_CLO = existing.Opinion_CLO;
                    if (string.IsNullOrEmpty(model.InitialActionRemarks)) model.InitialActionRemarks = existing.InitialActionRemarks;
                    if (string.IsNullOrEmpty(model.InitialAction)) model.InitialAction = existing.InitialAction;
                    if (!model.ApprovalDate.HasValue) model.ApprovalDate = existing.ApprovalDate;
                    if (string.IsNullOrEmpty(model.ApprovalOutwardNo)) model.ApprovalOutwardNo = existing.ApprovalOutwardNo;
                }

                try
                {
                    _appealRepo.SaveAppeal(model);
                    
                    _ = _activityLogger.LogCaseUpdatedAsync(
                        "APPEAL",
                        model.CaseID,
                        $"MFA {model.CorpMFANumber}/{model.CorpMFAYear}",
                        null,
                        model.HighCourtBench ?? "High Court",
                        null,
                        model,
                        $"Saved High Court Appeal MFA #{model.CorpMFANumber}/{model.CorpMFAYear} (Bench: {model.HighCourtBench}, Stage: {model.CorpMFAStage ?? "N/A"})");

                    var userName = User.Claims.FirstOrDefault(c => c.Type == "FullName")?.Value ?? User.Identity?.Name ?? "User";
                    TempData["SuccessMessage"] = $"Details are saved successfully. Details are updated by {userName}.";
                    
                    return RedirectToAction("Manage", new { id = model.CaseID }); 
                }
                catch (Exception ex)
                {
                    TempData["ErrorMessage"] = "Error saving appeal: " + ex.Message;
                    // Log the full exception if possible, or at least show the inner exception
                    if (ex.InnerException != null)
                    {
                        TempData["ErrorMessage"] += " | Inner: " + ex.InnerException.Message;
                    }
                }
            }
            else
            {
                var errors = string.Join(" | ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
                TempData["ErrorMessage"] = "Validation Error: " + errors;
            }
            return RedirectToAction("Manage", new { id = model.CaseID });
        }

        // ==========================================
        // PER-ROLE ACTIONS: LO, Dy CLO, CLO, MD
        // ==========================================

        [HttpGet]
        public IActionResult LOAction(int id)
        {
            var caseDetails = _caseRepo.GetCaseById(id);
            if (caseDetails == null) return NotFound();

            var model = _appealRepo.GetAppealByCaseId(id) ?? new AppealViewModel { CaseID = id };
            model.MVCNo = caseDetails.MVCNo ?? "";
            model.MVCYear = caseDetails.MVCYear;
            model.DivisionName = caseDetails.DivisionName ?? "";

            bool isAuthorized = User.IsInRole("LO") || User.IsInRole("Admin");
            if (!isAuthorized)
            {
                TempData["ErrorMessage"] = "Access Denied: Only Law Officer (LO) can record this action.";
                return RedirectToAction("Details", "Case", new { id });
            }

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult LOAction(AppealViewModel model)
        {
            bool isAuthorized = User.IsInRole("LO") || User.IsInRole("Admin");
            if (!isAuthorized)
            {
                TempData["ErrorMessage"] = "Access Denied: Only Law Officer (LO) or Admin can record this action.";
                return RedirectToAction("Details", "Case", new { id = model.CaseID });
            }

            var dbAppeal = _appealRepo.GetAppealByCaseId(model.CaseID) ?? new AppealViewModel { CaseID = model.CaseID };
            dbAppeal.ActionTaken_LO = model.ActionTaken_LO;
            dbAppeal.ApprovalDate_LO = model.ApprovalDate_LO;
            dbAppeal.Opinion_LO = model.Opinion_LO;

            try
            {
                _appealRepo.SaveAppeal(dbAppeal);

                _ = _activityLogger.LogSubEntityActionAsync(
                    "APPEAL",
                    model.CaseID,
                    $"Appeal #{model.CaseID}",
                    "CENTRAL_OFFICER_REVIEW",
                    $"Law Officer (LO) action recorded: {model.ActionTaken_LO} | Opinion: {model.Opinion_LO}",
                    model);

                TempData["SuccessMessage"] = "Law Officer action recorded successfully.";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error saving action: " + ex.Message;
            }

            return RedirectToAction("Details", "Case", new { id = model.CaseID });
        }

        [HttpGet]
        public IActionResult DyCLOAction(int id)
        {
            var caseDetails = _caseRepo.GetCaseById(id);
            if (caseDetails == null) return NotFound();

            var model = _appealRepo.GetAppealByCaseId(id) ?? new AppealViewModel { CaseID = id };
            model.MVCNo = caseDetails.MVCNo ?? "";
            model.MVCYear = caseDetails.MVCYear;
            model.DivisionName = caseDetails.DivisionName ?? "";

            bool isAuthorized = User.IsInRole("Dy CLO") || User.IsInRole("DyCLO") || User.IsInRole("Admin");
            if (!isAuthorized)
            {
                TempData["ErrorMessage"] = "Access Denied: Only Deputy Chief Law Officer can record this action.";
                return RedirectToAction("Details", "Case", new { id });
            }

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult DyCLOAction(AppealViewModel model)
        {
            bool isAuthorized = User.IsInRole("Dy CLO") || User.IsInRole("DyCLO") || User.IsInRole("Admin");
            if (!isAuthorized)
            {
                TempData["ErrorMessage"] = "Access Denied: Only Deputy Chief Law Officer or Admin can record this action.";
                return RedirectToAction("Details", "Case", new { id = model.CaseID });
            }

            var dbAppeal = _appealRepo.GetAppealByCaseId(model.CaseID) ?? new AppealViewModel { CaseID = model.CaseID };
            dbAppeal.ActionTaken_DyCLO = model.ActionTaken_DyCLO;
            dbAppeal.ApprovalDate_DyCLO = model.ApprovalDate_DyCLO;
            dbAppeal.Opinion_DyCLO = model.Opinion_DyCLO;

            try
            {
                _appealRepo.SaveAppeal(dbAppeal);

                _ = _activityLogger.LogSubEntityActionAsync(
                    "APPEAL",
                    model.CaseID,
                    $"Appeal #{model.CaseID}",
                    "CENTRAL_OFFICER_REVIEW",
                    $"Deputy Chief Law Officer (Dy CLO) action recorded: {model.ActionTaken_DyCLO} | Opinion: {model.Opinion_DyCLO}",
                    model);

                TempData["SuccessMessage"] = "Deputy Chief Law Officer action recorded successfully.";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error saving action: " + ex.Message;
            }

            return RedirectToAction("Details", "Case", new { id = model.CaseID });
        }

        [HttpGet]
        public IActionResult CLOAction(int id)
        {
            var caseDetails = _caseRepo.GetCaseById(id);
            if (caseDetails == null) return NotFound();

            var model = _appealRepo.GetAppealByCaseId(id) ?? new AppealViewModel { CaseID = id };
            model.MVCNo = caseDetails.MVCNo ?? "";
            model.MVCYear = caseDetails.MVCYear;
            model.DivisionName = caseDetails.DivisionName ?? "";

            bool isAuthorizedClo = User.IsInRole("CLO") || User.IsInRole("Dy CLO") || User.IsInRole("DyCLO") || User.IsInRole("Admin");
            if (!isAuthorizedClo)
            {
                TempData["ErrorMessage"] = "Access Denied: Only CLO can record this action.";
                return RedirectToAction("Details", "Case", new { id });
            }

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CLOAction(AppealViewModel model)
        {
            bool isAuthorizedClo = User.IsInRole("CLO") || User.IsInRole("Dy CLO") || User.IsInRole("DyCLO") || User.IsInRole("Admin");
            if (!isAuthorizedClo)
            {
                string roleName = User.FindFirstValue(ClaimTypes.Role) ?? "";
                _logger.LogWarning("Access Denied: User role {Role} attempted Appeal CLOAction", roleName);
                TempData["ErrorMessage"] = "Access Denied: Only CLO, Dy CLO, or Admin can record this action.";
                return RedirectToAction("Details", "Case", new { id = model.CaseID });
            }

            var dbAppeal = _appealRepo.GetAppealByCaseId(model.CaseID) ?? new AppealViewModel { CaseID = model.CaseID };

            string actionVal = !string.IsNullOrEmpty(model.ActionTaken_CLO) ? model.ActionTaken_CLO : model.InitialAction;
            DateTime? approvalDateVal = model.ApprovalDate_CLO ?? model.ApprovalDate;

            dbAppeal.ActionTaken_CLO = actionVal;
            dbAppeal.ApprovalDate_CLO = approvalDateVal;
            dbAppeal.Opinion_CLO = model.Opinion_CLO;

            // Backward compat
            dbAppeal.ApprovalDate = approvalDateVal;
            if (actionVal == "Approved")
            {
                dbAppeal.InitialAction = "Pending before Competent Authority";
            }
            else if (actionVal == "Rejected")
            {
                dbAppeal.InitialAction = "Close";
            }
            else if (!string.IsNullOrEmpty(actionVal))
            {
                dbAppeal.InitialAction = actionVal;
            }

            try
            {
                _appealRepo.SaveAppeal(dbAppeal);

                _ = _activityLogger.LogSubEntityActionAsync(
                    "APPEAL",
                    model.CaseID,
                    $"Appeal #{model.CaseID}",
                    "CENTRAL_OFFICER_REVIEW",
                    $"Chief Law Officer (CLO) action recorded: {actionVal} | Opinion: {model.Opinion_CLO}",
                    model);

                TempData["SuccessMessage"] = "Chief Law Officer action recorded successfully.";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error saving action: " + ex.Message;
            }

            return RedirectToAction("Details", "Case", new { id = model.CaseID });
        }

        [HttpGet]
        public IActionResult MDAction(int id)
        {
            var caseDetails = _caseRepo.GetCaseById(id);
            if (caseDetails == null) return NotFound();

            var model = _appealRepo.GetAppealByCaseId(id) ?? new AppealViewModel { CaseID = id };
            model.MVCNo = caseDetails.MVCNo ?? "";
            model.MVCYear = caseDetails.MVCYear;
            model.DivisionName = caseDetails.DivisionName ?? "";

            bool isAuthorized = User.IsInRole("MD") || User.IsInRole("Admin");
            if (!isAuthorized)
            {
                TempData["ErrorMessage"] = "Access Denied: Only Managing Director (MD) can record this action.";
                return RedirectToAction("Details", "Case", new { id });
            }

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult MDAction(AppealViewModel model)
        {
            bool isAuthorized = User.IsInRole("MD") || User.IsInRole("Admin");
            if (!isAuthorized)
            {
                TempData["ErrorMessage"] = "Access Denied: Only Managing Director (MD) or Admin can record this action.";
                return RedirectToAction("Details", "Case", new { id = model.CaseID });
            }

            var dbAppeal = _appealRepo.GetAppealByCaseId(model.CaseID) ?? new AppealViewModel { CaseID = model.CaseID };
            dbAppeal.ActionTaken_MD = model.ActionTaken_MD;
            dbAppeal.ApprovalDate_MD = model.ApprovalDate_MD;
            dbAppeal.Opinion_MD = model.Opinion_MD;

            try
            {
                _appealRepo.SaveAppeal(dbAppeal);

                _ = _activityLogger.LogSubEntityActionAsync(
                    "APPEAL",
                    model.CaseID,
                    $"Appeal #{model.CaseID}",
                    "CENTRAL_OFFICER_REVIEW",
                    $"Managing Director (MD) action recorded: {model.ActionTaken_MD} | Opinion: {model.Opinion_MD}",
                    model);

                TempData["SuccessMessage"] = "Managing Director action recorded successfully.";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error saving action: " + ex.Message;
            }

            return RedirectToAction("Details", "Case", new { id = model.CaseID });
        }

        [HttpGet]
        public IActionResult ClaimantList(string? mvcSearchNo = null, int? mvcSearchYear = null, int? searchMactId = null, string? mfaSearchNo = null, int? mfaSearchYear = null)
        {
            var divIdString = User.FindFirstValue("DivisionID") ?? "0";
            int divisionId = int.TryParse(divIdString, out int id) ? id : 0;
            
            ViewBag.MACTs = _masterRepo.GetAllMACTs() ?? new List<MACT>();
            if (divisionId == 5) divisionId = 0;

            // 1. Prioritize MFA Search
            if (!string.IsNullOrEmpty(mfaSearchNo) && mfaSearchYear.HasValue)
            {
                var appeal = _appealRepo.GetAppealByMFADetails(mfaSearchNo, mfaSearchYear.Value);
                if (appeal != null)
                {
                    return RedirectToAction("ManageClaimant", new { id = appeal.CaseID });
                }
                else
                {
                    // If no existing appeal, we might need an MVC case to register it correctly
                    // However, if we allow "Old cases without MVC No", we need a way to handle it.
                    // For now, let's just warn if the specific MFA record isn't found.
                    TempData["ErrorMessage"] = "No registered claimant appeal found with the specified MFA details.";
                }
            }
            // 2. Secondary: MVC Search (For New Registrations)
            else if (!string.IsNullOrEmpty(mvcSearchNo) && mvcSearchYear.HasValue && searchMactId.HasValue)
            {
                var mvcCase = _caseRepo.GetCaseByMVCDetails(mvcSearchNo, mvcSearchYear.Value, searchMactId.Value);
                if (mvcCase != null)
                {
                    return RedirectToAction("ManageClaimant", new { id = mvcCase.CaseID });
                }
                else
                {
                    TempData["ErrorMessage"] = "No MVC registration found with the specified details.";
                }
            }

            var list = _appealRepo.GetAllClaimantAppeals(divisionId);
            return View(list);
        }

        [HttpGet]
        public IActionResult ViewClaimant(int id)
        {
            var caseDetails = _caseRepo.GetCaseById(id);
            if (caseDetails == null) return NotFound();

            var model = _appealRepo.GetAppealByCaseId(id);
            if (model == null)
            {
                TempData["ErrorMessage"] = "No claimant appeal found for this case.";
                return RedirectToAction("ClaimantList");
            }

            model.MVCNo = caseDetails.MVCNo ?? "";
            model.MVCYear = caseDetails.MVCYear;
            model.DivisionName = caseDetails.DivisionName ?? "";
            model.MACTName = caseDetails.MACTName ?? "";

            return View(model);
        }

        [HttpGet]
        public IActionResult ManageClaimant(int id)
        {
            IEnumerable<Division> divisions = _masterRepo.GetAllDivisions() ?? new List<Division>();
            ViewBag.Divisions = divisions;
            
            ViewBag.HCAdvocates = _masterRepo.GetHighCourtAdvocates() ?? new List<HighCourtAdvocate>();

            var caseDetails = _caseRepo.GetCaseById(id);
            if (caseDetails == null) return NotFound();

            var model = _appealRepo.GetAppealByCaseId(id);
            if (model == null)
            {
                model = new AppealViewModel { CaseID = id };
            }

            model.MVCNo = caseDetails.MVCNo ?? "";
            model.MVCYear = caseDetails.MVCYear;
            model.DivisionName = caseDetails.DivisionName ?? "";
            model.MACTName = caseDetails.MACTName ?? "";

            // Auto-populate if empty
            if (string.IsNullOrEmpty(model.ClaimantMVCNumber))
            {
                model.ClaimantMVCNumber = caseDetails.MVCNo;
                model.ClaimantMVCYear = caseDetails.MVCYear;
                model.ClaimantDivisionID = caseDetails.DivisionID;
                model.ClaimantMVCCurrentStatus = !string.IsNullOrEmpty(caseDetails.DisposalResult) ? caseDetails.DisposalResult : (caseDetails.CurrentStage ?? "Pending");
            }

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult SaveClaimant(AppealViewModel model)
        {
            // Support for cases without pre-existing MVC record
            if (model.CaseID == 0 && !string.IsNullOrEmpty(model.ClaimantMVCNumber) && model.MACTID.HasValue)
            {
                var divIdString = User.FindFirstValue("DivisionID") ?? "0";
                int currentDiv = int.TryParse(divIdString, out int id) ? id : 0;
                int targetDiv = (model.ClaimantDivisionID ?? 0) > 0 ? model.ClaimantDivisionID.Value : (currentDiv > 0 && currentDiv != 5 ? currentDiv : 1); // Fallback to 1 if no div

                model.CaseID = _caseRepo.CreateSkeletonCase(model.ClaimantMVCNumber, model.ClaimantMVCYear ?? DateTime.Now.Year, model.MACTID.Value, targetDiv);
            }

            if (model.CaseID == 0)
            {
                TempData["ErrorMessage"] = "Could not link to an MVC case. Please search or enter MVC details correctly.";
                return RedirectToAction("Register");
            }

            // Handle File Uploads
            if (model.InitialActionFile1 != null) model.InitialActionPath1 = SaveFile(model.InitialActionFile1, "appeals/initial");
            if (model.InitialActionFile2 != null) model.InitialActionPath2 = SaveFile(model.InitialActionFile2, "appeals/initial");
            if (model.ApprovalCopyFile != null) model.ApprovalCopyPath = SaveFile(model.ApprovalCopyFile, "appeals/approval");
            if (model.MFAJudgmentCopyFile != null) model.MFAJudgmentCopyPath = SaveFile(model.MFAJudgmentCopyFile, "appeals/mfa_judgment");

            if (model.CorpMFAActionTakenFile != null) model.CorpMFAActionTakenPath = SaveFile(model.CorpMFAActionTakenFile, "appeals/outcome_approval");
            if (model.ClaimantSCJudgmentFile != null) model.ClaimantSCJudgmentPath = SaveFile(model.ClaimantSCJudgmentFile, "appeals/sc_judgment");

            // Handle "Others" Bench
            if (model.HighCourtBench == "Others" && !string.IsNullOrEmpty(model.OtherHighCourtBench))
            {
                model.HighCourtBench = model.OtherHighCourtBench;
            }

            var existing = _appealRepo.GetAppealByCaseId(model.CaseID);
            if (existing != null)
            {
                // Update file paths only if new files were uploaded
                if (!string.IsNullOrEmpty(model.InitialActionPath1)) existing.InitialActionPath1 = model.InitialActionPath1;
                if (!string.IsNullOrEmpty(model.InitialActionPath2)) existing.InitialActionPath2 = model.InitialActionPath2;
                if (!string.IsNullOrEmpty(model.ApprovalCopyPath)) existing.ApprovalCopyPath = model.ApprovalCopyPath;
                if (!string.IsNullOrEmpty(model.MFAJudgmentCopyPath)) existing.MFAJudgmentCopyPath = model.MFAJudgmentCopyPath;
                if (!string.IsNullOrEmpty(model.CorpMFAActionTakenPath)) existing.CorpMFAActionTakenPath = model.CorpMFAActionTakenPath;
                if (!string.IsNullOrEmpty(model.ClaimantSCJudgmentPath)) existing.ClaimantSCJudgmentPath = model.ClaimantSCJudgmentPath;

                // Update root fields
                existing.FeasibilityReceived = model.FeasibilityReceived;
                existing.FeasibilityReceiptDate = model.FeasibilityReceiptDate;
                existing.InitialAction = model.InitialAction;
                existing.InitialActionRemarks = model.InitialActionRemarks;
                existing.HighCourtBench = model.HighCourtBench;
                existing.OtherHighCourtBench = model.OtherHighCourtBench;

                // Update claimant fields
                existing.ClaimantDivisionID = model.ClaimantDivisionID;
                existing.ClaimantMVCNumber = model.ClaimantMVCNumber;
                existing.ClaimantMVCYear = model.ClaimantMVCYear;
                existing.ClaimantMVCCurrentStatus = model.ClaimantMVCCurrentStatus;
                existing.ClaimantMFANumber = model.ClaimantMFANumber;
                existing.ClaimantMFAYear = model.ClaimantMFAYear;
                if (!string.IsNullOrWhiteSpace(model.ClaimantMFACNRNumber))
                {
                    existing.ClaimantMFACNRNumber = model.ClaimantMFACNRNumber.Trim().ToUpperInvariant();
                }
                existing.ClaimantMFAEntrustmentNo = model.ClaimantMFAEntrustmentNo;
                existing.ClaimantMFAEntrustmentDate = model.ClaimantMFAEntrustmentDate;
                existing.ClaimantMFAAdvocate = model.ClaimantMFAAdvocate;
                existing.ClaimantMFAStatus = model.ClaimantMFAStatus;
                existing.ClaimantMFADecision = model.ClaimantMFADecision;
                existing.ClaimantActionTaken = model.ClaimantActionTaken;
                existing.ClaimantApprovalNo = model.ClaimantApprovalNo;
                existing.ClaimantApprovalDate = model.ClaimantApprovalDate;
                
                existing.IsClaimantSCAppeal = model.IsClaimantSCAppeal;
                existing.IsClaimantSCPending = model.IsClaimantSCPending;
                existing.ClaimantSCNumber = model.ClaimantSCNumber;
                existing.ClaimantSCDiaryNumber = model.ClaimantSCDiaryNumber;
                existing.ClaimantSCYear = model.ClaimantSCYear;
                existing.ClaimantSLPYear = model.ClaimantSLPYear;
                existing.ClaimantSCFiledBy = model.ClaimantSCFiledBy;
                existing.ClaimantSCEntrustmentNo = model.ClaimantSCEntrustmentNo;
                existing.ClaimantSCEntrustmentDate = model.ClaimantSCEntrustmentDate;
                existing.ClaimantSCAdvocate = model.ClaimantSCAdvocate;
                existing.ClaimantSCStatus = model.ClaimantSCStatus;
                existing.ClaimantSCOutcome = model.ClaimantSCOutcome;
                existing.ClaimantSCActionTaken = model.ClaimantSCActionTaken;
                existing.ClaimantSCClosureNo = model.ClaimantSCClosureNo;
                existing.ClaimantSCClosureDate = model.ClaimantSCClosureDate;
                existing.ClaimantSCJudgmentPath = model.ClaimantSCJudgmentPath;

                _appealRepo.SaveAppeal(existing);
            }
            else 
            {
                _appealRepo.SaveAppeal(model);
            }

            _ = _activityLogger.LogCaseUpdatedAsync(
                "APPEAL",
                model.CaseID,
                $"Claimant MFA {model.ClaimantMFANumber}/{model.ClaimantMFAYear}",
                null,
                model.HighCourtBench ?? "High Court",
                null,
                model,
                $"Claimant Appeal MFA #{model.ClaimantMFANumber}/{model.ClaimantMFAYear} registered/updated (Advocate: {model.ClaimantMFAAdvocate}, Status: {model.ClaimantMFAStatus})");
            
            TempData["SuccessMessage"] = "Claimant Appeal registered/updated successfully.";
            return RedirectToAction("ClaimantList");
        }

        private string? SaveFile(IFormFile? file, string folder)
        {
            if (file == null || file.Length == 0) return null;

            var uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads", folder);
            if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

            var uniqueFileName = $"{Guid.NewGuid()}_{file.FileName}";
            var filePath = Path.Combine(uploadsFolder, uniqueFileName);

            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                file.CopyTo(fileStream);
            }

            return $"/uploads/{folder}/{uniqueFileName}";
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult SaveCompliance(int caseId, DateTime complianceDate, decimal amount, string chequeDetails)
        {
            try 
            {
                var appeal = _appealRepo.GetAppealByCaseId(caseId);
                if (appeal == null)
                {
                     return Json(new { success = false, message = "Appeal Details not found. Please ensure the case has entered the appeal workflow." });
                }

                appeal.FinalComplianceDate = complianceDate;
                appeal.AmountDeposited = amount;
                appeal.FinalComplianceStatus = "Complied";
                
                string remark = $"Cheque/Ref No: {chequeDetails}";
                if (!string.IsNullOrEmpty(appeal.FinalRemarks))
                    appeal.FinalRemarks += " | " + remark;
                else
                    appeal.FinalRemarks = remark;

                _appealRepo.SaveAppeal(appeal);

                _ = _activityLogger.LogSubEntityActionAsync(
                    "APPEAL",
                    caseId,
                    $"Appeal #{caseId}",
                    "COMPLIANCE_RECORDED",
                    $"Appeal Final Compliance recorded: Amount ₹{amount:N2}, Date: {complianceDate:dd/MM/yyyy}, Cheque/Ref: {chequeDetails}",
                    new { caseId, complianceDate, amount, chequeDetails });

                var userName = User.Identity?.Name ?? "User";
                return Json(new { success = true, message = $"Compliance details saved successfully by {userName}." });
            }
            catch(Exception ex)
            {
                return Json(new { success = false, message = "Error saving: " + ex.Message });
            }
        }
    }
}
