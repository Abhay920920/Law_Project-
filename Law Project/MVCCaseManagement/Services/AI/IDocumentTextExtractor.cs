using System.Threading;
using System.Threading.Tasks;

namespace MVCCaseManagement.Services.AI
{
    public interface IDocumentTextExtractor
    {
        /// <summary>
        /// Safely resolves the document path, verifies security boundaries, and extracts text content with page markings.
        /// </summary>
        Task<string> ExtractTextAsync(string relativeOrFileName, int maxPages = 15, CancellationToken cancellationToken = default);

        /// <summary>
        /// Resolves the secure canonical physical file path for an upload, or null if invalid or not found.
        /// </summary>
        string? ResolveSecurePhysicalPath(string relativeOrFileName);
    }
}
