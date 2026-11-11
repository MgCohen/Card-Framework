# Layout

[Back to the index](README.md)

`App/Host` is the only composition root. Plugins hold the base systems, and modules hold gameplay slices. Each plugin and module registers only its own services, in its root class.

```text
Base/
├─ App/Host/            Program.cs: VsaBuilder.Create(args).UseWebHost().Build().RunAsync()
├─ Plugins/             base systems, one folder for each plugin
│  ├─ TypeKeys/         TypeKey, one frozen registry for each key root
│  ├─ Ecs/              Domain: World, MatchWorld · Infrastructure: reader, writer, factories · six primitives · the only Friflo user
│  ├─ Wire/             WireLog, WireCatalog, GroupService, RecordStep · MemoryPack
│  └─ Network/          ITransport, loopback transport
├─ Modules/             gameplay, one folder for each module
│  ├─ Cards/            CreateCard, MoveCard · keys Card, CardZone, Zone
│  └─ Resources/        ChangeResource<T> · keys Health, Mana
├─ Tests/               one dotnet test project for each area
└─ docs/                these pages
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

- A key is an unmanaged struct in `Domain/Shared/`: `IValue<T>`, `IComponent` or `ITag`. `IComponent` and `ITag` have `[TypeArgument]`, so the module scan finds each key. A module registers no keys.
- A module that reads the World in a handler has a `Domain` project and an `Infrastructure` project, both with no files.
- A command can be generic on one key: `ChangeResourceCommand<T>`. The module scan closes its open handler for each key that meets the constraints of `T`. An open handler with two or more generic parameters stops the host.

## Tests

| Project | Area |
|---|---|
| `Cards.Tests` | Acceptance test, slices, the first module validator |
| `Ecs.Tests` | World, match scope, Friflo boundary (B1 to B5) |
| `Wire.Tests` | Primitives, log, catalog, groups, peers, B6 |
| `Schema.Tests` | Key assemblies load in `Build()`; own process |
| `App.Tests`, `TypeKeys.Tests`, `Network.Tests`, `Smoke.Tests` | Host, keys, transport, packages |
