---
type: spike
status: draft
tags: [#spike, #engine, #runtime, #fsm, #presentation, #state]
---

# FSM Architecture — Design Sketch

> **Status: draft / iterating.** Working sketch from the FSM-architecture design pass. Settles points 1–6 of the open list (engine-as-FSM, state-owned actions, intent-mapper-as-presentation, presentation-state owns mapper, `IHandlerContext` removed + validator/handler split, handler responsibilities). Points 7 (history envelope reshape) and 8 (request / targeting routing) are explicitly deferred.
>
> **Self-contained** — readable standalone. **Not yet propagated to canonical Models.** Resolution path: lift to ADR-0008-or-higher and patch `engine/01`, `engine/03`, `engine/04`, `actions/02`, `actions/03`, `effects/03`, `effects/04`, `presentation/01`, `state/state.md`, plus glossary + open-items.

## TL;DR

- **Engine *is* the FSM.** `runtime.Evaluate` returns a `MatchState`, not a list of follow-up commands. The engine recognizes the new state and dispatches a `TransitionStateCommand` through the normal pipeline.
- **`MatchState` lives inside a sealed `MatchStateSlice` wrapper.** Scaffold's exact-type slice keying (`Map<Reference, Type, Slice>`) means we can't make the polymorphic state itself a slice. The wrapper carries a polymorphic `Current` field; mutators target the wrapper with a guard.
- **State carries data + transition predicate, not transition target.** `Actions` (valid command types), `EnterCommands` (data), `ShouldExit(action, result, scope)` (pure predicate), plus phase-specific fields (`Attackers`, `Blockings`, `HandCount`, etc.). **No `Next` method** — successor knowledge lives in `ITurnFlow.NextAfter`, the single source of turn structure.
- **Cross-cutting state-driven overrides are SBAs (state-based actions).** Game-over, lethal-damage cleanup, legend rule. Each SBA is a one-method `Check(scope) → MatchState?` that preempts the normal `ShouldExit` / `Flow` path.
- **`Evaluate` collapses to ~5 lines** — SBA scan → `current.ShouldExit` → `Flow.NextAfter` (or stay). Could be folded fully into the engine, leaving the runtime to only register SBAs + Flow.
- **Presentation: per-state ViewModels.** `MatchStateViewModel` is polymorphic; `ViewModelRegistry` maps `MatchState` type → factory. The VM exposes observables (replaces `AffordanceMap`) and owns its own `Map(intent) → GameCommand` (replaces global `IIntentMapper`).
- **Drops:** `AffordanceMap`, global `IIntentMapper`, `IAvailableActions` service, `AvailableActionsState` slice, `ActionTemplate` (wire form). `Actions` stays on `MatchState` as `IReadOnlyList<Type>` for engine gate + tooling.
- **`IHandlerContext` removed.** Handlers inherit from `GameCommandHandler<,>` base with `[Inject] Store`. Specifics (`IModifierFactory`, etc.) injected via constructor on subclasses. `ICommandAwaiter` removed.
- **`Validate` splits from `Handle`.** `ICommandValidator<TCommand>` and `ICommandHandler<TCommand, TResult>` are separate interfaces. Pipeline resolves N validators + 1 handler per command. `ValidatedCommandHandler<,>` is a combined base for handlers that want both.

## Context (locked from prior architecture)

What the architecture looked like before this pass:

- `runtime.Evaluate(action, result) → IReadOnlyList<GameAction>` — returns follow-up actions, engine threads them into FIFO.
- Available actions published via `IAvailableActions.Set(snapshot)` — runtime-driven service; written from inside `Evaluate` or mid-handler.
- `AvailableActionsState` slice carries the `AvailableActionsSnapshot` (typed `ActionTemplate` list).
- Presentation projects the snapshot through a game-supplied `AffordanceMap` (UI-friendly form) and a single global `IIntentMapper` that switches on every `ActionTemplate` subtype.
- `IHandlerContext` exposes `Store`, `Actions` (snapshot writer), `Resolver` (decision awaiter), `Modifiers`.
- Decisions ("pick a target") use `ICommandAwaiter` — handler calls `ctx.Resolver.Wait<T>()` to suspend until a matching command arrives.

What changed in this pass — the runtime had quietly drifted toward "state evaluator" already (per `oq-runtime-as-state-evaluator`). This pass commits to that direction structurally.

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

```csharp
public abstract class GameRulesRuntime<TDefinition> {
    protected abstract ITurnFlow           Flow              { get; }
    protected abstract IReadOnlyList<ISBA> StateBasedActions { get; } = [];

    public virtual MatchState Evaluate(GameAction action, CommandResult result) {
        // Default body: SBAs preempt; otherwise current.ShouldExit + Flow.NextAfter.
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

### D9 — What presentation drops

| Dropped | Replacement |
|---|---|
| `AffordanceMap` | VM-exposed observables. The VM is the projection. |
| Global `IIntentMapper` | Per-VM `Map(intent)`. |
| `IAvailableActions` service | None. State carries `Actions` directly. |
| `AvailableActionsState` slice | None. Subsumed by `MatchStateSlice`. |
| `ActionTemplate` (wire form) | None for now. `Actions` is `IReadOnlyList<Type>`. Richer form revisited with targeting (point 8). |

**Note:** `Actions` stays on `MatchState`. Used for the engine's prophylactic intake gate (`if (!current.Actions.Contains(command.GetType())) reject`), tooling, and network legality. Authoritative legality remains with `handler.Validate`.

### D10 — `IHandlerContext` removed; handlers DI dependencies via base + constructor

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

public abstract class ValidatedCommandHandler<TCommand, TResult>
    : GameCommandHandler<TCommand, TResult>, ICommandValidator<TCommand>
    where TCommand : GameCommand
    where TResult  : CommandResult
{
    public abstract bool Validate(TCommand command, out string? reason);
}
```

**Pipeline change:**

```
Dispatch:
  resolve all ICommandValidator<TCommand> from container
  for each in registered order:
    if !validator.Validate(cmd, out reason) → reject
  enqueue

Drain:
  resolve ICommandHandler<TCommand, TResult>
  await handler.Handle(cmd)
  fold envelope, etc.
```

**What this unlocks:**

- *Multi-layer validation natively.* The five validation layers from ADR-0007 each become their own validator. Framework ships defaults (e.g., `MatchStateActionGate` — rejects commands not in `current.Actions`); games add legality, cost, anti-cheat layers.
- *Optional validation.* Commands without a validator skip the check (e.g., `PassCommand`).
- *Reusable validators.* A creature-tap validator works for any command that taps creatures.
- *Composable.* Add / remove / re-order validators by changing DI registration; no handler changes.

**Implication for D9's prophylactic intake gate:** the engine-side `if (!current.Actions.Contains(command.GetType())) reject` becomes a registered framework validator (`MatchStateActionGate`), not a hardcoded engine check. Same effect; now disable-able / replaceable for testing.

**Tradeoffs:**

- *Property injection is "hidden" relative to constructor injection.* Acknowledged. Worth it for ergonomics — most handlers just override `Handle`. New framework-level services land on the base class without touching subclasses.
- *`ICommandAwaiter` removed* — FSM expresses awaits as states; no async suspension inside `Handle`. Likely fully removed from the architecture once point 8 (request / targeting routing) settles.
- *Cancellation tokens (when they land)* — passed as a method argument: `Handle(TCommand command, CancellationToken ct)`. Not threaded through the base.

### D11 — Handler responsibilities

- **Read state** — `Store.Get<TState>()` / `GetAll<TState>()`.
- **Mutate state** — `Store.Execute(payload)`.
- **Execute effects (placeholder)** — inline computation + multiple `Store.Execute` calls. Proper effect pipeline deferred to `oq-effect-pipeline`. Semantic-event emission ("damage dealt", "card drawn"), if needed, becomes a concern of the effect pipeline — handlers do not emit a separate channel.

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

## Deferred (explicitly)

- **Point 7 — `HistoryEnvelope` reshape.** Wait until effects pipeline lands and there are real envelope consumers to design against. Note: `TransitionStateCommand` is now in the pipeline, so transitions are auto-captured in envelopes. Whether transition envelopes need a special shape (no actor, synthetic result) is a TBD detail.
- **Point 8 — Request / targeting routing.** Tied to targeting design. Will likely also kill `ICommandAwaiter` for good and revisit the richer `Actions` shape (typed descriptors with parameter constraints). The current `IReadOnlyList<Type>` is the placeholder.
- **`oq-effect-pipeline`** — effect orchestration; whether handlers compose effects vs. call inline. Today combat resolution is inline in `CommitHandler` / `ResolveCombatHandler` as a placeholder. Semantic-event emission ("damage dealt", "card drawn") is owned by this design pass — effects, not handlers, dispatch events.

## Effect on existing open items

To audit on the next pass — see `Product/ledgers/OPEN-ITEMS.md`.

## When this lands

Resolution path on promotion:

1. **ADR**: write `0008-engine-as-fsm.md` (or next number) — record the runtime contract change, the `MatchStateSlice` wrapper pattern, the `ITurnFlow` split, the SBA mechanism, the presentation simplification.
2. **Models to rewrite or retire:**
   - `engine/03-game-rules-runtime.md` — `Evaluate` signature change (returns `MatchState`); add `Flow` + `StateBasedActions` properties.
   - `engine/04-available-actions.md` — retire (subsumed by state-owned `Actions`).
   - `actions/01-command-pipeline.md` — pipeline change: N validators + 1 handler resolution.
   - `actions/02-intent-mapper.md` — move conceptually to presentation, become per-VM.
   - `actions/03-command-handler.md` — `Validate` splits into `ICommandValidator<TCommand>`; new `GameCommandHandler<,>` + `ValidatedCommandHandler<,>` base classes.
   - `actions/05-available-actions-snapshot.md` — retire.
   - `effects/03-handler-context.md` — retire (`IHandlerContext` removed).
   - `effects/04-command-awaiter.md` — retire.
   - `presentation/01-presentation-loop.md` — VM-polymorphism + registry; drop `AffordanceMap`.
3. **New Models:**
   - `engine/0X-match-state.md` — the FSM contract, `MatchStateSlice` pattern, `ITurnFlow`, SBAs.
   - `engine/0X-transition-pipeline.md` — `TransitionStateCommand` + handler.
   - `actions/0X-validator-handler-split.md` — `ICommandValidator` / `ICommandHandler` separation, `ValidatedCommandHandler` combined base, multi-layer validator pipeline.
   - `presentation/0X-view-model-registry.md` — VM polymorphism + registry.
4. **Glossary:** add `MatchState`, `MatchStateSlice`, `ITurnFlow`, `SBA`, `MatchStateViewModel`, `ViewModelRegistry`, `ICommandValidator`, `GameCommandHandler`, `ValidatedCommandHandler`. Retire `AffordanceMap`, `ActionTemplate`, `IIntentMapper` (as global), `IAvailableActions`, `AvailableActionsState`, `ICommandAwaiter`, `IHandlerContext`.
5. **Open-items ledger:** close `oq-runtime-as-state-evaluator`, `oq-fsm-game-state`. Update `oq-intent-template-lookup`, `oq-history-envelope`, `oq-effect-cancellation-on-match-end`, `oq-session-state-machine`. Add `oq-targeting-routing`.

## Open questions inside this design

- **Polymorphic `Current` cast in handlers / mutators.** Every handler that reads phase data does a one-line cast (`(BattlePhaseState)slice.Current`). Acceptable; could collapse with an extension method (`scope.GetMatchState<T>()`) if it gets noisy. Defer.
- **Wrapper vs sibling slices for phase-specific data.** Default is wrapper-with-polymorphic-field (`MatchStateSlice.Current.Attackers`); per-phase sibling slices (`BattlePhaseDataSlice`) are an alternative if subscriber fan-out becomes a concern. Defer until proven.
- **Session vs match lifecycle layering.** Two-tier shape: an engine-owned session FSM (`NotStarted` / `Connecting` / `Ready` / `InMatch` / `Ended`) gates whether the match-level FSM is active. Match-side setup (shuffle, deal, place starting stuff) is the first MatchState's `EnterCommands`. Session-side setup (allocate match, connect players, agree on decks) is engine-level and runs before the match FSM starts. The `EngineLifecycle` enum is the seed for the session tier; it grows per `oq-session-state-machine`. Don't conflate the two tiers.
- **Effect execution via `Evaluate` (Magic stack pattern).** In Magic-shaped games, the runtime decides *when* effects fire — `Evaluate` reads state ("stack has a top + all players passed") and returns a `ResolveTopState`, whose `EnterCommands` runs the effect (or kicks the effect pipeline). Effects don't run from `Evaluate` directly; they run from the state's `EnterCommands` or via the effect pipeline's surface. This pattern feeds into `oq-effect-pipeline`: runtime decides *when*, pipeline decides *how* (immediate / batched / stacked).
- **Multi-layer validator pipeline (default validator set).** The validator/handler split (D10) supports N validators per command. To settle: which validators ship as framework defaults (`MatchStateActionGate` for sure; what else?), what's the ordering convention (registration order? priority weight?), can validators access the result of prior validators (e.g., short-circuit semantics, accumulated reason).
- **Scratchpad / scoped writes for handlers.** Handlers today write to the live store via `Store.Execute`. A future scratchpad / transactional / replay flavor (read-your-writes-then-commit-or-rollback) would benefit from interface-segregating the handler-facing surface (`IScope` extending `IStateScope` with `Execute`). Deferred — concrete trigger hasn't surfaced; current shape is `[Inject] Store` directly on the base class.
