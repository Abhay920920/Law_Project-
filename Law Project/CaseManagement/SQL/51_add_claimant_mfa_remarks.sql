-- Add ClaimantMFARemarks column to APPEAL_DETAILS table
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS 
    WHERE TABLE_NAME = 'APPEAL_DETAILS' AND COLUMN_NAME = 'ClaimantMFARemarks')
BEGIN
    ALTER TABLE APPEAL_DETAILS ADD ClaimantMFARemarks NVARCHAR(MAX) NULL;
    PRINT 'Added ClaimantMFARemarks column to APPEAL_DETAILS';
END
GO

-- Update stored procedure to include ClaimantMFARemarks
IF OBJECT_ID('sp_SaveAppealFull', 'P') IS NOT NULL
    DROP PROCEDURE sp_SaveAppealFull;
GO

CREATE PROCEDURE sp_SaveAppealFull
    @CaseID INT,
    
    -- Section 1: Feasibility
    @FeasibilityReceived BIT,
    @FeasibilityReceiptDate DATE = NULL,
    @InitialAction NVARCHAR(100) = NULL,
    @ApprovalOutwardNo NVARCHAR(50) = NULL,
    @ApprovalDate DATE = NULL,
    @InitialActionRemarks NVARCHAR(MAX) = NULL,
    @InitialActionPath1 NVARCHAR(MAX) = NULL,
    @InitialActionPath2 NVARCHAR(MAX) = NULL,
    
    -- Section 2: Corporation MFA
    @CorpMFANumber NVARCHAR(50) = NULL,
    @CorpMFAYear INT = NULL,
    @HighCourtBench NVARCHAR(100) = NULL,
    @OtherHighCourtBench NVARCHAR(100) = NULL,
    @IsPendingForFiling BIT = NULL,
    @CorpMFAEntrustmentNo NVARCHAR(50) = NULL,
    @CorpMFAEntrustmentDate DATE = NULL,
    @CorpMFAAdvocate NVARCHAR(200) = NULL,
    
    -- Section 3: Stay
    @StayGranted BIT,
    @StayComplianceOutwardNo NVARCHAR(50) = NULL,
    @StayComplianceDate DATE = NULL,
    @StayOrderPath1 NVARCHAR(MAX) = NULL,
    @StayOrderPath2 NVARCHAR(MAX) = NULL,
    @ComplianceLetterPath NVARCHAR(MAX) = NULL,
    
    -- Section 4: MFA Status
    @CorpMFAStatus NVARCHAR(50) = NULL,
    @RestorationFiled BIT,
    @RestorationDate DATE = NULL,
    @RestorationStatus NVARCHAR(100) = NULL,
    
    -- Section 5: Outcome
    @CorpMFAOutcome NVARCHAR(100) = NULL,
    @CorpMFAActionTaken NVARCHAR(100) = NULL,
    @ClosureOutwardNo NVARCHAR(50) = NULL,
    @ClosureDate DATE = NULL,
    
    -- Section 6: Corporation SC
    @CorpSCNumber NVARCHAR(50) = NULL,
    @CorpSCYear INT = NULL,
    @CorpSCEntrustmentNo NVARCHAR(50) = NULL,
    @CorpSCEntrustmentDate DATE = NULL,
    @CorpSCAdvocate NVARCHAR(200) = NULL,
    @CorpSCStatus NVARCHAR(50) = NULL,
    
    -- Section 7: Cross Appeal
    @CrossAppealFiledBy NVARCHAR(50) = NULL,
    @CrossMFANumber NVARCHAR(50) = NULL,
    @CrossMFAStatus NVARCHAR(100) = NULL,
    
    -- Section 8: Claimant Division and MVC
    @ClaimantDivisionID INT = NULL,
    @ClaimantMVCNumber NVARCHAR(50) = NULL,
    @ClaimantMVCYear INT = NULL,
    @ClaimantMVCCurrentStatus NVARCHAR(100) = NULL,
    
    -- Section 8: Claimant MFA
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
    @ClaimantMFARemarks NVARCHAR(MAX) = NULL,
    
    -- Section 9: Claimant SC
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
    
    -- Final Closure
    @FinalComplianceStatus NVARCHAR(100) = NULL,
    @AmountDeposited DECIMAL(18,2) = NULL,
    @FinalComplianceDate DATE = NULL,
    @FinalRemarks NVARCHAR(500) = NULL,
    
    -- Connected Cases as XML (for SQL Server 2012 support)
    @ConnectedCasesXml XML = NULL
AS
BEGIN
    SET NOCOUNT ON;
    
    BEGIN TRY
        BEGIN TRANSACTION;

        -- Check if Appeal record exists
        IF EXISTS (SELECT 1 FROM APPEAL_DETAILS WHERE CaseID = @CaseID)
        BEGIN
            UPDATE APPEAL_DETAILS
            SET 
                FeasibilityReceived = @FeasibilityReceived,
                FeasibilityReceiptDate = @FeasibilityReceiptDate,
                InitialAction = @InitialAction,
                ApprovalOutwardNo = @ApprovalOutwardNo,
                ApprovalDate = @ApprovalDate,
                InitialActionRemarks = @InitialActionRemarks,
                InitialActionPath1 = @InitialActionPath1,
                InitialActionPath2 = @InitialActionPath2,
                
                CorpMFANumber = @CorpMFANumber,
                CorpMFAYear = @CorpMFAYear,
                HighCourtBench = @HighCourtBench,
                OtherHighCourtBench = @OtherHighCourtBench,
                IsPendingForFiling = @IsPendingForFiling,
                CorpMFAEntrustmentNo = @CorpMFAEntrustmentNo,
                CorpMFAEntrustmentDate = @CorpMFAEntrustmentDate,
                CorpMFAAdvocate = @CorpMFAAdvocate,
                
                StayGranted = @StayGranted,
                StayComplianceOutwardNo = @StayComplianceOutwardNo,
                StayComplianceDate = @StayComplianceDate,
                StayOrderPath1 = @StayOrderPath1,
                StayOrderPath2 = @StayOrderPath2,
                ComplianceLetterPath = @ComplianceLetterPath,
                
                CorpMFAStatus = @CorpMFAStatus,
                RestorationFiled = @RestorationFiled,
                RestorationDate = @RestorationDate,
                RestorationStatus = @RestorationStatus,
                
                CorpMFAOutcome = @CorpMFAOutcome,
                CorpMFAActionTaken = @CorpMFAActionTaken,
                ClosureOutwardNo = @ClosureOutwardNo,
                ClosureDate = @ClosureDate,
                
                CorpSCNumber = @CorpSCNumber,
                CorpSCYear = @CorpSCYear,
                CorpSCEntrustmentNo = @CorpSCEntrustmentNo,
                CorpSCEntrustmentDate = @CorpSCEntrustmentDate,
                CorpSCAdvocate = @CorpSCAdvocate,
                CorpSCStatus = @CorpSCStatus,
                
                CrossAppealFiledBy = @CrossAppealFiledBy,
                CrossMFANumber = @CrossMFANumber,
                CrossMFAStatus = @CrossMFAStatus,
                
                ClaimantDivisionID = @ClaimantDivisionID,
                ClaimantMVCNumber = @ClaimantMVCNumber,
                ClaimantMVCYear = @ClaimantMVCYear,
                ClaimantMVCCurrentStatus = @ClaimantMVCCurrentStatus,
                
                ClaimantMFANumber = @ClaimantMFANumber,
                ClaimantMFAYear = @ClaimantMFAYear,
                ClaimantMFAEntrustmentNo = @ClaimantMFAEntrustmentNo,
                ClaimantMFAEntrustmentDate = @ClaimantMFAEntrustmentDate,
                ClaimantMFAAdvocate = @ClaimantMFAAdvocate,
                ClaimantMFAStatus = @ClaimantMFAStatus,
                ClaimantMFADecision = @ClaimantMFADecision,
                ClaimantActionTaken = @ClaimantActionTaken,
                ClaimantApprovalNo = @ClaimantApprovalNo,
                ClaimantApprovalDate = @ClaimantApprovalDate,
                ClaimantMFARemarks = @ClaimantMFARemarks,
                
                ClaimantSCNumber = @ClaimantSCNumber,
                ClaimantSCDiaryNumber = @ClaimantSCDiaryNumber,
                ClaimantSCYear = @ClaimantSCYear,
                ClaimantSLPYear = @ClaimantSLPYear,
                ClaimantSCFiledBy = @ClaimantSCFiledBy,
                ClaimantSCEntrustmentNo = @ClaimantSCEntrustmentNo,
                ClaimantSCEntrustmentDate = @ClaimantSCEntrustmentDate,
                ClaimantSCAdvocate = @ClaimantSCAdvocate,
                ClaimantSCStatus = @ClaimantSCStatus,
                IsClaimantSCAppeal = @IsClaimantSCAppeal,
                IsClaimantSCPending = @IsClaimantSCPending,
                ClaimantSCOutcome = @ClaimantSCOutcome,
                ClaimantSCActionTaken = @ClaimantSCActionTaken,
                ClaimantSCClosureNo = @ClaimantSCClosureNo,
                ClaimantSCClosureDate = @ClaimantSCClosureDate,
                
                FinalComplianceStatus = @FinalComplianceStatus,
                AmountDeposited = @AmountDeposited,
                FinalComplianceDate = @FinalComplianceDate,
                FinalRemarks = @FinalRemarks,
                ModifiedAt = GETDATE()
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
                ClaimantMFAAdvocate, ClaimantMFAStatus, ClaimantMFADecision, ClaimantActionTaken, ClaimantApprovalNo, ClaimantApprovalDate, ClaimantMFARemarks,
                ClaimantSCNumber, ClaimantSCDiaryNumber, ClaimantSCYear, ClaimantSLPYear, ClaimantSCFiledBy, ClaimantSCEntrustmentNo, ClaimantSCEntrustmentDate, ClaimantSCAdvocate, ClaimantSCStatus,
                IsClaimantSCAppeal, IsClaimantSCPending, ClaimantSCOutcome, ClaimantSCActionTaken, ClaimantSCClosureNo, ClaimantSCClosureDate,
                FinalComplianceStatus, AmountDeposited, FinalComplianceDate, FinalRemarks, CreatedAt
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
                @ClaimantMFAAdvocate, @ClaimantMFAStatus, @ClaimantMFADecision, @ClaimantActionTaken, @ClaimantApprovalNo, @ClaimantApprovalDate, @ClaimantMFARemarks,
                @ClaimantSCNumber, @ClaimantSCDiaryNumber, @ClaimantSCYear, @ClaimantSLPYear, @ClaimantSCFiledBy, @ClaimantSCEntrustmentNo, @ClaimantSCEntrustmentDate, @ClaimantSCAdvocate, @ClaimantSCStatus,
                @IsClaimantSCAppeal, @IsClaimantSCPending, @ClaimantSCOutcome, @ClaimantSCActionTaken, @ClaimantSCClosureNo, @ClaimantSCClosureDate,
                @FinalComplianceStatus, @AmountDeposited, @FinalComplianceDate, @FinalRemarks, GETDATE()
            );
        END

        -- Handle Connected Cases using XML parsing (SQL 2012 compatible)
        DELETE FROM APPEAL_CONNECTED WHERE CaseID = @CaseID;
        
        IF @ConnectedCasesXml IS NOT NULL
        BEGIN
            INSERT INTO APPEAL_CONNECTED (CaseID, FiledBy, ConnectedMVCNo, MFA_Number, Status)
            SELECT @CaseID, 
                   T.c.value('@FiledBy', 'NVARCHAR(50)'),
                   T.c.value('@ConnectedMVCNo', 'NVARCHAR(50)'),
                   T.c.value('@MFA_Number', 'NVARCHAR(50)'),
                   T.c.value('@Status', 'NVARCHAR(100)')
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
