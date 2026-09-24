# Architecture Documentation

> Updated Codebase Analysis & Mapping — 2026-08-07

## Overview
**MVCCaseManagement** is a web-based enterprise case management system built with **ASP.NET Core 9.0 MVC**. It automates and centralizes the tracking of Motor Vehicle Claim (MVC / MACT) cases, High Court / Supreme Court Appeals, Labour Court disputes, Service Matters (Direct Writ Petitions), Gratuity claims, Execution Petitions (EP), Petty Bills, and related legal matters across various divisions of a public transport corporation (NWKRTC).

The application replaces legacy register-based workflows with server-side paginated queries, role-based cookie authentication, dynamic SQL migration bootstrappers, automated SMS notification services, and responsive Bootstrap 5 Razor views.

---

## System Architecture Diagram

```
+-----------------------------------------------------------------------------------+
|                                Client Browser                                     |
|             (Bootstrap 5 + Bilingual English/Kannada Razor Views)                 |
+-----------------------------------------------------------------------------------+
                                          |
                                    HTTP / HTTPS
                                          v
+-----------------------------------------------------------------------------------+
|                             ASP.NET Core 9.0 Middleware                           |
|  - Security Headers (CSP, X-Frame-Options, X-Content-Type-Options)                |
|  - Session Middleware                                                             |
|  - Cookie Authentication & Claims Authorization ("Admin", "DivisionID", etc.)    |
|  - Static Files & Physical File Provider (/uploads and wwwroot/uploads)           |
+-----------------------------------------------------------------------------------+
                                          |
                                          v
+-----------------------------------------------------------------------------------+
|                                 MVC Controllers                                   |
|  AccountController       | CaseController         | AppealController             |
|  DashboardController     | LabourController       | GratuityController           |
|  EPController            | LabourEPController     | MasterController             |
|  JudgementController     | AuditController        | ReportController             |
+-----------------------------------------------------------------------------------+
                                          |
                        +-----------------+-----------------+
                        |                                   |
                        v                                   v
+-----------------------------------------------+   +-------------------------------+
|             Services & Utils                  |   |    Hosted Background Service  |
| - SMSService (HttpClient API)                 |   | - NotificationBackgroundService|
| - TR18Service (Legal Opinion & Docs)          |   |   (Runs daily background jobs)|
| - PasswordHelper (PBKDF2 Hashing)             |   +-------------------------------+
+-----------------------------------------------+
                        |
                        v
+-----------------------------------------------------------------------------------+
|                         Data Access Layer (DAL / Repositories)                    |
| - DBHelper (SqlConnection Factory)                                                |
| - DBMigration (Auto Schema Setup & Dynamic Migration)                             |
| - CaseRepository / LabourRepository / AppealRepository / MasterRepository / etc.  |
|   (Dapper & ADO.NET SqlCommand with Parameterized Queries)                        |
+-----------------------------------------------------------------------------------+
                                          |
                                          v
+-----------------------------------------------------------------------------------+
|                                 SQL Server DB                                     |
| Tables: USERS, MVC_CASES, APPEAL_DETAILS, LABOUR_CASES, LABOUR_SERVICE_MATTERS,   |
|         LABOUR_ARISING_APPLICATIONS, LABOUR_EP_DETAILS, GRA_CASES,                |
|         HIGH_COURT_ADVOCATES, LABOUR_ADVOCATES, DIVISION_MASTER, MACT_MASTER, etc.|
+-----------------------------------------------------------------------------------+
```

---

## Core Components & Modules

### 1. Controllers (`/MVCCaseManagement/Controllers`)
- [AccountController.cs](file:///c:/Law%20Project/Law%20Project/MVCCaseManagement/Controllers/AccountController.cs): Handles secure login, logout, password reset, OTP verification, and session initialization (`SessionKeys.DivisionID`, `SessionKeys.UserID`).
- [CaseController.cs](file:///c:/Law%20Project/Law%20Project/MVCCaseManagement/Controllers/CaseController.cs): Manages core Motor Vehicle Claim (MVC / MACT) case registration, filtering, adverse details, PW/RW witnesses, and case transfers.
- [LabourController.cs](file:///c:/Law%20Project/Law%20Project/MVCCaseManagement/Controllers/LabourController.cs): Manages Industrial Disputes, Labour Court cases (ID, LCA, Serial Applications), Service Matters (Direct Writ Petitions), Arising Applications, Central Office / CLO / MD Action workflows, Summary cards, and PDF uploads.
- [LabourEPController.cs](file:///c:/Law%20Project/Law%20Project/MVCCaseManagement/Controllers/LabourEPController.cs): Manages Labour Execution Petitions (EP) and compliance tracking.
- [GratuityController.cs](file:///c:/Law%20Project/Law%20Project/MVCCaseManagement/Controllers/GratuityController.cs): Handles Payment of Gratuity Act claims, interest payments, controlling authority orders, and Petty Bills.
- [AppealController.cs](file:///c:/Law%20Project/Law%20Project/MVCCaseManagement/Controllers/AppealController.cs): Manages High Court (MFA) and Supreme Court (SLP) appeal records and links them to underlying MVC cases.
- [EPController.cs](file:///c:/Law%20Project/Law%20Project/MVCCaseManagement/Controllers/EPController.cs): Manages Execution Petitions for MVC awards.
- [MasterController.cs](file:///c:/Law%20Project/Law%20Project/MVCCaseManagement/Controllers/MasterController.cs): Master data management for Divisions, MACT Courts, Labour Courts, Gratuity Courts, Labour Advocates, High Court Advocates, and Gratuity Advocates.
- [ReportController.cs](file:///c:/Law%20Project/Law%20Project/MVCCaseManagement/Controllers/ReportController.cs): Generates division-wise and statewide case summary reports.
- [DashboardController.cs](file:///c:/Law%20Project/Law%20Project/MVCCaseManagement/Controllers/DashboardController.cs): Renders statistical KPIs using optimized DB-side aggregations.

### 2. Data Access Layer (`/MVCCaseManagement/DAL`)
- [DBHelper.cs](file:///c:/Law%20Project/Law%20Project/MVCCaseManagement/DAL/DBHelper.cs): Singleton utility creating and managing SQL Server connections (`SqlConnection`).
- [DBMigration.cs](file:///c:/Law%20Project/Law%20Project/MVCCaseManagement/DAL/DBMigration.cs): Runtime schema migration utility executing `EnsureAll()` on startup to apply table schemas, foreign keys, and indexes automatically.
- Repositories: Standardized interface-based data access ([LabourRepository.cs](file:///c:/Law%20Project/Law%20Project/MVCCaseManagement/DAL/LabourRepository.cs), [CaseRepository.cs](file:///c:/Law%20Project/Law%20Project/MVCCaseManagement/DAL/CaseRepository.cs), [GratuityRepository.cs](file:///c:/Law%20Project/Law%20Project/MVCCaseManagement/DAL/GratuityRepository.cs), [MasterRepository.cs](file:///c:/Law%20Project/Law%20Project/MVCCaseManagement/DAL/MasterRepository.cs), [AppealRepository.cs](file:///c:/Law%20Project/Law%20Project/MVCCaseManagement/DAL/AppealRepository.cs)) utilizing ADO.NET parameterized queries to prevent SQL injection.

### 3. Domain Models (`/MVCCaseManagement/Models`)
- [LabourCase.cs](file:///c:/Law%20Project/Law%20Project/MVCCaseManagement/Models/LabourCase.cs): Composite model for Labour Cases, Service Matters (Writ Petitions), Writ Appeals, and Supreme Court Appeals.
- [ArisingApplication.cs](file:///c:/Law%20Project/Law%20Project/MVCCaseManagement/Models/ArisingApplication.cs): Model for Applications arising from parent Labour cases.
- [MVCCaseViewModel.cs](file:///c:/Law%20Project/Law%20Project/MVCCaseManagement/Models/MVCCaseViewModel.cs) & [MVCCase.cs](file:///c:/Law%20Project/Law%20Project/MVCCaseManagement/Models/MVCCase.cs): Models for Motor Accident Claims.
- [GratuityCase.cs](file:///c:/Law%20Project/Law%20Project/MVCCaseManagement/Models/GratuityCase.cs): Models for Gratuity Act claims and payments.
- [Masters.cs](file:///c:/Law%20Project/Law%20Project/MVCCaseManagement/Models/Masters.cs): Identity models for High Court Advocates, Labour Advocates, Division, MACT, and Court masters.

---

## Key Feature Implementations & Fixes

1. **Static Upload File Serving**:
   - `Program.cs` serves static files from both `wwwroot/uploads` and `ContentRootPath/uploads` via custom `PhysicalFileProvider` middleware.
   - Standardized upload helper in controllers to save directly under `WebRootPath` (`wwwroot/uploads`).

2. **Service Matter Details & Card Mapping**:
   - Updated `MapServiceToModel` in `LabourRepository.cs` to map `LABOUR_SERVICE_MATTERS` columns into both `CO_Service_*` and primary `LabourCase` properties (`PetitionerName`, `CaseStatus`, `DisposalDate`, `DisposalResult`, `AdvocateName`, `EntrustmentNo`).
   - Summary cards in `Details.cshtml` evaluate disposal results, advocate fallbacks, and entrustment details cleanly.

3. **Service Matter Advocates Scoping**:
   - `ManageServiceMatter` fetches advocate options exclusively from `HIGH_COURT_ADVOCATES` table (managed via `http://localhost:5000/Master/HighCourtAdvocates`).
   - Lower court `GetAllAdvocates()` in `LabourRepository.cs` queries `HIGH_COURT_ADVOCATES` as configured for uniform High Court advocate access.

4. **Service Matter 404 Action Route Resolution**:
   - Updated `LabourController.cs` `Action(id)` GET method to fall back to `GetServiceMatterById` and redirect seamlessly to `ManageServiceMatter`, eliminating HTTP 404 errors.
   - Header buttons in `Details.cshtml` route Service Matter cases directly to `ManageServiceMatter`.

---

## Data Flow

1. **User Request**: User accesses web interface through an authenticated browser session.
2. **Authentication / Authorization**: Cookie authentication validates user claims (`UserID`, `RoleID`, `DivisionID`). Access to restricted division resources is enforced.
3. **Controller Execution**: Controller delegates data manipulation to specific repository interfaces.
4. **Data Access & Storage**: Repositories interact with SQL Server via parameterized queries or stored procedures.
5. **Dynamic Migration**: On application bootup, `DBMigration.EnsureAll` verifies table structures and updates database indexes and columns.
6. **Background Automation**: `NotificationBackgroundService` polls upcoming hearing schedules and dispatches alerts asynchronously.

---

## Integration Points

| Integration | Type | Purpose |
|-------------|------|---------|
| **SQL Server (LocalDB / MSSQL)** | Relational DB | Primary relational data store for cases, users, masters, audit logs |
| **SMS Gateway API** | External HTTP Service | Dispatches hearing alerts and OTP notifications via `SMSService` |
| **TR-18 Legal Portal API** | Web API / Storage | Manages legal opinion requests and document uploads |
| **Local Upload Store (`wwwroot/uploads`)** | Physical File System | Stores uploaded PDFs, petition copies, certified court copies, and judgment files |
