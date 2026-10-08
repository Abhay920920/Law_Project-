using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace MVCCaseManagement.Models
{

    public class ECourtsState
    {
        [JsonPropertyName("state_code")]
        public string StateCode { get; set; } = string.Empty;

        [JsonPropertyName("state_name")]
        public string StateName { get; set; } = string.Empty;
    }

    public class ECourtsDistrict
    {
        [JsonPropertyName("dist_census_code")]
        public string DistCensusCode { get; set; } = string.Empty;

        [JsonPropertyName("dist_name")]
        public string DistName { get; set; } = string.Empty;

        [JsonPropertyName("state_code")]
        public string StateCode { get; set; } = string.Empty;
    }

    public class ECourtsEstablishment
    {
        [JsonPropertyName("est_code")]
        public string EstCode { get; set; } = string.Empty;

        [JsonPropertyName("court_est_name")]
        public string CourtEstName { get; set; } = string.Empty;

        [JsonPropertyName("dist_code")]
        public string DistCode { get; set; } = string.Empty;
    }

    public class ECourtsCaseType
    {
        [JsonPropertyName("case_type_code")]
        public string CaseTypeCode { get; set; } = string.Empty;

        [JsonPropertyName("type_name")]
        public string TypeName { get; set; } = string.Empty;
    }

    public class ECourtsHighCourtBench
    {
        [JsonPropertyName("bench_code")]
        public string BenchCode { get; set; } = string.Empty;

        [JsonPropertyName("bench_name")]
        public string BenchName { get; set; } = string.Empty;
    }

    public class ECourtsApiResponse<T>
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public T? Data { get; set; }
    }

    public class TrackedCaseModel
    {
        public int TrackedID { get; set; }
        public string CNRNumber { get; set; } = string.Empty;
        public string EstCode { get; set; } = string.Empty;
        public string CaseTypeCode { get; set; } = string.Empty;
        public string CaseNumber { get; set; } = string.Empty;
        public int CaseYear { get; set; }
        public int? RelatedCaseID { get; set; }
        public string? RelatedModule { get; set; } // MVC, Labour, Gratuity, Other
        public int DivisionID { get; set; }
        public string? Petitioner { get; set; }
        public string? Respondent { get; set; }
        public string? CaseStatus { get; set; }
        public DateTime? NextHearingDate { get; set; }
        public string? StagePurpose { get; set; }
        public string? CourtNo { get; set; }
        public string? JudgeDesignation { get; set; }
        public DateTime LastSyncedAt { get; set; }
        public DateTime CreatedDate { get; set; }
    }

    public class DailyCauseListHeaderModel
    {
        public int ListID { get; set; }
        public string EstCode { get; set; } = string.Empty;
        public string CourtNo { get; set; } = string.Empty;
        public DateTime CauseListDate { get; set; }
        public string ListType { get; set; } = "civil"; // civil, criminal
        public int TotalCases { get; set; }
        public int CorpCasesCount { get; set; }
        public DateTime CreatedDate { get; set; }
    }

    public class DailyCauseListItemModel
    {
        public int ItemID { get; set; }
        public int ListID { get; set; }
        public int ItemNo { get; set; }
        public string? CaseNumber { get; set; }
        public string? CNRNumber { get; set; }
        public string? CaseType { get; set; }
        public string? Petitioner { get; set; }
        public string? Respondent { get; set; }
        public string? AdvocateName { get; set; }
        public string? Stage { get; set; }
        public bool IsCorpCase { get; set; }
        public string? CourtName { get; set; }
        public string? JudgeName { get; set; }
    }

    public class CaseHearingHistoryModel
    {
        public int HearingID { get; set; }
        public string CNRNumber { get; set; } = string.Empty;
        public DateTime HearingDate { get; set; }
        public string? BusinessTransacted { get; set; }
        public DateTime? NextHearingDate { get; set; }
        public string? NextStage { get; set; }
        public DateTime CreatedDate { get; set; }
    }

    public class CaseOrderModel
    {
        public int OrderID { get; set; }
        public string CNRNumber { get; set; } = string.Empty;
        public DateTime OrderDate { get; set; }
        public string? OrderType { get; set; }
        public string? LocalPdfPath { get; set; }
        public string? ExternalUrl { get; set; }
        public DateTime CreatedDate { get; set; }
    }
}
