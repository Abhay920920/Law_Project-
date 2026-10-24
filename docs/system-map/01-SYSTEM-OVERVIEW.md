# 🏛️ NWKRTC Law Management System — System Architecture Overview

**Document Reference**: High-Level Architecture & Component Topology  
**Target Solution**: `LawProject.sln` (.NET 9.0)  
**Verification Level**: **VERIFIED IN SOURCE**  

---

## 1. System Topology & Technology Stack

The North Western Karnataka Road Transport Corporation (NWKRTC) Law Management System is an enterprise-scale legal operations platform designed to digitize, monitor, and audit legal dispute workflows across all corporate operating divisions (Belagavi, Hubballi-Dharwad, Gadag, Bagalkot, Vijayapura, Uttara Kannada, Haveri, and Central Office).

```
                      +------------------------------------------+
                      |         Web Browser Clients              |
                      |   (Legal Officers, Section Officers,     |
                      |    LO, Dy CLO, CLO, MD, Administrators)   |
                      +--------------------+---------------------+
                                           | HTTPS / TLS 1.2+
                                           v
                      +------------------------------------------+
                      |      IIS 10.0 Web Application Pool       |
                      |         (NWKRTC_Law_AppPool)             |
                      +--------------------+---------------------+
                                           |
                +--------------------------+--------------------------+
                |                                                     |
                v                                                     v
+-------------------------------+             +-------------------------------+
|  MVCCaseManagement (ASP.NET)  |             |  NapixEcourtsApi (Microservice)|
|  - 19 Controllers, 338 Routes |             |  - AES-128-CBC + HMAC-SHA256  |
|  - 143 Razor Views (.cshtml)  |             |  - Direct NIC NAPIX v1.3 Pipe |
|  - Cookie Auth & Claims       |             +---------------+---------------+
|  - Multi-Tenant Division RBAC |                             |
+---------------+---------------+                             |
                |                                             |
                | ADO.NET Parameterized Queries / Dapper     | Government TLS
                v                                             v
+-------------------------------+             +-------------------------------+
|  Microsoft SQL Server 2019+   |             |  NIC e-Courts NAPIX Gateway   |
|  - Database: Admin_Law        |             |  - High Court / District Data |
|  - 82 Unique Tables           |             +-------------------------------+
|  - Stored Procedures & Triggers|
+-------------------------------+
                |
                +---------------------------------------------+
                |                                             |
                v                                             v
+-------------------------------+             +-------------------------------+
|  Local Ollama AI Subsystem    |             |  Karnataka Mobile-1 SMS       |
|  - Daemon: localhost:11434    |             |  - Bulk Hearing Alerts        |
|  - Model: Qwen2.5:7b-instruct |             |  - Password Reset OTP Delivery|
+-------------------------------+             +-------------------------------+
```

---

## 2. Core Projects in Unified Solution (`LawProject.sln`)

1. **`MVCCaseManagement`** (`Law Project\CaseManagement\MVCCaseManagement.csproj`):
   - **Target Framework**: .NET 9.0 (`net9.0`)
   - **Output Type**: Executable Web Application (`Microsoft.NET.Sdk.Web`)
   - **Core Packages**: `Microsoft.AspNetCore.Mvc.Razor.RuntimeCompilation`, `Microsoft.Data.SqlClient`, `Dapper`, `System.Security.Cryptography`, `System.Text.Json`.
   - **Role**: Serves the primary web user interface, legal domain workflows, case management, document streaming, report generation, and AI legal assistant.

2. **`MVCCaseManagement.Tests`** (`Law Project\MVCCaseManagement.Tests\MVCCaseManagement.Tests.csproj`):
   - **Target Framework**: .NET 9.0 (`net9.0`)
   - **Test Framework**: xUnit (`xunit` v2.8.2, `xunit.runner.visualstudio` v2.8.2)
   - **Test Inventory**: 18 test classes containing **289 passing unit and integration tests**.
   - **Role**: Validates legal formulas, anti-CSRF token enforcement, SSRF defenses, SQL injection prevention, and RAG evaluation.

3. **`NapixEcourtsApi`** (`NapixEcourtsApi\NapixEcourtsApi.csproj`):
   - **Target Framework**: .NET 8.0 (`net8.0`)
   - **Role**: Specialized microservice library implementing the National Informatics Centre (NIC) e-Courts NAPIX API specification v1.3.

---

## 3. Background Services & Worker Processes

The system hosts two critical continuous background services as `IHostedService` threads registered in `Program.cs`:

1. **`NotificationBackgroundService`**:
   - **Schedule**: Evaluates daily hearing schedules every morning at 07:00 IST.
   - **Logic**: Queries `MVC_CASE_HEARING_DETAILS` and `LABOUR_HEARINGS` for hearings occurring within the next 48 hours.
   - **Dispatch**: Formats personalized SMS notifications and sends them via the Karnataka Mobile-1 SMS gateway.

2. **`NapixBackgroundSyncService`**:
   - **Schedule**: Executes continuous queue processing and hourly automated sync cycles.
   - **Logic**: Dequeues pending case synchronization jobs from `NAPIX_SYNC_QUEUE`, enforces rate limits (max 20 calls/min, max 1000 calls/hr), and queries the NIC e-Courts gateway.
   - **Circuit Breaking**: Uses `CircuitBreaker.cs` to prevent resource exhaustion when the external government gateway is unavailable.
