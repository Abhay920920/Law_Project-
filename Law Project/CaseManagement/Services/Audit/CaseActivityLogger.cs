using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MVCCaseManagement.Models.Audit;

namespace MVCCaseManagement.Services.Audit
{
    public class CaseActivityLogger : ICaseActivityLogger
    {
        private readonly string _connectionString;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<CaseActivityLogger> _logger;

        private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = false,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };

        private static readonly HashSet<string> _ignoredFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "ModifiedDate", "ModifiedBy", "CreatedDate", "CreatedBy", "CreatedAt", "UpdatedAt",
            "Divisions", "MACTs", "Advocates", "Stages", "Courts", "CaseTypes", "Token",
            "ClaimsPrincipal", "Action", "Controller", "ReturnUrl"
        };

        public CaseActivityLogger(
            IConfiguration configuration,
            IHttpContextAccessor httpContextAccessor,
            ILogger<CaseActivityLogger> logger)
        {
            _connectionString = configuration.GetConnectionString("MVCCaseDB")
                ?? configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("Connection string 'MVCCaseDB' or 'DefaultConnection' not configured in appsettings.json.");
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
        }

        private (int? userId, string username, string fullName, string role, int? divisionId, string divisionName, string ipAddress) GetCurrentActor()
        {
            var user = _httpContextAccessor.HttpContext?.User;
            int? userId = null;
            string username = "SYSTEM";
            string fullName = "System Background Worker";
            string role = "System";
            int? divisionId = null;
            string divisionName = "Central Office";
            string ipAddress = _httpContextAccessor.HttpContext?.Connection?.RemoteIpAddress?.ToString() ?? "127.0.0.1";

            if (user != null && user.Identity != null && user.Identity.IsAuthenticated)
            {
                username = user.Identity.Name ?? "User";
                fullName = user.FindFirstValue("FullName") ?? username;
                role = user.FindFirstValue(ClaimTypes.Role) ?? user.FindFirstValue("Role") ?? "User";

                var uidStr = user.FindFirstValue("UserID") ?? user.FindFirstValue(ClaimTypes.NameIdentifier);
                if (int.TryParse(uidStr, out int parsedUid))
                {
                    userId = parsedUid;
                }

                var divIdStr = user.FindFirstValue("DivisionID");
                if (int.TryParse(divIdStr, out int parsedDivId))
                {
                    divisionId = parsedDivId;
                }

                divisionName = user.FindFirstValue("DivisionName") ?? (divisionId == 5 ? "Central Office" : "NWKRTC Division");
            }

            return (userId, username, fullName, role, divisionId, divisionName, ipAddress);
        }

        public async Task LogCaseCreatedAsync(
            string module, 
            int caseId, 
            string caseNumber, 
            string? vehicleNo, 
            string? courtName, 
            object newValues, 
            string? customSummary = null, 
            CancellationToken cancellationToken = default)
        {
            try
            {
                var actor = GetCurrentActor();
                string newJson = JsonSerializer.Serialize(newValues, _jsonOptions);
                string summary = customSummary ?? $"Registered new {module} case {caseNumber}" + 
                    (!string.IsNullOrEmpty(vehicleNo) ? $" [Vehicle: {vehicleNo}]" : "") +
                    (!string.IsNullOrEmpty(courtName) ? $" at {courtName}" : "");

                await InsertActivityLogAsync(new CaseActivityLog
                {
                    Timestamp = DateTime.Now,
                    UserID = actor.userId,
                    Username = actor.username,
                    UserFullName = actor.fullName,
                    UserRole = actor.role,
                    DivisionID = actor.divisionId,
                    DivisionName = actor.divisionName,
                    IpAddress = actor.ipAddress,
                    Module = module.ToUpperInvariant(),
                    CaseID = caseId,
                    CaseNumber = caseNumber,
                    VehicleNo = vehicleNo,
                    CourtName = courtName,
                    ActionType = "CREATED",
                    ActionSummary = summary,
                    ChangedFieldsSummary = "Initial Case Registration",
                    NewValuesJson = newJson
                }, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to write CREATED activity log for case {CaseNumber} ({Module})", caseNumber, module);
            }
        }

        public async Task LogCaseUpdatedAsync(
            string module, 
            int caseId, 
            string caseNumber, 
            string? vehicleNo, 
            string? courtName, 
            object oldValues, 
            object newValues, 
            string? customSummary = null, 
            CancellationToken cancellationToken = default)
        {
            try
            {
                var actor = GetCurrentActor();
                string oldJson = JsonSerializer.Serialize(oldValues, _jsonOptions);
                string newJson = JsonSerializer.Serialize(newValues, _jsonOptions);

                var diffList = ComputeFieldDifferences(oldValues, newValues);
                string changedSummary = diffList.Count > 0
                    ? string.Join(" | ", diffList.Take(5).Select(d => $"{d.DisplayName}: [{d.OldValue ?? "None"} → {d.NewValue ?? "None"}]"))
                    : "Record updated";

                if (diffList.Count > 5)
                {
                    changedSummary += $" (+{diffList.Count - 5} more)";
                }

                string summary = customSummary ?? $"Updated {module} case {caseNumber}" + 
                    (diffList.Count > 0 ? $" ({diffList.Count} field{(diffList.Count > 1 ? "s" : "")} changed)" : "");

                await InsertActivityLogAsync(new CaseActivityLog
                {
                    Timestamp = DateTime.Now,
                    UserID = actor.userId,
                    Username = actor.username,
                    UserFullName = actor.fullName,
                    UserRole = actor.role,
                    DivisionID = actor.divisionId,
                    DivisionName = actor.divisionName,
                    IpAddress = actor.ipAddress,
                    Module = module.ToUpperInvariant(),
                    CaseID = caseId,
                    CaseNumber = caseNumber,
                    VehicleNo = vehicleNo,
                    CourtName = courtName,
                    ActionType = "UPDATED",
                    ActionSummary = summary,
                    ChangedFieldsSummary = changedSummary,
                    OldValuesJson = oldJson,
                    NewValuesJson = newJson
                }, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to write UPDATED activity log for case {CaseNumber} ({Module})", caseNumber, module);
            }
        }

        public async Task LogCaseDeletedAsync(
            string module, 
            int caseId, 
            string caseNumber, 
            string? vehicleNo, 
            string reason, 
            CancellationToken cancellationToken = default)
        {
            try
            {
                var actor = GetCurrentActor();
                string summary = $"Deleted {module} case {caseNumber}. Reason: {reason}";

                await InsertActivityLogAsync(new CaseActivityLog
                {
                    Timestamp = DateTime.Now,
                    UserID = actor.userId,
                    Username = actor.username,
                    UserFullName = actor.fullName,
                    UserRole = actor.role,
                    DivisionID = actor.divisionId,
                    DivisionName = actor.divisionName,
                    IpAddress = actor.ipAddress,
                    Module = module.ToUpperInvariant(),
                    CaseID = caseId,
                    CaseNumber = caseNumber,
                    VehicleNo = vehicleNo,
                    ActionType = "DELETED",
                    ActionSummary = summary,
                    ChangedFieldsSummary = $"Case record removed/archived. Reason: {reason}"
                }, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to write DELETED activity log for case {CaseNumber} ({Module})", caseNumber, module);
            }
        }

        public async Task LogCaseTransferredAsync(
            string module, 
            int caseId, 
            string caseNumber, 
            int fromDivisionId, 
            int toDivisionId, 
            string fromDivisionName, 
            string toDivisionName, 
            string? reason = null, 
            CancellationToken cancellationToken = default)
        {
            try
            {
                var actor = GetCurrentActor();
                string summary = $"Transferred {module} case {caseNumber} from {fromDivisionName} to {toDivisionName}." +
                    (!string.IsNullOrEmpty(reason) ? $" Reason: {reason}" : "");

                await InsertActivityLogAsync(new CaseActivityLog
                {
                    Timestamp = DateTime.Now,
                    UserID = actor.userId,
                    Username = actor.username,
                    UserFullName = actor.fullName,
                    UserRole = actor.role,
                    DivisionID = actor.divisionId,
                    DivisionName = actor.divisionName,
                    IpAddress = actor.ipAddress,
                    Module = module.ToUpperInvariant(),
                    CaseID = caseId,
                    CaseNumber = caseNumber,
                    ActionType = "TRANSFERRED",
                    ActionSummary = summary,
                    ChangedFieldsSummary = $"Division: [{fromDivisionName} → {toDivisionName}]"
                }, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to write TRANSFERRED activity log for case {CaseNumber} ({Module})", caseNumber, module);
            }
        }

        public async Task LogSubEntityActionAsync(
            string module, 
            int caseId, 
            string caseNumber, 
            string actionType, 
            string summary, 
            object? details = null, 
            CancellationToken cancellationToken = default)
        {
            try
            {
                var actor = GetCurrentActor();
                string? detailsJson = details != null ? JsonSerializer.Serialize(details, _jsonOptions) : null;

                await InsertActivityLogAsync(new CaseActivityLog
                {
                    Timestamp = DateTime.Now,
                    UserID = actor.userId,
                    Username = actor.username,
                    UserFullName = actor.fullName,
                    UserRole = actor.role,
                    DivisionID = actor.divisionId,
                    DivisionName = actor.divisionName,
                    IpAddress = actor.ipAddress,
                    Module = module.ToUpperInvariant(),
                    CaseID = caseId,
                    CaseNumber = caseNumber,
                    ActionType = actionType.ToUpperInvariant(),
                    ActionSummary = summary,
                    ChangedFieldsSummary = summary,
                    NewValuesJson = detailsJson
                }, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to write sub-action {ActionType} for case {CaseNumber}", actionType, caseNumber);
            }
        }

        private async Task InsertActivityLogAsync(CaseActivityLog log, CancellationToken cancellationToken)
        {
            const string sql = @"
                INSERT INTO CASE_ACTIVITY_LOGS (
                    Timestamp, UserID, Username, UserFullName, UserRole,
                    DivisionID, DivisionName, IpAddress, Module, CaseID,
                    CaseNumber, VehicleNo, CourtName, ActionType, ActionSummary,
                    ChangedFieldsSummary, OldValuesJson, NewValuesJson
                ) VALUES (
                    @Timestamp, @UserID, @Username, @UserFullName, @UserRole,
                    @DivisionID, @DivisionName, @IpAddress, @Module, @CaseID,
                    @CaseNumber, @VehicleNo, @CourtName, @ActionType, @ActionSummary,
                    @ChangedFieldsSummary, @OldValuesJson, @NewValuesJson
                );";

            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync(cancellationToken);

            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Timestamp", log.Timestamp);
            cmd.Parameters.AddWithValue("@UserID", (object?)log.UserID ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Username", log.Username);
            cmd.Parameters.AddWithValue("@UserFullName", (object?)log.UserFullName ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@UserRole", (object?)log.UserRole ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@DivisionID", (object?)log.DivisionID ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@DivisionName", (object?)log.DivisionName ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@IpAddress", (object?)log.IpAddress ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Module", log.Module);
            cmd.Parameters.AddWithValue("@CaseID", (object?)log.CaseID ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@CaseNumber", log.CaseNumber);
            cmd.Parameters.AddWithValue("@VehicleNo", (object?)log.VehicleNo ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@CourtName", (object?)log.CourtName ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@ActionType", log.ActionType);
            cmd.Parameters.AddWithValue("@ActionSummary", log.ActionSummary);
            cmd.Parameters.AddWithValue("@ChangedFieldsSummary", (object?)log.ChangedFieldsSummary ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@OldValuesJson", (object?)log.OldValuesJson ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@NewValuesJson", (object?)log.NewValuesJson ?? DBNull.Value);

            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        public async Task<ActivityLogPagedResult> GetLogSheetAsync(ActivityLogFilter filter, CancellationToken cancellationToken = default)
        {
            var result = new ActivityLogPagedResult
            {
                Page = filter.Page > 0 ? filter.Page : 1,
                PageSize = filter.PageSize > 0 ? filter.PageSize : 25,
                Filter = filter
            };

            var whereClauses = new List<string>();
            var parameters = new List<SqlParameter>();

            // Module filter
            if (!string.IsNullOrEmpty(filter.Module) && filter.Module.ToLower() != "all")
            {
                whereClauses.Add("Module = @Module");
                parameters.Add(new SqlParameter("@Module", filter.Module.ToUpper()));
            }

            // ActionType filter
            if (!string.IsNullOrEmpty(filter.ActionType) && filter.ActionType.ToLower() != "all")
            {
                whereClauses.Add("ActionType = @ActionType");
                parameters.Add(new SqlParameter("@ActionType", filter.ActionType.ToUpper()));
            }

            // Division filter
            if (filter.DivisionID.HasValue && filter.DivisionID.Value > 0)
            {
                whereClauses.Add("DivisionID = @DivisionID");
                parameters.Add(new SqlParameter("@DivisionID", filter.DivisionID.Value));
            }

            // Date Range Presets
            DateTime today = DateTime.Today;
            if (!string.IsNullOrEmpty(filter.DatePreset) && filter.DatePreset.ToLower() != "all")
            {
                switch (filter.DatePreset.ToLower())
                {
                    case "today":
                        whereClauses.Add("Timestamp >= @StartToday");
                        parameters.Add(new SqlParameter("@StartToday", today));
                        break;
                    case "yesterday":
                        whereClauses.Add("Timestamp >= @YesterdayStart AND Timestamp < @TodayStart");
                        parameters.Add(new SqlParameter("@YesterdayStart", today.AddDays(-1)));
                        parameters.Add(new SqlParameter("@TodayStart", today));
                        break;
                    case "week":
                        whereClauses.Add("Timestamp >= @WeekStart");
                        parameters.Add(new SqlParameter("@WeekStart", today.AddDays(-7)));
                        break;
                    case "month":
                        whereClauses.Add("Timestamp >= @MonthStart");
                        parameters.Add(new SqlParameter("@MonthStart", new DateTime(today.Year, today.Month, 1)));
                        break;
                    case "custom":
                        if (filter.DateFrom.HasValue)
                        {
                            whereClauses.Add("Timestamp >= @DateFrom");
                            parameters.Add(new SqlParameter("@DateFrom", filter.DateFrom.Value));
                        }
                        if (filter.DateTo.HasValue)
                        {
                            whereClauses.Add("Timestamp <= @DateTo");
                            parameters.Add(new SqlParameter("@DateTo", filter.DateTo.Value.Date.AddDays(1).AddTicks(-1)));
                        }
                        break;
                }
            }

            // Search Term
            if (!string.IsNullOrEmpty(filter.SearchTerm))
            {
                whereClauses.Add("(CaseNumber LIKE @Search OR VehicleNo LIKE @Search OR Username LIKE @Search OR UserFullName LIKE @Search OR ActionSummary LIKE @Search)");
                parameters.Add(new SqlParameter("@Search", $"%{filter.SearchTerm.Trim()}%"));
            }

            string whereSql = whereClauses.Count > 0 ? "WHERE " + string.Join(" AND ", whereClauses) : "";

            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync(cancellationToken);

            // Count Query
            string countSql = $"SELECT COUNT(*) FROM CASE_ACTIVITY_LOGS {whereSql};";
            using (var countCmd = new SqlCommand(countSql, conn))
            {
                foreach (var p in parameters)
                {
                    countCmd.Parameters.Add(new SqlParameter(p.ParameterName, p.Value));
                }
                result.TotalCount = (int)await countCmd.ExecuteScalarAsync(cancellationToken);
            }

            // Paged Data Query
            int offset = (result.Page - 1) * result.PageSize;
            string dataSql = $@"
                SELECT 
                    LogID, Timestamp, UserID, Username, UserFullName, UserRole,
                    DivisionID, DivisionName, IpAddress, Module, CaseID,
                    CaseNumber, VehicleNo, CourtName, ActionType, ActionSummary,
                    ChangedFieldsSummary, OldValuesJson, NewValuesJson
                FROM CASE_ACTIVITY_LOGS
                {whereSql}
                ORDER BY Timestamp DESC
                OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;";

            using (var dataCmd = new SqlCommand(dataSql, conn))
            {
                foreach (var p in parameters)
                {
                    dataCmd.Parameters.Add(new SqlParameter(p.ParameterName, p.Value));
                }
                dataCmd.Parameters.Add(new SqlParameter("@Offset", offset));
                dataCmd.Parameters.Add(new SqlParameter("@PageSize", result.PageSize));

                using var reader = await dataCmd.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    result.Logs.Add(new CaseActivityLog
                    {
                        LogID = reader.GetInt64(0),
                        Timestamp = reader.GetDateTime(1),
                        UserID = reader.IsDBNull(2) ? null : reader.GetInt32(2),
                        Username = reader.GetString(3),
                        UserFullName = reader.IsDBNull(4) ? null : reader.GetString(4),
                        UserRole = reader.IsDBNull(5) ? null : reader.GetString(5),
                        DivisionID = reader.IsDBNull(6) ? null : reader.GetInt32(6),
                        DivisionName = reader.IsDBNull(7) ? null : reader.GetString(7),
                        IpAddress = reader.IsDBNull(8) ? null : reader.GetString(8),
                        Module = reader.GetString(9),
                        CaseID = reader.IsDBNull(10) ? null : reader.GetInt32(10),
                        CaseNumber = reader.GetString(11),
                        VehicleNo = reader.IsDBNull(12) ? null : reader.GetString(12),
                        CourtName = reader.IsDBNull(13) ? null : reader.GetString(13),
                        ActionType = reader.GetString(14),
                        ActionSummary = reader.GetString(15),
                        ChangedFieldsSummary = reader.IsDBNull(16) ? null : reader.GetString(16),
                        OldValuesJson = reader.IsDBNull(17) ? null : reader.GetString(17),
                        NewValuesJson = reader.IsDBNull(18) ? null : reader.GetString(18)
                    });
                }
            }

            result.KpiSummary = await GetSummaryKpisAsync(filter.DivisionID, cancellationToken);
            return result;
        }

        public async Task<ActivityLogKpiSummary> GetSummaryKpisAsync(int? divisionId = null, CancellationToken cancellationToken = default)
        {
            var kpi = new ActivityLogKpiSummary();
            DateTime today = DateTime.Today;

            string divFilter = (divisionId.HasValue && divisionId.Value > 0) ? "AND DivisionID = @DivisionID" : "";

            string sql = $@"
                SELECT 
                    COUNT(*) AS TotalToday,
                    SUM(CASE WHEN ActionType = 'CREATED' THEN 1 ELSE 0 END) AS CreatedToday,
                    SUM(CASE WHEN ActionType = 'UPDATED' THEN 1 ELSE 0 END) AS UpdatedToday,
                    SUM(CASE WHEN ActionType IN ('DELETED', 'TRANSFERRED') THEN 1 ELSE 0 END) AS DeletedTransferredToday,
                    COUNT(DISTINCT UserID) AS ActiveUsersToday
                FROM CASE_ACTIVITY_LOGS
                WHERE Timestamp >= @TodayStart {divFilter};";

            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync(cancellationToken);

            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@TodayStart", today);
            if (divisionId.HasValue && divisionId.Value > 0)
            {
                cmd.Parameters.AddWithValue("@DivisionID", divisionId.Value);
            }

            using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                kpi.TotalToday = reader.IsDBNull(0) ? 0 : reader.GetInt32(0);
                kpi.CreatedToday = reader.IsDBNull(1) ? 0 : reader.GetInt32(1);
                kpi.UpdatedToday = reader.IsDBNull(2) ? 0 : reader.GetInt32(2);
                kpi.DeletedTransferredToday = reader.IsDBNull(3) ? 0 : reader.GetInt32(3);
                kpi.ActiveUsersToday = reader.IsDBNull(4) ? 0 : reader.GetInt32(4);
            }

            return kpi;
        }

        public async Task<List<FieldChangeDiff>> GetLogDiffAsync(long logId, CancellationToken cancellationToken = default)
        {
            var list = new List<FieldChangeDiff>();

            const string sql = "SELECT OldValuesJson, NewValuesJson FROM CASE_ACTIVITY_LOGS WHERE LogID = @LogID;";
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync(cancellationToken);

            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@LogID", logId);

            using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                string? oldJson = reader.IsDBNull(0) ? null : reader.GetString(0);
                string? newJson = reader.IsDBNull(1) ? null : reader.GetString(1);

                list = ParseJsonDiff(oldJson, newJson);
            }

            return list;
        }

        private List<FieldChangeDiff> ComputeFieldDifferences(object oldObj, object newObj)
        {
            var diffs = new List<FieldChangeDiff>();
            if (oldObj == null || newObj == null) return diffs;

            var oldProps = oldObj.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
            var newProps = newObj.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance).ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);

            foreach (var prop in oldProps)
            {
                if (_ignoredFields.Contains(prop.Name)) continue;
                if (!newProps.TryGetValue(prop.Name, out var newProp)) continue;

                // Only compare simple types (strings, primitives, decimals, dates, enums)
                var propType = prop.PropertyType;
                if (propType.IsClass && propType != typeof(string)) continue;

                var oldVal = prop.GetValue(oldObj);
                var newVal = newProp.GetValue(newObj);

                string oldStr = FormatValueForDisplay(oldVal);
                string newStr = FormatValueForDisplay(newVal);

                if (!string.Equals(oldStr, newStr, StringComparison.Ordinal))
                {
                    diffs.Add(new FieldChangeDiff
                    {
                        FieldName = prop.Name,
                        DisplayName = SplitCamelCase(prop.Name),
                        OldValue = oldStr,
                        NewValue = newStr
                    });
                }
            }

            return diffs;
        }

        private List<FieldChangeDiff> ParseJsonDiff(string? oldJson, string? newJson)
        {
            var diffs = new List<FieldChangeDiff>();
            if (string.IsNullOrEmpty(oldJson) && string.IsNullOrEmpty(newJson)) return diffs;

            try
            {
                var oldDict = !string.IsNullOrEmpty(oldJson)
                    ? JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(oldJson) ?? new Dictionary<string, JsonElement>()
                    : new Dictionary<string, JsonElement>();

                var newDict = !string.IsNullOrEmpty(newJson)
                    ? JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(newJson) ?? new Dictionary<string, JsonElement>()
                    : new Dictionary<string, JsonElement>();

                var allKeys = oldDict.Keys.Union(newDict.Keys, StringComparer.OrdinalIgnoreCase);

                foreach (var key in allKeys)
                {
                    if (_ignoredFields.Contains(key)) continue;

                    string oldVal = oldDict.TryGetValue(key, out var oldElem) ? oldElem.ToString() : "";
                    string newVal = newDict.TryGetValue(key, out var newElem) ? newElem.ToString() : "";

                    if (!string.Equals(oldVal, newVal, StringComparison.Ordinal))
                    {
                        diffs.Add(new FieldChangeDiff
                        {
                            FieldName = key,
                            DisplayName = SplitCamelCase(key),
                            OldValue = string.IsNullOrWhiteSpace(oldVal) ? "--" : oldVal,
                            NewValue = string.IsNullOrWhiteSpace(newVal) ? "--" : newVal
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse JSON diff between old and new states.");
            }

            return diffs;
        }

        private static string FormatValueForDisplay(object? val)
        {
            if (val == null) return string.Empty;
            if (val is DateTime dt)
            {
                return dt == DateTime.MinValue ? string.Empty : dt.ToString("dd-MMM-yyyy");
            }
            if (val is decimal dec)
            {
                return dec.ToString("N2");
            }
            if (val is double dbl)
            {
                return dbl.ToString("N2");
            }
            return val.ToString()?.Trim() ?? string.Empty;
        }

        private static string SplitCamelCase(string str)
        {
            if (string.IsNullOrEmpty(str)) return str;
            return System.Text.RegularExpressions.Regex.Replace(str, "([A-Z])", " $1", System.Text.RegularExpressions.RegexOptions.Compiled).Trim();
        }
    }
}
