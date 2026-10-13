using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MVCCaseManagement.Models.Audit;

namespace MVCCaseManagement.Services.Audit
{
    public interface ICaseActivityLogger
    {
        Task LogCaseCreatedAsync(string module, int caseId, string caseNumber, string? vehicleNo, string? courtName, object newValues, string? customSummary = null, CancellationToken cancellationToken = default);
        Task LogCaseUpdatedAsync(string module, int caseId, string caseNumber, string? vehicleNo, string? courtName, object oldValues, object newValues, string? customSummary = null, CancellationToken cancellationToken = default);
        Task LogCaseDeletedAsync(string module, int caseId, string caseNumber, string? vehicleNo, string reason, CancellationToken cancellationToken = default);
        Task LogCaseTransferredAsync(string module, int caseId, string caseNumber, int fromDivisionId, int toDivisionId, string fromDivisionName, string toDivisionName, string? reason = null, CancellationToken cancellationToken = default);
        Task LogSubEntityActionAsync(string module, int caseId, string caseNumber, string actionType, string summary, object? details = null, CancellationToken cancellationToken = default);

        Task<ActivityLogPagedResult> GetLogSheetAsync(ActivityLogFilter filter, CancellationToken cancellationToken = default);
        Task<ActivityLogKpiSummary> GetSummaryKpisAsync(int? divisionId = null, CancellationToken cancellationToken = default);
        Task<List<FieldChangeDiff>> GetLogDiffAsync(long logId, CancellationToken cancellationToken = default);
    }
}
