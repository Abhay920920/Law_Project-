# NWKRTC LAW PROJECT — PRIORITIZED PERFORMANCE BOTTLENECKS & FINDINGS

---

## 1. Summary of Priority Findings

| ID | Bottleneck / Anti-Pattern | Category | Severity | Status | Impact on Production |
| :--- | :--- | :---: | :---: | :---: | :--- |
| **F-01** | Missing Foreign Key Indexes on Child Tables (`APPEAL_DETAILS`, `MVC_CASE_ADVERSE_DETAILS`, `CASE_VIEW_TRACKING`, `MVC_CASE_CONNECTED`, `GRA_PAYMENTS`) | Database | **HIGH** | **RESOLVED** | Index seek instead of full table scans during case listings and details views. |
| **F-02** | Redundant Master Data Database Queries on Every Page Render (`GetAllDivisions`, `GetAllMACTs`, `GetAllAdvocates`) | Application | **MEDIUM** | **RESOLVED** | Added `IMemoryCache` (30m TTL) with write invalidation. Eliminates 4–8 DB hits per page request. |
| **F-03** | Sequential Multi-Query Execution on Single Case Load (`GetCaseById`) | DAL / Network | **MEDIUM** | **ACCEPTED (Architectural)** | Sequential child table queries over remote SQL connection. Preserved for exact domain model mapping and transaction safety. |
| **F-04** | Large PDF Court Judgment In-Memory Buffering | Memory / HTTP | **MEDIUM** | **RESOLVED** | Streamed via `ReadAsStreamAsync()` directly into response pipeline with magic-byte validation. |
| **F-05** | External Government Gateway Latency (`delhigw.napix.gov.in`) | External API | **HIGH (External)** | **MITIGATED** | Gateway round-trip takes 550ms–1200ms. Throttled background sync (1s sleep) prevents rate limiting. Token cached for 50 min. |
| **F-06** | Multi-Instance Background Sync Race Conditions | Concurrency | **HIGH** | **RESOLVED** | Distributed non-blocking locking implemented via `sp_getapplock` and `sp_releaseapplock`. |

---

## 2. Detailed Root-Cause Analysis

### Finding F-01: Missing Foreign Key Indexes on Child Tables
- **Root Cause**: Tables like `APPEAL_DETAILS`, `MVC_CASE_ADVERSE_DETAILS`, and `MVC_CASE_CONNECTED` lacked indexes on `CaseID`.
- **Observed Behavior**: Joins and subqueries in `GetAllCases` and `GetCaseById` performed index scans or table scans across entire tables.
- **Implemented Fix**: Added idempotent `CREATE INDEX` scripts into [`DBMigration.cs:L1192-L1210`](file:///c:/Users/adts-/Desktop/Law%20Project/Law%20Project/MVCCaseManagement/DAL/DBMigration.cs#L1192-L1210).

### Finding F-02: Repeated Static Master Data Queries
- **Root Cause**: `MasterRepository` executed SQL queries against `DIVISION_MASTER`, `MACT_MASTER`, and `MVC_DIVISION_ADVOCATES` on every single page load containing dropdown filters.
- **Observed Behavior**: High connection churn and memory allocation of transient `DataTable` objects.
- **Implemented Fix**: Introduced in-memory caching (`IMemoryCache`) in [`MasterRepository.cs:L61-L245`](file:///c:/Users/adts-/Desktop/Law%20Project/Law%20Project/MVCCaseManagement/DAL/MasterRepository.cs#L61-L245) with automatic cache clearance on master record additions, updates, or deletions.
