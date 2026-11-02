---
type: spike
status: proposed
created: 2026-10-31
references:
  - docs/lookup-patterns.md
  - docs/architecture.md
  - Product/spikes/framework-roadmap.md
  - Product/spikes/command-service.md
sandbox: Assets/Framework/Routing/
tags: [#spike, #infra, #router, #wire, #commands]
---

# Shared infrastructure — type-keyed router

> **Status: proposed.** This spike is one of four parallel pieces of the shared
> pipeline infrastructure (intent, effect, command, event). The other pieces are
> record ids and allocation, the block and wire system, and the command pipeline.
> This piece owns the frozen `Type` lookup only: register routed members, get the
> member for a type, and get the members under a type. Hooks are out of scope
> (D8). No code changes yet.

## 1. TL;DR

1. One generic class in a new system `Framework.Routing` (folder `Assets/Framework/Routing/`, D14): `TypeRouter<T>`. It is a frozen map from a `Type` to its members. A `RouteMode` (`Single` or `Multiple`) sets the duplicate rule (D10).
2. One member contract, `IRouted { Type RouteType }`, and one optional `IOrdered { int Order }`.
3. Registration is one line for each router: `RegisterTypeRouter<T>(mode)`. It scans the loaded assemblies for members (D9). `TypeScan` scans once and caches the result, so other systems can use the same scan.
4. Dispatch reads (`Get`, `TryGet`, `GetAll`) use the **exact type only**. Leaf message types become `sealed`, so an exact route cannot miss a subclass.
5. A query read, `GetAllDerivedFrom(type)`, gives the members of every registered type under `type`: subclasses, implementations and closed generics (§ 5.6).
6. `Dispatcher` (byte array) and `Network._byType` (id allocator) stay specialized. Hooks are out of scope (D8).

### Decisions (2026-10-31, D9 to D13 on 2026-11-01, D14 on 2026-11-02)

The user made these decisions for all four infrastructure spikes. This spike uses them.

| # | Decision | Effect on this spike |
|---|---|---|
| D1 | Ids are typed `readonly struct`s around `int` (`EntityId`, `RequestId`, `BlockId`, `MessageId`). | The router key stays `Type`. Typed ids key live data only, and this piece holds no live data. |
| D2 | Shared infrastructure lives in `Framework.Core` (routers, `IdAllocator<TId>`, `EntityId`). `Framework.Wire` references `Framework.Core`. | Closes the home question. § 8 gives the ARCH-02 rows and the new Core text. |
| D3 | A new `Framework.Blocks` feature owns all `Block*` types and `BlockId`. | `BlockStartEvent` and `BlockEndEvent` move out of Wire. They route like any other event (§ 10). |
| D4 | The async core uses `System.Threading.Tasks.Task`, not UniTask. | No effect. Router reads are synchronous. |
| D5 | The wire carries `CauseId` only. The nesting parent comes from bracket order. No parent override. | The router does not carry or compute block parents (§ 10). |
| D6 | An intent opens no block. | No effect. The intent router only finds a handler. |
| D7 | Hooks exist only at the command level. Game-wide hooks are frozen DI registrations. Card hooks go in a server-only `HookTable`. | No effect here after D8. The command pipeline piece owns hooks. |
| D8 | The router scope is type lookup only: register routed members, get the member for a type, get the members under a type. Hooks and hook tables are out of scope. | § 5.4 removes the hook design. § 5.6 adds the subtype read `GetAllDerivedFrom`. |
| D9 | Members are found by an assembly scan, not by one registration line for each member. Source generation can come later, as an optional V2 plugin. | § 5.5. The explicit verbs stay only for tests and for closed generic members. |
| D10 | One router class, `TypeRouter<T>`, with a `RouteMode` option. `TypeMultiRouter<T>` is removed. | § 5.1, § 5.2. A wrong read is a run-time error, not a compile error. |
| D11 | The API and the examples are about type lookup, with neutral types. Commands and events are users of the router, not its API. | § 4. |
| D12 | Names: `RouteMode { Single, Multiple }`. Reads `Get`, `TryGet`, `GetAll`, `GetAllDerivedFrom`, `RoutedTypesDerivedFrom`, `RoutedTypes`. `HandlerRegistry` becomes `WireEventDispatcher`. The Pipeline owns the run policy for many members. | § 4, § 5.2, § 6, § 9, § 12. |
| D13 | The command member types are the Pipeline's abstract classes `Handler<Command, ICommandScope>` and `Validator<Command>`, not `ICommandHandler` and `ICommandValidator`. The Pipeline uses `RouteMode.Single` for both in v0. | § 4, § 5.2, § 6, § 9. |
| D14 | Plan an extraction, not a refactor. The router is a new system in one root folder, `Assets/Framework/Routing/`, assembly `Framework.Routing`. The build changes no current system. D2 stays for ids. For routers, `Framework.Routing` replaces `Framework.Core`. | § 4, § 5.2, § 8, § 9, § 10, § 11. |

## 2. Scope

The user approved this table (2026-11-01). The design sections cover only the concerns marked "in".

| Concern | In / out | Owner if out |
|---|---|---|
| Member contract: `IRouted` (route type), `IOrdered` (order) | in | — |
| DI registration: one style for members and routers | in | — |
| Exact read, one member: `Get` / `TryGet`, with the duplicate and miss rules | in | — |
| Exact read, N members in a stable order: `GetAll` | in | — |
| Subtype read: `GetAllDerivedFrom` / `RoutedTypesDerivedFrom` | in | — |
| Composition check of the registered types: `RoutedTypes` | in | — |
| Rule: leaf messages are `sealed` (ROUTE-03), so exact dispatch cannot miss a subclass | in | — |
| Call the member (`Handle`, `Apply`) and the context it gets | out | Pipeline (intent, effect, command); Blocks & Wires (event apply in `HandlerRegistry`) |
| Select the stage by message kind (`Enqueue`) | out | Pipeline |
| Hooks: game-wide hooks, `HookTable`, merge, tie rule | out | Pipeline |
| `_direct` per-entity triggers | out | Pipeline |
| Byte id to channel (`Dispatcher`), type to channel (`Network`) | out | Blocks & Wires |
| Id allocation (`Network._nextId`, `IdAllocator<TId>`) | out | Record Ids |
| Kind keys (`SlotRegistry`, tag slots) | out | Kind Ids |
| Live data keyed by entity | out | the feature Store that holds the data |

**Boundary.** At composition, the router gets a member list from DI. At run time, it gets a `Type` and gives back one member, N members or the members under that type. It gives them to the stage that calls them: the Pipeline (intent, effect, command) or `HandlerRegistry` (events).

## 3. Current shapes

| Copy | Key | Cardinality | Miss | Duplicate | Frozen | `file:line` |
|---|---|---|---|---|---|---|
| `HandlerRegistry._byType` | `Type` (`IWireEventHandler.EventType`) | 0..N, registration order | silent no-op | allowed (fan-out) | yes | `Assets/Framework/Wire/Runtime/Stream/HandlerRegistry.cs:8`, dispatch `:22` |
| `CommandHandlerRegistry._byType` | `Type` (`ICommandHandler.CommandType`) | exactly 1 | throws | throws | yes | `Assets/Framework/Commands/Runtime/CommandHandlerRegistry.cs:8`, miss `:24` |
| `CommandValidatorRegistry._byType` | `Type` (`ICommandValidator.CommandType`) | 0..1 | try-get, absent means accept | throws | yes | `Assets/Framework/Commands/Runtime/CommandValidatorRegistry.cs:8`, miss `:24` |
| `EffectStore._hooks` | `(Type, HookPhase)` | 0..N, sorted by `HookPriority` | empty array | allowed | **no** (`Subscribe`/`Unsubscribe`) | `Assets/Spike/ECS/VerticalSlice/Engine/Pipeline/EffectStore.cs:34`, read `:74` |
| `EffectStore._direct` | `(int entityId, Type)` | 0..1, replace on set | `null` | replaces | **no** | `Assets/Spike/ECS/VerticalSlice/Engine/Pipeline/EffectStore.cs:35`, read `:109` |
| `Dispatcher._channels` | `byte` (array index) | 0..1 | throws | overwrites | after build callback | `Assets/Framework/Wire/Runtime/Network/Dispatcher.cs:11`, miss `:28` |
| `Network._byType` | `Type` | 0..1 | `KeyNotFoundException` | overwrites | after build callback | `Assets/Framework/Wire/Runtime/Network/Network.cs:11`, read `:36` |
| `HandlerRegistrationExtensions` | n/a (DI feed) | n/a | n/a | n/a | n/a | `Assets/Framework/Bootstrap/Runtime/HandlerRegistrationExtensions.cs:10-20` |

Findings:

- The three Framework `Type` copies have the same key, the same backing and the same DI feed. Only the duplicate rule and the miss behavior change.
- `CommandValidatorRegistry` is a verbatim copy of `CommandHandlerRegistry` (see `command-service.md` § Phase 1 build notes). Only the read verb is different.
- The three helpers in `HandlerRegistrationExtensions` have identical bodies.
- `EffectStore._hooks` sorts with `List.Sort`, which is not stable. Two hooks with the same priority can change order between runs.
- `EffectStore.Subscribers` allocates a new array (`ToArray`) on each read.

## 4. API

`TypeRouter<T>` is a frozen map from a `Type` to the members of type `T` that route to it. `TypeScan` finds the members. The composition root registers one router for each member interface.

| Audience | Entry point | Does |
|---|---|---|
| member author | `IRouted.RouteType` (optional `IOrdered.Order`) | Says which type the member is for. |
| composition | `builder.RegisterTypeRouter<T>(RouteMode mode)` | Scans for the members of `T` and registers the router. |
| runtime | `Get`, `TryGet`, `GetAll` | Dispatch read by exact type. |
| runtime | `GetAllDerivedFrom`, `RoutedTypesDerivedFrom` | Query read over a type family. Not for dispatch. |
| composition check | `RoutedTypes` | Lists the registered route types. |

Call site, with neutral types:

```csharp
// 1. The types to route on: any types
public abstract class Note { }
public sealed class Hello : Note { }
public sealed class Bye : Note { }

// 2. The members: each one says its route type
public interface INoteReader : IRouted { void Read(Note note); }
public sealed class HelloReader : INoteReader
{
    public Type RouteType => typeof(Hello);
    public void Read(Note note) { /* ... */ }
}

// 3. Composition: one line. The scan finds HelloReader (and ByeReader).
builder.RegisterTypeRouter<INoteReader>(RouteMode.Single);

// 4. Runtime: DI injects the router. The class reads it.
public sealed class NoteDesk
{
    readonly TypeRouter<INoteReader> _readers;
    public NoteDesk(TypeRouter<INoteReader> readers) => _readers = readers;

    public void Take(Note note) => _readers.Get(note.GetType()).Read(note);    // exact type
    public IReadOnlyList<INoteReader> AllReaders() => _readers.GetAllDerivedFrom(typeof(Note));
}
```

Who uses it (the framework levels):

| Use | Member type | Mode | Read |
|---|---|---|---|
| command handlers | `Handler<Command, ICommandScope>` (Pipeline) | `Single` | `Get` |
| command validators | `Validator<Command>` (Pipeline) | `Single` | `TryGet` |
| event apply handlers | `IWireEventHandler` | `Multiple` | `GetAll` |
| effect and intent handlers (later) | the Pipeline decides | `Single` | `Get` |

Dependencies:

| From | To | Why |
|---|---|---|
| `Framework.Routing` (`TypeRouter<T>`, `TypeScan`, `IRouted`, `RegisterTypeRouter<T>`) | VContainer | The DI verb is part of the system (D14). No Framework assembly. |
| Member assemblies (Commands, Wire, game code) | `Framework.Routing` | Members implement `IRouted`. Added at migration. |

Rules. "Gate" is `Tools/Rulebook/gate.ps1`. It checks the rules in `Tools/Rulebook/rulebook.json` and fails the build. The `ROUTE-*` rules are new proposals from this spike (§ 8).

| Rule | Enforced by |
|---|---|
| Code does not write its own `Dictionary<Type, *>` router. | Gate (proposed ROUTE-01) |
| Code does not keep a router in a `static` field. | Gate (proposed ROUTE-02) |
| A concrete message type is `sealed`, so exact dispatch cannot miss a subclass. | Gate (proposed ROUTE-03) |
| A member gives its route type. | Compiler: the constraint `where T : class, IRouted` |
| No member is added after composition. | No method exists for it. Only the constructor writes. |
| A route type has a member when `Get` reads it. | Run time: `Get` throws. No earlier check exists yet (§ 11). |
| Dispatch uses exact reads, not `GetAllDerivedFrom`. | Code review only. |

## 5. Generalized contract

### 5.1 Decision on question 1: one class, one contract

`docs/lookup-patterns.md` says "Standardize the contract, not the class" for Routers. This spike shares the class where key, backing and feed are identical. That is the case for every `Type` router that DI fills from a list.

The first version of this spike had two classes, one for each mode. The user chose one class (D10). It is easier to learn, and one DI verb covers every level. The mode becomes a constructor option:

- `RouteMode.Single`: two members with the same route type throw in the constructor. `Get` and `TryGet` are the normal reads.
- `RouteMode.Multiple`: many members for one route type are legal. `GetAll` is the normal read. `Get` and `TryGet` throw if the type has more than one member.

Cost: a wrong read (for example `Get` on a type with two members) is a run-time error, not a compile error. The error text names the router and the type.

### 5.2 Signatures

Namespace `Framework.Routing`, folder `Assets/Framework/Routing/Runtime/` (D14). The asmdef has `noEngineReferences: true` and references VContainer only. One top-level type per file.

```csharp
[RequireImplementors]   // keeps scanned members in a stripped player (§ 5.5)
public interface IRouted  { Type RouteType { get; } }
public interface IOrdered { int Order { get; } }   // lower runs first; equal values sort by full type name

public enum RouteMode { Single, Multiple }
```

```csharp
public sealed class TypeRouter<T> where T : class, IRouted
{
    public TypeRouter(IReadOnlyList<T> members, RouteMode mode);

    public T    Get(Type type);                     // exactly one member; throws on a miss or on many
    public bool TryGet(Type type, out T member);    // false on a miss; throws on many
    public IReadOnlyList<T> GetAll(Type type);         // 0..N in order; empty on a miss; no allocation

    public IReadOnlyList<T>    GetAllDerivedFrom(Type type);   // type itself, subclasses, implementors, closed generics (§ 5.6)
    public IReadOnlyList<Type> RoutedTypesDerivedFrom(Type type); // the routed types in that same set (§ 5.6)
    public IReadOnlyCollection<Type> RoutedTypes { get; }   // composition checks only
}
```

```csharp
public static class TypeScan
{
    // Concrete types that implement or derive from baseType. Scans once for each baseType, then caches.
    public static IReadOnlyList<Type> ConcreteTypesOf(Type baseType);
}
```

Rules for the implementation:

- The constructor is the only writer. After the constructor, no member is added or removed.
- A duplicate with `RouteMode.Single` throws `InvalidOperationException`: `Duplicate {T} for {type}.`
- A miss in `Get` throws `InvalidOperationException`: `No {T} registered for {type}.` The current messages carry the same facts.
- `GetAll` returns a stored array. Callers iterate with `for`, not `foreach`, to keep the hot path free of enumerator allocation.
- **Order.** The constructor sorts once: `IOrdered.Order` (0 when absent), then `Type.FullName` (ordinal). Registration order is not used, because the scan order is not stable. Both sides run the same build, so both sides get the same order.

Member interfaces derive from `IRouted`. The bridge base classes implement it explicitly, so the current property names stay:

```csharp
public abstract class WireEventHandler<T> : IWireEventHandler where T : WireEvent
{
    public Type EventType => typeof(T);
    Type IRouted.RouteType => typeof(T);
    // ...
}
```

The Pipeline owns the command member types (D13). They are abstract classes, so only the Pipeline assembly can call the entry:

```csharp
public abstract class Handler<TMessage, TScope> : IRouted
{
    public abstract Type RouteType { get; }
    protected internal abstract Task<object> Handle(TMessage m, TScope s);
}
public abstract class CommandHandler<T> : Handler<Command, ICommandScope> where T : Command
{
    public override Type RouteType => typeof(T);   // Commands owns this base
}
```

`Validator<TMessage>` has the same shape, with `protected internal abstract bool Validate(TMessage m, out string reason)`. `[RequireImplementors]` on `IRouted` keeps a subclass two levels down, such as `PlayCardHandler : CommandHandler<PlayCard>` (§ 5.5).

### 5.3 Question 2: composite keys

The router key is `Type` and nothing else. A second key part goes one of two ways:

1. **Small closed set** (for example a phase enum with two values). Partition, do not compose. The caller keeps one router for each value, in an array indexed by the value. The router itself does not know about the partition.
2. **Open set** (for example `entityId` in `_direct`). This is live data, not a route. It belongs in a Store or in a live table that another piece owns.

If a frozen route needs a composite key that fits neither case, write a `readonly struct` key that implements `IEquatable<TKey>`. Do not use a `ValueTuple` key. This case does not exist today, so no generic `Router<TKey, T>` ships now.

### 5.4 Question 3: hooks are out of scope

Hooks are not part of this piece (D8). The command pipeline piece owns the frozen game-wide hooks, the live card hooks (`HookTable`), the merge of the two, the tie rule and `_direct`. It can use `TypeRouter<IHook>` with `RouteMode.Multiple` as a normal fan-out router.

### 5.5 Question 4: registration by scan

One DI verb for every level (D9):

```csharp
public static void RegisterTypeRouter<T>(this IContainerBuilder b, RouteMode mode, bool scan = true)
    where T : class, IRouted
{
    if (scan)
        foreach (var type in TypeScan.ConcreteTypesOf(typeof(T)))
            b.Register(type, Lifetime.Singleton).As<T>();   // VContainer Register(Type, Lifetime)
    b.Register<TypeRouter<T>>(Lifetime.Singleton).WithParameter(mode);
}
```

VContainer then injects `IReadOnlyList<T>` into the router constructor. An empty list resolves cleanly (`command-service.md` § Phase 0 build notes).

What the scan reads:

1. `AppDomain.CurrentDomain.GetAssemblies()`. It skips dynamic assemblies.
2. Only the assembly of `T` and the assemblies that reference it. Other assemblies cannot hold a member, so the scan skips them. No "example target" type is necessary.
3. It skips assemblies that reference `nunit.framework`. A test handler then does not enter a production container. A test registers its members explicitly.
4. It keeps concrete classes that are assignable to `T`. It skips abstract classes and open generic definitions.

`TypeScan` caches each result by `baseType`. It is the one scan for the framework. Other systems that need "all concrete types of X" call it too.

The explicit verbs stay for two cases:

- **Tests** that need a subset: `RegisterTypeRouter<T>(mode, scan: false)` and then `RegisterCommandHandler<THandler>()` for each member.
- **Closed generic members.** The scan cannot close `TagSetHandler<T>`. `RegisterTagValueType<T>()` stays and registers `TagSetHandler<int>` and the other closed types explicitly.

**Code stripping.** A member that only the scan reaches has no direct reference, so the linker removes it. The fix is one attribute on `IRouted`:

```csharp
// Framework.Routing. Our own copy: the assembly has noEngineReferences, so it cannot use UnityEngine.Scripting.
[AttributeUsage(AttributeTargets.Interface)]
public sealed class RequireImplementorsAttribute : Attribute { }

[RequireImplementors]
public interface IRouted { Type RouteType { get; } }
```

Test result (2026-11-01, Unity `6000.6.6f1`, Windows player, Mono and IL2CPP, stripping level High). The test used a separate project with members in an assembly with no engine references. The members implement `IHandler : IRouted`, one member has a constructor parameter, and no code names a member.

| Build | Protection | Members found | Members built |
|---|---|---|---|
| M | none, stripping level Minimal | 3 | 3 |
| A | none | 0 | 0 |
| B | `link.xml` that an `IUnityLinkerProcessor` writes at build time from `TypeScan` | 3 | 3 |
| C1 | our own `RequireImplementorsAttribute`, namespace `Test.Core`, on `IRouted` | 3 | 3 |
| C2 | our own copy with Unity's full name `UnityEngine.Scripting.RequireImplementorsAttribute` | 3 | 3 |

- A clean rerun confirms A and C1 with two separate Unity launches and with no `link.xml` generator in the project. "Before" has no attribute and gives 0 / 0. "After" adds only the `[RequireImplementors]` line in the source and gives 3 / 3.
- Build A shows that the problem is real: the linker removes all members.
- C1 shows that the linker reads the attribute by its short name. The namespace and the engine reference are not necessary. The attribute also passes through a derived interface and keeps the constructors.
- The build sizes of A, B, C1 and C2 are the same (about 8.6 MB of managed code). Both fixes keep only the member types.

Decision: use C1. It needs no maintenance and no Editor code. Unity does not document that the linker reads the attribute by name. Thus the smoke build (§ 11) runs again after each Unity upgrade. If a Unity upgrade breaks C1, use B. Unity documents B and it has the same result.

IL2CPP gives the same result. A before and after rerun with `ScriptingImplementation.IL2CPP` (two separate Unity launches, stripping level High) gives 0 / 0 with no attribute and 3 / 3 with the attribute.

A second IL2CPP test (stripping level High, one build without the attributes and one build with them) examined our own copies of both attributes on each kind of base type. Each case had two derived types, one with a constructor parameter. The cells give found / built. With no attribute, all cases gave 0 / 0.

| Base type | `RequireImplementors` | `RequireDerived` |
|---|---|---|
| interface, class implementors | 2 / 2 | 0 / 0 |
| interface, record implementors | 2 / 2 | not tested |
| abstract class | 0 / 0 | 2 / 2 |
| base class, not abstract | 0 / 0 | 2 / 2 |
| abstract record | 0 / 0 | 2 / 2 |
| record, not abstract | not tested | 2 / 2 |

- `RequireImplementors` works only on an interface. The implementors can be classes or records.
- `RequireDerived` works only on a class or a record, abstract or not. It does not work on an interface.
- The linker reads both attributes by their class names, so `Framework.Routing` can hold both with no engine reference.
- `IRouted` is an interface, so the router needs only `RequireImplementors`. If a future scan root is a base class, use `RequireDerived` on it.

A third IL2CPP test used the Pipeline member shape (D13): `IRouted` ← abstract `Handler<TMessage, TScope>` ← abstract `CommandHandler<T>` ← sealed `PlayCardHandler : CommandHandler<PlayCard>`, and a direct subclass of `Handler<Command, ICommandScope>` with a constructor parameter. The scan root was the closed type `Handler<Command, ICommandScope>` and also the interface. With no attribute, all cases gave 0 / 0.

| Case | Attribute | Found / built |
|---|---|---|
| P1 | `RequireImplementors` on the routed interface only | 2 / 2 |
| P2 | `RequireDerived` on `Handler<,>` only | 2 / 2 |
| P3 | both | 2 / 2 |

`RequireImplementors` on `IRouted` keeps a class that gets `IRouted` from a base class, two levels down and through generic abstract bases. The Pipeline member types need no attribute of their own.

The Unity analyzer gives warning `UAC0005` on `AppDomain.GetAssemblies()`. It recommends `CurrentAssemblies.GetLoadedAssemblies()`, but that API is in `UnityEngine`, and `Framework.Routing` cannot reference it. In a player, the assemblies do not unload, so the warning applies only to the Editor after a domain reload. `TypeScan` clears its cache on a domain reload.

### 5.6 Question 6: exact dispatch, and a read for subtypes

| Read | Key match | Use |
|---|---|---|
| `Get`, `TryGet`, `GetAll` | exact `RouteType` | dispatch: one message to its member or members |
| `GetAllDerivedFrom(type)` | `RouteType` is `type`, or derives from it, implements it, or closes it | queries: "all readers for every `Note`", "all handlers for every `TagSetEvent<>`" |
| `RoutedTypesDerivedFrom(type)` | the same match | the registered route types themselves |

**Dispatch stays exact.** The router dispatches by exact runtime type (`msg.GetType()`).

- All current copies use the exact type.
- `command-service.md` defers base-type matching for validators "until a second validator on one command actually earns" it.
- Cross-cutting needs have other homes. "Every event" uses `EventStream.OnAppended`. "Every command" (the system gate) goes in the engine loop (roadmap phase 1).
- Closed generics are exact types. `TagSetEvent<int>` and `TagSetEvent<bool>` route apart, as they do today.

Risk: a concrete message type with a subclass. A handler for the parent then misses the child with no error. Rule ROUTE-03 removes this risk: every concrete leaf type is `sealed`.

**The subtype index.** The constructor builds it once, next to the exact index:

```csharp
// For each member m, in member order:
for (var t = m.RouteType; t != null && t != typeof(object); t = t.BaseType)
{
    Add(_under, t, m);
    if (t.IsGenericType) Add(_under, t.GetGenericTypeDefinition(), m);   // TagSetEvent<> finds TagSetEvent<int>
}
foreach (var i in m.RouteType.GetInterfaces()) Add(_under, i, m);
// Then freeze each list to an array. Add skips a member that is already in the list.
```

Rules for the subtype read:

- **Order is member order** (§ 5.2). Type depth has no effect, so there is no "base first or derived first" question.
- **Deterministic.** Both sides build the index from the same members, so both sides get the same result.
- **No allocation.** `GetAllDerivedFrom` and `RoutedTypesDerivedFrom` return stored arrays. A miss returns an empty array.
- **`object` is not a key.** The walk stops below `object`.
- **Exact members are included.** `GetAllDerivedFrom(typeof(Hello))` returns the `Hello` member too.
- **Cost.** Memory is about the member count multiplied by the depth of the type tree. The trees are small (two or three levels).

`GetAllDerivedFrom` does not change dispatch. A stage that wants base-type dispatch must call `GetAllDerivedFrom` itself and own that choice. No stage does this today.

## 6. Call-site migration

| Current type | New type | Change needed |
|---|---|---|
| `HandlerRegistry` | `WireEventDispatcher`, which holds a `TypeRouter<IWireEventHandler>` (`Multiple`) | Rename the class (D12). Keep its constructor and the `OnAppended` link. Replace the dictionary. Tests change only the class name. |
| `IWireEventHandler`, `WireEventHandler<T>` | `: IRouted` | Add the interface and one explicit property. |
| `CommandHandlerRegistry` | `TypeRouter<Handler<Command, ICommandScope>>` | Delete the class. The Pipeline calls `Get(cmd.GetType())`. |
| `CommandValidatorRegistry` | `TypeRouter<Validator<Command>>` | Delete the class. The Pipeline calls `TryGet(...)`. |
| `ICommandHandler`, `ICommandValidator` | `Handler<Command, ICommandScope>`, `Validator<Command>` | Pipeline step 2 removes the interfaces. The command migration uses the new types. |
| `CommandInstaller` | `RegisterTypeRouter<Handler<Command, ICommandScope>>(RouteMode.Single)`, `RegisterTypeRouter<Validator<Command>>(RouteMode.Single)` | Two lines change. |
| `EngineStack`, `CommandServiceTests` | `new TypeRouter<...>(list)` | Change the constructor calls. |
| `BootstrapWiringIntegrationTests` | none | Update two comments that name the old registries. |
| `HandlerRegistrationExtensions` | unchanged | The scan verb `RegisterTypeRouter<T>` lives in `Framework.Routing` (D14). The member verbs stay for tests and closed generics. |
| `Network._byType` | unchanged | None here. Blocks & Wires owns it. A named miss message is a proposal for that piece. |
| `Dispatcher._channels` | unchanged | None. |
| `EffectStore` (spike) | out of scope (D8) | None. The command pipeline piece decides its replacement. |

## 7. What stays specialized, and why

- **`Dispatcher._channels` (`byte` to channel, array).** It is on the hot receive path. The id is already a dense `byte`, so an array index beats any hash. The lookup-patterns doc calls this deliberate. It conforms to the contract: frozen after the build callback, throws on a miss.
- **`Network._byType` (`Type` to channel).** It is also the **id allocator**: `Register<T, TPacked>` assigns the next `byte` and fills the `Dispatcher`. Channels come from a build callback (`WireRegistrar`), not from a DI list. Its generic constraints (`IPackable<TPacked>`) do not fit `IRouted`. The record-ids piece may replace the allocator. The router should not absorb it.
- **Live tables (for example card hooks).** A router is frozen. Data that changes while a match runs is not a router. See § 5.4.
- **`SlotRegistry<TKey>._byType`.** This is a Registry (kind to identity), not a Router (message to handler). It stays a Registry.
- **Stage-specific glue.** The router only finds members. It does not invoke them. `WireEventDispatcher.Dispatch` loops and calls `Apply`. `CommandService` opens the block and calls `Handle`. Invoke logic stays with each stage, because each stage passes different context.

## 8. Rulebook rules to propose

Proposals only. `rulebook.json` does not change in this spike.

| Id | Title | Rule | Check kind | Status at start |
|---|---|---|---|---|
| ARCH-02 (edit) | Layer references | Change the `allowed` table as shown below (D2, D3). | `asmdef.allowedReferences` (exists) | enforced |
| ROUTE-01 | No hand-written type routers | A Framework runtime type does not declare a field of type `Dictionary<System.Type, *>`. Allowed: `Framework.Routing.TypeRouter*`, `Framework.Routing.TypeScan`, `Framework.Core.SlotRegistry*`, `Framework.Wire.Network`. Another piece that adds a live table adds its own entry. | new: `type.forbiddenFieldTypes` | advisory, then blocking after the command migration |
| ROUTE-02 | Routers are not static | A Framework runtime type does not declare a `static` field of type `TypeRouter<>` or `Dictionary<System.Type, *>`. Allowed: `Framework.Routing.TypeScan` (a cache of types, the same for every scope). | new: `type.noStaticFieldsOfType` | blocking |
| ROUTE-03 | Routed messages are sealed or abstract | Each subtype of `Framework.Wire.WireEvent` and `Framework.Commands.Command` in Framework runtime assemblies is `sealed` or `abstract`. | new: `type.sealedOrAbstract` | advisory, then blocking after the leaf messages are sealed |
| ROUTE-04 | Router members derive from IRouted | Each type that a `TypeRouter<>` collects comes through `IRouted`. The generic constraint enforces this, so this rule is a test, not a check. | `attached` (unit test in `Framework.Routing.Tests`) | enforced |

**ARCH-02 rows.** Only these rows change. The other rows stay as they are. The ids and blocks pieces can add a row for a feature they create. Each row lists only the references that the D1 to D3 and D14 moves need.

| Assembly | Allowed today | Proposed | When |
|---|---|---|---|
| `Framework.Routing` (new) | n/a | `[]` | Build step B1 |
| `Framework.Core` | `[]` | `[]` (no change) | n/a |
| `Framework.Wire` | `[]` | `["Framework.Core", "Framework.Routing"]` | Migration |
| `Framework.Blocks` (new) | n/a | `["Framework.Core", "Framework.Wire"]` | Blocks piece |
| `Framework.Requests` | `["Framework.Wire"]` | `["Framework.Core", "Framework.Wire"]` | Ids piece |
| `Framework.Commands` | `["Framework.Wire"]` | `["Framework.Core", "Framework.Blocks", "Framework.Routing", "Framework.Wire"]` | Migration |
| `Framework.Engine` | `Wire, Tags, Entities, Requests, States, Commands` | add `"Framework.Core"`, `"Framework.Blocks"`, `"Framework.Routing"` | Migration |
| `Framework.Bootstrap` | `Core, Commands, Engine, Entities, Requests, States, Tags, Wire` | add `"Framework.Blocks"`, `"Framework.Routing"` | Migration |

The `Framework.Routing` row is the only change outside `Assets/Framework/Routing/` in the build. ARCH-02 fails on an assembly that is not in its table, so B1 cannot pass the gate without it.

ARCH-09 ("Wire holds no domain events") then has no exception. Its `allowed: ["Framework.Wire.Block*"]` list becomes `[]` after the Block types move (D3).

**New Core text.** Replace the "Shared primitives" line in `docs/feature-layout.md` § Where things live:

> **Shared primitives** → `Assets/Framework/Core/`. Typed ids (`EntityId`, …), id allocators (`IdAllocator<TId>`), frozen lookup primitives (`SlotRegistry<TKey>`) and attribute markers. `TypeRouter<T>` and `TypeScan` live in `Framework.Routing` (D14). Core holds no domain state, no wire events, no Unity engine references and no references to other Framework assemblies. If a type has domain behavior, it belongs in a feature.

Text for the `why` fields:

- ROUTE-01: "One router class for each mode. A copy drifts in its miss and duplicate behavior."
- ROUTE-02: "A static router leaks between the server and client scopes and between tests."
- ROUTE-03: "Dispatch is by exact type. A handler for a non-sealed concrete type misses its subclasses with no error."

## 9. Plan

The plan builds a new system next to the current systems (D14). No step edits, moves or migrates a current system. The diff of each step stays inside `Assets/Framework/Routing/`. The only exception is the ARCH-02 row for `Framework.Routing` in B1 (§ 8). The migration is one row in § 11. Run `powershell -NoProfile -File Tools/Rulebook/gate.ps1` after each step. Each step is one PR.

```
Assets/Framework/Routing/
├─ Runtime/     Framework.Routing.asmdef        (noEngineReferences, references VContainer)
└─ Tests/       Framework.Routing.Tests.asmdef
   └─ Fixtures/ neutral note types and readers
```

The system has no data, so it has no `Assets/` subfolder.

- **B1. Contract.** Make the folder, both asmdefs and the ARCH-02 row. Add `IRouted`, `IOrdered`, `RouteMode` and `TypeRouter<T>` to `Runtime/`. Add the fixtures `Note`, `Hello`, `Bye`, `Gen<T>` and their readers to `Tests/Fixtures/`. Add `TypeRouterTests`: duplicate throws with `Single`, `Get` throws on a miss and on many, `TryGet` miss, empty `GetAll`, order by `Order` then full name, no allocation on `GetAll`. Add `TypeRouterDerivedFromTests`: base class, interface, open generic definition, exact type included, member order kept, empty on a miss, no allocation. ROUTE-04 is the constraint test in `TypeRouterTests`.
- **B2. Scan.** Add `TypeScan` to `Runtime/`, with `ConcreteTypesOf(Type)` and `ConcreteTypesIn(Assembly, Type)`. Add `TypeScanTests`: `ConcreteTypesIn` on the test assembly finds the fixture readers and skips abstract types and open generics. `ConcreteTypesOf` skips the test assembly. A second call returns the cached list.
- **B3. Stripping.** Add `RequireImplementorsAttribute` and `RequireDerivedAttribute` to `Runtime/` (§ 5.5). Put `[RequireImplementors]` on `IRouted`. Add a test that `IRouted` has the attribute.
- **B4. Registration verb.** Add `RoutingRegistrationExtensions.RegisterTypeRouter<T>(mode, scan)` to `Runtime/` (§ 5.5). Add `RegistrationTests`: with `scan: false` and one explicit fixture member, the container resolves a `TypeRouter<T>` with one routed type.

## 10. Interfaces the other three pieces need from this piece

**Record ids and allocation.**

- The router keys on `Type`, never on an allocated id. Id allocation and routing stay separate.
- If a record kind travels by `int` slot, the receiver rehydrates the slot to a `Type` (through the registry) and then routes by `Type`. Or it uses a slot-indexed array, as `Dispatcher` does. This piece does not provide a slot router.
- Composition order: ids are allocated before any router is built. The router constructor reads no ids.

**Block and wire system.**

- `HandlerRegistry` becomes `WireEventDispatcher` (D12). It keeps its public constructor `(EventStream, IReadOnlyList<IWireEventHandler>)`. Internally it holds a `TypeRouter<IWireEventHandler>` with `RouteMode.Multiple`.
- `IWireEventHandler : IRouted`. A new apply-handler base must implement `IRouted.RouteType`.
- `Framework.Wire` references `Framework.Routing` at migration (D14).
- `Framework.Blocks` owns `BlockStartEvent`, `BlockEndEvent`, `BlockScope` and `BlockId` (D3). Block events have no special route. Their apply handlers are found by the scan and route by exact type, as every other event does. The leaf Block events are `sealed` (ROUTE-03).
- The router does not carry, store or compute a block parent. The wire carries `CauseId` only, and the receiver gets the parent from bracket order (D5). No router API takes a parent or a block id.
- `Dispatcher` and `Network` stay as they are. The wire piece owns them.

**Command pipeline.**

- `TypeRouter<T>.Get(Type)` for exactly one handler. `TypeRouter<T>.TryGet(Type, out T)` for an optional member. `TypeRouter<T>.GetAll(Type)` with `RouteMode.Multiple` for fan-out.
- `GetAllDerivedFrom(Type)` and `RoutedTypesDerivedFrom(Type)` for a query over a type family. These reads do not dispatch.
- `TypeRouter<T>.RoutedTypes` for a composition check, for example "each command type in the catalog has a handler".
- `IOrdered` for any stage that needs a stable member order.
- Each level (intent, effect, command, event) owns its own router instance. One `Enqueue` pipeline selects the stage by message kind. It then asks that stage's router. The router does not know about stages, blocks or `Task`. An intent route opens no block (D6); the block decision belongs to the stage, not the router.
- Hooks are not in this piece (D8). If the pipeline keeps frozen game-wide hooks in a `TypeRouter<IHook>` (`Multiple`), it owns the split by phase, the live table, the merge and the tie rule.

## 11. Later

| Item | Why later | Trigger to reopen |
|---|---|---|
| Migration of current systems (§ 6) | The plan is an extraction (D14) | Catalog cross-spike order reaches the router |
| Rule checks ROUTE-01 to ROUTE-03 | They live in `Assets/Tools/Rulebook/` | Migration starts |
| Stripping smoke build harness | It is a separate Unity project | First player build, or a Unity upgrade |
| Composition-time completeness check | No list of known command types yet | A command catalog exists |
| Rule: each scan root has a keep attribute | `IRouted` is the only scan root | A second scan root appears |
| Build-time `link.xml` generator | The attribute passes the smoke build | The smoke build fails after an upgrade |
| Slot router (`int` slot to member) | `Dispatcher` already uses a slot array | A second slot lookup appears |

Migration order, from the earlier plan:

1. Wire: add the `Framework.Routing` reference, make `IWireEventHandler : IRouted`, rename `HandlerRegistry` to `WireEventDispatcher` with a `TypeRouter<IWireEventHandler>` (`Multiple`). Gate with `-With playmode`.
2. Commands: after Pipeline step 2. Replace both registries with `TypeRouter<T>` of the Pipeline types. Then set ROUTE-01 to blocking.
3. Installers: use `RegisterTypeRouter<T>` in `CommandInstaller` and `EngineInstaller`. Keep `RegisterTagValueType<T>()`.
4. Seal the leaf `WireEvent` types (§ 12). Then set ROUTE-03 to blocking. Gate with `-With playmode`.
5. Docs: `docs/lookup-patterns.md` § Router and the `docs/architecture.md` codemap.

The completeness check can be unnecessary. `RoutedTypes` already lists the routed types, so a later check or a debug listing can use it.

## 12. Closed items

- ~~**Home assembly.**~~ Closed by D14: `Framework.Routing`, in its own folder. D2 first chose `Framework.Core`. § 8 gives the ARCH-02 rows.
- ~~**Sealing and AutoPacker.**~~ Answered: AutoPacker accepts `sealed partial`. No fallback is necessary. Evidence:
  - The package ships only a compiled generator, `Library/PackageCache/com.scaffold.autopacker@fd08e7dbfb00/Runtime/AutoPackerGenerator.dll`. Its README says the generator source lives in `Generators/AutoPacker/` of the Scaffold repository, not in the package. There is no `.cs` source to cite by line.
  - The generator writes the type header from three parts: accessibility, `" partial "` and the type keyword (`GetAccessibilityKeyword`, `GetTypeKeyword`, `EmitPartial` in the DLL strings). It never writes `sealed`. In C#, `sealed` on one partial part seals the whole type, so the user part can carry it alone.
  - Test build (scratchpad, .NET 10 SDK, the same DLL as analyzer): `[AutoPack] abstract partial class WireEvent`, `sealed partial class LeafEvent : WireEvent`, and `sealed partial class LeafGen<T> : WireEvent where T : unmanaged`. It compiled with no diagnostics. A `Pack` and `new LeafEvent(packed)` round trip printed `3 sealed=True`.
  - The generated `LeafEvent.Packed.g.cs:4` is `public partial class LeafEvent : IPackable, …`. Lines `:11` and `:25` are `public override` (`Unpack`, `Pack`). `override` is legal in a sealed class. `PackTyped` and `UnpackTyped` (`:20`, `:14`) are not virtual.
  - `WireRegistrar` filters only on `IsAbstract` and `IsGenericTypeDefinition` (`Assets/Framework/Wire/Runtime/Network/WireRegistrar.cs:71`, `:77`). It has no `IsSealed` check.
  - Residual risk: the generator has a `virtual` emit path (`HasUserDeclaredVirtualOrAbstractPackOrUnpack`). A new `virtual` member in a sealed class is error CS0549. The test did not reach that path for a leaf. If a future leaf declares its own virtual `Pack`, the build fails at once, so the risk is visible, not silent.
- ~~**Hooks, `HookTable`, `_direct`, hook tie rule.**~~ Moved to the command pipeline piece by D8.
- ~~**Names.**~~ `HandlerRegistry` becomes `WireEventDispatcher` (D12).
- ~~**Subtype read scope.**~~ `GetAllDerivedFrom` includes interfaces and open generic definitions (D12).
- ~~**Many members for one type.**~~ The router supports `Single` and `Multiple` for each router, set at registration. The Pipeline chooses the mode for validators and handlers, and how a command runs many members (D12).
