# Engine

Gives server code one handle to every service, read-only store view, the event stream and the network.

**Code:** `Assets/Engine/` · assembly `Framework.Engine` · depends on `Framework.Wire`, `Framework.Tags`, `Framework.Entities`, `Framework.Requests`, `Framework.States`, `Framework.Commands`

## What it does
- Defines one class, `GameEngine`, in `Assets/Engine/Runtime/GameEngine.cs`.
- Exposes the services: `Entities`, `Tags`, `Requests`, `States`, `Commands`.
- Exposes the stores only through `IReadOnly*` views: `EntityStore`, `TagStore`, `RequestStore`, `StateStore`.
- Exposes `Stream` (the `EventStream`) and `Network`.
- Rule ARCH-07 checks that `GameEngine` exposes only these member types.

## How it works
1. Server: `EngineInstaller` registers `GameEngine` as a singleton. The client never registers it.
2. VContainer builds it by constructor injection from the singletons the other installers registered.
3. Server code resolves `GameEngine` and authors through its services, for example `engine.Commands.Execute(cmd)`.
4. The engine adds no behaviour. Every call goes straight to the service, which emits into the shared `EventStream`.

Client code resolves the stores and `EventStream` it needs directly from the container.

## Key types
| Type | Job |
|---|---|
| `GameEngine` | Aggregate of references. Built once per server scope. |

## Example
From `Assets/Bootstrap/Tests/GameplayLayerTests.cs`:

```csharp
_scope = BuildScope(isServer: true, "ServerScope");

var engine   = _scope.Container.Resolve<GameEngine>();
var stream   = _scope.Container.Resolve<EventStream>();
var commands = _scope.Container.Resolve<CommandService>();

Assert.AreSame(stream,   engine.Stream);
Assert.AreSame(commands, engine.Commands);
```

## Gaps
- `GameEngine` is only a bag of references. It has no game loop, no pump, no turn order and no stack.
- `Stream` exposes the concrete `EventStream`, whose `Emit` is public. Any holder of the engine can emit events and skip the services. Tests use this to inject responses.
- The constructor takes eleven parameters. Each new feature adds to it.
- Engine has no tests of its own. Rule ARCH-08 lists it as an exception. `GameplayLayerTests` and the integration tests cover it.
