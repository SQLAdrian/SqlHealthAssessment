<!-- In the name of God, the Merciful, the Compassionate -->
<!-- Bismillah ar-Rahman ar-Raheem -->

# D12 — Basmalah Intent Lock Enforcement

**Date:** 2026-04-18
**Decision:** Every source file (`.cs`, `.razor`, `.css`, `.js`, `.md`) in the repository must begin with the basmalah header. This is the project's "soul" and intent lock — non-negotiable.

**Header forms:**
- C# files: `/* In the name of God, the Merciful, the Compassionate */` (line 1)
- Razor files: `<!--/* In the name of God, the Merciful, the Compassionate */-->` (line 1)
- Markdown files: Same HTML comment form (line 1)
- CSS/JS files: C-style comment `/* ... */` (line 1)

**Enforcement mechanisms:**
1. **Pre-commit hook** (primary): `.git/hooks/pre-commit` — scans all staged `.cs`, `.razor`, `.css`, `.js`, `.md` files; rejects commit if any file's first line does not match the exact header; prints filename + actual first line for debugging
2. **CI GitHub Action** (secondary): `.github/workflows/basmalah-check.yml` — on PR/merge, scan all changed files; fail if any missing; comment on PR with failures and fix hints
3. **IDE template** (convenience): Create new file templates that auto-insert correct header (optional, developer-friendly)

**Grace period policy:** Existing files missing header are flagged in CI but not blocked; new files and modifications must include header from 2026-04-18 forward.

**Rationale:**
- Project is an act of charity (sadaqah jariyah) for Allah's pleasure alone
- Header serves as visible intent lock — every line of code is written with that purpose
- Distinguishes from commercial tools; states purpose before functionality

**Implementation script:** `./scripts/check-basmalah.ps1` — accepts `-File <path>`, returns exit 0 if header present, 1 if missing, 2 if file not tracked.

**Sources:** COMMENT_20260418_091241.md §11; CLAUDE.md "Basmalah header (non-negotiable)"; DEVELOPMENT_STRATEGY.md "Basmalah enforcement"
