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
