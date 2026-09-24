USE MVCCaseDB;
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[GRA_CASES]') AND name = 'IsDocumentSent')
BEGIN
    ALTER TABLE GRA_CASES ADD IsDocumentSent BIT NOT NULL DEFAULT 0;
END

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[GRA_CASES]') AND name = 'DocumentOutwardNo')
BEGIN
    ALTER TABLE GRA_CASES ADD DocumentOutwardNo NVARCHAR(50) NULL;
END

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[GRA_CASES]') AND name = 'DocumentOutwardDate')
BEGIN
    ALTER TABLE GRA_CASES ADD DocumentOutwardDate DATE NULL;
END

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[GRA_CASES]') AND name = 'IsObjectionFiled')
BEGIN
    ALTER TABLE GRA_CASES ADD IsObjectionFiled BIT NOT NULL DEFAULT 0;
END

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[GRA_CASES]') AND name = 'ObjectionFiledDate')
BEGIN
    ALTER TABLE GRA_CASES ADD ObjectionFiledDate DATE NULL;
END

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[GRA_CASES]') AND name = 'IsEvidenceFiled')
BEGIN
    ALTER TABLE GRA_CASES ADD IsEvidenceFiled BIT NOT NULL DEFAULT 0;
END

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[GRA_CASES]') AND name = 'CurrentStage')
BEGIN
    ALTER TABLE GRA_CASES ADD CurrentStage NVARCHAR(100) NULL;
END

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[GRA_CASES]') AND name = 'NextHearingDate')
BEGIN
    ALTER TABLE GRA_CASES ADD NextHearingDate DATE NULL;
END

PRINT 'GRA_CASES table updated successfully.';
