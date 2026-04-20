<!-- In the name of God, the Merciful, the Compassionate -->

# Context Optimization — Handoff Spec

**Goal:** Cut token burn and compact frequency via (a) strict `CLAUDE.md` rules, (b) grep-friendly index files under `.claude/docs/`, and (c) a clean split between **human-readable** docs and **machine-fodder** indexes.

**Scope:** Three layers. Layer 0 is the tiny-model recipe (a 3B model can run it). Layer A is generic discipline (portable to any project / any LLM extension). Layer B is SQLTriage-specific. Commit only when Adrian asks.

---

## Layer 0 — Dead-simple recipe (for small / local models)

**Read this whole section first. Do the steps in order. Do not skip. Do not think ahead.**

### What we are doing

You will make 7 small text files. Each file is a list. Each list tells a bigger LLM where things are, so it does not have to read the whole project every time.

You will also add some rules to one file (`CLAUDE.md`) and some patterns to another file (`.claudeignore`).

When you finish, **stop**. Do not commit. Do not push. The human will check your work.

### Rules you must follow every step

1. **One file at a time.** Finish one, then start the next.
2. **If a file already exists, read it first.** Then edit it. Do not overwrite without reading.
3. **Every new file starts with this exact line:**
   ```
   <!-- In the name of God, the Merciful, the Compassionate -->
   ```
4. **Every new file under `.claude/docs/` has this block right after the first line:**
   ```
   ---
   name: <short title>
   audience: machine
   purpose: <one short sentence>
   generated: 2026-04-19
   source_of_truth: <file or folder you read to make this>
   items: <how many rows you wrote>
   ---
   ```
   If the file is for humans to read, write `audience: human` instead.
5. **Do not read a file bigger than 300 lines all at once.** Use `grep` to find the part you need. Then read only those lines.
6. **Never guess.** If you do not know a value, write `[STUB]` in that cell. Do not make something up.
7. **Keep each file short.** Machine-list files: 200 lines or less. Human-read files: 300 lines or less.
8. **When you finish all 9 steps below, stop and report what you did.** One short paragraph. No long summary.

### The 9 steps, in order

**Step 1 — Check the project.**
Run these commands and save the output. You will use it later.
```
ls Data/Services/
ls Pages/
ls Components/Shared/
ls .claude/docs/
cat Config/sql-checks.json | head -5
```

**Step 2 — Make `.claude/docs/README.md`.**
A small table. 15 to 30 lines. Columns: `File | Audience | Purpose | Source | Items`.
One row per file you will create in the next steps. Fill in `items` later if you do not know yet.

**Step 3 — Make `.claude/docs/services-index.md`.**
- Audience: `machine`.
- Source: `Data/Services/*.cs`.
- For each `.cs` file, write one row: `| <filename> | <interface name> | <one-sentence purpose> | <3 to 5 method names> |`.
- Get the purpose from the `/// <summary>` block near the top of the file. If missing, write `[STUB]`.
- Group rows under these headers: **Auth & RBAC**, **Alerting**, **Assessment & Checks**, **Scheduling**, **Infrastructure**, **UI support**, **Other**.
- Target: 80 to 140 lines.

**Step 4 — Make `.claude/docs/pages-index.md`.**
- Audience: `machine`.
- Source: `Pages/*.razor`.
- For each `.razor` file, write one row: `| <route> | <filename> | <one-sentence purpose> | <up to 3 services injected> |`.
- Get the route from `@page "..."`. Get services from `@inject` lines.
- Target: 80 to 120 lines.

**Step 5 — Make `.claude/docs/components-index.md`.**
- Audience: `machine`.
- Source: `Components/Shared/*.razor`.
- For each file, write one row: `| <component name> | <one-sentence purpose> | <up to 3 [Parameter] names> | <up to 2 pages that use it> |`.
- Find callers with: `grep -rn "<ComponentName" Pages/ | head -3`.
- Target: 80 to 120 lines.

**Step 6 — Make `.claude/docs/checks-catalog.md`.**
- Audience: `machine`.
- Source: `Config/sql-checks.json`.
- For each check, write one row: `| <check_id> | <category> | <severity> | <quick?> | <status> | <timeoutSec> |`.
- If a field is missing in the JSON, write `[STUB]` in that cell.
- Target: however many checks there are, plus 10 lines of header.

**Step 7 — Make `.claude/docs/css-design-system.md`.**
- Audience: `machine`.
- Source: the top part of `wwwroot/css/app.css` only.
- **Do not read the whole `app.css` file. It is about 8700 lines. You will run out of memory.**
- Use `grep -n "^:root\|^\[data-theme" wwwroot/css/app.css` to find the theme block lines. Read only those lines.
- Table 1 — CSS variables: `| --var-name | purpose | default | rolls-royce | amg |`.
- Table 2 — Classes used in more than one place: `| class | purpose | file:line |`. Use `grep -n "^\.[a-z]" wwwroot/css/app.css | head -50` to find them.
- Target: 120 to 180 lines.

**Step 8 — Make `.claude/docs/grep-cheatsheet.md`.**
- Audience: `machine`.
- A small file of useful grep commands. 30 to 50 lines.
- Example rows:
  ```
  # Find a route
  grep -rn "@page \"/capacity\"" Pages/
  # Find who uses a component
  grep -rn "<StatCard" Pages/ Components/
  # Find where a CSS variable is used
  grep -rn "var(--bg-primary)" wwwroot/css/ Components/ Pages/
  ```

**Step 9 — Update `CLAUDE.md` and `.claudeignore`.**
- Open `CLAUDE.md`. Find the section called "Search first". Paste the block from Layer A5 below **right after** that section.
- Open `.claudeignore`. Paste the block from section B9 below **at the bottom**. Do not remove anything.

### Stop rules (very important)

- Stop at the end of Step 9. Do not commit. Do not run `git push`.
- If any step fails, stop and write what went wrong. Do not keep going.
- Do not rewrite files that are not on this list.
- Do not change any `.cs` or `.razor` code files. You are only making lists.

---

## Layer A — Generic convention (portable)

### A1. Frontmatter audience tag

Every file under `.claude/docs/` declares its audience:

```markdown
---
name: {{short title}}
audience: machine | human | both
purpose: {{one line — what a reader gets from this file}}
generated: 2026-04-19              # date last regenerated (YYYY-MM-DD)
source_of_truth: {{file/glob or "manual"}}
items: 42                          # count of rows/entries (optional)
---
```

- `audience: machine` → grep targets, lookup tables, catalogues. Humans don't read these end-to-end. Columns over prose.
- `audience: human` → conventions, patterns, narrative context. Written in plain English.
- `audience: both` → rare; typically top-level README-style indexes.

### A2. File-naming convention

```
.claude/docs/
  README.md                         — index of indexes (audience: both)
  conventions.md                    — human (coding style, commit rules, naming)
  patterns.md                       — human (architectural patterns, idioms)
  services-index.md                 — machine (service catalogue)
  pages-index.md                    — machine (route catalogue)
  components-index.md               — machine (component catalogue)
  <domain>-catalog.md               — machine (data catalogues — checks, configs, schemas)
  css-design-system.md              — machine (design tokens, class lookup)
  grep-cheatsheet.md                — machine (canonical grep commands)
```

Rule of thumb: if a human needs to read it linearly, it's a doc. If an LLM greps or table-looks-up, it's an index.

### A3. Machine-index format discipline

- One line per item. No prose paragraphs.
- Columns separated by `|` (tables) or `—` (lists).
- Each file ≤200 lines total.
- Sort alphabetically unless noted.
- Each line self-contained — no "see above" references.
- Use `[STUB]` or `???` markers for unimplemented / unknown cells. Never leave blank without marker.
- First content row = stats line: `<!-- generated: YYYY-MM-DD | items: N | source: path/glob -->`.

### A4. Human-doc format discipline

- Plain English. Complete sentences.
- Keep ≤300 lines. Split by topic if longer.
- Example-driven where possible (`// Good:` / `// Bad:` pairs).
- No tables unless they clarify a decision matrix.

### A5. CLAUDE.md rule block (verbatim, portable — paste into any project)

```markdown
## Context discipline (read before every session)

### Index-first policy
Before reading source files, check if an index covers your task:

| Before you touch… | Read this index first |
|---|---|
| Styling, themes, CSS tokens                     | `.claude/docs/css-design-system.md` |
| Any file under `<services-dir>/`                | `.claude/docs/services-index.md` |
| Any file under `<pages-dir>/`                   | `.claude/docs/pages-index.md` |
| Any file under `<components-dir>/`              | `.claude/docs/components-index.md` |
| Data catalogues / config registries             | `.claude/docs/<domain>-catalog.md` |
| Coding style, commit rules, naming              | `.claude/docs/conventions.md` |
| Architectural patterns, idioms                  | `.claude/docs/patterns.md` |

**Hard rule:** If the task can be answered from an index, **do not read the source files**. Grep the index, then read only the single file you need to edit.

### File-read discipline
- Never `Read` a file >200 lines without `offset`+`limit`. Target the section.
- Never re-read a file already present in this session's tool results — grep the existing output instead.
- Always pass `head_limit` on `Grep`. Default 20. Bump deliberately.
- Prefer `output_mode: "count"` or `"files_with_matches"` before `"content"`.

### Post-compact behaviour
After a context compact, do NOT restate project state. Authoritative sources:
- `.claude/docs/README.md` (index of indexes)
- Any `.ignore/*_Context_*.md` file (project snapshot, if present)
- `memory/*.md` (locked decisions)

If the summary would re-derive these, say "state per indexes + memory" instead.

### Chat output cap
- Default response ≤40 words. Explain only when asked.
- No preambles ("Let me…", "I'll check…"). State what you did; show the diff.
- No trailing summaries of what you just changed — the diff is visible.

### Subagent policy
- For "find every reference to X across N files" or "audit Y across the repo": spawn an Explore subagent with a short self-contained brief. Its summary (~500 tok) replaces ~5k of raw grep in main context.
- Only spawn when explicitly asked or when the search will return >50 hits.
```

### A6. Regeneration script convention

Each machine-index names its `source_of_truth` in frontmatter. A manual regen script (`.claude/scripts/regen-indexes.sh` or `.ps1`) should:
- Read frontmatter `source_of_truth` glob.
- Rebuild the table rows from source.
- Preserve the header/stats line with today's date.
- Run manually — **never as a pre-commit hook**. Generated-file noise in commits is worse than stale indexes; indexes drift gracefully.

### A7. `.claudeignore` convention

Exclude from auto-context:
- Large planning docs (PRDs, strategies, post-mortems) — read on demand only.
- Git submodules and vendored code.
- Generated artifacts, test fixtures, large JSON/SQL dumps.
- Anything the human owns and the LLM shouldn't guess-edit (e.g. `*.sql` if the human writes all SQL).

---

## Layer B — SQLTriage-specific deliverables

All files start with `<!-- In the name of God, the Merciful, the Compassionate -->`.

### B0. Prep: confirm `source_of_truth` inputs

Before generating, verify these paths still exist and list item counts:
- `Data/Services/*.cs`
- `Pages/*.razor`
- `Components/Shared/*.razor`
- `Config/sql-checks.json`
- `wwwroot/css/app.css` (for `:root` and `[data-theme]` theme blocks — file is now ~356 lines; read only the top section)

Record counts in each generated file's frontmatter `items:` field.

### B1. `.claude/docs/css-design-system.md` (OVERWRITE existing 24-line stub — audience: machine)

Sections in this order:

1. **CSS variables** — table: `--var-name | purpose | default value | rolls-royce | amg`. Source: the `:root` and `[data-theme]` blocks at the top of `app.css`.
2. **Theme system rules** — 5 bullets max: where `data-theme` is set, how `themes.js` applies it, why no inline `style.setProperty`, how new themes are added, chart palette hook point.
3. **Class catalogue** — table: `class | purpose | file location (line)`. Only list classes used in >1 file or defined with non-trivial rules. Group by: Layout, Nav, Toolbar, Panel/Card, Chart, Form, Modal, Print. Use `grep -n "^\.[a-z]" wwwroot/css/app.css` to harvest.
4. **Hardcoded holdouts** — list any `#xxxxxx`, `px` radius/shadow values still in `app.css` that should move to vars. Mark with `[STUB]` → `TODO: move to var`.
5. **Do NOT** list — rules: no Tailwind, no inline styles for colour/radius/transition, no `!important` except print overrides, no hardcoded hex outside `:root` blocks.

Target length: 120–180 lines.

### B2. `.claude/docs/services-index.md` (NEW — audience: machine)

Table with columns: `File | Interface | Purpose (1 line) | Key public methods`

Source: `Data/Services/*.cs`. Extract purpose from class-level `/// <summary>` block. For key methods, list 3–5 public method names max — no signatures, just names.

Example row:
```
| ChartThemeService.cs | IChartThemeService | Theme palette + OnThemeChanged event bus for chart components | GetPalette, ApplyTheme, OnThemeChanged |
```

Group by category (headers): **Auth & RBAC**, **Alerting**, **Assessment & Checks**, **Scheduling**, **Infrastructure (cache/export/PowerShell)**, **UI support (theme/settings/print)**, **Other**.

Missing summaries → mark purpose cell as `[STUB] — read file`. Do not invent purposes.

Target: 80–140 lines.

### B3. `.claude/docs/pages-index.md` (NEW — audience: machine)

Table: `Route | File | Purpose | Key services used`

Source: `Pages/*.razor`. Purpose: one line from the first `<h1>`/`<h3>` tag or page header. Services: `@inject` directives at top of file (grep `@inject` per file, max 3 shown).

Example:
```
| /capacity    | CapacityPlanning.razor | Disk + CPU forecasting via linear regression | ForecastService, ConnectionHealthService |
| /quickcheck  | QuickCheck.razor       | Quick Check runner (≤60s subset)              | ICheckRunner, IFindingTranslator |
```

Group by: **Core dashboards** (/, /dashboard/*, /instance, /environment), **Checks & Audit** (/quickcheck, /fullaudit, /checks, /bestpractice, /vulnerabilityassessment, /diagnostics-roadmap), **Monitoring** (/blocking, /sessions, /longqueries, /waitevents, /xevents, /pevents, /pmemory*), **Admin** (/settings, /login, /servers, /scheduled-tasks, /service-management, /alerting-config, /deploysqlwatch, /deploydarlingPM, /editauditscripts, /bulkeditchecks), **Tools** (/query, /plan-analysis, /dbatools, /check-validator), **Info** (/about, /guide, /health).

Target: 80–120 lines.

### B4. `.claude/docs/components-index.md` (NEW — audience: machine)

Table: `Component | Purpose | Key [Parameter]s | Used by (sample pages)`

Source: `Components/Shared/*.razor`. Extract `[Parameter]` attribute declarations (3 max per component). "Used by" — grep `<ComponentName` across `Pages/` and pick up to 2 representative usages.

Example:
```
| StatCard.razor       | KPI card with delta + trend arrow                 | Title, Value, Delta, TrendDirection | Dashboard, Health, InstanceOverview |
| TimeSeriesChart.razor| ApexCharts line chart with baseline overlay       | SeriesData, BaselineData, YAxisLabel | Health, CapacityPlanning |
```

Group: **Cards & KPIs**, **Charts**, **Tables & Grids**, **Modals & Dialogs**, **Layout (wrappers/toolbars)**, **Guards (Rbac/Admin)**, **Live monitoring (session/blocking/deadlock)**, **Utility (loader/toast/lazy)**.

Target: 80–120 lines.

### B5. `.claude/docs/checks-catalog.md` (NEW — audience: machine — generated from `Config/sql-checks.json`)

Table: `check_id | category | severity | quick? | status | timeoutSec`

Source: `Config/sql-checks.json`. 

Add a one-line intro: "Authoritative runtime check list. Regenerate after edits to `Config/sql-checks.json`."

Missing fields (`status`, `severity`, `quick` may not be populated on every check) → leave as `[STUB]`.

Target: ~current check count + 10 lines of header.

### B6. `.claude/docs/grep-cheatsheet.md` (NEW — audience: machine)

Canonical grep commands for common lookups. Keep ≤50 lines. Example rows:

```
# Find route → file
grep -rn "@page \"/capacity\"" Pages/

# Find service interface implementations
grep -rn "class .*ChartThemeService.*:" Data/Services/

# Find CSS variable usage
grep -rn "var(--bg-primary)" wwwroot/css/ Components/ Pages/

# Find component callers
grep -rn "<StatCard" Pages/ Components/
```

### B7. `.claude/docs/README.md` (NEW — audience: both)

Index of indexes. One table. Columns: `File | Audience | Purpose | Source of truth | Items`. 15–30 lines max. This is the single entry point — Claude reads this first when it sees a task that smells like it has an index.

### B8. Update `CLAUDE.md`

Paste the Layer A5 block verbatim **after** the existing "Search first" section. Replace the `<services-dir>` / `<pages-dir>` / `<components-dir>` placeholders with SQLTriage paths:
- `<services-dir>` → `Data/Services/`
- `<pages-dir>` → `Pages/`
- `<components-dir>` → `Components/Shared/`
- `<domain>-catalog.md` → `checks-catalog.md`

Add one extra row to the table:
```
| Project state, scope, decisions, weeks remaining | `.ignore/SQLTriage_Context_Caveman.md` |
```

### B9. Patch `.claudeignore`

Append these entries (do not remove existing):

```
# Planning docs — read on demand only (not via auto-load)
.ignore/SQLTriage_PRD.md
.ignore/SQLTriage_Release_Checklist.md
.ignore/SQLTriage_Strategic_Blueprint.md
.ignore/DEVELOPMENT_STRATEGY.md
.ignore/WORKFILE_remaining.md
.ignore/OPUS_ANALYSIS_COMPLETE_*.md
.ignore/OPUS_MEGA_PROMPT_COMPLETE.md
.ignore/PRE-MORTEM_*.md
.ignore/DECISIONS/
.ignore/review/

# Submodules — large, read only when plan viewer work is active
lib/PerformanceStudio/

# SQL (Adrian owns SQL — do not auto-search)
*.sql
*.sqlplan
*.dacpac.xml

# Generated/cached
Data/Sql/HealthChecks/
BPScripts/Ignore/
Deploy/
!Deploy/installer/

# Test fixtures
Tests/**/TestData/
Tests/**/Fixtures/
```

**Do NOT ignore:** `.ignore/SQLTriage_Context_Caveman.md` — it's small and load-bearing.

---

## Acceptance criteria

- [ ] All 7 files exist under `.claude/docs/` (README, css-design-system, services-index, pages-index, components-index, checks-catalog, grep-cheatsheet); `patterns.md` and `conventions.md` unchanged unless existing stubs
- [ ] Every file in `.claude/docs/` has frontmatter with `name`, `audience`, `purpose`, `generated`, `source_of_truth`
- [ ] Each machine-index file ≤200 lines; human docs ≤300 lines
- [ ] `CLAUDE.md` contains the Layer A5 "Context discipline" block with SQLTriage paths substituted
- [ ] `.claudeignore` has the new blocks appended (no removals)
- [ ] `git status` shows only these files changed:
  - `.claude/docs/README.md` (new)
  - `.claude/docs/css-design-system.md` (modified)
  - `.claude/docs/services-index.md` (new)
  - `.claude/docs/pages-index.md` (new)
  - `.claude/docs/components-index.md` (new)
  - `.claude/docs/checks-catalog.md` (new)
  - `.claude/docs/grep-cheatsheet.md` (new)
  - `CLAUDE.md` (modified)
  - `.claudeignore` (modified)

**Do not commit** unless Adrian explicitly says "commit this".

---

## Notes for the executor (Sonnet or any LLM)

- **Target the top theme blocks in app.css.** It is now ~356 lines with `:root` and `[data-theme]` blocks at the top. Read only those; no need for grep-based line discovery.
- **Don't re-read files already present in this session's tool results.** Grep the existing tool-result buffer.
- Class summaries for services: extract from first `/// <summary>` block per file. If absent, read first 40 lines and write a 1-liner based on the class body. If still unclear, write `[STUB]`.
- Page purposes: check the `<h1>`/`<h3>` first; fallback to `@code` comments; last resort, describe from the `@inject` list.
- `sql-checks.json` may not have all new fields (`status`, `severity`, `quick`) on every check — use whatever's present; leave `[STUB]` where absent and note at top of `checks-catalog.md`.
- Keep grep output tight: use `-n`, `head_limit`, and `files_with_matches` aggressively. You will be evaluated on token efficiency of your own session.

**Expected total new lines across all deliverables:** ~600–900 lines.
**Expected ongoing token saving:** 30–50% on styling/page/service-touching turns; 3–5k per compact.

---

## Portability note (separate-repo candidate)

Layer A (A1–A7) is fully generic. If extracted to a standalone repo (suggested name: **`claude-context-kit`** or **`llm-context-discipline`**), it would ship:

- `README.md` — the problem (compact-burn, repeated file reads, drift after compacts) and the solution (index-first discipline).
- `templates/CLAUDE.md.template` — the Layer A5 block with `<placeholder>` slots.
- `templates/.claudeignore.template` — Layer A7 starter.
- `templates/.claude/docs/README.md.template` — index-of-indexes skeleton.
- `templates/.claude/docs/*-index.md.template` — one template per machine-index archetype.
- `scripts/regen-indexes.{sh,ps1}` — skeleton regen script that reads `source_of_truth` from frontmatter.
- `docs/audience-split.md` — human-vs-machine rationale.
- `docs/why-not-pre-commit.md` — why regen is manual, not hooked.

Works with: Claude Code, Continue (VSCode/JetBrains), Cursor, Cline, local Ollama front-ends, any tool that reads project-local `.md` files or a `CLAUDE.md`-equivalent.

Licence suggestion: MIT or CC0 — maximise adoption.
