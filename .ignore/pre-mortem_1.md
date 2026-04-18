<!-- In the name of God, the Merciful, the Compassionate -->
<!-- Bismillah ar-Rahman ar-Raheem -->

# SQLTriage Pre-Mortem — Why This Becomes a Dud in 1 Year

**Analyst:** Opus pre-mortem simulation  
**Date:** 2026-04-18  
**Status:** Draft — for validation against current planning documents

---

## Executive Summary

You are building a product that nobody asked for. The current plan solves **your** problem (SQL Server monitoring + governance), not the **market's** problem (DBAs need instant answers, not PDFs). The governance narrative is compelling to you — but will it compel a budget holder to click "Download"?

**Core failure mode:** You will spend 8 months building a sophisticated governance engine, and the market will respond with "Nice charts. But does it catch deadlocks?"

---

## Top 10 Reasons This Project Dies

### 10. The "Just One More Feature" Death March (95% probability, Fatal)

**What happens:** Week 3, GovernanceService algorithm debates consume 2 days. You haven't written a single page yet. Week 6: AI/ML accuracy targets not met, you add "explainability" feature. Week 8 becomes Week 12 becomes Week 16. Momentum dies.

**Evidence:** 33 tasks in 8 weeks = 4 tasks/week, zero buffer. No scope-cut criteria. No "Must-have vs Nice-have" split.

**Mitigation not in plan:** Hard scope freeze (v1.0 = Tasks 1–30 only), weekly burn-down, public commitment date.

---

### 9. AI/ML Is a Solution in Search of a Problem (80% probability, High)

**What happens:** Models trained on sp_triage yield CPU MAPE 28% (target: 15%). Week 7 spent fighting scikit-learn instead of polishing UI. "Predictive" tab sits empty in v1.0.

**Evidence:** Success criteria are arbitrary academic thresholds. No feature engineering plan. Assumes sp_triage data is clean (it's not).

**Realistic:** Best case: CPU trend indicator (up/down/flat) with R² ~0.6. That's not actionable.

**Mitigation:** Lower expectations: v1.0 AI/ML = trend direction only. Use exponential smoothing (simpler). Publish training data limitations openly.

---

### 8. ChartTheme Is Beautiful But Unnecessary (70% probability, Medium)

**What happens:** Week 7, deep in ChartTheme.cs tuning emerald opacity. Meanwhile, error messages still say "An error occurred." Gorgeous charts with zero data points = empty museum.

**Evidence:** UI Refresh is Priority 2.5 (medium impact). But ChartTheme has its own 100+ line C# spec. No user validation on "Rolls Royce" preference.

**Mitigation:** Ship with default dark theme first. Apply Rolls Royce tweaks in v1.1 based on user feedback. Spend UI time on functionality, not aesthetics.

---

### 7. Copying Code You Don't Understand (65% probability, High)

**What happens:** Fresh start sounds clean, but you're cherry-picking from a codebase you don't fully own. You copy CredentialProtector that depends on native DLL. You copy SessionDataService and inherit its memory leak. You copy 472 SQL checks, 20% broken on SQL 2022.

**Evidence:** "Copy only proven core code" — but "proven" ≠ "bug-free." No plan to audit copied code before copying. No tests on old code.

**Realistic:** v1.0 ships with 3 critical bugs inherited from old codebase. Post-launch firefighting begins.

**Mitigation:** Audit each copied component before copy. Consider rewriting 5 most complex services. Add integration tests validating copied services in new DI.

---

### 6. The "Governance" Story Falls Flat (60% probability, Fatal)

**What happens:** You demo to 10 DBAs. Feedback: "I don't have to pass audits, my manager does. And my manager doesn't use this tool." Governance tab sits unused. Built a feature nobody wants.

**Evidence:** PRD talks to CxOs and budget holders — but you have **no sales channel** to reach them. "Reduce audit prep from days to minutes" — but audit prep already automated by many tools. Assumes DBAs hand PDF to auditor — auditors want direct system access.

**Mitigation:** Before building GovernanceService, interview 3 compliance officers: "Would this PDF help your audit?" Pivot message to "Governance for the DBA" not "for the enterprise." Track usage: if <20% click Governance tab in first month, it's a flop.

---

### 5. You'll Never Finish 33 Tasks in 8 Weeks (55% probability, High)

**What happens:** Week 4: behind because ChartTheme took longer than expected. Week 5: AI/ML hits — ONNX version conflicts cost 2 days. Week 6: WiX XML cryptic, 3 days gone. Week 7: unit tests reveal design flaws, forced refactor. Week 8: no time for docs. v1.0 ships half-baked.

**Evidence:** 33 tasks / 8 weeks = 4.125 tasks/week, every week, no buffer. No estimates in days. No critical path. No parallelization plan.

**Realistic timeline:** Fresh start (3w) + Governance/RBAC (3w) = Week 6. AI/ML (2w) = Week 8. UI polish (2w) = Week 10. v1.0 slips 2+ months.

**Mitigation:** MoSCoW prioritization. Cut all "Could-have" to v1.1 (reduce to 21 tasks). Add 1-week buffer before release for integration hell.

---

### 4. Database Schema Migration Nightmare (50% probability, High)

**What happens:** You add 6 new tables for RBAC/Audit. User upgrades from 0.85.2. Migration locks SQLite 30 minutes, user kills it, corruption. Support ticket. Bad review.

**Evidence:** Zero migration strategy in PRD or WORKFILE. "Implement role store (SQLite table or JSON)" — two options, no decision. No SchemaVersion table. No upgrade path plan for existing users.

**Mitigation:** Use migrations library. Version all schema changes. Test upgrade from old database before release. Include migration progress bar.

---

### 3. "Fresh Start" Was the Wrong Call (45% probability, Fatal)

**What happens:** By Week 5, you've spent 60% effort rebuilding what already worked. Old SessionDataService had bugs but was battle-tested across 200 DBs. Your fresh SqliteCacheStore loses data on crash. You've traded **known bugs** for **unknown bugs**.

**Evidence:** Assumption: "Copy only proven core" — but "proven" ≠ "bug-free." Assumption: "Discard all legacy debt" — but some debt is **generational knowledge** (why queries written that way). No parallel validation plan.

**Realistic:** Launch Day, you secretly run old code on your own monitoring servers because new version crashes every 4 hours.

**Mitigation:** Before discarding, run component through compatibility matrix. Keep old codebase in separate branch, cherry-pick bug fixes. Have rollback plan: revert to old codebase in 1 day.

---

### 2. No Early Users = Building in a Vacuum (50% probability, Fatal)

**What happens:** You ship v1.0. Post to r/SQLServer. Zero downloads. Why?

- Windows only (alienates Linux DBAs)
- Manual credential entry (not Azure-friendly)
- No cloud integrations
- No API
- Governance PDF is "nice" but not "essential"

You built a beautiful knife in a world moving to guns.

**Evidence:** Target: "budget-conscious enterprises who can't afford SolarWinds" — but these orgs already use **free** tools (sp_Blitz, DBA Dash). No distribution plan — "post to Reddit" is hoping, not marketing. No feedback loop until Beta (Week 7+). No revenue until v1.1.

**Realistic:** 120 GitHub stars, 5 active users, 3 open issues (all bugs), silent death.

**Mitigation:** Ship **Minimum Viable Audience** by Week 4: one page, one chart, one check. Share screenshot with 10 SQL Server users on Discord/Twitter: "Would you use this?" Get 10 committed beta testers before coding Governance Dashboard. Target **consultants** first — they need audit evidence NOW.

---

### 1. Building a Tool for Yourself, Not the Market (60% probability, Fatal)

**What happens:** You're an elite DBA. Your needs: deep SQL knowledge, willing to read docs, care about compliance. Average DBA is overworked, firefighting, wants instant answers, uses sp_Blitz because it's one command.

**Comparison:**

| Tool | Install | Time to Value | Governance |
|------|---------|---------------|------------|
| sp_Blitz | Zero (run script) | 30 sec | None |
| DBA Dash | Service + collector | 10 min | Graphs only |
| **SQLTriage** | EXE + add servers + onboard + explore UI | **30 min** | PDFs + dashboards |

You're 20x more effort for 0.5x more value.

**Evidence:** PRD talks CxO language — but DBAs aren't decision makers. No 10x better identified. What does SQLTriage do that sp_Blitz can't?

**Mitigation:** Find **one killer feature** that's 10x better:
- "One-click HIPAA audit evidence package" (not just SQL, include AD, network)
- Pivot to "SQL Health Copilot" — AI answers natural language questions
- Build **one killer integration**: dbatools.io, Teams alerts, Azure Monitor

---

## Viability Scorecard

| Dimension | Score (1–10) | Rationale |
|-----------|-------------|-----------|
| Technical feasibility | 8/10 | Skills exist, but 33 tasks too many for 8 weeks |
| Market need | 4/10 | Governance is "nice-to-have," not "must-have" for DBAs |
| Differentiation | 3/10 | sp_Blitz is free, fast, trusted. What's your 10x? |
| Executability | 6/10 | Scope too broad; needs ruthless prioritization |
| Sustainability | 2/10 | No revenue until v1.1, no users until launch — zero runway |

**Overall: 4.6/10 — Likely to fail**

---

## How to Actually Win (The 3-Pivot Plan)

### Pivot 1: Narrow to a Niche, Then Expand
Don't build "SQL Server Monitoring & Governance." Build **"HIPAA Audit Evidence Generator for SQL Server."**

- Target: Healthcare DBAs under compliance deadlines
- Messaging: "Export your HIPAA audit package in 5 minutes"
- Features: Only checks that map to HIPAA §164.308(a)(1)
- Price: Free (email signup builds list)
- **Outcome:** Own a niche. Expand to SOC2 next year.

### Pivot 2: Start as a Plugin, Not a Tool
Integrate into **dbatools** (PowerShell module, 150K+ users).

- AI/ML as `Get-DbaPredictiveCapacity` cmdlet
- Governance PDF as `Export-DbaComplianceReport`
- Distribution = their existing install base
- **Outcome:** Instant reach, credibility by association

### Pivot 3: "SQL Health Copilot" (AI-First)
Position as **AI assistant**, not monitoring tool.

- Drop 80% of 472 checks — keep only 50 that AI analyzes
- "Ask SQLTriage: Why is my server slow?" → AI synthesizes answer from metrics
- "Fix this for me" → generates T-SQL script
- **Outcome:** You're not another dashboard, you're ChatGPT for SQL Server

---

## The Brutal Truth

You will not finish v1.0 in 8 weeks with current scope. You will not get 1000 users. You will not displace sp_Blitz.

**But you can succeed if you:**

1. **Narrow to instant value** — Quick Check in <60 seconds from EXE launch (no install, no config)
2. **Target a desperate niche** — Healthcare DBAs facing HIPAA audit next month
3. **Build one 10x feature** — One-click compliant evidence package that no one else offers for free
4. **Ship in 4 weeks** — not 8 — v0.1 with just Quick Check + 1 report
5. **Get 10 users** — before writing Governance Dashboard

The Rolls Royce analogy is wrong. You're not building a Rolls Royce (luxury, slow, bespoke). You're building a **Ferrari**: fast, striking, gets attention immediately.

---

## Recommended Scope Trim for v1.0 (21 tasks → 4 weeks)

**Must-have (M):**
- Tasks 1–5: Brand/version/build fixes
- Task 6: Background refresh + spinner
- Task 13: FAQ + Support link
- Task 22: No manual JSON editing
- Task 25: RBAC implementation (basic only: Admin/ReadOnly/Auditor)
- Task 28: Tamper-proof audit logging (HMAC chain)
- Task 30: Unit test suite (80%)
- Task 33: Release posting (Reddit/HN)

**Should-have (S):**
- Task 8: Dashboard JSON validation
- Task 9: PDF/Excel export
- Task 17: Governance Dashboard (simplified: Risk Level + Maturity % only)
- Task 19: Error messages with governance impact
- Task 20: Onboarding wizard (skip if Task #1 solved)

**Could-have (C) — defer to v1.1:**
- Task 23: AI/ML (trim to linear regression only, no anomaly detection)
- Task 24: UI Theme Refresh (use ApexCharts dark theme, Tailwind later)
- Task 26: AD/LDAP (keep local auth only)
- Task 27: GPO templates
- Task 29: CI/CD polish
- Task 31: Documentation Generator
- Task 32: Code signing (defer to v1.1)

**New Task (0): Quick Check Launch Experience**
- On EXE launch with no servers: show "Add Server" dialog
- Auto-detect local SQL instances via `SqlDataSourceEnumerator`
- Pre-populate server list
- Checkbox: "Run Quick Check immediately after connect" (default: checked)
- After servers added, navigate directly to Quick Check results page
- Quick Check runs all 240+ checks in parallel (existing sp_Blitz-style queries)
- Results populate within 60 seconds with color-coded PASS/WARN/FAIL
- Toast notifications for critical findings
- **This is v1.0's hook.**

---

## Validation Request to Opus

When you read this pre-mortem, please:

1. Identify any **incorrect assumptions** in the risk analysis
2. Suggest **additional failure modes** not listed
3. Propose a **minimum viable v1.0 scope** that can ship in 4 weeks with high confidence
4. Validate or reject the "Quick Check Launch" as the primary user acquisition hook
5. Recommend **which 3 features** to build first to get 10 real users

---

*End of pre-mortem_1.md*

