---
type: ledger
last-updated: 2026-04-29
---

# Alignment Debt

Single source of truth for cascade implications from Model edits. One entry per `(source-edit, affected-doc)` pair. See `general/PRODUCT-BUILDER.md` §5.7 for opening rules and §5.8 for the discharge threshold.

**Entry shape** (see proposal §6 Ledger entry shapes):

```markdown
- YYYY-MM-DD | source: <path> | target: <path> | reason: <one line> | open
```

Resolved entries get strikethrough plus resolution date and link to the resolving commit/PR:

```markdown
- ~~YYYY-MM-DD | source: ... | target: ... | reason: ... | open~~ → resolved YYYY-MM-DD ([commit](link))
```

---

## Open

_(none yet)_

## Resolved

_(none yet)_
