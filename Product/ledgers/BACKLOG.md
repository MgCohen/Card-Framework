---
type: ledger
last-updated: 2026-04-30
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

- **`/builder:debt-pass` command** — threshold-driven auto-discharge of `ALIGNMENT-DEBT.md`. Slash command at `.claude/commands/builder/debt-pass.md`; GHA workflow on push-to-main with `paths: [Product/ledgers/ALIGNMENT-DEBT.md]` and a 5-entry threshold gate; opens a PR for human review per §5.5/§5.8. Promoted out of the Tier 2/3 umbrella below.
  Trigger: ready to build now (plan in `PLANS/auto-debt-pass.md`, gaps 1–9 resolved).
  References: `PLANS/auto-debt-pass.md`, `_workflow/PRODUCT-BUILDER.md` §5.5, §5.7, §5.8.
  Status: open

- **`/builder:design-pass` skill** — structured Q-by-Q design walk for non-mechanical Model edits. Pattern: enumerate questions in dependency order; for each present options + lean + rationale; wait for verdict; advance. Locks open design space progressively without committing edits until all Qs settle. Session 15 is the canonical example (8 Qs on the §6 boundary redesign).
  Trigger: when a second design question of similar shape arrives, or post Tier 1 commands.
  References: `Product/sessions/session-15.md`, `ProductBuilder/AI-FIRST-AUTOMATION.md` §8.
  Status: open

- **`/builder:end-session` headless mode** — extend with flags `--auto-confirm-checkpoint`, `--auto-pr`, and `--steps=close-only` so it can be composed by unattended automation (specifically `/builder:debt-pass`'s checkpoint-refresh step). Currently end-session has a hard human-approval gate on the checkpoint diff (step 5) and "suggest, don't auto-create" PR default (step 7) — both load-bearing for interactive use, but block headless composition.
  Trigger: after `/builder:debt-pass` lands and accumulates evidence on what end-session needs to expose. Blocks: migrating debt-pass step 7 from local fill rules to delegation.
  References: `PLANS/auto-debt-pass.md` §1 step 7, `.claude/commands/builder/end-session.md`.
  Status: open

- **Other Tier 2/3 commands and skills** — see `ProductBuilder/AI-FIRST-AUTOMATION.md` §8 sequencing for the ordered list and triggers. (`/builder:debt-pass` was promoted out of this umbrella above.)
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
