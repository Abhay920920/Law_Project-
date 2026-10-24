# 💾 NWKRTC Law Management System — Data Integrity & Recovery Strategy

**Document Reference**: Phase Four Data Protection & Non-Destructive Engineering Plan  
**Target Database**: `Admin_Law` on Microsoft SQL Server 2019+  
**Application Target**: `MVCCaseManagement` (.NET 9.0)  
**Date**: October 24, 2026  

---

## 📌 Executive Statement on Data Protection

Under the non-negotiable engineering rules:
1. **Zero Data Loss**: No legal claims, financial records, installment ledgers, or court decrees may ever be deleted, truncated, reset, or overwritten.
2. **Zero In-Place Destruction**: All migrations must be additive and backward-compatible.
3. **No Unverified Claims**: Data loss prevention requires verified recovery procedures, transactional boundaries, and distributed locks.

---

## 1. Database Protection & Backup Verification Protocol

### 1.1 Live Production Safeguards
- The live database endpoint `198.38.89.31` (`Admin_Law`) is strictly protected. No ad-hoc, untracked, or destructive DDL/DML scripts are executed directly.
- Synthetic and automated regression tests run strictly on mock contexts or in-memory collections, ensuring zero test data pollutes production records.

### 1.2 Recommended Production Backup Script (SQL Server Agent / DBA Runbook)
```sql
-- Full Database Backup with Checksum and Verification
BACKUP DATABASE [Admin_Law]
TO DISK = N'D:\SQL_Backups\Admin_Law_Full_PreRemediation.bak'
WITH FORMAT, INIT,
NAME = N'Admin_Law-Full Backup Before Security Remediation',
SKIP, NOREWIND, NOUNLOAD, COMPRESSION, CHECKSUM, STATS = 10;
GO

-- Verify Backup File Integrity
RESTORE VERIFYONLY
FROM DISK = N'D:\SQL_Backups\Admin_Law_Full_PreRemediation.bak'
WITH CHECKSUM;
GO
```

---

## 2. Schema Migration Safety & Concurrency Locking

### 2.1 Distributed Lock via `sp_getapplock`
In [`DatabaseMigrationRunner.cs`](file:///c:/Users/adts-/Desktop/Law%20Project/Law%20Project/CaseManagement/DAL/DatabaseMigrationRunner.cs), migrations execute inside a SQL Server application lock:
```sql
EXEC @result = sp_getapplock 
    @Resource = 'MVCCaseManagement_Migrations', 
    @LockMode = 'Exclusive', 
    @LockOwner = 'Session', 
    @LockTimeout = 30000;
```
This guarantees that multiple instances running under IIS web gardens or load-balanced clusters cannot execute concurrent schema migrations or corrupt migration history.

### 2.2 Additive Migration Rules
- Columns are added via `db.EnsureColumn(...)` only if `INFORMATION_SCHEMA.COLUMNS` indicates they do not exist.
- Data types are never narrowed (e.g. `NVARCHAR(MAX)` or `DECIMAL(18,2)` preserved).
- Default constraints are specified without modifying existing row values.

---

## 3. Transactional Integrity Across Legal & Financial Operations

### 3.1 Multi-Table Transaction Boundaries
All operations spanning multiple relational tables must execute within an explicit `SqlTransaction`:
1. **Arising Applications**: Insert into `LABOUR_ARISING_APPLICATIONS` and initial ledger entry in `tbl_Arising_Application_Payments` are committed atomically.
2. **Execution Petitions (EP)**: Linking an EP to an MVC or Labour dispute updates the parent case stage and inserts the EP record within a single transaction.
3. **Gratuity Calculations**: Computing 10% statutory delayed interest updates `GRA_CASES` and appends to `tbl_Gratuity_Interest` atomically.
4. **Petty Bills**: Previous payment deduplication and itemized disbursement ledgers are committed in a single atomic transaction in `PettyBillRepository.cs`.

### 3.2 Rollback Invariant
If an intermediate statement fails due to constraint violations, deadlocks, or timeouts:
- The `SqlTransaction.Rollback()` method executes immediately.
- The error is logged with structured correlation ID (`X-Correlation-ID`).
- The user receives an actionable error response without partial state commit.

---

## 4. File-Storage & Database Metadata Consistency

### 4.1 Safe File-Write / DB-Commit Pattern
Physical file uploads (e.g., judgment copies, stay orders, medical proofs) must maintain consistency with database metadata:
1. File is uploaded and validated for extension and MIME type.
2. Stored with a cryptographically unique filename (e.g. `{guid}_{sanitizedName}`) in canonical `uploads/` directory.
3. Database record is updated with relative path.
4. If database update fails, the newly uploaded file is quarantined or cleaned up, preventing orphaned clutter while ensuring the previous valid attachment record remains unaltered.
5. Deletion of cases or applications never blindly deletes physical files that may be shared or referenced across connected matters (e.g., connected MFA appeals).

---

## 5. Recovery Procedures for Interrupted Operations

| Failure Scenario | Automatic System Behavior | Operational Recovery Procedure |
| :--- | :--- | :--- |
| **IIS AppPool Restart during e-Courts Sync** | `NapixBackgroundSyncService` worker thread terminates. State in `NAPIX_SYNC_QUEUE` remains `Pending` or `In_Progress`. | On next startup, `NapixSyncEngine` identifies stale `In_Progress` items older than 30 minutes and re-queues them with priority. |
| **Database Network Drop during Case Registration** | ADO.NET connection timeout triggers; transaction rolls back automatically on SQL Server. | Client receives 500 error; no orphaned rows created. Legal officer resubmits form. |
| **External NAPIX Gateway Timeout (504)** | `ECourtsNapixService` logs timeout; queue item attempt count increments up to 3 with exponential backoff. | If max attempts reached, state marked `FAILED` with error text. Quota counter preserved. |
| **SMS Gateway Outage** | SMS send attempt fails; error logged in `tbl_SMS_Logs`. | `NotificationBackgroundService` resumes next morning; critical hearing reminders dispatch on next cycle. |
