using System;

namespace MVCCaseManagement.Models
{
    public class GratuityInterestPayment
    {
        public int InterestPaymentID { get; set; }
        public int CaseID { get; set; }
        public decimal? InterestRate { get; set; }
        public decimal? Amount { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public string? ChequeNumber { get; set; }
        public DateTime? ChequeDate { get; set; }
        public DateTime CreatedDate { get; set; } = DateTime.Now;
    }
}
