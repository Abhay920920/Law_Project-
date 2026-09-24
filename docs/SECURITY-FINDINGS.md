# SECURITY FINDINGS & AUDIT INVENTORY

## 1. Inventory of Remediated Security Flaws

| ID | Vulnerability | Location | Original Severity | Remediated Status |
|:---|:---|:---|:---:|:---:|
| SEC-001 | Hardcoded Plaintext Database Credentials | `appsettings.json`, `NapixEcourtsApi/appsettings.json`, `Migrator/Program.cs` | Critical | **RESOLVED** (Replaced with external config / env vars) |
| SEC-002 | Hardcoded SMS Gateway Credentials & Key | `SMSService.cs` | High | **RESOLVED** (Externalized into `SmsOptions`) |
| SEC-003 | Hardcoded TR-18 API Secrets | `TR18Service.cs` | High | **RESOLVED** (Externalized into `TR18Options`) |
| SEC-004 | Hardcoded NAPIX Auth & HMAC Keys | `ECourtsNapixService.cs` | Critical | **RESOLVED** (Externalized into `ECourtsOptions`) |
| SEC-005 | SSRF & Open Redirect in Order PDF Viewer | `ECourtsController.cs` | Critical | **RESOLVED** (Strict host allowlist & private IP blocking) |
| SEC-006 | Anonymous Access to Case Search APIs & Orders | `CasesController.cs`, `ECourtsController.cs` | High | **RESOLVED** (Enforced `[Authorize]` across all API endpoints) |
| SEC-007 | Broken Access Control / Default to Central Office | `CaseController.cs`, `LabourController.cs` | Critical | **RESOLVED** (Switched from Session to Claims with IDOR checks) |
| SEC-008 | Insecure Direct Object Reference (IDOR) on POST | `CaseController.cs`, `LabourController.cs`, `GratuityController.cs` | High | **RESOLVED** (Enforced division ownership checks on POST) |
| SEC-009 | Unrestricted Static Uploads Access | `Program.cs` (`UseStaticFiles` on `/uploads`) | High | **RESOLVED** (Removed static map; routed via `UploadsController`) |
| SEC-010 | Missing Magic-Byte Validation on Uploads | `ECourtsController.cs`, `UploadsController.cs` | High | **RESOLVED** (Enforced `%PDF-` signature & GUID naming) |
| SEC-011 | Disabled TLS Certificate Validation | `Program.cs`, `NapixEcourtsApi/Program.cs` | Critical | **RESOLVED** (Removed `return true;` bypasses) |
| SEC-012 | Potential CSRF on State-Changing Actions | Multiple controllers | Medium | **RESOLVED** (Global `AutoValidateAntiforgeryTokenAttribute`) |
| SEC-013 | Missing Security Headers & Cookie Flags | HTTP Response pipeline | Medium | **RESOLVED** (Added HSTS, CSP, nosniff, SameSite Lax, HttpOnly) |

---

## 2. Mandatory Credential Rotation Notice

> [!CAUTION]
> Because the following credentials were previously stored in plain text within source code and repositories, they must be considered compromised and **MUST BE ROTATED** in your production systems:
> 1. **SQL Server Database Passwords**: Rotate all passwords for production database users (e.g. `sa`, `nwkrtc_law_user`).
> 2. **SMS Gateway Credentials**: Rotate the API password and HMAC secure key with the telecommunications service provider.
> 3. **e-Courts NAPIX AuthKey & Client Secret**: Request refreshed API credentials from the National Informatics Centre (NIC) / Government of India NAPIX administrative team.
> 4. **TR-18 API Keys**: Generate a new API token on the TR-18 application gateway (`tr18.itnwkrtc.in`).
