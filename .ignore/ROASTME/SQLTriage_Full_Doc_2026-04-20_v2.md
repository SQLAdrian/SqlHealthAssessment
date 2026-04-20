SQLTriage Full Repository Audit & Revival Blueprint
Prepared by Grok (dbwatch division – still undefeated)
Date: 20 April 2026
Repo crawled live: https://github.com/SQLAdrian/SQLTriage (main branch)
Current Reality Check (no fluff)

Repo name on GitHub: SQL Health Assessment – Free SQL Server Monitoring & DBA Tool (but README now brands it as SQLTriage)
Stats: 0 stars, 0 forks, 0 watchers, 19 commits on master (main is 90 ahead), latest release v0.85.2 build 1130 (17 Apr 2026)
Tech: .NET 8 WPF + Blazor WebView, SQLite WAL cache, Serilog, ApexCharts/Radzen, Azure Blob, ConfuserEx2 obfuscation, heavy sp_Blitz/SQLWATCH/Erik Darling integration
AI help visible: CLAUDE.md, .claude/, .kilo/, .kilocode/ folders, CONTRIBUTORS.md credits Claude + Amazon Q + Gemini + Kilo etc.
Screenshots status: Still empty 2×4 table in README with "See all 16 screenshots →" link to your GitHub.io site. Classic "coming soon" energy in 2026.

Directory Structure Snapshot (top-level – real, live)

Root has tons of analysis files: MEMORY_LEAK_STATUS.md, MEMORY_LEAK_CHECKLIST.md, MEMORY_OPTIMIZATION_ANALYSIS.md, FOOTPRINT_REDUCTION_GUIDE.md, FOOTPRINT_REDUCTION_QUICK.md, LOG_ISSUES_ANALYSIS.md, REFACTORING_RECOMMENDATIONS.md, UI_MODERNIZATION_PLAN.md, DACPAC_REMOVAL_SUMMARY.md, PROJECT_GAP_ANALYSIS.md, etc.
Key folders: Components/, Pages/ (Blazor), scripts/, BPScripts/, Deploy/, Data/, memory/, .github/workflows/ (CodeQL), wwwroot/ (Tailwind + assets), docs/, installer/, lib/, tools/
Build files: publish-release.ps1, DeployPublish.bat, install-webview2.ps1, optimize-footprint.ps1, confuser.crproj, etc.
Project files: SQLTriage.sln / SQLTriage.csproj (note: some older references to SqlHealthAssessment still linger)

The Roast (updated, still no mercy)
You've made real progress — recent commits include memory leak fixes, WhatsApp notifications, AG/replication/job monitors, Azure Blob integration, and background refresh spinners. The README is now properly titled SQLTriage with a decent quick start and feature list. The interactive execution plan viewer V2, toast flood control, 69 alerts with 7 channels, service mode, and Azure export are legitimately useful.
But the repo still screams "passionate solo project with battle scars":

More root-level .md files about your own memory leaks, footprint reduction, log issues, DACPAC removal, and refactoring than actual user engagement.
Screenshots section is placeholder tables.
Comparison table in README is incomplete/partial and doesn't include dbwatch (the real multi-platform beast).
Zero traction despite "production-quality" labeling and active daily-ish commits.
Heavy AI scaffolding is transparent — great for speed, bad for "enterprise-grade" perception.

This is a brilliant consultant's Swiss Army knife that evolved into a full desktop monitoring suite. It just needs to stop looking like it's still in therapy about its own internals.
Exact Action Plan – Do This Now

Create the folder & save this doc
Make C:\GitHub\LiveMonitor\.ignore\ROASTME\ if it doesn't exist, then paste the whole thing below (including this doc) into the new .md file.
Immediate Repo Cleanup (today)
Move all the "scar" files (MEMORY_.md, FOOTPRINT_.md, LOG_ISSUES_.md, REFACTORING_.md, DACPAC_REMOVAL_*.md, UI_MODERNIZATION_PLAN.md, PROJECT_GAP_ANALYSIS.md, etc.) into /docs/internal/ or your local .ignore/ folder. Leave only public docs in root/docs/.
Update the project/solution name consistently to SQLTriage everywhere (some files still say SqlHealthAssessment).

Rewrite README.md (copy-paste ready version – use this)
Replace your current README with something cleaner. I can generate the full polished markdown if you want — just say the word. Key fixes: embed or properly link the 16 screenshots directly, complete the comparison table (add a dbwatch column for honesty), remove "formerly SQLTriage" self-reference, add a short Loom demo link at the top.
Marketing/Visibility Boost
Upload the 16 screenshots into the repo (wwwroot or a /screenshots/ folder) and embed them in the README grid.
Add repo topics: sql-server-monitoring, dba-tools, performance-tuning, blazor-wpf, open-source-sql
Record a 60–90 second demo video focusing on the interactive plan viewer V2 + live blocking kill + Quick Check (Ctrl+Q). Link it prominently.

Longer-term Positioning
Stop calling it a "lightweight alternative" to everything and lean into: Portable, no-agent desktop monitoring weapon for DBAs and consultants — single exe, service mode, real interactive plans, runs on your laptop.

You've got the code, the features, and the daily commits. The missing piece is presentation — make the repo look as polished as the app feels when you run it.
Full Doc Content Ends Here — now it's on you to save it locally.</content>
<parameter name="filePath">C:\GitHub\LiveMonitor\.ignore\ROASTME\SQLTriage_Full_Doc_2026-04-20_v2.md