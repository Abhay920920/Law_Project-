# REMAINING OPERATIONAL RISKS & RECOMMENDATIONS

## 1. External Infrastructure Dependencies

1. **Government e-Courts & NAPIX Availability**:
   - *Risk*: The application relies on external government gateways (`delhigw.napix.gov.in`, `services.ecourts.gov.in`). These external endpoints occasionally undergo unannounced maintenance or experience intermittent network latency.
   - *Mitigation Implemented*: Local data is insulated against remote failure; circuit breakers and 30-second timeouts prevent worker stalling.
   - *Operational Recommendation*: Implement external uptime monitoring and establish an SLA with NIC/NAPIX administrators.

2. **Telecommunications SMS Gateway Service**:
   - *Risk*: SMS delivery rates depend on telecommunications operators, DLT template approvals (TRAI guidelines in India), and provider account balance.
   - *Operational Recommendation*: Ensure that all SMS templates registered on the SMS portal match the exact template IDs configured in `appsettings.json`.

---

## 2. Infrastructure & Hosting Recommendations

1. **Secret Store Integration**:
   - For cloud/enterprise on-premises hosting, inject database credentials and API secrets via Azure Key Vault, AWS Secrets Manager, or HashiCorp Vault rather than plain environment variables.
2. **Automated Database Backups**:
   - Ensure the SQL Server Agent job executing the daily and transaction log backups is active, and schedule an automated weekly restore-verification run.
3. **Log Aggregation**:
   - Ingest structured application logs (with correlation IDs) into a centralized SIEM / log aggregator (e.g., Elasticsearch/Kibana, Datadog, or Azure Application Insights) for real-time alerting.
