IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[MVC_CASES]') AND name = 'ObjectionOutwardNo')
BEGIN
    ALTER TABLE MVC_CASES ADD ObjectionOutwardNo NVARCHAR(50) NULL;
END

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[MVC_CASES]') AND name = 'ClosureDate')
BEGIN
    ALTER TABLE MVC_CASES ADD ClosureDate DATETIME NULL;
END
GO
