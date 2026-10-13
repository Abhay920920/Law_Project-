using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Authorization;
using MVCCaseManagement.DAL;
using MVCCaseManagement.Models;
using MVCCaseManagement.Common;
using MVCCaseManagement.Services.Audit;
using System.Security.Claims;

namespace MVCCaseManagement.Controllers
{
    [Authorize]
    public class GratuityController : Controller
    {
        private readonly IGratuityRepository _gratuityRepo;
        private readonly IMasterRepository _masterRepo;
        private readonly ICaseRepository _caseRepo;
        private readonly IWebHostEnvironment _environment;
        private readonly ICaseActivityLogger _activityLogger;
        private readonly ILogger<GratuityController> _logger;

        public GratuityController(IGratuityRepository gratuityRepo, IMasterRepository masterRepo, ICaseRepository caseRepo, IWebHostEnvironment environment, ICaseActivityLogger activityLogger, ILogger<GratuityController> logger)
        {
            _gratuityRepo = gratuityRepo;
            _masterRepo = masterRepo;
            _caseRepo = caseRepo;
            _environment = environment;
            _activityLogger = activityLogger;
            _logger = logger;
        }

        public async Task<IActionResult> Index()
        {
            var divIdString = User.FindFirst("DivisionID")?.Value;
            bool isCentralOffice = string.IsNullOrEmpty(divIdString) || divIdString == "0" || divIdString == "5";

            var stats = await _gratuityRepo.GetDashboardStats(isCentralOffice ? 0 : (int.TryParse(divIdString, out int id) ? id : 0));
            
            ViewBag.TotalCases = stats.TotalCases;
            ViewBag.PendingCases = stats.PendingCases;
            ViewBag.FavorCases = stats.FavorCases;
            ViewBag.AgainstCases = stats.AgainstCases;
            ViewBag.SentToCOCount = stats.SentToCOCount;
            ViewBag.NoActionTakenCount = stats.NoActionTakenCount;
            ViewBag.WPPendingCount = stats.WPPendingCount;
            ViewBag.WritAppealPendingCount = stats.WritAppealPendingCount;
            ViewBag.SLPPendingCount = stats.SLPPendingCount;
            ViewBag.PGACRPendingCount = stats.PGACRPendingCount;
            ViewBag.PGAApplCRPendingCount = stats.PGAApplCRPendingCount;
            ViewBag.CasesThisMonth = stats.CasesThisMonth;
            ViewBag.UpcomingHearingsCount = stats.UpcomingHearingsCount;
            ViewBag.FeasibilityReviewCount = stats.FeasibilityReviewCount;
            ViewBag.TotalAwardAmountAgainst = stats.TotalAwardAmountAgainst;
            ViewBag.TotalClaimedAmount = stats.TotalClaimedAmount;
            ViewBag.PendingCLOLOCount = stats.PendingCLOLOCount;
            ViewBag.PendingCompetentAuthorityCount = stats.PendingCompetentAuthorityCount;

            // Show today's hearings by default
            DateTime today = DateTime.Today;
            ViewBag.SelectedDate = today.ToString("yyyy-MM-dd");
            ViewBag.RecentCases = await _gratuityRepo.GetCasesByHearingDate(today, isCentralOffice ? 0 : (int.TryParse(divIdString, out int dId) ? dId : 0));

            var cases = await _gratuityRepo.GetAllCases(isCentralOffice ? 0 : (int.TryParse(divIdString, out int cId) ? cId : 0), 1, 10);
            return View(cases);
        }

        [HttpGet]
        public IActionResult AuditCalculation()
        {
            var divIdString = User.FindFirst("DivisionID")?.Value;
            bool isCentralOffice = string.IsNullOrEmpty(divIdString) || divIdString == "0" || divIdString == "5";
            ViewBag.IsCentralOffice = isCentralOffice;
            return View();
        }
        [HttpGet]
        public async Task<IActionResult> FindCaseDetails(string pgaNumber)
        {
            if (string.IsNullOrWhiteSpace(pgaNumber)) return BadRequest("Invalid PGA Number");

            var divIdString = User.FindFirst("DivisionID")?.Value;
            bool isCO = string.IsNullOrEmpty(divIdString) || divIdString == "0" || divIdString == "5";
            int divisionId = int.TryParse(divIdString, out int id) ? id : 0;

            var cases = await _gratuityRepo.GetAllCases(isCO ? 0 : divisionId, 1, 50, pgaNumber, "all");
            var gratuityCase = cases.FirstOrDefault(c => c.PGANumber != null && c.PGANumber.Equals(pgaNumber, StringComparison.OrdinalIgnoreCase));

            if (gratuityCase == null)
            {
                gratuityCase = cases.FirstOrDefault(c => c.PGANumber != null && c.PGANumber.Contains(pgaNumber, StringComparison.OrdinalIgnoreCase));
            }

            if (gratuityCase == null) return NotFound();

            // Fetch the full case to get Payments
            var fullCase = await _gratuityRepo.GetCaseById(gratuityCase.CaseID) ?? gratuityCase;

            var allPayments = new List<object>();
            if (fullCase.Payments != null)
            {
                allPayments.AddRange(fullCase.Payments.Select(p => new {
                    amount = p.Amount,
                    chequeDate = p.ChequeDate?.ToString("yyyy-MM-dd"),
                    chequeNumber = p.ChequeNumber,
                    remarks = "Principal Payment"
                }));
            }
            if (fullCase.InterestPayments != null)
            {
                allPayments.AddRange(fullCase.InterestPayments.Select(ip => new {
                    amount = ip.Amount,
                    chequeDate = ip.ChequeDate?.ToString("yyyy-MM-dd"),
                    chequeNumber = ip.ChequeNumber,
                    remarks = "Interest Payment"
                }));
            }

            return Json(new {
                caseID = fullCase.CaseID,
                pgaNumber = fullCase.PGANumber,
                employeeName = fullCase.ClaimantName,
                awardAmount = fullCase.OrderedAmount_CA ?? fullCase.GratuityAmount_CA_Reg ?? fullCase.GratuityAmount_Corp_Reg ?? fullCase.GratuityAmount_Corp_Act ?? 0,
                interestRate = fullCase.InterestRate ?? 10,
                retirementDate = fullCase.RetirementDate?.ToString("yyyy-MM-dd"),
                courtType = fullCase.CourtType,
                caseStatus = fullCase.CaseStatus,
                designation = fullCase.ClaimantDesignation,
                workingStatus = fullCase.WorkingStatus,
                entrustmentNo = fullCase.EntrustmentNo,
                entrustmentDate = fullCase.EntrustmentDate?.ToString("dd-MM-yyyy"),
                advocateName = fullCase.AdvocateName,
                currentStage = fullCase.CurrentStage,
                nextHearingDate = fullCase.NextHearingDate?.ToString("dd-MM-yyyy"),
                amountClaimed = fullCase.AmountClaimed,
                appointmentDate = fullCase.AppointmentDate?.ToString("dd-MM-yyyy"),
                totalServicePeriod = fullCase.TotalServicePeriod,
                
                gratuityCorpReg = fullCase.GratuityAmount_Corp_Reg,
                gratuityCorpAct = fullCase.GratuityAmount_Corp_Act,
                gratuityCAReg = fullCase.GratuityAmount_CA_Reg,
                gratuityCAAct = fullCase.GratuityAmount_CA_Act,
                orderedCA = fullCase.OrderedAmount_CA,
                actualPaid = fullCase.ActualPaidAmount,
                actualPaidCA = fullCase.ActualPaidAmount_CA,
                
                payments = allPayments
            });
        }

        [HttpGet]
        public async Task<IActionResult> GetCasesByDate(string date, string? endDate = null)
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

            var cases = await _gratuityRepo.GetCasesByHearingDate(hearingDate, isCentralOffice ? 0 : divisionId, endDateParsed);

            return PartialView("_DashboardRecentCases", cases);
        }

        public async Task<IActionResult> CaseList(int page = 1, string search = "", string status = "all")
        {
            int pageSize = 10;
            var divIdString = User.FindFirst("DivisionID")?.Value;
            bool isCentralOffice = string.IsNullOrEmpty(divIdString) || divIdString == "0" || divIdString == "5";
            int divisionId = int.TryParse(divIdString, out int id) ? id : 0;

            // Default CO view: Sent to CO if no filter active
            if (isCentralOffice && !string.IsNullOrEmpty(search)) 
            {
                status = "all";
            }
            else if (isCentralOffice && status == "all" && !Request.Query.ContainsKey("status"))
            {
                if (User.IsInRole("CLO"))
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


            var cases = await _gratuityRepo.GetAllCases(isCentralOffice ? 0 : divisionId, page, pageSize, search, status);
            var totalCount = await _gratuityRepo.GetTotalCaseCount(isCentralOffice ? 0 : divisionId, search, status);

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
            var model = new GratuityViewModel();
            
            // Auto-fetch Division from Logged-in User Claims
            var divIdString = User.FindFirst("DivisionID")?.Value;
            var divName = User.FindFirst("DivisionName")?.Value;
            
            bool isCentralOffice = string.IsNullOrEmpty(divIdString) || divIdString == "0" || divIdString == "5";
            int divisionCode = int.TryParse(divIdString, out int id) ? id : 0;

            if (!isCentralOffice)
            {
                model.DivisionCode = divisionCode;
            }

            ViewBag.IsCentralOffice = isCentralOffice;
            ViewBag.UserDivisionName = divName;

            PopulateDropdowns(model);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(GratuityViewModel model)
        {
            // Remove dynamic list properties from validation binding
            ModelState.Remove("DivisionList");
            ModelState.Remove("CourtList");
            ModelState.Remove("StatusList");
            ModelState.Remove("WorkingStatusList");
            ModelState.Remove("GratuityCourtList");
            ModelState.Remove("GratuityAdvocateList");

            // Clean up Payments / InterestPayments validation entries if empty or unfilled
            foreach (var key in ModelState.Keys.Where(k => k.StartsWith("Payments") || k.StartsWith("InterestPayments")).ToList())
            {
                ModelState.Remove(key);
            }

            if (model.Payments != null)
            {
                model.Payments = model.Payments.Where(p => p.Amount.HasValue && p.Amount.Value > 0).ToList();
            }
            if (model.InterestPayments != null)
            {
                model.InterestPayments = model.InterestPayments.Where(p => p.Amount.HasValue && p.Amount.Value > 0).ToList();
            }

            if (ModelState.IsValid)
            {
                // Set CreatedBy from User Claims if available
                model.CreatedBy = User.FindFirst("FullName")?.Value ?? "System";
                
                // Handle File Upload
                if (model.AdverseJudgmentFile != null)
                {
                    model.AdverseJudgmentPath = SaveFile(model.AdverseJudgmentFile, "gratuity");
                }
                if (model.AppealJudgmentFile != null)
                {
                     model.AppealJudgmentPath = SaveFile(model.AppealJudgmentFile, "gratuity");
                }

                try
                {
                    // Map ViewModel to Entity
                    var gratuityCase = (GratuityCase)model;

                    await _gratuityRepo.AddCase(gratuityCase);
                    _ = _activityLogger.LogCaseCreatedAsync("GRATUITY", gratuityCase.CaseID, gratuityCase.PGANumber, null, gratuityCase.CourtType, gratuityCase, $"Registered new Gratuity Case #{gratuityCase.PGANumber}");
                    TempData["SuccessMessage"] = "Gratuity Case created successfully!";
                    return RedirectToAction(nameof(Index));
                }
                catch (Exception ex)
                {
                    // Log the error (optional)
                    // Display the full error to the user for debugging
                    TempData["ErrorMessage"] = "Error saving case: " + ex.Message + " | " + ex.InnerException?.Message;
                    
                    var divIdString = User.FindFirst("DivisionID")?.Value;
                    var divName = User.FindFirst("DivisionName")?.Value;
                    ViewBag.IsCentralOffice = string.IsNullOrEmpty(divIdString) || divIdString == "0" || divIdString == "5";
                    ViewBag.UserDivisionName = divName;

                    PopulateDropdowns(model); // Ensure lists are repopulated
                    return View(model);
                }
            }

            var divClaim = User.FindFirst("DivisionID")?.Value;
            var dName = User.FindFirst("DivisionName")?.Value;
            ViewBag.IsCentralOffice = string.IsNullOrEmpty(divClaim) || divClaim == "0" || divClaim == "5";
            ViewBag.UserDivisionName = dName;

            PopulateDropdowns(model);
            var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
            var invalidKeys = ModelState.Keys.Where(k => ModelState[k].Errors.Count > 0).ToList();
            TempData["ErrorMessage"] = "Please check the form for errors. Fields failing: " + string.Join(", ", invalidKeys) + ". Errors: " + string.Join(" | ", errors);
            return View(model);
        }
        
        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            var gratuityCase = await _gratuityRepo.GetCaseById(id);
            if (gratuityCase == null)
            {
                return NotFound();
            }

            var divIdString = User.FindFirst("DivisionID")?.Value;
            bool isCO = string.IsNullOrEmpty(divIdString) || divIdString == "0" || divIdString == "5";
            int userDivId = int.TryParse(divIdString, out int parsedId) ? parsedId : -1;

            // IDOR Protection: Non-CO users can only view their own division's cases
            if (!isCO && gratuityCase.DivisionCode != userDivId)
            {
                _logger.LogWarning("Unauthorized access attempt to Gratuity Case ID {CaseID} by User ID {UserID} from Division {UserDivID}", id, User.FindFirstValue(ClaimTypes.NameIdentifier), userDivId);
                TempData["ErrorMessage"] = "Access Denied: You cannot view cases from other divisions.";
                return RedirectToAction("Index");
            }

            // Fetch Division Name
            var divisions = _masterRepo.GetAllDivisions();
            var div = divisions.FirstOrDefault(d => d.DivisionID == gratuityCase.DivisionCode);
            ViewBag.DivisionName = div?.DivisionNameEnglish ?? "Unknown Division";

            ViewBag.IsCentralOffice = isCO;
            ViewBag.IsCLO = User.IsInRole("CLO") || User.IsInRole("Dy CLO") || User.IsInRole("DyCLO");

            // Mark as viewed by CO if opened by a CO user and it's in "Sent to CO" status
            if (isCO && gratuityCase.ForwardingStatus == "Sent to Central Office" && !gratuityCase.IsViewedByCO)
            {
                await _gratuityRepo.MarkAsViewedByCO(id);
                gratuityCase.IsViewedByCO = true;
            }

            return View(gratuityCase);
        }

        public async Task<IActionResult> Edit(int id)
        {
            var gratuityCase = await _gratuityRepo.GetCaseById(id);
            if(gratuityCase == null)
            {
                return NotFound();
            }

            // Division ownership guard — prevent cross-division tampering
            var divClaim = User.FindFirst("DivisionID")?.Value;
            int myDiv = int.TryParse(divClaim, out int d) ? d : 0;
            bool isCO = (myDiv == 0 || myDiv == 5);
            if (!isCO && gratuityCase.DivisionCode != myDiv)
            {
                TempData["ErrorMessage"] = "Access Denied: You can only edit cases from your own division.";
                return RedirectToAction("CaseList");
            }

            // Lock guard — prevent division users from editing once sent to CO
            if (!isCO && gratuityCase.ForwardingStatus == "Sent to Central Office")
            {
                TempData["ErrorMessage"] = "This case has been sent to the Central Office and is now locked for editing.";
                return RedirectToAction("CaseList");
            }

            // Map Entity to ViewModel
            var model = new GratuityViewModel
            {
                CaseID = gratuityCase.CaseID,
                CaseStatus = gratuityCase.CaseStatus,
                DivisionCode = gratuityCase.DivisionCode,
                PGANumber = gratuityCase.PGANumber,
                CourtType = gratuityCase.CourtType,
                ClaimantName = gratuityCase.ClaimantName,
                ClaimantDesignation = gratuityCase.ClaimantDesignation,
                DateOfBirth = gratuityCase.DateOfBirth,
                WorkingStatus = gratuityCase.WorkingStatus,
                IsAdditionalBenefitsGiven = gratuityCase.IsAdditionalBenefitsGiven,
                AdditionalBenefitsAmount = gratuityCase.AdditionalBenefitsAmount,
                EntrustmentNo = gratuityCase.EntrustmentNo,
                EntrustmentDate = gratuityCase.EntrustmentDate,
                AdvocateName = gratuityCase.AdvocateName,
                DisposalResult = gratuityCase.DisposalResult,

                // Case Progress
                IsDocumentSent = gratuityCase.IsDocumentSent,
                DocumentOutwardNo = gratuityCase.DocumentOutwardNo,
                DocumentOutwardDate = gratuityCase.DocumentOutwardDate,
                IsObjectionFiled = gratuityCase.IsObjectionFiled,
                ObjectionOutwardNo = gratuityCase.ObjectionOutwardNo,
                ObjectionFiledDate = gratuityCase.ObjectionFiledDate,
                IsEvidenceFiled = gratuityCase.IsEvidenceFiled,
                CurrentStage = gratuityCase.CurrentStage,
                NextHearingDate = gratuityCase.NextHearingDate,

                AmountClaimed = gratuityCase.AmountClaimed,
                AppointmentDate = gratuityCase.AppointmentDate,
                AppointmentDate_CA = gratuityCase.AppointmentDate_CA,
                CA_AppointmentRemark = gratuityCase.CA_AppointmentRemark,
                RetirementDate = gratuityCase.RetirementDate,
                RetirementDate_CA = gratuityCase.RetirementDate_CA,
                TotalServicePeriod = gratuityCase.TotalServicePeriod,
                Period_SPE_LWA_ABS = gratuityCase.Period_SPE_LWA_ABS,
                Period_SPE_LWA_ABS_CA = gratuityCase.Period_SPE_LWA_ABS_CA,
                QualifyingService_Corp = gratuityCase.QualifyingService_Corp,
                QualifyingService_CA = gratuityCase.QualifyingService_CA,
                LastDrawnPay = gratuityCase.LastDrawnPay,
                LastDrawnPay_CA = gratuityCase.LastDrawnPay_CA,
                LastDrawnBasic = gratuityCase.LastDrawnBasic,
                LastDrawnBDA = gratuityCase.LastDrawnBDA,
                LastDrawnDA = gratuityCase.LastDrawnDA,
                LastDrawnBasic_CA = gratuityCase.LastDrawnBasic_CA,
                LastDrawnBDA_CA = gratuityCase.LastDrawnBDA_CA,
                LastDrawnDA_CA = gratuityCase.LastDrawnDA_CA,
                GratuityAmount_Corp_Reg = gratuityCase.GratuityAmount_Corp_Reg,
                GratuityAmount_Corp_Act = gratuityCase.GratuityAmount_Corp_Act,
                IsDeductionMade = gratuityCase.IsDeductionMade,
                DeductionAmount = gratuityCase.DeductionAmount,
                DeductionDetails = gratuityCase.DeductionDetails,
                ActualPaidAmount = gratuityCase.ActualPaidAmount,
                ChequeNumber = gratuityCase.ChequeNumber,
                ChequeDate = gratuityCase.ChequeDate,
                Payments = gratuityCase.Payments,
                DisposalDate = gratuityCase.DisposalDate,
                CopyAppliedDate = gratuityCase.CopyAppliedDate,
                CopyIssuedDate = gratuityCase.CopyIssuedDate,
                CopyDeliveredDate = gratuityCase.CopyDeliveredDate,
                CopyReceivedDate = gratuityCase.CopyReceivedDate,
                DelayRemarks = gratuityCase.DelayRemarks,
                GratuityAmount_CA_Reg = gratuityCase.GratuityAmount_CA_Reg,
                GratuityAmount_CA_Act = gratuityCase.GratuityAmount_CA_Act,
                OrderedAmount_CA = gratuityCase.OrderedAmount_CA,
                IsDeductionMade_CA = gratuityCase.IsDeductionMade_CA,
                DeductionAmount_CA = gratuityCase.DeductionAmount_CA,
                DeductionDetails_CA = gratuityCase.DeductionDetails_CA,
                ActualPaidAmount_CA = gratuityCase.ActualPaidAmount_CA,
                IsInterestPayable = gratuityCase.IsInterestPayable,
                InterestRate = gratuityCase.InterestRate,
                InterestRemarks = gratuityCase.InterestRemarks,
                InterestPayments = gratuityCase.InterestPayments,
                ChequeNumber_CA = gratuityCase.ChequeNumber_CA,
                ChequeDate_CA = gratuityCase.ChequeDate_CA,
                AdvocateOpinion = gratuityCase.AdvocateOpinion,
                LOOpinion = gratuityCase.LOOpinion,
                DCOpinion = gratuityCase.DCOpinion,
                AppealNumber = gratuityCase.AppealNumber,
                AppealYear = gratuityCase.AppealYear,
                AppealArisingNo = gratuityCase.AppealArisingNo,
                AppealArisingYear = gratuityCase.AppealArisingYear,
                AppealCourt = gratuityCase.AppealCourt,
                AppealEntrustmentNo = gratuityCase.AppealEntrustmentNo,
                AppealEntrustmentDate = gratuityCase.AppealEntrustmentDate,
                AppealAdvocate = gratuityCase.AppealAdvocate,
                AppealAwardDetails = gratuityCase.AppealAwardDetails,
                AppealDisposalDate = gratuityCase.AppealDisposalDate,
                AppealComplianceAmount = gratuityCase.AppealComplianceAmount,
                AppealComplianceChequeNumber = gratuityCase.AppealComplianceChequeNumber,
                AppealComplianceChequeDate = gratuityCase.AppealComplianceChequeDate,
                AppealCopyAppliedDate = gratuityCase.AppealCopyAppliedDate,
                AppealCopyReadyDate = gratuityCase.AppealCopyReadyDate,
                AppealCopyDeliveredDate = gratuityCase.AppealCopyDeliveredDate,
                AppealCopyReceivedDate = gratuityCase.AppealCopyReceivedDate,
                AppealDelayRemarks = gratuityCase.AppealDelayRemarks,
                AppealAdvocateOpinion = gratuityCase.AppealAdvocateOpinion,
                AppealLOOpinion = gratuityCase.AppealLOOpinion,
                AppealDCOpinion = gratuityCase.AppealDCOpinion,
                AppealJudgmentPath = gratuityCase.AppealJudgmentPath,
                FinalAmount = gratuityCase.FinalAmount,
                ComplianceStatus = gratuityCase.ComplianceStatus,
                Remarks = gratuityCase.Remarks,
                ForwardingStatus = gratuityCase.ForwardingStatus,
                ClosureRemarks = gratuityCase.ClosureRemarks,
                ClosureDate = gratuityCase.ClosureDate,
                OutwardNumber = gratuityCase.OutwardNumber,
                OutwardDate = gratuityCase.OutwardDate,
                AdverseJudgmentPath = gratuityCase.AdverseJudgmentPath,
                AppealRemarks = gratuityCase.AppealRemarks,
                AppealDate = gratuityCase.AppealDate,

                // --- NEW / MISSING MAPPINGS ---
                IsFeasibilityReceived = gratuityCase.IsFeasibilityReceived,
                FeasibilityReceiptDate = gratuityCase.FeasibilityReceiptDate,
                ActionTaken = gratuityCase.ActionTaken,
                ApprovalOutwardNo = gratuityCase.ApprovalOutwardNo,
                ApprovalDate = gratuityCase.ApprovalDate,
                AppealActionOutwardNo = gratuityCase.AppealActionOutwardNo,
                AppealActionDate = gratuityCase.AppealActionDate,

                // Populate _Appeal fields
                ForwardingStatus_Appeal = !string.IsNullOrEmpty(gratuityCase.ForwardingStatus_Appeal) ? gratuityCase.ForwardingStatus_Appeal : (gratuityCase.ForwardingStatus == "Appeal before Appellant Authority" ? gratuityCase.ForwardingStatus : null),
                OutwardNumber_Appeal = !string.IsNullOrEmpty(gratuityCase.OutwardNumber_Appeal) ? gratuityCase.OutwardNumber_Appeal : (gratuityCase.ForwardingStatus == "Appeal before Appellant Authority" ? gratuityCase.OutwardNumber : null),
                OutwardDate_Appeal = gratuityCase.OutwardDate_Appeal ?? (gratuityCase.ForwardingStatus == "Appeal before Appellant Authority" ? gratuityCase.OutwardDate : null),

                // Mirrors
                IsPendingForFiling = gratuityCase.IsPendingForFiling,
                HighCourtBench = gratuityCase.HighCourtBench,
                OtherHighCourtBench = gratuityCase.OtherHighCourtBench,
                StayGranted = gratuityCase.StayGranted,
                StayComplianceOutwardNo = gratuityCase.StayComplianceOutwardNo,
                StayComplianceDate = gratuityCase.StayComplianceDate,
                StayOrderPath1 = gratuityCase.StayOrderPath1,
                StayOrderPath2 = gratuityCase.StayOrderPath2,

                CorpWPNumber = gratuityCase.CorpWPNumber,
                CorpWPYear = gratuityCase.CorpWPYear,
                CorpWPAdvocate = gratuityCase.CorpWPAdvocate,
                CorpWPEntrustmentNo = gratuityCase.CorpWPEntrustmentNo,
                CorpWPEntrustmentDate = gratuityCase.CorpWPEntrustmentDate,
                CorpWPStatus = gratuityCase.CorpWPStatus,
                RestorationFiled = gratuityCase.RestorationFiled,
                RestorationDate = gratuityCase.RestorationDate,
                RestorationStatus = gratuityCase.RestorationStatus,
                CorpWPOutcome = gratuityCase.CorpWPOutcome,
                CorpWPActionTaken = gratuityCase.CorpWPActionTaken,
                ClosureOutwardNo = gratuityCase.ClosureOutwardNo,

                InitialActionRemarks = gratuityCase.InitialActionRemarks,
                InitialActionPath1 = gratuityCase.InitialActionPath1,
                InitialActionPath2 = gratuityCase.InitialActionPath2,
                FinalRemarks = gratuityCase.FinalRemarks,

                IsClaimantSCAppeal = gratuityCase.IsClaimantSCAppeal,
                IsClaimantSCPending = gratuityCase.IsClaimantSCPending,
                ClaimantSCDiaryNumber = gratuityCase.ClaimantSCDiaryNumber,
                ClaimantSCYear = gratuityCase.ClaimantSCYear,
                ClaimantSCNumber = gratuityCase.ClaimantSCNumber,
                ClaimantSLPYear = gratuityCase.ClaimantSLPYear,
                ClaimantSCFiledBy = gratuityCase.ClaimantSCFiledBy,
                ClaimantSCEntrustmentNo = gratuityCase.ClaimantSCEntrustmentNo,
                ClaimantSCEntrustmentDate = gratuityCase.ClaimantSCEntrustmentDate,
                ClaimantSCAdvocate = gratuityCase.ClaimantSCAdvocate,
                ClaimantSCStatus = gratuityCase.ClaimantSCStatus,
                ClaimantSCOutcome = gratuityCase.ClaimantSCOutcome,
                ClaimantSCActionTaken = gratuityCase.ClaimantSCActionTaken,
                ClaimantSCClosureNo = gratuityCase.ClaimantSCClosureNo,
                ClaimantSCClosureDate = gratuityCase.ClaimantSCClosureDate,

                ClaimantDivisionID = gratuityCase.ClaimantDivisionID,
                ClaimantArisingWPNumber = gratuityCase.ClaimantArisingWPNumber,
                ClaimantArisingWPYear = gratuityCase.ClaimantArisingWPYear,
                ClaimantMVCCurrentStatus = gratuityCase.ClaimantMVCCurrentStatus,
                ClaimantWPNumber = gratuityCase.ClaimantWPNumber,
                ClaimantWPYear = gratuityCase.ClaimantWPYear,
                ClaimantHighCourtBench = gratuityCase.ClaimantHighCourtBench,
                ClaimantOtherHighCourtBench = gratuityCase.ClaimantOtherHighCourtBench,
                ClaimantWPEntrustmentNo = gratuityCase.ClaimantWPEntrustmentNo,
                ClaimantWPEntrustmentDate = gratuityCase.ClaimantWPEntrustmentDate,
                ClaimantWPAdvocate = gratuityCase.ClaimantWPAdvocate,
                ClaimantWPStatus = gratuityCase.ClaimantWPStatus,
                ClaimantWPDecision = gratuityCase.ClaimantWPDecision,
                ClaimantActionTaken = gratuityCase.ClaimantActionTaken,
                ClaimantApprovalNo = gratuityCase.ClaimantApprovalNo,
                ClaimantApprovalDate = gratuityCase.ClaimantApprovalDate,
                EvidenceRemarks = gratuityCase.EvidenceRemarks,
                EvidenceWitnessName = gratuityCase.EvidenceWitnessName,
                EvidenceWitnessDesignation = gratuityCase.EvidenceWitnessDesignation
            };

            var divisions = _masterRepo.GetAllDivisions();
            var divOfCase = divisions.FirstOrDefault(div => div.DivisionID == gratuityCase.DivisionCode);
            ViewBag.UserDivisionName = divOfCase?.DivisionNameEnglish ?? "Unknown Division";
            ViewBag.IsCentralOffice = isCO;

            PopulateDropdowns(model);
            return View("Create", model); // Reusing Create view for Edit
        }
        
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(GratuityViewModel model)
        {
            // Remove dynamic list properties from validation binding
            ModelState.Remove("DivisionList");
            ModelState.Remove("CourtList");
            ModelState.Remove("StatusList");
            ModelState.Remove("WorkingStatusList");
            ModelState.Remove("GratuityCourtList");
            ModelState.Remove("GratuityAdvocateList");

            // Clean up Payments / InterestPayments validation entries if empty or unfilled
            foreach (var key in ModelState.Keys.Where(k => k.StartsWith("Payments") || k.StartsWith("InterestPayments")).ToList())
            {
                ModelState.Remove(key);
            }

            if (model.Payments != null)
            {
                model.Payments = model.Payments.Where(p => p.Amount.HasValue && p.Amount.Value > 0).ToList();
            }
            if (model.InterestPayments != null)
            {
                model.InterestPayments = model.InterestPayments.Where(p => p.Amount.HasValue && p.Amount.Value > 0).ToList();
            }

            if (ModelState.IsValid)
            {
                // Handle File Upload
                if (model.AdverseJudgmentFile != null)
                {
                    model.AdverseJudgmentPath = SaveFile(model.AdverseJudgmentFile, "gratuity");
                }
                if (model.AppealJudgmentFile != null)
                {
                     model.AppealJudgmentPath = SaveFile(model.AppealJudgmentFile, "gratuity");
                }
                
                // Handle Mirrored File Uploads
                if (model.InitialActionFile1 != null) model.InitialActionPath1 = SaveFile(model.InitialActionFile1, "appeal_docs");
                if (model.InitialActionFile2 != null) model.InitialActionPath2 = SaveFile(model.InitialActionFile2, "appeal_docs");
                if (model.StayOrderFile1 != null) model.StayOrderPath1 = SaveFile(model.StayOrderFile1, "appeal_docs");
                if (model.StayOrderFile2 != null) model.StayOrderPath2 = SaveFile(model.StayOrderFile2, "appeal_docs");

                // Fetch existing to prevent data loss
                var existing = await _gratuityRepo.GetCaseById(model.CaseID);
                if (existing == null) return NotFound();

                // Lock guard & IDOR check — prevent division users from editing other divisions or once sent to CO
                var userDivClaim = User.FindFirst("DivisionID")?.Value;
                int myDiv = int.TryParse(userDivClaim, out int d) ? d : 0;
                bool isCO = (myDiv == 0 || myDiv == 5 || User.IsInRole("CentralOffice") || User.IsInRole("CO") || User.IsInRole("CLO") || User.IsInRole("MD") || User.IsInRole("Admin"));

                if (!isCO && existing.DivisionCode != myDiv)
                {
                    _logger.LogWarning("IDOR attempt: User from division {UserDiv} tried to edit Gratuity case {CaseID} (Division {CaseDiv})",
                        myDiv, model.CaseID, existing.DivisionCode);
                    return Forbid();
                }

                if (!isCO && existing.ForwardingStatus == "Sent to Central Office")
                {
                    TempData["ErrorMessage"] = "Access Denied: This case is locked as it has already been sent to the Central Office.";
                    return RedirectToAction("CaseList");
                }

                // Merge Basic Info
                existing.CaseStatus = model.CaseStatus;
                existing.DivisionCode = model.DivisionCode;
                existing.PGANumber = model.PGANumber;
                existing.CourtType = model.CourtType;
                existing.ClaimantName = model.ClaimantName;
                existing.ClaimantDesignation = model.ClaimantDesignation;
                existing.WorkingStatus = model.WorkingStatus;
                existing.EntrustmentNo = model.EntrustmentNo;
                existing.EntrustmentDate = model.EntrustmentDate;
                existing.AdvocateName = model.AdvocateName;
                existing.DisposalResult = model.DisposalResult;
                existing.DateOfBirth = model.DateOfBirth;
                existing.IsAdditionalBenefitsGiven = model.IsAdditionalBenefitsGiven;
                existing.AdditionalBenefitsAmount = model.AdditionalBenefitsAmount;

                // Case Progress
                existing.IsDocumentSent = model.IsDocumentSent;
                existing.DocumentOutwardNo = model.DocumentOutwardNo;
                existing.DocumentOutwardDate = model.DocumentOutwardDate;
                existing.IsObjectionFiled = model.IsObjectionFiled;
                existing.ObjectionOutwardNo = model.ObjectionOutwardNo;
                existing.ObjectionFiledDate = model.ObjectionFiledDate;
                existing.IsEvidenceFiled = model.IsEvidenceFiled;
                existing.CurrentStage = model.CurrentStage;
                existing.NextHearingDate = model.NextHearingDate;
                
                // Assessment details
                existing.AmountClaimed = model.AmountClaimed;
                existing.AppointmentDate = model.AppointmentDate;
                existing.RetirementDate = model.RetirementDate;
                existing.TotalServicePeriod = model.TotalServicePeriod;
                existing.Period_SPE_LWA_ABS = model.Period_SPE_LWA_ABS;
                existing.QualifyingService_Corp = model.QualifyingService_Corp;
                existing.QualifyingService_CA = model.QualifyingService_CA;
                existing.LastDrawnPay = model.LastDrawnPay;
                existing.LastDrawnPay_CA = model.LastDrawnPay_CA;
                existing.LastDrawnBasic = model.LastDrawnBasic;
                existing.LastDrawnBDA = model.LastDrawnBDA;
                existing.LastDrawnDA = model.LastDrawnDA;
                existing.LastDrawnBasic_CA = model.LastDrawnBasic_CA;
                existing.LastDrawnBDA_CA = model.LastDrawnBDA_CA;
                existing.LastDrawnDA_CA = model.LastDrawnDA_CA;
                existing.GratuityAmount_Corp_Reg = model.GratuityAmount_Corp_Reg;
                existing.GratuityAmount_Corp_Act = model.GratuityAmount_Corp_Act;
                existing.IsDeductionMade = model.IsDeductionMade;
                existing.DeductionAmount = model.DeductionAmount;
                existing.DeductionDetails = model.DeductionDetails;
                existing.ActualPaidAmount = model.ActualPaidAmount;
                existing.ChequeNumber = model.ChequeNumber;
                existing.ChequeDate = model.ChequeDate;
                existing.Payments = model.Payments;
                existing.DisposalDate = model.DisposalDate;
                existing.AppointmentDate_CA = model.AppointmentDate_CA;
                existing.CA_AppointmentRemark = model.CA_AppointmentRemark;
                existing.RetirementDate_CA = model.RetirementDate_CA;
                existing.Period_SPE_LWA_ABS_CA = model.Period_SPE_LWA_ABS_CA;
                existing.DelayRemarks = model.DelayRemarks;
                existing.CopyAppliedDate = model.CopyAppliedDate;
                existing.CopyIssuedDate = model.CopyIssuedDate;
                existing.CopyDeliveredDate = model.CopyDeliveredDate;
                existing.CopyReceivedDate = model.CopyReceivedDate;
                existing.GratuityAmount_CA_Reg = model.GratuityAmount_CA_Reg;
                existing.GratuityAmount_CA_Act = model.GratuityAmount_CA_Act;
                existing.OrderedAmount_CA = model.OrderedAmount_CA;
                existing.IsDeductionMade_CA = model.IsDeductionMade_CA;
                existing.DeductionAmount_CA = model.DeductionAmount_CA;
                existing.DeductionDetails_CA = model.DeductionDetails_CA;
                existing.ActualPaidAmount_CA = model.ActualPaidAmount_CA;
                existing.IsInterestPayable = model.IsInterestPayable;
                existing.InterestRate = model.InterestRate;
                existing.InterestRemarks = model.InterestRemarks;
                existing.InterestPayments = model.InterestPayments;
                existing.ChequeNumber_CA = model.ChequeNumber_CA;
                existing.ChequeDate_CA = model.ChequeDate_CA;

                // Opinions
                existing.AdvocateOpinion = model.AdvocateOpinion;
                existing.LOOpinion = model.LOOpinion;
                existing.DCOpinion = model.DCOpinion;
                existing.EvidenceRemarks = model.EvidenceRemarks;
                existing.EvidenceWitnessName = model.EvidenceWitnessName;
                existing.EvidenceWitnessDesignation = model.EvidenceWitnessDesignation;

                // Appeal / Misc (Tab 3 in Create.cshtml)
                existing.AppealNumber = model.AppealNumber;
                existing.AppealYear = model.AppealYear;
                existing.AppealArisingNo = model.AppealArisingNo;
                existing.AppealArisingYear = model.AppealArisingYear;
                existing.AppealCourt = model.AppealCourt;
                existing.AppealEntrustmentNo = model.AppealEntrustmentNo;
                existing.AppealEntrustmentDate = model.AppealEntrustmentDate;
                existing.AppealAdvocate = model.AppealAdvocate;
                existing.AppealAwardDetails = model.AppealAwardDetails;
                existing.AppealDisposalDate = model.AppealDisposalDate;
                existing.AppealComplianceAmount = model.AppealComplianceAmount;
                existing.AppealComplianceChequeNumber = model.AppealComplianceChequeNumber;
                existing.AppealComplianceChequeDate = model.AppealComplianceChequeDate;
                existing.AppealCopyAppliedDate = model.AppealCopyAppliedDate;
                existing.AppealCopyReadyDate = model.AppealCopyReadyDate;
                existing.AppealCopyDeliveredDate = model.AppealCopyDeliveredDate;
                existing.AppealCopyReceivedDate = model.AppealCopyReceivedDate;
                existing.AppealDelayRemarks = model.AppealDelayRemarks;
                existing.AppealAdvocateOpinion = model.AppealAdvocateOpinion;
                existing.AppealLOOpinion = model.AppealLOOpinion;
                existing.AppealDCOpinion = model.AppealDCOpinion;
                existing.FinalAmount = model.FinalAmount;
                existing.ComplianceStatus = model.ComplianceStatus;
                existing.Remarks = model.Remarks;
                
                // Outcome Forwarding & Closure (Tab 2)
                existing.ForwardingStatus = model.ForwardingStatus;
                existing.ClosureRemarks = model.ClosureRemarks;
                existing.ClosureDate = model.ClosureDate;
                existing.OutwardNumber = model.OutwardNumber;
                existing.OutwardDate = model.OutwardDate;

                // Appeal Forwarding & Outward Details (Tab 3)
                existing.ForwardingStatus_Appeal = model.ForwardingStatus_Appeal;
                existing.OutwardNumber_Appeal = model.OutwardNumber_Appeal;
                existing.OutwardDate_Appeal = model.OutwardDate_Appeal;

                // If sent to Central Office from division in either Tab 2 or Tab 3, reset IsViewedByCO
                if (!isCO && (model.ForwardingStatus == "Sent to Central Office" || model.ForwardingStatus_Appeal == "Sent to Central Office"))
                {
                    existing.IsViewedByCO = false;
                }
                if (!string.IsNullOrEmpty(model.AdverseJudgmentPath))
                {
                    existing.AdverseJudgmentPath = model.AdverseJudgmentPath;
                }
                if (!string.IsNullOrEmpty(model.AppealJudgmentPath))
                {
                    existing.AppealJudgmentPath = model.AppealJudgmentPath;
                }
                existing.AppealRemarks = model.AppealRemarks;
                existing.AppealDate = model.AppealDate;

                // Mirrored File Paths
                if (!string.IsNullOrEmpty(model.InitialActionPath1)) existing.InitialActionPath1 = model.InitialActionPath1;
                if (!string.IsNullOrEmpty(model.InitialActionPath2)) existing.InitialActionPath2 = model.InitialActionPath2;
                if (!string.IsNullOrEmpty(model.StayOrderPath1)) existing.StayOrderPath1 = model.StayOrderPath1;
                if (!string.IsNullOrEmpty(model.StayOrderPath2)) existing.StayOrderPath2 = model.StayOrderPath2;

                // Appeal Action Details (Specific to Tab 2 Action section in Create.cshtml)
                existing.AppealActionOutwardNo = model.AppealActionOutwardNo;
                existing.AppealActionDate = model.AppealActionDate;

                // --- MISSING MIRRORED FIELDS ---
                existing.IsFeasibilityReceived = model.IsFeasibilityReceived;
                existing.FeasibilityReceiptDate = model.FeasibilityReceiptDate;
                existing.ActionTaken = model.ActionTaken;
                existing.ApprovalOutwardNo = model.ApprovalOutwardNo;
                existing.ApprovalDate = model.ApprovalDate;

                existing.IsPendingForFiling = model.IsPendingForFiling;
                existing.HighCourtBench = model.HighCourtBench;
                existing.OtherHighCourtBench = model.OtherHighCourtBench;
                existing.StayGranted = model.StayGranted;
                existing.StayComplianceOutwardNo = model.StayComplianceOutwardNo;
                existing.StayComplianceDate = model.StayComplianceDate;

                existing.CorpWPNumber = model.CorpWPNumber;
                existing.CorpWPYear = model.CorpWPYear;
                existing.CorpWPAdvocate = model.CorpWPAdvocate;
                existing.CorpWPEntrustmentNo = model.CorpWPEntrustmentNo;
                existing.CorpWPEntrustmentDate = model.CorpWPEntrustmentDate;
                existing.CorpWPStatus = model.CorpWPStatus;
                existing.RestorationFiled = model.RestorationFiled;
                existing.RestorationDate = model.RestorationDate;
                existing.RestorationStatus = model.RestorationStatus;
                existing.CorpWPOutcome = model.CorpWPOutcome;
                existing.CorpWPActionTaken = model.CorpWPActionTaken;
                existing.ClosureOutwardNo = model.ClosureOutwardNo;

                existing.InitialActionRemarks = model.InitialActionRemarks;
                existing.FinalRemarks = model.FinalRemarks;

                existing.IsClaimantSCAppeal = model.IsClaimantSCAppeal;
                existing.IsClaimantSCPending = model.IsClaimantSCPending;
                existing.ClaimantSCDiaryNumber = model.ClaimantSCDiaryNumber;
                existing.ClaimantSCYear = model.ClaimantSCYear;
                existing.ClaimantSCNumber = model.ClaimantSCNumber;
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

                existing.ClaimantDivisionID = model.ClaimantDivisionID;
                existing.ClaimantArisingWPNumber = model.ClaimantArisingWPNumber;
                existing.ClaimantArisingWPYear = model.ClaimantArisingWPYear;
                existing.ClaimantMVCCurrentStatus = model.ClaimantMVCCurrentStatus;
                existing.ClaimantWPNumber = model.ClaimantWPNumber;
                existing.ClaimantWPYear = model.ClaimantWPYear;
                existing.ClaimantHighCourtBench = model.ClaimantHighCourtBench;
                existing.ClaimantOtherHighCourtBench = model.ClaimantOtherHighCourtBench;
                existing.ClaimantWPEntrustmentNo = model.ClaimantWPEntrustmentNo;
                existing.ClaimantWPEntrustmentDate = model.ClaimantWPEntrustmentDate;
                existing.ClaimantWPAdvocate = model.ClaimantWPAdvocate;
                existing.ClaimantWPStatus = model.ClaimantWPStatus;
                existing.ClaimantWPDecision = model.ClaimantWPDecision;
                existing.ClaimantActionTaken = model.ClaimantActionTaken;
                existing.ClaimantApprovalNo = model.ClaimantApprovalNo;
                existing.ClaimantApprovalDate = model.ClaimantApprovalDate;
                existing.IsViewedByCO = model.IsViewedByCO;
 
                await _gratuityRepo.UpdateCase(existing);
                _ = _activityLogger.LogCaseUpdatedAsync("GRATUITY", existing.CaseID, existing.PGANumber, null, existing.CourtType, null, existing, $"Updated Gratuity case #{existing.PGANumber}");
                TempData["SuccessMessage"] = "Gratuity Case updated successfully!";
                return RedirectToAction(nameof(Index));
            }
            
            var finalDivClaim = User.FindFirst("DivisionID")?.Value;
            bool isCOUser = string.IsNullOrEmpty(finalDivClaim) || finalDivClaim == "0" || finalDivClaim == "5";
            var allDivs = _masterRepo.GetAllDivisions();
            var caseDiv = allDivs.FirstOrDefault(d => d.DivisionID == model.DivisionCode);
            ViewBag.UserDivisionName = caseDiv?.DivisionNameEnglish ?? "Unknown Division";
            ViewBag.IsCentralOffice = isCOUser;

            PopulateDropdowns(model);
            return View("Create", model);
        }

        [HttpGet]
        [Authorize]
        public async Task<IActionResult> CLOAction(int id)
        {
            var divIdString = User.FindFirst("DivisionID")?.Value;
            bool isCO = string.IsNullOrEmpty(divIdString) || divIdString == "0" || divIdString == "5";
            bool isMD = User.IsInRole("MD");

            if (!isCO && !isMD)
            {
                TempData["ErrorMessage"] = "Access Denied.";
                return RedirectToAction("Details", new { id });
            }

            var model = await _gratuityRepo.GetCaseById(id);
            if (model == null)
            {
                _logger.LogWarning("Gratuity Case not found for CLOAction: ID {CaseID}", id);
                return NotFound();
            }

            string roleName = User.FindFirstValue(System.Security.Claims.ClaimTypes.Role) ?? "";
            if (roleName != "CLO" && roleName != "Dy CLO" && roleName != "DyCLO" && roleName != "Admin" && roleName != "MD")
            {
                _logger.LogWarning("Unauthorized CLOAction attempt by user role {RoleName}", roleName);
                TempData["ErrorMessage"] = "Access Denied: You do not have permission for CLO Action.";
                return RedirectToAction("Details", new { id });
            }

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> CLOAction(GratuityCase model)
        {
            string roleName = User.FindFirstValue(System.Security.Claims.ClaimTypes.Role) ?? "";
            if (roleName != "CLO" && roleName != "Dy CLO" && roleName != "DyCLO" && roleName != "Admin" && roleName != "MD")
            {
                _logger.LogWarning("Unauthorized CLOAction POST attempt by user role {RoleName}", roleName);
                TempData["ErrorMessage"] = "Access Denied: You do not have permission for CLO Action.";
                return RedirectToAction("Details", new { id = model.CaseID });
            }

            var divIdString = User.FindFirst("DivisionID")?.Value;
            bool isCO = string.IsNullOrEmpty(divIdString) || divIdString == "0" || divIdString == "5";
            bool isMD = User.IsInRole("MD");

            if (!isCO && !isMD)
            {
                TempData["ErrorMessage"] = "Access Denied.";
                return RedirectToAction("Details", new { id = model.CaseID });
            }

            var dbCase = await _gratuityRepo.GetCaseById(model.CaseID);
            if (dbCase == null) return NotFound();

            dbCase.DisposalResult = model.DisposalResult; // Using DisposalResult for Approved/Rejected
            dbCase.ApprovalDate = model.ApprovalDate;

            if (model.DisposalResult == "Approved")
            {
                dbCase.ActionTaken = "Pending before Competent Authority";
            }
            else if (model.DisposalResult == "Rejected")
            {
                dbCase.ActionTaken = "Closed";
            }

            dbCase.ModifiedDate = DateTime.Now;

            await _gratuityRepo.UpdateCase(dbCase);
            _ = _activityLogger.LogSubEntityActionAsync("GRATUITY", dbCase.CaseID, dbCase.PGANumber, "CENTRAL_OFFICER_REVIEW", $"CLO Action recorded: {model.DisposalResult} (Date: {model.ApprovalDate:yyyy-MM-dd})", new { DisposalResult = model.DisposalResult, ApprovalDate = model.ApprovalDate });

            TempData["SuccessMessage"] = "Action recorded successfully.";
            return RedirectToAction("Details", new { id = model.CaseID });
        }

        [HttpGet]
        [Authorize]
        public async Task<IActionResult> Action(int id)
        {
            var divIdString = User.FindFirst("DivisionID")?.Value;
            bool isCO = string.IsNullOrEmpty(divIdString) || divIdString == "0" || divIdString == "5";
            bool isMD = User.IsInRole("MD");

            if (!isCO && !isMD)
            {
                TempData["ErrorMessage"] = "Access Denied: Only Central Office can perform this action.";
                return RedirectToAction("Details", new { id });
            }

            _logger.LogInformation("GET Action for Gratuity Case ID: {CaseID}", id);
            var gratuityCase = await _gratuityRepo.GetCaseById(id);
            if (gratuityCase == null)
            {
                _logger.LogWarning("Gratuity Case not found: ID {CaseID}", id);
                return NotFound();
            }

            // Reuse the mapping logic from Edit, or create a specific ViewModel mapping
            // For now, mapping manually to GratuityViewModel as Action view will likely need it
            var model = new GratuityViewModel
            {
                CaseID = gratuityCase.CaseID,
                CaseStatus = gratuityCase.CaseStatus,
                DivisionCode = gratuityCase.DivisionCode,
                PGANumber = gratuityCase.PGANumber,
                CourtType = gratuityCase.CourtType,
                ClaimantName = gratuityCase.ClaimantName,
                // Map only necessary fields for Action view context
                AdvocateName = gratuityCase.AdvocateName,
                AdvocateOpinion = gratuityCase.AdvocateOpinion,
                LOOpinion = gratuityCase.LOOpinion,
                DCOpinion = gratuityCase.DCOpinion,
                
                // Feasibility Action Details
                IsFeasibilityReceived = gratuityCase.IsFeasibilityReceived,
                FeasibilityReceiptDate = gratuityCase.FeasibilityReceiptDate,
                ActionTaken = gratuityCase.ActionTaken,
                ApprovalOutwardNo = gratuityCase.ApprovalOutwardNo,
                ApprovalDate = gratuityCase.ApprovalDate,
                AppealActionOutwardNo = gratuityCase.AppealActionOutwardNo,
                AppealActionDate = gratuityCase.AppealActionDate,
                InitialActionRemarks = gratuityCase.InitialActionRemarks,
                InitialActionPath1 = gratuityCase.InitialActionPath1,
                InitialActionPath2 = gratuityCase.InitialActionPath2,
                FinalRemarks = gratuityCase.FinalRemarks,
                
                // Appeal / High Court Details
                AppealNumber = gratuityCase.AppealNumber,
                AppealYear = gratuityCase.AppealYear,
                AppealArisingNo = gratuityCase.AppealArisingNo,
                AppealArisingYear = gratuityCase.AppealArisingYear,
                AppealCourt = gratuityCase.AppealCourt,
                AppealEntrustmentNo = gratuityCase.AppealEntrustmentNo,
                AppealEntrustmentDate = gratuityCase.AppealEntrustmentDate,
                AppealAdvocate = gratuityCase.AppealAdvocate,
                AppealAwardDetails = gratuityCase.AppealAwardDetails,
                AppealDisposalDate = gratuityCase.AppealDisposalDate,
                AppealJudgmentPath = gratuityCase.AppealJudgmentPath,
                AppealComplianceAmount = gratuityCase.AppealComplianceAmount,
                AppealComplianceChequeNumber = gratuityCase.AppealComplianceChequeNumber,
                AppealComplianceChequeDate = gratuityCase.AppealComplianceChequeDate,
                FinalAmount = gratuityCase.FinalAmount,
                ComplianceStatus = gratuityCase.ComplianceStatus,
                
                // Add more mappings as needed for Claimant Appeal placeholders or display
                ForwardingStatus = gratuityCase.ForwardingStatus,
                OutwardNumber = gratuityCase.OutwardNumber,
                OutwardDate = gratuityCase.OutwardDate,
                
                // Mappings for Action View display
                AppealDate = gratuityCase.AppealDate,
                AppealRemarks = gratuityCase.AppealRemarks,
                Remarks = gratuityCase.Remarks,

                // --- MIRRORED FIELDS MAPPING ---
                IsPendingForFiling = gratuityCase.IsPendingForFiling,
                HighCourtBench = gratuityCase.HighCourtBench,
                OtherHighCourtBench = gratuityCase.OtherHighCourtBench,
                StayGranted = gratuityCase.StayGranted,
                StayComplianceOutwardNo = gratuityCase.StayComplianceOutwardNo,
                StayComplianceDate = gratuityCase.StayComplianceDate,
                // Files would ideally be mapped if paths exist
                StayOrderPath1 = gratuityCase.StayOrderPath1,
                StayOrderPath2 = gratuityCase.StayOrderPath2,

                CorpWPStatus = gratuityCase.CorpWPStatus,
                RestorationFiled = gratuityCase.RestorationFiled,
                RestorationDate = gratuityCase.RestorationDate,
                RestorationStatus = gratuityCase.RestorationStatus,
                CorpWPOutcome = gratuityCase.CorpWPOutcome,
                CorpWPActionTaken = gratuityCase.CorpWPActionTaken,
                ClosureOutwardNo = gratuityCase.ClosureOutwardNo,

                IsClaimantSCAppeal = gratuityCase.IsClaimantSCAppeal,
                IsClaimantSCPending = gratuityCase.IsClaimantSCPending,
                ClaimantSCDiaryNumber = gratuityCase.ClaimantSCDiaryNumber,
                ClaimantSCYear = gratuityCase.ClaimantSCYear,
                ClaimantSCNumber = gratuityCase.ClaimantSCNumber,
                ClaimantSLPYear = gratuityCase.ClaimantSLPYear,
                ClaimantSCFiledBy = gratuityCase.ClaimantSCFiledBy,
                ClaimantSCEntrustmentNo = gratuityCase.ClaimantSCEntrustmentNo,
                ClaimantSCEntrustmentDate = gratuityCase.ClaimantSCEntrustmentDate,
                ClaimantSCAdvocate = gratuityCase.ClaimantSCAdvocate,
                ClaimantSCStatus = gratuityCase.ClaimantSCStatus,
                ClaimantSCOutcome = gratuityCase.ClaimantSCOutcome,
                ClaimantSCActionTaken = gratuityCase.ClaimantSCActionTaken,
                ClaimantSCClosureNo = gratuityCase.ClaimantSCClosureNo,
                ClaimantSCClosureDate = gratuityCase.ClaimantSCClosureDate,

                ClaimantDivisionID = gratuityCase.ClaimantDivisionID,
                ClaimantArisingWPNumber = gratuityCase.ClaimantArisingWPNumber,
                ClaimantArisingWPYear = gratuityCase.ClaimantArisingWPYear,
                ClaimantMVCCurrentStatus = gratuityCase.ClaimantMVCCurrentStatus,
                ClaimantWPNumber = gratuityCase.ClaimantWPNumber,
                ClaimantWPYear = gratuityCase.ClaimantWPYear,
                ClaimantHighCourtBench = gratuityCase.ClaimantHighCourtBench,
                ClaimantOtherHighCourtBench = gratuityCase.ClaimantOtherHighCourtBench,
                ClaimantWPEntrustmentNo = gratuityCase.ClaimantWPEntrustmentNo,
                ClaimantWPEntrustmentDate = gratuityCase.ClaimantWPEntrustmentDate,
                ClaimantWPAdvocate = gratuityCase.ClaimantWPAdvocate,
                ClaimantWPStatus = gratuityCase.ClaimantWPStatus,
                ClaimantWPDecision = gratuityCase.ClaimantWPDecision,
                ClaimantActionTaken = gratuityCase.ClaimantActionTaken,
                ClaimantApprovalNo = gratuityCase.ClaimantApprovalNo,
                ClaimantApprovalDate = gratuityCase.ClaimantApprovalDate,
                
                CorpWPNumber = gratuityCase.CorpWPNumber,
                CorpWPYear = gratuityCase.CorpWPYear,
                CorpWPEntrustmentNo = gratuityCase.CorpWPEntrustmentNo,
                CorpWPEntrustmentDate = gratuityCase.CorpWPEntrustmentDate,
                CorpWPAdvocate = gratuityCase.CorpWPAdvocate
            };
            
            // We might need to populate dropdowns if editing is allowed in Action view
            PopulateDropdowns(model);

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> Action(GratuityViewModel model)
        {
            var divIdString = User.FindFirst("DivisionID")?.Value;
            bool isCO = string.IsNullOrEmpty(divIdString) || divIdString == "0" || divIdString == "5";
            bool isMD = User.IsInRole("MD");

            if (!isCO && !isMD)
            {
                TempData["ErrorMessage"] = "Access Denied: Only Central Office can perform this action.";
                return RedirectToAction("Details", new { id = model.CaseID });
            }

            _logger.LogInformation("POST Action for Gratuity Case ID: {CaseID}", model.CaseID);
            
            var existing = await _gratuityRepo.GetCaseById(model.CaseID);
            if (existing == null) return NotFound();

            // Handle File Uploads for Action Page
            if (model.InitialActionFile1 != null) existing.InitialActionPath1 = SaveFile(model.InitialActionFile1, "appeal_docs");
            if (model.InitialActionFile2 != null) existing.InitialActionPath2 = SaveFile(model.InitialActionFile2, "appeal_docs");
            if (model.StayOrderFile1 != null) existing.StayOrderPath1 = SaveFile(model.StayOrderFile1, "appeal_docs");
            if (model.StayOrderFile2 != null) existing.StayOrderPath2 = SaveFile(model.StayOrderFile2, "appeal_docs");

            // Update Action-specific fields
            existing.IsFeasibilityReceived = model.IsFeasibilityReceived;
            existing.FeasibilityReceiptDate = model.FeasibilityReceiptDate;
            existing.ActionTaken = model.ActionTaken;
            existing.ApprovalOutwardNo = model.ApprovalOutwardNo;
            existing.ApprovalDate = model.ApprovalDate;
            existing.AppealActionOutwardNo = model.AppealActionOutwardNo;
            existing.AppealActionDate = model.AppealActionDate;
            existing.InitialActionRemarks = model.InitialActionRemarks;
            existing.FinalRemarks = model.FinalRemarks;

            // Update High Court Appeal (Mirrored)
            existing.IsPendingForFiling = model.IsPendingForFiling;
            existing.HighCourtBench = model.HighCourtBench;
            existing.OtherHighCourtBench = model.OtherHighCourtBench;
            existing.StayGranted = model.StayGranted;
            existing.StayComplianceOutwardNo = model.StayComplianceOutwardNo;
            existing.StayComplianceDate = model.StayComplianceDate;
            existing.CorpWPNumber = model.CorpWPNumber;
            existing.CorpWPYear = model.CorpWPYear;
            existing.CorpWPAdvocate = model.CorpWPAdvocate;
            existing.CorpWPEntrustmentNo = model.CorpWPEntrustmentNo;
            existing.CorpWPEntrustmentDate = model.CorpWPEntrustmentDate;
            existing.CorpWPStatus = model.CorpWPStatus;
            existing.RestorationFiled = model.RestorationFiled;
            existing.RestorationDate = model.RestorationDate;
            existing.RestorationStatus = model.RestorationStatus;
            existing.CorpWPOutcome = model.CorpWPOutcome;
            existing.CorpWPActionTaken = model.CorpWPActionTaken;
            existing.ClosureOutwardNo = model.ClosureOutwardNo;

            // Appellate Authority and Claimant Appeal fields removed from Action page

            // Update Supreme Court Appeal (Mirrored) - MISSING MAPPINGS FIXED
            existing.IsClaimantSCAppeal = model.IsClaimantSCAppeal;
            existing.IsClaimantSCPending = model.IsClaimantSCPending;
            existing.ClaimantSCDiaryNumber = model.ClaimantSCDiaryNumber;
            existing.ClaimantSCYear = model.ClaimantSCYear;
            existing.ClaimantSCNumber = model.ClaimantSCNumber;
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

            existing.ModifiedDate = DateTime.Now;

            await _gratuityRepo.UpdateCase(existing);
            _ = _activityLogger.LogCaseUpdatedAsync("GRATUITY", existing.CaseID, existing.PGANumber, null, existing.CourtType, null, existing, $"Central Office Action recorded on Gratuity case #{existing.PGANumber}");
            TempData["SuccessMessage"] = "Action updated successfully!";
            return RedirectToAction(nameof(Index));
        }

        private void PopulateDropdowns(GratuityViewModel model)
        {
            // Populate DivisionList from MasterRepository
            var divisions = _masterRepo.GetAllDivisions();
            model.DivisionList = divisions.Select(d => new SelectListItem 
            { 
                Text = d.DivisionNameEnglish, 
                Value = d.DivisionID.ToString() 
            }).ToList();

            // Populate other static lists
             model.StatusList = new List<SelectListItem>
            {
                new SelectListItem { Text = "Pending", Value = "Pending" },
                new SelectListItem { Text = "Disposed", Value = "Disposed" },
                new SelectListItem { Text = "DNP", Value = "DNP" },
                new SelectListItem { Text = "Ex-parte", Value = "Ex-parte" }
            };

            model.CourtList = new List<SelectListItem>
            {
                new SelectListItem { Text = "Controlling Authority", Value = "Controlling Authority" },
                new SelectListItem { Text = "Appellate Authority", Value = "Appellate Authority" }
            };

            model.WorkingStatusList = new List<SelectListItem>
            {
                new SelectListItem { Text = "Retired", Value = "Retired" },
                new SelectListItem { Text = "VRS", Value = "VRS" },
                new SelectListItem { Text = "Death", Value = "Death" },
                new SelectListItem { Text = "Dismissed", Value = "Dismissed" },
                new SelectListItem { Text = "Others", Value = "Others" }
            };

            // Populate Gratuity Court Master List
            model.GratuityCourtList = _masterRepo.GetAllGratuityCourts().Select(c => new SelectListItem 
            { 
                Text = c.CourtName, 
                Value = c.CourtName // Storing name as string to match existing DB column
            }).ToList();

            // Populate Gratuity Advocate Master List
            model.GratuityAdvocateList = _masterRepo.GetAllGratuityAdvocates().Select(a => new SelectListItem
            {
                Text = a.AdvocateName,
                Value = a.AdvocateName // Storing name as string to match existing DB column
            }).ToList();

            model.DesignationList = new List<SelectListItem>
            {
                new SelectListItem { Text = "Driver", Value = "Driver" },
                new SelectListItem { Text = "Conductor", Value = "Conductor" },
                new SelectListItem { Text = "Mechanic", Value = "Mechanic" },
                new SelectListItem { Text = "Assistant", Value = "Assistant" },
                new SelectListItem { Text = "Inspector", Value = "Inspector" },
                new SelectListItem { Text = "Supervisor", Value = "Supervisor" },
                new SelectListItem { Text = "Clerk", Value = "Clerk" },
                new SelectListItem { Text = "Officer", Value = "Officer" },
                new SelectListItem { Text = "Others", Value = "Others" }
            };
        }

        private static readonly HashSet<string> _allowedExtensions =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { ".pdf", ".doc", ".docx", ".jpg", ".jpeg", ".png", ".tiff", ".bmp" };

        private string? SaveFile(IFormFile? file, string folder)
        {
            if (file == null || file.Length == 0) return null;

            // Security: validate extension against whitelist
            string ext = Path.GetExtension(file.FileName);
            if (!_allowedExtensions.Contains(ext))
                throw new InvalidOperationException(
                    $"File type '{ext}' is not allowed. Only PDF, Word, and image files are permitted.");

            // Security: 20 MB limit
            const long maxBytes = 20 * 1024 * 1024;
            if (file.Length > maxBytes)
                throw new InvalidOperationException("File size exceeds the 20 MB limit.");

            var uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads", folder);
            if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

            // GUID + extension only — no original filename (prevents path traversal)
            var uniqueFileName = $"{Guid.NewGuid()}{ext}";
            var filePath = Path.Combine(uploadsFolder, uniqueFileName);

            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                file.CopyTo(fileStream);
            }

            return $"/uploads/{folder}/{uniqueFileName}";
        }

        [HttpGet]
        public async Task<IActionResult> ClaimantAppeals(int page = 1, string search = "")
        {
            try
            {
                int divisionId = 0;
                var divisionClaim = User.FindFirst("DivisionID");
                if (divisionClaim != null && int.TryParse(divisionClaim.Value, out int divId))
                {
                    divisionId = divId;
                }

                // Reuse GetAllCases but we might want to filter or just show all
                var cases = await _gratuityRepo.GetAllCases(divisionId, page, 10, search);
                var totalCount = await _gratuityRepo.GetTotalCaseCount(divisionId, search);

                ViewBag.CurrentPage = page;
                ViewBag.TotalPages = (int)Math.Ceiling((double)totalCount / 10);
                ViewBag.Search = search;

                return View(cases);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return View(new List<GratuityCase>());
            }
        }

        [HttpGet]
        public async Task<IActionResult> ManageClaimantAppeal(int id)
        {
            var gratuityCase = await _gratuityRepo.GetCaseById(id);
            if (gratuityCase == null)
            {
                return NotFound();
            }

            var viewModel = new GratuityViewModel
            {
                CaseID = gratuityCase.CaseID,
                DivisionCode = gratuityCase.DivisionCode,
                PGANumber = gratuityCase.PGANumber,
                ClaimantName = gratuityCase.ClaimantName,
                
                // Map Claimant Appeal Fields
                ClaimantDivisionID = gratuityCase.ClaimantDivisionID,
                ClaimantArisingWPNumber = gratuityCase.ClaimantArisingWPNumber,
                ClaimantArisingWPYear = gratuityCase.ClaimantArisingWPYear,
                ClaimantMVCCurrentStatus = gratuityCase.ClaimantMVCCurrentStatus,
                ClaimantWPNumber = gratuityCase.ClaimantWPNumber,
                ClaimantWPYear = gratuityCase.ClaimantWPYear,
                ClaimantWPEntrustmentNo = gratuityCase.ClaimantWPEntrustmentNo,
                ClaimantWPEntrustmentDate = gratuityCase.ClaimantWPEntrustmentDate,
                ClaimantWPAdvocate = gratuityCase.ClaimantWPAdvocate,
                ClaimantWPStatus = gratuityCase.ClaimantWPStatus,
                ClaimantWPDecision = gratuityCase.ClaimantWPDecision,
                ClaimantActionTaken = gratuityCase.ClaimantActionTaken,
                ClaimantApprovalNo = gratuityCase.ClaimantApprovalNo,
                ClaimantApprovalDate = gratuityCase.ClaimantApprovalDate
            };
            
            PopulateDropdowns(viewModel);
            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ManageClaimantAppeal(GratuityViewModel model)
        {
             // We only want to update specific fields, but UpdateCase updates all. 
             // Ideally we should partially update, but for now we fetch, merge, and save.
             
             // Since UpdateCase takes a full object and overwrites, we must be careful.
             // Best practice here without a specific partial update method is:
             // 1. Get existing case
             // 2. Update only modified fields
             // 3. Save
             
             var existingCase = await _gratuityRepo.GetCaseById(model.CaseID);
             if (existingCase == null) return NotFound();

             // Update Claimant Appeal Fields
             existingCase.ClaimantDivisionID = model.ClaimantDivisionID;
             existingCase.ClaimantArisingWPNumber = model.ClaimantArisingWPNumber;
             existingCase.ClaimantArisingWPYear = model.ClaimantArisingWPYear;
             existingCase.ClaimantMVCCurrentStatus = model.ClaimantMVCCurrentStatus;
             existingCase.ClaimantWPNumber = model.ClaimantWPNumber;
             existingCase.ClaimantWPYear = model.ClaimantWPYear;
             existingCase.ClaimantWPEntrustmentNo = model.ClaimantWPEntrustmentNo;
             existingCase.ClaimantWPEntrustmentDate = model.ClaimantWPEntrustmentDate;
             existingCase.ClaimantWPAdvocate = model.ClaimantWPAdvocate;
             existingCase.ClaimantWPStatus = model.ClaimantWPStatus;
             existingCase.ClaimantWPDecision = model.ClaimantWPDecision;
             existingCase.ClaimantActionTaken = model.ClaimantActionTaken;
             existingCase.ClaimantApprovalNo = model.ClaimantApprovalNo;
             existingCase.ClaimantApprovalDate = model.ClaimantApprovalDate;
             
             existingCase.ModifiedDate = DateTime.Now;
             existingCase.CreatedBy = User.Identity?.Name; // Or ModifiedBy if exists

             await _gratuityRepo.UpdateCase(existingCase);
             _ = _activityLogger.LogCaseUpdatedAsync("GRATUITY", existingCase.CaseID, $"Claimant WP {existingCase.ClaimantWPNumber}/{existingCase.ClaimantWPYear}", null, existingCase.AppealCourt ?? "High Court", null, model, $"Updated Gratuity Claimant Appeal WP #{existingCase.ClaimantWPNumber}/{existingCase.ClaimantWPYear}");
             
             TempData["SuccessMessage"] = "Claimant Appeal details updated successfully!";
             return RedirectToAction("ClaimantAppeals");
        }

        [HttpGet]
        public IActionResult CreateClaimantAppeal()
        {
            var viewModel = new GratuityViewModel();
            PopulateDropdowns(viewModel);
            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateClaimantAppeal(GratuityViewModel model)
        {
            try
            {
                var gratuityCase = new GratuityCase
                {
                    ClaimantWPNumber = model.ClaimantWPNumber,
                    ClaimantWPYear = model.ClaimantWPYear,
                    ClaimantArisingWPNumber = model.ClaimantArisingWPNumber,
                    AppealCourt = model.AppealCourt, // Using this for Step 2 Court
                    ClaimantHighCourtBench = model.ClaimantHighCourtBench,
                    ClaimantWPEntrustmentNo = model.ClaimantWPEntrustmentNo,
                    ClaimantWPEntrustmentDate = model.ClaimantWPEntrustmentDate,
                    ClaimantWPAdvocate = model.ClaimantWPAdvocate,
                    
                    // Default values for mandatory-ish fields if needed
                    CreatedDate = DateTime.Now,
                    ModifiedDate = DateTime.Now,
                    CreatedBy = User.Identity?.Name,
                    CaseStatus = "Pending"
                };

                int id = await _gratuityRepo.AddCase(gratuityCase);
                if (id > 0)
                {
                    _ = _activityLogger.LogCaseCreatedAsync("GRATUITY", id, $"Claimant WP {model.ClaimantWPNumber}/{model.ClaimantWPYear}", null, model.AppealCourt ?? "High Court", model, $"Registered new Gratuity Claimant Appeal WP #{model.ClaimantWPNumber}/{model.ClaimantWPYear}");
                    TempData["SuccessMessage"] = "New Claimant Appeal created successfully!";
                    return RedirectToAction("ClaimantAppeals");
                }
                
                ModelState.AddModelError("", "Failed to save the appeal.");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Error: " + ex.Message);
            }

            PopulateDropdowns(model);
            return View(model);
        }
    }
}
