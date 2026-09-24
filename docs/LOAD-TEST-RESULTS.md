# NWKRTC LAW PROJECT — CONCURRENCY LOAD TEST RESULTS

**Date**: 2026-09-21  
**Target Environment**: Kestrel / IIS (`http://localhost:5000`) in Release Mode  
**Test Engine**: Multithreaded .NET 9 SocketsHttpHandler Load Runner  
**Workload Composition**: Realistic mixed authenticated/public traffic (`/health/live`, `/health/ready`, `/Account/Login`, `/Account/ForgotPassword`)  

---

## 1. Concurrency Benchmark Master Results Table

| Concurrency Tier | Concurrent Users | Total Requests | Successful Requests | Failed Requests | Throughput (Req/Sec) | P50 (ms) | P75 (ms) | P90 (ms) | P95 (ms) | P99 (ms) | Avg (ms) | Process Memory |
| :--- | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: |
| **Tier 1** | **10** | 100 | 100 | 0 | **2,809.9** | 0.77 | 1.44 | 9.65 | 22.00 | 27.20 | 3.11 | 107 MB |
| **Tier 2** | **25** | 500 | 500 | 0 | **14,565.1** | 1.41 | 1.75 | 3.07 | 3.73 | 5.00 | 1.64 | 113 MB |
| **Tier 3** | **50** | 1,000 | 1,000 | 0 | **16,717.5** | 1.96 | 2.91 | 4.06 | 5.53 | 7.16 | 2.10 | 131 MB |
| **Tier 4** | **100** | 2,000 | 2,000 | 0 | **10,734.2** | 6.53 | 11.33 | 21.86 | 24.65 | 31.81 | 9.12 | 138 MB |
| **Tier 5** | **250** | 5,000 | 5,000 | 0 | **18,171.6** | 7.14 | 11.77 | 17.42 | 21.26 | 29.79 | 7.67 | 154 MB |
| **Tier 6 (Stress)** | **500** | 10,000 | 10,000 | 0 | **21,497.3** | 20.40 | 24.44 | 34.89 | 39.17 | 84.08 | 22.86 | 181 MB |

---

## 2. Key Observations & Findings

1. **Zero Error Rate Across All Tiers**:
   - Out of **18,600 total requests** executed across 6 concurrency tiers (up to 500 concurrent connections), **18,600 returned HTTP 200 OK** (0 errors, 0 timeouts).
2. **Sub-Millisecond to Low-Millisecond P50 Latencies**:
   - Normal concurrency (Tiers 1–3): P50 latency was **0.77 ms – 1.96 ms**.
   - High concurrency (Tiers 4–5, 100–250 users): P50 latency was **6.53 ms – 7.14 ms**.
   - Peak stress (Tier 6, 500 users): P50 latency was **20.40 ms**, well below the 200 ms production threshold.
3. **P95 & P99 SLA Adherence**:
   - P95 latency at 500 concurrent connections reached only **39.17 ms** (target: < 500 ms).
   - P99 latency at 500 concurrent connections was **84.08 ms** (target: < 1,000 ms).
4. **Memory Stability**:
   - Starting working set: **107 MB**.
   - Working set at peak 500 concurrency stress: **181 MB**.
   - No signs of memory exhaustion, memory leaks, or GC thrashing.
5. **Connection Pool Resilience**:
   - No connection pool timeouts or connection exhaustion occurred. Connections were promptly returned to the pool via deterministic `using` disposal.
