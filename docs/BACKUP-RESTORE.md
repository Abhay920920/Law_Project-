# BACKUP & RESTORE PROCEDURE

## 1. Backup Strategy & Objectives

- **Recovery Point Objective (RPO)**: <= 15 minutes.
- **Recovery Time Objective (RTO)**: <= 60 minutes.
- **Data Classification**: Legally sensitive, confidential corporate records.

---

## 2. Backup Architecture

### 2.1 Backup Cadence
1. **Full Database Backup**: Daily at 01:00 AM IST.
2. **Differential Backup**: Every 6 hours (07:00, 13:00, 19:00 IST).
3. **Transaction Log Backup**: Every 15 minutes during active business hours (08:00 - 20:00 IST).

### 2.2 Backup Command Example
```sql
BACKUP DATABASE [NWKRTC_LawProject]
TO DISK = 'E:\Backups\NWKRTC_LawProject_Full_$(ESCAPE_NONE(DATE)).bak'
WITH FORMAT, 
     MEDIANAME = 'NWKRTC_LawProject_FullBackup',
     NAME = 'Full Backup of NWKRTC_LawProject',
     COMPRESSION,
     CHECKSUM;
```

---

## 3. Non-Production Restore Verification

**Critical Rule**: A backup that has never been restored successfully must NOT be considered verified.

### 3.1 Verification Steps
1. Verify header and checksum integrity:
   ```sql
   RESTORE VERIFYONLY FROM DISK = 'E:\Backups\NWKRTC_LawProject_Full.bak';
   ```
2. Test restore onto a staging/test SQL Server instance:
   ```sql
   RESTORE DATABASE [NWKRTC_LawProject_RestoreTest]
   FROM DISK = 'E:\Backups\NWKRTC_LawProject_Full.bak'
   WITH MOVE 'NWKRTC_LawProject' TO 'E:\Data\NWKRTC_LawProject_Test.mdf',
        MOVE 'NWKRTC_LawProject_Log' TO 'E:\Data\NWKRTC_LawProject_Test.ldf',
        REPLACE;
   ```
3. Run post-restore sanity queries:
   - Check row counts on `MVC_CASES`, `LABOUR_CASES`, `GRATUITY_CASES`.
   - Verify foreign key integrity between `MVC_CASES` and `ADVERSE_AWARDS`.
   - Verify user accounts and role definitions in `USER_MASTER`.
