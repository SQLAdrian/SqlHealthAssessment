# SQLTriage — Remaining Work

> **Diagnostic Philosophy:** Diagnose deeply → Export thoroughly → Decide manually
>
> SQLTriage is a diagnostic superset (deep-dive analysis tool), not a monitoring platform (operational automation). We enrich findings, provide prescriptive guidance, and export evidence packages — but DBA retains agency to decide and act. This differentiates from dbWatch's "Monitor → Alert → Automate → Report" closed-loop model.

**Critical Gap Type — Presentation Layer:** Many diagnostic capabilities already exist in the codebase (SQL checks, baseline calculations, health scoring) but are **not exposed in the UI**. These are **visibility gaps**, not engine gaps. Priority 0 is making the existing diagnostic data **obvious** to users.

**Repo:** SQLAdrian/SqlHealthAssessment  
**Current version:** 0.85.2 (build 1197)  
**Stack:** Blazor Hybrid WPF (.NET 8, net8.0-windows), single-exe, SQLite cache, Serilog  
**Key paths:** `Pages/` (37 .razor pages), `Data/Services/`, `Components/Shared/`, `Data/Caching/`  
**Do not touch:** SQL queries (user owns SQL), `.claude/docs/` for CSS patterns, `app.css` (7500 lines — grep don't read)

---

## DONE THIS SESSION (do not re-do)

- Maintenance mode global banner in MainLayout
- Live Monitor session prefetch on connection
- Chart data point cap slider in Settings
- Credential export/import (.lmcreds with PBKDF2 passphrase)
- Proxy-aware auto-update + detailed error messages
- Release workflow fix (increment-build.ps1 no longer pushes tags)
- GitHub Pages fully populated (badges, download cards, 16 screenshots, demo.gif)
- RBAC UI wiring (Settings → Access Control, Login page, page guards)
- DPI-aware manifest (PerMonitorV2)
- QUICKSTART.md
- Check Validator search + category chips

---

## REMAINING ITEMS

### PRIORITY 1 — Quick wins, self-contained

---

#### 1. Background refresh thread + "Refresh now" spinner
**File:** `Pages/Sessions.razor`  
**Problem:** `LoadSessions()` sets `IsLoading = true` which blanks the whole table. Data fetch should happen on a background thread with the old data still visible; only swap the table when new data arrives.  
**Diagnostic value:** Keeps context during refresh — DBA can continue analyzing sessions while data updates in background. Spinner provides feedback without disruption.  
**How:**
1. Add `_backgroundSessions List<SessionInfo>` field alongside `AllSessions`
2. In `LoadSessions()`, remove `IsLoading = true` at the top. Fetch into a local `newSessions` variable
3. Only after fetch completes: `AllSessions = newSessions; LastRefreshTime = ...; await InvokeAsync(StateHasChanged)`
4. Add a spinner icon next to the refresh timestamp that shows while the fetch is in-flight:
   ```razor
   @if (_refreshing)
   {
       <i class="fa-solid fa-spinner fa-spin" style="font-size:11px;color:var(--text-secondary);"></i>
   }
   ```
5. Add `private bool _refreshing;` — set to true at start of `LoadSessions()`, false at end (in finally block)
6. The `_loadLock.Wait(0)` guard is already in place — don't remove it

**Result:** Table never flashes blank. Spinner shows in the toolbar. Users can keep scrolling during refresh.

---

#### 2. Rate-limit status bar badge
**Files:** `Data/RateLimiter.cs`, `Components/Layout/NavMenu.razor` (or `MainLayout.razor`)  
**Problem:** `RateLimiter` is defined but not registered in DI and not shown anywhere.  
**Diagnostic value:** Visual feedback when query throttling activates — prevents user from thinking queries are slow when they're actually being rate-limited. Part of transparent diagnostic environment.  
**How:**
1. Register `RateLimiter` as singleton in `App.xaml.cs`:
   ```csharp
   services.AddSingleton<RateLimiter>();
   ```
2. In `QueryExecutor.cs` (or wherever queries run), inject `RateLimiter` and call `TryAcquire()` before executing
3. In `MainLayout.razor`, inject `RateLimiter` and add a small badge in the bottom-right corner:
   ```razor
   @if (_rateLimitActive)
   {
       <div style="position:fixed;bottom:8px;right:12px;z-index:1500;
                   background:#7c2d00;color:#ffb347;font-size:11px;
                   padding:3px 8px;border-radius:4px;font-family:monospace;">
           ⚠ throttled
       </div>
   }
   ```
4. Poll `RateLimiter.IsRateLimited` on a 5s timer in MainLayout (same pattern as maintenance banner timer)
5. Subscribe to `RateLimiter.RateLimitExceeded` event to show immediately when triggered

**Note:** `RateLimiter` has `IsRateLimited`, `CurrentQueryCount`, `MaxQueriesPerMinute`, `GetTimeToReset()`, `RateLimitExceeded` event. Check `Data/RateLimiter.cs` for the full API before wiring.

---

#### 3. Dashboard JSON schema validation — inline error + Reset to default
**Files:** `Pages/DashboardEditor.razor`, `Data/Services/DashboardConfigService.cs`  
**Problem:** If `dashboard-config.json` gets malformed, the app silently fails or throws. No inline validation in the editor.  
**Diagnostic value:** Prevents configuration corruption from breaking diagnostic dashboards — keeps analysis environment stable.  
**How:**
1. In `DashboardConfigService`, add a `Validate(string json)` method that:
   - Tries `JsonSerializer.Deserialize<DashboardConfig>(json)`
   - Returns `(bool valid, string? error)`
2. In `DashboardEditor.razor`, on the JSON text area `@oninput`:
   - Call `Validate()` and show a red border + error message below the editor if invalid
   - Disable the Save button while JSON is invalid
3. Add a "Reset to default" button that calls a new `DashboardConfigService.ResetToDefault()` method — copies the embedded default config from `Config/dashboard-config.json` (ship a `dashboard-config.default.json` alongside it)
4. The default file path: `Config/dashboard-config.default.json` — copy this from the current `Config/dashboard-config.json` as the baseline

---

#### 4. PDF/Excel export for tabular audit results
**Files:** `Pages/FullAudit.razor`, `Pages/VulnerabilityAssessment.razor`, `Data/Services/PrintService.cs`  
**Problem:** PDF export exists via `PrintService` for some pages. Excel export is missing entirely.  
**Diagnostic value:** Exports enable thorough offline analysis, sharing with colleagues, archiving for compliance, and importing into external tools (Excel, Power BI). Fulfills "Export thoroughly" mantra.  
**How for Excel (no new NuGet required — use CSV as Excel-compatible output):**
1. Add a `ExportToExcelAsync(DataTable data, string fileName)` helper in a new `ExcelExportService.cs` or inline in the page
2. Use `CsvHelper` (already in project? check) or write a simple TSV/CSV writer
3. If `CsvHelper` is not present, use the existing `CsvParser` pattern — it already writes CSV
4. For true Excel (.xlsx): add `ClosedXML` NuGet (`dotnet add package ClosedXML`) — it's MIT, lightweight, no COM dependency
5. For PDF: `PrintService.PrintToPdfAsync()` already exists — wire it to the audit results table the same way it's wired on other pages
6. Add "Export CSV" and "Export PDF" buttons to `FullAudit.razor` and `VulnerabilityAssessment.razor` toolbar

**CsvHelper check:** `grep -r "CsvHelper\|CsvParser" Data/` to see what's already there.

---

### PRIORITY 2 — Medium effort

---

#### 5. FAQ expansion + Support channel link
**Files:** `QUICKSTART.md`, `docs/index.md`, any in-app Help page  
**Items to add to FAQ:**
- Alert threshold tuning: "Why is my alert firing constantly?" → explain IQR baseline, how to adjust `NextAlertDelayMinutes`, how to use dry-run mode
- SQLWATCH is optional: many pages work without it; only historical dashboards need it
- Service mode setup: step-by-step, port 5000 default, Windows Service install via installer
- Credential migration: explain the new `.lmcreds` export in Settings → Server Credentials
- Debug log location: `logs/app-YYYYMMDD.log` next to the exe

**Support channel:**
1. Enable GitHub Discussions on the repo (Settings → Features → Discussions)
2. Add a "Get Help" link in the nav or About page pointing to `https://github.com/SQLAdrian/SqlHealthAssessment/discussions`
3. Optional: add a "Report a bug" button in the About page that opens `https://github.com/SQLAdrian/SqlHealthAssessment/issues/new?template=bug_report.md`

---

#### 6. Query Plan Viewer — ServerVersion/ServerEdition probe
**File:** `Pages/QueryPlanViewer.razor` (or wherever the plan modal is shown), `Components/Shared/QueryPlanModal.razor`  
**Problem:** `ServerVersion` and `ServerEdition` parameters are not populated at runtime, so the ONLINE/RESUMABLE index rebuild checkboxes are always enabled regardless of SQL Server version.  
**How:**
1. On page load, run: `SELECT SERVERPROPERTY('ProductVersion'), SERVERPROPERTY('EngineEdition')`
2. Parse major version (e.g. `15` = SQL 2019, `16` = SQL 2022)
3. Disable ONLINE checkbox if version < 9 (SQL 2005) or edition = Express (EngineEdition = 4)
4. Disable RESUMABLE checkbox if version < 14 (SQL 2017)
5. The `ConnectionHealthService.IsAzureSql(serverName)` method already exists — use it to detect Azure SQL and disable relevant options

---

#### 7. draw.io / SVG export of Environment View 
**File:** `Pages/EnvironmentView.razor` (or similar), the force-directed graph JS  
**Problem:** The topology graph is rendered in a `<canvas>` or SVG via D3/custom JS. Users want to export it as a draw.io-compatible XML or plain SVG for documentation.  
**How:**
1. Check how the graph is rendered: `grep -n "canvas\|svg\|d3\|topology\|force" Pages/EnvironmentView.razor`
2. If SVG: add a JS function `exportTopologySvg()` that serialises the SVG element to a string and triggers a download via `blazorDownloadFile` (already in `wwwroot/scripts/download.js`)
3. If Canvas: use `canvas.toDataURL('image/png')` for a PNG export, or redraw onto a hidden SVG
4. For draw.io XML: the format is straightforward — each server = `<mxCell>` with `style="shape=..."`, edges = connection cells. Generate from the same data model used to render the graph

---

#### 8. SQL Server CPU & Latency Benchmark (Item 15) - FOR NEXT PHASE. SKIP FOR NOW
**Context:** Initial SQL at `C:/temp/proc_stats_enriched.sql`  
**File to create:** `Data/Services/BenchmarkService.cs`, new page `Pages/Benchmark.razor`  
**Diagnostic value:** Quantitative performance benchmarking across servers — identifies hardware/VM bottlenecks, hypervisor contention, cross-instance performance differences. Adds objective metrics to subjective "wait stats" analysis.  
**How:**
1. The SQL benchmark runs inside SQL Server — it's safe read-only DMV queries plus arithmetic
2. Add the benchmark queries to `Data/Services/BenchmarkService.cs` with a `RunBenchmarkAsync(string serverName)` method
3. Results go into the SQLite cache via `liveQueriesCacheStore.UpsertStatValueAsync()`
4. New page at `/benchmark` with a "Run benchmark" button per server and a comparison table
5. Add scheduler delay + signal wait queries (already written in the worklist memory) to detect vCPU steal
6. Ratings: use these rough baselines (adjust after real-world testing):
   - Integer arithmetic: < 100ms = fast, 100–500ms = normal, > 500ms = degraded
   - String ops: < 200ms = fast, > 1s = degraded
   - Signal wait pct > 25% = likely hypervisor contention

---

### PRIORITY 3 — Larger sessions

---

#### 9. Documentation Generator + Installation Helper  - FOR NEXT PHASE. SKIP FOR NOW
**Status:** Design phase — user has docx templates as reference  
**Diagnostic value:** Auto-generates comprehensive server documentation (configuration, security, performance) from live diagnostics — saves hours of manual inventory. Installation helper provides pre-deployment checklist.  
**Planned pages:** `/documentation` (generate SQL Server state docs from live DMV data), `/installation-helper` (guided hardening steps)  
**Approach:**
1. Start by looking at the docx templates to understand what data is needed
2. The app already collects most of this: instance config (sp_configure), disk/memory, AG status, backup history, security findings from VA
3. Documentation page: assemble these into a structured view with export to PDF via PrintService
4. Installation Helper: a wizard-style page that checks current config against best practices and gives a checklist
5. dbatools.io integration: dbatools has a REST API — use HttpClient to call it for additional checks. The `AutoUpdateService._httpClient` pattern is the model.

---

#### 10. Code Signing (Item 20) — USER ACTION REQUIRED  - FOR NEXT PHASE. SKIP FOR NOW
**Rationale:** Builds trust in diagnostic tool — ensures users the executable hasn't been tampered with. Critical for tool that inspects production databases.
**Status:** Workflow is written and waiting. User needs to buy the cert.  
**Steps:**
1. Buy Certum OV cert at certum.eu (~$60/yr, individual validation, 1–3 days)
2. Install cert, export `.pfx` from `certlm.msc → Personal → Certificates → right-click → Export → PKCS#12`
3. Encode and add to GitHub Secrets:
   ```powershell
   [Convert]::ToBase64String([IO.File]::ReadAllBytes("cert.pfx")) | clip
   ```
   - Secret name: `CODESIGN_CERT_BASE64`
   - Secret name: `CODESIGN_CERT_PASSWORD`
4. Trigger a release: `git tag v0.85.3 && git push origin v0.85.3`
5. The `.github/workflows/release.yml` handles everything else automatically

---

#### 11. Public release posting plan
**Rationale:** Position SQLTriage as diagnostic specialist, not general monitor. Emphasize unique differentiators: no-agent, interactive plans, offline-capable, Windows-native.
**When ready** (after screenshots/GIF and code signing):  
- **r/SQLServer**: Title "SQLTriage — Deep diagnostic tool for SQL Server (free, no agents, interactive plans)" — focus on query plan viewer, blocking chains, VA findings export.
- **r/sysadmin**: Emphasize Windows Service mode + no network footprint + portable single-exe.
- **SQLServerCentral.com**: Article titled "Why I Built a Diagnostic-First SQL Server Tool" — contrasts with monitoring platforms; explains "Diagnose deeply → Export thoroughly → Decide manually" philosophy.
- **dev.to**: Technical deep-dive on Blazor Hybrid WPF (unusual combination) + SQLite cache strategy + interactive SVG plan rendering.
- **Hacker News Show HN**: "SQLTriage: Desktop SQL Server diagnostic tool with interactive execution plans, blocking analysis, and vulnerability assessment — no agents, MIT license" — be honest about scope (SQL Server only), highlight open-source.

---

## ARCHITECTURE NOTES FOR OTHER LLMS

### Project structure
```
SqlHealthAssessment.sln
├── App.xaml.cs               ← DI registration, startup, error handling
├── MainWindow.xaml.cs        ← WPF shell, BlazorWebView host
├── Pages/*.razor             ← @page routes (37 pages)
├── Components/Shared/*.razor ← Reusable components
├── Components/Layout/
│   ├── MainLayout.razor      ← App shell, banners, router
│   └── NavMenu.razor         ← Sidebar navigation
├── Data/
│   ├── ConnectionManager.cs  ← ServerConnectionManager
│   ├── UserSettingsService.cs ← All user prefs (user-settings.json)
│   ├── AutoUpdateService.cs  ← Update check, download, apply
│   ├── CredentialPorter.cs   ← Export/import credentials
│   ├── SessionDataService.cs ← Live sessions DMV queries
│   ├── Caching/
│   │   └── SqliteCacheStore.cs ← SQLite WAL cache
│   └── Services/
│       ├── AlertEvaluationService.cs
│       ├── ConnectionHealthService.cs
│       ├── NotificationChannelService.cs
│       ├── RbacService.cs
│       ├── PrintService.cs
│       └── ... (20 services total)
├── Config/
│   ├── version.json          ← { version, buildNumber, buildDate, whatsnew[] }
│   ├── dashboard-config.json ← Panel layout
│   └── appsettings.json
└── wwwroot/
    └── scripts/
        ├── download.js       ← blazorDownloadFile() helper
        └── app.js
```

### Key conventions
- C# files: `/* In the name of God, the Merciful, the Compassionate */` header
- Razor files: `<!--/* In the name of God, the Merciful, the Compassionate */-->` header
- DI: nullable optional params `Service? svc = null` — services may not be available
- Background tasks: `_ = Task.Run(async () => { ... })` pattern
- Credentials: always `CredentialProtector.Encrypt/Decrypt` — never store plaintext
- Connections: always specify database (`"master"` for DMV queries, not default)
- No Tailwind — uses CSS variable design system (`var(--accent)`, `var(--bg-secondary)`, etc.)
- No `<` in Razor `@code` switch statements — Razor reads it as HTML; use if/else
- Do not write SQL queries — user owns all SQL

### Build
```bash
dotnet build SqlHealthAssessment.sln
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
./increment-build.ps1   # bumps Config/version.json buildNumber only, no tags
# To release: git tag v0.85.3 && git push origin v0.85.3
```

### CSS design tokens (key ones)
```css
var(--accent)          /* green #00ff00 */
var(--bg-secondary)    /* dark panel background */
var(--bg-hover)        /* slightly lighter hover */
var(--border)          /* border color */
var(--text-secondary)  /* muted text */
var(--green)           /* success */
var(--red)             /* error */
var(--orange)          /* warning */
var(--blue)            /* info/link */
```

### MainLayout banner pattern (reference implementation)
Two banners already exist in `Components/Layout/MainLayout.razor`:
1. Update available — green accent, `position:fixed;top:0`
2. Maintenance mode — amber `#7c5a00`, stacks below update banner

Pattern for adding a new banner:
```razor
@if (_conditionActive)
{
    <div style="position:fixed;top:0;left:0;right:0;z-index:2000;
                background:COLOR;color:TEXT_COLOR;
                display:flex;align-items:center;justify-content:center;gap:12px;
                padding:7px 16px;font-size:13px;box-shadow:0 2px 6px rgba(0,0,0,0.3);">
        <i class="fa-solid fa-ICON"></i>
        <span>MESSAGE</span>
        <a href="/target-page" style="color:TEXT_COLOR;font-weight:600;text-decoration:underline;">Action →</a>
    </div>
    <div style="height:36px;"></div>
}
```
Timer polling pattern (30s):
```csharp
_timer = new System.Timers.Timer(30_000);
_timer.Elapsed += (_, _) => _ = InvokeAsync(() => { RefreshState(); StateHasChanged(); });
_timer.AutoReset = true;
_timer.Start();
// In Dispose(): _timer?.Stop(); _timer?.Dispose();
```

### Settings page pattern
All settings follow the same structure. To add a new setting:
1. Add property to `UserSettings` class in `Data/UserSettingsService.cs`
2. Add `Get/Set` methods to `UserSettingsService`
3. Add field + load in `LoadSettings()` in `Pages/Settings.razor`
4. Add UI in the appropriate `<div class="settings-group">` section

### Alert evaluation gate
`AlertEvaluationService` already checks `AlertWindowConfig.ShouldFire(alert.AlwaysAlert)` at line ~170.
`NotificationChannelService.GetAlertWindows()` returns the current config.
`NotificationChannelService.UpdateAlertWindows(config)` saves changes.
Manual maintenance: set `config.MaintenanceActiveUntil = DateTime.Now.AddMinutes(N)` then call `UpdateAlertWindows`.

---

## Competitive Context: dbWatch Comparison

**Philosophical Split:**
| Aspect | dbWatch | SQLTriage (target) |
|--------|---------|-------------------|
| Core model | Monitor → Alert → Automate → Report (closed loop) | Diagnose deeply → Export thoroughly → Decide manually (open loop) |
| Action | Automated jobs, scheduled reports, threshold alerts | Prescriptive guidance, manual trigger, user agency |
| Value prop | Operational efficiency (save DBA time via automation) | Diagnostic depth (find root cause faster with richer data) |
| Target user | Enterprise DBA teams managing hundreds of instances | Windows DBA/consultant diagnosing specific issues |
| Deployment | Server-agent, multi-platform | Single-exe desktop, SQL Server only |

**Implication for development:** We can match/exceed dbWatch on diagnostic richness (history, forensics, compliance evidence) while deliberately not building the automation layer. This is a feature, not a gap.

---

## WHAT GOOD OUTPUT LOOKS LIKE

For each coding task above:
1. Read the file(s) mentioned before editing
2. Use `grep` / `Glob` to verify class/method names before referencing them
3. Build after changes: `dotnet build SqlHealthAssessment.sln -c Release --no-restore`
4. Fix all errors before committing
5. Commit with: `git commit -m "feat/fix: description\n\nCo-Authored-By: Claude Sonnet 4.6 <noreply@anthropic.com>"`
6. Do NOT push unless explicitly asked
7. Do NOT create new .md documentation files unless asked
8. Do NOT add features beyond what is described

---

## New Tasks from Grok Audits (SQLTriage Revival)

### Repo Hygiene (Today)
**Rationale:** Clean project identity before public launch. Diagnostic tools need professional presentation to be taken seriously.
- Move all "scar" files (MEMORY_LEAK_STATUS.md, FOOTPRINT_REDUCTION_GUIDE.md, LOG_ISSUES_ANALYSIS.md, DACPAC_REMOVAL_SUMMARY.md, REFACTORING_RECOMMENDATIONS.md, UI_MODERNIZATION_PLAN.md, PROJECT_GAP_ANALYSIS.md) to `.ignore/ROASTME/` or `/docs/internal/`.
- Update project/solution names from "SqlHealthAssessment" to "SQLTriage" consistently in .csproj, .sln, and code.

### README Rewrite (Today)
**Rationale:** First impression establishes diagnostic positioning — "deep analysis, your decisions" — vs. dbWatch's automation message. Include dbWatch comparison table with "diagnostic-only" vs. "operational platform" distinction.
- Replace README.md with polished version: Hero section with badges, embedded screenshots grid (16 images), complete comparison table (add dbwatch column), Loom demo link, remove self-references.
- Upload screenshots to `/screenshots/` folder and embed in README.
**Diagnostic branding emphasis:** "No agents, no cloud, no automation — just deep SQL Server diagnostics on your machine."

### Marketing Boost (This Week)
**Rationale:** Attract SQL Server community by highlighting unique value: desktop app with interactive plan viewer, blocking kill, and no agent footprint.
- Add repo topics: sql-server-monitoring, dba-tools, performance-tuning, blazor-wpf, open-source-sql.
- Record 60-90s Loom demo of interactive plan viewer V2 + blocking kill + Quick Check.
- Add badges: .NET 8 • Blazor • No Agent • GPL-3.0.
**Messaging note:** Frame as "DBA's pocket diagnostic tool" — like a stethoscope, not a robotic surgeon.

### Feature Additions (v0.86 Prep)
- **Maintenance Recommendation Engine** (reframed from "Scheduled Maintenance Operations"): Add `Pages/MaintenanceAdvisor.razor` — analyzes index fragmentation, outdated stats, missing indexes, integrity issues → generates **review-ready T-SQL scripts** (REBUILD/REORGANIZE/UPDATE STATISTICS/DBCC). User reviews and executes manually. **No automation** — diagnostic output only. Uses Quartz.NET only if user wants scheduled generation (see item 369). Philosophy: Diagnose maintenance needs → Export fix scripts → Manual review/execute.
- **Management Templates** (reframed as Diagnostic Templates): Add `Pages/TemplateLibrary.razor` with predefined diagnostic templates (Wait Stats analysis, Blocking investigation, Performance baseline, Security audit) as JSON in `templates/`. Templates define: which queries to run, how to render results, what thresholds to apply. Allows apply/customize/export. Philosophy: "Diagnose with expert guidance" — templates encode diagnostic workflows, not automated actions.

### Positioning Shift
**Core message:** SQLTriage is a **diagnostic tool**, not a monitoring platform. Competes with dbWatch by going deep on SQL Server internals, not broad on automation. Users choose SQLTriage when they need to **investigate** (blocking, plan analysis, security findings), not when they need to **respond** (alerts, scheduled jobs). This justifies single-platform focus (SQL Server depth > multi-platform breadth).
- Stop "lightweight alternative" — position as "Portable, no-agent desktop diagnostic weapon for SQL Server DBAs — single exe, service mode, real interactive plans, no automation overhead."

### Critical Strategic Gaps (From Feedback)
**Note 2026-04-21:** Gap list updated with **diagnostic-first reframing**. Original dbWatch feature names retained for reference, but descriptions now align with "Diagnose deeply → Export thoroughly → Decide manually" mantra. Automation-leaning features converted to recommendation/reporting modes.
**Note:** All features below are filtered through diagnostic philosophy: "Diagnose deeply → Export thoroughly → Decide manually". We provide rich diagnostic output and prescriptive guidance, but DBA retains agency to review and act.

1. **Maintenance Recommendation Engine** (HIGH priority): [Reframed from "Automated Maintenance Execution"] Generate T-SQL scripts (index rebuild/reorganize, UPDATE STATISTICS, DBCC CHECKDB) based on diagnostic analysis. Scripts include explanations, risk notes, and rollback guidance. User reviews and executes manually. Effort: 2-3 weeks (script generation + validation).
2. **Historical Performance Repository** (HIGH priority): Extend SQLite schema to store aggregated metrics (hourly/daily rollups of wait stats, session counts, resource usage). Retention: 6-12 months configurable. Enables trend analysis, "when did this start?" diagnostics, and baseline comparisons. Effort: 1-2 weeks (schema + backfill + UI rollup views).
3. **Compliance Framework** (MEDIUM-HIGH priority): Map existing 489 VA checks to industry standards (CIS, PCI, NIST, GDPR). Generate compliance scorecard dashboard (percentage compliant by control family). Export audit-ready PDF packages with evidence (query text, plan screenshot, finding details). **No automated remediation** — just diagnostic reporting. Effort: 3-4 weeks (mapping research + scoring engine + report templates).
4. **Threshold-Based Filtering & Highlighting** (MEDIUM priority): [Reframed from "Threshold-Based Alerting"] Add UI controls to filter sessions/metrics by configured thresholds (CPU > X%, wait time > Y ms). Highlight rows that exceed thresholds with color coding. **No alerts, no emails, no always-on monitoring** — purely a triage aid during active diagnosis. User sets thresholds in Settings; IQR outlier detection remains as auto-detection complement. Effort: 1 week.
5. **Multi-Tenant / MSP Features** (LOW-MEDIUM priority): Add "Environment" abstraction to group servers (Dev/Prod/CustomerA/CustomerB). Multi-server health rollup dashboard. Template deployment for connection strings and common dashboard layouts. **No user isolation or billing** — purely organizational for consultants managing multiple clients. Effort: 4-6 weeks.
6. **Advanced Blocking Analysis** (MEDIUM priority): Store blocking events in history table. Timeline view showing blocking chain evolution. "Top Blocking Offenders" report (sessions that blocked others most over past 24h). Include SQL text for blocking and blocked statements. Effort: 2 weeks (event capture + timeline UI + report).
7. **Health Score & Risk Rating** (MEDIUM priority): Compute weighted index (0-100) from: performance degradation trends, compliance gaps, security findings, resource saturation, blocking frequency. Executive summary panel on dashboard. Tooltips explain score composition. Enables quick "is this server healthy?" answer. Effort: 1 week.
8. **Diagnostic Report Packages** (MEDIUM priority): [Reframed from "Scheduled & Automated Reporting"] One-click generation of: (a) Executive Summary (health score + top 5 risks + trend graphs), (b) DBA Handoff Package (full findings + connection details + known issues), (c) Audit Evidence (VA findings with screenshots/plans). **Optional scheduling** via internal timer (Quartz.NET) to generate reports at configured times and drop to disk — no email delivery, just file output. Effort: 1-2 weeks.
9. **Configuration Snapshot & Diff** (MEDIUM-LOW priority): [Reframed from "Configuration Drift Detection"] Manual "Save Baseline" captures current sp_configure + surface area config. "Compare to Baseline" highlights changes (additions/removals/modifications) with color-coded diff. No continuous monitoring — user-triggered diagnostic for post-change verification (e.g., after patch/upgrade). Effort: 2 weeks.
10. **Performance Baselines & Anomaly Detection** (LOW-MEDIUM priority): [Reframed from "Performance Benchmarks & Baselines"] Learn typical values per server (weekly pattern, hourly profile). Z-score anomaly detection flags metrics deviating >2σ from learned baseline. "Baseline period" configurable (e.g., "use last 30 days as normal"). Suppressed during known maintenance windows. Effort: 2-3 weeks (ML-lite training + anomaly scoring).

### Implementation Roadmap (Aligned to Diagnostic Philosophy)
**Note:** Phases built around diagnostic capabilities; automation/scheduling kept minimal and user-triggered only.

**Pre-phase — Presentation Gap Audit (P0 — before coding new features)**

Many diagnostic capabilities already exist in the engine (SQL queries, VA checks, health checks) but lack **presentation layer integration**. These are **work items** to make existing diagnostic data visible and actionable:

| Existing Infrastructure | Missing Presentation | Work Item |
|------------------------|---------------------|-----------|
| `SqlAssessmentService` (489 VA checks in `ruleset.json`) | No compliance mapping to CIS/PCI/NIST; no scorecard dashboard; VA page only shows raw findings | **Add Compliance Mapping Layer** — map check IDs to frameworks; add framework selector; compute compliance % per control family; color-coded scorecard on VA page |
| `AlertBaselineService` + `alert_baseline_stats` table | Baseline data only used for alerts; no UI to view "normal range" for any metric; no trend overlay on charts | **Expose Baselines on Dashboards** — add shaded p25-p75 band to all time-series charts; tooltip shows "normal range"; configurable baseline window (7d/30d/90d) |
| `HealthCheckService` + `ServerHealthStatus` | Health page exists but no single overall score (0-100); not shown on dashboard/home; no trend arrow | **Add Executive Health Badge** — compute weighted 0-100 score; display prominently on home page and sessions header; show ↑↓ vs yesterday |
| Session filters (HideSleeping, ShowOnlyBlocked, HideLowIO) | Only boolean filters; no numeric thresholds (CPU > X, Wait > Y ms) | **Add Numeric Threshold Filters** — Settings → Thresholds section; slider/input for CPU, memory, wait time; highlight rows exceeding thresholds |
| `PrintService.PrintToPdfAsync()` | PDF export exists on some pages but not all; no scheduled generation despite UserSettings having `VaScheduledPdfEnabled`/`RoadmapScheduledPdfEnabled` | **Wire Scheduler + Add Report Bundles** — implement background job that reads schedule settings, generates PDFs, saves to `%APPDATA%\SQLTriage\Reports\`; add Executive Summary, DBA Handoff, Audit Evidence bundles |
| Index fragmentation check in `ruleset.json` (PERF004) | Finding shows "high fragmentation" but no script to rebuild; no per-index recommendation | **Maintenance Script Generator** — page that lists fragmented indexes with `ALTER INDEX` scripts; includes rollback notes; copy-to-clipboard |
| Blocking queries in `SessionDataService` | Live blocking chain shows SPIDs but not the actual SQL causing block; no history | **Blocking Forensics Tab** — modal showing blocker's SQL text, plan, session info; store blocking events in new `blocking_events` table; timeline of last 24h |
| `SqliteCacheStore` with `cache_timeseries` | Raw data kept 7 days only; no rollup to monthly/quarterly for long-term capacity planning | **Time-Series Rollup Service** — daily/weekly aggregations; separate `cache_timeseries_rollup` table; query rollups when viewing >30d range |
| `DynamicDashboard` + `PreloadFromCacheAsync` | Cache preloading happens but timing/cache-hit rate is invisible; no metrics to tune performance; concurrency limits fixed at 5/10 | **Dashboard Performance Telemetry** — add Stopwatch to `LoadPanelDataAsync` and `PreloadFromCacheAsync`; log per-panel load time, cache hit/miss, SQLite read latency; expose optional UI overlay (Developer Mode) to see real-time metrics |
| `CachingQueryExecutor` + `QueryThrottleService` | Throttling limits hardcoded (MaxHeavyConcurrent=5, MaxLightConcurrent=10); not user-configurable; no visibility into current queue depth | **Concurrency Configuration** — add `MaxHeavyConcurrent` and `MaxLightConcurrent` to UserSettings; sliders in Settings → Performance (range 3-15 heavy, 5-30 light); read in `QueryThrottleService` constructor; display current active semaphore count in status bar when Developer Mode enabled |
| `CacheStateTracker` + `_hasLoadedOnce` flag | Dashboard preloads cache on first visit but no proactive warm-up; subsequent visits fast but first visit to any dashboard is cold | **Cache Warm-up on Startup** — after connection test, call `WarmCacheForDashboardAsync` for Home, QuickCheck, Health dashboards; run at low priority; respects `EnableDebugLogging=false` to avoid surprising background load |
| No panel lazy-loading | All panels load in parallel immediately; dashboard with 15+ panels overwhelms SQL even with cache preloading | **Lazy-Load Panels** (stretch) — Settings → `LazyLoadThreshold` (default 6); load first N panels synchronously, remainder after 200ms delay or when scrolled into view; reduces initial perceived load time |

**Action:** Complete all Pre-phase items **before** starting Phases 1-3. These "visibility gaps" are prerequisite to using the diagnostic data effectively.

- **Phase 1 (2-3 months)**: Historical repository (P0), Maintenance recommendation engine (P1), Health score (P1), Threshold filtering (P1), Advanced blocking (P1).
- **Phase 2 (3-4 months)**: Compliance framework (P1), Diagnostic report packages (P1), Config snapshot & diff (P2), Baselines & anomalies (P2).
- **Phase 3 (4-6 months)**: Multi-tenant environments (P2), Predictive capacity forecasting (stretch — anomaly-based, not automated response), Integration APIs (webhooks to feed findings into external ticketing systems — still diagnostic handoff, not auto-remediation).

**Guiding Principle:** Each feature must answer: "Does this help the DBA diagnose more deeply, export more thoroughly, or decide more confidently?" If yes → include. If it removes DBA agency or adds operational burden → defer to separate "SQLTriage Operator" module (future commercial add-on).

Files affected: README.md, WORKFILE_remaining.md (this file), new Pages/, templates/, .csproj, .sln.
Commit pattern: `git commit -m "feat: description\n\nCo-Authored-By: Claude Sonnet 4.6 <noreply@anthropic.com>"`
**Commit note:** Use `feat:` prefix for new diagnostics (e.g., `feat: add maintenance recommendation engine`), `fix:` for bug fixes, `refactor:` for code quality. All commits must pass `dotnet build` and ideally include brief manual test steps in commit body.
