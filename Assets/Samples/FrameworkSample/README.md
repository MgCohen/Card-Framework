# Framework sample: play a card

This sample uses the five framework systems in one "play a card" flow.
A server plays a card from Hand to Board. A client gets the same blocks over a loopback wire.

| System | Sample types |
|---|---|
| RecordIds | `CardId`, `Card`, `CardStore` |
| TypeKeys | `Zone` (`Zone.Hand`, `Zone.Board`), `ZoneRegistry` |
| Pipeline | `GameMessage`, `PlayCard`, `PlayCardValidator`, `PlayCardHandler`, `GameScope`, `GameView`, `GamePipeline` |
| TypeRouting | routes `PlayCard` to its validator and handler |
| Blocks | `CardMoved`, inside a `GroupService` group |

## Install calls

```csharp
var wire = Framework.Wire.Wire.Connect();

server.RegisterInstance(wire.ServerStream).AsSelf().As<IEventEmitter>();
server.RegisterInstance(wire.ServerNetwork);
server.Register<CardStore>(Lifetime.Singleton);
server.AddBlocks();
server.AddIdSpace<CardId>(Role.Server);
server.AddTypeKeys(o => o.Validate = true);
server.AddKeySpace<Zone, ZoneRegistry>();
server.AddPipeline<GamePipeline, GameMessage, GameScope, GameView>();

client.RegisterInstance(wire.ClientStream).AsSelf().As<IEventEmitter>();
client.RegisterInstance(wire.ClientNetwork);
client.AddBlocks(o => o.Role = Role.Client);
client.AddKeySpace<Zone, ZoneRegistry>();
```

| Install call | Registers |
|---|---|
| `AddBlocks` | `BlockPositions`, `GroupService` (server), each `Block` type on the `Network` |
| `AddPipeline` | the handler router, the validator router, `PipelineServices`, `GamePipeline` |
| `AddIdSpace` | `IdAllocator<CardId>` |
| `AddKeySpace` | `ZoneRegistry` |

The sample does not call `AddTypeRouting`. `AddPipeline` registers its own routers (`RouteMode.Single`).
The sample does not register block types on the `Network`. `AddBlocks` does it.

## Handler and validator

```csharp
public sealed class PlayCardValidator : Validator<GameMessage, PlayCard, GameView>
{
    protected override bool Validate(PlayCard message, GameView view, out string reason)
    {
        reason = view.TryGetZone(message.Card, out var zone) && zone is Zone.Hand
            ? null
            : $"Card {message.Card.Value} is not in Hand.";
        return reason == null;
    }
}

public sealed class PlayCardHandler : Handler<GameMessage, PlayCard, GameScope>
{
    protected override Task<object> Handle(PlayCard message, GameScope scope)
    {
        scope.Groups.Create(() => scope.Move(message.Card, new Zone.Board()));
        return Task.FromResult<object>(null);
    }
}
```

- The message parameter has the concrete type, so the code does not cast.
- `GameView` has only read methods, so a validator cannot change the game state.
- `GamePipeline` takes one `PipelineServices` and its own dependencies.

## Call flow

```
pipeline.Add(PlayCard) -> Run
  GamePipeline.CreateView -> GameView
  PlayCardValidator(PlayCard, GameView)   card not in Hand -> Rejected, no blocks
  GamePipeline.CreateScope -> GameScope
  PlayCardHandler(PlayCard, GameScope) -> Groups.Create(() => scope.Move(card, Board))
    GroupStart, CardMoved(From, To), GroupEnd -> server EventStream -> LoopbackTransport -> client EventStream
```

## Run it

Run `powershell -NoProfile -File Tools/Rulebook/gate.ps1`. The tests are in `Tests/PlayCardTests.cs` (assembly `Framework.Samples.FrameworkSample.Tests`).
