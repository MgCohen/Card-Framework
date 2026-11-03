# TypeRouting

TypeRouting finds the member (a handler, a validator, a reader) for a message type. It scans the assemblies for members, registers them in VContainer and freezes them in one `TypeRouter<T>`.

**Code:** `Assets/TypeRouting/` · assemblies `Framework.TypeRouting` (depends on `Framework.Stripping`) and `Framework.TypeRouting.Install` (depends on `Framework.TypeRouting`, `VContainer`) · both `noEngineReferences: true`

## What it does
- Declares a member as `IRouted`: `RouteType` names the message type that the member handles.
- Sorts members by `IOrdered.Order`, then by the full type name of the member.
- Has two modes. `RouteMode.Single` permits one member for each type. `RouteMode.Multiple` permits many.
- Finds members for the exact type (`Get`, `TryGet`, `GetAll`) and for a base type, an interface or a generic definition (`GetAllDerivedFrom`).
- Scans each loaded assembly that reaches the home assembly of `T` through a chain of references.
- Keeps scanned members in a stripped player through `[RequireImplementors]` on `IRouted`. The attribute is in [Stripping](stripping.md).

## How it works
1. The composition root calls `builder.AddTypeRouting(o => o.Route<T>(mode))`.
2. `RegisterTypeRouter<T>` calls `TypeScan.ConcreteTypesOf(typeof(T))` and registers each concrete type `As<T>()`.
3. VContainer gives all members to the `TypeRouter<T>` constructor.
4. The constructor sorts the members and builds three dictionaries. If a duplicate occurs in `Single` mode, it throws.
5. A lookup is one dictionary read and does not allocate.

## Key types
| Type | Job |
|---|---|
| `IRouted` / `IOrdered` | Member contract and optional sort order. |
| `RouteMode` | `Single` or `Multiple`. |
| `TypeRouter<T>` | Frozen lookup from message type to members. |
| `TypeScan` | Finds concrete members. Caches on (base type, `includeTests`). |
| `TypeRoutingOptions` | `Scan`, `IncludeTestAssemblies`, `Route<T>(mode)`. |
| `TypeRoutingInstall` | `AddTypeRouting` and `RegisterTypeRouter<T>`. |

## Example
From `Assets/TypeRouting/Tests/InstallTests.cs`:
```csharp
builder.AddTypeRouting(o =>
{
    o.IncludeTestAssemblies = true;
    o.Route<INoteReader>(RouteMode.Single);
});

var router = builder.Build().Resolve<TypeRouter<INoteReader>>();
Assert.That(router.Get(typeof(Hello)), Is.InstanceOf<HelloReader>());
```

## Gaps
- Only Pipeline uses `TypeRouter<T>`. The Wire, Commands and Requests registries still have their own lookups.
- No adapter connects `AddTypeRouting` to the Bootstrap `IInstaller`.
- Rules ROUTE-01 to ROUTE-03 are not in `rulebook.json`.
- No smoke build of a stripped player checks `[RequireImplementors]`. A test in the Editor shows the attribute only.
- The scan skips each assembly that references `nunit.framework`, unless `IncludeTestAssemblies` is true.
