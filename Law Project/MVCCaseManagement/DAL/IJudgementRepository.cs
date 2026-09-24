using MVCCaseManagement.Models;

namespace MVCCaseManagement.DAL
{
    public interface IJudgementRepository
    {
        IEnumerable<JudgementViewModel> GetAllJudgements();
        JudgementViewModel? GetJudgementById(int id);
        void SaveJudgement(JudgementViewModel model);
        void DeleteJudgement(int id);
    }
}
