# 📑 NWKRTC Law Management System — Feature Traceability & Regression Matrix

**System Target**: NWKRTC Legal Management Platform (ASP.NET Core 9.0 MVC / SQL Server)  
**Baseline Date**: October 24, 2026  
**Document**: Phase Two Feature Inventory & Zero-Regression Matrix  

---

## 🏛️ End-to-End Feature Traceability Matrix

| Feature Domain | Route / UI View | Controller Action | Business Service | Repository | Database Entities | Authorization Policy | Automated Tests | Baseline Status |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| **MVC Claims Registration** | `/Case/Create` | `CaseController.Create` | - | `CaseRepository.SaveCase` | `MVC_CASES`, `MVC_CASE_PETITIONERS`, `MVC_CASE_ADVERSE_DETAILS` | Authenticated (Division User / CO) | `CaseFilterIntegrationTests` | **VERIFIED** |
| **MVC Claims Lifecycle & Edit** | `/Case/Edit/{id}` | `CaseController.Edit` | - | `CaseRepository.GetCaseById`, `UpdateCase` | `MVC_CASES` | Division Ownership Guard (`model.DivisionID == userDiv`) | Unit tests | **VERIFIED** |
| **Witness & Proof Tracking** | `/Case/Details/{id}` | `CaseController.Details` | - | `CaseRepository.GetCaseById` | `MVC_CASE_WITNESS_DETAILS` | Division Gated / CO | UI verification | **VERIFIED** |
| **High Court Appeal (MFA/SLP)** | `/Appeal/Manage/{id}` | `AppealController.Manage` | - | `AppealRepository.SaveAppeal` | `APPEAL_DETAILS`, `HIGH_COURT_APPEALS` | Central Office / Division User | Manual test | **VERIFIED** |
| **Execution Petitions (MVC EP)** | `/EP/Index`, `/EP/Edit/{id}` | `EPController.Index`, `Edit` | - | `EPRepository.GetAllEPs`, `SaveEP`, `UpdateEP` | `tbl_EP_Cases`, `MVC_CASES` | Authenticated (Division check pending patch) | Schema verified | **REMEDIATION REQ** |
| **Labour Court (ID/KID Cases)** | `/Labour/CaseList`, `/Labour/Edit/{id}` | `LabourController.CaseList`, `Edit` | - | `LabourRepository.GetCases`, `SaveCase` | `tbl_Labour_Cases` | Division Ownership Guard (`model.DivisionID == myDiv`) | Schema verified | **VERIFIED** |
| **Multi-Tier Review (LO $\to$ MD)** | `/Labour/Action/{id}` | `LabourController.Action` | - | `LabourRepository.SaveLegalReview` | `tbl_Labour_Cases`, `tbl_Legal_Opinions` | Hierarchical Role Checks (`LO`, `DyCLO`, `CLO`, `MD`) | Integration suite | **VERIFIED** |
| **Official A4 Dossier Proforma** | `/Labour/PrintCaseDossier/{id}` | `LabourController.PrintCaseDossier` | - | `LabourRepository.GetCaseById` | `tbl_Labour_Cases` | Division User / CO | Print layout | **VERIFIED** |
| **Arising Applications (33-C(2))** | `/Labour/ArisingApplication`, `/Labour/EditArisingApplication/{id}` | `LabourController.EditArisingApplication` | - | `ArisingApplicationRepository.Save`, `Update` | `tbl_Arising_Applications` | Authenticated (Division check pending patch) | Schema verified | **REMEDIATION REQ** |
| **Arising Application Ledger** | `/Labour/DetailsArisingApplication/{id}` | `LabourController.DetailsArisingApplication` | - | `ArisingApplicationRepository.GetPayments` | `tbl_Arising_Application_Payments` | Division User / CO | Schema verified | **VERIFIED** |
| **Payment of Gratuity (CA/AA)** | `/Gratuity/CaseList`, `/Gratuity/Edit/{id}` | `GratuityController.Edit` | - | `GratuityRepository.GetCaseById`, `Update` | `tbl_Gratuity_Cases` | Division Ownership Guard (`existing.DivisionCode == myDiv`) | Schema verified | **VERIFIED** |
| **Statutory 10% Gratuity Interest** | `/Gratuity/AuditCalculation` | `GratuityController.AuditCalculation` | - | `GratuityRepository` | `tbl_Gratuity_Interest` | Authenticated | `GratuityCalculationTests` | **VERIFIED** |
| **Other Courts (Civil/Revenue)** | `/Home/OtherCourts` | `HomeController.OtherCourts` | - | `OtherCourtsRepository.GetAll` | `tbl_Other_Courts_Cases` | Authenticated | Schema verified | **VERIFIED** |
| **Petty Bill Fee Vetting & Audit** | `/Report/Index`, `/Report/PrintBill/{id}` | `ReportController.PrintBill` | - | `PettyBillRepository.SaveBill` | `tbl_Petty_Bills`, `tbl_Petty_Bill_Payments` | Authenticated | `PettyBillSerializationTests` | **VERIFIED** |
| **Previous Payment Deduplication** | `/Report/SavePettyBill` | `ReportController.SavePettyBill` | - | `PettyBillRepository` | `tbl_Petty_Bill_Disbursements` | Authenticated | `PettyBillSerializationTests` | **VERIFIED** |
| **User Authentication & PBKDF2** | `/Account/Login` | `AccountController.Login` | `PasswordHelper` | `UserRepository.GetUserByUsername` | `tbl_Users` | Public / Anonymous | `PasswordHelperTests` | **VERIFIED** |
| **Brute-Force & Lockout Guard** | `/Account/Login` | `AccountController.Login` | `IMemoryCache` | `UserRepository.UpdateLastLogin` | In-memory cache + `tbl_Users` | Public | Cache tests | **VERIFIED** |
| **OTP Password Reset** | `/Account/ForgotPassword` | `AccountController.ForgotPassword` | `SMSService` | `UserRepository.SaveOtp` | `tbl_Users` | Public | SMS unit tests | **VERIFIED** |
| **Division Controller Read-Only** | Global Pipeline Filter | `DivisionControllerReadOnlyFilter` | - | - | - | Role `Division Controller` / `DC` | Filter tests | **VERIFIED** |
| **Audit Activity Logging** | Internal Async Worker | `ICaseActivityLogger` | `CaseActivityLogger` | Direct ADO.NET | `tbl_Case_Activity_Logs` | Background system service | Event logging | **VERIFIED** |
| **Physical Document Streaming** | `/uploads/{*filePath}` | `UploadsController.Download` | - | Direct SQL Lookup | `uploads/` filesystem | Authenticated (Scope expansion req) | Path tests | **REMEDIATION REQ** |
| **e-Courts NAPIX Encryption** | `/ECourts/SyncCase` | `ECourtsController.SyncCase` | `ECourtsNapixService` | `ECourtsRepository` | `tbl_Napix_Tracked_Cases` | Authenticated | `NapixDirectPipelineTests` | **VERIFIED** |
| **Hourly NAPIX Quota (1000/hr)** | Background & On-Demand | `NapixQuotaService` | `NapixQuotaService` | `ECourtsRepository` | `tbl_Napix_Api_Quota` | System Singleton | `NapixSyncEngineTests` | **VERIFIED** |
| **Nyaya Patha RAG AI Assistant** | `/NyayaPatha/Ask` | `NyayaPathaController.Ask` | `UnifiedLegalResearchService` | 34 AI Subsystem Services | SQL Precedents & Vector Cache | `NyayaPathaAIAccess` Policy | 7 AI Evaluation Suites (100+ tests) | **VERIFIED** |
| **Daily SMS Hearing Alerts** | Background Worker | Hosted Service | `SMSService` | `NotificationRepository` | `tbl_SMS_Logs` | Background Service | Mock pipeline | **VERIFIED** |
| **Monthly MIS Reports & Statements** | `/Report/MonthlyStatement` | `ReportController.MonthlyStatement` | - | Multi-Repo Aggregates | `MVC_CASES`, `tbl_Labour_Cases` | Central Office / Division User | Manual test | **VERIFIED** |

---

## 🎯 Verification Matrix Invariants
1. **Zero Functionality Reduction**: No module, controller route, or report export will be omitted or simplified.
2. **Backward Data Compatibility**: All 83 tables and relationships retain their existing schema definitions.
3. **Multi-Tenant Integrity**: Division users are restricted to their own division's cases; Central Office retains global administrative scrutiny.
