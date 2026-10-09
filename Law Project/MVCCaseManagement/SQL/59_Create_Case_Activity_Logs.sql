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
