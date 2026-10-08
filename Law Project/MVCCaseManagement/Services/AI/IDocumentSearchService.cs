using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MVCCaseManagement.Models.AI;

namespace MVCCaseManagement.Services.AI
{
    public interface IDocumentSearchService
    {
        /// <summary>
        /// Searches uploaded PDFs and case documents while preserving Document, Case, Page Number, Section, and Text.
        /// Results never expose physical filesystem paths and provide secure document viewer citations.
        /// </summary>
        Task<List<DocumentSearchResultDto>> SearchDocumentsAsync(
            string query,
            string? caseType = null,
            int? caseId = null,
            int maxResults = 5,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Extracts pages of a specific document while preserving page numbers, section headers, and text.
        /// </summary>
        Task<List<DocumentPageExtractDto>> ExtractPagesAsync(
            string relativeOrFileName,
            string? caseType = null,
            int? caseId = null,
            int maxPages = 15,
            CancellationToken cancellationToken = default);
    }
}
