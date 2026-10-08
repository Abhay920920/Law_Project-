# 🌐 e-Courts NAPIX API Gateway & Integration Specification

> **Gateway Infrastructure**: National Informatics Centre (NIC) e-Courts NAPIX Platform  
> **Supported Courts**: District Courts / ICJS (19 APIs) & High Courts (22 APIs)  
> **C# Service Implementation**: [`ECourtsNapixService.cs`](file:///c:/Law%20Project/Law%20Project/MVCCaseManagement/Utils/ECourtsNapixService.cs)  
> **Client Controller**: [`ECourtsController.cs`](file:///c:/Law%20Project/Law%20Project/MVCCaseManagement/Controllers/ECourtsController.cs)  
> **Frontend Integration**: [`ecourts-details.js`](file:///c:/Law%20Project/Law%20Project/MVCCaseManagement/wwwroot/js/ecourts-details.js)

---

## 📌 1. Integration Architecture Overview

The **Law Project** integrates directly with the official **Indian e-Courts System** via the **NIC NAPIX Gateway**. This integration enables automated 16-digit CNR auto-linking, real-time case status lookups, hearing date synchronizations, judgment order views, and cause list fetches for both District Courts (ICJS) and High Courts.

```
+-----------------------------------+                           +-----------------------------------+
|       ASP.NET Core App            |                           |       NIC e-Courts NAPIX          |
|     (ECourtsNapixService)         |                           |          Gateway API              |
|                                   |                           |      (delhigw.napix.gov.in)       |
+-----------------------------------+                           +-----------------------------------+
                  |                                                               |
                  |---------------- 1. OAuth2 Token Fetch (Basic Auth) ---------->|
                  |<--------------- 2. Access Token (Cached 50m in IMemoryCache) -|
                  |                                                               |
                  |---------------- 3. AES-128-CBC Payload + HMAC-SHA256 Token -->|
                  |<--------------- 4. Decrypted response_str Payload ------------|
                  |                                                               |
                  |================ [NAPIX Quota Tracker Enforced] ===============|
                  |  - Rolling 1-hr quota limit (1000 calls/hr)                   |
                  |  - Audit logging to dbo.NAPIX_API_CALLS                       |
```

---

## 🔑 2. Cryptographic Security Protocol

The NAPIX Gateway enforces strict enterprise security controls requiring OAuth2 bearer authentication, payload encryption, and request signing.

### 2.1 Configuration Keys (`appsettings.json`)
```json
"eCourts": {
  "ApiKey": "d0dbb05f2a364983ceaadd49765b1cea",
  "SecretKey": "27ab3841f3d3780daab222f0ba609a0b",
  "DeptId": "clonwkrtc",
  "HmacKey": "15081947",
  "AuthKey": "tD7Ju6n0Cdf4vxUo",
  "IV": "tD7Ju6n0Cdf4vxUo",
  "Version": "v1.0",
  "GatewayUrl": "https://delhigw.napix.gov.in/nic/ecourts",
  "OAuthTokenUrl": "https://delhigw.napix.gov.in/nic/ecourts/oauth2/token"
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
| `KAHC` | Karnataka High Court | `29` | High Court Bench | Bench Code |
| `MHBOM` | Maharashtra | `27` | Mumbai | `1` |
| `DL` | Delhi | `7` | Delhi Courts | District Code |

---

## 📜 4. Official OpenAPI Endpoint Directory

The monorepo catalogs all official OpenAPI specs under [`District Court Status API/`](file:///c:/Law%20Project/District%20Court%20Status%20API) and [`High Court Case Status API/`](file:///c:/Law%20Project/High%20Court%20Case%20Status%20API).

### 4.1 District Court / ICJS APIs (19 Endpoints)

| Endpoint File | Base Path & Action | Description |
| :--- | :--- | :--- |
| `ecourt-icjs-cnr-api_1.0.0.json` | `ecourt-icjs-cnr-api/CNR` | Query full case history, party names, and hearing history by 16-digit CNR. |
| `ecourt-icjs-cnr-current-status-api_1.0.0.json` | `ecourt-icjs-cnr-current-status-api/CurrentStatus` | Lightweight query returning current stage, next hearing date, and court room. |
| `ecourt-icjs-case-number-api_1.0.0.json` | `ecourt-icjs-case-number-api/caseNumber` | Search CNR & case details by case type, registration number, and year. |
| `ecourt-icjs-cause-list-api_1.0.0.json` | `ecourt-icjs-cause-list-api/CauseList` | Fetch daily civil/criminal cause lists for a given establishment and date. |
| `ecourt-icjs-show-business-api_1.0.0.json` | `ecourt-icjs-show-business-api/showBusiness` | Fetch court daily business proceedings and hearing logs. |
| `ecourt-icjs-show-order-api_1.0.0.json` | `ecourt-icjs-show-order-api/showOrder` | Retrieve interim and final order PDF URLs. |
| `ecourt-icjs-state-master-api_1.0.0.json` | `ecourt-icjs-state-master-api/StateMaster` | List all state codes across India. |
| `ecourt-icjs-district-master-api_1.0.0.json` | `ecourt-icjs-district-master-api/DistrictMaster` | List all district codes for a selected state. |
| `ecourt-icjs-court-complex-api_1.0.0.json` | `ecourt-icjs-court-complex-api/CourtComplex` | List court complexes and establishment codes (`est_code`). |
| `ecourt-icjs-casetype-master-api_1.0.0.json` | `ecourt-icjs-casetype-master-api/caseTypeMaster` | Master list of case types (MVC, ID, LCA, EP, Execution). |
| `ecourt-icjs-court-judge-list-api_1.0.0.json` | `ecourt-icjs-court-judge-list-api/CourtJudgeList` | List active judge designations and court room numbers. |
| `ecourt-icjs-chargesheet-status-api_1.0.0.json` | `ecourt-icjs-chargesheet-status-api/chargesheetStatus` | Police chargesheet status integration. |
| `ecourt-icjs-fir-status-api_1.0.0.json` | `ecourt-icjs-fir-status-api/firStatus` | FIR status lookup for motor accident & criminal cases. |
| `ecourt-icjs-pretrial-order-details-api_1.0.0.json` | `ecourt-icjs-pretrial-order-details-api/PretrialOrderDetails` | Pre-trial hearing orders. |
| `ecourt-icjs-pretrial-order-view-api_1.0.0.json` | `ecourt-icjs-pretrial-order-view-api/PretrialOrderView` | Pre-trial order document downloads. |
| `ecourt-icjs-act-master-api_1.0.0.json` | `ecourt-icjs-act-master-api/actMaster` | Master catalog of legal Acts (Motor Vehicles Act, ID Act). |
| `ecourt-icjs-convicted-details-api_1.0.0.json` | `ecourt-icjs-convicted-details-api/ConvictedDetails` | Conviction records and judgment outcomes. |
| `ecourt-icjs-remand-bail-accused-api_1.0.0.json` | `ecourt-icjs-remand-bail-accused-api/RemandBailAccused` | Remand and bail status. |
| `ecourt-icjs-undertrial-prisoner-tagged-api_1.0.0.json` | `ecourt-icjs-undertrial-prisoner-tagged-api/UndertrialPrisonerTagged` | Undertrial prisoner tracking. |

---

### 4.2 High Court APIs (22 Endpoints)

| Endpoint File | Base Path & Action | Description |
| :--- | :--- | :--- |
| `hc-cnr-api_1.0.0.json` | `hc-cnr-api/CNR` | High Court case status by 16-digit High Court CNR (`KAHC...`). |
| `hc-current-status-api_1.0.0.json` | `hc-current-status-api/currentstatus` | Live status, stage, next date, and bench assignment for High Court appeals. |
| `hc-case-search-api_1.0.0.json` | `hc-case-search-api/casesearch` | Search High Court MFA/WP cases by type, number, and year. |
| `hc-causelist-details-api_1.0.0.json` | `hc-causelist-details-api/CauselistDetails` | High Court daily bench cause lists. |
| `hc-causelist-bench-api_1.0.0.json` | `hc-causelist-bench-api/causelistbench` | List active High Court benches for cause lists. |
| `hc-order-api_1.0.0.json` | `hc-order-api/order` | High Court interim orders and final judgment documents. |
| `hc-advocate-name-api_1.0.0.json` | `hc-advocate-name-api/advocatename` | Search High Court cases by advocate name. |
| `hc-advocate-bar-reg-api_1.0.0.json` | `hc-advocate-bar-reg-api/advocatebarreg` | Search High Court cases by Bar Registration Number. |
| `hc-party-name-api_1.0.0.json` | `hc-party-name-api/partyname` | Search High Court cases by petitioner or respondent corporation name. |
| `hc-bench-master-api_1.0.0.json` | `hc-bench-master-api/bench` | Master list of High Court benches (Principal Bench Bengaluru, Dharwad Bench, Kalaburagi Bench). |
| `hc-case-type-master-api_1.0.0.json` | `hc-case-type-master-api/casetypemaster` | High Court case types (MFA, WP, WA, SLP, CCC). |
| `hc-caveat-details-api_1.0.0.json` | `hc-caveat-details-api/caveatdetails` | Caveat petition search and status. |
| `hc-caveat-name-api_1.0.0.json` | `hc-caveat-name-api/caveatname` | Caveat search by applicant name. |
| `hc-show-business-api_1.0.0.json` | `hc-show-business-api/showBusiness` | High Court daily business hearing logs. |
| `hc-show-causelist-api_1.0.0.json` | `hc-show-causelist-api/showcauselist` | Interactive High Court cause list renderer. |
| `hc-filing-api_1.0.0.json` | `hc-filing-api/filing` | High Court defective filing & compliance status. |
| `hc-state-api_1.0.0.json` | `hc-state-api/state` | High Court state code master. |
| `hc-district-api_1.0.0.json` | `hc-district-api/district` | High Court district jurisdiction master. |
| `hc-act-master-api_1.0.0.json` | `hc-act-master-api/actmaster` | High Court legal act master. |
| `hc-act-details-api_1.0.0.json` | `hc-act-details-api/actdetails` | Act and section details for High Court writs. |
| `hc-view-citation-order_1.0.0.json` | `hc-view-citation-order/viewcitationorder` | High Court reported citation orders. |
| `overview_1.0.0.json` | `overview` | General API metadata and version details. |

---

## 💻 5. Controller & Client-Side UI Workflow

### 5.1 Controller Routes (`ECourtsController.cs`)
- `GET /ECourts/GetCnrDetails?cnrNumber=KAUKA20006722017`: JSON API returning full case details from e-Courts.
- `GET /ECourts/GetOrders?cnrNumber=KAUKA20006722017`: Returns list of order dates and PDF download links.
- `POST /ECourts/AutoLinkCnr`: Auto-discovers and saves 16-digit CNR to local `MVC_CASES` or `LABOUR_CASES` table.
- `POST /ECourts/SyncCaseStatus`: Pulls live hearing date and stage from e-Courts and updates local record.

### 5.2 Frontend UI (`ecourts-details.js`)
- Renders live e-Courts badge next to case numbers in Razor views.
- Clicking **"View e-Courts Status"** triggers an async AJAX modal displaying:
  - Live Court & Judge Name
  - Next Hearing Date & Stage
  - Petitioner & Respondent Advocates
  - Daily Business Log & Order PDF Viewers
