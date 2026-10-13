using System.Threading;
using System.Threading.Tasks;
using MVCCaseManagement.Models.AI;

namespace MVCCaseManagement.Services.AI
{
    public interface IQueryRouterService
    {
        /// <summary>
        /// Classifies a natural-language legal question into required source categories
        /// (INTERNAL_DATABASE, INTERNAL_DOCUMENTS, ECOURTS, LEGAL_WEB, JUDGMENTS, SIMILAR_CASES, MULTI_SOURCE)
        /// and extracts case identifiers, statutory provisions, and query focus.
        /// </summary>
        QueryRouteResult RouteQuery(string question, string? contextCaseType = null, int? contextCaseId = null);
    }
}
