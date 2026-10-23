# 🚀 Law Project — Comprehensive Publishing & Deployment Guide

> **Target Environment**: Windows Server 2019 / 2022 with Internet Information Services (IIS 10.0)  
> **Framework Target**: ASP.NET Core 9.0 (.NET 9.0.x Hosting Bundle)  
> **Master Solution**: [`LawProject.sln`](file:///c:/Users/adts-/Desktop/Law%20Project/LawProject.sln)  
> **Quality Gate**: 284/284 Automated Tests Passing Across 17 Test Suites

---

## 📋 1. Solution Components Overview

The unified solution file `LawProject.sln` aggregates all system components:
- **`MVCCaseManagement`** (ASP.NET Core 9.0 Web MVC): Main case portal, hearing schedules, Nyaya Patha AI assistant, and reports.
- **`MVCCaseManagement.Tests`** (xUnit, .NET 9.0): Automated test suite containing **284 tests** covering crypto, RAG AI, interest formulas, and API security.
- **`NapixEcourtsApi`** (ASP.NET Core Web API): Microservice handling AES-128 / HMAC-SHA256 e-Courts gateway communications.

---

## 🛠️ 2. Build & Publishing Pipelines

### Option A: One-Click Master PowerShell Pipeline (Recommended)
Execute [`publish.ps1`](file:///c:/Users/adts-/Desktop/Law%20Project/publish.ps1) from an administrative terminal:

```powershell
# Standard Framework-Dependent publish (.NET 9 Hosting Bundle installed on server)
.\publish.ps1

# Self-Contained publish (win-x64, bundles .NET 9 runtime inside the binaries)
.\publish.ps1 -SelfContained

# Custom Destination directory
.\publish.ps1 -OutputDir "C:\inetpub\wwwroot\LawProject"
```

The script automatically:
1. Validates prerequisites (.NET 9 SDK, git state).
2. Cleans previous compilation artifacts.
3. Compiles `LawProject.sln` under `Release` configuration.
4. Executes all 284 unit tests via `dotnet test` (aborts if any test fails).
5. Compiles and publishes `MVCCaseManagement` to `Publish_Output/MVCCaseManagement`.
6. Compiles and publishes `NapixEcourtsApi` to `Publish_Output/NapixEcourtsApi`.
7. Synchronizes Razor views and static assets.
8. Ensures required directories (`uploads/`, `logs/`) exist.

### Option B: Windows Command Prompt (`publish.bat`)
Double-click or run from CMD:
```cmd
publish.bat
```

### Option C: Manual CLI Step-by-Step

```powershell
# 1. Restore & Build Release Configuration
dotnet build LawProject.sln -c Release

# 2. Run All Unit & Integration Tests (284 tests)
dotnet test LawProject.sln -c Release --no-build

# 3. Publish Main Web Application
dotnet publish "Law Project\CaseManagement\MVCCaseManagement.csproj" -c Release -o "Publish_Output\MVCCaseManagement"

# 4. Publish eCourts NAPIX Microservice
dotnet publish "NapixEcourtsApi\NapixEcourtsApi.csproj" -c Release -o "Publish_Output\NapixEcourtsApi"
```

---

## 📦 3. Published Output Directory Structure

After execution, `Publish_Output/` contains:

```
Publish_Output/
├── MVCCaseManagement/            <-- Web Application for IIS / Kestrel
│   ├── MVCCaseManagement.dll
│   ├── MVCCaseManagement.exe
│   ├── web.config                <-- Hardened IIS config (50MB upload limit, out-of-process)
│   ├── appsettings.json          <-- Base configuration
│   ├── appsettings.Production.json
│   ├── wwwroot/                  <-- Bundled CSS, JavaScript, icons
│   │   └── uploads/              <-- Uploaded case PDFs & petitions
│   ├── uploads/                  <-- Authenticated document vault
│   └── logs/                     <-- Stdout & application log destination
└── NapixEcourtsApi/              <-- e-Courts Microservice
    ├── NapixEcourtsApi.dll
    ├── NapixEcourtsApi.exe
    ├── appsettings.json
    └── logs/
```

---

## 🌐 4. Deploying to Windows IIS (Internet Information Services)

### Step 1: Install Server Prerequisites
1. Install **[.NET 9.0 Hosting Bundle for Windows](https://dotnet.microsoft.com/download/dotnet/9.0)**.
2. Verify IIS features are enabled:
   - Web Server (IIS) > Web Management Tools > IIS Management Console
   - Web Server (IIS) > World Wide Web Services > Application Development Features > **.NET Extensibility 4.8 / ASP.NET 4.8**
   - Web Server (IIS) > Common HTTP Features > **Static Content**

### Step 2: Configure IIS Application Pool
1. Open **IIS Manager** (`inetmgr.exe`).
2. Right-click **Application Pools** > **Add Application Pool**:
   - **Name**: `LawProjectAppPool`
   - **.NET CLR Version**: `No Managed Code` (since ASP.NET Core runs in-process or out-of-process via ANCM)
   - **Managed Pipeline Mode**: `Integrated`
3. Right-click `LawProjectAppPool` > **Advanced Settings**:
   - **Start Mode**: `AlwaysRunning` *(Crucial: Keeps the automated daily SMS background notification service alive 24/7)*
   - **Idle Time-out (minutes)**: `0` *(Prevents IIS from idling out background sync threads)*
   - **Recycling > Regular Time Interval (minutes)**: `0` (or configure off-peak maintenance window at 03:00 AM)

### Step 3: Create IIS Website
1. Right-click **Sites** > **Add Website**:
   - **Site Name**: `LawCaseManagement`
   - **Application Pool**: `LawProjectAppPool`
   - **Physical Path**: `C:\inetpub\wwwroot\LawCaseManagement` (or your destination folder)
   - **Binding**: HTTPS (Port 443 with TLS certificate) or HTTP (Port 80/5000)

### Step 4: Configure Folder Permissions
The IIS Application Pool identity requires write access to the `uploads/` and `logs/` folders:
1. Right-click `Publish_Output\MVCCaseManagement\uploads` > **Properties** > **Security** > **Edit**.
2. Click **Add**, type `IIS AppPool\LawProjectAppPool`, click **Check Names**, and grant **Modify** / **Write** permissions.
3. Repeat for the `logs/` directory.

---

## 🛡️ 5. Hardened `web.config` Configuration

The published `web.config` enforces request size caps, prevents information disclosure, and configures the ASP.NET Core Module (ANCM):

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <system.webServer>
    <handlers>
      <add name="aspNetCore" path="*" verb="*" modules="AspNetCoreModuleV2" resourceType="Unspecified" />
    </handlers>
    <aspNetCore processPath="dotnet" arguments=".\MVCCaseManagement.dll" stdoutLogEnabled="true" stdoutLogFile=".\logs\stdout" hostingModel="inprocess">
      <environmentVariables>
        <environmentVariable name="ASPNETCORE_ENVIRONMENT" value="Production" />
      </environmentVariables>
    </aspNetCore>
    <security>
      <requestFiltering>
        <!-- 52,428,800 bytes = 50 MB max petition/judgment PDF upload -->
        <requestLimits maxAllowedContentLength="52428800" />
      </requestFiltering>
    </security>
    <httpProtocol>
      <customHeaders>
        <remove name="X-Powered-By" />
        <add name="X-Frame-Options" value="SAMEORIGIN" />
        <add name="X-Content-Type-Options" value="nosniff" />
        <add name="Referrer-Policy" value="strict-origin-when-cross-origin" />
      </customHeaders>
    </httpProtocol>
  </system.webServer>
</configuration>
```

---

## 🔍 6. Post-Deployment Verification Checklist

1. **HTTP Status Check**: Navigate to `https://your-domain.in/Home` and verify HTTP 200 response.
2. **Database Auto-Migration**: Review `logs/stdout` to confirm `DBMigration.EnsureAll()` executed successfully.
3. **eCourts NAPIX Test**: Navigate to `/ECourts/MyCases` and perform a test status refresh.
4. **SMS Service Health**: Verify `NotificationBackgroundService` logged its initial heartbeat in the logs.
5. **Nyaya Patha AI**: Navigate to `/NyayaPatha` and test a sample query (ensure Ollama service is active on `127.0.0.1:11434`).
