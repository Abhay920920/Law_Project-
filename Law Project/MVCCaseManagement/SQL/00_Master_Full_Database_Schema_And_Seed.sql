-- =============================================================================
-- 🏛️ Law Project — Master Database Schema & Seed Data Script
-- Target Engine: Microsoft SQL Server (2019+ / Azure SQL / LocalDB / Express)
-- Generated On: 2026-10-09 11:23:18
-- Description: Complete Self-Contained Schema, Tables, Constraints, Stored Procedures & Seed Data
-- =============================================================================

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET NOCOUNT ON;
GO

-- -----------------------------------------------------------------------------
-- Table: [ADVOCATE_MASTER]
-- -----------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.tables t JOIN sys.schemas s ON t.schema_id = s.schema_id WHERE t.name = 'ADVOCATE_MASTER' AND s.name = 'dbo')
BEGIN
    CREATE TABLE [dbo].[ADVOCATE_MASTER] (
        [AdvocateID] INT IDENTITY(1,1) NOT NULL,
        [AdvocateName] NVARCHAR(200) NOT NULL,
        [Bench] NVARCHAR(50) NULL,
        [IsActive] BIT NULL DEFAULT ((1)),
        [CreatedDate] DATETIME NULL DEFAULT (getdate())
    );
END;
GO

-- -----------------------------------------------------------------------------
-- Table: [AI_AUDIT_LOGS]
-- -----------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.tables t JOIN sys.schemas s ON t.schema_id = s.schema_id WHERE t.name = 'AI_AUDIT_LOGS' AND s.name = 'dbo')
BEGIN
    CREATE TABLE [dbo].[AI_AUDIT_LOGS] (
        [AuditID] INT IDENTITY(1,1) NOT NULL,
        [UserID] INT NOT NULL,
        [Role] NVARCHAR(50) NULL,
        [DivisionID] INT NOT NULL,
        [ConversationID] INT NULL,
        [CaseType] NVARCHAR(50) NULL,
        [CaseID] INT NULL,
        [Question] NVARCHAR(MAX) NULL,
        [RetrievedSources] NVARCHAR(MAX) NULL,
        [Model] NVARCHAR(100) NULL,
        [ExecutionTimeMs] INT NOT NULL DEFAULT ((0)),
        [Status] NVARCHAR(50) NOT NULL DEFAULT ('SUCCESS'),
        [ErrorMessage] NVARCHAR(MAX) NULL,
        [CreatedAt] DATETIME NOT NULL DEFAULT (getdate())
    );
END;
GO

-- -----------------------------------------------------------------------------
-- Table: [AI_CONVERSATIONS]
-- -----------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.tables t JOIN sys.schemas s ON t.schema_id = s.schema_id WHERE t.name = 'AI_CONVERSATIONS' AND s.name = 'dbo')
BEGIN
    CREATE TABLE [dbo].[AI_CONVERSATIONS] (
        [ConversationID] INT IDENTITY(1,1) NOT NULL,
        [UserID] INT NOT NULL,
        [CaseType] NVARCHAR(50) NULL,
        [CaseID] INT NULL,
        [Title] NVARCHAR(255) NOT NULL,
        [CreatedAt] DATETIME NOT NULL DEFAULT (getdate()),
        [UpdatedAt] DATETIME NOT NULL DEFAULT (getdate()),
        [IsActive] BIT NOT NULL DEFAULT ((1))
    );
END;
GO

-- -----------------------------------------------------------------------------
-- Table: [AI_MESSAGES]
-- -----------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.tables t JOIN sys.schemas s ON t.schema_id = s.schema_id WHERE t.name = 'AI_MESSAGES' AND s.name = 'dbo')
BEGIN
    CREATE TABLE [dbo].[AI_MESSAGES] (
        [MessageID] INT IDENTITY(1,1) NOT NULL,
        [ConversationID] INT NOT NULL,
        [Role] NVARCHAR(20) NOT NULL,
        [MessageText] NVARCHAR(MAX) NOT NULL,
        [Model] NVARCHAR(100) NULL,
        [CreatedAt] DATETIME NOT NULL DEFAULT (getdate())
    );
END;
GO

-- -----------------------------------------------------------------------------
-- Table: [APPEAL_CONNECTED]
-- -----------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.tables t JOIN sys.schemas s ON t.schema_id = s.schema_id WHERE t.name = 'APPEAL_CONNECTED' AND s.name = 'dbo')
BEGIN
    CREATE TABLE [dbo].[APPEAL_CONNECTED] (
        [ConnectedID] INT IDENTITY(1,1) NOT NULL,
        [CaseID] INT NOT NULL,
        [ConnectedMVCNo] VARCHAR(50) NULL,
        [FiledBy] VARCHAR(50) NULL,
        [MFA_Number] VARCHAR(50) NULL,
        [Status] VARCHAR(50) NULL
    );
END;
GO

-- -----------------------------------------------------------------------------
-- Table: [APPEAL_DETAILS]
-- -----------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.tables t JOIN sys.schemas s ON t.schema_id = s.schema_id WHERE t.name = 'APPEAL_DETAILS' AND s.name = 'dbo')
BEGIN
    CREATE TABLE [dbo].[APPEAL_DETAILS] (
        [AppealID] INT IDENTITY(1,1) NOT NULL,
        [CaseID] INT NOT NULL,
        [FeasibilityReceived] BIT NULL DEFAULT ((0)),
        [FeasibilityReceiptDate] DATE NULL,
        [InitialAction] VARCHAR(50) NULL,
        [ApprovalOutwardNo] VARCHAR(50) NULL,
        [ApprovalDate] DATE NULL,
        [CorpMFANumber] VARCHAR(50) NULL,
        [CorpMFAYear] INT NULL,
        [HighCourtBench] VARCHAR(50) NULL,
        [CorpMFAEntrustmentNo] VARCHAR(50) NULL,
        [CorpMFAEntrustmentDate] DATE NULL,
        [CorpMFAAdvocate] VARCHAR(255) NULL,
        [StayGranted] BIT NULL DEFAULT ((0)),
        [StayComplianceOutwardNo] VARCHAR(50) NULL,
        [StayComplianceDate] DATE NULL,
        [CorpMFAStatus] VARCHAR(50) NULL,
        [RestorationFiled] BIT NULL DEFAULT ((0)),
        [RestorationDate] DATE NULL,
        [RestorationStatus] VARCHAR(100) NULL,
        [CorpMFAOutcome] VARCHAR(50) NULL,
        [ClosureOutwardNo] VARCHAR(50) NULL,
        [ClosureDate] DATE NULL,
        [CorpSCNumber] VARCHAR(50) NULL,
        [CorpSCYear] INT NULL,
        [CorpSCEntrustmentNo] VARCHAR(50) NULL,
        [CorpSCEntrustmentDate] DATE NULL,
        [CorpSCAdvocate] VARCHAR(255) NULL,
        [CorpSCStatus] VARCHAR(50) NULL,
        [CrossAppealFiledBy] VARCHAR(50) NULL,
        [CrossMFANumber] VARCHAR(50) NULL,
        [CrossMFAStatus] VARCHAR(50) NULL,
        [ClaimantMFANumber] VARCHAR(50) NULL,
        [ClaimantMFAYear] INT NULL,
        [ClaimantMFAEntrustmentNo] VARCHAR(50) NULL,
        [ClaimantMFAEntrustmentDate] DATE NULL,
        [ClaimantMFAAdvocate] VARCHAR(255) NULL,
        [ClaimantMFAStatus] VARCHAR(50) NULL,
        [ClaimantMFADecision] VARCHAR(50) NULL,
        [ClaimantActionTaken] VARCHAR(100) NULL,
        [ClaimantApprovalNo] VARCHAR(50) NULL,
        [ClaimantApprovalDate] DATE NULL,
        [ClaimantSCNumber] VARCHAR(50) NULL,
        [ClaimantSCYear] INT NULL,
        [ClaimantSCFiledBy] VARCHAR(50) NULL,
        [ClaimantSCEntrustmentNo] VARCHAR(50) NULL,
        [ClaimantSCEntrustmentDate] DATE NULL,
        [ClaimantSCAdvocate] VARCHAR(255) NULL,
        [ClaimantSCStatus] VARCHAR(50) NULL,
        [FinalComplianceStatus] VARCHAR(100) NULL,
        [AmountDeposited] DECIMAL(18, 2) NULL,
        [FinalComplianceDate] DATE NULL,
        [FinalRemarks] NVARCHAR(MAX) NULL,
        [CreatedAt] DATETIME NULL DEFAULT (getdate()),
        [UpdatedAt] DATETIME NULL DEFAULT (getdate()),
        [ClaimantDivisionID] INT NULL,
        [ClaimantMVCNumber] NVARCHAR(50) NULL,
        [ClaimantMVCYear] INT NULL,
        [ClaimantMVCCurrentStatus] NVARCHAR(100) NULL,
        [ModifiedAt] DATETIME NULL,
        [StayOrderPath1] NVARCHAR(500) NULL,
        [StayOrderPath2] NVARCHAR(500) NULL,
        [ClaimantSCOutcome] NVARCHAR(100) NULL,
        [ClaimantSCActionTaken] NVARCHAR(100) NULL,
        [ClaimantSCClosureNo] NVARCHAR(50) NULL,
        [ClaimantSCClosureDate] DATE NULL,
        [ClaimantSCDiaryNumber] NVARCHAR(50) NULL,
        [IsClaimantSCPending] BIT NOT NULL DEFAULT ((0)),
        [IsClaimantSCAppeal] BIT NOT NULL DEFAULT ((0)),
        [InitialActionRemarks] NVARCHAR(MAX) NULL,
        [InitialActionPath1] VARCHAR(500) NULL,
        [InitialActionPath2] VARCHAR(500) NULL,
        [ClaimantSLPYear] INT NULL,
        [OtherHighCourtBench] NVARCHAR(100) NULL,
        [ComplianceLetterPath] NVARCHAR(MAX) NULL,
        [CorpMFAActionTaken] NVARCHAR(100) NULL,
        [IsPendingForFiling] BIT NULL,
        [Opinion_CLO] NVARCHAR(MAX) NULL,
        [ClaimantMFARemarks] NVARCHAR(MAX) NULL,
        [ApprovalCopyPath] NVARCHAR(MAX) NULL,
        [MFAJudgmentCopyPath] NVARCHAR(MAX) NULL,
        [CorpMFAActionTakenPath] NVARCHAR(MAX) NULL,
        [ClaimantSCJudgmentPath] NVARCHAR(MAX) NULL,
        [CNRNumber] NVARCHAR(16) NULL,
        [EstCode] NVARCHAR(50) NULL,
        [CaseTypeCode] NVARCHAR(50) NULL,
        [CorpMFACNRNumber] NVARCHAR(50) NULL,
        [CorpMFANextHearingDate] DATE NULL,
        [CorpMFAStage] NVARCHAR(100) NULL,
        [ClaimantMFACNRNumber] NVARCHAR(50) NULL,
        [CourtHall] NVARCHAR(100) NULL,
        [ClaimantMFANextHearingDate] DATE NULL,
        [ClaimantMFAStage] NVARCHAR(100) NULL,
        [ActionTaken_LO] NVARCHAR(100) NULL,
        [ApprovalDate_LO] DATE NULL,
        [Opinion_LO] NVARCHAR(MAX) NULL,
        [ActionTaken_DyCLO] NVARCHAR(100) NULL,
        [ApprovalDate_DyCLO] DATE NULL,
        [Opinion_DyCLO] NVARCHAR(MAX) NULL,
        [ActionTaken_CLO] NVARCHAR(100) NULL,
        [ApprovalDate_CLO] DATE NULL,
        [ActionTaken_MD] NVARCHAR(100) NULL,
        [ApprovalDate_MD] DATE NULL,
        [Opinion_MD] NVARCHAR(MAX) NULL
    );
END;
GO

-- -----------------------------------------------------------------------------
-- Table: [APPROVAL_LETTERS]
-- -----------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.tables t JOIN sys.schemas s ON t.schema_id = s.schema_id WHERE t.name = 'APPROVAL_LETTERS' AND s.name = 'dbo')
BEGIN
    CREATE TABLE [dbo].[APPROVAL_LETTERS] (
        [LetterID] INT IDENTITY(1,1) NOT NULL,
        [LetterNumber] NVARCHAR(100) NULL,
        [LetterDate] DATE NULL,
        [FilePath] NVARCHAR(500) NULL,
        [FileName] NVARCHAR(255) NULL,
        [Remarks] NVARCHAR(500) NULL,
        [DivisionID] INT NOT NULL DEFAULT ((0)),
        [UploadedBy] NVARCHAR(100) NULL,
        [UploadedDate] DATETIME NULL DEFAULT (getdate())
    );
END;
GO

-- -----------------------------------------------------------------------------
-- Table: [AUDIT_LOG]
-- -----------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.tables t JOIN sys.schemas s ON t.schema_id = s.schema_id WHERE t.name = 'AUDIT_LOG' AND s.name = 'dbo')
BEGIN
    CREATE TABLE [dbo].[AUDIT_LOG] (
        [LogID] INT IDENTITY(1,1) NOT NULL,
        [TableName] NVARCHAR(50) NULL,
        [RecordID] INT NULL,
        [Action] NVARCHAR(20) NULL,
        [OldValue] NVARCHAR(MAX) NULL,
        [NewValue] NVARCHAR(MAX) NULL,
        [UserID] INT NULL,
        [LogDate] DATETIME NULL DEFAULT (getdate())
    );
END;
GO

-- -----------------------------------------------------------------------------
-- Table: [CASE_HEARINGS_HISTORY]
-- -----------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.tables t JOIN sys.schemas s ON t.schema_id = s.schema_id WHERE t.name = 'CASE_HEARINGS_HISTORY' AND s.name = 'dbo')
BEGIN
    CREATE TABLE [dbo].[CASE_HEARINGS_HISTORY] (
        [HearingID] INT IDENTITY(1,1) NOT NULL,
        [CNRNumber] NVARCHAR(50) NOT NULL,
        [HearingDate] DATE NOT NULL,
        [BusinessTransacted] NVARCHAR(MAX) NULL,
        [NextHearingDate] DATE NULL,
        [NextStage] NVARCHAR(255) NULL,
        [CreatedDate] DATETIME NULL DEFAULT (getdate())
    );
END;
GO

-- -----------------------------------------------------------------------------
-- Table: [CASE_NOTINGS]
-- -----------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.tables t JOIN sys.schemas s ON t.schema_id = s.schema_id WHERE t.name = 'CASE_NOTINGS' AND s.name = 'dbo')
BEGIN
    CREATE TABLE [dbo].[CASE_NOTINGS] (
        [NotingID] INT IDENTITY(1,1) NOT NULL,
        [CaseType] VARCHAR(20) NOT NULL,
        [CaseID] INT NOT NULL,
        [NotingText] NVARCHAR(MAX) NOT NULL,
        [CreatedByUsername] NVARCHAR(100) NOT NULL,
        [CreatedByName] NVARCHAR(150) NULL,
        [CreatedByRole] NVARCHAR(100) NULL,
        [CreatedByDivision] NVARCHAR(100) NULL,
        [CreatedDate] DATETIME NOT NULL DEFAULT (getdate()),
        [IsActive] BIT NOT NULL DEFAULT ((1))
    );
END;
GO

-- -----------------------------------------------------------------------------
-- Table: [CASE_ORDERS]
-- -----------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.tables t JOIN sys.schemas s ON t.schema_id = s.schema_id WHERE t.name = 'CASE_ORDERS' AND s.name = 'dbo')
BEGIN
    CREATE TABLE [dbo].[CASE_ORDERS] (
        [OrderID] INT IDENTITY(1,1) NOT NULL,
        [CNRNumber] NVARCHAR(50) NOT NULL,
        [OrderDate] DATE NOT NULL,
        [OrderType] NVARCHAR(100) NULL,
        [LocalPdfPath] NVARCHAR(MAX) NULL,
        [ExternalUrl] NVARCHAR(MAX) NULL,
        [CreatedDate] DATETIME NULL DEFAULT (getdate())
    );
END;
GO

-- -----------------------------------------------------------------------------
-- Table: [CASE_STATUS_MASTER]
-- -----------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.tables t JOIN sys.schemas s ON t.schema_id = s.schema_id WHERE t.name = 'CASE_STATUS_MASTER' AND s.name = 'dbo')
BEGIN
    CREATE TABLE [dbo].[CASE_STATUS_MASTER] (
        [StatusID] INT IDENTITY(1,1) NOT NULL,
        [StatusCode] NVARCHAR(20) NOT NULL,
        [StatusName] NVARCHAR(50) NOT NULL,
        [DisplayOrder] INT NULL,
        [IsActive] BIT NULL DEFAULT ((1)),
        [CreatedBy] NVARCHAR(50) NULL,
        [CreatedDate] DATETIME NULL DEFAULT (getdate())
    );
END;
GO

-- -----------------------------------------------------------------------------
-- Table: [CASE_VIEW_TRACKING]
-- -----------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.tables t JOIN sys.schemas s ON t.schema_id = s.schema_id WHERE t.name = 'CASE_VIEW_TRACKING' AND s.name = 'dbo')
BEGIN
    CREATE TABLE [dbo].[CASE_VIEW_TRACKING] (
        [ViewID] INT IDENTITY(1,1) NOT NULL,
        [CaseID] INT NOT NULL,
        [ViewedAt] DATETIME NOT NULL DEFAULT (getdate())
    );
END;
GO

-- -----------------------------------------------------------------------------
-- Table: [CAUSELIST_ITEMS]
-- -----------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.tables t JOIN sys.schemas s ON t.schema_id = s.schema_id WHERE t.name = 'CAUSELIST_ITEMS' AND s.name = 'dbo')
BEGIN
    CREATE TABLE [dbo].[CAUSELIST_ITEMS] (
        [ItemID] INT IDENTITY(1,1) NOT NULL,
        [ListID] INT NOT NULL,
        [ItemNo] INT NOT NULL,
        [CaseNumber] NVARCHAR(100) NULL,
        [CNRNumber] NVARCHAR(50) NULL,
        [CaseType] NVARCHAR(100) NULL,
        [Petitioner] NVARCHAR(500) NULL,
        [Respondent] NVARCHAR(500) NULL,
        [AdvocateName] NVARCHAR(500) NULL,
        [Stage] NVARCHAR(255) NULL,
        [IsCorpCase] BIT NOT NULL DEFAULT ((0)),
        [CourtName] NVARCHAR(255) NULL,
        [JudgeName] NVARCHAR(255) NULL
    );
END;
GO

-- -----------------------------------------------------------------------------
-- Table: [DAILY_CAUSELISTS]
-- -----------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.tables t JOIN sys.schemas s ON t.schema_id = s.schema_id WHERE t.name = 'DAILY_CAUSELISTS' AND s.name = 'dbo')
BEGIN
    CREATE TABLE [dbo].[DAILY_CAUSELISTS] (
        [ListID] INT IDENTITY(1,1) NOT NULL,
        [EstCode] NVARCHAR(50) NOT NULL,
        [CourtNo] NVARCHAR(50) NOT NULL,
        [CauseListDate] DATE NOT NULL,
        [ListType] NVARCHAR(50) NOT NULL DEFAULT ('civil'),
        [TotalCases] INT NOT NULL DEFAULT ((0)),
        [CorpCasesCount] INT NOT NULL DEFAULT ((0)),
        [CreatedDate] DATETIME NULL DEFAULT (getdate())
    );
END;
GO

-- -----------------------------------------------------------------------------
-- Table: [DEPOT_MASTER]
-- -----------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.tables t JOIN sys.schemas s ON t.schema_id = s.schema_id WHERE t.name = 'DEPOT_MASTER' AND s.name = 'dbo')
BEGIN
    CREATE TABLE [dbo].[DEPOT_MASTER] (
        [DepotID] INT IDENTITY(1,1) NOT NULL,
        [DepotCode] NVARCHAR(20) NOT NULL,
        [DivisionCode] NVARCHAR(20) NOT NULL,
        [DepotNameEnglish] NVARCHAR(100) NOT NULL,
        [DepotNameKannada] NVARCHAR(100) NULL,
        [IsActive] BIT NULL DEFAULT ((1)),
        [CreatedBy] NVARCHAR(50) NULL,
        [CreatedDate] DATETIME NULL DEFAULT (getdate())
    );
END;
GO

-- -----------------------------------------------------------------------------
-- Table: [DIVISION_MASTER]
-- -----------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.tables t JOIN sys.schemas s ON t.schema_id = s.schema_id WHERE t.name = 'DIVISION_MASTER' AND s.name = 'dbo')
BEGIN
    CREATE TABLE [dbo].[DIVISION_MASTER] (
        [DivisionID] INT IDENTITY(1,1) NOT NULL,
        [DivisionCode] NVARCHAR(20) NOT NULL,
        [DivisionNameEnglish] NVARCHAR(100) NOT NULL,
        [DivisionNameKannada] NVARCHAR(100) NULL,
        [Location] NVARCHAR(100) NULL,
        [IsActive] BIT NULL DEFAULT ((1)),
        [CreatedBy] NVARCHAR(50) NULL,
        [CreatedDate] DATETIME NULL DEFAULT (getdate()),
        [ModifiedBy] NVARCHAR(50) NULL,
        [ModifiedDate] DATETIME NULL
    );
END;
GO

-- -----------------------------------------------------------------------------
-- Table: [ECOURTS_TRACKED_CASES]
-- -----------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.tables t JOIN sys.schemas s ON t.schema_id = s.schema_id WHERE t.name = 'ECOURTS_TRACKED_CASES' AND s.name = 'dbo')
BEGIN
    CREATE TABLE [dbo].[ECOURTS_TRACKED_CASES] (
        [TrackedID] INT IDENTITY(1,1) NOT NULL,
        [CNRNumber] NVARCHAR(50) NOT NULL,
        [EstCode] NVARCHAR(50) NULL,
        [CaseTypeCode] NVARCHAR(50) NULL,
        [CaseNumber] NVARCHAR(100) NULL,
        [CaseYear] INT NULL,
        [RelatedCaseID] INT NULL,
        [RelatedModule] NVARCHAR(50) NULL,
        [DivisionID] INT NOT NULL DEFAULT ((0)),
        [Petitioner] NVARCHAR(255) NULL,
        [Respondent] NVARCHAR(255) NULL,
        [CaseStatus] NVARCHAR(100) NULL,
        [NextHearingDate] DATE NULL,
        [StagePurpose] NVARCHAR(255) NULL,
        [CourtNo] NVARCHAR(50) NULL,
        [JudgeDesignation] NVARCHAR(255) NULL,
        [LastSyncedAt] DATETIME NULL,
        [CreatedDate] DATETIME NULL DEFAULT (getdate())
    );
END;
GO

-- -----------------------------------------------------------------------------
-- Table: [GRA_CASES]
-- -----------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.tables t JOIN sys.schemas s ON t.schema_id = s.schema_id WHERE t.name = 'GRA_CASES' AND s.name = 'dbo')
BEGIN
    CREATE TABLE [dbo].[GRA_CASES] (
        [CaseID] INT IDENTITY(1,1) NOT NULL,
        [CreatedDate] DATETIME NULL DEFAULT (getdate()),
        [ModifiedDate] DATETIME NULL DEFAULT (getdate()),
        [CreatedBy] VARCHAR(100) NULL,
        [CaseStatus] VARCHAR(50) NULL,
        [DivisionCode] INT NULL,
        [PGANumber] VARCHAR(100) NULL,
        [CourtType] VARCHAR(100) NULL,
        [ClaimantName] VARCHAR(200) NULL,
        [ClaimantDesignation] VARCHAR(100) NULL,
        [WorkingStatus] VARCHAR(50) NULL,
        [EntrustmentNo] VARCHAR(100) NULL,
        [EntrustmentDate] DATETIME NULL,
        [AdvocateName] VARCHAR(200) NULL,
        [DisposalResult] VARCHAR(50) NULL,
        [AmountClaimed] NVARCHAR(MAX) NULL,
        [AppointmentDate] DATETIME NULL,
        [RetirementDate] DATETIME NULL,
        [TotalServicePeriod] VARCHAR(100) NULL,
        [Period_SPE_LWA_ABS] VARCHAR(100) NULL,
        [QualifyingService_Corp] VARCHAR(100) NULL,
        [QualifyingService_CA] VARCHAR(100) NULL,
        [LastDrawnPay] DECIMAL(18, 2) NULL,
        [GratuityAmount_Corp_Reg] DECIMAL(18, 2) NULL,
        [GratuityAmount_Corp_Act] DECIMAL(18, 2) NULL,
        [IsDeductionMade] BIT NULL DEFAULT ((0)),
        [DeductionDetails] NVARCHAR(MAX) NULL,
        [ActualPaidAmount] DECIMAL(18, 2) NULL,
        [ChequeNumber] VARCHAR(50) NULL,
        [ChequeDate] DATETIME NULL,
        [DisposalDate] DATETIME NULL,
        [CopyAppliedDate] DATETIME NULL,
        [CopyIssuedDate] DATETIME NULL,
        [CopyReceivedDate] DATETIME NULL,
        [GratuityAmount_CA_Reg] DECIMAL(18, 2) NULL,
        [GratuityAmount_CA_Act] DECIMAL(18, 2) NULL,
        [OrderedAmount_CA] DECIMAL(18, 2) NULL,
        [AdvocateOpinion] NVARCHAR(MAX) NULL,
        [LOOpinion] NVARCHAR(MAX) NULL,
        [DCOpinion] NVARCHAR(MAX) NULL,
        [AppealNumber] VARCHAR(100) NULL,
        [AppealArisingNo] VARCHAR(100) NULL,
        [AppealCourt] VARCHAR(100) NULL,
        [AppealEntrustmentNo] VARCHAR(100) NULL,
        [AppealEntrustmentDate] DATETIME NULL,
        [AppealAdvocate] VARCHAR(200) NULL,
        [AppealAwardDetails] NVARCHAR(MAX) NULL,
        [FinalAmount] DECIMAL(18, 2) NULL,
        [ComplianceStatus] VARCHAR(100) NULL,
        [Remarks] NVARCHAR(MAX) NULL,
        [ForwardingStatus] NVARCHAR(100) NULL,
        [ClosureRemarks] NVARCHAR(1000) NULL,
        [ClosureDate] DATETIME NULL,
        [OutwardNumber] NVARCHAR(50) NULL,
        [OutwardDate] DATETIME NULL,
        [AdverseJudgmentPath] NVARCHAR(MAX) NULL,
        [AppealRemarks] NVARCHAR(1000) NULL,
        [AppealDate] DATETIME NULL,
        [LastDrawnBasic] DECIMAL(18, 2) NULL,
        [LastDrawnBDA] DECIMAL(18, 2) NULL,
        [LastDrawnBasic_CA] DECIMAL(18, 2) NULL,
        [LastDrawnBDA_CA] DECIMAL(18, 2) NULL,
        [DeductionAmount] DECIMAL(18, 2) NULL,
        [IsDeductionMade_CA] BIT NOT NULL DEFAULT ((0)),
        [DeductionDetails_CA] NVARCHAR(MAX) NULL,
        [DeductionAmount_CA] DECIMAL(18, 2) NULL,
        [ActualPaidAmount_CA] DECIMAL(18, 2) NULL,
        [ChequeNumber_CA] NVARCHAR(100) NULL,
        [ChequeDate_CA] DATETIME NULL,
        [CopyDeliveredDate] DATETIME NULL,
        [AppointmentDate_CA] DATETIME NULL,
        [RetirementDate_CA] DATETIME NULL,
        [Period_SPE_LWA_ABS_CA] NVARCHAR(100) NULL,
        [LastDrawnPay_CA] DECIMAL(18, 2) NULL,
        [IsDocumentSent] BIT NOT NULL DEFAULT ((0)),
        [DocumentOutwardNo] NVARCHAR(50) NULL,
        [DocumentOutwardDate] DATETIME NULL,
        [IsObjectionFiled] BIT NOT NULL DEFAULT ((0)),
        [ObjectionFiledDate] DATETIME NULL,
        [IsEvidenceFiled] BIT NOT NULL DEFAULT ((0)),
        [CurrentStage] NVARCHAR(100) NULL,
        [NextHearingDate] DATETIME NULL,
        [IsPendingForFiling] BIT NULL,
        [HighCourtBench] NVARCHAR(50) NULL,
        [OtherHighCourtBench] NVARCHAR(100) NULL,
        [StayGranted] BIT NULL,
        [StayComplianceOutwardNo] NVARCHAR(50) NULL,
        [StayComplianceDate] DATETIME NULL,
        [StayOrderPath1] NVARCHAR(500) NULL,
        [StayOrderPath2] NVARCHAR(500) NULL,
        [InitialActionRemarks] NVARCHAR(MAX) NULL,
        [InitialActionPath1] NVARCHAR(500) NULL,
        [InitialActionPath2] NVARCHAR(500) NULL,
        [FinalRemarks] NVARCHAR(MAX) NULL,
        [CorpWPStatus] NVARCHAR(50) NULL,
        [RestorationFiled] BIT NULL,
        [RestorationDate] DATETIME NULL,
        [RestorationStatus] NVARCHAR(100) NULL,
        [CorpWPOutcome] NVARCHAR(100) NULL,
        [CorpWPActionTaken] NVARCHAR(100) NULL,
        [ClosureOutwardNo] NVARCHAR(50) NULL,
        [IsClaimantSCAppeal] BIT NULL,
        [IsClaimantSCPending] BIT NULL,
        [ClaimantSCDiaryNumber] NVARCHAR(50) NULL,
        [ClaimantSCYear] INT NULL,
        [ClaimantSCNumber] NVARCHAR(50) NULL,
        [ClaimantSLPYear] INT NULL,
        [ClaimantSCFiledBy] NVARCHAR(50) NULL,
        [ClaimantSCEntrustmentNo] NVARCHAR(50) NULL,
        [ClaimantSCEntrustmentDate] DATETIME NULL,
        [ClaimantSCAdvocate] NVARCHAR(200) NULL,
        [ClaimantSCStatus] NVARCHAR(50) NULL,
        [ClaimantSCOutcome] NVARCHAR(100) NULL,
        [ClaimantSCActionTaken] NVARCHAR(100) NULL,
        [ClaimantSCClosureNo] NVARCHAR(50) NULL,
        [ClaimantSCClosureDate] DATETIME NULL,
        [ClaimantDivisionID] INT NULL,
        [ClaimantArisingWPNumber] NVARCHAR(50) NULL,
        [ClaimantArisingWPYear] INT NULL,
        [ClaimantMVCCurrentStatus] NVARCHAR(100) NULL,
        [ClaimantWPNumber] NVARCHAR(50) NULL,
        [ClaimantWPYear] INT NULL,
        [ClaimantWPEntrustmentNo] NVARCHAR(50) NULL,
        [ClaimantWPEntrustmentDate] DATETIME NULL,
        [ClaimantWPAdvocate] NVARCHAR(200) NULL,
        [ClaimantWPStatus] NVARCHAR(50) NULL,
        [ClaimantWPDecision] NVARCHAR(100) NULL,
        [ClaimantActionTaken] NVARCHAR(100) NULL,
        [ClaimantApprovalNo] NVARCHAR(50) NULL,
        [ClaimantApprovalDate] DATETIME NULL,
        [CorpWPNumber] NVARCHAR(200) NULL,
        [CorpWPYear] INT NULL,
        [CorpWPAdvocate] NVARCHAR(500) NULL,
        [CorpWPEntrustmentNo] NVARCHAR(200) NULL,
        [CorpWPEntrustmentDate] DATETIME NULL,
        [LastDrawnDA] DECIMAL(18, 2) NULL,
        [LastDrawnDA_CA] DECIMAL(18, 2) NULL,
        [DateOfBirth] DATETIME NULL,
        [CA_AppointmentRemark] NVARCHAR(500) NULL,
        [IsAdditionalBenefitsGiven] BIT NOT NULL DEFAULT ((0)),
        [AdditionalBenefitsAmount] DECIMAL(18, 2) NULL,
        [DelayRemarks] NVARCHAR(MAX) NULL,
        [IsFeasibilityReceived] BIT NULL,
        [FeasibilityReceiptDate] DATETIME NULL,
        [ActionTaken] NVARCHAR(100) NULL,
        [ApprovalOutwardNo] NVARCHAR(50) NULL,
        [ApprovalDate] DATETIME NULL,
        [AppealComplianceAmount] DECIMAL(18, 2) NULL,
        [AppealComplianceChequeNumber] NVARCHAR(50) NULL,
        [AppealComplianceChequeDate] DATETIME NULL,
        [ObjectionOutwardNo] NVARCHAR(50) NULL,
        [AppealActionOutwardNo] NVARCHAR(MAX) NULL,
        [AppealActionDate] DATETIME2 NULL,
        [AppealDisposalDate] DATETIME2 NULL,
        [AppealJudgmentPath] NVARCHAR(MAX) NULL,
        [ClaimantHighCourtBench] NVARCHAR(MAX) NULL,
        [ClaimantOtherHighCourtBench] NVARCHAR(MAX) NULL,
        [AppealYear] INT NULL,
        [AppealArisingYear] INT NULL,
        [IsInterestPayable] BIT NULL,
        [InterestRate] DECIMAL(18, 2) NULL,
        [InterestRemarks] NVARCHAR(MAX) NULL,
        [IsViewedByCO] BIT NOT NULL DEFAULT ((0)),
        [CLOOpinion] NVARCHAR(MAX) NULL,
        [ForwardingStatus_Appeal] NVARCHAR(200) NULL,
        [OutwardNumber_Appeal] NVARCHAR(100) NULL,
        [OutwardDate_Appeal] DATE NULL,
        [AppealCopyAppliedDate] DATE NULL,
        [AppealCopyReadyDate] DATE NULL,
        [AppealCopyDeliveredDate] DATE NULL,
        [AppealCopyReceivedDate] DATE NULL,
        [AppealDelayRemarks] NVARCHAR(MAX) NULL,
        [AppealAdvocateOpinion] NVARCHAR(MAX) NULL,
        [AppealLOOpinion] NVARCHAR(MAX) NULL,
        [AppealDCOpinion] NVARCHAR(MAX) NULL,
        [EvidenceRemarks] NVARCHAR(MAX) NULL,
        [EvidenceWitnessName] NVARCHAR(200) NULL,
        [EvidenceWitnessDesignation] NVARCHAR(100) NULL,
        [RealizationDate] DATE NULL,
        [CNRNumber] NVARCHAR(50) NULL
    );
END;
GO

-- -----------------------------------------------------------------------------
-- Table: [GRA_INTEREST_PAYMENTS]
-- -----------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.tables t JOIN sys.schemas s ON t.schema_id = s.schema_id WHERE t.name = 'GRA_INTEREST_PAYMENTS' AND s.name = 'dbo')
BEGIN
    CREATE TABLE [dbo].[GRA_INTEREST_PAYMENTS] (
        [InterestPaymentID] INT IDENTITY(1,1) NOT NULL,
        [CaseID] INT NOT NULL,
        [Amount] DECIMAL(18, 2) NOT NULL,
        [ChequeNumber] NVARCHAR(100) NULL,
        [ChequeDate] DATETIME NULL,
        [CreatedDate] DATETIME NULL DEFAULT (getdate()),
        [InterestRate] DECIMAL(18, 2) NULL,
        [FromDate] DATE NULL,
        [ToDate] DATE NULL
    );
END;
GO

-- -----------------------------------------------------------------------------
-- Table: [GRA_PAYMENTS]
-- -----------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.tables t JOIN sys.schemas s ON t.schema_id = s.schema_id WHERE t.name = 'GRA_PAYMENTS' AND s.name = 'dbo')
BEGIN
    CREATE TABLE [dbo].[GRA_PAYMENTS] (
        [PaymentID] INT IDENTITY(1,1) NOT NULL,
        [CaseID] INT NOT NULL,
        [Amount] DECIMAL(18, 2) NOT NULL,
        [ChequeNumber] NVARCHAR(100) NULL,
        [ChequeDate] DATETIME NULL,
        [CreatedDate] DATETIME NULL DEFAULT (getdate())
    );
END;
GO

-- -----------------------------------------------------------------------------
-- Table: [GRATUITY_ADVOCATE_MASTER]
-- -----------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.tables t JOIN sys.schemas s ON t.schema_id = s.schema_id WHERE t.name = 'GRATUITY_ADVOCATE_MASTER' AND s.name = 'dbo')
BEGIN
    CREATE TABLE [dbo].[GRATUITY_ADVOCATE_MASTER] (
        [AdvocateID] INT IDENTITY(1,1) NOT NULL,
        [AdvocateName] NVARCHAR(200) NOT NULL,
        [Specialization] NVARCHAR(100) NULL,
        [IsActive] BIT NOT NULL DEFAULT ((1))
    );
END;
GO

-- -----------------------------------------------------------------------------
-- Table: [GRATUITY_COURT_MASTER]
-- -----------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.tables t JOIN sys.schemas s ON t.schema_id = s.schema_id WHERE t.name = 'GRATUITY_COURT_MASTER' AND s.name = 'dbo')
BEGIN
    CREATE TABLE [dbo].[GRATUITY_COURT_MASTER] (
        [CourtID] INT IDENTITY(1,1) NOT NULL,
        [CourtName] NVARCHAR(200) NOT NULL,
        [Location] NVARCHAR(200) NULL,
        [IsActive] BIT NOT NULL DEFAULT ((1))
    );
END;
GO

-- -----------------------------------------------------------------------------
-- Table: [GRATUITY_ENCLOSED_DOCS]
-- -----------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.tables t JOIN sys.schemas s ON t.schema_id = s.schema_id WHERE t.name = 'GRATUITY_ENCLOSED_DOCS' AND s.name = 'dbo')
BEGIN
    CREATE TABLE [dbo].[GRATUITY_ENCLOSED_DOCS] (
        [DocID] INT IDENTITY(1,1) NOT NULL,
        [CaseID] INT NOT NULL,
        [DocName] NVARCHAR(255) NULL,
        [PageCount] INT NULL,
        [CreatedDate] DATETIME NULL DEFAULT (getdate())
    );
END;
GO

-- -----------------------------------------------------------------------------
-- Table: [HIGH_COURT_ADVOCATES]
-- -----------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.tables t JOIN sys.schemas s ON t.schema_id = s.schema_id WHERE t.name = 'HIGH_COURT_ADVOCATES' AND s.name = 'dbo')
BEGIN
    CREATE TABLE [dbo].[HIGH_COURT_ADVOCATES] (
        [AdvocateID] INT IDENTITY(1,1) NOT NULL,
        [AdvocateName] NVARCHAR(100) NOT NULL,
        [Bench] NVARCHAR(50) NULL,
        [IsActive] BIT NULL DEFAULT ((1))
    );
END;
GO

-- -----------------------------------------------------------------------------
-- Table: [JUDGEMENT_REPO]
-- -----------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.tables t JOIN sys.schemas s ON t.schema_id = s.schema_id WHERE t.name = 'JUDGEMENT_REPO' AND s.name = 'dbo')
BEGIN
    CREATE TABLE [dbo].[JUDGEMENT_REPO] (
        [JudgementID] INT IDENTITY(1,1) NOT NULL,
        [Title] NVARCHAR(MAX) NOT NULL,
        [Court] NVARCHAR(100) NULL,
        [JudgementDate] DATE NULL,
        [Remarks] NVARCHAR(MAX) NULL,
        [FilePath] NVARCHAR(500) NULL,
        [UploadedBy] NVARCHAR(100) NULL,
        [UploadedDate] DATETIME NULL DEFAULT (getdate()),
        [Category] NVARCHAR(50) NULL DEFAULT ('Judgement')
    );
END;
GO

-- -----------------------------------------------------------------------------
-- Table: [LABOUR_ADVOCATES]
-- -----------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.tables t JOIN sys.schemas s ON t.schema_id = s.schema_id WHERE t.name = 'LABOUR_ADVOCATES' AND s.name = 'dbo')
BEGIN
    CREATE TABLE [dbo].[LABOUR_ADVOCATES] (
        [AdvocateID] INT IDENTITY(1,1) NOT NULL,
        [AdvocateName] NVARCHAR(200) NOT NULL,
        [IsActive] BIT NULL DEFAULT ((1))
    );
END;
GO

-- -----------------------------------------------------------------------------
-- Table: [LABOUR_ARISING_APPLICATIONS]
-- -----------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.tables t JOIN sys.schemas s ON t.schema_id = s.schema_id WHERE t.name = 'LABOUR_ARISING_APPLICATIONS' AND s.name = 'dbo')
BEGIN
    CREATE TABLE [dbo].[LABOUR_ARISING_APPLICATIONS] (
        [ArisingID] INT IDENTITY(1,1) NOT NULL,
        [ParentCaseID] INT NULL,
        [Parent_CaseNumber] NVARCHAR(100) NULL,
        [Parent_CaseYear] INT NULL,
        [Parent_CourtID] INT NULL,
        [Parent_CourtName] NVARCHAR(200) NULL,
        [Parent_CaseType] NVARCHAR(50) NULL,
        [Parent_CaseStatus] NVARCHAR(50) NULL,
        [Parent_PetitionerName] NVARCHAR(200) NULL,
        [DivisionID] INT NOT NULL,
        [CaseNumber] NVARCHAR(100) NOT NULL,
        [CaseYear] INT NULL,
        [CourtID] INT NULL,
        [OtherCourtDetails] NVARCHAR(200) NULL,
        [CaseType] NVARCHAR(50) NULL DEFAULT ('Arising Application'),
        [CaseStatus] NVARCHAR(50) NULL DEFAULT ('Pending'),
        [PetitionerName] NVARCHAR(200) NULL,
        [EntrustmentNo] NVARCHAR(100) NULL,
        [EntrustmentDate] DATE NULL,
        [AdvocateID] INT NULL,
        [AdvocateName] NVARCHAR(200) NULL,
        [IsDocumentSent] BIT NULL DEFAULT ((0)),
        [DocumentSent_OutwardNo] NVARCHAR(50) NULL,
        [DocumentSent_OutwardDate] DATE NULL,
        [IsObjectionFiled] BIT NULL DEFAULT ((0)),
        [ObjectionFiled_OutwardNo] NVARCHAR(50) NULL,
        [ObjectionFiled_OutwardDate] DATE NULL,
        [IsEvidenceFiled] BIT NULL DEFAULT ((0)),
        [CurrentStage] NVARCHAR(200) NULL,
        [NextHearingDate] DATE NULL,
        [Remarks] NVARCHAR(MAX) NULL,
        [CreatedDate] DATETIME NULL DEFAULT (getdate()),
        [CreatedBy] INT NULL,
        [ModifiedDate] DATETIME NULL,
        [ModifiedBy] INT NULL,
        [DisposalMode] NVARCHAR(50) NULL,
        [DisposalDate] DATE NULL,
        [DisposalResult] NVARCHAR(50) NULL,
        [FavorRemark] NVARCHAR(MAX) NULL,
        [FavorOutwardDate] DATE NULL,
        [LokAdalat_COApprovalRequired] BIT NULL,
        [LokAdalat_OutwardNo] NVARCHAR(100) NULL,
        [LokAdalat_Date] DATE NULL,
        [LokAdalatDocumentPath] NVARCHAR(MAX) NULL,
        [JudgmentCopyPath] NVARCHAR(MAX) NULL,
        [DE_HistorySheet] BIT NULL DEFAULT ((0)),
        [DE_HistorySheetPath] NVARCHAR(500) NULL,
        [DE_ObjectionsFiled] BIT NULL DEFAULT ((0)),
        [DE_ObjectionsRemark] NVARCHAR(MAX) NULL,
        [DE_DocumentsMarked] BIT NULL DEFAULT ((0)),
        [DE_DocumentsRemark] NVARCHAR(MAX) NULL,
        [DE_Order] NVARCHAR(50) NULL,
        [DE_EO_IsBasedOnDocuments] BIT NULL DEFAULT ((0)),
        [DE_EO_Name] NVARCHAR(200) NULL,
        [DE_EO_Designation] NVARCHAR(100) NULL,
        [DE_Reporter_IsBasedOnDocuments] BIT NULL DEFAULT ((0)),
        [DE_Reporter_Name] NVARCHAR(200) NULL,
        [DE_Reporter_Designation] NVARCHAR(100) NULL,
        [DE_Other_IsBasedOnDocuments] BIT NULL DEFAULT ((0)),
        [DE_Other_Name] NVARCHAR(200) NULL,
        [DE_Other_Designation] NVARCHAR(100) NULL,
        [CC_CaseDisposedDate] DATE NULL,
        [CC_PublicationDate] DATE NULL,
        [CC_AppliedDate] DATE NULL,
        [CC_IssuedDate] DATE NULL,
        [CC_ReceivedDate] DATE NULL,
        [CC_Remarks] NVARCHAR(MAX) NULL,
        [AwardDetails] NVARCHAR(MAX) NULL,
        [Opinion_Advocate] NVARCHAR(MAX) NULL,
        [Opinion_LO] NVARCHAR(MAX) NULL,
        [Opinion_DC] NVARCHAR(MAX) NULL,
        [SentToCO] BIT NULL DEFAULT ((0)),
        [CO_OutwardNo] NVARCHAR(100) NULL,
        [CO_OutwardDate] DATE NULL,
        [CO_Remarks] NVARCHAR(MAX) NULL,
        [CC_DeliveredDate] DATE NULL,
        [EmployeeNo] NVARCHAR(50) NULL,
        [PFNumber] NVARCHAR(50) NULL,
        [Designation] NVARCHAR(100) NULL,
        [WorkingStatus] NVARCHAR(50) NULL,
        [LegalRepresentativeName] NVARCHAR(200) NULL,
        [LRRelationship] NVARCHAR(100) NULL,
        [IsWorkman] BIT NULL DEFAULT ((0)),
        [IsWorkmanRemark] NVARCHAR(MAX) NULL,
        [NatureOfMisconduct] NVARCHAR(MAX) NULL,
        [ClaimFiledOn] NVARCHAR(200) NULL,
        [DelayInFiling] NVARCHAR(200) NULL,
        [ClaimDetails] NVARCHAR(MAX) NULL,
        [CO_FeasibilityReceived] BIT NULL,
        [CO_FeasibilityDate] DATE NULL,
        [CO_ActionTaken] NVARCHAR(100) NULL,
        [CO_ApprovalOutwardNo] NVARCHAR(100) NULL,
        [CO_ApprovalDate] DATE NULL,
        [CO_ClosedDocumentPath] NVARCHAR(MAX) NULL,
        [CO_WP_CaseStatus_Option] NVARCHAR(100) NULL,
        [CO_WP_CaseNumber] NVARCHAR(100) NULL,
        [CO_WP_Year] INT NULL,
        [CO_WP_HighCourtBench] NVARCHAR(100) NULL,
        [CO_WP_EntrustmentNo] NVARCHAR(100) NULL,
        [CO_WP_EntrustmentDate] DATE NULL,
        [CO_WP_AdvocateName] NVARCHAR(255) NULL,
        [CO_WP_StayGranted] BIT NULL,
        [CO_WP_StayApprovalNo] NVARCHAR(100) NULL,
        [CO_WP_StayNature] NVARCHAR(255) NULL,
        [CO_WP_StayDate] DATE NULL,
        [CO_WP_StayOrderPath] NVARCHAR(MAX) NULL,
        [CO_WP_StayRemark] NVARCHAR(MAX) NULL,
        [CO_WP_Status] NVARCHAR(100) NULL,
        [CO_WP_Outcome] NVARCHAR(100) NULL,
        [CO_WP_OutcomeRemark] NVARCHAR(MAX) NULL,
        [CO_WP_OutcomeOutwardNo] NVARCHAR(100) NULL,
        [CO_WP_OutcomeOutwardDate] DATE NULL,
        [CO_WP_ActionTaken] NVARCHAR(100) NULL,
        [CO_IsWorkmanReinstated] BIT NULL,
        [CO_ReinstatedSubjectToWP] NVARCHAR(100) NULL,
        [CO_Reinstatement_StayGranted] BIT NULL,
        [CO_Reinstatement_StayApprovalNo] NVARCHAR(100) NULL,
        [CO_Reinstatement_StayNature] NVARCHAR(100) NULL,
        [CO_Reinstatement_StayDate] DATE NULL,
        [CO_Reinstatement_StayOrderPath] NVARCHAR(MAX) NULL,
        [CO_Reinstatement_StayRemark] NVARCHAR(MAX) NULL,
        [CO_ReinstatementApprovalIssued] BIT NULL,
        [CO_ReinstatementApprovalDate] DATE NULL,
        [CO_Reinstatement_ApprovalNo] NVARCHAR(100) NULL,
        [CO_Reinstatement_ApprovalCopyPath] NVARCHAR(MAX) NULL,
        [CO_WA_CaseStatus_Option] NVARCHAR(100) NULL,
        [CO_WA_CaseNumber] NVARCHAR(100) NULL,
        [CO_WA_Year] INT NULL,
        [CO_WA_HighCourtBench] NVARCHAR(100) NULL,
        [CO_WA_EntrustmentNo] NVARCHAR(100) NULL,
        [CO_WA_EntrustmentDate] DATE NULL,
        [CO_WA_AdvocateName] NVARCHAR(255) NULL,
        [CO_WA_StayGranted] BIT NULL,
        [CO_WA_StayApprovalNo] NVARCHAR(100) NULL,
        [CO_WA_StayNature] NVARCHAR(255) NULL,
        [CO_WA_StayDate] DATE NULL,
        [CO_WA_StayOrderPath] NVARCHAR(MAX) NULL,
        [CO_WA_StayRemark] NVARCHAR(MAX) NULL,
        [CO_WA_Status] NVARCHAR(100) NULL,
        [CO_WA_Outcome] NVARCHAR(100) NULL,
        [CO_WA_OutcomeRemark] NVARCHAR(MAX) NULL,
        [CO_WA_OutcomeOutwardNo] NVARCHAR(100) NULL,
        [CO_WA_OutcomeOutwardDate] DATE NULL,
        [CO_WA_ActionTaken] NVARCHAR(100) NULL,
        [CO_FurtherAppeal_Status_Option] NVARCHAR(100) NULL,
        [CO_FurtherAppeal_CaseNumber] NVARCHAR(100) NULL,
        [CO_FurtherAppeal_Year] INT NULL,
        [CO_FurtherAppeal_EntrustmentNo] NVARCHAR(100) NULL,
        [CO_FurtherAppeal_EntrustmentDate] DATE NULL,
        [CO_FurtherAppeal_AdvocateName] NVARCHAR(255) NULL,
        [CO_FurtherAppeal_CaseStatus] NVARCHAR(100) NULL,
        [CO_FurtherAppeal_DisposalOutwardNo] NVARCHAR(100) NULL,
        [CO_FurtherAppeal_DisposalDate] DATE NULL,
        [Opinion_CLO] NVARCHAR(MAX) NULL,
        [CO_Disposal_Nature] NVARCHAR(100) NULL,
        [CO_Disposal_CommSentToDivision] BIT NULL,
        [CO_Disposal_OutwardNo] NVARCHAR(100) NULL,
        [CO_Disposal_Date] DATE NULL,
        [CO_Disposal_Decision] NVARCHAR(100) NULL,
        [CO_Disposal_ApprovalOutwardNo] NVARCHAR(100) NULL,
        [CO_Disposal_ApprovalDate] DATE NULL,
        [CO_WP_JudgmentCopyPath] NVARCHAR(MAX) NULL,
        [NatureOfCase] NVARCHAR(100) NULL,
        [AwardAmount] DECIMAL(18, 2) NULL,
        [ClaimPetitionPath] NVARCHAR(MAX) NULL,
        [CNRNumber] NVARCHAR(16) NULL,
        [EstCode] NVARCHAR(50) NULL,
        [CaseTypeCode] NVARCHAR(20) NULL,
        [CO_WP_CNRNumber] NVARCHAR(16) NULL,
        [CO_WA_CNRNumber] NVARCHAR(16) NULL,
        [ActionTaken_LO] NVARCHAR(100) NULL,
        [ApprovalDate_LO] DATE NULL,
        [ActionTaken_DyCLO] NVARCHAR(100) NULL,
        [ApprovalDate_DyCLO] DATE NULL,
        [Opinion_DyCLO] NVARCHAR(MAX) NULL,
        [ActionTaken_CLO] NVARCHAR(100) NULL,
        [ApprovalDate_CLO] DATE NULL,
        [ActionTaken_MD] NVARCHAR(100) NULL,
        [ApprovalDate_MD] DATE NULL,
        [Opinion_MD] NVARCHAR(MAX) NULL
    );
END;
GO

-- -----------------------------------------------------------------------------
-- Table: [LABOUR_ARISING_ENCLOSED_DOCS]
-- -----------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.tables t JOIN sys.schemas s ON t.schema_id = s.schema_id WHERE t.name = 'LABOUR_ARISING_ENCLOSED_DOCS' AND s.name = 'dbo')
BEGIN
    CREATE TABLE [dbo].[LABOUR_ARISING_ENCLOSED_DOCS] (
        [DocID] INT IDENTITY(1,1) NOT NULL,
        [ArisingID] INT NOT NULL,
        [DocName] NVARCHAR(500) NULL,
        [PageCount] INT NULL
    );
END;
GO

-- -----------------------------------------------------------------------------
-- Table: [LABOUR_ARISING_PAYMENTS]
-- -----------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.tables t JOIN sys.schemas s ON t.schema_id = s.schema_id WHERE t.name = 'LABOUR_ARISING_PAYMENTS' AND s.name = 'dbo')
BEGIN
    CREATE TABLE [dbo].[LABOUR_ARISING_PAYMENTS] (
        [PaymentID] INT IDENTITY(1,1) NOT NULL,
        [ArisingID] INT NOT NULL,
        [Amount] DECIMAL(18, 2) NOT NULL,
        [PaymentDate] DATE NOT NULL,
        [ChequeNumber] NVARCHAR(50) NULL,
        [ChequeDate] DATE NULL,
        [Remarks] NVARCHAR(MAX) NULL,
        [CreatedDate] DATETIME NULL DEFAULT (getdate())
    );
END;
GO

-- -----------------------------------------------------------------------------
-- Table: [LABOUR_CASE_EVIDENCE]
-- -----------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.tables t JOIN sys.schemas s ON t.schema_id = s.schema_id WHERE t.name = 'LABOUR_CASE_EVIDENCE' AND s.name = 'dbo')
BEGIN
    CREATE TABLE [dbo].[LABOUR_CASE_EVIDENCE] (
        [EvidenceID] INT IDENTITY(1,1) NOT NULL,
        [CaseID] INT NOT NULL,
        [EvidenceType] NVARCHAR(100) NULL,
        [OtherDetails] NVARCHAR(200) NULL
    );
END;
GO

-- -----------------------------------------------------------------------------
-- Table: [LABOUR_CASE_HISTORY]
-- -----------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.tables t JOIN sys.schemas s ON t.schema_id = s.schema_id WHERE t.name = 'LABOUR_CASE_HISTORY' AND s.name = 'dbo')
BEGIN
    CREATE TABLE [dbo].[LABOUR_CASE_HISTORY] (
        [HistoryID] INT IDENTITY(1,1) NOT NULL,
        [CaseID] INT NOT NULL,
        [CurrentStage] NVARCHAR(200) NULL,
        [NextHearingDate] DATE NULL,
        [Remarks] NVARCHAR(MAX) NULL,
        [CreatedDate] DATETIME NULL DEFAULT (getdate())
    );
END;
GO

-- -----------------------------------------------------------------------------
-- Table: [LABOUR_CASE_VIEW_TRACKING]
-- -----------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.tables t JOIN sys.schemas s ON t.schema_id = s.schema_id WHERE t.name = 'LABOUR_CASE_VIEW_TRACKING' AND s.name = 'dbo')
BEGIN
    CREATE TABLE [dbo].[LABOUR_CASE_VIEW_TRACKING] (
        [ViewID] INT IDENTITY(1,1) NOT NULL,
        [CaseID] INT NOT NULL,
        [ViewedAt] DATETIME NULL DEFAULT (getdate())
    );
END;
GO

-- -----------------------------------------------------------------------------
-- Table: [LABOUR_CASES]
-- -----------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.tables t JOIN sys.schemas s ON t.schema_id = s.schema_id WHERE t.name = 'LABOUR_CASES' AND s.name = 'dbo')
BEGIN
    CREATE TABLE [dbo].[LABOUR_CASES] (
        [CaseID] INT IDENTITY(1,1) NOT NULL,
        [DivisionID] INT NULL,
        [CaseType] NVARCHAR(50) NOT NULL,
        [CaseStatus] NVARCHAR(50) NOT NULL,
        [CaseNumber] NVARCHAR(100) NOT NULL,
        [CaseYear] INT NOT NULL,
        [CourtID] INT NULL,
        [OtherCourtDetails] NVARCHAR(200) NULL,
        [PetitionerName] NVARCHAR(200) NOT NULL,
        [EmployeeNo] NVARCHAR(50) NULL,
        [Designation] NVARCHAR(100) NULL,
        [WorkingStatus] NVARCHAR(50) NULL,
        [NatureOfCase] NVARCHAR(100) NULL,
        [NatureOfMisconduct] NVARCHAR(MAX) NULL,
        [EntrustmentNo] NVARCHAR(100) NULL,
        [EntrustmentDate] DATETIME NULL,
        [AdvocateID] INT NULL,
        [AdvocateName] NVARCHAR(200) NULL,
        [IsDoubleClaim] BIT NULL DEFAULT ((0)),
        [CurrentStage] NVARCHAR(200) NULL,
        [CaseHistory] NVARCHAR(MAX) NULL,
        [DisposalMode] NVARCHAR(50) NULL,
        [DisposalDate] DATETIME NULL,
        [DisposalResult] NVARCHAR(50) NULL,
        [LokAdalat_COApprovalRequired] BIT NULL DEFAULT ((0)),
        [LokAdalat_OutwardNo] NVARCHAR(100) NULL,
        [LokAdalat_Date] DATETIME NULL,
        [Against_CaseCategory] NVARCHAR(50) NULL,
        [Against_BriefFacts] NVARCHAR(MAX) NULL,
        [Against_PunishmentImposed] NVARCHAR(100) NULL,
        [Against_PunishmentNo] NVARCHAR(100) NULL,
        [Against_PunishmentDate] DATETIME NULL,
        [DE_HistorySheet] BIT NULL DEFAULT ((0)),
        [DE_ObjectionsFiled] BIT NULL DEFAULT ((0)),
        [DE_DocumentsMarked] NVARCHAR(MAX) NULL,
        [DE_Order] NVARCHAR(50) NULL,
        [DE_EO_Name] NVARCHAR(200) NULL,
        [DE_EO_Designation] NVARCHAR(100) NULL,
        [DE_Reporter_Name] NVARCHAR(200) NULL,
        [DE_Reporter_Designation] NVARCHAR(100) NULL,
        [CC_PublicationDate] DATETIME NULL,
        [CC_AppliedDate] DATETIME NULL,
        [CC_IssuedDate] DATETIME NULL,
        [CC_ReceivedDate] DATETIME NULL,
        [CC_Remarks] NVARCHAR(MAX) NULL,
        [AwardDetails] NVARCHAR(MAX) NULL,
        [Opinion_Advocate] NVARCHAR(MAX) NULL,
        [Opinion_LO] NVARCHAR(MAX) NULL,
        [Opinion_DC] NVARCHAR(MAX) NULL,
        [SentToCO] BIT NULL DEFAULT ((0)),
        [CO_OutwardNo] NVARCHAR(100) NULL,
        [CO_OutwardDate] DATETIME NULL,
        [IsArisingApplication] BIT NULL DEFAULT ((0)),
        [Arising_OriginalCaseNumber] NVARCHAR(100) NULL,
        [Arising_OriginalCaseYear] INT NULL,
        [Arising_OriginalCourt] NVARCHAR(200) NULL,
        [CreatedDate] DATETIME NULL DEFAULT (getdate()),
        [CreatedBy] INT NULL,
        [ModifiedDate] DATETIME NULL,
        [ModifiedBy] INT NULL,
        [Arising_CurrentStatus] NVARCHAR(100) NULL,
        [Arising_ApplicationStatus] NVARCHAR(100) NULL,
        [IsCOApprovalRequired] BIT NULL DEFAULT ((0)),
        [COApproval_OutwardNo] NVARCHAR(50) NULL,
        [COApproval_OutwardDate] DATETIME NULL,
        [IsDocumentSent] BIT NULL DEFAULT ((0)),
        [DocumentSent_OutwardNo] NVARCHAR(50) NULL,
        [DocumentSent_OutwardDate] DATETIME NULL,
        [IsObjectionFiled] BIT NULL DEFAULT ((0)),
        [ObjectionFiled_OutwardNo] NVARCHAR(50) NULL,
        [ObjectionFiled_OutwardDate] DATETIME NULL,
        [DoubleClaimRemarks] NVARCHAR(MAX) NULL,
        [NextHearingDate] DATETIME NULL,
        [FavorRemark] NVARCHAR(MAX) NULL,
        [DE_HistorySheetPath] NVARCHAR(MAX) NULL,
        [JudgmentCopyPath] NVARCHAR(MAX) NULL,
        [CC_CaseDisposedDate] DATETIME NULL,
        [CO_Remarks] NVARCHAR(MAX) NULL,
        [CO_FeasibilityReceived] BIT NULL,
        [CO_FeasibilityDate] DATETIME2 NULL,
        [CO_ActionTaken] NVARCHAR(100) NULL,
        [CO_ApprovalOutwardNo] NVARCHAR(50) NULL,
        [CO_ApprovalDate] DATETIME2 NULL,
        [CO_WP_CaseStatus_Option] NVARCHAR(100) NULL,
        [CO_WP_CaseNumber] NVARCHAR(100) NULL,
        [CO_WP_Year] INT NULL,
        [CO_WP_HighCourtBench] NVARCHAR(100) NULL,
        [CO_WP_EntrustmentNo] NVARCHAR(100) NULL,
        [CO_WP_EntrustmentDate] DATETIME2 NULL,
        [CO_WP_AdvocateName] NVARCHAR(200) NULL,
        [CO_WP_StayGranted] BIT NULL,
        [CO_WP_StayApprovalNo] NVARCHAR(100) NULL,
        [CO_WP_StayNature] NVARCHAR(100) NULL,
        [CO_WP_StayDate] DATETIME2 NULL,
        [CO_ReinstatedSubjectToWP] NVARCHAR(100) NULL,
        [CO_ReinstatementApprovalIssued] BIT NULL,
        [CO_ReinstatementApprovalDate] DATETIME2 NULL,
        [CO_WP_Status] NVARCHAR(50) NULL,
        [CO_OverallCaseStatus] NVARCHAR(50) NULL,
        [CO_Disposal_Nature] NVARCHAR(100) NULL,
        [CO_Disposal_CommSentToDivision] BIT NULL,
        [CO_Disposal_OutwardNo] NVARCHAR(100) NULL,
        [CO_Disposal_Date] DATETIME2 NULL,
        [CO_Disposal_Decision] NVARCHAR(100) NULL,
        [CO_Disposal_ApprovalOutwardNo] NVARCHAR(100) NULL,
        [CO_Disposal_ApprovalDate] DATETIME2 NULL,
        [CO_FurtherAppeal_Status_Option] NVARCHAR(100) NULL,
        [CO_FurtherAppeal_CaseNumber] NVARCHAR(100) NULL,
        [CO_FurtherAppeal_Year] INT NULL,
        [CO_FurtherAppeal_EntrustmentNo] NVARCHAR(100) NULL,
        [CO_FurtherAppeal_EntrustmentDate] DATETIME2 NULL,
        [CO_FurtherAppeal_AdvocateName] NVARCHAR(200) NULL,
        [CO_FurtherAppeal_CaseStatus] NVARCHAR(50) NULL,
        [CO_Claimant_DivisionName] NVARCHAR(200) NULL,
        [CO_Claimant_ArisingOutOf] NVARCHAR(100) NULL,
        [CO_Claimant_Court] NVARCHAR(200) NULL,
        [CO_Claimant_CaseNumber] NVARCHAR(100) NULL,
        [CO_Claimant_HighCourtBench] NVARCHAR(100) NULL,
        [CO_Claimant_OriginalCaseStatus] NVARCHAR(100) NULL,
        [CO_Claimant_IsConnected] BIT NULL,
        [CO_Claimant_ConnectedDetails] NVARCHAR(MAX) NULL,
        [CO_Claimant_EntrustmentDate] DATETIME2 NULL,
        [CO_Claimant_AdvocateName] NVARCHAR(200) NULL,
        [CO_Claimant_CaseStatus] NVARCHAR(50) NULL,
        [CO_Service_Division] NVARCHAR(200) NULL,
        [CO_Service_WPNumber] NVARCHAR(100) NULL,
        [CO_Service_PetitionerName] NVARCHAR(200) NULL,
        [CO_Service_CaseNature] NVARCHAR(100) NULL,
        [CO_Service_Prayer] NVARCHAR(MAX) NULL,
        [CO_Service_StayGranted] BIT NULL,
        [CO_Service_StayVacateFiled] BIT NULL,
        [CO_Service_Status] NVARCHAR(50) NULL,
        [CO_Service_DisposalDate] DATETIME2 NULL,
        [CO_Service_ActionTaken] NVARCHAR(100) NULL,
        [CO_Service_ApprovalSentDetails] NVARCHAR(MAX) NULL,
        [CO_Service_OutwardNo] NVARCHAR(100) NULL,
        [CO_Service_AppealFiledBefore] NVARCHAR(100) NULL,
        [CO_Service_AppealType] NVARCHAR(50) NULL,
        [CO_Service_AppealEntrustmentDate] DATETIME2 NULL,
        [CO_Service_AppealAdvocate] NVARCHAR(200) NULL,
        [CO_Service_AppealStatus] NVARCHAR(50) NULL,
        [CO_Service_OutwardDate] DATETIME2 NULL,
        [CO_FurtherAppeal_DisposalOutwardNo] NVARCHAR(100) NULL,
        [CO_FurtherAppeal_DisposalDate] DATETIME2 NULL,
        [DE_EO_IsBasedOnDocuments] BIT NOT NULL DEFAULT ((0)),
        [DE_Reporter_IsBasedOnDocuments] BIT NOT NULL DEFAULT ((0)),
        [DE_Other_Name] NVARCHAR(MAX) NULL,
        [DE_Other_Designation] NVARCHAR(MAX) NULL,
        [DE_Other_IsBasedOnDocuments] BIT NOT NULL DEFAULT ((0)),
        [DE_ObjectionsRemark] NVARCHAR(MAX) NULL,
        [DE_DocumentsRemark] NVARCHAR(MAX) NULL,
        [FavorOutwardDate] DATE NULL,
        [CO_WP_StayOrderPath] NVARCHAR(MAX) NULL,
        [CO_WP_StayRemark] NVARCHAR(MAX) NULL,
        [CO_WP_Outcome] NVARCHAR(50) NULL,
        [CO_WP_OutcomeRemark] NVARCHAR(MAX) NULL,
        [CO_WP_OutcomeOutwardNo] NVARCHAR(50) NULL,
        [CO_WP_OutcomeOutwardDate] DATE NULL,
        [CO_Service_PetitionCopyPath] NVARCHAR(MAX) NULL,
        [TransferredFromDivisionID] INT NULL,
        [TransferDate] DATETIME NULL,
        [IsTransferViewed] BIT NOT NULL DEFAULT ((0)),
        [IsEnquiryOfficerEvidence] BIT NULL DEFAULT ((0)),
        [IsReporterEvidence] BIT NULL DEFAULT ((0)),
        [IsOtherEvidence] BIT NULL DEFAULT ((0)),
        [CO_IsWorkmanReinstated] BIT NOT NULL DEFAULT ((0)),
        [CO_Reinstatement_StayGranted] BIT NULL,
        [CO_Reinstatement_StayApprovalNo] NVARCHAR(100) NULL,
        [CO_Reinstatement_StayNature] NVARCHAR(100) NULL,
        [CO_Reinstatement_StayDate] DATETIME NULL,
        [CO_Reinstatement_StayOrderPath] NVARCHAR(MAX) NULL,
        [CO_Reinstatement_StayRemark] NVARCHAR(MAX) NULL,
        [CO_WP_ActionTaken] NVARCHAR(100) NULL,
        [CO_StayComplianceRemark] NVARCHAR(MAX) NULL,
        [CO_StayComplianceFilePath] NVARCHAR(MAX) NULL,
        [CO_WA_CaseStatus_Option] NVARCHAR(100) NULL,
        [CO_WA_CaseNumber] NVARCHAR(100) NULL,
        [CO_WA_Year] INT NULL,
        [CO_WA_HighCourtBench] NVARCHAR(100) NULL,
        [CO_WA_EntrustmentNo] NVARCHAR(100) NULL,
        [CO_WA_EntrustmentDate] DATETIME NULL,
        [CO_WA_AdvocateName] NVARCHAR(200) NULL,
        [CO_WA_StayGranted] BIT NULL,
        [CO_WA_StayApprovalNo] NVARCHAR(100) NULL,
        [CO_WA_StayNature] NVARCHAR(100) NULL,
        [CO_WA_StayDate] DATETIME NULL,
        [CO_WA_StayOrderPath] NVARCHAR(MAX) NULL,
        [CO_WA_StayRemark] NVARCHAR(MAX) NULL,
        [CO_WA_Status] NVARCHAR(100) NULL,
        [CO_WA_Outcome] NVARCHAR(100) NULL,
        [CO_WA_OutcomeRemark] NVARCHAR(MAX) NULL,
        [CO_WA_OutcomeOutwardNo] NVARCHAR(100) NULL,
        [CO_WA_OutcomeOutwardDate] DATE NULL,
        [CO_WA_ActionTaken] NVARCHAR(100) NULL,
        [CO_Claimant_CaseYear] INT NULL,
        [IsClaimantSCPending] BIT NULL,
        [ClaimantSCDiaryNumber] NVARCHAR(50) NULL,
        [ClaimantSCYear] INT NULL,
        [ClaimantSCNumber] NVARCHAR(50) NULL,
        [ClaimantSLPYear] INT NULL,
        [ClaimantSCFiledBy] NVARCHAR(50) NULL,
        [ClaimantSCEntrustmentNo] NVARCHAR(50) NULL,
        [ClaimantSCEntrustmentDate] DATE NULL,
        [ClaimantSCAdvocate] NVARCHAR(200) NULL,
        [ClaimantSCStatus] NVARCHAR(50) NULL,
        [ClaimantSCOutcome] NVARCHAR(100) NULL,
        [ClaimantSCActionTaken] NVARCHAR(100) NULL,
        [ClaimantSCClosureNo] NVARCHAR(50) NULL,
        [ClaimantSCClosureDate] DATE NULL,
        [LokAdalatDocumentPath] NVARCHAR(500) NULL,
        [Opinion_CLO] NVARCHAR(MAX) NULL,
        [IsFiledWithinLimitation] BIT NULL,
        [LimitationRemark] NVARCHAR(MAX) NULL,
        [IsDelayCondoned] BIT NULL,
        [DelayCondonationRemark] NVARCHAR(MAX) NULL,
        [CO_StayComplianceDate] DATETIME NULL,
        [CO_FeasibilityDocumentPath] NVARCHAR(MAX) NULL,
        [CO_ClosedDocumentPath] NVARCHAR(MAX) NULL,
        [CO_Service_StayCompliance] BIT NULL,
        [CO_Service_ApprovalOutwardNo] NVARCHAR(100) NULL,
        [CO_Service_ApprovalDate] DATE NULL,
        [CO_Service_ApprovalCopyPath] NVARCHAR(MAX) NULL,
        [CO_Service_EntrustmentNo] NVARCHAR(100) NULL,
        [CO_Service_EntrustmentDate] DATE NULL,
        [PFNumber] NVARCHAR(50) NULL,
        [IsWorkman] BIT NULL DEFAULT ((0)),
        [IsWorkmanRemark] NVARCHAR(MAX) NULL,
        [SerialApp_CaseNumber] NVARCHAR(100) NULL,
        [SerialApp_CurrentStage] NVARCHAR(200) NULL,
        [Against_PunishmentCopyPath] NVARCHAR(MAX) NULL,
        [IsEPFiled] BIT NOT NULL DEFAULT ((0)),
        [CC_DeliveredDate] DATE NULL,
        [CO_Claimant_EntrustmentNo] NVARCHAR(100) NULL,
        [CO_Claimant_PetitionCopyPath] NVARCHAR(MAX) NULL,
        [IsReinstatementViewed] BIT NOT NULL DEFAULT ((0)),
        [CO_Reinstatement_ApprovalNo] NVARCHAR(100) NULL,
        [CO_Reinstatement_ApprovalCopyPath] NVARCHAR(MAX) NULL,
        [IsViewedByCO] BIT NOT NULL DEFAULT ((0)),
        [ClaimPetitionPath] NVARCHAR(MAX) NULL,
        [ClaimFiledOn] NVARCHAR(200) NULL,
        [DelayInFiling] NVARCHAR(200) NULL,
        [DE_EO_BasedOnDocsRemark] NVARCHAR(MAX) NULL,
        [DE_Reporter_BasedOnDocsRemark] NVARCHAR(MAX) NULL,
        [DE_Other_BasedOnDocsRemark] NVARCHAR(MAX) NULL,
        [ChargesStatus] NVARCHAR(50) NULL,
        [TerminalBenefitsPaid] NVARCHAR(MAX) NULL,
        [SerialApplicationDetails] NVARCHAR(MAX) NULL,
        [IsRepeatDismissal] BIT NULL DEFAULT ((0)),
        [RepeatDismissalRemark] NVARCHAR(MAX) NULL,
        [HasAppealDetails] BIT NULL DEFAULT ((0)),
        [AppealDetailsRemark] NVARCHAR(MAX) NULL,
        [LegalRepresentativeName] NVARCHAR(200) NULL,
        [ClaimDetails] NVARCHAR(MAX) NULL,
        [LRRelationship] NVARCHAR(100) NULL,
        [JudgmentCopyPath2] NVARCHAR(500) NULL,
        [ServiceID] INT NULL,
        [CO_Service_WPYear] INT NULL,
        [CO_Service_IsEmployee] BIT NULL DEFAULT ((0)),
        [CO_WP_JudgmentCopyPath] NVARCHAR(MAX) NULL,
        [CNRNumber] NVARCHAR(50) NULL,
        [EstCode] NVARCHAR(50) NULL,
        [CaseTypeCode] NVARCHAR(50) NULL,
        [CO_WP_CNRNumber] NVARCHAR(16) NULL,
        [CO_WA_CNRNumber] NVARCHAR(16) NULL,
        [CO_Claimant_CNRNumber] NVARCHAR(16) NULL,
        [ActionTaken_LO] NVARCHAR(100) NULL,
        [ApprovalDate_LO] DATE NULL,
        [ActionTaken_DyCLO] NVARCHAR(100) NULL,
        [ApprovalDate_DyCLO] DATE NULL,
        [Opinion_DyCLO] NVARCHAR(MAX) NULL,
        [ActionTaken_CLO] NVARCHAR(100) NULL,
        [ApprovalDate_CLO] DATE NULL,
        [ActionTaken_MD] NVARCHAR(100) NULL,
        [ApprovalDate_MD] DATE NULL,
        [Opinion_MD] NVARCHAR(MAX) NULL,
        [LastNapixSyncAt] DATETIME2 NULL,
        [LastNapixSyncStatus] NVARCHAR(30) NULL,
        [LastNapixSyncError] NVARCHAR(500) NULL,
        [NapixSyncAttemptCount] INT NOT NULL DEFAULT ((0)),
        [NapixDataHash] NVARCHAR(64) NULL,
        [PendDispStatus] NVARCHAR(20) NULL,
        [EstName] NVARCHAR(250) NULL,
        [ECourtsStage] NVARCHAR(150) NULL,
        [ECourtsCourtNo] NVARCHAR(50) NULL,
        [ECourtsJudge] NVARCHAR(250) NULL
    );
END;
GO

-- -----------------------------------------------------------------------------
-- Table: [LABOUR_CONNECTED_CASES]
-- -----------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.tables t JOIN sys.schemas s ON t.schema_id = s.schema_id WHERE t.name = 'LABOUR_CONNECTED_CASES' AND s.name = 'dbo')
BEGIN
    CREATE TABLE [dbo].[LABOUR_CONNECTED_CASES] (
        [ConnectedCaseID] INT IDENTITY(1,1) NOT NULL,
        [CaseID] INT NOT NULL,
        [CaseDetails] NVARCHAR(500) NULL,
        [FiledBy] NVARCHAR(100) NULL,
        [CurrentStatus] NVARCHAR(200) NULL,
        [CaseType] NVARCHAR(100) NULL
    );
END;
GO

-- -----------------------------------------------------------------------------
-- Table: [LABOUR_COURTS]
-- -----------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.tables t JOIN sys.schemas s ON t.schema_id = s.schema_id WHERE t.name = 'LABOUR_COURTS' AND s.name = 'dbo')
BEGIN
    CREATE TABLE [dbo].[LABOUR_COURTS] (
        [CourtID] INT IDENTITY(1,1) NOT NULL,
        [CourtName] NVARCHAR(200) NOT NULL,
        [Location] NVARCHAR(100) NULL,
        [IsActive] BIT NULL DEFAULT ((1))
    );
END;
GO

-- -----------------------------------------------------------------------------
-- Table: [LABOUR_ENCLOSED_DOCS]
-- -----------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.tables t JOIN sys.schemas s ON t.schema_id = s.schema_id WHERE t.name = 'LABOUR_ENCLOSED_DOCS' AND s.name = 'dbo')
BEGIN
    CREATE TABLE [dbo].[LABOUR_ENCLOSED_DOCS] (
        [DocID] INT IDENTITY(1,1) NOT NULL,
        [CaseID] INT NULL,
        [DocName] NVARCHAR(500) NULL,
        [PageCount] INT NULL
    );
END;
GO

-- -----------------------------------------------------------------------------
-- Table: [LABOUR_EP_DETAILS]
-- -----------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.tables t JOIN sys.schemas s ON t.schema_id = s.schema_id WHERE t.name = 'LABOUR_EP_DETAILS' AND s.name = 'dbo')
BEGIN
    CREATE TABLE [dbo].[LABOUR_EP_DETAILS] (
        [EPID] INT IDENTITY(1,1) NOT NULL,
        [CaseID] INT NOT NULL,
        [DivisionID] INT NOT NULL DEFAULT ((0)),
        [EPNumber] NVARCHAR(50) NULL,
        [EPYear] INT NULL,
        [EPCourt] NVARCHAR(200) NULL,
        [ArisingFromCaseNo] NVARCHAR(100) NULL,
        [ArisingFromCaseYear] INT NULL,
        [ArisingFromCourt] NVARCHAR(200) NULL,
        [OriginalCaseStatus] NVARCHAR(50) NULL,
        [EPStatus] NVARCHAR(50) NULL,
        [NextHearingDate] DATE NULL,
        [AsPerECourts] NVARCHAR(200) NULL,
        [IsSentToAccounts] BIT NOT NULL DEFAULT ((0)),
        [DateSentToAccounts] DATE NULL,
        [AwardAmount] DECIMAL(18, 2) NULL,
        [InterestRate] DECIMAL(5, 2) NULL,
        [LiabilityPercentage] DECIMAL(5, 2) NULL,
        [PetitionDate] DATE NULL,
        [PettyBillDate] DATE NULL,
        [PettyBillAmount] DECIMAL(18, 2) NULL,
        [ChequeNumber] NVARCHAR(50) NULL,
        [ChequeDate] DATE NULL,
        [AmountPaid] DECIMAL(18, 2) NULL,
        [ComplianceStatus] NVARCHAR(50) NULL,
        [DateOfCompliance] DATE NULL,
        [Remarks] NVARCHAR(MAX) NULL,
        [CreatedBy] INT NULL,
        [CreatedDate] DATETIME NULL DEFAULT (getdate()),
        [ModifiedBy] INT NULL,
        [ModifiedDate] DATETIME NULL,
        [EP_EntrustmentNo] NVARCHAR(100) NULL,
        [EP_EntrustmentDate] DATE NULL,
        [AdvocateID] INT NULL,
        [AdvocateName] NVARCHAR(200) NULL,
        [CaseOutcome] NVARCHAR(100) NULL,
        [DisposalDate] DATE NULL,
        [DisposalRemarks] NVARCHAR(MAX) NULL,
        [ClosureDate] DATE NULL,
        [CNRNumber] NVARCHAR(16) NULL,
        [EstCode] NVARCHAR(50) NULL,
        [CaseTypeCode] NVARCHAR(20) NULL
    );
END;
GO

-- -----------------------------------------------------------------------------
-- Table: [LABOUR_EP_PAYMENTS]
-- -----------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.tables t JOIN sys.schemas s ON t.schema_id = s.schema_id WHERE t.name = 'LABOUR_EP_PAYMENTS' AND s.name = 'dbo')
BEGIN
    CREATE TABLE [dbo].[LABOUR_EP_PAYMENTS] (
        [PaymentID] INT IDENTITY(1,1) NOT NULL,
        [EPID] INT NOT NULL,
        [Amount] DECIMAL(18, 2) NOT NULL,
        [PaymentDate] DATE NOT NULL,
        [ChequeNumber] NVARCHAR(50) NULL,
        [ChequeDate] DATE NULL,
        [Remarks] NVARCHAR(MAX) NULL
    );
END;
GO

-- -----------------------------------------------------------------------------
-- Table: [LABOUR_REINSTATED_DOCUMENTS]
-- -----------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.tables t JOIN sys.schemas s ON t.schema_id = s.schema_id WHERE t.name = 'LABOUR_REINSTATED_DOCUMENTS' AND s.name = 'dbo')
BEGIN
    CREATE TABLE [dbo].[LABOUR_REINSTATED_DOCUMENTS] (
        [DocumentID] INT IDENTITY(1,1) NOT NULL,
        [CaseID] INT NOT NULL,
        [DocumentName] NVARCHAR(255) NOT NULL,
        [DocumentPath] NVARCHAR(MAX) NOT NULL,
        [UploadedDate] DATETIME NULL DEFAULT (getdate())
    );
END;
GO

-- -----------------------------------------------------------------------------
-- Table: [LABOUR_SERVICE_MATTERS]
-- -----------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.tables t JOIN sys.schemas s ON t.schema_id = s.schema_id WHERE t.name = 'LABOUR_SERVICE_MATTERS' AND s.name = 'dbo')
BEGIN
    CREATE TABLE [dbo].[LABOUR_SERVICE_MATTERS] (
        [ServiceID] INT IDENTITY(1,1) NOT NULL,
        [CaseID] INT NULL,
        [DivisionID] INT NOT NULL,
        [WPNumber] NVARCHAR(100) NULL,
        [PetitionerName] NVARCHAR(200) NULL,
        [CaseNature] NVARCHAR(100) NULL,
        [Prayer] NVARCHAR(MAX) NULL,
        [PetitionCopyPath] NVARCHAR(500) NULL,
        [StayGranted] BIT NULL,
        [StayVacateFiled] BIT NULL,
        [StayCompliance] BIT NULL,
        [ApprovalOutwardNo] NVARCHAR(100) NULL,
        [ApprovalDate] DATE NULL,
        [ApprovalCopyPath] NVARCHAR(500) NULL,
        [Status] NVARCHAR(50) NULL,
        [DisposalDate] DATE NULL,
        [ActionTaken] NVARCHAR(100) NULL,
        [ApprovalSentDetails] NVARCHAR(MAX) NULL,
        [OutwardNo] NVARCHAR(100) NULL,
        [OutwardDate] DATE NULL,
        [EntrustmentNo] NVARCHAR(100) NULL,
        [EntrustmentDate] DATE NULL,
        [AppealFiledBefore] NVARCHAR(100) NULL,
        [AppealType] NVARCHAR(50) NULL,
        [AppealEntrustmentDate] DATE NULL,
        [AppealAdvocate] NVARCHAR(200) NULL,
        [AppealStatus] NVARCHAR(50) NULL,
        [WA_CaseNumber] NVARCHAR(100) NULL,
        [WA_Year] INT NULL,
        [WA_HighCourtBench] NVARCHAR(100) NULL,
        [WA_EntrustmentNo] NVARCHAR(100) NULL,
        [WA_EntrustmentDate] DATE NULL,
        [WA_AdvocateName] NVARCHAR(200) NULL,
        [WA_StayGranted] BIT NULL,
        [WA_StayApprovalNo] NVARCHAR(100) NULL,
        [WA_StayNature] NVARCHAR(100) NULL,
        [WA_StayDate] DATE NULL,
        [WA_StayOrderPath] NVARCHAR(500) NULL,
        [WA_StayRemark] NVARCHAR(MAX) NULL,
        [WA_Status] NVARCHAR(50) NULL,
        [WA_Outcome] NVARCHAR(50) NULL,
        [WA_OutcomeRemark] NVARCHAR(MAX) NULL,
        [WA_OutcomeOutwardNo] NVARCHAR(100) NULL,
        [WA_OutcomeOutwardDate] DATE NULL,
        [WA_ActionTaken] NVARCHAR(50) NULL,
        [SC_Pending] BIT NULL DEFAULT ((0)),
        [SC_DiaryNumber] NVARCHAR(50) NULL,
        [SC_Year] INT NULL,
        [SC_Number] NVARCHAR(50) NULL,
        [SC_SLPYear] INT NULL,
        [SC_FiledBy] NVARCHAR(50) NULL,
        [SC_EntrustmentNo] NVARCHAR(50) NULL,
        [SC_EntrustmentDate] DATE NULL,
        [SC_Advocate] NVARCHAR(200) NULL,
        [SC_Status] NVARCHAR(50) NULL,
        [SC_Outcome] NVARCHAR(100) NULL,
        [SC_ActionTaken] NVARCHAR(100) NULL,
        [SC_ClosureNo] NVARCHAR(50) NULL,
        [SC_ClosureDate] DATE NULL,
        [CreatedDate] DATETIME NULL DEFAULT (getdate()),
        [CreatedBy] INT NULL,
        [ModifiedDate] DATETIME NULL,
        [ModifiedBy] INT NULL,
        [WPYear] INT NULL,
        [IsEmployee] BIT NULL DEFAULT ((0)),
        [CNRNumber] NVARCHAR(16) NULL,
        [EstCode] NVARCHAR(50) NULL,
        [CaseTypeCode] NVARCHAR(50) NULL,
        [NextHearingDate] DATE NULL,
        [Stage] NVARCHAR(150) NULL,
        [CourtHall] NVARCHAR(150) NULL,
        [ActionTaken_LO] NVARCHAR(100) NULL,
        [ApprovalDate_LO] DATE NULL,
        [Opinion_LO] NVARCHAR(MAX) NULL,
        [ActionTaken_DyCLO] NVARCHAR(100) NULL,
        [ApprovalDate_DyCLO] DATE NULL,
        [Opinion_DyCLO] NVARCHAR(MAX) NULL,
        [ActionTaken_CLO] NVARCHAR(100) NULL,
        [ApprovalDate_CLO] DATE NULL,
        [Opinion_CLO] NVARCHAR(MAX) NULL,
        [ActionTaken_MD] NVARCHAR(100) NULL,
        [ApprovalDate_MD] DATE NULL,
        [Opinion_MD] NVARCHAR(MAX) NULL
    );
END;
GO

-- -----------------------------------------------------------------------------
-- Table: [MACT_MASTER]
-- -----------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.tables t JOIN sys.schemas s ON t.schema_id = s.schema_id WHERE t.name = 'MACT_MASTER' AND s.name = 'dbo')
BEGIN
    CREATE TABLE [dbo].[MACT_MASTER] (
        [MACTID] INT IDENTITY(1,1) NOT NULL,
        [MACTCode] NVARCHAR(20) NOT NULL,
        [MACTName] NVARCHAR(100) NOT NULL,
        [Location] NVARCHAR(100) NULL,
        [IsActive] BIT NULL DEFAULT ((1)),
        [CreatedBy] NVARCHAR(50) NULL,
        [CreatedDate] DATETIME NULL DEFAULT (getdate()),
        [ModifiedBy] NVARCHAR(50) NULL,
        [ModifiedDate] DATETIME NULL
    );
END;
GO

-- -----------------------------------------------------------------------------
-- Table: [MVC_CASE]
-- -----------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.tables t JOIN sys.schemas s ON t.schema_id = s.schema_id WHERE t.name = 'MVC_CASE' AND s.name = 'dbo')
BEGIN
    CREATE TABLE [dbo].[MVC_CASE] (
        [CaseID] INT IDENTITY(1,1) NOT NULL,
        [DivisionID] INT NOT NULL,
        [MVCNo] NVARCHAR(50) NOT NULL,
        [MVCYear] INT NOT NULL,
        [MACTID] INT NOT NULL,
        [NoticeDate] DATE NOT NULL,
        [StatusID] INT NOT NULL,
        [Remarks] NVARCHAR(500) NULL,
        [DisposalDate] DATE NULL,
        [CreatedBy] NVARCHAR(50) NULL,
        [CreatedDate] DATETIME NULL DEFAULT (getdate()),
        [ModifiedBy] NVARCHAR(50) NULL,
        [ModifiedDate] DATETIME NULL
    );
END;
GO

-- -----------------------------------------------------------------------------
-- Table: [MVC_CASE_ADVERSE_CONNECTED]
-- -----------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.tables t JOIN sys.schemas s ON t.schema_id = s.schema_id WHERE t.name = 'MVC_CASE_ADVERSE_CONNECTED' AND s.name = 'dbo')
BEGIN
    CREATE TABLE [dbo].[MVC_CASE_ADVERSE_CONNECTED] (
        [AdvConnID] INT IDENTITY(1,1) NOT NULL,
        [CaseID] INT NULL,
        [CaseDetails] NVARCHAR(500) NULL,
        [Status] NVARCHAR(200) NULL,
        [AwardAmount] DECIMAL(18, 2) NULL,
        [InterestRate] DECIMAL(5, 2) NULL,
        [VictimAge] INT NULL,
        [Occupation] NVARCHAR(100) NULL,
        [IncomeConsidered] DECIMAL(18, 2) NULL,
        [InjuryDetails] NVARCHAR(MAX) NULL,
        [CurrentStage] NVARCHAR(100) NULL
    );
END;
GO

-- -----------------------------------------------------------------------------
-- Table: [MVC_CASE_ADVERSE_DETAILS]
-- -----------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.tables t JOIN sys.schemas s ON t.schema_id = s.schema_id WHERE t.name = 'MVC_CASE_ADVERSE_DETAILS' AND s.name = 'dbo')
BEGIN
    CREATE TABLE [dbo].[MVC_CASE_ADVERSE_DETAILS] (
        [AdverseID] INT IDENTITY(1,1) NOT NULL,
        [CaseID] INT NULL,
        [BusInsuranceDetails] NVARCHAR(MAX) NULL,
        [IsPrivateHired] BIT NULL DEFAULT ((0)),
        [ClaimPetitionDate] DATE NULL,
        [AwardDate] DATE NULL,
        [ObjectionFiled] BIT NULL DEFAULT ((0)),
        [ObjectionRemarks] NVARCHAR(MAX) NULL,
        [RWType] NVARCHAR(50) NULL,
        [TR18Remarks] NVARCHAR(MAX) NULL,
        [TR18UploadPath] NVARCHAR(MAX) NULL,
        [IsAllegedAccident] BIT NULL DEFAULT ((0)),
        [AdverseDoubleClaimFlag] BIT NULL DEFAULT ((0)),
        [DoubleClaimDetails] NVARCHAR(MAX) NULL,
        [SecurityRequired] BIT NULL DEFAULT ((0)),
        [SecurityUploadPath] NVARCHAR(MAX) NULL,
        [DisposedOnDate] DATE NULL,
        [CopyAppliedDate] DATE NULL,
        [CertifiedCopyRemarks] NVARCHAR(MAX) NULL,
        [CopyIssuedDate] DATE NULL,
        [CopyReceivedDate] DATE NULL,
        [AwardAmount] DECIMAL(18, 2) NULL,
        [InterestRate] DECIMAL(5, 2) NULL,
        [VictimAge] INT NULL,
        [Occupation] NVARCHAR(100) NULL,
        [IncomeConsidered] DECIMAL(18, 2) NULL,
        [InjuryDetails] NVARCHAR(MAX) NULL,
        [DriverPunishmentStatus] NVARCHAR(50) NULL,
        [PunishmentOrderUploadPath] NVARCHAR(MAX) NULL,
        [InterimCompAmount] DECIMAL(18, 2) NULL,
        [InterimCompDeducted] BIT NULL DEFAULT ((0)),
        [AdvocateOpinion] NVARCHAR(MAX) NULL,
        [LOOpinion] NVARCHAR(MAX) NULL,
        [DCOpinion] NVARCHAR(MAX) NULL,
        [ForwardingStatus] NVARCHAR(50) NULL,
        [ClosureRemarks] NVARCHAR(MAX) NULL,
        [OutwardNumber] NVARCHAR(50) NULL,
        [OutwardDate] DATE NULL,
        [IsBusInsured] BIT NOT NULL DEFAULT ((0)),
        [MannerOfAccident] NVARCHAR(MAX) NULL,
        [CopyDeliveredDate] DATE NULL,
        [IsCorpLiable] BIT NULL DEFAULT ((0)),
        [LiabilityPercentage] DECIMAL(5, 2) NULL DEFAULT ((100.00)),
        [LiabilityRemarks] NVARCHAR(500) NULL,
        [GovIDProofUploadPath] NVARCHAR(500) NULL,
        [FutureProspectus] NVARCHAR(MAX) NULL,
        [AdverseJudgmentUploadPath] NVARCHAR(MAX) NULL,
        [InjuryType] VARCHAR(50) NULL,
        [TreatedDocFlag] BIT NULL,
        [ClosureDate] DATE NULL,
        [MannerOfAccidentRO] NVARCHAR(MAX) NULL,
        [IsDeceasedInTR18] BIT NULL,
        [IsSTPassenger] BIT NULL,
        [IsMedicalExpensesPaid] BIT NULL,
        [MedicalPaidAmount] DECIMAL(18, 2) NULL,
        [MedicalPaidRemarks] NVARCHAR(MAX) NULL,
        [IsARFAmountPaid] BIT NULL,
        [ARFPaidAmount] DECIMAL(18, 2) NULL,
        [ARFPaidRemarks] NVARCHAR(MAX) NULL,
        [IsDelayApplicationFiled] BIT NULL,
        [DelayApplicationPath] NVARCHAR(MAX) NULL,
        [IsDelayCondonedAdverse] BIT NULL,
        [DelayCondonedOrderPath] NVARCHAR(MAX) NULL,
        [IsFIRFiledAgainstDriver] BIT NULL,
        [IsBusCameraInstalled] BIT NULL,
        [IsCameraFootageProduced] BIT NULL,
        [IsPhotographProduced] BIT NULL,
        [PhotographNotProducedReason] NVARCHAR(500) NULL,
        [PoliceSketchExhibitNo] NVARCHAR(100) NULL,
        [IsPoliceSketchEnclosed] BIT NULL,
        [IsEvidenceBasedOnSecurityReport] BIT NULL,
        [SecurityReportNoEvidenceReason] NVARCHAR(500) NULL,
        [IsImpleadingAppFiled] BIT NULL,
        [ImpleadingAppNotFiledReason] NVARCHAR(500) NULL,
        [IsVictimSalaried] BIT NULL,
        [IsIncomeCrossVerified] BIT NULL,
        [DoesIncomeTallyWithDocuments] BIT NULL,
        [IsAmountDepositedInEP] BIT NULL,
        [FutureProspectsPercentage] DECIMAL(18, 2) NULL,
        [PersonalExpensesDeduction] DECIMAL(18, 2) NULL,
        [Multiplier] DECIMAL(18, 2) NULL,
        [LossOfDependency] DECIMAL(18, 2) NULL,
        [LossOfConsortium] DECIMAL(18, 2) NULL,
        [LossOfEstate] DECIMAL(18, 2) NULL,
        [FuneralExpenses] DECIMAL(18, 2) NULL,
        [LossOfLoveAffection] DECIMAL(18, 2) NULL,
        [MedicalExpenseOther] DECIMAL(18, 2) NULL,
        [PainSufferings] DECIMAL(18, 2) NULL,
        [ConveyanceAttendant] DECIMAL(18, 2) NULL,
        [LossOfFutureIncome] DECIMAL(18, 2) NULL,
        [LossOfIncomeLaidUp] DECIMAL(18, 2) NULL,
        [LossOfAmenities] DECIMAL(18, 2) NULL,
        [FutureMedicalExpenses] DECIMAL(18, 2) NULL,
        [InjuryOtherExpense] DECIMAL(18, 2) NULL,
        [IsEPFiled] BIT NULL,
        [EPDepositedAmount] DECIMAL(18, 2) NULL,
        [IsMedicalInsuranceClaimed] BIT NOT NULL DEFAULT ((0)),
        [IncomePeriod] VARCHAR(20) NULL DEFAULT ('monthly'),
        [IsMedicalBillsVerified] BIT NULL,
        [CustomCompensation] NVARCHAR(MAX) NULL,
        [IsFIRFiled] BIT NULL,
        [IsChargeSheetFiled] BIT NULL,
        [PunishmentRemarks] NVARCHAR(500) NULL,
        [DisabilityPercentage] DECIMAL(18, 2) NULL,
        [EPNumber] NVARCHAR(100) NULL,
        [EPCourt] NVARCHAR(255) NULL,
        [EPStage] NVARCHAR(100) NULL,
        [EPNextHearingDate] DATE NULL,
        [AgeProofUploadPath] NVARCHAR(MAX) NULL,
        [RoundOffAmount] DECIMAL(18, 2) NULL,
        [AsPerECourts] NVARCHAR(MAX) NULL
    );
END;
GO

-- -----------------------------------------------------------------------------
-- Table: [MVC_CASE_ADVERSE_DOCS]
-- -----------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.tables t JOIN sys.schemas s ON t.schema_id = s.schema_id WHERE t.name = 'MVC_CASE_ADVERSE_DOCS' AND s.name = 'dbo')
BEGIN
    CREATE TABLE [dbo].[MVC_CASE_ADVERSE_DOCS] (
        [DocID] INT IDENTITY(1,1) NOT NULL,
        [CaseID] INT NOT NULL,
        [DocName] NVARCHAR(MAX) NOT NULL,
        [PageCount] INT NULL
    );
END;
GO

-- -----------------------------------------------------------------------------
-- Table: [MVC_CASE_ADVERSE_PW]
-- -----------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.tables t JOIN sys.schemas s ON t.schema_id = s.schema_id WHERE t.name = 'MVC_CASE_ADVERSE_PW' AND s.name = 'dbo')
BEGIN
    CREATE TABLE [dbo].[MVC_CASE_ADVERSE_PW] (
        [PWID] INT IDENTITY(1,1) NOT NULL,
        [CaseID] INT NULL,
        [PWName] NVARCHAR(200) NULL,
        [Type] NVARCHAR(20) NULL,
        [IsTreated] BIT NOT NULL DEFAULT ((0)),
        [Remark] NVARCHAR(MAX) NULL,
        [Designation] NVARCHAR(500) NULL,
        [PWDesignation] NVARCHAR(100) NULL,
        [PWRemark] NVARCHAR(MAX) NULL
    );
END;
GO

-- -----------------------------------------------------------------------------
-- Table: [MVC_CASE_ADVERSE_RW]
-- -----------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.tables t JOIN sys.schemas s ON t.schema_id = s.schema_id WHERE t.name = 'MVC_CASE_ADVERSE_RW' AND s.name = 'dbo')
BEGIN
    CREATE TABLE [dbo].[MVC_CASE_ADVERSE_RW] (
        [RWID] INT IDENTITY(1,1) NOT NULL,
        [CaseID] INT NULL,
        [RWName] NVARCHAR(200) NULL
    );
END;
GO

-- -----------------------------------------------------------------------------
-- Table: [MVC_CASE_CONNECTED]
-- -----------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.tables t JOIN sys.schemas s ON t.schema_id = s.schema_id WHERE t.name = 'MVC_CASE_CONNECTED' AND s.name = 'dbo')
BEGIN
    CREATE TABLE [dbo].[MVC_CASE_CONNECTED] (
        [ConnectedID] INT IDENTITY(1,1) NOT NULL,
        [CaseID] INT NULL,
        [ConnectedMVCNo] NVARCHAR(50) NULL,
        [ConnectedYear] INT NULL,
        [Remarks] NVARCHAR(MAX) NULL,
        [MACT] NVARCHAR(100) NULL,
        [IsDoubleClaim] BIT NOT NULL DEFAULT ((0))
    );
END;
GO

-- -----------------------------------------------------------------------------
-- Table: [MVC_CASE_OPPOSITE_VEHICLES]
-- -----------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.tables t JOIN sys.schemas s ON t.schema_id = s.schema_id WHERE t.name = 'MVC_CASE_OPPOSITE_VEHICLES' AND s.name = 'dbo')
BEGIN
    CREATE TABLE [dbo].[MVC_CASE_OPPOSITE_VEHICLES] (
        [OppositeVehicleID] INT IDENTITY(1,1) NOT NULL,
        [CaseID] INT NOT NULL,
        [VehicleNo] NVARCHAR(50) NOT NULL
    );
END;
GO

-- -----------------------------------------------------------------------------
-- Table: [MVC_CASE_PAYMENTS]
-- -----------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.tables t JOIN sys.schemas s ON t.schema_id = s.schema_id WHERE t.name = 'MVC_CASE_PAYMENTS' AND s.name = 'dbo')
BEGIN
    CREATE TABLE [dbo].[MVC_CASE_PAYMENTS] (
        [PaymentID] INT IDENTITY(1,1) NOT NULL,
        [PaymentID] INT IDENTITY(1,1) NOT NULL,
        [CaseID] INT NOT NULL,
        [CaseID] INT NOT NULL,
        [Amount] DECIMAL(18, 2) NOT NULL,
        [Amount] DECIMAL(18, 2) NOT NULL,
        [ChequeNumber] NVARCHAR(100) NULL,
        [ChequeNumber] NVARCHAR(100) NULL,
        [ChequeDate] DATE NULL,
        [ChequeDate] DATE NULL,
        [Remarks] NVARCHAR(500) NULL,
        [PaymentType] NVARCHAR(50) NULL,
        [Remarks] NVARCHAR(500) NULL,
        [CreatedDate] DATETIME NULL DEFAULT (getdate()),
        [PaymentType] NVARCHAR(50) NULL,
        [CreatedDate] DATETIME NOT NULL DEFAULT (getdate()),
        [CreatedBy] NVARCHAR(100) NULL,
        [CreatedBy] NVARCHAR(100) NULL
    );
END;
GO

-- -----------------------------------------------------------------------------
-- Table: [MVC_CASE_PAYMENTS]
-- -----------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.tables t JOIN sys.schemas s ON t.schema_id = s.schema_id WHERE t.name = 'MVC_CASE_PAYMENTS' AND s.name = 'dbo')
BEGIN
    CREATE TABLE [dbo].[MVC_CASE_PAYMENTS] (
        [PaymentID] INT IDENTITY(1,1) NOT NULL,
        [PaymentID] INT IDENTITY(1,1) NOT NULL,
        [CaseID] INT NOT NULL,
        [CaseID] INT NOT NULL,
        [Amount] DECIMAL(18, 2) NOT NULL,
        [Amount] DECIMAL(18, 2) NOT NULL,
        [ChequeNumber] NVARCHAR(100) NULL,
        [ChequeNumber] NVARCHAR(100) NULL,
        [ChequeDate] DATE NULL,
        [ChequeDate] DATE NULL,
        [Remarks] NVARCHAR(500) NULL,
        [PaymentType] NVARCHAR(50) NULL,
        [Remarks] NVARCHAR(500) NULL,
        [CreatedDate] DATETIME NULL DEFAULT (getdate()),
        [PaymentType] NVARCHAR(50) NULL,
        [CreatedDate] DATETIME NOT NULL DEFAULT (getdate()),
        [CreatedBy] NVARCHAR(100) NULL,
        [CreatedBy] NVARCHAR(100) NULL
    );
END;
GO

-- -----------------------------------------------------------------------------
-- Table: [MVC_CASE_PETITIONERS]
-- -----------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.tables t JOIN sys.schemas s ON t.schema_id = s.schema_id WHERE t.name = 'MVC_CASE_PETITIONERS' AND s.name = 'dbo')
BEGIN
    CREATE TABLE [dbo].[MVC_CASE_PETITIONERS] (
        [PetitionerID] INT IDENTITY(1,1) NOT NULL,
        [CaseID] INT NULL,
        [PetitionerName] NVARCHAR(200) NOT NULL,
        [Relationship] NVARCHAR(100) NULL
    );
END;
GO

-- -----------------------------------------------------------------------------
-- Table: [MVC_CASE_RESPONDENTS]
-- -----------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.tables t JOIN sys.schemas s ON t.schema_id = s.schema_id WHERE t.name = 'MVC_CASE_RESPONDENTS' AND s.name = 'dbo')
BEGIN
    CREATE TABLE [dbo].[MVC_CASE_RESPONDENTS] (
        [RespondentID] INT IDENTITY(1,1) NOT NULL,
        [CaseID] INT NOT NULL,
        [RespondentName] NVARCHAR(500) NOT NULL,
        [Remarks] NVARCHAR(MAX) NULL,
        [CreatedAt] DATETIME NULL DEFAULT (getdate())
    );
END;
GO

-- -----------------------------------------------------------------------------
-- Table: [MVC_CASES]
-- -----------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.tables t JOIN sys.schemas s ON t.schema_id = s.schema_id WHERE t.name = 'MVC_CASES' AND s.name = 'dbo')
BEGIN
    CREATE TABLE [dbo].[MVC_CASES] (
        [CaseID] INT IDENTITY(1,1) NOT NULL,
        [DivisionID] INT NOT NULL,
        [MVCNo] NVARCHAR(50) NOT NULL,
        [MVCYear] INT NOT NULL,
        [MACTID] INT NOT NULL,
        [VehicleNo] NVARCHAR(20) NULL,
        [AccidentDate] DATE NULL,
        [CaseType] NVARCHAR(50) NULL DEFAULT ('Pending'),
        [StatusID] INT NULL DEFAULT ((1)),
        [ClaimType] NVARCHAR(50) NULL,
        [ThirdPartyFlag] BIT NULL DEFAULT ((0)),
        [ClaimAmount] DECIMAL(18, 2) NULL,
        [AdvocateID] INT NULL,
        [EntrustmentNo] NVARCHAR(50) NULL,
        [EntrustmentDate] DATE NULL,
        [DoubleClaimFlag] BIT NULL DEFAULT ((0)),
        [CurrentStage] NVARCHAR(100) NULL,
        [DisposalStatus] NVARCHAR(50) NULL,
        [DisposalResult] NVARCHAR(50) NULL,
        [DisposalRemarks] NVARCHAR(MAX) NULL,
        [CreatedAt] DATETIME NULL DEFAULT (getdate()),
        [UpdatedAt] DATETIME NULL DEFAULT (getdate()),
        [NextHearingDate] DATE NULL,
        [IsDocumentSent] BIT NULL DEFAULT ((0)),
        [DocumentOutwardNo] NVARCHAR(50) NULL,
        [DocumentOutwardDate] DATE NULL,
        [IsObjectionFiled] BIT NULL DEFAULT ((0)),
        [ObjectionFiledDate] DATE NULL,
        [IsEvidenceFiled] BIT NULL DEFAULT ((0)),
        [IsEPFiled] BIT NOT NULL DEFAULT ((0)),
        [EPNumber] NVARCHAR(50) NULL,
        [EPStage] NVARCHAR(100) NULL,
        [AdvocateName] NVARCHAR(255) NULL,
        [EPNextHearingDate] DATE NULL,
        [VehicleType] NVARCHAR(20) NULL,
        [ObjectionOutwardNo] NVARCHAR(50) NULL,
        [ClosureDate] DATETIME NULL,
        [IsWithinLimitation] BIT NULL,
        [LimitationRemark] NVARCHAR(MAX) NULL,
        [IsDelayCondoned] BIT NULL,
        [DelayRemark] NVARCHAR(MAX) NULL,
        [TransferredFromDivisionID] INT NULL,
        [TransferDate] DATETIME NULL,
        [IsTransferViewed] BIT NOT NULL DEFAULT ((0)),
        [PetitionFiledFor] NVARCHAR(100) NULL,
        [IsSTPassenger] BIT NOT NULL DEFAULT ((0)),
        [IsMedicalExpensesPaid] BIT NOT NULL DEFAULT ((0)),
        [MedicalPaidAmount] DECIMAL(18, 2) NULL,
        [MedicalPaidRemarks] NVARCHAR(500) NULL,
        [IsARFAmountPaid] BIT NOT NULL DEFAULT ((0)),
        [ARFPaidAmount] DECIMAL(18, 2) NULL,
        [ARFPaidRemarks] NVARCHAR(500) NULL,
        [IsOppositeVehicleInmate] BIT NOT NULL DEFAULT ((0)),
        [ClaimRemark] NVARCHAR(MAX) NULL,
        [ClaimPetitionDate] DATE NULL,
        [HasInterimOrder] BIT NOT NULL DEFAULT ((0)),
        [InterimOrderFilePath] NVARCHAR(MAX) NULL,
        [CNRNumber] NVARCHAR(50) NULL,
        [EstCode] NVARCHAR(50) NULL,
        [CaseTypeCode] NVARCHAR(50) NULL,
        [CaseStatus] NVARCHAR(100) NULL,
        [CourtHall] NVARCHAR(150) NULL,
        [ModifiedDate] DATETIME NULL,
        [ModifiedBy] INT NULL,
        [FavorJudgmentPath] NVARCHAR(500) NULL,
        [LastNapixSyncAt] DATETIME2 NULL,
        [LastNapixSyncStatus] NVARCHAR(30) NULL,
        [LastNapixSyncError] NVARCHAR(500) NULL,
        [NapixSyncAttemptCount] INT NOT NULL DEFAULT ((0)),
        [NapixDataHash] NVARCHAR(64) NULL,
        [PendDispStatus] NVARCHAR(20) NULL,
        [EstName] NVARCHAR(250) NULL,
        [ECourtsStage] NVARCHAR(150) NULL,
        [ECourtsCourtNo] NVARCHAR(50) NULL,
        [ECourtsJudge] NVARCHAR(250) NULL
    );
END;
GO

-- -----------------------------------------------------------------------------
-- Table: [MVC_DIVISION_ADVOCATES]
-- -----------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.tables t JOIN sys.schemas s ON t.schema_id = s.schema_id WHERE t.name = 'MVC_DIVISION_ADVOCATES' AND s.name = 'dbo')
BEGIN
    CREATE TABLE [dbo].[MVC_DIVISION_ADVOCATES] (
        [Id] INT IDENTITY(1,1) NOT NULL,
        [DivisionName] NVARCHAR(100) NULL,
        [AdvocateName] NVARCHAR(255) NULL
    );
END;
GO

-- -----------------------------------------------------------------------------
-- Table: [MVC_EP_DETAILS]
-- -----------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.tables t JOIN sys.schemas s ON t.schema_id = s.schema_id WHERE t.name = 'MVC_EP_DETAILS' AND s.name = 'dbo')
BEGIN
    CREATE TABLE [dbo].[MVC_EP_DETAILS] (
        [EPID] INT IDENTITY(1,1) NOT NULL,
        [CaseID] INT NOT NULL,
        [EPNumber] NVARCHAR(100) NOT NULL,
        [EPCourt] NVARCHAR(200) NULL,
        [ArisingFromMVCNo] NVARCHAR(50) NULL,
        [ArisingFromMVCYear] INT NULL,
        [ArisingFromMACT] NVARCHAR(200) NULL,
        [OriginalMVCStatus] NVARCHAR(100) NULL,
        [EPStatus] NVARCHAR(100) NULL,
        [IsSentToAccounts] BIT NULL DEFAULT ((0)),
        [DateSentToAccounts] DATE NULL,
        [PettyBillDate] DATE NULL,
        [PettyBillAmount] DECIMAL(18, 2) NULL,
        [ChequeNumber] NVARCHAR(50) NULL,
        [ChequeDate] DATE NULL,
        [AmountPaid] DECIMAL(18, 2) NULL,
        [ComplianceStatus] NVARCHAR(50) NULL,
        [DateOfCompliance] DATE NULL,
        [Remarks] NVARCHAR(MAX) NULL,
        [CreatedBy] NVARCHAR(50) NULL,
        [CreatedDate] DATETIME NULL DEFAULT (getdate()),
        [ModifiedBy] NVARCHAR(50) NULL,
        [ModifiedDate] DATETIME NULL,
        [DivisionID] INT NULL,
        [AsPerECourts] NVARCHAR(500) NULL,
        [PetitionDate] DATETIME NULL,
        [AwardAmount] DECIMAL(18, 2) NULL,
        [InterestRate] DECIMAL(18, 2) NULL,
        [LiabilityPercentage] DECIMAL(18, 2) NULL,
        [NextHearingDate] DATE NULL,
        [VehicleNo] NVARCHAR(50) NULL,
        [AccidentDate] DATE NULL,
        [EPYear] INT NULL,
        [EP_EntrustmentNo] NVARCHAR(100) NULL,
        [EP_EntrustmentDate] DATE NULL,
        [AdvocateID] INT NULL,
        [AdvocateName] NVARCHAR(200) NULL,
        [CaseOutcome] NVARCHAR(50) NULL,
        [DisposalDate] DATE NULL,
        [ClosureDate] DATE NULL,
        [DisposalRemarks] NVARCHAR(MAX) NULL,
        [CNRNumber] NVARCHAR(16) NULL,
        [EstCode] NVARCHAR(50) NULL,
        [CaseTypeCode] NVARCHAR(50) NULL,
        [RealizationDate] DATE NULL,
        [CalculationMethod] NVARCHAR(50) NULL
    );
END;
GO

-- -----------------------------------------------------------------------------
-- Table: [MVC_EP_PAYMENTS]
-- -----------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.tables t JOIN sys.schemas s ON t.schema_id = s.schema_id WHERE t.name = 'MVC_EP_PAYMENTS' AND s.name = 'dbo')
BEGIN
    CREATE TABLE [dbo].[MVC_EP_PAYMENTS] (
        [PaymentID] INT IDENTITY(1,1) NOT NULL,
        [EPID] INT NOT NULL,
        [Amount] DECIMAL(18, 2) NOT NULL,
        [PaymentDate] DATETIME NOT NULL,
        [ChequeNumber] NVARCHAR(100) NULL,
        [ChequeDate] DATETIME NULL,
        [Remarks] NVARCHAR(MAX) NULL,
        [CreatedAt] DATETIME NULL DEFAULT (getdate())
    );
END;
GO

-- -----------------------------------------------------------------------------
-- Table: [MVC_REMIND_BACK_ADVERSE_CONNECTED]
-- -----------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.tables t JOIN sys.schemas s ON t.schema_id = s.schema_id WHERE t.name = 'MVC_REMIND_BACK_ADVERSE_CONNECTED' AND s.name = 'dbo')
BEGIN
    CREATE TABLE [dbo].[MVC_REMIND_BACK_ADVERSE_CONNECTED] (
        [ID] INT IDENTITY(1,1) NOT NULL,
        [RemindBackID] INT NULL,
        [CaseDetails] NVARCHAR(200) NULL,
        [Status] NVARCHAR(50) NULL,
        [CurrentStage] NVARCHAR(100) NULL,
        [AwardAmount] DECIMAL(18, 2) NULL,
        [InterestRate] DECIMAL(18, 2) NULL,
        [VictimAge] INT NULL,
        [Occupation] NVARCHAR(100) NULL,
        [IncomeConsidered] DECIMAL(18, 2) NULL,
        [InjuryDetails] NVARCHAR(1000) NULL
    );
END;
GO

-- -----------------------------------------------------------------------------
-- Table: [MVC_REMIND_BACK_ADVERSE_DETAILS]
-- -----------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.tables t JOIN sys.schemas s ON t.schema_id = s.schema_id WHERE t.name = 'MVC_REMIND_BACK_ADVERSE_DETAILS' AND s.name = 'dbo')
BEGIN
    CREATE TABLE [dbo].[MVC_REMIND_BACK_ADVERSE_DETAILS] (
        [AdverseID] INT IDENTITY(1,1) NOT NULL,
        [RemindBackID] INT NULL,
        [BusInsuranceDetails] NVARCHAR(500) NULL,
        [IsBusInsured] BIT NULL,
        [ClaimPetitionDate] DATETIME NULL,
        [AwardDate] DATETIME NULL,
        [MannerOfAccident] NVARCHAR(MAX) NULL,
        [ObjectionFiled] BIT NULL,
        [ObjectionRemarks] NVARCHAR(500) NULL,
        [RWType] NVARCHAR(50) NULL,
        [TR18Remarks] NVARCHAR(1000) NULL,
        [TR18UploadPath] NVARCHAR(500) NULL,
        [IsAllegedAccident] BIT NULL,
        [AdverseDoubleClaimFlag] BIT NULL,
        [DoubleClaimDetails] NVARCHAR(500) NULL,
        [SecurityRequired] BIT NULL,
        [SecurityUploadPath] NVARCHAR(500) NULL,
        [GovIDProofUploadPath] NVARCHAR(500) NULL,
        [DisposedOnDate] DATETIME NULL,
        [CopyAppliedDate] DATETIME NULL,
        [CertifiedCopyRemarks] NVARCHAR(500) NULL,
        [CopyIssuedDate] DATETIME NULL,
        [CopyReceivedDate] DATETIME NULL,
        [CopyDeliveredDate] DATETIME NULL,
        [AwardAmount] DECIMAL(18, 2) NULL,
        [InterestRate] DECIMAL(18, 2) NULL,
        [VictimAge] INT NULL,
        [Occupation] NVARCHAR(100) NULL,
        [IncomeConsidered] DECIMAL(18, 2) NULL,
        [InjuryDetails] NVARCHAR(MAX) NULL,
        [DriverPunishmentStatus] NVARCHAR(200) NULL,
        [PunishmentOrderUploadPath] NVARCHAR(500) NULL,
        [InterimCompAmount] DECIMAL(18, 2) NULL,
        [InterimCompDeducted] BIT NULL,
        [AdvocateOpinion] NVARCHAR(1000) NULL,
        [LOOpinion] NVARCHAR(1000) NULL,
        [DCOpinion] NVARCHAR(1000) NULL,
        [ForwardingStatus] NVARCHAR(100) NULL,
        [ClosureRemarks] NVARCHAR(1000) NULL,
        [ClosureDate] DATETIME NULL,
        [OutwardNumber] NVARCHAR(50) NULL,
        [OutwardDate] DATETIME NULL,
        [IsCorpLiable] BIT NULL,
        [LiabilityPercentage] DECIMAL(18, 2) NULL,
        [LiabilityRemarks] NVARCHAR(500) NULL,
        [AdverseJudgmentUploadPath] NVARCHAR(500) NULL,
        [FutureProspectus] NVARCHAR(1000) NULL,
        [InjuryType] NVARCHAR(50) NULL,
        [TreatedDocFlag] BIT NULL,
        [IsEPFiled] BIT NULL,
        [EPDepositedAmount] DECIMAL(18, 2) NULL,
        [IsMedicalInsuranceClaimed] BIT NOT NULL DEFAULT ((0)),
        [IncomePeriod] VARCHAR(20) NULL DEFAULT ('monthly'),
        [IsMedicalBillsVerified] BIT NULL,
        [CustomCompensation] NVARCHAR(MAX) NULL,
        [IsFIRFiled] BIT NULL,
        [IsChargeSheetFiled] BIT NULL,
        [PunishmentRemarks] NVARCHAR(500) NULL,
        [IsBusCameraInstalled] BIT NULL,
        [IsCameraFootageProduced] BIT NULL,
        [IsPhotographProduced] BIT NULL,
        [PhotographNotProducedReason] NVARCHAR(MAX) NULL,
        [PoliceSketchExhibitNo] NVARCHAR(100) NULL,
        [IsPoliceSketchEnclosed] BIT NULL,
        [IsEvidenceBasedOnSecurityReport] BIT NULL,
        [SecurityReportNoEvidenceReason] NVARCHAR(MAX) NULL,
        [IsImpleadingAppFiled] BIT NULL,
        [ImpleadingAppNotFiledReason] NVARCHAR(MAX) NULL,
        [IsVictimSalaried] BIT NULL,
        [IsIncomeCrossVerified] BIT NULL,
        [DoesIncomeTallyWithDocuments] BIT NULL,
        [IsAmountDepositedInEP] BIT NOT NULL DEFAULT ((0)),
        [FutureProspectsPercentage] DECIMAL(18, 2) NULL,
        [PersonalExpensesDeduction] DECIMAL(18, 2) NULL,
        [Multiplier] DECIMAL(18, 2) NULL,
        [LossOfDependency] DECIMAL(18, 2) NULL,
        [LossOfConsortium] DECIMAL(18, 2) NULL,
        [LossOfEstate] DECIMAL(18, 2) NULL,
        [FuneralExpenses] DECIMAL(18, 2) NULL,
        [LossOfLoveAffection] DECIMAL(18, 2) NULL,
        [MedicalExpenseOther] DECIMAL(18, 2) NULL,
        [PainSufferings] DECIMAL(18, 2) NULL,
        [ConveyanceAttendant] DECIMAL(18, 2) NULL,
        [LossOfFutureIncome] DECIMAL(18, 2) NULL,
        [LossOfIncomeLaidUp] DECIMAL(18, 2) NULL,
        [LossOfAmenities] DECIMAL(18, 2) NULL,
        [FutureMedicalExpenses] DECIMAL(18, 2) NULL,
        [InjuryOtherExpense] DECIMAL(18, 2) NULL,
        [DisabilityPercentage] DECIMAL(18, 2) NULL,
        [EPNumber] NVARCHAR(100) NULL,
        [EPCourt] NVARCHAR(255) NULL,
        [EPStage] NVARCHAR(100) NULL,
        [EPNextHearingDate] DATE NULL,
        [AgeProofUploadPath] NVARCHAR(MAX) NULL,
        [IsSTPassenger] BIT NULL,
        [IsMedicalExpensesPaid] BIT NULL,
        [MedicalPaidAmount] DECIMAL(18, 2) NULL,
        [MedicalPaidRemarks] NVARCHAR(MAX) NULL,
        [IsARFAmountPaid] BIT NULL,
        [ARFPaidAmount] DECIMAL(18, 2) NULL,
        [ARFPaidRemarks] NVARCHAR(MAX) NULL,
        [IsDeceasedInTR18] BIT NOT NULL DEFAULT ((0)),
        [MannerOfAccidentRO] NVARCHAR(MAX) NULL,
        [RoundOffAmount] DECIMAL(18, 2) NULL,
        [AsPerECourts] NVARCHAR(MAX) NULL,
        [IsDelayApplicationFiled] BIT NOT NULL DEFAULT ((0)),
        [DelayApplicationPath] NVARCHAR(MAX) NULL,
        [IsDelayCondonedAdverse] BIT NULL,
        [DelayCondonedOrderPath] NVARCHAR(MAX) NULL
    );
END;
GO

-- -----------------------------------------------------------------------------
-- Table: [MVC_REMIND_BACK_ADVERSE_PW]
-- -----------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.tables t JOIN sys.schemas s ON t.schema_id = s.schema_id WHERE t.name = 'MVC_REMIND_BACK_ADVERSE_PW' AND s.name = 'dbo')
BEGIN
    CREATE TABLE [dbo].[MVC_REMIND_BACK_ADVERSE_PW] (
        [PWID] INT IDENTITY(1,1) NOT NULL,
        [RemindBackID] INT NULL,
        [PWName] NVARCHAR(200) NULL,
        [Type] NVARCHAR(50) NULL,
        [IsTreated] BIT NOT NULL DEFAULT ((0)),
        [Designation] NVARCHAR(100) NULL,
        [Remark] NVARCHAR(MAX) NULL
    );
END;
GO

-- -----------------------------------------------------------------------------
-- Table: [MVC_REMIND_BACK_ADVERSE_RW]
-- -----------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.tables t JOIN sys.schemas s ON t.schema_id = s.schema_id WHERE t.name = 'MVC_REMIND_BACK_ADVERSE_RW' AND s.name = 'dbo')
BEGIN
    CREATE TABLE [dbo].[MVC_REMIND_BACK_ADVERSE_RW] (
        [RWID] INT IDENTITY(1,1) NOT NULL,
        [RemindBackID] INT NULL,
        [RWName] NVARCHAR(200) NULL,
        [Designation] NVARCHAR(100) NULL
    );
END;
GO

-- -----------------------------------------------------------------------------
-- Table: [MVC_REMIND_BACK_CASES]
-- -----------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.tables t JOIN sys.schemas s ON t.schema_id = s.schema_id WHERE t.name = 'MVC_REMIND_BACK_CASES' AND s.name = 'dbo')
BEGIN
    CREATE TABLE [dbo].[MVC_REMIND_BACK_CASES] (
        [RemindBackID] INT IDENTITY(1,1) NOT NULL,
        [OriginalCaseID] INT NULL,
        [DivisionID] INT NULL,
        [MVCNo] NVARCHAR(50) NULL,
        [MVCYear] INT NULL,
        [MACTID] INT NULL,
        [VehicleNo] NVARCHAR(20) NULL,
        [VehicleType] NVARCHAR(20) NULL,
        [AccidentDate] DATETIME NULL,
        [CaseType] NVARCHAR(50) NULL,
        [StatusID] INT NULL,
        [ClaimType] NVARCHAR(50) NULL,
        [ThirdPartyFlag] BIT NULL,
        [ClaimAmount] DECIMAL(18, 2) NULL,
        [AdvocateID] INT NULL,
        [AdvocateName] NVARCHAR(200) NULL,
        [EntrustmentNo] NVARCHAR(50) NULL,
        [EntrustmentDate] DATETIME NULL,
        [DoubleClaimFlag] BIT NULL,
        [NextHearingDate] DATETIME NULL,
        [CurrentStage] NVARCHAR(100) NULL,
        [DisposalStatus] NVARCHAR(50) NULL,
        [DisposalResult] NVARCHAR(50) NULL,
        [DisposalRemarks] NVARCHAR(500) NULL,
        [IsDocumentSent] BIT NULL,
        [DocumentOutwardNo] NVARCHAR(50) NULL,
        [DocumentOutwardDate] DATETIME NULL,
        [IsObjectionFiled] BIT NULL,
        [ObjectionFiledDate] DATETIME NULL,
        [ObjectionOutwardNo] NVARCHAR(50) NULL,
        [IsEvidenceFiled] BIT NULL,
        [ClosureDate] DATETIME NULL,
        [CreatedAt] DATETIME NULL DEFAULT (getdate()),
        [CreatedBy] NVARCHAR(100) NULL,
        [CNRNumber] NVARCHAR(16) NULL,
        [EstCode] NVARCHAR(50) NULL,
        [CaseTypeCode] NVARCHAR(50) NULL
    );
END;
GO

-- -----------------------------------------------------------------------------
-- Table: [MVC_REMIND_BACK_CONNECTED]
-- -----------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.tables t JOIN sys.schemas s ON t.schema_id = s.schema_id WHERE t.name = 'MVC_REMIND_BACK_CONNECTED' AND s.name = 'dbo')
BEGIN
    CREATE TABLE [dbo].[MVC_REMIND_BACK_CONNECTED] (
        [ConnectedID] INT IDENTITY(1,1) NOT NULL,
        [RemindBackID] INT NULL,
        [ConnectedMVCNo] NVARCHAR(50) NULL,
        [ConnectedYear] INT NULL,
        [Remarks] NVARCHAR(500) NULL,
        [MACT] NVARCHAR(100) NULL,
        [IsDoubleClaim] BIT NULL
    );
END;
GO

-- -----------------------------------------------------------------------------
-- Table: [MVC_REMIND_BACK_PETITIONERS]
-- -----------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.tables t JOIN sys.schemas s ON t.schema_id = s.schema_id WHERE t.name = 'MVC_REMIND_BACK_PETITIONERS' AND s.name = 'dbo')
BEGIN
    CREATE TABLE [dbo].[MVC_REMIND_BACK_PETITIONERS] (
        [PetitionerID] INT IDENTITY(1,1) NOT NULL,
        [RemindBackID] INT NULL,
        [PetitionerName] NVARCHAR(200) NULL,
        [Relationship] NVARCHAR(50) NULL
    );
END;
GO

-- -----------------------------------------------------------------------------
-- Table: [MVC_REMIND_BACK_RESPONDENTS]
-- -----------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.tables t JOIN sys.schemas s ON t.schema_id = s.schema_id WHERE t.name = 'MVC_REMIND_BACK_RESPONDENTS' AND s.name = 'dbo')
BEGIN
    CREATE TABLE [dbo].[MVC_REMIND_BACK_RESPONDENTS] (
        [RespondentID] INT IDENTITY(1,1) NOT NULL,
        [RemindBackID] INT NOT NULL,
        [RespondentName] NVARCHAR(255) NOT NULL,
        [Remarks] NVARCHAR(MAX) NULL
    );
END;
GO

-- -----------------------------------------------------------------------------
-- Table: [NAPIX_API_CALLS]
-- -----------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.tables t JOIN sys.schemas s ON t.schema_id = s.schema_id WHERE t.name = 'NAPIX_API_CALLS' AND s.name = 'dbo')
BEGIN
    CREATE TABLE [dbo].[NAPIX_API_CALLS] (
        [CallID] BIGINT IDENTITY(1,1) NOT NULL,
        [CalledAt] DATETIME2 NOT NULL DEFAULT (sysutcdatetime()),
        [Endpoint] NVARCHAR(120) NOT NULL,
        [HttpStatus] INT NULL,
        [IsSuccess] BIT NOT NULL DEFAULT ((0)),
        [Username] NVARCHAR(100) NULL,
        [CNRNumber] NVARCHAR(30) NULL,
        [DurationMs] INT NULL,
        [Module] NVARCHAR(20) NOT NULL DEFAULT ('MVC')
    );
END;
GO

-- -----------------------------------------------------------------------------
-- Table: [NAPIX_SYNC_QUEUE]
-- -----------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.tables t JOIN sys.schemas s ON t.schema_id = s.schema_id WHERE t.name = 'NAPIX_SYNC_QUEUE' AND s.name = 'dbo')
BEGIN
    CREATE TABLE [dbo].[NAPIX_SYNC_QUEUE] (
        [QueueId] BIGINT IDENTITY(1,1) NOT NULL,
        [Module] NVARCHAR(20) NOT NULL,
        [CaseId] INT NOT NULL,
        [CNRNumber] NVARCHAR(30) NOT NULL,
        [Priority] INT NOT NULL DEFAULT ((3)),
        [Status] NVARCHAR(30) NOT NULL DEFAULT ('Queued'),
        [RequestedAt] DATETIME2 NOT NULL DEFAULT (sysutcdatetime()),
        [LockedUntil] DATETIME2 NULL,
        [NextAttemptAt] DATETIME2 NULL,
        [AttemptCount] INT NOT NULL DEFAULT ((0)),
        [LastError] NVARCHAR(500) NULL,
        [ProcessedAt] DATETIME2 NULL
    );
END;
GO

-- -----------------------------------------------------------------------------
-- Table: [OTHER_CASE_DOCUMENTS]
-- -----------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.tables t JOIN sys.schemas s ON t.schema_id = s.schema_id WHERE t.name = 'OTHER_CASE_DOCUMENTS' AND s.name = 'dbo')
BEGIN
    CREATE TABLE [dbo].[OTHER_CASE_DOCUMENTS] (
        [Id] INT IDENTITY(1,1) NOT NULL,
        [CaseID] INT NOT NULL,
        [DocName] NVARCHAR(500) NULL,
        [PageCount] INT NULL
    );
END;
GO

-- -----------------------------------------------------------------------------
-- Table: [OTHER_CASE_EVIDENCE_CORP]
-- -----------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.tables t JOIN sys.schemas s ON t.schema_id = s.schema_id WHERE t.name = 'OTHER_CASE_EVIDENCE_CORP' AND s.name = 'dbo')
BEGIN
    CREATE TABLE [dbo].[OTHER_CASE_EVIDENCE_CORP] (
        [EvidenceID] INT IDENTITY(1,1) NOT NULL,
        [CaseID] INT NOT NULL,
        [Name] NVARCHAR(500) NOT NULL,
        [Designation] NVARCHAR(500) NULL
    );
END;
GO

-- -----------------------------------------------------------------------------
-- Table: [OTHER_CASE_EVIDENCE_RESPONDENT]
-- -----------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.tables t JOIN sys.schemas s ON t.schema_id = s.schema_id WHERE t.name = 'OTHER_CASE_EVIDENCE_RESPONDENT' AND s.name = 'dbo')
BEGIN
    CREATE TABLE [dbo].[OTHER_CASE_EVIDENCE_RESPONDENT] (
        [EvidenceID] INT IDENTITY(1,1) NOT NULL,
        [CaseID] INT NOT NULL,
        [Name] NVARCHAR(500) NOT NULL,
        [Remark] NVARCHAR(MAX) NULL
    );
END;
GO

-- -----------------------------------------------------------------------------
-- Table: [OTHER_CASE_PETITIONERS]
-- -----------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.tables t JOIN sys.schemas s ON t.schema_id = s.schema_id WHERE t.name = 'OTHER_CASE_PETITIONERS' AND s.name = 'dbo')
BEGIN
    CREATE TABLE [dbo].[OTHER_CASE_PETITIONERS] (
        [PetitionerID] INT IDENTITY(1,1) NOT NULL,
        [CaseID] INT NOT NULL,
        [PetitionerName] NVARCHAR(250) NULL,
        [PetitionerRemark] NVARCHAR(MAX) NULL
    );
END;
GO

-- -----------------------------------------------------------------------------
-- Table: [OTHER_CASE_RESPONDENTS]
-- -----------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.tables t JOIN sys.schemas s ON t.schema_id = s.schema_id WHERE t.name = 'OTHER_CASE_RESPONDENTS' AND s.name = 'dbo')
BEGIN
    CREATE TABLE [dbo].[OTHER_CASE_RESPONDENTS] (
        [RespondentID] INT IDENTITY(1,1) NOT NULL,
        [CaseID] INT NOT NULL,
        [RespondentName] NVARCHAR(500) NOT NULL,
        [RespondentRemark] NVARCHAR(MAX) NULL
    );
END;
GO

-- -----------------------------------------------------------------------------
-- Table: [OTHER_CASES]
-- -----------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.tables t JOIN sys.schemas s ON t.schema_id = s.schema_id WHERE t.name = 'OTHER_CASES' AND s.name = 'dbo')
BEGIN
    CREATE TABLE [dbo].[OTHER_CASES] (
        [CaseID] INT IDENTITY(1,1) NOT NULL,
        [DivisionID] INT NOT NULL,
        [CaseType] NVARCHAR(50) NOT NULL,
        [LitigantType] NVARCHAR(50) NOT NULL,
        [IsPendingForFiling] BIT NULL DEFAULT ((0)),
        [CaseNumber] NVARCHAR(100) NULL,
        [CaseYear] INT NULL,
        [Court] NVARCHAR(200) NULL,
        [CaseNature] NVARCHAR(MAX) NULL,
        [EntrustmentNumber] NVARCHAR(100) NULL,
        [EntrustmentDate] DATE NULL,
        [AdvocateName] NVARCHAR(200) NULL,
        [EvidenceFiled] NVARCHAR(50) NULL,
        [CaseStatus] NVARCHAR(100) NULL,
        [NextDateOfHearing] DATE NULL,
        [InterimOrder] NVARCHAR(50) NULL,
        [InterimOrderFilePath] NVARCHAR(MAX) NULL,
        [AwardDetails] NVARCHAR(MAX) NULL,
        [Result] NVARCHAR(100) NULL,
        [ClosureDate] DATE NULL,
        [ClosureRemark] NVARCHAR(MAX) NULL,
        [OutwardNumber] NVARCHAR(100) NULL,
        [OutwardDate] DATE NULL,
        [CreatedBy] INT NULL,
        [CreatedDate] DATETIME NULL DEFAULT (getdate()),
        [ModifiedBy] INT NULL,
        [ModifiedDate] DATETIME NULL,
        [ClaimDetails] NVARCHAR(MAX) NULL,
        [RespondentName] NVARCHAR(500) NULL,
        [Remark] NVARCHAR(MAX) NULL,
        [CopyDisposedOn] DATE NULL,
        [CopyAppliedOn] DATE NULL,
        [CopyReadyOn] DATE NULL,
        [CopyDeliveredOn] DATE NULL,
        [CopyReceivedAtDivision] DATE NULL,
        [CertifiedCopyRemarks] NVARCHAR(MAX) NULL,
        [DisposalStatus] NVARCHAR(100) NULL,
        [AdvocateOpinion] NVARCHAR(MAX) NULL,
        [LawOfficerOpinion] NVARCHAR(MAX) NULL,
        [DCFinalDecision] NVARCHAR(MAX) NULL,
        [FinalForwardingStatus] NVARCHAR(100) NULL,
        [FinalJudgmentFilePath] NVARCHAR(MAX) NULL,
        [ClosureNumber] NVARCHAR(50) NULL,
        [PetitionerName] NVARCHAR(500) NULL,
        [PetitionerRelationship] NVARCHAR(500) NULL,
        [ClaimType] NVARCHAR(200) NULL,
        [DateOfClaimPetition] DATETIME NULL,
        [CaseStage] NVARCHAR(200) NULL,
        [DocumentSent] NVARCHAR(10) NULL,
        [ObjectionVerified] NVARCHAR(10) NULL,
        [ObjectionOutwardNumber] NVARCHAR(100) NULL,
        [ObjectionFiledDate] DATETIME NULL,
        [IsObjectionFiled] NVARCHAR(10) NULL,
        [ObjectionPending] NVARCHAR(10) NULL,
        [ObjectionRemarks] NVARCHAR(MAX) NULL,
        [InitialOutwardNumber] NVARCHAR(100) NULL,
        [InitialOutwardDate] DATETIME NULL,
        [VehicleNumber] NVARCHAR(50) NULL,
        [DateOfAccident] DATETIME NULL,
        [ClaimAmount] DECIMAL(18, 2) NULL,
        [IsVehicleInvolved] NVARCHAR(10) NULL,
        [VehicleType] NVARCHAR(50) NULL,
        [IsCorporationEmployee] NVARCHAR(10) NULL,
        [MannerOfIncident] NVARCHAR(MAX) NULL,
        [ClaimRemarks] NVARCHAR(MAX) NULL,
        [AppealNumber] NVARCHAR(100) NULL,
        [AppealYear] INT NULL,
        [ArisingOutOSNumber] NVARCHAR(100) NULL,
        [OSYear] INT NULL,
        [AppealEntrustmentNumber] NVARCHAR(100) NULL,
        [AppealEntrustmentDate] DATE NULL,
        [CourtAppellateAuth] NVARCHAR(200) NULL,
        [AppealAdvocateName] NVARCHAR(200) NULL,
        [AppealComplianceAmount] DECIMAL(18, 2) NULL,
        [AppealChequeNumber] NVARCHAR(100) NULL,
        [AppealChequeDate] DATE NULL,
        [AppealAwardDetails] NVARCHAR(MAX) NULL,
        [AppealCaseDisposedOn] DATE NULL,
        [AppealCopyAppliedOn] DATE NULL,
        [AppealCopyReadyOn] DATE NULL,
        [AppealCopyDeliveredOn] DATE NULL,
        [AppealCopyReceivedAtDivision] DATE NULL,
        [AppealDelayRemarks] NVARCHAR(MAX) NULL,
        [AppealAdvocateOpinion] NVARCHAR(MAX) NULL,
        [AppealALOOpinion] NVARCHAR(MAX) NULL,
        [AppealDCOpinion] NVARCHAR(MAX) NULL,
        [AppealForwardingStatus] NVARCHAR(100) NULL,
        [AppealOutwardNumber] NVARCHAR(100) NULL,
        [AppealOutwardDate] DATE NULL,
        [AppealClosureNumber] NVARCHAR(100) NULL,
        [AppealClosureDate] DATE NULL,
        [AppealJudgmentCopyPath] NVARCHAR(MAX) NULL,
        [CNRNumber] NVARCHAR(50) NULL,
        [EstCode] NVARCHAR(50) NULL,
        [CaseTypeCode] NVARCHAR(20) NULL,
        [OtherCourtDetails] NVARCHAR(200) NULL,
        [LastNapixSyncAt] DATETIME2 NULL,
        [LastNapixSyncStatus] NVARCHAR(100) NULL,
        [LastNapixSyncError] NVARCHAR(MAX) NULL,
        [NapixSyncAttemptCount] INT NOT NULL DEFAULT ((0)),
        [NapixDataHash] NVARCHAR(64) NULL,
        [PendDispStatus] NVARCHAR(10) NULL,
        [EstName] NVARCHAR(200) NULL,
        [ECourtsStage] NVARCHAR(200) NULL,
        [ECourtsCourtNo] NVARCHAR(50) NULL,
        [ECourtsJudge] NVARCHAR(200) NULL
    );
END;
GO

-- -----------------------------------------------------------------------------
-- Table: [PETTY_BILL_INTEREST]
-- -----------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.tables t JOIN sys.schemas s ON t.schema_id = s.schema_id WHERE t.name = 'PETTY_BILL_INTEREST' AND s.name = 'dbo')
BEGIN
    CREATE TABLE [dbo].[PETTY_BILL_INTEREST] (
        [InterestID] INT IDENTITY(1,1) NOT NULL,
        [BillID] INT NOT NULL,
        [FromDate] DATE NOT NULL,
        [ToDate] DATE NOT NULL,
        [Amount] DECIMAL(18, 2) NOT NULL DEFAULT ((0))
    );
END;
GO

-- -----------------------------------------------------------------------------
-- Table: [PETTY_BILL_PAYMENTS]
-- -----------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.tables t JOIN sys.schemas s ON t.schema_id = s.schema_id WHERE t.name = 'PETTY_BILL_PAYMENTS' AND s.name = 'dbo')
BEGIN
    CREATE TABLE [dbo].[PETTY_BILL_PAYMENTS] (
        [PaymentID] INT IDENTITY(1,1) NOT NULL,
        [BillID] INT NOT NULL,
        [ChequeNumber] NVARCHAR(100) NULL,
        [ChequeDate] DATE NULL,
        [Amount] DECIMAL(18, 2) NOT NULL DEFAULT ((0)),
        [IsSelected] BIT NOT NULL DEFAULT ((1)),
        [Remarks] NVARCHAR(500) NULL,
        [IsCancelled] BIT NOT NULL DEFAULT ((0))
    );
END;
GO

-- -----------------------------------------------------------------------------
-- Table: [PETTY_BILLS]
-- -----------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.tables t JOIN sys.schemas s ON t.schema_id = s.schema_id WHERE t.name = 'PETTY_BILLS' AND s.name = 'dbo')
BEGIN
    CREATE TABLE [dbo].[PETTY_BILLS] (
        [BillID] INT IDENTITY(1,1) NOT NULL,
        [CaseID] INT NULL,
        [MVCNo] NVARCHAR(50) NULL,
        [MVCYear] INT NULL,
        [MACTName] NVARCHAR(200) NULL,
        [VehicleNo] NVARCHAR(50) NULL,
        [AccidentDate] DATE NULL,
        [PetitionerName] NVARCHAR(200) NULL,
        [AppealNumber] NVARCHAR(100) NULL,
        [DivisionName] NVARCHAR(100) NULL,
        [EnhancedAmount] DECIMAL(18, 2) NOT NULL DEFAULT ((0)),
        [CourtCost] DECIMAL(18, 2) NOT NULL DEFAULT ((0)),
        [TDSPercentage] DECIMAL(5, 2) NOT NULL DEFAULT ((10)),
        [TDSAmount] DECIMAL(18, 2) NOT NULL DEFAULT ((0)),
        [FileRefNo] NVARCHAR(100) NULL,
        [ApprovalLetterNo] NVARCHAR(100) NULL,
        [ApprovalDate] DATE NULL,
        [BillDate] DATE NULL,
        [NetPayable] DECIMAL(18, 2) NOT NULL DEFAULT ((0)),
        [GrossPayable] DECIMAL(18, 2) NOT NULL DEFAULT ((0)),
        [TotalDeductions] DECIMAL(18, 2) NOT NULL DEFAULT ((0)),
        [CreatedAt] DATETIME NOT NULL DEFAULT (getdate()),
        [UpdatedAt] DATETIME NOT NULL DEFAULT (getdate()),
        [AwardType] NVARCHAR(50) NULL DEFAULT ('Lower Court'),
        [BaseAwardAmount] DECIMAL(18, 2) NOT NULL DEFAULT ((0)),
        [InterestFrom] DATE NULL,
        [InterestTo] DATE NULL,
        [IsRelaxationApplied] BIT NULL,
        [RelaxationFrom] DATE NULL,
        [RelaxationTo] DATE NULL
    );
END;
GO

-- -----------------------------------------------------------------------------
-- Table: [ROLE_MASTER]
-- -----------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.tables t JOIN sys.schemas s ON t.schema_id = s.schema_id WHERE t.name = 'ROLE_MASTER' AND s.name = 'dbo')
BEGIN
    CREATE TABLE [dbo].[ROLE_MASTER] (
        [RoleID] INT IDENTITY(1,1) NOT NULL,
        [RoleName] NVARCHAR(50) NOT NULL,
        [Description] NVARCHAR(200) NULL,
        [IsActive] BIT NULL DEFAULT ((1)),
        [CreatedDate] DATETIME NULL DEFAULT (getdate())
    );
END;
GO

-- -----------------------------------------------------------------------------
-- Table: [SCHEMA_MIGRATIONS]
-- -----------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.tables t JOIN sys.schemas s ON t.schema_id = s.schema_id WHERE t.name = 'SCHEMA_MIGRATIONS' AND s.name = 'dbo')
BEGIN
    CREATE TABLE [dbo].[SCHEMA_MIGRATIONS] (
        [MigrationID] NVARCHAR(200) NOT NULL,
        [Description] NVARCHAR(500) NOT NULL,
        [AppliedAt] DATETIME2 NOT NULL DEFAULT (sysutcdatetime()),
        [ExecutionTimeMs] INT NOT NULL
    );
END;
GO

-- -----------------------------------------------------------------------------
-- Table: [SYSTEM_NOTIFICATIONS]
-- -----------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.tables t JOIN sys.schemas s ON t.schema_id = s.schema_id WHERE t.name = 'SYSTEM_NOTIFICATIONS' AND s.name = 'dbo')
BEGIN
    CREATE TABLE [dbo].[SYSTEM_NOTIFICATIONS] (
        [NotificationID] INT IDENTITY(1,1) NOT NULL,
        [UserID] INT NULL,
        [DivisionID] INT NULL,
        [Title] NVARCHAR(200) NOT NULL,
        [Message] NVARCHAR(MAX) NOT NULL,
        [RelatedCaseType] NVARCHAR(100) NULL,
        [RelatedCaseID] INT NULL,
        [LinkUrl] NVARCHAR(500) NULL,
        [IsRead] BIT NOT NULL DEFAULT ((0)),
        [CreatedDate] DATETIME NULL DEFAULT (getdate())
    );
END;
GO

-- -----------------------------------------------------------------------------
-- Table: [TRACKED_CASES]
-- -----------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.tables t JOIN sys.schemas s ON t.schema_id = s.schema_id WHERE t.name = 'TRACKED_CASES' AND s.name = 'dbo')
BEGIN
    CREATE TABLE [dbo].[TRACKED_CASES] (
        [TrackedCaseID] INT IDENTITY(1,1) NOT NULL,
        [CNRNumber] NVARCHAR(16) NULL,
        [EstCode] NVARCHAR(50) NOT NULL,
        [CaseTypeCode] NVARCHAR(50) NOT NULL,
        [RegNo] NVARCHAR(50) NOT NULL,
        [RegYear] INT NOT NULL,
        [PetitionerName] NVARCHAR(255) NULL,
        [RespondentName] NVARCHAR(255) NULL,
        [CurrentStage] NVARCHAR(100) NULL,
        [NextHearingDate] DATE NULL,
        [CourtNo] NVARCHAR(50) NULL,
        [JudgeName] NVARCHAR(200) NULL,
        [IsHighCourt] BIT NULL DEFAULT ((0)),
        [LastSyncedDate] DATETIME NULL DEFAULT (getdate()),
        [CreatedDate] DATETIME NULL DEFAULT (getdate())
    );
END;
GO

-- -----------------------------------------------------------------------------
-- Table: [USER_OTP_VERIFICATIONS]
-- -----------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.tables t JOIN sys.schemas s ON t.schema_id = s.schema_id WHERE t.name = 'USER_OTP_VERIFICATIONS' AND s.name = 'dbo')
BEGIN
    CREATE TABLE [dbo].[USER_OTP_VERIFICATIONS] (
        [VerificationID] INT IDENTITY(1,1) NOT NULL,
        [UserID] INT NOT NULL,
        [OTP] VARCHAR(10) NOT NULL,
        [ExpiryTime] DATETIME NOT NULL,
        [IsVerified] BIT NULL DEFAULT ((0)),
        [CreatedAt] DATETIME NULL DEFAULT (getdate())
    );
END;
GO

-- -----------------------------------------------------------------------------
-- Table: [USERS]
-- -----------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.tables t JOIN sys.schemas s ON t.schema_id = s.schema_id WHERE t.name = 'USERS' AND s.name = 'dbo')
BEGIN
    CREATE TABLE [dbo].[USERS] (
        [UserID] INT IDENTITY(1,1) NOT NULL,
        [Username] NVARCHAR(50) NOT NULL,
        [PasswordHash] NVARCHAR(256) NOT NULL,
        [FullName] NVARCHAR(100) NOT NULL,
        [Email] NVARCHAR(100) NULL,
        [Mobile] NVARCHAR(15) NULL,
        [RoleID] INT NOT NULL,
        [DivisionID] INT NULL,
        [IsActive] BIT NULL DEFAULT ((1)),
        [LastLogin] DATETIME NULL,
        [CreatedBy] NVARCHAR(50) NULL,
        [CreatedDate] DATETIME NULL DEFAULT (getdate()),
        [ModifiedBy] NVARCHAR(50) NULL,
        [ModifiedDate] DATETIME NULL
    );
END;
GO

-- =============================================================================
-- PRIMARY KEY CONSTRAINTS
-- =============================================================================
IF NOT EXISTS (SELECT 1 FROM sys.key_constraints WHERE name = 'PK__ADVOCATE__AAC1BB59FAB5D822')
BEGIN
    ALTER TABLE [dbo].[ADVOCATE_MASTER] ADD CONSTRAINT [PK__ADVOCATE__AAC1BB59FAB5D822] PRIMARY KEY CLUSTERED ([AdvocateID]);
END;
GO
IF NOT EXISTS (SELECT 1 FROM sys.key_constraints WHERE name = 'PK__APPEAL_C__CF8832E469F45927')
BEGIN
    ALTER TABLE [dbo].[APPEAL_CONNECTED] ADD CONSTRAINT [PK__APPEAL_C__CF8832E469F45927] PRIMARY KEY CLUSTERED ([ConnectedID]);
END;
GO
IF NOT EXISTS (SELECT 1 FROM sys.key_constraints WHERE name = 'PK__APPEAL_D__BB684E1045D55A47')
BEGIN
    ALTER TABLE [dbo].[APPEAL_DETAILS] ADD CONSTRAINT [PK__APPEAL_D__BB684E1045D55A47] PRIMARY KEY CLUSTERED ([AppealID]);
END;
GO
IF NOT EXISTS (SELECT 1 FROM sys.key_constraints WHERE name = 'PK__AUDIT_LO__5E5499A8E6F3FB8D')
BEGIN
    ALTER TABLE [dbo].[AUDIT_LOG] ADD CONSTRAINT [PK__AUDIT_LO__5E5499A8E6F3FB8D] PRIMARY KEY CLUSTERED ([LogID]);
END;
GO
IF NOT EXISTS (SELECT 1 FROM sys.key_constraints WHERE name = 'PK__CASE_STA__C8EE2043C78B7444')
BEGIN
    ALTER TABLE [dbo].[CASE_STATUS_MASTER] ADD CONSTRAINT [PK__CASE_STA__C8EE2043C78B7444] PRIMARY KEY CLUSTERED ([StatusID]);
END;
GO
IF NOT EXISTS (SELECT 1 FROM sys.key_constraints WHERE name = 'PK__CASE_VIE__1E371C16A057B7C9')
BEGIN
    ALTER TABLE [dbo].[CASE_VIEW_TRACKING] ADD CONSTRAINT [PK__CASE_VIE__1E371C16A057B7C9] PRIMARY KEY CLUSTERED ([ViewID]);
END;
GO
IF NOT EXISTS (SELECT 1 FROM sys.key_constraints WHERE name = 'PK__DEPOT_MA__42C1C1F76ACA54F5')
BEGIN
    ALTER TABLE [dbo].[DEPOT_MASTER] ADD CONSTRAINT [PK__DEPOT_MA__42C1C1F76ACA54F5] PRIMARY KEY CLUSTERED ([DepotID]);
END;
GO
IF NOT EXISTS (SELECT 1 FROM sys.key_constraints WHERE name = 'PK__DIVISION__20EFC688398F811E')
BEGIN
    ALTER TABLE [dbo].[DIVISION_MASTER] ADD CONSTRAINT [PK__DIVISION__20EFC688398F811E] PRIMARY KEY CLUSTERED ([DivisionID]);
END;
GO
IF NOT EXISTS (SELECT 1 FROM sys.key_constraints WHERE name = 'PK_GRA_CASES')
BEGIN
    ALTER TABLE [dbo].[GRA_CASES] ADD CONSTRAINT [PK_GRA_CASES] PRIMARY KEY CLUSTERED ([CaseID]);
END;
GO
IF NOT EXISTS (SELECT 1 FROM sys.key_constraints WHERE name = 'PK__GRA_PAYM__9B556A580607B89E')
BEGIN
    ALTER TABLE [dbo].[GRA_PAYMENTS] ADD CONSTRAINT [PK__GRA_PAYM__9B556A580607B89E] PRIMARY KEY CLUSTERED ([PaymentID]);
END;
GO
IF NOT EXISTS (SELECT 1 FROM sys.key_constraints WHERE name = 'PK_GRATUITY_ADVOCATE_MASTER')
BEGIN
    ALTER TABLE [dbo].[GRATUITY_ADVOCATE_MASTER] ADD CONSTRAINT [PK_GRATUITY_ADVOCATE_MASTER] PRIMARY KEY CLUSTERED ([AdvocateID]);
END;
GO
IF NOT EXISTS (SELECT 1 FROM sys.key_constraints WHERE name = 'PK_GRATUITY_COURT_MASTER')
BEGIN
    ALTER TABLE [dbo].[GRATUITY_COURT_MASTER] ADD CONSTRAINT [PK_GRATUITY_COURT_MASTER] PRIMARY KEY CLUSTERED ([CourtID]);
END;
GO
IF NOT EXISTS (SELECT 1 FROM sys.key_constraints WHERE name = 'PK_GRATUITY_ENCLOSED_DOCS')
BEGIN
    ALTER TABLE [dbo].[GRATUITY_ENCLOSED_DOCS] ADD CONSTRAINT [PK_GRATUITY_ENCLOSED_DOCS] PRIMARY KEY CLUSTERED ([DocID]);
END;
GO
IF NOT EXISTS (SELECT 1 FROM sys.key_constraints WHERE name = 'PK__HIGH_COU__AAC1BB5929A0C86A')
BEGIN
    ALTER TABLE [dbo].[HIGH_COURT_ADVOCATES] ADD CONSTRAINT [PK__HIGH_COU__AAC1BB5929A0C86A] PRIMARY KEY CLUSTERED ([AdvocateID]);
END;
GO
IF NOT EXISTS (SELECT 1 FROM sys.key_constraints WHERE name = 'PK__JUDGEMEN__0846C26F1931F147')
BEGIN
    ALTER TABLE [dbo].[JUDGEMENT_REPO] ADD CONSTRAINT [PK__JUDGEMEN__0846C26F1931F147] PRIMARY KEY CLUSTERED ([JudgementID]);
END;
GO
IF NOT EXISTS (SELECT 1 FROM sys.key_constraints WHERE name = 'PK__MACT_MAS__F50CBCAA1B68DB1A')
BEGIN
    ALTER TABLE [dbo].[MACT_MASTER] ADD CONSTRAINT [PK__MACT_MAS__F50CBCAA1B68DB1A] PRIMARY KEY CLUSTERED ([MACTID]);
END;
GO
IF NOT EXISTS (SELECT 1 FROM sys.key_constraints WHERE name = 'PK__MVC_CASE__6CAE526C45A3AB2B')
BEGIN
    ALTER TABLE [dbo].[MVC_CASE] ADD CONSTRAINT [PK__MVC_CASE__6CAE526C45A3AB2B] PRIMARY KEY CLUSTERED ([CaseID]);
END;
GO
IF NOT EXISTS (SELECT 1 FROM sys.key_constraints WHERE name = 'PK__MVC_CASE__A338123EDD74D185')
BEGIN
    ALTER TABLE [dbo].[MVC_CASE_ADVERSE_CONNECTED] ADD CONSTRAINT [PK__MVC_CASE__A338123EDD74D185] PRIMARY KEY CLUSTERED ([AdvConnID]);
END;
GO
IF NOT EXISTS (SELECT 1 FROM sys.key_constraints WHERE name = 'PK__MVC_CASE__C9C05964F141DACF')
BEGIN
    ALTER TABLE [dbo].[MVC_CASE_ADVERSE_DETAILS] ADD CONSTRAINT [PK__MVC_CASE__C9C05964F141DACF] PRIMARY KEY CLUSTERED ([AdverseID]);
END;
GO
IF NOT EXISTS (SELECT 1 FROM sys.key_constraints WHERE name = 'PK__MVC_CASE__BD006BFF87375374')
BEGIN
    ALTER TABLE [dbo].[MVC_CASE_ADVERSE_PW] ADD CONSTRAINT [PK__MVC_CASE__BD006BFF87375374] PRIMARY KEY CLUSTERED ([PWID]);
END;
GO
IF NOT EXISTS (SELECT 1 FROM sys.key_constraints WHERE name = 'PK__MVC_CASE__DC152C81427EFDF3')
BEGIN
    ALTER TABLE [dbo].[MVC_CASE_ADVERSE_RW] ADD CONSTRAINT [PK__MVC_CASE__DC152C81427EFDF3] PRIMARY KEY CLUSTERED ([RWID]);
END;
GO
IF NOT EXISTS (SELECT 1 FROM sys.key_constraints WHERE name = 'PK__MVC_CASE__CF8832E4AACA3821')
BEGIN
    ALTER TABLE [dbo].[MVC_CASE_CONNECTED] ADD CONSTRAINT [PK__MVC_CASE__CF8832E4AACA3821] PRIMARY KEY CLUSTERED ([ConnectedID]);
END;
GO
IF NOT EXISTS (SELECT 1 FROM sys.key_constraints WHERE name = 'PK__MVC_CASE__9B556A580321EA49')
BEGIN
    ALTER TABLE [dbo].[MVC_CASE_PAYMENTS] ADD CONSTRAINT [PK__MVC_CASE__9B556A580321EA49] PRIMARY KEY CLUSTERED ([PaymentID]);
END;
GO
IF NOT EXISTS (SELECT 1 FROM sys.key_constraints WHERE name = 'PK__MVC_CASE__9B556A580321EA49')
BEGIN
    ALTER TABLE [dbo].[MVC_CASE_PAYMENTS] ADD CONSTRAINT [PK__MVC_CASE__9B556A580321EA49] PRIMARY KEY CLUSTERED ([PaymentID]);
END;
GO
IF NOT EXISTS (SELECT 1 FROM sys.key_constraints WHERE name = 'PK__MVC_CASE__178BDCD00B6C5EDB')
BEGIN
    ALTER TABLE [dbo].[MVC_CASE_PETITIONERS] ADD CONSTRAINT [PK__MVC_CASE__178BDCD00B6C5EDB] PRIMARY KEY CLUSTERED ([PetitionerID]);
END;
GO
IF NOT EXISTS (SELECT 1 FROM sys.key_constraints WHERE name = 'PK__MVC_CASE__6CAE526C8A0835E6')
BEGIN
    ALTER TABLE [dbo].[MVC_CASES] ADD CONSTRAINT [PK__MVC_CASE__6CAE526C8A0835E6] PRIMARY KEY CLUSTERED ([CaseID]);
END;
GO
IF NOT EXISTS (SELECT 1 FROM sys.key_constraints WHERE name = 'PK_MVC_DIVISION_ADVOCATES')
BEGIN
    ALTER TABLE [dbo].[MVC_DIVISION_ADVOCATES] ADD CONSTRAINT [PK_MVC_DIVISION_ADVOCATES] PRIMARY KEY CLUSTERED ([Id]);
END;
GO
IF NOT EXISTS (SELECT 1 FROM sys.key_constraints WHERE name = 'PK__NAPIX_AP__5180CF8A9BBA105A')
BEGIN
    ALTER TABLE [dbo].[NAPIX_API_CALLS] ADD CONSTRAINT [PK__NAPIX_AP__5180CF8A9BBA105A] PRIMARY KEY CLUSTERED ([CallID]);
END;
GO
IF NOT EXISTS (SELECT 1 FROM sys.key_constraints WHERE name = 'PK__NAPIX_SY__8324E715A7A6E6CA')
BEGIN
    ALTER TABLE [dbo].[NAPIX_SYNC_QUEUE] ADD CONSTRAINT [PK__NAPIX_SY__8324E715A7A6E6CA] PRIMARY KEY CLUSTERED ([QueueId]);
END;
GO
IF NOT EXISTS (SELECT 1 FROM sys.key_constraints WHERE name = 'PK__PETTY_BI__20832C07BDD09C37')
BEGIN
    ALTER TABLE [dbo].[PETTY_BILL_INTEREST] ADD CONSTRAINT [PK__PETTY_BI__20832C07BDD09C37] PRIMARY KEY CLUSTERED ([InterestID]);
END;
GO
IF NOT EXISTS (SELECT 1 FROM sys.key_constraints WHERE name = 'PK__PETTY_BI__9B556A58161CACA0')
BEGIN
    ALTER TABLE [dbo].[PETTY_BILL_PAYMENTS] ADD CONSTRAINT [PK__PETTY_BI__9B556A58161CACA0] PRIMARY KEY CLUSTERED ([PaymentID]);
END;
GO
IF NOT EXISTS (SELECT 1 FROM sys.key_constraints WHERE name = 'PK__PETTY_BI__11F2FC4ACEDF691B')
BEGIN
    ALTER TABLE [dbo].[PETTY_BILLS] ADD CONSTRAINT [PK__PETTY_BI__11F2FC4ACEDF691B] PRIMARY KEY CLUSTERED ([BillID]);
END;
GO
IF NOT EXISTS (SELECT 1 FROM sys.key_constraints WHERE name = 'PK__ROLE_MAS__8AFACE3A45AE1851')
BEGIN
    ALTER TABLE [dbo].[ROLE_MASTER] ADD CONSTRAINT [PK__ROLE_MAS__8AFACE3A45AE1851] PRIMARY KEY CLUSTERED ([RoleID]);
END;
GO
IF NOT EXISTS (SELECT 1 FROM sys.key_constraints WHERE name = 'PK__USERS__1788CCAC787C9F36')
BEGIN
    ALTER TABLE [dbo].[USERS] ADD CONSTRAINT [PK__USERS__1788CCAC787C9F36] PRIMARY KEY CLUSTERED ([UserID]);
END;
GO

-- =============================================================================
-- FOREIGN KEY CONSTRAINTS
-- =============================================================================
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_GRA_INTEREST_PAYMENTS_CaseID')
BEGIN
    ALTER TABLE [dbo].[GRA_INTEREST_PAYMENTS] WITH CHECK ADD CONSTRAINT [FK_GRA_INTEREST_PAYMENTS_CaseID] FOREIGN KEY([CaseID]) REFERENCES [dbo].[GRA_CASES] ([CaseID]);
END;
GO
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK__GRA_PAYME__CaseI__7BE56230')
BEGIN
    ALTER TABLE [dbo].[GRA_PAYMENTS] WITH CHECK ADD CONSTRAINT [FK__GRA_PAYME__CaseI__7BE56230] FOREIGN KEY([CaseID]) REFERENCES [dbo].[GRA_CASES] ([CaseID]);
END;
GO
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_MVC_Status')
BEGIN
    ALTER TABLE [dbo].[MVC_CASE] WITH CHECK ADD CONSTRAINT [FK_MVC_Status] FOREIGN KEY([StatusID]) REFERENCES [dbo].[CASE_STATUS_MASTER] ([StatusID]);
END;
GO
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_PettyBillPayments_Bill')
BEGIN
    ALTER TABLE [dbo].[PETTY_BILL_PAYMENTS] WITH CHECK ADD CONSTRAINT [FK_PettyBillPayments_Bill] FOREIGN KEY([BillID]) REFERENCES [dbo].[PETTY_BILLS] ([BillID]);
END;
GO
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_PettyBillInterest_Bill')
BEGIN
    ALTER TABLE [dbo].[PETTY_BILL_INTEREST] WITH CHECK ADD CONSTRAINT [FK_PettyBillInterest_Bill] FOREIGN KEY([BillID]) REFERENCES [dbo].[PETTY_BILLS] ([BillID]);
END;
GO
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_OtherCases_Division')
BEGIN
    ALTER TABLE [dbo].[OTHER_CASES] WITH CHECK ADD CONSTRAINT [FK_OtherCases_Division] FOREIGN KEY([DivisionID]) REFERENCES [dbo].[DIVISION_MASTER] ([DivisionID]);
END;
GO
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_APPEAL_DETAILS_ClaimantDivisionID')
BEGIN
    ALTER TABLE [dbo].[APPEAL_DETAILS] WITH CHECK ADD CONSTRAINT [FK_APPEAL_DETAILS_ClaimantDivisionID] FOREIGN KEY([ClaimantDivisionID]) REFERENCES [dbo].[DIVISION_MASTER] ([DivisionID]);
END;
GO
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_MVC_Division')
BEGIN
    ALTER TABLE [dbo].[MVC_CASE] WITH CHECK ADD CONSTRAINT [FK_MVC_Division] FOREIGN KEY([DivisionID]) REFERENCES [dbo].[DIVISION_MASTER] ([DivisionID]);
END;
GO
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Users_Division')
BEGIN
    ALTER TABLE [dbo].[USERS] WITH CHECK ADD CONSTRAINT [FK_Users_Division] FOREIGN KEY([DivisionID]) REFERENCES [dbo].[DIVISION_MASTER] ([DivisionID]);
END;
GO
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Depot_Division')
BEGIN
    ALTER TABLE [dbo].[DEPOT_MASTER] WITH CHECK ADD CONSTRAINT [FK_Depot_Division] FOREIGN KEY([DivisionCode]) REFERENCES [dbo].[DIVISION_MASTER] ([DivisionCode]);
END;
GO
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_MVC_MACT')
BEGIN
    ALTER TABLE [dbo].[MVC_CASE] WITH CHECK ADD CONSTRAINT [FK_MVC_MACT] FOREIGN KEY([MACTID]) REFERENCES [dbo].[MACT_MASTER] ([MACTID]);
END;
GO
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_CAUSELIST_ITEMS_ListID')
BEGIN
    ALTER TABLE [dbo].[CAUSELIST_ITEMS] WITH CHECK ADD CONSTRAINT [FK_CAUSELIST_ITEMS_ListID] FOREIGN KEY([ListID]) REFERENCES [dbo].[DAILY_CAUSELISTS] ([ListID]);
END;
GO
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_LABOUR_ARISING_ENCLOSED_DOCS_ARISINGID')
BEGIN
    ALTER TABLE [dbo].[LABOUR_ARISING_ENCLOSED_DOCS] WITH CHECK ADD CONSTRAINT [FK_LABOUR_ARISING_ENCLOSED_DOCS_ARISINGID] FOREIGN KEY([ArisingID]) REFERENCES [dbo].[LABOUR_ARISING_APPLICATIONS] ([ArisingID]);
END;
GO
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_LABOUR_ARISING_PAYMENTS_ARISINGID')
BEGIN
    ALTER TABLE [dbo].[LABOUR_ARISING_PAYMENTS] WITH CHECK ADD CONSTRAINT [FK_LABOUR_ARISING_PAYMENTS_ARISINGID] FOREIGN KEY([ArisingID]) REFERENCES [dbo].[LABOUR_ARISING_APPLICATIONS] ([ArisingID]);
END;
GO
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_MVC_OppositeVehicles_CaseID')
BEGIN
    ALTER TABLE [dbo].[MVC_CASE_OPPOSITE_VEHICLES] WITH CHECK ADD CONSTRAINT [FK_MVC_OppositeVehicles_CaseID] FOREIGN KEY([CaseID]) REFERENCES [dbo].[MVC_CASES] ([CaseID]);
END;
GO
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_CasePayments_Case')
BEGIN
    ALTER TABLE [dbo].[MVC_CASE_PAYMENTS] WITH CHECK ADD CONSTRAINT [FK_CasePayments_Case] FOREIGN KEY([CaseID]) REFERENCES [dbo].[MVC_CASES] ([CaseID]);
END;
GO
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_MVC_CASE_RESPONDENTS_CaseID')
BEGIN
    ALTER TABLE [dbo].[MVC_CASE_RESPONDENTS] WITH CHECK ADD CONSTRAINT [FK_MVC_CASE_RESPONDENTS_CaseID] FOREIGN KEY([CaseID]) REFERENCES [dbo].[MVC_CASES] ([CaseID]);
END;
GO
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_MVC_CASE_ADVERSE_DOCS_CaseID')
BEGIN
    ALTER TABLE [dbo].[MVC_CASE_ADVERSE_DOCS] WITH CHECK ADD CONSTRAINT [FK_MVC_CASE_ADVERSE_DOCS_CaseID] FOREIGN KEY([CaseID]) REFERENCES [dbo].[MVC_CASES] ([CaseID]);
END;
GO
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_EP_Case')
BEGIN
    ALTER TABLE [dbo].[MVC_EP_DETAILS] WITH CHECK ADD CONSTRAINT [FK_EP_Case] FOREIGN KEY([CaseID]) REFERENCES [dbo].[MVC_CASES] ([CaseID]);
END;
GO
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK__APPEAL_CO__CaseI__6C190EBB')
BEGIN
    ALTER TABLE [dbo].[APPEAL_CONNECTED] WITH CHECK ADD CONSTRAINT [FK__APPEAL_CO__CaseI__6C190EBB] FOREIGN KEY([CaseID]) REFERENCES [dbo].[MVC_CASES] ([CaseID]);
END;
GO
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK__APPEAL_DE__CaseI__6D0D32F4')
BEGIN
    ALTER TABLE [dbo].[APPEAL_DETAILS] WITH CHECK ADD CONSTRAINT [FK__APPEAL_DE__CaseI__6D0D32F4] FOREIGN KEY([CaseID]) REFERENCES [dbo].[MVC_CASES] ([CaseID]);
END;
GO
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK__CASE_VIEW__CaseI__6EF57B66')
BEGIN
    ALTER TABLE [dbo].[CASE_VIEW_TRACKING] WITH CHECK ADD CONSTRAINT [FK__CASE_VIEW__CaseI__6EF57B66] FOREIGN KEY([CaseID]) REFERENCES [dbo].[MVC_CASES] ([CaseID]);
END;
GO
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK__MVC_CASE___CaseI__73BA3083')
BEGIN
    ALTER TABLE [dbo].[MVC_CASE_ADVERSE_CONNECTED] WITH CHECK ADD CONSTRAINT [FK__MVC_CASE___CaseI__73BA3083] FOREIGN KEY([CaseID]) REFERENCES [dbo].[MVC_CASES] ([CaseID]);
END;
GO
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK__MVC_CASE___CaseI__74AE54BC')
BEGIN
    ALTER TABLE [dbo].[MVC_CASE_ADVERSE_DETAILS] WITH CHECK ADD CONSTRAINT [FK__MVC_CASE___CaseI__74AE54BC] FOREIGN KEY([CaseID]) REFERENCES [dbo].[MVC_CASES] ([CaseID]);
END;
GO
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK__MVC_CASE___CaseI__75A278F5')
BEGIN
    ALTER TABLE [dbo].[MVC_CASE_ADVERSE_PW] WITH CHECK ADD CONSTRAINT [FK__MVC_CASE___CaseI__75A278F5] FOREIGN KEY([CaseID]) REFERENCES [dbo].[MVC_CASES] ([CaseID]);
END;
GO
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK__MVC_CASE___CaseI__76969D2E')
BEGIN
    ALTER TABLE [dbo].[MVC_CASE_ADVERSE_RW] WITH CHECK ADD CONSTRAINT [FK__MVC_CASE___CaseI__76969D2E] FOREIGN KEY([CaseID]) REFERENCES [dbo].[MVC_CASES] ([CaseID]);
END;
GO
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK__MVC_CASE___CaseI__778AC167')
BEGIN
    ALTER TABLE [dbo].[MVC_CASE_CONNECTED] WITH CHECK ADD CONSTRAINT [FK__MVC_CASE___CaseI__778AC167] FOREIGN KEY([CaseID]) REFERENCES [dbo].[MVC_CASES] ([CaseID]);
END;
GO
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK__MVC_CASE___CaseI__787EE5A0')
BEGIN
    ALTER TABLE [dbo].[MVC_CASE_PETITIONERS] WITH CHECK ADD CONSTRAINT [FK__MVC_CASE___CaseI__787EE5A0] FOREIGN KEY([CaseID]) REFERENCES [dbo].[MVC_CASES] ([CaseID]);
END;
GO
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Users_Role')
BEGIN
    ALTER TABLE [dbo].[USERS] WITH CHECK ADD CONSTRAINT [FK_Users_Role] FOREIGN KEY([RoleID]) REFERENCES [dbo].[ROLE_MASTER] ([RoleID]);
END;
GO
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_ArisingApp_ParentCase')
BEGIN
    ALTER TABLE [dbo].[LABOUR_ARISING_APPLICATIONS] WITH CHECK ADD CONSTRAINT [FK_ArisingApp_ParentCase] FOREIGN KEY([ParentCaseID]) REFERENCES [dbo].[LABOUR_CASES] ([CaseID]);
END;
GO
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Labour_History_Case')
BEGIN
    ALTER TABLE [dbo].[LABOUR_CASE_HISTORY] WITH CHECK ADD CONSTRAINT [FK_Labour_History_Case] FOREIGN KEY([CaseID]) REFERENCES [dbo].[LABOUR_CASES] ([CaseID]);
END;
GO
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK__LABOUR_CA__CaseI__4A18FC72')
BEGIN
    ALTER TABLE [dbo].[LABOUR_CASE_EVIDENCE] WITH CHECK ADD CONSTRAINT [FK__LABOUR_CA__CaseI__4A18FC72] FOREIGN KEY([CaseID]) REFERENCES [dbo].[LABOUR_CASES] ([CaseID]);
END;
GO
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_ServiceMatter_Case')
BEGIN
    ALTER TABLE [dbo].[LABOUR_SERVICE_MATTERS] WITH CHECK ADD CONSTRAINT [FK_ServiceMatter_Case] FOREIGN KEY([CaseID]) REFERENCES [dbo].[LABOUR_CASES] ([CaseID]);
END;
GO
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_LABOUR_CONNECTED_CASES_LABOUR_CASES')
BEGIN
    ALTER TABLE [dbo].[LABOUR_CONNECTED_CASES] WITH CHECK ADD CONSTRAINT [FK_LABOUR_CONNECTED_CASES_LABOUR_CASES] FOREIGN KEY([CaseID]) REFERENCES [dbo].[LABOUR_CASES] ([CaseID]);
END;
GO
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_LabourView_Case')
BEGIN
    ALTER TABLE [dbo].[LABOUR_CASE_VIEW_TRACKING] WITH CHECK ADD CONSTRAINT [FK_LabourView_Case] FOREIGN KEY([CaseID]) REFERENCES [dbo].[LABOUR_CASES] ([CaseID]);
END;
GO
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_LabourReinstatedDocuments_Case')
BEGIN
    ALTER TABLE [dbo].[LABOUR_REINSTATED_DOCUMENTS] WITH CHECK ADD CONSTRAINT [FK_LabourReinstatedDocuments_Case] FOREIGN KEY([CaseID]) REFERENCES [dbo].[LABOUR_CASES] ([CaseID]);
END;
GO
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK__LABOUR_EP___EPID__3BEAD8AC')
BEGIN
    ALTER TABLE [dbo].[LABOUR_EP_PAYMENTS] WITH CHECK ADD CONSTRAINT [FK__LABOUR_EP___EPID__3BEAD8AC] FOREIGN KEY([EPID]) REFERENCES [dbo].[LABOUR_EP_DETAILS] ([EPID]);
END;
GO
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK__USER_OTP___UserI__2077C861')
BEGIN
    ALTER TABLE [dbo].[USER_OTP_VERIFICATIONS] WITH CHECK ADD CONSTRAINT [FK__USER_OTP___UserI__2077C861] FOREIGN KEY([UserID]) REFERENCES [dbo].[USERS] ([UserID]);
END;
GO
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_OtherCaseDocs_Case')
BEGIN
    ALTER TABLE [dbo].[OTHER_CASE_DOCUMENTS] WITH CHECK ADD CONSTRAINT [FK_OtherCaseDocs_Case] FOREIGN KEY([CaseID]) REFERENCES [dbo].[OTHER_CASES] ([CaseID]);
END;
GO
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_OtherCaseRespondents_Case')
BEGIN
    ALTER TABLE [dbo].[OTHER_CASE_RESPONDENTS] WITH CHECK ADD CONSTRAINT [FK_OtherCaseRespondents_Case] FOREIGN KEY([CaseID]) REFERENCES [dbo].[OTHER_CASES] ([CaseID]);
END;
GO
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_OtherEvidenceResp_Case')
BEGIN
    ALTER TABLE [dbo].[OTHER_CASE_EVIDENCE_RESPONDENT] WITH CHECK ADD CONSTRAINT [FK_OtherEvidenceResp_Case] FOREIGN KEY([CaseID]) REFERENCES [dbo].[OTHER_CASES] ([CaseID]);
END;
GO
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_OtherEvidenceCorp_Case')
BEGIN
    ALTER TABLE [dbo].[OTHER_CASE_EVIDENCE_CORP] WITH CHECK ADD CONSTRAINT [FK_OtherEvidenceCorp_Case] FOREIGN KEY([CaseID]) REFERENCES [dbo].[OTHER_CASES] ([CaseID]);
END;
GO
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK__OTHER_CAS__CaseI__68536ACF')
BEGIN
    ALTER TABLE [dbo].[OTHER_CASE_PETITIONERS] WITH CHECK ADD CONSTRAINT [FK__OTHER_CAS__CaseI__68536ACF] FOREIGN KEY([CaseID]) REFERENCES [dbo].[OTHER_CASES] ([CaseID]);
END;
GO
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK__MVC_REMIN__Remin__4F9CCB9E')
BEGIN
    ALTER TABLE [dbo].[MVC_REMIND_BACK_PETITIONERS] WITH CHECK ADD CONSTRAINT [FK__MVC_REMIN__Remin__4F9CCB9E] FOREIGN KEY([RemindBackID]) REFERENCES [dbo].[MVC_REMIND_BACK_CASES] ([RemindBackID]);
END;
GO
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK__MVC_REMIN__Remin__52793849')
BEGIN
    ALTER TABLE [dbo].[MVC_REMIND_BACK_CONNECTED] WITH CHECK ADD CONSTRAINT [FK__MVC_REMIN__Remin__52793849] FOREIGN KEY([RemindBackID]) REFERENCES [dbo].[MVC_REMIND_BACK_CASES] ([RemindBackID]);
END;
GO
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK__MVC_REMIN__Remin__5555A4F4')
BEGIN
    ALTER TABLE [dbo].[MVC_REMIND_BACK_ADVERSE_DETAILS] WITH CHECK ADD CONSTRAINT [FK__MVC_REMIN__Remin__5555A4F4] FOREIGN KEY([RemindBackID]) REFERENCES [dbo].[MVC_REMIND_BACK_CASES] ([RemindBackID]);
END;
GO
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK__MVC_REMIN__Remin__5832119F')
BEGIN
    ALTER TABLE [dbo].[MVC_REMIND_BACK_ADVERSE_PW] WITH CHECK ADD CONSTRAINT [FK__MVC_REMIN__Remin__5832119F] FOREIGN KEY([RemindBackID]) REFERENCES [dbo].[MVC_REMIND_BACK_CASES] ([RemindBackID]);
END;
GO
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK__MVC_REMIN__Remin__5B0E7E4A')
BEGIN
    ALTER TABLE [dbo].[MVC_REMIND_BACK_ADVERSE_RW] WITH CHECK ADD CONSTRAINT [FK__MVC_REMIN__Remin__5B0E7E4A] FOREIGN KEY([RemindBackID]) REFERENCES [dbo].[MVC_REMIND_BACK_CASES] ([RemindBackID]);
END;
GO
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK__MVC_REMIN__Remin__5DEAEAF5')
BEGIN
    ALTER TABLE [dbo].[MVC_REMIND_BACK_ADVERSE_CONNECTED] WITH CHECK ADD CONSTRAINT [FK__MVC_REMIN__Remin__5DEAEAF5] FOREIGN KEY([RemindBackID]) REFERENCES [dbo].[MVC_REMIND_BACK_CASES] ([RemindBackID]);
END;
GO
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_MVC_RemindBack_Respondents')
BEGIN
    ALTER TABLE [dbo].[MVC_REMIND_BACK_RESPONDENTS] WITH CHECK ADD CONSTRAINT [FK_MVC_RemindBack_Respondents] FOREIGN KEY([RemindBackID]) REFERENCES [dbo].[MVC_REMIND_BACK_CASES] ([RemindBackID]);
END;
GO
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK__MVC_EP_PAY__EPID__634EBE90')
BEGIN
    ALTER TABLE [dbo].[MVC_EP_PAYMENTS] WITH CHECK ADD CONSTRAINT [FK__MVC_EP_PAY__EPID__634EBE90] FOREIGN KEY([EPID]) REFERENCES [dbo].[MVC_EP_DETAILS] ([EPID]);
END;
GO

-- =============================================================================
-- STORED PROCEDURES & ROUTINES
-- =============================================================================
CREATE PROCEDURE dbo.usp_GetNapixQuotaSummary
                        AS
                        BEGIN
                            SET NOCOUNT ON;
                            DECLARE @HourStart DATETIME2 = DATEADD(HOUR, -1, SYSUTCDATETIME());

                            -- 1. Combined System Quota across 3 apps (3000 total capacity)
                            SELECT
                                COUNT(*) AS TotalCallsThisHour,
                                ISNULL(SUM(CASE WHEN IsSuccess=1 THEN 1 ELSE 0 END), 0) AS SuccessfulCalls,
                                ISNULL(SUM(CASE WHEN IsSuccess=0 THEN 1 ELSE 0 END), 0) AS FailedCalls,
                                3000 AS HourlyQuota,
                                3000 - COUNT(*) AS RemainingQuota,
                                CAST(COUNT(*) * 100.0 / 3000 AS DECIMAL(5,1)) AS UsedPercent
                            FROM dbo.NAPIX_API_CALLS
                            WHERE CalledAt >= @HourStart;

                            -- 2. Per-Module Breakdown (MVC, Labour, OtherCourts - 1000 each)
                            SELECT
                                m.ModuleName AS Module,
                                ISNULL(c.TotalCalls, 0) AS TotalCallsThisHour,
                                ISNULL(c.SuccessfulCalls, 0) AS SuccessfulCalls,
                                ISNULL(c.FailedCalls, 0) AS FailedCalls,
                                1000 AS HourlyQuota,
                                1000 - ISNULL(c.TotalCalls, 0) AS RemainingQuota,
                                CAST(ISNULL(c.TotalCalls, 0) * 100.0 / 1000 AS DECIMAL(5,1)) AS UsedPercent
                            FROM (VALUES ('MVC'), ('Labour'), ('OtherCourts')) AS m(ModuleName)
                            LEFT JOIN (
                                SELECT
                                    CASE 
                                        WHEN UPPER(Module) LIKE '%OTHER%' OR UPPER(Module) IN ('OS','PSC','CC','CONSUMER','LAC','ECA') THEN 'OtherCourts'
                                        WHEN UPPER(Module) LIKE '%LABOUR%' THEN 'Labour'
                                        ELSE 'MVC'
                                    END AS ModuleName,
                                    COUNT(*) AS TotalCalls,
                                    ISNULL(SUM(CASE WHEN IsSuccess=1 THEN 1 ELSE 0 END), 0) AS SuccessfulCalls,
                                    ISNULL(SUM(CASE WHEN IsSuccess=0 THEN 1 ELSE 0 END), 0) AS FailedCalls
                                FROM dbo.NAPIX_API_CALLS
                                WHERE CalledAt >= @HourStart
                                GROUP BY 
                                    CASE 
                                        WHEN UPPER(Module) LIKE '%OTHER%' OR UPPER(Module) IN ('OS','PSC','CC','CONSUMER','LAC','ECA') THEN 'OtherCourts'
                                        WHEN UPPER(Module) LIKE '%LABOUR%' THEN 'Labour'
                                        ELSE 'MVC'
                                    END
                            ) c ON m.ModuleName = c.ModuleName;

                            -- 3. Per-endpoint breakdown
                            SELECT
                                Endpoint,
                                Module,
                                COUNT(*) AS TotalCalls,
                                ISNULL(SUM(CASE WHEN IsSuccess=1 THEN 1 ELSE 0 END), 0) AS SuccessCalls,
                                ISNULL(SUM(CASE WHEN IsSuccess=0 THEN 1 ELSE 0 END), 0) AS FailedCalls,
                                ISNULL(AVG(DurationMs), 0) AS AvgDurationMs
                            FROM dbo.NAPIX_API_CALLS
                            WHERE CalledAt >= @HourStart
                            GROUP BY Endpoint, Module
                            ORDER BY TotalCalls DESC;

                            -- 4. Per-minute trend
                            SELECT
                                DATEPART(MINUTE, DATEADD(HOUR,5,DATEADD(MINUTE,30,CalledAt))) AS MinuteIST,
                                DATEPART(HOUR, DATEADD(HOUR,5,DATEADD(MINUTE,30,CalledAt))) AS HourIST,
                                CAST(DATEADD(HOUR,5,DATEADD(MINUTE,30,CalledAt)) AS DATE) AS DateIST,
                                COUNT(*) AS CallCount
                            FROM dbo.NAPIX_API_CALLS
                            WHERE CalledAt >= @HourStart
                            GROUP BY
                                DATEPART(MINUTE, DATEADD(HOUR,5,DATEADD(MINUTE,30,CalledAt))),
                                DATEPART(HOUR, DATEADD(HOUR,5,DATEADD(MINUTE,30,CalledAt))),
                                CAST(DATEADD(HOUR,5,DATEADD(MINUTE,30,CalledAt)) AS DATE)
                            ORDER BY DateIST, HourIST, MinuteIST;

                            -- 5. Hourly trend
                            SELECT
                                DATEPART(HOUR, DATEADD(HOUR,5,DATEADD(MINUTE,30,CalledAt))) AS HourIST,
                                CAST(DATEADD(HOUR,5,DATEADD(MINUTE,30,CalledAt)) AS DATE) AS DateIST,
                                COUNT(*) AS CallCount
                            FROM dbo.NAPIX_API_CALLS
                            WHERE CalledAt >= DATEADD(HOUR,-24,SYSUTCDATETIME())
                            GROUP BY
                                DATEPART(HOUR, DATEADD(HOUR,5,DATEADD(MINUTE,30,CalledAt))),
                                CAST(DATEADD(HOUR,5,DATEADD(MINUTE,30,CalledAt)) AS DATE)
                            ORDER BY DateIST, HourIST;

                            -- 6. Recent 50 calls
                            SELECT TOP 50
                                CallID,
                                Module,
                                DATEADD(HOUR,5,DATEADD(MINUTE,30,CalledAt)) AS CalledAtIST,
                                Endpoint, HttpStatus, IsSuccess, Username, CNRNumber, DurationMs
                            FROM dbo.NAPIX_API_CALLS
                            ORDER BY CallID DESC;
                        END;
GO

CREATE PROCEDURE [dbo].[sp_GetDashboardStats] @DivisionID INT = 0 AS BEGIN SET NOCOUNT ON; DECLARE @TotalCases INT; DECLARE @PendingCases INT; DECLARE @FavorCases INT; DECLARE @AgainstCases INT; DECLARE @SentToCOCount INT; SELECT @TotalCases = COUNT(*), @PendingCases = SUM(CASE WHEN ISNULL(c.DisposalResult, '') = '' OR c.DisposalResult = 'Pending' THEN 1 ELSE 0 END), @FavorCases = SUM(CASE WHEN c.DisposalResult = 'Favor' THEN 1 ELSE 0 END), @AgainstCases = SUM(CASE WHEN c.DisposalResult = 'Against' THEN 1 ELSE 0 END), @SentToCOCount = SUM(CASE WHEN adv.ForwardingStatus = 'Sent to Central Office' AND vt.ViewID IS NULL THEN 1 ELSE 0 END) FROM MVC_CASES c LEFT JOIN MVC_CASE_ADVERSE_DETAILS adv ON c.CaseID = adv.CaseID LEFT JOIN CASE_VIEW_TRACKING vt ON c.CaseID = vt.CaseID WHERE (@DivisionID = 0 OR @DivisionID = 5 OR c.DivisionID = @DivisionID); SELECT ISNULL(@TotalCases, 0) AS TotalCases, ISNULL(@PendingCases, 0) AS PendingCases, ISNULL(@FavorCases, 0) AS FavorCases, ISNULL(@AgainstCases, 0) AS AgainstCases, ISNULL(@SentToCOCount, 0) AS SentToCOCount; END
GO

CREATE PROCEDURE dbo.sp_ReserveNapixQuotaSlot
            @Module NVARCHAR(20),
            @MaxCallsPerHour INT,
            @Reserved BIT OUTPUT,
            @CurrentUsage INT OUTPUT,
            @RetryAfterSeconds INT OUTPUT
        AS
        BEGIN
            SET NOCOUNT ON;
            SET TRANSACTION ISOLATION LEVEL SERIALIZABLE;
            BEGIN TRANSACTION;

            DECLARE @WindowStart DATETIME2 = DATEADD(MINUTE, -60, SYSUTCDATETIME());

            SELECT @CurrentUsage = COUNT(*)
            FROM dbo.NAPIX_API_CALLS WITH (UPDLOCK, HOLDLOCK)
            WHERE CalledAt >= @WindowStart
              AND (Module = @Module OR (@Module = 'MVC' AND Module IS NULL));

            -- Also account for currently in-flight requests in the queue
            DECLARE @InFlight INT = 0;
            SELECT @InFlight = COUNT(*)
            FROM dbo.NAPIX_SYNC_QUEUE WITH (UPDLOCK, HOLDLOCK)
            WHERE Module = @Module AND Status = 'Processing' AND LockedUntil > SYSUTCDATETIME();

            SET @CurrentUsage = @CurrentUsage + @InFlight;

            IF @CurrentUsage < @MaxCallsPerHour
            BEGIN
                SET @Reserved = 1;
                SET @RetryAfterSeconds = 0;
                COMMIT TRANSACTION;
            END
            ELSE
            BEGIN
                SET @Reserved = 0;
                SELECT @RetryAfterSeconds = DATEDIFF(SECOND, SYSUTCDATETIME(), DATEADD(MINUTE, 60, MIN(CalledAt)))
                FROM dbo.NAPIX_API_CALLS WITH (NOLOCK)
                WHERE CalledAt >= @WindowStart
                  AND (Module = @Module OR (@Module = 'MVC' AND Module IS NULL));

                IF @RetryAfterSeconds IS NULL OR @RetryAfterSeconds <= 0
                    SET @RetryAfterSeconds = 5;
                COMMIT TRANSACTION;
            END
        END;
GO

CREATE PROCEDURE dbo.sp_DequeueNapixSyncJob
            @Module NVARCHAR(20),
            @LeaseMinutes INT = 5
        AS
        BEGIN
            SET NOCOUNT ON;
            DECLARE @Now DATETIME2 = SYSUTCDATETIME();

            -- Recover expired leases
            UPDATE dbo.NAPIX_SYNC_QUEUE
            SET Status = 'Queued', LockedUntil = NULL, AttemptCount = AttemptCount + 1,
                LastError = 'Lease expired / recovered from ungraceful shutdown'
            WHERE Module = @Module 
              AND Status = 'Processing' 
              AND LockedUntil < @Now;

            -- Atomically lock and retrieve the top priority eligible job
            ;WITH NextJob AS (
                SELECT TOP (1) QueueId, Module, CaseId, CNRNumber, Priority, AttemptCount, Status, LockedUntil
                FROM dbo.NAPIX_SYNC_QUEUE WITH (UPDLOCK, READPAST)
                WHERE Module = @Module
                  AND Status = 'Queued'
                  AND (NextAttemptAt IS NULL OR NextAttemptAt <= @Now)
                ORDER BY Priority ASC, RequestedAt ASC
            )
            UPDATE NextJob
            SET Status = 'Processing',
                LockedUntil = DATEADD(MINUTE, @LeaseMinutes, @Now)
            OUTPUT 
                inserted.QueueId,
                inserted.Module,
                inserted.CaseId,
                inserted.CNRNumber,
                inserted.Priority,
                inserted.AttemptCount;
        END;
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

CREATE PROCEDURE [dbo].[sp_SaveAppealFull]
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
    @ClaimantMFARemarks NVARCHAR(MAX) = NULL,
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
                FeasibilityReceived = @FeasibilityReceived, FeasibilityReceiptDate = @FeasibilityReceiptDate,
                InitialAction = @InitialAction, ApprovalOutwardNo = @ApprovalOutwardNo, ApprovalDate = @ApprovalDate,
                InitialActionRemarks = @InitialActionRemarks, InitialActionPath1 = @InitialActionPath1, InitialActionPath2 = @InitialActionPath2,
                CorpMFANumber = @CorpMFANumber, CorpMFAYear = @CorpMFAYear, HighCourtBench = @HighCourtBench, OtherHighCourtBench = @OtherHighCourtBench,
                IsPendingForFiling = @IsPendingForFiling, CorpMFAEntrustmentNo = @CorpMFAEntrustmentNo, CorpMFAEntrustmentDate = @CorpMFAEntrustmentDate, CorpMFAAdvocate = @CorpMFAAdvocate,
                StayGranted = @StayGranted, StayComplianceOutwardNo = @StayComplianceOutwardNo, StayComplianceDate = @StayComplianceDate,
                StayOrderPath1 = @StayOrderPath1, StayOrderPath2 = @StayOrderPath2, ComplianceLetterPath = @ComplianceLetterPath,
                CorpMFAStatus = @CorpMFAStatus, RestorationFiled = @RestorationFiled, RestorationDate = @RestorationDate, RestorationStatus = @RestorationStatus,
                CorpMFAOutcome = @CorpMFAOutcome, CorpMFAActionTaken = @CorpMFAActionTaken, ClosureOutwardNo = @ClosureOutwardNo, ClosureDate = @ClosureDate,
                CorpSCNumber = @CorpSCNumber, CorpSCYear = @CorpSCYear, CorpSCEntrustmentNo = @CorpSCEntrustmentNo, CorpSCEntrustmentDate = @CorpSCEntrustmentDate, CorpSCAdvocate = @CorpSCAdvocate, CorpSCStatus = @CorpSCStatus,
                ClaimantDivisionID = @ClaimantDivisionID, ClaimantMVCNumber = @ClaimantMVCNumber, ClaimantMVCYear = @ClaimantMVCYear, ClaimantMVCCurrentStatus = @ClaimantMVCCurrentStatus,
                ClaimantMFANumber = @ClaimantMFANumber, ClaimantMFAYear = @ClaimantMFAYear, ClaimantMFAEntrustmentNo = @ClaimantMFAEntrustmentNo, ClaimantMFAEntrustmentDate = @ClaimantMFAEntrustmentDate,
                ClaimantMFAAdvocate = @ClaimantMFAAdvocate, ClaimantMFAStatus = @ClaimantMFAStatus, ClaimantMFADecision = @ClaimantMFADecision, ClaimantActionTaken = @ClaimantActionTaken, ClaimantApprovalNo = @ClaimantApprovalNo, ClaimantApprovalDate = @ClaimantApprovalDate, ClaimantMFARemarks = @ClaimantMFARemarks,
                ClaimantSCNumber = @ClaimantSCNumber, ClaimantSCDiaryNumber = @ClaimantSCDiaryNumber, ClaimantSCYear = @ClaimantSCYear, ClaimantSLPYear = @ClaimantSLPYear, ClaimantSCFiledBy = @ClaimantSCFiledBy, ClaimantSCEntrustmentNo = @ClaimantSCEntrustmentNo, ClaimantSCEntrustmentDate = @ClaimantSCEntrustmentDate, ClaimantSCAdvocate = @ClaimantSCAdvocate, ClaimantSCStatus = @ClaimantSCStatus,
                IsClaimantSCAppeal = @IsClaimantSCAppeal, IsClaimantSCPending = @IsClaimantSCPending, ClaimantSCOutcome = @ClaimantSCOutcome, ClaimantSCActionTaken = @ClaimantSCActionTaken, ClaimantSCClosureNo = @ClaimantSCClosureNo, ClaimantSCClosureDate = @ClaimantSCClosureDate,
                FinalComplianceStatus = @FinalComplianceStatus, AmountDeposited = @AmountDeposited, FinalComplianceDate = @FinalComplianceDate, FinalRemarks = @FinalRemarks, ModifiedAt = GETDATE()
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
                @ClaimantDivisionID, @ClaimantMVCNumber, @ClaimantMVCYear, @ClaimantMVCCurrentStatus,
                @ClaimantMFANumber, @ClaimantMFAYear, @ClaimantMFAEntrustmentNo, @ClaimantMFAEntrustmentDate,
                @ClaimantMFAAdvocate, @ClaimantMFAStatus, @ClaimantMFADecision, @ClaimantActionTaken, @ClaimantApprovalNo, @ClaimantApprovalDate, @ClaimantMFARemarks,
                @ClaimantSCNumber, @ClaimantSCDiaryNumber, @ClaimantSCYear, @ClaimantSLPYear, @ClaimantSCFiledBy, @ClaimantSCEntrustmentNo, @ClaimantSCEntrustmentDate, @ClaimantSCAdvocate, @ClaimantSCStatus,
                @IsClaimantSCAppeal, @IsClaimantSCPending, @ClaimantSCOutcome, @ClaimantSCActionTaken, @ClaimantSCClosureNo, @ClaimantSCClosureDate,
                @FinalComplianceStatus, @AmountDeposited, @FinalComplianceDate, @FinalRemarks, GETDATE()
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

-- =============================================================================
-- 🌿 SEED DATA: CORE SYSTEM & DIVISION MASTERS
-- =============================================================================
-- Seed Data: [ROLE_MASTER]
SET IDENTITY_INSERT [dbo].[ROLE_MASTER] ON;
IF NOT EXISTS (SELECT 1 FROM [dbo].[ROLE_MASTER] WHERE [RoleID] = 1)
    INSERT INTO [dbo].[ROLE_MASTER] ([RoleID], [RoleName], [Description], [IsActive], [CreatedDate]) VALUES (1, N'Admin', N'System Administrator - Full access', 1, '2026-01-08 16:54:20.153');
IF NOT EXISTS (SELECT 1 FROM [dbo].[ROLE_MASTER] WHERE [RoleID] = 2)
    INSERT INTO [dbo].[ROLE_MASTER] ([RoleID], [RoleName], [Description], [IsActive], [CreatedDate]) VALUES (2, N'Division User', N'Division Level User - Limited access', 1, '2026-01-08 16:54:20.153');
IF NOT EXISTS (SELECT 1 FROM [dbo].[ROLE_MASTER] WHERE [RoleID] = 3)
    INSERT INTO [dbo].[ROLE_MASTER] ([RoleID], [RoleName], [Description], [IsActive], [CreatedDate]) VALUES (3, N'MD', N'Only View', 1, '2026-02-26 12:43:43.910');
IF NOT EXISTS (SELECT 1 FROM [dbo].[ROLE_MASTER] WHERE [RoleID] = 4)
    INSERT INTO [dbo].[ROLE_MASTER] ([RoleID], [RoleName], [Description], [IsActive], [CreatedDate]) VALUES (4, N'CLO', N'Only View', 1, '2026-02-26 12:43:43.927');
IF NOT EXISTS (SELECT 1 FROM [dbo].[ROLE_MASTER] WHERE [RoleID] = 5)
    INSERT INTO [dbo].[ROLE_MASTER] ([RoleID], [RoleName], [Description], [IsActive], [CreatedDate]) VALUES (5, N'LO', NULL, 1, '2026-09-21 17:09:29.330');
IF NOT EXISTS (SELECT 1 FROM [dbo].[ROLE_MASTER] WHERE [RoleID] = 6)
    INSERT INTO [dbo].[ROLE_MASTER] ([RoleID], [RoleName], [Description], [IsActive], [CreatedDate]) VALUES (6, N'Dy CLO', NULL, 1, '2026-09-21 17:09:29.330');
SET IDENTITY_INSERT [dbo].[ROLE_MASTER] OFF;
GO

-- Seed Data: [DIVISION_MASTER]
SET IDENTITY_INSERT [dbo].[DIVISION_MASTER] ON;
IF NOT EXISTS (SELECT 1 FROM [dbo].[DIVISION_MASTER] WHERE [DivisionID] = 1)
    INSERT INTO [dbo].[DIVISION_MASTER] ([DivisionID], [DivisionCode], [DivisionNameEnglish], [DivisionNameKannada], [Location], [IsActive], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (1, N'BGK', N'Bagalkot', N'à²¬à²¾à²—à²²à²•à³‹à²Ÿà³†', NULL, 1, N'SYSTEM', '2026-01-08 16:54:20.157', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[DIVISION_MASTER] WHERE [DivisionID] = 2)
    INSERT INTO [dbo].[DIVISION_MASTER] ([DivisionID], [DivisionCode], [DivisionNameEnglish], [DivisionNameKannada], [Location], [IsActive], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (2, N'BGM', N'Belagavi', N'à²¬à³†à²³à²—à²¾à²µà²¿', NULL, 1, N'SYSTEM', '2026-01-08 16:54:20.157', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[DIVISION_MASTER] WHERE [DivisionID] = 4)
    INSERT INTO [dbo].[DIVISION_MASTER] ([DivisionID], [DivisionCode], [DivisionNameEnglish], [DivisionNameKannada], [Location], [IsActive], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (4, N'CKD', N'Chikkodi', N'à²šà²¿à²•à³à²•à³‹à²¡à²¿', NULL, 1, N'SYSTEM', '2026-01-08 16:54:20.157', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[DIVISION_MASTER] WHERE [DivisionID] = 5)
    INSERT INTO [dbo].[DIVISION_MASTER] ([DivisionID], [DivisionCode], [DivisionNameEnglish], [DivisionNameKannada], [Location], [IsActive], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (5, N'COH', N'Central Office', N'à²•à³‡à²‚à²¦à³à²° à²•à²›à³‡à²°à²¿', NULL, 1, N'SYSTEM', '2026-01-08 16:54:20.157', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[DIVISION_MASTER] WHERE [DivisionID] = 6)
    INSERT INTO [dbo].[DIVISION_MASTER] ([DivisionID], [DivisionCode], [DivisionNameEnglish], [DivisionNameKannada], [Location], [IsActive], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (6, N'DWR', N'Dharwad Rural', N'à²§à²¾à²°à²µà²¾à²¡ (à²—à³à²°à²¾)', NULL, 1, N'SYSTEM', '2026-01-08 16:54:20.157', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[DIVISION_MASTER] WHERE [DivisionID] = 7)
    INSERT INTO [dbo].[DIVISION_MASTER] ([DivisionID], [DivisionCode], [DivisionNameEnglish], [DivisionNameKannada], [Location], [IsActive], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (7, N'GDG', N'Gadag', N'à²—à²¦à²—', NULL, 1, N'SYSTEM', '2026-01-08 16:54:20.157', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[DIVISION_MASTER] WHERE [DivisionID] = 8)
    INSERT INTO [dbo].[DIVISION_MASTER] ([DivisionID], [DivisionCode], [DivisionNameEnglish], [DivisionNameKannada], [Location], [IsActive], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (8, N'HBL', N'Hubballi Rural', N'à²¹à³à²¬à³à²¬à²³à³à²³à²¿ (à²—à³à²°à²¾)', NULL, 1, N'SYSTEM', '2026-01-08 16:54:20.157', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[DIVISION_MASTER] WHERE [DivisionID] = 9)
    INSERT INTO [dbo].[DIVISION_MASTER] ([DivisionID], [DivisionCode], [DivisionNameEnglish], [DivisionNameKannada], [Location], [IsActive], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (9, N'HDC', N'Hubballi Dharwad City', N'à²¹à³-à²§à²¾ à²¨à²—à²° à²¸à²¾à²°à²¿à²—à³†', NULL, 1, N'SYSTEM', '2026-01-08 16:54:20.157', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[DIVISION_MASTER] WHERE [DivisionID] = 10)
    INSERT INTO [dbo].[DIVISION_MASTER] ([DivisionID], [DivisionCode], [DivisionNameEnglish], [DivisionNameKannada], [Location], [IsActive], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (10, N'HVR', N'Haveri', N'à²¹à²¾à²µà³‡à²°à²¿', NULL, 1, N'SYSTEM', '2026-01-08 16:54:20.157', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[DIVISION_MASTER] WHERE [DivisionID] = 11)
    INSERT INTO [dbo].[DIVISION_MASTER] ([DivisionID], [DivisionCode], [DivisionNameEnglish], [DivisionNameKannada], [Location], [IsActive], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (11, N'NKD', N'Uttara Kannada', N'à²‰à²¤à³à²¤à²° à²•à²¨à³à²¨à²¡', NULL, 1, N'SYSTEM', '2026-01-08 16:54:20.157', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[DIVISION_MASTER] WHERE [DivisionID] = 12)
    INSERT INTO [dbo].[DIVISION_MASTER] ([DivisionID], [DivisionCode], [DivisionNameEnglish], [DivisionNameKannada], [Location], [IsActive], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (12, N'RWH', N'Regional Workshop', N'à²ªà³à²°à²¾à²¦à³‡à²¶à²¿à²• à²•à²¾à²°à³à²¯à²¾à²—à²¾à²°', NULL, 1, N'SYSTEM', '2026-01-08 16:54:20.157', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[DIVISION_MASTER] WHERE [DivisionID] = 13)
    INSERT INTO [dbo].[DIVISION_MASTER] ([DivisionID], [DivisionCode], [DivisionNameEnglish], [DivisionNameKannada], [Location], [IsActive], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (13, N'CO', N'Central Office(Lower Courts)', N'ಕೇಂದ್ರ ಕಚೇರಿ ವಿಭಾಗ', N'', 1, N'Admin', '2026-08-06 11:42:27.840', NULL, NULL);
SET IDENTITY_INSERT [dbo].[DIVISION_MASTER] OFF;
GO

-- Seed Data: [USERS]
SET IDENTITY_INSERT [dbo].[USERS] ON;
IF NOT EXISTS (SELECT 1 FROM [dbo].[USERS] WHERE [UserID] = 1)
    INSERT INTO [dbo].[USERS] ([UserID], [Username], [PasswordHash], [FullName], [Email], [Mobile], [RoleID], [DivisionID], [IsActive], [LastLogin], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (1, N'admin', N'8D969EEF6ECAD3C29A3A629280E686CF0C3F5D5A86AFF3CA12020C923ADC6C92', N'System Administrator', N'admin@mvccasemgmt.gov.in', NULL, 1, NULL, 1, '2026-10-07 17:47:18.120', N'SYSTEM', '2026-01-08 16:54:20.167', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[USERS] WHERE [UserID] = 2)
    INSERT INTO [dbo].[USERS] ([UserID], [Username], [PasswordHash], [FullName], [Email], [Mobile], [RoleID], [DivisionID], [IsActive], [LastLogin], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (2, N'LAW_BGK', N'AQAAAAIAAYagAAAAEGbH13diiMOIngsaPYA2+zFKeOziq0C8LeMQZWYhC4Z7VSZapQHduVtTenCL9gShDw==', N'Bagalkot User', NULL, N'7019151971', 2, 1, 1, '2026-09-02 17:13:34.023', N'System', '2026-01-08 17:22:23.620', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[USERS] WHERE [UserID] = 3)
    INSERT INTO [dbo].[USERS] ([UserID], [Username], [PasswordHash], [FullName], [Email], [Mobile], [RoleID], [DivisionID], [IsActive], [LastLogin], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (3, N'LAW_BGM', N'8D969EEF6ECAD3C29A3A629280E686CF0C3F5D5A86AFF3CA12020C923ADC6C92', N'Belagavi User', NULL, NULL, 2, 2, 1, '2026-10-07 17:26:53.077', N'System', '2026-01-08 17:22:23.620', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[USERS] WHERE [UserID] = 5)
    INSERT INTO [dbo].[USERS] ([UserID], [Username], [PasswordHash], [FullName], [Email], [Mobile], [RoleID], [DivisionID], [IsActive], [LastLogin], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (5, N'LAW_CKD', N'8D969EEF6ECAD3C29A3A629280E686CF0C3F5D5A86AFF3CA12020C923ADC6C92', N'Chikkodi User', NULL, NULL, 2, 4, 1, '2026-10-07 15:47:41.240', N'System', '2026-01-08 17:22:23.620', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[USERS] WHERE [UserID] = 6)
    INSERT INTO [dbo].[USERS] ([UserID], [Username], [PasswordHash], [FullName], [Email], [Mobile], [RoleID], [DivisionID], [IsActive], [LastLogin], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (6, N'LAW_COH', N'8D969EEF6ECAD3C29A3A629280E686CF0C3F5D5A86AFF3CA12020C923ADC6C92', N'Central Office User', NULL, NULL, 2, 5, 1, '2026-10-09 11:10:07.637', N'System', '2026-01-08 17:22:23.623', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[USERS] WHERE [UserID] = 7)
    INSERT INTO [dbo].[USERS] ([UserID], [Username], [PasswordHash], [FullName], [Email], [Mobile], [RoleID], [DivisionID], [IsActive], [LastLogin], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (7, N'LAW_DWR', N'8D969EEF6ECAD3C29A3A629280E686CF0C3F5D5A86AFF3CA12020C923ADC6C92', N'Dharwad Rural User', NULL, NULL, 2, 6, 1, '2026-09-18 13:02:41.720', N'System', '2026-01-08 17:22:23.623', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[USERS] WHERE [UserID] = 8)
    INSERT INTO [dbo].[USERS] ([UserID], [Username], [PasswordHash], [FullName], [Email], [Mobile], [RoleID], [DivisionID], [IsActive], [LastLogin], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (8, N'LAW_GDG', N'8D969EEF6ECAD3C29A3A629280E686CF0C3F5D5A86AFF3CA12020C923ADC6C92', N'Gadag User', NULL, NULL, 2, 7, 1, '2026-10-08 15:14:08.520', N'System', '2026-01-08 17:22:23.623', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[USERS] WHERE [UserID] = 9)
    INSERT INTO [dbo].[USERS] ([UserID], [Username], [PasswordHash], [FullName], [Email], [Mobile], [RoleID], [DivisionID], [IsActive], [LastLogin], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (9, N'LAW_HBL', N'AQAAAAIAAYagAAAAEB+rMmXVU7cgfKRulbiK0lrBAm0WiPaDAJj0gou/XBJFFuP2c7qMxl+/dwileePOgQ==', N'Hubballi Rural User', NULL, N'9611035172', 2, 8, 1, '2026-10-08 14:38:04.747', N'System', '2026-01-08 17:22:23.623', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[USERS] WHERE [UserID] = 10)
    INSERT INTO [dbo].[USERS] ([UserID], [Username], [PasswordHash], [FullName], [Email], [Mobile], [RoleID], [DivisionID], [IsActive], [LastLogin], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (10, N'LAW_HDC', N'8D969EEF6ECAD3C29A3A629280E686CF0C3F5D5A86AFF3CA12020C923ADC6C92', N'HD City User', NULL, NULL, 2, 9, 1, '2026-09-30 11:23:02.687', N'System', '2026-01-08 17:22:23.623', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[USERS] WHERE [UserID] = 11)
    INSERT INTO [dbo].[USERS] ([UserID], [Username], [PasswordHash], [FullName], [Email], [Mobile], [RoleID], [DivisionID], [IsActive], [LastLogin], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (11, N'LAW_HVR', N'8D969EEF6ECAD3C29A3A629280E686CF0C3F5D5A86AFF3CA12020C923ADC6C92', N'Haveri User', NULL, NULL, 2, 10, 1, '2026-09-30 11:23:19.030', N'System', '2026-01-08 17:22:23.623', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[USERS] WHERE [UserID] = 12)
    INSERT INTO [dbo].[USERS] ([UserID], [Username], [PasswordHash], [FullName], [Email], [Mobile], [RoleID], [DivisionID], [IsActive], [LastLogin], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (12, N'LAW_NKD', N'8D969EEF6ECAD3C29A3A629280E686CF0C3F5D5A86AFF3CA12020C923ADC6C92', N'Uttara Kannada User', NULL, NULL, 2, 11, 1, '2026-10-08 17:54:28.793', N'System', '2026-01-08 17:22:23.623', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[USERS] WHERE [UserID] = 13)
    INSERT INTO [dbo].[USERS] ([UserID], [Username], [PasswordHash], [FullName], [Email], [Mobile], [RoleID], [DivisionID], [IsActive], [LastLogin], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (13, N'LAW_RWH', N'8D969EEF6ECAD3C29A3A629280E686CF0C3F5D5A86AFF3CA12020C923ADC6C92', N'Regional Workshop User', NULL, NULL, 2, 12, 1, '2026-04-28 09:38:02.700', N'System', '2026-01-08 17:22:23.623', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[USERS] WHERE [UserID] = 14)
    INSERT INTO [dbo].[USERS] ([UserID], [Username], [PasswordHash], [FullName], [Email], [Mobile], [RoleID], [DivisionID], [IsActive], [LastLogin], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (14, N'md', N'AQAAAAIAAYagAAAAEMyazz4O7FuwJaVT+YhfnBOAtDLtz8wheF7YIpw8KsmrkWDojPEgEbiJx6VTFFl1ig==', N'Managing Director', NULL, N'7019151971', 3, 5, 1, '2026-10-08 13:27:25.160', NULL, '2026-02-26 12:43:43.927', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[USERS] WHERE [UserID] = 15)
    INSERT INTO [dbo].[USERS] ([UserID], [Username], [PasswordHash], [FullName], [Email], [Mobile], [RoleID], [DivisionID], [IsActive], [LastLogin], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (15, N'clo', N'AQAAAAIAAYagAAAAELaq13xUDAR2+0TiQXF6ZWzrAgPcKnkjjobTXf5+scejaVEClMa/ofSTGC2w2GnZgg==', N'Chief Law Officer', NULL, N'7019151971', 4, 5, 1, '2026-10-01 13:59:18.580', NULL, '2026-02-26 12:43:43.927', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[USERS] WHERE [UserID] = 16)
    INSERT INTO [dbo].[USERS] ([UserID], [Username], [PasswordHash], [FullName], [Email], [Mobile], [RoleID], [DivisionID], [IsActive], [LastLogin], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (16, N'Law_CO', N'8D969EEF6ECAD3C29A3A629280E686CF0C3F5D5A86AFF3CA12020C923ADC6C92', N'CO User', N'co@example.com', N'9876543210', 2, 13, 1, '2026-08-18 12:25:59.213', N'Admin', '2026-08-06 11:44:54.127', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[USERS] WHERE [UserID] = 17)
    INSERT INTO [dbo].[USERS] ([UserID], [Username], [PasswordHash], [FullName], [Email], [Mobile], [RoleID], [DivisionID], [IsActive], [LastLogin], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (17, N'dy_clo', N'8D969EEF6ECAD3C29A3A629280E686CF0C3F5D5A86AFF3CA12020C923ADC6C92', N'Deputy Chief Law Officer', N'dyclo@nwkrtc.in', N'9876543210', 6, 5, 1, '2026-10-07 11:43:46.147', NULL, '2026-09-21 17:09:29.330', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[USERS] WHERE [UserID] = 18)
    INSERT INTO [dbo].[USERS] ([UserID], [Username], [PasswordHash], [FullName], [Email], [Mobile], [RoleID], [DivisionID], [IsActive], [LastLogin], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (18, N'dyclo', N'8D969EEF6ECAD3C29A3A629280E686CF0C3F5D5A86AFF3CA12020C923ADC6C92', N'Deputy Chief Law Officer', N'dyclo@nwkrtc.in', N'9876543210', 6, 5, 1, '2026-09-22 13:00:25.117', NULL, '2026-09-21 17:09:29.330', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[USERS] WHERE [UserID] = 19)
    INSERT INTO [dbo].[USERS] ([UserID], [Username], [PasswordHash], [FullName], [Email], [Mobile], [RoleID], [DivisionID], [IsActive], [LastLogin], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (19, N'lo', N'8D969EEF6ECAD3C29A3A629280E686CF0C3F5D5A86AFF3CA12020C923ADC6C92', N'Law Officer', N'lo@nwkrtc.in', N'9876543211', 5, 5, 1, '2026-09-23 14:31:17.423', NULL, '2026-09-21 17:09:29.330', NULL, NULL);
SET IDENTITY_INSERT [dbo].[USERS] OFF;
GO

-- Seed Data: [CASE_STATUS_MASTER]
SET IDENTITY_INSERT [dbo].[CASE_STATUS_MASTER] ON;
IF NOT EXISTS (SELECT 1 FROM [dbo].[CASE_STATUS_MASTER] WHERE [StatusID] = 1)
    INSERT INTO [dbo].[CASE_STATUS_MASTER] ([StatusID], [StatusCode], [StatusName], [DisplayOrder], [IsActive], [CreatedBy], [CreatedDate]) VALUES (1, N'PND', N'Pending', 1, 1, NULL, '2026-01-08 16:54:20.153');
IF NOT EXISTS (SELECT 1 FROM [dbo].[CASE_STATUS_MASTER] WHERE [StatusID] = 2)
    INSERT INTO [dbo].[CASE_STATUS_MASTER] ([StatusID], [StatusCode], [StatusName], [DisplayOrder], [IsActive], [CreatedBy], [CreatedDate]) VALUES (2, N'INP', N'In Progress', 2, 1, NULL, '2026-01-08 16:54:20.153');
IF NOT EXISTS (SELECT 1 FROM [dbo].[CASE_STATUS_MASTER] WHERE [StatusID] = 3)
    INSERT INTO [dbo].[CASE_STATUS_MASTER] ([StatusID], [StatusCode], [StatusName], [DisplayOrder], [IsActive], [CreatedBy], [CreatedDate]) VALUES (3, N'DSP', N'Disposed', 3, 1, NULL, '2026-01-08 16:54:20.153');
IF NOT EXISTS (SELECT 1 FROM [dbo].[CASE_STATUS_MASTER] WHERE [StatusID] = 4)
    INSERT INTO [dbo].[CASE_STATUS_MASTER] ([StatusID], [StatusCode], [StatusName], [DisplayOrder], [IsActive], [CreatedBy], [CreatedDate]) VALUES (4, N'CLO', N'Closed', 4, 1, NULL, '2026-01-08 16:54:20.153');
SET IDENTITY_INSERT [dbo].[CASE_STATUS_MASTER] OFF;
GO

-- Seed Data: [MACT_MASTER]
SET IDENTITY_INSERT [dbo].[MACT_MASTER] ON;
IF NOT EXISTS (SELECT 1 FROM [dbo].[MACT_MASTER] WHERE [MACTID] = 1)
    INSERT INTO [dbo].[MACT_MASTER] ([MACTID], [MACTCode], [MACTName], [Location], [IsActive], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (1, N'KABK01', N'Principal District & Sessions Court, Bagalkot [KABK01]', N'Bagalkot', 1, N'SYSTEM', '2026-01-08 17:01:39.810', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[MACT_MASTER] WHERE [MACTID] = 2)
    INSERT INTO [dbo].[MACT_MASTER] ([MACTID], [MACTCode], [MACTName], [Location], [IsActive], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (2, N'KABG01', N'Principal District & Sessions Court, Belagavi [KABG01]', N'Belagavi', 1, N'SYSTEM', '2026-01-08 17:01:39.810', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[MACT_MASTER] WHERE [MACTID] = 3)
    INSERT INTO [dbo].[MACT_MASTER] ([MACTID], [MACTCode], [MACTName], [Location], [IsActive], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (3, N'MACT-CKD', N'Senior Civil Judge & MACT, Chikkodi', N'Chikkodi', 1, N'SYSTEM', '2026-01-08 17:01:39.810', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[MACT_MASTER] WHERE [MACTID] = 4)
    INSERT INTO [dbo].[MACT_MASTER] ([MACTID], [MACTCode], [MACTName], [Location], [IsActive], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (4, N'KAHC02', N'High Court of Karnataka, Dharwad Bench [KAHC02]', N'Dharwad', 1, N'SYSTEM', '2026-01-08 17:01:39.810', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[MACT_MASTER] WHERE [MACTID] = 5)
    INSERT INTO [dbo].[MACT_MASTER] ([MACTID], [MACTCode], [MACTName], [Location], [IsActive], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (5, N'KAGD01', N'Principal District & Sessions Court, Gadag [KAGD01]', N'Gadag', 1, N'SYSTEM', '2026-01-08 17:01:39.810', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[MACT_MASTER] WHERE [MACTID] = 6)
    INSERT INTO [dbo].[MACT_MASTER] ([MACTID], [MACTCode], [MACTName], [Location], [IsActive], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (6, N'KADW02', N'Addl Senior Civil Judge & JMFC, Hubballi [KADW02]', N'Hubballi', 1, N'SYSTEM', '2026-01-08 17:01:39.810', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[MACT_MASTER] WHERE [MACTID] = 7)
    INSERT INTO [dbo].[MACT_MASTER] ([MACTID], [MACTCode], [MACTName], [Location], [IsActive], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (7, N'KAHA01', N'Principal District & Sessions Court, Haveri [KAHA01]', N'Haveri', 1, N'SYSTEM', '2026-01-08 17:01:39.810', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[MACT_MASTER] WHERE [MACTID] = 8)
    INSERT INTO [dbo].[MACT_MASTER] ([MACTID], [MACTCode], [MACTName], [Location], [IsActive], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (8, N'KAUK01', N'Principal District & Sessions Court, Karwar [KAUK01]', N'Karwar', 1, N'SYSTEM', '2026-01-08 17:01:39.810', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[MACT_MASTER] WHERE [MACTID] = 9)
    INSERT INTO [dbo].[MACT_MASTER] ([MACTID], [MACTCode], [MACTName], [Location], [IsActive], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (9, N'KAUKA2', N'SENIOR CIVIL JUDGE AND PRL. JMFC, SIRSI', N'SIRSI', 1, N'SYSTEM', '2026-01-08 17:01:39.810', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[MACT_MASTER] WHERE [MACTID] = 10)
    INSERT INTO [dbo].[MACT_MASTER] ([MACTID], [MACTCode], [MACTName], [Location], [IsActive], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (10, N'MACT-GKK', N'Senior Civil Judge, Gokak', N'Gokak', 1, N'SYSTEM', '2026-01-08 17:01:39.810', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[MACT_MASTER] WHERE [MACTID] = 11)
    INSERT INTO [dbo].[MACT_MASTER] ([MACTID], [MACTCode], [MACTName], [Location], [IsActive], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (11, N'MACT-ATN', N'Senior Civil Judge, Athani', N'Athani', 1, N'SYSTEM', '2026-01-08 17:01:39.810', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[MACT_MASTER] WHERE [MACTID] = 12)
    INSERT INTO [dbo].[MACT_MASTER] ([MACTID], [MACTCode], [MACTName], [Location], [IsActive], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (12, N'MACT-HGL', N'Senior Civil Judge, Hanagal', N'Hanagal', 1, N'SYSTEM', '2026-01-08 17:01:39.810', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[MACT_MASTER] WHERE [MACTID] = 13)
    INSERT INTO [dbo].[MACT_MASTER] ([MACTID], [MACTCode], [MACTName], [Location], [IsActive], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (13, N'KAHC01', N'High Court of Karnataka, Principal Bench Bengaluru [KAHC01]', N'Bengaluru', 1, NULL, '2026-01-14 15:27:17.170', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[MACT_MASTER] WHERE [MACTID] = 14)
    INSERT INTO [dbo].[MACT_MASTER] ([MACTID], [MACTCode], [MACTName], [Location], [IsActive], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (14, N'M7465', N'Judge, Court of Small Causes & MACT (SCCH-1), Bengaluru City', N'Bengaluru City', 1, NULL, '2026-01-14 15:27:17.170', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[MACT_MASTER] WHERE [MACTID] = 15)
    INSERT INTO [dbo].[MACT_MASTER] ([MACTID], [MACTCode], [MACTName], [Location], [IsActive], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (15, N'M7742', N'Judge, Court of Small Causes & MACT (SCCH-2), Bengaluru City', N'Bengaluru City', 1, NULL, '2026-01-14 15:27:17.170', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[MACT_MASTER] WHERE [MACTID] = 16)
    INSERT INTO [dbo].[MACT_MASTER] ([MACTID], [MACTCode], [MACTName], [Location], [IsActive], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (16, N'M2096', N'Judge, Court of Small Causes & MACT (SCCH-3), Bengaluru City', N'Bengaluru City', 1, NULL, '2026-01-14 15:27:17.170', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[MACT_MASTER] WHERE [MACTID] = 17)
    INSERT INTO [dbo].[MACT_MASTER] ([MACTID], [MACTCode], [MACTName], [Location], [IsActive], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (17, N'M3936', N'Judge, Court of Small Causes & MACT (SCCH-4), Bengaluru City', N'Bengaluru City', 1, NULL, '2026-01-14 15:27:17.170', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[MACT_MASTER] WHERE [MACTID] = 18)
    INSERT INTO [dbo].[MACT_MASTER] ([MACTID], [MACTCode], [MACTName], [Location], [IsActive], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (18, N'M1958', N'Prl. District & Sessions Judge & MACT, Bengaluru Rural', N'Bengaluru Rural', 1, NULL, '2026-01-14 15:27:17.170', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[MACT_MASTER] WHERE [MACTID] = 19)
    INSERT INTO [dbo].[MACT_MASTER] ([MACTID], [MACTCode], [MACTName], [Location], [IsActive], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (19, N'M4343', N'Prl. Senior Civil Judge & MACT, Devanahalli', N'Devanahalli', 1, NULL, '2026-01-14 15:27:17.170', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[MACT_MASTER] WHERE [MACTID] = 20)
    INSERT INTO [dbo].[MACT_MASTER] ([MACTID], [MACTCode], [MACTName], [Location], [IsActive], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (20, N'M8070', N'Prl. Senior Civil Judge & MACT, Doddaballapura', N'Doddaballapura', 1, NULL, '2026-01-14 15:27:17.170', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[MACT_MASTER] WHERE [MACTID] = 21)
    INSERT INTO [dbo].[MACT_MASTER] ([MACTID], [MACTCode], [MACTName], [Location], [IsActive], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (21, N'M6921', N'Senior Civil Judge & MACT, Nelamangala', N'Nelamangala', 1, NULL, '2026-01-14 15:27:17.170', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[MACT_MASTER] WHERE [MACTID] = 22)
    INSERT INTO [dbo].[MACT_MASTER] ([MACTID], [MACTCode], [MACTName], [Location], [IsActive], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (22, N'M7146', N'Prl. District & Sessions Judge & MACT, Ramanagara', N'Ramanagara', 1, NULL, '2026-01-14 15:27:17.170', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[MACT_MASTER] WHERE [MACTID] = 23)
    INSERT INTO [dbo].[MACT_MASTER] ([MACTID], [MACTCode], [MACTName], [Location], [IsActive], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (23, N'M7412', N'Senior Civil Judge & MACT, Kanakapura', N'Kanakapura', 1, NULL, '2026-01-14 15:27:17.170', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[MACT_MASTER] WHERE [MACTID] = 24)
    INSERT INTO [dbo].[MACT_MASTER] ([MACTID], [MACTCode], [MACTName], [Location], [IsActive], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (24, N'M5536', N'Prl. District & Sessions Judge & MACT, Mysuru', N'Mysuru', 1, NULL, '2026-01-14 15:27:17.170', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[MACT_MASTER] WHERE [MACTID] = 25)
    INSERT INTO [dbo].[MACT_MASTER] ([MACTID], [MACTCode], [MACTName], [Location], [IsActive], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (25, N'M3674', N'Prl. District & Sessions Judge & MACT, Mandya', N'Mandya', 1, NULL, '2026-01-14 15:27:17.170', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[MACT_MASTER] WHERE [MACTID] = 26)
    INSERT INTO [dbo].[MACT_MASTER] ([MACTID], [MACTCode], [MACTName], [Location], [IsActive], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (26, N'M2089', N'Prl. District & Sessions Judge & MACT, Hassan', N'Hassan', 1, NULL, '2026-01-14 15:27:17.170', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[MACT_MASTER] WHERE [MACTID] = 27)
    INSERT INTO [dbo].[MACT_MASTER] ([MACTID], [MACTCode], [MACTName], [Location], [IsActive], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (27, N'M9341', N'Senior Civil Judge & MACT, Sakleshpur', N'Sakleshpur', 1, NULL, '2026-01-14 15:27:17.170', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[MACT_MASTER] WHERE [MACTID] = 28)
    INSERT INTO [dbo].[MACT_MASTER] ([MACTID], [MACTCode], [MACTName], [Location], [IsActive], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (28, N'M2025', N'Prl. District & Sessions Judge & MACT, Chikkamagaluru', N'Chikkamagaluru', 1, NULL, '2026-01-14 15:27:17.170', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[MACT_MASTER] WHERE [MACTID] = 29)
    INSERT INTO [dbo].[MACT_MASTER] ([MACTID], [MACTCode], [MACTName], [Location], [IsActive], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (29, N'M1906', N'Prl. District & Sessions Judge & MACT, Tumakuru', N'Tumakuru', 1, NULL, '2026-01-14 15:27:17.170', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[MACT_MASTER] WHERE [MACTID] = 30)
    INSERT INTO [dbo].[MACT_MASTER] ([MACTID], [MACTCode], [MACTName], [Location], [IsActive], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (30, N'M3377', N'Prl. District & Sessions Judge & MACT, Kolar', N'Kolar', 1, NULL, '2026-01-14 15:27:17.170', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[MACT_MASTER] WHERE [MACTID] = 31)
    INSERT INTO [dbo].[MACT_MASTER] ([MACTID], [MACTCode], [MACTName], [Location], [IsActive], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (31, N'M4722', N'Prl. District & Sessions Judge & MACT, Chikkaballapura', N'Chikkaballapura', 1, NULL, '2026-01-14 15:27:17.170', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[MACT_MASTER] WHERE [MACTID] = 32)
    INSERT INTO [dbo].[MACT_MASTER] ([MACTID], [MACTCode], [MACTName], [Location], [IsActive], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (32, N'M3505', N'Prl. District & Sessions Judge & MACT, Davanagere', N'Davanagere', 1, NULL, '2026-01-14 15:27:17.170', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[MACT_MASTER] WHERE [MACTID] = 33)
    INSERT INTO [dbo].[MACT_MASTER] ([MACTID], [MACTCode], [MACTName], [Location], [IsActive], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (33, N'M2399', N'Senior Civil Judge & MACT, Channagiri', N'Channagiri', 1, NULL, '2026-01-14 15:27:17.170', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[MACT_MASTER] WHERE [MACTID] = 34)
    INSERT INTO [dbo].[MACT_MASTER] ([MACTID], [MACTCode], [MACTName], [Location], [IsActive], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (34, N'M2471', N'Senior Civil Judge & MACT, Jagalur', N'Jagalur', 1, NULL, '2026-01-14 15:27:17.170', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[MACT_MASTER] WHERE [MACTID] = 35)
    INSERT INTO [dbo].[MACT_MASTER] ([MACTID], [MACTCode], [MACTName], [Location], [IsActive], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (35, N'M3374', N'Prl. District & Sessions Judge & MACT, Shivamogga', N'Shivamogga', 1, NULL, '2026-01-14 15:27:17.170', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[MACT_MASTER] WHERE [MACTID] = 36)
    INSERT INTO [dbo].[MACT_MASTER] ([MACTID], [MACTCode], [MACTName], [Location], [IsActive], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (36, N'M1550', N'Prl. District & Sessions Judge & MACT, Ballari', N'Ballari', 1, NULL, '2026-01-14 15:27:17.170', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[MACT_MASTER] WHERE [MACTID] = 37)
    INSERT INTO [dbo].[MACT_MASTER] ([MACTID], [MACTCode], [MACTName], [Location], [IsActive], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (37, N'M5732', N'Senior Civil Judge & MACT, Sandur', N'Sandur', 1, NULL, '2026-01-14 15:27:17.170', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[MACT_MASTER] WHERE [MACTID] = 38)
    INSERT INTO [dbo].[MACT_MASTER] ([MACTID], [MACTCode], [MACTName], [Location], [IsActive], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (38, N'M9489', N'Prl. District & Sessions Judge & MACT, Koppal', N'Koppal', 1, NULL, '2026-01-14 15:27:17.170', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[MACT_MASTER] WHERE [MACTID] = 39)
    INSERT INTO [dbo].[MACT_MASTER] ([MACTID], [MACTCode], [MACTName], [Location], [IsActive], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (39, N'M5991', N'Prl. District & Sessions Judge & MACT, Raichur', N'Raichur', 1, NULL, '2026-01-14 15:27:17.170', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[MACT_MASTER] WHERE [MACTID] = 40)
    INSERT INTO [dbo].[MACT_MASTER] ([MACTID], [MACTCode], [MACTName], [Location], [IsActive], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (40, N'KAHC03', N'High Court of Karnataka, Kalaburagi Bench [KAHC03]', N'Kalaburagi', 1, NULL, '2026-01-14 15:27:17.170', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[MACT_MASTER] WHERE [MACTID] = 41)
    INSERT INTO [dbo].[MACT_MASTER] ([MACTID], [MACTCode], [MACTName], [Location], [IsActive], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (41, N'M3570', N'Prl. District & Sessions Judge & MACT, Bidar', N'Bidar', 1, NULL, '2026-01-14 15:27:17.170', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[MACT_MASTER] WHERE [MACTID] = 42)
    INSERT INTO [dbo].[MACT_MASTER] ([MACTID], [MACTCode], [MACTName], [Location], [IsActive], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (42, N'KABJ01', N'Principal District & Sessions Court, Vijayapura [KABJ01]', N'Vijayapura', 1, NULL, '2026-01-14 15:27:17.170', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[MACT_MASTER] WHERE [MACTID] = 43)
    INSERT INTO [dbo].[MACT_MASTER] ([MACTID], [MACTCode], [MACTName], [Location], [IsActive], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (43, N'M6561', N'Prl. District & Sessions Judge & MACT, Yadgir', N'Yadgir', 1, NULL, '2026-01-14 15:27:17.170', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[MACT_MASTER] WHERE [MACTID] = 44)
    INSERT INTO [dbo].[MACT_MASTER] ([MACTID], [MACTCode], [MACTName], [Location], [IsActive], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (44, N'M5009', N'Prl. District & Sessions Judge & MACT, Chitradurga', N'Chitradurga', 1, NULL, '2026-01-14 15:27:17.170', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[MACT_MASTER] WHERE [MACTID] = 45)
    INSERT INTO [dbo].[MACT_MASTER] ([MACTID], [MACTCode], [MACTName], [Location], [IsActive], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (45, N'M1700', N'Prl. District & Sessions Judge & MACT, Kodagu (Madikeri)', N'Kodagu (Madikeri)', 1, NULL, '2026-01-14 15:27:17.170', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[MACT_MASTER] WHERE [MACTID] = 46)
    INSERT INTO [dbo].[MACT_MASTER] ([MACTID], [MACTCode], [MACTName], [Location], [IsActive], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (46, N'M3568', N'Prl. District & Sessions Judge & MACT, Dakshina Kannada (Mangaluru)', N'Dakshina Kannada (Mangaluru)', 1, NULL, '2026-01-14 15:27:17.170', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[MACT_MASTER] WHERE [MACTID] = 47)
    INSERT INTO [dbo].[MACT_MASTER] ([MACTID], [MACTCode], [MACTName], [Location], [IsActive], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (47, N'M6871', N'Senior Civil Judge & MACT, Bantwal', N'Bantwal', 1, NULL, '2026-01-14 15:27:17.170', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[MACT_MASTER] WHERE [MACTID] = 48)
    INSERT INTO [dbo].[MACT_MASTER] ([MACTID], [MACTCode], [MACTName], [Location], [IsActive], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (48, N'M4417', N'Prl. District & Sessions Judge & MACT, Udupi', N'Udupi', 1, NULL, '2026-01-14 15:27:17.170', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[MACT_MASTER] WHERE [MACTID] = 49)
    INSERT INTO [dbo].[MACT_MASTER] ([MACTID], [MACTCode], [MACTName], [Location], [IsActive], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (49, N'M8860', N'Prl. District & Sessions Judge & MACT, Chamarajanagara', N'Chamarajanagara', 1, NULL, '2026-01-14 15:27:17.170', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[MACT_MASTER] WHERE [MACTID] = 50)
    INSERT INTO [dbo].[MACT_MASTER] ([MACTID], [MACTCode], [MACTName], [Location], [IsActive], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (50, N'M4575', N'Prl. District & Sessions Judge & MACT, Gadag', N'Gadag', 1, NULL, '2026-01-14 15:27:17.170', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[MACT_MASTER] WHERE [MACTID] = 51)
    INSERT INTO [dbo].[MACT_MASTER] ([MACTID], [MACTCode], [MACTName], [Location], [IsActive], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (51, N'M8856', N'PRL. SENIOR CIVIL JUDGE AND CJM COURT, VIJAYAPURA', N'VIJAYAPURA', 1, NULL, '2026-01-22 13:23:26.837', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[MACT_MASTER] WHERE [MACTID] = 52)
    INSERT INTO [dbo].[MACT_MASTER] ([MACTID], [MACTCode], [MACTName], [Location], [IsActive], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (52, N'M6679', N'SENIOR CIVIL JUDGE & MACT ANKOLA', N'ANKOLA', 1, NULL, '2026-01-28 11:50:57.133', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[MACT_MASTER] WHERE [MACTID] = 53)
    INSERT INTO [dbo].[MACT_MASTER] ([MACTID], [MACTCode], [MACTName], [Location], [IsActive], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (53, N'M7914', N'PRL. DISTRICT & SESSION JUDGE & MACT KARWAR', N'KARWAR', 1, NULL, '2026-01-28 11:51:52.890', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[MACT_MASTER] WHERE [MACTID] = 54)
    INSERT INTO [dbo].[MACT_MASTER] ([MACTID], [MACTCode], [MACTName], [Location], [IsActive], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (54, N'M1943', N'SENIOR CIVIL JUDGE & MACT KUMTA', N'KUMTA', 1, NULL, '2026-01-28 11:52:19.687', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[MACT_MASTER] WHERE [MACTID] = 55)
    INSERT INTO [dbo].[MACT_MASTER] ([MACTID], [MACTCode], [MACTName], [Location], [IsActive], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (55, N'M5510', N'SENIOR CIVIL JUDGE & MACT BHATKAL', N'BHATKAL', 1, NULL, '2026-01-28 11:52:54.063', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[MACT_MASTER] WHERE [MACTID] = 56)
    INSERT INTO [dbo].[MACT_MASTER] ([MACTID], [MACTCode], [MACTName], [Location], [IsActive], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (56, N'M4376', N'SENIOR CIVIL JUDGE & MACT YALLAPUR', N'YALLAPUR', 1, NULL, '2026-01-28 11:53:18.657', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[MACT_MASTER] WHERE [MACTID] = 57)
    INSERT INTO [dbo].[MACT_MASTER] ([MACTID], [MACTCode], [MACTName], [Location], [IsActive], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (57, N'M8400', N'SENIOR CIVIL JUDGE & MACT HONNAVAR', N'HONNAVAR', 1, NULL, '2026-01-28 11:53:50.830', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[MACT_MASTER] WHERE [MACTID] = 58)
    INSERT INTO [dbo].[MACT_MASTER] ([MACTID], [MACTCode], [MACTName], [Location], [IsActive], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (58, N'M2013', N'SENIOR CIVIL JUDGE & MACT SIRSI', N'SIRSI', 1, NULL, '2026-01-28 11:54:09.783', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[MACT_MASTER] WHERE [MACTID] = 59)
    INSERT INTO [dbo].[MACT_MASTER] ([MACTID], [MACTCode], [MACTName], [Location], [IsActive], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (59, N'M4526', N'SENIOR CIVIL JUDGE & MACT MUNDGOD', N'MUNDGOD', 1, NULL, '2026-01-28 11:54:54.707', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[MACT_MASTER] WHERE [MACTID] = 60)
    INSERT INTO [dbo].[MACT_MASTER] ([MACTID], [MACTCode], [MACTName], [Location], [IsActive], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (60, N'M4071', N'SENIOR CIVIL JUDGE & MACT NAVALAGUND', N'NAVALAGUND', 1, NULL, '2026-01-28 12:06:42.160', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[MACT_MASTER] WHERE [MACTID] = 61)
    INSERT INTO [dbo].[MACT_MASTER] ([MACTID], [MACTCode], [MACTName], [Location], [IsActive], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (61, N'M4225', N'PRL. DISTRICT & SESSION JUDGE & MACT HUBBALLI', N'HUBLI', 1, NULL, '2026-01-28 12:07:29.410', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[MACT_MASTER] WHERE [MACTID] = 62)
    INSERT INTO [dbo].[MACT_MASTER] ([MACTID], [MACTCode], [MACTName], [Location], [IsActive], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (62, N'M7632', N'PRL. DISTRICT & SESSION JUDGE & MACT DHARWAD', N'DHARWAD', 1, NULL, '2026-01-28 12:08:02.943', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[MACT_MASTER] WHERE [MACTID] = 63)
    INSERT INTO [dbo].[MACT_MASTER] ([MACTID], [MACTCode], [MACTName], [Location], [IsActive], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (63, N'M7587', N'SENIOR CIVIL JUDGE & MACT KALAGHATAGI', N'KALAGHATAGI', 1, NULL, '2026-01-28 12:08:35.617', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[MACT_MASTER] WHERE [MACTID] = 64)
    INSERT INTO [dbo].[MACT_MASTER] ([MACTID], [MACTCode], [MACTName], [Location], [IsActive], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (64, N'M7253', N'SENIOR CIVIL JUDGE & MACT HAVERI', N'HAVERI', 1, NULL, '2026-01-28 12:10:20.880', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[MACT_MASTER] WHERE [MACTID] = 65)
    INSERT INTO [dbo].[MACT_MASTER] ([MACTID], [MACTCode], [MACTName], [Location], [IsActive], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (65, N'M2345', N'SENIOR CIVIL JUDGE & MACT RANEBENNUR', N'RANEBENNUR', 1, NULL, '2026-01-28 12:10:39.020', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[MACT_MASTER] WHERE [MACTID] = 66)
    INSERT INTO [dbo].[MACT_MASTER] ([MACTID], [MACTCode], [MACTName], [Location], [IsActive], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (66, N'M3269', N'SENIOR CIVIL JUDGE & MACT SAVANUR', N'SAVANUR', 1, NULL, '2026-01-28 12:11:11.990', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[MACT_MASTER] WHERE [MACTID] = 67)
    INSERT INTO [dbo].[MACT_MASTER] ([MACTID], [MACTCode], [MACTName], [Location], [IsActive], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (67, N'M8786', N'SENIOR CIVIL JUDGE & MACT BADAMI', N'BADAMI', 1, NULL, '2026-01-28 12:12:09.697', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[MACT_MASTER] WHERE [MACTID] = 68)
    INSERT INTO [dbo].[MACT_MASTER] ([MACTID], [MACTCode], [MACTName], [Location], [IsActive], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (68, N'M9035', N'SENIOR CIVIL JUDGE & MACT SINDANOOR', N'SINDANOOR', 1, NULL, '2026-01-28 12:12:41.743', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[MACT_MASTER] WHERE [MACTID] = 69)
    INSERT INTO [dbo].[MACT_MASTER] ([MACTID], [MACTCode], [MACTName], [Location], [IsActive], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (69, N'M7725', N'SENIOR CIVIL JUDGE & MACT BELAGAVI', N'BELAGAVI', 1, NULL, '2026-01-28 12:13:22.290', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[MACT_MASTER] WHERE [MACTID] = 70)
    INSERT INTO [dbo].[MACT_MASTER] ([MACTID], [MACTCode], [MACTName], [Location], [IsActive], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (70, N'M4265', N'SENIOR CIVIL JUDGE & MACT KHANAPUR', N'KHANAPUR', 1, NULL, '2026-01-28 12:14:01.730', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[MACT_MASTER] WHERE [MACTID] = 71)
    INSERT INTO [dbo].[MACT_MASTER] ([MACTID], [MACTCode], [MACTName], [Location], [IsActive], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (71, N'M4888', N'SENIOR CIVIL JUDGE & MACT DAVANAGERI', N'DAVANAGERI', 1, NULL, '2026-01-28 12:14:49.250', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[MACT_MASTER] WHERE [MACTID] = 72)
    INSERT INTO [dbo].[MACT_MASTER] ([MACTID], [MACTCode], [MACTName], [Location], [IsActive], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (72, N'M9895', N'SENIOR CIVIL JUDGE & MACT ARASIKERI', N'ARASIKERI', 1, NULL, '2026-01-28 12:15:17.047', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[MACT_MASTER] WHERE [MACTID] = 73)
    INSERT INTO [dbo].[MACT_MASTER] ([MACTID], [MACTCode], [MACTName], [Location], [IsActive], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (73, N'M2867', N'SENIOR CIVIL JUDGE & MACT BALLARY', N'BALLARY', 1, NULL, '2026-01-28 12:15:38.153', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[MACT_MASTER] WHERE [MACTID] = 74)
    INSERT INTO [dbo].[MACT_MASTER] ([MACTID], [MACTCode], [MACTName], [Location], [IsActive], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (74, N'M1571', N'SENIOR CIVIL JUDGE & MACT SAGAR', N'SAGAR', 1, NULL, '2026-01-28 12:15:51.937', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[MACT_MASTER] WHERE [MACTID] = 75)
    INSERT INTO [dbo].[MACT_MASTER] ([MACTID], [MACTCode], [MACTName], [Location], [IsActive], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (75, N'M5405', N'SENIOR CIVIL JUDGE & MACT SORAB', N'SORAB', 1, NULL, '2026-01-28 12:16:01.843', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[MACT_MASTER] WHERE [MACTID] = 76)
    INSERT INTO [dbo].[MACT_MASTER] ([MACTID], [MACTCode], [MACTName], [Location], [IsActive], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (76, N'M3077', N'SENIOR CIVIL JUDGE & MACT SHIVAMOGGA', N'SHIVAMOGGA', 1, NULL, '2026-01-28 12:16:25.577', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[MACT_MASTER] WHERE [MACTID] = 77)
    INSERT INTO [dbo].[MACT_MASTER] ([MACTID], [MACTCode], [MACTName], [Location], [IsActive], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (77, N'M4646', N'SENIOR CIVIL JUDGE & MACT HOSANAGAR', N'HOSANAGAR', 1, NULL, '2026-01-28 12:16:42.750', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[MACT_MASTER] WHERE [MACTID] = 78)
    INSERT INTO [dbo].[MACT_MASTER] ([MACTID], [MACTCode], [MACTName], [Location], [IsActive], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (78, N'M4537', N'SENIOR CIVIL JUDGE & MACT CHIKKAMANGALURU', N'CHIKKAMANGALURU', 1, NULL, '2026-01-28 12:17:07.940', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[MACT_MASTER] WHERE [MACTID] = 79)
    INSERT INTO [dbo].[MACT_MASTER] ([MACTID], [MACTCode], [MACTName], [Location], [IsActive], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (79, N'M7205', N'SENIOR CIVIL JUDGE & MACT K R PETE', N'K R PETE', 1, NULL, '2026-01-28 12:17:24.190', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[MACT_MASTER] WHERE [MACTID] = 80)
    INSERT INTO [dbo].[MACT_MASTER] ([MACTID], [MACTCode], [MACTName], [Location], [IsActive], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (80, N'M8118', N'SENIOR CIVIL JUDGE & MACT UDUPI', N'UDUPI', 1, NULL, '2026-01-28 12:17:37.610', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[MACT_MASTER] WHERE [MACTID] = 81)
    INSERT INTO [dbo].[MACT_MASTER] ([MACTID], [MACTCode], [MACTName], [Location], [IsActive], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (81, N'M2171', N'SENIOR CIVIL JUDGE & MACT UDUPI', N'UDUPI', 1, NULL, '2026-01-28 12:17:38.017', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[MACT_MASTER] WHERE [MACTID] = 82)
    INSERT INTO [dbo].[MACT_MASTER] ([MACTID], [MACTCode], [MACTName], [Location], [IsActive], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (82, N'M9618', N'SENIOR CIVIL JUDGE & MACT KUNDAPUR', N'KUNDAPUR', 1, NULL, '2026-01-28 12:17:54.550', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[MACT_MASTER] WHERE [MACTID] = 83)
    INSERT INTO [dbo].[MACT_MASTER] ([MACTID], [MACTCode], [MACTName], [Location], [IsActive], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (83, N'M8596', N'SENIOR CIVIL JUDGE & MACT MYSURU', N'MYSURU', 1, NULL, '2026-01-28 12:18:13.817', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[MACT_MASTER] WHERE [MACTID] = 84)
    INSERT INTO [dbo].[MACT_MASTER] ([MACTID], [MACTCode], [MACTName], [Location], [IsActive], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (84, N'M1974', N'SENIOR CIVIL JUDGE & MACT BIDAR', N'BIDAR', 1, NULL, '2026-01-28 12:18:36.987', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[MACT_MASTER] WHERE [MACTID] = 85)
    INSERT INTO [dbo].[MACT_MASTER] ([MACTID], [MACTCode], [MACTName], [Location], [IsActive], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (85, N'M9762', N'SENIOR CIVIL JUDGE & MACT KADUR', N'KADUR', 1, NULL, '2026-01-28 12:19:07.253', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[MACT_MASTER] WHERE [MACTID] = 86)
    INSERT INTO [dbo].[MACT_MASTER] ([MACTID], [MACTCode], [MACTName], [Location], [IsActive], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (86, N'M3096', N'SENIOR CIVIL JUDGE & PRL. JMFC YELLAPUR', N'YELLAPUR', 1, NULL, '2026-01-28 12:20:02.130', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[MACT_MASTER] WHERE [MACTID] = 87)
    INSERT INTO [dbo].[MACT_MASTER] ([MACTID], [MACTCode], [MACTName], [Location], [IsActive], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (87, N'M4008', N'SENIOR CIVIL JUDGE & MACT HALIYAL', N'HALIYAL', 1, NULL, '2026-01-28 12:20:17.913', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[MACT_MASTER] WHERE [MACTID] = 88)
    INSERT INTO [dbo].[MACT_MASTER] ([MACTID], [MACTCode], [MACTName], [Location], [IsActive], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (88, N'M8918', N'PRL. SENIOR CIVIL JUDGE AND JMFC RANEBENNUR', N'RANEBENNUR', 1, NULL, '2026-01-28 12:20:57.040', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[MACT_MASTER] WHERE [MACTID] = 89)
    INSERT INTO [dbo].[MACT_MASTER] ([MACTID], [MACTCode], [MACTName], [Location], [IsActive], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (89, N'M3937', N'SENIOR CIVIL JUDGE & JMFC MUDDEBIHAL', N'MUDDEBIHAL', 1, NULL, '2026-01-28 12:21:21.197', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[MACT_MASTER] WHERE [MACTID] = 90)
    INSERT INTO [dbo].[MACT_MASTER] ([MACTID], [MACTCode], [MACTName], [Location], [IsActive], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (90, N'M8466', N'SENIOR CIVIL JUDGE & MACT MULBAGAL', N'MULBAGAL', 1, NULL, '2026-01-28 12:21:47.600', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[MACT_MASTER] WHERE [MACTID] = 91)
    INSERT INTO [dbo].[MACT_MASTER] ([MACTID], [MACTCode], [MACTName], [Location], [IsActive], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (91, N'M3909', N'DISTRICT & SESSION JUDGE & MACT BAGALKOT', N'BAGALKOT', 1, NULL, '2026-01-28 12:25:49.640', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[MACT_MASTER] WHERE [MACTID] = 92)
    INSERT INTO [dbo].[MACT_MASTER] ([MACTID], [MACTCode], [MACTName], [Location], [IsActive], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (92, N'M1747', N'SENIOR CIVIL JUDGE & MACT HUNGUND', N'HUNGUND', 1, NULL, '2026-01-28 12:26:32.567', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[MACT_MASTER] WHERE [MACTID] = 93)
    INSERT INTO [dbo].[MACT_MASTER] ([MACTID], [MACTCode], [MACTName], [Location], [IsActive], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (93, N'M9966', N'SENIOR CIVIL JUDGE & MACT BILAGI', N'BILAGI', 1, NULL, '2026-01-28 12:26:54.033', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[MACT_MASTER] WHERE [MACTID] = 94)
    INSERT INTO [dbo].[MACT_MASTER] ([MACTID], [MACTCode], [MACTName], [Location], [IsActive], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (94, N'M1914', N'PRL. SENIOR CIVIL JUDGE & MACT JAMAKHANDI', N'JAMAKHANDI', 1, NULL, '2026-01-28 12:27:59.460', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[MACT_MASTER] WHERE [MACTID] = 95)
    INSERT INTO [dbo].[MACT_MASTER] ([MACTID], [MACTCode], [MACTName], [Location], [IsActive], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (95, N'M2717', N'SENIOR CIVIL JUDGE & MACT JAMAKHANDI', N'JAMAKHANDI', 1, NULL, '2026-01-28 12:28:14.553', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[MACT_MASTER] WHERE [MACTID] = 96)
    INSERT INTO [dbo].[MACT_MASTER] ([MACTID], [MACTCode], [MACTName], [Location], [IsActive], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (96, N'M7501', N'SENIOR CIVIL JUDGE & MACT MUDHOL', N'MUDHOL', 1, NULL, '2026-01-28 12:28:42.070', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[MACT_MASTER] WHERE [MACTID] = 97)
    INSERT INTO [dbo].[MACT_MASTER] ([MACTID], [MACTCode], [MACTName], [Location], [IsActive], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (97, N'M9861', N'PRL. SENIOR CIVIL JUDGE & MACT MUDHOL', N'MUDHOL', 1, NULL, '2026-01-28 12:29:20.743', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[MACT_MASTER] WHERE [MACTID] = 98)
    INSERT INTO [dbo].[MACT_MASTER] ([MACTID], [MACTCode], [MACTName], [Location], [IsActive], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (98, N'M2065', N'SENIOR CIVIL JUDGE & MACT BANAHATTI', N'BANAHATTI', 1, NULL, '2026-01-28 12:29:36.523', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[MACT_MASTER] WHERE [MACTID] = 99)
    INSERT INTO [dbo].[MACT_MASTER] ([MACTID], [MACTCode], [MACTName], [Location], [IsActive], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (99, N'M8023', N'PRL. SENIOR CIVIL JUDGE & MACT RAICHUR', N'RAICHUR', 1, NULL, '2026-01-28 12:30:12.737', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[MACT_MASTER] WHERE [MACTID] = 100)
    INSERT INTO [dbo].[MACT_MASTER] ([MACTID], [MACTCode], [MACTName], [Location], [IsActive], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (100, N'M4969', N'ADDL. DISTRICT & SESSION JUDGE & MACT VIJAYAPURA', N'VIJAYAPURA', 1, NULL, '2026-01-28 12:32:27.443', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[MACT_MASTER] WHERE [MACTID] = 101)
    INSERT INTO [dbo].[MACT_MASTER] ([MACTID], [MACTCode], [MACTName], [Location], [IsActive], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (101, N'M3683', N'SENIOR CIVIL JUDGE & MACT RAMDURGA', N'RAMDURGA', 1, NULL, '2026-01-28 12:34:37.573', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[MACT_MASTER] WHERE [MACTID] = 102)
    INSERT INTO [dbo].[MACT_MASTER] ([MACTID], [MACTCode], [MACTName], [Location], [IsActive], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (102, N'M6380', N'DSITRICT & SESSION COURT PUNE MAHARASHTRA', N'PUNE', 1, NULL, '2026-01-28 12:35:30.950', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[MACT_MASTER] WHERE [MACTID] = 103)
    INSERT INTO [dbo].[MACT_MASTER] ([MACTID], [MACTCode], [MACTName], [Location], [IsActive], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (103, N'M7344', N'DISTRICT JUDGE - 17 ADDL. SESSION JUDGE PUNE', N'PUNE', 1, NULL, '2026-01-28 12:35:52.747', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[MACT_MASTER] WHERE [MACTID] = 104)
    INSERT INTO [dbo].[MACT_MASTER] ([MACTID], [MACTCode], [MACTName], [Location], [IsActive], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (104, N'M7316', N'SENIOR CIVIL JUDGE & MACT HIREKERUR', N'HIREKERUR', 1, NULL, '2026-01-30 12:33:51.353', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[MACT_MASTER] WHERE [MACTID] = 105)
    INSERT INTO [dbo].[MACT_MASTER] ([MACTID], [MACTCode], [MACTName], [Location], [IsActive], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (105, N'M4229', N'CIVIL JUDGE & JMFC HIREKERUR', N'HIREKERUR', 1, NULL, '2026-01-30 12:34:13.167', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[MACT_MASTER] WHERE [MACTID] = 106)
    INSERT INTO [dbo].[MACT_MASTER] ([MACTID], [MACTCode], [MACTName], [Location], [IsActive], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (106, N'M2384', N'SENIOR CIVIL JUDGE & MACT BYADAGI', N'BYADAGI', 1, NULL, '2026-01-30 12:34:28.777', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[MACT_MASTER] WHERE [MACTID] = 107)
    INSERT INTO [dbo].[MACT_MASTER] ([MACTID], [MACTCode], [MACTName], [Location], [IsActive], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (107, N'M5829', N'CIVIL JUDGE & JMFC BYADAGI', N'BYADAGI', 1, NULL, '2026-01-30 12:34:47.290', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[MACT_MASTER] WHERE [MACTID] = 108)
    INSERT INTO [dbo].[MACT_MASTER] ([MACTID], [MACTCode], [MACTName], [Location], [IsActive], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (108, N'M9575', N'SENIOR CIVIL JUDGE & MACT SHIGGAON', N'SHIGGAON', 1, NULL, '2026-01-30 12:35:21.280', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[MACT_MASTER] WHERE [MACTID] = 109)
    INSERT INTO [dbo].[MACT_MASTER] ([MACTID], [MACTCode], [MACTName], [Location], [IsActive], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (109, N'M7173', N'CIVIL JUDGE & JMFC SHIGGAON', N'SHIGGAON', 1, NULL, '2026-01-30 12:35:39.750', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[MACT_MASTER] WHERE [MACTID] = 110)
    INSERT INTO [dbo].[MACT_MASTER] ([MACTID], [MACTCode], [MACTName], [Location], [IsActive], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (110, N'M8481', N'CIVIL JUDGE & JMFC HANGAL', N'HANGAL', 1, NULL, '2026-01-30 12:35:59.813', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[MACT_MASTER] WHERE [MACTID] = 111)
    INSERT INTO [dbo].[MACT_MASTER] ([MACTID], [MACTCode], [MACTName], [Location], [IsActive], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (111, N'M9388', N'SENIOR CIVIL JUDGE & MACT SAUNDATTI', N'SAUNDATTI', 1, NULL, '2026-01-30 12:39:03.427', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[MACT_MASTER] WHERE [MACTID] = 112)
    INSERT INTO [dbo].[MACT_MASTER] ([MACTID], [MACTCode], [MACTName], [Location], [IsActive], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (112, N'M5451', N'ADDL. SENIOR CIVIL JUDGE AND JMFC NIPPANI', N'NIPPANI', 1, NULL, '2026-01-30 12:39:33.647', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[MACT_MASTER] WHERE [MACTID] = 113)
    INSERT INTO [dbo].[MACT_MASTER] ([MACTID], [MACTCode], [MACTName], [Location], [IsActive], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (113, N'M6910', N'ADDL. SENIOR CIVIL JUDGE AND MACT SAUNDATTI', N'SAUNDATTI', 1, NULL, '2026-01-30 12:40:01.087', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[MACT_MASTER] WHERE [MACTID] = 114)
    INSERT INTO [dbo].[MACT_MASTER] ([MACTID], [MACTCode], [MACTName], [Location], [IsActive], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (114, N'M6146', N'SENIOR CIVIL JUDGE & MACT BAILHONGAL', N'BAILHONGAL', 1, NULL, '2026-01-30 12:59:29.113', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[MACT_MASTER] WHERE [MACTID] = 115)
    INSERT INTO [dbo].[MACT_MASTER] ([MACTID], [MACTCode], [MACTName], [Location], [IsActive], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (115, N'M2274', N'PRL. SENIOR CIVIL JUDGE & JMFC BELATHANGADY', N'BELATHANGADY', 1, NULL, '2026-01-31 12:30:40.863', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[MACT_MASTER] WHERE [MACTID] = 116)
    INSERT INTO [dbo].[MACT_MASTER] ([MACTID], [MACTCode], [MACTName], [Location], [IsActive], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (116, N'M5303', N'PRL. SENIOR CIVIL JUDGE & JMFC BELAGAVI', N'BELAGAVI', 1, NULL, '2026-01-31 12:31:37.787', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[MACT_MASTER] WHERE [MACTID] = 117)
    INSERT INTO [dbo].[MACT_MASTER] ([MACTID], [MACTCode], [MACTName], [Location], [IsActive], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (117, N'M2916', N'SENIOR CIVIL JUDGE & AMACT LAXMESHWAR', N'LAXMESHWAR', 1, NULL, '2026-01-31 12:32:19.943', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[MACT_MASTER] WHERE [MACTID] = 118)
    INSERT INTO [dbo].[MACT_MASTER] ([MACTID], [MACTCode], [MACTName], [Location], [IsActive], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (118, N'M9729', N'CIVIL JUDGE (SR DN) AND MACT NO. IX B. BAGEWADI', N'B. BAGEWADI', 1, NULL, '2026-01-31 12:33:28.273', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[MACT_MASTER] WHERE [MACTID] = 119)
    INSERT INTO [dbo].[MACT_MASTER] ([MACTID], [MACTCode], [MACTName], [Location], [IsActive], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (119, N'M7947', N'ADDL. DISTRICT & SESSIONS JUDGE, KOPPAL SITTING AT GANGAWATI', N'GANGAWATI', 1, NULL, '2026-01-31 12:34:32.370', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[MACT_MASTER] WHERE [MACTID] = 120)
    INSERT INTO [dbo].[MACT_MASTER] ([MACTID], [MACTCode], [MACTName], [Location], [IsActive], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (120, N'M5114', N'PRL. SENIOR CIVIL JUDGE AND CJM COURT, GADAG', N'GADAG', 1, NULL, '2026-01-31 12:36:18.200', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[MACT_MASTER] WHERE [MACTID] = 121)
    INSERT INTO [dbo].[MACT_MASTER] ([MACTID], [MACTCode], [MACTName], [Location], [IsActive], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (121, N'M1142', N'ADDL. DISTRICT JUDGE CUM MACT KARNOOL, AT ADONI', N'KARNOOL (ADONI)', 1, NULL, '2026-01-31 12:37:11.797', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[MACT_MASTER] WHERE [MACTID] = 122)
    INSERT INTO [dbo].[MACT_MASTER] ([MACTID], [MACTCode], [MACTName], [Location], [IsActive], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (122, N'M5625', N'SCCH-14-XVI ADDL. JUDGE, COURT OF SMALL CAUSE & ACJM, BENGALURU SOUTH ', N'BENGALURU', 1, NULL, '2026-01-31 12:43:24.277', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[MACT_MASTER] WHERE [MACTID] = 123)
    INSERT INTO [dbo].[MACT_MASTER] ([MACTID], [MACTCode], [MACTName], [Location], [IsActive], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (123, N'M5889', N'SENIOR CIVIL JUDGE & JMFC RAIBAG', N'RAIBAG', 1, NULL, '2026-02-02 16:10:35.230', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[MACT_MASTER] WHERE [MACTID] = 124)
    INSERT INTO [dbo].[MACT_MASTER] ([MACTID], [MACTCode], [MACTName], [Location], [IsActive], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (124, N'M3784', N'DISTRICT & SESSION COURT RATNAGIRI', N'RATNAGIRI (MAHARASHTRA)', 1, NULL, '2026-02-02 16:11:08.890', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[MACT_MASTER] WHERE [MACTID] = 125)
    INSERT INTO [dbo].[MACT_MASTER] ([MACTID], [MACTCode], [MACTName], [Location], [IsActive], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (125, N'M4929', N'SENIOR CIVIL JUDGE & JMFC YELBURGA', N'YELBURGA', 1, NULL, '2026-02-02 17:14:07.127', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[MACT_MASTER] WHERE [MACTID] = 126)
    INSERT INTO [dbo].[MACT_MASTER] ([MACTID], [MACTCode], [MACTName], [Location], [IsActive], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (126, N'M6213', N'SENIOR CIVIL JUDGE & JMFC KUSHTAGI', N'KUSHTAGI', 1, NULL, '2026-02-02 17:37:22.110', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[MACT_MASTER] WHERE [MACTID] = 127)
    INSERT INTO [dbo].[MACT_MASTER] ([MACTID], [MACTCode], [MACTName], [Location], [IsActive], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (127, N'M9111', N'DISTRICT & SESSION COURT SANGALI', N'SANGALI', 1, NULL, '2026-02-03 09:56:37.430', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[MACT_MASTER] WHERE [MACTID] = 128)
    INSERT INTO [dbo].[MACT_MASTER] ([MACTID], [MACTCode], [MACTName], [Location], [IsActive], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (128, N'M3144', N'DISTRICT JUDGE 1 & ADDL. SESSION JUDGE KARAD', N'KARAD', 1, NULL, '2026-02-03 09:57:31.900', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[MACT_MASTER] WHERE [MACTID] = 129)
    INSERT INTO [dbo].[MACT_MASTER] ([MACTID], [MACTCode], [MACTName], [Location], [IsActive], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (129, N'M4793', N'DISTRICT COURT ICHALKARANJI', N'ICHALKARANJI', 1, NULL, '2026-02-03 09:58:35.277', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[MACT_MASTER] WHERE [MACTID] = 130)
    INSERT INTO [dbo].[MACT_MASTER] ([MACTID], [MACTCode], [MACTName], [Location], [IsActive], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (130, N'M5047', N'DISTRICT JUDGE 2 ADDL. SESSION JUDGE ICHALAKARANJI', N'ICHALKARANJI', 1, NULL, '2026-02-03 09:59:24.810', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[MACT_MASTER] WHERE [MACTID] = 131)
    INSERT INTO [dbo].[MACT_MASTER] ([MACTID], [MACTCode], [MACTName], [Location], [IsActive], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (131, N'M4943', N'MEMBER, MOTOR ACCIDENT CLAIMS TRIBUNAL KOLHAPUR', N'KOLHAPUR', 1, NULL, '2026-02-03 10:00:21.280', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[MACT_MASTER] WHERE [MACTID] = 132)
    INSERT INTO [dbo].[MACT_MASTER] ([MACTID], [MACTCode], [MACTName], [Location], [IsActive], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (132, N'M3422', N'MEMBER, MOTOR ACCIDENT CLAIMS TRIBUNAL JAYSINGPUR', N'JAYSINGPUR', 1, NULL, '2026-02-03 10:01:02.530', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[MACT_MASTER] WHERE [MACTID] = 133)
    INSERT INTO [dbo].[MACT_MASTER] ([MACTID], [MACTCode], [MACTName], [Location], [IsActive], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (133, N'M5654', N'DISTRICT COURT KOLHAPUR', N'KOLHAPUR', 1, NULL, '2026-02-03 10:01:43.237', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[MACT_MASTER] WHERE [MACTID] = 134)
    INSERT INTO [dbo].[MACT_MASTER] ([MACTID], [MACTCode], [MACTName], [Location], [IsActive], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (134, N'M2354', N'DISTRICT & SESSION COURT NASHIK', N'NASHIK', 1, NULL, '2026-02-03 10:02:30.083', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[MACT_MASTER] WHERE [MACTID] = 135)
    INSERT INTO [dbo].[MACT_MASTER] ([MACTID], [MACTCode], [MACTName], [Location], [IsActive], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (135, N'M1249', N'1 ADDL. SENIOR CIVIL JUDGE & CJM VIJAYAPURA', N'VIJAYAPURA', 1, NULL, '2026-02-03 10:03:32.727', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[MACT_MASTER] WHERE [MACTID] = 136)
    INSERT INTO [dbo].[MACT_MASTER] ([MACTID], [MACTCode], [MACTName], [Location], [IsActive], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (136, N'M8394', N'ADDL. CIVIL JUDGE (SR DN), HOSPET', N'HOSPET', 1, NULL, '2026-02-03 10:04:42.930', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[MACT_MASTER] WHERE [MACTID] = 137)
    INSERT INTO [dbo].[MACT_MASTER] ([MACTID], [MACTCode], [MACTName], [Location], [IsActive], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (137, N'M6368', N'SR. CIVIL JUDGE & JMFC SHORAPUR', N'SHORAPUR', 1, NULL, '2026-02-03 10:05:31.557', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[MACT_MASTER] WHERE [MACTID] = 139)
    INSERT INTO [dbo].[MACT_MASTER] ([MACTID], [MACTCode], [MACTName], [Location], [IsActive], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (139, N'M3343', N'PRL. SENIOR CIVIL JUDGE & CJM COURT KARWAR', N'KARWAR', 1, NULL, '2026-02-03 10:14:16.810', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[MACT_MASTER] WHERE [MACTID] = 140)
    INSERT INTO [dbo].[MACT_MASTER] ([MACTID], [MACTCode], [MACTName], [Location], [IsActive], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (140, N'M8364', N'PRL. DISTRICT & SESSION JUDGE NIZAMABAD', N'NIZAMABAD', 1, NULL, '2026-02-03 10:16:35.927', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[MACT_MASTER] WHERE [MACTID] = 141)
    INSERT INTO [dbo].[MACT_MASTER] ([MACTID], [MACTCode], [MACTName], [Location], [IsActive], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (141, N'M8590', N'CHIEF JUDGE HYDERABAD', N'HYDERABAD', 1, NULL, '2026-02-03 10:17:11.050', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[MACT_MASTER] WHERE [MACTID] = 142)
    INSERT INTO [dbo].[MACT_MASTER] ([MACTID], [MACTCode], [MACTName], [Location], [IsActive], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (142, N'M2295', N'SENIOR CIVIL JUDGE & JMFC SAGAR', N'SAGAR', 1, NULL, '2026-02-03 10:18:31.257', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[MACT_MASTER] WHERE [MACTID] = 143)
    INSERT INTO [dbo].[MACT_MASTER] ([MACTID], [MACTCode], [MACTName], [Location], [IsActive], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (143, N'M4718', N'MEMBER, MOTOR ACCIDENT CLAIMS TRIBUNAL SATARA', N'SATARA', 1, NULL, '2026-02-03 10:19:14.150', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[MACT_MASTER] WHERE [MACTID] = 144)
    INSERT INTO [dbo].[MACT_MASTER] ([MACTID], [MACTCode], [MACTName], [Location], [IsActive], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (144, N'M7124', N'MOTOR ACCIDENT CLAIM TRIBUNAL HYDERABAD', N'HYDERABAD', 1, NULL, '2026-02-03 10:20:14.560', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[MACT_MASTER] WHERE [MACTID] = 145)
    INSERT INTO [dbo].[MACT_MASTER] ([MACTID], [MACTCode], [MACTName], [Location], [IsActive], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (145, N'M7413', N'MOTOR ACCIDENT CLAIMS TRIBUNAL-III SOUTH GOA MARGOA', N'MARGOA', 1, NULL, '2026-02-03 10:21:39.997', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[MACT_MASTER] WHERE [MACTID] = 146)
    INSERT INTO [dbo].[MACT_MASTER] ([MACTID], [MACTCode], [MACTName], [Location], [IsActive], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (146, N'M8381', N'SENIOR CIVIL JUDGE & JMFC HIRIYUR', N'HIRIYUR', 1, NULL, '2026-02-04 15:11:37.797', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[MACT_MASTER] WHERE [MACTID] = 147)
    INSERT INTO [dbo].[MACT_MASTER] ([MACTID], [MACTCode], [MACTName], [Location], [IsActive], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (147, N'M6559', N'IV AADL. SENIOR CIVIL JUDGE & JMFC VIJAYAPURA', N'VIJAYAPURA', 1, NULL, '2026-02-04 15:12:21.203', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[MACT_MASTER] WHERE [MACTID] = 148)
    INSERT INTO [dbo].[MACT_MASTER] ([MACTID], [MACTCode], [MACTName], [Location], [IsActive], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (148, N'M7531', N'SENIOR CIVIL JUDGE & JMFC MUDALAGI', N'MUDALAGI', 1, NULL, '2026-02-04 15:14:01.393', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[MACT_MASTER] WHERE [MACTID] = 149)
    INSERT INTO [dbo].[MACT_MASTER] ([MACTID], [MACTCode], [MACTName], [Location], [IsActive], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (149, N'M1411', N'MOTOR ACCIDENT CLAIM TRIBUNAL-CUM-ADDL. DISTRICT JUDGE TIRUPATI', N'TIRUPATI', 1, NULL, '2026-02-04 16:00:04.240', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[MACT_MASTER] WHERE [MACTID] = 150)
    INSERT INTO [dbo].[MACT_MASTER] ([MACTID], [MACTCode], [MACTName], [Location], [IsActive], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (150, N'M2166', N'SENIOR CIVIL JUDGE & JMFC CUM MACT -  XIII AT HAGARIBOMNANHALLI', N'HAGARIBOMNANHALLI', 1, NULL, '2026-03-02 15:45:51.647', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[MACT_MASTER] WHERE [MACTID] = 151)
    INSERT INTO [dbo].[MACT_MASTER] ([MACTID], [MACTCode], [MACTName], [Location], [IsActive], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (151, N'M8722', N'PRL. DISTRICT COURT MAHABUBNAGAR', N'MAHABUBNAGAR', 1, NULL, '2026-03-02 15:46:29.447', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[MACT_MASTER] WHERE [MACTID] = 152)
    INSERT INTO [dbo].[MACT_MASTER] ([MACTID], [MACTCode], [MACTName], [Location], [IsActive], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (152, N'M7393', N'SENIOR CIVIL JUDGE & JMFC SHIKARIPURA', N'SHIKARIPURA', 1, NULL, '2026-03-02 15:47:08.790', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[MACT_MASTER] WHERE [MACTID] = 153)
    INSERT INTO [dbo].[MACT_MASTER] ([MACTID], [MACTCode], [MACTName], [Location], [IsActive], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (153, N'M7576', N'PRL. SENIOR CIVIL JUDGE & CJM DHARWAD', N'DHARWAD', 1, NULL, '2026-03-02 15:47:45.120', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[MACT_MASTER] WHERE [MACTID] = 154)
    INSERT INTO [dbo].[MACT_MASTER] ([MACTID], [MACTCode], [MACTName], [Location], [IsActive], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (154, N'M3362', N'PRL. SENIOR CIVIL JUDGE & CJM DAVANGERE', N'DAVANGERE', 1, NULL, '2026-03-02 15:48:19.683', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[MACT_MASTER] WHERE [MACTID] = 155)
    INSERT INTO [dbo].[MACT_MASTER] ([MACTID], [MACTCode], [MACTName], [Location], [IsActive], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (155, N'M5739', N'SENIOR CIVIL JUDGE AND JMFC TIPTUR', N'TIPTUR', 1, NULL, '2026-03-02 15:49:10.717', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[MACT_MASTER] WHERE [MACTID] = 156)
    INSERT INTO [dbo].[MACT_MASTER] ([MACTID], [MACTCode], [MACTName], [Location], [IsActive], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (156, N'M7101', N'SENIOR CIVIL JUDGE LINGSUGUR', N'LINGSUGUR', 1, NULL, '2026-03-02 15:49:57.827', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[MACT_MASTER] WHERE [MACTID] = 157)
    INSERT INTO [dbo].[MACT_MASTER] ([MACTID], [MACTCode], [MACTName], [Location], [IsActive], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (157, N'M2977', N'SENIOR CIVIL JUDGE AND JMFC, RON ', N'RON', 1, NULL, '2026-03-18 11:20:09.297', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[MACT_MASTER] WHERE [MACTID] = 158)
    INSERT INTO [dbo].[MACT_MASTER] ([MACTID], [MACTCode], [MACTName], [Location], [IsActive], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (158, N'M1908', N'COURT OF SMALL CAUSES SCCH-22-XX ADDL JUDGE BENGALURU', N'BENGALURU', 1, NULL, '2026-03-18 11:21:20.607', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[MACT_MASTER] WHERE [MACTID] = 159)
    INSERT INTO [dbo].[MACT_MASTER] ([MACTID], [MACTCode], [MACTName], [Location], [IsActive], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (159, N'M9644', N'SENIOR CIVIL JUDGE AND CJM KOPPAL', N'KOPPAL', 1, NULL, '2026-03-18 11:22:40.970', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[MACT_MASTER] WHERE [MACTID] = 160)
    INSERT INTO [dbo].[MACT_MASTER] ([MACTID], [MACTCode], [MACTName], [Location], [IsActive], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (160, N'M5615', N'PRL. SENIOR CIVIL JUDGE AND JMFC ATHANI', N'ATHANI', 1, NULL, '2026-03-18 11:55:38.503', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[MACT_MASTER] WHERE [MACTID] = 161)
    INSERT INTO [dbo].[MACT_MASTER] ([MACTID], [MACTCode], [MACTName], [Location], [IsActive], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (161, N'M7967', N'SENIOR CIVIL JUDGE LINGASUGUR', N'LINGASUGUR', 1, NULL, '2026-03-23 15:06:48.210', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[MACT_MASTER] WHERE [MACTID] = 162)
    INSERT INTO [dbo].[MACT_MASTER] ([MACTID], [MACTCode], [MACTName], [Location], [IsActive], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (162, N'M1232', N'SENIOR CIVIL JUDGE TIPTUR', N'TIPTUR', 1, NULL, '2026-03-23 15:10:44.820', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[MACT_MASTER] WHERE [MACTID] = 163)
    INSERT INTO [dbo].[MACT_MASTER] ([MACTID], [MACTCode], [MACTName], [Location], [IsActive], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (163, N'M5496', N'PRINVIPAL SENIOR CIVIL JUDGE AND JMFC SRIRANGAPATANA', N'SRIRANGAPATANA', 1, NULL, '2026-03-23 15:14:55.050', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[MACT_MASTER] WHERE [MACTID] = 164)
    INSERT INTO [dbo].[MACT_MASTER] ([MACTID], [MACTCode], [MACTName], [Location], [IsActive], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (164, N'M1093', N'PRINCIPAL DISTRICT COURT MAHABUBNAGAR', N'MAHABUBNAGAR', 1, NULL, '2026-03-23 15:21:31.913', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[MACT_MASTER] WHERE [MACTID] = 165)
    INSERT INTO [dbo].[MACT_MASTER] ([MACTID], [MACTCode], [MACTName], [Location], [IsActive], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (165, N'M2490', N'MOTOR ACCIDENT CLAIMS TRIBUNAL PUNALUR', N'PUNALUR (KL)', 1, NULL, '2026-03-23 16:02:06.667', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[MACT_MASTER] WHERE [MACTID] = 166)
    INSERT INTO [dbo].[MACT_MASTER] ([MACTID], [MACTCode], [MACTName], [Location], [IsActive], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (166, N'M6487', N'MOTOR ACCIDENTS CLAIMS TRIBUNAL THIRUVANTHAPURAM', N'THIRUVANTHAPURAM (KL)', 1, NULL, '2026-03-23 16:02:55.960', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[MACT_MASTER] WHERE [MACTID] = 167)
    INSERT INTO [dbo].[MACT_MASTER] ([MACTID], [MACTCode], [MACTName], [Location], [IsActive], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (167, N'M4482', N'MOTOR ACCIDENT CLAIMS TRIBUNAL AT-BEED', N'BEED (MH)', 1, NULL, '2026-03-23 16:03:44.390', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[MACT_MASTER] WHERE [MACTID] = 168)
    INSERT INTO [dbo].[MACT_MASTER] ([MACTID], [MACTCode], [MACTName], [Location], [IsActive], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (168, N'M1154', N'SENIOR CIVIL JUDGE AND MACT MUNDARAGI', N'MUNDARAGI', 1, NULL, '2026-03-23 17:15:48.517', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[MACT_MASTER] WHERE [MACTID] = 169)
    INSERT INTO [dbo].[MACT_MASTER] ([MACTID], [MACTCode], [MACTName], [Location], [IsActive], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (169, N'M3115', N'SENIOR CIVIL JUDGE AND MACT MUNDARAGI', N'MUNDARAGI', 1, NULL, '2026-03-24 13:21:57.920', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[MACT_MASTER] WHERE [MACTID] = 170)
    INSERT INTO [dbo].[MACT_MASTER] ([MACTID], [MACTCode], [MACTName], [Location], [IsActive], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (170, N'M2945', N'PRL. DISTRICT AND SESSIONS JUDGE GADAG', N'GADAG', 1, NULL, '2026-03-24 16:01:41.367', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[MACT_MASTER] WHERE [MACTID] = 171)
    INSERT INTO [dbo].[MACT_MASTER] ([MACTID], [MACTCode], [MACTName], [Location], [IsActive], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (171, N'M4124', N'PRL. SENIOR CIVIL JUDGE AND CJM HAVERI', N'HAVERI', 1, NULL, '2026-03-26 17:02:08.967', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[MACT_MASTER] WHERE [MACTID] = 172)
    INSERT INTO [dbo].[MACT_MASTER] ([MACTID], [MACTCode], [MACTName], [Location], [IsActive], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (172, N'M4471', N'PRL. SENIOR CIVIL JUDGE AND JMFC BHADRAVATHI', NULL, 1, NULL, '2026-03-27 10:44:40.373', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[MACT_MASTER] WHERE [MACTID] = 173)
    INSERT INTO [dbo].[MACT_MASTER] ([MACTID], [MACTCode], [MACTName], [Location], [IsActive], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (173, N'M4354', N'MEMBER MOTOR ACCIDENT CLAIMS TRIBUNAL (MAHARASHTRA) PUNE AT PUNE', N'PUNE (MH)', 1, NULL, '2026-03-27 12:54:35.660', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[MACT_MASTER] WHERE [MACTID] = 174)
    INSERT INTO [dbo].[MACT_MASTER] ([MACTID], [MACTCode], [MACTName], [Location], [IsActive], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (174, N'M8189', N'MOTOR ACCIDENT CLAIMS TRIBUNAL (HON''BLE PRL. DISTRICT JUDGE) AT ANANTAPURAMU', N'ANANTAPURAMU', 1, NULL, '2026-03-27 12:58:02.617', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[MACT_MASTER] WHERE [MACTID] = 175)
    INSERT INTO [dbo].[MACT_MASTER] ([MACTID], [MACTCode], [MACTName], [Location], [IsActive], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (175, N'M4754', N'SENIOR CIVIL JUDGE AND JMFC HARAPANAHALLI', N'HARAPANAHALLI', 1, NULL, '2026-03-27 13:12:29.717', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[MACT_MASTER] WHERE [MACTID] = 176)
    INSERT INTO [dbo].[MACT_MASTER] ([MACTID], [MACTCode], [MACTName], [Location], [IsActive], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (176, N'M2832', N'SENIOR CIVIL JUDGE AND CJM KOPPAL', N'KOPPAL', 1, NULL, '2026-03-27 17:10:18.293', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[MACT_MASTER] WHERE [MACTID] = 178)
    INSERT INTO [dbo].[MACT_MASTER] ([MACTID], [MACTCode], [MACTName], [Location], [IsActive], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (178, N'M3651', N'SENIOR CIVIL JUDGE AND JMFC AT K. R. PET', N'K. R. PET', 1, NULL, '2026-03-27 17:29:16.810', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[MACT_MASTER] WHERE [MACTID] = 179)
    INSERT INTO [dbo].[MACT_MASTER] ([MACTID], [MACTCode], [MACTName], [Location], [IsActive], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (179, N'M4051', N'SENIOR CIVIJ JUDGE AND JMFC AT K. R. PET', N'K. R. PET', 1, NULL, '2026-03-27 17:29:44.857', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[MACT_MASTER] WHERE [MACTID] = 180)
    INSERT INTO [dbo].[MACT_MASTER] ([MACTID], [MACTCode], [MACTName], [Location], [IsActive], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (180, N'M4516', N'MOTOR ACCIDENT CLAIMS TRIBUNAL D. K. MANGALURU', N'MANGALURU', 1, NULL, '2026-03-27 17:44:59.550', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[MACT_MASTER] WHERE [MACTID] = 181)
    INSERT INTO [dbo].[MACT_MASTER] ([MACTID], [MACTCode], [MACTName], [Location], [IsActive], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (181, N'M4658', N'PRL. SENIOR CIVIL JUDGE AND JMFC, HOSAPETE', N'HOSAPETE', 1, NULL, '2026-04-09 11:04:53.410', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[MACT_MASTER] WHERE [MACTID] = 182)
    INSERT INTO [dbo].[MACT_MASTER] ([MACTID], [MACTCode], [MACTName], [Location], [IsActive], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (182, N'M3536', N'ADDL SENIOR CIVIL JUDGE AND MACT ', N'SRIRANGAPATANA', 1, NULL, '2026-09-01 16:27:59.440', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[MACT_MASTER] WHERE [MACTID] = 183)
    INSERT INTO [dbo].[MACT_MASTER] ([MACTID], [MACTCode], [MACTName], [Location], [IsActive], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (183, N'M4321', N'ADDL SENIOR CIVIL JUDGE AND MACT SRIRANGAPATANA', N'SRIRANGAPATANA', 1, NULL, '2026-09-01 16:35:53.697', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[MACT_MASTER] WHERE [MACTID] = 184)
    INSERT INTO [dbo].[MACT_MASTER] ([MACTID], [MACTCode], [MACTName], [Location], [IsActive], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (184, N'M4538', N'PRL. SENIOR CIVIJ JUDGE AND CJM BALLARI', N'BALLARI', 1, NULL, '2026-09-02 17:51:27.857', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[MACT_MASTER] WHERE [MACTID] = 185)
    INSERT INTO [dbo].[MACT_MASTER] ([MACTID], [MACTCode], [MACTName], [Location], [IsActive], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (185, N'KAVP01', N'MACT Court [KAVP01]', N'Karnataka', 1, NULL, '2026-09-21 16:30:15.293', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [dbo].[MACT_MASTER] WHERE [MACTID] = 187)
    INSERT INTO [dbo].[MACT_MASTER] ([MACTID], [MACTCode], [MACTName], [Location], [IsActive], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate]) VALUES (187, N'M8058', N'VI ADDL DISTRICT & SESSIONS JUDGE & M.A.C.T. BELAGAVI', N'BELAGAVI', 1, NULL, '2026-09-22 11:14:53.613', NULL, NULL);
SET IDENTITY_INSERT [dbo].[MACT_MASTER] OFF;
GO

