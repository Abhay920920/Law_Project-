using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace MVCCaseManagement.Services.Storage
{
    /// <summary>
    /// Production-grade physical file storage service.
    /// Decouples persistent legal files from Git/IIS deployment working trees to ensure zero data loss.
    /// </summary>
    public class PhysicalFileStorageService : IFileStorageService
    {
        private readonly IWebHostEnvironment _env;
        private readonly IConfiguration _config;
        private readonly ILogger<PhysicalFileStorageService> _logger;
        private readonly string _vaultRootPath;

        private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".pdf", ".doc", ".docx", ".jpg", ".jpeg", ".png", ".gif", ".tiff", ".bmp", ".txt"
        };

        private const long MaxFileSizeBytes = 50 * 1024 * 1024; // 50 MB production threshold

        public PhysicalFileStorageService(
            IWebHostEnvironment env,
            IConfiguration config,
            ILogger<PhysicalFileStorageService> logger)
        {
            _env = env;
            _config = config;
            _logger = logger;
            _vaultRootPath = InitializeVaultRoot();
        }

        public string GetVaultRootPath() => _vaultRootPath;

        public async Task<string> SaveFileAsync(
            IFormFile? file,
            string subFolder,
            string? existingFilePath = null,
            CancellationToken cancellationToken = default)
        {
            if (file == null || file.Length == 0)
            {
                return existingFilePath ?? string.Empty;
            }

            // 1. Validation: File Size
            if (file.Length > MaxFileSizeBytes)
            {
                throw new InvalidOperationException($"File size ({file.Length / (1024 * 1024)} MB) exceeds maximum allowed limit of 50 MB.");
            }

            // 2. Validation: Extension Whitelist
            string ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (string.IsNullOrEmpty(ext) || !AllowedExtensions.Contains(ext))
            {
                throw new InvalidOperationException($"File extension '{ext}' is not permitted. Only PDF, Office documents, and images are allowed.");
            }

            // 3. Security: Magic bytes validation
            ValidateFileHeader(file, ext);

            // 4. Sanitize subfolder to prevent path traversal
            string cleanSubFolder = SanitizeSubFolder(subFolder);

            // 5. Clean up old file if replacing
            DeleteOldFileIfPresent(existingFilePath);

            // 6. Ensure destination directory exists in persistent vault
            string targetDir = Path.Combine(_vaultRootPath, cleanSubFolder);
            if (!Directory.Exists(targetDir))
            {
                Directory.CreateDirectory(targetDir);
            }

            // 7. Write securely using GUID to prevent filename collisions and enumeration attacks
            string uniqueFileName = $"{Guid.NewGuid():N}{ext}";
            string physicalDestination = Path.Combine(targetDir, uniqueFileName);

            using (var stream = new FileStream(physicalDestination, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true))
            {
                await file.CopyToAsync(stream, cancellationToken);
            }

            _logger.LogInformation("Successfully saved persistent document to vault: {PhysicalPath} ({Size} bytes)", physicalDestination, file.Length);

            // Also synchronize to local WebRoot uploads if running in development mode for convenience
            TryMirrorToWebRoot(physicalDestination, cleanSubFolder, uniqueFileName);

            // Standard relative URL route handled by UploadsController
            return $"/uploads/{cleanSubFolder.Replace('\\', '/')}/{uniqueFileName}";
        }

        public string? LocatePhysicalFile(string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath)) return null;

            string cleanPath = CleanRelativePath(relativePath);
            if (cleanPath.Contains("..") || Path.IsPathRooted(cleanPath))
            {
                _logger.LogWarning("Path traversal attempt blocked in LocatePhysicalFile: {Path}", relativePath);
                return null;
            }

            string fileName = Path.GetFileName(cleanPath);

            // Candidate priority 1: Configured External Persistent Vault
            var candidatePaths = new List<string>
            {
                Path.Combine(_vaultRootPath, cleanPath),
                Path.Combine(_vaultRootPath, fileName)
            };

            // Candidate priority 2: Parent directory uploads_vault (e.g., ../uploads_vault)
            try
            {
                string parentVault = Path.GetFullPath(Path.Combine(_env.ContentRootPath, "..", "uploads_vault"));
                if (Directory.Exists(parentVault))
                {
                    candidatePaths.Add(Path.Combine(parentVault, cleanPath));
                    candidatePaths.Add(Path.Combine(parentVault, fileName));
                }
            }
            catch { /* Ignore parent path check errors */ }

            // Candidate priority 3: ContentRoot uploads
            if (!string.IsNullOrEmpty(_env.ContentRootPath))
            {
                candidatePaths.Add(Path.Combine(_env.ContentRootPath, "uploads", cleanPath));
                candidatePaths.Add(Path.Combine(_env.ContentRootPath, "uploads", fileName));
            }

            // Candidate priority 4: WebRoot uploads
            if (!string.IsNullOrEmpty(_env.WebRootPath))
            {
                candidatePaths.Add(Path.Combine(_env.WebRootPath, "uploads", cleanPath));
                candidatePaths.Add(Path.Combine(_env.WebRootPath, "uploads", fileName));
            }

            // Probe explicit candidate paths
            foreach (var candidate in candidatePaths)
            {
                try
                {
                    string fullCandidate = Path.GetFullPath(candidate);
                    if (File.Exists(fullCandidate))
                    {
                        return fullCandidate;
                    }
                }
                catch { /* Ignore invalid candidate paths */ }
            }

            // Candidate priority 5: Recursive search within Vault and WebRoot for files >= 6 chars
            if (fileName.Length >= 6)
            {
                var searchDirs = new[] { _vaultRootPath, _env.WebRootPath != null ? Path.Combine(_env.WebRootPath, "uploads") : null }
                    .Where(d => !string.IsNullOrEmpty(d) && Directory.Exists(d));

                foreach (var dir in searchDirs)
                {
                    try
                    {
                        var matches = Directory.GetFiles(dir!, fileName, SearchOption.AllDirectories);
                        if (matches.Length > 0 && File.Exists(matches[0]))
                        {
                            return matches[0];
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "Directory search exception in {Directory} for {FileName}", dir, fileName);
                    }
                }
            }

            return null;
        }

        public bool FileExists(string relativePath)
        {
            return LocatePhysicalFile(relativePath) != null;
        }

        private string InitializeVaultRoot()
        {
            // Priority 1: Configured path in appsettings.json
            string? configuredPath = _config["Storage:UploadsDirectory"] 
                                  ?? _config["AppSettings:UploadsDirectory"]
                                  ?? Environment.GetEnvironmentVariable("LAW_PROJECT_UPLOADS_DIR");

            if (!string.IsNullOrWhiteSpace(configuredPath))
            {
                try
                {
                    string fullConfigured = Path.GetFullPath(configuredPath);
                    if (!Directory.Exists(fullConfigured))
                    {
                        Directory.CreateDirectory(fullConfigured);
                    }
                    _logger.LogInformation("Initialized File Vault using configured path: {VaultPath}", fullConfigured);
                    return fullConfigured;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to initialize configured upload vault path: {Path}. Falling back to default detection.", configuredPath);
                }
            }

            // Priority 2: Standard Plesk production external vault (outside httpdocs / publish)
            try
            {
                string pleskCommonVault = @"C:\inetpub\vhosts\itnwkrtc.in\uploads_vault";
                if (Directory.Exists(@"C:\inetpub\vhosts\itnwkrtc.in"))
                {
                    if (!Directory.Exists(pleskCommonVault)) Directory.CreateDirectory(pleskCommonVault);
                    _logger.LogInformation("Initialized File Vault at standard Plesk external root: {VaultPath}", pleskCommonVault);
                    return pleskCommonVault;
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Could not access or create standard Plesk vault path.");
            }

            // Priority 3: Sibling directory `../uploads_vault` relative to ContentRootPath
            try
            {
                string siblingVault = Path.GetFullPath(Path.Combine(_env.ContentRootPath, "..", "uploads_vault"));
                if (!Directory.Exists(siblingVault)) Directory.CreateDirectory(siblingVault);
                _logger.LogInformation("Initialized File Vault at sibling external root: {VaultPath}", siblingVault);
                return siblingVault;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Could not access or create sibling vault path.");
            }

            // Priority 4: Development fallback inside WebRoot/uploads or ContentRoot/uploads
            string fallbackWebRoot = !string.IsNullOrEmpty(_env.WebRootPath)
                ? Path.Combine(_env.WebRootPath, "uploads")
                : Path.Combine(_env.ContentRootPath, "uploads");

            if (!Directory.Exists(fallbackWebRoot)) Directory.CreateDirectory(fallbackWebRoot);
            _logger.LogInformation("Initialized File Vault using WebRoot fallback: {VaultPath}", fallbackWebRoot);
            return fallbackWebRoot;
        }

        private static string CleanRelativePath(string rawPath)
        {
            string unescaped = Uri.UnescapeDataString(rawPath).Replace('/', '\\').Trim('\\');
            if (unescaped.StartsWith("uploads\\", StringComparison.OrdinalIgnoreCase))
            {
                unescaped = unescaped.Substring("uploads\\".Length);
            }
            return unescaped;
        }

        private static string SanitizeSubFolder(string subFolder)
        {
            if (string.IsNullOrWhiteSpace(subFolder)) return "general";
            string clean = subFolder.Replace('/', '\\').Trim('\\');
            if (clean.Contains(".."))
            {
                clean = clean.Replace("..", "").Trim('\\');
            }
            return string.IsNullOrWhiteSpace(clean) ? "general" : clean;
        }

        public bool DeleteFileIfExists(string? relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath)) return false;
            bool deleted = false;
            try
            {
                string? physicalOld = LocatePhysicalFile(relativePath);
                if (!string.IsNullOrEmpty(physicalOld) && File.Exists(physicalOld))
                {
                    File.Delete(physicalOld);
                    _logger.LogInformation("Deleted document from storage: {Path}", physicalOld);
                    deleted = true;
                }

                if (!string.IsNullOrEmpty(_env.WebRootPath))
                {
                    string clean = CleanRelativePath(relativePath);
                    string webMirror = Path.Combine(_env.WebRootPath, "uploads", clean);
                    if (File.Exists(webMirror) && !string.Equals(webMirror, physicalOld, StringComparison.OrdinalIgnoreCase))
                    {
                        File.Delete(webMirror);
                        deleted = true;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete file {Path}: {Msg}", relativePath, ex.Message);
            }
            return deleted;
        }

        private void DeleteOldFileIfPresent(string? existingFilePath)
        {
            if (string.IsNullOrWhiteSpace(existingFilePath)) return;
            DeleteFileIfExists(existingFilePath);
        }

        private void TryMirrorToWebRoot(string vaultSourceFile, string subFolder, string fileName)
        {
            try
            {
                if (string.IsNullOrEmpty(_env.WebRootPath)) return;
                string webRootTargetDir = Path.Combine(_env.WebRootPath, "uploads", subFolder);
                if (!Directory.Exists(webRootTargetDir)) Directory.CreateDirectory(webRootTargetDir);
                string webRootFile = Path.Combine(webRootTargetDir, fileName);

                if (!string.Equals(vaultSourceFile, webRootFile, StringComparison.OrdinalIgnoreCase))
                {
                    File.Copy(vaultSourceFile, webRootFile, overwrite: true);
                }
            }
            catch { /* Mirroring to WebRoot is best-effort for local dev */ }
        }

        private static void ValidateFileHeader(IFormFile file, string ext)
        {
            try
            {
                using var stream = file.OpenReadStream();
                byte[] header = new byte[8];
                int bytesRead = stream.Read(header, 0, header.Length);
                if (bytesRead < 4) return;

                if (ext.Equals(".pdf", StringComparison.OrdinalIgnoreCase))
                {
                    // %PDF is 0x25, 0x50, 0x44, 0x46
                    if (header[0] != 0x25 || header[1] != 0x50 || header[2] != 0x44 || header[3] != 0x46)
                    {
                        throw new InvalidOperationException("The uploaded file does not contain a valid PDF signature.");
                    }
                }
            }
            catch (InvalidOperationException)
            {
                throw;
            }
            catch { /* Stream read check non-blocking */ }
        }
    }
}

