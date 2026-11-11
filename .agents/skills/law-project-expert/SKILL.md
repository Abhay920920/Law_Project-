---
name: law-project-expert
description: >-
  Authoritative knowledge base and technical reference for the NWKRTC Law Project codebase.
  Covers full directory blueprints, controller mappings, services, DAL repositories, functional modules
  (MVC/MACT, Labour, Gratuity, Other Courts, e-Courts Napix sync, Activity Log, Nyaya Patha AI),
  SQL Server 2012 database architecture on 198.38.89.31, multi-tenant security boundaries, and developer runbooks.
---

# 🏛️ NWKRTC Law Project — Comprehensive Technical Reference & System Architecture

> **Primary Application**: North Western Karnataka Road Transport Corporation (NWKRTC) Legal Case Management System  
> **Production URL**: `https://law.itnwkrtc.in`  
> **Framework & Runtime**: ASP.NET Core (.NET 9.0), C# 13, Dapper / ADO.NET  
> **Database Environment**: Microsoft SQL Server 2012 Express (SP3-GDR 11.0.6251.0 X64) on `198.38.89.31:1433` (`Admin_Law`)  
> **Web Server**: Kestrel behind IIS Reverse Proxy, Windows Server

---

## 🧭 Repository Structure & Component Map

```
Law Project/
├── Law Project/
│   ├── CaseManagement/                    # Main ASP.NET Core (.NET 9) Web Application
│   │   ├── Controllers/                   # 14 MVC/API Controllers
│   │   ├── DAL/                           # Data Access Layer (DBHelper, ADO.NET, Dapper)
│   │   ├── Models/                        # Domain entities, DTOs, ViewModels
│   │   ├── Services/                      # Domain & AI services
│   │   │   ├── AI/                        # Nyaya Patha AI & RAG Subsystem
│   │   │   │   ├── Catalog/               # Schema metadata catalog (NWKRTCSchemaCatalog)
│   │   │   │   ├── Compiler/              # T-SQL compiler & Strix AST safety guard
│   │   │   │   └── Presentation/          # Tabular presenter & currency formatters
│   │   │   └── Audit/                     # CaseActivityLogger & auditing
│   │   ├── Utils/                         # Shared helpers (NetworkDeviceHelper, PasswordHelper)
│   │   ├── Views/                         # Razor Views (Bootstrap 5, Glassmorphism UI)
│   │   ├── SQL/                           # Schema migrations (01 through 64)
│   │   ├── Program.cs                     # Startup, ForwardedHeaders, DI, Cookie Authentication
│   │   └── appsettings.json               # Database connection strings, API keys, SMS, AI config
│   ├── MVCCaseManagement.Tests/           # xUnit Test Project (.NET 9)
│   │   ├── NyayaPathaDynamicEngineTests.cs
│   │   ├── NyayaPathaSecurityAndAITests.cs
│   │   └── Domain & calculation test suites
│   └── NapixEcourtsApi/                   # NJDG e-Courts Napix API Integration Client
└── .agents/                               # Antigravity Operating Directives, Skills, and Rules
    ├── rules/mandatory-skills-protocol.md
    └── skills/
        ├── law-project-expert/SKILL.md    # THIS FILE: Definitive Knowledge Base
        ├── graphify-8/                    # Dependency mapping skill
        ├── rules/ponytail.md              # Minimalist senior developer rules
        ├── find-security-vulnerabilities-in-code/ # Strix white-box security gate
        └── gsd-core-next/                 # Spec-driven phased execution loop
```

---

## 🏛️ Core Functional Modules

### 1. MVC / MACT Case Management
- **Key Controller**: [`CaseController.cs`](file:///c:/Users/IT/Desktop/Law%20Project/Law%20Project/CaseManagement/Controllers/CaseController.cs)
- **Primary DAL**: [`CaseRepository.cs`](file:///c:/Users/IT/Desktop/Law%20Project/Law%20Project/CaseManagement/DAL/CaseRepository.cs)
- **Key Tables**: `MVC_CASES`, `MVC_CASE_ADVERSE_DETAILS`, `MVC_CASE_PETITIONERS`, `MVC_CASE_EVIDENCES`, `MVC_CASE_NOTINGS`
- **Features**:
  - Motor Accident Claims Tribunal litigation lifecycle.
  - Tracking Claim Amount, Awarded Compensation, Interest, and Deposited amounts.
  - Execution Petitions (EP), Remand Back cases, Lok Adalat settlements.
  - TR-18 accident report integration via `TR18Service.cs`.

### 2. Labour Disputes Module
- **Key Controller**: [`LabourController.cs`](file:///c:/Users/IT/Desktop/Law%20Project/Law%20Project/CaseManagement/Controllers/LabourController.cs), [`LabourEPController.cs`](file:///c:/Users/IT/Desktop/Law%20Project/Law%20Project/CaseManagement/Controllers/LabourEPController.cs)
- **Primary DAL**: [`LabourRepository.cs`](file:///c:/Users/IT/Desktop/Law%20Project/Law%20Project/CaseManagement/DAL/LabourRepository.cs), [`LabourEPRepository.cs`](file:///c:/Users/IT/Desktop/Law%20Project/Law%20Project/CaseManagement/DAL/LabourEPRepository.cs)
- **Key Tables**: `LABOUR_CASES`, `LABOUR_SERVICE_MATTERS`, `LABOUR_EP_DETAILS`, `LABOUR_ARISING_APPLICATIONS`
- **Features**:
  - Industrial Disputes Act (KID, Section 10, Section 33C(2)), Reference cases, and Reinstatements.
  - Multi-tier workflow: Division submissions are forwarded (`SentToCO = 1`) to Central Office (CLO / Dy CLO / MD) for approval.
  - Once sent to CO, division users are locked from editing (`DivisionControllerReadOnlyFilter` / sent lock).
  - Deduplicated schema enforced by filtered unique index:
    `UX_LABOUR_CASES_Div_Court_Year_Num` on `(DivisionID, CourtID, CaseYear, CaseNumber)`.

### 3. Payment of Gratuity Module
- **Key Controller**: [`GratuityController.cs`](file:///c:/Users/IT/Desktop/Law%20Project/Law%20Project/CaseManagement/Controllers/GratuityController.cs)
- **Primary DAL**: [`GratuityRepository.cs`](file:///c:/Users/IT/Desktop/Law%20Project/Law%20Project/CaseManagement/DAL/GratuityRepository.cs)
- **Key Tables**: `GRA_CASES`, `GRA_CALCULATION_SETTINGS`, `GRA_APPEAL_DETAILS`
- **Features**:
  - Controlling Authority (CA) and Appellate Authority under Payment of Gratuity Act, 1972.
  - Automated statutory gratuity formula calculation, delayed interest computation, and compliance payment tracking.
  - DivisionCode tenancy filtering across all 9 operating divisions.

### 4. Other Courts Gateway
- **Key Controller**: [`HomeController.cs`](file:///c:/Users/IT/Desktop/Law%20Project/Law%20Project/CaseManagement/Controllers/HomeController.cs)
- **Key Tables**: `OTHER_COURTS_CASES`, `ECA_CASES`
- **Features**:
  - Original Suits (O.S. Corporation vs Claimant), Criminal Cases (CC), Consumer Disputes, Land Acquisition (LAC), and Employee's Compensation Act (ECA).

### 5. e-Courts NJDG Napix Integration
- **Key Controller**: [`ECourtsController.cs`](file:///c:/Users/IT/Desktop/Law%20Project/Law%20Project/CaseManagement/Controllers/ECourtsController.cs)
- **Primary Service**: [`HighCourtDailyCauseListScannerService.cs`](file:///c:/Users/IT/Desktop/Law%20Project/Law%20Project/CaseManagement/Services/HighCourtDailyCauseListScannerService.cs), [`NapixEcourtsClient.cs`](file:///c:/Users/IT/Desktop/Law%20Project/NapixEcourtsApi/NapixEcourtsClient.cs)
- **Features**:
  - API sync with National Judicial Data Grid via Napix gateway (`delhigw.napix.gov.in`).
  - High Court MFA daily cause list scanner strictly restricted to NWKRTC matters.
  - Automated CNR number linking, hearing date synchronization, and stage drift detection.

### 6. Activity Log & Security Tracking
- **Key Controller**: [`ActivityLogController.cs`](file:///c:/Users/IT/Desktop/Law%20Project/Law%20Project/CaseManagement/Controllers/ActivityLogController.cs)
- **Primary Service**: [`CaseActivityLogger.cs`](file:///c:/Users/IT/Desktop/Law%20Project/Law%20Project/CaseManagement/Services/Audit/CaseActivityLogger.cs)
- **Network Helper**: [`NetworkDeviceHelper.cs`](file:///c:/Users/IT/Desktop/Law%20Project/Law%20Project/CaseManagement/Utils/NetworkDeviceHelper.cs)
- **Key Table**: `CASE_ACTIVITY_LOGS`
- **Features**:
  - Comprehensive audit trail for 21 CRUD operations, logins, failed logins, password resets, and case mutations.
  - Automatic snapshot fallback for before/after field diffs when direct edits occur.
  - High-performance MAC resolution: calls `SendARP` only on private/local LAN IPs; skips WAN IPs to eliminate thread blocking.
  - Covering composite index: `IX_CASE_ACTIVITY_LOGS_Division_Timestamp` on `(DivisionID ASC, Timestamp DESC)`.

### 7. Nyaya Patha AI (ನ್ಯಾಯ ಪಥ) — 5-Tier Dynamic Intelligence Engine
- **Key Controller**: [`NyayaPathaController.cs`](file:///c:/Users/IT/Desktop/Law%20Project/Law%20Project/CaseManagement/Controllers/NyayaPathaController.cs)
- **Orchestrator**: [`UnifiedLegalResearchService.cs`](file:///c:/Users/IT/Desktop/Law%20Project/Law%20Project/CaseManagement/Services/AI/UnifiedLegalResearchService.cs)
- **Schema Catalog**: [`NWKRTCSchemaCatalog.cs`](file:///c:/Users/IT/Desktop/Law%20Project/Law%20Project/CaseManagement/Services/AI/Catalog/NWKRTCSchemaCatalog.cs)
- **T-SQL Compiler**: [`DynamicSqlCompiler.cs`](file:///c:/Users/IT/Desktop/Law%20Project/Law%20Project/CaseManagement/Services/AI/Compiler/DynamicSqlCompiler.cs)
- **AST Safety Guard**: [`StrixSqlSafetyGuard.cs`](file:///c:/Users/IT/Desktop/Law%20Project/Law%20Project/CaseManagement/Services/AI/Compiler/StrixSqlSafetyGuard.cs)
- **Presenter**: [`DynamicTabularPresenter.cs`](file:///c:/Users/IT/Desktop/Law%20Project/Law%20Project/CaseManagement/Services/AI/Presentation/DynamicTabularPresenter.cs)
- **Features**:
  - Dynamically answers ANY natural language question from the SQL Server database.
  - 100% database factual accuracy; zero synthetic benchmark bypasses.
  - Deterministic fast-path executes in < 50ms with 0 LLM tokens and operates fully offline.
  - Strix AST Safety Guard enforces read-only `SELECT`, rejects DDL/DML/exec/semicolons, and injects mandatory tenant isolation predicates.

---

## 🗄️ Database Architecture & T-SQL Dialect Rules

### Target Server Details
- **Engine**: Microsoft SQL Server 2012 Express (SP3-GDR 11.0.6251.0 X64)
- **Database**: `Admin_Law`
- **Host**: `198.38.89.31:1433`
- **User**: `admin_Law`

### Strict SQL Server 2012 Dialect Rules
When authoring raw SQL queries, Dapper commands, or migrations, you **MUST** adhere to SQL Server 2012 Express limitations:
1. ❌ **No `STRING_AGG()`** (SQL Server 2017+): Must use `FOR XML PATH('')` or correlated subqueries.
2. ❌ **No JSON functions** (`OPENJSON`, `JSON_VALUE`, `JSON_QUERY` are SQL Server 2016+): All JSON serialization and diff parsing must be executed in C# application layer.
3. ❌ **No `TRIM()`** (SQL Server 2017+): Always use `LTRIM(RTRIM(...))`.
4. ❌ **No `DROP TABLE IF EXISTS`** (SQL Server 2016+): Use `IF OBJECT_ID('dbo.Table', 'U') IS NOT NULL DROP TABLE dbo.Table`.
5. ⚠️ **Memory Buffer Cap**: SQL Server 2012 Express has a 1 GB maximum RAM buffer pool and 10 GB database size limit. Always enforce `TOP 100` windowing on dynamic or unindexed queries.

### Multi-Tenant Boundary Isolation Rules
- **Central Office & Admin**: `DivisionID == 0` or `DivisionID == 5`, or roles `Admin`, `CO`, `CLO`, `Dy CLO`, `MD` have statewide access.
- **Divisional Users**: Must strictly filter by the authenticated user's `DivisionID`:
  ```sql
  WHERE t.DivisionID = @TenantDivisionId
  ```
- **Fail-Closed Security**: Missing or unparseable division claims must fail closed (deny access), never falling open to Central Office.

---

## 🛠️ Developer Runbook & Verification Protocol

### 1. Building the Application
Because Kestrel or IIS Express may hold a read lock on `bin/Debug/net9.0/MVCCaseManagement.dll`, verify compilation without overwriting locked binaries using `-t:Compile`:
```powershell
$env:PATH = "C:\Users\IT\AppData\Local\Microsoft\dotnet;$env:PATH"
Set-Location "Law Project\CaseManagement"
dotnet build MVCCaseManagement.csproj -t:Compile --no-restore -p:NuGetAudit=false
```

### 2. Running Unit Tests
In the Windows development sandbox, run test suites with `BypassSandbox: true` so that `vstest.console.exe` can establish local IPC socket communication:
```powershell
$env:PATH = "C:\Users\IT\AppData\Local\Microsoft\dotnet;$env:PATH"
Set-Location "Law Project\MVCCaseManagement.Tests"
dotnet test --no-restore --filter "FullyQualifiedName~NyayaPathaDynamicEngineTests"
```

### 3. Git Operations
Always use the installed MinGit executable with `--no-pager` to prevent pager hangs:
```powershell
& "C:\Users\IT\AppData\Local\Programs\MinGit\cmd\git.exe" --no-pager status -s
```

---

## 🧭 The 4 Mandatory Operating Directives Checklist

For **EVERY** future prompt, you must evaluate changes against the 4 compulsory skills:
1. 🕸️ **`graphify-8`**: Map affected communities and God Nodes (`DBHelper`, `CaseRepository`, `ECourtsController`, `LabourController`, `UnifiedLegalResearchService`).
2. 🦹 **`rules/ponytail.md`**: Climb the 7-Rung Ladder; reuse existing DAL/helpers; fix root causes in shared methods once; zero unnecessary boilerplate.
3. 🔍 **`find-security-vulnerabilities-in-code`**: Multi-tenant boundary isolation, 100% parameterization (`@param`), IDOR prevention, read-only AST safety.
4. ⚡ **`gsd-core-next`**: Follow Discuss $\to$ Plan $\to$ Execute $\to$ Verify $\to$ Ship. Verify via `dotnet build -t:Compile` and test suites before reporting completion.
