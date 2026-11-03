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
        private readonly IHighCourtPartySearchStore? _partyStore;
        private static readonly SemaphoreSlim _scanLock = new(1, 1);

        public HighCourtDailyCauseListScannerService(
            IECourtsNapixService napixService,
            IWebHostEnvironment env,
            ILogger<HighCourtDailyCauseListScannerService> logger,
            IHighCourtPartySearchStore? partyStore = null)
        {
            _napixService = napixService;
            _env = env;
            _logger = logger;
            _partyStore = partyStore;
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

        public void PurgePreviousDaysCache(DateTime? referenceDate = null)
        {
            try
            {
                var today = (referenceDate ?? DateTime.Today).Date;
                string cacheDir = Path.Combine(_env.ContentRootPath, "App_Data", "causelist_cache");
                if (!Directory.Exists(cacheDir)) return;

                var files = Directory.GetFiles(cacheDir, "highcourt_*_*.json");
                foreach (var file in files)
                {
                    try
                    {
                        var fileName = Path.GetFileNameWithoutExtension(file);
                        // File naming convention: highcourt_{EST}_{yyyy-MM-dd}
                        var parts = fileName.Split('_');
                        if (parts.Length >= 3 && DateTime.TryParseExact(parts[parts.Length - 1], "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var fileDate))
                        {
                            if (fileDate.Date < today)
                            {
                                File.Delete(file);
                                _logger.LogInformation("[CauseListScanner] Purged previous day cause list cache file: {FileName} (Date: {Date:yyyy-MM-dd})", Path.GetFileName(file), fileDate);
                            }
                        }
                    }
                    catch (Exception exF)
                    {
                        _logger.LogWarning(exF, "[CauseListScanner] Error checking/purging cache file {File}", file);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[CauseListScanner] Error purging previous days cause list cache.");
            }
        }

        public async Task<List<PartyCaseSearchResultItem>> GetOrScanDailyCauseListAsync(
            string estCode,
            DateTime targetDate,
            bool forceRefresh = false,
            CancellationToken ct = default)
        {
            // Prune previous day files automatically so cache remains strictly clean
            PurgePreviousDaysCache();

            if (targetDate.DayOfWeek == DayOfWeek.Saturday || targetDate.DayOfWeek == DayOfWeek.Sunday)
            {
                _logger.LogInformation("[CauseListScanner] Target date {Date:yyyy-MM-dd} is a weekend ({Day}). Court is closed.", targetDate, targetDate.DayOfWeek);
                return new List<PartyCaseSearchResultItem>();
            }
            if (string.IsNullOrWhiteSpace(estCode) || estCode.Equals("ALL", StringComparison.OrdinalIgnoreCase))
            {
                // Multi-bench aggregation: scan Dharwad, Bengaluru Principal, and Kalaburagi benches
                var multiResults = new List<PartyCaseSearchResultItem>();
                var seenKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                var benchTasks = new[] { "KAHC02", "KAHC01", "KAHC03" }
                    .Select(async bCode =>
                    {
                        try
                        {
                            return await GetOrScanDailyCauseListAsync(bCode, targetDate, forceRefresh, ct);
                        }
                        catch (Exception exB)
                        {
                            _logger.LogWarning(exB, "[CauseListScanner] Multi-bench scan error for {Bench} on {Date:yyyy-MM-dd}", bCode, targetDate);
                            return new List<PartyCaseSearchResultItem>();
                        }
                    });

                var allBenchesLists = await Task.WhenAll(benchTasks);
                foreach (var benchList in allBenchesLists)
                {
                    foreach (var item in benchList)
                    {
                        string key = !string.IsNullOrEmpty(item.CNRNumber) && !item.CNRNumber.EndsWith("000000")
                            ? item.CNRNumber.Trim().ToUpperInvariant()
                            : $"{item.CaseType}_{item.RegNumber}_{item.RegYear}".Trim().ToUpperInvariant();

                        if (!seenKeys.Contains(key))
                        {
                            seenKeys.Add(key);
                            multiResults.Add(item);
                        }
                    }
                }

                return multiResults;
            }

            string normEst = estCode.Trim().ToUpperInvariant();
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

            // If scanned items found, write cache and merge into master store
            if (scanned.Count > 0)
            {
                try
                {
                    string json = JsonSerializer.Serialize(scanned, new JsonSerializerOptions { WriteIndented = true });
                    await File.WriteAllTextAsync(cacheFile, json, ct);
                    _logger.LogInformation("[CauseListScanner] Cached {Count} NWKRTC cases to {CacheFile}", scanned.Count, cacheFile);

                    if (_partyStore != null)
                    {
                        _ = _partyStore.MergeFreshCasesAsync(scanned, ct);
                    }
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
            string normEst = string.IsNullOrWhiteSpace(estCode) ? "ALL" : estCode.Trim().ToUpperInvariant();

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

                // Scan each bench in parallel with bounded concurrency (Max 2 to prevent NAPIX rate-limiting/500 errors)
                await Parallel.ForEachAsync(benchList, new ParallelOptions { MaxDegreeOfParallelism = 2, CancellationToken = ct }, async (bench, token) =>
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

                            JsonElement? showResult = null;
                            for (int retry = 1; retry <= 3; retry++)
                            {
                                showResult = await _napixService.GetHighCourtShowCauselistAsync(estCode, bench.BenchId, clId, dateStr);
                                if (showResult.HasValue) break;
                                await Task.Delay(400, token);
                            }
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

                // Extract Court Hall header before cleaning running headers
                string courtHall = $"Court Hall ({judgeName})";
                var chNumMatch = Regex.Match(fullText, @"COURT\s*(?:HALL)?\s*NO\s*[:\s]*(\d+)", RegexOptions.IgnoreCase);
                if (chNumMatch.Success && chNumMatch.Groups[1].Value != "0")
                {
                    courtHall = $"COURT HALL NO : {chNumMatch.Groups[1].Value.Trim()}";
                }
                else
                {
                    var chGenericMatch = Regex.Match(fullText, @"COURT\s+HALL\s+NO\s*:\s*([A-Za-z0-9\s]+?)(?:Cause\s+List|Sl\.No|$)", RegexOptions.IgnoreCase);
                    if (chGenericMatch.Success && !chGenericMatch.Groups[1].Value.Trim().Equals("0"))
                    {
                        courtHall = $"COURT HALL NO : {chGenericMatch.Groups[1].Value.Trim()}";
                    }
                    else if (judgeName.Contains("NAGAPRASANNA", StringComparison.OrdinalIgnoreCase))
                    {
                        courtHall = "COURT HALL NO : 2";
                    }
                    else if (judgeName.Contains("NADAF", StringComparison.OrdinalIgnoreCase) || judgeName.Contains("SHANTHI", StringComparison.OrdinalIgnoreCase))
                    {
                        courtHall = "COURT HALL NO : 6";
                    }
                    else if (judgeName.Contains("KAMAL", StringComparison.OrdinalIgnoreCase))
                    {
                        courtHall = "COURT HALL NO : 10";
                    }
                }

                // Extract Cause List No header
                string causeListNo = "1";
                var clNoMatch = Regex.Match(causelistType ?? fullText, @"Cause\s+List\s+No\.?\s*(\d+)", RegexOptions.IgnoreCase);
                if (clNoMatch.Success)
                {
                    causeListNo = clNoMatch.Groups[1].Value.Trim();
                }

                // Strip running page headers and website links so serial numbers and case codes detach cleanly
                // Pre-process fullText:
                // 1. Separate 4-digit years from serial numbers before case types: e.g. "202544WP" -> "2025 44 WP"
                fullText = Regex.Replace(fullText, @"\b(19\d{2}|20\d{2})(\d{1,3}(?:\.\d{1,2})?)(?=(?:wt)?(?:WP|WA|CCC|CP|RP|EP|MFA|MSA|CR|CRP|CRL)\b)", "$1 $2 ", RegexOptions.IgnoreCase);
                // 2. Separate R/P/A-numbers from serial numbers: e.g. "R1660WP" -> "R16 60 WP"
                fullText = Regex.Replace(fullText, @"\b([RPA]\d{1,2})(\d{1,2})(?=(?:wt)?(?:WP|WA|CCC|CP|RP|EP|MFA|MSA|CR|CRP|CRL)\b)", "$1 $2 ", RegexOptions.IgnoreCase);
                // 3. Separate trailing letters from serial numbers: e.g. "RAO27WP" -> "RAO 27 WP"
                fullText = Regex.Replace(fullText, @"(?<=[A-Za-z])(\d{1,3}(?:\.\d{1,2})?)(?=(?:wt)?(?:WP|WA|CCC|CP|RP|EP|MFA|MSA|CR|CRP|CRL)\b)", " $1 ", RegexOptions.IgnoreCase);
                // 4. In Karnataka High Court, headers have the pattern "031120261.367422WP" (Date + ListNo + Dot + BenchId + SerialNo + CaseType).
                // We replace up to BenchId (e.g. "\b\d{8}\d*\.\d{3,4}") so the serial number (e.g. 22 or 35) is preserved!
                fullText = Regex.Replace(fullText, @"Website:http[^\s]*|Page\s+\d+\s+of\s+\d+|\b\d{8}\d*\.\d{3,4}", " ", RegexOptions.IgnoreCase);

                // Regex to match item starting row: (Sl.No.) (CaseType) (RegNo/RegYear)
                // In NAPIX PDF text extracted by PdfPig, serial number is often joined to case type without whitespace (e.g. "41WA/100206/2025" or "18WP 64148/2011")
                var itemRegex = new Regex(@"(?:^|\s|[A-Za-z0-9\.\-]+?)(\d{1,3}(?:\.\d{1,2})?)\s*([A-Za-z]+(?:\.[A-Za-z]+|\([A-Za-z0-9\-]+\))?(?:\s*NO\.?)?)[\s/]+(\d+/\d{4})", RegexOptions.IgnoreCase);
                var matches = itemRegex.Matches(fullText);

                for (int i = 0; i < matches.Count; i++)
                {
                    var m = matches[i];
                    string rawSerialNo = m.Groups[1].Value.Trim();
                    string serialNo = Regex.Match(rawSerialNo, @"\d{1,3}(?:\.\d{1,2})?$")?.Value ?? rawSerialNo;
                    string rawCaseType = m.Groups[2].Value.Trim().ToUpperInvariant();
                    string caseType = Regex.Replace(rawCaseType, @"\s+NO\.?", "").Trim();
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
                    if (!IsNwkrtcRelated(blockText, estCode))
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

                    // Subject Classification Gate:
                    // Strictly allow Labour (L, KSRTC), Gratuity (L, PG), Service (S, KSRTC), and Commercial Tender (GM, TEN)
                    // Strictly exclude MFA / MACT appeals (MV, I / MV, D) and Land Revenue (KLR, RES)
                    if (caseType.Equals("MFA", StringComparison.OrdinalIgnoreCase))
                    {
                        continue; // Exclude motor accident insurance appeals
                    }

                    string subjectCategory = "";
                    var anyCatMatch = Regex.Match(blockText, @"\(\s*([A-Za-z]+\s*,\s*[A-Za-z0-9\/\-]+)\s*\)", RegexOptions.IgnoreCase);
                    if (anyCatMatch.Success)
                    {
                        var allowedCatMatch = Regex.Match(blockText, @"\(\s*(?:L\s*,\s*(?:KSRTC|PG)|S\s*,\s*KSRTC|GM\s*,\s*TEN)\s*\)", RegexOptions.IgnoreCase);
                        if (!allowedCatMatch.Success)
                        {
                            continue; // Exclude non-matching categories (MV, I, KLR, RES, GM, CPC, etc.)
                        }
                        subjectCategory = allowedCatMatch.Value.Trim();
                    }
                    else
                    {
                        // In some benches (e.g. Division Bench Writ Appeals / VS-format lists), the High Court PDF omits the category column.
                        // Strictly limit to Writ Petitions (WP), Writ Appeals (WA), and Contempt (CCC)
                        if (!caseType.Equals("WP", StringComparison.OrdinalIgnoreCase) && 
                            !caseType.Equals("WA", StringComparison.OrdinalIgnoreCase) &&
                            !caseType.Equals("CCC", StringComparison.OrdinalIgnoreCase))
                        {
                            continue; // Exclude non-writ jurisdictions (MFA motor accident appeals, etc.)
                        }

                        subjectCategory = (blockText.Contains("LABOUR", StringComparison.OrdinalIgnoreCase) || 
                                           blockText.Contains("WORKMAN", StringComparison.OrdinalIgnoreCase) || 
                                           blockText.Contains("INDUSTRIAL", StringComparison.OrdinalIgnoreCase)) 
                                           ? "(L, KSRTC)" 
                                           : "(S, KSRTC)";
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
                    else
                    {
                        // Handle VS format (common in Division Bench & Single Judge orders lists)
                        // Seamlessly handles embedded VS even without spaces: "DANDINAVARVSTHE", "BAGBANVSMANAGING", "PATAGARVSTHE"
                        var vsMatch = Regex.Match(blockText, @"(?:\s+|\b|(?<=[A-Za-z0-9\.\)]))\s*VS\s*(?:\s+|\b|(?=[A-Za-z]))", RegexOptions.IgnoreCase);
                        if (vsMatch.Success)
                        {
                            string beforeVs = blockText.Substring(0, vsMatch.Index).Trim().Trim(',', ' ', '\r', '\n');
                            string afterVs = blockText.Substring(vsMatch.Index + vsMatch.Length).Trim();

                            int lastParen = beforeVs.LastIndexOf(')');
                            petitioner = (lastParen >= 0 && lastParen < beforeVs.Length - 1) ? beforeVs.Substring(lastParen + 1).Trim() : beforeVs;

                            var resMatch = Regex.Match(afterVs, @"^(.*?)(?=(?:Mr\.|ADV|IA\s*NO|\d+WP|\d+WA|$))", RegexOptions.IgnoreCase | RegexOptions.Singleline);
                            respondent = resMatch.Success && !string.IsNullOrWhiteSpace(resMatch.Value) ? resMatch.Value.Trim() : afterVs;
                        }
                    }

                    // Strip any attached page header/footer metadata from petitioner and respondent
                    petitioner = Regex.Replace(petitioner, @"Website:http[^\s]*|Page\s+\d+\s+of\s+\d+|COURT\s+HALL\s+NO[^
]*|Cause\s+List\s+No[^
]*|Sl\.No\.[^
]*|Case\s+No\.[^
]*", " ", RegexOptions.IgnoreCase);
                    respondent = Regex.Replace(respondent, @"Website:http[^\s]*|Page\s+\d+\s+of\s+\d+|COURT\s+HALL\s+NO[^
]*|Cause\s+List\s+No[^
]*|Sl\.No\.[^
]*|Case\s+No\.[^
]*", " ", RegexOptions.IgnoreCase);

                    // Extract Counsel/Advocate if present (covers Dharwad, Bengaluru, Kalaburagi standing counsel)
                    var advMatch = Regex.Match(blockText, @"(?:MADANMOHAN\s*(?:M\s*)?KHANNUR|M\s*M\s*KHANNUR|PRASHANT\s*(?:S\s*)?HOSMANI|P\s*S\s*HOSMANI|PRASHANT\s*HOSMANI|PRAKASH\s*(?:N\s*)?HOSAMAN[EI]|P\s*N\s*HOSAMANE|KINI\s*N\s*S|N\s*S\s*KINI|SUNITHA\s*(?:P\s*)?KALASOOR|VEENA\s*HEGDE|HEGDE\s*VEENA|VEERESH\s*(?:R\s*)?BUDIHAL|SHIVAKUMAR\s*(?:S\s*)?BADAWADAGI|S\s*S\s*BHUTI|S\s*C\s*BHUTI|CHANDRASHEKHAR\s*(?:M\s*)?HOSAMANI|C\s*M\s*HOSAMANI|S\s*L\s*MATTI|SHIVANAND\s*L\s*MATTI|VAISHALI\s*(?:K\s*)?KALADAGI|ROHIT\s*(?:S\s*)?PATIL|MANJUNATH\s*(?:A\s*)?KARIGANNAVAR|D\s*VIJAYKUMAR|D\s*VIJAYA\s*KUMAR|PREMAVATHI\s*(?:B\s*)?PATIL|SHWETA\s*KULKARNI|B\s*L\s*SANJEEV|H\s*R\s*GUNDAPPA|F\s*S\s*DABALI|SANTOSH\s*(?:B\s*)?MANE|MALLIKARJUN\s*SAHUKAR|LINGARAJ\s*MARADI|RAVI\s*HEGDE|KALPANA\s*(?:T\s*)?M)", RegexOptions.IgnoreCase);
                    if (advMatch.Success)
                    {
                        advocate = advMatch.Value.ToUpperInvariant();

                        // Clean advocate from respondent if embedded
                        if (!string.IsNullOrEmpty(respondent))
                        {
                            int advIdx = respondent.IndexOf(advMatch.Value, StringComparison.OrdinalIgnoreCase);
                            if (advIdx >= 0)
                            {
                                respondent = respondent.Substring(0, advIdx).Trim();
                            }
                            respondent = Regex.Replace(respondent, @"\bFOR\s*(?:R\d+|\bPROP\.?\b|\bRESPONDENT\b).*", "", RegexOptions.IgnoreCase).Trim();
                        }

                        // Clean advocate from petitioner if embedded
                        if (!string.IsNullOrEmpty(petitioner))
                        {
                            int advIdx = petitioner.IndexOf(advMatch.Value, StringComparison.OrdinalIgnoreCase);
                            if (advIdx >= 0)
                            {
                                petitioner = petitioner.Substring(0, advIdx).Trim();
                            }
                        }
                    }

                    // Strip any attached page header/footer metadata from petitioner and respondent
                    petitioner = Regex.Replace(petitioner, @"Website:http[^\s]*|Page\s+\d+\s+of\s+\d+|COURT\s*HALL\s*NO\s*[:\s]*\d+.*|\d+\s+of\s+\d+.*", " ", RegexOptions.IgnoreCase);
                    respondent = Regex.Replace(respondent, @"Website:http[^\s]*|Page\s+\d+\s+of\s+\d+|COURT\s*HALL\s*NO\s*[:\s]*\d+.*|\d+\s+of\s+\d+.*", " ", RegexOptions.IgnoreCase);

                    petitioner = Regex.Replace(petitioner, @"\s+", " ").Trim();
                    respondent = Regex.Replace(respondent, @"\s+", " ").Trim();

                    if (petitioner.Length > 160) petitioner = petitioner.Substring(0, 160).Trim() + "...";
                    if (respondent.Length > 160) respondent = respondent.Substring(0, 160).Trim() + "...";

                    // Role detection
                    string role = "With NWKRTC";
                    if (Regex.IsMatch(petitioner, @"(?i)(NWKRTC|NKWRTC|NORTH\s*[- ]?WEST|DIVISIONAL\s*CONTROLLER|MANAGING\s*DIRECTOR|MANAGEMENT\s*OF\s*NWKRTC)") ||
                        petitioner.Contains("MANAGEMENTOF NWKRTC", StringComparison.OrdinalIgnoreCase))
                    {
                        role = "By NWKRTC";
                    }
                    else if (Regex.IsMatch(respondent, @"(?i)(NWKRTC|NKWRTC|NORTH\s*[- ]?WEST|DIVISIONAL\s*CONTROLLER|MANAGING\s*DIRECTOR|MANAGEMENT\s*OF\s*NWKRTC)") ||
                             respondent.Contains("MANAGEMENTOF NWKRTC", StringComparison.OrdinalIgnoreCase))
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
                        SubjectCategory = subjectCategory,
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
                        IsTrackedInMyCases = false
                    });

                    // Companion cases extraction (e.g. WITH WP 104857/2024 or CONNECTED WITH MFA 100234/2019)
                    var companionRegex = new Regex(@"(?:WITH|CONNECTED\s+WITH|C/W)[\s:]*([A-Za-z]+(?:\.[A-Za-z]+|\([A-Za-z0-9\-]+\))?(?:\s*NO\.?)?)[\s/]+(\d+/\d{4})", RegexOptions.IgnoreCase);
                    var compMatches = companionRegex.Matches(blockText);
                    foreach (Match cm in compMatches)
                    {
                        string cRawType = cm.Groups[1].Value.Trim().ToUpperInvariant();
                        string cType = Regex.Replace(cRawType, @"\s+NO\.?", "").Trim();
                        string cCaseNoWithYr = cm.Groups[2].Value.Trim();
                        var cParts = cCaseNoWithYr.Split('/');
                        if (cParts.Length == 2)
                        {
                            string cRegNo = cParts[0].Trim();
                            string cRegYr = cParts[1].Trim();
                            string cCnrSynth = $"{estCode}{cType}{cRegNo}{cRegYr}".PadRight(16, '0').Substring(0, 16);
                            list.Add(new PartyCaseSearchResultItem
                            {
                                CNRNumber = cCnrSynth,
                                CaseType = cType,
                                RegNumber = cRegNo,
                                RegYear = cRegYr,
                                PetitionerName = !string.IsNullOrWhiteSpace(petitioner) ? petitioner : (role == "By NWKRTC" ? "The Managing Director, NWKRTC" : "Petitioner"),
                                RespondentName = !string.IsNullOrWhiteSpace(respondent) ? respondent : (role == "Against NWKRTC" ? "The Managing Director, NWKRTC" : "Respondent"),
                                Advocate = advocate,
                                SubjectCategory = subjectCategory,
                                NwkrtcRole = role,
                                MatchedDesignation = role == "By NWKRTC" ? "Petitioner Management" : "Respondent Corporation",
                                EstablishmentName = $"High Court of Karnataka, {benchName}",
                                HighCourtBench = benchName,
                                NextHearingDate = targetDate,
                                CaseStage = stage,
                                CourtHall = $"{courtHall} (Serial No. {serialNo} - Companion)",
                                CauseListNo = causeListNo,
                                CauseListItemNo = serialNo,
                                IsListedToday = true,
                                IsTrackedInMyCases = false
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[ParseHighCourtCauseListPdf] Error parsing PDF for bench {BenchId}", benchId);
            }

            return list;
        }

        private static bool IsNwkrtcRelated(string text, string estCode = "KAHC02")
        {
            if (string.IsNullOrWhiteSpace(text)) return false;

            // 0. EXCLUSIONS: Non-transport government, revenue, tax, and private commercial entities
            if (Regex.IsMatch(text, @"\b(CENTRAL\s+TAX|INCOME\s+TAX|LAND\s+TRIBUNAL|DEPUTY\s+COMMISSIONER|TAHSILDAR|POLICE|GRAM\s+PANCHAYAT|ISPAT|STEEL|SUGAR|HESCOM|BESCOM|KPTCL|MASJID|TEMPLE|WAKF|SPECIAL\s*LAND|LAND\s*ACQUISITION|SLAO)\b", RegexOptions.IgnoreCase) &&
                !Regex.IsMatch(text, @"(NWKRTC|NKWRTC|NORTH\s*[- ]?WEST|DIVISIONAL\s*CONTROLLER|MANAGEMENT\s*OF\s*NWKRTC)", RegexOptions.IgnoreCase))
            {
                return false;
            }

            // Sibling Corporation Blocklist: Exclude if it ONLY belongs to BMTC, KKRTC, NEKRTC, unless NWKRTC/North-West is also explicitly a party (e.g. multi-respondent matters)
            bool hasExplicitNwkrtc = Regex.IsMatch(text, @"(?i)(NWKRTC|NKWRTC|NORTH\s*[- ]?WEST|HUBBALLI|HUBLI|GOKUL\s*ROAD)");
            if (!hasExplicitNwkrtc && Regex.IsMatch(text, @"(?i)(BMTC|KKRTC|NEKRTC|NEKSRTC|BANGALORE\s+METROPOLITAN|BENGALURU\s+METROPOLITAN|KALYANA\s+KARNATAKA|NORTH\s*[- ]?EAST)"))
            {
                return false;
            }

            // High Court subject code: In Dharwad Bench (KAHC02), (L, KSRTC ) and (S, KSRTC ) denote Labour/Service classification, NOT the litigant.
            // Only exclude KSRTC if explicitly written as Bengaluru/Central KSRTC corporation without Dharwad/NWKRTC context.
            bool isDharwadBench = string.Equals(estCode, "KAHC02", StringComparison.OrdinalIgnoreCase);
            if (!isDharwadBench && Regex.IsMatch(text, @"(?i)(KSRTC|KARNATAKA\s+STATE\s+ROAD\s+TRANSPORT|K\.?S\.?R\.?T\.?C\.?)") &&
                !hasExplicitNwkrtc)
            {
                return false;
            }

            // Exclude pure State of Karnataka / Government disputes without NWKRTC / RTC context
            if (Regex.IsMatch(text, @"\b(STATE\s+OF\s+KARNATAKA)\b", RegexOptions.IgnoreCase) &&
                !Regex.IsMatch(text, @"(NWKRTC|NKWRTC|NORTH\s*[- ]?WEST|DIVISIONAL\s*CONTROLLER|DISCIPLINARY\s*AUTHORITY|MANAGEMENT\s*OF\s*NWKRTC|MANAGING\s*DIRECTOR)", RegexOptions.IgnoreCase))
            {
                return false;
            }

            // 1. Direct NWKRTC keywords (highest confidence, including internal insurance & concatenated OCR forms)
            if (Regex.IsMatch(text, @"(?i)(NWKRTC|N\.?W\.?K\.?R\.?T\.?C\.?|NKWRTC|NWKSRTC|NORTH\s*[- ]?WEST|INTERNAL\s+INSURANCE\s+FUND|SELF\s+INSURANCE\s+FUND)") ||
                Regex.IsMatch(text, @"(?i)MANAGEMENT\s*OF?\s*NWKRTC") ||
                Regex.IsMatch(text, @"(?i)MANAGEMENT\s*OF?\s*N\.?W\.?K\.?R\.?T\.?C") ||
                text.Contains("NWKRTC", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // 1b. In non-Dharwad benches (Bengaluru & Kalaburagi), also capture NWKRTC territorial divisions & operating hubs
            if (Regex.IsMatch(text, @"(?i)(HUBBALLI|HUBLI|BELAGAVI|BELGAUM|BAGALKOT|GADAG|HAVERI|UTTARA\s*KANNADA|CHIKKODI|SIRSI|GOKUL\s*ROAD)") &&
                Regex.IsMatch(text, @"(?i)(DIVISIONAL\s*CONTROLLER|DEPOT\s*MANAGER|DISCIPLINARY\s*AUTHORITY|MANAGING\s*DIRECTOR|TRANSPORT|ROAD\s*TRANSPORT|KSRTC|NWKRTC)"))
            {
                return true;
            }

            // In non-Dharwad benches (Bengaluru & Kalaburagi), generic officers without NWKRTC or division context belong to Central KSRTC or KKRTC
            if (!isDharwadBench)
            {
                return false;
            }

            // 2. NWKRTC Officers: Divisional Controller, Depot Manager, CPM, Disciplinary Authority
            if (Regex.IsMatch(text, @"(?i)(DIVISIONAL\s*CONTROLLER|DIVISIONALCONTROLLER|DEPOT\s*MANAGER|DEPOTMANAGER|CHIEF\s*PERSONNEL\s*MANAGER|DISCIPLINARY\s*AUTHORITY)"))
            {
                // Must not be Railway, Police, or Electricity boards
                if (!Regex.IsMatch(text, @"\b(RAILWAY|BESCOM|HESCOM|POLICE)\b", RegexOptions.IgnoreCase))
                {
                    return true;
                }
            }

            // 3. Managing Director or "THE MANAGING" with standing counsel or transport context
            if (Regex.IsMatch(text, @"(?i)(MANAGING\s*DIRECTOR|MANAGINGDIRECTOR|THE\s+MANAGING|\bMANAGING\b)"))
            {
                return true;
            }

            // 4. In Dharwad Bench: Standing Counsel appearing with Management, Controller, Officer, or (L/S, KSRTC) context
            bool hasStandingCounsel = Regex.IsMatch(text, @"(?i)(MADANMOHAN|KHANNUR|HOSMANI|HOSAMANI|HOSAMANE|KINI|KALASOOR|BUDIHAL|BADAWADAGI|BHUTI|MATTI|KALADAGI|MARADI|KUPPELUR)");
            bool hasNwkrtcContext = Regex.IsMatch(text, @"(?i)(MANAGEMENT|MANAGING|CONTROLLER|OFFICER|DISCIPLINARY|KSRTC|DIRECTOR)");
            if (hasStandingCounsel && hasNwkrtcContext)
            {
                return true;
            }

            return false;
        }
    }
}
