---
type: model
status: settled
references: [features/engine/models/01-evaluation-loop.md, features/engine/models/02-gamerules-contract.md, features/engine/models/04-gamestate-slices.md, features/engine/models/05-game-factory.md]
tags: [#engine, #lifecycle, #match]
---

# Match Lifecycle

Locks how a `GameEngine` transitions from constructed → running → ended: where the lifecycle API lives, what `StartMatch` / `FinishMatch` actually do, how end-of-match is signalled, what disposal covers, and what gets gated where. Resolves `oq-engine-match-lifecycle`.

Builds on:

- [`engine/01-evaluation-loop.md`](features/engine/models/01-evaluation-loop.md) — drain loop, dispatch entry
- [`engine/02-gamerules-contract.md`](features/engine/models/02-gamerules-contract.md) — runtime contract, `OnIncomingCommand` flow gate
- [`engine/04-gamestate-slices.md`](features/engine/models/04-gamestate-slices.md) — `EngineState`, `HistoryState`
- [`engine/05-game-factory.md`](features/engine/models/05-game-factory.md) — match construction, scope ownership

---

## 1. Scope

This Model owns the **match** lifecycle — one `GameEngine` instance, one match, the transitions between *not yet started*, *running*, and *ended*. It does **not** own broader session lifecycle: pause / resume / concede / disconnect / reconnect / multi-match orchestration are higher-layer concerns that wrap the engine externally and stay out of scope here. A future `IGameSession` (or app-level controller) composes `GameEngine` for those needs.

The per-match DI scope (the VContainer scope in which the engine and its services were resolved) is also **not** owned by the engine — see §6. The engine owns the *match*; the host owns the *scope* that hosts the match.

---

## 2. Lifecycle States

```csharp
public enum EngineLifecycle
{
    NotStarted,   // Build returned an engine; StartMatch not yet called
    Started,      // StartMatch ran; engine accepting Dispatch
    Ended,        // FinishMatch ran; engine no longer accepting Dispatch
}
```

Three states. No intermediate `Starting` / `Ending` — transitions are atomic from the public surface. Internal flags like "drain in progress" live separately on `EngineState` (see [`engine/04 §3.2`](features/engine/models/04-gamestate-slices.md)) and don't conflate with lifecycle.

The state is mirrored on `EngineState.Lifecycle` so any consumer reading state via `IStateScope` sees it without holding an engine reference. The `EngineState` slice is the source of truth for state-driven consumers; `engine.Lifecycle` is a convenience accessor for host code that already holds the engine.

---

## 3. Engine Surface

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

`StartMatch` and `FinishMatch` return `IntakeResult` (the same shape every dispatch produces) so the host sees a uniform success/reject surface. Calling `StartMatch` from a non-`NotStarted` state, or `FinishMatch` from `Ended`, surfaces as a normal `IntakeResult.Reject` rather than an exception — the public API treats lifecycle errors the same way it treats any other rejected command.

---

## 4. Transitions Are Commands (β Mechanism)

Every lifecycle transition flows through the normal pipeline as a framework-owned `GameAction`. The public methods are sugar that construct and dispatch.

### 4.1 `StartMatchCommand`

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

### 4.2 `EndMatchCommand`

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

### 4.3 Why commands rather than direct flips

Three benefits fall out of running lifecycle transitions through the pipeline:

- **Signals land in normal envelopes.** No "synthesized envelope" code path; `MatchStartedSignal` and `MatchEndedSignal` are ordinary `HistoryEntry`s with full envelope context (cause, chain, actor).
- **Triggers fire naturally.** `OnAfter<MatchStartedSignal>` / `OnAfter<MatchEndedSignal>` reach the trigger fold via the same machinery as any other command; cards or rules wanting to react to lifecycle do so without new mechanism.
- **First snapshot opportunity is free.** `StartMatchCommand`'s drain runs `OnQueueDrained` after the handler completes — the runtime publishes its initial `AvailableActions` snapshot through the same hook it uses every other turn.

The cost is two framework-owned commands and handlers, registered by the framework's `EngineInstaller`.

---

## 5. `MatchOutcome`

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

---

## 6. Disposal Boundary

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

---

## 7. Intake Gates

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

Once `Lifecycle` flips to `Ended` mid-drain (via `EndMatchCommand`'s handler), the queue tail continues to drain naturally (per Q5 verdict). Already-queued commands aren't pre-cleared; they execute in order, each one passing through `OnIncomingCommand` at *intake time* (which already happened) but with `Lifecycle == Ended` for any *new* dispatches. If a tail command tries to dispatch follow-ups during the drain, those new dispatches hit the `Ended` gate and reject. Trigger reactions and evaluator follow-ups dispatched *during* the same drain that ended the match are subject to the same gate — they reject if dispatched after the lifecycle flip.

---

## 8. Traced Flow

### 8.1 Pile Counter — start, play, win

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

### 8.2 Host abort

```
host.engine.FinishMatch(new MatchOutcome.Aborted("Window closed"))
  → Dispatch(new EndMatchCommand(...))
  → Lifecycle gate: Started, EndMatchCommand → permitted
  → EndMatchHandler.Handle: signal + state flip
  → Lifecycle == Ended
  → host disposes scope
```

### 8.3 Cancel before start

```
host.engine.FinishMatch(new MatchOutcome.Aborted("Player backed out"))
  → Dispatch(new EndMatchCommand(...))
  → Lifecycle gate: NotStarted, EndMatchCommand → permitted
  → EndMatchHandler.Validate: NotStarted is allowed → ok
  → EndMatchHandler.Handle: signal + state flip
  → Lifecycle: NotStarted → Ended (no MatchStartedSignal ever fired)
```

### 8.4 Dispose without finish

```
host.engine.Dispose()
  → Lifecycle != Ended → engine.FinishMatch(Aborted("Disposed"))
  → EndMatchCommand runs through pipeline, MatchEndedSignal lands
  → runtime: if IDisposable, dispose
  → engine marks itself disposed
```

---

## 9. Invariants

1. **Three lifecycle states.** `NotStarted`, `Started`, `Ended`. No intermediate states on the public enum; drain-in-progress lives on `EngineState` separately.
2. **Lifecycle transitions are commands.** Every `NotStarted → Started` and `* → Ended` transition flows through the normal pipeline as `StartMatchCommand` or `EndMatchCommand`. No direct state writes from the public methods; `engine.StartMatch` / `engine.FinishMatch` are sugar.
3. **Signals land in normal envelopes.** `MatchStartedSignal` and `MatchEndedSignal` are emitted via `scope.Emit` from inside their respective handlers; they appear in `HistoryState` as ordinary `HistoryEntry`s.
4. **Lifecycle gate is uniform and framework-owned.** `Dispatch` gates on `Lifecycle` before the runtime's `OnIncomingCommand` runs. `NotStarted` permits only `StartMatchCommand` and `EndMatchCommand`; `Ended` permits nothing.
5. **End-of-match has one transition path, two entry points.** Runtime / effect drives by dispatching `EndMatchCommand` (typically from `Evaluate`); host drives by calling `engine.FinishMatch(outcome)`. Both reach `EndMatchHandler.Handle`. `Dispose` is a third entry point that synthesizes `MatchOutcome.Aborted("Disposed")`.
6. **Queue tail drains naturally on end.** When the lifecycle flips to `Ended` mid-drain, the queue is not pre-cleared. Already-queued commands continue to execute. New dispatches hit the `Ended` gate.
7. **Engine owns runtime instance disposal; host owns scope disposal.** `engine.Dispose` releases the runtime if `IDisposable`. The per-match DI scope is opened and torn down by the host / outer controller — engine never sees the scope object.
8. **`FinishMatch` permits `NotStarted → Ended`.** Cancel-before-start is supported. Only `MatchEndedSignal` lands; no start signal ever fired.
9. **`MatchOutcome` is a closed type hierarchy.** Abstract base + sealed subtypes (`Win`, `Loss`, `Draw`, `Aborted`); consumers pattern-match. No nullable-field tuple shape.

---

## 10. Open Follow-Ups

- **Async-effect cancellation on match end** — when `EndMatchCommand` resolves while an effect is mid-`await` (target picker, choose-one prompt), the awaiting effect is still alive. Whether the engine threads a `CancellationToken` through `IHandlerContext` / `IEffectScope` is an effect-flow concern, not a lifecycle concern. Tracked separately under a new OPEN-ITEMS entry.
- **Match-end observation API** — host currently observes match end via `MatchEndedSignal` in `HistoryState` or via state subscription on `EngineState.Lifecycle`. If a more ergonomic surface (e.g. `Task<MatchOutcome> MatchCompletion { get; }`) becomes obviously needed, add it. Defer until a real consumer surfaces.
- **Multi-winner / team outcomes** — `MatchOutcome.Win` is single-actor. Add `TeamWin` (or similar) when a co-op / team game forces it.
- **`MatchStartedSignal` payload** — currently fieldless; the fact-of-start is its presence in history. If a consumer needs the start time, RNG seed, or actor list embedded in the signal itself (rather than read from state), add fields then.
- **Per-match scope ownership convention** — this Model says the host owns the scope, but doesn't pin a concrete pattern (a `GameSession` controller, a VContainer scope-builder helper, etc.). Pin when a real game integrates with a real scope-bearing controller.

---

## 11. Related

- [`engine/01-evaluation-loop.md`](features/engine/models/01-evaluation-loop.md) — drain loop, dispatch entry, lifecycle gate
- [`engine/02-gamerules-contract.md`](features/engine/models/02-gamerules-contract.md) — runtime contract, `OnIncomingCommand`, `Evaluate`, `OnQueueDrained`
- [`engine/04-gamestate-slices.md`](features/engine/models/04-gamestate-slices.md) — `EngineState.Lifecycle`, `HistoryState`
- [`engine/05-game-factory.md`](features/engine/models/05-game-factory.md) — match construction, scope context
- [`support/samples/pile-counter.md`](support/samples/pile-counter.md) §9.6 — runtime dispatching `EndMatchCommand` from `Evaluate`
- [`ledgers/GLOSSARY.md`](ledgers/GLOSSARY.md) — `EngineLifecycle`, `MatchOutcome`, `MatchStartedSignal`, `MatchEndedSignal`, `EndMatchCommand`, `StartMatchCommand`
- [`ledgers/OPEN-ITEMS.md`](ledgers/OPEN-ITEMS.md) — async-effect cancellation follow-up
