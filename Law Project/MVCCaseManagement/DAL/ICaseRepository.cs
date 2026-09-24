using MVCCaseManagement.Models;

namespace MVCCaseManagement.DAL
{
    public interface ICaseRepository
    {
        IEnumerable<ConnectedCaseViewModel> SearchLinkedCases(string vehicleNo, DateTime accidentDate);
        int SaveCase(MVCCaseViewModel model);
        MVCCaseViewModel? GetCaseById(int caseId);
        bool UpdateCase(MVCCaseViewModel model);
        IEnumerable<MVCCaseViewModel> GetAllCases(int divisionId = 0, int pageNumber = 1, int pageSize = 10, string? search = null, string? status = null);
        int GetTotalCaseCount(int divisionId = 0, string? search = null, string? status = null);
        DashboardStatsViewModel GetDashboardStats(int divisionId = 0);
        void MarkCaseAsViewed(int caseId);
        List<string> GetAdvocatesByDivision(int divisionId);
        List<string> GetHighCourtAdvocatesByBench(string benchCode);
        MVCCaseViewModel? GetCaseByMvcDetails(int divisionId, string mvcNo, int mvcYear, int mactId);
        MVCCaseViewModel? GetCaseByMVCDetails(string mvcNo, int mvcYear, int mactId);
        MVCCaseViewModel? GetCaseByAuditCriteria(string mvcNo, string vehicleNo, DateTime accidentDate);
        bool SaveCasePayments(int caseId, List<CasePaymentViewModel> payments);
        List<CasePaymentViewModel> GetCasePayments(int caseId);
        List<RecentCaseViewModel> GetCasesByHearingDate(DateTime hearingDate, int divisionId = 0, DateTime? endDate = null);
        List<GlobalCaseViewModel> GetHearingsTodayIrrespectiveOfModule(int divisionId = 0, string? module = null);
        List<GlobalCaseViewModel> GetAwardsTodayIrrespectiveOfModule(int divisionId = 0, string? module = null);
        List<GlobalCaseViewModel> GetComplianceDueIrrespectiveOfModule(int divisionId = 0, string? module = null);
        List<GlobalCaseViewModel> GetCriticalDelaysIrrespectiveOfModule(int divisionId = 0, string? module = null);
        int CreateSkeletonCase(string mvcNo, int mvcYear, int mactId, int divisionId);
        int SaveRemindBackCase(MVCCaseViewModel model);
        List<MVCCaseViewModel> GetAllRemindBackCases(int divisionId = 0, int page = 1, int pageSize = 10, string? search = null);
        int GetTotalRemindBackCaseCount(int divisionId = 0, string? search = null);
        MVCCaseViewModel? GetRemindBackCaseById(int remindBackId);
        
        // CASE TRANSFER
        bool TransferCase(int caseId, int toDivisionId, string remarks);
        IEnumerable<MVCCaseViewModel> GetRecentTransfers(int divisionId);
        void MarkTransferAsViewed(int caseId);

        // e-Courts Live Sync
        bool UpdateLiveSyncInfo(int caseId, DateTime? nextHearingDate, string? stage, string? courtHall);
        bool UpdateCNR(int caseId, string? cnrNumber, string? estCode);

        // Third Party Cases
        List<MVCCaseViewModel> GetAllThirdPartyCases(int divisionId = 0, int page = 1, int pageSize = 10, string? search = null);
        int GetTotalThirdPartyCaseCount(int divisionId = 0, string? search = null);
    }
}
