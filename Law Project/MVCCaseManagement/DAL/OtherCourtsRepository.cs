using Microsoft.Data.SqlClient;
using MVCCaseManagement.Models;
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
                    @AppealAwardDetails, @AppealCaseDisposedOn, @AppealCopyAppliedOn, @CopyReadyOn, @AppealCopyDeliveredOn, 
                    @AppealCopyReceivedAtDivision, @AppealDelayRemarks, @AppealAdvocateOpinion, @AppealALOOpinion, @AppealDCOpinion, 
                    @AppealForwardingStatus, @AppealOutwardNumber, @AppealOutwardDate, @AppealClosureNumber, @AppealClosureDate, 
                    @AppealJudgmentCopyPath,
                    @CreatedBy, GETDATE()
                );
                SELECT CAST(SCOPE_IDENTITY() AS INT);";

            var parameters = BuildParameters(model);
            var result = _db.ExecuteScalar(query, parameters);
            int caseId = result != null ? Convert.ToInt32(result) : 0;

            if (caseId > 0)
            {
                // Petitioners
                if (model.Petitioners != null)
                {
                    foreach (var pet in model.Petitioners)
                    {
                        if (!string.IsNullOrEmpty(pet.Name))
                        {
                            string petQuery = "INSERT INTO OTHER_CASE_PETITIONERS (CaseID, PetitionerName, PetitionerRemark) VALUES (@CaseID, @Name, @Remark)";
                            _db.ExecuteNonQuery(petQuery, new[] { 
                                new SqlParameter("@CaseID", caseId), 
                                new SqlParameter("@Name", pet.Name),
                                new SqlParameter("@Remark", (object?)pet.Remark ?? DBNull.Value)
                            });
                        }
                    }
                }

                // Respondents
                if (model.Respondents != null)
                {
                    foreach (var resp in model.Respondents)
                    {
                        if (!string.IsNullOrEmpty(resp.Name))
                        {
                            string respQuery = "INSERT INTO OTHER_CASE_RESPONDENTS (CaseID, RespondentName, RespondentRemark) VALUES (@CaseID, @Name, @Remark)";
                            _db.ExecuteNonQuery(respQuery, new[] { 
                                new SqlParameter("@CaseID", caseId), 
                                new SqlParameter("@Name", resp.Name),
                                new SqlParameter("@Remark", (object?)resp.Remark ?? DBNull.Value)
                            });
                        }
                    }
                }

                // Respondent Evidence
                if (model.RespondentEvidence != null)
                {
                    foreach (var resp in model.RespondentEvidence)
                    {
                        if (!string.IsNullOrEmpty(resp.Name))
                        {
                            string q = "INSERT INTO OTHER_CASE_EVIDENCE_RESPONDENT (CaseID, Name, Remark) VALUES (@CaseID, @Name, @Remark)";
                            _db.ExecuteNonQuery(q, new[] { 
                                new SqlParameter("@CaseID", caseId), 
                                new SqlParameter("@Name", resp.Name),
                                new SqlParameter("@Remark", (object?)resp.Remark ?? DBNull.Value)
                            });
                        }
                    }
                }

                // Corp Evidence
                if (model.CorpEvidence != null)
                {
                    foreach (var corp in model.CorpEvidence)
                    {
                        if (!string.IsNullOrEmpty(corp.Name))
                        {
                            string q = "INSERT INTO OTHER_CASE_EVIDENCE_CORP (CaseID, Name, Designation) VALUES (@CaseID, @Name, @Designation)";
                            _db.ExecuteNonQuery(q, new[] { 
                                new SqlParameter("@CaseID", caseId), 
                                new SqlParameter("@Name", corp.Name),
                                new SqlParameter("@Designation", (object?)corp.Designation ?? DBNull.Value)
                            });
                        }
                    }
                }

                // Enclosed Documents
                if (model.EnclosedDocuments != null)
                {
                    foreach (var doc in model.EnclosedDocuments)
                    {
                        if (!string.IsNullOrEmpty(doc.DocName))
                        {
                            string q = "INSERT INTO OTHER_CASE_DOCUMENTS (CaseID, DocName, PageCount) VALUES (@CaseID, @DocName, @PageCount)";
                            _db.ExecuteNonQuery(q, new[] { 
                                new SqlParameter("@CaseID", caseId), 
                                new SqlParameter("@DocName", doc.DocName),
                                new SqlParameter("@PageCount", (object?)doc.PageCount ?? DBNull.Value)
                            });
                        }
                    }
                }
            }

            if (caseId > 0 && string.IsNullOrEmpty(model.PetitionerName) && model.Petitioners != null && model.Petitioners.Any())
            {
                model.PetitionerName = model.Petitioners[0].Name;
                _db.ExecuteNonQuery("UPDATE OTHER_CASES SET PetitionerName = @PetName WHERE CaseID = @CaseID", new[] {
                    new SqlParameter("@CaseID", caseId),
                    new SqlParameter("@PetName", (object?)model.PetitionerName ?? DBNull.Value)
                });
            }

            return caseId;
        }

        public bool UpdateCase(OtherCourtsCase model)
        {
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
                    ModifiedBy = @ModifiedBy,
                    ModifiedDate = GETDATE()
                WHERE CaseID = @CaseID";

            var parameters = BuildParameters(model, includeId: true);
            bool success = _db.ExecuteNonQuery(query, parameters) > 0;

            if (success)
            {
                // Petitioners (only synchronize if collection is provided)
                if (model.Petitioners != null)
                {
                    _db.ExecuteNonQuery("DELETE FROM OTHER_CASE_PETITIONERS WHERE CaseID = @CaseID", new[] { new SqlParameter("@CaseID", model.CaseID) });
                    foreach (var pet in model.Petitioners)
                    {
                        if (!string.IsNullOrEmpty(pet.Name))
                        {
                            string petQuery = "INSERT INTO OTHER_CASE_PETITIONERS (CaseID, PetitionerName, PetitionerRemark) VALUES (@CaseID, @Name, @Remark)";
                            _db.ExecuteNonQuery(petQuery, new[] { 
                                new SqlParameter("@CaseID", model.CaseID), 
                                new SqlParameter("@Name", pet.Name),
                                new SqlParameter("@Remark", (object?)pet.Remark ?? DBNull.Value)
                            });
                        }
                    }
                }
                
                // Respondents (only synchronize if collection is provided)
                if (model.Respondents != null)
                {
                    _db.ExecuteNonQuery("DELETE FROM OTHER_CASE_RESPONDENTS WHERE CaseID = @CaseID", new[] { new SqlParameter("@CaseID", model.CaseID) });
                    foreach (var resp in model.Respondents)
                    {
                        if (!string.IsNullOrEmpty(resp.Name))
                        {
                            string respQuery = "INSERT INTO OTHER_CASE_RESPONDENTS (CaseID, RespondentName, RespondentRemark) VALUES (@CaseID, @Name, @Remark)";
                            _db.ExecuteNonQuery(respQuery, new[] { 
                                new SqlParameter("@CaseID", model.CaseID), 
                                new SqlParameter("@Name", resp.Name),
                                new SqlParameter("@Remark", (object?)resp.Remark ?? DBNull.Value)
                            });
                        }
                    }
                }

                // Respondent Evidence (only synchronize if collection is provided)
                if (model.RespondentEvidence != null)
                {
                    _db.ExecuteNonQuery("DELETE FROM OTHER_CASE_EVIDENCE_RESPONDENT WHERE CaseID = @CaseID", new[] { new SqlParameter("@CaseID", model.CaseID) });
                    foreach (var resp in model.RespondentEvidence)
                    {
                        if (!string.IsNullOrEmpty(resp.Name))
                        {
                            string q = "INSERT INTO OTHER_CASE_EVIDENCE_RESPONDENT (CaseID, Name, Remark) VALUES (@CaseID, @Name, @Remark)";
                            _db.ExecuteNonQuery(q, new[] { 
                                new SqlParameter("@CaseID", model.CaseID), 
                                new SqlParameter("@Name", resp.Name),
                                new SqlParameter("@Remark", (object?)resp.Remark ?? DBNull.Value)
                            });
                        }
                    }
                }

                // Corp Evidence (only synchronize if collection is provided)
                if (model.CorpEvidence != null)
                {
                    _db.ExecuteNonQuery("DELETE FROM OTHER_CASE_EVIDENCE_CORP WHERE CaseID = @CaseID", new[] { new SqlParameter("@CaseID", model.CaseID) });
                    foreach (var corp in model.CorpEvidence)
                    {
                        if (!string.IsNullOrEmpty(corp.Name))
                        {
                            string q = "INSERT INTO OTHER_CASE_EVIDENCE_CORP (CaseID, Name, Designation) VALUES (@CaseID, @Name, @Designation)";
                            _db.ExecuteNonQuery(q, new[] { 
                                new SqlParameter("@CaseID", model.CaseID), 
                                new SqlParameter("@Name", corp.Name),
                                new SqlParameter("@Designation", (object?)corp.Designation ?? DBNull.Value)
                            });
                        }
                    }
                }

                // Enclosed Documents (only synchronize if collection is provided)
                if (model.EnclosedDocuments != null)
                {
                    _db.ExecuteNonQuery("DELETE FROM OTHER_CASE_DOCUMENTS WHERE CaseID = @CaseID", new[] { new SqlParameter("@CaseID", model.CaseID) });
                    foreach (var doc in model.EnclosedDocuments)
                    {
                        if (!string.IsNullOrEmpty(doc.DocName))
                        {
                            string q = "INSERT INTO OTHER_CASE_DOCUMENTS (CaseID, DocName, PageCount) VALUES (@CaseID, @DocName, @PageCount)";
                            _db.ExecuteNonQuery(q, new[] { 
                                new SqlParameter("@CaseID", model.CaseID), 
                                new SqlParameter("@DocName", doc.DocName),
                                new SqlParameter("@PageCount", (object?)doc.PageCount ?? DBNull.Value)
                            });
                        }
                    }
                }
            }

            if (success && string.IsNullOrEmpty(model.PetitionerName) && model.Petitioners != null && model.Petitioners.Any())
            {
                model.PetitionerName = model.Petitioners[0].Name;
                _db.ExecuteNonQuery("UPDATE OTHER_CASES SET PetitionerName = @PetName WHERE CaseID = @CaseID", new[] {
                    new SqlParameter("@CaseID", model.CaseID),
                    new SqlParameter("@PetName", (object?)model.PetitionerName ?? DBNull.Value)
                });
            }

            return success;
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

            if (!string.IsNullOrEmpty(caseType))
            {
                whereClause += " AND c.CaseType = @CaseType";
                paramsList.Add(new SqlParameter("@CaseType", caseType));
            }

            if (!string.IsNullOrEmpty(litigantType))
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
            stats.PendingCases = allCases.Count(c => c.CaseStage?.ToLower() == "pending");
            stats.CloseCount = allCases.Count(c => c.CaseStage?.ToLower() == "disposed");
            stats.HearingsToday = allCases.Count(c => c.NextDateOfHearing?.Date == DateTime.Today);
            stats.ComplianceDueCount = allCases.Count(c => c.CaseStage?.ToLower() == "pending" && c.NextDateOfHearing < DateTime.Today);

            stats.RecentCases = allCases
                .Where(c => c.NextDateOfHearing >= DateTime.Today)
                .OrderBy(c => c.NextDateOfHearing)
                .Take(10)
                .Select(c => new RecentCaseViewModel
                {
                    CaseID = c.CaseID,
                    MVCNo = c.CaseNumber ?? "N/A",
                    MACTName = c.Court ?? "N/A",
                    DisposalResult = c.CaseStage ?? "Pending",
                    CreatedAt = c.CreatedDate,
                    NextHearingDate = c.NextDateOfHearing
                }).ToList();

            return stats;
        }

        private OtherCourtsCase MapToModel(DataRow row)
        {
            return new OtherCourtsCase
            {
                CaseID = row["CaseID"] != DBNull.Value ? Convert.ToInt32(row["CaseID"]) : 0,
                DivisionID = row["DivisionID"] != DBNull.Value ? Convert.ToInt32(row["DivisionID"]) : 0,
                DivisionName = row["DivisionName"]?.ToString(),
                CaseType = row["CaseType"] != DBNull.Value ? row["CaseType"].ToString() ?? "" : "",
                LitigantType = row["LitigantType"] != DBNull.Value ? row["LitigantType"].ToString() ?? "" : "",
                IsPendingForFiling = row["IsPendingForFiling"] != DBNull.Value && Convert.ToBoolean(row["IsPendingForFiling"]),
                CaseNumber = row["CaseNumber"]?.ToString(),
                CaseYear = row["CaseYear"] != DBNull.Value ? Convert.ToInt32(row["CaseYear"]) : null,
                Court = row["Court"]?.ToString(),
                CaseNature = row["CaseNature"]?.ToString(),
                ClaimDetails = row["ClaimDetails"]?.ToString(),
                RespondentName = row["RespondentName"]?.ToString(),
                EntrustmentNumber = row["EntrustmentNumber"]?.ToString(),
                EntrustmentDate = row["EntrustmentDate"] != DBNull.Value ? Convert.ToDateTime(row["EntrustmentDate"]) : null,
                AdvocateName = row["AdvocateName"]?.ToString(),
                EvidenceFiled = row["EvidenceFiled"]?.ToString(),
                CaseStatus = row["CaseStatus"]?.ToString(),
                NextDateOfHearing = row["NextDateOfHearing"] != DBNull.Value ? Convert.ToDateTime(row["NextDateOfHearing"]) : null,
                InterimOrder = row["InterimOrder"]?.ToString(),
                InterimOrderFilePath = row["InterimOrderFilePath"]?.ToString(),
                AwardDetails = row["AwardDetails"]?.ToString(),
                Result = row["Result"]?.ToString(),
                DisposalStatus = row["DisposalStatus"]?.ToString(),
                
                // CC Details
                CopyDisposedOn = row["CopyDisposedOn"] != DBNull.Value ? Convert.ToDateTime(row["CopyDisposedOn"]) : null,
                CopyAppliedOn = row["CopyAppliedOn"] != DBNull.Value ? Convert.ToDateTime(row["CopyAppliedOn"]) : null,
                CopyReadyOn = row["CopyReadyOn"] != DBNull.Value ? Convert.ToDateTime(row["CopyReadyOn"]) : null,
                CopyDeliveredOn = row["CopyDeliveredOn"] != DBNull.Value ? Convert.ToDateTime(row["CopyDeliveredOn"]) : null,
                CopyReceivedAtDivision = row["CopyReceivedAtDivision"] != DBNull.Value ? Convert.ToDateTime(row["CopyReceivedAtDivision"]) : null,
                CertifiedCopyRemarks = row["CertifiedCopyRemarks"]?.ToString(),

                AdvocateOpinion = row["AdvocateOpinion"]?.ToString(),
                LawOfficerOpinion = row["LawOfficerOpinion"]?.ToString(),
                DCFinalDecision = row["DCFinalDecision"]?.ToString(),
                FinalForwardingStatus = row["FinalForwardingStatus"]?.ToString(),
                FinalJudgmentFilePath = row["FinalJudgmentFilePath"]?.ToString(),

                PetitionerName = row["PetitionerName"]?.ToString(),
                PetitionerRelationship = row["PetitionerRelationship"]?.ToString(),
                ClaimType = row["ClaimType"]?.ToString(),
                DateOfClaimPetition = row["DateOfClaimPetition"] != DBNull.Value ? Convert.ToDateTime(row["DateOfClaimPetition"]) : null,
                CaseStage = row["CaseStage"]?.ToString(),
                DocumentSent = row["DocumentSent"]?.ToString(),
                ObjectionVerified = row["ObjectionVerified"]?.ToString(),
                IsObjectionFiled = row["IsObjectionFiled"]?.ToString(),
                ObjectionPending = row["ObjectionPending"]?.ToString(),
                ObjectionRemarks = row["ObjectionRemarks"]?.ToString(),
                ObjectionOutwardNumber = row["ObjectionOutwardNumber"]?.ToString(),
                ObjectionFiledDate = row["ObjectionFiledDate"] != DBNull.Value ? Convert.ToDateTime(row["ObjectionFiledDate"]) : null,

                ClosureDate = row["ClosureDate"] != DBNull.Value ? Convert.ToDateTime(row["ClosureDate"]) : null,
                ClosureNumber = row["ClosureNumber"]?.ToString(),
                ClosureRemark = row["ClosureRemark"]?.ToString(),
                OutwardNumber = row["OutwardNumber"]?.ToString(),
                OutwardDate = row["OutwardDate"] != DBNull.Value ? Convert.ToDateTime(row["OutwardDate"]) : null,
                InitialOutwardNumber = row["InitialOutwardNumber"]?.ToString(),
                InitialOutwardDate = row["InitialOutwardDate"] != DBNull.Value ? Convert.ToDateTime(row["InitialOutwardDate"]) : null,
                VehicleNumber = row["VehicleNumber"]?.ToString(),
                DateOfAccident = row["DateOfAccident"] != DBNull.Value ? Convert.ToDateTime(row["DateOfAccident"]) : null,
                ClaimAmount = row["ClaimAmount"] != DBNull.Value ? Convert.ToDecimal(row["ClaimAmount"]) : null,
                IsVehicleInvolved = row.Table.Columns.Contains("IsVehicleInvolved") ? row["IsVehicleInvolved"]?.ToString() : null,
                VehicleType = row.Table.Columns.Contains("VehicleType") ? row["VehicleType"]?.ToString() : null,
                IsCorporationEmployee = row.Table.Columns.Contains("IsCorporationEmployee") ? row["IsCorporationEmployee"]?.ToString() : null,
                MannerOfIncident = row.Table.Columns.Contains("MannerOfIncident") ? row["MannerOfIncident"]?.ToString() : null,
                ClaimRemarks = row.Table.Columns.Contains("ClaimRemarks") ? row["ClaimRemarks"]?.ToString() : null,
                CreatedBy = row["CreatedBy"] != DBNull.Value ? Convert.ToInt32(row["CreatedBy"]) : null,
                CreatedDate = row["CreatedDate"] != DBNull.Value ? Convert.ToDateTime(row["CreatedDate"]) : DateTime.Now,
                ModifiedBy = row["ModifiedBy"] != DBNull.Value ? Convert.ToInt32(row["ModifiedBy"]) : null,
                ModifiedDate = row["ModifiedDate"] != DBNull.Value ? Convert.ToDateTime(row["ModifiedDate"]) : null,

                // Appeal Fields
                AppealNumber = row.Table.Columns.Contains("AppealNumber") ? row["AppealNumber"]?.ToString() : null,
                AppealYear = row.Table.Columns.Contains("AppealYear") && row["AppealYear"] != DBNull.Value ? Convert.ToInt32(row["AppealYear"]) : null,
                ArisingOutOSNumber = row.Table.Columns.Contains("ArisingOutOSNumber") ? row["ArisingOutOSNumber"]?.ToString() : null,
                OSYear = row.Table.Columns.Contains("OSYear") && row["OSYear"] != DBNull.Value ? Convert.ToInt32(row["OSYear"]) : null,
                AppealEntrustmentNumber = row.Table.Columns.Contains("AppealEntrustmentNumber") ? row["AppealEntrustmentNumber"]?.ToString() : null,
                AppealEntrustmentDate = row.Table.Columns.Contains("AppealEntrustmentDate") && row["AppealEntrustmentDate"] != DBNull.Value ? Convert.ToDateTime(row["AppealEntrustmentDate"]) : null,
                CourtAppellateAuth = row.Table.Columns.Contains("CourtAppellateAuth") ? row["CourtAppellateAuth"]?.ToString() : null,
                AppealAdvocateName = row.Table.Columns.Contains("AppealAdvocateName") ? row["AppealAdvocateName"]?.ToString() : null,
                AppealComplianceAmount = row.Table.Columns.Contains("AppealComplianceAmount") && row["AppealComplianceAmount"] != DBNull.Value ? Convert.ToDecimal(row["AppealComplianceAmount"]) : null,
                AppealChequeNumber = row.Table.Columns.Contains("AppealChequeNumber") ? row["AppealChequeNumber"]?.ToString() : null,
                AppealChequeDate = row.Table.Columns.Contains("AppealChequeDate") && row["AppealChequeDate"] != DBNull.Value ? Convert.ToDateTime(row["AppealChequeDate"]) : null,
                AppealAwardDetails = row.Table.Columns.Contains("AppealAwardDetails") ? row["AppealAwardDetails"]?.ToString() : null,
                AppealCaseDisposedOn = row.Table.Columns.Contains("AppealCaseDisposedOn") && row["AppealCaseDisposedOn"] != DBNull.Value ? Convert.ToDateTime(row["AppealCaseDisposedOn"]) : null,
                AppealCopyAppliedOn = row.Table.Columns.Contains("AppealCopyAppliedOn") && row["AppealCopyAppliedOn"] != DBNull.Value ? Convert.ToDateTime(row["AppealCopyAppliedOn"]) : null,
                AppealCopyReadyOn = row.Table.Columns.Contains("AppealCopyReadyOn") && row["AppealCopyReadyOn"] != DBNull.Value ? Convert.ToDateTime(row["AppealCopyReadyOn"]) : null,
                AppealCopyDeliveredOn = row.Table.Columns.Contains("AppealCopyDeliveredOn") && row["AppealCopyDeliveredOn"] != DBNull.Value ? Convert.ToDateTime(row["AppealCopyDeliveredOn"]) : null,
                AppealCopyReceivedAtDivision = row.Table.Columns.Contains("AppealCopyReceivedAtDivision") && row["AppealCopyReceivedAtDivision"] != DBNull.Value ? Convert.ToDateTime(row["AppealCopyReceivedAtDivision"]) : null,
                AppealDelayRemarks = row.Table.Columns.Contains("AppealDelayRemarks") ? row["AppealDelayRemarks"]?.ToString() : null,
                AppealAdvocateOpinion = row.Table.Columns.Contains("AppealAdvocateOpinion") ? row["AppealAdvocateOpinion"]?.ToString() : null,
                AppealALOOpinion = row.Table.Columns.Contains("AppealALOOpinion") ? row["AppealALOOpinion"]?.ToString() : null,
                AppealDCOpinion = row.Table.Columns.Contains("AppealDCOpinion") ? row["AppealDCOpinion"]?.ToString() : null,
                AppealForwardingStatus = row.Table.Columns.Contains("AppealForwardingStatus") ? row["AppealForwardingStatus"]?.ToString() : null,
                AppealOutwardNumber = row.Table.Columns.Contains("AppealOutwardNumber") ? row["AppealOutwardNumber"]?.ToString() : null,
                AppealOutwardDate = row.Table.Columns.Contains("AppealOutwardDate") && row["AppealOutwardDate"] != DBNull.Value ? Convert.ToDateTime(row["AppealOutwardDate"]) : null,
                AppealClosureNumber = row.Table.Columns.Contains("AppealClosureNumber") ? row["AppealClosureNumber"]?.ToString() : null,
                AppealClosureDate = row.Table.Columns.Contains("AppealClosureDate") && row["AppealClosureDate"] != DBNull.Value ? Convert.ToDateTime(row["AppealClosureDate"]) : null,
                AppealJudgmentCopyPath = row.Table.Columns.Contains("AppealJudgmentCopyPath") ? row["AppealJudgmentCopyPath"]?.ToString() : null,
            };
        }
    }
}
