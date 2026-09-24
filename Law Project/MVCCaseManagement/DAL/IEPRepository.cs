using System.Collections.Generic;
using MVCCaseManagement.Models;

namespace MVCCaseManagement.DAL
{
    public interface IEPRepository
    {
        IEnumerable<EPViewModel> GetAllEPs(int divisionId = 0, int pageNumber = 1, int pageSize = 10, string? search = null, string? status = null);
        int GetTotalEPCount(int divisionId = 0, string? search = null, string? status = null);
        EPViewModel? GetEPById(int epId);
        int SaveEP(EPViewModel model);
        bool UpdateEP(EPViewModel model);
    }
}
