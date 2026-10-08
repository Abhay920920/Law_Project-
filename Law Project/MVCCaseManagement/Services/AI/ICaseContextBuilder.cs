using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MVCCaseManagement.Models.AI;

namespace MVCCaseManagement.Services.AI
{
    public interface ICaseContextBuilder
    {
        /// <summary>
        /// Assembles a complete, structured legal dossier for the specified case.
        /// Aggregates case records, chronological internal notings, relevant judgments, 
        /// extracted PDF documents, similar cases, and applicable provisions.
        /// </summary>
        Task<CaseDossier?> BuildDossierAsync(string caseType, int caseId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Assembles a legal dossier with optional deep external enrichment (e-Courts live API and Web precedent scraping).
        /// Default is false for fast page load and dossier rendering (< 50ms).
        /// </summary>
        Task<CaseDossier?> BuildDossierAsync(string caseType, int caseId, bool deepEnrich, CancellationToken cancellationToken = default);

        /// <summary>
        /// Attempts to parse and resolve case identifier from user natural language query (e.g. "MVC/123/2024", "KID/45/2023", "MFA/567/2022")
        /// and build the dossier.
        /// </summary>
        Task<CaseDossier?> ResolveAndBuildDossierAsync(string caseSearchTerm, CancellationToken cancellationToken = default);

        /// <summary>
        /// Resolves complex legal queries using a structured LegalQueryPlan (e.g. Vehicle History, Advocate Caseload, Hearing Calendar, Financial Exposure).
        /// </summary>
        Task<CaseDossier?> ResolveAndBuildDossierAsync(string caseSearchTerm, LegalQueryPlan? plan, int userDivisionId = 5, CancellationToken cancellationToken = default);

        /// <summary>
        /// Quick search to populate autocomplete or case selector dropdowns with scoped division filtering.
        /// </summary>
        Task<List<CaseSearchItemDto>> SearchCasesAsync(string query, int maxResults = 15, int userDivisionId = 5, CancellationToken cancellationToken = default);
    }
}
