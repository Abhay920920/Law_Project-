using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MVCCaseManagement.Models;
using MVCCaseManagement.Models.AI;

namespace MVCCaseManagement.Services.AI
{
    public interface ILegalSearchService
    {
        Task<List<JudgementViewModel>> SearchJudgementsAsync(string query, int top = 5, CancellationToken cancellationToken = default);
        Task<List<SimilarCaseMatch>> SearchSimilarCasesAsync(string caseType, int caseId, string? queryKeywords = null, int top = 5, CancellationToken cancellationToken = default);
        Task<List<CaseNoting>> SearchCaseNotingsAsync(string query, int top = 5, CancellationToken cancellationToken = default);
        List<string> GetApplicableProvisions(string caseType);
    }
}
