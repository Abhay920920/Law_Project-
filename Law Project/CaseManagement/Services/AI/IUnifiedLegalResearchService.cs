using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MVCCaseManagement.Models.AI;

namespace MVCCaseManagement.Services.AI
{
    public class LegalResearchRequest
    {
        public string Question { get; set; } = string.Empty;
        public int? ConversationId { get; set; }
        public string? CaseType { get; set; }
        public int? CaseId { get; set; }
        public string? QuickAction { get; set; }
        public int UserId { get; set; }
        public int DivisionId { get; set; }
        public string UserRole { get; set; } = "Officer";
        public bool IncludeWeb { get; set; } = false;
    }

    public class LegalResearchResult
    {
        public bool Success { get; set; } = true;
        public int ConversationId { get; set; }
        public string Answer { get; set; } = string.Empty;
        public List<string> SourcesSearched { get; set; } = new();
        public List<CitationDto> Citations { get; set; } = new();
        public List<DetectedConflictDto> Conflicts { get; set; } = new();
        public string RouteCategory { get; set; } = string.Empty;
        public string? DossierSummary { get; set; }
        public string? FollowUpContext { get; set; }
        public string? ErrorMessage { get; set; }
        public string Model { get; set; } = string.Empty;
        public int ExecutionTimeMs { get; set; }
        public VerificationResult? Verification { get; set; }
        public EvidencePack? EvidencePack { get; set; }
        public EvidenceSufficiencyEvaluation? Sufficiency { get; set; }
    }

    public interface IUnifiedLegalResearchService
    {
        /// <summary>
        /// Coordinates the end-to-end legal research pipeline:
        /// Query Understanding -> Source Selection -> Concurrent Search -> Result Normalization ->
        /// Relevance Ranking -> Conflict Detection -> Evidence Package Assembly -> Claude Reasoning ->
        /// Grounded Answer + Citations.
        /// </summary>
        Task<LegalResearchResult> ExecuteResearchAsync(
            LegalResearchRequest request,
            CancellationToken cancellationToken = default);
    }
}
