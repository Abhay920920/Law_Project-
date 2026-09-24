# DATABASE MIGRATION STRATEGY & RUNNER

## 1. Migration Overview

To eliminate unsafe runtime schema mutations and repeated startup introspection, database changes are managed through a centralized, ordered, versioned migration runner: `DatabaseMigrationRunner.cs`.

---

## 2. Migration Architecture

### 2.1 Ledger Table Schema
Every migration executed on any environment is permanently recorded in `SCHEMA_MIGRATIONS`:

```sql
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'SCHEMA_MIGRATIONS')
BEGIN
    CREATE TABLE SCHEMA_MIGRATIONS (
        MigrationId INT IDENTITY(1,1) PRIMARY KEY,
        MigrationVersion VARCHAR(50) NOT NULL UNIQUE,
        Description NVARCHAR(255) NOT NULL,
        AppliedOn DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        AppliedBy NVARCHAR(100) NOT NULL DEFAULT SUSER_SNAME(),
        ExecutionDurationMs INT NOT NULL
    );
END
```

### 2.2 Distributed Concurrency Control
To guarantee safety in multi-instance load-balanced environments, migrations acquire an exclusive application lock prior to evaluation:

```sql
EXEC @result = sp_getapplock 
    @Resource = 'NWKRTC_LawProject_Database_Migration',
    @LockMode = 'Exclusive',
    @LockOwner = 'Transaction',
    @LockTimeout = 30000;
```

If another container or server is currently running migrations, secondary instances wait or safely skip execution once the migration version is recorded.

---

## 3. Migration Sequence

| Version | Script Name / Description | Target Entities | Type |
|:---|:---|:---|:---|
| `V001` | Initial Baseline Schema | All Core Tables | Schema Init |
| `V002` | Add Arising Application and Reinstatement Columns | `LABOUR_CASES`, `LABOUR_CONNECTED_CASES` | Additive Column |
| `V003` | Performance Index Pack | `MVC_CASES`, `LABOUR_CASES`, `GRATUITY_CASES` | Indexes |
| `V004` | Audit Log and Notification Tables | `AUDIT_LOGS`, `NOTIFICATIONS` | Schema Ext |

---

## 4. Rollback & Disaster Recovery Rules

- **Strict Non-Destructive Policy**: Rollback scripts must never execute `DROP TABLE` or `DROP COLUMN` on tables containing historical legal data.
- **Backup Verification Requirement**: Before applying any major schema change in production, an automated full backup verification (`RESTORE VERIFYONLY`) is required.
