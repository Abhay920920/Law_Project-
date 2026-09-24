# FILE SECURITY SPECIFICATION

## 1. Objective

Court orders, adverse judgments, charge sheets, enquiry reports, and legal correspondence are sensitive, confidential legal artifacts. The file storage architecture prevents unauthorized file execution, directory traversal, malicious payload uploads, and unauthenticated document harvesting.

---

## 2. File Upload Pipeline

Every uploaded file (e.g., in `CaseController`, `LabourController`, `GratuityController`, `JudgementController`, and `ECourtsController`) passes through strict validation:

```text
Incoming IFormFile
       │
       ▼
1. Authentication Check (`[Authorize]`)
       │
       ▼
2. Division / Case IDOR Check
       │
       ▼
3. File Size Validation (Max 20 MB)
       │
       ▼
4. File Extension Whitelist (.pdf, .doc, .docx, .jpg, .jpeg, .png)
       │
       ▼
5. Magic Bytes / Header Validation (%PDF-, PK zip, image signatures)
       │
       ▼
6. Non-Deterministic Filename Generation (`Guid.NewGuid() + extension`)
       │
       ▼
7. Canonical Path Traversal Guard (`Path.GetFullPath(...)`)
       │
       ▼
8. Disk Storage in Isolated Directory (Outside executable execution paths)
```

---

## 3. Secure File Retrieval Architecture

Previously, static files in `/uploads` were mapped directly via `UseStaticFiles` and accessible by guessing or enumerating URLs.

### Hardened Delivery Mechanism:
- Direct `/uploads` physical file access is disabled in `Program.cs`.
- All requests route through `UploadsController.cs`:
  1. Validates that the user is authenticated.
  2. Ensures the requested path resides within the designated uploads directory using canonical path comparison.
  3. Serves the file as an application stream with strict `Content-Disposition: inline` (or attachment) and `X-Content-Type-Options: nosniff`.
