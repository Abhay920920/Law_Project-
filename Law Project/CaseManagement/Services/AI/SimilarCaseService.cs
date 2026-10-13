using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MVCCaseManagement.DAL;
using MVCCaseManagement.Models;
using MVCCaseManagement.Models.AI;

namespace MVCCaseManagement.Services.AI
{
    public class SimilarCaseService : ISimilarCaseService
    {
        private readonly ILegalSearchService _internalSearch;
        private readonly ILegalWebSearchService _webSearch;
        private readonly ICaseRepository _caseRepo;
        private readonly ILabourRepository _labourRepo;
        private readonly IAppealRepository _appealRepo;
        private readonly IJudgementRepository _judgementRepo;
        private readonly ILogger<SimilarCaseService> _logger;

        public SimilarCaseService(
            ILegalSearchService internalSearch,
            ILegalWebSearchService webSearch,
            ICaseRepository caseRepo,
            ILabourRepository labourRepo,
            IAppealRepository appealRepo,
            IJudgementRepository judgementRepo,
            ILogger<SimilarCaseService> logger)
        {
            _internalSearch = internalSearch ?? throw new ArgumentNullException(nameof(internalSearch));
            _webSearch = webSearch ?? throw new ArgumentNullException(nameof(webSearch));
            _caseRepo = caseRepo ?? throw new ArgumentNullException(nameof(caseRepo));
            _labourRepo = labourRepo ?? throw new ArgumentNullException(nameof(labourRepo));
            _appealRepo = appealRepo ?? throw new ArgumentNullException(nameof(appealRepo));
            _judgementRepo = judgementRepo ?? throw new ArgumentNullException(nameof(judgementRepo));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<List<UnifiedSimilarCaseDto>> FindSimilarCasesAsync(
            string caseType,
            int caseId,
            string? queryOrContext = null,
            int maxResults = 6,
            CancellationToken cancellationToken = default)
        {
            var results = new List<UnifiedSimilarCaseDto>();
            string upperType = (caseType ?? "MVC").ToUpperInvariant();

            // Extract multi-dimensional comparison attributes
            string vehicleType = string.Empty;
            string courtName = string.Empty;
            string currentStage = string.Empty;
            string mannerOfAccident = string.Empty;
            string legalIssue = string.Empty;
            bool hasBusCamera = false;
            bool hasChargesheet = false;

            if (upperType == "MVC" && caseId > 0)
            {
                var c = _caseRepo.GetCaseById(caseId);
                if (c != null)
                {
                    vehicleType = c.VehicleType ?? "Bus";
                    courtName = c.MACTName ?? "MACT";
                    currentStage = c.CurrentStage ?? "Pending";
                    if (c.AdverseAward != null)
                    {
                        mannerOfAccident = c.AdverseAward.MannerOfAccident ?? "";
                        hasBusCamera = c.AdverseAward.IsBusCameraInstalled == true;
                        hasChargesheet = c.AdverseAward.IsChargeSheetFiled == true;
                        legalIssue = $"Contributory Negligence (Corporation Liability {c.AdverseAward.LiabilityPercentage ?? 100}%)";
                    }
                }
            }
            else if (upperType == "LABOUR" && caseId > 0)
            {
                var l = _labourRepo.GetCaseById(caseId);
                if (l != null)
                {
                    courtName = l.CourtName ?? "Labour Court";
                    currentStage = l.CurrentStage ?? "Pending";
                    legalIssue = l.NatureOfMisconduct ?? l.NatureOfCase ?? "Industrial Dispute";
                }
            }

            string combinedSearchTerms = queryOrContext ?? string.Empty;
            if (string.IsNullOrWhiteSpace(combinedSearchTerms))
            {
                combinedSearchTerms = $"{vehicleType} accident {mannerOfAccident} {legalIssue} compensation 166";
            }

            try
            {
                // Run concurrent search tasks across repositories
                var internalTask = _internalSearch.SearchSimilarCasesAsync(upperType, caseId, combinedSearchTerms, 5, cancellationToken);
                var judgementTask = _internalSearch.SearchJudgementsAsync(combinedSearchTerms, 5, cancellationToken);
                var webTask = _webSearch.SearchPrecedentsAsync(combinedSearchTerms, null, 5, cancellationToken);

                await Task.WhenAll(internalTask, judgementTask, webTask);

                // 1. Process Internal Cases with Multi-Factor Scoring
                foreach (var match in await internalTask)
                {
                    double score = CalculateMultiFactorScore(
                        matchType: match.CaseType,
                        targetType: upperType,
                        matchCourt: match.CourtOrTribunal,
                        targetCourt: courtName,
                        matchSummary: match.Summary,
                        searchTerms: combinedSearchTerms);

                    results.Add(new UnifiedSimilarCaseDto
                    {
                        CaseId = match.CaseId,
                        SourceType = $"NWKRTC_{match.CaseType}",
                        CaseNumber = match.CaseNumber,
                        Court = match.CourtOrTribunal,
                        FactsSummary = match.Summary,
                        LegalIssues = $"{match.CaseType} Dispute & Claim Assessment",
                        OutcomeOrStage = "Internal Record",
                        SimilarityScore = Math.Round(score, 2),
                        SimilarityBasis = $"[NWKRTC Case Repository] Multi-factor: Case Type ({match.CaseType}), Jurisdiction ({match.CourtOrTribunal}), Factual Alignment"
                    });
                }

                // 2. Process JUDGEMENT_REPO Rulings
                foreach (var j in await judgementTask)
                {
                    double score = CalculateJudgmentScore(j, combinedSearchTerms, upperType);
                    results.Add(new UnifiedSimilarCaseDto
                    {
                        CaseId = j.JudgementID,
                        SourceType = "JudgementRepo",
                        CaseNumber = $"Judgement Repo #{j.JudgementID}",
                        Court = j.Court ?? "High Court of Karnataka",
                        FactsSummary = j.Title,
                        LegalIssues = j.Remarks ?? "Judicial Precedent",
                        OutcomeOrStage = j.JudgementDate?.ToString("dd-MM-yyyy") ?? "Precedent",
                        SimilarityScore = Math.Round(score, 2),
                        SimilarityBasis = $"[Judgement Repo] Binding/persuasive judicial precedent on: {j.Remarks ?? j.Title}",
                        Citation = $"Judgement Repo #{j.JudgementID}"
                    });
                }

                // 3. Process Authoritative External Precedents
                foreach (var w in await webTask)
                {
                    double score = w.SourceType == "SupremeCourt" ? 0.93 : 0.87;
                    results.Add(new UnifiedSimilarCaseDto
                    {
                        SourceType = w.SourceType == "SupremeCourt" ? "SupremeCourt_Precedent" : "HighCourt_Precedent",
                        CaseNumber = w.CaseNumber ?? w.Title,
                        Court = w.Court,
                        FactsSummary = w.Title,
                        LegalIssues = w.Excerpt.Length > 150 ? w.Excerpt.Substring(0, 150) + "..." : w.Excerpt,
                        OutcomeOrStage = w.JudgmentDate?.ToString("dd-MM-yyyy") ?? "Judicial Authority",
                        SimilarityScore = score,
                        SimilarityBasis = $"[Authoritative Legal Source] {w.Court} precedent regarding statutory interpretation and SRTC liabilities.",
                        Url = w.Url
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing multi-factor similar case search for {CaseType} #{CaseId}", upperType, caseId);
            }

            // Deduplicate by case number and rank descending by multi-factor similarity score
            return results
                .GroupBy(r => r.CaseNumber, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .OrderByDescending(r => r.SimilarityScore)
                .Take(Math.Max(1, maxResults))
                .ToList();
        }

        private static double CalculateMultiFactorScore(
            string matchType,
            string targetType,
            string matchCourt,
            string targetCourt,
            string matchSummary,
            string searchTerms)
        {
            double score = 0.50;

            // Factor 1: Case Type Match (0.20 weight)
            if (string.Equals(matchType, targetType, StringComparison.OrdinalIgnoreCase))
                score += 0.20;

            // Factor 2: Court / Tribunal Jurisdiction (0.15 weight)
            if (!string.IsNullOrWhiteSpace(matchCourt) && !string.IsNullOrWhiteSpace(targetCourt) &&
                (matchCourt.Contains(targetCourt, StringComparison.OrdinalIgnoreCase) || targetCourt.Contains(matchCourt, StringComparison.OrdinalIgnoreCase)))
            {
                score += 0.15;
            }

            // Factor 3: Factual & Legal Keywords Alignment (0.15 weight)
            var terms = searchTerms.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            int matched = terms.Count(t => matchSummary.Contains(t, StringComparison.OrdinalIgnoreCase));
            score += Math.Min(0.14, matched * 0.04);

            return Math.Min(0.99, score);
        }

        private static double CalculateJudgmentScore(JudgementViewModel j, string query, string caseType)
        {
            double score = 0.55;
            string haystack = $"{j.Title} {j.Court} {j.Remarks}".ToLowerInvariant();
            var words = query.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);

            int matches = words.Count(w => haystack.Contains(w));
            score += Math.Min(0.35, matches * 0.07);

            if (caseType == "MVC" && (haystack.Contains("compensation") || haystack.Contains("accident") || haystack.Contains("negligence")))
                score += 0.08;

            return Math.Min(0.99, score);
        }
    }
}
