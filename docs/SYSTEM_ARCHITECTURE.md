# 🏗️ Law Project — System Architecture & Technical Design Specification

> **Target Version**: ASP.NET Core 9.0 MVC (net9.0)  
> **Database Engine**: Microsoft SQL Server  
> **Documentation Scope**: End-to-end architectural layers, data access patterns, security, database schemas, background services, and external API gateways.

---

## 📌 1. Architectural Overview

The **Law Case Management System** (`MVCCaseManagement`) is designed as a high-performance, modular **Layered Enterprise Monolith**. It separates concerns across clear functional layers:

1. **Presentation Layer**: Bootstrap 5 + Bilingual English/Kannada Razor Views (`.cshtml`), enhanced with server-side rendered forms, async JavaScript API callables, DataTables, and responsive cards.
2. **Controller & Middleware Layer**: ASP.NET Core 9.0 MVC Controllers, Session State Management, Cookie Authentication, Claims-Based Authorization, and Physical File Provider Middleware.
3. **Services & Utility Layer**: External e-Courts NAPIX Encryption/Decryption (`ECourtsNapixService`), SMS Notification Gateway (`SMSService`), Legal Opinion Request Handler (`TR18Service`), and Password Hashing (`PasswordHelper`).
4. **Hosted Background Services**: `NotificationBackgroundService` (daily cron background worker for automated hearing reminders and deadline alerts).
5. **Data Access Layer (DAL)**: `DBHelper` SQL Connection Factory, `DBMigration` dynamic schema bootstrapper, and repository pattern implementations using **Dapper** & parameterized **ADO.NET** (`SqlCommand`).
6. **Database Layer**: Microsoft SQL Server database storing legal records, audit logs, advocate masters, and indexed case relationships.

---

## 🏛️ 2. Comprehensive System Architecture Diagram

```
+-----------------------------------------------------------------------------------+
|                                Client Browser                                     |
|           (Bootstrap 5 + Bilingual English/Kannada Razor UI Views)                |
+-----------------------------------------------------------------------------------+
                                          |
                                    HTTP / HTTPS
                                          v
+-----------------------------------------------------------------------------------+
|                             ASP.NET Core 9.0 Middleware                           |
|  - Security Headers (CSP, X-Frame-Options, X-Content-Type-Options)                |
|  - Session State Middleware (SessionKeys: DivisionID, UserID, RoleID)             |
|  - Cookie Authentication & Claims Authorization                                   |
|  - Physical File Provider Middleware (/uploads and wwwroot/uploads)               |
|  - HTTP Response Compression (Brotli & Gzip)                                      |
+-----------------------------------------------------------------------------------+
                                          |
                                          v
+-----------------------------------------------------------------------------------+
|                                 MVC Controllers                                   |
|  - AccountController        - CaseController           - AppealController         |
|  - LabourController         - LabourEPController       - GratuityController       |
|  - ECourtsController        - MasterController         - ReportController         |
|  - DashboardController      - EPController             - AuditController          |
+-----------------------------------------------------------------------------------+
                                          |
                     +--------------------+--------------------+
                     |                                         |
                     v                                         v
+------------------------------------------+ +--------------------------------------+
|             Services & Utils             | |      Hosted Background Service       |
| - ECourtsNapixService (AES/HMAC API)     | | - NotificationBackgroundService      |
| - SMSService (HttpClient Gateway)        | |   (Daily cron for hearing alerts &   |
| - TR18Service (Legal Opinion Workflow)   | |    stay expiration notifications)    |
| - PasswordHelper (PBKDF2 Hashing)        | +--------------------------------------+
+------------------------------------------+
                     |
                     v
+-----------------------------------------------------------------------------------+
|                       Data Access Layer (DAL / Repositories)                      |
| - DBHelper (SqlConnection Factory & Lifecycle)                                    |
| - DBMigration (Runtime Auto Schema Setup & Dynamic Migration Bootstrapper)        |
| - CaseRepository    - LabourRepository     - GratuityRepository                   |
| - MasterRepository  - AppealRepository     - ECourtsRepository                    |
|   (Dapper & ADO.NET SqlCommand with Parameterized Queries)                        |
+-----------------------------------------------------------------------------------+
                                          |
                                          v
+-----------------------------------------------------------------------------------+
|                                 SQL Server Database                               |
| Core Tables: USERS, MVC_CASES, APPEAL_DETAILS, LABOUR_CASES, LABOUR_SERVICE_MATTERS,|
|              LABOUR_ARISING_APPLICATIONS, LABOUR_EP_DETAILS, GRA_CASES,           |
|              HIGH_COURT_ADVOCATES, LABOUR_ADVOCATES, DIVISION_MASTER, MACT_MASTER,  |
|              ECOURTS_SYNC_LOG, PETTY_BILLS, etc.                                  |
+-----------------------------------------------------------------------------------+
```

---

## 🔐 3. Authentication, Authorization & Security Matrix

### 3.1 Security Controls & Session Management
- **Authentication Protocol**: Cookie Authentication (`CookieAuthenticationDefaults.AuthenticationScheme`).
- **Session Keys**:
  - `SessionKeys.UserID`: Logged in user identifier.
  - `SessionKeys.UserName`: Display name.
  - `SessionKeys.RoleID`: Numeric role identifier.
  - `SessionKeys.DivisionID`: Division scope filter (`0` or `100` for Statewide/Central Office, specific division ID for branch users).
- **Password Security**: Salted **PBKDF2** password hashing via standard `Rfc2898DeriveBytes`.
- **Static Asset Security**: File uploads (`/uploads`) are strictly served through ASP.NET Core `PhysicalFileProvider` with content-type verification and security headers.

### 3.2 Role-Based Access Control (RBAC) Matrix

| User Role | Division Scope | View Records | Add / Edit Case | Entrust Advocate | CO / CLO / MD Action | Admin Masters |
| :--- | :---: | :---: | :---: | :---: | :---: | :---: |
| **System Administrator** | All Divisions | ✅ Full | ✅ Full | ✅ Full | ✅ Full | ✅ Full |
| **Central Law Office (CLO)** | Statewide (100) | ✅ Full | ✅ Full | ✅ High Court | ✅ CLO Approvals | ❌ Read Only |
| **Managing Director (MD)** | Statewide (100) | ✅ Full | 👁️ View Only | ❌ No | ✅ Final Approvals | ❌ Read Only |
| **Central Office Staff** | Statewide (100) | ✅ Full | ✅ Full | ✅ Entrustment | ✅ Recommendations | ❌ Read Only |
| **Division User** | Own Division | 🔒 Scoped | 🔒 Own Division | 🔒 Division Advocates | ❌ No | ❌ No |

---

## ⚙️ 4. Data Access Layer & Dynamic Database Bootstrapper

### 4.1 Data Access Strategy
The application explicitly avoids heavyweight ORM overhead by employing a lightweight hybrid data layer:
- **Dapper**: High-speed mapping of SQL queries directly to domain POCOs (`MVCCase`, `LabourCase`, `GratuityCase`).
- **ADO.NET (`SqlCommand` & `SqlDataReader`)**: Precise stream processing for complex reports and multi-result stored procedures.
- **Strict Parameterization**: All SQL queries execute with explicit `SqlParameter` or Dapper parameters to guarantee immunity against SQL injection vulnerabilities.

### 4.2 Dynamic Database Schema Migration (`DBMigration.cs`)
On application startup, `Program.cs` triggers `DBMigration.EnsureAll(connectionString)`:
1. **Schema Check**: Validates the presence of core tables and automatically issues `CREATE TABLE` scripts if tables do not exist.
2. **Column Migration**: Scans existing tables for missing columns (e.g. `CNRNumber`, `CO_Service_*`, `IsDisincentiveApplied`) and executes `ALTER TABLE ADD` dynamically.
3. **Index Optimization**: Automatically creates non-clustered performance indexes on high-cardinality search columns:
   - `IX_MVC_CASES_Division_Status` on `MVC_CASES(DivisionID, CaseStatus)`
   - `IX_MVC_CASES_CNR` on `MVC_CASES(CNRNumber)`
   - `IX_LABOUR_CASES_Division` on `LABOUR_CASES(DivisionID)`
   - `IX_GRA_CASES_Division` on `GRA_CASES(DivisionID)`

---

## 💾 5. Database Schema Architecture

### 5.1 Core Domain Tables

#### 1. `MVC_CASES`
Stores Motor Accident Claim Tribunal records.
Key Columns: `CaseID`, `DivisionID`, `MactID`, `ClaimPetitionerName`, `AccidentDate`, `VehicleNo`, `DriverName`, `ClaimAmount`, `AwardAmount`, `CNRNumber`, `CaseStatus`, `AdvocateID`.

#### 2. `APPEAL_DETAILS`
Stores High Court (MFA) and Supreme Court (SLP) appeals linked to underlying MVC cases.
Key Columns: `AppealID`, `CaseID`, `AppealNo`, `CourtType` (High Court / Supreme Court), `FilingDate`, `Appellant`, `AdvocateID`, `StayGranted` (Bit), `StayExpiryDate`, `DisposalDate`.

#### 3. `LABOUR_CASES`
Stores Industrial Disputes (ID), Labour Court Applications (LCA), and Serial Applications.
Key Columns: `LabourCaseID`, `DivisionID`, `CourtID`, `CaseNo`, `ApplicantName`, `DisputeType`, `EntrustmentNo`, `CaseStatus`, `CLO_Recommendation`, `MD_Approval_Status`.

#### 4. `LABOUR_SERVICE_MATTERS`
Stores Direct High Court Writ Petitions (WP) and Writ Appeals (WA).
Key Columns: `ServiceID`, `DivisionID`, `WritPetitionNo`, `PetitionerName`, `RespondentDetails`, `HighCourtBenchID`, `AdvocateID`, `InterimOrderDetails`, `CO_Service_Action`, `DisposalResult`.

#### 5. `GRA_CASES`
Stores Payment of Gratuity Act claims.
Key Columns: `GratuityID`, `DivisionID`, `ControllingAuthorityID`, `EmployeeName`, `Designation`, `ClaimedAmount`, `DeterminedAmount`, `InterestRate`, `InterestAmount`, `PaymentStatus`.

#### 6. `HIGH_COURT_ADVOCATES` & `LABOUR_ADVOCATES`
Master directories of legal counsel.
Key Columns: `AdvocateID`, `AdvocateName`, `BarRegistrationNo`, `PhoneNo`, `Email`, `Specialization`, `IsActive`.

---

## 🌐 6. e-Courts NAPIX Integration Architecture

The e-Courts gateway connects the application directly to the National Informatics Centre (NIC) e-Courts NAPIX API infrastructure.

```
+-------------------+             +-----------------------+             +-----------------------+
| MVCCaseManagement |             |  Local PHP Proxy      |             | NIC e-Courts NAPIX    |
| (ECourtsController|             |  (http://localhost/   |             | Gateway               |
|  & C# Service)    |             |   ecourts/api.php)    |             | (delhigw.napix.gov.in)|
+-------------------+             +-----------------------+             +-----------------------+
          |                                   |                                     |
          |--- 1. Query live status --------->|                                     |
          |    (Case No / CNR)                |--- 2. OAuth2 Client Auth ---------->|
          |                                   |    (Client ID + Client Secret)      |
          |                                   |<-- 3. Access Token -----------------|
          |                                   |                                     |
          |                                   |--- 4. AES-128-CBC Encrypted -------->|
          |                                   |    Request + HMAC SHA256            |
          |                                   |<-- 5. Encrypted Response -----------|
          |<-- 6. Decrypted JSON -------------|                                     |
```

For complete technical specifications, cryptography details, and OpenAPI specs, see the **[e-Courts NAPIX Integration Guide](file:///c:/Law%20Project/docs/ECOURTS_INTEGRATION_GUIDE.md)**.

---

## 🔔 7. Background Services & Notification Engine

### `NotificationBackgroundService` Architecture
- **Service Type**: ASP.NET Core `IHostedService` / `BackgroundService`.
- **Execution Interval**: Daily execution (runs every 24 hours at designated low-traffic hours).
- **Core Jobs**:
  1. **Hearing Reminders**: Queries `MVC_CASES`, `LABOUR_CASES`, and `APPEAL_DETAILS` for hearings scheduled within 1, 3, and 7 days. Dispatches SMS notifications to assigned Advocates and Division Legal Officers via `SMSService`.
  2. **Stay Expiration Alerts**: Checks High Court stay orders where `StayExpiryDate` is approaching within 15 days.
  3. **Appeal Limitation Alerts**: Monitors judgment disposal dates to warn division users before the statutory 90-day High Court appeal window lapses.

---

## 📊 8. Dashboard & Reporting Engine

- **High-Performance Aggregations**: Dashboard metrics (Total Pending, Award Amounts, Disposal Rates) are calculated directly within SQL Server using optimized `COUNT()` and `SUM()` aggregates.
- **Statewide vs. Division View**: Division users see scoped metrics, while Central Office, CLO, and MD users see statewide consolidated KPIs.
- **Export Formats**: Report views support direct printing, PDF generation, and Excel/CSV data exports via client-side DataTables plugins.
