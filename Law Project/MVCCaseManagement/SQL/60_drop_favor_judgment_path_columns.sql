-- Migration 60: Safely drop FavorJudgmentPath from MVC_CASES and MVC_REMIND_BACK_CASES
-- As court judgments are now fetched live via eCourts NAPIX Gateway

BEGIN TRANSACTION;

BEGIN TRY
    -- 1. Check and drop from MVC_CASES
    IF EXISTS (
        SELECT 1 
        FROM INFORMATION_SCHEMA.COLUMNS 
        WHERE TABLE_NAME = 'MVC_CASES' AND COLUMN_NAME = 'FavorJudgmentPath'
    )
    BEGIN
        ALTER TABLE MVC_CASES DROP COLUMN FavorJudgmentPath;
        PRINT 'FavorJudgmentPath dropped from MVC_CASES.';
    END
    ELSE
    BEGIN
        PRINT 'FavorJudgmentPath does not exist in MVC_CASES.';
    END

    -- 2. Check and drop from MVC_REMIND_BACK_CASES
    IF EXISTS (
        SELECT 1 
        FROM INFORMATION_SCHEMA.COLUMNS 
        WHERE TABLE_NAME = 'MVC_REMIND_BACK_CASES' AND COLUMN_NAME = 'FavorJudgmentPath'
    )
    BEGIN
        ALTER TABLE MVC_REMIND_BACK_CASES DROP COLUMN FavorJudgmentPath;
        PRINT 'FavorJudgmentPath dropped from MVC_REMIND_BACK_CASES.';
    END
    ELSE
    BEGIN
        PRINT 'FavorJudgmentPath does not exist in MVC_REMIND_BACK_CASES.';
    END

    COMMIT TRANSACTION;
    PRINT 'Migration 60 completed successfully.';
END TRY
BEGIN CATCH
    ROLLBACK TRANSACTION;
    PRINT 'Error during migration 60: ' + ERROR_MESSAGE();
    THROW;
END CATCH;
