---
type: feature
status: settled
references: [features/entities/models/01-definition-instance-model.md, features/zones/models/01-zone-model.md, features/engine/models/02-gamerules-contract.md, features/triggers/models/01-trigger-fold.md, features/effects/models/04-effect-flow.md]
tags: [#actions]
---

# actions — feature

## What it is

The framework's command pipeline. A **`PlayerIntent`** (UI input or system attempt) is mapped through `IIntentMapper` into a typed **`GameCommand`** that flows through the pipeline (lifecycle gate → handler.Validate → `OnIncomingCommand` → queue → drain → before-fold → handler → after-fold → envelope) and emits a **`HistoryEnvelope`** capturing the cause and effects. The feature also owns the **`AvailableActionsSnapshot`** shape (typed `ActionTemplate` per command type) that tells presentation what's currently legal.

```
                PlayerIntent (UI input / system attempt)
                         │
                IntentMapper.Map(template, choice)
                         │
                         ▼
                   GameCommand
                         │
                engine.Dispatch
                         │
        ┌────────────────┴────────────────┐
        │ Lifecycle gate                  │
        │ → handler.Validate              │ ← authoritative legality
        │ → GameRules.OnIncomingCommand   │ ← flow / queue routing
        │ → enqueue                       │
        │ → Drain:                        │
        │   ├─ before-fold (replace/cancel)│
        │   ├─ handler.Handle              │ ← handler.Execute applies state changes via mutators
        │   ├─ after-fold (reactions)      │
        │   └─ envelope publishes          │
        └─────────────────────────────────┘
                         │
                         ▼
                   HistoryEnvelope
                         │
                  Observers / Networking
```

## Vocabulary

**Feature-owned terms only.** Cross-feature terms used by this feature — `GameAction`, `CompositeAction`, `GameSignal`, `Trigger<T>`, `OnBefore`/`OnAfter`, `TriggerMatch`, `TriggerFoldResult`, `TriggerPhase` (defined in `triggers/`); `IEffectScope`, `IHandlerContext`, `ICommandAwaiter`, `Effect`, `Mutator` (defined in `effects/`); `GameRulesRuntime`, `IAvailableActions`, `AvailableActionsSnapshot`, `IntakeResult`, `DrainResult`, `IIntakeQueue`, `EngineLifecycle` (defined in `engine/`); `Card`, `Zone`, `OwnershipState`, `CardRef`, `InstanceId`, `Actor`, `ActorId`, `IReference`, `Identifier<T>` — live in their owning features or `Product/ledgers/GLOSSARY.md`; link to their canonical home, don't restate.

| Term | Meaning |
|---|---|
| `PlayerIntent` | A structured intent submitted to the engine from outside the rules layer (drag-drop, AI choice, network message, scripted bot, replay playback). Carries what the actor attempted, not the final state mutation. |
| `IIntentMapper` | Game-supplied service: `Map(ActionTemplate template, PlayerChoice choice) → GameCommand`. Switches on typed template subtype to produce a well-formed command. Decision-unaware. |
| `PlayerChoice` | The data the player committed to: target list, parameter values, mode selections. Bound by `IIntentMapper` against a template to produce a `GameCommand`. |
| `GameCommand` | Base type for everything that flows through the pipeline (defined in `triggers/`); subtypes are `GameAction` (executable, queued), `CompositeAction` (inline, no queue), `GameSignal` (no execution). |
| `ICommandHandler<TCommand, TResult>` | Per-command resolver. One per `GameCommand` type. `Validate(cmd, state) → bool` (authoritative legality check) + `Handle(cmd, ctx) → Task<TResult>` (apply state via mutators, return result). Game-supplied. |
| `ICommandPipeline` | Framework executor: `Validate(cmd, state)` (resolves handler, delegates) + `Execute(action) → CommandResult` (full lifecycle for one action). |
| `CommandResult` | Typed return from a handler. Subclasses per command. Populates `HistoryEnvelope.Result`. May carry cancellation flag, redirected target, damage amount, drawn instance, opened decision id, etc. |
| `HistoryEnvelope` | The grouping unit observers consume — one per `GameAction`, with cause metadata (`OriginCommandId`, `OriginIntent` (first only), `Actor`, `SourceInstance`, `Result`, `Entries[]`). |
| `OriginCommandId` | ID of the first command in a chain. Threaded by the engine onto every evaluator follow-up so all chain members share identity. |
| `OriginIntent` | The originating `PlayerIntent` recorded on the first envelope of a chain only. |
| `ActionTemplate` | Snapshot wire-form. Abstract base (`CommandType`, `Source?`); typed subtypes per command (`MoveCardTemplate`, `CastSpellTemplate`, etc.) carrying per-action shape (targets, modes, parameters). |
| `AvailableActionsSnapshot` | `IReadOnlyList<ActionTemplate>` published by `GameRulesRuntime.OnQueueDrained` (and effects mid-execution) via `IAvailableActions.Set`. |
| `TargetSlot` / `ModeOption` / `ParameterSpec` | Building blocks for `ActionTemplate` subtypes that need them; not all templates carry all three. |
| `IntentGateway` | Thin entry layer translating `PlayerIntent` → dispatched `GameCommand` (typically reads the snapshot, runs `IntentMapper`, calls `engine.Dispatch`). Game-supplied. |

## Architecture

The feature has two Models:

1. **Command pipeline** (Model 01) — defines `PlayerIntent`, `GameCommand`, `CommandResult`, `HistoryEnvelope`, the pipeline mechanics (validate → handler → mutators → envelope), the relationship to triggers (before-fold / after-fold), the `IIntentMapper` contract.

2. **Availability snapshot** (Model 02) — defines `ActionTemplate` (typed per command type), `PlayerChoice`, `AvailableActionsSnapshot` shape; per-template eligibility filtering posture; bridge to `IIntentMapper.Map` (template + choice → command).

The pipeline writes envelopes that observers consume; the snapshot tells observers what's *currently possible*. They share the `IntentMapper` boundary — snapshot templates flow through the mapper to become commands.

### The five validation layers

Validation is layered explicitly. **`handler.Validate` is authoritative at dispatch**; the others narrow the search space, gate UI, ensure well-formedness, or enforce game-level flow. Full breakdown lives in [`Product/decisions/0007-five-validation-layers.md`](decisions/0007-five-validation-layers.md) (project-wide ADR — spans engine + actions + presentation).

| Layer | Owner feature | Concern | When |
|---|---|---|---|
| Per-template eligibility | `engine/` (rule's `OnQueueDrained`) | Which templates are even relevant | Snapshot construction |
| `AffordanceMap` | `presentation/` | UI affordance projection | UI render |
| `IIntentMapper.Map` | `actions/` | Well-formedness | Intent → command translation |
| `handler.Validate` | `actions/` | Authoritative legality | Engine intake (every dispatch) |
| `GameRules.OnIncomingCommand` | `engine/` | Flow gate / queue routing | Engine intake (every dispatch) |

## Composition

| Model | Role |
|---|---|
| [`models/01-command-pipeline.md`](models/01-command-pipeline.md) | Pipeline mechanics; the six shapes (`PlayerIntent`, `ActionTemplate`, `GameCommand`, `StateMutation`, `CommandResult`, `HistoryEnvelope`); `IIntentMapper` contract; relationship to triggers and effects |
| [`models/02-availability-snapshot.md`](models/02-availability-snapshot.md) | Typed `ActionTemplate` per command type; `TargetSlot` / `ModeOption` / `ParameterSpec` building blocks; `PlayerChoice` shape; eligibility framing |

## Invariants

Cross-Model rules that hold across both Models and shape how downstream features must use the actions pipeline.

- **One enforced entry point.** Every `GameCommand` enters the pipeline through `engine.Dispatch`. No bypass paths; no in-process injection.
- **`handler.Validate` is authoritative.** Pre-template eligibility, AffordanceMap, IntentMapper, and `OnIncomingCommand` are advisory or specialized; the handler is final at dispatch.
- **Handlers return typed results.** `CommandResult` subtypes per command (`MoveCardResult`, `DealDamageResult`, etc.). Effects branch on these.
- **One handler per command type.** DI registration is closed-generic (`ICommandHandler<DealDamageCommand, DamageResult>`). Multiple registrations for the same `TCommand` are an error at container build.
- **Handlers are the only layer that issues mutators.** Effects sequence commands; mutators write state; handlers bridge by calling `ctx.Store.Execute(payload)`.
- **One envelope per `GameAction`.** Effect-dispatched siblings share `OriginCommandId` but each gets its own envelope. The historical "child folds into parent envelope" model is retired.
- **`OriginCommandId` is engine-threaded.** The engine copies it from the just-executed command onto every evaluator follow-up; effects do not need to set it.
- **`OriginIntent` is recorded once, on the first envelope.** Subsequent envelopes in the chain share `OriginCommandId` but carry no `OriginIntent`.
- **UI creates intents, not state changes.** `PlayerIntent` is the boundary; everything beyond is framework / game logic.
- **Snapshot is approximate; dispatch is authoritative.** Per-binding `Validate` runs at dispatch (always) and may run during snapshot enumeration (on-demand by the rule). UI gates may show stale bindings; dispatch catches them.
- **`ActionTemplate` is the snapshot wire-form.** `Identifier<T>` fields on templates are boundary-correct (per [`Product/decisions/0005-objects-over-ids.md`](decisions/0005-objects-over-ids.md)) — they're transported to presentation and consumed by `IntentMapper`.

## Boundaries

**Owns:** `PlayerIntent` and `IntentGateway` contracts; `IIntentMapper` interface; `GameCommand` pipeline mechanics (validate / before-fold integration / handler / after-fold integration / envelope close); `ICommandHandler<TCommand, TResult>` and `CommandHandler<TCommand, TResult>` typed base; `CommandResult` and its variant convention; `HistoryEnvelope` shape and `OriginCommandId` / `OriginIntent` threading; `AvailableActionsSnapshot`; `ActionTemplate` (abstract base + typed subtypes per command); `PlayerChoice`; `TargetSlot` / `ModeOption` / `ParameterSpec` building blocks.

**Does not own:**
- `GameCommand` type hierarchy (`GameAction`, `CompositeAction`, `GameSignal`) → `triggers/`
- Trigger fold mechanics (before-fold / after-fold execution) → `triggers/`
- `Effect` orchestration, `IEffectScope`, `ICommandAwaiter` → `effects/`
- `IAvailableActions` service (writer) — the snapshot is published by → `engine/`
- `GameRulesRuntime` four-method contract — `OnIncomingCommand` and `Evaluate` are owned by → `engine/`
- `Mutator<TState, TPayload>` and `Store.Execute(payload)` mechanics → `engine/` (`engine/04-gamestate-slices`)
- Lifecycle gate, drain loop, queue → `engine/`
- Card-specific shape, zone-specific shape → `cards/`, `zones/`
- UI presentation, `AffordanceMap` implementation → `presentation/`

## Dependencies

*Mirrors the front-matter `references:` array in human-readable form (per §3).*

- **Upstream:** `entities/`, `zones/`, `triggers/` (type hierarchy hosting the pipeline), `engine/` (calls into `GameRulesRuntime`; consumes `IAvailableActions`), `effects/` (handler signature returns `Task<TResult>`; effects orchestrate via `scope.Dispatch`).
- **Downstream:** `presentation/` (consumes `AvailableActionsSnapshot`, `HistoryEnvelope`, builds `AffordanceMap`).

## Open

See `ledgers/OPEN-ITEMS.md`:

- **`bl-interaction-feature`** — `interaction/` feature for the gesture-to-intent layer (the layer between presentation gestures and `PlayerIntent`). All intent vocabulary depends on it.
- Scheduler models and how they affect action flow — broader scheduling design (interrupt injection, priority queues, MTG-stack semantics) lives mostly in `engine/`; affects this pipeline.
- Rollback / rejection signaling to presentation — how rejected dispatches surface to UI (toast? state subscription? envelope entry?).
- Mandatory `HistoryEnvelope` metadata — partially settled (`OriginCommandId`, `OriginIntent`, `Actor`, `SourceInstance`, `Result`, `Entries`); typed `HistoryEnvelope` record pin tracked under `engine/04`.
- `PlayerChoice` shape under typed templates — may collapse to positional `IReadOnlyList<EntityId>` + `IReadOnlyDictionary<string, object>` parameters now that templates carry typed slot data. Pin when `IIntentMapper` is implemented.
