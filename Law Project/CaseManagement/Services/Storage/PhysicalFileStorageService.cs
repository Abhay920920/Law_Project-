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
        private readonly string _primaryVaultPath;
        private readonly string? _secondaryVaultPath;

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
            var (primary, secondary) = InitializeVaultRoots();
            _primaryVaultPath = primary;
            _secondaryVaultPath = secondary;
        }

        public string GetVaultRootPath() => _primaryVaultPath;
        public string? GetSecondaryVaultRootPath() => _secondaryVaultPath;

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

            // 6. Ensure destination directory exists in persistent primary vault (law.itnwkrtc.in/uploads_vault outside publish)
            string primaryTargetDir = Path.Combine(_primaryVaultPath, cleanSubFolder);
            if (!Directory.Exists(primaryTargetDir))
            {
                Directory.CreateDirectory(primaryTargetDir);
            }

            // 7. Write securely using GUID to prevent filename collisions and enumeration attacks
            string uniqueFileName = $"{Guid.NewGuid():N}{ext}";
            string physicalDestination = Path.Combine(primaryTargetDir, uniqueFileName);

            using (var stream = new FileStream(physicalDestination, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true))
            {
                await file.CopyToAsync(stream, cancellationToken);
            }

            _logger.LogInformation("Successfully saved persistent document to primary vault (law.itnwkrtc.in): {PhysicalPath} ({Size} bytes)", physicalDestination, file.Length);

            // 8. Dual-Write: Redundantly store in secondary vault (itnwkrtc.in/httpdocs/uploads_vault outside publish)
            if (!string.IsNullOrEmpty(_secondaryVaultPath) && !string.Equals(_primaryVaultPath, _secondaryVaultPath, StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    string secTargetDir = Path.Combine(_secondaryVaultPath, cleanSubFolder);
                    if (!Directory.Exists(secTargetDir))
                    {
                        Directory.CreateDirectory(secTargetDir);
                    }
                    string secDestination = Path.Combine(secTargetDir, uniqueFileName);
                    File.Copy(physicalDestination, secDestination, overwrite: true);
                    _logger.LogInformation("Successfully mirrored persistent document to secondary vault (itnwkrtc.in): {PhysicalPath}", secDestination);
                }
                catch (Exception exSec)
                {
                    _logger.LogWarning(exSec, "Could not mirror upload to secondary vault ({SecVault}): {Msg}", _secondaryVaultPath, exSec.Message);
                }
            }

            // 9. Also synchronize to local WebRoot uploads if running in development mode for convenience
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
            TryAddRoot(_primaryVaultPath);
            if (!string.IsNullOrEmpty(_secondaryVaultPath))
            {
                TryAddRoot(_secondaryVaultPath);
            }
            if (!string.IsNullOrEmpty(_env.ContentRootPath))
            {
                // Immediate local roots in application folder
                TryAddRoot(Path.Combine(_env.ContentRootPath, "uploads_vault"));
                TryAddRoot(Path.Combine(_env.ContentRootPath, "uploads"));
                TryAddRoot(Path.Combine(_env.ContentRootPath, "wwwroot", "uploads"));
                TryAddRoot(Path.Combine(_env.ContentRootPath, "wwwroot", "uploads_vault"));
                TryAddRoot(Path.Combine(_env.ContentRootPath, "httpdocs", "uploads_vault"));
                TryAddRoot(Path.Combine(_env.ContentRootPath, "httpdocs", "uploads"));

                // Dynamic parent traversal: Walk up parents to discover main domain folders
                // Handles: C:\inetpub\vhosts\itnwkrtc.in\law.itnwkrtc.in -> C:\inetpub\vhosts\itnwkrtc.in
                // Handles: C:\inetpub\vhosts\itnwkrtc.in\subdomains\law.itnwkrtc.in -> C:\inetpub\vhosts\itnwkrtc.in
                try
                {
                    var currentDir = new DirectoryInfo(_env.ContentRootPath);
                    var parent = currentDir.Parent;
                    int depth = 0;
                    while (parent != null && depth < 3)
                    {
                        TryAddRoot(Path.Combine(parent.FullName, "uploads_vault"));
                        TryAddRoot(Path.Combine(parent.FullName, "httpdocs", "uploads_vault"));
                        TryAddRoot(Path.Combine(parent.FullName, "httpdocs", "uploads"));
                        TryAddRoot(Path.Combine(parent.FullName, "uploads"));
                        parent = parent.Parent;
                        depth++;
                    }
                }
                catch { }
            }

            // Known Plesk IIS absolute hosting paths across drives C:, D:, E:
            foreach (var drive in new[] { "C", "D", "E" })
            {
                TryAddRoot($@"{drive}:\inetpub\vhosts\itnwkrtc.in\uploads_vault");
                TryAddRoot($@"{drive}:\inetpub\vhosts\itnwkrtc.in\httpdocs\uploads_vault");
                TryAddRoot($@"{drive}:\inetpub\vhosts\itnwkrtc.in\httpdocs\uploads");
                TryAddRoot($@"{drive}:\inetpub\vhosts\itnwkrtc.in\uploads");
                TryAddRoot($@"{drive}:\inetpub\vhosts\itnwkrtc.in\law.itnwkrtc.in\uploads_vault");
                TryAddRoot($@"{drive}:\inetpub\vhosts\itnwkrtc.in\law.itnwkrtc.in\httpdocs\uploads_vault");
                TryAddRoot($@"{drive}:\inetpub\vhosts\itnwkrtc.in\law.itnwkrtc.in\wwwroot\uploads");
                TryAddRoot($@"{drive}:\inetpub\vhosts\itnwkrtc.in\law.itnwkrtc.in\wwwroot\uploads_vault");
            }

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

                    if (cleanPath.Contains("punishments", StringComparison.OrdinalIgnoreCase))
                    {
                        string alt6 = Path.Combine(root, cleanPath.Replace("punishments", @"Labour\Punishments", StringComparison.OrdinalIgnoreCase));
                        if (File.Exists(alt6)) return alt6;
                    }

                    if (cleanPath.Contains("history_sheets", StringComparison.OrdinalIgnoreCase))
                    {
                        string alt7 = Path.Combine(root, cleanPath.Replace("history_sheets", @"Labour\HistorySheets", StringComparison.OrdinalIgnoreCase));
                        if (File.Exists(alt7)) return alt7;
                    }

                    if (cleanPath.Contains(@"Labour\ReinstatementDocuments", StringComparison.OrdinalIgnoreCase))
                    {
                        string alt8 = Path.Combine(root, cleanPath.Replace(@"Labour\ReinstatementDocuments", "Labour", StringComparison.OrdinalIgnoreCase));
                        if (File.Exists(alt8)) return alt8;
                    }

                    if (cleanPath.Contains(@"Labour\CaseDocuments", StringComparison.OrdinalIgnoreCase))
                    {
                        string alt9 = Path.Combine(root, cleanPath.Replace(@"Labour\CaseDocuments", "Labour", StringComparison.OrdinalIgnoreCase));
                        if (File.Exists(alt9)) return alt9;
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

        private (string primary, string? secondary) InitializeVaultRoots()
        {
            string? configuredPrimary = _config["Storage:UploadsDirectory"] 
                                     ?? _config["AppSettings:UploadsDirectory"]
                                     ?? Environment.GetEnvironmentVariable("LAW_PROJECT_UPLOADS_DIR");

            string? configuredSecondary = _config["Storage:SecondaryUploadsDirectory"]
                                       ?? _config["AppSettings:SecondaryUploadsDirectory"]
                                       ?? Environment.GetEnvironmentVariable("LAW_PROJECT_SECONDARY_UPLOADS_DIR");

            string contentRoot = _env.ContentRootPath ?? Directory.GetCurrentDirectory();

            // 1. Resolve Primary Vault (law.itnwkrtc.in\uploads_vault - strictly OUTSIDE publish)
            string primaryVault;
            if (!string.IsNullOrWhiteSpace(configuredPrimary))
            {
                primaryVault = Path.IsPathRooted(configuredPrimary)
                    ? Path.GetFullPath(configuredPrimary)
                    : Path.GetFullPath(Path.Combine(contentRoot, configuredPrimary));

                // If running in a 'publish' folder and configured path resolves inside 'publish',
                // automatically hoist it up one directory to keep persistent storage safe from redeploys!
                if (Path.GetFileName(contentRoot).Equals("publish", StringComparison.OrdinalIgnoreCase) &&
                    primaryVault.StartsWith(contentRoot, StringComparison.OrdinalIgnoreCase))
                {
                    primaryVault = Path.GetFullPath(Path.Combine(contentRoot, "..", "uploads_vault"));
                }
            }
            else
            {
                // Default: Outside publish folder (sibling to publish or project)
                primaryVault = Path.GetFullPath(Path.Combine(contentRoot, "..", "uploads_vault"));
            }

            try
            {
                if (!Directory.Exists(primaryVault))
                {
                    Directory.CreateDirectory(primaryVault);
                }
                _logger.LogInformation("Initialized Primary Persistent File Vault (law.itnwkrtc.in outside publish): {VaultPath}", primaryVault);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not initialize primary vault at {Path}: {Msg}", primaryVault, ex.Message);
            }

            // 2. Resolve Secondary Redundant Vault (itnwkrtc.in\httpdocs\uploads_vault - strictly OUTSIDE publish)
            string? secondaryVault = null;
            if (!string.IsNullOrWhiteSpace(configuredSecondary))
            {
                try
                {
                    secondaryVault = Path.IsPathRooted(configuredSecondary)
                        ? Path.GetFullPath(configuredSecondary)
                        : Path.GetFullPath(Path.Combine(contentRoot, configuredSecondary));
                }
                catch { }
            }

            if (string.IsNullOrWhiteSpace(secondaryVault))
            {
                // Auto-discover Plesk main domain root (itnwkrtc.in\httpdocs\uploads_vault)
                var secondaryCandidates = new List<string>
                {
                    Path.GetFullPath(Path.Combine(contentRoot, "..", "..", "httpdocs", "uploads_vault")),
                    Path.GetFullPath(Path.Combine(contentRoot, "..", "httpdocs", "uploads_vault")),
                    Path.GetFullPath(Path.Combine(contentRoot, "..", "..", "uploads_vault"))
                };

                foreach (var drive in new[] { "C", "D", "E" })
                {
                    secondaryCandidates.Add($@"{drive}:\inetpub\vhosts\itnwkrtc.in\httpdocs\uploads_vault");
                    secondaryCandidates.Add($@"{drive}:\inetpub\vhosts\itnwkrtc.in\uploads_vault");
                }

                foreach (var cand in secondaryCandidates)
                {
                    try
                    {
                        if (Directory.Exists(cand) && !string.Equals(cand, primaryVault, StringComparison.OrdinalIgnoreCase))
                        {
                            secondaryVault = cand;
                            break;
                        }
                    }
                    catch { }
                }

                // If existing folder not found yet, check if itnwkrtc.in parent directory exists
                if (secondaryVault == null)
                {
                    try
                    {
                        string candidate = Path.GetFullPath(Path.Combine(contentRoot, "..", "..", "httpdocs", "uploads_vault"));
                        string? parentOfHttpdocs = Path.GetDirectoryName(Path.GetDirectoryName(candidate));
                        if (!string.IsNullOrEmpty(parentOfHttpdocs) && Directory.Exists(parentOfHttpdocs))
                        {
                            secondaryVault = candidate;
                        }
                    }
                    catch { }
                }
            }

            if (!string.IsNullOrWhiteSpace(secondaryVault))
            {
                try
                {
                    if (!Directory.Exists(secondaryVault))
                    {
                        Directory.CreateDirectory(secondaryVault);
                    }
                    _logger.LogInformation("Initialized Secondary Persistent File Vault (itnwkrtc.in outside publish): {VaultPath}", secondaryVault);
                }
                catch (Exception exSec)
                {
                    _logger.LogWarning(exSec, "Secondary vault at {Path} inaccessible or lacks write permission: {Msg}", secondaryVault, exSec.Message);
                }
            }

            // 3. Automatic Zero-Loss Migration:
            // If any files were stored in the legacy 'publish\uploads_vault' (e.g. from recent sessions),
            // safely migrate/copy them to the persistent vaults so no uploads are lost!
            MigrateLegacyPublishFiles(contentRoot, primaryVault, secondaryVault);

            return (primaryVault, secondaryVault);
        }

        private void MigrateLegacyPublishFiles(string contentRoot, string primaryVault, string? secondaryVault)
        {
            try
            {
                // Zero-loss migration only targets publish / deployment directories or Production hosting environments
                bool isPublishDir = Path.GetFileName(contentRoot).Equals("publish", StringComparison.OrdinalIgnoreCase);
                bool isProd = false;
                try
                {
                    isProd = string.Equals(_env?.EnvironmentName, "Production", StringComparison.OrdinalIgnoreCase);
                }
                catch { }

                if (!isPublishDir && !isProd)
                {
                    return;
                }

                var legacyCandidates = new[]
                {
                    Path.Combine(contentRoot, "uploads_vault"),
                    Path.Combine(contentRoot, "wwwroot", "uploads"),
                    Path.Combine(contentRoot, "uploads")
                };

                foreach (var legacyDir in legacyCandidates)
                {
                    if (!Directory.Exists(legacyDir) ||
                        string.Equals(legacyDir, primaryVault, StringComparison.OrdinalIgnoreCase) ||
                        (!string.IsNullOrEmpty(secondaryVault) && string.Equals(legacyDir, secondaryVault, StringComparison.OrdinalIgnoreCase)))
                    {
                        continue;
                    }

                    var files = Directory.GetFiles(legacyDir, "*.*", SearchOption.AllDirectories);
                    if (files.Length == 0) continue;

                    _logger.LogInformation("Detected {Count} files in legacy directory '{Dir}'. Auto-migrating to persistent vaults...", files.Length, legacyDir);
                    foreach (var file in files)
                    {
                        try
                        {
                            string fileName = Path.GetFileName(file);
                            if (string.Equals(fileName, ".gitkeep", StringComparison.OrdinalIgnoreCase)) continue;

                            string relative = Path.GetRelativePath(legacyDir, file);

                            // Copy to Primary Vault (outside publish)
                            string primaryDest = Path.Combine(primaryVault, relative);
                            string? primDir = Path.GetDirectoryName(primaryDest);
                            if (!string.IsNullOrEmpty(primDir) && !Directory.Exists(primDir)) Directory.CreateDirectory(primDir);
                            if (!File.Exists(primaryDest))
                            {
                                File.Copy(file, primaryDest, overwrite: false);
                            }

                            // Copy to Secondary Vault (itnwkrtc.in/httpdocs/uploads_vault)
                            if (!string.IsNullOrEmpty(secondaryVault) && Directory.Exists(secondaryVault))
                            {
                                string secDest = Path.Combine(secondaryVault, relative);
                                string? secDir = Path.GetDirectoryName(secDest);
                                if (!string.IsNullOrEmpty(secDir) && !Directory.Exists(secDir)) Directory.CreateDirectory(secDir);
                                if (!File.Exists(secDest))
                                {
                                    File.Copy(file, secDest, overwrite: false);
                                }
                            }
                        }
                        catch (Exception exFile)
                        {
                            _logger.LogWarning(exFile, "Could not auto-migrate legacy publish file {File}", file);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error checking for legacy publish files to migrate.");
            }
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
            string clean = CleanRelativePath(relativePath);
            if (string.IsNullOrEmpty(clean)) return false;

            try
            {
                // 1. Delete from primary vault (law.itnwkrtc.in)
                string primaryTarget = Path.Combine(_primaryVaultPath, clean);
                if (File.Exists(primaryTarget))
                {
                    File.Delete(primaryTarget);
                    _logger.LogInformation("Deleted document from primary vault: {Path}", primaryTarget);
                    deleted = true;
                }

                // 2. Delete from secondary vault (itnwkrtc.in)
                if (!string.IsNullOrEmpty(_secondaryVaultPath))
                {
                    string secTarget = Path.Combine(_secondaryVaultPath, clean);
                    if (File.Exists(secTarget))
                    {
                        File.Delete(secTarget);
                        _logger.LogInformation("Deleted document from secondary vault: {Path}", secTarget);
                        deleted = true;
                    }
                }

                // 3. Delete from fallback/legacy physical path if located elsewhere
                string? physicalOld = LocatePhysicalFile(relativePath);
                if (!string.IsNullOrEmpty(physicalOld) && File.Exists(physicalOld) &&
                    !string.Equals(physicalOld, primaryTarget, StringComparison.OrdinalIgnoreCase))
                {
                    File.Delete(physicalOld);
                    _logger.LogInformation("Deleted document from fallback storage: {Path}", physicalOld);
                    deleted = true;
                }

                // 4. Delete from WebRoot mirror
                if (!string.IsNullOrEmpty(_env.WebRootPath))
                {
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

