---
type: claude-rules
last-updated: 2026-04-20
maintainer: [Your Name]
version: 1.0
---

## Role

You are an AI assistant helping with this C# project. Follow context discipline rules to provide accurate, efficient responses. Use indexes first for navigation, then read code selectively. Maintain conventions and validate information.

## Guidelines

### Search Order
1. Check `services-index.md` for service locations
2. Find service file and grep for methods/concepts
3. Read only relevant sections (avoid full scans)
4. If not found, check `pages-index.md`
5. As last resort, grep codebase (code files only)

### File Types
- **Read**: .cs, .xaml, .razor, .py, .js, .go (code logic)
- **Skip**: .json, .sql, .xml, .csv, .config, .resx (data/config)

### Why This Approach
- Fast: 2-3 targeted reads vs. 50+ full scans
- Accurate: Human-maintained indexes
- Verifiable: Check against code

## Context

### Resources
- `services-index.md`: Service catalog and locations
- `pages-index.md`: UI pages and components
- `.claude/docs/`: All project indexes

### Conventions
- **Organization**: Services in `Data/Services/`, Pages in `Pages/`
- **Naming**: Services end with `Service`, Interfaces with `I`
- **Async**: Methods return `Task<T>`
- **DI**: Constructor injection only, registered in `Program.cs`
- **DB**: Repository pattern, parameterized queries, transactions
- **Errors**: Log and re-throw, structured logging

### Maintenance
- Update indexes after refactors
- Validate: Grep files mentioned in indexes
- Refresh: Monthly or after major changes

## Examples

**Finding a service method:**
1. Check `services-index.md` for `AlertService`
2. Grep `Data/Services/AlertService.cs` for method
3. Read only that method's code

**Handling drift:**
- If index points to missing file, update index
- Run `llmck validate` to check

## Notes

- Known gaps: [Update as needed]
- Q&A: Incompleteness? Fill in. Deviate from order? Justify. Refresh frequency? Quarterly.
