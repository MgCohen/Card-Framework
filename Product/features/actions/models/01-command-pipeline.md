---
type: model
status: settled
references: [features/actions/models/03-command-handler.md, features/actions/models/04-history-envelope.md, features/actions/models/06-command-validator.md, features/triggers/models/01-trigger.md, features/effects/models/02-effect-scope.md, features/effects/models/05-pending-effects.md, features/effects/models/06-request-system.md, features/engine/models/03-game-rules-runtime.md, features/engine/models/07-match-state.md, features/presentation/models/01-match-state-view-model.md, decisions/0006-two-pipeline-architecture.md, decisions/0007-five-validation-layers.md, decisions/0010-validator-handler-split.md, decisions/0011-effect-orchestration-dispatch-requests.md, decisions/0012-presentation-per-state-view-models.md]
tags: [#actions, #model, #pipeline]
---

# Command Pipeline — model

> **Amendment (2026-05-11) per [ADR-0013](Product/decisions/0013-engine-pipeline-orchestration.md).** The drain mechanic + per-type routing decision now lives in `engine`, not in this feature. The `actions` feature owns: `GameCommand` family + validators + handlers + `HistoryEnvelope` shape. The `engine` drain pattern-matches command type and invokes the validator chain / handler / trigger fold in the right order per type (full table in ADR-0013). This Model's drain narrative below still describes the *what* (the sequence of validator → before-fold → handler → after-fold → envelope for actions) — but the *who* (orchestrator) is now engine. `ITriggerPipeline` is held by engine, not by this pipeline.
>
> **`GameCommand` family is 4 siblings:** `GameAction`, `GameSignal`, `Request<TResponse>`, `Response`. Request/Response are siblings of GameAction (not subtypes) and participate in the trigger fold per ADR-0013. **`CompositeAction` is removed from the family** — replaced by **`CompositeMutation`** (reusable inline mutation cluster, lives outside the `GameCommand` hierarchy, reached via `scope.Execute(CompositeMutation)` rather than `Dispatch`). Per-type bypass rules: `GameSignal` skips the validator chain and handler (folds only); no other sibling bypasses.

## What it is

The dispatch mechanic: how a `GameCommand` flows through `engine.Dispatch` — registered validator chain (per [ADR-0010](Product/decisions/0010-validator-handler-split.md)), enqueue, drain (before-fold → handler → after-fold → envelope) — and how the queue is drained framework-FIFO per [ADR-0006](Product/decisions/0006-two-pipeline-architecture.md).

```text
engine.Dispatch(command)
  → pipeline.Validate (registered ICommandValidator<TCommand> chain — see actions/06)
      • SessionGate            (framework default, Layer 1)
      • MatchStateActionGate   (framework default, Layer 2)
      • game-shipped validators (registration-order; short-circuit on reject)
  → enqueue (framework FIFO)
  → drain:
      → before-fold (OnBefore<T>: modify / cancel / replace)
      → handler.Handle (read state via Store, invoke mutators, return CommandResult)
      → after-fold (OnAfter<T>: dispatch reactions)
      → envelope publishes
  → GameRulesRuntime.Evaluate (state-based follow-ups; returns next MatchState)
```

The pipeline owns the *flow*; the surrounding load-bearing types live in their own models — `ICommandHandler` ([`03`](features/actions/models/03-command-handler.md)), `HistoryEnvelope` ([`04`](features/actions/models/04-history-envelope.md)), `ICommandValidator` ([`06`](features/actions/models/06-command-validator.md)). Per [ADR-0012](Product/decisions/0012-presentation-per-state-view-models.md), gesture-to-command mapping moves from the retired `IIntentMapper` to per-state `MatchStateViewModel<TState>.Map` (see [`features/presentation/models/01-match-state-view-model.md`](features/presentation/models/01-match-state-view-model.md)); the typed-descriptor wire form (`ActionTemplate`, `AvailableActionsSnapshot`) retires — intake legality lives on `MatchState.Actions` (see [`features/engine/models/07-match-state.md`](features/engine/models/07-match-state.md)) and rich UI projection lives on per-state VM observables.

## Contracts

### `GameCommand` — type hierarchy (five siblings)

Per [ADR-0011](Product/decisions/0011-effect-orchestration-dispatch-requests.md) D14, the family expands to five siblings — adding `Request<TResponse>` and `Response` for the state-linked request system (player-decision routing per [`effects/06-request-system.md`](features/effects/models/06-request-system.md)).

```csharp
abstract record GameCommand;                          // base — all pipeline-dispatchable types
abstract record GameAction         : GameCommand;     // executable, modifiable, queued through engine
abstract record CompositeAction    : GameCommand;     // reusable StateMutation group, runs inline within a handler
abstract record GameSignal         : GameCommand;     // emit-only — trigger surface, no handler

abstract record Request            : GameCommand      // opens a player-decision request; mutates RequestsSlice
{
    public RequestId    Id    { get; init; }
    public IActor       Asker { get; init; }
    public abstract Type ResponseType { get; }
}

abstract record Request<TResponse> : Request          // generic specialization; Dispatch returns Task<TResponse>
    where TResponse : Response
{
    public override Type ResponseType => typeof(TResponse);
}

abstract record Response           : GameCommand      // answers a Request by Id; mutates AnswersSlice + clears RequestsSlice
{
    public RequestId Id { get; init; }
}
```

| Type | Executed by | Modifiable in before-fold | Goes through engine queue |
|---|---|---|---|
| `GameAction` | Handler, via engine queue | Yes | Yes |
| `CompositeAction` | Inline, within parent handler | No | No |
| `GameSignal` | Nothing — written to history | No | No |
| `Request<TResponse>` | `IRequestService.Open` via the `Dispatch` router | No | No (state-linked; not a queueable action) |
| `Response` | `IRequestService.RecordAnswer` via the `Dispatch` router | No | No |

A handler should never dispatch another `GameAction` directly — that's an `IEffect`'s responsibility (per [`effects/01-effect.md`](features/effects/models/01-effect.md)). Handlers also do not open requests; player-decision routing is an effect-author concern via `await scope.Dispatch(request)`.

### `ICommandPipeline`

```csharp
public interface ICommandPipeline
{
    // Resolves every ICommandValidator<TCommand> for the runtime type and runs them
    // in DI registration order; short-circuits on first reject. See actions/06-command-validator.md.
    bool Validate(GameCommand cmd, out string? reason);

    // Runs the full pipeline for one action: before-fold → handler → after-fold → envelope.
    Task<CommandResult> Execute(GameAction action);
}
```

Framework executor: drives the validator chain at intake; resolves the registered `ICommandHandler` for the command type at drain; drives the trigger fold and envelope. Game authors do not implement this; they register handlers (per [`03-command-handler.md`](features/actions/models/03-command-handler.md)) and validators (per [`06-command-validator.md`](features/actions/models/06-command-validator.md)).

### Pipeline mechanics

Per `engine.Dispatch(command)`:

1. **`pipeline.Validate`** (framework). Resolves every `ICommandValidator<TCommand>` registered for the command's runtime type and runs them in DI registration order — short-circuit on first reject. Per [ADR-0010](Product/decisions/0010-validator-handler-split.md), framework defaults (`SessionGate`, `MatchStateActionGate`) register first; game-shipped validators register after. **The validator chain is authoritative** (per [ADR-0007](Product/decisions/0007-five-validation-layers.md), refined by ADR-0010); the runtime no longer participates in intake routing (per [ADR-0006](Product/decisions/0006-two-pipeline-architecture.md)).
2. **Route by kind** (per [ADR-0011](Product/decisions/0011-effect-orchestration-dispatch-requests.md) D14). The router pattern-matches on the command's runtime type:

   | Kind | Routing |
   |---|---|
   | `GameAction` | Enqueue onto framework FIFO; drain (see §Drain below). |
   | `CompositeAction` | Inline expansion in parent handler's scope; no enqueue, no own envelope. |
   | `GameSignal` | Write to history (validators run; no handler). |
   | `Request<TResponse>` | `IRequestService.Open(request)` → mutate `RequestsSlice` → return pending `Task<TResponse>`. Drain pauses on non-empty `RequestsSlice` (per [`effects/05-pending-effects.md`](features/effects/models/05-pending-effects.md)). |
   | `Response` | `IRequestService.RecordAnswer(id, response)` → mutate `AnswersSlice` + clear `RequestsSlice[id]` → resolve the awaited `Task<TResponse>`. |

3. **Drain** (framework, when the `GameAction` queue is non-empty and no drain is in progress):
   1. Dequeue.
   2. **Before-fold** — `ITriggerPipeline.RunBefore` runs `OnBefore<T>` triggers in priority order (per [`triggers/02-trigger-pipeline.md`](features/triggers/models/02-trigger-pipeline.md)). Triggers may modify, cancel (`scope.Cancel`), or replace (`scope.Replace`). On replace: original envelope closes with `Replaced`; replacement dispatches as a fresh command through the channel.
   3. **`handler.Handle(command)`** (game). Reads state through the property-injected `Store` (per [`03-command-handler.md`](features/actions/models/03-command-handler.md)), decides outcome, calls `Store.Execute(payload)` to fan out to registered mutators, returns `Task<TResult>`. Per [ADR-0010](Product/decisions/0010-validator-handler-split.md), handler bodies are atomic from the player-input POV; player decisions ride the request system, not handler `await`s.
   4. **After-fold** — `ITriggerPipeline.RunAfter` runs `OnAfter<T>` triggers. Reactions dispatched via `engine.Dispatch` (separate envelopes; framework FIFO).
   5. **Envelope publishes** with `CommandResult` (per [`04-history-envelope.md`](features/actions/models/04-history-envelope.md)).
4. **Engine drains `PendingEffectsSlice`** (per [ADR-0011 lock 1](Product/decisions/0011-effect-orchestration-dispatch-requests.md)). After the command queue empties and the drain-policy composition allows (no SBA preempts; `RequestsSlice` empty; `MatchState.AllowsDrain`), the engine pops the top `EffectInstance` and invokes `IEffectPipeline.Run(instance, scope)`. Effects run start-to-finish as async coroutines; `await scope.Dispatch(...)` calls re-enter the pipeline via this same flow for sub-commands. See [`effects/05-pending-effects.md`](features/effects/models/05-pending-effects.md).
5. **`GameRulesRuntime.Evaluate`** (game). Returns the next `MatchState` (per [ADR-0009](Product/decisions/0009-engine-as-fsm.md); default body composes SBAs preempt → request pause → pending-effect pause → `ShouldExit` + `Flow.NextAfter`). The engine compares the returned type to `MatchStateSlice.Current.GetType()` and synthesizes `TransitionStateCommand` on a type change.
6. Repeat until the command queue, the pending-effects queue, and the open-requests slice all reach steady state.

### Relationship to triggers

Per [`triggers/01-trigger.md`](features/triggers/models/01-trigger.md):

- `OnBefore<T>` triggers run before the handler. They may modify the command, cancel it (`scope.Cancel`), or replace it (`scope.Replace`).
- `OnAfter<T>` triggers run after the handler. They may dispatch reactions through the channel (`engine.Dispatch`).
- The trigger pipeline runs as part of `pipeline.Execute` — handler authors don't see the fold directly.

### Relationship to effects

Per [`effects/01-effect.md`](features/effects/models/01-effect.md):

- An `Effect` orchestrates multiple `GameAction`s by calling `scope.Dispatch(action)` (immediate, inline, await result).
- Effects never mutate state directly; only handlers invoke mutators.

## Invariants

- **One enforced entry point.** Every `GameCommand` enters the pipeline through `engine.Dispatch`. No bypass paths; no in-process injection.
- **UI affordances are approximate; dispatch is authoritative.** Per-state `MatchStateViewModel<TState>` observables project the affordance surface (per [ADR-0012](Product/decisions/0012-presentation-per-state-view-models.md)); the registered `ICommandValidator<TCommand>` chain runs at dispatch and has the final say. UI gates may show stale options; dispatch catches them.
- **Command-queue routing is framework-FIFO.** Per [ADR-0006](Product/decisions/0006-two-pipeline-architecture.md), the runtime does not gate intake or route commands; game-style differences in how reactions are scheduled live at the effect-level orchestration layer (per [ADR-0011](Product/decisions/0011-effect-orchestration-dispatch-requests.md)).
- **`Ref<T>` and `Reference` fields on `GameCommand`s are boundary-correct.** Per [ADR-0005](Product/decisions/0005-objects-over-ids.md) — typed references flow through the pipeline and the envelope verbatim; serialization rides the typed shape. The retired `ActionTemplate` wire-form was the previous carrier; post-cascade the carrier is the typed `GameCommand` itself (`MoveCardCommand(Ref<Card>, Ref<Zone>, Ref<Zone>)`, etc.) produced by `MatchStateViewModel<TState>.Map(intent)`.

## Decisions

Linked: [`Product/decisions/0006-two-pipeline-architecture.md`](Product/decisions/0006-two-pipeline-architecture.md) — framework command pipeline + game effect pipeline split. Per ADR-0006, command-queue routing is framework-FIFO; the runtime does not gate intake or route commands; effect-level orchestration is the customizable layer where game styles diverge.

Linked: [`Product/decisions/0007-five-validation-layers.md`](Product/decisions/0007-five-validation-layers.md) — five validation layers (refined by ADR-0010: the registered validator chain is authoritative; framework defaults `SessionGate` + `MatchStateActionGate` ride the same chain).

Linked: [`Product/decisions/0010-validator-handler-split.md`](Product/decisions/0010-validator-handler-split.md) — `Validate` splits from `Handle`; `GameCommandHandler<,>` + `CommandValidator<>` bases land; `IHandlerContext` retired; framework intake validators replace the inline gate.

Linked: [`Product/decisions/0011-effect-orchestration-dispatch-requests.md`](Product/decisions/0011-effect-orchestration-dispatch-requests.md) — `GameCommand` family expands to five siblings (`Request<TResponse>` + `Response` added); single async `Dispatch<TResult>(GameCommand)` surface; `PendingEffectsSlice` is the universal effect queue drained by the engine; async-coroutine effects; state-linked request system.

## Examples

### Pile Counter — a complete dispatch

```text
Player drags top card of Pile A onto Pile B
  → IntentGateway produces a typed PlayerIntent: new DropCardIntent(card, A, B)
  → Gateway looks up active VM via ViewModelRegistry.Create(MatchStateSlice.Current)
  → ActiveVM.Map(intent) → MoveCardCommand(card, A, B)         // per ADR-0012
  → engine.Dispatch(MoveCardCommand)
     ├─ Framework validators (registered first per ADR-0010):
     │    SessionGate           → engine.Session is InMatchSession → permitted
     │    MatchStateActionGate  → typeof(MoveCardCommand) ∈ MatchStateSlice.Current.Actions → permitted
     ├─ Game-shipped validators (in DI registration order):
     │    MoveCardValidator.Validate → ok
     ├─ short-circuit-on-reject (none rejected here)
     └─ enqueued (framework FIFO)

Drain begins:
  ├─ Dequeue MoveCardCommand
  ├─ envelope opens (OriginCommandId = newGuid, OriginIntent = DropCardIntent)
  ├─ ITriggerPipeline.OnBefore<MoveCardCommand> → no triggers registered → fold continues
  ├─ MoveCardHandler.Handle:                                    // extends ValidatedCommandHandler<,>
  │    Store.Execute(new MoveCardPayload(card, A, B))          // [Inject] Store on the base; no IHandlerContext
  │      → MoveCardMutator      → ZoneContentsState updates (card moved A→B)
  │      → PileCounterMutator   → per-pile PileState count updates
  │      → LastMoveMutator      → LastMoveState updates
  │      → atomic commit
  │    return new MoveCardResult(card, A, B)
  ├─ ITriggerPipeline.OnAfter<MoveCardCommand> → no triggers → fold continues
  └─ envelope publishes (per 04-history-envelope.md)

GameRulesRuntime.Evaluate(MoveCardCommand, MoveCardResult):                 // per ADR-0009
  → SBA pass (ISBA.Check) — none applies
  → MatchStateSlice.Current.ShouldExit(action, result, scope)?
       All piles match target? → true → exit current state
  → Flow.NextAfter(current) → new PileGameOverState(winner: player1)
  → return PileGameOverState

Engine notices type change:
  → enqueue TransitionStateCommand(newState)
  → TransitionStateHandler:
       Store.Execute(new SetMatchStatePayload(newState))         // MatchStateSlice mutator
       enqueue newState.EnterCommands (e.g., EndMatchCommand(Win(player1)))

Presenter (subscribed to MatchStateSlice):
  → notices state-type change → ViewModelRegistry.Create(PileGameOverState)
  → new PileGameOverViewModel.Bind(state, scope) runs
  → view rebinds via MVVM template selection

(drain continues until queue empty)
```

The envelope is the unit observers consume. Triggers are fold steps in the same execution. Mutators are pure functions of `(currentState, payload, scope)` returning new state. FSM transitions flow through the command pipeline as `TransitionStateCommand`s (per [ADR-0009](Product/decisions/0009-engine-as-fsm.md)) — same envelope shape as any other command. UI rebinds via state-slice subscription on the presenter side (per [ADR-0012](Product/decisions/0012-presentation-per-state-view-models.md)); no out-of-band snapshot push.

## Open follow-ups

- **Scheduler models** — broader scheduling design (interrupt injection, priority queues, MTG-stack semantics) lives at the effect-level orchestration layer per ADR-0006; intersects this pipeline indirectly.

---

*Below: provenance — feature-internal decisions folded on 2026-05-08; element-specific Qs (validation authority, IntentMapper, vocabulary rename) extracted to sibling models on 2026-05-09. The two Qs that remain here are the pipeline-level decisions: flow-level intake (Q1) and command catalog shape (Q2).*

## Options considered

### Q1 — `OnIncomingCommand` is purely flow (formerly ADR-0003 — superseded then folded)

> **Status: superseded by [ADR-0006](Product/decisions/0006-two-pipeline-architecture.md) (2026-05-07).** `OnIncomingCommand` itself is **deleted**. Walking through Stacklands / Hearthstone / MTG-like game styles concretely showed that command-queue routing isn't what games actually customize — what differs is *effect-level* orchestration (when each effect starts, whether effects nest / batch / stack). Per-command primitives (modify / cancel / replace / dispatch) are framework-level via `IEffectScope` during the trigger fold; pause/end gating moved to the framework lifecycle gate on `engine.Dispatch`; game-flow legality lives in `pipeline.Validate`. The "purely flow" principle this ADR locked is preserved in spirit — the framework still distinguishes flow-level concerns from per-command body inspection — but the named hook is gone. The original options-considered analysis is preserved below as historical record per P14.

`OnIncomingCommand` initially mixed three concerns: pause/end gating, queue routing, and command-body inspection. This forced the rule to know about every command shape and made composition fragile.

- **A — Allow `OnIncomingCommand` to read command bodies for early validation.** Rejected — duplicates handler-side validation; couples the rule to every command type.
- **B — Replace `OnIncomingCommand` with per-command callbacks.** Rejected — loses the flow-level concerns (pause/end).
- **C — `OnIncomingCommand` is purely flow: pause/end gating + queue routing; never reads the body.** **Chosen at the time** (now superseded — see ADR-0006).

### Q2 — Generic command primitives, rule-enforced constraints (formerly ADR-0005 — folded)

Specialized commands like `MoveTopCardCommand` accumulate quickly: top-card, bottom-card, random-card, n-cards, etc. Each one duplicates `MoveCardCommand` plus a constraint baked into the type. This explodes the command catalog.

- **A — Ship specialized commands per common pattern.** Rejected — combinatorial explosion of command types.
- **B — Ship one parameterized command** (`MoveCardCommand` with a "selector" enum). Rejected — the selector becomes a stringly-typed sub-language inside the command.
- **C — Ship generic primitives (`MoveCardCommand`); rules enforce constraints via per-template eligibility and `Validate`.** **Chosen.** "Top card", "random card", "from hand only" are constraints expressed by the rule when constructing templates and validated in handlers.

## Rationale

Q1's `OnIncomingCommand`-is-flow keeps the rule shape-agnostic at intake; body inspection belongs to the per-command handler. The named hook itself was deleted by ADR-0006, but the principle (flow-level concerns separate from body inspection) was preserved. Q2's generic primitives keep the command catalog small and stable; constraints live where the game already knows them.

## Consequences

- **Enables:** simple, command-shape-agnostic flow check via the framework lifecycle gate (Q1, post-supersession); small stable command catalog (Q2).
- **Constrains:** rule cannot reject by body at intake (Q1, Q2 push body checks to handlers); rules carry constraint logic that lives in the right place (Q2).
- **Reverts/replaces:** `OnIncomingCommand` deleted per ADR-0006 (Q1).
- **Cascade:** consumed by `engine/03-game-rules-runtime.md` (`GameRulesRuntime` two-method contract: `FilterTriggers` + `Evaluate`); `effects/01-effect.md` (handlers don't dispatch `GameAction`s — sequencing belongs to effects).
