using System;

namespace MVCCaseManagement.Models
{
    public class GratuityPayment
    {
        public int PaymentID { get; set; }
        public int CaseID { get; set; }
        public decimal? Amount { get; set; }
        public string? ChequeNumber { get; set; }
        public DateTime? ChequeDate { get; set; }
        public DateTime CreatedDate { get; set; } = DateTime.Now;
    }
}
