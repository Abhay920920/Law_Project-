using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace MVCCaseManagement.Models
{
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
        public string? RawJson { get; set; }
        public string? AddedBy { get; set; }

        // Internal Case Remarks & Tracking Metadata
        public string? Remarks { get; set; }
        public DateTime? RemarkUpdatedDate { get; set; }
        public string? RemarkUpdatedBy { get; set; }

        // Disposal & DNP Analysis properties
        public string? NatureOfDisposal { get; set; }
        public string? DisposalDate { get; set; }
        public bool IsDNP { get; set; }

        // Dynamic properties extracted from authentic eCourts NAPIX payload
        public string? CourtEstName { get; set; }
        public string? BenchId { get; set; }
        public string? BenchName { get; set; }
        public string? CauseListType { get; set; }
        public string? CauseListNo { get; set; }
        public string? CauseListItemNo { get; set; }
        public string? ItemNo { get; set; }

        // Daily Proceedings & Hearing Date Analysis properties
        public string? FirstHearingDate { get; set; }
        public string? LastHearingDate { get; set; }
        public string? LastProceeding { get; set; }
        public string? LastHearingJudge { get; set; }
        public int TotalHearings { get; set; }
        public int? DaysToNextHearing { get; set; }
        public int? DaysSinceLastHearing { get; set; }
        public double? AvgDaysBetweenHearings { get; set; }
        public string? HearingUrgency { get; set; } // TODAY, TOMORROW, THIS_WEEK, THIS_MONTH, OVERDUE, UPCOMING, UNSCHEDULED, DISPOSED
        public string? HearingAnalysisSummary { get; set; }
        public List<Dictionary<string, string?>>? ProceedingsHistory { get; set; }
    }

    public class SaveCaseRemarkRequest
    {
        public int TrackedID { get; set; }
        public string? Remarks { get; set; }
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
}
