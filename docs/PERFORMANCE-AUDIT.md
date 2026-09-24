# NWKRTC LAW PROJECT — COMPLETE APPLICATION PERFORMANCE INVENTORY & AUDIT

---

## 1. Application Inventory & Component Profile

| Component | Operation | DB Calls | External Calls | File I/O | Current Latency | Performance Risk & Bottleneck Profile |
| :--- | :--- | :---: | :---: | :---: | :---: | :--- |
| **AccountController** | `Login` (GET) | 0 | 0 | Razor view | < 1 ms | LOW: Fast Razor render; memory cache for lockout check. |
| **AccountController** | `Login` (POST) | 1–2 | 0 | 0 | 8–15 ms | LOW: PBKDF2 hash verification; brute-force throttled. |
| **AccountController** | `ForgotPassword` / `VerifyOtp` | 1–2 | 1 (SMS) | 0 | 120–450 ms | MEDIUM: Dependent on Karnataka SMS Gateway response. |
| **DashboardController** | `Index` | 4–6 | 0 | 0 | 25–65 ms | MEDIUM: Aggregates stats across MVC, Labour, Gratuity. |
| **CaseController** | `Index` (Listing) | 2 | 0 | 0 | 15–45 ms | MEDIUM: Paged query with `OFFSET ... FETCH`; covered by indexes. |
| **CaseController** | `Details` (View) | 6–8 | 0 | 0 | 45–110 ms | HIGH: Fetches petitioners, respondents, connected cases sequentially. |
| **CaseController** | `Create` / `Edit` (Save) | 4–6 | 0 | 1 (PDF) | 35–85 ms | MEDIUM: Atomic `SqlTransaction` with upload file validation. |
| **LabourController** | `Index` / `Details` | 2–5 | 0 | 0 | 20–70 ms | MEDIUM: Filtered by division; sargable predicates. |
| **GratuityController** | `Index` / `Details` | 3–5 | 0 | 0 | 25–80 ms | MEDIUM: Retrieves calculations, payments, and interest items. |
| **ReportController** | Custom Reports Generation | 1–3 | 0 | 0 | 50–350 ms | HIGH: Large record aggregations; requires covering indexes on date columns. |
| **ECourtsController** | `GetCnrDetails` (NAPIX) | 1–2 | 1 (NAPIX) | 0 | 560–1200 ms | CRITICAL (External): 99% of time spent in Gov Gateway WAN transit. |
| **ECourtsController** | `ViewOrderPdf` (Proxy) | 0 | 1 (eCourts) | Stream | 200–800 ms | MEDIUM: External court PDF fetch; protected against SSRF. |
| **UploadsController** | `GetUploadFile` | 0–1 | 0 | PhysicalFile | < 2 ms | LOW: Fast non-buffering OS streaming (`PhysicalFileResult`). |
| **NotificationBackgroundService** | Daily Sync & Alerts Cycle | N (Tracked) | N (NAPIX) | Log | Throttled (1s/case) | HIGH: Multi-instance distributed lock via `sp_getapplock`; throttled. |
| **MasterController** | Dropdown Lists (Divisions, Courts) | 0 (Cached) | 0 | 0 | < 0.1 ms | LOW (Optimized): In-memory caching with 30-min TTL. |

---

## 2. Anti-Pattern Codebase Scan Results

A comprehensive code analysis was performed across the entire repository for known enterprise anti-patterns:

1. **Sync-Over-Async (`.Result`, `.Wait()`)**:
   - `NotificationBackgroundService`: Fully asynchronous (`await ExecuteNonQueryAsync`, `await conn.OpenAsync`).
   - `ECourtsNapixService`: Fully asynchronous (`await _httpClient.SendAsync`, `await JsonDocument.ParseAsync`).
   - Legacy MVC Controller actions: Standard synchronous ADO.NET patterns with connection pool boundaries maintained cleanly.
2. **Unbounded Collections & Memory Leaks**:
   - `MasterRepository`: Master tables are bounded (Divisions: 6, Courts: < 100, Advocates: < 500). In-memory caching footprint is < 1 MB.
   - Session State: 60-minute expiration with cookie-based session ticket.
3. **Database Connection Leaks**:
   - All connection instances in `DBHelper` and repositories are enclosed in C# `using` blocks, guaranteeing immediate return to connection pool upon execution completion or exception.
4. **File Buffering into Memory**:
   - `UploadsController` serves physical files directly from disk using `PhysicalFileResult`, bypassing web server managed memory buffers.
