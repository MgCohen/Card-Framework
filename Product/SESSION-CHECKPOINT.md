---
type: checkpoint
last-updated: 2026-05-01
tags: [#checkpoint]
---

# Session Checkpoint

**Last updated:** 2026-05-01 (session 19)
**Replaceable.** History lives in `sessions/`. Never append here.

---

## Where we stopped

**S19 settled the GameState slice catalog and triggered an Effect/Trigger unification.** Two-tier slice model landed: framework-owned slices (`AvailableActionsState`, `EngineState`, `HistoryState`) auto-seeded at `Build()`; game-authored slices registered via builder. `Context` deleted — triggers now receive `IEffectScope` directly, `Self` closure-captured. `Trigger<T>` introduced (`Validate` + `Execute`). `FoldResult` demoted to pipeline-internal; `scope.Cancel()`/`scope.Replace()` are the public API. `Effect.Validate` added. Debt net-zero (3 opened, 3 closed same session). Ready for engine wiring design pass.

---

## The active open question (singular)

**Engine wiring stack** — `IHandlerRegistry` interface, `GameEngineBuilder` construction protocol, `ICommandPipeline` interface, engine startup / first `OnQueueDrained` trigger, `GameRules` service wiring. Cluster of 5 — one design-pass session.

---

## Other still-open

Carried forward from `ledgers/OPEN-QUESTIONS.md`. Items the next few sessions are likely to touch.

- **Engine wiring stack** — `IHandlerRegistry` + `GameEngineBuilder` + `ICommandPipeline` + startup. Active question for S20.
- **`ITriggerSystem` vs `ITriggerPipeline` unification (S17/S19)** — subscription surface updated in S19 (`OnBefore/OnAfter` now take `Trigger<T>`); two-type vs one-type question still unresolved.
- **`IIntakeQueue` shape** — pin at engine implementation.
- **Game lifecycle design / `EndGameCommand` semantics** — session start, turn structure, end conditions. Prerequisite for trigger subscription lifecycle.
- **Trigger subscription lifecycle** — Register/Unregister timing for regular cards (not just attachments). Depends on game lifecycle.
- **Zone model internal opens (S17)** — 9 questions in `zones/01-zone-model.md`; membership storage / visibility / actor semantics blocking.
- **Entity destruction / Cleanup lifecycle (S17)** — what "destroyed" means for the Store; ongoing-effects depend on it.
- **`HistoryEnvelope` typed definition (S17)** — `HistoryState` slice exists; payload is pseudocode. Load-bearing for replay + multiplayer.
- **Intent → template lookup (S17)** — gateway side of `IIntentMapper.Map(template, choice)`.
- **FSM-shaped game state (S16).** Awaiter succession. Lower urgency.
- **Multi-actor / multi-open awaits.** Pin when an async-decision-heavy game arrives.

---

## Suggested next steps

| # | Task | Notes |
|---|------|-------|
| **1** | **Engine wiring design pass** | `IHandlerRegistry` + `GameEngineBuilder` + `ICommandPipeline` + `ITriggerSystem`/`ITriggerPipeline` unification + startup. Cluster of 5 — one design-pass session. |
| **WF-1** | **Tier 1 slash commands** | Per `ProductBuilder/AI-FIRST-AUTOMATION.md` §8 Tier 1. Cheap leverage on every session. |
| **12** | **Game lifecycle design** | Best unblocked task after engine wiring. New feature folder (`features/lifecycle/`?) or in `engine/`. |
| **7** | **`PLANS/` — first implementation slice** | Pull after engine wiring and lifecycle land. |
| **15** | **MTG-strict SBA ordering** | Defer until a game needs it. Captured as `features/engine/decisions/0006-mtg-sba-known-difference.md`. |

---

## Artifacts to read on resume

| File | Why |
|------|-----|
| [_workflow/PRODUCT-BUILDER.md](_workflow/PRODUCT-BUILDER.md) | The workflow itself. |
| [ledgers/FEATURE-MAP.md](ledgers/FEATURE-MAP.md) | Bird's-eye view of features, Models, ADRs. |
| [ledgers/ALIGNMENT-DEBT.md](ledgers/ALIGNMENT-DEBT.md) | **Clean after S19.** |
| [features/engine/models/04-gamestate-slices.md](features/engine/models/04-gamestate-slices.md) | S19: slice catalog. Two-tier model, 3 framework slices, invariants. |
| [features/triggers/models/01-trigger-fold.md](features/triggers/models/01-trigger-fold.md) | S19: `Trigger<T>` defined; `Context` deleted; `FoldResult` internal; subscription surface updated. |
| [features/effects/models/04-effect-flow.md](features/effects/models/04-effect-flow.md) | S19: `Effect.Validate` + `IEffectScope.Replace` added. Canonical effect-flow contract. |
| [features/effects/models/02-replacement-prevention.md](features/effects/models/02-replacement-prevention.md) | S19 cascade: `FoldResult` demoted; `TriggerMatch` uses `IEffectScope`; prevention example updated. |
| [sessions/session-19.md](sessions/session-19.md) | S19 execution record. |
| [ledgers/OPEN-QUESTIONS.md](ledgers/OPEN-QUESTIONS.md) | Durable design questions ledger. Slice catalog resolved S19. |
| [features/engine/models/02-gamerules-contract.md](features/engine/models/02-gamerules-contract.md) | `GameRules` interface — services injected, four methods. Load-bearing for engine wiring. |
| [support/samples/pile-counter.md](support/samples/pile-counter.md) | Floor sample. Status: `locked`. |

---

## How to use this file

- **On milestone / session end:** Edit in place. Refresh date, "where we stopped," and "next steps."
- **Keep it short** — if it grows past two screens, trim. Detail belongs in `features/<feature>/` and `sessions/`.
- **Optional commit/PR line:** "Checkpoint: `Product/SESSION-CHECKPOINT.md`."
