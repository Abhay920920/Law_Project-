# 📋 Law Project — Project Rules, Standards & Engineering Guidelines

> **Target Audience**: All software engineers, database administrators, and QA engineers contributing to the **Law Project** monorepo.

---

## 🏛️ 1. Core Architectural Principles

1. **Monorepo Cohesion**: All components (`MVCCaseManagement`, `NapixEcourtsApi`, `MVCCaseManagement.Tests`, `docs`, `scripts`) are developed in sync within the unified solution [`LawProject.sln`](file:///c:/Users/adts-/Desktop/Law%20Project/LawProject.sln).
2. **Layered Separation of Concerns**:
   - **Controllers**: Handle HTTP routing, input validation, claims-based authorization, and view model assembly. Business logic must not be embedded directly in Razor views.
   - **Services**: Encapsulate multi-step workflows, external communications (NAPIX, SMS, Ollama), cryptographic operations, and quota budgeting.
   - **Data Access Layer (DAL)**: All database interactions must reside in dedicated repository classes inheriting from or utilizing `DBHelper`.
3. **Resilience & Fault Isolation**: External dependencies (NAPIX eCourts, Ollama AI, Mobile-1 SMS) must have circuit breakers, graceful fallbacks, and non-blocking retry mechanisms. External outages must never crash the primary case registry.

---

## 💻 2. C# 13 & .NET 9.0 Coding Standards

1. **Target Framework**: All projects must compile against `.NET 9.0` (C# 13).
2. **Nullable Reference Types**: Ensure nullable annotations (`#nullable enable`) are respected. Avoid raw `!` null-forgiving operators without defensive checks.
3. **Strongly-Typed Options**: Do not read configuration values via raw string indexing (`_config["Key"]`). Use `IOptions<TOptions>` or strongly typed option classes (`ECourtsOptions`, `SmsOptions`, `TR18Options`, `NyayaPathaOptions`).
4. **Dependency Injection**:
   - Register services with appropriate lifetimes in [`Program.cs`](file:///c:/Users/adts-/Desktop/Law%20Project/Law%20Project/CaseManagement/Program.cs):
     - `Transient`: Lightweight stateless services (`IQueryRouterService`, `IDocumentTextExtractor`).
     - `Scoped`: Repositories and request-bound context builders (`ICaseRepository`, `ICaseContextBuilder`).
     - `Singleton`: Thread-safe services, background workers, memory caches (`NapixQuotaService`, `NapixBackgroundSyncService`).
5. **Asynchronous Patterns**: Use `async`/`await` throughout I/O-bound operations. Never call `.Result` or `.Wait()` on asynchronous tasks to prevent thread-pool starvation and deadlocks.

---

## 🛡️ 3. Security, Input Validation & Defensive Coding

### 3.1 Strict SQL Parameterization (Zero-Tolerance SQLi Policy)
- **Rule**: Direct string concatenation or interpolation in SQL queries is strictly prohibited.
- **Allowed**:
  ```csharp
  // CORRECT - Parameterized query
  using var cmd = new SqlCommand("SELECT * FROM tbl_MVC_Cases WHERE CaseID = @CaseID", conn);
  cmd.Parameters.AddWithValue("@CaseID", caseId);
  ```
- **Prohibited**:
  ```csharp
  // STRICTLY FORBIDDEN - Vulnerable to SQL Injection
  string sql = $"SELECT * FROM tbl_MVC_Cases WHERE CaseID = {caseId}";
  ```

### 3.2 Cross-Site Request Forgery (CSRF / XSRF)
- All state-changing controller actions (`POST`, `PUT`, `DELETE`) must be protected by anti-forgery token validation:
  ```csharp
  [HttpPost]
  [ValidateAntiForgeryToken]
  public async Task<IActionResult> SaveCase(MVCCaseViewModel model)
  ```
- Global validation is enforced via `builder.Services.AddAntiforgery(options => options.HeaderName = "RequestVerificationToken");`.
- In AJAX requests, include the token from `<input type="hidden" name="__RequestVerificationToken" ... />` in the `RequestVerificationToken` header.

### 3.3 File Upload Security (`UploadsController`)
- **Allowed Extensions**: Whitelist only `.pdf`, `.jpg`, `.jpeg`, `.png`. Executable files (`.exe`, `.bat`, `.dll`, `.ps1`) are strictly rejected.
- **MIME & Magic Bytes**: Inspect file headers, not just extensions.
- **Path Traversal Prevention**: Never save files using the client-provided filename. Generate sanitized filenames using `Guid.NewGuid()` and store files outside public web roots or route through authenticated handlers.
- **Size Limits**: Enforce maximum upload limit (50 MB) in both `web.config` and controller actions.

### 3.4 Credential Safety & Secret Handling
- **Never hardcode passwords or API keys** in C# source files.
- `appsettings.json` must be excluded or sanitized before public pushes. Sensitive connection strings and SMS passwords must use environment variables or encrypted configuration in production.

---

## 💾 4. Database & DAL Guidelines

1. **Connection Disposal**: Always wrap `SqlConnection`, `SqlCommand`, and `SqlDataReader` in `using` declarations or blocks to guarantee immediate connection return to the pool:
   ```csharp
   using var conn = DBHelper.GetConnection();
   await conn.OpenAsync();
   using var cmd = new SqlCommand(query, conn);
   ```
2. **Transactions**: Use `SqlTransaction` for multi-table updates (e.g., case creation + audit log + notification record). Always commit inside a `try` block and rollback inside `catch`.
3. **Database Migrations**:
   - New database columns or tables must be added to [`00_Master_Full_Database_Schema_And_Seed.sql`](file:///c:/Users/adts-/Desktop/Law%20Project/Law%20Project/CaseManagement/SQL/00_Master_Full_Database_Schema_And_Seed.sql).
   - Also register idempotent schema check scripts in [`DatabaseMigrationRunner.cs`](file:///c:/Users/adts-/Desktop/Law%20Project/Law%20Project/CaseManagement/DAL/DatabaseMigrationRunner.cs) so existing deployments upgrade automatically on startup.
4. **Pagination**: Large result sets (`MyCases`, case lists) must implement database-level pagination using SQL Server `OFFSET ... ROWS FETCH NEXT ... ROWS ONLY` rather than loading full tables into memory.

---

## 🧪 5. Testing & Verification Standards

1. **Test Framework**: The solution uses **xUnit** with FluentAssertions in [`Law Project/MVCCaseManagement.Tests`](file:///c:/Users/adts-/Desktop/Law%20Project/Law%20Project/MVCCaseManagement.Tests).
2. **Pre-Commit Verification**: All 284 unit and integration tests must pass with zero failures:
   ```bash
   dotnet test LawProject.sln -c Release
   ```
3. **Test Scope**:
   - `NapixDirectPipelineTests`: Cryptographic signing, token caching, AES encryption.
   - `NyayaPathaSecurityAndAITests`: RAG retrieval, conflict detection, prompt management, hallucination verification.
   - `GratuityCalculationTests`: 10% statutory interest formulas and compound calculations.
   - `UrlSecurityValidatorTests`: SSRF prevention and URL whitelisting.

---

## 🚀 6. Release & Publishing Discipline

1. **Publish Automation**: Always deploy through the official automated pipelines:
   - PowerShell: `.\publish.ps1`
   - Windows Batch: `publish.bat`
2. **Synchronized Output**: When modifying Razor views or controllers, verify synchronization between development project views and published mirrors in `Publish_Output/` and `publish/`.
3. **IIS Application Pool**: Production application pools must be set to `AlwaysRunning` with `Idle Time-out = 0` to preserve background sync timers and SMS dispatchers.
