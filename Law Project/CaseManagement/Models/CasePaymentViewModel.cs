using System;
using System.ComponentModel.DataAnnotations;

namespace MVCCaseManagement.Models
{
    public class CasePaymentViewModel
    {
        public int PaymentID { get; set; }
        public int CaseID { get; set; }
        
        [Required(ErrorMessage = "Amount is required")]
        [Range(0.01, double.MaxValue, ErrorMessage = "Amount must be greater than 0")]
        [Display(Name = "Payment Amount")]
        public decimal Amount { get; set; }
        
        [Display(Name = "Cheque Number")]
        [StringLength(100)]
        public string? ChequeNumber { get; set; }
        
        [Display(Name = "Cheque Date")]
        [DataType(DataType.Date)]
        public DateTime? ChequeDate { get; set; }
        
        [Display(Name = "Payment Type")]
        [StringLength(50)]
        public string? PaymentType { get; set; } // Interim, Advance, Partial Settlement
        
        [Display(Name = "Remarks")]
        [StringLength(500)]
        public string? Remarks { get; set; }
        
        public DateTime CreatedDate { get; set; } = DateTime.Now;
        
        public string? CreatedBy { get; set; }
    }
}
