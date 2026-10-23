# 🌐 NapixEcourtsApi — Microservice Specification

> **Component**: Dedicated ASP.NET Core Web API proxy service for National Informatics Centre (NIC) e-Courts NAPIX Gateway integration.  
> **Master Documentation**: See [`docs/ECOURTS_INTEGRATION_GUIDE.md`](../docs/ECOURTS_INTEGRATION_GUIDE.md)

---

## 📌 Overview

**`NapixEcourtsApi`** is an auxiliary microservice designed to handle secure outbound communication with the **NIC NAPIX Gateway**. It offloads:
1. **OAuth2 Bearer Token Lifecycle**: Token acquisition, in-memory caching, expiration checking, and automatic refresh on HTTP 401.
2. **Payload Cryptography**:
   - AES-128-CBC encryption of request query parameters.
   - HMAC-SHA256 request signing with secret keys.
   - AES-128-CBC decryption of encrypted court response payloads.
3. **Court Endpoint Proxying**: Proxies queries for both District Courts / ICJS (19 endpoints) and High Courts (22 endpoints).

---

## 🛠️ Configuration (`appsettings.json`)

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "eCourts": {
    "DeptId": "clonwkrtc",
    "AuthKey": "tD7Ju6n0Cdf4vxUo",
    "HmacKey": "15081947",
    "Version": "v1.0",
    "GatewayUrl": "https://delhigw.napix.gov.in/nic/ecourts",
    "OAuthTokenUrl": "https://delhigw.napix.gov.in/nic/ecourts/oauth2/token"
  }
}
```

---

## 🚀 Running the Microservice Locally

```bash
cd NapixEcourtsApi
dotnet run
```

The service will listen on `http://localhost:5002` (or configured port). For comprehensive API specifications, see [`docs/ECOURTS_INTEGRATION_GUIDE.md`](../docs/ECOURTS_INTEGRATION_GUIDE.md).
