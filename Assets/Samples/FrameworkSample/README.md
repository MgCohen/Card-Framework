# Framework sample: play a card

This sample uses the five framework systems in one "play a card" flow.
A server plays a card from Hand to Board. A client gets the same blocks over a loopback wire.

| System | Sample types |
|---|---|
| RecordIds | `CardId`, `Card`, `CardStore` |
| TypeKeys | `Zone` (`Zone.Hand`, `Zone.Board`), `ZoneRegistry` |
| Pipeline | `GameMessage`, `PlayCard`, `PlayCardValidator`, `PlayCardHandler`, `GameScope`, `GamePipeline` |
| TypeRouting | routes `PlayCard` to its validator and handler |
| Blocks | `CardMoved`, inside a `GroupService` group |

## Install calls

```csharp
server.AddBlocks();
server.AddTypeRouting(_ => { });
server.AddIdSpace<CardId>(Role.Server);
server.AddTypeKeys(o => o.Validate = true);
server.AddKeySpace<Zone, ZoneRegistry>();
server.AddPipeline<GamePipeline, GameMessage, GameScope>();

client.AddBlocks(o => o.Role = Role.Client);
client.AddKeySpace<Zone, ZoneRegistry>();
```

Each side also registers its `EventStream` (as `IEventEmitter`) from `Wire.Connect()`. The server also registers `CardStore`.

## Call flow

```
pipeline.Add(PlayCard) -> Run
  PlayCardValidator   card not in Hand -> Rejected, no blocks
  GamePipeline.CreateScope -> GameScope
  PlayCardHandler -> Groups.Create { scope.Move(card, Board) }
    GroupStart, CardMoved(From, To), GroupEnd -> server EventStream -> LoopbackTransport -> client EventStream
```

## Run it

Run `powershell -NoProfile -File Tools/Rulebook/gate.ps1`. The tests are in `Tests/PlayCardTests.cs` (assembly `Framework.Samples.FrameworkSample.Tests`).
