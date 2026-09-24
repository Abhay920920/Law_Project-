-- Create Labour Courts Master Table
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'LABOUR_COURTS')
BEGIN
    CREATE TABLE LABOUR_COURTS (
        CourtID INT IDENTITY(1,1) PRIMARY KEY,
        CourtName NVARCHAR(200) NOT NULL, -- Labour Court, Industrial Tribunal, etc.
        Location NVARCHAR(100) NULL,
        IsActive BIT DEFAULT 1
    );

    -- Insert Default Courts
    INSERT INTO LABOUR_COURTS (CourtName, Location) VALUES 
    ('Labour Court', 'Hubballi'),
    ('Industrial Tribunal', 'Hubballi'),
    ('District Court', 'Hubballi'),
    ('Industrial Tribunal', 'Bengaluru'),
    ('Additional Labour Court', 'Hubballi');
END

-- Create Labour Cases Table
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'LABOUR_CASES')
BEGIN
    CREATE TABLE LABOUR_CASES (
        CaseID INT IDENTITY(1,1) PRIMARY KEY,
        DivisionID INT NULL, -- Division where notice is received
        
        -- Basic Details
        CaseType NVARCHAR(50) NOT NULL, -- KID, ID, REF, Arising, Direct, Complaint, Serial
        CaseStatus NVARCHAR(50) NOT NULL, -- Pending, Disposed, DNP, Ex-parte
        
        -- Court & Case Identification
        CaseNumber NVARCHAR(100) NOT NULL,
        CaseYear INT NOT NULL,
        CourtID INT NULL,
        OtherCourtDetails NVARCHAR(200) NULL, -- If court is not in master
        
        -- Employee / Petitioner Details
        PetitionerName NVARCHAR(200) NOT NULL,
        EmployeeNo NVARCHAR(50) NULL,
        Designation NVARCHAR(100) NULL,
        WorkingStatus NVARCHAR(50) NULL, -- Regular, Trainee, Retired, VRS, Death
        
        -- Nature of Case
        NatureOfCase NVARCHAR(100) NULL, -- Dismissal, Removal, Reduction, etc.
        NatureOfMisconduct NVARCHAR(MAX) NULL,
        
        -- Legal Details
        EntrustmentNo NVARCHAR(100) NULL,
        EntrustmentDate DATETIME NULL,
        AdvocateID INT NULL, -- Link to Master Advocate
        AdvocateName NVARCHAR(200) NULL, -- Manual entry if not in master
        IsDoubleClaim BIT DEFAULT 0,
        
        -- Case Progress
        CurrentStage NVARCHAR(200) NULL,
        CaseHistory NVARCHAR(MAX) NULL,
        
        -- Disposal Details
        DisposalMode NVARCHAR(50) NULL, -- Disposed, DNP, Lok Adalat
        DisposalDate DATETIME NULL,
        DisposalResult NVARCHAR(50) NULL, -- In Favour, Against
        
        -- If Settled in Lok Adalat
        LokAdalat_COApprovalRequired BIT DEFAULT 0,
        LokAdalat_OutwardNo NVARCHAR(100) NULL,
        LokAdalat_Date DATETIME NULL,
        
        -- Work After Disposal (If Against)
        Against_CaseCategory NVARCHAR(50) NULL, -- ABS, DFL, NINC, etc.
        Against_BriefFacts NVARCHAR(MAX) NULL,
        Against_PunishmentImposed NVARCHAR(100) NULL,
        Against_PunishmentNo NVARCHAR(100) NULL,
        Against_PunishmentDate DATETIME NULL,
        
        -- Departmental Enquiry (DE) Details
        DE_HistorySheet BIT DEFAULT 0,
        DE_ObjectionsFiled BIT DEFAULT 0,
        DE_DocumentsMarked NVARCHAR(MAX) NULL,
        DE_Order NVARCHAR(50) NULL, -- Proved / Not Proved
        DE_EO_Name NVARCHAR(200) NULL,
        DE_EO_Designation NVARCHAR(100) NULL,
        DE_Reporter_Name NVARCHAR(200) NULL,
        DE_Reporter_Designation NVARCHAR(100) NULL,
        
        -- Certified Copy Details
        CC_PublicationDate DATETIME NULL,
        CC_AppliedDate DATETIME NULL,
        CC_IssuedDate DATETIME NULL,
        CC_ReceivedDate DATETIME NULL,
        CC_Remarks NVARCHAR(MAX) NULL,
        
        -- Opinion & Awards
        AwardDetails NVARCHAR(MAX) NULL,
        Opinion_Advocate NVARCHAR(MAX) NULL,
        Opinion_LO NVARCHAR(MAX) NULL,
        Opinion_DC NVARCHAR(MAX) NULL,
        
        -- CO Communication
        SentToCO BIT DEFAULT 0,
        CO_OutwardNo NVARCHAR(100) NULL,
        CO_OutwardDate DATETIME NULL,
        
        -- Arising Application (If this case IS an Arising App)
        IsArisingApplication BIT DEFAULT 0,
        Arising_OriginalCaseNumber NVARCHAR(100) NULL,
        Arising_OriginalCaseYear INT NULL,
        Arising_OriginalCourt NVARCHAR(200) NULL,
        
        -- Meta
        CreatedDate DATETIME DEFAULT GETDATE(),
        CreatedBy INT NULL,
        ModifiedDate DATETIME NULL,
        ModifiedBy INT NULL
    );
END
