# 📋 NWKRTC Law Management System — Page & Form Control Inventory

**Document Reference**: Field-Level Inventory Across All Razor Views, Modals & Partial Views  
**Total Form Controls Cataloged**: **3,874 Controls** across 106 HTML `<form>` tags  
**Machine-Readable Companion**: [`FIELD-CATALOG.csv`](file:///c:/Users/adts-/Desktop/Law%20Project/docs/system-map/FIELD-CATALOG.csv)  
**Verification Level**: **VERIFIED IN SOURCE**  

---

## 1. Inventory Summary by Control Type

| Control Type | HTML Tag | Total Elements | Primary Purpose & Usage |
| :--- | :---: | :---: | :--- |
| **Input Fields** | `<input>` | **2,541** | Text (names, numbers, CNR), numbers (amounts, percentages), dates (accident, hearing, order), hidden (IDs, CSRF), files (PDFs). |
| **Select Dropdowns** | `<select>` | **383** | Case types, court names, advocates, stages, divisions, depots, feasibility recommendations, review decisions. |
| **Text Areas** | `<textarea>` | **182** | Legal facts, petitioner contentions, officer opinions, tribunal award summaries, settlement terms, grounds of appeal. |
| **Buttons & Triggers**| `<button>` | **861** | Submit buttons, modal popups, dynamic row additions, export to Excel/PDF, print commands, sync triggers. |

---

## 2. Key Page-Level Form Inventories

### 2.1 MVC / MACT New Registration Form (`Views/Case/Create.cshtml`)
- **Primary Form**: `<form asp-action="Create" method="post" enctype="multipart/form-data">`
- **Fields Cataloged**:
  - `CaseNumber` (`<input type="text" required>`): Format: `MVC 123/2024` or tribunal case register identifier.
  - `CNRNumber` (`<input type="text">`): 16-character alphanumeric e-Courts Case Number Record.
  - `CourtID` (`<select required>`): Dropdown populated from `COURT_NAMES` filtered by division.
  - `AccidentDate` (`<input type="date" required>`): Date on which the motor vehicle collision occurred.
  - `VehicleNumber` (`<input type="text" required>`): NWKRTC bus registration (e.g., `KA-25-F-1234`).
  - `DepotID` (`<select required>`): Operating depot under the logged-in division.
  - `DriverName` & `DriverTokenNo` (`<input type="text">`): Corporation bus driver particulars.
  - `ClaimAmount` (`<input type="number" step="0.01">`): Total monetary claim demanded by petitioners.
  - `PetitionerNames` (`<input type="text" required>`): Primary claimant / injured party names.
  - `AdvocateID` (`<select>`): Assigned Corporation panel advocate.
  - `AttachedFiles` (`<input type="file" multiple accept=".pdf,.jpg,.jpeg">`): FIR, spot sketch, vehicle inspection report.
  - `SubmitButton` (`<button type="submit" class="btn btn-primary">`): Form submit trigger.

---

### 2.2 Adverse Award & Feasibility Form (`Views/Case/_AdverseDetailsModal.cshtml`)
- **Primary Form**: `<form asp-action="SaveAdverseDetails" method="post" enctype="multipart/form-data">`
- **Fields Cataloged**:
  - `CaseID` (`<input type="hidden">`): Parent case identifier.
  - `AwardDate` (`<input type="date" required>`): Date on which MACT pronounced judgment.
  - `TotalCompensation` (`<input type="number" step="0.01" required>`): Total amount awarded by tribunal.
  - `CorpLiabilityPercent` (`<input type="number" step="1" min="0" max="100">`): NWKRTC liability share (e.g., 50% or 100%).
  - `InterestRate` (`<input type="number" step="0.1">`): Tribunal awarded interest rate (e.g., 6.0% or 9.0%).
  - `IsAppealFeasible` (`<select required>`): Options: `Yes`, `No`, `Conditional`.
  - `FeasibilityRemarks` (`<textarea>`): Legal justification from panel advocate regarding grounds for High Court MFA.
  - `AwardCopyFile` (`<input type="file" accept=".pdf">`): Official certified judgment copy.

---

### 2.3 High Court Appeal Management (`Views/Appeal/Manage.cshtml`)
- **Primary Form**: `<form asp-action="Save" method="post" enctype="multipart/form-data">`
- **Fields Cataloged**:
  - `AppealID` & `CaseID` (`<input type="hidden">`): Entity keys.
  - `AppealType` (`<select required>`): Options: `Corporation MFA`, `Claimant MFA`, `Cross Objection`, `SLP`.
  - `AppealNumber` (`<input type="text">`): High Court filing number (e.g., `MFA 100234/2024`).
  - `HighCourtBench` (`<select required>`): Options: `Dharwad Bench`, `Principal Bench Bengaluru`, `Kalaburagi Bench`.
  - `AdvocateID` (`<select required>`): High Court panel advocate from `HC_ADVOCATES`.
  - `StayOrderStatus` (`<select>`): Options: `Stay Granted`, `Conditional Stay (Deposit 50%)`, `Stay Refused`, `Pending`.
  - `DepositedAmount` (`<input type="number" step="0.01">`): Statutory 50% deposit amount made before High Court.
  - `JudgmentFile` (`<input type="file" accept=".pdf">`): High Court order copy.

---

### 2.4 Gratuity Audit Calculation Form (`Views/Gratuity/AuditCalculation.cshtml`)
- **Primary Form**: `<form asp-action="SaveAuditCalculation" method="post">`
- **Fields Cataloged**:
  - `GratuityID` (`<input type="hidden">`): Gratuity case foreign key.
  - `PrincipalAmount` (`<input type="number" step="0.01" required>`): Basic gratuity awarded by Controlling Authority.
  - `DueDate` (`<input type="date" required>`): 30 days post-retirement/resignation date.
  - `DepositDate` (`<input type="date" required>`): Date NWKRTC deposited amount with authority.
  - `CalculatedDays` (`<input type="text" readonly>`): Computed elapsed days (`(DepositDate - DueDate).Days`).
  - `StatutoryInterest` (`<input type="text" readonly>`): Auto-computed at statutory 10% per annum.
  - `StatutoryDeductions` (`<input type="number" step="0.01">`): Penal deductions, damage recovery, audit recoveries.
  - `PreviousDisbursements` (`<input type="number" step="0.01">`): Earlier partial disbursements.
  - `NetPayable` (`<input type="text" readonly>`): Total final balance payable to employee.

*(See full machine-readable details of all 3,874 controls in `docs/system-map/FIELD-CATALOG.csv`)*
