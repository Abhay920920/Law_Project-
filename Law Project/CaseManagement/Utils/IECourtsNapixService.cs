using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;

namespace MVCCaseManagement.Utils
{
    public interface IECourtsNapixService
    {
        Task<string> GetAccessTokenAsync(string module = "MVC");
        Task<JsonElement?> QueryApiAsync(string endpointBasePath, string endpointAction, string pipeParameters, string module = "MVC");
        Task<JsonElement?> GetStatesAsync(bool isHighCourt = false, string module = "MVC");
        Task<JsonElement?> GetDistrictsAsync(string stateCode, bool isHighCourt = false, string module = "MVC");
        Task<JsonElement?> GetCourtComplexesAsync(string stateCode, string distCode, string module = "MVC");
        Task<JsonElement?> GetHighCourtBenchesAsync(string stateCode, string module = "MVC");
        Task<JsonElement?> GetCaseTypesAsync(string? estCode, bool isHighCourt = false, string? stateCode = null, string? distCode = null, string type = "all", string module = "MVC");
        Task<string?> DiscoverCnrAsync(string estCode, string caseType, string regNo, string regYear, bool isHighCourt = false, string module = "MVC");
        Task<string?> DiscoverCnrByFirAsync(string estCode, string policeStationCode, string firNo, string firYear, string module = "MVC");
        Task<string?> DiscoverCnrByPartyNameAsync(string estCode, string partyName, string regYear, string pendDisp = "P", bool isHighCourt = false, string module = "MVC");
        Task<JsonElement?> GetCaseBusinessAsync(string cnrNumber, string date, bool isHighCourt = false, string module = "MVC");
        Task<JsonElement?> GetCnrDetailsAsync(string cnrNumber, bool isHighCourt = false, string module = "MVC");
        Task<JsonElement?> GetCurrentStatusAsync(string cnr, bool isHighCourt = false, string module = "MVC");
        Task<JsonElement?> GetCauselistAsync(string estCode, string courtNo, string causelistDate, string type = "civil", bool isHighCourt = false, string module = "MVC");
        Task<JsonElement?> GetOrdersAsync(string cnrNumber, bool isHighCourt = false, List<string>? candidateDates = null, string module = "MVC");
        Task<JsonElement?> GetHighCourtCauselistBenchesAsync(string estCode, string causelistDate, string module = "MVC");
        Task<JsonElement?> GetHighCourtCauselistDetailsAsync(string estCode, string benchId, string causelistDate, string module = "MVC");
        Task<JsonElement?> GetHighCourtShowCauselistAsync(string estCode, string benchId, string causelistId, string causelistDate, string module = "MVC");
        Task<JsonElement?> GetHighCourtBenchMasterAsync(string estCode, string module = "MVC");
        Task<byte[]?> GetOrderPdfBytesAsync(string cnrNumber, string orderNo, string orderDate, bool isHighCourt = false, string module = "MVC");
        string? GetLastModuleError(string module = "MVC");
        void ResetCircuitBreaker();
    }
}
