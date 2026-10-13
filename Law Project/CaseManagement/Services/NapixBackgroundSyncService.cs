using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MVCCaseManagement.Utils;

namespace MVCCaseManagement.Services
{
    /// <summary>
    /// Background Hosted Service running two independent bounded workers:
    /// 1. MVC Worker (1,000 calls/hr bucket)
    /// 2. Labour Worker (1,000 calls/hr bucket)
    /// Plus a steady-state periodic scheduler for active cases (PendDispStatus != 'D').
    /// </summary>
    public class NapixBackgroundSyncService : BackgroundService
    {
        private readonly NapixSyncEngine _engine;
        private readonly IConfiguration _config;
        private readonly ILogger<NapixBackgroundSyncService> _logger;
        private readonly string _connectionString;

        public NapixBackgroundSyncService(
            NapixSyncEngine engine,
            IConfiguration config,
            ILogger<NapixBackgroundSyncService> logger)
        {
            _engine = engine;
            _config = config;
            _logger = logger;
            _connectionString = config.GetConnectionString("MVCCaseDB") ?? "";
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("[NapixBackgroundWorker] Service started. Spawning dual MVC and Labour workers.");

            try
            {
                // Run MVC worker, Labour worker, and periodic active case scheduler concurrently
                var mvcTask = RunModuleWorkerAsync("MVC", stoppingToken);
                var labourTask = RunModuleWorkerAsync("Labour", stoppingToken);
                var schedulerTask = RunPeriodicSchedulerAsync(stoppingToken);

                await Task.WhenAll(mvcTask, labourTask, schedulerTask);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("[NapixBackgroundWorker] Service stopped gracefully.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[NapixBackgroundWorker] Unhandled error in background sync workers.");
            }
        }

        /// <summary>
        /// Dedicated bounded worker loop for a specific module app (MVC or Labour).
        /// Processes queue jobs sequentially per module, honoring atomic quota reservations.
        /// </summary>
        private async Task RunModuleWorkerAsync(string module, CancellationToken stoppingToken)
        {
            _logger.LogInformation("[NapixBackgroundWorker] Started worker loop for module: {Module}", module);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var job = await _engine.DequeueJobAsync(module, leaseMinutes: 5);
                    if (job != null)
                    {
                        var result = await _engine.ProcessSyncJobAsync(job, stoppingToken);

                        if (result.IsThrottled)
                        {
                            int delay = Math.Max(5, result.RetryAfterSeconds);
                            _logger.LogInformation("[NapixBackgroundWorker] {Module} quota reached. Pausing worker for {Delay}s.", module, delay);
                            await Task.Delay(TimeSpan.FromSeconds(delay), stoppingToken);
                        }
                        else
                        {
                            // Yield briefly to prevent tight loops on fast local DB updates
                            await Task.Delay(250, stoppingToken);
                        }
                    }
                    else
                    {
                        // Queue empty for this module, sleep before polling again
                        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[NapixBackgroundWorker] Unexpected error in {Module} worker loop. Sleeping 10s before resuming.", module);
                    await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
                }
            }

            _logger.LogInformation("[NapixBackgroundWorker] Stopped worker loop for module: {Module}", module);
        }

        /// <summary>
        /// Steady-state periodic scheduler (Rule 1):
        /// Runs every 30 minutes to discover active cases requiring refresh.
        /// STRICTLY EXCLUDES cases where PendDispStatus = 'D' (Disposed).
        /// </summary>
        private async Task RunPeriodicSchedulerAsync(CancellationToken stoppingToken)
        {
            // Initial delay on startup to allow application initialization
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ScheduleActiveCasesAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[NapixScheduler] Error during periodic active cases scheduling.");
                }

                // Schedule every 30 minutes
                await Task.Delay(TimeSpan.FromMinutes(30), stoppingToken);
            }
        }

        private async Task ScheduleActiveCasesAsync(CancellationToken stoppingToken)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync(stoppingToken);

            // 1. MVC Active Cases: Upcoming hearing in next 3 days OR not synced in 7 days
            // Rule 1: Exclude PendDispStatus = 'D'
            const string sqlMvc = @"
                SELECT TOP 50 CaseID, CNRNumber,
                       CASE WHEN NextHearingDate BETWEEN CAST(GETDATE() AS DATE) AND DATEADD(DAY, 3, CAST(GETDATE() AS DATE)) THEN 2 ELSE 3 END AS Priority
                FROM dbo.MVC_CASES WITH (NOLOCK)
                WHERE CNRNumber IS NOT NULL AND LEN(CNRNumber) = 16
                  AND (PendDispStatus IS NULL OR PendDispStatus <> 'D')
                  AND (
                      (NextHearingDate BETWEEN CAST(GETDATE() AS DATE) AND DATEADD(DAY, 3, CAST(GETDATE() AS DATE)) AND (LastNapixSyncAt IS NULL OR LastNapixSyncAt < DATEADD(DAY, -1, SYSUTCDATETIME())))
                      OR (LastNapixSyncAt IS NULL OR LastNapixSyncAt < DATEADD(DAY, -7, SYSUTCDATETIME()))
                  )
                  AND NOT EXISTS (
                      SELECT 1 FROM dbo.NAPIX_SYNC_QUEUE q WITH (NOLOCK)
                      WHERE q.Module = 'MVC' AND q.CNRNumber = MVC_CASES.CNRNumber AND q.Status IN ('Queued', 'Processing')
                  );";

            using (var cmd = new SqlCommand(sqlMvc, conn))
            using (var rdr = await cmd.ExecuteReaderAsync(stoppingToken))
            {
                while (await rdr.ReadAsync(stoppingToken))
                {
                    int caseId = rdr.GetInt32(0);
                    string cnr = rdr.GetString(1);
                    int priority = rdr.GetInt32(2);

                    await _engine.EnqueueCaseAsync("MVC", caseId, cnr, priority);
                }
            }

            // 2. Labour Active Cases: Upcoming hearing in next 3 days OR not synced in 7 days
            // Rule 1: Exclude PendDispStatus = 'D'
            const string sqlLabour = @"
                SELECT TOP 50 CaseID, CNRNumber,
                       CASE WHEN NextHearingDate BETWEEN CAST(GETDATE() AS DATE) AND DATEADD(DAY, 3, CAST(GETDATE() AS DATE)) THEN 2 ELSE 3 END AS Priority
                FROM LABOUR_CASES WITH (NOLOCK)
                WHERE CNRNumber IS NOT NULL AND LEN(CNRNumber) = 16
                  AND (PendDispStatus IS NULL OR PendDispStatus <> 'D')
                  AND (
                      (NextHearingDate BETWEEN CAST(GETDATE() AS DATE) AND DATEADD(DAY, 3, CAST(GETDATE() AS DATE)) AND (LastNapixSyncAt IS NULL OR LastNapixSyncAt < DATEADD(DAY, -1, SYSUTCDATETIME())))
                      OR (LastNapixSyncAt IS NULL OR LastNapixSyncAt < DATEADD(DAY, -7, SYSUTCDATETIME()))
                  )
                  AND NOT EXISTS (
                      SELECT 1 FROM dbo.NAPIX_SYNC_QUEUE q WITH (NOLOCK)
                      WHERE q.Module = 'Labour' AND q.CNRNumber = LABOUR_CASES.CNRNumber AND q.Status IN ('Queued', 'Processing')
                  );";

            using (var cmd = new SqlCommand(sqlLabour, conn))
            using (var rdr = await cmd.ExecuteReaderAsync(stoppingToken))
            {
                while (await rdr.ReadAsync(stoppingToken))
                {
                    int caseId = rdr.GetInt32(0);
                    string cnr = rdr.GetString(1);
                    int priority = rdr.GetInt32(2);

                    await _engine.EnqueueCaseAsync("Labour", caseId, cnr, priority);
                }
            }

            // 3. Other Courts Active Cases: Upcoming hearing in next 3 days OR not synced in 7 days
            const string sqlOther = @"
                SELECT TOP 50 CaseID, CNRNumber,
                       CASE WHEN NextDateOfHearing BETWEEN CAST(GETDATE() AS DATE) AND DATEADD(DAY, 3, CAST(GETDATE() AS DATE)) THEN 2 ELSE 3 END AS Priority
                FROM OTHER_CASES WITH (NOLOCK)
                WHERE CNRNumber IS NOT NULL AND LEN(CNRNumber) = 16
                  AND (PendDispStatus IS NULL OR PendDispStatus <> 'D')
                  AND (
                      (NextDateOfHearing BETWEEN CAST(GETDATE() AS DATE) AND DATEADD(DAY, 3, CAST(GETDATE() AS DATE)) AND (LastNapixSyncAt IS NULL OR LastNapixSyncAt < DATEADD(DAY, -1, SYSUTCDATETIME())))
                      OR (LastNapixSyncAt IS NULL OR LastNapixSyncAt < DATEADD(DAY, -7, SYSUTCDATETIME()))
                  )
                  AND NOT EXISTS (
                      SELECT 1 FROM dbo.NAPIX_SYNC_QUEUE q WITH (NOLOCK)
                      WHERE q.Module = 'OtherCourts' AND q.CNRNumber = OTHER_CASES.CNRNumber AND q.Status IN ('Queued', 'Processing')
                  );";

            using (var cmd = new SqlCommand(sqlOther, conn))
            using (var rdr = await cmd.ExecuteReaderAsync(stoppingToken))
            {
                while (await rdr.ReadAsync(stoppingToken))
                {
                    int caseId = rdr.GetInt32(0);
                    string cnr = rdr.GetString(1);
                    int priority = rdr.GetInt32(2);

                    await _engine.EnqueueCaseAsync("OtherCourts", caseId, cnr, priority);
                }
            }
        }
    }
}
