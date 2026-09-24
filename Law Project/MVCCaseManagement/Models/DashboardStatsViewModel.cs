namespace MVCCaseManagement.Models
{
    public class DashboardStatsViewModel
    {
        public int TotalCases { get; set; }
        public int PendingCases { get; set; }
        public int FavorCases { get; set; }
        public int AgainstCases { get; set; }
        public int SentToCOCount { get; set; }
        public int AppealCount { get; set; }
        public int CloseCount { get; set; }
        public int PendingCompetentAuthorityCount { get; set; }
        public int PendingDecisionCount { get; set; }
        public int PendingCLOLOCount { get; set; }

        public int MVCCount { get; set; }
        public int LabourCount { get; set; }
        public int GratuityCount { get; set; }
        
        // New Statistics
        public int CasesThisMonth { get; set; }
        public int UpcomingHearingsCount { get; set; }
        public decimal TotalAwardAmountAgainst { get; set; }
        public decimal MonthlyAwardAmountAgainst { get; set; }
        
        public int SLPPendingCount { get; set; }
        public int WPPendingCount { get; set; }
        public int WritAppealPendingCount { get; set; }
        public int PGACRPendingCount { get; set; }
        public int PGAApplCRPendingCount { get; set; }
        public int MFAPendingCount { get; set; }
        public int CorpMFAPendingCount { get; set; }
        public int ClaimantMFAPendingCount { get; set; }
        public int NoActionTakenCount { get; set; }
        public int FeasibilityReviewCount { get; set; }
        public decimal TotalClaimedAmount { get; set; }
        
        // Snapshot Stats (Cross-Module Unified)
        public int HearingsToday { get; set; }
        public int HearingsTodayMvc { get; set; }
        public int HearingsTodayLabour { get; set; }
        public int HearingsTodayGratuity { get; set; }
        public int HearingsTodayOther { get; set; }

        public int AwardsReceivedToday { get; set; }
        public int AwardsThisMonth { get; set; }

        public int ComplianceDueCount { get; set; }
        public int ComplianceOverdueCount { get; set; }

        public int CriticalDelaysCount { get; set; }
        public int CriticalDelaysRecent { get; set; }       // 1 - 30 days overdue
        public int CriticalDelaysStale { get; set; }        // > 30 days overdue
        public int CriticalDelaysMissingDate { get; set; }  // NextHearingDate is null
        
        public List<RecentCaseViewModel> RecentCases { get; set; } = new List<RecentCaseViewModel>();
    }

    public class RecentCaseViewModel
    {
        public int CaseID { get; set; }
        public string MVCNo { get; set; } = "";
        public string MACTName { get; set; } = "";
        public string? DisposalResult { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? NextHearingDate { get; set; }
    }

    public class GlobalCaseViewModel
    {
        public int CaseID { get; set; }
        public string ModuleName { get; set; } = "";
        public string CaseNo { get; set; } = "";
        public string CourtOrAuthority { get; set; } = "";
        public string? DisposalResult { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? NextHearingDate { get; set; }
        public string DetailsUrl { get; set; } = "";
        public string? CNRNumber { get; set; }
        public int DaysOverdue { get; set; }
        public string? Stage { get; set; }
        public string? CourtHall { get; set; }
        public bool IsHighCourt { get; set; }
    }
}
