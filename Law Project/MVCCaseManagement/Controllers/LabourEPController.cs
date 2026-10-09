using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MVCCaseManagement.DAL;
using MVCCaseManagement.Models;
using System.Security.Claims;
using MVCCaseManagement.Services.Audit;

namespace MVCCaseManagement.Controllers
{
    [Authorize]
    public class LabourEPController : Controller
    {
        private readonly ILabourEPRepository _epRepo;
        private readonly ILabourRepository _caseRepo;
        private readonly IArisingApplicationRepository _arisingRepo;
        private readonly IMasterRepository _masterRepo;
        private readonly ICaseActivityLogger _activityLogger;

        public LabourEPController(ILabourEPRepository epRepo, ILabourRepository caseRepo, IArisingApplicationRepository arisingRepo, IMasterRepository masterRepo, ICaseActivityLogger activityLogger)
        {
            _epRepo = epRepo;
            _caseRepo = caseRepo;
            _arisingRepo = arisingRepo;
            _masterRepo = masterRepo;
            _activityLogger = activityLogger;
        }

        public IActionResult Index(int page = 1, string search = "", string status = "all")
        {
            int pageSize = 10;
            int divisionId = int.Parse(User.FindFirstValue("DivisionID") ?? "0");
            bool isCentralOffice = divisionId == 0 || divisionId == 5;

            // Default CO view: Sent to CO if no filter active
            if (isCentralOffice && string.IsNullOrEmpty(search) && status == "all" && !Request.Query.ContainsKey("status"))
            {
                status = "SentToCO";
            }

            var eps = _epRepo.GetAllEPs(isCentralOffice ? 0 : divisionId, page, pageSize, search, status);
            var totalCount = _epRepo.GetTotalEPCount(isCentralOffice ? 0 : divisionId, search, status);

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = (int)Math.Ceiling((double)totalCount / pageSize);
            ViewBag.CurrentSearch = search;
            ViewBag.CurrentStatus = status;
            ViewBag.IsCentralOffice = isCentralOffice;

            return View(eps);
        }

        public IActionResult Create(int? caseId)
        {
            int divisionId = int.Parse(User.FindFirstValue("DivisionID") ?? "0");
            var divisions = _masterRepo.GetAllDivisions();
            var userDiv = divisions.FirstOrDefault(d => d.DivisionID == divisionId);

            ViewBag.LabourCourts = _masterRepo.GetAllLabourCourts();
            ViewBag.MACTs = _masterRepo.GetAllMACTs();
            ViewBag.Advocates = _masterRepo.GetAllLabourAdvocates();

            var model = new LabourEPViewModel
            {
                DivisionID = divisionId,
                DivisionName = userDiv?.DivisionNameEnglish ?? "Unknown"
            };

            if (caseId.HasValue)
            {
                var caseDetails = _caseRepo.GetCaseById(caseId.Value);
                if (caseDetails != null)
                {
                    model.CaseID = caseId.Value;
                    model.ArisingFromCaseNo = caseDetails.CaseNumber;
                    model.ArisingFromCaseYear = caseDetails.CaseYear;
                    model.ArisingFromCourt = caseDetails.CourtName;
                }
                else
                {
                    var arisingDetails = _arisingRepo.GetById(caseId.Value);
                    if (arisingDetails != null)
                    {
                        model.CaseID = arisingDetails.ParentCaseID ?? arisingDetails.ArisingID;
                        model.ArisingFromCaseNo = arisingDetails.CaseNumber;
                        model.ArisingFromCaseYear = arisingDetails.CaseYear;
                        model.ArisingFromCourt = arisingDetails.CourtName;
                    }
                }
            }
            return View(model);
        }

        [HttpGet]
        public IActionResult Details(int id)
        {
            var ep = _epRepo.GetEPById(id);
            if (ep == null) return NotFound();

            var divisions = _masterRepo.GetAllDivisions();
            var userDiv = divisions.FirstOrDefault(d => d.DivisionID == ep.DivisionID);
            ep.DivisionName = userDiv?.DivisionNameEnglish ?? "Unknown";

            ArisingApplication? arisingApp = null;
            LabourCase? primaryCase = null;

            // 1. Fetch matching Arising Application
            if (!string.IsNullOrWhiteSpace(ep.ArisingFromCaseNo) && ep.ArisingFromCaseYear.HasValue)
            {
                var arisingMatches = _arisingRepo.GetAll(0, 1, 10, ep.ArisingFromCaseNo.Trim());
                arisingApp = arisingMatches.FirstOrDefault(a => (a.CaseNumber ?? "").Trim().Equals(ep.ArisingFromCaseNo.Trim(), StringComparison.OrdinalIgnoreCase) && a.CaseYear == ep.ArisingFromCaseYear.Value);
            }
            if (arisingApp == null && ep.CaseID > 0)
            {
                arisingApp = _arisingRepo.GetById(ep.CaseID);
            }

            // 2. Fetch linked Primary Labour Case
            if (arisingApp != null && arisingApp.ParentCaseID.HasValue && arisingApp.ParentCaseID.Value > 0)
            {
                primaryCase = _caseRepo.GetCaseById(arisingApp.ParentCaseID.Value);
            }
            if (primaryCase == null && ep.CaseID > 0)
            {
                primaryCase = _caseRepo.GetCaseById(ep.CaseID);
            }
            if (primaryCase == null && !string.IsNullOrWhiteSpace(ep.ArisingFromCaseNo) && ep.ArisingFromCaseYear.HasValue)
            {
                primaryCase = _caseRepo.GetCaseByNumber(ep.ArisingFromCaseNo.Trim(), ep.ArisingFromCaseYear.Value);
            }

            ViewBag.LinkedArisingApp = arisingApp;
            ViewBag.LinkedPrimaryCase = primaryCase;
            return View(ep);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(LabourEPViewModel model)
        {
            if (model.CaseID <= 0)
            {
                ModelState.AddModelError("CaseID", "Please click 'Link Application' to confirm linking this EP with the Arising Application.");
            }

            if (ModelState.IsValid)
            {
                try
                {
                    int epId = _epRepo.SaveEP(model);
                    if (epId > 0)
                    {
                        _ = _activityLogger.LogCaseCreatedAsync(
                            "LABOUR_EP",
                            epId,
                            $"Labour EP {model.EPNumber}/{model.EPYear}",
                            null,
                            model.EPCourt,
                            model,
                            $"Registered Labour Execution Petition #{model.EPNumber}/{model.EPYear} (Linked to Arising App: {model.ArisingFromCaseNo}/{model.ArisingFromCaseYear}, Court: {model.EPCourt})");

                        TempData["SuccessMessage"] = $"Labour EP #{model.EPNumber}/{model.EPYear} saved and successfully linked to Arising Application No {model.ArisingFromCaseNo}/{model.ArisingFromCaseYear}!";
                        return RedirectToAction(nameof(Index));
                    }
                    else
                    {
                        ModelState.AddModelError("", "Failed to save EP details. Please try again.");
                    }
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", "Error saving EP details: " + ex.Message);
                    TempData["ErrorMessage"] = "Error: " + ex.Message;
                }
            }
            
            ViewBag.LabourCourts = _masterRepo.GetAllLabourCourts();
            ViewBag.MACTs = _masterRepo.GetAllMACTs();
            ViewBag.Advocates = _masterRepo.GetAllLabourAdvocates();
            return View(model);
        }

        public IActionResult Edit(int id)
        {
            var ep = _epRepo.GetEPById(id);
            if (ep == null) return NotFound();

            var divisions = _masterRepo.GetAllDivisions();
            var userDiv = divisions.FirstOrDefault(d => d.DivisionID == ep.DivisionID);
            ep.DivisionName = userDiv?.DivisionNameEnglish ?? "Unknown";
            
            ViewBag.LabourCourts = _masterRepo.GetAllLabourCourts();
            ViewBag.MACTs = _masterRepo.GetAllMACTs();
            ViewBag.Advocates = _masterRepo.GetAllLabourAdvocates();
            return View(ep);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Edit(LabourEPViewModel model)
        {
            // Auto-link to parent Labour Case if CaseID is not yet set
            if (model.CaseID <= 0 && !string.IsNullOrWhiteSpace(model.ArisingFromCaseNo) && model.ArisingFromCaseYear.HasValue)
            {
                var parentCase = _caseRepo.GetCaseByNumber(model.ArisingFromCaseNo.Trim(), model.ArisingFromCaseYear.Value);
                if (parentCase != null)
                {
                    model.CaseID = parentCase.CaseID;
                }
                else
                {
                    var arisingMatches = _arisingRepo.GetAll(0, 1, 10, model.ArisingFromCaseNo.Trim());
                    var matchedArising = arisingMatches.FirstOrDefault(a => a.CaseNumber.Trim().Equals(model.ArisingFromCaseNo.Trim(), StringComparison.OrdinalIgnoreCase) && a.CaseYear == model.ArisingFromCaseYear.Value);
                    if (matchedArising != null)
                    {
                        model.CaseID = matchedArising.ParentCaseID ?? matchedArising.ArisingID;
                    }
                }
            }

            if (ModelState.IsValid)
            {
                try
                {
                    bool updated = _epRepo.UpdateEP(model);
                    if (updated)
                    {
                        _ = _activityLogger.LogCaseUpdatedAsync(
                            "LABOUR_EP",
                            model.EPID,
                            $"Labour EP {model.EPNumber}/{model.EPYear}",
                            null,
                            model.EPCourt,
                            null,
                            model,
                            $"Updated Labour Execution Petition #{model.EPNumber}/{model.EPYear} (Court: {model.EPCourt}, Stage: {model.EPStatus ?? "N/A"})");

                        TempData["SuccessMessage"] = $"Labour EP #{model.EPNumber}/{model.EPYear} updated and successfully linked to Arising Application No {model.ArisingFromCaseNo}/{model.ArisingFromCaseYear}!";
                        return RedirectToAction(nameof(Index));
                    }
                    else
                    {
                        ModelState.AddModelError("", "Failed to update EP details.");
                    }
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", "Error updating EP details: " + ex.Message);
                    TempData["ErrorMessage"] = "Error: " + ex.Message;
                }
            }
            ViewBag.LabourCourts = _masterRepo.GetAllLabourCourts();
            ViewBag.MACTs = _masterRepo.GetAllMACTs();
            ViewBag.Advocates = _masterRepo.GetAllLabourAdvocates();
            return View(model);
        }

        [HttpGet]
        public IActionResult GetCaseStatus(string caseNo, int caseYear)
        {
            if (string.IsNullOrEmpty(caseNo) || caseYear == 0) return Json(new { count = 0, cases = new object[0] });
            
            var matches = _caseRepo.GetCasesByNumber(caseNo, caseYear);
            var list = matches.Select(c => new {
                caseId = c.CaseID,
                caseNo = c.CaseNumber,
                caseYear = c.CaseYear,
                status = c.DisposalResult ?? c.CaseStatus,
                disposalResult = c.DisposalResult ?? c.CaseStatus,
                currentStage = c.CurrentStage,
                stage = c.CurrentStage,
                courtName = c.CourtName ?? "Labour Court",
                court = c.CourtName ?? "Labour Court",
                divisionName = c.DivisionName ?? "Unknown Division",
                petitionerName = c.PetitionerName ?? "N/A",
                petitioner = c.PetitionerName ?? "N/A",
                awardDetails = c.AwardDetails ?? c.FavorRemark
            }).ToList<object>();

            try
            {
                var arisingList = _arisingRepo.GetAll(0, 1, 50, caseNo);
                foreach (var a in arisingList)
                {
                    if ((a.CaseNumber ?? "").Trim().Equals(caseNo.Trim(), StringComparison.OrdinalIgnoreCase) && a.CaseYear == caseYear)
                    {
                        int id = a.ParentCaseID ?? a.ArisingID;
                        if (!list.Any(x => (int)((dynamic)x).caseId == id))
                        {
                            list.Add(new {
                                caseId = id,
                                caseNo = a.CaseNumber,
                                caseYear = a.CaseYear ?? caseYear,
                                status = a.DisposalResult ?? a.CaseStatus,
                                disposalResult = a.DisposalResult ?? a.CaseStatus,
                                currentStage = a.CurrentStage,
                                stage = a.CurrentStage,
                                courtName = a.CourtName ?? "Labour Court",
                                court = a.CourtName ?? "Labour Court",
                                divisionName = a.DivisionName ?? "Unknown Division",
                                petitionerName = a.PetitionerName ?? "N/A",
                                petitioner = a.PetitionerName ?? "N/A",
                                awardDetails = a.AwardDetails ?? a.FavorRemark
                            });
                        }
                    }
                }
            }
            catch { }

            return Json(new { count = list.Count, cases = list });
        }

        [HttpGet]
        public IActionResult GetLabourCaseDetails(string caseNo, int caseYear, int? caseId)
        {
            if (caseId.HasValue && caseId.Value > 0)
            {
                var singleCase = _caseRepo.GetCaseById(caseId.Value);
                if (singleCase != null)
                {
                    return Json(new {
                        count = 1,
                        cases = new[] {
                            new {
                                caseId = singleCase.CaseID,
                                caseNo = singleCase.CaseNumber,
                                caseYear = singleCase.CaseYear,
                                status = singleCase.CaseStatus,
                                disposalResult = singleCase.DisposalResult ?? singleCase.CaseStatus,
                                currentStage = singleCase.CurrentStage,
                                courtName = singleCase.CourtName ?? "Labour Court",
                                divisionName = singleCase.DivisionName ?? "Unknown Division",
                                petitionerName = singleCase.PetitionerName ?? "N/A",
                                awardDetails = singleCase.AwardDetails ?? singleCase.FavorRemark
                            }
                        }
                    });
                }

                var arisingCase = _arisingRepo.GetById(caseId.Value);
                if (arisingCase != null)
                {
                    return Json(new {
                        count = 1,
                        cases = new[] {
                            new {
                                caseId = arisingCase.ParentCaseID ?? arisingCase.ArisingID,
                                caseNo = arisingCase.CaseNumber,
                                caseYear = arisingCase.CaseYear ?? caseYear,
                                status = arisingCase.CaseStatus,
                                disposalResult = arisingCase.DisposalResult ?? arisingCase.CaseStatus,
                                currentStage = arisingCase.CurrentStage,
                                courtName = arisingCase.CourtName ?? "Labour Court",
                                divisionName = arisingCase.DivisionName ?? "Unknown Division",
                                petitionerName = arisingCase.PetitionerName ?? "N/A",
                                awardDetails = arisingCase.AwardDetails ?? arisingCase.FavorRemark
                            }
                        }
                    });
                }
            }

            if (string.IsNullOrEmpty(caseNo) || caseYear == 0) return Json(new { count = 0, cases = new object[0] });
            
            var matches = _caseRepo.GetCasesByNumber(caseNo, caseYear);
            var list = matches.Select(c => new {
                caseId = c.CaseID,
                caseNo = c.CaseNumber,
                caseYear = c.CaseYear,
                status = c.CaseStatus,
                disposalResult = c.DisposalResult ?? c.CaseStatus,
                currentStage = c.CurrentStage,
                courtName = c.CourtName ?? "Labour Court",
                divisionName = c.DivisionName ?? "Unknown Division",
                petitionerName = c.PetitionerName ?? "N/A",
                awardDetails = c.AwardDetails ?? c.FavorRemark
            }).ToList<object>();

            try
            {
                var arisingList = _arisingRepo.GetAll(0, 1, 50, caseNo);
                foreach (var a in arisingList)
                {
                    if ((a.CaseNumber ?? "").Trim().Equals(caseNo.Trim(), StringComparison.OrdinalIgnoreCase) && a.CaseYear == caseYear)
                    {
                        int id = a.ParentCaseID ?? a.ArisingID;
                        if (!list.Any(x => (int)((dynamic)x).caseId == id))
                        {
                            list.Add(new {
                                caseId = id,
                                caseNo = a.CaseNumber,
                                caseYear = a.CaseYear ?? caseYear,
                                status = a.CaseStatus,
                                disposalResult = a.DisposalResult ?? a.CaseStatus,
                                currentStage = a.CurrentStage,
                                courtName = a.CourtName ?? "Labour Court",
                                divisionName = a.DivisionName ?? "Unknown Division",
                                petitionerName = a.PetitionerName ?? "N/A",
                                awardDetails = a.AwardDetails ?? a.FavorRemark
                            });
                        }
                    }
                }
            }
            catch { }

            return Json(new { count = list.Count, cases = list });
        }
    }
}
