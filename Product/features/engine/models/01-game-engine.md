---
type: model
status: settled
references: [features/actions/models/01-command-pipeline.md, features/engine/models/03-game-rules-runtime.md, features/engine/models/07-match-state.md, features/engine/models/08-turn-flow.md, features/state/state.md, decisions/0009-engine-as-fsm.md]
tags: [#engine, #evaluation-loop, #orchestrator, #fsm]
---

# Game Engine — model

## What it is

The engine is the **orchestrator** of the framework's command pipeline: a strict-FIFO queue, a per-step drain loop that calls `pipeline.Execute` then `runtime.Evaluate`, a single `Dispatch` entry point used by every external source (player input, timers, trigger reactions), and a two-layer intake gate. **Session lifecycle** lives as a typed sealed hierarchy on `GameEngine.Session` (`NotStartedSession` / `InMatchSession` / `EndedSession`); **match-state legality** consults `MatchStateSlice.Current.Actions` from the Store. Per [ADR-0009](Product/decisions/0009-engine-as-fsm.md) the engine IS the FSM — `runtime.Evaluate` returns a `MatchState`; the engine detects type-changes and dispatches `TransitionStateCommand`. The engine does not know what any particular action means or what game is being played — that knowledge lives in the runtime ([`03-game-rules-runtime.md`](features/engine/models/03-game-rules-runtime.md)) and in the concrete `MatchState` / `ITurnFlow` / `ISBA` implementations ([`07-match-state.md`](features/engine/models/07-match-state.md), [`08-turn-flow.md`](features/engine/models/08-turn-flow.md), [`09-state-based-actions.md`](features/engine/models/09-state-based-actions.md)).

Per [ADR-0006](Product/decisions/0006-two-pipeline-architecture.md), the command pipeline is **framework-owned** (no game customization on routing); effect-level orchestration is a separate forthcoming surface ([ADR-0011 — *forthcoming*]). The session lifecycle types — `SessionState` hierarchy, `StartMatchCommand` / `EndMatchCommand`, `MatchStartedSignal` / `MatchEndedSignal`, `MatchOutcome` hierarchy — are defined below in §Session lifecycle.

```csharp
public sealed class GameEngine : IDisposable
{
    private readonly Queue<GameAction>             pending = new();
    private readonly ICommandPipeline              pipeline;
    private readonly GameRulesRuntime<TDefinition> runtime;
    private readonly Store                         store;   // framework slices auto-seeded at Build()
    private bool running;

    public SessionState Session { get; private set; } = new NotStartedSession();
    public event Action<SessionState>? SessionChanged;

    public bool StartMatch (out string? reason);                                   // NotStarted → InMatch; dispatches StartMatchCommand
    public bool Dispatch   (GameAction action, out string? reason);                // single intake; session + match-state-actions gated
    public bool FinishMatch(MatchOutcome outcome, out string? reason);             // * → Ended; dispatches EndMatchCommand
    public void Dispose    ();                                                     // ensures FinishMatch(Aborted) if not Ended; disposes runtime if IDisposable

    private void Drain();
}
```

## Contracts

### The three parts

**`ICommandPipeline`** — the framework's executor. Owns two responsibilities: validating commands at intake (resolving the handler from the registry and delegating to `handler.Validate`) and executing them through the full pipeline (before-fold → handler → after-fold → envelope). Does not know about the queue or what happens after.

```csharp
public interface ICommandPipeline
{
    // Called at intake — resolves the handler and delegates to handler.Validate.
    bool Validate(GameCommand command, IStateScope state, out string? reason);

    // Called by the engine loop — runs the full pipeline for one action.
    CommandResult Execute(GameAction action);
}
```

**Rules layer** (`GameRulesDefinition` + `GameRulesRuntime<TDefinition>`) — the game's decision layer. Two cooperating types: pure data + stateful runtime brain. Called after each action resolves to read state and return the next `MatchState`. Full canonical contract in [`03-game-rules-runtime.md`](features/engine/models/03-game-rules-runtime.md).

**`GameEngine`** — the loop itself. Holds the queue, calls the pipeline, calls the runtime, manages the drain cycle, owns the session lifecycle field.

### The engine loop

```csharp
public sealed class GameEngine : IDisposable
{
    private readonly Queue<GameAction>             pending = new();
    private readonly ICommandPipeline              pipeline;
    private readonly GameRulesRuntime<TDefinition> runtime;
    private readonly Store                         store;
    private bool running;
    private bool disposed;

    // Session lifecycle as a typed sealed hierarchy on a field (per ADR-0009 D12).
    // Subscribers use SessionChanged or read Session directly.
    public SessionState Session { get; private set; } = new NotStartedSession();
    public event Action<SessionState>? SessionChanged;

    // Sugar that constructs and dispatches StartMatchCommand. NotStarted → InMatch.
    public bool StartMatch(out string? reason)
        => Dispatch(new StartMatchCommand(), out reason);

    // Sugar that constructs and dispatches EndMatchCommand. * → Ended (idempotent: rejects
    // re-end). Permits NotStarted → Ended for cancel-before-start.
    public bool FinishMatch(MatchOutcome outcome, out string? reason)
        => Dispatch(new EndMatchCommand(outcome), out reason);

    // Single entry point for all sources — player input, timers, trigger reactions.
    // Two-layer intake gate runs first (framework: session + match-state-actions),
    // then pipeline.Validate. Per ADR-0006 the runtime does not gate intake —
    // command-queue routing is framework-owned FIFO.
    public bool Dispatch(GameAction action, out string? reason)
    {
        if (disposed) throw new ObjectDisposedException(nameof(GameEngine));

        // Layer 1 — session gate. See §Intake gates table below.
        if (Session is EndedSession)
        {
            reason = "MatchEnded";
            return false;
        }
        if (Session is NotStartedSession
            && action is not StartMatchCommand
            && action is not EndMatchCommand)
        {
            reason = "MatchNotStarted";
            return false;
        }

        // Layer 2 — match-state-actions gate. Lifecycle commands (Start / End / Transition)
        // bypass; all other commands must appear in MatchState.Current.Actions.
        if (Session is InMatchSession && !IsLifecycleCommand(action))
        {
            var current = store.Get<MatchStateSlice>().Current;
            if (current.Actions.Count > 0 && !current.Actions.Contains(action.GetType()))
            {
                reason = $"NotInMatchStateActions:{current.GetType().Name}";
                return false;
            }
        }

        if (!pipeline.Validate(action, store, out reason))
            return false;

        pending.Enqueue(action);

        if (!running) Drain();

        reason = null;
        return true;
    }

    private void Drain()
    {
        running = true;

        while (pending.Count > 0)
        {
            var action = pending.Dequeue();
            var result = pipeline.Execute(action);

            // Game-specific: given the state after this action, what is the new MatchState?
            var newState = runtime.Evaluate(action, result);
            var current  = store.Get<MatchStateSlice>().Current;
            if (newState.GetType() != current.GetType())
                pending.Enqueue(new TransitionStateCommand(newState) with {
                    OriginCommandId = action.OriginCommandId
                });
        }

        running = false;
    }

    // If Session is not EndedSession, dispatches EndMatchCommand(Aborted("Disposed")) so the
    // end signal lands in history. Then disposes the runtime if IDisposable. The per-match
    // DI scope is opened and torn down by the host — engine never sees the scope object.
    public void Dispose()
    {
        if (disposed) return;
        if (Session is not EndedSession)
            FinishMatch(new MatchOutcome.Aborted("Disposed"), out _);
        if (runtime is IDisposable d) d.Dispose();
        disposed = true;
    }

    private static bool IsLifecycleCommand(GameAction action)
        => action is StartMatchCommand or EndMatchCommand or TransitionStateCommand;
}
```

### Loop semantics

- **Try-get `Dispatch`.** Returns `bool` plus `out string? reason` — symmetric with `pipeline.Validate`. `false` carries a non-null reason; `true` carries `null`.
- **`Dispatch(GameAction)`, not `Dispatch(GameCommand)`.** `GameSignal` is emitted via `scope.Emit` from inside handlers (never dispatched); `CompositeAction` runs inline within a parent handler (never queued). The engine's queue holds only `GameAction`s.
- **Drain is not re-entered.** If a command is dispatched while the loop is already running, it is enqueued and picked up by the current drain cycle.
- **Per-step evaluation.** The loop calls `runtime.Evaluate` after every action, not once at the end. This is what gives the engine per-step FSM awareness.
- **`OriginCommandId` threading.** The engine copies `OriginCommandId` from the just-executed action onto every `TransitionStateCommand` it synthesizes from `Evaluate`. The runtime does not need to set it. The same threading applies to handler-dispatched follow-ups going through `Dispatch`.
- **Two-layer intake gate runs before `pipeline.Validate`.** Layer 1 checks `Session`; Layer 2 checks `MatchStateSlice.Current.Actions`. The full table is in §Intake gates below. Per [ADR-0010 — *forthcoming*] both layers will move to registered framework validators (`SessionGate`, `MatchStateActionGate`); until then they live inline in `Dispatch`.
- **No `OnQueueDrained` callback.** Per ADR-0006, drain-end logic folds into `Evaluate` — the runtime returns a `MatchState`; the engine transitions if the type changed. There is no separate "queue is about to empty" callback.
- **Engine-synthesized transitions enter the queue directly.** `TransitionStateCommand`s the engine appends after `Evaluate` bypass the intake gate (they are framework-internal). External entry points and handler-dispatched commands still go through `Dispatch` (gate + validate + enqueue).

### Entry points

Everything that wants the engine to act submits a `GameAction`. The engine does not know or care which entry point created it.

```csharp
// Player drags a card
public sealed class IntentGateway
{
    private readonly GameEngine engine;

    public void Submit(PlayerIntent request)
    {
        var action = Convert(request); // resolve actor, zone, intent
        if (!engine.Dispatch(action, out var reason))
            ShowToast(reason);
    }
}

// A turn timer fires
public sealed class TurnTimerService
{
    private readonly GameEngine engine;

    // ADR-0005: ActorId is boundary-correct — timer events originate from an external clock with no
    // Actor object reference (type-mismatch boundary per ADR-0005).
    public void OnTimeExpired(ActorId actor)
    {
        engine.Dispatch(new ForceEndTurnCommand(actor), out _);
    }
}

// Trigger reactions — fired from inside an effect via IEffectScope.Dispatch.
// That surface internally calls engine.Dispatch; reactions go through the same path
// as any other entry. See features/triggers/models/02-trigger-pipeline.md.
```

### Intake gates

`Dispatch` consults two gates before delegating to `pipeline.Validate`. Both are framework-owned; per [ADR-0010 — *forthcoming*] they move to registered validators.

| Gate | Source of truth | Rejects |
|---|---|---|
| **Session gate** (Layer 1) | `engine.Session` | `NotStartedSession`: anything except `StartMatchCommand` / `EndMatchCommand`. `EndedSession`: anything. |
| **MatchState actions gate** (Layer 2) | `MatchStateSlice.Current.Actions` | When `Session is InMatchSession`: commands whose `GetType()` is not in `Actions`, unless `Actions` is empty (open state). Lifecycle commands (`StartMatchCommand`, `EndMatchCommand`, `TransitionStateCommand`) bypass. |

Full rules:

| Session | Match state | Command | Result |
|---|---|---|---|
| `NotStartedSession` | (no match yet) | `StartMatchCommand` | Validate runs; handler enters `InMatchSession`, seeds initial `MatchState` |
| `NotStartedSession` | (no match yet) | `EndMatchCommand` | Validate runs; handler flips to `EndedSession` (cancel-before-start) |
| `NotStartedSession` | (no match yet) | Any other | Reject(`"MatchNotStarted"`) before pipeline runs |
| `InMatchSession` | `current.Actions = []` (open) | Any (except `Start`/`End`) | Match-state gate skipped; pipeline runs |
| `InMatchSession` | `current.Actions` non-empty | `EndMatchCommand` | Validate runs; handler flips to `EndedSession` |
| `InMatchSession` | `current.Actions` contains `T` | `T` | Pipeline runs |
| `InMatchSession` | `current.Actions` does NOT contain `T` | `T` | Reject(`"NotInMatchStateActions:..."`) before pipeline runs |
| `EndedSession` | (match over) | Anything | Reject(`"MatchEnded"`) before pipeline runs |

**Queue tail drains naturally on end.** When `EndMatchCommand`'s handler flips `Session` to `EndedSession` mid-drain, the queue tail continues to execute — already-queued commands aren't pre-cleared. New `Dispatch` calls during the same drain hit the `EndedSession` gate and reject. Engine-synthesized `TransitionStateCommand` is bypassed from the gate (framework-internal), so the FSM can still resolve final transitions during tail drain.

### Session lifecycle

The framework-owned types that drive the session gate. Per [ADR-0009 D12](Product/decisions/0009-engine-as-fsm.md), session lifecycle is a typed sealed hierarchy on a `GameEngine` field — not a Store slice. The match-FSM lives in the Store (`MatchStateSlice`); session lifecycle is metadata *about* the engine itself.

```csharp
public abstract record SessionState;
public sealed   record NotStartedSession                       : SessionState;
public sealed   record InMatchSession                          : SessionState;
public sealed   record EndedSession(MatchOutcome Outcome)      : SessionState;
// Future additions for networking: ConnectingSession, ReconnectingSession,
// ShowingResultsSession, LobbySession, AbortedSession(Reason), ...

public sealed record StartMatchCommand            : GameAction;
public sealed record StartMatchResult             : CommandResult;

public sealed record EndMatchCommand(MatchOutcome Outcome) : GameAction;
public sealed record EndMatchResult (MatchOutcome Outcome) : CommandResult;

public sealed record MatchStartedSignal                            : GameSignal;
public sealed record MatchEndedSignal  (MatchOutcome Outcome)      : GameSignal;

public abstract record MatchOutcome
{
    public sealed record Win    (Actor   Winner, string? Reason = null) : MatchOutcome;
    public sealed record Loss   (Actor   Loser,  string? Reason = null) : MatchOutcome;
    public sealed record Draw   (string? Reason = null)                 : MatchOutcome;
    public sealed record Aborted(string  Reason)                        : MatchOutcome;
}
```

`engine.StartMatch()` is `Dispatch(new StartMatchCommand())`; `engine.FinishMatch(outcome)` is `Dispatch(new EndMatchCommand(outcome))`. The framework-internal `StartMatchHandler` and `EndMatchHandler` mutate `engine.Session` directly (the field is the source of truth, not a Store slice) and emit the corresponding signal via `ctx.Emit(...)`. Three benefits fall out of running transitions through the pipeline rather than via direct flips: signals land in normal envelopes; triggers fire naturally (`OnAfter<MatchStartedSignal>` / `OnAfter<MatchEndedSignal>`); the first state's `EnterCommands` fire after `StartMatchCommand`'s drain when `Evaluate` returns the initial `MatchState` (typically `ITurnFlow.Initial`).

**Mutating `engine.Session`.** The engine exposes an internal method (or the handler is wired with a reference to the engine instance) — implementation detail; the principle is that the field is engine-owned. `SessionChanged` fires on every transition.

**Hybrid future flagged.** Long-term shape likely **B+C hybrid** per ADR-0009 — host code (Unity scene, dedicated server) owns session orchestration (lobby, matchmaking, reconnect logic); engine exposes hooks (`Session` property, `SessionChanged` event, `StartMatch` / `FinishMatch` / `Dispose`). Framework provides typed states and hooks; host wires the orchestration appropriate to its environment.

`MatchOutcome` is a closed type hierarchy — abstract base + sealed subtypes. Consumers pattern-match. Multi-winner scenarios are a future extension — add `TeamWin(IReadOnlyList<Actor> Winners, ...)` when a real game forces it. `Reason` is free-form on every variant for now; structuring (enum, localization keys) is deferred.

**End-of-match has one transition path, multiple entry points.** Runtime/SBA drives by returning a `GameOverState`-shaped `MatchState`, whose `EnterCommands` typically include `EndMatchCommand(outcome)`. Host drives via `engine.FinishMatch(outcome)`. `engine.Dispose` synthesizes `MatchOutcome.Aborted("Disposed")` if not already ended. All paths reach `EndMatchHandler.Handle`.

### The `EngineState` slice

The engine surfaces some of its internal state as a framework-owned `BaseState` slice — for state-driven consumers reading via `IStateScope` (debug overlays, replay tooling, etc.). Per ADR-0009 the `Lifecycle` field is **removed**; session lifecycle lives on `engine.Session`, not on a slice.

```csharp
public sealed record EngineState(
    bool         IsRunning,
    GameCommand? CurrentCommand) : BaseState;
```

- **`IsRunning`** — drain flag. Updated on `Drain` start/stop. Consumers reading state mid-drain (rare) can detect re-entry.
- **`CurrentCommand`** — the action currently executing through `pipeline.Execute`, or `null` between commands. Updated before/after each `pipeline.Execute` call.
- **Seeded.** `EngineState(IsRunning: false, CurrentCommand: null)` at `Build()` (framework auto-seeds; never declared in `MatchSetup.InitialSlices`).
- **Written by.** Engine internals only. Not writable by game authors.

The slice is a framework-owned tier slice — see [`engine.md` §Framework-owned slices](features/engine/engine.md) for the catalog and [`state/state.md` §Slice tiers](features/state/state.md) for the framework-vs-game-authored convention. The session lifecycle's source of truth is `engine.Session`, not this slice.

### State pattern — `GameState` as Scaffold slices

`GameState` is **not a single record**. State lives as multiple typed `BaseState` slices in a Scaffold `Store`. There is no `state.Piles`, `state.LastMove`, etc. Reads always go through `IStateScope.Get<TState>()`; writes always go through `Store.Execute(payload)` → registered `Mutator<TState, TPayload>`. The runtime reads through the injected `IStateScope`; it never mutates state directly — the only "state write" the runtime triggers is by returning a different-typed `MatchState` from `Evaluate`, which the engine routes through `TransitionStateCommand`.

The full slice catalog (framework-owned vs game-authored, the `ISliceProvider` mechanism, subscriptions, atomic batches) is in [`state/state.md`](features/state/state.md) and [`engine.md` §Framework-owned slices](features/engine/engine.md).

### Reading state, returning a MatchState

The runtime's lens is the **current game state**, not which specific action just ran. If the runtime matched on action types ("after `PlayCardCommand` → enter `ExecuteCardState`"), it would miss any other way a card could end up in the same situation (an opponent's trigger, a copy effect, a token). State-based evaluation handles all sources the same way. The methods relevant to the loop:

```csharp
public abstract class GameRulesRuntime<TDefinition>
    where TDefinition : GameRulesDefinition
{
    // Game-policy filter on which registered triggers are eligible right now.
    public virtual IReadOnlyList<TriggerMatch> FilterTriggers(
        IReadOnlyList<TriggerMatch> candidates,
        GameCommand                 command,
        TriggerPhase                phase)
        => candidates;

    // Called after each GameAction resolves.
    // Returns the new MatchState (may be the current one — engine no-ops on type-equality).
    // Default body: SBAs preempt; otherwise current.ShouldExit + Flow.NextAfter.
    public virtual MatchState Evaluate(
        GameAction    justExecuted,
        CommandResult result) { /* see engine/03 */ }
}
```

State is read through the injected `IStateScope` (`State.Get<TState>()`). The runtime observes state and returns the new `MatchState`; it never mutates state directly. See [`03-game-rules-runtime.md`](features/engine/models/03-game-rules-runtime.md) for the full contract.

### Drain end — detected from state, not a callback

There is no `OnQueueDrained` callback (deleted per ADR-0006). The runtime's `Evaluate` is called after every action; the engine decides whether the queue is "done" structurally by checking whether `pending.Count == 0` after handling the latest action and its evaluator follow-ups. State-based follow-ups now flow through:

- **`MatchState.EnterCommands`** — when a transition fires, the new state's entry commands enqueue automatically via `TransitionStateHandler`.
- **`ISBA.Check` returning a different state** — preempts Flow when a cross-cutting condition is met.
- **`MatchState.ShouldExit` + `ITurnFlow.NextAfter`** — the runtime's default `Evaluate` body composes these.

Custom orchestration (e.g., MTG-style stack resolution timing) lives in `Evaluate` overrides — see [`engine/03`](features/engine/models/03-game-rules-runtime.md) and [`07-match-state.md`](features/engine/models/07-match-state.md). The framework no longer carries an `IAvailableActions` service or an `AvailableActionsState` slice — `MatchState.Actions: IReadOnlyList<Type>` replaces both. The wire-form `ActionTemplate` and presentation-side projection are reworked in [ADR-0012 — *forthcoming*].

### Relationship to the command pipeline

The pipeline described in [`actions/01-command-pipeline.md`](features/actions/models/01-command-pipeline.md) does not change. The engine wraps it:

| Concern | Where it lives |
|---|---|
| Execute one action (before triggers, handler, after triggers, history) | `ICommandPipeline` (framework) |
| Decide what `MatchState` runs next | `GameRulesRuntime<T>` (game-specific, via Flow / SBAs / ShouldExit) |
| Drive the loop between them | `GameEngine` (framework) |
| Submit actions from outside | Entry points (`IntentGateway`, `TurnTimerService`, etc.) |
| Trigger fold ordering | `ITriggerPipeline` (swappable; default FIFO) |
| Session lifecycle | `GameEngine.Session` (framework field; not a slice) |
| Match-state legality (prophylactic) | `MatchStateSlice.Current.Actions` (Store slice; framework + game authored states) |

The `GameCommandDispatcher` from earlier sketches becomes `ICommandPipeline.Execute` — it handles one action and returns a result. Reactions are dispatched through `IEffectScope.Dispatch` from inside trigger executes; that path enters the engine through the same `Dispatch` method as external entry points.

## Invariants

1. **Strict-FIFO queue, framework-owned.** No runtime customization on routing; per ADR-0006 (cross-Model invariant pinned by Feature root).
2. **`Dispatch(GameAction)` only.** `GameSignal` emits via `scope.Emit`; `CompositeAction` runs inline. The queue holds `GameAction`s.
3. **Per-step `Evaluate`.** Called after every action's `pipeline.Execute` returns, not once at end of drain. `Evaluate` returns a `MatchState`; the engine compares its type to `MatchStateSlice.Current.GetType()` and synthesizes `TransitionStateCommand` on a type change.
4. **`OriginCommandId` threading is mechanical.** The engine copies `OriginCommandId` from the just-executed action onto every synthesized `TransitionStateCommand`. The runtime does not set it.
5. **Engine-synthesized transitions bypass the intake gate.** `TransitionStateCommand`s the engine appends after `Evaluate` are framework-internal and enter the queue directly. External dispatches and trigger reactions still go through `Dispatch` (gate + validate + enqueue).
6. **Two-layer intake gate runs before `pipeline.Validate`.** Framework consults `engine.Session` and `MatchStateSlice.Current.Actions`. Per ADR-0006 there is no runtime-side intake hook. Per [ADR-0010 — *forthcoming*] the gates move to registered framework validators.
7. **`engine.StartMatch` / `engine.FinishMatch` are sugar.** They construct and dispatch the framework-owned lifecycle commands through `Dispatch`. No direct state writes from the public engine surface; lifecycle transitions flow through the normal pipeline.
8. **`engine.Dispose` ensures end-of-match.** If `Session is not EndedSession`, `Dispose` calls `FinishMatch(Aborted("Disposed"))` so `MatchEndedSignal` lands in history. Then disposes the runtime if `IDisposable`. The per-match DI scope is opened and torn down by the host — engine never sees the scope object.
9. **Drain is not re-entered.** If a command is dispatched while drain is running, it is enqueued and picked up by the current cycle. Concurrency / async re-entry is an open follow-up.
10. **`GameState` is multiple slices.** No single record. Reads via `IStateScope.Get<TState>()`; writes via `Store.Execute(payload)` → `Mutator<TState, TPayload>`. The runtime never writes directly. Match-state writes happen only through `TransitionStateCommand` → `SetMatchStatePayload` (framework-owned mutator).
11. **Session lifecycle lives on `GameEngine.Session`, not in a slice.** Per ADR-0009 D12. Match-FSM subscribers use `store.Subscribe<MatchStateSlice>`; session subscribers use the `SessionChanged` event or read `engine.Session`.

## Decisions

*Provenance for §Session lifecycle + §Intake gates: [ADR-0009](Product/decisions/0009-engine-as-fsm.md) — D1, D12. The state-pattern decision (Q1 below) is single-feature scope; folded as Q1 below.*

## Examples

### Pile Counter — single-state FSM with SBA-driven game-over

```csharp
// MatchState shape (Pile Counter has just two): see engine/07.
public sealed record PilePlayingState                       : MatchState { /* Actions = [MoveCardCommand] */ }
public sealed record PileGameOverState(MatchOutcome Outcome) : MatchState { /* Actions = [] */ }

// Runtime composes a single SBA + a trivial Flow.
public sealed class PileCounterRulesRuntime
    : GameRulesRuntime<PileCounterRulesDefinition>
{
    protected override ITurnFlow             Flow              { get; }
    protected override IReadOnlyList<ISBA>   StateBasedActions { get; }

    public PileCounterRulesRuntime(
        PileCounterRulesDefinition definition,
        IStateScope                state,
        PileCounterTurnFlow        flow,
        PileCounterWinSBA          winCheck)
        : base(definition, state)
    {
        Flow              = flow;
        StateBasedActions = [winCheck];
    }

    // Uses the runtime base's default Evaluate body — SBAs preempt; otherwise
    // current.ShouldExit + Flow.NextAfter. PilePlayingState never exits via
    // ShouldExit, so the only path out is the SBA returning PileGameOverState.
}
```

Traced flow:

```text
engine.StartMatch()
  → Dispatch(StartMatchCommand)
    → StartMatchHandler: engine.Session = InMatchSession; emit MatchStartedSignal
    → drain continues
  → Evaluate(StartMatchCommand, result):
    → WinConditionSBA.Check: piles incomplete → null
    → current = PilePlayingState; ShouldExit = false; return current
  → no transition (same type)

Player drags top card of pile A to pile B
  → IntentGateway.Submit(PlayerIntent)
  → engine.Dispatch(MoveCardCommand)
    → Session gate: InMatchSession; pass
    → MatchState actions gate: PilePlayingState.Actions = [MoveCardCommand]; pass
    → pipeline.Validate: handler-side check passes
    → enqueue + drain
  → pipeline.Execute(MoveCardCommand):
    → before-fold; handler mutates ZoneContentsState + PileState + LastMoveState; after-fold
  → Evaluate(MoveCardCommand, result):
    → WinConditionSBA.Check: piles[1] complete, others incomplete → null
    → current = PilePlayingState; ShouldExit = false; return current
  → no transition

[... many moves later ...]

After final move
  → Evaluate(MoveCardCommand, result):
    → WinConditionSBA.Check: all piles match target → returns PileGameOverState(MatchOutcome.Win(player))
  → engine sees type change → synthesizes TransitionStateCommand(PileGameOverState)
  → Drain dequeues TransitionStateCommand:
    → handler writes MatchStateSlice via SetMatchStatePayload
    → handler enqueues PileGameOverState.EnterCommands (typically EndMatchCommand)
  → Drain dequeues EndMatchCommand:
    → EndMatchHandler: engine.Session = EndedSession(outcome); emit MatchEndedSignal
  → Drain returns; queue empty.
```

The engine drove three distinct transitions through the pipeline: `StartMatchCommand`, `TransitionStateCommand`, `EndMatchCommand`. Each carries a normal `HistoryEnvelope`; replay reconstructs the exact same sequence.

### MTG-shaped Flow + SBA composition

```csharp
public sealed class StandardMagicRulesRuntime : GameRulesRuntime<StandardMagicRulesDefinition>
{
    protected override ITurnFlow             Flow              { get; }
    protected override IReadOnlyList<ISBA>   StateBasedActions { get; }

    public StandardMagicRulesRuntime(
        StandardMagicRulesDefinition definition,
        IStateScope                  state,
        StandardMtgTurnFlow          flow,
        LethalDamageSBA              lethal,
        LegendRuleSBA                legend,
        EmptyLibrarySBA              drawFromEmpty)
        : base(definition, state)
    {
        Flow              = flow;
        StateBasedActions = [lethal, legend, drawFromEmpty];
    }
}
```

The default `Evaluate` body runs the three SBAs in order; if all return null, it consults `current.ShouldExit` + `Flow.NextAfter`. Game-specific custom orchestration (stack-resolution timing for instant-speed cards) lives in an override of `Evaluate` — see [`engine/03`](features/engine/models/03-game-rules-runtime.md).

## Open follow-ups

- **Effect pipeline shape.** The substantive open design surface — how the runtime expresses effect-level orchestration (when an effect starts, whether it nests, batches, or stacks). Per ADR-0006, this is a separate concept from command-queue routing. Tracked as `oq-effect-pipeline`; designed in [ADR-0011 — *forthcoming*].
- **Concurrency and re-entry.** The `running` guard serializes dispatch. Networking and async input will require a more explicit contract here.
- **Validator pipeline migration.** Per [ADR-0010 — *forthcoming*] the two-layer intake gate moves from inline `Dispatch` code to registered framework validators (`SessionGate`, `MatchStateActionGate`). Until then the inline gate is the transitional shape.
- **Hybrid session lifecycle.** Per ADR-0009 D12, the long-term shape splits session orchestration between host and engine. The `Session` property + `SessionChanged` event are the framework hooks; how host code composes lobby / matchmaking / reconnect against them is host-shaped, not framework-shaped.
- **Runtime composition.** A single `GameRulesRuntime<T>` is shown. Composing smaller rule modules is `oq-gamerules-composition`.

---

*Below: provenance — how the state pattern was settled. Single-feature scope (no project-wide ADR applies; folded into Model provenance per §4.5 on 2026-05-08). Original ADR-0005 landing 2026-04-29.*

## Options considered

### Q1 — State pattern

The framework needs a state model that supports replay, per-player views, deterministic networking, and zero state held by the rule. We had Scaffold States available with a redux-like immutable model.

- **A — Mutable POCO state mutated in handlers.** Rejected — breaks replay and forces deep cloning for views.
- **B — Custom event-sourced state.** Rejected — duplicates Scaffold infrastructure for no incremental benefit.
- **C — Scaffold States: `GameState` immutable, redux-like; mutators react to commands. `MatchStateSlice` joins per ADR-0009 as a framework-owned slice wrapping the polymorphic match state.** **Chosen.**

## Rationale

`GameState` immutable + redux-like via Scaffold gives us four wins simultaneously: deterministic replay, snapshot/restore, per-player views, and a clean separation between state (Store) and decisions (runtime). The Store is canonical; entities are accessed via Entities-shaped handles through `com.scaffold.entities.states`. Match-state writes go through the same internal-mutator pattern (`SetMatchStatePayload` → `SetMatchStateMutator`) as every other slice — no special-case write path. Custom event sourcing would duplicate Scaffold infrastructure for no incremental benefit; mutable POCOs would force deep cloning for per-player views and break replay.

## Consequences

- **Enables** deterministic replay; snapshot/restore (per-entity variables included via the bridge package); per-player views (read-only Scope handles); the clean two-layer split between rule (decisions) and state (slices); the FSM shape per ADR-0009.
- **Constrains** every state change to be expressible as a mutator over a payload — ad-hoc mutation isn't possible; the runtime never writes state directly (its only "write" is returning a different-typed `MatchState`, which the engine routes through `TransitionStateCommand`). Reverts/replaces earlier mutable-POCO sketches in pre-migration drafts.
