---
type: model
status: superseded
superseded_by: decisions/0014-service-first-state-model.md
archived_on: 2026-10-18
reason: "Match state is one GameState in StateStore.Current, replaced via StateService.Push and StatePushEvent; MatchState and MatchStateSlice are retired."
references: [features/engine/archive/01-game-engine.md, features/engine/archive/03-game-rules-runtime.md, features/engine/archive/08-turn-flow.md, features/engine/archive/09-state-based-actions.md, features/state/archive/state.md, features/actions/archive/01-command-pipeline.md, decisions/0009-engine-as-fsm.md]
tags: [#engine, #match-state, #fsm]
---

> **Archived 2026-10-18.** Superseded by [`Product/decisions/0014-service-first-state-model.md`](Product/decisions/0014-service-first-state-model.md). Match state is one GameState in StateStore.Current, replaced via StateService.Push and StatePushEvent; MatchState and MatchStateSlice are retired. The body below is the old model, kept as history.

# Match State — model

> **Amendment (2026-05-11) per spike Q-walk locks.** Four refinements:
>
> 1. **Action-gate triad on `MatchState`.** `Actions: IReadOnlyList<Type>` (whitelist when active) + `ActionsOpen: bool` (default `true` — anything passes). `MatchStateActionGate` semantics: `ActionsOpen == true` → pass; `ActionsOpen == false` + command type ∈ `Actions` → pass; `ActionsOpen == false` + command type ∉ `Actions` → reject. Authoring patterns: default open (no override); locked state (`ActionsOpen = false`, empty `Actions`) for resolving / animating; whitelist (`ActionsOpen = false`, populated `Actions`) for normal phases.
> 2. **Lifecycle hooks become methods.** `EnterCommands` (virtual property) → **`OnEnter(IStateScope scope) → IReadOnlyList<GameAction>`** (virtual method). New **`OnExit(IStateScope scope) → IReadOnlyList<GameAction>`** mirror. `ShouldExit` renames to **`IsCompleted(action, result, scope) → bool`** for ergonomic naming (predicate, not directive). Methods can read scope to compute their lists; output stays data-only (`IReadOnlyList<GameAction>`).
> 3. **Engine drain for state transitions:**
>    - On state-type change, engine enqueues `oldState.OnExit(scope)` commands **before** `TransitionStateCommand(newState)` (so exits drain while `oldState` is still current).
>    - `TransitionStateHandler` writes `MatchStateSlice.Current = newState`, then enqueues `newState.OnEnter(scope)` commands (so entries drain while `newState` is current).
>    - Engine does NOT call `runtime.Evaluate` after `TransitionStateCommand` (transition-loop prevention invariant).
> 4. **Match end auto-emit.** Framework ships `EndOfMatchState(MatchOutcome Outcome) : MatchState` (`ActionsOpen = false`). When `Evaluate` returns `EndOfMatchState`, engine synthesizes the transition normally; after the transition handler completes, engine pattern-matches `MatchStateSlice.Current is EndOfMatchState` and auto-dispatches `EndMatchCommand(outcome)` — no host orchestrator required. `EndMatchCommand`'s handler flips `engine.Session = EndedSession(outcome)`. Games' `ITurnFlow.NextAfter` returns `EndOfMatchState(...)` when game logic decides the match is over. `EndedSession` is terminal — rematch is dispose-and-recreate via `GameFactory.Build`.

## What it is

The framework's first-class concept of "what state is the match in." **`MatchState`** is an abstract record; concrete subclasses (game-authored) describe individual phases / decision modes / cleanup states. **`MatchStateSlice`** is the framework-owned slice wrapping the polymorphic current state. **`TransitionStateCommand`** is the framework-owned command the engine dispatches when `runtime.Evaluate` returns a state whose type differs from the current — every transition flows through the same pipeline as every other command.

```csharp
public sealed record MatchStateSlice(MatchState Current) : BaseState;

public abstract record MatchState : BaseState {
    public virtual IReadOnlyList<Type>       Actions       => [];
    public virtual IReadOnlyList<GameAction> EnterCommands => [];
    public virtual bool ShouldExit(GameAction action, CommandResult result, IStateScope state) => false;
}

public sealed record TransitionStateCommand(MatchState NewState) : GameAction;
public sealed record TransitionResult                            : CommandResult;
```

```
runtime.Evaluate(action, result) → MatchState (newState)
                                      │
                       newState.GetType() == current.GetType()?
                              yes ──── no-op ──────┐
                              no                   │
                              ▼                    │
                   engine.Enqueue(                 │
                     new TransitionStateCommand(   │
                       newState))                  │
                              │                    │
                              ▼                    │
                  TransitionStateHandler:          │
                  1. Store.Execute(                │
                       SetMatchStatePayload(s))    │
                  2. enqueue newState.EnterCommands│
                              │                    │
                              ▼                    ▼
                       drain continues
```

## Contracts

### Why a wrapper slice for a polymorphic state

Scaffold's `Store` keys slices on `(Reference, typeof(TState))` with **exact-type match** on the secondary key — there is no `IsAssignableFrom` walk. A polymorphic `MatchState` cannot itself be a slice: writing a `MainPhaseState` and reading a `MatchState` would miss. The wrapper resolves this:

- `MatchStateSlice` is the slice (one record type, framework-owned).
- `MatchState` is the polymorphic shape stored *inside* the slice via the `Current` field.

Single subscription channel (`store.Subscribe<MatchStateSlice>(handler)`); pattern-match `Current` to read phase-specific data. Mutators that touch phase-specific data target `MatchStateSlice` with a type guard (see §Same-type field updates below).

### The three virtual members

Each concrete `MatchState` overrides what it cares about; the base defaults are sensible empties.

#### `Actions` — valid command types

```csharp
public virtual IReadOnlyList<Type> Actions => [];
```

What it's for: a **prophylactic intake gate** — the framework registers `MatchStateActionGate` per [ADR-0010](Product/decisions/0010-validator-handler-split.md) (see [`actions/06-command-validator.md`](features/actions/archive/06-command-validator.md)) that rejects commands whose `GetType()` is not in `current.Actions`. Also used by tooling ("what commands are legal right now?") and by network legality (clients check before submitting).

**Authoritative legality lives in the per-command validator chain** (per ADR-0010). `Actions` is a cheap first cut at the framework-default layer; game-shipped `ICommandValidator<TCommand>` validators own "does this specific command pass against current state?"

Type-only — no `ActionTemplate` wire-form, no targets / modes / parameters. Richer presentation-side projection lives in per-state ViewModels per [ADR-0012](Product/decisions/0012-presentation-per-state-view-models.md).

#### `EnterCommands` — commands the engine enqueues on entry

```csharp
public virtual IReadOnlyList<GameAction> EnterCommands => [];
```

What it's for: framework-driven setup when entering a new state. `TransitionStateHandler` enqueues each `GameAction` in order *after* the slice is written. Useful for "draw 2 cards on turn start" (a `BeginningOfTurnState` lists `[new DrawCardCommand(p), new DrawCardCommand(p)]`) or "tap untapped permanents on Untap step entry."

Each `EnterCommand` flows through the normal pipeline: validators, handler, trigger fold, envelope. They are not synthesized writes — they are real `GameAction`s that other code can intercept.

#### `ShouldExit` — exit predicate

```csharp
public virtual bool ShouldExit(GameAction action, CommandResult result, IStateScope state) => false;
```

What it's for: predicate the runtime's default `Evaluate` consults to ask "did this just-executed action move us out of this state?" Pure read; no mutations, no dispatch, no successor knowledge.

Default `false` is correct for states that exit only via cross-cutting overrides (SBAs) — e.g., a `GameOverState` is entered by an SBA-detected condition, never via predicate. Most phase / step states will override.

#### Phase-specific fields

Concrete subclasses carry whatever fields the state needs. Examples:

```csharp
public sealed record MainPhaseState(IActor ActivePlayer, int PhaseNumber)         : MatchState;
public sealed record DeclareAttackersState(
    IActor ActivePlayer,
    IReadOnlyList<Ref<Card>> SelectedAttackers)                                   : MatchState;
public sealed record GameOverState(MatchOutcome Outcome)                          : MatchState;
```

Pattern-match `Current` to read them:

```csharp
var current = scope.Get<MatchStateSlice>().Current;
if (current is DeclareAttackersState attack && attack.SelectedAttackers.Count > 0) {
    // ...
}
```

### `TransitionStateCommand` — the only transition path

`TransitionStateCommand` is **framework-internal** — dispatched by the engine drain after `Evaluate` detects a type-change, not authored by games. The handler is also framework-internal:

```csharp
public sealed class TransitionStateHandler
    : GameCommandHandler<TransitionStateCommand, TransitionResult>
{
    private readonly IEngineQueue engine;

    public TransitionStateHandler(IEngineQueue engine)
    {
        this.engine = engine;
    }

    // Store comes from the GameCommandHandler<,> base via [Inject] (per ADR-0010).
    public override Task<TransitionResult> Handle(TransitionStateCommand cmd)
    {
        Store.Execute(new SetMatchStatePayload(cmd.NewState));
        foreach (var enter in cmd.NewState.EnterCommands)
            engine.Enqueue(enter);
        return Task.FromResult(new TransitionResult());
    }
}
```

Per [ADR-0010](Product/decisions/0010-validator-handler-split.md), this handler extends `GameCommandHandler<TransitionStateCommand, TransitionResult>` (property-injected `Store`) and constructor-injects `IEngineQueue` for the follow-up enqueue. No validator is registered against `TransitionStateCommand` — it's framework-internal and bypasses the validator chain on enqueue.

### `SetMatchStatePayload` and the mutator

```csharp
public sealed record SetMatchStatePayload(MatchState NewState);

public sealed class SetMatchStateMutator : Mutator<MatchStateSlice, SetMatchStatePayload> {
    public override MatchStateSlice Change(
        MatchStateSlice current,
        SetMatchStatePayload payload,
        IStateScope scope)
        => new MatchStateSlice(payload.NewState);
}
```

The framework registers `SetMatchStateMutator` and reserves `SetMatchStatePayload` for `TransitionStateHandler`'s use. **Games never dispatch `SetMatchStatePayload` directly** — transitions flow through `TransitionStateCommand` so they appear in history and pass through the trigger fold. The mutator is reachable to games for the same-type field-update pattern below.

### Same-type field updates (`with` and guard mutators)

`Evaluate` only triggers a `TransitionStateCommand` when the returned state's type *differs*. Updating phase-specific fields on the *same-typed* state (e.g., adding an attacker to `DeclareAttackersState.SelectedAttackers`) doesn't go through `TransitionStateCommand` — that would be the wrong channel (no type change). Use a game-supplied payload + mutator that targets `MatchStateSlice` with a type guard:

```csharp
public sealed record AddAttackerPayload(Ref<Card> Card);

public sealed class AddAttackerMutator : Mutator<MatchStateSlice, AddAttackerPayload> {
    public override MatchStateSlice Change(
        MatchStateSlice current,
        AddAttackerPayload payload,
        IStateScope scope)
    {
        if (current.Current is not DeclareAttackersState d) return current;
        return new MatchStateSlice(d with {
            SelectedAttackers = [..d.SelectedAttackers, payload.Card]
        });
    }
}
```

The guard pattern keeps writes locally typed without leaking into `Evaluate`. Multiple game-authored mutators can target `MatchStateSlice` — each with its own payload and its own type guard.

### Type-equality semantics on transitions

The engine compares `Evaluate`'s return value's `.GetType()` to the current state's `.GetType()`:

```csharp
var newState = runtime.Evaluate(action, result);
var current  = store.Get<MatchStateSlice>().Current;
if (newState.GetType() != current.GetType())
    engine.Enqueue(new TransitionStateCommand(newState));
// Same type returned → no transition. EnterCommands do not re-fire.
```

**Why type-equality, not reference-equality.** Records are immutable; constructing a fresh same-typed instance with updated fields (the `with` pattern) shouldn't accidentally re-trigger entry. Type-equality matches authorial intent: "we're still in the same kind of state."

If a state genuinely needs to re-enter (e.g., extra-turn cards in MTG that re-enter `UntapStep`), today the workaround is to introduce a distinct state type (`ExtraTurnUntapStepState`) or have an SBA force the transition. Explicit `Reenter()` semantics on `TransitionStateCommand` is deferred until a real game forces it.

### Initial state seeding

`MatchStateSlice` is framework-owned and auto-seeded at `Build()`. The seed value comes from the game's `ITurnFlow.Initial` (see [`08-turn-flow.md`](features/engine/archive/08-turn-flow.md)). The factory walks:

```
factory.Build(matchSetup):
  ...
  Store.Execute(new SetMatchStatePayload(turnFlow.Initial))   // framework-internal
  ...
```

The initial state's `EnterCommands` fire on the first drain after `engine.StartMatch()`.

### Subscriptions

Presentation subscribes to `MatchStateSlice` to learn "the state changed":

```csharp
store.Subscribe<MatchStateSlice>((reference, slice, ev) => {
    if (ev != StateChangeEvent.Updated) return;
    var current = slice.Current;
    switch (current) {
        case MainPhaseState m:        ShowMainPhaseUI(m);     break;
        case DeclareAttackersState d: ShowCombatPickerUI(d);  break;
        case GameOverState g:         ShowEndScreen(g);       break;
    }
});
```

Per-state polymorphic ViewModels ([ADR-0012](Product/decisions/0012-presentation-per-state-view-models.md)) make this clean — the registry maps state-type to factory; the presenter binds the right VM on each transition.

## Invariants

1. **`MatchStateSlice` is framework-owned, auto-seeded.** Seeded at `Build()` from `ITurnFlow.Initial`. Game authors never declare it in `MatchSetup.InitialSlices`; including it throws at validation.
2. **`MatchState` is data plus one predicate.** Three virtual members (`Actions`, `EnterCommands`, `ShouldExit`) plus phase-specific fields. No `OnEnter(ctx)`, no `Next(action) → MatchState`. Successor logic lives in [`ITurnFlow`](features/engine/archive/08-turn-flow.md); cross-cutting overrides live in [`ISBA`](features/engine/archive/09-state-based-actions.md).
3. **Transitions flow through the command pipeline.** `TransitionStateCommand` is the only path that writes `MatchStateSlice`. `Evaluate` returns a `MatchState`; the engine drain detects a type change and dispatches. Triggers fire (`OnAfter<TransitionStateCommand>` works naturally); history records the envelope; replay reconstructs the exact sequence.
4. **Type-equality on transitions.** Same `GetType()` returned = no transition. `EnterCommands` do not re-fire on same-typed updates.
5. **`EnterCommands` fire after the slice is written.** The handler writes via `SetMatchStatePayload` first, then enqueues each `EnterCommand` in order. Each enters the queue and runs through the normal pipeline.
6. **Same-type field updates use game-supplied payloads with type guards.** `Evaluate` is not the place to update fields — return the current state and let game-supplied mutators handle the field write.
7. **`SetMatchStatePayload` is reserved for the framework's `TransitionStateHandler`.** Games target `MatchStateSlice` with their own payloads and type guards.

## Examples

### Pile Counter — single playing state + SBA-driven game over

```csharp
public sealed record PilePlayingState : MatchState {
    public override IReadOnlyList<Type> Actions => [typeof(MoveCardCommand)];
}

public sealed record PileGameOverState(MatchOutcome Outcome) : MatchState {
    public override IReadOnlyList<Type> Actions => [];
}
```

Pile Counter has no phase structure — one perpetual `PilePlayingState`. The win condition lives as an SBA ([`09-state-based-actions.md`](features/engine/archive/09-state-based-actions.md)) that returns `PileGameOverState(MatchOutcome.Win(player))` when all piles match their targets. `ITurnFlow.Initial` returns `new PilePlayingState()`; `ITurnFlow.NextAfter` is never called because `ShouldExit` always returns false.

### Hearthstone-shaped phase loop

```csharp
public sealed record HsBeginningOfTurnState(IActor ActivePlayer) : MatchState {
    public override IReadOnlyList<GameAction> EnterCommands => [
        new UntapAllCommand(ActivePlayer),
        new DrawCardCommand(ActivePlayer),
        new ResetManaCommand(ActivePlayer),
    ];
    public override IReadOnlyList<Type> Actions => [];                       // setup only; no player input
    public override bool ShouldExit(GameAction action, CommandResult result, IStateScope state)
        => action is ResetManaCommand;
}

public sealed record HsMainPhaseState(IActor ActivePlayer) : MatchState {
    public override IReadOnlyList<Type> Actions => [
        typeof(PlayCardCommand),
        typeof(AttackCommand),
        typeof(EndTurnCommand),
    ];
    public override bool ShouldExit(GameAction action, CommandResult result, IStateScope state)
        => action is EndTurnCommand;
}

public sealed record HsEndPhaseState(IActor ActivePlayer) : MatchState {
    public override IReadOnlyList<GameAction> EnterCommands => [
        new FireEndOfTurnTriggersCommand(),
        new EnforceHandSizeCommand(ActivePlayer),
    ];
    public override bool ShouldExit(GameAction action, CommandResult result, IStateScope state)
        => action is EnforceHandSizeCommand;
}
```

The `ITurnFlow.NextAfter` for this shape walks the three states in order, then swaps `ActivePlayer` on cycle.

### MTG-shaped declare-attackers (phase-specific fields)

```csharp
public sealed record DeclareAttackersState(
    IActor                   ActivePlayer,
    IReadOnlyList<Ref<Card>> SelectedAttackers)
    : MatchState
{
    public override IReadOnlyList<Type> Actions => [
        typeof(SelectAttackerCommand),
        typeof(DeselectAttackerCommand),
        typeof(CommitAttackersCommand),
    ];
    public override bool ShouldExit(GameAction action, CommandResult result, IStateScope state)
        => action is CommitAttackersCommand;
}
```

Per-attacker selections mutate the state via the `AddAttackerMutator` guard pattern shown above (`AddAttackerPayload(Ref<Card>)` runs through the same-type field-update path). `Evaluate` keeps returning `DeclareAttackersState` until `CommitAttackersCommand` triggers `ShouldExit`; the runtime's Flow then transitions to `DeclareBlockersState`.

## Decisions

*Provenance: [ADR-0009](Product/decisions/0009-engine-as-fsm.md) — D1, D2, D3.*

## Open follow-ups

- **Authoring DSL.** Concrete `MatchState` subclasses are hand-authored records. A visual-scripting binding could declaratively express states + EnterCommands + ShouldExit guards. Defer to the authoring feature.
- **Explicit re-enter semantics.** Same-type returns are no-ops. Games that need re-entry (extra-turn cards, repeating-phase shapes) will force `TransitionStateCommand` to carry a `Reenter` flag — or a distinct-type workaround.
- **Phase-specific field ergonomics.** The `with` pattern + guard mutator is workable but verbose for states with many fields. Builder helpers / record DSL deferred.
- **`MatchStateSlice` initial-state contract.** The factory reads `ITurnFlow.Initial` to seed at `Build()`. Whether `Initial` is the right contract or whether it should live on `GameDefinition` is pinned in [`08-turn-flow.md`](features/engine/archive/08-turn-flow.md); revisit if Flow's surface grows.
