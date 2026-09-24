USE MVCCaseDB;
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[GRA_CASES]') AND name = 'Period_SPE_LWA_ABS_CA')
BEGIN
    ALTER TABLE GRA_CASES ADD Period_SPE_LWA_ABS_CA NVARCHAR(100) NULL;
END

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[GRA_CASES]') AND name = 'LastDrawnPay_CA')
BEGIN
    ALTER TABLE GRA_CASES ADD LastDrawnPay_CA DECIMAL(18, 2) NULL;
END

PRINT 'GRA_CASES table updated with ALC service columns.';
