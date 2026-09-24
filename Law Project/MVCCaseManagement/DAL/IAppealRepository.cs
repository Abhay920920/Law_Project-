using MVCCaseManagement.Models;

namespace MVCCaseManagement.DAL
{
    public interface IAppealRepository
    {
        AppealViewModel GetAppealByCaseId(int caseId);
        void SaveAppeal(AppealViewModel model);
        IEnumerable<AppealViewModel> GetAllClaimantAppeals(int divisionId = 0);
        AppealViewModel? GetAppealByMFADetails(string mfaNo, int mfaYear);
    }
}
