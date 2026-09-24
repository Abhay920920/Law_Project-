# NYAYA PATHA — NAPIX eCourts Integration (.NET 8)

Wraps the NAPIX/eCourts **District Court Case Status API** behind a clean internal
ASP.NET Core Web API, handling the two-layer NAPIX authentication scheme
(OAuth2 bearer token + AES-128-CBC/HMAC-SHA256 payload encryption) so the rest
of NYAYA PATHA never has to touch it directly.

## Project layout

```
NapixEcourtsApi/
├── Program.cs                          # DI wiring, Swagger, HttpClient setup
├── appsettings.json                    # Config placeholders (see below)
├── Configuration/
│   └── NapixOptions.cs                 # Strongly-typed settings binding
├── Services/
│   ├── NapixCryptoService.cs           # AES-128-CBC encrypt/decrypt + HMAC-SHA256
│   ├── NapixAuthService.cs             # OAuth2 client-credentials token, cached
│   └── NapixEcourtsClient.cs           # Calls CNR / current-status / show-business / order
├── Models/
│   └── NapixModels.cs                  # Envelope + decrypted response DTOs
└── Controllers/
    └── CasesController.cs              # REST surface for NYAYA PATHA to consume
```

## 1. Fill in real credentials

Edit `appsettings.json` (or better, override via environment variables /
`dotnet user-secrets` so real secrets never sit in source control):

```json
"Napix": {
  "ClientId": "<API Key from your NAPIX App>",
  "ClientSecret": "<API Secret from your NAPIX App>",
  "DeptId": "clonwkrtc",
  "AuthenticationKey": "<Authentication Key from e-Committee>"
}
```

`HmacSharedKey` (`15081947`) and the URLs are already correct — those are
fixed, documented values from the NAPIX spec, not per-subscriber secrets.

Using user-secrets locally instead of editing the file:
```bash
dotnet user-secrets init
dotnet user-secrets set "Napix:ClientId" "your-key"
dotnet user-secrets set "Napix:ClientSecret" "your-secret"
dotnet user-secrets set "Napix:AuthenticationKey" "your-auth-key"
```

## 2. Run it

```bash
dotnet restore
dotnet run
```

Swagger UI comes up at `https://localhost:<port>/swagger` in Development.

## 3. Endpoints exposed to the rest of your app

| Method | Route | Wraps |
|---|---|---|
| GET | `/api/cases/{cnr}` | Comprehensive case history with parsed hearings, all interim orders, final orders/judgments with direct PDF download URLs, and raw payload |
| GET | `/api/cases/{cnr}/raw` | Complete unadulterated decrypted JSON document directly from eCourts |
| GET | `/api/cases/{cnr}/orders-zip` | Automatically downloads all interim & final orders/judgments for the case as a single `.zip` archive |
| GET | `/api/cases/{cnr}/view` | Interactive web portal to view all orders/judgments in browser without downloading |
| GET | `/api/cases/{cnr}/order?orderNo=1&date=yyyy-MM-dd` | Streams the order/judgment PDF inline for in-browser viewing |
| GET | `/api/cases/{cnr}/business?date=yyyy-MM-dd` | `dc-show-business-api/showBusiness` — business transacted on date |
| POST | `/api/cases/current-status` (body: `{ "cnrs": ["...", "..."] }`) | `dc-current-status-api/currentStatus` — up to 500 CNRs |

All four handle NAPIX's documented error codes (`INVALID_CNR`, `INVALID_TOKEN`,
`RECORD_NOT_FOUND`, etc.) by surfacing them as HTTP 502 with the original
NAPIX message in the body, and basic input validation (CNR must be 16
alphanumeric chars, max 500 CNRs per current-status call) as HTTP 400.

## Design notes / things to double-check against your live account

- **`NapixAuthService` is a singleton** so the OAuth bearer token (1-hour
  validity) is cached and shared across requests instead of being re-fetched
  on every call — this matters for staying under your NAPIX rate plan
  (documented default: 100 calls/hour).
- **AES key = IV** is intentional, not a bug — that's exactly what the NAPIX
  spec (Annexure-A) specifies. `AuthenticationKey` gets right-padded/truncated
  to 16 bytes for AES-128, mirroring NAPIX's own sample `.NET` code.
- **Error-body shape**: the spec's error tables list `Status` /
  `StatusDescription` conceptually but don't show the exact live JSON key
  names for the 600/626/627/628/629/632 codes. `SendAndParseEnvelopeAsync`
  makes a best-effort guess (`status` / `statusDescription`) — **confirm the
  actual field names against a real error response from your account** and
  adjust `NapixEcourtsClient.SendAndParseEnvelopeAsync` if they differ. The
  raw body is always included in the thrown `NapixApiException` either way,
  so nothing is silently swallowed.
- **Order API** returns a decrypted **PDF byte stream**, not JSON — handled
  separately in `GetOrderPdfAsync`/`DecryptToBytes` rather than going through
  the JSON-oriented `DecryptResponseStr`.
- **Given your notes about identical undecryptable ciphertext from
  `dc-cnr-api`** regardless of payload — that's a symptom this code can't fix
  from the client side. If token generation (`NapixAuthService`) succeeds but
  every data call comes back with the same ciphertext, it strongly suggests a
  gateway-level rejection (IP not whitelisted, or the CNR product not
  actually subscribed on your app) rather than a bug in the encryption logic
  above — worth confirming with NAPIX support before debugging the crypto
  further.

## Extending

Adding another eCourts endpoint (e.g. `dc-party-name-api`) follows the same
pattern as `GetShowBusinessAsync`: build the parameter dictionary in the
documented order, call `BuildGetUrl(...)`, decrypt with `DecryptAndVerify(...)`,
deserialize the JSON shape from that endpoint's spec section.
