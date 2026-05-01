---
type: checkpoint
last-updated: 2026-05-01
tags: [#checkpoint]
---

# Session Checkpoint

**Last updated:** 2026-05-01 (session 16)
**Replaceable.** History lives in `sessions/`. Never append here.

---

## Where we stopped

**§6 patch + cascade executed (S16).** All S15 design decisions mechanically applied across 5 Models and the pile-counter sample. 12 alignment-debt entries opened and discharged in the same pass. Pile-counter re-locked. Ledger clean (0 open entries).

---

## The active open question (singular)

**`GameState` slice catalog** — concrete catalog of slices (`PilesState`, `LastMoveState`, `ZonesState`, `TurnState`, `AvailableActionsState`) on top of Scaffold `Store`. Next unblocked candidate after §6 patch landed.

---

## Other still-open

Carried forward from `ledgers/OPEN-QUESTIONS.md`. Items the next few sessions are likely to touch.

- **`GameState` slice catalog** — concrete catalog of slices (`PilesState`, `LastMoveState`, `ZonesState`, `TurnState`, `AvailableActionsState`, decisions slice, etc.) on top of Scaffold `Store`. Load-bearing for engine wiring.
- **`IIntakeQueue` shape** — pin at engine implementation.
- **Game lifecycle design** — session start, turn structure, phase transitions, end conditions. Prerequisite for trigger subscription lifecycle.
- **Trigger subscription lifecycle** — Register/Unregister timing relative to zone transitions. Depends on game lifecycle.
- **`EndGameCommand` semantics** — bleeds into session lifecycle.
- **FSM-shaped game state (reframed S16).** `SnapshotScope`/`FreeInput`/`DecisionPrompt` deleted; `ICommandAwaiter` is the current mechanism. FSM-shaped game state (awaiter succession) is the eventual successor. Lower urgency.
- **Multi-actor / multi-open awaits.** Today single-open is a runtime assert. Type-only routing is structurally tractable for distinct types; pin when an async-decision-heavy game arrives.

---

## Suggested next steps

| # | Task | Notes |
|---|------|-------|
| **1** | **`GameState` slice catalog** | Active question. Concrete slice list + ownership model. Likely needs a dedicated Model under `features/engine/` or `features/state/`. |
| **WF-1** | **Tier 1 slash commands** | Per `ProductBuilder/AI-FIRST-AUTOMATION.md` §8 Tier 1. Cheap leverage on every session. |
| **WF-2** | **`/builder:design-pass` skill** | Q-by-Q with options + lean + verdict. S15 is the canonical example. |
| **12** | **Game lifecycle design** | Best unblocked task after §6 + slice catalog. New feature folder (`features/lifecycle/`?) or in `engine/`. |
| **7** | **`PLANS/` — first implementation slice** | Pull after §6, slice catalog, and lifecycle land and at least one feature is ready for build-gate consideration. |
| **15** | **MTG-strict SBA ordering** | Defer until a game needs it. Captured as `features/engine/decisions/0006-mtg-sba-known-difference.md`. |

---

## Artifacts to read on resume

| File | Why |
|------|-----|
| [_workflow/PRODUCT-BUILDER.md](_workflow/PRODUCT-BUILDER.md) | The workflow itself. |
| [ledgers/FEATURE-MAP.md](ledgers/FEATURE-MAP.md) | Bird's-eye view of features, Models, ADRs. |
| [ledgers/ALIGNMENT-DEBT.md](ledgers/ALIGNMENT-DEBT.md) | 0 open entries. Ledger clean after S16 cascade. |
| [features/effects/models/04-effect-flow.md](features/effects/models/04-effect-flow.md) | Canonical effect-flow contract. §6 patched in S16; `ICommandAwaiter` settled. |
| [sessions/session-16.md](sessions/session-16.md) | S16 execution record. §6 patch + cascade, pile-counter re-lock. |
| [ledgers/OPEN-QUESTIONS.md](ledgers/OPEN-QUESTIONS.md) | Durable design questions ledger. |
| [features/effects/models/01-async-dispatch.md](features/effects/models/01-async-dispatch.md) | Layer hierarchy, two dispatch paths. |
| [features/engine/models/03-snapshot-pipeline.md](features/engine/models/03-snapshot-pipeline.md) | Snapshot production; cascade completed in S16 (§4 Scope drop, §7 handler example). |
| [features/engine/models/02-gamerules-contract.md](features/engine/models/02-gamerules-contract.md) | `GameRules` interface — services injected, four methods. |
| [support/samples/pile-counter.md](support/samples/pile-counter.md) | Floor sample. **Status: `locked`** re-locked S16. |

---

## How to use this file

- **On milestone / session end:** Edit in place. Refresh date, "where we stopped," and "next steps."
- **Keep it short** — if it grows past two screens, trim. Detail belongs in `features/<feature>/` and `sessions/`.
- **Optional commit/PR line:** "Checkpoint: `Product/SESSION-CHECKPOINT.md`."
