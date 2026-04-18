<!-- In the name of God, the Merciful, the Compassionate -->
<!-- Bismillah ar-Rahman ar-Raheem -->

# SQLTriage — Active Decision Log

**Last updated:** 2026-04-18
**Status:** Decision capture complete; Opus Pass 1 corrections applied. Waiting for Opus review on PRE-MORTEM_PASS2.

This file is a lightweight index to the detailed decision records in `.ignore/DECISIONS/`. Each decision file is self-contained (~150–250 tokens) and versioned by topic.

## Decision Index (13 decisions)

| ID | Topic | File |
|----|-------|------|
| D01 | Architecture: Option D (In-Place Hardening) | `.ignore/DECISIONS/01_architecture_option.md` |
| D02 | Scope v1.0: Must/Should/Could split (15 Must-haves; Must #11 dropped) | `.ignore/DECISIONS/02_scope_v1.0.md` |
| D03 | Translation Substrate: IFindingTranslator service (rules-engine, Quick subset) | `.ignore/DECISIONS/03_translator_service.md` |
| D04 | RBAC: Argon2id via Konscious.Security.Cryptography + OWASP params + admin bootstrap + RoleGuard + session timeout | `.ignore/DECISIONS/04_rbac_hashing.md` |
| D05 | AuditLog: Single-writer + 64 KB checkpoint + DPAPI-machine HMAC + 90d key rotation + 7d grace + chain validation + Event Log fallback | `.ignore/DECISIONS/05_auditlog_design.md` |
| D06 | Reporting: QuestPDF engine for 3-page Governance Report | `.ignore/DECISIONS/06_report_engine.md` |
| D07 | Charts: ChartTheme singleton (Rolls Royce palette) | `.ignore/DECISIONS/07_chart_theme.md` |
| D08 | UX: Quick Check ≤60s triage hook (budget 55s, DOP ≤4, per-check 8s timeout) | `.ignore/DECISIONS/08_quick_check_hook.md` |
| D09 | Policy: Basmalah intent lock enforcement (pre-commit + CI) | `.ignore/DECISIONS/09_basmalah_policy.md` |
| D10 | Deferral: AI/ML ONNX & Tailwind UI to v1.1 | `.ignore/DECISIONS/10_ai_ml_deferral.md` |
| D11 | Governance: Capped scoring (3-level clamp) + vector weights JSON (25/20/20/15/20) + maturity bands JSON + Quick(Indicative) vs Full(Official) | `.ignore/DECISIONS/11_governance_scoring.md` |
| D12 | Basmalah: Enforcement mechanism details (`./scripts/check-basmalah.ps1`) | `.ignore/DECISIONS/12_basmalah_policy.md` |
| D13 | ICheckRunner interface + budget/timeout abstraction for Quick vs Full | `.ignore/DECISIONS/13_icheckrunner_interface.md` |
| D14 | Data flow: Check → Governance (capped) → BusinessRiskBand → Translator (read-only consumer) | `.ignore/DECISIONS/14_translator_governance_flow.md` |
| D15 | RBAC: Admin bootstrap (Onboarding), RoleGuard component, session timeout (8h/4h), local password reset | `.ignore/DECISIONS/15_rbac_bootstrap_roleguard.md` |

## How to Use This Index

- **New session:** Read this file first (≈250 tokens). Then read only the specific D*.md files relevant to the task at hand.
- **Opus review:** Point to `.ignore/DECISIONS/` — Opus can glob all 15 files (~3500 tokens total) vs monolithic 5k+ token COMMENT.
- **Traceability:** Each decision cites sources (WORKFILE line, PRD section, Opus analysis reference).

## Decision Capture Process

1. Create `.ignore/DECISIONS/XX_topic.md` (two-digit sequence, underscore-separated, lowercase; XX = sequential number)
2. Fill with: Decision / Rationale / Implementation / Rejected alternatives / Sources
3. Update this index (add row to table, increment count)
4. Link from parent planning docs (WORKFILE, PRD) by referencing decision ID (e.g., "See D13")

## Status Summary

- ✅ Architecture defined (Option D)
- ✅ Scope trimmed & revised (15 Must-haves; Must #11 dropped per Opus)
- ✅ Translator core specified (rules-engine, Quick subset only)
- ✅ RBAC + AuditLog + Governance + Report all finalized per Opus Pass 1
- ✅ ICheckRunner abstraction created
- ✅ Data flow circularity resolved
- ⏳ **Awaiting:** Opus Pass 2 review (PRE-MORTEM_PASS2.md analysis)

## Outstanding Items (for next Opus pass)

- `.ignore/PRE-MORTEM_PASS2.md` — fresh pre-mortem on current state (post-corrections)
- `OPUS_MEGA_PROMPT_COMPLETE.md` updated to reference PRE-MORTEM_PASS2 as required reading
- DECISIONS files D02–D05, D11 updated per Opus corrections (done); D06–D12 unchanged (no Opus feedback on those yet)

## Next Actions

1. **Create PRE-MORTEM_PASS2.md** — analyze current state (post-Opus corrections) for new failure modes introduced
2. **Update Opus prompt** to include PRE-MORTEM_PASS2 in required reads
3. **Begin Week 0 execution:** build cleanup (CA1416, nullable), brand rename (31 files), basmalah pre-commit hook

