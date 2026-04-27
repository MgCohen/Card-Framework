# Action Flow Snippets

This document sketches a possible end-to-end flow in code-like snippets. It is not an implementation plan. The goal is to make the proposed payloads, results, services, handlers, state changes, history envelope, and animation handoff easier to see.

Scenario:

> Player drags a healing card from `Hand` to `PlayArea`. The game accepts the request. `**PlayCardCommand**` only performs placement: hand → play area, with semantics for that step. After the pipeline finishes, the **evaluator** reads state, sees a card sitting in `PlayArea`, and returns an `ExecuteCardCommand` for the engine to enqueue next. The execute step runs the card’s effect (here: heal) and moves the card to **discard** if that is what this game does after resolve. State updates and separate envelopes (one per command) drive UI. This sample ignores failure ordering and fine-grained “finished” vs “resolved” naming.

## 1. Definitions

The card definition has baseline variables, visual metadata, and one simple effect entry point.

```csharp
public sealed class CardDefinition
{
    public DefinitionId Id { get; init; }
    public string DisplayName { get; init; } = "";
    public VariableBag BaseVariables { get; init; } = new();
    public VisualDefinitionId Visual { get; init; }
    public IEffect Effect { get; init; } = EmptyEffect.Instance;
}

public sealed class HealFiveEffect : IEffect
{
    public void Trigger(EffectContext context)
    {
        context.Dispatch(new HealActorCommand(
            actor: context.Actor,
            source: context.SourceInstance,
            target: context.Actor,
            amount: 5));
    }
}
```

## 2. UI Detects Drag

The UI does not move canonical state. It creates a request.

```csharp
public sealed class CardDragController
{
    private readonly IIntentGateway requests;

    public void OnDrop(CardView cardView, ZoneView targetZone, PointerDropContext pointer)
    {
        var request = new MoveInstanceToZoneRequest
        {
            Actor = cardView.ViewerActor,
            SourceInstance = cardView.InstanceId,
            FromZone = cardView.CurrentZoneId,
            ToZone = targetZone.ZoneId,
            Input = pointer
        };

        requests.Submit(request);
    }
}
```

Payload:

```csharp
public sealed record MoveInstanceToZoneRequest
{
    public required ActorId Actor { get; init; }
    public required InstanceId SourceInstance { get; init; }
    public required ZoneId FromZone { get; init; }
    public required ZoneId ToZone { get; init; }
    public PointerDropContext? Input { get; init; }
}
```

## 3. Engine Receives PlayerIntent

The gateway resolves actor/session/instance, optionally checks available actions, converts the intent into a game command, and hands it to the engine via `Dispatch`. It returns an `IntentResult` immediately — either a synchronous rejection (before the engine) or an acceptance with a `IntentId` the caller can use to match the future envelope.

```csharp
public sealed class IntentGateway : IIntentGateway
{
    private readonly IntentMapper actionMatcher;
    private readonly GameEngine engine;

    public IntentResult Submit(MoveInstanceToZoneRequest request)
    {
        var match = actionMatcher.TryMatch(request);
        if (match.RequiresMatch && !match.Success)
            return new IntentResult { Status = IntentStatus.Rejected, Reason = RejectReason.NotAvailableAction };

        var commandChainId = CommandChainId.New();

        // Game-specific interpretation: dragging from hand to play area means play card.
        GameCommand command = (match.CommandOverride
            ?? new PlayCardCommand(
                actor: request.Actor,
                source: request.SourceInstance,
                fromZone: request.FromZone,
                playZone: request.ToZone))
            with { OriginCommandId = commandChainId, OriginIntent = request };

        engine.Dispatch(command);
        return new IntentResult { Status = IntentStatus.Accepted, IntentId = commandChainId };
    }
}
```

`IntentResult.Accepted` does **not** mean the command succeeded — it means the engine received it. The actual outcome arrives via the published `HistoryEnvelope` whose `Cause.OriginCommandId` matches.

```csharp
public sealed record IntentResult
{
    public required IntentStatus Status { get; init; }
    public RejectReason? Reason { get; init; }
    public IntentId? IntentId { get; init; }
}
```

## 4. Game Command Enters Pipeline

`ICommandPipeline` opens an envelope, runs pre-hooks, invokes the handler, and publishes the envelope. It handles exactly one command and returns a result. It does not decide what runs next — that is the evaluator's job (see section 6b).

```csharp
public sealed class CommandPipeline : ICommandPipeline
{
    private readonly IHistoryService history;

    public CommandResult Execute(GameCommand command)
    {
        using var envelope = history.OpenEnvelope(new EnvelopeCause
        {
            Actor = command.Actor,
            SourceInstance = command.Source,
            OriginCommandId = command.OriginCommandId,
            OriginIntent = command.OriginIntent  // non-null only on the first command in a chain
        });

        var before = triggerSystem.RunBefore(command, context);
        if (before.IsCancelled)
        {
            var rejected = CommandResult.Rejected(before.Reason);
            envelope.Add(new CommandRejectedEntry(command, rejected));
            return history.CloseAndPublish(envelope, rejected);
        }

        if (before.IsReplaced)
        {
            history.CloseAndPublish(envelope, CommandResult.Replaced);
            engine.Dispatch(before.Replacement);
            return CommandResult.Replaced;
        }

        var result = Handle(before.Command, envelope);

        triggerSystem.RunAfter(before.Command, result, context);

        return history.CloseAndPublish(envelope, result);
    }
}
```

Payload:

```csharp
public readonly record struct GameActionId;

// Base for all pipeline-dispatchable types (GameAction, CompositeAction, GameSignal).
public abstract record GameCommand;

// Executable, modifiable, queued through the engine.
public abstract record GameAction : GameCommand
{
    public required ActorId Actor { get; init; }
    public required InstanceId Source { get; init; }
    public CommandChainId? OriginCommandId { get; init; }
    public PlayerIntent? OriginIntent { get; init; }  // set only on gateway-originated commands; carried by the first envelope for correlation
}

public sealed record PlayCardCommand : GameAction
{
    public required ZoneId FromZone { get; init; }
    public required ZoneId PlayZone { get; init; }

    public PlayCardCommand(ActorId actor, InstanceId source, ZoneId fromZone, ZoneId playZone)
    {
        Actor = actor;
        Source = source;
        FromZone = fromZone;
        PlayZone = playZone;
    }
}

// Second step: run definition effect and this game’s post-play moves (e.g. to discard).
// Created by the evaluator after it sees a card in PlayArea — not by PlayCardHandler or the pipeline.
public sealed record ExecuteCardCommand : GameAction
{
    public required ZoneId FromPlayZone { get; init; }
    public required ZoneId DiscardZone { get; init; }
}
```

## 5. Before Triggers Validate Or Modify

`OnBefore<T>` triggers run in sequence before the handler. Each receives the current command and returns the (potentially modified) command. Returning a cancelled marker or a different `GameAction` type stops the fold — the original action does not execute.

Game-rule validation (turn order, legality) registers as before triggers rather than as hardcoded pre-hook classes:

```csharp
// Registered by the game rules component at startup, not per-instance.
triggerSystem.OnBefore<PlayCardCommand>(
    condition: (cmd, ctx) => !ctx.Rules.ActorCanAct(cmd.Actor),
    react:     (cmd, ctx) => TriggerResult<PlayCardCommand>.Cancel(RejectReason.NotYourTurn));

triggerSystem.OnBefore<PlayCardCommand>(
    condition: (cmd, ctx) => !ctx.Rules.CardCanBePlayed(cmd.Actor, cmd.Source),
    react:     (cmd, ctx) => TriggerResult<PlayCardCommand>.Cancel(RejectReason.CardCannotBePlayed));
```

Per-instance effects (card abilities that modify or prevent actions) register the same way but with instance-scoped conditions and are unregistered when the instance leaves play. See [21-trigger-reaction-system.md](21-trigger-reaction-system.md) for the full trigger model.

## 6. Play Card Handler (Placement Only)

`PlayCardHandler` only performs **putting the card into the play area** and the semantics for that **step**. It does **not** run the card’s `Effect` and does **not** move the card to discard. Inserts use each zone’s default placement.

```csharp
public sealed class PlayCardHandler : ICommandHandler<PlayCardCommand, PlayCardResult>
{
    public PlayCardResult Handle(PlayCardCommand command, ResolutionScope scope)
    {
        scope.Emit(new CardPlayStartedSignal(
            actor: command.Actor,
            card: command.Source,
            fromZone: command.FromZone,
            playZone: command.PlayZone));

        scope.Apply(new RemoveFromZoneMutation(command.FromZone, command.Source));
        scope.Apply(new InsertIntoZoneMutation(command.PlayZone, command.Source));

        scope.Emit(new CardMovedSignal(
            card: command.Source,
            fromZone: command.FromZone,
            toZone: command.PlayZone));

        scope.Emit(new PlayCardResolvedSignal(command.Actor, command.Source, command.PlayZone));

        return new PlayCardResult
        {
            Status = CommandStatus.Success,
            PlayedCard = command.Source
        };
    }
}
```

**Note:** This handler is intentionally granular. Deeper design (composites, segment boundaries, and two-command stories) is in [Handler Mutations, Sequential Apply, And Semantic Events](19-handler-mutations-and-semantics.md).

## 6b. Evaluator: “PlayArea Has A Card” → `ExecuteCardCommand`

After the pipeline returns, the engine calls `GameRules.Evaluate`. The evaluator reads the **current game state** — not which command just ran — and returns any follow-up commands to enqueue. This is where game-specific chaining logic lives, not inside the pipeline.

In this sample game the rule is: any card sitting in `PlayArea` must be executed. The evaluator does not match on `PlayCardCommand`; it just checks the zone. Any command that leaves a card in `PlayArea` (a trigger, a copy effect, etc.) produces the same follow-up automatically.

```csharp
public sealed class SampleGameEvaluator : GameRules
{
    public IReadOnlyList<GameCommand> Evaluate(
        GameCommand justExecuted,
        CommandResult result,
        GameState state)
    {
        // State-based check: is there a card in PlayArea waiting to be executed?
        var playArea = state.GetZone(WellKnownZones.PlayArea);
        if (playArea.HasCards)
        {
            var card = playArea.TopCard;
            return new GameCommand[]
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

        return Array.Empty<GameCommand>();
    }

    public void OnQueueDrained(GameEngine engine, GameState state)
    {
        // Queue is empty and PlayArea is empty.
        // Player still has cards in hand and energy — do nothing; await input.
    }
}
```

The evaluator is also called when the queue empties (`OnQueueDrained`). That is when the game decides to advance a phase, grant priority, or simply await the next player action. See [20-engine-evaluation-loop.md](20-engine-evaluation-loop.md) for the full `GameEngine` drain loop and evaluator interface.

## 6c. Execute Card Handler (Effect, Then Discard In This Game)

A **separate** handler for `**ExecuteCardCommand`**: runs the definition’s `Effect` (here: heal via a further child command) and, for this sample, removes from the play area and inserts into `DiscardZone`.

```csharp
public sealed class ExecuteCardHandler : ICommandHandler<ExecuteCardCommand, ExecuteCardResult>
{
    public ExecuteCardResult Handle(ExecuteCardCommand command, ResolutionScope scope)
    {
        var card = scope.GetInstance(command.Source);
        var definition = scope.GetDefinition(card.DefinitionId);

        definition.Effect.Trigger(new EffectContext(
            actor: command.Actor,
            sourceInstance: command.Source,
            scope: scope));

        scope.Apply(new RemoveFromZoneMutation(command.FromPlayZone, command.Source));
        scope.Apply(new InsertIntoZoneMutation(command.DiscardZone, command.Source));

        return new ExecuteCardResult
        {
            Status = CommandStatus.Success,
            ExecutedInstance = command.Source
        };
    }
}
```

## 7. Effect Dispatches Heal Command

The effect is simple for now. `Trigger` lives under `**ExecuteCardHandler**`, not under `PlayCardHandler`. The heal path dispatches a child `GameCommand`.

```csharp
public readonly struct EffectContext
{
    public EffectContext(ActorId actor, InstanceId sourceInstance, ResolutionScope scope)
    {
        Actor = actor;
        SourceInstance = sourceInstance;
        Scope = scope;
    }

    public ActorId Actor { get; }
    public InstanceId SourceInstance { get; }
    public ResolutionScope Scope { get; }

    public CommandResult Dispatch(GameCommand command)
    {
        return Scope.DispatchChild(command);
    }
}
```

## 8. Heal Handler Changes Player Life

The heal command is rules-level because prevention, modifiers, caps, or triggers may exist later.

```csharp
public sealed record HealActorCommand : GameAction
{
    public required ActorId Target { get; init; }
    public required int Amount { get; init; }

    public HealActorCommand(ActorId actor, InstanceId source, ActorId target, int amount)
    {
        Actor = actor;
        Source = source;
        Target = target;
        Amount = amount;
    }
}

public sealed class HealActorHandler : ICommandHandler<HealActorCommand, HealResult>
{
    public HealResult Handle(HealActorCommand command, ResolutionScope scope)
    {
        var current = scope.GetVariable<int>(command.Target, "life");
        var max = scope.GetVariable<int>(command.Target, "max_life");
        var next = Math.Min(current + command.Amount, max);
        var actual = next - current;

        scope.Emit(new ActorHealedSignal(
            source: command.Source,
            target: command.Target,
            amount: actual));

        scope.Apply(new SetVariableMutation<int>(
            instance: command.Target,
            key: "life",
            value: next));

        return new HealResult
        {
            Status = CommandStatus.Success,
            Target = command.Target,
            RequestedAmount = command.Amount,
            ActualAmount = actual
        };
    }
}
```

## 9. State Scope Applies Mutations And Records Changes

The state backend might be immutable Store, mutable tracked entities, or a hybrid. The handler does not care.

```csharp
public sealed class ResolutionScope
{
    private readonly IStateBackend state;
    private readonly HistoryEnvelopeBuilder history;
    private readonly IGameCommandDispatcher dispatcher;

    public void Apply(StateMutation mutation)
    {
        StateChange change = state.Apply(mutation);
        history.Add(change);
    }

    public void Semantic(SemanticEvent semanticEvent)
    {
        history.Add(semanticEvent);
    }

    public CommandResult DispatchChild(GameCommand command)
    {
        return dispatcher.DispatchChild(command, this);
    }
}
```

Mutation and change:

```csharp
public abstract record StateMutation;

public sealed record SetVariableMutation<T>(
    InstanceId instance,
    string key,
    T value) : StateMutation;

public abstract record StateChange : HistoryEntry;

public sealed record VariableChanged<T>(
    InstanceId instance,
    string key,
    T oldValue,
    T newValue) : StateChange;
```

## 10. Envelope Captures Semantic Events And State Changes

The envelope is the thing observers consume.

```csharp
public sealed class HistoryEnvelopeBuilder
{
    private readonly List<HistoryEntry> entries = new();

    public EnvelopeCause Cause { get; init; }

    public void Add(HistoryEntry entry)
    {
        entries.Add(entry);
    }

    public HistoryEnvelope Build(CommandResult result)
    {
        return new HistoryEnvelope
        {
            Cause = Cause,
            Result = result,
            Entries = entries.ToArray()
        };
    }
}

public sealed record HistoryEnvelope
{
    public required EnvelopeCause Cause { get; init; }
    public required CommandResult Result { get; init; }
    public required IReadOnlyList<HistoryEntry> Entries { get; init; }
}
```

Each engine queue step produces its own envelope. `PlayCardCommand` and `ExecuteCardCommand` are separate pipeline executions, so they produce separate envelopes. Both carry the same `OriginCommandId` — the engine threads it from the initial command onto all evaluator follow-ups. Only the first envelope also carries `OriginIntent`, the original player request that started the chain.

Child commands dispatched inline via `scope.DispatchChild` (e.g. `HealActorCommand` inside `ExecuteCardHandler`) are **not** separate queue steps. Their entries fold into the current envelope, not a new one.

```text
// Envelope 1: PlayCardCommand
HistoryEnvelope
  Cause:
    originCommandId: C1
    originRequest: MoveInstanceToZoneRequest(C17, Hand -> PlayArea)  // first command only
    actor: P1
    source: C17
  Result: PlayCardResult { Status: Success }
  Entries:
    CardPlayStarted(P1, C17)
    ZoneContentRemoved(P1.Hand, C17)
    ZoneContentInserted(P1.PlayArea, C17, index: 0)
    CardMoved(C17, P1.Hand, P1.PlayArea)
    PlayCardResolved(P1, C17, P1.PlayArea)

// Envelope 2: ExecuteCardCommand (evaluator follow-up; engine threads OriginCommandId = C1)
HistoryEnvelope
  Cause:
    originCommandId: C1
    originRequest: null  // follow-up; no originating request
    actor: P1
    source: C17
  Result: ExecuteCardResult { Status: Success }
  Entries:
    // HealActorCommand is a child dispatch inside ExecuteCardHandler — same envelope
    ActorHealed(C17, P1, amount: 5)
    VariableChanged(P1, "life", 10, 15)
    ZoneContentRemoved(P1.PlayArea, C17)
    ZoneContentInserted(P1.Discard, C17, index: 0)
```

## 11. UI Consumes Envelope

The UI reads semantic events for animation intent and state changes for final data.

```csharp
public sealed class CardAnimationEnvelopePlayer
{
    private readonly ICardVisualRegistry visuals;
    private readonly IViewModelUpdater viewModels;

    public async Task Play(HistoryEnvelope envelope)
    {
        foreach (var entry in envelope.Entries)
        {
            switch (entry)
            {
                case CardPlayStartedSignal e:
                    await visuals.Card(e.Card).Play("play_start");
                    break;

                case CardMovedSignal e:
                    await visuals.Card(e.Card).MoveFromTo(e.FromZone, e.ToZone);
                    break;

                case PlayCardResolvedSignal e:
                    await visuals.Card(e.Card).MarkPlacedInPlay();
                    break;

                case ActorHealedSignal e:
                    await visuals.Actor(e.Target).PlayNumberPopup($"+{e.Amount}");
                    break;

                case VariableChanged<int> e when e.Key == "life":
                    viewModels.SetVariable(e.Instance, e.Key, e.NewValue);
                    break;
            }
        }
    }
}
```

Notes:

- The state may already be updated internally.
- UI gets the envelope at a boundary and can sequence animation.
- UI can wait for animation markers before visually applying some entries if the presentation system wants that.
- Network clients can receive filtered versions of the same envelope.

## 12. Full Example Flow Summary

```text
UI drop
  -> MoveInstanceToZoneRequest
  -> IntentGateway.Submit
      -> available action check passes
      -> commandChainId = C1
      -> engine.Dispatch(PlayCardCommand { OriginCommandId: C1, OriginIntent: MoveInstanceToZoneRequest })
      -> return IntentResult { Accepted, IntentId: C1 }   <-- caller gets this immediately

Engine dequeues PlayCardCommand { OriginCommandId: C1 }:
  -> ICommandPipeline.Execute
      -> CommandPreHooks
      -> PlayCardHandler
          -> Semantic: CardPlayStarted
          -> StateMutation: RemoveFromZone(Hand, C17)
          -> StateMutation: InsertIntoZone(PlayArea, C17)
          -> Semantic: CardMoved
          -> Semantic: PlayCardResolved
      -> Envelope 1 published { OriginCommandId: C1, OriginIntent: MoveInstanceToZone, Result: Success }
  -> GameRules.Evaluate(PlayCardCommand, result, state)
      -> state.GetZone(PlayArea).HasCards == true
      -> returns [ExecuteCardCommand { Source: C17 }]
  -> Engine threads C1 onto follow-up; enqueues ExecuteCardCommand { OriginCommandId: C1 }

Engine dequeues ExecuteCardCommand { OriginCommandId: C1 }:
  -> ICommandPipeline.Execute
      -> ExecuteCardHandler
          -> definition.Effect.Trigger
              -> HealActorCommand dispatched as child (scope.DispatchChild — same envelope)
              -> HealActorHandler
                  -> Semantic: ActorHealed
                  -> StateMutation: SetVariable(P1, "life", 15)
                  -> StateChange: VariableChanged(P1, "life", 10, 15)
          -> StateMutation: RemoveFromZone(PlayArea, C17)
          -> StateMutation: InsertIntoZone(Discard, C17)
      -> Envelope 2 published { OriginCommandId: C1, OriginIntent: null, Result: Success }
  -> GameRules.Evaluate(ExecuteCardCommand, result, state)
      -> state.GetZone(PlayArea).HasCards == false
      -> returns []

Queue empty -> OnQueueDrained:
  -> player still has energy and cards in hand -> no action, await input

UI receives Envelope 1 (C1): animate card placement
UI receives Envelope 2 (C1): animate heal popup + discard move
```

