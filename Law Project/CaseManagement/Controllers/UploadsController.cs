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

namespace MVCCaseManagement.Controllers
{
    [Authorize]
    public class UploadsController : Controller
    {
        private readonly IWebHostEnvironment _environment;
        private readonly IConfiguration _configuration;
        private readonly ILogger<UploadsController> _logger;

        private static readonly string[] AllowedExtensions = new[]
        {
            ".pdf", ".jpg", ".jpeg", ".png", ".gif", ".doc", ".docx", ".txt", ".tiff", ".bmp"
        };

        public UploadsController(
            IWebHostEnvironment environment,
            IConfiguration configuration,
            ILogger<UploadsController> logger)
        {
            _environment = environment;
            _configuration = configuration;
            _logger = logger;
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

            // Check potential physical storage paths on disk with canonical boundary validation
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
                        return PhysicalFile(matchingFiles[0], GetContentType(matchingFiles[0]));
                    }
                }

                if (Directory.Exists(canonicalWebUploads))
                {
                    var matchingFiles = Directory.GetFiles(canonicalWebUploads, fileName, SearchOption.AllDirectories);
                    if (matchingFiles.Length > 0 && System.IO.File.Exists(matchingFiles[0]))
                    {
                        return PhysicalFile(matchingFiles[0], GetContentType(matchingFiles[0]));
                    }
                }
            }

            _logger.LogWarning("Requested upload file not found on disk: {FilePath}", filePath);

            // Query DB to see if this file was attached to a specific case
            ViewBag.FileName = fileName;
            ViewBag.RequestedPath = filePath;
            ViewBag.CaseInfo = null;
            ViewBag.CaseID = null;

            try
            {
                string connStr = _configuration.GetConnectionString("MVCCaseDB") ?? "";
                if (!string.IsNullOrEmpty(connStr))
                {
                    using (var conn = new SqlConnection(connStr))
                    {
                        conn.Open();
                        string sql = @"
                            SELECT TOP 1 a.CaseID, m.MVCNo, m.MVCYear, m.DivisionID 
                            FROM MVC_CASE_ADVERSE_DETAILS a
                            LEFT JOIN MVC_CASES m ON a.CaseID = m.CaseID
                            WHERE a.GovIDProofUploadPath LIKE '%' + @fileName + '%'
                               OR a.TR18UploadPath LIKE '%' + @fileName + '%'
                               OR a.PunishmentOrderUploadPath LIKE '%' + @fileName + '%'
                               OR a.SecurityUploadPath LIKE '%' + @fileName + '%'
                               OR a.AdverseJudgmentUploadPath LIKE '%' + @fileName + '%'
                               OR a.DelayApplicationPath LIKE '%' + @fileName + '%'
                               OR a.DelayCondonedOrderPath LIKE '%' + @fileName + '%'
                               OR a.AgeProofUploadPath LIKE '%' + @fileName + '%'";

                        using (var cmd = new SqlCommand(sql, conn))
                        {
                            cmd.Parameters.Add(new SqlParameter("@fileName", System.Data.SqlDbType.NVarChar, 255) { Value = fileName });
                            using (var r = cmd.ExecuteReader())
                            {
                                if (r.Read())
                                {
                                    int cId = r.GetInt32(0);
                                    string mvcNo = r.IsDBNull(1) ? "" : r.GetString(1);
                                    int mvcYr = r.IsDBNull(2) ? 0 : r.GetInt32(2);
                                    int caseDivId = r.IsDBNull(3) ? 0 : r.GetInt32(3);

                                    // Division ownership check
                                    var divIdString = User.FindFirstValue("DivisionID");
                                    int userDivId = int.TryParse(divIdString, out int pId) ? pId : 0;
                                    bool isCentral = userDivId == 0 || userDivId == 5 || User.IsInRole("Admin");

                                    if (isCentral || caseDivId == userDivId)
                                    {
                                        ViewBag.CaseID = cId;
                                        ViewBag.CaseInfo = $"MVC No. {mvcNo}/{mvcYr} (Case ID: {cId})";
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking DB for missing upload file info.");
            }

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
