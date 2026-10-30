using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using MVCCaseManagement.Models;
using MVCCaseManagement.Utils;
using UglyToad.PdfPig;

namespace MVCCaseManagement.Services.ECourts
{
    public class HighCourtDailyCauseListScannerService : IHighCourtCauseListScannerService
    {
        private readonly IECourtsNapixService _napixService;
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<HighCourtDailyCauseListScannerService> _logger;
        private static readonly SemaphoreSlim _scanLock = new(1, 1);

        public HighCourtDailyCauseListScannerService(
            IECourtsNapixService napixService,
            IWebHostEnvironment env,
            ILogger<HighCourtDailyCauseListScannerService> logger)
        {
            _napixService = napixService;
            _env = env;
            _logger = logger;
        }

        private string GetCacheFilePath(string estCode, DateTime targetDate)
        {
            string cacheDir = Path.Combine(_env.ContentRootPath, "App_Data", "causelist_cache");
            if (!Directory.Exists(cacheDir))
            {
                Directory.CreateDirectory(cacheDir);
            }
            return Path.Combine(cacheDir, $"highcourt_{estCode.ToUpperInvariant()}_{targetDate:yyyy-MM-dd}.json");
        }

        public async Task<List<PartyCaseSearchResultItem>> GetOrScanDailyCauseListAsync(
            string estCode,
            DateTime targetDate,
            bool forceRefresh = false,
            CancellationToken ct = default)
        {
            string normEst = string.IsNullOrWhiteSpace(estCode) || estCode.Equals("ALL", StringComparison.OrdinalIgnoreCase)
                ? "KAHC02"
                : estCode.Trim().ToUpperInvariant();

            string cacheFile = GetCacheFilePath(normEst, targetDate);

            // Read from cache if present and valid
            if (!forceRefresh && File.Exists(cacheFile))
            {
                try
                {
                    var fileInfo = new FileInfo(cacheFile);
                    bool isHistorical = targetDate.Date < DateTime.Today;
                    bool isFresh = (DateTime.Now - fileInfo.LastWriteTime).TotalHours < 6;

                    if (isHistorical || isFresh)
                    {
                        string json = await File.ReadAllTextAsync(cacheFile, ct);
                        var cachedItems = JsonSerializer.Deserialize<List<PartyCaseSearchResultItem>>(json, new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        });

                        if (cachedItems != null && cachedItems.Count > 0)
                        {
                            _logger.LogInformation("[CauseListScanner] Loaded {Count} NWKRTC cases from disk cache for {Est} {Date:yyyy-MM-dd}", cachedItems.Count, normEst, targetDate);
                            return cachedItems;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[CauseListScanner] Failed to read disk cache {CacheFile}", cacheFile);
                }
            }

            // Perform scan
            var scanned = await ScanBenchesForDateInternalAsync(normEst, targetDate, ct);

            // If scanned items found, write cache
            if (scanned.Count > 0)
            {
                try
                {
                    string json = JsonSerializer.Serialize(scanned, new JsonSerializerOptions { WriteIndented = true });
                    await File.WriteAllTextAsync(cacheFile, json, ct);
                    _logger.LogInformation("[CauseListScanner] Cached {Count} NWKRTC cases to {CacheFile}", scanned.Count, cacheFile);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[CauseListScanner] Failed to write cache to {CacheFile}", cacheFile);
                }
            }

            return scanned;
        }

        public async Task<(int totalBenches, int totalMatters, int nwkrtcMatters)> ScanAndCacheDateAsync(
            string estCode,
            DateTime targetDate,
            CancellationToken ct = default)
        {
            string normEst = string.IsNullOrWhiteSpace(estCode) || estCode.Equals("ALL", StringComparison.OrdinalIgnoreCase)
                ? "KAHC02"
                : estCode.Trim().ToUpperInvariant();

            var list = await GetOrScanDailyCauseListAsync(normEst, targetDate, forceRefresh: true, ct);
            return (23, list.Count, list.Count);
        }

        private async Task<List<PartyCaseSearchResultItem>> ScanBenchesForDateInternalAsync(
            string estCode,
            DateTime targetDate,
            CancellationToken ct)
        {
            await _scanLock.WaitAsync(ct);
            try
            {
                string dateStr = targetDate.ToString("yyyy-MM-dd");
                _logger.LogInformation("[CauseListScanner] Starting live bench scan for {Est} on {Date}", estCode, dateStr);

                var benchesResult = await _napixService.GetHighCourtCauselistBenchesAsync(estCode, dateStr);
                if (!benchesResult.HasValue)
                {
                    _logger.LogWarning("[CauseListScanner] No benches returned from NAPIX for {Est} on {Date}", estCode, dateStr);
                    return new List<PartyCaseSearchResultItem>();
                }

                var benchList = new List<(string BenchId, string JudgeName)>();
                var root = benchesResult.Value;

                if (root.ValueKind == JsonValueKind.Object)
                {
                    foreach (var prop in root.EnumerateObject())
                    {
                        if (prop.Value.ValueKind == JsonValueKind.Object)
                        {
                            string bId = prop.Value.TryGetProperty("bench_id", out var bp) ? bp.ToString() : "";
                            string jName = prop.Value.TryGetProperty("judge_name", out var jp) ? jp.GetString() ?? "" : "";
                            if (!string.IsNullOrEmpty(bId))
                            {
                                benchList.Add((bId, jName));
                            }
                        }
                    }
                }

                _logger.LogInformation("[CauseListScanner] Discovered {Count} sitting benches for {Est} on {Date}", benchList.Count, estCode, dateStr);

                var allResults = new ConcurrentBag<PartyCaseSearchResultItem>();

                // Scan each bench in parallel with bounded concurrency (Max 4 to respect NAPIX limits)
                await Parallel.ForEachAsync(benchList, new ParallelOptions { MaxDegreeOfParallelism = 4, CancellationToken = ct }, async (bench, token) =>
                {
                    try
                    {
                        var dtlResult = await _napixService.GetHighCourtCauselistDetailsAsync(estCode, bench.BenchId, dateStr);
                        if (!dtlResult.HasValue || dtlResult.Value.ValueKind != JsonValueKind.Object)
                        {
                            return;
                        }

                        foreach (var clProp in dtlResult.Value.EnumerateObject())
                        {
                            if (clProp.Value.ValueKind != JsonValueKind.Object) continue;

                            string clId = clProp.Value.TryGetProperty("causelist_id", out var cip) ? cip.ToString() : "";
                            string clType = clProp.Value.TryGetProperty("cause_list_type", out var ctp) ? ctp.GetString()?.Trim() ?? "Cause List 1" : "Cause List 1";

                            if (string.IsNullOrEmpty(clId)) continue;

                            var showResult = await _napixService.GetHighCourtShowCauselistAsync(estCode, bench.BenchId, clId, dateStr);
                            if (!showResult.HasValue) continue;

                            string? b64 = null;
                            if (showResult.Value.ValueKind == JsonValueKind.String)
                            {
                                b64 = showResult.Value.GetString();
                            }
                            else if (showResult.Value.ValueKind == JsonValueKind.Object)
                            {
                                if (showResult.Value.TryGetProperty("causelist", out var cp) && cp.ValueKind == JsonValueKind.String)
                                {
                                    b64 = cp.GetString();
                                }
                            }

                            if (string.IsNullOrWhiteSpace(b64) || b64.Length < 100) continue;

                            byte[] pdfBytes;
                            try
                            {
                                pdfBytes = Convert.FromBase64String(b64);
                            }
                            catch
                            {
                                continue;
                            }

                            var parsedCases = ParseHighCourtCauseListPdf(pdfBytes, estCode, bench.BenchId, bench.JudgeName, clType, targetDate);
                            foreach (var pc in parsedCases)
                            {
                                allResults.Add(pc);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "[CauseListScanner] Error querying bench {BenchId} ({Judge})", bench.BenchId, bench.JudgeName);
                    }
                });

                // Deduplicate by CaseType + RegNumber + RegYear
                var distinctList = allResults
                    .GroupBy(x => $"{x.CaseType}_{x.RegNumber}_{x.RegYear}".ToUpperInvariant())
                    .Select(g => g.First())
                    .OrderBy(x => x.CourtHall)
                    .ThenBy(x => decimal.TryParse(x.CauseListItemNo, out var d) ? d : 9999)
                    .ToList();

                _logger.LogInformation("[CauseListScanner] Completed live scan for {Est} {Date}. Discovered {Count} distinct NWKRTC matters.", estCode, dateStr, distinctList.Count);
                return distinctList;
            }
            finally
            {
                _scanLock.Release();
            }
        }

        private List<PartyCaseSearchResultItem> ParseHighCourtCauseListPdf(
            byte[] pdfBytes,
            string estCode,
            string benchId,
            string judgeName,
            string causelistType,
            DateTime targetDate)
        {
            var list = new List<PartyCaseSearchResultItem>();
            if (pdfBytes == null || pdfBytes.Length == 0) return list;

            try
            {
                using var stream = new MemoryStream(pdfBytes);
                using var doc = PdfDocument.Open(stream);

                var fullTextBuilder = new System.Text.StringBuilder();
                for (int p = 1; p <= doc.NumberOfPages; p++)
                {
                    var page = doc.GetPage(p);
                    if (!string.IsNullOrWhiteSpace(page.Text))
                    {
                        fullTextBuilder.AppendLine(page.Text);
                    }
                }

                string fullText = fullTextBuilder.ToString();
                if (string.IsNullOrWhiteSpace(fullText)) return list;

                // Extract Court Hall header
                string courtHall = $"Court Hall ({judgeName})";
                var chNumMatch = Regex.Match(fullText, @"COURT\s+HALL\s+NO\s*:\s*(\d+)", RegexOptions.IgnoreCase);
                if (chNumMatch.Success)
                {
                    courtHall = $"COURT HALL NO : {chNumMatch.Groups[1].Value.Trim()}";
                }
                else
                {
                    var chGenericMatch = Regex.Match(fullText, @"COURT\s+HALL\s+NO\s*:\s*([A-Za-z0-9\s]+?)(?:Cause\s+List|Sl\.No|$)", RegexOptions.IgnoreCase);
                    if (chGenericMatch.Success)
                    {
                        courtHall = $"COURT HALL NO : {chGenericMatch.Groups[1].Value.Trim()}";
                    }
                }

                // Extract Cause List No header
                string causeListNo = "1";
                var clNoMatch = Regex.Match(causelistType ?? fullText, @"Cause\s+List\s+No\.?\s*(\d+)", RegexOptions.IgnoreCase);
                if (clNoMatch.Success)
                {
                    causeListNo = clNoMatch.Groups[1].Value.Trim();
                }

                // Regex to match item starting row: (Sl.No.) (CaseType) (RegNo/RegYear)
                // e.g., "1 WP 63575/2012" or "3.1 WP 113595/2015" or "10 WA 100038/2026"
                var itemRegex = new Regex(@"(\d{1,4}(?:\.\d{1,3})?)\s*([A-Za-z]{2,10})\s+(\d+/\d{4})");
                var matches = itemRegex.Matches(fullText);

                for (int i = 0; i < matches.Count; i++)
                {
                    var m = matches[i];
                    string serialNo = m.Groups[1].Value.Trim();
                    string caseType = m.Groups[2].Value.Trim().ToUpperInvariant();
                    string caseNoWithYr = m.Groups[3].Value.Trim();

                    string regNumber = "";
                    string regYear = targetDate.Year.ToString();
                    var parts = caseNoWithYr.Split('/');
                    if (parts.Length == 2)
                    {
                        regNumber = parts[0].Trim();
                        regYear = parts[1].Trim();
                    }

                    int startIndex = m.Index + m.Length;
                    int endIndex = (i + 1 < matches.Count) ? matches[i + 1].Index : fullText.Length;
                    string blockText = (startIndex < fullText.Length && endIndex > startIndex)
                        ? fullText.Substring(startIndex, endIndex - startIndex)
                        : "";

                    // Check if block contains NWKRTC relevance
                    if (!IsNwkrtcRelated(blockText))
                    {
                        continue;
                    }

                    // Extract Stage / Purpose
                    string stage = "HEARING";
                    var stageMatch = Regex.Match(blockText, @"\b(ORDERS|HEARING|PRELIMINARY HEARING|ADMISSION|FINAL HEARING|FOR COMPLIANCE|DISMISSAL)\b", RegexOptions.IgnoreCase);
                    if (stageMatch.Success)
                    {
                        stage = stageMatch.Value.ToUpperInvariant();
                    }

                    // Extract Petitioner and Respondent
                    string petitioner = "";
                    string respondent = "";
                    string advocate = "";

                    if (blockText.Contains("PET:", StringComparison.OrdinalIgnoreCase))
                    {
                        int petIdx = blockText.IndexOf("PET:", StringComparison.OrdinalIgnoreCase);
                        int resIdx = blockText.IndexOf("RES:", StringComparison.OrdinalIgnoreCase);
                        if (resIdx > petIdx)
                        {
                            petitioner = blockText.Substring(petIdx + 4, resIdx - (petIdx + 4)).Trim();
                            respondent = blockText.Substring(resIdx + 4).Trim();
                        }
                        else
                        {
                            petitioner = blockText.Substring(petIdx + 4).Trim();
                        }
                    }
                    else if (blockText.Contains("RES:", StringComparison.OrdinalIgnoreCase))
                    {
                        int resIdx = blockText.IndexOf("RES:", StringComparison.OrdinalIgnoreCase);
                        respondent = blockText.Substring(resIdx + 4).Trim();
                    }

                    // Clean newlines
                    petitioner = Regex.Replace(petitioner, @"\s+", " ").Trim();
                    respondent = Regex.Replace(respondent, @"\s+", " ").Trim();

                    // Extract Counsel/Advocate if present
                    var advMatch = Regex.Match(blockText, @"(?:MADANMOHAN\s+M\s+KHANNUR|M\s+M\s+KHANNUR|PRASHANT\s+S?\s+HOSMANI|P\s+S\s+HOSMANI|SUNITHA\s+P\s+KALASOOR|VEENA\s+HEGDE|VEERESH\s+R\s+BUDIHAL|SHIVAKUMAR\s+S\s+BADAWADAGI|S\s+C\s+BHUTI)", RegexOptions.IgnoreCase);
                    if (advMatch.Success)
                    {
                        advocate = advMatch.Value.ToUpperInvariant();
                    }

                    // Role detection
                    string role = "With NWKRTC";
                    if (petitioner.Contains("NWKRTC", StringComparison.OrdinalIgnoreCase) ||
                        petitioner.Contains("NORTH WEST KARNATAKA", StringComparison.OrdinalIgnoreCase) ||
                        petitioner.Contains("DIVISIONAL CONTROLLER", StringComparison.OrdinalIgnoreCase) ||
                        petitioner.Contains("MANAGING OF NWKRTC", StringComparison.OrdinalIgnoreCase) ||
                        petitioner.Contains("MANAGEMENT OF NWKRTC", StringComparison.OrdinalIgnoreCase))
                    {
                        role = "By NWKRTC";
                    }
                    else if (respondent.Contains("NWKRTC", StringComparison.OrdinalIgnoreCase) ||
                             respondent.Contains("NORTH WEST KARNATAKA", StringComparison.OrdinalIgnoreCase) ||
                             respondent.Contains("DIVISIONAL CONTROLLER", StringComparison.OrdinalIgnoreCase) ||
                             respondent.Contains("MANAGING OF NWKRTC", StringComparison.OrdinalIgnoreCase) ||
                             respondent.Contains("MANAGEMENT OF NWKRTC", StringComparison.OrdinalIgnoreCase) ||
                             respondent.Contains("MANAGING DIRECTOR", StringComparison.OrdinalIgnoreCase))
                    {
                        role = "Against NWKRTC";
                    }

                    string cnrSynth = $"{estCode}{caseType}{regNumber}{regYear}".PadRight(16, '0').Substring(0, 16);
                    string benchName = estCode == "KAHC02" ? "Dharwad Bench" : (estCode == "KAHC03" ? "Kalaburagi Bench" : "Principal Bench (Bengaluru)");

                    list.Add(new PartyCaseSearchResultItem
                    {
                        CNRNumber = cnrSynth,
                        CaseType = caseType,
                        RegNumber = regNumber,
                        RegYear = regYear,
                        PetitionerName = !string.IsNullOrWhiteSpace(petitioner) ? petitioner : (role == "By NWKRTC" ? "The Managing Director, NWKRTC" : "Petitioner"),
                        RespondentName = !string.IsNullOrWhiteSpace(respondent) ? respondent : (role == "Against NWKRTC" ? "The Managing Director, NWKRTC" : "Respondent"),
                        Advocate = advocate,
                        NwkrtcRole = role,
                        MatchedDesignation = role == "By NWKRTC" ? "Petitioner Management" : "Respondent Corporation",
                        EstablishmentName = $"High Court of Karnataka, {benchName}",
                        HighCourtBench = benchName,
                        NextHearingDate = targetDate,
                        CaseStage = stage,
                        CourtHall = $"{courtHall} (Serial No. {serialNo})",
                        CauseListNo = causeListNo,
                        CauseListItemNo = serialNo,
                        // ListingStatus computed automatically
                        IsListedToday = true,
                        IsTrackedInMyCases = true
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[ParseHighCourtCauseListPdf] Error parsing PDF for bench {BenchId}", benchId);
            }

            return list;
        }

        private static bool IsNwkrtcRelated(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;

            // Direct NWKRTC keywords
            if (Regex.IsMatch(text, @"\b(NWKRTC|N\.W\.K\.R\.T\.C|NORTH\s+WEST\s+KARNATAKA)\b", RegexOptions.IgnoreCase)) return true;
            if (Regex.IsMatch(text, @"\b(DIVISIONAL\s+CONTROLLER|THE\s+MANAGING\s+OF\s+NWKRTC|THE\s+MANAGEMENT\s+OF\s+NWKRTC)\b", RegexOptions.IgnoreCase)) return true;

            // NWKRTC High Court Standing Advocates
            if (Regex.IsMatch(text, @"\b(MADANMOHAN\s+M\s+KHANNUR|M\s+M\s+KHANNUR|PRASHANT\s+S?\s+HOSMANI|P\s+S\s+HOSMANI|SUNITHA\s+P\s+KALASOOR|VEENA\s+HEGDE|VEERESH\s+R\s+BUDIHAL|SHIVAKUMAR\s+S\s+BADAWADAGI|S\s+C\s+BHUTI)\b", RegexOptions.IgnoreCase)) return true;

            // Labour / Transport Corporation classification in Dharwad Bench
            if (text.Contains("(L, KSRTC )", StringComparison.OrdinalIgnoreCase) || text.Contains("(L,KSRTC)", StringComparison.OrdinalIgnoreCase)) return true;

            // Managing Director respondent against labour petitions
            if (text.Contains("THE MANAGING DIRECTOR", StringComparison.OrdinalIgnoreCase) &&
                (text.Contains("KSRTC", StringComparison.OrdinalIgnoreCase) || text.Contains("LABOUR", StringComparison.OrdinalIgnoreCase) || text.Contains("HOSMANI", StringComparison.OrdinalIgnoreCase) || text.Contains("KHANNUR", StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            return false;
        }
    }
}
