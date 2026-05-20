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

> **Status: in-design.** Captures the locked direction through the
> 2026-05-20 Q1 walk. Direction confirmed but final details pending; flip
> to `status: active` once 4a + 4b have shipped and the convention
> stabilizes.

## What step 4 is

Step 4 in [`wire-refoundation-roadmap.md`](wire-refoundation-roadmap.md): *"Service
pattern. Generalize the per-role service shape (command / effect / request /
state). Decide what's load-bearing and what's incidental."*

The deliverable is:

1. **Scaffold** — `GameEngine` stub + DI substrate (VContainer + Scaffold.Appflow)
   + `Framework.Engine` asmdef (pure C#) + `Framework.Bootstrap` asmdef
   (Unity-coupled) + a sandbox scene proving the bones work.
2. **Worked example** — `EntityService` (formalized
   `Framework.Entities/EntityStore`) plus the **handler-based event
   command pattern** (see §Locked decisions L8) so server-side mutations
   round-trip to a client-side mirror through `Framework.Wire`, **without
   either side carrying side-aware code**.

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

### L2 — Services are side-agnostic; emission lives in handlers

**Revised from earlier "services hold wire ref" framing** based on the
2026-05-20 Q1 walk. The original "service holds `IWireSender`" coupled
services to wire knowledge and forced the symmetric/asymmetric class
question. Resolution: services don't know about wire at all. Domain
events raised by the service feed into per-side handler classes that
translate to/from wire events. See L8.

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

- `Framework.Engine` — pure C#. Holds `GameEngine`, `GameEngineBuilder`
  (if earned). No Unity dep. Composition primitives only.
- `Framework.Bootstrap` — Unity-coupled. Holds `FrameworkAppFlowRoot`
  (MonoBehaviour `AppFlowRoot` subclass), layer installers
  (`ServerEntityLayer`, `ClientEntityLayer`), sandbox scene + IMGUI
  status UI.

### L5 — Step 4 ship scope

Phases 4a–4d (see [`step4-service-phases.md`](step4-service-phases.md)):

- **4a — Scaffold.** Manifest, asmdefs, sandbox scene, "Engine bootstrapped"
  IMGUI. No services beyond an empty `GameEngine` shell.
- **4b — `EntityService` + handler pattern.** Worked example carrying
  the full handler-based event command pattern (L8).
- **4c — Pattern doc validation.** Read this doc against shipped code.
- **4d — Step 4 closure.** Status flip, handoff for step 5+.

### L6 — Single `EntityService` class on both sides

Same `EntityService` class instantiated in both server and client scopes.
Identical implementation; identical state shape (a `Dictionary<int, Entity>`
+ `_nextId`). No `IsServer` / `IsClient` runtime check inside the service.

**Asymmetric behavior comes from which handlers are registered in each
scope's layers** (L8), not from any code branch inside `EntityService`.

The asymmetric `EntityService` + separate `EntityMirror` shape was
considered and rejected (Q1 Option A) on these grounds:
- Two owners for the entity dictionary
- Awkward ownership for future preview/rollback work
- Type-count saving was overstated

### L7 — `World` does not exist as a separate substrate

The slice's `World` (event log + block-id allocator + open-block stack)
splits across services that need its pieces. Step 4 ships no `World`
type. `EntityService` needs no piece of it.

### L8 — Handler-based event command pattern (the architectural lock)

**Every wire event type has handler classes.** Two flavors, matching the
direction of translation:

- **Emit handlers** (server-side): subscribe to **domain events** raised
  by services (e.g. `EntityService.OnCreated`, `entity.OnTagSet`); produce
  the appropriate wire event; write to `EventStream`. Direction: domain → wire.
- **Apply handlers** (client-side): implement `IWireEventHandler<TEvent>`;
  receive the typed event from `EventStream`; call mutation methods on
  the local service. Direction: wire → domain.

**Why this shape:**

1. Each event type has one place that knows how to translate it. No
   switch cases scattered across multiple places.
2. Handlers are small (10–20 LOC), focused, and ctor-inject only what
   they need. Comfortable to test in isolation.
3. The asymmetry between server (producer) and client (consumer) is
   honest about what's happening on each side. No fake symmetry.
4. Server-side can register cross-cutting handlers (logging,
   rollback buffer, debug) by adding to the layer registration. Same
   pattern, different concern.
5. Reproducible: same `Emit*` / `Apply*` shape scales to RequestService,
   CommandService, etc. as later steps add them.

**`WireEventDispatcher`** lives in `Framework.Wire` as the central
infrastructure. It subscribes once to `EventStream.OnAppended`, routes
events by runtime type to all `IWireEventHandler<TEvent>`s registered
for that type. Per-type dispatch via `Dictionary<Type, List<Action<WireEvent>>>`.

```csharp
// Framework.Wire/Runtime/Events/IWireEventHandler.cs
public interface IWireEventHandler<TEvent> where TEvent : WireEvent
{
    void Handle(TEvent ev);
}

// Framework.Wire/Runtime/Events/WireEventDispatcher.cs
public sealed class WireEventDispatcher
{
    readonly Dictionary<Type, List<Action<WireEvent>>> _byType = new();

    public WireEventDispatcher(EventStream stream)
        => stream.OnAppended += Dispatch;

    public void Register<TEvent>(IWireEventHandler<TEvent> handler) where TEvent : WireEvent
    {
        if (!_byType.TryGetValue(typeof(TEvent), out var list))
            _byType[typeof(TEvent)] = list = new();
        list.Add(ev => handler.Handle((TEvent)ev));
    }

    void Dispatch(WireEvent ev)
    {
        if (_byType.TryGetValue(ev.GetType(), out var list))
            foreach (var h in list) h(ev);
    }
}
```

**Emit handlers do NOT use `IWireEventHandler<T>`.** They subscribe to
domain events (e.g. `EntityService.OnCreated`, `entity.OnTagSet`) and
write to `EventStream` directly. The interface is for the receive side
only; lying about it with a unified interface obscures the data flow.

(Sub-Q: confirm separate Emit / Apply concepts — see Open Questions.)

## Bootstrap shape

```csharp
// Framework.Bootstrap/Runtime/FrameworkAppFlowRoot.cs
[DefaultExecutionOrder(-1000)]
public sealed class FrameworkAppFlowRoot : AppFlowRoot
{
    protected override IEnumerable<IScopeLayer> GetInitialLayers()
    {
        yield return new EngineLayer();          // 4a
        yield return new ServerEntityLayer();    // 4b (server scope wiring)
        yield return new ClientEntityLayer();    // 4b (client scope wiring)
    }
}
```

For the sandbox demo, server + client run as keyed registrations in the
same scope (per pending Q2 lean); or under sibling child scopes (TBD
during 4b).

## Concrete shape sketch (subject to 4a/4b implementation)

### EntityService — pure C#, side-agnostic

```csharp
public sealed class EntityService
{
    readonly Dictionary<int, Entity> _byId = new();
    int _nextId = 1;

    public event Action<Entity>? OnCreated;

    public Entity Create(EntityType type, int defId = 0)
        => CreateInternal(_nextId++, type, defId);

    public Entity Create(int presetId, EntityType type, int defId = 0)
        => CreateInternal(presetId, type, defId);

    Entity CreateInternal(int id, EntityType type, int defId)
    {
        var e = new Entity(id, type, defId);
        _byId[id] = e;
        if (id >= _nextId) _nextId = id + 1;
        OnCreated?.Invoke(e);
        return e;
    }

    public Entity? Get(int id) => _byId.GetValueOrDefault(id);
}
```

### Entity — raises its own internal events on mutation

```csharp
public sealed class Entity
{
    readonly Dictionary<int, object> _slots = new();

    public int Id { get; }
    public EntityType Type { get; }
    public int DefinitionId { get; }

    internal event Action<TagKey, object>?              OnTagSet;
    internal event Action<TagKey, object, object>?      OnTagChanged;
    internal event Action<TagKey, object>?              OnTagRemoved;

    internal Entity(int id, EntityType type, int defId) { Id = id; Type = type; DefinitionId = defId; }

    public void Set<T>(TagKey<T> key, T value) where T : unmanaged
    {
        if (_slots.TryGetValue(key.Slot, out var raw))
        {
            var prev = (T)raw;
            if (EqualityComparer<T>.Default.Equals(prev, value)) return;
            _slots[key.Slot] = value;
            OnTagChanged?.Invoke(key, prev!, value!);
        }
        else
        {
            _slots[key.Slot] = value;
            OnTagSet?.Invoke(key, value!);
        }
    }

    // ... Get / Has / Remove similar
}
```

### Emit handlers — server-side, subscribe to domain events

```csharp
// One handler per wire event type. Small. Focused.
internal sealed class EmitEntityCreated
{
    public EmitEntityCreated(EntityService entities, EventStream stream)
    {
        entities.OnCreated += e => stream.Add(new EntityCreatedEvent {
            EntityId = e.Id, TypeSlot = e.Type.Slot, DefinitionId = e.DefinitionId
        });
    }
}

// Generic on the tag value type. One class, multiple closed registrations.
internal sealed class EmitTagSet<T> where T : unmanaged
{
    public EmitTagSet(EntityService entities, EventStream stream)
    {
        entities.OnCreated += entity =>
        {
            entity.OnTagSet += (key, value) =>
            {
                if (value is T t)
                    stream.Add(new TagSetEvent<T> { EntityId = entity.Id, Slot = key.Slot, Value = t });
            };
        };
    }
}

// EmitTagChange<T>, EmitTagRemoved — analogous
```

### Apply handlers — client-side, implement `IWireEventHandler<T>`

```csharp
internal sealed class ApplyEntityCreated : IWireEventHandler<EntityCreatedEvent>
{
    readonly EntityService _entities;
    public ApplyEntityCreated(EntityService entities) { _entities = entities; }

    public void Handle(EntityCreatedEvent ev)
        => _entities.Create(ev.EntityId, EntityTypeRegistry.BySlot(ev.TypeSlot), ev.DefinitionId);
}

internal sealed class ApplyTagSet<T> : IWireEventHandler<TagSetEvent<T>> where T : unmanaged
{
    readonly EntityService _entities;
    public ApplyTagSet(EntityService entities) { _entities = entities; }

    public void Handle(TagSetEvent<T> ev)
        => _entities.Get(ev.EntityId)?.Set((TagKey<T>)TagRegistry.BySlot(ev.Slot), ev.Value);
}
```

### Layer registration

```csharp
// Server scope: EntityService + Emit handlers + (no Apply handlers)
internal sealed class ServerEntityLayer : IScopeLayer
{
    public void Install(IContainerBuilder b)
    {
        b.Register<EntityService>(Lifetime.Singleton);
        b.Register<WireEventDispatcher>(Lifetime.Singleton);
        b.RegisterEntryPoint<EmitEntityCreated>(Lifetime.Singleton);
        b.RegisterEntryPoint<EmitTagSet<int>>(Lifetime.Singleton);
        b.RegisterEntryPoint<EmitTagSet<bool>>(Lifetime.Singleton);
        b.RegisterEntryPoint<EmitTagChange<int>>(Lifetime.Singleton);
        b.RegisterEntryPoint<EmitTagChange<bool>>(Lifetime.Singleton);
        b.RegisterEntryPoint<EmitTagRemoved>(Lifetime.Singleton);
    }
}

// Client scope: EntityService + Apply handlers + (no Emit handlers)
internal sealed class ClientEntityLayer : IScopeLayer
{
    public void Install(IContainerBuilder b)
    {
        b.Register<EntityService>(Lifetime.Singleton);
        b.Register<WireEventDispatcher>(Lifetime.Singleton);
        b.Register<IWireEventHandler<EntityCreatedEvent>, ApplyEntityCreated>(Lifetime.Singleton);
        b.Register<IWireEventHandler<TagSetEvent<int>>,  ApplyTagSet<int>>(Lifetime.Singleton);
        b.Register<IWireEventHandler<TagSetEvent<bool>>, ApplyTagSet<bool>>(Lifetime.Singleton);
        // ... + ApplyTagChange<T>, ApplyTagRemoved
    }
}
```

The dispatcher's per-type registry gets populated at scope build via a
`WireHandlerInstaller<TEvent> : IAsyncInitializable` bridge (one per
closed type) or a one-time `IPostBuildCallback`. Exact mechanism is a
detail to settle during 4a's dispatcher implementation.

## Pattern reproducibility

The same `Emit*` / `Apply*` + dispatcher pattern applies to every future
service:

| Future service | Emit handlers (server) | Apply handlers (client) |
|---|---|---|
| **RequestService** (step 5+) | `EmitRequestOpened`, `EmitRequestClosed` | `ApplyRequestOpened`, `ApplyRequestClosed` |
| **CommandService** (step 5+) | `EmitBlockStart`, `EmitBlockEnd` | `ApplyBlockStart`, `ApplyBlockEnd` |
| **EffectService** (step 5+) | `EmitBlockStart` (Effect kind), `EmitBlockEnd` | `ApplyBlockStart`, `ApplyBlockEnd` |

Each service stays side-agnostic and pure C#. Its wire integration is
expressed entirely in handlers.

## Open questions (remaining)

Walk during 4a / 4b implementation as they come up:

### Q1 — Final direction confirmation (pending user verdict)

The handler-based pattern in L8. **Status: proposed; pending user confirmation
before 4a starts.** Specifically:

1. `WireEventDispatcher` + `IWireEventHandler<TEvent>` infrastructure in
   Framework.Wire.
2. Emit / Apply asymmetry (separate concepts; only Apply has the marker
   interface).
3. Single `EntityService` class both sides; per-side via layer
   registration only.

### Q2 — Sandbox scene: one root or two scopes?

For 4b's "server mutates → client mirrors" demo, the scene needs both
sides:

- **A** — One scene, one `AppFlowRoot`; server + client distinguished by
  keyed registration. Simplest.
- **B** — Two `AppFlowRoot`s in the same scene; shared `Wire` instance via
  parent scope. Cleaner separation.

**Lean: A.** Lock during 4b's sandbox-scene work.

### Q3 — `GameEngine`'s actual surface

- **A** — Empty marker class. Services hang off the container, not on
  `GameEngine`.
- **B** — Service-bag pattern (`engine.Get<T>()`).
- **C** — Composition-shaped properties.

**Lean: A.** Services resolved from VContainer; `GameEngine` is just a
singleton root. Lock during 4a's `Framework.Engine` work.

### Q4 — `GameEngineBuilder` needed in step 4?

**Lean: no.** Tests construct `EntityService` directly with fakes;
production uses `AppFlowRoot`. Defer until a non-Unity consumer appears.
Lock during 4a's `Framework.Engine` work.

### Q5 — EntityService specifics (mostly already locked)

- **Q5a — Naming.** `EntityService` (rename from `EntityStore`).
  **Locked.**
- **Q5b — Auto-emission seam.** Per L8 — `Entity` raises internal events;
  `EmitX` handlers subscribe. **Locked.**
- **Q5c — `TagChangeEvent<T>`.** Add to `Framework.Tags` for symmetric
  Set/Change/Removed trio. **Locked.**
- **Q5d — Indexed queries (`With` / `Where`).** Defer. **Locked.**
- **Q5e — `Destroy` + `EntityDestroyedEvent`.** Defer. **Locked.**

### Q6 — Per-T closed-type registration ergonomics

`Layer.Install` enumerates closed tag-value types
(`EmitTagSet<int>`, `EmitTagSet<bool>`, ...). For step 4 this is ~6 lines
across both layers. As tag types grow, this becomes friction.

- **Lean: accept manual enumeration for step 4.** Future: source-gen
  picks up registered tag types and emits closed registrations. Tracked
  as `oq-handler-codegen`.

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
| Preview / rollback buffering on Emit handlers | When the speculation feature is designed |
| View-model layer | Step 7 |

## Convention summary (preliminary; finalizes in 4c)

> **A service is** a plain class that owns one concern and holds private
> state. It is **side-agnostic** — same class instantiated in any scope;
> behavior asymmetry comes from which handlers the scope's layers
> register. Construction is by VContainer constructor injection via a
> `Scaffold.Appflow` `IScopeLayer`. Cycles forbidden (use lazy interface
> resolution if forced).
>
> **Wire integration** is expressed through **handlers**, not on the
> service itself. **Emit handlers** subscribe to the service's domain
> events and write wire events to `EventStream`; **Apply handlers**
> implement `IWireEventHandler<TEvent>` and apply received events to
> the local service. The `WireEventDispatcher` routes by event runtime
> type.
>
> **Lifecycle.** Services + handlers live for the VContainer scope's
> lifetime. Async init via `IAsyncInitializable`; teardown via
> `IAsyncDisposable`. Reset = dispose + reconstruct the scope (Appflow
> `Host.PopAsync` + `PushAsync`).

(Finalizes when 4c's validation pass runs.)
