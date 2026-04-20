<!-- In the name of God, the Merciful, the Compassionate -->
<!-- Bismillah ar-Rahman ar-Raheem -->

# SQLTriage

Blazor Hybrid WPF (.NET 8), single-exe Windows desktop. Falls back to Blazor Server if WebView2 missing.

## Search first
- Grep, don't read. `app.css` is 7500 lines; `.ignore/*.md` are huge.
- Scope to `Pages/`, `Data/Services/`, `Components/` (95% of changes).
- `.claudeignore` excludes `bin/`, `obj/`, worktrees, PDFs, SQL scripts, root-level docs.
- Don't re-read files already read this session.
- Styling/new-page work: read `.claude/docs/css-design-system.md` and `.claude/docs/patterns.md` first.

## Context discipline (read before every session)

### Index-first policy
Before reading source files, check if an index covers your task:

| Before you touch… | Read this index first |
|---|---|
| Styling, themes, CSS tokens | `.claude/docs/css-design-system.md` |
| Any file under `Data/Services/` | `.claude/docs/services-index.md` |
| Any file under `Pages/` | `.claude/docs/pages-index.md` |
| Any file under `Components/Shared/` | `.claude/docs/components-index.md` |
| Data catalogues / config registries | `.claude/docs/checks-catalog.md` |
| Coding style, commit rules, naming | `.claude/docs/conventions.md` |
| Architectural patterns, idioms | `.claude/docs/patterns.md` |
| Project state, scope, decisions, weeks remaining | `.ignore/SQLTriage_Context_Caveman.md` |

**Hard rule:** If the task can be answered from an index, **do not read the source files**. Grep the index, then read only the single file you need to edit.

### Anchor-driven lookup (staleness-proof)
Every index row includes an `Anchor` column. This is NOT a source-file marker — it's a **search hint**. To find current code:
1. Use the Anchor value to construct a grep pattern: `grep -n "EvaluateAllAsync|OnInitializedAsync|<purpose keyword>" <file>`
2. `Read` the file starting at the returned line (± ~20 lines for context)
3. If grep returns nothing (marker pattern stale), broaden search: `grep -rn "<ClassName>|<method stub>" <file>` or semantic intent keywords from the index row

This guarantees the LLM interrogates live code, not stale index line numbers, even if index summaries are out of date.

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

## For AI Assistants: Context Navigation (llmck)

This repo includes **llmck** — an index-first context engineering system that helps LLMs navigate large codebases efficiently.

**Start here when using AI:**
1. Read `.claude/docs/README.md` — navigation guide
2. Read `.claude/docs/services-index.md` — all 34 services mapped
3. Then grep/read specific files (code-files-only: `.cs`, `.xaml`, `.razor`, `.md` only)

**Never read:** `.json`, `.sql`, `.xml`, `.csv`, `.config`, `.resx` (infrastructure files, not logic)

**Generate/validate indexes locally:**
```bash
pip install -e https://github.com/afsultan/llm-context-kit.git
python -m llmck sync       # regenerate indexes
python -m llmck validate   # check for drift
```

See: https://github.com/afsultan/llm-context-kit

## Layout
- `MainWindow.xaml.cs` — WPF shell, BlazorWebView, zoom, DevTools
- `App.xaml.cs` — DI, Serilog, startup
- `Pages/*.razor` (37) — `@page` routes
- `Components/{Shared,Layout}/*.razor` — DynamicPanel, StatCard, DataGrid, DeadlockViewer, NavMenu
- `Data/Services/*.cs` (20) — Blob, Assessment, RBAC, Forecast, Alert, Notification
- `Data/Models/*.cs` — POCOs · `Data/Caching/*.cs` — SQLite WAL, 2-wk retention, delta-fetch
- `Config/` — appsettings, version, dashboard-config

## Conventions
- Basmalah header (non-negotiable): `.cs` → `/* In the name of God, the Merciful, the Compassionate */`; `.razor` → `<!--/* … */-->`
- Credentials: `CredentialProtector.Encrypt/Decrypt` (AES-256-GCM + DPAPI)
- Connections: explicit DB name (`"master"` for non-SQLWATCH); `HasSqlWatch` defaults `false`
- DI: nullable optional params (`Service? svc = null`)
- Background: `_ = Task.Run(async () => { … })`
- Commits: `feat:`/`fix:`/`docs:`; co-author `Co-Authored-By: Claude Opus 4.6 <noreply@anthropic.com>`; branch `main` → PR `master`

## Build
```
dotnet build SQLTriage.sln
dotnet publish -c Release -r win-x64
./increment-build.ps1   # bumps Config/version.json
```
Close the running exe first (file lock).

## Key subsystems (one-liners)
- Baseline overlay: DashboardToolbar toggle → 7-day-old cache → dashed overlay on TimeSeriesChart
- Deadlock viewer: `panelType="Deadlock"` parses `system_health` XEvent XML
- Forecasting: `ForecastService` linear regression → `/capacity` (disk + CPU)
- Maturity roadmap: `/diagnostics-roadmap` maps 489 sql-checks → 5 levels via QuickCheck
- Debug logging: UserSettingsService toggle flips `LoggingLevelSwitch` at runtime

## Model / thinking level
Flag if current model seems mismatched — one line only, no interruption:
- **Sonnet default** — routine tasks: file edits, rename, grep, boilerplate, test writing, CI fixes.
- **Sonnet + extended thinking** — ICheckRunner budget logic, AuditLog HMAC chain, concurrency, security primitives.
- **Opus** — gate reviews (Wk 2/4/6/8), architectural decisions that conflict with `.ignore/OPUS_ANALYSIS_COMPLETE_*` or `memory/project_sqltriage_v1_lockin.md`, new scope proposals.

**Trigger:** if a task touches >3 interdependent services, changes a locked decision (see lock-in memory), or involves security-critical code — note "this may warrant Opus" once and continue.

## Don't
- Use Tailwind (CSS variable design system is authoritative)
- Bulk-restyle RDL (expression-bound styles make it futile)
- Call `CreateIfNotExistsAsync` on Azure Blob (fails with directory-scoped SAS)
- Assume WebView2 is available (handle server-mode fallback)
- Hardcode SQLWATCH (some servers don't have it)
- Use `<` in Razor `@code` switch (Razor reads it as HTML; use `if/else`)
- Write or edit SQL (user owns SQL; focus on C#/Blazor)
- Commit after every change (commit only when explicitly asked)

## Output shorthand (narration only — NOT final answers)

You MAY compress these using the same vocab:
- Progress updates between tool calls ("reading USS for nxt on wl...")
- Status narration ("checking QP")
- Thinking/reasoning you emit before a conclusion

You MUST write in full English for:
- The final prose answer to the user (the last block before you stop)
- Explanations the user must act on (instructions, warnings, confirmations)
- Any output a user will copy or share

Rule of thumb: if you're narrating your own work → shorthand. If you're answering the user → English.

## Hard exclusions — NEVER use shorthand in:

- Edit / Write tool `content` or `new_string` parameters (file contents)
- Bash tool `command` parameter (shell execution)
- Git commit messages
- Grep / Glob pattern parameters
- Any tool input that flows to disk, shell, or a remote system
- Anything inside backticks anywhere
- File paths, identifiers, SQL, JSON, error messages

A `validate-no-shorthand` PreToolUse hook enforces this. If you write shorthand into a tool call, the call is blocked and you must expand and retry in English. Don't fight the validator — it's there to prevent breaking your code.

Rule: shorthand lives in the conversation stream. It must never enter a tool call's arguments.

### Memory location discovery

Hooks resolve memory file paths in this order:
1. **`$env:CLAUDE_COMPRESSION_MEMORY`** — set this environment variable to point to any directory (e.g., `%APPDATA%\Claude\memory\LiveMonitor`). Use when you want memory outside the project tree.
2. **`<project_root>/memory/`** — default project-relative location.

This means any LLM can discover the memory location by reading `CLAUDE.md` (this section) and, if needed, checking the environment. The env var makes the system portable across folder structures without editing hook scripts.


## Output shorthand (narration only — NOT final answers)

You MAY compress these using the same vocab:
- Progress updates between tool calls ("reading USS for nxt on wl...")
- Status narration ("checking QP")
- Thinking/reasoning you emit before a conclusion

You MUST write in full English for:
- The final prose answer to the user (the last block before you stop)
- Explanations the user must act on (instructions, warnings, confirmations)
- Any output a user will copy or share

Rule of thumb: if you're narrating your own work → shorthand. If you're answering the user → English.

## Hard exclusions — NEVER use shorthand in:

- Edit / Write tool `content` or `new_string` parameters (file contents)
- Bash tool `command` parameter (shell execution)
- Git commit messages
- Grep / Glob pattern parameters
- Any tool input that flows to disk, shell, or a remote system
- Anything inside backticks anywhere
- File paths, identifiers, SQL, JSON, error messages

A `validate-no-shorthand` PreToolUse hook enforces this. If you write shorthand into a tool call, the call is blocked and you must expand and retry in English. Don't fight the validator — it's there to prevent breaking the user's code.

Rule: shorthand lives in the conversation stream. It must never enter a tool call's arguments.

### Memory location discovery

Hooks resolve memory file paths in this order:
1. **`$env:CLAUDE_COMPRESSION_MEMORY`** — set this environment variable to point to any directory (e.g., `%APPDATA%\Claude\memory\LiveMonitor`). Use when you want memory outside the project tree.
2. **`<project_root>/memory/`** — default project-relative location.

This means any LLM can discover the memory location by reading `CLAUDE.md` (this section) and, if needed, checking the environment. The env var makes the system portable across folder structures without editing hook scripts.


## Output shorthand (narration only — NOT final answers)

You MAY compress these using the same vocab:
- Progress updates between tool calls ("reading USS for nxt on wl...")
- Status narration ("checking QP")
- Thinking/reasoning you emit before a conclusion

You MUST write in full English for:
- The final prose answer to the user (the last block before you stop)
- Explanations the user must act on (instructions, warnings, confirmations)
- Any output a user will copy or share

Rule of thumb: if you're narrating your own work → shorthand. If you're answering the user → English.

## Hard exclusions — NEVER use shorthand in:

- Edit / Write tool `content` or `new_string` parameters (file contents)
- Bash tool `command` parameter (shell execution)
- Git commit messages
- Grep / Glob pattern parameters
- Any tool input that flows to disk, shell, or a remote system
- Anything inside backticks anywhere
- File paths, identifiers, SQL, JSON, error messages

A `validate-no-shorthand` PreToolUse hook enforces this. If you write shorthand into a tool call, the call is blocked and you must expand and retry in English. Don't fight the validator — it's there to prevent breaking the user's code.

Rule: shorthand lives in the conversation stream. It must never enter a tool call's arguments.

### Memory location discovery

Hooks resolve memory file paths in this order:
1. **`$env:CLAUDE_COMPRESSION_MEMORY`** — set this environment variable to point to any directory (e.g., `%APPDATA%\Claude\memory\LiveMonitor`). Use when you want memory outside the project tree.
2. **`<project_root>/memory/`** — default project-relative location.

This means any LLM can discover the memory location by reading `CLAUDE.md` (this section) and, if needed, checking the environment. The env var makes the system portable across folder structures without editing hook scripts.


## Output shorthand (narration only — NOT final answers)

You MAY compress these using the same vocab:
- Progress updates between tool calls ("reading USS for nxt on wl...")
- Status narration ("checking QP")
- Thinking/reasoning you emit before a conclusion

You MUST write in full English for:
- The final prose answer to the user (the last block before you stop)
- Explanations the user must act on (instructions, warnings, confirmations)
- Any output a user will copy or share

Rule of thumb: if you're narrating your own work → shorthand. If you're answering the user → English.

## Hard exclusions — NEVER use shorthand in:

- Edit / Write tool `content` or `new_string` parameters (file contents)
- Bash tool `command` parameter (shell execution)
- Git commit messages
- Grep / Glob pattern parameters
- Any tool input that flows to disk, shell, or a remote system
- Anything inside backticks anywhere
- File paths, identifiers, SQL, JSON, error messages

A `validate-no-shorthand` PreToolUse hook enforces this. If you write shorthand into a tool call, the call is blocked and you must expand and retry in English. Don't fight the validator — it's there to prevent breaking the user's code.

Rule: shorthand lives in the conversation stream. It must never enter a tool call's arguments.

### Memory location discovery

Hooks resolve memory file paths in this order:
1. **`$env:CLAUDE_COMPRESSION_MEMORY`** — set this environment variable to point to any directory (e.g., `%APPDATA%\Claude\memory\LiveMonitor`). Use when you want memory outside the project tree.
2. **`<project_root>/memory/`** — default project-relative location.

This means any LLM can discover the memory location by reading `CLAUDE.md` (this section) and, if needed, checking the environment. The env var makes the system portable across folder structures without editing hook scripts.


## Output shorthand (narration only — NOT final answers)

You MAY compress these using the same vocab:
- Progress updates between tool calls ("reading USS for nxt on wl...")
- Status narration ("checking QP")
- Thinking/reasoning you emit before a conclusion

You MUST write in full English for:
- The final prose answer to the user (the last block before you stop)
- Explanations the user must act on (instructions, warnings, confirmations)
- Any output a user will copy or share

Rule of thumb: if you're narrating your own work → shorthand. If you're answering the user → English.

## Hard exclusions — NEVER use shorthand in:

- Edit / Write tool `content` or `new_string` parameters (file contents)
- Bash tool `command` parameter (shell execution)
- Git commit messages
- Grep / Glob pattern parameters
- Any tool input that flows to disk, shell, or a remote system
- Anything inside backticks anywhere
- File paths, identifiers, SQL, JSON, error messages

A `validate-no-shorthand` PreToolUse hook enforces this. If you write shorthand into a tool call, the call is blocked and you must expand and retry in English. Don't fight the validator — it's there to prevent breaking the user's code.

Rule: shorthand lives in the conversation stream. It must never enter a tool call's arguments.

### Memory location discovery

Hooks resolve memory file paths in this order:
1. **`$env:CLAUDE_COMPRESSION_MEMORY`** — set this environment variable to point to any directory (e.g., `%APPDATA%\Claude\memory\LiveMonitor`). Use when you want memory outside the project tree.
2. **`<project_root>/memory/`** — default project-relative location.

This means any LLM can discover the memory location by reading `CLAUDE.md` (this section) and, if needed, checking the environment. The env var makes the system portable across folder structures without editing hook scripts.


## Input compression vocab (self-learning)

**Session-start banner:** the `compress-prompt` hook sets a one-shot banner instruction on the first turn of each session (inside `<compression-banner>` or `<vocab-context>` tags). If you see that instruction, prepend the requested `⟨compression: ACTIVE · vocab=N · ...⟩` line to your FIRST reply in the session — it's how the user confirms the pipeline is live without interrogating you. After that first turn, stop emitting it.

Read `memory/session_vocab.md` at session start. When you see shorthand in user input:
1. Expand it using the active dict before reasoning.
2. After each turn where you expanded a shorthand, increment "Uses this session" for that row.
3. If a long phrase (≥15 chars) appears ≥3 times in this conversation, add a row under "Proposed" with a 2-3 char abbreviation you believe tokenizes to 1-2 BPE tokens.
4. If a "Proposed" entry hits 3 uses, move it to "Active" and reset counter to 0.
5. On /compact or session end: increment `Sessions used` in `vocab_stable.md` for any active entry used this session. If it hits 5 sessions, move it to `vocab_stable.md` with today's date as "Locked since".
6. Prune: remove active entries with 0 uses for 10 consecutive sessions. Max 50 rows total in session_vocab.md.

## Output shorthand (narration only — NOT final answers)

You MAY compress these using the same vocab:
- Progress updates between tool calls ("reading USS for nxt on wl...")
- Status narration ("checking QP")
- Thinking/reasoning you emit before a conclusion

You MUST write in full English for:
- The final prose answer to the user (the last block before you stop)
- Explanations the user must act on (instructions, warnings, confirmations)
- Any output a user will copy or share

Rule of thumb: if you're narrating your own work → shorthand. If you're answering the user → English.

## Hard exclusions — NEVER use shorthand in:

- Edit / Write tool `content` or `new_string` parameters (file contents)
- Bash tool `command` parameter (shell execution)
- Git commit messages
- Grep / Glob pattern parameters
- Any tool input that flows to disk, shell, or a remote system
- Anything inside backticks anywhere
- File paths, identifiers, SQL, JSON, error messages

A `validate-no-shorthand` PreToolUse hook enforces this. If you write shorthand into a tool call, the call is blocked and you must expand and retry in English. Don't fight the validator — it's there to prevent breaking the user's code.

Rule: shorthand lives in the conversation stream. It must never enter a tool call's arguments.

### Memory location discovery

Hooks resolve memory file paths in this order:
1. **`$env:CLAUDE_COMPRESSION_MEMORY`** — set this environment variable to point to any directory (e.g., `%APPDATA%\Claude\memory\LiveMonitor`). Use when you want memory outside the project tree.
2. **`<project_root>/memory/`** — default project-relative location.

This means any LLM can discover the memory location by reading `CLAUDE.md` (this section) and, if needed, checking the environment. The env var makes the system portable across folder structures without editing hook scripts.
