# 🛣️ NWKRTC Law Management System — Route & Controller Action Registry

**Document Reference**: Exhaustive Inventory of Controller Actions, HTTP Verbs & Endpoints  
**Total Actions Cataloged**: **342 Actions** (338 in `MVCCaseManagement` + 4 in `NapixEcourtsApi`)  
**Machine-Readable Companion**: [`ROUTE-CATALOG.csv`](file:///c:/Users/adts-/Desktop/Law%20Project/docs/system-map/ROUTE-CATALOG.csv)  
**Verification Level**: **VERIFIED IN SOURCE**  

---

## 1. Controller Action Distribution

| Controller | Actions | Primary Domain / Responsibilities |
| :--- | :---: | :--- |
| **`LabourController`** | **78** | Industrial disputes, hearings, multi-tier review (LO/DyCLO/CLO/MD), Arising Apps (33-C(2)), Writ Petitions, CCC. |
| **`MasterController`** | **51** | Administrative management of Courts, Advocates, Divisions, Case Types, and System Configuration. |
| **`ECourtsController`** | **39** | NAPIX synchronization, automated sync queues, cause list downloads, CNR linking, and batch processing. |
| **`CaseController`** | **30** | MVC/MACT claims lifecycle, registrations, hearings, adverse awards, third-party matters, and remand backs. |
| **`HomeController`** | **26** | Central portal dashboard, hearing calendars, critical delay monitors, and Other Courts gateways (OS, ECA). |
| **`AppealController`** | **20** | High Court appeals (MFA, Writ), Supreme Court SLP, legal opinions, and advocate engagement. |
| **`GratuityController`** | **19** | Payment of Gratuity Act claims, Controlling Authority awards, Appellate Authority appeals, 10% interest. |
| **`NyayaPathaController`**| **14** | Air-gapped AI chat, RAG query streaming, context retrieval, citation verification, and petty bill auto-audit. |
| **`ReportController`** | **11** | Petty bill creation and voucher printing, division and consolidated monthly MIS statements (MMR-ST2). |
| **`AccountController`** | **9** | Officer authentication, cookie claims hydration, password changes, and Mobile-1 SMS OTP verification. |
| **`LabourEPController`** | **8** | Labour Execution Petitions, liability calculations, and court installment ledgers. |
| **`AuditController`** | **6** | Financial audit records, compensation ledger tracking, and payment reconciliations. |
| **`JudgementController`** | **6** | Legal precedent repository, circular uploads, and judicial order indexing. |
| **`EPController`** | **6** | Motor Vehicle execution petitions, attachment orders, and court deposit vouchers. |
| **`ApiDashboardController`**| **5** | NAPIX gateway monitoring, hourly quota consumption, latency metrics, and circuit breaker status. |
| **`NotificationController`**| **4** | Push notifications, unread notification counts, and officer reminder history. |
| **`ActivityLogController`**| **3** | Forensic case activity audit trails with SendARP layer-2 physical MAC and IP tracking. |
| **`DashboardController`** | **2** | Operational MACT dashboard statistics and upcoming hearing calendar. |
| **`UploadsController`** | **1** | Zero-trust physical file download streaming across 6 legal dispute domains. |
| **`CasesController`** *(Napix)* | **4** | Microservice endpoints for direct NAPIX encryption, signing, and raw NIC gateway queries. |

---

## 2. Representative Route Registry Sample

Below is an extract of key controller routes with their security, parameter, and authorization profiles (full catalog in `ROUTE-CATALOG.csv`):

| Route ID | Controller | Action | Verb | Route URL | Authorization | Anti-Forgery | Parameters |
| :--- | :--- | :--- | :---: | :--- | :--- | :---: | :--- |
| `RT-0001` | `AccountController` | `Login` | GET | `/Account/Login` | `[AllowAnonymous]` | No | `string returnUrl = null` |
| `RT-0002` | `AccountController` | `Login` | POST | `/Account/Login` | `[AllowAnonymous]` | `Validate` | `LoginViewModel model, string returnUrl` |
| `RT-0003` | `AccountController` | `Logout` | GET | `/Account/Logout` | `[Authorize]` | No | None |
| `RT-0010` | `CaseController` | `Create` | GET | `/Case/Create` | `[Authorize]` | No | None |
| `RT-0011` | `CaseController` | `Create` | POST | `/Case/Create` | `[Authorize]` | `Validate` | `CaseViewModel model, IFormFile[] files` |
| `RT-0015` | `CaseController` | `Edit` | GET | `/Case/Edit` | `[Authorize]` | No | `int id` (Division ownership enforced) |
| `RT-0016` | `CaseController` | `Edit` | POST | `/Case/Edit` | `[Authorize]` | `Validate` | `CaseViewModel model` |
| `RT-0022` | `CaseController` | `SaveHearing` | POST | `/Case/SaveHearing` | `[Authorize]` | `Validate` | `HearingViewModel model` |
| `RT-0023` | `CaseController` | `SaveAdverseDetails`| POST | `/Case/SaveAdverseDetails`| `[Authorize]`| `Validate`| `AdverseDetailsViewModel model` |
| `RT-0041` | `AppealController` | `Manage` | GET | `/Appeal/Manage` | `[Authorize]` | No | `int id` (Division ownership enforced) |
| `RT-0042` | `AppealController` | `Save` | POST | `/Appeal/Save` | `[Authorize]` | `Validate` | `AppealViewModel model` |
| `RT-0061` | `LabourController` | `Create` | GET | `/Labour/Create` | `[Authorize]` | No | None |
| `RT-0062` | `LabourController` | `Create` | POST | `/Labour/Create` | `[Authorize]` | `Validate` | `LabourCaseViewModel model` |
| `RT-0075` | `LabourController` | `LOAction` | POST | `/Labour/LOAction` | `[Authorize(Roles="LO,Admin")]` | `Validate` | `LegalActionViewModel model` |
| `RT-0076` | `LabourController` | `DyCLOAction` | POST | `/Labour/DyCLOAction`| `[Authorize(Roles="DyCLO,Admin")]`| `Validate`| `LegalActionViewModel model` |
| `RT-0077` | `LabourController` | `CLOAction` | POST | `/Labour/CLOAction` | `[Authorize(Roles="CLO,Admin")]` | `Validate` | `LegalActionViewModel model` |
| `RT-0078` | `LabourController` | `MDAction` | POST | `/Labour/MDAction` | `[Authorize(Roles="MD,Admin")]` | `Validate` | `LegalActionViewModel model` |
| `RT-0091` | `LabourController` | `DeleteArisingApplication`| POST| `/Labour/DeleteArisingApplication`| `[Authorize]`| `Validate`| `int id` (Division verified) |
| `RT-0139` | `GratuityController` | `Create` | POST | `/Gratuity/Create` | `[Authorize]` | `Validate` | `GratuityCaseViewModel model` |
| `RT-0145` | `GratuityController` | `AuditCalculation`| POST| `/Gratuity/AuditCalculation`| `[Authorize]`| `Validate`| `GratuityAuditViewModel model` |
| `RT-0158` | `EPController` | `Edit` | POST | `/EP/Edit` | `[Authorize]` | `Validate` | `EPViewModel model` (Locked division) |
| `RT-0164` | `LabourEPController` | `Edit` | POST | `/LabourEP/Edit` | `[Authorize]` | `Validate` | `LabourEPViewModel model` (Locked div) |
| `RT-0172` | `ReportController` | `SavePettyBill` | POST | `/Report/SavePettyBill` | `[Authorize]` | `Validate` | `PettyBillViewModel model` |
| `RT-0173` | `ReportController` | `PrintBill` | GET | `/Report/PrintBill` | `[Authorize]` | No | `int id` (Division verified) |
| `RT-0185` | `ECourtsController` | `QueueCaseSync` | POST | `/ECourts/QueueCaseSync` | `[Authorize]` | `Validate` | `int caseId, string cnrNumber, string module`|
| `RT-0224` | `NyayaPathaController` | `Ask` | POST | `/NyayaPatha/Ask` | `[Authorize]` | `Validate` | `NyayaPathaQueryModel model` |
| `RT-0338` | `UploadsController` | `Download` | GET | `/Uploads/{fileName}` | `[Authorize]` | No | `string fileName` (6-domain check) |

*(See full machine-readable details in `docs/system-map/ROUTE-CATALOG.csv`)*
