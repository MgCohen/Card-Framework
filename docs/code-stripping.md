# Code stripping

To keep a type that only reflection reaches, put one attribute on its base type. Use `RequireImplementors` on an interface and `RequireDerived` on a class or a record. An assembly with `noEngineReferences: true` declares its own copy of the attribute. The linker reads the attribute by its class name.

## 1. The problem

The UnityLinker removes each type that has no reference in the IL. Reflection is not a reference. Thus a type that only a scan finds (`Assembly.GetTypes()`, `Activator.CreateInstance`, DI registration by scan) is removed from a player build.

```
1. Roots   scenes, MonoBehaviours, [RuntimeInitializeOnLoadMethod], link.xml, [Preserve]
2. Mark    follow each reference in the IL from the roots: calls, fields, typeof, base types
3. Sweep   remove each type and method with no mark
```

| Where | Stripping |
|---|---|
| Editor, EditMode and PlayMode tests | None. The gate cannot find this problem. |
| Player, stripping level Minimal | None in project assemblies |
| Player, stripping level Low, Medium, High | Removes unreferenced types in project assemblies |
| Mono and IL2CPP | Same UnityLinker, same result |

A stripped type does not cause an error. The scan finds fewer types, and the player continues without them.

## 2. Choose the attribute

| Base type | Attribute | Effect |
|---|---|---|
| Interface | `RequireImplementors` | Keeps each class or record that implements it |
| Abstract class or record | `RequireDerived` | Keeps each derived class or record |
| Class or record, not abstract | `RequireDerived` | Keeps each derived class or record |
| One type, no hierarchy | `Preserve` | Keeps only that type |

The linker uses the attribute only when it keeps the base type. A router or a scan that uses `typeof(Base)` keeps it.

The wrong attribute does nothing. `RequireDerived` on an interface keeps no implementors. `RequireImplementors` on a class keeps no derived types. The test in § 5 shows both cases.

The attribute also keeps the constructors of the types, including constructors with parameters.

## 3. Declare the attribute

### 3.1 Assembly with engine references

Use Unity's attributes.

```csharp
using UnityEngine.Scripting;

[RequireImplementors] public interface IGameHook { }
[RequireDerived]      public abstract class GameRule { }
```

### 3.2 Assembly with `noEngineReferences: true`

`Framework.Routing`, `Framework.Commands` and other pure assemblies cannot reference `UnityEngine.Scripting`. `Framework.Routing` declares our own copies.

```csharp
namespace Framework.Routing
{
    // The UnityLinker reads these attributes by class name. The namespace does not matter.
    [AttributeUsage(AttributeTargets.Interface)]
    public sealed class RequireImplementorsAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Class)]
    public sealed class RequireDerivedAttribute : Attribute { }
}

[RequireImplementors]
public interface IRouted { Type RouteType { get; } }
```

- Keep the class names exactly: `RequireImplementorsAttribute`, `RequireDerivedAttribute`, `PreserveAttribute`.
- Put the attribute on the root base type only. Derived interfaces and derived classes need no attribute.
- Member assemblies need no new reference. They already reference the assembly of the base type.

**Warning:** Run the smoke build (§ 6) after each Unity upgrade. Unity documents the name rule for `PreserveAttribute` only. For `RequireImplementors` and `RequireDerived`, only the test in § 5 shows it.

## 4. Fallbacks

Use these only if § 3 stops working, or for types that have no common base type.

| Option | Maintenance | Note |
|---|---|---|
| `link.xml` written at build time | None | Documented by Unity |
| `link.xml` written by hand | Each new assembly | Read only from `Assets/`, not packages |
| `[Preserve]` on each type | Each new type | A missing attribute gives no error |

The build-time generator is an `IUnityLinkerProcessor`. It runs the same scan as the runtime and writes the file:

```csharp
// Editor assembly. Unity calls it before the linker, in each player build.
sealed class ScannedTypesLinkXml : IUnityLinkerProcessor
{
    public int callbackOrder => 0;

    public string GenerateAdditionalLinkXmlFile(BuildReport report, UnityLinkerBuildPipelineData data)
    {
        var path = Path.Combine(data.inputDirectory, "ScannedTypes.xml");
        var sb = new StringBuilder("<linker>\n");
        foreach (var g in TypeScan.ConcreteTypesOf(typeof(IRouted)).GroupBy(t => t.Assembly.GetName().Name))
        {
            sb.Append($"  <assembly fullname=\"{g.Key}\">\n");
            foreach (var t in g) sb.Append($"    <type fullname=\"{t.FullName}\" preserve=\"all\"/>\n");
            sb.Append("  </assembly>\n");
        }
        File.WriteAllText(path, sb.Append("</linker>\n").ToString());
        return path;
    }
}
```

## 5. Test results

Unity `6000.6.6f1`, Windows player, IL2CPP, stripping level High, 2026-11-01. The members are in an assembly with `noEngineReferences: true`. No code names a member. Each case has two derived types, and one of them has a constructor parameter. All attributes are our own copies. The cells give "found / built" for the build with the attribute. The build without attributes gave 0 / 0 for all cases.

| Base type | `RequireImplementors` | `RequireDerived` |
|---|---|---|
| Interface, class implementors | 2 / 2 | 0 / 0 |
| Interface, record implementors | 2 / 2 | not tested |
| Abstract class | 0 / 0 | 2 / 2 |
| Class, not abstract | 0 / 0 | 2 / 2 |
| Abstract record | 0 / 0 | 2 / 2 |
| Record, not abstract | not tested | 2 / 2 |

A first test on Mono and IL2CPP compared more options for `IRouted`. Members implemented a derived interface, `IHandler : IRouted`.

| Protection | Found / built |
|---|---|
| None, stripping level Minimal | 3 / 3 |
| None, stripping level High | 0 / 0 |
| `link.xml` written at build time | 3 / 3 |
| Our `RequireImplementorsAttribute`, own namespace | 3 / 3 |
| Our copy with Unity's full name | 3 / 3 |

The managed code size was the same for all protected builds.

A third test used a deep class chain: interface `IRouted` ← abstract generic `Handler<TMessage, TScope>` ← abstract generic `CommandHandler<T>` ← sealed `PlayCardHandler`. A second member derived directly from `Handler<Command, ICommandScope>` and had a constructor parameter.

| Case | Attribute | Found / built |
|---|---|---|
| P1 | `RequireImplementors` on the routed interface only | 2 / 2 |
| P2 | `RequireDerived` on `Handler<,>` only | 2 / 2 |
| P3 | both | 2 / 2 |

An implementor is a class that implements the interface directly or through a base class. `RequireImplementors` keeps both.

## 6. Smoke build

The Editor does not strip code. Thus only a player build can show a stripping problem.

1. Make a small project with the base type, two derived types and a probe.
2. Put the derived types in an assembly with `noEngineReferences: true`. Do not name them in code.
3. In the probe, use `[RuntimeInitializeOnLoadMethod]`. Scan the types, construct each one, and write the counts to a file.
4. Make one build without the attribute and one build with it. Use stripping level High and IL2CPP.
5. Use a separate batch-mode Unity launch for each build.
6. Run each player with `-batchmode -nographics`. Compare the counts.

```bash
Unity.exe -batchmode -quit -nographics -projectPath <project> -executeMethod Builder.BuildHigh -logFile build.log
```

- Keep the project path short. Windows has a limit of 260 characters, and the package import fails above it.
- IL2CPP needs the Unity module "Windows Build Support (IL2CPP)" and the Visual Studio workload "Desktop development with C++".
- `GameAssembly.dll` in the output shows that the build used IL2CPP.

## 7. Related

- The scan and the router: [Product/spikes/infra-type-router.md](../Product/spikes/infra-type-router.md) § 5.5.
- Unity: [Preserve code during managed code stripping](https://docs.unity3d.com/Manual/managed-code-stripping-preserving.html).
