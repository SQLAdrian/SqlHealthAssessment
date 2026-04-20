---
name: Session Vocabulary
description: Active abbreviations for input compression — auto-updated by LLM each session
type: reference
---

# Session Vocabulary

**Rules for the LLM:**
- When you see a shorthand token below in user input, expand it using this table before reasoning.
- If you CANNOT expand a shorthand (not in table AND can't infer from context), flag it: `[unknown shorthand: XYZ]` and ask the user.
- When you notice a phrase repeating ≥3 times in a session, propose an abbreviation: add a row under "Proposed" with `[PROPOSED]` tag.
- Prefer abbreviations that are 1-2 BPE tokens. Uppercase acronyms usually tokenize well (e.g. USS = 1 token, UsrStSrv = 3 tokens).

## Active abbreviations

| Shorthand | Full phrase | Uses this session | Status |
|-----------|-------------|-------------------|--------|
| wl | worklist | 0 | active |
| nxt | what is next | 0 | active |

## Proposed (awaiting ≥3 uses to promote to active)

_(LLM adds rows here when a phrase repeats ≥3 times. After 3 uses in "Proposed", move the row to Active above.)_
