using System;
using System.Data;
using System.Diagnostics;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace MVCCaseManagement.DAL
{
    /// <summary>
    /// Versioned database migration runner with distributed application locking (sp_getapplock).
    /// Eliminates slow startup schema introspection by recording executed migrations in SCHEMA_MIGRATIONS.
    /// </summary>
    public static class DatabaseMigrationRunner
    {
        public static void RunMigrations(DBHelper db, ILogger logger)
        {
            var stopwatch = Stopwatch.StartNew();
            logger.LogInformation("Checking database schema migrations...");

            try
            {
                using var conn = db.GetConnection();
                conn.Open();

                // 1. Ensure SCHEMA_MIGRATIONS tracking table exists
                string ensureTableSql = @"
                    IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'SCHEMA_MIGRATIONS')
                    BEGIN
                        CREATE TABLE SCHEMA_MIGRATIONS (
                            MigrationID NVARCHAR(200) PRIMARY KEY,
                            Description NVARCHAR(500) NOT NULL,
                            AppliedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
                            ExecutionTimeMs INT NOT NULL
                        );
                    END";

                using (var cmd = new SqlCommand(ensureTableSql, conn))
                {
                    cmd.ExecuteNonQuery();
                }

                // 2. Acquire distributed application lock to prevent multi-instance race conditions
                bool lockAcquired = false;
                try
                {
                    using (var lockCmd = new SqlCommand("sp_getapplock", conn))
                    {
                        lockCmd.CommandType = CommandType.StoredProcedure;
                        lockCmd.Parameters.AddWithValue("@Resource", "LawProject_Database_Migration_Lock");
                        lockCmd.Parameters.AddWithValue("@LockMode", "Exclusive");
                        lockCmd.Parameters.AddWithValue("@LockOwner", "Session");
                        lockCmd.Parameters.AddWithValue("@LockTimeout", 15000); // 15 seconds

                        var returnParam = lockCmd.Parameters.Add("@Result", SqlDbType.Int);
                        returnParam.Direction = ParameterDirection.ReturnValue;

                        lockCmd.ExecuteNonQuery();
                        int lockResult = (int)returnParam.Value;
                        lockAcquired = lockResult >= 0; // 0 = granted, 1 = granted after waiting
                    }

                    if (!lockAcquired)
                    {
                        logger.LogWarning("Another application instance is currently executing database migrations. Skipping migration check on this instance.");
                        return;
                    }

                    // 3. Check if baseline migration has already executed
                    const string baselineMigrationId = "V1_0_0_Baseline_Schema_Hardening";
                    bool isBaselineApplied = false;

                    using (var checkCmd = new SqlCommand("SELECT COUNT(*) FROM SCHEMA_MIGRATIONS WHERE MigrationID = @id", conn))
                    {
                        checkCmd.Parameters.Add(new SqlParameter("@id", SqlDbType.NVarChar, 200) { Value = baselineMigrationId });
                        int count = (int)(checkCmd.ExecuteScalar() ?? 0);
                        isBaselineApplied = count > 0;
                    }

                    if (!isBaselineApplied)
                    {
                        logger.LogInformation("Executing baseline schema migrations (DBMigration.EnsureAll)...");
                        var baselineTimer = Stopwatch.StartNew();

                        DBMigration.EnsureAll(db);

                        baselineTimer.Stop();

                        // Record completion in SCHEMA_MIGRATIONS
                        string recordSql = @"
                            INSERT INTO SCHEMA_MIGRATIONS (MigrationID, Description, ExecutionTimeMs)
                            VALUES (@id, @desc, @time)";

                        using (var recordCmd = new SqlCommand(recordSql, conn))
                        {
                            recordCmd.Parameters.Add(new SqlParameter("@id", SqlDbType.NVarChar, 200) { Value = baselineMigrationId });
                            recordCmd.Parameters.Add(new SqlParameter("@desc", SqlDbType.NVarChar, 500) { Value = "Complete baseline schema checks, tables, and column hardening" });
                            recordCmd.Parameters.Add(new SqlParameter("@time", SqlDbType.Int) { Value = (int)baselineTimer.ElapsedMilliseconds });
                            recordCmd.ExecuteNonQuery();
                        }

                        logger.LogInformation("Baseline schema migration completed and recorded in {Elapsed}ms.", baselineTimer.ElapsedMilliseconds);
                    }
                    else
                    {
                        logger.LogInformation("Database schema is up to date ({MigrationId} verified in {Elapsed}ms). Skipping schema introspection.",
                            baselineMigrationId, stopwatch.ElapsedMilliseconds);
                    }

                    // 4. Versioned Migration: V1_1_0_MVC_LiveECourts_Status_Columns
                    const string v1_1_0_LiveECourtsMigrationId = "V1_1_0_MVC_LiveECourts_Status_Columns";
                    bool isV110Applied = false;
                    using (var checkCmd2 = new SqlCommand("SELECT COUNT(*) FROM SCHEMA_MIGRATIONS WHERE MigrationID = @id", conn))
                    {
                        checkCmd2.Parameters.Add(new SqlParameter("@id", SqlDbType.NVarChar, 200) { Value = v1_1_0_LiveECourtsMigrationId });
                        isV110Applied = (int)(checkCmd2.ExecuteScalar() ?? 0) > 0;
                    }

                    if (!isV110Applied)
                    {
                        logger.LogInformation("Applying migration {MigrationId}...", v1_1_0_LiveECourtsMigrationId);
                        var migTimer = Stopwatch.StartNew();

                        string alterSql = @"
                            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'MVC_CASES' AND COLUMN_NAME = 'CaseStatus')
                                ALTER TABLE MVC_CASES ADD CaseStatus NVARCHAR(100) NULL;

                            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'MVC_CASES' AND COLUMN_NAME = 'CourtHall')
                                ALTER TABLE MVC_CASES ADD CourtHall NVARCHAR(150) NULL;

                            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'MVC_CASES' AND COLUMN_NAME = 'CNRNumber')
                                ALTER TABLE MVC_CASES ADD CNRNumber NVARCHAR(16) NULL;

                            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'MVC_CASES' AND COLUMN_NAME = 'EstCode')
                                ALTER TABLE MVC_CASES ADD EstCode NVARCHAR(50) NULL;

                            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'MVC_CASES' AND COLUMN_NAME = 'CaseTypeCode')
                                ALTER TABLE MVC_CASES ADD CaseTypeCode NVARCHAR(50) NULL;
                        ";
                        using (var alterCmd = new SqlCommand(alterSql, conn))
                        {
                            alterCmd.ExecuteNonQuery();
                        }

                        migTimer.Stop();

                        string recordSql2 = @"
                            INSERT INTO SCHEMA_MIGRATIONS (MigrationID, Description, ExecutionTimeMs)
                            VALUES (@id, @desc, @time)";
                        using (var recordCmd2 = new SqlCommand(recordSql2, conn))
                        {
                            recordCmd2.Parameters.Add(new SqlParameter("@id", SqlDbType.NVarChar, 200) { Value = v1_1_0_LiveECourtsMigrationId });
                            recordCmd2.Parameters.Add(new SqlParameter("@desc", SqlDbType.NVarChar, 500) { Value = "Add CaseStatus, CourtHall, CNRNumber, EstCode to MVC_CASES for live NAPIX court synchronization" });
                            recordCmd2.Parameters.Add(new SqlParameter("@time", SqlDbType.Int) { Value = (int)migTimer.ElapsedMilliseconds });
                            recordCmd2.ExecuteNonQuery();
                        }
                        logger.LogInformation("Migration {MigrationId} applied successfully in {Elapsed}ms.", v1_1_0_LiveECourtsMigrationId, migTimer.ElapsedMilliseconds);
                    }

                    // 5. Versioned Migration: V1_1_1_MVC_ModifiedDate_Column
                    const string v1_1_1_ModifiedDateMigrationId = "V1_1_1_MVC_ModifiedDate_Column";
                    bool isV111Applied = false;
                    using (var checkCmd3 = new SqlCommand("SELECT COUNT(*) FROM SCHEMA_MIGRATIONS WHERE MigrationID = @id", conn))
                    {
                        checkCmd3.Parameters.Add(new SqlParameter("@id", SqlDbType.NVarChar, 200) { Value = v1_1_1_ModifiedDateMigrationId });
                        isV111Applied = (int)(checkCmd3.ExecuteScalar() ?? 0) > 0;
                    }

                    if (!isV111Applied)
                    {
                        logger.LogInformation("Applying migration {MigrationId}...", v1_1_1_ModifiedDateMigrationId);
                        var migTimer2 = Stopwatch.StartNew();

                        string alterSql2 = @"
                            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'MVC_CASES' AND COLUMN_NAME = 'ModifiedDate')
                                ALTER TABLE MVC_CASES ADD ModifiedDate DATETIME NULL;

                            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'MVC_CASES' AND COLUMN_NAME = 'ModifiedBy')
                                ALTER TABLE MVC_CASES ADD ModifiedBy INT NULL;
                        ";
                        using (var alterCmd2 = new SqlCommand(alterSql2, conn))
                        {
                            alterCmd2.ExecuteNonQuery();
                        }

                        migTimer2.Stop();

                        string recordSql3 = @"
                            INSERT INTO SCHEMA_MIGRATIONS (MigrationID, Description, ExecutionTimeMs)
                            VALUES (@id, @desc, @time)";
                        using (var recordCmd3 = new SqlCommand(recordSql3, conn))
                        {
                            recordCmd3.Parameters.Add(new SqlParameter("@id", SqlDbType.NVarChar, 200) { Value = v1_1_1_ModifiedDateMigrationId });
                            recordCmd3.Parameters.Add(new SqlParameter("@desc", SqlDbType.NVarChar, 500) { Value = "Add ModifiedDate and ModifiedBy columns to MVC_CASES" });
                            recordCmd3.Parameters.Add(new SqlParameter("@time", SqlDbType.Int) { Value = (int)migTimer2.ElapsedMilliseconds });
                            recordCmd3.ExecuteNonQuery();
                        }
                        logger.LogInformation("Migration {MigrationId} applied successfully in {Elapsed}ms.", v1_1_1_ModifiedDateMigrationId, migTimer2.ElapsedMilliseconds);
                    }

                    // 6. Versioned Migration: V1_1_2_Add_Dy_CLO_And_LO_Roles_And_Users
                    const string v1_1_2_DyCloLoMigrationId = "V1_1_2_Add_Dy_CLO_And_LO_Roles_And_Users";
                    bool isV112Applied = false;
                    using (var checkCmd4 = new SqlCommand("SELECT COUNT(*) FROM SCHEMA_MIGRATIONS WHERE MigrationID = @id", conn))
                    {
                        checkCmd4.Parameters.Add(new SqlParameter("@id", SqlDbType.NVarChar, 200) { Value = v1_1_2_DyCloLoMigrationId });
                        isV112Applied = (int)(checkCmd4.ExecuteScalar() ?? 0) > 0;
                    }

                    if (!isV112Applied)
                    {
                        logger.LogInformation("Applying migration {MigrationId}...", v1_1_2_DyCloLoMigrationId);
                        var migTimer3 = Stopwatch.StartNew();

                        string seedSql = @"
                            IF NOT EXISTS (SELECT 1 FROM ROLE_MASTER WHERE RoleName = 'LO')
                                INSERT INTO ROLE_MASTER (RoleName, IsActive) VALUES ('LO', 1);

                            IF NOT EXISTS (SELECT 1 FROM ROLE_MASTER WHERE RoleName = 'Dy CLO')
                                INSERT INTO ROLE_MASTER (RoleName, IsActive) VALUES ('Dy CLO', 1);

                            DECLARE @LoRoleID INT = (SELECT RoleID FROM ROLE_MASTER WHERE RoleName = 'LO');
                            DECLARE @DyCloRoleID INT = (SELECT RoleID FROM ROLE_MASTER WHERE RoleName = 'Dy CLO');
                            DECLARE @DefaultPasswordHash NVARCHAR(256) = '8D969EEF6ECAD3C29A3A629280E686CF0C3F5D5A86AFF3CA12020C923ADC6C92';

                            IF NOT EXISTS (SELECT 1 FROM USERS WHERE Username = 'dy_clo')
                                INSERT INTO USERS (Username, PasswordHash, FullName, Email, Mobile, RoleID, DivisionID, IsActive, CreatedDate)
                                VALUES ('dy_clo', @DefaultPasswordHash, 'Deputy Chief Law Officer', 'dyclo@nwkrtc.in', '9876543210', @DyCloRoleID, 5, 1, GETDATE());

                            IF NOT EXISTS (SELECT 1 FROM USERS WHERE Username = 'dyclo')
                                INSERT INTO USERS (Username, PasswordHash, FullName, Email, Mobile, RoleID, DivisionID, IsActive, CreatedDate)
                                VALUES ('dyclo', @DefaultPasswordHash, 'Deputy Chief Law Officer', 'dyclo@nwkrtc.in', '9876543210', @DyCloRoleID, 5, 1, GETDATE());

                            IF NOT EXISTS (SELECT 1 FROM USERS WHERE Username = 'lo')
                                INSERT INTO USERS (Username, PasswordHash, FullName, Email, Mobile, RoleID, DivisionID, IsActive, CreatedDate)
                                VALUES ('lo', @DefaultPasswordHash, 'Law Officer', 'lo@nwkrtc.in', '9876543211', @LoRoleID, 5, 1, GETDATE());
                        ";
                        using (var seedCmd = new SqlCommand(seedSql, conn))
                        {
                            seedCmd.ExecuteNonQuery();
                        }

                        migTimer3.Stop();

                        string recordSql4 = @"
                            INSERT INTO SCHEMA_MIGRATIONS (MigrationID, Description, ExecutionTimeMs)
                            VALUES (@id, @desc, @time)";
                        using (var recordCmd4 = new SqlCommand(recordSql4, conn))
                        {
                            recordCmd4.Parameters.Add(new SqlParameter("@id", SqlDbType.NVarChar, 200) { Value = v1_1_2_DyCloLoMigrationId });
                            recordCmd4.Parameters.Add(new SqlParameter("@desc", SqlDbType.NVarChar, 500) { Value = "Add LO and Dy CLO roles to ROLE_MASTER and seed users dy_clo, dyclo, and lo" });
                            recordCmd4.Parameters.Add(new SqlParameter("@time", SqlDbType.Int) { Value = (int)migTimer3.ElapsedMilliseconds });
                            recordCmd4.ExecuteNonQuery();
                        }
                        logger.LogInformation("Migration {MigrationId} applied successfully in {Elapsed}ms.", v1_1_2_DyCloLoMigrationId, migTimer3.ElapsedMilliseconds);
                    }

                    // 7. Versioned Migration: V1_1_3_Role_Action_Columns_LO_DyCLO_CLO_MD
                    const string v1_1_3_RoleActionMigrationId = "V1_1_3_Role_Action_Columns_LO_DyCLO_CLO_MD";
                    bool isV113Applied = false;
                    using (var checkCmd5 = new SqlCommand("SELECT COUNT(*) FROM SCHEMA_MIGRATIONS WHERE MigrationID = @id", conn))
                    {
                        checkCmd5.Parameters.Add(new SqlParameter("@id", SqlDbType.NVarChar, 200) { Value = v1_1_3_RoleActionMigrationId });
                        isV113Applied = (int)(checkCmd5.ExecuteScalar() ?? 0) > 0;
                    }

                    if (!isV113Applied)
                    {
                        logger.LogInformation("Applying migration {MigrationId}...", v1_1_3_RoleActionMigrationId);
                        var migTimer4 = Stopwatch.StartNew();

                        string alterRoleColumnsSql = @"
                            -- APPEAL_DETAILS: per-role action columns
                            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'APPEAL_DETAILS' AND COLUMN_NAME = 'ActionTaken_LO')
                                ALTER TABLE APPEAL_DETAILS ADD ActionTaken_LO NVARCHAR(100) NULL;

                            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'APPEAL_DETAILS' AND COLUMN_NAME = 'ApprovalDate_LO')
                                ALTER TABLE APPEAL_DETAILS ADD ApprovalDate_LO DATE NULL;

                            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'APPEAL_DETAILS' AND COLUMN_NAME = 'Opinion_LO')
                                ALTER TABLE APPEAL_DETAILS ADD Opinion_LO NVARCHAR(MAX) NULL;

                            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'APPEAL_DETAILS' AND COLUMN_NAME = 'ActionTaken_DyCLO')
                                ALTER TABLE APPEAL_DETAILS ADD ActionTaken_DyCLO NVARCHAR(100) NULL;

                            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'APPEAL_DETAILS' AND COLUMN_NAME = 'ApprovalDate_DyCLO')
                                ALTER TABLE APPEAL_DETAILS ADD ApprovalDate_DyCLO DATE NULL;

                            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'APPEAL_DETAILS' AND COLUMN_NAME = 'Opinion_DyCLO')
                                ALTER TABLE APPEAL_DETAILS ADD Opinion_DyCLO NVARCHAR(MAX) NULL;

                            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'APPEAL_DETAILS' AND COLUMN_NAME = 'ActionTaken_CLO')
                                ALTER TABLE APPEAL_DETAILS ADD ActionTaken_CLO NVARCHAR(100) NULL;

                            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'APPEAL_DETAILS' AND COLUMN_NAME = 'ApprovalDate_CLO')
                                ALTER TABLE APPEAL_DETAILS ADD ApprovalDate_CLO DATE NULL;

                            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'APPEAL_DETAILS' AND COLUMN_NAME = 'ActionTaken_MD')
                                ALTER TABLE APPEAL_DETAILS ADD ActionTaken_MD NVARCHAR(100) NULL;

                            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'APPEAL_DETAILS' AND COLUMN_NAME = 'ApprovalDate_MD')
                                ALTER TABLE APPEAL_DETAILS ADD ApprovalDate_MD DATE NULL;

                            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'APPEAL_DETAILS' AND COLUMN_NAME = 'Opinion_MD')
                                ALTER TABLE APPEAL_DETAILS ADD Opinion_MD NVARCHAR(MAX) NULL;

                            -- LABOUR_CASES: per-role action columns
                            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'LABOUR_CASES' AND COLUMN_NAME = 'ActionTaken_LO')
                                ALTER TABLE LABOUR_CASES ADD ActionTaken_LO NVARCHAR(100) NULL;

                            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'LABOUR_CASES' AND COLUMN_NAME = 'ApprovalDate_LO')
                                ALTER TABLE LABOUR_CASES ADD ApprovalDate_LO DATE NULL;

                            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'LABOUR_CASES' AND COLUMN_NAME = 'ActionTaken_DyCLO')
                                ALTER TABLE LABOUR_CASES ADD ActionTaken_DyCLO NVARCHAR(100) NULL;

                            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'LABOUR_CASES' AND COLUMN_NAME = 'ApprovalDate_DyCLO')
                                ALTER TABLE LABOUR_CASES ADD ApprovalDate_DyCLO DATE NULL;

                            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'LABOUR_CASES' AND COLUMN_NAME = 'Opinion_DyCLO')
                                ALTER TABLE LABOUR_CASES ADD Opinion_DyCLO NVARCHAR(MAX) NULL;

                            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'LABOUR_CASES' AND COLUMN_NAME = 'ActionTaken_CLO')
                                ALTER TABLE LABOUR_CASES ADD ActionTaken_CLO NVARCHAR(100) NULL;

                            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'LABOUR_CASES' AND COLUMN_NAME = 'ApprovalDate_CLO')
                                ALTER TABLE LABOUR_CASES ADD ApprovalDate_CLO DATE NULL;

                            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'LABOUR_CASES' AND COLUMN_NAME = 'ActionTaken_MD')
                                ALTER TABLE LABOUR_CASES ADD ActionTaken_MD NVARCHAR(100) NULL;

                            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'LABOUR_CASES' AND COLUMN_NAME = 'ApprovalDate_MD')
                                ALTER TABLE LABOUR_CASES ADD ApprovalDate_MD DATE NULL;

                            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'LABOUR_CASES' AND COLUMN_NAME = 'Opinion_MD')
                                ALTER TABLE LABOUR_CASES ADD Opinion_MD NVARCHAR(MAX) NULL;
                        ";

                        using (var alterCmd = new SqlCommand(alterRoleColumnsSql, conn))
                        {
                            alterCmd.ExecuteNonQuery();
                        }

                        migTimer4.Stop();

                        string recordSql5 = @"
                            INSERT INTO SCHEMA_MIGRATIONS (MigrationID, Description, ExecutionTimeMs)
                            VALUES (@id, @desc, @time)";
                        using (var recordCmd5 = new SqlCommand(recordSql5, conn))
                        {
                            recordCmd5.Parameters.Add(new SqlParameter("@id", SqlDbType.NVarChar, 200) { Value = v1_1_3_RoleActionMigrationId });
                            recordCmd5.Parameters.Add(new SqlParameter("@desc", SqlDbType.NVarChar, 500) { Value = "Add per-role action columns for LO, DyCLO, CLO, MD to APPEAL_DETAILS and LABOUR_CASES" });
                            recordCmd5.Parameters.Add(new SqlParameter("@time", SqlDbType.Int) { Value = (int)migTimer4.ElapsedMilliseconds });
                            recordCmd5.ExecuteNonQuery();
                        }
                        logger.LogInformation("Migration {MigrationId} applied successfully in {Elapsed}ms.", v1_1_3_RoleActionMigrationId, migTimer4.ElapsedMilliseconds);
                    }

                    // 7. Versioned Migration: V1_1_4_Dual_Napix_App_Quota_Tracking
                    const string v1_1_4_DualNapixMigrationId = "V1_1_4_Dual_Napix_App_Quota_Tracking";
                    bool isV114Applied = false;
                    using (var checkCmd5 = new SqlCommand("SELECT COUNT(*) FROM SCHEMA_MIGRATIONS WHERE MigrationID = @id", conn))
                    {
                        checkCmd5.Parameters.Add(new SqlParameter("@id", SqlDbType.NVarChar, 200) { Value = v1_1_4_DualNapixMigrationId });
                        isV114Applied = (int)(checkCmd5.ExecuteScalar() ?? 0) > 0;
                    }

                    if (!isV114Applied)
                    {
                        logger.LogInformation("Applying migration {MigrationId}...", v1_1_4_DualNapixMigrationId);
                        var migTimer5 = Stopwatch.StartNew();

                        string dualNapixSql = @"
                            IF EXISTS (SELECT 1 FROM sys.tables WHERE name = 'NAPIX_API_CALLS')
                            BEGIN
                                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.NAPIX_API_CALLS') AND name = 'Module')
                                    ALTER TABLE dbo.NAPIX_API_CALLS ADD Module NVARCHAR(20) NOT NULL DEFAULT 'MVC';

                                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_NAPIX_API_CALLS_Module_Hour')
                                    CREATE NONCLUSTERED INDEX IX_NAPIX_API_CALLS_Module_Hour
                                        ON dbo.NAPIX_API_CALLS (Module, CalledAt) INCLUDE (Endpoint, IsSuccess);
                            END
                        ";

                        using (var alterCmd = new SqlCommand(dualNapixSql, conn))
                        {
                            alterCmd.ExecuteNonQuery();
                        }

                        string dropSummarySpSql = "IF OBJECT_ID('dbo.usp_GetNapixQuotaSummary', 'P') IS NOT NULL DROP PROCEDURE dbo.usp_GetNapixQuotaSummary;";
                        using (var dropCmd = new SqlCommand(dropSummarySpSql, conn))
                        {
                            dropCmd.ExecuteNonQuery();
                        }

                        string sprocSql = @"
                            CREATE PROCEDURE dbo.usp_GetNapixQuotaSummary
                            AS
                            BEGIN
                                SET NOCOUNT ON;
                                DECLARE @HourStart DATETIME2 = DATEADD(HOUR, -1, SYSUTCDATETIME());

                                -- 1. Combined System Quota
                                SELECT
                                    COUNT(*) AS TotalCallsThisHour,
                                    ISNULL(SUM(CASE WHEN IsSuccess=1 THEN 1 ELSE 0 END), 0) AS SuccessfulCalls,
                                    ISNULL(SUM(CASE WHEN IsSuccess=0 THEN 1 ELSE 0 END), 0) AS FailedCalls,
                                    2000 AS HourlyQuota,
                                    2000 - COUNT(*) AS RemainingQuota,
                                    CAST(COUNT(*) * 100.0 / 2000 AS DECIMAL(5,1)) AS UsedPercent
                                FROM dbo.NAPIX_API_CALLS
                                WHERE CalledAt >= @HourStart;

                                -- 2. Per-Module Breakdown
                                SELECT
                                    m.ModuleName AS Module,
                                    ISNULL(c.TotalCalls, 0) AS TotalCallsThisHour,
                                    ISNULL(c.SuccessfulCalls, 0) AS SuccessfulCalls,
                                    ISNULL(c.FailedCalls, 0) AS FailedCalls,
                                    1000 AS HourlyQuota,
                                    1000 - ISNULL(c.TotalCalls, 0) AS RemainingQuota,
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

                                -- 3. Per-endpoint breakdown
                                SELECT
                                    Endpoint,
                                    Module,
                                    COUNT(*) AS TotalCalls,
                                    ISNULL(SUM(CASE WHEN IsSuccess=1 THEN 1 ELSE 0 END), 0) AS SuccessCalls,
                                    ISNULL(SUM(CASE WHEN IsSuccess=0 THEN 1 ELSE 0 END), 0) AS FailedCalls,
                                    ISNULL(AVG(DurationMs), 0) AS AvgDurationMs
                                FROM dbo.NAPIX_API_CALLS
                                WHERE CalledAt >= @HourStart
                                GROUP BY Endpoint, Module
                                ORDER BY TotalCalls DESC;

                                -- 4. Per-minute trend
                                SELECT
                                    DATEPART(MINUTE, DATEADD(HOUR,5,DATEADD(MINUTE,30,CalledAt))) AS MinuteIST,
                                    DATEPART(HOUR, DATEADD(HOUR,5,DATEADD(MINUTE,30,CalledAt))) AS HourIST,
                                    CAST(DATEADD(HOUR,5,DATEADD(MINUTE,30,CalledAt)) AS DATE) AS DateIST,
                                    COUNT(*) AS CallCount
                                FROM dbo.NAPIX_API_CALLS
                                WHERE CalledAt >= @HourStart
                                GROUP BY
                                    DATEPART(MINUTE, DATEADD(HOUR,5,DATEADD(MINUTE,30,CalledAt))),
                                    DATEPART(HOUR, DATEADD(HOUR,5,DATEADD(MINUTE,30,CalledAt))),
                                    CAST(DATEADD(HOUR,5,DATEADD(MINUTE,30,CalledAt)) AS DATE)
                                ORDER BY DateIST, HourIST, MinuteIST;

                                -- 5. Hourly trend
                                SELECT
                                    DATEPART(HOUR, DATEADD(HOUR,5,DATEADD(MINUTE,30,CalledAt))) AS HourIST,
                                    CAST(DATEADD(HOUR,5,DATEADD(MINUTE,30,CalledAt)) AS DATE) AS DateIST,
                                    COUNT(*) AS CallCount
                                FROM dbo.NAPIX_API_CALLS
                                WHERE CalledAt >= DATEADD(HOUR,-24,SYSUTCDATETIME())
                                GROUP BY
                                    DATEPART(HOUR, DATEADD(HOUR,5,DATEADD(MINUTE,30,CalledAt))),
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
                            END";

                        using (var sprocCmd = new SqlCommand(sprocSql, conn))
                        {
                            sprocCmd.ExecuteNonQuery();
                        }

                        migTimer5.Stop();

                        string recordSql6 = @"
                            INSERT INTO SCHEMA_MIGRATIONS (MigrationID, Description, ExecutionTimeMs)
                            VALUES (@id, @desc, @time)";
                        using (var recordCmd6 = new SqlCommand(recordSql6, conn))
                        {
                            recordCmd6.Parameters.Add(new SqlParameter("@id", SqlDbType.NVarChar, 200) { Value = v1_1_4_DualNapixMigrationId });
                            recordCmd6.Parameters.Add(new SqlParameter("@desc", SqlDbType.NVarChar, 500) { Value = "Add Module column to NAPIX_API_CALLS for dual MVC and Labour NAPIX App quota tracking" });
                            recordCmd6.Parameters.Add(new SqlParameter("@time", SqlDbType.Int) { Value = (int)migTimer5.ElapsedMilliseconds });
                            recordCmd6.ExecuteNonQuery();
                        }
                        logger.LogInformation("Migration {MigrationId} applied successfully in {Elapsed}ms.", v1_1_4_DualNapixMigrationId, migTimer5.ElapsedMilliseconds);
                    }

                    // 8. Versioned Migration: V1_1_5_Production_Napix_Sync_Queue
                    const string v1_1_5_NapixSyncQueueMigrationId = "V1_1_5_Production_Napix_Sync_Queue";
                    bool isV115Applied = false;
                    using (var checkCmd6 = new SqlCommand("SELECT COUNT(*) FROM SCHEMA_MIGRATIONS WHERE MigrationID = @id", conn))
                    {
                        checkCmd6.Parameters.Add(new SqlParameter("@id", SqlDbType.NVarChar, 200) { Value = v1_1_5_NapixSyncQueueMigrationId });
                        isV115Applied = (int)(checkCmd6.ExecuteScalar() ?? 0) > 0;
                    }

                    if (!isV115Applied)
                    {
                        logger.LogInformation("Applying migration {MigrationId}...", v1_1_5_NapixSyncQueueMigrationId);
                        var migTimer6 = Stopwatch.StartNew();

                        string tablesAndIndexesSql = @"
                            -- 1. Extend MVC_CASES with NAPIX e-Courts sync audit columns
                            IF EXISTS (SELECT 1 FROM sys.tables WHERE name = 'MVC_CASES')
                            BEGIN
                                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.MVC_CASES') AND name = 'NextHearingDate')
                                    ALTER TABLE dbo.MVC_CASES ADD NextHearingDate DATE NULL;
                                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.MVC_CASES') AND name = 'LastNapixSyncAt')
                                    ALTER TABLE dbo.MVC_CASES ADD LastNapixSyncAt DATETIME2 NULL;
                                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.MVC_CASES') AND name = 'LastNapixSyncStatus')
                                    ALTER TABLE dbo.MVC_CASES ADD LastNapixSyncStatus NVARCHAR(30) NULL;
                                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.MVC_CASES') AND name = 'LastNapixSyncError')
                                    ALTER TABLE dbo.MVC_CASES ADD LastNapixSyncError NVARCHAR(500) NULL;
                                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.MVC_CASES') AND name = 'NapixSyncAttemptCount')
                                    ALTER TABLE dbo.MVC_CASES ADD NapixSyncAttemptCount INT NOT NULL DEFAULT 0;
                                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.MVC_CASES') AND name = 'NapixDataHash')
                                    ALTER TABLE dbo.MVC_CASES ADD NapixDataHash NVARCHAR(64) NULL;
                                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.MVC_CASES') AND name = 'PendDispStatus')
                                    ALTER TABLE dbo.MVC_CASES ADD PendDispStatus NVARCHAR(20) NULL;
                                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.MVC_CASES') AND name = 'EstName')
                                    ALTER TABLE dbo.MVC_CASES ADD EstName NVARCHAR(250) NULL;
                                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.MVC_CASES') AND name = 'ECourtsStage')
                                    ALTER TABLE dbo.MVC_CASES ADD ECourtsStage NVARCHAR(150) NULL;
                                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.MVC_CASES') AND name = 'ECourtsCourtNo')
                                    ALTER TABLE dbo.MVC_CASES ADD ECourtsCourtNo NVARCHAR(50) NULL;
                                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.MVC_CASES') AND name = 'ECourtsJudge')
                                    ALTER TABLE dbo.MVC_CASES ADD ECourtsJudge NVARCHAR(250) NULL;
                            END

                            -- 2. Extend LABOUR_CASES with NAPIX e-Courts sync audit columns
                            IF EXISTS (SELECT 1 FROM sys.tables WHERE name = 'LABOUR_CASES')
                            BEGIN
                                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('LABOUR_CASES') AND name = 'NextHearingDate')
                                    ALTER TABLE LABOUR_CASES ADD NextHearingDate DATE NULL;
                                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('LABOUR_CASES') AND name = 'LastNapixSyncAt')
                                    ALTER TABLE LABOUR_CASES ADD LastNapixSyncAt DATETIME2 NULL;
                                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('LABOUR_CASES') AND name = 'LastNapixSyncStatus')
                                    ALTER TABLE LABOUR_CASES ADD LastNapixSyncStatus NVARCHAR(30) NULL;
                                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('LABOUR_CASES') AND name = 'LastNapixSyncError')
                                    ALTER TABLE LABOUR_CASES ADD LastNapixSyncError NVARCHAR(500) NULL;
                                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('LABOUR_CASES') AND name = 'NapixSyncAttemptCount')
                                    ALTER TABLE LABOUR_CASES ADD NapixSyncAttemptCount INT NOT NULL DEFAULT 0;
                                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('LABOUR_CASES') AND name = 'NapixDataHash')
                                    ALTER TABLE LABOUR_CASES ADD NapixDataHash NVARCHAR(64) NULL;
                                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('LABOUR_CASES') AND name = 'PendDispStatus')
                                    ALTER TABLE LABOUR_CASES ADD PendDispStatus NVARCHAR(20) NULL;
                                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('LABOUR_CASES') AND name = 'EstName')
                                    ALTER TABLE LABOUR_CASES ADD EstName NVARCHAR(250) NULL;
                                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('LABOUR_CASES') AND name = 'ECourtsStage')
                                    ALTER TABLE LABOUR_CASES ADD ECourtsStage NVARCHAR(150) NULL;
                                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('LABOUR_CASES') AND name = 'ECourtsCourtNo')
                                    ALTER TABLE LABOUR_CASES ADD ECourtsCourtNo NVARCHAR(50) NULL;
                                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('LABOUR_CASES') AND name = 'ECourtsJudge')
                                    ALTER TABLE LABOUR_CASES ADD ECourtsJudge NVARCHAR(250) NULL;
                            END

                            -- 3. Persistent Queue Table
                            IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'NAPIX_SYNC_QUEUE')
                            BEGIN
                                CREATE TABLE dbo.NAPIX_SYNC_QUEUE (
                                    QueueId BIGINT IDENTITY(1,1) PRIMARY KEY,
                                    Module NVARCHAR(20) NOT NULL,
                                    CaseId INT NOT NULL,
                                    CNRNumber NVARCHAR(30) NOT NULL,
                                    Priority INT NOT NULL DEFAULT 3,
                                    Status NVARCHAR(30) NOT NULL DEFAULT 'Queued',
                                    RequestedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
                                    LockedUntil DATETIME2 NULL,
                                    NextAttemptAt DATETIME2 NULL,
                                    AttemptCount INT NOT NULL DEFAULT 0,
                                    LastError NVARCHAR(500) NULL,
                                    ProcessedAt DATETIME2 NULL
                                );
                            END

                            -- Filtered Unique Index: guarantees at most ONE active (Queued or Processing) job per Module + CNR
                            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UQ_NAPIX_SYNC_ACTIVE')
                            BEGIN
                                CREATE UNIQUE NONCLUSTERED INDEX UQ_NAPIX_SYNC_ACTIVE 
                                    ON dbo.NAPIX_SYNC_QUEUE (Module, CNRNumber) 
                                    WHERE Status IN ('Queued', 'Processing');
                            END

                            -- Fetch index for performant bounded worker dequeue
                            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_NAPIX_SYNC_FETCH')
                            BEGIN
                                CREATE NONCLUSTERED INDEX IX_NAPIX_SYNC_FETCH 
                                    ON dbo.NAPIX_SYNC_QUEUE (Module, Status, Priority, NextAttemptAt, RequestedAt) 
                                    INCLUDE (QueueId, CaseId, CNRNumber, AttemptCount);
                            END";

                        using (var cmd = new SqlCommand(tablesAndIndexesSql, conn))
                        {
                            cmd.CommandTimeout = 120;
                            cmd.ExecuteNonQuery();
                        }

                        // 4. Stored Procedure: Atomic Quota Slot Reservation (compatible with SQL Server 2012)
                        string dropReserveQuotaSpSql = "IF OBJECT_ID('dbo.sp_ReserveNapixQuotaSlot', 'P') IS NOT NULL DROP PROCEDURE dbo.sp_ReserveNapixQuotaSlot;";
                        using (var dropCmd = new SqlCommand(dropReserveQuotaSpSql, conn))
                        {
                            dropCmd.ExecuteNonQuery();
                        }

                        string reserveQuotaSpSql = @"
                            CREATE PROCEDURE dbo.sp_ReserveNapixQuotaSlot
                                @Module NVARCHAR(20),
                                @MaxCallsPerHour INT,
                                @Reserved BIT OUTPUT,
                                @CurrentUsage INT OUTPUT,
                                @RetryAfterSeconds INT OUTPUT
                            AS
                            BEGIN
                                SET NOCOUNT ON;
                                SET TRANSACTION ISOLATION LEVEL SERIALIZABLE;
                                BEGIN TRANSACTION;

                                DECLARE @WindowStart DATETIME2 = DATEADD(MINUTE, -60, SYSUTCDATETIME());

                                SELECT @CurrentUsage = COUNT(*)
                                FROM dbo.NAPIX_API_CALLS WITH (UPDLOCK, HOLDLOCK)
                                WHERE CalledAt >= @WindowStart
                                  AND (Module = @Module OR (@Module = 'MVC' AND Module IS NULL));

                                -- Also account for currently in-flight requests in the queue
                                DECLARE @InFlight INT = 0;
                                SELECT @InFlight = COUNT(*)
                                FROM dbo.NAPIX_SYNC_QUEUE WITH (UPDLOCK, HOLDLOCK)
                                WHERE Module = @Module AND Status = 'Processing' AND LockedUntil > SYSUTCDATETIME();

                                SET @CurrentUsage = @CurrentUsage + @InFlight;

                                IF @CurrentUsage < @MaxCallsPerHour
                                BEGIN
                                    SET @Reserved = 1;
                                    SET @RetryAfterSeconds = 0;
                                    COMMIT TRANSACTION;
                                END
                                ELSE
                                BEGIN
                                    SET @Reserved = 0;
                                    SELECT @RetryAfterSeconds = DATEDIFF(SECOND, SYSUTCDATETIME(), DATEADD(MINUTE, 60, MIN(CalledAt)))
                                    FROM dbo.NAPIX_API_CALLS WITH (NOLOCK)
                                    WHERE CalledAt >= @WindowStart
                                      AND (Module = @Module OR (@Module = 'MVC' AND Module IS NULL));

                                    IF @RetryAfterSeconds IS NULL OR @RetryAfterSeconds <= 0
                                        SET @RetryAfterSeconds = 5;
                                    COMMIT TRANSACTION;
                                END
                            END;";

                        using (var cmd = new SqlCommand(reserveQuotaSpSql, conn))
                        {
                            cmd.CommandTimeout = 120;
                            cmd.ExecuteNonQuery();
                        }

                        // 5. Stored Procedure: Atomic Dequeue with Lease Lock & Crash Recovery (compatible with SQL Server 2012)
                        string dropDequeueSpSql = "IF OBJECT_ID('dbo.sp_DequeueNapixSyncJob', 'P') IS NOT NULL DROP PROCEDURE dbo.sp_DequeueNapixSyncJob;";
                        using (var dropCmd = new SqlCommand(dropDequeueSpSql, conn))
                        {
                            dropCmd.ExecuteNonQuery();
                        }

                        string dequeueJobSpSql = @"
                            CREATE PROCEDURE dbo.sp_DequeueNapixSyncJob
                                @Module NVARCHAR(20),
                                @LeaseMinutes INT = 5
                            AS
                            BEGIN
                                SET NOCOUNT ON;
                                DECLARE @Now DATETIME2 = SYSUTCDATETIME();

                                -- Recover expired leases
                                UPDATE dbo.NAPIX_SYNC_QUEUE
                                SET Status = 'Queued', LockedUntil = NULL, AttemptCount = AttemptCount + 1,
                                    LastError = 'Lease expired / recovered from ungraceful shutdown'
                                WHERE Module = @Module 
                                  AND Status = 'Processing' 
                                  AND LockedUntil < @Now;

                                -- Atomically lock and retrieve the top priority eligible job
                                ;WITH NextJob AS (
                                    SELECT TOP (1) QueueId, Module, CaseId, CNRNumber, Priority, AttemptCount, Status, LockedUntil
                                    FROM dbo.NAPIX_SYNC_QUEUE WITH (UPDLOCK, READPAST)
                                    WHERE Module = @Module
                                      AND Status = 'Queued'
                                      AND (NextAttemptAt IS NULL OR NextAttemptAt <= @Now)
                                    ORDER BY Priority ASC, RequestedAt ASC
                                )
                                UPDATE NextJob
                                SET Status = 'Processing',
                                    LockedUntil = DATEADD(MINUTE, @LeaseMinutes, @Now)
                                OUTPUT 
                                    inserted.QueueId,
                                    inserted.Module,
                                    inserted.CaseId,
                                    inserted.CNRNumber,
                                    inserted.Priority,
                                    inserted.AttemptCount;
                            END;";

                        using (var cmd = new SqlCommand(dequeueJobSpSql, conn))
                        {
                            cmd.CommandTimeout = 120;
                            cmd.ExecuteNonQuery();
                        }

                        migTimer6.Stop();

                        string recordSql7 = @"
                            INSERT INTO SCHEMA_MIGRATIONS (MigrationID, Description, ExecutionTimeMs)
                            VALUES (@id, @desc, @time)";
                        using (var recordCmd7 = new SqlCommand(recordSql7, conn))
                        {
                            recordCmd7.Parameters.Add(new SqlParameter("@id", SqlDbType.NVarChar, 200) { Value = v1_1_5_NapixSyncQueueMigrationId });
                            recordCmd7.Parameters.Add(new SqlParameter("@desc", SqlDbType.NVarChar, 500) { Value = "Persistent NAPIX sync queue, atomic quota slot reservation, and eCourts status columns" });
                            recordCmd7.Parameters.Add(new SqlParameter("@time", SqlDbType.Int) { Value = (int)migTimer6.ElapsedMilliseconds });
                            recordCmd7.ExecuteNonQuery();
                        }
                        logger.LogInformation("Migration {MigrationId} applied successfully in {Elapsed}ms.", v1_1_5_NapixSyncQueueMigrationId, migTimer6.ElapsedMilliseconds);
                    }

                    // 9. Versioned Migration: V1_1_6_Ensure_Latest_Napix_Quota_Summary_Sproc
                    const string v1_1_6_NapixSummarySprocMigrationId = "V1_1_6_Ensure_Latest_Napix_Quota_Summary_Sproc";
                    bool isV116Applied = false;
                    using (var checkCmd7 = new SqlCommand("SELECT COUNT(*) FROM SCHEMA_MIGRATIONS WHERE MigrationID = @id", conn))
                    {
                        checkCmd7.Parameters.Add(new SqlParameter("@id", SqlDbType.NVarChar, 200) { Value = v1_1_6_NapixSummarySprocMigrationId });
                        isV116Applied = (int)(checkCmd7.ExecuteScalar() ?? 0) > 0;
                    }

                    if (!isV116Applied)
                    {
                        logger.LogInformation("Applying migration {MigrationId}...", v1_1_6_NapixSummarySprocMigrationId);
                        var migTimer7 = Stopwatch.StartNew();

                        string dropSql = "IF OBJECT_ID('dbo.usp_GetNapixQuotaSummary', 'P') IS NOT NULL DROP PROCEDURE dbo.usp_GetNapixQuotaSummary;";
                        using (var dropCmd = new SqlCommand(dropSql, conn))
                        {
                            dropCmd.ExecuteNonQuery();
                        }

                        string sprocSql = @"
                            CREATE PROCEDURE dbo.usp_GetNapixQuotaSummary
                            AS
                            BEGIN
                                SET NOCOUNT ON;
                                DECLARE @HourStart DATETIME2 = DATEADD(HOUR, -1, SYSUTCDATETIME());

                                -- 1. Combined System Quota
                                SELECT
                                    COUNT(*) AS TotalCallsThisHour,
                                    ISNULL(SUM(CASE WHEN IsSuccess=1 THEN 1 ELSE 0 END), 0) AS SuccessfulCalls,
                                    ISNULL(SUM(CASE WHEN IsSuccess=0 THEN 1 ELSE 0 END), 0) AS FailedCalls,
                                    2000 AS HourlyQuota,
                                    2000 - COUNT(*) AS RemainingQuota,
                                    CAST(COUNT(*) * 100.0 / 2000 AS DECIMAL(5,1)) AS UsedPercent
                                FROM dbo.NAPIX_API_CALLS
                                WHERE CalledAt >= @HourStart;

                                -- 2. Per-Module Breakdown
                                SELECT
                                    m.ModuleName AS Module,
                                    ISNULL(c.TotalCalls, 0) AS TotalCallsThisHour,
                                    ISNULL(c.SuccessfulCalls, 0) AS SuccessfulCalls,
                                    ISNULL(c.FailedCalls, 0) AS FailedCalls,
                                    1000 AS HourlyQuota,
                                    1000 - ISNULL(c.TotalCalls, 0) AS RemainingQuota,
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

                                -- 3. Per-endpoint breakdown
                                SELECT
                                    Endpoint,
                                    Module,
                                    COUNT(*) AS TotalCalls,
                                    ISNULL(SUM(CASE WHEN IsSuccess=1 THEN 1 ELSE 0 END), 0) AS SuccessCalls,
                                    ISNULL(SUM(CASE WHEN IsSuccess=0 THEN 1 ELSE 0 END), 0) AS FailedCalls,
                                    ISNULL(AVG(DurationMs), 0) AS AvgDurationMs
                                FROM dbo.NAPIX_API_CALLS
                                WHERE CalledAt >= @HourStart
                                GROUP BY Endpoint, Module
                                ORDER BY TotalCalls DESC;

                                -- 4. Per-minute trend
                                SELECT
                                    DATEPART(MINUTE, DATEADD(HOUR,5,DATEADD(MINUTE,30,CalledAt))) AS MinuteIST,
                                    DATEPART(HOUR, DATEADD(HOUR,5,DATEADD(MINUTE,30,CalledAt))) AS HourIST,
                                    CAST(DATEADD(HOUR,5,DATEADD(MINUTE,30,CalledAt)) AS DATE) AS DateIST,
                                    COUNT(*) AS CallCount
                                FROM dbo.NAPIX_API_CALLS
                                WHERE CalledAt >= @HourStart
                                GROUP BY
                                    DATEPART(MINUTE, DATEADD(HOUR,5,DATEADD(MINUTE,30,CalledAt))),
                                    DATEPART(HOUR, DATEADD(HOUR,5,DATEADD(MINUTE,30,CalledAt))),
                                    CAST(DATEADD(HOUR,5,DATEADD(MINUTE,30,CalledAt)) AS DATE)
                                ORDER BY DateIST, HourIST, MinuteIST;

                                -- 5. Hourly trend
                                SELECT
                                    DATEPART(HOUR, DATEADD(HOUR,5,DATEADD(MINUTE,30,CalledAt))) AS HourIST,
                                    CAST(DATEADD(HOUR,5,DATEADD(MINUTE,30,CalledAt)) AS DATE) AS DateIST,
                                    COUNT(*) AS CallCount
                                FROM dbo.NAPIX_API_CALLS
                                WHERE CalledAt >= DATEADD(HOUR,-24,SYSUTCDATETIME())
                                GROUP BY
                                    DATEPART(HOUR, DATEADD(HOUR,5,DATEADD(MINUTE,30,CalledAt))),
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
                            END;";

                        using (var createCmd = new SqlCommand(sprocSql, conn))
                        {
                            createCmd.ExecuteNonQuery();
                        }

                        migTimer7.Stop();

                        string recordSql8 = @"
                            INSERT INTO SCHEMA_MIGRATIONS (MigrationID, Description, ExecutionTimeMs)
                            VALUES (@id, @desc, @time)";
                        using (var recordCmd8 = new SqlCommand(recordSql8, conn))
                        {
                            recordCmd8.Parameters.Add(new SqlParameter("@id", SqlDbType.NVarChar, 200) { Value = v1_1_6_NapixSummarySprocMigrationId });
                            recordCmd8.Parameters.Add(new SqlParameter("@desc", SqlDbType.NVarChar, 500) { Value = "Ensure latest 6-result-set usp_GetNapixQuotaSummary procedure with Module breakdown and endpoint stats" });
                            recordCmd8.Parameters.Add(new SqlParameter("@time", SqlDbType.Int) { Value = (int)migTimer7.ElapsedMilliseconds });
                            recordCmd8.ExecuteNonQuery();
                        }
                        logger.LogInformation("Migration {MigrationId} applied successfully in {Elapsed}ms.", v1_1_6_NapixSummarySprocMigrationId, migTimer7.ElapsedMilliseconds);
                    }

                    // 10. Versioned Migration: V1_1_7_Other_Courts_NAPIX_Integration_And_Performance_Indexes
                    const string v1_1_7_OtherCourtsMigrationId = "V1_1_7_Other_Courts_NAPIX_Integration_And_Performance_Indexes";
                    bool isV117Applied = false;
                    using (var checkCmd8 = new SqlCommand("SELECT COUNT(*) FROM SCHEMA_MIGRATIONS WHERE MigrationID = @id", conn))
                    {
                        checkCmd8.Parameters.Add(new SqlParameter("@id", SqlDbType.NVarChar, 200) { Value = v1_1_7_OtherCourtsMigrationId });
                        isV117Applied = (int)(checkCmd8.ExecuteScalar() ?? 0) > 0;
                    }

                    if (!isV117Applied)
                    {
                        logger.LogInformation("Applying migration {MigrationId}...", v1_1_7_OtherCourtsMigrationId);
                        var migTimer8 = Stopwatch.StartNew();

                        string otherCourtsSchemaSql = @"
                            -- Ensure OTHER_CASES columns
                            IF EXISTS (SELECT 1 FROM sys.tables WHERE name = 'OTHER_CASES')
                            BEGIN
                                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('OTHER_CASES') AND name = 'CNRNumber')
                                    ALTER TABLE OTHER_CASES ADD CNRNumber NVARCHAR(16) NULL;
                                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('OTHER_CASES') AND name = 'EstCode')
                                    ALTER TABLE OTHER_CASES ADD EstCode NVARCHAR(50) NULL;
                                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('OTHER_CASES') AND name = 'CaseTypeCode')
                                    ALTER TABLE OTHER_CASES ADD CaseTypeCode NVARCHAR(20) NULL;
                                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('OTHER_CASES') AND name = 'OtherCourtDetails')
                                    ALTER TABLE OTHER_CASES ADD OtherCourtDetails NVARCHAR(200) NULL;
                                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('OTHER_CASES') AND name = 'LastNapixSyncAt')
                                    ALTER TABLE OTHER_CASES ADD LastNapixSyncAt DATETIME2 NULL;
                                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('OTHER_CASES') AND name = 'LastNapixSyncStatus')
                                    ALTER TABLE OTHER_CASES ADD LastNapixSyncStatus NVARCHAR(100) NULL;
                                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('OTHER_CASES') AND name = 'LastNapixSyncError')
                                    ALTER TABLE OTHER_CASES ADD LastNapixSyncError NVARCHAR(MAX) NULL;
                                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('OTHER_CASES') AND name = 'NapixSyncAttemptCount')
                                    ALTER TABLE OTHER_CASES ADD NapixSyncAttemptCount INT NOT NULL DEFAULT 0;
                                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('OTHER_CASES') AND name = 'NapixDataHash')
                                    ALTER TABLE OTHER_CASES ADD NapixDataHash NVARCHAR(64) NULL;
                                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('OTHER_CASES') AND name = 'PendDispStatus')
                                    ALTER TABLE OTHER_CASES ADD PendDispStatus NVARCHAR(10) NULL;
                                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('OTHER_CASES') AND name = 'EstName')
                                    ALTER TABLE OTHER_CASES ADD EstName NVARCHAR(200) NULL;
                                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('OTHER_CASES') AND name = 'ECourtsStage')
                                    ALTER TABLE OTHER_CASES ADD ECourtsStage NVARCHAR(200) NULL;
                                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('OTHER_CASES') AND name = 'ECourtsCourtNo')
                                    ALTER TABLE OTHER_CASES ADD ECourtsCourtNo NVARCHAR(50) NULL;
                                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('OTHER_CASES') AND name = 'ECourtsJudge')
                                    ALTER TABLE OTHER_CASES ADD ECourtsJudge NVARCHAR(200) NULL;

                                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_OTHER_CASES_CNR')
                                    CREATE NONCLUSTERED INDEX IX_OTHER_CASES_CNR ON OTHER_CASES(CNRNumber) WHERE CNRNumber IS NOT NULL;

                                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_OTHER_CASES_DIV_TYPE')
                                BEGIN
                                    IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('OTHER_CASES') AND name = 'DivisionID')
                                       AND EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('OTHER_CASES') AND name = 'CaseType')
                                       AND EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('OTHER_CASES') AND name = 'LitigantType')
                                    BEGIN
                                        CREATE NONCLUSTERED INDEX IX_OTHER_CASES_DIV_TYPE ON OTHER_CASES(DivisionID, CaseType, LitigantType);
                                    END
                                END
                            END

                            -- Gratuity Performance Indexes (Table is GRA_CASES)
                            IF EXISTS (SELECT 1 FROM sys.tables WHERE name = 'GRA_CASES')
                            BEGIN
                                IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('GRA_CASES') AND name = 'DivisionCode')
                                   AND EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('GRA_CASES') AND name = 'CaseStatus')
                                BEGIN
                                    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_GRA_CASES_DIV_STATUS')
                                        CREATE NONCLUSTERED INDEX IX_GRA_CASES_DIV_STATUS ON GRA_CASES(DivisionCode, CaseStatus);
                                END
                                ELSE IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('GRA_CASES') AND name = 'DivisionID')
                                   AND EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('GRA_CASES') AND name = 'CaseStatus')
                                BEGIN
                                    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_GRA_CASES_DIV_STATUS')
                                        CREATE NONCLUSTERED INDEX IX_GRA_CASES_DIV_STATUS ON GRA_CASES(DivisionID, CaseStatus);
                                END

                                IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('GRA_CASES') AND name = 'NextHearingDate')
                                BEGIN
                                    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_GRA_CASES_HEARING')
                                        CREATE NONCLUSTERED INDEX IX_GRA_CASES_HEARING ON GRA_CASES(NextHearingDate);
                                END
                            END
                        ";

                        using (var schemaCmd = new SqlCommand(otherCourtsSchemaSql, conn))
                        {
                            schemaCmd.CommandTimeout = 120;
                            schemaCmd.ExecuteNonQuery();
                        }

                        // Deploy 3-Module Quota Summary Sproc (MVC + Labour + OtherCourts)
                        string dropSql = "IF OBJECT_ID('dbo.usp_GetNapixQuotaSummary', 'P') IS NOT NULL DROP PROCEDURE dbo.usp_GetNapixQuotaSummary;";
                        using (var dropCmd = new SqlCommand(dropSql, conn))
                        {
                            dropCmd.ExecuteNonQuery();
                        }

                        string sprocSql = @"
                            CREATE PROCEDURE dbo.usp_GetNapixQuotaSummary
                            AS
                            BEGIN
                                SET NOCOUNT ON;
                                DECLARE @HourStart DATETIME2 = DATEADD(HOUR, -1, SYSUTCDATETIME());

                                -- 1. Combined System Quota across 3 apps (3000 total capacity)
                                SELECT
                                    COUNT(*) AS TotalCallsThisHour,
                                    ISNULL(SUM(CASE WHEN IsSuccess=1 THEN 1 ELSE 0 END), 0) AS SuccessfulCalls,
                                    ISNULL(SUM(CASE WHEN IsSuccess=0 THEN 1 ELSE 0 END), 0) AS FailedCalls,
                                    3000 AS HourlyQuota,
                                    3000 - COUNT(*) AS RemainingQuota,
                                    CAST(COUNT(*) * 100.0 / 3000 AS DECIMAL(5,1)) AS UsedPercent
                                FROM dbo.NAPIX_API_CALLS
                                WHERE CalledAt >= @HourStart;

                                -- 2. Per-Module Breakdown (MVC, Labour, OtherCourts - 1000 each)
                                SELECT
                                    m.ModuleName AS Module,
                                    ISNULL(c.TotalCalls, 0) AS TotalCallsThisHour,
                                    ISNULL(c.SuccessfulCalls, 0) AS SuccessfulCalls,
                                    ISNULL(c.FailedCalls, 0) AS FailedCalls,
                                    1000 AS HourlyQuota,
                                    1000 - ISNULL(c.TotalCalls, 0) AS RemainingQuota,
                                    CAST(ISNULL(c.TotalCalls, 0) * 100.0 / 1000 AS DECIMAL(5,1)) AS UsedPercent
                                FROM (VALUES ('MVC'), ('Labour'), ('OtherCourts')) AS m(ModuleName)
                                LEFT JOIN (
                                    SELECT
                                        CASE 
                                            WHEN UPPER(Module) LIKE '%OTHER%' OR UPPER(Module) IN ('OS','PSC','CC','CONSUMER','LAC','ECA') THEN 'OtherCourts'
                                            WHEN UPPER(Module) LIKE '%LABOUR%' THEN 'Labour'
                                            ELSE 'MVC'
                                        END AS ModuleName,
                                        COUNT(*) AS TotalCalls,
                                        ISNULL(SUM(CASE WHEN IsSuccess=1 THEN 1 ELSE 0 END), 0) AS SuccessfulCalls,
                                        ISNULL(SUM(CASE WHEN IsSuccess=0 THEN 1 ELSE 0 END), 0) AS FailedCalls
                                    FROM dbo.NAPIX_API_CALLS
                                    WHERE CalledAt >= @HourStart
                                    GROUP BY 
                                        CASE 
                                            WHEN UPPER(Module) LIKE '%OTHER%' OR UPPER(Module) IN ('OS','PSC','CC','CONSUMER','LAC','ECA') THEN 'OtherCourts'
                                            WHEN UPPER(Module) LIKE '%LABOUR%' THEN 'Labour'
                                            ELSE 'MVC'
                                        END
                                ) c ON m.ModuleName = c.ModuleName;

                                -- 3. Per-endpoint breakdown
                                SELECT
                                    Endpoint,
                                    Module,
                                    COUNT(*) AS TotalCalls,
                                    ISNULL(SUM(CASE WHEN IsSuccess=1 THEN 1 ELSE 0 END), 0) AS SuccessCalls,
                                    ISNULL(SUM(CASE WHEN IsSuccess=0 THEN 1 ELSE 0 END), 0) AS FailedCalls,
                                    ISNULL(AVG(DurationMs), 0) AS AvgDurationMs
                                FROM dbo.NAPIX_API_CALLS
                                WHERE CalledAt >= @HourStart
                                GROUP BY Endpoint, Module
                                ORDER BY TotalCalls DESC;

                                -- 4. Per-minute trend
                                SELECT
                                    DATEPART(MINUTE, DATEADD(HOUR,5,DATEADD(MINUTE,30,CalledAt))) AS MinuteIST,
                                    DATEPART(HOUR, DATEADD(HOUR,5,DATEADD(MINUTE,30,CalledAt))) AS HourIST,
                                    CAST(DATEADD(HOUR,5,DATEADD(MINUTE,30,CalledAt)) AS DATE) AS DateIST,
                                    COUNT(*) AS CallCount
                                FROM dbo.NAPIX_API_CALLS
                                WHERE CalledAt >= @HourStart
                                GROUP BY
                                    DATEPART(MINUTE, DATEADD(HOUR,5,DATEADD(MINUTE,30,CalledAt))),
                                    DATEPART(HOUR, DATEADD(HOUR,5,DATEADD(MINUTE,30,CalledAt))),
                                    CAST(DATEADD(HOUR,5,DATEADD(MINUTE,30,CalledAt)) AS DATE)
                                ORDER BY DateIST, HourIST, MinuteIST;

                                -- 5. Hourly trend
                                SELECT
                                    DATEPART(HOUR, DATEADD(HOUR,5,DATEADD(MINUTE,30,CalledAt))) AS HourIST,
                                    CAST(DATEADD(HOUR,5,DATEADD(MINUTE,30,CalledAt)) AS DATE) AS DateIST,
                                    COUNT(*) AS CallCount
                                FROM dbo.NAPIX_API_CALLS
                                WHERE CalledAt >= DATEADD(HOUR,-24,SYSUTCDATETIME())
                                GROUP BY
                                    DATEPART(HOUR, DATEADD(HOUR,5,DATEADD(MINUTE,30,CalledAt))),
                                    DATEPART(HOUR, DATEADD(HOUR,5,DATEADD(MINUTE,30,CalledAt))),
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
                            END;";

                        using (var createCmd = new SqlCommand(sprocSql, conn))
                        {
                            createCmd.ExecuteNonQuery();
                        }

                        migTimer8.Stop();

                        string recordSql9 = @"
                            INSERT INTO SCHEMA_MIGRATIONS (MigrationID, Description, ExecutionTimeMs)
                            VALUES (@id, @desc, @time)";
                        using (var recordCmd9 = new SqlCommand(recordSql9, conn))
                        {
                            recordCmd9.Parameters.Add(new SqlParameter("@id", SqlDbType.NVarChar, 200) { Value = v1_1_7_OtherCourtsMigrationId });
                            recordCmd9.Parameters.Add(new SqlParameter("@desc", SqlDbType.NVarChar, 500) { Value = "Other Courts NAPIX integration schema, GRA_CASES performance indexes, and 3-module quota summary sproc" });
                            recordCmd9.Parameters.Add(new SqlParameter("@time", SqlDbType.Int) { Value = (int)migTimer8.ElapsedMilliseconds });
                            recordCmd9.ExecuteNonQuery();
                        }
                        logger.LogInformation("Migration {MigrationId} applied successfully in {Elapsed}ms.", v1_1_7_OtherCourtsMigrationId, migTimer8.ElapsedMilliseconds);
                    }

                    // 11. Versioned Migration: V1_1_8_Labour_Connected_Cases_And_Enclosed_Docs_Schema
                    const string v1_1_8_LabourSchemaMigrationId = "V1_1_8_Labour_Connected_Cases_And_Enclosed_Docs_Schema";
                    bool isV118Applied = false;
                    using (var checkCmd9 = new SqlCommand("SELECT COUNT(*) FROM SCHEMA_MIGRATIONS WHERE MigrationID = @id", conn))
                    {
                        checkCmd9.Parameters.Add(new SqlParameter("@id", SqlDbType.NVarChar, 200) { Value = v1_1_8_LabourSchemaMigrationId });
                        isV118Applied = (int)(checkCmd9.ExecuteScalar() ?? 0) > 0;
                    }

                    if (!isV118Applied)
                    {
                        logger.LogInformation("Applying migration {MigrationId}...", v1_1_8_LabourSchemaMigrationId);
                        var migTimer9 = Stopwatch.StartNew();

                        string labourSchemaSql = @"
                            IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('LABOUR_CONNECTED_CASES') AND name = 'CaseType')
                                ALTER TABLE LABOUR_CONNECTED_CASES ADD CaseType NVARCHAR(100) NULL;

                            IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('LABOUR_CASES') AND name = 'IsReinstatementViewed')
                                ALTER TABLE LABOUR_CASES ADD IsReinstatementViewed BIT NOT NULL DEFAULT 0;

                            IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'LABOUR_ENCLOSED_DOCS')
                                CREATE TABLE LABOUR_ENCLOSED_DOCS (
                                    DocID INT PRIMARY KEY IDENTITY(1,1),
                                    CaseID INT NOT NULL,
                                    DocName NVARCHAR(500) NULL,
                                    PageCount INT NULL
                                );

                            -- Self-healing data repair: normalize CaseStatus and preserve CurrentStage
                            IF EXISTS (SELECT 1 FROM sys.tables WHERE name = 'LABOUR_CASES')
                            BEGIN
                                UPDATE LABOUR_CASES 
                                SET CurrentStage = CASE WHEN (CurrentStage IS NULL OR CurrentStage = '') THEN CaseStatus ELSE CurrentStage END,
                                    CaseStatus = CASE 
                                        WHEN UPPER(LTRIM(RTRIM(CaseStatus))) IN ('DISPOSED', 'DISMISSED', 'CLOSED', 'DECIDED') THEN 'Disposed'
                                        ELSE 'Pending'
                                    END
                                WHERE CaseStatus NOT IN ('Pending', 'Disposed', 'DNP', 'Ex-parte') 
                                  AND CaseStatus IS NOT NULL AND CaseStatus <> '';
                            END
                        ";

                        using (var schemaCmd = new SqlCommand(labourSchemaSql, conn))
                        {
                            schemaCmd.CommandTimeout = 120;
                            schemaCmd.ExecuteNonQuery();
                        }

                        migTimer9.Stop();

                        string recordSql10 = @"
                            INSERT INTO SCHEMA_MIGRATIONS (MigrationID, Description, ExecutionTimeMs)
                            VALUES (@id, @desc, @time)";
                        using (var recordCmd10 = new SqlCommand(recordSql10, conn))
                        {
                            recordCmd10.Parameters.Add(new SqlParameter("@id", SqlDbType.NVarChar, 200) { Value = v1_1_8_LabourSchemaMigrationId });
                            recordCmd10.Parameters.Add(new SqlParameter("@desc", SqlDbType.NVarChar, 500) { Value = "Labour connected cases schema, enclosed docs table, and status normalization" });
                            recordCmd10.Parameters.Add(new SqlParameter("@time", SqlDbType.Int) { Value = (int)migTimer9.ElapsedMilliseconds });
                            recordCmd10.ExecuteNonQuery();
                        }
                        logger.LogInformation("Migration {MigrationId} applied successfully in {Elapsed}ms.", v1_1_8_LabourSchemaMigrationId, migTimer9.ElapsedMilliseconds);
                    }

                    // -------------------------------------------------------------
                    // MIGRATION V1.1.9: Nyaya Patha AI Assistant Schema
                    // Isolated AI tables for conversations, messages, and audit logs.
                    // Does NOT alter or affect existing business tables.
                    // -------------------------------------------------------------
                    string v1_1_9_NyayaPathaAIMigrationId = "V1_1_9_Nyaya_Patha_AI_Assistant_Schema";
                    bool v1_1_9_Applied = false;

                    using (var checkCmd = new SqlCommand("SELECT COUNT(1) FROM SCHEMA_MIGRATIONS WHERE MigrationID = @id", conn))
                    {
                        checkCmd.Parameters.Add(new SqlParameter("@id", SqlDbType.NVarChar, 200) { Value = v1_1_9_NyayaPathaAIMigrationId });
                        v1_1_9_Applied = (int)checkCmd.ExecuteScalar() > 0;
                    }

                    if (!v1_1_9_Applied)
                    {
                        logger.LogInformation("Applying migration {MigrationId}...", v1_1_9_NyayaPathaAIMigrationId);
                        var migTimer10 = System.Diagnostics.Stopwatch.StartNew();

                        string aiSchemaSql = @"
                            IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'AI_CONVERSATIONS')
                            BEGIN
                                CREATE TABLE AI_CONVERSATIONS (
                                    ConversationID INT PRIMARY KEY IDENTITY(1,1),
                                    UserID INT NOT NULL,
                                    CaseType NVARCHAR(50) NULL,
                                    CaseID INT NULL,
                                    Title NVARCHAR(255) NOT NULL,
                                    CreatedAt DATETIME NOT NULL DEFAULT GETDATE(),
                                    UpdatedAt DATETIME NOT NULL DEFAULT GETDATE(),
                                    IsActive BIT NOT NULL DEFAULT 1
                                );
                                CREATE INDEX IX_AI_CONVERSATIONS_UserID ON AI_CONVERSATIONS (UserID, IsActive, UpdatedAt DESC);
                            END

                            IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'AI_MESSAGES')
                            BEGIN
                                CREATE TABLE AI_MESSAGES (
                                    MessageID INT PRIMARY KEY IDENTITY(1,1),
                                    ConversationID INT NOT NULL,
                                    Role NVARCHAR(20) NOT NULL,
                                    MessageText NVARCHAR(MAX) NOT NULL,
                                    Model NVARCHAR(100) NULL,
                                    CreatedAt DATETIME NOT NULL DEFAULT GETDATE()
                                );
                                CREATE INDEX IX_AI_MESSAGES_ConversationID ON AI_MESSAGES (ConversationID, MessageID ASC);
                            END

                            IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'AI_AUDIT_LOGS')
                            BEGIN
                                CREATE TABLE AI_AUDIT_LOGS (
                                    AuditID INT PRIMARY KEY IDENTITY(1,1),
                                    UserID INT NOT NULL,
                                    Role NVARCHAR(50) NULL,
                                    DivisionID INT NOT NULL,
                                    ConversationID INT NULL,
                                    CaseType NVARCHAR(50) NULL,
                                    CaseID INT NULL,
                                    Question NVARCHAR(MAX) NULL,
                                    RetrievedSources NVARCHAR(MAX) NULL,
                                    Model NVARCHAR(100) NULL,
                                    ExecutionTimeMs INT NOT NULL DEFAULT 0,
                                    Status NVARCHAR(50) NOT NULL DEFAULT 'SUCCESS',
                                    ErrorMessage NVARCHAR(MAX) NULL,
                                    CreatedAt DATETIME NOT NULL DEFAULT GETDATE()
                                );
                                CREATE INDEX IX_AI_AUDIT_LOGS_UserID_Date ON AI_AUDIT_LOGS (UserID, CreatedAt DESC);
                                CREATE INDEX IX_AI_AUDIT_LOGS_Case ON AI_AUDIT_LOGS (CaseType, CaseID);
                            END
                        ";

                        using (var schemaCmd = new SqlCommand(aiSchemaSql, conn))
                        {
                            schemaCmd.CommandTimeout = 120;
                            schemaCmd.ExecuteNonQuery();
                        }

                        migTimer10.Stop();

                        string recordSql11 = @"
                            INSERT INTO SCHEMA_MIGRATIONS (MigrationID, Description, ExecutionTimeMs)
                            VALUES (@id, @desc, @time)";
                        using (var recordCmd11 = new SqlCommand(recordSql11, conn))
                        {
                            recordCmd11.Parameters.Add(new SqlParameter("@id", SqlDbType.NVarChar, 200) { Value = v1_1_9_NyayaPathaAIMigrationId });
                            recordCmd11.Parameters.Add(new SqlParameter("@desc", SqlDbType.NVarChar, 500) { Value = "Nyaya Patha AI Assistant Schema: AI_CONVERSATIONS, AI_MESSAGES, AI_AUDIT_LOGS" });
                            recordCmd11.Parameters.Add(new SqlParameter("@time", SqlDbType.Int) { Value = (int)migTimer10.ElapsedMilliseconds });
                            recordCmd11.ExecuteNonQuery();
                        }
                        logger.LogInformation("Migration {MigrationId} applied successfully in {Elapsed}ms.", v1_1_9_NyayaPathaAIMigrationId, migTimer10.ElapsedMilliseconds);
                    }

                    // 15. Migration: CASE_ACTIVITY_LOGS (User & Case Audit Activity Log Sheet)
                    const string v1_2_0_CaseActivityLogsMigrationId = "20261009_v1_2_0_Case_Activity_Logs";
                    bool isV120Applied = false;
                    using (var checkCmd = new SqlCommand("SELECT COUNT(*) FROM SCHEMA_MIGRATIONS WHERE MigrationID = @id", conn))
                    {
                        checkCmd.Parameters.Add(new SqlParameter("@id", SqlDbType.NVarChar, 200) { Value = v1_2_0_CaseActivityLogsMigrationId });
                        isV120Applied = (int)(checkCmd.ExecuteScalar() ?? 0) > 0;
                    }

                    if (!isV120Applied)
                    {
                        var migTimer11 = Stopwatch.StartNew();
                        logger.LogInformation("Applying migration: {MigrationId} (Enterprise Case Activity & Audit Log Sheet)...", v1_2_0_CaseActivityLogsMigrationId);

                        string activityLogSql = @"
                            IF NOT EXISTS (SELECT 1 FROM sys.tables t JOIN sys.schemas s ON t.schema_id = s.schema_id WHERE t.name = 'CASE_ACTIVITY_LOGS' AND s.name = 'dbo')
                            BEGIN
                                CREATE TABLE [dbo].[CASE_ACTIVITY_LOGS] (
                                    [LogID] BIGINT IDENTITY(1,1) NOT NULL,
                                    [Timestamp] DATETIME NOT NULL CONSTRAINT [DF_CASE_ACTIVITY_LOGS_Timestamp] DEFAULT (GETDATE()),
                                    [UserID] INT NULL,
                                    [Username] NVARCHAR(100) NOT NULL,
                                    [UserFullName] NVARCHAR(150) NULL,
                                    [UserRole] NVARCHAR(50) NULL,
                                    [DivisionID] INT NULL,
                                    [DivisionName] NVARCHAR(100) NULL,
                                    [IpAddress] NVARCHAR(50) NULL,
                                    [Module] NVARCHAR(30) NOT NULL,
                                    [CaseID] INT NULL,
                                    [CaseNumber] NVARCHAR(100) NOT NULL,
                                    [VehicleNo] NVARCHAR(50) NULL,
                                    [CourtName] NVARCHAR(150) NULL,
                                    [ActionType] NVARCHAR(30) NOT NULL,
                                    [ActionSummary] NVARCHAR(500) NOT NULL,
                                    [ChangedFieldsSummary] NVARCHAR(MAX) NULL,
                                    [OldValuesJson] NVARCHAR(MAX) NULL,
                                    [NewValuesJson] NVARCHAR(MAX) NULL,
                                    CONSTRAINT [PK_CASE_ACTIVITY_LOGS] PRIMARY KEY CLUSTERED ([LogID])
                                );

                                CREATE NONCLUSTERED INDEX [IX_ACTIVITY_LOGS_Timestamp] ON [dbo].[CASE_ACTIVITY_LOGS] ([Timestamp] DESC);
                                CREATE NONCLUSTERED INDEX [IX_ACTIVITY_LOGS_Division_Action] ON [dbo].[CASE_ACTIVITY_LOGS] ([DivisionID], [ActionType], [Timestamp] DESC);
                                CREATE NONCLUSTERED INDEX [IX_ACTIVITY_LOGS_CaseNumber] ON [dbo].[CASE_ACTIVITY_LOGS] ([CaseNumber]);
                                CREATE NONCLUSTERED INDEX [IX_ACTIVITY_LOGS_Module_Date] ON [dbo].[CASE_ACTIVITY_LOGS] ([Module], [Timestamp] DESC);
                            END;
                        ";

                        using (var schemaCmd = new SqlCommand(activityLogSql, conn))
                        {
                            schemaCmd.CommandTimeout = 120;
                            schemaCmd.ExecuteNonQuery();
                        }

                        migTimer11.Stop();

                        string recordSql12 = @"
                            INSERT INTO SCHEMA_MIGRATIONS (MigrationID, Description, ExecutionTimeMs)
                            VALUES (@id, @desc, @time)";
                        using (var recordCmd12 = new SqlCommand(recordSql12, conn))
                        {
                            recordCmd12.Parameters.Add(new SqlParameter("@id", SqlDbType.NVarChar, 200) { Value = v1_2_0_CaseActivityLogsMigrationId });
                            recordCmd12.Parameters.Add(new SqlParameter("@desc", SqlDbType.NVarChar, 500) { Value = "Enterprise Case Activity & Audit Log Sheet: CASE_ACTIVITY_LOGS" });
                            recordCmd12.Parameters.Add(new SqlParameter("@time", SqlDbType.Int) { Value = (int)migTimer11.ElapsedMilliseconds });
                            recordCmd12.ExecuteNonQuery();
                        }
                        logger.LogInformation("Migration {MigrationId} applied successfully in {Elapsed}ms.", v1_2_0_CaseActivityLogsMigrationId, migTimer11.ElapsedMilliseconds);
                    }
                }
                finally
                {
                    if (lockAcquired)
                    {
                        try
                        {
                            using var releaseCmd = new SqlCommand("sp_releaseapplock", conn);
                            releaseCmd.CommandType = CommandType.StoredProcedure;
                            releaseCmd.Parameters.AddWithValue("@Resource", "LawProject_Database_Migration_Lock");
                            releaseCmd.Parameters.AddWithValue("@LockOwner", "Session");
                            releaseCmd.ExecuteNonQuery();
                        }
                        catch { /* non-critical cleanup */ }
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Database migration runner encountered an error: {Message}", ex.Message);
                throw;
            }
        }
    }
}
