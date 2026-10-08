# Nyaya Patha AI — Production Deployment & Operations Guide

## Executive Overview
**Nyaya Patha AI (ನ್ಯಾಯ ಪಥ)** is the production-grade conversational legal research assistant integrated natively into the **NWKRTC Law Project (MVCCaseManagement)**.

It adheres to the strict operational principle:
> **SEARCH FIRST → VERIFY → RANK → DETECT CONFLICTS → REASON → CITE → ANSWER**

Nyaya Patha is fully provider-agnostic, supporting on-premises local open-weight inference via **Ollama** as primary, with policy-governed fallback options (**OpenRouter**, **Anthropic Claude**), an instant **AI Kill Switch**, comprehensive **rate limiting**, **conflict disclosure**, and audit logging.

---

## 1. Hardware Detection & Topology Analysis

### Actual Host Hardware Profile (Audited)
- **CPU**: Intel Core i5-12500 (6 Cores / 12 Logical Processors, 3.0–4.6 GHz)
- **Total System RAM**: 16.0 GB (15.7 GB usable)
- **GPU**: Intel UHD Graphics 770 (Integrated GPU; no dedicated NVIDIA CUDA VRAM)
- **OS**: Windows (Server / Pro) with IIS 10.0 + ASP.NET Core 9.0 Module

### Model Sizing & Hardware Recommendations

| Topology Option | Hardware Profile | Recommended Model | RAM/VRAM Footprint | Expected Speed | Suitability |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **Option A: Single Host (CPU Only)** | 6 Cores, 16 GB RAM, No GPU *(Current Host)* | `qwen2.5:7b-instruct-q4_K_M` or `llama3.2:3b` | 4.2 GB – 4.8 GB RAM | 8–15 tokens/sec | **Recommended for Current Host** (Leaves 11 GB for SQL Server & IIS) |
| **Option A (Sub-optimal)** | 6 Cores, 16 GB RAM, No GPU | `qwen2.5:14b` | 9.2 GB RAM | 1.8–3.5 tokens/sec | **Not Recommended for CPU Only** (Heavy RAM pressure on SQL Server) |
| **Option B: Dual-Tier Architecture** | Separate Internal AI Server (RTX 3090/4090 or A4000/A5000) | `qwen2.5:14b` or `deepseek-r1:14b` | 10 GB – 16 GB VRAM | 45–70 tokens/sec | **Recommended Enterprise Topology** |

> [!IMPORTANT]
> **CPU-Only Sizing Rule**: On a 16 GB machine running SQL Server, IIS, and the application, running a 14B model directly on CPU will cause high memory pressure and slow responses. If running Ollama on the same server, deploy `qwen2.5:7b` (`ollama pull qwen2.5:7b`). If higher parameter models (14B/32B) are required, adopt the **Dual-Tier Architecture** with a dedicated internal GPU server.

---

## 2. Architecture Topologies

### Topology A: Single-Host Local Deployment
Both ASP.NET Core and Ollama run on the same Windows server:
```
[ Browser Client ]
       │ HTTPS (443)
       ▼
 [ IIS / Kestrel ] ───▶ [ SQL Server (Admin_Law) ]
       │ HTTP (127.0.0.1:11434)
       ▼
 [ Ollama Service ] ───▶ [ Local Model: qwen2.5:7b ]
```

### Topology B: Dual-Tier Enterprise AI Server Deployment
The Web Application runs on the existing IIS server, while Ollama runs on a dedicated internal compute server within the NWKRTC intranet:
```
[ Browser Client ]
       │ HTTPS (443)
       ▼
 [ Web Server: 198.38.89.x ] ───▶ [ SQL Server: 198.38.89.31 ]
       │ HTTP (10.x.x.x:11434 or 192.168.x.x:11434)
       ▼
 [ Internal AI Server (GPU) ] ───▶ [ Ollama: qwen2.5:14b / deepseek-r1:14b ]
```

---

## 3. Ollama Installation & Service Configuration

### Step 1: Install Ollama on Windows
1. Download the Windows installer from `https://ollama.com/download/OllamaSetup.exe`.
2. Run the installer as Administrator.
3. Verify installation in PowerShell:
   ```powershell
   ollama --version
   ```

### Step 2: Configure Ollama as a Windows Background Service
By default, Ollama binds to `127.0.0.1:11434`. If using Dual-Tier topology, configure Ollama to listen on all interfaces:
1. Open System Properties → Environment Variables.
2. Under **System Variables**, add:
   - `OLLAMA_HOST` = `0.0.0.0:11434`
   - `OLLAMA_ORIGINS` = `*`
   - `OLLAMA_NUM_PARALLEL` = `4`
   - `OLLAMA_MAX_LOADED_MODELS` = `1`
3. Restart the Ollama service:
   ```powershell
   Stop-Process -Name "ollama" -Force
   Start-Process "ollama" -ArgumentList "serve"
   ```

### Step 3: Download the Legal Model
For single-host CPU deployment:
```powershell
ollama pull qwen2.5:7b
```
For dual-tier GPU deployment:
```powershell
ollama pull qwen2.5:14b
```
Verify the model is loaded:
```powershell
ollama list
```

---

## 4. ASP.NET Core & IIS Configuration

### Step 1: Configuration in `appsettings.json` / `appsettings.Production.json`
```json
{
  "AI": {
    "Enabled": true,
    "Provider": "Ollama",
    "Model": "qwen2.5:7b",
    "BaseUrl": "http://127.0.0.1:11434",
    "TimeoutSeconds": 120,
    "AllowExternalProviders": false,
    "FallbackProvider": "OpenRouter",
    "MaxConcurrentRequests": 5,
    "MaxInputLength": 4000
  },
  "NyayaPathaAI": {
    "IsEnabled": true,
    "MaxContextTokens": 100000,
    "EnableDocumentExtraction": true,
    "MaxPagesPerDocument": 15,
    "EnableLegalSearch": true
  }
}
```

### Step 2: Environment Variables (Production Security)
Never store production secrets in source control. Set the following Windows System Environment Variables on the production server:

| Environment Variable | Recommended Production Value | Description |
| :--- | :--- | :--- |
| `AI__Enabled` | `true` | Master AI feature toggle |
| `AI__Provider` | `Ollama` | Primary reasoning engine |
| `AI__Model` | `qwen2.5:7b` (or `qwen2.5:14b`) | Target legal model |
| `AI__BaseUrl` | `http://127.0.0.1:11434` (or internal IP) | Ollama REST endpoint |
| `AI__AllowExternalProviders` | `false` | Strict zero-cloud data leak policy |
| `AI__TimeoutSeconds` | `120` | Maximum request duration |
| `AI__MaxConcurrentRequests` | `5` | Semaphore throttling limit |

### Step 3: IIS Application Pool Settings
1. Open **IIS Manager** (`inetmgr`).
2. Select Application Pool `MVCCaseManagementPool`.
3. Click **Advanced Settings**:
   - **Start Mode**: `AlwaysRunning`
   - **Idle Time-out (minutes)**: `0` (Prevents unloading between legal queries)
   - **Recycling → Regular Time Interval (minutes)**: `0` (Recycle only during maintenance windows)
   - **Rapid-Fail Protection**: Set Maximum Failures = `5`, Failure Interval = `5` minutes.
4. Verify Request Limits:
   - Request Timeout: `180 seconds` (allows deep legal research across all 7 sources).

---

## 5. Security & Authorization Boundary

### Central Office Leadership Policy Matrix
Nyaya Patha AI strictly enforces the following server-side security authorization:
```csharp
(DivisionID == 5 || DivisionID == 0)
AND
(Role == "Dy CLO" || Role == "DyCLO" || Role == "CLO" || Role == "MD")
```
- **Unauthorized Roles** (LO, CO, Admin, Regional Case Workers):
  - Received `HTTP 403 Forbidden` server-side before any database or document query is executed.
  - UI navigation menu is automatically hidden.
- **Untrusted Reference Material Policy**:
  - System prompt rigorously commands the LLM to treat all documents, e-Courts texts, web queries, and database records as raw evidence, actively neutralizing prompt injection attacks.
- **Fail-Closed Privacy Guard**:
  - `AllowExternalProviders = false` prevents external transmission to cloud APIs unless explicitly enabled by leadership.

---

## 6. Health Check & Diagnostics

The production health probe is available at:
```http
GET /NyayaPatha/Health
```

### Sample Responses:

#### Healthy (Operational):
```json
{
  "status": "Healthy",
  "isEnabled": true,
  "provider": "Ollama",
  "configuredModel": "qwen2.5:7b",
  "isProviderReachable": true,
  "isDatabaseConnected": true,
  "isECourtsConfigured": true,
  "allowExternalProviders": false,
  "activeConcurrentRequests": 0,
  "message": "Nyaya Patha AI operational with Ollama (qwen2.5:7b).",
  "timestamp": "2026-10-07T11:05:00Z"
}
```

#### Degraded (Ollama Unreachable, Core Operations Intact):
```json
{
  "status": "Degraded",
  "isEnabled": true,
  "provider": "Ollama",
  "configuredModel": "qwen2.5:7b",
  "isProviderReachable": false,
  "isDatabaseConnected": true,
  "isECourtsConfigured": true,
  "allowExternalProviders": false,
  "activeConcurrentRequests": 0,
  "message": "Ollama inference service is unreachable at configured endpoint. Verified database records remain operational.",
  "timestamp": "2026-10-07T11:05:00Z"
}
```

#### Disabled (Kill Switch Engaged):
```json
{
  "status": "Disabled",
  "isEnabled": false,
  "provider": "Ollama",
  "configuredModel": "qwen2.5:7b",
  "message": "Nyaya Patha AI is currently disabled in system configuration (Kill switch engaged)."
}
```

---

## 7. Emergency Rollback & Kill Switch Procedure

If unexpected behavior, resource exhaustion, or host maintenance occurs:

### Immediate Kill Switch Activation (Zero Downtime)
Set either of the following environment variables or `appsettings.json` keys:
```json
"AI": {
  "Enabled": false
}
```
**Impact of Kill Switch**:
1. Navigation link disappears from `_Layout.cshtml`.
2. `/NyayaPatha` renders the clean deactivation page (`Views/NyayaPatha/Disabled.cshtml`).
3. `/NyayaPatha/Chat` immediately returns `{ success: false, errorMessage: "Nyaya Patha AI is currently disabled in system settings." }`.
4. No background AI threads or Ollama calls are dispatched.
5. All regular case management features (MVC, Labour, Appeals, Gratuity, e-Courts sync, SMS) continue operating with 100% normality.

---

## 8. Troubleshooting Guide

| Symptom | Probable Cause | Corrective Action |
| :--- | :--- | :--- |
| **"Local Ollama service unreachable at http://..."** | Ollama background process is stopped or port blocked | Run `Get-Process ollama` in PowerShell. If stopped, start via `ollama serve`. Verify port `11434` with `Test-NetConnection -ComputerName 127.0.0.1 -Port 11434`. |
| **"Inference timeout after 120 seconds"** | Model is too large for CPU-only inference | Switch model from 14B to `qwen2.5:7b` in `AI:Model` or increase `AI:TimeoutSeconds` to `180`. |
| **"Context payload exceeds maximum allowable length"** | User query or loaded case exhibits exceeded token bounds | Refine case scope or reduce `MaxPagesPerDocument` in `NyayaPathaAI:MaxPagesPerDocument`. |
| **"The AI assistant is currently processing maximum legal research requests"** | All 5 concurrency slots are active | The built-in rate-limiting semaphore protected the server. Request will process once active queries complete. |
| **"External cloud LLM transmission is disabled by security policy"** | Local Ollama failed and fallback was attempted, but external transmission is disallowed | Set `AI:AllowExternalProviders = true` only if leadership explicitly approves external cloud API transmission. Otherwise, restart Ollama. |
