---
type: spike
status: proposed
created: 2026-10-31
references:
  - docs/lookup-patterns.md
  - docs/architecture.md
  - Product/spikes/framework-roadmap.md
  - Product/spikes/command-service.md
  - Product/spikes/hook-config-design.md
sandbox: Assets/Framework/Core/
tags: [#spike, #infra, #router, #wire, #commands, #hooks]
---

# Shared infrastructure — type-keyed router

> **Status: proposed.** This spike is one of four parallel pieces of the shared
> pipeline infrastructure (intent, effect, command, event). The other pieces are
> record ids and allocation, the block and wire system, and the command pipeline.
> This piece owns the frozen `Type` to handler lookup. No code changes yet.

## 1. TL;DR

1. Two small generic classes in `Framework.Core`: `TypeRouter<T>` (0..1 for each type) and `TypeMultiRouter<T>` (0..N, stable order).
2. One member contract, `IRouted { Type RouteType }`, and one optional `IOrdered { int Order }`. All levels use the same two classes and one DI style.
3. Dispatch is by **exact type only**. Leaf message types become `sealed`, so an exact route cannot miss a subclass.
4. Game-wide hooks are a frozen `TypeMultiRouter<IHook>` for each phase. Card hooks are **not** a router. They go to a server-only `HookTable`, which uses the same read shape.
5. `Dispatcher` (byte array) and `Network._byType` (id allocator) stay specialized. They conform to the contract, not the class.

### Decisions (2026-10-31)

The user made these decisions for all four infrastructure spikes. This spike uses them.

| # | Decision | Effect on this spike |
|---|---|---|
| D1 | Ids are typed `readonly struct`s around `int` (`EntityId`, `RequestId`, `BlockId`, `MessageId`). | The router key stays `Type`. Typed ids key live data only (`HookTable` cleanup by `EntityId`). |
| D2 | Shared infrastructure lives in `Framework.Core` (routers, `IdAllocator<TId>`, `EntityId`). `Framework.Wire` references `Framework.Core`. | Closes the home question. § 6 gives the ARCH-02 rows and the new Core text. |
| D3 | A new `Framework.Blocks` feature owns all `Block*` types and `BlockId`. | `BlockStartEvent` and `BlockEndEvent` move out of Wire. They route like any other event (§ 8). |
| D4 | The async core uses `System.Threading.Tasks.Task`, not UniTask. | No effect. Router reads are synchronous. |
| D5 | The wire carries `CauseId` only. The nesting parent comes from bracket order. No parent override. | The router does not carry or compute block parents (§ 8). |
| D6 | An intent opens no block. | No effect. The intent router only finds a handler. |
| D7 | Hooks exist only at the command level. Game-wide hooks are frozen DI registrations. Card hooks go in a server-only `HookTable`. | § 3.4 splits hooks into a frozen router and a live table. |

## 2. Current shapes

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

## 3. Generalized contract

### 3.1 Decision on question 1: two classes, one contract

`docs/lookup-patterns.md` says "Standardize the contract, not the class" for Routers. This spike keeps that rule for routers that differ in key or backing. It shares the class only where key, backing and feed are identical. That is the case for every `Type` router that DI fills from a list.

A single class with a cardinality enum does not fit. Each arity has a different read surface (`For` returns one member, `All` returns a list). A single class must then expose reads that are illegal for its own arity. Two classes make the illegal read a compile error.

One and Optional share one class, `TypeRouter<T>`. They differ only in the read the caller uses. Many and Ordered share `TypeMultiRouter<T>`. Order is a property of the member, not of the router.

### 3.2 Signatures

Namespace `Framework.Core`, folder `Assets/Framework/Core/Runtime/Routing/`. One top-level type per file.

```csharp
namespace Framework.Core
{
    public interface IRouted
    {
        Type RouteType { get; }
    }
}
```

```csharp
namespace Framework.Core
{
    public interface IOrdered
    {
        // Lower runs first. Equal values keep registration order.
        int Order { get; }
    }
}
```

```csharp
namespace Framework.Core
{
    public sealed class TypeRouter<T> where T : class, IRouted
    {
        readonly Dictionary<Type, T> _byType = new();

        public TypeRouter(IReadOnlyList<T> members) { /* throws on a duplicate RouteType */ }

        public T    For(Type type);                    // One: throws on a miss
        public bool TryFor(Type type, out T member);   // Optional: false on a miss
        public bool Has(Type type);
        public IReadOnlyCollection<Type> Types { get; } // composition checks only
    }
}
```

```csharp
namespace Framework.Core
{
    public sealed class TypeMultiRouter<T> : IRouteSource<T> where T : class, IRouted
    {
        static readonly T[] None = Array.Empty<T>();
        readonly Dictionary<Type, T[]> _byType = new();

        public TypeMultiRouter(IReadOnlyList<T> members) { /* stable sort by IOrdered.Order, 0 when absent */ }

        public IReadOnlyList<T> All(Type type);   // empty on a miss; never null; no allocation
    }
}
```

```csharp
namespace Framework.Core
{
    public interface IRouteSource<T>
    {
        IReadOnlyList<T> All(Type type);
    }
}
```

Rules for the implementation:

- The constructor is the only writer. After the constructor, no member is added or removed.
- A duplicate in `TypeRouter<T>` throws `InvalidOperationException`: `Duplicate {T} for {type}.`
- A miss in `For` throws `InvalidOperationException`: `No {T} registered for {type}.` The current messages carry the same facts.
- `All` returns the stored array. Callers iterate with `for`, not `foreach`, to keep the hot path free of enumerator allocation.
- The ordered sort runs once, in the constructor. It is stable: the tie-break is the index in `members`.

Member interfaces derive from `IRouted`. The bridge base classes implement it explicitly, so the current property names stay:

```csharp
public interface IWireEventHandler : IRouted { Type EventType { get; } void Apply(WireEvent ev); void Unapply(WireEvent ev); }

public abstract class WireEventHandler<T> : IWireEventHandler where T : WireEvent
{
    public Type EventType => typeof(T);
    Type IRouted.RouteType => typeof(T);
    // ...
}
```

`ICommandHandler`, `ICommandValidator`, `CommandHandler<T>` and `CommandValidator<T>` get the same two lines.

### 3.3 Question 2: composite keys

The router key is `Type` and nothing else. A second key part goes one of two ways:

1. **Small closed set** (for example `HookPhase`, two values). Partition, do not compose. Keep one table for each value, in an array indexed by the value. A read is then one array index and one `Type` hash. There is no tuple hash and no enum comparer (enum comparers can box under IL2CPP).
2. **Open set** (for example `entityId` in `_direct`). This is live data, not a route. It belongs in a Store or in the `HookTable` below, not in a router.

If a frozen route needs a composite key that fits neither case, write a `readonly struct` key that implements `IEquatable<TKey>`. Do not use a `ValueTuple` key. This case does not exist today, so no generic `Router<TKey, T>` ships now.

### 3.4 Question 3: live hook subscriptions

Hooks exist only at the command level (D7). They split into two sources:

1. **Game-wide hooks** are frozen DI registrations. They are a router: one `TypeMultiRouter<IHook>` for each `HookPhase` value, keyed by the command type (§ 3.3, partition by phase).
2. **Card hooks** do not belong in the router. A Router is frozen at composition, and card hooks change while a match runs. They are also not a Store: a Store is written only by apply handlers, exists on both sides and rebuilds from the wire. Card hooks hold delegates, run on the server only and do not travel on the wire.

Phase 4 adds a server-only `HookTable` for card hooks. It is a third kind of lookup: **live, service-written, server-only**. This spike proposes to add it to `docs/lookup-patterns.md` as the pattern "Subscription table". Its shape:

```csharp
public sealed class HookTable   // command-level hooks feature, server only
{
    // One copy-on-write table for each HookPhase value.
    public Subscription Subscribe(EntityId owner, IHook hook);
    public void Unsubscribe(Subscription sub);
    public void UnsubscribeAll(EntityId owner);              // the card leaves play
    public IReadOnlyList<IHook> All(Type commandType, HookPhase phase);
}
```

- **Copy-on-write, not snapshot-on-read.** `Subscribe` and `Unsubscribe` build a new array for the key. `All` returns the current array. An in-flight dispatch keeps the old array, so the snapshot semantics stay and reads do not allocate.
- **Shared rules.** `IHook : IRouted, IOrdered`. The same stable order as `TypeMultiRouter<T>`. The same `All` read shape (`IRouteSource<T>` for each phase).
- **Merge at dispatch.** The command pipeline reads both sources for `(commandType, phase)`. Both arrays are already sorted, so it merges them by `Order` with no sort and no allocation. On equal `Order`, game-wide hooks run first. This is the one tie rule for the two sources.
- `_direct` (per-entity trigger factories) is out of scope here. Phase 4 decides if it becomes definition data (card definition to trigger effect, read from `Catalog<TDef>`) or a `HookTable` entry keyed by `EntityId`.

### 3.5 Question 4: DI registration

One style for every level:

1. A member registers as `Register<T>(Lifetime.Singleton).AsImplementedInterfaces()`.
2. The router registers as a closed generic singleton. VContainer injects `IReadOnlyList<TMember>` into its constructor. This is the current mechanism (see `command-service.md` § Phase 0 build notes: an empty list resolves cleanly).

```csharp
public static class HandlerRegistrationExtensions
{
    public static RegistrationBuilder RegisterHandler<T>(this IContainerBuilder b)          where T : IWireEventHandler => b.RegisterRouted<T>();
    public static RegistrationBuilder RegisterCommandHandler<T>(this IContainerBuilder b)   where T : ICommandHandler   => b.RegisterRouted<T>();
    public static RegistrationBuilder RegisterCommandValidator<T>(this IContainerBuilder b) where T : ICommandValidator => b.RegisterRouted<T>();

    public static RegistrationBuilder RegisterTypeRouter<T>(this IContainerBuilder b)      where T : class, IRouted
        => b.Register<TypeRouter<T>>(Lifetime.Singleton);
    public static RegistrationBuilder RegisterTypeMultiRouter<T>(this IContainerBuilder b) where T : class, IRouted
        => b.Register<TypeMultiRouter<T>>(Lifetime.Singleton);

    static RegistrationBuilder RegisterRouted<T>(this IContainerBuilder b) where T : IRouted
        => b.Register<T>(Lifetime.Singleton).AsImplementedInterfaces();
}
```

The named verbs stay. They tell game authors what level they register into. New levels add one line each (`RegisterEffectHandler<T>`, `RegisterHook<T>`). Side effect: `AsImplementedInterfaces` also registers each member as `IRouted` and `IOrdered`. Nothing resolves those lists, so this is harmless.

### 3.6 Question 6: exact type only

The router dispatches by exact runtime type (`msg.GetType()`). It does not walk base types or interfaces.

- All current copies use the exact type. The wire is deterministic only if both sides pick the same handlers. A base-type walk adds an order question (base first or derived first) with no current user.
- `command-service.md` defers base-type matching for validators "until a second validator on one command actually earns" it.
- Cross-cutting needs have other homes. "Every event" uses `EventStream.OnAppended`. "Every command" (the system gate) goes in the engine loop (roadmap phase 1).
- Closed generics are exact types. `TagSetEvent<int>` and `TagSetEvent<bool>` route apart, as they do today.

Risk: a concrete message type with a subclass. A handler for the parent then misses the child with no error. Rule ROUTE-03 removes this risk: every concrete leaf type is `sealed`.

## 4. Call-site migration

| Current type | New type | Change needed |
|---|---|---|
| `HandlerRegistry` (internals) | `TypeMultiRouter<IWireEventHandler>` inside `HandlerRegistry` | Keep the class, its constructor and the `OnAppended` link. Replace the dictionary. No test call site changes. |
| `IWireEventHandler`, `WireEventHandler<T>` | `: IRouted` | Add the interface and one explicit property. |
| `CommandHandlerRegistry` | `TypeRouter<ICommandHandler>` | Delete the class. `CommandService` calls `_handlers.For(cmd.GetType())`. |
| `CommandValidatorRegistry` | `TypeRouter<ICommandValidator>` | Delete the class. `CommandService` calls `_validators.TryFor(...)`. |
| `ICommandHandler`, `ICommandValidator` and their bases | `: IRouted` | Add the interface and one explicit property. |
| `CommandInstaller` | `RegisterTypeRouter<ICommandHandler>()`, `RegisterTypeRouter<ICommandValidator>()` | Two lines change. |
| `EngineStack`, `CommandServiceTests` | `new TypeRouter<...>(list)` | Change the constructor calls. |
| `BootstrapWiringIntegrationTests` | none | Update two comments that name the old registries. |
| `HandlerRegistrationExtensions` | one private `RegisterRouted<T>` | Same public verbs. Add the two router verbs. |
| `Network._byType` | unchanged | Replace the bare indexer with a miss that throws a named message. |
| `Dispatcher._channels` | unchanged | None. |
| `EffectStore` (spike) | `HookTable` (phase 4) | None now. The spike stays until phase 8. |

## 5. What stays specialized, and why

- **`Dispatcher._channels` (`byte` to channel, array).** It is on the hot receive path. The id is already a dense `byte`, so an array index beats any hash. The lookup-patterns doc calls this deliberate. It conforms to the contract: frozen after the build callback, throws on a miss.
- **`Network._byType` (`Type` to channel).** It is also the **id allocator**: `Register<T, TPacked>` assigns the next `byte` and fills the `Dispatcher`. Channels come from a build callback (`WireRegistrar`), not from a DI list. Its generic constraints (`IPackable<TPacked>`) do not fit `IRouted`. The record-ids piece may replace the allocator. The router should not absorb it.
- **`HookTable`.** It holds card hooks, which are live. See § 3.4. Game-wide hooks are not specialized: they use `TypeMultiRouter<IHook>`.
- **`SlotRegistry<TKey>._byType`.** This is a Registry (kind to identity), not a Router (message to handler). It stays a Registry.
- **Stage-specific glue.** The router only finds members. It does not invoke them. `HandlerRegistry.Dispatch` loops and calls `Apply`. `CommandService` opens the block and calls `Handle`. Invoke logic stays with each stage, because each stage passes different context.

## 6. Rulebook rules to propose

Proposals only. `rulebook.json` does not change in this spike.

| Id | Title | Rule | Check kind | Status at start |
|---|---|---|---|---|
| ARCH-02 (edit) | Layer references | Change the `allowed` table as shown below (D2, D3). | `asmdef.allowedReferences` (exists) | enforced |
| ROUTE-01 | No hand-written type routers | A Framework runtime type does not declare a field of type `Dictionary<System.Type, *>`. Allowed: `Framework.Core.TypeRouter*`, `Framework.Core.SlotRegistry*`, `Framework.Wire.Network`, the server-only `HookTable`. | new: `type.forbiddenFieldTypes` | advisory, then blocking after step 3 |
| ROUTE-02 | Routers are not static | A Framework runtime type does not declare a `static` field of type `TypeRouter<>`, `TypeMultiRouter<>` or `Dictionary<System.Type, *>`. | new: `type.noStaticFieldsOfType` | blocking |
| ROUTE-03 | Routed messages are sealed or abstract | Each subtype of `Framework.Wire.WireEvent` and `Framework.Commands.Command` in Framework runtime assemblies is `sealed` or `abstract`. | new: `type.sealedOrAbstract` | advisory, then blocking after step 5 |
| ROUTE-04 | Router members derive from IRouted | Each type that a `TypeRouter<>` or `TypeMultiRouter<>` collects comes through `IRouted`. The generic constraint enforces this, so this rule is a test, not a check. | `attached` (unit test in `Framework.Core.Tests`) | enforced |

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

> **Shared primitives** → `Assets/Framework/Core/`. Typed ids (`EntityId`, …), id allocators (`IdAllocator<TId>`), frozen lookup primitives (`SlotRegistry<TKey>`, `TypeRouter<T>`, `TypeMultiRouter<T>`) and attribute markers. Core holds no domain state, no wire events, no Unity engine references and no references to other Framework assemblies. If a type has domain behavior, it belongs in a feature.

Text for the `why` fields:

- ROUTE-01: "One router class for each arity. A copy drifts in its miss and duplicate behavior."
- ROUTE-02: "A static router leaks between the server and client scopes and between tests."
- ROUTE-03: "Dispatch is by exact type. A handler for a non-sealed concrete type misses its subclasses with no error."

## 7. Migration steps

Each step is one PR. Run `powershell -NoProfile -File Tools/Rulebook/gate.ps1` after each step. Add `-With playmode` for steps that touch Wire.

1. **Add the contract.** Add `IRouted`, `IOrdered`, `IRouteSource<T>`, `TypeRouter<T>`, `TypeMultiRouter<T>` to `Framework.Core`. Add unit tests in `Framework.Core.Tests`: duplicate throws, miss throws, `TryFor` miss, empty `All`, stable order, no allocation on `All`. No caller changes.
2. **Wire.** Apply the `Framework.Wire` row of the ARCH-02 edit (D2) and the new Core text. Add `Framework.Core` to `Framework.Wire.asmdef`. Make `IWireEventHandler : IRouted`. Change `HandlerRegistry` internals to `TypeMultiRouter<IWireEventHandler>`. Gate with `-With playmode`.
3. **Commands.** Make `ICommandHandler` and `ICommandValidator` derive from `IRouted`. Replace both registries with `TypeRouter<T>`. Update `CommandService`, `CommandInstaller`, `EngineStack`, `CommandServiceTests`. Delete the two registry files and their `.meta` files.
4. **Registration.** Collapse `HandlerRegistrationExtensions` onto `RegisterRouted<T>`. Add `RegisterTypeRouter<T>` and `RegisterTypeMultiRouter<T>`. Use them in `CommandInstaller` and `EngineInstaller`.
5. **Seal leaf messages.** Add `sealed` to the concrete `WireEvent` subtypes (`EntityCreatedEvent`, `StatePushEvent`, `TagSetEvent<T>`, `TagChangeEvent<T>`, `TagRemovedEvent`, `BlockStartEvent` and `BlockEndEvent` in their new `Framework.Blocks` home, the Multiplayer events). AutoPacker and `WireRegistrar` accept `sealed partial` (§ 9, answered). Gate with `-With playmode`.
6. **Rules.** Ask for approval, then add ROUTE-01 to ROUTE-04 to `rulebook.json`. Add the new check kinds in `Assets/Tools/Rulebook/Editor/Checks/` with fixture tests.
7. **Docs.** Update `docs/lookup-patterns.md` § Router (name the shared classes) and add "Subscription table". Update the `docs/architecture.md` codemap.
8. **Phase 4.** Register game-wide hooks as `TypeMultiRouter<IHook>` for each phase. Build `HookTable` for card hooks on `IRouted`, `IOrdered` and the copy-on-write rule. Effect handlers use `TypeRouter<IEffectHandler>` or `TypeMultiRouter<IEffectHandler>`, as the reactions spike decides.

## 8. Interfaces the other three pieces need from this piece

**Record ids and allocation.**

- The router keys on `Type`, never on an allocated id. Id allocation and routing stay separate.
- If a record kind travels by `int` slot, the receiver rehydrates the slot to a `Type` (through the registry) and then routes by `Type`. Or it uses a slot-indexed array, as `Dispatcher` does. This piece does not provide a slot router.
- Composition order: ids are allocated before any router is built. The router constructor reads no ids.

**Block and wire system.**

- `HandlerRegistry` keeps its public constructor `(EventStream, IReadOnlyList<IWireEventHandler>)`. Internally it holds a `TypeMultiRouter<IWireEventHandler>`.
- `IWireEventHandler : IRouted`. A new apply-handler base must implement `IRouted.RouteType`.
- `Framework.Wire` references `Framework.Core` (D2).
- `Framework.Blocks` owns `BlockStartEvent`, `BlockEndEvent`, `BlockScope` and `BlockId` (D3). Block events have no special route. Their apply handlers register with `RegisterHandler<T>()` and route by exact type, as every other event does. The leaf Block events are `sealed` (ROUTE-03).
- The router does not carry, store or compute a block parent. The wire carries `CauseId` only, and the receiver gets the parent from bracket order (D5). No router API takes a parent or a block id.
- `Dispatcher` and `Network` stay as they are. The wire piece owns them.

**Command pipeline.**

- `TypeRouter<T>.For(Type)` for exactly one handler. `TypeRouter<T>.TryFor(Type, out T)` for an optional member. `TypeMultiRouter<T>.All(Type)` and `IRouteSource<T>.All(Type)` for fan-out.
- `TypeRouter<T>.Types` for a composition check, for example "each command type in the catalog has a handler".
- `IOrdered` for any stage that needs a stable member order.
- Each level (intent, effect, command, event) owns its own router instance. One `Enqueue` pipeline selects the stage by message kind. It then asks that stage's router. The router does not know about stages, blocks or `Task`. An intent route opens no block (D6); the block decision belongs to the stage, not the router.
- Hooks exist at the command level only (D7). The command stage reads two sources for `(commandType, phase)`: the frozen `TypeMultiRouter<IHook>` for that phase (game-wide, DI) and `HookTable.All(commandType, phase)` (card hooks, server only). It merges them by `Order`. On a tie, game-wide hooks run first. No other level has hooks.
- `HookTable` is copy-on-write, so `Subscribe` and `Unsubscribe` are safe during a dispatch. `UnsubscribeAll(EntityId)` removes the hooks of a card when it leaves play.

## 9. Open questions

Closed:

- ~~**Home assembly.**~~ Closed by D2: `Framework.Core`. § 6 gives the ARCH-02 rows and the new Core text.
- ~~**Sealing and AutoPacker.**~~ Answered: AutoPacker accepts `sealed partial`. No fallback is necessary. Evidence:
  - The package ships only a compiled generator, `Library/PackageCache/com.scaffold.autopacker@fd08e7dbfb00/Runtime/AutoPackerGenerator.dll`. Its README says the generator source lives in `Generators/AutoPacker/` of the Scaffold repository, not in the package. There is no `.cs` source to cite by line.
  - The generator writes the type header from three parts: accessibility, `" partial "` and the type keyword (`GetAccessibilityKeyword`, `GetTypeKeyword`, `EmitPartial` in the DLL strings). It never writes `sealed`. In C#, `sealed` on one partial part seals the whole type, so the user part can carry it alone.
  - Test build (scratchpad, .NET 10 SDK, the same DLL as analyzer): `[AutoPack] abstract partial class WireEvent`, `sealed partial class LeafEvent : WireEvent`, and `sealed partial class LeafGen<T> : WireEvent where T : unmanaged`. It compiled with no diagnostics. A `Pack` and `new LeafEvent(packed)` round trip printed `3 sealed=True`.
  - The generated `LeafEvent.Packed.g.cs:4` is `public partial class LeafEvent : IPackable, …`. Lines `:11` and `:25` are `public override` (`Unpack`, `Pack`). `override` is legal in a sealed class. `PackTyped` and `UnpackTyped` (`:20`, `:14`) are not virtual.
  - `WireRegistrar` filters only on `IsAbstract` and `IsGenericTypeDefinition` (`Assets/Framework/Wire/Runtime/Network/WireRegistrar.cs:71`, `:77`). It has no `IsSealed` check.
  - Residual risk: the generator has a `virtual` emit path (`HasUserDeclaredVirtualOrAbstractPackOrUnpack`). A new `virtual` member in a sealed class is error CS0549. The test did not reach that path for a leaf. If a future leaf declares its own virtual `Pack`, the build fails at once, so the risk is visible, not silent.

Still open:

1. **Subscription table as a pattern.** Is "live, service-written, server-only" a new named pattern in `docs/lookup-patterns.md`? Or is `HookTable` an internal detail of the hooks feature with a lookup facet only (as `Session` is)?
2. **Composition-time completeness.** Should the engine check at build time that each known command type has a handler, through `TypeRouter<T>.Types`? This needs a list of known command types, which does not exist yet.
3. **Validator fan-out.** If the engine loop adds more than one validator for each command, validators move from `TypeRouter<T>` to `TypeMultiRouter<T>` with `IOrdered`. Confirm that this stays deferred.
4. **`_direct` per-entity triggers.** Definition data or `HookTable` entries keyed by `EntityId`? Phase 4 decides, with `oq-trigger-subscription-entry`.
5. **Names.** Rename `HandlerRegistry` to `ApplyDispatcher` later? It dispatches; it does not register kinds.
6. **Hook tie rule.** On equal `Order`, game-wide hooks run before card hooks (§ 3.4). Confirm, or make the tie an error at subscribe time.
