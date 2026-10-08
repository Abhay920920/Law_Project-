using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using UglyToad.PdfPig;
using MVCCaseManagement.DAL;
using MVCCaseManagement.Models.AI;

namespace MVCCaseManagement.Services.AI
{
    public class DocumentSearchService : IDocumentSearchService
    {
        private readonly IDocumentTextExtractor _extractor;
        private readonly ICaseRepository _caseRepo;
        private readonly ILabourRepository _labourRepo;
        private readonly IAppealRepository _appealRepo;
        private readonly IJudgementRepository _judgementRepo;
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<DocumentSearchService> _logger;

        // In-memory cache for parsed PDF pages by relative file path to avoid redundant disk I/O
        private static readonly ConcurrentDictionary<string, (DateTime CachedAt, List<DocumentPageExtractDto> Pages)> PageCache = new(StringComparer.OrdinalIgnoreCase);

        public DocumentSearchService(
            IDocumentTextExtractor extractor,
            ICaseRepository caseRepo,
            ILabourRepository labourRepo,
            IAppealRepository appealRepo,
            IJudgementRepository judgementRepo,
            IWebHostEnvironment environment,
            ILogger<DocumentSearchService> logger)
        {
            _extractor = extractor ?? throw new ArgumentNullException(nameof(extractor));
            _caseRepo = caseRepo ?? throw new ArgumentNullException(nameof(caseRepo));
            _labourRepo = labourRepo ?? throw new ArgumentNullException(nameof(labourRepo));
            _appealRepo = appealRepo ?? throw new ArgumentNullException(nameof(appealRepo));
            _judgementRepo = judgementRepo ?? throw new ArgumentNullException(nameof(judgementRepo));
            _environment = environment ?? throw new ArgumentNullException(nameof(environment));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<List<DocumentSearchResultDto>> SearchDocumentsAsync(
            string query,
            string? caseType = null,
            int? caseId = null,
            int maxResults = 5,
            CancellationToken cancellationToken = default)
        {
            var results = new List<DocumentSearchResultDto>();
            if (string.IsNullOrWhiteSpace(query))
                return results;

            // 1. Discover relevant document references from repositories
            var candidateDocs = DiscoverCandidateDocuments(caseType, caseId);

            var queryTerms = query.Split(new[] { ' ', ',', ';', '/', '-' }, StringSplitOptions.RemoveEmptyEntries)
                                  .Where(t => t.Length >= 3 && !IsCommonStopword(t))
                                  .ToList();
            if (!queryTerms.Any()) queryTerms.Add(query.Trim());

            foreach (var docRef in candidateDocs)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var pages = await ExtractPagesAsync(docRef.RelativePath, docRef.CaseType, docRef.CaseId, maxPages: 20, cancellationToken);
                foreach (var page in pages)
                {
                    if (string.IsNullOrWhiteSpace(page.Text)) continue;

                    double score = CalculateMatchScore(page.Text, queryTerms, query);
                    if (score > 0)
                    {
                        string snippet = ExtractRelevantSnippet(page.Text, queryTerms);
                        string safeViewerUrl = BuildSecureViewerUrl(docRef.RelativePath, page.PageNumber);

                        results.Add(new DocumentSearchResultDto
                        {
                            DocumentName = docRef.DocumentName,
                            CaseType = docRef.CaseType,
                            CaseId = docRef.CaseId,
                            CaseNumber = docRef.CaseNumber,
                            PageNumber = page.PageNumber,
                            Section = page.Section,
                            MatchedSnippet = snippet,
                            SecureViewerUrl = safeViewerUrl,
                            RelevanceScore = score,
                            IsVerified = true
                        });
                    }
                }
            }

            return results
                .OrderByDescending(r => r.RelevanceScore)
                .Take(Math.Max(1, maxResults))
                .ToList();
        }

        public async Task<List<DocumentPageExtractDto>> ExtractPagesAsync(
            string relativeOrFileName,
            string? caseType = null,
            int? caseId = null,
            int maxPages = 15,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(relativeOrFileName))
                return new List<DocumentPageExtractDto>();

            string cacheKey = relativeOrFileName.Trim();
            if (PageCache.TryGetValue(cacheKey, out var cached) && (DateTime.UtcNow - cached.CachedAt).TotalMinutes < 30)
            {
                return cached.Pages;
            }

            var pages = new List<DocumentPageExtractDto>();

            await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                string? physicalPath = _extractor.ResolveSecurePhysicalPath(relativeOrFileName);
                if (physicalPath == null || !File.Exists(physicalPath))
                {
                    _logger.LogWarning("Document file '{Path}' not found or path denied.", relativeOrFileName);
                    return;
                }

                string ext = Path.GetExtension(physicalPath).ToLowerInvariant();
                string docName = Path.GetFileName(physicalPath);

                if (ext == ".pdf")
                {
                    try
                    {
                        using var pdf = PdfDocument.Open(physicalPath);
                        int totalPages = pdf.NumberOfPages;
                        int pagesToRead = Math.Min(totalPages, Math.Max(1, maxPages));

                        for (int pageNum = 1; pageNum <= pagesToRead; pageNum++)
                        {
                            cancellationToken.ThrowIfCancellationRequested();

                            var page = pdf.GetPage(pageNum);
                            string text = page.Text ?? string.Empty;
                            string section = DetectSectionHeader(text, pageNum);

                            pages.Add(new DocumentPageExtractDto
                            {
                                DocumentName = docName,
                                CaseType = caseType,
                                CaseId = caseId,
                                PageNumber = pageNum,
                                Section = section,
                                Text = text.Trim()
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to extract pages from PDF: {Doc}", docName);
                    }
                }
                else if (ext == ".txt")
                {
                    try
                    {
                        string content = File.ReadAllText(physicalPath);
                        pages.Add(new DocumentPageExtractDto
                        {
                            DocumentName = docName,
                            CaseType = caseType,
                            CaseId = caseId,
                            PageNumber = 1,
                            Section = "Text Document",
                            Text = content
                        });
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to read text file: {Doc}", docName);
                    }
                }

                PageCache[cacheKey] = (DateTime.UtcNow, pages);
            }, cancellationToken);

            return pages;
        }

        private List<DocCandidate> DiscoverCandidateDocuments(string? caseType, int? caseId)
        {
            var list = new List<DocCandidate>();
            string upperType = (caseType ?? "").ToUpperInvariant();

            if (caseId.HasValue && caseId.Value > 0)
            {
                if (upperType == "MVC" || string.IsNullOrEmpty(upperType))
                {
                    var mvc = _caseRepo.GetCaseById(caseId.Value);
                    if (mvc != null)
                    {
                        string caseNo = $"MVC/{mvc.MVCNo}/{mvc.MVCYear}";
                        AddDocIfValid(list, "Adverse Judgment Copy", mvc.AdverseAward?.AdverseJudgmentUploadPath, "MVC", caseId.Value, caseNo);
                        AddDocIfValid(list, "Interim Court Order", mvc.InterimOrderFilePath, "MVC", caseId.Value, caseNo);
                        AddDocIfValid(list, "Favor Judgment Copy", mvc.FavorJudgmentPath, "MVC", caseId.Value, caseNo);
                        AddDocIfValid(list, "Security Report", mvc.AdverseAward?.SecurityUploadPath, "MVC", caseId.Value, caseNo);
                    }
                }

                if (upperType == "LABOUR" || string.IsNullOrEmpty(upperType))
                {
                    var labour = _labourRepo.GetCaseById(caseId.Value);
                    if (labour != null)
                    {
                        string caseNo = $"{labour.CaseType}/{labour.CaseNumber}/{labour.CaseYear}";
                        AddDocIfValid(list, "Stay Compliance Document", labour.CO_StayComplianceFilePath, "LABOUR", caseId.Value, caseNo);
                    }
                }
            }

            // Also include recent documents from JUDGEMENT_REPO if available
            try
            {
                var judgements = _judgementRepo.GetAllJudgements();
                foreach (var j in judgements.Where(j => !string.IsNullOrWhiteSpace(j.FilePath)).Take(4))
                {
                    AddDocIfValid(list, j.Title, j.FilePath, "JUDGEMENT", j.JudgementID, $"Repo #{j.JudgementID}");
                }
            }
            catch { /* Ignore non-critical repository errors */ }

            return list;
        }

        private static void AddDocIfValid(List<DocCandidate> list, string name, string? path, string caseType, int caseId, string caseNo)
        {
            if (!string.IsNullOrWhiteSpace(path) && !list.Any(x => x.RelativePath.Equals(path, StringComparison.OrdinalIgnoreCase)))
            {
                list.Add(new DocCandidate
                {
                    DocumentName = name,
                    RelativePath = path.Trim(),
                    CaseType = caseType,
                    CaseId = caseId,
                    CaseNumber = caseNo
                });
            }
        }

        private static string DetectSectionHeader(string pageText, int pageNumber)
        {
            if (string.IsNullOrWhiteSpace(pageText))
                return $"Page {pageNumber}";

            var lines = pageText.Split('\n')
                                .Select(l => l.Trim())
                                .Where(l => l.Length > 2)
                                .Take(4)
                                .ToList();

            foreach (var line in lines)
            {
                if (Regex.IsMatch(line, @"^(ORDER|JUDGMENT|AWARD|IN THE COURT|BEFORE THE|ISSUES|FINDINGS|OPERATIVE ORDER)", RegexOptions.IgnoreCase))
                {
                    return line.Length > 60 ? line.Substring(0, 60) + "..." : line;
                }
            }

            return lines.FirstOrDefault() ?? $"Page {pageNumber}";
        }

        private static double CalculateMatchScore(string text, List<string> terms, string query)
        {
            string lower = text.ToLowerInvariant();
            double score = 0;

            if (lower.Contains(query.ToLowerInvariant()))
                score += 1.5;

            int matchedTerms = 0;
            foreach (var term in terms)
            {
                if (lower.Contains(term.ToLowerInvariant()))
                {
                    matchedTerms++;
                    score += 0.4;
                }
            }

            return matchedTerms > 0 ? score : 0;
        }

        private static string ExtractRelevantSnippet(string text, List<string> terms)
        {
            string lower = text.ToLowerInvariant();
            int bestIndex = -1;

            foreach (var term in terms)
            {
                int idx = lower.IndexOf(term.ToLowerInvariant());
                if (idx >= 0)
                {
                    bestIndex = idx;
                    break;
                }
            }

            if (bestIndex < 0) bestIndex = 0;

            int start = Math.Max(0, bestIndex - 80);
            int length = Math.Min(260, text.Length - start);
            string snippet = text.Substring(start, length).Trim().Replace("\r\n", " ").Replace("\n", " ");

            if (start > 0) snippet = "..." + snippet;
            if (start + length < text.Length) snippet += "...";

            return snippet;
        }

        private static string BuildSecureViewerUrl(string relativePath, int pageNumber)
        {
            string clean = relativePath.TrimStart('/', '\\').Replace('\\', '/');
            return $"/uploads/{clean}#page={pageNumber}";
        }

        private static bool IsCommonStopword(string word)
        {
            var stopwords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "the", "and", "for", "with", "this", "that", "case", "from", "court", "order", "date", "what", "does", "say"
            };
            return stopwords.Contains(word);
        }

        private class DocCandidate
        {
            public string DocumentName { get; set; } = string.Empty;
            public string RelativePath { get; set; } = string.Empty;
            public string CaseType { get; set; } = string.Empty;
            public int CaseId { get; set; }
            public string CaseNumber { get; set; } = string.Empty;
        }
    }
}
