<!-- In the name of God, the Merciful, the Compassionate -->
<!-- Bismillah ar-Rahman ar-Raheem -->

# D06 — Report Engine: QuestPDF

**Date:** 2026-04-18
**Decision:** Use QuestPDF 2024.x for Governance Report PDF generation. Replaces `PrintService` for governance-specific reports.

**Rationale:**
- Fluent API superior to iTextSharp licensing complexity
- Good Blazor integration via QuestPDF.Previewer (dev-time)
- MIT-licensed, actively maintained
- Supports headers/footers, tables, charts (via image snapshots from ApexCharts)

**Report Structure (3 pages):**
1. **Cover** — SQLTriage logo, report date, server name
2. **Executive Snapshot** — Overall Risk Level (capped Governance score), Operational Maturity %, Downtime Exposure, Audit Readiness; key insight (one sentence from IFindingTranslator)
3. **Configuration Drift Summary** — PASS/WARN/FAIL table mapped to SOC2/HIPAA controls (stated as "aligned with")
4. **Security Risks** — over-privileged accounts checklist (R-03 etc.)
5. **Cost Efficiency Insights** — CPU waste, licensing opportunities, projected annual savings
6. **Priority Action Plan** — Top 10 fixes ranked by business ROI (from Risk Register)
7. **Appendix** (optional) — raw findings table

**Styling:** Playfair Display headings (18px), emerald/amber/rose color bands, 1-inch A4 margins.

**Cost of Downtime calculator:** User sets revenue/hr in Settings; tool multiplies by exposure hours; embedded in PDF footer.

**Integration:** `ReportService.GenerateGovernanceReport(GovernanceReport)` returns `byte[]`; bind to "Export PDF" button on Governance page; sample PDF at `wwwroot/samples/SQLTriage-Governance-Sample.pdf`.

**Sources:** COMMENT_20260418_091241.md §7; SQLTriage_PRD.md §4 (Governance Report Export) and §9 (QuestPDF task 34); WORKFILE_remaining.md Task 34

