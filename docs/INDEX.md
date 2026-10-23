# 📚 Law Project — Master Documentation Catalog

Welcome to the centralized documentation library for the **NWKRTC Law Project**. All architectural specifications, operational guides, domain walkthroughs, and integration manuals are cataloged below following the Diataxis framework.

---

## 🏛️ 1. Architecture, Standards & System Design

| Document | Primary Audience | Purpose & Summary |
| :--- | :--- | :--- |
| 🏗️ **[SYSTEM_ARCHITECTURE.md](SYSTEM_ARCHITECTURE.md)** | Architects, Lead Engineers | Complete technical architecture, middleware pipelines, 19 MVC controllers, 34 AI services, 32 DAL repositories, 8 RBAC roles, and background worker threads. |
| 📋 **[PROJECT_RULES.md](PROJECT_RULES.md)** | All Developers | Coding standards, C# 13 / .NET 9 conventions, zero-tolerance SQL parameterization rules, CSRF tokens, file upload safety, and release discipline. |
| 💾 **[DATABASE_SCHEMA.md](DATABASE_SCHEMA.md)** | DBAs, Backend Engineers | Comprehensive dictionary for all 83 database tables, Entity Relationship Diagram (ERD), stored procedures, and dynamic schema migration engine. |
| 📑 **[SRS.md](SRS.md)** | Stakeholders, QA, Developers | Software Requirements Specification detailing business requirements, functional rules, 8 user roles, and verification criteria. |

---

## ⚙️ 2. Domain Modules & Operational Guides

| Document | Primary Audience | Purpose & Summary |
| :--- | :--- | :--- |
| ⚖️ **[MODULES_AND_FEATURES_GUIDE.md](MODULES_AND_FEATURES_GUIDE.md)** | Legal Officers, Business Analysts | Functional walkthrough of all core modules: Motor Vehicle Claims (MVC), Labour Disputes & Arising Applications, Payment of Gratuity Act, Other Courts, and MIS Statements. |
| 🌐 **[ECOURTS_INTEGRATION_GUIDE.md](ECOURTS_INTEGRATION_GUIDE.md)** | Integration Engineers, Admins | NIC e-Courts NAPIX v1.3 gateway guide, AES-128/HMAC cryptography, automated Court Hall resolution, MyCases server-side pagination, and 41 OpenAPI endpoints. |
| 💰 **[PETTY_BILL_PREVIOUS_PAYMENTS_GUIDE.md](PETTY_BILL_PREVIOUS_PAYMENTS_GUIDE.md)** | Finance, Legal Auditors | Procedure for tracking advocate fees, certified copy expenses, previous voucher disbursements, and automatic fee disincentives. |

---

## 🤖 3. AI & Deployment Operations

| Document | Primary Audience | Purpose & Summary |
| :--- | :--- | :--- |
| 🧠 **[NYAYA_PATHA_PRODUCTION_DEPLOYMENT_PLAN.md](NYAYA_PATHA_PRODUCTION_DEPLOYMENT_PLAN.md)** | SysAdmins, AI Engineers | Nyaya Patha AI (ನ್ಯಾಯ ಪಥ) operational guide: on-premises Ollama Qwen2.5:7b deployment, Search-First RAG pipeline, hardware sizing, and instant kill switches. |
| 🚀 **[PUBLISHING_GUIDE.md](PUBLISHING_GUIDE.md)** | DevOps, SysAdmins | Complete publishing guide: `publish.ps1` / `publish.bat`, 284/284 test gates, IIS 10.0 Application Pool configuration, `web.config` rules, and post-deploy checks. |

---

## 📑 4. External Technical References

- **`docs/references/eCourts_Open_API_Specifications_NAPIX_1.3_22.04.2025-1-1-1.pdf`**: Official NIC e-Courts Open API Specification v1.3.
- **`docs/references/HighCourt_Open_API_Specifications_1.0_NAPIX_07.09.2023-1-1-1.pdf`**: Official NIC High Court Open API Specification v1.0.
