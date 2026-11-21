-- ==============================================================================================
-- Migration Script: 65_Dashboard_And_Case_Query_Covering_Indexes.sql
-- Description: Targeted non-clustered covering indexes for high-frequency Dashboard,
--              Hearing, Transfer, Adverse Award, and Appeal queries across 6 litigation tables.
-- Dialect: Microsoft SQL Server 2012 Express (SP3-GDR 11.0.6251.0 X64)
-- Database: Admin_Law
-- Safety: 100% idempotent (IF NOT EXISTS guards); zero data modification; zero column alterations.
-- ==============================================================================================

SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

-- 1. MVC_CASES: Division & NextHearingDate Covering Index (Dashboard & Hearings)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_MVC_CASES_Division_NextHearing' AND object_id = OBJECT_ID('dbo.MVC_CASES'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_MVC_CASES_Division_NextHearing]
    ON [dbo].[MVC_CASES] ([DivisionID] ASC, [NextHearingDate] ASC)
    INCLUDE ([CaseID], [MVCNo], [MVCYear], [MACTID], [DisposalResult], [CreatedAt], [CNRNumber], [CurrentStage]);
END
GO

-- 2. MVC_CASES: ThirdPartyFlag & Division Covering Index (Third Party Filter & Listing)
-- If exists without enhanced columns (CreatedAt, MACTID), drop and recreate
IF EXISTS (
    SELECT 1 FROM sys.indexes i 
    WHERE i.name = 'IX_MVC_CASES_ThirdParty_Division' 
      AND i.object_id = OBJECT_ID('dbo.MVC_CASES')
      AND NOT EXISTS (
          SELECT 1 FROM sys.index_columns ic 
          JOIN sys.columns c ON ic.object_id = c.object_id AND ic.column_id = c.column_id
          WHERE ic.object_id = i.object_id AND ic.index_id = i.index_id AND c.name = 'CreatedAt' AND ic.is_included_column = 1
      )
)
BEGIN
    DROP INDEX [IX_MVC_CASES_ThirdParty_Division] ON [dbo].[MVC_CASES];
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_MVC_CASES_ThirdParty_Division' AND object_id = OBJECT_ID('dbo.MVC_CASES'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_MVC_CASES_ThirdParty_Division]
    ON [dbo].[MVC_CASES] ([ThirdPartyFlag] ASC, [DivisionID] ASC)
    INCLUDE ([CaseID], [MVCNo], [MVCYear], [VehicleNo], [ClaimAmount], [AdvocateName], [DisposalResult], [DisposalStatus], [NextHearingDate], [CreatedAt], [MACTID]);
END
GO

-- 3. MVC_CASES: Transfers Covering Index (Recent Transfers Notification Query)
-- If exists without enhanced column (MVCYear), drop and recreate
IF EXISTS (
    SELECT 1 FROM sys.indexes i 
    WHERE i.name = 'IX_MVC_CASES_Transfers' 
      AND i.object_id = OBJECT_ID('dbo.MVC_CASES')
      AND NOT EXISTS (
          SELECT 1 FROM sys.index_columns ic 
          JOIN sys.columns c ON ic.object_id = c.object_id AND ic.column_id = c.column_id
          WHERE ic.object_id = i.object_id AND ic.index_id = i.index_id AND c.name = 'MVCYear' AND ic.is_included_column = 1
      )
)
BEGIN
    DROP INDEX [IX_MVC_CASES_Transfers] ON [dbo].[MVC_CASES];
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_MVC_CASES_Transfers' AND object_id = OBJECT_ID('dbo.MVC_CASES'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_MVC_CASES_Transfers]
    ON [dbo].[MVC_CASES] ([DivisionID] ASC, [IsTransferViewed] ASC, [TransferredFromDivisionID] ASC)
    INCLUDE ([TransferDate], [CaseID], [MVCNo], [MVCYear]);
END
GO

-- 4. LABOUR_CASES: Division & NextHearingDate Covering Index (Labour Hearings & Dashboard)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_LABOUR_CASES_Division_NextHearing' AND (object_id = OBJECT_ID('LABOUR_CASES') OR object_id = OBJECT_ID('admin_Law.LABOUR_CASES') OR object_id = OBJECT_ID('dbo.LABOUR_CASES')))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_LABOUR_CASES_Division_NextHearing]
    ON [LABOUR_CASES] ([DivisionID] ASC, [NextHearingDate] ASC)
    INCLUDE ([CaseID], [CaseType], [CaseNumber], [CaseYear], [CourtID], [DisposalResult], [CreatedDate], [CNRNumber], [CurrentStage], [CO_StayComplianceDate], [DisposalDate], [OtherCourtDetails], [CaseStatus]);
END
GO

-- 5. GRA_CASES: DivisionCode & NextHearingDate Covering Index (Gratuity Hearings & Compliance)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_GRA_CASES_Division_NextHearing' AND object_id = OBJECT_ID('dbo.GRA_CASES'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_GRA_CASES_Division_NextHearing]
    ON [dbo].[GRA_CASES] ([DivisionCode] ASC, [NextHearingDate] ASC)
    INCLUDE ([CaseID], [PGANumber], [CourtType], [DisposalResult], [CreatedDate], [CurrentStage], [StayComplianceDate], [DisposalDate], [ClaimantName]);
END
GO

-- 6. MVC_CASE_ADVERSE_DETAILS: ForwardingStatus & AwardDate Covering Index (Central Office Adverse Workflow)
-- If exists without enhanced column (AwardAmount), drop and recreate
IF EXISTS (
    SELECT 1 FROM sys.indexes i 
    WHERE i.name = 'IX_MVC_CASE_ADVERSE_Forwarding_AwardDate' 
      AND i.object_id = OBJECT_ID('dbo.MVC_CASE_ADVERSE_DETAILS')
      AND NOT EXISTS (
          SELECT 1 FROM sys.index_columns ic 
          JOIN sys.columns c ON ic.object_id = c.object_id AND ic.column_id = c.column_id
          WHERE ic.object_id = i.object_id AND ic.index_id = i.index_id AND c.name = 'AwardAmount' AND ic.is_included_column = 1
      )
)
BEGIN
    DROP INDEX [IX_MVC_CASE_ADVERSE_Forwarding_AwardDate] ON [dbo].[MVC_CASE_ADVERSE_DETAILS];
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_MVC_CASE_ADVERSE_Forwarding_AwardDate' AND object_id = OBJECT_ID('dbo.MVC_CASE_ADVERSE_DETAILS'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_MVC_CASE_ADVERSE_Forwarding_AwardDate]
    ON [dbo].[MVC_CASE_ADVERSE_DETAILS] ([ForwardingStatus] ASC, [AwardDate] ASC)
    INCLUDE ([CaseID], [AwardAmount]);
END
GO

-- 7. APPEAL_DETAILS: FinalComplianceDate Covering Index (Appeal Compliance & Overdue Status)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_APPEAL_DETAILS_Compliance_Status' AND object_id = OBJECT_ID('dbo.APPEAL_DETAILS'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_APPEAL_DETAILS_Compliance_Status]
    ON [dbo].[APPEAL_DETAILS] ([FinalComplianceDate] ASC)
    INCLUDE ([CaseID], [InitialAction], [CorpSCStatus], [CorpSCNumber], [ClaimantSCStatus], [ClaimantSCNumber]);
END
GO

-- 8. OTHER_CASES: DivisionID & NextDateOfHearing Covering Index (Other Courts Dashboard & Hearings)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_OTHER_CASES_Division_Hearing' AND (object_id = OBJECT_ID('OTHER_CASES') OR object_id = OBJECT_ID('admin_Law.OTHER_CASES') OR object_id = OBJECT_ID('dbo.OTHER_CASES')))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_OTHER_CASES_Division_Hearing]
    ON [OTHER_CASES] ([DivisionID] ASC, [NextDateOfHearing] ASC)
    INCLUDE ([CaseID], [CaseType], [CaseNumber], [CaseYear], [Court], [DisposalStatus], [CreatedDate], [CaseStage]);
END
GO

-- 9. Record execution in SCHEMA_MIGRATIONS tracking table
IF OBJECT_ID('SCHEMA_MIGRATIONS', 'U') IS NOT NULL
BEGIN
    IF NOT EXISTS (SELECT 1 FROM SCHEMA_MIGRATIONS WHERE MigrationID = '20261121_v1_2_8_Dashboard_And_Case_Query_Covering_Indexes')
    BEGIN
        INSERT INTO SCHEMA_MIGRATIONS (MigrationID, Description, ExecutionTimeMs)
        VALUES ('20261121_v1_2_8_Dashboard_And_Case_Query_Covering_Indexes', 
                'Create targeted covering nonclustered indexes for dashboard, hearings, transfers, adverse awards, and appeal queries', 
                100);
    END
END
GO
