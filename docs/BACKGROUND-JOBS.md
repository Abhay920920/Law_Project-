# BACKGROUND JOBS & ASYNCHRONOUS WORKERS

## 1. Overview

Asynchronous operations, automated e-Courts synchronization, hearing date reminders, and SMS dispatch run via `IHostedService` implementations:
- `NotificationBackgroundService.cs`: Scans for upcoming hearing dates (7 days, 3 days, tomorrow) and dispatches in-app notifications and SMS alerts to advocates and division officers.

---

## 2. Multi-Instance Safe Concurrency (Distributed Locking)

When deploying multiple application containers or web farm instances behind a load balancer, multiple background workers running simultaneously could result in duplicate SMS dispatches, duplicate alerts, and race conditions.

### Implementation:
`NotificationBackgroundService.cs` utilizes SQL Server application locking via `sp_getapplock`:

```csharp
using var conn = new SqlConnection(connectionString);
await conn.OpenAsync(cancellationToken);
using var cmd = new SqlCommand(@"
    DECLARE @res INT;
    EXEC @res = sp_getapplock 
        @Resource = 'NWKRTC_NotificationBackgroundService', 
        @LockMode = 'Exclusive', 
        @LockOwner = 'Session', 
        @LockTimeout = 0;
    SELECT @res;", conn);

var lockResult = (int)(await cmd.ExecuteScalarAsync(cancellationToken) ?? -1);
if (lockResult < 0)
{
    _logger.LogInformation("Another instance is currently processing notifications. Skipping this cycle.");
    return;
}
```

---

## 3. Resilience & Failure Isolation

- **CancellationToken Propagation**: All asynchronous database reads and network calls honor the host shutdown token, ensuring graceful termination during deployments.
- **Per-Item Exception Shielding**: If processing an alert for a specific case encounters an exception (e.g., malformed phone number), the worker logs the error with the case ID and continues processing subsequent cases. A single faulty record cannot crash or stall the background service.
- **Exponential Backoff on Fatal Errors**: Unhandled top-level worker exceptions trigger an exponential backoff before the next scheduled iteration.
