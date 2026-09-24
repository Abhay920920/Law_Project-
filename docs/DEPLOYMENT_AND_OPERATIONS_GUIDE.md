# 🚀 Law Project — Deployment & Operations Guide

> **Deployment Target**: Windows Server / IIS 10+ or Kestrel Web Server  
> **Database Engine**: Microsoft SQL Server 2019 / 2022 / Azure SQL / Express  
> **Application Framework**: ASP.NET Core 9.0 (net9.0)

---

## 📌 1. Server Environment Prerequisites

### 1.1 Hardware Requirements
- **CPU**: 4 Cores minimum (8 Cores recommended for high-concurrency production).
- **RAM**: 8 GB minimum (16 GB recommended).
- **Disk Storage**: 50 GB SSD storage for application binaries, SQL database, and uploaded legal PDF files.

### 1.2 Software Prerequisites
- **Operating System**: Windows Server 2019/2022 or Windows 10/11 Professional (64-bit).
- **.NET Hosting Bundle**: [.NET 9.0 Hosting Bundle for Windows IIS](https://dotnet.microsoft.com/download/dotnet/9.0).
- **Database Engine**: Microsoft SQL Server 2019+ or SQL Server Express with Management Studio (SSMS).
- **Web Server**: Internet Information Services (IIS 10.0+) with WebSockets and Static Content modules enabled.

---

## ⚙️ 2. Application Configuration (`appsettings.json`)

Configure production settings in `Law Project/MVCCaseManagement/appsettings.json` or `appsettings.Production.json`:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "MVCCaseManagement.Utils.ECourtsNapixService": "Information"
    }
  },
  "AllowedHosts": "*",
  "ConnectionStrings": {
    "DefaultConnection": "Server=YOUR_SQL_SERVER;Database=MVCCaseManagementDB;User Id=sa;Password=YOUR_STRONG_PASSWORD;TrustServerCertificate=True;Encrypt=True;MultipleActiveResultSets=true;"
  },
  "eCourts": {
    "ApiKey": "YOUR_NAPIX_CLIENT_ID",
    "SecretKey": "YOUR_NAPIX_CLIENT_SECRET",
    "DeptId": "clonwkrtc",
    "HmacKey": "15081947",
    "AuthKey": "tD7Ju6n0Cdf4vxUo",
    "IV": "tD7Ju6n0Cdf4vxUo",
    "Version": "v1.0",
    "GatewayUrl": "https://delhigw.napix.gov.in/nic/ecourts",
    "OAuthTokenUrl": "https://delhigw.napix.gov.in/nic/ecourts/oauth2/token"
  },
  "SMS": {
    "ApiUrl": "https://api.smsgateway.com/send",
    "ApiKey": "YOUR_SMS_API_KEY",
    "SenderId": "LAWPRJ"
  }
}
```

---

## 🗄️ 3. Database Initialization & Schema Migration

The system features an automated **Runtime Schema Migration Bootstrapper** (`DBMigration.cs`).

### 3.1 Automatic Bootstrapping
When the application starts, `Program.cs` executes `DBMigration.EnsureAll(connectionString)`:
1. Verifies database existence (creates database if missing).
2. Generates core tables (`USERS`, `MVC_CASES`, `LABOUR_CASES`, `GRA_CASES`, `APPEAL_DETAILS`, etc.).
3. Adds missing columns and schema constraints automatically.
4. Generates non-clustered performance indexes.

### 3.2 Manual SQL Migration Scripts
If manual SQL deployment is required by enterprise DBA policies:
- Navigate to `Law Project/MVCCaseManagement/SQL/` and `SqlScripts/`.
- Execute `RunMigration.cs` or run `schema.sql` manually via SQL Server Management Studio (SSMS).

---

## 📦 4. Building & Publishing for Production

### 4.1 Automated Publish Script (`publish-project.bat`)
Run the bundled deployment script from `Law Project/MVCCaseManagement/`:

```cmd
publish-project.bat
```

### 4.2 Manual CLI Publish
Alternatively, run the dotnet CLI publish command:

```powershell
cd "Law Project/MVCCaseManagement"
dotnet publish MVCCaseManagement.csproj -c Release -o ./publish /p:UseAppHost=true
```

This generates compiled binaries, views, static assets, and configuration files in `Law Project/MVCCaseManagement/publish/`.

---

## 🌐 5. IIS Server Configuration & Hosting

### 5.1 Create Application Pool
1. Open **IIS Manager** (`inetmgr`).
2. Add Application Pool:
   - **Name**: `MVCCaseManagementAppPool`
   - **.NET CLR Version**: `No Managed Code`
   - **Managed Pipeline Mode**: `Integrated`
3. Advanced Settings:
   - Set **Start Mode** to `AlwaysRunning`.
   - Set **Idle Time-out (minutes)** to `0` (prevents background SMS job from sleeping).

### 5.2 Create IIS Website
1. Add Website:
   - **Site Name**: `MVCCaseManagement`
   - **Physical Path**: `C:\Law Project\Law Project\MVCCaseManagement\publish`
   - **Application Pool**: `MVCCaseManagementAppPool`
   - **Binding**: HTTP (Port 80 / 5000) or HTTPS (Port 443 with SSL Certificate).

### 5.3 Configure Upload Directory Permissions
The application stores uploaded court petitions and certified judgment PDF files under `/uploads`:
- Ensure the `IIS_IUSRS` group and `IIS AppPool\MVCCaseManagementAppPool` user have **Read, Write, and Modify** permissions on:
  - `C:\Law Project\Law Project\MVCCaseManagement\publish\wwwroot\uploads`
  - `C:\Law Project\Law Project\MVCCaseManagement\publish\uploads`

---

## 🔔 6. Operations & Background Service Maintenance

### 6.1 Hosted Background Service
`NotificationBackgroundService` executes automatically inside the ASP.NET Core process:
- Ensures daily SMS notifications for court hearings are dispatched without requiring external Windows Task Scheduler tasks.
- **Important**: Ensure the IIS Application Pool **Start Mode** is set to `AlwaysRunning` and **Preload Enabled** is set to `True` so the background worker stays active 24/7.

---

## 🔍 7. Troubleshooting & Diagnostics

### 7.1 NAPIX e-Courts Gateway Diagnostics
- **Log File Location**: `Law Project/MVCCaseManagement/napix_log.txt`.
- **Token Failures**: If `INVALID_TOKEN` appears repeatedly, verify `ApiKey` and `SecretKey` in `appsettings.json`.
- **PHP Proxy Connection**: If direct NAPIX calls fail, verify local XAMPP Apache service is running on `http://localhost/ecourts/api.php`.

### 7.2 Static PDF Upload 404 Errors
- Verify that both `wwwroot/uploads` and `ContentRootPath/uploads` exist.
- Check `Program.cs` file provider configuration:
  ```csharp
  app.UseStaticFiles(new StaticFileOptions
  {
      FileProvider = new PhysicalFileProvider(uploadsPath),
      RequestPath = "/uploads"
  });
  ```

### 7.3 SQL Connection Timeouts
- Ensure SQL Server TCP/IP protocol is enabled in SQL Server Configuration Manager (Port 1433).
- Check Windows Firewall rules for SQL Server port 1433.
