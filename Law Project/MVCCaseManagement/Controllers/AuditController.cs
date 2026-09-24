using System;
using System.Linq;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MVCCaseManagement.DAL;
using MVCCaseManagement.Models;

namespace MVCCaseManagement.Controllers
{
    [Authorize]
    public class AuditController : Controller
    {
        private readonly IEPRepository _epRepo;
        private readonly IMasterRepository _masterRepo;
        private readonly ICaseRepository _caseRepo;

        public AuditController(IEPRepository epRepo, IMasterRepository masterRepo, ICaseRepository caseRepo)
        {
            _epRepo = epRepo;
            _masterRepo = masterRepo;
            _caseRepo = caseRepo;
        }

        public IActionResult Index()
        {
            int divisionId = int.Parse(User.FindFirstValue("DivisionID") ?? "0");
            var eps = _epRepo.GetAllEPs(divisionId);
            return View(eps);
        }

        [HttpGet]
        public IActionResult Create()
        {
            int divisionId = int.Parse(User.FindFirstValue("DivisionID") ?? "0");
            var divisions = _masterRepo.GetAllDivisions();
            var div = divisions.FirstOrDefault(d => d.DivisionID == divisionId);

            var model = new EPViewModel
            {
                DivisionID = divisionId,
                DivisionName = div?.DivisionNameEnglish ?? "Unknown"
            };

            ViewBag.MACTs = _masterRepo.GetAllMACTs();
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(EPViewModel model)
        {
            // Sanitize and normalize payments
            if (model.Payments != null)
            {
                model.Payments = model.Payments.Where(p => p.Amount > 0).ToList();
                foreach (var p in model.Payments)
                {
                    if (p.PaymentDate == default)
                        p.PaymentDate = p.ChequeDate ?? DateTime.Today;
                }
            }

            if (string.IsNullOrWhiteSpace(model.EPNumber))
            {
                model.EPNumber = $"EP/{model.ArisingFromMVCNo}/{model.ArisingFromMVCYear ?? DateTime.Today.Year}";
            }

            ModelState.Remove("EPNumber");
            ModelState.Remove("DivisionName");

            if (ModelState.IsValid)
            {
                int epId = _epRepo.SaveEP(model);
                if (epId > 0)
                {
                    TempData["SuccessMessage"] = "Audit entry created successfully.";
                    return RedirectToAction(nameof(Index));
                }
                ModelState.AddModelError("", "Error creating audit entry.");
            }

            // Repopulate dropdowns on error
            ViewBag.MACTs = _masterRepo.GetAllMACTs();
            var divisions = _masterRepo.GetAllDivisions();
            var div = divisions.FirstOrDefault(d => d.DivisionID == model.DivisionID);
            model.DivisionName = div?.DivisionNameEnglish ?? "Unknown";

            return View(model);
        }

        [HttpGet]
        public IActionResult Manage(int id)
        {
            var model = _epRepo.GetEPById(id);
            if (model == null) return NotFound();

            // Populate Division Name for display
            var divisions = _masterRepo.GetAllDivisions();
            var div = divisions.FirstOrDefault(d => d.DivisionID == model.DivisionID);
            model.DivisionName = div?.DivisionNameEnglish ?? "Unknown";

            ViewBag.MACTs = _masterRepo.GetAllMACTs();
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Manage(EPViewModel model)
        {
            // Sanitize and normalize payments
            if (model.Payments != null)
            {
                model.Payments = model.Payments.Where(p => p.Amount > 0).ToList();
                foreach (var p in model.Payments)
                {
                    if (p.PaymentDate == default)
                        p.PaymentDate = p.ChequeDate ?? DateTime.Today;
                }
            }

            if (string.IsNullOrWhiteSpace(model.EPNumber))
            {
                var existing = _epRepo.GetEPById(model.EPID);
                model.EPNumber = existing?.EPNumber ?? $"EP/{model.ArisingFromMVCNo}/{model.ArisingFromMVCYear ?? DateTime.Today.Year}";
            }

            ModelState.Remove("EPNumber");
            ModelState.Remove("DivisionName");

            if (ModelState.IsValid)
            {
                bool success = _epRepo.UpdateEP(model);
                if (success)
                {
                    TempData["SuccessMessage"] = "Audit entry and payment records updated successfully.";
                    return RedirectToAction(nameof(Index));
                }
                ModelState.AddModelError("", "Error updating audit entry.");
            }

            // Repopulate dropdowns
            ViewBag.MACTs = _masterRepo.GetAllMACTs();
            var divisions = _masterRepo.GetAllDivisions();
            var div = divisions.FirstOrDefault(d => d.DivisionID == model.DivisionID);
            model.DivisionName = div?.DivisionNameEnglish ?? "Unknown";

            return View(model);
        }

        [HttpGet]
        public IActionResult FindCaseDetails(string mvcNo, string vehicleNo, DateTime accidentDate)
        {
            var caseDetails = _caseRepo.GetCaseByAuditCriteria(mvcNo, vehicleNo, accidentDate);
            if (caseDetails == null) return NotFound();

            return Json(new {
                caseID = caseDetails.CaseID,
                mvcYear = caseDetails.MVCYear,
                mactName = caseDetails.MACTName,
                divisionID = caseDetails.DivisionID,
                petitionerName = caseDetails.Petitioners.FirstOrDefault()?.PetitionerName ?? "",
                awardAmount = caseDetails.AdverseAward?.AwardAmount,
                interestRate = caseDetails.AdverseAward?.InterestRate,
                liabilityPercentage = caseDetails.AdverseAward?.LiabilityPercentage,
                petitionDate = caseDetails.AdverseAward?.ClaimPetitionDate?.ToString("yyyy-MM-dd")
            });
        }
    }
}
