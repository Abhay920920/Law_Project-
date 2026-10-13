-- ============================================================
-- 61_create_napix_api_quota.sql  (updated: hourly quota)
-- NAPIX API quota tracking table and helper sproc
-- Run once on the production database
-- ============================================================

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'NAPIX_API_CALLS')
BEGIN
    CREATE TABLE dbo.NAPIX_API_CALLS (
        CallID          BIGINT          IDENTITY(1,1) PRIMARY KEY,
        CalledAt        DATETIME2       NOT NULL DEFAULT SYSUTCDATETIME(),
        Endpoint        NVARCHAR(120)   NOT NULL,
        HttpStatus      INT             NULL,
        IsSuccess       BIT             NOT NULL DEFAULT 0,
        Username        NVARCHAR(100)   NULL,
        CNRNumber       NVARCHAR(30)    NULL,
        DurationMs      INT             NULL
    );

    CREATE NONCLUSTERED INDEX IX_NAPIX_API_CALLS_Hour
        ON dbo.NAPIX_API_CALLS (CalledAt) INCLUDE (Endpoint, IsSuccess);
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

    -- 1. Rolling-hour totals (primary quota window)
    SELECT
        COUNT(*)                                              AS TotalCallsThisHour,
        ISNULL(SUM(CASE WHEN IsSuccess=1 THEN 1 ELSE 0 END), 0) AS SuccessfulCalls,
        ISNULL(SUM(CASE WHEN IsSuccess=0 THEN 1 ELSE 0 END), 0) AS FailedCalls,
        1000                                                  AS HourlyQuota,
        1000 - COUNT(*)                                       AS RemainingQuota,
        CAST(COUNT(*) * 100.0 / 1000 AS DECIMAL(5,1))        AS UsedPercent
    FROM dbo.NAPIX_API_CALLS
    WHERE CalledAt >= @HourStart;

    -- 2. Per-endpoint breakdown — rolling hour
    SELECT
        Endpoint,
        COUNT(*)                                              AS TotalCalls,
        ISNULL(SUM(CASE WHEN IsSuccess=1 THEN 1 ELSE 0 END), 0) AS SuccessCalls,
        ISNULL(SUM(CASE WHEN IsSuccess=0 THEN 1 ELSE 0 END), 0) AS FailedCalls,
        ISNULL(AVG(DurationMs), 0)                            AS AvgDurationMs
    FROM dbo.NAPIX_API_CALLS
    WHERE CalledAt >= @HourStart
    GROUP BY Endpoint
    ORDER BY TotalCalls DESC;

    -- 3. Per-minute trend — last 60 minutes (for the live sparkline)
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

    -- 4. Hourly trend — last 24 hours (for historical bar chart)
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

    -- 5. Recent 50 calls
    SELECT TOP 50
        CallID,
        DATEADD(HOUR,5,DATEADD(MINUTE,30,CalledAt)) AS CalledAtIST,
        Endpoint, HttpStatus, IsSuccess, Username, CNRNumber, DurationMs
    FROM dbo.NAPIX_API_CALLS
    ORDER BY CallID DESC;
END
GO
