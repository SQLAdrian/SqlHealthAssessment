<!-- In the name of God, the Merciful, the Compassionate -->
<!-- Bismillah ar-Rahman ar-Raheem -->

# SQLTriage — Caveman Context File

**Purpose:** Dense, token-efficient project overview for AI agents (Claude, Kilo, Cline, Amazon Q, Grok).  
**Format:** Minimal grammar, maximum information density.  
**Usage:** Drop into workspace root; agents read this instead of long PRD/strategy docs.

---

## Project Identity

```
Project:    SQLTriage
Type:       Enterprise SQL Governance tool (agentless, Windows-only)
Stack:      C# / Blazor Hybrid / WebView2 / SQLite WAL
UI:         Custom CSS + ApexCharts; Tailwind migration planned v1.1
Goal:       Executive-ready compliance/risk reports (SOC2/POPIA)
License:    GPL v3
Repo:       github.com/SQLAdrian/SqlHealthAssessment
```

---

## Current State (2026-04-19)

```
Branch:     main
Commit:     96440a5 (ci: exclude research_output + theme report)
Week:       Week 0 complete → Week 1 starting
Status:     Build pending; code complete, not yet compiled
Rename:     SqlHealthAssessment → SQLTriage (fully propagated)
Basmalah:   100% tracked .md files compliant
Attribution:Claude, Kilo, Cline, Amazon Q, Grok credited via Co-Authored-By
```

---

## Architecture (Option D — In-Place Hardening)

```
Pattern:    Keep existing codebase, harden gaps, add new subsystems
Reason:     80% complete, battle-tested, fits 8-week timeline
No rewrite: Fresh rewrite rejected (too risky, loses stability)
New pieces:  ICheckRunner (abstraction), GovernanceService, IFindingTranslator,
             AuditLog (queue + tamper detection), RBAC (Argon2id), ChartTheme,
             ThemeSwitcher (3 variants: default, rolls-royce, amg)
Integration:New services register in DI (App.xaml.cs), configs in JSON,
             charts via ApexCharts JS interop
```

---

## Tech Stack

| Layer | Technology |
|-------|------------|
| Runtime | .NET 8 (net8.0-windows), WPF + Blazor Hybrid |
| UI | Blazor components (Razor), WebView2, ApexCharts |
| DB | SQLite (WAL mode, local cache), SQL Server (target) |
| Auth | Windows auth + optional SQL auth; RBAC with Argon2id |
| Logging | Serilog (file rolling, daily) |
| Config | JSON files in `/Config` directory |
| Reports | QuestPDF (server-side PDF generation) |
| CI | GitHub Actions (codeql, release with Inno Setup) |
| Installer | Inno Setup (MSI optional) |

---

## Core Concepts

```
Check:      Individual SQL health/VA check defined in Config/queries.json
            Tags: "quick" (≤60s) or "full" (comprehensive)
            Metadata: title, description, severity, remediation, tags

ICheckRunner: Abstraction layer. RunSubsetAsync(quick|full|custom) enforces
             55s timeout budget (Quick leaves 5s buffer). Per-check timeout
             overrides via queries.json "timeoutSec" field.

GovernanceService: Capped scoring (P1≤40, Category≤60, Total≤100).
                   Vector weights: Security 30%, Performance 25%, Reliability 25%,
                   Cost 15%, Compliance 5%. Hot-reload from governance-weights.json.

IFindingTranslator: Rules-engine template system. 40 Quick checks mapped
                    to executive summaries. Template version → cache key.
                    Fallback: raw finding text if translation fails.

AuditLog: Single-writer queue + checkpoint recovery. Writes to SQLite
          + Windows Event Log (if admin). Tamper detection: HMAC on load
          refuses writes on mismatch. Retention: 30 days (configurable).

RBAC: Roles: Admin, DBA, Auditor, ReadOnly. Permissions via RbacGuard
     component. Bootstrap: first-run creates Admin from Windows user.
     Passwords: Argon2id (Konscious lib, 12 iterations, 128MB memory).

ChartTheme: Three variants via [data-theme] CSS attribute.
           Personality vars: radius (2–16px), transition (80–500ms),
           shadow depth, glassmorphism (blur 0–12px).
           ApexCharts palettes per theme (success/warning/critical/neutral).
```

---

## File Structure (Key Paths)

```
/Config
  queries.json               ← 60+ check definitions (id, sql, quick tag, timeoutSec)
  governance-weights.json     ← scoring weights (editable by user, hot-reload)
  control_mappings.json       ← compliance framework mappings
  appsettings.json            ← runtime config (connection strings, timeouts)
  version.json                ← version + whatsnew array

/Data
  /Services
    UserSettingsService.cs    ← persists user prefs (JSON in %APPDATA%)
    ChartThemeService.cs      ← theme colour palettes + change events
    ICheckRunner.cs           ← abstraction for check execution
    GovernanceService.cs      ← scoring engine
    IFindingTranslator.cs     ← natural language summarisation
    AuditLogService.cs        ← tamper-evident audit trail
    RbacService.cs            ← role/permission checks
    ... (other services)

/Components
  /Shared
    StatCard.razor            ← KPI card (uses ChartTheme colours)
    TimeSeriesChart.razor      ← ApexCharts wrapper (theme-aware)
    BlockingTreeViewer.razor   ← hierarchical blocking view

/Pages
  Settings.razor             ← UI Theme dropdown (default/rolls-royce/amg)
  QuickCheck.razor           ← Quick check trigger + results
  FullAudit.razor            ← Full VA execution + PDF export
  About.razor                ← version + build info

/wwwroot/css/app.css         ← 217KB theme system; all radii/transitions var()-driven
/installer/SQLTriage.iss     ← Inno Setup script (AppName=SQLTriage)

/.ignore
  SQLTriage_PRD.md           ← full requirements (1099 lines)
  DEVELOPMENT_STRATEGY.md    ← Option D rationale + Week 0–6 plan
  WORKFILE_remaining.md      ← 39 implementation tasks
  DECISIONS/                 ← 15 architectural decision records (D01–D15)
```

---

## Current Configuration

```
Theme:         default (can switch to rolls-royce or amg in Settings)
Quick timeout: 55s max (60s total minus 5s buffer)
Full timeout:  300s (5 minutes) — configurable via queries.json
Cache:         SQLite WAL, auto-maintenance every 4h
Audit log:     Enabled, 30-day retention, tamper detection ON
RBAC:          Not yet implemented (Week 3)
Governance:    Not yet implemented (Week 2)
Translator:    Not yet implemented (Week 2)
```

---

## Open Tasks (Week 1–6)

**Week 1 (Now):**
- [ ] `SqlQueryRepository` — load `queries.json`, filter by `quick: true` tag
- [ ] `ChartTheme` integration — inject palette into ApexCharts options
- [ ] Tag 40 checks as Quick (runtime ≤60s total)
- [ ] Verify Quick Check passes on test server (SQL 2019)

**Week 2:**
- [ ] `GovernanceService` — capped scoring + hot-reload
- [ ] `IFindingTranslator` — rules-engine templates + cache invalidation
- [ ] ErrorCatalog — 60 scenarios with governance impact
- [ ] Equivalence test: Quick vs Full scoring drift <5%

**Week 3:**
- [ ] `AuditLogService` — single-writer queue + checkpoint
- [ ] `RBAC` — Argon2id password hashing, role guard components
- [ ] Onboarding wizard step 1: Admin bootstrap

**Week 4:**
- [ ] `ReportService` + QuestPDF — 3-page executive PDF
- [ ] RoleGuard coverage on all pages
- [ ] Startup chain validation (tamper detection)
- [ ] Gate 2 review

**Week 5–6:**
- [ ] Polish: error messages, accessibility, performance tuning
- [ ] MSI installer (optional)
- [ ] Screenshots (16) for docs
- [ ] Release candidate

---

## Known Constraints

```
Build:        dotnet build SQLTriage.sln — zero warnings target
OS:           Windows 10/11, Server 2016+ (WebView2 runtime required)
SQL:          SQL Server 2016+ (Azure SQL MI supported, Azure DB partial)
Memory:       200MB baseline + cache size (default 500MB)
Database:     No agent install; uses standard SQL connections only
```

---

## AI Agent Quickref

```
When editing:
- Always add basmalah header to new files (<!-- Bismillah... -->)
- Follow CLAUDE.md conventions (file naming, DI patterns)
- Commit with Co-Authored-By trailer for AI assistants
- Do NOT write SQL unless explicitly requested (user owns SQL)
- Preserve existing patterns; fix gaps; do NOT rewrite

Communication:
- No pleasantries. Be concise. Output code + "Done."
- Only explain if asked "Why?"
- Assume Adrian is human engineer reviewing all changes

Testing:
- Build must succeed: dotnet build -warnaserror (after Week 1 T1 cleanup)
- Run tests: dotnet test (if tests exist for changed area)
- Manual smoke test: launch SQLTriage.exe, navigate to affected page

Style:
- PascalCase for methods/properties; camelCase for locals
- XML docs on public APIs
- Nullable reference types enabled
- Async suffix for async methods
```

---

## Token-Saving "Zip File" State

```
Context snapshot (post-Week0):
Rename:complete, Basmalah:100%, Theme:default/rolls-royce/amg ready,
Services:ChartThemeService added, DI:registered, MainWindow:theme inject,
Settings:UI dropdown wired, CSS:var sweep done (386 subs), Build:pending.
Next:Week1 — SqlQueryRepository + Quick tag + ChartTheme palette hook.
```

---

## Decision Index (D01–D15)

```
D01  Option D (In-Place Hardening) — no rewrite
D02  SqlQueryRepository abstraction (ICheckRunner) — separate Quick/Full
D03  Governance capped scoring (P1≤40, cat≤60, tot≤100)
D04  governance-weights.json hot-reload via IOptionsMonitor
D05  Argon2id (Konscious lib) for RBAC password hashing
D06  AuditLog single-writer + checkpoint recovery
D07  QuestPDF for reports (no live chart embeds)
D08  ICheckRunner timeout budget: 55s Quick / 300s Full
D09  Basmalah Intent Lock — every file starts with Islamic header
D10  Translator rules-engine (40 Quick templates) + versioned cache
D11  AuditLog startup HMAC check — tamper detection → refuse writes
D12  Translator cache key: (findingId, translatorVersion, weightsHash)
D13  ChartTheme via CSS variables + personality (radius/transition/shadow)
D14  Theme switcher UI in Settings (default/rolls-royce/amg)
D15  RoleGuard component + Admin bootstrap via Onboarding Step 1
```

---

## Quick Links (for AI)

```
Full PRD:            .ignore/SQLTriage_PRD.md
Strategy:            .ignore/DEVELOPMENT_STRATEGY.md
Tasks:               .ignore/WORKFILE_remaining.md
Checklist:           .ignore/SQLTriage_Release_Checklist.md
Decisions:           .ignore/DECISIONS/D01-D15.md
Pre-Mortem:          .ignore/PRE-MORTEM_PASS3.md
Opus Analysis:       .ignore/OPUS_ANALYSIS_COMPLETE_2026-04-18.md
Contributors:        CONTRIBUTORS.md
Build Instructions:  DEPLOYMENT_GUIDE.md
```

---

**Token count:** ~1200 tokens (compressed vs ~5000 for full PRD).  
**Update frequency:** Revise after each week gate.  
**Maintainer:** Adrian Sullivan (human engineer, all changes reviewed).

---

*End of Caveman Context File*
