using System.Text.Json;
using System.Threading.Tasks;

namespace MVCCaseManagement.Utils
{
    public interface IECourtsNapixService
    {
        Task<string> GetAccessTokenAsync();
        Task<JsonElement?> QueryApiAsync(string endpointBasePath, string endpointAction, string pipeParameters);
        Task<JsonElement?> GetStatesAsync(bool isHighCourt = false);
        Task<JsonElement?> GetDistrictsAsync(string stateCode, bool isHighCourt = false);
        Task<JsonElement?> GetCourtComplexesAsync(string stateCode, string distCode);
        Task<JsonElement?> GetHighCourtBenchesAsync(string stateCode);
        Task<JsonElement?> GetCaseTypesAsync(string? estCode, bool isHighCourt = false, string? stateCode = null, string? distCode = null, string type = "all");
        Task<string?> DiscoverCnrAsync(string estCode, string caseType, string regNo, string regYear, bool isHighCourt = false);
        Task<string?> DiscoverCnrByFirAsync(string estCode, string policeStationCode, string firNo, string firYear);
        Task<string?> DiscoverCnrByPartyNameAsync(string estCode, string partyName, string regYear, string pendDisp = "P", bool isHighCourt = false);
        Task<JsonElement?> GetCaseBusinessAsync(string cnrNumber, string date, bool isHighCourt = false);
        Task<JsonElement?> GetCnrDetailsAsync(string cnrNumber, bool isHighCourt = false);
        Task<JsonElement?> GetCauselistAsync(string estCode, string courtNo, string causelistDate, string type = "civil", bool isHighCourt = false);
        Task<JsonElement?> GetOrdersAsync(string cnrNumber, bool isHighCourt = false, List<string>? candidateDates = null);
        Task<JsonElement?> GetHighCourtCauselistBenchesAsync(string estCode, string causelistDate);
        Task<JsonElement?> GetHighCourtCauselistDetailsAsync(string estCode, string benchId, string causelistDate);
        Task<JsonElement?> GetHighCourtShowCauselistAsync(string estCode, string benchId, string causelistId, string causelistDate);
        Task<JsonElement?> GetHighCourtBenchMasterAsync(string estCode);
        Task<byte[]?> GetOrderPdfBytesAsync(string cnrNumber, string orderNo, string orderDate, bool isHighCourt = false);
    }
}
