using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using MVCCaseManagement.Models;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;

namespace MVCCaseManagement.DAL
{
    public class GratuityRepository : IGratuityRepository
    {
        private readonly string _connectionString;

        public GratuityRepository(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("MVCCaseDB") ?? "";
        }

        private IDbConnection CreateConnection()
        {
            return new SqlConnection(_connectionString);
        }

        public async Task<IEnumerable<GratuityCase>> GetAllCases(int divisionId = 0, int pageNumber = 1, int pageSize = 10, string? search = null, string? status = null)
        {
            using (var connection = CreateConnection())
            {
                int offset = (pageNumber - 1) * pageSize;
                var paramsList = new DynamicParameters();
                paramsList.Add("DivisionID", divisionId);
                paramsList.Add("Offset", offset);
                paramsList.Add("PageSize", pageSize);
                paramsList.Add("Search", string.IsNullOrEmpty(search) ? null : "%" + search.Trim() + "%");

                string query = "SELECT * FROM GRA_CASES WHERE 1=1";
                
                if (divisionId > 0 && divisionId != 5)
                {
                    query += " AND DivisionCode = @DivisionID";
                }

                // CO Filter logic
                bool isCentralOffice = divisionId == 0 || divisionId == 5;
                if (isCentralOffice && string.IsNullOrEmpty(search))
                {
                    if (status == "SentToCO" || string.IsNullOrEmpty(status))
                    {
                        query += " AND ForwardingStatus = 'Sent to Central Office' AND IsViewedByCO = 0";
                    }
                }

                if (!string.IsNullOrEmpty(search))
                {
                    query += " AND (PGANumber LIKE @Search OR ClaimantName LIKE @Search OR AdvocateName LIKE @Search)";
                }

                if (!string.IsNullOrEmpty(status) && status != "all")
                {
                    if (status == "Pending")
                        query += " AND (DisposalResult IS NULL OR DisposalResult = 'Pending')";
                    else if (status == "SentToCO")
                        query += " AND ForwardingStatus = 'Sent to Central Office' AND IsViewedByCO = 0";
                    else if (status == "SentToCO_All")
                        query += " AND ForwardingStatus = 'Sent to Central Office'";
                    else if (status == "NoAction")
                        query += " AND ForwardingStatus = 'Sent to Central Office' AND (ActionTaken IS NULL OR RTRIM(LTRIM(ActionTaken)) = '')";
                    else if (status == "WPPending")
                        query += " AND (CorpWPStatus = 'Pending' OR IsPendingForFiling = 1) AND AppealEntrustmentNo IS NOT NULL AND RTRIM(LTRIM(AppealEntrustmentNo)) <> '' AND AppealEntrustmentDate IS NOT NULL";
                    else if (status == "SLPPending")
                        query += " AND (ClaimantSCStatus = 'Pending' OR IsClaimantSCPending = 1) AND ClaimantSCEntrustmentNo IS NOT NULL AND RTRIM(LTRIM(ClaimantSCEntrustmentNo)) <> '' AND ClaimantSCEntrustmentDate IS NOT NULL";
                    else if (status == "PGACRPending")
                        query += " AND (CourtType = 'Controlling Authority' OR CourtType IS NULL) AND (AppealNumber IS NULL OR RTRIM(LTRIM(AppealNumber)) = '') AND (ClaimantWPNumber IS NULL OR RTRIM(LTRIM(ClaimantWPNumber)) = '') AND (DisposalResult IS NULL OR DisposalResult = 'Pending')";
                    else if (status == "PGAApplCRPending")
                        query += " AND ((AppealNumber IS NOT NULL AND RTRIM(LTRIM(AppealNumber)) <> '') OR (ClaimantWPNumber IS NOT NULL AND RTRIM(LTRIM(ClaimantWPNumber)) <> '')) AND (DisposalResult IS NULL OR DisposalResult = 'Pending')";
                    else if (status == "Disposed")
                        query += " AND (DisposalResult IN ('Favor', 'Favor (Settled)', 'Against', 'Dismissed'))";
                    else if (status == "FeasibilityPending")
                        query += " AND ActionTaken = 'Pending'";
                    else if (status == "PendingDecision")
                        query += " AND (UPPER(LTRIM(RTRIM(ActionTaken))) LIKE '%PENDING%DECISION%' OR UPPER(LTRIM(RTRIM(ActionTaken))) LIKE '%PENDING%FOR%ACTION%')";
                    else if (status == "PendingCompetentAuthority")
                        query += " AND (UPPER(LTRIM(RTRIM(ActionTaken))) LIKE '%PENDING%COMPETENT%AUTHORITY%' OR UPPER(LTRIM(RTRIM(ActionTaken))) LIKE '%PENDING%AT%CA%')";
                    else if (status == "AppealApproved")
                        query += " AND (UPPER(LTRIM(RTRIM(ActionTaken))) = 'APPEAL' OR UPPER(LTRIM(RTRIM(ActionTaken))) LIKE '%APPEAL%')";
                    else if (status == "NonCompliance")
                        query += " AND AppealEntrustmentNo IS NOT NULL AND RTRIM(LTRIM(AppealEntrustmentNo)) <> '' AND (AppealComplianceAmount IS NULL OR AppealComplianceAmount = 0 OR AppealComplianceChequeNumber IS NULL OR RTRIM(LTRIM(AppealComplianceChequeNumber)) = '')";
                    else
                    {
                        query += " AND DisposalResult = @StatusFilter";
                        paramsList.Add("StatusFilter", status);
                    }
                }

                query += " ORDER BY CreatedDate DESC OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY";
                return await connection.QueryAsync<GratuityCase>(query, paramsList);
            }
        }

        public async Task<int> GetTotalCaseCount(int divisionId = 0, string? search = null, string? status = null)
        {
            using (var connection = CreateConnection())
            {
                var paramsList = new DynamicParameters();
                paramsList.Add("DivisionID", divisionId);
                paramsList.Add("Search", string.IsNullOrEmpty(search) ? null : "%" + search.Trim() + "%");

                string query = "SELECT COUNT(*) FROM GRA_CASES WHERE 1=1";
                
                if (divisionId > 0 && divisionId != 5)
                {
                    query += " AND DivisionCode = @DivisionID";
                }

                // CO Filter logic
                bool isCentralOffice = divisionId == 0 || divisionId == 5;
                if (isCentralOffice && string.IsNullOrEmpty(search))
                {
                    if (status == "SentToCO" || string.IsNullOrEmpty(status))
                    {
                        query += " AND ForwardingStatus = 'Sent to Central Office' AND IsViewedByCO = 0";
                    }
                }

                if (!string.IsNullOrEmpty(search))
                {
                    query += " AND (PGANumber LIKE @Search OR ClaimantName LIKE @Search OR AdvocateName LIKE @Search)";
                }

                if (!string.IsNullOrEmpty(status) && status != "all")
                {
                    if (status == "Pending")
                        query += " AND (DisposalResult IS NULL OR DisposalResult = 'Pending')";
                    else if (status == "SentToCO")
                        query += " AND ForwardingStatus = 'Sent to Central Office' AND IsViewedByCO = 0";
                    else if (status == "SentToCO_All")
                        query += " AND ForwardingStatus = 'Sent to Central Office'";
                    else if (status == "NoAction")
                        query += " AND ForwardingStatus = 'Sent to Central Office' AND (ActionTaken IS NULL OR RTRIM(LTRIM(ActionTaken)) = '')";
                    else if (status == "WPPending")
                        query += " AND (CorpWPStatus = 'Pending' OR IsPendingForFiling = 1) AND (HighCourtBench IS NULL OR HighCourtBench <> 'Division Bench') AND AppealEntrustmentNo IS NOT NULL AND RTRIM(LTRIM(AppealEntrustmentNo)) <> '' AND AppealEntrustmentDate IS NOT NULL";
                    else if (status == "WritAppealPending")
                        query += " AND (CorpWPStatus = 'Pending' OR IsPendingForFiling = 1) AND HighCourtBench = 'Division Bench' AND AppealEntrustmentNo IS NOT NULL AND RTRIM(LTRIM(AppealEntrustmentNo)) <> '' AND AppealEntrustmentDate IS NOT NULL";
                    else if (status == "SLPPending")
                        query += " AND (ClaimantSCStatus = 'Pending' OR IsClaimantSCPending = 1) AND ClaimantSCEntrustmentNo IS NOT NULL AND RTRIM(LTRIM(ClaimantSCEntrustmentNo)) <> '' AND ClaimantSCEntrustmentDate IS NOT NULL";
                    else if (status == "PGACRPending")
                        query += " AND (CourtType = 'Controlling Authority' OR CourtType IS NULL) AND (AppealNumber IS NULL OR RTRIM(LTRIM(AppealNumber)) = '') AND (ClaimantWPNumber IS NULL OR RTRIM(LTRIM(ClaimantWPNumber)) = '') AND (DisposalResult IS NULL OR DisposalResult = 'Pending')";
                    else if (status == "PGAApplCRPending")
                        query += " AND ((AppealNumber IS NOT NULL AND RTRIM(LTRIM(AppealNumber)) <> '') OR (ClaimantWPNumber IS NOT NULL AND RTRIM(LTRIM(ClaimantWPNumber)) <> '')) AND (DisposalResult IS NULL OR DisposalResult = 'Pending')";
                    else if (status == "Disposed")
                        query += " AND (DisposalResult IN ('Favor', 'Favor (Settled)', 'Against', 'Dismissed'))";
                    else if (status == "FeasibilityPending")
                        query += " AND ActionTaken = 'Pending'";
                    else if (status == "PendingDecision")
                        query += " AND (UPPER(LTRIM(RTRIM(ActionTaken))) LIKE '%PENDING%DECISION%' OR UPPER(LTRIM(RTRIM(ActionTaken))) LIKE '%PENDING%FOR%ACTION%')";
                    else if (status == "PendingCompetentAuthority")
                        query += " AND (UPPER(LTRIM(RTRIM(ActionTaken))) LIKE '%PENDING%COMPETENT%AUTHORITY%' OR UPPER(LTRIM(RTRIM(ActionTaken))) LIKE '%PENDING%AT%CA%')";
                    else if (status == "AppealApproved")
                        query += " AND (UPPER(LTRIM(RTRIM(ActionTaken))) = 'APPEAL' OR UPPER(LTRIM(RTRIM(ActionTaken))) LIKE '%APPEAL%')";
                    else if (status == "NonCompliance")
                        query += " AND AppealEntrustmentNo IS NOT NULL AND RTRIM(LTRIM(AppealEntrustmentNo)) <> '' AND (AppealComplianceAmount IS NULL OR AppealComplianceAmount = 0 OR AppealComplianceChequeNumber IS NULL OR RTRIM(LTRIM(AppealComplianceChequeNumber)) = '')";
                    else
                    {
                        query += " AND DisposalResult = @StatusFilter";
                        paramsList.Add("StatusFilter", status);
                    }
                }

                return await connection.ExecuteScalarAsync<int>(query, paramsList);
            }
        }

        public async Task<GratuityCase?> GetCaseById(int id)
        {
            using (var connection = CreateConnection())
            {
                var query = "SELECT * FROM GRA_CASES WHERE CaseID = @Id";
                var gratuityCase = await connection.QueryFirstOrDefaultAsync<GratuityCase>(query, new { Id = id });

                if (gratuityCase != null)
                {
                    var pQuery = "SELECT * FROM GRA_PAYMENTS WHERE CaseID = @Id ORDER BY CreatedDate ASC";
                    var payments = await connection.QueryAsync<GratuityPayment>(pQuery, new { Id = id });
                    gratuityCase.Payments = payments.AsList();

                    var ipQuery = "SELECT * FROM GRA_INTEREST_PAYMENTS WHERE CaseID = @Id ORDER BY CreatedDate ASC";
                    var interestPayments = await connection.QueryAsync<GratuityInterestPayment>(ipQuery, new { Id = id });
                    gratuityCase.InterestPayments = interestPayments.AsList();

                    gratuityCase.EnclosedDocuments = await GetEnclosedDocs(id, connection);
                }

                return gratuityCase;
            }
        }

        public async Task<int> AddCase(GratuityCase c)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        var query = @"
                            INSERT INTO GRA_CASES (
                                CaseStatus, DivisionCode, PGANumber, CourtType, ClaimantName, DateOfBirth,
                                ClaimantDesignation, WorkingStatus, EntrustmentNo, EntrustmentDate, AdvocateName,
                                DisposalResult, IsAdditionalBenefitsGiven, AdditionalBenefitsAmount, 
                                IsDocumentSent, DocumentOutwardNo, DocumentOutwardDate, IsObjectionFiled, ObjectionOutwardNo,
                                ObjectionFiledDate, IsEvidenceFiled, CurrentStage, NextHearingDate, AmountClaimed, 
                                AppointmentDate, AppointmentDate_CA, CA_AppointmentRemark, RetirementDate, RetirementDate_CA,
                                TotalServicePeriod, Period_SPE_LWA_ABS, Period_SPE_LWA_ABS_CA, QualifyingService_Corp, QualifyingService_CA,
                                LastDrawnPay, LastDrawnPay_CA, LastDrawnBasic, LastDrawnBDA, LastDrawnDA, LastDrawnBasic_CA, LastDrawnBDA_CA, LastDrawnDA_CA,
                                GratuityAmount_Corp_Reg, GratuityAmount_Corp_Act, IsDeductionMade, DeductionAmount, DeductionDetails, ActualPaidAmount,
                                ChequeNumber, ChequeDate, DisposalDate, DelayRemarks,
                                CopyAppliedDate, CopyIssuedDate, CopyDeliveredDate, CopyReceivedDate, GratuityAmount_CA_Reg,
                                GratuityAmount_CA_Act, OrderedAmount_CA, IsDeductionMade_CA, DeductionAmount_CA, DeductionDetails_CA,
                                ActualPaidAmount_CA, IsInterestPayable, InterestRate, InterestRemarks, ChequeNumber_CA, ChequeDate_CA,
                                IsFeasibilityReceived, FeasibilityReceiptDate, ActionTaken, ApprovalOutwardNo, ApprovalDate,
                                AdvocateOpinion, LOOpinion, DCOpinion,
                                AppealNumber, AppealYear, AppealArisingNo, AppealArisingYear, AppealCourt, AppealEntrustmentNo, AppealEntrustmentDate,
                                AppealAdvocate, AppealAwardDetails, AppealDisposalDate, AppealJudgmentPath, FinalAmount, ComplianceStatus, Remarks,
                                AppealComplianceAmount, AppealComplianceChequeNumber, AppealComplianceChequeDate,
                                ForwardingStatus, ClosureRemarks, ClosureDate, OutwardNumber, OutwardDate, AdverseJudgmentPath,
                                AppealRemarks, AppealDate,
                                ForwardingStatus_Appeal, OutwardNumber_Appeal, OutwardDate_Appeal,
                                AppealCopyAppliedDate, AppealCopyReadyDate, AppealCopyDeliveredDate, AppealCopyReceivedDate,
                                AppealDelayRemarks, AppealAdvocateOpinion, AppealLOOpinion, AppealDCOpinion,
                                IsPendingForFiling, HighCourtBench, OtherHighCourtBench, StayGranted,
                                StayComplianceOutwardNo, StayComplianceDate, StayOrderPath1, StayOrderPath2,
                                CorpWPNumber, CorpWPYear, CorpWPAdvocate, CorpWPEntrustmentNo, CorpWPEntrustmentDate,
                                CorpWPStatus, RestorationFiled, RestorationDate, RestorationStatus,
                                CorpWPOutcome, CorpWPActionTaken, ClosureOutwardNo,
                                InitialActionRemarks, InitialActionPath1, InitialActionPath2, FinalRemarks,
                                AppealActionOutwardNo, AppealActionDate,
                                IsClaimantSCAppeal, IsClaimantSCPending, ClaimantSCDiaryNumber, ClaimantSCYear,
                                ClaimantSCNumber, ClaimantSLPYear, ClaimantSCFiledBy, ClaimantSCEntrustmentNo,
                                ClaimantSCEntrustmentDate, ClaimantSCAdvocate, ClaimantSCStatus, ClaimantSCOutcome,
                                ClaimantSCActionTaken, ClaimantSCClosureNo, ClaimantSCClosureDate,
                                ClaimantDivisionID, ClaimantArisingWPNumber, ClaimantArisingWPYear, ClaimantMVCCurrentStatus,
                                EvidenceRemarks, EvidenceWitnessName, EvidenceWitnessDesignation,
                                ClaimantWPNumber, ClaimantWPYear, ClaimantHighCourtBench, ClaimantOtherHighCourtBench,
                                ClaimantWPEntrustmentNo, ClaimantWPEntrustmentDate,
                                ClaimantWPAdvocate, ClaimantWPStatus, ClaimantWPDecision, ClaimantActionTaken,
                                ClaimantApprovalNo, ClaimantApprovalDate,
                                CreatedDate, ModifiedDate, CreatedBy, IsViewedByCO
                            ) VALUES (
                                @CaseStatus, @DivisionCode, @PGANumber, @CourtType, @ClaimantName, @DateOfBirth,
                                @ClaimantDesignation, @WorkingStatus, @EntrustmentNo, @EntrustmentDate, @AdvocateName,
                                @DisposalResult, @IsAdditionalBenefitsGiven, @AdditionalBenefitsAmount,
                                @IsDocumentSent, @DocumentOutwardNo, @DocumentOutwardDate, @IsObjectionFiled, @ObjectionOutwardNo,
                                @ObjectionFiledDate, @IsEvidenceFiled, @CurrentStage, @NextHearingDate, @AmountClaimed, 
                                @AppointmentDate, @AppointmentDate_CA, @CA_AppointmentRemark, @RetirementDate, @RetirementDate_CA,
                                @TotalServicePeriod, @Period_SPE_LWA_ABS, @Period_SPE_LWA_ABS_CA, @QualifyingService_Corp, @QualifyingService_CA,
                                @LastDrawnPay, @LastDrawnPay_CA, @LastDrawnBasic, @LastDrawnBDA, @LastDrawnDA, @LastDrawnBasic_CA, @LastDrawnBDA_CA, @LastDrawnDA_CA,
                                @GratuityAmount_Corp_Reg, @GratuityAmount_Corp_Act, @IsDeductionMade, @DeductionAmount, @DeductionDetails, @ActualPaidAmount,
                                @ChequeNumber, @ChequeDate, @DisposalDate, @DelayRemarks,
                                @CopyAppliedDate, @CopyIssuedDate, @CopyDeliveredDate, @CopyReceivedDate, @GratuityAmount_CA_Reg,
                                @GratuityAmount_CA_Act, @OrderedAmount_CA, @IsDeductionMade_CA, @DeductionAmount_CA, @DeductionDetails_CA,
                                @ActualPaidAmount_CA, @IsInterestPayable, @InterestRate, @InterestRemarks, @ChequeNumber_CA, @ChequeDate_CA,
                                @IsFeasibilityReceived, @FeasibilityReceiptDate, @ActionTaken, @ApprovalOutwardNo, @ApprovalDate,
                                @AdvocateOpinion, @LOOpinion, @DCOpinion,
                                @AppealNumber, @AppealYear, @AppealArisingNo, @AppealArisingYear, @AppealCourt, @AppealEntrustmentNo, @AppealEntrustmentDate,
                                @AppealAdvocate, @AppealAwardDetails, @AppealDisposalDate, @AppealJudgmentPath, @FinalAmount, @ComplianceStatus, @Remarks,
                                @AppealComplianceAmount, @AppealComplianceChequeNumber, @AppealComplianceChequeDate,
                                @ForwardingStatus, @ClosureRemarks, @ClosureDate, @OutwardNumber, @OutwardDate, @AdverseJudgmentPath,
                                @AppealRemarks, @AppealDate,
                                @ForwardingStatus_Appeal, @OutwardNumber_Appeal, @OutwardDate_Appeal,
                                @AppealCopyAppliedDate, @AppealCopyReadyDate, @AppealCopyDeliveredDate, @AppealCopyReceivedDate,
                                @AppealDelayRemarks, @AppealAdvocateOpinion, @AppealLOOpinion, @AppealDCOpinion,
                                @IsPendingForFiling, @HighCourtBench, @OtherHighCourtBench, @StayGranted,
                                @StayComplianceOutwardNo, @StayComplianceDate, @StayOrderPath1, @StayOrderPath2,
                                @CorpWPNumber, @CorpWPYear, @CorpWPAdvocate, @CorpWPEntrustmentNo, @CorpWPEntrustmentDate,
                                @CorpWPStatus, @RestorationFiled, @RestorationDate, @RestorationStatus,
                                @CorpWPOutcome, @CorpWPActionTaken, @ClosureOutwardNo,
                                @InitialActionRemarks, @InitialActionPath1, @InitialActionPath2, @FinalRemarks,
                                @AppealActionOutwardNo, @AppealActionDate,
                                @IsClaimantSCAppeal, @IsClaimantSCPending, @ClaimantSCDiaryNumber, @ClaimantSCYear,
                                @ClaimantSCNumber, @ClaimantSLPYear, @ClaimantSCFiledBy, @ClaimantSCEntrustmentNo,
                                @ClaimantSCEntrustmentDate, @ClaimantSCAdvocate, @ClaimantSCStatus, @ClaimantSCOutcome,
                                @ClaimantSCActionTaken, @ClaimantSCClosureNo, @ClaimantSCClosureDate,
                                @ClaimantDivisionID, @ClaimantArisingWPNumber, @ClaimantArisingWPYear, @ClaimantMVCCurrentStatus,
                                @EvidenceRemarks, @EvidenceWitnessName, @EvidenceWitnessDesignation,
                                @ClaimantWPNumber, @ClaimantWPYear, @ClaimantHighCourtBench, @ClaimantOtherHighCourtBench,
                                @ClaimantWPEntrustmentNo, @ClaimantWPEntrustmentDate,
                                @ClaimantWPAdvocate, @ClaimantWPStatus, @ClaimantWPDecision, @ClaimantActionTaken,
                                @ClaimantApprovalNo, @ClaimantApprovalDate,
                                GETDATE(), GETDATE(), @CreatedBy, @IsViewedByCO
                            );
                            SELECT CAST(SCOPE_IDENTITY() as int)";

                        var caseId = await connection.ExecuteScalarAsync<int>(query, c, transaction);

                        if (caseId > 0 && c.Payments != null && c.Payments.Count > 0)
                        {
                            foreach (var p in c.Payments)
                            {
                                p.CaseID = caseId;
                                var pQuery = "INSERT INTO GRA_PAYMENTS (CaseID, Amount, ChequeNumber, ChequeDate) VALUES (@CaseID, @Amount, @ChequeNumber, @ChequeDate)";
                                await connection.ExecuteAsync(pQuery, p, transaction);
                            }
                        }

                        // Insert Interest Payments
                        if (caseId > 0 && c.InterestPayments != null && c.InterestPayments.Count > 0)
                        {
                            foreach (var ip in c.InterestPayments)
                            {
                                ip.CaseID = caseId;
                                var ipQuery = "INSERT INTO GRA_INTEREST_PAYMENTS (CaseID, InterestRate, Amount, FromDate, ToDate, ChequeNumber, ChequeDate) VALUES (@CaseID, @InterestRate, @Amount, @FromDate, @ToDate, @ChequeNumber, @ChequeDate)";
                                await connection.ExecuteAsync(ipQuery, ip, transaction);
                            }
                        }

                        // Save Enclosed Documents
                        await SaveEnclosedDocs(caseId, c.EnclosedDocuments, connection, transaction);

                        transaction.Commit();
                        return caseId;
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }
        }

        public async Task<bool> UpdateCase(GratuityCase c)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        var query = @"
                            UPDATE GRA_CASES SET 
                                CaseStatus = @CaseStatus, DivisionCode = @DivisionCode, PGANumber = @PGANumber, 
                                CourtType = @CourtType, ClaimantName = @ClaimantName, DateOfBirth = @DateOfBirth,
                                ClaimantDesignation = @ClaimantDesignation,
                                WorkingStatus = @WorkingStatus, EntrustmentNo = @EntrustmentNo, EntrustmentDate = @EntrustmentDate,
                                AdvocateName = @AdvocateName, DisposalResult = @DisposalResult, 
                                IsAdditionalBenefitsGiven = @IsAdditionalBenefitsGiven, AdditionalBenefitsAmount = @AdditionalBenefitsAmount,
                                IsDocumentSent = @IsDocumentSent, DocumentOutwardNo = @DocumentOutwardNo, DocumentOutwardDate = @DocumentOutwardDate,
                                IsObjectionFiled = @IsObjectionFiled, ObjectionOutwardNo = @ObjectionOutwardNo, ObjectionFiledDate = @ObjectionFiledDate, IsEvidenceFiled = @IsEvidenceFiled,
                                CurrentStage = @CurrentStage, NextHearingDate = @NextHearingDate, AmountClaimed = @AmountClaimed,
                                AppointmentDate = @AppointmentDate, AppointmentDate_CA = @AppointmentDate_CA, CA_AppointmentRemark = @CA_AppointmentRemark,
                                RetirementDate = @RetirementDate, RetirementDate_CA = @RetirementDate_CA,
                                TotalServicePeriod = @TotalServicePeriod, Period_SPE_LWA_ABS = @Period_SPE_LWA_ABS, Period_SPE_LWA_ABS_CA = @Period_SPE_LWA_ABS_CA,
                                QualifyingService_Corp = @QualifyingService_Corp, QualifyingService_CA = @QualifyingService_CA,
                                LastDrawnPay = @LastDrawnPay, LastDrawnPay_CA = @LastDrawnPay_CA,
                                LastDrawnBasic = @LastDrawnBasic, LastDrawnBDA = @LastDrawnBDA, LastDrawnDA = @LastDrawnDA,
                                LastDrawnBasic_CA = @LastDrawnBasic_CA, LastDrawnBDA_CA = @LastDrawnBDA_CA, LastDrawnDA_CA = @LastDrawnDA_CA,
                                GratuityAmount_Corp_Reg = @GratuityAmount_Corp_Reg, GratuityAmount_Corp_Act = @GratuityAmount_Corp_Act,
                                IsDeductionMade = @IsDeductionMade, DeductionAmount = @DeductionAmount, 
                                DeductionDetails = @DeductionDetails, ActualPaidAmount = @ActualPaidAmount,
                                ChequeNumber = @ChequeNumber, ChequeDate = @ChequeDate, DisposalDate = @DisposalDate, DelayRemarks = @DelayRemarks,
                                CopyAppliedDate = @CopyAppliedDate, CopyIssuedDate = @CopyIssuedDate, CopyDeliveredDate = @CopyDeliveredDate, CopyReceivedDate = @CopyReceivedDate,
                                GratuityAmount_CA_Reg = @GratuityAmount_CA_Reg, GratuityAmount_CA_Act = @GratuityAmount_CA_Act,
                                OrderedAmount_CA = @OrderedAmount_CA, IsDeductionMade_CA = @IsDeductionMade_CA,
                                DeductionAmount_CA = @DeductionAmount_CA, DeductionDetails_CA = @DeductionDetails_CA,
                                ActualPaidAmount_CA = @ActualPaidAmount_CA, IsInterestPayable = @IsInterestPayable, InterestRate = @InterestRate, InterestRemarks = @InterestRemarks, ChequeNumber_CA = @ChequeNumber_CA, ChequeDate_CA = @ChequeDate_CA,
                                IsFeasibilityReceived = @IsFeasibilityReceived,
                                FeasibilityReceiptDate = @FeasibilityReceiptDate,
                                ActionTaken = @ActionTaken,
                                ApprovalOutwardNo = @ApprovalOutwardNo,
                                ApprovalDate = @ApprovalDate,
                                AdvocateOpinion = @AdvocateOpinion, LOOpinion = @LOOpinion,
                                DCOpinion = @DCOpinion,                                 AppealNumber = @AppealNumber, AppealYear = @AppealYear,
                                AppealArisingNo = @AppealArisingNo, AppealArisingYear = @AppealArisingYear,
                                AppealCourt = @AppealCourt, AppealEntrustmentNo = @AppealEntrustmentNo,
                                AppealEntrustmentDate = @AppealEntrustmentDate, AppealAdvocate = @AppealAdvocate,
                                AppealAwardDetails = @AppealAwardDetails, AppealDisposalDate = @AppealDisposalDate, AppealJudgmentPath = COALESCE(@AppealJudgmentPath, AppealJudgmentPath), FinalAmount = @FinalAmount, ComplianceStatus = @ComplianceStatus,
                                AppealComplianceAmount = @AppealComplianceAmount, AppealComplianceChequeNumber = @AppealComplianceChequeNumber, AppealComplianceChequeDate = @AppealComplianceChequeDate,
                                Remarks = @Remarks, ForwardingStatus = @ForwardingStatus, ClosureRemarks = @ClosureRemarks,
                                ClosureDate = @ClosureDate, OutwardNumber = @OutwardNumber, OutwardDate = @OutwardDate,
                                AdverseJudgmentPath = COALESCE(@AdverseJudgmentPath, AdverseJudgmentPath), AppealRemarks = @AppealRemarks, AppealDate = @AppealDate,
                                ForwardingStatus_Appeal = @ForwardingStatus_Appeal, OutwardNumber_Appeal = @OutwardNumber_Appeal, OutwardDate_Appeal = @OutwardDate_Appeal,
                                AppealCopyAppliedDate = @AppealCopyAppliedDate, AppealCopyReadyDate = @AppealCopyReadyDate, 
                                AppealCopyDeliveredDate = @AppealCopyDeliveredDate, AppealCopyReceivedDate = @AppealCopyReceivedDate,
                                AppealDelayRemarks = @AppealDelayRemarks, AppealAdvocateOpinion = @AppealAdvocateOpinion, 
                                AppealLOOpinion = @AppealLOOpinion, AppealDCOpinion = @AppealDCOpinion,
                                
                                -- NEW FIELDS
                                IsPendingForFiling = @IsPendingForFiling, HighCourtBench = @HighCourtBench, OtherHighCourtBench = @OtherHighCourtBench,
                                StayGranted = @StayGranted, StayComplianceOutwardNo = @StayComplianceOutwardNo, StayComplianceDate = @StayComplianceDate,
                                StayOrderPath1 = COALESCE(@StayOrderPath1, StayOrderPath1), StayOrderPath2 = COALESCE(@StayOrderPath2, StayOrderPath2),
                                InitialActionRemarks = @InitialActionRemarks, InitialActionPath1 = COALESCE(@InitialActionPath1, InitialActionPath1), InitialActionPath2 = COALESCE(@InitialActionPath2, InitialActionPath2), FinalRemarks = @FinalRemarks,
                                AppealActionOutwardNo = @AppealActionOutwardNo, AppealActionDate = @AppealActionDate,
                                CorpWPNumber = @CorpWPNumber, CorpWPYear = @CorpWPYear, CorpWPAdvocate = @CorpWPAdvocate,
                                CorpWPEntrustmentNo = @CorpWPEntrustmentNo, CorpWPEntrustmentDate = @CorpWPEntrustmentDate,
                                CorpWPStatus = @CorpWPStatus, RestorationFiled = @RestorationFiled, RestorationDate = @RestorationDate, RestorationStatus = @RestorationStatus,
                                CorpWPOutcome = @CorpWPOutcome, CorpWPActionTaken = @CorpWPActionTaken, ClosureOutwardNo = @ClosureOutwardNo,
                                
                                IsClaimantSCAppeal = @IsClaimantSCAppeal, IsClaimantSCPending = @IsClaimantSCPending, ClaimantSCDiaryNumber = @ClaimantSCDiaryNumber,
                                ClaimantSCYear = @ClaimantSCYear, ClaimantSCNumber = @ClaimantSCNumber, ClaimantSLPYear = @ClaimantSLPYear,
                                ClaimantSCFiledBy = @ClaimantSCFiledBy, ClaimantSCEntrustmentNo = @ClaimantSCEntrustmentNo, ClaimantSCEntrustmentDate = @ClaimantSCEntrustmentDate,
                                ClaimantSCAdvocate = @ClaimantSCAdvocate, ClaimantSCStatus = @ClaimantSCStatus, ClaimantSCOutcome = @ClaimantSCOutcome,
                                ClaimantSCActionTaken = @ClaimantSCActionTaken, ClaimantSCClosureNo = @ClaimantSCClosureNo, ClaimantSCClosureDate = @ClaimantSCClosureDate,
                                ClaimantDivisionID = @ClaimantDivisionID, ClaimantArisingWPNumber = @ClaimantArisingWPNumber, ClaimantArisingWPYear = @ClaimantArisingWPYear,
                                ClaimantMVCCurrentStatus = @ClaimantMVCCurrentStatus, 
                                EvidenceRemarks = @EvidenceRemarks, EvidenceWitnessName = @EvidenceWitnessName, EvidenceWitnessDesignation = @EvidenceWitnessDesignation,
                                ClaimantWPNumber = @ClaimantWPNumber, ClaimantWPYear = @ClaimantWPYear,
                                ClaimantHighCourtBench = @ClaimantHighCourtBench, ClaimantOtherHighCourtBench = @ClaimantOtherHighCourtBench,
                                ClaimantWPEntrustmentNo = @ClaimantWPEntrustmentNo, ClaimantWPEntrustmentDate = @ClaimantWPEntrustmentDate, ClaimantWPAdvocate = @ClaimantWPAdvocate,
                                ClaimantWPStatus = @ClaimantWPStatus, ClaimantWPDecision = @ClaimantWPDecision, ClaimantActionTaken = @ClaimantActionTaken,
                                ClaimantApprovalNo = @ClaimantApprovalNo, ClaimantApprovalDate = @ClaimantApprovalDate,

                                IsViewedByCO = @IsViewedByCO,
                                ModifiedDate = GETDATE()
                            WHERE CaseID = @CaseID";

                        var rows = await connection.ExecuteAsync(query, c, transaction);

                        if (rows > 0)
                        {
                            // Synchronize payments only if collection is provided in payload
                            if (c.Payments != null)
                            {
                                await connection.ExecuteAsync("DELETE FROM GRA_PAYMENTS WHERE CaseID = @CaseID", new { CaseID = c.CaseID }, transaction);
                                if (c.Payments.Count > 0)
                                {
                                    foreach (var p in c.Payments)
                                    {
                                        p.CaseID = c.CaseID;
                                        var pQuery = "INSERT INTO GRA_PAYMENTS (CaseID, Amount, ChequeNumber, ChequeDate) VALUES (@CaseID, @Amount, @ChequeNumber, @ChequeDate)";
                                        await connection.ExecuteAsync(pQuery, p, transaction);
                                    }
                                }
                            }

                            // Synchronize interest payments only if collection is provided in payload
                            if (c.InterestPayments != null)
                            {
                                await connection.ExecuteAsync("DELETE FROM GRA_INTEREST_PAYMENTS WHERE CaseID = @CaseID", new { CaseID = c.CaseID }, transaction);
                                if (c.InterestPayments.Count > 0)
                                {
                                    foreach (var ip in c.InterestPayments)
                                    {
                                        ip.CaseID = c.CaseID;
                                        var ipQuery = "INSERT INTO GRA_INTEREST_PAYMENTS (CaseID, InterestRate, Amount, FromDate, ToDate, ChequeNumber, ChequeDate) VALUES (@CaseID, @InterestRate, @Amount, @FromDate, @ToDate, @ChequeNumber, @ChequeDate)";
                                        await connection.ExecuteAsync(ipQuery, ip, transaction);
                                    }
                                }
                            }

                            // Save Enclosed Documents only if collection is provided in payload
                            if (c.EnclosedDocuments != null)
                            {
                                await SaveEnclosedDocs(c.CaseID, c.EnclosedDocuments, connection, transaction);
                            }
                        }

                        transaction.Commit();
                        return rows > 0;
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }
        }

        public async Task<bool> DeleteCase(int id)
        {
            using (var connection = CreateConnection())
            {
                var query = "DELETE FROM GRA_CASES WHERE CaseID = @Id";
                var rows = await connection.ExecuteAsync(query, new { Id = id });
                return rows > 0;
            }
        }

        public async Task<IEnumerable<GratuityCase>> GetCasesByDivision(int divisionId)
        {
             using (var connection = CreateConnection())
            {
                var query = "SELECT * FROM GRA_CASES WHERE DivisionCode = @DivisionCode ORDER BY CreatedDate DESC";
                return await connection.QueryAsync<GratuityCase>(query, new { DivisionCode = divisionId });
            }
        }

        public async Task<DashboardStatsViewModel> GetDashboardStats(int divisionId = 0)
        {
            using (var connection = CreateConnection())
            {
                var stats = new DashboardStatsViewModel();
                var param = new { DivisionID = divisionId };
                string divFilter = (divisionId > 0 && divisionId != 5) ? " AND DivisionCode = @DivisionID" : "";

                stats.TotalCases = await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM GRA_CASES WHERE 1=1" + divFilter, param);
                stats.PendingCases = await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM GRA_CASES WHERE (DisposalResult IS NULL OR DisposalResult = 'Pending')" + divFilter, param);
                stats.FavorCases = await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM GRA_CASES WHERE (DisposalResult = 'Favor' OR DisposalResult = 'Favor (Settled)')" + divFilter, param);
                stats.AgainstCases = await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM GRA_CASES WHERE (DisposalResult = 'Against' OR DisposalResult = 'Dismissed')" + divFilter, param);
                stats.SentToCOCount = await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM GRA_CASES WHERE ForwardingStatus = 'Sent to Central Office' AND IsViewedByCO = 0" + divFilter, param);
                stats.NoActionTakenCount = await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM GRA_CASES WHERE ForwardingStatus = 'Sent to Central Office' AND (ActionTaken IS NULL OR RTRIM(LTRIM(ActionTaken)) = '')" + divFilter, param);
                stats.WPPendingCount = await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM GRA_CASES WHERE (CorpWPStatus = 'Pending' OR IsPendingForFiling = 1) AND (HighCourtBench IS NULL OR HighCourtBench <> 'Division Bench') AND AppealEntrustmentNo IS NOT NULL AND RTRIM(LTRIM(AppealEntrustmentNo)) <> '' AND AppealEntrustmentDate IS NOT NULL" + divFilter, param);
                stats.WritAppealPendingCount = await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM GRA_CASES WHERE (CorpWPStatus = 'Pending' OR IsPendingForFiling = 1) AND HighCourtBench = 'Division Bench' AND AppealEntrustmentNo IS NOT NULL AND RTRIM(LTRIM(AppealEntrustmentNo)) <> '' AND AppealEntrustmentDate IS NOT NULL" + divFilter, param);
                stats.SLPPendingCount = await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM GRA_CASES WHERE (ClaimantSCStatus = 'Pending' OR IsClaimantSCPending = 1) AND ClaimantSCEntrustmentNo IS NOT NULL AND RTRIM(LTRIM(ClaimantSCEntrustmentNo)) <> '' AND ClaimantSCEntrustmentDate IS NOT NULL" + divFilter, param);

                stats.PGACRPendingCount = await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM GRA_CASES WHERE (CourtType = 'Controlling Authority' OR CourtType IS NULL) AND (AppealNumber IS NULL OR RTRIM(LTRIM(AppealNumber)) = '') AND (ClaimantWPNumber IS NULL OR RTRIM(LTRIM(ClaimantWPNumber)) = '') AND (DisposalResult IS NULL OR DisposalResult = 'Pending')" + divFilter, param);
                stats.PGAApplCRPendingCount = await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM GRA_CASES WHERE ((AppealNumber IS NOT NULL AND RTRIM(LTRIM(AppealNumber)) <> '') OR (ClaimantWPNumber IS NOT NULL AND RTRIM(LTRIM(ClaimantWPNumber)) <> '')) AND (DisposalResult IS NULL OR DisposalResult = 'Pending')" + divFilter, param);
                
                stats.CasesThisMonth = await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM GRA_CASES WHERE CreatedDate >= DATEADD(month, DATEDIFF(month, 0, GETDATE()), 0)" + divFilter, param);
                stats.UpcomingHearingsCount = await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM GRA_CASES WHERE NextHearingDate BETWEEN CAST(GETDATE() AS DATE) AND CAST(DATEADD(day, 7, GETDATE()) AS DATE)" + divFilter, param);
                stats.FeasibilityReviewCount = await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM GRA_CASES WHERE (ActionTaken = 'Pending' OR ActionTaken = 'PENDING')" + divFilter, param);
                stats.PendingCompetentAuthorityCount = await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM GRA_CASES WHERE (UPPER(LTRIM(RTRIM(ActionTaken))) LIKE '%PENDING%COMPETENT%AUTHORITY%' OR UPPER(LTRIM(RTRIM(ActionTaken))) = 'PENDING AT CA')" + divFilter, param);
                stats.PendingCLOLOCount = await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM GRA_CASES WHERE (UPPER(LTRIM(RTRIM(ActionTaken))) LIKE '%PENDING%DECISION%' OR UPPER(LTRIM(RTRIM(ActionTaken))) = 'PENDING FOR ACTION')" + divFilter, param);
                
                // Finance Exposure: Sum of OrderedAmount_CA (Liability awarded by Controlling Authority)
                stats.TotalAwardAmountAgainst = await connection.ExecuteScalarAsync<decimal>("SELECT ISNULL(SUM(OrderedAmount_CA), 0) FROM GRA_CASES WHERE 1=1" + divFilter, param);
                // stats.TotalClaimedAmount = await connection.ExecuteScalarAsync<decimal>("SELECT ISNULL(SUM(AmountClaimed), 0) FROM GRA_CASES WHERE 1=1" + divFilter, param);

                // Snapshot Metrics
                stats.HearingsToday = await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM GRA_CASES WHERE CAST(NextHearingDate AS DATE) = CAST(GETDATE() AS DATE)" + divFilter, param);
                stats.AwardsReceivedToday = await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM GRA_CASES WHERE CAST(DisposalDate AS DATE) = CAST(GETDATE() AS DATE) AND DisposalResult IN ('Against', 'Dismissed')" + divFilter, param);
                stats.ComplianceDueCount = await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM GRA_CASES WHERE StayComplianceDate BETWEEN CAST(GETDATE() AS DATE) AND CAST(DATEADD(day, 7, GETDATE()) AS DATE)" + divFilter, param);
                stats.CriticalDelaysCount = await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM GRA_CASES WHERE NextHearingDate < CAST(GETDATE() AS DATE) AND (DisposalResult IS NULL OR DisposalResult = 'Pending')" + divFilter, param);

                return stats;
            }
        }

        public async Task<List<RecentCaseViewModel>> GetCasesByHearingDate(DateTime hearingDate, int divisionId = 0, DateTime? endDate = null)
        {
            using (var connection = CreateConnection())
            {
                string query = @"
                    SELECT 
                        CaseID, 
                        PGANumber as MVCNo, 
                        ClaimantName as MACTName, 
                        DisposalResult, 
                        CreatedDate as CreatedAt, 
                        NextHearingDate
                    FROM GRA_CASES 
                    WHERE CAST(NextHearingDate AS DATE) ";

                if (endDate.HasValue)
                {
                    query += " BETWEEN @StartDate AND @EndDate";
                }
                else
                {
                    query += " = @StartDate";
                }

                if (divisionId > 0 && divisionId != 5)
                {
                    query += " AND DivisionCode = @DivisionID";
                }

                query += " ORDER BY NextHearingDate ASC";

                var results = await connection.QueryAsync<RecentCaseViewModel>(query, new 
                { 
                    StartDate = hearingDate.Date, 
                    EndDate = endDate?.Date,
                    DivisionID = divisionId 
                });

                return results.AsList();
            }
        }

        public async Task<bool> MarkAsViewedByCO(int caseId)
        {
            using (var connection = CreateConnection())
            {
                string query = "UPDATE GRA_CASES SET IsViewedByCO = 1 WHERE CaseID = @CaseID";
                var rows = await connection.ExecuteAsync(query, new { CaseID = caseId });
                return rows > 0;
            }
        }

        private async Task SaveEnclosedDocs(int caseId, List<EnclosedDocument> docs, IDbConnection conn, IDbTransaction trans)
        {
            if (docs == null) return;
            try
            {
                await conn.ExecuteAsync("DELETE FROM GRATUITY_ENCLOSED_DOCS WHERE CaseID = @CaseID", new { CaseID = caseId }, trans);
                if (docs != null && docs.Any())
                {
                    foreach (var doc in docs)
                    {
                        if (!string.IsNullOrEmpty(doc.DocName))
                        {
                            await conn.ExecuteAsync("INSERT INTO GRATUITY_ENCLOSED_DOCS (CaseID, DocName, PageCount) VALUES (@CaseID, @DocName, @PageCount)",
                                new { CaseID = caseId, DocName = doc.DocName, PageCount = doc.PageCount }, trans);
                        }
                    }
                }
            }
            catch
            {
                // Silently swallow enclosed doc save errors if table issue occurs so case saving succeeds
            }
        }

        private async Task<List<EnclosedDocument>> GetEnclosedDocs(int caseId, IDbConnection conn)
        {
            try
            {
                var docs = await conn.QueryAsync<EnclosedDocument>("SELECT DocName, PageCount FROM GRATUITY_ENCLOSED_DOCS WHERE CaseID = @CaseID", new { CaseID = caseId });
                return docs.AsList();
            }
            catch { return new List<EnclosedDocument>(); }
        }
    }
}
