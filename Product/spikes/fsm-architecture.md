---
type: spike
status: draft
tags: [#spike, #engine, #runtime, #fsm, #presentation, #state]
---

# FSM Architecture — Design Sketch

> **Status: draft / iterating.** Working sketch from the FSM-architecture design pass. Settles points 1–6 of the open list (engine-as-FSM, state-owned actions, intent-mapper-as-presentation, presentation-state owns mapper, handler-context shrink, handler responsibilities + signals). Points 7 (history envelope reshape) and 8 (request / targeting routing) are explicitly deferred.
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
- **`IHandlerContext` shrinks to `{ Store }`.** Specifics (`IModifierFactory`, `ISignalBus`) injected per-handler. `ICommandAwaiter` removed.
- **Signals = typed semantic events**, single channel, envelope-recorded. `DamageDealtSignal`, `CardDrawnSignal`, etc. Emitted via injected `ISignalBus`. Triggers fire on commands today; signal-driven triggers are a separate pivot.

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

The glue is mechanical enough that it could fold fully into the engine, leaving the runtime to expose only:

```csharp
public abstract class GameRulesRuntime<TDefinition> {
    protected abstract ITurnFlow           Flow              { get; }
    protected abstract IReadOnlyList<ISBA> StateBasedActions { get; } = [];
}
```

### D6 — Transitions go through the command pipeline

Engine, post-`Evaluate`:

```csharp
var newState = runtime.Evaluate(action, result);
var current  = scope.Get<MatchStateSlice>().Current;
if (!ReferenceEquals(newState, current))
    Enqueue(new TransitionStateCommand(newState));
```

`TransitionStateHandler.Handle`:

```csharp
public Task<TransitionResult> Handle(TransitionStateCommand cmd, IHandlerContext ctx) {
    ctx.Store.Execute(new SetMatchStatePayload(cmd.NewState));   // mutate slice
    foreach (var enter in cmd.NewState.EnterCommands)
        engine.Enqueue(enter);                                    // chained via engine
    return Task.FromResult(new TransitionResult());
}
```

Self-transition (same instance returned) → engine no-ops. Re-enter semantics not exposed today; add explicit `Reenter()` only when forced.

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

### D10 — `IHandlerContext` shrinks to `{ Store }`

```csharp
public interface IHandlerContext {
    Store Store { get; }
}
```

Specifics injected per-handler:

- `IModifierFactory` — modifier-relevant handlers DI it. Same singleton lifetime preserves the deterministic ID counter.
- `ISignalBus` — most handlers will emit at least one signal; injected for now (could move to context if emission proves universal).
- `ICommandAwaiter` — **removed**. FSM expresses awaits as states; no async suspension inside `Handle`. Likely fully removed from the architecture once point 8 (request / targeting routing) settles.

The wrapper itself is mostly ceremonial at this point — could collapse to passing `Store` directly. Kept for future-proofing and convention.

### D11 — Handler responsibilities

- **Read state** — `ctx.Store.Get<TState>()` / `GetAll<TState>()`.
- **Mutate state** — `ctx.Store.Execute(payload)`.
- **Emit signals** — via injected `ISignalBus`.
- **Execute effects (placeholder)** — inline computation + multiple `Store.Execute` calls. Proper effect pipeline deferred to `oq-effect-pipeline`.

### D12 — Signals are typed semantic events, envelope-recorded

```csharp
public interface ISignal { }
public interface ISignalBus { void Emit<T>(T signal) where T : ISignal; }

public sealed record DamageDealtSignal     (Ref<Card> Source, Reference Target, int Amount) : ISignal;
public sealed record CreatureEnteredSignal (Ref<Card> Card, Ref<Zone> Zone)                 : ISignal;
public sealed record CardDrawnSignal       (Ref<Player> Drawer, Ref<Card> Card)             : ISignal;
```

Single channel. Engine threads emitted signals into the current envelope (exact field shape lands with point 7). Triggers, observers, and replay all read from the envelope.

**Implication for triggers (deferred):** today's triggers fire on commands (before/after fold). Signal-driven triggers ("when a creature deals damage") are more idiomatic for game rules. The pivot is real but lives in a separate trigger-pipeline pass.

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
- **`oq-effect-pipeline`** — effect orchestration; whether handlers compose effects vs. call inline. Today combat resolution is inline in `CommitHandler` / `ResolveCombatHandler` as a placeholder.
- **`oq-trigger-signal-pivot` (new)** — triggers firing on signals (semantic) instead of (or alongside) commands. The signal channel exists; the trigger pipeline still listens on commands.

## Effect on existing open items

To audit on the next pass — see `Product/ledgers/OPEN-ITEMS.md`.

## When this lands

Resolution path on promotion:

1. **ADR**: write `0008-engine-as-fsm.md` (or next number) — record the runtime contract change, the `MatchStateSlice` wrapper pattern, the `ITurnFlow` split, the SBA mechanism, the presentation simplification.
2. **Models to rewrite or retire:**
   - `engine/03-game-rules-runtime.md` — `Evaluate` signature change; `FilterTriggers` may follow in the trigger-signal pivot.
   - `engine/04-available-actions.md` — retire (subsumed by state-owned `Actions`).
   - `actions/02-intent-mapper.md` — move conceptually to presentation, become per-VM.
   - `actions/05-available-actions-snapshot.md` — retire.
   - `effects/03-handler-context.md` — shrink to `{ Store }`.
   - `effects/04-command-awaiter.md` — retire.
   - `presentation/01-presentation-loop.md` — VM-polymorphism + registry; drop `AffordanceMap`.
3. **New Models:**
   - `engine/0X-match-state.md` — the FSM contract, `MatchStateSlice` pattern, `ITurnFlow`, SBAs.
   - `engine/0X-transition-pipeline.md` — `TransitionStateCommand` + handler.
   - `actions/0X-signals.md` — `ISignal`, `ISignalBus`, semantic-event channel.
   - `presentation/0X-view-model-registry.md` — VM polymorphism + registry.
4. **Glossary:** add `MatchState`, `MatchStateSlice`, `ITurnFlow`, `SBA`, `MatchStateViewModel`, `ViewModelRegistry`, `ISignal`. Retire `AffordanceMap`, `ActionTemplate`, `IIntentMapper` (as global), `IAvailableActions`, `AvailableActionsState`, `ICommandAwaiter`.
5. **Open-items ledger:** close `oq-runtime-as-state-evaluator`, `oq-fsm-game-state`. Update `oq-intent-template-lookup`, `oq-history-envelope`, `oq-effect-cancellation-on-match-end`. Add `oq-trigger-signal-pivot`, `oq-targeting-routing`.

## Open questions inside this design

- **Self-transition semantics.** Engine no-ops on same-instance return today. Re-enter API not exposed. Add only if a concrete case forces it.
- **Polymorphic `Current` cast in handlers / mutators.** Every handler that reads phase data does a one-line cast (`(BattlePhaseState)slice.Current`). Acceptable; could collapse with an extension method (`scope.GetMatchState<T>()`) if it gets noisy.
- **Wrapper vs sibling slices for phase-specific data.** Default is wrapper-with-polymorphic-field (`MatchStateSlice.Current.Attackers`); per-phase sibling slices (`BattlePhaseDataSlice`) are an alternative if subscriber fan-out becomes a concern. Defer until proven.
- **Engine-fold of `Evaluate`.** With SBAs + Flow externalized, `Evaluate`'s body is glue. Could fold fully into the engine, exposing only `runtime.Flow` + `runtime.StateBasedActions`. Open structural choice; both shapes work.
- **`IHandlerContext` collapse.** With only `Store` left, the wrapper might not earn its keep. Could pass `Store` directly. Cosmetic; either shape works.
- **`ISignalBus` placement.** Injected today. Move to context if "most handlers emit" proves true in practice.
