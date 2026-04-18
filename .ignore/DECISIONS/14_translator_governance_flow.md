<!-- In the name of God, the Merciful, the Compassionate -->
<!-- Bismillah ar-Rahman ar-Raheem -->

# D14 — Translator ↔ Governance Data Flow (Explicit Non-Circular)

**Date:** 2026-04-18
**Decision:** Data flow is strictly one-way: `Check Result → GovernanceVector (capped) → BusinessRiskBand → Translator (renders)`. Translator is a read-only consumer; it does NOT produce or modify Governance state.

**Flow stages:**
1. **Raw check:** `VulnerabilityAssessmentService.RunChecksAsync()` executes VA queries → produces `CheckResult { CheckId, Severity, Category, RawDataDictionary, Passed: bool }`
2. **Governance vector:** `GovernanceService.ComputeFullAsync()` consumes all `CheckResult`:
   - Applies per-finding cap (40/25/10/5)
   - Groups by category, sums, applies per-category ceiling
   - Averages category contributions → overall score 0–100
   - Derives **per-finding `BusinessRiskBand`** from capped per-finding contribution: `if contribution >= 30 → "CRITICAL"`, `>=20 → "HIGH"`, `>=10 → "MEDIUM"`, else `"LOW"`
   - Returns `GovernanceReport { OverallScore, MaturityBand, PerCategory }` + attaches `BusinessRiskBand` to each finding in a side table
3. **Translator input:** `IFindingTranslator.TranslateAsync(findingId)` receives:
   - `CheckResult` (raw data, category, severity)
   - `QueryMetadata` from `SqlQueryRepository` (`audience` tags, `controls` list, `governance` category weight overrides)
   - `BusinessRiskBand` from GovernanceService output (read-only)
4. **Translator output:** `FindingTranslation { Dba, ItManager, Executive }`; `Executive.BusinessRisk` = input `BusinessRiskBand`; `Executive.ComplianceControls` = `QueryMetadata.controls`; `Executive.PlainLanguageSummary` = template rendering of (category, severity, BusinessRiskBand)

**Cache key:** `(findingId, translatorVersion, governanceWeightsVersion)` — invalidates when either input changes.

**Why explicit:** PRD draft implied Governance scoring consumes `Executive.BusinessRisk` (circular). This dependency direction is backwards. Fix: Governance produces; Translator consumes.

**Sources:** Opus §3 (circular dependency identified), §A.1 (translator consumes Governance, not co-producer), COMMENT D03 originally; PRD §7 Implementation amended

