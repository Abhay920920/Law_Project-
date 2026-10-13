using System;

namespace MVCCaseManagement.Models
{
    public class CaseNoting
    {
        public int NotingID { get; set; }
        public string CaseType { get; set; } = string.Empty; // 'MVC', 'LABOUR', 'ARISING'
        public int CaseID { get; set; }
        public string NotingText { get; set; } = string.Empty;
        public string CreatedByUsername { get; set; } = string.Empty;
        public string? CreatedByName { get; set; }
        public string? CreatedByRole { get; set; }
        public string? CreatedByDivision { get; set; }
        public DateTime CreatedDate { get; set; } = DateTime.Now;
        public bool IsActive { get; set; } = true;
    }

    public class AddCaseNotingDto
    {
        public string CaseType { get; set; } = "MVC";
        public int CaseID { get; set; }
        public string NotingText { get; set; } = string.Empty;
    }

    public class DeleteCaseNotingDto
    {
        public int NotingID { get; set; }
        public string? CaseType { get; set; }
        public int CaseID { get; set; }
    }

    public class CaseNotingViewModel
    {
        public string CaseType { get; set; } = "MVC";
        public int CaseID { get; set; }
        public string? CaseNumberTitle { get; set; }
        public List<CaseNoting> Notings { get; set; } = new List<CaseNoting>();
        public bool CanAddNoting { get; set; } = true;
    }
}
