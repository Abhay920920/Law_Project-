-- Create Approval Letters Table
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'APPROVAL_LETTERS')
BEGIN
    CREATE TABLE APPROVAL_LETTERS (
        LetterID INT IDENTITY(1,1) PRIMARY KEY,
        LetterNumber NVARCHAR(100) NULL,
        LetterDate DATE NULL,
        FilePath NVARCHAR(500) NULL,
        FileName NVARCHAR(255) NULL,
        Remarks NVARCHAR(500) NULL,
        DivisionID INT NOT NULL DEFAULT 0,
        UploadedBy NVARCHAR(100) NULL,
        UploadedDate DATETIME NULL DEFAULT GETDATE()
    );
    
    PRINT 'Table APPROVAL_LETTERS created successfully.';
END
ELSE
BEGIN
    PRINT 'Table APPROVAL_LETTERS already exists.';
END
GO
