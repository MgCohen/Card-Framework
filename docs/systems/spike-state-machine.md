# State Machine (deleted spike)

A standalone game-state FSM where each state is a zero-data record and every transition runs as a command through one queue.

**Code:** deleted. Last version at commit `b13907e`, [`Assets/Spike/ECS/StateMachine/`](https://github.com/MgCohen/Card-Framework/tree/b13907e/Assets/Spike/ECS/StateMachine). Built May 2026.

## What it does
- Models a simplified MTG turn (`Upkeep` to `End`, then back to `Upkeep`) as `GameState` records with no fields. The record type is the identity.
- Keeps "state data" off the record. Attack selection lives as an `IsAttacking` tag on creatures; turn and active player live on `MatchSingleton`.
- Auto-advances: after each command the engine asks `IsCompleted`, then asks `IStateFlow` for the successor.
- Runs transitions as a `PushStateCommand`, so they flow through the same pipeline as every other command.
- Records each transition as a 2-byte `StatePushEvent(NewKind, PrevKind)` that replay can walk forward or back.
- Ships 19 EditMode tests and an IMGUI sandbox (`StateMachineBootstrap`) whose buttons follow `GameState.Actions`.

## How it works
1. `GameEngine.Enqueue(cmd)` adds to a FIFO queue and drains it inline (a sync engine).
2. For each command, `HandlerRegistry` resolves the `ICommandHandler<TCmd, TRes>` and runs it. Every command logs a `CommandExecutedEvent`.
3. `Evaluate` (skipped for `PushStateCommand`): if `State.Current.IsCompleted(action, result, scope)`, it calls `Flow.NextAfter(current)` and enqueues `PushStateCommand(next)`.
4. `PushStateHandler` enqueues `old.OnExit(...)`, emits `StatePushEvent`, swaps `StateService.Current`, then enqueues `new.OnEnter(...)`.
5. Generated commands run later in the same drain. Replay reads the log and never re-runs `OnEnter` or `OnExit`.

## Key types
| Type | Job |
|---|---|
| `GameState` | Abstract record. Virtuals: `Actions`, `OnEnter`, `OnExit`, `IsCompleted`. |
| `IStateFlow` | Successor map: `NextAfter(current, scope)`. `null` means stay. |
| `GameEngine` | Queue, drain loop and the `Evaluate` hook. |
| `PushStateCommand` / `PushStateHandler` | The transition command and its four-step lifecycle. |
| `StateService` | Holds `Current`. `Push(next)` enqueues a `PushStateCommand`. |
| `StateRegistry` | Maps state type to a byte kind. Kinds 0 to 9 are framework, 10 and up are game. |
| `StatePushEvent` | Wire record with `NewKind` and `PrevKind` bytes. |
| `ICommandScope` / `IStateScope` | Handler scope (read `World`, `Enqueue`, `Emit`). State scope is read-only. |
| `MatchSingleton` | Turn, active player and outcome. |

## Shapes worth keeping
State behavior is pure functions of the world. The record holds nothing.
```csharp
public sealed record CombatDamageState : GameState
{
    public override IReadOnlyList<GameAction> OnEnter(IStateScope s)
    {
        var attackers = s.World.With(MtgTags.IsAttacking).ToList();
        var cmds = new List<GameAction>(attackers.Count);
        foreach (var atk in attackers)
            cmds.Add(new DealDamageCommand(atk.Id, atk.Get(MtgTags.Power)));
        return cmds;
    }
    ...
}
```
The `Evaluate` hook turns completion into an ordinary queued command, and skips itself after a push so a transition cannot recurse.
```csharp
private void Evaluate(GameAction action, CommandResult result)
{
    if (action is PushStateCommand) return;
    var current = State.Current;
    var scope = new StateScope(World, current);
    if (!current.IsCompleted(action, result, scope)) return;
    var next = Flow.NextAfter(current, scope);
    if (next == null) return;
    _queue.Enqueue(new PushStateCommand(next));
}
```

## Relation to the Framework
- Informs `Framework.States`. The Framework kept the zero-data `GameState`, the baseline states (`MatchNotStartedState`, `PlayingState`, `MatchEndedState`) and a single `StatePushEvent`. That event carries one `int Slot` resolved through `GameStateRegistry`, not the spike's byte pair.
- The Framework does not have the behavior surface yet (`Actions`, `OnEnter`, `OnExit`, `IsCompleted`, `IStateFlow`, `PushStateCommand`, `Evaluate`). It belongs to roadmap phase 1, which replaces the synchronous `CommandService.Execute` with an `Enqueue` loop.
- To fit, `PushStateHandler` must call `StateService.Push<TState>()` and let `StatePushHandler` write `StateStore`. The spike writes `Current` directly.
- Spike handlers write `Entity` tags and `MatchSingleton` in place. In the Framework they must call `TagService` (or a match store's service) so each change is a wire event.
- The spike's `PrevKind` gave a cheap undo. The Framework has no unapply yet (`WireEventHandler.Unapply` throws).

## Limits
- Zero-input deadlock. A state with no input and empty `OnEnter` never completes, because `IsCompleted` only runs after a command. `CombatDamage_WithZeroAttackers_AutoCompletes` pins this.
- Nothing enforces `GameState.Actions`. Only the IMGUI sandbox reads it, and any command is accepted in any state.
- `Entity` stores boxed `object` values in a `Dictionary`, and `World` is a flat list with linear `With` queries.
- No game-over path. The flow maps `MatchEndedState` to `null`, and nothing pushes it.
