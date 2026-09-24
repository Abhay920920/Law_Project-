-- Create stored procedure for Dashboard statistics
GO

IF OBJECT_ID('dbo.sp_GetDashboardStats', 'P') IS NOT NULL
    DROP PROCEDURE dbo.sp_GetDashboardStats;
GO

CREATE PROCEDURE sp_GetDashboardStats
    @DivisionID INT = 0
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @TotalCases INT;
    DECLARE @PendingCases INT;
    DECLARE @FavorCases INT;
    DECLARE @AgainstCases INT;

    -- Calculate stats
    SELECT 
        @TotalCases = COUNT(*),
        @PendingCases = SUM(CASE WHEN ISNULL(DisposalResult, '') = '' OR DisposalResult = 'Pending' THEN 1 ELSE 0 END),
        @FavorCases = SUM(CASE WHEN DisposalResult = 'Favor' THEN 1 ELSE 0 END),
        @AgainstCases = SUM(CASE WHEN DisposalResult = 'Against' THEN 1 ELSE 0 END)
    FROM MVC_CASES
    WHERE (@DivisionID = 0 OR @DivisionID = 5 OR DivisionID = @DivisionID);

    -- Return stats as a result set
    SELECT 
        ISNULL(@TotalCases, 0) AS TotalCases,
        ISNULL(@PendingCases, 0) AS PendingCases,
        ISNULL(@FavorCases, 0) AS FavorCases,
        ISNULL(@AgainstCases, 0) AS AgainstCases;

    -- Return Recent 5 Cases
    SELECT TOP 5
        c.CaseID, c.MVCNo, c.MACTID, m.MACTName, c.DisposalResult, c.CreatedAt
    FROM MVC_CASES c
    JOIN MACT_MASTER m ON c.MACTID = m.MACTID
    WHERE (@DivisionID = 0 OR @DivisionID = 5 OR c.DivisionID = @DivisionID)
    ORDER BY c.CreatedAt DESC;
END
GO
