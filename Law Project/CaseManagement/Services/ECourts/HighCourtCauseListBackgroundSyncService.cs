using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MVCCaseManagement.Services.ECourts
{
    /// <summary>
    /// Background Hosted Service that automatically scans and caches the High Court daily cause list
    /// for NWKRTC every morning (for today) and every evening (for tomorrow).
    /// Runs seamlessly in the background so whenever users or legal officers open the
    /// High Court Cause List page, the complete 100% accurate list is loaded instantaneously.
    /// </summary>
    public class HighCourtCauseListBackgroundSyncService : BackgroundService
    {
        private readonly IHighCourtCauseListScannerService _scannerService;
        private readonly ILogger<HighCourtCauseListBackgroundSyncService> _logger;

        public HighCourtCauseListBackgroundSyncService(
            IHighCourtCauseListScannerService scannerService,
            ILogger<HighCourtCauseListBackgroundSyncService> logger)
        {
            _scannerService = scannerService;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("[HighCourtCauseListSync] Background worker registered. Delaying initial run 45 seconds.");

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(45), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            // Initial check on boot
            await RunDailyCheckAsync(stoppingToken);

            // Periodic loop every 30 minutes
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromMinutes(30), stoppingToken);
                    await RunDailyCheckAsync(stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[HighCourtCauseListSync] Error during periodic cause list check.");
                }
            }

            _logger.LogInformation("[HighCourtCauseListSync] Background worker stopped.");
        }

        private async Task RunDailyCheckAsync(CancellationToken stoppingToken)
        {
            try
            {
                var now = DateTime.Now;
                _logger.LogInformation("[HighCourtCauseListSync] Running cause list check at {Time:HH:mm:ss}", now);

                // 1. Morning & Daytime check: Ensure today's list is scanned and cached
                if (now.Hour >= 6 && now.Hour <= 15)
                {
                    var today = DateTime.Today;
                    var res = await _scannerService.ScanAndCacheDateAsync("KAHC02", today, stoppingToken);
                    _logger.LogInformation("[HighCourtCauseListSync] Today's cause list check complete for {Date:yyyy-MM-dd}: {Count} NWKRTC matters.", today, res.nwkrtcMatters);
                }

                // 2. Evening check (after 17:30): Ensure tomorrow's list is scanned and pre-cached
                if (now.Hour >= 17 || (now.Hour == 16 && now.Minute >= 30))
                {
                    var tomorrow = DateTime.Today.AddDays(1);
                    // Skip Sundays
                    if (tomorrow.DayOfWeek != DayOfWeek.Sunday)
                    {
                        var res = await _scannerService.ScanAndCacheDateAsync("KAHC02", tomorrow, stoppingToken);
                        _logger.LogInformation("[HighCourtCauseListSync] Tomorrow's cause list pre-cached for {Date:yyyy-MM-dd}: {Count} NWKRTC matters.", tomorrow, res.nwkrtcMatters);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[HighCourtCauseListSync] Error during scheduled cause list sync.");
            }
        }
    }
}

