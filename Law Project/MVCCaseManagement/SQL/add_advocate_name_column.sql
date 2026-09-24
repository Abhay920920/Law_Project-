USE Admin_Law;
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[MVC_CASES]') AND name = 'AdvocateName')
BEGIN
    ALTER TABLE MVC_CASES ADD AdvocateName NVARCHAR(255) NULL;
    PRINT 'Column AdvocateName added to MVC_CASES.';
END
ELSE
BEGIN
    PRINT 'Column AdvocateName already exists in MVC_CASES.';
END
GO

-- Migrate existing data
PRINT 'Migrating existing advocate names...';
UPDATE c
SET c.AdvocateName = a.AdvocateName
FROM MVC_CASES c
JOIN ADVOCATE_MASTER a ON c.AdvocateID = a.AdvocateID
WHERE c.AdvocateName IS NULL;

PRINT 'Migration completed.';
GO
