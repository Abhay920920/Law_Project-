# SECURITY HARDENING REPORT

## 1. Executive Summary

This document details the security hardening measures implemented across the entire NWKRTC Law Project codebase to achieve production-grade security, confidentiality, integrity, and resilience. Every fix preserves 100% of business functionality, routes, and workflows while eliminating critical security vulnerabilities.

---

## 2. Hardened Vulnerabilities & Implemented Controls

### 2.1 Hardcoded Secrets & Credential Management
- **Identified Risk**: Hardcoded database connection strings, passwords (`password=root`), SMS gateway credentials (`username=nwkrtc`, `password=nwkrtc@123`, secure HMAC keys), e-Courts NAPIX AuthKey and HMAC secrets, and TR-18 API keys were embedded directly in source code and configuration files.
- **Implemented Fix**:
  - Created strongly-typed `AppOptions.cs` (`ECourtsOptions`, `SmsOptions`, `TR18Options`) configured via the standard ASP.NET Core `IOptions<T>` pattern.
  - Stripped all plaintext secrets and passwords from `appsettings.json` and `NapixEcourtsApi/appsettings.json`.
  - Added `appsettings.Development.json.example` providing structured templates for local development and CI/CD secret injection (e.g., Azure Key Vault / AWS Secrets Manager / Environment Variables).
  - Refactored `SMSService.cs`, `TR18Service.cs`, `ECourtsNapixService.cs`, and `Migrator/Program.cs` to read strictly from configuration/environment variables with no hardcoded fallback secrets.

### 2.2 Server-Side Request Forgery (SSRF) & Open Redirects
- **Identified Risk**: In `ECourtsController.cs` and `UploadsController.cs`, remote PDF URLs were downloaded directly using user-supplied parameters without domain or IP validation, allowing potential SSRF attacks targeting internal networks, localhost, or cloud metadata endpoints (`169.254.169.254`).
- **Implemented Fix**:
  - Developed `UrlSecurityValidator.cs` with an approved government domain whitelist (`*.ecourts.gov.in`, `delhigw.napix.gov.in`, `tr18.itnwkrtc.in`, `karnatakajudiciary.kar.nic.in`).
  - Implemented strict blocking of non-HTTPS schemes, IPv4/IPv6 loopback (`127.0.0.0/8`, `::1`), RFC1918 private subnets (`10.0.0.0/8`, `172.16.0.0/12`, `192.168.0.0/16`), and link-local cloud metadata addresses (`169.254.169.254`).
  - Validated external URLs prior to opening any HTTP connections in `ECourtsController.ViewOrderPdf`.

### 2.3 Broken Access Control & IDOR / BOLA Prevention
- **Identified Risk**:
  - Controllers relied on `HttpContext.Session.GetInt32("DivisionID") ?? 0`. When sessions expired but cookies persisted, users defaulted to Division 0 (Central Office / Admin), granting division users unauthorized access to Central Office actions!
  - `Edit` POST actions in `CaseController.cs`, `LabourController.cs`, and `GratuityController.cs` did not verify whether the case being updated belonged to the caller's assigned division.
  - Sensitive API endpoints (`/api/cases/*`) and PDF download actions had `[AllowAnonymous]` attributes enabled.
- **Implemented Fix**:
  - Migrated division and role identification to authenticated claims (`User.FindFirstValue("DivisionID")`, `User.IsInRole(...)`).
  - Added strict ownership validation on `Edit` POST methods: non-Central Office users cannot modify cases belonging to another division.
  - Removed `[AllowAnonymous]` from `CasesController`, `ECourtsController.GetCnrRaw`, `DownloadAllOrdersZip`, and `UploadOrderPdf`.
  - Secured role-specific actions (`CLOAction`, `MDAction`) with server-side role checks preventing privilege escalation.

### 2.4 File Upload Security
- **Identified Risk**: Upload endpoints permitted arbitrary uploads without magic-byte verification, and static files in `/uploads` were served directly by `PhysicalFileProvider`, bypassing authentication.
- **Implemented Fix**:
  - Removed the unauthenticated `/uploads` static file mapping in `Program.cs`. All document requests must route through `UploadsController` with authorization.
  - Added PDF file signature validation (`%PDF-` magic bytes `0x25, 0x50, 0x44, 0x46, 0x2D`).
  - Enforced a 20 MB size limit and whitelist of safe document extensions (`.pdf`, `.doc`, `.docx`, `.jpg`, `.jpeg`, `.png`).
  - Generated non-deterministic GUID filenames on disk and prevented directory traversal via `Path.GetFullPath` canonical containment checks.

### 2.5 Cross-Site Request Forgery (CSRF)
- **Identified Risk**: Certain state-changing POST actions lacked CSRF validation tokens.
- **Implemented Fix**:
  - Enforced `[AutoValidateAntiforgeryToken]` globally in `Program.cs` for all non-GET/HEAD/OPTIONS/TRACE requests.
  - Confirmed and applied explicit `[ValidateAntiForgeryToken]` on all form submission endpoints.

### 2.6 TLS Certificate Validation
- **Identified Risk**: Custom `HttpClientHandler` delegates returned `ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true;`, disabling TLS certificate verification globally.
- **Implemented Fix**:
  - Removed all `return true;` TLS bypasses.
  - Restored standard platform X.509 certificate validation.

### 2.7 Security Headers & Cookie Security
- **Implemented Fix**:
  - `HSTS`: Max-age 31536000 seconds with `includeSubDomains`.
  - `X-Content-Type-Options: nosniff`.
  - `X-Frame-Options: SAMEORIGIN`.
  - `Referrer-Policy: strict-origin-when-cross-origin`.
  - `Content-Security-Policy`: Formulated based on application dependencies (Bootstrap, Bootstrap Icons, Chart.js, inline Razor scripts).
  - Authentication Cookies configured with `HttpOnly = true`, `SecurePolicy = Always`, `SameSite = Lax`, and sliding expiration.

---

## 3. Verification & Compliance Matrix

| Vulnerability Category | Original Status | Hardened Status | Verification Method |
|:---|:---|:---|:---|
| Hardcoded Credentials | Vulnerable | **SECURE** | Codebase grep & external config validation |
| SSRF in PDF retrieval | Vulnerable | **SECURE** | `UrlSecurityValidatorTests` unit tests |
| Broken Access Control / IDOR | Vulnerable | **SECURE** | Claims-based division ownership checks |
| File Upload Insecurity | Vulnerable | **SECURE** | Magic-bytes check, GUID naming, auth routing |
| CSRF Vulnerabilities | Partial | **SECURE** | Global `AutoValidateAntiforgeryTokenAttribute` |
| TLS Certificate Bypass | Vulnerable | **SECURE** | Platform TLS certificate enforcement |
| Missing Security Headers | Missing | **SECURE** | Custom security headers middleware |
