---
type: checkpoint
last-updated: 2026-04-29
tags: [#checkpoint]
---

# Session Checkpoint

**Last updated:** 2026-04-29 (session 12)
**Replaceable.** History lives in `sessions/`. Never append here.

---

## Where we stopped

- **Migration complete.** All 8 phases of `ProductBuilder/MIGRATION-GUIDE.md` landed (PR #16, branch `claude/complete-migration-plan-LRBKr`). The flat 28-doc `RESEARCH/` is fully migrated into `Product/`. The legacy folder is retired; pre-migration content is preserved in `RESEARCH_BACKUP/`.
- **Workflow accepted.** `_workflow/PRODUCT-BUILDER.md` is the canonical workflow doc (`status: accepted`); adoption captured as `decisions/0001-adopt-workflow.md`.
- **20 ADRs landed.** `engine/` (6), `actions/` (8), `triggers/` (2), `effects/` (3), `general/` (1 naming-conventions ADR, plus 1 adopt-workflow). No further backfill — future ADRs are forward-going.
- **Pile Counter sample status `locked`** after re-checking the four-part lock-bar against `features/engine/models/02-gamerules-contract.md`.
- **12 session notes backfilled** in `sessions/session-01..12.md` from PR + commit history. Reconstructions, not records.
- **Three ledgers seeded.** `ledgers/ALIGNMENT-DEBT.md` is empty (start from zero). `ledgers/BACKLOG.md` carries deferred infrastructure (FEATURE-MAP auto-generator, `/builder:lint`, `builder-drift-detection`, Tier 2/3 commands). `ledgers/FEATURE-MAP.md` has its initial table + empty mermaid block.
- **Cumulative design decisions preserved.** All settled work from sessions 1–10 (services-injected `GameRules`, snapshot in state via `IAvailableActions`, typed `ActionTemplate` per command, on-demand validation, `OnIncomingCommand` purely flow, validation in handler, generic command primitives, `PlayerIntent → IIntentMapper → GameCommand`, five validation layers, redux-like state, MTG-strict SBA known difference, trigger fold via `OnBefore<T>`/`OnAfter<T>`, async layer hierarchy, suspension via decision IDs, replacement priority) is captured in feature ADRs and Model provenance — see `features/<feature>/decisions/`.

---

## The active open question (singular)

**Effect-flow doc.** Effects mid-execution need `IAvailableActions` (target pickers publish decision-scoped snapshots) and `IGameStateProvider` — likely through `IHandlerContext`. The full effect lifecycle (async orchestration, decision-id threading, await semantics, suspension/resumption) is currently scattered across `features/effects/models/01-async-dispatch.md` and the implicit handler-context placeholders in `support/samples/pile-counter.md`. Needs a single canonical Model under `features/effects/models/`.

This is also a prerequisite for replacing the transitional `FreeInput`/`DecisionPrompt` snapshot scope with a typed state-machine (see `features/engine/models/03-snapshot-pipeline.md` §4).

Tracked as task #20.

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
| **20** | **Effect-flow canonical Model** | Active question. Model under `features/effects/models/04-effect-flow.md` (or merged with `01-async-dispatch.md` if that's the cleaner shape — decide on session start). Pulls from `features/effects/models/01-async-dispatch.md`, `features/engine/models/03-snapshot-pipeline.md` §7, and the handler-context placeholders in `support/samples/pile-counter.md`. |
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
| [ledgers/ALIGNMENT-DEBT.md](ledgers/ALIGNMENT-DEBT.md) | Empty at session-12 start. Open entries here would force the next session into pass-mode at >5. |
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
