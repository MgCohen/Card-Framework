---
type: model
status: settled
references: [features/engine/models/01-evaluation-loop.md, features/engine/models/02-gamerules-contract.md, features/engine/models/04-gamestate-slices.md, features/engine/models/05-game-factory.md]
tags: [#engine, #lifecycle, #match]
---

# Match Lifecycle — model

## What it is

How a `GameEngine` transitions from constructed → running → ended. The lifecycle has three states (`NotStarted` / `Started` / `Ended`); transitions flow through the normal pipeline as framework-owned `StartMatchCommand` / `EndMatchCommand`; the public `engine.StartMatch()` / `engine.FinishMatch(outcome)` are sugar that construct and dispatch. End-of-match is signalled via `MatchEndedSignal` carrying a `MatchOutcome` (closed type hierarchy: `Win` / `Loss` / `Draw` / `Aborted`). Disposal is split: the engine owns runtime-instance disposal; the host owns per-match DI scope teardown.

```csharp
public enum EngineLifecycle { NotStarted, Started, Ended }

public sealed class GameEngine : IDisposable
{
    public EngineLifecycle Lifecycle { get; }

    public bool StartMatch (out string? reason);
    public bool Dispatch   (GameAction action, out string? reason);
    public bool FinishMatch(MatchOutcome outcome, out string? reason);
    public void Dispose    ();
}
```

This Model owns the **match** lifecycle — one `GameEngine` instance, one match. It does **not** own broader session lifecycle: pause / resume / concede / disconnect / reconnect / multi-match orchestration are higher-layer concerns that wrap the engine externally.

## Contracts

### Lifecycle states

```csharp
public enum EngineLifecycle
{
    NotStarted,   // Build returned an engine; StartMatch not yet called
    Started,      // StartMatch ran; engine accepting Dispatch
    Ended,        // FinishMatch ran; engine no longer accepting Dispatch
}
```

Three states. No intermediate `Starting` / `Ending` — transitions are atomic from the public surface. Internal flags like "drain in progress" live separately on `EngineState` (see [`engine/04 §EngineState`](features/engine/models/04-gamestate-slices.md)) and don't conflate with lifecycle.

The state is mirrored on `EngineState.Lifecycle` so any consumer reading state via `IStateScope` sees it without holding an engine reference. The `EngineState` slice is the source of truth for state-driven consumers; `engine.Lifecycle` is a convenience accessor for host code that already holds the engine.

### Engine surface

```csharp
public sealed class GameEngine : IDisposable
{
    public EngineLifecycle Lifecycle { get; }

    // NotStarted → Started. Constructs and dispatches StartMatchCommand.
    public bool StartMatch(out string? reason);

    // Single dispatch entry point during normal play.
    public bool Dispatch(GameAction action, out string? reason);

    // Started → Ended (or NotStarted → Ended for cancel-before-start).
    // Constructs and dispatches EndMatchCommand. Idempotent: subsequent
    // calls return false with reason "MatchEnded".
    public bool FinishMatch(MatchOutcome outcome, out string? reason);

    // Releases the runtime if IDisposable. Calls FinishMatch(Aborted) if
    // not already Ended. Per-match DI scope teardown is the host's job.
    public void Dispose();
}
```

`StartMatch`, `Dispatch`, and `FinishMatch` all use the try-get pattern (`bool` + `out string? reason`) — symmetric across the engine's intake surface and matching `pipeline.Validate`. `true` on success with `reason = null`; `false` on rejection with a non-null reason. Calling `StartMatch` from a non-`NotStarted` state, or `FinishMatch` from `Ended`, returns `false` with a reason rather than throwing — lifecycle errors flow through the same surface as any other rejected command.

`Dispatch(GameAction)` (not `GameCommand`): per the type hierarchy, only `GameAction` is queueable. `GameSignal` is emitted via `scope.Emit` from inside handlers (never dispatched); `CompositeAction` runs inline within a parent handler (never queued).

### Transitions are commands

Every lifecycle transition flows through the normal pipeline as a framework-owned `GameAction`. The public methods are sugar that construct and dispatch.

#### `StartMatchCommand`

```csharp
public sealed record StartMatchCommand : GameAction;

public sealed record StartMatchResult(EngineLifecycle Lifecycle) : CommandResult;

internal sealed class StartMatchHandler
    : CommandHandler<StartMatchCommand, StartMatchResult>
{
    public override bool Validate(StartMatchCommand cmd, IStateScope state, out string? reason)
    {
        var current = state.Get<EngineState>().Lifecycle;
        reason = current == EngineLifecycle.NotStarted
            ? null
            : $"Cannot start match from {current}";
        return reason is null;
    }

    public override Task<StartMatchResult> Handle(StartMatchCommand cmd, IHandlerContext ctx)
    {
        ctx.Emit(new MatchStartedSignal());
        ctx.Store.Execute(new SetEngineLifecyclePayload(EngineLifecycle.Started));
        return Task.FromResult(new StartMatchResult(EngineLifecycle.Started));
    }
}
```

`engine.StartMatch()` is exactly `Dispatch(new StartMatchCommand())`. The handler emits `MatchStartedSignal` into its envelope (so the signal lands as a normal `HistoryEntry`) and flips `EngineState.Lifecycle` via the framework-internal `SetEngineLifecyclePayload`. The signal carries no fields — its presence in history is the fact.

#### `EndMatchCommand`

```csharp
public sealed record EndMatchCommand(MatchOutcome Outcome) : GameAction;

public sealed record EndMatchResult(MatchOutcome Outcome) : CommandResult;

internal sealed class EndMatchHandler
    : CommandHandler<EndMatchCommand, EndMatchResult>
{
    public override bool Validate(EndMatchCommand cmd, IStateScope state, out string? reason)
    {
        var current = state.Get<EngineState>().Lifecycle;
        reason = current is EngineLifecycle.NotStarted or EngineLifecycle.Started
            ? null
            : "Match already ended";
        return reason is null;
    }

    public override Task<EndMatchResult> Handle(EndMatchCommand cmd, IHandlerContext ctx)
    {
        ctx.Emit(new MatchEndedSignal(cmd.Outcome));
        ctx.Store.Execute(new SetEngineLifecyclePayload(EngineLifecycle.Ended));
        return Task.FromResult(new EndMatchResult(cmd.Outcome));
    }
}
```

`engine.FinishMatch(outcome)` is `Dispatch(new EndMatchCommand(outcome))`. The handler emits `MatchEndedSignal` (carrying the outcome) and flips `Lifecycle`. From `NotStarted`, no `MatchStartedSignal` ever fired — only the end signal lands. This is the cancel-before-start path.

#### Why commands rather than direct flips

Three benefits fall out of running lifecycle transitions through the pipeline:

- **Signals land in normal envelopes.** No "synthesized envelope" code path; `MatchStartedSignal` and `MatchEndedSignal` are ordinary `HistoryEntry`s with full envelope context (cause, chain, actor).
- **Triggers fire naturally.** `OnAfter<MatchStartedSignal>` / `OnAfter<MatchEndedSignal>` reach the trigger fold via the same machinery as any other command; cards or rules wanting to react to lifecycle do so without new mechanism.
- **First snapshot opportunity is free.** `StartMatchCommand`'s drain runs `runtime.Evaluate` after the handler completes — the runtime publishes its initial `AvailableActions` snapshot through `Actions.Set(...)` from inside `Evaluate` the same way it does for every subsequent drain.

The cost is two framework-owned commands and handlers, registered by the framework's `EngineInstaller`.

### Match setup

After `engine.StartMatch()` returns, the engine is in `Started` but the world is content-empty: zones spawned by `Build` exist but hold no cards; players exist with default variable values; game-singleton slices that needed actor refs (e.g. `TurnState.Active`) are still null. **Setup commands** are the framework's pattern for populating this content.

There is no special framework primitive for setup — setup commands are ordinary `GameAction`s dispatched through the normal pipeline, identifiable only by *when* they run (between `StartMatchCommand` and the first player-driven action). The framework supports two dispatch paths; games may use either or both.

#### Host-driven setup

The simplest pattern: the host (the code that called `engine.StartMatch()`) dispatches setup commands directly after `StartMatch` returns.

```csharp
var engine = factory.Create(matchSetup);
engine.StartMatch();                                   // dispatches StartMatchCommand

// Host-driven setup commands
foreach (var (zoneRef, cardDefs) in puzzle.InitialDeals)
    foreach (var def in cardDefs)
        engine.Dispatch(new CreateAndInsertCardCommand(def, zoneRef));

engine.Dispatch(new BeginFirstTurnCommand(firstPlayer));

// At this point, normal play begins.
```

Use this when the host knows the setup steps statically — solo puzzles, tutorials, scripted scenarios, tests.

#### Runtime-driven setup

Alternative: the `GameRulesRuntime` returns setup commands from its first `Evaluate` invocation. The drain after `StartMatchCommand` is the runtime's first opportunity to act; commands returned from `Evaluate` queue immediately and run before the host's next `Dispatch`.

```csharp
public class StandardMagicRulesRuntime : GameRulesRuntime<MagicRulesDefinition>
{
    private bool setupComplete = false;

    public override IReadOnlyList<GameAction> Evaluate(GameAction _, CommandResult __)
    {
        if (setupComplete) return Array.Empty<GameAction>();

        var actions = new List<GameAction>();

        // Deal opening hands, decide first player, advance to first phase, …
        foreach (var player in GetPlayers(State))
            for (int i = 0; i < Definition.StartingHandSize; i++)
                actions.Add(new DrawCardCommand(player));

        actions.Add(new BeginFirstTurnCommand(ChooseFirstPlayer(State)));

        setupComplete = true;
        return actions;
    }
}
```

Use this when setup logic is rule-defined (depends on game data, varies by mode) rather than host-defined. The runtime owns the timing and the order; the host doesn't need to know what setup looks like. (The `setupComplete` flag illustration above mixes concerns — in production it should be a state slice or computed from state, since per ADR-0002 the runtime holds zero gameplay state. Use `State.Get<SetupPhaseState>()` or similar.)

#### Recommended pattern

For most games, **runtime-driven setup is the cleaner default** — it keeps setup logic next to the game's other rules, survives replay (the runtime re-runs the same logic deterministically given the same `Definition` and seed), and frees the host from needing to know game-specific setup details. Host-driven setup is the right call for tests and scripted scenarios where you specifically want to bypass the runtime's setup logic.

Setup commands flow through the trigger fold like any other commands — `OnBefore<DrawCardCommand>` triggers fire on the opening-hand draws if any are registered. This is usually the desired behavior; games that need triggers to *not* fire during setup can guard via game state (e.g. a `SetupPhase` flag in `EngineState` or a per-game state slice).

#### Naming convention

Framework-owned commands carry the `MatchCommand` suffix (`StartMatchCommand`, `EndMatchCommand`). Game-authored setup commands have no required suffix; conventional names are `BeginFirstTurnCommand`, `DealOpeningHandsCommand`, `Setup<X>Command`. Choose names that read well in history envelopes — they appear in the canonical record alongside player actions.

### `MatchOutcome`

Outcomes are a closed type hierarchy — abstract base record, sealed subtype records — so consumers pattern-match without nullable-field gymnastics.

```csharp
public abstract record MatchOutcome
{
    public sealed record Win    (Actor   Winner, string? Reason = null) : MatchOutcome;
    public sealed record Loss   (Actor   Loser,  string? Reason = null) : MatchOutcome;
    public sealed record Draw   (string? Reason = null)                 : MatchOutcome;
    public sealed record Aborted(string  Reason)                        : MatchOutcome;
}
```

- `Win` requires a winner. Multi-winner scenarios (team / co-op) are a future extension — add a `TeamWin(IReadOnlyList<Actor> Winners, ...)` subtype when a real game forces it.
- `Loss` covers solo games where there's no winner to name (puzzle fail, time-out without an opponent). 2-player games typically express "A wins" rather than "B loses"; either is valid — pick the framing that reads cleanest in history.
- `Draw` carries no actor. Reason is optional (e.g., turn-limit, mutual elimination).
- `Aborted` requires a reason. Used for host-driven termination: window close, network drop, time-out, dispose-without-finish.

Consumer pattern-matching:

```csharp
var label = outcome switch
{
    MatchOutcome.Win  w => $"{w.Winner} wins",
    MatchOutcome.Loss l => $"{l.Loser} loses",
    MatchOutcome.Draw _ => "Draw",
    MatchOutcome.Aborted a => $"Aborted: {a.Reason}",
};
```

`Reason` is free-form on every variant for now; structuring (enum, localization keys) is deferred until presentation surfaces force the question.

### Disposal boundary

Two distinct teardowns, owned by different layers:

| Concern | Owner | When |
|---|---|---|
| Runtime instance disposal (if `IDisposable`) | `GameEngine` | `engine.Dispose()` |
| Per-match DI scope disposal | Host / outer controller | After observing match end |

`engine.Dispose()` releases what the engine itself constructed or holds:

1. If `Lifecycle != Ended`, call `FinishMatch(new MatchOutcome.Aborted("Disposed"))`. The end command runs through the pipeline, `MatchEndedSignal` lands in history, lifecycle flips.
2. If the runtime implements `IDisposable`, dispose it.
3. Mark internal state as disposed; subsequent `Dispatch` / `StartMatch` / `FinishMatch` throw `ObjectDisposedException`.

The engine does **not** dispose the per-match DI scope. The factory's `Build` operates within an outer-owned scope ([`engine/05 §Behavior resolution`](features/engine/models/05-game-factory.md)); the same outer layer that opened the scope tears it down. A typical app-level flow:

```
1. GameSessionController opens a per-match VContainer scope.
2. factory.NewMatch().With...().Build()  → engine resolved within that scope.
3. engine.StartMatch()                   → match runs.
4. ... gameplay ...
5. Match ends (runtime dispatches EndMatchCommand, host calls FinishMatch,
   or engine.Dispose triggers Aborted).
6. Controller observes Lifecycle = Ended (via state subscription, or signal,
   or polling) and disposes the VContainer scope, which cascades through
   game subsystems registered scope-locally (IStackSystem, IDeckSystem, ...).
```

The engine surfaces `MatchEndedSignal` in history; the controller hooks it however it likes. The framework does not ship a "match-end callback" mechanism on the engine — observation is via state or history, both of which already exist.

### Intake gates

`Dispatch` (and the public `StartMatch` / `FinishMatch` sugar that route through it) gates on lifecycle before any other validation:

| Lifecycle | Command | Result |
|---|---|---|
| `NotStarted` | `StartMatchCommand` | Validate runs; handler flips to `Started` |
| `NotStarted` | `EndMatchCommand` | Validate runs; handler flips to `Ended` (cancel-before-start) |
| `NotStarted` | Any other | Reject(`"MatchNotStarted"`) before pipeline runs |
| `Started` | `EndMatchCommand` | Validate runs; handler flips to `Ended` |
| `Started` | Any other | Normal pipeline flow |
| `Ended` | Anything | Reject(`"MatchEnded"`) before pipeline runs |

The lifecycle gate sits on the engine's `Dispatch` itself. Per [ADR-0006](Product/decisions/0006-two-pipeline-architecture.md), the runtime no longer has an `OnIncomingCommand` hook — the gate runs first, then `pipeline.Validate`. Lifecycle is a framework concern, applied uniformly.

Once `Lifecycle` flips to `Ended` mid-drain (via `EndMatchCommand`'s handler), the queue tail continues to drain naturally. Already-queued commands aren't pre-cleared; they execute in order. If a tail command tries to dispatch follow-ups during the drain, those new dispatches hit the `Ended` gate and reject. Trigger reactions dispatched through `engine.Dispatch` *during* the same drain that ended the match are subject to the same gate — they reject if dispatched after the lifecycle flip. Evaluator follow-ups bypass `Dispatch` entirely (they go through the engine's internal `pending.Enqueue` after `Evaluate` returns), so they are not subject to the lifecycle gate; runtime authors who want to suppress evaluator follow-ups after match end check `EngineState.Lifecycle` from inside `Evaluate`.

## Invariants

1. **Three lifecycle states.** `NotStarted`, `Started`, `Ended`. No intermediate states on the public enum; drain-in-progress lives on `EngineState` separately.
2. **Lifecycle transitions are commands.** Every `NotStarted → Started` and `* → Ended` transition flows through the normal pipeline as `StartMatchCommand` or `EndMatchCommand`. No direct state writes from the public methods; `engine.StartMatch` / `engine.FinishMatch` are sugar.
3. **Signals land in normal envelopes.** `MatchStartedSignal` and `MatchEndedSignal` are emitted via `scope.Emit` from inside their respective handlers; they appear in `HistoryState` as ordinary `HistoryEntry`s.
4. **Lifecycle gate is uniform and framework-owned.** `Dispatch` gates on `Lifecycle` before `pipeline.Validate` runs. `NotStarted` permits only `StartMatchCommand` and `EndMatchCommand`; `Ended` permits nothing. Per ADR-0006 there is no runtime-side intake hook.
5. **End-of-match has one transition path, two entry points.** Runtime / effect drives by dispatching `EndMatchCommand` (typically from `Evaluate`); host drives by calling `engine.FinishMatch(outcome)`. Both reach `EndMatchHandler.Handle`. `Dispose` is a third entry point that synthesizes `MatchOutcome.Aborted("Disposed")`.
6. **Queue tail drains naturally on end.** When the lifecycle flips to `Ended` mid-drain, the queue is not pre-cleared. Already-queued commands continue to execute. New dispatches hit the `Ended` gate.
7. **Engine owns runtime instance disposal; host owns scope disposal.** `engine.Dispose` releases the runtime if `IDisposable`. The per-match DI scope is opened and torn down by the host / outer controller — engine never sees the scope object.
8. **`FinishMatch` permits `NotStarted → Ended`.** Cancel-before-start is supported. Only `MatchEndedSignal` lands; no start signal ever fired.
9. **`MatchOutcome` is a closed type hierarchy.** Abstract base + sealed subtypes (`Win`, `Loss`, `Draw`, `Aborted`); consumers pattern-match. No nullable-field tuple shape.

## Decisions

*n/a — no elevated ADR.* (Match-lifecycle shape is single-feature scope. Resolves `oq-engine-match-lifecycle` from earlier checkpoint work.)

## Examples

### Pile Counter — start, play, win

```
host.engine.StartMatch()
  → Dispatch(new StartMatchCommand())
  → Lifecycle gate: NotStarted, command is StartMatchCommand → permitted
  → pipeline.Validate → handler.Validate(NotStarted) → ok
  → pipeline.Execute:
     - StartMatchHandler.Handle: ctx.Emit(MatchStartedSignal),
       Store.Execute(SetEngineLifecyclePayload(Started))
     - envelope closes carrying MatchStartedSignal
  → Evaluate: returns []
  → drain ends, runtime.Evaluate publishes initial AvailableActions via Actions.Set
  → engine.Lifecycle == Started

host.engine.Dispatch(new MoveCardCommand(top of A → B), out _)
  → Lifecycle gate: Started, normal command → permitted
  → handler.Validate, enqueue, drain runs
  → all four piles now match target
  → Evaluate: returns [new EndMatchCommand(new MatchOutcome.Win(player1))]
  → engine enqueues, dequeues
  → pipeline runs EndMatchHandler:
     - ctx.Emit(MatchEndedSignal(Win(player1)))
     - Store.Execute(SetEngineLifecyclePayload(Ended))
  → envelope closes carrying MatchEndedSignal
  → drain continues — queue is empty
  → runtime.Evaluate publishes terminal snapshot or no-ops
  → engine.Lifecycle == Ended

host observes MatchEndedSignal in history; tears down the per-match scope.
```

### Host abort

```
host.engine.FinishMatch(new MatchOutcome.Aborted("Window closed"))
  → Dispatch(new EndMatchCommand(...))
  → Lifecycle gate: Started, EndMatchCommand → permitted
  → EndMatchHandler.Handle: signal + state flip
  → Lifecycle == Ended
  → host disposes scope
```

### Cancel before start

```
host.engine.FinishMatch(new MatchOutcome.Aborted("Player backed out"))
  → Dispatch(new EndMatchCommand(...))
  → Lifecycle gate: NotStarted, EndMatchCommand → permitted
  → EndMatchHandler.Validate: NotStarted is allowed → ok
  → EndMatchHandler.Handle: signal + state flip
  → Lifecycle: NotStarted → Ended (no MatchStartedSignal ever fired)
```

### Dispose without finish

```
host.engine.Dispose()
  → Lifecycle != Ended → engine.FinishMatch(Aborted("Disposed"))
  → EndMatchCommand runs through pipeline, MatchEndedSignal lands
  → runtime: if IDisposable, dispose
  → engine marks itself disposed
```

## Open follow-ups

- **Async-effect cancellation on match end** — when `EndMatchCommand` resolves while an effect is mid-`await` (target picker, choose-one prompt), the awaiting effect is still alive. Whether the engine threads a `CancellationToken` through `IHandlerContext` / `IEffectScope` is an effect-flow concern, not a lifecycle concern.
- **Match-end observation API** — host currently observes match end via `MatchEndedSignal` in `HistoryState` or via state subscription on `EngineState.Lifecycle`. If a more ergonomic surface (e.g. `Task<MatchOutcome> MatchCompletion { get; }`) becomes obviously needed, add it. Defer until a real consumer surfaces.
- **Multi-winner / team outcomes** — `MatchOutcome.Win` is single-actor. Add `TeamWin` (or similar) when a co-op / team game forces it.
- **`MatchStartedSignal` payload** — currently fieldless; the fact-of-start is its presence in history. If a consumer needs the start time, RNG seed, or actor list embedded in the signal itself (rather than read from state), add fields then.
- **Per-match scope ownership convention** — this Model says the host owns the scope, but doesn't pin a concrete pattern (a `GameSession` controller, a VContainer scope-builder helper, etc.). Pin when a real game integrates with a real scope-bearing controller.

---

*Below: provenance.*

## Options considered

*n/a — descriptive Model. The match-lifecycle shape sits on top of [ADR-0006](Product/decisions/0006-two-pipeline-architecture.md) (the lifecycle gate replaces the deleted `OnIncomingCommand` runtime hook) and the existing command-pipeline conventions. No new options were considered for this Model itself.*

## Rationale

Routing every lifecycle transition through the normal pipeline as a framework-owned `GameAction` gives three things at no extra cost: signals land in normal envelopes (no synthesized-envelope code path), triggers fire naturally on lifecycle (`OnAfter<MatchStartedSignal>` etc. work via the same machinery), and the first snapshot opportunity is free (the drain after `StartMatchCommand` is the runtime's first chance to publish via `Actions.Set`). The framework cost is two commands and two handlers, registered by the framework's `EngineInstaller`.

Splitting disposal between engine (runtime instance) and host (DI scope) keeps the engine's responsibilities narrow — the engine doesn't know about VContainer or scope objects. The host that opened the scope owns its teardown. The engine owns what it constructed.

The closed `MatchOutcome` hierarchy (vs. nullable-field tuple) lets consumers pattern-match cleanly without guarding `Winner is null` etc.

## Consequences

- **Enables** lifecycle-aware triggers and effects (`OnAfter<MatchStartedSignal>` etc.); deterministic replay (lifecycle transitions are envelopes in `HistoryState`); cancel-before-start (`NotStarted → Ended` is supported); host-driven and runtime-driven setup patterns through the same dispatch surface; clean disposal split between engine (runtime) and host (scope).
- **Constrains** `engine.StartMatch` / `engine.FinishMatch` to construct and dispatch through the normal pipeline (no direct state writes); two framework-owned commands and handlers must be registered by `EngineInstaller`; queue-tail drains naturally on `Ended` (no pre-clear); evaluator follow-ups bypass the lifecycle gate (runtime authors guard via `State.Get<EngineState>().Lifecycle` from inside `Evaluate` if they want to suppress post-end follow-ups).
