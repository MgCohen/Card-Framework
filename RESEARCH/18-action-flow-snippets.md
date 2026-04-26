# Action Flow Snippets

This document sketches a possible end-to-end flow in code-like snippets. It is not an implementation plan. The goal is to make the proposed payloads, results, services, handlers, state changes, history envelope, and animation handoff easier to see.

Scenario:

> Player drags a healing card from `Hand` to `PlayArea`. The game accepts the request. `**PlayCardCommand**` only performs placement: hand → play area, with semantics for that step. **Game-specific engine logic** then sees the successful `PlayCardCommand`, decides that “play” implies **execute**, and **creates a second** `ExecuteCardCommand` (linked to the first). The execute step runs the card’s effect (here: heal) and moves the card to **discard** if that is what this game does after resolve. State updates and a grouped envelope (two command steps) drive UI. This sample ignores failure ordering and fine-grained “finished” vs “resolved” naming.

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
        // Reactions (including game-specific “what happens after this command”) may dispatch
        // further GameCommands, e.g. PlayCard -> ExecuteCard, in the same session/envelope.
        pipeline.RunReactions(pre.Command, result, envelope);

        return history.CloseAndPublish(envelope, result);
    }
}
```

Payload:

```csharp
public readonly record struct GameCommandId;

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

// Second step: run definition effect and this game’s post-play moves (e.g. to discard).
// Created by game-specific engine/reaction after PlayCardCommand succeeds, not by PlayCardHandler.
public sealed record ExecuteCardCommand : GameCommand
{
    public required GameCommandId CausedByPlayCommandId { get; init; }
    public required ZoneId FromPlayZone { get; init; }
    public required ZoneId DiscardZone { get; init; }
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

## 6. Play Card Handler (Placement Only)

`PlayCardHandler` only performs **putting the card into the play area** and the semantics for that **step**. It does **not** run the card’s `Effect` and does **not** move the card to discard. Inserts use each zone’s default placement.

```csharp
public sealed class PlayCardHandler : ICommandHandler<PlayCardCommand, PlayCardResult>
{
    public PlayCardResult Handle(PlayCardCommand command, ResolutionScope scope)
    {
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

        scope.Semantic(new PlayCardResolvedEvent(command.Actor, command.Source, command.PlayZone));

        return new PlayCardResult
        {
            Status = CommandStatus.Success,
            PlayedCard = command.Source
        };
    }
}
```

**Note:** This handler is intentionally granular. Deeper design (composites, segment boundaries, and two-command stories) is in [Handler Mutations, Sequential Apply, And Semantic Events](19-handler-mutations-and-semantics.md).

## 6b. Engine / Reaction: “Play Succeeded” → `ExecuteCardCommand`

**Undefined, game-specific** logic runs as part of the pipeline after a command is handled (for example, a `CommandReaction` or a pluggable `IGameEngineStep`). It receives the **executed** `PlayCardCommand` and its result, and—because **this** sample game treats “play” as “then execute”—it **creates** a second `GameCommand`. That command is **tied to the first** with `CausedByPlayCommandId` (or another parent/chain field your dispatcher assigns from the play step).

```csharp
// Illustrative: real placement may be a dedicated reaction, an engine pass, or a preset hook.
public sealed class SamplePlayThenExecuteEngine : ICommandReaction<PlayCardCommand, PlayCardResult>
{
    public void React(PlayCardCommand play, PlayCardResult result, ReactionContext context)
    {
        if (result.Status != CommandStatus.Success)
        {
            return;
        }

        // Sample game: after placement, the simulation must “execute” the card (effect, then yard).
        var execute = new ExecuteCardCommand
        {
            CausedByPlayCommandId = context.PlayCommandId,
            Actor = play.Actor,
            Source = play.Source,
            FromPlayZone = play.PlayZone,
            DiscardZone = context.Game.DiscardZoneFor(play.Actor)
        };

        // Same envelope/session, new segment, parent link in context — exact shape is implementation.
        context.DispatchFollowUp(execute);
    }
}
```

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

Example envelope (collapsed **flat** list; a real system would usually group these into **segments** for `PlayCardCommand` then `ExecuteCardCommand`, with `ExecuteCard` carrying `CausedByPlayCommandId`):

```text
HistoryEnvelope
  Cause:
    request: MoveInstanceToZone(C17, Hand -> PlayArea)
    actor: P1
    source: C17
  Result:
    (last command in chain, e.g. ExecuteCardResult, or an aggregate; sample-only)
  Entries:
    # -- segment: PlayCardCommand (placement) --
    CardPlayStarted(P1, C17)
    ZoneContentRemoved(P1.Hand, C17)
    ZoneContentInserted(P1.PlayArea, C17, index: 0)
    CardMoved(C17, P1.Hand, P1.PlayArea)
    PlayCardResolved(P1, C17, P1.PlayArea)
    # -- segment: ExecuteCardCommand (effect + discard in this game), tied to play via CausedByPlayCommandId --
    (child) HealActorCommand / HealActorHandler
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
                case CardPlayStartedEvent e:
                    await visuals.Card(e.Card).Play("play_start");
                    break;

                case CardMovedEvent e:
                    await visuals.Card(e.Card).MoveFromTo(e.FromZone, e.ToZone);
                    break;

                case PlayCardResolvedEvent e:
                    await visuals.Card(e.Card).MarkPlacedInPlay();
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
      -> Semantic: PlayCardResolved
  -> (game-specific engine / reaction, success path)
  -> Creates ExecuteCardCommand, linked: CausedByPlayCommandId
  -> ExecuteCardHandler
      -> definition.Effect.Trigger
          -> HealActorCommand
          -> HealActorHandler
              -> Semantic: ActorHealed
              -> StateMutation: SetVariable(P1, "life", 15)
              -> StateChange: VariableChanged(P1, "life", 10, 15)
      -> StateMutation: RemoveFromZone(PlayArea, C17)
      -> StateMutation: InsertIntoZone(Discard, C17)
  -> HistoryEnvelope closes
  -> UI animates: place in play, then heal + discard, using segment boundaries as needed
```

