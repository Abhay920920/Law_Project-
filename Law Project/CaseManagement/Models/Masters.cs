using System.ComponentModel.DataAnnotations;

namespace MVCCaseManagement.Models
{
    public class Division
    {
        public int DivisionID { get; set; }
        
        [Required, StringLength(10)]
        public string DivisionCode { get; set; } = string.Empty;
        
        [Required, StringLength(100)]
        public string DivisionNameEnglish { get; set; } = string.Empty;
        
        [StringLength(100)]
        public string? DivisionNameKannada { get; set; }
        
        [StringLength(200)]
        public string? Location { get; set; }
        public bool IsActive { get; set; } = true;
    }

    public class Depot
    {
        public int DepotID { get; set; }
        
        [Required, StringLength(10)]
        public string DepotCode { get; set; } = string.Empty;
        
        [Required, StringLength(10)]
        public string DivisionCode { get; set; } = string.Empty;
        
        [Required, StringLength(100)]
        public string DepotNameEnglish { get; set; } = string.Empty;
        
        [StringLength(100)]
        public string? DepotNameKannada { get; set; }
        public bool IsActive { get; set; } = true;
    }

    public class MACT
    {
        public int MACTID { get; set; }
        
        [Required, StringLength(20)]
        public string MACTCode { get; set; } = string.Empty;
        
        [Required, StringLength(200)]
        public string MACTName { get; set; } = string.Empty;
        
        [StringLength(200)]
        public string? Location { get; set; }
        public bool IsActive { get; set; } = true;
    }

    public class CaseStatus
    {
        public int StatusID { get; set; }
        
        [Required, StringLength(10)]
        public string StatusCode { get; set; } = string.Empty;
        
        [Required, StringLength(50)]
        public string StatusName { get; set; } = string.Empty;
        public int DisplayOrder { get; set; }
        public bool IsActive { get; set; } = true;
    }

    public class Role
    {
        public int RoleID { get; set; }
        
        [Required, StringLength(50)]
        public string RoleName { get; set; } = string.Empty;
        
        [StringLength(200)]
        public string? Description { get; set; }
        public bool IsActive { get; set; } = true;
    }

    public class Advocate
    {
        public int AdvocateID { get; set; }
        
        [Required, StringLength(200)]
        public string AdvocateName { get; set; } = string.Empty;
        
        [StringLength(100)]
        public string? Bench { get; set; }
        public bool IsActive { get; set; } = true;
    }

    public class HighCourtAdvocate
    {
        public int AdvocateID { get; set; }
        
        [Required, StringLength(200)]
        public string AdvocateName { get; set; } = string.Empty;
        
        [StringLength(50)]
        public string? Bench { get; set; } // DWR, BNG, GLB, etc.
        public bool IsActive { get; set; } = true;
    }

    public class GratuityCourt
    {
        public int CourtID { get; set; }
        
        [Required, StringLength(200)]
        public string CourtName { get; set; } = string.Empty;
        
        [StringLength(200)]
        public string? Location { get; set; }
        public bool IsActive { get; set; } = true;
    }

    public class GratuityAdvocate
    {
        public int AdvocateID { get; set; }
        
        [Required, StringLength(200)]
        public string AdvocateName { get; set; } = string.Empty;
        
        [StringLength(100)]
        public string? Specialization { get; set; }
        public bool IsActive { get; set; } = true;
    }
}
