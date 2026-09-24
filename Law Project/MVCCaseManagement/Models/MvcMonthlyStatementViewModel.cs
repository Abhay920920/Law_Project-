using System;
using System.Collections.Generic;

namespace MVCCaseManagement.Models
{
    public class MvcMonthlyStatementViewModel
    {
        public int SelectedMonth { get; set; }
        public int SelectedYear { get; set; }
        public string SelectedModule { get; set; } = "MVC"; // "MVC" or "Labour"
        public string SelectedCaseType { get; set; } = "ALL"; // "ALL", "KID", "ID", "ArisingApplication"
        public string SelectedView { get; set; } = "MonthlyStatement"; // "MonthlyStatement" or "HighCourt"
        public HighCourtSupremeStatementData HighCourtData { get; set; } = new();
        public MmrSt2StatementData MmrSt2Data { get; set; } = new();
        public List<DivisionColumnData> Divisions { get; set; } = new();
    }

    public class MmrSt2RowData
    {
        public string CourtName { get; set; } = "";
        public int BeginningPending { get; set; }
        public int EntrustedCount { get; set; }
        public int TotalCases => BeginningPending + EntrustedCount;
        public int DisposedFavorCount { get; set; }
        public int DisposedAgainstCount { get; set; }
        public int TotalDisposed => DisposedFavorCount + DisposedAgainstCount;
        public int EndingPending => TotalCases - TotalDisposed;
    }

    public class MmrSt2StatementData
    {
        public MmrSt2RowData SupremeCourt { get; set; } = new MmrSt2RowData { CourtName = "SUPREME COURT" };
        public MmrSt2RowData HighCourt { get; set; } = new MmrSt2RowData { CourtName = "HIGH COURT" };
        public MmrSt2RowData OtherCourts { get; set; } = new MmrSt2RowData { CourtName = "OTHER COURTS" };

        public int TotalBeginningPending => SupremeCourt.BeginningPending + HighCourt.BeginningPending + OtherCourts.BeginningPending;
        public int TotalEntrustedCount => SupremeCourt.EntrustedCount + HighCourt.EntrustedCount + OtherCourts.EntrustedCount;
        public int TotalCases => SupremeCourt.TotalCases + HighCourt.TotalCases + OtherCourts.TotalCases;
        public int TotalDisposedFavor => SupremeCourt.DisposedFavorCount + HighCourt.DisposedFavorCount + OtherCourts.DisposedFavorCount;
        public int TotalDisposedAgainst => SupremeCourt.DisposedAgainstCount + HighCourt.DisposedAgainstCount + OtherCourts.DisposedAgainstCount;
        public int TotalDisposed => SupremeCourt.TotalDisposed + HighCourt.TotalDisposed + OtherCourts.TotalDisposed;
        public int TotalEndingPending => SupremeCourt.EndingPending + HighCourt.EndingPending + OtherCourts.EndingPending;
    }

    public class HighCourtSectionData
    {
        public int BeginningPending { get; set; }
        public int EntrustedCorp { get; set; }
        public int EntrustedClaimant { get; set; }
        public int TotalDisposed { get; set; }
        public int DisposedFavorCorp { get; set; }
        public int DisposedFavorClaimant { get; set; }
        public int EndingPending { get; set; }
    }

    public class HighCourtSupremeStatementData
    {
        // 1. MFA CASES (MVC)
        public HighCourtSectionData MfaCases { get; set; } = new();

        // 2. LABOUR COURT CASES IN W.P.
        public HighCourtSectionData LabourWpCases { get; set; } = new();

        // 3. LABOUR COURT CASES IN W.A.
        public HighCourtSectionData LabourWaCases { get; set; } = new();

        // 5. S.L.P. CASES (Supreme Court)
        public int SlpMvcCasesCount { get; set; }
        public int SlpLabourCasesCount { get; set; }
    }

    public class DivisionColumnData
    {
        public int DivisionID { get; set; }
        public string DivisionCode { get; set; } = "";
        public string DivisionName { get; set; } = "";
        
        // 1. Pending at beginning of month
        public int BeginningPending { get; set; }
        
        // 2. Entrusted during month
        public int EntrustedCount { get; set; }
        
        // 3. Disposed in favour of NWKRTC
        public int DisposedFavorCount { get; set; }
        
        // 4. Disposed against NWKRTC
        public int DisposedAgainstCount { get; set; }

        // 5. Received from other division (Labour)
        public int ReceivedFromOtherDivisionCount { get; set; }
        
        // 6. Transferred to other division
        public int TransferredCount { get; set; }
        
        // 7. Pending at end of month (Computed property or explicit)
        public int CalculatedEndingPending => BeginningPending + EntrustedCount + ReceivedFromOtherDivisionCount - (DisposedFavorCount + DisposedAgainstCount + TransferredCount);
        public int EndingPending { get; set; }

        // 8. Compensation paid in MVC case for the month
        public decimal CompensationPaidAmount { get; set; }
        
        // No. of cases Compensation paid in MVC case for the month
        public int CompensationPaidCaseCount { get; set; }
        
        // Outstanding balance pending at Accounts (MVC amount)
        public decimal OutstandingBalanceAmount { get; set; }

        // Outstanding balance pending at Accounts In no. of cases (Labour count)
        public int AccountsPendingCasesCount { get; set; }

        // Amount due in gratuity cases
        public decimal GratuityAmountDue { get; set; }
    }
}
