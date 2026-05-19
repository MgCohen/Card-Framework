# GraphFlow Bake Types & Node Patterns

Reference for how runtime nodes are authored and how the source generator
processes them into editor nodes + registry entries.

---

## Node authoring overview

Every runtime node is a `[Serializable]` class that extends `RuntimeNode` (or
`RuntimeNode<TRunner>` for runner-typed nodes). The `[GraphNode]` attribute
tells the source generator to:

1. **Emit a partial constructor** (`GraphNodeRuntimePartialGenerator`) that
   initialises port handles (`InputPort<T>`, `OutputPort<T>`, `FlowInPort`,
   `FlowOutPort`), calls the optional `partial void InitializePorts()` hook,
   and populates the `Ports` dictionary.
2. **Emit an editor mirror** (a GT `Node` subclass with `OnDefinePorts`) so the
   node appears in the graph editor menu.
3. **Emit a registry entry** (factory + port-name sets) so `GraphBakerCore` can
   translate the editor graph into the runtime asset.

---

## Bake types (node families)

### 1. Plain data node

No flow ports. The factory is `_ => new MyNode()`.

```csharp
[Serializable, GraphNode(Category = "Math")]
public sealed partial class Add : RuntimeNode
{
    public InputPort<int> A = null!;
    public InputPort<int> B = null!;
    public OutputPort<int> Result = null!;

    partial void InitializePorts() =>
        Result = new OutputPort<int>(flow => A.Read(flow) + B.Read(flow));
}
```

### 2. Flow node

Has `FlowInPort` / `FlowOutPort` fields. Otherwise identical to data nodes.

```csharp
[Serializable, GraphNode(Category = "Flow")]
public sealed partial class Branch : RuntimeNode
{
    public FlowInPort In = null!;
    public InputPort<bool> Condition = null!;
    public FlowOutPort True = null!;
    public FlowOutPort False = null!;

    partial void InitializePorts() =>
        In = FlowInPort.Sync(this, nameof(In), flow =>
            Condition.Read(flow) ? True : False);
}
```

### 3. Entry node (`IGraphEntry`)

Extends `EntryRuntimeNode<TPayload>`. The registry entry includes an
`EntryTypeId` so `GraphRunner.Run(payload)` can locate the right entry point.

### 4. Option-based node (OnTrigger / Return pattern)

A single editor node with a **dropdown** that dispatches to different runtime
classes at bake time. The pattern has three layers:

| Layer | Location | Example |
|-------|----------|---------|
| **Editor base class** | `Editor/Nodes/` (framework) | `OnTriggerEditorNode<TEnum>` |
| **Per-package shim** | Generated `.g.cs` | `OnTrigger : OnTriggerEditorNode<EventType>` |
| **Registry factory** | Generated registry | Reads option, calls `Catalog.Resolve(picked)` |

The editor base defines `OnDefineOptions` (the dropdown) and `OnDefinePorts`
(adds typed ports based on the selected option). The per-package shim closes the
generic over the package's catalog enum. The registry factory reads the option at
bake time and instantiates the correct runtime class.

### 5. Variable node (Get / Set / Observe)

Three hand-written generic runtime classes (`GetVariable<T>`, `SetVariable<T>`,
`ObserveVariable<T>`) live in `Scaffold.GraphFlow.Nodes`. The generator discovers
`BlackboardVariable<T>` subclasses during its existing assembly walk and emits
the editor layer:

| Layer | What | Where |
|-------|------|-------|
| **Runtime nodes** | `GetVariable<T>`, `SetVariable<T>`, `ObserveVariable<T>` — hand-written generics | Runtime assembly (`Runtime/Variables/`) |
| **Catalog entries** | `CatalogEntry` per variable type (`CatalogKind.Variable`), `VariableType` enum, `Resolve(VariableType)` | Runtime assembly (inside `<Stem>Catalog`, by `GraphCatalogEmitter`) |
| **Editor shims** | `GetVariable`, `SetVariable`, `ObserveVariable` — close the generic base over `<Stem>Catalog.VariableType` | Editor assembly (inline in `GraphPackageEmitter`) |
| **Registry entries** | Factory creates closed generic (e.g. `new GetVariable<int>()`) based on dropdown | Editor assembly (by `VariableNodeEmitter`) |

Variables are a fifth catalog concern alongside events, returns, commands, and
entries. The editor shims forward to `Catalog.Resolve(picked)?.Type` — the same
one-liner pattern as `OnTriggerEditorNode<TEnum>` and `ReturnEditorNode<TEnum>`.

Variable type discovery piggybacks on the same assembly walk as `[GraphNode]`
discovery in `EmitGenericNodeArtifacts` — no duplicate type enumeration. The
discovered types are passed to `EmitCatalogIfRunnerAsm` since `BlackboardVariable<T>`
subclasses live in the framework assembly, not the runner's assembly.

**Adding a new variable type** — the only manual step:

```csharp
[Serializable] public sealed class BlackboardVector3 : BlackboardVariable<Vector3> { }
```

The generator discovers this subclass and auto-emits the enum entry, editor
ports, and registry factory. Reimport the generator DLL in Unity to pick up
the changes.

---

## Generator deployment

After editing files under `Generators/Scaffold.GraphFlow.*/`:

```powershell
dotnet build Generators/Scaffold.GraphFlow.PackageGenerator/Scaffold.GraphFlow.PackageGenerator.csproj -c Release

# Copy both DLLs into the Unity package:
cp Generators/Scaffold.GraphFlow.PackageGenerator/bin/Release/netstandard2.0/Scaffold.GraphFlow.PackageGenerator.dll `
   Assets/Packages/com.scaffold.graphflow/Generators/Scaffold.GraphFlow.PackageGenerator.dll

cp Generators/Scaffold.GraphFlow.Attributes/bin/Release/netstandard2.0/Scaffold.GraphFlow.AttributesLib.dll `
   Assets/Packages/com.scaffold.graphflow/Runtime/Attributes/Scaffold.GraphFlow.AttributesLib.dll
```

Then reimport the DLLs in Unity (right-click > Reimport, or wait for auto-refresh).

---

## Bake pipeline (ScriptedImporter flow)

1. User saves a `.card` file (GT graph asset).
2. Unity's `ScriptedImporter` (`CardEffectGraphImporter`) fires.
3. `GraphDatabase.LoadGraphForImporter<TGraph>` loads the GT editor graph.
4. `GraphBakerCore.Bake<TRunner, TAsset>` walks editor nodes:
   - Looks up each editor node type in the `GraphPackageRegistry`.
   - Calls the registry `Factory` to create the runtime node.
   - Records flow edges and data edges by matching port names.
   - Bakes variables from the GT blackboard panel via `EditorBlackboardVariables`.
   - Bakes variable edges from `IVariableNode` connections.
5. The baked `GraphAsset<TRunner>` is set as the main imported object.

---

## Registry structure

Each `NodeRegistration` contains:

| Field | Purpose |
|-------|---------|
| `EditorNodeType` | The GT editor node class (used to match during bake) |
| `Factory` | `Func<INode, RuntimeNode>` — creates the runtime instance |
| `FlowInputPortNames` | Valid flow-in port names for edge validation |
| `FlowOutputPortNames` | Valid flow-out port names |
| `DataInputPortNames` | Valid data-in port names |
| `DataOutputPortNames` | Valid data-out port names |
| `EntryTypeId` | (Entry nodes only) `AssemblyQualifiedName` of the payload type |
