---
type: feature
status: settled
references: [features/entities/models/02-instance.md, features/zones/models/01-zone-model.md, features/triggers/models/01-trigger-fold.md, features/engine/models/03-game-rules-runtime.md]
tags: [#actions]
---

# actions — feature

## What it is

The framework's command pipeline. A **`PlayerIntent`** (UI input or system attempt) is mapped through `IIntentMapper` into a typed **`GameCommand`** that flows through the pipeline (engine lifecycle gate → `handler.Validate` → framework FIFO → drain → before-fold → handler → after-fold → envelope) and emits a **`HistoryEnvelope`** capturing cause and effects. The feature also owns the **`AvailableActionsSnapshot`** shape (typed `ActionTemplate` per command type) that tells presentation what's currently legal.

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
        ┌────────────────┴─────────────────┐
        │ Engine lifecycle gate (framework)│
        │ → handler.Validate               │ ← authoritative legality
        │ → enqueue (framework FIFO)       │
        │ → Drain:                         │
        │   ├─ before-fold (replace/cancel)│
        │   ├─ handler.Handle              │ ← invokes mutators via ctx.Store.Execute
        │   ├─ after-fold (reactions)      │
        │   └─ envelope publishes          │
        └──────────────────────────────────┘
                         │
                         ▼
                   HistoryEnvelope
                         │
                  Observers / Networking
```

Per [ADR-0006 — two-pipeline architecture](Product/decisions/0006-two-pipeline-architecture.md), command-queue routing is framework-FIFO (the runtime does not gate intake or route commands); effect-level orchestration is the customizable layer where game styles diverge.

## Vocabulary

**Feature-owned terms only.** Cross-feature terms used by this feature — `GameAction`, `CompositeAction`, `GameSignal`, `Trigger<T>`, `OnBefore`/`OnAfter`, `TriggerMatch`, `TriggerFoldResult`, `TriggerPhase` (defined in `triggers/`); `IEffectScope`, `IHandlerContext`, `Effect`, `Mutator` (defined in `effects/`); `GameRulesRuntime`, `IAvailableActions`, `EngineLifecycle`, `EngineState` (defined in `engine/`); `Card`, `CardRef`, `Zone`, `Reference`, `Ref<T>`, `IActor`, `ActorId` — live in their owning features or `Product/ledgers/GLOSSARY.md`; link to their canonical home, don't restate.

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
| `AvailableActionsSnapshot` | `IReadOnlyList<ActionTemplate>` published by `GameRulesRuntime.Evaluate` (and effects mid-execution) via `IAvailableActions.Set`. |
| `TargetSlot` / `ModeOption` / `ParameterSpec` | Building blocks for `ActionTemplate` subtypes that need them; not all templates carry all three. |
| `IntentGateway` | Thin entry layer translating `PlayerIntent` → dispatched `GameCommand` (typically reads the snapshot, runs `IntentMapper`, calls `engine.Dispatch`). Game-supplied. |

## Architecture

The feature has five Models. Each is the canonical home for one load-bearing element of the action pipeline:

1. **`ICommandPipeline`** ([`01-command-pipeline.md`](features/actions/models/01-command-pipeline.md)) — the dispatch flow: lifecycle gate → validate → enqueue → drain (before-fold → handler → after-fold → envelope) → evaluate. Type hierarchy reference. Framework-FIFO routing per ADR-0006.
2. **`IIntentMapper`** ([`02-intent-mapper.md`](features/actions/models/02-intent-mapper.md)) — the boundary that turns `PlayerIntent` (UI/AI/network/etc) into a typed `GameCommand`. Owns `PlayerIntent` and `IntentGateway` as its tied surfaces.
3. **`ICommandHandler<TCommand, TResult>`** ([`03-command-handler.md`](features/actions/models/03-command-handler.md)) — the per-command resolver. `Validate` (authoritative) + `Handle` (writes via mutators, returns typed `CommandResult`).
4. **`HistoryEnvelope`** ([`04-history-envelope.md`](features/actions/models/04-history-envelope.md)) — the grouping unit observers consume. One per `GameAction`. `HistoryState` slice holds the ordered envelope log.
5. **`AvailableActionsSnapshot`** ([`05-available-actions-snapshot.md`](features/actions/models/05-available-actions-snapshot.md)) — typed `ActionTemplate` per command type; `PlayerChoice` shape; eligibility framing.

The six shapes that flow through the pipeline (`PlayerIntent` → `ActionTemplate` → `GameCommand` → `StateMutation` → `CommandResult` → `HistoryEnvelope`) are owned across these models plus `triggers/` (`GameCommand` type hierarchy) and `state/` (`StateMutation`).

### The five validation layers

Validation is layered explicitly. **`handler.Validate` is authoritative at dispatch**; the others narrow the search space, gate UI, ensure well-formedness, or enforce engine-level flow. Full breakdown lives in [`Product/decisions/0007-five-validation-layers.md`](Product/decisions/0007-five-validation-layers.md) (project-wide ADR — spans engine + actions + presentation).

| Layer | Owner feature | Concern | When |
|---|---|---|---|
| Per-template eligibility | `engine/` (runtime's `Evaluate`) | Which templates are even relevant | Snapshot construction |
| `AffordanceMap` | `presentation/` | UI affordance projection | UI render |
| `IIntentMapper.Map` | `actions/` | Well-formedness | Intent → command translation |
| `handler.Validate` | `actions/` | Authoritative legality | Engine intake (every dispatch) |
| Engine lifecycle gate | `engine/` (framework) | Match-lifecycle gate (paused/ended) | Engine intake (every dispatch) |

## Composition

| Model | Role |
|---|---|
| [`01-command-pipeline.md`](features/actions/models/01-command-pipeline.md) | `ICommandPipeline`; dispatch flow; type-hierarchy reference; framework-FIFO routing |
| [`02-intent-mapper.md`](features/actions/models/02-intent-mapper.md) | `IIntentMapper` + `PlayerIntent` + `IntentGateway` (input boundary) |
| [`03-command-handler.md`](features/actions/models/03-command-handler.md) | `ICommandHandler<TCommand, TResult>` + `CommandResult` (per-command resolver) |
| [`04-history-envelope.md`](features/actions/models/04-history-envelope.md) | `HistoryEnvelope` + `HistoryState` slice (envelope log) |
| [`05-available-actions-snapshot.md`](features/actions/models/05-available-actions-snapshot.md) | `AvailableActionsSnapshot`, typed `ActionTemplate`, `PlayerChoice`, eligibility |

## Invariants

Cross-Model rules that hold across both Models and shape how downstream features must use the actions pipeline.

- **One enforced entry point.** Every `GameCommand` enters the pipeline through `engine.Dispatch`. No bypass paths; no in-process injection.
- **`handler.Validate` is authoritative.** Pre-template eligibility, AffordanceMap, IntentMapper, and the engine lifecycle gate are advisory or specialized; the handler is final at dispatch.
- **Handlers return typed results.** `CommandResult` subtypes per command (`MoveCardResult`, `DealDamageResult`, etc.). Effects branch on these.
- **One handler per command type.** DI registration is closed-generic (`ICommandHandler<DealDamageCommand, DamageResult>`). Multiple registrations for the same `TCommand` are an error at container build.
- **Handlers are the only layer that issues mutators.** Effects sequence commands; mutators write state; handlers bridge by calling `ctx.Store.Execute(payload)`.
- **One envelope per `GameAction`.** Effect-dispatched siblings share `OriginCommandId` but each gets its own envelope. The historical "child folds into parent envelope" model is retired.
- **`OriginCommandId` is engine-threaded.** The engine copies it from the just-executed command onto every evaluator follow-up; effects do not need to set it.
- **`OriginIntent` is recorded once, on the first envelope.** Subsequent envelopes in the chain share `OriginCommandId` but carry no `OriginIntent`.
- **UI creates intents, not state changes.** `PlayerIntent` is the boundary; everything beyond is framework / game logic.
- **Snapshot is approximate; dispatch is authoritative.** Per-binding `Validate` runs at dispatch (always) and may run during snapshot enumeration (on-demand by the rule). UI gates may show stale bindings; dispatch catches them.
- **`ActionTemplate` is the snapshot wire-form.** `Ref<T>` and `Reference` fields on templates are boundary-correct (per [ADR-0005](Product/decisions/0005-objects-over-ids.md)) — they're transported to presentation and consumed by `IntentMapper`.
- **Command-queue routing is framework-FIFO.** Per [ADR-0006](Product/decisions/0006-two-pipeline-architecture.md), the runtime does not gate intake or route commands; game-style differences in how reactions are scheduled live at the effect-level orchestration layer.

## Boundaries

**Owns:** `PlayerIntent` and `IntentGateway` contracts; `IIntentMapper` interface; `GameCommand` pipeline mechanics (validate / before-fold integration / handler / after-fold integration / envelope close); `ICommandHandler<TCommand, TResult>` and `CommandHandler<TCommand, TResult>` typed base; `CommandResult` and its variant convention; `HistoryEnvelope` shape and `OriginCommandId` / `OriginIntent` threading; `AvailableActionsSnapshot`; `ActionTemplate` (abstract base + typed subtypes per command); `PlayerChoice`; `TargetSlot` / `ModeOption` / `ParameterSpec` building blocks.

**Does not own:**
- `GameCommand` type hierarchy (`GameAction`, `CompositeAction`, `GameSignal`) → `triggers/`
- Trigger fold mechanics (before-fold / after-fold execution) → `triggers/`
- `Effect` orchestration, `IEffectScope` → `effects/`
- `IAvailableActions` service (writer) — the snapshot is published by → `engine/`
- `GameRulesRuntime` four-method contract — `FilterTriggers` and `Evaluate` are owned by → `engine/`
- `Mutator<TState, TPayload>` and `Store.Execute(payload)` mechanics → `engine/` (`state/state`)
- Engine lifecycle gate, framework FIFO, drain loop → `engine/`
- Card-specific shape, zone-specific shape → `cards/`, `zones/`
- UI presentation, `AffordanceMap` implementation → `presentation/`

## Dependencies

*Mirrors the front-matter `references:` array in human-readable form (per §3).*

- **Upstream:** `entities/`, `zones/`, `triggers/` (type hierarchy hosting the pipeline), `engine/` (lifecycle gate, framework FIFO, `GameRulesRuntime` runtime), `effects/` (handler signature returns `Task<TResult>`; effects orchestrate via `scope.Dispatch`).
- **Downstream:** `presentation/` (consumes `AvailableActionsSnapshot`, `HistoryEnvelope`, builds `AffordanceMap`).

## Open

See `ledgers/OPEN-ITEMS.md`:

- **`bl-interaction-feature`** — `interaction/` feature for the gesture-to-intent layer (the layer between presentation gestures and `PlayerIntent`). All intent vocabulary depends on it.
- Scheduler models — broader scheduling design (interrupt injection, priority queues, MTG-stack semantics) lives in `engine/` + `effects/` per ADR-0006; affects this pipeline indirectly via the effect-level layer.
- Rollback / rejection signaling to presentation — how rejected dispatches surface to UI (toast? state subscription? envelope entry?).
- Mandatory `HistoryEnvelope` metadata — partially settled (`OriginCommandId`, `OriginIntent`, `Actor`, `SourceInstance`, `Result`, `Entries`); typed `HistoryEnvelope` record pin tracked under `engine/04`.
- `PlayerChoice` shape under typed templates — may collapse to positional `IReadOnlyList<EntityId>` + `IReadOnlyDictionary<string, object>` parameters now that templates carry typed slot data. Pin when `IIntentMapper` is implemented.
