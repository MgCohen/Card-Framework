---
type: checkpoint
last-updated: 2026-04-30
tags: [#checkpoint]
---

# Session Checkpoint

**Last updated:** 2026-04-30 (session 13)
**Replaceable.** History lives in `sessions/`. Never append here.

---

## Where we stopped

- **Effect-flow Model landed.** `features/effects/models/04-effect-flow.md` (`status: settled`, builds-on `effects/01`, `engine/03`) pins: `Effect` as opaque per-instance abstract type; `IEffectScope` exposing Scaffold `IStateScope` + `Dispatch` + `Cancel` (no mutate); `IHandlerContext` exposing concrete Scaffold `Store` + `IAvailableActions` + `IDecisionResolver` (no `Mutate`, no `CurrentDecisionId`); writes via `Store.Execute<TPayload>(payload)` fan out to all mutators registered for that payload type; mutators are Scaffold `Mutator<TState, TPayload>`; `DecisionId` retired in favour of the action's `CommandId`; uniform boundary via `DecisionResponseCommand`; `ICommandHandler.Handle` returns `Task<TResult>`. Eight invariants in §10. **§6 (uniform boundary) carries an unresolved review**, see active question. (PR #17, session 13)
- **Bound to Scaffold State.** Read/write surfaces are not invented — they reference `com.scaffold.states` types directly (`IStateScope`, `IStoreScope`, `Store`, `Mutator<TState, TPayload>`). State is sliced; there is no single-record `GameState`.
- **Open-question struck.** "Effect-side service symmetry" resolved — link in `OPEN-QUESTIONS.md`.
- **12 alignment-debt entries open**: 6 from the new Model (pile-counter §9.4 / §9.5 / Q8, snapshot-pipeline §4 / §7, command-pipeline `Handle`), 6 in the "Scaffold State/Store types audit" cascade (engine/02, engine/03, both engine ADRs, sample, OPEN-QUESTIONS). Discharge before re-locking pile-counter.
- **Migration complete (sessions 11–12).** All 8 phases of `ProductBuilder/MIGRATION-GUIDE.md` landed; flat 28-doc `RESEARCH/` migrated into `Product/`. Legacy folder retired; pre-migration content in `RESEARCH_BACKUP/`.
- **Workflow accepted.** `_workflow/PRODUCT-BUILDER.md` is canonical (`status: accepted`); adoption is `decisions/0001-adopt-workflow.md`.
- **20 ADRs landed.** No further backfill — future ADRs are forward-going.
- **Pile Counter sample status `locked`** at end of session 12; this session opened debt against §9.4–9.5 and Q8, so re-lock is contingent on debt discharge.

---

## The active open question (singular)

**Alignment-debt discharge (forced by §5.8 threshold).** `ALIGNMENT-DEBT.md` carries 12 open entries — well past the 5-entry threshold that forces the next session into pass-mode (§5.5 alignment-debt discharge). The §6 boundary redesign is the architecturally interesting next step but is **deferred** until the ledger drains.

The 12 entries split into two batches: 6 from the new effect-flow Model edits (pile-counter §9.4 / §9.5 / Q8, snapshot-pipeline §4 / §7, command-pipeline `Handle` signature) and 6 in the "Scaffold State/Store types audit" cascade (engine/02, engine/03, both engine ADRs, sample-wide, OPEN-QUESTIONS). All mechanical text edits — no architectural decisions. After discharge: re-lock pile-counter, then resume with §6.

**Deferred next-after:** §6 boundary redesign in `04-effect-flow.md`. The `IIntentMapper`-branches-on-snapshot-scope shape was flagged wrong-batch by user but no counter-shape pinned yet — likely the routing concern belongs to a different layer than the mapper.

---

## Other still-open

Carried forward from pre-migration `ledgers/OPEN-QUESTIONS.md`. See that file for the canonical durable list; the items below are the ones the next few sessions are likely to touch.

- **`IGameStateProvider` shape** — pin at engine implementation.
- **`IIntakeQueue` shape** — pin at engine implementation.
- **`GameState` contract** — concrete slice catalog (turn, phase, decisions, zones, available actions) on top of Store. Entity slices use `EntityVariableState` via the `com.scaffold.entities.states` bridge.
- **Game lifecycle design** — session start, turn structure, phase transitions, end conditions. Prerequisite for trigger subscription lifecycle.
- **Trigger subscription lifecycle** — Register/Unregister timing relative to zone transitions. Depends on game lifecycle.
- **`EndGameCommand` semantics** — bleeds into session lifecycle.
- **Snapshot scope as state machine** — revisit with the effect-flow doc.

---

## Suggested next steps

The active question is design work (task #20). The other tracked items are workflow infrastructure to make future sessions cheaper, then forward-going design.

| # | Task | Notes |
|---|------|-------|
| **20-fu** | **Cascade debt discharge** | Active question (forced by §5.8 — 12 open entries, threshold 5). 12 entries in `ALIGNMENT-DEBT.md` covering pile-counter, snapshot-pipeline, command-pipeline, and the Scaffold-types cascade (engine/02, engine/03, ADRs, OPEN-QUESTIONS). Mechanical, none architectural. Then re-lock pile-counter. |
| **20-§6** | **Effect-flow §6 boundary redesign** | Deferred next-after. The `IIntentMapper`-branches-on-snapshot-scope shape in `04-effect-flow.md` §6 was flagged wrong-batch — user wants the routing concern moved out of the mapper. Settle the alternative shape, patch §6. |
| ~~20~~ | ~~Effect-flow canonical Model~~ | Landed in session 13 — `features/effects/models/04-effect-flow.md`. §6 still open per row above. |
| **WF-1** | **Tier 1 slash commands** | Per `ProductBuilder/AI-FIRST-AUTOMATION.md` §8 Tier 1: `/builder:start-session`, `/builder:new`, `/builder:end-session`, `/builder:promote-glossary`. Land under `.claude/commands/` per-project. Cheap leverage on every session. |
| 12 | **Game lifecycle design** | Best unblocked task after #20. Likely lives in a new feature folder (`features/lifecycle/`?) or in `engine/`; decide on session start. |
| 2 | **`GameState` contract** | Concrete slice catalog. Folds task #9 (`StateChange` revisit, unblocked by the bridge decision). |
| 7 | **`PLANS/` — first implementation slice** | Pull after #20/#12/#2 land and at least one feature is ready for build-gate consideration. |
| 15 | **MTG-strict SBA ordering** | Defer until a game needs it. Captured as `features/engine/decisions/0006-mtg-sba-known-difference.md`. |

---

## Artifacts to read on resume

| File | Why |
|------|-----|
| [_workflow/PRODUCT-BUILDER.md](_workflow/PRODUCT-BUILDER.md) | The workflow itself. Read this if you haven't lately. |
| [ledgers/FEATURE-MAP.md](ledgers/FEATURE-MAP.md) | Bird's-eye view of features, Models, ADRs. |
| [ledgers/ALIGNMENT-DEBT.md](ledgers/ALIGNMENT-DEBT.md) | 12 open entries from session 13 (over §5.8 threshold of 5). Direct round + Scaffold State/Store cascade. Next session is forced pass-mode. |
| [features/effects/models/04-effect-flow.md](features/effects/models/04-effect-flow.md) | New canonical effect-flow contract from session 13. Read §10 (invariants) for the headline pins. |
| [ledgers/OPEN-QUESTIONS.md](ledgers/OPEN-QUESTIONS.md) | Durable design questions ledger. |
| [features/effects/models/01-async-dispatch.md](features/effects/models/01-async-dispatch.md) | Layer hierarchy, two dispatch paths — feeds task #20. |
| [features/engine/models/03-snapshot-pipeline.md](features/engine/models/03-snapshot-pipeline.md) | Snapshot production; §7 has effect-side symmetry — feeds task #20. |
| [features/engine/models/02-gamerules-contract.md](features/engine/models/02-gamerules-contract.md) | `GameRules` interface — services injected, four methods. |
| [support/samples/pile-counter.md](support/samples/pile-counter.md) | Floor sample; rule sketched against the current shape. |

For the Tier 1 commands (WF-1):

| File | Why |
|------|-----|
| [../ProductBuilder/AI-FIRST-AUTOMATION.md](../ProductBuilder/AI-FIRST-AUTOMATION.md) | Catalog of slash commands and skills, with §3 detailed specs and §8 sequencing. |

---

## How to use this file

- **On milestone / session end:** Edit in place. Refresh date, "where we stopped," and "next steps."
- **Keep it short** — if it grows past two screens, trim. Detail belongs in `features/<feature>/` and `sessions/`.
- **Optional commit/PR line:** "Checkpoint: `Product/SESSION-CHECKPOINT.md`."
