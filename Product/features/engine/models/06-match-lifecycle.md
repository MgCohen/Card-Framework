---
type: model
status: settled
references: [features/engine/models/01-evaluation-loop.md, features/engine/models/02-gamerules-contract.md, features/engine/models/04-gamestate-slices.md, features/engine/models/05-game-factory.md]
tags: [#engine, #lifecycle, #match]
---

# Match Lifecycle — model

## What it is

The framework's match lifecycle system. One `GameEngine` instance, one match — the system has three components:

1. **The FSM** — three states (`NotStarted` / `Started` / `Ended`) with defined semantics and valid transitions.
2. **The intake gate** — state-conditional rules over which commands `engine.Dispatch` admits, including the queue-tail-on-end semantics.
3. **The required implementation** — the framework-owned types that realize the system: `EngineLifecycle` enum, `StartMatchCommand` / `EndMatchCommand` and handlers, `MatchStartedSignal` / `MatchEndedSignal`, `MatchOutcome` closed hierarchy.

Broader session lifecycle (pause / resume / concede / disconnect / reconnect / multi-match orchestration) is a higher-layer concern that wraps the engine externally — not part of this system.

The `GameEngine` surface methods that drive transitions (`StartMatch` / `FinishMatch` / `Dispose` / `Lifecycle`) live in [`engine/01`](features/engine/models/01-evaluation-loop.md) (where `GameEngine` is canonical). Setup-command patterns and the disposal split between engine and host are recipes/conventions in [`engine.md`](features/engine/engine.md).

```csharp
public enum EngineLifecycle { NotStarted, Started, Ended }
```

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

Three states. No intermediate `Starting` / `Ending` — transitions are atomic from the public engine surface. Internal flags like "drain in progress" live separately on `EngineState` (see [`engine/01 §The EngineState slice`](features/engine/models/01-evaluation-loop.md)) and don't conflate with lifecycle.

The state is mirrored on `EngineState.Lifecycle` so any consumer reading state via `IStateScope` sees it without holding an engine reference. The `EngineState` slice is the source of truth for state-driven consumers; `engine.Lifecycle` (defined in [`engine/01`](features/engine/models/01-evaluation-loop.md)) is a convenience accessor for host code that already holds the engine.

### Lifecycle gate

The framework's intake gate is part of the lifecycle system. `engine.Dispatch` consults `EngineState.Lifecycle` before delegating to `pipeline.Validate` and admits a state-specific subset of commands. Per [ADR-0006](Product/decisions/0006-two-pipeline-architecture.md), the runtime no longer has an `OnIncomingCommand` hook — the gate runs first, uniformly. Enforcement lives in [`engine/01 §Lifecycle gate enforcement`](features/engine/models/01-evaluation-loop.md); this section defines the rules.

| Lifecycle | Command | Result |
|---|---|---|
| `NotStarted` | `StartMatchCommand` | Validate runs; handler flips to `Started` |
| `NotStarted` | `EndMatchCommand` | Validate runs; handler flips to `Ended` (cancel-before-start) |
| `NotStarted` | Any other | Reject(`"MatchNotStarted"`) before pipeline runs |
| `Started` | `EndMatchCommand` | Validate runs; handler flips to `Ended` |
| `Started` | Any other | Normal pipeline flow |
| `Ended` | Anything | Reject(`"MatchEnded"`) before pipeline runs |

**Queue tail drains naturally on end.** When `EndMatchCommand`'s handler flips `Lifecycle` to `Ended` mid-drain, the queue tail continues to execute — already-queued commands aren't pre-cleared. If a tail command tries to dispatch follow-ups during the same drain, those new dispatches hit the `Ended` gate and reject. Trigger reactions dispatched through `engine.Dispatch` *during* the same drain that ended the match are subject to the same gate. Evaluator follow-ups bypass `Dispatch` entirely (they go through the engine's internal `pending.Enqueue` after `Evaluate` returns), so they are *not* subject to the lifecycle gate; runtime authors who want to suppress evaluator follow-ups after match end check `EngineState.Lifecycle` from inside `Evaluate`.

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

## Invariants

1. **Three lifecycle states.** `NotStarted`, `Started`, `Ended`. No intermediate states on the public enum; drain-in-progress lives on `EngineState` separately.
2. **Lifecycle gate is uniform and framework-owned.** The gate consults `EngineState.Lifecycle` before `pipeline.Validate` runs and admits a state-specific subset of commands per the rules table. Per ADR-0006 the runtime has no intake hook.
3. **Queue tail drains naturally on end.** When the lifecycle flips to `Ended` mid-drain, the queue is not pre-cleared — already-queued commands continue executing in order. New `Dispatch` calls during the same drain hit the `Ended` gate and reject. Evaluator follow-ups bypass the gate.
4. **Lifecycle transitions are commands.** Every `NotStarted → Started` and `* → Ended` transition flows through the normal pipeline as `StartMatchCommand` or `EndMatchCommand`. Handlers emit signals and flip `EngineState.Lifecycle` via the framework-internal `SetEngineLifecyclePayload` mutator — no direct state writes.
5. **Signals land in normal envelopes.** `MatchStartedSignal` and `MatchEndedSignal` are emitted via `scope.Emit` from inside their respective handlers; they appear in `HistoryState` as ordinary `HistoryEntry`s.
6. **End-of-match has one transition path, multiple entry points.** Runtime / effect drives by dispatching `EndMatchCommand` (typically from `Evaluate`); host drives via `engine.FinishMatch(outcome)` (defined in [`engine/01`](features/engine/models/01-evaluation-loop.md)); `engine.Dispose` synthesizes `MatchOutcome.Aborted("Disposed")` if not already ended. All paths reach `EndMatchHandler.Handle`.
7. **`EndMatchCommand` permits `NotStarted → Ended`.** Cancel-before-start is supported. Only `MatchEndedSignal` lands; no start signal ever fired.
8. **`MatchOutcome` is a closed type hierarchy.** Abstract base + sealed subtypes (`Win`, `Loss`, `Draw`, `Aborted`); consumers pattern-match. No nullable-field tuple shape.

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

---

*Below: provenance.*

## Options considered

*n/a — descriptive Model. The match-lifecycle shape sits on top of [ADR-0006](Product/decisions/0006-two-pipeline-architecture.md) (the lifecycle gate replaces the deleted `OnIncomingCommand` runtime hook) and the existing command-pipeline conventions. No new options were considered for this Model itself.*

## Rationale

Routing every lifecycle transition through the normal pipeline as a framework-owned `GameAction` gives three things at no extra cost: signals land in normal envelopes (no synthesized-envelope code path), triggers fire naturally on lifecycle (`OnAfter<MatchStartedSignal>` etc. work via the same machinery), and the first snapshot opportunity is free (the drain after `StartMatchCommand` is the runtime's first chance to publish via `Actions.Set`). The framework cost is two commands and two handlers, registered by the framework's `EngineInstaller`.

The closed `MatchOutcome` hierarchy (vs. nullable-field tuple) lets consumers pattern-match cleanly without guarding `Winner is null` etc.

## Consequences

- **Enables** lifecycle-aware triggers and effects (`OnAfter<MatchStartedSignal>` etc.); deterministic replay (lifecycle transitions are envelopes in `HistoryState`); cancel-before-start (`NotStarted → Ended` is supported); pattern-matching consumers over `MatchOutcome` without nullable guards.
- **Constrains** lifecycle handlers to flip state via `SetEngineLifecyclePayload` (no direct writes); two framework-owned commands and handlers must be registered by `EngineInstaller`; `MatchOutcome` extension (e.g. `TeamWin`) requires adding a sealed subtype rather than overloading existing variants.
