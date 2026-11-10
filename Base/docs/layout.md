# Layout

[Back to the index](README.md)

`App/Host` is the only composition root. Plugins hold the base systems, and modules hold gameplay slices. Each plugin and module registers only its own services, in its root class.

```text
Base/
├─ App/Host/            Program.cs: VsaBuilder.Create(args).UseWebHost().Build().RunAsync()
├─ Plugins/             base systems, one folder for each plugin
│  ├─ TypeKeys/         TypeKey, one frozen registry for each key root
│  ├─ Ecs/              World, reader, writer, Match, six primitives · the only Friflo user
│  ├─ Wire/             WireLog, WireCatalog, GroupService, RecordStep · MemoryPack
│  └─ Network/          ITransport, loopback transport
├─ Modules/             gameplay, one folder for each module
│  ├─ Cards/            CreateCard, MoveCard · keys Card, CardZone, Zone
│  └─ Resources/        ChangeResource · key Health
├─ Tests/               one dotnet test project for each area
└─ docs/                these pages
```

## Plugins and modules

| Part | Root class | Layer folders | Example |
|---|---|---|---|
| Plugin | `Plugin` | `Domain/`, `Contracts/Commands/`, `Handlers/Commands/` | `Plugins/Ecs` |
| Module | `Module` | `Domain/Shared/`, `Slices/<Feature>/` | `Modules/Cards` |

```text
Modules/Cards/
├─ Cards.csproj, CardsModule.cs        root: registers WireTypes.Of(t => t.Component<CardZone>().Tag<Card>())
├─ Projects/                           Domain.Shared, Domain, Contracts.Commands, Handlers.Commands
├─ Domain/Shared/                      Card, CardZone, Zone: keys that other modules can read
└─ Slices/
   ├─ CreateCard/                      CreateCardCommand, CreateCardHandler
   └─ MoveCard/                        MoveCardCommand, MoveCardHandler, MoveCardValidator, CardErrors
```

- A key is an unmanaged struct in `Domain/Shared/`: `IValue<T>`, `IComponent` or `ITag`.
- A module that reads the World in a handler has a `Domain` project, also with no files.
- A command is not generic. An open `CommandHandler<,>` stops the host.

## Tests

| Project | Area |
|---|---|
| `Cards.Tests` | Acceptance test, slices, the first module validator |
| `Ecs.Tests` | World, Match, Friflo boundary (B1 to B5) |
| `Wire.Tests` | Primitives, log, catalog, groups, peers, B6 |
| `Schema.Tests` | Key assemblies load in `Build()`; own process |
| `App.Tests`, `TypeKeys.Tests`, `Network.Tests`, `Smoke.Tests` | Host, keys, transport, packages |
