# SQLTriage — Free SQL Server Monitoring (No Agents, Single EXE)

**Real-time dashboards • Interactive execution plans • 69 alerts • Runs as service**  
Lightweight Windows desktop + headless service alternative to SolarWinds DPA, Redgate SQL Monitor, SentryOne, and Idera SQL Diagnostic Manager.

**Latest**: v0.85.2 (17 Apr 2026) • [Download →](https://github.com/SQLAdrian/SQLTriage/releases)

**Demo**: [SQL Server DBA Monitoring Demo](docs/sql-server-dba-monitoring-demo.gif)

## Screenshots (16 Real Ones)

| Add Server | What's New | Wait State | Live Sessions | Database Health | Instance Overview | Environment Map | Monitor 50+ Instances | Run Full SQL Audits | Upload to Azure Blob | Multiple Notification Channels | Maturity Roadmap | Alerting | Query Plan Viewer | Risk Register & Compliance |
|------------|------------|------------|---------------|-----------------|-------------------|-----------------|-----------------------|----------------------|----------------------|------------------------------|-------------------|----------|-------------------|------------------------------|
| ![Add Server](docs/screenshots/1-addserver.jpg) | ![What's New](docs/screenshots/2-whats-new.jpg) | ![Wait State](docs/screenshots/3-wait-state.jpg) | ![Live Sessions](docs/screenshots/4-live-sessions.jpg) | ![Database Health](docs/screenshots/5-database-health.jpg) | ![Instance Overview](docs/screenshots/6-instance-overview.jpg) | ![Environment Map](docs/screenshots/7-environment-map.jpg) | ![Monitor 50+ Instances](docs/screenshots/8-servers-easily-monitor-50-instances-or-more.jpg) | ![Run Full SQL Audits](docs/screenshots/9-run-full-sql-audits.jpg) | ![Upload to Azure Blob](docs/screenshots/10-microsoft-sql-vulnerability-assessment.jpg) | ![Multiple Notification Channels](docs/screenshots/11-upload-results-securely-to-azure-blob-storage.jpg) | ![Maturity Roadmap](docs/screenshots/12-multiple-notification-channels.jpg) | ![Alerting](docs/screenshots/13-maturity-roadmap.jpg) | ![Query Plan Viewer](docs/screenshots/14-alerting.jpg) | ![Risk Register & Compliance](docs/screenshots/15-query-plan-viewer.jpg) |
| Basic setup | Release notes | Performance bottlenecks | Active sessions | DB status | Server overview | Multi-server view | Scalability | Comprehensive checks | Secure exports | Email, WhatsApp, etc. | Health scoring | 69 custom alerts | Interactive plans | CIS/NIST mapping |

## Why DBAs Use It

- **Agentless**: No software on SQL Servers
- **Single EXE**: Portable, no installation
- **Real-Time**: Live monitoring with background refresh
- **Interactive Plans**: V2 viewer with hover details
- **Comprehensive**: 489 VA rules, 69 alerts, service mode
- **Free & Open**: GPL-3.0, community-driven

## Comparison

| Feature | SQLTriage | sp_Blitz | SQLWATCH | dbwatch | SolarWinds DPA |
|---------|-----------|----------|----------|---------|----------------|
| Cost | Free | Free | Free | $$$ | $$$ |
| Agent Required | No | No | Yes | No | Yes |
| Interactive Plan Viewer | Yes (V2) | No | No | Yes | Yes |
| Runs as Windows Service | Yes | No | Partial | Yes | Yes |
| Multi-Platform | SQL Server only | SQL only | SQL only | 8 platforms | SQL only |
| Historical Trends | Basic | No | Yes | Yes | Yes |
| Compliance Mapping | Basic VA | No | No | Yes | Yes |
| Automated Maintenance | No | No | No | Yes | Yes |
| Threshold Alerts | IQR-based | No | Basic | Yes | Yes |
| MSP/Multi-Tenant | No | No | No | Yes | Yes |

## Quick Start

**Requires:** Windows 10 1809+ · SQL Server 2016+

1. Download SQLTriage.exe from [Releases](https://github.com/SQLAdrian/SQLTriage/releases)
2. Run it (no install needed)
3. Add Server: Servers → Add Server → Test → Save
4. Live Monitor: Ctrl+2 for sessions, queries, stats
5. Quick Check: Ctrl+Q for health snapshot

**[Full Guide](QUICKSTART.md)**

## Features

- **Real-Time Monitoring**: Live sessions, wait stats, top queries
- **Interactive Plans**: V2 viewer with copy, hover, fade
- **Alerting**: 69 alerts, 7 channels, maintenance windows
- **Vulnerability Assessment**: 489 rules, CIS mapping
- **Service Mode**: Headless 24/7 with remote access
- **Azure Integration**: Secure blob uploads
- **RBAC**: Admin/Operator/Viewer roles
- **Export**: PDF, CSV, JSON

## Architecture

Polling + SQLite WAL cache (6 tables). Self-monitoring memory service. Blazor WebView2 in WPF. No agents.

## Built With

.NET 8, Blazor, SQLite, Serilog, ApexCharts, Radzen, Azure SDK, ConfuserEx2. Powered by sp_Blitz, SQLWATCH, Erik Darling scripts.

## Community & AI

Built with Claude, Kilo, Gemini—see [CONTRIBUTORS.md](CONTRIBUTORS.md).

## Contributing

PRs welcome. See [CONTRIBUTING.md](CONTRIBUTING.md).

## License

GPL-3.0 - see [LICENSE.txt](LICENSE.txt)</content>
<parameter name="filePath">C:\GitHub\LiveMonitor\README_draft.md