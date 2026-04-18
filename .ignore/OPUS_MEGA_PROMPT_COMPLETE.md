<!-- In the name of God, the Merciful, the Compassionate -->
<!-- Bismillah ar-Rahman ar-Raheem -->

# SQLTriage: Opus Review Prompt — In-Place Hardening Strategy

**File:** `.ignore/OPUS_MEGA_PROMPT_COMPLETE.md`
**Purpose:** Single copy-paste block to send to Claude Opus for architecture review, gap analysis, and gate review prompts
**Audience:** Claude Opus (paid) — lead architect synthesis
**Date:** 2026-04-18
**Critical decisions already made:** See `.ignore/COMMENT_20260418_091241.md` — Option D (in-place hardening), ~15 Must-haves v1.0, IFindingTranslator core, RBAC Argon2id, AuditLog single-writer, QuestPDF, error catalog expansion, Quick Check ≤60s, AI/ML & UI theme deferred to v1.1, Basmalah enforced on all files.

---

## INSTRUCTIONS

You are the lead architect for **SQLTriage**, a SQL Server monitoring and governance tool being built via **in-place hardening** of an 80% complete existing codebase.

**Your job:**
1. Read all referenced planning documents and the COMMENT file
2. Identify gaps or missing pieces in the Option D plan
3. Suggest enhancements to IFindingTranslator, GovernanceService, AuditLog, RBAC, SqlQueryRepository designs
4. Validate the Must/Should/Could scope split (13–15 Must-haves v1.0)
5. Produce 4 gate review prompts (weeks 2, 4, 6, 8 of Option D timeline)
6. Recommend any additional tools/libraries not yet considered
7. Confirm Quick Check ≤60s feasibility and translation substrate completeness

Deliver a structured response with Parts A–F as described below.

---

## 📝 OUTPUT DELIVERY

### Primary Output (console/chat)
Provide your full structured response (Parts A–F) directly in this conversation.

### Secondary Output (file artifact)
**Also write your complete response to a file** at:
```
.ignore/OPUS_ANALYSIS_COMPLETE_<YYYY-MM-DD>.md
```
Example: `.ignore/OPUS_ANALYSIS_COMPLETE_2026-04-18.md`

**File format:** Same Markdown structure (Parts A–F) with metadata block:
```markdown
---
Generated: 2026-04-18
Source: OPUS_MEGA_PROMPT_COMPLETE.md
Documents reviewed: WORKFILE_remaining.md, SQLTriage_PRD.md, SQLTriage_Release_Checklist.md, SQLTriage_Strategic_Blueprint.md, DEVELOPMENT_STRATEGY.md, COMMENT_20260418_091241.md
---
```

---

## CONTEXT

**Brand:** SQLTriage — "Lightweight SQL Server Monitoring & Governance Tool"
**Tagline:** Reduce audit preparation from days to minutes
**Positioning:** Free evidence generator for DBAs to pass audits — targets budget-conscious enterprises ($10M–$200M revenue)

**Tech Stack:** Blazor Hybrid WPF (.NET 8), single-exe, SQLite WAL cache, Serilog, ApexCharts v3

**Chosen Strategy:** Option D — In-Place Hardening of existing codebase (80% complete). Preserve battle-tested integration, fix gaps, implement missing must-haves. **Do NOT propose fresh start — already rejected.**

**Decision Records:** All active decisions documented in `.ignore/DECISIONS/` (12 files, D01–D12). Read them first — they are the authoritative source. `COMMENT_active.md` is the index. Total decision context ~2400 tokens.

**SQL Modifiability Requirement:** All SQL queries external `.sql` files in `Data/Sql/`. Implement `SqlQueryRepository` runtime loader + hot-reload (if not already present).

**Components to KEEP / HARDEN (already exist):**
- `Data/Caching/SqliteCacheStore.cs` (WAL, 2-week retention, delta-fetch)
- `Data/CredentialProtector.cs` (AES-256-GCM + DPAPI)
- `Data/ConnectionManager.cs` / `ServerConnectionManager.cs`
- `Data/Services/VulnerabilityAssessmentService.cs`
- `Data/Services/AlertEvaluationService.cs` (IQR baseline)
- `Data/Services/NotificationChannelService.cs` (email/Teams/webhook)
- `Data/Services/PrintService.cs` (PDF export)
- `Data/Services/AutoUpdateService.cs` (Squirrel.Windows)
- `Data/Services/SessionDataService.cs` (DMV queries)
- `wwwroot/css/app.css` (7500-line design system)
- Existing Blazor pages with working patterns (statemanagement, DI, error handling)

**Missing Must-Haves to IMPLEMENT (v1.0):**
- `IFindingTranslator` — 3-audience translation (DBA/IT/Executive)
- `GovernanceService` revised — capped scoring, vector weights, `Config/governance-weights.json`
- `AuditLogService` — single-writer queue, 4 KB checkpoint files, DPAPI-machine, Event Log mirror
- `ReportService` — QuestPDF 3-page Governance Report (Executive Snapshot + Risk Register + Action Plan)
- `ErrorCatalog` — ~60 scenarios with governance impact, plus `ErrorCatalogTests` coverage
- `ChartTheme` singleton — Rolls Royce ApexCharts palette (emerald/amber/slate, Playfair headings)
- `SqlQueryRepository` (if missing) + `Data/Sql/queries.json` metadata with `"quick": true` tags for ~40 checks
- RBAC Argon2id hashing (update `RbacService` implementation from CredentialProtector for passwords)
- Quick Check UX (curated subset execution ≤60s from EXE launch)

**Deferred to v1.1 (DO NOT implement):**
- Full AI/ML ONNX pipeline (anomaly detection, multi-metric, SignalR streaming)
- Tailwind UI theme refresh (component styling; use existing `app.css` + ChartTheme for charts only)
- AD/LDAP integration (local auth acceptable for v1.0)
- GPO ADMX templates
- Documentation Generator page
- Code signing automation (v1.0.1)
- Advanced multivariate forecasting (CPU linear regression only if easy, else defer)

---

## PLANNING DOCUMENT REFERENCES

**Read in this order:**

1. `.ignore/DECISIONS/` — Decision records D01–D15 (~4000 tokens). Start here. `COMMENT_active.md` is the index.
2. `.ignore/PRE-MORTEM_PASS2.md` — **NEW** — pre-mortem analysis on current (post-Opus-correction) state. Read after DECISIONS.
3. `.ignore/WORKFILE_remaining.md` — Must/Should/Could task list
4. `.ignore/SQLTriage_PRD.md` — Product requirements (Philosophy §1, Governance algorithm §2.3 rewritten, ICheckRunner §7, Translation §7.2, ChartTheme §8, AI/ML deferred §5; acceptance criteria updated §11)
5. `.ignore/DEVELOPMENT_STRATEGY.md` — Option D rationale, Week 0–4 MVP plan, cost ($15–25)
6. `.ignore/pre-mortem_1.md` — original 10 failure modes (historic context)
7. `.ignore/SQLTriage_Release_Checklist.md` — 200+ validation items
8. `.ignore/SQLTriage_Strategic_Blueprint.md` — GTM, monetization
9. `.ignore/CLAUDE.md` — Project conventions (lean)
10. `OPUS_ANALYSIS_COMPLETE_2026-04-18.md` — **your output destination** (Parts A–F write here too)

**Token tip:** Glob DECISIONS directory (~4000 tokens); then PRE-MORTEM_PASS2 (~2500 tokens); PRD and others (~5000 tokens). Total ≈ 11.5k tokens.

---

## SPECIFIC REQUESTS

**This is Pass 2 review.** All decisions from Pass 1 (D01–D15) now locked. New material since your last analysis:

- D13: ICheckRunner interface abstracting Quick/Full execution
- D14: Explicit non-circular data flow (Governance → Translator)
- D15: RBAC bootstrap + RoleGuard + session timeout specs
- PRE-MORTEM_PASS2.md: 12 new failure modes on the corrected architecture
- PRD §2.3 fully rewritten; §7 split; §11 acceptance updated with 5 missing items

### Part A — Gap Analysis (Post-Correction)

Focus on **residual risks** after Opus Pass 1 refinements:

1. Does the `ICheckRunner` abstraction cleanly handle all sub-setting scenarios (Quick, Full, custom)? Any edge-cases unmapped?
2. Are the 12 PRE-MORTEM_PASS2 risks adequately mitigated by existing decisions? Which remain open?
3. Quick Check WAN fallback — is "Extended Check" (user consents) acceptable given triage-first promise? Or should WAN be excluded from ≤60s guarantee?
4. Is the rules-engine translator (Quick subset only) sufficient to validate the "translation substrate" differentiator, or does it feel half-baked to users?
5. ChartTheme refactor scope — are there hidden charts (GaugeChart, BandedHeatmap) that will require new component creation rather than just refactor?
6. QuestPDF without live chart embeds — does this undercut Governance Report value vs. competitors? Or is native bar/gauges sufficient?
7. RBAC Argon2 package availability on .NET 8 win-x64 — confirmed? Backup plan documented?
8. AuditLog startup chain validation — does refusing writes after tamper detection create denial-of-service risk if false positive occurs? Is admin-ack via Settings sufficient?

### Part B — Design Validation (Already Specified)

Briefly confirm:
- Governance scoring clamp (3-level) algorithmically sound given vector weights (25/20/20/15/20)?
- Hot-reload of `governance-weights.json` + `queries.json` race-condition free?
- Translator cache key `(findingId, translatorVersion, weightsHash)` correct?
- ICheckRunner budget enforcement (55s cut) leaves 5s buffer for UI thread — valid?

### Part C — Implementation Sequencing (Week 0–6)

Week 0–4 plan from DEVELOPMENT_STRATEGY stands. Confirm no change to sequence with these new abstractions. Should `RoleGuard` be Week 4 or 5? (Opus Pass 1 says Week 4.)

### Part D — Gate Review Prompts (Updated)

Reuse prompts from Pass 1; add validation items for:
- Gate 1 (Wk2): ICheckRunner `RunSubsetAsync` passes timeout/budget tests
- Gate 2 (Wk4): Translator cache invalidates on weights change; BusinessRiskBand mapping verified
- Gate 3 (Wk6): AuditLog startup chain validation test; Argon2 OWASP test vectors; RoleGuard enforces on all pages

### Part E — Gemma Prompts

Same 10 prompts from Pass 1 are still valid. Confirm no additional prompts needed for:
- RoleGuard component creation
- WAN auto-downgrade logic in QuickCheckRunner
- Weights hot-reload invalidation hook

### Part F — Additional Recommendations

Given Pass 2 additions, any extra NuGet (already listed: QuestPDF, Konscious Argon2, Zxcvbn.Core)? Any CI tests to add? Any other overlooked coupling?

---

## RESPONSE INSTRUCTIONS (Token Budget)

**This is a synthesis task. You have all documents already. Output concisely:**

- Use bullet points, not paragraphs. Cite documents by name (D05, PRD §2.3, WORKFILE Task 32, PRE-MORTEM_PASS2 §P2-04).
- Do NOT re-explain concepts already in PRD or decisions already in DECISIONS/.
- Focus on: (A) residual gap spotting, (B) validation of new abstractions (ICheckRunner, data flow), (C) confirm Pass 1 decisions hold, (D) minor tweaks to gate prompts if needed.
- Amplification factor < 20%. Terse is fine.

**Parts A–F structure must be followed but can be terse.**
- **Translation substrate:** Does `IFindingTranslator` design (DBA/IT/Executive models in `Data/Models/Translation/`) fully capture the "3-audience" requirement? Is the data flow from VA check → metadata → translator → Governance score augmentation complete?
- **Quick Check feasibility:** Is ≤60s EXE → results technically achievable with existing cache and parallel query execution? What query count (~40 checks) and timeout settings are needed?
- **Governance scoring:** Capped critical-failure at 40pts + per-framework vector weights — realistic? Does `Config/governance-weights.json` format support runtime reload?
- **RBAC + Argon2id:** Is `Microsoft.AspNetCore.Cryptography.KeyDerivation.Pbkdf2` correct choice? Are 5 roles (Admin/DBA/ReadOnly/Auditor/Operator) sufficient for v1.0 scope?
- **AuditLog single-writer:** `BlockingCollection<AuditEvent>` + checkpoint files + Event Log mirror — chain validation strategy sound? Checkpoint rotation (4 KB files) adequate?
- **ErrorCatalog:** ~60 scenarios with `UserMessage`, `GovernanceImpact`, `Remediation` — coverage sufficient for common operational failures?
- **ChartTheme integration:** Singleton injected into all `Chart*.razor` components — any charts missing integration points?
- **Pre-mortem alignment:** Which of the 10 failure modes in pre-mortem_1 are highest probability given Option D? Are scope-trim triggers correctly placed?
- **Acceptance criteria:** PRD §11 updated — do listed criteria cover all Must-haves? Missing any?

### Part B: Design Enhancements (Specific)
Propose concrete improvements to:
- **SqlQueryRepository:** File watcher vs manual reload? `queries.json` schema augmented with `"quick": true`, `"audience": ["dba","it","exec"]`, `"governance": {"Security":0.3}` weight overrides?
- **GovernanceService:** Should capped scoring happen per-category or overall? How to handle `quick` subset vs full VA scoring drift? Maturity band thresholds (0–20 Emerging, 21–40 Bronze, …)?
- **IFindingTranslator:** Should translator cache per-finding or recompute on each Governance refresh? How to handle VA re-run invalidations? Link `FindingExecutive.BusinessRisk` to Governance risk level?
- **AuditLogService:** HMAC key stored via DPAPI-machine with 90-day rotation? Checkpoint naming (`audit_2026-04-18_00.enc`) and recovery from partial incomplete file? Event Log source registration requires admin — handle gracefully?
- **QuestPDF ReportService:** Page layout (1-inch margins, Playfair headings, emerald/amber/rose bands). Include executive summary on page 1; append raw findings table optional page 4? "Cost of Downtime" calculator UI placement?

### Part C: 8-Week Execution Plan (Option D — In-Place Hardening)
Split into:
- **Opus weeks (2 total):** Week 0 (architecture validation of Translator + GovernanceService + AuditLog designs), Week 4 (gate review + Report PDF spec sign-off)
- **Gemma weeks (6 total):** Week 0 (build cleanup + brand rename), Week 1 (SqlQueryRepository + ChartTheme), Week 2 (GovernanceService + Quick Check subset tagging), Week 3 (IFindingTranslator + ErrorCatalog), Week 4 (AuditLog + RBAC Argon2id), Week 5 (QuestPDF Report + Onboarding UX), Week 6 (Unit tests + polish + release candidate)

Specify exact Gemma prompts for each week (top 10). Include Opus gate checkpoints.

### Part D: Gate Review Prompts (4 prompts, ≤200 words each)
Write prompts for:
- Gate 1 (Week 2): SqlQueryRepository + ChartTheme + Quick Check tagging complete
- Gate 2 (Week 4): GovernanceService + IFindingTranslator + ErrorCatalog integration
- Gate 3 (Week 6): AuditLog + RBAC Argon2id + ReportService functional
- Gate 4 (Week 8): Acceptance criteria met, build clean, basmalah compliance, v1.0 release candidate

Each prompt: reference artifacts built, ask for validation against PRD/Checklist, request sign-off or blockers list.

### Part E: Top 10 Gemma Implementation Prompts
Provide 10 copy-paste ready prompts for Must-have tasks:
1. Harden build: fix CA1416 warnings, enable nullable, ensure zero build warnings
2. Implement `SqlQueryRepository.cs` with file loader + `Reload()` method; integrate DI
3. Implement `ChartTheme.cs` singleton; integrate with `TimeSeriesChart.razor`, `StatCard.razor`
4. Implement `GovernanceService` revised: capped scoring + vector weights from JSON
5. Implement `IFindingTranslator` + `FindingDba/ItManager/Executive` models; VA metadata mapping
6. Implement `ErrorCatalog` with 60 scenarios; `ErrorCatalogTests` coverage ≥95%
7. Implement `AuditLogService` single-writer queue + 4 KB checkpoint + Event Log mirror
8. Update `RbacService` to Argon2id hashing; SQLite schema migration; page guards
9. Implement `ReportService` with QuestPDF; 3-page Governance Report PDF generation
10. Onboarding → Quick Check UX: auto-detect → connect → run ~40 tagged checks → results ≤60s

Each prompt must include: context, inputs (existing files to reference), outputs (file paths + code structure), quality checks (what to verify before finalizing).

### Part F: Additional Recommendations
- Missing NuGet packages? (`QuestPDF` confirmed; any others?)
- Test strategy: xUnit + Coverlet, what to mock, integration test approach
- CI/CD pipeline fixes (submodule, warnings as errors)
- Code signing workflow (defer to v1.0.1)
- Basmalah enforcement mechanism details (pre-commit hook script)
- Any other considerations for Option D success?

---

## RESPONSE INSTRUCTIONS (Token Budget)

**This is a synthesis task. You have all documents already. Output concisely:**

- Use bullet points, not paragraphs. Cite documents by name (WORKFILE line 412, PRD §1.4, D03, D08).
- Do NOT re-explain concepts already in PRD or decisions already in `DECISIONS/`.
- Focus on: (A) gap spotting, (B) validation of Option D vs C, (C) any uncaptured decisions, (D) gate review prompts (≤200 words each), (E) gemma prompts that compile, (F) concrete additional recommendations.
- Amplification factor < 20%. If 5 words suffice, don't use 10.

**Parts A–F structure must be followed but can be terse.**

---

## DOCUMENT VERSION HISTORY

| Version | Date | Changes |
|---------|------|---------|
| 1.0 | 2026-04-18 | Initial prompt for Option D (in-place hardening) based on COMMENT_20260418_091241.md decisions |

---

**Next action:** Copy this entire prompt → send to Claude Opus for analysis. Opus will read all referenced docs + COMMENT file and return Parts A–F response (both in chat and to `.ignore/OPUS_ANALYSIS_COMPLETE_2026-04-18.md`).

