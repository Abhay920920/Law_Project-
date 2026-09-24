using MVCCaseManagement.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MVCCaseManagement.DAL
{
    public interface IGratuityRepository
    {
        Task<IEnumerable<GratuityCase>> GetAllCases(int divisionId = 0, int pageNumber = 1, int pageSize = 10, string? search = null, string? status = null);
        Task<int> GetTotalCaseCount(int divisionId = 0, string? search = null, string? status = null);
        Task<GratuityCase?> GetCaseById(int id);
        Task<int> AddCase(GratuityCase gratuityCase);
        Task<bool> UpdateCase(GratuityCase gratuityCase);
        Task<bool> DeleteCase(int id);
        Task<bool> MarkAsViewedByCO(int caseId);
        
        // Additional helper methods if needed
        Task<IEnumerable<GratuityCase>> GetCasesByDivision(int divisionId);
        Task<DashboardStatsViewModel> GetDashboardStats(int divisionId = 0);
        Task<List<RecentCaseViewModel>> GetCasesByHearingDate(DateTime hearingDate, int divisionId = 0, DateTime? endDate = null);
    }
}
