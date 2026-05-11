---
type: model
status: settled
references: [features/engine/models/03-game-rules-runtime.md, features/engine/models/07-match-state.md, features/engine/models/09-state-based-actions.md, decisions/0009-engine-as-fsm.md]
tags: [#engine, #turn-flow, #fsm]
---

# Turn Flow — model

## What it is

The single source of turn-structure truth. **`ITurnFlow`** declares the game's transition table: given the current `MatchState`, what state follows it? The runtime's default `Evaluate` body calls `Flow.NextAfter(current)` when `current.ShouldExit(...)` returns true. Adding a phase = new `MatchState` subclass + new arm in `NextAfter`. Tweaking turn order = edit `ITurnFlow` only; state classes don't change.

```csharp
public interface ITurnFlow {
    MatchState Initial   { get; }                    // initial state seeded at Build()
    MatchState NextAfter (MatchState current);       // successor when current.ShouldExit returns true
}
```

```
runtime.Evaluate(action, result):
  ┌─ SBAs preempt — see engine/09 ─────────────────────────┐
  │ foreach (sba in StateBasedActions)                     │
  │   if (sba.Check(State) is { } over) return over;       │
  └────────────────────────────────────────────────────────┘
                              │
                              ▼
              current = State.Get<MatchStateSlice>().Current
                              │
                              ▼
         current.ShouldExit(action, result, State)?
              yes ──► Flow.NextAfter(current) ──► return
              no  ──► return current
```

## Contracts

### Position in the runtime

The runtime base class declares `Flow` and `StateBasedActions` as abstract — concrete runtimes wire their game-specific implementations:

```csharp
public abstract class GameRulesRuntime<TDefinition> where TDefinition : GameRulesDefinition
{
    protected abstract ITurnFlow            Flow              { get; }
    protected abstract IReadOnlyList<ISBA>  StateBasedActions { get; }

    public virtual MatchState Evaluate(GameAction action, CommandResult result) {
        foreach (var sba in StateBasedActions)
            if (sba.Check(State) is { } over) return over;

        var current = State.Get<MatchStateSlice>().Current;
        return current.ShouldExit(action, result, State)
            ? Flow.NextAfter(current)
            : current;
    }
    // ...
}
```

`ITurnFlow` is typically constructor-injected into the concrete `GameRulesRuntime<TDefinition>` via DI. Game-mode variants (Standard vs. Commander vs. Two-Headed Giant) swap the implementation without touching state classes.

### `Initial` — the seed state

`MatchStateSlice` is auto-seeded at `Build()`. The factory walks:

```csharp
Store.Execute(new SetMatchStatePayload(turnFlow.Initial));
```

This runs before `engine.StartMatch()`. The initial state's `EnterCommands` fire on the first drain after `StartMatch`.

`Initial` is a property, not a method — the value is fixed per `ITurnFlow` implementation and immutable for the match's lifetime. Games that need different initial states for different scenarios swap `ITurnFlow` implementations at match construction (or via `GameDefinition` variants).

### `NextAfter` — the transition table

The single method that defines the turn shape. Pure: no state writes, no dispatch, no side effects. Pattern-match on the current state and return the successor:

```csharp
public sealed class HearthstoneTurnFlow : ITurnFlow
{
    public MatchState Initial => new HsBeginningOfTurnState(_first);

    public MatchState NextAfter(MatchState current) => current switch {
        HsBeginningOfTurnState(var p) => new HsMainPhaseState(p),
        HsMainPhaseState(var p)       => new HsEndPhaseState(p),
        HsEndPhaseState(var p)        => new HsBeginningOfTurnState(OtherPlayer(p)),
        _                              => current,
    };

    // ...
}
```

**Return values:**

- A **different-typed** state — the typical case. The engine detects the type change and dispatches `TransitionStateCommand`.
- The **same-typed** state with updated fields — `current with { ... }` for incremental progression (e.g., `MainPhaseState { PhaseNumber: 1 }` → `MainPhaseState { PhaseNumber: 2 }`). The engine no-ops on type-equality; the runtime's intent is "stay in this kind of state, with these fields updated." To make the field update visible, return the new same-typed instance and let a game-supplied mutator catch the change via type guard (see [`07-match-state.md` §Same-type field updates](features/engine/models/07-match-state.md)). Or: don't update fields in `NextAfter`; let domain commands + mutators do it.
- The **current instance** — idempotent stay. The engine no-ops.

**`NextAfter` is only called when `ShouldExit` returns true.** A state whose `ShouldExit` is always `false` never reaches Flow. This is the canonical pattern for SBA-driven states (e.g., `GameOverState` — only enters via SBA, never exits).

### State + Flow ownership split — why?

Concrete states own:
- Their phase-specific fields
- Their `Actions` list (what command types are valid here)
- Their `EnterCommands` (what runs when we arrive)
- Their `ShouldExit` predicate ("are we done?")

`ITurnFlow` owns:
- The initial state
- The successor map ("after this, what comes next?")

Splitting them gives three properties:

1. **Single source of turn-structure truth.** All `NextAfter` arms in one place. Adding a phase = one new arm + one new state class. Reordering = edit Flow only.
2. **Swappable Flows over the same state set.** Standard MTG, Commander, Two-Headed Giant share most state types but differ in turn ordering — swap Flow, reuse states.
3. **Visual-scripting friendly.** Each `NextAfter` arm is a transition-table entry; a node-graph editor maps cleanly. States become "vertices with payload"; Flow becomes "edges with guards."

The alternative — `MatchState.Next() → MatchState` on each state — distributes successor knowledge across every state subclass. Renaming a phase forces a cascade; visual scripting becomes harder.

### Guard-conditional arms

The `NextAfter` switch can branch on state, not just type. Inject `IStateScope` if needed for state-dependent guards:

```csharp
public sealed class MtgTurnFlow : ITurnFlow
{
    private readonly IStateScope _state;
    public MtgTurnFlow(IStateScope state) { _state = state; }

    public MatchState NextAfter(MatchState current) => current switch {
        CombatDamageState(var p) when NoCreaturesAttacked(p) => new EndOfCombatState(p),
        CombatDamageState(var p)                              => new SecondMainPhaseState(p),
        // ...
        _ => current,
    };

    private bool NoCreaturesAttacked(IActor p) => /* read attackers from state */;
}
```

The reads are still pure (no writes); `IStateScope` is read-only by design. SBA-shaped logic still belongs as `ISBA` ([`09-state-based-actions.md`](features/engine/models/09-state-based-actions.md)) — Flow's guards are for "structural successor depends on state," SBAs are for "cross-cutting override regardless of which state we're in."

### `NullTurnFlow` for stateless games

Games with a single perpetual state (Pile Counter) ship a trivial Flow:

```csharp
public sealed class PileCounterTurnFlow : ITurnFlow
{
    public MatchState Initial               => new PilePlayingState();
    public MatchState NextAfter(MatchState) => throw new InvalidOperationException(
        "PileCounter has no exit predicate; NextAfter should not be called.");
}
```

Or the framework's `NullTurnFlow<TState>` (planned, deferred until a second consumer surfaces):

```csharp
public sealed class NullTurnFlow<TState> : ITurnFlow where TState : MatchState, new() {
    public MatchState Initial               => new TState();
    public MatchState NextAfter(MatchState) => Initial;   // idempotent
}
```

## Invariants

1. **`NextAfter` is pure.** No state writes, no dispatch, no side effects beyond reads through an injected `IStateScope`.
2. **`NextAfter` is only consulted when `ShouldExit` returns true.** The runtime's default `Evaluate` body enforces this. Games that need a different ordering override `Evaluate`.
3. **SBAs preempt Flow.** Per [ADR-0009](Product/decisions/0009-engine-as-fsm.md), the default `Evaluate` body runs registered SBAs first; if any returns a non-null override, Flow is not consulted.
4. **`Initial` is immutable per `ITurnFlow` instance.** The seed value is fixed; games swap implementations for different starting shapes.
5. **One `ITurnFlow` per match.** Wired into the concrete `GameRulesRuntime<TDefinition>` via DI; the runtime's `Flow` property is constructor-injected and immutable for the runtime's lifetime.

## Examples

### Pile Counter — perpetual single state

```csharp
public sealed class PileCounterTurnFlow : ITurnFlow {
    public MatchState Initial               => new PilePlayingState();
    public MatchState NextAfter(MatchState) => throw new InvalidOperationException(
        "PileCounter never exits PilePlayingState via ShouldExit; transitions are SBA-driven.");
}
```

The `WinConditionSBA` ([`09-state-based-actions.md`](features/engine/models/09-state-based-actions.md)) returns `PileGameOverState(MatchOutcome.Win(player))` when the win condition is met. `NextAfter` is never reached because `PilePlayingState.ShouldExit` always returns `false`.

### Hearthstone — three-phase turn loop

```csharp
public sealed class HearthstoneTurnFlow : ITurnFlow
{
    private readonly IActor _first;
    public HearthstoneTurnFlow(IActor firstPlayer) { _first = firstPlayer; }

    public MatchState Initial => new HsBeginningOfTurnState(_first);

    public MatchState NextAfter(MatchState current) => current switch {
        HsBeginningOfTurnState(var p) => new HsMainPhaseState(p),
        HsMainPhaseState(var p)       => new HsEndPhaseState(p),
        HsEndPhaseState(var p)        => new HsBeginningOfTurnState(OtherPlayer(p)),
        _                              => current,
    };

    private static IActor OtherPlayer(IActor p) => /* lookup opposing actor */;
}
```

### MTG — five-step turn with sub-phases

```csharp
public sealed class StandardMtgTurnFlow : ITurnFlow
{
    private readonly IActor _first;
    public StandardMtgTurnFlow(IActor firstPlayer) { _first = firstPlayer; }

    public MatchState Initial => new UntapStepState(_first);

    public MatchState NextAfter(MatchState current) => current switch {
        UntapStepState (var p)               => new UpkeepStepState(p),
        UpkeepStepState(var p)               => new DrawStepState(p),
        DrawStepState(var p)                 => new PreCombatMainPhaseState(p),
        PreCombatMainPhaseState(var p)       => new BeginningOfCombatStepState(p),
        BeginningOfCombatStepState(var p)    => new DeclareAttackersState(p, []),
        DeclareAttackersState(var p, _)      => new DeclareBlockersState(p, []),
        DeclareBlockersState(var p, _)       => new CombatDamageStepState(p),
        CombatDamageStepState(var p)         => new EndOfCombatStepState(p),
        EndOfCombatStepState(var p)          => new PostCombatMainPhaseState(p),
        PostCombatMainPhaseState(var p)      => new EndStepState(p),
        EndStepState(var p)                  => new CleanupStepState(p),
        CleanupStepState(var p)              => new UntapStepState(OtherPlayer(p)),
        _                                     => current,
    };

    private static IActor OtherPlayer(IActor p) => /* lookup opposing actor */;
}
```

Compose this with `CommanderRulesSBA`, `MulliganSBA`, etc. ([`09-state-based-actions.md`](features/engine/models/09-state-based-actions.md)) for variant rule sets without touching state classes.

## Decisions

*Provenance: [ADR-0009](Product/decisions/0009-engine-as-fsm.md) — D4.*

## Open follow-ups

- **State-aware guards on `NextAfter`.** Injecting `IStateScope` for guard arms works; whether the interface should expose `NextAfter(MatchState, IStateScope)` directly (avoiding the inject-into-constructor pattern) is a small ergonomics question. Defer until a real game forces the signature.
- **Composing multiple Flows for sub-games / modes.** A sideboard mini-game, a token-spawning sub-flow, or a multi-stage tournament. Defer; no current game forces it.
- **`NullTurnFlow<TState>`.** Worth a framework helper for the common "single perpetual state, SBA-driven exit" case. Defer until a second consumer (beyond Pile Counter) surfaces.
- **`ITurnFlow.Initial` vs `GameDefinition.InitialState`.** Initial-state seeding currently goes through Flow. Whether it should live on `GameDefinition` directly (so games without phase structure don't need a Flow at all) is open. Defer until non-Pile-Counter consumers express a preference.
