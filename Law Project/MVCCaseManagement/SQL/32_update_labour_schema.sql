-- Migration to add missing columns to LABOUR_CASES and create EVIDENCE/ADVOCATE tables

-- 1. Add missing columns to LABOUR_CASES
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('LABOUR_CASES') AND name = 'IsCOApprovalRequired')
BEGIN
    ALTER TABLE LABOUR_CASES ADD IsCOApprovalRequired BIT DEFAULT 0;
END

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('LABOUR_CASES') AND name = 'COApproval_OutwardNo')
BEGIN
    ALTER TABLE LABOUR_CASES ADD COApproval_OutwardNo NVARCHAR(50) NULL;
END

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('LABOUR_CASES') AND name = 'COApproval_OutwardDate')
BEGIN
    ALTER TABLE LABOUR_CASES ADD COApproval_OutwardDate DATETIME NULL;
END

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('LABOUR_CASES') AND name = 'IsDocumentSent')
BEGIN
    ALTER TABLE LABOUR_CASES ADD IsDocumentSent BIT DEFAULT 0;
END

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('LABOUR_CASES') AND name = 'DocumentSent_OutwardNo')
BEGIN
    ALTER TABLE LABOUR_CASES ADD DocumentSent_OutwardNo NVARCHAR(50) NULL;
END

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('LABOUR_CASES') AND name = 'DocumentSent_OutwardDate')
BEGIN
    ALTER TABLE LABOUR_CASES ADD DocumentSent_OutwardDate DATETIME NULL;
END

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('LABOUR_CASES') AND name = 'IsObjectionFiled')
BEGIN
    ALTER TABLE LABOUR_CASES ADD IsObjectionFiled BIT DEFAULT 0;
END

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('LABOUR_CASES') AND name = 'ObjectionFiled_OutwardNo')
BEGIN
    ALTER TABLE LABOUR_CASES ADD ObjectionFiled_OutwardNo NVARCHAR(50) NULL;
END

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('LABOUR_CASES') AND name = 'ObjectionFiled_OutwardDate')
BEGIN
    ALTER TABLE LABOUR_CASES ADD ObjectionFiled_OutwardDate DATETIME NULL;
END

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('LABOUR_CASES') AND name = 'DoubleClaimRemarks')
BEGIN
    ALTER TABLE LABOUR_CASES ADD DoubleClaimRemarks NVARCHAR(MAX) NULL;
END

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('LABOUR_CASES') AND name = 'NextHearingDate')
BEGIN
    ALTER TABLE LABOUR_CASES ADD NextHearingDate DATETIME NULL;
END

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('LABOUR_CASES') AND name = 'FavorRemark')
BEGIN
    ALTER TABLE LABOUR_CASES ADD FavorRemark NVARCHAR(MAX) NULL;
END

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('LABOUR_CASES') AND name = 'DE_HistorySheetPath')
BEGIN
    ALTER TABLE LABOUR_CASES ADD DE_HistorySheetPath NVARCHAR(MAX) NULL;
END

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('LABOUR_CASES') AND name = 'JudgmentCopyPath')
BEGIN
    ALTER TABLE LABOUR_CASES ADD JudgmentCopyPath NVARCHAR(MAX) NULL;
END

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('LABOUR_CASES') AND name = 'CC_CaseDisposedDate')
BEGIN
    ALTER TABLE LABOUR_CASES ADD CC_CaseDisposedDate DATETIME NULL;
END

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('LABOUR_CASES') AND name = 'CO_Remarks')
BEGIN
    ALTER TABLE LABOUR_CASES ADD CO_Remarks NVARCHAR(MAX) NULL;
END

-- 2. Create LABOUR_CASE_EVIDENCE table if it doesn't exist
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'LABOUR_CASE_EVIDENCE')
BEGIN
    CREATE TABLE LABOUR_CASE_EVIDENCE (
        EvidenceID INT IDENTITY(1,1) PRIMARY KEY,
        CaseID INT NOT NULL,
        EvidenceType NVARCHAR(100) NULL,
        OtherDetails NVARCHAR(200) NULL,
        FOREIGN KEY (CaseID) REFERENCES LABOUR_CASES(CaseID)
    );
END

-- 3. Create LABOUR_ADVOCATES table if it doesn't exist
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'LABOUR_ADVOCATES')
BEGIN
    CREATE TABLE LABOUR_ADVOCATES (
        AdvocateID INT IDENTITY(1,1) PRIMARY KEY,
        AdvocateName NVARCHAR(200) NOT NULL,
        IsActive BIT DEFAULT 1
    );

    -- Optional: Seed with some data if needed, or leave for user to populate
END
