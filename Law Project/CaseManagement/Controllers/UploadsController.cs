using System;
using System.IO;
using System.Linq;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Data.SqlClient;
using MVCCaseManagement.Services.Storage;

namespace MVCCaseManagement.Controllers
{
    [Authorize]
    public class UploadsController : Controller
    {
        private readonly IWebHostEnvironment _environment;
        private readonly IConfiguration _configuration;
        private readonly ILogger<UploadsController> _logger;
        private readonly IFileStorageService _fileStorageService;

        private static readonly string[] AllowedExtensions = new[]
        {
            ".pdf", ".jpg", ".jpeg", ".png", ".gif", ".doc", ".docx", ".txt", ".tiff", ".bmp"
        };

        public UploadsController(
            IWebHostEnvironment environment,
            IConfiguration configuration,
            ILogger<UploadsController> logger,
            IFileStorageService fileStorageService)
        {
            _environment = environment;
            _configuration = configuration;
            _logger = logger;
            _fileStorageService = fileStorageService;
        }

        [Route("uploads/{*filePath}")]
        public IActionResult GetUploadFile(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return NotFound();
            }

            // Decode URL characters (e.g. %20 -> space)
            string decodedPath = Uri.UnescapeDataString(filePath).TrimStart('/', '\\');

            // Security prevention against path traversal
            if (decodedPath.Contains("..") || Path.IsPathRooted(decodedPath))
            {
                return BadRequest("Invalid file path.");
            }

            string ext = Path.GetExtension(decodedPath).ToLowerInvariant();
            if (!AllowedExtensions.Contains(ext))
            {
                return BadRequest("File extension not permitted.");
            }

            string fileName = Path.GetFileName(decodedPath);
            string canonicalContentUploads = Path.GetFullPath(Path.Combine(_environment.ContentRootPath, "uploads"));
            string canonicalWebUploads = Path.GetFullPath(Path.Combine(_environment.WebRootPath, "uploads"));

            // 1. Enforce Division Ownership and Cross-Tenant Access Control (IDOR Prevention)
            var divIdString = User.FindFirstValue("DivisionID");
            int userDivId = int.TryParse(divIdString, out int pId) ? pId : 0;
            bool isCentral = string.IsNullOrEmpty(divIdString) || userDivId == 0 || userDivId == 5 
                             || User.IsInRole("Admin") || User.IsInRole("CLO") || User.IsInRole("Dy CLO") || User.IsInRole("MD");

            int attachedCaseId = 0;
            int attachedCaseDivId = 0;
            bool isSharedDoc = false;
            string attachedMvcNo = "";
            int attachedMvcYr = 0;

            try
            {
                string connStr = _configuration.GetConnectionString("MVCCaseDB") ?? "";
                if (!string.IsNullOrEmpty(connStr))
                {
                    using (var conn = new SqlConnection(connStr))
                    {
                        conn.Open();
                        string sql = @"
                            SELECT TOP 1 DivisionID, IsShared FROM (
                                -- 1. MVC Case Adverse Details
                                SELECT a.CaseID, ISNULL(m.DivisionID, 0) AS DivisionID, 0 AS IsShared
                                FROM MVC_CASE_ADVERSE_DETAILS a
                                INNER JOIN MVC_CASES m ON a.CaseID = m.CaseID
                                WHERE a.GovIDProofUploadPath LIKE '%' + @fileName + '%'
                                   OR a.TR18UploadPath LIKE '%' + @fileName + '%'
                                   OR a.PunishmentOrderUploadPath LIKE '%' + @fileName + '%'
                                   OR a.SecurityUploadPath LIKE '%' + @fileName + '%'
                                   OR a.AdverseJudgmentUploadPath LIKE '%' + @fileName + '%'
                                   OR a.DelayApplicationPath LIKE '%' + @fileName + '%'
                                   OR a.DelayCondonedOrderPath LIKE '%' + @fileName + '%'
                                   OR a.AgeProofUploadPath LIKE '%' + @fileName + '%'

                                UNION ALL

                                -- 2. MVC Direct Cases
                                SELECT CaseID, ISNULL(DivisionID, 0) AS DivisionID, 0 AS IsShared
                                FROM MVC_CASES
                                WHERE InterimOrderFilePath LIKE '%' + @fileName + '%'
                                   OR FavorJudgmentPath LIKE '%' + @fileName + '%'

                                UNION ALL

                                -- 3. MVC High Court Appeals
                                SELECT ap.CaseID, ISNULL(m.DivisionID, 0) AS DivisionID, 0 AS IsShared
                                FROM APPEAL_DETAILS ap
                                INNER JOIN MVC_CASES m ON ap.CaseID = m.CaseID
                                WHERE ap.ApprovalCopyPath LIKE '%' + @fileName + '%'
                                   OR ap.InitialActionPath1 LIKE '%' + @fileName + '%'
                                   OR ap.InitialActionPath2 LIKE '%' + @fileName + '%'
                                   OR ap.StayOrderPath1 LIKE '%' + @fileName + '%'
                                   OR ap.StayOrderPath2 LIKE '%' + @fileName + '%'
                                   OR ap.MFAJudgmentCopyPath LIKE '%' + @fileName + '%'
                                   OR ap.ComplianceLetterPath LIKE '%' + @fileName + '%'
                                   OR ap.CorpMFAActionTakenPath LIKE '%' + @fileName + '%'
                                   OR ap.ClaimantSCJudgmentPath LIKE '%' + @fileName + '%'

                                UNION ALL

                                -- 4. Labour Court Cases
                                SELECT CaseID, ISNULL(DivisionID, 0) AS DivisionID, 0 AS IsShared
                                FROM LABOUR_CASES
                                WHERE Against_PunishmentCopyPath LIKE '%' + @fileName + '%'
                                   OR ClaimPetitionPath LIKE '%' + @fileName + '%'
                                   OR JudgmentCopyPath LIKE '%' + @fileName + '%'
                                   OR JudgmentCopyPath2 LIKE '%' + @fileName + '%'
                                   OR LokAdalatDocumentPath LIKE '%' + @fileName + '%'
                                   OR CO_WP_JudgmentCopyPath LIKE '%' + @fileName + '%'
                                   OR CO_WA_StayOrderPath LIKE '%' + @fileName + '%'
                                   OR CO_WP_StayOrderPath LIKE '%' + @fileName + '%'
                                   OR CO_StayComplianceFilePath LIKE '%' + @fileName + '%'
                                   OR CO_ClosedDocumentPath LIKE '%' + @fileName + '%'
                                   OR DE_HistorySheetPath LIKE '%' + @fileName + '%'
                                   OR CO_Claimant_PetitionCopyPath LIKE '%' + @fileName + '%'
                                   OR CO_Service_PetitionCopyPath LIKE '%' + @fileName + '%'
                                   OR CO_Service_ApprovalCopyPath LIKE '%' + @fileName + '%'
                                   OR CO_Reinstatement_StayOrderPath LIKE '%' + @fileName + '%'
                                   OR CO_Reinstatement_ApprovalCopyPath LIKE '%' + @fileName + '%'

                                UNION ALL

                                -- 4b. Labour Reinstated / User Uploaded Documents
                                SELECT r.CaseID, ISNULL(lc.DivisionID, 0) AS DivisionID, 0 AS IsShared
                                FROM LABOUR_REINSTATED_DOCUMENTS r
                                INNER JOIN LABOUR_CASES lc ON r.CaseID = lc.CaseID
                                WHERE r.DocumentPath LIKE '%' + @fileName + '%'

                                UNION ALL

                                -- 5. Labour Arising Applications (Section 33-C(2))
                                SELECT ArisingID AS CaseID, ISNULL(DivisionID, 0) AS DivisionID, 0 AS IsShared
                                FROM LABOUR_ARISING_APPLICATIONS
                                WHERE JudgmentCopyPath LIKE '%' + @fileName + '%'
                                   OR LokAdalatDocumentPath LIKE '%' + @fileName + '%'
                                   OR CO_WP_JudgmentCopyPath LIKE '%' + @fileName + '%'
                                   OR ClaimPetitionPath LIKE '%' + @fileName + '%'
                                   OR DE_HistorySheetPath LIKE '%' + @fileName + '%'

                                UNION ALL

                                -- 6. Gratuity Claims (CA / AA)
                                SELECT CaseID, ISNULL(DivisionCode, 0) AS DivisionID, 0 AS IsShared
                                FROM GRA_CASES
                                WHERE AdverseJudgmentPath LIKE '%' + @fileName + '%'
                                   OR AppealJudgmentPath LIKE '%' + @fileName + '%'
                                   OR InitialActionPath1 LIKE '%' + @fileName + '%'
                                   OR InitialActionPath2 LIKE '%' + @fileName + '%'
                                   OR StayOrderPath1 LIKE '%' + @fileName + '%'
                                   OR StayOrderPath2 LIKE '%' + @fileName + '%'

                                UNION ALL

                                -- 7. Other Courts
                                SELECT CaseID, ISNULL(DivisionID, 0) AS DivisionID, 0 AS IsShared
                                FROM OTHER_CASES
                                WHERE InterimOrderFilePath LIKE '%' + @fileName + '%'
                                   OR FinalJudgmentFilePath LIKE '%' + @fileName + '%'
                                   OR AppealJudgmentCopyPath LIKE '%' + @fileName + '%'

                                UNION ALL

                                -- 8. Organization Judgments / Circulars (Shared Corp-Wide)
                                SELECT JudgementID AS CaseID, 0 AS DivisionID, 1 AS IsShared
                                FROM JUDGEMENT_REPO
                                WHERE FilePath LIKE '%' + @fileName + '%'

                                UNION ALL

                                -- 9. e-Courts Orders (Shared Corp-Wide)
                                SELECT OrderID AS CaseID, 0 AS DivisionID, 1 AS IsShared
                                FROM CASE_ORDERS
                                WHERE LocalPdfPath LIKE '%' + @fileName + '%'
                            ) AS AllAttachments";

                        using (var cmd = new SqlCommand(sql, conn))
                        {
                            cmd.Parameters.Add(new SqlParameter("@fileName", System.Data.SqlDbType.NVarChar, 255) { Value = fileName });
                            using (var r = cmd.ExecuteReader())
                            {
                                if (r.Read())
                                {
                                    attachedCaseDivId = r.IsDBNull(0) ? 0 : Convert.ToInt32(r.GetValue(0));
                                    isSharedDoc = !r.IsDBNull(1) && Convert.ToInt32(r.GetValue(1)) == 1;
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking DB for upload file division ownership for {FileName}", fileName);
            }

            // Zero-trust enforcement: Non-central office users can only download documents verified to belong to their assigned division
            if (!isCentral)
            {
                // Corporation-wide shared documents (precedent circulars, e-Courts cause lists) are accessible by all authenticated users
                if (isSharedDoc
                    || decodedPath.StartsWith("judgments/", StringComparison.OrdinalIgnoreCase)
                    || decodedPath.StartsWith("judgements/", StringComparison.OrdinalIgnoreCase)
                    || decodedPath.StartsWith("cases/", StringComparison.OrdinalIgnoreCase))
                {
                    // Allowed
                }
                else if (attachedCaseDivId > 0 && attachedCaseDivId != userDivId)
                {
                    _logger.LogWarning("Cross-tenant download forbidden: User Division {UserDiv} tried accessing document {FileName} (Resolved Division: {CaseDiv})", userDivId, fileName, attachedCaseDivId);
                    return Forbid();
                }
            }

            // 2. Locate and stream physical file: Check persistent storage vault first via IFileStorageService
            string? vaultPhysicalPath = _fileStorageService.LocatePhysicalFile(decodedPath);
            if (!string.IsNullOrEmpty(vaultPhysicalPath) && System.IO.File.Exists(vaultPhysicalPath))
            {
                Response.Headers.Append("Content-Disposition", $"inline; filename=\"{fileName}\"");
                return PhysicalFile(vaultPhysicalPath, GetContentType(vaultPhysicalPath));
            }

            // 3. Fallback: Locate physical file from legacy canonical boundaries
            var candidatePaths = new[]
            {
                Path.Combine(_environment.ContentRootPath, "uploads", decodedPath),
                Path.Combine(_environment.WebRootPath, "uploads", decodedPath),
                Path.Combine(_environment.ContentRootPath, "uploads", fileName),
                Path.Combine(_environment.WebRootPath, "uploads", fileName)
            };

            foreach (var candidate in candidatePaths)
            {
                string fullCandidate = Path.GetFullPath(candidate);
                if (fullCandidate.StartsWith(canonicalContentUploads, StringComparison.OrdinalIgnoreCase) ||
                    fullCandidate.StartsWith(canonicalWebUploads, StringComparison.OrdinalIgnoreCase))
                {
                    if (System.IO.File.Exists(fullCandidate))
                    {
                        Response.Headers.Append("Content-Disposition", $"inline; filename=\"{fileName}\"");
                        return PhysicalFile(fullCandidate, GetContentType(fullCandidate));
                    }
                }
            }

            // Deep search inside uploads root directory only for full filenames (minimum 6 characters)
            if (fileName.Length >= 6)
            {
                if (Directory.Exists(canonicalContentUploads))
                {
                    var matchingFiles = Directory.GetFiles(canonicalContentUploads, fileName, SearchOption.AllDirectories);
                    if (matchingFiles.Length > 0 && System.IO.File.Exists(matchingFiles[0]))
                    {
                        Response.Headers.Append("Content-Disposition", $"inline; filename=\"{fileName}\"");
                        return PhysicalFile(matchingFiles[0], GetContentType(matchingFiles[0]));
                    }
                }

                if (Directory.Exists(canonicalWebUploads))
                {
                    var matchingFiles = Directory.GetFiles(canonicalWebUploads, fileName, SearchOption.AllDirectories);
                    if (matchingFiles.Length > 0 && System.IO.File.Exists(matchingFiles[0]))
                    {
                        Response.Headers.Append("Content-Disposition", $"inline; filename=\"{fileName}\"");
                        return PhysicalFile(matchingFiles[0], GetContentType(matchingFiles[0]));
                    }
                }
            }

            _logger.LogWarning("Requested upload file not found on disk: {FilePath}", filePath);

            ViewBag.FileName = fileName;
            ViewBag.RequestedPath = filePath;
            ViewBag.CaseInfo = attachedCaseId > 0 ? $"MVC No. {attachedMvcNo}/{attachedMvcYr} (Case ID: {attachedCaseId})" : null;
            ViewBag.CaseID = attachedCaseId > 0 ? (int?)attachedCaseId : null;

            return View("~/Views/Shared/FileNotFound.cshtml");
        }

        private static string GetContentType(string path)
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            return ext switch
            {
                ".pdf" => "application/pdf",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".gif" => "image/gif",
                ".doc" => "application/msword",
                ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                ".txt" => "text/plain",
                _ => "application/octet-stream"
            };
        }
    }
}
