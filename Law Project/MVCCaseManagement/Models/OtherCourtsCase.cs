using System;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace MVCCaseManagement.Models
{
    public class OtherCourtsCase
    {
        [Key]
        public int CaseID { get; set; }
        
        public int DivisionID { get; set; }
        
        [Required]
        public string? CaseType { get; set; } // OS, PSC, CC, ECA, Consumer, LAC
        
        public string? CaseStage { get; set; } // ಪ್ರಕರಣದ ಹಂತ
        
        [Required]
        public string LitigantType { get; set; } // Corporation, Claimant
        
        public bool IsPendingForFiling { get; set; }
        
        public string? CaseNumber { get; set; }
        
        public int? CaseYear { get; set; }
        
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

        public string? Court { get; set; }
        
        public string? CaseNature { get; set; }
        
        public string? ClaimDetails { get; set; }
        
        public string? PetitionerName { get; set; }
        public string? PetitionerRelationship { get; set; }
        public string? ClaimType { get; set; }
        public DateTime? DateOfClaimPetition { get; set; }
        
        public string? RespondentName { get; set; }
        
        public List<RespondentInfo> Respondents { get; set; } = new List<RespondentInfo>();
        
        public List<RespondentInfo> RespondentEvidence { get; set; } = new List<RespondentInfo>();
        
        public List<CorpEvidence> CorpEvidence { get; set; } = new List<CorpEvidence>();
        
        public List<EnclosedDocument> EnclosedDocuments { get; set; } = new List<EnclosedDocument>();
        
        public DateTime? CopyDisposedOn { get; set; }
        public DateTime? CopyAppliedOn { get; set; }
        public DateTime? CopyReadyOn { get; set; }
        public DateTime? CopyDeliveredOn { get; set; }
        public DateTime? CopyReceivedAtDivision { get; set; }
        public string? CertifiedCopyRemarks { get; set; }
        
        public string? EntrustmentNumber { get; set; }
        
        public DateTime? EntrustmentDate { get; set; }
        
        public string? AdvocateName { get; set; }
        
        public string? EvidenceFiled { get; set; } // Yes, No
        
        public string? CaseStatus { get; set; }
        
        public string? DocumentSent { get; set; } // Yes, No
        public string? ObjectionVerified { get; set; } // Yes, No
        public string? IsObjectionFiled { get; set; } // Yes, No
        public string? ObjectionPending { get; set; } // Yes, No
        public string? ObjectionRemarks { get; set; }
        
        public DateTime? NextDateOfHearing { get; set; }
        
        public string? InterimOrder { get; set; } // Yes, No
        
        public string? InterimOrderFilePath { get; set; }
        
        public IFormFile? InterimOrderFile { get; set; }
        
        public string? AwardDetails { get; set; }
        
        public string? Result { get; set; } // FAVOR, AGAINST
        
        public string? DisposalStatus { get; set; } // DISPOSED, SENT TO CENTRAL OFFICE, CLOSED AT DIVISION LEVEL
        
        public DateTime? ClosureDate { get; set; }
        
        public string? ClosureNumber { get; set; }
        
        public string? ClosureRemark { get; set; }
        
        // Opinions & Final Status
        public string? AdvocateOpinion { get; set; }
        public string? LawOfficerOpinion { get; set; }
        public string? DCFinalDecision { get; set; }
        public string? FinalForwardingStatus { get; set; } // PENDING, FORWARDED, etc.
        public string? FinalJudgmentFilePath { get; set; }
        public IFormFile? FinalJudgmentFile { get; set; }
        
        public string? OutwardNumber { get; set; }
        public DateTime? OutwardDate { get; set; }

        public string? InitialOutwardNumber { get; set; }
        public DateTime? InitialOutwardDate { get; set; }
        
        public string? ObjectionOutwardNumber { get; set; }
        public DateTime? ObjectionFiledDate { get; set; }
        
        public int? CreatedBy { get; set; }
        
        public DateTime CreatedDate { get; set; } = DateTime.Now;
        
        public int? ModifiedBy { get; set; }
        
        public DateTime? ModifiedDate { get; set; }

        // For display
        public string? DivisionName { get; set; }

        // ECA & Specific fields
        public string? IsCorporationEmployee { get; set; } // Yes, No
        public string? IsVehicleInvolved { get; set; } // yes, no
        public string? VehicleType { get; set; } // Corporation, Private
        public string? VehicleNumber { get; set; }
        public DateTime? DateOfAccident { get; set; }
        public string? MannerOfIncident { get; set; }
        public decimal? ClaimAmount { get; set; }
        public string? ClaimRemarks { get; set; }
        public List<PetitionerInfo> Petitioners { get; set; } = new List<PetitionerInfo>();

        // Appeal Details (Added for LAC and other courts)
        public string? AppealNumber { get; set; }
        public int? AppealYear { get; set; }
        public string? ArisingOutOSNumber { get; set; }
        public int? OSYear { get; set; }
        public string? AppealEntrustmentNumber { get; set; }
        public DateTime? AppealEntrustmentDate { get; set; }
        public string? CourtAppellateAuth { get; set; }
        public string? AppealAdvocateName { get; set; }
        
        public decimal? AppealComplianceAmount { get; set; }
        public string? AppealChequeNumber { get; set; }
        public DateTime? AppealChequeDate { get; set; }
        
        public string? AppealAwardDetails { get; set; }
        public DateTime? AppealCaseDisposedOn { get; set; }
        public DateTime? AppealCopyAppliedOn { get; set; }
        public DateTime? AppealCopyReadyOn { get; set; }
        public DateTime? AppealCopyDeliveredOn { get; set; }
        public DateTime? AppealCopyReceivedAtDivision { get; set; }
        public string? AppealDelayRemarks { get; set; }
        
        public string? AppealAdvocateOpinion { get; set; }
        public string? AppealALOOpinion { get; set; }
        public string? AppealDCOpinion { get; set; }
        public string? AppealForwardingStatus { get; set; } // PENDING, FORWARDED TO CO, CLOSED
        
        public string? AppealOutwardNumber { get; set; }
        public DateTime? AppealOutwardDate { get; set; }
        public string? AppealClosureNumber { get; set; }
        public DateTime? AppealClosureDate { get; set; }
        
        public string? AppealJudgmentCopyPath { get; set; }
        public IFormFile? AppealJudgmentCopy { get; set; }
    }

    public class PetitionerInfo
    {
        public string? Name { get; set; }
        public string? Remark { get; set; }
    }

    public class RespondentInfo
    {
        public string? Name { get; set; }
        public string? Remark { get; set; }
    }

    public class CorpEvidence
    {
        public string? Name { get; set; }
        public string? Designation { get; set; }
    }


}
