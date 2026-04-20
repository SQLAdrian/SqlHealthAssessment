<!-- In the name of God, the Merciful, the Compassionate -->
<!-- Bismillah ar-Rahman ar-Raheem -->

# Self-Learning Token Compression — Feature Specification

## Overview

The self-learning compression system enables Claude Code to maintain a session-scoped and long-term shorthand vocabulary that automatically expands and contracts based on usage. The goal is reducing token consumption on repetitive phrases while preserving backward compatibility and strict boundaries between conversation shorthand and tool arguments.

## Core Concepts

### Vocabulary Tiers

| Tier | Location | Mutability | Lifetime | Typical entries |
|------|----------|------------|----------|----------------|
| **Active** | `memory/session_vocab.md` | Mutable (in-session) | Per-session | "Workload" → "wl" (3+ uses this session) |
| **Stable** | `memory/vocab_stable.md` | Locked (read-only) | Across sessions | Graduated from Active after 5 sessions |
| **Proposed** | `memory/session_vocab.md` (Proposed section) | ephemeral | Within session only | Candidates before 3-use threshold |

### Discovery & Memory Resolution

All hook scripts resolve their memory file paths via:

1. **`$env:CLAUDE_COMPRESSION_MEMORY`** — explicit override (e.g., `%APPDATA%\Claude\memory\LiveMonitor`)
2. **`<project_root>/memory/`** — default project-relative location

This two-tier resolution allows the same hooks to work on multiple projects without modification.

## Data Flow

```
User input → compress-prompt hook → expand shorthands → LLM reasoning
     ↓
Tool call (Edit/Write/etc) → validate-no-shorthand → block if un-backticked shorthand
     ↓
Turn completion → log-compression-metrics → append JSONL record
     ↓
Session end / /compact → promote Active→Stable, prune zeros, sync memory files
```

## Self-Learning Loop

### 1. Read
At session start, read `memory/session_vocab.md` and `memory/vocab_stable.md` into an in-memory dictionary. Display a banner: `⟨compression: ACTIVE · vocab=N · memory=<path>⟩`.

### 2. Expand
When the user's input contains a shorthand (e.g., "check wl status"), expand it using the full phrase from the active or stable vocabulary *before* passing text to the LLM for reasoning.

### 3. Observe
After every LLM turn, detect which shorthands appeared in the user's input during that turn. Increment the "Uses this session" counter for each matching row in `session_vocab.md`.

### 4. Propose
If a long phrase (≥15 characters) appears ≥3 times in the conversation, automatically add a row under the "Proposed" section with a 2–3 character abbreviation. The abbreviation should tokenise to 1–2 BPE tokens in GPT-2/4 tokenisers.

### 5. Promote
When a "Proposed" entry reaches 3 uses this session, move it to the "Active" table and reset its counter to 0.

### 6. Persist
On `/compact` or session end:
- Increment `Sessions used` in `vocab_stable.md` for every Active entry used this session.
- If `Sessions used` ≥ 5, move the entry to `vocab_stable.md` with today's date in "Locked since". Never delete stable entries.
- Prune Active entries with 0 uses across 10 consecutive sessions.
- Enforce a hard limit of 50 rows in `session_vocab.md` Active table (evict LRU if needed).

### 7. Log
Each turn, record `compression-metrics.jsonl`:
```json
{"timestamp":"2026-04-20T03:00:00Z","original_chars":142,"compressed_chars":105,"saved_pct":26.06,"matched_shorthand":["wl","nxt"]}
```

## Validation & Hard Exclusions

Shorthand is permitted **only in the conversational stream**. Blocked contexts:

- Tool call parameters (Edit `content`, Write `new_string`, Bash `command`, etc.)
- File paths, identifiers, SQL, JSON
- Anything inside backticks (`` `shorthand` ``)
- Git commit messages
- Grep/Glob patterns

The `validate-no-shorthand` PreToolUse hook enforces all these restrictions by scanning tool arguments against the current vocabulary before dispatch. If it finds an un-backticked shorthand, it exits 1 and the tool call is cancelled.

## Extension Points

- New project? Drop the `templates/compression/` scripts into your hook directory and paste the `claude-md-compression-block.template` into your `CLAUDE.md`.
- Custom vocabulary? Edit `memory/session_vocab.md` manually (add rows under "Proposed") before the learning loop observes them.
- External memory? Set `$env:CLAUDE_COMPRESSION_MEMORY` to share vocabulary across multiple project clones or worktrees.

## Security & Privacy

- All vocab files and metrics live inside the project directory (or the explicitly configured memory path). No telemetry is sent anywhere.
- Hooks only read text — they never modify source files unless the user explicitly commits a vocabulary change.
- The validator blocks tool calls but does not alter or inject content.

## Future Work

- Token-aware scoring: use tiktoken to estimate actual token savings, not just character ratios.
- Cross-project vocabulary sync via a shared memory directory.
- Auto-suggest retirement for Active entries that plateau below a usage threshold.