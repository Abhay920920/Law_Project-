# 🚀 NWKRTC Law Management System — Deployment & Rollback Plan

**Document Reference**: Production Deployment, IIS Configuration & Reversion Runbook  
**Target Environment**: Windows Server 2019 / 2022 | IIS 10.0 | ASP.NET Core 9.0 Runtime Hosting Bundle  
**Date**: October 24, 2026  
**Remediation Branch**: `security-remediation`  

---

## 📌 1. Pre-Deployment Readiness Checklist

Before initiating production deployment:
- [x] Full solution compilation verified (`0 Errors, Release Mode`).
- [x] Automated test gate satisfied (`289/289 Passing Tests, 0 Failures`).
- [x] Immutable Git backup branch confirmed (`backup-main-before-remediation`).
- [ ] Database backup executed and verified with `RESTORE VERIFYONLY`.
- [ ] Production IIS Application Pool stopped or `app_offline.htm` deployed.
- [ ] Production Environment Variables provisioned.

---

## ⚙️ 2. Production Environment Variable Configuration

To eliminate plaintext secrets from configuration files, configure the following Windows System Environment Variables on the production web server:

| Environment Variable | Target Setting in App | Description |
| :--- | :--- | :--- |
| `ConnectionStrings__MVCCaseDB` | `ConnectionStrings:MVCCaseDB` | Production SQL Server connection string with encrypted credentials. |
| `SMS__Password` | `SMS:Password` | Karnataka Mobile-1 SMS gateway password. |
| `SMS__SecureKey` | `SMS:SecureKey` | Karnataka Mobile-1 secure authentication token. |
| `eCourts__MVC__ApiKey` | `eCourts:MVC:ApiKey` | NIC NAPIX eCourts OAuth client API key. |
| `eCourts__MVC__SecretKey` | `eCourts:MVC:SecretKey` | NIC NAPIX eCourts OAuth secret key. |
| `AppSettings__TR18ApiKey` | `AppSettings:TR18ApiKey` | TR-18 accident portal API key. |

---

## 🏢 3. IIS 10.0 Application Pool Hardening

Because the system runs two critical background hosted services (`NotificationBackgroundService` for daily SMS reminders and `NapixBackgroundSyncService` for hourly court synchronization), IIS worker process recycling must be configured correctly:

### PowerShell Configuration Command (Run as Administrator)
```powershell
Import-Module WebAdministration

$AppPoolName = "NWKRTC_Law_AppPool"

# 1. Set StartMode to AlwaysRunning so background workers initialize immediately
Set-ItemProperty "IIS:\AppPools\$AppPoolName" -Name "startMode" -Value "AlwaysRunning"

# 2. Set IdleTimeout to 0 to prevent IIS from terminating background jobs during quiet hours
Set-ItemProperty "IIS:\AppPools\$AppPoolName" -Name "processModel.idleTimeout" -Value "00:00:00"

# 3. Enable preload on the web site
Set-ItemProperty "IIS:\Sites\LawProject" -Name "preloadEnabled" -Value $True

Write-Host "IIS Application Pool successfully hardened for background hosted services." -ForegroundColor Green
```

---

## 📦 4. Step-by-Step Deployment Procedure

### Step 1: Place Application Offline
Deploy `app_offline.htm` into the target IIS root folder (`C:\inetpub\wwwroot\LawProject\`) to gracefully drain active requests.

### Step 2: Publish Compiled Artifacts
Execute the master publish script from the repository root:
```powershell
.\publish.ps1 -Configuration Release -CleanPublish
```
This builds all projects, runs all 289 tests as a mandatory quality gate, and generates pre-compiled binaries in `publish\`.

### Step 3: Synchronize Binaries to Production
Copy the published output to the production IIS directory, excluding:
- `uploads\` folder (preserve all historical uploaded files).
- `appsettings.Production.json` (preserve environment overrides).

### Step 4: Remove Application Offline Page
Delete `app_offline.htm` from the production directory.

### Step 5: Post-Deployment Smoke Verification
1. Query Health Endpoints:
   - `GET https://law.nwkrtc.in/health/live` $\to$ Returns HTTP 200 OK.
   - `GET https://law.nwkrtc.in/health/ready` $\to$ Returns HTTP 200 OK.
2. Authenticate as a Division User (e.g. Belagavi) and confirm access to own cases.
3. Test physical document download to verify IDOR boundary rejection on other divisions' documents.
4. Test e-Courts NAPIX connection status on `/ApiDashboard`.

---

## 🔄 5. Rollback Runbook (Zero-Downtime Reversion)

If post-deployment smoke verification detects an unforeseen issue:

### Step 1: Immediate Git Rollback
```powershell
# Restore exact pre-remediation commit
git checkout backup-main-before-remediation

# Re-publish previous binary baseline
.\publish.ps1 -Configuration Release
```

### Step 2: Swap Production Binaries
Copy the previous baseline binaries from `Publish_Output\Previous` back into the IIS root.

### Step 3: Database Compatibility
Because all database changes made during remediation are strictly additive (no dropped columns or renamed tables), previous application binaries remain 100% backward-compatible with the database without requiring a database restore.

### Step 4: Restart Application Pool
```powershell
Restart-WebAppPool -Name "NWKRTC_Law_AppPool"
```
