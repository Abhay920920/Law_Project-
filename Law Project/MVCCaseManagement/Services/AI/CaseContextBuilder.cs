using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using MVCCaseManagement.DAL;
using MVCCaseManagement.Models;
using MVCCaseManagement.Models.AI;

namespace MVCCaseManagement.Services.AI
{
    public class CaseContextBuilder : ICaseContextBuilder
    {
        private readonly ICaseRepository _caseRepo;
        private readonly ILabourRepository _labourRepo;
        private readonly IAppealRepository _appealRepo;
        private readonly IGratuityRepository _gratuityRepo;
        private readonly IOtherCourtsRepository _otherCourtsRepo;
        private readonly ICaseNotingRepository _notingRepo;
        private readonly IECourtsRepository _eCourtsRepo;
        private readonly IDocumentTextExtractor _docExtractor;
        private readonly ILegalSearchService _legalSearch;
        private readonly IECourtsContextService _eCourtsContext;
        private readonly ILegalWebSearchService _webSearch;
        private readonly ISimilarCaseService _similarCaseService;
        private readonly DBHelper _db;
        private readonly ILogger<CaseContextBuilder> _logger;

        public CaseContextBuilder(
            ICaseRepository caseRepo,
            ILabourRepository labourRepo,
            IAppealRepository appealRepo,
            IGratuityRepository gratuityRepo,
            IOtherCourtsRepository otherCourtsRepo,
            ICaseNotingRepository notingRepo,
            IECourtsRepository eCourtsRepo,
            IDocumentTextExtractor docExtractor,
            ILegalSearchService legalSearch,
            IECourtsContextService eCourtsContext,
            ILegalWebSearchService webSearch,
            ISimilarCaseService similarCaseService,
            DBHelper db,
            ILogger<CaseContextBuilder> logger)
        {
            _caseRepo = caseRepo ?? throw new ArgumentNullException(nameof(caseRepo));
            _labourRepo = labourRepo ?? throw new ArgumentNullException(nameof(labourRepo));
            _appealRepo = appealRepo ?? throw new ArgumentNullException(nameof(appealRepo));
            _gratuityRepo = gratuityRepo ?? throw new ArgumentNullException(nameof(gratuityRepo));
            _otherCourtsRepo = otherCourtsRepo ?? throw new ArgumentNullException(nameof(otherCourtsRepo));
            _notingRepo = notingRepo ?? throw new ArgumentNullException(nameof(notingRepo));
            _eCourtsRepo = eCourtsRepo ?? throw new ArgumentNullException(nameof(eCourtsRepo));
            _docExtractor = docExtractor ?? throw new ArgumentNullException(nameof(docExtractor));
            _legalSearch = legalSearch ?? throw new ArgumentNullException(nameof(legalSearch));
            _eCourtsContext = eCourtsContext ?? throw new ArgumentNullException(nameof(eCourtsContext));
            _webSearch = webSearch ?? throw new ArgumentNullException(nameof(webSearch));
            _similarCaseService = similarCaseService ?? throw new ArgumentNullException(nameof(similarCaseService));
            _db = db ?? throw new ArgumentNullException(nameof(db));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public Task<CaseDossier?> BuildDossierAsync(string caseType, int caseId, CancellationToken cancellationToken = default)
        {
            return BuildDossierAsync(caseType, caseId, deepEnrich: false, cancellationToken);
        }

        public async Task<CaseDossier?> BuildDossierAsync(string caseType, int caseId, bool deepEnrich, CancellationToken cancellationToken = default)
        {
            string upperType = (caseType ?? "MVC").ToUpperInvariant();
            _logger.LogInformation("Building Nyaya Patha legal dossier for {CaseType} id {CaseId} (deepEnrich={DeepEnrich})", upperType, caseId, deepEnrich);

            CaseDossier? dossier = null;
            switch (upperType)
            {
                case "MVC":
                    dossier = await BuildMvcDossierAsync(caseId, cancellationToken);
                    break;
                case "LABOUR":
                    dossier = await BuildLabourDossierAsync(caseId, cancellationToken);
                    break;
                case "APPEAL":
                    dossier = await BuildAppealDossierAsync(caseId, cancellationToken);
                    break;
                case "GRATUITY":
                    dossier = await BuildGratuityDossierAsync(caseId, cancellationToken);
                    break;
                case "OTHERCOURTS":
                    dossier = await BuildOtherCourtsDossierAsync(caseId, cancellationToken);
                    break;
                default:
                    dossier = await BuildMvcDossierAsync(caseId, cancellationToken);
                    break;
            }

            // Only perform heavy external web / e-Courts scraping if explicitly requested (e.g., deep research query)
            if (dossier != null && deepEnrich)
            {
                await EnrichDossierWithECourtsAndWebSourcesAsync(dossier, cancellationToken);
            }

            return dossier;
        }

        private async Task EnrichDossierWithECourtsAndWebSourcesAsync(CaseDossier dossier, CancellationToken cancellationToken)
        {
            string searchSubject = $"{dossier.CaseType} {dossier.CaseNumber} {dossier.CourtName}";
            if (dossier.StructuredFacts.TryGetValue("Manner of Accident (Claim)", out var manner))
            {
                searchSubject += $" {manner}";
            }
            else if (dossier.StructuredFacts.TryGetValue("Dispute Category", out var disp))
            {
                searchSubject += $" {disp}";
            }

            try
            {
                // 1. e-Courts retrieval
                var eCourtsTask = _eCourtsContext.GetCaseSummaryAsync(dossier.CNRNumber, dossier.CaseType, dossier.CaseId, cancellationToken);

                // 2. Unified similar cases (merges internal, JudgementRepo, eCourts, web)
                var similarTask = _similarCaseService.FindSimilarCasesAsync(dossier.CaseType, dossier.CaseId, searchSubject, 6, cancellationToken);

                // 3. Authoritative legal web precedents (Supreme Court, Karnataka HC, eCourts)
                var webTask = _webSearch.SearchPrecedentsAsync(searchSubject, null, 5, cancellationToken);

                await Task.WhenAll(eCourtsTask, similarTask, webTask);

                dossier.ECourtsSummary = await eCourtsTask;
                if (dossier.ECourtsSummary?.History.Count > 0 && dossier.ECourtsHistory.Count == 0)
                {
                    dossier.ECourtsHistory.AddRange(dossier.ECourtsSummary.History);
                }

                dossier.UnifiedSimilarCases = await similarTask;
                dossier.ExternalLegalSources = await webTask;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Non-critical error enriching dossier {CaseType} #{CaseId} with e-Courts / web sources", dossier.CaseType, dossier.CaseId);
            }
        }

        private async Task<CaseDossier?> BuildMvcDossierAsync(int caseId, CancellationToken cancellationToken)
        {
            var mvc = _caseRepo.GetCaseById(caseId);
            if (mvc == null)
                return null;

            string petitionerNames = mvc.Petitioners != null && mvc.Petitioners.Count > 0
                ? string.Join("; ", mvc.Petitioners.Where(p => !string.IsNullOrWhiteSpace(p.PetitionerName)).Select(p => !string.IsNullOrWhiteSpace(p.Relationship) ? $"{p.PetitionerName} ({p.Relationship})" : p.PetitionerName))
                : "Claimant(s)";

            string respondentNames = mvc.Respondents != null && mvc.Respondents.Count > 0
                ? string.Join(", ", mvc.Respondents.Where(r => !string.IsNullOrWhiteSpace(r.RespondentName)).Select(r => r.RespondentName))
                : "NWKRTC / Others";

            string resolvedCourt = !string.IsNullOrWhiteSpace(mvc.EstName)
                ? mvc.EstName
                : (mvc.MACTName ?? "Motor Accident Claims Tribunal");

            var dossier = new CaseDossier
            {
                CaseId = mvc.CaseID,
                CaseType = "MVC",
                CaseNumber = $"MVC/{mvc.MVCNo}/{mvc.MVCYear}",
                CourtName = resolvedCourt,
                CurrentStage = mvc.CurrentStage ?? "Pending",
                NextHearingDate = mvc.NextHearingDate,
                Petitioner = petitionerNames,
                Respondent = respondentNames,
                VehicleNo = mvc.VehicleNo,
                CNRNumber = mvc.CNRNumber
            };

            // 1. Structured Facts & Procedures
            var facts = dossier.StructuredFacts;
            facts["Case Number"] = dossier.CaseNumber;
            facts["Tribunal / Court"] = dossier.CourtName;
            facts["Vehicle Number"] = mvc.VehicleNo ?? "Not specified";
            facts["Vehicle Type"] = mvc.VehicleType ?? "Bus";
            facts["Accident Date"] = mvc.AccidentDate.HasValue ? mvc.AccidentDate.Value.ToString("dd-MM-yyyy") : "Not specified";
            facts["Claim Amount"] = mvc.ClaimAmount.HasValue ? $"Rs. {mvc.ClaimAmount.Value:N2}" : "Not specified";
            facts["Award Amount"] = mvc.AdverseAward?.AwardAmount.HasValue == true ? $"Rs. {mvc.AdverseAward.AwardAmount.Value:N2}" : "N/A";
            facts["Stage"] = mvc.CurrentStage ?? "Not recorded";
            facts["Judge Name"] = mvc.ECourtsJudge ?? "Not specified";
            facts["Court Hall"] = mvc.CourtHall ?? mvc.ECourtsCourtNo ?? "Not specified";
            facts["CNR Number"] = mvc.CNRNumber ?? "None";
            facts["Disposal Status"] = mvc.DisposalStatus ?? "Pending";
            facts["Disposal Result"] = mvc.DisposalResult ?? "N/A";
            if (!string.IsNullOrWhiteSpace(mvc.AdvocateName))
                facts["Appearing Advocate"] = mvc.AdvocateName;
            if (!string.IsNullOrWhiteSpace(mvc.ClaimType))
                facts["Claim Nature"] = mvc.ClaimType;
            if (mvc.ClosureDate.HasValue)
                facts["Closure Date"] = mvc.ClosureDate.Value.ToString("dd-MM-yyyy");
            if (!string.IsNullOrWhiteSpace(mvc.PetitionFiledFor))
                facts["Petition Filed Under"] = mvc.PetitionFiledFor;
            if (!string.IsNullOrWhiteSpace(mvc.DocumentOutwardNo))
                facts["Document Outward No"] = $"{mvc.DocumentOutwardNo} (Dated: {(mvc.DocumentOutwardDate.HasValue ? mvc.DocumentOutwardDate.Value.ToString("dd-MM-yyyy") : "N/A")})";
            if (!string.IsNullOrWhiteSpace(mvc.ObjectionOutwardNo))
                facts["Objection Outward No"] = $"{mvc.ObjectionOutwardNo} (Filed: {(mvc.ObjectionFiledDate.HasValue ? mvc.ObjectionFiledDate.Value.ToString("dd-MM-yyyy") : "N/A")})";
            if (!string.IsNullOrWhiteSpace(mvc.EntrustmentNo))
                facts["Entrustment Details"] = $"{mvc.EntrustmentNo} (Dated: {(mvc.EntrustmentDate.HasValue ? mvc.EntrustmentDate.Value.ToString("dd-MM-yyyy") : "N/A")})";

            if (mvc.AdverseAward != null)
            {
                var adv = mvc.AdverseAward;
                facts["Manner of Accident (Claim)"] = adv.MannerOfAccident ?? "N/A";
                facts["Manner of Accident (RO Report)"] = adv.MannerOfAccidentRO ?? "N/A";
                facts["TR-18 Remarks"] = adv.TR18Remarks ?? "N/A";
                facts["Deceased in TR-18"] = adv.IsDeceasedInTR18 ? "Yes" : "No";
                facts["FIR Filed Against ST Driver"] = adv.IsFIRFiled.HasValue ? (adv.IsFIRFiled.Value ? "Yes" : "No") : "Not recorded";
                facts["Chargesheet Filed Against ST Driver"] = adv.IsChargeSheetFiled.HasValue ? (adv.IsChargeSheetFiled.Value ? "Yes" : "No") : "Not recorded";
                facts["Bus Camera Installed"] = adv.IsBusCameraInstalled.HasValue ? (adv.IsBusCameraInstalled.Value ? "Yes" : "No") : "Not recorded";
                facts["Camera Footage Produced"] = adv.IsCameraFootageProduced.HasValue ? (adv.IsCameraFootageProduced.Value ? "Yes" : "No") : "Not recorded";
                facts["Photographs Produced"] = adv.IsPhotographProduced.HasValue ? (adv.IsPhotographProduced.Value ? "Yes" : "No") : "Not recorded";
                facts["Delay Application Filed"] = adv.IsDelayApplicationFiled ? "Yes" : "No";
                facts["Delay Condoned"] = adv.IsDelayCondonedAdverse.HasValue ? (adv.IsDelayCondonedAdverse.Value ? "Yes" : "No") : "Not recorded";
                facts["Liability of Corporation (%)"] = $"{adv.LiabilityPercentage ?? 100}%";
                facts["Driver Punishment Status"] = adv.DriverPunishmentStatus ?? "None";
                facts["Advocate Opinion"] = adv.AdvocateOpinion ?? "None";
                facts["Law Officer (LO) Opinion"] = adv.LOOpinion ?? "None";
                facts["Divisional Controller Opinion"] = adv.DCOpinion ?? "None";
            }

            // 2. Case Notings (Internal Opinions)
            var notings = _notingRepo.GetNotings("MVC", caseId);
            foreach (var n in notings)
            {
                dossier.Notings.Add(new CaseNotingDto
                {
                    NotingId = n.NotingID,
                    NotingText = n.NotingText,
                    AuthorName = n.CreatedByName ?? n.CreatedByUsername,
                    AuthorRole = n.CreatedByRole ?? "Officer",
                    CreatedDate = n.CreatedDate
                });
            }

            // 3. e-Courts history if CNR exists
            if (!string.IsNullOrWhiteSpace(mvc.CNRNumber))
            {
                var tracked = await _eCourtsRepo.GetTrackedCaseByCnrAsync(mvc.CNRNumber);
                if (tracked != null)
                {
                    dossier.ECourtsHistory.Add(new ECourtsHistoryItemDto
                    {
                        EventDate = tracked.NextHearingDate ?? tracked.LastSyncedDate,
                        Stage = tracked.CurrentStage ?? "Live Synced",
                        CourtHall = tracked.CourtNo,
                        Judge = tracked.JudgeName,
                        OrderDetails = $"e-Courts Registered: {tracked.CaseTypeCode}/{tracked.RegNo}/{tracked.RegYear}"
                    });
                }
            }

            // 4. Extract Text from Uploaded Documents (controlled up to 2 key documents)
            var docCandidates = new List<(string Label, string? Path)>
            {
                ("Adverse Judgment Copy", mvc.AdverseAward?.AdverseJudgmentUploadPath),
                ("Interim Court Order", mvc.InterimOrderFilePath),
                ("Favor Judgment Copy", mvc.FavorJudgmentPath),
                ("Security Investigation", mvc.AdverseAward?.SecurityUploadPath)
            };

            int docsLoaded = 0;
            foreach (var (label, path) in docCandidates)
            {
                if (!string.IsNullOrWhiteSpace(path) && docsLoaded < 2)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    string text = await _docExtractor.ExtractTextAsync(path, maxPages: 10, cancellationToken);
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        dossier.ExtractedDocuments.Add(new DocumentExtractDto
                        {
                            DocumentName = label,
                            SourceFilePath = path,
                            PageCount = 10,
                            ExtractedText = text
                        });
                        docsLoaded++;
                    }
                }
            }

            // 5. Relevant Judgments from JUDGEMENT_REPO
            string searchTerms = $"{mvc.VehicleType} accident compensation 166 163A";
            var judgements = await _legalSearch.SearchJudgementsAsync(searchTerms, 3, cancellationToken);
            foreach (var j in judgements)
            {
                dossier.RelevantJudgments.Add(new RelevantJudgmentDto
                {
                    JudgementId = j.JudgementID,
                    Title = j.Title,
                    Court = j.Court ?? "High Court of Karnataka",
                    JudgementDate = j.JudgementDate,
                    KeyPrinciple = j.Remarks ?? j.Title,
                    Citation = $"Judgement Repo #{j.JudgementID} ({j.Court ?? "High Court"})"
                });
            }

            // 6. Similar Cases
            var similar = await _legalSearch.SearchSimilarCasesAsync("MVC", caseId, null, 4, cancellationToken);
            dossier.SimilarCases.AddRange(similar);

            // 7. Applicable Legal Provisions
            dossier.ApplicableProvisions.AddRange(_legalSearch.GetApplicableProvisions("MVC"));

            return dossier;
        }

        private async Task<CaseDossier?> BuildLabourDossierAsync(int caseId, CancellationToken cancellationToken)
        {
            var labour = _labourRepo.GetCaseById(caseId);
            if (labour == null)
                return null;

            string employee = !string.IsNullOrWhiteSpace(labour.PetitionerName) ? labour.PetitionerName : "Workman";
            var dossier = new CaseDossier
            {
                CaseId = labour.CaseID,
                CaseType = "LABOUR",
                CaseNumber = $"{labour.CaseType}/{labour.CaseNumber}/{labour.CaseYear}",
                CourtName = labour.CourtName ?? "Labour Court / Industrial Tribunal",
                CurrentStage = labour.CurrentStage ?? labour.CaseStatus ?? "Pending",
                NextHearingDate = null,
                Petitioner = employee,
                Respondent = "NWKRTC",
                CNRNumber = labour.CNRNumber
            };

            var facts = dossier.StructuredFacts;
            facts["Dispute Number"] = dossier.CaseNumber;
            facts["Employee Name"] = employee;
            facts["Employee No"] = labour.EmployeeNo ?? "N/A";
            facts["Designation"] = labour.Designation ?? "Not specified";
            facts["Dispute Nature"] = labour.NatureOfCase ?? "Termination / Dispute";
            facts["Nature of Misconduct"] = labour.NatureOfMisconduct ?? "Not recorded";
            facts["Working Status"] = labour.WorkingStatus ?? "Not recorded";
            facts["Stage"] = labour.CurrentStage ?? "Pending";
            facts["Advocate Name"] = labour.AdvocateName ?? "Not specified";

            // Case notings
            var notings = _notingRepo.GetNotings("LABOUR", caseId);
            foreach (var n in notings)
            {
                dossier.Notings.Add(new CaseNotingDto
                {
                    NotingId = n.NotingID,
                    NotingText = n.NotingText,
                    AuthorName = n.CreatedByName ?? n.CreatedByUsername,
                    AuthorRole = n.CreatedByRole ?? "Officer",
                    CreatedDate = n.CreatedDate
                });
            }

            // Document extraction if stay / award document uploaded
            if (!string.IsNullOrWhiteSpace(labour.CO_StayComplianceFilePath))
            {
                string text = await _docExtractor.ExtractTextAsync(labour.CO_StayComplianceFilePath, 10, cancellationToken);
                dossier.ExtractedDocuments.Add(new DocumentExtractDto
                {
                    DocumentName = "Stay Compliance Document",
                    SourceFilePath = labour.CO_StayComplianceFilePath,
                    ExtractedText = text
                });
            }

            // Relevant Judgments
            var judgements = await _legalSearch.SearchJudgementsAsync("Industrial Disputes Act Section 11A 33C domestic enquiry", 3, cancellationToken);
            foreach (var j in judgements)
            {
                dossier.RelevantJudgments.Add(new RelevantJudgmentDto
                {
                    JudgementId = j.JudgementID,
                    Title = j.Title,
                    Court = j.Court ?? "Labour Court / High Court",
                    JudgementDate = j.JudgementDate,
                    KeyPrinciple = j.Remarks ?? j.Title,
                    Citation = $"Judgement Repo #{j.JudgementID}"
                });
            }

            // Similar cases
            var similar = await _legalSearch.SearchSimilarCasesAsync("LABOUR", caseId, null, 4, cancellationToken);
            dossier.SimilarCases.AddRange(similar);

            // Applicable provisions
            dossier.ApplicableProvisions.AddRange(_legalSearch.GetApplicableProvisions("LABOUR"));

            return dossier;
        }

        private async Task<CaseDossier?> BuildAppealDossierAsync(int caseId, CancellationToken cancellationToken)
        {
            var appeal = _appealRepo.GetAppealByCaseId(caseId);
            if (appeal == null)
            {
                // Try treating caseId as CaseID of underlying MVC
                return await BuildMvcDossierAsync(caseId, cancellationToken);
            }

            var dossier = new CaseDossier
            {
                CaseId = appeal.AppealID,
                CaseType = "APPEAL",
                CaseNumber = string.IsNullOrWhiteSpace(appeal.CorpMFANumber) ? $"Appeal/{appeal.AppealID}" : $"MFA/{appeal.CorpMFANumber}/{appeal.CorpMFAYear}",
                CourtName = appeal.HighCourtBench ?? "High Court of Karnataka",
                CurrentStage = appeal.CorpMFAStage ?? appeal.CorpMFAStatus ?? "Pending",
                Petitioner = "NWKRTC / Appellant",
                Respondent = "Respondents",
                CNRNumber = appeal.CorpMFACNRNumber
            };

            var facts = dossier.StructuredFacts;
            facts["MFA Number"] = dossier.CaseNumber;
            facts["Bench"] = appeal.HighCourtBench ?? "Dharwad / Bengaluru";
            facts["Advocate"] = appeal.CorpMFAAdvocate ?? "Panel Advocate";
            facts["Stay Granted"] = appeal.StayGranted ? "Yes" : "No";
            facts["Stay Compliance Date"] = appeal.StayComplianceDate?.ToString("dd-MM-yyyy") ?? "N/A";
            facts["Law Officer Opinion"] = appeal.Opinion_LO ?? "None";
            facts["Dy CLO Opinion"] = appeal.Opinion_DyCLO ?? "None";
            facts["CLO Opinion"] = appeal.Opinion_CLO ?? "None";
            facts["MD Decision"] = appeal.Opinion_MD ?? "None";
            facts["MFA Status"] = appeal.CorpMFAStatus ?? "Pending";
            facts["Outcome"] = appeal.CorpMFAOutcome ?? "Pending";

            var notings = _notingRepo.GetNotings("APPEAL", appeal.AppealID);
            foreach (var n in notings)
            {
                dossier.Notings.Add(new CaseNotingDto
                {
                    NotingId = n.NotingID,
                    NotingText = n.NotingText,
                    AuthorName = n.CreatedByName ?? n.CreatedByUsername,
                    AuthorRole = n.CreatedByRole ?? "Officer",
                    CreatedDate = n.CreatedDate
                });
            }

            dossier.ApplicableProvisions.AddRange(_legalSearch.GetApplicableProvisions("APPEAL"));
            return dossier;
        }

        private async Task<CaseDossier?> BuildGratuityDossierAsync(int caseId, CancellationToken cancellationToken)
        {
            var gra = await _gratuityRepo.GetCaseById(caseId);
            if (gra == null)
                return null;

            var dossier = new CaseDossier
            {
                CaseId = gra.CaseID,
                CaseType = "GRATUITY",
                CaseNumber = $"PGA/{gra.PGANumber ?? gra.CaseID.ToString()}",
                CourtName = gra.CourtType ?? "Controlling Authority, Payment of Gratuity",
                CurrentStage = gra.CurrentStage ?? "Pending",
                Petitioner = gra.ClaimantName ?? "Workman",
                Respondent = "NWKRTC"
            };

            var facts = dossier.StructuredFacts;
            facts["PGA Number"] = dossier.CaseNumber;
            facts["Claimant"] = gra.ClaimantName ?? "Workman";
            facts["Authority"] = dossier.CourtName;
            facts["Qualifying Service (Corp)"] = gra.QualifyingService_Corp ?? "N/A";
            facts["Qualifying Service (CA)"] = gra.QualifyingService_CA ?? "N/A";
            facts["Claimed Amount"] = gra.AmountClaimed ?? "N/A";
            facts["Corporation Calculated Amount"] = gra.GratuityAmount_Corp_Act.HasValue ? $"Rs. {gra.GratuityAmount_Corp_Act.Value:N2}" : "N/A";
            facts["Ordered Amount (CA)"] = gra.OrderedAmount_CA.HasValue ? $"Rs. {gra.OrderedAmount_CA.Value:N2}" : "N/A";
            facts["Stage"] = gra.CurrentStage ?? "Pending";

            dossier.ApplicableProvisions.AddRange(_legalSearch.GetApplicableProvisions("GRATUITY"));
            return dossier;
        }

        private async Task<CaseDossier?> BuildOtherCourtsDossierAsync(int caseId, CancellationToken cancellationToken)
        {
            var other = _otherCourtsRepo.GetCaseById(caseId);
            if (other == null)
                return null;

            var dossier = new CaseDossier
            {
                CaseId = other.CaseID,
                CaseType = "OTHERCOURTS",
                CaseNumber = $"{other.CaseType}/{other.CaseNumber}/{other.CaseYear}",
                CourtName = other.Court ?? "Civil Court",
                CurrentStage = other.CaseStage ?? "Pending",
                Petitioner = other.PetitionerName ?? "Plaintiff / Petitioner",
                Respondent = other.RespondentName ?? "NWKRTC",
                CNRNumber = other.CNRNumber
            };

            var facts = dossier.StructuredFacts;
            facts["Case Number"] = dossier.CaseNumber;
            facts["Court Type"] = other.Court ?? "Civil Court";
            facts["Subject Matter"] = other.CaseNature ?? "Civil Dispute";
            facts["Stage"] = other.CaseStage ?? "Pending";

            return dossier;
        }

        public async Task<CaseDossier?> ResolveAndBuildDossierAsync(string caseSearchTerm, CancellationToken cancellationToken = default)
        {
            return await ResolveAndBuildDossierAsync(caseSearchTerm, null, 5, cancellationToken);
        }

        public async Task<CaseDossier?> ResolveAndBuildDossierAsync(string caseSearchTerm, LegalQueryPlan? plan, int userDivisionId = 5, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(caseSearchTerm))
                return null;

            string clean = caseSearchTerm.Trim();

            // Priority 0: Structured Plan Dispatch if deterministic database query
            if (plan != null && plan.IsDeterministicDatabaseQuery)
            {
                switch (plan.Intent)
                {
                    case LegalQueryIntent.VehicleHistory:
                        if (!string.IsNullOrWhiteSpace(plan.VehicleNumber))
                        {
                            var vehDossier = await ResolveVehicleLitigationHistoryAsync(plan.VehicleNumber, userDivisionId, cancellationToken);
                            if (vehDossier != null) return vehDossier;
                        }
                        break;

                    case LegalQueryIntent.AdvocatePortfolio:
                        if (!string.IsNullOrWhiteSpace(plan.AdvocateName))
                        {
                            var advDossier = await ResolveAdvocatePortfolioAsync(plan.AdvocateName, userDivisionId, cancellationToken);
                            if (advDossier != null) return advDossier;
                        }
                        break;

                    case LegalQueryIntent.HearingCalendar:
                        var calDossier = await ResolveHearingCalendarAsync(plan, userDivisionId, cancellationToken);
                        if (calDossier != null) return calDossier;
                        break;

                    case LegalQueryIntent.FinancialRisk:
                        var finDossier = await ResolveFinancialExposureAsync(plan.AmountThreshold ?? 1000000m, plan.DivisionId, userDivisionId, cancellationToken);
                        if (finDossier != null) return finDossier;
                        break;

                    case LegalQueryIntent.ExecutionRisk:
                        var epDossier = await ResolveExecutionPetitionsAsync(plan.DivisionId, userDivisionId, cancellationToken);
                        if (epDossier != null) return epDossier;
                        break;

                    case LegalQueryIntent.CompoundFilter:
                        var compDossier = await ResolveCompoundFilterAsync(plan, userDivisionId, cancellationToken);
                        if (compDossier != null) return compDossier;
                        break;

                    case LegalQueryIntent.DivisionalStatistics:
                        var divDossier = await BuildDivisionalDossierAsync(clean, cancellationToken);
                        if (divDossier != null) return divDossier;
                        break;
                }
            }

            // 1. Priority Match: 16-character CNR Number (e.g. KAHC020050702018 or KADH010012342024)
            var cnrMatch = Regex.Match(clean, @"\b([A-Z]{4}\d{12})\b", RegexOptions.IgnoreCase);
            if (cnrMatch.Success)
            {
                string cnr = cnrMatch.Groups[1].Value.ToUpperInvariant();
                using var conn = _db.GetConnection();
                await conn.OpenAsync(cancellationToken);

                // Check MVC_CASES by CNRNumber
                string mvcCnrSql = "SELECT TOP 1 CaseID FROM MVC_CASES WHERE UPPER(CNRNumber) = @CNR ORDER BY CaseID DESC";
                using (var cmd = new SqlCommand(mvcCnrSql, conn))
                {
                    cmd.Parameters.AddWithValue("@CNR", cnr);
                    var obj = await cmd.ExecuteScalarAsync(cancellationToken);
                    if (obj != null && obj != DBNull.Value)
                    {
                        int foundCaseId = Convert.ToInt32(obj);
                        return await BuildDossierAsync("MVC", foundCaseId, cancellationToken);
                    }
                }

                // Check LABOUR_CASES by CNRNumber
                string labourCnrSql = "SELECT TOP 1 CaseID FROM LABOUR_CASES WHERE UPPER(CNRNumber) = @CNR ORDER BY CaseID DESC";
                using (var cmd = new SqlCommand(labourCnrSql, conn))
                {
                    cmd.Parameters.AddWithValue("@CNR", cnr);
                    var obj = await cmd.ExecuteScalarAsync(cancellationToken);
                    if (obj != null && obj != DBNull.Value)
                    {
                        int foundCaseId = Convert.ToInt32(obj);
                        return await BuildDossierAsync("LABOUR", foundCaseId, cancellationToken);
                    }
                }
            }

            // 2. Match patterns like "MVC/123/2024" or "MVC 123 2024" or "KID/45/2023"
            var mvcMatch = Regex.Match(clean, @"\b(?:MVC[/\s-]*)(\d+)[/\s-]*(20\d\d|\d\d)\b", RegexOptions.IgnoreCase);
            if (mvcMatch.Success && !clean.StartsWith("KID", StringComparison.OrdinalIgnoreCase) && !clean.StartsWith("MFA", StringComparison.OrdinalIgnoreCase))
            {
                string mvcNo = mvcMatch.Groups[1].Value;
                string yearStr = mvcMatch.Groups[2].Value;
                int year = yearStr.Length == 2 ? 2000 + int.Parse(yearStr) : int.Parse(yearStr);

                using var conn = _db.GetConnection();
                await conn.OpenAsync(cancellationToken);
                string sql = "SELECT TOP 1 CaseID FROM MVC_CASES WHERE MVCNo = @No AND MVCYear = @Year ORDER BY CaseID DESC";
                using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@No", mvcNo);
                cmd.Parameters.AddWithValue("@Year", year);

                var obj = await cmd.ExecuteScalarAsync(cancellationToken);
                if (obj != null && obj != DBNull.Value)
                {
                    int foundCaseId = Convert.ToInt32(obj);
                    return await BuildDossierAsync("MVC", foundCaseId, cancellationToken);
                }
            }

            // 3. Try Labour search
            var labourMatch = Regex.Match(clean, @"(KID|ID|REF|WP|WA|LCA)[/\s-]*(\d+)[/\s-]*(20\d\d|\d\d)", RegexOptions.IgnoreCase);
            if (labourMatch.Success)
            {
                string subType = labourMatch.Groups[1].Value;
                string no = labourMatch.Groups[2].Value;
                string yearStr = labourMatch.Groups[3].Value;
                int year = yearStr.Length == 2 ? 2000 + int.Parse(yearStr) : int.Parse(yearStr);

                using var conn = _db.GetConnection();
                await conn.OpenAsync(cancellationToken);
                string sql = "SELECT TOP 1 CaseID FROM LABOUR_CASES WHERE CaseType = @Type AND CaseNumber = @No AND CaseYear = @Year ORDER BY CaseID DESC";
                using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@Type", subType);
                cmd.Parameters.AddWithValue("@No", no);
                cmd.Parameters.AddWithValue("@Year", year);

                var obj = await cmd.ExecuteScalarAsync(cancellationToken);
                if (obj != null && obj != DBNull.Value)
                {
                    int foundCaseId = Convert.ToInt32(obj);
                    return await BuildDossierAsync("LABOUR", foundCaseId, cancellationToken);
                }
            }

            // 4. Vehicle Number Match (e.g. KA-31-F-1310, KA 31 F 1310, KA31F1310)
            var vehMatch = Regex.Match(clean, @"\b(KA[- ]?\d{1,2}[- ]?[A-Z]{1,3}[- ]?\d{1,4})\b", RegexOptions.IgnoreCase);
            if (vehMatch.Success)
            {
                string rawVeh = vehMatch.Groups[1].Value;
                var vehHistory = await ResolveVehicleLitigationHistoryAsync(rawVeh, userDivisionId, cancellationToken);
                if (vehHistory != null) return vehHistory;
            }

            // 5. Claimant / Petitioner Name Search
            if (clean.Contains("claimant", StringComparison.OrdinalIgnoreCase) || 
                clean.Contains("petitioner", StringComparison.OrdinalIgnoreCase) ||
                clean.Contains("deceased", StringComparison.OrdinalIgnoreCase) ||
                clean.Contains("case of", StringComparison.OrdinalIgnoreCase))
            {
                var stopWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
                    "what", "is", "the", "details", "abt", "about", "this", "case", "of", "claimant", "petitioner", "status", "give", "me", "show", "tell", "deceased", "who", "find", "and", "for"
                };
                var words = clean.Split(new[] { ' ', ',', ':', ';', '?' }, StringSplitOptions.RemoveEmptyEntries)
                                 .Where(w => !stopWords.Contains(w) && w.Length >= 4)
                                 .ToList();

                if (words.Count > 0)
                {
                    string candidateTerm = words.First();
                    using var conn = _db.GetConnection();
                    await conn.OpenAsync(cancellationToken);
                    string petSql = "SELECT TOP 1 CaseID FROM MVC_CASE_PETITIONERS WHERE PetitionerName LIKE @Pet ORDER BY CaseID DESC";
                    using var cmd = new SqlCommand(petSql, conn);
                    cmd.Parameters.AddWithValue("@Pet", $"%{candidateTerm}%");

                    var obj = await cmd.ExecuteScalarAsync(cancellationToken);
                    if (obj != null && obj != DBNull.Value)
                    {
                        int foundCaseId = Convert.ToInt32(obj);
                        return await BuildDossierAsync("MVC", foundCaseId, cancellationToken);
                    }
                }
            }

            // 6. Direct ID match if numeric
            if (int.TryParse(clean, out int directId))
            {
                var candidate = await BuildDossierAsync("MVC", directId, cancellationToken);
                if (candidate != null) return candidate;

                candidate = await BuildDossierAsync("LABOUR", directId, cancellationToken);
                if (candidate != null) return candidate;
            }

            // Try Divisional Statistics Match
            var divCandidate = await BuildDivisionalDossierAsync(clean, cancellationToken);
            if (divCandidate != null) return divCandidate;

            return null;
        }

        private async Task<CaseDossier?> ResolveVehicleLitigationHistoryAsync(string vehicleNo, int userDivisionId, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(vehicleNo)) return null;

            string cleanVeh = Regex.Replace(vehicleNo, @"[\s-]+", "").ToUpperInvariant();
            string displayVeh = vehicleNo.ToUpperInvariant().Trim();

            try
            {
                using var conn = _db.GetConnection();
                await conn.OpenAsync(cancellationToken);

                // Fetch all MVC accident cases for this vehicle
                string sql = @"
                    SELECT 
                        c.CaseID,
                        c.MVCNo,
                        c.MVCYear,
                        c.VehicleNo,
                        c.AccidentDate,
                        c.AccidentLocation,
                        c.DriverName,
                        c.DriverTokenNo,
                        ISNULL((SELECT TOP 1 PetitionerName FROM MVC_CASE_PETITIONERS WHERE CaseID = c.CaseID AND PetitionerName IS NOT NULL AND PetitionerName <> ''), 'Claimant') AS PetitionerName,
                        ISNULL(m.MACTName, ISNULL(c.EstName, 'MACT Tribunal')) AS CourtName,
                        d.DivisionNameEnglish AS DivisionName,
                        ISNULL(c.CurrentStage, 'Pending') AS CurrentStage,
                        ISNULL(c.PendDispStatus, 'P') AS PendDispStatus,
                        c.DisposalStatus,
                        c.DisposalResult,
                        c.ClaimAmount,
                        c.AwardAmount,
                        c.NextHearingDate,
                        adv.AwardAmount AS AdverseAwardAmount,
                        adv.DriverPunishmentStatus,
                        adv.IsFIRFiled,
                        adv.TR18Remarks
                    FROM MVC_CASES c
                    LEFT JOIN DIVISION_MASTER d ON c.DivisionID = d.DivisionID
                    LEFT JOIN MACT_MASTER m ON c.MACTID = m.MACTID
                    LEFT JOIN MVC_CASE_ADVERSE_DETAILS adv ON c.CaseID = adv.CaseID
                    WHERE REPLACE(REPLACE(UPPER(c.VehicleNo), '-', ''), ' ', '') LIKE @VehPattern
                    ORDER BY c.AccidentDate DESC, c.MVCYear DESC, c.CaseID DESC";

                var cases = new List<dynamic>();
                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@VehPattern", $"%{cleanVeh}%");
                    using var rdr = await cmd.ExecuteReaderAsync(cancellationToken);
                    while (await rdr.ReadAsync(cancellationToken))
                    {
                        cases.Add(new
                        {
                            CaseId = rdr.GetInt32(0),
                            MVCNo = rdr.IsDBNull(1) ? "N/A" : rdr.GetString(1),
                            MVCYear = rdr.IsDBNull(2) ? 0 : rdr.GetInt32(2),
                            VehicleNo = rdr.IsDBNull(3) ? displayVeh : rdr.GetString(3),
                            AccidentDate = rdr.IsDBNull(4) ? (DateTime?)null : rdr.GetDateTime(4),
                            AccidentLocation = rdr.IsDBNull(5) ? "N/A" : rdr.GetString(5),
                            DriverName = rdr.IsDBNull(6) ? "N/A" : rdr.GetString(6),
                            DriverTokenNo = rdr.IsDBNull(7) ? "N/A" : rdr.GetString(7),
                            PetitionerName = rdr.IsDBNull(8) ? "Claimant" : rdr.GetString(8),
                            CourtName = rdr.IsDBNull(9) ? "MACT Tribunal" : rdr.GetString(9),
                            DivisionName = rdr.IsDBNull(10) ? "N/A" : rdr.GetString(10),
                            CurrentStage = rdr.IsDBNull(11) ? "Pending" : rdr.GetString(11),
                            PendDispStatus = rdr.IsDBNull(12) ? "P" : rdr.GetString(12),
                            DisposalStatus = rdr.IsDBNull(13) ? "" : rdr.GetString(13),
                            DisposalResult = rdr.IsDBNull(14) ? "" : rdr.GetString(14),
                            ClaimAmount = rdr.IsDBNull(15) ? (decimal?)null : rdr.GetDecimal(15),
                            AwardAmount = rdr.IsDBNull(16) ? (decimal?)null : rdr.GetDecimal(16),
                            NextHearingDate = rdr.IsDBNull(17) ? (DateTime?)null : rdr.GetDateTime(17),
                            AdverseAward = rdr.IsDBNull(18) ? (decimal?)null : rdr.GetDecimal(18),
                            DriverPunishment = rdr.IsDBNull(19) ? "N/A" : rdr.GetString(19),
                            IsFIRFiled = !rdr.IsDBNull(20) && (rdr.GetBoolean(20) || rdr.GetInt32(20) == 1),
                            TR18Remarks = rdr.IsDBNull(21) ? "" : rdr.GetString(21)
                        });
                    }
                }

                // Check Execution Petitions for this vehicle
                string epSql = @"
                    SELECT 
                        ep.EPID,
                        ep.EPNumber,
                        ep.EPYear,
                        ep.EPCourt,
                        ep.ArisingFromMVCNo,
                        ep.EPStatus,
                        ep.AwardAmount,
                        ep.AmountPaid,
                        ep.NextHearingDate
                    FROM MVC_EP_DETAILS ep
                    WHERE REPLACE(REPLACE(UPPER(ep.VehicleNo), '-', ''), ' ', '') LIKE @VehPattern
                    ORDER BY ep.EPID DESC";

                var epList = new List<dynamic>();
                using (var epCmd = new SqlCommand(epSql, conn))
                {
                    epCmd.Parameters.AddWithValue("@VehPattern", $"%{cleanVeh}%");
                    using var rdr = await epCmd.ExecuteReaderAsync(cancellationToken);
                    while (await rdr.ReadAsync(cancellationToken))
                    {
                        epList.Add(new
                        {
                            EPID = rdr.GetInt32(0),
                            EPNumber = rdr.IsDBNull(1) ? "N/A" : rdr.GetString(1),
                            EPYear = rdr.IsDBNull(2) ? 0 : rdr.GetInt32(2),
                            EPCourt = rdr.IsDBNull(3) ? "Executing Court" : rdr.GetString(3),
                            ArisingMVC = rdr.IsDBNull(4) ? "N/A" : rdr.GetString(4),
                            EPStatus = rdr.IsDBNull(5) ? "Pending" : rdr.GetString(5),
                            AwardAmount = rdr.IsDBNull(6) ? (decimal?)null : rdr.GetDecimal(6),
                            AmountPaid = rdr.IsDBNull(7) ? (decimal?)null : rdr.GetDecimal(7),
                            NextHearingDate = rdr.IsDBNull(8) ? (DateTime?)null : rdr.GetDateTime(8)
                        });
                    }
                }

                if (cases.Count == 0 && epList.Count == 0)
                {
                    return new CaseDossier
                    {
                        CaseType = "VehicleLitigationHistory",
                        CaseNumber = $"Vehicle History - {displayVeh}",
                        CourtName = "NWKRTC Central Database",
                        CurrentStage = "No Records Found",
                        Petitioner = "N/A",
                        Respondent = "NWKRTC",
                        StructuredFacts = new Dictionary<string, string>
                        {
                            ["DirectMarkdown"] = $"### 🚗 Vehicle Litigation History: **{displayVeh}**\n\nNo litigation records or accident claims were found in the NWKRTC database for vehicle **{displayVeh}**.\n\n*Source: NWKRTC Central Database (Direct SQL Query — 100% Real-Time & Verified).* "
                        }
                    };
                }

                int totalCases = cases.Count;
                int pendingCases = cases.Count(c => c.PendDispStatus == "P" && c.DisposalStatus != "DISPOSED");
                int disposedCases = totalCases - pendingCases;
                decimal totalClaimAmt = cases.Where(c => c.ClaimAmount != null).Sum(c => (decimal)c.ClaimAmount);
                decimal totalAwardAmt = cases.Where(c => c.AwardAmount != null || c.AdverseAward != null)
                                             .Sum(c => (decimal)(c.AdverseAward ?? c.AwardAmount ?? 0m));

                var sb = new System.Text.StringBuilder();
                sb.AppendLine("## NYAYA PATHA — Vehicle Litigation & Accident History");
                sb.AppendLine();
                sb.AppendLine("---");
                sb.AppendLine();
                sb.AppendLine($"### 🚗 Official Vehicle Profile: **{displayVeh}**");
                sb.AppendLine();
                sb.AppendLine("| Metric | Value | Status |");
                sb.AppendLine("|---|:---:|:---:|");
                sb.AppendLine($"| **Total Accident Claims (MVC)** | **{totalCases}** | Historical Record |");
                sb.AppendLine($"| **Pending MACT Litigation** | **{pendingCases}** | Active / Under Trial |");
                sb.AppendLine($"| **Disposed Claims** | **{disposedCases}** | Concluded |");
                sb.AppendLine($"| **Total Claim Liability Demanded** | **Rs. {totalClaimAmt:N0}** | Combined Claim Amount |");
                sb.AppendLine($"| **Total Award Liability Fastened** | **Rs. {totalAwardAmt:N0}** | Awarded by Tribunals |");
                sb.AppendLine($"| **Active Execution Petitions (EP)** | **{epList.Count}** | {(epList.Count > 0 ? "⚠️ Recovery Proceedings" : "None")} |");
                sb.AppendLine();

                sb.AppendLine("#### 📋 Detailed MVC Claims Log for Vehicle");
                sb.AppendLine("| Case No | Year | Accident Date | Claimant | MACT Tribunal / Division | Stage | Claim (Rs.) | Award (Rs.) |");
                sb.AppendLine("|---|:---:|:---:|---|---|:---:|:---:|:---:|");
                foreach (var c in cases)
                {
                    string accDtStr = c.AccidentDate != null ? ((DateTime)c.AccidentDate).ToString("dd-MM-yyyy") : "N/A";
                    string claimStr = c.ClaimAmount != null ? $"Rs. {((decimal)c.ClaimAmount):N0}" : "N/A";
                    decimal? awd = c.AdverseAward ?? c.AwardAmount;
                    string awardStr = awd != null && awd > 0 ? $"Rs. {awd:N0}" : "-";
                    sb.AppendLine($"| **MVC/{c.MVCNo}/{c.MVCYear}** | {c.MVCYear} | {accDtStr} | {c.PetitionerName} | {c.CourtName} ({c.DivisionName}) | **{c.CurrentStage}** | {claimStr} | {awardStr} |");
                }
                sb.AppendLine();

                if (epList.Count > 0)
                {
                    sb.AppendLine("#### ⚡ Execution Petitions (EP) Linked to Vehicle");
                    sb.AppendLine("| EP No | Arising MVC | Executing Court | Status | Award Amount | Paid Amount | Next Hearing |");
                    sb.AppendLine("|---|---|---|:---:|:---:|:---:|:---:|");
                    foreach (var ep in epList)
                    {
                        string nextDt = ep.NextHearingDate != null ? ((DateTime)ep.NextHearingDate).ToString("dd-MM-yyyy") : "N/A";
                        string awdStr = ep.AwardAmount != null ? $"Rs. {((decimal)ep.AwardAmount):N0}" : "N/A";
                        string paidStr = ep.AmountPaid != null ? $"Rs. {((decimal)ep.AmountPaid):N0}" : "Rs. 0";
                        sb.AppendLine($"| **{ep.EPNumber}** | MVC/{ep.ArisingMVC} | {ep.EPCourt} | **{ep.EPStatus}** | {awdStr} | {paidStr} | {nextDt} |");
                    }
                    sb.AppendLine();
                }

                sb.AppendLine("---");
                sb.AppendLine("*Source: NWKRTC Central Case Management Database (Direct SQL Query — 100% Real-Time & Verified, Zero External LLM Cost).*");

                var dossier = new CaseDossier
                {
                    CaseType = "VehicleLitigationHistory",
                    CaseNumber = $"Vehicle History - {displayVeh}",
                    CourtName = "NWKRTC Central Database",
                    CurrentStage = $"{pendingCases} Pending / {totalCases} Total",
                    Petitioner = "Various Claimants",
                    Respondent = "NWKRTC",
                    VehicleNo = displayVeh
                };
                dossier.StructuredFacts["DirectMarkdown"] = sb.ToString();
                dossier.StructuredFacts["Total Cases"] = totalCases.ToString();
                dossier.StructuredFacts["Pending Cases"] = pendingCases.ToString();
                return dossier;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resolving vehicle litigation history for {VehicleNo}", vehicleNo);
                return null;
            }
        }

        private async Task<CaseDossier?> ResolveAdvocatePortfolioAsync(string advocateName, int userDivisionId, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(advocateName)) return null;

            string cleanAdv = advocateName.Trim();
            try
            {
                using var conn = _db.GetConnection();
                await conn.OpenAsync(cancellationToken);

                // 1. MVC cases for this advocate
                string mvcSql = @"
                    SELECT TOP 30
                        c.CaseID,
                        c.MVCNo,
                        c.MVCYear,
                        COALESCE(c.AdvocateName, a.AdvocateName) AS AdvName,
                        ISNULL(m.MACTName, ISNULL(c.EstName, 'MACT Tribunal')) AS CourtName,
                        d.DivisionNameEnglish AS DivisionName,
                        ISNULL(c.CurrentStage, 'Pending') AS CurrentStage,
                        ISNULL(c.PendDispStatus, 'P') AS PendDispStatus,
                        c.DisposalStatus,
                        c.DisposalResult,
                        c.NextHearingDate,
                        c.ClaimAmount,
                        c.AwardAmount
                    FROM MVC_CASES c
                    LEFT JOIN ADVOCATE_MASTER a ON c.AdvocateID = a.AdvocateID
                    LEFT JOIN DIVISION_MASTER d ON c.DivisionID = d.DivisionID
                    LEFT JOIN MACT_MASTER m ON c.MACTID = m.MACTID
                    WHERE (c.AdvocateName LIKE @Adv OR a.AdvocateName LIKE @Adv)
                    ORDER BY c.MVCYear DESC, c.CaseID DESC";

                var mvcList = new List<dynamic>();
                using (var cmd = new SqlCommand(mvcSql, conn))
                {
                    cmd.Parameters.AddWithValue("@Adv", $"%{cleanAdv}%");
                    using var rdr = await cmd.ExecuteReaderAsync(cancellationToken);
                    while (await rdr.ReadAsync(cancellationToken))
                    {
                        mvcList.Add(new
                        {
                            CaseId = rdr.GetInt32(0),
                            MVCNo = rdr.IsDBNull(1) ? "N/A" : rdr.GetString(1),
                            MVCYear = rdr.IsDBNull(2) ? 0 : rdr.GetInt32(2),
                            AdvName = rdr.IsDBNull(3) ? cleanAdv : rdr.GetString(3),
                            CourtName = rdr.IsDBNull(4) ? "MACT" : rdr.GetString(4),
                            DivisionName = rdr.IsDBNull(5) ? "N/A" : rdr.GetString(5),
                            CurrentStage = rdr.IsDBNull(6) ? "Pending" : rdr.GetString(6),
                            PendDispStatus = rdr.IsDBNull(7) ? "P" : rdr.GetString(7),
                            DisposalStatus = rdr.IsDBNull(8) ? "" : rdr.GetString(8),
                            DisposalResult = rdr.IsDBNull(9) ? "" : rdr.GetString(9),
                            NextHearingDate = rdr.IsDBNull(10) ? (DateTime?)null : rdr.GetDateTime(10),
                            ClaimAmount = rdr.IsDBNull(11) ? (decimal?)null : rdr.GetDecimal(11),
                            AwardAmount = rdr.IsDBNull(12) ? (decimal?)null : rdr.GetDecimal(12)
                        });
                    }
                }

                // 2. Labour cases for this advocate
                string labourSql = @"
                    SELECT TOP 30
                        c.CaseID,
                        c.CaseType,
                        c.CaseNumber,
                        c.CaseYear,
                        COALESCE(c.AdvocateName, am.AdvocateName, la.AdvocateName, hca.AdvocateName) AS AdvName,
                        ISNULL(lc.CourtName, 'Labour Court') AS CourtName,
                        dm.DivisionNameEnglish AS DivisionName,
                        ISNULL(c.CurrentStage, 'Pending') AS CurrentStage,
                        c.CaseStatus,
                        c.DisposalResult,
                        c.NextHearingDate
                    FROM LABOUR_CASES c
                    LEFT JOIN LABOUR_COURTS lc ON c.CourtID = lc.CourtID
                    LEFT JOIN DIVISION_MASTER dm ON c.DivisionID = dm.DivisionID
                    LEFT JOIN ADVOCATE_MASTER am ON c.AdvocateID = am.AdvocateID
                    LEFT JOIN LABOUR_ADVOCATES la ON c.AdvocateID = la.AdvocateID
                    LEFT JOIN HIGH_COURT_ADVOCATES hca ON c.AdvocateID = hca.AdvocateID
                    WHERE (c.AdvocateName LIKE @Adv OR am.AdvocateName LIKE @Adv OR la.AdvocateName LIKE @Adv OR hca.AdvocateName LIKE @Adv)
                    ORDER BY c.CaseYear DESC, c.CaseID DESC";

                var labourList = new List<dynamic>();
                using (var cmd = new SqlCommand(labourSql, conn))
                {
                    cmd.Parameters.AddWithValue("@Adv", $"%{cleanAdv}%");
                    using var rdr = await cmd.ExecuteReaderAsync(cancellationToken);
                    while (await rdr.ReadAsync(cancellationToken))
                    {
                        labourList.Add(new
                        {
                            CaseId = rdr.GetInt32(0),
                            CaseType = rdr.IsDBNull(1) ? "KID" : rdr.GetString(1),
                            CaseNumber = rdr.IsDBNull(2) ? "N/A" : rdr.GetString(2),
                            CaseYear = rdr.IsDBNull(3) ? 0 : rdr.GetInt32(3),
                            AdvName = rdr.IsDBNull(4) ? cleanAdv : rdr.GetString(4),
                            CourtName = rdr.IsDBNull(5) ? "Labour Court" : rdr.GetString(5),
                            DivisionName = rdr.IsDBNull(6) ? "N/A" : rdr.GetString(6),
                            CurrentStage = rdr.IsDBNull(7) ? "Pending" : rdr.GetString(7),
                            CaseStatus = rdr.IsDBNull(8) ? "" : rdr.GetString(8),
                            DisposalResult = rdr.IsDBNull(9) ? "" : rdr.GetString(9),
                            NextHearingDate = rdr.IsDBNull(10) ? (DateTime?)null : rdr.GetDateTime(10)
                        });
                    }
                }

                // 3. Appeals for this advocate
                string appSql = @"
                    SELECT TOP 20
                        app.AppealID,
                        app.CorpMFANumber,
                        app.CorpMFAYear,
                        app.CorpMFAAdvocate,
                        ISNULL(app.HighCourtBench, 'High Court of Karnataka') AS CourtName,
                        d.DivisionNameEnglish AS DivisionName,
                        ISNULL(app.CorpMFAStage, 'Pending') AS CurrentStage,
                        app.CorpMFAStatus,
                        app.CorpMFAOutcome,
                        app.NextHearingDate
                    FROM APPEAL_DETAILS app
                    LEFT JOIN MVC_CASES mc ON app.CaseID = mc.CaseID
                    LEFT JOIN DIVISION_MASTER d ON mc.DivisionID = d.DivisionID
                    WHERE app.CorpMFAAdvocate LIKE @Adv
                    ORDER BY app.CorpMFAYear DESC, app.AppealID DESC";

                var appList = new List<dynamic>();
                using (var cmd = new SqlCommand(appSql, conn))
                {
                    cmd.Parameters.AddWithValue("@Adv", $"%{cleanAdv}%");
                    using var rdr = await cmd.ExecuteReaderAsync(cancellationToken);
                    while (await rdr.ReadAsync(cancellationToken))
                    {
                        appList.Add(new
                        {
                            AppealId = rdr.GetInt32(0),
                            MFANo = rdr.IsDBNull(1) ? "N/A" : rdr.GetString(1),
                            MFAYear = rdr.IsDBNull(2) ? 0 : rdr.GetInt32(2),
                            AdvName = rdr.IsDBNull(3) ? cleanAdv : rdr.GetString(3),
                            CourtName = rdr.IsDBNull(4) ? "High Court" : rdr.GetString(4),
                            DivisionName = rdr.IsDBNull(5) ? "N/A" : rdr.GetString(5),
                            CurrentStage = rdr.IsDBNull(6) ? "Pending" : rdr.GetString(6),
                            Status = rdr.IsDBNull(7) ? "" : rdr.GetString(7),
                            Outcome = rdr.IsDBNull(8) ? "" : rdr.GetString(8),
                            NextHearingDate = rdr.IsDBNull(9) ? (DateTime?)null : rdr.GetDateTime(9)
                        });
                    }
                }

                int totalFound = mvcList.Count + labourList.Count + appList.Count;
                if (totalFound == 0)
                {
                    return new CaseDossier
                    {
                        CaseType = "AdvocatePortfolio",
                        CaseNumber = $"Advocate - {cleanAdv}",
                        CourtName = "NWKRTC Central Database",
                        CurrentStage = "No Records Found",
                        Petitioner = cleanAdv,
                        Respondent = "NWKRTC",
                        StructuredFacts = new Dictionary<string, string>
                        {
                            ["DirectMarkdown"] = $"### ⚖️ Advocate Caseload Portfolio: **{cleanAdv}**\n\nNo active or disposed cases found in the NWKRTC database for Advocate **{cleanAdv}**.\n\n*Source: NWKRTC Central Database (Direct SQL Query — 100% Real-Time & Verified).* "
                        }
                    };
                }

                int mvcPending = mvcList.Count(c => c.PendDispStatus == "P" && c.DisposalStatus != "DISPOSED");
                int mvcDisposed = mvcList.Count - mvcPending;
                int labourPending = labourList.Count(c => c.CaseStatus != "DISPOSED");
                int labourDisposed = labourList.Count - labourPending;
                int appPending = appList.Count(c => c.Status != "DISPOSED" && c.Status != "CLOSED");
                int appDisposed = appList.Count - appPending;

                int totalActive = mvcPending + labourPending + appPending;
                int totalDisposed = mvcDisposed + labourDisposed + appDisposed;

                var sb = new System.Text.StringBuilder();
                sb.AppendLine("## NYAYA PATHA — Panel Advocate Litigation Portfolio");
                sb.AppendLine();
                sb.AppendLine("---");
                sb.AppendLine();
                sb.AppendLine($"### ⚖️ Caseload Profile: **Advocate {cleanAdv}**");
                sb.AppendLine();
                sb.AppendLine("| Litigation Module | Entrusted Cases | Active / Pending | Disposed / Concluded |");
                sb.AppendLine("|---|:---:|:---:|:---:|");
                sb.AppendLine($"| 🚗 **MVC / MACT Claims** | {mvcList.Count} | **{mvcPending}** | {mvcDisposed} |");
                sb.AppendLine($"| ⚖️ **Labour Disputes (KID/ID)** | {labourList.Count} | **{labourPending}** | {labourDisposed} |");
                sb.AppendLine($"| 📑 **Appeals (High Court MFA)** | {appList.Count} | **{appPending}** | {appDisposed} |");
                sb.AppendLine($"| 🟢 **Total Caseload Entrusted** | **{totalFound}** | **{totalActive}** | **{totalDisposed}** |");
                sb.AppendLine();

                if (mvcList.Count > 0)
                {
                    sb.AppendLine("#### 🚗 MVC / MACT Accident Claims Handled");
                    sb.AppendLine("| Case No | Court / Tribunal | Division | Current Stage | Next Hearing | Claim (Rs.) | Award (Rs.) |");
                    sb.AppendLine("|---|---|---|:---:|:---:|:---:|:---:|");
                    foreach (var c in mvcList)
                    {
                        string nextDt = c.NextHearingDate != null ? ((DateTime)c.NextHearingDate).ToString("dd-MM-yyyy") : "-";
                        string claimStr = c.ClaimAmount != null ? $"Rs. {((decimal)c.ClaimAmount):N0}" : "-";
                        string awardStr = c.AwardAmount != null && c.AwardAmount > 0 ? $"Rs. {((decimal)c.AwardAmount):N0}" : "-";
                        sb.AppendLine($"| **MVC/{c.MVCNo}/{c.MVCYear}** | {c.CourtName} | {c.DivisionName} | **{c.CurrentStage}** | {nextDt} | {claimStr} | {awardStr} |");
                    }
                    sb.AppendLine();
                }

                if (labourList.Count > 0)
                {
                    sb.AppendLine("#### ⚖️ Labour & Industrial Disputes Handled");
                    sb.AppendLine("| Case No | Labour Court | Division | Stage | Status | Next Hearing |");
                    sb.AppendLine("|---|---|---|:---:|:---:|:---:|");
                    foreach (var c in labourList)
                    {
                        string nextDt = c.NextHearingDate != null ? ((DateTime)c.NextHearingDate).ToString("dd-MM-yyyy") : "-";
                        sb.AppendLine($"| **{c.CaseType}/{c.CaseNumber}/{c.CaseYear}** | {c.CourtName} | {c.DivisionName} | **{c.CurrentStage}** | {c.CaseStatus} | {nextDt} |");
                    }
                    sb.AppendLine();
                }

                if (appList.Count > 0)
                {
                    sb.AppendLine("#### 📑 High Court MFA Appeals Handled");
                    sb.AppendLine("| Appeal No | Bench | Division | Stage | Status / Outcome | Next Hearing |");
                    sb.AppendLine("|---|---|---|:---:|:---:|:---:|");
                    foreach (var a in appList)
                    {
                        string nextDt = a.NextHearingDate != null ? ((DateTime)a.NextHearingDate).ToString("dd-MM-yyyy") : "-";
                        sb.AppendLine($"| **MFA/{a.MFANo}/{a.MFAYear}** | {a.CourtName} | {a.DivisionName} | **{a.CurrentStage}** | {a.Outcome ?? a.Status} | {nextDt} |");
                    }
                    sb.AppendLine();
                }

                sb.AppendLine("---");
                sb.AppendLine("*Source: NWKRTC Central Case Management Database (Direct SQL Query — 100% Real-Time & Verified, Zero External LLM Cost).*");

                var dossier = new CaseDossier
                {
                    CaseType = "AdvocatePortfolio",
                    CaseNumber = $"Advocate - {cleanAdv}",
                    CourtName = "NWKRTC Central Database",
                    CurrentStage = $"{totalActive} Active / {totalFound} Total",
                    Petitioner = cleanAdv,
                    Respondent = "NWKRTC"
                };
                dossier.StructuredFacts["DirectMarkdown"] = sb.ToString();
                dossier.StructuredFacts["Advocate Name"] = cleanAdv;
                dossier.StructuredFacts["Total Cases"] = totalFound.ToString();
                dossier.StructuredFacts["Active Cases"] = totalActive.ToString();
                return dossier;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resolving advocate portfolio for {AdvocateName}", advocateName);
                return null;
            }
        }

        private async Task<CaseDossier?> ResolveHearingCalendarAsync(LegalQueryPlan plan, int userDivisionId, CancellationToken cancellationToken)
        {
            DateTime startDate = plan.DateRangeStart ?? DateTime.Today;
            DateTime endDate = plan.DateRangeEnd ?? DateTime.Today.AddDays(7);
            int targetDivId = plan.DivisionId ?? (userDivisionId != 5 ? userDivisionId : 0);
            string rangeLabel = !string.IsNullOrWhiteSpace(plan.DateRangeLabel) 
                ? plan.DateRangeLabel 
                : $"{startDate:dd-MM-yyyy} to {endDate:dd-MM-yyyy}";

            try
            {
                using var conn = _db.GetConnection();
                await conn.OpenAsync(cancellationToken);

                string mvcSql = @"
                    SELECT TOP 40
                        'MVC' AS Module,
                        c.CaseID,
                        'MVC/' + c.MVCNo + '/' + CAST(c.MVCYear AS VARCHAR) AS CaseNumber,
                        ISNULL(m.MACTName, ISNULL(c.EstName, 'MACT Tribunal')) AS CourtName,
                        d.DivisionNameEnglish AS DivisionName,
                        ISNULL(c.CurrentStage, 'Hearing') AS Stage,
                        c.NextHearingDate,
                        COALESCE(c.AdvocateName, a.AdvocateName, 'Panel Advocate') AS Advocate,
                        ISNULL((SELECT TOP 1 PetitionerName FROM MVC_CASE_PETITIONERS WHERE CaseID = c.CaseID AND PetitionerName IS NOT NULL AND PetitionerName <> ''), 'Claimant') AS Petitioner
                    FROM MVC_CASES c
                    LEFT JOIN MACT_MASTER m ON c.MACTID = m.MACTID
                    LEFT JOIN DIVISION_MASTER d ON c.DivisionID = d.DivisionID
                    LEFT JOIN ADVOCATE_MASTER a ON c.AdvocateID = a.AdvocateID
                    WHERE c.NextHearingDate >= @StartDate AND c.NextHearingDate <= @EndDate
                      AND (@DivId = 0 OR c.DivisionID = @DivId)
                      AND (c.PendDispStatus = 'P' OR c.PendDispStatus IS NULL)
                    ORDER BY c.NextHearingDate ASC, c.CaseID ASC";

                var hearings = new List<dynamic>();
                using (var cmd = new SqlCommand(mvcSql, conn))
                {
                    cmd.Parameters.AddWithValue("@StartDate", startDate.Date);
                    cmd.Parameters.AddWithValue("@EndDate", endDate.Date);
                    cmd.Parameters.AddWithValue("@DivId", targetDivId);
                    using var rdr = await cmd.ExecuteReaderAsync(cancellationToken);
                    while (await rdr.ReadAsync(cancellationToken))
                    {
                        hearings.Add(new
                        {
                            Module = "MVC",
                            CaseId = rdr.GetInt32(1),
                            CaseNumber = rdr.GetString(2),
                            CourtName = rdr.GetString(3),
                            DivisionName = rdr.GetString(4),
                            Stage = rdr.GetString(5),
                            HearingDate = rdr.GetDateTime(6),
                            Advocate = rdr.GetString(7),
                            Petitioner = rdr.GetString(8)
                        });
                    }
                }

                // Labour hearings
                string labSql = @"
                    SELECT TOP 30
                        'LABOUR' AS Module,
                        c.CaseID,
                        c.CaseType + '/' + c.CaseNumber + '/' + CAST(c.CaseYear AS VARCHAR) AS CaseNumber,
                        ISNULL(lc.CourtName, 'Labour Court') AS CourtName,
                        dm.DivisionNameEnglish AS DivisionName,
                        ISNULL(c.CurrentStage, 'Hearing') AS Stage,
                        c.NextHearingDate,
                        COALESCE(c.AdvocateName, am.AdvocateName, 'Panel Advocate') AS Advocate,
                        ISNULL(c.EmployeeName, 'Workman') AS Petitioner
                    FROM LABOUR_CASES c
                    LEFT JOIN LABOUR_COURTS lc ON c.CourtID = lc.CourtID
                    LEFT JOIN DIVISION_MASTER dm ON c.DivisionID = dm.DivisionID
                    LEFT JOIN ADVOCATE_MASTER am ON c.AdvocateID = am.AdvocateID
                    WHERE c.NextHearingDate >= @StartDate AND c.NextHearingDate <= @EndDate
                      AND (@DivId = 0 OR c.DivisionID = @DivId)
                      AND (c.CaseStatus IS NULL OR c.CaseStatus <> 'DISPOSED')
                    ORDER BY c.NextHearingDate ASC, c.CaseID ASC";

                using (var cmd = new SqlCommand(labSql, conn))
                {
                    cmd.Parameters.AddWithValue("@StartDate", startDate.Date);
                    cmd.Parameters.AddWithValue("@EndDate", endDate.Date);
                    cmd.Parameters.AddWithValue("@DivId", targetDivId);
                    using var rdr = await cmd.ExecuteReaderAsync(cancellationToken);
                    while (await rdr.ReadAsync(cancellationToken))
                    {
                        hearings.Add(new
                        {
                            Module = "LABOUR",
                            CaseId = rdr.GetInt32(1),
                            CaseNumber = rdr.GetString(2),
                            CourtName = rdr.GetString(3),
                            DivisionName = rdr.GetString(4),
                            Stage = rdr.GetString(5),
                            HearingDate = rdr.GetDateTime(6),
                            Advocate = rdr.GetString(7),
                            Petitioner = rdr.GetString(8)
                        });
                    }
                }

                // Execution Petitions
                string epSql = @"
                    SELECT TOP 20
                        'EP' AS Module,
                        ep.EPID,
                        ISNULL(ep.EPNumber, 'EP/' + ep.ArisingFromMVCNo) AS CaseNumber,
                        ISNULL(ep.EPCourt, 'Executing Court') AS CourtName,
                        d.DivisionNameEnglish AS DivisionName,
                        ISNULL(ep.EPStatus, 'Pending') AS Stage,
                        ep.NextHearingDate,
                        ISNULL(ep.AdvocateName, 'Panel Advocate') AS Advocate,
                        'Decree Holder' AS Petitioner
                    FROM MVC_EP_DETAILS ep
                    LEFT JOIN DIVISION_MASTER d ON ep.DivisionID = d.DivisionID
                    WHERE ep.NextHearingDate >= @StartDate AND ep.NextHearingDate <= @EndDate
                      AND (@DivId = 0 OR ep.DivisionID = @DivId)
                      AND (ep.EPStatus IS NULL OR (ep.EPStatus <> 'Closed' AND ep.EPStatus <> 'Disposed'))
                    ORDER BY ep.NextHearingDate ASC, ep.EPID ASC";

                using (var cmd = new SqlCommand(epSql, conn))
                {
                    cmd.Parameters.AddWithValue("@StartDate", startDate.Date);
                    cmd.Parameters.AddWithValue("@EndDate", endDate.Date);
                    cmd.Parameters.AddWithValue("@DivId", targetDivId);
                    using var rdr = await cmd.ExecuteReaderAsync(cancellationToken);
                    while (await rdr.ReadAsync(cancellationToken))
                    {
                        hearings.Add(new
                        {
                            Module = "EP",
                            CaseId = rdr.GetInt32(1),
                            CaseNumber = rdr.GetString(2),
                            CourtName = rdr.GetString(3),
                            DivisionName = rdr.GetString(4),
                            Stage = rdr.GetString(5),
                            HearingDate = rdr.GetDateTime(6),
                            Advocate = rdr.GetString(7),
                            Petitioner = rdr.GetString(8)
                        });
                    }
                }

                var sb = new System.Text.StringBuilder();
                sb.AppendLine("## NYAYA PATHA — Daily Cause List & Hearing Calendar");
                sb.AppendLine();
                sb.AppendLine("---");
                sb.AppendLine();
                sb.AppendLine($"### 📅 Scheduled Hearings: **{rangeLabel}**");
                sb.AppendLine();

                if (hearings.Count == 0)
                {
                    sb.AppendLine($"*No court hearings are listed in the database between **{startDate:dd-MM-yyyy}** and **{endDate:dd-MM-yyyy}**.*");
                    sb.AppendLine();
                }
                else
                {
                    sb.AppendLine($"**Total Hearings Listed:** **{hearings.Count}** cases across MACT, Labour & Executing Courts.");
                    sb.AppendLine();

                    var grouped = hearings.OrderBy(h => (DateTime)h.HearingDate).GroupBy(h => ((DateTime)h.HearingDate).ToString("dd-MM-yyyy (dddd)"));
                    foreach (var g in grouped)
                    {
                        sb.AppendLine($"#### 🏛️ Hearings on {g.Key}");
                        sb.AppendLine("| Mod | Case Number | Court / Bench | Division | Claimant / Workman | Current Stage | Appearing Advocate |");
                        sb.AppendLine("|:---:|---|---|---|---|:---:|---|");
                        foreach (var h in g)
                        {
                            string icon = h.Module == "MVC" ? "🚗" : (h.Module == "LABOUR" ? "⚖️" : "⚡");
                            sb.AppendLine($"| {icon} | **{h.CaseNumber}** | {h.CourtName} | {h.DivisionName} | {h.Petitioner} | **{h.Stage}** | {h.Advocate} |");
                        }
                        sb.AppendLine();
                    }
                }

                sb.AppendLine("---");
                sb.AppendLine("*Source: NWKRTC Central Case Management Database (Direct SQL Query — 100% Real-Time & Verified, Zero External LLM Cost).*");

                var dossier = new CaseDossier
                {
                    CaseType = "HearingCalendar",
                    CaseNumber = $"Cause List - {rangeLabel}",
                    CourtName = "NWKRTC Central Database",
                    CurrentStage = $"{hearings.Count} Hearings Listed",
                    Petitioner = "Daily Cause List",
                    Respondent = "NWKRTC"
                };
                dossier.StructuredFacts["DirectMarkdown"] = sb.ToString();
                dossier.StructuredFacts["Hearing Count"] = hearings.Count.ToString();
                return dossier;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resolving hearing calendar");
                return null;
            }
        }

        private async Task<CaseDossier?> ResolveFinancialExposureAsync(decimal threshold, int? divisionId, int userDivisionId, CancellationToken cancellationToken)
        {
            int targetDivId = divisionId ?? (userDivisionId != 5 ? userDivisionId : 0);
            string thresholdDisplay = threshold >= 10000000m 
                ? $"Rs. {threshold / 10000000m:0.##} Crore" 
                : $"Rs. {threshold / 100000m:0.##} Lakh";

            try
            {
                using var conn = _db.GetConnection();
                await conn.OpenAsync(cancellationToken);

                string sql = @"
                    SELECT TOP 30
                        c.CaseID,
                        'MVC/' + c.MVCNo + '/' + CAST(c.MVCYear AS VARCHAR) AS CaseNumber,
                        c.MVCYear,
                        d.DivisionNameEnglish AS DivisionName,
                        ISNULL(m.MACTName, ISNULL(c.EstName, 'MACT Tribunal')) AS CourtName,
                        ISNULL((SELECT TOP 1 PetitionerName FROM MVC_CASE_PETITIONERS WHERE CaseID = c.CaseID AND PetitionerName IS NOT NULL AND PetitionerName <> ''), 'Claimant') AS Petitioner,
                        ISNULL(c.CurrentStage, 'Pending') AS CurrentStage,
                        c.ClaimAmount,
                        COALESCE(adv.AwardAmount, c.AwardAmount) AS AwardAmount,
                        c.NextHearingDate
                    FROM MVC_CASES c
                    LEFT JOIN DIVISION_MASTER d ON c.DivisionID = d.DivisionID
                    LEFT JOIN MACT_MASTER m ON c.MACTID = m.MACTID
                    LEFT JOIN MVC_CASE_ADVERSE_DETAILS adv ON c.CaseID = adv.CaseID
                    WHERE (@DivId = 0 OR c.DivisionID = @DivId)
                      AND (c.ClaimAmount >= @Threshold OR COALESCE(adv.AwardAmount, c.AwardAmount) >= @Threshold)
                    ORDER BY COALESCE(adv.AwardAmount, c.AwardAmount, c.ClaimAmount) DESC, c.CaseID DESC";

                var list = new List<dynamic>();
                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@DivId", targetDivId);
                    cmd.Parameters.AddWithValue("@Threshold", threshold);
                    using var rdr = await cmd.ExecuteReaderAsync(cancellationToken);
                    while (await rdr.ReadAsync(cancellationToken))
                    {
                        list.Add(new
                        {
                            CaseId = rdr.GetInt32(0),
                            CaseNumber = rdr.GetString(1),
                            MVCYear = rdr.GetInt32(2),
                            DivisionName = rdr.IsDBNull(3) ? "N/A" : rdr.GetString(3),
                            CourtName = rdr.GetString(4),
                            Petitioner = rdr.GetString(5),
                            CurrentStage = rdr.GetString(6),
                            ClaimAmount = rdr.IsDBNull(7) ? (decimal?)null : rdr.GetDecimal(7),
                            AwardAmount = rdr.IsDBNull(8) ? (decimal?)null : rdr.GetDecimal(8),
                            NextHearingDate = rdr.IsDBNull(9) ? (DateTime?)null : rdr.GetDateTime(9)
                        });
                    }
                }

                var sb = new System.Text.StringBuilder();
                sb.AppendLine("## NYAYA PATHA — High-Exposure Financial Liability Audit");
                sb.AppendLine();
                sb.AppendLine("---");
                sb.AppendLine();
                sb.AppendLine($"### 💰 High Financial Risk Claims: **Threshold >= {thresholdDisplay}**");
                sb.AppendLine();

                if (list.Count == 0)
                {
                    sb.AppendLine($"*No cases found exceeding the exposure threshold of **{thresholdDisplay}** in the selected jurisdiction.*");
                    sb.AppendLine();
                }
                else
                {
                    decimal totalClaimSum = list.Where(c => c.ClaimAmount != null).Sum(c => (decimal)c.ClaimAmount);
                    decimal totalAwardSum = list.Where(c => c.AwardAmount != null && c.AwardAmount > 0).Sum(c => (decimal)c.AwardAmount);

                    sb.AppendLine("| Risk Metric | Total Value | Status |");
                    sb.AppendLine("|---|:---:|:---:|");
                    sb.AppendLine($"| **Identified High-Risk Cases** | **{list.Count}** | Above {thresholdDisplay} |");
                    sb.AppendLine($"| **Combined Claim Exposure** | **Rs. {totalClaimSum:N0}** | Demanded Liability |");
                    sb.AppendLine($"| **Combined Award Exposure** | **Rs. {totalAwardSum:N0}** | Fastened by Tribunals |");
                    sb.AppendLine();

                    sb.AppendLine("#### 📋 Top Exposure Cases Portfolio");
                    sb.AppendLine("| Case Number | Division | MACT Tribunal | Claimant | Stage | Claim Amount | Award Amount | Next Hearing |");
                    sb.AppendLine("|---|---|---|---|:---:|:---:|:---:|:---:|");
                    foreach (var c in list)
                    {
                        string claimStr = c.ClaimAmount != null ? $"Rs. {((decimal)c.ClaimAmount):N0}" : "-";
                        string awardStr = c.AwardAmount != null && c.AwardAmount > 0 ? $"**Rs. {((decimal)c.AwardAmount):N0}**" : "-";
                        string nextDt = c.NextHearingDate != null ? ((DateTime)c.NextHearingDate).ToString("dd-MM-yyyy") : "-";
                        sb.AppendLine($"| **{c.CaseNumber}** | {c.DivisionName} | {c.CourtName} | {c.Petitioner} | **{c.CurrentStage}** | {claimStr} | {awardStr} | {nextDt} |");
                    }
                    sb.AppendLine();
                }

                sb.AppendLine("---");
                sb.AppendLine("*Source: NWKRTC Central Case Management Database (Direct SQL Query — 100% Real-Time & Verified, Zero External LLM Cost).*");

                var dossier = new CaseDossier
                {
                    CaseType = "FinancialExposure",
                    CaseNumber = $"Financial Exposure >= {thresholdDisplay}",
                    CourtName = "NWKRTC Central Database",
                    CurrentStage = $"{list.Count} High Risk Cases",
                    Petitioner = "Financial Audit",
                    Respondent = "NWKRTC"
                };
                dossier.StructuredFacts["DirectMarkdown"] = sb.ToString();
                return dossier;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resolving financial exposure");
                return null;
            }
        }

        private async Task<CaseDossier?> ResolveExecutionPetitionsAsync(int? divisionId, int userDivisionId, CancellationToken cancellationToken)
        {
            int targetDivId = divisionId ?? (userDivisionId != 5 ? userDivisionId : 0);

            try
            {
                using var conn = _db.GetConnection();
                await conn.OpenAsync(cancellationToken);

                string sql = @"
                    SELECT TOP 40
                        ep.EPID,
                        ep.CaseID,
                        ISNULL(ep.EPNumber, 'EP/' + ep.ArisingFromMVCNo) AS EPNumber,
                        ep.EPYear,
                        ISNULL(ep.EPCourt, 'Executing Court') AS CourtName,
                        ep.ArisingFromMVCNo,
                        ep.ArisingFromMVCYear,
                        ISNULL(ep.EPStatus, 'Pending') AS EPStatus,
                        d.DivisionNameEnglish AS DivisionName,
                        ep.AwardAmount,
                        ep.AmountPaid,
                        ep.NextHearingDate,
                        ISNULL(ep.Remarks, '') AS Remarks,
                        ISNULL(ep.VehicleNo, 'N/A') AS VehicleNo
                    FROM MVC_EP_DETAILS ep
                    LEFT JOIN DIVISION_MASTER d ON ep.DivisionID = d.DivisionID
                    WHERE (@DivId = 0 OR ep.DivisionID = @DivId)
                      AND (ep.EPStatus IS NULL OR (ep.EPStatus <> 'Closed' AND ep.EPStatus <> 'Disposed'))
                    ORDER BY 
                      CASE WHEN ep.Remarks LIKE '%attach%' OR ep.Remarks LIKE '%warrant%' THEN 0 ELSE 1 END,
                      ep.NextHearingDate ASC, ep.EPID DESC";

                var epList = new List<dynamic>();
                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@DivId", targetDivId);
                    using var rdr = await cmd.ExecuteReaderAsync(cancellationToken);
                    while (await rdr.ReadAsync(cancellationToken))
                    {
                        epList.Add(new
                        {
                            EPID = rdr.GetInt32(0),
                            CaseID = rdr.IsDBNull(1) ? 0 : rdr.GetInt32(1),
                            EPNumber = rdr.GetString(2),
                            EPYear = rdr.IsDBNull(3) ? 0 : rdr.GetInt32(3),
                            CourtName = rdr.GetString(4),
                            ArisingMVC = rdr.IsDBNull(5) ? "N/A" : rdr.GetString(5),
                            ArisingMVCYear = rdr.IsDBNull(6) ? 0 : rdr.GetInt32(6),
                            EPStatus = rdr.GetString(7),
                            DivisionName = rdr.IsDBNull(8) ? "N/A" : rdr.GetString(8),
                            AwardAmount = rdr.IsDBNull(9) ? (decimal?)null : rdr.GetDecimal(9),
                            AmountPaid = rdr.IsDBNull(10) ? (decimal?)null : rdr.GetDecimal(10),
                            NextHearingDate = rdr.IsDBNull(11) ? (DateTime?)null : rdr.GetDateTime(11),
                            Remarks = rdr.GetString(12),
                            VehicleNo = rdr.GetString(13)
                        });
                    }
                }

                var sb = new System.Text.StringBuilder();
                sb.AppendLine("## NYAYA PATHA — Execution Petitions (EP) & Coercive Recovery Monitor");
                sb.AppendLine();
                sb.AppendLine("---");
                sb.AppendLine();
                sb.AppendLine("### ⚡ Pending Execution Petitions & Bus Attachment Risk Register");
                sb.AppendLine();

                if (epList.Count == 0)
                {
                    sb.AppendLine("*No pending execution petitions requiring urgent compliance found in the selected jurisdiction.*");
                    sb.AppendLine();
                }
                else
                {
                    int attachmentRisks = epList.Count(e => ((string)e.Remarks).IndexOf("attach", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                                            ((string)e.Remarks).IndexOf("warrant", StringComparison.OrdinalIgnoreCase) >= 0);
                    decimal totalAward = epList.Where(e => e.AwardAmount != null).Sum(e => (decimal)e.AwardAmount);
                    decimal totalPaid = epList.Where(e => e.AmountPaid != null).Sum(e => (decimal)e.AmountPaid);

                    sb.AppendLine("| Recovery Risk Parameter | Status / Value | Action Required |");
                    sb.AppendLine("|---|:---:|:---:|");
                    sb.AppendLine($"| **Active Execution Petitions** | **{epList.Count}** | Pending in Executing Courts |");
                    sb.AppendLine($"| **Coercive / Attachment Warnings** | **{attachmentRisks}** | 🚨 **Urgent Legal Intervention** |");
                    sb.AppendLine($"| **Total Award Execution Demanded** | **Rs. {totalAward:N0}** | Decreed Liability |");
                    sb.AppendLine($"| **Total Disbursed / Paid** | **Rs. {totalPaid:N0}** | Deposit Towards Satisfaction |");
                    sb.AppendLine($"| **Outstanding Recovery Balance** | **Rs. {Math.Max(0, totalAward - totalPaid):N0}** | Immediate Action Needed |");
                    sb.AppendLine();

                    sb.AppendLine("#### 📋 Itemized Execution Petitions Register");
                    sb.AppendLine("| EP Number | Arising From | Vehicle No | Executing Court | Division | Status | Outstanding | Next Hearing | Risk Alert |");
                    sb.AppendLine("|---|---|---|---|---|:---:|:---:|:---:|:---:|");
                    foreach (var ep in epList)
                    {
                        decimal bal = Math.Max(0, (ep.AwardAmount ?? 0m) - (ep.AmountPaid ?? 0m));
                        string balStr = bal > 0 ? $"Rs. {bal:N0}" : "Satisfied";
                        string nextDt = ep.NextHearingDate != null ? ((DateTime)ep.NextHearingDate).ToString("dd-MM-yyyy") : "-";
                        bool isCoercive = ((string)ep.Remarks).IndexOf("attach", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                          ((string)ep.Remarks).IndexOf("warrant", StringComparison.OrdinalIgnoreCase) >= 0;
                        string alert = isCoercive ? "🚨 **ATTACHMENT RISK**" : "Normal";
                        sb.AppendLine($"| **{ep.EPNumber}** | MVC/{ep.ArisingMVC}/{ep.ArisingMVCYear} | {ep.VehicleNo} | {ep.CourtName} | {ep.DivisionName} | **{ep.EPStatus}** | {balStr} | {nextDt} | {alert} |");
                    }
                    sb.AppendLine();
                }

                sb.AppendLine("---");
                sb.AppendLine("*Source: NWKRTC Central Case Management Database (Direct SQL Query — 100% Real-Time & Verified, Zero External LLM Cost).*");

                var dossier = new CaseDossier
                {
                    CaseType = "ExecutionPetitions",
                    CaseNumber = "Execution Petitions Register",
                    CourtName = "Executing Courts / NWKRTC",
                    CurrentStage = $"{epList.Count} Active Execution Petitions",
                    Petitioner = "Decree Holders",
                    Respondent = "NWKRTC"
                };
                dossier.StructuredFacts["DirectMarkdown"] = sb.ToString();
                return dossier;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resolving execution petitions");
                return null;
            }
        }

        private async Task<CaseDossier?> ResolveCompoundFilterAsync(LegalQueryPlan plan, int userDivisionId, CancellationToken cancellationToken)
        {
            if (!string.IsNullOrWhiteSpace(plan.VehicleNumber))
                return await ResolveVehicleLitigationHistoryAsync(plan.VehicleNumber, userDivisionId, cancellationToken);

            if (!string.IsNullOrWhiteSpace(plan.AdvocateName))
                return await ResolveAdvocatePortfolioAsync(plan.AdvocateName, userDivisionId, cancellationToken);

            if (plan.AmountThreshold.HasValue)
                return await ResolveFinancialExposureAsync(plan.AmountThreshold.Value, plan.DivisionId, userDivisionId, cancellationToken);

            return await BuildDivisionalDossierAsync(plan.RawQuery, cancellationToken);
        }

        private async Task<CaseDossier?> BuildDivisionalDossierAsync(string query, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(query)) return null;
            string lower = query.ToLowerInvariant();

            bool isCountOrDivisionQuery = lower.Contains("division") || lower.Contains("divison") ||
                                          lower.Contains("how many") || lower.Contains("total cases") ||
                                          lower.Contains("pending cases") || lower.Contains("disposed cases") ||
                                          lower.Contains("case count") || lower.Contains("cases in") ||
                                          lower.Contains("cases of") || lower.Contains("cases under") ||
                                          lower.Contains("statistics") || lower.Contains("dharwad") ||
                                          lower.Contains("hubballi") || lower.Contains("belagavi") ||
                                          lower.Contains("bagalkot") || lower.Contains("gadag") ||
                                          lower.Contains("haveri") || lower.Contains("uttara kannada");

            if (!isCountOrDivisionQuery) return null;

            try
            {
                using var conn = _db.GetConnection();
                await conn.OpenAsync(cancellationToken);

                // Fetch all divisions
                var divisions = new List<(int Id, string Code, string Name)>();
                using (var divCmd = new SqlCommand("SELECT DivisionID, DivisionCode, DivisionNameEnglish FROM DIVISION_MASTER WHERE DivisionID NOT IN (0, 13) ORDER BY DivisionID", conn))
                using (var rdr = await divCmd.ExecuteReaderAsync(cancellationToken))
                {
                    while (await rdr.ReadAsync(cancellationToken))
                    {
                        divisions.Add((
                            rdr.GetInt32(0),
                            rdr.IsDBNull(1) ? "" : rdr.GetString(1),
                            rdr.IsDBNull(2) ? "" : rdr.GetString(2)
                        ));
                    }
                }

                // Match requested division(s)
                var matchedDivs = new List<(int Id, string Code, string Name)>();
                if (lower.Contains("dharwad") || Regex.IsMatch(query, @"\b(dwr|hdc)\b", RegexOptions.IgnoreCase))
                {
                    if (lower.Contains("rural") || Regex.IsMatch(query, @"\bdwr\b", RegexOptions.IgnoreCase))
                        matchedDivs.AddRange(divisions.Where(d => d.Id == 6));
                    else if (lower.Contains("city") || Regex.IsMatch(query, @"\bhdc\b", RegexOptions.IgnoreCase))
                        matchedDivs.AddRange(divisions.Where(d => d.Id == 9));
                    else
                        matchedDivs.AddRange(divisions.Where(d => d.Id == 6 || d.Id == 9));
                }
                else if (lower.Contains("hubballi") || lower.Contains("hubli") || Regex.IsMatch(query, @"\b(hbl)\b", RegexOptions.IgnoreCase))
                {
                    if (lower.Contains("rural") || Regex.IsMatch(query, @"\bhbl\b", RegexOptions.IgnoreCase))
                        matchedDivs.AddRange(divisions.Where(d => d.Id == 8));
                    else if (lower.Contains("city") || Regex.IsMatch(query, @"\bhdc\b", RegexOptions.IgnoreCase))
                        matchedDivs.AddRange(divisions.Where(d => d.Id == 9));
                    else
                        matchedDivs.AddRange(divisions.Where(d => d.Id == 8 || d.Id == 9));
                }
                else if (lower.Contains("belagavi") || lower.Contains("belgaum") || Regex.IsMatch(query, @"\bbgm\b", RegexOptions.IgnoreCase))
                {
                    matchedDivs.AddRange(divisions.Where(d => d.Id == 2));
                }
                else if (lower.Contains("bagalkot") || lower.Contains("bagalkote") || Regex.IsMatch(query, @"\bbgk\b", RegexOptions.IgnoreCase))
                {
                    matchedDivs.AddRange(divisions.Where(d => d.Id == 1));
                }
                else if (lower.Contains("chikkodi") || Regex.IsMatch(query, @"\bckd\b", RegexOptions.IgnoreCase))
                {
                    matchedDivs.AddRange(divisions.Where(d => d.Id == 4));
                }
                else if (lower.Contains("gadag") || Regex.IsMatch(query, @"\bgdg\b", RegexOptions.IgnoreCase))
                {
                    matchedDivs.AddRange(divisions.Where(d => d.Id == 7));
                }
                else if (lower.Contains("haveri") || Regex.IsMatch(query, @"\bhvr\b", RegexOptions.IgnoreCase))
                {
                    matchedDivs.AddRange(divisions.Where(d => d.Id == 10));
                }
                else if (lower.Contains("uttara kannada") || lower.Contains("karwar") || Regex.IsMatch(query, @"\bnkd\b", RegexOptions.IgnoreCase))
                {
                    matchedDivs.AddRange(divisions.Where(d => d.Id == 11));
                }
                else if (lower.Contains("regional workshop") || Regex.IsMatch(query, @"\brwh\b", RegexOptions.IgnoreCase))
                {
                    matchedDivs.AddRange(divisions.Where(d => d.Id == 12));
                }
                else if (lower.Contains("central office") || Regex.IsMatch(query, @"\bcoh\b", RegexOptions.IgnoreCase))
                {
                    matchedDivs.AddRange(divisions.Where(d => d.Id == 5));
                }
                else
                {
                    foreach (var d in divisions)
                    {
                        if (!string.IsNullOrWhiteSpace(d.Code) && Regex.IsMatch(query, $@"\b{Regex.Escape(d.Code)}\b", RegexOptions.IgnoreCase))
                            matchedDivs.Add(d);
                        else if (!string.IsNullOrWhiteSpace(d.Name) && lower.Contains(d.Name.ToLowerInvariant()))
                            matchedDivs.Add(d);
                    }
                }

                // If query is for all divisions across NWKRTC
                if (matchedDivs.Count == 0 && (lower.Contains("all division") || lower.Contains("all divisions") || lower.Contains("overall") || lower.Contains("corporation")))
                {
                    matchedDivs.AddRange(divisions.Where(d => d.Id != 5)); // All operating divisions
                }

                if (matchedDivs.Count == 0) return null;

                var dossier = new CaseDossier
                {
                    CaseType = "DivisionalStatistics",
                    CourtName = "NWKRTC Litigation Database",
                    CurrentStage = "Official Database Counts",
                    Petitioner = "Official System Records",
                    Respondent = "NWKRTC"
                };

                var sb = new System.Text.StringBuilder();
                string primaryTitle = matchedDivs.Count == 1 
                    ? $"{matchedDivs[0].Name} ({matchedDivs[0].Code})"
                    : string.Join(" & ", matchedDivs.Select(d => $"{d.Name} ({d.Code})"));

                dossier.CaseNumber = $"Divisional Register - {primaryTitle}";

                sb.AppendLine("## NYAYA PATHA — Internal Divisional Case Register");
                sb.AppendLine();
                sb.AppendLine("---");
                sb.AppendLine();
                sb.AppendLine($"### 📊 Official Statistics: **{primaryTitle}**");
                sb.AppendLine();

                int grandTotalCases = 0;
                int grandTotalPending = 0;
                int grandTotalDisposed = 0;

                string statsSql = @"
                    SELECT
                        (SELECT COUNT(*) FROM MVC_CASES WHERE DivisionID = @DivID) as MvcTotal,
                        (SELECT COUNT(*) FROM MVC_CASES WHERE DivisionID = @DivID AND (PendDispStatus = 'P' OR PendDispStatus IS NULL) AND (DisposalStatus IS NULL OR DisposalStatus <> 'DISPOSED')) as MvcPending,
                        (SELECT COUNT(*) FROM MVC_CASES WHERE DivisionID = @DivID AND (PendDispStatus = 'D' OR DisposalStatus = 'DISPOSED')) as MvcDisposed,

                        (SELECT COUNT(*) FROM LABOUR_CASES WHERE DivisionID = @DivID) as LabourTotal,
                        (SELECT COUNT(*) FROM LABOUR_CASES WHERE DivisionID = @DivID AND (CaseStatus IS NULL OR CaseStatus <> 'Disposed')) as LabourPending,
                        (SELECT COUNT(*) FROM LABOUR_CASES WHERE DivisionID = @DivID AND CaseStatus = 'Disposed') as LabourDisposed,

                        (SELECT COUNT(*) FROM GRA_CASES WHERE DivisionCode = @DivID) as GraTotal,
                        (SELECT COUNT(*) FROM GRA_CASES WHERE DivisionCode = @DivID AND (CaseStatus IS NULL OR CaseStatus <> 'Disposed')) as GraPending,
                        (SELECT COUNT(*) FROM GRA_CASES WHERE DivisionCode = @DivID AND CaseStatus = 'Disposed') as GraDisposed,

                        (SELECT COUNT(*) FROM MVC_EP_DETAILS WHERE DivisionID = @DivID) as MvcEpTotal,
                        (SELECT COUNT(*) FROM MVC_EP_DETAILS WHERE DivisionID = @DivID AND (EPStatus IS NULL OR (EPStatus <> 'Closed' AND EPStatus <> 'Disposed'))) as MvcEpPending,

                        (SELECT COUNT(*) FROM LABOUR_EP_DETAILS WHERE DivisionID = @DivID) as LabourEpTotal,
                        (SELECT COUNT(*) FROM LABOUR_EP_DETAILS WHERE DivisionID = @DivID AND (EPStatus IS NULL OR (EPStatus <> 'Closed' AND EPStatus <> 'Disposed'))) as LabourEpPending,

                        (SELECT COUNT(*) FROM APPEAL_DETAILS a JOIN MVC_CASES m ON a.CaseID = m.CaseID WHERE m.DivisionID = @DivID) as AppealTotal,
                        (SELECT COUNT(*) FROM APPEAL_DETAILS a JOIN MVC_CASES m ON a.CaseID = m.CaseID WHERE m.DivisionID = @DivID AND (a.CorpMFAStatus IS NULL OR a.CorpMFAStatus <> 'Disposed')) as AppealPending";

                foreach (var div in matchedDivs)
                {
                    int mvcTot = 0, mvcPend = 0, mvcDisp = 0;
                    int labTot = 0, labPend = 0, labDisp = 0;
                    int graTot = 0, graPend = 0, graDisp = 0;
                    int mvcEpTot = 0, mvcEpPend = 0;
                    int labEpTot = 0, labEpPend = 0;
                    int appTot = 0, appPend = 0;

                    using (var cmd = new SqlCommand(statsSql, conn))
                    {
                        cmd.Parameters.AddWithValue("@DivID", div.Id);
                        using var rdr = await cmd.ExecuteReaderAsync(cancellationToken);
                        if (await rdr.ReadAsync(cancellationToken))
                        {
                            mvcTot = rdr.IsDBNull(0) ? 0 : rdr.GetInt32(0);
                            mvcPend = rdr.IsDBNull(1) ? 0 : rdr.GetInt32(1);
                            mvcDisp = rdr.IsDBNull(2) ? 0 : rdr.GetInt32(2);

                            labTot = rdr.IsDBNull(3) ? 0 : rdr.GetInt32(3);
                            labPend = rdr.IsDBNull(4) ? 0 : rdr.GetInt32(4);
                            labDisp = rdr.IsDBNull(5) ? 0 : rdr.GetInt32(5);

                            graTot = rdr.IsDBNull(6) ? 0 : rdr.GetInt32(6);
                            graPend = rdr.IsDBNull(7) ? 0 : rdr.GetInt32(7);
                            graDisp = rdr.IsDBNull(8) ? 0 : rdr.GetInt32(8);

                            mvcEpTot = rdr.IsDBNull(9) ? 0 : rdr.GetInt32(9);
                            mvcEpPend = rdr.IsDBNull(10) ? 0 : rdr.GetInt32(10);

                            labEpTot = rdr.IsDBNull(11) ? 0 : rdr.GetInt32(11);
                            labEpPend = rdr.IsDBNull(12) ? 0 : rdr.GetInt32(12);

                            appTot = rdr.IsDBNull(13) ? 0 : rdr.GetInt32(13);
                            appPend = rdr.IsDBNull(14) ? 0 : rdr.GetInt32(14);
                        }
                    }

                    int divTot = mvcTot + labTot + graTot + mvcEpTot + labEpTot + appTot;
                    int divPend = mvcPend + labPend + graPend + mvcEpPend + labEpPend + appPend;
                    int divDisp = mvcDisp + labDisp + graDisp + (mvcEpTot - mvcEpPend) + (labEpTot - labEpPend) + (appTot - appPend);

                    grandTotalCases += divTot;
                    grandTotalPending += divPend;
                    grandTotalDisposed += divDisp;

                    sb.AppendLine($"#### 🏛️ **{div.Name} ({div.Code})** — Case Status Across All Modules");
                    sb.AppendLine("| Litigation Module | Total Registered | Pending Cases | Disposed / Concluded |");
                    sb.AppendLine("|---|:---:|:---:|:---:|");
                    sb.AppendLine($"| 🚗 **MVC / MACT Accident Claims** | {mvcTot} | **{mvcPend}** | {mvcDisp} |");
                    sb.AppendLine($"| ⚖️ **Labour & Industrial Disputes (KID/ID)** | {labTot} | **{labPend}** | {labDisp} |");
                    sb.AppendLine($"| 📜 **Gratuity (PG) Cases** | {graTot} | **{graPend}** | {graDisp} |");
                    sb.AppendLine($"| 📑 **Appeals (High Court MFA / SC)** | {appTot} | **{appPend}** | {appTot - appPend} |");
                    sb.AppendLine($"| ⚡ **Execution Petitions (MVC + Labour EP)** | {mvcEpTot + labEpTot} | **{mvcEpPend + labEpPend}** | {(mvcEpTot - mvcEpPend) + (labEpTot - labEpPend)} |");
                    sb.AppendLine($"| 🟢 **Subtotal: {div.Code}** | **{divTot}** | **{divPend}** | **{divDisp}** |");
                    sb.AppendLine();

                    // Detect user interest in detailed pending case list
                    bool isMvcWanted = lower.Contains("mvc") || lower.Contains("mact") || lower.Contains("accident");
                    bool isLabourWanted = lower.Contains("labour") || lower.Contains("kid") || lower.Contains("industrial");
                    bool isGraWanted = lower.Contains("gratuity") || lower.Contains("gra") || lower.Contains("pg");
                    bool isMfaWanted = lower.Contains("mfa") || lower.Contains("appeal") || lower.Contains("high court") || lower.Contains("hc ");
                    bool isEpWanted = lower.Contains("execution") || System.Text.RegularExpressions.Regex.IsMatch(lower, @"\b(ep|eps)\b");
                    bool isPendingOrListRequested = lower.Contains("pending") || lower.Contains("case") || lower.Contains("list") || lower.Contains("show") || lower.Contains("want") || lower.Contains("give") || lower.Contains("all");
                    bool isSpecificModuleRequested = isMvcWanted || isLabourWanted || isGraWanted || isMfaWanted || isEpWanted;

                    // 1. Detailed MVC Pending Cases List
                    if ((isMvcWanted || (!isSpecificModuleRequested && isPendingOrListRequested)) && mvcPend > 0)
                    {
                        string mvcListSql = @"
                            SELECT TOP 30
                                c.CaseID,
                                c.MVCNo,
                                c.MVCYear,
                                ISNULL(c.VehicleNo, 'N/A') AS VehicleNo,
                                ISNULL((SELECT TOP 1 PetitionerName FROM MVC_CASE_PETITIONERS WHERE CaseID = c.CaseID AND PetitionerName IS NOT NULL AND PetitionerName <> ''), 'Claimant') AS PetitionerName,
                                ISNULL(m.MACTName, ISNULL(c.EstName, 'MACT Tribunal')) AS CourtName,
                                ISNULL(c.CurrentStage, 'Pending') AS CurrentStage,
                                c.NextHearingDate,
                                c.ClaimAmount
                            FROM MVC_CASES c
                            LEFT JOIN MACT_MASTER m ON c.MACTID = m.MACTID
                            WHERE c.DivisionID = @DivID
                              AND (c.PendDispStatus = 'P' OR c.PendDispStatus IS NULL)
                              AND (c.DisposalStatus IS NULL OR c.DisposalStatus <> 'DISPOSED')
                            ORDER BY 
                              CASE WHEN c.NextHearingDate IS NOT NULL AND c.NextHearingDate >= CAST(GETDATE() AS DATE) THEN 0 ELSE 1 END,
                              c.NextHearingDate ASC,
                              c.MVCYear DESC,
                              c.CaseID DESC";

                        try
                        {
                            using var mvcCmd = new SqlCommand(mvcListSql, conn);
                            mvcCmd.Parameters.AddWithValue("@DivID", div.Id);
                            using var mvcRdr = await mvcCmd.ExecuteReaderAsync(cancellationToken);
                            var mvcRows = new List<(int CaseId, string MvcNo, int Year, string Vehicle, string Petitioner, string Court, string Stage, DateTime? HearingDate, decimal? Claim)>();
                            while (await mvcRdr.ReadAsync(cancellationToken))
                            {
                                mvcRows.Add((
                                    mvcRdr.GetInt32(0),
                                    mvcRdr.IsDBNull(1) ? "" : mvcRdr.GetString(1),
                                    mvcRdr.IsDBNull(2) ? 0 : mvcRdr.GetInt32(2),
                                    mvcRdr.IsDBNull(3) ? "N/A" : mvcRdr.GetString(3),
                                    mvcRdr.IsDBNull(4) ? "Claimant" : mvcRdr.GetString(4),
                                    mvcRdr.IsDBNull(5) ? "MACT Tribunal" : mvcRdr.GetString(5),
                                    mvcRdr.IsDBNull(6) ? "Pending" : mvcRdr.GetString(6),
                                    mvcRdr.IsDBNull(7) ? null : mvcRdr.GetDateTime(7),
                                    mvcRdr.IsDBNull(8) ? null : mvcRdr.GetDecimal(8)
                                ));
                            }

                            if (mvcRows.Count > 0)
                            {
                                sb.AppendLine();
                                sb.AppendLine($"### 📋 **Active Pending MVC / MACT Claims ({div.Name} — {div.Code})**");
                                sb.AppendLine($"> Showing **{mvcRows.Count}** pending cases in register (Total pending in division: **{mvcPend}**):");
                                sb.AppendLine();
                                sb.AppendLine("| # | Case Number | Vehicle No | Claimant / Petitioner | Court / Tribunal | Stage | Next Hearing | Claim (₹) |");
                                sb.AppendLine("|:---:|---|---|---|---|---|:---:|:---:|");
                                int idx = 1;
                                foreach (var r in mvcRows)
                                {
                                    string hearingStr = r.HearingDate.HasValue ? r.HearingDate.Value.ToString("dd-MMM-yyyy") : "Not fixed";
                                    string claimStr = r.Claim.HasValue ? $"{r.Claim.Value:N0}" : "—";
                                    sb.AppendLine($"| {idx++} | **MVC/{r.MvcNo}/{r.Year}** | `{r.Vehicle}` | {r.Petitioner} | {r.Court} | <span class=\"badge bg-info-subtle text-info border\">{r.Stage}</span> | {hearingStr} | {claimStr} |");
                                }
                                sb.AppendLine();
                                sb.AppendLine($"> 💡 *Tip: Ask `\"Analyze MVC/{mvcRows[0].MvcNo}/{mvcRows[0].Year}\"` to inspect detailed facts, TR-18 remarks, and e-Courts history.*");
                                sb.AppendLine();
                            }
                        }
                        catch (Exception exMvc)
                        {
                            _logger.LogWarning(exMvc, "Error fetching detailed MVC pending list for division {DivId}", div.Id);
                        }
                    }

                    // 2. Detailed Labour Pending Cases List
                    if ((isLabourWanted || (!isSpecificModuleRequested && isPendingOrListRequested && matchedDivs.Count == 1)) && labPend > 0)
                    {
                        string labListSql = @"
                            SELECT TOP 25
                                c.CaseID,
                                ISNULL(c.CaseType, 'KID') AS CaseType,
                                c.CaseNumber,
                                c.CaseYear,
                                ISNULL(c.PetitionerName, 'Workman') AS PetitionerName,
                                ISNULL(c.CourtName, 'Labour Court') AS CourtName,
                                ISNULL(c.CurrentStage, 'Pending') AS CurrentStage,
                                c.NextHearingDate
                            FROM LABOUR_CASES c
                            WHERE c.DivisionID = @DivID
                              AND (c.CaseStatus IS NULL OR c.CaseStatus <> 'Disposed')
                            ORDER BY 
                              CASE WHEN c.NextHearingDate IS NOT NULL AND c.NextHearingDate >= CAST(GETDATE() AS DATE) THEN 0 ELSE 1 END,
                              c.NextHearingDate ASC,
                              c.CaseYear DESC,
                              c.CaseID DESC";

                        try
                        {
                            using var labCmd = new SqlCommand(labListSql, conn);
                            labCmd.Parameters.AddWithValue("@DivID", div.Id);
                            using var labRdr = await labCmd.ExecuteReaderAsync(cancellationToken);
                            var labRows = new List<(int CaseId, string CaseType, string CaseNumber, int? Year, string Petitioner, string Court, string Stage, DateTime? HearingDate)>();
                            while (await labRdr.ReadAsync(cancellationToken))
                            {
                                labRows.Add((
                                    labRdr.GetInt32(0),
                                    labRdr.IsDBNull(1) ? "KID" : labRdr.GetString(1),
                                    labRdr.IsDBNull(2) ? "" : labRdr.GetString(2),
                                    labRdr.IsDBNull(3) ? (int?)null : labRdr.GetInt32(3),
                                    labRdr.IsDBNull(4) ? "Workman" : labRdr.GetString(4),
                                    labRdr.IsDBNull(5) ? "Labour Court" : labRdr.GetString(5),
                                    labRdr.IsDBNull(6) ? "Pending" : labRdr.GetString(6),
                                    labRdr.IsDBNull(7) ? (DateTime?)null : labRdr.GetDateTime(7)
                                ));
                            }

                            if (labRows.Count > 0)
                            {
                                sb.AppendLine();
                                sb.AppendLine($"### ⚖️ **Active Pending Labour Disputes ({div.Name} — {div.Code})**");
                                sb.AppendLine($"> Showing **{labRows.Count}** dispute cases in register (Total pending in division: **{labPend}**):");
                                sb.AppendLine();
                                sb.AppendLine("| # | Case Number | Workman / Petitioner | Labour Court / Tribunal | Stage | Next Hearing |");
                                sb.AppendLine("|:---:|---|---|---|---|:---:|");
                                int idx = 1;
                                foreach (var r in labRows)
                                {
                                    string hearingStr = r.HearingDate.HasValue ? r.HearingDate.Value.ToString("dd-MMM-yyyy") : "Not fixed";
                                    string fullNo = !string.IsNullOrWhiteSpace(r.CaseNumber) 
                                        ? (r.CaseNumber.Contains("/") ? r.CaseNumber : $"{r.CaseType}/{r.CaseNumber}/{(r.Year?.ToString() ?? "")}".TrimEnd('/'))
                                        : $"{r.CaseType} #{r.CaseId}";
                                    sb.AppendLine($"| {idx++} | **{fullNo}** | {r.Petitioner} | {r.Court} | <span class=\"badge bg-warning-subtle text-warning border\">{r.Stage}</span> | {hearingStr} |");
                                }
                                sb.AppendLine();
                            }
                        }
                        catch (Exception exLab)
                        {
                            _logger.LogWarning(exLab, "Error fetching detailed Labour pending list for division {DivId}", div.Id);
                        }
                    }

                    // 3. Detailed Gratuity Pending Cases List
                    if (isGraWanted && graPend > 0)
                    {
                        string graListSql = @"
                            SELECT TOP 25
                                CaseID,
                                ISNULL(PGANumber, '') AS PGANumber,
                                ISNULL(ClaimantName, 'Applicant') AS ClaimantName,
                                ISNULL(ControllingAuthority, 'Controlling Authority') AS ControllingAuthority,
                                ISNULL(CurrentStage, 'Pending') AS CurrentStage,
                                NextHearingDate
                            FROM GRA_CASES
                            WHERE DivisionCode = @DivID
                              AND (CaseStatus IS NULL OR CaseStatus <> 'Disposed')
                            ORDER BY NextHearingDate ASC, CreatedDate DESC";

                        try
                        {
                            using var graCmd = new SqlCommand(graListSql, conn);
                            graCmd.Parameters.AddWithValue("@DivID", div.Id);
                            using var graRdr = await graCmd.ExecuteReaderAsync(cancellationToken);
                            var graRows = new List<(int CaseId, string PgaNo, string Claimant, string Authority, string Stage, DateTime? HearingDate)>();
                            while (await graRdr.ReadAsync(cancellationToken))
                            {
                                graRows.Add((
                                    graRdr.GetInt32(0),
                                    graRdr.IsDBNull(1) ? "" : graRdr.GetString(1),
                                    graRdr.IsDBNull(2) ? "Applicant" : graRdr.GetString(2),
                                    graRdr.IsDBNull(3) ? "Controlling Authority" : graRdr.GetString(3),
                                    graRdr.IsDBNull(4) ? "Pending" : graRdr.GetString(4),
                                    graRdr.IsDBNull(5) ? (DateTime?)null : graRdr.GetDateTime(5)
                                ));
                            }

                            if (graRows.Count > 0)
                            {
                                sb.AppendLine();
                                sb.AppendLine($"### 📜 **Active Pending Gratuity (PG) Cases ({div.Name} — {div.Code})**");
                                sb.AppendLine($"> Showing **{graRows.Count}** gratuity cases in register (Total pending in division: **{graPend}**):");
                                sb.AppendLine();
                                sb.AppendLine("| # | PGA Number | Claimant Name | Controlling Authority | Stage | Next Hearing |");
                                sb.AppendLine("|:---:|---|---|---|---|:---:|");
                                int idx = 1;
                                foreach (var r in graRows)
                                {
                                    string hearingStr = r.HearingDate.HasValue ? r.HearingDate.Value.ToString("dd-MMM-yyyy") : "Not fixed";
                                    sb.AppendLine($"| {idx++} | **PGA/{r.PgaNo}** | {r.Claimant} | {r.Authority} | {r.Stage} | {hearingStr} |");
                                }
                                sb.AppendLine();
                            }
                        }
                        catch (Exception exGra)
                        {
                            _logger.LogWarning(exGra, "Error fetching detailed Gratuity pending list for division {DivId}", div.Id);
                        }
                    }

                    // 4. Detailed Appeal / High Court MFA Cases List
                    if (isMfaWanted && appTot > 0)
                    {
                        string appListSql = @"
                            SELECT TOP 50
                                a.CaseID,
                                m.MVCNo,
                                m.MVCYear,
                                ISNULL(m.VehicleNo, 'N/A') AS VehicleNo,
                                ISNULL(a.CorpMFANumber, ISNULL(a.ClaimantMFANumber, '')) AS MfaNo,
                                ISNULL(a.CorpMFAYear, a.ClaimantMFAYear) AS MfaYear,
                                ISNULL(a.HighCourtBench, 'High Court Bench') AS Bench,
                                ISNULL(a.CorpMFAAdvocate, ISNULL(a.ClaimantMFAAdvocate, 'Panel Counsel')) AS Advocate,
                                ISNULL(a.CorpMFAStatus, ISNULL(a.ClaimantMFAStatus, 'Pending')) AS AppealStatus,
                                CASE WHEN a.StayGranted = 1 THEN 'Yes' ELSE 'No' END AS StayGranted,
                                ISNULL(a.CorpMFAOutcome, ISNULL(a.ClaimantMFADecision, '')) AS Outcome,
                                ISNULL((SELECT TOP 1 PetitionerName FROM MVC_CASE_PETITIONERS WHERE CaseID = m.CaseID AND PetitionerName IS NOT NULL AND PetitionerName <> ''), 'Claimant') AS PetitionerName
                            FROM APPEAL_DETAILS a
                            JOIN MVC_CASES m ON a.CaseID = m.CaseID
                            WHERE m.DivisionID = @DivID
                            ORDER BY 
                                CASE WHEN a.CorpMFAYear IS NOT NULL THEN a.CorpMFAYear ELSE a.ClaimantMFAYear END DESC,
                                a.CaseID DESC";

                        try
                        {
                            using var appCmd = new SqlCommand(appListSql, conn);
                            appCmd.Parameters.AddWithValue("@DivID", div.Id);
                            using var appRdr = await appCmd.ExecuteReaderAsync(cancellationToken);
                            var appRows = new List<(int CaseId, string MvcNo, int Year, string Vehicle, string MfaNo, int? MfaYear, string Bench, string Advocate, string Status, string Stay, string Outcome, string Petitioner)>();
                            while (await appRdr.ReadAsync(cancellationToken))
                            {
                                appRows.Add((
                                    appRdr.GetInt32(0),
                                    appRdr.IsDBNull(1) ? "" : appRdr.GetString(1),
                                    appRdr.IsDBNull(2) ? 0 : appRdr.GetInt32(2),
                                    appRdr.IsDBNull(3) ? "N/A" : appRdr.GetString(3),
                                    appRdr.IsDBNull(4) ? "" : appRdr.GetString(4),
                                    appRdr.IsDBNull(5) ? (int?)null : appRdr.GetInt32(5),
                                    appRdr.IsDBNull(6) ? "High Court" : appRdr.GetString(6),
                                    appRdr.IsDBNull(7) ? "Panel Counsel" : appRdr.GetString(7),
                                    appRdr.IsDBNull(8) ? "Pending" : appRdr.GetString(8),
                                    appRdr.IsDBNull(9) ? "No" : appRdr.GetString(9),
                                    appRdr.IsDBNull(10) ? "" : appRdr.GetString(10),
                                    appRdr.IsDBNull(11) ? "Claimant" : appRdr.GetString(11)
                                ));
                            }

                            if (appRows.Count > 0)
                            {
                                sb.AppendLine();
                                sb.AppendLine($"### 📑 **High Court MFA & Appeal Cases ({div.Name} — {div.Code})**");
                                sb.AppendLine($"> Showing **{appRows.Count}** appeal cases in register (Total appeals in division: **{appTot}**, Pending: **{appPend}**):");
                                sb.AppendLine();
                                sb.AppendLine("| # | High Court Appeal No | HC Bench | Original Case | Vehicle No | Claimant / Respondent | High Court Advocate | Status | Stay Granted |");
                                sb.AppendLine("|:---:|---|---|---|---|---|---|:---:|:---:|");
                                int idx = 1;
                                foreach (var r in appRows)
                                {
                                    string mfaDisplay = !string.IsNullOrWhiteSpace(r.MfaNo) 
                                        ? (r.MfaYear.HasValue ? $"**MFA/{r.MfaNo}/{r.MfaYear}**" : $"**MFA/{r.MfaNo}**")
                                        : "*Pending Filing / Entrustment*";
                                    string stayBadge = r.Stay.Equals("Yes", StringComparison.OrdinalIgnoreCase)
                                        ? "<span class=\"badge bg-success-subtle text-success border\">Yes</span>"
                                        : "<span class=\"badge bg-secondary-subtle text-secondary border\">No</span>";
                                    sb.AppendLine($"| {idx++} | {mfaDisplay} | {r.Bench} | MVC/{r.MvcNo}/{r.Year} | `{r.Vehicle}` | {r.Petitioner} | {r.Advocate} | <span class=\"badge bg-primary-subtle text-primary border\">{r.Status}</span> | {stayBadge} |");
                                }
                                sb.AppendLine();
                                sb.AppendLine($"> 💡 *Tip: Ask `\"Analyze MVC/{appRows[0].MvcNo}/{appRows[0].Year}\"` to inspect trial court records and connected MFA proceedings.*");
                                sb.AppendLine();
                            }
                        }
                        catch (Exception exApp)
                        {
                            _logger.LogWarning(exApp, "Error fetching detailed Appeal list for division {DivId}", div.Id);
                        }
                    }

                    // 5. Detailed Execution Petitions (EP) List
                    if (isEpWanted && (mvcEpTot + labEpTot) > 0)
                    {
                        string epListSql = @"
                            SELECT TOP 50
                                ep.EPID,
                                ep.CaseID,
                                m.MVCNo,
                                m.MVCYear,
                                ISNULL(m.VehicleNo, 'N/A') AS VehicleNo,
                                ISNULL(ep.EPNumber, '') AS EPNumber,
                                ep.EPYear,
                                ISNULL(ep.EPStatus, 'Pending') AS EPStatus,
                                ISNULL(ep.AttachmentWarrant, 'No') AS AttachmentWarrant,
                                ep.NextHearingDate,
                                ep.ClaimAmount
                            FROM MVC_EP_DETAILS ep
                            JOIN MVC_CASES m ON ep.CaseID = m.CaseID
                            WHERE ep.DivisionID = @DivID
                            ORDER BY ep.NextHearingDate ASC, ep.EPID DESC";

                        try
                        {
                            using var epCmd = new SqlCommand(epListSql, conn);
                            epCmd.Parameters.AddWithValue("@DivID", div.Id);
                            using var epRdr = await epCmd.ExecuteReaderAsync(cancellationToken);
                            var epRows = new List<(int EpId, int CaseId, string MvcNo, int Year, string Vehicle, string EpNo, int? EpYear, string Status, string Warrant, DateTime? HearingDate, decimal? Claim)>();
                            while (await epRdr.ReadAsync(cancellationToken))
                            {
                                epRows.Add((
                                    epRdr.GetInt32(0),
                                    epRdr.GetInt32(1),
                                    epRdr.IsDBNull(2) ? "" : epRdr.GetString(2),
                                    epRdr.IsDBNull(3) ? 0 : epRdr.GetInt32(3),
                                    epRdr.IsDBNull(4) ? "N/A" : epRdr.GetString(4),
                                    epRdr.IsDBNull(5) ? "" : epRdr.GetString(5),
                                    epRdr.IsDBNull(6) ? (int?)null : epRdr.GetInt32(6),
                                    epRdr.IsDBNull(7) ? "Pending" : epRdr.GetString(7),
                                    epRdr.IsDBNull(8) ? "No" : epRdr.GetString(8),
                                    epRdr.IsDBNull(9) ? (DateTime?)null : epRdr.GetDateTime(9),
                                    epRdr.IsDBNull(10) ? (decimal?)null : epRdr.GetDecimal(10)
                                ));
                            }

                            if (epRows.Count > 0)
                            {
                                sb.AppendLine();
                                sb.AppendLine($"### ⚡ **Active Execution Petitions (EP) ({div.Name} — {div.Code})**");
                                sb.AppendLine($"> Showing **{epRows.Count}** execution petitions in register (Total EP in division: **{mvcEpTot + labEpTot}**):");
                                sb.AppendLine();
                                sb.AppendLine("| # | EP Number | Original Case | Vehicle No | Status | Attachment Warrant | Next Hearing | Claim (₹) |");
                                sb.AppendLine("|:---:|---|---|---|:---:|:---:|:---:|:---:|");
                                int idx = 1;
                                foreach (var r in epRows)
                                {
                                    string epDisplay = !string.IsNullOrWhiteSpace(r.EpNo)
                                        ? (r.EpYear.HasValue ? $"**EP/{r.EpNo}/{r.EpYear}**" : $"**EP/{r.EpNo}**")
                                        : $"EP #{r.EpId}";
                                    string hearingStr = r.HearingDate.HasValue ? r.HearingDate.Value.ToString("dd-MMM-yyyy") : "Not fixed";
                                    string claimStr = r.Claim.HasValue ? $"{r.Claim.Value:N0}" : "—";
                                    string warrantBadge = r.Warrant.Equals("Yes", StringComparison.OrdinalIgnoreCase)
                                        ? "<span class=\"badge bg-danger text-white border\">YES</span>"
                                        : "<span class=\"badge bg-secondary-subtle text-secondary border\">No</span>";
                                    sb.AppendLine($"| {idx++} | {epDisplay} | MVC/{r.MvcNo}/{r.Year} | `{r.Vehicle}` | <span class=\"badge bg-warning-subtle text-warning border\">{r.Status}</span> | {warrantBadge} | {hearingStr} | {claimStr} |");
                                }
                                sb.AppendLine();
                            }
                        }
                        catch (Exception exEp)
                        {
                            _logger.LogWarning(exEp, "Error fetching detailed EP list for division {DivId}", div.Id);
                        }
                    }
                }

                if (matchedDivs.Count > 1)
                {
                    sb.AppendLine("---");
                    sb.AppendLine();
                    sb.AppendLine($"### 📈 **Grand Total Summary for {primaryTitle}**");
                    sb.AppendLine("| Metric | Grand Total Across Modules |");
                    sb.AppendLine("|---|:---:|");
                    sb.AppendLine($"| 📌 **Total Pending Cases** | **{grandTotalPending}** |");
                    sb.AppendLine($"| 📁 **Total Registered Cases** | **{grandTotalCases}** |");
                    sb.AppendLine($"| ✅ **Total Disposed Cases** | **{grandTotalDisposed}** |");
                    sb.AppendLine();
                }

                sb.AppendLine("---");
                sb.AppendLine("*Source: NWKRTC Central Case Management Database (Direct SQL Query — 100% Real-Time & Verified, Zero External LLM Cost).*");

                dossier.StructuredFacts["Division Name"] = primaryTitle;
                dossier.StructuredFacts["Total Cases Registered"] = grandTotalCases.ToString();
                dossier.StructuredFacts["Pending Cases"] = grandTotalPending.ToString();
                dossier.StructuredFacts["Disposed Cases"] = grandTotalDisposed.ToString();
                dossier.StructuredFacts["DirectMarkdown"] = sb.ToString();

                return dossier;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to build divisional statistics dossier");
                return null;
            }
        }

        public async Task<List<CaseSearchItemDto>> SearchCasesAsync(string query, int maxResults = 15, CancellationToken cancellationToken = default)
        {
            var results = new List<CaseSearchItemDto>();
            if (string.IsNullOrWhiteSpace(query))
                return results;

            string clean = query.Trim();
            int top = Math.Min(30, Math.Max(1, maxResults));

            try
            {
                using var conn = _db.GetConnection();
                await conn.OpenAsync(cancellationToken);

                // 1. Search MVC
                string mvcSql = @"
                    SELECT TOP (@Top) CaseID, MVCNo, MVCYear, ISNULL(VehicleNo, '') AS VehicleNo,
                           ISNULL(PetitionerName, '') AS Petitioner, ISNULL(CurrentStage, '') AS Stage
                    FROM MVC_CASES
                    WHERE MVCNo LIKE @Q OR VehicleNo LIKE @Q OR PetitionerName LIKE @Q OR CNRNumber LIKE @Q
                    ORDER BY MVCYear DESC, CaseID DESC";

                using (var cmd = new SqlCommand(mvcSql, conn))
                {
                    cmd.Parameters.AddWithValue("@Top", top);
                    cmd.Parameters.AddWithValue("@Q", $"%{clean}%");
                    using var rdr = await cmd.ExecuteReaderAsync(cancellationToken);
                    while (await rdr.ReadAsync(cancellationToken))
                    {
                        results.Add(new CaseSearchItemDto
                        {
                            CaseId = rdr.GetInt32(rdr.GetOrdinal("CaseID")),
                            CaseType = "MVC",
                            CaseNumber = $"MVC/{rdr["MVCNo"]}/{rdr["MVCYear"]}",
                            Summary = $"Vehicle: {rdr["VehicleNo"]} | Claimant: {rdr["Petitioner"]} | Stage: {rdr["Stage"]}"
                        });
                    }
                }

                // 2. Search Labour
                string labourSql = @"
                    SELECT TOP (@Top) CaseID, CaseType, CaseNumber, CaseYear,
                           ISNULL(EmployeeName, '') AS Employee, ISNULL(CurrentStage, '') AS Stage
                    FROM LABOUR_CASES
                    WHERE CaseNumber LIKE @Q OR EmployeeName LIKE @Q OR CNRNumber LIKE @Q
                    ORDER BY CaseYear DESC, CaseID DESC";

                using (var cmd = new SqlCommand(labourSql, conn))
                {
                    cmd.Parameters.AddWithValue("@Top", top);
                    cmd.Parameters.AddWithValue("@Q", $"%{clean}%");
                    using var rdr = await cmd.ExecuteReaderAsync(cancellationToken);
                    while (await rdr.ReadAsync(cancellationToken))
                    {
                        results.Add(new CaseSearchItemDto
                        {
                            CaseId = rdr.GetInt32(rdr.GetOrdinal("CaseID")),
                            CaseType = "LABOUR",
                            CaseNumber = $"{rdr["CaseType"]}/{rdr["CaseNumber"]}/{rdr["CaseYear"]}",
                            Summary = $"Employee: {rdr["Employee"]} | Stage: {rdr["Stage"]}"
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching cases with query: {Query}", query);
            }

            return results;
        }
    }
}
