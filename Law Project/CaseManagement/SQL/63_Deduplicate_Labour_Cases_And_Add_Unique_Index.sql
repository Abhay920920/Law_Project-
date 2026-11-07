-- ==============================================================================================
-- Script: 63_Deduplicate_Labour_Cases_And_Add_Unique_Index.sql
-- Description: Transactional deduplication of LABOUR_CASES with child-table foreign key repointing
--              and creation of a UNIQUE filtered index to prevent future duplicates permanently.
-- Target DB: CaseManagement / Law Project (SQL Server)
-- ==============================================================================================

SET NOCOUNT ON;
BEGIN TRANSACTION;

BEGIN TRY
    PRINT '>>> [Step 1] Identifying duplicate Labour cases...';

    IF OBJECT_ID('tempdb..#LabourDups') IS NOT NULL 
        DROP TABLE #LabourDups;

    CREATE TABLE #LabourDups (
        DupCaseID INT NOT NULL PRIMARY KEY,
        MasterCaseID INT NOT NULL,
        DivisionID INT NOT NULL,
        CaseNumber NVARCHAR(100),
        CaseYear INT
    );

    -- Identify duplicate records partitioned by DivisionID, CourtID, CaseYear, and normalized CaseNumber
    ;WITH CleanCases AS (
        SELECT 
            CaseID,
            DivisionID,
            CourtID,
            CaseYear,
            CaseNumber,
            CNRNumber,
            CreatedDate,
            ROW_NUMBER() OVER (
                PARTITION BY DivisionID, CourtID, CaseYear, REPLACE(LTRIM(RTRIM(CaseNumber)), ' ', '')
                ORDER BY 
                    CASE WHEN CNRNumber IS NOT NULL AND LTRIM(RTRIM(CNRNumber)) <> '' THEN 0 ELSE 1 END,
                    CreatedDate DESC, 
                    CaseID DESC
            ) AS RowNum,
            FIRST_VALUE(CaseID) OVER (
                PARTITION BY DivisionID, CourtID, CaseYear, REPLACE(LTRIM(RTRIM(CaseNumber)), ' ', '')
                ORDER BY 
                    CASE WHEN CNRNumber IS NOT NULL AND LTRIM(RTRIM(CNRNumber)) <> '' THEN 0 ELSE 1 END,
                    CreatedDate DESC, 
                    CaseID DESC
            ) AS MasterID
        FROM LABOUR_CASES
        WHERE CaseNumber IS NOT NULL 
          AND LTRIM(RTRIM(CaseNumber)) <> ''
          AND CourtID IS NOT NULL
          AND CaseYear IS NOT NULL
    )
    INSERT INTO #LabourDups (DupCaseID, MasterCaseID, DivisionID, CaseNumber, CaseYear)
    SELECT CaseID, MasterID, DivisionID, CaseNumber, CaseYear
    FROM CleanCases
    WHERE RowNum > 1;

    -- Also detect duplicates with identical CNRNumber within the same Division (if not already captured)
    ;WITH CnrCases AS (
        SELECT 
            CaseID,
            DivisionID,
            CaseNumber,
            CaseYear,
            CNRNumber,
            ROW_NUMBER() OVER (
                PARTITION BY DivisionID, UPPER(LTRIM(RTRIM(CNRNumber)))
                ORDER BY CreatedDate DESC, CaseID DESC
            ) AS RowNum,
            FIRST_VALUE(CaseID) OVER (
                PARTITION BY DivisionID, UPPER(LTRIM(RTRIM(CNRNumber)))
                ORDER BY CreatedDate DESC, CaseID DESC
            ) AS MasterID
        FROM LABOUR_CASES
        WHERE CNRNumber IS NOT NULL 
          AND LEN(LTRIM(RTRIM(CNRNumber))) = 16
          AND CaseID NOT IN (SELECT DupCaseID FROM #LabourDups)
    )
    INSERT INTO #LabourDups (DupCaseID, MasterCaseID, DivisionID, CaseNumber, CaseYear)
    SELECT c.CaseID, c.MasterID, c.DivisionID, c.CaseNumber, c.CaseYear
    FROM CnrCases c
    WHERE c.RowNum > 1
      AND c.CaseID NOT IN (SELECT DupCaseID FROM #LabourDups);

    DECLARE @DupCount INT = (SELECT COUNT(*) FROM #LabourDups);
    PRINT '>>> Found ' + CAST(@DupCount AS VARCHAR(10)) + ' duplicate Labour Case record(s) to merge.';

    IF @DupCount > 0
    BEGIN
        SELECT 
            d.DupCaseID AS Duplicate_CaseID_To_Delete,
            d.MasterCaseID AS Master_CaseID_Preserved,
            d.DivisionID,
            d.CaseNumber,
            d.CaseYear
        FROM #LabourDups d;

        PRINT '>>> [Step 2] Repointing child tables...';

        -- 2.1 LABOUR_CASE_VIEW_TRACKING
        IF OBJECT_ID('LABOUR_CASE_VIEW_TRACKING', 'U') IS NOT NULL
        BEGIN
            DELETE vt
            FROM LABOUR_CASE_VIEW_TRACKING vt
            INNER JOIN #LabourDups d ON vt.CaseID = d.DupCaseID
            WHERE EXISTS (
                SELECT 1 FROM LABOUR_CASE_VIEW_TRACKING m 
                WHERE m.CaseID = d.MasterCaseID
            );

            UPDATE vt
            SET vt.CaseID = d.MasterCaseID
            FROM LABOUR_CASE_VIEW_TRACKING vt
            INNER JOIN #LabourDups d ON vt.CaseID = d.DupCaseID;
            PRINT '    - Repointed LABOUR_CASE_VIEW_TRACKING';
        END

        -- 2.2 LABOUR_SERVICE_MATTERS
        IF OBJECT_ID('LABOUR_SERVICE_MATTERS', 'U') IS NOT NULL
        BEGIN
            UPDATE s
            SET s.CaseID = d.MasterCaseID
            FROM LABOUR_SERVICE_MATTERS s
            INNER JOIN #LabourDups d ON s.CaseID = d.DupCaseID;
            PRINT '    - Repointed LABOUR_SERVICE_MATTERS';
        END

        -- 2.3 LABOUR_EP_DETAILS
        IF OBJECT_ID('LABOUR_EP_DETAILS', 'U') IS NOT NULL
        BEGIN
            UPDATE ep
            SET ep.CaseID = d.MasterCaseID
            FROM LABOUR_EP_DETAILS ep
            INNER JOIN #LabourDups d ON ep.CaseID = d.DupCaseID;
            PRINT '    - Repointed LABOUR_EP_DETAILS';
        END

        -- 2.4 LABOUR_ARISING_APPLICATIONS
        IF OBJECT_ID('LABOUR_ARISING_APPLICATIONS', 'U') IS NOT NULL
        BEGIN
            UPDATE a
            SET a.ParentCaseID = d.MasterCaseID
            FROM LABOUR_ARISING_APPLICATIONS a
            INNER JOIN #LabourDups d ON a.ParentCaseID = d.DupCaseID;
            PRINT '    - Repointed LABOUR_ARISING_APPLICATIONS';
        END

        -- 2.5 LABOUR_CASE_HISTORY
        IF OBJECT_ID('LABOUR_CASE_HISTORY', 'U') IS NOT NULL
        BEGIN
            UPDATE h
            SET h.CaseID = d.MasterCaseID
            FROM LABOUR_CASE_HISTORY h
            INNER JOIN #LabourDups d ON h.CaseID = d.DupCaseID;
            PRINT '    - Repointed LABOUR_CASE_HISTORY';
        END

        -- 2.6 LABOUR_CASE_EVIDENCE
        IF OBJECT_ID('LABOUR_CASE_EVIDENCE', 'U') IS NOT NULL
        BEGIN
            UPDATE e
            SET e.CaseID = d.MasterCaseID
            FROM LABOUR_CASE_EVIDENCE e
            INNER JOIN #LabourDups d ON e.CaseID = d.DupCaseID;
            PRINT '    - Repointed LABOUR_CASE_EVIDENCE';
        END

        -- 2.7 LABOUR_CONNECTED_CASES
        IF OBJECT_ID('LABOUR_CONNECTED_CASES', 'U') IS NOT NULL
        BEGIN
            UPDATE cc
            SET cc.CaseID = d.MasterCaseID
            FROM LABOUR_CONNECTED_CASES cc
            INNER JOIN #LabourDups d ON cc.CaseID = d.DupCaseID;
            PRINT '    - Repointed LABOUR_CONNECTED_CASES';
        END

        -- 2.8 LABOUR_REINSTATED_DOCUMENTS
        IF OBJECT_ID('LABOUR_REINSTATED_DOCUMENTS', 'U') IS NOT NULL
        BEGIN
            UPDATE rd
            SET rd.CaseID = d.MasterCaseID
            FROM LABOUR_REINSTATED_DOCUMENTS rd
            INNER JOIN #LabourDups d ON rd.CaseID = d.DupCaseID;
            PRINT '    - Repointed LABOUR_REINSTATED_DOCUMENTS';
        END

        -- 2.9 LABOUR_ENCLOSED_DOCS
        IF OBJECT_ID('LABOUR_ENCLOSED_DOCS', 'U') IS NOT NULL
        BEGIN
            UPDATE ed
            SET ed.CaseID = d.MasterCaseID
            FROM LABOUR_ENCLOSED_DOCS ed
            INNER JOIN #LabourDups d ON ed.CaseID = d.DupCaseID;
            PRINT '    - Repointed LABOUR_ENCLOSED_DOCS';
        END

        -- 2.10 CASE_NOTINGS
        IF OBJECT_ID('CASE_NOTINGS', 'U') IS NOT NULL
        BEGIN
            UPDATE cn
            SET cn.CaseID = d.MasterCaseID
            FROM CASE_NOTINGS cn
            INNER JOIN #LabourDups d ON cn.CaseType = 'LABOUR' AND cn.CaseID = d.DupCaseID;
            PRINT '    - Repointed CASE_NOTINGS';
        END

        -- Step 3: Delete duplicate cases from LABOUR_CASES
        PRINT '>>> [Step 3] Deleting redundant duplicate cases from LABOUR_CASES...';
        DELETE c
        FROM LABOUR_CASES c
        INNER JOIN #LabourDups d ON c.CaseID = d.DupCaseID;
        PRINT '    - Deleted ' + CAST(@@ROWCOUNT AS VARCHAR(10)) + ' duplicate row(s) from LABOUR_CASES.';
    END

    IF OBJECT_ID('tempdb..#LabourDups') IS NOT NULL 
        DROP TABLE #LabourDups;

    -- Step 4: Create Unique Filtered Index to permanently block duplicate insertions
    PRINT '>>> [Step 4] Ensuring unique index UX_LABOUR_CASES_Div_Court_Year_Num exists...';
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_LABOUR_CASES_Div_Court_Year_Num' AND object_id = OBJECT_ID('LABOUR_CASES'))
    BEGIN
        CREATE UNIQUE NONCLUSTERED INDEX UX_LABOUR_CASES_Div_Court_Year_Num
        ON LABOUR_CASES (DivisionID, CourtID, CaseYear, CaseNumber)
        WHERE CaseNumber IS NOT NULL AND CourtID IS NOT NULL AND CaseYear IS NOT NULL;
        PRINT '    - Created index UX_LABOUR_CASES_Div_Court_Year_Num successfully.';
    END
    ELSE
    BEGIN
        PRINT '    - Index UX_LABOUR_CASES_Div_Court_Year_Num already exists.';
    END

    -- Step 5: Record in SCHEMA_MIGRATIONS tracking table if present
    IF OBJECT_ID('SCHEMA_MIGRATIONS', 'U') IS NOT NULL
    BEGIN
        IF NOT EXISTS (SELECT 1 FROM SCHEMA_MIGRATIONS WHERE MigrationID = '20261107_v1_2_6_Deduplicate_Labour_Cases_And_Add_Unique_Index')
        BEGIN
            INSERT INTO SCHEMA_MIGRATIONS (MigrationID, Description, ExecutionTimeMs)
            VALUES ('20261107_v1_2_6_Deduplicate_Labour_Cases_And_Add_Unique_Index', 
                    'Deduplicate existing LABOUR_CASES, repoint child tables, and create unique filtered index on (DivisionID, CourtID, CaseYear, CaseNumber)', 
                    100);
            PRINT '    - Recorded migration in SCHEMA_MIGRATIONS.';
        END
    END

    COMMIT TRANSACTION;
    PRINT '>>> DEDUPLICATION & INDEX CREATION COMPLETED SUCCESSFULLY! <<<';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;

    IF OBJECT_ID('tempdb..#LabourDups') IS NOT NULL 
        DROP TABLE #LabourDups;

    PRINT '!!! ERROR OCCURRED: ' + ERROR_MESSAGE();
    THROW;
END CATCH;
