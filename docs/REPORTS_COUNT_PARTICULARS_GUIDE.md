# 📊 Central Office Reports — Complete Particulars & Field Criteria Guide

This document provides an exhaustive reference of all database tables, column names, SQL filter conditions, and calculation formulas used to compute every single **Particular** across all **Central Office Reports** in the NWKRTC Legal Case Management Platform.

---

## 📑 Table of Contents
1. [🚘 1. MVC Cases Monthly Statement](#1-mvc-cases-monthly-statement)
2. [⚖️ 2. Labour Cases Monthly Statement](#2-labour-cases-monthly-statement)
3. [💰 3. Gratuity Cases Monthly Statement](#3-gratuity-cases-monthly-statement)
4. [📁 4. Others / Consumer Forum / LAC Monthly Statement](#4-others--consumer-forum--lac-monthly-statement)
5. [🏛️ 5. High Court & Supreme Court Statement](#5-high-court--supreme-court-statement)
6. [📋 6. MMR-ST2 Consolidated Statement](#6-mmr-st2-consolidated-statement)

---

## 🚘 1. MVC Cases Monthly Statement

* **Primary Database Tables**: `MVC_CASES` (aliased `c`), `MVC_CASE_ADVERSE_DETAILS` (aliased `ad`), `PETTY_BILLS`, `PETTY_BILL_PAYMENTS`, `MVC_CASE_PAYMENTS`
* **Target Controller Action**: `ReportController.CentralOffice` (`module=MVC`)

| Particular No. | Particular Name | Target Table | Evaluated Database Columns & Filter Logic |
| :--- | :--- | :--- | :--- |
| **1** | **No. of Cases pending at the beginning of the month** | `MVC_CASES`, `MVC_CASE_ADVERSE_DETAILS` | • `c.EntrustmentDate < @StartDate` (fallback: `c.CreatedAt < @StartDate` if null)<br/>• AND (`ad.DisposedOnDate IS NULL` OR `ad.DisposedOnDate >= @StartDate`)<br/>• AND (`c.DisposalStatus IS NULL` OR `c.DisposalStatus <> 'CLOSED'` OR `c.UpdatedAt >= @StartDate`) |
| **2** | **No. of Cases Entrusted** | `MVC_CASES` | • (`c.EntrustmentDate >= @StartDate` AND `c.EntrustmentDate <= @EndDate`)<br/>• OR (`c.EntrustmentDate IS NULL` AND `c.CreatedAt >= @StartDate` AND `c.CreatedAt <= @EndDate`) |
| **3** | **No. of Cases disposed off in favour of NWKRTC** | `MVC_CASES`, `MVC_CASE_ADVERSE_DETAILS` | • (`ad.DisposedOnDate >= @StartDate` AND `ad.DisposedOnDate <= @EndDate`) OR (`ad.DisposedOnDate IS NULL` AND `c.DisposalStatus = 'DISPOSED'` AND `c.UpdatedAt >= @StartDate` AND `c.UpdatedAt <= @EndDate`)<br/>• AND `UPPER(c.DisposalResult)` IN (`'FAVOR'`, `'FAVOUR'`, `'DISMISSED'`, `'EX-PARTE'`, `'FAVOR (SETTLED)'`) |
| **4** | **No. of Cases disposed off against NWKRTC** | `MVC_CASES`, `MVC_CASE_ADVERSE_DETAILS` | • (`ad.DisposedOnDate >= @StartDate` AND `ad.DisposedOnDate <= @EndDate`) OR (`ad.DisposedOnDate IS NULL` AND `c.DisposalStatus = 'DISPOSED'` AND `c.UpdatedAt >= @StartDate` AND `c.UpdatedAt <= @EndDate`)<br/>• AND `UPPER(c.DisposalResult)` IN (`'AGAINST'`, `'ALLOWED'`, `'ADVERSE'`) |
| **5** | **No. of cases transferred** | `MVC_CASES`, `MVC_CASE_ADVERSE_DETAILS` | • (`ad.DisposedOnDate >= @StartDate` AND `ad.DisposedOnDate <= @EndDate`) OR (`c.UpdatedAt >= @StartDate` AND `c.UpdatedAt <= @EndDate`)<br/>• AND (`UPPER(c.DisposalResult) = 'TRANSFERRED'` OR `UPPER(c.DisposalStatus) = 'TRANSFERRED'`) |
| **6** | **No. of cases pending at the end of the month** | `MVC_CASES`, `MVC_CASE_ADVERSE_DETAILS` | • (`c.EntrustmentDate <= @EndDate` OR `c.CreatedAt <= @EndDate`)<br/>• AND (`ad.DisposedOnDate IS NULL` OR `ad.DisposedOnDate > @EndDate`)<br/>• AND (`c.DisposalStatus IS NULL` OR `c.DisposalStatus <> 'DISPOSED'` OR `c.UpdatedAt > @EndDate`) |
| **7** | **Compensation paid in MVC case for the month** | `PETTY_BILL_PAYMENTS`, `PETTY_BILLS` / `MVC_CASE_PAYMENTS` | • `SUM(p.Amount)` for cheque payments where `p.ChequeDate >= @StartDate AND p.ChequeDate <= @EndDate`<br/>• AND (`p.IsCancelled IS NULL` OR `p.IsCancelled = 0`) |
| **8** | **No. of cases Compensation paid in MVC case for the month** | `PETTY_BILLS`, `PETTY_BILL_PAYMENTS` | • `COUNT(DISTINCT b.CaseID)` for bills paid in the selected month (`p.ChequeDate >= @StartDate AND <= @EndDate`) |
| **9** | **Outstanding balance pending at Accounts** | `MVC_CASES`, `MVC_CASE_ADVERSE_DETAILS`, `PETTY_BILL_PAYMENTS` | • `SUM(ISNULL(ad.AwardAmount, 0) - ISNULL(PaidTotal, 0))` where `c.DisposalStatus <> 'CLOSED'` and `ad.AwardAmount > 0` across active cases |

---

## ⚖️ 2. Labour Cases Monthly Statement

* **Primary Database Tables**: `LABOUR_CASES` (aliased `c`), `LABOUR_EP_DETAILS` (aliased `ep`)
* **Target Controller Action**: `ReportController.CentralOffice` (`module=Labour`)
* **Optional Sub-Filters**: `caseType` (`ALL`, `KID`, `ID`, `ArisingApplication`)

| Particular No. | Particular Name | Target Table | Evaluated Database Columns & Filter Logic |
| :--- | :--- | :--- | :--- |
| **1** | **No. of Cases pending at the beginning of the month** | `LABOUR_CASES` | • (`c.EntrustmentDate < @StartDate` OR `c.CreatedDate < @StartDate`)<br/>• AND (`c.DisposalDate IS NULL` OR `c.DisposalDate >= @StartDate`)<br/>• AND (`c.CaseStatus IS NULL` OR `c.CaseStatus <> 'Disposed'` OR `c.ModifiedDate >= @StartDate`) |
| **2** | **No. of Cases Entrusted** | `LABOUR_CASES` | • (`c.EntrustmentDate >= @StartDate` AND `c.EntrustmentDate <= @EndDate`)<br/>• OR (`c.EntrustmentDate IS NULL` AND `c.CreatedDate >= @StartDate` AND `c.CreatedDate <= @EndDate`) |
| **3** | **No. of Cases disposed off in favour of NWKRTC** | `LABOUR_CASES` | • (`c.DisposalDate >= @StartDate` AND `c.DisposalDate <= @EndDate`) OR (`c.CaseStatus = 'Disposed'` AND `c.ModifiedDate >= @StartDate AND <= @EndDate`)<br/>• AND `UPPER(c.DisposalResult)` IN (`'FAVOR'`, `'FAVOUR'`, `'DISMISSED'`, `'EX-PARTE'`, `'PARTIALLY FAVOR'`) |
| **4** | **No. of cases disposed off against NWKRTC** | `LABOUR_CASES` | • (`c.DisposalDate >= @StartDate` AND `c.DisposalDate <= @EndDate`) OR (`c.CaseStatus = 'Disposed'` AND `c.ModifiedDate >= @StartDate AND <= @EndDate`)<br/>• AND `UPPER(c.DisposalResult)` IN (`'AGAINST'`, `'ALLOWED'`, `'ADVERSE'`) |
| **5** | **No. of cases received from other division** | `LABOUR_CASES` | • `c.TransferredFromDivisionID IS NOT NULL AND c.TransferredFromDivisionID > 0`<br/>• AND (`c.TransferDate >= @StartDate AND <= @EndDate` OR `c.CreatedDate >= @StartDate AND <= @EndDate`) |
| **6** | **No. of cases transfered to other division** | `LABOUR_CASES` | • (`c.DisposalDate >= @StartDate AND <= @EndDate` OR `c.ModifiedDate >= @StartDate AND <= @EndDate`)<br/>• AND (`UPPER(c.DisposalResult) = 'TRANSFERRED'` OR `UPPER(c.CaseStatus) = 'TRANSFERRED'`) |
| **7** | **No. of cases pending at the end of the month** | `LABOUR_CASES` | • (`c.EntrustmentDate <= @EndDate` OR `c.CreatedDate <= @EndDate`)<br/>• AND (`c.DisposalDate IS NULL` OR `c.DisposalDate > @EndDate`)<br/>• AND (`c.CaseStatus IS NULL` OR `c.CaseStatus <> 'Disposed'` OR `c.ModifiedDate > @EndDate`) |
| **8** | **Outstanding balance pending at Accounts In no. of cases** | `LABOUR_EP_DETAILS`, `LABOUR_CASES` | • `COUNT(DISTINCT ep.EPID)` where (`ep.IsSentToAccounts = 1` OR `c.SentToCO = 1`) and `ep.ComplianceStatus <> 'COMPLIED'`<br/>• Fallback: `COUNT(*)` where `(c.SentToCO = 1 OR c.IsEPFiled = 1)` and `c.CaseStatus <> 'Disposed'` |

---

## 💰 3. Gratuity Cases Monthly Statement

* **Primary Database Table**: `GRA_CASES` (aliased `c`)
* **Target Controller Action**: `ReportController.CentralOffice` (`module=Gratuity`)
* **Optional Sub-Filters**: `caseType` (`ALL`, `PGACR`, `PGAApplCR`)

| Particular No. | Particular Name | Target Table | Evaluated Database Columns & Filter Logic |
| :--- | :--- | :--- | :--- |
| **1** | **No. of Cases pending at the beginning of the month** | `GRA_CASES` | • (`c.EntrustmentDate < @StartDate` OR `c.CreatedDate < @StartDate`)<br/>• AND (`c.DisposalDate IS NULL` OR `c.DisposalDate >= @StartDate`)<br/>• AND (`c.CaseStatus IS NULL` OR `c.CaseStatus <> 'Disposed'` OR `c.ModifiedDate >= @StartDate`) |
| **2** | **No. of Cases Entrusted** | `GRA_CASES` | • (`c.EntrustmentDate >= @StartDate AND <= @EndDate`) OR (`c.CreatedDate >= @StartDate AND <= @EndDate`) |
| **3** | **No. of Cases disposed off in favour of NWKRTC** | `GRA_CASES` | • (`c.DisposalDate >= @StartDate AND <= @EndDate`) OR (`c.CaseStatus = 'Disposed'` AND `c.ModifiedDate >= @StartDate AND <= @EndDate`)<br/>• AND `UPPER(c.DisposalResult)` IN (`'FAVOR'`, `'FAVOUR'`, `'DISMISSED'`, `'EX-PARTE'`, `'FAVOR (SETTLED)'`) |
| **4** | **No. of cases disposed off against NWKRTC** | `GRA_CASES` | • (`c.DisposalDate >= @StartDate AND <= @EndDate`) OR (`c.CaseStatus = 'Disposed'` AND `c.ModifiedDate >= @StartDate AND <= @EndDate`)<br/>• AND `UPPER(c.DisposalResult)` IN (`'AGAINST'`, `'ALLOWED'`, `'ADVERSE'`) |
| **5** | **No. of cases received from other division** | `GRA_CASES` | • (`c.DisposalDate >= @StartDate AND <= @EndDate` OR `c.CreatedDate >= @StartDate AND <= @EndDate`)<br/>• AND (`UPPER(c.ForwardingStatus) LIKE '%RECEIVED%'` OR `UPPER(c.Remarks) LIKE '%RECEIVED%'`) |
| **6** | **No. of cases transfered to other division** | `GRA_CASES` | • (`c.DisposalDate >= @StartDate AND <= @EndDate` OR `c.ModifiedDate >= @StartDate AND <= @EndDate`)<br/>• AND (`UPPER(c.DisposalResult) = 'TRANSFERRED'` OR `UPPER(c.CaseStatus) = 'TRANSFERRED'`) |
| **7** | **No. of cases pending at the end of the month** | `GRA_CASES` | • (`c.EntrustmentDate <= @EndDate` OR `c.CreatedDate <= @EndDate`)<br/>• AND (`c.DisposalDate IS NULL` OR `c.DisposalDate > @EndDate`)<br/>• AND (`c.CaseStatus IS NULL` OR `c.CaseStatus <> 'Disposed'` OR `c.ModifiedDate > @EndDate`) |
| **8** | **Amount due in gratuity cases** | `GRA_CASES` | • `SUM(ISNULL(c.OrderedAmount_CA, ISNULL(c.FinalAmount, ISNULL(c.GratuityAmount_CA_Act, ISNULL(c.GratuityAmount_Corp_Act, 0)))))`<br/>• Where `c.ComplianceStatus IS NULL` OR `UPPER(c.ComplianceStatus) <> 'COMPLIED'` |

---

## 📁 4. Others / Consumer Forum / LAC Monthly Statement

* **Primary Database Table**: `OTHER_CASES` (aliased `c`)
* **Target Controller Action**: `ReportController.CentralOffice` (`module=Others`)
* **Optional Sub-Filters**: `caseType` (`ALL`, `OS`, `PSC`, `CC`, `ECA`, `LAC`, `Consumer`)

| Particular No. | Particular Name | Target Table | Evaluated Database Columns & Filter Logic |
| :--- | :--- | :--- | :--- |
| **1** | **No. of Cases pending at the beginning of the month** | `OTHER_CASES` | • (`c.EntrustmentDate < @StartDate` OR `c.CreatedDate < @StartDate`)<br/>• AND (`c.ClosureDate IS NULL` OR `c.ClosureDate >= @StartDate`)<br/>• AND (`c.CaseStatus IS NULL` OR `c.CaseStatus <> 'Disposed'` OR `c.DisposalStatus <> 'CLOSED AT DIVISION LEVEL'` OR `c.ModifiedDate >= @StartDate`) |
| **2** | **No. of Cases Entrusted** | `OTHER_CASES` | • (`c.EntrustmentDate >= @StartDate AND <= @EndDate`) OR (`c.CreatedDate >= @StartDate AND <= @EndDate`) |
| **3** | **No. of Cases disposed off in favour of NWKRTC** | `OTHER_CASES` | • (`c.ClosureDate >= @StartDate AND <= @EndDate`) OR (`c.ModifiedDate >= @StartDate AND <= @EndDate`)<br/>• AND `UPPER(c.Result)` IN (`'FAVOR'`, `'FAVOUR'`, `'DISMISSED'`, `'EX-PARTE'`, `'FAVOR (SETTLED)'`) |
| **4** | **No. of cases disposed off against NWKRTC** | `OTHER_CASES` | • (`c.ClosureDate >= @StartDate AND <= @EndDate`) OR (`c.ModifiedDate >= @StartDate AND <= @EndDate`)<br/>• AND `UPPER(c.Result)` IN (`'AGAINST'`, `'ALLOWED'`, `'ADVERSE'`) |
| **5** | **No. of cases pending at the end of the month** | `OTHER_CASES` | • (`c.EntrustmentDate <= @EndDate` OR `c.CreatedDate <= @EndDate`)<br/>• AND (`c.ClosureDate IS NULL` OR `c.ClosureDate > @EndDate`)<br/>• AND (`c.CaseStatus IS NULL` OR `c.CaseStatus <> 'Disposed'` OR `c.ModifiedDate > @EndDate`) |

---

## 🏛️ 5. High Court & Supreme Court Statement

* **Primary Database Tables**: `MVC_CASES`, `MVC_CASE_ADVERSE_DETAILS`, `LABOUR_CASES`
* **Target Controller Action**: `ReportController.CentralOffice` (`view=HighCourt`)

### 1. MFA CASES (MVC High Court Appeals)
* **Identification Filter**: `CorpMFANo IS NOT NULL` OR `ClaimantMFANo IS NOT NULL` OR `MFAEntrustmentNo IS NOT NULL` OR `UPPER(CurrentStatus) LIKE '%MFA%'`

| Row Letter | Particular Name | Evaluated Database Columns & Filter Logic |
| :--- | :--- | :--- |
| **a** | **No. of Cases Pending at beginning** | (`c.EntrustmentDate < @StartDate` OR `c.CreatedAt < @StartDate`) AND (`ad.DisposedOnDate IS NULL` OR `ad.DisposedOnDate >= @StartDate`) |
| **b** | **Entrusted by Corporation** | `c.CorpMFANo IS NOT NULL AND RTRIM(LTRIM(c.CorpMFANo)) <> ''` AND (`EntrustmentDate/CreatedAt` in selected month) |
| **c** | **Entrusted by Claimant** | `c.ClaimantMFANo IS NOT NULL AND RTRIM(LTRIM(c.ClaimantMFANo)) <> ''` AND (`EntrustmentDate/CreatedAt` in selected month) |
| **d** | **Total Disposed** | `ad.DisposedOnDate >= @StartDate AND ad.DisposedOnDate <= @EndDate` |
| **e** | **Disposed Favour Corporation** | `ad.DisposedOnDate` in month AND `UPPER(c.DisposalResult)` IN (`'FAVOR'`, `'FAVOUR'`, `'DISMISSED'`) |
| **f** | **Disposed Favour Claimant** | `ad.DisposedOnDate` in month AND `UPPER(c.DisposalResult)` IN (`'AGAINST'`, `'ALLOWED'`, `'ADVERSE'`) |
| **g** | **Ending Pending** | (`c.EntrustmentDate <= @EndDate` OR `c.CreatedAt <= @EndDate`) AND (`ad.DisposedOnDate IS NULL` OR `ad.DisposedOnDate > @EndDate`) |

### 2. LABOUR COURT CASES IN W.P. (Writ Petitions)
* **Identification Filter**: `UPPER(CaseType) LIKE '%WP%'` OR `IsArisingApplication = 0` OR `HighCourtBench IS NOT NULL`

| Row Letter | Particular Name | Evaluated Database Columns & Filter Logic |
| :--- | :--- | :--- |
| **a** | **No. of Cases Pending at beginning** | (`c.EntrustmentDate < @StartDate` OR `c.CreatedDate < @StartDate`) AND (`c.DisposalDate IS NULL` OR `c.DisposalDate >= @StartDate`) |
| **b** | **Entrusted by Corporation** | `UPPER(c.FiledBy) LIKE '%CORP%'` AND (`EntrustmentDate/CreatedDate` in selected month) |
| **c** | **Entrusted by Claimant** | `UPPER(c.FiledBy) LIKE '%CLAIMANT%'` AND (`EntrustmentDate/CreatedDate` in selected month) |
| **d** | **Total Disposed** | `c.DisposalDate >= @StartDate AND c.DisposalDate <= @EndDate` |
| **e** | **Disposed Favour Corporation** | `c.DisposalDate` in month AND `UPPER(c.DisposalResult)` IN (`'FAVOR'`, `'FAVOUR'`, `'DISMISSED'`) |
| **f** | **Disposed Favour Claimant** | `c.DisposalDate` in month AND `UPPER(c.DisposalResult)` IN (`'AGAINST'`, `'ALLOWED'`) |
| **g** | **Ending Pending** | (`c.EntrustmentDate <= @EndDate` OR `c.CreatedDate <= @EndDate`) AND (`c.DisposalDate IS NULL` OR `c.DisposalDate > @EndDate`) |

### 3. LABOUR COURT CASES IN W.A. (Writ Appeals)
* **Identification Filter**: `UPPER(CaseType) LIKE '%WA%'` OR `HighCourtBench = 'Division Bench'`
* Evaluates identical logic to W.P. table for rows **a** through **g**.

### 5. S.L.P. CASES (Supreme Court)
* **MVC S.L.P. Count**: `COUNT(*)` from `MVC_CASES` where `CorpSLPNo IS NOT NULL` OR `ClaimantSLPNo IS NOT NULL` OR `UPPER(CurrentStatus) LIKE '%SLP%'`
* **Labour S.L.P. Count**: `COUNT(*)` from `LABOUR_CASES` where `ClaimantSCNumber IS NOT NULL` OR `UPPER(CaseType) LIKE '%SLP%'`

---

## 📋 6. MMR-ST2 Consolidated Statement

* **Primary Database Tables**: `MVC_CASES`, `LABOUR_CASES`, `GRA_CASES`, `OTHER_CASES`
* **Target Controller Action**: `ReportController.CentralOffice` (`module=MMR-ST2`)

This report aggregates Supreme Court, High Court, and Other Courts case statistics across **all four legal modules** into a single consolidated table.

```
+-------------------------------------------------------------------------------------------------------------------------------+
|                                    CASES RECEIVED, DISPOSED OF AND PENDING DURING THE MONTH                                    |
+----+--------------------+---------------+------------------------+-------------+----------------------------------+---------------+
| Sl | Name of the Court  | Cases Pending | No. of cases entrusted | Total cases | Cases disposed during the month  | Cases Pending |
| No | wherein pending    | (Beginning)   | (During Month)         | (Col 3 + 4) | Favour  | Against | Total (6+7)  | End (Col 5-8) |
+----+--------------------+---------------+------------------------+-------------+---------+---------+--------------+---------------+
| 1  | SUPREME COURT      | [Sum of SC]   | [Sum of SC]            | [Calc]      | [Sum]   | [Sum]   | [Calc]       | [Calc]        |
| 2  | HIGH COURT         | [Sum of HC]   | [Sum of HC]            | [Calc]      | [Sum]   | [Sum]   | [Calc]       | [Calc]        |
| 3  | OTHER COURTS       | [Sum of OC]   | [Sum of OC]            | [Calc]      | [Sum]   | [Sum]   | [Calc]       | [Calc]        |
+----+--------------------+---------------+------------------------+-------------+---------+---------+--------------+---------------+
|    | Total              | [Grand Sum]   | [Grand Sum]            | [Grand Sum] | [Sum]   | [Sum]   | [Grand Sum]  | [Grand Sum]   |
+----+--------------------+---------------+------------------------+-------------+---------+---------+--------------+---------------+
```

### Row-by-Row Module Categorization & Database Criteria:

#### Row 1: SUPREME COURT
Aggregates Supreme Court Special Leave Petitions (SLPs) and Appeals across all 4 module tables:
1. **MVC Cases**: `CorpSLPNo IS NOT NULL` OR `ClaimantSLPNo IS NOT NULL` OR `UPPER(CurrentStatus) LIKE '%SLP%'`
2. **Labour Cases**: `ClaimantSCNumber IS NOT NULL` OR `UPPER(CaseType) LIKE '%SLP%'`
3. **Gratuity Cases**: `UPPER(CourtType) LIKE '%SUPREME%'` OR `UPPER(AppealNumber) LIKE '%SLP%'`
4. **Other Cases**: `UPPER(CourtType) LIKE '%SUPREME%'` OR `UPPER(CaseType) LIKE '%SLP%'`

#### Row 2: HIGH COURT
Aggregates High Court Writ Petitions (WP), Writ Appeals (WA), and Miscellaneous First Appeals (MFA) across all 4 module tables:
1. **MVC Cases**: `CorpMFANo IS NOT NULL` OR `ClaimantMFANo IS NOT NULL` OR `MFAEntrustmentNo IS NOT NULL` OR `UPPER(CurrentStatus) LIKE '%MFA%'` (excluding Supreme Court SLPs)
2. **Labour Cases**: `UPPER(CaseType) LIKE '%WP%'` OR `UPPER(CaseType) LIKE '%WA%'` OR `HighCourtBench IS NOT NULL` (excluding Supreme Court SLPs)
3. **Gratuity Cases**: `UPPER(CourtType) LIKE '%HIGH%'` OR `UPPER(AppealNumber) LIKE '%WP%'` OR `UPPER(AppealNumber) LIKE '%WA%'` (excluding Supreme Court SLPs)
4. **Other Cases**: `UPPER(CourtType) LIKE '%HIGH%'` OR `UPPER(CaseType) LIKE '%WP%'` OR `UPPER(CaseType) LIKE '%WA%'` (excluding Supreme Court SLPs)

#### Row 3: OTHER COURTS
Aggregates all lower trial court, MACT, Labour Court, Controlling Authority (Gratuity), Civil Court (OS), Consumer Forum, and Land Acquisition (LAC) cases across all 4 module tables:
1. **MVC Cases**: All `MVC_CASES` records where case is not High Court (MFA) or Supreme Court (SLP)
2. **Labour Cases**: All `LABOUR_CASES` records (KID, ID, Arising Applications) where case is not WP, WA, or SLP
3. **Gratuity Cases**: All `GRA_CASES` records (Controlling Authority / Appellate Authority) not matching High Court or Supreme Court filters
4. **Other Cases**: All `OTHER_CASES` records (OS, Consumer, LAC, ECA, PSC, CC) not matching High Court or Supreme Court filters

---

## 🧮 Mathematical Verification Formulas

For all report rows and totals:
$$\text{Total Cases} = \text{Cases Pending (Beginning)} + \text{No. of Cases Entrusted}$$
$$\text{Total Disposed} = \text{Cases Disposed Favour} + \text{Cases Disposed Against}$$
$$\text{Cases Pending End} = \text{Total Cases} - \text{Total Disposed}$$

---
*Documented and verified against ASP.NET Core 9.0 MVC `ReportController.cs` source code.*
