# GraphFlow follow-ups

Companion to [`UPSTREAM-CHANGES.md`](UPSTREAM-CHANGES.md). That doc covers
what we **did** to make the package work on Unity 6.4. This one covers what
we **should still do** — the deferred cleanups and the more durable design
work we noted in the post-port audit.

Sections 1–4 are upstream-flavoured (suitable for sending to Scaffold as
proposals). Sections 5–6 are local to our project / process.

---

## 1. Make the source generator version-aware (highest leverage)

**Problem.** Our patch fixed the generator by renaming
`port.name` → `port.Name` in `GraphPackageEmitter.cs`. That gets us
working on Unity 6.4, but it **breaks the same generator on Unity 6.3
and earlier**, where Graph Toolkit still uses camelCase. So today the
generator works on exactly one API surface; whichever rename you ship
locks the other version out.

**Better fix.** Generators have full access to the `Compilation` object
at generation time. They can probe which members exist on a given type
and emit the accessor that's actually there. One DLL, both versions.

**Sketch** (drop into a new
`Tools/GraphFlowGenerator/Scaffold.GraphFlow.PackageGenerator/GtkApiSurface.cs`,
then have `GraphPackageEmitter` consult it):

```csharp
internal sealed class GtkApiSurface
{
    public string PortName { get; }
    public string PortDataType { get; }
    public string PortIsConnected { get; }
    public string PortFirstConnectedPort { get; }
    public string VariableName { get; }
    public string VariableDataType { get; }
    public string VariableNodeVariable { get; }

    GtkApiSurface(string portName, string portDataType, /* … */ )
    {
        PortName = portName;
        PortDataType = portDataType;
        // …
    }

    public static GtkApiSurface Resolve(Compilation compilation)
    {
        var iPort         = compilation.GetTypeByMetadataName("Unity.GraphToolkit.Editor.IPort");
        var iVariable     = compilation.GetTypeByMetadataName("Unity.GraphToolkit.Editor.IVariable");
        var iVariableNode = compilation.GetTypeByMetadataName("Unity.GraphToolkit.Editor.IVariableNode");

        return new GtkApiSurface(
            portName:                Pick(iPort,         "Name",                "name"),
            portDataType:            Pick(iPort,         "DataType",            "dataType"),
            portIsConnected:         Pick(iPort,         "IsConnected",         "isConnected"),
            portFirstConnectedPort:  Pick(iPort,         "FirstConnectedPort",  "firstConnectedPort"),
            variableName:            Pick(iVariable,     "Name",                "name"),
            variableDataType:        Pick(iVariable,     "DataType",            "dataType"),
            variableNodeVariable:    Pick(iVariableNode, "Variable",            "variable"));
    }

    static string Pick(INamedTypeSymbol? type, string preferred, string fallback)
        => type?.GetMembers(preferred).Length > 0 ? preferred : fallback;
}
```

**Use it in the emitter** (replaces our literal `port.Name` patches):

```csharp
var api = GtkApiSurface.Resolve(compilation);
// …
sb.AppendLine($"    var fromIsFlow = fromReg.FlowOutputPortNames.Contains(port.{api.PortName});");
sb.AppendLine($"    var fromIsData = fromReg.DataOutputPortNames.Contains(port.{api.PortName});");
```

**Result.** Generator builds once, ships once, runs correctly against
whatever Graph Toolkit version it finds in the consuming project. Single
source of truth for the rename mapping (the `Pick` calls in `Resolve`).

**Cost.** ~30 lines new code, plus mechanical update of the 9 template
lines in `GraphPackageEmitter.cs` to use the resolved accessors. Half a
day's work including tests.

---

## 2. Make the hand-written editor code version-aware

**Problem.** Same root cause as §1, different layer. Our 5 patched
`Editor/*.cs` files now hard-code PascalCase access (`port.Name`,
`variable.DataType` etc.). They won't compile on Unity 6.3 or earlier.

**Generators can probe; hand-written code can't.** So we need a
compile-time switch. Two reasonable approaches.

### Option A — `versionDefines` + a compat shim (recommended)

Add to `Editor/Scaffold.GraphFlow.Editor.asmdef`:

```json
"versionDefines": [
  {
    "name": "Unity",
    "expression": "6000.4",
    "define": "SCAFFOLD_GTK_PASCAL_CASE"
  }
]
```

Then one new file, `Editor/GtkCompat.cs`, holds every conditional:

```csharp
using System;
using Unity.GraphToolkit.Editor;

namespace Scaffold.GraphFlow.Editor
{
    internal static class GtkCompat
    {
#if SCAFFOLD_GTK_PASCAL_CASE
        public static string  NameOf(this IPort p)              => p.Name;
        public static Type    DataTypeOf(this IPort p)          => p.DataType;
        public static bool    IsConnectedOf(this IPort p)       => p.IsConnected;
        public static IPort?  FirstConnectedPortOf(this IPort p) => p.FirstConnectedPort;
        public static string  NameOf(this IVariable v)          => v.Name;
        public static Type    DataTypeOf(this IVariable v)      => v.DataType;
        public static IVariable VariableOf(this IVariableNode n) => n.Variable;
#else
        public static string  NameOf(this IPort p)              => p.name;
        public static Type    DataTypeOf(this IPort p)          => p.dataType;
        public static bool    IsConnectedOf(this IPort p)       => p.isConnected;
        public static IPort?  FirstConnectedPortOf(this IPort p) => p.firstConnectedPort;
        public static string  NameOf(this IVariable v)          => v.name;
        public static Type    DataTypeOf(this IVariable v)      => v.dataType;
        public static IVariable VariableOf(this IVariableNode n) => n.variable;
#endif
    }
}
```

Call sites become `port.NameOf()`, `variable.DataTypeOf()`,
`vn.VariableOf()` — no `#if` litter, one localized compat layer.

### Option B — bump min Unity and drop older support

Edit `package.json`: `"unity": "6000.4"`. Keep our PascalCase patches
as-is. Users on older Unity can pin an older `com.scaffold.graphflow`
version. Simpler but cuts off existing users.

**Recommendation:** Option A. Cost is one file + a global find-replace
to the compat method names. Older-Unity users keep working.

---

## 3. Snapshot tests need a strategy

`Generators/Scaffold.GraphFlow.PackageGenerator.SnapshotTests/` has 30+
`.expected` files capturing the generator's output verbatim. We didn't
touch them — but they're now stale relative to our patched generator,
and they'll become more so once §1 lands.

Three options:

1. **Regenerate against current Unity** (whichever the CI box runs). One
   set of snapshots, version-pinned.
2. **Parameterize the snapshot test runner** by Unity version, with two
   sets of `.expected` files. More complete but doubles the maintenance.
3. **Strip the API-specific lines from snapshots** by post-processing
   before compare. Fragile; defeats the point of snapshot tests.

(1) is the right starting point; revisit if you actually need to support
both versions in CI.

---

## 4. Smaller upstream cleanups

These don't need design — straightforward improvements.

### 4a. Drop the stray GUID asmdef ref to the generator DLL

`Samples/CardSandbox/Editor/Scaffold.GraphFlow.CardSandbox.Editor.asmdef`
originally referenced `GUID:27a1badd...` which is the
`Scaffold.GraphFlow.PackageGenerator.dll`. Analyzer DLLs are attached
via the `RoslynAnalyzer` label on their `.meta` file — never via asmdef
`references`. We removed it; upstream should too.

### 4b. Remove the brittle `LogAssert.Expect`

`Editor/Tests/EditorBakeVariableTests.cs` line 72 (pre-patch) expected
the Unity warning "A new asset is created at the same path". Unity 6.4
no longer emits that. We deleted the line plus its now-unused `using
System.Text.RegularExpressions;` and `using UnityEngine.TestTools;`.
All 4 affected tests pass without it. Functional assertions unchanged.

### 4c. Drop `autoReferenced: true` on `Scaffold.GraphFlow.Editor.asmdef`

`autoReferenced: true` means every other editor asmdef in a consuming
project gets `Scaffold.GraphFlow.Editor` auto-linked. That's noise for
projects that have GraphFlow installed but don't author graph packages
themselves. Flip to `false`; users who need the editor assembly add an
explicit ref. Low priority.

### 4d. Consider `Samples~/` instead of `Samples/`

UPM convention for opt-in samples is `Samples~/` (tilde-suffixed,
ignored by AssetDB; importable via Package Manager UI). The current
`Samples/` compiles eagerly — which is what surfaced the generator
breakage in our import, so arguably valuable as a smoke test. Worth a
deliberate decision either way.

---

## 5. Our project's process / docs (not upstream)

### 5a. Update `CLAUDE.md` project map

We added `Tools/GraphFlowGenerator/` at the project root. `CLAUDE.md`'s
project map doesn't mention it. Add a one-liner so future contributors
(human or agent) know where the generator source lives without grepping.

### 5b. Add `Tools/GraphFlowGenerator/README.md`

Brief explanation: "This is generator source for
`com.scaffold.graphflow`'s `Scaffold.GraphFlow.PackageGenerator.dll`.
Rebuild with `dotnet build -c Release` and copy
`bin/Release/netstandard2.0/Scaffold.GraphFlow.PackageGenerator.dll`
into `Packages/com.scaffold.graphflow/Generators/`." Same content as the
relevant section of `PORT-NOTES.md`, just colocated with the code.

### 5c. Tighten edit discipline

The post-port audit flagged my use of `replace_all` for `port.name` →
`port.Name` etc. in `GraphBakerCore.cs`. It worked because every match
was unambiguously the GTK accessor — but global replaces are a footgun.
Future patches of this kind should prefer targeted edits with enough
surrounding context to be uniquely-matched.

### 5d. Bump local `package.json` version (optional)

Local copy still says `"version": "0.1.0"` — same as upstream. Bump to
`"0.1.0+local.gtk-6.4"` or similar so the patched-vs-pristine status is
discoverable from inside the package without reading `PORT-NOTES.md`.

---

## 6. Suggested order

1. **§1 + §2 together** — these are the durable design fix. Pursuing
   either alone leaves half the codebase fragile.
2. **§4 (a-c)** — small, can be folded into the same PR as §2.
3. **§3** — only matters once §1 ships; can wait.
4. **§5** — our project, do whenever.
5. **§4d** — needs a design conversation, not blocking.

If §1+§2 land upstream cleanly, our embedded copy in `Packages/`
becomes redundant — we can return to consuming Scaffold via git URL
like the other `com.scaffold.*` packages, and retire `Tools/`.
