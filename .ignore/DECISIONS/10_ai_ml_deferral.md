<!-- In the name of God, the Merciful, the Compassionate -->
<!-- Bismillah ar-Rahman ar-Raheem -->

# D10 — AI/ML & UI Theme Deferral to v1.1

**Date:** 2026-04-18
**Decision:** Full AI/ML ONNX pipeline (anomaly detection, multi-variate forecasting, SignalR streaming) deferred to v1.1. Tailwind UI migration (full Rolls Royce theme) deferred to v1.1. Quick Check hook requires only ChartTheme charts, not component styling.

**Rationale:**
- 33 tasks in 8 weeks impossible; pre-mortem velocity trigger mandates scope trim
- Must-haves focus on translation substrate and governance core
- AI/ML adds ~8 tasks (model training, ONNX integration, forecast UI, anomaly hub) — non-blocking for v1.0 value prop
- Tailwind migration adds ~10 tasks (install, configure, migrate 15+ components) — cosmetic, not functional

**v1.0 interim:**
- Use ApexCharts default dark theme for charts
- `ChartTheme` singleton ensures premium palette on charts only (no component styling)
- `SQLTriage_PRD.md` §5 AI/ML spec retained for future reference
- `DEVELOPMENT_STRATEGY.md` §17 Tailwind strategy retained for v1.1

**v1.1 plan (high level):**
- AI/ML: Python scikit-learn training on `sp_triage` historical data → ONNX export → `Microsoft.ML.OnnxRuntime` inference; `PredictiveService` + `/capacity` page + SignalR `AnomalyHub`
- UI Theme: Tailwind config, glassmorphism panels, Playfair/Inter fonts, migrate all `.razor` components from inline styles to Tailwind utility classes over 2-week sprint

**Trigger for reconsideration:** After v1.0 Must-haves shipped and validated with 5 beta testers. Re-open scope planning document.

**Sources:** COMMENT_20260418_091241.md §2; SQLTriage_PRD.md §5 (marked DEFERRED) and §12 Out of Scope; WORKFILE_remaining.md Task 23 & 24 marked DEFERRED; DEVELOPMENT_STRATEGY.md Could-Have section

