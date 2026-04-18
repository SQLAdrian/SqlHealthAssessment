<!-- In the name of God, the Merciful, the Compassionate -->
<!-- Bismillah ar-Rahman ar-Raheem -->

# D08 — Local Quick Check UX: ≤60s Triage Hook (Local Envelope)

**Date:** 2026-04-18
**Updated:** 2026-04-18 (WAN scope clarification)
**Decision:** "Local Quick Check" is primary acquisition hook: curated ~40-check VA subset tagged `"quick": true`, runnable ≤60 seconds from EXE launch to results visible **on local SQL Server connections (RTT ≤15 ms)**. Remote/WAN servers get "Extended Check" mode with longer budget and explicit user consent.

**User journey (local):**
1. Launch EXE (no install, single-file)
2. Auto-redirect to `/onboarding` (no servers configured)
3. Auto-detect local SQL Server instances (or manual entry)
4. Checkbox pre-checked: "✓ Run Quick Check immediately after connecting"
5. Click "Connect" → server added → auto-navigate to Quick Check page
6. Parallel execution of ~40 tagged checks starts (DOP ≤4)
7. **≤ 60 s:** Results page shows PASS/WARN/FAIL summary per category; critical findings highlighted
8. CRITICAL finding triggers toast notification
9. THEN user asked: "Want ongoing monitoring? Deploy SQLWATCH"

**User journey (remote/WAN):**
- After connection, app measures RTT from first 5 checks
- If avg RTT >15 ms: banner "Remote server detected — Quick Check will take 2–3 minutes. Continue?" with "Run Extended Check" / "Skip" buttons
- On consent: DOP=2, per-check timeout 12s, global budget 90s; results marked "Extended (remote)"

**Technical requirements:**
- `queries.json` metadata: each check has `"quick": true|false`, `"timeoutSec": 8|12|20` (derived from `query_analysis.performance_impact`: Low→8, Medium→12, High→20)
- `ICheckRunner.RunSubsetAsync(checkIds, budget=55s, ct)` with per-check timeout enforcement; budget cut leaves 5s UI buffer
- WAN auto-detection: if avg RTT >15ms from first 5 checks, downgrade DOP to 2, raise per-check timeout to 12s, require user consent
- Results cached; full VA available as "Assess All 343 Checks" button on Dashboard

**Success metric:** Time from EXE launch to summary visible ≤ 60 seconds (P95, **local SQL Server with RTT ≤15 ms**). Measured with built-in telemetry (opt-out).

**Marketing caveat:** Position as "Local Quick Check in under 60 seconds" with footnote: "Performance varies with server load, network latency, and check complexity. Remote servers may take longer."

**Failure mode avoided:** User adds server → empty dashboard → churn. Triage-first ensures instant value on local/testing servers.

**Sources:** COMMENT D08 originally; Opus §A.2 (WAN fallback, budget 55s, DOP=4, timeout 8s); PRD §1.4 rewritten with local envelope; PRE-MORTEM_PASS2 §P2-01/P2-02; WORKFILE Task 20 revised

