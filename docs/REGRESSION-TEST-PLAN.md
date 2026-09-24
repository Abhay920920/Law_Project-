# REGRESSION TEST PLAN

## 1. Scope & Objective

To verify that all security, performance, and reliability hardening measures preserve 100% of existing business workflows, bilingual UI views, legal calculation formulas, and external integrations.

---

## 2. Automated Test Suite (`MVCCaseManagement.Tests`)

| Test File | Target Class | Validated Behaviors |
|:---|:---|:---|
| `PasswordHelperTests.cs` | `PasswordHelper` | PBKDF2 hash generation, password verification, backward compatibility with legacy SHA-256 hashes, null/empty safety. |
| `UrlSecurityValidatorTests.cs` | `UrlSecurityValidator` | Allowlisting of approved government domains (`*.ecourts.gov.in`, `delhigw.napix.gov.in`), blocking of private IPs (10.x, 192.168.x, 172.16.x), loopback (127.0.0.1, localhost), and AWS metadata (169.254.169.254). |
| `NumberTextTests.cs` | `NumberText` | Legal Indian currency translation (Rupees / Paise / Lakh / Crore) for formal court submissions and award orders. |
| `GratuityCalculationTests.cs` | `GratuityCase`, `GratuityController` | Summation of payment installments, simple statutory interest calculation formula, and deduction invariants. |

---

## 3. Manual Verification Checklist for Core Modules

1. **Authentication & Session**:
   - Log in as Division User (e.g., Belagavi Division). Confirm only Belagavi cases appear.
   - Verify that session timeout does not grant Central Office access.
   - Log in as Central Office / CLO / MD. Confirm cross-division visibility and action buttons.
2. **MVC Case Management**:
   - Create a new MVC case with Petitioners, Claimants, and Connected Cases.
   - Edit the case and update hearing date. Confirm no unedited collections are dropped.
   - Attempt to edit another division's case directly via URL; confirm HTTP 403 Forbidden.
3. **Labour Cases & Arising Applications**:
   - Register a Labour Case and mark it `SentToCO`. Verify division edit lock triggers.
   - Submit Central Office action (CLO/MD). Confirm status updates in dashboard stats.
4. **Gratuity Management**:
   - Add multiple payment rows. Confirm Ordered Amount calculation matches row sums.
   - Record CLO/MD approval. Confirm outward numbers and dates persist.
5. **e-Courts & NAPIX Sync**:
   - Perform live status fetch using a test 16-character CNR number.
   - Verify court hall, hearing date, and stage sync cleanly to the case record.
   - View court order PDF; verify stream loads in viewer without SSRF risk.
