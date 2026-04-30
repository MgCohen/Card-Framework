---
type: model
status: draft
last-updated: 2026-04-29
builds-on: [PRODUCT-BUILDER-PROPOSAL.md]
tags: [#workflow, #simulation]
---

# Flow Simulation — Replaying Past Sessions Under the New Workflow

**Purpose.** Walk session-by-session through what we *actually* did and re-cast it as it would have unfolded under the PRODUCT-BUILDER-PROPOSAL. The goal is to stress-test the workflow against history we already know — does it absorb the moves we made, or does it pinch?

**Method.** One section per session, drawn from PR titles, commits, and the docs that landed. For each: the entry context, the branches taken, the artifacts created, and how each maps onto the proposed structure. Where the proposed workflow would have produced a *different* shape, that's called out.

---

## Session 1 — Initial framework wave (commits `0772b35`…`5c07eec`)

**Entry context.** Empty repo. Goal: lay down the design surface from scratch.

**What actually happened.** A bulk dump of docs 00–20 landed across two or three sittings: vision, archetypes, capability map, architecture, scaffold spike, roadmap, open-questions, recommendations, then core-model + glossary + entity/instance + zone decisions + zone model + deckmech reference + action flow decisions + action request/command/history + action terminology + action definition proposal + flow snippets + handler mutations + evaluation loop.

**Under the new workflow.**

- The orientation block (vision, archetypes, architecture, roadmap, open-questions, recommendations) lands in `general/`:
  - `general/VISION.md`, `general/SCOPE.md`, `general/ARCHITECTURE.md`, `general/ROADMAP.md`, `ledgers/OPEN-QUESTIONS.md`, `general/RECOMMENDATIONS.md`, `ledgers/GLOSSARY.md`. Candidate features go to `ledgers/FEATURE-MAP.md` as `considered` rows.
- The scaffold-fit investigation (doc 04) is recognized as a **spike** — pre-design exploration of an external package. Lands as `spikes/scaffold-state-fit.md` with status `active` → `concluded`.
- The core-model docs (10, 11, 12) land as **Models** in feature folders:
  - `features/entities/models/01-definition-instance-model.md` (model)
  - `features/zones/models/01-zone-model.md` (model, with provenance from old doc 11 folded below the divider)
- The action-track explosion (docs 13–20) is the messy part. Under the new workflow:
  - Doc 13 (deckmech reference) is a **spike** in `spikes/deckmech-command-pipeline.md`.
  - Doc 14 (action flow decisions) collapses: each major decision becomes an ADR in `features/actions/decisions/`. The discussion narrative folds into the relevant Model's provenance section.
  - Docs 15, 17, 18, 19, 20 collapse into roughly two Models — `features/actions/models/01-command-pipeline.md` and `features/engine/models/01-evaluation-loop.md` — each with options/rationale/consequences below the divider.
  - Doc 16 (action terminology discussion) doesn't get a doc of its own; it becomes glossary entries + a session-note ephemeral thread, since terminology debates don't survive as durable artifacts.

**Session note (~50 lines, two parts).** Active question in: "what's the framework actually shaped like?" Active question out: "is the action pipeline coherent end-to-end?" Decisions settled: ~6 ADRs covering envelope, dispatch, handler return shape, etc. Followups: ~10 entries to OPEN-QUESTIONS.

**Where the workflow pinches.** Session 1 was a flood — too much to cleanly atomize on the fly. The new workflow *would* have forced atomization, and the result would be more navigable. But there's a real risk that early sessions, where the shape is still molten, get over-formalized. **Mitigation:** the Spike doc type is the relief valve — when in doubt, write a spike, graduate later.

---

## Session 2 — Review last session (PR #1, `claude/review-last-session-4YVrT`)

**Entry context.** Session-1 dump landed; need to audit and stitch.

**What actually happened.** Updated docs, session-checkpoint refresh, fixed return-value and envelope grouping in doc 18 (alignment with doc 20).

**Under the new workflow.** This is a recognizable pattern: an alignment-pass session (§5.5). Entry context = "discharge inconsistencies between docs 18 and 20." Work = mechanical edits. The session note records the trail; `ledgers/ALIGNMENT-DEBT.md` gets ticked off.

**Artifact-level mapping.** No new docs. Edits to `features/actions/models/01-command-pipeline.md` and `features/engine/models/01-evaluation-loop.md`. Two debt entries closed.

**Verdict.** Workflow absorbs this cleanly. The §5.5 pattern was *invented* for sessions like this.

---

## Session 3 — Terminology pass (PR #2, `claude/review-checkpoint-tasks-B6I5t`)

**Entry context.** OriginRequest/PlayerRequest naming felt wrong; terminology review surfaced glossary gaps.

**What actually happened.** Renamed OriginRequest → OriginIntent, PlayerRequest → PlayerIntent across all docs. Glossary gaps audited.

**Under the new workflow.**

- The rename is a **decision**, so it gets an ADR: `features/actions/decisions/0002-intent-vocabulary.md` (or a top-level `decisions/`-style ADR if we add one — see §1 of MIGRATION-GUIDE for that question).
- Glossary changelog gets an entry: `2026-XX-XX: Renamed OriginRequest→OriginIntent, PlayerRequest→PlayerIntent. ADR-NNNN.`
- Session note records the sweep across affected docs.
- Multiple `ledgers/ALIGNMENT-DEBT.md` entries created and closed in the same session (rename-and-reconcile is one pass).

**Where the workflow helps.** Today, the rename is visible only in PR titles. Under the new workflow, the ADR is searchable (`features/actions/decisions/`) and the glossary changelog gives a single read-out of vocabulary churn.

---

## Session 4 — Composite-action pattern (PR #3, `claude/review-checkpoint-planning-nTlZT`)

**Entry context.** Doc 19 had a scope-helper sketch that felt off.

**What actually happened.** Added MoveCard composite section to doc 19 with before/after snippets. Then revised doc 19 §5: composite-operation pattern replaces scope helper.

**Under the new workflow.**

- The composite/scope-helper choice is a real fork → ADR: `features/actions/decisions/NNNN-composite-vs-scope-helper.md`.
- The model `features/actions/models/01-command-pipeline.md` (or a sibling) gets edited to carry the new shape; "Options considered" below the divider gets the entry.
- Session note: branch from "doc 19 review" → "scope helper feels wrong" → "composite pattern" → ADR landed → model updated.

**Verdict.** This is the workflow's sweet spot — a single decision with two siblings considered, settled in-session, ADR created. Clean.

---

## Session 5 — Trigger system + terminology cleanup (PR #4, `claude/review-checkpoint-EbFQF`)

**Entry context.** Trigger/reaction system needed shape; gateway/status/id terminology lagged behind PlayerIntent rename.

**What actually happened.** Renamed gateway/status/id types to match PlayerIntent terminology. Wrote doc 21 (trigger and reaction system design). Terminology cleanup "task A" applied across docs 02–20.

**Under the new workflow.**

- The trigger system gets its own feature folder: `features/triggers/`. First model: `features/triggers/models/01-trigger-fold.md`.
- Multiple ADRs land in `features/triggers/decisions/`: trigger-fold semantics, OnBefore/OnAfter shape, subscription model.
- Terminology cleanup is another alignment pass — debt entries against docs 02–20 (now scattered across `general/` orientation files + `features/actions/`, `features/engine/`).
- Glossary changelog gets a multi-line entry.

**Where the workflow shows strain.** A *naming* pass that touches 18 docs is a lot of churn no matter how it's organized. The workflow doesn't make the work smaller — but the alignment-debt ledger means we don't lose track of what was reconciled vs. what's still drifting.

---

## Session 6 — Naming alignment pass (PR #5, `claude/review-checkpoint-tasks-sm7Ic`)

**Entry context.** "Settled renames" still hadn't propagated everywhere.

**What actually happened.** Naming alignment pass: applied all settled renames across all research docs (commit `fd99468`).

**Under the new workflow.** Pure §5.5 alignment pass. Entry context = `ledgers/ALIGNMENT-DEBT.md` entries from terminology decisions. Work = mechanical sweep. Session note records which features were touched. Most debt entries close in one session.

**Pattern observed.** Terminology decisions create debt in O(N) docs. Sessions 3, 5, 6 are all variations on this. The workflow absorbs them but doesn't shrink them. **Possible automation hook** (see AI-FIRST doc): glossary-rename → automated grep + suggested replacements as a skill.

---

## Session 7 — Async / dispatch model (PR #8, `claude/review-checkpoint-questions-f7Wdz`)

**Entry context.** Async story was scattered; needed a canonical doc.

**What actually happened.** Added external package research (Scaffold States, Scaffold Entities) — pre-existing folder. Audited and updated open questions. Wrote doc 22 (async decisions and suspension). Then rewrote doc 22 to clarify Effect/GameAction/CompositeAction hierarchy. Locked async/dispatch model; updated checkpoint + open questions.

**Under the new workflow.**

- Scaffold States / Scaffold Entities deep-dives are **spikes** (prior-art surveys): `spikes/scaffold-states-deep-dive.md`, `spikes/scaffold-entities-deep-dive.md`. Status `concluded` (or `graduated` if they fed a Model).
  - The spike → Model graduation is a recognized transition in the workflow. Forward-link added when doc 22 lands.
- Doc 22 lands as a Model in a new feature folder: `features/effects/models/01-async-dispatch.md` (or `features/engine/models/02-async-dispatch.md` — see MIGRATION for placement).
- The rewrite-mid-session is interesting: under the new workflow, the first version doesn't get a separate file; it's a draft that converges. Session-note ephemeral thread records the pivot ("first cut had three layers; collapsed to two").
- ADRs: probably 2–3 — async-layer hierarchy, suspension model, dispatch-path split.

**Verdict.** The spike → model → ADR cascade is exactly what §4.1 was designed for. Session 7 would be cleaner under the new workflow — the prior-art survey would be visibly distinct from the design commit.

---

## Session 8 — Replacement/prevention/ongoing + cross-doc audit (PR #9/#10)

**Entry context.** Replacement effects, prevention, continuous/ongoing effects all unspecified. Snippets across docs were drifting.

**What actually happened.** Doc 23 (replacement/prevention), doc 24 (continuous/ongoing). Cross-doc audit. Snippet fixes.

**Under the new workflow.**

- Doc 23 → Model in `features/effects/models/02-replacement-prevention.md`.
- Doc 24 → Model in `features/effects/models/03-ongoing-effects.md`.
- Snippet drift = alignment debt. Cross-doc audit closes it.
- ADRs: priority model for replacement, attachment-entity choice, turn-signal lifecycle.

**Where the workflow helps.** Today, finding "what's the replacement-effect priority story?" means reading doc 23 plus checking doc 22 plus tracking down whatever the latest commit changed. Under the new workflow, `features/effects/decisions/` has the ADR. Done.

---

## Session 9 — GameRules + Pile Counter sample (PR #11, `claude/review-checkpoint-iUozR`)

**Entry context.** GameRules shape was placeholder; needed a real contract. And the design needed a concrete stress-test.

**What actually happened.** Doc 25 (GameRules contract). Doc 26 (Pile Counter sample game). Locked Pile Counter setup decisions. Locked Pile Counter puzzle source. Docs 26 + 27 (presentation loop). Aligned doc 25 with 26 + 27.

**Under the new workflow.**

- Doc 25 → Model in `features/engine/models/02-gamerules-contract.md` (or its own feature folder if `gamerules` warrants one).
- Doc 26 → **Sample** in `support/samples/pile-counter.md`. Two-layer (Subject = Pile Counter game rules; Projection = how it maps onto framework concepts).
  - The "lock" commits map to status transition `draft` → `locked`. Per §4.4, the four-part lock-bar is checked: subject finalized ✓, projection uses settled abstractions ✓ (mostly — see below), no open alignment debt ✓, stable ≥ 1 session ✓ (after the alignment pass).
- Doc 27 → Model in `features/presentation/models/01-presentation-loop.md`.
- Aligning doc 25 with 26 + 27 = alignment-debt entries created when 26/27 surfaced gaps in 25; closed in same session.

**Where this stresses the workflow.** The Sample's `locked` bar requires that the projection uses only `settled` abstractions. In session 9, GameRules was *just* finalized — projection-onto-GameRules counted as "settled" only because we said so. Under the workflow this would have been a `draft` sample for at least one more session (§4.4 condition 4: stable ≥ 1 session). The workflow would have *slowed us down here*, by one session. Worth it? Probably yes — the lock is a load-bearing claim.

---

## Session 10 — ActionAvailability snapshot (PR #12, `claude/doc-28-action-availability`)

**Entry context.** UI needed a clear snapshot shape; existing docs only sketched it.

**What actually happened.** Doc 28 (ActionAvailability snapshot shape).

**Under the new workflow.**

- Doc 28 → Model in `features/actions/models/02-availability-snapshot.md` (or `features/presentation/models/02-availability-snapshot.md`, depending on how we decide the actions-vs-presentation boundary in MIGRATION).
- ADR: typed-template-vs-generic-template; eligibility filter shape.

**Verdict.** Single-purpose session, single-doc landing. Workflow trivially absorbs it.

---

## Session 11 — Snapshot production pipeline (PR #13, `claude/review-checkpoint-ZAqFg`)

**Entry context.** Doc 28 left "who writes the snapshot, when?" open. Active question coming in.

**What actually happened.** Doc 29 (snapshot production pipeline). Service-driven publication; typed templates; on-demand validation; services-injected GameRules. Doc-alignment edits to 25, 26, 27, 28.

**Under the new workflow.**

- Doc 29 → Model in `features/engine/models/03-snapshot-pipeline.md` (or split between `features/engine/` and `features/actions/`).
- ADRs: service-driven publication, typed ActionTemplate, on-demand validation, GameRules-as-abstract-class-with-injected-services.
- The doc-alignment edits to 25, 26, 27, 28 = ledger entries opened in `ledgers/ALIGNMENT-DEBT.md` (one per affected doc) when doc 29 lands, closed in same session.
- The Pile Counter sample (`support/samples/pile-counter.md`) gets re-checked against the new GameRules shape. If the sample was `locked`, this triggers the §4.4 "design changes that affect the locked sample" path — explicit ledger entry naming the sample as target, deliberate decision to re-lock.

**Where the workflow helps most.** Doc 29 cascades. Today, "what did doc 29 change in doc 25?" is hidden in the merge commit. Under the new workflow, the alignment-debt ledger names every affected artifact, and resolution is a tracked checkbox.

---

## Session 12 — Workflow proposal (current session, PR #14, `claude/analyze-workflow-structure-0w0mz`)

**Entry context.** Pattern of sessions had stabilized; no shared shape for the work was written down.

**What actually happened.** Multiple iterations on `PRODUCT-BUILDER-PROPOSAL.md`. Then the user asked to move it to `ProductBuilder/` and expand with this doc, a migration guide, and an AI-first plan.

**Under the new workflow.** This is meta — the proposal *defines* the workflow, so it can't fully obey it yet. But forward-going:

- This very document, `ProductBuilder/FLOW-SIMULATION.md`, is itself a Model (or arguably an analysis spike). Front-matter says `type: model`.
- `ProductBuilder/MIGRATION-GUIDE.md` is a one-shot artifact: a plan for the migration. Not quite any of the 8 doc types — closer to a Plan than anything else.
- `ProductBuilder/AI-FIRST-AUTOMATION.md` is a forward-looking spec. Closest to a Spike (premise: "what would AI-first look like?") that may graduate into multiple Models as items get built.

**Note.** `ProductBuilder/` is the staging area for the workflow design itself. After migration, `PRODUCT-BUILDER-PROPOSAL.md` graduates to `Product/_workflow/PRODUCT-BUILDER.md`. The other docs (`MIGRATION-GUIDE.md`, `FLOW-SIMULATION.md`, `AI-FIRST-AUTOMATION.md`, `PRODUCT-BUILDER-GAPS.md`) stay in place — they're living artifacts that may evolve when the workflow gets exercised for real. Nothing else graduates, nothing retires.

---

## Patterns observed across all sessions

1. **Alignment passes are a recurring shape.** Sessions 2, 6, and parts of 3, 5, 8, 9, 11 are alignment-pass-shaped. The §5.5 callout was earned.
2. **Spike → Model → ADR is the natural cascade.** Session 7 (Scaffold deep-dive → async model → async ADRs) is the canonical example. Earlier sessions did spike-shaped work without naming it.
3. **Terminology decisions cascade.** Sessions 3, 5, 6 are all rename propagation. The proposal handles this, but it doesn't shrink the work — automation could (see AI-FIRST).
4. **Sample locking is load-bearing.** Session 9 locked Pile Counter; session 11 cascaded changes through it. The alignment-debt-against-locked-sample mechanism would have caught the cascade explicitly rather than implicitly.
5. **The most chaotic session (session 1) is the hardest to retrofit.** Bulk-dump sessions don't decompose neatly. The Spike doc type is the relief valve, but in practice early sessions may stay messy.
6. **The branching-session frame (§5) matches reality.** None of these sessions were linear. Every session had a primary thread plus 1–3 detours. The "session note as trail, not narrative" framing fits cleanly.

## What the workflow would have prevented

- Doc-number drift: today, "doc 28" means whatever 28 currently is. Under per-feature counters, `features/actions/models/02` is stable — its position in the pipeline doesn't shift when an unrelated feature adds a doc.
- Lost decision provenance: ADRs would have made every rename, every async-layer choice, every snapshot-publication choice individually searchable.
- Implicit alignment debt: every "align doc X with doc Y" pass would have been visible as ledger entries before being worked.
- Sample-projection rot: the locked-bar prevents a sample from claiming coverage it doesn't have.

## What the workflow would NOT have changed

- The actual design discoveries. Session 9's GameRules shape would have been the same shape.
- The number of sessions. Maybe ±1 from the sample-locking cooldown rule, but not materially.
- The total volume of writing. The same content, organized.

The workflow is a navigation/provenance system. It's not a productivity system.

---

# Ideal Flow — A Reconstructed 12-Session Run

A fake replay. Same problem space, same milestones, but each session is shaped *as if* the workflow had been in place from day one. No thrashing across sessions, no rename storms, no implicit debt. Each entry records: files added, actions taken, key points. Format is compact — these are session-note shaped, not narrative.

The premise: 12 sessions to land the same design surface as the historical run, but cleanly. ADRs land alongside Models. Spikes get named. Alignment debt is opened and closed deliberately. Samples lock only when stable.

---

## Session 01 — Project orientation

**Files added.**
- `general/VISION.md`
- `general/SCOPE.md`
- `general/ROADMAP.md`
- `ledgers/GLOSSARY.md` (skeleton)
- `ledgers/OPEN-QUESTIONS.md` (seeded)
- `general/RECOMMENDATIONS.md` (placeholder)
- `sessions/session-01.md`

**Actions taken.**
- Frame the project: card framework, archetype-agnostic, scaffold-based.
- Enumerate the seven features that will live under `features/` (entities, zones, actions, triggers, effects, engine, presentation). Mark this as a *naming-only* commitment — folders created empty, no models yet.
- Seed `OPEN-QUESTIONS.md` with the durable-from-day-one ones: state ownership, async story, multiplayer.

**Key points.**
- No feature-level design yet. Pure orientation.
- Glossary is empty-but-present so the changelog has somewhere to live from session 02 onward.
- Active question going out: "what's the architectural backbone — engine + store ownership?"

---

## Session 02 — Architecture spike + entity model

**Files added.**
- `spikes/scaffold-state-fit.md` (status: `active` → `concluded`)
- `general/ARCHITECTURE.md`
- `features/entities/models/01-definition-instance-model.md` (Model, draft)
- `features/entities/decisions/0001-definition-vs-instance-split.md`
- `sessions/session-02.md`

**Actions taken.**
- Spike: read Scaffold State + Scaffold Entities packages, project them against the architecture question.
- Spike findings inform `general/ARCHITECTURE.md`: redux-like state, mutator pattern, store as reactive layer.
- First feature Model: entity definition vs. runtime instance.
- One ADR: definition/instance split (single-class? two? split won — searchable.)

**Key points.**
- Spike → Model graduation is recorded explicitly. `scaffold-state-fit.md` gets forward-link to architecture doc and entity model.
- Entities is the lowest dependency, written first. Zones depend on entities; everything else depends on both.
- Active question going out: "zones — what's the membership model?"

---

## Session 03 — Zones model

**Files added.**
- `features/zones/models/01-zone-model.md` (Model, settled)
- `features/zones/decisions/0001-zone-as-collection.md`
- `features/zones/decisions/0002-zone-membership-mutator.md`
- `features/zones/decisions/0003-stack-vs-bag-zone-typing.md`
- `sessions/session-03.md`

**Actions taken.**
- Zones model: top half describes the system; provenance below the divider records three real forks and their rejected alternatives.
- Three ADRs land in same session — one per atomic decision.
- Glossary patched with `Zone`, `ZoneMembership`, `Stack`, `Bag`. Changelog entry.

**Key points.**
- All three zone decisions made in one sitting → all three ADRs in one session. No one-decision-at-a-time tax.
- No alignment debt opened (no upstream models changed).
- Active question going out: "what does the action pipeline look like end-to-end?"

---

## Session 04 — Action pipeline + engine evaluation loop

**Files added.**
- `spikes/deckmech-command-pipeline.md` (status: `concluded`)
- `features/actions/models/01-command-pipeline.md` (Model)
- `features/engine/models/01-evaluation-loop.md` (Model)
- `features/actions/decisions/0001-command-envelope-shape.md`
- `features/actions/decisions/0002-handler-validate-authoritative.md`
- `features/actions/decisions/0003-onincomingcommand-flow-only.md`
- `features/engine/decisions/0001-evaluator-pull-loop.md`
- `sessions/session-04.md`

**Actions taken.**
- Spike on deckmech (external reference) for prior art on command pipelines.
- Two sibling Models: action pipeline + engine evaluation loop. Cross-link explicitly via `builds-on:` front matter.
- Four ADRs land for the load-bearing forks.
- `OPEN-QUESTIONS.md` updated: validation timing, re-validation at dequeue.

**Key points.**
- This is the historical session 1's bulk-dump, atomized. Two Models instead of seven docs; four ADRs instead of buried decisions.
- Examples sections in both Models walk through *one* end-to-end command flow.
- Active question going out: "we're using ad-hoc terminology — does the vocabulary hold up?"

---

## Session 05 — Terminology pass

**Files added.**
- `features/actions/decisions/0004-intent-vocabulary.md` (the rename: Request → Intent across the actions stack)
- `sessions/session-05.md`

**Files modified (alignment-debt closure).**
- `features/actions/models/01-command-pipeline.md`
- `features/engine/models/01-evaluation-loop.md`
- `features/zones/models/01-zone-model.md` (one stray reference)
- `ledgers/GLOSSARY.md` (changelog entry)

**Actions taken.**
- ADR proposed and accepted: rename `OriginRequest`/`PlayerRequest` → `OriginIntent`/`PlayerIntent`. One file, one decision.
- `ALIGNMENT-DEBT.md` gets three entries (one per affected Model) → closed in same session.
- Glossary changelog: one entry covering the rename batch.

**Key points.**
- Alignment-pass-shaped session (§5.5 of workflow). Mechanical sweep; trail captured.
- The historical sessions 3 + 5 + 6 collapse into this single session because the cascade is bounded — there are only three Models so far, not 28 docs.
- Active question going out: "trigger system shape."

---

## Session 06 — Trigger system

**Files added.**
- `features/triggers/models/01-trigger-fold.md` (Model)
- `features/triggers/decisions/0001-fold-semantics.md`
- `features/triggers/decisions/0002-onbefore-onafter-split.md`
- `features/triggers/decisions/0003-priority-by-source.md`
- `sessions/session-06.md`

**Actions taken.**
- Trigger fold Model. Stress-tested against a synthetic scenario in the Examples section.
- Three ADRs.
- Subscription lifecycle deferred — added to `OPEN-QUESTIONS.md` as durable-not-yet-answerable, blocked on game-lifecycle design.

**Key points.**
- Triggers separated from effects — confirmed during the session that the dependency points triggers → effects is shallow enough to keep them in distinct feature folders.
- One ephemeral question (in session note scratch): "do we need a trigger registry or can effects just declare?" — answered in-session, not promoted.
- Active question going out: "async/effect dispatch — Hearthstone-class games need it."

---

## Session 07 — Async / effect dispatch

**Files added.**
- `spikes/sabberstone-architecture.md` (status: `graduated`)
- `spikes/fireplace-architecture.md` (status: `concluded`)
- `features/effects/models/01-async-dispatch.md` (Model)
- `features/effects/decisions/0001-async-layer-hierarchy.md`
- `features/effects/decisions/0002-two-dispatch-paths.md`
- `features/effects/decisions/0003-suspension-via-handle.md`
- `sessions/session-07.md`

**Actions taken.**
- Two prior-art spikes for context (SabberStone task/option flow, Fireplace JSON option indexing).
- Async/dispatch Model. Provenance section records the rejected three-layer hierarchy and why it collapsed to two.
- Three ADRs.
- `ALIGNMENT-DEBT.md`: one entry against `features/actions/models/01` (composite-action mention now formalized).

**Key points.**
- Spike → Model → ADR cascade in one session. Forward-link from each spike to the Model.
- The "first cut had three layers, collapsed to two" pivot mid-session lives in the session note ephemeral thread, not as a separate doc.
- Active question going out: "replacement/prevention effects — separate Model or part of async?"

---

## Session 08 — Replacement, prevention, ongoing

**Files added.**
- `features/effects/models/02-replacement-prevention.md` (Model)
- `features/effects/models/03-ongoing-effects.md` (Model)
- `features/effects/decisions/0004-trigger-pipeline-priority.md`
- `features/effects/decisions/0005-ongoing-as-attachment-entity.md`
- `sessions/session-08.md`

**Files modified (debt closure).**
- `features/effects/models/01-async-dispatch.md` (debt entry from session 07 closed — composite-action mention reconciled)
- `features/triggers/models/01-trigger-fold.md` (replacement effects clarified relationship to fold)

**Actions taken.**
- Two siblings under `features/effects/`. Replacement/prevention as Model 02; ongoing as Model 03.
- Two ADRs.
- Cross-doc audit closes the session-07 alignment-debt entry plus opens-and-closes one new entry against the trigger Model.

**Key points.**
- Effects subsystem complete: 3 Models, 5 ADRs across sessions 07–08.
- Examples in each Model now stress-test against scenarios (replacement: damage prevention; ongoing: attachment auras).
- Active question going out: "GameRules — what does the user-facing contract look like?"

---

## Session 09 — GameRules contract

**Files added.**
- `features/engine/models/02-gamerules-contract.md` (Model)
- `features/engine/decisions/0002-gamerules-as-class-with-services.md`
- `features/engine/decisions/0003-gamerules-zero-state.md`
- `features/engine/decisions/0004-onqueuedrained-trigger.md`
- `sessions/session-09.md`

**Files modified (debt closure).**
- `features/actions/models/01-command-pipeline.md` (validation-in-handler reference now points at GameRules contract)

**Actions taken.**
- GameRules Model. Top half describes the class shape and method set; provenance records why interface-then-class, why services injected.
- Three ADRs.
- One alignment-debt entry against the action pipeline closed (validation responsibility line drawn cleanly).

**Key points.**
- GameRules locked. Services layer (`IAvailableActions`, `IIntakeQueue`, `IGameStateProvider`, `IHandlerRegistry`) named but not yet shaped.
- Active question going out: "we need a concrete sample to stress-test the design."

---

## Session 10 — Pile Counter sample (draft)

**Files added.**
- `support/samples/pile-counter.md` (Sample, status: `draft`)
- `features/presentation/models/01-presentation-loop.md` (Model)
- `features/presentation/decisions/0001-pull-not-push.md`
- `sessions/session-10.md`

**Files modified.**
- `features/engine/models/02-gamerules-contract.md` (Examples section now references pile-counter.md)
- `ledgers/GLOSSARY.md` (terms surfaced by the sample: `PuzzleSource`, `PileTarget`)

**Actions taken.**
- Pile Counter written as a Sample: Subject (the puzzle game), Why-this-sample (floor case for solo / no async / no triggers), Projection onto framework abstractions, What-this-does-NOT-exercise.
- Presentation loop Model written alongside, since the sample needed it to project onto.
- Status stays `draft` — lock-bar requires stable ≥ 1 session, see workflow §4.4.

**Key points.**
- Sample is honest about what it doesn't exercise: no async, no triggers, no effects, no multiplayer.
- One ADR on presentation: pull (UI reads snapshot) vs. push (engine signals UI).
- Active question going out: "ActionAvailability snapshot — what shape, written by whom?"

---

## Session 11 — Pile Counter lock + ActionAvailability snapshot

**Files added.**
- `features/actions/models/02-availability-snapshot.md` (Model)
- `features/actions/decisions/0005-typed-action-template.md`
- `features/actions/decisions/0006-eligibility-as-template-filter.md`
- `sessions/session-11.md`

**Files modified.**
- `support/samples/pile-counter.md` (status: `draft` → `locked`)
- `features/presentation/models/01-presentation-loop.md` (snapshot consumer side filled in)

**Actions taken.**
- Re-check the four-part lock-bar against Pile Counter:
  1. Subject finalized? ✓
  2. Projection uses only `settled` abstractions? ✓ (entities, zones, actions, engine, presentation all settled at this point)
  3. No open alignment debt against the sample? ✓
  4. Stable ≥ 1 session since session 10? ✓
- Promote sample to `locked`.
- Snapshot Model written alongside. Two ADRs.

**Key points.**
- Sample lock is deliberate and verified, not implicit. The cooldown rule cost one session — paid willingly.
- Active question going out: "snapshot *production* — who writes it, when, on which signal?"

---

## Session 12 — Snapshot pipeline + sample relock cascade

**Files added.**
- `features/engine/models/03-snapshot-pipeline.md` (Model)
- `features/engine/decisions/0005-snapshot-in-state.md`
- `features/engine/decisions/0006-iavailableactions-single-writer.md`
- `features/actions/decisions/0007-on-demand-validation.md`
- `sessions/session-12.md`

**Files modified (debt cascade).**
- `features/engine/models/02-gamerules-contract.md` (services-injected shape now formalized via snapshot pipeline)
- `features/actions/models/02-availability-snapshot.md` (eager-vs-lazy framing replaced by on-demand)
- `features/presentation/models/01-presentation-loop.md` (consumer side aligned)
- `support/samples/pile-counter.md` (re-checked against new GameRules shape)

**Actions taken.**
- Snapshot pipeline Model. Three ADRs.
- `ALIGNMENT-DEBT.md` opens four entries (one per affected doc, per §5.7) → all four closed in the same session via mechanical alignment pass. Session note carries `debt: 4 opened` for the snapshot-pipeline Model edit.
- Pile Counter lock re-validated explicitly: per workflow §4.4, locked-sample-affecting changes trigger a deliberate decision. Outcome: re-lock with the new shape (Sample's Projection updated, status stays `locked`).

**Key points.**
- The most cascade-heavy session. Workflow handles it: every affected doc named, every reconciliation tracked, sample relock recorded as a deliberate decision rather than silent drift.
- Active question going out: "effect-flow doc — `IHandlerContext`, decision-id threading, await semantics."

---

## Footprint summary

After 12 ideal sessions:

- **`general/`** — 7 files (orientation + glossary + open-questions + alignment-debt) + templates.
- **`features/`** — 7 feature folders, ~10 Models, ~25 ADRs total.
- **`spikes/`** — 4 spikes (scaffold-state-fit, deckmech, sabberstone, fireplace), all `concluded` or `graduated`.
- **`support/samples/`** — 1 sample, `locked`.
- **`sessions/`** — 12 session notes, each ~50-line cap.

**Versus historical run:**

| | Historical | Ideal |
|---|---|---|
| Numbered docs | 28 | — (replaced by feature folders) |
| Models | (implicit, scattered) | ~10 |
| ADRs | (none — load-bearing decisions buried in mega-docs) | ~25 |
| Spikes | (named retroactively) | 4 |
| Sessions with rename cascades | 3 (sessions 3, 5, 6) | 1 (session 5) |
| Sessions with implicit alignment work | ~5 | 0 (all explicit via debt ledger) |

Same design lands. Half the rename-pass overhead. Every load-bearing decision searchable. Every cascade audited.

This is the visualization of the workflow's payoff: not faster, but legible.
