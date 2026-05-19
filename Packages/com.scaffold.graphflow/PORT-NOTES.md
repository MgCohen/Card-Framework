# GraphFlow Port Notes (Unity 6.4 / Card Framework)

Embedded copy of `com.scaffold.graphflow` from
`github.com/MgCohen/Scaffold` (main, as of 2026-05-19).
Embedded under `Packages/` rather than fetched via git URL so we can edit
asmdefs and patch API drift locally.

## Status: green

Batch validation: **42 passed / 0 failed out of 42**. All hand-written
code compiles, the Roslyn generator was rebuilt from source against the
current Graph Toolkit API, sample compiles and its `CardSandbox.Tests`
pass 2/2, and the editor tests pass 10/10.

Companion docs:

- [`UPSTREAM-CHANGES.md`](UPSTREAM-CHANGES.md) — clean summary of what we
  changed at the package level; sendable to Scaffold as-is.
- [`FOLLOW-UPS.md`](FOLLOW-UPS.md) — what we **didn't** do yet but
  probably should: making the generator + hand-written code support
  both Unity 6.3 and 6.4 from a single source.
- This file (`PORT-NOTES.md`) — project-local context: manifest layout,
  embed vs. git URL, where the generator source lives.

## What's in the project

```
Packages/com.scaffold.graphflow/      ← embedded package (this folder)
  Generators/
    Scaffold.GraphFlow.PackageGenerator.dll   ← rebuilt locally
    Scaffold.GraphFlow.AttributesLib.dll      ← unchanged from upstream
  Runtime/, Editor/, Samples/, Tests/         ← source

Tools/GraphFlowGenerator/             ← generator source (outside Assets/)
  Scaffold.GraphFlow.Attributes/
  Scaffold.GraphFlow.PackageGenerator/        ← patched; rebuild from here
```

To rebuild the generator after any future API drift:

```
cd Tools/GraphFlowGenerator/Scaffold.GraphFlow.PackageGenerator
dotnet build -c Release
cp bin/Release/netstandard2.0/Scaffold.GraphFlow.PackageGenerator.dll \
   ../../../Packages/com.scaffold.graphflow/Generators/
```

## Why we did all this

### 1. Don't add `com.unity.graphtoolkit` to `manifest.json`

Unity 6.4 ships Graph Toolkit as a built-in module
(`UnityEditor.GraphToolkitModule`). Adding the explicit UPM package
creates duplicate-type errors (`UnityRestrictedAttribute` defined in
both the explicit package and the built-in module). Just rely on the
built-in.

### 2. Asmdef GUIDs → named refs

Scaffold's asmdefs reference Graph Toolkit by GUIDs that don't match the
built-in module's GUIDs. Replaced with named refs
(`Unity.GraphToolkit.Editor`, `Unity.GraphToolkit.Common.Editor`) in
five asmdefs:

- `Runtime/Scaffold.GraphFlow.asmdef`
- `Editor/Scaffold.GraphFlow.Editor.asmdef`
- `Editor/Tests/Scaffold.GraphFlow.EditorTests.asmdef`
- `Samples/CardSandbox/Runtime/Scaffold.GraphFlow.CardSandbox.asmdef`
- `Samples/CardSandbox/Editor/Scaffold.GraphFlow.CardSandbox.Editor.asmdef`

Also dropped the GUID ref to the generator DLL from the sample editor
asmdef — analyzer DLLs don't belong in asmdef `references`; they're
attached as Roslyn analyzers in the Inspector.

### 3. camelCase → PascalCase across Graph Toolkit interfaces

Unity 6.4's built-in Graph Toolkit uses .NET naming conventions.

| Old (Scaffold) | New (Unity 6.4 GTK) |
|---|---|
| `IPort.name` | `IPort.Name` |
| `IPort.dataType` | `IPort.DataType` |
| `IPort.isConnected` | `IPort.IsConnected` |
| `IPort.firstConnectedPort` | `IPort.FirstConnectedPort` |
| `IVariable.name` | `IVariable.Name` |
| `IVariable.dataType` | `IVariable.DataType` |
| `IVariableNode.variable` | `IVariableNode.Variable` |

Patched in five files:

- `Editor/EditorBlackboardVariables.cs`
- `Editor/EditorVariableIdentity.cs`
- `Editor/PortValueResolver.cs`
- `Editor/GraphBakerCore.cs`
- `Editor/Tests/EditorBakeVariableTests.cs`

### 4. Roslyn generator rebuild

The shipped `Scaffold.GraphFlow.PackageGenerator.dll` emitted lowercase
GTK calls into generated `.g.cs` files (e.g. `port.name` literal in
generated validators). Generator source is **not** in the package, but
**is** in the Scaffold repo at `Generators/Scaffold.GraphFlow.PackageGenerator/`.

Copied source into `Tools/GraphFlowGenerator/`, patched 9 lines in
`GraphPackageEmitter.cs` (lines 512–582; `port.name`→`port.Name`,
`other.name`→`other.Name`), rebuilt with `dotnet build -c Release`,
dropped resulting DLL into `Packages/com.scaffold.graphflow/Generators/`.

Build environment: .NET 10 SDK, `Microsoft.CodeAnalysis.CSharp 4.3.1`
(as declared in the csproj).

## Fixed: brittle `LogAssert.Expect` in editor tests

`Editor/Tests/EditorBakeVariableTests.cs` previously waited for a Unity
warning ("A new asset is created at the same path") that 6.4 no longer
emits, making 4 tests fail despite all functional assertions passing.
Dropped the `LogAssert.Expect` line plus the now-unused
`System.Text.RegularExpressions` and `UnityEngine.TestTools` using
directives. All 4 tests now pass. Details + recommended upstream fix in
[`UPSTREAM-CHANGES.md`](UPSTREAM-CHANGES.md) §5.

## Snapshot tests not run

`Generators/Scaffold.GraphFlow.PackageGenerator.SnapshotTests/` exists
in the Scaffold repo (compares generator output to `.expected` files).
We didn't copy or run those — they'd need updating after our patch.
Worth running them if we ever materially modify the generator beyond
mechanical API renames.
