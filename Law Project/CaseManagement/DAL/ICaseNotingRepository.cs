using System.Collections.Generic;
using MVCCaseManagement.Models;

namespace MVCCaseManagement.DAL
{
    public interface ICaseNotingRepository
    {
        List<CaseNoting> GetNotings(string caseType, int caseId);
        CaseNoting? GetNotingById(int notingId);
        int AddNoting(CaseNoting noting);
        bool DeleteNoting(int notingId);
    }
}
