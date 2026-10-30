using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MVCCaseManagement.Models;

namespace MVCCaseManagement.Services.ECourts
{
    public interface IHighCourtCauseListScannerService
    {
        /// <summary>
        /// Retrieves the daily High Court cause list cases with respect to NWKRTC for targetDate.
        /// If already cached on disk, loads from cache instantaneously (<10ms).
        /// If missing or forceRefresh is true, performs an automated live scan of all sitting benches via NAPIX,
        /// parses the official cause list rosters with UglyToad.PdfPig, and writes the cache.
        /// </summary>
        Task<List<PartyCaseSearchResultItem>> GetOrScanDailyCauseListAsync(
            string estCode,
            DateTime targetDate,
            bool forceRefresh = false,
            CancellationToken ct = default);

        /// <summary>
        /// Triggers a fresh scan and cache update for targetDate across all sitting benches.
        /// Returns total benches scanned, total matters identified, and NWKRTC matters extracted.
        /// </summary>
        Task<(int totalBenches, int totalMatters, int nwkrtcMatters)> ScanAndCacheDateAsync(
            string estCode,
            DateTime targetDate,
            CancellationToken ct = default);
    }
}

