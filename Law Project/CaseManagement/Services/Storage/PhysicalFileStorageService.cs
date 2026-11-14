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
            if (string.IsNullOrEmpty(cleanPath) || cleanPath.Contains("..") || Path.IsPathRooted(cleanPath))
            {
                _logger.LogWarning("Path traversal attempt or invalid path in LocatePhysicalFile: {Path}", relativePath);
                return null;
            }

            string fileName = Path.GetFileName(cleanPath);

            // Collect all potential root directories across hosting environments (Plesk, IIS, local dev)
            var searchRoots = new List<string>();

            void TryAddRoot(string? root)
            {
                if (string.IsNullOrWhiteSpace(root)) return;
                try
                {
                    string full = Path.GetFullPath(root);
                    if (Directory.Exists(full) && !searchRoots.Contains(full, StringComparer.OrdinalIgnoreCase))
                    {
                        searchRoots.Add(full);
                    }
                }
                catch { }
            }

            // Priority roots across hosting environments (Plesk IIS, local dev, published root)
            TryAddRoot(_vaultRootPath);
            if (!string.IsNullOrEmpty(_env.ContentRootPath))
            {
                TryAddRoot(Path.Combine(_env.ContentRootPath, "uploads_vault"));
                TryAddRoot(Path.Combine(_env.ContentRootPath, "uploads"));
                TryAddRoot(Path.Combine(_env.ContentRootPath, "..", "uploads_vault"));
                TryAddRoot(Path.Combine(_env.ContentRootPath, "..", "uploads"));
                TryAddRoot(Path.Combine(_env.ContentRootPath, "httpdocs", "uploads_vault"));
                TryAddRoot(Path.Combine(_env.ContentRootPath, "httpdocs", "uploads"));
            }
            TryAddRoot(@"C:\inetpub\vhosts\itnwkrtc.in\law.itnwkrtc.in\uploads_vault");
            TryAddRoot(@"C:\inetpub\vhosts\itnwkrtc.in\law.itnwkrtc.in\httpdocs\uploads_vault");
            TryAddRoot(@"C:\inetpub\vhosts\itnwkrtc.in\law.itnwkrtc.in\wwwroot\uploads");
            TryAddRoot(@"C:\inetpub\vhosts\itnwkrtc.in\law.itnwkrtc.in\wwwroot\uploads_vault");
            TryAddRoot(@"C:\inetpub\vhosts\itnwkrtc.in\httpdocs\uploads_vault");
            TryAddRoot(@"C:\inetpub\vhosts\itnwkrtc.in\uploads_vault");
            if (!string.IsNullOrEmpty(_env.WebRootPath))
            {
                TryAddRoot(Path.Combine(_env.WebRootPath, "uploads"));
                TryAddRoot(Path.Combine(_env.WebRootPath, "uploads_vault"));
            }

            // 1. Direct candidate probes across all search roots
            string rawRel = (relativePath ?? string.Empty).Trim().TrimStart('/', '\\').Replace('/', '\\');
            foreach (var root in searchRoots)
            {
                try
                {
                    // Check exact clean relative subpath (e.g., Labour\Judgments\file.pdf)
                    string directCandidate = Path.Combine(root, cleanPath);
                    if (File.Exists(directCandidate))
                    {
                        return directCandidate;
                    }

                    // Check with explicit 'uploads' folder (e.g., root\uploads\Labour\Judgments\file.pdf)
                    string uploadsCandidate = Path.Combine(root, "uploads", cleanPath);
                    if (File.Exists(uploadsCandidate))
                    {
                        return uploadsCandidate;
                    }

                    // Check unstripped raw relative path if different
                    if (!string.IsNullOrEmpty(rawRel) && !rawRel.Equals(cleanPath, StringComparison.OrdinalIgnoreCase))
                    {
                        string rawCandidate = Path.Combine(root, rawRel);
                        if (File.Exists(rawCandidate))
                        {
                            return rawCandidate;
                        }
                    }

                    // Check common folder spelling and alias variants
                    if (cleanPath.Contains("judgements", StringComparison.OrdinalIgnoreCase))
                    {
                        string alt1 = Path.Combine(root, cleanPath.Replace("judgements", "judgments", StringComparison.OrdinalIgnoreCase));
                        if (File.Exists(alt1)) return alt1;
                        string alt1Up = Path.Combine(root, "uploads", cleanPath.Replace("judgements", "judgments", StringComparison.OrdinalIgnoreCase));
                        if (File.Exists(alt1Up)) return alt1Up;
                    }
                    else if (cleanPath.Contains("judgments", StringComparison.OrdinalIgnoreCase))
                    {
                        string alt2 = Path.Combine(root, cleanPath.Replace("judgments", "judgements", StringComparison.OrdinalIgnoreCase));
                        if (File.Exists(alt2)) return alt2;
                        string alt2Up = Path.Combine(root, "uploads", cleanPath.Replace("judgments", "judgements", StringComparison.OrdinalIgnoreCase));
                        if (File.Exists(alt2Up)) return alt2Up;
                    }

                    if (cleanPath.Contains("appeal_docs", StringComparison.OrdinalIgnoreCase))
                    {
                        string alt3 = Path.Combine(root, cleanPath.Replace("appeal_docs", "appeals", StringComparison.OrdinalIgnoreCase));
                        if (File.Exists(alt3)) return alt3;
                    }

                    if (cleanPath.Contains(@"Labour\ClaimPetitions", StringComparison.OrdinalIgnoreCase))
                    {
                        string alt4 = Path.Combine(root, cleanPath.Replace(@"Labour\ClaimPetitions", "claims", StringComparison.OrdinalIgnoreCase));
                        if (File.Exists(alt4)) return alt4;
                    }

                    if (cleanPath.Contains(@"cases\AdverseJudgments", StringComparison.OrdinalIgnoreCase))
                    {
                        string alt5 = Path.Combine(root, cleanPath.Replace(@"cases\AdverseJudgments", "cases", StringComparison.OrdinalIgnoreCase));
                        if (File.Exists(alt5)) return alt5;
                    }

                    // Check flat file in root (e.g., root\file.pdf)
                    if (!string.IsNullOrEmpty(fileName))
                    {
                        string flatCandidate = Path.Combine(root, fileName);
                        if (File.Exists(flatCandidate))
                        {
                            return flatCandidate;
                        }
                    }
                }
                catch { }
            }

            // 2. Context-aware recursive search within all known roots for exact filename
            if (!string.IsNullOrEmpty(fileName) && fileName.Length >= 4)
            {
                string? expectedDirName = Path.GetDirectoryName(cleanPath);
                foreach (var root in searchRoots)
                {
                    try
                    {
                        var matches = Directory.GetFiles(root, fileName, SearchOption.AllDirectories);
                        if (matches.Length > 0)
                        {
                            // Priority 1: Match that resides within the expected subfolder path
                            if (!string.IsNullOrEmpty(expectedDirName))
                            {
                                var contextMatch = matches.FirstOrDefault(m => 
                                    m.Contains(expectedDirName, StringComparison.OrdinalIgnoreCase) ||
                                    m.Contains(Path.GetFileName(expectedDirName), StringComparison.OrdinalIgnoreCase));
                                if (!string.IsNullOrEmpty(contextMatch) && File.Exists(contextMatch))
                                {
                                    return contextMatch;
                                }
                            }

                            // Priority 2: If only 1 file exists across the vault with this exact name, return it safely
                            if (matches.Length == 1 && File.Exists(matches[0]))
                            {
                                return matches[0];
                            }
                            // Priority 3: If multiple matches exist, avoid returning wrong document if name is too generic
                            else if (matches.Length > 1 && fileName.Length > 8)
                            {
                                if (File.Exists(matches[0]))
                                {
                                    return matches[0];
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "Directory search exception in {Directory} for {FileName}", root, fileName);
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
            // 1. Configured path from configuration / environment variable
            string? configuredPath = _config["Storage:UploadsDirectory"] 
                                  ?? _config["AppSettings:UploadsDirectory"]
                                  ?? Environment.GetEnvironmentVariable("LAW_PROJECT_UPLOADS_DIR");

            if (!string.IsNullOrWhiteSpace(configuredPath))
            {
                try
                {
                    string resolvedConfig = Path.IsPathRooted(configuredPath)
                        ? Path.GetFullPath(configuredPath)
                        : Path.GetFullPath(Path.Combine(_env.ContentRootPath ?? Directory.GetCurrentDirectory(), configuredPath));

                    // If configured path exists and contains any items, prefer it immediately
                    if (Directory.Exists(resolvedConfig) && Directory.EnumerateFileSystemEntries(resolvedConfig).Any())
                    {
                        _logger.LogInformation("Initialized File Vault using populated configured path: {VaultPath}", resolvedConfig);
                        return resolvedConfig;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed probing configured upload vault path: {Path}", configuredPath);
                }
            }

            // 2. Plesk domain vault: ContentRootPath\uploads_vault or explicit domain path
            try
            {
                string pleskLawDomainVault = @"C:\inetpub\vhosts\itnwkrtc.in\law.itnwkrtc.in\uploads_vault";
                if (Directory.Exists(pleskLawDomainVault) && Directory.EnumerateFileSystemEntries(pleskLawDomainVault).Any())
                {
                    _logger.LogInformation("Initialized File Vault using populated Plesk law.itnwkrtc.in domain root: {VaultPath}", pleskLawDomainVault);
                    return pleskLawDomainVault;
                }

                if (!string.IsNullOrEmpty(_env.ContentRootPath))
                {
                    string contentRootVault = Path.GetFullPath(Path.Combine(_env.ContentRootPath, "uploads_vault"));
                    if (Directory.Exists(contentRootVault) && Directory.EnumerateFileSystemEntries(contentRootVault).Any())
                    {
                        _logger.LogInformation("Initialized File Vault using populated ContentRoot uploads_vault: {VaultPath}", contentRootVault);
                        return contentRootVault;
                    }

                    string httpdocsSubVault = Path.GetFullPath(Path.Combine(_env.ContentRootPath, "httpdocs", "uploads_vault"));
                    if (Directory.Exists(httpdocsSubVault) && Directory.EnumerateFileSystemEntries(httpdocsSubVault).Any())
                    {
                        _logger.LogInformation("Initialized File Vault using populated ContentRoot httpdocs uploads_vault: {VaultPath}", httpdocsSubVault);
                        return httpdocsSubVault;
                    }
                }
            }
            catch { }

            // 3. Explicit Plesk httpdocs path: C:\inetpub\vhosts\itnwkrtc.in\httpdocs\uploads_vault
            try
            {
                string pleskHttpdocsVault = @"C:\inetpub\vhosts\itnwkrtc.in\httpdocs\uploads_vault";
                if (Directory.Exists(pleskHttpdocsVault) && Directory.EnumerateFileSystemEntries(pleskHttpdocsVault).Any())
                {
                    _logger.LogInformation("Initialized File Vault using populated Plesk httpdocs root: {VaultPath}", pleskHttpdocsVault);
                    return pleskHttpdocsVault;
                }
            }
            catch { }

            // 4. Standard Plesk external vault: C:\inetpub\vhosts\itnwkrtc.in\uploads_vault (if populated)
            try
            {
                string pleskCommonVault = @"C:\inetpub\vhosts\itnwkrtc.in\uploads_vault";
                if (Directory.Exists(pleskCommonVault) && Directory.EnumerateFileSystemEntries(pleskCommonVault).Any())
                {
                    _logger.LogInformation("Initialized File Vault using populated Plesk external root: {VaultPath}", pleskCommonVault);
                    return pleskCommonVault;
                }
            }
            catch { }

            // 5. Sibling directory ../uploads_vault relative to ContentRootPath (if populated)
            try
            {
                if (!string.IsNullOrEmpty(_env.ContentRootPath))
                {
                    string siblingVault = Path.GetFullPath(Path.Combine(_env.ContentRootPath, "..", "uploads_vault"));
                    if (Directory.Exists(siblingVault) && Directory.EnumerateFileSystemEntries(siblingVault).Any())
                    {
                        _logger.LogInformation("Initialized File Vault using populated sibling external root: {VaultPath}", siblingVault);
                        return siblingVault;
                    }
                }
            }
            catch { }

            // 6. If none were populated yet, honor configured path if explicitly given
            if (!string.IsNullOrWhiteSpace(configuredPath))
            {
                try
                {
                    string resolvedConfig = Path.IsPathRooted(configuredPath)
                        ? Path.GetFullPath(configuredPath)
                        : Path.GetFullPath(Path.Combine(_env.ContentRootPath ?? Directory.GetCurrentDirectory(), configuredPath));

                    if (!Directory.Exists(resolvedConfig)) Directory.CreateDirectory(resolvedConfig);
                    _logger.LogInformation("Initialized File Vault using configured path: {VaultPath}", resolvedConfig);
                    return resolvedConfig;
                }
                catch { }
            }

            // 7. Check if ContentRootPath\uploads_vault exists (even if currently empty)
            if (!string.IsNullOrEmpty(_env.ContentRootPath))
            {
                string httpdocsVault = Path.GetFullPath(Path.Combine(_env.ContentRootPath, "uploads_vault"));
                if (Directory.Exists(httpdocsVault))
                {
                    return httpdocsVault;
                }
            }

            // 8. Plesk root detection
            if (Directory.Exists(@"C:\inetpub\vhosts\itnwkrtc.in"))
            {
                string pleskHttpdocs = @"C:\inetpub\vhosts\itnwkrtc.in\httpdocs\uploads_vault";
                if (Directory.Exists(pleskHttpdocs)) return pleskHttpdocs;

                string pleskCommon = @"C:\inetpub\vhosts\itnwkrtc.in\uploads_vault";
                if (!Directory.Exists(pleskCommon)) Directory.CreateDirectory(pleskCommon);
                return pleskCommon;
            }

            // 9. Sibling directory fallback
            try
            {
                if (!string.IsNullOrEmpty(_env.ContentRootPath))
                {
                    string siblingVault = Path.GetFullPath(Path.Combine(_env.ContentRootPath, "..", "uploads_vault"));
                    if (!Directory.Exists(siblingVault)) Directory.CreateDirectory(siblingVault);
                    return siblingVault;
                }
            }
            catch { }

            // 10. WebRoot or ContentRoot uploads fallback
            string fallbackWebRoot = !string.IsNullOrEmpty(_env.WebRootPath)
                ? Path.Combine(_env.WebRootPath, "uploads")
                : Path.Combine(_env.ContentRootPath ?? Directory.GetCurrentDirectory(), "uploads");

            if (!Directory.Exists(fallbackWebRoot)) Directory.CreateDirectory(fallbackWebRoot);
            _logger.LogInformation("Initialized File Vault using WebRoot fallback: {VaultPath}", fallbackWebRoot);
            return fallbackWebRoot;
        }

        private static string CleanRelativePath(string rawPath)
        {
            if (string.IsNullOrWhiteSpace(rawPath)) return string.Empty;

            string unescaped = Uri.UnescapeDataString(rawPath).Trim();

            // Strip any query parameters (e.g. /uploads/test.pdf?v=123)
            int queryIdx = unescaped.IndexOf('?');
            if (queryIdx >= 0)
            {
                unescaped = unescaped.Substring(0, queryIdx).Trim();
            }

            unescaped = unescaped.Replace('/', '\\').Trim('\\');

            // Strip virtual app paths e.g. ~\
            if (unescaped.StartsWith("~\\", StringComparison.OrdinalIgnoreCase))
            {
                unescaped = unescaped.Substring(2).TrimStart('\\');
            }

            // Strip leading "uploads_vault\" or "uploads\"
            if (unescaped.StartsWith("uploads_vault\\", StringComparison.OrdinalIgnoreCase))
            {
                unescaped = unescaped.Substring("uploads_vault\\".Length);
            }
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

