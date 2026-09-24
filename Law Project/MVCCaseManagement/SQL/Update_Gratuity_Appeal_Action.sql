IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[GRA_CASES]') AND name = N'AppealActionOutwardNo')
BEGIN
    ALTER TABLE [GRA_CASES] ADD [AppealActionOutwardNo] NVARCHAR(MAX) NULL;
END

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[GRA_CASES]') AND name = N'AppealActionDate')
BEGIN
    ALTER TABLE [GRA_CASES] ADD [AppealActionDate] DATETIME2 NULL;
END
