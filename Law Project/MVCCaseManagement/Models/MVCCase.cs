using System.ComponentModel.DataAnnotations;

namespace MVCCaseManagement.Models
{
    public class MVCCase
    {
        public int CaseID { get; set; }

        [Required(ErrorMessage = "Division is required")]
        public int DivisionID { get; set; }
        public string DivisionName { get; set; } = string.Empty;

        [Required(ErrorMessage = "MVC No is required")]
        [Display(Name = "MVC Number")]
        public string MVCNo { get; set; } = string.Empty;

        [Required(ErrorMessage = "MVC Year is required")]
        [Range(2020, 2030, ErrorMessage = "Year must be between 2020 and 2030")]
        [Display(Name = "MVC Year")]
        public int MVCYear { get; set; }

        [Required(ErrorMessage = "MACT is required")]
        public int MACTID { get; set; }
        public string MACTName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Notice Date is required")]
        [DataType(DataType.Date)]
        [Display(Name = "Notice Date")]
        public DateTime NoticeDate { get; set; }

        [Required]
        public int StatusID { get; set; }
        public string StatusName { get; set; } = string.Empty;

        [DataType(DataType.MultilineText)]
        public string? Remarks { get; set; }

        [DataType(DataType.Date)]
        [Display(Name = "Disposal Date")]
        public DateTime? DisposalDate { get; set; }

        public string? CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }
        public string? ModifiedBy { get; set; }
        public DateTime? ModifiedDate { get; set; }

        // Computed property
        public int? DaysPending
        {
            get
            {
                if (DisposalDate.HasValue || StatusName == "Disposed" || StatusName == "Closed")
                    return null;

                return (DateTime.Now - NoticeDate).Days;
            }
        }
    }
}
