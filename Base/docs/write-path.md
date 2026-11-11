# Write path

[Back to the index](README.md)

A gameplay handler reads the World through its call and writes it only with primitives. The Ecs primitive handlers are the only World writers, and each accepted primitive goes to the `WireLog`.

## A match

```csharp
using IHost host = VsaBuilder.Create().UseGenericHost().Build();
using Match match = new Match(host.Services);            // one scope, one World

EntityId card = (await match.Commands.SendAsync(new CreateCardCommand(Zone.Hand), ct)).Value;
await match.Commands.SendAsync(new MoveCardCommand(card, Zone.Board), ct);
await match.Commands.SendAsync(new ChangeResourceCommand<Health>(card, 5), ct);

int health = match.Reader.Get<Health>(match.Reader.ById(card)).Value;   // 5
WireLog log = match.Services.GetRequiredService<WireLog>();             // 5 entries
```

| Member | Gives |
|---|---|
| `match.Commands` | `ICommandSender` of the match scope |
| `match.Reader` | Read access to the match World |
| `match.Services` | The match scope: `IWorldReader`, `WireLog`, `GroupService` |
| `match.Dispose()` | Ends the scope |

## One command

```text
match.Commands.SendAsync(MoveCardCommand)                       envelope.Services = match scope
 └ default command pipeline
    ├ MoveCardValidator      reads IWorldReader from envelope.Services; an error rejects the move
    └ MoveCardHandler        ICommandSender of the match scope
       └ SendAsync(ChangeComponent<CardZone>)
          └ IPrimitive pipeline (Wire)
             ├ CreateEntityId                    CreateEntity only: a default id gets the next id
             ├ ChangeComponentHandler<CardZone>  MatchWorld.Writer → World.Set; no change gives Unchanged
             └ RecordStep                        accepted, no error → WireLog.Append
```

```csharp
public override async Task<Result<int>> HandleAsync(Envelope<ChangeResourceCommand<T>> envelope)
{
    IWorldReader world = envelope.Services.GetRequiredService<IWorldReader>();   // a local, not a field
    ...
    T next = new T { Value = current + change.Delta };                         // T: Health, Mana, ...
    Result<T> written = await commands.SendAsync(new ChangeComponent<T>(target, next), ct);
}
```

## Access

| Code | Gets | Access |
|---|---|---|
| Game driver | `Match` | Reads `match.Reader`; sends commands |
| Gameplay handler, validator | `envelope.Services` | Reads `IWorldReader`; sends primitives |
| Ecs and Wire tests | Internal `MatchWorld.World` | Set a state before a test sends primitives |
| Primitive handler (Ecs) | `MatchWorld` | Writes through the internal `Writer` |
| Code with no match scope | The app services | None: reader and primitives throw |

## Bypass routes

| Route | Result | Stopped by |
|---|---|---|
| A constructor asks for `World` or `IWorldWriter` | Resolve fails | Container |
| `GetService<IWorldWriter>()` in a match scope | `null` | Container |
| `MatchWorld.Writer` outside Ecs | Does not compile | Compiler |
| Cast of the reader to `IWorldWriter` | `InvalidCastException` | Separate reader object (B4) |
| Reader, writer, World or `MatchWorld` in a field outside Ecs | Gate fails | B5 |
| Hook on a primitive outside Ecs and Wire | Gate fails | B6 |
| `AddPipeline` for a primitive type in a module | Takes the primitives from Wire | Review |
| `IWorldFactory.Create()` or `new Match(...)` in a handler | A separate World | Not a risk to the match |
| Primitive or reader in a scope with no match | `InvalidOperationException` | `MatchWorld` |
| `WireLog.Append` from a module | A false log entry; no World write | Review |

## Schema load

Friflo reads the loaded assemblies when it makes the first World. A key assembly that loads after that fails with `TypeInitializationException`. `Build()` loads each key assembly through the module scan, before a `Match` exists. `Schema.Tests` checks this in its own process.

## Non-goals

- No write member on `Entity` or on the reader. Reason: one place writes.
- No gameplay hook on a primitive. Reason: gameplay reacts to its own commands.
