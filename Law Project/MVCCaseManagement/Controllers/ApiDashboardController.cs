using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using MVCCaseManagement.Utils;

namespace MVCCaseManagement.Controllers
{
    [Authorize(Roles = "Admin,CO,CLO,MD,LO,Dy CLO,DyCLO")]
    public class ApiDashboardController : Controller
    {
        private readonly NapixQuotaService _quota;
        private readonly string _connStr;
        private readonly int _hourlyQuota;
        private readonly NapixSyncEngine _syncEngine;

        public ApiDashboardController(NapixQuotaService quota, IConfiguration config, NapixSyncEngine syncEngine)
        {
            _quota       = quota;
            _connStr     = config.GetConnectionString("MVCCaseDB") ?? "";
            _hourlyQuota = config.GetValue<int>("eCourts:HourlyQuota", 1000);
            _syncEngine  = syncEngine;
        }

        // GET /ApiDashboard/DryRun
        [HttpGet]
        public async Task<IActionResult> DryRun()
        {
            using var conn = new SqlConnection(_connStr);
            await conn.OpenAsync();

            const string sql = @"
                -- MVC Stats
                SELECT 
                    COUNT(*) AS TotalMvc,
                    SUM(CASE WHEN CNRNumber IS NOT NULL AND LEN(CNRNumber) = 16 THEN 1 ELSE 0 END) AS MvcWithCnr,
                    SUM(CASE WHEN LastNapixSyncAt IS NOT NULL THEN 1 ELSE 0 END) AS MvcSynced,
                    SUM(CASE WHEN PendDispStatus = 'D' THEN 1 ELSE 0 END) AS MvcDisposed,
                    SUM(CASE WHEN PendDispStatus = 'P' THEN 1 ELSE 0 END) AS MvcPending,
                    SUM(CASE WHEN LastNapixSyncAt IS NULL AND CNRNumber IS NOT NULL AND LEN(CNRNumber) = 16 THEN 1 ELSE 0 END) AS MvcEligibleBackfill
                FROM dbo.MVC_CASES WITH (NOLOCK);

                -- Labour Stats
                SELECT 
                    COUNT(*) AS TotalLabour,
                    SUM(CASE WHEN CNRNumber IS NOT NULL AND LEN(CNRNumber) = 16 THEN 1 ELSE 0 END) AS LabourWithCnr,
                    SUM(CASE WHEN LastNapixSyncAt IS NOT NULL THEN 1 ELSE 0 END) AS LabourSynced,
                    SUM(CASE WHEN PendDispStatus = 'D' THEN 1 ELSE 0 END) AS LabourDisposed,
                    SUM(CASE WHEN PendDispStatus = 'P' THEN 1 ELSE 0 END) AS LabourPending,
                    SUM(CASE WHEN LastNapixSyncAt IS NULL AND CNRNumber IS NOT NULL AND LEN(CNRNumber) = 16 THEN 1 ELSE 0 END) AS LabourEligibleBackfill
                FROM LABOUR_CASES WITH (NOLOCK);

                -- Active Queue Stats
                SELECT 
                    Module,
                    Status,
                    COUNT(*) AS Count
                FROM dbo.NAPIX_SYNC_QUEUE WITH (NOLOCK)
                WHERE Status IN ('Queued', 'Processing')
                GROUP BY Module, Status;

                -- Actual Call Throughput in past 15 min
                SELECT 
                    Module,
                    COUNT(*) AS CallsPast15Min,
                    SUM(CASE WHEN IsSuccess = 1 THEN 1 ELSE 0 END) AS SuccessPast15Min,
                    AVG(DurationMs) AS AvgDurationMs
                FROM dbo.NAPIX_API_CALLS WITH (NOLOCK)
                WHERE CalledAt >= DATEADD(MINUTE, -15, SYSUTCDATETIME())
                GROUP BY Module;";

            using var cmd = new SqlCommand(sql, conn);
            using var rdr = await cmd.ExecuteReaderAsync();

            var mvcStats = new ModuleBackfillStat { Module = "MVC" };
            if (await rdr.ReadAsync())
            {
                mvcStats.TotalCases = rdr.GetInt32(0);
                mvcStats.CasesWithCnr = rdr.IsDBNull(1) ? 0 : rdr.GetInt32(1);
                mvcStats.CasesSynced = rdr.IsDBNull(2) ? 0 : rdr.GetInt32(2);
                mvcStats.AuthoritativeDisposed = rdr.IsDBNull(3) ? 0 : rdr.GetInt32(3);
                mvcStats.AuthoritativePending = rdr.IsDBNull(4) ? 0 : rdr.GetInt32(4);
                mvcStats.EligibleForBackfill = rdr.IsDBNull(5) ? 0 : rdr.GetInt32(5);
            }

            await rdr.NextResultAsync();
            var labourStats = new ModuleBackfillStat { Module = "Labour" };
            if (await rdr.ReadAsync())
            {
                labourStats.TotalCases = rdr.GetInt32(0);
                labourStats.CasesWithCnr = rdr.IsDBNull(1) ? 0 : rdr.GetInt32(1);
                labourStats.CasesSynced = rdr.IsDBNull(2) ? 0 : rdr.GetInt32(2);
                labourStats.AuthoritativeDisposed = rdr.IsDBNull(3) ? 0 : rdr.GetInt32(3);
                labourStats.AuthoritativePending = rdr.IsDBNull(4) ? 0 : rdr.GetInt32(4);
                labourStats.EligibleForBackfill = rdr.IsDBNull(5) ? 0 : rdr.GetInt32(5);
            }

            await rdr.NextResultAsync();
            int mvcQueued = 0, mvcProcessing = 0, labourQueued = 0, labourProcessing = 0;
            while (await rdr.ReadAsync())
            {
                string mod = rdr.GetString(0);
                string st = rdr.GetString(1);
                int cnt = rdr.GetInt32(2);
                if (mod.Equals("Labour", StringComparison.OrdinalIgnoreCase))
                {
                    if (st == "Queued") labourQueued = cnt;
                    else if (st == "Processing") labourProcessing = cnt;
                }
                else
                {
                    if (st == "Queued") mvcQueued = cnt;
                    else if (st == "Processing") mvcProcessing = cnt;
                }
            }

            mvcStats.ActiveQueued = mvcQueued;
            mvcStats.ActiveProcessing = mvcProcessing;
            labourStats.ActiveQueued = labourQueued;
            labourStats.ActiveProcessing = labourProcessing;

            await rdr.NextResultAsync();
            double mvcRate = 0, labourRate = 0;
            while (await rdr.ReadAsync())
            {
                string mod = rdr.IsDBNull(0) ? "MVC" : rdr.GetString(0);
                int calls15 = rdr.GetInt32(1);
                double ratePerMin = calls15 / 15.0;
                if (mod.Equals("Labour", StringComparison.OrdinalIgnoreCase)) labourRate = ratePerMin;
                else mvcRate = ratePerMin;
            }

            // Estimate completion time (Rule 18)
            double effectiveMvcRate = mvcRate > 0 ? Math.Min(mvcRate, 16.6) : 12.0;
            double effectiveLabourRate = labourRate > 0 ? Math.Min(labourRate, 16.6) : 12.0;

            int mvcRemainingWorkload = mvcStats.EligibleForBackfill + mvcStats.ActiveQueued;
            int labourRemainingWorkload = labourStats.EligibleForBackfill + labourStats.ActiveQueued;

            double mvcMinutes = effectiveMvcRate > 0 ? (mvcRemainingWorkload / effectiveMvcRate) : 0;
            double labourMinutes = effectiveLabourRate > 0 ? (labourRemainingWorkload / effectiveLabourRate) : 0;

            return Json(new
            {
                success = true,
                mvc = mvcStats,
                labour = labourStats,
                combined = new
                {
                    totalCases = mvcStats.TotalCases + labourStats.TotalCases,
                    casesWithCnr = mvcStats.CasesWithCnr + labourStats.CasesWithCnr,
                    casesSynced = mvcStats.CasesSynced + labourStats.CasesSynced,
                    eligibleBackfill = mvcStats.EligibleForBackfill + labourStats.EligibleForBackfill,
                    activeQueue = mvcStats.ActiveQueued + labourStats.ActiveQueued + mvcStats.ActiveProcessing + labourStats.ActiveProcessing,
                    estimatedHours = Math.Round(Math.Max(mvcMinutes, labourMinutes) / 60.0, 1),
                    estimatedMinutes = Math.Round(Math.Max(mvcMinutes, labourMinutes), 0)
                }
            });
        }

        // POST /ApiDashboard/QueueBatch
        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> QueueBatch([FromForm] string mode)
        {
            // Modes: Pilot10, Staged100, Full
            int limitPerModule = mode switch
            {
                "Pilot10" => 10,
                "Staged100" => 100,
                "Full" => 10000,
                _ => 10
            };

            int mvcEnqueued = 0;
            int labourEnqueued = 0;

            using (var conn = new SqlConnection(_connStr))
            {
                await conn.OpenAsync();

                // Select eligible unsynced MVC cases (including disposed cases per Rule 4!)
                // Rule 17: Backfill Idempotency — ignore if already active in queue
                string sqlMvc = @"
                    SELECT TOP (@Limit) CaseID, CNRNumber
                    FROM dbo.MVC_CASES WITH (NOLOCK)
                    WHERE CNRNumber IS NOT NULL AND LEN(CNRNumber) = 16
                      AND LastNapixSyncAt IS NULL
                      AND NOT EXISTS (
                          SELECT 1 FROM dbo.NAPIX_SYNC_QUEUE q WITH (NOLOCK)
                          WHERE q.Module = 'MVC' AND q.CNRNumber = MVC_CASES.CNRNumber AND q.Status IN ('Queued', 'Processing')
                      )
                    ORDER BY CaseID ASC;";

                using (var cmd = new SqlCommand(sqlMvc, conn))
                {
                    cmd.Parameters.AddWithValue("@Limit", limitPerModule);
                    using var rdr = await cmd.ExecuteReaderAsync();
                    var mvcCases = new List<(int id, string cnr)>();
                    while (await rdr.ReadAsync())
                    {
                        mvcCases.Add((rdr.GetInt32(0), rdr.GetString(1)));
                    }
                    rdr.Close();

                    foreach (var (cId, cnr) in mvcCases)
                    {
                        var (ok, _, _) = await _syncEngine.EnqueueCaseAsync("MVC", cId, cnr, priority: mode == "Full" ? 4 : 3);
                        if (ok) mvcEnqueued++;
                    }
                }

                // Select eligible unsynced Labour cases (including disposed cases per Rule 4!)
                string sqlLabour = @"
                    SELECT TOP (@Limit) CaseID, CNRNumber
                    FROM LABOUR_CASES WITH (NOLOCK)
                    WHERE CNRNumber IS NOT NULL AND LEN(CNRNumber) = 16
                      AND LastNapixSyncAt IS NULL
                      AND NOT EXISTS (
                          SELECT 1 FROM dbo.NAPIX_SYNC_QUEUE q WITH (NOLOCK)
                          WHERE q.Module = 'Labour' AND q.CNRNumber = LABOUR_CASES.CNRNumber AND q.Status IN ('Queued', 'Processing')
                      )
                    ORDER BY CaseID ASC;";

                using (var cmd = new SqlCommand(sqlLabour, conn))
                {
                    cmd.Parameters.AddWithValue("@Limit", limitPerModule);
                    using var rdr = await cmd.ExecuteReaderAsync();
                    var labourCases = new List<(int id, string cnr)>();
                    while (await rdr.ReadAsync())
                    {
                        labourCases.Add((rdr.GetInt32(0), rdr.GetString(1)));
                    }
                    rdr.Close();

                    foreach (var (cId, cnr) in labourCases)
                    {
                        var (ok, _, _) = await _syncEngine.EnqueueCaseAsync("Labour", cId, cnr, priority: mode == "Full" ? 4 : 3);
                        if (ok) labourEnqueued++;
                    }
                }
            }

            return Json(new
            {
                success = true,
                mode = mode,
                mvcEnqueued = mvcEnqueued,
                labourEnqueued = labourEnqueued,
                totalEnqueued = mvcEnqueued + labourEnqueued,
                message = $"Successfully queued {mvcEnqueued} MVC cases and {labourEnqueued} Labour cases."
            });
        }

        // GET /ApiDashboard
        public async Task<IActionResult> Index()
        {
            var vm = await BuildDashboardVmAsync();
            return View(vm);
        }

        // GET /ApiDashboard/Data  (JSON for live auto-refresh + sidebar dot)
        [HttpGet]
        public async Task<IActionResult> Data()
        {
            var vm = await BuildDashboardVmAsync();
            return Json(vm);
        }

        // POST /ApiDashboard/ResetCache
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ResetCache(string? module = null)
        {
            _quota.InvalidateCache(module);
            TempData["SuccessMessage"] = "NAPIX quota cache refreshed successfully.";
            return RedirectToAction(nameof(Index));
        }

        private static bool _sprocChecked = false;
        private static readonly object _sprocLock = new object();

        private void EnsureLatestSproc(SqlConnection conn)
        {
            if (_sprocChecked) return;
            lock (_sprocLock)
            {
                if (_sprocChecked) return;
                try
                {
                    string dropSql = "IF OBJECT_ID('dbo.usp_GetNapixQuotaSummary', 'P') IS NOT NULL DROP PROCEDURE dbo.usp_GetNapixQuotaSummary;";
                    using (var dropCmd = new SqlCommand(dropSql, conn))
                    {
                        dropCmd.ExecuteNonQuery();
                    }

                    string sprocSql = @"
                        CREATE PROCEDURE dbo.usp_GetNapixQuotaSummary
                        AS
                        BEGIN
                            SET NOCOUNT ON;
                            DECLARE @HourStart DATETIME2 = DATEADD(HOUR, -1, SYSUTCDATETIME());

                            -- 1. Combined System Quota across 3 apps (3000 total capacity)
                            SELECT
                                COUNT(*) AS TotalCallsThisHour,
                                ISNULL(SUM(CASE WHEN IsSuccess=1 THEN 1 ELSE 0 END), 0) AS SuccessfulCalls,
                                ISNULL(SUM(CASE WHEN IsSuccess=0 THEN 1 ELSE 0 END), 0) AS FailedCalls,
                                3000 AS HourlyQuota,
                                3000 - COUNT(*) AS RemainingQuota,
                                CAST(COUNT(*) * 100.0 / 3000 AS DECIMAL(5,1)) AS UsedPercent
                            FROM dbo.NAPIX_API_CALLS
                            WHERE CalledAt >= @HourStart;

                            -- 2. Per-Module Breakdown (MVC, Labour, OtherCourts - 1000 each)
                            SELECT
                                m.ModuleName AS Module,
                                ISNULL(c.TotalCalls, 0) AS TotalCallsThisHour,
                                ISNULL(c.SuccessfulCalls, 0) AS SuccessfulCalls,
                                ISNULL(c.FailedCalls, 0) AS FailedCalls,
                                1000 AS HourlyQuota,
                                1000 - ISNULL(c.TotalCalls, 0) AS RemainingQuota,
                                CAST(ISNULL(c.TotalCalls, 0) * 100.0 / 1000 AS DECIMAL(5,1)) AS UsedPercent
                            FROM (VALUES ('MVC'), ('Labour'), ('OtherCourts')) AS m(ModuleName)
                            LEFT JOIN (
                                SELECT
                                    CASE 
                                        WHEN UPPER(Module) LIKE '%OTHER%' OR UPPER(Module) IN ('OS','PSC','CC','CONSUMER','LAC','ECA') THEN 'OtherCourts'
                                        WHEN UPPER(Module) LIKE '%LABOUR%' THEN 'Labour'
                                        ELSE 'MVC'
                                    END AS ModuleName,
                                    COUNT(*) AS TotalCalls,
                                    ISNULL(SUM(CASE WHEN IsSuccess=1 THEN 1 ELSE 0 END), 0) AS SuccessfulCalls,
                                    ISNULL(SUM(CASE WHEN IsSuccess=0 THEN 1 ELSE 0 END), 0) AS FailedCalls
                                FROM dbo.NAPIX_API_CALLS
                                WHERE CalledAt >= @HourStart
                                GROUP BY 
                                    CASE 
                                        WHEN UPPER(Module) LIKE '%OTHER%' OR UPPER(Module) IN ('OS','PSC','CC','CONSUMER','LAC','ECA') THEN 'OtherCourts'
                                        WHEN UPPER(Module) LIKE '%LABOUR%' THEN 'Labour'
                                        ELSE 'MVC'
                                    END
                            ) c ON m.ModuleName = c.ModuleName;

                            -- 3. Per-endpoint breakdown
                            SELECT
                                Endpoint,
                                Module,
                                COUNT(*) AS TotalCalls,
                                ISNULL(SUM(CASE WHEN IsSuccess=1 THEN 1 ELSE 0 END), 0) AS SuccessCalls,
                                ISNULL(SUM(CASE WHEN IsSuccess=0 THEN 1 ELSE 0 END), 0) AS FailedCalls,
                                ISNULL(AVG(DurationMs), 0) AS AvgDurationMs
                            FROM dbo.NAPIX_API_CALLS
                            WHERE CalledAt >= @HourStart
                            GROUP BY Endpoint, Module
                            ORDER BY TotalCalls DESC;

                            -- 4. Per-minute trend
                            SELECT
                                DATEPART(MINUTE, DATEADD(HOUR,5,DATEADD(MINUTE,30,CalledAt))) AS MinuteIST,
                                DATEPART(HOUR, DATEADD(HOUR,5,DATEADD(MINUTE,30,CalledAt))) AS HourIST,
                                CAST(DATEADD(HOUR,5,DATEADD(MINUTE,30,CalledAt)) AS DATE) AS DateIST,
                                COUNT(*) AS CallCount
                            FROM dbo.NAPIX_API_CALLS
                            WHERE CalledAt >= @HourStart
                            GROUP BY
                                DATEPART(MINUTE, DATEADD(HOUR,5,DATEADD(MINUTE,30,CalledAt))),
                                DATEPART(HOUR, DATEADD(HOUR,5,DATEADD(MINUTE,30,CalledAt))),
                                CAST(DATEADD(HOUR,5,DATEADD(MINUTE,30,CalledAt)) AS DATE)
                            ORDER BY DateIST, HourIST, MinuteIST;

                            -- 5. Hourly trend
                            SELECT
                                DATEPART(HOUR, DATEADD(HOUR,5,DATEADD(MINUTE,30,CalledAt))) AS HourIST,
                                CAST(DATEADD(HOUR,5,DATEADD(MINUTE,30,CalledAt)) AS DATE) AS DateIST,
                                COUNT(*) AS CallCount
                            FROM dbo.NAPIX_API_CALLS
                            WHERE CalledAt >= DATEADD(HOUR,-24,SYSUTCDATETIME())
                            GROUP BY
                                DATEPART(HOUR, DATEADD(HOUR,5,DATEADD(MINUTE,30,CalledAt))),
                                CAST(DATEADD(HOUR,5,DATEADD(MINUTE,30,CalledAt)) AS DATE)
                            ORDER BY DateIST, HourIST;

                            -- 6. Recent 50 calls
                            SELECT TOP 50
                                CallID,
                                Module,
                                DATEADD(HOUR,5,DATEADD(MINUTE,30,CalledAt)) AS CalledAtIST,
                                Endpoint, HttpStatus, IsSuccess, Username, CNRNumber, DurationMs
                            FROM dbo.NAPIX_API_CALLS
                            ORDER BY CallID DESC;
                        END;";

                    using (var createCmd = new SqlCommand(sprocSql, conn))
                    {
                        createCmd.ExecuteNonQuery();
                    }
                    _sprocChecked = true;
                }
                catch
                {
                    // Non-fatal: dynamic parser handles legacy schemas gracefully
                }
            }
        }

        private static string SafeGetString(SqlDataReader rdr, int index, string fallback = "")
        {
            if (index >= rdr.FieldCount || rdr.IsDBNull(index)) return fallback;
            return rdr.GetValue(index)?.ToString() ?? fallback;
        }

        private static int SafeGetInt32(SqlDataReader rdr, int index, int fallback = 0)
        {
            if (index >= rdr.FieldCount || rdr.IsDBNull(index)) return fallback;
            var val = rdr.GetValue(index);
            if (val is int i) return i;
            if (val is long l) return (int)l;
            if (val is short s) return s;
            if (val is decimal d) return (int)d;
            if (int.TryParse(val?.ToString(), out int parsed)) return parsed;
            return fallback;
        }

        private static double SafeGetDouble(SqlDataReader rdr, int index, double fallback = 0.0)
        {
            if (index >= rdr.FieldCount || rdr.IsDBNull(index)) return fallback;
            var val = rdr.GetValue(index);
            if (val is decimal d) return (double)d;
            if (val is double dbl) return dbl;
            if (val is float f) return f;
            if (val is int i) return i;
            if (double.TryParse(val?.ToString(), out double parsed)) return parsed;
            return fallback;
        }

        private static DateTime SafeGetDateTime(SqlDataReader rdr, int index)
        {
            if (index >= rdr.FieldCount || rdr.IsDBNull(index)) return DateTime.UtcNow;
            var val = rdr.GetValue(index);
            if (val is DateTime dt) return dt;
            if (DateTime.TryParse(val?.ToString(), out var parsed)) return parsed;
            return DateTime.UtcNow;
        }

        private async Task<ApiDashboardVm> BuildDashboardVmAsync()
        {
            var vm = new ApiDashboardVm { HourlyQuota = _hourlyQuota * 2 }; // Combined system quota = 2,000 calls/hr

            try
            {
                using var conn = new SqlConnection(_connStr);
                await conn.OpenAsync();

                // Ensure the 6-result-set stored procedure is deployed
                EnsureLatestSproc(conn);

                using var cmd = new SqlCommand("dbo.usp_GetNapixQuotaSummary", conn)
                    { CommandType = CommandType.StoredProcedure, CommandTimeout = 15 };
                using var rdr = await cmd.ExecuteReaderAsync();

                do
                {
                    if (rdr.FieldCount == 0) continue;
                    string col0 = rdr.GetName(0);

                    // 1. Combined System Quota
                    if (string.Equals(col0, "TotalCallsThisHour", StringComparison.OrdinalIgnoreCase))
                    {
                        if (await rdr.ReadAsync())
                        {
                            vm.TotalCallsThisHour = SafeGetInt32(rdr, 0);
                            vm.SuccessfulCalls    = SafeGetInt32(rdr, 1);
                            vm.FailedCalls        = SafeGetInt32(rdr, 2);
                            vm.HourlyQuota        = SafeGetInt32(rdr, 3, _hourlyQuota * 2);
                            vm.RemainingQuota     = SafeGetInt32(rdr, 4);
                            vm.UsedPercent        = SafeGetDouble(rdr, 5);
                        }
                    }
                    // 2. Per-Module Breakdown
                    else if (string.Equals(col0, "Module", StringComparison.OrdinalIgnoreCase) && rdr.FieldCount >= 7)
                    {
                        while (await rdr.ReadAsync())
                        {
                            vm.ModuleBreakdown.Add(new ModuleQuotaStat
                            {
                                Module             = SafeGetString(rdr, 0, "MVC"),
                                TotalCallsThisHour = SafeGetInt32(rdr, 1),
                                SuccessfulCalls    = SafeGetInt32(rdr, 2),
                                FailedCalls        = SafeGetInt32(rdr, 3),
                                HourlyQuota        = SafeGetInt32(rdr, 4, 1000),
                                RemainingQuota     = SafeGetInt32(rdr, 5, 1000),
                                UsedPercent        = SafeGetDouble(rdr, 6)
                            });
                        }
                    }
                    // 3. Per-Endpoint Breakdown
                    else if (string.Equals(col0, "Endpoint", StringComparison.OrdinalIgnoreCase))
                    {
                        while (await rdr.ReadAsync())
                        {
                            bool hasModuleCol = rdr.FieldCount >= 6 && string.Equals(rdr.GetName(1), "Module", StringComparison.OrdinalIgnoreCase);
                            vm.EndpointBreakdown.Add(new EndpointStat
                            {
                                Endpoint      = SafeGetString(rdr, 0),
                                Module        = hasModuleCol ? SafeGetString(rdr, 1, "MVC") : "MVC",
                                TotalCalls    = SafeGetInt32(rdr, hasModuleCol ? 2 : 1),
                                SuccessCalls  = SafeGetInt32(rdr, hasModuleCol ? 3 : 2),
                                FailedCalls   = SafeGetInt32(rdr, hasModuleCol ? 4 : 3),
                                AvgDurationMs = SafeGetInt32(rdr, hasModuleCol ? 5 : 4)
                            });
                        }
                    }
                    // 4. Per-Minute Trend
                    else if (string.Equals(col0, "MinuteIST", StringComparison.OrdinalIgnoreCase))
                    {
                        while (await rdr.ReadAsync())
                        {
                            vm.MinuteTrend.Add(new MinuteTrendPoint
                            {
                                Minute    = SafeGetInt32(rdr, 0),
                                Hour      = SafeGetInt32(rdr, 1),
                                Date      = SafeGetDateTime(rdr, 2),
                                CallCount = SafeGetInt32(rdr, 3)
                            });
                        }
                    }
                    // 5. Hourly Trend
                    else if (string.Equals(col0, "HourIST", StringComparison.OrdinalIgnoreCase))
                    {
                        while (await rdr.ReadAsync())
                        {
                            vm.HourlyTrend.Add(new HourlyTrendPoint
                            {
                                Hour      = SafeGetInt32(rdr, 0),
                                Date      = SafeGetDateTime(rdr, 1),
                                CallCount = SafeGetInt32(rdr, 2)
                            });
                        }
                    }
                    // 6. Recent Calls
                    else if (string.Equals(col0, "CallID", StringComparison.OrdinalIgnoreCase))
                    {
                        while (await rdr.ReadAsync())
                        {
                            bool hasModuleCol = rdr.FieldCount >= 9 && string.Equals(rdr.GetName(1), "Module", StringComparison.OrdinalIgnoreCase);
                            vm.RecentCalls.Add(new RecentCallRow
                            {
                                CallID     = rdr.GetInt64(0),
                                Module     = hasModuleCol ? SafeGetString(rdr, 1, "MVC") : "MVC",
                                CalledAt   = SafeGetDateTime(rdr, hasModuleCol ? 2 : 1),
                                Endpoint   = SafeGetString(rdr, hasModuleCol ? 3 : 2),
                                HttpStatus = rdr.IsDBNull(hasModuleCol ? 4 : 3) ? null : SafeGetInt32(rdr, hasModuleCol ? 4 : 3),
                                IsSuccess  = rdr.GetBoolean(hasModuleCol ? 5 : 4),
                                Username   = rdr.IsDBNull(hasModuleCol ? 6 : 5) ? null : SafeGetString(rdr, hasModuleCol ? 6 : 5),
                                CNRNumber  = rdr.IsDBNull(hasModuleCol ? 7 : 6) ? null : SafeGetString(rdr, hasModuleCol ? 7 : 6),
                                DurationMs = rdr.IsDBNull(hasModuleCol ? 8 : 7) ? null : SafeGetInt32(rdr, hasModuleCol ? 8 : 7)
                            });
                        }
                    }
                } while (await rdr.NextResultAsync());

                // Fallback guarantee for module breakdown if older sproc is running
                if (vm.ModuleBreakdown.Count == 0)
                {
                    vm.ModuleBreakdown.Add(new ModuleQuotaStat
                    {
                        Module             = "MVC",
                        TotalCallsThisHour = vm.TotalCallsThisHour,
                        SuccessfulCalls    = vm.SuccessfulCalls,
                        FailedCalls        = vm.FailedCalls,
                        HourlyQuota        = 1000,
                        RemainingQuota     = Math.Max(0, 1000 - vm.TotalCallsThisHour),
                        UsedPercent        = Math.Round(vm.TotalCallsThisHour * 100.0 / 1000.0, 1)
                    });
                    vm.ModuleBreakdown.Add(new ModuleQuotaStat
                    {
                        Module             = "Labour",
                        TotalCallsThisHour = 0,
                        SuccessfulCalls    = 0,
                        FailedCalls        = 0,
                        HourlyQuota        = 1000,
                        RemainingQuota     = 1000,
                        UsedPercent        = 0.0
                    });
                }
            }
            catch (Exception ex)
            {
                vm.Error = $"Database error: {ex.Message}";
            }

            return vm;
        }
    }

    // ── View models ──────────────────────────────────────────────────────────────

    public class ApiDashboardVm
    {
        public int    TotalCallsThisHour { get; set; }
        public int    SuccessfulCalls    { get; set; }
        public int    FailedCalls        { get; set; }
        public int    HourlyQuota        { get; set; }
        public int    RemainingQuota     { get; set; }
        public double UsedPercent        { get; set; }
        public string? Error             { get; set; }

        public List<ModuleQuotaStat>  ModuleBreakdown   { get; set; } = new();
        public List<EndpointStat>    EndpointBreakdown { get; set; } = new();
        public List<MinuteTrendPoint> MinuteTrend      { get; set; } = new();
        public List<HourlyTrendPoint> HourlyTrend      { get; set; } = new();
        public List<RecentCallRow>    RecentCalls       { get; set; } = new();
    }

    public class ModuleQuotaStat
    {
        public string Module             { get; set; } = "MVC";
        public int    TotalCallsThisHour { get; set; }
        public int    SuccessfulCalls    { get; set; }
        public int    FailedCalls        { get; set; }
        public int    HourlyQuota        { get; set; } = 1000;
        public int    RemainingQuota     { get; set; } = 1000;
        public double UsedPercent        { get; set; }
    }

    public class EndpointStat
    {
        public string Endpoint      { get; set; } = "";
        public string Module        { get; set; } = "MVC";
        public int    TotalCalls    { get; set; }
        public int    SuccessCalls  { get; set; }
        public int    FailedCalls   { get; set; }
        public int    AvgDurationMs { get; set; }
    }

    public class MinuteTrendPoint
    {
        public int      Minute    { get; set; }
        public int      Hour      { get; set; }
        public DateTime Date      { get; set; }
        public int      CallCount { get; set; }
    }

    public class HourlyTrendPoint
    {
        public int      Hour      { get; set; }
        public DateTime Date      { get; set; }
        public int      CallCount { get; set; }
    }

    public class RecentCallRow
    {
        public long     CallID     { get; set; }
        public string   Module     { get; set; } = "MVC";
        public DateTime CalledAt   { get; set; }
        public string   Endpoint   { get; set; } = "";
        public int?     HttpStatus { get; set; }
        public bool     IsSuccess  { get; set; }
        public string?  Username   { get; set; }
        public string?  CNRNumber  { get; set; }
        public int?     DurationMs { get; set; }
    }

    public class ModuleBackfillStat
    {
        public string Module { get; set; } = "MVC";
        public int TotalCases { get; set; }
        public int CasesWithCnr { get; set; }
        public int CasesSynced { get; set; }
        public int AuthoritativeDisposed { get; set; }
        public int AuthoritativePending { get; set; }
        public int EligibleForBackfill { get; set; }
        public int ActiveQueued { get; set; }
        public int ActiveProcessing { get; set; }
    }
}
