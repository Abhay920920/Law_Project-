# 🏛️ Legal Case Management System (`MVCCaseManagement`)

> **Application Subsystem**: ASP.NET Core 9.0 MVC Enterprise Web Application  
> **Parent Repository**: [Law Project Monorepo Root](file:///c:/Law%20Project/README.md)

---

## 📌 Application Overview

`MVCCaseManagement` is the core web application serving legal claim workflows, legal opinion requests, advocate fee tracking, and e-Courts live integrations across transport and corporate divisions.

---

## 📁 Central & Subsystem Documentation Index

### 🌐 Global Monorepo Documentation (`/docs/`)
- **[Root Monorepo README](file:///c:/Law%20Project/README.md)** — Master overview, stack, and directory structure.
- **[System Architecture Guide](file:///c:/Law%20Project/docs/SYSTEM_ARCHITECTURE.md)** — Layered architecture, controllers, DAL, RBAC security matrix, and ERD schemas.
- **[e-Courts NAPIX Integration Guide](file:///c:/Law%20Project/docs/ECOURTS_INTEGRATION_GUIDE.md)** — Complete e-Courts gateway spec, AES-128-CBC encryption, HMAC-SHA256 signing, and 41 OpenAPI JSON specs.
- **[Modules & Features Guide](file:///c:/Law%20Project/docs/MODULES_AND_FEATURES_GUIDE.md)** — Detailed operational manual for MVC, Labour, Gratuity, and Alerts.
- **[Deployment & Operations Guide](file:///c:/Law%20Project/docs/DEPLOYMENT_AND_OPERATIONS_GUIDE.md)** — IIS setup, `appsettings.json`, SQL migrations, and publish guides.

### 🏢 Application Specific Guides (`MVCCaseManagement/docs/`)
- **[Application Documentation Index](docs/README.md)** — Local index for app specs.
- **[Software Requirements Specification (SRS)](docs/SRS.md)** — System scope, target audience, module specifications, and user roles.
- **[Quick Start Guide](docs/QUICKSTART.md)** — Local development setup and initial database migration.
- **[Notification & Hearing Alerts](docs/UPCOMING_HEARING_ALERTS.md)** — Specifications for background notifications and SMS alerts.

---

## ⚡ Key Feature Highlights

- **Multi-Module Support**: MVC/MACT, Labour Court, Service Matters (Direct High Court Writ Petitions), Execution Petitions (EP), Gratuity Act Claims.
- **Official e-Courts NAPIX Integration**: Automatic 16-digit CNR discovery, AES-128-CBC payload encryption, HMAC-SHA256 token signing, District & High Court cause list sync.
- **Automated Background Notifications**: Daily SMS hearing reminders, stay order expiration warnings, and High Court appeal window alerts via `NotificationBackgroundService`.
- **High-Performance Architecture**: Non-clustered SQL performance indexes, Brotli/Gzip HTTP compression, and Dapper/ADO.NET parameterization.
