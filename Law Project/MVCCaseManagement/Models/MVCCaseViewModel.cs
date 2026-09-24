using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace MVCCaseManagement.Models
{
    public class MVCCaseViewModel
    {
        public int CaseID { get; set; }
        public int? OriginalCaseID { get; set; }
        public int RemindBackID { get; set; }
        public DateTime CreatedAt { get; set; }

        // SECTION A: CASE IDENTIFICATION
        [Required(ErrorMessage = "Division is required")]
        [Display(Name = "Division")]
        public int DivisionID { get; set; }
        public string? DivisionName { get; set; }

        [Required(ErrorMessage = "MVC Number is required")]
        [Display(Name = "MVC No")]
        [StringLength(50)]
        public string MVCNo { get; set; } = string.Empty;

        [Required(ErrorMessage = "MVC Year is required")]
        [Display(Name = "MVC Year")]
        public int MVCYear { get; set; }

        public bool IsPendingForFiling { get; set; }

        // MACTID is now optional — court establishment is resolved from eCourts NAPIX Gateway
        [Display(Name = "MACT Court")]
        public int MACTID { get; set; } = 0;
        public string? MACTName { get; set; }

        [Display(Name = "Vehicle Number")]
        [StringLength(20)]
        [RegularExpression(@"^(?=(?:.*\d){4,}).+$", ErrorMessage = "Vehicle number must contain at least 4 digits")]
        public string? VehicleNo { get; set; }

        [Display(Name = "Opposite Vehicle Number")]
        public List<string> OppositeVehicleNumbers { get; set; } = new();

        [Display(Name = "Vehicle Type")]
        [StringLength(20)]
        public string? VehicleType { get; set; } = "Corporation"; // Default
        
        [Display(Name = "Petition Filed Under")]
        [StringLength(100)]
        public string? PetitionFiledFor { get; set; }

        [DataType(DataType.Date)]
        [Display(Name = "Date of Accident")]
        public DateTime? AccidentDate { get; set; }

        [Display(Name = "Alleged Accident")]
        public bool IsAllegedAccident { get; set; }

        [Required(ErrorMessage = "Case Type is required")]
        [Display(Name = "Case Type")]
        [StringLength(50)]
        public string CaseType { get; set; } = "Pending";

        public int StatusID { get; set; } = 1; // Auto Pending

        // eCourts NAPIX Fields
        [Display(Name = "16-Digit CNR Number")]
        [StringLength(16)]
        public string? CNRNumber { get; set; }

        [Display(Name = "eCourts Establishment Code")]
        [StringLength(50)]
        public string? EstCode { get; set; }

        [Display(Name = "eCourts Case Type Code")]
        [StringLength(50)]
        public string? CaseTypeCode { get; set; }

        [Display(Name = "Live Case Status")]
        [StringLength(100)]
        public string? CaseStatus { get; set; }

        [Display(Name = "Court Hall / Bench")]
        [StringLength(150)]
        public string? CourtHall { get; set; }

        public DateTime? ModifiedDate { get; set; }
        public int? ModifiedBy { get; set; }

        // SECTION B: PETITIONER DETAILS
        public List<PetitionerViewModel> Petitioners { get; set; } = new();

        // SECTION C: NATURE OF CLAIM
        [Display(Name = "Claim Type")]
        [StringLength(50)]
        public string? ClaimType { get; set; }

        [Display(Name = "3rd Party")]
        public bool ThirdPartyFlag { get; set; }

        // SECTION D: FINANCIAL & ADVOCATE DETAILS
        [Display(Name = "Claim Amount")]
        public decimal? ClaimAmount { get; set; }

        [Display(Name = "Advocate Name")]
        public int? AdvocateID { get; set; }
        public string? AdvocateName { get; set; }

        [Display(Name = "Entrustment Number")]
        [StringLength(50)]
        public string? EntrustmentNo { get; set; }

        [DataType(DataType.Date)]
        [Display(Name = "Entrustment Date")]
        public DateTime? EntrustmentDate { get; set; }



        // SECTION E: CASE LINKAGES
        [Display(Name = "Double Claim")]
        public bool DoubleClaimFlag { get; set; }

        [Display(Name = "Whether the claim petition is filed within the time period of limitation?")]
        public bool IsWithinLimitation { get; set; }

        [Display(Name = "Limitation Remark")]
        public string? LimitationRemark { get; set; }

        [Display(Name = "Delay Condoned?")]
        public bool? IsDelayCondoned { get; set; }

        [Display(Name = "Delay Condonation Remark")]
        public string? DelayRemark { get; set; }

        public bool HasCentralOfficeAction { get; set; }

        public List<ConnectedCaseViewModel> ConnectedCases { get; set; } = new();

        // SECTION F: CASE PROGRESS
        [Display(Name = "Document Sent?")]
        public bool IsDocumentSent { get; set; }

        [Display(Name = "Outward Number")]
        [StringLength(50)]
        public string? DocumentOutwardNo { get; set; }

        [DataType(DataType.Date)]
        [Display(Name = "Outward Date")]
        public DateTime? DocumentOutwardDate { get; set; }

        [Display(Name = "Whether the claimant / deceased was a pillion rider or inmate of the opposite vehicle?")]
        public bool IsOppositeVehicleInmate { get; set; }

        [Display(Name = "Objection Filed?")]
        public bool IsObjectionFiled { get; set; }

        [DataType(DataType.Date)]
        [Display(Name = "Objection Date")]
        public DateTime? ObjectionFiledDate { get; set; }

        [Display(Name = "Objection Outward Number")]
        [StringLength(50)]
        public string? ObjectionOutwardNo { get; set; }

        [Display(Name = "Evidence Filed?")]
        public bool IsEvidenceFiled { get; set; }

        [DataType(DataType.Date)]
        [Display(Name = "Next Date of Hearing")]
        public DateTime? NextHearingDate { get; set; }

        [Display(Name = "Current Stage")]
        [StringLength(100)]
        public string? CurrentStage { get; set; }

        // THIRD PARTY REGISTRATION FIELDS
        public List<RespondentViewModel> Respondents { get; set; } = new();
        public List<RespondentEvidenceViewModel> RespondentEvidence { get; set; } = new();
        public List<RespondentEvidenceViewModel> CorpEvidence { get; set; } = new();

        [Display(Name = "Claim Remark")]
        public string? ClaimRemark { get; set; }

        [DataType(DataType.Date)]
        [Display(Name = "Date of Claim Petition")]
        public DateTime? ClaimPetitionDate { get; set; }

        public bool HasInterimOrder { get; set; }
        public string? InterimOrderFilePath { get; set; }
        public IFormFile? InterimOrderFile { get; set; }

        // Moved from AdverseAward
        [Display(Name = "Whether deceased/injured was passenger in ST bus")]
        public bool IsSTPassenger { get; set; }

        [Display(Name = "Whether any medical expenses is paid by Corporation?")]
        public bool IsMedicalExpensesPaid { get; set; }

        [Display(Name = "Amount paid")]
        public decimal? MedicalPaidAmount { get; set; }

        [Display(Name = "Remark")]
        public string? MedicalPaidRemarks { get; set; }

        [Display(Name = "Whether ARF amount is paid?")]
        public bool IsARFAmountPaid { get; set; }

        [Display(Name = "Total amount paid")]
        public decimal? ARFPaidAmount { get; set; }

        [Display(Name = "Remark")]
        public string? ARFPaidRemarks { get; set; }

        // SECTION G: DISPOSAL STATUS
        [Display(Name = "Disposal Status")]
        [StringLength(50)]
        public string? DisposalStatus { get; set; }

        [Display(Name = "Result")]
        [StringLength(50)]
        public string? DisposalResult { get; set; }

        [Display(Name = "Disposal Remarks")]
        [StringLength(500)]
        public string? DisposalRemarks { get; set; }

        [DataType(DataType.Date)]
        [Display(Name = "Closure Date")]
        public DateTime? ClosureDate { get; set; }

        // ADVERSE AWARD WORKFLOW (Visible if DisposalResult == "Against")
        public AdverseAwardViewModel AdverseAward { get; set; } = new();

        // LINKED APPEAL DETAILS (For Central Office View)
        public AppealViewModel? LinkedAppeal { get; set; }

        public bool HasAppeal { get; set; }
        public string? ForwardingStatus { get; set; }

        // List View MFA Details
        public string? MFANo { get; set; }
        public int? MFAYear { get; set; }
        public string? CorpMFANo { get; set; }
        public int? CorpMFAYear { get; set; }
        public string? ClaimantMFANo { get; set; }
        public int? ClaimantMFAYear { get; set; }
        public string? MFAEntrustmentNo { get; set; }
        public DateTime? MFAEntrustmentDate { get; set; }

        // CASE TRANSFER FIELDS
        public int? TransferredFromDivisionID { get; set; }
        public string? TransferredFromDivisionName { get; set; }
        public DateTime? TransferDate { get; set; }
        public bool IsTransferViewed { get; set; }
    }

    public class RespondentViewModel
    {
        public string RespondentName { get; set; } = string.Empty;
        public string? Remarks { get; set; }
    }

    public class RespondentEvidenceViewModel
    {
        public string Name { get; set; } = string.Empty;
        public string? Remark { get; set; } // For RespondentEvidence
        public string? Designation { get; set; } // For CorpEvidence
    }

    public class PetitionerViewModel
    {
        [Required(ErrorMessage = "Petitioner name is required")]
        [Display(Name = "Petitioner Name")]
        [StringLength(200)]
        public string PetitionerName { get; set; } = string.Empty;

        [Display(Name = "Relationship")]
        [StringLength(50)]
        public string? Relationship { get; set; }

        [Display(Name = "Custom Relationship")]
        [StringLength(100)]
        public string? RelationshipOthers { get; set; }
    }

    public class ConnectedCaseViewModel
    {
        public int? ConnectedCaseID { get; set; } // Added for viewing details
        [StringLength(50)]
        public string ConnectedMVCNo { get; set; } = string.Empty;
        public int ConnectedYear { get; set; }
        [StringLength(100)]
        public string? MACT { get; set; }
        [StringLength(50)]
        public string? Status { get; set; } // Added Status mapping to user's new list
        [StringLength(200)]
        public string? Remarks { get; set; }
        [StringLength(100)]
        public string? CurrentStage { get; set; }
        public bool IsDoubleClaim { get; set; }
        [StringLength(16)]
        public string? ConnectedCNRNumber { get; set; }
    }

    public class ConnectedCaseAwardDetails
    {
        [Display(Name = "Liability of Corporation")]
        public bool IsCorpLiable { get; set; }

        [Display(Name = "Liability Percentage")]
        [Range(0, 100)]
        public decimal? LiabilityPercentage { get; set; } = 100;

        [Display(Name = "Liability Remarks")]
        [StringLength(500)]
        public string? LiabilityRemarks { get; set; }

        [Display(Name = "Award Amount")]
        public decimal? AwardAmount { get; set; }

        [Display(Name = "Rate of Interest")]
        public decimal? InterestRate { get; set; }

        [Display(Name = "Age of Injured/Deceased")]
        public int? VictimAge { get; set; }

        [Display(Name = "Occupation")]
        [StringLength(100)]
        public string? Occupation { get; set; }

        [Display(Name = "Income Considered")]
        public decimal? IncomeConsidered { get; set; }

        [Display(Name = "Injury / Damage Details")]
        [StringLength(1000)]
        public string? InjuryDetails { get; set; }

        [Display(Name = "Disability Percentage")]
        public decimal? DisabilityPercentage { get; set; }

        public decimal? PainSufferings { get; set; }
        public decimal? ConveyanceAttendant { get; set; }
        public decimal? LossOfFutureIncome { get; set; }
        public decimal? LossOfIncomeLaidUp { get; set; }
        public decimal? LossOfAmenities { get; set; }
        public decimal? FutureMedicalExpenses { get; set; }
    }

    public class DoctorEvidenceViewModel
    {
        public string DoctorName { get; set; } = string.Empty;
        public bool IsTreated { get; set; }
    }

    public class AdverseConnectedCase
    {
        public string? ConnectedMVCNo { get; set; }
        public int ConnectedYear { get; set; }
        
        [StringLength(100)]
        public string? MACT { get; set; }
        
        public string? Status { get; set; }
        
        [StringLength(100)]
        public string? CurrentStage { get; set; }

        public ConnectedCaseAwardDetails AwardDetails { get; set; } = new();
    }

    public class CustomCompensationHead
    {
        public string HeadName { get; set; } = string.Empty;
        public decimal Amount { get; set; }
    }

    public class AdverseAwardViewModel
    {
        // 1. Vehicle & Insurance Details
        [Display(Name = "ST Vehicle Insurance Details")]
        [StringLength(500)]
        public string? BusInsuranceDetails { get; set; }

        [Display(Name = "Is ST Vehicle Insured")]
        public bool IsBusInsured { get; set; }

        [Display(Name = "Manner of Accident as per claim petition / award")]
        public string? MannerOfAccident { get; set; }

        [Display(Name = "Manner of accident as per departmental accidental record (as per RO's report)")]
        public string? MannerOfAccidentRO { get; set; }


        // 2. Important Dates
        [DataType(DataType.Date)]
        [Display(Name = "Date of Claim Petition")]
        public DateTime? ClaimPetitionDate { get; set; }

        [DataType(DataType.Date)]
        [Display(Name = "Date of Award")]
        public DateTime? AwardDate { get; set; }

        // 3. Objections
        [Display(Name = "Objection Filed")]
        public bool ObjectionFiled { get; set; } = true;

        [Display(Name = "Objection Remarks")]
        [StringLength(500)]
        public string? ObjectionRemarks { get; set; }



        // 4. Gov ID Proof
        [Display(Name = "Gov ID Proof")]
        public IFormFile? GovIDProofFile { get; set; }
        public string? GovIDProofUploadPath { get; set; }

        // EVIDENCE DETAILS
        public List<string> PetitionerPWNames { get; set; } = new();
        public List<DoctorEvidenceViewModel> Doctors { get; set; } = new();
        
        [Display(Name = "Treated Doctor")]
        public bool TreatedDocFlag { get; set; }
        
        [Display(Name = "RW Type")]
        public string? RWType { get; set; } // Driver, Conductor, Others
        public List<string> RWNames { get; set; } = new();
        public List<string> RWDesignations { get; set; } = new();

        [Display(Name = "TR-18 Remarks")]
        [StringLength(1000)]
        public string? TR18Remarks { get; set; }
        public string? TR18UploadPath { get; set; }
        public IFormFile? TR18File { get; set; }

        [Display(Name = "Whether names of the deceased/injured are found in TR-18?")]
        public bool IsDeceasedInTR18 { get; set; }

        [Display(Name = "Whether petitioner claimed Medical Insurance")]
        public bool IsMedicalInsuranceClaimed { get; set; }

        [Display(Name = "Whether the petitioners filed application for condonation of delay?")]
        public bool IsDelayApplicationFiled { get; set; }
        public string? DelayApplicationPath { get; set; }
        public IFormFile? IAFile { get; set; }

        [Display(Name = "Whether delay is condoned?")]
        public bool? IsDelayCondonedAdverse { get; set; }
        public string? DelayCondonedOrderPath { get; set; }
        public IFormFile? DelayOrderFile { get; set; }

        [Display(Name = "Future Prospectus")]
        [StringLength(1000)]
        public string? FutureProspectus { get; set; }

        [Display(Name = "Alleged Accident")]
        public bool IsAllegedAccident { get; set; }

        // CERTIFIED COPY DETAILS
        [DataType(DataType.Date)]
        [Display(Name = "copy Disposed On")]
        public DateTime? DisposedOnDate { get; set; }

        [DataType(DataType.Date)]
        [Display(Name = "Certified Copy Applied On")]
        public DateTime? CopyAppliedDate { get; set; }
        
        [Display(Name = "Remark")]
        [StringLength(500)]
        public string? CertifiedCopyRemarks { get; set; }

        [DataType(DataType.Date)]
        [Display(Name = "copy ready on")]
        public DateTime? CopyIssuedDate { get; set; }

        [DataType(DataType.Date)]
        [Display(Name = "Copy Delivered on")]
        public DateTime? CopyDeliveredDate { get; set; }

        [DataType(DataType.Date)]
        [Display(Name = "copy received at division")]
        public DateTime? CopyReceivedDate { get; set; }

        // AWARD DETAILS
        [Display(Name = "Liability of Corporation")]
        public bool IsCorpLiable { get; set; }

        [Display(Name = "Liability Percentage")]
        [Range(0, 100)]
        public decimal? LiabilityPercentage { get; set; } = 100;

        [Display(Name = "Liable Amount")]
        public decimal? LiableAmount { get; set; }

        [Display(Name = "Liability Remarks")]
        [StringLength(500)]
        public string? LiabilityRemarks { get; set; }
        [Display(Name = "Award Amount")]
        public decimal? AwardAmount { get; set; }

        [Display(Name = "Rate of Interest")]
        public decimal? InterestRate { get; set; }

        [Display(Name = "Age of Injured/Deceased")]
        public int? VictimAge { get; set; }

        [Display(Name = "Occupation")]
        [StringLength(100)]
        public string? Occupation { get; set; }

        [Display(Name = "Income Considered by MACT")]
        public decimal? IncomeConsidered { get; set; }

        public string? IncomePeriod { get; set; } = "monthly";

        public string? InjuryType { get; set; }

        [Display(Name = "Injury / Damage Details")]
        [StringLength(1000)]
        public string? InjuryDetails { get; set; }

        // DISCIPLINARY & COMPENSATION
        [Display(Name = "Punishment on Vehicle Driver")]
        [StringLength(200)]
        public string? DriverPunishmentStatus { get; set; } // Pending for Orders, etc.
        public string? PunishmentOrderUploadPath { get; set; }
        public IFormFile? PunishmentOrderFile { get; set; }
        
        [Display(Name = "Punishment Remarks")]
        [StringLength(500)]
        public string? PunishmentRemarks { get; set; }

        [Display(Name = "Interim Compensation Paid")]
        public decimal? InterimCompAmount { get; set; }

        [Display(Name = "Interim Compensation deducted?")]
        public bool InterimCompDeducted { get; set; }

        [Display(Name = "Double Claim")]
        public bool DoubleClaimFlag { get; set; }

        [Display(Name = "Double Claim Details")]
        [StringLength(500)]
        public string? DoubleClaimDetails { get; set; }

        // OPINIONS
        [Display(Name = "Opinion of Advocate")]
        [StringLength(1000)]
        public string? AdvocateOpinion { get; set; }

        [Display(Name = "Opinion of Law Officer")]
        [StringLength(1000)]
        public string? LOOpinion { get; set; }

        [Display(Name = "Opinion of Divisional Controller")]
        [StringLength(1000)]
        public string? DCOpinion { get; set; }

        // CLOSURE / FORWARDING
        [Display(Name = "Forwarding Status")]
        [StringLength(100)]
        public string? ForwardingStatus { get; set; } // Closed at Division, Sent to CO

        [Display(Name = "Closure Remarks")]
        [StringLength(1000)]
        public string? ClosureRemarks { get; set; }

        [DataType(DataType.Date)]
        [Display(Name = "Closure Date")]
        public DateTime? ClosureDate { get; set; }

        public string? AdverseJudgmentUploadPath { get; set; }
        public IFormFile? AdverseJudgmentFile { get; set; }

        [Display(Name = "Outward Number")]
        [StringLength(50)]
        public string? OutwardNumber { get; set; }

        [DataType(DataType.Date)]
        [Display(Name = "Outward Date")]
        public DateTime? OutwardDate { get; set; }

        // Security Investigation
        [Display(Name = "Security Investigation Conducted?")]
        public bool SecurityRequired { get; set; }
        public string? SecurityUploadPath { get; set; }
        public IFormFile? SecurityDocFile { get; set; }

        // NEW FIELDS FROM USER REQUEST
        [Display(Name = "FIR filed against ST Driver")]
        public bool? IsFIRFiled { get; set; }

        [Display(Name = "Chargesheet filed against ST Driver")]
        public bool? IsChargeSheetFiled { get; set; }

        [Display(Name = "Whether bus cameras are installed in the ST bus")]
        public bool? IsBusCameraInstalled { get; set; }

        [Display(Name = "Whether camera footage is produced before the Tribunal")]
        public bool? IsCameraFootageProduced { get; set; }

        [Display(Name = "Photography/ Videography produced?")]
        public bool? IsPhotographProduced { get; set; }

        [Display(Name = "Reasons for not producing photographs")]
        public string? PhotographNotProducedReason { get; set; }

        [Display(Name = "Police sketch Exhibit No.")]
        public string? PoliceSketchExhibitNo { get; set; }

        [Display(Name = "Whether police sketch is enclosed (if not produced)")]
        public bool? IsPoliceSketchEnclosed { get; set; }

        [Display(Name = "Whether evidence is adduced based on security investigation report")]
        public bool? IsEvidenceBasedOnSecurityReport { get; set; }

        [Display(Name = "Reasons for not adducing evidence from security report")]
        public string? SecurityReportNoEvidenceReason { get; set; }

        [Display(Name = "Whether impleading application is filed")]
        public bool? IsImpleadingAppFiled { get; set; }

        [Display(Name = "Reasons for not filing impleading application")]
        public string? ImpleadingAppNotFiledReason { get; set; }

        [Display(Name = "Whether the deceased was salaried person")]
        public bool? IsVictimSalaried { get; set; }

        [Display(Name = "Whether income of the deceased is cross verified")]
        public bool? IsIncomeCrossVerified { get; set; }

        [Display(Name = "Whether income tallies with documents")]
        public bool? DoesIncomeTallyWithDocuments { get; set; }

        [Display(Name = "Whether medical bills verified in injury/death")]
        public bool? IsMedicalBillsVerified { get; set; }

        [Display(Name = "Age Proof Verification")]
        public IFormFile? AgeProofFile { get; set; }
        public string? AgeProofUploadPath { get; set; }

        [Display(Name = "Whether any amount is deposited in view of EP/attachment warrant")]
        public bool IsAmountDepositedInEP { get; set; }

        [Display(Name = "Whether EP filed?")]
        public bool IsEPFiled { get; set; }

        [Display(Name = "Amount Deposited in EP")]
        public decimal? EPDepositedAmount { get; set; }

        // Compensation Breakdown
        [Display(Name = "Future Prospects (%)")]
        public decimal? FutureProspectsPercentage { get; set; }

        [Display(Name = "Personal Expenses Deduction")]
        public decimal? PersonalExpensesDeduction { get; set; }

        [Display(Name = "Multiplier")]
        public decimal? Multiplier { get; set; }

        [Display(Name = "Loss of Dependency")]
        public decimal? LossOfDependency { get; set; }

        [Display(Name = "Loss of Consortium")]
        public decimal? LossOfConsortium { get; set; }

        [Display(Name = "Loss of Estate")]
        public decimal? LossOfEstate { get; set; }

        [Display(Name = "Funeral Expenses")]
        public decimal? FuneralExpenses { get; set; }

        [Display(Name = "Loss of Love & Affection")]
        public decimal? LossOfLoveAffection { get; set; }

        [Display(Name = "Medical Expense and Other Expense (Death)")]
        public decimal? MedicalExpenseOther { get; set; }

        public decimal? PainSufferings { get; set; }
        public decimal? ConveyanceAttendant { get; set; }
        public decimal? LossOfFutureIncome { get; set; }
        public decimal? LossOfIncomeLaidUp { get; set; }
        public decimal? LossOfAmenities { get; set; }
        public decimal? FutureMedicalExpenses { get; set; }
        public decimal? InjuryOtherExpense { get; set; }

        [Display(Name = "Round Off Amount")]
        public decimal? RoundOffAmount { get; set; }

        public List<CustomCompensationHead> CustomCompensationHeads { get; set; } = new();

        public List<AdverseConnectedCase> ConnectedCases { get; set; } = new();

        [Display(Name = "Disability Percentage assessed by MACT")]
        public decimal? DisabilityPercentage { get; set; }
        
        // Payment Details for Audit
        public List<CasePaymentViewModel> Payments { get; set; } = new();


        public List<EnclosedDocument> EnclosedDocuments { get; set; } = new();

        [Display(Name = "As per e-Courts (EP Status)")]
        public string? AsPerECourts { get; set; }

        [Display(Name = "EP Number")]
        public string? EPNumber { get; set; }

        [Display(Name = "EP Court")]
        public string? EPCourt { get; set; }

        [Display(Name = "Current Stage of EP")]
        public string? EPStage { get; set; }

        [DataType(DataType.Date)]
        [Display(Name = "EP Next Hearing Date")]
        public DateTime? EPNextHearingDate { get; set; }
    }

}
