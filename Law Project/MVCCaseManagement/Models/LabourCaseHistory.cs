using System;
using System.ComponentModel.DataAnnotations;

namespace MVCCaseManagement.Models
{
    public class LabourCaseHistory
    {
        public int HistoryID { get; set; }
        public int CaseID { get; set; }

        [StringLength(200)]
        public string? CurrentStage { get; set; }

        [DataType(DataType.Date)]
        public DateTime? NextHearingDate { get; set; }

        public string? Remarks { get; set; }

        public DateTime CreatedDate { get; set; } = DateTime.Now;
    }
}
