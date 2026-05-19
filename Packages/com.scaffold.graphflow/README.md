# Scaffold GraphFlow

Source-generator-driven graph package for Unity GraphToolkit. Authors define payloads; the generator emits editor mirrors, runtime nodes, and the registry. Multiple `[GraphPackage]`-annotated runners (effects, dialogue, AI, …) coexist in one project.

---

## Quickstart — create a new graph package

### 1. Create runtime + editor asmdefs

`Runtime/MyEffects.asmdef`:

```json
{
  "name": "MyGame.Effects",
  "rootNamespace": "MyGame.Effects",
  "references": ["GUID:bab7af28f08a411c9af5ef2f6d191216"],
  "autoReferenced": false
}
```

`Editor/MyEffects.Editor.asmdef`:

```json
{
  "name": "MyGame.Effects.Editor",
  "rootNamespace": "MyGame.Effects",
  "references": [
    "MyGame.Effects",
    "GUID:bab7af28f08a411c9af5ef2f6d191216",
    "GUID:78d83f106afb440f84be7f3da9e310e9",
    "Unity.GraphToolkit.Editor",
    "Unity.GraphToolkit.Common.Editor"
  ],
  "includePlatforms": ["Editor"],
  "autoReferenced": false
}
```

GUIDs: `bab7af28...` = `Scaffold.GraphFlow` (runtime), `78d83f10...` = `Scaffold.GraphFlow.Editor`. The attributes asmdef (`Scaffold.GraphFlow.PackageAttributes`) is `autoReferenced: true` — no explicit reference needed.

The runtime asmdef must reference the generator DLL as a **Roslyn analyzer**: select `Assets/Packages/com.scaffold.graphflow/Generators/Scaffold.GraphFlow.PackageGenerator.dll`, in Inspector check `Editor` only, label `RoslynAnalyzer`, and under `Validate References` add the asmdef GUID under `Run only on assemblies matching`. Editor asmdef gets the same analyzer wiring.

### 2. Subclass `GraphRunner`

```csharp
namespace MyGame.Effects
{
    public sealed class EffectRunner : GraphRunner
    {
        // Long-lived host services live here.
        public IDamageService Damage { get; }
        public EffectRunner(IDamageService damage) => Damage = damage;
    }
}
```

### 3. Hand-write the GraphAsset SO (one-liner)

Required because Unity's `MonoScript` binding for `ScriptableObject` needs an on-disk `.cs` file. The generator can't emit it. The class name must be `<RunnerStem>GraphAsset` — for `EffectRunner` the stem is `Effect`.

```csharp
public sealed class EffectGraphAsset : GraphAsset<EffectRunner> { }
```

### 4. Declare the package

`AssemblyInfo.cs` (in the runtime asmdef folder):

```csharp
using Scaffold.GraphFlow;
using MyGame.Effects;

[assembly: GraphPackage(
    Runner = typeof(EffectRunner),
    Extension = "effect",                  // file extension, no leading dot
    AssetMenu = "MyGame/Effect Graph",     // Project-window create menu path
    Convention = PortConvention.AllFieldsIn,
    RegistryNamespace = "MyGame.Effects.Generated",
    // Mode 2 (optional — see "Mode 2 commands" below):
    DispatcherBase = typeof(EffectCommandDispatcher<,>),
    CommandBase    = typeof(Command<>))]
```

### 5. Reimport. Done.

The generator emits `EffectGraph`, `EffectGraphImporter`, `EffectGraphRegistry`, `EffectCatalog`, `OnTriggerEditorNode`, `ReturnEditorNode`, plus per-payload editor mirrors and runtime nodes. Project menu now has `Assets/Create/MyGame/Effect Graph`.

---

## Authoring payloads

Four payload kinds. Each is a plain class — generator picks them up by interface / attribute / inheritance.

### Entry — `IGraphEntry`

The host's entry point. Carries data the graph reads at run start. Get a `FlowOut` plus output ports per public field.

```csharp
public sealed class OnPlay : IGraphEntry
{
    public int CardId;
    public object? Target;
}
```

Author drops an `OnPlay` node in the graph; runtime calls `controller.Run(new OnPlay { CardId = 7 })`.

### Event — `[GraphEvent]`

Surfaces in the package's `OnTrigger` node picker. The event class is the type; its public fields become OnTrigger output ports.

```csharp
[GraphEvent]
public sealed class DamageDealt
{
    public int Amount;
    public object? Target;
}
```

The host publishes events through whatever bus it uses; the trigger graph subscribes via `OnTrigger<DamageDealt>` (one shared editor node, type chosen via picker).

### Mode-1 action — `IGraphAction<TRunner>` + `IExecutable<TRunner>`

Self-contained action — payload IS its own executor.

```csharp
public sealed class Log : IGraphAction<EffectRunner>, IExecutable<EffectRunner>
{
    public string Message = "";

    public Task Execute(EffectRunner runner)
    {
        Debug.Log(Message);
        return Task.CompletedTask;
    }
}
```

### Mode-2 command — `Command<TResult>` (uses `DispatcherBase`)

For commands that participate in a host pipeline. Define the command base + dispatcher base once per package; subclass the command per concrete action.

**Once per package:**

```csharp
public abstract class Command<TResult>
{
    public abstract Task<TResult> Execute(IEffectScope scope, Flow flow);
}

public abstract class EffectCommandDispatcher<TCmd, TResult> : RuntimeNode<EffectRunner>
    where TCmd : Command<TResult>, new()
{
    public FlowInPort  FlowIn  = null!;
    public FlowOutPort FlowOut = null!;

    protected EffectCommandDispatcher()
    {
        FlowIn  = new FlowInPort(this);
        FlowOut = new FlowOutPort(this, nameof(FlowOut));
        Ports.Add(FlowIn.Name,  FlowIn);
        Ports.Add(FlowOut.Name, FlowOut);
    }

    protected abstract TCmd BuildPayload();
    protected abstract void WriteOutputs(TResult result);

    public sealed override async Task Execute(EffectRunner runner, Flow flow)
    {
        var cmd    = BuildPayload();
        var result = await cmd.Execute((IEffectScope)flow.Scope!, flow);
        WriteOutputs(result);
        await flow.GoTo(FlowOut);
    }
}
```

**Per concrete command:**

```csharp
public sealed class DealDamage : Command<Unit>, IGraphAction<EffectRunner>
{
    public int Amount;

    [GraphPortIgnore]      // exclude from generated ports — set by host code
    public object? Target;

    public override Task<Unit> Execute(IEffectScope scope, Flow flow)
    {
        scope.Damage.Apply(Target, Amount);
        return Task.FromResult(Unit.Default);
    }
}
```

---

## Built-in nodes

Drop directly from the Add Node menu. Categories:

| Category | Nodes |
|---|---|
| Flow | `Branch` (bool → True/False), `Cancel`, `Return<T>` (typed value), `Return` (no value) |
| Logic | `Not`, `And`, `Or` |
| Compare | `GreaterThan` (int), `LessThan` (int) |
| Math | `Add`, `Subtract`, `Multiply` (int) |
| Convert | `IntToString` |
| Trigger | `OnTrigger` (event-type picker — surfaces every `[GraphEvent]` in your package) |

Plus: `Return` (no value picker → no Value port → terminate flow with `Outcome.Returned` and null result).

---

## Runtime — running a graph

```csharp
// Load the asset (Unity asset DB, Addressables, Resources — your call).
var asset = AssetDatabase.LoadAssetAtPath<EffectGraphAsset>(path);

var runner     = new EffectRunner(damageService);
var controller = new GraphController<EffectRunner>(asset);
controller.Initialize(runner, scopeFactory: () => new EffectScope());

// Run — payload type maps to entry node in the graph.
Flow flow = await controller.Run(new OnPlay { CardId = 7 });

// Result interpretation.
switch (flow.Outcome)
{
    case FlowOutcome.Returned: var result = flow.ReadResult<int>(); break;
    case FlowOutcome.Cancelled: /* explicit Cancel node fired */    break;
    case FlowOutcome.Stopped:   /* walked off the end */            break;
}
```

For trigger graphs (entry = `OnTrigger<TEvent>`):

```csharp
// At init time, walk runner.EntriesByPayload and subscribe each OnTrigger<T> entry
// to the bus. The dispatched payload is the event itself — runner.Run<TEvent>(e)
// resolves the entry through EntriesByPayload[typeof(TEvent)].
foreach (var entry in runner.EntriesByPayload.Values)
{
    if (entry is OnTrigger<DamageDealt> trig)
        bus.Subscribe<DamageDealt>(async e => await runner.Run(e), trig.Timing);
}
```

---

## Diagnostics

| Code | Severity | Trigger |
|---|---|---|
| EFG002 | Warning | `[In]` on a `readonly` field |
| EFG003 | Warning | `[Out]` on a settable field (in `AttributedFields` mode) |
| EFG004 | Warning | Port-classified field type isn't Unity-serializable |
| EFG006 | Warning | Field name in `{FlowIn, FlowOut}` (collides with reserved port) |
| EFG007 | Warning | Action payload has no execution path (no `IExecutable`, no `DispatcherBase`) |
| EFG008 | Warning | Payload satisfies bindings for two `[GraphPackage]`s |
| EFG009 | Warning | Type derived from `RuntimeNode` / `Node` is missing `[Serializable]` |

In-graph (validator runs on `OnGraphChanged`):

| Code | Severity | Trigger |
|---|---|---|
| EFG-V01 | Error | Unsupported node type (no registry entry) |
| EFG-V02 | Warning | Duplicate entry node of same type |
| EFG-V03 | Error | Edge pairs incompatible port kinds (flow↔data or unknown) |
| EFG-V04 | Warning | Flow output unwired and node isn't a terminator |
| EFG-V05 | Error | Flow output has >1 connection (executor walks one path, drops the rest — use Branch for conditional fan-out) |

---

## `[GraphPackage]` reference

| Property | Required | Purpose |
|---|---|---|
| `Runner` | yes | `typeof(YourRunner)` — package discriminator |
| `Extension` | yes | Asset file extension (no leading dot) |
| `AssetMenu` | yes | Project menu path |
| `Convention` | yes | `AllFieldsIn` / `AttributedFields` / `CommandResultPair` / `MutableInReadOnlyOut` |
| `RegistryNamespace` | yes | Namespace for emitted registry / catalog |
| `CommandBase` | Mode-2 | Open generic command base, e.g. `typeof(Command<>)` |
| `DispatcherBase` | Mode-2 | Open generic dispatcher base, e.g. `typeof(EffectCommandDispatcher<,>)` |

---

## Field tags

| Attribute | Purpose |
|---|---|
| `[GraphPort]` | Pin a port id across renames (advanced) |
| `[GraphPortIgnore]` | Exclude a public field from port emission (host-set runtime data) |
| `[GraphEvent]` | Mark an event class for `OnTrigger` discovery |
| `[GraphReturnType]` | Add a custom type to the `Return` picker (primitives are baked-in) |
| `[GraphNode]` | Mark a `RuntimeNode` derivative as a graph-author-visible node (`Category = ...`) |
| `[GraphHidden]` | Suppress emission for a payload that would otherwise qualify |
| `[In]` / `[Out]` | Direction tags in `AttributedFields` mode |

---

## Pitfalls

- **Generator DLL not labeled as Roslyn analyzer** → no emission. Check the DLL's `.meta` flags `Editor`, `RoslynAnalyzer`, and the asmdef GUID under `Run Only On Assemblies With Reference`.
- **`[GraphPackage]` on a type instead of the assembly** → not discovered. Must be assembly-level.
- **`<RunnerStem>GraphAsset.cs` missing** → graph asset can't be saved. Hand-write the one-liner SO subclass; name must match the runner stem.
- **`OnTrigger` picker shows `None` for an event** → bake throws "no EventType selected". Pick a real event before saving.
- **EFG009 fires on a runtime node** → add `[Serializable]` to the class. Attribute does not inherit; every link in the SerializeReference chain needs it.
- **Field literals don't survive bake** → field type isn't Unity-serializable. See EFG004.

---

## Working sample

`Samples/CardSandbox/` — full Mode-2 setup with events, commands, dispatcher, scope, runner. Importable through Package Manager → Samples.

`Samples~/M0Sandbox/` — minimal Mode-1 + Mode-2 vertical slice. Tilde-suffixed (Unity skips by default); import via Package Manager to use.

---

## After changing the generator

```bash
dotnet build Generators/Scaffold.GraphFlow.PackageGenerator/Scaffold.GraphFlow.PackageGenerator.csproj -c Release
cp Generators/Scaffold.GraphFlow.PackageGenerator/bin/Release/netstandard2.0/Scaffold.GraphFlow.PackageGenerator.dll \
   Assets/Packages/com.scaffold.graphflow/Generators/Scaffold.GraphFlow.PackageGenerator.dll
```

Then in Unity: right-click the synced DLL → Reimport.

---

## Related

- [ExecPlan v2](../../../Plans/GraphFlow/ExecPlan-v2.md) — design rationale, milestones
- [Tests/](Tests/) — hand-built integration tests against synthetic fixtures
