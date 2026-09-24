IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('LABOUR_CASES') AND name = 'Arising_CurrentStatus')
BEGIN
    ALTER TABLE LABOUR_CASES ADD Arising_CurrentStatus NVARCHAR(100) NULL;
END

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('LABOUR_CASES') AND name = 'Arising_ApplicationStatus')
BEGIN
    ALTER TABLE LABOUR_CASES ADD Arising_ApplicationStatus NVARCHAR(100) NULL;
END
