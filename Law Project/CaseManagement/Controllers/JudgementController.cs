using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MVCCaseManagement.DAL;
using MVCCaseManagement.Models;
using System.Security.Claims;
using MVCCaseManagement.Services.Audit;

namespace MVCCaseManagement.Controllers
{
    [Authorize]
    public class JudgementController : Controller
    {
        private readonly IJudgementRepository _repo;
        private readonly IWebHostEnvironment _environment;
        private readonly ICaseActivityLogger _activityLogger;

        public JudgementController(IJudgementRepository repo, IWebHostEnvironment environment, ICaseActivityLogger activityLogger)
        {
            _repo = repo;
            _environment = environment;
            _activityLogger = activityLogger;
        }

        public IActionResult Index()
        {
            var list = _repo.GetAllJudgements();
            return View(list);
        }

        [HttpGet]
        public IActionResult Create()
        {
            if (!IsAuthorizedToUpload())
            {
                return RedirectToAction("Index");
            }
            return View(new JudgementViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(JudgementViewModel model)
        {
            if (!IsAuthorizedToUpload())
            {
                return RedirectToAction("Index");
            }

            if (ModelState.IsValid)
            {
                if (model.JudgementFile != null)
                {
                    model.FilePath = SaveFile(model.JudgementFile);
                }

                model.UploadedBy = User.FindFirstValue("FullName");
                _repo.SaveJudgement(model);

                _ = _activityLogger.LogSubEntityActionAsync(
                    "JUDGEMENT",
                    model.JudgementID,
                    model.Title ?? "Judgement",
                    "JUDGEMENT_CREATED",
                    $"Uploaded / Created judgement '{model.Title}' (Category: {model.Category ?? "Judgement"}, Court: {model.Court ?? "N/A"})",
                    model);

                return RedirectToAction("Index");
            }
            return View(model);
        }

        [HttpGet]
        public IActionResult Edit(int id)
        {
            if (!IsAuthorizedToUpload())
            {
                return RedirectToAction("Index");
            }

            var judgement = _repo.GetJudgementById(id);
            if (judgement == null)
            {
                TempData["ErrorMessage"] = "Judgement record not found.";
                return RedirectToAction("Index");
            }

            return View(judgement);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Edit(JudgementViewModel model, bool removeFile = false)
        {
            if (!IsAuthorizedToUpload())
            {
                return RedirectToAction("Index");
            }

            var existing = _repo.GetJudgementById(model.JudgementID);
            if (existing == null)
            {
                TempData["ErrorMessage"] = "Judgement record not found.";
                return RedirectToAction("Index");
            }

            if (ModelState.IsValid)
            {
                if (model.JudgementFile != null && model.JudgementFile.Length > 0)
                {
                    // Save replacement file
                    var newFilePath = SaveFile(model.JudgementFile);

                    // Delete old file if present
                    if (!string.IsNullOrEmpty(existing.FilePath) && existing.FilePath != newFilePath)
                    {
                        var relativePath = existing.FilePath.TrimStart('/');
                        var fullPath = Path.Combine(_environment.WebRootPath, relativePath.Replace("/", Path.DirectorySeparatorChar.ToString()));
                        if (System.IO.File.Exists(fullPath))
                        {
                            try { System.IO.File.Delete(fullPath); } catch { }
                        }
                    }

                    model.FilePath = newFilePath;
                }
                else if (removeFile)
                {
                    // User explicitly requested to remove the attached file
                    if (!string.IsNullOrEmpty(existing.FilePath))
                    {
                        var relativePath = existing.FilePath.TrimStart('/');
                        var fullPath = Path.Combine(_environment.WebRootPath, relativePath.Replace("/", Path.DirectorySeparatorChar.ToString()));
                        if (System.IO.File.Exists(fullPath))
                        {
                            try { System.IO.File.Delete(fullPath); } catch { }
                        }
                    }
                    model.FilePath = null;
                }
                else
                {
                    // Retain existing file
                    model.FilePath = existing.FilePath;
                }

                model.UploadedBy = existing.UploadedBy ?? User.FindFirstValue("FullName");
                _repo.UpdateJudgement(model);

                _ = _activityLogger.LogSubEntityActionAsync(
                    "JUDGEMENT",
                    model.JudgementID,
                    model.Title ?? "Judgement",
                    "JUDGEMENT_UPDATED",
                    $"Updated judgement '{model.Title}' (Category: {model.Category ?? "Judgement"}, Court: {model.Court ?? "N/A"})",
                    model);

                TempData["SuccessMessage"] = "Judgement updated successfully.";
                return RedirectToAction("Index");
            }

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Delete(int id)
        {
            if (!IsAuthorizedToUpload())
            {
                return RedirectToAction("Index");
            }

            var judgement = _repo.GetJudgementById(id);
            if (judgement != null)
            {
                // Delete physical file if exists
                if (!string.IsNullOrEmpty(judgement.FilePath))
                {
                    // Convert relative path to absolute path
                    var relativePath = judgement.FilePath.TrimStart('/');
                    var fullPath = Path.Combine(_environment.WebRootPath, relativePath.Replace("/", Path.DirectorySeparatorChar.ToString()));
                    
                    if (System.IO.File.Exists(fullPath))
                    {
                        System.IO.File.Delete(fullPath);
                    }
                }

                _repo.DeleteJudgement(id);

                _ = _activityLogger.LogSubEntityActionAsync(
                    "JUDGEMENT",
                    id,
                    judgement.Title ?? $"Judgement #{id}",
                    "JUDGEMENT_DELETED",
                    $"Deleted judgement '{judgement.Title}' (#{id})",
                    judgement);

                TempData["SuccessMessage"] = "Judgement deleted successfully.";
            }

            return RedirectToAction("Index");
        }

        private bool IsAuthorizedToUpload()
        {
            var divId = User.FindFirst("DivisionID")?.Value;
            return divId == "5" || divId == "0";
        }

        private static readonly HashSet<string> _allowedExtensions =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { ".pdf", ".doc", ".docx", ".jpg", ".jpeg", ".png" };

        private string? SaveFile(IFormFile file)
        {
            if (file == null || file.Length == 0) return null;

            string ext = Path.GetExtension(file.FileName);
            if (!_allowedExtensions.Contains(ext))
                throw new InvalidOperationException(
                    $"File type '{ext}' is not allowed. Only PDF, Word, and image files are permitted.");

            const long maxBytes = 20 * 1024 * 1024;
            if (file.Length > maxBytes)
                throw new InvalidOperationException("File size exceeds the 20 MB limit.");

            var uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads", "judgments");
            if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

            var uniqueFileName = $"{Guid.NewGuid()}{ext}";
            var filePath = Path.Combine(uploadsFolder, uniqueFileName);

            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                file.CopyTo(fileStream);
            }

            return $"/uploads/judgments/{uniqueFileName}";
        }
    }
}
