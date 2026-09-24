# DATA INTEGRITY FINDINGS

## 1. Identified Integrity Risks & Resolutions

| Area | Observed Risk | Implemented Safeguard |
|:---|:---|:---|
| **Multi-Table Case Creation** | Partial failures when saving Petitioners or Connected Cases could leave orphaned parent case headers. | Enclosed all child inserts inside an explicit `SqlTransaction`. Rolls back completely if any child insert fails. |
| **Document Overwrite on Edit** | Editing a case without uploading a new document risked clearing the existing file path. | Added `COALESCE(@NewUploadPath, ExistingPath)` semantics to all update queries. |
| **Arising Application Sync** | Loose coupling between parent Labour Cases and Arising Applications allowed orphaned child records. | Added parent resolution and foreign key indexing between `LABOUR_ARISING_APPLICATIONS` and `LABOUR_CASES`. |
| **Financial & Interest Calculation** | Inconsistent rounding or negative entries could distort statutory payments. | Validated non-negative financial inputs and verified statutory formulas against legal specifications. |
| **Division Scoping on Updates** | Division users editing a case could inadvertently reassign the division ID or modify other divisions. | Enforced immutable division assignments and server-side authorization checks on all `POST` handlers. |
