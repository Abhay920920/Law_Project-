# 🌐 e-Courts NAPIX API Gateway & Integration Specification

> **Gateway Infrastructure**: National Informatics Centre (NIC) e-Courts NAPIX Platform v1.3  
> **Supported Courts**: District Courts / ICJS (19 APIs) & High Courts (22 APIs)  
> **C# Service Implementation**: [`ECourtsNapixService.cs`](file:///c:/Users/IT/Desktop/Law%20Project/Law%20Project/CaseManagement/Utils/ECourtsNapixService.cs)  
> **Sync Engine**: [`NapixSyncEngine.cs`](file:///c:/Users/IT/Desktop/Law%20Project/Law%20Project/CaseManagement/Utils/NapixSyncEngine.cs)  
> **Cause List Scanner**: [`HighCourtDailyCauseListScannerService.cs`](file:///c:/Users/IT/Desktop/Law%20Project/Law%20Project/CaseManagement/Services/ECourts/HighCourtDailyCauseListScannerService.cs)  
> **Cause List Background Worker**: [`HighCourtCauseListBackgroundSyncService.cs`](file:///c:/Users/IT/Desktop/Law%20Project/Law%20Project/CaseManagement/Services/ECourts/HighCourtCauseListBackgroundSyncService.cs)  
> **Client Controller**: [`ECourtsController.cs`](file:///c:/Users/IT/Desktop/Law%20Project/Law%20Project/CaseManagement/Controllers/ECourtsController.cs)  
> **Frontend Integration**: [`HighCourtCauseList.cshtml`](file:///c:/Users/IT/Desktop/Law%20Project/Law%20Project/CaseManagement/Views/ECourts/HighCourtCauseList.cshtml) & [`ecourts-details.js`](file:///c:/Users/IT/Desktop/Law%20Project/Law%20Project/CaseManagement/wwwroot/js/ecourts-details.js)

---

## 📌 1. Integration Architecture Overview

The **Law Project** integrates directly with the official **Indian e-Courts System** via the **NIC NAPIX Gateway v1.3**. This integration enables automated 16-digit CNR auto-linking, real-time case status lookups, hearing date synchronizations, judgment order views, court hall resolution, multi-bench cause list scanning, and daily proceeding downloads for both District Courts (ICJS) and High Courts.

```mermaid
sequenceDiagram
    autonumber
    actor User as Legal Officer / User
    participant App as ASP.NET Core (ECourtsController)
    participant Scanner as HighCourtDailyCauseListScannerService
    participant Svc as ECourtsNapixService
    participant Quota as NapixQuotaService (tbl_Napix_Api_Quota)
    participant Gateway as NIC e-Courts NAPIX (delhigw.napix.gov.in)
    participant Cache as File Cache (App_Data/causelist_cache)
    participant DB as SQL Server (Admin_Law)

    User->>App: Request Case Sync / View Cause List
    alt High Court Daily Cause List Request
        App->>Scanner: GetOrScanDailyCauseListAsync(estCode=ALL, targetDate)
        alt Cache Exists for Today
            Scanner->>Cache: Read highcourt_{bench}_{date}.json
            Cache-->>Scanner: Return Cached Cause List Items
        else Cache Missing / Force Refresh
            Scanner->>Gateway: Fetch Daily Sitting Cause List PDFs (Parallel Benches)
            Gateway-->>Scanner: PDF Cause List Stream
            Scanner->>Scanner: Parse via PdfPig, Filter NWKRTC & Subject Code
            Scanner->>Cache: Persist JSON Cache
        end
        Scanner-->>App: Unified Scanned Cases (Bengaluru, Dharwad, Kalaburagi)
        App-->>User: Today's Scheduled Matters UI Table
    else Interactive Case Status / Sync
        App->>Quota: Check Hourly Quota (1000 calls/hr)
        alt Quota Available
            App->>Svc: ExecuteEncryptedRequestAsync(apiPath, queryParams)
            Svc->>Gateway: GET /nic/ecourts/{path}?request_str=...&request_token=...
            Gateway-->>Svc: response_str (Encrypted AES-128-CBC)
            Svc->>Svc: Decrypt response_str -> JSON Object
            Svc->>Quota: Record Usage in tbl_Napix_Api_Calls
            Svc->>DB: Update tbl_ECourts_Cases, tbl_ECourts_Hearings, HallNumber
            Svc-->>App: Structured Decrypted Case Data
            App-->>User: Live Case Status Modal / Paginated Registry
        end
    end
```

---

## 🔑 2. Cryptographic Security Protocol

The NAPIX Gateway enforces enterprise security controls requiring OAuth2 bearer authentication, payload encryption, and request signing.

### 2.1 Configuration Keys (`appsettings.json`)
```json
"eCourts": {
  "DeptId": "clonwkrtc",
  "AuthKey": "tD7Ju6n0Cdf4vxUo",
  "HmacKey": "15081947",
  "Version": "v1.0",
  "GatewayUrl": "https://delhigw.napix.gov.in/nic/ecourts",
  "OAuthTokenUrl": "https://delhigw.napix.gov.in/nic/ecourts/oauth2/token",
  "EnableBackgroundSync": true,
  "MVC": {
    "ApiKey": "5763de722863865e2691e137691d4333",
    "SecretKey": "454aba6d3c5c867ae185cd198f572ef8"
  },
  "Labour": {
    "ApiKey": "9b54f1a9420610260bf6fc16b6aea571",
    "SecretKey": "81138f78dc15e93b1381e415f34771c1"
  },
  "OtherCourts": {
    "ApiKey": "e3c53d81c28155569d13699aee22c7e2",
    "SecretKey": "88c225479d6d43d30d59562cb4ff8fef",
    "HourlyQuota": 1000
  }
}
```

### 2.2 Security Execution Flow
1. **OAuth2 Bearer Token**:
   - Transmitted via Basic Authorization header (`Basic Base64(ApiKey:SecretKey)`).
   - Grants `access_token` cached in `IMemoryCache` for 55 minutes (`eCourts_OAuth_Token`).
   - If NAPIX returns `INVALID_TOKEN` or HTTP 401, `ECourtsNapixService` automatically evicts cache, acquires a fresh token, and retries the request seamlessly.
2. **AES-128-CBC Payload Encryption**:
   - Plaintext query parameters (e.g. `est_code=KAUKA01|case_type=MVC|reg_no=123|reg_year=2024`) are encrypted using **AES-128-CBC** with `AuthKey` and `IV`.
   - The encrypted byte array is Base64 encoded and URL-escaped into `request_str`.
3. **HMAC-SHA256 Request Signing**:
   - A cryptographic hash of the plaintext query parameters is generated using **HMAC-SHA256** signed with `HmacKey`.
   - Transmitted in the request query string as `request_token`.
4. **Payload Decryption**:
   - Successful responses return an encrypted string inside `response_str`.
   - `ECourtsNapixService.AesDecrypt()` decrypts `response_str` into structured JSON objects.

---

## 🆔 3. 16-Digit CNR Discovery & Decoding Engine

### 3.1 CNR Structure
An Indian e-Courts CNR (Case Number Record) is a 16-character unique identifier:
$$\text{CNR Format}: \underbrace{\text{KA}}_{\text{State}} \underbrace{\text{UKA}}_{\text{District}} \underbrace{20006722017}_{\text{Establishment, Case Sequence \& Year}}$$

### 3.2 Automated Code Decoding (`DecodeCnrCodes`)
When querying status or orders by CNR, `ECourtsNapixService` automatically extracts state and district parameters:

| CNR Prefix | State Name | State Code (NAPIX / CIS) | District Name | District Code |
| :--- | :--- | :--- | :--- | :--- |
| `KAUKA` | Karnataka | `29` / `6` | Uttara Kannada | `19` |
| `KADW` | Karnataka | `29` / `6` | Dharwad | `1` |
| `KABG` / `KABAG` | Karnataka | `29` / `6` | Bagalkot | `4` |
| `KADVG` / `KADAG` | Karnataka | `29` / `6` | Davanagere | `9` |
| `KAHC01` | Karnataka High Court | `29` | Principal Bench Bengaluru | `1` |
| `KAHC02` | Karnataka High Court | `29` | Dharwad Bench | `2` |
| `KAHC03` | Karnataka High Court | `29` | Kalaburagi Bench | `3` |
| `MHBOM` | Maharashtra | `27` | Mumbai | `1` |
| `DL` | Delhi | `7` | Delhi Courts | District Code |

---

## 🏛️ 4. Automated Court Hall Resolution Engine

In high-volume court establishments, hearing notices often omit explicit courtroom designations. The **Hall Resolution Engine** in `ECourtsController` and `ECourtsNapixService`:
1. Scans daily cause list proceedings and establishment master rosters (`CourtJudgeList`).
2. Extracts judge designation, courtroom number, and bench type from raw NAPIX business proceedings.
3. Automatically persists the decoded `HallNumber` into `tbl_ECourts_Cases` and `tbl_MVC_Cases`.
4. Displays the courtroom badge directly on user dashboards and hearing reminder SMS notifications.

---

## 📄 5. MyCases Server-Side Pagination & Real-Time Status

The [`/ECourts/MyCases`](file:///c:/Users/IT/Desktop/Law%20Project/Law%20Project/CaseManagement/Views/ECourts/MyCases.cshtml) interface provides high-performance server-side pagination:
- **Server-Side SQL Pagination**: Employs `OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY` preventing client memory saturation over tens of thousands of cases.
- **Multi-Filter Support**: Filter by Division (`DivisionID`), Case Type (`MVC`, `Labour`, `Other`), Court Complex, Date Range, or Search Term (CNR, Case Number, Petitioner/Respondent).
- **Real-Time Status Badges**:
  - `SYNCED`: Live status matches e-Courts records within the last 24 hours.
  - `HEARING_SOON`: Next hearing scheduled within 7 days.
  - `DISPOSED`: Case disposed in court with final order available.
  - `STALE`: Case not synced in > 30 days, one-click manual sync enabled.

---

## ⏱️ 6. Hourly Quota Management (`tbl_Napix_Api_Quota`)

To prevent IP rate-limiting from the National Informatics Centre gateway:
- Every outbound request checks [`NapixQuotaService.cs`](file:///c:/Users/IT/Desktop/Law%20Project/Law%20Project/CaseManagement/Utils/NapixQuotaService.cs).
- Enforces an hourly cap of **1,000 requests per module window**.
- Tracks consumed calls, remaining allowance, and window resets in real-time.
- If quota reaches 95%, background bulk sync jobs automatically pause and yield bandwidth to interactive user requests.

---

## ⚖️ 7. High Court Multi-Bench Daily Cause List Scanner & Background Sync Architecture

The High Court Cause List infrastructure delivers full-roster scanning, subject code parsing, and strict RTC jurisdictional boundary enforcement:

### 7.1 Architecture & Multi-Bench Parallel Scanning
- **Service**: [`HighCourtDailyCauseListScannerService.cs`](file:///c:/Users/IT/Desktop/Law%20Project/Law%20Project/CaseManagement/Services/ECourts/HighCourtDailyCauseListScannerService.cs)
- **Multi-Bench Aggregation**:
  - When `estCode=ALL` is requested, the scanner executes queries in parallel across all three High Court establishments:
    - **`KAHC01`**: Principal Bench at Bengaluru (~42 sitting courts)
    - **`KAHC02`**: Bench at Dharwad (~20 sitting courts)
    - **`KAHC03`**: Bench at Kalaburagi (~16 sitting courts)
  - Results are deduped by CNR and synthetic key (`CaseType_RegNo_RegYear`) and unified in memory.

### 7.2 High Court Subject Category Extraction
Every case is tagged with its official Karnataka High Court classification code in parentheses:
- **`(L, KSRTC )`**: Labour matters involving state road transport corporations.
- **`(S, KSRTC )`**: Service disputes (dismissal, suspension, promotion, seniority).
- **`(L, PG )`**: Payment of Gratuity appeals.
- **`(GM, TEN )`**: General Miscellaneous Commercial Tenders (stall allotments, fuel tenders).
- **Jurisdictional Gates**: MFA motor accident appeals (`MV, I`, `MV, D`) and Land Revenue disputes (`KLR, RES`, `LA, RES`, `SLAO`) are strictly excluded from the cause list table.

### 7.3 Corporation Boundary Enforcement & Multi-Respondent Rules
- **Sibling RTC Exclusion**: Zero false positives from sibling corporations (**BMTC**, **KKRTC**, **NEKRTC**, or Central Bengaluru **KSRTC**).
- **Multi-Respondent Safeguard**: In state-wide policy petitions where multiple transport corporations are arrayed together (e.g. `R1: State of Karnataka`, `R2: BMTC`, `R3: NWKRTC`), NWKRTC is identified and preserved.
- **Territorial Operating Hub Detection**: Identifies NWKRTC divisional references (`HUBBALLI`, `HUBLI`, `BELAGAVI`, `BAGALKOT`, `GADAG`, `HAVERI`, `UTTARA KANNADA`, `CHIKKODI`, `SIRSI`, `GOKUL ROAD`) across all three benches even if the court registry omitted the abbreviated acronym `NWKRTC`.

### 7.4 Zero-Database File Caching Engine
- Cause list results are serialized to JSON in `App_Data/causelist_cache/`:
  - `highcourt_KAHC01_{yyyy-MM-dd}.json`
  - `highcourt_KAHC02_{yyyy-MM-dd}.json`
  - `highcourt_KAHC03_{yyyy-MM-dd}.json`
- Operates strictly with **zero database intervention**, protecting core transactional tables while guaranteeing high-speed load times.

### 7.5 Background Sync Worker (`HighCourtCauseListBackgroundSyncService`)
- Runs continuously every 30 minutes.
- **Working Day Morning Routine**: Pre-caches today's cause lists across all three benches.
- **Evening Routine (after 16:30 hrs)**: Automatically pre-caches tomorrow's cause lists across all three benches.
- **Automated Cache Pruning**: Automatically deletes stale historical cache files to keep storage clean.

---

## 📜 8. OpenAPI Endpoints Catalog (41 Endpoints)

Full OpenAPI specifications are maintained in the repository:
- **District Courts / ICJS (19 Endpoints)**: `District Court Status API/` (`ecourt-icjs-cnr-api`, `ecourt-icjs-cause-list-api`, `ecourt-icjs-show-order-api`, etc.).
- **High Courts (22 Endpoints)**: `High Court Case Status API/` (`hc-cnr-api`, `hc-case-search-api`, `hc-order-api`, `hc-causelist-bench-api`, etc.).
