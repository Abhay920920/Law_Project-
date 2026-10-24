# 🗄️ NWKRTC Law Management System — Database & Entity Relationship Map

**Document Reference**: Relational Database Architecture & Schema Specification  
**Target Engine**: Microsoft SQL Server (2019+ / Azure SQL / LocalDB)  
**Database Name**: `Admin_Law`  
**Total Unique Tables**: **82 Unique Tables**  
**Source Baseline**: [`00_Master_Full_Database_Schema_And_Seed.sql`](file:///c:/Users/adts-/Desktop/Law%20Project/Law%20Project/CaseManagement/SQL/00_Master_Full_Database_Schema_And_Seed.sql)  
**Verification Level**: **VERIFIED IN SOURCE**  

---

## 1. Core Schema Groups & Domain Tables

The 82 tables are organized into 8 functional domain clusters:

### 1.1 Motor Vehicle Accident Claims (MVC / MACT)
- `[dbo].[MVC_CASES]`: Master record of all accident claims (CaseNumber, CNRNumber, AccidentDate, VehicleNumber, DivisionID, CourtID, CaseStatus, NextHearingDate).
- `[dbo].[MVC_PETITIONER_DETAILS]`: Claimants, deceased/injured details, age, occupation, monthly income.
- `[dbo].[MVC_CASE_HEARING_DETAILS]`: Daily hearing history, court business, daily orders, next hearing purpose.
- `[dbo].[MVC_CASE_ADVERSE_DETAILS]`: Tribunal awards, total compensation, liability percentage, interest rate, feasibility opinion.
- `[dbo].[MVC_THIRD_PARTY_CASES]`: Claims involving other vehicles, private buses, two-wheelers, and insurance companies.
- `[dbo].[APPEAL_DETAILS]`: High Court MFA, Writ, and SLP appeals (AppealNumber, Bench, AdvocateID, StayOrderStatus).
- `[dbo].[EP_DETAILS]`: Execution petitions filed by claimants to recover compensation via court attachment warrants.
- `[dbo].[PETTY_BILL_PAYMENTS]`: Advocate legal fee vouchers, court process charges, typing and spot inspection disbursements.

### 1.2 Labour Disputes & Industrial Relations
- `[dbo].[LABOUR_CASES]`: Industrial disputes under Industrial Disputes Act 1947 (ID, KID, Ref No, WorkmanName, DivisionID, Stage, Status).
- `[dbo].[LABOUR_PETITIONER_DETAILS]`: Workman particulars, designation, depot, date of appointment, date of dismissal.
- `[dbo].[LABOUR_HEARINGS]`: Proceedings before Labour Courts and Industrial Tribunals.
- `[dbo].[LABOUR_CASE_NOTINGS]`: Legal officer opinions, DyCLO endorsements, CLO recommendations, MD approval records.
- `[dbo].[LABOUR_ARISING_APPLICATIONS]`: Section 33-C(2) monetary claim recovery applications.
- `[dbo].[tbl_Arising_Application_Payments]`: Installment disbursements under 33-C(2) orders.
- `[dbo].[LABOUR_EP_DETAILS]`: Execution petitions arising from Labour Court awards.
- `[dbo].[SERVICE_MATTERS]`: Writ petitions and civil disputes concerning seniority, promotions, and disciplinary inquiries.
- `[dbo].[CCC_CASES]`: Civil Contempt of Court proceedings filed under the Contempt of Courts Act 1971.
- `[dbo].[REVIEW_PETITIONS]`: High Court review petitions seeking review of adverse orders.

### 1.3 Gratuity Claims
- `[dbo].[GRA_CASES]`: Claims under Payment of Gratuity Act 1972 before Controlling Authority (ClaimNumber, EmployeeName, PrincipalAmount).
- `[dbo].[tbl_Gratuity_Interest]`: Statutory 10% per annum simple interest audit computations (CalculatedDays, InterestAmount, NetPayable).
- `[dbo].[tbl_Gratuity_Payments]`: Disbursement vouchers paid to retired/deceased employees.

### 1.4 Other Courts & Civil Disputes
- `[dbo].[OTHER_CASES]`: Original Suits (O.S.), Employees' Compensation Act (ECA), Consumer Forum, and Land Acquisition Claims (LAC).

### 1.5 e-Courts NAPIX Integration
- `[dbo].[NAPIX_SYNC_QUEUE]`: Persistent FIFO queue for case sync requests (Module, CaseID, CNRNumber, Priority, Status, NextAttempt).
- `[dbo].[NAPIX_API_CALLS]`: Telemetry log recording external API calls (Endpoint, LatencyMs, StatusCode, ResponseSize, ErrorMessage).
- `[dbo].[NAPIX_API_QUOTA]`: Hourly quota tracking table (WindowStart, CallCount, QuotaLimit).
- `[dbo].[CAUSE_LIST_DETAILS]`: Daily High Court and District Court cause list board records.

### 1.6 Audit, Security & User Administration
- `[dbo].[USERS]`: User account credentials, PBKDF2 password hashes, role assignments, division linkages.
- `[dbo].[DIVISIONS]`: NWKRTC operating divisions (DivisionID, DivisionName, DivisionCode).
- `[dbo].[COURT_NAMES]`: Court master register (CourtID, CourtName, DivisionID, CourtType).
- `[dbo].[ADVOCATE_NAMES]`: Corporation panel advocates (AdvocateID, AdvocateName, EnrolmentNumber, MobileNumber).
- `[dbo].[HC_ADVOCATES]`: High Court panel advocates grouped by bench (Bengaluru, Dharwad, Kalaburagi).
- `[dbo].[tbl_Case_Activity_Logs]`: Forensic audit log with actor username, timestamp, action type, IP address, and SendARP physical MAC.
- `[dbo].[User_OTP_Verifications]`: One-time password tokens for secure mobile authentication.

---

## 2. Master Table ER Reference

```
[USERS] 1 ---> N [MVC_CASES]
[DIVISIONS] 1 ---> N [USERS]
[DIVISIONS] 1 ---> N [MVC_CASES]

[MVC_CASES] 1 ---> N [MVC_PETITIONER_DETAILS] (PK: CaseID)
[MVC_CASES] 1 ---> N [MVC_CASE_HEARING_DETAILS] (PK: CaseID)
[MVC_CASES] 1 ---> 1 [MVC_CASE_ADVERSE_DETAILS] (PK: CaseID)
[MVC_CASES] 1 ---> N [APPEAL_DETAILS] (PK: CaseID)
[MVC_CASES] 1 ---> N [EP_DETAILS] (PK: CaseID)
[MVC_CASES] 1 ---> N [PETTY_BILL_PAYMENTS] (PK: CaseID)
[MVC_CASES] 1 ---> N [tbl_Case_Activity_Logs] (PK: CaseID)

[LABOUR_CASES] 1 ---> N [LABOUR_PETITIONER_DETAILS] (PK: LabourCaseID)
[LABOUR_CASES] 1 ---> N [LABOUR_HEARINGS] (PK: LabourCaseID)
[LABOUR_CASES] 1 ---> N [LABOUR_CASE_NOTINGS] (PK: LabourCaseID)
[LABOUR_CASES] 1 ---> N [LABOUR_ARISING_APPLICATIONS] (PK: LabourCaseID)
[LABOUR_CASES] 1 ---> N [LABOUR_EP_DETAILS] (PK: LabourCaseID)

[GRA_CASES] 1 ---> N [tbl_Gratuity_Interest] (PK: GratuityID)
[GRA_CASES] 1 ---> N [tbl_Gratuity_Payments] (PK: GratuityID)

[NAPIX_SYNC_QUEUE] 1 ---> N [NAPIX_API_CALLS] (FK: QueueID)
```
*(See visual diagram in `docs/system-map/diagrams/09-database-erd.mmd`)*
