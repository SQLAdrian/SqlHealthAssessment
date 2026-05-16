# SQLTriage — SQL Server Audit & Compliance Platform

**500+ checks. Framework-mapped findings. Governance scores. Remediation costs. All in seconds — agentless.**

**Latest**: v0.85.3 (May 2026) • [Download →](https://github.com/SQLAdrian/SQLTriage/releases)

---

## Screenshots

| Audit Dashboard | Compliance Map | Query Plans | Multi-Instance |
| --- | --- | --- | --- |
| ![Sessions](docs/screenshots/4-live-sessions.jpg) | ![Waits](docs/screenshots/3-wait-state.jpg) | ![Plans](docs/screenshots/14-query-plan-viewer.jpg) | ![Overview](docs/screenshots/6-instance-overview.jpg) |

Full gallery → `/docs/screenshots`

---

## What It Is

SQLTriage is an **agentless**, audit-first SQL Server compliance platform for DBAs, DBA firms, and MSPs.

Run it against a SQL Server. In seconds you get:

- A **governance score** (Bronze → Silver → Gold → Platinum) backed by 500+ checks
- **Framework gap analysis** — which findings map to NIST SP 800-53, CIS Controls v8, SOC 2, ISO 27001, STIG
- A **remediation cost estimate** — effort hours × failed checks × your hourly rate
- A **PDF report** the CIO can read — not a script output a DBA has to translate

Live monitoring runs in the background while you work. It's the "while you wait" companion, not the main event.

---

## The 15-Minute Assessment

1. Add the SQL Server — no agent, no install on the target
2. Run Quick Assessment — 500+ checks execute in under 2 seconds
3. Read the governance score — Security: 15/100. Backup: 0/100. Compliance gaps visible immediately
4. Export the PDF — governance score, top findings, framework gaps, remediation estimate
5. Present the proposal — the report IS the proposal

---

## Comparison

| Capability | SQLTriage | sp_Blitz | SQLWATCH | MS Vulnerability Assessment | SolarWinds DPA |
| --- | --- | --- | --- | --- | --- |
| Cost | Free | Free | Free | Free (SSMS only) | $$$ |
| Agents Required | No | No | Yes | No | Yes |
| Checks | 500+ | ~150 | — | ~100 | — |
| Framework Mapping | NIST / CIS / SOC 2 / ISO / STIG | No | No | Partial | No |
| Governance Score | Yes (maturity bands) | No | No | No | No |
| Remediation Costing | Yes (effort × rate) | No | No | No | No |
| PDF Report (CIO-facing) | Yes | No | No | No | Yes |
| Interactive Plan Viewer | Yes (V2) | No | No | No | Yes |
| Multi-Instance | Yes | No | Yes | No | Yes |
| Historical Trending | Yes (short-term) | No | Yes | No | Yes |
| Adaptive Alerting | Yes (IQR-based) | No | Basic | No | Yes |
| Runs as Windows Service | Yes | No | Partial | No | Yes |
| Single EXE | Yes | No | No | No | No |

**Positioning**: SQLTriage = audit-first compliance evidence with live monitoring built in. sp_Blitz = one-time health check. SQLWATCH = continuous history. MS VA = basic vulnerability scan. Enterprise tools = full observability.

---

## Why Not Just Use sp_Blitz or MS Vulnerability Assessment?

| sp_Blitz / MS VA Output | SQLTriage Output |
| --- | --- |
| `Priority 1: Backups not performed in 7 days` | Security: 15/100 — NIST CP-9 gap, SOC 2 A1.2 non-compliant |
| CLI output in SSMS | Branded PDF with governance score |
| DBA reads it, fixes it, done | CIO reads it, schedules a budget meeting |
| One-time finding | Ongoing compliance evidence with trend history |
| Technical language | Audit-ready framework language |

The CIO doesn't act on `sys.databases.last_backup > 7 days`. They act on NIST CP-9 and SOC 2 A1.2 non-compliance. SQLTriage makes that translation automatically — every check maps to the frameworks your auditors care about.

---

## Key Capabilities

### Audit & Compliance
- 500+ checks across Security, Backup, Performance, Configuration, Reliability
- Framework mapping: NIST SP 800-53 Rev 5, CIS Controls v8, SOC 2 (2017 TSC), ISO 27001:2022, STIG
- Maturity roadmap: Bronze → Silver → Gold → Platinum progression with actionable gap list
- Governance scoring with weighted categories and trend history
- Compliance Map — visual heatmap of control coverage vs gaps

### Remediation Intelligence
- Per-check effort-hours weighting (tunable via Cost Tuner)
- Estate-wide remediation cost estimate: effort × failed-check count × hourly rate
- Remediation playbooks with prioritised action steps
- CIO Dashboard: licensing cost analysis, TCO, remediation investment summary

### Live Monitoring (while you work)
- Active sessions across all instances — no RDP, no scripts
- Blocking chains, wait stats, top resource consumers
- Adaptive alerting (IQR-based thresholds, low noise)
- XEvent integration for deadlock analysis

### Reporting
- PDF export: governance score, top findings, framework gaps, remediation estimate
- Executive summary suitable for CIO / board presentation
- Scheduled report delivery
- CSV / JSON export for integration with other tools

### Deployment
- Single EXE — no installation, no agent on monitored servers
- Optional Windows service mode for always-on monitoring
- Role-based access: Admin / Operator / Viewer
- Azure Blob export for secure sharing

---

## Real-World Scale

Tested in production environments:

- **200+ SQL Server instances monitored**
- **500+ checks in under 2 seconds**
- ~800MB RAM (service mode, full monitoring enabled)
- No agents deployed to monitored servers

---

## Quick Start (60 seconds)

1. Download `SQLTriage.exe`
2. Run it — no installation required
3. Add your SQL Server instances
4. Run Quick Assessment
5. Export PDF

Full guide → `QUICKSTART.md`

---

## Architecture

- Agentless polling (targeted DMV queries, configurable intervals)
- Local SQLite WAL cache for fast concurrent reads
- Blazor Hybrid WPF (.NET 8) — single-exe desktop, optional Blazor Server mode
- Lightweight batching across large instance estates

---

## When This May Not Be Enough

SQLTriage is not designed for:

- Long-term performance data warehousing
- Cross-platform monitoring (Oracle, MySQL, PostgreSQL)
- Full enterprise observability (use enterprise tools for that)

---

## Built With

.NET 8 · Blazor · WPF · SQLite · Serilog · ApexCharts · Radzen · QuestPDF · Azure SDK

Standing on the shoulders of: sp_Blitz (Brent Ozar), SQLWATCH (Marcin Gminski), sp_triage (sqldba.org), PerformanceMonitor (Erik Darling), MadeiraToolbox (Eitan Blumin), TigerToolbox (Pedro Lopes), Ola Hallengren Maintenance Solution.

---

## Contributing

PRs welcome — see `CONTRIBUTING.md`

---

## License

GPL-3.0 — see `LICENSE.txt`
