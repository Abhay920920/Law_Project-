using System;
using System.Collections.Concurrent;
using System.Data;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace MVCCaseManagement.Utils
{
    /// <summary>
    /// Singleton service that:
    ///   1. Enforces a ROLLING 1-HOUR quota (1000 calls/hr per module app) before every NAPIX call.
    ///   2. Logs every call (endpoint, HTTP status, duration, user, CNR, module) to NAPIX_API_CALLS.
    ///   3. Caches rolling counts per module in-memory for 15 seconds to avoid DB hits per request.
    /// </summary>
    public class NapixQuotaService
    {
        private readonly string _connectionString;
        private readonly ILogger<NapixQuotaService> _logger;
        private readonly IConfiguration _config;
        private readonly int _defaultHourlyQuota;

        private class ModuleCache
        {
            public int Count { get; set; } = -1;
            public DateTime Expiry { get; set; } = DateTime.MinValue;
            public SemaphoreSlim Lock { get; } = new SemaphoreSlim(1, 1);
        }

        private readonly ConcurrentDictionary<string, ModuleCache> _moduleCaches = new(StringComparer.OrdinalIgnoreCase);

        public NapixQuotaService(IConfiguration config, ILogger<NapixQuotaService> logger)
        {
            _config           = config;
            _connectionString = config.GetConnectionString("MVCCaseDB") ?? "";
            _defaultHourlyQuota = config.GetValue<int>("eCourts:HourlyQuota", 1000);
            _logger           = logger;
        }

        // ── Public API ─────────────────────────────────────────────────────────

        public int HourlyQuota => _defaultHourlyQuota;

        public int GetHourlyQuota(string module = "MVC")
        {
            string cleanModule = NormalizeModule(module);
            return _config.GetValue<int>($"eCourts:{cleanModule}:HourlyQuota", _defaultHourlyQuota);
        }

        /// <summary>Checks whether rolling quota is available for the specified module.</summary>
        public async Task<bool> IsQuotaAvailableAsync(string module = "MVC")
        {
            string cleanModule = NormalizeModule(module);
            int count = await GetCachedHourlyCountAsync(cleanModule);
            return count < GetHourlyQuota(cleanModule);
        }

        /// <summary>
        /// Atomically reserves a quota slot using SQL Server UPDLOCK/HOLDLOCK.
        /// Concurrency-safe across multiple worker threads and multi-instance deployments.
        /// </summary>
        public async Task<QuotaReservationResult> TryReserveSlotAsync(string module = "MVC")
        {
            string cleanModule = NormalizeModule(module);
            int maxPerHour = GetHourlyQuota(cleanModule);

            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();
                using var cmd = new SqlCommand("dbo.sp_ReserveNapixQuotaSlot", conn)
                {
                    CommandType = CommandType.StoredProcedure,
                    CommandTimeout = 30
                };
                cmd.Parameters.AddWithValue("@Module", cleanModule);
                cmd.Parameters.AddWithValue("@MaxCallsPerHour", maxPerHour);

                var pReserved = new SqlParameter("@Reserved", SqlDbType.Bit) { Direction = ParameterDirection.Output };
                var pCurrentUsage = new SqlParameter("@CurrentUsage", SqlDbType.Int) { Direction = ParameterDirection.Output };
                var pRetryAfter = new SqlParameter("@RetryAfterSeconds", SqlDbType.Int) { Direction = ParameterDirection.Output };

                cmd.Parameters.Add(pReserved);
                cmd.Parameters.Add(pCurrentUsage);
                cmd.Parameters.Add(pRetryAfter);

                await cmd.ExecuteNonQueryAsync();

                bool reserved = pReserved.Value is bool b && b;
                int currentUsage = pCurrentUsage.Value is int u ? u : 0;
                int retryAfter = pRetryAfter.Value is int r ? r : 0;

                return new QuotaReservationResult
                {
                    Reserved = reserved,
                    CurrentUsage = currentUsage,
                    RetryAfterSeconds = retryAfter,
                    HourlyQuota = maxPerHour
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[QuotaGuard] Atomic slot reservation failed for {Module}. Defaulting to fail-safe backoff.", cleanModule);
                int count = await FetchRollingHourCountFromDbAsync(cleanModule);
                bool ok = count < maxPerHour;
                return new QuotaReservationResult
                {
                    Reserved = ok,
                    CurrentUsage = count,
                    RetryAfterSeconds = ok ? 0 : 30,
                    HourlyQuota = maxPerHour
                };
            }
        }

        /// <summary>
        /// Fire-and-forget: logs one completed NAPIX call to the DB tracking table tagged by module.
        /// </summary>
        public void TrackCall(string endpoint, int httpStatus, bool success, string? username, string? cnr, int durationMs, string module = "MVC")
        {
            _ = LogCallAsync(endpoint, httpStatus, success, username, cnr, durationMs, NormalizeModule(module));
        }

        /// <summary>Returns the rolling 1-hour call count for a specific module app (DB-backed, cached 15 s).</summary>
        public async Task<int> GetCachedHourlyCountAsync(string module = "MVC")
        {
            string cleanModule = NormalizeModule(module);
            var cache = _moduleCaches.GetOrAdd(cleanModule, _ => new ModuleCache());

            if (cache.Count >= 0 && DateTime.UtcNow < cache.Expiry)
                return cache.Count;

            await cache.Lock.WaitAsync();
            try
            {
                if (cache.Count >= 0 && DateTime.UtcNow < cache.Expiry)
                    return cache.Count;

                cache.Count  = await FetchRollingHourCountFromDbAsync(cleanModule);
                cache.Expiry = DateTime.UtcNow.AddSeconds(15);
            }
            finally
            {
                cache.Lock.Release();
            }

            return cache.Count;
        }

        /// <summary>Invalidates the in-memory cache so the next call re-reads from DB.</summary>
        public void InvalidateCache(string? module = null)
        {
            if (string.IsNullOrWhiteSpace(module))
            {
                foreach (var cache in _moduleCaches.Values)
                    cache.Expiry = DateTime.MinValue;
            }
            else
            {
                if (_moduleCaches.TryGetValue(NormalizeModule(module), out var cache))
                    cache.Expiry = DateTime.MinValue;
            }
        }

        // ── Private helpers ────────────────────────────────────────────────────

        public static string NormalizeModule(string? module)
        {
            if (string.IsNullOrWhiteSpace(module)) return "MVC";
            string m = module.Trim().ToUpperInvariant();
            if (m.Contains("MVC")) return "MVC";
            if (m.Contains("OTHER") || m.Contains("OS") || m.Contains("PSC") || m.Contains("CC") || m.Contains("CONSUMER") || m.Contains("LAC") || m.Contains("ECA")) return "OtherCourts";
            if (m.Contains("LABOUR") || m.Contains("SERVICE") || m.Contains("WRIT")) return "Labour";
            return "MVC";
        }

        private async Task LogCallAsync(string endpoint, int httpStatus, bool success,
                                        string? username, string? cnr, int durationMs, string module)
        {
            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();
                using var cmd = new SqlCommand(
                    @"INSERT INTO dbo.NAPIX_API_CALLS (Endpoint, HttpStatus, IsSuccess, Username, CNRNumber, DurationMs, Module)
                      VALUES (@ep, @hs, @ok, @usr, @cnr, @dur, @mod)", conn);
                cmd.Parameters.Add("@ep",  SqlDbType.NVarChar, 120).Value = endpoint;
                cmd.Parameters.Add("@hs",  SqlDbType.Int).Value           = (object?)httpStatus ?? DBNull.Value;
                cmd.Parameters.Add("@ok",  SqlDbType.Bit).Value           = success;
                cmd.Parameters.Add("@usr", SqlDbType.NVarChar, 100).Value = (object?)username ?? DBNull.Value;
                cmd.Parameters.Add("@cnr", SqlDbType.NVarChar, 30).Value  = (object?)cnr ?? DBNull.Value;
                cmd.Parameters.Add("@dur", SqlDbType.Int).Value           = (object?)durationMs ?? DBNull.Value;
                cmd.Parameters.Add("@mod", SqlDbType.NVarChar, 20).Value  = module;
                await cmd.ExecuteNonQueryAsync();

                if (_moduleCaches.TryGetValue(module, out var cache) && cache.Count >= 0)
                {
                    cache.Count++;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[QuotaGuard] Failed to log NAPIX call for {Endpoint} ({Module}).", endpoint, module);
            }
        }

        private async Task<int> FetchRollingHourCountFromDbAsync(string module)
        {
            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();
                using var cmd = new SqlCommand(
                    "SELECT COUNT(*) FROM dbo.NAPIX_API_CALLS WHERE CalledAt >= DATEADD(HOUR, -1, SYSUTCDATETIME()) AND (Module = @mod OR (@mod = 'MVC' AND Module IS NULL))", conn);
                cmd.Parameters.Add("@mod", SqlDbType.NVarChar, 20).Value = module;
                var result = await cmd.ExecuteScalarAsync();
                return Convert.ToInt32(result);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[QuotaGuard] Failed to fetch rolling-hour call count from DB for module {Module}.", module);
                return 0; // fail-open: don't block calls if DB is unreachable
            }
        }
    }

    public class NapixCallResult
    {
        public bool Blocked    { get; init; }
        public string? Body    { get; init; }
        public int HttpStatus  { get; init; }
        public bool IsSuccess  { get; init; }
    }

    public class QuotaReservationResult
    {
        public bool Reserved { get; set; }
        public int CurrentUsage { get; set; }
        public int RetryAfterSeconds { get; set; }
        public int HourlyQuota { get; set; }
    }
}
