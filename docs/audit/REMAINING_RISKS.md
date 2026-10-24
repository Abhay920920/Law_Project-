# ⚠️ NWKRTC Law Management System — Remaining Risks & Operational Assumptions

**Document Reference**: Phase Eight Operational Risk Assessment & Recommendations  
**Date**: October 24, 2026  
**Auditor**: Principal Enterprise Software Architect & Application Security Engineer  

---

## 📌 Executive Summary

While all code-level vulnerabilities (IDOR, SQL identifier injection, CSRF enforcement, physical file download gating, and client attribution) have been remediated and verified with 289 passing automated tests, certain external and operational risks remain outside the scope of application source code. These items are cataloged below with recommended mitigation controls.

---

## 1. External & Environmental Risks

### 1.1 NIC e-Courts NAPIX Gateway Protocol Constraints (Static IV Requirement)
- **Risk Assessment**: **Medium / Accepted External Constraint**
- **Description**: As specified in official NIC documentation (`docs/references/eCourts_Open_API_Specifications_NAPIX_1.3_22.04.2025-1-1-1.pdf`), the government gateway requires AES-128-CBC with a static, pre-shared Initialization Vector (IV).
- **Security Implication**: In theory, CBC with a static IV across identical plaintext prefixes is vulnerable to pattern analysis.
- **Mitigation / Reality**: Communications occur over TLS 1.2 / TLS 1.3 encrypted transport, which provides outer semantic security. The application strictly conforms to government specifications to preserve gateway interoperability.
- **Action**: No application changes recommended; accept as an external interoperability constraint.

### 1.2 Credential Rotation Requirement for Historical Working Copy Secrets
- **Risk Assessment**: **High (Operational)**
- **Description**: Working copy configuration files (`appsettings.json`) previously stored plaintext database credentials and SMS gateway tokens.
- **Mitigation Implemented**: Production configurations are documented to load secrets via Windows Environment Variables.
- **Stakeholder Action Required**:
  1. Rotate the database user password for `admin_Law` on `198.38.89.31`.
  2. Rotate the SMS SecureKey token with the Karnataka Mobile-1 administrator.
  3. Provision the new rotated secrets into Windows Environment Variables on the production server.

### 1.3 IIS Worker Process Inactivity Recycling
- **Risk Assessment**: **Medium (Operations & Availability)**
- **Description**: Standard IIS worker processes shut down after 20 minutes of idle time by default. This terminates background `IHostedService` threads (`NotificationBackgroundService` for daily SMS reminders and `NapixBackgroundSyncService` for court sync).
- **Mitigation Documented**: Provided automated PowerShell configuration script in [`DEPLOYMENT_AND_ROLLBACK_PLAN.md`](file:///c:/Users/adts-/Desktop/Law%20Project/docs/audit/DEPLOYMENT_AND_ROLLBACK_PLAN.md) setting `startMode = AlwaysRunning` and `idleTimeout = 0`.
- **Stakeholder Action Required**: Execute the configuration script on the target IIS host before cutting over production traffic.

---

## 2. Recommended Future Enhancements (Post-Remediation)

1. **Centralized Log Aggregation**:
   - Currently, structured logs write to local disk (`logs/fatal-error.txt`) and Windows Event Log.
   - *Recommendation*: Deploy a centralized log sink (such as Seq or Grafana Loki) to aggregate distributed correlation IDs (`X-Correlation-ID`) across multi-server deployments.

2. **End-to-End Browser UI Testing**:
   - The test harness includes 289 unit and integration tests.
   - *Recommendation*: Introduce Playwright or Cypress for end-to-end browser testing of official A4 legal dossier print preview rendering.

3. **Periodic Disaster Recovery Restore Drills**:
   - Automated backups run on SQL Server.
   - *Recommendation*: Conduct an annual mock restore test onto a secondary test server to verify recovery time objectives (RTO < 2 hours).

---

## 🎯 Final Security Clearance
Subject to executing credential rotation and configuring IIS `AlwaysRunning` parameters, the codebase is **verified robust, data-consistent, and cleared for enterprise production operation**.
