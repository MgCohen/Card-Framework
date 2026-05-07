---
type: model
status: settled
references: [features/engine/models/01-evaluation-loop.md, features/engine/models/02-gamerules-contract.md, features/engine/models/04-gamestate-slices.md, features/engine/models/05-game-factory.md]
tags: [#engine, #lifecycle, #match]
---

# Match Lifecycle — model

## What it is

How a `GameEngine` transitions from constructed → running → ended: where the lifecycle API lives, what `StartMatch` / `FinishMatch` actually do, how end-of-match is signalled, what disposal covers, and what gets gated where.

This Model owns the **match** lifecycle — one `GameEngine` instance, one match, transitions between *not yet started*, *running*, and *ended*. It does **not** own broader session lifecycle: pause / resume / concede / disconnect / reconnect / multi-match orchestration are higher-layer concerns that wrap the engine externally and stay out of scope here. A future `IGameSession` (or app-level controller) composes `GameEngine` for those needs. The per-match DI scope is also **not** owned by the engine — see Disposal Boundary below.

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

The state is mirrored on `EngineState.Lifecycle` so any consumer reading state via `IStateScope` sees it without holding an engine reference. The `EngineState` slice is the source of truth; `engine.Lifecycle` is a convenience accessor.

### Engine surface

```csharp
public sealed class GameEngine : IDisposable
{
    public EngineLifecycle Lifecycle { get; }

    // NotStarted → Started. Constructs and dispatches StartMatchCommand.
    public IntakeResult StartMatch();

    // Single dispatch entry point during normal play.
    public IntakeResult Dispatch(GameCommand command);

    // Started → Ended (or NotStarted → Ended for cancel-before-start).
    // Constructs and dispatches EndMatchCommand. Idempotent: subsequent
    // calls return Reject("MatchEnded").
    public IntakeResult FinishMatch(MatchOutcome outcome);

    // Releases the runtime if IDisposable. Calls FinishMatch(Aborted) if
    // not already Ended. Per-match DI scope teardown is the host's job.
    public void Dispose();
}
```

`StartMatch` and `FinishMatch` return `IntakeResult` (the same shape every dispatch produces) so the host sees a uniform success/reject surface. Calling `StartMatch` from a non-`NotStarted` state, or `FinishMatch` from `Ended`, surfaces as a normal `IntakeResult.Reject` rather than an exception.

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

`engine.StartMatch()` is exactly `Dispatch(new StartMatchCommand())`. The handler emits `MatchStartedSignal` into its envelope and flips `EngineState.Lifecycle` via the framework-internal `SetEngineLifecyclePayload`. The signal carries no fields — its presence in history is the fact.

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
- **First snapshot opportunity is free.** `StartMatchCommand`'s drain runs `OnQueueDrained` after the handler completes — the runtime publishes its initial `AvailableActions` snapshot through the same hook it uses every other turn.

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
- `Loss` covers solo games where there's no winner to name (puzzle fail, time-out without an opponent). 2-player games typically express "A wins" rather than "B loses"; either is valid.
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

The engine does **not** dispose the per-match DI scope. The factory's `Build` operates within an outer-owned scope; the same outer layer that opened the scope tears it down. A typical app-level flow:

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

The lifecycle gate sits on the engine's `Dispatch` itself, not in the runtime's `OnIncomingCommand`. Reasoning: lifecycle is a framework concern, applied uniformly. The runtime's `OnIncomingCommand` still runs (after the lifecycle gate) for game-specific intake policy.

Once `Lifecycle` flips to `Ended` mid-drain (via `EndMatchCommand`'s handler), the queue tail continues to drain naturally. Already-queued commands aren't pre-cleared; they execute in order. New dispatches hit the `Ended` gate and reject. Trigger reactions and evaluator follow-ups dispatched *during* the same drain that ended the match are subject to the same gate — they reject if dispatched after the lifecycle flip.

## Invariants

Model-local invariants. (See `engine.md ## Invariants` for feature-level rules — lifecycle gate framework-owned, lifecycle-as-commands, signals-in-envelopes.)

1. **Three lifecycle states.** `NotStarted`, `Started`, `Ended`. No intermediate states on the public enum; drain-in-progress lives on `EngineState` separately.
2. **Lifecycle transitions are commands.** Every `NotStarted → Started` and `* → Ended` transition flows through the normal pipeline as `StartMatchCommand` or `EndMatchCommand`.
3. **Signals land in normal envelopes.** `MatchStartedSignal` and `MatchEndedSignal` are emitted via `scope.Emit` from inside their respective handlers; they appear in `HistoryState` as ordinary `HistoryEntry`s.
4. **Lifecycle gate is uniform and framework-owned.** `Dispatch` gates on `Lifecycle` before the runtime's `OnIncomingCommand` runs. `NotStarted` permits only `StartMatchCommand` and `EndMatchCommand`; `Ended` permits nothing.
5. **End-of-match has one transition path, two entry points.** Runtime / effect drives by dispatching `EndMatchCommand` (typically from `Evaluate`); host drives by calling `engine.FinishMatch(outcome)`. Both reach `EndMatchHandler.Handle`. `Dispose` is a third entry point that synthesizes `MatchOutcome.Aborted("Disposed")`.
6. **Queue tail drains naturally on end.** When the lifecycle flips to `Ended` mid-drain, the queue is not pre-cleared. Already-queued commands continue to execute. New dispatches hit the `Ended` gate.
7. **Engine owns runtime instance disposal; host owns scope disposal.** `engine.Dispose` releases the runtime if `IDisposable`. The per-match DI scope is opened and torn down by the host / outer controller.
8. **`FinishMatch` permits `NotStarted → Ended`.** Cancel-before-start is supported. Only `MatchEndedSignal` lands; no start signal ever fired.
9. **`MatchOutcome` is a closed type hierarchy.** Abstract base + sealed subtypes (`Win`, `Loss`, `Draw`, `Aborted`); consumers pattern-match.

## Decisions

*n/a — no elevated ADR.*

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
  → drain ends, OnQueueDrained: runtime publishes initial AvailableActions
  → engine.Lifecycle == Started

host.engine.Dispatch(new MoveCardCommand(top of A → B))
  → Lifecycle gate: Started, normal command → permitted
  → handler.Validate, OnIncomingCommand, drain runs
  → all four piles now match target
  → Evaluate: returns [new EndMatchCommand(new MatchOutcome.Win(player1))]
  → engine enqueues, dequeues
  → pipeline runs EndMatchHandler:
     - ctx.Emit(MatchEndedSignal(Win(player1)))
     - Store.Execute(SetEngineLifecyclePayload(Ended))
  → envelope closes carrying MatchEndedSignal
  → drain continues — queue is empty
  → OnQueueDrained: runtime publishes terminal snapshot or no-ops
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
- **Match-end observation API** — host currently observes match end via `MatchEndedSignal` in `HistoryState` or via state subscription on `EngineState.Lifecycle`. If a more ergonomic surface (e.g. `Task<MatchOutcome> MatchCompletion { get; }`) becomes obviously needed, add it.
- **Multi-winner / team outcomes** — `MatchOutcome.Win` is single-actor. Add `TeamWin` (or similar) when a co-op / team game forces it.
- **`MatchStartedSignal` payload** — currently fieldless; the fact-of-start is its presence in history. If a consumer needs the start time, RNG seed, or actor list embedded in the signal itself (rather than read from state), add fields then.
- **Per-match scope ownership convention** — this Model says the host owns the scope, but doesn't pin a concrete pattern (a `GameSession` controller, a VContainer scope-builder helper, etc.). Pin when a real game integrates with a real scope-bearing controller.

---

*Below: provenance — feature-internal decisions for the match lifecycle. No ADR was opened (feature-internal scope); the design space is captured here.*

## Options considered

### Q1 — Where lifecycle state lives

- **A — On the engine only.** Rejected: state-driven consumers need `IStateScope` access, not an engine reference; breaks the no-engine-reference invariant for the runtime.
- **B — On the engine + mirrored in `EngineState` slice.** **Chosen.** `EngineState.Lifecycle` is the source of truth; `engine.Lifecycle` is a convenience accessor for host code that already holds the engine.

### Q2 — Whether transitions are commands or direct flips

- **A — Direct state flip with synthesized signal envelope.** Rejected: requires special-case envelope construction; signals land outside the normal trigger fold.
- **B — Lifecycle transitions are framework-owned `GameAction`s** (`StartMatchCommand`, `EndMatchCommand`); public `StartMatch` / `FinishMatch` methods are sugar that construct and dispatch. **Chosen.** Signals land in normal envelopes; triggers fire naturally; first snapshot opportunity is free.

### Q3 — `MatchOutcome` shape

- **A — Single record with nullable fields** (`Outcome { Winner?, Loser?, Reason? }`). Rejected: pattern-matching requires nullable-field gymnastics; consumer must guard every field.
- **B — Closed type hierarchy** (abstract base + sealed `Win` / `Loss` / `Draw` / `Aborted` subtypes). **Chosen.** Pattern-matching reads cleanly; each variant carries only its relevant fields.

### Q4 — Cancel-before-start

- **A — Reject `FinishMatch` from `NotStarted`.** Rejected: hosts that need to abort during setup have nowhere to go.
- **B — Permit `NotStarted → Ended` via `EndMatchCommand`.** **Chosen.** Cancel-before-start lands a `MatchEndedSignal` (`Aborted` outcome); no `MatchStartedSignal` ever fired.

### Q5 — Queue tail behavior on lifecycle flip

- **A — Pre-clear the queue on `Ended`.** Rejected: silently drops in-flight commands; opaque to history.
- **B — Drain the tail naturally; new dispatches hit the gate.** **Chosen.** Already-queued commands execute in order; trigger reactions and evaluator follow-ups dispatched after the lifecycle flip reject.

### Q6 — Disposal boundary

- **A — Engine disposes everything (runtime + DI scope).** Rejected: forces engine to know about VContainer; couples engine to scope-management lifecycle.
- **B — Engine disposes runtime; host disposes per-match scope.** **Chosen.** Engine owns the *match*; host owns the *scope* that hosts the match. Engine never sees the scope object.

## Rationale

Q1's split ("source of truth in state, convenience accessor on engine") matches the pattern used everywhere else (snapshot in state, etc.). Q2's lifecycle-as-commands choice cleans up the special-case code path for synthesized envelopes and gives downstream features (triggers, snapshot pipeline) free integration with lifecycle events. Q3's closed type hierarchy makes consumer code readable. Q4's cancel-before-start is mandatory for any host that builds matches lazily (deck choice, lobby, etc.). Q5's tail-drain preserves history fidelity. Q6's engine-runtime / host-scope split keeps each layer narrow.

## Consequences

- **Enables:** state-driven lifecycle observation; `OnAfter<MatchStartedSignal>` / `OnAfter<MatchEndedSignal>` triggers; first snapshot at start; cancel-before-start for lobby/setup flows; pattern-match-friendly outcome consumers; scope teardown decoupled from engine.
- **Constrains:** every public lifecycle method routes through a framework-owned command + handler (two framework command types in the catalog); the host must own the per-match scope and observe match-end before tearing it down.
- **Cascade:** the lifecycle gate is consumed by `engine/01-evaluation-loop` (gate runs before `OnIncomingCommand`); `EngineState.Lifecycle` slice is consumed by handlers (`StartMatchHandler.Validate`, `EndMatchHandler.Validate`); `MatchOutcome` enters game vocabulary as the canonical end-of-match shape.
