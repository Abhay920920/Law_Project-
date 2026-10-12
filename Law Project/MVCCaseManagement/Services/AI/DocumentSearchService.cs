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
        private readonly DBHelper? _db;
        private readonly IEmbeddingService? _embeddingService;
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
            ILogger<DocumentSearchService> logger,
            DBHelper? db = null,
            IEmbeddingService? embeddingService = null)
        {
            _extractor = extractor ?? throw new ArgumentNullException(nameof(extractor));
            _caseRepo = caseRepo ?? throw new ArgumentNullException(nameof(caseRepo));
            _labourRepo = labourRepo ?? throw new ArgumentNullException(nameof(labourRepo));
            _appealRepo = appealRepo ?? throw new ArgumentNullException(nameof(appealRepo));
            _judgementRepo = judgementRepo ?? throw new ArgumentNullException(nameof(judgementRepo));
            _environment = environment ?? throw new ArgumentNullException(nameof(environment));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _db = db;
            _embeddingService = embeddingService;
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

            var queryTerms = query.Split(new[] { ' ', ',', ';', '/', '-' }, StringSplitOptions.RemoveEmptyEntries)
                                  .Where(t => t.Length >= 3 && !IsCommonStopword(t))
                                  .ToList();
            if (!queryTerms.Any()) queryTerms.Add(query.Trim());

            // 0. Fast DB Retrieval: Check DOCUMENT_CHUNKS table first if case context is loaded
            if (_db != null && caseId.HasValue && caseId.Value > 0)
            {
                var dbChunks = await GetIndexedChunksFromDbAsync(caseType, caseId, null, cancellationToken);
                if (dbChunks.Count > 0)
                {
                    foreach (var page in dbChunks)
                    {
                        if (string.IsNullOrWhiteSpace(page.Text)) continue;
                        double score = CalculateMatchScore(page.Text, queryTerms, query);
                        if (score > 0)
                        {
                            results.Add(new DocumentSearchResultDto
                            {
                                DocumentName = page.DocumentName,
                                CaseType = page.CaseType,
                                CaseId = page.CaseId,
                                CaseNumber = page.CaseId?.ToString() ?? "",
                                PageNumber = page.PageNumber,
                                Section = page.Section,
                                MatchedSnippet = ExtractRelevantSnippet(page.Text, queryTerms),
                                SecureViewerUrl = BuildSecureViewerUrl(page.DocumentName, page.PageNumber),
                                RelevanceScore = score,
                                IsVerified = true
                            });
                        }
                    }

                    if (results.Count > 0)
                    {
                        if (_embeddingService != null && results.Count > 1)
                        {
                            var reranked = await _embeddingService.HybridRerankAsync(
                                query,
                                results,
                                r => (r.MatchedSnippet ?? "") + " " + (r.Section ?? "") + " " + (r.DocumentName ?? ""),
                                r => r.RelevanceScore,
                                semanticWeight: 0.6,
                                cancellationToken);

                            return reranked
                                .Select(x => { x.Item.RelevanceScore = Math.Round(x.HybridScore, 3); return x.Item; })
                                .Take(Math.Max(1, maxResults))
                                .ToList();
                        }

                        return results
                            .OrderByDescending(r => r.RelevanceScore)
                            .Take(Math.Max(1, maxResults))
                            .ToList();
                    }
                }
            }

            // 1. Discover relevant document references from repositories
            var candidateDocs = DiscoverCandidateDocuments(caseType, caseId);

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

            if (_embeddingService != null && results.Count > 1)
            {
                var reranked = await _embeddingService.HybridRerankAsync(
                    query,
                    results,
                    r => (r.MatchedSnippet ?? "") + " " + (r.Section ?? "") + " " + (r.DocumentName ?? ""),
                    r => r.RelevanceScore,
                    semanticWeight: 0.6,
                    cancellationToken);

                return reranked
                    .Select(x => { x.Item.RelevanceScore = Math.Round(x.HybridScore, 3); return x.Item; })
                    .Take(Math.Max(1, maxResults))
                    .ToList();
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

            // Check SQL Server DOCUMENT_CHUNKS before reading physical disk file
            string docName = Path.GetFileName(relativeOrFileName);
            if (_db != null)
            {
                var indexed = await GetIndexedChunksFromDbAsync(caseType, caseId, docName, cancellationToken);
                if (indexed.Count > 0)
                {
                    PageCache[cacheKey] = (DateTime.UtcNow, indexed);
                    return indexed;
                }
            }

            var pages = new List<DocumentPageExtractDto>();
            string computedHash = string.Empty;

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
                try
                {
                    using var sha = System.Security.Cryptography.SHA256.Create();
                    using var stream = File.OpenRead(physicalPath);
                    computedHash = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
                }
                catch { computedHash = Guid.NewGuid().ToString("N"); }

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
                            string rawText = page.Text ?? string.Empty;
                            bool isScanned = string.IsNullOrWhiteSpace(rawText) || rawText.Trim().Length < 20;
                            double conf = isScanned ? 0.0 : 0.98;
                            string section = DetectSectionHeader(rawText, pageNum);

                            string pageText = isScanned
                                ? $"[Scanned Exhibit Notice: Page {pageNum} of '{docName}' contains photographic/scanned document imagery without embedded digital text layer. Verified provenance logged.]"
                                : rawText.Trim();

                            pages.Add(new DocumentPageExtractDto
                            {
                                DocumentName = docName,
                                CaseType = caseType,
                                CaseId = caseId,
                                PageNumber = pageNum,
                                Section = section,
                                Text = pageText,
                                IsScannedPage = isScanned,
                                OcrConfidence = conf
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

            // Persist newly extracted pages to DOCUMENT_CHUNKS idempotently
            if (pages.Count > 0 && !string.IsNullOrWhiteSpace(computedHash))
            {
                await SaveChunksToDbAsync(docName, computedHash, caseType, caseId, pages, cancellationToken);
            }

            return pages;
        }

        private async Task<List<DocumentPageExtractDto>> GetIndexedChunksFromDbAsync(string? caseType, int? caseId, string? docName, CancellationToken cancellationToken)
        {
            var pages = new List<DocumentPageExtractDto>();
            if (_db == null) return pages;

            try
            {
                using var conn = _db.GetConnection();
                await conn.OpenAsync(cancellationToken);

                string sql = @"
                    SELECT ChunkID, DocumentName, CaseType, CaseId, PageNumber, SectionHeader, ChunkText
                    FROM DOCUMENT_CHUNKS
                    WHERE (@CaseType IS NULL OR CaseType = @CaseType)
                      AND (@CaseId IS NULL OR CaseId = @CaseId)
                      AND (@DocName IS NULL OR DocumentName = @DocName)
                    ORDER BY PageNumber ASC, ChunkIndex ASC";

                using var cmd = new Microsoft.Data.SqlClient.SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@CaseType", (object?)caseType ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@CaseId", (object?)caseId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@DocName", (object?)docName ?? DBNull.Value);

                using var rdr = await cmd.ExecuteReaderAsync(cancellationToken);
                while (await rdr.ReadAsync(cancellationToken))
                {
                    pages.Add(new DocumentPageExtractDto
                    {
                        DocumentName = rdr["DocumentName"]?.ToString() ?? "",
                        CaseType = rdr["CaseType"] != DBNull.Value ? rdr["CaseType"]?.ToString() : null,
                        CaseId = rdr["CaseId"] != DBNull.Value ? Convert.ToInt32(rdr["CaseId"]) : null,
                        PageNumber = Convert.ToInt32(rdr["PageNumber"]),
                        Section = rdr["SectionHeader"]?.ToString() ?? "Exhibit",
                        Text = rdr["ChunkText"]?.ToString() ?? ""
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not fetch indexed chunks from DOCUMENT_CHUNKS table.");
            }

            return pages;
        }

        private async Task SaveChunksToDbAsync(string docName, string docHash, string? caseType, int? caseId, List<DocumentPageExtractDto> pages, CancellationToken cancellationToken)
        {
            if (_db == null || pages == null || pages.Count == 0) return;

            try
            {
                using var conn = _db.GetConnection();
                await conn.OpenAsync(cancellationToken);

                // Incremental re-indexing: Purge stale chunks if document has been updated/modified
                string cleanSql = "DELETE FROM DOCUMENT_CHUNKS WHERE DocumentName = @DocName AND DocumentHash <> @Hash;";
                using (var cleanCmd = new Microsoft.Data.SqlClient.SqlCommand(cleanSql, conn))
                {
                    cleanCmd.Parameters.AddWithValue("@DocName", docName);
                    cleanCmd.Parameters.AddWithValue("@Hash", docHash);
                    await cleanCmd.ExecuteNonQueryAsync(cancellationToken);
                }

                for (int i = 0; i < pages.Count; i++)
                {
                    var page = pages[i];
                    string insertSql = @"
                        IF NOT EXISTS (SELECT 1 FROM DOCUMENT_CHUNKS WHERE DocumentHash = @Hash AND ChunkIndex = @Idx)
                        BEGIN
                            INSERT INTO DOCUMENT_CHUNKS (DocumentName, DocumentHash, CaseType, CaseId, ChunkIndex, PageNumber, SectionHeader, ChunkText, TokenCount, CreatedAt)
                            VALUES (@DocName, @Hash, @CaseType, @CaseId, @Idx, @PageNum, @Section, @Text, @TokenCount, GETDATE());
                        END";

                    using var cmd = new Microsoft.Data.SqlClient.SqlCommand(insertSql, conn);
                    cmd.Parameters.AddWithValue("@DocName", docName);
                    cmd.Parameters.AddWithValue("@Hash", docHash);
                    cmd.Parameters.AddWithValue("@CaseType", (object?)caseType ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@CaseId", (object?)caseId ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Idx", i);
                    cmd.Parameters.AddWithValue("@PageNum", page.PageNumber);
                    cmd.Parameters.AddWithValue("@Section", (object?)page.Section ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Text", page.Text ?? "");
                    cmd.Parameters.AddWithValue("@TokenCount", (page.Text?.Length ?? 0) / 4);

                    await cmd.ExecuteNonQueryAsync(cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to persist chunks into DOCUMENT_CHUNKS.");
            }
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

        public async Task<List<EvidenceChunk>> RetrieveEvidenceChunksAsync(
            string query,
            string? caseType = null,
            int? caseId = null,
            int maxChunks = 10,
            CancellationToken cancellationToken = default)
        {
            var chunks = new List<EvidenceChunk>();
            if (string.IsNullOrWhiteSpace(query))
                return chunks;

            var candidateDocs = DiscoverCandidateDocuments(caseType, caseId);
            var queryTerms = query.Split(new[] { ' ', ',', ';', '/', '-' }, StringSplitOptions.RemoveEmptyEntries)
                                  .Where(t => t.Length >= 3 && !IsCommonStopword(t))
                                  .ToList();
            if (!queryTerms.Any()) queryTerms.Add(query.Trim());

            string webRoot = _environment.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");

            foreach (var docRef in candidateDocs)
            {
                cancellationToken.ThrowIfCancellationRequested();

                string fullPath = Path.Combine(webRoot, "uploads", docRef.RelativePath.TrimStart('/', '\\'));
                string docHash = ComputeFileSha256(fullPath);
                string docType = ClassifyDocumentType(docRef.DocumentName, null);

                var pages = await ExtractPagesAsync(docRef.RelativePath, docRef.CaseType, docRef.CaseId, maxPages: 20, cancellationToken);
                foreach (var page in pages)
                {
                    if (string.IsNullOrWhiteSpace(page.Text)) continue;

                    // Segment page into paragraph chunks
                    var paragraphs = page.Text.Split(new[] { "\r\n\r\n", "\n\n" }, StringSplitOptions.RemoveEmptyEntries)
                                              .Where(p => p.Trim().Length > 20)
                                              .ToList();

                    if (paragraphs.Count == 0) paragraphs.Add(page.Text);

                    int pIdx = 1;
                    foreach (var para in paragraphs)
                    {
                        double score = CalculateMatchScore(para, queryTerms, query);
                        if (score > 0)
                        {
                            string chunkId = $"DOC-{(docHash.Length >= 8 ? docHash.Substring(0, 8) : "GEN")}-P{page.PageNumber}-C{pIdx}";
                            
                            chunks.Add(new EvidenceChunk
                            {
                                ChunkId = chunkId,
                                DocumentId = docRef.RelativePath,
                                DocumentName = docRef.DocumentName,
                                DocumentHash = docHash,
                                DocumentType = docType,
                                CaseId = docRef.CaseId,
                                CaseType = docRef.CaseType,
                                CaseNumber = docRef.CaseNumber,
                                PageNumber = page.PageNumber,
                                SectionOrProvision = page.Section,
                                Content = para.Trim(),
                                RetrievalScore = score,
                                AuthorityLevel = docType == "Award" || docType == "Order" 
                                    ? SourceAuthorityLevel.OfficialECourts 
                                    : SourceAuthorityLevel.NWKRTCDatabase,
                                Taxonomy = EvidenceTaxonomy.Fact,
                                IsVerified = true
                            });
                        }
                        pIdx++;
                    }
                }
            }

            return chunks
                .OrderByDescending(c => c.RetrievalScore)
                .Take(Math.Max(1, maxChunks))
                .ToList();
        }

        private static string ComputeFileSha256(string fullPath)
        {
            try
            {
                if (!File.Exists(fullPath)) return string.Empty;
                using var sha256 = System.Security.Cryptography.SHA256.Create();
                using var stream = File.OpenRead(fullPath);
                byte[] hash = sha256.ComputeHash(stream);
                return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            }
            catch
            {
                return string.Empty;
            }
        }

        private static string ClassifyDocumentType(string filename, string? content)
        {
            string fn = (filename ?? "").ToLowerInvariant();
            if (fn.Contains("award")) return "Award";
            if (fn.Contains("order") || fn.Contains("stay")) return "Order";
            if (fn.Contains("petition") || fn.Contains("claim")) return "Petition";
            if (fn.Contains("fir")) return "FIR";
            if (fn.Contains("sketch") || fn.Contains("spot")) return "SpotSketch";
            if (fn.Contains("judgment") || fn.Contains("judgement")) return "Judgement";
            if (fn.Contains("counter") || fn.Contains("written statement") || fn.Contains("ws")) return "Counter";
            if (fn.Contains("noting") || fn.Contains("note")) return "Noting";
            return "Exhibit";
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
