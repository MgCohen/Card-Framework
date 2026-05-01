---
type: checkpoint
last-updated: 2026-05-01
tags: [#checkpoint]
---

# Session Checkpoint

**Last updated:** 2026-05-01 (session 15)
**Replaceable.** History lives in `sessions/`. Never append here.

---

## Where we stopped

Auto debt-pass: 4 entries discharged, 0 stragglers caught, 1 skipped.

---

## 🎯 The active open question (singular)

**§6 boundary redesign in `features/effects/models/04-effect-flow.md`.** The current §6 sketch has `IIntentMapper` branching on snapshot scope to pick between `MapFreeInput` and `DecisionResponseCommand`. User flagged this wrong-batch in session 13: the routing concern likely belongs to a different layer than the mapper, but no counter-shape was pinned. Settle the alternative shape and patch §6 (with cascade-aware edits to anywhere downstream that mirrors the current sketch).

Architecturally interesting; mechanical-only edits unlikely. Sample re-lock and the slice-catalog work are the next two candidates after this lands.

---

## ❓ Other still-open

Carried forward from `ledgers/OPEN-QUESTIONS.md`. Items the next few sessions are likely to touch.

- **`GameState` slice catalog** — concrete catalog of slices (`PilesState`, `LastMoveState`, `ZonesState`, `TurnState`, `AvailableActionsState`, decisions slice, etc.) on top of Scaffold `Store`. Load-bearing for engine wiring.
- **`IIntakeQueue` shape** — pin at engine implementation.
- **Game lifecycle design** — session start, turn structure, phase transitions, end conditions. Prerequisite for trigger subscription lifecycle.
- **Trigger subscription lifecycle** — Register/Unregister timing relative to zone transitions. Depends on game lifecycle.
- **`EndGameCommand` semantics** — bleeds into session lifecycle.
- **Snapshot scope as state machine** — revisit with the effect-flow doc.

---

## Suggested next steps

| # | Task | Notes |
|---|------|-------|
| **20-§6** | **Effect-flow §6 boundary redesign** | Active question. Move the `FreeInput`/`DecisionPrompt` routing concern out of `IIntentMapper`. Settle the alternative shape, patch §6 + downstream mirrors. |
| **20-relock** | **Re-lock pile-counter** | Status `draft` after session 14's §9 rewrite. Re-lock per §4.4 lockbar after one stable session with no further contract edits. |
| **2** | **`GameState` slice catalog** | Now load-bearing in `OPEN-QUESTIONS.md`. Concrete slice list + ownership model. Likely needs a dedicated Model under `features/engine/` or `features/state/`. |
| **WF-1** | **Tier 1 slash commands** | Per `ProductBuilder/AI-FIRST-AUTOMATION.md` §8 Tier 1. Cheap leverage on every session. |
| **12** | **Game lifecycle design** | Best unblocked task after §6 + slice catalog. New feature folder (`features/lifecycle/`?) or in `engine/`. |
| **7** | **`PLANS/` — first implementation slice** | Pull after §6, slice catalog, and lifecycle land and at least one feature is ready for build-gate consideration. |
| **15** | **MTG-strict SBA ordering** | Defer until a game needs it. Captured as `features/engine/decisions/0006-mtg-sba-known-difference.md`. |

---

## Artifacts to read on resume

| File | Why |
|------|-----|
| [_workflow/PRODUCT-BUILDER.md](_workflow/PRODUCT-BUILDER.md) | The workflow itself. |
| [ledgers/FEATURE-MAP.md](ledgers/FEATURE-MAP.md) | Bird's-eye view of features, Models, ADRs. |
| [ledgers/ALIGNMENT-DEBT.md](ledgers/ALIGNMENT-DEBT.md) | 0 open entries. Resolved section grew by 12 in session 14. |
| [features/effects/models/04-effect-flow.md](features/effects/models/04-effect-flow.md) | Canonical effect-flow contract. §6 is the active question. Read §10 (invariants) for the headline pins. |
| [ledgers/OPEN-QUESTIONS.md](ledgers/OPEN-QUESTIONS.md) | Durable design questions ledger. |
| [features/effects/models/01-async-dispatch.md](features/effects/models/01-async-dispatch.md) | Layer hierarchy, two dispatch paths. |
| [features/engine/models/03-snapshot-pipeline.md](features/engine/models/03-snapshot-pipeline.md) | Snapshot production; refreshed in session 14 against Scaffold State. |
| [features/engine/models/02-gamerules-contract.md](features/engine/models/02-gamerules-contract.md) | `GameRules` interface — services injected, four methods. Refreshed in session 14. |
| [support/samples/pile-counter.md](support/samples/pile-counter.md) | Floor sample. **Status: `draft`** after session 14 rewrite. Re-lock candidate. |

For the Tier 1 commands (WF-1):

| File | Why |
|------|-----|
| [../ProductBuilder/AI-FIRST-AUTOMATION.md](../ProductBuilder/AI-FIRST-AUTOMATION.md) | Catalog of slash commands and skills, with §3 detailed specs and §8 sequencing. |

---

## How to use this file

- **On milestone / session end:** Edit in place. Refresh date, "where we stopped," and "next steps."
- **Keep it short** — if it grows past two screens, trim. Detail belongs in `features/<feature>/` and `sessions/`.
- **Optional commit/PR line:** "Checkpoint: `Product/SESSION-CHECKPOINT.md`."
