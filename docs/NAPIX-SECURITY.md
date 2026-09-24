# NAPIX & E-COURTS INTEGRATION SECURITY

## 1. Integration Architecture

The NWKRTC Law Project interfaces with the Government of India National Data Sharing and Accessibility Platform (NAPIX) and the Supreme Court / High Court / District Court e-Courts APIs to synchronize live case status, hearing dates, court halls, and judicial orders.

---

## 2. Cryptographic Security Standards

1. **HMAC-SHA256 Request Signing**:
   - Outgoing requests to NAPIX endpoints include an HMAC-SHA256 signature generated over request timestamps and payloads using the configured client secret.
   - Secrets are loaded from environment variables/configuration and never logged.
2. **AES-256 Payload Decryption**:
   - Encrypted court responses from the central e-Courts server are decrypted in-memory using AES-256-CBC.
   - Keys and IVs are securely cleared after decryption.
3. **OAuth Token Caching**:
   - Access tokens obtained from the NAPIX authentication server are cached in memory with a safety margin (5 minutes before expiration) to eliminate redundant token requests while preventing expired token re-use.

---

## 3. Resilience & Safe Failure Handlers

- **Circuit Breaker & Timeouts**: All external HTTP calls to `delhigw.napix.gov.in` and `services.ecourts.gov.in` are bounded by strict 30-second timeouts.
- **Zero Local Corruption on Remote Failure**:
  - If NAPIX returns an HTTP 5xx, invalid JSON, or network timeout, local case status is never corrupted.
  - The failure is recorded in the synchronization audit table with the correlation ID, and the existing local case record remains intact.
- **Sensitive Data Scrubbing**:
  - Outgoing and incoming NAPIX request logs automatically sanitize authentication headers, access tokens, and bearer keys.
