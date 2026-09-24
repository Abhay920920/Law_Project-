-- =============================================
-- LABOUR_ARISING_APPLICATIONS Table
-- Separate table for Arising Applications
-- Connected to LABOUR_CASES via ParentCaseID (FK)
-- with denormalized Case Number + Year + Court for lookup
-- =============================================

IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='LABOUR_ARISING_APPLICATIONS' AND xtype='U')
BEGIN
    CREATE TABLE LABOUR_ARISING_APPLICATIONS (
        ArisingID           INT IDENTITY(1,1) PRIMARY KEY,

        -- Parent Case Connection (FK to LABOUR_CASES)
        ParentCaseID        INT NULL,
        Parent_CaseNumber   NVARCHAR(100) NULL,
        Parent_CaseYear     INT NULL,
        Parent_CourtID      INT NULL,
        Parent_CourtName    NVARCHAR(200) NULL,
        Parent_CaseType     NVARCHAR(50) NULL,
        Parent_CaseStatus   NVARCHAR(50) NULL,
        Parent_PetitionerName NVARCHAR(200) NULL,

        -- Division
        DivisionID          INT NOT NULL,

        -- Case Identification
        CaseNumber          NVARCHAR(100) NOT NULL,
        CaseYear            INT NULL,
        CourtID             INT NULL,
        OtherCourtDetails   NVARCHAR(200) NULL,

        -- Case Type & Status
        CaseType            NVARCHAR(50) DEFAULT 'Arising Application',
        CaseStatus          NVARCHAR(50) DEFAULT 'Pending',

        -- Petitioner
        PetitionerName      NVARCHAR(200) NULL,

        -- Entrustment & Advocate
        EntrustmentNo       NVARCHAR(100) NULL,
        EntrustmentDate     DATE NULL,
        AdvocateID          INT NULL,
        AdvocateName        NVARCHAR(200) NULL,

        -- Progress: Document Sent
        IsDocumentSent      BIT DEFAULT 0,
        DocumentSent_OutwardNo   NVARCHAR(50) NULL,
        DocumentSent_OutwardDate DATE NULL,

        -- Progress: Objection Filed
        IsObjectionFiled    BIT DEFAULT 0,
        ObjectionFiled_OutwardNo   NVARCHAR(50) NULL,
        ObjectionFiled_OutwardDate DATE NULL,

        -- Progress: Evidence Filed
        IsEvidenceFiled     BIT DEFAULT 0,

        -- Current Stage & Hearing
        CurrentStage        NVARCHAR(200) NULL,
        NextHearingDate     DATE NULL,

        -- Remarks
        Remarks             NVARCHAR(MAX) NULL,

        -- Metadata
        CreatedDate         DATETIME DEFAULT GETDATE(),
        CreatedBy           INT NULL,
        ModifiedDate        DATETIME NULL,
        ModifiedBy          INT NULL,

        -- Foreign Key to Parent Case
        CONSTRAINT FK_ArisingApp_ParentCase 
            FOREIGN KEY (ParentCaseID) REFERENCES LABOUR_CASES(CaseID)
            ON DELETE SET NULL  -- Parent deletion leaves arising app intact
            ON UPDATE CASCADE
    );

    -- Index on parent linkage for fast lookups
    CREATE NONCLUSTERED INDEX IX_ArisingApp_ParentCaseID 
        ON LABOUR_ARISING_APPLICATIONS(ParentCaseID);

    CREATE NONCLUSTERED INDEX IX_ArisingApp_CaseNumber_Year 
        ON LABOUR_ARISING_APPLICATIONS(CaseNumber, CaseYear);

    CREATE NONCLUSTERED INDEX IX_ArisingApp_DivisionID 
        ON LABOUR_ARISING_APPLICATIONS(DivisionID);

    CREATE NONCLUSTERED INDEX IX_ArisingApp_NextHearing 
        ON LABOUR_ARISING_APPLICATIONS(NextHearingDate)
        WHERE NextHearingDate IS NOT NULL;

    PRINT 'Table LABOUR_ARISING_APPLICATIONS created successfully.';
END
ELSE
BEGIN
    PRINT 'Table LABOUR_ARISING_APPLICATIONS already exists.';
END
GO
