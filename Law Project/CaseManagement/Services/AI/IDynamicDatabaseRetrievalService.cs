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

        /// <summary>
        /// Compiles and executes an arbitrary natural language database inquiry across all NWKRTC modules
        /// (MVC, Labour, Gratuity, Appeals, Petty Bills), enforcing multi-tenant isolation and returning styled Markdown.
        /// </summary>
        Task<DynamicQueryResult> ExecuteDynamicInquiryAsync(
            string naturalQuery,
            int userDivisionId,
            bool isCentralOffice,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Directly retrieves a complete CaseDossier from live database records for any module.
        /// </summary>
        Task<CaseDossier?> RetrieveCaseDossierAsync(
            string module,
            string caseNumber,
            int year,
            int userDivisionId,
            bool isCentralOffice,
            CancellationToken cancellationToken = default);
    }
}
