# Upstream changes for `com.scaffold.graphflow` on Unity 6.4

A summary of the changes needed to make `com.scaffold.graphflow` build and pass
its own tests on **Unity 6000.4.0f1** (Unity 6.4), where Graph Toolkit is shipped
as a built-in module (`UnityEditor.GraphToolkitModule`) rather than as a
scoped UPM package.

Validated by running the package's own test assemblies in batch mode against a
clean Unity 6.4 project:

```
Total: 42 | Passed: 42 | Failed: 0
- Scaffold.GraphFlow.Tests              30/30
- Scaffold.GraphFlow.EditorTests        10/10
- Scaffold.GraphFlow.CardSandbox.Tests   2/2
```

Each change below is something the **upstream package would want to absorb** if
it intends to support Unity 6.4+. Project-local concerns (manifest entries,
embed vs. git URL, where we keep the generator source) are intentionally left
out — see `PORT-NOTES.md` in this folder for those.

This is **what we did** — the minimum to get the package working on 6.4.
For **what we'd recommend doing next** (notably: making the generator and
hand-written code work on both 6.3 and 6.4 from a single source), see
[`FOLLOW-UPS.md`](FOLLOW-UPS.md).

---

## 1. Graph Toolkit became a built-in module in 6.4

In Unity 6.4, `Unity.GraphToolkit.Editor` is part of the editor itself
(`UnityEditor.GraphToolkitModule`). Pulling the experimental UPM package
(`com.unity.graphtoolkit` 0.4.0-exp.2 and earlier) alongside the built-in
module produces type-conflict errors:

```
error CS0433: The type 'UnityRestrictedAttribute' exists in both
  'Unity.GraphToolkit.UnityRestricted' and 'UnityEditor.GraphToolkitModule'
```

**Implication for `com.scaffold.graphflow`**: it can rely on the built-in
module being available on 6.4+. No package dependency required.

## 2. Asmdef GUID refs → named refs

The five asmdefs referenced Graph Toolkit (and each other) by GUID. The
GUIDs don't match what the built-in 6.4 module uses, so the references
fail to resolve. Replaced GUIDs with named references everywhere; the
named refs survive future GUID churn too.

Files:
- `Runtime/Scaffold.GraphFlow.asmdef`
- `Editor/Scaffold.GraphFlow.Editor.asmdef`
- `Editor/Tests/Scaffold.GraphFlow.EditorTests.asmdef`
- `Samples/CardSandbox/Runtime/Scaffold.GraphFlow.CardSandbox.asmdef`
- `Samples/CardSandbox/Editor/Scaffold.GraphFlow.CardSandbox.Editor.asmdef`

Pattern of change (illustrative):

```diff
-    "GUID:bab7af28f08a411c9af5ef2f6d191216",
-    "GUID:b13665e99cedac1faa8a90387e6163c3",
-    "GUID:70ef9a24f4cfc4aec911c1414e3f90ad",
-    "GUID:d1e08c06f8f9473888c892637c83c913",
-    "GUID:aeaa2ad3d0d1b9aec5a07ab29b764460"
+    "Scaffold.GraphFlow",
+    "Scaffold.GraphFlow.PackageAttributes",
+    "Scaffold.Variables",
+    "Unity.GraphToolkit.Editor"
```

Also dropped a stray GUID reference to the **generator DLL itself** from the
sample editor asmdef (`Samples/CardSandbox/Editor/Scaffold.GraphFlow.CardSandbox.Editor.asmdef`)
— analyzer DLLs are attached via the Inspector's Roslyn Analyzer label,
not via asmdef `references`.

## 3. camelCase → PascalCase across Graph Toolkit interfaces

Unity 6.4's built-in Graph Toolkit uses .NET naming conventions for
public members. The same accessors that exist as `port.name` /
`variable.dataType` / `vn.variable` etc. on older GTK now ship as
`port.Name` / `variable.DataType` / `vn.Variable`.

| Old (≤ Scaffold's GTK) | New (Unity 6.4) |
|---|---|
| `IPort.name` | `IPort.Name` |
| `IPort.dataType` | `IPort.DataType` |
| `IPort.isConnected` | `IPort.IsConnected` |
| `IPort.firstConnectedPort` | `IPort.FirstConnectedPort` |
| `IVariable.name` | `IVariable.Name` |
| `IVariable.dataType` | `IVariable.DataType` |
| `IVariableNode.variable` | `IVariableNode.Variable` |

Patched call sites in:

- `Editor/EditorBlackboardVariables.cs`
- `Editor/EditorVariableIdentity.cs`
- `Editor/PortValueResolver.cs`
- `Editor/GraphBakerCore.cs` (~22 occurrences)
- `Editor/Tests/EditorBakeVariableTests.cs`

## 4. Roslyn generator templates needed the same rename

`Generators/Scaffold.GraphFlow.PackageGenerator.dll` emits validator code
into consuming assemblies. Its `StringBuilder.AppendLine` templates
hard-code the **old**, lowercase GTK accessors, so anything the generator
emits fails to compile against 6.4.

**Source file**:
`Generators/Scaffold.GraphFlow.PackageGenerator/GraphPackageEmitter.cs`
(in the Scaffold repo at `Generators/`, not in the published package).

**Patch** — 9 occurrences across lines 512–582, all of the same form:

```diff
- sb.AppendLine("    var fromIsFlow = fromReg.FlowOutputPortNames.Contains(port.name);");
+ sb.AppendLine("    var fromIsFlow = fromReg.FlowOutputPortNames.Contains(port.Name);");
```

After patching, rebuild the generator (`dotnet build -c Release` in
`Generators/Scaffold.GraphFlow.PackageGenerator/`) and ship the new
`Scaffold.GraphFlow.PackageGenerator.dll` inside the package's
`Generators/` folder.

Other source files in the generator project (`GraphNodeRuntimePartialGenerator.cs`,
`GraphCatalogEmitter.cs`, etc.) also reference a `.name` field — but those
are *model* properties on the generator's internal data structures (e.g.
`PortModel.name`), not Graph Toolkit accessors. They emit *port-name
strings* into generated code, not property access expressions, so they
do not need to change.

**Snapshot tests** (`Generators/Scaffold.GraphFlow.PackageGenerator.SnapshotTests/`)
will need their `.expected` files regenerated to match the new PascalCase
output. Not done here; we only validated against Unity batch tests, not
the standalone snapshot runner.

## 5. `EditorBakeVariableTests` had a brittle `LogAssert.Expect`

`Editor/Tests/EditorBakeVariableTests.cs` called

```csharp
LogAssert.Expect(LogType.Warning,
    new Regex("A new asset is created at the same path"));
var graph = GraphDatabase.CreateGraph<CardEffectGraph>(_assetPath);
```

inside its `CreateTestGraph` helper. In earlier Unity versions, the
internal AssetDatabase fired a "A new asset is created at the same path"
warning during `CreateGraph`, and the test pre-declared it so it wouldn't
fail the `LogAssert` default-strict policy. In Unity 6.4 that warning is
no longer emitted, so the `LogAssert.Expect` waits for a log that never
arrives and the test fails — even though the asset is still created
correctly and every functional `Assert.AreEqual` passes.

**Fix**: removed the `LogAssert.Expect` line plus the now-unused
`System.Text.RegularExpressions` and `UnityEngine.TestTools` using
directives. All 4 affected tests (`BakeVariables_RoundTrip_…`,
`BakeVariables_StableIdentity_…`, `BakeVariables_TypeNameMatchesDefaultValueType`,
`ReBake_AfterVariableRename_PreservesVariableId`) now pass.

If older Unity versions still need to be supported, a `#if UNITY_6000_4_OR_NEWER`
guard would work, but for a 6.4-targeted package it's simpler to drop.

---

## Things we intentionally did NOT change

- **`package.json`** — kept `"unity": "6000.3"`. The package works on 6.3
  too if a compatible GTK is around; we don't want to bump the minimum
  just because we tested on 6.4.
- **Sample layout** — `Samples/CardSandbox/` (no `~`). Unity's UPM
  convention is `Samples~/` for hidden, on-demand samples. The current
  layout means the sample compiles eagerly — which is what surfaced the
  generator-output bug in the first place, so it's arguably useful as a
  smoke test. Worth a separate conversation upstream.
- **The unrelated nullability warnings** in
  `PayloadFieldLintPass.cs:41` and `GraphPayloadNodeEmitter.cs:92,103`
  (`CS8604: Possible null reference argument`). These existed before
  our patch; we left them alone.
