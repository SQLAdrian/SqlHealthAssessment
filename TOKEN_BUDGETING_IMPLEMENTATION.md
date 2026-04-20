<!-- In the name of God, the Merciful, the Compassionate -->
<!-- Bismillah ar-Rahman ar-Raheem -->

# Token Budgeting Implementation Notes

## Context

The input compression system was introduced to mitigate token consumption in long-running LLM coding sessions. It builds on the `llmck` index-first context discipline, where most of the project's knowledge is stored outside the conversation in indexed files.

## Budget Strategy

| Phase | Budget | Strategy |
|-------|--------|----------|
| Index-first | ~2 000 tok | Read `.claude/docs/*-index.md` only |
| Deep dive | ~4 000 tok | Grep → read single targeted source file |
| Compression active | ~1 200 tok | 25–40% reduction on repeated phrases via shorthand expansion |

The LLM tracks session token usage automatically via hook instrumentation. When budget exceeds 85%, the `validate-no-shorthand` hook warns and temporarily disables *new* shorthand proposals until consumption normalises.

## Implementation Components

### Python: `llmck/`

- `utils.py` — shared helpers (path resolution, markdown table parsing, JSONL rotation). Currently minimal; may grow as compression matures.
- `sync.py` — regenerates `pages-index.md` and `services-index.md` from source-file heuristics.
- `validate.py` — compares index contents against live code (anchor-based) to detect drift.

### PowerShell Hooks (templates)

| Hook | Trigger | Purpose |
|------|---------|---------|
| `compress-prompt.ps1` | UserPromptSubmit (pre-LLM) | Expand shorthands in user text before Claude sees it |
| `validate-no-shorthand.ps1` | PreToolUse | Block tool calls carrying raw shorthand in arguments |
| `log-compression-metrics.ps1` | UserPromptSubmit (post-turn) | Append a JSONL record to `memory/compression-metrics.jsonl` |
| `claude-md-compression-block` | Paste into CLAUDE.md | Document rules; read by LLM at session start |

### Memory Files (project-relative or env overridden)

```
memory/
├── session_vocab.md        # Active + Proposed (mutable per-session)
├── vocab_stable.md         # Locked graduated entries (append-only)
├── compression-metrics.jsonl  # one JSON object per turn
└── compression-banner.md   # one-shot session banner (auto-generated)
```

## Token Savings Calculation

Saved % = `(original_chars − compressed_chars) / original_chars × 100`

Per-turn records allow computation of:

- Cumulative savings per session
- Average compression ratio across sessions
- Most-used shorthands (by frequency and by total chars saved)
- Optimal retirement threshold: shorthands with <5 lifetime uses pruned after 10 silent sessions

## Maintenance

- Run `python -m llmck validate` daily to detect index drift.
- On vocabulary churn (>10 new Active entries), review `compression-metrics.jsonl` to ensure savings justify cognitive overhead.
- If `validate-no-shorthand` false-positives spike, check for over-aggressive Regex (word-boundary collisions with backticks).

## References

- Related: `docs/self-learning-compression.md` (feature spec)
- CLAUDE.md § "Input compression vocab (self-learning)" and § "Hard exclusions"
- Repository: https://github.com/afsultan/llm-context-kit