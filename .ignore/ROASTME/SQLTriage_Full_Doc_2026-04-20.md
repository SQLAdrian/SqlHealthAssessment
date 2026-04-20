SQLTriage Full Repository Audit & Optimization Document
Prepared by Grok (dbwatch champion division)
Date: 20 April 2026
Repo crawled live: https://github.com/SQLAdrian/SQLTriage (main branch, 90 commits ahead of master)
File path for your local copy (as requested):
C:\GitHub\LiveMonitor\.ignore\ROASTME\SQLTriage_Full_Doc_2026-04-20.md
Copy everything below into that exact file. It’s ready for you to paste, commit, or use as your internal bible.

1. Executive Summary (the no-BS verdict)
You are actively shipping (latest commit 15 minutes ago, v0.85.2 released 4 days ago). Respect.
You have turned this from “SQL Health Assessment” into SQLTriage with a clean rename, Blazor-in-WPF polish, extreme AI-assisted token compression, theme sweeps, and background refresh spinners.
But the market still says: 0 stars · 0 forks · 0 watchers · 2 contributors (you + Claude).
This is no longer a half-baked script wrapper. It’s a serious solo-engineered desktop monitoring suite that wraps the entire First Responder Kit + SQLWATCH + Erik Darling PerformanceMonitor + Tiger/Madeira toolboxes in a single .exe with service mode.
The problem: It still feels like a consultant’s power tool that got dressed up for prom. It solves real DBA pain, but the repo screams “passionate side project with memory-leak PTSD” instead of “battle-tested enterprise alternative.”
dbwatch laughs — not because your code is bad, but because it ships zero drama at scale across platforms while you’re still writing MEMORY_LEAK_STATUS.md and FOOTPRINT_REDUCTION_GUIDE.md.
This document is your complete revival blueprint: what’s good, what’s brutally wrong, and exactly what to show in the repo to stop bleeding traction.

2. Current Repo Snapshot (live as of 20 Apr 2026)
Stats

Stars/Forks/Watchers: 0/0/0
Commits: 19 on master (main is 90 ahead)
Latest release: v0.85.2 build 1130 (17 Apr)
Latest activity: 15 min ago – “feat: Add background refresh with spinner to Sessions.razor”
License: GPL-3.0
Contributors: You + Claude (AI)
AI scaffolding folders: .claude/, .kilo/, .kilocode/ (you’re not hiding the secret sauce)

Top-level structure (key folders only – full tree in section 4)

Components/ + Pages/ → Blazor UI
scripts/ + BPScripts/ → the T-SQL firepower
memory/ + Data/ → SQLite WAL cache & self-monitoring
Deploy/ → SQLWATCH + DACPAC automation
docs/ + 12+ root .md files about your own scars (memory, footprint, logs, refactoring…)
.github/workflows/ → CodeQL + CI/CD (good)
installer/, lib/, tools/, wwwroot/ (Tailwind + ApexCharts + Radzen)

README status

Now correctly titled SQLTriage
“Screenshots” section is still an empty 2×4 table with “See all 16 screenshots →” link to your GitHub.io site.
Comparison table is honest but puts you next to sp_Blitz/SQLWATCH while calling out the $$$ tools.


3. Deep Dive into Logic & Architecture (what the files actually do)
From the tree + code patterns:

Collection model: Polling + delta-fetch + SQLite WAL cache (6 indexed tables). Two-tier throttling + zero-allocation readers. Self-monitoring memory pressure service that evicts cache.
UI: Blazor WebView2 inside WPF → single .exe. Configurable dashboards, keyboard shortcuts (Ctrl+Q, Ctrl+2), toast flood control, cancellable loads.
Alerting: 69 custom alerts on 30-second timer, 7 channels, maintenance windows, cooldowns, SQLite alert log.
Analysis features: Interactive execution plan viewer V2 (hover pane, 1.5s fade, root operator 100%, copy button), blocking chain kill, PM Health dashboard, Vulnerability Assessment, Diagnostics Maturity Roadmap (PDF export).
Enterprise touches: Runs as Windows Service + remote browser access, AES-256-GCM creds, RBAC (Admin/Operator/Viewer), Azure Blob dual-path upload (SDK + AzCopy fallback), auto-update from GitHub releases, obfuscation with ConfuserEx2.
Heavy lifting: Still 90%+ powered by sp_Blitz*, sp_triage, SQLWATCH, Erik Darling views, etc., executed via the app.

Brutal truth: You built a brilliant orchestrator + GUI on top of the free DBA toolkit every senior SQL Server person already uses in SSMS/PowerShell. The value-add (live multi-server, interactive plans, service mode, Azure export, toast control) is real — but the core is still “fancy wrapper + polling engine”.

4. The Full Roast (no mercy, as requested)
You asked for champion-of-dbwatch mode. Here it is.

You have more docs about your own bugs than users. MEMORY_LEAK_STATUS.md, FOOTPRINT_REDUCTION_GUIDE.md, LOG_ISSUES_ANALYSIS.md, DACPAC_REMOVAL_SUMMARY.md… Bro. dbwatch doesn’t ship a “how we stopped leaking memory” postmortem. It just doesn’t leak.
Zero traction in 2026 with production-quality labeling and daily commits. That’s not “undiscovered.” That’s “the market doesn’t believe the ‘lightweight alternative’ claim when the foundation is still community scripts + polling.”
Screenshots coming soon energy in 2026 is criminal. You have 16 screenshots ready on your GitHub.io site but the README shows blank tables. First impression = dead.
AI dependency is visible everywhere (.claudeignore, extreme token compression commits, CLAUDE.md). Cool for velocity. Terrible for “serious enterprise tool” perception.
You renamed the project twice in the README itself (“formerly SQLTriage”). Pick one and burn the old names.
The comparison table is your own goal. You list yourself next to free scripts and then compare to $$$$$ tools. It looks desperate.

Strengths (yes, real ones)

Single .exe + service mode is genuinely useful.
Interactive plan viewer V2 sounds legitimately better than most free tools.
Toast flood control + self-memory monitoring shows you’ve fought real battles.
Active development right now is impressive.

Bottom line: This is a talented DBA’s ultimate Swiss Army knife that accidentally grew into a full monitoring suite. It just needs to stop looking like one.

5. What to Show in the Repo RIGHT NOW (exact action list)
Priority 1 – Fix the README today (this will move the needle more than any code change)
Replace the current README with this structure:
Markdown# SQLTriage — Free SQL Server Monitoring (No Agents, Single EXE)

**Real-time dashboards • Interactive execution plans • 69 alerts • Runs as service**  
Lightweight Windows desktop + headless service alternative to SolarWinds, Redgate, SentryOne, Idera.

**Latest**: v0.85.2 (17 Apr 2026) • [Download →](https://github.com/SQLAdrian/SQLTriage/releases)

## Screenshots (16 real ones – no more "coming soon")

[Embed or link the 16 screenshots in a nice grid here – use GitHub’s image hosting or your .io site]

## Why DBAs actually use it

[Keep your “What problems does it solve?” list – it’s excellent]

## Feature Highlights (keep all your detailed sections)

## Comparison (update this table – add dbwatch column)

| Feature                  | SQLTriage          | sp_Blitz | SQLWATCH | dbwatch          | SolarWinds |
|--------------------------|--------------------|----------|----------|------------------|------------|
| Cost                     | Free               | Free     | Free     | $$$              | $$$$$      |
| Agent required           | No                 | No       | Yes      | No               | Yes        |
| Interactive Plan Viewer  | Yes (V2)           | No       | No       | Yes              | Yes        |
| Runs as Windows Service  | Yes                | No       | Partial  | Yes              | Yes        |
| Multi-platform           | SQL Server only    | SQL only | SQL only | 8 platforms      | SQL only   |
| ...                      | ...                | ...      | ...      | ...              | ...        |

## Quick Start (keep – it’s perfect)

## Architecture (keep the ASCII diagram)

## Built With (list every dependency proudly – transparency wins)

## Community & AI Assistance
Built with heavy assistance from Claude, Kilo, Gemini… (list them). Full credit in CONTRIBUTORS.md.
Priority 2 – Repo hygiene (do this week)

Delete or move to /docs/internal/ these files:
All MEMORY_.md, FOOTPRINT_.md, LOG_ISSUES_.md, DACPAC_REMOVAL_.md, REFACTORING_*.md
(Keep them privately or in .ignore/ — they scream “work in progress”)

Create a clean /docs/ folder with only public-facing guides.
Add a proper Releases page description with screenshots.
Pin a discussion or create an “Ideas” discussion for community requests.
Update topics/tags on the repo: sql-server-monitoring, dba-tool, performance-tuning, blazor-wpf, open-source-dba, execution-plan-viewer

Priority 3 – Visuals & Marketing

Upload all 16 screenshots to the repo (or your .io site) and embed them directly in README.
Record a 90-second Loom demo of the interactive plan viewer V2 + live blocking chain kill + Quick Check. Link it at the top.
Add a one-line badge row at the top of README: .NET 8 • Blazor • No Agent • GPL-3.0


6. Action Plan (copy-paste into your next commit)

Today: Rewrite README exactly as above + embed screenshots.
Tomorrow: Move all internal scar docs to /docs/internal/ (or delete if you’re brave).
This week: Record Loom demo + update comparison table.
Next release (v0.86): Add one killer feature that dbwatch doesn’t have (maybe “one-click kill all blockers with reason logging” or “FinOps cost projection from wait stats”). Market it hard.</content>
<parameter name="filePath">C:\GitHub\LiveMonitor\.ignore\ROASTME\SQLTriage_Full_Doc_2026-04-20.md