# Layout

[Back to the index](README.md)

`App/Host` is the only composition root. Plugins hold the base systems, and modules hold gameplay slices. Each plugin and module registers only its own services, in its root class.

```text
Source/
├─ App/Host/            Program.cs: VsaBuilder.Create(args).UseWebHost().Build().RunAsync()
├─ Plugins/             base systems, one folder for each plugin
│  ├─ TypeKeys/         TypeKey, one frozen registry for each key root
│  ├─ Ecs/              Domain: World, MatchWorld · Infrastructure: reader, writer, factories · six primitives · the only Friflo user
│  ├─ Wire/             WireLog, WireCatalog, GroupService, RecordStep, WireHello · MemoryPack
│  ├─ Network/          ITransport, InProcessTransport
│  └─ Peers/            PeerRole, WireServer, WireClient, Ping · joins Wire and Network
├─ Modules/             gameplay, one folder for each module
│  ├─ Cards/            CreateCard, MoveCard · keys Card, CardZone, Zone
│  └─ Resources/        ChangeResource<T> · keys Health, Mana
└─ Tests/               one dotnet test project for each area
```

## Plugins and modules

| Part | Root class | Layer folders | Example |
|---|---|---|---|
| Plugin | `Plugin` | `Domain/`, `Infrastructure/`, `Contracts/Commands/`, `Handlers/Commands/` | `Plugins/Ecs` |
| Module | `Module` | `Domain/Shared/`, `Slices/<Feature>/` | `Modules/Cards` |

```text
Modules/Cards/
├─ Cards.csproj, CardsModule.cs        root: registers nothing; Card and CardZone are keys through [TypeArgument]
├─ Projects/                           Domain.Shared, Domain, Infrastructure, Contracts.Commands, Handlers.Commands
├─ Domain/Shared/                      Card, CardZone, Zone: keys that other modules can read
└─ Slices/
   ├─ CreateCard/                      CreateCardCommand, CreateCardHandler
   └─ MoveCard/                        MoveCardCommand, MoveCardHandler, MoveCardValidator, CardErrors
```

- A key is a struct in `Domain/Shared/`: `IValue<T>`, `IComponent` or `ITag`. `IComponent` and `ITag` have `[TypeArgument]`, so the module scan finds each key. A module registers no keys.
- A module that reads the World in a handler has a `Domain` project and an `Infrastructure` project, both with no files.
- A command can be generic on one key: `ChangeResourceCommand<T>`. The module scan closes its open handler for each key that meets the constraints of `T`. An open handler with two or more generic parameters stops the host.

## Tests

| Project | Area |
|---|---|
| `Cards.Tests` | Acceptance test, slices, the first module validator |
| `Ecs.Tests` | World, match scope, Friflo boundary (B1 to B5) |
| `Wire.Tests` | Primitives, log, catalog, groups, frames into a client bus, B6 |
| `Peers.Tests` | Server and clients over `InProcessTransport`, hello, client faults, client messages, disconnect |
| `Schema.Tests` | Key assemblies load in `Build()`; own process |
| `App.Tests`, `TypeKeys.Tests`, `Network.Tests`, `Smoke.Tests` | Host, keys, transport, packages |
| `Testing` | Not tests: `RecordingTransport`, `Peer`, `PeerHost` |

## Non-goals

- No data keys from content. Keys are C# struct types. Reason: the compiler checks each key access, the catalog ids and the hash come from type names, and Friflo needs a struct type for each component. A key from content needs a dynamic store and its own delta.
