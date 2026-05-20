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
> 2026-05-20 walk; all open Qs resolved (Q1–Q7). Flip to `status: active`
> once 4a + 4b have shipped and the convention stabilizes against actual
> code.

## What step 4 is

Step 4 in [`wire-refoundation-roadmap.md`](wire-refoundation-roadmap.md): *"Service
pattern. Generalize the per-role service shape (command / effect / request /
state). Decide what's load-bearing and what's incidental."*

The deliverable is:

1. **Scaffold** — `GameEngine` stub + DI substrate (VContainer + Scaffold.Appflow)
   + `Framework.Engine` asmdef (pure C#) + `Framework.Bootstrap` asmdef
   (Unity-coupled) + a sandbox scene proving the bones work.
2. **Worked example** — `EntityService` (formalized
   `Framework.Entities/EntityStore`) plus the **service-emit + per-type
   Apply-handler pattern** (see §Locked decisions L8–L10) so server-side
   mutations round-trip to a client-side mirror through `Framework.Wire`
   with same-class services on both sides and no `IsServer` / `IsClient`
   runtime branches.

The scaffold + worked example together **define** the service pattern by
example. Slice's specific services (Command / Effect / Trigger / Request)
move to later steps where they get the established pattern applied.

**End-state demo (4d):** press Play on the sandbox scene → IMGUI shows
"Engine bootstrapped" (4a milestone) → expand to "1 entity created, tag
events emitted, client mirror in sync" (4b milestone), with both sides
running in the same scene under one `FrameworkAppFlowRoot`.

## Locked decisions

### L1 — Convention only, no `IService` interface or base class

A "service" is a plain class that owns one concern, holds private state,
and is registered into a VContainer scope by a layer's `Install`. There
is no `IService` marker, no `ServiceBase`, no shared virtual lifecycle
hooks. Each service has the surface it needs and no ceremony.

### L2 — Services are side-agnostic; asymmetry lives in DI binding only

Same service class is registered in any scope. No `IsServer` /
`IsClient` runtime check inside service code. Per-side behavior comes
from layer composition:

- **Which other classes are registered alongside** the service (Apply
  handlers, bridges, forwarders)
- **Which implementations bind** to the service's dependencies
  (e.g. `IEventEmitter` → real EventStream on the server, → no-op on the
  client)

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
  layer installers (`EngineLayer`, `ServerEntityLayer`,
  `ClientEntityLayer`), sandbox scene + IMGUI status UI.

### L5 — Step 4 ship scope

Phases 4a–4d (see [`step4-service-phases.md`](step4-service-phases.md)):

- **4a — Scaffold.** Manifest, asmdefs, two `AppFlowRoot` subclasses,
  sandbox scene, "Engine bootstrapped" IMGUI. `EventStream` gains its
  `Register<T>` / `Emit` dispatch surface + `IWireEventHandler<T>` +
  `IEventEmitter` + `NoOpEventEmitter`; `Network` surface is reviewed
  against L10. `GameEngine` exposes `Stream` + `Network` properties
  (4b adds `Entities`).
- **4b — `EntityService` + per-type Apply handlers.** Worked example
  carrying the full pattern (L8–L10): service emits via `IEventEmitter`,
  per-type Apply handlers register on `EventStream`, `WireForwarder` +
  `NetworkInboundBridge` cross the network boundary.
- **4c — Pattern doc validation.** Read this doc against shipped code.
- **4d — Step 4 closure.** Status flip, handoff for step 5+.

### L6 — Single `EntityService` class on both sides

Same `EntityService` class instantiated in both server and client scopes.
Identical implementation; identical state shape (a `Dictionary<int, Entity>`
+ `_nextId`).

Asymmetric behavior comes from L2: server scope binds `IEventEmitter`
to a real `EventStream` and registers `WireForwarder`; client scope binds
`IEventEmitter` to a no-op and registers `NetworkInboundBridge` plus all
per-type Apply handlers.

The asymmetric `EntityService` + separate `EntityMirror` shape was
considered and rejected (Q1 Option A) on these grounds:

- Two owners for the entity dictionary
- Awkward ownership for future preview/rollback work
- Type-count saving was overstated

### L7 — `World` does not exist as a separate substrate

The slice's `World` (event log + block-id allocator + open-block stack)
splits across services that need its pieces. Step 4 ships no `World`
type. `EntityService` needs no piece of it.

### L8 — `EventStream` is the dispatch surface; no separate dispatcher

`EventStream` (in `Framework.Wire`) owns the per-type dispatch table
internally. There is no `WireEventDispatcher`. There is no
`HandlerRegistry`. The table is an implementation detail of
`EventStream`.

```csharp
// Framework.Wire/Runtime/Events/IWireEventHandler.cs
public interface IWireEventHandler<TEvent> where TEvent : WireEvent
{
    void Apply(TEvent ev);
    void Unapply(TEvent ev);   // rollback seam; throws NotImplementedException in step 4
}

// Framework.Wire/Runtime/Events/EventStream.cs
public sealed class EventStream
{
    readonly Dictionary<Type, List<Action<WireEvent>>> _byType = new();
    readonly List<WireEvent> _history = new();

    public IReadOnlyList<WireEvent> History => _history;

    public void Register<TEvent>(IWireEventHandler<TEvent> handler) where TEvent : WireEvent
    {
        if (!_byType.TryGetValue(typeof(TEvent), out var list))
            _byType[typeof(TEvent)] = list = new();
        list.Add(ev => handler.Apply((TEvent)ev));
    }

    public void Emit(WireEvent ev)
    {
        _history.Add(ev);
        if (_byType.TryGetValue(ev.GetType(), out var list))
            foreach (var h in list) h(ev);
    }
}
```

`Emit` is a single operation: append to history, dispatch to handlers
registered for the event's runtime type. No distinction between
"author emits" and "received apply" at the EventStream level — both call
`Emit`. The asymmetry between sides is in *which handlers are
registered*, not in two API verbs.

Apply handlers are per-type classes implementing
`IWireEventHandler<T>`. They live in the same asmdef as the service
whose state they apply to (e.g.
`Framework.Entities/Runtime/Wire/EntityCreatedHandler.cs`). Each
ctor-injects the service it mutates + the `EventStream`. The handler is
registered on `EventStream` via an `IAsyncInitializable` init step
called by VContainer at scope build (per L11 below; lock specifics
during 4a).

Step 4 ships `Apply` only; `Unapply` throws `NotImplementedException`
until the rollback feature is designed (Deferred items).

### L9 — Services emit via `IEventEmitter`; author / apply API split

Services that emit ctor-inject `IEventEmitter` — an emit-only narrowing
of `EventStream`:

```csharp
// Framework.Wire/Runtime/Events/IEventEmitter.cs
public interface IEventEmitter
{
    void Emit(WireEvent ev);
}

internal sealed class NoOpEventEmitter : IEventEmitter
{
    public void Emit(WireEvent ev) { /* drop */ }
}
```

DI bindings:

- **Server:** `IEventEmitter` → the scope's `EventStream` (real emit).
- **Client:** `IEventEmitter` → `NoOpEventEmitter` (drops on the floor).

Same service code path both sides; emission is silent on the client by
DI binding. The no-op is the structural backstop: even if the public
author API is hit on the client by mistake, nothing leaks.

Services that emit have two surfaces:

- **Public author API** (`Create`, `Set`, `Remove`) — mutates state +
  emits via `IEventEmitter`. Used by server-side callers.
- **Internal apply API** (`ApplyCreated`, `ApplyTagSet<T>`,
  `RevertCreated`, ...) — mutates state without emitting. Used by
  per-type Apply handlers on the client.

The internal apply API is the loop-safe path (Apply handlers never trip
the emit seam). The no-op binding catches anything that slips through.

### L10 — `Network` and `EventStream` don't reference each other; two bridge classes cross

`Network` (in `Framework.Wire`) is a dumb pipe: serialization +
transport. Its surface:

```csharp
Network.Register<TEvent>();     // declare type for serializer
Network.Send(WireEvent ev);
// inbound: typed delivery to subscribers
```

Channels are a serialization detail — not exposed structurally.

Two thin bridge classes cross the `Network` ↔ `EventStream` boundary,
both registered by boot layers:

- **`WireForwarder`** (server only): registers as
  `IWireEventHandler<TEvent>` on `EventStream` for each known event
  type; on `Apply`, calls `Network.Send`. (Server side: "Apply" here
  means "an emission happened, forward it out.")
- **`NetworkInboundBridge`** (client only): subscribes to `Network`'s
  typed receive surface for each event type; on receive, calls
  `EventStream.Emit`, which dispatches to the registered per-type Apply
  handlers.

Neither `Network` nor `EventStream` names the other. Boot wires them at
scope init. The same two bridge classes serve every feature; per-event
ceremony lives in the per-type Apply handlers, not in the bridges.

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
`AppFlowRoot`s** (Q2 — revised lean confirmed 2026-05-20). Each side
has its own scope, its own `EventStream`, its own `GameEngine`. They
communicate via `Network` instances wired to a shared in-memory
transport at the scene level.

```csharp
// Framework.Bootstrap/Runtime/ServerAppFlowRoot.cs
[DefaultExecutionOrder(-1000)]
public sealed class ServerAppFlowRoot : AppFlowRoot
{
    protected override IEnumerable<IScopeLayer> GetInitialLayers()
    {
        yield return new EngineLayer();          // 4a — GameEngine + EventStream + Network
        yield return new ServerEntityLayer();    // 4b — EntityService (real IEventEmitter) + WireForwarder
    }
}

// Framework.Bootstrap/Runtime/ClientAppFlowRoot.cs
[DefaultExecutionOrder(-1000)]
public sealed class ClientAppFlowRoot : AppFlowRoot
{
    protected override IEnumerable<IScopeLayer> GetInitialLayers()
    {
        yield return new EngineLayer();          // 4a — GameEngine + EventStream + Network
        yield return new ClientEntityLayer();    // 4b — EntityService (no-op IEventEmitter) + Bridge + Apply handlers
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
runtimes that happen to share a scene for demo purposes" — and avoids
the keyed-registration fight when `IEventEmitter` binds differently per
side.

## Concrete shape sketch (subject to 4a/4b implementation)

### EntityService — pure C#, side-agnostic, author + internal apply surfaces

```csharp
public sealed class EntityService
{
    readonly Dictionary<int, Entity> _byId = new();
    readonly IEventEmitter _emitter;
    int _nextId = 1;

    public EntityService(IEventEmitter emitter) { _emitter = emitter; }

    // ── Public author API ────────────────────────────────────────────
    // Server-side callers use these. Mutates + emits.

    public Entity Create(EntityType type, int defId = 0)
    {
        var e = CreateInternal(_nextId++, type, defId);
        _emitter.Emit(new EntityCreatedEvent {
            EntityId = e.Id, TypeSlot = type.Slot, DefinitionId = defId
        });
        return e;
    }

    public Entity? Get(int id) => _byId.GetValueOrDefault(id);

    // ── Internal apply API ───────────────────────────────────────────
    // Client-side Apply handlers use these. Mutates only; never emits.

    internal Entity ApplyCreated(int id, EntityType type, int defId)
        => CreateInternal(id, type, defId);

    Entity CreateInternal(int id, EntityType type, int defId)
    {
        var e = new Entity(id, type, defId, _emitter);
        _byId[id] = e;
        if (id >= _nextId) _nextId = id + 1;
        return e;
    }
}
```

### Entity — emits its own tag events via the same `IEventEmitter`

```csharp
public sealed class Entity
{
    readonly Dictionary<int, object> _slots = new();
    readonly IEventEmitter _emitter;

    public int Id { get; }
    public EntityType Type { get; }
    public int DefinitionId { get; }

    internal Entity(int id, EntityType type, int defId, IEventEmitter emitter)
    {
        Id = id; Type = type; DefinitionId = defId; _emitter = emitter;
    }

    // ── Public author API ────────────────────────────────────────────

    public void Set<T>(TagKey<T> key, T value) where T : unmanaged
    {
        if (_slots.TryGetValue(key.Slot, out var raw))
        {
            var prev = (T)raw;
            if (EqualityComparer<T>.Default.Equals(prev, value)) return;
            _slots[key.Slot] = value;
            _emitter.Emit(new TagChangeEvent<T> {
                EntityId = Id, Slot = key.Slot, Prev = prev, Next = value
            });
        }
        else
        {
            _slots[key.Slot] = value;
            _emitter.Emit(new TagSetEvent<T> {
                EntityId = Id, Slot = key.Slot, Value = value
            });
        }
    }

    public T? Get<T>(TagKey<T> key) where T : unmanaged
        => _slots.TryGetValue(key.Slot, out var raw) ? (T)raw : default;

    // ── Internal apply API ───────────────────────────────────────────
    // Client-side Apply handlers call these. Mutates without emit.

    internal void ApplyTagSet<T>(int slot, T value) where T : unmanaged
        => _slots[slot] = value;

    internal void ApplyTagRemoved(int slot)
        => _slots.Remove(slot);
}
```

### Per-type Apply handlers — client-side, register on `EventStream`

```csharp
// Framework.Entities/Runtime/Wire/EntityCreatedHandler.cs
internal sealed class EntityCreatedHandler
    : IWireEventHandler<EntityCreatedEvent>, IAsyncInitializable
{
    readonly EntityService _entities;
    readonly EventStream _stream;

    public EntityCreatedHandler(EntityService entities, EventStream stream)
    { _entities = entities; _stream = stream; }

    public UniTask InitializeAsync(CancellationToken ct)
    {
        _stream.Register<EntityCreatedEvent>(this);
        return UniTask.CompletedTask;
    }

    public void Apply(EntityCreatedEvent ev)
        => _entities.ApplyCreated(ev.EntityId,
                                  EntityTypeRegistry.BySlot(ev.TypeSlot),
                                  ev.DefinitionId);

    public void Unapply(EntityCreatedEvent ev)
        => throw new NotImplementedException("Rollback lights up later");
}

internal sealed class TagSetHandler<T>
    : IWireEventHandler<TagSetEvent<T>>, IAsyncInitializable
    where T : unmanaged
{
    readonly EntityService _entities;
    readonly EventStream _stream;

    public TagSetHandler(EntityService entities, EventStream stream)
    { _entities = entities; _stream = stream; }

    public UniTask InitializeAsync(CancellationToken ct)
    {
        _stream.Register<TagSetEvent<T>>(this);
        return UniTask.CompletedTask;
    }

    public void Apply(TagSetEvent<T> ev)
        => _entities.Get(ev.EntityId)?.ApplyTagSet(ev.Slot, ev.Value);

    public void Unapply(TagSetEvent<T> ev)
        => throw new NotImplementedException("Rollback lights up later");
}

// TagChangeHandler<T>, TagRemovedHandler — analogous
```

### Bridges — one each, registered per side, in `Framework.Wire`

```csharp
// Server side: subscribe to EventStream emissions; push to Network.
internal sealed class WireForwarder
    : IWireEventHandler<EntityCreatedEvent>,
      IWireEventHandler<TagSetEvent<int>>,
      IWireEventHandler<TagSetEvent<bool>>,
      // ... per-event-type, one impl
      IAsyncInitializable
{
    readonly EventStream _stream;
    readonly Network _network;

    public WireForwarder(EventStream stream, Network network)
    { _stream = stream; _network = network; }

    public UniTask InitializeAsync(CancellationToken ct)
    {
        _stream.Register<EntityCreatedEvent>(this);
        _stream.Register<TagSetEvent<int>>(this);
        _stream.Register<TagSetEvent<bool>>(this);
        // ...
        return UniTask.CompletedTask;
    }

    public void Apply(EntityCreatedEvent ev)  => _network.Send(ev);
    public void Apply(TagSetEvent<int> ev)    => _network.Send(ev);
    public void Apply(TagSetEvent<bool> ev)   => _network.Send(ev);
    // ...
    public void Unapply<T>(T ev) => throw new NotImplementedException();
}

// Client side: subscribe to Network receives; push to EventStream.
internal sealed class NetworkInboundBridge : IAsyncInitializable
{
    readonly EventStream _stream;
    readonly Network _network;

    public NetworkInboundBridge(EventStream stream, Network network)
    { _stream = stream; _network = network; }

    public UniTask InitializeAsync(CancellationToken ct)
    {
        _network.Subscribe<EntityCreatedEvent>(ev => _stream.Emit(ev));
        _network.Subscribe<TagSetEvent<int>>(ev => _stream.Emit(ev));
        _network.Subscribe<TagSetEvent<bool>>(ev => _stream.Emit(ev));
        // ...
        return UniTask.CompletedTask;
    }
}
```

(Bridge implementations enumerate event types manually in step 4;
source-gen takes over later — same plan as Q6.)

### Layer registration

```csharp
// Server scope
internal sealed class ServerEntityLayer : IScopeLayer
{
    public void Install(IContainerBuilder b)
    {
        b.Register<EventStream>(Lifetime.Singleton);
        b.Register<IEventEmitter>(c => c.Resolve<EventStream>().AsEmitter(),
                                  Lifetime.Singleton);   // real binding
        b.Register<Network>(Lifetime.Singleton);
        b.Register<EntityService>(Lifetime.Singleton);

        b.RegisterEntryPoint<WireForwarder>(Lifetime.Singleton);

        // NO Apply handlers on server.
    }
}

// Client scope
internal sealed class ClientEntityLayer : IScopeLayer
{
    public void Install(IContainerBuilder b)
    {
        b.Register<EventStream>(Lifetime.Singleton);
        b.Register<IEventEmitter, NoOpEventEmitter>(Lifetime.Singleton);
        b.Register<Network>(Lifetime.Singleton);
        b.Register<EntityService>(Lifetime.Singleton);

        b.RegisterEntryPoint<NetworkInboundBridge>(Lifetime.Singleton);

        // Per-type Apply handlers. Manual enumeration in step 4; source-gen later (Q6).
        b.RegisterEntryPoint<EntityCreatedHandler>(Lifetime.Singleton);
        b.RegisterEntryPoint<TagSetHandler<int>>(Lifetime.Singleton);
        b.RegisterEntryPoint<TagSetHandler<bool>>(Lifetime.Singleton);
        b.RegisterEntryPoint<TagChangeHandler<int>>(Lifetime.Singleton);
        b.RegisterEntryPoint<TagChangeHandler<bool>>(Lifetime.Singleton);
        b.RegisterEntryPoint<TagRemovedHandler>(Lifetime.Singleton);
    }
}
```

`RegisterEntryPoint` makes the handler an `IAsyncInitializable`
participant; its `InitializeAsync` is where `_stream.Register<TEvent>(this)`
runs. Exact mechanism (entry-point vs. `IPostBuildCallback`) settles
during 4a's `EventStream` work.

## Pattern reproducibility

The same shape applies to every future service:

| Future service | Service emits | Per-type Apply handlers (client) |
|---|---|---|
| **RequestService** (step 5+) | `RequestOpenedEvent`, `RequestClosedEvent` | `RequestOpenedHandler`, `RequestClosedHandler` |
| **CommandService** (step 5+) | `BlockStartEvent`, `BlockEndEvent` | `BlockStartHandler`, `BlockEndHandler` |
| **EffectService** (step 5+) | same block events, Effect kind | shared handlers |

Each service stays side-agnostic and pure C#. Its wire integration:
ctor-injected `IEventEmitter` + per-type Apply handler classes
co-located in the same asmdef.

## Resolved questions

All step-4 design Qs locked as of 2026-05-20. Implementation phases own
the specifics; the verdicts here are binding.

### Q1 — Final direction confirmation ✅

Locked as L8–L10:

- `EventStream` is the dispatch surface (no separate dispatcher class).
- `IWireEventHandler<T>` carries `Apply` + `Unapply`; per-type classes
  register on `EventStream`.
- Services emit via `IEventEmitter` (real on server, no-op on client).
- Author / apply API split on services that emit.
- `Network` and `EventStream` are decoupled; `WireForwarder` +
  `NetworkInboundBridge` cross the boundary.

### Q2 — Sandbox scene shape ✅

**Two `AppFlowRoot`s in one scene.** Each side has its own scope, its
own `EventStream`, its own `EntityService`, its own `GameEngine`. They
share a scene-level in-memory transport that wires the two `Network`
instances together. Honest about "two runtimes for the demo," avoids
keyed-registration fights with per-side `IEventEmitter` bindings.

### Q3 — `GameEngine`'s surface ✅

**Composition-shaped properties.** Per L11 — `GameEngine` carries a
typed property per framework service (`Stream`, `Network`, `Entities`,
...). Discoverable end-user surface; internal plumbing still
ctor-injects services directly.

### Q4 — `GameEngineBuilder` in step 4 ✅

**No.** Tests construct `EntityService` directly with fakes; production
uses `AppFlowRoot`. Defer until a non-Unity consumer appears.

### Q5 — EntityService specifics ✅

- **Q5a — Naming.** `EntityService` (rename from `EntityStore`).
- **Q5b — Emit seam.** Per L9 — service + entity ctor-inject
  `IEventEmitter`; emit inline. No EmitX handler classes.
- **Q5c — `TagChangeEvent<T>`.** Add to `Framework.Tags` for symmetric
  Set/Change/Removed trio.
- **Q5d — Indexed queries (`With` / `Where`).** Defer.
- **Q5e — `Destroy` + `EntityDestroyedEvent`.** Defer.
- **Q5f — Internal apply API surface.** `ApplyCreated`, `ApplyTagSet<T>`,
  `ApplyTagRemoved` on service/entity. Naming reviewed during 4b.

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

> **A service is** a plain class that owns one concern and holds private
> state. It is **side-agnostic** — same class instantiated in any scope;
> behavior asymmetry comes from layer registration (which handlers + which
> binding for `IEventEmitter`). Construction is by VContainer constructor
> injection via a `Scaffold.Appflow` `IScopeLayer`. Cycles forbidden (use
> lazy interface resolution if forced).
>
> **Services that emit** ctor-inject `IEventEmitter` and call
> `_emitter.Emit(...)` inline on mutations. They expose a **public author
> API** (mutates + emits) and an **internal apply API** (mutates without
> emit) used by client-side Apply handlers.
>
> **Apply handlers** are per-type classes implementing
> `IWireEventHandler<TEvent>` with `Apply` + `Unapply`. They register on
> `EventStream` during `IAsyncInitializable.InitializeAsync` and call
> internal apply methods on the service. Step 4 ships `Apply` only.
>
> **`Network` and `EventStream` don't reference each other.** Two boot-
> registered bridges cross the boundary: `WireForwarder` (server,
> stream → network) and `NetworkInboundBridge` (client, network → stream).
>
> **Lifecycle.** Services + handlers live for the VContainer scope's
> lifetime. Async init via `IAsyncInitializable`; teardown via
> `IAsyncDisposable`. Reset = dispose + reconstruct the scope (Appflow
> `Host.PopAsync` + `PushAsync`).

(Finalizes when 4c's validation pass runs.)
