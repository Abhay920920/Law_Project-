using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace MVCCaseManagement.Utils
{
    public class NapixSyncJob
    {
        public long QueueId { get; set; }
        public string Module { get; set; } = "MVC";
        public int CaseId { get; set; }
        public string CNRNumber { get; set; } = string.Empty;
        public int Priority { get; set; } = 3;
        public int AttemptCount { get; set; }
    }

    public class SyncProcessResult
    {
        public bool IsSuccess { get; set; }
        public bool IsThrottled { get; set; }
        public bool IsPermanentFailure { get; set; }
        public int RetryAfterSeconds { get; set; }
        public string? PendDispStatus { get; set; }
        public DateTime? NextHearingDate { get; set; }
        public string? Message { get; set; }

        public static SyncProcessResult Success(string pendDispStatus, DateTime? nextHearingDate) =>
            new() { IsSuccess = true, PendDispStatus = pendDispStatus, NextHearingDate = nextHearingDate, Message = "Synchronized successfully" };

        public static SyncProcessResult Throttled(int retryAfterSeconds) =>
            new() { IsThrottled = true, RetryAfterSeconds = retryAfterSeconds, Message = $"Quota throttled. Retry after {retryAfterSeconds}s" };

        public static SyncProcessResult FailedPermanent(string reason) =>
            new() { IsPermanentFailure = true, Message = reason };

        public static SyncProcessResult RetryLater(int delayMinutes, string reason) =>
            new() { Message = $"Transient failure. Retrying in {delayMinutes}m: {reason}" };
    }

    public class NapixSyncEngine
    {
        private readonly string _connectionString;
        private readonly IECourtsNapixService _napixService;
        private readonly NapixQuotaService _quota;
        private readonly ILogger<NapixSyncEngine> _logger;

        private static readonly string[] DateFormats = new[]
        {
            "dd-MM-yyyy", "dd/MM/yyyy", "yyyy-MM-dd",
            "d-M-yyyy", "d/M/yyyy", "dd.MM.yyyy",
            "yyyy/MM/dd", "d-MMM-yyyy", "dd-MMM-yyyy"
        };

        public NapixSyncEngine(
            IConfiguration config,
            IECourtsNapixService napixService,
            NapixQuotaService quota,
            ILogger<NapixSyncEngine> logger)
        {
            _connectionString = config.GetConnectionString("MVCCaseDB") ?? "";
            _napixService = napixService;
            _quota = quota;
            _logger = logger;
        }

        /// <summary>
        /// Enqueues a case for e-Courts NAPIX synchronization with concurrency protection
        /// using filtered unique index UQ_NAPIX_SYNC_ACTIVE.
        /// Priority: 1 = Manual Sync, 2 = Approaching Hearing, 3 = Staged/Scheduled, 4 = Full Backfill.
        /// </summary>
        public async Task<(bool Queued, string Message, long QueueId)> EnqueueCaseAsync(
            string module, int caseId, string cnrNumber, int priority = 3)
        {
            string cleanModule = NapixQuotaService.NormalizeModule(module);
            string cleanCnr = Regex.Replace(cnrNumber ?? "", @"[^A-Za-z0-9]", "").ToUpperInvariant();

            if (string.IsNullOrWhiteSpace(cleanCnr))
            {
                return (false, "CNR number is required", 0);
            }

            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                // Idempotent insert: only if no active Queued/Processing record exists
                const string sql = @"
                    IF NOT EXISTS (
                        SELECT 1 FROM dbo.NAPIX_SYNC_QUEUE WITH (UPDLOCK, HOLDLOCK)
                        WHERE Module = @Module AND CNRNumber = @CNRNumber AND Status IN ('Queued', 'Processing')
                    )
                    BEGIN
                        INSERT INTO dbo.NAPIX_SYNC_QUEUE (Module, CaseId, CNRNumber, Priority, Status, RequestedAt)
                        VALUES (@Module, @CaseId, @CNRNumber, @Priority, 'Queued', SYSUTCDATETIME());
                        SELECT SCOPE_IDENTITY();
                    END
                    ELSE
                    BEGIN
                        SELECT QueueId FROM dbo.NAPIX_SYNC_QUEUE
                        WHERE Module = @Module AND CNRNumber = @CNRNumber AND Status IN ('Queued', 'Processing');
                    END";

                using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@Module", cleanModule);
                cmd.Parameters.AddWithValue("@CaseId", caseId);
                cmd.Parameters.AddWithValue("@CNRNumber", cleanCnr);
                cmd.Parameters.AddWithValue("@Priority", priority);

                var scalar = await cmd.ExecuteScalarAsync();
                long qId = scalar != null && scalar != DBNull.Value ? Convert.ToInt64(scalar) : 0;

                return (true, "Case queued for e-Courts synchronization", qId);
            }
            catch (SqlException ex) when (ex.Number == 2601 || ex.Number == 2627) // Unique constraint violation
            {
                _logger.LogInformation("[SyncEngine] Active sync job already exists for {Module} CNR {CNR}. Suppressing duplicate.", cleanModule, cleanCnr);
                return (true, "Case is already queued or processing", 0);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[SyncEngine] Failed to enqueue sync job for {Module} CNR {CNR}", cleanModule, cleanCnr);
                return (false, ex.Message, 0);
            }
        }

        /// <summary>
        /// Atomically retrieves the highest-priority eligible sync job for a module
        /// with lease locking and automatic recovery of expired leases.
        /// </summary>
        public async Task<NapixSyncJob?> DequeueJobAsync(string module, int leaseMinutes = 5)
        {
            string cleanModule = NapixQuotaService.NormalizeModule(module);

            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();
                using var cmd = new SqlCommand("dbo.sp_DequeueNapixSyncJob", conn)
                {
                    CommandType = CommandType.StoredProcedure,
                    CommandTimeout = 30
                };
                cmd.Parameters.AddWithValue("@Module", cleanModule);
                cmd.Parameters.AddWithValue("@LeaseMinutes", leaseMinutes);

                using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    return new NapixSyncJob
                    {
                        QueueId = reader.GetInt64(0),
                        Module = reader.GetString(1),
                        CaseId = reader.GetInt32(2),
                        CNRNumber = reader.GetString(3),
                        Priority = reader.GetInt32(4),
                        AttemptCount = reader.GetInt32(5)
                    };
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[SyncEngine] Dequeue error for module {Module}", cleanModule);
                return null;
            }
        }

        /// <summary>
        /// Processes a single sync job following all 19 production rules:
        /// 1. Local CNR validation (no quota consumed if invalid)
        /// 2. Atomic SQL Server quota slot reservation
        /// 3. Standard CNR details fetch via existing NAPIX service
        /// 4. Extraction of authoritative pend_disp and NextHearingDate
        /// 5. SHA-256 data hash computation
        /// 6. Updating case table without touching internal business fields
        /// 7. Exponential backoff for transient errors, termination for permanent errors
        /// </summary>
        public async Task<SyncProcessResult> ProcessSyncJobAsync(NapixSyncJob job, CancellationToken ct = default)
        {
            string cleanModule = NapixQuotaService.NormalizeModule(job.Module);
            string cleanCnr = (job.CNRNumber ?? "").Trim().ToUpperInvariant();

            // ── Step 1: Local CNR Validation (Rule 3 — do not consume quota) ──
            if (cleanCnr.Length != 16 || !Regex.IsMatch(cleanCnr, "^[A-Za-z0-9]{16}$"))
            {
                string invalidMsg = $"INVALID_CNR: CNR '{cleanCnr}' must be exactly 16 alphanumeric characters.";
                _logger.LogWarning("[SyncEngine] {Msg} Marking job {QueueId} permanently failed without consuming quota.", invalidMsg, job.QueueId);

                await UpdateQueueRecordAsync(job.QueueId, "Failed", invalidMsg, processed: true);
                await UpdateCaseRecordAuditAsync(cleanModule, job.CaseId, cleanCnr, "Failed_Permanent", invalidMsg, job.AttemptCount + 1, null, null, null, null, null, null, null);

                return SyncProcessResult.FailedPermanent(invalidMsg);
            }

            // ── Step 2: Atomic Quota Slot Reservation (Rule 2 & 3) ──
            var quotaRes = await _quota.TryReserveSlotAsync(cleanModule);
            if (!quotaRes.Reserved)
            {
                int delaySec = Math.Max(5, quotaRes.RetryAfterSeconds);
                _logger.LogWarning("[SyncEngine] Quota full for {Module} ({Usage}/{Max}). Backing off job {QueueId} for {Delay}s.",
                    cleanModule, quotaRes.CurrentUsage, quotaRes.HourlyQuota, job.QueueId, delaySec);

                await ReleaseJobBackToQueueAsync(job.QueueId, delaySec);
                return SyncProcessResult.Throttled(delaySec);
            }

            // ── Step 3: Fetch CNR Details (Rule 11 & 12 — standard CNR sync) ──
            bool isHighCourt = cleanCnr.StartsWith("HC", StringComparison.OrdinalIgnoreCase) ||
                               (cleanCnr.Length >= 4 && cleanCnr.Substring(2, 2).Equals("HC", StringComparison.OrdinalIgnoreCase));

            JsonElement? responseElement = null;
            string? gatewayError = null;

            try
            {
                responseElement = await _napixService.GetCnrDetailsAsync(cleanCnr, isHighCourt, cleanModule);
                gatewayError = _napixService.GetLastModuleError(cleanModule);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[SyncEngine] NAPIX service threw exception for {Module} CNR {CNR}", cleanModule, cleanCnr);
                gatewayError = ex.Message;
            }

            // ── Step 4: Handle Failure / Error Responses (Rule 8) ──
            string payloadErr = string.Empty;
            if (!responseElement.HasValue || IsErrorPayload(responseElement.Value, out payloadErr))
            {
                string errorDesc = !string.IsNullOrWhiteSpace(payloadErr) ? payloadErr : (gatewayError ?? "e-Courts NAPIX Gateway returned empty response");
                bool isPermanent = IsPermanentError(errorDesc);

                if (isPermanent)
                {
                    _logger.LogWarning("[SyncEngine] Permanent error for {Module} CNR {CNR}: {Error}. Terminating retry loop.", cleanModule, cleanCnr, errorDesc);
                    await UpdateQueueRecordAsync(job.QueueId, "Failed", errorDesc, processed: true);
                    await UpdateCaseRecordAuditAsync(cleanModule, job.CaseId, cleanCnr, "Failed_Permanent", errorDesc, job.AttemptCount + 1, null, null, null, null, null, null, null);
                    return SyncProcessResult.FailedPermanent(errorDesc);
                }

                // Transient failure — apply exponential backoff: 5m, 15m, 60m, 180m, 360m
                int nextDelayMinutes = job.AttemptCount switch
                {
                    0 => 5,
                    1 => 15,
                    2 => 60,
                    3 => 180,
                    _ => 360
                };

                if (job.AttemptCount >= 5)
                {
                    string maxRetryMsg = $"Max retry attempts (5) exceeded. Last error: {errorDesc}";
                    _logger.LogWarning("[SyncEngine] {Msg} for {Module} CNR {CNR}", maxRetryMsg, cleanModule, cleanCnr);
                    await UpdateQueueRecordAsync(job.QueueId, "Failed", maxRetryMsg, processed: true);
                    await UpdateCaseRecordAuditAsync(cleanModule, job.CaseId, cleanCnr, "Failed_Transient_MaxAttempts", maxRetryMsg, job.AttemptCount + 1, null, null, null, null, null, null, null);
                    return SyncProcessResult.FailedPermanent(maxRetryMsg);
                }

                _logger.LogInformation("[SyncEngine] Transient error for {Module} CNR {CNR} (Attempt {Attempt}): {Error}. Retrying in {Delay}m.",
                    cleanModule, cleanCnr, job.AttemptCount + 1, errorDesc, nextDelayMinutes);

                await RescheduleRetryJobAsync(job.QueueId, job.AttemptCount + 1, nextDelayMinutes, errorDesc);
                await UpdateCaseRecordAuditAsync(cleanModule, job.CaseId, cleanCnr, "Failed_Transient", errorDesc, job.AttemptCount + 1, null, null, null, null, null, null, null);
                return SyncProcessResult.RetryLater(nextDelayMinutes, errorDesc);
            }

            // ── Step 5: Successful Response Parsing (Rules 1, 5, 13, 14, 15) ──
            var root = responseElement.Value;
            string rawJson = root.GetRawText();

            // Authoritative pend_disp: 'D' = Disposed, 'P' = Pending (Rule 1)
            string rawPendDisp = ExtractStringProp(root, "pend_disp", "case_status", "status") ?? "P";
            string pendDispStatus = (rawPendDisp.Trim().Equals("D", StringComparison.OrdinalIgnoreCase) ||
                                     rawPendDisp.Contains("DISPOSED", StringComparison.OrdinalIgnoreCase))
                ? "D" : "P";

            // Next Hearing Date (Rule 5)
            // Next Hearing Date (Rule 5)
            string? rawNextDate = ExtractStringProp(root, "date_next_list", "next_date", "next_hearing_date", "nxt_date");
            DateTime? nextHearingDate = ParseNormalizedDate(rawNextDate);

            // Decision Date (Rule 5 & 13)
            string? rawDecisionDate = ExtractStringProp(root, "date_of_decision", "decision_date", "disposal_date", "dt_decision", "dateofdecision");
            DateTime? decisionDate = ParseNormalizedDate(rawDecisionDate);

            // Case Status
            string? caseStatus = ExtractStringProp(root, "case_status", "current_status", "status");
            if (string.IsNullOrWhiteSpace(caseStatus))
            {
                caseStatus = pendDispStatus == "D" || decisionDate.HasValue ? "Disposed" : "Pending";
            }

            // Additional eCourts presentation fields
            string? stage = ExtractStringProp(root, "purpose_name", "stage", "case_stage");
            string? estName = ExtractStringProp(root, "est_name", "establishment_name", "court_name");
            string? courtNo = ExtractStringProp(root, "court_no", "court_number");
            string? judge = ExtractStringProp(root, "judge", "judge_name", "presiding_officer", "court_judge");

            // SHA-256 Hash for change detection (Rule 14)
            string dataHash = ComputeSha256(rawJson);

            // Update Case record — ONLY e-Courts fields, preserving internal business data (Rule 1 & 13)
            await UpdateCaseRecordAuditAsync(
                cleanModule,
                job.CaseId,
                cleanCnr,
                syncStatus: "Success",
                syncError: null,
                attemptCount: job.AttemptCount + 1,
                dataHash: dataHash,
                pendDispStatus: pendDispStatus,
                nextHearingDate: nextHearingDate,
                estName: estName,
                stage: stage,
                courtNo: courtNo,
                judge: judge,
                decisionDate: decisionDate,
                caseStatus: caseStatus);

            // Mark Queue item completed
            await UpdateQueueRecordAsync(job.QueueId, "Success", null, processed: true);

            _logger.LogInformation("[SyncEngine] Successfully synchronized {Module} CNR {CNR} (PendDisp: {Status}, NextHearing: {Date}).",
                cleanModule, cleanCnr, pendDispStatus, nextHearingDate?.ToString("yyyy-MM-dd") ?? "None");

            return SyncProcessResult.Success(pendDispStatus, nextHearingDate);
        }

        // ── Helper Database Methods ──────────────────────────────────────────

        private async Task ReleaseJobBackToQueueAsync(long queueId, int retryAfterSeconds)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            const string sql = @"
                UPDATE dbo.NAPIX_SYNC_QUEUE
                SET Status = 'Queued',
                    LockedUntil = NULL,
                    NextAttemptAt = DATEADD(SECOND, @Secs, SYSUTCDATETIME())
                WHERE QueueId = @QueueId;";
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@QueueId", queueId);
            cmd.Parameters.AddWithValue("@Secs", retryAfterSeconds);
            await cmd.ExecuteNonQueryAsync();
        }

        private async Task RescheduleRetryJobAsync(long queueId, int attemptCount, int delayMinutes, string lastError)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            const string sql = @"
                UPDATE dbo.NAPIX_SYNC_QUEUE
                SET Status = 'Queued',
                    LockedUntil = NULL,
                    AttemptCount = @Attempts,
                    NextAttemptAt = DATEADD(MINUTE, @Mins, SYSUTCDATETIME()),
                    LastError = @LastError
                WHERE QueueId = @QueueId;";
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@QueueId", queueId);
            cmd.Parameters.AddWithValue("@Attempts", attemptCount);
            cmd.Parameters.AddWithValue("@Mins", delayMinutes);
            cmd.Parameters.AddWithValue("@LastError", (object?)lastError ?? DBNull.Value);
            await cmd.ExecuteNonQueryAsync();
        }

        private async Task UpdateQueueRecordAsync(long queueId, string status, string? error, bool processed)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            const string sql = @"
                UPDATE dbo.NAPIX_SYNC_QUEUE
                SET Status = @Status,
                    LockedUntil = NULL,
                    LastError = @LastError,
                    ProcessedAt = CASE WHEN @Processed = 1 THEN SYSUTCDATETIME() ELSE ProcessedAt END
                WHERE QueueId = @QueueId;";
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@QueueId", queueId);
            cmd.Parameters.AddWithValue("@Status", status);
            cmd.Parameters.AddWithValue("@LastError", (object?)error ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Processed", processed ? 1 : 0);
            await cmd.ExecuteNonQueryAsync();
        }

        private async Task UpdateCaseRecordAuditAsync(
            string module,
            int caseId,
            string cnrNumber,
            string syncStatus,
            string? syncError,
            int attemptCount,
            string? dataHash,
            string? pendDispStatus,
            DateTime? nextHearingDate,
            string? estName,
            string? stage,
            string? courtNo,
            string? judge,
            DateTime? decisionDate = null,
            string? caseStatus = null)
        {
            string sql;
            if (module.Equals("OtherCourts", StringComparison.OrdinalIgnoreCase) || module.Contains("Other", StringComparison.OrdinalIgnoreCase))
            {
                sql = @"
                    UPDATE OTHER_CASES
                    SET LastNapixSyncAt = SYSUTCDATETIME(),
                        LastNapixSyncStatus = @Status,
                        LastNapixSyncError = @Error,
                        NapixSyncAttemptCount = @AttemptCount,
                        NapixDataHash = COALESCE(@DataHash, NapixDataHash),
                        PendDispStatus = COALESCE(@PendDispStatus, PendDispStatus),
                        NextDateOfHearing = COALESCE(@NextHearingDate, NextDateOfHearing),
                        EstName = COALESCE(@EstName, EstName),
                        ECourtsStage = COALESCE(@Stage, ECourtsStage),
                        ECourtsCourtNo = COALESCE(@CourtNo, ECourtsCourtNo),
                        ECourtsJudge = COALESCE(@Judge, ECourtsJudge),
                        CaseStatus = COALESCE(NULLIF(@CaseStatus, ''), CaseStatus),
                        DecisionDate = COALESCE(DecisionDate, @DecisionDate)
                    WHERE CaseID = @CaseId OR (CNRNumber = @CNRNumber AND @CNRNumber IS NOT NULL AND @CNRNumber <> '');";
            }
            else if (module.Equals("Labour", StringComparison.OrdinalIgnoreCase))
            {
                sql = @"
                    UPDATE LABOUR_CASES
                    SET LastNapixSyncAt = SYSUTCDATETIME(),
                        LastNapixSyncStatus = @Status,
                        LastNapixSyncError = @Error,
                        NapixSyncAttemptCount = @AttemptCount,
                        NapixDataHash = COALESCE(@DataHash, NapixDataHash),
                        PendDispStatus = CASE WHEN @CaseStatus = 'Disposed' OR @DecisionDate IS NOT NULL THEN 'D' ELSE COALESCE(@PendDispStatus, PendDispStatus) END,
                        NextHearingDate = COALESCE(@NextHearingDate, NextHearingDate),
                        CurrentStage = COALESCE(NULLIF(@Stage, ''), CurrentStage),
                        EstName = COALESCE(@EstName, EstName),
                        ECourtsStage = COALESCE(@Stage, ECourtsStage),
                        ECourtsCourtNo = COALESCE(@CourtNo, ECourtsCourtNo),
                        ECourtsJudge = COALESCE(@Judge, ECourtsJudge),
                        CaseStatus = COALESCE(NULLIF(@CaseStatus, ''), CaseStatus),
                        DisposalDate = COALESCE(DisposalDate, @DecisionDate),
                        DisposalResult = CASE WHEN (DisposalResult IS NULL OR DisposalResult = '' OR DisposalResult = 'Pending') AND (@PendDispStatus = 'D' OR @CaseStatus = 'Disposed') THEN 'Disposed' ELSE DisposalResult END,
                        CO_WP_Status = CASE WHEN CO_WP_CNRNumber = @CNRNumber THEN COALESCE(NULLIF(@CaseStatus, ''), CO_WP_Status) ELSE CO_WP_Status END,
                        CO_WA_Status = CASE WHEN CO_WA_CNRNumber = @CNRNumber THEN COALESCE(NULLIF(@CaseStatus, ''), CO_WA_Status) ELSE CO_WA_Status END,
                        CO_Claimant_CaseStatus = CASE WHEN CO_Claimant_CNRNumber = @CNRNumber THEN COALESCE(NULLIF(@CaseStatus, ''), CO_Claimant_CaseStatus) ELSE CO_Claimant_CaseStatus END
                    WHERE CaseID = @CaseId 
                       OR (CNRNumber = @CNRNumber AND @CNRNumber IS NOT NULL AND @CNRNumber <> '')
                       OR (CO_WP_CNRNumber = @CNRNumber AND @CNRNumber IS NOT NULL AND @CNRNumber <> '')
                       OR (CO_WA_CNRNumber = @CNRNumber AND @CNRNumber IS NOT NULL AND @CNRNumber <> '')
                       OR (CO_Claimant_CNRNumber = @CNRNumber AND @CNRNumber IS NOT NULL AND @CNRNumber <> '');";
            }
            else
            {
                sql = @"
                    UPDATE dbo.MVC_CASES
                    SET LastNapixSyncAt = SYSUTCDATETIME(),
                        LastNapixSyncStatus = @Status,
                        LastNapixSyncError = @Error,
                        NapixSyncAttemptCount = @AttemptCount,
                        NapixDataHash = COALESCE(@DataHash, NapixDataHash),
                        PendDispStatus = COALESCE(@PendDispStatus, PendDispStatus),
                        NextHearingDate = COALESCE(@NextHearingDate, NextHearingDate),
                        EstName = COALESCE(@EstName, EstName),
                        ECourtsStage = COALESCE(@Stage, ECourtsStage),
                        ECourtsCourtNo = COALESCE(@CourtNo, ECourtsCourtNo),
                        ECourtsJudge = COALESCE(@Judge, ECourtsJudge),
                        CaseStatus = COALESCE(NULLIF(@CaseStatus, ''), CaseStatus),
                        ClosureDate = COALESCE(ClosureDate, @DecisionDate),
                        DisposalStatus = CASE WHEN (DisposalStatus IS NULL OR DisposalStatus = '' OR DisposalStatus = 'Pending') AND @PendDispStatus = 'D' THEN 'Disposed' ELSE DisposalStatus END
                    WHERE CaseID = @CaseId OR (CNRNumber = @CNRNumber AND @CNRNumber IS NOT NULL AND @CNRNumber <> '');";
            }

            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@CaseId", caseId);
            cmd.Parameters.AddWithValue("@CNRNumber", (object?)cnrNumber ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Status", syncStatus);
            cmd.Parameters.AddWithValue("@Error", (object?)syncError ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@AttemptCount", attemptCount);
            cmd.Parameters.AddWithValue("@DataHash", (object?)dataHash ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@PendDispStatus", (object?)pendDispStatus ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@NextHearingDate", (object?)nextHearingDate?.Date ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@EstName", (object?)estName ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Stage", (object?)stage ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@CourtNo", (object?)courtNo ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Judge", (object?)judge ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@DecisionDate", (object?)decisionDate?.Date ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@CaseStatus", (object?)caseStatus ?? DBNull.Value);

            await cmd.ExecuteNonQueryAsync();
        }

        // ── Parsing & Cryptographic Helpers ─────────────────────────────────

        public static string ComputeSha256(string content)
        {
            if (string.IsNullOrEmpty(content)) return string.Empty;
            using var sha = SHA256.Create();
            byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(content));
            return Convert.ToHexString(bytes).ToLowerInvariant();
        }

        public static DateTime? ParseNormalizedDate(string? dateStr)
        {
            if (string.IsNullOrWhiteSpace(dateStr)) return null;
            string trimmed = dateStr.Trim();

            // Ignore keywords that aren't dates
            if (trimmed.Equals("Pending", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Equals("Disposed", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Equals("—", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Equals("-", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            if (DateTime.TryParseExact(trimmed, DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
            {
                return dt;
            }

            if (DateTime.TryParse(trimmed, CultureInfo.InvariantCulture, DateTimeStyles.None, out var generalDt))
            {
                return generalDt;
            }

            return null;
        }

        private static bool IsPermanentError(string? error)
        {
            if (string.IsNullOrWhiteSpace(error)) return false;
            string upper = error.ToUpperInvariant();

            return upper.Contains("600") ||                   // INVALID_CNR from NIC
                   upper.Contains("628") ||                   // RECORD_NOT_FOUND
                   upper.Contains("RECORD_NOT_FOUND") ||
                   upper.Contains("RECORD NOT FOUND") ||
                   upper.Contains("NO RECORD FOUND") ||
                   upper.Contains("INVALID_CNR") ||
                   upper.Contains("INVALID_DEPT_ID") ||
                   upper.Contains("NOT REGISTERED");
        }

        private static bool IsErrorPayload(JsonElement root, out string errorDesc)
        {
            errorDesc = string.Empty;

            if (root.ValueKind == JsonValueKind.Object)
            {
                if (root.TryGetProperty("status_code", out var sc))
                {
                    string code = sc.ToString();
                    if (code == "600" || code == "626" || code == "628" || code == "629" || code == "400" || code == "401" || code == "500")
                    {
                        string status = root.TryGetProperty("status", out var st) ? st.ToString() : code;
                        errorDesc = $"{code} ({status})";
                        return true;
                    }
                }

                if (root.TryGetProperty("error", out var err) && !string.IsNullOrWhiteSpace(err.GetString()))
                {
                    errorDesc = err.GetString()!;
                    return true;
                }
            }

            return false;
        }

        private static string? ExtractStringProp(JsonElement root, params string[] propertyNames)
        {
            if (root.ValueKind == JsonValueKind.Object)
            {
                foreach (var name in propertyNames)
                {
                    if (root.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(p.GetString()))
                    {
                        return p.GetString()!.Trim();
                    }
                }

                // Search nested objects
                foreach (var child in root.EnumerateObject())
                {
                    if (child.Value.ValueKind == JsonValueKind.Object)
                    {
                        string? found = ExtractStringProp(child.Value, propertyNames);
                        if (!string.IsNullOrEmpty(found)) return found;
                    }
                    else if (child.Value.ValueKind == JsonValueKind.Array && child.Value.GetArrayLength() > 0)
                    {
                        foreach (var item in child.Value.EnumerateArray())
                        {
                            if (item.ValueKind == JsonValueKind.Object)
                            {
                                string? itemFound = ExtractStringProp(item, propertyNames);
                                if (!string.IsNullOrEmpty(itemFound)) return itemFound;
                            }
                        }
                    }
                }
            }
            else if (root.ValueKind == JsonValueKind.Array && root.GetArrayLength() > 0)
            {
                foreach (var item in root.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.Object)
                    {
                        string? found = ExtractStringProp(item, propertyNames);
                        if (!string.IsNullOrEmpty(found)) return found;
                    }
                }
            }

            return null;
        }
    }
}
