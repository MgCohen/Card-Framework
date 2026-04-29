---
type: ledger
last-updated: 2026-04-29
---

# Backlog

Deferred concrete work — known TODOs with trigger conditions for revisit. Distinct from `OPEN-QUESTIONS.md` (deliberate deferrals, not unanswered questions). See `general/PRODUCT-BUILDER.md` §6 for cadence.

**Entry shape** (see proposal §6 Ledger entry shapes):

```markdown
- **Title** — 2-line context describing what's deferred and why.
  Trigger: `<condition that makes this actionable>`.
  References: `<paths or links to related docs>`.
  Status: open
```

Done entries get strikethrough plus link to the PR/commit that closed them:

```markdown
- ~~**Title** — context. Trigger: ... References: ... Status: open~~ → done YYYY-MM-DD ([commit](link))
```

---

## Open

- **FEATURE-MAP auto-generator** — replace hand-maintained `FEATURE-MAP.md` with a generator that reads feature roots, Models, and `builds-on:` front matter to produce the index table, mermaid graph, and model breakdown.
  Trigger: `~15 Models in PRODUCT/features/` OR first drift incident traceable to a stale FEATURE-MAP.
  References: `ProductBuilder/AI-FIRST-AUTOMATION.md` §6, `ProductBuilder/PRODUCT-BUILDER-PROPOSAL.md` §6.
  Status: open

- **`/builder:lint` skill** — reference-staleness linter (broken paths, terms missing from glossary, Models pointing at superseded ADRs without acknowledgment, missing/invalid `builds-on:`, Sample edits without cooldown commit-message marker).
  Trigger: post-migration, after Tier 1 commands settle.
  References: `ProductBuilder/AI-FIRST-AUTOMATION.md` §3, §5.10 of proposal.
  Status: open

- **`builder-drift-detection` skill** — session-mode + CI-mode drift detection between code in `Assets/` and `settled`/`implemented` Features/Models in `PRODUCT/`.
  Trigger: first feature reaches `implemented` status (§5.6.2 build gate).
  References: `ProductBuilder/AI-FIRST-AUTOMATION.md` §5, `ProductBuilder/PRODUCT-BUILDER-PROPOSAL.md` §5.12.
  Status: open

- **Other Tier 2/3 commands and skills** — see `ProductBuilder/AI-FIRST-AUTOMATION.md` §8 sequencing for the ordered list and triggers.
  Trigger: per item in §8.
  References: `ProductBuilder/AI-FIRST-AUTOMATION.md` §8.
  Status: open

## Done

_(none yet)_
