# 📋 NWKRTC Law Management System — Implementation Baseline Report

**Execution Context**: Security Remediation & Data Integrity Preservation Protocol  
**Date**: October 24, 2026  
**Git Branch**: `security-remediation` (branched from `main` @ `f8432f3`)  
**Backup Branch**: `backup-main-before-remediation`  
**Solution Target**: `LawProject.sln` (.NET 9.0 / .NET 8.0, Microsoft SQL Server 2019+, Local Ollama Qwen2.5:7b)  

---

## 1. Repository & Working-Tree State

### 1.1 Git Status & Branch Isolation
- **Active Remediation Branch**: `security-remediation`
- **Immutable Backup Branch**: `backup-main-before-remediation` (matches remote `origin/main` commit `f8432f3`)
- **Working Tree Integrity**: All pre-existing working modifications preserved without data loss or reset.

### 1.2 Preserved Uncommitted Changes Inventory
The following 12 pre-existing modified files in the working directory have been preserved:
1. `Law Project/CaseManagement/Controllers/ApiDashboardController.cs` (Removal of `[IgnoreAntiforgeryToken]`)
2. `Law Project/CaseManagement/Controllers/CaseController.cs` (Removal of `[IgnoreAntiforgeryToken]` from mutation actions)
3. `Law Project/CaseManagement/Controllers/ECourtsController.cs` (Hardened antiforgery and NAPIX response formatting)
4. `Law Project/CaseManagement/Controllers/LabourController.cs` (Removal of `[IgnoreAntiforgeryToken]`)
5. `Law Project/CaseManagement/Controllers/NotificationController.cs` (Antiforgery enforcement)
6. `Law Project/CaseManagement/Controllers/NyayaPathaController.cs` (CSRF token enforcement on AI query endpoints)
7. `Law Project/CaseManagement/Controllers/ReportController.cs` (Petty bill calculation and export logic additions)
8. `Law Project/CaseManagement/Controllers/UploadsController.cs` (Initial database check before streaming physical files)
9. `Law Project/CaseManagement/Views/Case/Edit.cshtml` (Safe JavaScript JSON serialization)
10. `Law Project/CaseManagement/Views/Labour/Details.cshtml` (Safe CNR serialization)
11. `Law Project/CaseManagement/Views/Shared/_Layout.cshtml` (Antiforgery request verification header configuration)
12. `Law Project/CaseManagement/Views/Shared/_OtherCourtsLayout.cshtml` (Layout navigation and security headers)

Untracked agent tools preserved:
- `.agents/` (Security scanning and engineering rule definitions)
- `skills-lock.json`

---

## 2. Build & Test Baseline Verification

### 2.1 Compilation Status
- **Command Executed**: `dotnet build LawProject.sln -c Release`
- **Status**: **0 Compilation Errors**, 5 Compiler Warnings (Roslyn nullable annotations)
- **Projects Built**:
  - `NapixEcourtsApi` (`net8.0`) $\to$ `bin/Release/net8.0/NapixEcourtsApi.dll`
  - `MVCCaseManagement` (`net9.0`) $\to$ `bin/Release/net9.0/MVCCaseManagement.dll`
  - `MVCCaseManagement.Tests` (`net9.0`) $\to$ `bin/Release/net9.0/MVCCaseManagement.Tests.dll`

### 2.2 Automated Test Execution Baseline
- **Command Executed**: `dotnet test "Law Project/MVCCaseManagement.Tests/MVCCaseManagement.Tests.csproj" -c Release`
- **Framework**: xUnit on .NET 9.0 (VSTest 17.14.1 x64)
- **Total Tests**: **284**
- **Passed**: **284 (100%)**
- **Failed**: **0 (0%)**
- **Skipped**: **0**
- **Execution Duration**: 30 seconds
- **Test Suites Verified (17 Suites)**:
  1. `CaseFilterIntegrationTests`: Divisional filter and status queries
  2. `GratuityCalculationTests`: 10% statutory delayed interest & deduction formulas
  3. `NapixDirectPipelineTests`: NAPIX v1.3 encryption, signing, and response parsing
  4. `NapixSyncEngineTests`: Hourly sync job scheduling and quota enforcement
  5. `NumberTextTests`: Currency to words conversion (Indian Rupee formatting)
  6. `NyayaPathaAdvancedPhasesIntegrationTests`: RAG pipeline and multi-source ranking
  7. `NyayaPathaAdvancedRAGEvaluationTests`: Precedent relevance scoring
  8. `NyayaPathaGoldenEvaluationTests`: Legal accuracy on golden benchmark dataset
  9. `NyayaPathaMaster100EvaluationSuiteTests`: Stress testing on 100 benchmark legal prompts
  10. `NyayaPathaPettyBillRAGTests`: Automated petty bill fee duplicate detection
  11. `NyayaPathaRealSqlIntegrationTests`: Dynamic schema retrieval and allowlisting
  12. `NyayaPathaReproducibleEvaluationBenchmarkTests`: Deterministic LLM response scoring
  13. `NyayaPathaSecurityAndAITests`: Divisional isolation and prompt injection defenses
  14. `PasswordHelperTests`: Salted PBKDF2 hashing and verification
  15. `PettyBillSerializationTests`: JSON payload deserialization and net payable invariants
  16. `UrlSecurityValidatorTests`: SSRF protection, IP filtering, and domain allowlists
  17. `CaseFilterIntegrationTests`: Cross-division search boundaries

---

## 3. Database Target & Migration Baseline

### 3.1 Connection Target Identity (Sanitized)
- **Server Endpoint**: Remote Host (`198.38.89.31`)
- **Target Database**: `Admin_Law`
- **Failover / Pool Settings**: `Pooling=true;Max Pool Size=200;MultiSubnetFailover=True;ConnectRetryCount=3;`
- **Security Rule**: Zero destructive DDL or DML execution against this live remote instance. All test suites operate in-memory or on isolated synthetic test structures.

### 3.2 Schema Architecture & Migrations
- **Total Tables**: 83 relational tables, 52 foreign key constraints.
- **Migration Engine**: Zero-downtime dynamic schema migration runner implemented in `DBMigration.cs` and `DatabaseMigrationRunner.cs`.
- **Concurrency Guard**: Distributed application locks via `sp_getapplock` (`MVCCaseManagement_Migrations`) to eliminate multi-instance race conditions on startup.

---

## 4. Architectural Modules & Integration Contracts

| Module | Primary Controller | DAL Repository | Integration / Engine |
| :--- | :--- | :--- | :--- |
| **MVC / MACT Claims** | `CaseController.cs` | `CaseRepository.cs` | TR-18 Accident Portal, Appeal Tracking |
| **Labour Court & ID** | `LabourController.cs` | `LabourRepository.cs` | Multi-tier review (LO $\to$ MD), Official A4 Dossier |
| **Arising Applications** | `LabourController.cs` | `ArisingApplicationRepository.cs` | Section 33-C(2) parent dispute linking & installment ledger |
| **Gratuity Claims** | `GratuityController.cs` | `GratuityRepository.cs` | 10% statutory delayed interest engine, CA/AA tracking |
| **Execution Petitions (EP)** | `EPController.cs`, `LabourEPController.cs` | `EPRepository.cs`, `LabourEPRepository.cs` | Attachment notices, court deposit tracking |
| **Other Courts** | `HomeController.cs` (OtherCourts) | `OtherCourtsRepository.cs` | Civil suits, Consumer forums, Tahsildar |
| **Petty Bills** | `ReportController.cs` | `PettyBillRepository.cs` | Previous payment deduplication & fee vetting |
| **e-Courts NAPIX** | `ECourtsController.cs` | `ECourtsRepository.cs` | AES-128-CBC / HMAC-SHA256, CNR auto-discovery |
| **Nyaya Patha AI** | `NyayaPathaController.cs` | 34 AI services | Local Ollama Qwen2.5:7b, Search-First RAG |
| **Notifications** | `NotificationController.cs` | `NotificationRepository.cs` | Karnataka Mobile-1 Gateway SMS, Background workers |

---

## 5. Security & Risk Baseline Summary

1. **[SEC-01] Partial IDOR in Physical File Download (`UploadsController.cs`)**: Division check only queries `MVC_CASE_ADVERSE_DETAILS`, leaving Labour, Arising, Gratuity, and Petty Bill documents accessible if `attachedCaseDivId == 0`.
2. **[SEC-02] IDOR in Execution Petitions (`EPController.cs` & `LabourEPController.cs`)**: `Edit` GET and POST methods do not verify `ep.DivisionID == userDivisionId`.
3. **[SEC-03] IDOR in Arising Applications (`LabourController.cs`)**: `DeleteArisingApplication(id)` and `EditArisingApplication` lack division boundary checks.
4. **[SEC-04] Cleartext Credentials**: Plaintext production credentials stored in `appsettings.json` and backup file `appsettings.json.unencrypted.bak`.
5. **[SEC-05] Unauthenticated MAC Spoofing**: `NetworkDeviceHelper.cs` trusts client-supplied headers for MAC addresses.
