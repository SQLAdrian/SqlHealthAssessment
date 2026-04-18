<!-- In the name of God, the Merciful, the Compassionate -->
<!-- Bismillah ar-Rahman ar-Raheem -->

# D03 — IFindingTranslator Service (Core Differentiator)

**Date:** 2026-04-18
**Decision:** Implement `IFindingTranslator` as dedicated service producing three audience-specific renderings per finding (DBA / IT Manager / Executive).

**Rationale:**
- Opus identified GovernanceService alone does NOT translate — this is the product's core differentiator
- Translation substrate is what saved clients >$50M: DBA language → CFO language
- Without this layer, tool is just another DBA utility, not a governance platform

**Outputs per finding:**
- `FindingDba`: Technical details, T-SQL snippets, wait stats, index DMVs, error numbers, repro steps
- `FindingItManager`: SLA impact, remediation effort estimate (hours/days), root cause category, affected systems
- `FindingExecutive`: One-sentence plain-language summary, business risk (HIGH/MEDIUM/LOW/CRITICAL), cost exposure (USD/hr downtime), compliance flags (SOC2 control IDs), priority rationale

**Implementation:**
- Models in `Data/Models/Translation/Finding{Dba,ItManager,Executive}.cs`
- Service: `Data/Services/FindingTranslator.cs` implementing `IFindingTranslator`
- Cached in `SqliteCacheStore` with TTL (1 hour or on VA re-run)
- Pulls metadata from `Data/Sql/queries.json` → each check has `audienceTags: ["dba","it","exec"]`
- Governance scoring uses `Executive.BusinessRisk` as input
- RBAC filters visible translations per user role correlating to audience

**Non-negotiable:** Every VA check must produce all three outputs. No exceptions.

**Sources:** COMMENT_20260418_091241.md §3; SQLTriage_PRD.md §7; WORKFILE_remaining.md Task 31

