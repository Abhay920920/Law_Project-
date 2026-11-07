# 🛡️ Mandatory 4-Skill Operating Protocol — Law Project

> **COMPULSORY INSTRUCTION FOR EVERY PROMPT**:  
> For **ANY** prompt, operation, question, bug fix, feature addition, or refactoring in this repository, you **MUST COMPULSORILY** follow the 4-skill framework below.  
> **Rule of Engagement**: **Always plan first, analyze through all 4 skills, and then implement step-by-step one by one.**

---

## 🧭 The 4 Skills Required on Every Task

1. **`graphify-8` — Structural Dependency & Impact Mapping** (`.agents/skills/graphify-8/`)
   - **Identify Community & God Nodes**: Check which of the 9 communities the request touches and whether it touches top God Nodes (`DBHelper`, `CaseRepository`, `ECourtsController`, `LabourController`, `UnifiedLegalResearchService`, `HighCourtDailyCauseListScannerService`, `ReportController`, `GratuityController`).
   - **Trace Blast Radius**: Map incoming and outgoing dependency edges before modifying any file. Ensure changes do not break cross-boundary bridges (e.g. `App_Data/causelist_cache`, `TR18Service`, `DivisionControllerReadOnlyFilter`).

2. **`rules/ponytail.md` — Lazy Senior Developer Principle** (`.agents/skills/rules/ponytail.md`)
   - *"Lazy means efficient, not careless. The best code is the code never written."*
   - **Climb the 7-Rung Ladder**:
     1. *Does this need to be built at all? (YAGNI)*
     2. *Does it already exist in this codebase?* (Reuse `DBHelper`, `DataRowExtensions`, `PasswordHelper`, existing repositories/services).
     3. *Does the .NET 9 standard library already do this?* Use it.
     4. *Does native ASP.NET Core framework cover it?* (Filters, attributes, middleware).
     5. *Does an installed package solve it?* (Dapper, `PdfPig`, `Microsoft.Data.SqlClient`).
     6. *Can this be one line?* Make it one line.
     7. *Only then: write the minimum code that works.*
   - **Root Cause over Symptom**: Fix the shared helper or method once; never patch individual call sites with one-off guards.
   - **Zero Bloat**: No unrequested abstractions, no unneeded dependencies, no boilerplate, shortest working diff.

3. **`find-security-vulnerabilities-in-code` — Strix White-Box Security Gate** (`.agents/skills/find-security-vulnerabilities-in-code/`)
   - **Multi-Tenant Boundary Isolation**: Every SQL query must strictly filter by `DivisionID` unless the user is Central Office or Admin (`DivisionID = 0 or 5`).
   - **100% Parameterization**: All SQL queries must use parameters (`@param`) with `SqlCommand` or Dapper. Never concatenate user input into SQL strings.
   - **Authorization & IDOR Defense**: Enforce `[Authorize]`, claims checking, and `DivisionControllerReadOnlyFilter` (Division Controller is strictly read-only; no POST/PUT/DELETE mutations).
   - **SSRF & Input Validation**: Validate external and e-Courts URLs via `UrlSecurityValidator`.

4. **`gsd-core-next` — Spec-Driven Phased Execution Loop** (`.agents/skills/gsd-core-next/`)
   - **Phased Lifecycle**: Follow the 5-step loop: **Discuss $\to$ Plan $\to$ Execute $\to$ Verify $\to$ Ship**.
   - **Plan First**: Always lay out a clear, structured plan with atomic phases/waves before writing code.
   - **Implement One by One**: Execute code modifications sequentially, file by file, preserving clean boundaries and existing comments.
   - **Verification Gate**: Never declare work complete without verifying: run tests (`dotnet test`), check database migrations, and confirm runnable validation checks.

---

## ⚡ Mandatory Execution Workflow for Every Prompt

```
[ Step 1: Graphify Impact Analysis ]
   └── Identify affected community, God nodes, and blast radius.

[ Step 2: Ponytail Minimalist Solution Design ]
   └── Climb 7-Rung ladder; reuse existing DAL/helpers; formulate shortest working diff.

[ Step 3: Strix Security & Tenancy Verification ]
   └── Verify DivisionID tenant filtering, SQL parameterization, and role checks.

[ Step 4: GSD Phased & Atomic Implementation ]
   └── Present the plan to the user; execute modifications one by one.

[ Step 5: Verification & Health Check ]
   └── Run test suites / validation checks; ensure zero regressions before reporting.
```

