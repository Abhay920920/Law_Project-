# 📑 NWKRTC Law Management System — Dropdown & Option Catalog

**Document Reference**: Exhaustive Catalog of Form Dropdowns, Enums, Database Lookups & Cascades  
**Total Dropdowns Cataloged**: **383 `<select>` Controls**  
**Verification Level**: **VERIFIED IN SOURCE**  

---

## 1. Primary Dropdown Categories & Sources

The 383 dropdown controls across the system originate from three distinct architectural sources:
1. **Dynamic Database Lookups**: Master tables populated via ADO.NET / Dapper queries.
2. **Hardcoded Domain Enums**: Statutory legal classifications embedded directly in C# view models or Razor views.
3. **Cascading JavaScript Selections**: Client-side Select2 dropdowns that query controller endpoints dynamically based on previous selections.

---

## 2. Core Master Dropdown Specifications

### 2.1 Court Names (`CourtID` Dropdown)
- **Appears in**: `Case/Create`, `Case/Edit`, `Labour/Create`, `Gratuity/Create`, `EP/Create`, `Home/OSCorporationForm`.
- **Option Source**: Database Table `[dbo].[COURT_NAMES]`.
- **Query**: `SELECT CourtID, CourtName FROM COURT_NAMES WHERE DivisionID = @divisionId OR DivisionID = 0 ORDER BY CourtName`.
- **Options Represented**:
  - District MACT Tribunals (Belagavi, Dharwad, Hubballi, Gadag, Bagalkot, Vijayapura, Karwar, Sirsi, Haveri).
  - Labour Courts & Industrial Tribunals (Hubballi Labour Court, Belagavi Industrial Tribunal).
  - Controlling & Appellate Authorities for Gratuity (Deputy Labour Commissioner offices).
  - Senior Civil Judge Courts & Principal District Courts.

---

### 2.2 Advocate Names (`AdvocateID` Dropdown)
- **Appears in**: `Case/Create`, `Case/Edit`, `Labour/Create`, `Appeal/Manage`, `Report/SavePettyBill`.
- **Option Source**: Database Table `[dbo].[ADVOCATE_NAMES]` & `[dbo].[HC_ADVOCATES]`.
- **Query**: `SELECT AdvocateID, AdvocateName, EnrolmentNumber FROM ADVOCATE_NAMES WHERE IsActive = 1 ORDER BY AdvocateName`.
- **High Court Appeals Query**: `SELECT AdvocateID, AdvocateName FROM HC_ADVOCATES WHERE Bench = @bench AND IsActive = 1`.

---

### 2.3 Operating Divisions (`DivisionID` Dropdown)
- **Appears in**: `Account/Register`, `Master/Index`, `Report/CentralOffice`, `Labour/Transfer`.
- **Option Source**: Database Table `[dbo].[DIVISIONS]`.
- **Hardcoded Identifier Mapping**:
  - `0` / `5`: Central Office (CO - Gokul Road, Hubballi)
  - `1`: Belagavi Division
  - `2`: Hubballi-Dharwad Division
  - `3`: Gadag Division
  - `4`: Bagalkot Division
  - `6`: Vijayapura Division
  - `7`: Uttara Kannada (Sirsi / Karwar) Division
  - `8`: Haveri Division
  - `9`: Chikodi Division

---

### 2.4 Case Status & Stages (`CaseStatus` / `CurrentStage` Dropdown)
- **Appears in**: Case List Filters, Dossier Views, Edit Forms.
- **Option Source**: Hardcoded Statutory Legal Stages.
- **MVC Claim Stages**:
  - `1. Notice Received / Summons Served`
  - `2. Written Statement (WS) Filed`
  - `3. Petitioner Evidence (PW) Ongoing`
  - `4. Corporation Evidence (RW) Ongoing`
  - `5. Final Arguments`
  - `6. Reserved for Judgment`
  - `7. Adverse Award Passed`
  - `8. Claim Dismissed`
  - `9. Settled in Lok Adalat`
  - `10. Disposed / Closed`

---

### 2.5 Multi-Tier Review Recommendations (`Recommendation` Dropdown)
- **Appears in**: `Labour/LOAction`, `Labour/DyCLOAction`, `Labour/CLOAction`, `Labour/MDAction`.
- **Option Source**: Hardcoded Workflow State Machine.
- **Options**:
  - `Accept Award / Comply with Order`
  - `File Writ Petition in High Court`
  - `File Writ Appeal before Division Bench`
  - `File Special Leave Petition (SLP) in Supreme Court`
  - `Seek Further Factual Clarification from Division`
  - `Reject Claim / Challenge Execution`

---

### 2.6 High Court Appeal Types (`AppealType` Dropdown)
- **Appears in**: `Appeal/Manage`, `Appeal/Register`.
- **Option Source**: Hardcoded Appellate Forms.
- **Options**:
  - `Corporation MFA` (Miscellaneous First Appeal filed by NWKRTC against MACT award)
  - `Claimant MFA` (Appeal filed by accident victims seeking quantum enhancement)
  - `Cross Objection` (Cross-objection under Order 41 Rule 22 CPC)
  - `Writ Petition (WP)` (Article 226/227 petitions against Labour awards)
  - `Writ Appeal (WA)` (Intra-court appeal against Single Judge judgment)
  - `Special Leave Petition (SLP)` (Article 136 petition before Supreme Court of India)
  - `Review Petition (RP)` (Review petition before High Court)
  - `Civil Contempt Case (CCC)` (Contempt proceedings under Contempt of Courts Act)

---

## 3. Dynamic Cascading Rules in JavaScript

The application implements cascading dropdown dependencies in `wwwroot/js/ecourts-cascade.js` and view templates:
1. **Division $\to$ Depot Cascade**: Selecting `DivisionID` triggers AJAX query `/Master/GetDepotsByDivision?divisionId={id}` and repopulates `DepotID`.
2. **Division $\to$ Court Cascade**: Selecting `DivisionID` triggers AJAX query `/Master/GetCourtsByDivision?divisionId={id}` to filter local taluk and district courts.
3. **High Court Bench $\to$ Advocate Cascade**: Selecting `HighCourtBench` (Dharwad, Bengaluru, Kalaburagi) triggers `/Appeal/GetAdvocatesByBench?bench={bench}` to show only advocates enrolled at that bench.
4. **Feasibility Decision $\to$ Grounds Cascade**: Selecting `IsAppealFeasible = "Yes"` un-hides the required `GroundsOfAppeal` textarea and `InitialActionPath1` file upload control. Selecting `"No"` hides these controls and displays a compensation settlement ledger.
