USE [Admin_Law]
GO

-- Add missing columns identified during audit
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[LABOUR_CASES]') AND name = 'CO_FurtherAppeal_DisposalOutwardNo')
BEGIN
    ALTER TABLE [LABOUR_CASES] ADD [CO_FurtherAppeal_DisposalOutwardNo] nvarchar(100) NULL;
END

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[LABOUR_CASES]') AND name = 'CO_FurtherAppeal_DisposalDate')
BEGIN
    ALTER TABLE [LABOUR_CASES] ADD [CO_FurtherAppeal_DisposalDate] datetime2(7) NULL;
END

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[LABOUR_CASES]') AND name = 'CO_Service_OutwardDate')
BEGIN
    ALTER TABLE [LABOUR_CASES] ADD [CO_Service_OutwardDate] datetime2(7) NULL;
END

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[LABOUR_CASES]') AND name = 'CO_Service_PetitionCopyPath')
BEGIN
    ALTER TABLE [LABOUR_CASES] ADD [CO_Service_PetitionCopyPath] nvarchar(max) NULL;
END

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[LABOUR_CASES]') AND name = 'CO_Claimant_IsConnected')
BEGIN
    ALTER TABLE [LABOUR_CASES] ADD [CO_Claimant_IsConnected] bit NULL;
END

-- Verify overall status column
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[LABOUR_CASES]') AND name = 'CO_OverallCaseStatus')
BEGIN
    ALTER TABLE [LABOUR_CASES] ADD [CO_OverallCaseStatus] nvarchar(50) NULL;
END

GO
