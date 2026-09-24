-- Update sp_SaveAppealFull to handle new Claimant Division and MVC fields
USE [NWKRTC_LEGAL];
GO

-- Drop existing procedure
IF OBJECT_ID('dbo.sp_SaveAppealFull', 'P') IS NOT NULL
    DROP PROCEDURE dbo.sp_SaveAppealFull;
GO

CREATE PROCEDURE sp_SaveAppealFull
    @CaseID INT,
    
    -- Section 1: Feasibility
    @FeasibilityReceived BIT,
    @FeasibilityReceiptDate DATE = NULL,
    @InitialAction NVARCHAR(100) = NULL,
    @ApprovalOutwardNo NVARCHAR(50) = NULL,
    @ApprovalDate DATE = NULL,
    
    -- Section 2: Corporation MFA
    @CorpMFANumber NVARCHAR(50) = NULL,
    @CorpMFAYear INT = NULL,
    @HighCourtBench NVARCHAR(50) = NULL,
    @CorpMFAEntrustmentNo NVARCHAR(50) = NULL,
    @CorpMFAEntrustmentDate DATE = NULL,
    @CorpMFAAdvocate NVARCHAR(200) = NULL,
    
    -- Section 3: Stay
    @StayGranted BIT,
    @StayComplianceOutwardNo NVARCHAR(50) = NULL,
    @StayComplianceDate DATE = NULL,
    
    -- Section 4: MFA Status
    @CorpMFAStatus NVARCHAR(50) = NULL,
    @RestorationFiled BIT,
    @RestorationDate DATE = NULL,
    @RestorationStatus NVARCHAR(100) = NULL,
    
    -- Section 5: Outcome
    @CorpMFAOutcome NVARCHAR(50) = NULL,
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
    
    -- Section 8: Claimant Division and MVC (NEW FIELDS)
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
    
    -- Section 9: Claimant SC
    @ClaimantSCNumber NVARCHAR(50) = NULL,
    @ClaimantSCYear INT = NULL,
    @ClaimantSCFiledBy NVARCHAR(50) = NULL,
    @ClaimantSCEntrustmentNo NVARCHAR(50) = NULL,
    @ClaimantSCEntrustmentDate DATE = NULL,
    @ClaimantSCAdvocate NVARCHAR(200) = NULL,
    @ClaimantSCStatus NVARCHAR(50) = NULL,
    
    -- Section 10: Closure
    @FinalComplianceStatus NVARCHAR(100) = NULL,
    @AmountDeposited DECIMAL(18,2) = NULL,
    @FinalComplianceDate DATE = NULL,
    @FinalRemarks NVARCHAR(500) = NULL,
    
    -- Connected Cases as JSON
    @ConnectedCasesJson NVARCHAR(MAX) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    -- Check if Appeal record exists
    IF EXISTS (SELECT 1 FROM APPEAL_DETAILS WHERE CaseID = @CaseID)
    BEGIN
        -- UPDATE existing record
        UPDATE APPEAL_DETAILS
        SET 
            FeasibilityReceived = @FeasibilityReceived,
            FeasibilityReceiptDate = @FeasibilityReceiptDate,
            InitialAction = @InitialAction,
            ApprovalOutwardNo = @ApprovalOutwardNo,
            ApprovalDate = @ApprovalDate,
            
            CorpMFANumber = @CorpMFANumber,
            CorpMFAYear = @CorpMFAYear,
            HighCourtBench = @HighCourtBench,
            CorpMFAEntrustmentNo = @CorpMFAEntrustmentNo,
            CorpMFAEntrustmentDate = @CorpMFAEntrustmentDate,
            CorpMFAAdvocate = @CorpMFAAdvocate,
            
            StayGranted = @StayGranted,
            StayComplianceOutwardNo = @StayComplianceOutwardNo,
            StayComplianceDate = @StayComplianceDate,
            
            CorpMFAStatus = @CorpMFAStatus,
            RestorationFiled = @RestorationFiled,
            RestorationDate = @RestorationDate,
            RestorationStatus = @RestorationStatus,
            
            CorpMFAOutcome = @CorpMFAOutcome,
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
            
            ClaimantSCNumber = @ClaimantSCNumber,
            ClaimantSCYear = @ClaimantSCYear,
            ClaimantSCFiledBy = @ClaimantSCFiledBy,
            ClaimantSCEntrustmentNo = @ClaimantSCEntrustmentNo,
            ClaimantSCEntrustmentDate = @ClaimantSCEntrustmentDate,
            ClaimantSCAdvocate = @ClaimantSCAdvocate,
            ClaimantSCStatus = @ClaimantSCStatus,
            
            FinalComplianceStatus = @FinalComplianceStatus,
            AmountDeposited = @AmountDeposited,
            FinalComplianceDate = @FinalComplianceDate,
            FinalRemarks = @FinalRemarks,
            
            ModifiedAt = GETDATE()
        WHERE CaseID = @CaseID;
    END
    ELSE
    BEGIN
        -- INSERT new record
        INSERT INTO APPEAL_DETAILS (
            CaseID, FeasibilityReceived, FeasibilityReceiptDate, InitialAction, ApprovalOutwardNo, ApprovalDate,
            CorpMFANumber, CorpMFAYear, HighCourtBench, CorpMFAEntrustmentNo, CorpMFAEntrustmentDate, CorpMFAAdvocate,
            StayGranted, StayComplianceOutwardNo, StayComplianceDate,
            CorpMFAStatus, RestorationFiled, RestorationDate, RestorationStatus,
            CorpMFAOutcome, ClosureOutwardNo, ClosureDate,
            CorpSCNumber, CorpSCYear, CorpSCEntrustmentNo, CorpSCEntrustmentDate, CorpSCAdvocate, CorpSCStatus,
            CrossAppealFiledBy, CrossMFANumber, CrossMFAStatus,
            ClaimantDivisionID, ClaimantMVCNumber, ClaimantMVCYear, ClaimantMVCCurrentStatus,
            ClaimantMFANumber, ClaimantMFAYear, ClaimantMFAEntrustmentNo, ClaimantMFAEntrustmentDate,
            ClaimantMFAAdvocate, ClaimantMFAStatus, ClaimantMFADecision, ClaimantActionTaken, ClaimantApprovalNo, ClaimantApprovalDate,
            ClaimantSCNumber, ClaimantSCYear, ClaimantSCFiledBy, ClaimantSCEntrustmentNo, ClaimantSCEntrustmentDate, ClaimantSCAdvocate, ClaimantSCStatus,
            FinalComplianceStatus, AmountDeposited, FinalComplianceDate, FinalRemarks,
            CreatedAt
        )
        VALUES (
            @CaseID, @FeasibilityReceived, @FeasibilityReceiptDate, @InitialAction, @ApprovalOutwardNo, @ApprovalDate,
            @CorpMFANumber, @CorpMFAYear, @HighCourtBench, @CorpMFAEntrustmentNo, @CorpMFAEntrustmentDate, @CorpMFAAdvocate,
            @StayGranted, @StayComplianceOutwardNo, @StayComplianceDate,
            @CorpMFAStatus, @RestorationFiled, @RestorationDate, @RestorationStatus,
            @CorpMFAOutcome, @ClosureOutwardNo, @ClosureDate,
            @CorpSCNumber, @CorpSCYear, @CorpSCEntrustmentNo, @CorpSCEntrustmentDate, @CorpSCAdvocate, @CorpSCStatus,
            @CrossAppealFiledBy, @CrossMFANumber, @CrossMFAStatus,
            @ClaimantDivisionID, @ClaimantMVCNumber, @ClaimantMVCYear, @ClaimantMVCCurrentStatus,
            @ClaimantMFANumber, @ClaimantMFAYear, @ClaimantMFAEntrustmentNo, @ClaimantMFAEntrustmentDate,
            @ClaimantMFAAdvocate, @ClaimantMFAStatus, @ClaimantMFADecision, @ClaimantActionTaken, @ClaimantApprovalNo, @ClaimantApprovalDate,
            @ClaimantSCNumber, @ClaimantSCYear, @ClaimantSCFiledBy, @ClaimantSCEntrustmentNo, @ClaimantSCEntrustmentDate, @ClaimantSCAdvocate, @ClaimantSCStatus,
            @FinalComplianceStatus, @AmountDeposited, @FinalComplianceDate, @FinalRemarks,
            GETDATE()
        );
    END

    -- Handle Connected Cases (Delete old + Insert new from JSON)
    DELETE FROM APPEAL_CONNECTED WHERE CaseID = @CaseID;
    
    IF @ConnectedCasesJson IS NOT NULL AND @ConnectedCasesJson != '[]'
    BEGIN
        INSERT INTO APPEAL_CONNECTED (CaseID, FiledBy, ConnectedMVCNo, MFA_Number, Status)
        SELECT 
            @CaseID,
            JSON_VALUE(value, '$.FiledBy'),
            JSON_VALUE(value, '$.ConnectedMVCNo'),
            JSON_VALUE(value, '$.MFA_Number'),
            JSON_VALUE(value, '$.Status')
        FROM OPENJSON(@ConnectedCasesJson);
    END
END
GO

PRINT 'Stored procedure sp_SaveAppealFull updated successfully!';
