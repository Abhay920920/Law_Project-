-- ============================================================
-- 62_add_module_to_napix_api_calls.sql
-- Add Module column to NAPIX_API_CALLS to support dual NAPIX apps
-- (MVC App = 1,000 calls/hr, Labour App = 1,000 calls/hr)
-- ============================================================

IF EXISTS (SELECT 1 FROM sys.tables WHERE name = 'NAPIX_API_CALLS')
BEGIN
    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.NAPIX_API_CALLS') AND name = 'Module')
    BEGIN
        ALTER TABLE dbo.NAPIX_API_CALLS ADD Module NVARCHAR(20) NOT NULL DEFAULT 'MVC';
    END

    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_NAPIX_API_CALLS_Module_Hour')
    BEGIN
        CREATE NONCLUSTERED INDEX IX_NAPIX_API_CALLS_Module_Hour
            ON dbo.NAPIX_API_CALLS (Module, CalledAt) INCLUDE (Endpoint, IsSuccess);
    END
END
GO

IF OBJECT_ID('dbo.usp_GetNapixQuotaSummary','P') IS NOT NULL DROP PROCEDURE dbo.usp_GetNapixQuotaSummary
GO

CREATE PROCEDURE dbo.usp_GetNapixQuotaSummary
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @HourStart  DATETIME2 = DATEADD(HOUR, -1, SYSUTCDATETIME());   -- rolling 1-hr window
    DECLARE @MonthStart DATETIME2 = DATEADD(DAY, 1 - DAY(GETUTCDATE()), CAST(CAST(GETUTCDATE() AS DATE) AS DATETIME2));

    -- 1. Combined System Quota (2,000 calls/hr total across MVC + Labour NAPIX Apps)
    SELECT
        COUNT(*)                                                AS TotalCallsThisHour,
        ISNULL(SUM(CASE WHEN IsSuccess=1 THEN 1 ELSE 0 END), 0) AS SuccessfulCalls,
        ISNULL(SUM(CASE WHEN IsSuccess=0 THEN 1 ELSE 0 END), 0) AS FailedCalls,
        2000                                                    AS HourlyQuota,
        2000 - COUNT(*)                                         AS RemainingQuota,
        CAST(COUNT(*) * 100.0 / 2000 AS DECIMAL(5,1))          AS UsedPercent
    FROM dbo.NAPIX_API_CALLS
    WHERE CalledAt >= @HourStart;

    -- 2. Per-Module Breakdown (MVC App vs Labour App - 1,000 calls/hr each)
    SELECT
        m.ModuleName                                            AS Module,
        ISNULL(c.TotalCalls, 0)                                 AS TotalCallsThisHour,
        ISNULL(c.SuccessfulCalls, 0)                            AS SuccessfulCalls,
        ISNULL(c.FailedCalls, 0)                                AS FailedCalls,
        1000                                                    AS HourlyQuota,
        1000 - ISNULL(c.TotalCalls, 0)                          AS RemainingQuota,
        CAST(ISNULL(c.TotalCalls, 0) * 100.0 / 1000 AS DECIMAL(5,1)) AS UsedPercent
    FROM (VALUES ('MVC'), ('Labour')) AS m(ModuleName)
    LEFT JOIN (
        SELECT
            UPPER(Module) AS ModuleName,
            COUNT(*) AS TotalCalls,
            ISNULL(SUM(CASE WHEN IsSuccess=1 THEN 1 ELSE 0 END), 0) AS SuccessfulCalls,
            ISNULL(SUM(CASE WHEN IsSuccess=0 THEN 1 ELSE 0 END), 0) AS FailedCalls
        FROM dbo.NAPIX_API_CALLS
        WHERE CalledAt >= @HourStart
        GROUP BY UPPER(Module)
    ) c ON m.ModuleName = c.ModuleName;

    -- 3. Per-endpoint breakdown — rolling hour
    SELECT
        Endpoint,
        Module,
        COUNT(*)                                                AS TotalCalls,
        ISNULL(SUM(CASE WHEN IsSuccess=1 THEN 1 ELSE 0 END), 0) AS SuccessCalls,
        ISNULL(SUM(CASE WHEN IsSuccess=0 THEN 1 ELSE 0 END), 0) AS FailedCalls,
        ISNULL(AVG(DurationMs), 0)                              AS AvgDurationMs
    FROM dbo.NAPIX_API_CALLS
    WHERE CalledAt >= @HourStart
    GROUP BY Endpoint, Module
    ORDER BY TotalCalls DESC;

    -- 4. Per-minute trend — last 60 minutes
    SELECT
        DATEPART(MINUTE, DATEADD(HOUR,5,DATEADD(MINUTE,30,CalledAt))) AS MinuteIST,
        DATEPART(HOUR,   DATEADD(HOUR,5,DATEADD(MINUTE,30,CalledAt))) AS HourIST,
        CAST(DATEADD(HOUR,5,DATEADD(MINUTE,30,CalledAt)) AS DATE)     AS DateIST,
        COUNT(*)                                                        AS CallCount
    FROM dbo.NAPIX_API_CALLS
    WHERE CalledAt >= @HourStart
    GROUP BY
        DATEPART(MINUTE, DATEADD(HOUR,5,DATEADD(MINUTE,30,CalledAt))),
        DATEPART(HOUR,   DATEADD(HOUR,5,DATEADD(MINUTE,30,CalledAt))),
        CAST(DATEADD(HOUR,5,DATEADD(MINUTE,30,CalledAt)) AS DATE)
    ORDER BY DateIST, HourIST, MinuteIST;

    -- 5. Hourly trend — last 24 hours
    SELECT
        DATEPART(HOUR, DATEADD(HOUR,5,DATEADD(MINUTE,30,CalledAt))) AS HourIST,
        CAST(DATEADD(HOUR,5,DATEADD(MINUTE,30,CalledAt)) AS DATE)   AS DateIST,
        COUNT(*)                                                      AS CallCount
    FROM dbo.NAPIX_API_CALLS
    WHERE CalledAt >= DATEADD(HOUR,-24,SYSUTCDATETIME())
    GROUP BY
        DATEPART(HOUR,DATEADD(HOUR,5,DATEADD(MINUTE,30,CalledAt))),
        CAST(DATEADD(HOUR,5,DATEADD(MINUTE,30,CalledAt)) AS DATE)
    ORDER BY DateIST, HourIST;

    -- 6. Recent 50 calls
    SELECT TOP 50
        CallID,
        Module,
        DATEADD(HOUR,5,DATEADD(MINUTE,30,CalledAt)) AS CalledAtIST,
        Endpoint, HttpStatus, IsSuccess, Username, CNRNumber, DurationMs
    FROM dbo.NAPIX_API_CALLS
    ORDER BY CallID DESC;
END
GO
