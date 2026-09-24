# 🚀 Law Project — Comprehensive Publishing & Deployment Guide

This guide covers building, testing, publishing, and deploying the **Law Project** (MVC Case Management Web Application & e-Courts NAPIX Gateway Microservice) into production environments.

---

## 📋 1. Solution Overview

The unified solution file `LawProject.sln` aggregates all components:
- **`MVCCaseManagement`** (ASP.NET Core 9.0 Web MVC): Main case portal, hearing schedules, and reports.
- **`MVCCaseManagement.Tests`** (xUnit, .NET 9.0): Automated test suite (36 tests).
- **`NapixEcourtsApi`** (ASP.NET Core Web API): Microservice handling AES-128 / HMAC-SHA256 e-Courts gateway communications.

---

## 🛠️ 2. How to Build & Publish

### Option A: One-Click Master Script (Recommended)
Run the root batch script or PowerShell script:

```cmd
# Double click or run from terminal:
publish.bat
```

Or using PowerShell (supports custom output directories and self-contained builds):

```powershell
# Standard Framework-Dependent publish (.NET 9 installed on server)
.\publish.ps1

# Self-Contained publish (win-x64, runs without .NET 9 runtime installed)
.\publish.ps1 -SelfContained
```

### Option B: Manual CLI Commands

```powershell
# 1. Build and verify all projects
dotnet build LawProject.sln -c Release

# 2. Run unit tests
dotnet test LawProject.sln -c Release --no-build

# 3. Publish Web Application
dotnet publish "Law Project\MVCCaseManagement\MVCCaseManagement.csproj" -c Release -o "Publish_Output\MVCCaseManagement"

# 4. Publish eCourts Microservice
dotnet publish "NapixEcourtsApi\NapixEcourtsApi.csproj" -c Release -o "Publish_Output\NapixEcourtsApi"
```

---

## 📦 3. Published Output Directory Structure

After publishing, `Publish_Output/` contains:

```
Publish_Output/
├── MVCCaseManagement/            <-- Web Application for IIS / Kestrel
│   ├── MVCCaseManagement.dll
│   ├── MVCCaseManagement.exe
│   ├── web.config                <-- Hardened IIS config (50MB upload limit)
│   ├── appsettings.json
│   ├── appsettings.Production.json
│   ├── wwwroot/                  <-- CSS, JavaScript, icons
│   │   └── uploads/              <-- Uploaded case PDFs & petitions
│   ├── uploads/
│   └── logs/                     <-- Application & stdout log destination
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
2. Ensure IIS features are enabled:
   - Web Server (IIS) > Web Management Tools > IIS Management Console
   - Web Server (IIS) > World Wide Web Services > Application Development Features > **.NET Extensibility 4.8 / ASP.NET 4.8**
   - Web Server (IIS) > Common HTTP Features > **Static Content**

### Step 2: Create IIS Application Pool
1. Open **IIS Manager** (`inetmgr.exe`).
2. Right-click **Application Pools** > **Add Application Pool**:
   - **Name**: `LawProjectAppPool`
   - **.NET CLR Version**: `No Managed Code` (since ASP.NET Core runs out-of-process/in-process via ANCM)
   - **Managed Pipeline Mode**: `Integrated`
3. Click **OK**, then right-click `LawProjectAppPool` > **Advanced Settings**:
   - **Start Mode**: `AlwaysRunning` *(Keeps the automated daily SMS background notification service alive 24/7)*
   - **Idle Time-out (minutes)**: `0` *(Prevents IIS from idling out background threads)*

### Step 3: Create IIS Website
1. Right-click **Sites** > **Add Website**:
   - **Site Name**: `LawCaseManagement`
   - **Application Pool**: `LawProjectAppPool`
   - **Physical Path**: `C:\inetpub\wwwroot\LawCaseManagement` (copy contents of `Publish_Output\MVCCaseManagement` here)
   - **Binding**: Port `80` (or `443` with SSL Certificate)

### Step 4: Configure Folder Permissions
The application requires write access to store court petitions, order PDFs, and logs:
1. In Windows Explorer, right-click the deployed folder > **Properties** > **Security** tab.
2. Click **Edit...** > **Add...**:
   - Add `IIS_IUSRS` and assign **Read**, **Write**, and **Modify** permissions.
   - Add `IIS AppPool\LawProjectAppPool` and assign **Read**, **Write**, and **Modify** permissions.
3. Specifically ensure write access for:
   - `\uploads`
   - `\wwwroot\uploads`
   - `\logs`

---

## 🔒 5. Production Configuration & Security Hardening

### 5.1 Environment Variables
Avoid hardcoding database passwords or API keys in configuration files on disk. Set environment variables on the production server (System Properties > Environment Variables or in IIS Application Pool settings):

| Variable Name | Description |
| :--- | :--- |
| `ConnectionStrings__MVCCaseDB` | Production SQL Server connection string |
| `SMS__Password` | SMS Gateway API password |
| `SMS__SecureKey` | SMS Gateway secure key |
| `eCourts__ApiKey` | NAPIX Client ID |
| `eCourts__SecretKey` | NAPIX Secret Key |
| `ASPNETCORE_ENVIRONMENT` | Set to `Production` |

### 5.2 Upload Limit Configuration
The bundled `web.config` is pre-configured with:
```xml
<security>
  <requestFiltering>
    <!-- 50 MB limit for legal petition & judgment scans -->
    <requestLimits maxAllowedContentLength="52428800" />
  </requestFiltering>
</security>
```

---

## 🗃️ 6. Git Version Control & GitHub Publishing

### Repository Setup
To initialize and push the Law Project to a clean GitHub or remote Git repository:

```bash
# 1. Ensure you are in the project root
cd "c:\Users\adts-\Desktop\Law Project"

# 2. Initialize git cleanly in this project directory (if not already initialized)
git init

# 3. Add remote pointing to your dedicated Law Project repository
# Replace with your actual GitHub repo URL:
git remote add origin https://github.com/YOUR_ORGANIZATION_OR_USERNAME/law-project.git

# 4. Check status (all binaries, sensitive files, and publish outputs are ignored)
git status

# 5. Commit and push
git add .
git commit -m "feat: complete enterprise law case management and ecourts platform"
git branch -M main
git push -u origin main
```
