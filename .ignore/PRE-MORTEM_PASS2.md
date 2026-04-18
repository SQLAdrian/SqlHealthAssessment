<!-- In the name of God, the Merciful, the Compassionate -->
<!-- Bismillah ar-Rahman ar-Raheem -->

# SQLTriage — Pre-Mortem Analysis (Pass 2 — Post-Clarification)

**Date:** 2026-04-18
**Context:** Post-Opus Pass 1 corrections applied. Scope refined to 15 Must-haves; ICheckRunner abstraction introduced; Translator rules-engine limited to Quick Check subset; Governance scoring clarified; RBAC/AuditLog/Report designs revised.

**Purpose:** Identify new failure modes introduced by the tighter specification before Week 0 execution begins.

---

## Expected Failure Rate (Post-Clarification)

**Overall P1-blocker risk: ~6–8%** after mitigations. Residual P1 risk **≈ 4–6%** at Week 4 gate.

### Residual P1 Risks

| ID | Failure Mode | P(P1) | Mitigation |
|----|--------------|-------|------------|
| P2-02 | Timeout mismatch for slow DMVs (some checks legitimately >8s) | 3% | Per-check `timeoutSec` override in `queries.json`; YAML `performance_impact` tags inform defaults |
| P2-04 | Translator cache not invalidated on weights reload | 2% | Cache key includes `weightsHash`; test added |
| P2-06 | Argon2 package compatibility on .NET 8 win-x64 | 3% | Week 0 build check; `Isopoh.Cryptography.Argon2` backup |
| P2-07 | AuditLog rotation false positive (key boundary) | 1% | File header version + dual-key grace; test covers |
| P2-10 | Quick vs Full Governance drift | 2% | Shared `ComputeCore` method; equivalence unit test |
| P2-11 | Rules-engine template maintenance (missing for new check) | 2% | CI test: every `"quick": true` check ID must have non-empty translation |
| **Combined** | **Any P1 materializing** | **~6%** | **Mitigations in place** |

### P2 Risks (non-blocking but notable)

- **P2-03 Translator phrasing quality:** 4% chance templates feel "technically stilted" to executives; beta NPS question will catch; can iterate in Week 5–6 if needed
- **P2-05 ChartTheme regression:** 3% chance 2–3 components need more than refactor; allocate 1 day buffer in Week 1
- **P2-08 Admin bootstrap friction:** 2% chance ≥40% testers abort at password creation; can relax zxcvbn threshold mid-W Week 1 if data shows

**Updated success probabilities:**
- On-time v1.0 (15 Musts): **87%**
- With ≤2 Should-haves: **93%**
- Zero P1 at release: **94%**

---

## Scope & Dependency Risks

### P2-01 — ICheckRunner WAN latency handling

**Failure mode (revised):** Queries execute server-side; we stream TDS results. Latency only affects round-trip per query start/completion, not query execution itself. On >200 ms RTT links, 40 queries × 200 ms overhead = +8s cumulative, still within 60s if queries fast. Degraded performance expected, not broken promise.

**Updated mitigation:** No WAN mode switch needed. Document expectation: "Results may take longer on high-latency connections (>200 ms RTT)." Keep DOP=4 for local; for RTT>200ms, user sees "This server is remote — checks may take 2–3 minutes" non-blocking banner. No user consent gate; just transparency.

**Trigger:** If beta testers with RTT>200ms report Quick Check >120s, consider reducing Quick subset to 25 checks for remote (auto-detect). PRD metric stays "local" envelope.

**Updated P(P1):** 5% → **2%** (now just a documentation/expectation issue)

---

### P2-02 — ICheckRunner timeout granularity mismatch

**Failure mode:** Some VA checks have long-running DMVs (e.g., `sys.dm_exec_query_stats` with no filter) that legitimately take >8s. Uniform 8s timeout causes false negatives (miss critical long-running query findings).

**Mitigation:** `queries.json` supports per-check `"timeoutSec"` override; default 8s, but key checks (Query Store, Wait Stats, backup history) set to 15–20s.

**Escalation:** If `TimedOutCount` exceeds 20% of Quick Check runs in beta, review timeout values per check; introduce `"quickTimeouts": false` flag on inherently slow but high-value checks and remove them from Quick subset.

---

### P2-03 — Rules-engine translator quality uneven

**Failure mode (revised):** With ~40 Quick Check checks, templates derive from existing YAML fields (`title`, `impact`, `recommendations`). Quality depends on YAML quality, not manual authorship. Full VA findings (any that surface) also translated, but those are the ones that matter (failures/passes). sp_Blitz provides fallback if any check fails to parse.

**Mitigation:** YAML is already high-quality (Brent Ozar/Microsoft sourced). Template placeholders: `{Category}`, `{Severity}`, `{BusinessRisk}`, `{Recommendations}`. Gemma fills from CheckResult + Governance output. No need to author 343 templates — only the ~40 Quick subset + any Full VA findings that appear (which are far fewer than 343 in practice).

**Updated P(P1):** 8% → **4%** (content pipeline solid; template count manageable; fallback exists)

---

### P2-04 — Translator cache invalidation on weights reload

**Failure mode:** `governance-weights.json` hot-reload changes category weights → per-finding `BusinessRiskBand` may change → cached translations become stale (Executive shows wrong risk level).

**Mitigation:** Cache key includes `weightsHash` (SHA256 of file contents at translation time). On weights change, `GovernanceService` raises `WeightsChanged` event; `FindingTranslator` clears cache. Also, translator version bump on any template change.

**Simplification:** Translator only runs for findings that actually exist (returned tests). sp_Blitz/VA returns only findings that occurred; pass/fail checks still get translated (PASS has "no issue" rendering). Scope is manageable: ~40 Quick subset + any Full VA findings that surface. Not 343 always.

**Updated P(P1):** 4% → **2%** (cache key correct; test added; limited to actual findings)

---

### P2-05 — ChartTheme refactor surfaces regressions

**Failure mode:** Grep-and-replace to `ChartTheme.Current` across 10+ components introduces subtle breakage: `TimeSeriesChart` custom baseline overlay logic lost; `StatCard` sparkline colors not applied; GaugeChart (maturity %) not created.

**Mitigation:** Create `ChartTheme` with full `GetOptions<T>()` method; update one component at a time; commit after each; visual regression screenshot per component.

**Escalation:** If Week 1 chi-squared diff of screenshots exceeds threshold, roll back and refactor more carefully; consider phasing integration (Week 1a: TimeSeriesChart only; Week 1b: others).

---

### P2-06 — RBAC Konscious Argon2 package not available for .NET 8 win-x64

**Failure mode:** `Konscious.Security.Cryptography.Argon2` latest version does not support .NET 8 or Windows-x64 native dependencies; build fails; no Argon2id hashing.

**Mitigation:** Verify package compatibility before Week 4. If unavailable, fall back to `Isopoh.Cryptography.Argon2` (also OWASP-compliant) as backup plan — documented in D04 alternative path.

**Escalation:** Pin to `Konscious 2.*` (known good on .NET 6/7); if still unavailable, file upstream issue, switch backup package within 1 day. Add compatibility check to Week 0 build validation.

---

### P2-07 — AuditLog chain validation false positive on rotation

**Failure mode:** 90-day key rotation with 7-day grace period creates edge case: checkpoint file written with old key, rotation happens, next startup validates against new key → HMAC fail, flags "compromised" incorrectly.

**Mitigation:** Checkpoint file header includes `KeyVersion` (1 = current, 2 = previous). Validation logic: try both keys if file version matches grace window. Trial correctly in unit test with two keys.

**Escalation:** If false-positive rate >1% in long-run testing, extend grace or record "key rotation boundary" in file header with explicit acceptance logic.

---

## User Experience Risks

### P2-08 — Onboarding admin bootstrap friction

**Failure mode:** First-run forces admin creation with password strength ≥12 chars and zxcvbn ≥3. Casual tester on laptop with "password123" rejected three times → frustration → abandonment.

**Mitigation:** Provide inline password strength meter; list exact requirements; offer "generate strong password" button; allow admin to proceed with weak password but show warning banner "We recommend a stronger password."

**Escalation:** Track drop-off funnel; if >40% abort at admin creation, relax to ≥8 chars without zxcvbn minimum — still better than none.

---

### P2-09 — Quick Check WAN users hit budget consistently

**Failure mode:** Remote SQL (Azure SQL, 100+ ms RTT) users always exceed 60s even with DOP=2 and WAN mode. They never see the "instant triage" value prop.

**Mitigation:** Already have Extended Check fallback. But tagline says ≤60s from launch — if they're remote, it's a broken promise.

**Escalation:** Consider remote-server Quick Check definition: server auto-tagged "remote" if RTT>50ms → skip Quick Check auto-launch, show Onboarding step "Your server is remote; Quick Check will take 2–3 minutes. Proceed?" with explicit consent. Truth-in-advertising > broken promise.

---

## Technical Debt & Integration Risks

### P2-10 — Quick Check vs Full VA drift coupling

**Failure mode:** `ComputeIndicativeAsync` and `ComputeFullAsync` share most code but diverge when weights reload or bugs fixed in only one path. Governance Report could disagree with Dashboard numbers.

**Mitigation:** Shared private method `ComputeCore(CheckResults, bool isIndicative)`; `ComputeIndicativeAsync` calls with subset; `ComputeFullAsync` calls with full set + `isIndicative=false`. Only flag differs; all clamping logic identical.

**Escalation:** Unit test: same weights, same check set flagged both ways → overall score identical. If not, block release.

---

### P2-11 — Translation rules-engine maintenance burden

**Failure mode:** 40 Quick Check templates hardcoded in C#; each VA query change requires translator template update; easy to forget; translations stale; users report mismatched findings.

**Mitigation:** Link each template key directly to `queries.json` check ID. Add test: for every `"quick": true` check ID, translator returns non-empty DBA/IT/Executive fields; fail build if any missing.

**Escalation:** Automated CI test ensures 100% coverage of Quick subset translation; missing template blocks merge.

---

### P2-12 — Governance weights JSON schema drift

**Failure mode:** `GovernanceService` expects `categories.{name}` → double, but admin manually edits `governance-weights.json` and introduces trailing comma or string value → hot-reload throws `JsonException`, Governance Dashboard breaks.

**Mitigation:** Use `System.Text.Json` with `JsonSerializerOptions.CommentHandling = JsonCommentHandling.Skip` to allow comments; wrap load in try-catch, log error, keep previous weights in memory; surface yellow banner.

**Escalation:** Schema test reads `governance-weights.json` at startup, validates all required keys + value types; fails fast in CI if malformed.

---

## Acceptance Validation Gaps (from Opus §A.9)

Opus identified 5 missing acceptance criteria. Add to PRD §11:

- [ ] RBAC admin bootstrap flow completes on first-run (admin user created; no bypass possible)
- [ ] Session timeout enforces (Admin/DBA 8h, Auditor 4h) — auto-logout after idle
- [ ] AuditLog startup chain validation detects tampering (test: inject bad HMAC row → compromised flag, refuses writes)
- [ ] Per-check timeout honored (inject slow DMV taking >8s → check cancelled, Quick Check still ≤60s overall)
- [ ] `governance-weights.json` hot-reload triggers Governance + Translator cache invalidation on next render

All five now tracked in D15, D05, D11, D13 respectively.

---

## Risk Priority Summary

| Priority | ID | Failure Mode | Likelihood | Impact | Mitigation |
|----------|----|--------------|------------|--------|------------|
| P1 | P2-02 | Timeout mismatch — slow DMVs exceed 8s default | LOW | MEDIUM | Per-check `timeoutSec` override from YAML; quick subset excludes High-impact |
| P1 | P2-04 | Translator cache stale after weights reload | LOW | MEDIUM | Cache key includes `weightsHash`; test added |
| P2 | P2-03 | Translation phrasing too technical | LOW | MEDIUM | YAML-derived templates; beta NPS question |
| P2 | P2-06 | Argon2 package .NET 8 compatibility | LOW | HIGH | Week 0 build check; backup package documented |
| P3 | P2-07 | AuditLog rotation false positive | VERY LOW | LOW | Dual-key grace + file header version |
| P3 | P2-08 | Admin bootstrap friction | LOW | LOW | Strength meter + generate button |

**Combined P(any P1): ~6%** (down from 18–22% pre-corrections)

---

## Trigger Conditions (Week 0–4)

- **Week 1:** ICheckRunner unit tests fail on timeout/budget → reduce Quick subset to 35 checks; add 1 day buffer
- **Week 2:** ChartTheme refactor finds >3 components needing new component creation → allocate 0.5 day extra
- **Week 3:** Translator template coverage <95% for Quick subset → add 2-day refinement sprint (cut Cost-of-Downtime widget to compensate)
- **Week 4:** Any P1 blocker active → scope freeze, 3-day stabilization sprint, drop 1 Should-have if needed

**Overall probability of clean Week 4 gate: 87%**

---

**Conclusion:** No showstopper new risks. consolidated_checks.sql quality de-risked content pipeline substantially. Proceed to Week 0 with confidence. Minor adjustments to acceptance criteria already applied (local envelope, weights hash cache key, translator scope).

**Residual risk acceptable:** ≤6% P1 probability, 87% on-time delivery.

**Next:** Attach this PRE-MORTEM_PASS2.md to Opus prompt for Pass 2 review; await final sign-off before Week 0 execution.

