using System.Data;
using Microsoft.Data.SqlClient;
using MVCCaseManagement.Models;

namespace MVCCaseManagement.DAL
{
    public interface IArisingApplicationRepository
    {
        int SaveArisingApplication(ArisingApplication model);
        void UpdateArisingApplication(ArisingApplication model);
        ArisingApplication? GetById(int arisingId);
        IEnumerable<ArisingApplication> GetByParentCaseId(int parentCaseId, string? caseNumber = null, int? caseYear = null);
        IEnumerable<ArisingApplication> GetAll(int divisionId = 0, int pageNumber = 1, int pageSize = 10, string? search = null);
        int GetTotalCount(int divisionId = 0, string? search = null);
        IEnumerable<ArisingApplication> GetByHearingDate(DateTime date, int divisionId, DateTime? endDate = null);
        void AddPayment(ArisingApplicationPayment payment);
        bool DeletePayment(int paymentId);
        bool DeleteArisingApplication(int arisingId);
        bool UpdateAwardAmount(int arisingId, decimal awardAmount);
        List<ArisingApplicationPayment> GetPaymentsByArisingId(int arisingId);
        bool UpdateArisingRoleAction(int arisingId, string role, string actionTaken, DateTime? approvalDate, string opinion, int? modifiedBy);
    }

    public class ArisingApplicationRepository : IArisingApplicationRepository
    {
        private readonly DBHelper _db;

        public ArisingApplicationRepository(DBHelper db)
        {
            _db = db;
        }

        /// <summary>
        /// Save a new Arising Application with full ACID transaction support.
        /// Returns the new ArisingID.
        /// </summary>
        public int SaveArisingApplication(ArisingApplication model)
        {
            using var connection = _db.GetConnection();
            connection.Open();
            using var transaction = connection.BeginTransaction(IsolationLevel.ReadCommitted);

            try
            {
                string query = @"
                    INSERT INTO LABOUR_ARISING_APPLICATIONS (
                        ParentCaseID, Parent_CaseNumber, Parent_CaseYear, Parent_CourtID,
                        Parent_CourtName, Parent_CaseType, Parent_CaseStatus, Parent_PetitionerName,
                        DivisionID, CaseNumber, CaseYear, CourtID, OtherCourtDetails, CNRNumber, EstCode, CaseTypeCode,
                        CaseType, CaseStatus, PetitionerName,
                        EntrustmentNo, EntrustmentDate, AdvocateID, AdvocateName,
                        IsDocumentSent, DocumentSent_OutwardNo, DocumentSent_OutwardDate,
                        IsObjectionFiled, ObjectionFiled_OutwardNo, ObjectionFiled_OutwardDate,
                        IsEvidenceFiled, CurrentStage, NextHearingDate,
                        DisposalMode, DisposalDate, DisposalResult, FavorRemark, FavorOutwardDate, JudgmentCopyPath, ClaimPetitionPath,
                        LokAdalat_COApprovalRequired, LokAdalat_OutwardNo, LokAdalat_Date, LokAdalatDocumentPath,
                        DE_HistorySheet, DE_HistorySheetPath, DE_ObjectionsFiled, DE_ObjectionsRemark,
                        DE_DocumentsMarked, DE_DocumentsRemark, DE_Order,
                        DE_EO_IsBasedOnDocuments, DE_EO_Name, DE_EO_Designation,
                        DE_Reporter_IsBasedOnDocuments, DE_Reporter_Name, DE_Reporter_Designation,
                        DE_Other_IsBasedOnDocuments, DE_Other_Name, DE_Other_Designation,
                        CC_CaseDisposedDate, CC_PublicationDate, CC_AppliedDate, CC_IssuedDate, CC_ReceivedDate, CC_Remarks,
                        AwardDetails, Opinion_Advocate, Opinion_LO, Opinion_DC,
                        ActionTaken_LO, ApprovalDate_LO, ActionTaken_DyCLO, ApprovalDate_DyCLO, Opinion_DyCLO,
                        ActionTaken_CLO, ApprovalDate_CLO, ActionTaken_MD, ApprovalDate_MD, Opinion_MD,
                        SentToCO, CO_OutwardNo, CO_OutwardDate, CO_Remarks, Opinion_CLO,
                        CO_FeasibilityReceived, CO_FeasibilityDate, CO_ActionTaken, CO_ApprovalOutwardNo, CO_ApprovalDate, CO_ClosedDocumentPath,
                        CO_WP_CaseStatus_Option, CO_WP_CaseNumber, CO_WP_Year, CO_WP_HighCourtBench, CO_WP_EntrustmentNo, CO_WP_EntrustmentDate, CO_WP_AdvocateName, CO_WP_StayGranted, CO_WP_StayApprovalNo, CO_WP_StayNature, CO_WP_StayDate, CO_WP_StayOrderPath, CO_WP_StayRemark,
                        CO_IsWorkmanReinstated, CO_ReinstatedSubjectToWP, CO_Reinstatement_StayGranted, CO_Reinstatement_StayApprovalNo, CO_Reinstatement_StayNature, CO_Reinstatement_StayDate, CO_Reinstatement_StayOrderPath, CO_Reinstatement_StayRemark, CO_ReinstatementApprovalIssued, CO_ReinstatementApprovalDate, CO_Reinstatement_ApprovalNo, CO_Reinstatement_ApprovalCopyPath,
                        CO_WP_Status, CO_WP_Outcome, CO_WP_OutcomeRemark, CO_WP_OutcomeOutwardNo, CO_WP_OutcomeOutwardDate, CO_WP_ActionTaken, CO_WP_JudgmentCopyPath,
                        CO_WA_CaseStatus_Option, CO_WA_CaseNumber, CO_WA_Year, CO_WA_HighCourtBench, CO_WA_EntrustmentNo, CO_WA_EntrustmentDate, CO_WA_AdvocateName, CO_WA_StayGranted, CO_WA_StayApprovalNo, CO_WA_StayNature, CO_WA_StayDate, CO_WA_StayOrderPath, CO_WA_StayRemark, CO_WA_Status, CO_WA_Outcome, CO_WA_OutcomeRemark, CO_WA_OutcomeOutwardNo, CO_WA_OutcomeOutwardDate, CO_WA_ActionTaken,
                        CO_Disposal_Nature, CO_Disposal_CommSentToDivision, CO_Disposal_OutwardNo, CO_Disposal_Date, CO_Disposal_Decision, CO_Disposal_ApprovalOutwardNo, CO_Disposal_ApprovalDate,
                        CO_FurtherAppeal_Status_Option, CO_FurtherAppeal_CaseNumber, CO_FurtherAppeal_Year, CO_FurtherAppeal_EntrustmentNo, CO_FurtherAppeal_EntrustmentDate, CO_FurtherAppeal_AdvocateName, CO_FurtherAppeal_CaseStatus, CO_FurtherAppeal_DisposalOutwardNo, CO_FurtherAppeal_DisposalDate,
                        EmployeeNo, PFNumber, Designation, WorkingStatus, LegalRepresentativeName, LRRelationship,
                        IsWorkman, IsWorkmanRemark, NatureOfCase, NatureOfMisconduct, ClaimFiledOn, DelayInFiling, ClaimDetails,
                        Remarks, CreatedDate, CreatedBy
                    ) VALUES (
                        @ParentCaseID, @Parent_CaseNumber, @Parent_CaseYear, @Parent_CourtID,
                        @Parent_CourtName, @Parent_CaseType, @Parent_CaseStatus, @Parent_PetitionerName,
                        @DivisionID, @CaseNumber, @CaseYear, @CourtID, @OtherCourtDetails, @CNRNumber, @EstCode, @CaseTypeCode,
                        @CaseType, @CaseStatus, @PetitionerName,
                        @EntrustmentNo, @EntrustmentDate, @AdvocateID, @AdvocateName,
                        @IsDocumentSent, @DocumentSent_OutwardNo, @DocumentSent_OutwardDate,
                        @IsObjectionFiled, @ObjectionFiled_OutwardNo, @ObjectionFiled_OutwardDate,
                        @IsEvidenceFiled, @CurrentStage, @NextHearingDate,
                        @DisposalMode, @DisposalDate, @DisposalResult, @FavorRemark, @FavorOutwardDate, @JudgmentCopyPath, @ClaimPetitionPath,
                        @LokAdalat_COApprovalRequired, @LokAdalat_OutwardNo, @LokAdalat_Date, @LokAdalatDocumentPath,
                        @DE_HistorySheet, @DE_HistorySheetPath, @DE_ObjectionsFiled, @DE_ObjectionsRemark,
                        @DE_DocumentsMarked, @DE_DocumentsRemark, @DE_Order,
                        @DE_EO_IsBasedOnDocuments, @DE_EO_Name, @DE_EO_Designation,
                        @DE_Reporter_IsBasedOnDocuments, @DE_Reporter_Name, @DE_Reporter_Designation,
                        @DE_Other_IsBasedOnDocuments, @DE_Other_Name, @DE_Other_Designation,
                        @CC_CaseDisposedDate, @CC_PublicationDate, @CC_AppliedDate, @CC_IssuedDate, @CC_ReceivedDate, @CC_Remarks,
                        @AwardDetails, @Opinion_Advocate, @Opinion_LO, @Opinion_DC,
                        @ActionTaken_LO, @ApprovalDate_LO, @ActionTaken_DyCLO, @ApprovalDate_DyCLO, @Opinion_DyCLO,
                        @ActionTaken_CLO, @ApprovalDate_CLO, @ActionTaken_MD, @ApprovalDate_MD, @Opinion_MD,
                        @SentToCO, @CO_OutwardNo, @CO_OutwardDate, @CO_Remarks, @Opinion_CLO,
                        @CO_FeasibilityReceived, @CO_FeasibilityDate, @CO_ActionTaken, @CO_ApprovalOutwardNo, @CO_ApprovalDate, @CO_ClosedDocumentPath,
                        @CO_WP_CaseStatus_Option, @CO_WP_CaseNumber, @CO_WP_Year, @CO_WP_HighCourtBench, @CO_WP_EntrustmentNo, @CO_WP_EntrustmentDate, @CO_WP_AdvocateName, @CO_WP_StayGranted, @CO_WP_StayApprovalNo, @CO_WP_StayNature, @CO_WP_StayDate, @CO_WP_StayOrderPath, @CO_WP_StayRemark,
                        @CO_IsWorkmanReinstated, @CO_ReinstatedSubjectToWP, @CO_Reinstatement_StayGranted, @CO_Reinstatement_StayApprovalNo, @CO_Reinstatement_StayNature, @CO_Reinstatement_StayDate, @CO_Reinstatement_StayOrderPath, @CO_Reinstatement_StayRemark, @CO_ReinstatementApprovalIssued, @CO_ReinstatementApprovalDate, @CO_Reinstatement_ApprovalNo, @CO_Reinstatement_ApprovalCopyPath,
                        @CO_WP_Status, @CO_WP_Outcome, @CO_WP_OutcomeRemark, @CO_WP_OutcomeOutwardNo, @CO_WP_OutcomeOutwardDate, @CO_WP_ActionTaken, @CO_WP_JudgmentCopyPath,
                        @CO_WA_CaseStatus_Option, @CO_WA_CaseNumber, @CO_WA_Year, @CO_WA_HighCourtBench, @CO_WA_EntrustmentNo, @CO_WA_EntrustmentDate, @CO_WA_AdvocateName, @CO_WA_StayGranted, @CO_WA_StayApprovalNo, @CO_WA_StayNature, @CO_WA_StayDate, @CO_WA_StayOrderPath, @CO_WA_StayRemark, @CO_WA_Status, @CO_WA_Outcome, @CO_WA_OutcomeRemark, @CO_WA_OutcomeOutwardNo, @CO_WA_OutcomeOutwardDate, @CO_WA_ActionTaken,
                        @CO_Disposal_Nature, @CO_Disposal_CommSentToDivision, @CO_Disposal_OutwardNo, @CO_Disposal_Date, @CO_Disposal_Decision, @CO_Disposal_ApprovalOutwardNo, @CO_Disposal_ApprovalDate,
                        @CO_FurtherAppeal_Status_Option, @CO_FurtherAppeal_CaseNumber, @CO_FurtherAppeal_Year, @CO_FurtherAppeal_EntrustmentNo, @CO_FurtherAppeal_EntrustmentDate, @CO_FurtherAppeal_AdvocateName, @CO_FurtherAppeal_CaseStatus, @CO_FurtherAppeal_DisposalOutwardNo, @CO_FurtherAppeal_DisposalDate,
                        @EmployeeNo, @PFNumber, @Designation, @WorkingStatus, @LegalRepresentativeName, @LRRelationship,
                        @IsWorkman, @IsWorkmanRemark, @NatureOfCase, @NatureOfMisconduct, @ClaimFiledOn, @DelayInFiling, @ClaimDetails,
                        @Remarks, GETDATE(), @CreatedBy
                    );
                    SELECT CAST(SCOPE_IDENTITY() AS INT);";

                var parameters = BuildParameters(model);
                var result = _db.ExecuteScalar(query, parameters, connection, transaction);
                int arisingId = result != null ? Convert.ToInt32(result) : 0;
                if (arisingId > 0)
                {
                    SaveEnclosedDocuments(arisingId, model.EnclosedDocuments, connection, transaction);
                }

                transaction.Commit();
                return arisingId;
            }
            catch
            {
                transaction.Rollback();
                throw;  // Re-throw to let controller handle error display
            }
        }

        /// <summary>
        /// Update an existing Arising Application with full ACID transaction support.
        /// </summary>
        public void UpdateArisingApplication(ArisingApplication model)
        {
            using var connection = _db.GetConnection();
            connection.Open();
            using var transaction = connection.BeginTransaction(IsolationLevel.ReadCommitted);

            try
            {
                string query = @"
                    UPDATE LABOUR_ARISING_APPLICATIONS SET
                        ParentCaseID = @ParentCaseID,
                        Parent_CaseNumber = @Parent_CaseNumber,
                        Parent_CaseYear = @Parent_CaseYear,
                        Parent_CourtID = @Parent_CourtID,
                        Parent_CourtName = @Parent_CourtName,
                        Parent_CaseType = @Parent_CaseType,
                        Parent_CaseStatus = @Parent_CaseStatus,
                        Parent_PetitionerName = @Parent_PetitionerName,
                        DivisionID = @DivisionID,
                        CaseNumber = @CaseNumber,
                        CaseYear = @CaseYear,
                        CourtID = @CourtID,
                        OtherCourtDetails = @OtherCourtDetails,
                        CNRNumber = @CNRNumber,
                        EstCode = @EstCode,
                        CaseTypeCode = @CaseTypeCode,
                        CaseType = @CaseType,
                        CaseStatus = @CaseStatus,
                        PetitionerName = @PetitionerName,
                        EntrustmentNo = @EntrustmentNo,
                        EntrustmentDate = @EntrustmentDate,
                        AdvocateID = @AdvocateID,
                        AdvocateName = @AdvocateName,
                        IsDocumentSent = @IsDocumentSent,
                        DocumentSent_OutwardNo = @DocumentSent_OutwardNo,
                        DocumentSent_OutwardDate = @DocumentSent_OutwardDate,
                        IsObjectionFiled = @IsObjectionFiled,
                        ObjectionFiled_OutwardNo = @ObjectionFiled_OutwardNo,
                        ObjectionFiled_OutwardDate = @ObjectionFiled_OutwardDate,
                        IsEvidenceFiled = @IsEvidenceFiled,
                        CurrentStage = @CurrentStage,
                        NextHearingDate = @NextHearingDate,
                        DisposalMode = @DisposalMode,
                        DisposalDate = @DisposalDate,
                        DisposalResult = @DisposalResult,
                        FavorRemark = @FavorRemark,
                        FavorOutwardDate = @FavorOutwardDate,
                        JudgmentCopyPath = COALESCE(@JudgmentCopyPath, JudgmentCopyPath),
                        ClaimPetitionPath = COALESCE(@ClaimPetitionPath, ClaimPetitionPath),
                        LokAdalat_COApprovalRequired = @LokAdalat_COApprovalRequired,
                        LokAdalat_OutwardNo = @LokAdalat_OutwardNo,
                        LokAdalat_Date = @LokAdalat_Date,
                        LokAdalatDocumentPath = COALESCE(@LokAdalatDocumentPath, LokAdalatDocumentPath),
                        DE_HistorySheet = @DE_HistorySheet,
                        DE_HistorySheetPath = COALESCE(@DE_HistorySheetPath, DE_HistorySheetPath),
                        DE_ObjectionsFiled = @DE_ObjectionsFiled,
                        DE_ObjectionsRemark = @DE_ObjectionsRemark,
                        DE_DocumentsMarked = @DE_DocumentsMarked,
                        DE_DocumentsRemark = @DE_DocumentsRemark,
                        DE_Order = @DE_Order,
                        DE_EO_IsBasedOnDocuments = @DE_EO_IsBasedOnDocuments,
                        DE_EO_Name = @DE_EO_Name,
                        DE_EO_Designation = @DE_EO_Designation,
                        DE_Reporter_IsBasedOnDocuments = @DE_Reporter_IsBasedOnDocuments,
                        DE_Reporter_Name = @DE_Reporter_Name,
                        DE_Reporter_Designation = @DE_Reporter_Designation,
                        DE_Other_IsBasedOnDocuments = @DE_Other_IsBasedOnDocuments,
                        DE_Other_Name = @DE_Other_Name,
                        DE_Other_Designation = @DE_Other_Designation,
                        CC_CaseDisposedDate = @CC_CaseDisposedDate,
                        CC_PublicationDate = @CC_PublicationDate,
                        CC_AppliedDate = @CC_AppliedDate,
                        CC_IssuedDate = @CC_IssuedDate,
                        CC_ReceivedDate = @CC_ReceivedDate,
                        CC_Remarks = @CC_Remarks,
                        AwardDetails = @AwardDetails,
                        Opinion_Advocate = @Opinion_Advocate,
                        Opinion_DC = @Opinion_DC,
                        -- Per-role action fields (ISNULL preserves data from other roles)
                        ActionTaken_LO = ISNULL(NULLIF(@ActionTaken_LO, ''), ActionTaken_LO),
                        ApprovalDate_LO = ISNULL(@ApprovalDate_LO, ApprovalDate_LO),
                        Opinion_LO = ISNULL(NULLIF(@Opinion_LO, ''), Opinion_LO),
                        ActionTaken_DyCLO = ISNULL(NULLIF(@ActionTaken_DyCLO, ''), ActionTaken_DyCLO),
                        ApprovalDate_DyCLO = ISNULL(@ApprovalDate_DyCLO, ApprovalDate_DyCLO),
                        Opinion_DyCLO = ISNULL(NULLIF(@Opinion_DyCLO, ''), Opinion_DyCLO),
                        ActionTaken_CLO = ISNULL(NULLIF(@ActionTaken_CLO, ''), ActionTaken_CLO),
                        ApprovalDate_CLO = ISNULL(@ApprovalDate_CLO, ApprovalDate_CLO),
                        Opinion_CLO = ISNULL(NULLIF(@Opinion_CLO, ''), Opinion_CLO),
                        ActionTaken_MD = ISNULL(NULLIF(@ActionTaken_MD, ''), ActionTaken_MD),
                        ApprovalDate_MD = ISNULL(@ApprovalDate_MD, ApprovalDate_MD),
                        Opinion_MD = ISNULL(NULLIF(@Opinion_MD, ''), Opinion_MD),
                        SentToCO = @SentToCO,
                        CO_OutwardNo = @CO_OutwardNo,
                        CO_OutwardDate = @CO_OutwardDate,
                        CO_Remarks = @CO_Remarks,
                        CO_FeasibilityReceived = ISNULL(@CO_FeasibilityReceived, CO_FeasibilityReceived),
                        CO_FeasibilityDate = ISNULL(@CO_FeasibilityDate, CO_FeasibilityDate),
                        CO_ActionTaken = ISNULL(NULLIF(@CO_ActionTaken, ''), CO_ActionTaken),
                        CO_ApprovalOutwardNo = ISNULL(NULLIF(@CO_ApprovalOutwardNo, ''), CO_ApprovalOutwardNo),
                        CO_ApprovalDate = ISNULL(@CO_ApprovalDate, CO_ApprovalDate),
                        CO_ClosedDocumentPath = COALESCE(@CO_ClosedDocumentPath, CO_ClosedDocumentPath),
                        CO_WP_CaseStatus_Option = ISNULL(NULLIF(@CO_WP_CaseStatus_Option, ''), CO_WP_CaseStatus_Option),
                        CO_WP_CaseNumber = ISNULL(NULLIF(@CO_WP_CaseNumber, ''), CO_WP_CaseNumber),
                        CO_WP_Year = ISNULL(@CO_WP_Year, CO_WP_Year),
                        CO_WP_HighCourtBench = ISNULL(NULLIF(@CO_WP_HighCourtBench, ''), CO_WP_HighCourtBench),
                        CO_WP_EntrustmentNo = ISNULL(NULLIF(@CO_WP_EntrustmentNo, ''), CO_WP_EntrustmentNo),
                        CO_WP_EntrustmentDate = ISNULL(@CO_WP_EntrustmentDate, CO_WP_EntrustmentDate),
                        CO_WP_AdvocateName = ISNULL(NULLIF(@CO_WP_AdvocateName, ''), CO_WP_AdvocateName),
                        CO_WP_StayGranted = ISNULL(@CO_WP_StayGranted, CO_WP_StayGranted),
                        CO_WP_StayApprovalNo = ISNULL(NULLIF(@CO_WP_StayApprovalNo, ''), CO_WP_StayApprovalNo),
                        CO_WP_StayNature = ISNULL(NULLIF(@CO_WP_StayNature, ''), CO_WP_StayNature),
                        CO_WP_StayDate = ISNULL(@CO_WP_StayDate, CO_WP_StayDate),
                        CO_WP_StayOrderPath = COALESCE(@CO_WP_StayOrderPath, CO_WP_StayOrderPath),
                        CO_WP_StayRemark = ISNULL(NULLIF(@CO_WP_StayRemark, ''), CO_WP_StayRemark),
                        CO_IsWorkmanReinstated = ISNULL(@CO_IsWorkmanReinstated, CO_IsWorkmanReinstated),
                        CO_ReinstatedSubjectToWP = ISNULL(@CO_ReinstatedSubjectToWP, CO_ReinstatedSubjectToWP),
                        CO_Reinstatement_StayGranted = ISNULL(@CO_Reinstatement_StayGranted, CO_Reinstatement_StayGranted),
                        CO_Reinstatement_StayApprovalNo = ISNULL(NULLIF(@CO_Reinstatement_StayApprovalNo, ''), CO_Reinstatement_StayApprovalNo),
                        CO_Reinstatement_StayNature = ISNULL(NULLIF(@CO_Reinstatement_StayNature, ''), CO_Reinstatement_StayNature),
                        CO_Reinstatement_StayDate = ISNULL(@CO_Reinstatement_StayDate, CO_Reinstatement_StayDate),
                        CO_Reinstatement_StayOrderPath = COALESCE(@CO_Reinstatement_StayOrderPath, CO_Reinstatement_StayOrderPath),
                        CO_Reinstatement_StayRemark = ISNULL(NULLIF(@CO_Reinstatement_StayRemark, ''), CO_Reinstatement_StayRemark),
                        CO_ReinstatementApprovalIssued = ISNULL(@CO_ReinstatementApprovalIssued, CO_ReinstatementApprovalIssued),
                        CO_ReinstatementApprovalDate = ISNULL(@CO_ReinstatementApprovalDate, CO_ReinstatementApprovalDate),
                        CO_Reinstatement_ApprovalNo = ISNULL(NULLIF(@CO_Reinstatement_ApprovalNo, ''), CO_Reinstatement_ApprovalNo),
                        CO_Reinstatement_ApprovalCopyPath = COALESCE(@CO_Reinstatement_ApprovalCopyPath, CO_Reinstatement_ApprovalCopyPath),
                        CO_WP_Status = ISNULL(NULLIF(@CO_WP_Status, ''), CO_WP_Status),
                        CO_WP_Outcome = ISNULL(NULLIF(@CO_WP_Outcome, ''), CO_WP_Outcome),
                        CO_WP_OutcomeRemark = ISNULL(NULLIF(@CO_WP_OutcomeRemark, ''), CO_WP_OutcomeRemark),
                        CO_WP_OutcomeOutwardNo = ISNULL(NULLIF(@CO_WP_OutcomeOutwardNo, ''), CO_WP_OutcomeOutwardNo),
                        CO_WP_OutcomeOutwardDate = ISNULL(@CO_WP_OutcomeOutwardDate, CO_WP_OutcomeOutwardDate),
                        CO_WP_ActionTaken = ISNULL(NULLIF(@CO_WP_ActionTaken, ''), CO_WP_ActionTaken),
                        CO_WP_JudgmentCopyPath = COALESCE(@CO_WP_JudgmentCopyPath, CO_WP_JudgmentCopyPath),
                        CO_WA_CaseStatus_Option = ISNULL(NULLIF(@CO_WA_CaseStatus_Option, ''), CO_WA_CaseStatus_Option),
                        CO_WA_CaseNumber = ISNULL(NULLIF(@CO_WA_CaseNumber, ''), CO_WA_CaseNumber),
                        CO_WA_Year = ISNULL(@CO_WA_Year, CO_WA_Year),
                        CO_WA_HighCourtBench = ISNULL(NULLIF(@CO_WA_HighCourtBench, ''), CO_WA_HighCourtBench),
                        CO_WA_EntrustmentNo = ISNULL(NULLIF(@CO_WA_EntrustmentNo, ''), CO_WA_EntrustmentNo),
                        CO_WA_EntrustmentDate = ISNULL(@CO_WA_EntrustmentDate, CO_WA_EntrustmentDate),
                        CO_WA_AdvocateName = ISNULL(NULLIF(@CO_WA_AdvocateName, ''), CO_WA_AdvocateName),
                        CO_WA_StayGranted = ISNULL(@CO_WA_StayGranted, CO_WA_StayGranted),
                        CO_WA_StayApprovalNo = ISNULL(NULLIF(@CO_WA_StayApprovalNo, ''), CO_WA_StayApprovalNo),
                        CO_WA_StayNature = ISNULL(NULLIF(@CO_WA_StayNature, ''), CO_WA_StayNature),
                        CO_WA_StayDate = ISNULL(@CO_WA_StayDate, CO_WA_StayDate),
                        CO_WA_StayOrderPath = COALESCE(@CO_WA_StayOrderPath, CO_WA_StayOrderPath),
                        CO_WA_StayRemark = ISNULL(NULLIF(@CO_WA_StayRemark, ''), CO_WA_StayRemark),
                        CO_WA_Status = ISNULL(NULLIF(@CO_WA_Status, ''), CO_WA_Status),
                        CO_WA_Outcome = ISNULL(NULLIF(@CO_WA_Outcome, ''), CO_WA_Outcome),
                        CO_WA_OutcomeRemark = ISNULL(NULLIF(@CO_WA_OutcomeRemark, ''), CO_WA_OutcomeRemark),
                        CO_WA_OutcomeOutwardNo = ISNULL(NULLIF(@CO_WA_OutcomeOutwardNo, ''), CO_WA_OutcomeOutwardNo),
                        CO_WA_OutcomeOutwardDate = ISNULL(@CO_WA_OutcomeOutwardDate, CO_WA_OutcomeOutwardDate),
                        CO_WA_ActionTaken = ISNULL(NULLIF(@CO_WA_ActionTaken, ''), CO_WA_ActionTaken),
                        CO_Disposal_Nature = ISNULL(NULLIF(@CO_Disposal_Nature, ''), CO_Disposal_Nature),
                        CO_Disposal_CommSentToDivision = ISNULL(NULLIF(@CO_Disposal_CommSentToDivision, ''), CO_Disposal_CommSentToDivision),
                        CO_Disposal_OutwardNo = ISNULL(NULLIF(@CO_Disposal_OutwardNo, ''), CO_Disposal_OutwardNo),
                        CO_Disposal_Date = ISNULL(@CO_Disposal_Date, CO_Disposal_Date),
                        CO_Disposal_Decision = ISNULL(NULLIF(@CO_Disposal_Decision, ''), CO_Disposal_Decision),
                        CO_Disposal_ApprovalOutwardNo = ISNULL(NULLIF(@CO_Disposal_ApprovalOutwardNo, ''), CO_Disposal_ApprovalOutwardNo),
                        CO_Disposal_ApprovalDate = ISNULL(@CO_Disposal_ApprovalDate, CO_Disposal_ApprovalDate),
                        CO_FurtherAppeal_Status_Option = ISNULL(NULLIF(@CO_FurtherAppeal_Status_Option, ''), CO_FurtherAppeal_Status_Option),
                        CO_FurtherAppeal_CaseNumber = ISNULL(NULLIF(@CO_FurtherAppeal_CaseNumber, ''), CO_FurtherAppeal_CaseNumber),
                        CO_FurtherAppeal_Year = ISNULL(@CO_FurtherAppeal_Year, CO_FurtherAppeal_Year),
                        CO_FurtherAppeal_EntrustmentNo = ISNULL(NULLIF(@CO_FurtherAppeal_EntrustmentNo, ''), CO_FurtherAppeal_EntrustmentNo),
                        CO_FurtherAppeal_EntrustmentDate = ISNULL(@CO_FurtherAppeal_EntrustmentDate, CO_FurtherAppeal_EntrustmentDate),
                        CO_FurtherAppeal_AdvocateName = ISNULL(NULLIF(@CO_FurtherAppeal_AdvocateName, ''), CO_FurtherAppeal_AdvocateName),
                        CO_FurtherAppeal_CaseStatus = ISNULL(NULLIF(@CO_FurtherAppeal_CaseStatus, ''), CO_FurtherAppeal_CaseStatus),
                        CO_FurtherAppeal_DisposalOutwardNo = ISNULL(NULLIF(@CO_FurtherAppeal_DisposalOutwardNo, ''), CO_FurtherAppeal_DisposalOutwardNo),
                        CO_FurtherAppeal_DisposalDate = ISNULL(@CO_FurtherAppeal_DisposalDate, CO_FurtherAppeal_DisposalDate),
                        EmployeeNo = @EmployeeNo,
                        PFNumber = @PFNumber,
                        Designation = @Designation,
                        WorkingStatus = @WorkingStatus,
                        LegalRepresentativeName = @LegalRepresentativeName,
                        LRRelationship = @LRRelationship,
                        IsWorkman = @IsWorkman,
                        IsWorkmanRemark = @IsWorkmanRemark,
                        NatureOfCase = @NatureOfCase,
                        NatureOfMisconduct = @NatureOfMisconduct,
                        ClaimFiledOn = @ClaimFiledOn,
                        DelayInFiling = @DelayInFiling,
                        ClaimDetails = @ClaimDetails,
                        Remarks = @Remarks,
                        ModifiedDate = GETDATE(),
                        ModifiedBy = @ModifiedBy
                    WHERE ArisingID = @ArisingID";

                var parameters = BuildParameters(model, includeId: true);
                _db.ExecuteNonQuery(query, parameters, connection, transaction);
                SaveEnclosedDocuments(model.ArisingID, model.EnclosedDocuments, connection, transaction);

                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        /// <summary>
        /// Retrieve a single Arising Application by its ID.
        /// Joins to LABOUR_COURTS and MVC_DIVISION for display names.
        /// </summary>
        public ArisingApplication? GetById(int arisingId)
        {
            string query = @"
                SELECT a.*, 
                       c.CourtName, c.Location AS CourtLocation,
                       d.DivisionNameEnglish AS DivisionName
                FROM LABOUR_ARISING_APPLICATIONS a
                LEFT JOIN LABOUR_COURTS c ON a.CourtID = c.CourtID
                LEFT JOIN DIVISION_MASTER d ON a.DivisionID = d.DivisionID
                WHERE a.ArisingID = @ArisingID";

            var dt = _db.ExecuteQuery(query, new[] { new SqlParameter("@ArisingID", arisingId) });
            if (dt.Rows.Count == 0) return null;

            return MapToModel(dt.Rows[0]);
        }

        /// <summary>
        /// Get all Arising Applications linked to a specific parent Labour Case (by ParentCaseID or CaseNumber/Year).
        /// </summary>
        public IEnumerable<ArisingApplication> GetByParentCaseId(int parentCaseId, string? caseNumber = null, int? caseYear = null)
        {
            string query = @"
                SELECT a.*, 
                       c.CourtName, c.Location AS CourtLocation,
                       d.DivisionNameEnglish AS DivisionName
                FROM LABOUR_ARISING_APPLICATIONS a
                LEFT JOIN LABOUR_COURTS c ON a.CourtID = c.CourtID
                LEFT JOIN DIVISION_MASTER d ON a.DivisionID = d.DivisionID
                WHERE (a.ParentCaseID > 0 AND a.ParentCaseID = @ParentCaseID)
                   OR (@CaseNumber IS NOT NULL AND @CaseNumber <> '' AND LTRIM(RTRIM(a.Parent_CaseNumber)) = LTRIM(RTRIM(@CaseNumber))
                       AND (@CaseYear IS NULL OR @CaseYear = 0 OR a.Parent_CaseYear = @CaseYear))
                ORDER BY a.CreatedDate DESC";

            var dt = _db.ExecuteQuery(query, new[] { 
                new SqlParameter("@ParentCaseID", parentCaseId),
                new SqlParameter("@CaseNumber", (object?)caseNumber ?? DBNull.Value),
                new SqlParameter("@CaseYear", (object?)caseYear ?? DBNull.Value)
            });
            return dt.AsEnumerable().Select(row => MapToModel(row)).ToList();
        }

        /// <summary>
        /// Get paginated list of all Arising Applications, with optional division filter and search.
        /// </summary>
        public IEnumerable<ArisingApplication> GetAll(int divisionId = 0, int pageNumber = 1, int pageSize = 10, string? search = null)
        {
            string whereClause = "WHERE 1=1";
            var paramsList = new List<SqlParameter>();

            if (divisionId > 0)
            {
                whereClause += " AND a.DivisionID = @DivisionID";
                paramsList.Add(new SqlParameter("@DivisionID", divisionId));
            }
            else
            {
                whereClause += " AND (a.SentToCO = 1 OR a.CO_ActionTaken IS NOT NULL OR a.CO_WP_CaseNumber IS NOT NULL)";
            }

            if (!string.IsNullOrEmpty(search))
            {
                whereClause += @" AND (
                    a.CaseNumber LIKE @Search OR
                    a.Parent_CaseNumber LIKE @Search OR
                    a.CO_WP_CaseNumber LIKE @Search OR
                    a.CO_WA_CaseNumber LIKE @Search OR
                    a.CO_ActionTaken LIKE @Search OR
                    a.PetitionerName LIKE @Search OR
                    a.CurrentStage LIKE @Search OR
                    a.EntrustmentNo LIKE @Search OR
                    a.AdvocateName LIKE @Search OR
                    a.CO_WP_AdvocateName LIKE @Search
                )";
                paramsList.Add(new SqlParameter("@Search", $"%{search}%"));
            }

            int offset = (pageNumber - 1) * pageSize;
            paramsList.Add(new SqlParameter("@Offset", offset));
            paramsList.Add(new SqlParameter("@PageSize", pageSize));

            string query = $@"
                SELECT a.*, 
                       c.CourtName, c.Location AS CourtLocation,
                       d.DivisionNameEnglish AS DivisionName
                FROM LABOUR_ARISING_APPLICATIONS a
                LEFT JOIN LABOUR_COURTS c ON a.CourtID = c.CourtID
                LEFT JOIN DIVISION_MASTER d ON a.DivisionID = d.DivisionID
                {whereClause}
                ORDER BY a.CreatedDate DESC
                OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY";

            var dt = _db.ExecuteQuery(query, paramsList.ToArray());
            return dt.AsEnumerable().Select(row => MapToModel(row)).ToList();
        }

        /// <summary>
        /// Get total count of Arising Applications for pagination.
        /// </summary>
        public int GetTotalCount(int divisionId = 0, string? search = null)
        {
            string whereClause = "WHERE 1=1";
            var paramsList = new List<SqlParameter>();

            if (divisionId > 0)
            {
                whereClause += " AND DivisionID = @DivisionID";
                paramsList.Add(new SqlParameter("@DivisionID", divisionId));
            }
            else
            {
                whereClause += " AND (SentToCO = 1 OR CO_ActionTaken IS NOT NULL OR CO_WP_CaseNumber IS NOT NULL)";
            }

            if (!string.IsNullOrEmpty(search))
            {
                whereClause += @" AND (
                    CaseNumber LIKE @Search OR
                    Parent_CaseNumber LIKE @Search OR
                    CO_WP_CaseNumber LIKE @Search OR
                    CO_WA_CaseNumber LIKE @Search OR
                    CO_ActionTaken LIKE @Search OR
                    PetitionerName LIKE @Search OR
                    CurrentStage LIKE @Search OR
                    EntrustmentNo LIKE @Search OR
                    AdvocateName LIKE @Search OR
                    CO_WP_AdvocateName LIKE @Search
                )";
                paramsList.Add(new SqlParameter("@Search", $"%{search}%"));
            }

            string query = $"SELECT COUNT(*) FROM LABOUR_ARISING_APPLICATIONS {whereClause}";
            var result = _db.ExecuteScalar(query, paramsList.ToArray());
            return result != null ? Convert.ToInt32(result) : 0;
        }

        /// <summary>
        /// Get Arising Applications by hearing date for dashboard display.
        /// </summary>
        public IEnumerable<ArisingApplication> GetByHearingDate(DateTime date, int divisionId, DateTime? endDate = null)
        {
            var paramsList = new List<SqlParameter>();
            string dateFilter;

            if (endDate.HasValue)
            {
                dateFilter = "a.NextHearingDate BETWEEN @StartDate AND @EndDate";
                paramsList.Add(new SqlParameter("@StartDate", date.Date));
                paramsList.Add(new SqlParameter("@EndDate", endDate.Value.Date));
            }
            else
            {
                dateFilter = "CAST(a.NextHearingDate AS DATE) = @HearingDate";
                paramsList.Add(new SqlParameter("@HearingDate", date.Date));
            }

            string divFilter = "";
            if (divisionId > 0)
            {
                divFilter = " AND a.DivisionID = @DivisionID";
                paramsList.Add(new SqlParameter("@DivisionID", divisionId));
            }
            else
            {
                divFilter = " AND a.SentToCO = 1";
            }

            string query = $@"
                SELECT a.*, 
                       c.CourtName, c.Location AS CourtLocation,
                       d.DivisionNameEnglish AS DivisionName
                FROM LABOUR_ARISING_APPLICATIONS a
                LEFT JOIN LABOUR_COURTS c ON a.CourtID = c.CourtID
                LEFT JOIN DIVISION_MASTER d ON a.DivisionID = d.DivisionID
                WHERE {dateFilter}{divFilter}
                ORDER BY a.NextHearingDate";

            var dt = _db.ExecuteQuery(query, paramsList.ToArray());
            return dt.AsEnumerable().Select(row => MapToModel(row)).ToList();
        }

        // ---- PRIVATE HELPERS ----

        private SqlParameter[] BuildParameters(ArisingApplication m, bool includeId = false)
        {
            var list = new List<SqlParameter>
            {
                new SqlParameter("@ParentCaseID", (object?)m.ParentCaseID ?? DBNull.Value),
                new SqlParameter("@Parent_CaseNumber", (object?)m.Parent_CaseNumber ?? DBNull.Value),
                new SqlParameter("@Parent_CaseYear", (object?)m.Parent_CaseYear ?? DBNull.Value),
                new SqlParameter("@Parent_CourtID", (object?)m.Parent_CourtID ?? DBNull.Value),
                new SqlParameter("@Parent_CourtName", (object?)m.Parent_CourtName ?? DBNull.Value),
                new SqlParameter("@Parent_CaseType", (object?)m.Parent_CaseType ?? DBNull.Value),
                new SqlParameter("@Parent_CaseStatus", (object?)m.Parent_CaseStatus ?? DBNull.Value),
                new SqlParameter("@Parent_PetitionerName", (object?)m.Parent_PetitionerName ?? DBNull.Value),
                new SqlParameter("@DivisionID", m.DivisionID),
                new SqlParameter("@CaseNumber", (object?)m.CaseNumber ?? DBNull.Value),
                new SqlParameter("@CaseYear", (object?)m.CaseYear ?? DBNull.Value),
                new SqlParameter("@CourtID", (object?)m.CourtID ?? DBNull.Value),
                new SqlParameter("@OtherCourtDetails", (object?)m.OtherCourtDetails ?? DBNull.Value),
                new SqlParameter("@CNRNumber", (object?)m.CNRNumber ?? DBNull.Value),
                new SqlParameter("@EstCode", (object?)m.EstCode ?? DBNull.Value),
                new SqlParameter("@CaseTypeCode", (object?)m.CaseTypeCode ?? DBNull.Value),
                new SqlParameter("@CaseType", (object?)m.CaseType ?? DBNull.Value),
                new SqlParameter("@CaseStatus", (object?)m.CaseStatus ?? DBNull.Value),
                new SqlParameter("@PetitionerName", (object?)m.PetitionerName ?? DBNull.Value),
                new SqlParameter("@EntrustmentNo", (object?)m.EntrustmentNo ?? DBNull.Value),
                new SqlParameter("@EntrustmentDate", (object?)m.EntrustmentDate ?? DBNull.Value),
                new SqlParameter("@AdvocateID", (object?)m.AdvocateID ?? DBNull.Value),
                new SqlParameter("@AdvocateName", (object?)m.AdvocateName ?? DBNull.Value),
                new SqlParameter("@IsDocumentSent", m.IsDocumentSent),
                new SqlParameter("@DocumentSent_OutwardNo", (object?)m.DocumentSent_OutwardNo ?? DBNull.Value),
                new SqlParameter("@DocumentSent_OutwardDate", (object?)m.DocumentSent_OutwardDate ?? DBNull.Value),
                new SqlParameter("@IsObjectionFiled", m.IsObjectionFiled),
                new SqlParameter("@ObjectionFiled_OutwardNo", (object?)m.ObjectionFiled_OutwardNo ?? DBNull.Value),
                new SqlParameter("@ObjectionFiled_OutwardDate", (object?)m.ObjectionFiled_OutwardDate ?? DBNull.Value),
                new SqlParameter("@IsEvidenceFiled", m.IsEvidenceFiled),
                new SqlParameter("@CurrentStage", (object?)m.CurrentStage ?? DBNull.Value),
                new SqlParameter("@NextHearingDate", (object?)m.NextHearingDate ?? DBNull.Value),
                new SqlParameter("@DisposalMode", (object?)m.DisposalMode ?? DBNull.Value),
                new SqlParameter("@DisposalDate", (object?)m.DisposalDate ?? DBNull.Value),
                new SqlParameter("@DisposalResult", (object?)m.DisposalResult ?? DBNull.Value),
                new SqlParameter("@FavorRemark", (object?)m.FavorRemark ?? DBNull.Value),
                new SqlParameter("@FavorOutwardDate", (object?)m.FavorOutwardDate ?? DBNull.Value),
                new SqlParameter("@JudgmentCopyPath", (object?)m.JudgmentCopyPath ?? DBNull.Value),
                new SqlParameter("@LokAdalat_COApprovalRequired", m.LokAdalat_COApprovalRequired),
                new SqlParameter("@LokAdalat_OutwardNo", (object?)m.LokAdalat_OutwardNo ?? DBNull.Value),
                new SqlParameter("@LokAdalat_Date", (object?)m.LokAdalat_Date ?? DBNull.Value),
                new SqlParameter("@LokAdalatDocumentPath", (object?)m.LokAdalatDocumentPath ?? DBNull.Value),
                new SqlParameter("@DE_HistorySheet", m.DE_HistorySheet),
                new SqlParameter("@DE_HistorySheetPath", (object?)m.DE_HistorySheetPath ?? DBNull.Value),
                new SqlParameter("@DE_ObjectionsFiled", m.DE_ObjectionsFiled),
                new SqlParameter("@DE_ObjectionsRemark", (object?)m.DE_ObjectionsRemark ?? DBNull.Value),
                new SqlParameter("@DE_DocumentsMarked", m.DE_DocumentsMarked),
                new SqlParameter("@DE_DocumentsRemark", (object?)m.DE_DocumentsRemark ?? DBNull.Value),
                new SqlParameter("@DE_Order", (object?)m.DE_Order ?? DBNull.Value),
                new SqlParameter("@DE_EO_IsBasedOnDocuments", m.DE_EO_IsBasedOnDocuments),
                new SqlParameter("@DE_EO_Name", (object?)m.DE_EO_Name ?? DBNull.Value),
                new SqlParameter("@DE_EO_Designation", (object?)m.DE_EO_Designation ?? DBNull.Value),
                new SqlParameter("@DE_Reporter_IsBasedOnDocuments", m.DE_Reporter_IsBasedOnDocuments),
                new SqlParameter("@DE_Reporter_Name", (object?)m.DE_Reporter_Name ?? DBNull.Value),
                new SqlParameter("@DE_Reporter_Designation", (object?)m.DE_Reporter_Designation ?? DBNull.Value),
                new SqlParameter("@DE_Other_IsBasedOnDocuments", m.DE_Other_IsBasedOnDocuments),
                new SqlParameter("@DE_Other_Name", (object?)m.DE_Other_Name ?? DBNull.Value),
                new SqlParameter("@DE_Other_Designation", (object?)m.DE_Other_Designation ?? DBNull.Value),
                new SqlParameter("@CC_CaseDisposedDate", (object?)m.CC_CaseDisposedDate ?? DBNull.Value),
                new SqlParameter("@CC_PublicationDate", (object?)m.CC_PublicationDate ?? DBNull.Value),
                new SqlParameter("@CC_AppliedDate", (object?)m.CC_AppliedDate ?? DBNull.Value),
                new SqlParameter("@CC_IssuedDate", (object?)m.CC_IssuedDate ?? DBNull.Value),
                new SqlParameter("@CC_ReceivedDate", (object?)m.CC_ReceivedDate ?? DBNull.Value),
                new SqlParameter("@CC_Remarks", (object?)m.CC_Remarks ?? DBNull.Value),
                new SqlParameter("@AwardDetails", (object?)m.AwardDetails ?? DBNull.Value),
                new SqlParameter("@Opinion_Advocate", (object?)m.Opinion_Advocate ?? DBNull.Value),
                new SqlParameter("@Opinion_LO", (object?)m.Opinion_LO ?? DBNull.Value),
                new SqlParameter("@Opinion_DC", (object?)m.Opinion_DC ?? DBNull.Value),
                new SqlParameter("@ActionTaken_LO", (object?)m.ActionTaken_LO ?? DBNull.Value),
                new SqlParameter("@ApprovalDate_LO", (object?)m.ApprovalDate_LO ?? DBNull.Value),
                new SqlParameter("@ActionTaken_DyCLO", (object?)m.ActionTaken_DyCLO ?? DBNull.Value),
                new SqlParameter("@ApprovalDate_DyCLO", (object?)m.ApprovalDate_DyCLO ?? DBNull.Value),
                new SqlParameter("@Opinion_DyCLO", (object?)m.Opinion_DyCLO ?? DBNull.Value),
                new SqlParameter("@ActionTaken_CLO", (object?)m.ActionTaken_CLO ?? DBNull.Value),
                new SqlParameter("@ApprovalDate_CLO", (object?)m.ApprovalDate_CLO ?? DBNull.Value),
                new SqlParameter("@ActionTaken_MD", (object?)m.ActionTaken_MD ?? DBNull.Value),
                new SqlParameter("@ApprovalDate_MD", (object?)m.ApprovalDate_MD ?? DBNull.Value),
                new SqlParameter("@Opinion_MD", (object?)m.Opinion_MD ?? DBNull.Value),
                new SqlParameter("@SentToCO", m.SentToCO),
                new SqlParameter("@CO_OutwardNo", (object?)m.CO_OutwardNo ?? DBNull.Value),
                new SqlParameter("@CO_OutwardDate", (object?)m.CO_OutwardDate ?? DBNull.Value),
                new SqlParameter("@CO_Remarks", (object?)m.CO_Remarks ?? DBNull.Value),
                new SqlParameter("@Opinion_CLO", (object?)m.Opinion_CLO ?? DBNull.Value),

                // CO Action Fields
                new SqlParameter("@CO_FeasibilityReceived", (object?)m.CO_FeasibilityReceived ?? DBNull.Value),
                new SqlParameter("@CO_FeasibilityDate", (object?)m.CO_FeasibilityDate ?? DBNull.Value),
                new SqlParameter("@CO_ActionTaken", (object?)m.CO_ActionTaken ?? DBNull.Value),
                new SqlParameter("@CO_ApprovalOutwardNo", (object?)m.CO_ApprovalOutwardNo ?? DBNull.Value),
                new SqlParameter("@CO_ApprovalDate", (object?)m.CO_ApprovalDate ?? DBNull.Value),
                new SqlParameter("@CO_ClosedDocumentPath", (object?)m.CO_ClosedDocumentPath ?? DBNull.Value),

                new SqlParameter("@CO_WP_CaseStatus_Option", (object?)m.CO_WP_CaseStatus_Option ?? DBNull.Value),
                new SqlParameter("@CO_WP_CaseNumber", (object?)m.CO_WP_CaseNumber ?? DBNull.Value),
                new SqlParameter("@CO_WP_Year", (object?)m.CO_WP_Year ?? DBNull.Value),
                new SqlParameter("@CO_WP_HighCourtBench", (object?)m.CO_WP_HighCourtBench ?? DBNull.Value),
                new SqlParameter("@CO_WP_EntrustmentNo", (object?)m.CO_WP_EntrustmentNo ?? DBNull.Value),
                new SqlParameter("@CO_WP_EntrustmentDate", (object?)m.CO_WP_EntrustmentDate ?? DBNull.Value),
                new SqlParameter("@CO_WP_AdvocateName", (object?)m.CO_WP_AdvocateName ?? DBNull.Value),
                new SqlParameter("@CO_WP_StayGranted", (object?)m.CO_WP_StayGranted ?? DBNull.Value),
                new SqlParameter("@CO_WP_StayApprovalNo", (object?)m.CO_WP_StayApprovalNo ?? DBNull.Value),
                new SqlParameter("@CO_WP_StayNature", (object?)m.CO_WP_StayNature ?? DBNull.Value),
                new SqlParameter("@CO_WP_StayDate", (object?)m.CO_WP_StayDate ?? DBNull.Value),
                new SqlParameter("@CO_WP_StayOrderPath", (object?)m.CO_WP_StayOrderPath ?? DBNull.Value),
                new SqlParameter("@CO_WP_StayRemark", (object?)m.CO_WP_StayRemark ?? DBNull.Value),

                new SqlParameter("@CO_IsWorkmanReinstated", m.CO_IsWorkmanReinstated),
                new SqlParameter("@CO_ReinstatedSubjectToWP", (object?)m.CO_ReinstatedSubjectToWP ?? DBNull.Value),
                new SqlParameter("@CO_Reinstatement_StayGranted", (object?)m.CO_Reinstatement_StayGranted ?? DBNull.Value),
                new SqlParameter("@CO_Reinstatement_StayApprovalNo", (object?)m.CO_Reinstatement_StayApprovalNo ?? DBNull.Value),
                new SqlParameter("@CO_Reinstatement_StayNature", (object?)m.CO_Reinstatement_StayNature ?? DBNull.Value),
                new SqlParameter("@CO_Reinstatement_StayDate", (object?)m.CO_Reinstatement_StayDate ?? DBNull.Value),
                new SqlParameter("@CO_Reinstatement_StayOrderPath", (object?)m.CO_Reinstatement_StayOrderPath ?? DBNull.Value),
                new SqlParameter("@CO_Reinstatement_StayRemark", (object?)m.CO_Reinstatement_StayRemark ?? DBNull.Value),
                new SqlParameter("@CO_ReinstatementApprovalIssued", (object?)m.CO_ReinstatementApprovalIssued ?? DBNull.Value),
                new SqlParameter("@CO_ReinstatementApprovalDate", (object?)m.CO_ReinstatementApprovalDate ?? DBNull.Value),
                new SqlParameter("@CO_Reinstatement_ApprovalNo", (object?)m.CO_Reinstatement_ApprovalNo ?? DBNull.Value),
                new SqlParameter("@CO_Reinstatement_ApprovalCopyPath", (object?)m.CO_Reinstatement_ApprovalCopyPath ?? DBNull.Value),

                new SqlParameter("@CO_WP_Status", (object?)m.CO_WP_Status ?? DBNull.Value),
                new SqlParameter("@CO_WP_Outcome", (object?)m.CO_WP_Outcome ?? DBNull.Value),
                new SqlParameter("@CO_WP_OutcomeRemark", (object?)m.CO_WP_OutcomeRemark ?? DBNull.Value),
                new SqlParameter("@CO_WP_OutcomeOutwardNo", (object?)m.CO_WP_OutcomeOutwardNo ?? DBNull.Value),
                new SqlParameter("@CO_WP_OutcomeOutwardDate", (object?)m.CO_WP_OutcomeOutwardDate ?? DBNull.Value),
                new SqlParameter("@CO_WP_ActionTaken", (object?)m.CO_WP_ActionTaken ?? DBNull.Value),
                new SqlParameter("@CO_WP_JudgmentCopyPath", (object?)m.CO_WP_JudgmentCopyPath ?? DBNull.Value),

                new SqlParameter("@CO_WA_CaseStatus_Option", (object?)m.CO_WA_CaseStatus_Option ?? DBNull.Value),
                new SqlParameter("@CO_WA_CaseNumber", (object?)m.CO_WA_CaseNumber ?? DBNull.Value),
                new SqlParameter("@CO_WA_Year", (object?)m.CO_WA_Year ?? DBNull.Value),
                new SqlParameter("@CO_WA_HighCourtBench", (object?)m.CO_WA_HighCourtBench ?? DBNull.Value),
                new SqlParameter("@CO_WA_EntrustmentNo", (object?)m.CO_WA_EntrustmentNo ?? DBNull.Value),
                new SqlParameter("@CO_WA_EntrustmentDate", (object?)m.CO_WA_EntrustmentDate ?? DBNull.Value),
                new SqlParameter("@CO_WA_AdvocateName", (object?)m.CO_WA_AdvocateName ?? DBNull.Value),
                new SqlParameter("@CO_WA_StayGranted", (object?)m.CO_WA_StayGranted ?? DBNull.Value),
                new SqlParameter("@CO_WA_StayApprovalNo", (object?)m.CO_WA_StayApprovalNo ?? DBNull.Value),
                new SqlParameter("@CO_WA_StayNature", (object?)m.CO_WA_StayNature ?? DBNull.Value),
                new SqlParameter("@CO_WA_StayDate", (object?)m.CO_WA_StayDate ?? DBNull.Value),
                new SqlParameter("@CO_WA_StayOrderPath", (object?)m.CO_WA_StayOrderPath ?? DBNull.Value),
                new SqlParameter("@CO_WA_StayRemark", (object?)m.CO_WA_StayRemark ?? DBNull.Value),
                new SqlParameter("@CO_WA_Status", (object?)m.CO_WA_Status ?? DBNull.Value),
                new SqlParameter("@CO_WA_Outcome", (object?)m.CO_WA_Outcome ?? DBNull.Value),
                new SqlParameter("@CO_WA_OutcomeRemark", (object?)m.CO_WA_OutcomeRemark ?? DBNull.Value),
                new SqlParameter("@CO_WA_OutcomeOutwardNo", (object?)m.CO_WA_OutcomeOutwardNo ?? DBNull.Value),
                new SqlParameter("@CO_WA_OutcomeOutwardDate", (object?)m.CO_WA_OutcomeOutwardDate ?? DBNull.Value),
                new SqlParameter("@CO_WA_ActionTaken", (object?)m.CO_WA_ActionTaken ?? DBNull.Value),

                new SqlParameter("@CO_Disposal_Nature", (object?)m.CO_Disposal_Nature ?? DBNull.Value),
                new SqlParameter("@CO_Disposal_CommSentToDivision", (object?)m.CO_Disposal_CommSentToDivision ?? DBNull.Value),
                new SqlParameter("@CO_Disposal_OutwardNo", (object?)m.CO_Disposal_OutwardNo ?? DBNull.Value),
                new SqlParameter("@CO_Disposal_Date", (object?)m.CO_Disposal_Date ?? DBNull.Value),
                new SqlParameter("@CO_Disposal_Decision", (object?)m.CO_Disposal_Decision ?? DBNull.Value),
                new SqlParameter("@CO_Disposal_ApprovalOutwardNo", (object?)m.CO_Disposal_ApprovalOutwardNo ?? DBNull.Value),
                new SqlParameter("@CO_Disposal_ApprovalDate", (object?)m.CO_Disposal_ApprovalDate ?? DBNull.Value),

                new SqlParameter("@CO_FurtherAppeal_Status_Option", (object?)m.CO_FurtherAppeal_Status_Option ?? DBNull.Value),
                new SqlParameter("@CO_FurtherAppeal_CaseNumber", (object?)m.CO_FurtherAppeal_CaseNumber ?? DBNull.Value),
                new SqlParameter("@CO_FurtherAppeal_Year", (object?)m.CO_FurtherAppeal_Year ?? DBNull.Value),
                new SqlParameter("@CO_FurtherAppeal_EntrustmentNo", (object?)m.CO_FurtherAppeal_EntrustmentNo ?? DBNull.Value),
                new SqlParameter("@CO_FurtherAppeal_EntrustmentDate", (object?)m.CO_FurtherAppeal_EntrustmentDate ?? DBNull.Value),
                new SqlParameter("@CO_FurtherAppeal_AdvocateName", (object?)m.CO_FurtherAppeal_AdvocateName ?? DBNull.Value),
                new SqlParameter("@CO_FurtherAppeal_CaseStatus", (object?)m.CO_FurtherAppeal_CaseStatus ?? DBNull.Value),
                new SqlParameter("@CO_FurtherAppeal_DisposalOutwardNo", (object?)m.CO_FurtherAppeal_DisposalOutwardNo ?? DBNull.Value),
                new SqlParameter("@CO_FurtherAppeal_DisposalDate", (object?)m.CO_FurtherAppeal_DisposalDate ?? DBNull.Value),
                new SqlParameter("@Remarks", (object?)m.Remarks ?? DBNull.Value),
                new SqlParameter("@EmployeeNo", (object?)m.EmployeeNo ?? DBNull.Value),
                new SqlParameter("@PFNumber", (object?)m.PFNumber ?? DBNull.Value),
                new SqlParameter("@Designation", (object?)m.Designation ?? DBNull.Value),
                new SqlParameter("@WorkingStatus", (object?)m.WorkingStatus ?? DBNull.Value),
                new SqlParameter("@LegalRepresentativeName", (object?)m.LegalRepresentativeName ?? DBNull.Value),
                new SqlParameter("@LRRelationship", (object?)m.LRRelationship ?? DBNull.Value),
                new SqlParameter("@IsWorkman", m.IsWorkman),
                new SqlParameter("@IsWorkmanRemark", (object?)m.IsWorkmanRemark ?? DBNull.Value),
                new SqlParameter("@NatureOfCase", (object?)m.NatureOfCase ?? DBNull.Value),
                new SqlParameter("@NatureOfMisconduct", (object?)m.NatureOfMisconduct ?? DBNull.Value),
                new SqlParameter("@ClaimFiledOn", (object?)m.ClaimFiledOn ?? DBNull.Value),
                new SqlParameter("@DelayInFiling", (object?)m.DelayInFiling ?? DBNull.Value),
                new SqlParameter("@ClaimDetails", (object?)m.ClaimDetails ?? DBNull.Value),
                new SqlParameter("@ClaimPetitionPath", (object?)m.ClaimPetitionPath ?? DBNull.Value),
                new SqlParameter("@CreatedBy", (object?)m.CreatedBy ?? DBNull.Value),
                new SqlParameter("@ModifiedBy", (object?)m.ModifiedBy ?? DBNull.Value),
            };

            if (includeId)
            {
                list.Add(new SqlParameter("@ArisingID", m.ArisingID));
            }

            return list.ToArray();
        }

        private ArisingApplication MapToModel(DataRow row)
        {
            return new ArisingApplication
            {
                ArisingID = Convert.ToInt32(row["ArisingID"]),
                ParentCaseID = row["ParentCaseID"] != DBNull.Value ? Convert.ToInt32(row["ParentCaseID"]) : null,
                Parent_CaseNumber = row["Parent_CaseNumber"]?.ToString(),
                Parent_CaseYear = row["Parent_CaseYear"] != DBNull.Value ? Convert.ToInt32(row["Parent_CaseYear"]) : null,
                Parent_CourtID = row["Parent_CourtID"] != DBNull.Value ? Convert.ToInt32(row["Parent_CourtID"]) : null,
                Parent_CourtName = row["Parent_CourtName"]?.ToString(),
                Parent_CaseType = row["Parent_CaseType"]?.ToString(),
                Parent_CaseStatus = row["Parent_CaseStatus"]?.ToString(),
                Parent_PetitionerName = row["Parent_PetitionerName"]?.ToString(),
                DivisionID = Convert.ToInt32(row["DivisionID"]),
                DivisionName = row.Table.Columns.Contains("DivisionName") ? row["DivisionName"]?.ToString() : null,
                CaseNumber = row["CaseNumber"]?.ToString() ?? "",
                CaseYear = row["CaseYear"] != DBNull.Value ? Convert.ToInt32(row["CaseYear"]) : null,
                CourtID = row["CourtID"] != DBNull.Value ? Convert.ToInt32(row["CourtID"]) : null,
                CourtName = row.Table.Columns.Contains("CourtName") ? row["CourtName"]?.ToString() : null,
                OtherCourtDetails = row["OtherCourtDetails"]?.ToString(),
                CNRNumber = row.Table.Columns.Contains("CNRNumber") ? row["CNRNumber"]?.ToString() : null,
                EstCode = row.Table.Columns.Contains("EstCode") ? row["EstCode"]?.ToString() : null,
                CaseTypeCode = row.Table.Columns.Contains("CaseTypeCode") ? row["CaseTypeCode"]?.ToString() : null,
                CaseType = row["CaseType"]?.ToString() ?? "Arising Application",
                CaseStatus = row["CaseStatus"]?.ToString() ?? "Pending",
                PetitionerName = row["PetitionerName"]?.ToString(),
                EntrustmentNo = row["EntrustmentNo"]?.ToString(),
                EntrustmentDate = row["EntrustmentDate"] != DBNull.Value ? Convert.ToDateTime(row["EntrustmentDate"]) : null,
                AdvocateID = row["AdvocateID"] != DBNull.Value ? Convert.ToInt32(row["AdvocateID"]) : null,
                AdvocateName = row["AdvocateName"]?.ToString(),
                IsDocumentSent = row["IsDocumentSent"] != DBNull.Value && Convert.ToBoolean(row["IsDocumentSent"]),
                DocumentSent_OutwardNo = row["DocumentSent_OutwardNo"]?.ToString(),
                DocumentSent_OutwardDate = row["DocumentSent_OutwardDate"] != DBNull.Value ? Convert.ToDateTime(row["DocumentSent_OutwardDate"]) : null,
                IsObjectionFiled = row["IsObjectionFiled"] != DBNull.Value && Convert.ToBoolean(row["IsObjectionFiled"]),
                ObjectionFiled_OutwardNo = row["ObjectionFiled_OutwardNo"]?.ToString(),
                ObjectionFiled_OutwardDate = row["ObjectionFiled_OutwardDate"] != DBNull.Value ? Convert.ToDateTime(row["ObjectionFiled_OutwardDate"]) : null,
                IsEvidenceFiled = row["IsEvidenceFiled"] != DBNull.Value && Convert.ToBoolean(row["IsEvidenceFiled"]),
                CurrentStage = row["CurrentStage"]?.ToString(),
                NextHearingDate = row["NextHearingDate"] != DBNull.Value ? Convert.ToDateTime(row["NextHearingDate"]) : null,
                DisposalMode = row["DisposalMode"]?.ToString(),
                DisposalDate = row["DisposalDate"] != DBNull.Value ? Convert.ToDateTime(row["DisposalDate"]) : null,
                DisposalResult = row["DisposalResult"]?.ToString(),
                FavorRemark = row["FavorRemark"]?.ToString(),
                FavorOutwardDate = row["FavorOutwardDate"] != DBNull.Value ? Convert.ToDateTime(row["FavorOutwardDate"]) : null,
                JudgmentCopyPath = row["JudgmentCopyPath"]?.ToString(),
                LokAdalat_COApprovalRequired = row["LokAdalat_COApprovalRequired"] != DBNull.Value && Convert.ToBoolean(row["LokAdalat_COApprovalRequired"]),
                LokAdalat_OutwardNo = row["LokAdalat_OutwardNo"]?.ToString(),
                LokAdalat_Date = row["LokAdalat_Date"] != DBNull.Value ? Convert.ToDateTime(row["LokAdalat_Date"]) : null,
                LokAdalatDocumentPath = row["LokAdalatDocumentPath"]?.ToString(),
                DE_HistorySheet = row["DE_HistorySheet"] != DBNull.Value && Convert.ToBoolean(row["DE_HistorySheet"]),
                DE_HistorySheetPath = row["DE_HistorySheetPath"]?.ToString(),
                DE_ObjectionsFiled = row["DE_ObjectionsFiled"] != DBNull.Value && Convert.ToBoolean(row["DE_ObjectionsFiled"]),
                DE_ObjectionsRemark = row["DE_ObjectionsRemark"]?.ToString(),
                DE_DocumentsMarked = row["DE_DocumentsMarked"] != DBNull.Value && Convert.ToBoolean(row["DE_DocumentsMarked"]),
                DE_DocumentsRemark = row["DE_DocumentsRemark"]?.ToString(),
                DE_Order = row["DE_Order"]?.ToString(),
                DE_EO_IsBasedOnDocuments = row["DE_EO_IsBasedOnDocuments"] != DBNull.Value && Convert.ToBoolean(row["DE_EO_IsBasedOnDocuments"]),
                DE_EO_Name = row["DE_EO_Name"]?.ToString(),
                DE_EO_Designation = row["DE_EO_Designation"]?.ToString(),
                DE_Reporter_IsBasedOnDocuments = row["DE_Reporter_IsBasedOnDocuments"] != DBNull.Value && Convert.ToBoolean(row["DE_Reporter_IsBasedOnDocuments"]),
                DE_Reporter_Name = row["DE_Reporter_Name"]?.ToString(),
                DE_Reporter_Designation = row["DE_Reporter_Designation"]?.ToString(),
                DE_Other_IsBasedOnDocuments = row["DE_Other_IsBasedOnDocuments"] != DBNull.Value && Convert.ToBoolean(row["DE_Other_IsBasedOnDocuments"]),
                DE_Other_Name = row["DE_Other_Name"]?.ToString(),
                DE_Other_Designation = row["DE_Other_Designation"]?.ToString(),
                CC_CaseDisposedDate = row["CC_CaseDisposedDate"] != DBNull.Value ? Convert.ToDateTime(row["CC_CaseDisposedDate"]) : null,
                CC_PublicationDate = row["CC_PublicationDate"] != DBNull.Value ? Convert.ToDateTime(row["CC_PublicationDate"]) : null,
                CC_AppliedDate = row["CC_AppliedDate"] != DBNull.Value ? Convert.ToDateTime(row["CC_AppliedDate"]) : null,
                CC_IssuedDate = row["CC_IssuedDate"] != DBNull.Value ? Convert.ToDateTime(row["CC_IssuedDate"]) : null,
                CC_ReceivedDate = row["CC_ReceivedDate"] != DBNull.Value ? Convert.ToDateTime(row["CC_ReceivedDate"]) : null,
                CC_Remarks = row["CC_Remarks"]?.ToString(),
                AwardDetails = row["AwardDetails"]?.ToString(),
                Opinion_Advocate = row["Opinion_Advocate"]?.ToString(),
                Opinion_LO = row["Opinion_LO"]?.ToString(),
                Opinion_DC = row["Opinion_DC"]?.ToString(),
                ActionTaken_LO = row.Table.Columns.Contains("ActionTaken_LO") ? row["ActionTaken_LO"]?.ToString() : null,
                ApprovalDate_LO = row.Table.Columns.Contains("ApprovalDate_LO") && row["ApprovalDate_LO"] != DBNull.Value ? Convert.ToDateTime(row["ApprovalDate_LO"]) : null,
                ActionTaken_DyCLO = row.Table.Columns.Contains("ActionTaken_DyCLO") ? row["ActionTaken_DyCLO"]?.ToString() : null,
                ApprovalDate_DyCLO = row.Table.Columns.Contains("ApprovalDate_DyCLO") && row["ApprovalDate_DyCLO"] != DBNull.Value ? Convert.ToDateTime(row["ApprovalDate_DyCLO"]) : null,
                Opinion_DyCLO = row.Table.Columns.Contains("Opinion_DyCLO") ? row["Opinion_DyCLO"]?.ToString() : null,
                ActionTaken_CLO = row.Table.Columns.Contains("ActionTaken_CLO") ? row["ActionTaken_CLO"]?.ToString() : null,
                ApprovalDate_CLO = row.Table.Columns.Contains("ApprovalDate_CLO") && row["ApprovalDate_CLO"] != DBNull.Value ? Convert.ToDateTime(row["ApprovalDate_CLO"]) : null,
                ActionTaken_MD = row.Table.Columns.Contains("ActionTaken_MD") ? row["ActionTaken_MD"]?.ToString() : null,
                ApprovalDate_MD = row.Table.Columns.Contains("ApprovalDate_MD") && row["ApprovalDate_MD"] != DBNull.Value ? Convert.ToDateTime(row["ApprovalDate_MD"]) : null,
                Opinion_MD = row.Table.Columns.Contains("Opinion_MD") ? row["Opinion_MD"]?.ToString() : null,
                SentToCO = row["SentToCO"] != DBNull.Value && Convert.ToBoolean(row["SentToCO"]),
                CO_Remarks = row["CO_Remarks"]?.ToString(),
                Opinion_CLO = row.Table.Columns.Contains("Opinion_CLO") ? row["Opinion_CLO"]?.ToString() : null,

                CO_FeasibilityReceived = row["CO_FeasibilityReceived"] != DBNull.Value ? Convert.ToBoolean(row["CO_FeasibilityReceived"]) : null,
                CO_FeasibilityDate = row["CO_FeasibilityDate"] != DBNull.Value ? Convert.ToDateTime(row["CO_FeasibilityDate"]) : null,
                CO_ActionTaken = row["CO_ActionTaken"]?.ToString(),
                CO_ApprovalOutwardNo = row["CO_ApprovalOutwardNo"]?.ToString(),
                CO_ApprovalDate = row["CO_ApprovalDate"] != DBNull.Value ? Convert.ToDateTime(row["CO_ApprovalDate"]) : null,
                CO_ClosedDocumentPath = row["CO_ClosedDocumentPath"]?.ToString(),

                CO_WP_CaseStatus_Option = row["CO_WP_CaseStatus_Option"]?.ToString(),
                CO_WP_CaseNumber = row["CO_WP_CaseNumber"]?.ToString(),
                CO_WP_Year = row["CO_WP_Year"] != DBNull.Value ? Convert.ToInt32(row["CO_WP_Year"]) : null,
                CO_WP_HighCourtBench = row["CO_WP_HighCourtBench"]?.ToString(),
                CO_WP_EntrustmentNo = row["CO_WP_EntrustmentNo"]?.ToString(),
                CO_WP_EntrustmentDate = row["CO_WP_EntrustmentDate"] != DBNull.Value ? Convert.ToDateTime(row["CO_WP_EntrustmentDate"]) : null,
                CO_WP_AdvocateName = row["CO_WP_AdvocateName"]?.ToString(),
                CO_WP_StayGranted = row["CO_WP_StayGranted"] != DBNull.Value ? Convert.ToBoolean(row["CO_WP_StayGranted"]) : null,
                CO_WP_StayApprovalNo = row["CO_WP_StayApprovalNo"]?.ToString(),
                CO_WP_StayNature = row["CO_WP_StayNature"]?.ToString(),
                CO_WP_StayDate = row["CO_WP_StayDate"] != DBNull.Value ? Convert.ToDateTime(row["CO_WP_StayDate"]) : null,
                CO_WP_StayOrderPath = row["CO_WP_StayOrderPath"]?.ToString(),
                CO_WP_StayRemark = row["CO_WP_StayRemark"]?.ToString(),

                CO_IsWorkmanReinstated = row["CO_IsWorkmanReinstated"] != DBNull.Value && Convert.ToBoolean(row["CO_IsWorkmanReinstated"]),
                CO_ReinstatedSubjectToWP = row["CO_ReinstatedSubjectToWP"]?.ToString(),
                CO_Reinstatement_StayGranted = row["CO_Reinstatement_StayGranted"] != DBNull.Value ? Convert.ToBoolean(row["CO_Reinstatement_StayGranted"]) : null,
                CO_Reinstatement_StayApprovalNo = row["CO_Reinstatement_StayApprovalNo"]?.ToString(),
                CO_Reinstatement_StayNature = row["CO_Reinstatement_StayNature"]?.ToString(),
                CO_Reinstatement_StayDate = row["CO_Reinstatement_StayDate"] != DBNull.Value ? Convert.ToDateTime(row["CO_Reinstatement_StayDate"]) : null,
                CO_Reinstatement_StayOrderPath = row["CO_Reinstatement_StayOrderPath"]?.ToString(),
                CO_Reinstatement_StayRemark = row["CO_Reinstatement_StayRemark"]?.ToString(),
                CO_ReinstatementApprovalIssued = row["CO_ReinstatementApprovalIssued"] != DBNull.Value ? Convert.ToBoolean(row["CO_ReinstatementApprovalIssued"]) : null,
                CO_ReinstatementApprovalDate = row["CO_ReinstatementApprovalDate"] != DBNull.Value ? Convert.ToDateTime(row["CO_ReinstatementApprovalDate"]) : null,
                CO_Reinstatement_ApprovalNo = row["CO_Reinstatement_ApprovalNo"]?.ToString(),
                CO_Reinstatement_ApprovalCopyPath = row["CO_Reinstatement_ApprovalCopyPath"]?.ToString(),

                CO_WP_Status = row["CO_WP_Status"]?.ToString(),
                CO_WP_Outcome = row["CO_WP_Outcome"]?.ToString(),
                CO_WP_OutcomeRemark = row["CO_WP_OutcomeRemark"]?.ToString(),
                CO_WP_OutcomeOutwardNo = row["CO_WP_OutcomeOutwardNo"]?.ToString(),
                CO_WP_OutcomeOutwardDate = row["CO_WP_OutcomeOutwardDate"] != DBNull.Value ? Convert.ToDateTime(row["CO_WP_OutcomeOutwardDate"]) : null,
                CO_WP_ActionTaken = row["CO_WP_ActionTaken"]?.ToString(),
                CO_WP_JudgmentCopyPath = row.Table.Columns.Contains("CO_WP_JudgmentCopyPath") ? row["CO_WP_JudgmentCopyPath"]?.ToString() : null,

                CO_WA_CaseStatus_Option = row["CO_WA_CaseStatus_Option"]?.ToString(),
                CO_WA_CaseNumber = row["CO_WA_CaseNumber"]?.ToString(),
                CO_WA_Year = row["CO_WA_Year"] != DBNull.Value ? Convert.ToInt32(row["CO_WA_Year"]) : null,
                CO_WA_HighCourtBench = row["CO_WA_HighCourtBench"]?.ToString(),
                CO_WA_EntrustmentNo = row["CO_WA_EntrustmentNo"]?.ToString(),
                CO_WA_EntrustmentDate = row["CO_WA_EntrustmentDate"] != DBNull.Value ? Convert.ToDateTime(row["CO_WA_EntrustmentDate"]) : null,
                CO_WA_AdvocateName = row["CO_WA_AdvocateName"]?.ToString(),
                CO_WA_StayGranted = row["CO_WA_StayGranted"] != DBNull.Value ? Convert.ToBoolean(row["CO_WA_StayGranted"]) : null,
                CO_WA_StayApprovalNo = row["CO_WA_StayApprovalNo"]?.ToString(),
                CO_WA_StayNature = row["CO_WA_StayNature"]?.ToString(),
                CO_WA_StayDate = row["CO_WA_StayDate"] != DBNull.Value ? Convert.ToDateTime(row["CO_WA_StayDate"]) : null,
                CO_WA_StayOrderPath = row["CO_WA_StayOrderPath"]?.ToString(),
                CO_WA_StayRemark = row["CO_WA_StayRemark"]?.ToString(),
                CO_WA_Status = row["CO_WA_Status"]?.ToString(),
                CO_WA_Outcome = row["CO_WA_Outcome"]?.ToString(),
                CO_WA_OutcomeRemark = row["CO_WA_OutcomeRemark"]?.ToString(),
                CO_WA_OutcomeOutwardNo = row["CO_WA_OutcomeOutwardNo"]?.ToString(),
                CO_WA_OutcomeOutwardDate = row["CO_WA_OutcomeOutwardDate"] != DBNull.Value ? Convert.ToDateTime(row["CO_WA_OutcomeOutwardDate"]) : null,
                CO_WA_ActionTaken = row["CO_WA_ActionTaken"]?.ToString(),

                CO_Disposal_Nature = row["CO_Disposal_Nature"]?.ToString(),
                CO_Disposal_CommSentToDivision = row["CO_Disposal_CommSentToDivision"] != DBNull.Value ? Convert.ToBoolean(row["CO_Disposal_CommSentToDivision"]) : null,
                CO_Disposal_OutwardNo = row["CO_Disposal_OutwardNo"]?.ToString(),
                CO_Disposal_Date = row["CO_Disposal_Date"] != DBNull.Value ? Convert.ToDateTime(row["CO_Disposal_Date"]) : null,
                CO_Disposal_Decision = row["CO_Disposal_Decision"]?.ToString(),
                CO_Disposal_ApprovalOutwardNo = row["CO_Disposal_ApprovalOutwardNo"]?.ToString(),
                CO_Disposal_ApprovalDate = row["CO_Disposal_ApprovalDate"] != DBNull.Value ? Convert.ToDateTime(row["CO_Disposal_ApprovalDate"]) : null,

                CO_FurtherAppeal_Status_Option = row["CO_FurtherAppeal_Status_Option"]?.ToString(),
                CO_FurtherAppeal_CaseNumber = row["CO_FurtherAppeal_CaseNumber"]?.ToString(),
                CO_FurtherAppeal_Year = row["CO_FurtherAppeal_Year"] != DBNull.Value ? Convert.ToInt32(row["CO_FurtherAppeal_Year"]) : null,
                CO_FurtherAppeal_EntrustmentNo = row["CO_FurtherAppeal_EntrustmentNo"]?.ToString(),
                CO_FurtherAppeal_EntrustmentDate = row["CO_FurtherAppeal_EntrustmentDate"] != DBNull.Value ? Convert.ToDateTime(row["CO_FurtherAppeal_EntrustmentDate"]) : null,
                CO_FurtherAppeal_AdvocateName = row["CO_FurtherAppeal_AdvocateName"]?.ToString(),
                CO_FurtherAppeal_CaseStatus = row["CO_FurtherAppeal_CaseStatus"]?.ToString(),
                CO_FurtherAppeal_DisposalOutwardNo = row["CO_FurtherAppeal_DisposalOutwardNo"]?.ToString(),
                CO_FurtherAppeal_DisposalDate = row["CO_FurtherAppeal_DisposalDate"] != DBNull.Value ? Convert.ToDateTime(row["CO_FurtherAppeal_DisposalDate"]) : null,
                EmployeeNo = row["EmployeeNo"]?.ToString(),
                PFNumber = row["PFNumber"]?.ToString(),
                Designation = row["Designation"]?.ToString(),
                WorkingStatus = row["WorkingStatus"]?.ToString(),
                LegalRepresentativeName = row["LegalRepresentativeName"]?.ToString(),
                LRRelationship = row["LRRelationship"]?.ToString(),
                IsWorkman = row["IsWorkman"] != DBNull.Value && Convert.ToBoolean(row["IsWorkman"]),
                IsWorkmanRemark = row["IsWorkmanRemark"]?.ToString(),
                NatureOfCase = row.Table.Columns.Contains("NatureOfCase") ? row["NatureOfCase"]?.ToString() : null,
                NatureOfMisconduct = row["NatureOfMisconduct"]?.ToString(),
                ClaimFiledOn = row["ClaimFiledOn"]?.ToString(),
                DelayInFiling = row["DelayInFiling"]?.ToString(),
                ClaimDetails = row["ClaimDetails"]?.ToString(),
                ClaimPetitionPath = row.Table.Columns.Contains("ClaimPetitionPath") ? row["ClaimPetitionPath"]?.ToString() : null,
                Remarks = row["Remarks"]?.ToString(),
                AwardAmount = row.Table.Columns.Contains("AwardAmount") && row["AwardAmount"] != DBNull.Value ? (decimal?)Convert.ToDecimal(row["AwardAmount"]) : null,
                Payments = GetPaymentsByArisingId(Convert.ToInt32(row["ArisingID"])),
                EnclosedDocuments = GetEnclosedDocuments(Convert.ToInt32(row["ArisingID"])),
                CreatedDate = row["CreatedDate"] != DBNull.Value ? Convert.ToDateTime(row["CreatedDate"]) : DateTime.Now,
                CreatedBy = row["CreatedBy"] != DBNull.Value ? Convert.ToInt32(row["CreatedBy"]) : null,
                ModifiedDate = row["ModifiedDate"] != DBNull.Value ? Convert.ToDateTime(row["ModifiedDate"]) : null,
                ModifiedBy = row["ModifiedBy"] != DBNull.Value ? Convert.ToInt32(row["ModifiedBy"]) : null,
            };
        }

        private void SaveEnclosedDocuments(int arisingId, List<EnclosedDocument>? docs, SqlConnection conn, SqlTransaction trans)
        {
            if (docs == null) return;
            _db.ExecuteNonQuery("DELETE FROM LABOUR_ARISING_ENCLOSED_DOCS WHERE ArisingID = @ArisingID", new[] { new SqlParameter("@ArisingID", arisingId) }, conn, trans);

            if (docs != null && docs.Count > 0)
            {
                foreach (var doc in docs)
                {
                    if (!string.IsNullOrWhiteSpace(doc.DocName))
                    {
                        string q = "INSERT INTO LABOUR_ARISING_ENCLOSED_DOCS (ArisingID, DocName, PageCount) VALUES (@ArisingID, @DocName, @PageCount)";
                        _db.ExecuteNonQuery(q, new[] {
                            new SqlParameter("@ArisingID", arisingId),
                            new SqlParameter("@DocName", doc.DocName),
                            new SqlParameter("@PageCount", (object?)doc.PageCount ?? DBNull.Value)
                        }, conn, trans);
                    }
                }
            }
        }

        private List<EnclosedDocument> GetEnclosedDocuments(int arisingId)
        {
            string query = "SELECT DocName, PageCount FROM LABOUR_ARISING_ENCLOSED_DOCS WHERE ArisingID = @ArisingID";
            var dt = _db.ExecuteQuery(query, new[] { new SqlParameter("@ArisingID", arisingId) });
            var list = new List<EnclosedDocument>();
            foreach (DataRow row in dt.Rows)
            {
                list.Add(new EnclosedDocument
                {
                    DocName = row["DocName"]?.ToString() ?? "",
                    PageCount = row["PageCount"] != DBNull.Value ? Convert.ToInt32(row["PageCount"]) : 0
                });
            }
            return list;
        }

        public void AddPayment(ArisingApplicationPayment payment)
        {
            string query = @"
                INSERT INTO LABOUR_ARISING_PAYMENTS (ArisingID, Amount, PaymentDate, ChequeNumber, ChequeDate, Remarks)
                VALUES (@ArisingID, @Amount, @PaymentDate, @ChequeNumber, @ChequeDate, @Remarks)";

            _db.ExecuteNonQuery(query, new[] {
                new SqlParameter("@ArisingID", payment.ArisingID),
                new SqlParameter("@Amount", payment.Amount),
                new SqlParameter("@PaymentDate", payment.PaymentDate),
                new SqlParameter("@ChequeNumber", (object?)payment.ChequeNumber ?? DBNull.Value),
                new SqlParameter("@ChequeDate", (object?)payment.ChequeDate ?? DBNull.Value),
                new SqlParameter("@Remarks", (object?)payment.Remarks ?? DBNull.Value)
            });
        }

        public bool DeletePayment(int paymentId)
        {
            string query = "DELETE FROM LABOUR_ARISING_PAYMENTS WHERE PaymentID = @PaymentID";
            return _db.ExecuteNonQuery(query, new[] { new SqlParameter("@PaymentID", paymentId) }) > 0;
        }

        public bool UpdateAwardAmount(int arisingId, decimal awardAmount)
        {
            string query = "UPDATE LABOUR_ARISING_APPLICATIONS SET AwardAmount = @AwardAmount WHERE ArisingID = @ArisingID";
            return _db.ExecuteNonQuery(query, new[] {
                new SqlParameter("@AwardAmount", awardAmount),
                new SqlParameter("@ArisingID", arisingId)
            }) > 0;
        }

        public List<ArisingApplicationPayment> GetPaymentsByArisingId(int arisingId)
        {
            var list = new List<ArisingApplicationPayment>();
            try
            {
                string query = "SELECT * FROM LABOUR_ARISING_PAYMENTS WHERE ArisingID = @ArisingID ORDER BY PaymentDate DESC";
                DataTable dt = _db.ExecuteQuery(query, new[] { new SqlParameter("@ArisingID", arisingId) });
                foreach (DataRow row in dt.Rows)
                {
                    list.Add(new ArisingApplicationPayment
                    {
                        PaymentID = Convert.ToInt32(row["PaymentID"]),
                        ArisingID = Convert.ToInt32(row["ArisingID"]),
                        Amount = Convert.ToDecimal(row["Amount"]),
                        PaymentDate = Convert.ToDateTime(row["PaymentDate"]),
                        ChequeNumber = row["ChequeNumber"]?.ToString(),
                        ChequeDate = row["ChequeDate"] != DBNull.Value ? (DateTime?)Convert.ToDateTime(row["ChequeDate"]) : null,
                        Remarks = row["Remarks"]?.ToString(),
                        CreatedDate = row["CreatedDate"] != DBNull.Value ? Convert.ToDateTime(row["CreatedDate"]) : DateTime.Now
                    });
                }
            }
            catch { }
            return list;
        }

        public bool DeleteArisingApplication(int arisingId)
        {
            string delPayQuery = "DELETE FROM LABOUR_ARISING_PAYMENTS WHERE ArisingID = @Id";
            _db.ExecuteNonQuery(delPayQuery, new[] { new SqlParameter("@Id", arisingId) });

            string query = "DELETE FROM LABOUR_ARISING_APPLICATIONS WHERE ArisingID = @Id";
            return _db.ExecuteNonQuery(query, new[] { new SqlParameter("@Id", arisingId) }) > 0;
        }

        public bool UpdateArisingRoleAction(int arisingId, string role, string actionTaken, DateTime? approvalDate, string opinion, int? modifiedBy)
        {
            string colAction = "";
            string colDate = "";
            string colOpinion = "";
            string extraUpdate = "";

            switch (role?.Trim().ToUpper())
            {
                case "LO":
                    colAction = "ActionTaken_LO";
                    colDate = "ApprovalDate_LO";
                    colOpinion = "Opinion_LO";
                    break;
                case "DY CLO":
                case "DYCLO":
                    colAction = "ActionTaken_DyCLO";
                    colDate = "ApprovalDate_DyCLO";
                    colOpinion = "Opinion_DyCLO";
                    break;
                case "CLO":
                    colAction = "ActionTaken_CLO";
                    colDate = "ApprovalDate_CLO";
                    colOpinion = "Opinion_CLO";
                    extraUpdate = @", CO_Disposal_Decision = @ActionTaken,
                                     CO_ApprovalDate = @ApprovalDate,
                                     CO_ActionTaken = CASE WHEN @ActionTaken = 'Approved' THEN 'Pending before Competent Authority' WHEN @ActionTaken = 'Rejected' THEN 'CLOSED' ELSE CO_ActionTaken END";
                    break;
                case "MD":
                    colAction = "ActionTaken_MD";
                    colDate = "ApprovalDate_MD";
                    colOpinion = "Opinion_MD";
                    extraUpdate = @", CO_ActionTaken = CASE WHEN @ActionTaken = 'Approved' THEN 'APPROVED' WHEN @ActionTaken = 'Rejected' THEN 'REJECTED' ELSE CO_ActionTaken END,
                                     CO_ApprovalDate = @ApprovalDate";
                    break;
                default:
                    return false;
            }

            string query = $@"
                UPDATE LABOUR_ARISING_APPLICATIONS SET
                    {colAction} = @ActionTaken,
                    {colDate} = @ApprovalDate,
                    {colOpinion} = @Opinion,
                    ModifiedDate = GETDATE(),
                    ModifiedBy = @ModifiedBy
                    {extraUpdate}
                WHERE ArisingID = @ArisingID";

            var parameters = new[]
            {
                new SqlParameter("@ArisingID", arisingId),
                new SqlParameter("@ActionTaken", (object?)actionTaken ?? DBNull.Value),
                new SqlParameter("@ApprovalDate", (object?)approvalDate ?? DBNull.Value),
                new SqlParameter("@Opinion", (object?)opinion ?? DBNull.Value),
                new SqlParameter("@ModifiedBy", (object?)modifiedBy ?? DBNull.Value)
            };

            int rows = _db.ExecuteNonQuery(query, parameters);
            return rows > 0;
        }
    }
}
