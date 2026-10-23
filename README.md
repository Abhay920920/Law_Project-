# 🏛️ Law Project — Enterprise Legal Case Management & e-Courts Portal

> **Enterprise Legal Management Monorepo**  
> Streamlining Motor Vehicle Claim (MVC/MACT) tracking, Labour disputes, Arising Applications, Service Matters (High Court Writs), Payment of Gratuity claims, Other Courts, Execution Petitions (EP), Nyaya Patha AI legal research, and real-time **e-Courts NAPIX Gateway v1.3** integration across public transport and corporate divisions.

---

## 📌 Executive Overview

**Law Project** is an enterprise-grade legal management platform built with **ASP.NET Core 9.0 MVC**, **Microsoft SQL Server**, and modern **Bootstrap 5 / Razor** web views. It centralizes and automates legal claim workflows, legal opinion requests, advocate entrustment, court fee/petty bill tracking, automated SMS hearing notifications, AI legal precedent research, and real-time status synchronization with official Indian court records via the **NIC e-Courts NAPIX API Gateway**.

### Key Monorepo Components

| Component / Subdirectory | Technology | Description |
| :--- | :--- | :--- |
| 🏢 **[MVCCaseManagement](Law%20Project/CaseManagement)** | ASP.NET Core 9.0 MVC, C#, SQL Server | Primary web application containing 19 MVC controllers, domain models, 34 AI services, 32 DAL repositories, background workers, and Razor UI views. |
| 🤖 **[Nyaya Patha AI](docs/NYAYA_PATHA_PRODUCTION_DEPLOYMENT_PLAN.md)** | Ollama / Qwen2.5:7b, RAG, BM25 | On-premises legal research assistant with hybrid search, factuality verification, conflict detection, and petty bill audit. |
| 🌐 **[NapixEcourtsApi](NapixEcourtsApi)** | ASP.NET Core Web API | Dedicated microservice handling AES-128 / HMAC-SHA256 e-Courts gateway communications. |
| 🏛️ **[District Court Status API](District%20Court%20Case%20Status%20API)** | JSON OpenAPI Specs | 19 official OpenAPI endpoint specifications for ICJS & District Court case lookup, Cause List, and FIR status. |
| ⚖️ **[High Court Case Status API](High%20Court%20Case%20Status%20API)** | JSON OpenAPI Specs | 22 official OpenAPI endpoint specifications for High Court case status, bench lists, advocate search, and orders. |
| 🧪 **[MVCCaseManagement.Tests](Law%20Project/MVCCaseManagement.Tests)** | xUnit, .NET 9.0 | Automated test suite comprising **284 tests across 17 test suites** ensuring 100% passing quality gates. |
| 📑 **[docs](docs/INDEX.md)** | Master Documentation Hub | Centralized documentation hub for all architecture, security, performance, SRS, database, and operations guides. |
| 🛠️ **[scripts](scripts)** | PowerShell & C# Scripts | Database schema exporters, maintenance scripts, and publish automation. |

---

## ⚡ Core Features & Legal Domain Modules

### 1. 🚘 Motor Vehicle Claims (MVC / MACT)
- Complete registration lifecycle for Motor Accident Claims Tribunal cases across divisions.
- PW/RW witness records, medical evidence, claimant details, compensation awards, and driver/vehicle mappings.
- High Court Appeal tracking (MFA - Miscellaneous First Appeal) and Supreme Court SLP integration.
- Execution Petitions (EP) tracking for recovery, attachment notices, and bank deposit compliance.
- Inter-division case transfers with immutable audit logs.

### 2. ⚖️ Labour Court, Service Matters & Arising Applications
- Industrial Disputes (ID), Labour Court Applications (LCA), and Serial Applications.
- Service Matters (Direct High Court Writ Petitions - WP) and Writ Appeals (WA).
- **Arising Applications**: Monetary computation claims (Section 33-C(2)) cross-referenced to parent labour disputes (KID/ID), with itemized installment ledgers and balance tracking.
- **Multi-Tier Legal Review**: Hierarchical scrutiny workflow (Panel Advocate $\to$ Law Officer $\to$ Dy. Chief Law Officer $\to$ Chief Law Officer $\to$ Managing Director $\to$ Divisional Controller).
- **Official A4 Legal Dossier**: Printable proforma report formatted for official department records with complete letterhead, case telemetry, and 3-tier attestation blocks.

### 3. 💰 Payment of Gratuity Act & Petty Bills
- Controlling Authority (CA) & Appellate Authority (AA) claim tracking.
- **Statutory 10% Interest Engine**: Computes statutory interest under the Payment of Gratuity Act for delayed payments automatically.
- Detailed order tracking, statutory 100% appeal deposits, and previous payment auditing.
- Petty bill approval workflow with automated deduplication and advocate fee disincentive checks.

### 4. 🏛️ Other Courts Module
- Tracking of Civil Suits (OS, RFA), Revenue Courts (Tahsildar / Assistant Commissioner / DC), Consumer Forums (DCDRC, SCDRC), and Commercial Arbitration Tribunals.

### 5. 🤖 Nyaya Patha AI Assistant (ನ್ಯಾಯ ಪಥ)
- Conversational legal research assistant operating on a strict **Search-First RAG Pipeline**.
- BM25 keyword matching + dense vector embeddings over legal precedents and corporate judgment records.
- On-premises local inference via **Ollama Qwen2.5:7b-instruct**, preserving complete data sovereignty.
- Automated duplicate petty bill detection, conflict analysis, and instant global kill-switch.

### 6. 🌐 Real-Time e-Courts Gateway Integration (NAPIX v1.3)
- **Automatic 16-Digit CNR Discovery**: Automatic resolution of CNR numbers from case type, case number, and registration year.
- **Court Hall Resolution Engine**: Decodes court hall numbers from daily cause lists and proceedings.
- **MyCases Paginated Registry**: Server-side SQL pagination with division filtering, search, and real-time status chips (`SYNCED`, `HEARING_SOON`, `DISPOSED`, `STALE`).
- **Cryptographic Security**: Enterprise OAuth2 bearer tokens, **AES-128-CBC** request payload encryption, and **HMAC-SHA256** parameter signing.
- **Hourly Quota Budgeting**: Enforces 1,000 requests/hour limit per module via `tbl_Napix_Api_Quota` to prevent IP rate-limiting.

### 7. 🔔 Automated Background Notifications
- `NotificationBackgroundService` hosted background job running daily.
- SMS alerts for upcoming hearing dates (1-day, 3-day, 7-day lead times) dispatched via Karnataka Mobile-1 Gateway.
- Stay order expiration warnings (15-day alert) and High Court limitation window alerts (90-day alert).

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
|  - CorrelationIdMiddleware (X-Correlation-ID distributed tracing)                 |
|  - Cookie Authentication & Claims Authorization (8 RBAC Roles)                    |
|  - Global AntiForgeryToken Validation on state-changing requests                  |
|  - DivisionControllerReadOnlyFilter (Divisional boundary isolation)               |
+-----------------------------------------------------------------------------------+
                                          |
                                          v
+-----------------------------------------------------------------------------------+
|                                19 MVC Controllers                                 |
| Account | Case | Labour | Gratuity | Appeal | EP | LabourEP | ECourts | NyayaPatha|
| Report | Master | Audit | ActivityLog | ApiDashboard | Judgement | Notification   |
+-----------------------------------------------------------------------------------+
                                          |
                     +--------------------+--------------------+
                     |                                         |
                     v                                         v
+------------------------------------------+ +--------------------------------------+
|            Services & AI Layer           | |      Hosted Background Services      |
| - Nyaya Patha AI Engine (34 AI services) | | - NotificationBackgroundService      |
| - Ollama LLM Provider (Qwen2.5:7b)       | |   (Daily SMS notification batch)     |
| - ECourtsNapixService (AES/HMAC NAPIX)   | | - NapixBackgroundSyncService         |
| - NapixQuotaService & SyncEngine         | |   (Hourly active case sync)          |
| - SMSService (Karnataka Mobile-1)        | +--------------------------------------+
+------------------------------------------+
                     |
                     v
+-----------------------------------------------------------------------------------+
|                       Data Access Layer (32 Repositories)                         |
| - DBHelper (SqlConnection Factory & Connection Pooling)                           |
| - DatabaseMigrationRunner & DBMigration (Zero-Downtime Dynamic Migrations)        |
| - CaseRepo | LabourRepo | GratuityRepo | AppealRepo | ECourtsRepo | MasterRepo    |
+-----------------------------------------------------------------------------------+
                                          |
                                          v
+-----------------------------------------------------------------------------------+
|                           Microsoft SQL Server Database                           |
| 83 Tables (tbl_MVC_Cases, tbl_Labour_Cases, tbl_Arising_Applications,             |
|            tbl_Gra_Cases, tbl_ECourts_Cases, tbl_Napix_Api_Quota, tbl_Users, etc.)|
+-----------------------------------------------------------------------------------+
```

- **Runtime Target**: `.NET 9.0` (C# 13)
- **Web Framework**: ASP.NET Core 9.0 MVC with Razor Engine
- **Database Engine**: Microsoft SQL Server (83 Tables, Stored Procedures, Automated Migrations)
- **AI Inference**: Local Ollama (`qwen2.5:7b-instruct-q4_K_M`)
- **Data Access**: ADO.NET (`SqlCommand`), Dapper, Parameterized Queries
- **Security**: PBKDF2 Password Hashing, Anti-Forgery Tokens, AES-128-CBC / HMAC-SHA256 Gateway Cryptography
- **Automated Tests**: **284 Passing Tests** in `MVCCaseManagement.Tests`

---

## 📚 Master Documentation Hub

All comprehensive technical guides are organized within the [`docs/`](docs/INDEX.md) directory:

| Guide | Primary Audience | Description |
| :--- | :--- | :--- |
| 🏗️ **[System Architecture Guide](docs/SYSTEM_ARCHITECTURE.md)** | Architects & Lead Devs | Detailed tier breakdown, middleware, 19 controllers, 34 AI services, 32 DAL repositories, and RBAC matrix. |
| 📋 **[Project Rules & Guidelines](docs/PROJECT_RULES.md)** | All Developers | Coding standards, C# 13 / .NET 9 guidelines, strict SQL parameterization, CSRF protection, and release rules. |
| 💾 **[Database Schema & Dictionary](docs/DATABASE_SCHEMA.md)** | DBAs & Backend Devs | Dictionary for all 83 database tables, ERD diagram, stored procedures, and migration runner. |
| ⚖️ **[Modules & Features Guide](docs/MODULES_AND_FEATURES_GUIDE.md)** | Legal Officers & Analysts | Domain walkthrough for MVC, Labour Court, Arising Applications, Gratuity, Other Courts, and MIS Reports. |
| 🌐 **[e-Courts NAPIX Integration Guide](docs/ECOURTS_INTEGRATION_GUIDE.md)** | Integration Engineers | Technical spec for NAPIX/NIC e-Courts v1.3 gateway, AES/HMAC crypto, Hall resolution, and MyCases pagination. |
| 🧠 **[Nyaya Patha AI Operations Guide](docs/NYAYA_PATHA_PRODUCTION_DEPLOYMENT_PLAN.md)** | SysAdmins & AI Engineers | Local Ollama deployment, Search-First RAG pipeline, hardware profiles, and conflict detection. |
| 💰 **[Petty Bill & Payment Audit Guide](docs/PETTY_BILL_PREVIOUS_PAYMENTS_GUIDE.md)** | Finance & Legal | Complete procedure for tracking advocate fees, certified copy expenses, and previous voucher disbursements. |
| 🚀 **[Publishing & Deployment Guide](docs/PUBLISHING_GUIDE.md)** | DevOps & SysAdmins | Prerequisites, `publish.ps1` / `publish.bat`, 284 test gates, IIS 10.0 Application Pool setup, and post-deploy checks. |
| 📑 **[Software Requirements Spec (SRS)](docs/SRS.md)** | Stakeholders & QA | Business requirements, 8 user roles, security boundaries, and statutory compensation workflows. |

---

## 🚀 Quick Start & Local Setup

### 1. Prerequisites
- **.NET 9.0 SDK**: [Download .NET 9.0](https://dotnet.microsoft.com/download/dotnet/9.0)
- **SQL Server**: SQL Server 2019+ or SQL Server Express / LocalDB
- **Ollama**: [Download Ollama for Windows](https://ollama.com/download) (Required for Nyaya Patha AI)
- **Visual Studio 2022** (v17.12+) or **VS Code** with C# Dev Kit

### 2. Configuration Setup
Navigate to `Law Project/CaseManagement/` and configure `appsettings.json`:

```json
{
  "ConnectionStrings": {
    "MVCCaseDB": "Server=localhost;Database=Admin_Law;Trusted_Connection=True;TrustServerCertificate=True;"
  },
  "eCourts": {
    "DeptId": "clonwkrtc",
    "AuthKey": "YOUR_AUTH_KEY",
    "HmacKey": "YOUR_HMAC_KEY",
    "GatewayUrl": "https://delhigw.napix.gov.in/nic/ecourts",
    "OAuthTokenUrl": "https://delhigw.napix.gov.in/nic/ecourts/oauth2/token"
  },
  "AI": {
    "Enabled": true,
    "Provider": "Ollama",
    "Model": "qwen2.5:7b",
    "BaseUrl": "http://127.0.0.1:11434"
  }
}
```

### 3. Database Initialization & Startup
The database can be initialized in two ways:

#### Option A: Direct SQL Master Script (Recommended for Fresh Setups)
Execute the unified master database script in SSMS or Azure Data Studio:
👉 **`Law Project/CaseManagement/SQL/00_Master_Full_Database_Schema_And_Seed.sql`**
This script sets up:
- All 83 tables, constraints, foreign keys, and performance indexes.
- Core seed data: 8 System Roles (`ROLE_MASTER`), Divisions (`DIVISION_MASTER`), MACT Tribunals, and System Users.

#### Option B: Automatic Startup Migration
The application automatically creates missing tables, columns, and indexes on startup via `DatabaseMigrationRunner.cs` and `DBMigration.EnsureAll()`.

Run the application:
```bash
cd "Law Project/CaseManagement"
dotnet run
```
Access the application at `http://localhost:5000` or `https://localhost:5001`.

### 4. Running Automated Tests (284 Tests)
```bash
dotnet test LawProject.sln -c Release
```

---

## 🛠️ Publishing & Production Deployment

Execute the master PowerShell deployment pipeline:
```powershell
.\publish.ps1
```
For complete step-by-step Windows Server IIS 10.0 configuration, see the [Publishing & Deployment Guide](docs/PUBLISHING_GUIDE.md).
