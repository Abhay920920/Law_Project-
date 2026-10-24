# 🛡️ NWKRTC Law Management System — Security & Data Integrity Map

**Document Reference**: Security Architecture, Insecure Direct Object Reference (IDOR) Defenses & Integrity Invariants  
**Verification Level**: **VERIFIED IN SOURCE & TEST**  

---

## 1. Security Architecture & Threat Defense Matrix

| Security Threat Category | Vulnerability Mechanism | Implemented Code Defense | Source Reference | Automated Verification Test |
| :--- | :--- | :--- | :--- | :--- |
| **Cross-Tenant IDOR (File Downloads)** | Cross-division access to uploaded legal documents via file name guessing (CWE-639). | Zero-Trust multi-domain SQL check across 6 tables (`MVC`, `APPEAL`, `LABOUR`, `ARISING`, `GRA`, `OTHER`). Non-CO users must match `userDivId`. | `UploadsController.cs:65-115` | `SecurityRemediationValidationTests.cs` |
| **Execution Petition IDOR** | Modification of another division's award liabilities or execution petitions. | Direct ownership verification on GET/POST `Edit` and `Details`. Invariant lock: `model.DivisionID = existing.DivisionID`. | `EPController.cs:40-75`, `LabourEPController.cs:55-90` | `SecurityRemediationValidationTests.cs` |
| **Labour Arising App IDOR & Deletion** | Unauthorized tampering or deletion of Section 33-C(2) monetary claim records. | Enforced `[ValidateAntiForgeryToken]` and division check on `DeleteArisingApplication` and `EditArisingApplication`. | `LabourController.cs:1890-1940` | `SecurityRemediationValidationTests.cs` |
| **High Court Appeal IDOR** | Unauthorized altering of appeal outcomes or advocate assignments for other divisions. | Enforced parent case division verification on `AppealController.Manage` and `AppealController.Save`. | `AppealController.cs:115-180` | `SecurityRemediationValidationTests.cs` |
| **Petty Bill Printing IDOR** | Viewing payment vouchers across divisional boundaries. | Parent case division verification against `userDivId` on `ReportController.PrintBill`. | `ReportController.cs:1420-1460` | `SecurityRemediationValidationTests.cs` |
| **Dynamic SQL Identifier Injection** | Arbitrary column or table name interpolation in dynamic SQL updates. | Strict allowlists for column names (`CaseID`, `AppealID`, `ServiceMatterID`, `LabourCaseID`) and table names. | `ECourtsRepository.cs:255-295` | `ECourtsRepository_UpdateCaseCnrLinkAsync_InvalidPrimaryKeyColumn_ThrowsArgumentException` |
| **Cross-Site Request Forgery (CSRF)** | State-changing POST requests executed without anti-forgery token verification. | Global `AutoValidateAntiforgeryTokenAttribute` in `Program.cs`. Client-side auto-header hook in `_Layout.cshtml`. | `Program.cs:28`, `_Layout.cshtml:521-550` | `SecurityRemediationValidationTests.cs` |
| **Server-Side Request Forgery (SSRF)** | Arbitrary outbound HTTP requests targeting private cloud metadata or localhost. | Strict validation in `UrlSecurityValidator.cs` blocking `127.0.0.1`, `169.254.169.254`, and private ranges. | `UrlSecurityValidator.cs:20-60` | `UrlSecurityValidator_LocalMetadataAndLoopback_AreBlocked` |
| **Forensic Identity Spoofing** | Forged client headers (`X-Forwarded-For`, `X-Client-MAC`) polluting audit logs. | Kernel layer-2 `SendARP` execution for LAN clients; WAN headers explicitly marked as `(Header: ...)`. | `NetworkDeviceHelper.cs:30-85` | `NetworkDeviceHelper_RemoteIpResolution_HandlesIPv4Directly` |

---

## 2. Data Integrity Invariants & Concurrency Safeguards

1. **Distributed Schema Migration Lock (`sp_getapplock`)**:
   - Implemented in `DatabaseMigrationRunner.cs`.
   - Prevents race conditions during IIS web garden recycling or concurrent app pool startups.
2. **Statutory 10% Simple Interest Formula Invariant**:
   - Under Section 7(3A) of the Payment of Gratuity Act 1972: `Interest = (Principal * 10 * Days) / (365 * 100)`.
   - Covered by unit tests in `GratuityCalculationTests.cs`.
3. **Petty Bill Deduplication & Previous Payment Reconciliation**:
   - `PettyBillRepository.cs` locks previous payment records within a `SqlTransaction` and verifies `NetPayable = Gross - PreviousDeductions - TDS`.
   - Covered by tests in `PettyBillSerializationTests.cs`.
