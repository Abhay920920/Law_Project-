using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MVCCaseManagement.DAL;
using MVCCaseManagement.Models;
using System.Security.Claims;

namespace MVCCaseManagement.Controllers
{
    [Authorize]
    public class EPController : Controller
    {
        private readonly IEPRepository _epRepo;
        private readonly ICaseRepository _caseRepo;
        private readonly IMasterRepository _masterRepo;

        public EPController(IEPRepository epRepo, ICaseRepository caseRepo, IMasterRepository masterRepo)
        {
            _epRepo = epRepo;
            _caseRepo = caseRepo;
            _masterRepo = masterRepo;
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

            var model = new EPViewModel
            {
                DivisionID = divisionId,
                DivisionName = userDiv?.DivisionNameEnglish ?? (User.IsInRole("Admin") ? "Central Office (Admin)" : "Unknown")
            };

            if (caseId.HasValue)
            {
                var caseDetails = _caseRepo.GetCaseById(caseId.Value);
                if (caseDetails != null)
                {
                    model.CaseID = caseId.Value;
                    model.ArisingFromMVCNo = caseDetails.MVCNo;
                    model.ArisingFromMVCYear = caseDetails.MVCYear;
                    model.ArisingFromMACT = caseDetails.MACTName;
                    model.VehicleNo = caseDetails.VehicleNo;
                    model.AccidentDate = caseDetails.AccidentDate;
                    model.CNRNumber = caseDetails.CNRNumber;
                    model.EstCode = caseDetails.EstCode;
                    model.CaseTypeCode = caseDetails.CaseTypeCode;
                    model.DivisionID = caseDetails.DivisionID;
                    model.DivisionName = caseDetails.DivisionName;

                    if (caseDetails.AdverseAward != null)
                    {
                        model.AwardAmount = caseDetails.AdverseAward.AwardAmount;
                        model.InterestRate = caseDetails.AdverseAward.InterestRate;
                        model.LiabilityPercentage = caseDetails.AdverseAward.LiabilityPercentage;
                        model.OriginalMVCStatus = caseDetails.DisposalResult ?? "Against";
                        if (!string.IsNullOrEmpty(caseDetails.AdverseAward.EPNumber))
                            model.EPNumber = caseDetails.AdverseAward.EPNumber;
                        if (!string.IsNullOrEmpty(caseDetails.AdverseAward.EPCourt))
                            model.EPCourt = caseDetails.AdverseAward.EPCourt;
                        if (!string.IsNullOrEmpty(caseDetails.AdverseAward.EPStage))
                            model.EPStatus = caseDetails.AdverseAward.EPStage;
                        if (caseDetails.AdverseAward.EPNextHearingDate.HasValue)
                            model.NextHearingDate = caseDetails.AdverseAward.EPNextHearingDate;
                    }
                }
            }
            ViewBag.MACTs = _masterRepo.GetAllMACTs();
            ViewBag.Advocates = _masterRepo.GetAllLabourAdvocates();
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(EPViewModel model)
        {
            if (model.CaseID == 0)
            {
                ModelState.AddModelError("CaseID", "Please search and select an MVC case to link with this EP.");
            }
            else if (model.DivisionID == 0)
            {
                var caseDetails = _caseRepo.GetCaseById(model.CaseID);
                if (caseDetails != null)
                {
                    model.DivisionID = caseDetails.DivisionID;
                    model.DivisionName = caseDetails.DivisionName;
                }
            }

            if (ModelState.IsValid)
            {
                try
                {
                    int epId = _epRepo.SaveEP(model);
                    if (epId > 0)
                    {
                        TempData["SuccessMessage"] = "EP Details saved successfully!";
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
            
            ViewBag.MACTs = _masterRepo.GetAllMACTs();
            ViewBag.Advocates = _masterRepo.GetAllLabourAdvocates();
            return View(ep);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Edit(EPViewModel model)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    bool updated = _epRepo.UpdateEP(model);
                    if (updated)
                    {
                        TempData["SuccessMessage"] = "EP Details updated successfully!";
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
            ViewBag.MACTs = _masterRepo.GetAllMACTs();
            ViewBag.Advocates = _masterRepo.GetAllLabourAdvocates();
            return View(model);
        }

        [HttpGet]
        public IActionResult GetCaseStatus(string mvcNo, int mvcYear, int mactId)
        {
            if (string.IsNullOrEmpty(mvcNo) || mvcYear == 0 || mactId == 0) return Json(null);
            
            var caseDetails = _caseRepo.GetCaseByMVCDetails(mvcNo, mvcYear, mactId);
            if (caseDetails != null)
            {
                return Json(new { 
                    caseId = caseDetails.CaseID,
                    status = caseDetails.DisposalResult,
                    stage = caseDetails.CurrentStage,
                    mact = caseDetails.MACTName,
                    petitioner = caseDetails.Petitioners.FirstOrDefault()?.PetitionerName ?? "N/A",
                    isEPFiled = caseDetails.AdverseAward?.IsEPFiled ?? false,
                    divisionId = caseDetails.DivisionID,
                    divisionName = caseDetails.DivisionName,
                    vehicleNo = caseDetails.VehicleNo,
                    accidentDate = caseDetails.AccidentDate.HasValue ? caseDetails.AccidentDate.Value.ToString("yyyy-MM-dd") : null,
                    awardAmount = caseDetails.AdverseAward?.AwardAmount,
                    interestRate = caseDetails.AdverseAward?.InterestRate,
                    liabilityPercentage = caseDetails.AdverseAward?.LiabilityPercentage,
                    epNumber = caseDetails.AdverseAward?.EPNumber,
                    epCourt = caseDetails.AdverseAward?.EPCourt,
                    epStage = caseDetails.AdverseAward?.EPStage,
                    epNextHearingDate = caseDetails.AdverseAward?.EPNextHearingDate.HasValue == true ? caseDetails.AdverseAward.EPNextHearingDate.Value.ToString("yyyy-MM-dd") : null,
                    cnrNumber = caseDetails.CNRNumber,
                    estCode = caseDetails.EstCode,
                    caseTypeCode = caseDetails.CaseTypeCode
                });
            }
            return Json(null);
        }
    }
}
