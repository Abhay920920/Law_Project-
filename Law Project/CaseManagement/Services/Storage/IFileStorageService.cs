using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace MVCCaseManagement.Services.Storage
{
    /// <summary>
    /// Unified high-performance file storage service for persistent legal documents.
    /// Manages document isolation, security verification, and decoupled persistent vault storage.
    /// </summary>
    public interface IFileStorageService
    {
        /// <summary>
        /// Validates, sanitizes, and writes an uploaded file to the persistent storage vault.
        /// </summary>
        /// <param name="file">The uploaded file.</param>
        /// <param name="subFolder">Target subcategory folder (e.g. punishments, claims, judgments).</param>
        /// <param name="existingFilePath">Optional previous file path to replace.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Standard relative web URL path (e.g. /uploads/punishments/{guid}.pdf) or empty string if file is null.</returns>
        Task<string> SaveFileAsync(IFormFile? file, string subFolder, string? existingFilePath = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Locates the absolute physical path of a file across candidate storage locations
        /// (external persistent vault, legacy content root, and web root).
        /// </summary>
        /// <param name="relativePath">Relative URL or decoded path (e.g. punishments/file.pdf or /uploads/punishments/file.pdf).</param>
        /// <returns>Full physical path if found, or null if file does not exist.</returns>
        string? LocatePhysicalFile(string relativePath);

        /// <summary>
        /// Checks whether a file exists in any storage location.
        /// </summary>
        bool FileExists(string relativePath);

        /// <summary>
        /// Safely deletes a file from the vault and legacy locations if present.
        /// </summary>
        bool DeleteFileIfExists(string? relativePath);

        /// <summary>
        /// Gets the primary root directory of the persistent storage vault.
        /// </summary>
        string GetVaultRootPath();
    }
}

