# DeckMech Command Pipeline Reference

This note summarizes the relevant command/action pipeline research from the local `C:\Unity\DeckMech` project, especially `C:\Unity\DeckMech\Plans\GameCommandPipeline\GameCommandPipeline-ExecPlan.md`.

It is reference material for the Card Framework action-flow docs. It is not an implementation plan for this repository.

## What Exists In DeckMech Now

DeckMech currently has a useful prototype around timeline-planned actions and command-based movement.

Implemented pieces:

- `GameAction` is an authored/scheduled action object.
- `MoveUnitGameAction` is stateless except for authored parameters like `Range`.
- Timeline slots store `ScheduledEntry(GameAction Action, ImmutableArray<Coord> Targets)`.
- `ActionPlanController` uses an action's `TryAim(...)` to build target choices, then schedules the selected targets.
- `TimelinePlaybackService` advances ticks, gathers scheduled actions for the current tick, asks each action to execute, then dispatches emitted commands.
- `ICommandDispatcher` runs ordered middleware, logs the final command payload, and publishes `CommandPublished`.
- `SimulationOrchestrator` subscribes to `CommandPublished` and calls `store.Execute(payload)`.
- `MoveUnitCommand` is a low-level state command handled by `SetUnitPositionCommandMutator`.

Current movement flow:

```text
TimelinePlaybackService.ResolveCurrentStep
  -> AdvanceTickCommand directly mutates Store
  -> BuildEffectsForCurrentTick
      -> MoveUnitGameAction.Execute(context, targets)
      -> context.Emit(new MoveUnitCommand(...))
  -> foreach emitted command:
      -> ICommandDispatcher.Dispatch(command)
      -> middleware/log/publish
      -> SimulationOrchestrator.OnCommandPublished
      -> store.Execute(command)
```

This works for simple movement, but it is still a fire-and-forget command flow.

## Current Limitations

The latest plan identifies several problems in the current implementation:

- `GameAction.Execute` emits commands into a list; commands apply only after all actions for the tick have run.
- Actions cannot synchronously receive a result from a command.
- Actions cannot branch on outcomes like "was this movement redirected?" or "how much damage hit shield?"
- `SimulationOrchestrator` is a hidden routing bridge from `CommandPublished` to `Store.Execute`.
- `ICommandDispatcher` is doing too much: middleware/logging/observation plus indirect state routing.
- If high-level game commands use the wrong path, they can be logged or published without being handled.
- Collision handling currently sits in `SetUnitPositionCommandMutator` as a silent guard, which prevents illegal overlap but does not produce a meaningful result.

The core lesson: fire-and-forget command publishing is not enough for game logic that needs validation, replacement, cancellation, state reads, or typed results.

## What We Were Discussing

The design conversation separated command-like things into two lanes:


| Lane          | Meaning                                       | Expected Behavior                                                            |
| ------------- | --------------------------------------------- | ---------------------------------------------------------------------------- |
| Game command  | A high-level intent that needs game logic.    | Runs through pre-hooks, handler, post-reactions, and returns a typed result. |
| State command | A low-level, already-approved state mutation. | Goes directly to the state store/scope.                                      |


Examples:

- `MoveActionCommand` is a game command: "try to move this unit to this cell."
- `MoveUnitCommand` is a state command: "apply this resolved delta to unit placement."
- `ApplyDamageCommand` is a game command: "apply damage and calculate shield/HP split."
- `ReduceShieldCommand` / `ReduceHealthCommand` are state commands.

The important architectural split:

- Game commands answer "what should happen?"
- State commands apply "this is the state change we have already decided to make."

## Planned Refactor Direction

The latest `GameCommandPipeline` plan supersedes the older command-handler pipeline.

Target shape:

```text
GameAction.Execute
  -> context.Dispatch(new MoveActionCommand(...))
  -> IGameCommandDispatcher.Dispatch(CommandEnvelope<MoveActionCommand>)
      -> CommandTrigger<MoveActionCommand>[]   // pre-validation/pre-execution
      -> MoveCommandHandler.Handle(...)
          -> scope.Execute(new MoveUnitCommand(...))
          -> maybe scope.Execute(new CollisionOccurredCommand(...))
          -> return MoveResult(...)
      -> CommandReaction<MoveActionCommand, MoveResult>[] // post-result reactions
      -> command log append
      -> return MoveResult to GameAction
```

Planned pieces:

- `IGameCommand<TResult>` marks high-level commands.
- `IStateCommand` marks low-level store mutations.
- `CommandEnvelope<TCommand>` carries command plus source metadata.
- `PipelineSignal<TCommand>` lets pre-hooks cancel or replace a command.
- `CommandTrigger<TCommand>` runs before the handler.
- `ICommandHandler<TCommand, TResult>` is the required handler for a game command.
- `CommandReaction<TCommand, TResult>` runs after the handler and sees the result.
- `ICommandExecutionScope` wraps Store writes and can later support overlays/speculative execution.
- `IGameCommandDispatcher` becomes the only valid entry point for game commands.
- `ICommandDispatcher` is no longer a routing bridge for game commands; at most it remains logging/middleware infrastructure or is retired.
- `SimulationOrchestrator` is deleted; its unrelated roles move to more specific services.

## Key Decisions From DeckMech

Useful decisions to carry into Card Framework research:

- A single command bus is not enough when some commands need typed results.
- Game commands need one enforced entry point.
- Missing handlers should be loud errors.
- Wrong-door dispatch should be loud errors.
- Pre-hooks can cancel or replace commands before execution.
- Post-reactions observe command + result and can dispatch follow-up game commands.
- Handlers should not receive raw Store if a scope abstraction can represent reads/writes.
- State commands should be a lower-level lane and should not re-enter game-command routing.
- Game actions should be able to synchronously branch on results.
- Command metadata belongs in an envelope, not inside the domain command record.
- Registration order is enough for early trigger/reaction ordering.

## Why This Matters For Card Framework

This maps very naturally to card-game needs:

- "Play this card" is a game command.
- "Move this card from hand to stack" is likely a state command after approval.
- "Deal damage" may be a game command because prevention, replacement, shield, armor, triggers, and result data matter.
- "Set variable `health` to N" is a state command.
- "Draw a card" may be a game command if replacement effects, visibility, deck-empty rules, or triggers can intervene.
- "Put this instance into this zone" is not approved by the zone; it is approved by game/action rules, then applied as state.

This also fits the zone correction we just made:

```text
PlayerIntent move card to zone
  -> game/action rules validate
  -> command pipeline may cancel/replace
  -> handler resolves the exact state commands
  -> zone organizes approved contents
  -> result/history/reactions observe outcome
```

## Card Framework Translation

For our framework, we probably want these conceptual layers:


| Concept                | DeckMech Reference                   | Card Framework Direction                                            |
| ---------------------- | ------------------------------------ | ------------------------------------------------------------------- |
| Authored action/effect | `GameAction`                         | Card action, card effect, system action, zone action, actor action. |
| High-level intent      | `IGameCommand<TResult>`              | `GameCommand`, `ActionRequest`, or similar. Needs naming pass.      |
| Low-level mutation     | `IStateCommand`                      | State mutation command/fact. Applies after game rules approve.      |
| Dispatcher             | `IGameCommandDispatcher`             | Single action-flow entry point.                                     |
| Pre-hook               | `CommandTrigger<TCommand>`           | Replacement/prevention/validation/modifier hook.                    |
| Handler                | `ICommandHandler<TCommand, TResult>` | Resolves game command into state changes and result.                |
| Post-hook              | `CommandReaction<TCommand, TResult>` | Trigger/reaction/observer after result.                             |
| Scope                  | `ICommandExecutionScope`             | Read/write state scope, later maybe overlay/speculative.            |
| Result                 | `CommandResult`                      | Outcome object for branching, history, reactions.                   |
| Envelope               | `CommandEnvelope<TCommand>`          | Source/actor/correlation/phase metadata.                            |


## Cautions For Card Framework

Things not to copy blindly:

- DeckMech's terminology is game-specific and timeline-specific.
- `GameCommand` may not be the final name for the Card Framework; "request", "intent", or "action command" may be clearer.
- DeckMech's state commands still rely on Store and Scaffold specifics.
- DeckMech's latest plan is not implemented yet.
- DeckMech's trigger/reaction split is promising, but we still need to define timing, nesting, cancellation, and history semantics.
- The Card Framework may need multiple schedulers, while DeckMech is centered on timeline ticks.

## Recommended Next Research Step

Use DeckMech as the baseline for the next docs:

1. ~~Define the Card Framework action-flow vocabulary.~~ Done. See docs 14–17.
2. ~~Decide whether the high-level unit is called `Intent`, `Request`, `Command`, or something else.~~ **Settled:** `PlayerIntent`. See [Action Definition Proposal](17-action-definition-proposal.md).
3. ~~Define two lanes: game/rules command vs state mutation command.~~ Done. `GameAction` (executable, queued) vs `StateMutation` (already-approved state change). See docs 17, 21.
4. ~~Define pre-handler hooks, handler execution, post-result reactions.~~ Done. `OnBefore<T>`/`OnAfter<T>` trigger folds replace `CommandTrigger`/`CommandReaction`. See docs 21, 23.
5. ~~Define how results become history.~~ Done. `CommandResult` folds into `HistoryEnvelope` via the pipeline. See docs 18, 20.
6. ~~Define how generated choices and zone movement enter the same pipeline.~~ Done. Decisions are `GameAction`s; zone movement is a normal `GameAction`. See docs 17, 22.

The most valuable baseline from DeckMech is the shape:

```text
intent/request -> pre-hooks -> handler -> state mutations -> result -> post-reactions -> history
```

That should be the starting point for the Card Framework action-flow model.