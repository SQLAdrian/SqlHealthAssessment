<!-- In the name of God, the Merciful, the Compassionate -->
<!-- Bismillah ar-Rahman ar-Raheem -->

# D07 — ChartTheme Singleton (Rolls Royce Palette)

**Date:** 2026-04-18
**Decision:** Create `ChartTheme` singleton in `Data/Services/ChartTheme.cs` providing ApexCharts options with Rolls Royce styling. Applied to all chart components.

**Palette:**
- Emerald (good): `#10B981` → `#34D399` gradient
- Amber (warn): `#F59E0B` → `#FBBF24` gradient
- Slate (neutral): `#475569` → `#94A3B8`
- Rose (critical): `#E11D48` → `#F43F5E`

**Typography:**
- Title: Playfair Display, 18px, `#1E293B` (slate-800), weight 600
- Axis labels: Inter, 11px, `#64748B`

**Defaults:**
- Grid dashed light slate (`#334155` stroke-dash 4)
- Smooth bezier curves (`curveType: 'smooth'`)
- Tooltip shared, legend top-right
- Forecast series: dashed (5px), stroke width 2px
- Confidence band: Area series, fill opacity 0.15

**Integration:**
- `TimeSeriesChart.razor` → `ChartTheme.GetOptions<TimeSeriesPoint>(Title, YAxisUnit)`
- `StatCard.razor` sparklines → apply `SeriesColors[0]` with rounded caps
- `Governance/Dashboard.razor` donut charts → emerald/amber/rose palette
- Any new chart must call `ChartTheme.GetOptions()`; hardcoded colors rejected

**Why:** Charts are most-viewed visual element; must convey premium quality on first impression.

**Sources:** COMMENT_20260418_091241.md §10; SQLTriage_PRD.md §8 (ChartTheme spec) and §5.12; DEVELOPMENT_STRATEGY.md ChartTheme strategy; WORKFILE_remaining.md Task 36

