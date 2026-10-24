# 🌐 NWKRTC Law Management System — Integration Map

**Document Reference**: External System Gateways, Cryptographic Protocols, Schemas & Quotas  
**Verification Level**: **VERIFIED IN SOURCE & TEST**  

---

## 1. NIC e-Courts NAPIX Gateway v1.3

- **Purpose**: Direct real-time and background synchronization with the National Informatics Centre (NIC) e-Courts system for High Court and District Court case records.
- **Microservice / Client**: `NapixEcourtsClient.cs` & `ECourtsNapixService.cs`.
- **Cryptographic Specifications**:
  - **Payload Encryption**: AES-128-CBC (`CipherMode.CBC`, `PaddingMode.PKCS7`).
  - **Initialization Vector (IV)**: Static 16-byte IV mandated by NIC v1.3 government API specification.
  - **Request Signing**: HMAC-SHA256 computed over the encrypted payload using client secret key.
  - **Transport Security**: TLS 1.2 / TLS 1.3.
- **Endpoints Implemented**:
  1. `POST /api/v1/cases/cnr`: Query complete case history, hearings, orders, and advocates by 16-character CNR Number.
  2. `POST /api/v1/cases/causelist`: Download daily cause list proceedings for Karnataka High Court (Dharwad, Bengaluru, Kalaburagi).
  3. `POST /api/v1/cases/sync`: Push status updates and verify decree satisfaction.
- **Throttling & Quotas**:
  - Maximum 20 calls/minute sliding rate limit.
  - Maximum 1,000 calls/hour budget recorded in `NAPIX_API_QUOTA`.
  - Circuit Breaker trips after 5 consecutive 5xx/timeout errors; half-open retry after 60 seconds.

---

## 2. Karnataka Mobile-1 Government SMS Gateway

- **Purpose**: Real-time delivery of password reset One-Time Passwords (OTP) and automated morning court hearing reminders to advocates and corporate officers.
- **Service Class**: `SmsService.cs` (invoked by `AccountController` and `NotificationBackgroundService`).
- **Transport**: HTTPS POST to `https://mobile1.karnataka.gov.in/sms/send`.
- **Payload Parameters**:
  - `username`: Registered corporate gateway account.
  - `password` / `secureKey`: Provisioned via environment variables (`SMS__Password`, `SMS__SecureKey`).
  - `senderId`: Registered DLT Sender ID (`NWKRTC`).
  - `templateId`: DLT approved hearing reminder and OTP templates.
  - `mobileNumber`: 10-digit Indian mobile number.
  - `message`: Formatted Marathi/Kannada/English notification text.
- **Audit Logging**: Every outbound SMS attempt is recorded in `tbl_SMS_Logs` with timestamp, recipient, delivery status, and gateway transaction ID.

---

## 3. Nyaya Patha Local AI Subsystem (Ollama)

- **Purpose**: Air-gapped, privacy-preserving legal research, petition drafting assistant, and petty bill duplicate auditing.
- **Architecture**: 100% on-premise execution; zero data leaves the corporate infrastructure.
- **Local Daemon**: Runs on `http://localhost:11434`.
- **Model Ingested**: `Qwen2.5:7b-instruct` (4-bit quantized GGUF).
- **RAG Pipeline**:
  - Embedding Model: Local embeddings via cosine similarity search over Karnataka High Court transport law judgments.
  - System Prompt Grounding: Context assembled by `CaseContextBuilder.cs` strictly filtered by user `DivisionID`.
  - Token Management: Pruned to maximum 4,096 context tokens to guarantee deterministic generation latency (< 2.5s time-to-first-token).

---

## 4. TR-18 Accident Intelligence Portal Integration

- **Purpose**: Integration with the Karnataka State Police TR-18 road accident database.
- **Status in Source**: Configuration hooks provisioned in `appsettings.json` (`AppSettings:TR18ApiKey`); repository methods implemented for vehicle accident number lookups; live external endpoint currently in pre-production pilot.
