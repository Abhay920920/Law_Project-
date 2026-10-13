using System.ComponentModel.DataAnnotations;

namespace MVCCaseManagement.Models
{
    public class PettyBillViewModel
    {
        // Bill Identification (for saved bills)
        public int BillID { get; set; }
        
        // Case Identification
        public int? CaseID { get; set; }
        public string MVCNo { get; set; } = string.Empty;
        public int? MVCYear { get; set; }
        public string? MACTName { get; set; }
        public string? DivisionName { get; set; }
        
        // Case Details
        public string? AppealNumber { get; set; }
        public string? VehicleNo { get; set; }
        public DateTime? AccidentDate { get; set; }
        public string? PetitionerName { get; set; }
        
        // Bill Details
        public string? ApprovalLetterNo { get; set; }
        public DateTime? ApprovalDate { get; set; }
        public string? FileRefNo { get; set; }
        public DateTime? BillDate { get; set; } = DateTime.Now;

        // NEW: Award Phase tracking
        public string AwardType { get; set; } = "Lower Court"; // Lower Court, High Court (Enhancement), High Court (Reduction)
        public decimal BaseAwardAmount { get; set; } // The original award amount
        
        // Custom Interest Duration (mainly for High Court / Enhancement)
        public DateTime? InterestFrom { get; set; }
        public DateTime? InterestTo { get; set; }

        // Interest Relaxation
        public bool IsRelaxationApplied { get; set; }
        public DateTime? RelaxationFrom { get; set; }
        public DateTime? RelaxationTo { get; set; }
        
        // Financial Details
        public decimal EnhancedAmount { get; set; } // This acts as the CURRENT Award Amount for calculations
        public decimal CourtCost { get; set; }
        public decimal TDSPercentage { get; set; } = 10;
        
        // Interest Entries
        public List<InterestEntry> InterestEntries { get; set; } = new();
        
        // Previous Payments
        public List<PreviousPayment> PreviousPayments { get; set; } = new();
        
        // Calculated Fields (stored for persistence)
        public decimal TotalInterest { get; set; }
        public decimal GrossPayable { get; set; }
        public decimal TotalPreviousPayments { get; set; }
        public decimal TDSAmount { get; set; }
        public decimal TotalDeductions { get; set; }
        public decimal NetPayable { get; set; }
    }
    
    public class InterestEntry
    {
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public decimal Amount { get; set; }
    }
    
    public class PreviousPayment
    {
        public int PaymentID { get; set; } // Database ID (if exists)
        public string ChequeNumber { get; set; } = string.Empty;
        public DateTime? ChequeDate { get; set; }
        public decimal Amount { get; set; }
        public bool IsSelected { get; set; } = true;
        public bool IsCancelled { get; set; } = false; // NEW: Track cancelled cheques/payments
        public string? Remarks { get; set; }
    }
    
    // Alias for compatibility with repository
    public class PaymentEntry : PreviousPayment { }
}
