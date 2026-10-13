using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MVCCaseManagement.Models.AI;

namespace MVCCaseManagement.Services.AI
{
    public interface ISimilarCaseService
    {
        /// <summary>
        /// Identifies similar cases across NWKRTC repositories, JUDGEMENT_REPO, and authoritative precedents
        /// based on multi-dimensional comparison: Facts, Legal Issues, Legal Provisions, Court, Case Type,
        /// Procedural Stage, Arguments, Evidence, Outcome, and Terminology.
        /// </summary>
        Task<List<UnifiedSimilarCaseDto>> FindSimilarCasesAsync(
            string caseType,
            int caseId,
            string? queryOrContext = null,
            int maxResults = 6,
            CancellationToken cancellationToken = default);
    }
}
