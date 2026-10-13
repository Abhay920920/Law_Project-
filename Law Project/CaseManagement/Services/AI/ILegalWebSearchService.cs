using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MVCCaseManagement.Models.AI;

namespace MVCCaseManagement.Services.AI
{
    public interface ILegalWebSearchService
    {
        Task<List<ExternalLegalSourceDto>> SearchPrecedentsAsync(string query, LegalSearchFilter? filter = null, int maxResults = 5, CancellationToken cancellationToken = default);
        Task<List<ExternalLegalSourceDto>> SearchRecentJudgmentsAsync(string legalTopic, string? court = null, int maxResults = 5, CancellationToken cancellationToken = default);
        Task<List<ExternalLegalSourceDto>> SearchByCaseOrCitationAsync(string caseOrCitation, CancellationToken cancellationToken = default);
        Task<List<ExternalLegalSourceDto>> SearchByPartiesAsync(string petitioner, string respondent, string? court = null, int maxResults = 5, CancellationToken cancellationToken = default);
    }
}
