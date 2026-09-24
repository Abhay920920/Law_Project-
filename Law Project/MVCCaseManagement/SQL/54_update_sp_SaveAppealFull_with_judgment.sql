-- Update sp_SaveAppealFull to include ClaimantSCJudgmentPath
IF OBJECT_ID('dbo.sp_SaveAppealFull', 'P') IS NOT NULL
    DROP PROCEDURE dbo.sp_SaveAppealFull;
GO

CREATE PROCEDURE sp_SaveAppealFull
    @CaseID INT,
    @FeasibilityReceived BIT,
    @FeasibilityReceiptDate DATE = NULL,
    @InitialAction NVARCHAR(100) = NULL,
    @ApprovalOutwardNo NVARCHAR(50) = NULL,
    @ApprovalDate DATE = NULL,
    @InitialActionRemarks NVARCHAR(MAX) = NULL,
    @InitialActionPath1 NVARCHAR(MAX) = NULL,
    @InitialActionPath2 NVARCHAR(MAX) = NULL,
    @CorpMFANumber NVARCHAR(50) = NULL,
    @CorpMFAYear INT = NULL,
    @HighCourtBench NVARCHAR(100) = NULL,
    @OtherHighCourtBench NVARCHAR(100) = NULL,
    @IsPendingForFiling BIT = NULL,
    @CorpMFAEntrustmentNo NVARCHAR(50) = NULL,
    @CorpMFAEntrustmentDate DATE = NULL,
    @CorpMFAAdvocate NVARCHAR(200) = NULL,
    @StayGranted BIT,
    @StayComplianceOutwardNo NVARCHAR(50) = NULL,
    @StayComplianceDate DATE = NULL,
    @StayOrderPath1 NVARCHAR(MAX) = NULL,
    @StayOrderPath2 NVARCHAR(MAX) = NULL,
    @ComplianceLetterPath NVARCHAR(MAX) = NULL,
    @CorpMFAStatus NVARCHAR(50) = NULL,
    @RestorationFiled BIT,
    @RestorationDate DATE = NULL,
    @RestorationStatus NVARCHAR(100) = NULL,
    @CorpMFAOutcome NVARCHAR(100) = NULL,
    @CorpMFAActionTaken NVARCHAR(100) = NULL,
    @ClosureOutwardNo NVARCHAR(50) = NULL,
    @ClosureDate DATE = NULL,
    @CorpSCNumber NVARCHAR(50) = NULL,
    @CorpSCYear INT = NULL,
    @CorpSCEntrustmentNo NVARCHAR(50) = NULL,
    @CorpSCEntrustmentDate DATE = NULL,
    @CorpSCAdvocate NVARCHAR(200) = NULL,
    @CorpSCStatus NVARCHAR(50) = NULL,
    @CrossAppealFiledBy NVARCHAR(50) = NULL,
    @CrossMFANumber NVARCHAR(50) = NULL,
    @CrossMFAStatus NVARCHAR(100) = NULL,
    @ClaimantDivisionID INT = NULL,
    @ClaimantMVCNumber NVARCHAR(50) = NULL,
    @ClaimantMVCYear INT = NULL,
    @ClaimantMVCCurrentStatus NVARCHAR(100) = NULL,
    @ClaimantMFANumber NVARCHAR(50) = NULL,
    @ClaimantMFAYear INT = NULL,
    @ClaimantMFAEntrustmentNo NVARCHAR(50) = NULL,
    @ClaimantMFAEntrustmentDate DATE = NULL,
    @ClaimantMFAAdvocate NVARCHAR(200) = NULL,
    @ClaimantMFAStatus NVARCHAR(50) = NULL,
    @ClaimantMFADecision NVARCHAR(50) = NULL,
    @ClaimantActionTaken NVARCHAR(100) = NULL,
    @ClaimantApprovalNo NVARCHAR(50) = NULL,
    @ClaimantApprovalDate DATE = NULL,
    @ClaimantSCNumber NVARCHAR(50) = NULL,
    @ClaimantSCDiaryNumber NVARCHAR(50) = NULL,
    @ClaimantSCYear INT = NULL,
    @ClaimantSLPYear INT = NULL,
    @ClaimantSCFiledBy NVARCHAR(50) = NULL,
    @ClaimantSCEntrustmentNo NVARCHAR(50) = NULL,
    @ClaimantSCEntrustmentDate DATE = NULL,
    @ClaimantSCAdvocate NVARCHAR(200) = NULL,
    @ClaimantSCStatus NVARCHAR(50) = NULL,
    @IsClaimantSCAppeal BIT = NULL,
    @IsClaimantSCPending BIT = NULL,
    @ClaimantSCOutcome NVARCHAR(100) = NULL,
    @ClaimantSCActionTaken NVARCHAR(100) = NULL,
    @ClaimantSCClosureNo NVARCHAR(50) = NULL,
    @ClaimantSCClosureDate DATE = NULL,
    @ClaimantSCJudgmentPath NVARCHAR(MAX) = NULL,
    @FinalComplianceStatus NVARCHAR(100) = NULL,
    @AmountDeposited DECIMAL(18,2) = NULL,
    @FinalComplianceDate DATE = NULL,
    @FinalRemarks NVARCHAR(500) = NULL,
    @ConnectedCasesXml XML = NULL
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRY
        BEGIN TRANSACTION;
        IF EXISTS (SELECT 1 FROM APPEAL_DETAILS WHERE CaseID = @CaseID)
        BEGIN
            UPDATE APPEAL_DETAILS SET 
                FeasibilityReceived = @FeasibilityReceived, FeasibilityReceiptDate = @FeasibilityReceiptDate, InitialAction = @InitialAction,
                ApprovalOutwardNo = @ApprovalOutwardNo, ApprovalDate = @ApprovalDate, InitialActionRemarks = @InitialActionRemarks,
                InitialActionPath1 = @InitialActionPath1, InitialActionPath2 = @InitialActionPath2, CorpMFANumber = @CorpMFANumber,
                CorpMFAYear = @CorpMFAYear, HighCourtBench = @HighCourtBench, OtherHighCourtBench = @OtherHighCourtBench,
                IsPendingForFiling = @IsPendingForFiling, CorpMFAEntrustmentNo = @CorpMFAEntrustmentNo,
                CorpMFAEntrustmentDate = @CorpMFAEntrustmentDate, CorpMFAAdvocate = @CorpMFAAdvocate, StayGranted = @StayGranted,
                StayComplianceOutwardNo = @StayComplianceOutwardNo, StayComplianceDate = @StayComplianceDate,
                StayOrderPath1 = @StayOrderPath1, StayOrderPath2 = @StayOrderPath2, ComplianceLetterPath = @ComplianceLetterPath,
                CorpMFAStatus = @CorpMFAStatus, RestorationFiled = @RestorationFiled, RestorationDate = @RestorationDate,
                RestorationStatus = @RestorationStatus, CorpMFAOutcome = @CorpMFAOutcome, CorpMFAActionTaken = @CorpMFAActionTaken,
                ClosureOutwardNo = @ClosureOutwardNo, ClosureDate = @ClosureDate, CorpSCNumber = @CorpSCNumber, CorpSCYear = @CorpSCYear,
                CorpSCEntrustmentNo = @CorpSCEntrustmentNo, CorpSCEntrustmentDate = @CorpSCEntrustmentDate,
                CorpSCAdvocate = @CorpSCAdvocate, CorpSCStatus = @CorpSCStatus, CrossAppealFiledBy = @CrossAppealFiledBy,
                CrossMFANumber = @CrossMFANumber, CrossMFAStatus = @CrossMFAStatus, ClaimantDivisionID = @ClaimantDivisionID,
                ClaimantMVCNumber = @ClaimantMVCNumber, ClaimantMVCYear = @ClaimantMVCYear, ClaimantMVCCurrentStatus = @ClaimantMVCCurrentStatus,
                ClaimantMFANumber = @ClaimantMFANumber, ClaimantMFAYear = @ClaimantMFAYear, ClaimantMFAEntrustmentNo = @ClaimantMFAEntrustmentNo,
                ClaimantMFAEntrustmentDate = @ClaimantMFAEntrustmentDate, ClaimantMFAAdvocate = @ClaimantMFAAdvocate,
                ClaimantMFAStatus = @ClaimantMFAStatus, ClaimantMFADecision = @ClaimantMFADecision, ClaimantActionTaken = @ClaimantActionTaken,
                ClaimantApprovalNo = @ClaimantApprovalNo, ClaimantApprovalDate = @ClaimantApprovalDate, ClaimantSCNumber = @ClaimantSCNumber,
                ClaimantSCDiaryNumber = @ClaimantSCDiaryNumber, ClaimantSCYear = @ClaimantSCYear, ClaimantSLPYear = @ClaimantSLPYear,
                ClaimantSCFiledBy = @ClaimantSCFiledBy, ClaimantSCEntrustmentNo = @ClaimantSCEntrustmentNo,
                ClaimantSCEntrustmentDate = @ClaimantSCEntrustmentDate, ClaimantSCAdvocate = @ClaimantSCAdvocate,
                ClaimantSCStatus = @ClaimantSCStatus, IsClaimantSCAppeal = @IsClaimantSCAppeal, IsClaimantSCPending = @IsClaimantSCPending,
                ClaimantSCOutcome = @ClaimantSCOutcome, ClaimantSCActionTaken = @ClaimantSCActionTaken,
                ClaimantSCClosureNo = @ClaimantSCClosureNo, ClaimantSCClosureDate = @ClaimantSCClosureDate,
                ClaimantSCJudgmentPath = @ClaimantSCJudgmentPath,
                FinalComplianceStatus = @FinalComplianceStatus, AmountDeposited = @AmountDeposited,
                FinalComplianceDate = @FinalComplianceDate, FinalRemarks = @FinalRemarks, ModifiedAt = GETDATE()
            WHERE CaseID = @CaseID;
        END
        ELSE
        BEGIN
            INSERT INTO APPEAL_DETAILS (
                CaseID, FeasibilityReceived, FeasibilityReceiptDate, InitialAction, ApprovalOutwardNo, ApprovalDate, InitialActionRemarks, InitialActionPath1, InitialActionPath2,
                CorpMFANumber, CorpMFAYear, HighCourtBench, OtherHighCourtBench, IsPendingForFiling, CorpMFAEntrustmentNo, CorpMFAEntrustmentDate, CorpMFAAdvocate,
                StayGranted, StayComplianceOutwardNo, StayComplianceDate, StayOrderPath1, StayOrderPath2, ComplianceLetterPath,
                CorpMFAStatus, RestorationFiled, RestorationDate, RestorationStatus,
                CorpMFAOutcome, CorpMFAActionTaken, ClosureOutwardNo, ClosureDate,
                CorpSCNumber, CorpSCYear, CorpSCEntrustmentNo, CorpSCEntrustmentDate, CorpSCAdvocate, CorpSCStatus,
                CrossAppealFiledBy, CrossMFANumber, CrossMFAStatus,
                ClaimantDivisionID, ClaimantMVCNumber, ClaimantMVCYear, ClaimantMVCCurrentStatus,
                ClaimantMFANumber, ClaimantMFAYear, ClaimantMFAEntrustmentNo, ClaimantMFAEntrustmentDate,
                ClaimantMFAAdvocate, ClaimantMFAStatus, ClaimantMFADecision, ClaimantActionTaken, ClaimantApprovalNo, ClaimantApprovalDate,
                ClaimantSCNumber, ClaimantSCDiaryNumber, ClaimantSCYear, ClaimantSLPYear, ClaimantSCFiledBy, ClaimantSCEntrustmentNo, ClaimantSCEntrustmentDate, ClaimantSCAdvocate, ClaimantSCStatus,
                IsClaimantSCAppeal, IsClaimantSCPending, ClaimantSCOutcome, ClaimantSCActionTaken, ClaimantSCClosureNo, ClaimantSCClosureDate,
                ClaimantSCJudgmentPath, FinalComplianceStatus, AmountDeposited, FinalComplianceDate, FinalRemarks, CreatedAt
            )
            VALUES (
                @CaseID, @FeasibilityReceived, @FeasibilityReceiptDate, @InitialAction, @ApprovalOutwardNo, @ApprovalDate, @InitialActionRemarks, @InitialActionPath1, @InitialActionPath2,
                @CorpMFANumber, @CorpMFAYear, @HighCourtBench, @OtherHighCourtBench, @IsPendingForFiling, @CorpMFAEntrustmentNo, @CorpMFAEntrustmentDate, @CorpMFAAdvocate,
                @StayGranted, @StayComplianceOutwardNo, @StayComplianceDate, @StayOrderPath1, @StayOrderPath2, @ComplianceLetterPath,
                @CorpMFAStatus, @RestorationFiled, @RestorationDate, @RestorationStatus,
                @CorpMFAOutcome, @CorpMFAActionTaken, @ClosureOutwardNo, @ClosureDate,
                @CorpSCNumber, @CorpSCYear, @CorpSCEntrustmentNo, @CorpSCEntrustmentDate, @CorpSCAdvocate, @CorpSCStatus,
                @CrossAppealFiledBy, @CrossMFANumber, @CrossMFAStatus,
                @ClaimantDivisionID, @ClaimantMVCNumber, @ClaimantMVCYear, @ClaimantMVCCurrentStatus,
                @ClaimantMFANumber, @ClaimantMFAYear, @ClaimantMFAEntrustmentNo, @ClaimantMFAEntrustmentDate,
                @ClaimantMFAAdvocate, @ClaimantMFAStatus, @ClaimantMFADecision, @ClaimantActionTaken, @ClaimantApprovalNo, @ClaimantApprovalDate,
                @ClaimantSCNumber, @ClaimantSCDiaryNumber, @ClaimantSCYear, @ClaimantSLPYear, @ClaimantSCFiledBy, @ClaimantSCEntrustmentNo, @ClaimantSCEntrustmentDate, @ClaimantSCAdvocate, @ClaimantSCStatus,
                @IsClaimantSCAppeal, @IsClaimantSCPending, @ClaimantSCOutcome, @ClaimantSCActionTaken, @ClaimantSCClosureNo, @ClaimantSCClosureDate,
                @ClaimantSCJudgmentPath, @FinalComplianceStatus, @AmountDeposited, @FinalComplianceDate, @FinalRemarks, GETDATE()
            );
        END
        DELETE FROM APPEAL_CONNECTED WHERE CaseID = @CaseID;
        IF @ConnectedCasesXml IS NOT NULL
        BEGIN
            INSERT INTO APPEAL_CONNECTED (CaseID, FiledBy, ConnectedMVCNo, MFA_Number, Status)
            SELECT @CaseID, T.c.value('@FiledBy', 'NVARCHAR(50)'), T.c.value('@ConnectedMVCNo', 'NVARCHAR(50)'), T.c.value('@MFA_Number', 'NVARCHAR(50)'), T.c.value('@Status', 'NVARCHAR(100)')
            FROM @ConnectedCasesXml.nodes('/ConnectedCases/Case') AS T(c);
        END
        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
GO
