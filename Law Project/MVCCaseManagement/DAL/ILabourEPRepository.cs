using System.Collections.Generic;
using MVCCaseManagement.Models;

namespace MVCCaseManagement.DAL
{
    public interface ILabourEPRepository
    {
        IEnumerable<LabourEPViewModel> GetAllEPs(int divisionId = 0, int pageNumber = 1, int pageSize = 10, string? search = null, string? status = null);
        int GetTotalEPCount(int divisionId = 0, string? search = null, string? status = null);
        LabourEPViewModel? GetEPById(int epId);
        int SaveEP(LabourEPViewModel model);
        bool UpdateEP(LabourEPViewModel model);
        IEnumerable<LabourEPViewModel> GetEPsByCaseId(int caseId);
        IEnumerable<LabourEPViewModel> GetEPsByArisingApplication(int parentCaseId, string? arisingCaseNo);
    }
}
