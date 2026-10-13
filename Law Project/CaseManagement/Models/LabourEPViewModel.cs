using System.ComponentModel.DataAnnotations;

namespace MVCCaseManagement.Models
{
    public class LabourEPViewModel
    {
        public int EPID { get; set; }

        [Required(ErrorMessage = "Case reference is required")]
        public int CaseID { get; set; }
        
        public int DivisionID { get; set; }
        public string? DivisionName { get; set; }

        // 1. Basic EP Details
        [Display(Name = "EP Number")]
        public string? EPNumber { get; set; }

        [Display(Name = "EP Year")]
        public int? EPYear { get; set; }

        [Display(Name = "EP Court")]
        public string? EPCourt { get; set; }

        // Arising out of Labour
        [Display(Name = "Arising Application No.")]
        public string? ArisingFromCaseNo { get; set; }

        [Display(Name = "Arising Application Year")]
        public int? ArisingFromCaseYear { get; set; }

        [Display(Name = "Arising From Labour Court / Tribunal")]
        public string? ArisingFromCourt { get; set; }

        [DataType(DataType.Date)]
        [Display(Name = "Petition Date")]
        public DateTime? PetitionDate { get; set; }

        // Case Award Details (for Audit)
        [Display(Name = "Award Amount")]
        public decimal? AwardAmount { get; set; }

        [DataType(DataType.Date)]
        [Display(Name = "Realization Date")]
        public DateTime? RealizationDate { get; set; }

        [Display(Name = "Calculation Method")]
        public string CalculationMethod { get; set; } = "InterestDeduction"; // Default

        [Display(Name = "Rate of Interest %")]
        public decimal? InterestRate { get; set; }

        [Display(Name = "Liability Percentage %")]
        public decimal? LiabilityPercentage { get; set; }

        // 2. Status of Original Labour Case
        [Display(Name = "Current Status of Arising Case / Application")]
        public string? OriginalCaseStatus { get; set; } // Pending for Approval, Pending before High Court, Pending at Accounts

        // 3. Execution Petition Status
        [Display(Name = "Current Status / Stage of EP")]
        public string? EPStatus { get; set; }

        [DataType(DataType.Date)]
        [Display(Name = "Next Date of Hearing")]
        public DateTime? NextHearingDate { get; set; }

        [Display(Name = "Entrustment No.")]
        public string? EP_EntrustmentNo { get; set; }

        [DataType(DataType.Date)]
        [Display(Name = "Entrustment Date")]
        public DateTime? EP_EntrustmentDate { get; set; }

        [Display(Name = "Advocate")]
        public int? AdvocateID { get; set; }

        [Display(Name = "Advocate Name")]
        public string? AdvocateName { get; set; }

        [Display(Name = "As per e-Courts")]
        public string? AsPerECourts { get; set; }

        // --- e-Courts Live Gateway Integration Fields ---
        [StringLength(16)]
        [Display(Name = "CNR Number")]
        public string? CNRNumber { get; set; }

        [StringLength(50)]
        [Display(Name = "Establishment Code")]
        public string? EstCode { get; set; }

        [StringLength(20)]
        [Display(Name = "Case Type Code")]
        public string? CaseTypeCode { get; set; }

        // Result & Disposal Details
        [Display(Name = "Case Outcome")]
        public string? CaseOutcome { get; set; } // Pending, Disposed

        [DataType(DataType.Date)]
        [Display(Name = "Date of Disposal")]
        public DateTime? DisposalDate { get; set; }

        [DataType(DataType.Date)]
        [Display(Name = "Closure Date")]
        public DateTime? ClosureDate { get; set; }

        [Display(Name = "Closure Remarks")]
        public string? DisposalRemarks { get; set; }

        // 4. Post-Disposal & Approval
        [Display(Name = "Details Sent to Accounts Department?")]
        public bool IsSentToAccounts { get; set; }

        [DataType(DataType.Date)]
        [Display(Name = "Date Sent")]
        public DateTime? DateSentToAccounts { get; set; }

        // 5. Payment Particulars
        [DataType(DataType.Date)]
        [Display(Name = "Petty Bill Date")]
        public DateTime? PettyBillDate { get; set; }

        [Display(Name = "Petty Bill Amount")]
        public decimal? PettyBillAmount { get; set; }

        [Display(Name = "Cheque Number")]
        public string? ChequeNumber { get; set; }

        [DataType(DataType.Date)]
        [Display(Name = "Cheque Date")]
        public DateTime? ChequeDate { get; set; }

        [Display(Name = "Amount Paid")]
        public decimal? AmountPaid { get; set; }

        // 6. Remarks / Compliance
        [Display(Name = "Compliance Status")]
        public string? ComplianceStatus { get; set; } // Paid, Partially Paid, Pending

        [DataType(DataType.Date)]
        [Display(Name = "Date of Compliance")]
        public DateTime? DateOfCompliance { get; set; }

        [Display(Name = "Remarks")]
        public string? Remarks { get; set; }

        // Multiple Payment Details
        public List<LabourEPPaymentViewModel> Payments { get; set; } = new();
    }

    public class LabourEPPaymentViewModel
    {
        public int PaymentID { get; set; }
        public int EPID { get; set; }
        public decimal Amount { get; set; }
        
        [DataType(DataType.Date)]
        public DateTime PaymentDate { get; set; } = DateTime.Now;
        
        public string? ChequeNumber { get; set; }
        
        [DataType(DataType.Date)]
        public DateTime? ChequeDate { get; set; }
        
        public string? Remarks { get; set; }
    }
}
