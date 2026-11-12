# IL2CPP probe: the Base stack in a Unity player

This probe examines the Base stack (VSA, Friflo, MemoryPack, Wire, Peers) in a Unity IL2CPP player. The branch is a record. It is not merged.

Unity `6000.6.6f1`, Windows x64 player, VSA `0.1.0-preview.14`, Friflo.Engine.ECS `3.6.0`, MemoryPack `1.21.4`. Base at `main` commit `33c183b`.

## Result

| Check | Mono, stripping Disabled | IL2CPP Minimal, no `link.xml` | IL2CPP Minimal + `link.xml` | IL2CPP High + `link.xml` |
|---|---|---|---|---|
| `friflo.world` | PASS | PASS | PASS | PASS |
| `vsa.compose` | PASS | FAIL | PASS | PASS |
| `memorypack.struct-key` | PASS | FAIL | PASS | PASS |
| `memorypack.reference-key` | PASS | FAIL | PASS | PASS |
| `vsa.closed-handlers` | PASS | FAIL | PASS | PASS |
| `done-check.server-client` | PASS | FAIL | PASS | PASS |

Output of IL2CPP High with `link.xml`:

```text
backend il2cpp
PASS friflo.world -> x=4 tag=true count after delete=0
PASS vsa.compose -> catalog types=19 hash=adba6b09a0fa637f
PASS memorypack.struct-key -> ChangeComponent { Entity = EntityId { Value = 3 }, Value = BaseProbe.Heat } (10 bytes)
PASS memorypack.reference-key -> ChangeComponent { Entity = EntityId { Value = 3 }, Value = BaseProbe.Label } (17 bytes)
PASS vsa.closed-handlers -> Health=5 Mana=2 Heat=9 Label=probe
PASS done-check.server-client -> entities=1 log entries=5 zone=Board health=5
RESULT PASS
```

The catalog hash is the same on Mono and IL2CPP. The wire ids are stable across the two backends.

## Checks

| Check | What it does |
|---|---|
| `friflo.world` | Friflo directly: create an entity with a component, add a tag, read both, delete the entity |
| `vsa.compose` | `new ServiceCollection().AddVsa()` finds the Base modules; `MatchFactory.Create()` gives a reader |
| `memorypack.struct-key` | `ChangeComponent<Heat>` goes through `WireCatalog.Write` and `Read` |
| `memorypack.reference-key` | Same, for `[MemoryPackable] partial struct Label` with a `string` field |
| `vsa.closed-handlers` | The module scan closes `ChangeResourceCommand<Health>`, `ChangeResourceCommand<Mana>`, `ChangeComponent<Heat>` and `ChangeComponent<Label>`; each handler runs |
| `done-check.server-client` | One server and one client over `InProcessTransport`: make, move and change a card; the two Worlds and the two logs are equal |

## Findings

1. **Base does not build for Unity as it is.** Base targets `net10.0`. A `netstandard2.1` build needs these changes (`base-netstandard21.patch`):

   | File | Change |
   |---|---|
   | `Directory.Build.props` | `TargetFramework` `netstandard2.1`; `PolySharp` for `IsExternalInit` (VSA.Sdk refuses a linked `Compile` item) |
   | `Plugins/Ecs/Domain/World.cs` | `ArrayBufferWriter.ResetWrittenCount()` → `Clear()` |
   | `Plugins/TypeKeys/Domain/TypeKeyRegistry.cs` | `ArgumentNullException.ThrowIfNull` → explicit `if` and `throw` |
   | `Plugins/Peers/Handlers/Commands/MatchQueue.cs` | `TaskCompletionSource` → `TaskCompletionSource<bool>` |

2. **The linker removes the module root assemblies, also at Minimal.** No code references `Ecs.dll`, `Wire.dll`, `Peers.dll`, `Network.dll`, `TypeKeys.dll`, `Cards.dll` or `Resources.dll` directly. `AppScan` then finds no modules. In the VSA probe, the modules were asmdefs, so Minimal kept them. Precompiled DLLs do not get this protection. A `link.xml` that keeps the VSA and Base assemblies makes Minimal and High pass.
3. **Friflo and MemoryPack need no `link.xml` entry.** They pass at High without one.
4. **Issue #246 does not block Base.** Base has no `unmanaged` constraint. The keys use `struct`, and the `struct` check works in IL2CPP (VSA probe, finding 3).
5. **A .NET 10 host cannot run the `netstandard2.1` build.** It loads the `net8` builds of MemoryPack.Core and Friflo, which have other interfaces (static abstract members). Unity loads only the `netstandard2.1` builds, so this is not a Unity risk. For Base tests, keep `net10.0`, or multi-target.

## Risks for later phases

| Phase | Risk | Proposal |
|---|---|---|
| M1 | The vendor or the Unity entry of vsa-template must produce `netstandard2.1` DLLs, or compile Base as source with asmdefs | Ask vsa-template which path is the standard. Multi-target Base: `net10.0` for tests, `netstandard2.1` for Unity |
| M1 | The API changes above can return when new code uses .NET 6+ APIs | Add the `netstandard2.1` build to the Base gate |
| M1, N1 | A Unity build without `link.xml` finds no modules, and it fails at run time, not at build time | The vendor tool writes a `link.xml` for each vendored module assembly |

## Run

1. Apply `base-netstandard21.patch` to a copy of `Base/`. Put `dotnet/` next to it as `Probe/`.
2. `dotnet build Probe/Probe.csproj -c Release -p:CopyLocalLockFileAssemblies=true -o dlls` (needs `GITHUB_TOKEN` for the VSA feed).
3. Make a Unity project from `MgCohen/vsa-consumer-unity` branch `spike/il2cpp-generics`. Delete its sample modules. Copy each DLL that is not in `Assets/Plugins/VSA` to `Assets/Plugins/Base`. Copy `unity/` to `Assets/Probe/` (the runner files to `Assets/Probe/Runner/`).
4. Build and run:

```bash
Unity.exe -batchmode -nographics -quit -projectPath . -executeMethod ProbeEditor.ProbeBuild.Build -probeBackend il2cpp -probeStripping High -probeBuild Builds/il2cpp-High
Builds/il2cpp-High/Probe.exe -batchmode -nographics -probeOutput probe.txt
```
