using MVCCaseManagement.Models;
using System.Collections.Generic;

namespace MVCCaseManagement.DAL
{
    public interface IOtherCourtsRepository
    {
        int SaveCase(OtherCourtsCase model);
        bool UpdateCase(OtherCourtsCase model);
        OtherCourtsCase? GetCaseById(int caseId);
        IEnumerable<OtherCourtsCase> GetAllCases(int divisionId = 0, string? caseType = null, string? litigantType = null);
        int GetTotalCount(int divisionId = 0, string? search = null);
        DashboardStatsViewModel GetDashboardStats(int divisionId, string caseType, string litigantType);
        bool DeleteCase(int caseId);
        bool UpdateLiveSyncInfo(int caseId, string cnr, string status, string? error, DateTime? nextDate, string? stage, string? courtNo, string? judge, string? pendDisp, string? estName);
    }
}
