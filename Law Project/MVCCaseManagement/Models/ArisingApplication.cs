using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MVCCaseManagement.Models
{
    /// <summary>
    /// Represents an Arising Application — a separate entity linked to a parent Labour Case 
    /// via CaseNumber + Year + Court. Stored in LABOUR_ARISING_APPLICATIONS table.
    /// </summary>
    public class ArisingApplication
    {
        public int ArisingID { get; set; }

        // --- Parent Case Connection ---
        // FK to LABOUR_CASES.CaseID (resolved via search)
        public int? ParentCaseID { get; set; }

        // Denormalized parent identifiers for display/search
        [StringLength(100)]
        public string? Parent_CaseNumber { get; set; }
        public int? Parent_CaseYear { get; set; }
        public int? Parent_CourtID { get; set; }
        [StringLength(200)]
        public string? Parent_CourtName { get; set; }  // Populated from join
        [StringLength(50)]
        public string? Parent_CaseType { get; set; }
        [StringLength(50)]
        public string? Parent_CaseStatus { get; set; }
        [StringLength(200)]
        public string? Parent_PetitionerName { get; set; }

        // --- Division ---
        public int DivisionID { get; set; }
        [StringLength(200)]
        public string? DivisionName { get; set; }  // Populated from join

        // --- Case Identification ---
        [StringLength(100)]
        public string CaseNumber { get; set; } = string.Empty;
        public int? CaseYear { get; set; } = DateTime.Now.Year;
        public int? CourtID { get; set; }
        [StringLength(200)]
        public string? CourtName { get; set; }  // Populated from join
        [StringLength(200)]
        public string? OtherCourtDetails { get; set; }

        // --- e-Courts Live Gateway Integration Fields ---
        [StringLength(16)]
        public string? CNRNumber { get; set; }

        [StringLength(50)]
        public string? EstCode { get; set; }

        [StringLength(20)]
        public string? CaseTypeCode { get; set; }

        // --- Case Type & Status ---
        [StringLength(50)]
        public string CaseType { get; set; } = "Arising Application";
        [StringLength(50)]
        public string CaseStatus { get; set; } = "Pending";

        // --- Petitioner ---
        [StringLength(200)]
        public string? PetitionerName { get; set; }

        // --- Petitioner Details ---
        [StringLength(50)]
        public string? EmployeeNo { get; set; }
        [StringLength(50)]
        public string? PFNumber { get; set; }
        [StringLength(100)]
        public string? Designation { get; set; }
        [StringLength(50)]
        public string? WorkingStatus { get; set; }
        [StringLength(200)]
        public string? LegalRepresentativeName { get; set; }
        [StringLength(100)]
        public string? LRRelationship { get; set; }
        public bool IsWorkman { get; set; }
        public string? IsWorkmanRemark { get; set; }

        // --- Case Details ---
        [StringLength(100)]
        public string? NatureOfCase { get; set; }
        public string? NatureOfMisconduct { get; set; }
        
        public string? ClaimFiledOn { get; set; }
        public string? DelayInFiling { get; set; }
        public string? ClaimDetails { get; set; }

        // --- Entrustment & Advocate ---
        [StringLength(100)]
        public string? EntrustmentNo { get; set; }
        [DataType(DataType.Date)]
        public DateTime? EntrustmentDate { get; set; }
        public int? AdvocateID { get; set; }
        [StringLength(200)]
        public string? AdvocateName { get; set; }

        // --- Progress: Document Sent ---
        public bool IsDocumentSent { get; set; }
        [StringLength(50)]
        public string? DocumentSent_OutwardNo { get; set; }
        [DataType(DataType.Date)]
        public DateTime? DocumentSent_OutwardDate { get; set; }

        // --- Progress: Objection Filed ---
        public bool IsObjectionFiled { get; set; }
        [StringLength(50)]
        public string? ObjectionFiled_OutwardNo { get; set; }
        [DataType(DataType.Date)]
        public DateTime? ObjectionFiled_OutwardDate { get; set; }

        // --- Progress: Evidence Filed ---
        public bool IsEvidenceFiled { get; set; }

        // --- Current Stage & Hearing ---
        [StringLength(200)]
        public string? CurrentStage { get; set; }
        [DataType(DataType.Date)]
        public DateTime? NextHearingDate { get; set; }

        // --- Disposal Details ---
        [StringLength(50)]
        public string? DisposalMode { get; set; }

        [DataType(DataType.Date)]
        public DateTime? DisposalDate { get; set; }

        [StringLength(50)]
        public string? DisposalResult { get; set; }

        public string? FavorRemark { get; set; }
        [DataType(DataType.Date)]
        public DateTime? FavorOutwardDate { get; set; }

        [StringLength(500)]
        public string? JudgmentCopyPath { get; set; }
        [NotMapped]
        public IFormFile? JudgmentCopyFile { get; set; }

        [StringLength(500)]
        public string? ClaimPetitionPath { get; set; }
        [NotMapped]
        public IFormFile? ClaimPetitionFile { get; set; }

        // --- Lok Adalat ---
        public bool LokAdalat_COApprovalRequired { get; set; }
        [StringLength(100)]
        public string? LokAdalat_OutwardNo { get; set; }
        [DataType(DataType.Date)]
        public DateTime? LokAdalat_Date { get; set; }
        [StringLength(500)]
        public string? LokAdalatDocumentPath { get; set; }
        [NotMapped]
        public IFormFile? LokAdalatDocumentFile { get; set; }

        // --- Departmental Enquiry (DE) ---
        public bool DE_HistorySheet { get; set; }
        [StringLength(500)]
        public string? DE_HistorySheetPath { get; set; }
        [NotMapped]
        public IFormFile? HistorySheetFile { get; set; }

        public bool DE_ObjectionsFiled { get; set; }
        public string? DE_ObjectionsRemark { get; set; }

        public bool DE_DocumentsMarked { get; set; }
        public string? DE_DocumentsRemark { get; set; }

        [StringLength(50)]
        public string? DE_Order { get; set; }

        public bool DE_EO_IsBasedOnDocuments { get; set; }
        [StringLength(200)]
        public string? DE_EO_Name { get; set; }
        [StringLength(100)]
        public string? DE_EO_Designation { get; set; }

        public bool DE_Reporter_IsBasedOnDocuments { get; set; }
        [StringLength(200)]
        public string? DE_Reporter_Name { get; set; }
        [StringLength(100)]
        public string? DE_Reporter_Designation { get; set; }

        public bool DE_Other_IsBasedOnDocuments { get; set; }
        [StringLength(200)]
        public string? DE_Other_Name { get; set; }
        [StringLength(100)]
        public string? DE_Other_Designation { get; set; }

        // --- Certified Copy & Timelines ---
        [DataType(DataType.Date)]
        public DateTime? CC_CaseDisposedDate { get; set; }
        [DataType(DataType.Date)]
        public DateTime? CC_PublicationDate { get; set; }
        [DataType(DataType.Date)]
        public DateTime? CC_AppliedDate { get; set; }
        [DataType(DataType.Date)]
        public DateTime? CC_IssuedDate { get; set; }
        [DataType(DataType.Date)]
        public DateTime? CC_ReceivedDate { get; set; }
        public string? CC_Remarks { get; set; }

        // --- Awards & Opinions ---
        public string? AwardDetails { get; set; }
        public string? Opinion_Advocate { get; set; }
        public string? Opinion_LO { get; set; }
        public string? Opinion_DC { get; set; }
        public string? Opinion_CLO { get; set; }

        // --- Per-Role Legal Actions (LO, Dy CLO, CLO, MD) ---
        [StringLength(100)]
        public string? ActionTaken_LO { get; set; }
        [DataType(DataType.Date)]
        public DateTime? ApprovalDate_LO { get; set; }

        [StringLength(100)]
        public string? ActionTaken_DyCLO { get; set; }
        [DataType(DataType.Date)]
        public DateTime? ApprovalDate_DyCLO { get; set; }
        public string? Opinion_DyCLO { get; set; }

        [StringLength(100)]
        public string? ActionTaken_CLO { get; set; }
        [DataType(DataType.Date)]
        public DateTime? ApprovalDate_CLO { get; set; }

        [StringLength(100)]
        public string? ActionTaken_MD { get; set; }
        [DataType(DataType.Date)]
        public DateTime? ApprovalDate_MD { get; set; }
        public string? Opinion_MD { get; set; }

        public bool SentToCO { get; set; }
        [StringLength(100)]
        public string? CO_OutwardNo { get; set; }
        [DataType(DataType.Date)]
        public DateTime? CO_OutwardDate { get; set; }
        public string? CO_Remarks { get; set; }

        // Section 1: Feasibility & Initial Action
        public bool? CO_FeasibilityReceived { get; set; }
        public DateTime? CO_FeasibilityDate { get; set; }
        [StringLength(100)]
        public string? CO_ActionTaken { get; set; }
        [StringLength(50)]
        public string? CO_ApprovalOutwardNo { get; set; }
        public DateTime? CO_ApprovalDate { get; set; }
        public string? CO_ClosedDocumentPath { get; set; }
        [NotMapped]
        public IFormFile? ClosedDocumentFile { get; set; }

        // Section 2: Appeal Filed by Corporation (Writ Petition)
        [StringLength(16)]
        public string? CO_WP_CNRNumber { get; set; }
        [StringLength(100)]
        public string? CO_WP_CaseStatus_Option { get; set; }
        [StringLength(100)]
        public string? CO_WP_CaseNumber { get; set; }
        public int? CO_WP_Year { get; set; }
        [StringLength(100)]
        public string? CO_WP_HighCourtBench { get; set; }
        [StringLength(100)]
        public string? CO_WP_EntrustmentNo { get; set; }
        public DateTime? CO_WP_EntrustmentDate { get; set; }
        [StringLength(200)]
        public string? CO_WP_AdvocateName { get; set; }
        public bool? CO_WP_StayGranted { get; set; }
        [StringLength(100)]
        public string? CO_WP_StayApprovalNo { get; set; }
        [StringLength(100)]
        public string? CO_WP_StayNature { get; set; }
        public DateTime? CO_WP_StayDate { get; set; }
        public string? CO_WP_StayOrderPath { get; set; }
        public string? CO_WP_StayRemark { get; set; }
        [NotMapped]
        public IFormFile? StayOrderFile { get; set; }

        // Section 3: Workman Reinstatement
        public bool CO_IsWorkmanReinstated { get; set; }
        [StringLength(100)]
        public string? CO_ReinstatedSubjectToWP { get; set; }
        public bool? CO_Reinstatement_StayGranted { get; set; }
        [StringLength(100)]
        public string? CO_Reinstatement_StayApprovalNo { get; set; }
        [StringLength(100)]
        public string? CO_Reinstatement_StayNature { get; set; }
        public DateTime? CO_Reinstatement_StayDate { get; set; }
        public string? CO_Reinstatement_StayOrderPath { get; set; }
        public string? CO_Reinstatement_StayRemark { get; set; }
        [NotMapped]
        public IFormFile? ReinstatementStayOrderFile { get; set; }

        public bool? CO_ReinstatementApprovalIssued { get; set; }
        public DateTime? CO_ReinstatementApprovalDate { get; set; }
        [StringLength(100)]
        public string? CO_Reinstatement_ApprovalNo { get; set; }
        public string? CO_Reinstatement_ApprovalCopyPath { get; set; }
        [NotMapped]
        public IFormFile? ReinstatementApprovalFile { get; set; }

        // Section 4: Writ Petition Status
        [StringLength(50)]
        public string? CO_WP_Status { get; set; }
        [StringLength(50)]
        public string? CO_WP_Outcome { get; set; }
        public string? CO_WP_OutcomeRemark { get; set; }
        [StringLength(50)]
        public string? CO_WP_OutcomeOutwardNo { get; set; }
        public DateTime? CO_WP_OutcomeOutwardDate { get; set; }
        [StringLength(50)]
        public string? CO_WP_ActionTaken { get; set; }
        public string? CO_WP_JudgmentCopyPath { get; set; }
        [NotMapped]
        public IFormFile? CO_WP_JudgmentCopyFile { get; set; }

        // Section 2a: Writ Appeal (WA)
        [StringLength(16)]
        public string? CO_WA_CNRNumber { get; set; }
        [StringLength(100)]
        public string? CO_WA_CaseStatus_Option { get; set; }
        [StringLength(100)]
        public string? CO_WA_CaseNumber { get; set; }
        public int? CO_WA_Year { get; set; }
        [StringLength(100)]
        public string? CO_WA_HighCourtBench { get; set; }
        [StringLength(100)]
        public string? CO_WA_EntrustmentNo { get; set; }
        public DateTime? CO_WA_EntrustmentDate { get; set; }
        [StringLength(200)]
        public string? CO_WA_AdvocateName { get; set; }
        public bool? CO_WA_StayGranted { get; set; }
        [StringLength(100)]
        public string? CO_WA_StayApprovalNo { get; set; }
        [StringLength(100)]
        public string? CO_WA_StayNature { get; set; }
        public DateTime? CO_WA_StayDate { get; set; }
        public string? CO_WA_StayOrderPath { get; set; }
        public string? CO_WA_StayRemark { get; set; }
        [NotMapped]
        public IFormFile? WAStayOrderFile { get; set; }

        [StringLength(50)]
        public string? CO_WA_Status { get; set; }
        [StringLength(50)]
        public string? CO_WA_Outcome { get; set; }
        public string? CO_WA_OutcomeRemark { get; set; }
        [StringLength(50)]
        public string? CO_WA_OutcomeOutwardNo { get; set; }
        public DateTime? CO_WA_OutcomeOutwardDate { get; set; }
        [StringLength(50)]
        public string? CO_WA_ActionTaken { get; set; }

        // Section 6: Disposal Outcome
        [StringLength(100)]
        public string? CO_Disposal_Nature { get; set; }
        public bool? CO_Disposal_CommSentToDivision { get; set; }
        [StringLength(100)]
        public string? CO_Disposal_OutwardNo { get; set; }
        public DateTime? CO_Disposal_Date { get; set; }
        [StringLength(100)]
        public string? CO_Disposal_Decision { get; set; }
        [StringLength(100)]
        public string? CO_Disposal_ApprovalOutwardNo { get; set; }
        public DateTime? CO_Disposal_ApprovalDate { get; set; }

        // Section 7: Further Appeal
        [StringLength(100)]
        public string? CO_FurtherAppeal_Status_Option { get; set; }
        [StringLength(100)]
        public string? CO_FurtherAppeal_CaseNumber { get; set; }
        public int? CO_FurtherAppeal_Year { get; set; }
        [StringLength(100)]
        public string? CO_FurtherAppeal_EntrustmentNo { get; set; }
        public DateTime? CO_FurtherAppeal_EntrustmentDate { get; set; }
        [StringLength(200)]
        public string? CO_FurtherAppeal_AdvocateName { get; set; }
        [StringLength(50)]
        public string? CO_FurtherAppeal_CaseStatus { get; set; }
        [StringLength(100)]
        public string? CO_FurtherAppeal_DisposalOutwardNo { get; set; }
        public DateTime? CO_FurtherAppeal_DisposalDate { get; set; }

        // --- Remarks ---
        public string? Remarks { get; set; }

        // --- Award & Payment Tracking ---
        public decimal? AwardAmount { get; set; }
        public List<ArisingApplicationPayment> Payments { get; set; } = new List<ArisingApplicationPayment>();
        public List<EnclosedDocument> EnclosedDocuments { get; set; } = new List<EnclosedDocument>();

        // --- Metadata ---
        public DateTime CreatedDate { get; set; } = DateTime.Now;
        public int? CreatedBy { get; set; }
        public DateTime? ModifiedDate { get; set; }
        public int? ModifiedBy { get; set; }
    }
}
