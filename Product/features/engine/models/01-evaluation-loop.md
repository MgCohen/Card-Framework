---
type: model
status: settled
references: root
tags: [#engine, #evaluation-loop, #orchestrator]
---

# Engine — Orchestrator And Evaluation Loop

This document defines what the **engine** is in the Card Framework: its structure, its responsibilities, and how game-specific rules plug into it. It is research, not a locked API.

Builds on:

- [Action Flow Snippets](features/actions/models/01-command-pipeline.md) — the existing pipeline that the engine wraps
- [Handler Mutations, Sequential Apply, And Semantic Events](features/actions/models/01-command-pipeline.md) — what happens inside a single command execution

---

## 1. What The Engine Is

The engine is the **orchestrator**. It is the central point that:

- Receives commands from all entry points (player input, timers, automatic triggers, network)
- Manages a queue of pending commands
- Executes one command at a time through the pipeline
- After each execution, asks game-specific logic what (if anything) comes next
- When the queue empties, asks game-specific logic what to do now

The engine does not know what any particular command means or what game is being played. That knowledge lives in the **evaluator**, which the game implements. The engine only knows how to run the loop.

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

### GameRules

The game's rules brain. Called after each command resolves. Reads the current game state (zones, variables, phase) and returns the commands that should run next — if any. Also called when the queue empties.

### GameEngine

The loop itself. Holds the queue, calls the pipeline, calls the evaluator, and manages the drain cycle.

---

## 3. The Engine Loop

```csharp
public sealed class GameEngine
{
    private readonly Queue<GameAction> pending = new();
    private readonly ICommandPipeline pipeline;
    private readonly GameRules rules;
    private readonly Store store;   // Scaffold Store; framework slices auto-seeded at Build()
    private bool running;

    // Single entry point for all sources — player input, timers, trigger reactions.
    // Pipeline validates first; GameRules.OnIncomingCommand decides scheduling.
    public void Dispatch(GameCommand command)
    {
        if (!pipeline.Validate(command, store, out _))
            return;
        var result = rules.OnIncomingCommand(command, DispatchOrigin.PlayerIntent);
        if (!result.Accepted)
            return;
        if (!running) Drain();
    }

    private void Drain()
    {
        running = true;

        while (pending.Count > 0)
        {
            var action = pending.Dequeue();
            var result = pipeline.Execute(action);

            // Game-specific: given the state after this action, what actions run next?
            var followUps = rules.Evaluate(action, result);
            foreach (var next in followUps)
                pending.Enqueue(next with { OriginCommandId = action.OriginCommandId });
        }

        running = false;

        // Queue is empty. Game decides what now.
        // running = false before dispatching so Drain() can re-enter if needed.
        var drainResult = rules.OnQueueDrained();
        foreach (var cmd in drainResult.Dispatch)
            Dispatch(cmd);
    }
}
```

Key points:

- `Drain` is **not re-entered**. If a command is dispatched while the loop is already running, it is enqueued and picked up by the current drain cycle.
- The loop evaluates **after every command**, not once at the end. This is what gives the engine per-step awareness.
- `OnQueueDrained` is the hook for when a drain just finished: the work queue is empty, so the game can phase-advance, grant priority, publish available actions, or await input. It is not a generic "idle" or presentation concept.
- `**OriginCommandId` threading:** the engine copies `OriginCommandId` from the just-executed command onto every evaluator follow-up. This means `PlayCardCommand` and `ExecuteCardCommand` (and any further chain members) all share the same `OriginCommandId`. The engine does this mechanically — the evaluator does not need to set it. `OriginCommandId` is the ID of the first command in the chain, not the original player request — though the first command's envelope separately records the originating request for correlation.

---

## 4. Entry Points

Everything that wants the engine to act submits a `GameCommand`. The engine does not know or care which entry point created it.

```csharp
// Player drags a card
public sealed class IntentGateway
{
    private readonly GameEngine engine;

    public void Submit(PlayerIntent request)
    {
        var command = Convert(request); // resolve actor, zone, intent
        engine.Dispatch(command);
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
        engine.Dispatch(new ForceEndTurnCommand(actor));
    }
}

// A trigger reaction fires — dispatches through the same channel as everything else.
// GameRules.OnIncomingCommand decides scheduling (RunNext, Enqueue, Stash, Discard).
// See features/triggers/models/01-trigger-fold.md for the full trigger model.
public sealed class TriggerContext
{
    private readonly GameEngine engine;

    public void Dispatch(GameCommand command)
    {
        engine.Dispatch(command);
    }
}
```

---

## 5. The Evaluator — Reading State, Not Just The Last Command

The evaluator's primary lens is the **current game state**, not which specific command just ran. This is the key design point.

If the evaluator matched on command types ("after `PlayCardCommand` → run execute"), it would miss any other way a card could end up in the same zone (an opponent's trigger, a copy effect, a token). State-based evaluation handles all sources the same way.

```csharp
public interface GameRules
{
    // Called when any command enters the dispatch channel.
    // Game decides scheduling: interrupt, queue, stash, or discard.
    IntakeResult OnIncomingCommand(GameCommand command, DispatchOrigin origin);

    // Called after each GameAction resolves.
    // Reads current state, returns 0..N actions to enqueue next.
    IReadOnlyList<GameAction> Evaluate(
        GameAction justExecuted,
        CommandResult result);

    // Called when a drain has finished: the work queue is empty.
    // Game decides: wait for input, advance phase, end game, etc.
    DrainResult OnQueueDrained();
}

public sealed record IntakeResult(bool Accepted, string? Reason = null);
public sealed record DrainResult(IReadOnlyList<GameCommand> Dispatch);
public enum DispatchOrigin { PlayerIntent, TriggerReaction, Timer, Internal }
```

`GameState` here is the simulation state — zone contents, actor variables, phase info. It is read-only from the evaluator's perspective. The evaluator observes and returns commands; it never mutates state directly.

---

## 6. Sample: Zone-Check Evaluation (Slay The Spire Style)

In this sample game, when a card is played it is moved into a **PlayArea** zone. The rule is: any card sitting in `PlayArea` must be executed. The evaluator does not care that a `PlayCardCommand` just ran — it just checks the zone.

```csharp
public sealed class SampleGameRules : GameRules
{
    public SchedulingDecision OnIncomingCommand(GameCommand command, SchedulingContext context)
    {
        // Slay the Spire style: triggered reactions run immediately.
        return SchedulingDecision.Enqueue;
    }

    public IReadOnlyList<GameAction> Evaluate(
        GameAction justExecuted,
        CommandResult result,
        GameState state)
    {
        // State-based check: is there a card in PlayArea waiting to be executed?
        var playArea = state.GetZone(WellKnownZones.PlayArea);
        if (playArea.HasCards)
        {
            var card = playArea.TopCard;
            return new GameAction[]
            {
                new ExecuteCardCommand
                {
                    Actor = state.ActiveActor,
                    Source = card,
                    FromPlayZone = WellKnownZones.PlayArea,
                    DiscardZone = state.DiscardZoneFor(state.ActiveActor)
                }
            };
        }

        return Array.Empty<GameAction>();
    }

    public void OnQueueDrained(GameEngine engine, GameState state)
    {
        // Queue is empty and PlayArea is empty.
        // Player still has cards in hand and energy — do nothing; await input.
        // If the player clicked "end turn", that would have dispatched
        // an EndTurnCommand, so we only reach here in the mid-turn idle state.
    }
}
```

### Traced flow

```text
Player drags card C17 from Hand to PlayArea
  → IntentGateway.Submit(MoveInstanceToZoneRequest)
  → engine.Dispatch(PlayCardCommand { Source: C17, FromZone: Hand, ToZone: PlayArea })

Engine dequeues PlayCardCommand:
  → pipeline executes: C17 removed from Hand, inserted into PlayArea
  → envelope published

Evaluator.Evaluate:
  → state.GetZone(PlayArea).HasCards == true
  → return [ExecuteCardCommand { Source: C17, ... }]

Engine enqueues ExecuteCardCommand and dequeues it:
  → pipeline executes: effect triggers (heal, damage, draw, etc.)
  → C17 removed from PlayArea, inserted into Discard
  → envelope published

Evaluator.Evaluate:
  → state.GetZone(PlayArea).HasCards == false
  → return []

Queue empty → OnQueueDrained:
  → player still has energy and cards in hand → no action, await input
```

The evaluator ran twice: once with a card in the zone (produced `ExecuteCardCommand`), once with the zone empty (produced nothing). The loop self-terminated.

---

## 7. OnQueueDrained — What Happens When A Drain Finishes

`OnQueueDrained` is where game-specific phase logic lives after the loop has no more work for this pass. Examples:


| Game situation                              | `OnQueueDrained` behaviour                                |
| ------------------------------------------- | --------------------------------------------------------- |
| Slay the Spire, mid-player-turn             | Do nothing — player still has available actions           |
| Slay the Spire, player clicked end turn     | `engine.Dispatch(new StartEnemyTurnCommand())`            |
| MTG-like, stack empty, no priority granted  | `engine.Dispatch(new GrantPriorityCommand(activePlayer))` |
| Any game, all players have no legal actions | `engine.Dispatch(new AdvancePhaseCommand())`              |


`OnQueueDrained` can dispatch back into the engine — the queue re-fills and `Drain` continues. It is not a special exit path; it is just another way commands enter.

---

## 8. Variation: Conditional Evaluation (MTG-like Priority)

The same interface handles priority-based games by adding a state condition before auto-executing:

```csharp
public IReadOnlyList<GameAction> Evaluate(
    GameAction justExecuted,
    CommandResult result,
    GameState state)
{
    var stack = state.GetZone(WellKnownZones.Stack);

    if (!stack.HasCards)
        return Array.Empty<GameAction>();

    // Card is on the stack. Has priority been resolved?
    if (state.PriorityState.BothPlayersPassed)
        return new[] { new ResolveTopOfStackCommand(stack.TopCard) };

    // Card is on the stack but priority is not resolved yet.
    // Grant priority if not already granted.
    if (!state.PriorityState.IsGranted)
        return new[] { new GrantPriorityCommand(state.PriorityState.NextToAct) };

    // Priority is granted and outstanding. Do nothing — wait for player input.
    return Array.Empty<GameAction>();
}
```

The structure is identical. The only difference is the conditions read from `state.PriorityState`. A Slay the Spire game has no priority state and never reaches those branches.

---

## 9. Relationship To The Existing Pipeline

The pipeline described in [Action Flow Snippets](features/actions/models/01-command-pipeline.md) does not change. The engine wraps it:


| Concern                                                                | Where it lives                                   |
| ---------------------------------------------------------------------- | ------------------------------------------------ |
| Execute one action (before triggers, handler, after triggers, history) | `ICommandPipeline` (existing)                    |
| Decide what command runs next                                          | `GameRules` (game-specific)                      |
| Drive the loop between them                                            | `GameEngine` (framework)                         |
| Submit commands from outside                                           | Entry points (IntentGateway, TimerService, etc.) |


The `GameCommandDispatcher` from the snippets becomes `ICommandPipeline.Execute` — it handles one command and returns a result. The inline `RunReactions` call that was inside the dispatcher moves out entirely. Reactions are now the evaluator's job, not the dispatcher's.

---

## 10. Available Actions Snapshot (Optional System)

Some games tell the player which actions are currently legal before they attempt anything — Hearthstone highlights playable cards, MTG shows what can be cast or activated. Other games place no restrictions on what a player can attempt — in a Stacklands-style game everything is always possible and the simulation just responds.

The framework supports both through an **optional** available-actions system. By default it is a no-op: all requests pass through the gateway unchecked. Games that need it opt in.

### The Snapshot

`IAvailableActionsSnapshot` is a read-only artifact the game publishes at the end of a drain cycle. It lists the actions currently available to each actor, each optionally bound to the request that would trigger it:

```csharp
public interface IAvailableActionsSnapshot
{
    // ADR-0005: ActorId is boundary-correct — presentation queries the snapshot by actor identity;
    // presentation is the wire-form consumer side per ADR-0005.
    IReadOnlyList<AvailableAction> GetActionsFor(ActorId actor);
}

public sealed record AvailableAction
{
    public required ActionId Action { get; init; }
    public PlayerIntent? TriggeringRequest { get; init; } // null = UI determines how to trigger
}
```

The binding between an action and its triggering request can be defined at the **UI level** (the UI knows how to express the action as a drag, click, or button press) or at the **system level** (the snapshot carries the exact request). Both are valid depending on the game.

### Publishing

The evaluator publishes the snapshot inside `OnQueueDrained`, after the queue empties:

```csharp
public void OnQueueDrained(GameEngine engine, GameState state)
{
    var snapshot = ComputeAvailableActions(state);
    state.PublishAvailableActions(snapshot); // stored as a state artifact; UI and gateway can read it
}
```

Games that do not use this system leave `OnQueueDrained` empty or call a no-op publisher.

### Gateway Fast-Reject

`IntentGateway` reads the snapshot before dispatching. If no snapshot is active, all requests pass:

```csharp
public IntentResult Submit(MoveInstanceToZoneRequest request)
{
    var match = actionMatcher.TryMatch(request); // reads current snapshot; no-op if none published
    if (match.RequiresMatch && !match.Success)
        return new IntentResult { Status = IntentStatus.Rejected, Reason = RejectReason.NotAvailableAction };

    // ... dispatch as normal
}
```

`IntentMapper` is a simple reader — it does not compute game logic itself. The game logic that decided what is legal already ran inside `OnQueueDrained`.

### When Each Game Uses This


| Game style        | Uses snapshot? | Notes                                                                                 |
| ----------------- | -------------- | ------------------------------------------------------------------------------------- |
| Hearthstone / MTG | Yes            | Engine publishes legal plays after each queue drain; gateway rejects illegal attempts |
| Stacklands        | No             | No snapshot published; gateway no-op; everything is always attemptable                |
| Slay the Spire    | Partial        | Could publish playable cards for UI highlights but skip gateway enforcement           |


---

## 11. Open Choices

- **Command naming:** "GameCommand" covers both player-initiated and automatic commands. A finer split (e.g. `PlayerCommand` vs `SystemCommand`) may be warranted once more command types exist.
- **Queue ordering:** A simple FIFO queue is shown. Priority queues, interrupt injection, or stack semantics (for MTG-like resolution order) are variations the engine queue shape would need to support.
- **Evaluator composition:** A single `GameRules` implementation is shown. In practice, it may be composed of smaller rule modules (state-based action checks, trigger evaluation, phase rules) rather than one monolithic class.
- **Concurrency and re-entry:** The `running` guard serializes dispatch. Networking and async input will require a more explicit contract here.

---

## 12. Related

- [features/actions/models/01-command-pipeline.md](features/actions/models/01-command-pipeline.md) — the existing end-to-end flow that this engine wraps
- [features/actions/models/01-command-pipeline.md](features/actions/models/01-command-pipeline.md) — what happens inside a single pipeline execution
- [features/actions/models/01-command-pipeline.md](features/actions/models/01-command-pipeline.md) — payload and result shapes
- [ledgers/OPEN-QUESTIONS.md](ledgers/OPEN-QUESTIONS.md) — scheduler and rules-engine open questions

