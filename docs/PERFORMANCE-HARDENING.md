# PERFORMANCE HARDENING & OPTIMIZATION

## 1. Executive Summary

This document details the performance optimizations implemented across the NWKRTC Law Project to minimize request latency, reduce database load, prevent connection pool exhaustion, and eliminate application startup delays.

---

## 2. Key Optimizations Implemented

### 2.1 Elimination of Startup Schema Introspection Bottleneck
- **Before**: On every application startup (and on certain repository constructor instantiations), the application executed hundreds of iterative schema-introspection queries (`SELECT * FROM sys.columns WHERE object_id = ...`) across multiple tables. In clustered or cloud deployments, this created multi-second boot latency and database CPU spikes.
- **After**: Implemented `DatabaseMigrationRunner.cs` utilizing a persistent `SCHEMA_MIGRATIONS` ledger table and `sp_getapplock`. Migrations are versioned and executed once in strict sequence. Subsequent startups complete schema checks in under 5 milliseconds.

### 2.2 Server-Side Pagination on Heavy Grids
- All primary case listings (`/Case`, `/Labour/CaseList`, `/Gratuity/CaseList`, `/EP/Index`, `/LabourEP/Index`) implement indexed SQL Server pagination (`OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY`).
- Eliminated client-side in-memory filtering of tens of thousands of records.

### 2.3 Critical Index Coverage
Targeted composite indexes ensure fast lookups on high-frequency filtering columns:
1. `IX_MVC_CASES_Division_Status` on `MVC_CASES(DivisionID, CurrentStage, DisposalResult)` INCLUDE `(MVCNo, MVCYear, NextHearingDate)`.
2. `IX_MVC_CASES_Vehicle_AccidentDate` on `MVC_CASES(VehicleNo, AccidentDate)`.
3. `IX_MVC_CASES_CNR` on `MVC_CASES(CNRNumber)` where `CNRNumber IS NOT NULL`.
4. `IX_LABOUR_CASES_Division_Status` on `LABOUR_CASES(DivisionID, CaseStatus, SentToCO)`.
5. `IX_GRATUITY_CASES_Division_Status` on `GRATUITY_CASES(DivisionCode, CaseStatus, ForwardingStatus)`.
6. `IX_LABOUR_ARISING_Parent` on `LABOUR_ARISING_APPLICATIONS(ParentCaseID)`.

### 2.4 Response Compression & HTTP Caching
- Enabled Gzip and Brotli compression in `Program.cs` for static MIME types (CSS, JavaScript, SVG, JSON).
- Static assets configured with immutable 365-day cache headers and cache-busting version tags (`asp-append-version="true"`).

### 2.5 Memory & Streaming Efficiency
- Document downloads and PDF views utilize asynchronous streaming (`FileStreamResult` / `HttpResponse.Body.WriteAsync`) rather than buffering entire 20MB files into large byte arrays in the Large Object Heap (LOH).
