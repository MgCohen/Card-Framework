# Bootstrap

Composes the Framework features into one VContainer scope, as a server or as a client.

**Code:** `Assets/Bootstrap/` · assembly `Framework.Bootstrap` · depends on `Framework.Core`, `Framework.Commands`, `Framework.Engine`, `Framework.Entities`, `Framework.Requests`, `Framework.States`, `Framework.Tags`, `Framework.Wire`, `Scaffold.AppFlow`, `VContainer`

## What it does
- `FrameworkAppFlowRoot` is the scene entry point. It reads a serialized `_isServer` flag and yields one `GameplayLayer`.
- `GameplayLayer` runs six installers into the same scope: Engine, Tag, Entity, Request, State, Command.
- Each installer registers stores and apply handlers on both roles, and services only on the server.
- `HandlerRegistrationExtensions` gives games `RegisterHandler<T>`, `RegisterCommandHandler<T>`, `RegisterCommandValidator<T>` and `RegisterTagValueType<T>`.
- Registered handlers are collected by list injection into `HandlerRegistry`, `CommandHandlerRegistry` and `CommandValidatorRegistry`.

## How it works
```text
FrameworkAppFlowRoot (MonoBehaviour, execution order -1000)
  └─ GameplayLayer(isServer, transport = null)
       EngineInstaller   both: ITransport (NullTransport if none), Network, EventStream, HandlerRegistry
                         server: GameEngine, WireForwarder    client: NetworkInboundBridge
       TagInstaller      both: TagStore, TagRegistry, tag handlers (int, bool)   server: TagService
       EntityInstaller   both: EntityStore, EntityTypeRegistry, EntityCreatedHandler   server: EntityService
       RequestInstaller  both: RequestStore   server: RequestService
       StateInstaller    both: StateStore, GameStateRegistry, StatePushHandler   server: StateService
       CommandInstaller  server only: BlockScope, both registries, CommandService
  build callbacks: resolve HandlerRegistry, WireRegistrar.RegisterAll(network),
                   then resolve WireForwarder (server) or NetworkInboundBridge (client)
```

The build callbacks matter because these classes wire themselves in their constructors. Resolving them subscribes them to the stream or network.

## Key types
| Type | Job |
|---|---|
| `FrameworkAppFlowRoot` | Scene root. Picks server or client. |
| `BootstrapLayer` | Abstract `IScopeLayer` that carries `isServer`. |
| `GameplayLayer` | The one layer. Runs the six installers flat. |
| `IInstaller` | One feature's registrations. Never its own AppFlow layer. |
| `EngineInstaller` | Wire substrate, transport, and the server or client bridge. |
| `TagInstaller`, `EntityInstaller`, `RequestInstaller`, `StateInstaller`, `CommandInstaller` | Per-feature registrations. |
| `HandlerRegistrationExtensions` | Registration helpers for game code. |

## Example
From `Assets/Testing/Support/MatchHarness.cs`:

```csharp
match._scope = LifetimeScope.Create(builder =>
{
    new GameplayLayer(isServer: true).Install(builder);
    bindScenario(builder);
}, "AcceptanceMatchScope");
if (match._scope.Container == null) match._scope.Build();
match.Engine = match._scope.Container.Resolve<GameEngine>();
```

## Gaps
- `FrameworkAppFlowRoot` passes no transport, so a scene always gets `NullTransport`. Only tests pass a real one.
- `FrameworkAppFlowRoot` has no extension point for game handlers, commands or validators. Tests add them by installing `GameplayLayer` into their own builder.
- Bootstrap does not use Multiplayer. The server or client role comes from a serialized bool, not from a `Session`.
- Tag value types are fixed to `int` and `bool`, in both `TagInstaller` and `WireRegistrar`.
- `BootstrapLayer` has one subclass, `GameplayLayer`.
