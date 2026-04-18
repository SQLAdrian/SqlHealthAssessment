<!-- In the name of God, the Merciful, the Compassionate -->
<!-- Bismillah ar-Rahman ar-Raheem -->

---
Generated: 2026-04-18
Source: OPUS_MEGA_PROMPT_COMPLETE.md (Pass 2)
Supersedes: OPUS_ANALYSIS_COMPLETE_2026-04-18_PASS1.md
Documents reviewed: PRE-MORTEM_PASS2.md, PRE-MORTEM_PASS3.md, SQLTriage_PRD.md §1.4/§2.3/§7.3, WORKFILE_remaining.md, DEVELOPMENT_STRATEGY.md, COMMENT_20260418_091241.md, DECISIONS/D01–D15, Config/queries.json, Config/governance-weights.json
---

# SQLTriage — Opus Pass 2 Analysis

<!-- In the name of God, the Merciful, the Compassionate -->

**Pass 2 posture:** Pass 1 identified 3 live contradictions + open substrate questions. All 4 are now resolved (see "User decisions folded in" below). Residual P1 risk at Week 4 gate: **~4%** (per PRE-MORTEM_PASS3). On-time v1.0 delivery: **89%**. No new blockers surfaced; proceed to Week 0.

## User decisions folded in (2026-04-18 resolution)

1. **AI/ML — DROPPED v1.0.** No Must #11. Task 23 (ONNX pipeline) stays in COULD-HAVE only. Do not re-propose.
2. **Scoring binds to check_id, not category rollup.** Category mapping lives in the source YAML (`consolidated_checks.sql` → `category:` field). `governance-weights.json` holds only the 5 dimension weights + caps + bands. Per-check overrides (rare) live in `queries.json` as `governance: { categoryWeight?, perFindingCap? }`. Agent pass populates `queries.json` from the 343 YAML blocks.
3. **343 vs 489 check count — resolved via `status` flag.** `queries.json` entries carry `status: working | broken | deprecated`. Runtime count = `count(status == working)`. 343 is current working; 489 historical included broken/dup culled during consolidation.
4. **INFO severity added.** 5 levels: CRITICAL/HIGH/MEDIUM/LOW/INFO. INFO contributes **0 points** to governance score; surfaces via translator; auto-excluded from Quick Check.
5. **Quick Check scope clarified.** Renamed "**Local Quick Check**" — ≤60s guarantee only for RTT ≤15 ms. WAN servers stream TDS; degraded performance is expected, not broken. No user-consent gate; transparent banner only (per PRE-MORTEM_PASS2 §P2-01 revision).

---

## Part A — Gap Analysis (Post-Correction)

**A.1 ICheckRunner sub-setting scenarios**
- Quick (curated ~40), Full (all `status==working`), Custom (user-selected set) — all handled by single `RunSubsetAsync(IEnumerable<string> checkIds, CheckRunOptions opts)`.
- **Edge-case not yet mapped:** re-run of a prior custom set after `queries.json` hot-reload that deprecates one of the selected IDs → currently undefined. **Fix:** `RunSubsetAsync` filters out `status != working` pre-execution and emits a `SkippedChecks` field in `CheckRunResult`. Add test.
- **Edge-case:** a check with `requires_database_iteration: true` (per source YAML) in a Quick subset balloons runtime. **Fix:** Quick-tag eligibility rule explicitly excludes `requires_database_iteration`. Documented in D08.

**A.2 PRE-MORTEM_PASS2 → PASS3 mitigation status**
- 11/12 risks mitigated in-design. Only P3-05 (Quick Check adoption resistance on remote) remains partially open — acceptable: Extended Check mode + transparent banner is the answer; no further mitigation required.
- **P2-12 weights JSON schema drift** (new in PASS2): `System.Text.Json` with `CommentHandling = Skip`; try-catch falls back to last-known-good in-memory weights; yellow banner surfaced. **Recommend CI test** reads `governance-weights.json` at build time and validates schema — cheap, blocks bad merges.

**A.3 Local Quick Check vs WAN (resolved)**
- No consent gate. Queries stream TDS; latency adds ~8s cumulative for RTT>200ms; still viable. Banner on RTT>15 ms: "Remote server detected — may take longer." Tagline qualified: "**Local** Quick Check in under 60 seconds."
- Remaining tweak: Onboarding page needs a one-liner "For WAN/Azure SQL, results may take 2–3 minutes" — add to Week 5 UX.

**A.4 Rules-engine translator — is it enough to validate the substrate?**
- Yes, provided templates consume the rich YAML fields already present (`title`, `impact`, `recommendations`, `evidence_based_findings`). Per PRE-MORTEM_PASS3 §P3-01: 40 Quick templates + any Full VA findings that surface — **not 343 hand-authored**. Quality gate: beta NPS ≥4.0 on "executive summary usefulness" question.
- **Risk if half-baked:** templates read as Mad-Libs. **Mitigation:** each Quick template hand-reviewed in Week 3 by user (4-hour editorial pass); sp_Blitz fallback renders standard findings verbatim.

**A.5 ChartTheme hidden components**
- Confirmed via grep Pass 1: 10+ components use hard-coded Apex options. **Likely need new component creation (not just refactor):** `GaugeChart.razor` (maturity %), `BandedHeatmap.razor` (risk register density) — neither exists today.
- **Recommend:** allocate 0.5-day buffer in Week 1 as PASS3 §P3-06 already calls out. If Week 1 velocity at risk, ship v1.0 with maturity-as-progress-bar and defer GaugeChart to v1.0.1.

**A.6 QuestPDF without live chart embeds**
- Acceptable for v1.0. Native bars + gauges + dense tables = "audit evidence" that reads as serious. Add disclaimer "Interactive charts available in dashboard." (PASS3 §P3-02.)
- **Competitor diff:** ApexCharts screenshots in PDFs look slick but fail print/archival (JPEG artifacting, color drift). Native QuestPDF rendering is actually *better* for the audit-prep tagline — lean into it.

**A.7 Argon2 .NET 8 win-x64**
- `Konscious.Security.Cryptography.Argon2` 1.3.x builds on .NET 8 per NuGet metadata. **Week 0 build check is non-negotiable** — if it fails, switch to `Isopoh.Cryptography.Argon2` (pure C#, no native deps). Both OWASP-compliant at m=19456, t=2, p=1.
- **Package choice note:** `Microsoft.AspNetCore.Cryptography.KeyDerivation.Pbkdf2` (mentioned in original mega prompt line 187) is **wrong primitive** — PBKDF2 not Argon2id. Keep Konscious/Isopoh.

**A.8 AuditLog startup chain validation — DoS risk on false positive**
- Refusing writes on HMAC mismatch is correct posture; writing into a compromised log destroys audit value. But "refuse writes" must not mean "crash app" — it means: banner, disable audit-dependent actions (Report export, RBAC changes), preserve read-only dashboard.
- **Admin-ack via Settings is acceptable:** require Admin role + reason string + zxcvbn ≥3 password re-entry. Event logged to Windows Event Log independent of tampered chain. **Add to PRD §11 acceptance:** tamper-recovery flow completes without data loss.

**A.9 PRD §11 acceptance criteria — re-audit**
- All 5 Pass 1 gaps now marked `[x]` in PASS3. One additional check:
  - **Missing:** "INFO severity findings surface in translator output but do not move the governance score." Add to §11.

---

## Part B — Design Validation

- **Governance scoring clamp (3-level):** algorithmically sound. Per-finding cap 40 (CRITICAL) / 25 / 10 / 5 / 0 (INFO), per-category clamp at `weight × 100`, overall = average of category subtotals, clamp at 100. 25/20/20/15/20 weights verified; Compliance at 20% correctly reflects audit-prep tagline. **No change needed.**
- **Hot-reload race:** `IOptionsMonitor<GovernanceWeights>` is thread-safe for reads; `GovernanceService` must take a `ReaderWriterLockSlim` around `ComputeCore` to avoid mid-compute weight swap. **Add to D11.** Same pattern for `SqlQueryRepository` on `queries.json` reload.
- **Translator cache key:** `(findingId, translatorVersion, weightsHash)` is correct. `weightsHash` = SHA256 of `governance-weights.json` file bytes at compute time. Invalidation wired via `GovernanceService.WeightsChanged` event → `FindingTranslator.ClearCache()`. Test added in PASS3 §P3-08.
- **55s budget buffer:** 5s UI buffer is valid. Global token cancellation at 55s; in-flight check kill + drain to UI in <1s on modern hardware. If Quick Check completes in 20s (local, 4-core, SSD), 35s slack — fine.

---

## Part C — Implementation Sequencing (unchanged)

- 4-week implementation + 4-week stabilization = 8 weeks to v1.0 tag. No change to Week 0–6 Gemma plan from Pass 1.
- **RoleGuard timing confirmed Week 4** — blocks on RBAC Argon2 completion; Page guard attributes added in same PR as role schema migration. Week 5 is too late (Report PDF requires authenticated Auditor role to generate).
- **Agent pass for `queries.json` population:** insert as **Week 0** task (new). Runs an agent over `research_output/01_new_checks/consolidated_checks.sql`, emits `Config/queries.json` with 343 `status: working` entries + auto-tagged Quick subset candidates. Manual review + trim to ~40 Quick happens in Week 2.

---

## Part D — Gate Review Prompts (Pass 2 deltas)

Reuse Pass 1 prompts; append these validation items:

- **Gate 1 (Wk2):**
  - `ICheckRunner.RunSubsetAsync` filters out `status != working` pre-execution; `SkippedChecks` populated.
  - `queries.json` populated from agent pass; all 343 entries have `status`, `category`, `severity`, `quick` fields.
  - INFO-severity checks emit finding with score contribution = 0 (unit test).
- **Gate 2 (Wk4):**
  - `FindingTranslator` cache invalidates on `governance-weights.json` change (file-touch integration test).
  - BusinessRiskBand mapping verified for each of 5 maturity bands.
  - `ReaderWriterLockSlim` usage in `ComputeCore` passes concurrency stress test (100 parallel reads + 1 writer).
- **Gate 3 (Wk6):**
  - AuditLog startup chain validation catches injected tamper; admin-ack flow restores write capability.
  - Argon2 OWASP test vectors pass (m=19456, t=2, p=1).
  - RoleGuard blocks unauthenticated access on all pages except `/onboarding` and `/login`.
- **Gate 4 (Wk8):**
  - All PRD §11 criteria green; INFO-surfaced-but-zero-scored test green.
  - `status == working` count displayed on Settings page.
  - Basmalah header present on every tracked `.cs/.razor/.css/.js/.md` (pre-commit hook + CI re-check).

---

## Part E — Gemma Prompts (additions)

Pass 1's 10 prompts stand. Add two:

**11. Agent pass: populate `Config/queries.json` from `consolidated_checks.sql`**
- Inputs: `research_output/01_new_checks/consolidated_checks.sql` (343 YAML+SQL blocks)
- For each check: parse YAML frontmatter, map to `queries.json` schema: `file` (split to `Data/Sql/HealthChecks/check_NNN.sql`), `description` (from `title`), `category` (verbatim from YAML), `severity` (map `priority` → CRITICAL/HIGH/MEDIUM/LOW/INFO), `status: "working"`, `quick` (auto-tag rule: `priority in (Critical,High) AND query_complexity=Low AND performance_impact=Low AND NOT requires_database_iteration`), `audience` (Configuration→dba+it, Security→dba+exec, Compliance→all, etc.), `controls` (from `evidence_based_findings` → `control_mappings.json` lookup), `timeoutSec` (Low=8, Medium=12, High=20).
- Outputs: `Config/queries.json` fully populated; `Data/Sql/HealthChecks/*.sql` 343 files; `Config/control_mappings.json` seed.
- Verify: JSON schema valid; all `status==working` checks resolve to an existing `.sql` file; Quick auto-tag yields 30–50 candidates.

**12. Implement `RoleGuard.razor` + session timeout**
- Inputs: `RbacService`, `UserSettingsService`
- Outputs: `Components/Shared/RoleGuard.razor` — `@attribute [RoleGuard(Roles="Admin,DBA")]` equivalent; wraps `<CascadingAuthenticationState>`. Idle-timer service enforces 8h Admin/DBA, 4h Auditor.
- Verify: unauth'd nav to `/governance` redirects to `/login`; idle >configured → forced logout; new session requires password re-entry.

---

## Part F — Additional Recommendations

**NuGet additions (beyond Pass 1 list):**
- None new. `QuestPDF 2024.*`, `Konscious.Security.Cryptography.Argon2 1.3.*` (backup: `Isopoh.Cryptography.Argon2`), `Zxcvbn.Core 3.*` confirmed.
- Optional: `FluentValidation` for `governance-weights.json` schema validation instead of hand-rolled. Adds ~80 KB; probably not worth it — hand-rolled validator + try-catch + last-known-good fallback is sufficient.

**CI tests to add:**
- Schema validators for `governance-weights.json` AND `queries.json` at build time (fail fast on malformed JSON).
- Translator coverage test: every `status==working AND quick==true` check has non-empty DBA/IT/Executive template output (failing template blocks merge).
- `ComputeCore` equivalence test: Quick and Full code paths produce identical overall score when given same check set (prevents PASS2 §P2-10 drift).
- Basmalah pre-commit + CI re-check (hook already installed at `tools/pre-commit-basmalah.sh`).

**Overlooked coupling:**
- **INFO severity + compliance controls.** An INFO check tagged with SOC2/HIPAA control should still appear in Governance Report PDF's "Controls Evidence" section even though it contributes 0 score. Ensure `ReportService` iterates ALL `status==working` findings, not just scored-nonzero.
- **Agent pass drift.** Once `queries.json` is generated, hand-edits drift from source YAML. **Mitigation:** add `sourceYamlHash` field per entry; CI warns if source YAML changed but `queries.json` entry wasn't regenerated.

---

## Failure probability (Pass 2 final)

| Gate | Pass 1 estimate | Pass 2 estimate | Notes |
|------|----------------|-----------------|-------|
| On-time v1.0 (15 → 12 Musts) | 82% | **89%** | AI/ML drop + scope clarity |
| With ≤2 Should-haves | 85% | **95%** | PASS3 headroom |
| Zero P1 at release | 75% | **96%** | All Pass 1 contradictions resolved |
| Combined P(any P1) | ~11% | **~4%** | PASS3 residual |

---

## Status: cleared for Week 0 execution

No showstoppers. 4 user decisions folded. PRE-MORTEM_PASS3 confirms mitigations. Awaiting only:
- User sign-off on the **Agent pass for queries.json** (new Week 0 task) — adds ~1 day, saves ~3 days of ambiguity.
- User sign-off on **control_mappings.json** seed scope (SOC2/HIPAA/ISO27001 — any others needed for v1.0?).

**Recommended next action:** lock scope, start Week 0. Opus role for Week 0 is architecture validation sign-off on Translator + GovernanceService + AuditLog designs — ~2 hours review against this Pass 2 document.
