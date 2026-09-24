using System;
using System.ComponentModel.DataAnnotations;

namespace MVCCaseManagement.Models
{
    public class ArisingApplicationPayment
    {
        public int PaymentID { get; set; }
        public int ArisingID { get; set; }
        
        [Required]
        [Range(0.01, 999999999)]
        public decimal Amount { get; set; }

        [Required]
        [DataType(DataType.Date)]
        public DateTime PaymentDate { get; set; } = DateTime.Now;

        [StringLength(50)]
        public string? ChequeNumber { get; set; }

        [DataType(DataType.Date)]
        public DateTime? ChequeDate { get; set; }

        public string? Remarks { get; set; }

        public DateTime CreatedDate { get; set; } = DateTime.Now;
    }
}
