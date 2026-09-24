# 🏛️ Law Project — Enterprise Legal Case Management & e-Courts Portal

> **Enterprise Legal Management Monorepo**  
> Streamlining Motor Vehicle Claim (MVC/MACT) tracking, Labour disputes, Service Matters (High Court Writ Petitions), Payment of Gratuity claims, Execution Petitions (EP), and real-time **e-Courts Gateway Integration** across public transport and corporate divisions.

---

## 📌 Executive Overview

**Law Project** is an enterprise-grade legal management platform built with **ASP.NET Core 9.0 MVC**, **Microsoft SQL Server**, and modern **Bootstrap 5 / Razor** web views. It centralizes and automates legal claim workflows, legal opinion requests (TR-18), advocate entrustment, court fee/petty bill tracking, automated SMS hearing notifications, and real-time status synchronization with official Indian court records via the **NIC e-Courts NAPIX API Gateway**.

### Key Monorepo Components

| Component / Subdirectory | Technology | Description |
| :--- | :--- | :--- |
| 🏢 **[MVCCaseManagement](file:///c:/Users/adts-/Desktop/Law%20Project/Law%20Project/MVCCaseManagement)** | ASP.NET Core 9.0 MVC, C#, SQL Server | Primary web application containing controllers, domain models, business logic, background workers, and Razor UI views. |
| 🏛️ **[District Court Status API](file:///c:/Users/adts-/Desktop/Law%20Project/District%20Court%20Case%20Status%20API)** | JSON OpenAPI Specs | 19 official OpenAPI endpoint specifications for ICJS & District Court case lookup, Cause List, and FIR status. |
| ⚖️ **[High Court Case Status API](file:///c:/Users/adts-/Desktop/Law%20Project/High%20Court%20Case%20Status%20API)** | JSON OpenAPI Specs | 22 official OpenAPI endpoint specifications for High Court case status, bench lists, advocate search, and orders. |
| 📑 **[docs](file:///c:/Users/adts-/Desktop/Law%20Project/docs/INDEX.md)** | Master Documentation Hub | Centralized documentation hub for all 47 architecture, security, performance, SRS, and operations guides (see [INDEX.md](file:///c:/Users/adts-/Desktop/Law%20Project/docs/INDEX.md)). |
| 🛠️ **[scripts](file:///c:/Users/adts-/Desktop/Law%20Project/scripts)** | PowerShell & Bash | Repository search, validation scripts, database migration runners, and automated CI/CD checks. |

---

## ⚡ Core Features & Legal Domain Modules

### 1. 🚘 Motor Vehicle Claims (MVC / MACT)
- Complete registration lifecycle for Motor Accident Claims Tribunal cases across divisions.
- PW/RW witness records, claimant details, compensation awards, and driver/vehicle mappings.
- High Court Appeal tracking (MFA - Miscellaneous First Appeal) and Supreme Court SLP integration.
- Execution Petitions (EP) tracking for recovery & deposit compliance.
- Inter-division case transfers with audit logs.

### 2. ⚖️ Labour Court & Service Matters (Writ Petitions)
- Industrial Disputes (ID), Labour Court Applications (LCA), and Serial Applications.
- Service Matters (Direct High Court Writ Petitions - WP) and Writ Appeals (WA).
- Central Office (CO), Chief Law Officer (CLO), and Managing Director (MD) recommendation workflows.
- High Court Advocate entrustment and opinion tracking.
- PDF petition & judgment upload management.

### 3. 💰 Payment of Gratuity Act & Petty Bills
- Controlling Authority (CA) & Appellate Authority (AA) claim tracking.
- Interest calculation engine for delayed gratuity payments.
- Detailed order tracking and previous payment auditing.
- Petty bill approval workflow and advocate fee disincentive checks.

### 4. 🌐 Real-Time e-Courts Gateway Integration (NAPIX / NIC)
- **Automatic 16-Digit CNR Discovery**: Automatic resolution of CNR numbers from case type, case number, and registration year.
- **District & High Court Live Sync**: Synchronize case status, next hearing dates, bench/judge names, and court orders directly from e-Courts.
- **Cryptographic Security**: Enterprise OAuth2 bearer tokens, **AES-128-CBC** request payload encryption, and **HMAC-SHA256** parameter signing.
- **Resilient Dual-Pipeline**: Direct C# NAPIX client with automated token caching & renewal, paired with an XAMPP/PHP fallback proxy (`api.php`).

### 5. 🔔 Automated Background Notifications
- `NotificationBackgroundService` hosted background job running daily.
- SMS alerts for upcoming hearing dates (1-day, 3-day, 7-day lead times).
- Stay order expiration warnings and High Court appeal filing deadline notifications.

---

## 🏗️ Technical Architecture & Stack

```
+-----------------------------------------------------------------------------------+
|                                Client Browser                                     |
|           (Responsive Bootstrap 5 + Bilingual English/Kannada Razor Views)        |
+-----------------------------------------------------------------------------------+
                                          |
                                    HTTP / HTTPS
                                          v
+-----------------------------------------------------------------------------------+
|                             ASP.NET Core 9.0 Middleware                           |
|  - Cookie Authentication & Claims Authorization (Admin, DivisionID, CO, CLO, MD) |
|  - Session State & Brotli/Gzip Compression                                        |
|  - Physical File Provider (/uploads serving PDF judgment files)                   |
+-----------------------------------------------------------------------------------+
                                          |
                                          v
+-----------------------------------------------------------------------------------+
|                                 MVC Controllers                                   |
| AccountController | CaseController | LabourController | GratuityController           |
| ECourtsController | AppealController | ReportController | DashboardController     |
+-----------------------------------------------------------------------------------+
                                          |
                     +--------------------+--------------------+
                     |                                         |
                     v                                         v
+------------------------------------------+ +--------------------------------------+
|            Services & Utils              | |      Hosted Background Service       |
| - ECourtsNapixService (AES/HMAC NAPIX)   | | - NotificationBackgroundService      |
| - SMSService & PasswordHelper (PBKDF2)   | |   (Runs daily background SMS job)    |
+------------------------------------------+ +--------------------------------------+
                     |
                     v
+-----------------------------------------------------------------------------------+
|                       Data Access Layer (DAL / Repositories)                      |
| - DBHelper (SqlConnection Factory)                                                |
| - DBMigration (Auto Schema Bootstrapper & Dynamic Migration)                      |
| - CaseRepository / LabourRepository / GratuityRepository / MasterRepository       |
|   (Dapper & ADO.NET SqlCommand with Parameterized Queries)                        |
+-----------------------------------------------------------------------------------+
                                          |
                                          v
+-----------------------------------------------------------------------------------+
|                                 SQL Server DB                                     |
| Tables: USERS, MVC_CASES, APPEAL_DETAILS, LABOUR_CASES, LABOUR_SERVICE_MATTERS,   |
|         GRA_CASES, HIGH_COURT_ADVOCATES, DIVISION_MASTER, MACT_MASTER, etc.       |
+-----------------------------------------------------------------------------------+
```

- **Runtime Target**: `.NET 9.0` (C# 13)
- **Web Framework**: ASP.NET Core MVC with Razor engine
- **Database Engine**: Microsoft SQL Server (LocalDB / Express / Enterprise)
- **Data Access**: ADO.NET (`SqlCommand`), Dapper, and dynamic runtime schema migrator (`DBMigration.cs`)
- **API Security**: AES-128-CBC Encryption, HMAC-SHA256 Signature, OAuth2 Token Management
- **UI & Styling**: Vanilla CSS3, Bootstrap 5, FontAwesome, DataTables, and bilingual English/Kannada layouts

---

## 📚 Master Documentation Index

All detailed guides are organized within the [`docs/`](file:///c:/Law%20Project/docs) directory:

| Guide | Target Audience | Description |
| :--- | :--- | :--- |
| 🏗️ **[System Architecture Guide](file:///c:/Law%20Project/docs/SYSTEM_ARCHITECTURE.md)** | Architects & Lead Devs | In-depth tier breakdown, MVC routing, DAL migration, RBAC matrix, and database schemas. |
| 🌐 **[e-Courts NAPIX Integration Guide](file:///c:/Law%20Project/docs/ECOURTS_INTEGRATION_GUIDE.md)** | Integration Engineers | Technical spec for NAPIX/NIC e-Courts gateway, AES/HMAC cryptography, and 41 OpenAPI JSON specs. |
| 📑 **[Modules & Features Guide](file:///c:/Law%20Project/docs/MODULES_AND_FEATURES_GUIDE.md)** | Business Analysts & Users | Detailed domain walkthrough for MVC, Labour Court, Service Matters, Gratuity, and Alerts. |
| 🚀 **[Deployment & Operations Guide](file:///c:/Law%20Project/docs/DEPLOYMENT_AND_OPERATIONS_GUIDE.md)** | DevOps & SysAdmins | Prerequisites, `appsettings.json`, SQL migrations, production publish scripts, and troubleshooting. |
| 📋 **[Project Rules & Guidelines](file:///c:/Law%20Project/docs/PROJECT_RULES.md)** | All Developers | Coding standards, SQL parameterization rules, error handling, and security guidelines. |
| 💰 **[Petty Bill & Payment Audit Guide](file:///c:/Law%20Project/docs/PETTY_BILL_PREVIOUS_PAYMENTS_GUIDE.md)** | Finance & Legal | Complete procedure for tracking legal fees, previous payments, and Controlling Authority orders. |

---

## 🚀 Quick Start & Local Setup

### 1. Prerequisites
- **.NET 9.0 SDK**: [Download .NET 9.0](https://dotnet.microsoft.com/download/dotnet/9.0)
- **SQL Server**: SQL Server 2019+ or SQL Server Express / LocalDB
- **Visual Studio 2022** (v17.12+) or **VS Code** with C# Dev Kit

### 2. Configuration Setup
Navigate to `Law Project/MVCCaseManagement/` and configure `appsettings.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=MVCCaseManagementDB;Trusted_Connection=True;MultipleActiveResultSets=true;Encrypt=False"
  },
  "eCourts": {
    "ApiKey": "YOUR_NAPIX_CLIENT_ID",
    "SecretKey": "YOUR_NAPIX_CLIENT_SECRET",
    "DeptId": "clonwkrtc",
    "HmacKey": "15081947",
    "AuthKey": "tD7Ju6n0Cdf4vxUo",
    "IV": "tD7Ju6n0Cdf4vxUo",
    "GatewayUrl": "https://delhigw.napix.gov.in/nic/ecourts"
  }
}
```

### 3. Database Initialisation & Startup
The application automatically creates required SQL tables, foreign keys, constraints, and non-clustered performance indexes on startup via `DBMigration.EnsureAll()`.

Run the application using the dotnet CLI:

```bash
cd "Law Project/MVCCaseManagement"
dotnet run
```

Access the application in your browser at `http://localhost:5000` or `https://localhost:5001`.

> [!TIP]
> **Default Admin Credentials**  
> Username: `admin`  
> Password: `adminpassword`  
> *(Ensure password is changed immediately upon first login in production environments).*

---

## 🛠️ Validation Scripts & Tools

The [`scripts/`](file:///c:/Law%20Project/scripts) folder contains diagnostic and validation tools:

```powershell
# Validate all workflows, templates, and skills
.\scripts\validate-all.ps1

# Run repository search tool
.\scripts\search_repo.ps1 -Query "ECourtsNapixService"
```

---

## 📄 License & Confidentiality

This software and related documentation contain proprietary and confidential information of the corporate entity. Unauthorized copying, distribution, or disclosure is strictly prohibited.
