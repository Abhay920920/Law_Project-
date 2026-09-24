# API SECURITY SPECIFICATION

## 1. Scope & Architecture

The application exposes both internal web API controllers and external gateway integrations. All API endpoints must adhere to strict zero-trust security standards:

- Authentication required on all business endpoints (no anonymous access to case details, CNR lookups, or PDF streams).
- Anti-CSRF verification for all state-changing browser requests.
- Strict input validation and sanitization.
- Correlation ID propagation for audit tracing and observability.

---

## 2. API Endpoints Hardening Summary

| Endpoint | Method | Original Auth | Hardened Auth | Implemented Protections |
|:---|:---:|:---:|:---:|:---|
| `/api/cases/{cnr}` | GET | `[AllowAnonymous]` | `[Authorize]` | CNR format validation (16 chars), rate limiting |
| `/api/cases/{cnr}/raw` | GET | `[AllowAnonymous]` | `[Authorize]` | Sanitized JSON output, sensitive data masking |
| `/api/cases/{cnr}/order` | GET | `[AllowAnonymous]` | `[Authorize]` | PDF stream with safe Content-Disposition headers |
| `/api/cases/{cnr}/view` | GET | `[AllowAnonymous]` | `[Authorize]` | CSP frame restrictions, sanitized parameters |
| `/ECourts/GetCnrRaw` | GET | `[AllowAnonymous]` | `[Authorize]` | Authentication check, case existence validation |
| `/ECourts/DownloadAllOrdersZip` | GET | `[AllowAnonymous]` | `[Authorize]` | Division ownership check, ZIP bomb limit (50MB) |
| `/ECourts/UploadOrderPdf` | POST | `[AllowAnonymous]` | `[Authorize]` | Magic byte verification, Anti-Forgery token, 20MB limit |
| `/Notification/MarkAsRead` | POST | Anonymous | `[Authorize]` | CSRF token, user ownership verification |

---

## 3. Rate Limiting & Abuse Prevention

- Authenticated users are limited to 100 requests per minute on external sync endpoints (`/ECourts/LiveSync*`, `/api/cases/*`).
- IP-based rate limiting on login attempts to prevent brute-force attacks against user credentials.
