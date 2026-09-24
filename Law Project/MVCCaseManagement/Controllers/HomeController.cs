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

        public HomeController(ICaseRepository caseRepo, IOtherCourtsRepository otherRepo, IMasterRepository masterRepo, IWebHostEnvironment env)
        {
            _caseRepo = caseRepo;
            _otherRepo = otherRepo;
            _masterRepo = masterRepo;
            _env = env;
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
            var stats = _otherRepo.GetDashboardStats(divisionId, "PSC", "Claimant");
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
            var stats = _otherRepo.GetDashboardStats(divisionId, "CC", "Claimant");
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
            var stats = _otherRepo.GetDashboardStats(divisionId, "Consumer", "Claimant");
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
            var stats = _otherRepo.GetDashboardStats(divisionId, "LAC", "Claimant");
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
            var stats = _otherRepo.GetDashboardStats(divisionId, "ECA", "Claimant");
            return View("ECADashboard", stats);
        }

        public IActionResult ECAForm()
        {
            ViewBag.ActiveCourt = "ECA";
            ViewBag.LitigantType = "Claimant";
            ViewBag.Advocates = _masterRepo.GetAllAdvocates();
            return View("ECA");
        }

        public IActionResult OtherCourtsCaseList(string activeCourt = "OS", string litigantType = "Claimant")
        {
            var divIdString = User.FindFirstValue("DivisionID");
            int divisionId = int.TryParse(divIdString, out int id) ? id : 0;

            ViewBag.ActiveCourt = activeCourt;
            ViewBag.LitigantType = litigantType;

            var cases = _otherRepo.GetAllCases(divisionId, activeCourt, litigantType);
            return View(cases);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult SaveOtherCourtsCase(OtherCourtsCase model)
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
                    string uploadsFolder = Path.Combine(_env.WebRootPath, "uploads", "other_courts");
                    if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

                    string uniqueFileName = Guid.NewGuid().ToString() + "_" + Path.GetFileName(model.InterimOrderFile.FileName);
                    string filePath = Path.Combine(uploadsFolder, uniqueFileName);

                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        model.InterimOrderFile.CopyTo(fileStream);
                    }
                    model.InterimOrderFilePath = "/uploads/other_courts/" + uniqueFileName;
                }

                // Handle Final Judgment File Upload
                if (model.FinalJudgmentFile != null && model.FinalJudgmentFile.Length > 0)
                {
                    string uploadsFolder = Path.Combine(_env.WebRootPath, "uploads", "other_courts_judgments");
                    if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

                    string uniqueFileName = Guid.NewGuid().ToString() + "_" + Path.GetFileName(model.FinalJudgmentFile.FileName);
                    string filePath = Path.Combine(uploadsFolder, uniqueFileName);

                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        model.FinalJudgmentFile.CopyTo(fileStream);
                    }
                    model.FinalJudgmentFilePath = "/uploads/other_courts_judgments/" + uniqueFileName;
                }

                // Handle Appeal Judgment File Upload
                if (model.AppealJudgmentCopy != null && model.AppealJudgmentCopy.Length > 0)
                {
                    string uploadsFolder = Path.Combine(_env.WebRootPath, "uploads", "other_courts_appeals");
                    if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

                    string uniqueFileName = Guid.NewGuid().ToString() + "_" + Path.GetFileName(model.AppealJudgmentCopy.FileName);
                    string filePath = Path.Combine(uploadsFolder, uniqueFileName);

                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        model.AppealJudgmentCopy.CopyTo(fileStream);
                    }
                    model.AppealJudgmentCopyPath = "/uploads/other_courts_appeals/" + uniqueFileName;
                }

                int newId = _otherRepo.SaveCase(model);
                if (newId > 0)
                {
                    TempData["SuccessMessage"] = "Case details saved successfully.";
                    return RedirectToAction("OtherCourts", new { activeCourt = model.CaseType });
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
            
            return View(model);
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
        public IActionResult UpdateOtherCourtsCase(OtherCourtsCase model)
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
                    string uploadsFolder = Path.Combine(_env.WebRootPath, "uploads", "other_courts");
                    if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

                    string uniqueFileName = Guid.NewGuid().ToString() + "_" + Path.GetFileName(model.InterimOrderFile.FileName);
                    string filePath = Path.Combine(uploadsFolder, uniqueFileName);

                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        model.InterimOrderFile.CopyTo(fileStream);
                    }
                    model.InterimOrderFilePath = "/uploads/other_courts/" + uniqueFileName;
                }

                // Handle Final Judgment File Upload
                if (model.FinalJudgmentFile != null && model.FinalJudgmentFile.Length > 0)
                {
                    string uploadsFolder = Path.Combine(_env.WebRootPath, "uploads", "other_courts_judgments");
                    if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

                    string uniqueFileName = Guid.NewGuid().ToString() + "_" + Path.GetFileName(model.FinalJudgmentFile.FileName);
                    string filePath = Path.Combine(uploadsFolder, uniqueFileName);

                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        model.FinalJudgmentFile.CopyTo(fileStream);
                    }
                    model.FinalJudgmentFilePath = "/uploads/other_courts_judgments/" + uniqueFileName;
                }

                // Handle Appeal Judgment File Upload
                if (model.AppealJudgmentCopy != null && model.AppealJudgmentCopy.Length > 0)
                {
                    string uploadsFolder = Path.Combine(_env.WebRootPath, "uploads", "other_courts_appeals");
                    if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

                    string uniqueFileName = Guid.NewGuid().ToString() + "_" + Path.GetFileName(model.AppealJudgmentCopy.FileName);
                    string filePath = Path.Combine(uploadsFolder, uniqueFileName);

                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        model.AppealJudgmentCopy.CopyTo(fileStream);
                    }
                    model.AppealJudgmentCopyPath = "/uploads/other_courts_appeals/" + uniqueFileName;
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
    }
}
