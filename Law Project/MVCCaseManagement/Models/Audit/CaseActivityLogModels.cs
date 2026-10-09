using System;
using System.Collections.Generic;

namespace MVCCaseManagement.Models.Audit
{
    public class CaseActivityLog
    {
        public long LogID { get; set; }
        public DateTime Timestamp { get; set; }
        public int? UserID { get; set; }
        public string Username { get; set; } = string.Empty;
        public string? UserFullName { get; set; }
        public string? UserRole { get; set; }
        public int? DivisionID { get; set; }
        public string? DivisionName { get; set; }
        public string? IpAddress { get; set; }
        public string Module { get; set; } = "MVC";
        public int? CaseID { get; set; }
        public string CaseNumber { get; set; } = string.Empty;
        public string? VehicleNo { get; set; }
        public string? CourtName { get; set; }
        public string ActionType { get; set; } = "CREATED"; // CREATED, UPDATED, DELETED, TRANSFERRED, PAYMENT_ADDED, PAYMENT_DELETED, NOTING_ADDED, NOTING_DELETED
        public string ActionSummary { get; set; } = string.Empty;
        public string? ChangedFieldsSummary { get; set; }
        public string? OldValuesJson { get; set; }
        public string? NewValuesJson { get; set; }
    }

    public class ActivityLogFilter
    {
        public string? SearchTerm { get; set; }
        public string? Module { get; set; } = "all";
        public string? ActionType { get; set; } = "all";
        public int? DivisionID { get; set; }
        public string? DatePreset { get; set; } = "all"; // all, today, yesterday, week, month, custom
        public DateTime? DateFrom { get; set; }
        public DateTime? DateTo { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 25;
    }

    public class ActivityLogPagedResult
    {
        public List<CaseActivityLog> Logs { get; set; } = new List<CaseActivityLog>();
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalPages => (int)Math.Ceiling((double)TotalCount / (PageSize > 0 ? PageSize : 25));
        public ActivityLogFilter Filter { get; set; } = new ActivityLogFilter();
        public ActivityLogKpiSummary KpiSummary { get; set; } = new ActivityLogKpiSummary();
    }

    public class ActivityLogKpiSummary
    {
        public int TotalToday { get; set; }
        public int CreatedToday { get; set; }
        public int UpdatedToday { get; set; }
        public int DeletedTransferredToday { get; set; }
        public int ActiveUsersToday { get; set; }
    }

    public class FieldChangeDiff
    {
        public string FieldName { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string? OldValue { get; set; }
        public string? NewValue { get; set; }
    }
}
