using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using MVCCaseManagement.Models;
using MVCCaseManagement.Utils;

namespace MVCCaseManagement.Services.ECourts
{
    public interface IHighCourtPartySearchStore
    {
        Task<List<PartyCaseSearchResultItem>> GetPartyCasesAsync(
            string estCode = "ALL", 
            string regYear = "ALL", 
            string pendDisp = "P", 
            CancellationToken ct = default);

        Task MergeFreshCasesAsync(
            IEnumerable<PartyCaseSearchResultItem> freshCases, 
            CancellationToken ct = default);

        Task<int> GetTotalCountAsync(CancellationToken ct = default);
    }

    public class HighCourtPartySearchStore : IHighCourtPartySearchStore
    {
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<HighCourtPartySearchStore> _logger;
        private readonly ConcurrentDictionary<string, PartyCaseSearchResultItem> _cases = new(StringComparer.OrdinalIgnoreCase);
        private readonly SemaphoreSlim _initLock = new(1, 1);
        private readonly SemaphoreSlim _saveLock = new(1, 1);
        private bool _isInitialized = false;

        public HighCourtPartySearchStore(IWebHostEnvironment env, ILogger<HighCourtPartySearchStore> logger)
        {
            _env = env;
            _logger = logger;
        }

        private string GetMasterFilePath()
        {
            string primaryDir = Path.Combine(_env.ContentRootPath, "App_Data", "party_search_cache");
            if (!Directory.Exists(primaryDir))
            {
                Directory.CreateDirectory(primaryDir);
            }
            return Path.Combine(primaryDir, "highcourt_nwkrtc_master.json");
        }

        private async Task EnsureInitializedAsync(CancellationToken ct)
        {
            if (_isInitialized && !_cases.IsEmpty) return;

            await _initLock.WaitAsync(ct);
            try
            {
                if (_isInitialized && !_cases.IsEmpty) return;

                string filePath = GetMasterFilePath();
                if (!File.Exists(filePath))
                {
                    // Fallback to application base directory or publish path if running published
                    string fallback = Path.Combine(AppContext.BaseDirectory, "App_Data", "party_search_cache", "highcourt_nwkrtc_master.json");
                    if (File.Exists(fallback))
                    {
                        filePath = fallback;
                    }
                }

                if (File.Exists(filePath))
                {
                    string json = await File.ReadAllTextAsync(filePath, ct);
                    var items = JsonSerializer.Deserialize<List<PartyCaseSearchResultItem>>(json, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    if (items != null && items.Count > 0)
                    {
                        foreach (var itm in items)
                        {
                            // Sanitize placeholder dates (e.g. 5000-01-01, 1900-01-01, 0001-01-01)
                            if (itm.NextHearingDate.HasValue && (itm.NextHearingDate.Value.Year < 2000 || itm.NextHearingDate.Value.Year > 2050))
                            {
                                itm.NextHearingDate = null;
                            }

                            string key = GetItemKey(itm);
                            if (!string.IsNullOrEmpty(key))
                            {
                                _cases[key] = itm;
                            }
                        }
                        _logger.LogInformation("[HighCourtPartySearchStore] Loaded {Count} master NWKRTC cases from {File}", _cases.Count, filePath);
                    }
                }
                else
                {
                    _logger.LogWarning("[HighCourtPartySearchStore] Master store file not found at {File}", filePath);
                }

                _isInitialized = true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[HighCourtPartySearchStore] Failed to initialize master store.");
            }
            finally
            {
                _initLock.Release();
            }
        }

        private static string GetItemKey(PartyCaseSearchResultItem item)
        {
            if (!string.IsNullOrWhiteSpace(item.CNRNumber) && item.CNRNumber.Trim() != "-")
            {
                return item.CNRNumber.Trim().ToUpperInvariant();
            }
            string regKey = $"{item.CaseType}_{item.RegNumber}_{item.RegYear}".Trim().ToUpperInvariant();
            return regKey != "__" ? regKey : "";
        }

        public async Task<List<PartyCaseSearchResultItem>> GetPartyCasesAsync(
            string estCode = "ALL", 
            string regYear = "ALL", 
            string pendDisp = "P", 
            CancellationToken ct = default)
        {
            await EnsureInitializedAsync(ct);

            var query = _cases.Values.AsEnumerable();

            // Strict NWKRTC entity validation (exclude other RTCs like NEKRTC, KSRTC, BMTC, KKRTC)
            query = query.Where(item =>
            {
                if (NwkrtcPartyMatcher.IsOtherCoOrganization(item.PetitionerName) ||
                    NwkrtcPartyMatcher.IsOtherCoOrganization(item.RespondentName))
                    return false;

                if (item.NwkrtcRole == "Other RTC" || item.NwkrtcRole == "Non-Corporation")
                    return false;

                string itemEst = !string.IsNullOrEmpty(item.CNRNumber) && item.CNRNumber.Length >= 6 
                    ? item.CNRNumber.Substring(0, 6).ToUpperInvariant() 
                    : "KAHC02";

                return NwkrtcPartyMatcher.IsNwkrtcEntity(item.PetitionerName, out _, itemEst) ||
                       NwkrtcPartyMatcher.IsNwkrtcEntity(item.RespondentName, out _, itemEst);
            });

            // Establishment Filter
            string cleanEst = (estCode ?? "ALL").Trim().ToUpperInvariant();
            if (cleanEst != "ALL" && !string.IsNullOrEmpty(cleanEst))
            {
                query = query.Where(item =>
                {
                    if (!string.IsNullOrWhiteSpace(item.CNRNumber) && item.CNRNumber.StartsWith(cleanEst, StringComparison.OrdinalIgnoreCase))
                        return true;

                    if (cleanEst == "KAHC01" && ((item.HighCourtBench != null && (item.HighCourtBench.Contains("Bengaluru", StringComparison.OrdinalIgnoreCase) || item.HighCourtBench.Contains("Principal", StringComparison.OrdinalIgnoreCase))) || 
                                                (item.EstablishmentName != null && item.EstablishmentName.Contains("Bengaluru", StringComparison.OrdinalIgnoreCase))))
                        return true;
                    if (cleanEst == "KAHC02" && ((item.HighCourtBench != null && item.HighCourtBench.Contains("Dharwad", StringComparison.OrdinalIgnoreCase)) || 
                                                (item.EstablishmentName != null && item.EstablishmentName.Contains("Dharwad", StringComparison.OrdinalIgnoreCase))))
                        return true;
                    if (cleanEst == "KAHC03" && ((item.HighCourtBench != null && item.HighCourtBench.Contains("Kalaburagi", StringComparison.OrdinalIgnoreCase)) || 
                                                (item.EstablishmentName != null && item.EstablishmentName.Contains("Kalaburagi", StringComparison.OrdinalIgnoreCase))))
                        return true;

                    return false;
                });
            }

            // Registration Year Filter
            string cleanYear = (regYear ?? "ALL").Trim().ToUpperInvariant();
            int currentYr = DateTime.Today.Year;
            if (cleanYear.Contains("RECENT") || cleanYear.Contains("2023–2026"))
            {
                var recentYears = new HashSet<string> { "2023", "2024", "2025", "2026" };
                query = query.Where(item => recentYears.Contains(item.RegYear?.Trim() ?? ""));
            }
            else if (cleanYear.Contains("LAST 5") || cleanYear.Contains("LAST5") || cleanYear.Contains("2022–2026"))
            {
                var yList = Enumerable.Range(currentYr - 4, 5).Select(y => y.ToString()).ToHashSet();
                query = query.Where(item => yList.Contains(item.RegYear?.Trim() ?? ""));
            }
            else if (cleanYear.Contains("LAST 10") || cleanYear.Contains("LAST10") || cleanYear.Contains("2017–2026"))
            {
                var yList = Enumerable.Range(currentYr - 9, 10).Select(y => y.ToString()).ToHashSet();
                query = query.Where(item => yList.Contains(item.RegYear?.Trim() ?? ""));
            }
            else if (cleanYear != "ALL" && !cleanYear.Contains("ACTIVE") && int.TryParse(cleanYear, out int specificYear))
            {
                query = query.Where(item => item.RegYear?.Trim() == specificYear.ToString());
            }

            // Return deep clones with transient IsListedToday reset to false for this specific request
            return query.Select(x => new PartyCaseSearchResultItem
            {
                CNRNumber = x.CNRNumber,
                CaseType = x.CaseType,
                RegNumber = x.RegNumber,
                RegYear = x.RegYear,
                PetitionerName = x.PetitionerName,
                RespondentName = x.RespondentName,
                NwkrtcRole = x.NwkrtcRole,
                MatchedDesignation = x.MatchedDesignation,
                EstablishmentName = x.EstablishmentName,
                HighCourtBench = x.HighCourtBench,
                NextHearingDate = (x.NextHearingDate.HasValue && x.NextHearingDate.Value.Year >= 2000 && x.NextHearingDate.Value.Year <= 2050) ? x.NextHearingDate : null,
                CaseStage = x.CaseStage ?? "Hearing Active",
                CourtHall = x.CourtHall,
                IsLocalMatch = false,
                LocalCaseId = null,
                LocalAppealId = null,
                LocalModule = null,
                IsListedToday = false,
                IsTrackedInMyCases = false,
                TrackedId = null,
                AssignedWorker = null,
                CauseListType = x.CauseListType,
                CauseListNo = x.CauseListNo,
                CauseListItemNo = x.CauseListItemNo,
                JudgeName = x.JudgeName,
                Advocate = x.Advocate,
                RoznamaText = null
            }).ToList();
        }

        public async Task MergeFreshCasesAsync(IEnumerable<PartyCaseSearchResultItem> freshCases, CancellationToken ct = default)
        {
            if (freshCases == null) return;
            await EnsureInitializedAsync(ct);

            bool modified = false;
            foreach (var item in freshCases)
            {
                if (item == null) continue;
                if (item.NextHearingDate.HasValue && (item.NextHearingDate.Value.Year < 2000 || item.NextHearingDate.Value.Year > 2050))
                {
                    item.NextHearingDate = null;
                }

                string key = GetItemKey(item);
                if (string.IsNullOrEmpty(key)) continue;

                if (_cases.TryGetValue(key, out var existing))
                {
                    if (!string.IsNullOrEmpty(item.CourtHall) && existing.CourtHall != item.CourtHall) { existing.CourtHall = item.CourtHall; modified = true; }
                    if (!string.IsNullOrEmpty(item.CauseListNo) && existing.CauseListNo != item.CauseListNo) { existing.CauseListNo = item.CauseListNo; modified = true; }
                    if (!string.IsNullOrEmpty(item.CauseListItemNo) && existing.CauseListItemNo != item.CauseListItemNo) { existing.CauseListItemNo = item.CauseListItemNo; modified = true; }
                    if (!string.IsNullOrEmpty(item.CaseStage) && existing.CaseStage != item.CaseStage) { existing.CaseStage = item.CaseStage; modified = true; }
                    if (!string.IsNullOrEmpty(item.Advocate) && existing.Advocate != item.Advocate) { existing.Advocate = item.Advocate; modified = true; }
                    if (item.NextHearingDate.HasValue && existing.NextHearingDate != item.NextHearingDate) { existing.NextHearingDate = item.NextHearingDate; modified = true; }
                }
                else
                {
                    _cases[key] = item;
                    modified = true;
                }
            }

            if (modified)
            {
                await _saveLock.WaitAsync(ct);
                try
                {
                    string filePath = GetMasterFilePath();
                    string tmpPath = filePath + ".tmp";
                    string json = JsonSerializer.Serialize(_cases.Values.ToList(), new JsonSerializerOptions { WriteIndented = true });
                    await File.WriteAllTextAsync(tmpPath, json, ct);
                    File.Move(tmpPath, filePath, overwrite: true);
                    _logger.LogInformation("[HighCourtPartySearchStore] Master store persisted successfully ({Count} total cases).", _cases.Count);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[HighCourtPartySearchStore] Error saving master store.");
                }
                finally
                {
                    _saveLock.Release();
                }
            }
        }

        public async Task<int> GetTotalCountAsync(CancellationToken ct = default)
        {
            await EnsureInitializedAsync(ct);
            return _cases.Count;
        }
    }
}

