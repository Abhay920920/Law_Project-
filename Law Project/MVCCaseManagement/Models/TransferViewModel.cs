using System.ComponentModel.DataAnnotations;

namespace MVCCaseManagement.Models
{
    public class TransferViewModel
    {
        public int CaseID { get; set; }

        [Display(Name = "Case Reference (KID/ID/REF No/Year)")]
        public string? CaseNumber { get; set; }

        public int FromDivisionID { get; set; }

        [Display(Name = "Current Division")]
        public string? FromDivisionName { get; set; }

        [Required(ErrorMessage = "Please select target division")]
        [Display(Name = "Transfer To Division")]
        public int ToDivisionID { get; set; }

        [Display(Name = "Transfer Remarks")]
        public string? TransferRemarks { get; set; }

        // Display-only fields once case is found
        public string? PetitionerName { get; set; }
        public string? EmployeeNo { get; set; }
        public string? CourtName { get; set; }
        public string? WorkingStatus { get; set; }
    }
}
