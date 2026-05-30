---
type: spike
status: active
created: 2026-05-20
references:
  - Product/spikes/wire-refoundation-roadmap.md
  - Product/spikes/definition-entity-setup.md
  - Product/spikes/step3-entity-phases.md
sandbox: Assets/Framework/Engine/, Assets/Framework/Bootstrap/
tags: [#spike, #services, #pattern, #architecture, #bootstrap, #handlers]
---

# Service pattern (step 4)

> **Status: active.** L1–L11 shipped through 4a + 4b. The 4e post-ship
> reshape (see [`step4-service-phases.md`](step4-service-phases.md))
> closed the "service double-write" deferral and tightened L2/L6:
> services are emit-only writers (handler is the sole writer on both
> sides); `TagService` / `EntityService` / `GameEngine` register only
> on the server scope. L1, L3–L5, L7, L8, L10 carry through unchanged.
> This doc reflects shipped reality as of `9d9d5c9`.

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

### L2 — No `IsServer` / `IsClient` runtime checks; asymmetry lives in registration

No service has an "am I server?" branch in its method bodies. Per-side
behavior comes from **which classes are registered** in each scope:

- **Stores + apply handlers**: register on both sides. Same code reads
  + reconstructs state identically on server and client.
- **Command services** (`TagService`, `EntityService`): register on the
  **server only**. The client cannot author state — making that a
  compile-time impossibility removes a class of latent bugs.
- **`GameEngine`** (composes the authoring services): server only.
- **Bridges**: `WireForwarder` (server) or `NetworkInboundBridge`
  (client) — exactly one per scope.

The no-bounce invariant has two layers:

1. **Service is emit-only.** `TagService.Set` peeks the store for dedup,
   then emits — it never writes. The synchronous dispatch runs the
   per-type handler, which is the sole writer. Same writer on both
   sides; no double-write on the server.
2. **Handlers don't hold an emit-capable reference.** They ctor-inject
   the store only. Calling `Emit` from inside a handler is a type
   error.

Together: events flow author → service → emit → handler → store. Same
graph both sides. Diff is who initiates: on the server, end-user code
calls the service. On the client, the wire delivers the event into the
local `EventStream`.

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

### L6 — Stores + handlers install on both sides; services install server-only; handler is the sole writer

Storage, command/emit, and apply are three concerns in three places:

- **`EntityStore`** (`TagStore`) — the in-memory repository. Pure
  mutation API. Never emits. **Registers on both sides.**
- **`EntityService`** (`TagService`) — the command surface. Peeks the
  store (for dedup + to pick the right event), then emits. Never
  writes. **Registers on the server only.**
- **Apply handlers** (`EntityCreatedHandler`, `TagSetHandler<T>`,
  `TagChangeHandler<T>`, `TagRemovedHandler`) — per-type writers that
  ctor-inject the store only. Each handler's `Apply` is the only path
  that mutates the store on its respective side. **Register on both
  sides.**

The per-side asymmetry is one bridge each:

- Server scope installs `WireForwarder` (subscribes to `EventStream`,
  pushes every appended event to `Network`).
- Client scope installs `NetworkInboundBridge` (subscribes to
  `Network.OnReceived`, emits the decoded event into the client's
  `EventStream`).

**No double-write on the server.** The service emits; the synchronous
dispatch runs the handler; the handler does the only write. Wire
forwarding (server) and inbound emit (client) are decoupled from apply
— they cross the network boundary in parallel to the handler chain on
each side.

**Equality short-circuit lives in the service.** `TagService.Set`
reads the store to compare prev vs new — reads aren't mutations, this
is fine. If equal, the service returns without emitting; no event, no
client traffic, no handler fire.

The asymmetric `EntityService` + separate `EntityMirror` shape was
considered and rejected (Q1 Option A) on these grounds:

- Two owners for the entity dictionary
- Awkward ownership for future preview/rollback work
- Type-count saving was overstated

The `IEventEmitter` + `NoOpEventEmitter` *per-side toggle* was
considered and rejected: it was a way to silence echo on the server.
The current shape eliminates echo structurally a different way — the
service doesn't write (so there's nothing to echo) and the client
doesn't have a service (so no client-originated emit exists). The
`IEventEmitter` interface that ships in L8 is a different concept —
single impl, ISP narrowing of `EventStream`, no NoOp variant.

**Reshape note (4e).** The original L6 design had services register on
both sides; server-side handler runs were idempotent no-ops because the
service had already written. The wiring fix in `c94595e` made handlers
actually run end-to-end, which exposed the wasted server-side write.
The 4e reshape (`f7c8af4` + `9d9d5c9`) moved to emit-only services +
role-gated registration — the shape this section now describes. The
"register both, no-op on server" rationale no longer applies.

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

**`EntityStore`** (and `TagStore`) is a plain repository. Reads + raw
writes; never emits. After 4e, the store also owns id allocation:
`AllocateId()` reserves the next id without inserting, so the service
can emit-first and the handler does the actual insert under the
reserved id.

```csharp
public sealed class EntityStore
{
    readonly Dictionary<int, Entity> _byId = new();
    int _nextId = 1;

    public int AllocateId() => _nextId++;

    // Auto-id insert — direct-store convenience for tests. Production
    // authoring goes through EntityService.Create (which AllocateId + emits).
    public Entity Insert(EntityType type, int defId = 0) { ... }

    // Explicit-id insert — idempotent. The handler path.
    public Entity Insert(int id, EntityType type, int defId) { ... }

    public Entity ById(int id);
    public bool   TryGetById(int id, out Entity entity);
}
```

```csharp
public sealed class TagStore
{
    public T    Get<T>(int entityId, int slot) where T : unmanaged;
    public bool Has(int entityId, int slot);
    public void Set<T>(int entityId, int slot, T value) where T : unmanaged;
    public bool Remove(int entityId, int slot);
}
```

**`EntityService`** / **`TagService`** are the command surfaces.
Emit-only. They never call store mutation methods (`Insert`, `Set`,
`Remove`) — only read methods (`AllocateId`, `Has`, `Get`). The
synchronous handler dispatch runs the per-type handler, which is the
sole writer.

```csharp
public sealed class EntityService
{
    readonly EntityStore _store;
    readonly IEventEmitter _events;

    public EntityService(EntityStore store, IEventEmitter events)
    { _store = store; _events = events; }

    public Entity Create(EntityType type, int definitionId = 0)
    {
        var id = _store.AllocateId();
        _events.Emit(new EntityCreatedEvent {
            EntityId = id, TypeSlot = type.Slot, DefinitionId = definitionId,
        });
        return _store.ById(id);   // handler ran synchronously; entity exists.
    }

    public Entity ById(int id) => _store.ById(id);
    public bool   TryGetById(int id, out Entity entity) => _store.TryGetById(id, out entity);
}
```

```csharp
public sealed class TagService
{
    readonly TagStore _store;
    readonly IEventEmitter _events;

    public TagService(TagStore store, IEventEmitter events)
    { _store = store; _events = events; }

    public T    Get<T>(int entityId, TagKey<T> key) where T : unmanaged
        => _store.Get<T>(entityId, key.Slot);
    public bool Has(int entityId, TagKey key) => _store.Has(entityId, key.Slot);

    public void Set<T>(int entityId, TagKey<T> key, T value) where T : unmanaged
    {
        if (_store.Has(entityId, key.Slot))
        {
            var prev = _store.Get<T>(entityId, key.Slot);
            if (EqualityComparer<T>.Default.Equals(prev, value)) return;   // peek-only dedup
            _events.Emit(new TagChangeEvent<T> {
                EntityId = entityId, Slot = key.Slot, Prev = prev, Next = value,
            });
        }
        else
        {
            _events.Emit(new TagSetEvent<T> {
                EntityId = entityId, Slot = key.Slot, Value = value,
            });
        }
    }

    public bool Remove(int entityId, TagKey key)
    {
        if (!_store.Has(entityId, key.Slot)) return false;
        _events.Emit(new TagRemovedEvent { EntityId = entityId, Slot = key.Slot });
        return true;   // dispatch is synchronous; handler removed by now.
    }
}
```

**`Entity`** is data-only. No service back-ref, no slot-mutation methods.
Tag storage lives in `TagStore` keyed by `(entityId, slot)` — `Entity`
doesn't own its own slots.

```csharp
public sealed class Entity
{
    public int        Id           { get; }
    public EntityType Type         { get; }
    public int        DefinitionId { get; }

    internal Entity(int id, EntityType type, int definitionId) { ... }
}
```

**End-user authoring** (server side) goes through the services:

```csharp
var card = engine.Entities.Create(EntityTypes.Card, definitionId: 101);
engine.Tags.Set(card.Id, SampleTags.Health, 30);
```

The fluent `entity.Set(...)` chain from step 3 is gone — its
ergonomics required Entity to hold a back-ref. The trade is: cleaner
separation now, no Pattern-B fluency on Entity. If the call-site verbosity
bites, we can re-add a typed handle (`EntityHandle` struct wrapping
`(id, service)`) without touching `Entity` itself.

**Apply handlers** depend on the store, not the service. They physically
cannot emit — the type system enforces no-bounce. After 4e they're also
the **sole writer** on each side (the service stopped writing). See L10
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
both registered by boot layers. The shipped shape subscribes via the
single `EventStream.OnAppended` / `Network.OnReceived` events rather
than per-type `IWireEventHandler` registration — fewer moving parts,
no per-T forwarder enumeration. Ctor side-effect; eager-resolved at
container build via `RegisterBuildCallback`.

- **`WireForwarder`** (server only) — every event appended to the local
  `EventStream` goes to `Network`.

```csharp
internal sealed class WireForwarder
{
    public WireForwarder(EventStream stream, Network network)
        => stream.OnAppended += network.Send;
}
```

- **`NetworkInboundBridge`** (client only) — every event delivered by
  `Network.OnReceived` is emitted into the local `EventStream`, where
  Apply handlers dispatch by runtime type.

```csharp
internal sealed class NetworkInboundBridge
{
    public NetworkInboundBridge(Network network, IEventEmitter emitter)
        => network.OnReceived += emitter.Emit;
}
```

**`HandlerRegistry`** (not a bridge, but the same shape) — collects
every `IWireEventHandler` registered in the scope via DI list injection,
subscribes to `EventStream.OnAppended` once, and dispatches by runtime
type. Handlers themselves are POCOs that ctor-inject their store; no
`IAsyncInitializable` ceremony per handler. See `c94595e` for the
rationale (the prior `IAsyncInitializable` self-registration pattern
silently no-op'd because open-generic registration didn't trigger
init).

Neither `Network` nor `EventStream` names the other. Boot wires them at
scope init. The same two bridge classes serve every feature; per-event
ceremony lives in the per-type Apply handlers, not in the bridges. The
typed-bucket dispatch (L8) means the inbound bridge needs no switch
statement — the bucket carries the type information.

### L11 — `GameEngine` is a server-only composition handle

`GameEngine` in `Framework.Engine` is a typed handle that exposes the
substrate + the authoring services for end-user server-side game code.
It's **registered only on the server scope** — the client has nothing
to compose under it (its `EventStream` + stores inject cleanly on their
own; the layer files themselves are the documented client surface).

```csharp
public sealed class GameEngine
{
    public EventStream   Stream      { get; }
    public Network       Network     { get; }
    public EntityStore   EntityStore { get; }
    public TagStore      TagStore    { get; }
    public EntityService Entities    { get; }
    public TagService    Tags        { get; }
    // step 5+ adds: Requests, Commands, Effects, Triggers, MatchState, ...
}
```

- **End-user server game code** prefers `engine.Entities.Create(...)`,
  `engine.Tags.Set(...)`, `engine.EntityStore.TryGetById(...)`,
  `engine.Stream.OnAppended += ...`. Discoverable, typed surface.
- **Internal plumbing** (handlers, bridges, framework-internal services)
  ctor-injects pieces directly. `GameEngine` is for ergonomics, not for
  plumbing.
- **Client code** uses `[Inject] EventStream`, `[Inject] EntityStore`,
  `[Inject] TagStore` directly. There is no `ClientEngine` — when the
  client acquires multiple coordinated concerns (input, request reader,
  connection state), each will live in its own service; whether a
  client-shaped handle earns its name will be a real question then.

The type changes whenever a framework service ships — `Framework.Engine`
gains a new property + ctor param. Stability is traded for
discoverability; accepted.

**Reshape note (4e).** L11 originally had `GameEngine` registered on
both sides and exposing only services (not stores). The 4e role-gating
made server-only the natural answer (it composes services the client
doesn't have), and stores were added as properties so server reads
share the same handle as authoring. The two-engine split
(`ServerEngine` + `ClientEngine`) was considered and rejected: the
client had nothing to put on its engine that wasn't already
injectable; pre-naming a shape with no caller earns nothing.

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

The same store + service + handler split applies to every future
feature. The role split is consistent across the table:

| Feature | Store (both sides) | Service (server only, emit-only) | Apply handlers (both sides, sole writer) |
|---|---|---|---|
| **Entities** (step 4) | `EntityStore`, `TagStore` | `EntityService`, `TagService` | `EntityCreatedHandler`, `TagSetHandler<T>`, `TagChangeHandler<T>`, `TagRemovedHandler` |
| **Requests** (step 5+) | `RequestStore` | `RequestService` | `RequestOpenedHandler`, `RequestClosedHandler` |
| **Commands** (step 5+) | `CommandStore` (open-block tracking) | `CommandService` | `BlockStartHandler`, `BlockEndHandler` |
| **Effects** (step 5+) | shared with commands | `EffectService` | shared handlers |

Each **store** is pure C# (repository contract: reads + raw writes, no
events). Registers on both sides.

Each **service** ctor-injects its store + `IEventEmitter`. Peeks the
store to decide what to emit; emits only; never writes. Registers on
the server scope only (layer takes `isServer` and gates the
registration). Surfaces on `GameEngine` as a property.

Each **apply handler** ctor-injects the store only (no
`IEventEmitter`). Performs the matching store mutation. Registers on
both sides — same handler is the sole writer on the server (where the
service emitted) and on the client (where the wire delivered the
event).

The `WireForwarder` (server) and `NetworkInboundBridge` (client) are
universal — one line each, no per-feature additions.

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

## Convention summary

> **The pattern is event sourcing with an in-memory repository
> projection.** For each feature, three classes with a strict role split:
>
> - **Store** — the repository. Holds state + pure mutation API.
>   Never emits. **Registers on both sides** (same class).
> - **Service** — the command surface. Ctor-injects the store +
>   `IEventEmitter`. **Emit-only**: peeks the store for dedup +
>   discrimination, then emits. Never writes. **Registers on the
>   server only**; the layer takes `isServer` and gates the
>   registration. Surfaces on `GameEngine` as a property.
> - **Apply handlers** — per-type classes deriving from
>   `WireEventHandler<T>`. Ctor-inject the **store only** (no
>   `IEventEmitter`, no service). Perform the matching mutation. POCOs;
>   collected via DI list injection into `HandlerRegistry`, which
>   subscribes to `EventStream.OnAppended` once and dispatches by
>   runtime type. **Sole writer on each side** — same handler code runs
>   on the server (where the service emitted) and on the client (where
>   the wire delivered the event). **Registers on both sides.**
>
> **Scoped stream interface.** `EventStream` implements `IEventEmitter`
> (emit-only) — services and the inbound bridge see this narrower
> surface. The full `EventStream` (with `OnAppended` + history) is
> resolvable too; handlers + the registry use it directly. Single impl;
> no NoOp variant.
>
> **No runtime `IsServer` checks.** Asymmetry lives in registration: the
> service is missing from the client scope; `GameEngine` is missing
> from the client scope; the bridge type differs (`WireForwarder` server,
> `NetworkInboundBridge` client). Method bodies never branch on side.
>
> **No double-write on the server.** The service doesn't write; the
> handler does. The equality short-circuit lives in the service (reads
> aren't writes); when it triggers, no event is emitted at all.
>
> **`Network` and `EventStream` don't reference each other.** The two
> bridges cross the boundary. The bucket-typed `EventStream` exposes
> both `Emit<T>` (typed fast path) and `Emit(WireEvent)` (type-routing
> path for the inbound bridge).
>
> **Lifecycle.** Stores + services + handlers + bridges live for the
> VContainer scope's lifetime. `HandlerRegistry` + bridges wire via
> ctor side-effects and are eager-resolved at container build via
> `RegisterBuildCallback`. No per-handler `IAsyncInitializable`
> ceremony. Teardown = dispose the scope (Appflow `Host.PopAsync` +
> `PushAsync`).
