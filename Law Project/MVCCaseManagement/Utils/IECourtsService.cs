using System.Collections.Generic;
using System.Threading.Tasks;
using MVCCaseManagement.Models;

namespace MVCCaseManagement.Utils
{
    public interface IECourtsService
    {
        Task<ECourtsApiResponse<List<ECourtsState>>> GetStatesAsync();
        Task<ECourtsApiResponse<List<ECourtsDistrict>>> GetDistrictsAsync(string stateCode);
        Task<ECourtsApiResponse<List<ECourtsEstablishment>>> GetCourtComplexesAsync(string stateCode, string distCode);
        Task<ECourtsApiResponse<List<ECourtsCaseType>>> GetCaseTypesAsync(string estCode);
        Task<ECourtsApiResponse<List<ECourtsHighCourtBench>>> GetHighCourtBenchesAsync(string stateCode);
        
        Task<ECourtsApiResponse<Dictionary<string, object>>> FetchCaseByNumberAsync(string estCode, string caseTypeCode, string regNo, string regYear);
        Task<ECourtsApiResponse<Dictionary<string, object>>> FetchCaseByCNRAsync(string cnrNumber);
        Task<ECourtsApiResponse<List<DailyCauseListItemModel>>> GetCauseListAsync(string estCode, string courtNo, string causelistDate, string listType = "civil");
        Task<Dictionary<string, object>> DiagnoseConnectionAsync();
    }
}
