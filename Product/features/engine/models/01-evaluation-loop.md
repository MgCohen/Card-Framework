---
type: model
status: settled
references: [features/actions/models/01-command-pipeline.md, features/engine/models/02-gamerules-contract.md, features/engine/models/06-match-lifecycle.md]
tags: [#engine, #evaluation-loop, #orchestrator]
---

# Evaluation Loop — model

## What it is

The engine is the **orchestrator** of the framework's command pipeline: a strict-FIFO queue, a per-step drain loop that calls `pipeline.Execute` then `runtime.Evaluate`, a single `Dispatch` entry point used by every external source (player input, timers, trigger reactions), and a uniform lifecycle gate (`NotStarted` / `Started` / `Ended`) with public sugar (`StartMatch` / `FinishMatch`) that constructs and dispatches the framework-owned lifecycle commands. It does not know what any particular action means or what game is being played — that knowledge lives in the runtime ([`02-gamerules-contract.md`](features/engine/models/02-gamerules-contract.md)). Per [ADR-0006](Product/decisions/0006-two-pipeline-architecture.md), the command pipeline is **framework-owned** (no game customization on routing); effect-level orchestration is a separate forthcoming surface (`oq-effect-pipeline`). The lifecycle types — `EngineLifecycle` enum, `StartMatchCommand` / `EndMatchCommand`, `MatchStartedSignal` / `MatchEndedSignal`, `MatchOutcome` hierarchy — are defined in [`06-match-lifecycle.md`](features/engine/models/06-match-lifecycle.md).

```csharp
public sealed class GameEngine : IDisposable
{
    private readonly Queue<GameAction>             pending = new();
    private readonly ICommandPipeline              pipeline;
    private readonly GameRulesRuntime<TDefinition> runtime;
    private readonly Store                         store;   // framework slices auto-seeded at Build()
    private bool running;

    public EngineLifecycle Lifecycle { get; }                                      // mirrored on EngineState.Lifecycle

    public bool StartMatch (out string? reason);                                   // NotStarted → Started; dispatches StartMatchCommand
    public bool Dispatch   (GameAction action, out string? reason);                // single intake; lifecycle-gated
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

**Rules layer** (`GameRulesDefinition` + `GameRulesRuntime<TDefinition>`) — the game's decision layer. Two cooperating types: pure data + stateful runtime brain. Called after each action resolves to read state and return follow-up actions. Full canonical contract in [`02-gamerules-contract.md`](features/engine/models/02-gamerules-contract.md).

**`GameEngine`** — the loop itself. Holds the queue, calls the pipeline, calls the runtime, manages the drain cycle.

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

    // Convenience accessor for hosts that hold the engine; EngineState.Lifecycle is the
    // source of truth for state-driven consumers reading via IStateScope.
    public EngineLifecycle Lifecycle => store.Get<EngineState>().Lifecycle;

    // Sugar that constructs and dispatches StartMatchCommand. NotStarted → Started.
    public bool StartMatch(out string? reason)
        => Dispatch(new StartMatchCommand(), out reason);

    // Sugar that constructs and dispatches EndMatchCommand. * → Ended (idempotent: rejects
    // re-end). Permits NotStarted → Ended for cancel-before-start.
    public bool FinishMatch(MatchOutcome outcome, out string? reason)
        => Dispatch(new EndMatchCommand(outcome), out reason);

    // Single entry point for all sources — player input, timers, trigger reactions.
    // Lifecycle gate runs first (framework), then pipeline.Validate. Per ADR-0006 the
    // runtime no longer gates intake — command-queue routing is framework-owned FIFO.
    public bool Dispatch(GameAction action, out string? reason)
    {
        if (disposed) throw new ObjectDisposedException(nameof(GameEngine));

        // Lifecycle gate — see §Lifecycle gate below for the full table.
        var lifecycle = Lifecycle;
        if (lifecycle == EngineLifecycle.Ended)
        {
            reason = "MatchEnded";
            return false;
        }
        if (lifecycle == EngineLifecycle.NotStarted
            && action is not StartMatchCommand
            && action is not EndMatchCommand)
        {
            reason = "MatchNotStarted";
            return false;
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

            // Game-specific: given the state after this action, what actions run next?
            var followUps = runtime.Evaluate(action, result);
            foreach (var next in followUps)
                pending.Enqueue(next with { OriginCommandId = action.OriginCommandId });
        }

        running = false;
    }

    // If Lifecycle != Ended, dispatches EndMatchCommand(Aborted("Disposed")) so the end
    // signal lands in history. Then disposes the runtime if IDisposable. The per-match
    // DI scope is opened and torn down by the host — engine never sees the scope object.
    public void Dispose()
    {
        if (disposed) return;
        if (Lifecycle != EngineLifecycle.Ended)
            FinishMatch(new MatchOutcome.Aborted("Disposed"), out _);
        if (runtime is IDisposable d) d.Dispose();
        disposed = true;
    }
}
```

### Loop semantics

- **Try-get `Dispatch`.** Returns `bool` plus `out string? reason` — symmetric with `pipeline.Validate`. `false` carries a non-null reason; `true` carries `null`.
- **`Dispatch(GameAction)`, not `Dispatch(GameCommand)`.** `GameSignal` is emitted via `scope.Emit` from inside handlers (never dispatched); `CompositeAction` runs inline within a parent handler (never queued). The engine's queue holds only `GameAction`s.
- **Drain is not re-entered.** If a command is dispatched while the loop is already running, it is enqueued and picked up by the current drain cycle.
- **Per-step evaluation.** The loop calls `runtime.Evaluate` after every action, not once at the end. This is what gives the engine per-step state-based awareness.
- **`OriginCommandId` threading.** The engine copies `OriginCommandId` from the just-executed action onto every evaluator follow-up. `PlayCardCommand` and any chain members share the same `OriginCommandId`. The engine does this mechanically — the runtime does not need to set it.
- **Lifecycle gate.** Framework rejects pre-`StartMatch` and post-`FinishMatch` dispatches uniformly. Full table in §Lifecycle gate below. Lifecycle types (enum, lifecycle commands, signals, `MatchOutcome`) are defined in [`06-match-lifecycle.md`](features/engine/models/06-match-lifecycle.md).
- **No `OnQueueDrained` callback.** Per ADR-0006, drain-end logic folds into `Evaluate` — the runtime detects "queue is about to empty" by observing state and its own follow-up emptiness, then publishes snapshots / dispatches phase advances from there.
- **Evaluator follow-ups bypass `Validate`.** Actions returned from `Evaluate` are produced by runtime logic and enter the queue directly. External entry points and trigger reactions still go through `Dispatch` (validate + enqueue).
- **Queue tail drains naturally on end.** When `EndMatchCommand`'s handler flips `Lifecycle` to `Ended` mid-drain, already-queued commands continue to execute in order — the queue is not pre-cleared. New `Dispatch` calls during the same drain hit the `Ended` gate and reject. Evaluator follow-ups bypass the gate (they enqueue directly), so runtime authors who want to suppress post-end follow-ups check `EngineState.Lifecycle` from inside `Evaluate`.

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
// as any other entry. See features/triggers/models/01-trigger-fold.md.
```

### Lifecycle gate

`Dispatch` (and the public `StartMatch` / `FinishMatch` sugar that route through it) gates on `EngineState.Lifecycle` before `pipeline.Validate` runs. Per [ADR-0006](Product/decisions/0006-two-pipeline-architecture.md), the runtime no longer has an `OnIncomingCommand` hook — the gate runs first, then `pipeline.Validate`. Lifecycle is a framework concern, applied uniformly.

| Lifecycle | Command | Result |
|---|---|---|
| `NotStarted` | `StartMatchCommand` | Validate runs; handler flips to `Started` |
| `NotStarted` | `EndMatchCommand` | Validate runs; handler flips to `Ended` (cancel-before-start) |
| `NotStarted` | Any other | Reject(`"MatchNotStarted"`) before pipeline runs |
| `Started` | `EndMatchCommand` | Validate runs; handler flips to `Ended` |
| `Started` | Any other | Normal pipeline flow |
| `Ended` | Anything | Reject(`"MatchEnded"`) before pipeline runs |

Once `Lifecycle` flips to `Ended` mid-drain (via `EndMatchCommand`'s handler), the queue tail continues to drain naturally — already-queued commands aren't pre-cleared and execute in order. If a tail command tries to dispatch follow-ups during the drain, those new dispatches hit the `Ended` gate and reject. Trigger reactions dispatched through `engine.Dispatch` *during* the same drain that ended the match are subject to the same gate. Evaluator follow-ups bypass `Dispatch` entirely (they go through the engine's internal `pending.Enqueue` after `Evaluate` returns), so they are not subject to the lifecycle gate; runtime authors who want to suppress evaluator follow-ups after match end check `EngineState.Lifecycle` from inside `Evaluate`.

The lifecycle commands and their handlers are defined in [`06-match-lifecycle.md`](features/engine/models/06-match-lifecycle.md).

### State pattern — `GameState` as Scaffold slices

`GameState` is **not a single record**. State lives as multiple typed `BaseState` slices in a Scaffold `Store`. There is no `state.Piles`, `state.LastMove`, etc. Reads always go through `IStateScope.Get<TState>()`; writes always go through `Store.Execute(payload)` → registered `Mutator<TState, TPayload>`. The runtime reads through the injected `IStateScope`; it never mutates state directly. `IAvailableActions.Set` is the single write path for the snapshot — it itself goes through an internal mutator, not a direct field write.

The full slice catalog (framework-owned vs game-authored, the `ISliceProvider` mechanism, subscriptions, atomic batches) is in [`04-gamestate-slices.md`](features/engine/models/04-gamestate-slices.md).

### Reading state, not the just-executed action

The runtime's primary lens for follow-ups is the **current game state**, not which specific action just ran. If the runtime matched on action types ("after `PlayCardCommand` → run execute"), it would miss any other way a card could end up in the same zone (an opponent's trigger, a copy effect, a token). State-based evaluation handles all sources the same way. The methods relevant to the loop:

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
    // Reads current state, returns 0..N actions to enqueue next.
    public virtual IReadOnlyList<GameAction> Evaluate(
        GameAction    justExecuted,
        CommandResult result)
        => Array.Empty<GameAction>();

    // Effect-pipeline orchestration surface — separate design pass; tracked as oq-effect-pipeline.
}
```

State is read through the injected `IStateScope` (`State.Get<TState>()`). The runtime observes state and returns follow-up actions; it never mutates state directly.

### Drain end — detected from state, not a callback

There is no `OnQueueDrained` callback (deleted per ADR-0006). The runtime detects "queue is about to empty" by observing state inside `Evaluate` and acts on that observation:

- **Phase advance** — when state-based follow-ups are exhausted and a phase needs to advance, return the phase-advance action from `Evaluate`.
- **Priority grant** (MTG-like) — when stack is non-empty and priority isn't granted, return `GrantPriorityCommand` from `Evaluate`.
- **Available-actions snapshot** — when the runtime decides "now is a good moment to publish," it calls `Actions.Set(...)` from inside `Evaluate`. Pile Counter publishes when its own follow-ups are empty; MTG publishes when priority is granted.

| Game situation                              | Runtime behaviour in `Evaluate`                         |
| ------------------------------------------- | ------------------------------------------------------- |
| Slay the Spire, mid-player-turn             | Return `[]` — player still has available actions        |
| Slay the Spire, player clicked end turn     | Return `[StartEnemyTurnCommand]`                        |
| MTG-like, stack empty, no priority granted  | Return `[GrantPriorityCommand(activePlayer)]`           |
| Any game, all players have no legal actions | Return `[AdvancePhaseCommand]`                          |

Snapshot publication is purely runtime-driven via the injected `IAvailableActions` service. The framework never auto-publishes; if the runtime never calls `Actions.Set`, the snapshot does not change. See [`engine/03-snapshot-pipeline.md`](features/engine/models/03-snapshot-pipeline.md).

### Relationship to the command pipeline

The pipeline described in [`actions/01-command-pipeline.md`](features/actions/models/01-command-pipeline.md) does not change. The engine wraps it:

| Concern                                                                | Where it lives                                   |
| ---------------------------------------------------------------------- | ------------------------------------------------ |
| Execute one action (before triggers, handler, after triggers, history) | `ICommandPipeline` (framework)                   |
| Decide what action runs next                                           | `GameRulesRuntime<T>` (game-specific)            |
| Drive the loop between them                                            | `GameEngine` (framework)                         |
| Submit actions from outside                                            | Entry points (`IntentGateway`, `TurnTimerService`, etc.) |
| Trigger fold ordering                                                  | `ITriggerPipeline` (swappable; default FIFO)     |

The `GameCommandDispatcher` from earlier sketches becomes `ICommandPipeline.Execute` — it handles one action and returns a result. Reactions are dispatched through `IEffectScope.Dispatch` from inside trigger executes; that path enters the engine through the same `Dispatch` method as external entry points.

### Available-actions snapshot (optional system)

Some games tell the player which actions are currently legal before they attempt anything — Hearthstone highlights playable cards, MTG shows what can be cast or activated. Other games place no restrictions on what a player can attempt — in a Stacklands-style game everything is always possible and the simulation just responds.

The framework supports both through an **optional** available-actions system. By default it is a no-op: all requests pass through the gateway unchecked. Games that need it opt in.

`AvailableActionsSnapshot` is a read-only artifact the runtime publishes through the injected `IAvailableActions` service. It lives as the `AvailableActionsState` slice. See [`actions/02-availability-snapshot.md`](features/actions/models/02-availability-snapshot.md) for the typed-template shape and [`engine/03-snapshot-pipeline.md`](features/engine/models/03-snapshot-pipeline.md) for the publication flow.

The runtime publishes whenever it deems appropriate (typically when its own follow-ups are exhausted and the queue is about to empty, but the moment is purely runtime-driven):

```csharp
public override IReadOnlyList<GameAction> Evaluate(GameAction action, CommandResult result)
{
    var followUps = ComputeStateBasedFollowUps();
    if (followUps.Any()) return followUps;

    // No state-based follow-ups. Publish snapshot at this moment.
    Actions.Set(BuildSnapshot());
    return Array.Empty<GameAction>();
}
```

Games that do not use this system never call `Actions.Set`. The framework never auto-publishes.

`IntentGateway` reads the snapshot before dispatching. If no snapshot is active, all requests pass:

```csharp
public IntentResult Submit(PlayerIntent request)
{
    var match = actionMatcher.TryMatch(request); // reads current snapshot; no-op if none published
    if (match.RequiresMatch && !match.Success)
        return new IntentResult { Status = IntentStatus.Rejected, Reason = RejectReason.NotAvailableAction };

    // ... dispatch as normal
}
```

`IntentMapper` is a simple reader — it does not compute game logic itself. The game logic that decided what is legal already ran inside `Evaluate`.

| Game style        | Uses snapshot? | Notes                                                                                  |
| ----------------- | -------------- | -------------------------------------------------------------------------------------- |
| Hearthstone / MTG | Yes            | Runtime publishes legal plays when its follow-ups exhaust; gateway rejects illegal attempts |
| Stacklands        | No             | No snapshot published; gateway no-op; everything is always attemptable                 |
| Slay the Spire    | Partial        | Could publish playable cards for UI highlights but skip gateway enforcement            |

## Invariants

1. **Strict-FIFO queue, framework-owned.** No runtime customization on routing; per ADR-0006 (cross-Model invariant pinned by Feature root).
2. **`Dispatch(GameAction)` only.** `GameSignal` emits via `scope.Emit`; `CompositeAction` runs inline. The queue holds `GameAction`s.
3. **Per-step `Evaluate`.** Called after every action's `pipeline.Execute` returns, not once at end of drain. Drain-end behavior folds into `Evaluate` (snapshot publication, phase advance) — there is no `OnQueueDrained` callback.
4. **`OriginCommandId` threading is mechanical.** The engine copies `OriginCommandId` from the just-executed action onto every evaluator follow-up. The runtime does not set it.
5. **Evaluator follow-ups bypass `Validate`.** External dispatches and trigger reactions go through `Dispatch` (validate + enqueue); follow-ups returned from `Evaluate` enter the queue directly.
6. **Lifecycle gate runs before `pipeline.Validate`.** Framework checks `EngineState.Lifecycle` first; per ADR-0006 there is no runtime-side intake hook. `NotStarted` admits only `StartMatchCommand` / `EndMatchCommand`; `Ended` admits nothing.
7. **Queue tail drains naturally on end.** When the lifecycle flips to `Ended` mid-drain, the queue is not pre-cleared — already-queued commands continue executing in order. New dispatches hit the `Ended` gate. Evaluator follow-ups bypass the gate.
8. **`engine.StartMatch` / `engine.FinishMatch` are sugar.** They construct and dispatch the framework-owned lifecycle commands through `Dispatch`. No direct state writes from the public engine surface; lifecycle transitions flow through the normal pipeline.
9. **`engine.Dispose` ensures end-of-match.** If `Lifecycle != Ended`, `Dispose` calls `FinishMatch(Aborted("Disposed"))` so `MatchEndedSignal` lands in history. Then disposes the runtime if `IDisposable`. The per-match DI scope is opened and torn down by the host — engine never sees the scope object.
10. **Drain is not re-entered.** If a command is dispatched while drain is running, it is enqueued and picked up by the current cycle. Concurrency / async re-entry is an open follow-up.
11. **`GameState` is multiple slices.** No single record. Reads via `IStateScope.Get<TState>()`; writes via `Store.Execute(payload)` → `Mutator<TState, TPayload>`. The runtime never writes directly; `IAvailableActions.Set` is the only "write" the runtime performs and even that goes through an internal mutator.

## Decisions

*n/a — no elevated ADR.* (The state-pattern decision is single-feature scope; folded as Q1 below.)

## Examples

### Zone-check evaluation (Slay the Spire style)

In this sample game, when a card is played it is moved into a **PlayArea** zone. The rule is: any card sitting in `PlayArea` must be executed. The runtime does not care that a `PlayCardCommand` just ran — it just checks the zone.

```csharp
public sealed class SampleRulesRuntime : GameRulesRuntime<SampleRulesDefinition>
{
    public override IReadOnlyList<GameAction> Evaluate(
        GameAction    justExecuted,
        CommandResult result)
    {
        // State-based check: is there a card in PlayArea waiting to be executed?
        var playArea = State.Get<ZoneContentsState>(WellKnownZones.PlayArea);
        if (playArea.HasCards)
        {
            var card = playArea.TopCard;
            return new GameAction[]
            {
                new ExecuteCardCommand
                {
                    Source       = card,
                    FromPlayZone = WellKnownZones.PlayArea,
                    DiscardZone  = State.Get<TurnState>().ActiveDiscardZone,
                }
            };
        }

        return Array.Empty<GameAction>();
    }
}
```

Traced flow:

```text
Player drags card C17 from Hand to PlayArea
  → IntentGateway.Submit(PlayerIntent)
  → engine.Dispatch(PlayCardCommand { Source: C17, FromZone: Hand, ToZone: PlayArea }, out _)

Engine validates and enqueues PlayCardCommand. Drain starts.

Drain dequeues PlayCardCommand:
  → pipeline executes: C17 removed from Hand, inserted into PlayArea
  → envelope published

runtime.Evaluate:
  → State.Get<ZoneContentsState>(PlayArea).HasCards == true
  → return [ExecuteCardCommand { Source: C17, ... }]

Engine enqueues ExecuteCardCommand and dequeues it:
  → pipeline executes: effect triggers (heal, damage, draw, etc.)
  → C17 removed from PlayArea, inserted into Discard
  → envelope published

runtime.Evaluate:
  → State.Get<ZoneContentsState>(PlayArea).HasCards == false
  → return []

Queue empty. Drain returns.
```

The runtime ran twice: once with a card in the zone (produced `ExecuteCardCommand`), once with the zone empty (produced nothing). The loop self-terminated.

### Conditional evaluation (MTG-like priority)

The same interface handles priority-based games by adding a state condition before auto-executing:

```csharp
public override IReadOnlyList<GameAction> Evaluate(
    GameAction    justExecuted,
    CommandResult result)
{
    var stack    = State.Get<ZoneContentsState>(WellKnownZones.Stack);
    var priority = State.Get<PriorityState>();

    if (!stack.HasCards)
        return Array.Empty<GameAction>();

    // Card is on the stack. Has priority been resolved?
    if (priority.BothPlayersPassed)
        return new[] { new ResolveTopOfStackCommand(stack.TopCard) };

    // Card is on the stack but priority is not resolved yet.
    // Grant priority if not already granted.
    if (!priority.IsGranted)
        return new[] { new GrantPriorityCommand(priority.NextToAct) };

    // Priority is granted and outstanding. Do nothing — wait for player input.
    return Array.Empty<GameAction>();
}
```

The structure is identical. The only difference is the conditions read from `PriorityState`. A Slay the Spire game has no priority state and never reaches those branches.

This is also where MTG-style stack semantics lives — per ADR-0006, the "stack" is a game-state zone (`ZoneContentsState` keyed by a stack-zone reference), and stack resolution is driven by `Evaluate` watching that zone. It is not a runtime queue customization.

## Open follow-ups

- **Effect pipeline shape.** The substantive open design surface — how the runtime expresses effect-level orchestration (when an effect starts, whether it nests, batches, or stacks). Per ADR-0006, this is a separate concept from command-queue routing. Tracked as `oq-effect-pipeline`.
- **Concurrency and re-entry.** The `running` guard serializes dispatch. Networking and async input will require a more explicit contract here.
- **Runtime composition.** A single `GameRulesRuntime<T>` is shown. Composing smaller rule modules (state-based action checks, trigger evaluation, phase rules) is `oq-gamerules-composition`.

---

*Below: provenance — how the state pattern was settled. Single-feature scope (no project-wide ADR applies; folded into Model provenance per §4.5 on 2026-05-08). Original ADR-0005 landing 2026-04-29.*

## Options considered

### Q1 — State pattern

The framework needs a state model that supports replay, per-player views, deterministic networking, and zero state held by the rule. We had Scaffold States available with a redux-like immutable model.

- **A — Mutable POCO state mutated in handlers.** Rejected — breaks replay and forces deep cloning for views.
- **B — Custom event-sourced state.** Rejected — duplicates Scaffold infrastructure for no incremental benefit.
- **C — Scaffold States: `GameState` immutable, redux-like; mutators react to commands. `IAvailableActions` writes via internal mutator.** **Chosen.**

## Rationale

`GameState` immutable + redux-like via Scaffold gives us four wins simultaneously: deterministic replay, snapshot/restore, per-player views, and a clean separation between state (Store) and decisions (runtime). The Store is canonical; entities are accessed via Entities-shaped handles through `com.scaffold.entities.states`. `IAvailableActions` writes via an internal mutator like every other state slice — no special-case write path. Custom event sourcing would duplicate Scaffold infrastructure for no incremental benefit; mutable POCOs would force deep cloning for per-player views and break replay.

## Consequences

- **Enables** deterministic replay; snapshot/restore (per-entity variables included via the bridge package); per-player views (read-only Scope handles); the clean two-layer split between rule (decisions) and state (slices).
- **Constrains** every state change to be expressible as a mutator over a payload — ad-hoc mutation isn't possible; the runtime never writes state directly (only through `Actions.Set`, which itself goes through an internal mutator). Reverts/replaces earlier mutable-POCO sketches in pre-migration drafts.
