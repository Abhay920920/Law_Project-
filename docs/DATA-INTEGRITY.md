# DATA INTEGRITY & ZERO-DATA-LOSS GUARANTEE

## 1. Principles of Data Integrity

The NWKRTC Law Project manages legally and financially binding case records, court judgments, claimant payments, and statutory gratuity entitlements. To guarantee **zero data loss** and zero corruption:

1. **All Existing Production Data is Immutable**: No records are deleted during upgrades or migrations.
2. **ACID Transaction Boundaries**: Every multi-table state transition (Case creation, Adverse Award recording, Arising Application registration, Case Transfer, Approval actions) is wrapped in an explicit `SqlTransaction`.
3. **No Destructive Operations**: `DROP DATABASE`, `DROP TABLE`, `TRUNCATE`, and unconstrained `DELETE` operations are completely forbidden in production scripts.
4. **Idempotent Background Jobs**: Background workers and external synchronizations maintain duplicate execution barriers using database applocks.

---

## 2. Transaction Architecture & Safeguards

### 2.1 Multi-Table Persistence Flow
When persisting complex parent-child legal entities (e.g., MVC Case with Petitioners, Respondents, Claimants, Connected Cases, and Adverse Awards):
```text
BEGIN TRANSACTION
    1. Check for duplicate CNR Number / Case Registration.
    2. Insert Parent Case Header (MVC_CASES / LABOUR_CASES / GRATUITY_CASES).
    3. Retrieve Scope Identity (CaseID).
    4. Insert Related Collections (Claimants, Witnesses, Connected Cases, Documents).
    5. Validate Referential Invariants (CaseID > 0, Foreign Keys Match).
COMMIT TRANSACTION
ON ERROR:
    ROLLBACK TRANSACTION
    Log Exception with Correlation ID and Entity Details.
```

### 2.2 Preserving Partial Historical State
- When users edit cases, partial updates must not overwrite unsubmitted related records.
- In `CaseRepository.cs`, `LabourRepository.cs`, `GratuityRepository.cs`, and `AppealRepository.cs`, file paths and existing attachment references are preserved using `COALESCE(@NewPath, ExistingPath)`. If a user does not upload a new file on an edit screen, the existing document reference is guaranteed to remain untouched.

---

## 3. Concurrency & Lost Update Prevention

- **Problem**: Simultaneous edits by multiple users (e.g., Division Legal Assistant and Central Office Law Officer updating the same case simultaneously).
- **Protection**:
  - `ModifiedDate` timestamp tracking on all major entities.
  - Role-based workflow states: Once a case is marked `SentToCO == true`, division users are placed in read-only lock mode. They cannot overwrite notes or actions recorded by Central Office officers.
  - Approval decisions by Competent Authority / MD / CLO require explicit role authorizations and transition validations.

---

## 4. Invariant Rules Enforced

1. **Referential Integrity**:
   - Every `ADVERSE_AWARDS`, `PETITIONERS`, and `CONNECTED_CASES` record must have a valid non-null foreign key referencing `MVC_CASES(CaseID)`.
2. **Financial Non-Negativity**:
   - `AwardAmount`, `InterestRate`, `ActualPaidAmount`, `GratuityAmount`, and `FinalAmount` are constrained to non-negative numerical ranges ($>= 0$).
3. **Gratuity Calculation Invariant**:
   - Statutory Gratuity: Net Payable Gratuity = Gross Entitlement - Statutory Deductions.
   - Net payable amounts cannot be negative.
4. **CNR Number Formatting**:
   - e-Courts CNR Numbers are standardized to 16 characters alphanumeric uppercase before indexing or querying.
