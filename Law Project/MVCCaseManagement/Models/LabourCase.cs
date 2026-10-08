using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Http;

namespace MVCCaseManagement.Models
{
    public class LabourCase
    {
        public int CaseID { get; set; }
        public int? ServiceID { get; set; } // For new Service Matters table
        
        public int DivisionID { get; set; }
        public string? DivisionName { get; set; } // Populated from Join
        
        // --- Basic Details ---
        
        [StringLength(50)]
        public string CaseType { get; set; } = string.Empty; // KID, ID, REF, etc.
        
        
        [StringLength(50)]
        public string CaseStatus { get; set; } = "Pending"; // Pending, Disposed, DNP, Ex-parte
        
        // --- Court & Case Identification ---
        
        [StringLength(100)]
        public string CaseNumber { get; set; } = string.Empty;
        
        
        public int? CaseYear { get; set; } = DateTime.Now.Year;
        
        public int? CourtID { get; set; }
        public string? CourtName { get; set; } // Populated from Master

        // --- e-Courts Live Gateway Integration Fields ---
        [StringLength(16)]
        public string? CNRNumber { get; set; }

        [StringLength(50)]
        public string? EstCode { get; set; }

        [StringLength(20)]
        public string? CaseTypeCode { get; set; }
        
        [StringLength(200)]
        public string? OtherCourtDetails { get; set; }

        public DateTime? LastNapixSyncAt { get; set; }
        public string? LastNapixSyncStatus { get; set; }
        public string? LastNapixSyncError { get; set; }
        public int NapixSyncAttemptCount { get; set; }
        public string? NapixDataHash { get; set; }
        public string? PendDispStatus { get; set; }
        public string? EstName { get; set; }
        public string? ECourtsStage { get; set; }
        public string? ECourtsCourtNo { get; set; }
        public string? ECourtsJudge { get; set; }
        
        // --- Customer / Employee / Petitioner Details ---
        
        [StringLength(200)]
        public string PetitionerName { get; set; } = string.Empty;
        
        [StringLength(50)]
        public string? EmployeeNo { get; set; }
        
        [StringLength(50)]
        public string? PFNumber { get; set; }
        
        [StringLength(100)]
        public string? Designation { get; set; }
        
        [StringLength(50)]
        public string? WorkingStatus { get; set; } // Regular, Trainee, Retired, VRS, Death
        [StringLength(200)]
        public string? LegalRepresentativeName { get; set; }
        [StringLength(100)]
        public string? LRRelationship { get; set; }

        public bool IsWorkman { get; set; }
        public string? IsWorkmanRemark { get; set; }
        
        // --- Nature Of Case ---
        [StringLength(100)]
        public string? NatureOfCase { get; set; } // Dismissal, Removal, etc.
        
        public string? NatureOfMisconduct { get; set; }
        
        // --- Legal Details ---
        [StringLength(100)]
        public string? EntrustmentNo { get; set; }
        
        [DataType(DataType.Date)]
        public DateTime? EntrustmentDate { get; set; }
        
        public int? AdvocateID { get; set; }
        [StringLength(200)]
        public string? AdvocateName { get; set; }
        
        public bool IsDoubleClaim { get; set; }
        public string? DoubleClaimRemarks { get; set; }
        
        // --- Progress ---
        [StringLength(200)]
        public string? CurrentStage { get; set; }
        
        // --- Serial Application Case Details ---
        [StringLength(100)]
        public string? SerialApp_CaseNumber { get; set; }
        [StringLength(200)]
        public string? SerialApp_CurrentStage { get; set; }
        
        public bool IsDocumentSent { get; set; }
        [StringLength(50)]
        public string? DocumentSent_OutwardNo { get; set; }
        public DateTime? DocumentSent_OutwardDate { get; set; }

        public bool IsObjectionFiled { get; set; }
        [StringLength(50)]
        public string? ObjectionFiled_OutwardNo { get; set; }
        public DateTime? ObjectionFiled_OutwardDate { get; set; }

        public bool IsEnquiryOfficerEvidence { get; set; }
        public bool IsReporterEvidence { get; set; }
        public bool IsOtherEvidence { get; set; }
        
        // --- Limitation & Delay Details ---
        public bool? IsFiledWithinLimitation { get; set; }
        public string? LimitationRemark { get; set; }
        public bool? IsDelayCondoned { get; set; }
        public string? DelayCondonationRemark { get; set; }

        // public List<LabourCaseEvidence> EvidenceList { get; set; } = new List<LabourCaseEvidence>();

        public List<LabourCaseHistory> HistoryList { get; set; } = new List<LabourCaseHistory>();
        public List<LabourReinstatementDocument> ReinstatedDocuments { get; set; } = new List<LabourReinstatementDocument>();
        public List<EnclosedDocument> EnclosedDocuments { get; set; } = new();

        // Legacy / Denormalized fields for quick access (updated from latest History)
        public string? CaseHistory { get; set; }        
        public DateTime? NextHearingDate { get; set; }

        public bool IsCOApprovalRequired { get; set; }
        [StringLength(50)]
        public string? COApproval_OutwardNo { get; set; }
        public DateTime? COApproval_OutwardDate { get; set; }
        
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
        
        // --- Lok Adalat ---
        public bool LokAdalat_COApprovalRequired { get; set; }
        [StringLength(100)]
        public string? LokAdalat_OutwardNo { get; set; }
        [DataType(DataType.Date)]
        public DateTime? LokAdalat_Date { get; set; }
        public string? LokAdalatDocumentPath { get; set; }
        [NotMapped]
        public IFormFile? LokAdalatDocumentFile { get; set; }
        
        // --- Post Disposal (Against) ---
        [StringLength(50)]
        public string? Against_CaseCategory { get; set; }
        public string? Against_BriefFacts { get; set; }
        [StringLength(100)]
        public string? Against_PunishmentImposed { get; set; }
        [StringLength(100)]
        public string? Against_PunishmentNo { get; set; }
        [DataType(DataType.Date)]
        public DateTime? Against_PunishmentDate { get; set; }
        
        public string? Against_PunishmentCopyPath { get; set; }
        [NotMapped]
        public IFormFile? Against_PunishmentCopyFile { get; set; }

        public string? ClaimPetitionPath { get; set; }
        [NotMapped]
        public IFormFile? ClaimPetitionFile { get; set; }
        
        public string? ClaimFiledOn { get; set; }
        public string? DelayInFiling { get; set; }
        public string? ClaimDetails { get; set; }
        
        // --- DE Details ---
        public bool DE_HistorySheet { get; set; }
        public string? DE_HistorySheetPath { get; set; } // Path to uploaded file
        [System.ComponentModel.DataAnnotations.Schema.NotMapped]
        public IFormFile? HistorySheetFile { get; set; } // For Upload

        public string? TerminalBenefitsPaid { get; set; }
        public string? SerialApplicationDetails { get; set; }
        public bool IsRepeatDismissal { get; set; }
        public string? RepeatDismissalRemark { get; set; }
        public bool HasAppealDetails { get; set; }
        public string? AppealDetailsRemark { get; set; }

        public bool DE_ObjectionsFiled { get; set; }
        public string? DE_ObjectionsRemark { get; set; }
        public bool DE_DocumentsMarked { get; set; }
        public string? DE_DocumentsRemark { get; set; }
        [StringLength(50)]
        public string? DE_Order { get; set; }
        public string? ChargesStatus { get; set; }
        public string? DE_EO_Name { get; set; }
        public string? DE_EO_Designation { get; set; }
        public bool DE_EO_IsBasedOnDocuments { get; set; }
        public string? DE_EO_BasedOnDocsRemark { get; set; }
        public string? DE_Reporter_Name { get; set; }
        public string? DE_Reporter_Designation { get; set; }
        public bool DE_Reporter_IsBasedOnDocuments { get; set; }
        public string? DE_Reporter_BasedOnDocsRemark { get; set; }
        public string? DE_Other_Name { get; set; }
        public string? DE_Other_Designation { get; set; }
        public bool DE_Other_IsBasedOnDocuments { get; set; }
        public string? DE_Other_BasedOnDocsRemark { get; set; }
        
        
        // --- Certified Copy ---
        [DataType(DataType.Date)]
        public DateTime? CC_CaseDisposedDate { get; set; }
        [DataType(DataType.Date)]
        public DateTime? CC_PublicationDate { get; set; }
        [DataType(DataType.Date)]
        public DateTime? CC_AppliedDate { get; set; }
        [DataType(DataType.Date)]
        public DateTime? CC_IssuedDate { get; set; }
        [DataType(DataType.Date)]
        public DateTime? CC_DeliveredDate { get; set; }
        [DataType(DataType.Date)]
        public DateTime? CC_ReceivedDate { get; set; }
        public string? CC_Remarks { get; set; }
        
        // --- Opinions ---
        public string? AwardDetails { get; set; }
        public string? Opinion_Advocate { get; set; }
        public string? Opinion_LO { get; set; }
        public string? Opinion_DC { get; set; }
        public string? Opinion_CLO { get; set; }

        // ==========================================
        // PER-ROLE ACTION FIELDS (LO, Dy CLO, CLO, MD)
        // Each role records: Action Taken, Approval Date, Opinion
        // ==========================================

        // LO Action
        [StringLength(100)]
        public string? ActionTaken_LO { get; set; }
        [DataType(DataType.Date)]
        public DateTime? ApprovalDate_LO { get; set; }
        // Opinion_LO already defined above

        // Dy CLO Action
        [StringLength(100)]
        public string? ActionTaken_DyCLO { get; set; }
        [DataType(DataType.Date)]
        public DateTime? ApprovalDate_DyCLO { get; set; }
        public string? Opinion_DyCLO { get; set; }

        // CLO Action (separate dedicated field)
        [StringLength(100)]
        public string? ActionTaken_CLO { get; set; }
        [DataType(DataType.Date)]
        public DateTime? ApprovalDate_CLO { get; set; }
        // Opinion_CLO already defined above

        // MD Action
        [StringLength(100)]
        public string? ActionTaken_MD { get; set; }
        [DataType(DataType.Date)]
        public DateTime? ApprovalDate_MD { get; set; }
        public string? Opinion_MD { get; set; }
        
        // --- CO Communication ---
        public bool? SentToCO { get; set; }
        [StringLength(100)]
        public string? CO_OutwardNo { get; set; }
        [DataType(DataType.Date)]
        public DateTime? CO_OutwardDate { get; set; }
        public string? CO_Remarks { get; set; }
        
        // --- CENTRAL OFFICE WORK (NEW SECTIONS) ---
        
        // Section 1: Feasibility & Initial Action
        public bool? CO_FeasibilityReceived { get; set; }
        public DateTime? CO_FeasibilityDate { get; set; }
        [StringLength(100)]
        public string? CO_ActionTaken { get; set; } // Pending for Decision, Pending before Competent Authority, Appeal, Close
        [StringLength(50)]
        public string? CO_ApprovalOutwardNo { get; set; }
        public DateTime? CO_ApprovalDate { get; set; }
        public string? CO_ClosedDocumentPath { get; set; }
        [NotMapped]
        public IFormFile? ClosedDocumentFile { get; set; }

        // Section 2: Appeal Filed by Corporation (Writ Petition)
        [StringLength(100)]
        public string? CO_WP_CaseStatus_Option { get; set; } // Pending for Filing / Assigned Case No.
        [StringLength(16)]
        public string? CO_WP_CNRNumber { get; set; }
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
        public string? CO_WP_StayNature { get; set; } // Interim Order / Section 17(B)
        public DateTime? CO_WP_StayDate { get; set; }
        
        // Stay Order Upload
        public string? CO_WP_StayOrderPath { get; set; }
        public string? CO_WP_StayRemark { get; set; }
        [NotMapped]
        public IFormFile? StayOrderFile { get; set; }

        // Section 3: Workman Reinstatement
        public bool CO_IsWorkmanReinstated { get; set; }
        [StringLength(100)]
        public string? CO_ReinstatedSubjectToWP { get; set; } // Interim Order / Section 17(B)
        
        // Reinstatement Stay Details (if Interim Order)
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
        public string? CO_WP_Status { get; set; } // Pending / Disposed
        
        [StringLength(50)]
        public string? CO_WP_Outcome { get; set; } // Favor / Against
        public string? CO_WP_OutcomeRemark { get; set; }
        [StringLength(50)]
        public string? CO_WP_OutcomeOutwardNo { get; set; }
        public DateTime? CO_WP_OutcomeOutwardDate { get; set; }
        [StringLength(50)]
        public string? CO_WP_ActionTaken { get; set; } // Close / Appeal

        // Section 2a: Writ Appeal (WA) - New Section
        [StringLength(100)]
        public string? CO_WA_CaseStatus_Option { get; set; }
        [StringLength(16)]
        public string? CO_WA_CNRNumber { get; set; }
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

        // WA Status
        [StringLength(50)]
        public string? CO_WA_Status { get; set; } // Pending / Disposed
        [StringLength(50)]
        public string? CO_WA_Outcome { get; set; } // Favor / Against
        public string? CO_WA_OutcomeRemark { get; set; }
        [StringLength(50)]
        public string? CO_WA_OutcomeOutwardNo { get; set; }
        public DateTime? CO_WA_OutcomeOutwardDate { get; set; }
        [StringLength(50)]
        public string? CO_WA_ActionTaken { get; set; }

        // Section 5: Connected / Cross Cases (Handled via List)
        public List<LabourConnectedCase> ConnectedCases { get; set; } = new List<LabourConnectedCase>();
        public List<LabourEPViewModel> ConnectedEPs { get; set; } = new List<LabourEPViewModel>(); // New field for EPs
        [StringLength(50)]
        public string? CO_OverallCaseStatus { get; set; } // Pending / Disposal

        // Section 6: Disposal Outcome
        [StringLength(100)]
        public string? CO_Disposal_Nature { get; set; }
        public bool? CO_Disposal_CommSentToDivision { get; set; }
        [StringLength(100)]
        public string? CO_Disposal_OutwardNo { get; set; }
        public DateTime? CO_Disposal_Date { get; set; }
        [StringLength(100)]
        public string? CO_Disposal_Decision { get; set; } // Approved / Appeal
        [StringLength(100)]
        public string? CO_Disposal_ApprovalOutwardNo { get; set; }
        public DateTime? CO_Disposal_ApprovalDate { get; set; }

        // Section 7: Further Appeal (After Disposal)
        [StringLength(100)]
        public string? CO_FurtherAppeal_Status_Option { get; set; } // Pending for Filing / Assigned Case No.
        [StringLength(100)]
        public string? CO_FurtherAppeal_CaseNumber { get; set; }
        public int? CO_FurtherAppeal_Year { get; set; }
        [StringLength(100)]
        public string? CO_FurtherAppeal_EntrustmentNo { get; set; }
        public DateTime? CO_FurtherAppeal_EntrustmentDate { get; set; }
        [StringLength(200)]
        public string? CO_FurtherAppeal_AdvocateName { get; set; }
        [StringLength(50)]
        public string? CO_FurtherAppeal_CaseStatus { get; set; } // PENDING / DISPOSED
        [StringLength(100)]
        public string? CO_FurtherAppeal_DisposalOutwardNo { get; set; }
        public DateTime? CO_FurtherAppeal_DisposalDate { get; set; }

        // Section 8: Claimant / Petitioner Appeal (Against Corporation)
        [StringLength(200)]
        public string? CO_Claimant_DivisionName { get; set; }
        [StringLength(100)]
        public string? CO_Claimant_ArisingOutOf { get; set; } // KID / ID / REF / Application / Others
        [StringLength(200)]
        public string? CO_Claimant_Court { get; set; }
        [StringLength(16)]
        public string? CO_Claimant_CNRNumber { get; set; }
        [StringLength(100)]
        public string? CO_Claimant_CaseNumber { get; set; }
        public int? CO_Claimant_CaseYear { get; set; }
        [StringLength(100)]
        public string? CO_Claimant_HighCourtBench { get; set; }
        [StringLength(100)]
        public string? CO_Claimant_OriginalCaseStatus { get; set; } // Pending for Approval, Appeal Filed, Pending before Division
        public bool? CO_Claimant_IsConnected { get; set; }
        [System.ComponentModel.DataAnnotations.Schema.NotMapped]
        public List<LabourConnectedCase> ClaimantConnectedCases { get; set; } = new List<LabourConnectedCase>();
        [StringLength(100)]
        public string? CO_Claimant_EntrustmentNo { get; set; }

        public DateTime? CO_Claimant_EntrustmentDate { get; set; }
        [StringLength(200)]
        public string? CO_Claimant_AdvocateName { get; set; }
        [StringLength(50)]
        public string? CO_Claimant_CaseStatus { get; set; } // Pending / Disposal

        public string? CO_Claimant_PetitionCopyPath { get; set; }
        [NotMapped]
        public IFormFile? CO_Claimant_PetitionCopyFile { get; set; }

        // Section 9: Service Matters / Direct Writ Petitions
        [StringLength(200)]
        public string? CO_Service_Division { get; set; }
        [StringLength(100)]
        public string? CO_Service_WPNumber { get; set; }
        public int? CO_Service_WPYear { get; set; }
        [StringLength(200)]
        public string? CO_Service_PetitionerName { get; set; }
        [StringLength(100)]
        public string? CO_Service_CaseNature { get; set; }
        public string? CO_Service_Prayer { get; set; }
        public string? CO_Service_PetitionCopyPath { get; set; }
        public bool? CO_Service_IsEmployee { get; set; }
        [System.ComponentModel.DataAnnotations.Schema.NotMapped]
        public IFormFile? CO_Service_PetitionCopyFile { get; set; }
        public bool? CO_Service_StayGranted { get; set; }
        public bool? CO_Service_StayVacateFiled { get; set; }
        [StringLength(50)]
        public string? CO_Service_Status { get; set; } // Pending / Disposed
        public DateTime? CO_Service_DisposalDate { get; set; }
        [StringLength(100)]
        public string? CO_Service_ActionTaken { get; set; } // Appeal / Close
        public string? CO_Service_ApprovalSentDetails { get; set; }
        [StringLength(100)]
        public string? CO_Service_OutwardNo { get; set; }
        public DateTime? CO_Service_OutwardDate { get; set; }
        [StringLength(100)]
        public string? CO_Service_AppealFiledBefore { get; set; } // Division Bench / Supreme Court
        [StringLength(50)]
        public string? CO_Service_AppealType { get; set; } // WA / SLP
        public DateTime? CO_Service_AppealEntrustmentDate { get; set; }
        [StringLength(200)]
        public string? CO_Service_AppealAdvocate { get; set; }
        [StringLength(50)]
        public string? CO_Service_AppealStatus { get; set; } // Pending / Disposed
        public bool? CO_Service_StayCompliance { get; set; }
        [StringLength(100)]
        public string? CO_Service_ApprovalOutwardNo { get; set; }
        public DateTime? CO_Service_ApprovalDate { get; set; }
        public string? CO_Service_ApprovalCopyPath { get; set; }
        [System.ComponentModel.DataAnnotations.Schema.NotMapped]
        public IFormFile? CO_Service_ApprovalCopyFile { get; set; }
        
        [StringLength(100)]
        public string? CO_Service_EntrustmentNo { get; set; }
        
        [DataType(DataType.Date)]
        public DateTime? CO_Service_EntrustmentDate { get; set; }

        // --- Judgment Copy ---
        public string? JudgmentCopyPath { get; set; }
        [System.ComponentModel.DataAnnotations.Schema.NotMapped]
        public IFormFile? JudgmentCopyFile { get; set; }
        public string? JudgmentCopyPath2 { get; set; }
        [System.ComponentModel.DataAnnotations.Schema.NotMapped]
        public IFormFile? JudgmentCopyFile2 { get; set; }

        public string? CO_WP_JudgmentCopyPath { get; set; }
        [System.ComponentModel.DataAnnotations.Schema.NotMapped]
        public IFormFile? CO_WP_JudgmentCopyFile { get; set; }

        // --- Arising Application ---
        public bool IsArisingApplication { get; set; }
        [StringLength(100)]
        public string? Arising_OriginalCaseNumber { get; set; }
        public int? Arising_OriginalCaseYear { get; set; }
        [StringLength(200)]
        public string? Arising_OriginalCourt { get; set; }
        public string? Arising_CurrentStatus { get; set; }
        public string? Arising_ApplicationStatus { get; set; }
        
        // Meta
        public DateTime CreatedDate { get; set; }
        public int? TransferredFromDivisionID { get; set; }
        public DateTime? TransferDate { get; set; }
        public bool IsTransferViewed { get; set; }
        public bool IsReinstatementViewed { get; set; }
        public bool IsViewedByCO { get; set; }
        [NotMapped]
        public string? TransferredFromDivisionName { get; set; }

        public int? CreatedBy { get; set; }
        public DateTime? ModifiedDate { get; set; }
        public int? ModifiedBy { get; set; }

        // Stay Compliance (Division User)
        public DateTime? CO_StayComplianceDate { get; set; }
        public string? CO_StayComplianceRemark { get; set; }
        public string? CO_StayComplianceFilePath { get; set; }
        // Section: Claimant SC Appeal
        public bool IsClaimantSCPending { get; set; }
        [StringLength(50)]
        public string? ClaimantSCDiaryNumber { get; set; }
        public int? ClaimantSCYear { get; set; }
        [StringLength(50)]
        public string? ClaimantSCNumber { get; set; }
        public int? ClaimantSLPYear { get; set; }
        [StringLength(50)]
        public string? ClaimantSCFiledBy { get; set; }
        [StringLength(50)]
        public string? ClaimantSCEntrustmentNo { get; set; }
        public DateTime? ClaimantSCEntrustmentDate { get; set; }
        [StringLength(200)]
        public string? ClaimantSCAdvocate { get; set; }
        [StringLength(50)]
        public string? ClaimantSCStatus { get; set; }
        [StringLength(100)]
        public string? ClaimantSCOutcome { get; set; }
        [StringLength(100)]
        public string? ClaimantSCActionTaken { get; set; }
        [StringLength(50)]
        public string? ClaimantSCClosureNo { get; set; }
        public DateTime? ClaimantSCClosureDate { get; set; }

        [NotMapped]
        public IFormFile? StayComplianceFile { get; set; }

        [NotMapped]
        public string? Arising_CO_WP_CaseNumber { get; set; }
        [NotMapped]
        public int? Arising_CO_WP_Year { get; set; }
        [NotMapped]
        public string? Arising_CO_WP_Status { get; set; }
        [NotMapped]
        public string? Arising_CO_WA_CaseNumber { get; set; }
        [NotMapped]
        public int? Arising_CO_WA_Year { get; set; }
        [NotMapped]
        public string? Arising_CO_WA_Status { get; set; }
    }
    
    public class LabourDashboardStats
    {
        public int TotalCases { get; set; }
        public int PendingCases { get; set; }
        public int DisposedCases { get; set; }
        public int AgainstCases { get; set; }
        public int SentToCOCount { get; set; }
        public int PendingCompetentAuthorityCount { get; set; }
        public int PendingCLOLOCount { get; set; }
        public int HighCourtAppealCount { get; set; }
        public int ArisingApplicationCount { get; set; }
        public int CasesThisMonth { get; set; }
        public int NoActionTakenCount { get; set; }
        public int ReinstatementCount { get; set; }
        public int SLPPendingCount { get; set; }
        public int WritAppealsClaimantCount { get; set; }
        public int WritPetitionClaimantCount { get; set; }
        public int ServiceMatterCount { get; set; }
        public decimal FinancialExposure { get; set; }
    }
    
    public class LabourCourt
    {
        public int CourtID { get; set; }
        public string CourtName { get; set; } = string.Empty;
        public string? Location { get; set; }
        public bool IsActive { get; set; }
    }

    public class LabourAdvocate
    {
        public int AdvocateID { get; set; }
        public string AdvocateName { get; set; } = string.Empty;
        public bool IsActive { get; set; }
    }

    public class LabourConnectedCase
    {
        public int ConnectedCaseID { get; set; }
        public int CaseID { get; set; }
        [StringLength(500)]
        public string? CaseDetails { get; set; }
        [StringLength(100)]
        public string? CaseType { get; set; }
        [StringLength(100)]
        public string? FiledBy { get; set; } // Corporation / Claimant
        [StringLength(200)]
        public string? CurrentStatus { get; set; }
    }

    public class LabourReinstatementDocument
    {
        public int DocumentID { get; set; }
        public int CaseID { get; set; }
        public string DocumentName { get; set; } = string.Empty;
        public string DocumentPath { get; set; } = string.Empty;
        public DateTime UploadedDate { get; set; }
    }
}
