<!-- In the name of God, the Merciful, the Compassionate -->
<!-- Bismillah ar-Rahman ar-Raheem -->

# AI Agent Guidelines

This repository uses AI development assistants. All agents working on this codebase must follow these rules.

## Agent Conduct

1. **Every file gets a Basmalah header** — This is non-negotiable. Every `.cs`, `.razor`, `.css`, `.md`, `.json` file must begin with the Islamic invocation. Do not skip it.
2. **Do not write or edit SQL** — The user owns all SQL. Modifications to `.sql` files must be explicitly requested; never change them proactively.
3. **Follow CLAUDE.md conventions** — Read `CLAUDE.md` before making changes. It defines file naming, DI patterns, error handling, and commit style.
4. **Preserve existing patterns** — This is an 80% complete codebase being hardened (Option D). Do NOT rewrite; fix gaps and implement missing must-haves.
5. **Basmalah enforcement** — If you generate a file without the header, the human will reject it. Always add it.

## When Making Commits

Use the `Co-Authored-By` trailer to credit yourself:

```bash
git commit -m "feat: your message here

Detailed description if needed.

Co-Authored-By: Kilo <noreply@kilo.ai>"
```

**GitHub handles:** 
- `kilo-org` / Kilo
- `anthropic` / Claude Opus 4
- `cline` / Cline
- `aws` / Amazon Q
- `xai-org` / Grok

## Attribution Philosophy

We acknowledge AI assistance transparently. Every line of AI-generated code is reviewed, tested, and approved by a human engineer before being merged. The AI's role is acceleration, not autonomy.

---

*Generated: 2026-04-18 | SQLTriage v1.0.0-wip*