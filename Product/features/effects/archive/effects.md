---
type: feature
status: abandoned
archived_on: 2026-10-18
reason: "Written for the old state model (slices, Ref<T>, MatchStateSlice) and never built. Read it for the open questions, not as the contract; the replacement design comes from Product/spikes/framework-roadmap.md."
references: [features/triggers/archive/01-trigger.md, features/actions/archive/01-command-pipeline.md, features/engine/archive/03-game-rules-runtime.md, features/entities/archive/02-instance.md, decisions/0011-effect-orchestration-dispatch-requests.md]
tags: [#effects]
---

> **Archived 2026-10-18.** Written for the old state model (slices, Ref<T>, MatchStateSlice) and never built. Read it for the open questions, not as the contract; the replacement design comes from Product/spikes/framework-roadmap.md.

# effects — feature

## What it is

The framework's lifetime layer. **Effects** are first-class async coroutines (`IEffect.Run(scope) : Task`) that orchestrate sequences of `GameCommand`s — the only layer that can sequence commands. Per [ADR-0011](Product/decisions/0011-effect-orchestration-dispatch-requests.md), effects fire only via the engine draining `PendingEffectsSlice` (the universal queue cast handlers and trigger reactions push to). Inside `Run`, effects use a **single async `Dispatch<TResult>(GameCommand)` surface** on `IEffectScope` routing all five `GameCommand` siblings (`GameAction`, `CompositeAction`, `GameSignal`, `Request<TResponse>`, `Response`). Player decisions ride the **state-linked request system** (`Request<TResponse>` opens a request in `RequestsSlice`; the player's `Response` resolves the awaited Task). Effects participate in **replacement and prevention** through the trigger fold and persist as **ongoing attachment entities** that subscribe to triggers and tick over time.

```
        IEffect.Run(scope) — single async method
              │
              ▼  await scope.Dispatch(command) — routes by runtime type:
              │
       ┌──────┴───────────────────────────────────────────────────────────────┐
       │                                                                       │
   GameAction → before-fold → handler → after-fold → envelope (inline)         │
       │                                                                       │
   CompositeAction → inline expansion in parent (no envelope)                   │
       │                                                                       │
   GameSignal → write to history                                                │
       │                                                                       │
   Request<TResponse> → IRequestService.Open → RequestsSlice += request         │
       │                  ↳ returns pending Task<TResponse>                     │
       │                  ↳ drain pauses on RequestsSlice non-empty             │
       │                  ↳ player's Response arrives via the same Dispatch     │
       │                  ↳ IRequestService.RecordAnswer → resolves the Task    │
       │                                                                       │
   Response → IRequestService.RecordAnswer → AnswersSlice += / RequestsSlice -= │
       │                                                                       │
       └──────────────────────────────────────────────────────────────────────┘

Engine drain (only call site of IEffectPipeline.Run):
  pop EffectInstance from PendingEffectsSlice → await effectPipeline.Run(instance, scope)
```

Effects are not authored as `Task<Result>` returned from a handler — that would conflate animation, decision, and simulation pauses. Each layer (presentation animation, request-system mid-effect decisions, engine drain) owns its own continuation; effects compose across layers via `IEffectScope`. Handler bodies stay atomic from the player-input POV (per [ADR-0010](Product/decisions/0010-validator-handler-split.md)) — they push effects onto `PendingEffectsSlice` and return; the engine drains effects, which then dispatch requests if player input is needed.

## Vocabulary

**Feature-owned terms only.** Cross-feature terms used by this feature — `GameCommand`, `GameAction`, `CompositeAction`, `GameSignal`, `Trigger<T>`, `OnBefore`/`OnAfter`, `TriggerMatch`, `TriggerFoldResult`, `ITriggerSystem`, `ITriggerPipeline`, `TriggerPhase` (defined in `triggers/`); `GameRulesRuntime`, `Evaluate`, `MatchState`, `MatchStateSlice`, `Store`, `Mutator<TState, TPayload>`, `IStateScope`, `IStoreScope` (defined in `engine/`); `CommandResult`, `HistoryEnvelope`, `ICommandValidator<TCommand>`, `GameCommandHandler<,>`, `ValidatedCommandHandler<,>` (defined in `actions/`); `PlayerIntent`, `IntentGateway`, `MatchStateViewModel<TState>`, `ViewModelRegistry` (defined in `presentation/` per [ADR-0012](Product/decisions/0012-presentation-per-state-view-models.md)); `IModifierFactory`, `ActiveModifier`, `ModifierId`, `ModifierSource`, `IEntityVariableStorage`, `InstanceWithSlices<TDef>`, `EntityState` (defined in `entities/`); `Reference`, `Ref<T>`, `Card`, `Zone` — live in their owning features or `Product/ledgers/GLOSSARY.md`; link to their canonical home, don't restate.

| Term | Meaning |
|---|---|
| `IEffect` | First-class type. Per-instance orchestrator. `Task Run(IEffectScope scope)`. One method; async coroutine; the C# async runtime holds continuations across `await`s. Internals opaque to the framework. |
| `IEffectScope` | Effect's runtime surface: `State` (read-only), `Store` (catalog reads), `Caster`, `Modifiers`, single async `Dispatch<TResult>(GameCommand)`, `Cancel`, `Replace`. No mutation channel. |
| `EffectInstance` | Wire-form record `(Source, EffectId, Payload)` — what gets pushed onto `PendingEffectsSlice`. Snapshot-friendly; flat; carries bound parameters captured at fire time. |
| `IEffectPipeline` | Engine-only service. `Task Run(EffectInstance, IEffectScope)`. The framework ships `InlineEffectPipeline` as default; resolves `EffectInstance` to a runnable `IEffect` through `IEffectDefinitionCatalog`. **Only the engine holds the reference and invokes `Run`.** |
| `PendingEffectsSlice` | Framework-owned slice carrying the ordered queue of `EffectInstance`s awaiting drain. LIFO by default; FIFO per game policy. Universal channel — cast handlers, trigger reactions, FSM `EnterCommands` all push here. |
| `RequestsSlice` / `AnswersSlice` | State-linked slices for player decisions. `RequestsSlice.Open` holds pending requests (wire format clients see); `AnswersSlice.By` holds recorded responses (replay audit trail). `IRequestService` is the only writer. |
| `IRequestService` | Engine-internal façade: `Open(request)`, `RecordAnswer(id, response)`, `Cancel(id)`. Reached by the `Dispatch` router; not by effect-author code. |
| Layer hierarchy | IEffect → GameAction → CompositeAction. Only effects can sequence; only handlers invoke mutators; effects never mutate. |
| Two dispatch paths | `engine.Dispatch(action)` (queued GameAction; framework FIFO; own envelope) vs `scope.Dispatch(command)` (single async surface for all five `GameCommand` siblings; effect awaits). Not interchangeable. |
| Player decisions as `Request<TResponse>` | A decision (target, choice, etc.) is a `Request<TResponse>` opened by `await scope.Dispatch(request)`. Resolves with a typed `TResponse` once the player's `Response` arrives. No `DecisionId`; `RequestId` is the canonical identifier. |
| Replacement | Before-fold outcome where a trigger calls `scope.Replace(next)` — original action's envelope closes with `Replaced`; `next` dispatches as fresh command through its own full fold. |
| Prevention | Modification with a zero floor (e.g. shield reduces damage to 0). Stateful prevention is an ongoing-effect attachment that owns its own state. |
| Attachment entity | Ongoing effect modeled as an entity (definition, `Ref<T>`, variables, subscriptions). Created via `CreateAndAttachAction`, destroyed via `DestroyEntityAction`; framework Cleanup hook unregisters subscriptions. |
| Mutator payload | Dedicated record type (e.g. `MoveCardPayload`), not the command. One payload may fan out to N registered mutators; `Store.Execute(payload)` runs every one and commits atomically. |

**Retired terms** (live in `Product/ledgers/GLOSSARY.md` §Retired terms):

- `ICommandAwaiter` — handler-side decision-await mechanism. Superseded by the request system. Canonical home archived at [`04-command-awaiter.md`](features/effects/archive/04-command-awaiter.md).
- `IHandlerContext` — handler aggregator. Retired by [ADR-0010](Product/decisions/0010-validator-handler-split.md). Handlers extend `GameCommandHandler<,>` (property-injected `Store`).

## Architecture

The feature has four Models post-ADR-0011. Each is the canonical home for one load-bearing C# type of the effects layer:

1. **`IEffect`** ([`01-effect.md`](features/effects/archive/01-effect.md)) — the orchestrator interface (`Task Run(IEffectScope scope)`); async-coroutine shape; layer hierarchy; engine-only firing rule.
2. **`IEffectScope`** ([`02-effect-scope.md`](features/effects/archive/02-effect-scope.md)) — the effect's runtime surface: read-only `State`, catalog `Store`, `Caster`, `Modifiers`, single async `Dispatch<TResult>(GameCommand)`, `Cancel`, `Replace`. No mutation channel.
3. **`PendingEffectsSlice`** ([`05-pending-effects.md`](features/effects/archive/05-pending-effects.md)) — the universal effect-firing queue. Cast handlers, trigger reactions, and FSM `EnterCommands` all push here; the engine drains. Drain-policy composition (SBA preempt, request pause, pending-effect pause) layered into `Evaluate`.
4. **Request system** ([`06-request-system.md`](features/effects/archive/06-request-system.md)) — the state-linked service for player decisions: `Request<TResponse>` / `Response` / `RequestsSlice` / `AnswersSlice` / `IRequestService`. Effects open requests via `await scope.Dispatch(request)`; the player's `Response` resolves the awaited Task.

The execution arc: an `IEffect` (1) calls `scope.Dispatch(...)` on its `IEffectScope` (2); for a `GameAction` the dispatched action's handler runs through the trigger fold (per [`triggers/01-trigger.md`](features/triggers/archive/01-trigger.md)) — replacement / prevention semantics live there as the three before-fold outcomes; for a `Request<TResponse>` the request system (4) opens the request in `RequestsSlice`, returns a pending `Task<TResponse>`, and resolves it once the player's `Response` arrives. Effects fire only via the engine draining `PendingEffectsSlice` (3) — cast handlers, trigger reactions, and FSM-driven runs all push instances; the engine pops and invokes `IEffectPipeline.Run`. Handler shape (`GameCommandHandler<,>` base; property-injected `Store`) lives in [`actions/03-command-handler.md`](features/actions/archive/03-command-handler.md) per [ADR-0010](Product/decisions/0010-validator-handler-split.md); the validator chain that gates intake (including framework defaults `SessionGate` + `MatchStateActionGate`) lives in [`actions/06-command-validator.md`](features/actions/archive/06-command-validator.md). Ongoing effects (auras, stateful prevention, durations) are realized as **attachment entities** — see [`entities/02-instance.md` §Attachment-entity usage](features/entities/archive/02-instance.md). Pipeline shape lives in [`actions/01-command-pipeline.md` §Pipeline mechanics](features/actions/archive/01-command-pipeline.md).

### Three layers, capabilities

| Layer | Read state | Mutate state | Dispatch command | Await player |
|---|---|---|---|---|
| **`IEffect`** | Yes (via `scope.State`) | **No** | Yes (single async `scope.Dispatch<TResult>(GameCommand)`) | Yes (via `await scope.Dispatch(request)` per the request system) |
| **`ICommandHandler`** | Yes (via property-injected `Store`) | Yes — by invoking mutators | No (handlers don't dispatch) | **No** (atomic from the player-input POV per [ADR-0010](Product/decisions/0010-validator-handler-split.md)) |
| **`Mutator<TState, TPayload>`** | Yes (current state in; sibling slices via `IStateScope`) | **Yes — only producer of new state** | No | No |

Three principles fall out of this table:

- **Mutation flows through mutators, never directly.** A mutator's `Change(state, payload, scope)` returning a new `TState` is the only path that produces state. Handlers don't write state inline; effects can't write state at all.
- **Only handlers invoke mutators.** Mutators are not auto-fired off command dispatch. The handler for a command decides which mutators to run and with what payload.
- **Effects orchestrate, never mutate.** An effect's job is to sequence dispatches and thread their results. State change happens entirely inside the dispatched actions' handlers.

### Cancellation discipline

Two cancellation cases exist; both surface through the same channel.

**Action cancelled by the before-fold.** A registered `OnBefore<T>` returns `FoldResult.Cancel(reason)`. The action's handler never runs. The effect's `await scope.Dispatch(...)` resolves with a `CommandResult` carrying the cancellation. The effect inspects the result and decides whether to continue:

```csharp
var damage = await scope.Dispatch(new DealDamageAction { Target = t, Amount = 6 });
if (damage.IsCancelled) return;        // shield absorbed; effect ends gracefully
await scope.Dispatch(new MoveToDiscardAction { Source = source });
```

**Effect aborts itself.** The effect calls `scope.Cancel()` when it knows it cannot continue (e.g. the targeted entity has been removed by a previous dispatch). The effect's `Execute` task completes; no further dispatches run.

The framework invariant: **cancellation of one `GameAction` never automatically cancels the orchestrating effect.** The effect is the only place that decides whether the chain should continue.

## Composition

| Model | Role |
|---|---|
| [`01-effect.md`](features/effects/archive/01-effect.md) | `IEffect` async-coroutine contract + layer hierarchy + engine-only firing rule |
| [`02-effect-scope.md`](features/effects/archive/02-effect-scope.md) | `IEffectScope` — single async `Dispatch<TResult>(GameCommand)` surface; routing-by-kind |
| [`05-pending-effects.md`](features/effects/archive/05-pending-effects.md) | `PendingEffectsSlice` + `EffectInstance` + `IEffectPipeline` + drain-policy composition |
| [`06-request-system.md`](features/effects/archive/06-request-system.md) | `Request<TResponse>` / `Response` / `RequestsSlice` / `AnswersSlice` / `IRequestService` + `RequestId` |
| ~~[`03-handler-context.md`]~~ | **Archived 2026-05-11** — `IHandlerContext` retired by [ADR-0010](Product/decisions/0010-validator-handler-split.md). See [`03-handler-context.md`](features/effects/archive/03-handler-context.md). Handler shape lives in `actions/03`. |
| ~~[`04-command-awaiter.md`]~~ | **Archived 2026-05-11** — `ICommandAwaiter` retired by [ADR-0011](Product/decisions/0011-effect-orchestration-dispatch-requests.md). See [`04-command-awaiter.md`](features/effects/archive/04-command-awaiter.md). Decision routing lives in the request system (`06-request-system`). |

Cross-feature content this feature consumes but does not own: before-fold outcomes (modify / cancel / replace) and replacement-of-replacement live in [`triggers/02-trigger-pipeline.md`](features/triggers/archive/02-trigger-pipeline.md); ongoing-effect attachment-entity pattern lives in [`entities/02-instance.md`](features/entities/archive/02-instance.md); handler shape and the validator chain live in [`actions/03-command-handler.md`](features/actions/archive/03-command-handler.md) and [`actions/06-command-validator.md`](features/actions/archive/06-command-validator.md).

## Invariants

Cross-Model rules that hold across the four Models and shape how downstream features must use effects.

- **Layer separation.** Only `IEffect`s sequence `GameCommand`s; only handlers invoke mutators; effects never mutate. A `GameAction` handler may use `CompositeAction`s internally but never dispatches other `GameAction`s — that's the effect's responsibility.
- **Engine-only effect firing.** Per [ADR-0011 lock 1](Product/decisions/0011-effect-orchestration-dispatch-requests.md), exactly one place in the system holds an `IEffectPipeline` reference — the engine's drain loop. Cast handlers, trigger reactions, and FSM-driven runs all push onto `PendingEffectsSlice`; the engine drains.
- **Single async `Dispatch` surface.** Per [ADR-0011 lock 2](Product/decisions/0011-effect-orchestration-dispatch-requests.md), `IEffectScope.Dispatch<TResult>(GameCommand)` routes all five `GameCommand` siblings. `scope.Dispatch` is the only call effects make to interact with the pipeline.
- **Async-coroutine effects.** Per [ADR-0011 lock 4](Product/decisions/0011-effect-orchestration-dispatch-requests.md), `IEffect.Run(scope)` is one async method; the C# async runtime carries continuations across `await`s; no `Step` field, no manual re-pushing.
- **Strictly serial per drain loop.** Only one effect's `Run` is in flight at a time. Awaiting a `Response` does not yield draining to a parallel sibling on `PendingEffectsSlice`.
- **Dispatch semantics are not interchangeable.** `scope.Dispatch` is immediate/inline-when-possible (effect awaits result; suspends only on `Request<TResponse>`); `engine.Dispatch` is deferred/FIFO (own envelope). Effects always use `scope`; trigger reactions push to `PendingEffectsSlice` and engine-side flows use `engine`.
- **Mutation discipline.** Mutators (`Mutator<TState, TPayload>.Change`) are the only producer of new slice state. Only handlers invoke mutators. Effects cannot mutate. Mutator payloads are dedicated types, not commands.
- **Subscription ownership.** Attachment entities (and any entity that registers triggers) own their subscriptions via `Ref<T>`. Cleanup-on-destruction unregisters every subscription owned by that instance — no manual cleanup required.
- **Fold determinism.** Replacements carry explicit priority. The before-fold processes them in rank order; ties break by stable, replay-safe rule (e.g., source entity ref). No registration-time-based ordering, no nondeterminism.
- **Request-system player decisions.** Player decisions ride `Request<TResponse>` / `Response` through the same async `Dispatch` surface. `RequestId` is the canonical identifier. Multiple open requests are supported (`RequestsSlice.Open` is a dictionary keyed by `RequestId`). No `DecisionId`, no type-only routing.
- **Drain pauses on open requests.** When `RequestsSlice.Open` is non-empty, the engine does not drain `PendingEffectsSlice` and the FSM does not transition (per `Evaluate`'s default body composition).
- **Cancellation never automatically cancels the orchestrating effect.** If a dispatched action is cancelled by the before-fold, the effect's `await scope.Dispatch(...)` resolves with a cancelled `CommandResult` — the effect inspects and decides whether to continue.
- **Save/load only at quiescent points.** Async-coroutine effects cannot serialize mid-`await`; save/load happens when no `Run` is in flight.

## Boundaries

**Owns:** the `IEffect` async-coroutine contract; layer hierarchy contract (IEffect / GameAction / CompositeAction); two-dispatch-paths semantics; `IEffectScope` (single async surface routing five `GameCommand` siblings); `PendingEffectsSlice` + `EffectInstance` + `IEffectPipeline` (the universal queue + engine-only firing rule); `RequestsSlice` / `AnswersSlice` / `IRequestService` + `Request<TResponse>` / `Response` / `RequestId` (the state-linked request system); mutation discipline (effects-can't-mutate); cancellation discipline (cancel never auto-cancels orchestrating effect); drain-policy composition (the guards `Evaluate` consults).

**Does not own:**
- `GameCommand` type hierarchy (the type system) → `triggers/`
- Trigger registration, fold execution mechanics, before-fold outcomes, replacement priority → `triggers/`
- Attachment-entity usage pattern (ongoing effects realized as entities) → `entities/`
- `GameRulesRuntime.FilterTriggers` (availability policy) → `engine/`
- Engine framework FIFO routing, drain → `engine/`
- `MatchState.Actions` (intake-legal command types), `MatchStateSlice`, `SessionState` hierarchy → `engine/`
- Pipeline envelope, history entries → `actions/`
- `MatchStateViewModel<TState>.Map(intent)` implementation → `presentation/`
- `IModifierFactory` implementation → `entities/`
- `store.Catalog` mechanism → `engine/`
- Animation handoff, presentation-side async → `presentation/`

## Dependencies

*Mirrors the front-matter `references:` array in human-readable form (per §3).*

- **Upstream:** `triggers/` (type hierarchy, `OnBefore`/`OnAfter`, `ITriggerPipeline`, fold mechanic), `actions/` (pipeline, envelope, validator chain, `GameCommandHandler<,>` base), `engine/` (`GameRulesRuntime`, `MatchStateSlice`, `MatchState.Actions`, evaluation loop, `Store`, `Mutator`), `entities/` (entity bridge, modifier factory, attachment-entity pattern via `InstanceWithSlices<TDef>`), `presentation/` (`PlayerIntent` ownership; per-state VM `Map`).
- **Downstream:** game-side rules code (defines `Effect` subclasses, `AbilityDefinition`s, ongoing-effect attachments), `presentation/` (consumes `DecisionOpenedSignal` / `DecisionResolvedSignal` to render decision UI).

## Open

See `ledgers/OPEN-ITEMS.md`:

- ~~**`oq-effect-pipeline`**~~ — *Resolved 2026-05-11 per [ADR-0011](Product/decisions/0011-effect-orchestration-dispatch-requests.md).* Effect-level orchestration is settled: engine-only firing via `PendingEffectsSlice`; async-coroutine effects; unified `Dispatch`; state-linked request system. The 2026-05-08 effect-pipeline spike is archived at `Product/spikes/archive/effect-pipeline.md`.
- **`oq-effect-authoring`** — the "recipe" a `CardDefinition` holds for constructing an `IEffect` from `(Source, EffectId, Payload)` at fire time. Authoring vocabulary (graphs / code / hybrid) is the open design surface. Tied to `bl-authoring-feature`.
- **`oq-effect-cancellation-on-match-end`** — thread `CancellationToken` through `IEffectScope.Dispatch`; engine cancels on match-end and GCs `RequestsSlice` / `AnswersSlice`. Implementation pass; resolution path locked.
- **`oq-effect-save-load`** — async-coroutine effects cannot be serialized mid-`await`. Save/load is supported at quiescent points; mid-effect save/load would force state-machine effects — separate spike.
- **Effect per-instance state** — where ongoing-effect counters live (effect object, `EntityState`, dedicated slice). Left to ongoing-effects + authoring follow-up.
- **`CommandResult` cancellation shape** — flag on base record vs typed `CancelledResult` subtype. Cosmetic; pin during command-pipeline cleanup.
- **Entity destruction lifecycle** (`oq-entity-destruction`) — what "destroyed" means, when Cleanup fires, what happens to in-flight dispatches referencing destroyed entities.
- **Multiplayer visibility on requests** — must other clients see open `RequestsSlice` entries before the deciding player responds, or is full envelope on resolution sufficient? Folds into visibility design.
- **Mulligan lifecycle** — opens via the request system as a setup-phase `MulliganRequest` per player. Concrete shape pins during the first game's setup phase.
