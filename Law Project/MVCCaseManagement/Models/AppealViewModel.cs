using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace MVCCaseManagement.Models
{
    public class ConnectedAppeal
    {
        public int ConnectedID { get; set; }
        public string? ConnectedMVCNo { get; set; }
        public string? FiledBy { get; set; }
        public string? MFA_Number { get; set; }
        public string? Status { get; set; }
    }

    public class AppealViewModel
    {
        public int AppealID { get; set; }
        [Required]
        public int CaseID { get; set; } // Link to MVC Case
        
        [StringLength(50)]
        public string MVCNo { get; set; } = ""; // For Display
        public int MVCYear { get; set; } // For Display
        
        [StringLength(100)]
        public string DivisionName { get; set; } = ""; // For Display

        public int? MACTID { get; set; } // Link to MACT Table
        
        [StringLength(100)]
        public string MACTName { get; set; } = ""; // For Display

        public List<ConnectedAppeal> ConnectedCases { get; set; } = new List<ConnectedAppeal>();

        // ==========================================
        // 1. FEASIBILITY & INITIAL ACTION
        // ==========================================
        public bool FeasibilityReceived { get; set; }
        
        [DataType(DataType.Date)]
        public DateTime? FeasibilityReceiptDate { get; set; }
        
        [StringLength(100)]
        public string? InitialAction { get; set; } // Pending Decision, Competent Auth, Appeal, Close
        
        public string? InitialActionRemarks { get; set; }
        public string? Opinion_CLO { get; set; }
        
        [StringLength(50)]
        public string? ApprovalOutwardNo { get; set; }
        
        [DataType(DataType.Date)]
        public DateTime? ApprovalDate { get; set; }

        // ==========================================
        // PER-ROLE ACTION FIELDS (LO, Dy CLO, CLO, MD)
        // Each role records their own: Action Taken, Approval Date, Opinion
        // ==========================================

        // LO Action
        [StringLength(100)]
        public string? ActionTaken_LO { get; set; }
        [DataType(DataType.Date)]
        public DateTime? ApprovalDate_LO { get; set; }
        public string? Opinion_LO { get; set; }

        // Dy CLO Action
        [StringLength(100)]
        public string? ActionTaken_DyCLO { get; set; }
        [DataType(DataType.Date)]
        public DateTime? ApprovalDate_DyCLO { get; set; }
        public string? Opinion_DyCLO { get; set; }

        // CLO Action (separate from shared InitialAction/ApprovalDate)
        [StringLength(100)]
        public string? ActionTaken_CLO { get; set; }
        [DataType(DataType.Date)]
        public DateTime? ApprovalDate_CLO { get; set; }
        // Opinion_CLO already exists above

        // MD Action
        [StringLength(100)]
        public string? ActionTaken_MD { get; set; }
        [DataType(DataType.Date)]
        public DateTime? ApprovalDate_MD { get; set; }
        public string? Opinion_MD { get; set; }

        public IFormFile? ApprovalCopyFile { get; set; }
        public string? ApprovalCopyPath { get; set; }

        public IFormFile? InitialActionFile1 { get; set; }
        public string? InitialActionPath1 { get; set; }
        public IFormFile? InitialActionFile2 { get; set; }
        public string? InitialActionPath2 { get; set; }

        // ==========================================
        // 2. APPEAL FILED BY CORPORATION (MFA)
        // ==========================================
        public bool IsPendingForFiling { get; set; } // Checkbox to hide Number/Year

        [StringLength(50)]
        public string? CorpMFANumber { get; set; }
        public int? CorpMFAYear { get; set; }
        
        [StringLength(50)]
        public string? HighCourtBench { get; set; }

        public string? OtherHighCourtBench { get; set; }
        
        [StringLength(50)]
        public string? CorpMFAEntrustmentNo { get; set; }
        
        [DataType(DataType.Date)]
        public DateTime? CorpMFAEntrustmentDate { get; set; }
        
        [StringLength(200)]
        public string? CorpMFAAdvocate { get; set; }

        [StringLength(50)]
        public string? CorpMFACNRNumber { get; set; }

        [DataType(DataType.Date)]
        public DateTime? CorpMFANextHearingDate { get; set; }

        [StringLength(100)]
        public string? CorpMFAStage { get; set; }

        // ==========================================
        // 3. INTERIM ORDERS / STAY
        // ==========================================
        public bool StayGranted { get; set; }
        
        [StringLength(50)]
        public string? StayComplianceOutwardNo { get; set; }
        
        [DataType(DataType.Date)]
        public DateTime? StayComplianceDate { get; set; }

        public IFormFile? StayOrderFile1 { get; set; }
        public string? StayOrderPath1 { get; set; }
        public IFormFile? StayOrderFile2 { get; set; }
        public string? StayOrderPath2 { get; set; }

        // ==========================================
        // 4. STATUS OF MFA (CORPORATION)
        // ==========================================
        [StringLength(50)]
        public string? CorpMFAStatus { get; set; } // Pending, Disposed, DNP
        
        // If DNP (Dismissed for Non-Prosecution)
        public bool RestorationFiled { get; set; }
        
        [DataType(DataType.Date)]
        public DateTime? RestorationDate { get; set; }
        [StringLength(100)]
        public string? RestorationStatus { get; set; }

        public IFormFile? MFAJudgmentCopyFile { get; set; }
        public string? MFAJudgmentCopyPath { get; set; }

        // ==========================================
        // 5. OUTCOME / CASE NATURE
        // ==========================================
        [StringLength(50)]
        public string? CorpMFAOutcome { get; set; } // Favour, Against
        
        [StringLength(50)]
        public string? CorpMFAActionTaken { get; set; } // New property for Action Taken (Matter Closed, Appeal Proposed)

        public IFormFile? CorpMFAActionTakenFile { get; set; }
        public string? CorpMFAActionTakenPath { get; set; }

        [StringLength(50)]
        public string? ClosureOutwardNo { get; set; }
        
        [DataType(DataType.Date)]
        public DateTime? ClosureDate { get; set; }

        // ==========================================
        // 6. APPEAL BEFORE SUPREME COURT (CORP)
        // ==========================================
        [StringLength(50)]
        public string? CorpSCNumber { get; set; }
        public int? CorpSCYear { get; set; }
        
        [StringLength(50)]
        public string? CorpSCEntrustmentNo { get; set; }
        
        [DataType(DataType.Date)]
        public DateTime? CorpSCEntrustmentDate { get; set; }
        [StringLength(200)]
        public string? CorpSCAdvocate { get; set; }
        
        [StringLength(50)]
        public string? CorpSCStatus { get; set; }

        // ==========================================
        // 8. CLAIMANT MFA APPEAL
        // ==========================================


        // ==========================================
        // 8. APPEAL FILED BY CLAIMANT
        // ==========================================
        // Claimant Division and MVC Details
        public int? ClaimantDivisionID { get; set; }  // For Division dropdown
        
        [StringLength(50)]
        public string? ClaimantMVCNumber { get; set; }
        public int? ClaimantMVCYear { get; set; }
        
        [StringLength(100)]
        public string? ClaimantMVCCurrentStatus { get; set; }
        
        // Claimant MFA Details
        [StringLength(50)]
        public string? ClaimantMFANumber { get; set; }
        public int? ClaimantMFAYear { get; set; }
        
        [StringLength(50)]
        public string? ClaimantMFACNRNumber { get; set; }

        [DataType(DataType.Date)]
        public DateTime? ClaimantMFANextHearingDate { get; set; }

        [StringLength(100)]
        public string? ClaimantMFAStage { get; set; }
        
        [StringLength(50)]
        public string? ClaimantMFAEntrustmentNo { get; set; }
        
        [DataType(DataType.Date)]
        public DateTime? ClaimantMFAEntrustmentDate { get; set; }
        
        [StringLength(200)]
        public string? ClaimantMFAAdvocate { get; set; }
        
        [StringLength(50)]
        public string? ClaimantMFAStatus { get; set; } // Pending, Disposed, DNP
        
        public string? ClaimantMFARemarks { get; set; } // Award/Judgment Details
        
        [StringLength(50)]
        public string? ClaimantMFADecision { get; set; } // Favour Corp, Against Corp
        
        [StringLength(100)]
        public string? ClaimantActionTaken { get; set; } // Closed, Appeal Filed
        
        [StringLength(50)]
        public string? ClaimantApprovalNo { get; set; }
        
        [DataType(DataType.Date)]
        public DateTime? ClaimantApprovalDate { get; set; }



        // ==========================================
        // 9. APPEAL BEFORE SUPREME COURT (CLAIMANT)
        // ==========================================
        public bool IsClaimantSCAppeal { get; set; } // Checkbox to show/hide section
        public bool IsClaimantSCPending { get; set; } // Checkbox to hide SC No / Diary No

        [StringLength(50)]
        public string? ClaimantSCNumber { get; set; } // SLP / Civil Appeal

        [StringLength(50)]
        public string? ClaimantSCDiaryNumber { get; set; }
        public int? ClaimantSCYear { get; set; } // Diary Year
        
        public int? ClaimantSLPYear { get; set; } // SLP Year

        [StringLength(50)]
        public string? ClaimantSCFiledBy { get; set; } // Corp / Claimant
        
        [StringLength(50)]
        public string? ClaimantSCEntrustmentNo { get; set; }
        
        [DataType(DataType.Date)]
        public DateTime? ClaimantSCEntrustmentDate { get; set; }
        [StringLength(200)]
        public string? ClaimantSCAdvocate { get; set; }
        
        [StringLength(50)]
        public string? ClaimantSCStatus { get; set; }

        // SC Outcome Fields
        [StringLength(100)]
        public string? ClaimantSCOutcome { get; set; }
        [StringLength(100)]
        public string? ClaimantSCActionTaken { get; set; } // Matter Closed, etc.
        [StringLength(50)]
        public string? ClaimantSCClosureNo { get; set; }
        [DataType(DataType.Date)]
        public DateTime? ClaimantSCClosureDate { get; set; }

        public IFormFile? ClaimantSCJudgmentFile { get; set; }
        public string? ClaimantSCJudgmentPath { get; set; }

        // ==========================================
        // 10. COMPLIANCE & CLOSURE
        // ==========================================
        [StringLength(100)]
        public string? FinalComplianceStatus { get; set; }
        public decimal? AmountDeposited { get; set; }
        
        [DataType(DataType.Date)]
        public DateTime? FinalComplianceDate { get; set; }
        
        [StringLength(500)]
        public string? FinalRemarks { get; set; }

        public IFormFile? ComplianceLetterFile { get; set; }
        public string? ComplianceLetterPath { get; set; }
    }
}
