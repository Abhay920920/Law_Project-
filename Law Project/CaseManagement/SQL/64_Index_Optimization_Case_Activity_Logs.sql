-- ==============================================================================================
-- Script: 64_Index_Optimization_Case_Activity_Logs.sql
-- Description: Composite index on CASE_ACTIVITY_LOGS (DivisionID ASC, Timestamp DESC)
--              covering paginated activity log queries and eliminating high-cost table scans.
-- ==============================================================================================

SET NOCOUNT ON;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_CASE_ACTIVITY_LOGS_Division_Timestamp' AND object_id = OBJECT_ID('dbo.CASE_ACTIVITY_LOGS'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_CASE_ACTIVITY_LOGS_Division_Timestamp]
    ON [dbo].[CASE_ACTIVITY_LOGS] ([DivisionID] ASC, [Timestamp] DESC)
    INCLUDE ([Module], [CaseID], [CaseNumber], [ActionType], [Username], [IpAddress]);
END;
GO

