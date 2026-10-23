-- Add missing columns to APPEAL_DETAILS table
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[APPEAL_DETAILS]') AND name = 'InitialActionPath1')
BEGIN
    ALTER TABLE [dbo].[APPEAL_DETAILS] ADD [InitialActionPath1] NVARCHAR(MAX) NULL;
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[APPEAL_DETAILS]') AND name = 'InitialActionPath2')
BEGIN
    ALTER TABLE [dbo].[APPEAL_DETAILS] ADD [InitialActionPath2] NVARCHAR(MAX) NULL;
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[APPEAL_DETAILS]') AND name = 'ComplianceLetterPath')
BEGIN
    ALTER TABLE [dbo].[APPEAL_DETAILS] ADD [ComplianceLetterPath] NVARCHAR(MAX) NULL;
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[APPEAL_DETAILS]') AND name = 'StayOrderPath1')
BEGIN
    ALTER TABLE [dbo].[APPEAL_DETAILS] ADD [StayOrderPath1] NVARCHAR(MAX) NULL;
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[APPEAL_DETAILS]') AND name = 'StayOrderPath2')
BEGIN
    ALTER TABLE [dbo].[APPEAL_DETAILS] ADD [StayOrderPath2] NVARCHAR(MAX) NULL;
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[APPEAL_DETAILS]') AND name = 'CreatedAt')
BEGIN
    ALTER TABLE [dbo].[APPEAL_DETAILS] ADD [CreatedAt] DATETIME NULL;
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[APPEAL_DETAILS]') AND name = 'ModifiedAt')
BEGIN
    ALTER TABLE [dbo].[APPEAL_DETAILS] ADD [ModifiedAt] DATETIME NULL;
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[APPEAL_DETAILS]') AND name = 'InitialActionRemarks')
BEGIN
    ALTER TABLE [dbo].[APPEAL_DETAILS] ADD [InitialActionRemarks] NVARCHAR(MAX) NULL;
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[APPEAL_DETAILS]') AND name = 'ClaimantSCOutcome')
BEGIN
    ALTER TABLE [dbo].[APPEAL_DETAILS] ADD [ClaimantSCOutcome] NVARCHAR(100) NULL;
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[APPEAL_DETAILS]') AND name = 'ClaimantSCActionTaken')
BEGIN
    ALTER TABLE [dbo].[APPEAL_DETAILS] ADD [ClaimantSCActionTaken] NVARCHAR(100) NULL;
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[APPEAL_DETAILS]') AND name = 'ClaimantSCClosureNo')
BEGIN
    ALTER TABLE [dbo].[APPEAL_DETAILS] ADD [ClaimantSCClosureNo] NVARCHAR(50) NULL;
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[APPEAL_DETAILS]') AND name = 'ClaimantSCClosureDate')
BEGIN
    ALTER TABLE [dbo].[APPEAL_DETAILS] ADD [ClaimantSCClosureDate] DATE NULL;
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[APPEAL_DETAILS]') AND name = 'ClaimantSCDiaryNumber')
BEGIN
    ALTER TABLE [dbo].[APPEAL_DETAILS] ADD [ClaimantSCDiaryNumber] NVARCHAR(50) NULL;
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[APPEAL_DETAILS]') AND name = 'ClaimantSLPYear')
BEGIN
    ALTER TABLE [dbo].[APPEAL_DETAILS] ADD [ClaimantSLPYear] INT NULL;
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[APPEAL_DETAILS]') AND name = 'IsClaimantSCPending')
BEGIN
    ALTER TABLE [dbo].[APPEAL_DETAILS] ADD [IsClaimantSCPending] BIT NULL;
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[APPEAL_DETAILS]') AND name = 'IsClaimantSCAppeal')
BEGIN
    ALTER TABLE [dbo].[APPEAL_DETAILS] ADD [IsClaimantSCAppeal] BIT NULL;
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[APPEAL_DETAILS]') AND name = 'OtherHighCourtBench')
BEGIN
    ALTER TABLE [dbo].[APPEAL_DETAILS] ADD [OtherHighCourtBench] NVARCHAR(100) NULL;
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[APPEAL_DETAILS]') AND name = 'CorpMFAActionTaken')
BEGIN
    ALTER TABLE [dbo].[APPEAL_DETAILS] ADD [CorpMFAActionTaken] NVARCHAR(100) NULL;
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[APPEAL_DETAILS]') AND name = 'IsPendingForFiling')
BEGIN
    ALTER TABLE [dbo].[APPEAL_DETAILS] ADD [IsPendingForFiling] BIT NULL;
END
GO

PRINT 'APPEAL_DETAILS schema updated with all missing columns.';

