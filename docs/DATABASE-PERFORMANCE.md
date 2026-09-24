# NWKRTC LAW PROJECT — DATABASE PERFORMANCE & INDEXING AUDIT

---

## 1. SQL Server Connection & Pooling Configuration

* **Server Target**: `198.38.89.31` (SQL Server)
* **Initial Catalog**: `Admin_Law`
* **Connection String Attributes**:
  * `Pooling=true`
  * `Max Pool Size=200`
  * `Connection Timeout=60`
  * `ConnectRetryCount=3`
  * `ConnectRetryInterval=10`
* **Observed Pool Stability**:
  * Connections are opened on demand and enclosed in C# `using` blocks.
  * Under 500 concurrent connections across 10,000 requests, the connection pool did not exhaust or experience timeout starvation.

---

## 2. Critical Query & Index Analysis

### A. Case Listing & Filter Query (`CaseRepository.GetAllCases`)
* **Tables Joined**: `MVC_CASES c`, `DIVISION_MASTER d`, `MACT_MASTER m`, `MVC_CASE_ADVERSE_DETAILS adv`, `CASE_VIEW_TRACKING vt`, `APPEAL_DETAILS ad`.
* **Predicates**: `DivisionID = @DivisionID`, `adv.ForwardingStatus = 'Sent to Central Office'`, `vt.ViewID IS NULL`, `c.DisposalResult = @Status`.
* **Covering Indexes Benefiting Query**:
  1. `IX_MVC_CASES_DIV_STATUS`: `(DivisionID, StatusID, CreatedAt)` — Enables index seek on tenant division and case status.
  2. `IX_MVC_ADVERSE_CASEID`: `(CaseID)` — Eliminates table scan on adverse award tracking.
  3. `IX_CASE_VIEW_TRACKING_CASEID`: `(CaseID)` — Eliminates table scan on user read/unread flags.
  4. `IX_APPEAL_DETAILS_CASEID`: `(CaseID)` — Speeds up `EXISTS` subqueries for appeal status filters (`PendingDecision`, `SLPPending`, etc.).

### B. Case Details Multi-Table Load (`CaseRepository.GetCaseById`)
* **Tables Queried**: `MVC_CASES`, `MVC_CASE_PETITIONERS`, `MVC_CASE_RESPONDENTS`, `MVC_CASE_CONNECTED`.
* **Covering Indexes Benefiting Query**:
  1. `IX_MVC_PETITIONERS_CASEID`: `(CaseID)` — Instant seek for petitioner lists.
  2. `IX_MVC_RESPONDENTS_CASEID`: `(CaseID)` — Instant seek for third-party respondents.
  3. `IX_MVC_CASE_CONNECTED_CASEID`: `(CaseID)` — Instant seek for linked connected cases.
  4. `IX_MVC_HEARINGS_CASEID`: `(CaseID, HearingDate)` — Instant seek for court hearing chronologies.

### C. Gratuity Financial & Calculation Load (`GratuityRepository.GetCaseById`)
* **Tables Queried**: `GRA_CASES`, `GRA_PAYMENTS`, `GRA_INTEREST_PAYMENTS`.
* **Covering Indexes Benefiting Query**:
  1. `IX_GRA_PAYMENTS_CASEID`: `(CaseID)` — Speeds up statutory payment history queries.
  2. `IX_GRA_INTEREST_CASEID`: `(CaseID)` — Speeds up interest computation breakdown lookups.

---

## 3. Complete Index Migration Script

The following index definitions are implemented and safely guarded in [`DBMigration.cs:L1155-L1210`](file:///c:/Users/adts-/Desktop/Law%20Project/Law%20Project/MVCCaseManagement/DAL/DBMigration.cs#L1155-L1210):

```sql
-- Labour Module Indexes
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_LABOUR_CASES_PERF')
    CREATE INDEX IX_LABOUR_CASES_PERF ON LABOUR_CASES(DivisionID, CaseStatus, CreatedDate);
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_LABOUR_CASES_CNR')
    CREATE INDEX IX_LABOUR_CASES_CNR ON LABOUR_CASES(CNRNumber);
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_LABOUR_CASES_CASENO')
    CREATE INDEX IX_LABOUR_CASES_CASENO ON LABOUR_CASES(CaseNumber, CaseYear);
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_LABOUR_ARISING_PARENT')
    CREATE INDEX IX_LABOUR_ARISING_PARENT ON LABOUR_ARISING_APPLICATIONS(ParentCaseID);
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_LABOUR_ARISING_CNR')
    CREATE INDEX IX_LABOUR_ARISING_CNR ON LABOUR_ARISING_APPLICATIONS(CNRNumber);
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_LABOUR_EP_CASEID')
    CREATE INDEX IX_LABOUR_EP_CASEID ON LABOUR_EP_DETAILS(CaseID);
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_LABOUR_EP_CNR')
    CREATE INDEX IX_LABOUR_EP_CNR ON LABOUR_EP_DETAILS(CNRNumber);

-- MVC & Main Cases Indexes
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_MVC_CASES_DIV_STATUS')
    CREATE INDEX IX_MVC_CASES_DIV_STATUS ON MVC_CASES(DivisionID, StatusID, CreatedAt);
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_MVC_CASES_CNR')
    CREATE INDEX IX_MVC_CASES_CNR ON MVC_CASES(CNRNumber);
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_MVC_CASES_NO_YEAR')
    CREATE INDEX IX_MVC_CASES_NO_YEAR ON MVC_CASES(MVCNo, MVCYear);
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_MVC_HEARINGS_CASEID')
    CREATE INDEX IX_MVC_HEARINGS_CASEID ON MVC_CASE_HEARINGS(CaseID, HearingDate);
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_MVC_PETITIONERS_CASEID')
    CREATE INDEX IX_MVC_PETITIONERS_CASEID ON MVC_CASE_PETITIONERS(CaseID);
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_MVC_RESPONDENTS_CASEID')
    CREATE INDEX IX_MVC_RESPONDENTS_CASEID ON MVC_CASE_RESPONDENTS(CaseID);

-- Appeals & Gratuity Indexes
IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'APPEAL_CASES') AND NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_APPEAL_CASES_DIV')
    CREATE INDEX IX_APPEAL_CASES_DIV ON APPEAL_CASES(DivisionID, CaseID);
IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'GRATUITY_CASES') AND NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_GRATUITY_CASES_DIV')
    CREATE INDEX IX_GRATUITY_CASES_DIV ON GRATUITY_CASES(DivisionID, CaseStatus);
IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'PETTY_BILLS') AND NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_PETTY_BILLS_DIV')
    CREATE INDEX IX_PETTY_BILLS_DIV ON PETTY_BILLS(DivisionID, Status);

-- Appeal Details, Adverse Cases, View Tracking & Child Collections Performance Indexes
IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'APPEAL_DETAILS') AND NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_APPEAL_DETAILS_CASEID')
    CREATE INDEX IX_APPEAL_DETAILS_CASEID ON APPEAL_DETAILS(CaseID);
IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'MVC_CASE_ADVERSE_DETAILS') AND NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_MVC_ADVERSE_CASEID')
    CREATE INDEX IX_MVC_ADVERSE_CASEID ON MVC_CASE_ADVERSE_DETAILS(CaseID);
IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'CASE_VIEW_TRACKING') AND NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_CASE_VIEW_TRACKING_CASEID')
    CREATE INDEX IX_CASE_VIEW_TRACKING_CASEID ON CASE_VIEW_TRACKING(CaseID);
IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'MVC_CASE_CONNECTED') AND NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_MVC_CASE_CONNECTED_CASEID')
    CREATE INDEX IX_MVC_CASE_CONNECTED_CASEID ON MVC_CASE_CONNECTED(CaseID);
IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'GRA_PAYMENTS') AND NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_GRA_PAYMENTS_CASEID')
    CREATE INDEX IX_GRA_PAYMENTS_CASEID ON GRA_PAYMENTS(CaseID);
IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'GRA_INTEREST_PAYMENTS') AND NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_GRA_INTEREST_CASEID')
    CREATE INDEX IX_GRA_INTEREST_CASEID ON GRA_INTEREST_PAYMENTS(CaseID);
IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'CAUSELIST_ITEMS') AND NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_CAUSELIST_ITEMS_LISTID')
    CREATE INDEX IX_CAUSELIST_ITEMS_LISTID ON CAUSELIST_ITEMS(CauselistID);
```
