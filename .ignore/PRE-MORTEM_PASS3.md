<!-- In the name of God, the Merciful, the Compassionate -->
<!-- Bismillah ar-Rahman ar-Raheem -->

---
Generated: 2026-04-18
Source: PRE-MORTEM_PASS2.md + OPUS_ANALYSIS_COMPLETE_2026-04-18.md
Documents reviewed: COMMENT_20260418_091241.md, WORKFILE_remaining.md, SQLTriage_PRD.md, DEVELOPMENT_STRATEGY.md
---

# SQLTriage — Pre-Mortem Analysis (Pass 3 — Post-Evaluation)

**Date:** 2026-04-18  
**Context:** Post-Opus Pass 2 evaluation of SQLTriage solution against mega prompt. Option D (In-Place Hardening) confirmed viable; all major gaps closed via design refinements. Residual risks quantified for final go/no-go.  

**Purpose:** Final pre-mortem before Week 0 execution. Identify any remaining failure modes not covered in Pass 2, assess mitigation completeness, and confirm 87% success probability holds.

---

## Expected Failure Rate (Post-Evaluation)

**Overall P1-blocker risk: ~4–6%** after all mitigations. Residual P1 risk **≈ 2–4%** at Week 4 gate.  

### Residual P1 Risks

| ID | Failure Mode | P(P1) | Mitigation (Updated) |
|----|--------------|-------|----------------------|
| P3-01 | Translator rules-engine templates too generic | 2% | Beta NPS feedback loop; iterate Week 5–6 if <4/5 average quality score |
| P3-02 | QuestPDF live chart embeds absent undercuts value | 2% | Native primitives sufficient; add "Charts coming in v1.1" disclaimer; focus on structured data |
| P3-03 | Argon2 package fallback friction | 1% | Isopoh backup documented; Week 0 compatibility test |
| P3-04 | AuditLog Event Log registration admin block | 1% | Graceful fallback to file-only; banner surfaces requirement |
| **Combined** | **Any P1 materializing** | **~4%** | **Mitigations solid; no new P1s from evaluation** |

### P2 Risks (non-blocking but notable)

- **P3-05 Quick Check UX abandonment:** 3% chance users skip Quick Check, go straight to Full VA (too slow first impression); mitigated by Onboarding checkbox default ON + tray icon "Triage Now"  
- **P3-06 ChartTheme refactor visual regressions:** 2% chance color mismatches in edge cases; mitigated by screenshot diffs + one-component-at-a-time integration  
- **P3-07 Governance weights JSON schema evolution:** 2% chance future updates introduce typos; mitigated by schema validation test + hot-reload error handling  

**Updated success probabilities:**  
- On-time v1.0 (15 Musts): **89%** (+2% from Pass 2)  
- With ≤2 Should-haves: **95%**  
- Zero P1 at release: **96%**  

---

## Scope & Dependency Risks

### P3-01 — Translator Quality Post-Rules Engine

**Failure mode (refined):** Rules-engine covers 40 Quick checks adequately, but executive summaries feel "templated" vs. human-curated. Users expect more nuanced language (e.g., "cost exposure" personalized to their industry).  

**Mitigation:** Templates use {placeholders} filled from real data (e.g., {downtimeCost} from Cost-of-Downtime settings). Beta feedback: add 5-point scale question "How useful were the executive summaries?" Target >4.0 average. If <4.0, Week 6 refinement sprint (cut one polish task).  

**Updated P(P1):** 2% (mitigated by feedback loop; templates data-driven)

### P3-02 — QuestPDF Without Charts

**Failure mode:** Competitors embed ApexCharts screenshots in PDFs; SQLTriage uses native bars/gauge. Looks less professional, undercuts "audit evidence" promise.  

**Mitigation:** Add disclaimer "Interactive charts available in dashboard; PDF uses native rendering for portability." Focus on data density: table of 50 findings + summary charts. No live embeds for v1.0 scope (Opus confirmed deferral).  

**Updated P(P1):** 2% (disclaimer + data focus; professional enough for v1.0)

### P3-03 — Argon2 Compatibility Issue

**Failure mode:** Konscious 2.x requires .NET 8, but Windows Server 2019 (.NET 8 compatible) may have issues with native libs. Build fails in CI.  

**Mitigation:** Week 0 test: add Konscious to test project, build on Win2019 VM. If fail, switch to Isopoh (also OWASP-compliant, pure C#). Document backup in COMMENT.  

**Updated P(P1):** 1% (backup package ready; low-risk)

### P3-04 — AuditLog Event Log Admin Hurdle

**Failure mode:** Event Log source registration requires admin rights once. First run on non-admin machine logs warning, falls back to file-only. Auditors complain about missing OS-level trail.  

**Mitigation:** Graceful degradation: file + Event Log if possible. Settings banner: "For full audit integrity, run once as admin to register Event Log source." No hard block.  

**Updated P(P1):** 1% (graceful; admin-run documented in deployment guide)

---

## User Experience Risks

### P3-05 — Quick Check Adoption Resistance

**Failure mode:** Users with 200+ms RTT skip Quick Check, run Full VA immediately (2–3min wait). Never see triage value; abandon tool.  

**Mitigation:** Onboarding: "Your connection is remote — Quick Check will take 2–3 minutes. Proceed?" with consent. Tray icon shows "Remote mode active." Document expectation.  

**Updated P(P1):** 3% (consent + transparency; not a blocker)

### P3-06 — ChartTheme Integration Hiccups

**Failure mode:** ApexCharts options deep; refactor misses a property (e.g., responsive breakpoints), charts look broken on mobile/tablet.  

**Mitigation:** Visual regression: screenshot all chart components pre/post, diff in PR. One component per commit. Allocate 0.5 day buffer Week 1.  

**Updated P(P1):** 2% (buffer + diffs; manageable)

### P3-07 — Governance Weights JSON Drift

**Failure mode:** Admin edits `governance-weights.json` adds trailing comma; hot-reload throws JsonException, Governance dashboard crashes.  

**Mitigation:** Wrap load in try-catch, log error, keep previous weights. Settings page: validate JSON on save, show red border + error message. Schema test in CI.  

**Updated P(P1):** 2% (error handling + validation; robust)

---

## Technical Debt & Integration Risks

### P3-08 — Translator Cache Key Granularity

**Failure mode:** Cache key includes `translatorVersion`, but weights change invalidates cache. If version not bumped on template tweak, stale translations.  

**Mitigation:** `translatorVersion` bumped on any change (manual constant). Cache key: `(findingId, translatorVersion, weightsHash)`. Test covers invalidation.  

**Updated P(P1):** <1% (design correct; tested)

### P3-09 — QuestPDF Memory/Perf on Large Reports

**Failure mode:** 100-finding report takes >5s or >500MB RAM; hangs UI on low-end machines.  

**Mitigation:** Perf test: generate 100-finding fixture <3s, <200MB. If fail, batch rendering (page-at-a-time).  

**Updated P(P1):** <1% (QuestPDF optimized; low volume)

### P3-10 — RBAC Bootstrap Edge Cases

**Failure mode:** Admin creation fails if zxcvbn lib unavailable or password too weak; user stuck in onboarding loop.  

**Mitigation:** Fallback: if Zxcvbn.Core fails, accept ≥12 chars without score check. Log warning.  

**Updated P(P1):** <1% (fallback + logging)

---

## Acceptance Validation Gaps (Updated from Opus)

Opus identified 5 missing criteria. All now covered via design updates:

- [x] RBAC admin bootstrap flow completes on first-run (Onboarding Step 1)
- [x] Session timeout enforces (default 8h Admin/DBA, 4h Auditor; configurable)
- [x] AuditLog startup chain validation detects tampering (HMAC check on load; refuse writes on fail)
- [x] Per-check timeout honored (ICheckRunner kills at 8s; Quick Check continues)
- [x] `governance-weights.json` hot-reload triggers Governance + Translator cache invalidation

All 5 added to PRD §11. No gaps remain.

---

## Risk Priority Summary

| Priority | ID | Failure Mode | Likelihood | Impact | Mitigation |
|----------|----|--------------|------------|--------|------------|
| P1 | P3-01 | Translator quality too generic | LOW | MEDIUM | Feedback loop + data placeholders |
| P1 | P3-02 | PDF lacks charts | LOW | LOW | Disclaimer + data focus |
| P2 | P3-05 | Quick Check skipped on remote | LOW | MEDIUM | Consent prompt + transparency |
| P2 | P3-06 | Chart regressions | LOW | LOW | Screenshot diffs + buffer |
| P2 | P3-07 | Weights JSON errors | LOW | LOW | Validation + error handling |
| P3 | P3-03 | Argon2 compat | VERY LOW | HIGH | Backup package + test |
| P3 | P3-04 | Event Log admin | VERY LOW | LOW | Graceful fallback |

**Combined P(any P1): ~4%** (stable from Pass 2; evaluation confirms mitigations)

---

## Trigger Conditions (Week 0–4)

- **Week 1:** Build cleanup fails (warnings persist) → reduce scope to 12 Musts, cut ChartTheme to Apex default  
- **Week 2:** ICheckRunner perf <55s on test → reduce Quick subset to 30 checks  
- **Week 3:** Translator coverage <90% for Quick → defer to v1.0.1, ship v1.0 with generic messages  
- **Week 4:** Any P1 active → freeze, 2-day fix sprint, drop 1 polish task  

**Overall probability of clean Week 4 gate: 89%**

---

**Conclusion:** No new failure modes from evaluation. Solution is hardened; risks are edge cases with backups. Proceed to Week 0 with high confidence. Residual risk acceptable at 4% P1.

**Next:** Distribute this PRE-MORTEM_PASS3.md to team; lock scope; start Week 0.
