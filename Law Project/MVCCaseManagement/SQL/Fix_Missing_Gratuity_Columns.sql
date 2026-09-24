IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[GRA_CASES]') AND name = N'AppealDisposalDate')
BEGIN
    ALTER TABLE [GRA_CASES] ADD [AppealDisposalDate] DATETIME2 NULL;
END

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[GRA_CASES]') AND name = N'AppealJudgmentPath')
BEGIN
    ALTER TABLE [GRA_CASES] ADD [AppealJudgmentPath] NVARCHAR(MAX) NULL;
END

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[GRA_CASES]') AND name = N'ClaimantHighCourtBench')
BEGIN
    ALTER TABLE [GRA_CASES] ADD [ClaimantHighCourtBench] NVARCHAR(MAX) NULL;
END

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[GRA_CASES]') AND name = N'ClaimantOtherHighCourtBench')
BEGIN
    ALTER TABLE [GRA_CASES] ADD [ClaimantOtherHighCourtBench] NVARCHAR(MAX) NULL;
END
