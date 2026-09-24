# 🏛️ Law Case Management System Documentation Index

Welcome to the documentation hub for **MVCCaseManagement**.

---

## 📚 Global Monorepo Documentation Hub (`/docs/`)

For full architectural, cryptographic, and operational details, refer to the master monorepo guides:

1. 🏗️ **[System Architecture Guide](file:///c:/Law%20Project/docs/SYSTEM_ARCHITECTURE.md)** — Architectural design, controller routing, DAL migration, and RBAC matrices.
2. 🌐 **[e-Courts NAPIX Integration Guide](file:///c:/Law%20Project/docs/ECOURTS_INTEGRATION_GUIDE.md)** — NIC e-Courts NAPIX gateway, AES/HMAC encryption, and 41 OpenAPI JSON specs.
3. 📑 **[Modules & Features Operational Guide](file:///c:/Law%20Project/docs/MODULES_AND_FEATURES_GUIDE.md)** — Detailed operational manual for MVC, Labour, Gratuity, and Alerts.
4. 🚀 **[Deployment & Operations Guide](file:///c:/Law%20Project/docs/DEPLOYMENT_AND_OPERATIONS_GUIDE.md)** — Server prerequisites, IIS setup, `appsettings.json`, and database deployment.

---

## 🏢 Application Specifications (`MVCCaseManagement/docs/`)

1. **[Software Requirements Specification (SRS)](SRS.md)**
   - System scope, module specifications (MVC/MACT, Labour, Gratuity, Service Matters), database table schemas, and user roles.

2. **[Quick Start Guide](QUICKSTART.md)**
   - Developer environment setup, `appsettings.json` configuration, database initialization, and default admin login.

3. **[Notification & Alert Specifications](UPCOMING_HEARING_ALERTS.md)**
   - Detailed rules for background notifications, upcoming hearing SMS alerts, compliance deadlines, stay order expirations, and appeal windows.

---

## 🏗️ Technical Stack Summary

- **Framework**: ASP.NET Core MVC (`net9.0`)
- **Database Engine**: Microsoft SQL Server
- **Data Access**: ADO.NET (`SqlCommand`), Dapper, and dynamic runtime schema migrator (`DBMigration.cs`)
- **API Security**: AES-128-CBC Encryption, HMAC-SHA256 Signature, OAuth2 Token Caching
- **UI & Views**: Responsive Bootstrap 5, DataTables, and bilingual English/Kannada Razor views
