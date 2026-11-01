---
type: spike
status: proposed
created: 2026-10-31
references:
  - docs/lookup-patterns.md
  - docs/architecture.md
  - Product/spikes/framework-roadmap.md
  - Product/spikes/command-service.md
sandbox: Assets/Framework/Core/
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

1. One generic class in `Framework.Core`: `TypeRouter<T>`. It is a frozen map from a `Type` to its members. A `RouteArity` (`One` or `Many`) sets the duplicate rule (D10).
2. One member contract, `IRouted { Type RouteType }`, and one optional `IOrdered { int Order }`.
3. Registration is one line for each router: `RegisterTypeRouter<T>(arity)`. It scans the loaded assemblies for members (D9). `TypeScan` scans once and caches the result, so other systems can use the same scan.
4. Dispatch reads (`For`, `TryFor`, `All`) use the **exact type only**. Leaf message types become `sealed`, so an exact route cannot miss a subclass.
5. A query read, `AllUnder(type)`, gives the members of every registered type under `type`: subclasses, implementations and closed generics (§ 5.6).
6. `Dispatcher` (byte array) and `Network._byType` (id allocator) stay specialized. Hooks are out of scope (D8).

### Decisions (2026-10-31, D9 to D11 on 2026-11-01)

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
| D8 | The router scope is type lookup only: register routed members, get the member for a type, get the members under a type. Hooks and hook tables are out of scope. | § 5.4 removes the hook design. § 5.6 adds the subtype read `AllUnder`. |
| D9 | Members are found by an assembly scan, not by one registration line for each member. Source generation can come later, as an optional V2 plugin. | § 5.5. The explicit verbs stay only for tests and for closed generic members. |
| D10 | One router class, `TypeRouter<T>`, with a `RouteArity` option. `TypeMultiRouter<T>` is removed. | § 5.1, § 5.2. A wrong read is a run-time error, not a compile error. |
| D11 | The API and the examples are about type lookup, with neutral types. Commands and events are users of the router, not its API. | § 4. |

## 2. Scope

The user approved this table (2026-11-01). The design sections cover only the concerns marked "in".

| Concern | In / out | Owner if out |
|---|---|---|
| Member contract: `IRouted` (route type), `IOrdered` (order) | in | — |
| DI registration: one style for members and routers | in | — |
| Exact read, one member: `For` / `TryFor`, with the duplicate and miss rules | in | — |
| Exact read, N members in a stable order: `All` | in | — |
| Subtype read: `AllUnder` / `TypesUnder` | in | — |
| Composition check of the registered types: `Types` | in | — |
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

- The three Framework `Type` copies have the same key, the same backing and the same DI feed. Only the arity and the miss behavior change.
- `CommandValidatorRegistry` is a verbatim copy of `CommandHandlerRegistry` (see `command-service.md` § Phase 1 build notes). Only the read verb is different.
- The three helpers in `HandlerRegistrationExtensions` have identical bodies.
- `EffectStore._hooks` sorts with `List.Sort`, which is not stable. Two hooks with the same priority can change order between runs.
- `EffectStore.Subscribers` allocates a new array (`ToArray`) on each read.

## 4. API

`TypeRouter<T>` is a frozen map from a `Type` to the members of type `T` that route to it. `TypeScan` finds the members. The composition root registers one router for each member interface.

| Audience | Entry point | Does |
|---|---|---|
| member author | `IRouted.RouteType` (optional `IOrdered.Order`) | Says which type the member is for. |
| composition | `builder.RegisterTypeRouter<T>(RouteArity arity)` | Scans for the members of `T` and registers the router. |
| runtime | `For`, `TryFor`, `All` | Dispatch read by exact type. |
| runtime | `AllUnder`, `TypesUnder` | Query read over a type family. Not for dispatch. |
| composition check | `Types` | Lists the registered route types. |

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
builder.RegisterTypeRouter<INoteReader>(RouteArity.One);

// 4. Runtime: DI injects the router. The class reads it.
public sealed class NoteDesk
{
    readonly TypeRouter<INoteReader> _readers;
    public NoteDesk(TypeRouter<INoteReader> readers) => _readers = readers;

    public void Take(Note note) => _readers.For(note.GetType()).Read(note);    // exact type
    public IReadOnlyList<INoteReader> AllReaders() => _readers.AllUnder(typeof(Note));
}
```

Who uses it (the framework levels):

| Use | Member interface | Arity | Read |
|---|---|---|---|
| command handlers | `ICommandHandler` | `One` | `For` |
| command validators | `ICommandValidator` | `One` | `TryFor` |
| event apply handlers | `IWireEventHandler` | `Many` | `All` |
| effect and intent handlers (later) | the Pipeline decides | `One` | `For` |

Dependencies:

| From | To | Why |
|---|---|---|
| `Framework.Core` (`TypeRouter<T>`, `TypeScan`, `IRouted`) | nothing | Core references no other assembly. |
| `Framework.Bootstrap` (`RegisterTypeRouter<T>`) | `Framework.Core`, VContainer | The DI verb lives with the other DI verbs. |
| Member assemblies (Commands, Wire, game code) | `Framework.Core` | Members implement `IRouted`. |

Rules. "Gate" is `Tools/Rulebook/gate.ps1`. It checks the rules in `Tools/Rulebook/rulebook.json` and fails the build. The `ROUTE-*` rules are new proposals from this spike (§ 8).

| Rule | Enforced by |
|---|---|
| Code does not write its own `Dictionary<Type, *>` router. | Gate (proposed ROUTE-01) |
| Code does not keep a router in a `static` field. | Gate (proposed ROUTE-02) |
| A concrete message type is `sealed`, so exact dispatch cannot miss a subclass. | Gate (proposed ROUTE-03) |
| A member gives its route type. | Compiler: the constraint `where T : class, IRouted` |
| No member is added after composition. | No method exists for it. Only the constructor writes. |
| A route type has a member when `For` reads it. | Run time: `For` throws. No earlier check exists yet (open question 1). |
| Dispatch uses exact reads, not `AllUnder`. | Code review only. |

## 5. Generalized contract

### 5.1 Decision on question 1: one class, one contract

`docs/lookup-patterns.md` says "Standardize the contract, not the class" for Routers. This spike shares the class where key, backing and feed are identical. That is the case for every `Type` router that DI fills from a list.

The first version of this spike had two classes, one for each arity. The user chose one class (D10). It is easier to learn, and one DI verb covers every level. The arity becomes a constructor option:

- `RouteArity.One`: two members with the same route type throw in the constructor. `For` and `TryFor` are the normal reads.
- `RouteArity.Many`: many members for one route type are legal. `All` is the normal read. `For` and `TryFor` throw if the type has more than one member.

Cost: a wrong read (for example `For` on a type with two members) is a run-time error, not a compile error. The error text names the router and the type.

### 5.2 Signatures

Namespace `Framework.Core`, folder `Assets/Framework/Core/Runtime/Routing/`. One top-level type per file.

```csharp
public interface IRouted  { Type RouteType { get; } }
public interface IOrdered { int Order { get; } }   // lower runs first; equal values sort by full type name

public enum RouteArity { One, Many }
```

```csharp
public sealed class TypeRouter<T> where T : class, IRouted
{
    public TypeRouter(IReadOnlyList<T> members, RouteArity arity);

    public T    For(Type type);                     // exactly one member; throws on a miss or on many
    public bool TryFor(Type type, out T member);    // false on a miss; throws on many
    public IReadOnlyList<T> All(Type type);         // 0..N in order; empty on a miss; no allocation

    public IReadOnlyList<T>    AllUnder(Type type);   // § 5.6
    public IReadOnlyList<Type> TypesUnder(Type type); // § 5.6
    public IReadOnlyCollection<Type> Types { get; }   // composition checks only
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
- A duplicate with `RouteArity.One` throws `InvalidOperationException`: `Duplicate {T} for {type}.`
- A miss in `For` throws `InvalidOperationException`: `No {T} registered for {type}.` The current messages carry the same facts.
- `All` returns a stored array. Callers iterate with `for`, not `foreach`, to keep the hot path free of enumerator allocation.
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

`ICommandHandler`, `ICommandValidator`, `CommandHandler<T>` and `CommandValidator<T>` get the same two lines.

### 5.3 Question 2: composite keys

The router key is `Type` and nothing else. A second key part goes one of two ways:

1. **Small closed set** (for example a phase enum with two values). Partition, do not compose. The caller keeps one router for each value, in an array indexed by the value. The router itself does not know about the partition.
2. **Open set** (for example `entityId` in `_direct`). This is live data, not a route. It belongs in a Store or in a live table that another piece owns.

If a frozen route needs a composite key that fits neither case, write a `readonly struct` key that implements `IEquatable<TKey>`. Do not use a `ValueTuple` key. This case does not exist today, so no generic `Router<TKey, T>` ships now.

### 5.4 Question 3: hooks are out of scope

Hooks are not part of this piece (D8). The command pipeline piece owns the frozen game-wide hooks, the live card hooks (`HookTable`), the merge of the two, the tie rule and `_direct`. It can use `TypeRouter<IHook>` with `RouteArity.Many` as a normal fan-out router.

### 5.5 Question 4: registration by scan

One DI verb for every level (D9):

```csharp
public static void RegisterTypeRouter<T>(this IContainerBuilder b, RouteArity arity, bool scan = true)
    where T : class, IRouted
{
    if (scan)
        foreach (var type in TypeScan.ConcreteTypesOf(typeof(T)))
            b.Register(type, Lifetime.Singleton).As<T>();   // VContainer Register(Type, Lifetime)
    b.Register<TypeRouter<T>>(Lifetime.Singleton).WithParameter(arity);
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

- **Tests** that need a subset: `RegisterTypeRouter<T>(arity, scan: false)` and then `RegisterCommandHandler<THandler>()` for each member.
- **Closed generic members.** The scan cannot close `TagSetHandler<T>`. `RegisterTagValueType<T>()` stays and registers `TagSetHandler<int>` and the other closed types explicitly.

**IL2CPP stripping.** A member that only the scan reaches has no direct reference, so the linker can remove it at stripping level Low or higher. An attribute on the base class does not help here. Unity's `[RequireImplementors]` needs `UnityEngine`, and `Framework.Core` and `Framework.Commands` have `noEngineReferences: true`. A `[Preserve]` attribute does not pass to subclasses. The proposal is a `link.xml` that keeps the Framework assemblies and the game assembly. Validate with one IL2CPP build at stripping level Low (step 1b).

### 5.6 Question 6: exact dispatch, and a read for subtypes

| Read | Key match | Use |
|---|---|---|
| `For`, `TryFor`, `All` | exact `RouteType` | dispatch: one message to its member or members |
| `AllUnder(type)` | `RouteType` is `type`, or derives from it, implements it, or closes it | queries: "all readers for every `Note`", "all handlers for every `TagSetEvent<>`" |
| `TypesUnder(type)` | the same match | the registered route types themselves |

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
- **No allocation.** `AllUnder` and `TypesUnder` return stored arrays. A miss returns an empty array.
- **`object` is not a key.** The walk stops below `object`.
- **Exact members are included.** `AllUnder(typeof(Hello))` returns the `Hello` member too.
- **Cost.** Memory is about the member count multiplied by the depth of the type tree. The trees are small (two or three levels).

`AllUnder` does not change dispatch. A stage that wants base-type dispatch must call `AllUnder` itself and own that choice. No stage does this today.

## 6. Call-site migration

| Current type | New type | Change needed |
|---|---|---|
| `HandlerRegistry` (internals) | `TypeRouter<IWireEventHandler>` (`Many`) inside `HandlerRegistry` | Keep the class, its constructor and the `OnAppended` link. Replace the dictionary. No test call site changes. |
| `IWireEventHandler`, `WireEventHandler<T>` | `: IRouted` | Add the interface and one explicit property. |
| `CommandHandlerRegistry` | `TypeRouter<ICommandHandler>` | Delete the class. `CommandService` calls `_handlers.For(cmd.GetType())`. |
| `CommandValidatorRegistry` | `TypeRouter<ICommandValidator>` | Delete the class. `CommandService` calls `_validators.TryFor(...)`. |
| `ICommandHandler`, `ICommandValidator` and their bases | `: IRouted` | Add the interface and one explicit property. |
| `CommandInstaller` | `RegisterTypeRouter<ICommandHandler>(RouteArity.One)`, `RegisterTypeRouter<ICommandValidator>(RouteArity.One)` | Two lines change. |
| `EngineStack`, `CommandServiceTests` | `new TypeRouter<...>(list)` | Change the constructor calls. |
| `BootstrapWiringIntegrationTests` | none | Update two comments that name the old registries. |
| `HandlerRegistrationExtensions` | `RegisterTypeRouter<T>(arity)` | Add the scan verb. The member verbs stay for tests and closed generics. |
| `Network._byType` | unchanged | None here. Blocks & Wires owns it. A named miss message is a proposal for that piece. |
| `Dispatcher._channels` | unchanged | None. |
| `EffectStore` (spike) | out of scope (D8) | None. The command pipeline piece decides its replacement. |

## 7. What stays specialized, and why

- **`Dispatcher._channels` (`byte` to channel, array).** It is on the hot receive path. The id is already a dense `byte`, so an array index beats any hash. The lookup-patterns doc calls this deliberate. It conforms to the contract: frozen after the build callback, throws on a miss.
- **`Network._byType` (`Type` to channel).** It is also the **id allocator**: `Register<T, TPacked>` assigns the next `byte` and fills the `Dispatcher`. Channels come from a build callback (`WireRegistrar`), not from a DI list. Its generic constraints (`IPackable<TPacked>`) do not fit `IRouted`. The record-ids piece may replace the allocator. The router should not absorb it.
- **Live tables (for example card hooks).** A router is frozen. Data that changes while a match runs is not a router. See § 5.4.
- **`SlotRegistry<TKey>._byType`.** This is a Registry (kind to identity), not a Router (message to handler). It stays a Registry.
- **Stage-specific glue.** The router only finds members. It does not invoke them. `HandlerRegistry.Dispatch` loops and calls `Apply`. `CommandService` opens the block and calls `Handle`. Invoke logic stays with each stage, because each stage passes different context.

## 8. Rulebook rules to propose

Proposals only. `rulebook.json` does not change in this spike.

| Id | Title | Rule | Check kind | Status at start |
|---|---|---|---|---|
| ARCH-02 (edit) | Layer references | Change the `allowed` table as shown below (D2, D3). | `asmdef.allowedReferences` (exists) | enforced |
| ROUTE-01 | No hand-written type routers | A Framework runtime type does not declare a field of type `Dictionary<System.Type, *>`. Allowed: `Framework.Core.TypeRouter*`, `Framework.Core.SlotRegistry*`, `Framework.Core.TypeScan`, `Framework.Wire.Network`. Another piece that adds a live table adds its own entry. | new: `type.forbiddenFieldTypes` | advisory, then blocking after step 3 |
| ROUTE-02 | Routers are not static | A Framework runtime type does not declare a `static` field of type `TypeRouter<>` or `Dictionary<System.Type, *>`. Allowed: `Framework.Core.TypeScan` (a cache of types, the same for every scope). | new: `type.noStaticFieldsOfType` | blocking |
| ROUTE-03 | Routed messages are sealed or abstract | Each subtype of `Framework.Wire.WireEvent` and `Framework.Commands.Command` in Framework runtime assemblies is `sealed` or `abstract`. | new: `type.sealedOrAbstract` | advisory, then blocking after step 5 |
| ROUTE-04 | Router members derive from IRouted | Each type that a `TypeRouter<>` collects comes through `IRouted`. The generic constraint enforces this, so this rule is a test, not a check. | `attached` (unit test in `Framework.Core.Tests`) | enforced |

**ARCH-02 rows.** Only these rows change. The other rows stay as they are. The ids and blocks pieces can add a row for a feature they create. Each row lists only the references that the D1 to D3 moves need.

| Assembly | Allowed today | Proposed |
|---|---|---|
| `Framework.Core` | `[]` | `[]` (no change) |
| `Framework.Wire` | `[]` | `["Framework.Core"]` |
| `Framework.Blocks` (new) | n/a | `["Framework.Core", "Framework.Wire"]` |
| `Framework.Requests` | `["Framework.Wire"]` | `["Framework.Core", "Framework.Wire"]` |
| `Framework.Commands` | `["Framework.Wire"]` | `["Framework.Core", "Framework.Blocks", "Framework.Wire"]` |
| `Framework.Engine` | `Wire, Tags, Entities, Requests, States, Commands` | add `"Framework.Core"`, `"Framework.Blocks"` |
| `Framework.Bootstrap` | `Core, Commands, Engine, Entities, Requests, States, Tags, Wire` | add `"Framework.Blocks"` |

ARCH-09 ("Wire holds no domain events") then has no exception. Its `allowed: ["Framework.Wire.Block*"]` list becomes `[]` after the Block types move (D3).

**New Core text.** Replace the "Shared primitives" line in `docs/feature-layout.md` § Where things live:

> **Shared primitives** → `Assets/Framework/Core/`. Typed ids (`EntityId`, …), id allocators (`IdAllocator<TId>`), frozen lookup primitives (`SlotRegistry<TKey>`, `TypeRouter<T>`), the type scan (`TypeScan`) and attribute markers. Core holds no domain state, no wire events, no Unity engine references and no references to other Framework assemblies. If a type has domain behavior, it belongs in a feature.

Text for the `why` fields:

- ROUTE-01: "One router class for each arity. A copy drifts in its miss and duplicate behavior."
- ROUTE-02: "A static router leaks between the server and client scopes and between tests."
- ROUTE-03: "Dispatch is by exact type. A handler for a non-sealed concrete type misses its subclasses with no error."

## 9. Migration steps

Each step is one PR. Run `powershell -NoProfile -File Tools/Rulebook/gate.ps1` after each step. Add `-With playmode` for steps that touch Wire.

1. **Add the contract.** Add `IRouted`, `IOrdered`, `RouteArity`, `TypeRouter<T>` and `TypeScan` to `Framework.Core`. Add unit tests in `Framework.Core.Tests`: duplicate throws with `One`, `For` throws on a miss and on many, `TryFor` miss, empty `All`, order by `Order` then full name, no allocation on `All`. For `TypeScan`: finds concrete types, skips abstract types, open generics and test assemblies, caches. For `AllUnder` and `TypesUnder`: base class, interface, open generic definition, exact type included, member order kept, empty on a miss, no allocation. No caller changes.
   1b. **IL2CPP check.** Build one IL2CPP player at stripping level Low with the `link.xml`. Make sure that the scan finds a member that nothing references directly.
2. **Wire.** Apply the `Framework.Wire` row of the ARCH-02 edit (D2) and the new Core text. Add `Framework.Core` to `Framework.Wire.asmdef`. Make `IWireEventHandler : IRouted`. Change `HandlerRegistry` internals to `TypeRouter<IWireEventHandler>` with `RouteArity.Many`. Gate with `-With playmode`.
3. **Commands.** Make `ICommandHandler` and `ICommandValidator` derive from `IRouted`. Replace both registries with `TypeRouter<T>`. Update `CommandService`, `CommandInstaller`, `EngineStack`, `CommandServiceTests`. Delete the two registry files and their `.meta` files.
4. **Registration.** Add `RegisterTypeRouter<T>(arity, scan)` to `HandlerRegistrationExtensions`. Use it in `CommandInstaller` and `EngineInstaller`. Delete the member registration lines that the scan now covers. Keep `RegisterTagValueType<T>()`.
5. **Seal leaf messages.** Add `sealed` to the concrete `WireEvent` subtypes (`EntityCreatedEvent`, `StatePushEvent`, `TagSetEvent<T>`, `TagChangeEvent<T>`, `TagRemovedEvent`, `BlockStartEvent` and `BlockEndEvent` in their new `Framework.Blocks` home, the Multiplayer events). AutoPacker and `WireRegistrar` accept `sealed partial` (§ 11, answered). Gate with `-With playmode`.
6. **Rules.** Ask for approval, then add ROUTE-01 to ROUTE-04 to `rulebook.json`. Add the new check kinds in `Assets/Tools/Rulebook/Editor/Checks/` with fixture tests.
7. **Docs.** Update `docs/lookup-patterns.md` § Router: name the shared classes and the two kinds of read (exact and under). Update the `docs/architecture.md` codemap.

## 10. Interfaces the other three pieces need from this piece

**Record ids and allocation.**

- The router keys on `Type`, never on an allocated id. Id allocation and routing stay separate.
- If a record kind travels by `int` slot, the receiver rehydrates the slot to a `Type` (through the registry) and then routes by `Type`. Or it uses a slot-indexed array, as `Dispatcher` does. This piece does not provide a slot router.
- Composition order: ids are allocated before any router is built. The router constructor reads no ids.

**Block and wire system.**

- `HandlerRegistry` keeps its public constructor `(EventStream, IReadOnlyList<IWireEventHandler>)`. Internally it holds a `TypeRouter<IWireEventHandler>` with `RouteArity.Many`.
- `IWireEventHandler : IRouted`. A new apply-handler base must implement `IRouted.RouteType`.
- `Framework.Wire` references `Framework.Core` (D2).
- `Framework.Blocks` owns `BlockStartEvent`, `BlockEndEvent`, `BlockScope` and `BlockId` (D3). Block events have no special route. Their apply handlers are found by the scan and route by exact type, as every other event does. The leaf Block events are `sealed` (ROUTE-03).
- The router does not carry, store or compute a block parent. The wire carries `CauseId` only, and the receiver gets the parent from bracket order (D5). No router API takes a parent or a block id.
- `Dispatcher` and `Network` stay as they are. The wire piece owns them.

**Command pipeline.**

- `TypeRouter<T>.For(Type)` for exactly one handler. `TypeRouter<T>.TryFor(Type, out T)` for an optional member. `TypeRouter<T>.All(Type)` with `RouteArity.Many` for fan-out.
- `AllUnder(Type)` and `TypesUnder(Type)` for a query over a type family. These reads do not dispatch.
- `TypeRouter<T>.Types` for a composition check, for example "each command type in the catalog has a handler".
- `IOrdered` for any stage that needs a stable member order.
- Each level (intent, effect, command, event) owns its own router instance. One `Enqueue` pipeline selects the stage by message kind. It then asks that stage's router. The router does not know about stages, blocks or `Task`. An intent route opens no block (D6); the block decision belongs to the stage, not the router.
- Hooks are not in this piece (D8). If the pipeline keeps frozen game-wide hooks in a `TypeRouter<IHook>` (`Many`), it owns the split by phase, the live table, the merge and the tie rule.

## 11. Open questions

Closed:

- ~~**Home assembly.**~~ Closed by D2: `Framework.Core`. § 8 gives the ARCH-02 rows and the new Core text.
- ~~**Sealing and AutoPacker.**~~ Answered: AutoPacker accepts `sealed partial`. No fallback is necessary. Evidence:
  - The package ships only a compiled generator, `Library/PackageCache/com.scaffold.autopacker@fd08e7dbfb00/Runtime/AutoPackerGenerator.dll`. Its README says the generator source lives in `Generators/AutoPacker/` of the Scaffold repository, not in the package. There is no `.cs` source to cite by line.
  - The generator writes the type header from three parts: accessibility, `" partial "` and the type keyword (`GetAccessibilityKeyword`, `GetTypeKeyword`, `EmitPartial` in the DLL strings). It never writes `sealed`. In C#, `sealed` on one partial part seals the whole type, so the user part can carry it alone.
  - Test build (scratchpad, .NET 10 SDK, the same DLL as analyzer): `[AutoPack] abstract partial class WireEvent`, `sealed partial class LeafEvent : WireEvent`, and `sealed partial class LeafGen<T> : WireEvent where T : unmanaged`. It compiled with no diagnostics. A `Pack` and `new LeafEvent(packed)` round trip printed `3 sealed=True`.
  - The generated `LeafEvent.Packed.g.cs:4` is `public partial class LeafEvent : IPackable, …`. Lines `:11` and `:25` are `public override` (`Unpack`, `Pack`). `override` is legal in a sealed class. `PackTyped` and `UnpackTyped` (`:20`, `:14`) are not virtual.
  - `WireRegistrar` filters only on `IsAbstract` and `IsGenericTypeDefinition` (`Assets/Framework/Wire/Runtime/Network/WireRegistrar.cs:71`, `:77`). It has no `IsSealed` check.
  - Residual risk: the generator has a `virtual` emit path (`HasUserDeclaredVirtualOrAbstractPackOrUnpack`). A new `virtual` member in a sealed class is error CS0549. The test did not reach that path for a leaf. If a future leaf declares its own virtual `Pack`, the build fails at once, so the risk is visible, not silent.

Still open:

- ~~**Hooks, `HookTable`, `_direct`, hook tie rule.**~~ Moved to the command pipeline piece by D8.

Still open:

1. **Composition-time completeness.** Should the engine check at build time that each known command type has a handler, through `TypeRouter<T>.Types`? This needs a list of known command types, which does not exist yet.
2. **Validator fan-out.** If the engine loop adds more than one validator for each command, the validator router changes from `RouteArity.One` to `RouteArity.Many`, with `IOrdered`. Confirm that this stays deferred.
3. **Names.** Rename `HandlerRegistry` to `ApplyDispatcher` later? It dispatches; it does not register kinds.
4. **Subtype read scope.** `AllUnder` includes interfaces and open generic definitions. Confirm both. If interfaces are not necessary, the index is smaller.
