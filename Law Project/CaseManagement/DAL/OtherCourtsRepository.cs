using Microsoft.Data.SqlClient;
using MVCCaseManagement.Models;
using MVCCaseManagement.Common;
using System.Data;

namespace MVCCaseManagement.DAL
{
    public class OtherCourtsRepository : IOtherCourtsRepository
    {
        private readonly DBHelper _db;

        public OtherCourtsRepository(DBHelper db)
        {
            _db = db;
        }

        public int SaveCase(OtherCourtsCase model)
        {
            // Ensure PetitionerName and RespondentName are safely populated from collections
            if (string.IsNullOrWhiteSpace(model.PetitionerName))
            {
                if (model.Petitioners != null && model.Petitioners.Any(p => !string.IsNullOrWhiteSpace(p.Name)))
                {
                    model.PetitionerName = model.Petitioners.First(p => !string.IsNullOrWhiteSpace(p.Name)).Name;
                }
                else if (model.Respondents != null && model.Respondents.Any(r => !string.IsNullOrWhiteSpace(r.Name)) && model.LitigantType == "Claimant")
                {
                    model.PetitionerName = model.Respondents.First(r => !string.IsNullOrWhiteSpace(r.Name)).Name;
                }
                else if (model.LitigantType == "Corporation")
                {
                    model.PetitionerName = "NWKRTC / Corporation";
                }
            }

            if (string.IsNullOrWhiteSpace(model.RespondentName))
            {
                if (model.Respondents != null && model.Respondents.Any(r => !string.IsNullOrWhiteSpace(r.Name)) && model.LitigantType == "Corporation")
                {
                    model.RespondentName = model.Respondents.First(r => !string.IsNullOrWhiteSpace(r.Name)).Name;
                }
                else if (model.LitigantType == "Claimant")
                {
                    model.RespondentName = "NWKRTC / Corporation";
                }
            }

            string query = @"
                INSERT INTO OTHER_CASES (
                    DivisionID, CaseType, LitigantType, IsPendingForFiling, CaseNumber, CaseYear, 
                    Court, CaseNature, ClaimDetails, RespondentName, EntrustmentNumber, EntrustmentDate, AdvocateName, 
                    EvidenceFiled, CaseStatus, NextDateOfHearing, InterimOrder, InterimOrderFilePath, 
                    AwardDetails, Result, DisposalStatus, ClosureDate, ClosureRemark, ClosureNumber, OutwardNumber, OutwardDate, 
                    AdvocateOpinion, LawOfficerOpinion, DCFinalDecision, FinalForwardingStatus, FinalJudgmentFilePath,
                    PetitionerName, PetitionerRelationship, ClaimType, DateOfClaimPetition, CaseStage,
                    DocumentSent, ObjectionVerified, IsObjectionFiled, ObjectionPending, ObjectionRemarks, 
                    ObjectionOutwardNumber, ObjectionFiledDate,
                    InitialOutwardNumber, InitialOutwardDate,
                    VehicleNumber, DateOfAccident, ClaimAmount, ClaimRemarks,
                    IsVehicleInvolved, VehicleType, IsCorporationEmployee, MannerOfIncident,
                    CopyDisposedOn, CopyAppliedOn, CopyReadyOn, CopyDeliveredOn, CopyReceivedAtDivision, CertifiedCopyRemarks,
                    AppealNumber, AppealYear, ArisingOutOSNumber, OSYear, AppealEntrustmentNumber, AppealEntrustmentDate, 
                    CourtAppellateAuth, AppealAdvocateName, AppealComplianceAmount, AppealChequeNumber, AppealChequeDate, 
                    AppealAwardDetails, AppealCaseDisposedOn, AppealCopyAppliedOn, AppealCopyReadyOn, AppealCopyDeliveredOn, 
                    AppealCopyReceivedAtDivision, AppealDelayRemarks, AppealAdvocateOpinion, AppealALOOpinion, AppealDCOpinion, 
                    AppealForwardingStatus, AppealOutwardNumber, AppealOutwardDate, AppealClosureNumber, AppealClosureDate, 
                    AppealJudgmentCopyPath,
                    CNRNumber, EstCode, CaseTypeCode, OtherCourtDetails, LastNapixSyncAt, LastNapixSyncStatus, LastNapixSyncError,
                    NapixSyncAttemptCount, NapixDataHash, PendDispStatus, EstName, ECourtsStage, ECourtsCourtNo, ECourtsJudge,
                    CreatedBy, CreatedDate
                ) VALUES (
                    @DivisionID, @CaseType, @LitigantType, @IsPendingForFiling, @CaseNumber, @CaseYear, 
                    @Court, @CaseNature, @ClaimDetails, @RespondentName, @EntrustmentNumber, @EntrustmentDate, @AdvocateName, 
                    @EvidenceFiled, @CaseStatus, @NextDateOfHearing, @InterimOrder, @InterimOrderFilePath, 
                    @AwardDetails, @Result, @DisposalStatus, @ClosureDate, @ClosureRemark, @ClosureNumber, @OutwardNumber, @OutwardDate, 
                    @AdvocateOpinion, @LawOfficerOpinion, @DCFinalDecision, @FinalForwardingStatus, @FinalJudgmentFilePath,
                    @PetitionerName, @PetitionerRelationship, @ClaimType, @DateOfClaimPetition, @CaseStage,
                    @DocumentSent, @ObjectionVerified, @IsObjectionFiled, @ObjectionPending, @ObjectionRemarks,
                    @ObjectionOutwardNumber, @ObjectionFiledDate,
                    @InitialOutwardNumber, @InitialOutwardDate,
                    @VehicleNumber, @DateOfAccident, @ClaimAmount, @ClaimRemarks,
                    @IsVehicleInvolved, @VehicleType, @IsCorporationEmployee, @MannerOfIncident,
                    @CopyDisposedOn, @CopyAppliedOn, @CopyReadyOn, @CopyDeliveredOn, @CopyReceivedAtDivision, @CertifiedCopyRemarks,
                    @AppealNumber, @AppealYear, @ArisingOutOSNumber, @OSYear, @AppealEntrustmentNumber, @AppealEntrustmentDate, 
                    @CourtAppellateAuth, @AppealAdvocateName, @AppealComplianceAmount, @AppealChequeNumber, @AppealChequeDate, 
                    @AppealAwardDetails, @AppealCaseDisposedOn, @AppealCopyAppliedOn, @AppealCopyReadyOn, @AppealCopyDeliveredOn, 
                    @AppealCopyReceivedAtDivision, @AppealDelayRemarks, @AppealAdvocateOpinion, @AppealALOOpinion, @AppealDCOpinion, 
                    @AppealForwardingStatus, @AppealOutwardNumber, @AppealOutwardDate, @AppealClosureNumber, @AppealClosureDate, 
                    @AppealJudgmentCopyPath,
                    @CNRNumber, @EstCode, @CaseTypeCode, @OtherCourtDetails, @LastNapixSyncAt, @LastNapixSyncStatus, @LastNapixSyncError,
                    @NapixSyncAttemptCount, @NapixDataHash, @PendDispStatus, @EstName, @ECourtsStage, @ECourtsCourtNo, @ECourtsJudge,
                    @CreatedBy, GETDATE()
                );
                SELECT CAST(SCOPE_IDENTITY() AS INT);";

            using (var conn = new SqlConnection(_db.GetConnectionString()))
            {
                conn.Open();
                using (var trans = conn.BeginTransaction())
                {
                    try
                    {
                        var parameters = BuildParameters(model);
                        int caseId = 0;
                        using (var cmd = new SqlCommand(query, conn, trans))
                        {
                            cmd.Parameters.AddRange(parameters);
                            var result = cmd.ExecuteScalar();
                            caseId = result != null ? Convert.ToInt32(result) : 0;
                        }

                        if (caseId > 0)
                        {
                            // Petitioners
                            if (model.Petitioners != null)
                            {
                                foreach (var pet in model.Petitioners)
                                {
                                    if (!string.IsNullOrWhiteSpace(pet.Name))
                                    {
                                        string petQuery = "INSERT INTO OTHER_CASE_PETITIONERS (CaseID, PetitionerName, PetitionerRemark) VALUES (@CaseID, @Name, @Remark)";
                                        using (var petCmd = new SqlCommand(petQuery, conn, trans))
                                        {
                                            petCmd.Parameters.AddWithValue("@CaseID", caseId);
                                            petCmd.Parameters.AddWithValue("@Name", pet.Name);
                                            petCmd.Parameters.AddWithValue("@Remark", (object?)pet.Remark ?? DBNull.Value);
                                            petCmd.ExecuteNonQuery();
                                        }
                                    }
                                }
                            }

                            // Respondents
                            if (model.Respondents != null)
                            {
                                foreach (var resp in model.Respondents)
                                {
                                    if (!string.IsNullOrWhiteSpace(resp.Name))
                                    {
                                        string respQuery = "INSERT INTO OTHER_CASE_RESPONDENTS (CaseID, RespondentName, RespondentRemark) VALUES (@CaseID, @Name, @Remark)";
                                        using (var respCmd = new SqlCommand(respQuery, conn, trans))
                                        {
                                            respCmd.Parameters.AddWithValue("@CaseID", caseId);
                                            respCmd.Parameters.AddWithValue("@Name", resp.Name);
                                            respCmd.Parameters.AddWithValue("@Remark", (object?)resp.Remark ?? DBNull.Value);
                                            respCmd.ExecuteNonQuery();
                                        }
                                    }
                                }
                            }

                            // Respondent Evidence
                            if (model.RespondentEvidence != null)
                            {
                                foreach (var resp in model.RespondentEvidence)
                                {
                                    if (!string.IsNullOrWhiteSpace(resp.Name))
                                    {
                                        string q = "INSERT INTO OTHER_CASE_EVIDENCE_RESPONDENT (CaseID, Name, Remark) VALUES (@CaseID, @Name, @Remark)";
                                        using (var respEvCmd = new SqlCommand(q, conn, trans))
                                        {
                                            respEvCmd.Parameters.AddWithValue("@CaseID", caseId);
                                            respEvCmd.Parameters.AddWithValue("@Name", resp.Name);
                                            respEvCmd.Parameters.AddWithValue("@Remark", (object?)resp.Remark ?? DBNull.Value);
                                            respEvCmd.ExecuteNonQuery();
                                        }
                                    }
                                }
                            }

                            // Corp Evidence
                            if (model.CorpEvidence != null)
                            {
                                foreach (var corp in model.CorpEvidence)
                                {
                                    if (!string.IsNullOrWhiteSpace(corp.Name))
                                    {
                                        string q = "INSERT INTO OTHER_CASE_EVIDENCE_CORP (CaseID, Name, Designation) VALUES (@CaseID, @Name, @Designation)";
                                        using (var corpEvCmd = new SqlCommand(q, conn, trans))
                                        {
                                            corpEvCmd.Parameters.AddWithValue("@CaseID", caseId);
                                            corpEvCmd.Parameters.AddWithValue("@Name", corp.Name);
                                            corpEvCmd.Parameters.AddWithValue("@Designation", (object?)corp.Designation ?? DBNull.Value);
                                            corpEvCmd.ExecuteNonQuery();
                                        }
                                    }
                                }
                            }

                            // Enclosed Documents
                            if (model.EnclosedDocuments != null)
                            {
                                foreach (var doc in model.EnclosedDocuments)
                                {
                                    if (!string.IsNullOrWhiteSpace(doc.DocName))
                                    {
                                        string q = "INSERT INTO OTHER_CASE_DOCUMENTS (CaseID, DocName, PageCount) VALUES (@CaseID, @DocName, @PageCount)";
                                        using (var docCmd = new SqlCommand(q, conn, trans))
                                        {
                                            docCmd.Parameters.AddWithValue("@CaseID", caseId);
                                            docCmd.Parameters.AddWithValue("@DocName", doc.DocName);
                                            docCmd.Parameters.AddWithValue("@PageCount", (object?)doc.PageCount ?? DBNull.Value);
                                            docCmd.ExecuteNonQuery();
                                        }
                                    }
                                }
                            }
                        }

                        trans.Commit();
                        return caseId;
                    }
                    catch
                    {
                        trans.Rollback();
                        throw;
                    }
                }
            }
        }

        public bool UpdateCase(OtherCourtsCase model)
        {
            // Fallback resolution for PetitionerName and RespondentName on update
            if (string.IsNullOrWhiteSpace(model.PetitionerName))
            {
                if (model.Petitioners != null && model.Petitioners.Any(p => !string.IsNullOrWhiteSpace(p.Name)))
                {
                    model.PetitionerName = model.Petitioners.First(p => !string.IsNullOrWhiteSpace(p.Name)).Name;
                }
                else if (model.Respondents != null && model.Respondents.Any(r => !string.IsNullOrWhiteSpace(r.Name)) && model.LitigantType == "Claimant")
                {
                    model.PetitionerName = model.Respondents.First(r => !string.IsNullOrWhiteSpace(r.Name)).Name;
                }
            }

            if (string.IsNullOrWhiteSpace(model.RespondentName))
            {
                if (model.Respondents != null && model.Respondents.Any(r => !string.IsNullOrWhiteSpace(r.Name)) && model.LitigantType == "Corporation")
                {
                    model.RespondentName = model.Respondents.First(r => !string.IsNullOrWhiteSpace(r.Name)).Name;
                }
            }

            string query = @"
                UPDATE OTHER_CASES SET
                    DivisionID = @DivisionID,
                    CaseType = @CaseType,
                    LitigantType = @LitigantType,
                    IsPendingForFiling = @IsPendingForFiling,
                    CaseNumber = @CaseNumber,
                    CaseYear = @CaseYear,
                    Court = @Court,
                    CaseNature = @CaseNature,
                    ClaimDetails = @ClaimDetails,
                    RespondentName = @RespondentName,
                    EntrustmentNumber = @EntrustmentNumber,
                    EntrustmentDate = @EntrustmentDate,
                    AdvocateName = @AdvocateName,
                    EvidenceFiled = @EvidenceFiled,
                    CaseStatus = @CaseStatus,
                    NextDateOfHearing = @NextDateOfHearing,
                    InterimOrder = @InterimOrder,
                    InterimOrderFilePath = COALESCE(@InterimOrderFilePath, InterimOrderFilePath),
                    AwardDetails = @AwardDetails,
                    Result = @Result,
                    DisposalStatus = @DisposalStatus,
                    ClosureDate = @ClosureDate,
                    ClosureRemark = @ClosureRemark,
                    ClosureNumber = @ClosureNumber,
                    AdvocateOpinion = @AdvocateOpinion,
                    LawOfficerOpinion = @LawOfficerOpinion,
                    DCFinalDecision = @DCFinalDecision,
                    FinalForwardingStatus = @FinalForwardingStatus,
                    FinalJudgmentFilePath = COALESCE(@FinalJudgmentFilePath, FinalJudgmentFilePath),
                    PetitionerName = @PetitionerName,
                    PetitionerRelationship = @PetitionerRelationship,
                    ClaimType = @ClaimType,
                    DateOfClaimPetition = @DateOfClaimPetition,
                    CaseStage = @CaseStage,
                    DocumentSent = @DocumentSent,
                    ObjectionVerified = @ObjectionVerified,
                    IsObjectionFiled = @IsObjectionFiled,
                    ObjectionPending = @ObjectionPending,
                    ObjectionRemarks = @ObjectionRemarks,
                    ObjectionOutwardNumber = @ObjectionOutwardNumber,
                    ObjectionFiledDate = @ObjectionFiledDate,
                    InitialOutwardNumber = @InitialOutwardNumber,
                    InitialOutwardDate = @InitialOutwardDate,
                    VehicleNumber = @VehicleNumber,
                    DateOfAccident = @DateOfAccident,
                    ClaimAmount = @ClaimAmount,
                    IsVehicleInvolved = @IsVehicleInvolved,
                    VehicleType = @VehicleType,
                    IsCorporationEmployee = @IsCorporationEmployee,
                    MannerOfIncident = @MannerOfIncident,
                    CopyDisposedOn = @CopyDisposedOn,
                    CopyAppliedOn = @CopyAppliedOn,
                    CopyReadyOn = @CopyReadyOn,
                    CopyDeliveredOn = @CopyDeliveredOn,
                    CopyReceivedAtDivision = @CopyReceivedAtDivision,
                    CertifiedCopyRemarks = @CertifiedCopyRemarks,
                    ClaimRemarks = @ClaimRemarks,
                    OutwardNumber = @OutwardNumber,
                    OutwardDate = @OutwardDate,
                    AppealNumber = @AppealNumber,
                    AppealYear = @AppealYear,
                    ArisingOutOSNumber = @ArisingOutOSNumber,
                    OSYear = @OSYear,
                    AppealEntrustmentNumber = @AppealEntrustmentNumber,
                    AppealEntrustmentDate = @AppealEntrustmentDate,
                    CourtAppellateAuth = @CourtAppellateAuth,
                    AppealAdvocateName = @AppealAdvocateName,
                    AppealComplianceAmount = @AppealComplianceAmount,
                    AppealChequeNumber = @AppealChequeNumber,
                    AppealChequeDate = @AppealChequeDate,
                    AppealAwardDetails = @AppealAwardDetails,
                    AppealCaseDisposedOn = @AppealCaseDisposedOn,
                    AppealCopyAppliedOn = @AppealCopyAppliedOn,
                    AppealCopyReadyOn = @AppealCopyReadyOn,
                    AppealCopyDeliveredOn = @AppealCopyDeliveredOn,
                    AppealCopyReceivedAtDivision = @AppealCopyReceivedAtDivision,
                    AppealDelayRemarks = @AppealDelayRemarks,
                    AppealAdvocateOpinion = @AppealAdvocateOpinion,
                    AppealALOOpinion = @AppealALOOpinion,
                    AppealDCOpinion = @AppealDCOpinion,
                    AppealForwardingStatus = @AppealForwardingStatus,
                    AppealOutwardNumber = @AppealOutwardNumber,
                    AppealOutwardDate = @AppealOutwardDate,
                    AppealClosureNumber = @AppealClosureNumber,
                    AppealClosureDate = @AppealClosureDate,
                    AppealJudgmentCopyPath = COALESCE(@AppealJudgmentCopyPath, AppealJudgmentCopyPath),
                    CNRNumber = COALESCE(@CNRNumber, CNRNumber),
                    EstCode = COALESCE(@EstCode, EstCode),
                    CaseTypeCode = COALESCE(@CaseTypeCode, CaseTypeCode),
                    OtherCourtDetails = COALESCE(@OtherCourtDetails, OtherCourtDetails),
                    LastNapixSyncAt = COALESCE(@LastNapixSyncAt, LastNapixSyncAt),
                    LastNapixSyncStatus = COALESCE(@LastNapixSyncStatus, LastNapixSyncStatus),
                    LastNapixSyncError = @LastNapixSyncError,
                    NapixSyncAttemptCount = @NapixSyncAttemptCount,
                    NapixDataHash = COALESCE(@NapixDataHash, NapixDataHash),
                    PendDispStatus = COALESCE(@PendDispStatus, PendDispStatus),
                    EstName = COALESCE(@EstName, EstName),
                    ECourtsStage = COALESCE(@ECourtsStage, ECourtsStage),
                    ECourtsCourtNo = COALESCE(@ECourtsCourtNo, ECourtsCourtNo),
                    ECourtsJudge = COALESCE(@ECourtsJudge, ECourtsJudge),
                    ModifiedBy = @ModifiedBy,
                    ModifiedDate = GETDATE()
                WHERE CaseID = @CaseID";

            using (var conn = new SqlConnection(_db.GetConnectionString()))
            {
                conn.Open();
                using (var trans = conn.BeginTransaction())
                {
                    try
                    {
                        var parameters = BuildParameters(model, includeId: true);
                        bool success = false;
                        using (var cmd = new SqlCommand(query, conn, trans))
                        {
                            cmd.Parameters.AddRange(parameters);
                            success = cmd.ExecuteNonQuery() > 0;
                        }

                        if (success)
                        {
                            // Petitioners (only synchronize if valid items are provided to avoid wiping existing child records)
                            if (model.Petitioners != null && model.Petitioners.Any(p => !string.IsNullOrWhiteSpace(p.Name)))
                            {
                                using (var delCmd = new SqlCommand("DELETE FROM OTHER_CASE_PETITIONERS WHERE CaseID = @CaseID", conn, trans))
                                {
                                    delCmd.Parameters.AddWithValue("@CaseID", model.CaseID);
                                    delCmd.ExecuteNonQuery();
                                }
                                foreach (var pet in model.Petitioners)
                                {
                                    if (!string.IsNullOrWhiteSpace(pet.Name))
                                    {
                                        string petQuery = "INSERT INTO OTHER_CASE_PETITIONERS (CaseID, PetitionerName, PetitionerRemark) VALUES (@CaseID, @Name, @Remark)";
                                        using (var insCmd = new SqlCommand(petQuery, conn, trans))
                                        {
                                            insCmd.Parameters.AddWithValue("@CaseID", model.CaseID);
                                            insCmd.Parameters.AddWithValue("@Name", pet.Name);
                                            insCmd.Parameters.AddWithValue("@Remark", (object?)pet.Remark ?? DBNull.Value);
                                            insCmd.ExecuteNonQuery();
                                        }
                                    }
                                }
                            }
                            
                            // Respondents (only synchronize if valid items are provided)
                            if (model.Respondents != null && model.Respondents.Any(r => !string.IsNullOrWhiteSpace(r.Name)))
                            {
                                using (var delCmd = new SqlCommand("DELETE FROM OTHER_CASE_RESPONDENTS WHERE CaseID = @CaseID", conn, trans))
                                {
                                    delCmd.Parameters.AddWithValue("@CaseID", model.CaseID);
                                    delCmd.ExecuteNonQuery();
                                }
                                foreach (var resp in model.Respondents)
                                {
                                    if (!string.IsNullOrWhiteSpace(resp.Name))
                                    {
                                        string respQuery = "INSERT INTO OTHER_CASE_RESPONDENTS (CaseID, RespondentName, RespondentRemark) VALUES (@CaseID, @Name, @Remark)";
                                        using (var insCmd = new SqlCommand(respQuery, conn, trans))
                                        {
                                            insCmd.Parameters.AddWithValue("@CaseID", model.CaseID);
                                            insCmd.Parameters.AddWithValue("@Name", resp.Name);
                                            insCmd.Parameters.AddWithValue("@Remark", (object?)resp.Remark ?? DBNull.Value);
                                            insCmd.ExecuteNonQuery();
                                        }
                                    }
                                }
                            }

                            // Respondent Evidence (only synchronize if valid items are provided)
                            if (model.RespondentEvidence != null && model.RespondentEvidence.Any(e => !string.IsNullOrWhiteSpace(e.Name)))
                            {
                                using (var delCmd = new SqlCommand("DELETE FROM OTHER_CASE_EVIDENCE_RESPONDENT WHERE CaseID = @CaseID", conn, trans))
                                {
                                    delCmd.Parameters.AddWithValue("@CaseID", model.CaseID);
                                    delCmd.ExecuteNonQuery();
                                }
                                foreach (var resp in model.RespondentEvidence)
                                {
                                    if (!string.IsNullOrWhiteSpace(resp.Name))
                                    {
                                        string q = "INSERT INTO OTHER_CASE_EVIDENCE_RESPONDENT (CaseID, Name, Remark) VALUES (@CaseID, @Name, @Remark)";
                                        using (var insCmd = new SqlCommand(q, conn, trans))
                                        {
                                            insCmd.Parameters.AddWithValue("@CaseID", model.CaseID);
                                            insCmd.Parameters.AddWithValue("@Name", resp.Name);
                                            insCmd.Parameters.AddWithValue("@Remark", (object?)resp.Remark ?? DBNull.Value);
                                            insCmd.ExecuteNonQuery();
                                        }
                                    }
                                }
                            }

                            // Corp Evidence (only synchronize if valid items are provided)
                            if (model.CorpEvidence != null && model.CorpEvidence.Any(c => !string.IsNullOrWhiteSpace(c.Name)))
                            {
                                using (var delCmd = new SqlCommand("DELETE FROM OTHER_CASE_EVIDENCE_CORP WHERE CaseID = @CaseID", conn, trans))
                                {
                                    delCmd.Parameters.AddWithValue("@CaseID", model.CaseID);
                                    delCmd.ExecuteNonQuery();
                                }
                                foreach (var corp in model.CorpEvidence)
                                {
                                    if (!string.IsNullOrWhiteSpace(corp.Name))
                                    {
                                        string q = "INSERT INTO OTHER_CASE_EVIDENCE_CORP (CaseID, Name, Designation) VALUES (@CaseID, @Name, @Designation)";
                                        using (var insCmd = new SqlCommand(q, conn, trans))
                                        {
                                            insCmd.Parameters.AddWithValue("@CaseID", model.CaseID);
                                            insCmd.Parameters.AddWithValue("@Name", corp.Name);
                                            insCmd.Parameters.AddWithValue("@Designation", (object?)corp.Designation ?? DBNull.Value);
                                            insCmd.ExecuteNonQuery();
                                        }
                                    }
                                }
                            }

                            // Enclosed Documents (only synchronize if valid items are provided)
                            if (model.EnclosedDocuments != null && model.EnclosedDocuments.Any(d => !string.IsNullOrWhiteSpace(d.DocName)))
                            {
                                using (var delCmd = new SqlCommand("DELETE FROM OTHER_CASE_DOCUMENTS WHERE CaseID = @CaseID", conn, trans))
                                {
                                    delCmd.Parameters.AddWithValue("@CaseID", model.CaseID);
                                    delCmd.ExecuteNonQuery();
                                }
                                foreach (var doc in model.EnclosedDocuments)
                                {
                                    if (!string.IsNullOrWhiteSpace(doc.DocName))
                                    {
                                        string q = "INSERT INTO OTHER_CASE_DOCUMENTS (CaseID, DocName, PageCount) VALUES (@CaseID, @DocName, @PageCount)";
                                        using (var insCmd = new SqlCommand(q, conn, trans))
                                        {
                                            insCmd.Parameters.AddWithValue("@CaseID", model.CaseID);
                                            insCmd.Parameters.AddWithValue("@DocName", doc.DocName);
                                            insCmd.Parameters.AddWithValue("@PageCount", (object?)doc.PageCount ?? DBNull.Value);
                                            insCmd.ExecuteNonQuery();
                                        }
                                    }
                                }
                            }
                        }

                        trans.Commit();
                        return success;
                    }
                    catch
                    {
                        trans.Rollback();
                        throw;
                    }
                }
            }
        }

        public OtherCourtsCase? GetCaseById(int caseId)
        {
            string query = @"
                SELECT c.*, d.DivisionNameEnglish AS DivisionName
                FROM OTHER_CASES c
                LEFT JOIN DIVISION_MASTER d ON c.DivisionID = d.DivisionID
                WHERE c.CaseID = @CaseID";

            var dt = _db.ExecuteQuery(query, new[] { new SqlParameter("@CaseID", caseId) });
            if (dt.Rows.Count == 0) return null;

            var model = MapToModel(dt.Rows[0]);
            
            // Load Petitioners
            string petQuery = "SELECT PetitionerName, PetitionerRemark FROM OTHER_CASE_PETITIONERS WHERE CaseID = @CaseID";
            var petDt = _db.ExecuteQuery(petQuery, new[] { new SqlParameter("@CaseID", caseId) });
            foreach (DataRow row in petDt.Rows)
            {
                model.Petitioners.Add(new PetitionerInfo { 
                    Name = row["PetitionerName"].ToString() ?? "",
                    Remark = row["PetitionerRemark"].ToString() ?? ""
                });
            }

            // Load Respondents
            string respQuery = "SELECT RespondentName, RespondentRemark FROM OTHER_CASE_RESPONDENTS WHERE CaseID = @CaseID";
            var respDt = _db.ExecuteQuery(respQuery, new[] { new SqlParameter("@CaseID", caseId) });
            foreach (DataRow row in respDt.Rows)
            {
                model.Respondents.Add(new RespondentInfo { 
                    Name = row["RespondentName"].ToString() ?? "",
                    Remark = row["RespondentRemark"]?.ToString() 
                });
            }

            // Load Respondent Evidence
            string respEvQuery = "SELECT Name, Remark FROM OTHER_CASE_EVIDENCE_RESPONDENT WHERE CaseID = @CaseID";
            var respEvDt = _db.ExecuteQuery(respEvQuery, new[] { new SqlParameter("@CaseID", caseId) });
            foreach (DataRow row in respEvDt.Rows)
            {
                model.RespondentEvidence.Add(new RespondentInfo { 
                    Name = row["Name"].ToString() ?? "",
                    Remark = row["Remark"]?.ToString() 
                });
            }

            // Load Corp Evidence
            string corpEvQuery = "SELECT Name, Designation FROM OTHER_CASE_EVIDENCE_CORP WHERE CaseID = @CaseID";
            var corpEvDt = _db.ExecuteQuery(corpEvQuery, new[] { new SqlParameter("@CaseID", caseId) });
            foreach (DataRow row in corpEvDt.Rows)
            {
                model.CorpEvidence.Add(new CorpEvidence { 
                    Name = row["Name"].ToString() ?? "",
                    Designation = row["Designation"]?.ToString() 
                });
            }

            // Load Enclosed Documents
            string docsQuery = "SELECT DocName, PageCount FROM OTHER_CASE_DOCUMENTS WHERE CaseID = @CaseID";
            var docsDt = _db.ExecuteQuery(docsQuery, new[] { new SqlParameter("@CaseID", caseId) });
            foreach (DataRow row in docsDt.Rows)
            {
                model.EnclosedDocuments.Add(new EnclosedDocument { 
                    DocName = row["DocName"].ToString() ?? "",
                    PageCount = row["PageCount"] != DBNull.Value ? Convert.ToInt32(row["PageCount"]) : null
                });
            }

            return model;
        }

        public IEnumerable<OtherCourtsCase> GetAllCases(int divisionId = 0, string? caseType = null, string? litigantType = null)
        {
            var paramsList = new List<SqlParameter>();
            string whereClause = "WHERE 1=1";

            if (divisionId > 0 && divisionId != 5)
            {
                whereClause += " AND c.DivisionID = @DivisionID";
                paramsList.Add(new SqlParameter("@DivisionID", divisionId));
            }

            if (!string.IsNullOrEmpty(caseType) && caseType != "All")
            {
                whereClause += " AND c.CaseType = @CaseType";
                paramsList.Add(new SqlParameter("@CaseType", caseType));
            }

            if (!string.IsNullOrEmpty(litigantType) && litigantType != "All")
            {
                whereClause += " AND c.LitigantType = @LitigantType";
                paramsList.Add(new SqlParameter("@LitigantType", litigantType));
            }

            string query = $@"
                SELECT c.*, d.DivisionNameEnglish AS DivisionName
                FROM OTHER_CASES c
                LEFT JOIN DIVISION_MASTER d ON c.DivisionID = d.DivisionID
                {whereClause}
                ORDER BY c.CreatedDate DESC";

            var dt = _db.ExecuteQuery(query, paramsList.ToArray());
            return dt.AsEnumerable().Select(row => MapToModel(row)).ToList();
        }

        public int GetTotalCount(int divisionId = 0, string? search = null)
        {
            var paramsList = new List<SqlParameter>();
            string whereClause = "WHERE 1=1";

            if (divisionId > 0 && divisionId != 5)
            {
                whereClause += " AND DivisionID = @DivisionID";
                paramsList.Add(new SqlParameter("@DivisionID", divisionId));
            }

            if (!string.IsNullOrEmpty(search))
            {
                whereClause += " AND (CaseNumber LIKE @Search OR CaseNature LIKE @Search)";
                paramsList.Add(new SqlParameter("@Search", $"%{search}%"));
            }

            string query = $"SELECT COUNT(*) FROM OTHER_CASES {whereClause}";
            var result = _db.ExecuteScalar(query, paramsList.ToArray());
            return result != null ? Convert.ToInt32(result) : 0;
        }

        private SqlParameter[] BuildParameters(OtherCourtsCase m, bool includeId = false)
        {
            var list = new List<SqlParameter>
            {
                new SqlParameter("@DivisionID", m.DivisionID),
                new SqlParameter("@CaseType", m.CaseType),
                new SqlParameter("@LitigantType", m.LitigantType),
                new SqlParameter("@IsPendingForFiling", m.IsPendingForFiling),
                new SqlParameter("@CaseNumber", (object?)m.CaseNumber ?? DBNull.Value),
                new SqlParameter("@CaseYear", (object?)m.CaseYear ?? DBNull.Value),
                new SqlParameter("@Court", (object?)m.Court ?? DBNull.Value),
                new SqlParameter("@CaseNature", (object?)m.CaseNature ?? DBNull.Value),
                new SqlParameter("@ClaimDetails", (object?)m.ClaimDetails ?? DBNull.Value),
                new SqlParameter("@RespondentName", (object?)m.RespondentName ?? DBNull.Value),
                new SqlParameter("@EntrustmentNumber", (object?)m.EntrustmentNumber ?? DBNull.Value),
                new SqlParameter("@EntrustmentDate", (object?)m.EntrustmentDate ?? DBNull.Value),
                new SqlParameter("@AdvocateName", (object?)m.AdvocateName ?? DBNull.Value),
                new SqlParameter("@EvidenceFiled", (object?)m.EvidenceFiled ?? DBNull.Value),
                new SqlParameter("@CaseStatus", (object?)m.CaseStatus ?? DBNull.Value),
                new SqlParameter("@NextDateOfHearing", (object?)m.NextDateOfHearing ?? DBNull.Value),
                new SqlParameter("@InterimOrder", (object?)m.InterimOrder ?? DBNull.Value),
                new SqlParameter("@InterimOrderFilePath", (object?)m.InterimOrderFilePath ?? DBNull.Value),
                new SqlParameter("@AwardDetails", (object?)m.AwardDetails ?? DBNull.Value),
                new SqlParameter("@Result", (object?)m.Result ?? DBNull.Value),
                new SqlParameter("@DisposalStatus", (object?)m.DisposalStatus ?? DBNull.Value),
                
                // CC Details
                new SqlParameter("@CopyDisposedOn", (object?)m.CopyDisposedOn ?? DBNull.Value),
                new SqlParameter("@CopyAppliedOn", (object?)m.CopyAppliedOn ?? DBNull.Value),
                new SqlParameter("@CopyReadyOn", (object?)m.CopyReadyOn ?? DBNull.Value),
                new SqlParameter("@CopyDeliveredOn", (object?)m.CopyDeliveredOn ?? DBNull.Value),
                new SqlParameter("@CopyReceivedAtDivision", (object?)m.CopyReceivedAtDivision ?? DBNull.Value),
                new SqlParameter("@CertifiedCopyRemarks", (object?)m.CertifiedCopyRemarks ?? DBNull.Value),

                new SqlParameter("@AdvocateOpinion", (object?)m.AdvocateOpinion ?? DBNull.Value),
                new SqlParameter("@LawOfficerOpinion", (object?)m.LawOfficerOpinion ?? DBNull.Value),
                new SqlParameter("@DCFinalDecision", (object?)m.DCFinalDecision ?? DBNull.Value),
                new SqlParameter("@FinalForwardingStatus", (object?)m.FinalForwardingStatus ?? DBNull.Value),
                new SqlParameter("@FinalJudgmentFilePath", (object?)m.FinalJudgmentFilePath ?? DBNull.Value),
                new SqlParameter("@PetitionerName", (object?)m.PetitionerName ?? DBNull.Value),
                new SqlParameter("@PetitionerRelationship", (object?)m.PetitionerRelationship ?? DBNull.Value),
                new SqlParameter("@ClaimType", (object?)m.ClaimType ?? DBNull.Value),
                new SqlParameter("@DateOfClaimPetition", (object?)m.DateOfClaimPetition ?? DBNull.Value),
                new SqlParameter("@CaseStage", (object?)m.CaseStage ?? DBNull.Value),
                new SqlParameter("@DocumentSent", (object?)m.DocumentSent ?? DBNull.Value),
                new SqlParameter("@ObjectionVerified", (object?)m.ObjectionVerified ?? DBNull.Value),
                new SqlParameter("@IsObjectionFiled", (object?)m.IsObjectionFiled ?? DBNull.Value),
                new SqlParameter("@ObjectionPending", (object?)m.ObjectionPending ?? DBNull.Value),
                new SqlParameter("@ObjectionRemarks", (object?)m.ObjectionRemarks ?? DBNull.Value),
                new SqlParameter("@ObjectionOutwardNumber", (object?)m.ObjectionOutwardNumber ?? DBNull.Value),
                new SqlParameter("@ObjectionFiledDate", (object?)m.ObjectionFiledDate ?? DBNull.Value),

                new SqlParameter("@ClosureDate", (object?)m.ClosureDate ?? DBNull.Value),
                new SqlParameter("@ClosureRemark", (object?)m.ClosureRemark ?? DBNull.Value),
                new SqlParameter("@ClosureNumber", (object?)m.ClosureNumber ?? DBNull.Value),
                new SqlParameter("@OutwardNumber", (object?)m.OutwardNumber ?? DBNull.Value),
                new SqlParameter("@OutwardDate", (object?)m.OutwardDate ?? DBNull.Value),
                new SqlParameter("@InitialOutwardNumber", (object?)m.InitialOutwardNumber ?? DBNull.Value),
                new SqlParameter("@InitialOutwardDate", (object?)m.InitialOutwardDate ?? DBNull.Value),
                new SqlParameter("@VehicleNumber", (object?)m.VehicleNumber ?? DBNull.Value),
                new SqlParameter("@DateOfAccident", (object?)m.DateOfAccident ?? DBNull.Value),
                new SqlParameter("@ClaimAmount", (object?)m.ClaimAmount ?? DBNull.Value),
                new SqlParameter("@IsVehicleInvolved", (object?)m.IsVehicleInvolved ?? DBNull.Value),
                new SqlParameter("@VehicleType", (object?)m.VehicleType ?? DBNull.Value),
                new SqlParameter("@IsCorporationEmployee", (object?)m.IsCorporationEmployee ?? DBNull.Value),
                new SqlParameter("@MannerOfIncident", (object?)m.MannerOfIncident ?? DBNull.Value),
                new SqlParameter("@ClaimRemarks", (object?)m.ClaimRemarks ?? DBNull.Value),
                new SqlParameter("@CreatedBy", (object?)m.CreatedBy ?? DBNull.Value),
                new SqlParameter("@ModifiedBy", (object?)m.ModifiedBy ?? DBNull.Value),

                // Appeal Fields
                new SqlParameter("@AppealNumber", (object?)m.AppealNumber ?? DBNull.Value),
                new SqlParameter("@AppealYear", (object?)m.AppealYear ?? DBNull.Value),
                new SqlParameter("@ArisingOutOSNumber", (object?)m.ArisingOutOSNumber ?? DBNull.Value),
                new SqlParameter("@OSYear", (object?)m.OSYear ?? DBNull.Value),
                new SqlParameter("@AppealEntrustmentNumber", (object?)m.AppealEntrustmentNumber ?? DBNull.Value),
                new SqlParameter("@AppealEntrustmentDate", (object?)m.AppealEntrustmentDate ?? DBNull.Value),
                new SqlParameter("@CourtAppellateAuth", (object?)m.CourtAppellateAuth ?? DBNull.Value),
                new SqlParameter("@AppealAdvocateName", (object?)m.AppealAdvocateName ?? DBNull.Value),
                new SqlParameter("@AppealComplianceAmount", (object?)m.AppealComplianceAmount ?? DBNull.Value),
                new SqlParameter("@AppealChequeNumber", (object?)m.AppealChequeNumber ?? DBNull.Value),
                new SqlParameter("@AppealChequeDate", (object?)m.AppealChequeDate ?? DBNull.Value),
                new SqlParameter("@AppealAwardDetails", (object?)m.AppealAwardDetails ?? DBNull.Value),
                new SqlParameter("@AppealCaseDisposedOn", (object?)m.AppealCaseDisposedOn ?? DBNull.Value),
                new SqlParameter("@AppealCopyAppliedOn", (object?)m.AppealCopyAppliedOn ?? DBNull.Value),
                new SqlParameter("@AppealCopyReadyOn", (object?)m.AppealCopyReadyOn ?? DBNull.Value),
                new SqlParameter("@AppealCopyDeliveredOn", (object?)m.AppealCopyDeliveredOn ?? DBNull.Value),
                new SqlParameter("@AppealCopyReceivedAtDivision", (object?)m.AppealCopyReceivedAtDivision ?? DBNull.Value),
                new SqlParameter("@AppealDelayRemarks", (object?)m.AppealDelayRemarks ?? DBNull.Value),
                new SqlParameter("@AppealAdvocateOpinion", (object?)m.AppealAdvocateOpinion ?? DBNull.Value),
                new SqlParameter("@AppealALOOpinion", (object?)m.AppealALOOpinion ?? DBNull.Value),
                new SqlParameter("@AppealDCOpinion", (object?)m.AppealDCOpinion ?? DBNull.Value),
                new SqlParameter("@AppealForwardingStatus", (object?)m.AppealForwardingStatus ?? DBNull.Value),
                new SqlParameter("@AppealOutwardNumber", (object?)m.AppealOutwardNumber ?? DBNull.Value),
                new SqlParameter("@AppealOutwardDate", (object?)m.AppealOutwardDate ?? DBNull.Value),
                new SqlParameter("@AppealClosureNumber", (object?)m.AppealClosureNumber ?? DBNull.Value),
                new SqlParameter("@AppealClosureDate", (object?)m.AppealClosureDate ?? DBNull.Value),
                new SqlParameter("@AppealJudgmentCopyPath", (object?)m.AppealJudgmentCopyPath ?? DBNull.Value),

                // e-Courts NAPIX Parameters
                new SqlParameter("@CNRNumber", (object?)m.CNRNumber ?? DBNull.Value),
                new SqlParameter("@EstCode", (object?)m.EstCode ?? DBNull.Value),
                new SqlParameter("@CaseTypeCode", (object?)m.CaseTypeCode ?? DBNull.Value),
                new SqlParameter("@OtherCourtDetails", (object?)m.OtherCourtDetails ?? DBNull.Value),
                new SqlParameter("@LastNapixSyncAt", (object?)m.LastNapixSyncAt ?? DBNull.Value),
                new SqlParameter("@LastNapixSyncStatus", (object?)m.LastNapixSyncStatus ?? DBNull.Value),
                new SqlParameter("@LastNapixSyncError", (object?)m.LastNapixSyncError ?? DBNull.Value),
                new SqlParameter("@NapixSyncAttemptCount", m.NapixSyncAttemptCount),
                new SqlParameter("@NapixDataHash", (object?)m.NapixDataHash ?? DBNull.Value),
                new SqlParameter("@PendDispStatus", (object?)m.PendDispStatus ?? DBNull.Value),
                new SqlParameter("@EstName", (object?)m.EstName ?? DBNull.Value),
                new SqlParameter("@ECourtsStage", (object?)m.ECourtsStage ?? DBNull.Value),
                new SqlParameter("@ECourtsCourtNo", (object?)m.ECourtsCourtNo ?? DBNull.Value),
                new SqlParameter("@ECourtsJudge", (object?)m.ECourtsJudge ?? DBNull.Value),
            };

            if (includeId)
            {
                list.Add(new SqlParameter("@CaseID", m.CaseID));
            }

            return list.ToArray();
        }

        public DashboardStatsViewModel GetDashboardStats(int divisionId, string caseType, string litigantType)
        {
            var stats = new DashboardStatsViewModel();
            var allCases = GetAllCases(divisionId, caseType, litigantType).ToList();

            stats.TotalCases = allCases.Count;

            bool IsClosed(OtherCourtsCase c)
            {
                var ds = (c.DisposalStatus ?? "").Trim().ToLower();
                var cs = (c.CaseStage ?? "").Trim().ToLower();
                var cstat = (c.CaseStatus ?? "").Trim().ToLower();
                return ds.Contains("disposed") || ds.Contains("closed") || ds.Contains("dnp") || ds.Contains("decreed")
                    || cs.Contains("disposed") || cs.Contains("closed") || cs.Contains("dnp")
                    || cstat.Contains("disposed") || cstat.Contains("closed") || c.ClosureDate.HasValue;
            }

            stats.CloseCount = allCases.Count(c => IsClosed(c));
            stats.PendingCases = allCases.Count - stats.CloseCount;
            stats.HearingsToday = allCases.Count(c => c.NextDateOfHearing.HasValue && c.NextDateOfHearing.Value.Date == DateTime.Today);
            stats.ComplianceDueCount = allCases.Count(c => !IsClosed(c) && c.NextDateOfHearing.HasValue && c.NextDateOfHearing.Value.Date < DateTime.Today);

            // Fetch upcoming hearings first; if none, show most recent cases so dashboard is never empty
            var upcoming = allCases
                .Where(c => !IsClosed(c) && c.NextDateOfHearing.HasValue && c.NextDateOfHearing.Value.Date >= DateTime.Today)
                .OrderBy(c => c.NextDateOfHearing)
                .Take(10)
                .ToList();

            if (!upcoming.Any())
            {
                upcoming = allCases
                    .OrderByDescending(c => c.CreatedDate)
                    .Take(10)
                    .ToList();
            }

            stats.RecentCases = upcoming.Select(c => new RecentCaseViewModel
            {
                CaseID = c.CaseID,
                MVCNo = !string.IsNullOrEmpty(c.CaseNumber) ? $"{c.CaseNumber}/{(c.CaseYear.HasValue ? c.CaseYear.ToString() : "")}" : "NOT FILED",
                MACTName = c.Court ?? "N/A",
                DisposalResult = IsClosed(c) ? (c.DisposalStatus ?? "Disposed") : (c.CaseStage ?? "Pending"),
                CreatedAt = c.CreatedDate,
                NextHearingDate = c.NextDateOfHearing
            }).ToList();

            return stats;
        }

        private OtherCourtsCase MapToModel(DataRow row)
        {
            return new OtherCourtsCase
            {
                CaseID = row.GetInt("CaseID") ?? 0,
                DivisionID = row.GetInt("DivisionID") ?? 0,
                DivisionName = row.GetString("DivisionName"),
                CaseType = row.GetString("CaseType") ?? "",
                LitigantType = row.GetString("LitigantType") ?? "",
                IsPendingForFiling = row.GetBool("IsPendingForFiling"),
                CaseNumber = row.GetString("CaseNumber"),
                CaseYear = row.GetInt("CaseYear"),
                Court = row.GetString("Court"),
                CaseNature = row.GetString("CaseNature"),
                ClaimDetails = row.GetString("ClaimDetails"),
                RespondentName = row.GetString("RespondentName"),
                EntrustmentNumber = row.GetString("EntrustmentNumber"),
                EntrustmentDate = row.GetDate("EntrustmentDate"),
                AdvocateName = row.GetString("AdvocateName"),
                EvidenceFiled = row.GetString("EvidenceFiled"),
                CaseStatus = row.GetString("CaseStatus"),
                NextDateOfHearing = row.GetDate("NextDateOfHearing"),
                InterimOrder = row.GetString("InterimOrder"),
                InterimOrderFilePath = row.GetString("InterimOrderFilePath"),
                AwardDetails = row.GetString("AwardDetails"),
                Result = row.GetString("Result"),
                DisposalStatus = row.GetString("DisposalStatus"),
                
                // CC Details
                CopyDisposedOn = row.GetDate("CopyDisposedOn"),
                CopyAppliedOn = row.GetDate("CopyAppliedOn"),
                CopyReadyOn = row.GetDate("CopyReadyOn"),
                CopyDeliveredOn = row.GetDate("CopyDeliveredOn"),
                CopyReceivedAtDivision = row.GetDate("CopyReceivedAtDivision"),
                CertifiedCopyRemarks = row.GetString("CertifiedCopyRemarks"),

                AdvocateOpinion = row.GetString("AdvocateOpinion"),
                LawOfficerOpinion = row.GetString("LawOfficerOpinion"),
                DCFinalDecision = row.GetString("DCFinalDecision"),
                FinalForwardingStatus = row.GetString("FinalForwardingStatus"),
                FinalJudgmentFilePath = row.GetString("FinalJudgmentFilePath"),

                PetitionerName = row.GetString("PetitionerName"),
                PetitionerRelationship = row.GetString("PetitionerRelationship"),
                ClaimType = row.GetString("ClaimType"),
                DateOfClaimPetition = row.GetDate("DateOfClaimPetition"),
                CaseStage = row.GetString("CaseStage"),
                DocumentSent = row.GetString("DocumentSent"),
                ObjectionVerified = row.GetString("ObjectionVerified"),
                IsObjectionFiled = row.GetString("IsObjectionFiled"),
                ObjectionPending = row.GetString("ObjectionPending"),
                ObjectionRemarks = row.GetString("ObjectionRemarks"),
                ObjectionOutwardNumber = row.GetString("ObjectionOutwardNumber"),
                ObjectionFiledDate = row.GetDate("ObjectionFiledDate"),

                ClosureDate = row.GetDate("ClosureDate"),
                ClosureNumber = row.GetString("ClosureNumber"),
                ClosureRemark = row.GetString("ClosureRemark"),
                OutwardNumber = row.GetString("OutwardNumber"),
                OutwardDate = row.GetDate("OutwardDate"),
                InitialOutwardNumber = row.GetString("InitialOutwardNumber"),
                InitialOutwardDate = row.GetDate("InitialOutwardDate"),
                VehicleNumber = row.GetString("VehicleNumber"),
                DateOfAccident = row.GetDate("DateOfAccident"),
                ClaimAmount = row.GetDecimal("ClaimAmount"),
                IsVehicleInvolved = row.GetString("IsVehicleInvolved"),
                VehicleType = row.GetString("VehicleType"),
                IsCorporationEmployee = row.GetString("IsCorporationEmployee"),
                MannerOfIncident = row.GetString("MannerOfIncident"),
                ClaimRemarks = row.GetString("ClaimRemarks"),
                CreatedBy = row.GetInt("CreatedBy"),
                CreatedDate = row.GetDate("CreatedDate") ?? DateTime.Now,
                ModifiedBy = row.GetInt("ModifiedBy"),
                ModifiedDate = row.GetDate("ModifiedDate"),

                // Appeal Fields
                AppealNumber = row.GetString("AppealNumber"),
                AppealYear = row.GetInt("AppealYear"),
                ArisingOutOSNumber = row.GetString("ArisingOutOSNumber"),
                OSYear = row.GetInt("OSYear"),
                AppealEntrustmentNumber = row.GetString("AppealEntrustmentNumber"),
                AppealEntrustmentDate = row.GetDate("AppealEntrustmentDate"),
                CourtAppellateAuth = row.GetString("CourtAppellateAuth"),
                AppealAdvocateName = row.GetString("AppealAdvocateName"),
                AppealComplianceAmount = row.GetDecimal("AppealComplianceAmount"),
                AppealChequeNumber = row.GetString("AppealChequeNumber"),
                AppealChequeDate = row.GetDate("AppealChequeDate"),
                AppealAwardDetails = row.GetString("AppealAwardDetails"),
                AppealCaseDisposedOn = row.GetDate("AppealCaseDisposedOn"),
                AppealCopyAppliedOn = row.GetDate("AppealCopyAppliedOn"),
                AppealCopyReadyOn = row.GetDate("AppealCopyReadyOn"),
                AppealCopyDeliveredOn = row.GetDate("AppealCopyDeliveredOn"),
                AppealCopyReceivedAtDivision = row.GetDate("AppealCopyReceivedAtDivision"),
                AppealDelayRemarks = row.GetString("AppealDelayRemarks"),
                AppealAdvocateOpinion = row.GetString("AppealAdvocateOpinion"),
                AppealALOOpinion = row.GetString("AppealALOOpinion"),
                AppealDCOpinion = row.GetString("AppealDCOpinion"),
                AppealForwardingStatus = row.GetString("AppealForwardingStatus"),
                AppealOutwardNumber = row.GetString("AppealOutwardNumber"),
                AppealOutwardDate = row.GetDate("AppealOutwardDate"),
                AppealClosureNumber = row.GetString("AppealClosureNumber"),
                AppealClosureDate = row.GetDate("AppealClosureDate"),
                AppealJudgmentCopyPath = row.GetString("AppealJudgmentCopyPath"),

                // e-Courts NAPIX Fields
                CNRNumber = row.GetString("CNRNumber"),
                EstCode = row.GetString("EstCode"),
                CaseTypeCode = row.GetString("CaseTypeCode"),
                OtherCourtDetails = row.GetString("OtherCourtDetails"),
                LastNapixSyncAt = row.GetDate("LastNapixSyncAt"),
                LastNapixSyncStatus = row.GetString("LastNapixSyncStatus"),
                LastNapixSyncError = row.GetString("LastNapixSyncError"),
                NapixSyncAttemptCount = row.GetInt("NapixSyncAttemptCount") ?? 0,
                NapixDataHash = row.GetString("NapixDataHash"),
                PendDispStatus = row.GetString("PendDispStatus"),
                EstName = row.GetString("EstName"),
                ECourtsStage = row.GetString("ECourtsStage"),
                ECourtsCourtNo = row.GetString("ECourtsCourtNo"),
                ECourtsJudge = row.GetString("ECourtsJudge"),
            };
        }

        public bool UpdateLiveSyncInfo(int caseId, string cnr, string status, string? error, DateTime? nextDate, string? stage, string? courtNo, string? judge, string? pendDisp, string? estName)
        {
            string sql = @"
                UPDATE OTHER_CASES
                SET LastNapixSyncAt = SYSUTCDATETIME(),
                    LastNapixSyncStatus = @Status,
                    LastNapixSyncError = @Error,
                    NapixSyncAttemptCount = NapixSyncAttemptCount + 1,
                    PendDispStatus = COALESCE(@PendDisp, PendDispStatus),
                    NextDateOfHearing = COALESCE(@NextDate, NextDateOfHearing),
                    EstName = COALESCE(@EstName, EstName),
                    ECourtsStage = COALESCE(@Stage, ECourtsStage),
                    ECourtsCourtNo = COALESCE(@CourtNo, ECourtsCourtNo),
                    ECourtsJudge = COALESCE(@Judge, ECourtsJudge),
                    CNRNumber = COALESCE(@CNR, CNRNumber)
                WHERE CaseID = @CaseID;";

            var parameters = new[]
            {
                new SqlParameter("@CaseID", caseId),
                new SqlParameter("@Status", status),
                new SqlParameter("@Error", (object?)error ?? DBNull.Value),
                new SqlParameter("@PendDisp", (object?)pendDisp ?? DBNull.Value),
                new SqlParameter("@NextDate", (object?)nextDate ?? DBNull.Value),
                new SqlParameter("@EstName", (object?)estName ?? DBNull.Value),
                new SqlParameter("@Stage", (object?)stage ?? DBNull.Value),
                new SqlParameter("@CourtNo", (object?)courtNo ?? DBNull.Value),
                new SqlParameter("@Judge", (object?)judge ?? DBNull.Value),
                new SqlParameter("@CNR", (object?)cnr ?? DBNull.Value)
            };

            return _db.ExecuteNonQuery(sql, parameters) > 0;
        }

        public bool DeleteCase(int caseId)
        {
            using (var conn = new SqlConnection(_db.GetConnectionString()))
            {
                conn.Open();
                using (var trans = conn.BeginTransaction())
                {
                    try
                    {
                        string[] tables = { 
                            "OTHER_CASE_PETITIONERS", 
                            "OTHER_CASE_RESPONDENTS", 
                            "OTHER_CASE_EVIDENCE_RESPONDENT", 
                            "OTHER_CASE_EVIDENCE_CORP", 
                            "OTHER_CASE_DOCUMENTS" 
                        };
                        foreach (var tbl in tables)
                        {
                            using (var cmd = new SqlCommand($"IF OBJECT_ID('{tbl}', 'U') IS NOT NULL DELETE FROM {tbl} WHERE CaseID = @Id", conn, trans))
                            {
                                cmd.Parameters.AddWithValue("@Id", caseId);
                                cmd.ExecuteNonQuery();
                            }
                        }
                        using (var notingCmd = new SqlCommand("IF OBJECT_ID('CASE_NOTINGS', 'U') IS NOT NULL DELETE FROM CASE_NOTINGS WHERE CaseType IN ('OTHER', 'OS', 'ECA', 'CC', 'PSC', 'LAC', 'Consumer') AND CaseID = @Id", conn, trans))
                        {
                            notingCmd.Parameters.AddWithValue("@Id", caseId);
                            notingCmd.ExecuteNonQuery();
                        }
                        int affected;
                        using (var delCmd = new SqlCommand("DELETE FROM OTHER_CASES WHERE CaseID = @Id", conn, trans))
                        {
                            delCmd.Parameters.AddWithValue("@Id", caseId);
                            affected = delCmd.ExecuteNonQuery();
                        }
                        trans.Commit();
                        return affected > 0;
                    }
                    catch
                    {
                        trans.Rollback();
                        throw;
                    }
                }
            }
        }
    }
}
