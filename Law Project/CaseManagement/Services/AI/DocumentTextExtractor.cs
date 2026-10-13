using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using UglyToad.PdfPig;

namespace MVCCaseManagement.Services.AI
{
    public class DocumentTextExtractor : IDocumentTextExtractor
    {
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<DocumentTextExtractor> _logger;

        private static readonly string[] AllowedExtensions = new[]
        {
            ".pdf", ".txt"
        };

        public DocumentTextExtractor(
            IWebHostEnvironment environment,
            ILogger<DocumentTextExtractor> logger)
        {
            _environment = environment ?? throw new ArgumentNullException(nameof(environment));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public string? ResolveSecurePhysicalPath(string relativeOrFileName)
        {
            if (string.IsNullOrWhiteSpace(relativeOrFileName))
                return null;

            string cleanPath = Uri.UnescapeDataString(relativeOrFileName).TrimStart('/', '\\');
            if (cleanPath.Contains("..") || Path.IsPathRooted(cleanPath))
            {
                _logger.LogWarning("Potential path traversal attempt detected in DocumentTextExtractor: {Path}", relativeOrFileName);
                return null;
            }

            string ext = Path.GetExtension(cleanPath).ToLowerInvariant();
            if (!AllowedExtensions.Contains(ext))
            {
                return null;
            }

            string fileName = Path.GetFileName(cleanPath);
            string canonicalContentUploads = Path.GetFullPath(Path.Combine(_environment.ContentRootPath, "uploads"));
            string canonicalWebUploads = Path.GetFullPath(Path.Combine(_environment.WebRootPath, "uploads"));

            var candidatePaths = new[]
            {
                Path.Combine(_environment.ContentRootPath, "uploads", cleanPath),
                Path.Combine(_environment.WebRootPath, "uploads", cleanPath),
                Path.Combine(_environment.ContentRootPath, "uploads", fileName),
                Path.Combine(_environment.WebRootPath, "uploads", fileName)
            };

            foreach (var candidate in candidatePaths)
            {
                string fullCandidate = Path.GetFullPath(candidate);
                if (fullCandidate.StartsWith(canonicalContentUploads, StringComparison.OrdinalIgnoreCase) ||
                    fullCandidate.StartsWith(canonicalWebUploads, StringComparison.OrdinalIgnoreCase))
                {
                    if (File.Exists(fullCandidate))
                    {
                        return fullCandidate;
                    }
                }
            }

            // Search by filename inside uploads if not directly at candidate path
            if (fileName.Length >= 4)
            {
                if (Directory.Exists(canonicalContentUploads))
                {
                    var files = Directory.GetFiles(canonicalContentUploads, fileName, SearchOption.AllDirectories);
                    if (files.Length > 0 && File.Exists(files[0]))
                        return files[0];
                }

                if (Directory.Exists(canonicalWebUploads))
                {
                    var files = Directory.GetFiles(canonicalWebUploads, fileName, SearchOption.AllDirectories);
                    if (files.Length > 0 && File.Exists(files[0]))
                        return files[0];
                }
            }

            return null;
        }

        public Task<string> ExtractTextAsync(string relativeOrFileName, int maxPages = 15, CancellationToken cancellationToken = default)
        {
            return Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                string? physicalPath = ResolveSecurePhysicalPath(relativeOrFileName);
                if (physicalPath == null || !File.Exists(physicalPath))
                {
                    string safeName = Path.GetFileName(relativeOrFileName);
                    return $"[Notice: Document file '{safeName}' is referenced in records but not accessible on local server storage.]";
                }

                string ext = Path.GetExtension(physicalPath).ToLowerInvariant();
                string docName = Path.GetFileName(physicalPath);

                if (ext == ".txt")
                {
                    try
                    {
                        string text = File.ReadAllText(physicalPath);
                        if (text.Length > 50000)
                            text = text.Substring(0, 50000) + "\n...[Content truncated at 50,000 characters]...";

                        return $"--- [Document: {docName}] ---\n" + text;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to read text file: {Path}", physicalPath);
                        return $"[Error reading document '{docName}': {ex.Message}]";
                    }
                }

                if (ext == ".pdf")
                {
                    try
                    {
                        var sb = new StringBuilder();
                        using var pdf = PdfDocument.Open(physicalPath);
                        int totalPages = pdf.NumberOfPages;
                        int pagesToRead = Math.Min(totalPages, Math.Max(1, maxPages));

                        sb.AppendLine($"--- [Document Header: {docName} | Total Pages: {totalPages} | Extracted Pages: 1 to {pagesToRead}] ---");

                        bool foundAnyText = false;
                        for (int pageNum = 1; pageNum <= pagesToRead; pageNum++)
                        {
                            cancellationToken.ThrowIfCancellationRequested();

                            var page = pdf.GetPage(pageNum);
                            string pageText = page.Text;

                            sb.AppendLine();
                            sb.AppendLine($"--- [Page {pageNum} of {totalPages}] ---");

                            if (string.IsNullOrWhiteSpace(pageText))
                            {
                                sb.AppendLine("[No selectable text on this page. Page may be a scanned image or photograph.]");
                            }
                            else
                            {
                                foundAnyText = true;
                                sb.AppendLine(pageText.Trim());
                            }
                        }

                        if (!foundAnyText)
                        {
                            sb.AppendLine();
                            sb.AppendLine("[Note: This PDF appears to be a scanned image or photographic document without embedded OCR text layer.]");
                        }
                        else if (totalPages > pagesToRead)
                        {
                            sb.AppendLine();
                            sb.AppendLine($"[Note: Extraction capped at first {pagesToRead} pages of {totalPages} total pages.]");
                        }

                        return sb.ToString();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error extracting text from PDF '{DocName}' at '{Path}'", docName, physicalPath);
                        return $"[Document extraction failed for '{docName}': {ex.Message}]";
                    }
                }

                return $"[Unsupported document format '{ext}' for text analysis.]";
            }, cancellationToken);
        }
    }
}
