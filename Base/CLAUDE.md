# Base/CLAUDE.md

`Base/` is a .NET stack in the VSA shape. It runs with no Unity. These rules apply to all work under `Base/`. The root `CLAUDE.md` git rules also apply.

Plan page: https://claude.ai/artifact/69oMeCjF3AU12Pc8ZneiXC. It is the source of truth for the layout, the decisions and the phases.

## Scope

- Work only in `Base/`. Do not change `Assets/`, `Packages/`, `ProjectSettings/`, `Tools/` or `docs/`.
- If you need current code from `Assets/`, copy it into `Base/`. Never change the source.

## Base gate

Run the gate before you claim done. It is the authority.

| Machine | Command |
|---|---|
| Windows | `& <core:secrets>/scripts/with-secrets.ps1 powershell -NoProfile -File Base/gate.ps1` |
| Linux, cloud | `<core:secrets>/scripts/with-secrets.sh pwsh Base/gate.ps1` |

```text
gate.ps1 (runs from Base/, so dotnet reads Base/global.json)
  1. solution  each *.csproj under Base/ is in Base.slnx
  2. restore   dotnet restore Base.slnx                          needs GITHUB_TOKEN for the VSA feed
  3. build     dotnet build -c Release --no-restore -warnaserror
  4. test      dotnet test --solution Base.slnx -c Release --no-build
→ PASS / FAIL for each step, exit 0 or 1
```

- `<core:secrets>` is the folder of the `core:secrets` skill. Load the skill to find it.
- `GITHUB_TOKEN` comes from the 1Password vault `agent` through the `core:secrets` wrapper. It needs the scope `read:packages`.
- Do not weaken a step or edit `gate.ps1` to make it pass.
- Do not set these in a project, props or config file: `NoWarn`, `TreatWarningsAsErrors` false, `WarningsNotAsErrors`, `MSBuildWarningsNotAsErrors`, `LangVersion`, `severity = none`.
- Do not add a deeper `Directory.Build.props` or `Directory.Build.targets`.
- Do not add `[Ignore]` or `[Explicit]` to a test.

## Tools

`dotnet-tools.json` pins `vsa.cli`. Run `dotnet tool restore` one time, then `dotnet vsa add module <Name>` or `dotnet vsa add plugin <Name>`.

To move to the latest VSA preview, run `dotnet tool update vsa.cli --local --prerelease`, then `dotnet vsa update`. Then delete each `bin`, `obj`, `bin~` and `obj~` folder.

## Packages

| Package | Version | Feed |
|---|---|---|
| VSA.Sdk and `VSA.*` | `0.1.0-preview.11` | GitHub Packages, owner `MgCohen` |
| Friflo.Engine.ECS | `3.6.0` | nuget.org |
| MemoryPack | `1.21.4` | nuget.org |
| NUnit, NUnit3TestAdapter | `4.6.1`, `6.3.1` | nuget.org |
| Microsoft.AspNetCore.Mvc.Testing | `10.0.14` | nuget.org (`App.Tests.csproj` only) |

- `nuget.config` maps `VSA.*` to GitHub Packages and all other packages to nuget.org.
- Do not add a package source in a project file.
- If you need a package that is not in this table, ask the orchestrator.

## Phase loop

```text
1 Brief → 2 Spec → Approval 1 → 3 Tests (red) → Approval 2 → 4 Code → 5 Gate → 6 Review → 7 PR + note
```

- Spec: API first. Give the entry points, the method shapes, the call sites and the bypass routes.
- Approval 1 and approval 2: stop, and send the spec or the tests to the orchestrator.
- Review: an independent reviewer agent examines the VSA rules, the code standards, the spec, the plan goal and the alignment with the other phases.
- If the review finds a conflict with another phase, send it to the orchestrator. Do not fix the other phase.

## Stacked PRs

| Phase | Cut from | PR into |
|---|---|---|
| P0 | `base` | `base` |
| P1 to P6 | The previous phase branch | The previous phase branch |
| U1 | `p6-wire` | `p6-wire` |
| P7 and later | `u1-unify`, then the previous phase branch | The branch it was cut from |

If a review changes a parent branch, rebase the child branch on it.

On Windows, the branch `base` and the folder `Base/` have the same name. Write `git diff base --`, not `git diff base`.

## VSA shape

```text
Base/
├─ App/Host/          the composition root: Program.cs, one VsaBuilder call
├─ Plugins/<Name>/    plugin modules: the base systems (TypeKeys, Ecs, Network, Wire)
├─ Modules/<Name>/    modules: the Cards and Resources sample
├─ docs/              base docs: layout, write path, peers
└─ Tests/             dotnet test projects; App.Tests references the host
```

The host is not a module. A module registers only its own services, in its root class. All composition is in `App/Host/Program.cs`, as in the VSA Notes sample.

```csharp
await VsaBuilder.Create(args).UseWebHost().Build().RunAsync();
```

```text
VsaBuilder.Build()                   VSA.Composition
  ├─ AppScan.Modules()               plugins first, then modules by name
  ├─ InstallLayers                   pipelines, event, command and request handlers, IBus, presenters
  ├─ WebVsaHost.Install              web host only: one HTTP route for each request
  └─ Module.Configure                each plugin, then each module
```

`Module.Configure` registers on `ModuleBuilder.Services`, type `IServiceCollection`. A factory gets `IServiceProvider`.

```csharp
public sealed class EcsPlugin : Plugin
{
    public override void Configure(ModuleBuilder module)
    {
        module.Services.AddSingleton<IWorldFactory, WorldFactory>();
    }
}
```

- The root class of a plugin module inherits `Plugin` (`VSA1014`). The root class of a module inherits `Module`.
- The module root holds only that class. Do not add `AddX` helpers to it.
- A test with no web host calls `VsaBuilder.Create().UseGenericHost().Modules(new TypeKeysPlugin()).Build()`, then reads `host.Services`.
- The VSA reference is [composition.md](https://github.com/MgCohen/vsa-template/blob/v0.1.0-preview.11/docs/composition.md).
- `App.Tests` sets `VsaArchitectureTests` to false and has its own `ArchitectureTests` class. Reason: the generated class has no body (`;`), and that needs C# 12.
- A test project that references `VSA.Testing.Architecture` sets the same property.
- When the package generates a `{ }` body, delete the property and the local class.

A module has the layout of [slice-layout.md](https://github.com/MgCohen/vsa-template/blob/v0.1.0-preview.11/docs/slice-layout.md): a root project, `Projects/` with one project for each layer, and `Slices/<Feature>/`. A plugin module has the framework layout: one folder for each layer, for example `Handlers/Commands/**` for `<Module>.Handlers.Commands`, and no `Slices/` (VSA0006).

## Handler pipeline

Base uses the VSA handler pipeline ([handler-pipeline.md](https://github.com/MgCohen/vsa-template/blob/v0.1.0-preview.11/docs/handler-pipeline.md)). Base has no pipeline code of its own.

```text
ICommandSender.SendAsync ─▶ Validator<T> ─▶ BeforeStep ─▶ Before<T> ─▶ handler ─▶ After<T> ─▶ AfterStep ─▶ Result<T>
```

- A handler takes `Envelope<T>` and returns `Result<T>`. A send returns `Result<T>`. A rejected message gives `Result<T>.Error`, not an exception.
- `envelope.Services` is the scope of the sender. Handlers, validators and steps resolve from that scope.
- A custom pipeline: `module.Services.AddPipeline(new PipelineBuilder<TMessage>()...)` in `Configure`.
- The scan finds validators and `Before<T>` and `After<T>` only in module layer assemblies, and only closed types.

## Friflo boundary

- Only `Plugins/Ecs` references Friflo.Engine.ECS.
- Other code holds the `Entity` handle and `EntityId`, and reads through `IWorldReader`. `Entity` has no read or write members.
- `Tests/Ecs.Tests` (`FrifloBoundaryTests`) fails the gate if code outside `Plugins/Ecs` uses a Friflo type other than the two markers.
- Only the primitive handlers in `Plugins/Ecs` write the World (next section).
- No service gives a `World` or an `IWorldWriter`. `new Match(host.Services)` makes a scope and its World. The scope gives `IWorldReader`; a scope with no match throws.
- The primitive handlers get the writer through the scoped `MatchWorld`. Its writer member is internal to Ecs.
- A gameplay handler or validator reads with `envelope.Services.GetRequiredService<IWorldReader>()` in the call. It keeps no reader in a field.
- A module that reads the World in a handler has a `Domain` project, also with no files. Without it, the handler cannot see `IWorldReader`.
- Friflo reads the loaded assemblies when it makes the first World. `Build()` loads each key assembly first, through the module scan and `WireTypes.Of` in `Configure`. Make no World before `Build()`.
- No type outside `Plugins/Ecs` keeps a `World`, a reader, a writer or a `MatchWorld` in a field (`FrifloBoundaryTests` B5).

## C# 11

- `Directory.Build.targets` sets `LangVersion` 11, because Unity compiles C# 11. It is in the targets file because VSA.Sdk sets `LangVersion` after the props file.
- C# 11 syntax is permitted.
- Features that need runtime support are forbidden in the layers that Unity uses. An example is `static abstract` interface members.

## Primitives and Wire

Each World change is a primitive command. Send it on `ICommandSender` from the match scope.

```text
bus.SendAsync(new ChangeComponent<Health>(id, value), ct)
 └ pipeline of IPrimitive (Wire)
    ├ CreateEntityId    BeforeStep: a default id gets World.NextId()
    ├ handler (Ecs)     writes the World; no change gives PrimitiveErrors.Unchanged
    └ RecordStep        AfterStep: an accepted primitive with no error goes to WireLog
```

- Primitives: `ChangeComponent<T>`, `RemoveComponent<T>`, `AddTag<T>`, `RemoveTag<T>`, `CreateEntity`, `DeleteEntity`. Each one implements `IPrimitive<TResult>`.
- Primitive handlers derive from `PrimitiveHandler<,>`, not `CommandHandler<,>`. The command scan builds an open `CommandHandler` pair, and that stops the host.
- A module adds its keys: `services.AddSingleton(WireTypes.Of(t => t.Component<Health>().Tag<Marked>()))`. This gives the closed handlers, the MemoryPack types and the catalog ids.
- The match scope gives `IWorldReader`, `WireLog`, `GroupService` and `ICommandSender` (`match.Commands`).
- Do not call `AddPipeline` for a primitive type in a module. That pipeline takes the primitives from Wire, and B6 does not find it.
- `WireCatalog` writes a frame as `[1-byte id][MemoryPack payload]` and reads it back. `WireCatalog.ApplyAsync` sends a received primitive to a bus, and appends a received group entry to the log. Wire does not reference Network (P8 joins them).
- `GroupService` puts `GroupStart` and `GroupEnd` around entries in the log.
- `PrimitiveBoundaryTests` (B6) fails the gate if a type outside Ecs and Wire hooks a primitive.
