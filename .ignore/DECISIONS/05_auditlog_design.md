<!-- In the name of God, the Merciful, the Compassionate -->
<!-- Bismillah ar-Rahman ar-Raheem -->

# D05 — AuditLog: Single-Writer + 64 KB Checkpoint + Event Log (Revised per Opus)

**Date:** 2026-04-18
**Updated:** 2026-04-18
**Decision:** `AuditLogService` uses `BlockingCollection<AuditEvent>` single-writer queue, flush to encrypted checkpoint files (default 64 KB, configurable), mirror to Windows Event Log (Source: "SQLTriage-Audit") with graceful fallback, DPAPI-machine HMAC with 90-day rotation + 7-day dual-key grace, startup chain validation, and graceful shutdown drain.

**Rationale:** 4 KB checkpoint too chatty (5–10 records → file churn). 64 KB = ~100–150 records; reduces FS metadata overhead while keeping recovery window reasonable (≈30 min at typical load). Configurable via Settings (v1.1).

**Checkpoint design:**
- Directory: `%PROGRAMDATA%\SQLTriage\audit\`
- Naming: `audit_YYYYMMDD_NNNN.enc` where NNNN = sequence number per day (000, 001, …)
- Rotation trigger: current file size ≥ 64KB → finalize (fsync), rename, open new
- Each record: `{ timestamp, userId, action, resource, details, previousHash, currentHash, signature }`
- Encryption: `ProtectedData.Protect(plaintext, null, DataProtectionScope.LocalMachine)` per record; HMAC-SHA256 chain: `currentHash = SHA256(timestamp|action|prevHash)`; `signature = HMAC-SHA256(currentHash, hmacKey)`
- HMAC key stored at `%PROGRAMDATA%\SQLTriage\audit\hmac.key` (DPAPI-machine encrypted)

**Key rotation:**
- Every 90 days generate new HMAC key
- Dual-key grace: for 7 days after rotation, accept old key OR new key on validation (allows in-flight writes to complete)
- After grace, archive old key in `hmac.key.archive` (encrypted, read-only)

**Startup chain validation:**
1. Enumerate checkpoint files in order (by name)
2. Read each record; verify HMAC(currentHash, currentKey)
3. On first HMAC fail: mark chain "compromised" by writing `audit_chain_compromised.flag`; refuse to accept new AuditLog writes until admin acknowledges via Settings page banner
4. Log chain status to Event Log (if available) or to separate tamper-evident file

**Event Log mirror:**
- Try `EventLog.CreateEventSource("SQLTriage-Audit", "Application")` once at service construction (requires admin)
- If `SecurityException` caught: set `_eventLogEnabled=false`, surface yellow banner in UI ("Elevate once to enable Windows Event Log audit mirroring"), continue file-only
- On each `WriteAsync`: if enabled, `EventLog.WriteEntry("SQLTriage-Audit", $"{ev.FindingId}|{ev.UserId}|{ev.Action}", Information)`

**Graceful shutdown:**
- `IHostApplicationLifetime.ApplicationStopping` → cancel writer loop after draining queue with 2s budget
- If drain incomplete: write "audit log flush incomplete on shutdown" to Event Log (or separate manifest); do NOT block exit (crash-safety over completeness)

**Recovery from partial file:**
- If last checkpoint ends mid-record (power loss mid-write), truncate to last valid HMAC boundary, log truncation as an audit event signed by current key

**Sources:** COMMENT D05 originally; Opus §A.5 (checkpoint size 64 KB configurable, naming scheme, key rotation 90d+7d, chain validation refusal, Event Log fallback, shutdown drain, partial-file recovery)

