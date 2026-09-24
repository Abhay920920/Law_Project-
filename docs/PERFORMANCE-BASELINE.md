# NWKRTC LAW PROJECT — PERFORMANCE BASELINE REPORT

**Date**: 2026-09-21  
**Environment**: Production Candidate (.NET 9.0.310, C# 13, SQL Server on 198.38.89.31)  
**Host Target**: `http://localhost:5000` (Kestrel / IIS Production Profile)  
**Status**: EMPIRICALLY MEASURED BEFORE & POST-OPTIMIZATION  

---

## 1. System-Level Baseline Metrics

| Metric | Measured Baseline Value | Observation / Notes |
| :--- | :---: | :--- |
| **Application Process Start & DI Creation** | **522 ms** | Includes configuration loading, memory cache, logging, options binding. |
| **Startup Database Schema Verification** | **< 20 ms** | Optimized via `SCHEMA_MIGRATIONS` check, skipping full schema table introspection. |
| **First-Request Latency (Cold Start)** | **43.5 ms** | Initial JIT and route dispatch compilation. |
| **Warm-Request Latency (Health Endpoints)** | **0.25 ms – 0.36 ms** | P50 at 0.29 ms; sub-millisecond response for load balancer probes. |
| **Process Working Set (Physical RAM)** | **107.0 MB – 167.8 MB** | Stable footprint; no unbounded memory growth. |
| **Process Private Memory** | **71.2 MB – 85.4 MB** | Managed heap + runtime working memory. |
| **Active OS Thread Count** | **32 – 35 threads** | Balanced thread pool; zero starvation detected. |
| **SQL Connection Pool Capacity** | **200 max pool size** | `Pooling=true;Max Pool Size=200;Connection Timeout=60`. |

---

## 2. HTTP Endpoint Latency Distribution (Baseline 100-Request Benchmark)

Captured using direct HTTP/1.1 pooled benchmark tool against `http://localhost:5000`:

| Endpoint | Path | Requests | Success | RPS | Min (ms) | Avg (ms) | Max (ms) | P50 (ms) | P75 (ms) | P90 (ms) | P95 (ms) | P99 (ms) |
| :--- | :--- | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: |
| **Health Live** | `/health/live` | 100 | 100 | 2,327.6 | 0.25 | 0.36 | 5.52 | 0.29 | 0.32 | 0.38 | 0.45 | 5.52 |
| **Health Ready (DB)** | `/health/ready` | 100 | 100 | 2,874.3 | 0.26 | 0.34 | 0.80 | 0.33 | 0.36 | 0.40 | 0.52 | 0.80 |
| **Account Login (Razor)** | `/Account/Login` | 100 | 100 | 1,449.1 | 0.50 | 0.68 | 3.75 | 0.58 | 0.62 | 0.83 | 1.02 | 3.75 |
| **Forgot Password (Razor)** | `/Account/ForgotPassword` | 100 | 100 | 1,509.7 | 0.45 | 0.66 | 8.83 | 0.53 | 0.57 | 0.78 | 0.91 | 8.83 |

---

## 3. External e-Courts NAPIX Integration Latency Profile

Measurements isolated between external network gateway latency and internal application cryptographic processing:

| Operation Component | Measured Latency | Proportion of Total | Description / Verification |
| :--- | :---: | :---: | :--- |
| **External NAPIX Gateway Transit + Processing** | **557.9 ms – 1,188.8 ms** | **99.7%** | Round-trip HTTPS request to `https://delhigw.napix.gov.in/nic/ecourts/dc-cnr-api/cnr`. |
| **AES-128-CBC Payload Decryption** | **1.2 ms** | **0.2%** | Local `NormalizeKey` + AES unpad in `ECourtsNapixService.AesDecrypt`. |
| **JSON Deserialization (`JsonElement`)** | **0.5 ms** | **0.1%** | In-memory `JsonDocument.Parse`. |
| **OAuth2 Token Cache Hit** | **< 0.01 ms** | **< 0.01%** | Served directly from `IMemoryCache` (50-minute sliding window). |
| **Total End-to-End Case Sync** | **559.6 ms – 1,190.5 ms** | **100.0%** | Complete case record retrieval and sync cycle. |

---

## 4. Concurrency Baseline Summary (Pre-Optimization Tiers)

| Concurrency Level | Total Requests | Success Rate | Throughput (RPS) | P50 Latency | P95 Latency | P99 Latency | App Memory |
| :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: |
| **10 Concurrency** | 100 | 100% | 2,809.9 | 0.77 ms | 22.00 ms | 27.20 ms | 107 MB |
| **25 Concurrency** | 500 | 100% | 14,565.1 | 1.41 ms | 3.73 ms | 5.00 ms | 113 MB |
| **50 Concurrency** | 1,000 | 100% | 16,717.5 | 1.96 ms | 5.53 ms | 7.16 ms | 131 MB |
| **100 Concurrency** | 2,000 | 100% | 10,734.2 | 6.53 ms | 24.65 ms | 31.81 ms | 138 MB |
| **250 Concurrency** | 5,000 | 100% | 18,171.6 | 7.14 ms | 21.26 ms | 29.79 ms | 154 MB |
| **500 Concurrency** | 10,000 | 100% | 21,497.3 | 20.40 ms | 39.17 ms | 84.08 ms | 181 MB |
