using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;

namespace MVCCaseManagement.DAL
{
    public class TrackedCaseModel
    {
        public int TrackedCaseID { get; set; }
        public string? CNRNumber { get; set; }
        public string EstCode { get; set; } = "";
        public string CaseTypeCode { get; set; } = "";
        public string RegNo { get; set; } = "";
        public int RegYear { get; set; }
        public string? PetitionerName { get; set; }
        public string? RespondentName { get; set; }
        public string? CurrentStage { get; set; }
        public DateTime? NextHearingDate { get; set; }
        public string? CourtNo { get; set; }
        public string? JudgeName { get; set; }
        public bool IsHighCourt { get; set; }
        public DateTime LastSyncedDate { get; set; }
    }

    public class CauselistHeaderModel
    {
        public int CauselistID { get; set; }
        public string EstCode { get; set; } = "";
        public string CourtNo { get; set; } = "";
        public DateTime CauselistDate { get; set; }
        public string CauselistType { get; set; } = "civil";
        public int TotalCases { get; set; }
        public int CorporationCasesCount { get; set; }
        public List<CauselistItemModel> Items { get; set; } = new List<CauselistItemModel>();
    }

    public class CauselistItemModel
    {
        public int ItemID { get; set; }
        public int CauselistID { get; set; }
        public int SrNo { get; set; }
        public string? CNRNumber { get; set; }
        public string? CaseNumber { get; set; }
        public string? PartyDetails { get; set; }
        public string? AdvocateDetails { get; set; }
        public string? Stage { get; set; }
        public bool IsCorporationCase { get; set; }
        public int? TrackedCaseID { get; set; }
    }

    public interface IECourtsRepository
    {
        Task<int> SaveTrackedCaseAsync(TrackedCaseModel model);
        Task<TrackedCaseModel?> GetTrackedCaseByCnrAsync(string cnrNumber);
        Task<IEnumerable<TrackedCaseModel>> GetAllTrackedCasesAsync();
        Task<bool> UpdateCaseCnrLinkAsync(string table, string primaryKeyCol, int id, string cnrNumber);
        Task<int> SaveCauselistAsync(CauselistHeaderModel header);
        Task<CauselistHeaderModel?> GetCauselistAsync(string estCode, string courtNo, DateTime date, string type = "civil");
        Task<IEnumerable<MVCCaseManagement.Models.HighCourtCauseListItem>> GetHighCourtCauseListAsync();
        Task UpdateAppealLiveECourtsDataAsync(string? cnrNumber, string? courtHall, DateTime? nextHearingDate, string? stage, int? appealId = null);
        Task UpdateTrackedCaseStatusAsync(string cnrNumber, string? status, DateTime? nextHearingDate, string? stage);
        Task SyncLiveCaseDataAsync(string cnrNumber, DateTime? nextHearingDate, string? stage, string? courtHall, string? caseStatus, int? caseId = null, string? module = null, int? appealId = null);
    }
}
