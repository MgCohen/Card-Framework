---
type: spike
status: in-design
created: 2026-05-20
references:
  - Product/spikes/wire-refoundation-roadmap.md
  - Product/spikes/definition-entity-setup.md
  - Product/spikes/step3-entity-phases.md
sandbox: Assets/Framework/Engine/, Assets/Framework/Bootstrap/
tags: [#spike, #services, #pattern, #architecture, #bootstrap, #handlers]
---

# Service pattern (step 4)

> **Status: in-design.** Full design (L1–L11) locked through the
> 2026-05-20 walk; all open Qs resolved (Q1–Q7). Repository-projection
> split (B1c) confirmed: `EntityStore` is the repository, `EntityService`
> is the command/emit wrapper, apply handlers depend on the store.
> `EventStream` exposes two scoped interfaces — `IEventEmitter` (emit
> only) and `IHandlerRegistry` (register only) — for Interface
> Segregation. **NB:** `IEventEmitter` is a surface narrowing with a
> single impl (`EventStream`), *not* the per-side toggle from an earlier
> draft; there is no `NoOp` variant. Flip to `status: active` once
> 4a + 4b have shipped and the convention stabilizes against actual
> code.

## What step 4 is

Step 4 in [`wire-refoundation-roadmap.md`](wire-refoundation-roadmap.md): *"Service
pattern. Generalize the per-role service shape (command / effect / request /
state). Decide what's load-bearing and what's incidental."*

The deliverable is:

1. **Scaffold** — `GameEngine` stub + DI substrate (VContainer + Scaffold.Appflow)
   + `Framework.Engine` asmdef (pure C#) + `Framework.Bootstrap` asmdef
   (Unity-coupled) + a sandbox scene proving the bones work.
2. **Worked example** — `EntityStore` (repository) + `EntityService`
   (command/emit wrapper) + per-type Apply handlers — the
   **event-sourced + in-memory-repository projection** shape that
   server-side mutations round-trip to a client-side mirror through
   `Framework.Wire` with same classes both sides and no `IsServer` /
   `IsClient` runtime branches.

The scaffold + worked example together **define** the service pattern by
example. Slice's specific services (Command / Effect / Trigger / Request)
move to later steps where they get the established pattern applied.

**End-state demo (4d):** press Play on the sandbox scene → two
`AppFlowRoot`s boot (server + client) → IMGUI on each shows "Engine
bootstrapped" (4a milestone) → expands to "1 entity created on server,
mirror in sync on client" (4b milestone).

## Locked decisions

### L1 — Convention only, no `IService` interface or base class

A "service" is a plain class that owns one concern, holds private state,
and is registered into a VContainer scope by a layer's `Install`. There
is no `IService` marker, no `ServiceBase`, no shared virtual lifecycle
hooks. Each service has the surface it needs and no ceremony.

### L2 — Services are side-agnostic; asymmetry lives in which classes are registered

Same service class is registered in any scope. No `IsServer` /
`IsClient` runtime check inside service code. Per-side behavior comes
from **which other classes are registered alongside** the service —
specifically, which bridge (`WireForwarder` on server, `NetworkInboundBridge`
on client) is installed. Apply handlers and the storage layer (`EntityStore`)
register on both sides; bouncing is structurally impossible because
handlers depend on the store, not the service.

Everything else flows from layer composition.

### L3 — DI substrate: VContainer + Scaffold.Appflow

Manifest gains both:

```json
"com.scaffold.appflow": "https://github.com/MgCohen/Scaffold.git?path=/Assets/Packages/com.scaffold.appflow#main",
"jp.hadashikick.vcontainer": "https://github.com/hadashiA/VContainer.git?path=VContainer/Assets/VContainer#1.17.0"
```

Appflow provides the layer pattern (`AppFlowRoot` / `IScopeLayer` /
`IAsyncInitializable`) on top of VContainer. Services + handlers register
through `IScopeLayer.Install(IContainerBuilder)`; lifecycle hooks come
from `IAsyncInitializable` (post-construct async init) and
`IAsyncDisposable` (teardown).

Service-to-service dependencies = VContainer constructor injection.

### L4 — Scaffold home: split asmdefs

- `Framework.Engine` — pure C#. Holds `GameEngine` (composition-shaped
  per L11). References `Framework.Entities`, `Framework.Tags`,
  `Framework.Wire` to type its service properties — all pure C#, so no
  Unity coupling violation. No `GameEngineBuilder` (Q4).
- `Framework.Bootstrap` — Unity-coupled. Holds the per-side
  `AppFlowRoot` subclasses (`ServerAppFlowRoot`, `ClientAppFlowRoot`),
  layer installers (`EngineLayer`, `EntityLayer`, `ServerBridgeLayer`,
  `ClientBridgeLayer`), sandbox scene + IMGUI status UI. Layers stack
  additively — both roots install `EngineLayer` + `EntityLayer`; each
  adds its own tiny bridge layer.

### L5 — Step 4 ship scope

Phases 4a–4d (see [`step4-service-phases.md`](step4-service-phases.md)):

- **4a — Scaffold.** Manifest, asmdefs, two `AppFlowRoot` subclasses,
  sandbox scene, "Engine bootstrapped" IMGUI. `EventStream` gains its
  typed-bucket dispatch surface (`Register<T>` + typed `Emit<T>` + a
  non-generic `Emit(WireEvent)` overload for the inbound bridge) +
  `IWireEventHandler<T>`; `Network` surface is reviewed against L10.
  `GameEngine` exposes `Stream` + `Network` properties (4b adds
  `Entities`).
- **4b — `EntityStore` + `EntityService` + per-type Apply handlers.**
  Worked example carrying the full pattern (L8–L10): `EntityStore`
  holds storage (no emit), `EntityService` wraps the store and emits
  on `EventStream`, per-type Apply handlers depend on `EntityStore`
  (cannot emit by construction), `WireForwarder` (server) +
  `NetworkInboundBridge` (client) cross the network boundary.
- **4c — Pattern doc validation.** Read this doc against shipped code.
- **4d — Step 4 closure.** Status flip, handoff for step 5+.

### L6 — `EntityStore` + `EntityService` split; both sides install both

Storage and command/emit are different concerns. They live in different
classes:

- **`EntityStore`** — the in-memory repository (`Dictionary<int, Entity>`
  + `_nextId`). Pure mutation API: `Insert`, `SetTag`, `RemoveTag`, `Get`.
  Never emits.
- **`EntityService`** — the command surface. Holds an `EntityStore` +
  `EventStream`. Public `Create` / `Set` / `Remove` delegate to the
  store and emit on the stream.

Both classes register on **both** sides. The per-side asymmetry is one
bridge each:

- Server scope installs `WireForwarder` (subscribes to `EventStream`,
  pushes to `Network`).
- Client scope installs `NetworkInboundBridge` (subscribes to `Network`,
  emits into `EventStream`).

Apply handlers register on both sides — and depend on `EntityStore`, not
`EntityService`. They physically cannot emit because they don't hold an
emit-capable reference. Type-system-enforced no-bounce.

On the server, apply handlers still fire (every emission fans out to all
registered handlers, including local ones) but they re-apply state that's
already there. `EntityStore.Insert` is idempotent for explicit-id inserts;
`SetTag` / `RemoveTag` are idempotent by nature. Wasted work, structurally
safe. (If hot-path cost shows up later, gate handler registration per
side. Not in step 4.)

The asymmetric `EntityService` + separate `EntityMirror` shape was
considered and rejected (Q1 Option A) on these grounds:

- Two owners for the entity dictionary
- Awkward ownership for future preview/rollback work
- Type-count saving was overstated

The `IEventEmitter` + `NoOpEventEmitter` *per-side toggle* was
considered and rejected: it was a way to silence echo, but the
store/service split eliminates echo structurally, so the toggle adds
no value. (The `IEventEmitter` interface that ships in L8 is a
different concept — single impl, ISP narrowing of `EventStream`, no
NoOp variant.)

### L7 — `World` does not exist as a separate substrate

The slice's `World` (event log + block-id allocator + open-block stack)
splits across services that need its pieces. Step 4 ships no `World`
type. `EntityService` needs no piece of it.

### L8 — `EventStream` is the dispatch surface; scoped interfaces, typed buckets

`EventStream` (in `Framework.Wire`) owns the per-type dispatch table.
No separate `WireEventDispatcher`. The table is typed via a bucket
abstraction — buckets carry their own `T`, so the stream stores
`Dictionary<Type, IHandlerBucket>` rather than `Dictionary<Type, object>`.

Per caller, the stream is surfaced through two **scoped interfaces**
(Interface Segregation):

- `IEventEmitter` — services and the inbound bridge see this. Two
  `Emit` overloads: typed `Emit<T>(T)` (services + forwarder) and
  non-generic `Emit(WireEvent)` (inbound bridge).
- `IHandlerRegistry` — apply handlers and the outbound forwarder see
  this. One `Register<T>(IWireEventHandler<T>)`.

There is **one impl** — `EventStream` itself implements both. No NoOp,
no per-side variation. The split is purely for narrowing each caller's
view of the stream; a service holding `IEventEmitter` cannot call
`Register`, a handler holding `IHandlerRegistry` cannot call `Emit`.

```csharp
// Framework.Wire/Runtime/Events/IWireEventHandler.cs
public interface IWireEventHandler<TEvent> where TEvent : WireEvent
{
    void Apply(TEvent ev);
    void Unapply(TEvent ev);   // rollback seam; throws NotImplementedException in step 4
}

// Framework.Wire/Runtime/Events/IEventEmitter.cs
public interface IEventEmitter
{
    void Emit<TEvent>(TEvent ev) where TEvent : WireEvent;   // typed fast path
    void Emit(WireEvent ev);                                  // type-routing
}

// Framework.Wire/Runtime/Events/IHandlerRegistry.cs
public interface IHandlerRegistry
{
    void Register<TEvent>(IWireEventHandler<TEvent> handler) where TEvent : WireEvent;
}

// Framework.Wire/Runtime/Events/HandlerBucket.cs
internal interface IHandlerBucket
{
    void DispatchUntyped(WireEvent ev);
}

internal sealed class HandlerBucket<T> : IHandlerBucket where T : WireEvent
{
    readonly List<IWireEventHandler<T>> _handlers = new();
    public void Add(IWireEventHandler<T> h) => _handlers.Add(h);
    public void Dispatch(T ev)
    {
        for (var i = 0; i < _handlers.Count; i++) _handlers[i].Apply(ev);
    }
    public void DispatchUntyped(WireEvent ev) => Dispatch((T)ev);
}

// Framework.Wire/Runtime/Events/EventStream.cs
public sealed class EventStream : IEventEmitter, IHandlerRegistry
{
    readonly Dictionary<Type, IHandlerBucket> _buckets = new();
    readonly List<WireEvent> _history = new();

    public IReadOnlyList<WireEvent> History => _history;

    public void Register<TEvent>(IWireEventHandler<TEvent> handler) where TEvent : WireEvent
    {
        if (!_buckets.TryGetValue(typeof(TEvent), out var bucket))
            _buckets[typeof(TEvent)] = bucket = new HandlerBucket<TEvent>();
        ((HandlerBucket<TEvent>)bucket).Add(handler);
    }

    public void Emit<TEvent>(TEvent ev) where TEvent : WireEvent
    {
        _history.Add(ev);
        if (_buckets.TryGetValue(typeof(TEvent), out var bucket))
            ((HandlerBucket<TEvent>)bucket).Dispatch(ev);
    }

    public void Emit(WireEvent ev)
    {
        _history.Add(ev);
        if (_buckets.TryGetValue(ev.GetType(), out var bucket))
            bucket.DispatchUntyped(ev);
    }
}
```

DI binds both `IEventEmitter` and `IHandlerRegistry` to the same
`EventStream` singleton. End-user code that wants the full surface
resolves `EventStream` directly (or reaches it via `GameEngine.Stream`).

`Emit` appends to history, dispatches to handlers registered for the
event's runtime type. No distinction between "author emits" and
"received apply" at the EventStream level — both call `Emit`. The
asymmetry between sides is in *which bridges are installed*, not in two
API verbs.

Apply handlers are per-type classes implementing
`IWireEventHandler<T>`. They live in the same asmdef as the storage they
mutate (e.g. `Framework.Entities/Runtime/Wire/EntityCreatedHandler.cs`).
Each ctor-injects the **store** it mutates + `IHandlerRegistry` (to
register on). The handler is registered via an `IAsyncInitializable`
init step called by VContainer at scope build (per Q7).

Step 4 ships `Apply` only; `Unapply` throws `NotImplementedException`
until the rollback feature is designed (Deferred items).

### L9 — Storage / command split — event-sourced with repository projection

The pattern is **event sourcing with an in-memory repository as the
projection**:

| Role                 | DDD/ES term         | Class                  |
|----------------------|---------------------|------------------------|
| Aggregate storage    | Repository          | `EntityStore`          |
| Command + emit       | Application service | `EntityService`        |
| Event → state apply  | Projection handler  | `*Handler : IWireEventHandler<T>` |
| Event log / bus      | Event stream        | `EventStream`          |
| Outbound replication | Outbox / transport  | `WireForwarder` + `Network` |

**`EntityStore`** is a plain repository over the entity dictionary.
Reads + raw writes; no events.

```csharp
public sealed class EntityStore
{
    readonly Dictionary<int, Entity> _byId = new();
    int _nextId = 1;

    public Entity Get(int id) => _byId.GetValueOrDefault(id);
    public IReadOnlyCollection<Entity> All => _byId.Values;

    public Entity Insert(EntityType type, int defId)
    {
        var entity = new Entity(_nextId++, type, defId);
        _byId[entity.Id] = entity;
        return entity;
    }

    // Explicit-id insert is idempotent — apply handlers can re-run safely.
    public Entity Insert(int id, EntityType type, int defId)
    {
        if (_byId.TryGetValue(id, out var existing)) return existing;
        var entity = new Entity(id, type, defId);
        _byId[id] = entity;
        if (id >= _nextId) _nextId = id + 1;
        return entity;
    }

    public void SetTag<T>(int id, int slot, T value) where T : unmanaged
        => _byId[id].SetSlotRaw(slot, value);

    public void RemoveTag(int id, int slot)
        => _byId[id].RemoveSlotRaw(slot);
}
```

**`EntityService`** is the command surface. Wraps the store; emits via
`IEventEmitter` (scoped narrowing of `EventStream`). Public `Create` /
`Set` / `Remove` are the only mutation methods external code can see.

```csharp
public sealed class EntityService
{
    readonly EntityStore _store;
    readonly IEventEmitter _events;   // scoped — emit only, no Register

    public EntityService(EntityStore store, IEventEmitter events)
    { _store = store; _events = events; }

    public Entity Get(int id) => _store.Get(id);

    public Entity Create(EntityType type, int defId = 0)
    {
        var entity = _store.Insert(type, defId);
        _events.Emit(new EntityCreatedEvent {
            EntityId = entity.Id, TypeSlot = type.Slot, DefinitionId = defId
        });
        return entity;
    }

    public void Set<T>(int entityId, TagKey<T> key, T value) where T : unmanaged
    {
        var entity = _store.Get(entityId);
        if (entity.TryGetSlotRaw<T>(key.Slot, out var prev))
        {
            if (EqualityComparer<T>.Default.Equals(prev, value)) return;
            _store.SetTag(entityId, key.Slot, value);
            _events.Emit(new TagChangeEvent<T> {
                EntityId = entityId, Slot = key.Slot, Prev = prev, Next = value
            });
        }
        else
        {
            _store.SetTag(entityId, key.Slot, value);
            _events.Emit(new TagSetEvent<T> {
                EntityId = entityId, Slot = key.Slot, Value = value
            });
        }
    }
}
```

**`Entity`** is data + bag. No service ref, no emit ref. Public readers,
internal raw setters that only `EntityStore` calls.

```csharp
public sealed class Entity
{
    readonly Dictionary<int, object> _slots = new();

    public int Id { get; }
    public EntityType Type { get; }
    public int DefinitionId { get; }

    internal Entity(int id, EntityType type, int defId)
    { Id = id; Type = type; DefinitionId = defId; }

    public T? Get<T>(TagKey<T> key) where T : unmanaged
        => _slots.TryGetValue(key.Slot, out var raw) ? (T)raw : default;

    internal bool TryGetSlotRaw<T>(int slot, out T value) where T : unmanaged
    {
        if (_slots.TryGetValue(slot, out var raw)) { value = (T)raw; return true; }
        value = default; return false;
    }

    internal void SetSlotRaw<T>(int slot, T value) where T : unmanaged
        => _slots[slot] = value;

    internal void RemoveSlotRaw(int slot)
        => _slots.Remove(slot);
}
```

**End-user authoring** goes through `EntityService`:

```csharp
var card = engine.Entities.Create(EntityTypes.Card, defId: 101);
engine.Entities.Set(card.Id, SampleTags.Health, 30);
```

The fluent `entity.Set(...)` chain from step 3 is gone — its
ergonomics required Entity to hold a back-ref. The trade is: cleaner
separation now, no Pattern-B fluency on Entity. If the call-site verbosity
bites, we can re-add a typed handle (`EntityHandle` struct wrapping
`(id, service)`) without touching `Entity` itself.

**Apply handlers** depend on `EntityStore`, not `EntityService`. They
physically cannot emit — the type system enforces no-bounce. See L10
for the handler shape.

### L10 — `Network` and `EventStream` don't reference each other; two bridge classes cross

`Network` (in `Framework.Wire`) is a dumb pipe: serialization +
transport. Its surface:

```csharp
Network.Register<TEvent>();         // declare type for serializer
Network.Send<TEvent>(TEvent ev);    // outbound
Network.OnReceived: Action<WireEvent>; // inbound — already typed-decoded
```

Channels are a serialization detail — not exposed structurally.

Two thin bridge classes cross the `Network` ↔ `EventStream` boundary,
both registered by boot layers:

- **`WireForwarder`** (server only) — a single class that implements
  `IWireEventHandler<T>` for every replicated event type. Ctor-injects
  `IHandlerRegistry` (scoped — register only) + `Network`. Registers
  itself for each `T` during `InitializeAsync`. Each `Apply` is a
  one-liner that calls `Network.Send(ev)`. Shape is symmetric with apply
  handlers — same registration API, same `Apply` contract.

```csharp
internal sealed class WireForwarder :
    IWireEventHandler<EntityCreatedEvent>,
    IWireEventHandler<TagSetEvent<int>>,
    IWireEventHandler<TagSetEvent<bool>>,
    IWireEventHandler<TagChangeEvent<int>>,
    IWireEventHandler<TagChangeEvent<bool>>,
    IWireEventHandler<TagRemovedEvent>,
    IAsyncInitializable
{
    readonly IHandlerRegistry _handlers;
    readonly Network _network;

    public WireForwarder(IHandlerRegistry handlers, Network network)
    { _handlers = handlers; _network = network; }

    public UniTask InitializeAsync(CancellationToken ct)
    {
        _handlers.Register<EntityCreatedEvent>(this);
        _handlers.Register<TagSetEvent<int>>(this);
        _handlers.Register<TagSetEvent<bool>>(this);
        _handlers.Register<TagChangeEvent<int>>(this);
        _handlers.Register<TagChangeEvent<bool>>(this);
        _handlers.Register<TagRemovedEvent>(this);
        return UniTask.CompletedTask;
    }

    public void Apply(EntityCreatedEvent ev)   => _network.Send(ev);
    public void Apply(TagSetEvent<int> ev)     => _network.Send(ev);
    public void Apply(TagSetEvent<bool> ev)    => _network.Send(ev);
    public void Apply(TagChangeEvent<int> ev)  => _network.Send(ev);
    public void Apply(TagChangeEvent<bool> ev) => _network.Send(ev);
    public void Apply(TagRemovedEvent ev)      => _network.Send(ev);

    public void Unapply<T>(T ev) => throw new NotImplementedException();
}
```

- **`NetworkInboundBridge`** (client only) — ctor-injects `IEventEmitter`
  (scoped — emit only) + `Network`. Subscribes to `Network.OnReceived`
  and forwards the decoded event into `IEventEmitter.Emit(WireEvent)`
  (non-generic overload). One line, no per-`T` switch.

```csharp
internal sealed class NetworkInboundBridge : IAsyncInitializable
{
    readonly IEventEmitter _events;
    readonly Network _network;

    public NetworkInboundBridge(IEventEmitter events, Network network)
    { _events = events; _network = network; }

    public UniTask InitializeAsync(CancellationToken ct)
    {
        _network.OnReceived += _events.Emit;
        return UniTask.CompletedTask;
    }
}
```

Neither `Network` nor `EventStream` names the other. Boot wires them at
scope init. The same two bridge classes serve every feature; per-event
ceremony lives in the per-type Apply handlers, not in the bridges. The
typed-bucket dispatch (L8) means the inbound bridge needs no switch
statement — the bucket carries the type information.

### L11 — `GameEngine` is a composition-shaped surface

`GameEngine` in `Framework.Engine` is a typed handle to the framework's
services. Each framework-feature service is exposed as a concrete
property:

```csharp
public sealed class GameEngine
{
    public EventStream Stream { get; }
    public Network Network { get; }
    public EntityService Entities { get; }
    // step 5+ adds: Requests, Commands, Effects, Triggers, MatchState, ...

    public GameEngine(EventStream stream, Network network, EntityService entities)
    {
        Stream = stream; Network = network; Entities = entities;
    }
}
```

- **End-user / call-site code** prefers `engine.Entities.Create(...)`,
  `engine.Stream.Emit(...)`. Discoverable, typed surface.
- **Internal plumbing** (handlers, bridges, framework-internal services)
  ctor-injects each service directly. `GameEngine` is for ergonomics,
  not for plumbing.

The type changes whenever a framework service ships — `Framework.Engine`
gains a new property + ctor param. Stability is traded for
discoverability; accepted.

## Bootstrap shape

The sandbox scene runs server and client as **two separate
`AppFlowRoot`s** (Q2). Each side has its own scope, its own
`EventStream`, its own `GameEngine`. They communicate via `Network`
instances wired to a shared in-memory transport at the scene level.

Layers stack additively. The same `EngineLayer` + `EntityLayer` run on
both sides; the only delta is which tiny bridge layer is added on top.

```csharp
// Framework.Bootstrap/Runtime/ServerAppFlowRoot.cs
[DefaultExecutionOrder(-1000)]
public sealed class ServerAppFlowRoot : AppFlowRoot
{
    protected override IEnumerable<IScopeLayer> GetInitialLayers()
    {
        yield return new EngineLayer();         // 4a — GameEngine + EventStream + Network
        yield return new EntityLayer();         // 4b — EntityStore + EntityService + all Apply handlers
        yield return new ServerBridgeLayer();   // 4b — WireForwarder only
    }
}

// Framework.Bootstrap/Runtime/ClientAppFlowRoot.cs
[DefaultExecutionOrder(-1000)]
public sealed class ClientAppFlowRoot : AppFlowRoot
{
    protected override IEnumerable<IScopeLayer> GetInitialLayers()
    {
        yield return new EngineLayer();         // 4a — GameEngine + EventStream + Network
        yield return new EntityLayer();         // 4b — EntityStore + EntityService + all Apply handlers
        yield return new ClientBridgeLayer();   // 4b — NetworkInboundBridge only
    }
}
```

Sandbox scene layout:

```
BootstrapSandbox.unity
  ├─ ServerRoot          [ServerAppFlowRoot]
  ├─ ClientRoot          [ClientAppFlowRoot]
  └─ SandboxTransport    [MonoBehaviour: creates in-memory transport,
                           injects into both Networks at scene init]
```

The two-root shape is honest about "server and client are different
runtimes that happen to share a scene for demo purposes." The
additive-layers shape means the entity layer doesn't fork by side —
storage + service + handlers are universal; only the bridge differs.

## Concrete shape sketch (subject to 4a/4b implementation)

Storage + service + entity classes live in L9. The remaining pieces:

### Per-type Apply handlers — depend on `EntityStore` + `IHandlerRegistry`

Handlers see `IHandlerRegistry`, not `IEventEmitter`. No-emit is enforced
by both their store-only data dependency and their stream-scope: nothing
in their ctor surface lets them emit.

```csharp
// Framework.Entities/Runtime/Wire/EntityCreatedHandler.cs
internal sealed class EntityCreatedHandler
    : IWireEventHandler<EntityCreatedEvent>, IAsyncInitializable
{
    readonly EntityStore _store;          // store, not service — can't mutate via emit
    readonly IHandlerRegistry _handlers;  // register only — no Emit access

    public EntityCreatedHandler(EntityStore store, IHandlerRegistry handlers)
    { _store = store; _handlers = handlers; }

    public UniTask InitializeAsync(CancellationToken ct)
    {
        _handlers.Register<EntityCreatedEvent>(this);
        return UniTask.CompletedTask;
    }

    public void Apply(EntityCreatedEvent ev)
        => _store.Insert(ev.EntityId,
                         EntityTypeRegistry.BySlot(ev.TypeSlot),
                         ev.DefinitionId);

    public void Unapply(EntityCreatedEvent ev)
        => throw new NotImplementedException("Rollback lights up later");
}

internal sealed class TagSetHandler<T>
    : IWireEventHandler<TagSetEvent<T>>, IAsyncInitializable
    where T : unmanaged
{
    readonly EntityStore _store;
    readonly IHandlerRegistry _handlers;

    public TagSetHandler(EntityStore store, IHandlerRegistry handlers)
    { _store = store; _handlers = handlers; }

    public UniTask InitializeAsync(CancellationToken ct)
    {
        _handlers.Register<TagSetEvent<T>>(this);
        return UniTask.CompletedTask;
    }

    public void Apply(TagSetEvent<T> ev)
        => _store.SetTag(ev.EntityId, ev.Slot, ev.Value);

    public void Unapply(TagSetEvent<T> ev)
        => throw new NotImplementedException("Rollback lights up later");
}

// TagChangeHandler<T>, TagRemovedHandler — analogous; all take EntityStore + IHandlerRegistry.
```

### Bridges

See L10 for `WireForwarder` (multi-interface `IWireEventHandler` on
server) and `NetworkInboundBridge` (one-liner non-generic `Emit` on
client).

### Layer registration

```csharp
// EngineLayer — both sides (Framework.Bootstrap)
internal sealed class EngineLayer : IScopeLayer
{
    public void Install(IContainerBuilder b)
    {
        // Single EventStream singleton, exposed as itself + the two scoped interfaces.
        b.Register<EventStream>(Lifetime.Singleton)
            .As<IEventEmitter>()
            .As<IHandlerRegistry>();
        b.Register<Network>(Lifetime.Singleton);
        b.Register<GameEngine>(Lifetime.Singleton);
    }
}

// EntityLayer — both sides (Framework.Bootstrap)
internal sealed class EntityLayer : IScopeLayer
{
    public void Install(IContainerBuilder b)
    {
        b.Register<EntityStore>(Lifetime.Singleton);
        b.Register<EntityService>(Lifetime.Singleton);

        // Per-type Apply handlers. Manual enumeration in step 4; source-gen later (Q6).
        b.RegisterEntryPoint<EntityCreatedHandler>(Lifetime.Singleton);
        b.RegisterEntryPoint<TagSetHandler<int>>(Lifetime.Singleton);
        b.RegisterEntryPoint<TagSetHandler<bool>>(Lifetime.Singleton);
        b.RegisterEntryPoint<TagChangeHandler<int>>(Lifetime.Singleton);
        b.RegisterEntryPoint<TagChangeHandler<bool>>(Lifetime.Singleton);
        b.RegisterEntryPoint<TagRemovedHandler>(Lifetime.Singleton);
    }
}

// ServerBridgeLayer — server only
internal sealed class ServerBridgeLayer : IScopeLayer
{
    public void Install(IContainerBuilder b)
        => b.RegisterEntryPoint<WireForwarder>(Lifetime.Singleton);
}

// ClientBridgeLayer — client only
internal sealed class ClientBridgeLayer : IScopeLayer
{
    public void Install(IContainerBuilder b)
        => b.RegisterEntryPoint<NetworkInboundBridge>(Lifetime.Singleton);
}
```

`RegisterEntryPoint` makes the handler an `IAsyncInitializable`
participant; its `InitializeAsync` is where
`_handlers.Register<TEvent>(this)` runs against the injected
`IHandlerRegistry`. Exact mechanism (entry-point vs. `IPostBuildCallback`)
settles during 4a's `EventStream` work.

## Pattern reproducibility

The same store-plus-service split applies to every future feature:

| Feature | Repository (store) | Command service (emits) | Per-type Apply handlers (depend on store) |
|---|---|---|---|
| **Entities** (step 4) | `EntityStore` | `EntityService` | `EntityCreatedHandler`, `TagSetHandler<T>`, ... |
| **Requests** (step 5+) | `RequestStore` | `RequestService` | `RequestOpenedHandler`, `RequestClosedHandler` |
| **Commands** (step 5+) | `CommandStore` (open-block tracking) | `CommandService` | `BlockStartHandler`, `BlockEndHandler` |
| **Effects** (step 5+) | shared with commands | `EffectService` | shared handlers |

Each store is pure C# (repository contract: reads + raw writes, no
events). Each service ctor-injects its store + `EventStream`. Apply
handlers ctor-inject the store. Server scope adds `WireForwarder` rows
per replicated event type; client scope's `NetworkInboundBridge` is the
same one-liner for every feature.

## Resolved questions

All step-4 design Qs locked as of 2026-05-20. Implementation phases own
the specifics; the verdicts here are binding.

### Q1 — Final direction confirmation ✅

Locked as L8–L11:

- `EventStream` is the dispatch surface (no separate dispatcher class).
  Typed `IHandlerBucket<T>` internals; typed `Emit<T>` for services +
  non-generic `Emit(WireEvent)` for the inbound bridge. Surfaced
  through two scoped interfaces — `IEventEmitter` (emit-only) and
  `IHandlerRegistry` (register-only) — ISP narrowing only, single impl.
- `IWireEventHandler<T>` carries `Apply` + `Unapply`; per-type classes
  register via `IHandlerRegistry`.
- **Storage / command split (B1c).** `EntityStore` is the in-memory
  repository (no emit); `EntityService` ctor-takes `IEventEmitter` and
  emits inline; apply handlers ctor-take `EntityStore` +
  `IHandlerRegistry` — neither dependency lets them emit. Type-system
  enforces no-bounce.
- `Network` and `EventStream` are decoupled; `WireForwarder`
  (handler-shaped, server only) + `NetworkInboundBridge` (one-liner,
  client only) cross the boundary.

### Q2 — Sandbox scene shape ✅

**Two `AppFlowRoot`s in one scene.** Each side has its own scope, its
own `EventStream`, its own `EntityStore` + `EntityService`, its own
`GameEngine`. They share a scene-level in-memory transport that wires
the two `Network` instances together. Honest about "two runtimes for
the demo," and the additive-layer shape means no per-side fighting over
registrations.

### Q3 — `GameEngine`'s surface ✅

**Composition-shaped properties.** Per L11 — `GameEngine` carries a
typed property per framework service (`Stream`, `Network`, `Entities`,
...). Discoverable end-user surface; internal plumbing still
ctor-injects services directly.

### Q4 — `GameEngineBuilder` in step 4 ✅

**No.** Tests construct `EntityService` directly with fakes; production
uses `AppFlowRoot`. Defer until a non-Unity consumer appears.

### Q5 — Entity-feature specifics ✅

- **Q5a — Naming.** `EntityStore` (repository) + `EntityService`
  (command/emit wrapper). The step-3 single-class `EntityStore` splits
  into two; the name `EntityStore` keeps the repository half, and
  `EntityService` joins as the command surface.
- **Q5b — Emit seam.** `EntityService` ctor-injects `EntityStore` +
  `IEventEmitter` (scoped interface — emit only, single impl backed by
  `EventStream`); calls `_events.Emit(...)` inline. Apply handlers
  ctor-inject `EntityStore` + `IHandlerRegistry` (scoped — register
  only); neither dependency exposes emit, so the no-bounce invariant
  is double-enforced (no service ref *and* no emit-capable interface).
- **Q5c — `TagChangeEvent<T>`.** Add to `Framework.Tags` for symmetric
  Set/Change/Removed trio.
- **Q5d — Indexed queries (`With` / `Where`).** Defer. When they ship,
  they live on `EntityStore` (read-side concern).
- **Q5e — `Destroy` + `EntityDestroyedEvent`.** Defer.
- **Q5f — Apply API surface.** Lives on `EntityStore` as the pure
  mutation methods: `Insert(int id, ...)`, `SetTag<T>`, `RemoveTag`.
  Apply handlers call those. No "ApplyCreated/ApplyTagSet" twins on
  `EntityService` — the store is the apply surface.

### Q6 — Per-T closed-type registration ergonomics ✅

**Manual enumeration in step 4; source-gen later.** ~12 lines across two
layers + the forwarder body is tolerable now. Source-gen picks up
registered tag types and emits closed registrations + the forwarder
dispatch table once friction compounds. Tracked as `oq-handler-codegen`.

### Q7 — Handler registration mechanism ✅

**`IAsyncInitializable` entry points.** Apply handlers and bridges
register themselves on `EventStream` / `Network` during their
`InitializeAsync`, invoked when the layer's entry points spin up.
Composes with Appflow's lifecycle; keeps registration colocated with
the handler.

## Deferred items

| Item | Lights up when |
|---|---|
| `CommandService` / `EffectService` / `TriggerService` ports | Step 5+ applying the pattern |
| `RequestService` shape | Step 5+ |
| `ISBA` registry + fixpoint | When a game forces it |
| `MatchState` + `Session` lifecycle | Step 5 |
| `ICommandValidator<T>` chain | Step 5+ |
| Ability abstraction (unify Subscribe/SetDirect/SBA) | When a 4th category earns it |
| `Entity.Destroy` + `EntityDestroyedEvent` | When `entity-identity-model.md` lands |
| `EntityService` indexed queries | When an author surface needs them |
| Source-gen per-T handler registration | When manual enumeration bites |
| `GameEngineBuilder` for non-Unity hosts | When a non-Unity consumer appears |
| `Unapply` real implementations (preview / rollback) | When the speculation feature is designed |
| View-model layer | Step 7 |

## Convention summary (preliminary; finalizes in 4c)

> **The pattern is event sourcing with an in-memory repository
> projection.** For each feature, three classes:
>
> - **Store** — the repository. Holds state + pure mutation API.
>   Never emits. Same class both sides.
> - **Service** — the command surface. Ctor-injects the store +
>   `IEventEmitter`. Public methods mutate via the store and emit
>   inline. Same class both sides.
> - **Apply handlers** — per-type classes implementing
>   `IWireEventHandler<T>` with `Apply` + `Unapply`. Ctor-inject the
>   **store** + `IHandlerRegistry` (never the service, never the
>   emitter), register themselves during
>   `IAsyncInitializable.InitializeAsync`, and re-execute the store's
>   mutation API. Cannot emit by construction. Step 4 ships `Apply`
>   only.
>
> **Scoped stream interfaces.** `EventStream` implements two scoped
> interfaces — `IEventEmitter` (emit only) and `IHandlerRegistry`
> (register only). Each caller's ctor surface narrows to what it
> actually needs; the type system blocks the wrong direction. Single
> impl, no NoOp variant — the narrowing is for Interface Segregation,
> not behavior toggling.
>
> **Side-agnostic.** Same store + service + handler classes register in
> any scope. Per-side asymmetry is one bridge each: `WireForwarder`
> (server, stream → network) and `NetworkInboundBridge` (client,
> network → stream). No `IsServer` / `IsClient` checks anywhere.
>
> **`Network` and `EventStream` don't reference each other.** The two
> bridges cross the boundary. The bucket-typed `EventStream` exposes
> both `Emit<T>` (typed fast path for services + the server-side
> forwarder) and `Emit(WireEvent)` (type-routing path for the inbound
> bridge).
>
> **Lifecycle.** Stores + services + handlers live for the VContainer
> scope's lifetime. Async init via `IAsyncInitializable`; teardown via
> `IAsyncDisposable`. Reset = dispose + reconstruct the scope (Appflow
> `Host.PopAsync` + `PushAsync`).

(Finalizes when 4c's validation pass runs.)
