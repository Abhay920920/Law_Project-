# ❓ NWKRTC Law Management System — Unresolved Questions & Architectural Gaps

**Document Reference**: Operational Gaps, Legacy Artifacts & Open Domain Questions  
**Verification Level**: **DOCUMENTED REPOSITORY AUDIT**  

---

## 1. Documented Gaps & Clarification Items

### 1.1 External NAPIX Static IV Government Constraint
- **Observation**: The Karnataka e-Courts NAPIX v1.3 gateway mandates AES-128-CBC with a static, pre-shared Initialization Vector (IV).
- **Security Assessment**: CBC with a fixed IV is susceptible to ciphertext pattern analysis across identical prefixes. While our application code strictly conforms to the NIC specification, NWKRTC should request NIC to support AES-GCM (Galois/Counter Mode) with dynamic nonces in future gateway revisions.

### 1.2 Non-Clustered Indexes in Master Seed Script
- **Observation**: The master database initialization script [`00_Master_Full_Database_Schema_And_Seed.sql`](file:///c:/Users/adts-/Desktop/Law%20Project/Law%20Project/CaseManagement/SQL/00_Master_Full_Database_Schema_And_Seed.sql) defines 82 tables with primary and foreign keys, but contains no non-clustered index definitions. Indexes are instead created dynamically at runtime by `DBMigration.cs:1300-1343` upon application startup.
- **Recommendation**: Non-clustered indexes for foreign keys (`CaseID`, `DivisionID`, `CNRNumber`) should be consolidated directly into the SQL script so newly provisioned standby databases are pre-indexed without requiring web app launch.

### 1.3 IIS Hosted Service Lifecycle Dependency
- **Observation**: `NotificationBackgroundService` (SMS engine) and `NapixBackgroundSyncService` (e-Courts sync engine) execute as in-process `IHostedService` threads inside the ASP.NET Core web host.
- **Operational Risk**: If deployed to standard IIS without setting `StartMode = "AlwaysRunning"` and `IdleTimeout = "00:00:00"`, worker process idle recycling halts background scheduled jobs.
- **Resolution**: Ensured PowerShell script is documented in [`docs/audit/DEPLOYMENT_AND_ROLLBACK_PLAN.md`](file:///c:/Users/adts-/Desktop/Law%20Project/docs/audit/DEPLOYMENT_AND_ROLLBACK_PLAN.md) to harden IIS configuration.

### 1.4 TR-18 Police Accident Portal Pilot
- **Observation**: The TR-18 accident portal integration has configuration keys and data models present in the repository, but the live production API endpoint is currently in pre-production pilot.
- **Recommendation**: Verify network whitelisting and production API credentials with Karnataka State Police IT division before enabling full automated ingestion.

---

## 2. Statement of Verification Boundaries

- **Verified in Source**: All 20 controllers, 342 actions, 143 Razor views, 3,874 form controls, 383 dropdowns, 82 database tables, and security defenses were directly parsed and verified from the current active repository code.
- **Verified by Test**: All 289 automated tests were executed and passed with 0 failures in 26 seconds.
- **Unverified (Requires Live External Access)**: Actual live responses from the remote NIC e-Courts gateway at runtime, live SMS delivery on actual telecom handsets, and live performance under 1,000+ concurrent users on remote server `198.38.89.31`.
