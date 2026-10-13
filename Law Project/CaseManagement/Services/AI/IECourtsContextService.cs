using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MVCCaseManagement.Models.AI;

namespace MVCCaseManagement.Services.AI
{
    public interface IECourtsContextService
    {
        /// <summary>
        /// Retrieves and normalizes e-Courts case summary, history, and court orders for a given CNR / case.
        /// Reuses existing IECourtsRepository and IECourtsNapixService without modifying them.
        /// Gracefully handles missing CNRs, timeouts, or API unavailability.
        /// </summary>
        Task<ECourtsCaseSummaryDto> GetCaseSummaryAsync(string? cnrNumber, string caseType, int caseId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves past orders, interim orders, and final judgments from e-Courts if available.
        /// </summary>
        Task<List<ECourtsOrderDto>> GetOrdersAndJudgmentsAsync(string cnrNumber, bool isHighCourt = false, CancellationToken cancellationToken = default);
    }
}
