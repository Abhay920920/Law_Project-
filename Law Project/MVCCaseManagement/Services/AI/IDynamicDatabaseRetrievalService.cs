using System.Threading;
using System.Threading.Tasks;
using MVCCaseManagement.Models.AI;

namespace MVCCaseManagement.Services.AI
{
    public interface IDynamicDatabaseRetrievalService
    {
        /// <summary>
        /// Executes a schema-allowlisted, parameterized, read-only dynamic SQL retrieval
        /// based on the structured LegalQueryPlan, enforcing row-level division authorization.
        /// </summary>
        Task<DynamicQueryResult?> ExecuteDynamicQueryAsync(
            LegalQueryPlan plan,
            int userDivisionId,
            CancellationToken cancellationToken = default);
    }
}
