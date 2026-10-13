using System;
using System.Collections.Generic;

namespace MVCCaseManagement.Models
{
    public class HighCourtCauseListViewModel
    {
        public DateTime SelectedDate { get; set; } = DateTime.Today;
        public string? SelectedBench { get; set; }
        public string? SelectedDivision { get; set; }
        public string? SelectedAppealType { get; set; }
        public string? SearchQuery { get; set; }

        public int TotalRegisteredCases { get; set; }
        public int ListedTodayCount { get; set; }
        public int ListedThisWeekCount { get; set; }
        public int SyncedCnrCount { get; set; }

        public List<HighCourtCauseListItem> Items { get; set; } = new List<HighCourtCauseListItem>();
        public List<string> Benches { get; set; } = new List<string> { "All", "Dharwad", "Kalaburagi", "Bangalore" };
        public List<string> Divisions { get; set; } = new List<string>();
    }

    public class HighCourtCauseListItem
    {
        public int AppealID { get; set; }
        public int CaseID { get; set; }
        public string AppealType { get; set; } = "Corporation MFA"; // "Corporation MFA" or "Claimant MFA"
        public string? MFANumber { get; set; }
        public int? MFAYear { get; set; }
        public string? CNRNumber { get; set; }
        public string HighCourtBench { get; set; } = "Dharwad";
        public string? CourtHall { get; set; }
        public string? ArisingMVCNo { get; set; }
        public int? ArisingMVCYear { get; set; }
        public string? DivisionName { get; set; }
        public string? MACTName { get; set; }
        public DateTime? NextHearingDate { get; set; }
        public string? CaseStage { get; set; }
        public string? PetitionerName { get; set; }
        public string? RespondentName { get; set; }

        public string DisplayMFANo => !string.IsNullOrEmpty(MFANumber) 
            ? (MFAYear.HasValue && MFAYear > 0 ? $"{MFANumber}/{MFAYear}" : MFANumber)
            : "Pending Allocation";

        public string ListingStatus
        {
            get
            {
                if (!NextHearingDate.HasValue) return "Pending Schedule";
                var dt = NextHearingDate.Value.Date;
                var today = DateTime.Today;
                if (dt == today) return "Listed Today";
                if (dt > today && dt <= today.AddDays(7)) return "Upcoming This Week";
                if (dt > today) return "Future Hearing";
                return "Past Hearing";
            }
        }
    }
}
