-- =============================================================================
-- Migration: 59_Create_Case_Activity_Logs.sql
-- Purpose: Enterprise Case Activity & Audit Log Sheet
-- Description: Tracks all case operations across NWKRTC (Created, Edited, Deleted, Transferred, Payments, Notings)
-- Ensures complete data integrity and non-destructive audit retention.
-- =============================================================================

IF NOT EXISTS (SELECT 1 FROM sys.tables t JOIN sys.schemas s ON t.schema_id = s.schema_id WHERE t.name = 'CASE_ACTIVITY_LOGS' AND s.name = 'dbo')
BEGIN
    CREATE TABLE [dbo].[CASE_ACTIVITY_LOGS] (
        [LogID] BIGINT IDENTITY(1,1) NOT NULL,
        [Timestamp] DATETIME NOT NULL CONSTRAINT [DF_CASE_ACTIVITY_LOGS_Timestamp] DEFAULT (GETDATE()),
        
        -- User & Division Identification
        [UserID] INT NULL,
        [Username] NVARCHAR(100) NOT NULL,
        [UserFullName] NVARCHAR(150) NULL,
        [UserRole] NVARCHAR(50) NULL,
        [DivisionID] INT NULL,
        [DivisionName] NVARCHAR(100) NULL,
        [IpAddress] NVARCHAR(50) NULL,
        
        -- Case Classification
        [Module] NVARCHAR(30) NOT NULL,            -- 'MVC', 'LABOUR', 'APPEAL', 'EP', 'GRATUITY', 'MASTER'
        [CaseID] INT NULL,
        [CaseNumber] NVARCHAR(100) NOT NULL,        -- 'MVC/931/2016', 'KID 12/2021', etc.
        [VehicleNo] NVARCHAR(50) NULL,
        [CourtName] NVARCHAR(150) NULL,
        
        -- Action Metadata
        [ActionType] NVARCHAR(30) NOT NULL,         -- 'CREATED', 'UPDATED', 'DELETED', 'TRANSFERRED', 'PAYMENT_ADDED', 'PAYMENT_DELETED', 'NOTING_ADDED', 'NOTING_DELETED'
        [ActionSummary] NVARCHAR(500) NOT NULL,     -- Human-readable short summary
        [ChangedFieldsSummary] NVARCHAR(MAX) NULL,  -- e.g. "Stage: [Pending → Evidence] | Claim: [₹5,00,000 → ₹7,50,000]"
        
        -- Full Audit State Payload (JSON)
        [OldValuesJson] NVARCHAR(MAX) NULL,
        [NewValuesJson] NVARCHAR(MAX) NULL,

        CONSTRAINT [PK_CASE_ACTIVITY_LOGS] PRIMARY KEY CLUSTERED ([LogID])
    );

    -- Optimized Indexes for Fast Dashboard Filtering
    CREATE NONCLUSTERED INDEX [IX_ACTIVITY_LOGS_Timestamp] ON [dbo].[CASE_ACTIVITY_LOGS] ([Timestamp] DESC);
    CREATE NONCLUSTERED INDEX [IX_ACTIVITY_LOGS_Division_Action] ON [dbo].[CASE_ACTIVITY_LOGS] ([DivisionID], [ActionType], [Timestamp] DESC);
    CREATE NONCLUSTERED INDEX [IX_ACTIVITY_LOGS_CaseNumber] ON [dbo].[CASE_ACTIVITY_LOGS] ([CaseNumber]);
    CREATE NONCLUSTERED INDEX [IX_ACTIVITY_LOGS_Module_Date] ON [dbo].[CASE_ACTIVITY_LOGS] ([Module], [Timestamp] DESC);

    PRINT 'Table [dbo].[CASE_ACTIVITY_LOGS] created successfully with performance indexes.';
END
ELSE
BEGIN
    PRINT 'Table [dbo].[CASE_ACTIVITY_LOGS] already exists.';
END
GO

-- Seed Baseline Activity History if empty
IF (SELECT COUNT(*) FROM [dbo].[CASE_ACTIVITY_LOGS]) = 0
BEGIN
    PRINT 'Seeding baseline historical activity from existing MVC and Labour cases...';
    
    INSERT INTO [dbo].[CASE_ACTIVITY_LOGS] (
        [Timestamp], [UserID], [Username], [UserFullName], [UserRole],
        [DivisionID], [DivisionName], [IpAddress], [Module], [CaseID],
        [CaseNumber], [VehicleNo], [CourtName], [ActionType], [ActionSummary],
        [ChangedFieldsSummary]
    )
    SELECT TOP 150
        COALESCE(c.UpdatedAt, c.CreatedAt, c.AccidentDate, GETDATE()) AS [Timestamp],
        c.ModifiedBy AS [UserID],
        COALESCE(u.Username, 'law_div_user') AS [Username],
        COALESCE(u.FullName, 'Division Legal Officer') AS [UserFullName],
        'Division User' AS [UserRole],
        c.DivisionID,
        d.DivisionNameEnglish AS [DivisionName],
        '127.0.0.1' AS [IpAddress],
        'MVC' AS [Module],
        c.CaseID,
        CONCAT('MVC ', c.MVCNo, '/', c.MVCYear) AS [CaseNumber],
        c.VehicleNo,
        COALESCE(c.CourtHall, m.MACTName, 'MACT Court') AS [CourtName],
        'CREATED' AS [ActionType],
        CONCAT('Case registered: MVC No. ', c.MVCNo, '/', c.MVCYear, ' (Vehicle: ', ISNULL(c.VehicleNo, 'N/A'), ', Stage: ', ISNULL(c.CurrentStage, 'Pending'), ')') AS [ActionSummary],
        'Initial Case Registration' AS [ChangedFieldsSummary]
    FROM MVC_CASES c
    LEFT JOIN USERS u ON c.ModifiedBy = u.UserID
    LEFT JOIN DIVISION_MASTER d ON c.DivisionID = d.DivisionID
    LEFT JOIN MACT_MASTER m ON c.MACTID = m.MACTID
    ORDER BY c.CaseID DESC;

    INSERT INTO [dbo].[CASE_ACTIVITY_LOGS] (
        [Timestamp], [UserID], [Username], [UserFullName], [UserRole],
        [DivisionID], [DivisionName], [IpAddress], [Module], [CaseID],
        [CaseNumber], [VehicleNo], [CourtName], [ActionType], [ActionSummary],
        [ChangedFieldsSummary]
    )
    SELECT TOP 150
        COALESCE(l.ModifiedDate, l.CreatedDate, GETDATE()) AS [Timestamp],
        l.CreatedBy AS [UserID],
        COALESCE(u.Username, 'labour_user') AS [Username],
        COALESCE(u.FullName, 'Labour Section In-Charge') AS [UserFullName],
        'Division User' AS [UserRole],
        l.DivisionID,
        d.DivisionNameEnglish AS [DivisionName],
        '127.0.0.1' AS [IpAddress],
        'LABOUR' AS [Module],
        l.CaseID,
        l.CaseNumber,
        NULL AS [VehicleNo],
        COALESCE(l.OtherCourtDetails, 'Labour Court') AS [CourtName],
        'CREATED' AS [ActionType],
        CONCAT('Labour case registered: ', l.CaseNumber, ' (Petitioner: ', ISNULL(l.PetitionerName, 'Workman'), ', Stage: ', ISNULL(l.CurrentStage, 'Pending'), ')') AS [ActionSummary],
        'Initial Case Registration' AS [ChangedFieldsSummary]
    FROM LABOUR_CASES l
    LEFT JOIN USERS u ON l.CreatedBy = u.UserID
    LEFT JOIN DIVISION_MASTER d ON l.DivisionID = d.DivisionID
    ORDER BY l.CaseID DESC;

    PRINT 'Baseline historical activity seeded successfully.';
END
GO
