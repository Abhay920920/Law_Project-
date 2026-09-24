-- Add Claimant SC Judgment Path column to APPEAL_DETAILS
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[APPEAL_DETAILS]') AND name = 'ClaimantSCJudgmentPath')
BEGIN
    ALTER TABLE [dbo].[APPEAL_DETAILS] ADD [ClaimantSCJudgmentPath] NVARCHAR(MAX) NULL;
END
GO
