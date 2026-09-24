USE [Admin_Law]
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[LABOUR_CASES]') AND name = 'CO_FeasibilityReceived')
BEGIN
    ALTER TABLE [LABOUR_CASES] ADD [CO_FeasibilityReceived] bit NULL;
    ALTER TABLE [LABOUR_CASES] ADD [CO_FeasibilityDate] datetime2(7) NULL;
    ALTER TABLE [LABOUR_CASES] ADD [CO_ActionTaken] nvarchar(100) NULL;
    ALTER TABLE [LABOUR_CASES] ADD [CO_ApprovalOutwardNo] nvarchar(50) NULL;
    ALTER TABLE [LABOUR_CASES] ADD [CO_ApprovalDate] datetime2(7) NULL;
    ALTER TABLE [LABOUR_CASES] ADD [CO_WP_CaseStatus_Option] nvarchar(100) NULL;
    ALTER TABLE [LABOUR_CASES] ADD [CO_WP_CaseNumber] nvarchar(100) NULL;
    ALTER TABLE [LABOUR_CASES] ADD [CO_WP_Year] int NULL;
    ALTER TABLE [LABOUR_CASES] ADD [CO_WP_HighCourtBench] nvarchar(100) NULL;
    ALTER TABLE [LABOUR_CASES] ADD [CO_WP_EntrustmentNo] nvarchar(100) NULL;
    ALTER TABLE [LABOUR_CASES] ADD [CO_WP_EntrustmentDate] datetime2(7) NULL;
    ALTER TABLE [LABOUR_CASES] ADD [CO_WP_AdvocateName] nvarchar(200) NULL;
    ALTER TABLE [LABOUR_CASES] ADD [CO_WP_StayGranted] bit NULL;
    ALTER TABLE [LABOUR_CASES] ADD [CO_WP_StayApprovalNo] nvarchar(100) NULL;
    ALTER TABLE [LABOUR_CASES] ADD [CO_WP_StayNature] nvarchar(100) NULL;
    ALTER TABLE [LABOUR_CASES] ADD [CO_WP_StayDate] datetime2(7) NULL;
    ALTER TABLE [LABOUR_CASES] ADD [CO_ReinstatedSubjectToWP] nvarchar(100) NULL;
    ALTER TABLE [LABOUR_CASES] ADD [CO_ReinstatementApprovalIssued] bit NULL;
    ALTER TABLE [LABOUR_CASES] ADD [CO_ReinstatementApprovalDate] datetime2(7) NULL;
    ALTER TABLE [LABOUR_CASES] ADD [CO_WP_Status] nvarchar(50) NULL;
    ALTER TABLE [LABOUR_CASES] ADD [CO_OverallCaseStatus] nvarchar(50) NULL;
    ALTER TABLE [LABOUR_CASES] ADD [CO_Disposal_Nature] nvarchar(100) NULL;
    ALTER TABLE [LABOUR_CASES] ADD [CO_Disposal_CommSentToDivision] bit NULL;
    ALTER TABLE [LABOUR_CASES] ADD [CO_Disposal_OutwardNo] nvarchar(100) NULL;
    ALTER TABLE [LABOUR_CASES] ADD [CO_Disposal_Date] datetime2(7) NULL;
    ALTER TABLE [LABOUR_CASES] ADD [CO_Disposal_Decision] nvarchar(100) NULL;
    ALTER TABLE [LABOUR_CASES] ADD [CO_Disposal_ApprovalOutwardNo] nvarchar(100) NULL;
    ALTER TABLE [LABOUR_CASES] ADD [CO_Disposal_ApprovalDate] datetime2(7) NULL;
    ALTER TABLE [LABOUR_CASES] ADD [CO_FurtherAppeal_Status_Option] nvarchar(100) NULL;
    ALTER TABLE [LABOUR_CASES] ADD [CO_FurtherAppeal_CaseNumber] nvarchar(100) NULL;
    ALTER TABLE [LABOUR_CASES] ADD [CO_FurtherAppeal_Year] int NULL;
    ALTER TABLE [LABOUR_CASES] ADD [CO_FurtherAppeal_EntrustmentNo] nvarchar(100) NULL;
    ALTER TABLE [LABOUR_CASES] ADD [CO_FurtherAppeal_EntrustmentDate] datetime2(7) NULL;
    ALTER TABLE [LABOUR_CASES] ADD [CO_FurtherAppeal_AdvocateName] nvarchar(200) NULL;
    ALTER TABLE [LABOUR_CASES] ADD [CO_FurtherAppeal_CaseStatus] nvarchar(50) NULL;
    ALTER TABLE [LABOUR_CASES] ADD [CO_Claimant_DivisionName] nvarchar(200) NULL;
    ALTER TABLE [LABOUR_CASES] ADD [CO_Claimant_ArisingOutOf] nvarchar(100) NULL;
    ALTER TABLE [LABOUR_CASES] ADD [CO_Claimant_Court] nvarchar(200) NULL;
    ALTER TABLE [LABOUR_CASES] ADD [CO_Claimant_CaseNumber] nvarchar(100) NULL;
    ALTER TABLE [LABOUR_CASES] ADD [CO_Claimant_HighCourtBench] nvarchar(100) NULL;
    ALTER TABLE [LABOUR_CASES] ADD [CO_Claimant_OriginalCaseStatus] nvarchar(100) NULL;
    ALTER TABLE [LABOUR_CASES] ADD [CO_Claimant_IsConnected] bit NULL;
    ALTER TABLE [LABOUR_CASES] ADD [CO_Claimant_ConnectedDetails] nvarchar(max) NULL;
    ALTER TABLE [LABOUR_CASES] ADD [CO_Claimant_EntrustmentDate] datetime2(7) NULL;
    ALTER TABLE [LABOUR_CASES] ADD [CO_Claimant_AdvocateName] nvarchar(200) NULL;
    ALTER TABLE [LABOUR_CASES] ADD [CO_Claimant_CaseStatus] nvarchar(50) NULL;
    ALTER TABLE [LABOUR_CASES] ADD [CO_Service_Division] nvarchar(200) NULL;
    ALTER TABLE [LABOUR_CASES] ADD [CO_Service_WPNumber] nvarchar(100) NULL;
    ALTER TABLE [LABOUR_CASES] ADD [CO_Service_PetitionerName] nvarchar(200) NULL;
    ALTER TABLE [LABOUR_CASES] ADD [CO_Service_CaseNature] nvarchar(100) NULL;
    ALTER TABLE [LABOUR_CASES] ADD [CO_Service_Prayer] nvarchar(max) NULL;
    ALTER TABLE [LABOUR_CASES] ADD [CO_Service_StayGranted] bit NULL;
    ALTER TABLE [LABOUR_CASES] ADD [CO_Service_StayVacateFiled] bit NULL;
    ALTER TABLE [LABOUR_CASES] ADD [CO_Service_Status] nvarchar(50) NULL;
    ALTER TABLE [LABOUR_CASES] ADD [CO_Service_DisposalDate] datetime2(7) NULL;
    ALTER TABLE [LABOUR_CASES] ADD [CO_Service_ActionTaken] nvarchar(100) NULL;
    ALTER TABLE [LABOUR_CASES] ADD [CO_Service_ApprovalSentDetails] nvarchar(max) NULL;
    ALTER TABLE [LABOUR_CASES] ADD [CO_Service_OutwardNo] nvarchar(100) NULL;
    ALTER TABLE [LABOUR_CASES] ADD [CO_Service_AppealFiledBefore] nvarchar(100) NULL;
    ALTER TABLE [LABOUR_CASES] ADD [CO_Service_AppealType] nvarchar(50) NULL;
    ALTER TABLE [LABOUR_CASES] ADD [CO_Service_AppealEntrustmentDate] datetime2(7) NULL;
    ALTER TABLE [LABOUR_CASES] ADD [CO_Service_AppealAdvocate] nvarchar(200) NULL;
    ALTER TABLE [LABOUR_CASES] ADD [CO_Service_AppealStatus] nvarchar(50) NULL;
END
GO
