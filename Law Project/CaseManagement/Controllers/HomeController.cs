using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using MVCCaseManagement.DAL;
using MVCCaseManagement.Models;
using System.IO;
using Microsoft.AspNetCore.Hosting;

namespace MVCCaseManagement.Controllers
{
    [Authorize]
    public class HomeController : Controller
    {
        private readonly ICaseRepository _caseRepo;
        private readonly IOtherCourtsRepository _otherRepo;
        private readonly IMasterRepository _masterRepo;
        private readonly IWebHostEnvironment _env;
        private readonly MVCCaseManagement.Services.Storage.IFileStorageService? _storageService;

        public HomeController(
            ICaseRepository caseRepo,
            IOtherCourtsRepository otherRepo,
            IMasterRepository masterRepo,
            IWebHostEnvironment env,
            MVCCaseManagement.Services.Storage.IFileStorageService? storageService = null)
        {
            _caseRepo = caseRepo;
            _otherRepo = otherRepo;
            _masterRepo = masterRepo;
            _env = env;
            _storageService = storageService;
        }

        private async Task<string?> SaveOtherCourtsFileAsync(Microsoft.AspNetCore.Http.IFormFile? file, string subFolder, string? existingPath = null)
        {
            if (file == null || file.Length == 0) return existingPath;
            if (_storageService != null)
            {
                return await _storageService.SaveFileAsync(file, subFolder, existingPath);
            }
            string uploadsFolder = Path.Combine(_env.WebRootPath, "uploads", subFolder);
            if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);
            string uniqueFileName = Guid.NewGuid().ToString() + "_" + Path.GetFileName(file.FileName);
            string filePath = Path.Combine(uploadsFolder, uniqueFileName);
            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(fileStream);
            }
            return $"/uploads/{subFolder}/{uniqueFileName}";
        }

        public IActionResult Index()
        {
            var divIdString = User.FindFirstValue("DivisionID");
            int divisionId = int.TryParse(divIdString, out int id) ? id : 0;
            
            var stats = _caseRepo.GetDashboardStats(divisionId);
            return View(stats);
        }

        public IActionResult HearingsToday(string module = "All")
        {
            var divIdString = User.FindFirstValue("DivisionID");
            int divisionId = int.TryParse(divIdString, out int id) ? id : 0;
            
            ViewBag.SelectedModule = module;
            var cases = _caseRepo.GetHearingsTodayIrrespectiveOfModule(divisionId, module);
            return View(cases);
        }

        public IActionResult AwardsToday(string module = "All")
        {
            var divIdString = User.FindFirstValue("DivisionID");
            int divisionId = int.TryParse(divIdString, out int id) ? id : 0;
            
            ViewBag.SelectedModule = module;
            var cases = _caseRepo.GetAwardsTodayIrrespectiveOfModule(divisionId, module);
            return View(cases);
        }

        public IActionResult ComplianceDue(string module = "All")
        {
            var divIdString = User.FindFirstValue("DivisionID");
            int divisionId = int.TryParse(divIdString, out int id) ? id : 0;
            
            ViewBag.SelectedModule = module;
            var cases = _caseRepo.GetComplianceDueIrrespectiveOfModule(divisionId, module);
            return View(cases);
        }

        public IActionResult CriticalDelays(string module = "All")
        {
            var divIdString = User.FindFirstValue("DivisionID");
            int divisionId = int.TryParse(divIdString, out int id) ? id : 0;
            
            ViewBag.SelectedModule = module;
            var cases = _caseRepo.GetCriticalDelaysIrrespectiveOfModule(divisionId, module);
            return View(cases);
        }
        public IActionResult OtherCourts(string activeCourt = "OS")
        {
            ViewBag.ActiveCourt = activeCourt;
            
            return activeCourt switch
            {
                "ECA" => RedirectToAction("ECA"),
                "PSC" => RedirectToAction("PSC"),
                "CC" => RedirectToAction("CC"),
                "Consumer" => RedirectToAction("Consumer"),
                "LAC" => RedirectToAction("LAC"),
                _ => View()
            };
        }

        public IActionResult OSCorporation()
        {
            ViewBag.ActiveCourt = "OS";
            ViewBag.LitigantType = "Corporation";
            
            var divIdString = User.FindFirstValue("DivisionID");
            int divisionId = int.TryParse(divIdString, out int id) ? id : 0;

            var stats = _otherRepo.GetDashboardStats(divisionId, "OS", "Corporation");
            return View(stats);
        }

        public IActionResult OSCorporationForm()
        {
            ViewBag.ActiveCourt = "OS";
            ViewBag.LitigantType = "Corporation";
            ViewBag.Advocates = _masterRepo.GetAllAdvocates();
            return View();
        }

        public IActionResult OSClaimant()
        {
            ViewBag.ActiveCourt = "OS";
            ViewBag.LitigantType = "Claimant";
            
            var divIdString = User.FindFirstValue("DivisionID");
            int divisionId = int.TryParse(divIdString, out int id) ? id : 0;

            var stats = _otherRepo.GetDashboardStats(divisionId, "OS", "Claimant");
            return View(stats);
        }

        public IActionResult OSClaimantForm()
        {
            ViewBag.ActiveCourt = "OS";
            ViewBag.LitigantType = "Claimant";
            ViewBag.Advocates = _masterRepo.GetAllAdvocates();
            return View();
        }

        // PSC
        public IActionResult PSC()
        {
            ViewBag.ActiveCourt = "PSC";
            ViewBag.LitigantType = "Claimant";
            var divIdString = User.FindFirstValue("DivisionID");
            int divisionId = int.TryParse(divIdString, out int id) ? id : 0;
            var stats = _otherRepo.GetDashboardStats(divisionId, "PSC", null);
            return View("OtherCourtsDashboard", stats);
        }

        public IActionResult PSCForm()
        {
            ViewBag.ActiveCourt = "PSC";
            ViewBag.LitigantType = "Claimant";
            ViewBag.Advocates = _masterRepo.GetAllAdvocates();
            return View("OtherCourtsForm");
        }

        // CC
        public IActionResult CC()
        {
            ViewBag.ActiveCourt = "CC";
            ViewBag.LitigantType = "Claimant";
            var divIdString = User.FindFirstValue("DivisionID");
            int divisionId = int.TryParse(divIdString, out int id) ? id : 0;
            var stats = _otherRepo.GetDashboardStats(divisionId, "CC", null);
            return View("OtherCourtsDashboard", stats);
        }

        public IActionResult CCForm()
        {
            ViewBag.ActiveCourt = "CC";
            ViewBag.LitigantType = "Claimant";
            ViewBag.Advocates = _masterRepo.GetAllAdvocates();
            return View("OtherCourtsForm");
        }

        // Consumer
        public IActionResult Consumer()
        {
            ViewBag.ActiveCourt = "Consumer";
            ViewBag.LitigantType = "Claimant";
            var divIdString = User.FindFirstValue("DivisionID");
            int divisionId = int.TryParse(divIdString, out int id) ? id : 0;
            var stats = _otherRepo.GetDashboardStats(divisionId, "Consumer", null);
            return View("OtherCourtsDashboard", stats);
        }

        public IActionResult ConsumerForm()
        {
            ViewBag.ActiveCourt = "Consumer";
            ViewBag.LitigantType = "Claimant";
            ViewBag.Advocates = _masterRepo.GetAllAdvocates();
            return View("OtherCourtsForm");
        }

        // LAC
        public IActionResult LAC()
        {
            ViewBag.ActiveCourt = "LAC";
            ViewBag.LitigantType = "Claimant";
            var divIdString = User.FindFirstValue("DivisionID");
            int divisionId = int.TryParse(divIdString, out int id) ? id : 0;
            var stats = _otherRepo.GetDashboardStats(divisionId, "LAC", null);
            return View("OtherCourtsDashboard", stats);
        }

        public IActionResult LACForm()
        {
            ViewBag.ActiveCourt = "LAC";
            ViewBag.LitigantType = "Claimant";
            ViewBag.Advocates = _masterRepo.GetAllAdvocates();
            return View("OtherCourtsForm");
        }

        public IActionResult ECA()
        {
            ViewBag.ActiveCourt = "ECA";
            ViewBag.LitigantType = "Claimant";
            var divIdString = User.FindFirstValue("DivisionID");
            int divisionId = int.TryParse(divIdString, out int id) ? id : 0;
            var stats = _otherRepo.GetDashboardStats(divisionId, "ECA", null);
            return View("ECADashboard", stats);
        }

        public IActionResult ECAForm()
        {
            ViewBag.ActiveCourt = "ECA";
            ViewBag.LitigantType = "Claimant";
            ViewBag.Advocates = _masterRepo.GetAllAdvocates();
            return View("ECA");
        }

        public IActionResult OtherCourtsCaseList(string activeCourt = "OS", string? litigantType = null)
        {
            var divIdString = User.FindFirstValue("DivisionID");
            int divisionId = int.TryParse(divIdString, out int id) ? id : 0;

            ViewBag.ActiveCourt = activeCourt;
            ViewBag.LitigantType = litigantType ?? (activeCourt == "OS" ? "Corporation" : "");

            // For non-OS courts (ECA, PSC, CC, Consumer, LAC), LitigantType shouldn't filter out cases
            string? filterLitigantType = (activeCourt == "OS" && !string.IsNullOrEmpty(litigantType) && litigantType != "All") 
                ? litigantType 
                : null;

            var cases = _otherRepo.GetAllCases(divisionId, activeCourt, filterLitigantType);
            return View(cases);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveOtherCourtsCase(OtherCourtsCase model)
        {
            try
            {
                var divIdString = User.FindFirstValue("DivisionID");
                int divisionId = int.TryParse(divIdString, out int id) ? id : 0;
                model.DivisionID = divisionId;
                model.CreatedBy = int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int userId) ? userId : null;

                // Handle Interim Order File Upload
                if (model.InterimOrderFile != null && model.InterimOrderFile.Length > 0)
                {
                    model.InterimOrderFilePath = await SaveOtherCourtsFileAsync(model.InterimOrderFile, "other_courts");
                }

                // Handle Final Judgment File Upload
                if (model.FinalJudgmentFile != null && model.FinalJudgmentFile.Length > 0)
                {
                    model.FinalJudgmentFilePath = await SaveOtherCourtsFileAsync(model.FinalJudgmentFile, "other_courts_judgments");
                }

                // Handle Appeal Judgment File Upload
                if (model.AppealJudgmentCopy != null && model.AppealJudgmentCopy.Length > 0)
                {
                    model.AppealJudgmentCopyPath = await SaveOtherCourtsFileAsync(model.AppealJudgmentCopy, "other_courts_appeals");
                }

                int newId = _otherRepo.SaveCase(model);
                if (newId > 0)
                {
                    TempData["SuccessMessage"] = $"Case #{(!string.IsNullOrEmpty(model.CaseNumber) ? model.CaseNumber : newId.ToString())} saved successfully.";
                    return RedirectToAction("OtherCourtsCaseList", new { activeCourt = model.CaseType, litigantType = model.LitigantType });
                }
                
                TempData["ErrorMessage"] = "Failed to save case. Please check your inputs.";
                return RedirectToAction(model.LitigantType == "Corporation" ? "OSCorporation" : "OSClaimant");
            }
            catch (System.Exception ex)
            {
                TempData["ErrorMessage"] = "Error: " + ex.Message;
                return RedirectToAction("Index");
            }
        }
        public IActionResult OtherCourtsCaseDetails(int id)
        {
            var model = _otherRepo.GetCaseById(id);
            if (model == null) return NotFound();

            var divIdString = User.FindFirst("DivisionID")?.Value;
            var roleUpper = (User.FindFirst(ClaimTypes.Role)?.Value ?? "").ToUpperInvariant();
            bool isCentralOffice = string.IsNullOrEmpty(divIdString) || divIdString == "0" || divIdString == "5"
                                   || roleUpper.Contains("MD") || roleUpper.Contains("CLO") || roleUpper.Contains("LO")
                                   || roleUpper.Contains("ADMIN") || roleUpper.Contains("CENTRAL");

            int userDivId = int.TryParse(divIdString, out int pId) ? pId : -1;
            if (!isCentralOffice && model.DivisionID > 0 && model.DivisionID != userDivId)
            {
                return Forbid();
            }

            ViewBag.IsCentralOffice = isCentralOffice;
            ViewBag.CanEdit = !User.IsInRole("Division Controller") && !User.IsInRole("DC");
            ViewBag.Divisions = _masterRepo.GetAllDivisions();
            ViewBag.Advocates = _masterRepo.GetAllAdvocates();

            var activeCnrs = new List<string>();
            if (!string.IsNullOrWhiteSpace(model.CNRNumber)) activeCnrs.Add(model.CNRNumber);
            ViewBag.CaseOrders = _caseRepo.GetCaseOrdersByCnrs(activeCnrs);
            
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateCnr(int caseId, string? cnrNumber)
        {
            var model = _otherRepo.GetCaseById(caseId);
            if (model == null) return NotFound(new { success = false, message = "Case not found." });

            var divIdString = User.FindFirst("DivisionID")?.Value;
            var roleUpper = (User.FindFirst(ClaimTypes.Role)?.Value ?? "").ToUpperInvariant();
            bool isCentralOffice = string.IsNullOrEmpty(divIdString) || divIdString == "0" || divIdString == "5"
                                   || roleUpper.Contains("MD") || roleUpper.Contains("CLO") || roleUpper.Contains("LO")
                                   || roleUpper.Contains("ADMIN") || roleUpper.Contains("CENTRAL");
            int userDivId = int.TryParse(divIdString, out int pId) ? pId : -1;
            if (!isCentralOffice && model.DivisionID > 0 && model.DivisionID != userDivId)
            {
                return Forbid();
            }

            string cleanCnr = (cnrNumber ?? "").Trim().Replace("-", "").Replace(" ", "").ToUpperInvariant();
            if (!string.IsNullOrEmpty(cleanCnr) && cleanCnr.Length != 16)
            {
                return Json(new { success = false, message = "CNR Number must be exactly 16 alphanumeric characters." });
            }

            int? userId = int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int uId) ? uId : null;
            string? estCode = cleanCnr.Length >= 6 ? cleanCnr.Substring(0, 6) : null;
            bool updated = _otherRepo.UpdateCNR(caseId, string.IsNullOrEmpty(cleanCnr) ? null : cleanCnr, estCode, userId);

            if (updated)
            {
                return Json(new { success = true, message = string.IsNullOrEmpty(cleanCnr) ? "CNR Number unlinked successfully." : "CNR Number linked and updated successfully." });
            }
            return Json(new { success = false, message = "Failed to update CNR Number in database." });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateOrReuploadOtherCourtsDocument(int caseId, string docKey, string? documentTitle, string? remarks, IFormFile? documentFile)
        {
            var model = _otherRepo.GetCaseById(caseId);
            if (model == null) return NotFound(new { success = false, message = "Case not found." });

            var divIdString = User.FindFirst("DivisionID")?.Value;
            var roleUpper = (User.FindFirst(ClaimTypes.Role)?.Value ?? "").ToUpperInvariant();
            bool isCentralOffice = string.IsNullOrEmpty(divIdString) || divIdString == "0" || divIdString == "5"
                                   || roleUpper.Contains("MD") || roleUpper.Contains("CLO") || roleUpper.Contains("LO")
                                   || roleUpper.Contains("ADMIN") || roleUpper.Contains("CENTRAL");
            int userDivId = int.TryParse(divIdString, out int pId) ? pId : -1;
            if (!isCentralOffice && model.DivisionID > 0 && model.DivisionID != userDivId)
            {
                return Forbid();
            }

            int? userId = int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int uId) ? uId : null;
            string? existingPath = docKey switch
            {
                "InterimOrder" or "InterimOrderFilePath" => model.InterimOrderFilePath,
                "FinalJudgment" or "FinalJudgmentFilePath" => model.FinalJudgmentFilePath,
                "AppealJudgment" or "AppealJudgmentCopy" or "AppealJudgmentCopyPath" => model.AppealJudgmentCopyPath,
                _ => null
            };

            string subFolder = docKey switch
            {
                "InterimOrder" or "InterimOrderFilePath" => "other_courts",
                "FinalJudgment" or "FinalJudgmentFilePath" => "other_courts_judgments",
                "AppealJudgment" or "AppealJudgmentCopy" or "AppealJudgmentCopyPath" => "other_courts_appeals",
                _ => "other_courts"
            };

            string? newSavedPath = null;
            if (documentFile != null && documentFile.Length > 0)
            {
                newSavedPath = await SaveOtherCourtsFileAsync(documentFile, subFolder, existingPath);
                _otherRepo.UpdateDocumentPath(caseId, docKey, newSavedPath, userId);
            }

            return Json(new { 
                success = true, 
                message = newSavedPath != null 
                    ? "Document successfully uploaded and secured in persistent vault." 
                    : "Document details saved successfully.",
                filePath = newSavedPath ?? existingPath
            });
        }

        public IActionResult OtherCourtsCaseEdit(int id)
        {
            var model = _otherRepo.GetCaseById(id);
            if (model == null) return NotFound();

            ViewBag.ActiveCourt = model.CaseType;
            ViewBag.LitigantType = model.LitigantType;
            ViewBag.Advocates = _masterRepo.GetAllAdvocates();
            
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateOtherCourtsCase(OtherCourtsCase model)
        {
            try
            {
                var existingCase = _otherRepo.GetCaseById(model.CaseID);
                if (existingCase == null) return NotFound();

                model.DivisionID = existingCase.DivisionID;
                model.ModifiedBy = int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int userId) ? userId : null;

                // Preserve file paths if no new file is uploaded
                model.InterimOrderFilePath = existingCase.InterimOrderFilePath;
                model.FinalJudgmentFilePath = existingCase.FinalJudgmentFilePath;
                model.AppealJudgmentCopyPath = existingCase.AppealJudgmentCopyPath;

                // Handle Interim Order File Upload
                if (model.InterimOrderFile != null && model.InterimOrderFile.Length > 0)
                {
                    model.InterimOrderFilePath = await SaveOtherCourtsFileAsync(model.InterimOrderFile, "other_courts", existingCase.InterimOrderFilePath);
                }

                // Handle Final Judgment File Upload
                if (model.FinalJudgmentFile != null && model.FinalJudgmentFile.Length > 0)
                {
                    model.FinalJudgmentFilePath = await SaveOtherCourtsFileAsync(model.FinalJudgmentFile, "other_courts_judgments", existingCase.FinalJudgmentFilePath);
                }

                // Handle Appeal Judgment File Upload
                if (model.AppealJudgmentCopy != null && model.AppealJudgmentCopy.Length > 0)
                {
                    model.AppealJudgmentCopyPath = await SaveOtherCourtsFileAsync(model.AppealJudgmentCopy, "other_courts_appeals", existingCase.AppealJudgmentCopyPath);
                }

                if (_otherRepo.UpdateCase(model))
                {
                    TempData["SuccessMessage"] = "Case details updated successfully.";
                    return RedirectToAction("OtherCourtsCaseList", new { activeCourt = model.CaseType, litigantType = model.LitigantType });
                }
                
                TempData["ErrorMessage"] = "Failed to update case.";
                return RedirectToAction("OtherCourtsCaseEdit", new { id = model.CaseID });
            }
            catch (System.Exception ex)
            {
                TempData["ErrorMessage"] = "Error: " + ex.Message;
                return RedirectToAction("OtherCourtsCaseList", new { activeCourt = model.CaseType, litigantType = model.LitigantType });
            }
        }

        [AllowAnonymous]
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View();
        }
    }
}
