# 👥 NWKRTC Law Management System — Role & Permission Matrix

**Document Reference**: Multi-Tenant Authorization, Claims Verification & Divisional Scoping  
**Roles Verified in Source**: `Admin`, `CO` (Central Office), `LO` (Law Officer), `DyCLO` (Deputy Chief Law Officer), `CLO` (Chief Law Officer), `MD` (Managing Director), `Division User` (Legal Assistant / Officer)  
**Verification Level**: **VERIFIED IN SOURCE**  

---

## 1. Role Definitions & Claim Attributes

Every authenticated officer has an ASP.NET Core Cookie ticket populated with standard and custom claims:
- `ClaimTypes.Name` / `username`: Login identifier (e.g., `LO_Central`, `Div_Belagavi`).
- `ClaimTypes.Role` / `Role`: User authorization tier.
- `DivisionID`: Integer identifier mapping to `[dbo].[DIVISIONS]` (`0` or `5` indicates Central Office; `1..9` indicates operating divisions).
- `DivisionName`: Human-readable name (e.g., `Belagavi`, `Central Office`).
- `FullName`: Officer name used in greetings and audit logs.

---

## 2. Comprehensive Role Permission Matrix

| Functional Module / Action | Anonymous | Division User | Central Office (CO) | Law Officer (LO) | Dy CLO | Chief Law Officer (CLO) | Managing Director (MD) | Admin |
| :--- | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: |
| **Login / Forgot Password** | **ALLOW** | **ALLOW** | **ALLOW** | **ALLOW** | **ALLOW** | **ALLOW** | **ALLOW** | **ALLOW** |
| **Home Portal Dashboard** | DENY | **ALLOW** (Own Div) | **ALLOW** (All) | **ALLOW** (All) | **ALLOW** (All) | **ALLOW** (All) | **ALLOW** (All) | **ALLOW** (All) |
| **MVC New Case Registration** | DENY | **ALLOW** | DENY | DENY (Read-Only) | DENY (Read-Only) | DENY (Read-Only) | DENY (Read-Only) | **ALLOW** |
| **MVC Case List & Search** | DENY | **ALLOW** (Own Div) | **ALLOW** (All) | **ALLOW** (All) | **ALLOW** (All) | **ALLOW** (All) | **ALLOW** (All) | **ALLOW** (All) |
| **MVC Edit Case Particulars** | DENY | **ALLOW** (Owner Div)| **ALLOW** | DENY (Read-Only) | DENY (Read-Only) | DENY (Read-Only) | DENY (Read-Only) | **ALLOW** |
| **Record Hearings / Adverse Orders**| DENY | **ALLOW** (Owner Div)| **ALLOW** | DENY (Read-Only) | DENY (Read-Only) | DENY (Read-Only) | DENY (Read-Only) | **ALLOW** |
| **File High Court Appeals (MFA)** | DENY | CONDITIONAL | **ALLOW** | **ALLOW** | **ALLOW** | **ALLOW** | **ALLOW** | **ALLOW** |
| **Labour Dispute Registration**| DENY | **ALLOW** (Own Div) | DENY | DENY (Read-Only) | DENY (Read-Only) | DENY (Read-Only) | DENY (Read-Only) | **ALLOW** |
| **Labour Legal Review - LO** | DENY | DENY | DENY | **ALLOW** | DENY | DENY | DENY | **ALLOW** |
| **Labour Legal Review - Dy CLO**| DENY | DENY | DENY | DENY | **ALLOW** | DENY | DENY | **ALLOW** |
| **Labour Legal Review - CLO** | DENY | DENY | DENY | DENY | DENY | **ALLOW** | DENY | **ALLOW** |
| **Labour Final Decision - MD** | DENY | DENY | DENY | DENY | DENY | DENY | **ALLOW** | **ALLOW** |
| **Labour Arising Applications**| DENY | **ALLOW** (Own Div) | **ALLOW** (View) | **ALLOW** (View) | **ALLOW** (View) | **ALLOW** (View) | **ALLOW** (View) | **ALLOW** |
| **Labour EP Execution Petitions**| DENY | **ALLOW** (Own Div) | **ALLOW** (View) | **ALLOW** (View) | **ALLOW** (View) | **ALLOW** (View) | **ALLOW** (View) | **ALLOW** |
| **Service & CCC Matters** | DENY | DENY | **ALLOW** | **ALLOW** | **ALLOW** | **ALLOW** | **ALLOW** | **ALLOW** |
| **Gratuity Claim Registration**| DENY | **ALLOW** (Own Div) | DENY | DENY (Read-Only) | DENY (Read-Only) | DENY (Read-Only) | DENY (Read-Only) | **ALLOW** |
| **Gratuity 10% Audit Calc** | DENY | **ALLOW** (Own Div) | **ALLOW** (All) | **ALLOW** (All) | **ALLOW** (All) | **ALLOW** (All) | **ALLOW** (All) | **ALLOW** (All) |
| **Petty Bill Submission** | DENY | **ALLOW** (Own Div) | DENY | DENY | DENY | DENY | DENY | **ALLOW** |
| **Petty Bill Voucher Print** | DENY | **ALLOW** (Owner Div)| **ALLOW** | **ALLOW** | **ALLOW** | **ALLOW** | **ALLOW** | **ALLOW** |
| **e-Courts On-Demand Sync** | DENY | **ALLOW** (Own Div) | **ALLOW** (All) | **ALLOW** (All) | **ALLOW** (All) | **ALLOW** (All) | **ALLOW** (All) | **ALLOW** (All) |
| **Nyaya Patha AI Assistant** | DENY | **ALLOW** (Div Scoped)| **ALLOW** (All) | **ALLOW** (All) | **ALLOW** (All) | **ALLOW** (All) | **ALLOW** (All) | **ALLOW** (All) |
| **Physical File Download** | DENY | **ALLOW** (Owner Div)| **ALLOW** (All) | **ALLOW** (All) | **ALLOW** (All) | **ALLOW** (All) | **ALLOW** (All) | **ALLOW** (All) |
| **API Health & Quota Dashboard**| DENY | DENY | **ALLOW** | **ALLOW** | **ALLOW** | **ALLOW** | **ALLOW** | **ALLOW** |
| **Master Tables Administration**| DENY | DENY | DENY | DENY | DENY | DENY | DENY | **ALLOW** |

---

## 3. Divisional Isolation Enforcement Rules

1. **Read Scoping**: When a division user requests a case list (`/Case/Index`, `/Labour/CaseList`, `/Gratuity/CaseList`), queries automatically append `WHERE DivisionID = @userDivId`. Central Office (`DivisionID in (0, 5)`) executes queries without division filters.
2. **Write Scoping**: Mutating POST endpoints (`Edit`, `DeleteArisingApplication`, `SaveHearing`, `SaveAdverseDetails`, `SavePettyBill`) query the existing database record first. If `existing.DivisionID != userDivId` and the user is not Central Office, the action returns `Forbid()` (HTTP 403) and overrides submitted model properties: `model.DivisionID = existing.DivisionID`.
3. **Physical File Scoping**: Document downloads in `UploadsController` perform a unified `UNION ALL` across all 6 case tables. Downloads for unlinked files or cases belonging to another division are blocked with `Forbid()`.
