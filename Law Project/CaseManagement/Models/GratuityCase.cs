using System;
using System.ComponentModel.DataAnnotations;

namespace MVCCaseManagement.Models
{
    public class GratuityCase
    {
        public int CaseID { get; set; }
        public DateTime? CreatedDate { get; set; }
        public DateTime? ModifiedDate { get; set; }
        public string? CreatedBy { get; set; }
        public bool IsViewedByCO { get; set; }

        // 1. Basic Case Details
        public string? CaseStatus { get; set; }
        public int? DivisionCode { get; set; }
        [Required(ErrorMessage = "PGA / CR Number is required")]
        public string? PGANumber { get; set; }
        public string? CourtType { get; set; }
        [Required(ErrorMessage = "Claimant Name is required")]
        public string? ClaimantName { get; set; }
        public DateTime? DateOfBirth { get; set; }
        public string? ClaimantDesignation { get; set; }
        public string? WorkingStatus { get; set; }
        [Required(ErrorMessage = "Entrustment Number is required")]
        public string? EntrustmentNo { get; set; }
        [Required(ErrorMessage = "Entrustment Date is required")]
        public DateTime? EntrustmentDate { get; set; }
        public string? AdvocateName { get; set; }
        public bool IsAdditionalBenefitsGiven { get; set; }
        public decimal? AdditionalBenefitsAmount { get; set; }

        // 2. Case Outcome
        public string? DisposalResult { get; set; }

        // 2a. Case Progress
        public bool IsDocumentSent { get; set; }
        public string? DocumentOutwardNo { get; set; }
        public DateTime? DocumentOutwardDate { get; set; }
        public bool IsObjectionFiled { get; set; }
        public string? ObjectionOutwardNo { get; set; }
        public DateTime? ObjectionFiledDate { get; set; }
        public bool IsEvidenceFiled { get; set; }
        public string? CurrentStage { get; set; }
        public DateTime? NextHearingDate { get; set; }

        // 3. Work After Disposal
        public string? AmountClaimed { get; set; }
        public DateTime? AppointmentDate { get; set; } // Corp
        public DateTime? AppointmentDate_CA { get; set; } // ALC
        public string? CA_AppointmentRemark { get; set; }
        public DateTime? RetirementDate { get; set; } // Corp
        public DateTime? RetirementDate_CA { get; set; } // ALC
        public string? TotalServicePeriod { get; set; }
        public string? Period_SPE_LWA_ABS { get; set; } // Corp
        public string? Period_SPE_LWA_ABS_CA { get; set; } // ALC
        public string? QualifyingService_Corp { get; set; }
        public string? QualifyingService_CA { get; set; }
        public decimal? LastDrawnPay { get; set; } // Corp
        public decimal? LastDrawnPay_CA { get; set; } // ALC
        public decimal? LastDrawnBasic { get; set; } // Corp
        public decimal? LastDrawnBDA { get; set; } // Corp
        public decimal? LastDrawnDA { get; set; } // Corp
        public decimal? LastDrawnBasic_CA { get; set; } // ALC
        public decimal? LastDrawnBDA_CA { get; set; } // ALC
        public decimal? LastDrawnDA_CA { get; set; } // ALC

        // 4. Gratuity Calculation – Corporation
        public decimal? GratuityAmount_Corp_Reg { get; set; }
        public decimal? GratuityAmount_Corp_Act { get; set; }
        public bool IsDeductionMade { get; set; }
        public decimal? DeductionAmount { get; set; }
        public string? DeductionDetails { get; set; }
        public decimal? ActualPaidAmount { get; set; }
        public string? ChequeNumber { get; set; }
        public DateTime? ChequeDate { get; set; }

        // 5. Certified Copy Details
        public DateTime? DisposalDate { get; set; }
        public DateTime? CopyAppliedDate { get; set; }
        public DateTime? CopyIssuedDate { get; set; }
        public DateTime? CopyDeliveredDate { get; set; }
        public DateTime? CopyReceivedDate { get; set; }
        public string? DelayRemarks { get; set; }

        // 6. Award Details (Controlling Authority)
        public decimal? GratuityAmount_CA_Reg { get; set; }
        public decimal? GratuityAmount_CA_Act { get; set; }
        public decimal? OrderedAmount_CA { get; set; }
        
        // 6a. Deduction & Payment Details (Controlling Authority)
        public bool IsDeductionMade_CA { get; set; }
        public decimal? DeductionAmount_CA { get; set; }
        public string? DeductionDetails_CA { get; set; }
        public decimal? ActualPaidAmount_CA { get; set; }
        
        // 6b. Interest Payment Details (CA)
        public bool IsInterestPayable { get; set; }
        public decimal? InterestRate { get; set; }
        public string? InterestRemarks { get; set; }
        public System.Collections.Generic.List<GratuityInterestPayment> InterestPayments { get; set; } = new();
        
        public string? ChequeNumber_CA { get; set; }
        public DateTime? ChequeDate_CA { get; set; }

        public string? AdvocateOpinion { get; set; }
        public string? LOOpinion { get; set; }
        public string? DCOpinion { get; set; }

        // 7. Appeal Before Appellate Authority
        public string? AppealNumber { get; set; }
        public int? AppealYear { get; set; }
        public string? AppealArisingNo { get; set; }
        public int? AppealArisingYear { get; set; }
        public string? AppealCourt { get; set; }
        public string? AppealEntrustmentNo { get; set; }
        public DateTime? AppealEntrustmentDate { get; set; }
        public string? AppealAdvocate { get; set; }

        public decimal? AppealComplianceAmount { get; set; }
        public string? AppealComplianceChequeNumber { get; set; }
        public DateTime? AppealComplianceChequeDate { get; set; }

        // 8. After Disposal of Appeal
        public string? AppealAwardDetails { get; set; }
        public DateTime? AppealDisposalDate { get; set; }
        public string? AppealJudgmentPath { get; set; }
        public decimal? FinalAmount { get; set; }
        public string? ComplianceStatus { get; set; }
        public string? Remarks { get; set; }
        public DateTime? AppealCopyAppliedDate { get; set; }
        public DateTime? AppealCopyReadyDate { get; set; }
        public DateTime? AppealCopyDeliveredDate { get; set; }
        public DateTime? AppealCopyReceivedDate { get; set; }
        public string? AppealDelayRemarks { get; set; }
        public string? AppealAdvocateOpinion { get; set; }
        public string? AppealLOOpinion { get; set; }
        public string? AppealDCOpinion { get; set; }

        public bool? IsFeasibilityReceived { get; set; } // Yes/No
        public DateTime? FeasibilityReceiptDate { get; set; }
        public string? ActionTaken { get; set; } // Pending, Appeal, Close
        public string? ApprovalOutwardNo { get; set; } // If Closed
        public DateTime? ApprovalDate { get; set; } // If Closed
        
        // 9. Closure / Forwarding
        public string? ForwardingStatus { get; set; }
        public string? ClosureRemarks { get; set; }
        public DateTime? ClosureDate { get; set; }
        public string? OutwardNumber { get; set; }
        public DateTime? OutwardDate { get; set; }
        public string? AdverseJudgmentPath { get; set; }
        public string? AppealRemarks { get; set; }
        public DateTime? AppealDate { get; set; }
        
        // 9a. Appeal Forwarding (separate from initial forwarding)
        public string? ForwardingStatus_Appeal { get; set; }
        public string? OutwardNumber_Appeal { get; set; }
        public DateTime? OutwardDate_Appeal { get; set; }
        
        // Exact Mirror Fields
        public string? InitialActionRemarks { get; set; }
        public string? InitialActionPath1 { get; set; }
        public string? InitialActionPath2 { get; set; }
        public string? FinalRemarks { get; set; }
        public string? EvidenceRemarks { get; set; }
        public string? EvidenceWitnessName { get; set; }
        public string? EvidenceWitnessDesignation { get; set; }

        // Appeal Action Details
        public string? AppealActionOutwardNo { get; set; }
        public DateTime? AppealActionDate { get; set; }

        // ==========================================
        // MIRRORED FIELDS FROM APPEAL VIEW MODEL
        // ==========================================

        // High Court Appeal (Corporation)
        public bool IsPendingForFiling { get; set; }
        public string? HighCourtBench { get; set; }
        public string? OtherHighCourtBench { get; set; }
        public bool StayGranted { get; set; }
        public string? StayComplianceOutwardNo { get; set; }
        public DateTime? StayComplianceDate { get; set; }
        public string? StayOrderPath1 { get; set; } // Assuming file uploads handled separately
        public string? StayOrderPath2 { get; set; }

        public string? CorpWPNumber { get; set; }
        public int? CorpWPYear { get; set; }
        public string? CorpWPAdvocate { get; set; }
        public string? CorpWPEntrustmentNo { get; set; }
        public DateTime? CorpWPEntrustmentDate { get; set; }
        public string? CorpWPStatus { get; set; }
        public bool RestorationFiled { get; set; }
        public DateTime? RestorationDate { get; set; }
        public string? RestorationStatus { get; set; }
        public string? CorpWPOutcome { get; set; }
        public string? CorpWPActionTaken { get; set; }
        public string? ClosureOutwardNo { get; set; }



        // Supreme Court Appeal (Claimant)
        public bool IsClaimantSCAppeal { get; set; }
        public bool IsClaimantSCPending { get; set; }
        public string? ClaimantSCDiaryNumber { get; set; }
        public int? ClaimantSCYear { get; set; }
        public string? ClaimantSCNumber { get; set; }
        public int? ClaimantSLPYear { get; set; }
        public string? ClaimantSCFiledBy { get; set; }
        public string? ClaimantSCEntrustmentNo { get; set; }
        public DateTime? ClaimantSCEntrustmentDate { get; set; }
        public string? ClaimantSCAdvocate { get; set; }
        public string? ClaimantSCStatus { get; set; }
        public string? ClaimantSCOutcome { get; set; }
        public string? ClaimantSCActionTaken { get; set; }
        public string? ClaimantSCClosureNo { get; set; }
        public DateTime? ClaimantSCClosureDate { get; set; }

        // Claimant Appeal (WP - High Court)
        public int? ClaimantDivisionID { get; set; }
        public string? ClaimantArisingWPNumber { get; set; }
        public int? ClaimantArisingWPYear { get; set; }
        public string? ClaimantMVCCurrentStatus { get; set; }
        public string? ClaimantWPNumber { get; set; }
        public int? ClaimantWPYear { get; set; }
        public string? ClaimantHighCourtBench { get; set; }
        public string? ClaimantOtherHighCourtBench { get; set; }
        public string? ClaimantWPEntrustmentNo { get; set; }
        public DateTime? ClaimantWPEntrustmentDate { get; set; }
        public string? ClaimantWPAdvocate { get; set; }
        public string? ClaimantWPStatus { get; set; }
        public string? ClaimantWPDecision { get; set; }
        public string? ClaimantActionTaken { get; set; }
        public string? ClaimantApprovalNo { get; set; }
        public DateTime? ClaimantApprovalDate { get; set; }

        public System.Collections.Generic.List<EnclosedDocument> EnclosedDocuments { get; set; } = new();
        public System.Collections.Generic.List<GratuityPayment> Payments { get; set; } = new();
    }
}
