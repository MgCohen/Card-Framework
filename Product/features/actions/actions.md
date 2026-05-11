---
type: feature
status: settled
references: [features/entities/models/02-instance.md, features/zones/models/01-zone-model.md, features/triggers/models/01-trigger.md, features/engine/models/03-game-rules-runtime.md]
tags: [#actions]
---

# actions — feature

## What it is

The framework's command pipeline. A **`PlayerIntent`** (UI gesture) is mapped by the **active per-state ViewModel** (lives in `presentation/`, looked up via `ViewModelRegistry` keyed on `MatchStateSlice.Current.GetType()`; per [ADR-0012](Product/decisions/0012-presentation-per-state-view-models.md)) into a typed **`GameCommand`** that flows through the pipeline (registered `ICommandValidator<TCommand>` chain → framework FIFO → drain → before-fold → handler → after-fold → envelope; per [ADR-0010](Product/decisions/0010-validator-handler-split.md)) and emits a **`HistoryEnvelope`** capturing cause and effects. The pre-cascade global `IIntentMapper` and `AvailableActionsSnapshot` / `ActionTemplate` wire form are retired by ADR-0012; valid command types live on `MatchState.Actions: IReadOnlyList<Type>` (per [ADR-0009](Product/decisions/0009-engine-as-fsm.md)) and per-state UI projections live on the active VM's observables. The `GameCommand` family expands to five siblings (`GameAction` / `CompositeAction` / `GameSignal` / `Request<TResponse>` / `Response`) per [ADR-0011](Product/decisions/0011-effect-orchestration-dispatch-requests.md); `IEffectScope.Dispatch` is the single async surface routing all five.

```
                PlayerIntent (UI gesture; game-defined typed record)
                         │
                ActiveViewModel.Map(intent)    ← in presentation/; per-state polymorphic
                         │
                         ▼
                   GameCommand
                         │
                engine.Dispatch
                         │
        ┌────────────────┴────────────────────────────┐
        │ pipeline.Validate                            │
        │   • SessionGate          (framework default) │
        │   • MatchStateActionGate (framework default) │
        │   • game-shipped validators (DI order)       │ ← authoritative chain
        │ → enqueue (framework FIFO)                   │
        │ → Drain:                                     │
        │   ├─ before-fold (replace/cancel)            │
        │   ├─ handler.Handle                          │ ← invokes mutators via Store.Execute
        │   ├─ after-fold (reactions)                  │
        │   └─ envelope publishes                      │
        │ → Engine drains PendingEffectsSlice          │
        │   (per ADR-0011; only IEffectPipeline call)  │
        └─────────────────────────────────────────────-┘
                         │
                         ▼
                   HistoryEnvelope
                         │
                  Observers / Networking
```

Per [ADR-0006 — two-pipeline architecture](Product/decisions/0006-two-pipeline-architecture.md), command-queue routing is framework-FIFO (the runtime does not gate intake or route commands); effect-level orchestration is the customizable layer where game styles diverge. Per [ADR-0010](Product/decisions/0010-validator-handler-split.md), validation lives in a separate `ICommandValidator<TCommand>` interface; framework defaults register first; game-shipped validators register after.

## Vocabulary

**Feature-owned terms only.** Cross-feature terms used by this feature — `GameAction`, `CompositeAction`, `GameSignal`, `Trigger<T>`, `OnBefore`/`OnAfter`, `TriggerMatch`, `TriggerFoldResult`, `TriggerPhase` (defined in `triggers/`); `IEffectScope`, `Effect`, `Mutator` (defined in `effects/`); `GameRulesRuntime`, `MatchState`, `MatchStateSlice`, `EngineLifecycle`, `EngineState` (defined in `engine/`); `PlayerIntent`, `IntentGateway`, `MatchStateViewModel<TState>`, `ViewModelRegistry` (defined in `presentation/` per [ADR-0012](Product/decisions/0012-presentation-per-state-view-models.md)); `Card`, `CardRef`, `Zone`, `Reference`, `Ref<T>`, `IActor`, `ActorId` — live in their owning features or `Product/ledgers/GLOSSARY.md`; link to their canonical home, don't restate.

| Term | Meaning |
|---|---|
| `GameCommand` | Base type for everything that flows through the pipeline (defined in `triggers/`). Per [ADR-0011](Product/decisions/0011-effect-orchestration-dispatch-requests.md), the family has five siblings: `GameAction` (executable, queued), `CompositeAction` (inline, no queue), `GameSignal` (history-only), `Request<TResponse>` (opens a player-decision request; mutates `RequestsSlice`), `Response` (answers a `Request` by `Id`; mutates `AnswersSlice` + clears `RequestsSlice`). |
| `Request<TResponse>` / `Response` | The two `GameCommand` siblings ADR-0011 adds. Carry only serializable references and values — no predicates. Canonical home: [`effects/06-request-system.md`](features/effects/models/06-request-system.md). |
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

**Retired terms** (live in `Product/ledgers/GLOSSARY.md` §Retired terms; canonical homes archived):

- `IIntentMapper` (global service) — retired by ADR-0012; replaced by per-state `MatchStateViewModel<TState>.Map(intent)` in `presentation/`. Canonical Model archived at [`archive/02-intent-mapper.md`](features/actions/archive/02-intent-mapper.md).
- `PlayerChoice` — retired by ADR-0012; the choice is encoded in the typed `PlayerIntent` record.
- `ActionTemplate` (wire form), `AvailableActionsSnapshot`, `TargetSlot` / `ModeOption` / `ParameterSpec` — retired across ADR-0009 (`IAvailableActions` service + slice) and ADR-0012 (typed-descriptor wire form). Replaced by `MatchState.Actions: IReadOnlyList<Type>` for the intake-gate surface (in `engine/`) and per-state VM observables for the rich UI projection (in `presentation/`). Canonical Model archived at [`archive/05-available-actions-snapshot.md`](features/actions/archive/05-available-actions-snapshot.md).
- `PlayerIntent`, `IntentGateway` ownership — moved from `actions/` to `presentation/` per ADR-0012. The types survive; they just live in presentation now.

## Architecture

The feature has four Models post-cascade. Each is the canonical home for one load-bearing element of the action pipeline:

1. **`ICommandPipeline`** ([`01-command-pipeline.md`](features/actions/models/01-command-pipeline.md)) — the dispatch flow: validator chain → enqueue → drain (before-fold → handler → after-fold → envelope) → engine drains `PendingEffectsSlice` → evaluate. Type hierarchy reference. Framework-FIFO routing per ADR-0006.
2. **`ICommandHandler<TCommand, TResult>`** ([`03-command-handler.md`](features/actions/models/03-command-handler.md)) — the per-command resolver. `Handle` (writes via mutators, returns typed `CommandResult`). Extends `GameCommandHandler<,>` base with property-injected `Store`.
3. **`HistoryEnvelope`** ([`04-history-envelope.md`](features/actions/models/04-history-envelope.md)) — the grouping unit observers consume. One per `GameAction`. `HistoryState` slice holds the ordered envelope log.
4. **`ICommandValidator<TCommand>`** ([`06-command-validator.md`](features/actions/models/06-command-validator.md)) — per-command legality. Validator chain machinery; framework defaults (`SessionGate`, `MatchStateActionGate`); `CommandValidator<>` and `ValidatedCommandHandler<,>` bases.

**Archived 2026-05-11 (per [ADR-0012](Product/decisions/0012-presentation-per-state-view-models.md)):**

- ~~`02-intent-mapper.md`~~ → [`archive/02-intent-mapper.md`](features/actions/archive/02-intent-mapper.md). Global `IIntentMapper` retired in favor of per-state `MatchStateViewModel<TState>.Map` (now in `presentation/`).
- ~~`05-available-actions-snapshot.md`~~ → [`archive/05-available-actions-snapshot.md`](features/actions/archive/05-available-actions-snapshot.md). Typed-descriptor wire form retired (ADR-0009 retired the service+slice; ADR-0012 finished the wire-form retirement). Replaced by `MatchState.Actions` (intake gate) + per-state VM observables (rich projection).

The shapes that flow through the pipeline post-cascade (`PlayerIntent` → `GameCommand` → `StateMutation` → `CommandResult` → `HistoryEnvelope`) are owned across these models plus `triggers/` (`GameCommand` type hierarchy), `state/` (`StateMutation`), and `presentation/` (`PlayerIntent` + per-state `MatchStateViewModel.Map`).

### The five validation layers (post-cascade)

Validation is layered explicitly. **The registered validator chain is authoritative at dispatch** (per [ADR-0010](Product/decisions/0010-validator-handler-split.md)); the other layers narrow the search space, gate UI, or ensure well-formedness. Full breakdown lives in [`Product/decisions/0007-five-validation-layers.md`](Product/decisions/0007-five-validation-layers.md) (project-wide ADR — spans engine + actions + presentation; refined by ADR-0010 + ADR-0012).

| Layer | Owner feature | Concern | When |
|---|---|---|---|
| Per-state VM affordance projection | `presentation/` (per [ADR-0012](Product/decisions/0012-presentation-per-state-view-models.md)) | UI shouldn't surface affordances that would be illegal | VM `Bind` / observable update |
| Per-state VM `Map(intent)` well-formedness | `presentation/` | Translate gesture to a valid command shape (throws / no-ops for unhandled intents in current mode) | UI gesture commit |
| `SessionGate` (framework default validator) | `actions/` (validator; reads `engine.Session`) | Engine-is-accepting-commands gate | Engine intake (every dispatch) |
| `MatchStateActionGate` (framework default validator) | `actions/` (validator; reads `MatchStateSlice.Current.Actions`) | Match-state-actions gate | Engine intake (every dispatch) |
| Game-shipped `ICommandValidator<TCommand>` | `actions/` (game-supplied) | Authoritative command-specific legality | Engine intake (every dispatch) |

## Composition

| Model | Role |
|---|---|
| [`01-command-pipeline.md`](features/actions/models/01-command-pipeline.md) | `ICommandPipeline`; dispatch flow; type-hierarchy reference; framework-FIFO routing |
| [`03-command-handler.md`](features/actions/models/03-command-handler.md) | `ICommandHandler<TCommand, TResult>` + `CommandResult` (per-command resolver); `GameCommandHandler<,>` base |
| [`04-history-envelope.md`](features/actions/models/04-history-envelope.md) | `HistoryEnvelope` + `HistoryState` slice (envelope log) |
| [`06-command-validator.md`](features/actions/models/06-command-validator.md) | `ICommandValidator<TCommand>` + `CommandValidator<>` + `ValidatedCommandHandler<,>`; framework defaults (`SessionGate`, `MatchStateActionGate`) |
| ~~[`02-intent-mapper.md`]~~ | **Archived 2026-05-11** (ADR-0012). See [`archive/02-intent-mapper.md`](features/actions/archive/02-intent-mapper.md). Replacement: per-state `MatchStateViewModel<TState>.Map` in `presentation/`. |
| ~~[`05-available-actions-snapshot.md`]~~ | **Archived 2026-05-11** (ADR-0009 + ADR-0012). See [`archive/05-available-actions-snapshot.md`](features/actions/archive/05-available-actions-snapshot.md). Replacement: `MatchState.Actions` + per-state VM observables. |

## Invariants

Cross-Model rules that hold across both Models and shape how downstream features must use the actions pipeline.

- **One enforced entry point.** Every `GameCommand` enters the pipeline through `engine.Dispatch`. No bypass paths; no in-process injection.
- **The validator chain is authoritative.** Per [ADR-0010](Product/decisions/0010-validator-handler-split.md), registered `ICommandValidator<TCommand>`s (framework defaults first, then game-shipped) run in DI order with short-circuit-on-reject. The per-state ViewModel and IntentMapper are advisory; the chain is final at dispatch.
- **Handlers return typed results.** `CommandResult` subtypes per command (`MoveCardResult`, `DealDamageResult`, etc.). Effects branch on these.
- **One handler per command type.** DI registration is closed-generic (`ICommandHandler<DealDamageCommand, DamageResult>`). Multiple registrations for the same `TCommand` are an error at container build.
- **Handlers are the only layer that issues mutators.** Effects sequence commands; mutators write state; handlers bridge by calling `Store.Execute(payload)`.
- **One envelope per `GameAction`.** Effect-dispatched siblings share `OriginCommandId` but each gets its own envelope. The historical "child folds into parent envelope" model is retired.
- **`OriginCommandId` is engine-threaded.** The engine copies it from the just-executed command onto every evaluator follow-up; effects do not need to set it.
- **`OriginIntent` is recorded once, on the first envelope.** Subsequent envelopes in the chain share `OriginCommandId` but carry no `OriginIntent`.
- **UI creates intents, not state changes.** `PlayerIntent` is the boundary; everything beyond is framework / game logic.
- **Snapshot is approximate; dispatch is authoritative.** Per-binding `Validate` runs at dispatch (always) and may run during snapshot enumeration (on-demand by the rule). UI gates may show stale bindings; dispatch catches them.
- **`MatchState.Actions` is the canonical "what's legal here?" surface.** Per [ADR-0009](Product/decisions/0009-engine-as-fsm.md) and [ADR-0012](Product/decisions/0012-presentation-per-state-view-models.md), the typed-descriptor wire form (`ActionTemplate`) retires; intake gating uses `IReadOnlyList<Type>` directly; rich UI projection lives on per-state VM observables.
- **Command-queue routing is framework-FIFO.** Per [ADR-0006](Product/decisions/0006-two-pipeline-architecture.md), the runtime does not gate intake or route commands; game-style differences in how reactions are scheduled live at the effect-level orchestration layer.

## Boundaries

**Owns:** `GameCommand` pipeline mechanics (validator chain / before-fold integration / handler / after-fold integration / envelope close); `ICommandHandler<TCommand, TResult>` and the `GameCommandHandler<TCommand, TResult>` typed base; `ICommandValidator<TCommand>` and the `CommandValidator<TCommand>` / `ValidatedCommandHandler<TCommand, TResult>` bases; framework-default validators (`SessionGate`, `MatchStateActionGate`); `CommandResult` and its variant convention; `HistoryEnvelope` shape and `OriginCommandId` / `OriginIntent` threading.

**Does not own:**
- `GameCommand` type hierarchy (`GameAction`, `CompositeAction`, `GameSignal`) → `triggers/`
- Trigger fold mechanics (before-fold / after-fold execution) → `triggers/`
- `Effect` orchestration, `IEffectScope` → `effects/`
- `MatchState.Actions` intake-gate surface and `MatchStateSlice` → `engine/`
- `GameRulesRuntime` contract — `FilterTriggers` and `Evaluate` are owned by → `engine/`
- `Mutator<TState, TPayload>` and `Store.Execute(payload)` mechanics → `engine/` (`state/state`)
- Engine lifecycle gate, framework FIFO, drain loop → `engine/`
- Card-specific shape, zone-specific shape → `cards/`, `zones/`
- `PlayerIntent`, `IntentGateway`, `MatchStateViewModel<TState>`, `ViewModelRegistry` — moved to `presentation/` by [ADR-0012](Product/decisions/0012-presentation-per-state-view-models.md)
- UI presentation and per-state UI projection → `presentation/`

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
