using System;
using System.ComponentModel.DataAnnotations;

namespace MVCCaseManagement.Models
{
    public class LabourCaseEvidence
    {
        public int EvidenceID { get; set; }
        public int CaseID { get; set; }

        [StringLength(100)]
        public string? EvidenceType { get; set; } // Enquiry Officer, Reporter, Other
        
        [StringLength(200)]
        public string? OtherDetails { get; set; }
        
        // Optional: Could add DateFiled if "follow up" implies timeline, but user only mentioned rows.
        // Keeping it simple based on previous fields.
    }
}
