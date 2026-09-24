# 📑 Law Project — Modules & Features Operational Guide

> **Enterprise Scope**: Motor Vehicle Claims (MVC/MACT), Labour Court & Service Matters, Payment of Gratuity Act, Appeals, Execution Petitions (EP), Petty Bills, and Notification Engine.

---

## 📌 1. Motor Vehicle Claims (MVC / MACT) Module

The **MVC Module** manages legal claims filed against the corporation under the Motor Vehicles Act before Motor Accident Claims Tribunals (MACT).

```
[Accident Event] ➔ [MVC Case Entry] ➔ [Witness Tracking (PW/RW)] ➔ [MACT Award / Judgment]
                                                                        |
                                         +------------------------------+------------------------------+
                                         |                                                             |
                                         v                                                             v
                              [High Court Appeal (MFA)]                                     [Execution Petition (EP)]
                                         |                                                             |
                                         v                                                             v
                              [Supreme Court SLP]                                           [Award Recovery & Deposit]
```

### 1.1 Key Features & Workflows
- **Case Registration**: Capture accident date, vehicle registration number, driver name, route details, tribunal court (`MACT_MASTER`), claimant details, and claimed compensation amount.
- **Witness Tracking**: Maintain petitioner witnesses (PW) and respondent witnesses (RW), cross-examination dates, and adverse remarks.
- **Award & Judgment Recording**: Log award amounts, liability percentages (corporation vs. third party), interest rates, and legal costs awarded by the tribunal.
- **High Court Appeals (MFA)**: Link MFA appeal records directly to parent MVC cases. Track filing deadlines, stay order applications, stay expiry dates, and High Court advocate entrustments.
- **Execution Petitions (EP)**: Monitor Execution Petitions filed by claimants for award execution. Track attachment notices, bank deposit compliance, and stays.
- **Inter-Division Transfers**: Transfer case jurisdiction when depot boundaries change, maintaining a complete transfer audit log.

---

## ⚖️ 2. Labour Disputes & Service Matters Module

The **Labour Module** governs Industrial Disputes (ID), Labour Court Applications (LCA), Serial Applications, and Direct High Court Service Matters (Writ Petitions - WP).

### 2.1 Sub-Modules & Classification

| Sub-Module | Jurisdiction | Description | Key Attributes |
| :--- | :--- | :--- | :--- |
| **Labour Court Cases** | Labour Court / Industrial Tribunal | Industrial Disputes (ID), Section 33-C(2) LCAs, and misconduct disputes. | Applicant Name, Entrustment No, Labour Advocate, Section, Award Status. |
| **Service Matters** | High Court (Direct Writ Petitions) | Direct Writ Petitions (WP) filed by employees or corporation against tribunal orders. | WP No, High Court Bench, High Court Advocate, Interim Stays, Disposal Result. |
| **Arising Applications** | Labour / High Court | Interlocutory applications arising from parent labour cases. | Parent Case ID, Application Type, Relief Sought, Order Date. |
| **Labour EP Details** | Labour Court | Execution Petitions for reinstatement or back-wage recovery. | EP No, Compliance Target Date, Back-wage Amount, Deposit Status. |

### 2.2 Corporate Action Approval Workflow (CO / CLO / MD)

Service Matters and Labour Court cases undergo hierarchical legal evaluation across three corporate approval levels:

```
+--------------------------+          +--------------------------+          +--------------------------+
|  Central Office (CO)     | ───────> |  Chief Law Officer (CLO) | ───────> |  Managing Director (MD)  |
|  - Case Scrutiny         |          - Legal Opinion Scrutiny   |          - Final Financial & Writ   |
|  - Initial Recommendation|          - Writ Appeal Advisability |            Appeal Sanction          |
+--------------------------+          +--------------------------+          +--------------------------+
```

1. **Central Office Review**: Verifies factual correctness, depot reports, and lower court judgment copies.
2. **CLO Legal Recommendation**: Evaluates advisability of filing Writ Appeals (WA) or complying with Labour Court reinstatement awards.
3. **MD Final Approval**: Authorizes high-value back-wage payments or Supreme Court SLP filings.

---

## 💰 3. Payment of Gratuity Act & Petty Bills Module

The **Gratuity & Petty Bills Module** manages statutory employee gratuity disputes, interest calculations, and legal expense vouchers.

### 3.1 Gratuity Claim Lifecycle
1. **Controlling Authority (CA) Proceedings**: Log claim details, employee designation, length of service, last drawn wages, and claimed gratuity.
2. **Order & Interest Calculation Engine**:
   - Computes statutory interest under the Payment of Gratuity Act (typically 10% per annum for delayed payments).
   - Computes compound/simple interest across delay periods automatically.
3. **Appellate Authority (AA) Appeals**: Manage statutory deposit prerequisites (100% deposit before CA) and appellate outcome tracking.

### 3.2 Petty Bills & Advocate Fee Audit System
- **Petty Bill Entry**: Log court fees, advocate fees, clerkage, typing charges, and certified copy expenses.
- **Previous Payments Audit**: Scans past voucher disbursements for the same case and advocate to eliminate duplicate fee payments.
- **Disincentive Penalty Rules**: Applies automatic fee disincentives if an advocate fails to file stay applications or misses mandatory compliance timelines.

---

## 🔔 4. Notification Engine & Background Alerts

The system includes an automated background notification subsystem (`NotificationBackgroundService`):

```
+-----------------------------------------------------------------------------------+
|                        NotificationBackgroundService                              |
|                          (Runs Daily Background Job)                              |
+-----------------------------------------------------------------------------------+
                                          |
          +-------------------------------+-------------------------------+
          |                               |                               |
          v                               v                               v
[Hearing Reminders]            [Stay Expiry Alerts]          [Appeal Expiration Window]
- 1, 3, & 7 days lead time     - 15 days prior to stay       - 90-day High Court limitation
- Dispatches SMS via SMSService  order expiration date         window warnings
```

- **SMS Gateway (`SMSService`)**: Sends automated SMS alerts directly to assigned advocates and legal officers.
- **Compliance Dashboards**: Displays color-coded alert banners on user dashboards for overdue actions.

---

## 📊 5. Reports & Analytics Dashboard

### 5.1 Dashboard KPIs
- **Pending vs. Disposed Summary**: Statewide and division-wise breakdown of active litigation.
- **Financial Exposure Matrix**: Total claim amount vs. actual award amount vs. saved corporation funds.
- **Advocate Performance Index**: Case success rates, disposal velocity, and pending case load per advocate.

### 5.2 Standard Reports
- **Division Monthly Summary**: Tabular export of all active MVC, Labour, and Gratuity cases for monthly review meetings.
- **High Court Pending Appeals**: Summary of pending MFAs and Writ Petitions categorized by bench (Bengaluru, Dharwad, Kalaburagi).
- **Upcoming Hearings Report**: Filterable hearing schedule by date range, court, and division.
