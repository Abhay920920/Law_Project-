# 🔗 NWKRTC Law Management System — Feature-to-Code Traceability Matrix

**Document Reference**: End-to-End Stack Traceability from UI to Database and Tests  
**Verification Level**: **VERIFIED IN SOURCE & TEST**  

---

## 1. Full-Stack Traceability Register

| Feature ID | Feature Name | UI View (`.cshtml`) | Controller / Action | ViewModel / DTO | Service / Helper | Repository | SQL Table(s) | Verification Test |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| **AUTH-01** | User Login | `Views/Account/Login.cshtml` | `AccountController.Login` (POST) | `LoginViewModel` | `PasswordHelper` (PBKDF2) | `CaseRepository` | `USERS, DIVISIONS` | `PasswordHelperTests` |
| **MVC-01** | MVC Registration | `Views/Case/Create.cshtml` | `CaseController.Create` (POST) | `CaseViewModel` | `NetworkDeviceHelper` | `CaseRepository` | `MVC_CASES, MVC_PETITIONER_DETAILS` | `CaseFilterIntegrationTests` |
| **MVC-02** | MVC Case List | `Views/Case/Index.cshtml` | `CaseController.Index` (GET) | `List<CaseViewModel>` | None | `CaseRepository` | `MVC_CASES` | `CaseFilterIntegrationTests` |
| **MVC-03** | MVC Case Dossier | `Views/Case/Details.cshtml` | `CaseController.Details` (GET) | `CaseDetailsViewModel` | None | `CaseRepository` | `MVC_CASES, HEARINGS, ADVERSE` | `SecurityRemediationValidationTests` |
| **MVC-04** | MVC Case Edit | `Views/Case/Edit.cshtml` | `CaseController.Edit` (POST) | `CaseViewModel` | `ICaseActivityLogger` | `CaseRepository` | `MVC_CASES` | `SecurityRemediationValidationTests` |
| **MVC-05** | Hearing Recording | `Views/Case/_HearingsTab.cshtml` | `CaseController.SaveHearing` (POST) | `HearingViewModel` | `ICaseActivityLogger` | `CaseRepository` | `MVC_CASE_HEARING_DETAILS` | `SecurityRemediationValidationTests` |
| **MVC-06** | Adverse Award Entry | `Views/Case/_AdverseModal.cshtml`| `CaseController.SaveAdverseDetails`| `AdverseDetailsViewModel`| `ICaseActivityLogger` | `CaseRepository` | `MVC_CASE_ADVERSE_DETAILS` | `SecurityRemediationValidationTests` |
| **APP-01** | MFA Appeal Filing | `Views/Appeal/Manage.cshtml` | `AppealController.Save` (POST) | `AppealViewModel` | `ICaseActivityLogger` | `AppealRepository` | `APPEAL_DETAILS, HC_ADVOCATES` | `SecurityRemediationValidationTests` |
| **APP-02** | SLP Registration | `Views/Appeal/SLPRegistration.cshtml`| `AppealController.SLPRegistration`| `SLPViewModel` | `ICaseActivityLogger` | `AppealRepository` | `APPEAL_DETAILS` | `Manual Verified` |
| **LAB-01** | Labour Dispute Reg | `Views/Labour/Create.cshtml` | `LabourController.Create` (POST) | `LabourCaseViewModel` | `NetworkDeviceHelper` | `LabourRepository` | `LABOUR_CASES, PETITIONERS` | `CaseFilterIntegrationTests` |
| **LAB-04** | LO Legal Review | `Views/Labour/LOAction.cshtml` | `LabourController.LOAction` (POST) | `LegalActionViewModel` | `ICaseActivityLogger` | `LabourRepository` | `LABOUR_CASES, NOTINGS` | `SecurityRemediationValidationTests` |
| **LAB-05** | DyCLO Legal Review | `Views/Labour/DyCLOAction.cshtml` | `LabourController.DyCLOAction` (POST) | `LegalActionViewModel` | `ICaseActivityLogger` | `LabourRepository` | `LABOUR_CASES, NOTINGS` | `SecurityRemediationValidationTests` |
| **LAB-06** | CLO Legal Review | `Views/Labour/CLOAction.cshtml` | `LabourController.CLOAction` (POST) | `LegalActionViewModel` | `ICaseActivityLogger` | `LabourRepository` | `LABOUR_CASES, NOTINGS` | `SecurityRemediationValidationTests` |
| **LAB-07** | MD Final Approval | `Views/Labour/MDAction.cshtml` | `LabourController.MDAction` (POST) | `LegalActionViewModel` | `ICaseActivityLogger` | `LabourRepository` | `LABOUR_CASES, NOTINGS` | `SecurityRemediationValidationTests` |
| **LAB-08** | Arising App (33-C) | `Views/Labour/ArisingApplication.cshtml`| `LabourController.SaveArisingApp`| `ArisingApplicationViewModel`| `ICaseActivityLogger`| `LabourRepository`| `LABOUR_ARISING_APPLICATIONS`| `SecurityRemediationValidationTests` |
| **GRA-01** | Gratuity Reg | `Views/Gratuity/Create.cshtml` | `GratuityController.Create` (POST) | `GratuityCaseViewModel`| None | `GratuityRepository` | `GRA_CASES` | `GratuityCalculationTests` |
| **GRA-04** | 10% Interest Calc | `Views/Gratuity/AuditCalculation.cshtml`| `GratuityController.AuditCalculation`| `GratuityAuditViewModel`| `GratuityCalculator` | `GratuityRepository`| `GRA_CASES, tbl_Gratuity_Interest`| `GratuityCalculationTests` |
| **EP-01** | Execution Petition | `Views/EP/Create.cshtml` | `EPController.Create` (POST) | `EPViewModel` | `ICaseActivityLogger` | `CaseRepository` | `EP_DETAILS` | `SecurityRemediationValidationTests` |
| **EP-02** | Labour EP Petitions| `Views/LabourEP/Create.cshtml` | `LabourEPController.Create` (POST) | `LabourEPViewModel` | `ICaseActivityLogger` | `LabourRepository` | `LABOUR_EP_DETAILS` | `SecurityRemediationValidationTests` |
| **BILL-01**| Petty Bill Submit | `Views/Report/PettyBill.cshtml` | `ReportController.SavePettyBill` (POST)| `PettyBillViewModel` | `NyayaPathaPettyBillRAG`| `PettyBillRepository`| `PETTY_BILL_PAYMENTS` | `PettyBillSerializationTests` |
| **BILL-02**| Print Bill Voucher | `Views/Report/PrintBill.cshtml` | `ReportController.PrintBill` (GET) | `PettyBillViewModel` | None | `PettyBillRepository`| `PETTY_BILL_PAYMENTS, MVC_CASES` | `SecurityRemediationValidationTests` |
| **EC-01** | eCourts Sync Queue | `Views/Case/Details.cshtml` | `ECourtsController.QueueCaseSync` (POST)| `CaseSyncRequestDto` | `NapixSyncEngine` | `ECourtsRepository` | `NAPIX_SYNC_QUEUE` | `NapixDirectPipelineTests` |
| **AI-01** | Nyaya Patha Query | `Views/NyayaPatha/Index.cshtml` | `NyayaPathaController.Ask` (POST) | `NyayaPathaQueryModel` | `CaseContextBuilder, Ollama`| `NyayaPathaRepository`| Vector Precedents, Active Cases | `NyayaPathaMaster100EvaluationSuiteTests`|
| **FILE-01**| Physical File Stream| Direct Attachment Link | `UploadsController.Download` (GET) | None | `IWebHostEnvironment` | Multi-Domain Query | `MVC, LABOUR, GRA, OTHER, APPEAL` | `SecurityRemediationValidationTests` |
