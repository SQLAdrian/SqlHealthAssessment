<!-- In the name of God, the Merciful, the Compassionate -->
<!-- Bismillah ar-Rahman ar-Raheem -->

# D09 — Basmalah Intent Lock Policy

**Date:** 2026-04-18
**Decision:** Every `.cs`, `.razor`, `.css`, `.js`, and `.md` file in the repository must begin with the basmalah header. Enforced via pre-commit hook (primary) and CI lint step (secondary).

**Headers:**
- C# files: `/* In the name of God, the Merciful, the Compassionate */` (line 1, no preceding whitespace)
- Razor files: `<!--/* In the name of God, the Merciful, the Compassionate */-->` (line 1)
- Markdown files: Same as Razor (HTML comment style)
- CSS/JS files: `/* In the name of God, the Merciful, the Compassionate */` (C-style comment)

**Rationale:**
- This project is an act of charity (sadaqah jariyah) for the pleasure of Allah
- The basmalah serves as an "intent lock" — a reminder of purpose at the file level
- Public declaration of intent; distinguishes from commercial tools

**Enforcement:**
- **Pre-commit hook** (`.git/hooks/pre-commit`): Scans staged `.cs`, `.razor`, `.css`, `.js`, `.md` files; rejects commit if any lack header; prints filename + line 1 content for debugging
- **CI GitHub Action** (`.github/workflows/lint-basmalah.yml`): On PR/merge, scan all changed files; fail if any missing; comment on PR with failures
- **Grace period:** Existing files without header flagged, not blocked; new/modified files must have header from today forward

**Tooling:**
- Hook script: `./scripts/check-basmalah.ps1` (PowerShell; reads file, checks first line begins with expected string)
- CI step: `run: powershell ./scripts/check-basmalah.ps1` on `paths` from `github.event.head_commit...`

**Exceptions:** None. Third-party code (NuGet packages, submodules) exempt — only our source files.

**Sources:** CLAUDE.md "Basmalah header (non-negotiable)"; DEVELOPMENT_STRATEGY.md "Basmalah enforcement"; COMMENT_20260418_091241.md §11
