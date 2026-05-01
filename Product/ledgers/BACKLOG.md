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

- **Migrate automation auth from API key to subscription OAuth** — `/builder:debt-pass` GHA currently uses `anthropic_api_key: ${{ secrets.ANTHROPIC_API_KEY }}` (Console API spend). Audit whether the secret is even set in the repo (as of 2026-04-30, `gh secret list` returned empty), then swap to `claude_code_oauth_token: ${{ secrets.CLAUDE_CODE_OAUTH_TOKEN }}` so runs bill against the subscription instead of API credits. Mint the token via `claude setup-token`; set as a repo secret with `gh secret set`.
  Trigger: before the first GHA workflow_dispatch / Layer 3 validation run, otherwise auth fails or bills the wrong account.
  References: `.github/workflows/debt-pass.yml:47`, `anthropics/claude-code-action@v1` README.
  Status: open

- **Upgrade `actions/checkout@v4` → `@v5`** — surfaced by Layer 3 dispatch run on 2026-04-30: `actions/checkout@v4` runs on Node.js 20, which GitHub Actions deprecates as default on 2026-06-02 (~5 weeks out) and removes from runners on 2026-09-16. `@v5` runs on Node.js 24. One-line bump in `.github/workflows/debt-pass.yml`. Worth checking other workflows we might add later for the same pin.
  Trigger: before 2026-06-02 to avoid the forced-upgrade behavior; or sooner if a new workflow lands first.
  References: [github.blog/changelog/2025-09-19-deprecation-of-node-20](https://github.blog/changelog/2025-09-19-deprecation-of-node-20-on-github-actions-runners/), `.github/workflows/debt-pass.yml`.
  Status: open

- **Graceful turn-budget exhaustion for `/builder:debt-pass`** — currently when the agent hits `--max-turns`, it fails hard with `error_max_turns` and all in-progress edits are lost (no commit, no PR). Bad outcome for a long-running unattended pass that did most of the work. Add a "near-budget" rule to the spec: when the agent estimates it has fewer than ~5 turns remaining, stop the discharge loop, jump to step 9, and commit/push/PR what's done with a `## Partial completion` section in the PR body listing entries discharged vs. entries left open. Reviewer sees real progress instead of a wasted run. Implementation note: agent doesn't natively know the budget — the spec needs to teach it to count its own turns or derive remaining budget from `--max-turns` (passed via env or hard-coded as a constant in the spec).
  Trigger: after the parallel-tool-use optimization above, since that may make the issue rare enough to ignore.
  References: `.claude/commands/builder/debt-pass.md`, [Layer 3 fifth run](https://github.com/MgCohen/Card-Framework/actions/runs/25193791092) (lost ~$1.49 of work).
  Status: open

- **`/builder:end-session` unsaved-body check** — end-session must detect when a session sketched or approved a Model body / interface / contract / ADR-shape that was *not* written to a canonical file (Model doc, ADR, feature note) and either prompt to save it, refuse to close, or surface it as deferred-with-justification. Today end-session only checks debt + hygiene; sketched-but-unsaved bodies leak into prose-only memory (session note + checkpoint) and require errata commits later. Session 15 is the canonical example: the `ICommandAwaiter` final shape was settled by user verdict but never written into `04-effect-flow.md`; an errata commit (`89c161e`) was needed to capture mapper + gateway invariants the close pass missed. Realistic mechanism: a yes/no/where prompt during step 2 of end-session — "Did this session settle any new interface, contract, or Model body? If yes, where is it saved?" — refuse to close on yes-and-nowhere.
  Trigger: next end-session skill iteration; or next session that exhibits the same leak.
  References: `Product/sessions/session-15.md`, errata commit `89c161e`, `.claude/commands/builder/end-session.md` step 2 (hygiene).
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

- ~~**Optimize `/builder:debt-pass` turn count via parallel tool use** — Layer 3 fifth dispatch on 2026-04-30 hit `--max-turns 30` after 31 turns ($1.49) discharging 5 entries. Rewrite step 4 (discharge loop) and step 5 (hygiene scan) to direct the agent toward batched/parallel tool calls. Target: discharge 5 entries in ≤ 20 turns instead of 30+, halving cost per real fire.~~ → done 2026-05-01 (spec rewritten with batched bash for term-replace/grep/git, parallel reads, top-level performance directive; baseline Fire-B was 45 turns / $1.89, post-rewrite Fire-C will measure the actual delta).
