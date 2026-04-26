# Action Flow Snippets

This document sketches a possible end-to-end flow in code-like snippets. It is not an implementation plan. The goal is to make the proposed payloads, results, services, handlers, state changes, history envelope, and animation handoff easier to see.

Scenario:

> Player drags a healing card from `Hand` to `PlayArea`. The game accepts the request. The card moves zones. Because this game interprets hand-to-play-area as "play this card," the engine executes the card. The card heals the player for 5. State updates immediately inside the engine. UI receives a grouped envelope and animates the card move plus heal.

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
    private readonly IRequestGateway requests;

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

## 3. Engine Receives Request

The engine resolves actor/session/instance, optionally checks available actions, and converts the request into a game command.

```csharp
public sealed class RequestGateway : IRequestGateway
{
    private readonly IAvailableActionMatcher actionMatcher;
    private readonly IGameCommandDispatcher commands;

    public RequestResult Submit(MoveInstanceToZoneRequest request)
    {
        var match = actionMatcher.TryMatch(request);
        if (match.RequiresMatch && !match.Success)
        {
            return commands.RejectRequest(request, RejectReason.NotAvailableAction);
        }

        // Game-specific interpretation: dragging from hand to play area means play card.
        GameCommand command = match.CommandOverride
            ?? new PlayCardCommand(
                actor: request.Actor,
                source: request.SourceInstance,
                fromZone: request.FromZone,
                playZone: request.ToZone);

        return commands.DispatchRequest(request, command);
    }
}
```

Result:

```csharp
public sealed record RequestResult
{
    public required RequestStatus Status { get; init; }
    public RejectReason? Reason { get; init; }
    public HistoryEnvelopeId? EnvelopeId { get; init; }
}
```

## 4. Game Command Enters Pipeline

The dispatcher opens an envelope, runs pre-hooks, invokes the handler, runs reactions, and publishes the envelope.

```csharp
public sealed class GameCommandDispatcher : IGameCommandDispatcher
{
    private readonly ICommandPipeline pipeline;
    private readonly IHistoryService history;

    public RequestResult DispatchRequest(Request request, GameCommand command)
    {
        using var envelope = history.OpenEnvelope(new EnvelopeCause
        {
            Request = request,
            Actor = command.Actor,
            SourceInstance = command.Source
        });

        var pre = pipeline.RunPreHooks(command, envelope);
        if (pre.Status != PreHookStatus.Continue)
        {
            var rejected = CommandResult.Rejected(pre.Reason);
            envelope.Add(new CommandRejectedEntry(command, rejected));
            return history.CloseAndPublish(envelope, rejected);
        }

        var result = pipeline.Handle(pre.Command, envelope);
        pipeline.RunReactions(pre.Command, result, envelope);

        return history.CloseAndPublish(envelope, result);
    }
}
```

Payload:

```csharp
public abstract record GameCommand
{
    public required ActorId Actor { get; init; }
    public required InstanceId Source { get; init; }
}

public sealed record PlayCardCommand : GameCommand
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
```

## 5. Pre-Hooks Validate Or Modify

Pre-hooks can reject, cancel, replace, or modify the command before state changes.

```csharp
public sealed class CanPlayCardPreHook : ICommandPreHook<PlayCardCommand>
{
    public PreHookResult<PlayCardCommand> Evaluate(PlayCardCommand command, ResolutionScope scope)
    {
        if (!scope.Rules.ActorCanAct(command.Actor))
        {
            return PreHookResult<PlayCardCommand>.Reject(RejectReason.NotYourTurn);
        }

        if (!scope.Rules.CardCanBePlayed(command.Actor, command.Source))
        {
            return PreHookResult<PlayCardCommand>.Reject(RejectReason.CardCannotBePlayed);
        }

        return PreHookResult<PlayCardCommand>.Continue(command);
    }
}
```

## 6. Handler Resolves Play Card

The handler is where the game command turns into state mutations and semantic events. Inserts use each zone’s default placement (ordering, top/bottom, and so on); explicit insertion plans are for exceptional cases, not the usual path.

```csharp
public sealed class PlayCardHandler : ICommandHandler<PlayCardCommand, PlayCardResult>
{
    public PlayCardResult Handle(PlayCardCommand command, ResolutionScope scope)
    {
        var card = scope.GetInstance(command.Source);
        var definition = scope.GetDefinition(card.DefinitionId);

        scope.Semantic(new CardPlayStartedEvent(
            actor: command.Actor,
            card: command.Source,
            fromZone: command.FromZone,
            playZone: command.PlayZone));

        scope.Apply(new RemoveFromZoneMutation(command.FromZone, command.Source));
        scope.Apply(new InsertIntoZoneMutation(command.PlayZone, command.Source));

        scope.Semantic(new CardMovedEvent(
            card: command.Source,
            fromZone: command.FromZone,
            toZone: command.PlayZone));

        definition.Effect.Trigger(new EffectContext(
            actor: command.Actor,
            sourceInstance: command.Source,
            scope: scope));

        scope.Apply(new RemoveFromZoneMutation(command.PlayZone, command.Source));
        scope.Apply(new InsertIntoZoneMutation(scope.Game.DiscardZoneFor(command.Actor), command.Source));

        scope.Semantic(new CardPlayFinishedEvent(command.Actor, command.Source));

        return new PlayCardResult
        {
            Status = CommandStatus.Success,
            PlayedCard = command.Source
        };
    }
}
```

**Note:** This handler is intentionally granular so each layer is visible. Sequential `scope.Apply` inside one `Handle`, manual `scope.Semantic` next to some applies, and double remove/insert (play area then discard) are discussed as design trade-offs—not as the only way to write handlers—in [Handler Mutations, Sequential Apply, And Semantic Events](19-handler-mutations-and-semantics.md).

## 7. Effect Dispatches Heal Command

The effect is simple for now. It dispatches a child game command.

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
public sealed record HealActorCommand : GameCommand
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

        scope.Semantic(new ActorHealedEvent(
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

Example envelope:

```text
HistoryEnvelope
  Cause:
    request: MoveInstanceToZone(C17, Hand -> PlayArea)
    actor: P1
    source: C17
  Result:
    PlayCardResult(success, C17)
  Entries:
    CardPlayStarted(P1, C17)
    ZoneContentRemoved(P1.Hand, C17)
    ZoneContentInserted(P1.PlayArea, C17, index: 0)
    CardMoved(C17, P1.Hand, P1.PlayArea)
    ActorHealed(C17, P1, amount: 5)
    VariableChanged(P1, "life", 10, 15)
    ZoneContentRemoved(P1.PlayArea, C17)
    ZoneContentInserted(P1.Discard, C17, index: 0)
    CardPlayFinished(P1, C17)
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
                case CardPlayStartedEvent e:
                    await visuals.Card(e.Card).Play("play_start");
                    break;

                case CardMovedEvent e:
                    await visuals.Card(e.Card).MoveFromTo(e.FromZone, e.ToZone);
                    break;

                case ActorHealedEvent e:
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
  -> RequestGateway.Submit
  -> PlayCardCommand
  -> CommandPreHooks
  -> PlayCardHandler
      -> Semantic: CardPlayStarted
      -> StateMutation: RemoveFromZone(Hand, C17)
      -> StateMutation: InsertIntoZone(PlayArea, C17)
      -> Semantic: CardMoved
      -> Effect.Trigger
          -> HealActorCommand
          -> HealActorHandler
              -> Semantic: ActorHealed
              -> StateMutation: SetVariable(P1, "life", 15)
              -> StateChange: VariableChanged(P1, "life", 10, 15)
      -> StateMutation: Move card to discard
      -> Semantic: CardPlayFinished
  -> HistoryEnvelope closes
  -> UI animates card play and heal
```

