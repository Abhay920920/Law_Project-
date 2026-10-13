using Microsoft.AspNetCore.Mvc;
using MVCCaseManagement.Common;
using MVCCaseManagement.DAL;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace MVCCaseManagement.Controllers
{
    [Authorize]
    public class DashboardController : Controller
    {
        private readonly ICaseRepository _caseRepo;
        private readonly ILabourRepository _labourRepo;

        public DashboardController(ICaseRepository caseRepo, ILabourRepository labourRepo)
        {
            _caseRepo = caseRepo;
            _labourRepo = labourRepo;
        }
        public IActionResult Index()
        {
            var role = User.FindFirstValue(ClaimTypes.Role);
            var divIdString = User.FindFirstValue("DivisionID");
            int divisionId = int.TryParse(divIdString, out int id) ? id : 0;

            ViewBag.Username = User.Identity?.Name;
            ViewBag.FullName = User.FindFirstValue("FullName");
            ViewBag.RoleName = role;
            ViewBag.DivisionName = User.FindFirstValue("DivisionName");

            var stats = (role == "Admin" || divisionId == 5) 
                ? _caseRepo.GetDashboardStats(0) 
                : _caseRepo.GetDashboardStats(divisionId);

            ViewBag.TotalCases = stats.TotalCases;
            ViewBag.PendingCases = stats.PendingCases;
            ViewBag.FavorCases = stats.FavorCases;
            ViewBag.AgainstCases = stats.AgainstCases;
            ViewBag.TotalDisposed = stats.FavorCases + stats.AgainstCases;
            
            ViewBag.AppealCount = stats.AppealCount;
            ViewBag.CloseCount = stats.CloseCount;
            ViewBag.PendingCompetentAuthorityCount = stats.PendingCompetentAuthorityCount;
            ViewBag.PendingDecisionCount = stats.PendingDecisionCount;

            // New stats
            ViewBag.CasesThisMonth = stats.CasesThisMonth;
            ViewBag.UpcomingHearingsCount = stats.UpcomingHearingsCount;
            ViewBag.TotalAwardAmountAgainst = stats.TotalAwardAmountAgainst;
            ViewBag.MonthlyAwardAmountAgainst = stats.MonthlyAwardAmountAgainst;
            ViewBag.SentToCOCount = stats.SentToCOCount;
            
            ViewBag.MVCCount = stats.MVCCount;
            ViewBag.LabourCount = stats.LabourCount;
            ViewBag.GratuityCount = stats.GratuityCount;

            ViewBag.SLPPendingCount = stats.SLPPendingCount;
            ViewBag.MFAPendingCount = stats.MFAPendingCount;
            ViewBag.CorpMFAPendingCount = stats.CorpMFAPendingCount;
            ViewBag.ClaimantMFAPendingCount = stats.ClaimantMFAPendingCount;
            ViewBag.NoActionTakenCount = stats.NoActionTakenCount;

            // Show today's cases by default
            DateTime today = DateTime.Today;
            ViewBag.SelectedDate = today.ToString("yyyy-MM-dd");
            ViewBag.RecentCases = (role == "Admin" || divisionId == 5)
                ? _caseRepo.GetCasesByHearingDate(today, 0)
                : _caseRepo.GetCasesByHearingDate(today, divisionId);

            // Notifications logic
            if (divisionId != 0 && divisionId != 5)
            {
                ViewBag.IncomingTransfers = _labourRepo.GetRecentTransfers(divisionId);
            }

            return View();
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

            var role = User.FindFirstValue(ClaimTypes.Role);
            var divIdString = User.FindFirstValue("DivisionID");
            int divisionId = int.TryParse(divIdString, out int id) ? id : 0;

            var cases = (role == "Admin" || divisionId == 5)
                ? _caseRepo.GetCasesByHearingDate(hearingDate, 0, endDateParsed)
                : _caseRepo.GetCasesByHearingDate(hearingDate, divisionId, endDateParsed);

            return PartialView("_DashboardRecentCases", cases);
        }
    }
}
