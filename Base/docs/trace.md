# Trace

[Back to the index](README.md)

The `play` command runs a fixed card play in one `Match` and prints each message of the run. Each line starts with a tag. The indent shows the parent message.

```powershell
cd Base
dotnet run --project App/Cli -- play
```

## What the command does

| Step | Messages |
|---|---|
| Set-up, not traced | Two cards in `Hand` (cost 3 and 9), one player entity, `+10` mana |
| Play 1 | `PlayCardCommand(card 1)`: accepted |
| Play 2 | `PlayCardCommand(card 1)` again: `Cards.NotInHand` |
| Play 3 | `PlayCardCommand(card 2)`: `Cards.NotEnoughMana` |

The command prints the legend, a `[WORLD]` line, the trace and a second `[WORLD]` line. Friflo writes one `Assemblies loaded` line first, when it makes the World.

## Output

```text
[WORLD]   card 1: zone=Hand cost=3 | card 2: zone=Hand cost=9 | player 3: mana=10
[SEND]    PlayCardCommand                msg=14             payload={ Card: 1, Player: 3 }
  [SEND]    MoveCardCommand              msg=15  parent=14  payload={ Card: 1, To: Board }
    [SEND]    ChangeComponent<CardZone>  msg=16  parent=15  payload={ Entity: 1, Value: Board }
    [RESULT]  ChangeComponent<CardZone>  msg=16  Ok(Board)
    [WIRE]    ChangeComponent<CardZone>  msg=16  entry 11
  [RESULT]  MoveCardCommand              msg=15  Ok
  [SEND]    ChangeResourceCommand        msg=17  parent=14  payload={ Target: 3, Delta: -3, Resource: Mana }
    [SEND]    ChangeComponent<Mana>      msg=18  parent=17  payload={ Entity: 3, Value: 7 }
    [RESULT]  ChangeComponent<Mana>      msg=18  Ok(7)
    [WIRE]    ChangeComponent<Mana>      msg=18  entry 12
  [RESULT]  ChangeResourceCommand        msg=17  Ok(7)
[RESULT]  PlayCardCommand                msg=14  Ok
[REJECT]  PlayCardCommand                msg=19             payload={ Card: 1, Player: 3 }  Error(Cards.NotInHand)
[REJECT]  PlayCardCommand                msg=20             payload={ Card: 2, Player: 3 }  Error(Cards.NotEnoughMana)
[WORLD]   card 1: zone=Board cost=3 | card 2: zone=Hand cost=9 | player 3: mana=7
```

| Tag | Written by | Meaning |
|---|---|---|
| `[SEND]` | `TraceBefore` | The message passed its validators. Its handler starts |
| `[REJECT]` | `TraceAfter` | A validator or a Before event stopped the message |
| `[RESULT]` | `TraceAfter` | The value that the handler returned |
| `[FAULT]` | `TraceAfter` | The handler or a hook threw an exception |
| `[WIRE]` | `WireLog.Appended` → `TraceLog.Wire` | `RecordStep` recorded an accepted primitive |
| `[WORLD]` | `PlayRun` | World values that `IWorldReader` reads |

`msg` is `Envelope.Id`. `parent` is `Envelope.Parent.Id`.

## How the hooks see a run

```text
SendAsync ─▶ Validate ─▶ before steps ─▶ Before events ─▶ Handle ─▶ After events ─▶ after steps
               │ reject                    TraceBefore (last)        TraceAfter (first)   RecordStep
               └──────────────────────────────────────────────────▶ [REJECT]             [WIRE]
```

- `TraceBefore : Before<IMessage>` has `Order = int.MaxValue`. When it runs, each validator and each before step passed. So `[SEND]` also means "the handler starts".
- A rejected message runs no Before event. `TraceAfter` writes its only line, `[REJECT]`.
- `TraceAfter : After<IMessage>` has `Order = int.MinValue`. `RecordStep` is an after step, so `[WIRE]` comes after the `[RESULT]` of its primitive.
- Both hooks only read: each returns the same message and never rejects. `TraceHookTests` checks it, and B6 lets `Trace` hook a primitive.

## TraceLog

`TraceLog` is scoped: one for each `Match`. Each host has the Trace plugin, because VSA.Sdk gives each module root each plugin root. A `TraceLog` records nothing until a caller calls `Start()`. Only `PlayRun` calls it.

```csharp
TraceLog trace = match.Services.GetRequiredService<TraceLog>();
WireLog log = match.Services.GetRequiredService<WireLog>();
log.Appended += entry => trace.Wire(log.Entries.Count, entry);
trace.Start();
// send messages
IEnumerable<string> lines = TraceText.Lines(trace.Events);
```

## Bypass routes

| Route | Trace shows |
|---|---|
| `GroupService` appends `GroupStart` or `GroupEnd` | `[WIRE]` with `msg=-` |
| A direct handler call, not on the bus | Nothing |
| A Before event with `Order = int.MaxValue` and a later type name rejects | `[SEND]`, then `[REJECT]` |
| `MoveCardCommand` or `ChangeResourceCommand` sent alone | Traced. The `PlayCard` checks do not run |
| A `TraceLog` with no `Start()` call | Nothing |
