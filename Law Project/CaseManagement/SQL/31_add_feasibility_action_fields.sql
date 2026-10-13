USE [Law_Project]
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[GRA_CASES]') AND name = 'IsFeasibilityReceived')
BEGIN
    ALTER TABLE [dbo].[GRA_CASES] ADD [IsFeasibilityReceived] BIT NULL;
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[GRA_CASES]') AND name = 'FeasibilityReceiptDate')
BEGIN
    ALTER TABLE [dbo].[GRA_CASES] ADD [FeasibilityReceiptDate] DATETIME NULL;
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[GRA_CASES]') AND name = 'ActionTaken')
BEGIN
    ALTER TABLE [dbo].[GRA_CASES] ADD [ActionTaken] NVARCHAR(50) NULL;
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[GRA_CASES]') AND name = 'ApprovalOutwardNo')
BEGIN
    ALTER TABLE [dbo].[GRA_CASES] ADD [ApprovalOutwardNo] NVARCHAR(50) NULL;
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[GRA_CASES]') AND name = 'ApprovalDate')
BEGIN
    ALTER TABLE [dbo].[GRA_CASES] ADD [ApprovalDate] DATETIME NULL;
END
GO
