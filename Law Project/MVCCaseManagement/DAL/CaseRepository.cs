using System;
using System.Collections.Generic;
using System.Data;
using Microsoft.Data.SqlClient;
using System.Text.Json;
using MVCCaseManagement.Models;

namespace MVCCaseManagement.DAL
{
    public class CaseRepository : ICaseRepository
    {
        private readonly DBHelper _db;

        public CaseRepository(DBHelper db)
        {
            _db = db;
        }

        public IEnumerable<ConnectedCaseViewModel> SearchLinkedCases(string vehicleNo, DateTime accidentDate)
        {
            var cases = new List<ConnectedCaseViewModel>();
            string query = @"
                SELECT c.MVCNo, c.MVCYear, m.MACTName as MACT, c.CurrentStage
                FROM MVC_CASES c
                LEFT JOIN MACT_MASTER m ON c.MACTID = m.MACTID
                WHERE REPLACE(c.VehicleNo, ' ', '') = REPLACE(@VehicleNo, ' ', '')
                  AND CAST(c.AccidentDate AS DATE) = CAST(@AccidentDate AS DATE)";

            var parameters = new[]
            {
                new SqlParameter("@VehicleNo", vehicleNo ?? (object)DBNull.Value),
                new SqlParameter("@AccidentDate", accidentDate.Date)
            };

            DataTable dt = _db.ExecuteQuery(query, parameters);
            foreach (DataRow row in dt.Rows)
            {
                cases.Add(new ConnectedCaseViewModel
                {
                    ConnectedMVCNo = row["MVCNo"]?.ToString() ?? "",
                    ConnectedYear = row["MVCYear"] != DBNull.Value ? Convert.ToInt32(row["MVCYear"]) : 0,
                    MACT = row["MACT"]?.ToString() ?? "",
                    CurrentStage = row["CurrentStage"]?.ToString() ?? "",
                    Remarks = "Auto-linked by system"
                });
            }
            return cases;
        }

        public int SaveCase(MVCCaseViewModel model)
        {
            using (var conn = new SqlConnection(_db.GetConnectionString()))
            {
                conn.Open();
                using (var trans = conn.BeginTransaction())
                {
                    try
                    {
                        // 0. CHECK FOR DUPLICATE CASE (CNRNumber takes priority; fallback to MVCNo+MVCYear)
                        int existingCount = 0;
                        if (!string.IsNullOrEmpty(model.CNRNumber))
                        {
                            string cnrDupQuery = "SELECT COUNT(*) FROM MVC_CASES WHERE CNRNumber = @CNRNumber";
                            var cnrDupCmd = new SqlCommand(cnrDupQuery, conn, trans);
                            cnrDupCmd.Parameters.AddWithValue("@CNRNumber", model.CNRNumber);
                            existingCount = (int)cnrDupCmd.ExecuteScalar();
                            if (existingCount > 0)
                                throw new InvalidOperationException($"Case already exists: CNR Number {model.CNRNumber} is already registered.");
                        }
                        else
                        {
                            string duplicateCheckQuery = @"
                                SELECT COUNT(*) FROM MVC_CASES 
                                WHERE MVCNo = @MVCNo AND MVCYear = @MVCYear";
                            var dupCmd = new SqlCommand(duplicateCheckQuery, conn, trans);
                            dupCmd.Parameters.AddWithValue("@MVCNo", model.MVCNo?.Trim() ?? string.Empty);
                            dupCmd.Parameters.AddWithValue("@MVCYear", model.MVCYear);
                            existingCount = (int)dupCmd.ExecuteScalar();
                            if (existingCount > 0)
                                throw new InvalidOperationException($"Case already exists: MVC No {model.MVCNo}/{model.MVCYear}. Please check the existing records.");
                        }

                        // 1. Save Main Case
                        string mainQuery = @"
                            INSERT INTO MVC_CASES (DivisionID, MVCNo, MVCYear, MACTID, VehicleNo, AccidentDate, CaseType, StatusID,
                                                 ClaimType, ThirdPartyFlag, ClaimAmount, AdvocateID, EntrustmentNo, EntrustmentDate,
                                                 DoubleClaimFlag, NextHearingDate, CurrentStage, DisposalStatus, DisposalResult, DisposalRemarks,
                                                 IsDocumentSent, DocumentOutwardNo, DocumentOutwardDate, IsObjectionFiled, ObjectionFiledDate, ObjectionOutwardNo, IsEvidenceFiled,
                                                 AdvocateName, VehicleType, PetitionFiledFor, ClosureDate,
                                                 IsWithinLimitation, LimitationRemark, IsDelayCondoned, DelayRemark,
                                                 IsSTPassenger, IsMedicalExpensesPaid, MedicalPaidAmount, MedicalPaidRemarks, IsARFAmountPaid, ARFPaidAmount, ARFPaidRemarks,
                                                 IsOppositeVehicleInmate, ClaimRemark, ClaimPetitionDate, HasInterimOrder, InterimOrderFilePath,
                                                 CNRNumber, EstCode, CaseTypeCode)
                            OUTPUT INSERTED.CaseID
                            VALUES (@DivisionID, @MVCNo, @MVCYear, @MACTID, @VehicleNo, @AccidentDate, @CaseType, @StatusID,
                                    @ClaimType, @ThirdPartyFlag, @ClaimAmount, @AdvocateID, @EntrustmentNo, @EntrustmentDate,
                                    @DoubleClaimFlag, @NextHearingDate, @CurrentStage, @DisposalStatus, @DisposalResult, @DisposalRemarks,
                                    @IsDocumentSent, @DocumentOutwardNo, @DocumentOutwardDate, @IsObjectionFiled, @ObjectionFiledDate, @ObjectionOutwardNo, @IsEvidenceFiled,
                                    @AdvocateName, @VehicleType, @PetitionFiledFor, @ClosureDate,
                                    @IsWithinLimitation, @LimitationRemark, @IsDelayCondoned, @DelayRemark,
                                    @IsSTPassenger, @IsMedicalExpensesPaid, @MedicalPaidAmount, @MedicalPaidRemarks, @IsARFAmountPaid, @ARFPaidAmount, @ARFPaidRemarks,
                                    @IsOppositeVehicleInmate, @ClaimRemark, @ClaimPetitionDate, @HasInterimOrder, @InterimOrderFilePath,
                                    @CNRNumber, @EstCode, @CaseTypeCode)";

                        var cmd = new SqlCommand(mainQuery, conn, trans);
                        cmd.Parameters.AddWithValue("@DivisionID", model.DivisionID);
                        cmd.Parameters.AddWithValue("@MVCNo", model.MVCNo?.Trim() ?? string.Empty);
                        cmd.Parameters.AddWithValue("@MVCYear", model.MVCYear);
                        cmd.Parameters.AddWithValue("@MACTID", model.MACTID);
                        cmd.Parameters.AddWithValue("@VehicleNo", (object?)model.VehicleNo?.Trim() ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@AccidentDate", model.AccidentDate ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@CaseType", model.CaseType);
                        cmd.Parameters.AddWithValue("@StatusID", model.StatusID);
                        cmd.Parameters.AddWithValue("@ClaimType", model.ClaimType ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@ThirdPartyFlag", model.ThirdPartyFlag);
                        cmd.Parameters.AddWithValue("@ClaimAmount", model.ClaimAmount ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@AdvocateID", model.AdvocateID ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@EntrustmentNo", model.EntrustmentNo ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@EntrustmentDate", model.EntrustmentDate ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@DoubleClaimFlag", model.DoubleClaimFlag);
                        cmd.Parameters.AddWithValue("@CNRNumber", (object?)model.CNRNumber?.Trim() ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@EstCode", model.EstCode ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@CaseTypeCode", model.CaseTypeCode ?? (object)DBNull.Value);

                        cmd.Parameters.AddWithValue("@NextHearingDate", model.NextHearingDate ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@CurrentStage", model.CurrentStage ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@DisposalStatus", model.DisposalStatus ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@DisposalResult", model.DisposalResult ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@DisposalRemarks", model.DisposalRemarks ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@IsDocumentSent", model.IsDocumentSent);
                        cmd.Parameters.AddWithValue("@DocumentOutwardNo", model.DocumentOutwardNo ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@DocumentOutwardDate", model.DocumentOutwardDate ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@IsObjectionFiled", model.IsObjectionFiled);
                        cmd.Parameters.AddWithValue("@VehicleType", model.VehicleType ?? "Corporation");
                        cmd.Parameters.AddWithValue("@PetitionFiledFor", model.PetitionFiledFor ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@ObjectionFiledDate", model.ObjectionFiledDate ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@IsEvidenceFiled", model.IsEvidenceFiled);
                        cmd.Parameters.AddWithValue("@AdvocateName", model.AdvocateName ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@ObjectionOutwardNo", model.ObjectionOutwardNo ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@ClosureDate", model.ClosureDate ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@IsWithinLimitation", model.IsWithinLimitation);
                        cmd.Parameters.AddWithValue("@LimitationRemark", model.LimitationRemark ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@IsDelayCondoned", (object)model.IsDelayCondoned ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@DelayRemark", model.DelayRemark ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@IsSTPassenger", model.IsSTPassenger);
                        cmd.Parameters.AddWithValue("@IsMedicalExpensesPaid", model.IsMedicalExpensesPaid);
                        cmd.Parameters.AddWithValue("@MedicalPaidAmount", model.MedicalPaidAmount ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@MedicalPaidRemarks", model.MedicalPaidRemarks ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@IsARFAmountPaid", model.IsARFAmountPaid);
                        cmd.Parameters.AddWithValue("@ARFPaidAmount", model.ARFPaidAmount ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@ARFPaidRemarks", model.ARFPaidRemarks ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@IsOppositeVehicleInmate", model.IsOppositeVehicleInmate);
                        cmd.Parameters.AddWithValue("@ClaimRemark", model.ClaimRemark ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@ClaimPetitionDate", (object?)model.ClaimPetitionDate ?? (object?)model.AdverseAward?.ClaimPetitionDate ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@HasInterimOrder", model.HasInterimOrder);
                        cmd.Parameters.AddWithValue("@InterimOrderFilePath", model.InterimOrderFilePath ?? (object)DBNull.Value);

                        int caseId = (int)cmd.ExecuteScalar();

                        // 2. Save Petitioners
                        foreach (var pet in model.Petitioners)
                        {
                            var petCmd = new SqlCommand("INSERT INTO MVC_CASE_PETITIONERS (CaseID, PetitionerName, Relationship) VALUES (@CaseID, @PetitionerName, @Relationship)", conn, trans);
                            petCmd.Parameters.AddWithValue("@CaseID", caseId);
                            petCmd.Parameters.AddWithValue("@PetitionerName", pet.PetitionerName);
                            string finalRel = pet.Relationship;
                            if (finalRel == "Others" && !string.IsNullOrEmpty(pet.RelationshipOthers))
                            {
                                finalRel = pet.RelationshipOthers;
                            }
                            petCmd.Parameters.AddWithValue("@Relationship", finalRel ?? (object)DBNull.Value);
                            petCmd.ExecuteNonQuery();
                        }

                        // 2.1 Save Respondents (Third Party)
                        foreach (var resp in model.Respondents)
                        {
                            if (!string.IsNullOrEmpty(resp.RespondentName))
                            {
                                var respCmd = new SqlCommand("INSERT INTO MVC_CASE_RESPONDENTS (CaseID, RespondentName, Remarks) VALUES (@CaseID, @RespondentName, @Remarks)", conn, trans);
                                respCmd.Parameters.AddWithValue("@CaseID", caseId);
                                respCmd.Parameters.AddWithValue("@RespondentName", resp.RespondentName);
                                respCmd.Parameters.AddWithValue("@Remarks", resp.Remarks ?? (object)DBNull.Value);
                                respCmd.ExecuteNonQuery();
                            }
                        }

                        // 3. Save Connected Cases (Section E)
                        foreach (var connCase in model.ConnectedCases)
                        {
                            var connCmd = new SqlCommand("INSERT INTO MVC_CASE_CONNECTED (CaseID, ConnectedMVCNo, ConnectedYear, Remarks, MACT, IsDoubleClaim) VALUES (@CaseID, @MVCNo, @Year, @Remarks, @MACT, @IsDoubleClaim)", conn, trans);
                            connCmd.Parameters.AddWithValue("@CaseID", caseId);
                            connCmd.Parameters.AddWithValue("@MVCNo", connCase.ConnectedMVCNo);
                            connCmd.Parameters.AddWithValue("@Year", connCase.ConnectedYear);
                            connCmd.Parameters.AddWithValue("@Remarks", connCase.Remarks ?? (object)DBNull.Value);
                            connCmd.Parameters.AddWithValue("@MACT", connCase.MACT ?? (object)DBNull.Value);
                            connCmd.Parameters.AddWithValue("@IsDoubleClaim", connCase.IsDoubleClaim);
                            connCmd.ExecuteNonQuery();
                        }

                        // 3.1 Save Opposite Vehicle Numbers
                        foreach (var vno in model.OppositeVehicleNumbers)
                        {
                            if (!string.IsNullOrEmpty(vno))
                            {
                                var vnoCmd = new SqlCommand("INSERT INTO MVC_CASE_OPPOSITE_VEHICLES (CaseID, VehicleNo) VALUES (@CaseID, @VehicleNo)", conn, trans);
                                vnoCmd.Parameters.AddWithValue("@CaseID", caseId);
                                vnoCmd.Parameters.AddWithValue("@VehicleNo", vno);
                                vnoCmd.ExecuteNonQuery();
                            }
                        }

                        // 4. Save Enclosed Documents
                        var adverseAward = model.AdverseAward ?? new AdverseAwardViewModel();
                        if (model.AdverseAward != null && model.AdverseAward.EnclosedDocuments != null)
                        {
                            foreach (var doc in model.AdverseAward.EnclosedDocuments)
                            {
                                if (!string.IsNullOrEmpty(doc.DocName))
                                {
                                    var docCmd = new SqlCommand("INSERT INTO MVC_CASE_ADVERSE_DOCS (CaseID, DocName, PageCount) VALUES (@CaseID, @DocName, @PageCount)", conn, trans);
                                    docCmd.Parameters.AddWithValue("@CaseID", caseId);
                                    docCmd.Parameters.AddWithValue("@DocName", doc.DocName);
                                    docCmd.Parameters.AddWithValue("@PageCount", doc.PageCount ?? (object)DBNull.Value);
                                    docCmd.ExecuteNonQuery();
                                }
                            }
                        }

                        // 5. Handle Adverse Award Details
                        if (adverseAward.ClaimPetitionDate == null && model.ClaimPetitionDate != null) adverseAward.ClaimPetitionDate = model.ClaimPetitionDate;
                        adverseAward.IsAllegedAccident = model.IsAllegedAccident || adverseAward.IsAllegedAccident;

                        string advQuery = @"
                                INSERT INTO MVC_CASE_ADVERSE_DETAILS (CaseID, BusInsuranceDetails, IsBusInsured, ClaimPetitionDate, AwardDate, MannerOfAccident, MannerOfAccidentRO,
                                    ObjectionFiled, ObjectionRemarks, RWType, TR18Remarks, IsDeceasedInTR18, TR18UploadPath, IsAllegedAccident,
                                    AdverseDoubleClaimFlag, DoubleClaimDetails, SecurityRequired, SecurityUploadPath, GovIDProofUploadPath,
                                    DisposedOnDate, CopyAppliedDate, CertifiedCopyRemarks, CopyIssuedDate, CopyReceivedDate, CopyDeliveredDate,
                                    AwardAmount, InterestRate, VictimAge, Occupation, IncomeConsidered, IncomePeriod, InjuryDetails,
                                    DriverPunishmentStatus, PunishmentOrderUploadPath, PunishmentRemarks, DisabilityPercentage, InterimCompAmount, InterimCompDeducted,
                                    AdvocateOpinion, LOOpinion, DCOpinion, ForwardingStatus, ClosureRemarks, ClosureDate, OutwardNumber, OutwardDate,
                                    IsCorpLiable, LiabilityPercentage, LiabilityRemarks, AdverseJudgmentUploadPath, FutureProspectus, InjuryType, TreatedDocFlag,
                                    IsSTPassenger, IsMedicalExpensesPaid, MedicalPaidAmount, MedicalPaidRemarks, IsARFAmountPaid, ARFPaidAmount, ARFPaidRemarks,
                                    IsDelayApplicationFiled, DelayApplicationPath, IsDelayCondonedAdverse, DelayCondonedOrderPath, IsMedicalInsuranceClaimed,
                                    IsFIRFiled, IsChargeSheetFiled, IsBusCameraInstalled, IsCameraFootageProduced, IsPhotographProduced, PhotographNotProducedReason,
                                    PoliceSketchExhibitNo, IsPoliceSketchEnclosed, IsEvidenceBasedOnSecurityReport, SecurityReportNoEvidenceReason,
                                    IsImpleadingAppFiled, ImpleadingAppNotFiledReason, IsVictimSalaried, IsIncomeCrossVerified, DoesIncomeTallyWithDocuments, IsMedicalBillsVerified,
                                    IsAmountDepositedInEP, IsEPFiled, EPDepositedAmount, FutureProspectsPercentage, PersonalExpensesDeduction, Multiplier,
                                    LossOfDependency, LossOfConsortium, LossOfEstate, FuneralExpenses, LossOfLoveAffection, MedicalExpenseOther, AgeProofUploadPath,
                                    PainSufferings, ConveyanceAttendant, LossOfFutureIncome, LossOfIncomeLaidUp, LossOfAmenities, FutureMedicalExpenses, InjuryOtherExpense, CustomCompensation, EPNumber, EPCourt, EPStage, EPNextHearingDate, RoundOffAmount, AsPerECourts)
                                VALUES (@CaseID, @BusInsurance, @IsBusInsured, @ClaimPetitionDate, @AwardDate, @MannerOfAccident, @MannerOfAccidentRO,
                                    @ObjectionFiled, @ObjectionRemarks, @RWType, @TR18Remarks, @IsDeceasedInTR18, @TR18Path, @IsAlleged,
                                    @DoubleClaimFlag, @DoubleClaimDetails, @SecurityRequired, @SecurityPath, @GovIDPath,
                                    @DisposedOn, @AppliedOn, @CopyRemarks, @IssuedOn, @ReceivedOn, @CopyDelivered,
                                    @AwardAmount, @InterestRate, @VictimAge, @Occupation, @Income, @IncomePeriod, @Injury,
                                    @PunishmentStatus, @PunishmentPath, @PunishmentRemarks, @DisabilityPercentage, @InterimAmount, @InterimDeducted,
                                    @AdvOpinion, @LOOpinion, @DCOpinion, @ForwardStatus, @ClosureRemarks, @ClosureDate, @OutwardNo, @OutwardDate,
                                    @IsCorpLiable, @LiabilityPercentage, @LiabilityRemarks, @AdverseJudgmentUploadPath, @FutureProspectus, @InjuryType, @TreatedDoc,
                                    @IsSTPassenger, @IsMedicalExpensesPaid, @MedicalPaidAmount, @MedicalPaidRemarks, @IsARFAmountPaid, @ARFPaidAmount, @ARFPaidRemarks,
                                    @IsDelayApplicationFiled, @DelayApplicationPath, @IsDelayCondonedAdverse, @DelayCondonedOrderPath, @IsMedicalInsuranceClaimed,
                                    @IsFIRFiled, @IsChargeSheetFiled, @IsBusCameraInstalled, @IsCameraFootageProduced, @IsPhotographProduced, @PhotographNotProducedReason,
                                    @PoliceSketchExhibitNo, @IsPoliceSketchEnclosed, @IsEvidenceBasedOnSecurityReport, @SecurityReportNoEvidenceReason,
                                    @IsImpleadingAppFiled, @ImpleadingAppNotFiledReason, @IsVictimSalaried, @IsIncomeCrossVerified, @DoesIncomeTallyWithDocuments, @IsMedicalBillsVerified,
                                    @IsAmountDepositedInEP, @IsEPFiled, @EPDepositedAmount, @FutureProspectsPercentage, @PersonalExpensesDeduction, @Multiplier,
                                    @LossOfDependency, @LossOfConsortium, @LossOfEstate, @FuneralExpenses, @LossOfLoveAffection, @MedicalExpenseOther, @AgeProofPath,
                                    @PainSufferings, @ConveyanceAttendant, @LossOfFutureIncome, @LossOfIncomeLaidUp, @LossOfAmenities, @FutureMedicalExpenses, @InjuryOtherExpense, @CustomCompensation, @EPNumber, @EPCourt, @EPStage, @EPNextHearingDate, @RoundOffAmount, @AsPerECourts)";

                            var advCmd = new SqlCommand(advQuery, conn, trans);
                            advCmd.Parameters.AddWithValue("@CaseID", caseId);
                            advCmd.Parameters.AddWithValue("@BusInsurance", adverseAward.BusInsuranceDetails ?? (object)DBNull.Value);
                            advCmd.Parameters.AddWithValue("@IsBusInsured", adverseAward.IsBusInsured);
                            advCmd.Parameters.AddWithValue("@ClaimPetitionDate", adverseAward.ClaimPetitionDate ?? (object)DBNull.Value);
                            advCmd.Parameters.AddWithValue("@AwardDate", adverseAward.AwardDate ?? (object)DBNull.Value);
                            advCmd.Parameters.AddWithValue("@MannerOfAccident", adverseAward.MannerOfAccident ?? (object)DBNull.Value);
                            advCmd.Parameters.AddWithValue("@MannerOfAccidentRO", adverseAward.MannerOfAccidentRO ?? (object)DBNull.Value);
                            advCmd.Parameters.AddWithValue("@ObjectionFiled", adverseAward.ObjectionFiled);
                            advCmd.Parameters.AddWithValue("@ObjectionRemarks", adverseAward.ObjectionRemarks ?? (object)DBNull.Value);
                            advCmd.Parameters.AddWithValue("@RWType", adverseAward.RWType ?? (object)DBNull.Value);
                            advCmd.Parameters.AddWithValue("@TR18Remarks", adverseAward.TR18Remarks ?? (object)DBNull.Value);
                            advCmd.Parameters.AddWithValue("@IsDeceasedInTR18", adverseAward.IsDeceasedInTR18);
                            advCmd.Parameters.AddWithValue("@TR18Path", adverseAward.TR18UploadPath ?? (object)DBNull.Value);
                            advCmd.Parameters.AddWithValue("@IsAlleged", adverseAward.IsAllegedAccident);
                            advCmd.Parameters.AddWithValue("@DoubleClaimFlag", adverseAward.DoubleClaimFlag);
                            advCmd.Parameters.AddWithValue("@DoubleClaimDetails", adverseAward.DoubleClaimDetails ?? (object)DBNull.Value);
                            advCmd.Parameters.AddWithValue("@SecurityRequired", adverseAward.SecurityRequired);
                            advCmd.Parameters.AddWithValue("@SecurityPath", adverseAward.SecurityUploadPath ?? (object)DBNull.Value);
                            advCmd.Parameters.AddWithValue("@GovIDPath", adverseAward.GovIDProofUploadPath ?? (object)DBNull.Value);
                            advCmd.Parameters.AddWithValue("@DisposedOn", adverseAward.DisposedOnDate ?? (object)DBNull.Value);
                            advCmd.Parameters.AddWithValue("@AppliedOn", adverseAward.CopyAppliedDate ?? (object)DBNull.Value);
                            advCmd.Parameters.AddWithValue("@CopyRemarks", adverseAward.CertifiedCopyRemarks ?? (object)DBNull.Value);
                            advCmd.Parameters.AddWithValue("@IssuedOn", adverseAward.CopyIssuedDate ?? (object)DBNull.Value);
                            advCmd.Parameters.AddWithValue("@ReceivedOn", adverseAward.CopyReceivedDate ?? (object)DBNull.Value);
                            advCmd.Parameters.AddWithValue("@CopyDelivered", adverseAward.CopyDeliveredDate ?? (object)DBNull.Value);
                            advCmd.Parameters.AddWithValue("@AwardAmount", adverseAward.AwardAmount ?? (object)DBNull.Value);
                            advCmd.Parameters.AddWithValue("@InterestRate", adverseAward.InterestRate ?? (object)DBNull.Value);
                            advCmd.Parameters.AddWithValue("@VictimAge", adverseAward.VictimAge ?? (object)DBNull.Value);
                            advCmd.Parameters.AddWithValue("@Occupation", adverseAward.Occupation ?? (object)DBNull.Value);
                            advCmd.Parameters.AddWithValue("@Income", adverseAward.IncomeConsidered ?? (object)DBNull.Value);
                            advCmd.Parameters.AddWithValue("@IncomePeriod", adverseAward.IncomePeriod ?? "monthly");
                            advCmd.Parameters.AddWithValue("@InjuryType", adverseAward.InjuryType ?? (object)DBNull.Value);
                            advCmd.Parameters.AddWithValue("@Injury", adverseAward.InjuryDetails ?? (object)DBNull.Value);
                            advCmd.Parameters.AddWithValue("@PunishmentStatus", adverseAward.DriverPunishmentStatus ?? (object)DBNull.Value);
                            advCmd.Parameters.AddWithValue("@PunishmentPath", adverseAward.PunishmentOrderUploadPath ?? (object)DBNull.Value);
                            advCmd.Parameters.AddWithValue("@PunishmentRemarks", adverseAward.PunishmentRemarks ?? (object)DBNull.Value);
                            advCmd.Parameters.AddWithValue("@DisabilityPercentage", adverseAward.DisabilityPercentage ?? (object)DBNull.Value);
                            advCmd.Parameters.AddWithValue("@InterimAmount", adverseAward.InterimCompAmount ?? (object)DBNull.Value);
                            advCmd.Parameters.AddWithValue("@InterimDeducted", adverseAward.InterimCompDeducted);
                            advCmd.Parameters.AddWithValue("@AdvOpinion", adverseAward.AdvocateOpinion ?? (object)DBNull.Value);
                            advCmd.Parameters.AddWithValue("@LOOpinion", adverseAward.LOOpinion ?? (object)DBNull.Value);
                            advCmd.Parameters.AddWithValue("@DCOpinion", adverseAward.DCOpinion ?? (object)DBNull.Value);
                            advCmd.Parameters.AddWithValue("@ForwardStatus", adverseAward.ForwardingStatus ?? (object)DBNull.Value);
                            advCmd.Parameters.AddWithValue("@ClosureRemarks", adverseAward.ClosureRemarks ?? (object)DBNull.Value);
                            advCmd.Parameters.AddWithValue("@ClosureDate", adverseAward.ClosureDate ?? (object)DBNull.Value);
                            advCmd.Parameters.AddWithValue("@OutwardNo", adverseAward.OutwardNumber ?? (object)DBNull.Value);
                            advCmd.Parameters.AddWithValue("@OutwardDate", adverseAward.OutwardDate ?? (object)DBNull.Value);
                            advCmd.Parameters.AddWithValue("@IsCorpLiable", adverseAward.IsCorpLiable);
                            advCmd.Parameters.AddWithValue("@LiabilityPercentage", adverseAward.LiabilityPercentage ?? (object)DBNull.Value);
                            advCmd.Parameters.AddWithValue("@LiabilityRemarks", adverseAward.LiabilityRemarks ?? (object)DBNull.Value);
                            advCmd.Parameters.AddWithValue("@AdverseJudgmentUploadPath", adverseAward.AdverseJudgmentUploadPath ?? (object)DBNull.Value);
                            advCmd.Parameters.AddWithValue("@FutureProspectus", adverseAward.FutureProspectus ?? (object)DBNull.Value);
                            advCmd.Parameters.AddWithValue("@TreatedDoc", adverseAward.TreatedDocFlag);
                            advCmd.Parameters.AddWithValue("@IsSTPassenger", model.IsSTPassenger);
                            advCmd.Parameters.AddWithValue("@IsMedicalExpensesPaid", model.IsMedicalExpensesPaid);
                            advCmd.Parameters.AddWithValue("@MedicalPaidAmount", model.MedicalPaidAmount ?? (object)DBNull.Value);
                            advCmd.Parameters.AddWithValue("@MedicalPaidRemarks", model.MedicalPaidRemarks ?? (object)DBNull.Value);
                            advCmd.Parameters.AddWithValue("@IsARFAmountPaid", model.IsARFAmountPaid);
                            advCmd.Parameters.AddWithValue("@ARFPaidAmount", model.ARFPaidAmount ?? (object)DBNull.Value);
                            advCmd.Parameters.AddWithValue("@ARFPaidRemarks", model.ARFPaidRemarks ?? (object)DBNull.Value);
                            advCmd.Parameters.AddWithValue("@IsDelayApplicationFiled", adverseAward.IsDelayApplicationFiled);
                            advCmd.Parameters.AddWithValue("@DelayApplicationPath", adverseAward.DelayApplicationPath ?? (object)DBNull.Value);
                            advCmd.Parameters.AddWithValue("@IsDelayCondonedAdverse", (object)adverseAward.IsDelayCondonedAdverse ?? DBNull.Value);
                            advCmd.Parameters.AddWithValue("@DelayCondonedOrderPath", adverseAward.DelayCondonedOrderPath ?? (object)DBNull.Value);
                            advCmd.Parameters.AddWithValue("@IsMedicalInsuranceClaimed", adverseAward.IsMedicalInsuranceClaimed);


                            advCmd.Parameters.AddWithValue("@IsFIRFiled", (object)adverseAward.IsFIRFiled ?? DBNull.Value);
                            advCmd.Parameters.AddWithValue("@IsChargeSheetFiled", (object)adverseAward.IsChargeSheetFiled ?? DBNull.Value);
                            advCmd.Parameters.AddWithValue("@IsBusCameraInstalled", (object)adverseAward.IsBusCameraInstalled ?? DBNull.Value);
                            advCmd.Parameters.AddWithValue("@IsCameraFootageProduced", (object)adverseAward.IsCameraFootageProduced ?? DBNull.Value);
                            advCmd.Parameters.AddWithValue("@IsPhotographProduced", (object)adverseAward.IsPhotographProduced ?? DBNull.Value);
                            advCmd.Parameters.AddWithValue("@PhotographNotProducedReason", (object)adverseAward.PhotographNotProducedReason ?? DBNull.Value);
                            advCmd.Parameters.AddWithValue("@PoliceSketchExhibitNo", (object)adverseAward.PoliceSketchExhibitNo ?? DBNull.Value);
                            advCmd.Parameters.AddWithValue("@IsPoliceSketchEnclosed", (object)adverseAward.IsPoliceSketchEnclosed ?? DBNull.Value);
                            advCmd.Parameters.AddWithValue("@IsEvidenceBasedOnSecurityReport", (object)adverseAward.IsEvidenceBasedOnSecurityReport ?? DBNull.Value);
                            advCmd.Parameters.AddWithValue("@SecurityReportNoEvidenceReason", (object)adverseAward.SecurityReportNoEvidenceReason ?? DBNull.Value);
                            advCmd.Parameters.AddWithValue("@IsImpleadingAppFiled", (object)adverseAward.IsImpleadingAppFiled ?? DBNull.Value);
                            advCmd.Parameters.AddWithValue("@ImpleadingAppNotFiledReason", (object)adverseAward.ImpleadingAppNotFiledReason ?? DBNull.Value);
                            advCmd.Parameters.AddWithValue("@IsVictimSalaried", (object)adverseAward.IsVictimSalaried ?? DBNull.Value);
                            advCmd.Parameters.AddWithValue("@IsIncomeCrossVerified", (object)adverseAward.IsIncomeCrossVerified ?? DBNull.Value);
                            advCmd.Parameters.AddWithValue("@DoesIncomeTallyWithDocuments", (object)adverseAward.DoesIncomeTallyWithDocuments ?? DBNull.Value);
                            advCmd.Parameters.AddWithValue("@IsMedicalBillsVerified", (object)adverseAward.IsMedicalBillsVerified ?? DBNull.Value);
                            advCmd.Parameters.AddWithValue("@IsAmountDepositedInEP", (object)adverseAward.IsAmountDepositedInEP ?? DBNull.Value);
                            advCmd.Parameters.AddWithValue("@IsEPFiled", (object)adverseAward.IsEPFiled ?? DBNull.Value);
                            advCmd.Parameters.AddWithValue("@EPDepositedAmount", (object)adverseAward.EPDepositedAmount ?? DBNull.Value);
                            advCmd.Parameters.AddWithValue("@FutureProspectsPercentage", (object)adverseAward.FutureProspectsPercentage ?? DBNull.Value);
                            advCmd.Parameters.AddWithValue("@PersonalExpensesDeduction", (object)adverseAward.PersonalExpensesDeduction ?? DBNull.Value);
                            advCmd.Parameters.AddWithValue("@Multiplier", (object)adverseAward.Multiplier ?? DBNull.Value);
                            advCmd.Parameters.AddWithValue("@LossOfDependency", (object)adverseAward.LossOfDependency ?? DBNull.Value);
                            advCmd.Parameters.AddWithValue("@LossOfConsortium", (object)adverseAward.LossOfConsortium ?? DBNull.Value);
                            advCmd.Parameters.AddWithValue("@LossOfEstate", (object)adverseAward.LossOfEstate ?? DBNull.Value);
                            advCmd.Parameters.AddWithValue("@FuneralExpenses", (object)adverseAward.FuneralExpenses ?? DBNull.Value);
                            advCmd.Parameters.AddWithValue("@LossOfLoveAffection", (object)adverseAward.LossOfLoveAffection ?? DBNull.Value);
                             advCmd.Parameters.AddWithValue("@MedicalExpenseOther", (object)adverseAward.MedicalExpenseOther ?? DBNull.Value);
                             advCmd.Parameters.AddWithValue("@AgeProofPath", adverseAward.AgeProofUploadPath ?? (object)DBNull.Value);
                             advCmd.Parameters.AddWithValue("@PainSufferings", (object)adverseAward.PainSufferings ?? DBNull.Value);
                             advCmd.Parameters.AddWithValue("@ConveyanceAttendant", (object)adverseAward.ConveyanceAttendant ?? DBNull.Value);
                             advCmd.Parameters.AddWithValue("@LossOfFutureIncome", (object)adverseAward.LossOfFutureIncome ?? DBNull.Value);
                             advCmd.Parameters.AddWithValue("@LossOfIncomeLaidUp", (object)adverseAward.LossOfIncomeLaidUp ?? DBNull.Value);
                             advCmd.Parameters.AddWithValue("@LossOfAmenities", (object)adverseAward.LossOfAmenities ?? DBNull.Value);
                             advCmd.Parameters.AddWithValue("@FutureMedicalExpenses", (object)adverseAward.FutureMedicalExpenses ?? DBNull.Value);
                             advCmd.Parameters.AddWithValue("@InjuryOtherExpense", (object)adverseAward.InjuryOtherExpense ?? DBNull.Value);
                             advCmd.Parameters.AddWithValue("@EPNumber", (object)adverseAward.EPNumber ?? DBNull.Value);
                             advCmd.Parameters.AddWithValue("@EPCourt", (object)adverseAward.EPCourt ?? DBNull.Value);
                             advCmd.Parameters.AddWithValue("@EPStage", (object)adverseAward.EPStage ?? DBNull.Value);
                             advCmd.Parameters.AddWithValue("@EPNextHearingDate", (object)adverseAward.EPNextHearingDate ?? DBNull.Value);
                             advCmd.Parameters.AddWithValue("@RoundOffAmount", (object)adverseAward.RoundOffAmount ?? DBNull.Value);
                             advCmd.Parameters.AddWithValue("@AsPerECourts", (object)adverseAward.AsPerECourts ?? DBNull.Value);
                             
                             if (adverseAward.CustomCompensationHeads != null && adverseAward.CustomCompensationHeads.Count > 0)
                                 advCmd.Parameters.AddWithValue("@CustomCompensation", JsonSerializer.Serialize(adverseAward.CustomCompensationHeads));
                             else
                                 advCmd.Parameters.AddWithValue("@CustomCompensation", DBNull.Value);



                            advCmd.ExecuteNonQuery();

                            if (model.DisposalResult == "Against" && model.AdverseAward != null)
                            {
                                // PW Lists
                                foreach (var pw in adverseAward.PetitionerPWNames)
                                {
                                    var pwCmd = new SqlCommand("INSERT INTO MVC_CASE_ADVERSE_PW (CaseID, PWName, Type) VALUES (@CaseID, @Name, 'Petitioner')", conn, trans);
                                pwCmd.Parameters.AddWithValue("@CaseID", caseId);
                                pwCmd.Parameters.AddWithValue("@Name", pw);
                                pwCmd.ExecuteNonQuery();
                            }
                            foreach (var dr in adverseAward.Doctors)
                            {
                                var pwCmd = new SqlCommand("INSERT INTO MVC_CASE_ADVERSE_PW (CaseID, PWName, Type, IsTreated) VALUES (@CaseID, @Name, 'Doctor', @IsTreated)", conn, trans);
                                pwCmd.Parameters.AddWithValue("@CaseID", caseId);
                                pwCmd.Parameters.AddWithValue("@Name", dr.DoctorName);
                                pwCmd.Parameters.AddWithValue("@IsTreated", dr.IsTreated);
                                pwCmd.ExecuteNonQuery();
                            }
                            // RW
                            foreach (var rw in adverseAward.RWNames)
                            {
                                var rwCmd = new SqlCommand("INSERT INTO MVC_CASE_ADVERSE_RW (CaseID, RWName) VALUES (@CaseID, @Name)", conn, trans);
                                rwCmd.Parameters.AddWithValue("@CaseID", caseId);
                                rwCmd.Parameters.AddWithValue("@Name", rw);
                                rwCmd.ExecuteNonQuery();
                            }
                            // Adverse Connected
                            foreach (var advConn in adverseAward.ConnectedCases)
                            {
                                 var acCmd = new SqlCommand(@"INSERT INTO MVC_CASE_ADVERSE_CONNECTED 
                                    (CaseID, CaseDetails, Status, CurrentStage, AwardAmount, InterestRate, VictimAge, Occupation, IncomeConsidered, InjuryDetails) 
                                    VALUES (@CaseID, @Details, @Status, @CurrentStage, @AwardAmount, @InterestRate, @VictimAge, @Occupation, @IncomeConsidered, @InjuryDetails)", conn, trans);
                                acCmd.Parameters.AddWithValue("@CaseID", caseId);
                                string details = $"{advConn.ConnectedMVCNo} / {advConn.ConnectedYear}";
                                if (!string.IsNullOrEmpty(advConn.MACT)) details += $" - {advConn.MACT}";
                                acCmd.Parameters.AddWithValue("@Details", details);
                                acCmd.Parameters.AddWithValue("@Status", advConn.Status ?? (object)DBNull.Value);
                                acCmd.Parameters.AddWithValue("@CurrentStage", advConn.CurrentStage ?? (object)DBNull.Value);
                                
                                // Award Details
                                acCmd.Parameters.AddWithValue("@AwardAmount", advConn.AwardDetails.AwardAmount ?? (object)DBNull.Value);
                                acCmd.Parameters.AddWithValue("@InterestRate", advConn.AwardDetails.InterestRate ?? (object)DBNull.Value);
                                acCmd.Parameters.AddWithValue("@VictimAge", advConn.AwardDetails.VictimAge ?? (object)DBNull.Value);
                                acCmd.Parameters.AddWithValue("@Occupation", advConn.AwardDetails.Occupation ?? (object)DBNull.Value);
                                acCmd.Parameters.AddWithValue("@IncomeConsidered", advConn.AwardDetails.IncomeConsidered ?? (object)DBNull.Value);
                                acCmd.Parameters.AddWithValue("@InjuryDetails", advConn.AwardDetails.InjuryDetails ?? (object)DBNull.Value);

                                acCmd.ExecuteNonQuery();
                            }
                        }

                        // 6. Save Third Party Evidence (Respondent & Corporation)
                        if (model.ThirdPartyFlag)
                        {
                            foreach (var ev in model.RespondentEvidence)
                            {
                                if (!string.IsNullOrEmpty(ev.Name))
                                {
                                    var evCmd = new SqlCommand("INSERT INTO MVC_CASE_ADVERSE_PW (CaseID, PWName, Type, Remark) VALUES (@CaseID, @Name, 'TP_Respondent', @Remark)", conn, trans);
                                    evCmd.Parameters.AddWithValue("@CaseID", caseId);
                                    evCmd.Parameters.AddWithValue("@Name", ev.Name);
                                    evCmd.Parameters.AddWithValue("@Remark", ev.Remark ?? (object)DBNull.Value);
                                    evCmd.ExecuteNonQuery();
                                }
                            }
                            foreach (var ev in model.CorpEvidence)
                            {
                                if (!string.IsNullOrEmpty(ev.Name))
                                {
                                    var evCmd = new SqlCommand("INSERT INTO MVC_CASE_ADVERSE_PW (CaseID, PWName, Type, Designation) VALUES (@CaseID, @Name, 'TP_Corporation', @Designation)", conn, trans);
                                    evCmd.Parameters.AddWithValue("@CaseID", caseId);
                                    evCmd.Parameters.AddWithValue("@Name", ev.Name);
                                    evCmd.Parameters.AddWithValue("@Designation", ev.Designation ?? (object)DBNull.Value);
                                    evCmd.ExecuteNonQuery();
                                }
                            }
                        }

                        trans.Commit();
                        return caseId;
                    }
                    catch (Exception)
                    {
                        trans.Rollback();
                        throw;
                    }
                }
            }
        }

        public MVCCaseViewModel? GetCaseByMvcDetails(int divisionId, string mvcNo, int mvcYear, int mactId)
        {
            // Similar logic to GetCaseById but query by MVC details
            string query = @"SELECT CaseID FROM MVC_CASES 
                             WHERE DivisionID = @DivisionID AND MVCNo = @MVCNo AND MVCYear = @MVCYear AND MACTID = @MACTID";
            
            var parameters = new[] {
                new SqlParameter("@DivisionID", divisionId),
                new SqlParameter("@MVCNo", mvcNo),
                new SqlParameter("@MVCYear", mvcYear),
                new SqlParameter("@MACTID", mactId)
            };

            int caseId = 0;
            var result = _db.ExecuteScalar(query, parameters);
            if (result != null && result != DBNull.Value)
            {
                caseId = Convert.ToInt32(result);
            }


            if (caseId > 0) return GetCaseById(caseId);
            return null;
        }
        public MVCCaseViewModel? GetCaseByMVCDetails(string mvcNo, int mvcYear, int mactId)
        {
             string query = @"SELECT CaseID FROM MVC_CASES 
                             WHERE MVCNo = @MVCNo AND MVCYear = @MVCYear AND MACTID = @MACTID";
            
            var parameters = new[] {
                new SqlParameter("@MVCNo", mvcNo),
                new SqlParameter("@MVCYear", mvcYear),
                new SqlParameter("@MACTID", mactId)
            };

            int caseId = 0;
            var result = _db.ExecuteScalar(query, parameters);
            if (result != null && result != DBNull.Value)
            {
                caseId = Convert.ToInt32(result);
            }

            if (caseId > 0) return GetCaseById(caseId);
            return null;
        }

        public MVCCaseViewModel? GetCaseById(int caseId)
        {
            var model = new MVCCaseViewModel();
            string query = @"
                SELECT c.*, d.DivisionNameEnglish as DivisionName, m.MACTName, 
                COALESCE(c.AdvocateName, a.AdvocateName) as AdvocateName,
                td.DivisionNameEnglish as TransferredFromDivisionName
                FROM MVC_CASES c
                LEFT JOIN DIVISION_MASTER d ON c.DivisionID = d.DivisionID
                LEFT JOIN MACT_MASTER m ON c.MACTID = m.MACTID
                LEFT JOIN ADVOCATE_MASTER a ON c.AdvocateID = a.AdvocateID
                LEFT JOIN DIVISION_MASTER td ON c.TransferredFromDivisionID = td.DivisionID
                WHERE c.CaseID = @CaseID";

            DataTable dt = _db.ExecuteQuery(query, new[] { new SqlParameter("@CaseID", caseId) });
            if (dt.Rows.Count == 0) return null;

            DataRow row = dt.Rows[0];
            // ... Mapping logic for main model ...
            model.CaseID = row["CaseID"] != DBNull.Value ? Convert.ToInt32(row["CaseID"]) : 0;
            model.DivisionID = row["DivisionID"] != DBNull.Value ? Convert.ToInt32(row["DivisionID"]) : 0;
            model.DivisionName = row["DivisionName"]?.ToString() ?? "";
            model.MVCNo = row["MVCNo"]?.ToString() ?? "";
            model.MVCYear = row["MVCYear"] != DBNull.Value ? Convert.ToInt32(row["MVCYear"]) : 0;
            model.MACTID = row["MACTID"] != DBNull.Value ? Convert.ToInt32(row["MACTID"]) : 0;
            model.MACTName = row["MACTName"]?.ToString() ?? "";
            model.VehicleNo = row["VehicleNo"]?.ToString();
            model.VehicleType = row["VehicleType"]?.ToString() ?? "Corporation";
            model.PetitionFiledFor = row.Table.Columns.Contains("PetitionFiledFor") ? row["PetitionFiledFor"]?.ToString() : null;
            model.AccidentDate = row["AccidentDate"] != DBNull.Value ? Convert.ToDateTime(row["AccidentDate"]) : null;
            model.CaseType = row["CaseType"]?.ToString() ?? "Pending";
            model.StatusID = row["StatusID"] != DBNull.Value ? Convert.ToInt32(row["StatusID"]) : 1;
            model.ClaimType = row["ClaimType"]?.ToString();
            model.ThirdPartyFlag = row["ThirdPartyFlag"] != DBNull.Value && (bool)row["ThirdPartyFlag"];
            model.ClaimAmount = row["ClaimAmount"] != DBNull.Value ? Convert.ToDecimal(row["ClaimAmount"]) : null;
            model.AdvocateID = row["AdvocateID"] != DBNull.Value ? Convert.ToInt32(row["AdvocateID"]) : null;
            model.AdvocateName = row["AdvocateName"]?.ToString();
            model.EntrustmentNo = row["EntrustmentNo"]?.ToString();
            model.EntrustmentDate = row["EntrustmentDate"] != DBNull.Value ? Convert.ToDateTime(row["EntrustmentDate"]) : null;

            model.DoubleClaimFlag = row["DoubleClaimFlag"] != DBNull.Value && (bool)row["DoubleClaimFlag"];
            model.CNRNumber = row.Table.Columns.Contains("CNRNumber") ? row["CNRNumber"]?.ToString() : null;
            model.EstCode = row.Table.Columns.Contains("EstCode") ? row["EstCode"]?.ToString() : null;
            model.CaseTypeCode = row.Table.Columns.Contains("CaseTypeCode") ? row["CaseTypeCode"]?.ToString() : null;
            model.CaseStatus = row.Table.Columns.Contains("CaseStatus") ? row["CaseStatus"]?.ToString() : null;
            model.CourtHall = row.Table.Columns.Contains("CourtHall") ? row["CourtHall"]?.ToString() : null;
            model.IsPendingForFiling = row.Table.Columns.Contains("IsPendingForFiling") && row["IsPendingForFiling"] != DBNull.Value && (bool)row["IsPendingForFiling"];
            model.ModifiedDate = row.Table.Columns.Contains("ModifiedDate") && row["ModifiedDate"] != DBNull.Value ? (DateTime?)Convert.ToDateTime(row["ModifiedDate"]) : null;
            model.ModifiedBy = row.Table.Columns.Contains("ModifiedBy") && row["ModifiedBy"] != DBNull.Value ? (int?)Convert.ToInt32(row["ModifiedBy"]) : null;
            model.NextHearingDate = row["NextHearingDate"] != DBNull.Value ? Convert.ToDateTime(row["NextHearingDate"]) : null;
            model.CurrentStage = row["CurrentStage"]?.ToString();
            model.DisposalStatus = row["DisposalStatus"]?.ToString();
            model.DisposalResult = row["DisposalResult"]?.ToString();
            model.DisposalRemarks = row["DisposalRemarks"]?.ToString();
            model.IsDocumentSent = row["IsDocumentSent"] != DBNull.Value && (bool)row["IsDocumentSent"];
            model.DocumentOutwardNo = row["DocumentOutwardNo"]?.ToString();
            model.DocumentOutwardDate = row["DocumentOutwardDate"] != DBNull.Value ? Convert.ToDateTime(row["DocumentOutwardDate"]) : null;
            model.IsObjectionFiled = row["IsObjectionFiled"] != DBNull.Value && (bool)row["IsObjectionFiled"];
            model.ObjectionFiledDate = row["ObjectionFiledDate"] != DBNull.Value ? Convert.ToDateTime(row["ObjectionFiledDate"]) : null;
            model.ObjectionOutwardNo = row["ObjectionOutwardNo"]?.ToString();
            model.ClosureDate = row["ClosureDate"] != DBNull.Value ? Convert.ToDateTime(row["ClosureDate"]) : null;
            model.IsEvidenceFiled = row["IsEvidenceFiled"] != DBNull.Value && (bool)row["IsEvidenceFiled"];
            model.IsWithinLimitation = row["IsWithinLimitation"] != DBNull.Value && (bool)row["IsWithinLimitation"];
            model.LimitationRemark = row["LimitationRemark"]?.ToString();
            model.IsDelayCondoned = row["IsDelayCondoned"] != DBNull.Value ? (bool?)Convert.ToBoolean(row["IsDelayCondoned"]) : null;
            model.DelayRemark = row["DelayRemark"]?.ToString();
            
            model.IsSTPassenger = row.Table.Columns.Contains("IsSTPassenger") && row["IsSTPassenger"] != DBNull.Value && (bool)row["IsSTPassenger"];
            model.IsMedicalExpensesPaid = row.Table.Columns.Contains("IsMedicalExpensesPaid") && row["IsMedicalExpensesPaid"] != DBNull.Value && (bool)row["IsMedicalExpensesPaid"];
            model.MedicalPaidAmount = row.Table.Columns.Contains("MedicalPaidAmount") && row["MedicalPaidAmount"] != DBNull.Value ? Convert.ToDecimal(row["MedicalPaidAmount"]) : null;
            model.MedicalPaidRemarks = row.Table.Columns.Contains("MedicalPaidRemarks") ? row["MedicalPaidRemarks"]?.ToString() : null;
            model.IsARFAmountPaid = row.Table.Columns.Contains("IsARFAmountPaid") && row["IsARFAmountPaid"] != DBNull.Value && (bool)row["IsARFAmountPaid"];
            model.ARFPaidAmount = row.Table.Columns.Contains("ARFPaidAmount") && row["ARFPaidAmount"] != DBNull.Value ? Convert.ToDecimal(row["ARFPaidAmount"]) : null;
            model.ARFPaidRemarks = row.Table.Columns.Contains("ARFPaidRemarks") ? row["ARFPaidRemarks"]?.ToString() : null;
            model.IsOppositeVehicleInmate = row.Table.Columns.Contains("IsOppositeVehicleInmate") && row["IsOppositeVehicleInmate"] != DBNull.Value && (bool)row["IsOppositeVehicleInmate"];
            model.ClaimRemark = row.Table.Columns.Contains("ClaimRemark") ? row["ClaimRemark"]?.ToString() : null;
            model.ClaimPetitionDate = row.Table.Columns.Contains("ClaimPetitionDate") && row["ClaimPetitionDate"] != DBNull.Value ? Convert.ToDateTime(row["ClaimPetitionDate"]) : null;
            model.HasInterimOrder = row.Table.Columns.Contains("HasInterimOrder") && row["HasInterimOrder"] != DBNull.Value && (bool)row["HasInterimOrder"];
            model.InterimOrderFilePath = row.Table.Columns.Contains("InterimOrderFilePath") ? row["InterimOrderFilePath"]?.ToString() : null;

            model.CreatedAt = row["CreatedAt"] != DBNull.Value ? Convert.ToDateTime(row["CreatedAt"]) : DateTime.MinValue;

            // Transfer Fields
            model.TransferredFromDivisionID = row.Table.Columns.Contains("TransferredFromDivisionID") && row["TransferredFromDivisionID"] != DBNull.Value ? (int?)Convert.ToInt32(row["TransferredFromDivisionID"]) : null;
            model.TransferredFromDivisionName = row.Table.Columns.Contains("TransferredFromDivisionName") ? row["TransferredFromDivisionName"]?.ToString() : null;
            model.TransferDate = row.Table.Columns.Contains("TransferDate") && row["TransferDate"] != DBNull.Value ? Convert.ToDateTime(row["TransferDate"]) : null;
            model.IsTransferViewed = row.Table.Columns.Contains("IsTransferViewed") && row["IsTransferViewed"] != DBNull.Value && (bool)row["IsTransferViewed"];

            // 2. Fetch Petitioners
            string petQuery = "SELECT * FROM MVC_CASE_PETITIONERS WHERE CaseID = @CaseID";
            DataTable petDt = _db.ExecuteQuery(petQuery, new[] { new SqlParameter("@CaseID", caseId) });
            foreach (DataRow r in petDt.Rows)
            {
                string? rel = r["Relationship"]?.ToString();
                var standardRelationships = new List<string> { "Self", "Father", "Mother", "Son", "Daughter", "Husband", "Wife", "Brother", "Sister", "Father-in-law", "Mother-in-law", "Brother-in-law", "Sister-in-law", "Son-in-law", "Daughter-in-law", "Guardian" };
                bool isStandard = string.IsNullOrEmpty(rel) || standardRelationships.Contains(rel);

                model.Petitioners.Add(new PetitionerViewModel
                {
                    PetitionerName = r["PetitionerName"].ToString()!,
                    Relationship = isStandard ? rel : "Others",
                    RelationshipOthers = isStandard ? null : rel
                });
            }

            // 2.1 Fetch Respondents (Third Party)
            string respQuery = "SELECT * FROM MVC_CASE_RESPONDENTS WHERE CaseID = @CaseID";
            DataTable respDt = _db.ExecuteQuery(respQuery, new[] { new SqlParameter("@CaseID", caseId) });
            foreach (DataRow r in respDt.Rows)
            {
                model.Respondents.Add(new RespondentViewModel
                {
                    RespondentName = r["RespondentName"].ToString()!,
                    Remarks = r["Remarks"]?.ToString()
                });
            }

            // 3. Fetch Connected Cases (Section E)
            string connQuery = @"
                SELECT cc.*, c.CaseID as ConnectedCaseID, c.CurrentStage, c.CNRNumber as FetchedCNR, m.MACTName as FetchedMACT
                FROM MVC_CASE_CONNECTED cc
                LEFT JOIN MVC_CASES c ON cc.ConnectedMVCNo = c.MVCNo AND cc.ConnectedYear = c.MVCYear
                LEFT JOIN MACT_MASTER m ON c.MACTID = m.MACTID
                WHERE cc.CaseID = @CaseID";
            DataTable connDt = _db.ExecuteQuery(connQuery, new[] { new SqlParameter("@CaseID", caseId) });
            
            // Track added to prevent bidirectional duplicates
            var addedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (DataRow r in connDt.Rows)
            {
                string mvc = r["ConnectedMVCNo"]?.ToString() ?? "";
                string year = r["ConnectedYear"]?.ToString() ?? "";
                if (string.IsNullOrEmpty(mvc) || string.IsNullOrEmpty(year)) continue;

                string key = $"{mvc.Trim().ToLower()}-{year.Trim().ToLower()}";
                if (!addedKeys.Contains(key))
                {
                    addedKeys.Add(key);
                    model.ConnectedCases.Add(new ConnectedCaseViewModel
                    {
                        ConnectedCaseID = r["ConnectedCaseID"] != DBNull.Value ? (int?)Convert.ToInt32(r["ConnectedCaseID"]) : null,
                        ConnectedMVCNo = mvc,
                        ConnectedYear = r["ConnectedYear"] != DBNull.Value ? Convert.ToInt32(r["ConnectedYear"]) : 0,
                        MACT = r["FetchedMACT"]?.ToString() ?? r["MACT"]?.ToString() ?? "",
                        CurrentStage = r["CurrentStage"]?.ToString() ?? "",
                        Remarks = r["Remarks"]?.ToString(),
                        IsDoubleClaim = r["IsDoubleClaim"] != DBNull.Value && (bool)r["IsDoubleClaim"],
                        ConnectedCNRNumber = r.Table.Columns.Contains("FetchedCNR") ? r["FetchedCNR"]?.ToString() : null
                    });
                }
            }

            // 3.1 Fetch Incoming Connected Cases (Bidirectional)
            string incomingConnQuery = @"
                SELECT c.CaseID, c.MVCNo, c.MVCYear, c.CNRNumber as FetchedCNR, m.MACTName as FetchedMACT, c.CurrentStage
                FROM MVC_CASE_CONNECTED cc
                JOIN MVC_CASES c ON cc.CaseID = c.CaseID
                LEFT JOIN MACT_MASTER m ON c.MACTID = m.MACTID
                WHERE cc.ConnectedMVCNo = @MVCNo AND cc.ConnectedYear = @MVCYear";

            DataTable incomingDt = _db.ExecuteQuery(incomingConnQuery, new[] { 
                new SqlParameter("@MVCNo", model.MVCNo),
                new SqlParameter("@MVCYear", model.MVCYear)
            });

            foreach (DataRow r in incomingDt.Rows)
            {
                string mvc = r["MVCNo"]?.ToString() ?? "";
                string year = r["MVCYear"]?.ToString() ?? "";
                if (string.IsNullOrEmpty(mvc) || string.IsNullOrEmpty(year)) continue;

                string key = $"{mvc.Trim().ToLower()}-{year.Trim().ToLower()}";
                if (!addedKeys.Contains(key))
                {
                    addedKeys.Add(key);
                    model.ConnectedCases.Add(new ConnectedCaseViewModel
                    {
                        ConnectedCaseID = Convert.ToInt32(r["CaseID"]),
                        ConnectedMVCNo = mvc,
                        ConnectedYear = Convert.ToInt32(r["MVCYear"]),
                        MACT = r["FetchedMACT"]?.ToString() ?? "",
                        CurrentStage = r["CurrentStage"]?.ToString() ?? "",
                        Remarks = "(Linked Parent Case)",
                        ConnectedCNRNumber = r.Table.Columns.Contains("FetchedCNR") ? r["FetchedCNR"]?.ToString() : null
                    });
                }
            }

            // 3.2 Fetch Automatic Connected Cases (By Vehicle & Accident Date)
            if (!string.IsNullOrEmpty(model.VehicleNo) && model.AccidentDate.HasValue)
            {
                try
                {
                    string autoQuery = @"
                        SELECT c.CaseID, c.MVCNo, c.MVCYear, c.CNRNumber as FetchedCNR, m.MACTName as FetchedMACT, c.CurrentStage
                        FROM MVC_CASES c
                        LEFT JOIN MACT_MASTER m ON c.MACTID = m.MACTID
                        WHERE c.VehicleNo = @VehicleNo
                        AND c.AccidentDate = @AccidentDate
                        AND c.CaseID <> @CaseID";

                    DataTable autoDt = _db.ExecuteQuery(autoQuery, new[] { 
                        new SqlParameter("@VehicleNo", model.VehicleNo),
                        new SqlParameter("@AccidentDate", model.AccidentDate.Value),
                        new SqlParameter("@CaseID", caseId)
                    });

                    foreach (DataRow r in autoDt.Rows)
                    {
                        string mvc = r["MVCNo"]?.ToString() ?? "";
                        string year = r["MVCYear"]?.ToString() ?? "";
                        if (string.IsNullOrEmpty(mvc) || string.IsNullOrEmpty(year)) continue;

                        string key = $"{mvc.Trim().ToLower()}-{year.Trim().ToLower()}";
                        if (!addedKeys.Contains(key))
                        {
                            addedKeys.Add(key);
                            model.ConnectedCases.Add(new ConnectedCaseViewModel
                            {
                                ConnectedCaseID = Convert.ToInt32(r["CaseID"]),
                                ConnectedMVCNo = mvc,
                                ConnectedYear = Convert.ToInt32(r["MVCYear"]),
                                MACT = r["FetchedMACT"]?.ToString() ?? "",
                                CurrentStage = r["CurrentStage"]?.ToString() ?? "",
                                Remarks = "(Same Vehicle & Accident Date)",
                                ConnectedCNRNumber = r.Table.Columns.Contains("FetchedCNR") ? r["FetchedCNR"]?.ToString() : null
                            });
                        }
                    }
                }
                catch
                {
                    // Non-critical auto-connected case lookup failure swallowed to prevent page load timeout
                }
            }
            
            // 3.3 Fetch Opposite Vehicle Numbers
            string oppVnoQuery = "SELECT VehicleNo FROM MVC_CASE_OPPOSITE_VEHICLES WHERE CaseID = @CaseID";
            DataTable oppVnoDt = _db.ExecuteQuery(oppVnoQuery, new[] { new SqlParameter("@CaseID", caseId) });
            foreach (DataRow r in oppVnoDt.Rows)
            {
                model.OppositeVehicleNumbers.Add(r["VehicleNo"]?.ToString() ?? "");
            }

            // 4. Fetch Adverse Details
            string advQuery = "SELECT * FROM MVC_CASE_ADVERSE_DETAILS WHERE CaseID = @CaseID";
            DataTable advDt = _db.ExecuteQuery(advQuery, new[] { new SqlParameter("@CaseID", caseId) });
            if (advDt.Rows.Count > 0)
            {
                DataRow ar = advDt.Rows[0];
                model.IsAllegedAccident = ar["IsAllegedAccident"] != DBNull.Value && (bool)ar["IsAllegedAccident"];
                model.AdverseAward = new AdverseAwardViewModel
                {
                   // 1. Adverse Basic
                   IsBusInsured = ar["IsBusInsured"] != DBNull.Value && (bool)ar["IsBusInsured"],
                   BusInsuranceDetails = ar["BusInsuranceDetails"]?.ToString(),
                   ClaimPetitionDate = ar["ClaimPetitionDate"] != DBNull.Value ? Convert.ToDateTime(ar["ClaimPetitionDate"]) : null,
                    AwardDate = ar["AwardDate"] != DBNull.Value ? Convert.ToDateTime(ar["AwardDate"]) : null,
                    TreatedDocFlag = ar["TreatedDocFlag"] != DBNull.Value && (bool)ar["TreatedDocFlag"],
                    MannerOfAccident = ar["MannerOfAccident"]?.ToString(),
                    MannerOfAccidentRO = ar["MannerOfAccidentRO"]?.ToString(),
                   ObjectionFiled = ar["ObjectionFiled"] != DBNull.Value && (bool)ar["ObjectionFiled"],
                   ObjectionRemarks = ar["ObjectionRemarks"]?.ToString(),
                   RWType = ar["RWType"]?.ToString(),
                   TR18Remarks = ar["TR18Remarks"]?.ToString(),
                   IsDeceasedInTR18 = ar["IsDeceasedInTR18"] != DBNull.Value && (bool)ar["IsDeceasedInTR18"],
                   TR18UploadPath = ar["TR18UploadPath"]?.ToString(),
                   IsAllegedAccident = ar["IsAllegedAccident"] != DBNull.Value && (bool)ar["IsAllegedAccident"],
                   DoubleClaimFlag = ar["AdverseDoubleClaimFlag"] != DBNull.Value && (bool)ar["AdverseDoubleClaimFlag"],
                   DoubleClaimDetails = ar["DoubleClaimDetails"]?.ToString(), 
                   SecurityRequired = ar["SecurityRequired"] != DBNull.Value && (bool)ar["SecurityRequired"],
                   SecurityUploadPath = ar["SecurityUploadPath"]?.ToString(),
                   GovIDProofUploadPath = ar["GovIDProofUploadPath"]?.ToString(),
                   DisposedOnDate = ar["DisposedOnDate"] != DBNull.Value ? Convert.ToDateTime(ar["DisposedOnDate"]) : null,
                   CopyAppliedDate = ar["CopyAppliedDate"] != DBNull.Value ? Convert.ToDateTime(ar["CopyAppliedDate"]) : null,
                   CertifiedCopyRemarks = ar["CertifiedCopyRemarks"]?.ToString(),
                   CopyIssuedDate = ar["CopyIssuedDate"] != DBNull.Value ? Convert.ToDateTime(ar["CopyIssuedDate"]) : null,
                   CopyReceivedDate = ar["CopyReceivedDate"] != DBNull.Value ? Convert.ToDateTime(ar["CopyReceivedDate"]) : null,
                   CopyDeliveredDate = ar["CopyDeliveredDate"] != DBNull.Value ? Convert.ToDateTime(ar["CopyDeliveredDate"]) : null,
                   AwardAmount = ar["AwardAmount"] != DBNull.Value ? Convert.ToDecimal(ar["AwardAmount"]) : null,
                   InterestRate = ar["InterestRate"] != DBNull.Value ? Convert.ToDecimal(ar["InterestRate"]) : null,
                   VictimAge = ar["VictimAge"] != DBNull.Value ? Convert.ToInt32(ar["VictimAge"]) : null,
                   Occupation = ar["Occupation"]?.ToString(),
                   IncomeConsidered = ar["IncomeConsidered"] != DBNull.Value ? Convert.ToDecimal(ar["IncomeConsidered"]) : null,
                    IncomePeriod = ar["IncomePeriod"]?.ToString() ?? "monthly",
                   InjuryType = ar["InjuryType"]?.ToString(),
                   InjuryDetails = ar["InjuryDetails"]?.ToString(),
                   DriverPunishmentStatus = ar["DriverPunishmentStatus"]?.ToString(),
                   PunishmentOrderUploadPath = ar["PunishmentOrderUploadPath"]?.ToString(),
                    PunishmentRemarks = ar["PunishmentRemarks"]?.ToString(),
                    DisabilityPercentage = ar["DisabilityPercentage"] != DBNull.Value ? (decimal?)ar["DisabilityPercentage"] : null,
                   InterimCompAmount = ar["InterimCompAmount"] != DBNull.Value ? Convert.ToDecimal(ar["InterimCompAmount"]) : null,
                   InterimCompDeducted = ar["InterimCompDeducted"] != DBNull.Value && (bool)ar["InterimCompDeducted"],
                   AdvocateOpinion = ar["AdvocateOpinion"]?.ToString(),
                   LOOpinion = ar["LOOpinion"]?.ToString(),
                   DCOpinion = ar["DCOpinion"]?.ToString(),
                   ForwardingStatus = ar["ForwardingStatus"]?.ToString(),
                   ClosureRemarks = ar["ClosureRemarks"]?.ToString(),
                   ClosureDate = ar["ClosureDate"] != DBNull.Value ? Convert.ToDateTime(ar["ClosureDate"]) : null,
                   OutwardNumber = ar["OutwardNumber"]?.ToString(),
                   OutwardDate = ar["OutwardDate"] != DBNull.Value ? Convert.ToDateTime(ar["OutwardDate"]) : null,
                   IsCorpLiable = ar["IsCorpLiable"] != DBNull.Value && (bool)ar["IsCorpLiable"],
                   LiabilityPercentage = ar["LiabilityPercentage"] != DBNull.Value ? Convert.ToDecimal(ar["LiabilityPercentage"]) : null,
                   LiabilityRemarks = ar["LiabilityRemarks"]?.ToString(),
                   LiableAmount = (ar["AwardAmount"] != DBNull.Value && ar["LiabilityPercentage"] != DBNull.Value) ? 
                        (Convert.ToDecimal(ar["AwardAmount"]) * Convert.ToDecimal(ar["LiabilityPercentage"]) / 100) : null,
                   AdverseJudgmentUploadPath = ar["AdverseJudgmentUploadPath"]?.ToString(),
                   FutureProspectus = ar["FutureProspectus"]?.ToString(),
                   IsDelayApplicationFiled = ar["IsDelayApplicationFiled"] != DBNull.Value && (bool)ar["IsDelayApplicationFiled"],
                   DelayApplicationPath = ar["DelayApplicationPath"]?.ToString(),
                   IsDelayCondonedAdverse = ar["IsDelayCondonedAdverse"] != DBNull.Value ? (bool?)ar["IsDelayCondonedAdverse"] : null,
                   DelayCondonedOrderPath = ar["DelayCondonedOrderPath"]?.ToString(),
                    IsMedicalInsuranceClaimed = ar["IsMedicalInsuranceClaimed"] != DBNull.Value && (bool)ar["IsMedicalInsuranceClaimed"],

                   IsFIRFiled = ar["IsFIRFiled"] != DBNull.Value ? (bool?)ar["IsFIRFiled"] : null,
                   IsChargeSheetFiled = ar["IsChargeSheetFiled"] != DBNull.Value ? (bool?)ar["IsChargeSheetFiled"] : null,
                   IsBusCameraInstalled = ar["IsBusCameraInstalled"] != DBNull.Value ? (bool?)ar["IsBusCameraInstalled"] : null,
                   IsCameraFootageProduced = ar["IsCameraFootageProduced"] != DBNull.Value ? (bool?)ar["IsCameraFootageProduced"] : null,
                   IsPhotographProduced = ar["IsPhotographProduced"] != DBNull.Value ? (bool?)ar["IsPhotographProduced"] : null,
                   PhotographNotProducedReason = ar["PhotographNotProducedReason"]?.ToString(),
                   PoliceSketchExhibitNo = ar["PoliceSketchExhibitNo"]?.ToString(),
                   IsPoliceSketchEnclosed = ar["IsPoliceSketchEnclosed"] != DBNull.Value ? (bool?)ar["IsPoliceSketchEnclosed"] : null,
                   IsEvidenceBasedOnSecurityReport = ar["IsEvidenceBasedOnSecurityReport"] != DBNull.Value ? (bool?)ar["IsEvidenceBasedOnSecurityReport"] : null,
                   SecurityReportNoEvidenceReason = ar["SecurityReportNoEvidenceReason"]?.ToString(),
                   IsImpleadingAppFiled = ar["IsImpleadingAppFiled"] != DBNull.Value ? (bool?)ar["IsImpleadingAppFiled"] : null,
                   ImpleadingAppNotFiledReason = ar["ImpleadingAppNotFiledReason"]?.ToString(),
                   IsVictimSalaried = ar["IsVictimSalaried"] != DBNull.Value ? (bool?)ar["IsVictimSalaried"] : null,
                   IsIncomeCrossVerified = ar["IsIncomeCrossVerified"] != DBNull.Value ? (bool?)ar["IsIncomeCrossVerified"] : null,
                   DoesIncomeTallyWithDocuments = ar["DoesIncomeTallyWithDocuments"] != DBNull.Value ? (bool?)ar["DoesIncomeTallyWithDocuments"] : null,
                   IsMedicalBillsVerified = ar["IsMedicalBillsVerified"] != DBNull.Value ? (bool?)ar["IsMedicalBillsVerified"] : null,
                   IsAmountDepositedInEP = ar["IsAmountDepositedInEP"] != DBNull.Value && (bool)ar["IsAmountDepositedInEP"],
                   IsEPFiled = ar["IsEPFiled"] != DBNull.Value && (bool)ar["IsEPFiled"],
                   EPDepositedAmount = ar["EPDepositedAmount"] != DBNull.Value ? (decimal?)ar["EPDepositedAmount"] : null,
                   FutureProspectsPercentage = ar["FutureProspectsPercentage"] != DBNull.Value ? (decimal?)ar["FutureProspectsPercentage"] : null,
                   PersonalExpensesDeduction = ar["PersonalExpensesDeduction"] != DBNull.Value ? (decimal?)ar["PersonalExpensesDeduction"] : null,
                   Multiplier = ar["Multiplier"] != DBNull.Value ? (decimal?)ar["Multiplier"] : null,
                   LossOfDependency = ar["LossOfDependency"] != DBNull.Value ? (decimal?)ar["LossOfDependency"] : null,
                   LossOfConsortium = ar["LossOfConsortium"] != DBNull.Value ? (decimal?)ar["LossOfConsortium"] : null,
                   LossOfEstate = ar["LossOfEstate"] != DBNull.Value ? (decimal?)ar["LossOfEstate"] : null,
                    FuneralExpenses = ar["FuneralExpenses"] != DBNull.Value ? (decimal?)ar["FuneralExpenses"] : null,
                    LossOfLoveAffection = ar["LossOfLoveAffection"] != DBNull.Value ? (decimal?)ar["LossOfLoveAffection"] : null,
                    MedicalExpenseOther = ar["MedicalExpenseOther"] != DBNull.Value ? (decimal?)ar["MedicalExpenseOther"] : null,
                     AgeProofUploadPath = ar.Table.Columns.Contains("AgeProofUploadPath") ? ar["AgeProofUploadPath"]?.ToString() : null,
                    PainSufferings = ar["PainSufferings"] != DBNull.Value ? (decimal?)ar["PainSufferings"] : null,
                    ConveyanceAttendant = ar["ConveyanceAttendant"] != DBNull.Value ? (decimal?)ar["ConveyanceAttendant"] : null,
                    LossOfFutureIncome = ar["LossOfFutureIncome"] != DBNull.Value ? (decimal?)ar["LossOfFutureIncome"] : null,
                    LossOfIncomeLaidUp = ar["LossOfIncomeLaidUp"] != DBNull.Value ? (decimal?)ar["LossOfIncomeLaidUp"] : null,
                    LossOfAmenities = ar["LossOfAmenities"] != DBNull.Value ? (decimal?)ar["LossOfAmenities"] : null,
                    FutureMedicalExpenses = ar["FutureMedicalExpenses"] != DBNull.Value ? (decimal?)ar["FutureMedicalExpenses"] : null,
                    InjuryOtherExpense = ar["InjuryOtherExpense"] != DBNull.Value ? (decimal?)ar["InjuryOtherExpense"] : null,
                    EPNumber = ar["EPNumber"]?.ToString(),
                    EPCourt = ar["EPCourt"]?.ToString(),
                    EPStage = ar["EPStage"]?.ToString(),
                    EPNextHearingDate = ar["EPNextHearingDate"] != DBNull.Value ? (DateTime?)ar["EPNextHearingDate"] : null,
                     RoundOffAmount = ar.Table.Columns.Contains("RoundOffAmount") && ar["RoundOffAmount"] != DBNull.Value ? (decimal?)ar["RoundOffAmount"] : null,
                     AsPerECourts = ar.Table.Columns.Contains("AsPerECourts") ? ar["AsPerECourts"]?.ToString() : null
                };
                
                if (ar.Table.Columns.Contains("CustomCompensation") && ar["CustomCompensation"] != DBNull.Value)
                {
                    try {
                        var parsed = JsonSerializer.Deserialize<List<CustomCompensationHead>>(ar["CustomCompensation"].ToString()!);
                        if (parsed != null) model.AdverseAward.CustomCompensationHeads = parsed;
                    } catch {}
                }

                // 4.1 PWs
                string pwQuery = "SELECT * FROM MVC_CASE_ADVERSE_PW WHERE CaseID = @CaseID";
                DataTable pwDt = _db.ExecuteQuery(pwQuery, new[] { new SqlParameter("@CaseID", caseId) });
                foreach(DataRow pwr in pwDt.Rows)
                {
                     if (pwr["Type"].ToString() == "Petitioner") model.AdverseAward.PetitionerPWNames.Add(pwr["PWName"].ToString()!);
                     else model.AdverseAward.Doctors.Add(new DoctorEvidenceViewModel { 
                         DoctorName = pwr["PWName"].ToString()!, 
                          IsTreated = pwr["IsTreated"] != DBNull.Value && Convert.ToBoolean(pwr["IsTreated"])
                      });
                      
                      if (pwr["Type"].ToString() == "TP_Respondent")
                      {
                          model.RespondentEvidence.Add(new RespondentEvidenceViewModel { 
                              Name = pwr["PWName"].ToString()!, 
                              Remark = pwr["Remark"]?.ToString() 
                          });
                      }
                      else if (pwr["Type"].ToString() == "TP_Corporation")
                      {
                          model.CorpEvidence.Add(new RespondentEvidenceViewModel { 
                              Name = pwr["PWName"].ToString()!, 
                              Designation = pwr["Designation"]?.ToString() 
                          });
                      }
                }

                // 4.2 RWs
                string rwQuery = "SELECT * FROM MVC_CASE_ADVERSE_RW WHERE CaseID = @CaseID";
                DataTable rwDt = _db.ExecuteQuery(rwQuery, new[] { new SqlParameter("@CaseID", caseId) });
                foreach(DataRow rwr in rwDt.Rows)
                {
                    model.AdverseAward.RWNames.Add(rwr["RWName"].ToString()!);
                }

                // 4.3 Adverse Connected
                string acQuery = "SELECT * FROM MVC_CASE_ADVERSE_CONNECTED WHERE CaseID = @CaseID";
                DataTable acDt = _db.ExecuteQuery(acQuery, new[] { new SqlParameter("@CaseID", caseId) });
                foreach (DataRow acr in acDt.Rows)
                {
                    string details = acr["CaseDetails"]?.ToString() ?? "";
                    string[] parts = details.Split(new[] { " - " }, StringSplitOptions.None);
                    string mvcPart = parts[0];
                    string mactPart = parts.Length > 1 ? parts[1] : "";

                    string[] mvcParts = mvcPart.Split('/');

                    var connectedCase = new AdverseConnectedCase
                    {
                        ConnectedMVCNo = mvcParts.Length > 0 ? mvcParts[0].Trim() : "",
                        ConnectedYear = mvcParts.Length > 1 && int.TryParse(mvcParts[1], out int y) ? y : 0,
                        MACT = mactPart,
                        Status = acr["Status"]?.ToString(),
                        CurrentStage = acr["CurrentStage"]?.ToString()
                    };

                    connectedCase.AwardDetails = new ConnectedCaseAwardDetails
                    {
                        AwardAmount = acr["AwardAmount"] != DBNull.Value ? (decimal?)Convert.ToDecimal(acr["AwardAmount"]) : null,
                        InterestRate = acr["InterestRate"] != DBNull.Value ? (decimal?)Convert.ToDecimal(acr["InterestRate"]) : null,
                        VictimAge = acr["VictimAge"] != DBNull.Value ? (int?)Convert.ToInt32(acr["VictimAge"]) : null,
                        Occupation = acr["Occupation"]?.ToString(),
                        IncomeConsidered = acr["IncomeConsidered"] != DBNull.Value ? (decimal?)Convert.ToDecimal(acr["IncomeConsidered"]) : null,
                        InjuryDetails = acr["InjuryDetails"]?.ToString()
                    };

                    model.AdverseAward.ConnectedCases.Add(connectedCase);
                }

                // 4.4 Enclosed Docs
                string docQuery = "SELECT * FROM MVC_CASE_ADVERSE_DOCS WHERE CaseID = @CaseID";
                DataTable docDt = _db.ExecuteQuery(docQuery, new[] { new SqlParameter("@CaseID", caseId) });
                foreach (DataRow docr in docDt.Rows)
                {
                    model.AdverseAward.EnclosedDocuments.Add(new EnclosedDocument { 
                        DocName = docr["DocName"].ToString()!, 
                        PageCount = docr.Table.Columns.Contains("PageCount") && docr["PageCount"] != DBNull.Value 
                            ? Convert.ToInt32(docr["PageCount"]) 
                            : null 
                    });
                }
                
                // Map to root for easier access
                model.ForwardingStatus = model.AdverseAward.ForwardingStatus;
            }

            // 5. Fetch EP Details (Independent of Adverse Award table row)
            string epCheckQuery = "SELECT TOP 1 * FROM MVC_EP_DETAILS WHERE CaseID = @CaseID ORDER BY EPID DESC";
            DataTable epDtDet = _db.ExecuteQuery(epCheckQuery, new[] { new SqlParameter("@CaseID", caseId) });
            if (epDtDet.Rows.Count > 0)
            {
                if (model.AdverseAward == null) model.AdverseAward = new AdverseAwardViewModel();
                var epr = epDtDet.Rows[0];
                model.AdverseAward.IsEPFiled = true;
                if (epr.Table.Columns.Contains("AsPerECourts") && epr["AsPerECourts"] != DBNull.Value)
                    model.AdverseAward.AsPerECourts = epr["AsPerECourts"]?.ToString();
                if (epr.Table.Columns.Contains("EPNumber") && epr["EPNumber"] != DBNull.Value)
                    model.AdverseAward.EPNumber = epr["EPNumber"]?.ToString() + (epr.Table.Columns.Contains("EPYear") && epr["EPYear"] != DBNull.Value ? "/" + epr["EPYear"].ToString() : "");
                if (epr.Table.Columns.Contains("EPCourt") && epr["EPCourt"] != DBNull.Value)
                    model.AdverseAward.EPCourt = epr["EPCourt"]?.ToString();
                if (epr.Table.Columns.Contains("NextHearingDate") && epr["NextHearingDate"] != DBNull.Value)
                    model.AdverseAward.EPNextHearingDate = (DateTime?)Convert.ToDateTime(epr["NextHearingDate"]);
                if (epr.Table.Columns.Contains("EPStage") && epr["EPStage"] != DBNull.Value && string.IsNullOrEmpty(model.AdverseAward.EPStage))
                    model.AdverseAward.EPStage = epr["EPStage"]?.ToString();
            }

            if (model.AdverseAward != null)
            {
                try
                {
                    model.AdverseAward.Payments = GetCasePayments(caseId);
                }
                catch { }
            }
            
            // 5. Fetch Linked Appeal Logic (Match by CaseID OR MVC Details for Claimant Appeals)
            string appealQuery = @"SELECT * FROM APPEAL_DETAILS 
                                 WHERE CaseID = @CaseID 
                                 OR (ClaimantMVCNumber = @MVCNo AND ClaimantMVCYear = @MVCYear)";
            DataTable appealDt = _db.ExecuteQuery(appealQuery, new[] { 
                new SqlParameter("@CaseID", caseId),
                new SqlParameter("@MVCNo", model.MVCNo),
                new SqlParameter("@MVCYear", model.MVCYear)
            });
            
            if (appealDt.Rows.Count > 0)
            {
                // Find primary appeal (by CaseID). If multiple, we will merge them.
                DataRow ar = appealDt.Rows[0];
                foreach (DataRow r in appealDt.Rows)
                {
                    if (r["CaseID"] != DBNull.Value && Convert.ToInt32(r["CaseID"]) == caseId)
                    {
                        ar = r;
                        break;
                    }
                }

                model.LinkedAppeal = new AppealViewModel
                {
                    AppealID = Convert.ToInt32(ar["AppealID"]),
                    CaseID = caseId,
                    // 1. Feasibility
                    FeasibilityReceived = ar["FeasibilityReceived"] != DBNull.Value && (bool)ar["FeasibilityReceived"],
                    FeasibilityReceiptDate = ar["FeasibilityReceiptDate"] != DBNull.Value ? Convert.ToDateTime(ar["FeasibilityReceiptDate"]) : null,
                    InitialAction = ar["InitialAction"]?.ToString(),
                    InitialActionRemarks = ar.Table.Columns.Contains("InitialActionRemarks") && ar["InitialActionRemarks"] != DBNull.Value ? ar["InitialActionRemarks"].ToString() : null,
                    ApprovalOutwardNo = ar["ApprovalOutwardNo"]?.ToString(),
                    ApprovalDate = ar["ApprovalDate"] != DBNull.Value ? Convert.ToDateTime(ar["ApprovalDate"]) : null,
                    InitialActionPath1 = ar.Table.Columns.Contains("InitialActionPath1") && ar["InitialActionPath1"] != DBNull.Value ? ar["InitialActionPath1"].ToString() : null,
                    InitialActionPath2 = ar.Table.Columns.Contains("InitialActionPath2") && ar["InitialActionPath2"] != DBNull.Value ? ar["InitialActionPath2"].ToString() : null,
                    Opinion_CLO = ar.Table.Columns.Contains("Opinion_CLO") && ar["Opinion_CLO"] != DBNull.Value ? ar["Opinion_CLO"].ToString() : null,
                    ApprovalCopyPath = ar.Table.Columns.Contains("ApprovalCopyPath") ? ar["ApprovalCopyPath"]?.ToString() : null,
                    MFAJudgmentCopyPath = ar.Table.Columns.Contains("MFAJudgmentCopyPath") ? ar["MFAJudgmentCopyPath"]?.ToString() : null,
                    CorpMFAActionTakenPath = ar.Table.Columns.Contains("CorpMFAActionTakenPath") ? ar["CorpMFAActionTakenPath"]?.ToString() : null,
                    ClaimantSCJudgmentPath = ar.Table.Columns.Contains("ClaimantSCJudgmentPath") ? ar["ClaimantSCJudgmentPath"]?.ToString() : null,

                    // Per-role action fields (LO, DyCLO, CLO, MD)
                    ActionTaken_LO = ar.Table.Columns.Contains("ActionTaken_LO") && ar["ActionTaken_LO"] != DBNull.Value ? ar["ActionTaken_LO"].ToString() : null,
                    ApprovalDate_LO = ar.Table.Columns.Contains("ApprovalDate_LO") && ar["ApprovalDate_LO"] != DBNull.Value ? (DateTime?)Convert.ToDateTime(ar["ApprovalDate_LO"]) : null,
                    Opinion_LO = ar.Table.Columns.Contains("Opinion_LO") && ar["Opinion_LO"] != DBNull.Value ? ar["Opinion_LO"].ToString() : null,
                    ActionTaken_DyCLO = ar.Table.Columns.Contains("ActionTaken_DyCLO") && ar["ActionTaken_DyCLO"] != DBNull.Value ? ar["ActionTaken_DyCLO"].ToString() : null,
                    ApprovalDate_DyCLO = ar.Table.Columns.Contains("ApprovalDate_DyCLO") && ar["ApprovalDate_DyCLO"] != DBNull.Value ? (DateTime?)Convert.ToDateTime(ar["ApprovalDate_DyCLO"]) : null,
                    Opinion_DyCLO = ar.Table.Columns.Contains("Opinion_DyCLO") && ar["Opinion_DyCLO"] != DBNull.Value ? ar["Opinion_DyCLO"].ToString() : null,
                    ActionTaken_CLO = ar.Table.Columns.Contains("ActionTaken_CLO") && ar["ActionTaken_CLO"] != DBNull.Value ? ar["ActionTaken_CLO"].ToString() : null,
                    ApprovalDate_CLO = ar.Table.Columns.Contains("ApprovalDate_CLO") && ar["ApprovalDate_CLO"] != DBNull.Value ? (DateTime?)Convert.ToDateTime(ar["ApprovalDate_CLO"]) : null,
                    ActionTaken_MD = ar.Table.Columns.Contains("ActionTaken_MD") && ar["ActionTaken_MD"] != DBNull.Value ? ar["ActionTaken_MD"].ToString() : null,
                    ApprovalDate_MD = ar.Table.Columns.Contains("ApprovalDate_MD") && ar["ApprovalDate_MD"] != DBNull.Value ? (DateTime?)Convert.ToDateTime(ar["ApprovalDate_MD"]) : null,
                    Opinion_MD = ar.Table.Columns.Contains("Opinion_MD") && ar["Opinion_MD"] != DBNull.Value ? ar["Opinion_MD"].ToString() : null,

                    // 2. Corp MFA
                    CorpMFANumber = ar["CorpMFANumber"]?.ToString(),
                    CorpMFAYear = ar["CorpMFAYear"] != DBNull.Value ? Convert.ToInt32(ar["CorpMFAYear"]) : null,
                    HighCourtBench = ar["HighCourtBench"]?.ToString(),
                    OtherHighCourtBench = ar.Table.Columns.Contains("OtherHighCourtBench") && ar["OtherHighCourtBench"] != DBNull.Value ? ar["OtherHighCourtBench"].ToString() : null,
                    IsPendingForFiling = ar.Table.Columns.Contains("IsPendingForFiling") && ar["IsPendingForFiling"] != DBNull.Value && (bool)ar["IsPendingForFiling"],
                    CorpMFAEntrustmentNo = ar["CorpMFAEntrustmentNo"]?.ToString(),
                    CorpMFAEntrustmentDate = ar["CorpMFAEntrustmentDate"] != DBNull.Value ? Convert.ToDateTime(ar["CorpMFAEntrustmentDate"]) : null,
                    CorpMFAAdvocate = ar["CorpMFAAdvocate"]?.ToString(),
                    CorpMFACNRNumber = ar.Table.Columns.Contains("CorpMFACNRNumber") ? ar["CorpMFACNRNumber"]?.ToString() : null,
                    CorpMFANextHearingDate = ar.Table.Columns.Contains("CorpMFANextHearingDate") && ar["CorpMFANextHearingDate"] != DBNull.Value ? (DateTime?)Convert.ToDateTime(ar["CorpMFANextHearingDate"]) : null,
                    CorpMFAStage = ar.Table.Columns.Contains("CorpMFAStage") ? ar["CorpMFAStage"]?.ToString() : null,

                    // 3. Status
                    CorpMFAStatus = ar["CorpMFAStatus"]?.ToString(),
                    RestorationFiled = ar["RestorationFiled"] != DBNull.Value && (bool)ar["RestorationFiled"],
                    RestorationDate = ar["RestorationDate"] != DBNull.Value ? Convert.ToDateTime(ar["RestorationDate"]) : null,
                    RestorationStatus = ar["RestorationStatus"]?.ToString(),

                    // 4. Outcome
                    CorpMFAOutcome = ar["CorpMFAOutcome"]?.ToString(),
                    CorpMFAActionTaken = ar.Table.Columns.Contains("CorpMFAActionTaken") && ar["CorpMFAActionTaken"] != DBNull.Value ? ar["CorpMFAActionTaken"].ToString() : null,
                    ClosureOutwardNo = ar["ClosureOutwardNo"]?.ToString(),
                    ClosureDate = ar["ClosureDate"] != DBNull.Value ? Convert.ToDateTime(ar["ClosureDate"]) : null,

                    // 5. Claimant Appeal
                    ClaimantMFANumber = ar.Table.Columns.Contains("ClaimantMFANumber") ? ar["ClaimantMFANumber"]?.ToString() : null,
                    ClaimantMFAYear = ar.Table.Columns.Contains("ClaimantMFAYear") && ar["ClaimantMFAYear"] != DBNull.Value ? (int?)Convert.ToInt32(ar["ClaimantMFAYear"]) : null,
                    ClaimantMFACNRNumber = ar.Table.Columns.Contains("ClaimantMFACNRNumber") ? ar["ClaimantMFACNRNumber"]?.ToString() : null,
                    ClaimantMFANextHearingDate = ar.Table.Columns.Contains("ClaimantMFANextHearingDate") && ar["ClaimantMFANextHearingDate"] != DBNull.Value ? (DateTime?)Convert.ToDateTime(ar["ClaimantMFANextHearingDate"]) : null,
                    ClaimantMFAStage = ar.Table.Columns.Contains("ClaimantMFAStage") ? ar["ClaimantMFAStage"]?.ToString() : null,
                    ClaimantMFAStatus = ar.Table.Columns.Contains("ClaimantMFAStatus") ? ar["ClaimantMFAStatus"]?.ToString() : null,
                    ClaimantMFADecision = ar.Table.Columns.Contains("ClaimantMFADecision") ? ar["ClaimantMFADecision"]?.ToString() : null,
                    ClaimantActionTaken = ar.Table.Columns.Contains("ClaimantActionTaken") ? ar["ClaimantActionTaken"]?.ToString() : null,
                    ClaimantApprovalNo = ar.Table.Columns.Contains("ClaimantApprovalNo") ? ar["ClaimantApprovalNo"]?.ToString() : null,
                    ClaimantApprovalDate = ar.Table.Columns.Contains("ClaimantApprovalDate") && ar["ClaimantApprovalDate"] != DBNull.Value ? (DateTime?)Convert.ToDateTime(ar["ClaimantApprovalDate"]) : null,
                    ClaimantMFAEntrustmentNo = ar.Table.Columns.Contains("ClaimantMFAEntrustmentNo") ? ar["ClaimantMFAEntrustmentNo"]?.ToString() : null,
                    ClaimantMFAEntrustmentDate = ar.Table.Columns.Contains("ClaimantMFAEntrustmentDate") && ar["ClaimantMFAEntrustmentDate"] != DBNull.Value ? (DateTime?)Convert.ToDateTime(ar["ClaimantMFAEntrustmentDate"]) : null,
                    ClaimantMFAAdvocate = ar.Table.Columns.Contains("ClaimantMFAAdvocate") ? ar["ClaimantMFAAdvocate"]?.ToString() : null,
                    ClaimantDivisionID = ar.Table.Columns.Contains("ClaimantDivisionID") && ar["ClaimantDivisionID"] != DBNull.Value ? (int?)Convert.ToInt32(ar["ClaimantDivisionID"]) : null,
                    ClaimantMVCNumber = ar.Table.Columns.Contains("ClaimantMVCNumber") ? ar["ClaimantMVCNumber"]?.ToString() : null,
                    ClaimantMVCYear = ar.Table.Columns.Contains("ClaimantMVCYear") && ar["ClaimantMVCYear"] != DBNull.Value ? (int?)Convert.ToInt32(ar["ClaimantMVCYear"]) : null,
                    ClaimantMVCCurrentStatus = ar.Table.Columns.Contains("ClaimantMVCCurrentStatus") ? ar["ClaimantMVCCurrentStatus"]?.ToString() : null,

                    // 6. Corp SC
                    CorpSCNumber = ar.Table.Columns.Contains("CorpSCNumber") ? ar["CorpSCNumber"]?.ToString() : null,
                    CorpSCYear = ar.Table.Columns.Contains("CorpSCYear") && ar["CorpSCYear"] != DBNull.Value ? (int?)Convert.ToInt32(ar["CorpSCYear"]) : null,
                    CorpSCEntrustmentNo = ar.Table.Columns.Contains("CorpSCEntrustmentNo") ? ar["CorpSCEntrustmentNo"]?.ToString() : null,
                    CorpSCEntrustmentDate = ar.Table.Columns.Contains("CorpSCEntrustmentDate") && ar["CorpSCEntrustmentDate"] != DBNull.Value ? (DateTime?)Convert.ToDateTime(ar["CorpSCEntrustmentDate"]) : null,
                    CorpSCAdvocate = ar.Table.Columns.Contains("CorpSCAdvocate") ? ar["CorpSCAdvocate"]?.ToString() : null,
                    CorpSCStatus = ar.Table.Columns.Contains("CorpSCStatus") ? ar["CorpSCStatus"]?.ToString() : null,

                    // 8. Claimant SC
                    IsClaimantSCAppeal = ar.Table.Columns.Contains("IsClaimantSCAppeal") && ar["IsClaimantSCAppeal"] != DBNull.Value && (bool)ar["IsClaimantSCAppeal"],
                    IsClaimantSCPending = ar.Table.Columns.Contains("IsClaimantSCPending") && ar["IsClaimantSCPending"] != DBNull.Value && (bool)ar["IsClaimantSCPending"],
                    ClaimantSCNumber = ar.Table.Columns.Contains("ClaimantSCNumber") ? ar["ClaimantSCNumber"]?.ToString() : null,
                    ClaimantSCDiaryNumber = ar.Table.Columns.Contains("ClaimantSCDiaryNumber") && ar["ClaimantSCDiaryNumber"] != DBNull.Value ? ar["ClaimantSCDiaryNumber"]?.ToString() : null,
                    ClaimantSCYear = ar.Table.Columns.Contains("ClaimantSCYear") && ar["ClaimantSCYear"] != DBNull.Value ? (int?)Convert.ToInt32(ar["ClaimantSCYear"]) : null,
                    ClaimantSLPYear = ar.Table.Columns.Contains("ClaimantSLPYear") && ar["ClaimantSLPYear"] != DBNull.Value ? (int?)Convert.ToInt32(ar["ClaimantSLPYear"]) : null,
                    ClaimantSCFiledBy = ar.Table.Columns.Contains("ClaimantSCFiledBy") ? ar["ClaimantSCFiledBy"]?.ToString() : null,
                    ClaimantSCEntrustmentNo = ar.Table.Columns.Contains("ClaimantSCEntrustmentNo") ? ar["ClaimantSCEntrustmentNo"]?.ToString() : null,
                    ClaimantSCEntrustmentDate = ar.Table.Columns.Contains("ClaimantSCEntrustmentDate") && ar["ClaimantSCEntrustmentDate"] != DBNull.Value ? (DateTime?)Convert.ToDateTime(ar["ClaimantSCEntrustmentDate"]) : null,
                    ClaimantSCAdvocate = ar.Table.Columns.Contains("ClaimantSCAdvocate") ? ar["ClaimantSCAdvocate"]?.ToString() : null,
                    ClaimantSCStatus = ar.Table.Columns.Contains("ClaimantSCStatus") ? ar["ClaimantSCStatus"]?.ToString() : null,
                    ClaimantSCOutcome = ar.Table.Columns.Contains("ClaimantSCOutcome") ? ar["ClaimantSCOutcome"]?.ToString() : null,
                    ClaimantSCActionTaken = ar.Table.Columns.Contains("ClaimantSCActionTaken") ? ar["ClaimantSCActionTaken"]?.ToString() : null,
                    ClaimantSCClosureNo = ar.Table.Columns.Contains("ClaimantSCClosureNo") ? ar["ClaimantSCClosureNo"]?.ToString() : null,
                    ClaimantSCClosureDate = ar.Table.Columns.Contains("ClaimantSCClosureDate") && ar["ClaimantSCClosureDate"] != DBNull.Value ? (DateTime?)Convert.ToDateTime(ar["ClaimantSCClosureDate"]) : null,

                    // 10. Compliance
                    FinalComplianceStatus = ar.Table.Columns.Contains("FinalComplianceStatus") ? ar["FinalComplianceStatus"]?.ToString() : null,
                    AmountDeposited = ar.Table.Columns.Contains("AmountDeposited") && ar["AmountDeposited"] != DBNull.Value ? (decimal?)Convert.ToDecimal(ar["AmountDeposited"]) : null,
                    FinalComplianceDate = ar.Table.Columns.Contains("FinalComplianceDate") && ar["FinalComplianceDate"] != DBNull.Value ? (DateTime?)Convert.ToDateTime(ar["FinalComplianceDate"]) : null,
                    FinalRemarks = ar.Table.Columns.Contains("FinalRemarks") ? ar["FinalRemarks"]?.ToString() : null,
                    ComplianceLetterPath = ar.Table.Columns.Contains("ComplianceLetterPath") ? ar["ComplianceLetterPath"]?.ToString() : null,

                    // 3. Stay Items
                    StayGranted = ar.Table.Columns.Contains("StayGranted") && ar["StayGranted"] != DBNull.Value && (bool)ar["StayGranted"],
                    StayComplianceOutwardNo = ar.Table.Columns.Contains("StayComplianceOutwardNo") ? ar["StayComplianceOutwardNo"]?.ToString() : null,
                    StayComplianceDate = ar.Table.Columns.Contains("StayComplianceDate") && ar["StayComplianceDate"] != DBNull.Value ? (DateTime?)Convert.ToDateTime(ar["StayComplianceDate"]) : null,
                    StayOrderPath1 = ar.Table.Columns.Contains("StayOrderPath1") ? ar["StayOrderPath1"]?.ToString() : null,
                    StayOrderPath2 = ar.Table.Columns.Contains("StayOrderPath2") ? ar["StayOrderPath2"]?.ToString() : null
                };

                // If multiple rows exist (e.g. separate records for Corp and Claimant appeals), merge them
                if (appealDt.Rows.Count > 1)
                {
                    foreach (DataRow otherRow in appealDt.Rows)
                    {
                        if (otherRow == ar) continue;

                        // Merge Corp Details if missing in primary
                        if (string.IsNullOrEmpty(model.LinkedAppeal.CorpMFANumber))
                        {
                            model.LinkedAppeal.CorpMFANumber = otherRow.Table.Columns.Contains("CorpMFANumber") ? otherRow["CorpMFANumber"]?.ToString() : model.LinkedAppeal.CorpMFANumber;
                            model.LinkedAppeal.CorpMFAYear = otherRow.Table.Columns.Contains("CorpMFAYear") && otherRow["CorpMFAYear"] != DBNull.Value ? (int?)Convert.ToInt32(otherRow["CorpMFAYear"]) : model.LinkedAppeal.CorpMFAYear;
                            model.LinkedAppeal.CorpMFAStatus = otherRow.Table.Columns.Contains("CorpMFAStatus") ? otherRow["CorpMFAStatus"]?.ToString() : model.LinkedAppeal.CorpMFAStatus;
                            model.LinkedAppeal.CorpMFAAdvocate = otherRow.Table.Columns.Contains("CorpMFAAdvocate") ? otherRow["CorpMFAAdvocate"]?.ToString() : model.LinkedAppeal.CorpMFAAdvocate;
                            model.LinkedAppeal.CorpMFACNRNumber = otherRow.Table.Columns.Contains("CorpMFACNRNumber") ? otherRow["CorpMFACNRNumber"]?.ToString() : model.LinkedAppeal.CorpMFACNRNumber;
                            model.LinkedAppeal.CorpMFAStage = otherRow.Table.Columns.Contains("CorpMFAStage") ? otherRow["CorpMFAStage"]?.ToString() : model.LinkedAppeal.CorpMFAStage;
                            model.LinkedAppeal.CorpMFANextHearingDate = otherRow.Table.Columns.Contains("CorpMFANextHearingDate") && otherRow["CorpMFANextHearingDate"] != DBNull.Value ? (DateTime?)Convert.ToDateTime(otherRow["CorpMFANextHearingDate"]) : model.LinkedAppeal.CorpMFANextHearingDate;
                            model.LinkedAppeal.CorpMFAEntrustmentNo = otherRow.Table.Columns.Contains("CorpMFAEntrustmentNo") ? otherRow["CorpMFAEntrustmentNo"]?.ToString() : model.LinkedAppeal.CorpMFAEntrustmentNo;
                            model.LinkedAppeal.CorpMFAEntrustmentDate = otherRow.Table.Columns.Contains("CorpMFAEntrustmentDate") && otherRow["CorpMFAEntrustmentDate"] != DBNull.Value ? (DateTime?)Convert.ToDateTime(otherRow["CorpMFAEntrustmentDate"]) : model.LinkedAppeal.CorpMFAEntrustmentDate;
                            model.LinkedAppeal.CorpMFAOutcome = otherRow.Table.Columns.Contains("CorpMFAOutcome") ? otherRow["CorpMFAOutcome"]?.ToString() : model.LinkedAppeal.CorpMFAOutcome;
                            model.LinkedAppeal.CorpMFAActionTaken = otherRow.Table.Columns.Contains("CorpMFAActionTaken") ? otherRow["CorpMFAActionTaken"]?.ToString() : model.LinkedAppeal.CorpMFAActionTaken;
                        }

                        // Merge Claimant Details if missing in primary
                        if (string.IsNullOrEmpty(model.LinkedAppeal.ClaimantMFANumber))
                        {
                            model.LinkedAppeal.ClaimantMFANumber = otherRow.Table.Columns.Contains("ClaimantMFANumber") ? otherRow["ClaimantMFANumber"]?.ToString() : null;
                            model.LinkedAppeal.ClaimantMFAYear = otherRow.Table.Columns.Contains("ClaimantMFAYear") && otherRow["ClaimantMFAYear"] != DBNull.Value ? (int?)Convert.ToInt32(otherRow["ClaimantMFAYear"]) : model.LinkedAppeal.ClaimantMFAYear;
                            model.LinkedAppeal.ClaimantMFAStatus = otherRow.Table.Columns.Contains("ClaimantMFAStatus") ? otherRow["ClaimantMFAStatus"]?.ToString() : model.LinkedAppeal.ClaimantMFAStatus;
                            model.LinkedAppeal.ClaimantMFADecision = otherRow.Table.Columns.Contains("ClaimantMFADecision") ? otherRow["ClaimantMFADecision"]?.ToString() : model.LinkedAppeal.ClaimantMFADecision;
                            model.LinkedAppeal.ClaimantActionTaken = otherRow.Table.Columns.Contains("ClaimantActionTaken") ? otherRow["ClaimantActionTaken"]?.ToString() : model.LinkedAppeal.ClaimantActionTaken;
                            model.LinkedAppeal.ClaimantApprovalNo = otherRow.Table.Columns.Contains("ClaimantApprovalNo") ? otherRow["ClaimantApprovalNo"]?.ToString() : model.LinkedAppeal.ClaimantApprovalNo;
                            model.LinkedAppeal.ClaimantApprovalDate = otherRow.Table.Columns.Contains("ClaimantApprovalDate") && otherRow["ClaimantApprovalDate"] != DBNull.Value ? (DateTime?)Convert.ToDateTime(otherRow["ClaimantApprovalDate"]) : model.LinkedAppeal.ClaimantApprovalDate;
                            model.LinkedAppeal.ClaimantMFAEntrustmentNo = otherRow.Table.Columns.Contains("ClaimantMFAEntrustmentNo") ? otherRow["ClaimantMFAEntrustmentNo"]?.ToString() : model.LinkedAppeal.ClaimantMFAEntrustmentNo;
                            model.LinkedAppeal.ClaimantMFAEntrustmentDate = otherRow.Table.Columns.Contains("ClaimantMFAEntrustmentDate") && otherRow["ClaimantMFAEntrustmentDate"] != DBNull.Value ? (DateTime?)Convert.ToDateTime(otherRow["ClaimantMFAEntrustmentDate"]) : model.LinkedAppeal.ClaimantMFAEntrustmentDate;
                            model.LinkedAppeal.ClaimantMFAAdvocate = otherRow.Table.Columns.Contains("ClaimantMFAAdvocate") ? otherRow["ClaimantMFAAdvocate"]?.ToString() : model.LinkedAppeal.ClaimantMFAAdvocate;
                            model.LinkedAppeal.ClaimantMVCNumber = otherRow.Table.Columns.Contains("ClaimantMVCNumber") ? otherRow["ClaimantMVCNumber"]?.ToString() : model.LinkedAppeal.ClaimantMVCNumber;
                            model.LinkedAppeal.ClaimantMVCYear = otherRow.Table.Columns.Contains("ClaimantMVCYear") && otherRow["ClaimantMVCYear"] != DBNull.Value ? (int?)Convert.ToInt32(otherRow["ClaimantMVCYear"]) : model.LinkedAppeal.ClaimantMVCYear;
                            model.LinkedAppeal.ClaimantMVCCurrentStatus = otherRow.Table.Columns.Contains("ClaimantMVCCurrentStatus") ? otherRow["ClaimantMVCCurrentStatus"]?.ToString() : model.LinkedAppeal.ClaimantMVCCurrentStatus;
                            model.LinkedAppeal.ClaimantMFACNRNumber = otherRow.Table.Columns.Contains("ClaimantMFACNRNumber") ? otherRow["ClaimantMFACNRNumber"]?.ToString() : model.LinkedAppeal.ClaimantMFACNRNumber;
                            model.LinkedAppeal.ClaimantMFANextHearingDate = otherRow.Table.Columns.Contains("ClaimantMFANextHearingDate") && otherRow["ClaimantMFANextHearingDate"] != DBNull.Value ? (DateTime?)Convert.ToDateTime(otherRow["ClaimantMFANextHearingDate"]) : model.LinkedAppeal.ClaimantMFANextHearingDate;
                            model.LinkedAppeal.ClaimantMFAStage = otherRow.Table.Columns.Contains("ClaimantMFAStage") ? otherRow["ClaimantMFAStage"]?.ToString() : model.LinkedAppeal.ClaimantMFAStage;
                        }
                    }
                }

                // 5.1 Fetch Connected Appeals (Cross Appeals)
                string connAppQuery = "SELECT * FROM APPEAL_CONNECTED WHERE CaseID = @CaseID";
                DataTable connAppDt = _db.ExecuteQuery(connAppQuery, new[] { new SqlParameter("@CaseID", caseId) });
                foreach (DataRow connAppRow in connAppDt.Rows)
                {
                    model.LinkedAppeal.ConnectedCases.Add(new ConnectedAppeal
                    {
                        ConnectedID = (int)connAppRow["ConnectedID"],
                        ConnectedMVCNo = connAppRow["ConnectedMVCNo"]?.ToString(),
                        FiledBy = connAppRow["FiledBy"]?.ToString(),
                        MFA_Number = connAppRow["MFA_Number"]?.ToString(),
                        Status = connAppRow["Status"]?.ToString()
                    });
                }
            }

            if (model.AdverseAward == null)
            {
                model.AdverseAward = new AdverseAwardViewModel();
            }
            if (model.AdverseAward.ClaimPetitionDate == null && model.ClaimPetitionDate != null)
            {
                model.AdverseAward.ClaimPetitionDate = model.ClaimPetitionDate;
            }
            if (model.ClaimPetitionDate == null && model.AdverseAward.ClaimPetitionDate != null)
            {
                model.ClaimPetitionDate = model.AdverseAward.ClaimPetitionDate;
            }
            model.AdverseAward.IsAllegedAccident = model.IsAllegedAccident || model.AdverseAward.IsAllegedAccident;
            model.IsAllegedAccident = model.AdverseAward.IsAllegedAccident;

            return model;
        }

        public bool UpdateCase(MVCCaseViewModel model)
        {
            using (var conn = new SqlConnection(_db.GetConnectionString()))
            {
                conn.Open();
                using (var trans = conn.BeginTransaction())
                {
                    try
                    {
                        string updateQuery = @"
                            UPDATE MVC_CASES SET DivisionID = @DivisionID, MVCNo = @MVCNo, MVCYear = @MVCYear, MACTID = @MACTID,
                                VehicleNo = @VehicleNo, AccidentDate = @AccidentDate, CaseType = @CaseType, StatusID = @StatusID,
                                ClaimType = @ClaimType, ThirdPartyFlag = @ThirdPartyFlag, ClaimAmount = @ClaimAmount,
                                AdvocateID = @AdvocateID, AdvocateName = @AdvocateName, EntrustmentNo = @EntrustmentNo, EntrustmentDate = @EntrustmentDate,
                                DoubleClaimFlag = @DoubleClaimFlag, NextHearingDate = @NextHearingDate, CurrentStage = @CurrentStage,
                                DisposalStatus = @DisposalStatus, DisposalResult = @DisposalResult, DisposalRemarks = @DisposalRemarks,
                                IsDocumentSent = @IsDocumentSent, DocumentOutwardNo = @DocumentOutwardNo, DocumentOutwardDate = @DocumentOutwardDate,
                                IsObjectionFiled = @IsObjectionFiled, ObjectionFiledDate = @ObjectionFiledDate, ObjectionOutwardNo = @ObjectionOutwardNo, IsEvidenceFiled = @IsEvidenceFiled,

                                VehicleType = @VehicleType, PetitionFiledFor = @PetitionFiledFor, ClosureDate = @ClosureDate,
                                IsWithinLimitation = @IsWithinLimitation, LimitationRemark = @LimitationRemark,
                                IsDelayCondoned = @IsDelayCondoned, DelayRemark = @DelayRemark,
                                IsSTPassenger = @IsSTPassenger, IsMedicalExpensesPaid = @IsMedicalExpensesPaid,
                                MedicalPaidAmount = @MedicalPaidAmount, MedicalPaidRemarks = @MedicalPaidRemarks,
                                 IsARFAmountPaid = @IsARFAmountPaid, ARFPaidAmount = @ARFPaidAmount, ARFPaidRemarks = @ARFPaidRemarks,
                                 IsOppositeVehicleInmate = @IsOppositeVehicleInmate,
                                 ClaimRemark = @ClaimRemark, ClaimPetitionDate = @ClaimPetitionDate,
                                 HasInterimOrder = @HasInterimOrder, InterimOrderFilePath = COALESCE(@InterimOrderFilePath, InterimOrderFilePath),
                                 CNRNumber = @CNRNumber, EstCode = @EstCode, CaseTypeCode = @CaseTypeCode,
                                 ModifiedDate = GETDATE()
                             WHERE CaseID = @CaseID";

                        var cmd = new SqlCommand(updateQuery, conn, trans);
                        cmd.Parameters.AddWithValue("@DivisionID", model.DivisionID);
                        cmd.Parameters.AddWithValue("@MVCNo", model.MVCNo?.Trim() ?? string.Empty);
                        cmd.Parameters.AddWithValue("@MVCYear", model.MVCYear);
                        cmd.Parameters.AddWithValue("@MACTID", model.MACTID);
                        cmd.Parameters.AddWithValue("@VehicleNo", (object?)model.VehicleNo?.Trim() ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@AccidentDate", model.AccidentDate ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@CaseType", model.CaseType);
                        cmd.Parameters.AddWithValue("@StatusID", model.StatusID);
                        cmd.Parameters.AddWithValue("@ClaimType", model.ClaimType ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@ThirdPartyFlag", model.ThirdPartyFlag);
                        cmd.Parameters.AddWithValue("@ClaimAmount", model.ClaimAmount ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@AdvocateID", model.AdvocateID ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@AdvocateName", model.AdvocateName ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@EntrustmentNo", model.EntrustmentNo ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@EntrustmentDate", model.EntrustmentDate ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@DoubleClaimFlag", model.DoubleClaimFlag);

                        cmd.Parameters.AddWithValue("@NextHearingDate", model.NextHearingDate ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@CurrentStage", model.CurrentStage ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@DisposalStatus", model.DisposalStatus ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@DisposalResult", model.DisposalResult ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@DisposalRemarks", model.DisposalRemarks ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@IsDocumentSent", model.IsDocumentSent);
                        cmd.Parameters.AddWithValue("@DocumentOutwardNo", model.DocumentOutwardNo ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@DocumentOutwardDate", model.DocumentOutwardDate ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@IsObjectionFiled", model.IsObjectionFiled);
                        cmd.Parameters.AddWithValue("@VehicleType", model.VehicleType ?? "Corporation");
                        cmd.Parameters.AddWithValue("@PetitionFiledFor", model.PetitionFiledFor ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@ObjectionFiledDate", model.ObjectionFiledDate ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@IsEvidenceFiled", model.IsEvidenceFiled);
                        cmd.Parameters.AddWithValue("@ObjectionOutwardNo", model.ObjectionOutwardNo ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@ClosureDate", model.ClosureDate ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@IsWithinLimitation", model.IsWithinLimitation);
                        cmd.Parameters.AddWithValue("@LimitationRemark", model.LimitationRemark ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@IsDelayCondoned", (object)model.IsDelayCondoned ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@DelayRemark", model.DelayRemark ?? (object)DBNull.Value);

                        cmd.Parameters.AddWithValue("@IsSTPassenger", model.IsSTPassenger);
                        cmd.Parameters.AddWithValue("@IsMedicalExpensesPaid", model.IsMedicalExpensesPaid);
                        cmd.Parameters.AddWithValue("@MedicalPaidAmount", model.MedicalPaidAmount ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@MedicalPaidRemarks", model.MedicalPaidRemarks ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@IsARFAmountPaid", model.IsARFAmountPaid);
                        cmd.Parameters.AddWithValue("@ARFPaidAmount", model.ARFPaidAmount ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@ARFPaidRemarks", model.ARFPaidRemarks ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@IsOppositeVehicleInmate", model.IsOppositeVehicleInmate);
                        cmd.Parameters.AddWithValue("@ClaimRemark", model.ClaimRemark ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@ClaimPetitionDate", (object?)model.ClaimPetitionDate ?? (object?)model.AdverseAward?.ClaimPetitionDate ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@HasInterimOrder", model.HasInterimOrder);
                        cmd.Parameters.AddWithValue("@InterimOrderFilePath", model.InterimOrderFilePath ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@CNRNumber", (object?)model.CNRNumber?.Trim() ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@EstCode", model.EstCode ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@CaseTypeCode", model.CaseTypeCode ?? (object)DBNull.Value);

                        cmd.Parameters.AddWithValue("@CaseID", model.CaseID);
                        cmd.ExecuteNonQuery();

                        // Update Child Tables (Delete and Re-insert only when collection is submitted)
                        if (model.Petitioners != null)
                        {
                            var delPetCmd = new SqlCommand("DELETE FROM MVC_CASE_PETITIONERS WHERE CaseID = @CaseID", conn, trans);
                            delPetCmd.Parameters.AddWithValue("@CaseID", model.CaseID);
                            delPetCmd.ExecuteNonQuery();
                            foreach (var pet in model.Petitioners)
                            {
                                var petCmd = new SqlCommand("INSERT INTO MVC_CASE_PETITIONERS (CaseID, PetitionerName, Relationship) VALUES (@CaseID, @PetitionerName, @Relationship)", conn, trans);
                                petCmd.Parameters.AddWithValue("@CaseID", model.CaseID);
                                petCmd.Parameters.AddWithValue("@PetitionerName", pet.PetitionerName);
                                string finalRel = pet.Relationship;
                                if (finalRel == "Others" && !string.IsNullOrEmpty(pet.RelationshipOthers))
                                {
                                    finalRel = pet.RelationshipOthers;
                                }
                                petCmd.Parameters.AddWithValue("@Relationship", finalRel ?? (object)DBNull.Value);
                                petCmd.ExecuteNonQuery();
                            }
                        }

                        if (model.Respondents != null)
                        {
                            var delRespCmd = new SqlCommand("DELETE FROM MVC_CASE_RESPONDENTS WHERE CaseID = @CaseID", conn, trans);
                            delRespCmd.Parameters.AddWithValue("@CaseID", model.CaseID);
                            delRespCmd.ExecuteNonQuery();
                            foreach (var resp in model.Respondents)
                            {
                                if (!string.IsNullOrEmpty(resp.RespondentName))
                                {
                                    var respCmd = new SqlCommand("INSERT INTO MVC_CASE_RESPONDENTS (CaseID, RespondentName, Remarks) VALUES (@CaseID, @RespondentName, @Remarks)", conn, trans);
                                    respCmd.Parameters.AddWithValue("@CaseID", model.CaseID);
                                    respCmd.Parameters.AddWithValue("@RespondentName", resp.RespondentName);
                                    respCmd.Parameters.AddWithValue("@Remarks", resp.Remarks ?? (object)DBNull.Value);
                                    respCmd.ExecuteNonQuery();
                                }
                            }
                        }

                        if (model.ConnectedCases != null)
                        {
                            var delConnCmd = new SqlCommand("DELETE FROM MVC_CASE_CONNECTED WHERE CaseID = @CaseID", conn, trans);
                            delConnCmd.Parameters.AddWithValue("@CaseID", model.CaseID);
                            delConnCmd.ExecuteNonQuery();
                            foreach (var connCase in model.ConnectedCases)
                            {
                                var connCmd = new SqlCommand("INSERT INTO MVC_CASE_CONNECTED (CaseID, ConnectedMVCNo, ConnectedYear, Remarks, MACT, IsDoubleClaim) VALUES (@CaseID, @MVCNo, @Year, @Remarks, @MACT, @IsDoubleClaim)", conn, trans);
                                connCmd.Parameters.AddWithValue("@CaseID", model.CaseID);
                                connCmd.Parameters.AddWithValue("@MVCNo", connCase.ConnectedMVCNo);
                                connCmd.Parameters.AddWithValue("@Year", connCase.ConnectedYear);
                                connCmd.Parameters.AddWithValue("@Remarks", connCase.Remarks ?? (object)DBNull.Value);
                                connCmd.Parameters.AddWithValue("@MACT", connCase.MACT ?? (object)DBNull.Value);
                                connCmd.Parameters.AddWithValue("@IsDoubleClaim", connCase.IsDoubleClaim);
                                connCmd.ExecuteNonQuery();
                            }
                        }

                        // Synchronize Opposite Vehicle Numbers
                        if (model.OppositeVehicleNumbers != null)
                        {
                            var delVnoCmd = new SqlCommand("DELETE FROM MVC_CASE_OPPOSITE_VEHICLES WHERE CaseID = @CaseID", conn, trans);
                            delVnoCmd.Parameters.AddWithValue("@CaseID", model.CaseID);
                            delVnoCmd.ExecuteNonQuery();
                            foreach (var vno in model.OppositeVehicleNumbers)
                            {
                                if (!string.IsNullOrWhiteSpace(vno))
                                {
                                    var vnoCmd = new SqlCommand("INSERT INTO MVC_CASE_OPPOSITE_VEHICLES (CaseID, VehicleNo) VALUES (@CaseID, @VehicleNo)", conn, trans);
                                    vnoCmd.Parameters.AddWithValue("@CaseID", model.CaseID);
                                    vnoCmd.Parameters.AddWithValue("@VehicleNo", vno.Trim());
                                    vnoCmd.ExecuteNonQuery();
                                }
                            }
                        }

                        // Adverse Award Update (Synchronize defense details and adverse award)
                        var adverseAward = model.AdverseAward ?? new AdverseAwardViewModel();
                        if (adverseAward.ClaimPetitionDate == null && model.ClaimPetitionDate != null) adverseAward.ClaimPetitionDate = model.ClaimPetitionDate;
                        adverseAward.IsAllegedAccident = model.IsAllegedAccident || adverseAward.IsAllegedAccident;

                        // Fetch existing adverse file paths to prevent wiping file uploads if omitted on edit
                        string existingTR18 = null, existingSec = null, existingGovID = null, existingPunish = null, existingJudg = null, existingDelay = null, existingDelayCond = null, existingAge = null;
                        using (var fetchCmd = new SqlCommand("SELECT TR18UploadPath, SecurityUploadPath, GovIDProofUploadPath, PunishmentOrderUploadPath, AdverseJudgmentUploadPath, DelayApplicationPath, DelayCondonedOrderPath, AgeProofUploadPath FROM MVC_CASE_ADVERSE_DETAILS WHERE CaseID = @CaseID", conn, trans))
                        {
                            fetchCmd.Parameters.AddWithValue("@CaseID", model.CaseID);
                            using (var reader = fetchCmd.ExecuteReader())
                            {
                                if (reader.Read())
                                {
                                    existingTR18 = reader["TR18UploadPath"]?.ToString();
                                    existingSec = reader["SecurityUploadPath"]?.ToString();
                                    existingGovID = reader["GovIDProofUploadPath"]?.ToString();
                                    existingPunish = reader["PunishmentOrderUploadPath"]?.ToString();
                                    existingJudg = reader["AdverseJudgmentUploadPath"]?.ToString();
                                    existingDelay = reader["DelayApplicationPath"]?.ToString();
                                    existingDelayCond = reader["DelayCondonedOrderPath"]?.ToString();
                                    existingAge = reader["AgeProofUploadPath"]?.ToString();
                                }
                            }
                        }

                        if (string.IsNullOrEmpty(adverseAward.TR18UploadPath)) adverseAward.TR18UploadPath = existingTR18;
                        if (string.IsNullOrEmpty(adverseAward.SecurityUploadPath)) adverseAward.SecurityUploadPath = existingSec;
                        if (string.IsNullOrEmpty(adverseAward.GovIDProofUploadPath)) adverseAward.GovIDProofUploadPath = existingGovID;
                        if (string.IsNullOrEmpty(adverseAward.PunishmentOrderUploadPath)) adverseAward.PunishmentOrderUploadPath = existingPunish;
                        if (string.IsNullOrEmpty(adverseAward.AdverseJudgmentUploadPath)) adverseAward.AdverseJudgmentUploadPath = existingJudg;
                        if (string.IsNullOrEmpty(adverseAward.DelayApplicationPath)) adverseAward.DelayApplicationPath = existingDelay;
                        if (string.IsNullOrEmpty(adverseAward.DelayCondonedOrderPath)) adverseAward.DelayCondonedOrderPath = existingDelayCond;
                        if (string.IsNullOrEmpty(adverseAward.AgeProofUploadPath)) adverseAward.AgeProofUploadPath = existingAge;

                        var delAdvDetCmd = new SqlCommand("DELETE FROM MVC_CASE_ADVERSE_DETAILS WHERE CaseID = @CaseID", conn, trans);
                        delAdvDetCmd.Parameters.AddWithValue("@CaseID", model.CaseID);
                        delAdvDetCmd.ExecuteNonQuery();
                            string advQuery = @"
                                INSERT INTO MVC_CASE_ADVERSE_DETAILS (CaseID, BusInsuranceDetails, IsBusInsured, ClaimPetitionDate, AwardDate, MannerOfAccident, MannerOfAccidentRO,
                                    ObjectionFiled, ObjectionRemarks, RWType, TR18Remarks, IsDeceasedInTR18, TR18UploadPath, IsAllegedAccident,
                                    AdverseDoubleClaimFlag, DoubleClaimDetails, SecurityRequired, SecurityUploadPath, GovIDProofUploadPath,
                                    DisposedOnDate, CopyAppliedDate, CertifiedCopyRemarks, CopyIssuedDate, CopyReceivedDate, CopyDeliveredDate,
                                    AwardAmount, InterestRate, VictimAge, Occupation, IncomeConsidered, IncomePeriod, InjuryDetails,
                                    DriverPunishmentStatus, PunishmentOrderUploadPath, PunishmentRemarks, DisabilityPercentage, InterimCompAmount, InterimCompDeducted,
                                    AdvocateOpinion, LOOpinion, DCOpinion, ForwardingStatus, ClosureRemarks, ClosureDate, OutwardNumber, OutwardDate,
                                    IsCorpLiable, LiabilityPercentage, LiabilityRemarks, AdverseJudgmentUploadPath, FutureProspectus, InjuryType, TreatedDocFlag,
                                    IsSTPassenger, IsMedicalExpensesPaid, MedicalPaidAmount, MedicalPaidRemarks, IsARFAmountPaid, ARFPaidAmount, ARFPaidRemarks,
                                    IsDelayApplicationFiled, DelayApplicationPath, IsDelayCondonedAdverse, DelayCondonedOrderPath, IsMedicalInsuranceClaimed,
                                    IsFIRFiled, IsChargeSheetFiled, IsBusCameraInstalled, IsCameraFootageProduced, IsPhotographProduced, PhotographNotProducedReason,
                                    PoliceSketchExhibitNo, IsPoliceSketchEnclosed, IsEvidenceBasedOnSecurityReport, SecurityReportNoEvidenceReason,
                                    IsImpleadingAppFiled, ImpleadingAppNotFiledReason, IsVictimSalaried, IsIncomeCrossVerified, DoesIncomeTallyWithDocuments, IsMedicalBillsVerified,
                                    IsAmountDepositedInEP, IsEPFiled, EPDepositedAmount, FutureProspectsPercentage, PersonalExpensesDeduction, Multiplier,
                                    LossOfDependency, LossOfConsortium, LossOfEstate, FuneralExpenses, LossOfLoveAffection, MedicalExpenseOther, AgeProofUploadPath, PainSufferings, ConveyanceAttendant, LossOfFutureIncome, LossOfIncomeLaidUp, LossOfAmenities, FutureMedicalExpenses, InjuryOtherExpense, CustomCompensation, EPNumber, EPCourt, EPStage, EPNextHearingDate, RoundOffAmount, AsPerECourts)
                                VALUES (@CaseID, @BusInsurance, @IsBusInsured, @ClaimPetitionDate, @AwardDate, @MannerOfAccident, @MannerOfAccidentRO,
                                    @ObjectionFiled, @ObjectionRemarks, @RWType, @TR18Remarks, @IsDeceasedInTR18, @TR18Path, @IsAlleged,
                                    @DoubleClaimFlag, @DoubleClaimDetails, @SecurityRequired, @SecurityPath, @GovIDPath,
                                    @DisposedOn, @AppliedOn, @CopyRemarks, @IssuedOn, @ReceivedOn, @CopyDelivered,
                                    @AwardAmount, @InterestRate, @VictimAge, @Occupation, @Income, @IncomePeriod, @Injury,
                                    @PunishmentStatus, @PunishmentPath, @PunishmentRemarks, @DisabilityPercentage, @InterimAmount, @InterimDeducted,
                                    @AdvOpinion, @LOOpinion, @DCOpinion, @ForwardStatus, @ClosureRemarks, @ClosureDate, @OutwardNo, @OutwardDate,
                                    @IsCorpLiable, @LiabilityPercentage, @LiabilityRemarks, @AdverseJudgmentUploadPath, @FutureProspectus, @InjuryType, @TreatedDoc,
                                    @IsSTPassenger, @IsMedicalExpensesPaid, @MedicalPaidAmount, @MedicalPaidRemarks, @IsARFAmountPaid, @ARFPaidAmount, @ARFPaidRemarks,
                                    @IsDelayApplicationFiled, @DelayApplicationPath, @IsDelayCondonedAdverse, @DelayCondonedOrderPath, @IsMedicalInsuranceClaimed,
                                    @IsFIRFiled, @IsChargeSheetFiled, @IsBusCameraInstalled, @IsCameraFootageProduced, @IsPhotographProduced, @PhotographNotProducedReason,
                                    @PoliceSketchExhibitNo, @IsPoliceSketchEnclosed, @IsEvidenceBasedOnSecurityReport, @SecurityReportNoEvidenceReason,
                                    @IsImpleadingAppFiled, @ImpleadingAppNotFiledReason, @IsVictimSalaried, @IsIncomeCrossVerified, @DoesIncomeTallyWithDocuments, @IsMedicalBillsVerified,
                                    @IsAmountDepositedInEP, @IsEPFiled, @EPDepositedAmount, @FutureProspectsPercentage, @PersonalExpensesDeduction, @Multiplier,
                                    @LossOfDependency, @LossOfConsortium, @LossOfEstate, @FuneralExpenses, @LossOfLoveAffection, @MedicalExpenseOther, @AgeProofPath, @PainSufferings, @ConveyanceAttendant, @LossOfFutureIncome, @LossOfIncomeLaidUp, @LossOfAmenities, @FutureMedicalExpenses, @InjuryOtherExpense, @CustomCompensation, @EPNumber, @EPCourt, @EPStage, @EPNextHearingDate, @RoundOffAmount, @AsPerECourts)";

                            var advCmd = new SqlCommand(advQuery, conn, trans);
                            advCmd.Parameters.AddWithValue("@CaseID", model.CaseID);
                            advCmd.Parameters.AddWithValue("@BusInsurance", model.AdverseAward.BusInsuranceDetails ?? (object)DBNull.Value);
                            advCmd.Parameters.AddWithValue("@IsBusInsured", model.AdverseAward.IsBusInsured);
                            advCmd.Parameters.AddWithValue("@ClaimPetitionDate", model.AdverseAward.ClaimPetitionDate ?? (object)DBNull.Value);
                            advCmd.Parameters.AddWithValue("@AwardDate", model.AdverseAward.AwardDate ?? (object)DBNull.Value);
                            advCmd.Parameters.AddWithValue("@MannerOfAccident", model.AdverseAward.MannerOfAccident ?? (object)DBNull.Value);
                            advCmd.Parameters.AddWithValue("@MannerOfAccidentRO", model.AdverseAward.MannerOfAccidentRO ?? (object)DBNull.Value);
                            advCmd.Parameters.AddWithValue("@ObjectionFiled", model.AdverseAward.ObjectionFiled);
                            advCmd.Parameters.AddWithValue("@ObjectionRemarks", model.AdverseAward.ObjectionRemarks ?? (object)DBNull.Value);
                            advCmd.Parameters.AddWithValue("@RWType", model.AdverseAward.RWType ?? (object)DBNull.Value);
                            advCmd.Parameters.AddWithValue("@TR18Remarks", model.AdverseAward.TR18Remarks ?? (object)DBNull.Value);
                            advCmd.Parameters.AddWithValue("@IsDeceasedInTR18", model.AdverseAward.IsDeceasedInTR18);
                            advCmd.Parameters.AddWithValue("@TR18Path", model.AdverseAward.TR18UploadPath ?? (object)DBNull.Value);
                            advCmd.Parameters.AddWithValue("@IsAlleged", model.AdverseAward.IsAllegedAccident);
                            advCmd.Parameters.AddWithValue("@DoubleClaimFlag", model.AdverseAward.DoubleClaimFlag);
                            advCmd.Parameters.AddWithValue("@DoubleClaimDetails", model.AdverseAward.DoubleClaimDetails ?? (object)DBNull.Value);
                            advCmd.Parameters.AddWithValue("@SecurityRequired", model.AdverseAward.SecurityRequired);
                            advCmd.Parameters.AddWithValue("@SecurityPath", model.AdverseAward.SecurityUploadPath ?? (object)DBNull.Value);
                            advCmd.Parameters.AddWithValue("@GovIDPath", model.AdverseAward.GovIDProofUploadPath ?? (object)DBNull.Value);
                            advCmd.Parameters.AddWithValue("@DisposedOn", model.AdverseAward.DisposedOnDate ?? (object)DBNull.Value);
                            advCmd.Parameters.AddWithValue("@AppliedOn", model.AdverseAward.CopyAppliedDate ?? (object)DBNull.Value);
                            advCmd.Parameters.AddWithValue("@CopyRemarks", model.AdverseAward.CertifiedCopyRemarks ?? (object)DBNull.Value);
                            advCmd.Parameters.AddWithValue("@IssuedOn", model.AdverseAward.CopyIssuedDate ?? (object)DBNull.Value);
                            advCmd.Parameters.AddWithValue("@ReceivedOn", model.AdverseAward.CopyReceivedDate ?? (object)DBNull.Value);
                            advCmd.Parameters.AddWithValue("@CopyDelivered", model.AdverseAward.CopyDeliveredDate ?? (object)DBNull.Value);
                            advCmd.Parameters.AddWithValue("@AwardAmount", model.AdverseAward.AwardAmount ?? (object)DBNull.Value);
                            advCmd.Parameters.AddWithValue("@InterestRate", model.AdverseAward.InterestRate ?? (object)DBNull.Value);
                            advCmd.Parameters.AddWithValue("@VictimAge", model.AdverseAward.VictimAge ?? (object)DBNull.Value);
                            advCmd.Parameters.AddWithValue("@Occupation", model.AdverseAward.Occupation ?? (object)DBNull.Value);
                            advCmd.Parameters.AddWithValue("@Income", model.AdverseAward.IncomeConsidered ?? (object)DBNull.Value);
                            advCmd.Parameters.AddWithValue("@IncomePeriod", model.AdverseAward.IncomePeriod ?? "monthly");
                            advCmd.Parameters.AddWithValue("@InjuryType", model.AdverseAward.InjuryType ?? (object)DBNull.Value);
                            advCmd.Parameters.AddWithValue("@Injury", model.AdverseAward.InjuryDetails ?? (object)DBNull.Value);
                            advCmd.Parameters.AddWithValue("@PunishmentStatus", model.AdverseAward.DriverPunishmentStatus ?? (object)DBNull.Value);
                            advCmd.Parameters.AddWithValue("@PunishmentPath", model.AdverseAward.PunishmentOrderUploadPath ?? (object)DBNull.Value);
                            advCmd.Parameters.AddWithValue("@PunishmentRemarks", model.AdverseAward.PunishmentRemarks ?? (object)DBNull.Value);
                            advCmd.Parameters.AddWithValue("@DisabilityPercentage", model.AdverseAward.DisabilityPercentage ?? (object)DBNull.Value);
                            advCmd.Parameters.AddWithValue("@InterimAmount", model.AdverseAward.InterimCompAmount ?? (object)DBNull.Value);
                            advCmd.Parameters.AddWithValue("@InterimDeducted", model.AdverseAward.InterimCompDeducted);
                            advCmd.Parameters.AddWithValue("@AdvOpinion", model.AdverseAward.AdvocateOpinion ?? (object)DBNull.Value);
                            advCmd.Parameters.AddWithValue("@LOOpinion", model.AdverseAward.LOOpinion ?? (object)DBNull.Value);
                            advCmd.Parameters.AddWithValue("@DCOpinion", model.AdverseAward.DCOpinion ?? (object)DBNull.Value);
                            advCmd.Parameters.AddWithValue("@ForwardStatus", model.AdverseAward.ForwardingStatus ?? (object)DBNull.Value);
                            advCmd.Parameters.AddWithValue("@ClosureRemarks", model.AdverseAward.ClosureRemarks ?? (object)DBNull.Value);
                            advCmd.Parameters.AddWithValue("@ClosureDate", model.AdverseAward.ClosureDate ?? (object)DBNull.Value);
                            advCmd.Parameters.AddWithValue("@OutwardNo", model.AdverseAward.OutwardNumber ?? (object)DBNull.Value);
                            advCmd.Parameters.AddWithValue("@OutwardDate", model.AdverseAward.OutwardDate ?? (object)DBNull.Value);
                            advCmd.Parameters.AddWithValue("@IsCorpLiable", model.AdverseAward.IsCorpLiable);
                            advCmd.Parameters.AddWithValue("@LiabilityPercentage", adverseAward.LiabilityPercentage ?? (object)DBNull.Value);
                            advCmd.Parameters.AddWithValue("@LiabilityRemarks", adverseAward.LiabilityRemarks ?? (object)DBNull.Value);
                            advCmd.Parameters.AddWithValue("@AdverseJudgmentUploadPath", adverseAward.AdverseJudgmentUploadPath ?? (object)DBNull.Value);
                            advCmd.Parameters.AddWithValue("@FutureProspectus", adverseAward.FutureProspectus ?? (object)DBNull.Value);
                            advCmd.Parameters.AddWithValue("@TreatedDoc", adverseAward.TreatedDocFlag);

                            advCmd.Parameters.AddWithValue("@IsSTPassenger", model.IsSTPassenger);
                            advCmd.Parameters.AddWithValue("@IsMedicalExpensesPaid", model.IsMedicalExpensesPaid);
                            advCmd.Parameters.AddWithValue("@MedicalPaidAmount", model.MedicalPaidAmount ?? (object)DBNull.Value);
                            advCmd.Parameters.AddWithValue("@MedicalPaidRemarks", model.MedicalPaidRemarks ?? (object)DBNull.Value);
                            advCmd.Parameters.AddWithValue("@IsARFAmountPaid", model.IsARFAmountPaid);
                            advCmd.Parameters.AddWithValue("@ARFPaidAmount", model.ARFPaidAmount ?? (object)DBNull.Value);
                            advCmd.Parameters.AddWithValue("@ARFPaidRemarks", model.ARFPaidRemarks ?? (object)DBNull.Value);
                            advCmd.Parameters.AddWithValue("@IsDelayApplicationFiled", adverseAward.IsDelayApplicationFiled);
                            advCmd.Parameters.AddWithValue("@DelayApplicationPath", adverseAward.DelayApplicationPath ?? (object)DBNull.Value);
                            advCmd.Parameters.AddWithValue("@IsDelayCondonedAdverse", (object)adverseAward.IsDelayCondonedAdverse ?? DBNull.Value);
                            advCmd.Parameters.AddWithValue("@DelayCondonedOrderPath", adverseAward.DelayCondonedOrderPath ?? (object)DBNull.Value);
                            advCmd.Parameters.AddWithValue("@IsMedicalInsuranceClaimed", adverseAward.IsMedicalInsuranceClaimed);


                            advCmd.Parameters.AddWithValue("@IsFIRFiled", (object)adverseAward.IsFIRFiled ?? DBNull.Value);
                            advCmd.Parameters.AddWithValue("@IsChargeSheetFiled", (object)adverseAward.IsChargeSheetFiled ?? DBNull.Value);
                            advCmd.Parameters.AddWithValue("@IsBusCameraInstalled", (object)adverseAward.IsBusCameraInstalled ?? DBNull.Value);
                            advCmd.Parameters.AddWithValue("@IsCameraFootageProduced", (object)adverseAward.IsCameraFootageProduced ?? DBNull.Value);
                            advCmd.Parameters.AddWithValue("@IsPhotographProduced", (object)adverseAward.IsPhotographProduced ?? DBNull.Value);
                            advCmd.Parameters.AddWithValue("@PhotographNotProducedReason", (object)adverseAward.PhotographNotProducedReason ?? DBNull.Value);
                            advCmd.Parameters.AddWithValue("@PoliceSketchExhibitNo", (object)adverseAward.PoliceSketchExhibitNo ?? DBNull.Value);
                            advCmd.Parameters.AddWithValue("@IsPoliceSketchEnclosed", (object)adverseAward.IsPoliceSketchEnclosed ?? DBNull.Value);
                            advCmd.Parameters.AddWithValue("@IsEvidenceBasedOnSecurityReport", (object)adverseAward.IsEvidenceBasedOnSecurityReport ?? DBNull.Value);
                            advCmd.Parameters.AddWithValue("@SecurityReportNoEvidenceReason", (object)adverseAward.SecurityReportNoEvidenceReason ?? DBNull.Value);
                            advCmd.Parameters.AddWithValue("@IsImpleadingAppFiled", (object)adverseAward.IsImpleadingAppFiled ?? DBNull.Value);
                            advCmd.Parameters.AddWithValue("@ImpleadingAppNotFiledReason", (object)adverseAward.ImpleadingAppNotFiledReason ?? DBNull.Value);
                            advCmd.Parameters.AddWithValue("@IsVictimSalaried", (object)adverseAward.IsVictimSalaried ?? DBNull.Value);
                            advCmd.Parameters.AddWithValue("@IsIncomeCrossVerified", (object)adverseAward.IsIncomeCrossVerified ?? DBNull.Value);
                            advCmd.Parameters.AddWithValue("@DoesIncomeTallyWithDocuments", (object)adverseAward.DoesIncomeTallyWithDocuments ?? DBNull.Value);
                            advCmd.Parameters.AddWithValue("@IsMedicalBillsVerified", (object)adverseAward.IsMedicalBillsVerified ?? DBNull.Value);
                            advCmd.Parameters.AddWithValue("@IsAmountDepositedInEP", (object)adverseAward.IsAmountDepositedInEP ?? DBNull.Value);
                            advCmd.Parameters.AddWithValue("@IsEPFiled", (object)adverseAward.IsEPFiled ?? DBNull.Value);
                            advCmd.Parameters.AddWithValue("@EPDepositedAmount", (object)adverseAward.EPDepositedAmount ?? DBNull.Value);
                            advCmd.Parameters.AddWithValue("@FutureProspectsPercentage", (object)adverseAward.FutureProspectsPercentage ?? DBNull.Value);
                            advCmd.Parameters.AddWithValue("@PersonalExpensesDeduction", (object)adverseAward.PersonalExpensesDeduction ?? DBNull.Value);
                            advCmd.Parameters.AddWithValue("@Multiplier", (object)adverseAward.Multiplier ?? DBNull.Value);
                            advCmd.Parameters.AddWithValue("@LossOfDependency", (object)adverseAward.LossOfDependency ?? DBNull.Value);
                            advCmd.Parameters.AddWithValue("@LossOfConsortium", (object)adverseAward.LossOfConsortium ?? DBNull.Value);
                            advCmd.Parameters.AddWithValue("@LossOfEstate", (object)adverseAward.LossOfEstate ?? DBNull.Value);
                            advCmd.Parameters.AddWithValue("@FuneralExpenses", (object)adverseAward.FuneralExpenses ?? DBNull.Value);
                            advCmd.Parameters.AddWithValue("@LossOfLoveAffection", (object)adverseAward.LossOfLoveAffection ?? DBNull.Value);
                             advCmd.Parameters.AddWithValue("@MedicalExpenseOther", (object)adverseAward.MedicalExpenseOther ?? DBNull.Value);
                             advCmd.Parameters.AddWithValue("@AgeProofPath", (object)adverseAward.AgeProofUploadPath ?? DBNull.Value);
                             advCmd.Parameters.AddWithValue("@PainSufferings", (object)adverseAward.PainSufferings ?? DBNull.Value);
                             advCmd.Parameters.AddWithValue("@ConveyanceAttendant", (object)adverseAward.ConveyanceAttendant ?? DBNull.Value);
                             advCmd.Parameters.AddWithValue("@LossOfFutureIncome", (object)adverseAward.LossOfFutureIncome ?? DBNull.Value);
                             advCmd.Parameters.AddWithValue("@LossOfIncomeLaidUp", (object)adverseAward.LossOfIncomeLaidUp ?? DBNull.Value);
                             advCmd.Parameters.AddWithValue("@LossOfAmenities", (object)adverseAward.LossOfAmenities ?? DBNull.Value);
                             advCmd.Parameters.AddWithValue("@FutureMedicalExpenses", (object)adverseAward.FutureMedicalExpenses ?? DBNull.Value);
                              advCmd.Parameters.AddWithValue("@InjuryOtherExpense", (object)adverseAward.InjuryOtherExpense ?? DBNull.Value);
                              advCmd.Parameters.AddWithValue("@EPNumber", (object)adverseAward.EPNumber ?? DBNull.Value);
                              advCmd.Parameters.AddWithValue("@EPCourt", (object)adverseAward.EPCourt ?? DBNull.Value);
                              advCmd.Parameters.AddWithValue("@EPStage", (object)adverseAward.EPStage ?? DBNull.Value);
                              advCmd.Parameters.AddWithValue("@EPNextHearingDate", (object)adverseAward.EPNextHearingDate ?? DBNull.Value);
                               advCmd.Parameters.AddWithValue("@RoundOffAmount", (object)adverseAward.RoundOffAmount ?? DBNull.Value);
                               advCmd.Parameters.AddWithValue("@AsPerECourts", (object)adverseAward.AsPerECourts ?? DBNull.Value);
if (adverseAward.CustomCompensationHeads != null && adverseAward.CustomCompensationHeads.Count > 0)
                                 advCmd.Parameters.AddWithValue("@CustomCompensation", JsonSerializer.Serialize(adverseAward.CustomCompensationHeads));
                             else
                                 advCmd.Parameters.AddWithValue("@CustomCompensation", DBNull.Value);


                            advCmd.ExecuteNonQuery();

                            if (model.DisposalResult == "Against" && model.AdverseAward != null)
                            {
                                var delAdvPWCmd = new SqlCommand("DELETE FROM MVC_CASE_ADVERSE_PW WHERE CaseID = @CaseID AND Type NOT LIKE 'TP_%'", conn, trans);
                                delAdvPWCmd.Parameters.AddWithValue("@CaseID", model.CaseID);
                                delAdvPWCmd.ExecuteNonQuery();
                                var delAdvRWCmd = new SqlCommand("DELETE FROM MVC_CASE_ADVERSE_RW WHERE CaseID = @CaseID", conn, trans);
                                delAdvRWCmd.Parameters.AddWithValue("@CaseID", model.CaseID);
                                delAdvRWCmd.ExecuteNonQuery();
                                var delAdvConnCmd = new SqlCommand("DELETE FROM MVC_CASE_ADVERSE_CONNECTED WHERE CaseID = @CaseID", conn, trans);
                                delAdvConnCmd.Parameters.AddWithValue("@CaseID", model.CaseID);
                                delAdvConnCmd.ExecuteNonQuery();
                                var delDocsCmd = new SqlCommand("DELETE FROM MVC_CASE_ADVERSE_DOCS WHERE CaseID = @CaseID", conn, trans);
                                delDocsCmd.Parameters.AddWithValue("@CaseID", model.CaseID);
                                delDocsCmd.ExecuteNonQuery();

                                foreach (var pw in model.AdverseAward.PetitionerPWNames)
                                {
                                    var pwCmd = new SqlCommand("INSERT INTO MVC_CASE_ADVERSE_PW (CaseID, PWName, Type) VALUES (@CaseID, @Name, 'Petitioner')", conn, trans);
                                    pwCmd.Parameters.AddWithValue("@CaseID", model.CaseID);
                                    pwCmd.Parameters.AddWithValue("@Name", pw);
                                    pwCmd.ExecuteNonQuery();
                                }
                                foreach (var dr in model.AdverseAward.Doctors)
                                {
                                    var pwCmd = new SqlCommand("INSERT INTO MVC_CASE_ADVERSE_PW (CaseID, PWName, Type, IsTreated) VALUES (@CaseID, @Name, 'Doctor', @IsTreated)", conn, trans);
                                    pwCmd.Parameters.AddWithValue("@CaseID", model.CaseID);
                                    pwCmd.Parameters.AddWithValue("@Name", dr.DoctorName);
                                    pwCmd.Parameters.AddWithValue("@IsTreated", dr.IsTreated);
                                    pwCmd.ExecuteNonQuery();
                                }
                                foreach (var rw in model.AdverseAward.RWNames)
                                {
                                    var rwCmd = new SqlCommand("INSERT INTO MVC_CASE_ADVERSE_RW (CaseID, RWName) VALUES (@CaseID, @Name)", conn, trans);
                                    rwCmd.Parameters.AddWithValue("@CaseID", model.CaseID);
                                    rwCmd.Parameters.AddWithValue("@Name", rw);
                                    rwCmd.ExecuteNonQuery();
                                }
                                foreach (var advConn in model.AdverseAward.ConnectedCases)
                                {
                                    var acCmd = new SqlCommand(@"INSERT INTO MVC_CASE_ADVERSE_CONNECTED 
                                        (CaseID, CaseDetails, Status, CurrentStage, AwardAmount, InterestRate, VictimAge, Occupation, IncomeConsidered, InjuryDetails) 
                                        VALUES (@CaseID, @Details, @Status, @CurrentStage, @AwardAmount, @InterestRate, @VictimAge, @Occupation, @IncomeConsidered, @InjuryDetails)", conn, trans);
                                    acCmd.Parameters.AddWithValue("@CaseID", model.CaseID);
                                    string details = $"{advConn.ConnectedMVCNo} / {advConn.ConnectedYear}";
                                    if (!string.IsNullOrEmpty(advConn.MACT)) details += $" - {advConn.MACT}";
                                    acCmd.Parameters.AddWithValue("@Details", details);
                                    acCmd.Parameters.AddWithValue("@Status", advConn.Status ?? (object)DBNull.Value);
                                    acCmd.Parameters.AddWithValue("@CurrentStage", advConn.CurrentStage ?? (object)DBNull.Value);

                                    // Award Details
                                    acCmd.Parameters.AddWithValue("@AwardAmount", advConn.AwardDetails.AwardAmount ?? (object)DBNull.Value);
                                    acCmd.Parameters.AddWithValue("@InterestRate", advConn.AwardDetails.InterestRate ?? (object)DBNull.Value);
                                    acCmd.Parameters.AddWithValue("@VictimAge", advConn.AwardDetails.VictimAge ?? (object)DBNull.Value);
                                    acCmd.Parameters.AddWithValue("@Occupation", advConn.AwardDetails.Occupation ?? (object)DBNull.Value);
                                    acCmd.Parameters.AddWithValue("@IncomeConsidered", advConn.AwardDetails.IncomeConsidered ?? (object)DBNull.Value);
                                    acCmd.Parameters.AddWithValue("@InjuryDetails", advConn.AwardDetails.InjuryDetails ?? (object)DBNull.Value);
                                    acCmd.ExecuteNonQuery();
                                }

                                // Enclosed Documents
                                if (model.AdverseAward.EnclosedDocuments != null)
                                {
                                    foreach (var doc in model.AdverseAward.EnclosedDocuments)
                                    {
                                        if (!string.IsNullOrEmpty(doc.DocName))
                                        {
                                            var docCmd = new SqlCommand("INSERT INTO MVC_CASE_ADVERSE_DOCS (CaseID, DocName, PageCount) VALUES (@CaseID, @DocName, @PageCount)", conn, trans);
                                            docCmd.Parameters.AddWithValue("@CaseID", model.CaseID);
                                            docCmd.Parameters.AddWithValue("@DocName", doc.DocName);
                                            docCmd.Parameters.AddWithValue("@PageCount", doc.PageCount ?? (object)DBNull.Value);
                                            docCmd.ExecuteNonQuery();
                                        }
                                    }
                                }
                            }

                            // Re-insert Third Party Evidence
                            if (model.ThirdPartyFlag)
                            {
                                foreach (var ev in model.RespondentEvidence)
                                {
                                    if (!string.IsNullOrEmpty(ev.Name))
                                    {
                                        var evCmd = new SqlCommand("INSERT INTO MVC_CASE_ADVERSE_PW (CaseID, PWName, Type, Remark) VALUES (@CaseID, @Name, 'TP_Respondent', @Remark)", conn, trans);
                                        evCmd.Parameters.AddWithValue("@CaseID", model.CaseID);
                                        evCmd.Parameters.AddWithValue("@Name", ev.Name);
                                        evCmd.Parameters.AddWithValue("@Remark", ev.Remark ?? (object)DBNull.Value);
                                        evCmd.ExecuteNonQuery();
                                    }
                                }
                                foreach (var ev in model.CorpEvidence)
                                {
                                    if (!string.IsNullOrEmpty(ev.Name))
                                    {
                                        var evCmd = new SqlCommand("INSERT INTO MVC_CASE_ADVERSE_PW (CaseID, PWName, Type, Designation) VALUES (@CaseID, @Name, 'TP_Corporation', @Designation)", conn, trans);
                                        evCmd.Parameters.AddWithValue("@CaseID", model.CaseID);
                                        evCmd.Parameters.AddWithValue("@Name", ev.Name);
                                        evCmd.Parameters.AddWithValue("@Designation", ev.Designation ?? (object)DBNull.Value);
                                        evCmd.ExecuteNonQuery();
                                    }
                                }
                            }

                        trans.Commit();
                        return true;
                    }
                    catch { trans.Rollback(); throw; }
                }
            }
        }

        public IEnumerable<MVCCaseViewModel> GetAllCases(int divisionId = 0, int pageNumber = 1, int pageSize = 10, string? search = null, string? status = null)
        {
            var cases = new List<MVCCaseViewModel>();
            try
            {
                int offset = (pageNumber - 1) * pageSize;

                string query = @"
                    SELECT 
                        c.CaseID, c.DivisionID, c.MVCNo, c.MVCYear, c.VehicleNo, c.AccidentDate,
                        c.CaseType, c.ClaimType, c.CurrentStage, c.DisposalResult, c.CreatedAt, c.NextHearingDate,
                        c.CNRNumber, c.EstCode, c.CaseTypeCode, c.CaseStatus, c.CourtHall,
                        d.DivisionNameEnglish as DivisionName, 
                        m.MACTName,
                        adv.ForwardingStatus,
                        COALESCE(adv.IsAllegedAccident, 0) as IsAllegedAccident,
                        CASE WHEN ad.AppealID IS NOT NULL THEN 1 ELSE 0 END as HasAppeal,
                        CASE WHEN ad.InitialAction IS NOT NULL AND ad.InitialAction != '' AND ad.InitialAction != 'Select...' 
                             OR (ad.Opinion_CLO IS NOT NULL AND ad.Opinion_CLO != '')
                             OR (ad.CorpMFANumber IS NOT NULL AND ad.CorpMFANumber != '')
                             OR (ad.ClaimantMFANumber IS NOT NULL AND ad.ClaimantMFANumber != '')
                        THEN 1 ELSE 0 END as HasCentralOfficeAction,
                        COALESCE(NULLIF(LTRIM(RTRIM(ad.CorpMFANumber)), ''), NULLIF(LTRIM(RTRIM(ad.ClaimantMFANumber)), '')) as MFANo,
                        COALESCE(NULLIF(ad.CorpMFAYear, 0), NULLIF(ad.ClaimantMFAYear, 0)) as MFAYear,
                        ad.CorpMFANumber as CorpMFANo, ad.CorpMFAYear as CorpMFAYearRow,
                        ad.ClaimantMFANumber as ClaimantMFANo, ad.ClaimantMFAYear as ClaimantMFAYearRow,
                        COALESCE(NULLIF(LTRIM(RTRIM(ad.CorpMFAEntrustmentNo)), ''), NULLIF(LTRIM(RTRIM(ad.ClaimantMFAEntrustmentNo)), '')) as MFAEntrustmentNo,
                        COALESCE(ad.CorpMFAEntrustmentDate, ad.ClaimantMFAEntrustmentDate) as MFAEntrustmentDate
                    FROM MVC_CASES c
                    JOIN DIVISION_MASTER d ON c.DivisionID = d.DivisionID
                    JOIN MACT_MASTER m ON c.MACTID = m.MACTID
                    LEFT JOIN MVC_CASE_ADVERSE_DETAILS adv ON c.CaseID = adv.CaseID
                    LEFT JOIN CASE_VIEW_TRACKING vt ON c.CaseID = vt.CaseID
                    LEFT JOIN APPEAL_DETAILS ad ON c.CaseID = ad.CaseID
                    WHERE 1=1";
                
                var paramsList = new List<SqlParameter>();

                if (divisionId > 0 && divisionId != 5) 
                {
                    query += " AND c.DivisionID = @DivisionID";
                    paramsList.Add(new SqlParameter("@DivisionID", divisionId));
                }

                // Central Office Filter
                bool isCentralOffice = divisionId == 0 || divisionId == 5;
                if (isCentralOffice && string.IsNullOrEmpty(search))
                {
                    // Default view (Inbox) shows only those forwarded to CO + Unread
                    if (status == "SentToCO" || string.IsNullOrEmpty(status))
                    {
                        query += " AND adv.ForwardingStatus = 'Sent to Central Office'";
                        query += " AND vt.ViewID IS NULL"; // Hide viewed cases ONLY in Inbox/Default view
                        query += @" AND NOT EXISTS (
                            SELECT 1 FROM APPEAL_DETAILS a 
                            WHERE (a.CaseID = c.CaseID OR (NULLIF(LTRIM(RTRIM(a.ClaimantMVCNumber)), '') IS NOT NULL AND a.ClaimantMVCNumber = c.MVCNo AND a.ClaimantMVCYear = c.MVCYear))
                            AND (
                                NULLIF(LTRIM(RTRIM(a.CorpMFANumber)), '') IS NOT NULL 
                                OR NULLIF(LTRIM(RTRIM(a.ClaimantMFANumber)), '') IS NOT NULL
                                OR NULLIF(LTRIM(RTRIM(a.CrossMFANumber)), '') IS NOT NULL
                                OR a.IsPendingForFiling = 1
                                OR UPPER(LTRIM(RTRIM(a.InitialAction))) = 'APPEAL'
                            )
                        )";
                    }
                    // For other statuses (SLPPending, etc.), we show ALL cases (Read + Unread)
                }
                // If searching or specific filter active, CO can see any case (global lookup requirement)

                if (!string.IsNullOrEmpty(search))
                {
                    // Check if search contains "/" for combined MVCNo/Year search (e.g., "21/2023")
                    if (search.Contains("/"))
                    {
                        var parts = search.Split('/');
                        if (parts.Length == 2 && int.TryParse(parts[1], out int year))
                        {
                            // Combined search: MVCNo and Year OR MFA No and Year
                            query += @" AND (
                                (c.MVCNo LIKE @SearchMVCNo AND c.MVCYear = @SearchYear) OR
                                EXISTS (
                                    SELECT 1 FROM APPEAL_DETAILS ad 
                                    WHERE ad.CaseID = c.CaseID 
                                    AND (
                                        (ad.CorpMFANumber LIKE @SearchMVCNo AND ad.CorpMFAYear = @SearchYear) OR
                                        (ad.ClaimantMFANumber LIKE @SearchMVCNo AND ad.ClaimantMFAYear = @SearchYear)
                                    )
                                )
                            )";
                            paramsList.Add(new SqlParameter("@SearchMVCNo", "%" + parts[0].Trim() + "%"));
                            paramsList.Add(new SqlParameter("@SearchYear", year));
                        }
                        else
                        {
                            // Invalid format, fall back to regular search
                            query += @" AND (
                                c.MVCNo LIKE @Search OR 
                                c.VehicleNo LIKE @Search OR 
                                CAST(c.MVCYear AS NVARCHAR) LIKE @Search OR
                                EXISTS (
                                    SELECT 1 FROM APPEAL_DETAILS ad 
                                    WHERE ad.CaseID = c.CaseID 
                                    AND (
                                        ad.CorpMFANumber LIKE @Search OR 
                                        ad.ClaimantMFANumber LIKE @Search
                                    )
                                )
                            )";
                            paramsList.Add(new SqlParameter("@Search", "%" + search + "%"));
                        }
                    }
                    else
                    {
                        // Regular search
                        query += @" AND (
                            c.MVCNo LIKE @Search OR 
                            c.VehicleNo LIKE @Search OR 
                            CAST(c.MVCYear AS NVARCHAR) LIKE @Search OR 
                            m.MACTName LIKE @Search OR
                            EXISTS (
                                SELECT 1 FROM APPEAL_DETAILS ad 
                                WHERE ad.CaseID = c.CaseID 
                                AND (
                                    ad.CorpMFANumber LIKE @Search OR 
                                    ad.ClaimantMFANumber LIKE @Search
                                )
                            )
                        )";
                        paramsList.Add(new SqlParameter("@Search", "%" + search + "%"));
                    }
                }

                if (!string.IsNullOrEmpty(status) && status != "all")
                {
                    if (status == "Pending")
                        query += " AND (c.DisposalResult IS NULL OR c.DisposalResult = 'Pending')";
                    else if (status == "SentToCO" || status == "SentToCO_All")
                    {
                        query += " AND adv.ForwardingStatus = 'Sent to Central Office'";
                        query += @" AND NOT EXISTS (
                            SELECT 1 FROM APPEAL_DETAILS a 
                            WHERE (a.CaseID = c.CaseID OR (NULLIF(LTRIM(RTRIM(a.ClaimantMVCNumber)), '') IS NOT NULL AND a.ClaimantMVCNumber = c.MVCNo AND a.ClaimantMVCYear = c.MVCYear))
                            AND (
                                NULLIF(LTRIM(RTRIM(a.CorpMFANumber)), '') IS NOT NULL 
                                OR NULLIF(LTRIM(RTRIM(a.ClaimantMFANumber)), '') IS NOT NULL
                                OR NULLIF(LTRIM(RTRIM(a.CrossMFANumber)), '') IS NOT NULL
                                OR a.IsPendingForFiling = 1
                                OR UPPER(LTRIM(RTRIM(a.InitialAction))) = 'APPEAL'
                            )
                        )";
                    }
                    else if (status == "Favor")
                        query += " AND c.DisposalResult = 'Favor'";
                    else if (status == "Against")
                        query += " AND c.DisposalResult = 'Against'";
                    else if (status == "PendingDecision")
                        query += " AND EXISTS (SELECT 1 FROM APPEAL_DETAILS a WHERE a.CaseID = c.CaseID AND (UPPER(LTRIM(RTRIM(a.InitialAction))) LIKE '%PENDING%DECISION%' OR UPPER(LTRIM(RTRIM(a.InitialAction))) LIKE '%PENDING%FOR%ACTION%'))";
                    else if (status == "PendingAtCA")
                        query += " AND EXISTS (SELECT 1 FROM APPEAL_DETAILS a WHERE a.CaseID = c.CaseID AND (UPPER(LTRIM(RTRIM(a.InitialAction))) LIKE '%PENDING%COMPETENT%AUTHORITY%' OR UPPER(LTRIM(RTRIM(a.InitialAction))) LIKE '%PENDING%AT%CA%'))";
                    else if (status == "SLPPending")
                        query += " AND EXISTS (SELECT 1 FROM APPEAL_DETAILS a WHERE a.CaseID = c.CaseID AND ((a.CorpSCStatus LIKE '%Pending%' AND NULLIF(LTRIM(RTRIM(a.CorpSCNumber)), '') IS NOT NULL) OR (a.ClaimantSCStatus LIKE '%Pending%' AND NULLIF(LTRIM(RTRIM(a.ClaimantSCNumber)), '') IS NOT NULL)))";
                    else if (status == "MFAPending")
                    {
                        query += @" AND EXISTS (
                            SELECT 1 FROM APPEAL_DETAILS a 
                            WHERE (a.CaseID = c.CaseID OR (NULLIF(LTRIM(RTRIM(a.ClaimantMVCNumber)), '') IS NOT NULL AND a.ClaimantMVCNumber = c.MVCNo AND a.ClaimantMVCYear = c.MVCYear))
                            AND (
                                ((a.CorpMFAStatus LIKE '%Pending%' OR a.CorpMFAStatus IS NULL OR a.CorpMFAStatus = '' OR a.CorpMFAStatus NOT LIKE '%Disposed%') AND (NULLIF(LTRIM(RTRIM(a.CorpMFANumber)), '') IS NOT NULL OR a.IsPendingForFiling = 1 OR UPPER(LTRIM(RTRIM(a.InitialAction))) = 'APPEAL'))
                                OR (NULLIF(LTRIM(RTRIM(a.ClaimantMFANumber)), '') IS NOT NULL AND (a.ClaimantMFAStatus LIKE '%Pending%' OR a.ClaimantMFAStatus IS NULL OR a.ClaimantMFAStatus = '' OR a.ClaimantMFAStatus NOT LIKE '%Disposed%'))
                                OR (NULLIF(LTRIM(RTRIM(a.CrossMFANumber)), '') IS NOT NULL)
                            )
                        )";
                    }
                    else if (status == "CompliancePending")
                        query += " AND EXISTS (SELECT 1 FROM APPEAL_DETAILS a WHERE a.CaseID = c.CaseID AND a.InitialAction IS NOT NULL AND a.InitialAction != '' AND UPPER(LTRIM(RTRIM(a.InitialAction))) NOT LIKE '%PENDING%COMPETENT%AUTHORITY%' AND UPPER(LTRIM(RTRIM(a.InitialAction))) NOT LIKE '%PENDING%DECISION%' AND (a.FinalComplianceStatus IS NULL OR a.FinalComplianceStatus != 'Complied'))";
                    else if (status == "NoAction")
                        query += " AND adv.ForwardingStatus IS NOT NULL AND adv.ForwardingStatus = 'Sent to Central Office' AND NOT EXISTS (SELECT 1 FROM APPEAL_DETAILS a WHERE a.CaseID = c.CaseID AND (NULLIF(LTRIM(RTRIM(a.InitialAction)), '') IS NOT NULL OR NULLIF(LTRIM(RTRIM(a.CorpMFANumber)), '') IS NOT NULL OR NULLIF(LTRIM(RTRIM(a.ClaimantMFANumber)), '') IS NOT NULL OR NULLIF(LTRIM(RTRIM(a.CrossMFANumber)), '') IS NOT NULL OR NULLIF(LTRIM(RTRIM(a.CorpSCNumber)), '') IS NOT NULL OR NULLIF(LTRIM(RTRIM(a.ClaimantSCNumber)), '') IS NOT NULL OR a.IsPendingForFiling = 1 OR a.StayGranted = 1 OR a.IsClaimantSCPending = 1))";
                    else if (status == "AwardsToday")
                        query += " AND CAST(adv.AwardDate AS DATE) = CAST(GETDATE() AS DATE)";
                    else if (status == "CriticalDelays")
                        query += " AND c.NextHearingDate < CAST(GETDATE() AS DATE) AND (c.DisposalResult IS NULL OR c.DisposalResult = 'Pending' OR c.DisposalResult = '')";
                    else if (status == "ComplianceDue")
                        query += " AND EXISTS (SELECT 1 FROM APPEAL_DETAILS a WHERE a.CaseID = c.CaseID AND a.FinalComplianceDate >= CAST(GETDATE() AS DATE) AND a.FinalComplianceDate <= DATEADD(day, 7, CAST(GETDATE() AS DATE)))";
                    else
                        query += " AND c.DisposalResult = @Status";
                    
                    if (status != "Decided" && status != "Pending" && status != "SentToCO" && status != "Favor" && status != "Against" 
                        && status != "PendingDecision" && status != "PendingAtCA" && status != "SLPPending" && status != "MFAPending" && status != "CompliancePending" && status != "NoAction" && status != "SentToCO_All"
                        && status != "AwardsToday" && status != "CriticalDelays" && status != "ComplianceDue") 
                         paramsList.Add(new SqlParameter("@Status", status));
                }

                query += " ORDER BY c.CreatedAt DESC OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY";

                paramsList.Add(new SqlParameter("@Offset", offset));
                paramsList.Add(new SqlParameter("@PageSize", pageSize));

                DataTable dt = _db.ExecuteQuery(query, paramsList.ToArray());

                foreach (DataRow row in dt.Rows)
                {
                    cases.Add(new MVCCaseViewModel
                    {
                        CaseID = row["CaseID"] != DBNull.Value ? Convert.ToInt32(row["CaseID"]) : 0,
                        DivisionID = row["DivisionID"] != DBNull.Value ? Convert.ToInt32(row["DivisionID"]) : 0,
                        DivisionName = row["DivisionName"]?.ToString() ?? "",
                        MACTName = row["MACTName"]?.ToString() ?? "",
                        MVCNo = row["MVCNo"]?.ToString() ?? "",
                        MVCYear = row["MVCYear"] != DBNull.Value ? Convert.ToInt32(row["MVCYear"]) : 0,
                        VehicleNo = row["VehicleNo"]?.ToString(),
                        AccidentDate = row["AccidentDate"] != DBNull.Value ? Convert.ToDateTime(row["AccidentDate"]) : null,
                        DisposalResult = row["DisposalResult"]?.ToString(),
                        CaseType = row["CaseType"]?.ToString() ?? "Pending",
                        ClaimType = row["ClaimType"]?.ToString(),
                        CurrentStage = row["CurrentStage"]?.ToString(),
                        CreatedAt = row["CreatedAt"] != DBNull.Value ? Convert.ToDateTime(row["CreatedAt"]) : DateTime.MinValue,
                        NextHearingDate = row["NextHearingDate"] != DBNull.Value ? Convert.ToDateTime(row["NextHearingDate"]) : null,
                        HasAppeal = row["HasAppeal"] != DBNull.Value && (int)row["HasAppeal"] == 1,
                        HasCentralOfficeAction = row["HasCentralOfficeAction"] != DBNull.Value && (int)row["HasCentralOfficeAction"] == 1,
                        ForwardingStatus = row["ForwardingStatus"]?.ToString(),
                        MFANo = row["MFANo"]?.ToString(),
                        MFAYear = row["MFAYear"] != DBNull.Value ? (int?)Convert.ToInt32(row["MFAYear"]) : null,
                        CorpMFANo = row["CorpMFANo"]?.ToString(),
                        CorpMFAYear = row["CorpMFAYearRow"] != DBNull.Value ? (int?)Convert.ToInt32(row["CorpMFAYearRow"]) : null,
                        ClaimantMFANo = row["ClaimantMFANo"]?.ToString(),
                        ClaimantMFAYear = row["ClaimantMFAYearRow"] != DBNull.Value ? (int?)Convert.ToInt32(row["ClaimantMFAYearRow"]) : null,
                        MFAEntrustmentNo = row["MFAEntrustmentNo"]?.ToString(),
                        MFAEntrustmentDate = row["MFAEntrustmentDate"] != DBNull.Value ? (DateTime?)row["MFAEntrustmentDate"] : null,
                        CNRNumber = row.Table.Columns.Contains("CNRNumber") && row["CNRNumber"] != DBNull.Value ? row["CNRNumber"]?.ToString() : null,
                        EstCode = row.Table.Columns.Contains("EstCode") && row["EstCode"] != DBNull.Value ? row["EstCode"]?.ToString() : null,
                        CaseTypeCode = row.Table.Columns.Contains("CaseTypeCode") && row["CaseTypeCode"] != DBNull.Value ? row["CaseTypeCode"]?.ToString() : null,
                        CaseStatus = row.Table.Columns.Contains("CaseStatus") && row["CaseStatus"] != DBNull.Value ? row["CaseStatus"]?.ToString() : null,
                        CourtHall = row.Table.Columns.Contains("CourtHall") && row["CourtHall"] != DBNull.Value ? row["CourtHall"]?.ToString() : null,
                        IsAllegedAccident = row.Table.Columns.Contains("IsAllegedAccident") && row["IsAllegedAccident"] != DBNull.Value && Convert.ToBoolean(row["IsAllegedAccident"])
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("CRITICAL ERROR IN GetAllCases: " + ex.Message);
                throw;
            }
            return cases;
        }

        public int GetTotalCaseCount(int divisionId = 0, string? search = null, string? status = null)
        {
            string query = @"SELECT COUNT(*) 
                FROM MVC_CASES c 
                LEFT JOIN MVC_CASE_ADVERSE_DETAILS adv ON c.CaseID = adv.CaseID
                LEFT JOIN CASE_VIEW_TRACKING vt ON c.CaseID = vt.CaseID
                LEFT JOIN MACT_MASTER m ON c.MACTID = m.MACTID
                WHERE 1=1";
            var paramsList = new List<SqlParameter>();

            if (divisionId > 0 && divisionId != 5)
            {
                query += " AND c.DivisionID = @DivisionID";
                paramsList.Add(new SqlParameter("@DivisionID", divisionId));
            }

            // Central Office Filter
            bool isCentralOffice = divisionId == 0 || divisionId == 5;
            if (isCentralOffice && string.IsNullOrEmpty(search))
            {
                if (status == "SentToCO" || string.IsNullOrEmpty(status))
                {
                    query += " AND adv.ForwardingStatus = 'Sent to Central Office'";
                    query += " AND vt.ViewID IS NULL"; 
                    query += @" AND NOT EXISTS (
                        SELECT 1 FROM APPEAL_DETAILS a 
                        WHERE (a.CaseID = c.CaseID OR (NULLIF(LTRIM(RTRIM(a.ClaimantMVCNumber)), '') IS NOT NULL AND a.ClaimantMVCNumber = c.MVCNo AND a.ClaimantMVCYear = c.MVCYear))
                        AND (
                            NULLIF(LTRIM(RTRIM(a.CorpMFANumber)), '') IS NOT NULL 
                            OR NULLIF(LTRIM(RTRIM(a.ClaimantMFANumber)), '') IS NOT NULL
                            OR NULLIF(LTRIM(RTRIM(a.CrossMFANumber)), '') IS NOT NULL
                            OR a.IsPendingForFiling = 1
                            OR UPPER(LTRIM(RTRIM(a.InitialAction))) = 'APPEAL'
                        )
                    )";
                }
            }

            if (!string.IsNullOrEmpty(search))
            {
                // Check if search contains "/" for combined MVCNo/Year search (e.g., "21/2023")
                if (search.Contains("/"))
                {
                    var parts = search.Split('/');
                    if (parts.Length == 2 && int.TryParse(parts[1], out int year))
                    {
                        // Combined search: MVCNo and Year OR MFA No and Year
                        query += @" AND (
                            (c.MVCNo LIKE @SearchMVCNo AND c.MVCYear = @SearchYear) OR
                            EXISTS (
                                SELECT 1 FROM APPEAL_DETAILS ad 
                                WHERE ad.CaseID = c.CaseID 
                                AND (
                                    (ad.CorpMFANumber LIKE @SearchMVCNo AND ad.CorpMFAYear = @SearchYear) OR
                                    (ad.ClaimantMFANumber LIKE @SearchMVCNo AND ad.ClaimantMFAYear = @SearchYear)
                                )
                            )
                        )";
                        paramsList.Add(new SqlParameter("@SearchMVCNo", "%" + parts[0].Trim() + "%"));
                        paramsList.Add(new SqlParameter("@SearchYear", year));
                    }
                    else
                    {
                        // Invalid format, fall back to regular search
                        query += @" AND (
                            c.MVCNo LIKE @Search OR 
                            c.VehicleNo LIKE @Search OR 
                            CAST(c.MVCYear AS NVARCHAR) LIKE @Search OR
                            EXISTS (
                                SELECT 1 FROM APPEAL_DETAILS ad 
                                WHERE ad.CaseID = c.CaseID 
                                AND (
                                    ad.CorpMFANumber LIKE @Search OR 
                                    ad.ClaimantMFANumber LIKE @Search
                                )
                            )
                        )";
                        paramsList.Add(new SqlParameter("@Search", "%" + search + "%"));
                    }
                }
                else
                {
                    // Regular search
                    query += @" AND (
                        c.MVCNo LIKE @Search OR 
                        c.VehicleNo LIKE @Search OR 
                        CAST(c.MVCYear AS NVARCHAR) LIKE @Search OR 
                        m.MACTName LIKE @Search OR
                        EXISTS (
                            SELECT 1 FROM APPEAL_DETAILS ad 
                            WHERE ad.CaseID = c.CaseID 
                            AND (
                                ad.CorpMFANumber LIKE @Search OR 
                                ad.ClaimantMFANumber LIKE @Search
                            )
                        )
                    )";
                    paramsList.Add(new SqlParameter("@Search", "%" + search + "%"));
                }
            }

            if (!string.IsNullOrEmpty(status) && status != "all")
            {
                if (status == "Pending")
                    query += " AND (c.DisposalResult IS NULL OR c.DisposalResult = 'Pending')";
                else if (status == "SentToCO" || status == "SentToCO_All")
                {
                    query += " AND adv.ForwardingStatus = 'Sent to Central Office'";
                    query += @" AND NOT EXISTS (
                        SELECT 1 FROM APPEAL_DETAILS a 
                        WHERE (a.CaseID = c.CaseID OR (NULLIF(LTRIM(RTRIM(a.ClaimantMVCNumber)), '') IS NOT NULL AND a.ClaimantMVCNumber = c.MVCNo AND a.ClaimantMVCYear = c.MVCYear))
                        AND (
                            NULLIF(LTRIM(RTRIM(a.CorpMFANumber)), '') IS NOT NULL 
                            OR NULLIF(LTRIM(RTRIM(a.ClaimantMFANumber)), '') IS NOT NULL
                            OR NULLIF(LTRIM(RTRIM(a.CrossMFANumber)), '') IS NOT NULL
                            OR a.IsPendingForFiling = 1
                            OR UPPER(LTRIM(RTRIM(a.InitialAction))) = 'APPEAL'
                        )
                    )";
                }
                else if (status == "Favor")
                    query += " AND c.DisposalResult = 'Favor'";
                else if (status == "Against")
                    query += " AND c.DisposalResult = 'Against'";
                else if (status == "PendingDecision")
                    query += " AND EXISTS (SELECT 1 FROM APPEAL_DETAILS a WHERE a.CaseID = c.CaseID AND (UPPER(LTRIM(RTRIM(a.InitialAction))) LIKE '%PENDING%DECISION%' OR UPPER(LTRIM(RTRIM(a.InitialAction))) LIKE '%PENDING%FOR%ACTION%'))";
                else if (status == "PendingAtCA")
                    query += " AND EXISTS (SELECT 1 FROM APPEAL_DETAILS a WHERE a.CaseID = c.CaseID AND (UPPER(LTRIM(RTRIM(a.InitialAction))) LIKE '%PENDING%COMPETENT%AUTHORITY%' OR UPPER(LTRIM(RTRIM(a.InitialAction))) LIKE '%PENDING%AT%CA%'))";
                else if (status == "SLPPending")
                    query += " AND EXISTS (SELECT 1 FROM APPEAL_DETAILS a WHERE a.CaseID = c.CaseID AND ((a.CorpSCStatus LIKE '%Pending%' AND a.CorpSCNumber IS NOT NULL AND a.CorpSCNumber != '') OR (a.ClaimantSCStatus LIKE '%Pending%' AND a.ClaimantSCNumber IS NOT NULL AND a.ClaimantSCNumber != '')))";
                else if (status == "MFAPending")
                {
                    query += @" AND EXISTS (
                        SELECT 1 FROM APPEAL_DETAILS a 
                        WHERE (a.CaseID = c.CaseID OR (NULLIF(LTRIM(RTRIM(a.ClaimantMVCNumber)), '') IS NOT NULL AND a.ClaimantMVCNumber = c.MVCNo AND a.ClaimantMVCYear = c.MVCYear))
                        AND (
                            ((a.CorpMFAStatus LIKE '%Pending%' OR a.CorpMFAStatus IS NULL OR a.CorpMFAStatus = '' OR a.CorpMFAStatus NOT LIKE '%Disposed%') AND (NULLIF(LTRIM(RTRIM(a.CorpMFANumber)), '') IS NOT NULL OR a.IsPendingForFiling = 1 OR UPPER(LTRIM(RTRIM(a.InitialAction))) = 'APPEAL'))
                            OR (NULLIF(LTRIM(RTRIM(a.ClaimantMFANumber)), '') IS NOT NULL AND (a.ClaimantMFAStatus LIKE '%Pending%' OR a.ClaimantMFAStatus IS NULL OR a.ClaimantMFAStatus = '' OR a.ClaimantMFAStatus NOT LIKE '%Disposed%'))
                            OR (NULLIF(LTRIM(RTRIM(a.CrossMFANumber)), '') IS NOT NULL)
                        )
                    )";
                }
                else if (status == "CompliancePending")
                    query += " AND EXISTS (SELECT 1 FROM APPEAL_DETAILS a WHERE a.CaseID = c.CaseID AND a.InitialAction IS NOT NULL AND a.InitialAction != '' AND UPPER(LTRIM(RTRIM(a.InitialAction))) NOT LIKE '%PENDING%COMPETENT%AUTHORITY%' AND UPPER(LTRIM(RTRIM(a.InitialAction))) NOT LIKE '%PENDING%DECISION%')";
                else if (status == "NoAction")
                    query += " AND adv.ForwardingStatus IS NOT NULL AND adv.ForwardingStatus = 'Sent to Central Office' AND NOT EXISTS (SELECT 1 FROM APPEAL_DETAILS a WHERE a.CaseID = c.CaseID AND (a.InitialAction IS NOT NULL AND a.InitialAction != ''))";
                else if (status == "AwardsToday")
                    query += " AND CAST(adv.AwardDate AS DATE) = CAST(GETDATE() AS DATE)";
                else if (status == "CriticalDelays")
                    query += " AND c.NextHearingDate < CAST(GETDATE() AS DATE) AND (c.DisposalResult IS NULL OR c.DisposalResult = 'Pending' OR c.DisposalResult = '')";
                else if (status == "ComplianceDue")
                    query += " AND EXISTS (SELECT 1 FROM APPEAL_DETAILS a WHERE a.CaseID = c.CaseID AND a.FinalComplianceDate >= CAST(GETDATE() AS DATE) AND a.FinalComplianceDate <= DATEADD(day, 7, CAST(GETDATE() AS DATE)))";
                else
                    query += " AND c.DisposalResult = @Status";

                if (status != "Decided" && status != "Pending" && status != "SentToCO" && status != "Favor" && status != "Against" 
                    && status != "PendingDecision" && status != "PendingAtCA" && status != "SLPPending" && status != "MFAPending" && status != "CompliancePending" && status != "NoAction" && status != "SentToCO_All"
                    && status != "AwardsToday" && status != "CriticalDelays" && status != "ComplianceDue") 
                     paramsList.Add(new SqlParameter("@Status", status));
            }

            return (int)_db.ExecuteScalar(query, paramsList.ToArray())!;
        }

        public DashboardStatsViewModel GetDashboardStats(int divisionId = 0)
        {
            var stats = new DashboardStatsViewModel();
            try
            {
            using (var conn = new SqlConnection(_db.GetConnectionString()))
            {
                conn.Open();
                
                // 1. Fetch Global Summary Stats (Aggregating MVC, Labour, and Gratuity)
                string summaryQuery = @"
                    SELECT 
                        (SELECT COUNT(*) FROM APPEAL_DETAILS a JOIN MVC_CASES mc ON a.CaseID = mc.CaseID WHERE ((a.CorpMFAStatus LIKE '%Pending%' OR a.CorpMFAStatus IS NULL OR a.CorpMFAStatus = '' OR a.IsPendingForFiling = 1) AND (a.CorpMFANumber IS NOT NULL AND a.CorpMFANumber != '' OR a.IsPendingForFiling = 1 OR UPPER(LTRIM(RTRIM(a.InitialAction))) = 'APPEAL')) AND (@DivisionID = 0 OR @DivisionID = 5 OR mc.DivisionID = @DivisionID)) AS CorpMFAPendingCount,
                        (SELECT COUNT(*) FROM APPEAL_DETAILS a JOIN MVC_CASES mc ON a.CaseID = mc.CaseID WHERE (a.ClaimantMFANumber IS NOT NULL AND a.ClaimantMFANumber != '') AND (@DivisionID = 0 OR @DivisionID = 5 OR mc.DivisionID = @DivisionID)) AS ClaimantMFAPendingCount,

                        (SELECT COUNT(*) FROM MVC_CASES WHERE (@DivisionID = 0 OR @DivisionID = 5 OR DivisionID = @DivisionID)) AS TotalCases,

                        -- 2. PENDING/ACTIVE CASES
                        (SELECT COUNT(*) FROM MVC_CASES WHERE (@DivisionID = 0 OR @DivisionID = 5 OR DivisionID = @DivisionID) AND (DisposalResult IS NULL OR DisposalResult = 'Pending' OR DisposalResult = '')) AS PendingCases,

                        -- 3. FAVOR CASES
                        (SELECT COUNT(*) FROM MVC_CASES WHERE (@DivisionID = 0 OR @DivisionID = 5 OR DivisionID = @DivisionID) AND DisposalResult = 'Favor') AS FavorCases,

                        -- 4. AGAINST CASES
                        (SELECT COUNT(*) FROM MVC_CASES WHERE (@DivisionID = 0 OR @DivisionID = 5 OR DivisionID = @DivisionID) AND DisposalResult = 'Against') AS AgainstCases,

                        -- 5. HEARINGS TODAY (Multi-Module Unified)
                        (SELECT COUNT(*) FROM MVC_CASES WHERE (@DivisionID = 0 OR @DivisionID = 5 OR DivisionID = @DivisionID) AND CAST(NextHearingDate AS DATE) = CAST(GETDATE() AS DATE)) AS HearingsTodayMvc,
                        (SELECT 
                            (SELECT COUNT(*) FROM LABOUR_CASES WHERE (@DivisionID = 0 OR @DivisionID = 5 OR DivisionID = @DivisionID) AND CAST(NextHearingDate AS DATE) = CAST(GETDATE() AS DATE))
                            +
                            (SELECT COUNT(*) FROM LABOUR_SERVICE_MATTERS WHERE (@DivisionID = 0 OR @DivisionID = 5 OR DivisionID = @DivisionID) AND CAST(NextHearingDate AS DATE) = CAST(GETDATE() AS DATE))
                        ) AS HearingsTodayLabour,
                        (SELECT COUNT(*) FROM GRA_CASES WHERE (@DivisionID = 0 OR @DivisionID = 5 OR DivisionCode = @DivisionID) AND CAST(NextHearingDate AS DATE) = CAST(GETDATE() AS DATE)) AS HearingsTodayGratuity,
                        (SELECT COUNT(*) FROM OTHER_CASES WHERE (@DivisionID = 0 OR @DivisionID = 5 OR DivisionID = @DivisionID) AND CAST(NextDateOfHearing AS DATE) = CAST(GETDATE() AS DATE)) AS HearingsTodayOther,

                        -- 6. AWARDS TODAY & THIS MONTH (Multi-Module Unified)
                        (
                            (SELECT COUNT(*) FROM MVC_CASE_ADVERSE_DETAILS ad JOIN MVC_CASES mc ON ad.CaseID = mc.CaseID WHERE (@DivisionID = 0 OR @DivisionID = 5 OR mc.DivisionID = @DivisionID) AND CAST(ad.AwardDate AS DATE) = CAST(GETDATE() AS DATE))
                            +
                            (SELECT COUNT(*) FROM LABOUR_CASES WHERE (@DivisionID = 0 OR @DivisionID = 5 OR DivisionID = @DivisionID) AND CAST(DisposalDate AS DATE) = CAST(GETDATE() AS DATE) AND DisposalResult IN ('Against', 'Favor'))
                            +
                            (SELECT COUNT(*) FROM LABOUR_SERVICE_MATTERS WHERE (@DivisionID = 0 OR @DivisionID = 5 OR DivisionID = @DivisionID) AND CAST(DisposalDate AS DATE) = CAST(GETDATE() AS DATE))
                            +
                            (SELECT COUNT(*) FROM GRA_CASES WHERE (@DivisionID = 0 OR @DivisionID = 5 OR DivisionCode = @DivisionID) AND CAST(DisposalDate AS DATE) = CAST(GETDATE() AS DATE) AND DisposalResult IN ('Against', 'Dismissed'))
                        ) AS AwardsToday,

                        (
                            (SELECT COUNT(*) FROM MVC_CASE_ADVERSE_DETAILS ad JOIN MVC_CASES mc ON ad.CaseID = mc.CaseID WHERE (@DivisionID = 0 OR @DivisionID = 5 OR mc.DivisionID = @DivisionID) AND ad.AwardDate >= DATEADD(day, -30, GETDATE()))
                            +
                            (SELECT COUNT(*) FROM LABOUR_CASES WHERE (@DivisionID = 0 OR @DivisionID = 5 OR DivisionID = @DivisionID) AND DisposalDate >= DATEADD(day, -30, GETDATE()) AND DisposalResult IN ('Against', 'Favor'))
                            +
                            (SELECT COUNT(*) FROM LABOUR_SERVICE_MATTERS WHERE (@DivisionID = 0 OR @DivisionID = 5 OR DivisionID = @DivisionID) AND DisposalDate >= DATEADD(day, -30, GETDATE()))
                            +
                            (SELECT COUNT(*) FROM GRA_CASES WHERE (@DivisionID = 0 OR @DivisionID = 5 OR DivisionCode = @DivisionID) AND DisposalDate >= DATEADD(day, -30, GETDATE()) AND DisposalResult IN ('Against', 'Dismissed'))
                        ) AS AwardsThisMonth,

                        -- 7. COMPLIANCE DUE (Next 7-15 days & Overdue)
                        (
                            (SELECT COUNT(*) FROM APPEAL_DETAILS a JOIN MVC_CASES mc ON a.CaseID = mc.CaseID WHERE (@DivisionID = 0 OR @DivisionID = 5 OR mc.DivisionID = @DivisionID) AND a.FinalComplianceDate >= CAST(GETDATE() AS DATE) AND a.FinalComplianceDate <= DATEADD(day, 15, CAST(GETDATE() AS DATE)))
                            +
                            (SELECT COUNT(*) FROM LABOUR_CASES WHERE (@DivisionID = 0 OR @DivisionID = 5 OR DivisionID = @DivisionID) AND CO_StayComplianceDate >= CAST(GETDATE() AS DATE) AND CO_StayComplianceDate <= DATEADD(day, 15, CAST(GETDATE() AS DATE)))
                            +
                            (SELECT COUNT(*) FROM GRA_CASES WHERE (@DivisionID = 0 OR @DivisionID = 5 OR DivisionCode = @DivisionID) AND StayComplianceDate >= CAST(GETDATE() AS DATE) AND StayComplianceDate <= DATEADD(day, 15, CAST(GETDATE() AS DATE)))
                        ) AS ComplianceDue,

                        (
                            (SELECT COUNT(*) FROM APPEAL_DETAILS a JOIN MVC_CASES mc ON a.CaseID = mc.CaseID WHERE (@DivisionID = 0 OR @DivisionID = 5 OR mc.DivisionID = @DivisionID) AND a.FinalComplianceDate < CAST(GETDATE() AS DATE) AND (a.InitialAction LIKE '%Pending%' OR a.InitialAction IS NULL OR a.InitialAction = ''))
                            +
                            (SELECT COUNT(*) FROM LABOUR_CASES WHERE (@DivisionID = 0 OR @DivisionID = 5 OR DivisionID = @DivisionID) AND CO_StayComplianceDate < CAST(GETDATE() AS DATE) AND DisposalResult = 'Against')
                            +
                            (SELECT COUNT(*) FROM GRA_CASES WHERE (@DivisionID = 0 OR @DivisionID = 5 OR DivisionCode = @DivisionID) AND StayComplianceDate < CAST(GETDATE() AS DATE) AND DisposalResult IN ('Against', 'Dismissed'))
                        ) AS ComplianceOverdue,

                        -- 8. CRITICAL DELAYS (Multi-Module Overdue & Aging Categories)
                        (
                            (SELECT COUNT(*) FROM MVC_CASES WHERE (@DivisionID = 0 OR @DivisionID = 5 OR DivisionID = @DivisionID) AND NextHearingDate < CAST(GETDATE() AS DATE) AND (DisposalResult IS NULL OR DisposalResult = 'Pending' OR DisposalResult = ''))
                            +
                            (SELECT COUNT(*) FROM LABOUR_CASES WHERE (@DivisionID = 0 OR @DivisionID = 5 OR DivisionID = @DivisionID) AND NextHearingDate < CAST(GETDATE() AS DATE) AND (DisposalResult IS NULL OR DisposalResult = 'Pending' OR CaseStatus = 'Pending' OR CaseStatus = '' OR CaseStatus IS NULL))
                            +
                            (SELECT COUNT(*) FROM LABOUR_SERVICE_MATTERS WHERE (@DivisionID = 0 OR @DivisionID = 5 OR DivisionID = @DivisionID) AND NextHearingDate < CAST(GETDATE() AS DATE) AND (Status IS NULL OR Status = 'Pending' OR Status = ''))
                            +
                            (SELECT COUNT(*) FROM GRA_CASES WHERE (@DivisionID = 0 OR @DivisionID = 5 OR DivisionCode = @DivisionID) AND NextHearingDate < CAST(GETDATE() AS DATE) AND (DisposalResult IS NULL OR DisposalResult = 'Pending' OR DisposalResult = ''))
                            +
                            (SELECT COUNT(*) FROM OTHER_CASES WHERE (@DivisionID = 0 OR @DivisionID = 5 OR DivisionID = @DivisionID) AND NextDateOfHearing < CAST(GETDATE() AS DATE) AND (DisposalStatus IS NULL OR DisposalStatus = 'Pending' OR DisposalStatus = ''))
                        ) AS CriticalDelays,

                        (
                            (SELECT COUNT(*) FROM MVC_CASES WHERE (@DivisionID = 0 OR @DivisionID = 5 OR DivisionID = @DivisionID) AND NextHearingDate >= DATEADD(day, -30, GETDATE()) AND NextHearingDate < CAST(GETDATE() AS DATE) AND (DisposalResult IS NULL OR DisposalResult = 'Pending' OR DisposalResult = ''))
                            +
                            (SELECT COUNT(*) FROM LABOUR_CASES WHERE (@DivisionID = 0 OR @DivisionID = 5 OR DivisionID = @DivisionID) AND NextHearingDate >= DATEADD(day, -30, GETDATE()) AND NextHearingDate < CAST(GETDATE() AS DATE) AND (DisposalResult IS NULL OR DisposalResult = 'Pending' OR CaseStatus = 'Pending' OR CaseStatus = '' OR CaseStatus IS NULL))
                            +
                            (SELECT COUNT(*) FROM LABOUR_SERVICE_MATTERS WHERE (@DivisionID = 0 OR @DivisionID = 5 OR DivisionID = @DivisionID) AND NextHearingDate >= DATEADD(day, -30, GETDATE()) AND NextHearingDate < CAST(GETDATE() AS DATE) AND (Status IS NULL OR Status = 'Pending' OR Status = ''))
                            +
                            (SELECT COUNT(*) FROM GRA_CASES WHERE (@DivisionID = 0 OR @DivisionID = 5 OR DivisionCode = @DivisionID) AND NextHearingDate >= DATEADD(day, -30, GETDATE()) AND NextHearingDate < CAST(GETDATE() AS DATE) AND (DisposalResult IS NULL OR DisposalResult = 'Pending' OR DisposalResult = ''))
                            +
                            (SELECT COUNT(*) FROM OTHER_CASES WHERE (@DivisionID = 0 OR @DivisionID = 5 OR DivisionID = @DivisionID) AND NextDateOfHearing >= DATEADD(day, -30, GETDATE()) AND NextDateOfHearing < CAST(GETDATE() AS DATE) AND (DisposalStatus IS NULL OR DisposalStatus = 'Pending' OR DisposalStatus = ''))
                        ) AS CriticalDelaysRecent,

                        (
                            (SELECT COUNT(*) FROM MVC_CASES WHERE (@DivisionID = 0 OR @DivisionID = 5 OR DivisionID = @DivisionID) AND NextHearingDate < DATEADD(day, -30, GETDATE()) AND (DisposalResult IS NULL OR DisposalResult = 'Pending' OR DisposalResult = ''))
                            +
                            (SELECT COUNT(*) FROM LABOUR_CASES WHERE (@DivisionID = 0 OR @DivisionID = 5 OR DivisionID = @DivisionID) AND NextHearingDate < DATEADD(day, -30, GETDATE()) AND (DisposalResult IS NULL OR DisposalResult = 'Pending' OR CaseStatus = 'Pending' OR CaseStatus = '' OR CaseStatus IS NULL))
                            +
                            (SELECT COUNT(*) FROM LABOUR_SERVICE_MATTERS WHERE (@DivisionID = 0 OR @DivisionID = 5 OR DivisionID = @DivisionID) AND NextHearingDate < DATEADD(day, -30, GETDATE()) AND (Status IS NULL OR Status = 'Pending' OR Status = ''))
                            +
                            (SELECT COUNT(*) FROM GRA_CASES WHERE (@DivisionID = 0 OR @DivisionID = 5 OR DivisionCode = @DivisionID) AND NextHearingDate < DATEADD(day, -30, GETDATE()) AND (DisposalResult IS NULL OR DisposalResult = 'Pending' OR DisposalResult = ''))
                            +
                            (SELECT COUNT(*) FROM OTHER_CASES WHERE (@DivisionID = 0 OR @DivisionID = 5 OR DivisionID = @DivisionID) AND NextDateOfHearing < DATEADD(day, -30, GETDATE()) AND (DisposalStatus IS NULL OR DisposalStatus = 'Pending' OR DisposalStatus = ''))
                        ) AS CriticalDelaysStale,

                        (
                            (SELECT COUNT(*) FROM MVC_CASES WHERE (@DivisionID = 0 OR @DivisionID = 5 OR DivisionID = @DivisionID) AND NextHearingDate IS NULL AND (DisposalResult IS NULL OR DisposalResult = 'Pending' OR DisposalResult = ''))
                            +
                            (SELECT COUNT(*) FROM LABOUR_CASES WHERE (@DivisionID = 0 OR @DivisionID = 5 OR DivisionID = @DivisionID) AND NextHearingDate IS NULL AND (DisposalResult IS NULL OR DisposalResult = 'Pending' OR CaseStatus = 'Pending' OR CaseStatus = '' OR CaseStatus IS NULL))
                            +
                            (SELECT COUNT(*) FROM LABOUR_SERVICE_MATTERS WHERE (@DivisionID = 0 OR @DivisionID = 5 OR DivisionID = @DivisionID) AND NextHearingDate IS NULL AND (Status IS NULL OR Status = 'Pending' OR Status = ''))
                            +
                            (SELECT COUNT(*) FROM GRA_CASES WHERE (@DivisionID = 0 OR @DivisionID = 5 OR DivisionCode = @DivisionID) AND NextHearingDate IS NULL AND (DisposalResult IS NULL OR DisposalResult = 'Pending' OR DisposalResult = ''))
                            +
                            (SELECT COUNT(*) FROM OTHER_CASES WHERE (@DivisionID = 0 OR @DivisionID = 5 OR DivisionID = @DivisionID) AND NextDateOfHearing IS NULL AND (DisposalStatus IS NULL OR DisposalStatus = 'Pending' OR DisposalStatus = ''))
                        ) AS CriticalDelaysMissingDate,

                        -- 9. MISC STATS
                        (SELECT COUNT(*) FROM MVC_CASE_ADVERSE_DETAILS ad JOIN MVC_CASES mc ON ad.CaseID = mc.CaseID 
                         WHERE ad.ForwardingStatus = 'Sent to Central Office' 
                         AND (@DivisionID = 0 OR @DivisionID = 5 OR mc.DivisionID = @DivisionID)
                         AND NOT EXISTS (
                             SELECT 1 FROM APPEAL_DETAILS a 
                             WHERE (a.CaseID = mc.CaseID OR (NULLIF(LTRIM(RTRIM(a.ClaimantMVCNumber)), '') IS NOT NULL AND a.ClaimantMVCNumber = mc.MVCNo AND a.ClaimantMVCYear = mc.MVCYear))
                             AND (
                                 NULLIF(LTRIM(RTRIM(a.CorpMFANumber)), '') IS NOT NULL 
                                 OR NULLIF(LTRIM(RTRIM(a.ClaimantMFANumber)), '') IS NOT NULL
                                 OR NULLIF(LTRIM(RTRIM(a.CrossMFANumber)), '') IS NOT NULL
                                 OR a.IsPendingForFiling = 1
                                 OR UPPER(LTRIM(RTRIM(a.InitialAction))) = 'APPEAL'
                             )
                         )) AS SentToCOCount,

                        (SELECT COUNT(*) FROM MVC_CASES WHERE CreatedAt >= DATEADD(month, DATEDIFF(month, 0, GETDATE()), 0) AND (@DivisionID = 0 OR @DivisionID = 5 OR DivisionID = @DivisionID)) AS CasesThisMonth,

                        (SELECT COUNT(*) FROM MVC_CASES WHERE (@DivisionID = 0 OR @DivisionID = 5 OR DivisionID = @DivisionID)) AS MVCCount,
                        (SELECT COUNT(*) FROM LABOUR_CASES WHERE (@DivisionID = 0 OR @DivisionID = 5 OR DivisionID = @DivisionID)) AS LabourCount,
                        (SELECT COUNT(*) FROM GRA_CASES WHERE (@DivisionID = 0 OR @DivisionID = 5 OR DivisionCode = @DivisionID)) AS GratuityCount,

                        (SELECT COUNT(*) FROM APPEAL_DETAILS a JOIN MVC_CASES mc ON a.CaseID = mc.CaseID WHERE (UPPER(LTRIM(RTRIM(a.InitialAction))) LIKE '%PENDING%COMPETENT%AUTHORITY%' OR UPPER(LTRIM(RTRIM(a.InitialAction))) LIKE '%PENDING%AT%CA%') AND (@DivisionID = 0 OR @DivisionID = 5 OR mc.DivisionID = @DivisionID)) AS PendingCACount,
 
                        (SELECT COUNT(*) FROM APPEAL_DETAILS a JOIN MVC_CASES mc ON a.CaseID = mc.CaseID WHERE (UPPER(LTRIM(RTRIM(a.InitialAction))) LIKE '%PENDING%DECISION%' OR UPPER(LTRIM(RTRIM(a.InitialAction))) LIKE '%PENDING%FOR%ACTION%') AND (@DivisionID = 0 OR @DivisionID = 5 OR mc.DivisionID = @DivisionID)) AS PendingDecisionCount,

                        (SELECT COUNT(*) FROM APPEAL_DETAILS a JOIN MVC_CASES mc ON a.CaseID = mc.CaseID WHERE (a.InitialAction = 'Appeal' OR a.InitialAction = 'APPEAL') AND (@DivisionID = 0 OR @DivisionID = 5 OR mc.DivisionID = @DivisionID)) AS AppealCount,

                        (SELECT COUNT(*) FROM APPEAL_DETAILS a JOIN MVC_CASES mc ON a.CaseID = mc.CaseID WHERE (a.InitialAction = 'Close' OR a.InitialAction = 'CLOSE' OR a.InitialAction = 'CLOSED') AND (@DivisionID = 0 OR @DivisionID = 5 OR mc.DivisionID = @DivisionID)) AS CloseCount,

                        (SELECT COUNT(*) FROM MVC_CASES WHERE (@DivisionID = 0 OR @DivisionID = 5 OR DivisionID = @DivisionID) AND NextHearingDate >= CAST(GETDATE() AS DATE) AND NextHearingDate <= DATEADD(day, 7, CAST(GETDATE() AS DATE))) AS UpcomingHearingsCount,
                        (SELECT ISNULL(SUM(AwardAmount), 0) FROM MVC_CASE_ADVERSE_DETAILS ad JOIN MVC_CASES mc ON ad.CaseID = mc.CaseID WHERE (@DivisionID = 0 OR @DivisionID = 5 OR mc.DivisionID = @DivisionID)) AS TotalAwardAmountAgainst,
                        (SELECT ISNULL(SUM(AwardAmount), 0) FROM MVC_CASE_ADVERSE_DETAILS ad JOIN MVC_CASES mc ON ad.CaseID = mc.CaseID WHERE (@DivisionID = 0 OR @DivisionID = 5 OR mc.DivisionID = @DivisionID) AND ad.AwardDate >= DATEADD(month, DATEDIFF(month, 0, GETDATE()), 0)) AS MonthlyAwardAmountAgainst";

                using (var cmd = new SqlCommand(summaryQuery, conn))
                {
                    cmd.Parameters.AddWithValue("@DivisionID", divisionId);
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            stats.TotalCases = reader["TotalCases"] != DBNull.Value ? Convert.ToInt32(reader["TotalCases"]) : 0;
                            stats.PendingCases = reader["PendingCases"] != DBNull.Value ? Convert.ToInt32(reader["PendingCases"]) : 0;
                            stats.FavorCases = reader["FavorCases"] != DBNull.Value ? Convert.ToInt32(reader["FavorCases"]) : 0;
                            stats.AgainstCases = reader["AgainstCases"] != DBNull.Value ? Convert.ToInt32(reader["AgainstCases"]) : 0;
                            stats.SentToCOCount = reader["SentToCOCount"] != DBNull.Value ? Convert.ToInt32(reader["SentToCOCount"]) : 0;
                            
                            stats.MVCCount = reader["MVCCount"] != DBNull.Value ? Convert.ToInt32(reader["MVCCount"]) : 0;
                            stats.LabourCount = reader["LabourCount"] != DBNull.Value ? Convert.ToInt32(reader["LabourCount"]) : 0;
                            stats.GratuityCount = reader["GratuityCount"] != DBNull.Value ? Convert.ToInt32(reader["GratuityCount"]) : 0;

                            stats.CasesThisMonth = reader["CasesThisMonth"] != DBNull.Value ? Convert.ToInt32(reader["CasesThisMonth"]) : 0;
                            stats.UpcomingHearingsCount = reader["UpcomingHearingsCount"] != DBNull.Value ? Convert.ToInt32(reader["UpcomingHearingsCount"]) : 0;
                            stats.TotalAwardAmountAgainst = reader["TotalAwardAmountAgainst"] != DBNull.Value ? Convert.ToDecimal(reader["TotalAwardAmountAgainst"]) : 0;
                            stats.MonthlyAwardAmountAgainst = reader["MonthlyAwardAmountAgainst"] != DBNull.Value ? Convert.ToDecimal(reader["MonthlyAwardAmountAgainst"]) : 0;
                            
                            // Map PendingCA for Division Users
                            stats.PendingCompetentAuthorityCount = reader["PendingCACount"] != DBNull.Value ? Convert.ToInt32(reader["PendingCACount"]) : 0;
                            stats.PendingDecisionCount = reader["PendingDecisionCount"] != DBNull.Value ? Convert.ToInt32(reader["PendingDecisionCount"]) : 0;
                            stats.AppealCount = reader["AppealCount"] != DBNull.Value ? Convert.ToInt32(reader["AppealCount"]) : 0;
                            stats.CloseCount = reader["CloseCount"] != DBNull.Value ? Convert.ToInt32(reader["CloseCount"]) : 0;

                            // Snapshot Stats (Cross-Module Unified)
                            stats.HearingsTodayMvc = reader["HearingsTodayMvc"] != DBNull.Value ? Convert.ToInt32(reader["HearingsTodayMvc"]) : 0;
                            stats.HearingsTodayLabour = reader["HearingsTodayLabour"] != DBNull.Value ? Convert.ToInt32(reader["HearingsTodayLabour"]) : 0;
                            stats.HearingsTodayGratuity = reader["HearingsTodayGratuity"] != DBNull.Value ? Convert.ToInt32(reader["HearingsTodayGratuity"]) : 0;
                            stats.HearingsTodayOther = reader["HearingsTodayOther"] != DBNull.Value ? Convert.ToInt32(reader["HearingsTodayOther"]) : 0;
                            stats.HearingsToday = stats.HearingsTodayMvc + stats.HearingsTodayLabour + stats.HearingsTodayGratuity + stats.HearingsTodayOther;

                            stats.AwardsReceivedToday = reader["AwardsToday"] != DBNull.Value ? Convert.ToInt32(reader["AwardsToday"]) : 0;
                            stats.AwardsThisMonth = reader["AwardsThisMonth"] != DBNull.Value ? Convert.ToInt32(reader["AwardsThisMonth"]) : 0;

                            stats.ComplianceDueCount = reader["ComplianceDue"] != DBNull.Value ? Convert.ToInt32(reader["ComplianceDue"]) : 0;
                            stats.ComplianceOverdueCount = reader["ComplianceOverdue"] != DBNull.Value ? Convert.ToInt32(reader["ComplianceOverdue"]) : 0;

                            stats.CriticalDelaysCount = reader["CriticalDelays"] != DBNull.Value ? Convert.ToInt32(reader["CriticalDelays"]) : 0;
                            stats.CriticalDelaysRecent = reader["CriticalDelaysRecent"] != DBNull.Value ? Convert.ToInt32(reader["CriticalDelaysRecent"]) : 0;
                            stats.CriticalDelaysStale = reader["CriticalDelaysStale"] != DBNull.Value ? Convert.ToInt32(reader["CriticalDelaysStale"]) : 0;
                            stats.CriticalDelaysMissingDate = reader["CriticalDelaysMissingDate"] != DBNull.Value ? Convert.ToInt32(reader["CriticalDelaysMissingDate"]) : 0;

                            stats.CorpMFAPendingCount = reader["CorpMFAPendingCount"] != DBNull.Value ? Convert.ToInt32(reader["CorpMFAPendingCount"]) : 0;
                            stats.ClaimantMFAPendingCount = reader["ClaimantMFAPendingCount"] != DBNull.Value ? Convert.ToInt32(reader["ClaimantMFAPendingCount"]) : 0;
                            stats.MFAPendingCount = stats.CorpMFAPendingCount + stats.ClaimantMFAPendingCount;
                        }
                    }
                }

                // Removed redundant second query block as counts are now integrated in primary summary query
                

                // 2.1 Fetch Additional Central Office Stats (SLP, MFA, No Action)
                if (divisionId == 0 || divisionId == 5)
                {
                    // SLP Pending
                    string slpQuery = "SELECT COUNT(*) FROM APPEAL_DETAILS WHERE ((CorpSCStatus LIKE '%Pending%' AND CorpSCNumber IS NOT NULL AND CorpSCNumber != '') OR (ClaimantSCStatus LIKE '%Pending%' AND ClaimantSCNumber IS NOT NULL AND ClaimantSCNumber != ''))";
                    stats.SLPPendingCount = (int)_db.ExecuteScalar(slpQuery, null)!;

                    // MFA Pending (Redundant as added above, but keeping for compatibility)
                    // ... 
                    // No Action Taken (Sent to CO but no Initial Action)

                    // No Action Taken (Sent to CO but no Initial Action)
                    string noActionQuery = @"
                        SELECT COUNT(*) 
                        FROM MVC_CASE_ADVERSE_DETAILS ad 
                        LEFT JOIN APPEAL_DETAILS apel ON ad.CaseID = apel.CaseID 
                        WHERE ad.ForwardingStatus = 'Sent to Central Office' 
                        AND (apel.InitialAction IS NULL OR apel.InitialAction = '')";
                    stats.NoActionTakenCount = (int)_db.ExecuteScalar(noActionQuery, null)!;
                }

                // 3. Fetch Recent Cases
                string recentQuery = @"
                    SELECT TOP 5
                        c.CaseID, c.MVCNo, c.MACTID, m.MACTName, c.DisposalResult, c.CreatedAt, c.NextHearingDate
                    FROM MVC_CASES c
                    JOIN MACT_MASTER m ON c.MACTID = m.MACTID
                    WHERE (@DivisionID = 0 OR @DivisionID = 5 OR c.DivisionID = @DivisionID)
                    ORDER BY c.CreatedAt DESC";

                using (var cmd = new SqlCommand(recentQuery, conn))
                {
                    cmd.Parameters.AddWithValue("@DivisionID", divisionId);
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            stats.RecentCases.Add(new RecentCaseViewModel
                            {
                                CaseID = Convert.ToInt32(reader["CaseID"]),
                                MVCNo = reader["MVCNo"].ToString() ?? "",
                                MACTName = reader["MACTName"].ToString() ?? "",
                                DisposalResult = reader["DisposalResult"]?.ToString(),
                                CreatedAt = reader["CreatedAt"] != DBNull.Value ? Convert.ToDateTime(reader["CreatedAt"]) : DateTime.MinValue,
                                NextHearingDate = reader["NextHearingDate"] != DBNull.Value ? Convert.ToDateTime(reader["NextHearingDate"]) : null
                            });
                        }
                    }
                }
            }
            }
            catch (Exception ex)
            {
                // Log the error and return empty stats to prevent 500 error
                Console.WriteLine($"Dashboard Stats Error: {ex.Message}");
                return stats;
            }
            return stats;
        }

        public void MarkCaseAsViewed(int caseId)
        {
            // Check if already viewed to avoid duplicates
            string checkQuery = "SELECT COUNT(*) FROM CASE_VIEW_TRACKING WHERE CaseID = @CaseID";
            int existingCount = (int)_db.ExecuteScalar(checkQuery, new[] { new SqlParameter("@CaseID", caseId) })!;
            
            if (existingCount == 0)
            {
                string insertQuery = "INSERT INTO CASE_VIEW_TRACKING (CaseID, ViewedAt) VALUES (@CaseID, GETDATE())";
                _db.ExecuteNonQuery(insertQuery, new[] { new SqlParameter("@CaseID", caseId) });
            }
        }


        public List<string> GetAdvocatesByDivision(int divisionId)
        {
            var advocates = new List<string>();
            // Fetch ALL advocates regardless of division (Global Fetch)
            string advQuery = "SELECT DISTINCT AdvocateName FROM MVC_DIVISION_ADVOCATES ORDER BY AdvocateName";
            DataTable adt = _db.ExecuteQuery(advQuery, null);
            foreach (DataRow row in adt.Rows)
            {
                advocates.Add(row["AdvocateName"].ToString()!);
            }

            return advocates;
        }
        public List<string> GetHighCourtAdvocatesByBench(string benchCode)
        {
            var advocates = new List<string>();
            if (string.IsNullOrEmpty(benchCode)) return advocates;

            string query = "SELECT AdvocateName FROM HIGH_COURT_ADVOCATES WHERE Bench = @Bench AND IsActive = 1 ORDER BY AdvocateName";
            DataTable dt = _db.ExecuteQuery(query, new[] { new SqlParameter("@Bench", benchCode) });
            
            foreach (DataRow row in dt.Rows)
            {
                advocates.Add(row["AdvocateName"].ToString()!);
            }
            return advocates;
        }

        public MVCCaseViewModel? GetCaseByAuditCriteria(string mvcNo, string vehicleNo, DateTime accidentDate)
        {
            string query = @"
                SELECT CaseID FROM MVC_CASES 
                WHERE MVCNo = @MVCNo 
                AND REPLACE(VehicleNo, ' ', '') = REPLACE(@VehicleNo, ' ', '')
                AND CAST(AccidentDate AS DATE) = CAST(@AccidentDate AS DATE)";

            var paramsList = new[] {
                new SqlParameter("@MVCNo", mvcNo),
                new SqlParameter("@VehicleNo", vehicleNo),
                new SqlParameter("@AccidentDate", accidentDate)
            };

            object? result = _db.ExecuteScalar(query, paramsList);
            if (result == null || result == DBNull.Value) return null;
            return GetCaseById(Convert.ToInt32(result));
        }

        // Payment Management Methods
        public bool SaveCasePayments(int caseId, List<CasePaymentViewModel> payments)
        {
            // Delete existing payments
            string deleteQuery = "DELETE FROM MVC_CASE_PAYMENTS WHERE CaseID = @CaseID";
            _db.ExecuteNonQuery(deleteQuery, new[] { new SqlParameter("@CaseID", caseId) });

            // Insert new payments
            if (payments != null && payments.Count > 0)
            {
                foreach (var payment in payments)
                {
                    string insertQuery = @"
                        INSERT INTO MVC_CASE_PAYMENTS (CaseID, Amount, ChequeNumber, ChequeDate, Remarks)
                        VALUES (@CaseID, @Amount, @ChequeNumber, @ChequeDate, @Remarks)";
                    
                    _db.ExecuteNonQuery(insertQuery, new[] {
                        new SqlParameter("@CaseID", caseId),
                        new SqlParameter("@Amount", payment.Amount),
                        new SqlParameter("@ChequeNumber", payment.ChequeNumber ?? (object)DBNull.Value),
                        new SqlParameter("@ChequeDate", payment.ChequeDate ?? (object)DBNull.Value),
                        new SqlParameter("@Remarks", payment.Remarks ?? (object)DBNull.Value)
                    });
                }
            }
            return true;
        }

        public List<CasePaymentViewModel> GetCasePayments(int caseId)
        {
            var payments = new List<CasePaymentViewModel>();
            string query = "SELECT * FROM MVC_CASE_PAYMENTS WHERE CaseID = @CaseID ORDER BY ChequeDate";
            DataTable dt = _db.ExecuteQuery(query, new[] { new SqlParameter("@CaseID", caseId) });
            
            foreach (DataRow row in dt.Rows)
            {
                payments.Add(new CasePaymentViewModel
                {
                    PaymentID = (int)row["PaymentID"],
                    CaseID = (int)row["CaseID"],
                    Amount = (decimal)row["Amount"],
                    ChequeNumber = row["ChequeNumber"] != DBNull.Value ? row["ChequeNumber"].ToString() : null,
                    ChequeDate = row["ChequeDate"] != DBNull.Value ? (DateTime?)row["ChequeDate"] : null,
                    Remarks = row["Remarks"] != DBNull.Value ? row["Remarks"].ToString() : null,
                    CreatedDate = row["CreatedDate"] != DBNull.Value ? (DateTime)row["CreatedDate"] : DateTime.Now
                });
            }
            return payments;
        }
        public List<RecentCaseViewModel> GetCasesByHearingDate(DateTime hearingDate, int divisionId = 0, DateTime? endDate = null)
        {
            var cases = new List<RecentCaseViewModel>();
            try
            {
                using (var conn = new SqlConnection(_db.GetConnectionString()))
                {
                    conn.Open();
                    string query;
                    
                    if (endDate.HasValue && endDate.Value != hearingDate)
                    {
                        // Date range query
                        query = @"
                            SELECT 
                                c.CaseID, c.MVCNo, c.MACTID, m.MACTName, c.DisposalResult, c.CreatedAt, c.NextHearingDate
                            FROM MVC_CASES c
                            JOIN MACT_MASTER m ON c.MACTID = m.MACTID
                            WHERE CAST(c.NextHearingDate AS DATE) >= CAST(@StartDate AS DATE)
                              AND CAST(c.NextHearingDate AS DATE) <= CAST(@EndDate AS DATE)
                              AND (@DivisionID = 0 OR @DivisionID = 5 OR c.DivisionID = @DivisionID)
                            ORDER BY c.NextHearingDate ASC, c.CreatedAt DESC";
                    }
                    else
                    {
                        // Single date query (original behavior)
                        query = @"
                            SELECT 
                                c.CaseID, c.MVCNo, c.MACTID, m.MACTName, c.DisposalResult, c.CreatedAt, c.NextHearingDate
                            FROM MVC_CASES c
                            JOIN MACT_MASTER m ON c.MACTID = m.MACTID
                            WHERE CAST(c.NextHearingDate AS DATE) = CAST(@StartDate AS DATE)
                              AND (@DivisionID = 0 OR @DivisionID = 5 OR c.DivisionID = @DivisionID)
                            ORDER BY c.CreatedAt DESC";
                    }

                    using (var cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@StartDate", hearingDate);
                        cmd.Parameters.AddWithValue("@EndDate", endDate ?? hearingDate);
                        cmd.Parameters.AddWithValue("@DivisionID", divisionId);
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                cases.Add(new RecentCaseViewModel
                                {
                                    CaseID = Convert.ToInt32(reader["CaseID"]),
                                    MVCNo = reader["MVCNo"].ToString() ?? "",
                                    MACTName = reader["MACTName"].ToString() ?? "",
                                    DisposalResult = reader["DisposalResult"]?.ToString(),
                                    CreatedAt = reader["CreatedAt"] != DBNull.Value ? Convert.ToDateTime(reader["CreatedAt"]) : DateTime.MinValue,
                                    NextHearingDate = reader["NextHearingDate"] != DBNull.Value ? Convert.ToDateTime(reader["NextHearingDate"]) : null
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"GetCasesByHearingDate Error: {ex.Message}");
                return cases; // Return empty list on error
            }
            return cases;
        }
        public List<GlobalCaseViewModel> GetHearingsTodayIrrespectiveOfModule(int divisionId = 0, string? module = null)
        {
            var cases = new List<GlobalCaseViewModel>();
            try
            {
                using (var conn = new SqlConnection(_db.GetConnectionString()))
                {
                    conn.Open();
                    var queryBuilder = new List<string>();
                    bool isAll = string.IsNullOrEmpty(module) || module == "All";

                    if (isAll || module == "MVC")
                    {
                        queryBuilder.Add(@"
                            SELECT 
                                'MVC' AS ModuleName, c.CaseID, ('MVC ' + c.MVCNo + ' / ' + CAST(c.MVCYear AS VARCHAR)) AS CaseNo, 
                                m.MACTName AS CourtOrAuthority, c.DisposalResult, c.CreatedAt, c.NextHearingDate, 
                                ('/Case/Details/' + CAST(c.CaseID AS VARCHAR)) AS DetailsUrl,
                                c.CNRNumber, 0 AS DaysOverdue, c.CurrentStage AS Stage, NULL AS CourtHall, 0 AS IsHighCourt
                            FROM MVC_CASES c
                            JOIN MACT_MASTER m ON c.MACTID = m.MACTID
                            WHERE CAST(c.NextHearingDate AS DATE) = CAST(GETDATE() AS DATE)
                              AND (@DivisionID = 0 OR @DivisionID = 5 OR c.DivisionID = @DivisionID)");
                    }

                    if (isAll || module == "Labour")
                    {
                        queryBuilder.Add(@"
                            SELECT
                                'Labour' AS ModuleName, lc.CaseID, 
                                (ISNULL(lc.CaseType, '') + ' ' + ISNULL(lc.CaseNumber, '') + ' / ' + CAST(ISNULL(lc.CaseYear, 0) AS VARCHAR)) AS CaseNo, 
                                ISNULL(lcourt.CourtName, lc.OtherCourtDetails) AS CourtOrAuthority, 
                                lc.DisposalResult, lc.CreatedDate AS CreatedAt, lc.NextHearingDate, 
                                ('/Labour/Details/' + CAST(lc.CaseID AS VARCHAR)) AS DetailsUrl,
                                lc.CNRNumber, 0 AS DaysOverdue, lc.CurrentStage AS Stage, NULL AS CourtHall, 0 AS IsHighCourt
                            FROM LABOUR_CASES lc
                            LEFT JOIN LABOUR_COURTS lcourt ON lc.CourtID = lcourt.CourtID
                            WHERE CAST(lc.NextHearingDate AS DATE) = CAST(GETDATE() AS DATE)
                              AND (@DivisionID = 0 OR @DivisionID = 5 OR lc.DivisionID = @DivisionID)");

                        queryBuilder.Add(@"
                            SELECT
                                'Labour Service' AS ModuleName, s.ServiceID AS CaseID, 
                                ('WP ' + ISNULL(s.WPNumber, '')) AS CaseNo, 
                                'High Court' AS CourtOrAuthority, 
                                s.Status AS DisposalResult, s.CreatedDate AS CreatedAt, s.NextHearingDate, 
                                ('/Labour/ServiceMatterDetails/' + CAST(s.ServiceID AS VARCHAR)) AS DetailsUrl,
                                s.CNRNumber, 0 AS DaysOverdue, ISNULL(s.Status, '') AS Stage, NULL AS CourtHall, 1 AS IsHighCourt
                            FROM LABOUR_SERVICE_MATTERS s
                            WHERE CAST(s.NextHearingDate AS DATE) = CAST(GETDATE() AS DATE)
                              AND (@DivisionID = 0 OR @DivisionID = 5 OR s.DivisionID = @DivisionID)");
                    }

                    if (isAll || module == "Gratuity")
                    {
                        queryBuilder.Add(@"
                            SELECT
                                'Gratuity' AS ModuleName, gc.CaseID, ('GRA ' + ISNULL(gc.PGANumber, '')) AS CaseNo, 
                                gc.CourtType AS CourtOrAuthority, 
                                gc.DisposalResult, gc.CreatedDate AS CreatedAt, gc.NextHearingDate, 
                                ('/Gratuity/Details/' + CAST(gc.CaseID AS VARCHAR)) AS DetailsUrl,
                                NULL AS CNRNumber, 0 AS DaysOverdue, gc.CurrentStage AS Stage, NULL AS CourtHall, 0 AS IsHighCourt
                            FROM GRA_CASES gc
                            WHERE CAST(gc.NextHearingDate AS DATE) = CAST(GETDATE() AS DATE)
                              AND (@DivisionID = 0 OR @DivisionID = 5 OR gc.DivisionCode = @DivisionID)");
                    }

                    if (isAll || module == "Other")
                    {
                        queryBuilder.Add(@"
                            SELECT
                                'Other Courts' AS ModuleName, oc.CaseID, 
                                (ISNULL(oc.CaseType, 'OS') + ' ' + ISNULL(oc.CaseNumber, '') + ' / ' + CAST(ISNULL(oc.CaseYear, 0) AS VARCHAR)) AS CaseNo, 
                                ISNULL(oc.Court, '') AS CourtOrAuthority, 
                                oc.DisposalStatus AS DisposalResult, oc.CreatedDate AS CreatedAt, oc.NextDateOfHearing AS NextHearingDate, 
                                ('/Home/OtherCourtsCaseDetails/' + CAST(oc.CaseID AS VARCHAR)) AS DetailsUrl,
                                NULL AS CNRNumber, 0 AS DaysOverdue, oc.CaseStage AS Stage, NULL AS CourtHall, 0 AS IsHighCourt
                            FROM OTHER_CASES oc
                            WHERE CAST(oc.NextDateOfHearing AS DATE) = CAST(GETDATE() AS DATE)
                              AND (@DivisionID = 0 OR @DivisionID = 5 OR oc.DivisionID = @DivisionID)");
                    }

                    if (queryBuilder.Count == 0) return cases;

                    string finalQuery = string.Join(" UNION ALL ", queryBuilder) + " ORDER BY NextHearingDate ASC, CreatedAt DESC";

                    using (var cmd = new SqlCommand(finalQuery, conn))
                    {
                        cmd.Parameters.AddWithValue("@DivisionID", divisionId);
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                cases.Add(new GlobalCaseViewModel
                                {
                                    ModuleName = reader["ModuleName"].ToString() ?? "",
                                    CaseID = Convert.ToInt32(reader["CaseID"]),
                                    CaseNo = reader["CaseNo"].ToString() ?? "",
                                    CourtOrAuthority = reader["CourtOrAuthority"].ToString() ?? "",
                                    DisposalResult = reader["DisposalResult"]?.ToString(),
                                    CreatedAt = reader["CreatedAt"] != DBNull.Value ? Convert.ToDateTime(reader["CreatedAt"]) : DateTime.MinValue,
                                    NextHearingDate = reader["NextHearingDate"] != DBNull.Value ? Convert.ToDateTime(reader["NextHearingDate"]) : null,
                                    DetailsUrl = reader["DetailsUrl"].ToString() ?? "",
                                    CNRNumber = reader["CNRNumber"] != DBNull.Value ? reader["CNRNumber"].ToString() : null,
                                    DaysOverdue = reader["DaysOverdue"] != DBNull.Value ? Convert.ToInt32(reader["DaysOverdue"]) : 0,
                                    Stage = reader["Stage"] != DBNull.Value ? reader["Stage"].ToString() : null,
                                    CourtHall = reader["CourtHall"] != DBNull.Value ? reader["CourtHall"].ToString() : null,
                                    IsHighCourt = reader["IsHighCourt"] != DBNull.Value && Convert.ToBoolean(reader["IsHighCourt"])
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"GetHearingsTodayIrrespectiveOfModule Error: {ex.Message}");
            }
            return cases;
        }

        public List<GlobalCaseViewModel> GetAwardsTodayIrrespectiveOfModule(int divisionId = 0, string? module = null)
        {
            var cases = new List<GlobalCaseViewModel>();
            try
            {
                using (var conn = new SqlConnection(_db.GetConnectionString()))
                {
                    conn.Open();
                    var queryBuilder = new List<string>();
                    bool isAll = string.IsNullOrEmpty(module) || module == "All";

                    if (isAll || module == "MVC")
                    {
                        queryBuilder.Add(@"
                            SELECT 
                                'MVC' AS ModuleName, c.CaseID, ('MVC ' + c.MVCNo + ' / ' + CAST(c.MVCYear AS VARCHAR)) AS CaseNo, 
                                m.MACTName AS CourtOrAuthority, c.DisposalResult, c.CreatedAt, c.NextHearingDate, 
                                ('/Case/Details/' + CAST(c.CaseID AS VARCHAR)) AS DetailsUrl,
                                c.CNRNumber, 0 AS DaysOverdue, c.CurrentStage AS Stage, NULL AS CourtHall, 0 AS IsHighCourt
                            FROM MVC_CASE_ADVERSE_DETAILS ad 
                            JOIN MVC_CASES c ON ad.CaseID = c.CaseID
                            JOIN MACT_MASTER m ON c.MACTID = m.MACTID
                            WHERE (CAST(ad.AwardDate AS DATE) = CAST(GETDATE() AS DATE) OR ad.AwardDate >= DATEADD(day, -30, GETDATE()))
                              AND (@DivisionID = 0 OR @DivisionID = 5 OR c.DivisionID = @DivisionID)");
                    }

                    if (isAll || module == "Labour")
                    {
                        queryBuilder.Add(@"
                            SELECT
                                'Labour' AS ModuleName, lc.CaseID, 
                                (ISNULL(lc.CaseType, '') + ' ' + ISNULL(lc.CaseNumber, '') + ' / ' + CAST(ISNULL(lc.CaseYear, 0) AS VARCHAR)) AS CaseNo, 
                                ISNULL(lcourt.CourtName, lc.OtherCourtDetails) AS CourtOrAuthority, 
                                lc.DisposalResult, lc.CreatedDate AS CreatedAt, lc.NextHearingDate, 
                                ('/Labour/Details/' + CAST(lc.CaseID AS VARCHAR)) AS DetailsUrl,
                                lc.CNRNumber, 0 AS DaysOverdue, lc.CurrentStage AS Stage, NULL AS CourtHall, 0 AS IsHighCourt
                            FROM LABOUR_CASES lc
                            LEFT JOIN LABOUR_COURTS lcourt ON lc.CourtID = lcourt.CourtID
                            WHERE (CAST(lc.DisposalDate AS DATE) = CAST(GETDATE() AS DATE) OR lc.DisposalDate >= DATEADD(day, -30, GETDATE()))
                              AND lc.DisposalResult IN ('Against', 'Favor')
                              AND (@DivisionID = 0 OR @DivisionID = 5 OR lc.DivisionID = @DivisionID)");

                        queryBuilder.Add(@"
                            SELECT
                                'Labour Service' AS ModuleName, s.ServiceID AS CaseID, 
                                ('WP ' + ISNULL(s.WPNumber, '')) AS CaseNo, 
                                'High Court' AS CourtOrAuthority, 
                                s.Status AS DisposalResult, s.CreatedDate AS CreatedAt, s.NextHearingDate, 
                                ('/Labour/ServiceMatterDetails/' + CAST(s.ServiceID AS VARCHAR)) AS DetailsUrl,
                                s.CNRNumber, 0 AS DaysOverdue, ISNULL(s.Status, '') AS Stage, NULL AS CourtHall, 1 AS IsHighCourt
                            FROM LABOUR_SERVICE_MATTERS s
                            WHERE (CAST(s.DisposalDate AS DATE) = CAST(GETDATE() AS DATE) OR s.DisposalDate >= DATEADD(day, -30, GETDATE()))
                              AND (@DivisionID = 0 OR @DivisionID = 5 OR s.DivisionID = @DivisionID)");
                    }

                    if (isAll || module == "Gratuity")
                    {
                        queryBuilder.Add(@"
                            SELECT
                                'Gratuity' AS ModuleName, gc.CaseID, ('GRA ' + ISNULL(gc.PGANumber, '')) AS CaseNo, 
                                gc.CourtType AS CourtOrAuthority, 
                                gc.DisposalResult, gc.CreatedDate AS CreatedAt, gc.NextHearingDate, 
                                ('/Gratuity/Details/' + CAST(gc.CaseID AS VARCHAR)) AS DetailsUrl,
                                NULL AS CNRNumber, 0 AS DaysOverdue, gc.CurrentStage AS Stage, NULL AS CourtHall, 0 AS IsHighCourt
                            FROM GRA_CASES gc
                            WHERE (CAST(gc.DisposalDate AS DATE) = CAST(GETDATE() AS DATE) OR gc.DisposalDate >= DATEADD(day, -30, GETDATE()))
                              AND gc.DisposalResult IN ('Against', 'Dismissed')
                              AND (@DivisionID = 0 OR @DivisionID = 5 OR gc.DivisionCode = @DivisionID)");
                    }

                    if (queryBuilder.Count == 0) return cases;

                    string finalQuery = string.Join(" UNION ALL ", queryBuilder) + " ORDER BY CreatedAt DESC";

                    using (var cmd = new SqlCommand(finalQuery, conn))
                    {
                        cmd.Parameters.AddWithValue("@DivisionID", divisionId);
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                cases.Add(new GlobalCaseViewModel
                                {
                                    ModuleName = reader["ModuleName"].ToString() ?? "",
                                    CaseID = Convert.ToInt32(reader["CaseID"]),
                                    CaseNo = reader["CaseNo"].ToString() ?? "",
                                    CourtOrAuthority = reader["CourtOrAuthority"].ToString() ?? "",
                                    DisposalResult = reader["DisposalResult"]?.ToString(),
                                    CreatedAt = reader["CreatedAt"] != DBNull.Value ? Convert.ToDateTime(reader["CreatedAt"]) : DateTime.MinValue,
                                    NextHearingDate = reader["NextHearingDate"] != DBNull.Value ? Convert.ToDateTime(reader["NextHearingDate"]) : null,
                                    DetailsUrl = reader["DetailsUrl"].ToString() ?? "",
                                    CNRNumber = reader["CNRNumber"] != DBNull.Value ? reader["CNRNumber"].ToString() : null,
                                    DaysOverdue = reader["DaysOverdue"] != DBNull.Value ? Convert.ToInt32(reader["DaysOverdue"]) : 0,
                                    Stage = reader["Stage"] != DBNull.Value ? reader["Stage"].ToString() : null,
                                    CourtHall = reader["CourtHall"] != DBNull.Value ? reader["CourtHall"].ToString() : null,
                                    IsHighCourt = reader["IsHighCourt"] != DBNull.Value && Convert.ToBoolean(reader["IsHighCourt"])
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"GetAwardsTodayIrrespectiveOfModule Error: {ex.Message}");
            }
            return cases;
        }

        public List<GlobalCaseViewModel> GetComplianceDueIrrespectiveOfModule(int divisionId = 0, string? module = null)
        {
            var cases = new List<GlobalCaseViewModel>();
            try
            {
                using (var conn = new SqlConnection(_db.GetConnectionString()))
                {
                    conn.Open();
                    var queryBuilder = new List<string>();
                    bool isAll = string.IsNullOrEmpty(module) || module == "All";

                    if (isAll || module == "MVC")
                    {
                        queryBuilder.Add(@"
                            SELECT 
                                'MVC' AS ModuleName, c.CaseID, ('MVC ' + c.MVCNo + ' / ' + CAST(c.MVCYear AS VARCHAR)) AS CaseNo, 
                                m.MACTName AS CourtOrAuthority, c.DisposalResult, c.CreatedAt, a.FinalComplianceDate AS NextHearingDate, 
                                ('/Case/Details/' + CAST(c.CaseID AS VARCHAR)) AS DetailsUrl,
                                c.CNRNumber, DATEDIFF(day, GETDATE(), a.FinalComplianceDate) AS DaysOverdue, c.CurrentStage AS Stage, NULL AS CourtHall, 0 AS IsHighCourt
                            FROM APPEAL_DETAILS a 
                            JOIN MVC_CASES c ON a.CaseID = c.CaseID
                            JOIN MACT_MASTER m ON c.MACTID = m.MACTID
                            WHERE a.FinalComplianceDate IS NOT NULL
                              AND (
                                (a.FinalComplianceDate >= CAST(GETDATE() AS DATE) AND a.FinalComplianceDate <= DATEADD(day, 30, CAST(GETDATE() AS DATE)))
                                OR (a.FinalComplianceDate < CAST(GETDATE() AS DATE) AND (a.InitialAction LIKE '%Pending%' OR a.InitialAction IS NULL OR a.InitialAction = ''))
                              )
                              AND (@DivisionID = 0 OR @DivisionID = 5 OR c.DivisionID = @DivisionID)");
                    }

                    if (isAll || module == "Labour")
                    {
                        queryBuilder.Add(@"
                            SELECT
                                'Labour' AS ModuleName, lc.CaseID, 
                                (ISNULL(lc.CaseType, '') + ' ' + ISNULL(lc.CaseNumber, '') + ' / ' + CAST(ISNULL(lc.CaseYear, 0) AS VARCHAR)) AS CaseNo, 
                                ISNULL(lcourt.CourtName, lc.OtherCourtDetails) AS CourtOrAuthority, 
                                lc.DisposalResult, lc.CreatedDate AS CreatedAt, lc.CO_StayComplianceDate AS NextHearingDate, 
                                ('/Labour/Details/' + CAST(lc.CaseID AS VARCHAR)) AS DetailsUrl,
                                lc.CNRNumber, DATEDIFF(day, GETDATE(), lc.CO_StayComplianceDate) AS DaysOverdue, lc.CurrentStage AS Stage, NULL AS CourtHall, 0 AS IsHighCourt
                            FROM LABOUR_CASES lc
                            LEFT JOIN LABOUR_COURTS lcourt ON lc.CourtID = lcourt.CourtID
                            WHERE lc.CO_StayComplianceDate IS NOT NULL
                              AND (
                                (lc.CO_StayComplianceDate >= CAST(GETDATE() AS DATE) AND lc.CO_StayComplianceDate <= DATEADD(day, 30, CAST(GETDATE() AS DATE)))
                                OR (lc.CO_StayComplianceDate < CAST(GETDATE() AS DATE) AND lc.DisposalResult = 'Against')
                              )
                              AND (@DivisionID = 0 OR @DivisionID = 5 OR lc.DivisionID = @DivisionID)");
                    }

                    if (isAll || module == "Gratuity")
                    {
                        queryBuilder.Add(@"
                            SELECT
                                'Gratuity' AS ModuleName, gc.CaseID, ('GRA ' + ISNULL(gc.PGANumber, '')) AS CaseNo, 
                                gc.CourtType AS CourtOrAuthority, 
                                gc.DisposalResult, gc.CreatedDate AS CreatedAt, gc.StayComplianceDate AS NextHearingDate, 
                                ('/Gratuity/Details/' + CAST(gc.CaseID AS VARCHAR)) AS DetailsUrl,
                                NULL AS CNRNumber, DATEDIFF(day, GETDATE(), gc.StayComplianceDate) AS DaysOverdue, gc.CurrentStage AS Stage, NULL AS CourtHall, 0 AS IsHighCourt
                            FROM GRA_CASES gc
                            WHERE gc.StayComplianceDate IS NOT NULL
                              AND (
                                (gc.StayComplianceDate >= CAST(GETDATE() AS DATE) AND gc.StayComplianceDate <= DATEADD(day, 30, CAST(GETDATE() AS DATE)))
                                OR (gc.StayComplianceDate < CAST(GETDATE() AS DATE) AND gc.DisposalResult IN ('Against', 'Dismissed'))
                              )
                              AND (@DivisionID = 0 OR @DivisionID = 5 OR gc.DivisionCode = @DivisionID)");
                    }

                    if (queryBuilder.Count == 0) return cases;

                    string finalQuery = string.Join(" UNION ALL ", queryBuilder) + " ORDER BY NextHearingDate ASC, CreatedAt DESC";

                    using (var cmd = new SqlCommand(finalQuery, conn))
                    {
                        cmd.Parameters.AddWithValue("@DivisionID", divisionId);
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                cases.Add(new GlobalCaseViewModel
                                {
                                    ModuleName = reader["ModuleName"].ToString() ?? "",
                                    CaseID = Convert.ToInt32(reader["CaseID"]),
                                    CaseNo = reader["CaseNo"].ToString() ?? "",
                                    CourtOrAuthority = reader["CourtOrAuthority"].ToString() ?? "",
                                    DisposalResult = reader["DisposalResult"]?.ToString(),
                                    CreatedAt = reader["CreatedAt"] != DBNull.Value ? Convert.ToDateTime(reader["CreatedAt"]) : DateTime.MinValue,
                                    NextHearingDate = reader["NextHearingDate"] != DBNull.Value ? Convert.ToDateTime(reader["NextHearingDate"]) : null,
                                    DetailsUrl = reader["DetailsUrl"].ToString() ?? "",
                                    CNRNumber = reader["CNRNumber"] != DBNull.Value ? reader["CNRNumber"].ToString() : null,
                                    DaysOverdue = reader["DaysOverdue"] != DBNull.Value ? Convert.ToInt32(reader["DaysOverdue"]) : 0,
                                    Stage = reader["Stage"] != DBNull.Value ? reader["Stage"].ToString() : null,
                                    CourtHall = reader["CourtHall"] != DBNull.Value ? reader["CourtHall"].ToString() : null,
                                    IsHighCourt = reader["IsHighCourt"] != DBNull.Value && Convert.ToBoolean(reader["IsHighCourt"])
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"GetComplianceDueIrrespectiveOfModule Error: {ex.Message}");
            }
            return cases;
        }

        public List<GlobalCaseViewModel> GetCriticalDelaysIrrespectiveOfModule(int divisionId = 0, string? module = null)
        {
            var cases = new List<GlobalCaseViewModel>();
            try
            {
                using (var conn = new SqlConnection(_db.GetConnectionString()))
                {
                    conn.Open();
                    var queryBuilder = new List<string>();
                    bool isAll = string.IsNullOrEmpty(module) || module == "All";

                    if (isAll || module == "MVC")
                    {
                        queryBuilder.Add(@"
                            SELECT 
                                'MVC' AS ModuleName, c.CaseID, ('MVC ' + c.MVCNo + ' / ' + CAST(c.MVCYear AS VARCHAR)) AS CaseNo, 
                                m.MACTName AS CourtOrAuthority, c.DisposalResult, c.CreatedAt, c.NextHearingDate, 
                                ('/Case/Details/' + CAST(c.CaseID AS VARCHAR)) AS DetailsUrl,
                                c.CNRNumber, 
                                CASE WHEN c.NextHearingDate IS NOT NULL THEN DATEDIFF(day, c.NextHearingDate, GETDATE()) ELSE 999 END AS DaysOverdue,
                                c.CurrentStage AS Stage, NULL AS CourtHall, 0 AS IsHighCourt
                            FROM MVC_CASES c
                            JOIN MACT_MASTER m ON c.MACTID = m.MACTID
                            WHERE c.NextHearingDate < CAST(GETDATE() AS DATE) 
                              AND (c.DisposalResult IS NULL OR c.DisposalResult = 'Pending' OR c.DisposalResult = '')
                              AND (@DivisionID = 0 OR @DivisionID = 5 OR c.DivisionID = @DivisionID)");
                    }

                    if (isAll || module == "Labour")
                    {
                        queryBuilder.Add(@"
                            SELECT
                                'Labour' AS ModuleName, lc.CaseID, 
                                (ISNULL(lc.CaseType, '') + ' ' + ISNULL(lc.CaseNumber, '') + ' / ' + CAST(ISNULL(lc.CaseYear, 0) AS VARCHAR)) AS CaseNo, 
                                ISNULL(lcourt.CourtName, lc.OtherCourtDetails) AS CourtOrAuthority, 
                                lc.DisposalResult, lc.CreatedDate AS CreatedAt, lc.NextHearingDate, 
                                ('/Labour/Details/' + CAST(lc.CaseID AS VARCHAR)) AS DetailsUrl,
                                lc.CNRNumber,
                                CASE WHEN lc.NextHearingDate IS NOT NULL THEN DATEDIFF(day, lc.NextHearingDate, GETDATE()) ELSE 999 END AS DaysOverdue,
                                lc.CurrentStage AS Stage, NULL AS CourtHall, 0 AS IsHighCourt
                            FROM LABOUR_CASES lc
                            LEFT JOIN LABOUR_COURTS lcourt ON lc.CourtID = lcourt.CourtID
                            WHERE lc.NextHearingDate < CAST(GETDATE() AS DATE) 
                              AND (lc.DisposalResult IS NULL OR lc.DisposalResult = 'Pending' OR lc.CaseStatus = 'Pending' OR lc.CaseStatus = '' OR lc.CaseStatus IS NULL)
                              AND (@DivisionID = 0 OR @DivisionID = 5 OR lc.DivisionID = @DivisionID)");

                        queryBuilder.Add(@"
                            SELECT
                                'Labour Service' AS ModuleName, s.ServiceID AS CaseID, 
                                ('WP ' + ISNULL(s.WPNumber, '')) AS CaseNo, 
                                'High Court' AS CourtOrAuthority, 
                                s.Status AS DisposalResult, s.CreatedDate AS CreatedAt, s.NextHearingDate, 
                                ('/Labour/ServiceMatterDetails/' + CAST(s.ServiceID AS VARCHAR)) AS DetailsUrl,
                                s.CNRNumber,
                                CASE WHEN s.NextHearingDate IS NOT NULL THEN DATEDIFF(day, s.NextHearingDate, GETDATE()) ELSE 999 END AS DaysOverdue,
                                ISNULL(s.Status, '') AS Stage, NULL AS CourtHall, 1 AS IsHighCourt
                            FROM LABOUR_SERVICE_MATTERS s
                            WHERE s.NextHearingDate < CAST(GETDATE() AS DATE) 
                              AND (s.Status IS NULL OR s.Status = 'Pending' OR s.Status = '')
                              AND (@DivisionID = 0 OR @DivisionID = 5 OR s.DivisionID = @DivisionID)");
                    }

                    if (isAll || module == "Gratuity")
                    {
                        queryBuilder.Add(@"
                            SELECT
                                'Gratuity' AS ModuleName, gc.CaseID, ('GRA ' + ISNULL(gc.PGANumber, '')) AS CaseNo, 
                                gc.CourtType AS CourtOrAuthority, 
                                gc.DisposalResult, gc.CreatedDate AS CreatedAt, gc.NextHearingDate, 
                                ('/Gratuity/Details/' + CAST(gc.CaseID AS VARCHAR)) AS DetailsUrl,
                                NULL AS CNRNumber,
                                CASE WHEN gc.NextHearingDate IS NOT NULL THEN DATEDIFF(day, gc.NextHearingDate, GETDATE()) ELSE 999 END AS DaysOverdue,
                                gc.CurrentStage AS Stage, NULL AS CourtHall, 0 AS IsHighCourt
                            FROM GRA_CASES gc
                            WHERE gc.NextHearingDate < CAST(GETDATE() AS DATE) 
                              AND (gc.DisposalResult IS NULL OR gc.DisposalResult = 'Pending' OR gc.DisposalResult = '')
                              AND (@DivisionID = 0 OR @DivisionID = 5 OR gc.DivisionCode = @DivisionID)");
                    }

                    if (isAll || module == "Other")
                    {
                        queryBuilder.Add(@"
                            SELECT
                                'Other Courts' AS ModuleName, oc.CaseID, 
                                (ISNULL(oc.CaseType, 'OS') + ' ' + ISNULL(oc.CaseNumber, '') + ' / ' + CAST(ISNULL(oc.CaseYear, 0) AS VARCHAR)) AS CaseNo, 
                                ISNULL(oc.Court, '') AS CourtOrAuthority, 
                                oc.DisposalStatus AS DisposalResult, oc.CreatedDate AS CreatedAt, oc.NextDateOfHearing AS NextHearingDate, 
                                ('/Home/OtherCourtsCaseDetails/' + CAST(oc.CaseID AS VARCHAR)) AS DetailsUrl,
                                NULL AS CNRNumber,
                                CASE WHEN oc.NextDateOfHearing IS NOT NULL THEN DATEDIFF(day, oc.NextDateOfHearing, GETDATE()) ELSE 999 END AS DaysOverdue,
                                oc.CaseStage AS Stage, NULL AS CourtHall, 0 AS IsHighCourt
                            FROM OTHER_CASES oc
                            WHERE oc.NextDateOfHearing < CAST(GETDATE() AS DATE) 
                              AND (oc.DisposalStatus IS NULL OR oc.DisposalStatus = 'Pending' OR oc.DisposalStatus = '')
                              AND (@DivisionID = 0 OR @DivisionID = 5 OR oc.DivisionID = @DivisionID)");
                    }

                    if (queryBuilder.Count == 0) return cases;

                    string finalQuery = string.Join(" UNION ALL ", queryBuilder) + " ORDER BY DaysOverdue ASC, NextHearingDate ASC";

                    using (var cmd = new SqlCommand(finalQuery, conn))
                    {
                        cmd.Parameters.AddWithValue("@DivisionID", divisionId);
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                cases.Add(new GlobalCaseViewModel
                                {
                                    ModuleName = reader["ModuleName"].ToString() ?? "",
                                    CaseID = Convert.ToInt32(reader["CaseID"]),
                                    CaseNo = reader["CaseNo"].ToString() ?? "",
                                    CourtOrAuthority = reader["CourtOrAuthority"].ToString() ?? "",
                                    DisposalResult = reader["DisposalResult"]?.ToString(),
                                    CreatedAt = reader["CreatedAt"] != DBNull.Value ? Convert.ToDateTime(reader["CreatedAt"]) : DateTime.MinValue,
                                    NextHearingDate = reader["NextHearingDate"] != DBNull.Value ? Convert.ToDateTime(reader["NextHearingDate"]) : null,
                                    DetailsUrl = reader["DetailsUrl"].ToString() ?? "",
                                    CNRNumber = reader["CNRNumber"] != DBNull.Value ? reader["CNRNumber"].ToString() : null,
                                    DaysOverdue = reader["DaysOverdue"] != DBNull.Value ? Convert.ToInt32(reader["DaysOverdue"]) : 0,
                                    Stage = reader["Stage"] != DBNull.Value ? reader["Stage"].ToString() : null,
                                    CourtHall = reader["CourtHall"] != DBNull.Value ? reader["CourtHall"].ToString() : null,
                                    IsHighCourt = reader["IsHighCourt"] != DBNull.Value && Convert.ToBoolean(reader["IsHighCourt"])
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"GetCriticalDelaysIrrespectiveOfModule Error: {ex.Message}");
            }
            return cases;
        }

        public int CreateSkeletonCase(string mvcNo, int mvcYear, int mactId, int divisionId)
        {
            const string query = @"
                INSERT INTO MVC_CASES (MVCNo, MVCYear, MACTID, DivisionID, CreatedAt)
                OUTPUT INSERTED.CaseID
                VALUES (@MVCNo, @MVCYear, @MACTID, @DivisionID, GETDATE())";

            var parameters = new[] {
                new SqlParameter("@MVCNo", mvcNo),
                new SqlParameter("@MVCYear", mvcYear),
                new SqlParameter("@MACTID", mactId),
                new SqlParameter("@DivisionID", divisionId)
            };

            var dt = _db.ExecuteQuery(query, parameters);
            return (int)dt.Rows[0]["CaseID"];
        }

        public int SaveRemindBackCase(MVCCaseViewModel model)
        {
            using (var conn = new SqlConnection(_db.GetConnectionString()))
            {
                conn.Open();
                using (var trans = conn.BeginTransaction())
                {
                    try
                    {
                        // 1. Save Main Case to MVC_REMIND_BACK_CASES
                        string mainQuery = @"
                            INSERT INTO MVC_REMIND_BACK_CASES (DivisionID, MVCNo, MVCYear, MACTID, VehicleNo, AccidentDate, CaseType, StatusID,
                                                             ClaimType, ThirdPartyFlag, ClaimAmount, AdvocateID, EntrustmentNo, EntrustmentDate,
                                                             DoubleClaimFlag, NextHearingDate, CurrentStage, DisposalStatus, DisposalResult, DisposalRemarks,
                                                              IsDocumentSent, DocumentOutwardNo, DocumentOutwardDate, IsObjectionFiled, ObjectionFiledDate, ObjectionOutwardNo, IsEvidenceFiled,
                                                AdvocateName, VehicleType, ClosureDate, OriginalCaseID, CNRNumber, EstCode, CaseTypeCode)
                            OUTPUT INSERTED.RemindBackID
                            VALUES (@DivisionID, @MVCNo, @MVCYear, @MACTID, @VehicleNo, @AccidentDate, @CaseType, @StatusID,
                                    @ClaimType, @ThirdPartyFlag, @ClaimAmount, @AdvocateID, @EntrustmentNo, @EntrustmentDate,
                                    @DoubleClaimFlag, @NextHearingDate, @CurrentStage, @DisposalStatus, @DisposalResult, @DisposalRemarks,
                                    @IsDocumentSent, @DocumentOutwardNo, @DocumentOutwardDate, @IsObjectionFiled, @ObjectionFiledDate, @ObjectionOutwardNo, @IsEvidenceFiled,
                                    @AdvocateName, @VehicleType, @ClosureDate, @OriginalCaseID, @CNRNumber, @EstCode, @CaseTypeCode)";

                        var cmd = new SqlCommand(mainQuery, conn, trans);
                        cmd.Parameters.AddWithValue("@DivisionID", model.DivisionID);
                        cmd.Parameters.AddWithValue("@MVCNo", model.MVCNo);
                        cmd.Parameters.AddWithValue("@MVCYear", model.MVCYear);
                        cmd.Parameters.AddWithValue("@MACTID", model.MACTID);
                        cmd.Parameters.AddWithValue("@VehicleNo", model.VehicleNo ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@AccidentDate", model.AccidentDate ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@CaseType", model.CaseType);
                        cmd.Parameters.AddWithValue("@StatusID", model.StatusID);
                        cmd.Parameters.AddWithValue("@ClaimType", model.ClaimType ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@ThirdPartyFlag", model.ThirdPartyFlag);
                        cmd.Parameters.AddWithValue("@ClaimAmount", model.ClaimAmount ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@AdvocateID", model.AdvocateID ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@EntrustmentNo", model.EntrustmentNo ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@EntrustmentDate", model.EntrustmentDate ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@DoubleClaimFlag", model.DoubleClaimFlag);
                        cmd.Parameters.AddWithValue("@NextHearingDate", model.NextHearingDate ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@CurrentStage", model.CurrentStage ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@DisposalStatus", model.DisposalStatus ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@DisposalResult", model.DisposalResult ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@DisposalRemarks", model.DisposalRemarks ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@IsDocumentSent", model.IsDocumentSent);
                        cmd.Parameters.AddWithValue("@DocumentOutwardNo", model.DocumentOutwardNo ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@DocumentOutwardDate", model.DocumentOutwardDate ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@IsObjectionFiled", model.IsObjectionFiled);
                        cmd.Parameters.AddWithValue("@VehicleType", model.VehicleType ?? "Corporation");
                        cmd.Parameters.AddWithValue("@ObjectionFiledDate", model.ObjectionFiledDate ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@IsEvidenceFiled", model.IsEvidenceFiled);
                        cmd.Parameters.AddWithValue("@AdvocateName", model.AdvocateName ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@ObjectionOutwardNo", model.ObjectionOutwardNo ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@ClosureDate", model.ClosureDate ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@OriginalCaseID", model.OriginalCaseID ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@CNRNumber", (object?)model.CNRNumber ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@EstCode", (object?)model.EstCode ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@CaseTypeCode", (object?)model.CaseTypeCode ?? DBNull.Value);

                        int rbId = (int)cmd.ExecuteScalar();

                        // 2. Save Petitioners
                        foreach (var pet in model.Petitioners)
                        {
                            var petCmd = new SqlCommand("INSERT INTO MVC_REMIND_BACK_PETITIONERS (RemindBackID, PetitionerName, Relationship) VALUES (@RemindBackID, @PetitionerName, @Relationship)", conn, trans);
                            petCmd.Parameters.AddWithValue("@RemindBackID", rbId);
                            petCmd.Parameters.AddWithValue("@PetitionerName", pet.PetitionerName);
                            petCmd.Parameters.AddWithValue("@Relationship", pet.Relationship ?? (object)DBNull.Value);
                            petCmd.ExecuteNonQuery();
                        }

                        // 2.1 Save Respondents (Third Party Remind Back)
                        if (model.Respondents != null)
                        {
                            foreach (var resp in model.Respondents)
                            {
                                if (!string.IsNullOrEmpty(resp.RespondentName))
                                {
                                    var respCmd = new SqlCommand("INSERT INTO MVC_REMIND_BACK_RESPONDENTS (RemindBackID, RespondentName, Remarks) VALUES (@RemindBackID, @RespondentName, @Remarks)", conn, trans);
                                    respCmd.Parameters.AddWithValue("@RemindBackID", rbId);
                                    respCmd.Parameters.AddWithValue("@RespondentName", resp.RespondentName);
                                    respCmd.Parameters.AddWithValue("@Remarks", resp.Remarks ?? (object)DBNull.Value);
                                    respCmd.ExecuteNonQuery();
                                }
                            }
                        }

                        // 3. Save Connected Cases
                        foreach (var connCase in model.ConnectedCases)
                        {
                            var connCmd = new SqlCommand("INSERT INTO MVC_REMIND_BACK_CONNECTED (RemindBackID, ConnectedMVCNo, ConnectedYear, Remarks, MACT, IsDoubleClaim) VALUES (@RemindBackID, @ConnectedMVCNo, @ConnectedYear, @Remarks, @MACT, @IsDoubleClaim)", conn, trans);
                            connCmd.Parameters.AddWithValue("@RemindBackID", rbId);
                            connCmd.Parameters.AddWithValue("@ConnectedMVCNo", connCase.ConnectedMVCNo);
                            connCmd.Parameters.AddWithValue("@ConnectedYear", connCase.ConnectedYear);
                            connCmd.Parameters.AddWithValue("@Remarks", connCase.Remarks ?? (object)DBNull.Value);
                            connCmd.Parameters.AddWithValue("@MACT", connCase.MACT ?? (object)DBNull.Value);
                            connCmd.Parameters.AddWithValue("@IsDoubleClaim", connCase.IsDoubleClaim);
                            connCmd.ExecuteNonQuery();
                        }

                        // 4. Handle Adverse Award Details
                        if (model.DisposalResult == "Against")
                        {
                            string advQuery = @"
                                INSERT INTO MVC_REMIND_BACK_ADVERSE_DETAILS (RemindBackID, BusInsuranceDetails, IsBusInsured, ClaimPetitionDate, AwardDate, MannerOfAccident,
                                    ObjectionFiled, ObjectionRemarks, RWType, TR18Remarks, TR18UploadPath, IsAllegedAccident,
                                    AdverseDoubleClaimFlag, DoubleClaimDetails, SecurityRequired, SecurityUploadPath, GovIDProofUploadPath,
                                    DisposedOnDate, CopyAppliedDate, CertifiedCopyRemarks, CopyIssuedDate, CopyReceivedDate, CopyDeliveredDate,
                                    AwardAmount, InterestRate, VictimAge, Occupation, IncomeConsidered, IncomePeriod, InjuryDetails,
                                    DriverPunishmentStatus, PunishmentOrderUploadPath, PunishmentRemarks, DisabilityPercentage, InterimCompAmount, InterimCompDeducted,
                                    AdvocateOpinion, LOOpinion, DCOpinion, ForwardingStatus, ClosureRemarks, ClosureDate, OutwardNumber, OutwardDate,
                                    IsCorpLiable, LiabilityPercentage, LiabilityRemarks, AdverseJudgmentUploadPath, FutureProspectus, InjuryType, TreatedDocFlag,
                                    IsEPFiled, EPDepositedAmount, IsFIRFiled, IsChargeSheetFiled, IsBusCameraInstalled, IsCameraFootageProduced, 
                                    IsPhotographProduced, PhotographNotProducedReason, PoliceSketchExhibitNo, IsPoliceSketchEnclosed, 
                                    IsEvidenceBasedOnSecurityReport, SecurityReportNoEvidenceReason, IsImpleadingAppFiled, ImpleadingAppNotFiledReason, 
                                    IsVictimSalaried, IsIncomeCrossVerified, DoesIncomeTallyWithDocuments, IsMedicalBillsVerified, IsAmountDepositedInEP, 
                                    FutureProspectsPercentage, PersonalExpensesDeduction, Multiplier, LossOfDependency, LossOfConsortium, 
                                    LossOfEstate, FuneralExpenses, LossOfLoveAffection, MedicalExpenseOther, AgeProofUploadPath, PainSufferings, ConveyanceAttendant, 
                                    LossOfFutureIncome, LossOfIncomeLaidUp, LossOfAmenities, FutureMedicalExpenses, InjuryOtherExpense,
                                    IsSTPassenger, IsMedicalExpensesPaid, MedicalPaidAmount, MedicalPaidRemarks, IsARFAmountPaid, ARFPaidAmount, 
                                    ARFPaidRemarks, IsDelayApplicationFiled, DelayApplicationPath, IsDelayCondonedAdverse, DelayCondonedOrderPath, IsMedicalInsuranceClaimed,
                                    IsDeceasedInTR18, MannerOfAccidentRO, CustomCompensation, EPNumber, EPCourt, EPStage, EPNextHearingDate, RoundOffAmount, AsPerECourts)
                                VALUES
                                    (@RemindBackID, @BusInsuranceDetails, @IsBusInsured, @ClaimPetitionDate, @AwardDate, @MannerOfAccident, @ObjectionFiled, @ObjectionRemarks, @RWType, @TR18Remarks, @TR18UploadPath, @IsAllegedAccident, @AdverseDoubleClaimFlag, @DoubleClaimDetails, @SecurityRequired, @SecurityUploadPath, @GovIDProofUploadPath, @DisposedOnDate, @CopyAppliedDate, @CertifiedCopyRemarks, @CopyIssuedDate, @CopyReceivedDate, @CopyDeliveredDate, @AwardAmount, @InterestRate, @VictimAge, @Occupation, @IncomeConsidered, @IncomePeriod, @InjuryDetails, @DriverPunishmentStatus, @PunishmentOrderUploadPath, @PunishmentRemarks, @DisabilityPercentage, @InterimCompAmount, @InterimCompDeducted, @AdvocateOpinion, @LOOpinion, @DCOpinion, @ForwardingStatus, @ClosureRemarks, @ClosureDate, @OutwardNumber, @OutwardDate, @IsCorpLiable, @LiabilityPercentage, @LiabilityRemarks, @AdverseJudgmentUploadPath, @FutureProspectus, @InjuryType, @TreatedDocFlag, @IsEPFiled, @EPDepositedAmount, @IsFIRFiled, @IsChargeSheetFiled, @IsBusCameraInstalled, @IsCameraFootageProduced, @IsPhotographProduced, @PhotographNotProducedReason, @PoliceSketchExhibitNo, @IsPoliceSketchEnclosed, @IsEvidenceBasedOnSecurityReport, @SecurityReportNoEvidenceReason, @IsImpleadingAppFiled, @ImpleadingAppNotFiledReason, @IsVictimSalaried, @IsIncomeCrossVerified, @DoesIncomeTallyWithDocuments, @IsMedicalBillsVerified, @IsAmountDepositedInEP, @FutureProspectsPercentage, @PersonalExpensesDeduction, @Multiplier, @LossOfDependency, @LossOfConsortium, @LossOfEstate, @FuneralExpenses, @LossOfLoveAffection, @MedicalExpenseOther, @AgeProofUploadPath, @PainSufferings, @ConveyanceAttendant, @LossOfFutureIncome, @LossOfIncomeLaidUp, @LossOfAmenities, @FutureMedicalExpenses, @InjuryOtherExpense, @IsSTPassenger, @IsMedicalExpensesPaid, @MedicalPaidAmount, @MedicalPaidRemarks, @IsARFAmountPaid, @ARFPaidAmount, @ARFPaidRemarks, @IsDelayApplicationFiled, @DelayApplicationPath, @IsDelayCondonedAdverse, @DelayCondonedOrderPath, @IsMedicalInsuranceClaimed, @IsDeceasedInTR18, @MannerOfAccidentRO, @CustomCompensation, @EPNumber, @EPCourt, @EPStage, @EPNextHearingDate, @RoundOffAmount, @AsPerECourts)";

                            var adverseAward = model.AdverseAward;
                            var advCmd = new SqlCommand(advQuery, conn, trans);
                            advCmd.Parameters.AddWithValue("@RemindBackID", rbId);
                            advCmd.Parameters.AddWithValue("@BusInsuranceDetails", adverseAward.BusInsuranceDetails ?? (object)DBNull.Value);
                            advCmd.Parameters.AddWithValue("@IsBusInsured", adverseAward.IsBusInsured);
                            advCmd.Parameters.AddWithValue("@ClaimPetitionDate", adverseAward.ClaimPetitionDate ?? (object)DBNull.Value);
                            advCmd.Parameters.AddWithValue("@AwardDate", adverseAward.AwardDate ?? (object)DBNull.Value);
                            advCmd.Parameters.AddWithValue("@MannerOfAccident", adverseAward.MannerOfAccident ?? (object)DBNull.Value);
                            advCmd.Parameters.AddWithValue("@ObjectionFiled", adverseAward.ObjectionFiled);
                            advCmd.Parameters.AddWithValue("@ObjectionRemarks", adverseAward.ObjectionRemarks ?? (object)DBNull.Value);
                            advCmd.Parameters.AddWithValue("@RWType", adverseAward.RWType ?? (object)DBNull.Value);
                            advCmd.Parameters.AddWithValue("@TR18Remarks", adverseAward.TR18Remarks ?? (object)DBNull.Value);
                            advCmd.Parameters.AddWithValue("@TR18UploadPath", adverseAward.TR18UploadPath ?? (object)DBNull.Value);
                            advCmd.Parameters.AddWithValue("@IsAllegedAccident", adverseAward.IsAllegedAccident);
                            advCmd.Parameters.AddWithValue("@AdverseDoubleClaimFlag", adverseAward.DoubleClaimFlag);
                            advCmd.Parameters.AddWithValue("@DoubleClaimDetails", adverseAward.DoubleClaimDetails ?? (object)DBNull.Value);
                            advCmd.Parameters.AddWithValue("@SecurityRequired", adverseAward.SecurityRequired);
                            advCmd.Parameters.AddWithValue("@SecurityUploadPath", adverseAward.SecurityUploadPath ?? (object)DBNull.Value);
                            advCmd.Parameters.AddWithValue("@GovIDProofUploadPath", adverseAward.GovIDProofUploadPath ?? (object)DBNull.Value);
                            advCmd.Parameters.AddWithValue("@DisposedOnDate", adverseAward.DisposedOnDate ?? (object)DBNull.Value);
                            advCmd.Parameters.AddWithValue("@CopyAppliedDate", adverseAward.CopyAppliedDate ?? (object)DBNull.Value);
                            advCmd.Parameters.AddWithValue("@CertifiedCopyRemarks", adverseAward.CertifiedCopyRemarks ?? (object)DBNull.Value);
                            advCmd.Parameters.AddWithValue("@CopyIssuedDate", adverseAward.CopyIssuedDate ?? (object)DBNull.Value);
                            advCmd.Parameters.AddWithValue("@CopyReceivedDate", adverseAward.CopyReceivedDate ?? (object)DBNull.Value);
                            advCmd.Parameters.AddWithValue("@CopyDeliveredDate", adverseAward.CopyDeliveredDate ?? (object)DBNull.Value);
                            advCmd.Parameters.AddWithValue("@AwardAmount", adverseAward.AwardAmount ?? (object)DBNull.Value);
                            advCmd.Parameters.AddWithValue("@InterestRate", adverseAward.InterestRate ?? (object)DBNull.Value);
                            advCmd.Parameters.AddWithValue("@VictimAge", adverseAward.VictimAge ?? (object)DBNull.Value);
                            advCmd.Parameters.AddWithValue("@Occupation", adverseAward.Occupation ?? (object)DBNull.Value);
                            advCmd.Parameters.AddWithValue("@IncomeConsidered", adverseAward.IncomeConsidered ?? (object)DBNull.Value);
                            advCmd.Parameters.AddWithValue("@IncomePeriod", adverseAward.IncomePeriod ?? "monthly");
                            advCmd.Parameters.AddWithValue("@InjuryDetails", adverseAward.InjuryDetails ?? (object)DBNull.Value);
                            advCmd.Parameters.AddWithValue("@DriverPunishmentStatus", adverseAward.DriverPunishmentStatus ?? (object)DBNull.Value);
                            advCmd.Parameters.AddWithValue("@PunishmentOrderUploadPath", adverseAward.PunishmentOrderUploadPath ?? (object)DBNull.Value);
                            advCmd.Parameters.AddWithValue("@PunishmentRemarks", adverseAward.PunishmentRemarks ?? (object)DBNull.Value);
                            advCmd.Parameters.AddWithValue("@DisabilityPercentage", adverseAward.DisabilityPercentage ?? (object)DBNull.Value);
                            advCmd.Parameters.AddWithValue("@InterimCompAmount", adverseAward.InterimCompAmount ?? (object)DBNull.Value);
                            advCmd.Parameters.AddWithValue("@InterimCompDeducted", adverseAward.InterimCompDeducted);
                            advCmd.Parameters.AddWithValue("@AdvocateOpinion", adverseAward.AdvocateOpinion ?? (object)DBNull.Value);
                            advCmd.Parameters.AddWithValue("@LOOpinion", adverseAward.LOOpinion ?? (object)DBNull.Value);
                            advCmd.Parameters.AddWithValue("@DCOpinion", adverseAward.DCOpinion ?? (object)DBNull.Value);
                            advCmd.Parameters.AddWithValue("@ForwardingStatus", adverseAward.ForwardingStatus ?? (object)DBNull.Value);
                            advCmd.Parameters.AddWithValue("@ClosureRemarks", adverseAward.ClosureRemarks ?? (object)DBNull.Value);
                            advCmd.Parameters.AddWithValue("@ClosureDate", adverseAward.ClosureDate ?? (object)DBNull.Value);
                            advCmd.Parameters.AddWithValue("@OutwardNumber", adverseAward.OutwardNumber ?? (object)DBNull.Value);
                            advCmd.Parameters.AddWithValue("@OutwardDate", adverseAward.OutwardDate ?? (object)DBNull.Value);
                            advCmd.Parameters.AddWithValue("@IsCorpLiable", adverseAward.IsCorpLiable);
                            advCmd.Parameters.AddWithValue("@LiabilityPercentage", adverseAward.LiabilityPercentage ?? (object)DBNull.Value);
                            advCmd.Parameters.AddWithValue("@LiabilityRemarks", adverseAward.LiabilityRemarks ?? (object)DBNull.Value);
                            advCmd.Parameters.AddWithValue("@AdverseJudgmentUploadPath", adverseAward.AdverseJudgmentUploadPath ?? (object)DBNull.Value);
                            advCmd.Parameters.AddWithValue("@FutureProspectus", adverseAward.FutureProspectus ?? (object)DBNull.Value);
                            advCmd.Parameters.AddWithValue("@InjuryType", adverseAward.InjuryType ?? (object)DBNull.Value);
                            advCmd.Parameters.AddWithValue("@TreatedDocFlag", adverseAward.TreatedDocFlag);
                            advCmd.Parameters.AddWithValue("@IsEPFiled", adverseAward.IsEPFiled);
                            advCmd.Parameters.AddWithValue("@EPDepositedAmount", adverseAward.EPDepositedAmount.HasValue ? (object)adverseAward.EPDepositedAmount.Value : DBNull.Value);
                            advCmd.Parameters.AddWithValue("@IsFIRFiled", adverseAward.IsFIRFiled.HasValue ? (object)adverseAward.IsFIRFiled.Value : DBNull.Value);
                            advCmd.Parameters.AddWithValue("@IsChargeSheetFiled", adverseAward.IsChargeSheetFiled.HasValue ? (object)adverseAward.IsChargeSheetFiled.Value : DBNull.Value);
                            advCmd.Parameters.AddWithValue("@IsBusCameraInstalled", adverseAward.IsBusCameraInstalled.HasValue ? (object)adverseAward.IsBusCameraInstalled.Value : DBNull.Value);
                            advCmd.Parameters.AddWithValue("@IsCameraFootageProduced", adverseAward.IsCameraFootageProduced.HasValue ? (object)adverseAward.IsCameraFootageProduced.Value : DBNull.Value);
                            advCmd.Parameters.AddWithValue("@IsPhotographProduced", adverseAward.IsPhotographProduced.HasValue ? (object)adverseAward.IsPhotographProduced.Value : DBNull.Value);
                            advCmd.Parameters.AddWithValue("@PhotographNotProducedReason", adverseAward.PhotographNotProducedReason ?? (object)DBNull.Value);
                            advCmd.Parameters.AddWithValue("@PoliceSketchExhibitNo", adverseAward.PoliceSketchExhibitNo ?? (object)DBNull.Value);
                            advCmd.Parameters.AddWithValue("@IsPoliceSketchEnclosed", adverseAward.IsPoliceSketchEnclosed.HasValue ? (object)adverseAward.IsPoliceSketchEnclosed.Value : DBNull.Value);
                            advCmd.Parameters.AddWithValue("@IsEvidenceBasedOnSecurityReport", adverseAward.IsEvidenceBasedOnSecurityReport.HasValue ? (object)adverseAward.IsEvidenceBasedOnSecurityReport.Value : DBNull.Value);
                            advCmd.Parameters.AddWithValue("@SecurityReportNoEvidenceReason", adverseAward.SecurityReportNoEvidenceReason ?? (object)DBNull.Value);
                            advCmd.Parameters.AddWithValue("@IsImpleadingAppFiled", adverseAward.IsImpleadingAppFiled.HasValue ? (object)adverseAward.IsImpleadingAppFiled.Value : DBNull.Value);
                            advCmd.Parameters.AddWithValue("@ImpleadingAppNotFiledReason", adverseAward.ImpleadingAppNotFiledReason ?? (object)DBNull.Value);
                            advCmd.Parameters.AddWithValue("@IsVictimSalaried", adverseAward.IsVictimSalaried.HasValue ? (object)adverseAward.IsVictimSalaried.Value : DBNull.Value);
                            advCmd.Parameters.AddWithValue("@IsIncomeCrossVerified", adverseAward.IsIncomeCrossVerified.HasValue ? (object)adverseAward.IsIncomeCrossVerified.Value : DBNull.Value);
                            advCmd.Parameters.AddWithValue("@DoesIncomeTallyWithDocuments", adverseAward.DoesIncomeTallyWithDocuments.HasValue ? (object)adverseAward.DoesIncomeTallyWithDocuments.Value : DBNull.Value);
                            advCmd.Parameters.AddWithValue("@IsMedicalBillsVerified", adverseAward.IsMedicalBillsVerified.HasValue ? (object)adverseAward.IsMedicalBillsVerified.Value : DBNull.Value);
                            advCmd.Parameters.AddWithValue("@IsAmountDepositedInEP", adverseAward.IsAmountDepositedInEP);
                            advCmd.Parameters.AddWithValue("@FutureProspectsPercentage", adverseAward.FutureProspectsPercentage.HasValue ? (object)adverseAward.FutureProspectsPercentage.Value : DBNull.Value);
                            advCmd.Parameters.AddWithValue("@PersonalExpensesDeduction", adverseAward.PersonalExpensesDeduction.HasValue ? (object)adverseAward.PersonalExpensesDeduction.Value : DBNull.Value);
                            advCmd.Parameters.AddWithValue("@Multiplier", adverseAward.Multiplier.HasValue ? (object)adverseAward.Multiplier.Value : DBNull.Value);
                            advCmd.Parameters.AddWithValue("@LossOfDependency", adverseAward.LossOfDependency.HasValue ? (object)adverseAward.LossOfDependency.Value : DBNull.Value);
                            advCmd.Parameters.AddWithValue("@LossOfConsortium", adverseAward.LossOfConsortium.HasValue ? (object)adverseAward.LossOfConsortium.Value : DBNull.Value);
                            advCmd.Parameters.AddWithValue("@LossOfEstate", adverseAward.LossOfEstate.HasValue ? (object)adverseAward.LossOfEstate.Value : DBNull.Value);
                            advCmd.Parameters.AddWithValue("@FuneralExpenses", adverseAward.FuneralExpenses.HasValue ? (object)adverseAward.FuneralExpenses.Value : DBNull.Value);
                            advCmd.Parameters.AddWithValue("@LossOfLoveAffection", adverseAward.LossOfLoveAffection.HasValue ? (object)adverseAward.LossOfLoveAffection.Value : DBNull.Value);
                            advCmd.Parameters.AddWithValue("@MedicalExpenseOther", adverseAward.MedicalExpenseOther.HasValue ? (object)adverseAward.MedicalExpenseOther.Value : DBNull.Value);
                            advCmd.Parameters.AddWithValue("@AgeProofUploadPath", adverseAward.AgeProofUploadPath ?? (object)DBNull.Value);
                            advCmd.Parameters.AddWithValue("@PainSufferings", adverseAward.PainSufferings.HasValue ? (object)adverseAward.PainSufferings.Value : DBNull.Value);
                            advCmd.Parameters.AddWithValue("@ConveyanceAttendant", adverseAward.ConveyanceAttendant.HasValue ? (object)adverseAward.ConveyanceAttendant.Value : DBNull.Value);
                            advCmd.Parameters.AddWithValue("@LossOfFutureIncome", adverseAward.LossOfFutureIncome.HasValue ? (object)adverseAward.LossOfFutureIncome.Value : DBNull.Value);
                            advCmd.Parameters.AddWithValue("@LossOfIncomeLaidUp", adverseAward.LossOfIncomeLaidUp.HasValue ? (object)adverseAward.LossOfIncomeLaidUp.Value : DBNull.Value);
                            advCmd.Parameters.AddWithValue("@LossOfAmenities", adverseAward.LossOfAmenities.HasValue ? (object)adverseAward.LossOfAmenities.Value : DBNull.Value);
                            advCmd.Parameters.AddWithValue("@FutureMedicalExpenses", adverseAward.FutureMedicalExpenses.HasValue ? (object)adverseAward.FutureMedicalExpenses.Value : DBNull.Value);
                            advCmd.Parameters.AddWithValue("@InjuryOtherExpense", adverseAward.InjuryOtherExpense.HasValue ? (object)adverseAward.InjuryOtherExpense.Value : DBNull.Value);
                            advCmd.Parameters.AddWithValue("@IsSTPassenger", model.IsSTPassenger);
                            advCmd.Parameters.AddWithValue("@IsMedicalExpensesPaid", model.IsMedicalExpensesPaid);
                            advCmd.Parameters.AddWithValue("@MedicalPaidAmount", model.MedicalPaidAmount.HasValue ? (object)model.MedicalPaidAmount.Value : DBNull.Value);
                            advCmd.Parameters.AddWithValue("@MedicalPaidRemarks", model.MedicalPaidRemarks ?? (object)DBNull.Value);
                            advCmd.Parameters.AddWithValue("@IsARFAmountPaid", model.IsARFAmountPaid);
                            advCmd.Parameters.AddWithValue("@ARFPaidAmount", model.ARFPaidAmount.HasValue ? (object)model.ARFPaidAmount.Value : DBNull.Value);
                            advCmd.Parameters.AddWithValue("@ARFPaidRemarks", model.ARFPaidRemarks ?? (object)DBNull.Value);
                            advCmd.Parameters.AddWithValue("@IsDelayApplicationFiled", adverseAward.IsDelayApplicationFiled);
                            advCmd.Parameters.AddWithValue("@DelayApplicationPath", adverseAward.DelayApplicationPath ?? (object)DBNull.Value);
                            advCmd.Parameters.AddWithValue("@IsDelayCondonedAdverse", adverseAward.IsDelayCondonedAdverse.HasValue ? (object)adverseAward.IsDelayCondonedAdverse.Value : DBNull.Value);
                            advCmd.Parameters.AddWithValue("@DelayCondonedOrderPath", adverseAward.DelayCondonedOrderPath ?? (object)DBNull.Value);
                            advCmd.Parameters.AddWithValue("@IsMedicalInsuranceClaimed", adverseAward.IsMedicalInsuranceClaimed);
                            advCmd.Parameters.AddWithValue("@IsDeceasedInTR18", adverseAward.IsDeceasedInTR18);
                            advCmd.Parameters.AddWithValue("@MannerOfAccidentRO", adverseAward.MannerOfAccidentRO ?? (object)DBNull.Value);

                            if (adverseAward.CustomCompensationHeads != null && adverseAward.CustomCompensationHeads.Count > 0)
                                advCmd.Parameters.AddWithValue("@CustomCompensation", System.Text.Json.JsonSerializer.Serialize(adverseAward.CustomCompensationHeads));
                            else
                                advCmd.Parameters.AddWithValue("@CustomCompensation", DBNull.Value);

                            advCmd.Parameters.AddWithValue("@EPNumber", adverseAward.EPNumber ?? (object)DBNull.Value);
                            advCmd.Parameters.AddWithValue("@EPCourt", adverseAward.EPCourt ?? (object)DBNull.Value);
                            advCmd.Parameters.AddWithValue("@EPStage", adverseAward.EPStage ?? (object)DBNull.Value);
                            advCmd.Parameters.AddWithValue("@EPNextHearingDate", adverseAward.EPNextHearingDate.HasValue ? (object)adverseAward.EPNextHearingDate.Value : DBNull.Value);
                            advCmd.Parameters.AddWithValue("@RoundOffAmount", adverseAward.RoundOffAmount.HasValue ? (object)adverseAward.RoundOffAmount.Value : DBNull.Value);
                            advCmd.Parameters.AddWithValue("@AsPerECourts", adverseAward.AsPerECourts ?? (object)DBNull.Value);

                            advCmd.ExecuteNonQuery();

                            // 4.1 Save PWs (Petitioner PWs & Doctors)
                            if (adverseAward.PetitionerPWNames != null)
                            {
                                foreach (var pw in adverseAward.PetitionerPWNames)
                                {
                                    if (!string.IsNullOrWhiteSpace(pw))
                                    {
                                        var pwCmd = new SqlCommand("INSERT INTO MVC_REMIND_BACK_ADVERSE_PW (RemindBackID, PWName, Type) VALUES (@RemindBackID, @Name, 'Petitioner')", conn, trans);
                                        pwCmd.Parameters.AddWithValue("@RemindBackID", rbId);
                                        pwCmd.Parameters.AddWithValue("@Name", pw);
                                        pwCmd.ExecuteNonQuery();
                                    }
                                }
                            }
                            if (adverseAward.Doctors != null)
                            {
                                foreach (var dr in adverseAward.Doctors)
                                {
                                    if (!string.IsNullOrWhiteSpace(dr.DoctorName))
                                    {
                                        var pwCmd = new SqlCommand("INSERT INTO MVC_REMIND_BACK_ADVERSE_PW (RemindBackID, PWName, Type, IsTreated) VALUES (@RemindBackID, @Name, 'Doctor', @IsTreated)", conn, trans);
                                        pwCmd.Parameters.AddWithValue("@RemindBackID", rbId);
                                        pwCmd.Parameters.AddWithValue("@Name", dr.DoctorName);
                                        pwCmd.Parameters.AddWithValue("@IsTreated", dr.IsTreated);
                                        pwCmd.ExecuteNonQuery();
                                    }
                                }
                            }

                            // 4.2 Save RWs
                            if (adverseAward.RWNames != null)
                            {
                                foreach (var rw in adverseAward.RWNames)
                                {
                                    if (!string.IsNullOrWhiteSpace(rw))
                                    {
                                        var rwCmd = new SqlCommand("INSERT INTO MVC_REMIND_BACK_ADVERSE_RW (RemindBackID, RWName) VALUES (@RemindBackID, @Name)", conn, trans);
                                        rwCmd.Parameters.AddWithValue("@RemindBackID", rbId);
                                        rwCmd.Parameters.AddWithValue("@Name", rw);
                                        rwCmd.ExecuteNonQuery();
                                    }
                                }
                            }

                            // 4.3 Save Respondent & Corp Evidence (if any)
                            if (model.RespondentEvidence != null)
                            {
                                foreach (var ev in model.RespondentEvidence)
                                {
                                    if (!string.IsNullOrWhiteSpace(ev.Name))
                                    {
                                        var evCmd = new SqlCommand("INSERT INTO MVC_REMIND_BACK_ADVERSE_PW (RemindBackID, PWName, Type, Remark) VALUES (@RemindBackID, @Name, 'TP_Respondent', @Remark)", conn, trans);
                                        evCmd.Parameters.AddWithValue("@RemindBackID", rbId);
                                        evCmd.Parameters.AddWithValue("@Name", ev.Name);
                                        evCmd.Parameters.AddWithValue("@Remark", ev.Remark ?? (object)DBNull.Value);
                                        evCmd.ExecuteNonQuery();
                                    }
                                }
                            }
                            if (model.CorpEvidence != null)
                            {
                                foreach (var ev in model.CorpEvidence)
                                {
                                    if (!string.IsNullOrWhiteSpace(ev.Name))
                                    {
                                        var evCmd = new SqlCommand("INSERT INTO MVC_REMIND_BACK_ADVERSE_PW (RemindBackID, PWName, Type, Designation) VALUES (@RemindBackID, @Name, 'TP_Corporation', @Designation)", conn, trans);
                                        evCmd.Parameters.AddWithValue("@RemindBackID", rbId);
                                        evCmd.Parameters.AddWithValue("@Name", ev.Name);
                                        evCmd.Parameters.AddWithValue("@Designation", ev.Designation ?? (object)DBNull.Value);
                                        evCmd.ExecuteNonQuery();
                                    }
                                }
                            }
                        }

                        trans.Commit();
                        return rbId;
                    }
                    catch (Exception)
                    {
                        trans.Rollback();
                        throw;
                    }
                }
            }
        }
        public bool TransferCase(int caseId, int toDivisionId, string remarks)
        {
            try
            {
                using (var conn = new SqlConnection(_db.GetConnectionString()))
                {
                    conn.Open();
                    string query = @"
                        UPDATE MVC_CASES 
                        SET TransferredFromDivisionID = DivisionID,
                            DivisionID = @ToDivisionID,
                            TransferDate = GETDATE(),
                            TransferRemarks = @Remarks,
                            IsTransferViewed = 0
                        WHERE CaseID = @CaseID";
                    
                    using (var cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@CaseID", caseId);
                        cmd.Parameters.AddWithValue("@ToDivisionID", toDivisionId);
                        cmd.Parameters.AddWithValue("@Remarks", remarks ?? (object)DBNull.Value);
                        return cmd.ExecuteNonQuery() > 0;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"TransferCase Error: {ex.Message}");
                return false;
            }
        }

        public IEnumerable<MVCCaseViewModel> GetRecentTransfers(int divisionId)
        {
            var transfers = new List<MVCCaseViewModel>();
            try
            {
                using (var conn = new SqlConnection(_db.GetConnectionString()))
                {
                    conn.Open();
                    string query = @"
                        SELECT c.*, d.DivisionNameEnglish as TransferredFromDivisionName
                        FROM MVC_CASES c
                        JOIN DIVISION_MASTER d ON c.TransferredFromDivisionID = d.DivisionID
                        WHERE c.DivisionID = @DivisionID 
                          AND c.TransferredFromDivisionID IS NOT NULL 
                          AND c.IsTransferViewed = 0
                        ORDER BY c.TransferDate DESC";
                    
                    using (var cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@DivisionID", divisionId);
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                transfers.Add(new MVCCaseViewModel
                                {
                                    CaseID = Convert.ToInt32(reader["CaseID"]),
                                    MVCNo = reader["MVCNo"].ToString() ?? "",
                                    MVCYear = Convert.ToInt32(reader["MVCYear"]),
                                    TransferredFromDivisionName = reader["TransferredFromDivisionName"].ToString(),
                                    TransferDate = Convert.ToDateTime(reader["TransferDate"]),
                                    IsTransferViewed = Convert.ToBoolean(reader["IsTransferViewed"])
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"GetRecentTransfers Error: {ex.Message}");
            }
            return transfers;
        }

        public void MarkTransferAsViewed(int caseId)
        {
            try
            {
                using (var conn = new SqlConnection(_db.GetConnectionString()))
                {
                    conn.Open();
                    string query = "UPDATE MVC_CASES SET IsTransferViewed = 1 WHERE CaseID = @CaseID";
                    using (var cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@CaseID", caseId);
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"MarkTransferAsViewed Error: {ex.Message}");
            }
        }

        public List<MVCCaseViewModel> GetAllRemindBackCases(int divisionId = 0, int page = 1, int pageSize = 10, string? search = null)
        {
            var cases = new List<MVCCaseViewModel>();
            try
            {
                int offset = (page - 1) * pageSize;
                string query = @"
                    SELECT 
                        rb.RemindBackID, rb.OriginalCaseID, rb.DivisionID, rb.MVCNo, rb.MVCYear, rb.MACTID,
                        rb.VehicleNo, rb.VehicleType, rb.AccidentDate, rb.CaseType, rb.ClaimType,
                        rb.ThirdPartyFlag, rb.ClaimAmount, rb.AdvocateID, rb.AdvocateName,
                        rb.EntrustmentNo, rb.EntrustmentDate, rb.DoubleClaimFlag,
                        rb.NextHearingDate, rb.CurrentStage, rb.DisposalStatus, rb.DisposalResult,
                        rb.DisposalRemarks, rb.ClosureDate, rb.CreatedAt,
                        rb.CNRNumber, rb.EstCode, rb.CaseTypeCode,
                        d.DivisionNameEnglish as DivisionName,
                        m.MACTName,
                        COALESCE(radv.IsAllegedAccident, 0) as IsAllegedAccident
                    FROM MVC_REMIND_BACK_CASES rb
                    LEFT JOIN DIVISION_MASTER d ON rb.DivisionID = d.DivisionID
                    LEFT JOIN MACT_MASTER m ON rb.MACTID = m.MACTID
                    LEFT JOIN MVC_REMIND_BACK_ADVERSE_DETAILS radv ON rb.RemindBackID = radv.RemindBackID
                    WHERE 1=1";

                var paramsList = new List<SqlParameter>();
                if (divisionId > 0 && divisionId != 5)
                {
                    query += " AND rb.DivisionID = @DivisionID";
                    paramsList.Add(new SqlParameter("@DivisionID", divisionId));
                }

                if (!string.IsNullOrWhiteSpace(search))
                {
                    query += @" AND (
                        rb.MVCNo LIKE @Search OR 
                        rb.VehicleNo LIKE @Search OR 
                        CAST(rb.MVCYear AS NVARCHAR) LIKE @Search OR 
                        rb.CNRNumber LIKE @Search OR
                        m.MACTName LIKE @Search OR
                        rb.AdvocateName LIKE @Search OR
                        EXISTS (
                            SELECT 1 FROM MVC_REMIND_BACK_PETITIONERS p 
                            WHERE p.RemindBackID = rb.RemindBackID AND p.PetitionerName LIKE @Search
                        )
                    )";
                    paramsList.Add(new SqlParameter("@Search", "%" + search.Trim() + "%"));
                }

                query += " ORDER BY rb.CreatedAt DESC, rb.RemindBackID DESC OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY";
                paramsList.Add(new SqlParameter("@Offset", offset));
                paramsList.Add(new SqlParameter("@PageSize", pageSize));

                DataTable dt = _db.ExecuteQuery(query, paramsList.ToArray());
                foreach (DataRow row in dt.Rows)
                {
                    int rbId = Convert.ToInt32(row["RemindBackID"]);
                    var c = new MVCCaseViewModel
                    {
                        RemindBackID = rbId,
                        CaseID = row["OriginalCaseID"] != DBNull.Value ? Convert.ToInt32(row["OriginalCaseID"]) : rbId,
                        OriginalCaseID = row["OriginalCaseID"] != DBNull.Value ? Convert.ToInt32(row["OriginalCaseID"]) : null,
                        DivisionID = row["DivisionID"] != DBNull.Value ? Convert.ToInt32(row["DivisionID"]) : 0,
                        DivisionName = row["DivisionName"]?.ToString() ?? "",
                        MACTID = row["MACTID"] != DBNull.Value ? Convert.ToInt32(row["MACTID"]) : 0,
                        MACTName = row["MACTName"]?.ToString() ?? "",
                        MVCNo = row["MVCNo"]?.ToString() ?? "",
                        MVCYear = row["MVCYear"] != DBNull.Value ? Convert.ToInt32(row["MVCYear"]) : 0,
                        VehicleNo = row["VehicleNo"]?.ToString(),
                        VehicleType = row["VehicleType"]?.ToString() ?? "Corporation",
                        AccidentDate = row["AccidentDate"] != DBNull.Value ? Convert.ToDateTime(row["AccidentDate"]) : null,
                        CaseType = row["CaseType"]?.ToString() ?? "RemindBack",
                        ClaimType = row["ClaimType"]?.ToString(),
                        ThirdPartyFlag = row["ThirdPartyFlag"] != DBNull.Value && Convert.ToBoolean(row["ThirdPartyFlag"]),
                        ClaimAmount = row["ClaimAmount"] != DBNull.Value ? Convert.ToDecimal(row["ClaimAmount"]) : null,
                        AdvocateID = row["AdvocateID"] != DBNull.Value ? Convert.ToInt32(row["AdvocateID"]) : null,
                        AdvocateName = row["AdvocateName"]?.ToString(),
                        EntrustmentNo = row["EntrustmentNo"]?.ToString(),
                        EntrustmentDate = row["EntrustmentDate"] != DBNull.Value ? Convert.ToDateTime(row["EntrustmentDate"]) : null,
                        DoubleClaimFlag = row["DoubleClaimFlag"] != DBNull.Value && Convert.ToBoolean(row["DoubleClaimFlag"]),
                        NextHearingDate = row["NextHearingDate"] != DBNull.Value ? Convert.ToDateTime(row["NextHearingDate"]) : null,
                        CurrentStage = row["CurrentStage"]?.ToString() ?? "Pending",
                        DisposalStatus = row["DisposalStatus"]?.ToString(),
                        DisposalResult = row["DisposalResult"]?.ToString(),
                        DisposalRemarks = row["DisposalRemarks"]?.ToString(),
                        ClosureDate = row["ClosureDate"] != DBNull.Value ? Convert.ToDateTime(row["ClosureDate"]) : null,
                        CreatedAt = row["CreatedAt"] != DBNull.Value ? Convert.ToDateTime(row["CreatedAt"]) : DateTime.MinValue,
                        CNRNumber = row.Table.Columns.Contains("CNRNumber") && row["CNRNumber"] != DBNull.Value ? row["CNRNumber"].ToString() : null,
                        EstCode = row.Table.Columns.Contains("EstCode") && row["EstCode"] != DBNull.Value ? row["EstCode"].ToString() : null,
                        CaseTypeCode = row.Table.Columns.Contains("CaseTypeCode") && row["CaseTypeCode"] != DBNull.Value ? row["CaseTypeCode"].ToString() : null,
                        IsAllegedAccident = row.Table.Columns.Contains("IsAllegedAccident") && row["IsAllegedAccident"] != DBNull.Value && Convert.ToBoolean(row["IsAllegedAccident"])
                    };

                    // Load Petitioners
                    string petQuery = "SELECT PetitionerName, Relationship FROM MVC_REMIND_BACK_PETITIONERS WHERE RemindBackID = @RemindBackID";
                    DataTable petDt = _db.ExecuteQuery(petQuery, new[] { new SqlParameter("@RemindBackID", rbId) });
                    foreach (DataRow petRow in petDt.Rows)
                    {
                        c.Petitioners.Add(new PetitionerViewModel
                        {
                            PetitionerName = petRow["PetitionerName"]?.ToString() ?? "",
                            Relationship = petRow["Relationship"]?.ToString()
                        });
                    }

                    cases.Add(c);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"GetAllRemindBackCases Error: {ex.Message}");
            }
            return cases;
        }

        public int GetTotalRemindBackCaseCount(int divisionId = 0, string? search = null)
        {
            try
            {
                string query = @"
                    SELECT COUNT(*)
                    FROM MVC_REMIND_BACK_CASES rb
                    LEFT JOIN MACT_MASTER m ON rb.MACTID = m.MACTID
                    WHERE 1=1";

                var paramsList = new List<SqlParameter>();
                if (divisionId > 0 && divisionId != 5)
                {
                    query += " AND rb.DivisionID = @DivisionID";
                    paramsList.Add(new SqlParameter("@DivisionID", divisionId));
                }

                if (!string.IsNullOrWhiteSpace(search))
                {
                    query += @" AND (
                        rb.MVCNo LIKE @Search OR 
                        rb.VehicleNo LIKE @Search OR 
                        CAST(rb.MVCYear AS NVARCHAR) LIKE @Search OR 
                        rb.CNRNumber LIKE @Search OR
                        m.MACTName LIKE @Search OR
                        rb.AdvocateName LIKE @Search OR
                        EXISTS (
                            SELECT 1 FROM MVC_REMIND_BACK_PETITIONERS p 
                            WHERE p.RemindBackID = rb.RemindBackID AND p.PetitionerName LIKE @Search
                        )
                    )";
                    paramsList.Add(new SqlParameter("@Search", "%" + search.Trim() + "%"));
                }

                object result = _db.ExecuteScalar(query, paramsList.ToArray()) ?? 0;
                return Convert.ToInt32(result);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"GetTotalRemindBackCaseCount Error: {ex.Message}");
                return 0;
            }
        }

        public MVCCaseViewModel? GetRemindBackCaseById(int remindBackId)
        {
            try
            {
                string query = @"
                    SELECT 
                        rb.*,
                        d.DivisionNameEnglish as DivisionName,
                        m.MACTName,
                        COALESCE(rb.AdvocateName, a.AdvocateName) as FinalAdvocateName
                    FROM MVC_REMIND_BACK_CASES rb
                    LEFT JOIN DIVISION_MASTER d ON rb.DivisionID = d.DivisionID
                    LEFT JOIN MACT_MASTER m ON rb.MACTID = m.MACTID
                    LEFT JOIN ADVOCATE_MASTER a ON rb.AdvocateID = a.AdvocateID
                    WHERE rb.RemindBackID = @RemindBackID";

                DataTable dt = _db.ExecuteQuery(query, new[] { new SqlParameter("@RemindBackID", remindBackId) });
                if (dt.Rows.Count == 0) return null;

                DataRow row = dt.Rows[0];
                var model = new MVCCaseViewModel
                {
                    RemindBackID = remindBackId,
                    CaseID = row["OriginalCaseID"] != DBNull.Value ? Convert.ToInt32(row["OriginalCaseID"]) : remindBackId,
                    OriginalCaseID = row["OriginalCaseID"] != DBNull.Value ? Convert.ToInt32(row["OriginalCaseID"]) : null,
                    DivisionID = row["DivisionID"] != DBNull.Value ? Convert.ToInt32(row["DivisionID"]) : 0,
                    DivisionName = row["DivisionName"]?.ToString() ?? "",
                    MVCNo = row["MVCNo"]?.ToString() ?? "",
                    MVCYear = row["MVCYear"] != DBNull.Value ? Convert.ToInt32(row["MVCYear"]) : 0,
                    MACTID = row["MACTID"] != DBNull.Value ? Convert.ToInt32(row["MACTID"]) : 0,
                    MACTName = row["MACTName"]?.ToString() ?? "",
                    VehicleNo = row["VehicleNo"]?.ToString(),
                    VehicleType = row["VehicleType"]?.ToString() ?? "Corporation",
                    AccidentDate = row["AccidentDate"] != DBNull.Value ? Convert.ToDateTime(row["AccidentDate"]) : null,
                    CaseType = row["CaseType"]?.ToString() ?? "RemindBack",
                    StatusID = row["StatusID"] != DBNull.Value ? Convert.ToInt32(row["StatusID"]) : 1,
                    ClaimType = row["ClaimType"]?.ToString(),
                    ThirdPartyFlag = row["ThirdPartyFlag"] != DBNull.Value && Convert.ToBoolean(row["ThirdPartyFlag"]),
                    ClaimAmount = row["ClaimAmount"] != DBNull.Value ? Convert.ToDecimal(row["ClaimAmount"]) : null,
                    AdvocateID = row["AdvocateID"] != DBNull.Value ? Convert.ToInt32(row["AdvocateID"]) : null,
                    AdvocateName = row["FinalAdvocateName"]?.ToString() ?? row["AdvocateName"]?.ToString(),
                    EntrustmentNo = row["EntrustmentNo"]?.ToString(),
                    EntrustmentDate = row["EntrustmentDate"] != DBNull.Value ? Convert.ToDateTime(row["EntrustmentDate"]) : null,
                    DoubleClaimFlag = row["DoubleClaimFlag"] != DBNull.Value && Convert.ToBoolean(row["DoubleClaimFlag"]),
                    CNRNumber = row.Table.Columns.Contains("CNRNumber") && row["CNRNumber"] != DBNull.Value ? row["CNRNumber"].ToString() : null,
                    EstCode = row.Table.Columns.Contains("EstCode") && row["EstCode"] != DBNull.Value ? row["EstCode"].ToString() : null,
                    CaseTypeCode = row.Table.Columns.Contains("CaseTypeCode") && row["CaseTypeCode"] != DBNull.Value ? row["CaseTypeCode"].ToString() : null,
                    NextHearingDate = row["NextHearingDate"] != DBNull.Value ? Convert.ToDateTime(row["NextHearingDate"]) : null,
                    CurrentStage = row["CurrentStage"]?.ToString() ?? "Pending",
                    DisposalStatus = row["DisposalStatus"]?.ToString(),
                    DisposalResult = row["DisposalResult"]?.ToString(),
                    DisposalRemarks = row["DisposalRemarks"]?.ToString(),
                    IsDocumentSent = row["IsDocumentSent"] != DBNull.Value && Convert.ToBoolean(row["IsDocumentSent"]),
                    DocumentOutwardNo = row["DocumentOutwardNo"]?.ToString(),
                    DocumentOutwardDate = row["DocumentOutwardDate"] != DBNull.Value ? Convert.ToDateTime(row["DocumentOutwardDate"]) : null,
                    IsObjectionFiled = row["IsObjectionFiled"] != DBNull.Value && Convert.ToBoolean(row["IsObjectionFiled"]),
                    ObjectionFiledDate = row["ObjectionFiledDate"] != DBNull.Value ? Convert.ToDateTime(row["ObjectionFiledDate"]) : null,
                    ObjectionOutwardNo = row["ObjectionOutwardNo"]?.ToString(),
                    ClosureDate = row["ClosureDate"] != DBNull.Value ? Convert.ToDateTime(row["ClosureDate"]) : null,
                    IsEvidenceFiled = row["IsEvidenceFiled"] != DBNull.Value && Convert.ToBoolean(row["IsEvidenceFiled"]),
                    CreatedAt = row["CreatedAt"] != DBNull.Value ? Convert.ToDateTime(row["CreatedAt"]) : DateTime.MinValue
                };

                // Load Petitioners
                string petQuery = "SELECT PetitionerName, Relationship FROM MVC_REMIND_BACK_PETITIONERS WHERE RemindBackID = @RemindBackID";
                DataTable petDt = _db.ExecuteQuery(petQuery, new[] { new SqlParameter("@RemindBackID", remindBackId) });
                foreach (DataRow petRow in petDt.Rows)
                {
                    model.Petitioners.Add(new PetitionerViewModel
                    {
                        PetitionerName = petRow["PetitionerName"]?.ToString() ?? "",
                        Relationship = petRow["Relationship"]?.ToString()
                    });
                }

                // Load Connected Cases
                string connQuery = "SELECT ConnectedMVCNo, ConnectedYear, Remarks, MACT, IsDoubleClaim FROM MVC_REMIND_BACK_CONNECTED WHERE RemindBackID = @RemindBackID";
                DataTable connDt = _db.ExecuteQuery(connQuery, new[] { new SqlParameter("@RemindBackID", remindBackId) });
                foreach (DataRow connRow in connDt.Rows)
                {
                    model.ConnectedCases.Add(new ConnectedCaseViewModel
                    {
                        ConnectedMVCNo = connRow["ConnectedMVCNo"]?.ToString() ?? "",
                        ConnectedYear = connRow["ConnectedYear"] != DBNull.Value ? Convert.ToInt32(connRow["ConnectedYear"]) : 0,
                        Remarks = connRow["Remarks"]?.ToString(),
                        MACT = connRow["MACT"]?.ToString(),
                        IsDoubleClaim = connRow["IsDoubleClaim"] != DBNull.Value && Convert.ToBoolean(connRow["IsDoubleClaim"])
                    });
                }

                // Load Respondents
                try
                {
                    string respQuery = "SELECT RespondentName, Remarks FROM MVC_REMIND_BACK_RESPONDENTS WHERE RemindBackID = @RemindBackID";
                    DataTable respDt = _db.ExecuteQuery(respQuery, new[] { new SqlParameter("@RemindBackID", remindBackId) });
                    foreach (DataRow respRow in respDt.Rows)
                    {
                        model.Respondents.Add(new RespondentViewModel
                        {
                            RespondentName = respRow["RespondentName"]?.ToString() ?? "",
                            Remarks = respRow["Remarks"]?.ToString()
                        });
                    }
                }
                catch { }

                // If Adverse details exist
                string advQuery = "SELECT * FROM MVC_REMIND_BACK_ADVERSE_DETAILS WHERE RemindBackID = @RemindBackID";
                DataTable advDt = _db.ExecuteQuery(advQuery, new[] { new SqlParameter("@RemindBackID", remindBackId) });
                if (advDt.Rows.Count > 0)
                {
                    DataRow advRow = advDt.Rows[0];
                    model.IsAllegedAccident = advRow.Table.Columns.Contains("IsAllegedAccident") && advRow["IsAllegedAccident"] != DBNull.Value && Convert.ToBoolean(advRow["IsAllegedAccident"]);
                    model.AdverseAward = new AdverseAwardViewModel
                    {
                        // 1. Adverse Basic & Insurance
                        IsBusInsured = advRow.Table.Columns.Contains("IsBusInsured") && advRow["IsBusInsured"] != DBNull.Value && Convert.ToBoolean(advRow["IsBusInsured"]),
                        BusInsuranceDetails = advRow.Table.Columns.Contains("BusInsuranceDetails") ? advRow["BusInsuranceDetails"]?.ToString() : null,
                        ClaimPetitionDate = advRow.Table.Columns.Contains("ClaimPetitionDate") && advRow["ClaimPetitionDate"] != DBNull.Value ? Convert.ToDateTime(advRow["ClaimPetitionDate"]) : null,
                        AwardDate = advRow["AwardDate"] != DBNull.Value ? Convert.ToDateTime(advRow["AwardDate"]) : null,
                        TreatedDocFlag = advRow.Table.Columns.Contains("TreatedDocFlag") && advRow["TreatedDocFlag"] != DBNull.Value && Convert.ToBoolean(advRow["TreatedDocFlag"]),
                        MannerOfAccident = advRow.Table.Columns.Contains("MannerOfAccident") ? advRow["MannerOfAccident"]?.ToString() : null,
                        MannerOfAccidentRO = advRow.Table.Columns.Contains("MannerOfAccidentRO") ? advRow["MannerOfAccidentRO"]?.ToString() : null,
                        ObjectionFiled = advRow.Table.Columns.Contains("ObjectionFiled") && advRow["ObjectionFiled"] != DBNull.Value && Convert.ToBoolean(advRow["ObjectionFiled"]),
                        ObjectionRemarks = advRow.Table.Columns.Contains("ObjectionRemarks") ? advRow["ObjectionRemarks"]?.ToString() : null,
                        RWType = advRow.Table.Columns.Contains("RWType") ? advRow["RWType"]?.ToString() : null,
                        TR18Remarks = advRow.Table.Columns.Contains("TR18Remarks") ? advRow["TR18Remarks"]?.ToString() : null,
                        IsDeceasedInTR18 = advRow.Table.Columns.Contains("IsDeceasedInTR18") && advRow["IsDeceasedInTR18"] != DBNull.Value && Convert.ToBoolean(advRow["IsDeceasedInTR18"]),
                        TR18UploadPath = advRow["TR18UploadPath"]?.ToString(),
                        IsAllegedAccident = advRow.Table.Columns.Contains("IsAllegedAccident") && advRow["IsAllegedAccident"] != DBNull.Value && Convert.ToBoolean(advRow["IsAllegedAccident"]),
                        DoubleClaimFlag = advRow.Table.Columns.Contains("AdverseDoubleClaimFlag") && advRow["AdverseDoubleClaimFlag"] != DBNull.Value && Convert.ToBoolean(advRow["AdverseDoubleClaimFlag"]),
                        DoubleClaimDetails = advRow.Table.Columns.Contains("DoubleClaimDetails") ? advRow["DoubleClaimDetails"]?.ToString() : null,
                        SecurityRequired = advRow.Table.Columns.Contains("SecurityRequired") && advRow["SecurityRequired"] != DBNull.Value && Convert.ToBoolean(advRow["SecurityRequired"]),
                        SecurityUploadPath = advRow["SecurityUploadPath"]?.ToString(),
                        GovIDProofUploadPath = advRow["GovIDProofUploadPath"]?.ToString(),
                        DisposedOnDate = advRow["DisposedOnDate"] != DBNull.Value ? Convert.ToDateTime(advRow["DisposedOnDate"]) : null,
                        CopyAppliedDate = advRow.Table.Columns.Contains("CopyAppliedDate") && advRow["CopyAppliedDate"] != DBNull.Value ? Convert.ToDateTime(advRow["CopyAppliedDate"]) : null,
                        CertifiedCopyRemarks = advRow.Table.Columns.Contains("CertifiedCopyRemarks") ? advRow["CertifiedCopyRemarks"]?.ToString() : null,
                        CopyIssuedDate = advRow.Table.Columns.Contains("CopyIssuedDate") && advRow["CopyIssuedDate"] != DBNull.Value ? Convert.ToDateTime(advRow["CopyIssuedDate"]) : null,
                        CopyReceivedDate = advRow.Table.Columns.Contains("CopyReceivedDate") && advRow["CopyReceivedDate"] != DBNull.Value ? Convert.ToDateTime(advRow["CopyReceivedDate"]) : null,
                        CopyDeliveredDate = advRow.Table.Columns.Contains("CopyDeliveredDate") && advRow["CopyDeliveredDate"] != DBNull.Value ? Convert.ToDateTime(advRow["CopyDeliveredDate"]) : null,
                        AwardAmount = advRow["AwardAmount"] != DBNull.Value ? Convert.ToDecimal(advRow["AwardAmount"]) : null,
                        InterestRate = advRow["InterestRate"] != DBNull.Value ? Convert.ToDecimal(advRow["InterestRate"]) : null,
                        VictimAge = advRow.Table.Columns.Contains("VictimAge") && advRow["VictimAge"] != DBNull.Value ? (int?)Convert.ToInt32(advRow["VictimAge"]) : null,
                        Occupation = advRow.Table.Columns.Contains("Occupation") ? advRow["Occupation"]?.ToString() : null,
                        IncomeConsidered = advRow.Table.Columns.Contains("IncomeConsidered") && advRow["IncomeConsidered"] != DBNull.Value ? Convert.ToDecimal(advRow["IncomeConsidered"]) : null,
                        IncomePeriod = advRow.Table.Columns.Contains("IncomePeriod") ? (advRow["IncomePeriod"]?.ToString() ?? "monthly") : "monthly",
                        InjuryType = advRow.Table.Columns.Contains("InjuryType") ? advRow["InjuryType"]?.ToString() : null,
                        InjuryDetails = advRow.Table.Columns.Contains("InjuryDetails") ? advRow["InjuryDetails"]?.ToString() : null,
                        DriverPunishmentStatus = advRow.Table.Columns.Contains("DriverPunishmentStatus") ? advRow["DriverPunishmentStatus"]?.ToString() : null,
                        PunishmentOrderUploadPath = advRow["PunishmentOrderUploadPath"]?.ToString(),
                        PunishmentRemarks = advRow.Table.Columns.Contains("PunishmentRemarks") ? advRow["PunishmentRemarks"]?.ToString() : null,
                        DisabilityPercentage = advRow.Table.Columns.Contains("DisabilityPercentage") && advRow["DisabilityPercentage"] != DBNull.Value ? (decimal?)advRow["DisabilityPercentage"] : null,
                        InterimCompAmount = advRow.Table.Columns.Contains("InterimCompAmount") && advRow["InterimCompAmount"] != DBNull.Value ? Convert.ToDecimal(advRow["InterimCompAmount"]) : null,
                        InterimCompDeducted = advRow.Table.Columns.Contains("InterimCompDeducted") && advRow["InterimCompDeducted"] != DBNull.Value && Convert.ToBoolean(advRow["InterimCompDeducted"]),
                        AdvocateOpinion = advRow.Table.Columns.Contains("AdvocateOpinion") ? advRow["AdvocateOpinion"]?.ToString() : null,
                        LOOpinion = advRow.Table.Columns.Contains("LOOpinion") ? advRow["LOOpinion"]?.ToString() : null,
                        DCOpinion = advRow.Table.Columns.Contains("DCOpinion") ? advRow["DCOpinion"]?.ToString() : null,
                        ForwardingStatus = advRow.Table.Columns.Contains("ForwardingStatus") ? advRow["ForwardingStatus"]?.ToString() : null,
                        ClosureRemarks = advRow.Table.Columns.Contains("ClosureRemarks") ? advRow["ClosureRemarks"]?.ToString() : null,
                        ClosureDate = advRow.Table.Columns.Contains("ClosureDate") && advRow["ClosureDate"] != DBNull.Value ? Convert.ToDateTime(advRow["ClosureDate"]) : null,
                        OutwardNumber = advRow.Table.Columns.Contains("OutwardNumber") ? advRow["OutwardNumber"]?.ToString() : null,
                        OutwardDate = advRow.Table.Columns.Contains("OutwardDate") && advRow["OutwardDate"] != DBNull.Value ? Convert.ToDateTime(advRow["OutwardDate"]) : null,
                        IsCorpLiable = advRow.Table.Columns.Contains("IsCorpLiable") && advRow["IsCorpLiable"] != DBNull.Value && Convert.ToBoolean(advRow["IsCorpLiable"]),
                        LiabilityPercentage = advRow.Table.Columns.Contains("LiabilityPercentage") && advRow["LiabilityPercentage"] != DBNull.Value ? Convert.ToDecimal(advRow["LiabilityPercentage"]) : null,
                        LiabilityRemarks = advRow.Table.Columns.Contains("LiabilityRemarks") ? advRow["LiabilityRemarks"]?.ToString() : null,
                        LiableAmount = (advRow["AwardAmount"] != DBNull.Value && advRow["LiabilityPercentage"] != DBNull.Value) ? 
                            (Convert.ToDecimal(advRow["AwardAmount"]) * Convert.ToDecimal(advRow["LiabilityPercentage"]) / 100) : null,
                        AdverseJudgmentUploadPath = advRow.Table.Columns.Contains("AdverseJudgmentUploadPath") ? advRow["AdverseJudgmentUploadPath"]?.ToString() : null,
                        FutureProspectus = advRow.Table.Columns.Contains("FutureProspectus") ? advRow["FutureProspectus"]?.ToString() : null,
                        IsDelayApplicationFiled = advRow.Table.Columns.Contains("IsDelayApplicationFiled") && advRow["IsDelayApplicationFiled"] != DBNull.Value && Convert.ToBoolean(advRow["IsDelayApplicationFiled"]),
                        DelayApplicationPath = advRow["DelayApplicationPath"]?.ToString(),
                        IsDelayCondonedAdverse = advRow.Table.Columns.Contains("IsDelayCondonedAdverse") && advRow["IsDelayCondonedAdverse"] != DBNull.Value ? (bool?)advRow["IsDelayCondonedAdverse"] : null,
                        DelayCondonedOrderPath = advRow["DelayCondonedOrderPath"]?.ToString(),
                        IsMedicalInsuranceClaimed = advRow.Table.Columns.Contains("IsMedicalInsuranceClaimed") && advRow["IsMedicalInsuranceClaimed"] != DBNull.Value && Convert.ToBoolean(advRow["IsMedicalInsuranceClaimed"]),
                        IsFIRFiled = advRow.Table.Columns.Contains("IsFIRFiled") && advRow["IsFIRFiled"] != DBNull.Value ? (bool?)advRow["IsFIRFiled"] : null,
                        IsChargeSheetFiled = advRow.Table.Columns.Contains("IsChargeSheetFiled") && advRow["IsChargeSheetFiled"] != DBNull.Value ? (bool?)advRow["IsChargeSheetFiled"] : null,
                        IsBusCameraInstalled = advRow.Table.Columns.Contains("IsBusCameraInstalled") && advRow["IsBusCameraInstalled"] != DBNull.Value ? (bool?)advRow["IsBusCameraInstalled"] : null,
                        IsCameraFootageProduced = advRow.Table.Columns.Contains("IsCameraFootageProduced") && advRow["IsCameraFootageProduced"] != DBNull.Value ? (bool?)advRow["IsCameraFootageProduced"] : null,
                        IsPhotographProduced = advRow.Table.Columns.Contains("IsPhotographProduced") && advRow["IsPhotographProduced"] != DBNull.Value ? (bool?)advRow["IsPhotographProduced"] : null,
                        PhotographNotProducedReason = advRow.Table.Columns.Contains("PhotographNotProducedReason") ? advRow["PhotographNotProducedReason"]?.ToString() : null,
                        PoliceSketchExhibitNo = advRow.Table.Columns.Contains("PoliceSketchExhibitNo") ? advRow["PoliceSketchExhibitNo"]?.ToString() : null,
                        IsPoliceSketchEnclosed = advRow.Table.Columns.Contains("IsPoliceSketchEnclosed") && advRow["IsPoliceSketchEnclosed"] != DBNull.Value ? (bool?)advRow["IsPoliceSketchEnclosed"] : null,
                        IsEvidenceBasedOnSecurityReport = advRow.Table.Columns.Contains("IsEvidenceBasedOnSecurityReport") && advRow["IsEvidenceBasedOnSecurityReport"] != DBNull.Value ? (bool?)advRow["IsEvidenceBasedOnSecurityReport"] : null,
                        SecurityReportNoEvidenceReason = advRow.Table.Columns.Contains("SecurityReportNoEvidenceReason") ? advRow["SecurityReportNoEvidenceReason"]?.ToString() : null,
                        IsImpleadingAppFiled = advRow.Table.Columns.Contains("IsImpleadingAppFiled") && advRow["IsImpleadingAppFiled"] != DBNull.Value ? (bool?)advRow["IsImpleadingAppFiled"] : null,
                        ImpleadingAppNotFiledReason = advRow.Table.Columns.Contains("ImpleadingAppNotFiledReason") ? advRow["ImpleadingAppNotFiledReason"]?.ToString() : null,
                        IsVictimSalaried = advRow.Table.Columns.Contains("IsVictimSalaried") && advRow["IsVictimSalaried"] != DBNull.Value ? (bool?)advRow["IsVictimSalaried"] : null,
                        IsIncomeCrossVerified = advRow.Table.Columns.Contains("IsIncomeCrossVerified") && advRow["IsIncomeCrossVerified"] != DBNull.Value ? (bool?)advRow["IsIncomeCrossVerified"] : null,
                        DoesIncomeTallyWithDocuments = advRow.Table.Columns.Contains("DoesIncomeTallyWithDocuments") && advRow["DoesIncomeTallyWithDocuments"] != DBNull.Value ? (bool?)advRow["DoesIncomeTallyWithDocuments"] : null,
                        IsMedicalBillsVerified = advRow.Table.Columns.Contains("IsMedicalBillsVerified") && advRow["IsMedicalBillsVerified"] != DBNull.Value ? (bool?)advRow["IsMedicalBillsVerified"] : null,
                        IsAmountDepositedInEP = advRow.Table.Columns.Contains("IsAmountDepositedInEP") && advRow["IsAmountDepositedInEP"] != DBNull.Value && Convert.ToBoolean(advRow["IsAmountDepositedInEP"]),
                        IsEPFiled = advRow.Table.Columns.Contains("IsEPFiled") && advRow["IsEPFiled"] != DBNull.Value && Convert.ToBoolean(advRow["IsEPFiled"]),
                        EPDepositedAmount = advRow.Table.Columns.Contains("EPDepositedAmount") && advRow["EPDepositedAmount"] != DBNull.Value ? (decimal?)advRow["EPDepositedAmount"] : null,
                        FutureProspectsPercentage = advRow.Table.Columns.Contains("FutureProspectsPercentage") && advRow["FutureProspectsPercentage"] != DBNull.Value ? (decimal?)advRow["FutureProspectsPercentage"] : null,
                        PersonalExpensesDeduction = advRow.Table.Columns.Contains("PersonalExpensesDeduction") && advRow["PersonalExpensesDeduction"] != DBNull.Value ? (decimal?)advRow["PersonalExpensesDeduction"] : null,
                        Multiplier = advRow.Table.Columns.Contains("Multiplier") && advRow["Multiplier"] != DBNull.Value ? (decimal?)advRow["Multiplier"] : null,
                        LossOfDependency = advRow.Table.Columns.Contains("LossOfDependency") && advRow["LossOfDependency"] != DBNull.Value ? (decimal?)advRow["LossOfDependency"] : null,
                        LossOfConsortium = advRow.Table.Columns.Contains("LossOfConsortium") && advRow["LossOfConsortium"] != DBNull.Value ? (decimal?)advRow["LossOfConsortium"] : null,
                        LossOfEstate = advRow.Table.Columns.Contains("LossOfEstate") && advRow["LossOfEstate"] != DBNull.Value ? (decimal?)advRow["LossOfEstate"] : null,
                        FuneralExpenses = advRow.Table.Columns.Contains("FuneralExpenses") && advRow["FuneralExpenses"] != DBNull.Value ? (decimal?)advRow["FuneralExpenses"] : null,
                        LossOfLoveAffection = advRow.Table.Columns.Contains("LossOfLoveAffection") && advRow["LossOfLoveAffection"] != DBNull.Value ? (decimal?)advRow["LossOfLoveAffection"] : null,
                        MedicalExpenseOther = advRow.Table.Columns.Contains("MedicalExpenseOther") && advRow["MedicalExpenseOther"] != DBNull.Value ? (decimal?)advRow["MedicalExpenseOther"] : null,
                        AgeProofUploadPath = advRow.Table.Columns.Contains("AgeProofUploadPath") ? advRow["AgeProofUploadPath"]?.ToString() : null,
                        PainSufferings = advRow.Table.Columns.Contains("PainSufferings") && advRow["PainSufferings"] != DBNull.Value ? (decimal?)advRow["PainSufferings"] : null,
                        ConveyanceAttendant = advRow.Table.Columns.Contains("ConveyanceAttendant") && advRow["ConveyanceAttendant"] != DBNull.Value ? (decimal?)advRow["ConveyanceAttendant"] : null,
                        LossOfFutureIncome = advRow.Table.Columns.Contains("LossOfFutureIncome") && advRow["LossOfFutureIncome"] != DBNull.Value ? (decimal?)advRow["LossOfFutureIncome"] : null,
                        LossOfIncomeLaidUp = advRow.Table.Columns.Contains("LossOfIncomeLaidUp") && advRow["LossOfIncomeLaidUp"] != DBNull.Value ? (decimal?)advRow["LossOfIncomeLaidUp"] : null,
                        LossOfAmenities = advRow.Table.Columns.Contains("LossOfAmenities") && advRow["LossOfAmenities"] != DBNull.Value ? (decimal?)advRow["LossOfAmenities"] : null,
                        FutureMedicalExpenses = advRow.Table.Columns.Contains("FutureMedicalExpenses") && advRow["FutureMedicalExpenses"] != DBNull.Value ? (decimal?)advRow["FutureMedicalExpenses"] : null,
                        InjuryOtherExpense = advRow.Table.Columns.Contains("InjuryOtherExpense") && advRow["InjuryOtherExpense"] != DBNull.Value ? (decimal?)advRow["InjuryOtherExpense"] : null,
                        EPNumber = advRow.Table.Columns.Contains("EPNumber") ? advRow["EPNumber"]?.ToString() : null,
                        EPCourt = advRow.Table.Columns.Contains("EPCourt") ? advRow["EPCourt"]?.ToString() : null,
                        EPStage = advRow.Table.Columns.Contains("EPStage") ? advRow["EPStage"]?.ToString() : null,
                        EPNextHearingDate = advRow.Table.Columns.Contains("EPNextHearingDate") && advRow["EPNextHearingDate"] != DBNull.Value ? (DateTime?)advRow["EPNextHearingDate"] : null,
                        RoundOffAmount = advRow.Table.Columns.Contains("RoundOffAmount") && advRow["RoundOffAmount"] != DBNull.Value ? (decimal?)advRow["RoundOffAmount"] : null,
                        AsPerECourts = advRow.Table.Columns.Contains("AsPerECourts") ? advRow["AsPerECourts"]?.ToString() : null
                    };

                    if (advRow.Table.Columns.Contains("CustomCompensation") && advRow["CustomCompensation"] != DBNull.Value)
                    {
                        try
                        {
                            var parsed = System.Text.Json.JsonSerializer.Deserialize<List<CustomCompensationHead>>(advRow["CustomCompensation"].ToString()!);
                            if (parsed != null) model.AdverseAward.CustomCompensationHeads = parsed;
                        }
                        catch { }
                    }

                    // 4.1 PWs
                    string pwQuery = "SELECT * FROM MVC_REMIND_BACK_ADVERSE_PW WHERE RemindBackID = @RemindBackID";
                    DataTable pwDt = _db.ExecuteQuery(pwQuery, new[] { new SqlParameter("@RemindBackID", remindBackId) });
                    foreach (DataRow pwr in pwDt.Rows)
                    {
                        string type = pwr["Type"]?.ToString() ?? "";
                        string name = pwr["PWName"]?.ToString() ?? "";
                        if (type == "Petitioner")
                        {
                            model.AdverseAward.PetitionerPWNames.Add(name);
                        }
                        else if (type == "Doctor")
                        {
                            model.AdverseAward.Doctors.Add(new DoctorEvidenceViewModel
                            {
                                DoctorName = name,
                                IsTreated = pwr["IsTreated"] != DBNull.Value && Convert.ToBoolean(pwr["IsTreated"])
                            });
                        }
                        else if (type == "TP_Respondent")
                        {
                            model.RespondentEvidence.Add(new RespondentEvidenceViewModel
                            {
                                Name = name,
                                Remark = pwr.Table.Columns.Contains("Remark") ? pwr["Remark"]?.ToString() : null
                            });
                        }
                        else if (type == "TP_Corporation")
                        {
                            model.CorpEvidence.Add(new RespondentEvidenceViewModel
                            {
                                Name = name,
                                Designation = pwr.Table.Columns.Contains("Designation") ? pwr["Designation"]?.ToString() : null
                            });
                        }
                    }

                    // 4.2 RWs
                    string rwQuery = "SELECT * FROM MVC_REMIND_BACK_ADVERSE_RW WHERE RemindBackID = @RemindBackID";
                    DataTable rwDt = _db.ExecuteQuery(rwQuery, new[] { new SqlParameter("@RemindBackID", remindBackId) });
                    foreach (DataRow rwr in rwDt.Rows)
                    {
                        string rwName = rwr["RWName"]?.ToString() ?? "";
                        if (!string.IsNullOrEmpty(rwName))
                        {
                            model.AdverseAward.RWNames.Add(rwName);
                        }
                    }
                }

                return model;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"GetRemindBackCaseById Error: {ex.Message}");
                return null;
            }
        }

        public bool UpdateLiveSyncInfo(int caseId, DateTime? nextHearingDate, string? stage, string? courtHall)
        {
            try
            {
                string query = @"
                    UPDATE MVC_CASES 
                    SET NextHearingDate = COALESCE(@NextHearingDate, NextHearingDate),
                        CurrentStage = COALESCE(@CurrentStage, CurrentStage)
                    WHERE CaseID = @CaseID";
                var parameters = new[]
                {
                    new SqlParameter("@CaseID", caseId),
                    new SqlParameter("@NextHearingDate", (object?)nextHearingDate ?? DBNull.Value),
                    new SqlParameter("@CurrentStage", (object?)stage ?? DBNull.Value)
                };
                int rows = _db.ExecuteNonQuery(query, parameters);
                return rows > 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"UpdateLiveSyncInfo Error: {ex.Message}");
                return false;
            }
        }

        public bool UpdateCNR(int caseId, string? cnrNumber, string? estCode)
        {
            try
            {
                string query = @"
                    UPDATE MVC_CASES 
                    SET CNRNumber = @CNRNumber,
                        EstCode = COALESCE(@EstCode, EstCode)
                    WHERE CaseID = @CaseID";
                var parameters = new[]
                {
                    new SqlParameter("@CaseID", caseId),
                    new SqlParameter("@CNRNumber", (object?)cnrNumber ?? DBNull.Value),
                    new SqlParameter("@EstCode", (object?)estCode ?? DBNull.Value)
                };
                int rows = _db.ExecuteNonQuery(query, parameters);
                return rows > 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"UpdateCNR Error: {ex.Message}");
                return false;
            }
        }

        public List<MVCCaseViewModel> GetAllThirdPartyCases(int divisionId = 0, int page = 1, int pageSize = 10, string? search = null)
        {
            var cases = new List<MVCCaseViewModel>();
            try
            {
                int offset = (page - 1) * pageSize;
                string query = @"
                    SELECT 
                        c.CaseID, c.DivisionID, c.MVCNo, c.MVCYear, c.MACTID,
                        c.VehicleNo, c.VehicleType, c.AccidentDate, c.CaseType, c.ClaimType,
                        c.ThirdPartyFlag, c.ClaimAmount, c.AdvocateName,
                        c.EntrustmentNo, c.EntrustmentDate,
                        c.NextHearingDate, c.CurrentStage, c.DisposalStatus, c.DisposalResult,
                        c.ClosureDate, c.CreatedAt, c.CNRNumber, c.EstCode, c.CaseTypeCode,
                        d.DivisionNameEnglish as DivisionName,
                        m.MACTName
                    FROM MVC_CASES c
                    LEFT JOIN DIVISION_MASTER d ON c.DivisionID = d.DivisionID
                    LEFT JOIN MACT_MASTER m ON c.MACTID = m.MACTID
                    WHERE c.ThirdPartyFlag = 1";

                var paramsList = new List<SqlParameter>();
                if (divisionId > 0 && divisionId != 5)
                {
                    query += " AND c.DivisionID = @DivisionID";
                    paramsList.Add(new SqlParameter("@DivisionID", divisionId));
                }

                if (!string.IsNullOrWhiteSpace(search))
                {
                    query += @" AND (
                        c.MVCNo LIKE @Search OR 
                        c.VehicleNo LIKE @Search OR 
                        CAST(c.MVCYear AS NVARCHAR) LIKE @Search OR 
                        c.CNRNumber LIKE @Search OR
                        m.MACTName LIKE @Search OR
                        c.AdvocateName LIKE @Search OR
                        c.DisposalStatus LIKE @Search OR
                        c.DisposalResult LIKE @Search
                    )";
                    paramsList.Add(new SqlParameter("@Search", "%" + search.Trim() + "%"));
                }

                query += " ORDER BY c.CreatedAt DESC, c.CaseID DESC OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY";
                paramsList.Add(new SqlParameter("@Offset", offset));
                paramsList.Add(new SqlParameter("@PageSize", pageSize));

                DataTable dt = _db.ExecuteQuery(query, paramsList.ToArray());
                foreach (DataRow row in dt.Rows)
                {
                    int caseId = Convert.ToInt32(row["CaseID"]);
                    var c = new MVCCaseViewModel
                    {
                        CaseID = caseId,
                        DivisionID = row["DivisionID"] != DBNull.Value ? Convert.ToInt32(row["DivisionID"]) : 0,
                        DivisionName = row["DivisionName"]?.ToString() ?? "",
                        MACTID = row["MACTID"] != DBNull.Value ? Convert.ToInt32(row["MACTID"]) : 0,
                        MACTName = row["MACTName"]?.ToString() ?? "",
                        MVCNo = row["MVCNo"]?.ToString() ?? "",
                        MVCYear = row["MVCYear"] != DBNull.Value ? Convert.ToInt32(row["MVCYear"]) : 0,
                        VehicleNo = row["VehicleNo"]?.ToString(),
                        VehicleType = row["VehicleType"]?.ToString() ?? "Corporation",
                        AccidentDate = row["AccidentDate"] != DBNull.Value ? Convert.ToDateTime(row["AccidentDate"]) : null,
                        CaseType = row["CaseType"]?.ToString() ?? "Pending",
                        ClaimType = row["ClaimType"]?.ToString(),
                        ThirdPartyFlag = true,
                        ClaimAmount = row["ClaimAmount"] != DBNull.Value ? Convert.ToDecimal(row["ClaimAmount"]) : null,
                        AdvocateName = row["AdvocateName"]?.ToString(),
                        EntrustmentNo = row["EntrustmentNo"]?.ToString(),
                        EntrustmentDate = row["EntrustmentDate"] != DBNull.Value ? Convert.ToDateTime(row["EntrustmentDate"]) : null,
                        NextHearingDate = row["NextHearingDate"] != DBNull.Value ? Convert.ToDateTime(row["NextHearingDate"]) : null,
                        CurrentStage = row["CurrentStage"]?.ToString() ?? "Pending",
                        DisposalStatus = row["DisposalStatus"]?.ToString(),
                        DisposalResult = row["DisposalResult"]?.ToString(),
                        ClosureDate = row["ClosureDate"] != DBNull.Value ? Convert.ToDateTime(row["ClosureDate"]) : null,
                        CreatedAt = row["CreatedAt"] != DBNull.Value ? Convert.ToDateTime(row["CreatedAt"]) : DateTime.MinValue,
                        CNRNumber = row.Table.Columns.Contains("CNRNumber") && row["CNRNumber"] != DBNull.Value ? row["CNRNumber"].ToString() : null,
                        EstCode = row.Table.Columns.Contains("EstCode") && row["EstCode"] != DBNull.Value ? row["EstCode"].ToString() : null,
                        CaseTypeCode = row.Table.Columns.Contains("CaseTypeCode") && row["CaseTypeCode"] != DBNull.Value ? row["CaseTypeCode"].ToString() : null,
                    };

                    // Load Respondents for third-party cases
                    string respQuery = "SELECT RespondentName, Remarks FROM MVC_CASE_RESPONDENTS WHERE CaseID = @CaseID";
                    DataTable respDt = _db.ExecuteQuery(respQuery, new[] { new SqlParameter("@CaseID", caseId) });
                    foreach (DataRow respRow in respDt.Rows)
                    {
                        c.Respondents.Add(new RespondentViewModel
                        {
                            RespondentName = respRow["RespondentName"]?.ToString() ?? "",
                            Remarks = respRow["Remarks"]?.ToString()
                        });
                    }

                    cases.Add(c);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"GetAllThirdPartyCases Error: {ex.Message}");
            }
            return cases;
        }

        public int GetTotalThirdPartyCaseCount(int divisionId = 0, string? search = null)
        {
            try
            {
                string query = @"
                    SELECT COUNT(*)
                    FROM MVC_CASES c
                    LEFT JOIN MACT_MASTER m ON c.MACTID = m.MACTID
                    WHERE c.ThirdPartyFlag = 1";

                var paramsList = new List<SqlParameter>();
                if (divisionId > 0 && divisionId != 5)
                {
                    query += " AND c.DivisionID = @DivisionID";
                    paramsList.Add(new SqlParameter("@DivisionID", divisionId));
                }

                if (!string.IsNullOrWhiteSpace(search))
                {
                    query += @" AND (
                        c.MVCNo LIKE @Search OR 
                        c.VehicleNo LIKE @Search OR 
                        CAST(c.MVCYear AS NVARCHAR) LIKE @Search OR 
                        c.CNRNumber LIKE @Search OR
                        m.MACTName LIKE @Search OR
                        c.AdvocateName LIKE @Search OR
                        c.DisposalStatus LIKE @Search OR
                        c.DisposalResult LIKE @Search
                    )";
                    paramsList.Add(new SqlParameter("@Search", "%" + search.Trim() + "%"));
                }

                object result = _db.ExecuteScalar(query, paramsList.ToArray()) ?? 0;
                return Convert.ToInt32(result);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"GetTotalThirdPartyCaseCount Error: {ex.Message}");
                return 0;
            }
        }
    }
}

