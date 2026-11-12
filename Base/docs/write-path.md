# Write path

[Back to the index](README.md)

A gameplay handler reads the World through its call and writes it only with primitives. The Ecs primitive handlers are the only World writers, and each accepted primitive goes to the `WireLog`.

## A match

```csharp
using IHost host = VsaBuilder.Create().UseGenericHost().Build();
using IServiceScope match = host.Services.GetRequiredService<MatchFactory>().Create();   // one scope, one World
ICommandSender commands = match.ServiceProvider.GetRequiredService<ICommandSender>();
IWorldReader reader = match.ServiceProvider.GetRequiredService<IWorldReader>();

EntityId card = (await commands.SendAsync(new CreateCardCommand(Zone.Hand), ct)).Value;
await commands.SendAsync(new MoveCardCommand(card, Zone.Board), ct);
await commands.SendAsync(new ChangeResourceCommand<Health>(card, 5), ct);

int health = reader.Get<Health>(reader.ById(card)).Value;                        // 5
WireLog log = match.ServiceProvider.GetRequiredService<WireLog>();               // 5 entries
```

| Type | Gives |
|---|---|
| `MatchFactory.Create()` | A new scope with a new World bound to it |
| `ICommandSender` of the scope | Sends into the match: `envelope.Services` is the match scope |
| `IWorldReader` of the scope | Read access to the match World |
| `WireLog`, `GroupService` of the scope | The log and the groups of the match |
| `match.Dispose()` | Ends the scope and its World: each reader and writer then throws `ObjectDisposedException` |

## One command

```text
commands.SendAsync(MoveCardCommand)                             envelope.Services = match scope
 └ default command pipeline
    ├ MoveCardValidator      reads IWorldReader from envelope.Services; an error rejects the move
    └ MoveCardHandler        ICommandSender of the match scope
       └ SendAsync(ChangeComponent<CardZone>)
          └ IPrimitive pipeline (Wire)
             ├ CreateEntityId                    CreateEntity only: a default id gets the next id
             ├ ChangeComponentHandler<CardZone>  match.Writer() → World.Set; no change gives Unchanged
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
| Game driver | The match scope | Reads `IWorldReader`; sends commands |
| Gameplay handler, validator | `envelope.Services` | Reads `IWorldReader`; sends primitives |
| Ecs and Wire tests | Internal `MatchWorld.World` | Set a state before a test sends primitives |
| Primitive handler (Ecs) | `MatchWorld` | Writes through the internal `match.Writer()` |
| Code with no match scope | The app services | None: reader and primitives throw |

## Bypass routes

| Route | Result | Stopped by |
|---|---|---|
| A constructor asks for `World` or `IWorldWriter` | Resolve fails | Container |
| `GetService<IWorldWriter>()` in a match scope | `null` | Container |
| `match.Writer()` or `new WorldWriter(...)` outside Ecs | Does not compile | Compiler |
| `new ChangeComponentHandler<T>(match)` outside Ecs | A World write with no log entry | Review |
| Cast of the reader to `IWorldWriter` | `InvalidCastException` | Separate reader object (B4) |
| Reader, writer, World or `MatchWorld` in a field outside Ecs | Gate fails | B5 |
| Hook on a primitive outside Ecs and Wire | Gate fails | B6 |
| `AddPipeline` for a primitive type in a module | Takes the primitives from Wire | Review |
| `IWorldFactory.Create()` or `MatchFactory.Create()` in a handler | A separate World | Not a risk to the match |
| Primitive or reader in a scope with no match | `InvalidOperationException` | `MatchWorld` |
| `WireLog.Append` from a module | A false log entry; no World write | Review |

## Schema load

Friflo reads the loaded assemblies when it makes the first World. A key assembly that loads after that fails with `TypeInitializationException`. `Build()` loads each key assembly through the module scan, before a match exists. `Schema.Tests` checks this in its own process.

## Non-goals

- No write member on `Entity` or on the reader. Reason: one place writes.
- No gameplay hook on a primitive. Reason: gameplay reacts to its own commands.
