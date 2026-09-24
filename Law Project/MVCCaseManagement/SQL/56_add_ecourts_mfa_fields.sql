-- Adding e-Courts live data tracking fields for Corporation MFA
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[APPEAL_DETAILS]') AND name = 'CorpMFACNRNumber')
BEGIN
    ALTER TABLE [dbo].[APPEAL_DETAILS] ADD [CorpMFACNRNumber] NVARCHAR(50) NULL;
END

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[APPEAL_DETAILS]') AND name = 'CorpMFANextHearingDate')
BEGIN
    ALTER TABLE [dbo].[APPEAL_DETAILS] ADD [CorpMFANextHearingDate] DATE NULL;
END

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[APPEAL_DETAILS]') AND name = 'CorpMFAStage')
BEGIN
    ALTER TABLE [dbo].[APPEAL_DETAILS] ADD [CorpMFAStage] NVARCHAR(100) NULL;
END

-- Adding e-Courts live data tracking fields for Claimant MFA
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[APPEAL_DETAILS]') AND name = 'ClaimantMFACNRNumber')
BEGIN
    ALTER TABLE [dbo].[APPEAL_DETAILS] ADD [ClaimantMFACNRNumber] NVARCHAR(50) NULL;
END
