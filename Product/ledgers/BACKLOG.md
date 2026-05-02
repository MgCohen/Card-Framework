---
type: ledger
last-updated: 2026-05-01
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

- id: bl-interaction-feature | **`interaction` feature — gesture-to-intent layer** — `features/interaction/` folder and at least one Model doc needed. The presentation loop assumes this layer converts drag/drop/click gestures to `PlayerIntent`, but no model, no folder, and no ADR exists. All intent vocabulary in `features/actions/decisions/0008-intent-vocabulary.md` depends on this layer to emit the intents.
  Trigger: next presentation or engine-wiring session; or whenever intent → template lookup (OPEN-QUESTIONS) is designed.
  References: `general/ARCHITECTURE.md`, `features/actions/decisions/0008-intent-vocabulary.md`, `features/presentation/models/01-presentation-loop.md` §8.
  Status: open

- id: bl-authoring-feature | **`authoring` feature — effect recipe and card catalog models** — `features/authoring/` folder and at least one Model doc needed. `effects/04-effect-flow.md` §3 defers how a card definition holds a per-instance `Effect` recipe to "a future authoring Model." No model, no folder, no ADR, no spike exists.
  Trigger: after slice catalog and game lifecycle land; before any vertical-slice implementation.
  References: `features/effects/models/04-effect-flow.md` §3 §9, `general/ARCHITECTURE.md` §Authoring, `ledgers/OPEN-QUESTIONS.md` §Authoring.
  Status: open

- id: bl-feature-map-generator | **FEATURE-MAP auto-generator** — replace hand-maintained `FEATURE-MAP.md` with a generator that reads feature roots, Models, and `builds-on:` front matter to produce the index table, mermaid graph, and model breakdown.
  Trigger: `~15 Models in Product/features/` OR first drift incident traceable to a stale FEATURE-MAP.
  References: `ProductBuilder/AI-FIRST-AUTOMATION.md` §6, `ProductBuilder/PRODUCT-BUILDER-PROPOSAL.md` §6.
  Status: open

- ~~**`/builder:lint` skill** — reference-staleness linter (broken paths, terms missing from glossary, Models pointing at superseded ADRs without acknowledgment, missing/invalid `builds-on:`, Sample edits without cooldown commit-message marker). Trigger: post-migration, after Tier 1 commands settle. References: `ProductBuilder/AI-FIRST-AUTOMATION.md` §3, §5.10 of proposal. Status: open~~ → done 2026-05-01 (skill spec at `.claude/commands/builder/lint.md`; archive integrity rules A1–A4 wired in; report template inlined into the skill)

- ~~**Migrate existing superseded/cancelled docs into archive/ (PR 2)** — grep `Product/features/` for any docs with `status: Superseded` or `status: Cancelled` (or the older `abandoned` equivalent per §4.2–§4.3), move each into its feature's `archive/` folder, add the four front-matter fields (`status`, `superseded_by`, `archived_on`, `reason`), run `/builder:lint`, fix any inbound-link warnings the lint flags. Trigger: immediately after the archive-convention PR (0004-archive-convention.md) merges. References: `Product/_workflow/PRODUCT-BUILDER.md §5.14`, `Product/decisions/0004-archive-convention.md`. Status: open~~ → done 2026-05-01 — migration scan ran clean; all 13 Models and 19 ADRs in `Product/features/` carry standard active statuses (`draft`, `settled`, `implemented`, `proposed`, `accepted`). The word "superseded" appearing in `actions/models/01-command-pipeline.md` and `effects/models/01-async-dispatch.md` is inline prose commentary on retired *concepts* within live docs, not a retired-doc status — no file moves required. `/builder:lint --archive-only` reports zero findings on all 7 feature `archive/` folders.

- ~~**`/builder:archive` skill (PR 3, optional)** — helper that automates archiving a doc: flips `status` to `Superseded` or `Cancelled`, writes `superseded_by` / `archived_on` / `reason`, `git mv`s the file into the feature's `archive/` folder (creating it with `.gitkeep` if absent), runs `/builder:lint`, opens a single-commit PR. One commit, mechanical. Trigger: after PR 2 migration lands. References: `Product/_workflow/PRODUCT-BUILDER.md §5.14`, `.claude/commands/builder/lint.md`. Status: open~~ → done 2026-05-01 (skill spec at `.claude/commands/builder/archive.md`; validates source, shows diff, user confirms, single commit + PR)

- id: bl-drift-detection | **`builder-drift-detection` skill** — session-mode + CI-mode drift detection between code in `Assets/` and `settled`/`implemented` Features/Models in `Product/`.
  Trigger: first feature reaches `implemented` status (§5.6.2 build gate).
  References: `ProductBuilder/AI-FIRST-AUTOMATION.md` §5, `ProductBuilder/PRODUCT-BUILDER-PROPOSAL.md` §5.12.
  Status: open

- ~~**`/builder:debt-pass` command** — threshold-driven auto-discharge of `ALIGNMENT-DEBT.md`. Slash command at `.claude/commands/builder/debt-pass.md`; GHA workflow on push-to-main with `paths: [Product/ledgers/ALIGNMENT-DEBT.md]` and a 5-entry threshold gate; opens a PR for human review per §5.5/§5.8. Promoted out of the Tier 2/3 umbrella below. Trigger: ready to build now (plan in `PLANS/auto-debt-pass.md`, gaps 1–9 resolved). References: `PLANS/auto-debt-pass.md`, `_workflow/PRODUCT-BUILDER.md` §5.5, §5.7, §5.8. Status: open~~ → done 2026-05-01 (command spec at `.claude/commands/builder/debt-pass.md`, GHA workflow at `.github/workflows/debt-pass.yml`, runbook at `Product/_workflow/_validation/debt-pass-runbook.md`, retired-tokens registry seeded at `Product/_workflow/retired-tokens.json`)

- ~~**`/builder:design-pass` skill** — structured Q-by-Q design walk for non-mechanical Model edits. Pattern: enumerate questions in dependency order; for each present options + lean + rationale; wait for verdict; advance. Locks open design space progressively without committing edits until all Qs settle. Session 15 is the canonical example (8 Qs on the §6 boundary redesign). Trigger: when a second design question of similar shape arrives, or post Tier 1 commands. References: `Product/sessions/session-15.md`, `ProductBuilder/AI-FIRST-AUTOMATION.md` §8. Status: open~~ → done 2026-05-01 (skill spec at `.claude/commands/builder/design-pass.md`, report template inlined into the skill, 🔒 chat-only extension added to EMOJI-LEGEND.md)

- ~~id: bl-end-session-headless | **`/builder:end-session` headless mode** — extend with flags `--auto-confirm-checkpoint`, `--auto-pr`, and `--steps=close-only` so it can be composed by unattended automation (specifically `/builder:debt-pass`'s checkpoint-refresh step). Currently end-session has a hard human-approval gate on the checkpoint diff (step 5) and "suggest, don't auto-create" PR default (step 7) — both load-bearing for interactive use, but block headless composition. Trigger: after `/builder:debt-pass` lands and accumulates evidence on what end-session needs to expose. Blocks: migrating debt-pass step 7 from local fill rules to delegation. References: `PLANS/auto-debt-pass.md` §1 step 7, `.claude/commands/builder/end-session.md`. Status: open~~ → done 2026-05-01 (end-session step 1b skips work-summary prompt when `work-summary` is pre-filled; `--auto-pr` flag added to step 7; debt-pass step 9 now writes note as `in-progress`, step 11 delegates to `end-session --auto-pr`, old commit/push/PR steps removed)

- ~~**Migrate automation auth from API key to subscription OAuth** — swap to `claude_code_oauth_token`. Trigger: before first GHA run. References: `.github/workflows/debt-pass.yml`, `anthropics/claude-code-action@v1` README. Status: open~~ → done 2026-05-01 (workflow already using `claude_code_oauth_token: ${{ secrets.CLAUDE_CODE_OAUTH_TOKEN }}`; migration completed in a prior pass)

- ~~**Upgrade `actions/checkout@v4` → `@v5`** — Node.js 20 deprecated 2026-06-02. One-line bump in `.github/workflows/debt-pass.yml`. Trigger: before 2026-06-02. References: `.github/workflows/debt-pass.yml`. Status: open~~ → done 2026-05-01 (bumped to `actions/checkout@v5`)

- id: bl-debt-pass-graceful-budget | **Graceful turn-budget exhaustion for `/builder:debt-pass`** — currently when the agent hits `--max-turns`, it fails hard with `error_max_turns` and all in-progress edits are lost (no commit, no PR). Bad outcome for a long-running unattended pass that did most of the work. Add a "near-budget" rule to the spec: when the agent estimates it has fewer than ~5 turns remaining, stop the discharge loop, jump to step 9, and commit/push/PR what's done with a `## Partial completion` section in the PR body listing entries discharged vs. entries left open. Reviewer sees real progress instead of a wasted run. Implementation note: agent doesn't natively know the budget — the spec needs to teach it to count its own turns or derive remaining budget from `--max-turns` (passed via env or hard-coded as a constant in the spec).
  Trigger: after the parallel-tool-use optimization above, since that may make the issue rare enough to ignore.
  References: `.claude/commands/builder/debt-pass.md`, [Layer 3 fifth run](https://github.com/MgCohen/Card-Framework/actions/runs/25193791092) (lost ~$1.49 of work).
  Status: open

- id: bl-spatial-rule-attachment | **Spatial rule attachment surface for zones** — when a game (Stacklands-style) needs spatial coordinates as rules data rather than presentation layout, the framework must commit to where spatial info attaches. The current zone contents shape is a flat `IReadOnlyList<Card>` — there is no pre-defined hook. When forced by a real game, decide between: extending `ZoneContentsState` with per-entry structure (slot/coord field), introducing a parallel `SpatialState` slice keyed by card, or per-card position variable.
  Trigger: first spatial-rules game (Stacklands-like) reaches design-pass.
  References: `Product/features/zones/models/01-zone-model.md` §Deferred, `Product/features/zones/decisions/0001-zone-shape.md`, session-20260501-1602-zone-model-internal-opens.
  Status: open

- id: bl-card-location-index | **Reverse-lookup index for card → zone** — derived aggregate slice (e.g., `CardLocationIndex` keyed by `CardRef → Zone`) that answers "what zone is this card in?" in O(1) instead of scanning all `ZoneContentsState` slices. Maintained atomically by the same mutators that write `ZoneContentsState`. Originally drafted into the cards Model during session-20260501-1602 but removed after audit found no current consumer in any Model.
  Trigger: first reverse-lookup consumer surfaces (e.g., a Stacklands-style game's spatial query, a presentation feature that needs to render card-to-zone arrows, or any rule that needs frequent reverse lookup at scale).
  References: `Product/features/cards/models/01-card-model.md`, `Product/features/zones/models/01-zone-model.md` §Read API, session-20260501-1602-zone-model-internal-opens.
  Status: open

- ~~id: bl-end-session-unsaved-body | **`/builder:end-session` unsaved-body check** — end-session must detect when a session sketched or approved a Model body / interface / contract / ADR-shape that was *not* written to a canonical file (Model doc, ADR, feature note) and either prompt to save it, refuse to close, or surface it as deferred-with-justification. Trigger: next end-session skill iteration; or next session that exhibits the same leak. References: `Product/sessions/session-15.md`, errata commit `89c161e`, `.claude/commands/builder/end-session.md` step 2 (hygiene). Status: open~~ → done 2026-05-01 (added step 2a to end-session: AI reads conversation history, classifies each artifact as Accepted/Not accepted/Uncertain, checks Accepted ones against Product/ files, renders a review table for user confirmation, then writes any Accepted + not-in-file artifacts before close proceeds; skipped in --auto-pr headless mode)

- ~~id: bl-reference-conventions | **Reference conventions doc** — no consistent convention exists for how docs reference each other. Four styles are in use: bare absolute paths (`features/x/y.md`), relative paths (`../ledgers/y.md`), markdown links with URL-encoded filenames, and `builds-on:` with inconsistent root assumptions. This causes `/builder:lint` to need complex multi-pattern scanning and produces false negatives when new patterns appear. A `_workflow/REFERENCE-CONVENTIONS.md` doc should define: (1) the canonical path format for `builds-on:` entries, (2) whether relative paths are allowed in markdown links, (3) whether prose/code-block path mentions are expected to resolve, and (4) what URL encoding rules apply. Once defined, the linter skill can be simplified to enforce one pattern. Trigger: before next `/builder:lint` skill iteration, or when a second reference-style inconsistency appears in the linter output. References: `.claude/skills/builder/lint/SKILL.md`, `Product/ledgers/ALIGNMENT-DEBT.md` (entries ad-20260501-027 through 032 are all reference-style violations). Status: open~~ → done 2026-05-01 (convention written at `Product/_workflow/REFERENCE-CONVENTIONS.md`: one rule — `Product/`-rooted absolute paths everywhere, no relative paths, no URL encoding, ASCII filenames; lint skill rewritten with S3 (no relative paths) and S4 (filename hygiene) rules; legacy `.claude/commands/builder/lint.md` deleted in favor of single `.claude/skills/builder/lint/SKILL.md` source of truth; migrated `FEATURE-MAP.md`, `04-gamestate-slices.md`, `0002-gamerules-zero-state.md`, `_workflow/EMOJI-LEGEND.md` `builds-on:`; evals rewritten to test S3 and S2 against fixtures)

- id: bl-tier-2-3-commands | **Other Tier 2/3 commands and skills** — see `ProductBuilder/AI-FIRST-AUTOMATION.md` §8 sequencing for the ordered list and triggers. (`/builder:debt-pass` was promoted out of this umbrella above.)
  Trigger: per item in §8.
  References: `ProductBuilder/AI-FIRST-AUTOMATION.md` §8.
  Status: open

- id: bl-definition-editor | **Definition Editor** — authoring tool for cards, zones, game templates, effects, visuals.
  Trigger: `authoring` feature reaches `settled`.
  References: ARCHITECTURE §"Authoring", FEATURE-MAP `authoring` (considered).
  Status: open

- id: bl-simulation-debugger | **Simulation Debugger** — inspector for state, history, pending tasks, triggers, decisions.
  Trigger: first feature reaches `implemented` (debugger needs a real running simulation to inspect).
  References: ARCHITECTURE §"Tooling".
  Status: open

- id: bl-scenario-runner | **Scenario Runner** — load a known state and exercise a command/effect for fast iteration.
  Trigger: first feature reaches `implemented`.
  References: ARCHITECTURE §"Tooling".
  Status: open

- id: bl-replay-viewer | **Replay Viewer** — step through history and inspect state transitions.
  Trigger: history pipeline (`engine/03-snapshot-pipeline`) reaches `implemented`.
  References: features/engine/models/03-snapshot-pipeline.md, ARCHITECTURE §"Tooling".
  Status: open

- id: bl-rule-trace | **Rule Trace** — explain why an action was legal/illegal and which effects modified it.
  Trigger: first feature reaches `implemented`.
  References: ARCHITECTURE §"Tooling".
  Status: open

- id: bl-validation-reports | **Validation Reports** — detect broken card definitions, missing assets, bad IDs, invalid references.
  Trigger: `authoring` feature reaches `draft` (needs a definition surface to validate).
  References: FEATURE-MAP `authoring` (considered).
  Status: open

- id: bl-versioned-definitions | **Versioned Definitions** — definition versioning/hashing for client-server agreement.
  Trigger: `networking` feature begins; or save/load arrives, whichever first.
  References: FEATURE-MAP `networking` (considered).
  Status: open

- id: bl-presets-package | **Presets package** — preset rule templates (TCG, deckbuilder, spatial sim) as compositions of framework primitives.
  Trigger: post-v1, after at least 2 features reach `implemented` and the framework primitive surface is stable.
  References: ARCHITECTURE §"Preset Direction", RECOMMENDATIONS §"Recommendation 7".
  Status: open

## Done

- ~~**Optimize `/builder:debt-pass` turn count via parallel tool use** — Layer 3 fifth dispatch on 2026-04-30 hit `--max-turns 30` after 31 turns ($1.49) discharging 5 entries. Rewrite step 4 (discharge loop) and step 5 (hygiene scan) to direct the agent toward batched/parallel tool calls. Target: discharge 5 entries in ≤ 20 turns instead of 30+, halving cost per real fire.~~ → done 2026-05-01 (spec rewritten with batched bash for term-replace/grep/git, parallel reads, top-level performance directive; baseline Fire-B was 45 turns / $1.89, post-rewrite Fire-C will measure the actual delta).
