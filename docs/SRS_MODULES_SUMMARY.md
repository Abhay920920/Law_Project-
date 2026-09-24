# Software Requirements Specification (SRS)
## Law Project - Case Management System

---

## 1. Introduction

### 1.1 Purpose
The purpose of this document is to specify the software requirements for the comprehensive **Case Management System (Law Project)**. This web-based system is designed to securely record, track, and manage legal cases across multiple domains (MVC/MACT, Gratuity, and Labour) at the Division level, entirely replacing legacy Excel-based registers.

### 1.2 Document Conventions
- **MVC**: Motor Vehicle Claims
- **MACT**: Motor Accident Claims Tribunal
- **EP**: Execution Petition
- **MFA**: Miscellaneous First Appeal
- **SLP**: Special Leave Petition
- **UI**: User Interface
- **DB**: Database

### 1.3 Intended Audience
This document is intended for:
- **Developers & Engineers**: To understand the system architecture, modules, and database structure.
- **QA & Testers**: For formulating test cases and validating functionality.
- **Stakeholders & Management**: For verifying that the software aligns with Government-approved tracking directives.

### 1.4 Product Scope
The **Case Management System** digitizes entirely the workflow handling for diverse transport/corporate legal claims. It includes:
1. **MVC/MACT Claims**: High Court Appeals, Execution Petitions, and Supreme Court tracking.
2. **Gratuity Cases**: Appellate Authority tracking, Controlling Authority directives, Payments, and Delays.
3. **Labour Cases**: Writ Petitions, Employee details, Depot-level allocations, and Disciplinary proceedings.
4. **Master Directories**: Repositories for Division codes, Courts/MACTs, Advocates, Case Statuses, and System Users.
5. **Role-Based Workflows**: Tailored, isolated views for Admins, Division Users, and the Central/Head Office.

---

## 2. Overall Description

### 2.1 Product Perspective
The system operates as a monolithic **ASP.NET Core MVC (net9.0)** application deployed on a centralized server using **SQL Server** as the persistent storage layer. It serves remote corporate divisions through a standard web-browser interface, ensuring concurrent, multi-user accessibility.

### 2.2 Product Functions
- **Unified Dashboard**: Real-time aggregated statistics categorized by Case Types and Status.
- **Case Lifecycle Management**: End-to-end registration, hearing scheduling, disposal updating, and appellant tracking.
- **Bi-lingual Interface**: Complete Kannada/English localizations aligning with regional government standards.
- **Automated Reporting**: On-the-fly PDF and Excel generation.
- **Audit Tracking**: Comprehensive and immutable audit logs capturing creation and modification of records.

### 2.3 User Classes and Characteristics
1. **Admin**: Responsible for creating users, handling Master Data CRUD operations, and performing system-wide audits.
2. **Division User**: Resource-scoped users tied to a specific `DivisionID`. Only authorized to create/edit cases and track lifecycles for their specific jurisdiction.
3. **Central Office (CO)**: High-level oversight users with read-all permissions and specific approval privileges.
