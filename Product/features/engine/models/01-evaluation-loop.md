---
type: model
status: settled
references: root
tags: [#engine, #evaluation-loop, #orchestrator]
---

# Engine — Orchestrator And Evaluation Loop

This document defines what the **engine** is in the Card Framework: its structure, its responsibilities, and how game-specific rules plug into it.

Builds on:

- [Action Flow Snippets](features/actions/models/01-command-pipeline.md) — the command pipeline that the engine wraps
- [decisions/0006-two-pipeline-architecture.md](decisions/0006-two-pipeline-architecture.md) — the three-level model (commands / effects / triggers ⊆ effects) and the command-vs-effect pipeline split

---

## 1. What The Engine Is

The engine is the **orchestrator** of the framework's command pipeline. It is the central point that:

- Receives `GameAction`s from all entry points (player input, timers, trigger reactions, network)
- Manages a strict-FIFO queue of pending actions
- Executes one action at a time through the pipeline
- After each execution, asks game-specific logic what (if anything) comes next

The engine does not know what any particular action means or what game is being played. That knowledge lives in the **runtime** (`GameRulesRuntime<TDefinition>`), which the game implements. The engine only knows how to run the loop.

Per [ADR-0006](decisions/0006-two-pipeline-architecture.md), the **command pipeline is framework-owned** — strict FIFO, no game customization on routing. Effect-level orchestration (when an effect starts, whether effects nest, batch, or stack) is a separate concern, expressed by the runtime through state-based decisions in `Evaluate` plus, eventually, a dedicated effect-pipeline surface (separate design pass; tracked as `oq-effect-pipeline`).

---

## 2. The Three Parts

### ICommandPipeline

The framework's executor. It owns two responsibilities: validating commands at intake (by resolving the handler from the registry and delegating to `handler.Validate`), and executing them through the full pipeline (before triggers → handler → after triggers → envelope). It does not know about the queue or what happens after.

```csharp
public interface ICommandPipeline
{
    // Called at intake — resolves the handler and delegates to handler.Validate.
    bool Validate(GameCommand command, IStateScope state, out string? reason);

    // Called by the engine loop — runs the full pipeline for one action.
    CommandResult Execute(GameAction action);
}
```

### Rules layer (`GameRulesDefinition` + `GameRulesRuntime<TDefinition>`)

The game's decision layer. Split into two cooperating types: `GameRulesDefinition` (pure `[SerializeField]` data, asset-friendly) and `GameRulesRuntime<TDefinition>` (stateful runtime brain with services). The runtime is called after each action resolves — reads the current game state through `IStateScope` and returns the actions that should run next, if any. See [`engine/02-gamerules-contract.md`](features/engine/models/02-gamerules-contract.md) for the canonical contract.

### GameEngine

The loop itself. Holds the queue, calls the pipeline, calls the runtime, and manages the drain cycle.

---

## 3. The Engine Loop

```csharp
public sealed class GameEngine
{
    private readonly Queue<GameAction>             pending = new();
    private readonly ICommandPipeline              pipeline;
    private readonly GameRulesRuntime<TDefinition> runtime;
    private readonly Store                         store;   // Scaffold Store; framework slices auto-seeded at Build()
    private bool running;

    // Single entry point for all sources — player input, timers, trigger reactions.
    // Lifecycle gate runs first (framework), then pipeline.Validate. Per ADR-0006 the
    // runtime no longer gates intake — command-queue routing is framework-owned FIFO.
    public bool Dispatch(GameAction action, out string? reason)
    {
        // Lifecycle gate — see engine/06 §7.
        var lifecycle = store.Get<EngineState>().Lifecycle;
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
}
```

Key points:

- **Try-get `Dispatch`.** Returns `bool` plus `out string? reason` — symmetric with `pipeline.Validate`. `false` carries a non-null reason; `true` carries `null`.
- **`Dispatch(GameAction)`, not `Dispatch(GameCommand)`.** `GameSignal` is emitted via `scope.Emit` from inside handlers (never dispatched); `CompositeAction` runs inline within a parent handler (never queued). The engine's queue holds only `GameAction`s.
- **Drain is not re-entered.** If a command is dispatched while the loop is already running, it is enqueued and picked up by the current drain cycle.
- **Per-step evaluation.** The loop calls `runtime.Evaluate` after every action, not once at the end. This is what gives the engine per-step state-based awareness.
- **`OriginCommandId` threading.** The engine copies `OriginCommandId` from the just-executed action onto every evaluator follow-up. `PlayCardCommand` and any chain members share the same `OriginCommandId`. The engine does this mechanically — the runtime does not need to set it.
- **Lifecycle gate.** Framework rejects pre-`StartMatch` and post-`FinishMatch` dispatches uniformly. Only `StartMatchCommand` and `EndMatchCommand` are admitted from `NotStarted`; nothing is admitted from `Ended`. Match lifecycle in full lives in [`engine/06-match-lifecycle.md`](features/engine/models/06-match-lifecycle.md).
- **No `OnQueueDrained` callback.** Per ADR-0006, drain-end logic folds into `Evaluate` — the runtime detects "queue is about to empty" by observing state and its own follow-up emptiness, then publishes snapshots / dispatches phase advances from there. See §7.
- **Evaluator follow-ups bypass `Validate`.** Actions returned from `Evaluate` are produced by runtime logic and enter the queue directly. External entry points and trigger reactions still go through `Dispatch` (validate + enqueue).

---

## 4. Entry Points

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

---

## 5. The Runtime — Reading State, Not Just The Last Action

The runtime's primary lens for follow-ups is the **current game state**, not which specific action just ran. This is the key design point.

If the runtime matched on action types ("after `PlayCardCommand` → run execute"), it would miss any other way a card could end up in the same zone (an opponent's trigger, a copy effect, a token). State-based evaluation handles all sources the same way.

The canonical runtime contract is in [`engine/02-gamerules-contract.md`](features/engine/models/02-gamerules-contract.md). The methods relevant to the loop:

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

State is read through the injected `IStateScope` (`State.Get<TState>()`) — see [04-effect-flow §4.1](features/effects/models/04-effect-flow.md). The runtime observes state and returns follow-up actions; it never mutates state directly.

---

## 6. Sample: Zone-Check Evaluation (Slay The Spire Style)

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

### Traced flow

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

---

## 7. Drain End — Detected From State, Not A Callback

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

---

## 8. Variation: Conditional Evaluation (MTG-like Priority)

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

---

## 9. Relationship To The Existing Pipeline

The pipeline described in [Action Flow Snippets](features/actions/models/01-command-pipeline.md) does not change. The engine wraps it:


| Concern                                                                | Where it lives                                   |
| ---------------------------------------------------------------------- | ------------------------------------------------ |
| Execute one action (before triggers, handler, after triggers, history) | `ICommandPipeline` (framework)                   |
| Decide what action runs next                                           | `GameRulesRuntime<T>` (game-specific)            |
| Drive the loop between them                                            | `GameEngine` (framework)                         |
| Submit actions from outside                                            | Entry points (IntentGateway, TimerService, etc.) |
| Trigger fold ordering                                                  | `ITriggerPipeline` (swappable; default FIFO)     |


The `GameCommandDispatcher` from the original snippets becomes `ICommandPipeline.Execute` — it handles one action and returns a result. Reactions are dispatched through `IEffectScope.Dispatch` from inside trigger executes; that path enters the engine through the same `Dispatch` method as external entry points.

---

## 10. Available Actions Snapshot (Optional System)

Some games tell the player which actions are currently legal before they attempt anything — Hearthstone highlights playable cards, MTG shows what can be cast or activated. Other games place no restrictions on what a player can attempt — in a Stacklands-style game everything is always possible and the simulation just responds.

The framework supports both through an **optional** available-actions system. By default it is a no-op: all requests pass through the gateway unchecked. Games that need it opt in.

### The Snapshot

`AvailableActionsSnapshot` is a read-only artifact the runtime publishes through the injected `IAvailableActions` service. It lives as the `AvailableActionsState` slice. See [`features/actions/models/02-availability-snapshot.md`](features/actions/models/02-availability-snapshot.md) for the typed-template shape and [`engine/03-snapshot-pipeline.md`](features/engine/models/03-snapshot-pipeline.md) for the publication flow.

### Publishing

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

### Gateway Fast-Reject

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

### When Each Game Uses This


| Game style        | Uses snapshot? | Notes                                                                                  |
| ----------------- | -------------- | -------------------------------------------------------------------------------------- |
| Hearthstone / MTG | Yes            | Runtime publishes legal plays when its follow-ups exhaust; gateway rejects illegal attempts |
| Stacklands        | No             | No snapshot published; gateway no-op; everything is always attemptable                 |
| Slay the Spire    | Partial        | Could publish playable cards for UI highlights but skip gateway enforcement            |


---

## 11. Open Choices

- **Effect pipeline shape.** The substantive open design surface — how the runtime expresses effect-level orchestration (when an effect starts, whether it nests, batches, or stacks). Per ADR-0006, this is a separate concept from command-queue routing. Tracked as `oq-effect-pipeline`.
- **Concurrency and re-entry.** The `running` guard serializes dispatch. Networking and async input will require a more explicit contract here.
- **Runtime composition.** A single `GameRulesRuntime<T>` is shown. Composing smaller rule modules (state-based action checks, trigger evaluation, phase rules) is `oq-gamerules-composition`.

---

## 12. Related

- [decisions/0006-two-pipeline-architecture.md](decisions/0006-two-pipeline-architecture.md) — three-level model, command vs effect pipeline split
- [features/engine/models/02-gamerules-contract.md](features/engine/models/02-gamerules-contract.md) — canonical runtime contract
- [features/actions/models/01-command-pipeline.md](features/actions/models/01-command-pipeline.md) — the command pipeline this engine wraps
- [features/actions/models/02-availability-snapshot.md](features/actions/models/02-availability-snapshot.md) — typed template shape
- [features/engine/models/03-snapshot-pipeline.md](features/engine/models/03-snapshot-pipeline.md) — snapshot publication via Actions.Set
- [features/engine/models/06-match-lifecycle.md](features/engine/models/06-match-lifecycle.md) — match start/end and lifecycle gate
- [features/triggers/models/01-trigger-fold.md](features/triggers/models/01-trigger-fold.md) — trigger fold inside `pipeline.Execute`
- [ledgers/OPEN-ITEMS.md](ledgers/OPEN-ITEMS.md) — `oq-effect-pipeline`, `oq-gamerules-composition`, etc.
