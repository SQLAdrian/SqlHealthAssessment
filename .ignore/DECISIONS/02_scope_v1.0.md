<!-- In the name of God, the Merciful, the Compassionate -->
<!-- Bismillah ar-Rahman ar-Raheem -->

# D02 — v1.0 Scope: Must/Should/Could Trim (Revised per Opus Pass 1)

**Date:** 2026-04-18
**Updated:** 2026-04-18 (post-Opus corrections)
**Decision:** Reduce scope from 33 tasks to 13–15 Must-haves for v1.0. AI/ML and UI theme deferred to v1.1. **Must #11 (Anomaly detection + SignalR streaming) dropped** — IQR-based AlertEvaluationService already covers basic anomaly; streaming layer is pure v1.1 surface area.

**Must-Have Core (v1.0 non-negotiable, 15 tasks):**
1. Build system zero-warnings (CA1416 fixed, nullable enabled), basmalah enforcement pre-commit hook, brand rename completion (SQLTriage everywhere)
2. `SqlQueryRepository` + `Config/queries.json` metadata with `"quick": true` tags on ~40 checks; `ICheckRunner` abstraction (`RunSubsetAsync` with budget/timeout)
3. `ChartTheme` singleton (Rolls Royce ApexCharts palette); integrate into all `Chart*.razor` components (TimeSeriesChart, StatCard, GaugeChart, BandedHeatmap/RiskRegister)
4. Quick Check UX: ≤60s EXE launch → results visible (P95, local SQL); per-check timeout 8s; global budget 55s; DOP capped at 4; WAN fallback mode (Extended Check)
5. `GovernanceService` revised: capped scoring (per-finding 40/25/10/5) → per-category clamp (weight ceiling) → overall ≤100; vector weights in `Config/governance-weights.json` (Security 25% / Performance 20% / Reliability 20% / Cost 15% / Compliance 20%); maturity bands in JSON (Emerging 0–20, Bronze 21–40, Silver 41–60, Gold 61–80, Platinum 81–100); hot-reload via `IOptionsMonitor`; Quick vs Full separation (`IsIndicative` flag)
6. `IFindingTranslator` service: rules-engine implementation for Quick Check subset only (~40 checks); produces `FindingDba`, `FindingItManager`, `FindingExecutive`; BusinessRisk derived from capped per-finding band; ComplianceControls from `queries.json`; cache invalidates on VA re-run or weights reload
7. RBAC + Argon2id: Use `Konscious.Security.Cryptography.Argon2` (OWASP 2024 params: memorySize=19456KB, iterations=2, parallelism=1, saltLength=16, hashLength=32); 5 roles (Admin/DBA/ReadOnly/Auditor/Operator); RoleGuard component; admin bootstrap in Onboarding (first-run forces admin with zxcvbn ≥3); session timeout (Admin/DBA 8h idle, Auditor 4h); local admin-only password reset page (no email v1.0)
8. `AuditLogService` single-writer: `BlockingCollection<AuditEvent>` + 64 KB checkpoint files (configurable) + DPAPI-machine HMAC; key rotation 90-day + 7-day dual-key grace; startup chain validation (HMAC break → "compromised" flag file, refuse new writes until admin ack); Event Log mirror with graceful fallback (no admin → file-only, surface banner)
9. `ErrorCatalog` expanded to **65 scenarios** (Opus-identified gaps: edition mismatch, SQLWATCH not installed, firewall block, Azure SQL TLS/firewall, long-running cancel added); `ErrorCatalogTests` with field-coverage ≥95% + golden-file JSON snapshot test
10. `ReportService` (QuestPDF): 3-page Governance Report PDF (Cover + Executive Snapshot, Risk Register + Config Drift, Action Plan + Cost-of-Downtime widget); Playfair Display headings, Inter body, emerald/amber/rose severity bands; **NO live chart embeds in v1.0** (use QuestPDF native primitives); sample PDF generated from synthetic dataset
11. Unit test ≥80% coverage (xUnit + Moq + Coverlet); integration tests gated behind `SQLTRIAGE_INTEGRATION=1`; golden-file tests for ErrorCatalog, queries.json, governance-weights.json
12. Onboarding wizard → Quick Check pipeline fully wired (auto-detect instances → connect → ICheckRunner quick subset ≤60s → Governance Dashboard with IsIndicative chip)
13. Settings UI: all configuration via UI (no manual JSON); `ICheckRunner`, `ChartTheme`, weights editor optional v1.1

**Should-Have (stretch — cut if >2 weeks behind at Week 4):**
- Full Governance Dashboard Risk Register table (all 472 findings sorted by BusinessRisk)
- Error messages with inline governance impact (already in catalog; UI rendering enhancement)
- Governance Report enhanced (full findings appendix, Cost-of-Downtime live calculator)
- `ChartTheme` integration completeness audit (grep for inline `ApexChartOptions`, fix remainder)

**Could-Have (defer to v1.1):**
- Full AI/ML ONNX pipeline (anomaly detection, multi-metric, SignalR streaming) — **Must #11 removed per Opus**
- Tailwind UI theme migration (component styling)
- AD/LDAP enterprise auth (local acceptable)
- GPO ADMX templates
- Documentation Generator page
- Code signing automation (v1.0.1 manually-signed acceptable)
- Live chart embed in PDF via headless browser (QuestPDF v1.2+ exploration)

**Velocity trigger:** Week 4 completed < 80% → suspend Could-haves and 2 Should-haves. Communicate scope-freeze explicitly.

**Sources:** Opus Analysis §A.9 (acceptance criteria gaps), §B.1–B.5 (design refinements), §C (8-week plan split), Part D (gate prompts), Part E (Gemma prompts), Part F (NuGet/test strategy); updated COMMENT D02 originally

