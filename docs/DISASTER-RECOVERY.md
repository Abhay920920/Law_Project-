# DISASTER RECOVERY PLAN

## 1. Disaster Scenarios & Response Runbook

| Scenario | Severity | Detection | Immediate Containment | Recovery Action | Validation |
|:---|:---|:---|:---|:---|:---|
| Primary SQL Server Hardware Failure | Critical | Health check failure (`/health/ready`), DB connection timeouts | Reroute traffic via DNS or Load Balancer to hot-standby / read replica | Restore latest Full + Diff + Log backups to secondary server, update connection string | Run health check and test user authentication |
| Application Server Crash / OOM | High | Process supervisor / IIS crash alert, HTTP 502/503 | Auto-restart worker process | Inspect memory dump, analyze Large Object Heap leaks, scale out instance | Verify `/health/live` returns HTTP 200 |
| Production Credential Compromise | Critical | Secret detected in log or external alert | Invalidate affected API keys / DB passwords immediately | Rotate database password in Secret Store and deploy updated config | Verify application connection and audit logs |
| External Government API Outage (e-Courts/NAPIX) | Medium | 5xx errors from remote gateway, logged in `CorrelationIdMiddleware` | Background service circuit breaker trips; stop sending live calls | System degrades gracefully to offline cached records; no local data corrupted | When API recovers, trigger automated retry queue |
| Accidental Bulk Data Deletion / Rogue Script | Critical | Audit log alert, missing cases reported | Immediately revoke execute permissions on suspected credentials | Perform point-in-time restore to a staging database, extract lost rows, re-insert | Verify checksums and row counts match pre-incident logs |

---

## 2. Emergency Incident Contacts & Protocols

- **Incident Commander**: Chief Law Officer / Head of IT (NWKRTC).
- **Escalation Path**: IT Operations -> Application Security Lead -> Database Administrator.
- **Communication Protocol**: Internal SRE incident bridge; automated system health alerts sent to designated admin mobile/email channels.
