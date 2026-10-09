using Microsoft.AspNetCore.Mvc;
using MVCCaseManagement.Models;
using MVCCaseManagement.DAL;
using MVCCaseManagement.Utils;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

using Microsoft.Extensions.Logging;
using System.Linq;

namespace MVCCaseManagement.Controllers
{
    [Authorize]
    public class CaseController : Controller
    {
        private readonly IMasterRepository _masterRepo;
        private readonly ICaseRepository _caseRepo;
        private readonly ICasePaymentRepository _paymentRepo;
        private readonly IAppealRepository _appealRepo;
        private readonly IWebHostEnvironment _environment;
        private readonly ITR18Service _tr18Service;
        private readonly IECourtsNapixService _napixService;
        private readonly IECourtsRepository _ecourtsRepo;
        private readonly ICaseNotingRepository _notingRepo;
        private readonly MVCCaseManagement.Services.Audit.ICaseActivityLogger _activityLogger;
        private readonly ILogger<CaseController> _logger;

        public CaseController(
            IMasterRepository masterRepo,
            ICaseRepository caseRepo,
            ICasePaymentRepository paymentRepo,
            IAppealRepository appealRepo,
            IWebHostEnvironment environment,
            ITR18Service tr18Service,
            IECourtsNapixService napixService,
            IECourtsRepository ecourtsRepo,
            ICaseNotingRepository notingRepo,
            MVCCaseManagement.Services.Audit.ICaseActivityLogger activityLogger,
            ILogger<CaseController> logger)
        {
            _masterRepo = masterRepo;
            _caseRepo = caseRepo;
            _paymentRepo = paymentRepo;
            _appealRepo = appealRepo;
            _environment = environment;
            _tr18Service = tr18Service;
            _napixService = napixService;
            _ecourtsRepo = ecourtsRepo;
            _notingRepo = notingRepo;
            _activityLogger = activityLogger;
            _logger = logger;
        }

        public IActionResult Index(int page = 1, string search = "", string status = "all")
        {
            int pageSize = 10;
            
            var divIdString = User.FindFirst("DivisionID")?.Value;
            bool isCentralOffice = string.IsNullOrEmpty(divIdString) || divIdString == "0" || divIdString == "5";
            int divisionId = int.TryParse(divIdString, out int id) ? id : 0;

            // REQ: Differentiated default views for CLO, MD, and other CO users
            if (isCentralOffice && !string.IsNullOrEmpty(search))
            {
                status = "all";
            }
            else if (isCentralOffice && status == "all" && !Request.Query.ContainsKey("status"))
            {
                if (User.IsInRole("CLO") || User.IsInRole("Dy CLO") || User.IsInRole("DyCLO"))
                {
                    status = "PendingDecision";
                }
                else if (User.IsInRole("MD"))
                {
                    status = "PendingAtCA";
                }
                else
                {
                    status = "SentToCO";
                }
            }
            else if (isCentralOffice && status == "all")
            {
                status = "SentToCO_All";
            }

            var stats = _caseRepo.GetDashboardStats(divisionId);
            var cases = _caseRepo.GetAllCases(divisionId, page, pageSize, search, status);
            var totalCount = _caseRepo.GetTotalCaseCount(divisionId, search, status);
            var recentTransfers = _caseRepo.GetRecentTransfers(divisionId);

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = (int)Math.Ceiling((double)totalCount / pageSize);
            ViewBag.Stats = stats;
            ViewBag.RecentTransfers = recentTransfers;
            ViewBag.CurrentSearch = search;
            ViewBag.CurrentStatus = status;

            return View(cases);
        }

        public IActionResult RemindBack(int page = 1, string search = "")
        {
            int pageSize = 10;
            var divIdString = User.FindFirstValue("DivisionID");
            int divisionId = int.TryParse(divIdString, out int id) ? id : 0;
            
            var cases = _caseRepo.GetAllRemindBackCases(divisionId, page, pageSize, search);
            var totalCount = _caseRepo.GetTotalRemindBackCaseCount(divisionId, search);

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = (int)Math.Ceiling((double)totalCount / pageSize);
            ViewBag.CurrentSearch = search;
            ViewBag.TotalCount = totalCount; // Pass total for the stat card

            return View(cases);
        }

        [HttpGet]
        public IActionResult ThirdPartyList(int page = 1, string search = "")
        {
            int pageSize = 10;
            var divIdString = User.FindFirstValue("DivisionID");
            int divisionId = int.TryParse(divIdString, out int id) ? id : 0;

            var cases = _caseRepo.GetAllThirdPartyCases(divisionId, page, pageSize, search);
            var totalCount = _caseRepo.GetTotalThirdPartyCaseCount(divisionId, search);

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = (int)Math.Ceiling((double)totalCount / pageSize);
            ViewBag.CurrentSearch = search;
            ViewBag.TotalCount = totalCount;

            return View(cases);
        }

        [HttpGet]
        public IActionResult RegisterThirdParty()
        {
            var divIdString = User.FindFirstValue("DivisionID");
            int divisionId = int.TryParse(divIdString, out int id) ? id : 0;
            if (User.IsInRole("Admin") && divisionId == 0) divisionId = 1;

            if (divisionId == 0 || (divisionId == 5 && !User.IsInRole("Admin"))) return Forbid();

            var divisions = _masterRepo.GetAllDivisions();
            ViewBag.Divisions = divisions;
            ViewBag.UserDivisionName = divisions.FirstOrDefault(d => d.DivisionID == divisionId)?.DivisionNameEnglish;
            ViewBag.MACTs = _masterRepo.GetAllMACTs();
            ViewBag.Advocates = _masterRepo.GetAllAdvocates();

            return View(new MVCCaseViewModel { DivisionID = divisionId, ThirdPartyFlag = true });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult RegisterThirdParty(MVCCaseViewModel model)
        {
            var divIdString = User.FindFirstValue("DivisionID");
            int divisionId = int.TryParse(divIdString, out int id) ? id : 0;
            if (User.IsInRole("Admin") && divisionId == 0) divisionId = model.DivisionID > 0 ? model.DivisionID : 1;
            model.DivisionID = divisionId;
            model.ThirdPartyFlag = true;

            if (model.IsPendingForFiling)
            {
                ModelState.Remove("MVCNo");
                ModelState.Remove("MVCYear");
                if (string.IsNullOrEmpty(model.MVCNo) || model.MVCNo == "0") model.MVCNo = "Pending";
                if (model.MVCYear == 0) model.MVCYear = DateTime.Now.Year;
            }

            // Extract EstCode from CNRNumber if not already set
            if (!string.IsNullOrWhiteSpace(model.CNRNumber))
            {
                model.CNRNumber = model.CNRNumber.Trim().ToUpperInvariant();
                if (string.IsNullOrWhiteSpace(model.EstCode) && model.CNRNumber.Length >= 6)
                {
                    model.EstCode = model.CNRNumber.Substring(0, 6);
                }
            }

            model.MACTID = ResolveMactId(model.EstCode, model.MACTID);
            ModelState.Remove("MACTID");
            ModelState.Remove("EstCode");
            ModelState.Remove("CaseTypeCode");

            if (ModelState.IsValid)
            {
                try
                {
                    // Handle File Uploads
                    if (model.InterimOrderFile != null)
                    {
                        model.InterimOrderFilePath = SaveFile(model.InterimOrderFile, "thirdparty/interim");
                    }

                    if ((model.DisposalResult == "Against" || model.DisposalResult == "Favor") && model.AdverseAward?.AdverseJudgmentFile != null)
                    {
                        model.AdverseAward.AdverseJudgmentUploadPath = SaveFile(model.AdverseAward.AdverseJudgmentFile, "cases");
                    }

                    // Clean & sanitize CNR Number
                    if (!string.IsNullOrWhiteSpace(model.CNRNumber))
                    {
                        model.CNRNumber = model.CNRNumber.Trim().ToUpperInvariant();
                    }

                    // eCourts Auto-CNR Discovery Fallback
                    if (string.IsNullOrEmpty(model.CNRNumber) && !string.IsNullOrEmpty(model.EstCode))
                    {
                        try
                        {
                            string caseTypeCode = string.IsNullOrEmpty(model.CaseTypeCode) ? "83" : model.CaseTypeCode;
                            var discoveredCnr = _napixService.DiscoverCnrAsync(model.EstCode, caseTypeCode, model.MVCNo, model.MVCYear.ToString()).GetAwaiter().GetResult();
                            if (!string.IsNullOrEmpty(discoveredCnr))
                            {
                                model.CNRNumber = discoveredCnr.Trim().ToUpperInvariant();
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "CNR Auto-Discovery fallback threw exception during Third Party case registration.");
                        }
                    }

                    // Ensure tracked case is saved for live synchronization
                    if (!string.IsNullOrEmpty(model.CNRNumber))
                    {
                        try
                        {
                            _ecourtsRepo.SaveTrackedCaseAsync(new MVCCaseManagement.DAL.TrackedCaseModel
                            {
                                CNRNumber = model.CNRNumber,
                                EstCode = model.EstCode ?? (model.CNRNumber.Length >= 5 ? model.CNRNumber.Substring(0, 5) : ""),
                                CaseTypeCode = model.CaseTypeCode ?? "83",
                                RegNo = model.MVCNo,
                                RegYear = model.MVCYear
                            }).GetAwaiter().GetResult();
                        }
                        catch { }
                    }

                    int newCaseId = _caseRepo.SaveCase(model);
                    _ = _activityLogger.LogCaseCreatedAsync("MVC", newCaseId, $"MVC {model.MVCNo}/{model.MVCYear}", model.VehicleNo, null, model, $"Registered new Third Party Case MVC {model.MVCNo}/{model.MVCYear}");

                    TempData["SuccessMessage"] = $"Third Party Case MVC No. {model.MVCNo}/{model.MVCYear} has been registered successfully.";
                    return RedirectToAction("Index");
                }
                catch (Exception ex)
                {
                    TempData["ErrorMessage"] = "Error registering third party case. " + ex.Message;
                    _logger.LogError(ex, "Error in RegisterThirdParty POST for Division {DivisionID}", divisionId);
                }
            }

            // Re-populate dropdowns on error
            var divisions = _masterRepo.GetAllDivisions();
            ViewBag.Divisions = divisions;
            ViewBag.UserDivisionName = divisions.FirstOrDefault(d => d.DivisionID == divisionId)?.DivisionNameEnglish;
            ViewBag.MACTs = _masterRepo.GetAllMACTs();
            ViewBag.Advocates = _masterRepo.GetAllAdvocates();
            return View(model);
        }

        [HttpGet]
        public IActionResult Create()
        {
            var divIdString = User.FindFirstValue("DivisionID");
            int divisionId = int.TryParse(divIdString, out int id) ? id : 0;
            if (User.IsInRole("Admin") && divisionId == 0) divisionId = 1;
            
            // Block Central Office (0 or 5)
            if (divisionId == 0 || (divisionId == 5 && !User.IsInRole("Admin")))
            {
                return Forbid(); // or RedirectToAction("Index")
            }

            // Fetch live data from Database
            var divisions = _masterRepo.GetAllDivisions();
            ViewBag.Divisions = divisions;
            var userDiv = divisions.FirstOrDefault(d => d.DivisionID == divisionId);
            ViewBag.UserDivisionName = userDiv?.DivisionNameEnglish;
            ViewBag.MACTs = _masterRepo.GetAllMACTs();
            ViewBag.Advocates = _masterRepo.GetAllAdvocates();

            // Pre-select Division in Model
            var model = new MVCCaseViewModel { DivisionID = divisionId };
            return View(model);
        }

        [HttpGet]
        public IActionResult RemindBackEntry()
        {
            var divIdString = User.FindFirstValue("DivisionID");
            int divisionId = int.TryParse(divIdString, out int id) ? id : 0;
            if (User.IsInRole("Admin") && divisionId == 0) divisionId = 1;

            if (divisionId == 0 || (divisionId == 5 && !User.IsInRole("Admin"))) return Forbid();

            // Fetch Master Data for the view (copied from Create)
            var divisions = _masterRepo.GetAllDivisions();
            ViewBag.Divisions = divisions;
            var userDiv = divisions.FirstOrDefault(d => d.DivisionID == divisionId);
            ViewBag.UserDivisionName = userDiv?.DivisionNameEnglish;
            ViewBag.MACTs = _masterRepo.GetAllMACTs();
            ViewBag.Advocates = _masterRepo.GetAllAdvocates();

            var model = new MVCCaseViewModel { DivisionID = divisionId };
            return View(model);
        }

        [HttpGet]
        public IActionResult GetCaseJson(string mvcNo, int mvcYear, int mactId)
        {
             var divIdString = User.FindFirstValue("DivisionID");
             int divisionId = int.TryParse(divIdString, out int id) ? id : 0;
             
             var kase = _caseRepo.GetCaseByMvcDetails(divisionId, mvcNo, mvcYear, mactId) ?? _caseRepo.GetCaseByMVCDetails(mvcNo, mvcYear, mactId);
             if (kase == null) return Json(new { success = false, message = "Case not found." });
             
             return Json(new { success = true, data = kase });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(MVCCaseViewModel model)
        {
            var divIdString = User.FindFirstValue("DivisionID");
            int divisionId = int.TryParse(divIdString, out int id) ? id : 0;
            if (User.IsInRole("Admin") && divisionId == 0) divisionId = model.DivisionID > 0 ? model.DivisionID : 1;
            model.DivisionID = divisionId;

            if (model.IsPendingForFiling)
            {
                ModelState.Remove("MVCNo");
                ModelState.Remove("MVCYear");
                if (string.IsNullOrEmpty(model.MVCNo) || model.MVCNo == "0") model.MVCNo = "Pending";
                if (model.MVCYear == 0) model.MVCYear = DateTime.Now.Year;
            }

            // Extract EstCode from CNRNumber if not already set
            if (!string.IsNullOrWhiteSpace(model.CNRNumber))
            {
                model.CNRNumber = model.CNRNumber.Trim().ToUpperInvariant();
                if (string.IsNullOrWhiteSpace(model.EstCode) && model.CNRNumber.Length >= 6)
                {
                    model.EstCode = model.CNRNumber.Substring(0, 6);
                }
            }

            model.MACTID = ResolveMactId(model.EstCode, model.MACTID);
            ModelState.Remove("MACTID");
            ModelState.Remove("EstCode");
            ModelState.Remove("CaseTypeCode");

            if (ModelState.IsValid)
            {
                try
                {
                    // Handle File Uploads (Only if new files are uploaded)
                    if (model.AdverseAward == null) model.AdverseAward = new AdverseAwardViewModel();

                    if (model.AdverseAward.SecurityDocFile != null)
                        model.AdverseAward.SecurityUploadPath = SaveFile(model.AdverseAward.SecurityDocFile, "cases");
                    if (model.AdverseAward.IAFile != null)
                        model.AdverseAward.DelayApplicationPath = SaveFile(model.AdverseAward.IAFile, "cases");
                    if (model.AdverseAward.DelayOrderFile != null)
                        model.AdverseAward.DelayCondonedOrderPath = SaveFile(model.AdverseAward.DelayOrderFile, "cases");

                    if (model.DisposalResult == "Against")
                    {
                        if (model.AdverseAward.TR18File != null)
                            model.AdverseAward.TR18UploadPath = SaveFile(model.AdverseAward.TR18File, "cases");
                        if (model.AdverseAward.PunishmentOrderFile != null)
                            model.AdverseAward.PunishmentOrderUploadPath = SaveFile(model.AdverseAward.PunishmentOrderFile, "cases");
                        if (model.AdverseAward.GovIDProofFile != null)
                            model.AdverseAward.GovIDProofUploadPath = SaveFile(model.AdverseAward.GovIDProofFile, "cases");
                        if (model.AdverseAward.AdverseJudgmentFile != null)
                            model.AdverseAward.AdverseJudgmentUploadPath = SaveFile(model.AdverseAward.AdverseJudgmentFile, "cases");
                        if (model.AdverseAward.AgeProofFile != null)
                            model.AdverseAward.AgeProofUploadPath = SaveFile(model.AdverseAward.AgeProofFile, "cases");
                    }

                    // Synchronize ClaimPetitionDate and IsAllegedAccident
                    if (model.ClaimPetitionDate == null && model.AdverseAward?.ClaimPetitionDate != null)
                        model.ClaimPetitionDate = model.AdverseAward.ClaimPetitionDate;
                    else if (model.AdverseAward != null && model.AdverseAward.ClaimPetitionDate == null && model.ClaimPetitionDate != null)
                        model.AdverseAward.ClaimPetitionDate = model.ClaimPetitionDate;

                    if (model.AdverseAward != null)
                    {
                        model.AdverseAward.IsAllegedAccident = model.IsAllegedAccident || model.AdverseAward.IsAllegedAccident;
                        model.IsAllegedAccident = model.AdverseAward.IsAllegedAccident;
                    }

                    // Clean & sanitize CNR Number
                    if (!string.IsNullOrWhiteSpace(model.CNRNumber))
                    {
                        model.CNRNumber = model.CNRNumber.Trim().ToUpperInvariant();
                    }

                    // eCourts Auto-CNR Discovery Fallback if CNR not provided
                    if (string.IsNullOrEmpty(model.CNRNumber) && !string.IsNullOrEmpty(model.EstCode))
                    {
                        try
                        {
                            string caseTypeCode = string.IsNullOrEmpty(model.CaseTypeCode) ? "83" : model.CaseTypeCode;
                            var discoveredCnr = _napixService.DiscoverCnrAsync(model.EstCode, caseTypeCode, model.MVCNo, model.MVCYear.ToString()).GetAwaiter().GetResult();
                            if (!string.IsNullOrEmpty(discoveredCnr))
                            {
                                model.CNRNumber = discoveredCnr.Trim().ToUpperInvariant();
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "CNR Auto-Discovery fallback threw exception during MVC case creation.");
                        }
                    }

                    // Register into eCourts Tracked Cases table if CNR is present
                    if (!string.IsNullOrEmpty(model.CNRNumber))
                    {
                        try
                        {
                            string caseTypeCode = string.IsNullOrEmpty(model.CaseTypeCode) ? "83" : model.CaseTypeCode;
                            _ecourtsRepo.SaveTrackedCaseAsync(new MVCCaseManagement.DAL.TrackedCaseModel
                            {
                                CNRNumber = model.CNRNumber,
                                EstCode = model.EstCode,
                                CaseTypeCode = caseTypeCode,
                                RegNo = model.MVCNo,
                                RegYear = model.MVCYear
                            }).GetAwaiter().GetResult();
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to register tracked case for CNR {CNR}", model.CNRNumber);
                        }
                    }

                    int newCaseId = _caseRepo.SaveCase(model);
                    _ = _activityLogger.LogCaseCreatedAsync("MVC", newCaseId, $"MVC {model.MVCNo}/{model.MVCYear}", model.VehicleNo, null, model);

                    if (model.LinkedAppeal != null)
                    {
                        model.LinkedAppeal.CaseID = newCaseId;
                        _appealRepo.SaveAppeal(model.LinkedAppeal);
                    }

                    TempData["SuccessMessage"] = $"Case MVC No. {model.MVCNo}/{model.MVCYear} has been saved successfully.";
                    return RedirectToAction("Index");
                }
                catch (Exception ex)
                {
                    TempData["ErrorMessage"] = "Error creating case. Please try again. " + ex.Message;
                    _logger.LogError(ex, "Error creating case for DivisionID {DivisionID}", divisionId);
                    
                    // Re-populate dropdowns on error
                    var divisions = _masterRepo.GetAllDivisions();
                    ViewBag.Divisions = divisions;
                    ViewBag.UserDivisionName = divisions.FirstOrDefault(d => d.DivisionID == divisionId)?.DivisionNameEnglish;
                    ViewBag.MACTs = _masterRepo.GetAllMACTs();
                    ViewBag.Advocates = _masterRepo.GetAllAdvocates();
                    return View(model);
                }
            }

            // Re-populate dropdowns on validation error
            var valDivisions = _masterRepo.GetAllDivisions();
            ViewBag.Divisions = valDivisions;
            ViewBag.UserDivisionName = valDivisions.FirstOrDefault(d => d.DivisionID == divisionId)?.DivisionNameEnglish;
            ViewBag.MACTs = _masterRepo.GetAllMACTs();
            ViewBag.Advocates = _masterRepo.GetAllAdvocates();
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult SaveRemindBack(MVCCaseViewModel model)
        {
             var divIdString = User.FindFirstValue("DivisionID");
             int divisionId = int.TryParse(divIdString, out int id) ? id : 0;
             if (User.IsInRole("Admin") && divisionId == 0) divisionId = model.DivisionID > 0 ? model.DivisionID : 1;
             model.DivisionID = divisionId;

             try
             {
                 // Handle File Uploads
                 if (model.DisposalResult == "Against")
                 {
                     model.AdverseAward.TR18UploadPath = SaveFile(model.AdverseAward.TR18File, "remindback");
                     model.AdverseAward.PunishmentOrderUploadPath = SaveFile(model.AdverseAward.PunishmentOrderFile, "remindback");
                     model.AdverseAward.SecurityUploadPath = SaveFile(model.AdverseAward.SecurityDocFile, "remindback");
                     model.AdverseAward.GovIDProofUploadPath = SaveFile(model.AdverseAward.GovIDProofFile, "remindback");
                     model.AdverseAward.AdverseJudgmentUploadPath = SaveFile(model.AdverseAward.AdverseJudgmentFile, "remindback");
                     model.AdverseAward.DelayApplicationPath = SaveFile(model.AdverseAward.IAFile, "remindback");
                     model.AdverseAward.DelayCondonedOrderPath = SaveFile(model.AdverseAward.DelayOrderFile, "remindback");
                     model.AdverseAward.AgeProofUploadPath = SaveFile(model.AdverseAward.AgeProofFile, "remindback");
                 }

                 // Clean & sanitize CNR Number
                 if (!string.IsNullOrWhiteSpace(model.CNRNumber))
                 {
                     model.CNRNumber = model.CNRNumber.Trim().ToUpperInvariant();
                 }

                 // eCourts Auto-CNR Discovery Fallback
                 if (string.IsNullOrEmpty(model.CNRNumber) && !string.IsNullOrEmpty(model.EstCode))
                 {
                     try
                     {
                         string caseTypeCode = string.IsNullOrEmpty(model.CaseTypeCode) ? "83" : model.CaseTypeCode;
                         var discoveredCnr = _napixService.DiscoverCnrAsync(model.EstCode, caseTypeCode, model.MVCNo, model.MVCYear.ToString()).GetAwaiter().GetResult();
                         if (!string.IsNullOrEmpty(discoveredCnr))
                         {
                             model.CNRNumber = discoveredCnr.Trim().ToUpperInvariant();
                         }
                     }
                     catch (Exception ex)
                     {
                         _logger.LogWarning(ex, "CNR Auto-Discovery fallback threw exception during Remand Back case registration.");
                     }
                 }

                 // Ensure tracked case is saved for live synchronization
                 if (!string.IsNullOrEmpty(model.CNRNumber))
                 {
                     try
                     {
                         _ecourtsRepo.SaveTrackedCaseAsync(new MVCCaseManagement.DAL.TrackedCaseModel
                         {
                             CNRNumber = model.CNRNumber,
                             EstCode = model.EstCode ?? (model.CNRNumber.Length >= 5 ? model.CNRNumber.Substring(0, 5) : ""),
                             CaseTypeCode = model.CaseTypeCode ?? "83",
                             RegNo = model.MVCNo,
                             RegYear = model.MVCYear
                         }).GetAwaiter().GetResult();
                     }
                     catch { }
                 }

                 int rbId = _caseRepo.SaveRemindBackCase(model);
                 if (rbId > 0)
                 {
                     _ = _activityLogger.LogCaseCreatedAsync("MVC", rbId, $"MVC {model.MVCNo}/{model.MVCYear}", model.VehicleNo, null, model, $"Registered Remand Back Case MVC {model.MVCNo}/{model.MVCYear}");
                     return Json(new { success = true, message = "Remand Back details saved successfully to the separate Remand Back table.", id = rbId });
                 }
                 return Json(new { success = false, message = "Failed to save Remand Back details." });
             }
             catch (Exception ex)
             {
                 return Json(new { success = false, message = "Error: " + ex.Message });
             }
        }

        [HttpGet]
        public async Task<IActionResult> Details(int id, bool isRemindBack = false)
        {
            var model = isRemindBack ? _caseRepo.GetRemindBackCaseById(id) : _caseRepo.GetCaseById(id);
            if (model == null) return NotFound();

            // Mark as viewed for Central Office users
            var divIdString = User.FindFirst("DivisionID")?.Value;
            var roleUpper = (User.FindFirst(ClaimTypes.Role)?.Value ?? "").ToUpperInvariant();
            bool isCentralOffice = string.IsNullOrEmpty(divIdString) || divIdString == "0" || divIdString == "5"
                                   || roleUpper.Contains("MD") || roleUpper.Contains("CLO") || roleUpper.Contains("LO")
                                   || roleUpper.Contains("ADMIN") || roleUpper.Contains("CENTRAL");
            
            ViewBag.IsCentralOffice = isCentralOffice;
            ViewBag.IsCLO = User.IsInRole("CLO") || User.IsInRole("Dy CLO") || User.IsInRole("DyCLO");
            ViewBag.IsRemindBack = isRemindBack;

            if (isCentralOffice && !isRemindBack)
            {
                _caseRepo.MarkCaseAsViewed(id);
            }

            // Fetch TR-18 Accident Details if possible
            if (!string.IsNullOrEmpty(model.VehicleNo) && model.AccidentDate.HasValue)
            {
                try
                {
                    var tr18Response = await _tr18Service.GetAccidentDetails(model.AccidentDate, model.VehicleNo);
                    if (tr18Response != null && tr18Response.Success)
                    {
                        ViewBag.TR18Data = tr18Response.Data;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "TR-18 Fetch Error for Case {CaseID}", id);
                }
            }

            ViewBag.Divisions = _masterRepo.GetAllDivisions();
            ViewBag.MACTs = _masterRepo.GetAllMACTs();
            ViewBag.Advocates = _masterRepo.GetAllAdvocates();
            ViewBag.CaseNotings = isCentralOffice ? _notingRepo.GetNotings("MVC", id) : new List<MVCCaseManagement.Models.CaseNoting>();
            return View("Details", model);
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
                CaseType = "MVC",
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
                _ = _activityLogger.LogSubEntityActionAsync("MVC", dto.CaseID, $"Case #{dto.CaseID}", "NOTING_ADDED", $"Added case noting: {(dto.NotingText.Length > 80 ? dto.NotingText.Substring(0, 77) + "..." : dto.NotingText)}");
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
                _ = _activityLogger.LogSubEntityActionAsync("MVC", existing.CaseID, $"Case #{existing.CaseID}", "NOTING_DELETED", $"Deleted noting #{dto.NotingID}");
            }
            return Json(new { success = deleted, message = deleted ? "Noting deleted successfully." : "Failed to delete noting." });
        }

        [HttpGet]
        public async Task<IActionResult> RemindBackDetails(int id)
        {
            return await Details(id, isRemindBack: true);
        }

        [HttpGet]
        public async Task<IActionResult> DetailsPartial(int id, bool isRemindBack = false)
        {
            var model = isRemindBack ? _caseRepo.GetRemindBackCaseById(id) : _caseRepo.GetCaseById(id);
            if (model == null) return NotFound();
            ViewBag.IsRemindBack = isRemindBack;

            // Fetch TR-18 Accident Details if possible
            if (!string.IsNullOrEmpty(model.VehicleNo) && model.AccidentDate.HasValue)
            {
                try
                {
                    var tr18Response = await _tr18Service.GetAccidentDetails(model.AccidentDate, model.VehicleNo);
                    if (tr18Response != null && tr18Response.Success)
                    {
                        ViewBag.TR18Data = tr18Response.Data;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "TR-18 Fetch Error (Partial) for Case {CaseID}", id);
                }
            }

            ViewData["HideActionBtn"] = true;
            return PartialView("_DetailsContent", model);
        }

        [HttpGet]
        public IActionResult Edit(int id)
        {
            var model = _caseRepo.GetCaseById(id);
            if (model == null) return NotFound();

            // IDOR check: division users can only edit their own division's cases
            var divIdString = User.FindFirstValue("DivisionID");
            int userDivisionId = int.TryParse(divIdString, out int parsedId) ? parsedId : 0;
            bool isCentralOffice = userDivisionId == 0 || userDivisionId == 5;
            if (!isCentralOffice && model.DivisionID != userDivisionId)
            {
                _logger.LogWarning("IDOR attempt: User DivisionID={UserDiv} tried to edit CaseID={CaseID} (DivisionID={CaseDiv})",
                    userDivisionId, id, model.DivisionID);
                return Forbid();
            }

            ViewBag.Divisions = _masterRepo.GetAllDivisions();
            ViewBag.MACTs = _masterRepo.GetAllMACTs();
            ViewBag.Advocates = _masterRepo.GetAllAdvocates();
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Edit(MVCCaseViewModel model)
        {
            if (!string.IsNullOrWhiteSpace(model.CNRNumber))
            {
                model.CNRNumber = model.CNRNumber.Trim().ToUpperInvariant();
                if (string.IsNullOrWhiteSpace(model.EstCode) && model.CNRNumber.Length >= 6)
                {
                    model.EstCode = model.CNRNumber.Substring(0, 6);
                }
            }

            model.MACTID = ResolveMactId(model.EstCode, model.MACTID);
            ModelState.Remove("MACTID");
            ModelState.Remove("EstCode");
            ModelState.Remove("CaseTypeCode");

            if (ModelState.IsValid)
            {
                try
                {
                    var existingCase = _caseRepo.GetCaseById(model.CaseID);
                    if (existingCase == null) return NotFound();

                    // IDOR check on POST: division users can only edit their own division's cases
                    var divIdString = User.FindFirstValue("DivisionID");
                    int userDivisionId = int.TryParse(divIdString, out int parsedId) ? parsedId : 0;
                    bool isCentralOffice = userDivisionId == 0 || userDivisionId == 5 || User.IsInRole("Admin");
                    if (!isCentralOffice && existingCase.DivisionID != userDivisionId)
                    {
                        _logger.LogWarning("IDOR attempt on POST: User DivisionID={UserDiv} tried to update CaseID={CaseID} (DivisionID={CaseDiv})",
                            userDivisionId, model.CaseID, existingCase.DivisionID);
                        return Forbid();
                    }

                    // Handle Interim Order file
                    if (model.InterimOrderFile != null)
                    {
                        model.InterimOrderFilePath = SaveFile(model.InterimOrderFile, "cases");
                        model.HasInterimOrder = true;
                    }
                    else if (existingCase != null && string.IsNullOrEmpty(model.InterimOrderFilePath))
                    {
                        model.InterimOrderFilePath = existingCase.InterimOrderFilePath;
                        if (!string.IsNullOrEmpty(model.InterimOrderFilePath)) model.HasInterimOrder = true;
                    }

                    // Handle File Uploads (Only if new files are uploaded, else preserve existing)
                    if (model.AdverseAward == null) model.AdverseAward = new AdverseAwardViewModel();

                    if (model.AdverseAward.SecurityDocFile != null)
                        model.AdverseAward.SecurityUploadPath = SaveFile(model.AdverseAward.SecurityDocFile, "cases");
                    else if (existingCase?.AdverseAward != null && string.IsNullOrEmpty(model.AdverseAward.SecurityUploadPath))
                        model.AdverseAward.SecurityUploadPath = existingCase.AdverseAward.SecurityUploadPath;

                    if (model.AdverseAward.IAFile != null)
                        model.AdverseAward.DelayApplicationPath = SaveFile(model.AdverseAward.IAFile, "cases");
                    else if (existingCase?.AdverseAward != null && string.IsNullOrEmpty(model.AdverseAward.DelayApplicationPath))
                        model.AdverseAward.DelayApplicationPath = existingCase.AdverseAward.DelayApplicationPath;

                    if (model.AdverseAward.DelayOrderFile != null)
                        model.AdverseAward.DelayCondonedOrderPath = SaveFile(model.AdverseAward.DelayOrderFile, "cases");
                    else if (existingCase?.AdverseAward != null && string.IsNullOrEmpty(model.AdverseAward.DelayCondonedOrderPath))
                        model.AdverseAward.DelayCondonedOrderPath = existingCase.AdverseAward.DelayCondonedOrderPath;

                    if (model.DisposalResult == "Against")
                    {
                        if (model.AdverseAward.TR18File != null)
                            model.AdverseAward.TR18UploadPath = SaveFile(model.AdverseAward.TR18File, "cases");
                        else if (existingCase?.AdverseAward != null && string.IsNullOrEmpty(model.AdverseAward.TR18UploadPath))
                            model.AdverseAward.TR18UploadPath = existingCase.AdverseAward.TR18UploadPath;
                        
                        if (model.AdverseAward.PunishmentOrderFile != null)
                            model.AdverseAward.PunishmentOrderUploadPath = SaveFile(model.AdverseAward.PunishmentOrderFile, "cases");
                        else if (existingCase?.AdverseAward != null && string.IsNullOrEmpty(model.AdverseAward.PunishmentOrderUploadPath))
                            model.AdverseAward.PunishmentOrderUploadPath = existingCase.AdverseAward.PunishmentOrderUploadPath;

                        if (model.AdverseAward.GovIDProofFile != null)
                            model.AdverseAward.GovIDProofUploadPath = SaveFile(model.AdverseAward.GovIDProofFile, "cases");
                        else if (existingCase?.AdverseAward != null && string.IsNullOrEmpty(model.AdverseAward.GovIDProofUploadPath))
                            model.AdverseAward.GovIDProofUploadPath = existingCase.AdverseAward.GovIDProofUploadPath;

                        if (model.AdverseAward.AdverseJudgmentFile != null)
                            model.AdverseAward.AdverseJudgmentUploadPath = SaveFile(model.AdverseAward.AdverseJudgmentFile, "cases");
                        else if (existingCase?.AdverseAward != null && string.IsNullOrEmpty(model.AdverseAward.AdverseJudgmentUploadPath))
                            model.AdverseAward.AdverseJudgmentUploadPath = existingCase.AdverseAward.AdverseJudgmentUploadPath;

                        if (model.AdverseAward.AgeProofFile != null)
                            model.AdverseAward.AgeProofUploadPath = SaveFile(model.AdverseAward.AgeProofFile, "cases");
                        else if (existingCase?.AdverseAward != null && string.IsNullOrEmpty(model.AdverseAward.AgeProofUploadPath))
                            model.AdverseAward.AgeProofUploadPath = existingCase.AdverseAward.AgeProofUploadPath;
                    }

                    // Synchronize ClaimPetitionDate and IsAllegedAccident
                    if (model.ClaimPetitionDate == null && model.AdverseAward?.ClaimPetitionDate != null)
                        model.ClaimPetitionDate = model.AdverseAward.ClaimPetitionDate;
                    else if (model.AdverseAward != null && model.AdverseAward.ClaimPetitionDate == null && model.ClaimPetitionDate != null)
                        model.AdverseAward.ClaimPetitionDate = model.ClaimPetitionDate;

                    if (model.AdverseAward != null)
                    {
                        model.AdverseAward.IsAllegedAccident = model.IsAllegedAccident || model.AdverseAward.IsAllegedAccident;
                        model.IsAllegedAccident = model.AdverseAward.IsAllegedAccident;
                    }

                    // Clean & sanitize CNR Number
                    if (!string.IsNullOrWhiteSpace(model.CNRNumber))
                    {
                        model.CNRNumber = model.CNRNumber.Trim().ToUpperInvariant();

                        try
                        {
                            string caseTypeCode = string.IsNullOrEmpty(model.CaseTypeCode) ? "83" : model.CaseTypeCode;
                            _ecourtsRepo.SaveTrackedCaseAsync(new MVCCaseManagement.DAL.TrackedCaseModel
                            {
                                CNRNumber = model.CNRNumber,
                                EstCode = model.EstCode,
                                CaseTypeCode = caseTypeCode,
                                RegNo = model.MVCNo,
                                RegYear = model.MVCYear
                            }).GetAwaiter().GetResult();
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to register tracked case for CNR {CNR} in Edit", model.CNRNumber);
                        }
                    }

                    _caseRepo.UpdateCase(model);
                    _ = _activityLogger.LogCaseUpdatedAsync("MVC", model.CaseID, $"MVC {model.MVCNo}/{model.MVCYear}", model.VehicleNo, null, existingCase, model);

                    if (model.LinkedAppeal != null)
                    {
                        model.LinkedAppeal.CaseID = model.CaseID;
                        _appealRepo.SaveAppeal(model.LinkedAppeal);
                    }
                    
                    var userName = User.Claims.FirstOrDefault(c => c.Type == "FullName")?.Value ?? User.Identity?.Name ?? "User";
                    TempData["SuccessMessage"] = "Case details updated successfully.";

                    return RedirectToAction("Index");
                }
                catch (Exception ex)
                {
                    TempData["ErrorMessage"] = "Error updating case. Please try again. " + ex.Message;
                    _logger.LogError(ex, "Error updating case ID {CaseID}", model.CaseID);
                    
                    // Re-populate dropdowns on error
                    var divIdString = User.FindFirstValue("DivisionID");
                    int userDivisionId = int.TryParse(divIdString, out int parsedId) ? parsedId : 0;
                    var divisions = _masterRepo.GetAllDivisions();
                    ViewBag.Divisions = divisions;
                    ViewBag.UserDivisionName = divisions.FirstOrDefault(d => d.DivisionID == userDivisionId)?.DivisionNameEnglish;
                    ViewBag.MACTs = _masterRepo.GetAllMACTs();
                    ViewBag.Advocates = _masterRepo.GetAllAdvocates();
                    return View(model);
                }
            }

            // Re-populate dropdowns on validation error
            var valDivIdString = User.FindFirstValue("DivisionID");
            int valUserDivisionId = int.TryParse(valDivIdString, out int valParsedId) ? valParsedId : 0;
            var valDivisions = _masterRepo.GetAllDivisions();
            ViewBag.Divisions = valDivisions;
            ViewBag.UserDivisionName = valDivisions.FirstOrDefault(d => d.DivisionID == valUserDivisionId)?.DivisionNameEnglish;
            ViewBag.MACTs = _masterRepo.GetAllMACTs();
            ViewBag.Advocates = _masterRepo.GetAllAdvocates();
            return View(model);
        }

        [Route("/Case/Transfer")]
        [Route("/Transfer/MVC")]
        public IActionResult Transfer()
        {
            var divIdString = User.FindFirstValue("DivisionID");
            int divisionId = int.TryParse(divIdString, out int id) ? id : 0;
            var divName = User.FindFirstValue("DivisionName");

            ViewBag.Divisions = _masterRepo.GetAllDivisions();
            ViewBag.MACTs = _masterRepo.GetAllMACTs();

            var model = new TransferViewModel
            {
                FromDivisionID = divisionId,
                FromDivisionName = divName
            };

            return View(model);
        }

        [HttpGet]
        [Route("/Case/SearchTransferCase")]
        public IActionResult SearchTransferCase(string mvcNo, int year, int mactId)
        {
            var c = _caseRepo.GetCaseByMVCDetails(mvcNo, year, mactId);

            if (c != null)
            {
                return Json(new { 
                    success = true, 
                    caseId = c.CaseID,
                    petitionerName = c.Petitioners.FirstOrDefault()?.PetitionerName ?? "N/A",
                    divisionName = c.DivisionName,
                    currentDivisionId = c.DivisionID
                });
            }

            return Json(new { success = false, message = "Case not found with the provided details." });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Route("/Case/ProcessTransfer")]
        public IActionResult ProcessTransfer(TransferViewModel model)
        {
            // CaseID is bound from hidden field
            if (model.CaseID == 0)
            {
                // Try to find the case if ID missing but details present (unlikely with proper form)
                TempData["ErrorMessage"] = "Please search and select a valid case first.";
                return RedirectToAction("Transfer");
            }

            if (model.ToDivisionID == 0)
            {
                TempData["ErrorMessage"] = "Please select a target division.";
                return RedirectToAction("Transfer");
            }

            // Verify current division of case
            var c = _caseRepo.GetCaseById(model.CaseID);
            if (c != null && c.DivisionID == model.ToDivisionID)
            {
                 TempData["ErrorMessage"] = "Case is already in the target division.";
                 return RedirectToAction("Transfer");
            }
            
            var success = _caseRepo.TransferCase(model.CaseID, model.ToDivisionID, model.TransferRemarks ?? "");
            if (success)
            {
                var targetDiv = _masterRepo.GetAllDivisions().FirstOrDefault(d => d.DivisionID == model.ToDivisionID);
                var srcDiv = _masterRepo.GetAllDivisions().FirstOrDefault(d => d.DivisionID == (c != null ? c.DivisionID : 0));
                _ = _activityLogger.LogCaseTransferredAsync("MVC", model.CaseID, $"MVC {c?.MVCNo}/{c?.MVCYear}", c?.DivisionID ?? 0, model.ToDivisionID, srcDiv?.DivisionNameEnglish ?? "Source Division", targetDiv?.DivisionNameEnglish ?? "Target Division", model.TransferRemarks);
                TempData["SuccessMessage"] = $"Case successfully transferred to {targetDiv?.DivisionNameEnglish}.";
                return RedirectToAction("Transfer");
            }
            else
            {
                TempData["ErrorMessage"] = "Error occurred during transfer. Please try again.";
                return RedirectToAction("Transfer");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Route("/Case/TransferFromDetails")]
        public IActionResult TransferFromDetails(int caseId, int toDivisionId, string? remarks)
        {
            if (caseId == 0 || toDivisionId == 0)
            {
                TempData["TransferError"] = toDivisionId == 0 ? "Please select a target division." : "Invalid case.";
                return RedirectToAction("Details", new { id = caseId });
            }

            var c = _caseRepo.GetCaseById(caseId);
            if (c == null)
            {
                TempData["TransferError"] = "Case not found.";
                return RedirectToAction("Details", new { id = caseId });
            }
            if (c.DivisionID == toDivisionId)
            {
                TempData["TransferError"] = "Case is already in the selected division.";
                return RedirectToAction("Details", new { id = caseId });
            }

            var success = _caseRepo.TransferCase(caseId, toDivisionId, remarks ?? "");
            if (success)
            {
                var targetDiv = _masterRepo.GetAllDivisions().FirstOrDefault(d => d.DivisionID == toDivisionId);
                TempData["TransferSuccess"] = $"Case MVC {c.MVCNo}/{c.MVCYear} successfully transferred to {targetDiv?.DivisionNameEnglish}.";
            }
            else
            {
                TempData["TransferError"] = "Transfer failed. Please try again.";
            }
            return RedirectToAction("Details", new { id = caseId });
        }

        [HttpPost]
        [IgnoreAntiforgeryToken]
        [Route("/Case/DismissTransferNotification")]
        public IActionResult DismissTransferNotification(int caseId)
        {
            _caseRepo.MarkTransferAsViewed(caseId);
            return Json(new { success = true });
        }

        [HttpGet]
        public IActionResult GetLinkedCases(string vehicleNo, DateTime accidentDate)
        {
            if (string.IsNullOrEmpty(vehicleNo)) return Json(new List<ConnectedCaseViewModel>());
            
            var cases = _caseRepo.SearchLinkedCases(vehicleNo, accidentDate);
            return Json(cases);
        }
        [HttpGet]
        public IActionResult GetAdvocates(int divisionId)
        {
            var advocates = _caseRepo.GetAdvocatesByDivision(divisionId);
            return Json(advocates);
        }

        private string? SaveFile(IFormFile? file, string folder)
        {
            if (file == null || file.Length == 0) return null;

            // 1. FILE SIZE VALIDATION (10MB max)
            const long maxFileSize = 10 * 1024 * 1024; // 10MB
            if (file.Length > maxFileSize)
            {
                throw new InvalidOperationException($"File size exceeds maximum allowed size of 10MB. File size: {file.Length /1024 / 1024}MB");
            }

            // 2. FILE EXTENSION VALIDATION (PDF only)
            var allowedExtensions = new[] { ".pdf" };
            var fileExtension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!allowedExtensions.Contains(fileExtension))
            {
                throw new InvalidOperationException($"Invalid file type. Only PDF files are allowed. Uploaded file type: {fileExtension}");
            }

            // 3. FILENAME SANITIZATION - Remove potentially dangerous characters and replace spaces with underscores
            var originalFileName = Path.GetFileNameWithoutExtension(file.FileName);
            var sanitizedFileName = string.Concat(originalFileName.Where(c => 
                char.IsLetterOrDigit(c) || c == '_' || c == '-' || c == ' '
            )).Trim().Replace(" ", "_");
            
            if (string.IsNullOrWhiteSpace(sanitizedFileName))
            {
                sanitizedFileName = "document";
            }

            // 4. CHECK FOR SUSPICIOUS PATTERNS (basic malware detection)
            using (var stream = file.OpenReadStream())
            {
                var buffer = new byte[512];
                int bytesRead = stream.Read(buffer, 0, buffer.Length);
                stream.Position = 0; // Reset for later use
                
                // Check for PDF signature
                var pdfSignature = new byte[] { 0x25, 0x50, 0x44, 0x46 }; // %PDF
                bool isPdf = true;
                if (bytesRead < pdfSignature.Length)
                {
                    isPdf = false;
                }
                else
                {
                    for (int i = 0; i < pdfSignature.Length; i++)
                    {
                        if (buffer[i] != pdfSignature[i])
                        {
                            isPdf = false;
                            break;
                        }
                    }
                }
                
                if (!isPdf)
                {
                    throw new InvalidOperationException("File appears to be corrupted or not a valid PDF file.");
                }
            }

            // 5. CREATE UPLOAD DIRECTORIES IF NOT EXISTS (Both ContentRoot & WebRoot for persistence across deploys)
            var contentFolder = Path.Combine(_environment.ContentRootPath, "uploads", folder);
            if (!Directory.Exists(contentFolder)) Directory.CreateDirectory(contentFolder);

            var uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads", folder);
            if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

            // 6. GENERATE UNIQUE FILENAME (GUID + timestamp + sanitized name)
            var timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");
            var uniqueFileName = $"{Guid.NewGuid()}_{timestamp}_{sanitizedFileName}{fileExtension}";
            var filePath = Path.Combine(uploadsFolder, uniqueFileName);
            var contentFilePath = Path.Combine(contentFolder, uniqueFileName);

            // 7. SAVE FILE SECURELY TO BOTH LOCATIONS
            try
            {
                using (var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    file.CopyTo(fileStream);
                }

                try
                {
                    System.IO.File.Copy(filePath, contentFilePath, overwrite: true);
                }
                catch (Exception exCopy)
                {
                    _logger.LogWarning(exCopy, "Could not copy uploaded file to ContentRoot uploads directory.");
                }
                
                _logger.LogInformation("File uploaded: {FileName} ({Size} bytes)", uniqueFileName, file.Length);
                
                return $"/uploads/{folder}/{uniqueFileName}";
            }
            catch (Exception ex)
            {
                // Clean up partial file if save failed
                if (System.IO.File.Exists(filePath))
                {
                    try { System.IO.File.Delete(filePath); } catch { }
                }
                if (System.IO.File.Exists(contentFilePath))
                {
                    try { System.IO.File.Delete(contentFilePath); } catch { }
                }
                
                _logger.LogError(ex, "Error saving uploaded file");
                throw new InvalidOperationException("Failed to save file. Please try again.", ex);
            }
        }

        // ========================================
        // Case Payment API Endpoints
        // ========================================

        [HttpGet]
        public IActionResult GetPayments(int caseId)
        {
            try
            {
                var payments = _paymentRepo.GetPaymentsByCaseId(caseId);
                return Json(payments);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult AddPayment([FromBody] CasePaymentViewModel payment)
        {
            try
            {
                if (!ModelState.IsValid)
                    return BadRequest(ModelState);

                var username = User.Identity?.Name ?? "System";
                payment.CreatedBy = username;
                payment.CreatedDate = DateTime.Now;

                var paymentId = _paymentRepo.AddPayment(payment);
                _ = _activityLogger.LogSubEntityActionAsync("MVC", payment.CaseID, $"Case #{payment.CaseID}", "PAYMENT_ADDED", $"Recorded payment of ₹{payment.Amount:N2} (Cheque: {payment.ChequeNumber})", payment);
                return Json(new { success = true, paymentId });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult DeletePayment(int paymentId)
        {
            try
            {
                var result = _paymentRepo.DeletePayment(paymentId);
                if (result)
                {
                    _ = _activityLogger.LogSubEntityActionAsync("MVC", 0, "Payment Record", "PAYMENT_DELETED", $"Deleted payment record #{paymentId}");
                }
                return Json(new { success = result });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        private int ResolveMactId(string? estCode, int currentMactId)
        {
            if (!string.IsNullOrWhiteSpace(estCode))
            {
                string cleanEst = estCode.Trim().ToUpperInvariant();
                var macts = _masterRepo.GetAllMACTs().ToList();

                var matched = macts.FirstOrDefault(m => 
                    !string.IsNullOrEmpty(m.MACTCode) && m.MACTCode.Trim().Equals(cleanEst, StringComparison.OrdinalIgnoreCase));

                if (matched == null)
                {
                    if (cleanEst.StartsWith("KADW02"))
                        matched = macts.FirstOrDefault(m => m.MACTName.Contains("Hubballi", StringComparison.OrdinalIgnoreCase));
                    else if (cleanEst.StartsWith("KADW"))
                        matched = macts.FirstOrDefault(m => m.MACTName.Contains("Dharwad", StringComparison.OrdinalIgnoreCase));
                    else if (cleanEst.StartsWith("KABG"))
                        matched = macts.FirstOrDefault(m => m.MACTName.Contains("Belagavi", StringComparison.OrdinalIgnoreCase) || m.MACTName.Contains("Belgaum", StringComparison.OrdinalIgnoreCase));
                    else if (cleanEst.StartsWith("KAUK"))
                        matched = macts.FirstOrDefault(m => m.MACTName.Contains("Karwar", StringComparison.OrdinalIgnoreCase) || m.MACTName.Contains("Sirsi", StringComparison.OrdinalIgnoreCase) || m.MACTName.Contains("Uttara", StringComparison.OrdinalIgnoreCase));
                    else if (cleanEst.StartsWith("KAGD"))
                        matched = macts.FirstOrDefault(m => m.MACTName.Contains("Gadag", StringComparison.OrdinalIgnoreCase));
                    else if (cleanEst.StartsWith("KAHA"))
                        matched = macts.FirstOrDefault(m => m.MACTName.Contains("Haveri", StringComparison.OrdinalIgnoreCase));
                    else if (cleanEst.StartsWith("KABJ"))
                        matched = macts.FirstOrDefault(m => m.MACTName.Contains("Vijayapura", StringComparison.OrdinalIgnoreCase) || m.MACTName.Contains("Bijapur", StringComparison.OrdinalIgnoreCase));
                    else if (cleanEst.StartsWith("KABK"))
                        matched = macts.FirstOrDefault(m => m.MACTName.Contains("Bagalkot", StringComparison.OrdinalIgnoreCase));
                    else if (cleanEst.StartsWith("KAHC"))
                        matched = macts.FirstOrDefault(m => m.MACTName.Contains("High Court", StringComparison.OrdinalIgnoreCase));
                }

                if (matched != null && matched.MACTID > 0)
                {
                    return matched.MACTID;
                }

                // Auto-create MACT entry if missing
                try
                {
                    string courtName = GetCourtNameFromEstCode(cleanEst);
                    string location = GetLocationFromEstCode(cleanEst);
                    _masterRepo.AddMACT(new MACT
                    {
                        MACTCode = cleanEst,
                        MACTName = courtName,
                        Location = location
                    });

                    var created = _masterRepo.GetAllMACTs().FirstOrDefault(m => 
                        (!string.IsNullOrEmpty(m.MACTCode) && m.MACTCode.Equals(cleanEst, StringComparison.OrdinalIgnoreCase)) ||
                        m.MACTName == courtName);

                    if (created != null && created.MACTID > 0)
                    {
                        return created.MACTID;
                    }
                }
                catch { }
            }

            if (currentMactId > 0)
            {
                return currentMactId;
            }

            return 1;
        }

        private string GetCourtNameFromEstCode(string estCode)
        {
            if (estCode.StartsWith("KADW02")) return "Addl Senior Civil Judge & JMFC, Hubballi [KADW02]";
            if (estCode.StartsWith("KADW")) return "Principal District & Sessions Court, Dharwad [KADW01]";
            if (estCode.StartsWith("KABG")) return "Principal District & Sessions Court, Belagavi [KABG01]";
            if (estCode.StartsWith("KAUKA2")) return "SENIOR CIVIL JUDGE AND PRL. JMFC, SIRSI";
            if (estCode.StartsWith("KAUK")) return "Principal District & Sessions Court, Karwar [KAUK01]";
            if (estCode.StartsWith("KAGD")) return "Principal District & Sessions Court, Gadag [KAGD01]";
            if (estCode.StartsWith("KAHA")) return "Principal District & Sessions Court, Haveri [KAHA01]";
            if (estCode.StartsWith("KABJ")) return "Principal District & Sessions Court, Vijayapura [KABJ01]";
            if (estCode.StartsWith("KABK")) return "Principal District & Sessions Court, Bagalkot [KABK01]";
            if (estCode.StartsWith("KAHC02")) return "High Court of Karnataka, Dharwad Bench [KAHC02]";
            if (estCode.StartsWith("KAHC03")) return "High Court of Karnataka, Kalaburagi Bench [KAHC03]";
            if (estCode.StartsWith("KAHC")) return "High Court of Karnataka, Principal Bench Bengaluru [KAHC01]";
            return $"MACT Court [{estCode}]";
        }

        private string GetLocationFromEstCode(string estCode)
        {
            if (estCode.StartsWith("KADW02")) return "Hubballi";
            if (estCode.StartsWith("KADW")) return "Dharwad";
            if (estCode.StartsWith("KABG")) return "Belagavi";
            if (estCode.StartsWith("KAUK")) return "Karwar";
            if (estCode.StartsWith("KAGD")) return "Gadag";
            if (estCode.StartsWith("KAHA")) return "Haveri";
            if (estCode.StartsWith("KABJ")) return "Vijayapura";
            if (estCode.StartsWith("KABK")) return "Bagalkot";
            return "Karnataka";
        }

        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> UpdateCnr(int caseId, string? cnrNumber)
        {
            var divIdString = User.FindFirst("DivisionID")?.Value;
            bool isCO = string.IsNullOrEmpty(divIdString) || divIdString == "0" || divIdString == "5";
            int userDivId = int.TryParse(divIdString, out int parsedId) ? parsedId : -1;

            var dbCase = _caseRepo.GetCaseById(caseId);
            if (dbCase == null)
            {
                return Json(new { success = false, message = "MVC Case not found." });
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
            bool updated = _caseRepo.UpdateCNR(caseId, cleanCnr, estCode);

            if (updated)
            {
                if (!string.IsNullOrEmpty(cleanCnr))
                {
                    try
                    {
                        await _ecourtsRepo.SaveTrackedCaseAsync(new MVCCaseManagement.DAL.TrackedCaseModel
                        {
                            CNRNumber = cleanCnr,
                            EstCode = estCode ?? "",
                            CaseTypeCode = "MVC",
                            RegNo = dbCase.MVCNo ?? "",
                            RegYear = dbCase.MVCYear
                        });
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to register tracked case for MVC CNR {CNR}", cleanCnr);
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
    }
}

