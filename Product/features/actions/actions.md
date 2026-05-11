---
type: feature
status: settled
references: [features/entities/models/02-instance.md, features/zones/models/01-zone-model.md, features/triggers/models/01-trigger.md, features/engine/models/03-game-rules-runtime.md]
tags: [#actions]
---

# actions — feature

## What it is

The framework's command pipeline. A **`PlayerIntent`** (UI input or system attempt) is mapped through `IIntentMapper` into a typed **`GameCommand`** that flows through the pipeline (registered `ICommandValidator<TCommand>` chain → framework FIFO → drain → before-fold → handler → after-fold → envelope) and emits a **`HistoryEnvelope`** capturing cause and effects. The feature also owns the **`AvailableActionsSnapshot`** shape (typed `ActionTemplate` per command type — *rework pending under [ADR-0012 — *forthcoming*]*) that tells presentation what's currently legal.

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
        ┌────────────────┴─────────────────────────┐
        │ pipeline.Validate                         │
        │   • SessionGate          (framework def.) │
        │   • MatchStateActionGate (framework def.) │
        │   • game-shipped validators (DI order)    │ ← authoritative chain
        │ → enqueue (framework FIFO)                │
        │ → Drain:                                  │
        │   ├─ before-fold (replace/cancel)         │
        │   ├─ handler.Handle                       │ ← invokes mutators via Store.Execute
        │   ├─ after-fold (reactions)               │
        │   └─ envelope publishes                   │
        └───────────────────────────────────────────┘
                         │
                         ▼
                   HistoryEnvelope
                         │
                  Observers / Networking
```

Per [ADR-0006 — two-pipeline architecture](Product/decisions/0006-two-pipeline-architecture.md), command-queue routing is framework-FIFO (the runtime does not gate intake or route commands); effect-level orchestration is the customizable layer where game styles diverge. Per [ADR-0010 — validator / handler split](Product/decisions/0010-validator-handler-split.md), validation lives in a separate `ICommandValidator<TCommand>` interface; framework defaults register first; game-shipped validators register after.

## Vocabulary

**Feature-owned terms only.** Cross-feature terms used by this feature — `GameAction`, `CompositeAction`, `GameSignal`, `Trigger<T>`, `OnBefore`/`OnAfter`, `TriggerMatch`, `TriggerFoldResult`, `TriggerPhase` (defined in `triggers/`); `IEffectScope`, `Effect`, `Mutator` (defined in `effects/`); `GameRulesRuntime`, `MatchState`, `MatchStateSlice`, `SessionState`, `EngineState` (defined in `engine/`); `Card`, `CardRef`, `Zone`, `Reference`, `Ref<T>`, `IActor`, `ActorId` — live in their owning features or `Product/ledgers/GLOSSARY.md`; link to their canonical home, don't restate. Retired terms (`IHandlerContext`, `IAvailableActions`, `EngineLifecycle`) live in `GLOSSARY.md` §Retired terms.

| Term | Meaning |
|---|---|
| `PlayerIntent` | A structured intent submitted to the engine from outside the rules layer (drag-drop, AI choice, network message, scripted bot, replay playback). Carries what the actor attempted, not the final state mutation. |
| `IIntentMapper` | Game-supplied service: `Map(ActionTemplate template, PlayerChoice choice) → GameCommand`. Switches on typed template subtype to produce a well-formed command. Decision-unaware. *(Rework pending under [ADR-0012 — *forthcoming*]; replaced by per-state `MatchStateViewModel<TState>.Map`.)* |
| `PlayerChoice` | The data the player committed to: target list, parameter values, mode selections. Bound by `IIntentMapper` against a template to produce a `GameCommand`. |
| `GameCommand` | Base type for everything that flows through the pipeline (defined in `triggers/`); subtypes are `GameAction` (executable, queued), `CompositeAction` (inline, no queue), `GameSignal` (no execution). |
| `ICommandHandler<TCommand, TResult>` | Per-command resolver. One per `GameCommand` type. `Handle(cmd) → Task<TResult>` (apply state via mutators, return result). Game-supplied. Typically extends the `GameCommandHandler<TCommand, TResult>` abstract base with `[Inject] Store`. |
| `ICommandValidator<TCommand>` | Per-command legality interface. `Validate(cmd, out reason) → bool`. Validators run before the handler, in DI registration order, short-circuit on first reject. Framework ships `SessionGate` + `MatchStateActionGate`; games append. Uniform across every `GameCommand` subtype. |
| `GameCommandHandler<TCommand, TResult>` | Abstract base for handlers: `[Inject] protected Store Store`. Subclasses override `Handle`. |
| `CommandValidator<TCommand>` | Abstract base for validators: `[Inject] protected Store Store`. Subclasses override `Validate`. |
| `ValidatedCommandHandler<TCommand, TResult>` | Combined base — one class implements both `ICommandValidator<TCommand>` and `ICommandHandler<TCommand, TResult>`. Common case when validation and handling logic live together. |
| `SessionGate` | Framework-default validator. Rejects commands when `engine.Session` is not accepting them (`NotStartedSession` except lifecycle commands; `EndedSession`). |
| `MatchStateActionGate` | Framework-default validator. Rejects commands whose runtime type isn't in `MatchStateSlice.Current.Actions`. Lifecycle commands (`StartMatchCommand`, `EndMatchCommand`, `TransitionStateCommand`) bypass. |
| `ICommandPipeline` | Framework executor: `Validate(cmd, out reason)` (runs the registered validator chain) + `Execute(action) → CommandResult` (full lifecycle for one action). |
| `CommandResult` | Typed return from a handler. Subclasses per command. Populates `HistoryEnvelope.Result`. May carry cancellation flag, redirected target, damage amount, drawn instance, opened decision id, etc. |
| `HistoryEnvelope` | The grouping unit observers consume — one per `GameAction`, with cause metadata (`OriginCommandId`, `OriginIntent` (first only), `Actor`, `SourceInstance`, `Result`, `Entries[]`). |
| `OriginCommandId` | ID of the first command in a chain. Threaded by the engine onto every evaluator follow-up so all chain members share identity. |
| `OriginIntent` | The originating `PlayerIntent` recorded on the first envelope of a chain only. |
| `ActionTemplate` | Snapshot wire-form. Abstract base (`CommandType`, `Source?`); typed subtypes per command (`MoveCardTemplate`, `CastSpellTemplate`, etc.) carrying per-action shape (targets, modes, parameters). |
| `AvailableActionsSnapshot` | `IReadOnlyList<ActionTemplate>` published by `GameRulesRuntime.Evaluate` (and effects mid-execution) via `IAvailableActions.Set`. |
| `TargetSlot` / `ModeOption` / `ParameterSpec` | Building blocks for `ActionTemplate` subtypes that need them; not all templates carry all three. |
| `IntentGateway` | Thin entry layer translating `PlayerIntent` → dispatched `GameCommand` (typically reads the snapshot, runs `IntentMapper`, calls `engine.Dispatch`). Game-supplied. |

## Architecture

The feature has six Models. Each is the canonical home for one load-bearing element of the action pipeline:

1. **`ICommandPipeline`** ([`01-command-pipeline.md`](features/actions/models/01-command-pipeline.md)) — the dispatch flow: validator chain → enqueue → drain (before-fold → handler → after-fold → envelope) → evaluate. Type hierarchy reference. Framework-FIFO routing per ADR-0006.
2. **`IIntentMapper`** ([`02-intent-mapper.md`](features/actions/models/02-intent-mapper.md)) — the boundary that turns `PlayerIntent` (UI/AI/network/etc) into a typed `GameCommand`. Owns `PlayerIntent` and `IntentGateway` as its tied surfaces. *(Rework pending under [ADR-0012 — *forthcoming*].)*
3. **`ICommandHandler<TCommand, TResult>`** ([`03-command-handler.md`](features/actions/models/03-command-handler.md)) — the per-command resolver. `Handle` (writes via mutators, returns typed `CommandResult`). Extends `GameCommandHandler<,>` base with property-injected `Store`.
4. **`HistoryEnvelope`** ([`04-history-envelope.md`](features/actions/models/04-history-envelope.md)) — the grouping unit observers consume. One per `GameAction`. `HistoryState` slice holds the ordered envelope log.
5. **`AvailableActionsSnapshot`** ([`05-available-actions-snapshot.md`](features/actions/models/05-available-actions-snapshot.md)) — typed `ActionTemplate` per command type; `PlayerChoice` shape; eligibility framing. *(Rework pending under [ADR-0012 — *forthcoming*].)*
6. **`ICommandValidator<TCommand>`** ([`06-command-validator.md`](features/actions/models/06-command-validator.md)) — per-command legality. Validator chain machinery; framework defaults (`SessionGate`, `MatchStateActionGate`); `CommandValidator<>` and `ValidatedCommandHandler<,>` bases.

The six shapes that flow through the pipeline (`PlayerIntent` → `ActionTemplate` → `GameCommand` → `StateMutation` → `CommandResult` → `HistoryEnvelope`) are owned across these models plus `triggers/` (`GameCommand` type hierarchy) and `state/` (`StateMutation`).

### The five validation layers

Validation is layered explicitly. **The registered validator chain is authoritative at dispatch** (per [ADR-0010](Product/decisions/0010-validator-handler-split.md)); the other layers narrow the search space, gate UI, or ensure well-formedness. Full breakdown lives in [`Product/decisions/0007-five-validation-layers.md`](Product/decisions/0007-five-validation-layers.md) (project-wide ADR — spans engine + actions + presentation; refined by ADR-0010).

| Layer | Owner feature | Concern | When |
|---|---|---|---|
| Per-state ViewModel affordances | `presentation/` (per-state VM, [ADR-0012 — *forthcoming*]) | Which actions the UI surfaces | UI render |
| `IIntentMapper.Map` | `actions/` (rework pending under ADR-0012) | Well-formedness | Intent → command translation |
| `SessionGate` (framework default) | `actions/` (validator; reads `engine.Session`) | Engine-is-accepting-commands gate | Engine intake (every dispatch) |
| `MatchStateActionGate` (framework default) | `actions/` (validator; reads `MatchStateSlice.Current.Actions`) | Match-state-actions gate | Engine intake (every dispatch) |
| Game-shipped `ICommandValidator<TCommand>` | `actions/` (game-supplied) | Authoritative command-specific legality | Engine intake (every dispatch) |

## Composition

| Model | Role |
|---|---|
| [`01-command-pipeline.md`](features/actions/models/01-command-pipeline.md) | `ICommandPipeline`; dispatch flow; type-hierarchy reference; framework-FIFO routing |
| [`02-intent-mapper.md`](features/actions/models/02-intent-mapper.md) | `IIntentMapper` + `PlayerIntent` + `IntentGateway` (input boundary) — *rework pending under ADR-0012* |
| [`03-command-handler.md`](features/actions/models/03-command-handler.md) | `ICommandHandler<TCommand, TResult>` + `CommandResult` (per-command resolver); `GameCommandHandler<,>` base |
| [`04-history-envelope.md`](features/actions/models/04-history-envelope.md) | `HistoryEnvelope` + `HistoryState` slice (envelope log) |
| [`05-available-actions-snapshot.md`](features/actions/models/05-available-actions-snapshot.md) | `AvailableActionsSnapshot`, typed `ActionTemplate`, `PlayerChoice`, eligibility — *rework pending under ADR-0012* |
| [`06-command-validator.md`](features/actions/models/06-command-validator.md) | `ICommandValidator<TCommand>` + `CommandValidator<>` + `ValidatedCommandHandler<,>`; framework defaults (`SessionGate`, `MatchStateActionGate`) |

## Invariants

Cross-Model rules that hold across both Models and shape how downstream features must use the actions pipeline.

- **One enforced entry point.** Every `GameCommand` enters the pipeline through `engine.Dispatch`. No bypass paths; no in-process injection.
- **The validator chain is authoritative.** Per [ADR-0010](Product/decisions/0010-validator-handler-split.md), registered `ICommandValidator<TCommand>`s (framework defaults first, then game-shipped) run in DI order with short-circuit-on-reject. The per-state ViewModel and IntentMapper are advisory; the chain is final at dispatch.
- **Handlers return typed results.** `CommandResult` subtypes per command (`MoveCardResult`, `DealDamageResult`, etc.). Effects branch on these.
- **One handler per command type.** DI registration is closed-generic (`ICommandHandler<DealDamageCommand, DamageResult>`). Multiple registrations for the same `TCommand` are an error at container build.
- **Handlers are the only layer that issues mutators.** Effects sequence commands; mutators write state; handlers bridge by calling `ctx.Store.Execute(payload)`.
- **One envelope per `GameAction`.** Effect-dispatched siblings share `OriginCommandId` but each gets its own envelope. The historical "child folds into parent envelope" model is retired.
- **`OriginCommandId` is engine-threaded.** The engine copies it from the just-executed command onto every evaluator follow-up; effects do not need to set it.
- **`OriginIntent` is recorded once, on the first envelope.** Subsequent envelopes in the chain share `OriginCommandId` but carry no `OriginIntent`.
- **UI creates intents, not state changes.** `PlayerIntent` is the boundary; everything beyond is framework / game logic.
- **Snapshot is approximate; dispatch is authoritative.** The validator chain runs at dispatch (always); UI gates may show stale affordances; dispatch catches them.
- **`ActionTemplate` is the snapshot wire-form.** `Ref<T>` and `Reference` fields on templates are boundary-correct (per [ADR-0005](Product/decisions/0005-objects-over-ids.md)) — they're transported to presentation and consumed by `IntentMapper`.
- **Command-queue routing is framework-FIFO.** Per [ADR-0006](Product/decisions/0006-two-pipeline-architecture.md), the runtime does not gate intake or route commands; game-style differences in how reactions are scheduled live at the effect-level orchestration layer.

## Boundaries

**Owns:** `PlayerIntent` and `IntentGateway` contracts; `IIntentMapper` interface; `GameCommand` pipeline mechanics (validator chain / before-fold integration / handler / after-fold integration / envelope close); `ICommandHandler<TCommand, TResult>` and the `GameCommandHandler<TCommand, TResult>` typed base; `ICommandValidator<TCommand>` and the `CommandValidator<TCommand>` / `ValidatedCommandHandler<TCommand, TResult>` bases; framework-default validators (`SessionGate`, `MatchStateActionGate`); `CommandResult` and its variant convention; `HistoryEnvelope` shape and `OriginCommandId` / `OriginIntent` threading; `AvailableActionsSnapshot`; `ActionTemplate` (abstract base + typed subtypes per command); `PlayerChoice`; `TargetSlot` / `ModeOption` / `ParameterSpec` building blocks.

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
