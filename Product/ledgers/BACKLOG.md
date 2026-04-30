---
type: ledger
last-updated: 2026-04-29
---

# Backlog

Deferred concrete work — known TODOs with trigger conditions for revisit. Distinct from `OPEN-QUESTIONS.md` (deliberate deferrals, not unanswered questions). See `_workflow/PRODUCT-BUILDER.md` §6 for cadence.

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
  Trigger: `~15 Models in Product/features/` OR first drift incident traceable to a stale FEATURE-MAP.
  References: `ProductBuilder/AI-FIRST-AUTOMATION.md` §6, `ProductBuilder/PRODUCT-BUILDER-PROPOSAL.md` §6.
  Status: open

- **`/builder:lint` skill** — reference-staleness linter (broken paths, terms missing from glossary, Models pointing at superseded ADRs without acknowledgment, missing/invalid `builds-on:`, Sample edits without cooldown commit-message marker).
  Trigger: post-migration, after Tier 1 commands settle.
  References: `ProductBuilder/AI-FIRST-AUTOMATION.md` §3, §5.10 of proposal.
  Status: open

- **`builder-drift-detection` skill** — session-mode + CI-mode drift detection between code in `Assets/` and `settled`/`implemented` Features/Models in `Product/`.
  Trigger: first feature reaches `implemented` status (§5.6.2 build gate).
  References: `ProductBuilder/AI-FIRST-AUTOMATION.md` §5, `ProductBuilder/PRODUCT-BUILDER-PROPOSAL.md` §5.12.
  Status: open

- **Other Tier 2/3 commands and skills** — see `ProductBuilder/AI-FIRST-AUTOMATION.md` §8 sequencing for the ordered list and triggers.
  Trigger: per item in §8.
  References: `ProductBuilder/AI-FIRST-AUTOMATION.md` §8.
  Status: open

- **Definition Editor** — authoring tool for cards, zones, game templates, effects, visuals.
  Trigger: `authoring` feature reaches `settled`.
  References: ARCHITECTURE §"Authoring", FEATURE-MAP `authoring` (considered).
  Status: open

- **Simulation Debugger** — inspector for state, history, pending tasks, triggers, decisions.
  Trigger: first feature reaches `implemented` (debugger needs a real running simulation to inspect).
  References: ARCHITECTURE §"Tooling".
  Status: open

- **Scenario Runner** — load a known state and exercise a command/effect for fast iteration.
  Trigger: first feature reaches `implemented`.
  References: ARCHITECTURE §"Tooling".
  Status: open

- **Replay Viewer** — step through history and inspect state transitions.
  Trigger: history pipeline (`engine/03-snapshot-pipeline`) reaches `implemented`.
  References: features/engine/models/03-snapshot-pipeline.md, ARCHITECTURE §"Tooling".
  Status: open

- **Rule Trace** — explain why an action was legal/illegal and which effects modified it.
  Trigger: first feature reaches `implemented`.
  References: ARCHITECTURE §"Tooling".
  Status: open

- **Validation Reports** — detect broken card definitions, missing assets, bad IDs, invalid references.
  Trigger: `authoring` feature reaches `draft` (needs a definition surface to validate).
  References: FEATURE-MAP `authoring` (considered).
  Status: open

- **Versioned Definitions** — definition versioning/hashing for client-server agreement.
  Trigger: `networking` feature begins; or save/load arrives, whichever first.
  References: FEATURE-MAP `networking` (considered).
  Status: open

- **Presets package** — preset rule templates (TCG, deckbuilder, spatial sim) as compositions of framework primitives.
  Trigger: post-v1, after at least 2 features reach `implemented` and the framework primitive surface is stable.
  References: ARCHITECTURE §"Preset Direction", RECOMMENDATIONS §"Recommendation 7".
  Status: open

## Done

_(none yet)_
