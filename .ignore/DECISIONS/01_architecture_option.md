<!-- In the name of God, the Merciful, the Compassionate -->
<!-- Bismillah ar-Rahman ar-Raheem -->

# D01 — Architecture Approach (Option D — In-Place Hardening)

**Date:** 2026-04-18
**Decision:** Build on existing 80% complete codebase; NOT fresh bootstrap.
**Rationale:**
- Option C estimated +2 weeks bootstrap with negative ROI (Opus §4.2)
- Existing code battle-tested, integrated; regression risk < greenfield risk
- Quick Check UX already prototyped; triage-first hook can ship immediately
- 65% confidence Option D is correct per external Opus review
**Effect:**
- DEVELOPMENT_STRATEGY.md switched from Option C → Option D
- No scaffolding phase; Week 1 = hardened implementation of Must-haves
- Preserve existing patterns (Blazor SPA, ApexCharts, DI, Basmalah)
**Implications:**
- Keep `SqliteCacheStore`, `CredentialProtector`, `ConnectionManager` as-is
- Fix build warnings (CA1416) in place; no clean-slate rebuild
- Brand rename tasks (31 files) still required
**Reject:** Fresh `blazorhybrid-wpf` template bootstrap — waste of 2 weeks
**Sources:** Opus analysis .ignore/OPUS_ANALYSIS_COMPLETE_2026-04-18.md §4.2; pre-mortem velocity triggers

