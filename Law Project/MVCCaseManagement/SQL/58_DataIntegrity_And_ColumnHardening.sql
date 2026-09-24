-- ==============================================================================
-- Migration Script: 58_DataIntegrity_And_ColumnHardening.sql
-- Description: Hardens database schema for ACID data integrity and zero data loss:
--              1. Expands outward and reference number columns to NVARCHAR(150)
--                 to prevent truncation errors on lengthy office dispatch strings.
--              2. Ensures ModifiedDate and ModifiedBy audit columns exist.
--              3. Creates missing index on MVC_CASE_OPPOSITE_VEHICLES if needed.
-- ==============================================================================

SET NOCOUNT ON;

-- 1. Hardening MVC_CASES
IF EXISTS (SELECT * FROM sys.tables WHERE name = 'MVC_CASES')
BEGIN
    IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('MVC_CASES') AND name = 'ObjectionOutwardNo')
        ALTER TABLE MVC_CASES ALTER COLUMN ObjectionOutwardNo NVARCHAR(150) NULL;

    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('MVC_CASES') AND name = 'ModifiedDate')
        ALTER TABLE MVC_CASES ADD ModifiedDate DATETIME NULL;

    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('MVC_CASES') AND name = 'ModifiedBy')
        ALTER TABLE MVC_CASES ADD ModifiedBy NVARCHAR(100) NULL;
END

-- 2. Hardening MVC_CASE_ADVERSE_DETAILS
IF EXISTS (SELECT * FROM sys.tables WHERE name = 'MVC_CASE_ADVERSE_DETAILS')
BEGIN
    IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('MVC_CASE_ADVERSE_DETAILS') AND name = 'OutwardNumber')
        ALTER TABLE MVC_CASE_ADVERSE_DETAILS ALTER COLUMN OutwardNumber NVARCHAR(150) NULL;

    IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('MVC_CASE_ADVERSE_DETAILS') AND name = 'EPNumber')
        ALTER TABLE MVC_CASE_ADVERSE_DETAILS ALTER COLUMN EPNumber NVARCHAR(150) NULL;
END

-- 3. Hardening LABOUR_CASES
IF EXISTS (SELECT * FROM sys.tables WHERE name = 'LABOUR_CASES')
BEGIN
    IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('LABOUR_CASES') AND name = 'COApproval_OutwardNo')
        ALTER TABLE LABOUR_CASES ALTER COLUMN COApproval_OutwardNo NVARCHAR(150) NULL;

    IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('LABOUR_CASES') AND name = 'DocumentSent_OutwardNo')
        ALTER TABLE LABOUR_CASES ALTER COLUMN DocumentSent_OutwardNo NVARCHAR(150) NULL;

    IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('LABOUR_CASES') AND name = 'ObjectionFiled_OutwardNo')
        ALTER TABLE LABOUR_CASES ALTER COLUMN ObjectionFiled_OutwardNo NVARCHAR(150) NULL;

    IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('LABOUR_CASES') AND name = 'LokAdalat_OutwardNo')
        ALTER TABLE LABOUR_CASES ALTER COLUMN LokAdalat_OutwardNo NVARCHAR(150) NULL;

    IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('LABOUR_CASES') AND name = 'CO_OutwardNo')
        ALTER TABLE LABOUR_CASES ALTER COLUMN CO_OutwardNo NVARCHAR(150) NULL;

    IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('LABOUR_CASES') AND name = 'CO_ApprovalOutwardNo')
        ALTER TABLE LABOUR_CASES ALTER COLUMN CO_ApprovalOutwardNo NVARCHAR(150) NULL;

    IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('LABOUR_CASES') AND name = 'CO_WP_OutcomeOutwardNo')
        ALTER TABLE LABOUR_CASES ALTER COLUMN CO_WP_OutcomeOutwardNo NVARCHAR(150) NULL;

    IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('LABOUR_CASES') AND name = 'CO_Disposal_OutwardNo')
        ALTER TABLE LABOUR_CASES ALTER COLUMN CO_Disposal_OutwardNo NVARCHAR(150) NULL;

    IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('LABOUR_CASES') AND name = 'CO_Disposal_ApprovalOutwardNo')
        ALTER TABLE LABOUR_CASES ALTER COLUMN CO_Disposal_ApprovalOutwardNo NVARCHAR(150) NULL;

    IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('LABOUR_CASES') AND name = 'CO_FurtherAppeal_DisposalOutwardNo')
        ALTER TABLE LABOUR_CASES ALTER COLUMN CO_FurtherAppeal_DisposalOutwardNo NVARCHAR(150) NULL;

    IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('LABOUR_CASES') AND name = 'CO_Service_ApprovalOutwardNo')
        ALTER TABLE LABOUR_CASES ALTER COLUMN CO_Service_ApprovalOutwardNo NVARCHAR(150) NULL;

    IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('LABOUR_CASES') AND name = 'CO_Service_OutwardNo')
        ALTER TABLE LABOUR_CASES ALTER COLUMN CO_Service_OutwardNo NVARCHAR(150) NULL;

    IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('LABOUR_CASES') AND name = 'CO_WA_OutcomeOutwardNo')
        ALTER TABLE LABOUR_CASES ALTER COLUMN CO_WA_OutcomeOutwardNo NVARCHAR(150) NULL;

    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('LABOUR_CASES') AND name = 'ModifiedDate')
        ALTER TABLE LABOUR_CASES ADD ModifiedDate DATETIME NULL;

    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('LABOUR_CASES') AND name = 'ModifiedBy')
        ALTER TABLE LABOUR_CASES ADD ModifiedBy NVARCHAR(100) NULL;
END

-- 4. Hardening GRA_CASES
IF EXISTS (SELECT * FROM sys.tables WHERE name = 'GRA_CASES')
BEGIN
    IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('GRA_CASES') AND name = 'DocumentOutwardNo')
        ALTER TABLE GRA_CASES ALTER COLUMN DocumentOutwardNo NVARCHAR(150) NULL;

    IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('GRA_CASES') AND name = 'ObjectionOutwardNo')
        ALTER TABLE GRA_CASES ALTER COLUMN ObjectionOutwardNo NVARCHAR(150) NULL;

    IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('GRA_CASES') AND name = 'ApprovalOutwardNo')
        ALTER TABLE GRA_CASES ALTER COLUMN ApprovalOutwardNo NVARCHAR(150) NULL;

    IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('GRA_CASES') AND name = 'OutwardNumber')
        ALTER TABLE GRA_CASES ALTER COLUMN OutwardNumber NVARCHAR(150) NULL;

    IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('GRA_CASES') AND name = 'OutwardNumber_Appeal')
        ALTER TABLE GRA_CASES ALTER COLUMN OutwardNumber_Appeal NVARCHAR(150) NULL;

    IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('GRA_CASES') AND name = 'StayComplianceOutwardNo')
        ALTER TABLE GRA_CASES ALTER COLUMN StayComplianceOutwardNo NVARCHAR(150) NULL;

    IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('GRA_CASES') AND name = 'AppealActionOutwardNo')
        ALTER TABLE GRA_CASES ALTER COLUMN AppealActionOutwardNo NVARCHAR(150) NULL;

    IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('GRA_CASES') AND name = 'ClosureOutwardNo')
        ALTER TABLE GRA_CASES ALTER COLUMN ClosureOutwardNo NVARCHAR(150) NULL;

    IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('GRA_CASES') AND name = 'ClaimantApprovalNo')
        ALTER TABLE GRA_CASES ALTER COLUMN ClaimantApprovalNo NVARCHAR(150) NULL;

    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('GRA_CASES') AND name = 'ModifiedDate')
        ALTER TABLE GRA_CASES ADD ModifiedDate DATETIME NULL;

    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('GRA_CASES') AND name = 'ModifiedBy')
        ALTER TABLE GRA_CASES ADD ModifiedBy NVARCHAR(100) NULL;
END

-- 5. Hardening MVC_CASE_OPPOSITE_VEHICLES Table and Index
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'MVC_CASE_OPPOSITE_VEHICLES')
BEGIN
    CREATE TABLE MVC_CASE_OPPOSITE_VEHICLES (
        OppositeVehicleID INT IDENTITY(1,1) PRIMARY KEY,
        CaseID INT NOT NULL,
        VehicleNo NVARCHAR(50) NOT NULL,
        CreatedDate DATETIME DEFAULT GETDATE(),
        CONSTRAINT FK_MVC_OppositeVehicles_CaseID FOREIGN KEY (CaseID) REFERENCES MVC_CASES(CaseID) ON DELETE CASCADE
    );
END

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_MVC_OppositeVehicles_CaseID' AND object_id = OBJECT_ID('MVC_CASE_OPPOSITE_VEHICLES'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_MVC_OppositeVehicles_CaseID ON MVC_CASE_OPPOSITE_VEHICLES(CaseID);
END
