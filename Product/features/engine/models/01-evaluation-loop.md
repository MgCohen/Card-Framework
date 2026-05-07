---
type: model
status: settled
references: root
tags: [#engine, #evaluation-loop, #orchestrator]
---

# Evaluation Loop — model

## What it is

The engine is the **orchestrator** — the central point that receives commands from all entry points (player input, timers, automatic triggers, network), manages a queue of pending commands, executes one command at a time through the pipeline, asks game-specific logic what (if anything) comes next after each execution, and asks game-specific logic what to do when the queue empties. The engine does not know what any particular command means or what game is being played — that knowledge lives in the **evaluator** (the runtime), which the game implements. The engine only knows how to run the loop.

## Contracts

### The three parts

**`ICommandPipeline`** — the framework's executor. Owns two responsibilities: validating commands at intake (by resolving the handler from the registry and delegating to `handler.Validate`), and executing them through the full pipeline (before triggers → handler → after triggers → envelope). Does not know about the queue or what happens after.

```csharp
public interface ICommandPipeline
{
    // Called at intake — resolves the handler and delegates to handler.Validate.
    bool Validate(GameCommand command, IStateScope state, out string? reason);

    // Called by the engine loop — runs the full pipeline for one action.
    CommandResult Execute(GameAction action);
}
```

**Rules layer (`GameRulesDefinition` + `GameRulesRuntime<T>`)** — the game's rules brain. Split into two cooperating types: `GameRulesDefinition` (pure `[SerializeField]` data, asset-friendly) and `GameRulesRuntime<TDefinition>` (stateful runtime brain with services + game-specific subsystems). The runtime is called after each command resolves — reads the current game state (zones, variables, phase) and returns the commands that should run next — if any. Also called when the queue empties. Throughout this doc, `GameRules` in code sketches denotes the runtime; see [`engine/02-gamerules-contract.md`](features/engine/models/02-gamerules-contract.md) for the canonical split.

**`GameEngine`** — the loop itself. Holds the queue, calls the pipeline, calls the evaluator, manages the drain cycle, gates lifecycle.

### The engine loop

```csharp
public sealed class GameEngine
{
    private readonly Queue<GameAction> pending = new();
    private readonly ICommandPipeline pipeline;
    private readonly GameRules rules;
    private readonly Store store;   // Scaffold Store; framework slices auto-seeded at Build()
    private bool running;

    // Single entry point for all sources — player input, timers, trigger reactions.
    // Lifecycle gate runs first (framework), then pipeline.Validate, then GameRules.OnIncomingCommand.
    public IntakeResult Dispatch(GameCommand command)
    {
        // Lifecycle gate — see engine/06 §Intake Gates.
        var lifecycle = store.Get<EngineState>().Lifecycle;
        if (lifecycle == EngineLifecycle.Ended)
            return IntakeResult.Reject("MatchEnded");
        if (lifecycle == EngineLifecycle.NotStarted
            && command is not StartMatchCommand
            && command is not EndMatchCommand)
            return IntakeResult.Reject("MatchNotStarted");

        if (!pipeline.Validate(command, store, out var reason))
            return IntakeResult.Reject(reason);
        var result = rules.OnIncomingCommand(command, DispatchOrigin.PlayerIntent);
        if (!result.Accepted)
            return result;
        if (!running) Drain();
        return result;
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
- `OnQueueDrained` is the hook for when a drain just finished: the work queue is empty, so the game can phase-advance, grant priority, publish available actions, or await input.
- **`OriginCommandId` threading:** the engine copies `OriginCommandId` from the just-executed command onto every evaluator follow-up. This means `PlayCardCommand` and `ExecuteCardCommand` (and any further chain members) all share the same `OriginCommandId`. The engine does this mechanically — the evaluator does not need to set it.
- **Lifecycle gate:** framework rejects pre-`StartMatch` and post-`FinishMatch` dispatches uniformly. Only `StartMatchCommand` and `EndMatchCommand` are admitted from `NotStarted`; nothing is admitted from `Ended`. Match lifecycle in full lives in [`engine/06-match-lifecycle.md`](features/engine/models/06-match-lifecycle.md).

### Entry points

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

    // ActorId is boundary-correct — timer events originate from an external clock with no
    // Actor object reference (per Product/decisions/0005-objects-over-ids.md).
    public void OnTimeExpired(ActorId actor)
    {
        engine.Dispatch(new ForceEndTurnCommand(actor));
    }
}

// A trigger reaction fires — dispatches through the same channel as everything else.
// GameRules.OnIncomingCommand decides scheduling (RunNext, Enqueue, Stash, Discard).
public sealed class TriggerContext
{
    private readonly GameEngine engine;

    public void Dispatch(GameCommand command)
    {
        engine.Dispatch(command);
    }
}
```

### The evaluator — reading state, not just the last command

The evaluator's primary lens is the **current game state**, not which specific command just ran. This is the key design point. If the evaluator matched on command types ("after `PlayCardCommand` → run execute"), it would miss any other way a card could end up in the same zone (an opponent's trigger, a copy effect, a token). State-based evaluation handles all sources the same way.

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

### `OnQueueDrained` — what happens when a drain finishes

`OnQueueDrained` is where game-specific phase logic lives after the loop has no more work for this pass.

| Game situation | `OnQueueDrained` behaviour |
|---|---|
| Slay the Spire, mid-player-turn | Do nothing — player still has available actions |
| Slay the Spire, player clicked end turn | `engine.Dispatch(new StartEnemyTurnCommand())` |
| MTG-like, stack empty, no priority granted | `engine.Dispatch(new GrantPriorityCommand(activePlayer))` |
| Any game, all players have no legal actions | `engine.Dispatch(new AdvancePhaseCommand())` |

`OnQueueDrained` can dispatch back into the engine — the queue re-fills and `Drain` continues. It is not a special exit path; it is just another way commands enter.

### Variation: conditional evaluation (MTG-like priority)

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
    if (!state.PriorityState.IsGranted)
        return new[] { new GrantPriorityCommand(state.PriorityState.NextToAct) };

    // Priority is granted and outstanding. Do nothing — wait for player input.
    return Array.Empty<GameAction>();
}
```

The structure is identical. The only difference is the conditions read from `state.PriorityState`. A Slay the Spire game has no priority state and never reaches those branches.

### Relationship to the existing pipeline

The pipeline does not change; the engine wraps it.

| Concern | Where it lives |
|---|---|
| Execute one action (before triggers, handler, after triggers, history) | `ICommandPipeline` |
| Decide what command runs next | `GameRules` (game-specific) |
| Drive the loop between them | `GameEngine` (framework) |
| Submit commands from outside | Entry points (`IntentGateway`, `TimerService`, etc.) |

### Available actions snapshot (optional)

Some games tell the player which actions are currently legal before they attempt anything; others let the simulation respond freely. The framework supports both through an optional available-actions system. By default it is a no-op; games opt in. Full mechanism — service-driven publication via `IAvailableActions`, typed `ActionTemplate` per command type, on-demand validation — lives in [`engine/03-snapshot-pipeline.md`](features/engine/models/03-snapshot-pipeline.md).

## Invariants

Model-local invariants. (See `engine.md ## Invariants` for feature-level rules — single orchestrator, lifecycle gate, evaluator-reads-state principle, OriginCommandId threading.)

- **`Drain` is not re-entered.** A command dispatched while `running` is already true is enqueued for the current drain cycle.
- **The loop evaluates after every command, not once at the end.** Per-step awareness.
- **`Dispatch` is the single entry point.** No bypass paths; all sources go through it.
- **`running = false` is set before `OnQueueDrained` dispatches its follow-ups.** This lets `Drain` re-enter if needed.
- **`OriginCommandId` is engine-threaded.** The evaluator does not need to set it on follow-ups; the engine copies it from the just-executed command.

## Decisions

*n/a — no elevated ADR.*

## Examples

**Zone-Check Evaluation (Slay the Spire style).** When a card is played it is moved into a `PlayArea` zone. Rule: any card sitting in `PlayArea` must be executed. The evaluator does not care that a `PlayCardCommand` just ran — it just checks the zone.

```csharp
public sealed class SampleGameRules : GameRules
{
    public IntakeResult OnIncomingCommand(GameCommand command, DispatchOrigin origin)
    {
        // Slay the Spire style: triggered reactions run immediately.
        return IntakeResult.Ok;
    }

    public IReadOnlyList<GameAction> Evaluate(
        GameAction justExecuted,
        CommandResult result)
    {
        var playArea = State.Get<ZonesState>().GetZone(WellKnownZones.PlayArea);
        if (playArea.HasCards)
        {
            var card = playArea.TopCard;
            return new GameAction[]
            {
                new ExecuteCardCommand
                {
                    Actor = State.Get<TurnState>().ActiveActor,
                    Source = card,
                    FromPlayZone = WellKnownZones.PlayArea,
                    DiscardZone = State.Get<ZonesState>().DiscardZoneFor(...)
                }
            };
        }

        return Array.Empty<GameAction>();
    }

    public DrainResult OnQueueDrained()
        => DrainResult.Empty;   // no work to do; await input
}
```

Traced flow:

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

## Open follow-ups

- **Command naming** — "GameCommand" covers both player-initiated and automatic commands. A finer split (`PlayerCommand` vs `SystemCommand`) may be warranted once more command types exist.
- **Queue ordering** — A simple FIFO queue is shown. Priority queues, interrupt injection, or stack semantics (for MTG-like resolution order) are variations the engine queue shape would need to support.
- **Evaluator composition** — A single `GameRules` implementation is shown. In practice, it may be composed of smaller rule modules. See `oq-gamerules-composition`.
- **Concurrency and re-entry** — The `running` guard serializes dispatch. Networking and async input will require a more explicit contract here.

---

*Below: provenance — feature-internal decision folded on 2026-05-07 from `features/engine/decisions/0006-mtg-sba-known-difference.md`. Single-feature scope (the SBA-ordering choice is internal to the engine's evaluation loop; games that need MTG ordering work around it per-game).*

## Options considered

### Q1 — MTG-strict state-based-action ordering (formerly ADR-0006 — folded)

Magic: the Gathering's rules engine performs state-based actions (SBAs) in a tight loop *between* trigger fold and reaction dispatch. This evaluation loop folds triggers and dispatches reactions in one phase. This is a real semantic difference and needs to be acknowledged rather than pretended away.

- **A — Add a pre-after-fold evaluator hook to the loop.** Deferred — until a game actually needs MTG-strict SBA ordering.
- **B — Rebuild the loop around an MTG-strict model.** Rejected: pessimizes the floor case; most games don't care.
- **C — Document as a known difference; implement a workaround (manual SBA pass before reactions) on a per-game basis.** **Chosen for now.**

## Rationale

The evaluation loop diverges from MTG-strict SBA ordering by design. Games that need MTG semantics implement the SBA pass themselves until the framework adds the hook. When a real game forces the issue, revisit Option A.

## Consequences

- **Enables:** a simpler default loop for the floor sample and Stacklands-class games.
- **Constrains:** porting MTG-class rules requires a manual SBA pass.
- **Re-open trigger:** first game that requires MTG-strict ordering.
- **Cascade:** the post-after-fold evaluator timing is consumed by `engine/02-gamerules-contract.md` (`Evaluate` semantics) and downstream effects/triggers.
