---
type: spike
status: draft
tags: [#spike, #engine, #runtime, #fsm, #presentation, #state, #effects, #dispatch]
---

# FSM Architecture — Design Sketch

> **Status: draft / iterating.** Working sketch from the FSM-architecture design pass + the open-questions walk that followed on 2026-05-10. Settles the original points 1–6 (engine-as-FSM, state-owned actions, intent-mapper-as-presentation, presentation-state owns mapper, `IHandlerContext` removed + validator/handler split, handler responsibilities) plus the Q-by-Q walk: session-lifecycle layering, validator pipeline shape, runtime↔effect-pipeline boundary, request/targeting routing. Original deferral on point 7 (history envelope reshape) still stands; original deferral on point 8 (request / targeting routing) is now resolved via the state-linked-service request system (D17).
>
> **Self-contained** — readable standalone. **Not yet propagated to canonical Models.** Resolution path: lift to a new ADR (0009 or higher) and patch `engine/01`, `engine/03`, `engine/04`, `actions/01`, `actions/02`, `actions/03`, `effects/01`, `effects/02`, `effects/03`, `effects/04`, `presentation/01`, `state/state.md`, plus glossary + open-items.

## TL;DR

**FSM core.**

- **Engine *is* the FSM.** `runtime.Evaluate` returns a `MatchState`. The engine recognizes a new state and dispatches a `TransitionStateCommand` through the normal pipeline.
- **`MatchStateSlice` wraps a polymorphic `MatchState`.** Sealed wrapper carries `Current`; mutators target the wrapper with a guard.
- **State is data + transition predicate.** `Actions`, `EnterCommands`, `ShouldExit(action, result, scope)`. No `Next` method — successor knowledge in `ITurnFlow.NextAfter`. `Evaluate` collapses to a few lines.
- **Cross-cutting overrides are SBAs.** Game-over, lethal damage cleanup, legend rule. Each is a one-method `Check(scope) → MatchState?` that preempts the `ShouldExit` / `Flow` path.

**Lifecycle layering.**

- **Match FSM lives in the Store** (`MatchStateSlice` + game-defined states).
- **Session lifecycle is an engine field**, not a slice. Typed sealed hierarchy on `engine.Session` (replaces / subsumes the old `EngineLifecycle` enum). Hybrid future: host code owns session orchestration; engine exposes hooks.

**Pipeline architecture.**

- **`Validate` splits from `Handle`.** `ICommandValidator<TCommand>` and `ICommandHandler<TCommand, TResult>` are separate. N validators + 1 handler resolved per command.
- Validator policy: **short-circuit on first reject; registration order; independent (no shared context); state via DI** (same `[Inject] Store` pattern as handlers).
- **Validators are uniform across all `GameCommand` subtypes.** Same machinery for actions, requests, responses, signals.
- `IHandlerContext` removed. Handlers inherit from `GameCommandHandler<,>` base; specific services via constructor.

**Effect orchestration.**

- **Layering rule: only the engine calls the effect pipeline.** Cast handlers and triggers do not dispatch effects directly (rolls back the 2026-05-08 Q1 lock). Triggers ride the `OnBefore<T>` / `OnAfter<T>` fold inside `Dispatch` — orthogonal to the layering, not a violation.
- **D-universal: a single queue/stack of pending effects** (`PendingEffectsSlice`). Commands push; engine drains. Game tunes drain timing by overriding `Evaluate`.
- **Effects are async coroutines.** `IEffect.Run(scope) : Task`. Author writes one method that runs start-to-finish with internal `await scope.Dispatch(...)`. No state-machine steps; the C# async runtime holds continuations. Save/load mid-effect not supported by default.

**Dispatch and the `GameCommand` family.**

- Five `GameCommand` siblings: `GameAction`, `CompositeAction`, `GameSignal`, `Request<TResponse>`, `Response`.
- **Single `Dispatch` surface for effects.** `Task<T> Dispatch<T>(GameCommand)`. Async; routes by kind; everything goes through the same drain.

**Request / response.**

- State-linked service pattern: `RequestsSlice` + `AnswersSlice` + `IRequestService`.
- **Requests carry valid IDs (serializable references), never predicates.** Game-side logic computes the valid set before opening; the request is a `Request<TResponse>` typed record.
- `RequestsSlice` is the wire format clients see — UI subscribes and renders pickers from open requests. VM in request-mode returns a `Response` from `Map(intent)`.

**Presentation.**

- Per-state polymorphic `MatchStateViewModel<TState>`; `ViewModelRegistry` maps state type → factory.
- Per-VM `Map(intent) → GameCommand` (or `Response`). Replaces the global `IIntentMapper`.

**Drops.**

`AffordanceMap`, global `IIntentMapper`, `IAvailableActions` service, `AvailableActionsState` slice, `ActionTemplate` (wire form), `IHandlerContext`, `ICommandAwaiter`. `Actions` stays on `MatchState` as `IReadOnlyList<Type>` for the engine gate + tooling.

## Context (locked from prior architecture)

What the architecture looked like before this pass:

- `runtime.Evaluate(action, result) → IReadOnlyList<GameAction>` — returns follow-up actions; engine threads them into FIFO.
- Available actions published via `IAvailableActions.Set(snapshot)` — runtime-driven service; written from inside `Evaluate` or mid-handler.
- `AvailableActionsState` slice carries the `AvailableActionsSnapshot` (typed `ActionTemplate` list).
- Presentation projects the snapshot through a game-supplied `AffordanceMap` (UI-friendly form) and a single global `IIntentMapper` that switches on every `ActionTemplate` subtype.
- `IHandlerContext` exposes `Store`, `Actions` (snapshot writer), `Resolver` (decision awaiter), `Modifiers`.
- Decisions ("pick a target") use `ICommandAwaiter` — handler calls `ctx.Resolver.Wait<T>()` to suspend until a matching command arrives.
- (From the 2026-05-08 effect-pipeline pass, Q1) Cast handlers and the trigger fold both produce `EffectInstance(Source, EffectId, Payload)` and route them through `IEffectPipeline.RunEffect`.

What changed in this pass — the runtime had quietly drifted toward "state evaluator" already (per `oq-runtime-as-state-evaluator`). This pass commits to that direction structurally and then walks downstream consequences: validator pipeline, effect-firing layering, dispatch unification, and request/targeting routing.

## Big decisions

### D1 — Engine *is* the FSM

`runtime.Evaluate(action, result) → MatchState`. No more list of follow-up actions. The engine recognizes "newState != current.Current" and dispatches a `TransitionStateCommand(newState)`, which goes through the same pipeline as any other command. The transition handler mutates `MatchStateSlice` and enqueues `newState.EnterCommands`.

Implication: there is no out-of-pipeline path. Even state transitions are commands. Free auditing in the history envelope; standard subscription channel for presentation.

### D2 — `MatchStateSlice` wraps a polymorphic `MatchState`

Scaffold keys slices on `(Reference, typeof(TState))` with **exact-type match** on the secondary key. There is no `IsAssignableFrom` walk. Consequence: a polymorphic `MatchState` can't itself be a slice. Use a sealed wrapper:

```csharp
public sealed record MatchStateSlice(MatchState Current) : BaseState;

public abstract record MatchState : BaseState {
    public virtual IReadOnlyList<Type>       Actions       => [];
    public virtual IReadOnlyList<GameAction> EnterCommands => [];
    public virtual bool ShouldExit(GameAction a, CommandResult r, IStateScope s) => false;
}
```

Single subscription channel (`MatchStateSlice`); pattern-match `Current` to read phase data. Mutators that touch phase-specific data target `MatchStateSlice` with a guard.

### D3 — State is data + one predicate, not behavior

Each concrete `MatchState` carries:

- **`Actions`** — list of valid `GameCommand` types. Used by the engine's pre-`Validate` gate and by tooling.
- **`EnterCommands`** — list of `GameAction`s the engine enqueues on entry. Pure data.
- **`ShouldExit`** — pure predicate over `(action, result, scope)`. No mutations, no dispatch, no successor knowledge.
- **Phase-specific fields** — `Attackers`, `Blockings`, `HandCount`, etc.

No `OnEnter(ctx)`. No `Next(action) → MatchState`. Behavior on the state is limited to the `ShouldExit` predicate; everything else is data.

### D4 — Successor knowledge lives in `ITurnFlow`, not on states

```csharp
public interface ITurnFlow {
    MatchState NextAfter(MatchState current);
}
```

Single source of turn-structure truth. Adding a phase = new state class + new factory entry in the VM registry + new `NextAfter` arm. Tweaking turn order = edit `ITurnFlow` only; states don't change.

Visual-scripting friendly: `NextAfter` is essentially a transition table. Each node = MatchState type, each edge = `NextAfter` arm with optional guard.

### D5 — Cross-cutting state-driven overrides are SBAs

State-based actions (game-over, lethal damage cleanup, legend rule) are state-driven, not command-driven. They run before the normal `ShouldExit` / `Flow` path:

```csharp
public interface ISBA {
    MatchState? Check(IStateScope scope);   // null = no override
}
```

`Evaluate` body:

```csharp
public override MatchState Evaluate(GameAction action, CommandResult result) {
    foreach (var sba in stateBasedActions)
        if (sba.Check(State) is { } over) return over;

    var current = State.Get<MatchStateSlice>().Current;
    return current.ShouldExit(action, result, State)
        ? Flow.NextAfter(current)
        : current;
}
```

The default body is mechanical, but `Evaluate` stays on the runtime. The runtime is the orchestrator with a reason to exist — it owns *when* things happen (e.g., when stack effects resolve in a Magic-shaped game; see "Open questions" for the stack-pattern note). Games override `Evaluate` only when they need custom orchestration; the default body uses `Flow + SBAs + ShouldExit`. Simpler games may put more logic in `Evaluate` than complex games (which distribute logic across states + handlers + Flow).

> Drain-policy composition (D14, D15): the default `Evaluate` body grows two more guards before the `ShouldExit` line — pause if `RequestsSlice` is non-empty, drain `PendingEffectsSlice` if non-empty. See D14 and D15 for the full body.

```csharp
public abstract class GameRulesRuntime<TDefinition> {
    protected abstract ITurnFlow           Flow              { get; }
    protected abstract IReadOnlyList<ISBA> StateBasedActions { get; } = [];

    public virtual MatchState Evaluate(GameAction action, CommandResult result) {
        // Default body: SBAs preempt; pause on open requests; drain pending effects;
        // otherwise current.ShouldExit + Flow.NextAfter.
        // Override to insert custom orchestration (e.g., stack-resolution timing).
    }
}
```

### D6 — Transitions go through the command pipeline

Engine, post-`Evaluate`:

```csharp
var newState = runtime.Evaluate(action, result);
var current  = scope.Get<MatchStateSlice>().Current;
if (newState.GetType() != current.GetType())
    Enqueue(new TransitionStateCommand(newState));
```

Transition fires on **type-equality**, not reference-equality — records are immutable so "stay" returns are usually `current` but it's easy to slip up and construct a fresh same-typed instance. Type-equality matches intent ("did we move to a different state class?") and tolerates the slip.

`TransitionStateHandler` (framework-internal):

```csharp
public sealed class TransitionStateHandler
    : GameCommandHandler<TransitionStateCommand, TransitionResult>
{
    [Inject] protected IEngineQueue Engine { get; private set; } = null!;

    public override Task<TransitionResult> Handle(TransitionStateCommand cmd) {
        Store.Execute(new SetMatchStatePayload(cmd.NewState));   // mutate slice
        foreach (var enter in cmd.NewState.EnterCommands)
            Engine.Enqueue(enter);                                // chained via engine
        return Task.FromResult(new TransitionResult());
    }
}
```

Self-transition (same type returned) → engine no-ops. Re-enter semantics not exposed today; add explicit `Reenter()` only when forced.

### D7 — Presentation is per-state polymorphic ViewModels

```csharp
public abstract class MatchStateViewModel<TState> : ViewModel where TState : MatchState {
    public abstract void        Bind (TState state, IStateScope scope);
    public abstract GameCommand Map  (PlayerIntent intent);
}

public sealed class ViewModelRegistry {
    private readonly Dictionary<Type, Func<MatchStateViewModel>> factories;
    public MatchStateViewModel Create(MatchState state) => factories[state.GetType()]();
}
```

Presenter subscribes to `MatchStateSlice`; on type change, creates a new VM via the registry and binds it to a polymorphic `Current` property on the parent VM. View follows via standard MVVM template selection. Re-bind on every state change (cheap; only observables update).

Many-to-one is free: two `MatchState` types can share one VM (e.g., `MainPhase1State` and `MainPhase2State` both → `MainPhaseViewModel`).

### D8 — Per-VM `Map(intent)` replaces global `IIntentMapper`

Each VM owns a small intent map scoped to its mode:

```csharp
public override GameCommand Map(PlayerIntent intent) => intent switch {
    TapCardIntent     t => new SelectAttackerCommand   (t.Card),
    UntapCardIntent   u => new DeselectAttackerCommand (u.Card),
    DonePressedIntent   => new CommitDeclarationCommand(),
    _ => throw new InvalidOperationException(intent.GetType().Name),
};
```

Smaller switches, scoped by mode. Adding a phase doesn't grow a central mapper.

When the FSM is in a normal phase, `Map` returns a `GameAction` (or `CompositeAction`). When the FSM is in any phase but a `Request` is open against the actor (D17), the active VM additionally — or instead — returns a `Response`. The same `Map` return type (`GameCommand`) covers both.

### D9 — What presentation drops

| Dropped | Replacement |
|---|---|
| `AffordanceMap` | VM-exposed observables. The VM is the projection. |
| Global `IIntentMapper` | Per-VM `Map(intent)`. |
| `IAvailableActions` service | None. State carries `Actions` directly. |
| `AvailableActionsState` slice | None. Subsumed by `MatchStateSlice`. |
| `ActionTemplate` (wire form) | None for now. `Actions` is `IReadOnlyList<Type>`. Richer form revisited if a real consumer needs it. |

**Note:** `Actions` stays on `MatchState`. Used for the engine's prophylactic intake gate (`if (!current.Actions.Contains(command.GetType())) reject` — implemented as a registered framework validator, see D10), tooling, and network legality. Authoritative legality remains with the validator pipeline.

### D10 — `IHandlerContext` removed; validator/handler split; uniform across all `GameCommand` kinds

`IHandlerContext` is dropped. Handlers inherit from a framework-supplied base class that uses property injection for the framework-level surface; specific services land via constructor injection per handler that needs them.

```csharp
public abstract class GameCommandHandler<TCommand, TResult>
    : ICommandHandler<TCommand, TResult>
    where TCommand : GameCommand
    where TResult  : CommandResult
{
    [Inject] protected Store Store { get; private set; } = null!;
    public abstract Task<TResult> Handle(TCommand command);
}

public sealed class SelectAttackerHandler
    : GameCommandHandler<SelectAttackerCommand, SelectAttackerResult>
{
    public override Task<SelectAttackerResult> Handle(SelectAttackerCommand cmd) {
        Store.Execute(new AddAttackerPayload(cmd.Card));
        return Task.FromResult(new SelectAttackerResult(cmd.Card));
    }
}

public sealed class ApplyBuffHandler : GameCommandHandler<ApplyBuffCommand, ApplyBuffResult> {
    private readonly IModifierFactory modifiers;
    public ApplyBuffHandler(IModifierFactory modifiers) { this.modifiers = modifiers; }
    // Store comes from base; modifiers via constructor on top.
    // ...
}
```

**`Validate` splits from `Handle` into a separate interface.** Handlers do work; validators check legality. Each is registered in DI; the pipeline resolves both per command.

```csharp
public interface ICommandHandler<TCommand, TResult>
    where TCommand : GameCommand
    where TResult  : CommandResult
{
    Task<TResult> Handle(TCommand command);
}

public interface ICommandValidator<TCommand>
    where TCommand : GameCommand
{
    bool Validate(TCommand command, out string? reason);
}

public abstract class CommandValidator<TCommand>
    : ICommandValidator<TCommand>
    where TCommand : GameCommand
{
    [Inject] protected Store Store { get; private set; } = null!;
    public abstract bool Validate(TCommand command, out string? reason);
}

public abstract class ValidatedCommandHandler<TCommand, TResult>
    : GameCommandHandler<TCommand, TResult>, ICommandValidator<TCommand>
    where TCommand : GameCommand
    where TResult  : CommandResult
{
    public abstract bool Validate(TCommand command, out string? reason);
}
```

**Validator policy (locked).**

- **Short-circuit on first reject.** First validator to reject wins; rest skipped. One reason returned. (A diagnostic "why can't I play this — list everything" surface, if needed, becomes a separate `Diagnose(command)` call that runs all validators non-short-circuit.)
- **Registration order.** DI registration order = run order. Reorder by changing registration. Framework defaults register first; games append after.
- **Independent.** Each validator gets only the command. State (when needed) via DI on the base. No shared context bag between validators or with the handler.
- **Optional registration.** Commands with no validator skip the check.

**Uniform across all `GameCommand` kinds.** The same `ICommandValidator<TCommand>` machinery applies to *any* `GameCommand` subtype — `GameAction`, `CompositeAction`, `GameSignal`, `Request<TResponse>`, `Response` (D13). The router resolves `IEnumerable<ICommandValidator<TCommand>>` for the runtime type and runs them in registration order. This is the framework's universal validation surface.

**Pipeline change:**

```
Dispatch:
  resolve all ICommandValidator<TCommand> from container
  for each in registered order:
    if !validator.Validate(cmd, out reason) → reject
  route by kind:
    GameAction     → resolve handler; await handler.Handle(cmd); fold envelope; OnBefore<T>/OnAfter<T> fold around
    CompositeAction → inline expansion in parent scope
    GameSignal     → write to history
    Request<T>     → mutate RequestsSlice
    Response       → mutate AnswersSlice + clear RequestsSlice
```

**What this unlocks:**

- *Multi-layer validation natively.* The five validation layers from ADR-0007 each become their own validator. Framework ships defaults (e.g., `MatchStateActionGate` — rejects commands not in `current.Actions`); games add legality, cost, anti-cheat layers.
- *Reusable validators.* A creature-tap validator works for any command that taps creatures.
- *Composable.* Add / remove / re-order validators by changing DI registration; no handler changes.
- *Uniform.* Request validators, response validators, action validators all use the same surface — no parallel infrastructure.

**Implication for D9's prophylactic intake gate:** the engine-side `if (!current.Actions.Contains(command.GetType())) reject` becomes a registered framework validator (`MatchStateActionGate`), not a hardcoded engine check. Same effect; now disable-able / replaceable for testing.

**Tradeoffs:**

- *Property injection is "hidden" relative to constructor injection.* Acknowledged. Worth it for ergonomics — most handlers/validators just override the one method. New framework-level services land on the base class without touching subclasses.
- *`ICommandAwaiter` removed* — FSM expresses awaits as states (FSM phases) and as Request/Response routing (D17); no async suspension inside `Handle`. Effects can `await` via `Dispatch` (D14, D15), but handlers cannot.
- *Cancellation tokens* — passed as a method argument when they land: `Handle(TCommand command, CancellationToken ct)`. Not threaded through the base.

### D11 — Handler responsibilities

- **Read state** — `Store.Get<TState>()` / `GetAll<TState>()`.
- **Mutate state** — `Store.Execute(payload)`.
- **Atomic and synchronous (from the player-input POV).** A handler returns its `CommandResult` without waiting on any external decision. Player input (target picks, choose-one prompts, etc.) does not enter handler bodies; it is routed through the request system (D17).
- **Handlers do not call the effect pipeline.** That layering rule lives in D13. Effects reach the engine's effect pipeline by being pushed onto `PendingEffectsSlice` (D15); cast handlers push, but do not run effects.

### D12 — Session vs match lifecycle layering

The match FSM (D1–D6) is a Store slice — it carries game-defined phases and gameplay reads it. The **session lifecycle** (NotStarted, Connecting, Reconnecting, Lobby, ShowingResults, Aborted, Ended) is different in kind:

- It's framework-agnostic infrastructure — every networked game needs the same set.
- No game rule reads it; it's metadata *about* the engine, not data *inside* the simulation.
- It must exist *before* any Store does (bootstrap chicken-and-egg).

Lock: **session lifecycle is a typed sealed hierarchy on a field of `GameEngine`, not a Store slice.**

```csharp
public abstract record SessionState;
public sealed   record NotStartedSession    : SessionState;
public sealed   record InMatchSession       : SessionState;
public sealed   record EndedSession(MatchOutcome Outcome) : SessionState;
// Future additions: ConnectingSession, ReconnectingSession, ShowingResultsSession, ...

public sealed class GameEngine {
    public SessionState Session { get; private set; } = new NotStartedSession();
    public event Action<SessionState>? SessionChanged;
    // ...
}
```

Subscribers use the engine event (`SessionChanged`) or read the field. Match-FSM subscribers continue using `MatchStateSlice` from the Store. Two observation patterns, honest about the layering split.

**Replaces / subsumes the old `EngineLifecycle` enum.** `EngineLifecycle.NotStarted/Started/Ended` becomes a coarse projection of `SessionState` and may be retired entirely once the canonical Models are patched.

**Hybrid future flagged.** Long-term shape likely **B+C hybrid** — host code (Unity scene, server process) owns session orchestration (lobby, matchmaking, reconnect logic); engine exposes hooks (SessionChanged event, `engine.StartMatch` / `engine.FinishMatch` / `engine.Dispose`). Framework provides the typed states and the hooks; host wires in the orchestration appropriate to its environment.

**Wiring (concrete).**

- `engine.StartMatch()` transitions session `NotStartedSession → InMatchSession`; seeds `MatchStateSlice.Current` with the initial match state; engine drains; first state's `EnterCommands` fire.
- `engine.FinishMatch(outcome)` force-transitions match to `GameOverState(outcome)` and session to `EndedSession(outcome)`.
- `engine.Dispose()` checks state; if not `EndedSession`, force-transitions with `MatchOutcome.Aborted`.

### D13 — Layering rule: only the engine calls the effect pipeline

The existing layering invariant is:

```
Effect      → orchestrate commands
Command     → invoke mutators
Mutator     → change state
```

Top-down responsibility stack. Lower layers should not know upper layers exist.

**Lock: only the engine ever calls the effect pipeline.** This rolls back the 2026-05-08 effect-pipeline pass Q1 (which had cast handlers and the trigger fold both routing through `IEffectPipeline.RunEffect`). The new rule is structurally enforced — exactly one place in the system holds an `IEffectPipeline` reference and dispatches to it: the engine's drain loop (D15).

The engine sits **above or beside** the effect layer (not below); it is the orchestrator. It is allowed to know about both the effect pipeline and the command pipeline because it owns them both.

**Triggers are not a layering issue.** `Dispatch(command)` internally runs `OnBefore<T>` / `OnAfter<T>` fold around the handler. Commands don't see triggers; triggers don't sit "above" commands from the command's POV — the dispatch mechanism wraps them. Trigger reactions, when they need to fire effects, push to `PendingEffectsSlice` (D15) instead of calling the effect pipeline directly. Same channel as cast handlers.

**Consequence on D11.** Handlers don't import `IEffectPipeline`. Cast handlers do their atomic work (e.g., put the spell on the stack zone and push an `EffectInstance` onto `PendingEffectsSlice`). The engine notices and fires from the queue.

### D14 — `GameCommand` family expanded; single async `Dispatch` surface

The architecture's `GameCommand` base previously carried three subtypes: `GameAction`, `CompositeAction`, `GameSignal`. Two more siblings join:

```
GameCommand (abstract base)
├── GameAction         — executable; atomic; has handler; returns CommandResult
├── CompositeAction    — reusable group of mutations; runs inline
├── GameSignal         — emit-only; history record
├── Request<TResponse> — opens a request; mutates RequestsSlice
└── Response           — answers a request; mutates AnswersSlice + closes RequestsSlice
```

`Request` is generic on its response type, providing typed `Dispatch` returns:

```csharp
public abstract record Request : GameCommand {
    public RequestId Id    { get; init; }
    public IActor    Asker { get; init; }
    public abstract Type ResponseType { get; }
}

public abstract record Request<TResponse> : Request
    where TResponse : Response
{
    public override Type ResponseType => typeof(TResponse);
}

public abstract record Response : GameCommand {
    public RequestId Id { get; init; }
}
```

**Single `Dispatch` surface for effects.** All five subtypes go through one entry point on `IEffectScope`:

```csharp
public interface IEffectScope {
    Task<TResult> Dispatch<TResult>(GameCommand command);
}
```

`Dispatch` is **async**. Even though mutators run synchronously, the dispatch path itself can run trigger before/after fold (whose reactions may push effects, which may suspend on requests). The router pattern-matches on the command's runtime type and routes:

| Kind | Routing | Returns |
|---|---|---|
| `GameAction` | validators → `OnBefore<T>` → handler → `OnAfter<T>` | `CommandResult` |
| `CompositeAction` | inline composition in parent scope | unit |
| `GameSignal` | write to history | unit |
| `Request<TResponse>` | validators → mutate `RequestsSlice` → await matching `Response` | `TResponse` |
| `Response` | validators → match by `Id` against open Request → mutate `AnswersSlice` + clear `RequestsSlice` | unit |

**Validation is part of routing.** Same uniform validator registry (D10) applies to every kind. If no validator is registered for a kind/type, validation is a no-op for that dispatch.

**Drain ordering.** `Dispatch` enqueues into the engine's drain. Effects `await` each `Dispatch`; while awaiting, the dispatched item is the next thing the drain runs (and any triggers it spawns drain after that). Effect-author code stays synchronous-looking via `await`.

```csharp
async Task LightningBolt(IEffectScope scope, IActor caster) {
    var validIds = ComputeTargets(scope.Store, caster);
    var response = await scope.Dispatch(new TargetRequest(asker: caster, validIds));
    await scope.Dispatch(new DealDamageCommand(response.Choice, 3));
    await scope.Dispatch(new CardDamagedSignal(response.Choice, 3));
}
```

### D15 — Engine owns the effect pipeline; D-universal pending-effects queue

**Lock: a single framework slice — `PendingEffectsSlice` — holds the queue of pending effect instances.** Every effect-firing path goes through this slice:

- Cast handlers push to it (a `CastSpellHandler` puts the spell on the stack zone *and* pushes its `EffectInstance` to `PendingEffectsSlice`).
- Trigger reactions (from `OnBefore<T>` / `OnAfter<T>` fold) push to it.
- FSM-driven effect runs (a `MatchState` whose `EnterCommands` includes a push) push to it.

```csharp
public sealed record PendingEffectsSlice(
    ImmutableList<EffectInstance> Pending
) : BaseState;
```

The list is ordered. Drain semantics are game-tunable: most card games drain LIFO (stack-shaped) so the top of the list is the next to fire; FIFO is also valid for queue-shaped games. `ImmutableList` keeps the ordering decision in the runtime, not the slice shape.

**The engine drains.** Engine's main loop, simplified:

```
loop:
  while command queue not empty:
    drain next command   (validators + handler + triggers + history)
  if PendingEffectsSlice non-empty AND drain-policy allows:
    pop effect instance
    await effectPipeline.Run(instance, scope)   ← only place the engine calls the pipeline
  else:
    idle / wait for input
```

**Drain policy composition.** Whether to drain the next pending effect is a composition of state-linked services:

```
DrainAllowed =
   no SBA preempts
   ∧ RequestsSlice empty (or: no open request blocking the actor)
   ∧ MatchState.AllowsDrain (game-state hook; e.g., MTG: false until both pass)
```

The default `Evaluate` body grows two guards:

```csharp
public override MatchState Evaluate(GameAction action, CommandResult result) {
    foreach (var sba in stateBasedActions)
        if (sba.Check(State) is { } over) return over;

    if (State.Get<RequestsSlice>().Open.Count > 0)        return Current;   // pause
    if (!State.Get<PendingEffectsSlice>().Pending.IsEmpty) return Current;  // engine will drain

    var current = State.Get<MatchStateSlice>().Current;
    return current.ShouldExit(action, result, State)
        ? Flow.NextAfter(current)
        : current;
}
```

(Pending-effect drain is the engine's loop responsibility, not `Evaluate`'s — `Evaluate` just keeps the FSM stationary while the engine works through pending effects.)

**Patterns this unifies.**

- *MTG stack.* `CastSpellHandler` pushes; player passes; runtime override of `Evaluate` returns the same state until both players have passed; engine drains; effect runs; runtime returns same state again as long as more pending; eventually returns to phase transition.
- *Hearthstone immediate-fire.* Same pushes, same drain. Default drain-policy is "drain to empty before any FSM transition." Player perceives it as immediate.
- *Trigger reactions.* Triggers that produce effects push them to the same queue. Drain order honors registration / priority within the trigger fold.

### D16 — Effects are async coroutines

**Lock: `IEffect.Run(scope) : Task`. One method. Runs start to finish.**

```csharp
public interface IEffect {
    Task Run(IEffectScope scope);
}
```

Author writes effects as a single async method that calls `await scope.Dispatch(...)` for sub-commands, signals, and request answers. The C# async runtime holds the continuation across awaits; no engine-side step bookkeeping; no re-pushing.

```csharp
public sealed class LightningBolt : IEffect {
    public async Task Run(IEffectScope scope) {
        var validIds = ComputeTargets(scope.Store, asker: scope.Caster);
        var response = await scope.Dispatch(new TargetRequest(scope.Caster, validIds));
        await scope.Dispatch(new DealDamageCommand(response.Choice, 3));
        await scope.Dispatch(new CardDamagedSignal(response.Choice, 3));
    }
}
```

**No state-machine "steps."** From the engine's POV, an effect on `PendingEffectsSlice` is an instance whose `Run` is invoked once. From the author's POV, "no concept of stack" — just write the method.

**Implications.**

- *Save/load mid-effect is not supported by default.* A C# `Task` continuation is not serializable. Save/load can happen at quiescent points (no effect's `Run` currently in flight). If a game later forces mid-effect save/load, that's a separate spike — would force state-machine effects.
- *Cancellation.* Match-end / disconnect needs a `CancellationToken` threaded through `IEffectScope` (or through `Dispatch`). The existing `oq-effect-cancellation-on-match-end` lives here; threading the token is the resolution path.
- *Concurrency.* Only one effect's `Run` is in flight at a time per drain loop. While an effect is awaiting a `Response`, the drain loop progresses (the response flows through the command pipeline and resolves the awaited Task), but no other pending effect is popped. Strict serialization on `PendingEffectsSlice` order.

### D17 — Request / response as a state-linked service

The pattern:

```
slice           — owns the data
service         — injectable façade; the only writer
engine reads    — for orchestration decisions (drain policy)
clients read    — via slice subscription
```

`MatchStateSlice` is one instance. **Requests are another instance of the same pattern, sibling to the FSM, not part of it.**

**Slices.**

```csharp
public sealed record RequestsSlice(
    ImmutableDictionary<RequestId, Request> Open
) : BaseState;

public sealed record AnswersSlice(
    ImmutableDictionary<RequestId, Response> By
) : BaseState;
```

Two slices because lifecycles differ: `Open` is "still asking"; `By` is "answered, awaiting consumer."

**Request / response pairs (framework-shipped).**

```csharp
public sealed record TargetRequest(
    RequestId Id, IActor Asker, IReadOnlyList<Reference> ValidTargets
) : Request<TargetResponse>;

public sealed record TargetResponse(RequestId Id, Reference Choice) : Response;
```

**Game-shipped pairs are open-extensible.** Games define their own `Request<TResponse>` / `Response` pairs for game-specific shapes (split N damage between targets, choose ordered list, choose a number, etc.). Each pair is a typed serializable record — wire-friendly for replay and network.

```csharp
public sealed record SplitDamageRequest(
    RequestId Id, IActor Asker, int Total, IReadOnlyList<Reference> ValidTargets
) : Request<SplitDamageResponse>;

public sealed record SplitDamageResponse(
    RequestId Id, ImmutableDictionary<Reference, int> Allocation
) : Response;
```

**No predicates in requests.** All request fields are serializable references and values. The valid-set is computed game-side *before* opening — the request carries the IDs.

**Validation.** Per D10's uniform validator registry — `ICommandValidator<TargetRequest>`, `ICommandValidator<TargetResponse>`, etc. The framework ships validators for framework pairs; games ship validators for their own pairs.

```csharp
public sealed class TargetResponseValidator : CommandValidator<TargetResponse> {
    public override bool Validate(TargetResponse r, out string? reason) {
        if (!Store.Get<RequestsSlice>().Open.TryGetValue(r.Id, out var req))
            return Bad(out reason, "no such request");
        if (req is not TargetRequest tr)
            return Bad(out reason, "type mismatch");
        if (!tr.ValidTargets.Contains(r.Choice))
            return Bad(out reason, "invalid target");
        return Ok(out reason);
    }
}
```

**Service surface (engine-internal).**

```csharp
public interface IRequestService {
    // Used by the Dispatch router on Request<T> dispatch:
    void Open(Request request);                          // mutates RequestsSlice
    void RecordAnswer(RequestId id, Response answer);    // mutates AnswersSlice + clears RequestsSlice
    void Cancel(RequestId id);                           // clears Open[id]; no answer
}
```

The service is engine-plumbing — exposed to the `Dispatch` router, not to effects directly. Effects interact with requests *only* by calling `await scope.Dispatch(request)`, which returns the typed response.

**Flow (mid-effect target).**

```
1. Effect Run() executes:
     var validIds = ComputeTargets(...);
     var response = await scope.Dispatch(new TargetRequest(asker, validIds));
   Dispatch routes Request<TargetResponse>:
     validators run → Open(request) → returns Task<TargetResponse>
   Effect's Run is now suspended on the awaited Task.

2. RequestsSlice has the open request. Drain policy pauses on RequestsSlice non-empty.

3. Client subscribed to RequestsSlice sees the open TargetRequest, renders picker
   showing only the validIds. Player taps one.

4. VM (in request-mode) maps the gesture:
     return new TargetResponse(currentRequestId, chosenRef);
   The Response is dispatched into the engine's command queue.

5. Dispatch routes Response:
     validators run (TargetResponseValidator: id known, choice valid)
     → RecordAnswer(id, response) (atomic mutation: AnswersSlice += , RequestsSlice -= )
   Engine resolves the awaited Task with the typed response.

6. Effect's Run resumes:
     await scope.Dispatch(new DealDamageCommand(response.Choice, 3));
     ...
```

**`AnswersSlice` lifecycle.** The Dispatch router resolves the awaited request `Task` with the `Response` directly; from the effect-author's POV, `AnswersSlice` is invisible. The slice exists for replay (so a recorded match can be re-played by re-dispatching the recorded responses against open requests in the same order). Match-end clears `AnswersSlice` along with everything else.

**Closed by this lock.** The original spike's "point 8 — request / targeting routing" is resolved. `ICommandAwaiter` is killed for good. The "richer `Actions` shape" question dissolves: `Actions` stays as `IReadOnlyList<Type>`; targeting parameters live on the `Request` records, not on `Actions`.

## Worked example — Magic combat flow

States, in order:

```csharp
public sealed record DeclareAttackersState(ImmutableList<Ref<Card>> Attackers) : MatchState {
    public override IReadOnlyList<Type> Actions => [
        typeof(SelectAttackerCommand),
        typeof(DeselectAttackerCommand),
        typeof(CommitDeclarationCommand),
    ];
    public override IReadOnlyList<GameAction> EnterCommands =>
        [new UntapAllCommand(), new DrawCardCommand()];
    public override bool ShouldExit(GameAction a, CommandResult r, IStateScope s) =>
        a is CommitDeclarationCommand;
}

public sealed record DeclareBlockersState(
    ImmutableList<Ref<Card>> Attackers,
    ImmutableDictionary<Ref<Card>, ImmutableList<Ref<Card>>> Blockings
) : MatchState {
    public override IReadOnlyList<Type> Actions => [
        typeof(SelectBlockerCommand),
        typeof(DeselectBlockerCommand),
        typeof(CommitDeclarationCommand),
    ];
    public override bool ShouldExit(GameAction a, CommandResult r, IStateScope s) =>
        a is CommitDeclarationCommand;
}

public sealed record ResolveCombatState(
    ImmutableList<Ref<Card>> Attackers,
    ImmutableDictionary<Ref<Card>, ImmutableList<Ref<Card>>> Blockings
) : MatchState {
    public override IReadOnlyList<Type> Actions => [];
    public override IReadOnlyList<GameAction> EnterCommands =>
        [new ResolveCombatCommand()];
    public override bool ShouldExit(GameAction a, CommandResult r, IStateScope s) =>
        a is ResolveCombatCommand;
}
```

Flow:

```csharp
public sealed class MagicTurnFlow : ITurnFlow {
    public MatchState NextAfter(MatchState current) => current switch {
        MainPhase1State                                   => DeclareAttackersState.Empty,
        DeclareAttackersState a when a.Attackers.IsEmpty  => new MainPhase2State(),
        DeclareAttackersState a                           => new DeclareBlockersState(a.Attackers, ImmutableDictionary<Ref<Card>, ImmutableList<Ref<Card>>>.Empty),
        DeclareBlockersState  b                           => new ResolveCombatState(b.Attackers, b.Blockings),
        ResolveCombatState                                => new MainPhase2State(),
        MainPhase2State                                   => new DiscardPhaseState(...),
        DiscardPhaseState                                 => NextTurnStart(),
        _                                                 => current,
    };
}
```

Combat resolution lives in the `ResolveCombatHandler`. It reads the resolve state, iterates attackers, fans out `DealDamageToPlayerPayload` / `DealDamageToCardPayload` / `TapCardPayload` calls. One envelope per command; SBA scan at top of `Evaluate` catches game-over before the natural transition.

Selection commands (`SelectAttackerCommand`, `SelectBlockerCommand`) mutate `MatchStateSlice.Current` via guarded mutators that pattern-match the current state and rebuild it with updated phase fields.

Generic exit commands (`Pass`, `CommitDeclaration`) have empty handlers. The work is in `ShouldExit` recognizing them and `Flow.NextAfter` constructing the successor.

Transient states (`ResolveCombatState`) follow a canonical shape: `Actions = []`, `EnterCommands = [oneCommand]`, `ShouldExit` returns true on that command. Same shape for "beginning of combat" / "end of combat" / "upkeep" — anywhere the engine needs a phase tick with no player input.

### Worked example — Lightning Bolt (mid-effect target request)

Player A casts Lightning Bolt:

```csharp
// Cast handler — atomic, no effect-pipeline call.
public sealed class CastLightningBoltHandler
    : GameCommandHandler<CastLightningBoltCommand, CastResult>
{
    public override Task<CastResult> Handle(CastLightningBoltCommand cmd) {
        Store.Execute(new SpendManaPayload(cmd.Caster, 1));
        Store.Execute(new MoveCardToZonePayload(cmd.Card, /* stack */));
        Store.Execute(new PushPendingEffectPayload(
            new EffectInstance(Source: cmd.Card.SelfRef, EffectId: "LightningBolt", Payload: new(...))));
        return Task.FromResult(new CastResult.Accepted());
    }
}

// Effect — author writes one async method.
public sealed class LightningBoltEffect : IEffect {
    public async Task Run(IEffectScope scope) {
        var validIds = scope.Store.QueryCreaturesAndPlayers();
        var response = await scope.Dispatch(new TargetRequest(scope.Caster, validIds));
        await scope.Dispatch(new DealDamageCommand(response.Choice, 3));
        await scope.Dispatch(new CardDamagedSignal(response.Choice, 3));
    }
}
```

Sequence:

```
1. CastLightningBoltCommand drains
   → mana spent, card on stack zone, EffectInstance pushed to PendingEffectsSlice
2. Evaluate runs:
   stack-non-empty signal in MTG flow says "stay in PriorityState"
3. Player A passes priority → PassPriorityCommand drains
   → handler marks "A passed"
4. Evaluate runs:
   both passed, PendingEffectsSlice non-empty → engine drains pending
5. Engine pops top EffectInstance, calls effectPipeline.Run(LightningBoltEffect, scope)
   Effect.Run() awaits Dispatch(new TargetRequest(...))
6. Dispatch routes Request<TargetResponse>:
   validators ok → RequestsSlice += request → returns Task<TargetResponse> (pending)
7. Drain pauses on RequestsSlice non-empty
   Client renders picker showing valid IDs
8. Player taps target c2; VM in request-mode returns TargetResponse(id, c2)
   Response enters command queue
9. Dispatch routes Response:
   validators ok (id known, c2 valid) → AnswersSlice += , RequestsSlice -=
   Awaited Task resolves with TargetResponse(c2)
10. Effect.Run() resumes:
    await Dispatch(DealDamage(c2, 3))   → drains via command pipeline
    await Dispatch(CardDamagedSignal(c2, 3)) → history
11. Effect.Run returns. Engine pops next pending effect (none).
12. Evaluate runs: stack empty, requests empty → priority back to active player
```

## Out of scope (untouched by this pass)

- **Effect pipeline internals.** The seam is settled — engine-only firing (D13), universal queue (D15), async coroutines (D16), `Dispatch` surface (D14), request routing (D17). What lives **inside** an effect (authoring vocabulary, primitive set, graph vs free-form, recipe shape, definition catalog) belongs to `oq-effect-pipeline` / `oq-effect-authoring`. The 2026-05-08 effect-pipeline spike re-write inherits today's locks and resolves the rest.
- **Trigger pipeline internals.** `Trigger<T>` / `ITriggerPipeline` / `OnBefore<T>` / `OnAfter<T>` continue working as today. Trigger reactions, when they need to fire effects, push to `PendingEffectsSlice` (D15). Trigger ordering inside the fold (`oq-trigger-ordering`) and trigger subscription entry points (`oq-trigger-subscription-entry`) are unchanged by today's work.
- **`HistoryEnvelope` typed shape.** Today's pseudocode-only definition stays as-is; reshape is point 7, deferred.
- **Targeting / request authoring vocabulary.** The runtime contract for requests is settled; what request *types* a game declares and how they compose with effects (closed-set primitives, builder DSL, recipe form, etc.) belongs to the authoring feature.

## Deferred (explicitly)

- **Point 7 — `HistoryEnvelope` reshape.** Wait until effects pipeline lands and there are real envelope consumers to design against. Note: `TransitionStateCommand`, `Request<T>`, `Response` are all in the pipeline, so their envelopes are auto-captured. Whether those envelopes need a special shape (no actor on transitions, request/response pairing) is a TBD detail.
- **Pre-play targeting.** A card whose play-condition requires a chosen target before the play-card command fires (target is a *parameter* of the play, not a request inside the resolution) likely needs a different shape than the mid-effect request system. Probably folds into a richer `PlayCardCommand(card, target)` that the cast validator accepts. Settle when the first game forces it. Today's request system covers mid-effect targeting only.
- **Authoring vocabulary for effects and effect recipes.** What the body of `IEffect.Run` looks like in production game code (free-form C#, builder DSL, declarative recipe, hybrid). Lives in `oq-effect-authoring` / `bl-authoring-feature`.
- **Default validator set.** Which validators ship as framework defaults (`MatchStateActionGate` is canonical; what else?), and whether registration ordering is by-convention or enforced. New open item: `oq-validator-defaults`.
- **Cancellation tokens.** When the first scenario forces it (match-end during mid-effect await; disconnect; abort), thread `CancellationToken` through `IEffectScope.Dispatch`. Folds into `oq-effect-cancellation-on-match-end`.

## Effect on existing open items (audit)

Verdict per entry. **closed** = resolution lands when this spike patches the canonical Models. **changed** = entry text needs revision. **unchanged** = not touched by today's locks. **new** = added by today's work.

### Engine + runtime

| Entry | Verdict | Note |
|---|---|---|
| `oq-entity-destruction` | unchanged | Cleanup lifecycle untouched. |
| `oq-attachment-storage` | unchanged | — |
| `oq-move-validation-execution-drift` | unchanged | — |
| `oq-effect-pipeline` | changed | Seam settled (D13–D17). Effect-pipeline spike re-write inherits; entry should point here. |
| `oq-effect-authoring` | changed | Runtime contract pinned (`async Task Run(scope)`); authoring vocabulary still open. |
| `oq-runtime-as-state-evaluator` | closed (on patch) | FSM lock realizes the collapse — `Evaluate → MatchState`; runtime is `Flow + SBAs + ShouldExit` plus drain-policy hooks. |
| `oq-session-state-machine` | changed | Session is a typed sealed hierarchy on `engine.Session` (D12); B+C hybrid flagged for future. |
| `oq-gamerules-composition` | unchanged | — |
| `oq-fsm-game-state` | closed (on patch) | The spike *is* the answer; locks resolve the design. |
| `oq-trigger-ordering` | unchanged | Triggers still ride `Dispatch`'s before/after fold; ordering rule still TBD. |
| `bl-actor-ownership-reverse-index` | unchanged | — |
| `oq-effect-cancellation-on-match-end` | changed | Now precisely shaped: `CancellationToken` threaded through `IEffectScope.Dispatch`; engine GC's `RequestsSlice` / `AnswersSlice` on match-end. |
| `oq-intent-template-lookup` | changed | Largely dissolves: per-VM `Map(intent) → GameCommand` (D8) replaces global lookup. Residual: VMs in request-mode return `Response`. |
| `oq-trigger-subscription-entry` | unchanged | — |
| `oq-history-envelope` | unchanged | Spike defers (point 7). |
| `bl-zone-visibility-custom-policy-serialization` | unchanged | Reinforced: today's "no predicates in requests" is the same problem class. Worth cross-linking. |
| `bl-card-resolution-helper-shape` | unchanged | — |
| `bl-pile-refs-index` | unchanged | — |

### Features yet to scaffold

| Entry | Verdict | Note |
|---|---|---|
| `bl-interaction-feature` | changed | Interaction still produces `PlayerIntent`; consumer is now per-VM `Map(intent)`. Cleaner boundary. |
| `bl-authoring-feature` | changed | Authored effect shape now constrained to `async Task Run(scope)`; recipe / vocabulary still open. |

### Tooling, spatial, multiplayer, presets

All **unchanged.**

### New entries to add

- **`oq-targeting-pre-play`** — Pre-play targeting (target as parameter of the play, not mid-effect request). Likely folds into a richer `PlayCardCommand(card, target)`. Decide later.
- **`oq-validator-defaults`** — Which validators ship as framework defaults (`MatchStateActionGate` is canonical; others?), ordering convention, when to short-circuit. Q5d deferred from today.
- **`oq-effect-save-load`** — Async-coroutine effects can't be serialized mid-flight. Save/load only at quiescent points. If a game forces mid-effect save/load, would force state-machine effects.
- **`oq-drain-policy-composition`** — Engine's drain decision composes multiple state-linked services (`MatchState.AllowsDrain` + `RequestsSlice` empty + `PendingEffectsSlice` non-empty + game overrides). Document the composition rule when stakes grow.

### Summary

| Verdict | Count |
|---|---|
| closed (on spike-patch) | 2 |
| changed | 7 |
| unchanged (in scope) | 11 |
| unchanged (out of scope) | 8 |
| new | 4 |

## When this lands

Resolution path on promotion:

1. **ADR**: write `0009-engine-as-fsm-and-dispatch-unification.md` (or next number) — record:
   - Runtime contract change (`Evaluate → MatchState`).
   - `MatchStateSlice` wrapper pattern.
   - `ITurnFlow` split, SBA mechanism.
   - Session lifecycle on engine field (D12).
   - Layering rule: only engine fires effects (D13).
   - `GameCommand` family expansion (`Request<TResponse>`, `Response`) (D14).
   - Single async `Dispatch` surface for effects (D14).
   - D-universal `PendingEffectsSlice` queue (D15).
   - Effects as async coroutines (D16).
   - Request/response state-linked service (D17).
   - Validator pipeline policy (D10): short-circuit, registration order, independent, command-only signature.

2. **Models to rewrite or retire:**
   - `engine/01-game-engine.md` — adds `engine.Session : SessionState` field; lifecycle gate enforcement updated; session typed-state hierarchy documented.
   - `engine/03-game-rules-runtime.md` — `Evaluate` signature change (returns `MatchState`); add `Flow` + `StateBasedActions` properties; document drain-policy composition.
   - `engine/04-available-actions.md` — retire (subsumed by state-owned `Actions`).
   - `actions/01-command-pipeline.md` — `GameCommand` family expanded; pipeline change (N validators + 1 handler per kind); routing-by-kind documented.
   - `actions/02-intent-mapper.md` — moves conceptually to presentation; per-VM `Map(intent)`.
   - `actions/03-command-handler.md` — `Validate` splits into `ICommandValidator<TCommand>`; new `GameCommandHandler<,>`, `CommandValidator<>`, `ValidatedCommandHandler<,>` base classes; uniform validator registry across all `GameCommand` kinds.
   - `actions/05-available-actions-snapshot.md` — retire.
   - `effects/01-effect.md` — effect contract is `IEffect.Run(scope) : Task`; async coroutine model; engine-only firing.
   - `effects/02-effect-scope.md` — `IEffectScope.Dispatch<T>` single surface; routing description.
   - `effects/03-handler-context.md` — retire (`IHandlerContext` removed).
   - `effects/04-command-awaiter.md` — retire (`ICommandAwaiter` killed by Request/Response system).
   - `presentation/01-presentation-loop.md` — VM-polymorphism + registry; drop `AffordanceMap`; per-VM `Map(intent)` returning `GameCommand` (incl. `Response`).

3. **New Models:**
   - `engine/0X-match-state.md` — the FSM contract, `MatchStateSlice` pattern, `ITurnFlow`, SBAs.
   - `engine/0X-transition-pipeline.md` — `TransitionStateCommand` + handler.
   - `engine/0X-session-state.md` — typed `SessionState` hierarchy on the engine field; lifecycle wiring (StartMatch / FinishMatch / Dispose).
   - `engine/0X-pending-effects.md` — `PendingEffectsSlice`, drain loop, drain-policy composition.
   - `engine/0X-request-system.md` (or `actions/0X-request-system.md`) — `Request<TResponse>` / `Response`, `RequestsSlice` / `AnswersSlice`, `IRequestService`, validator pairing convention.
   - `actions/0X-validator-handler-split.md` — `ICommandValidator` / `ICommandHandler` separation, `ValidatedCommandHandler` combined base, multi-layer validator pipeline, uniform across `GameCommand` kinds.
   - `presentation/0X-view-model-registry.md` — VM polymorphism + registry.

4. **Glossary:** add `MatchState`, `MatchStateSlice`, `ITurnFlow`, `SBA`, `MatchStateViewModel`, `ViewModelRegistry`, `ICommandValidator`, `CommandValidator`, `GameCommandHandler`, `ValidatedCommandHandler`, `SessionState`, `PendingEffectsSlice`, `IEffect`, `IEffectScope`, `Request`, `Response`, `RequestId`, `RequestsSlice`, `AnswersSlice`, `IRequestService`, `TransitionStateCommand`. Retire `AffordanceMap`, `ActionTemplate`, `IIntentMapper` (as global), `IAvailableActions`, `AvailableActionsState`, `ICommandAwaiter`, `IHandlerContext`, `EngineLifecycle` (subsumed by `SessionState`).

5. **Open-items ledger:**
   - Close: `oq-runtime-as-state-evaluator`, `oq-fsm-game-state`.
   - Update: `oq-effect-pipeline`, `oq-effect-authoring`, `oq-session-state-machine`, `oq-effect-cancellation-on-match-end`, `oq-intent-template-lookup`, `bl-interaction-feature`, `bl-authoring-feature`.
   - Add: `oq-targeting-pre-play`, `oq-validator-defaults`, `oq-effect-save-load`, `oq-drain-policy-composition`.

## Open questions inside this design

Still genuinely open after today's walk:

- **Polymorphic `Current` cast in handlers / mutators.** Every handler that reads phase data does a one-line cast (`(BattlePhaseState)slice.Current`). Acceptable; could collapse with an extension method (`scope.GetMatchState<T>()`) if it gets noisy. Defer.
- **Wrapper vs sibling slices for phase-specific data.** Default is wrapper-with-polymorphic-field (`MatchStateSlice.Current.Attackers`); per-phase sibling slices (`BattlePhaseDataSlice`) are an alternative if subscriber fan-out becomes a concern. Defer until proven.
- **`PendingEffectsSlice` ordering shape.** `ImmutableList<EffectInstance>` keeps the ordering decision in the runtime. Whether to expose helpers for stack-shaped (LIFO) vs queue-shaped (FIFO) drain on the slice itself, or leave runtime-side, is open. Decide on first multi-game stress.
- **`RequestsSlice` keyed by `RequestId` only vs by actor.** Multi-actor parallel requests (each player chooses simultaneously) are supported by the dictionary, but querying "any open request for actor X" is O(N). Add a parallel actor index when a real query consumer surfaces.
- **Scratchpad / scoped writes for effects.** Effects today write through `Store.Execute` on the live store via `Dispatch`. A future scratchpad / transactional / preview flavor (read-your-writes-then-commit-or-rollback) would benefit from interface-segregating the effect-facing surface (`IScope` extending `IStateScope` with `Execute`). Deferred — current shape is `[Inject] Store` directly on the base class.
- **`MatchStateActionGate` as one of multiple framework-default validators.** Q5d deferred; promoted to `oq-validator-defaults`.

Resolved by today's walk (no longer open inside this design):

- ~~Effect execution via `Evaluate` (Magic stack pattern)~~ — resolved by D15 (D-universal queue) + D16 (async coroutines).
- ~~Multi-layer validator pipeline (default validator set)~~ — policy locked in D10; default set deferred to `oq-validator-defaults`.
- ~~Session vs match lifecycle layering~~ — resolved by D12.
- ~~Request / targeting routing (point 8)~~ — resolved by D17.
