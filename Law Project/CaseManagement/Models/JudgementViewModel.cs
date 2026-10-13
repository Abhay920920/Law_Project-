using System.ComponentModel.DataAnnotations;

namespace MVCCaseManagement.Models
{
    public class JudgementViewModel
    {
        public int JudgementID { get; set; }

        [Required]
        [Display(Name = "Judgement Title")]
        public string Title { get; set; } = string.Empty;

        [Display(Name = "Court / Authority")]
        public string? Court { get; set; }

        [DataType(DataType.Date)]
        [Display(Name = "Date of Judgement")]
        public DateTime? JudgementDate { get; set; }

        [Display(Name = "Remarks / Key Points")]
        public string? Remarks { get; set; }

        [Required]
        [Display(Name = "Category")]
        public string Category { get; set; } = "Judgement";

        public string? FilePath { get; set; }

        [Display(Name = "Upload Copy")]
        public IFormFile? JudgementFile { get; set; }

        public string? UploadedBy { get; set; }
        public DateTime UploadedDate { get; set; }
    }
}
