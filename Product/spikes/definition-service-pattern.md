---
type: spike
status: in-design
created: 2026-05-20
references:
  - Product/spikes/wire-refoundation-roadmap.md
  - Product/spikes/definition-entity-setup.md
  - Product/spikes/step3-entity-phases.md
sandbox: Assets/Framework/Engine/, Assets/Framework/Bootstrap/
tags: [#spike, #services, #pattern, #architecture, #bootstrap]
---

# Service pattern (step 4)

> **Status: in-design.** Captures the locked direction from the 2026-05-20
> reframe. Step 4 ships a **scaffold + worked example**, not an abstract
> Q-walk. Pattern conventions emerge from the concrete code; this doc
> finalizes once 4a + 4b have shipped and the conventions stabilize.

## What step 4 is

Step 4 in [`wire-refoundation-roadmap.md`](wire-refoundation-roadmap.md): *"Service
pattern. Generalize the per-role service shape (command / effect / request /
state). Decide what's load-bearing and what's incidental."*

**Generalize**, not port. The deliverable is:

1. **The scaffold** — `GameEngine` stub + DI substrate (VContainer + Scaffold.Appflow)
   + `Framework.Engine` asmdef (pure C#) + `Framework.Bootstrap` asmdef
   (Unity-coupled) + a sandbox scene proving the bones work.
2. **One worked example** — `EntityService` (formalized
   `Framework.Entities/EntityStore`) registered into the scaffold via a
   layer installer; auto-emission lights up so server-side mutations
   round-trip to a client-side mirror through `Framework.Wire`.

The scaffold + worked example together **define** the service pattern by
example. Slice's specific services (Command / Effect / Trigger / Request)
move to later steps where they get the established pattern applied to
them.

**End-state demo (4d):** press Play on the sandbox scene → an IMGUI panel
shows "Engine bootstrapped" (4a milestone) → expand to "1 entity created,
3 tag events emitted, client mirror in sync" (4b milestone).

## Locked decisions

### L1 — Convention only, no `IService` interface or base class

A "service" is a plain class that obeys the conventions in this doc and is
registered into a VContainer scope by a layer's `Install`. There is no
`IService` marker, no `ServiceBase`, no shared virtual lifecycle hooks.
Each service has the surface it needs and no ceremony.

Compile-time enforcement is none; pattern drift is caught by review.
Matches slice's shape — slice has no shared service base either.

### L2 — Services emit through a direct wire reference

A service that emits events takes a wire capability at construction (per
Q-to-be-locked: the exact interface name) and calls it directly. No
event-callback indirection, no engine-mediated forwarding queue.

```csharp
public sealed class EntityService
{
    readonly IWireSender _wire;       // exact interface TBD in 4a
    public EntityService(IWireSender wire) { _wire = wire; }

    public Entity Create(EntityType type, int definitionId = 0)
    {
        var entity = ...;
        _wire.Send(new EntityCreatedEvent { ... });
        return entity;
    }
}
```

Trade-off: services that emit are tightly coupled to `Framework.Wire`.
Accepted because (a) emission is core to a service's role; (b) loose-coupling
indirection (slice's `Action<WireEvent>` events + engine subscription)
adds plumbing without offsetting clarity.

### L3 — DI substrate: VContainer + Scaffold.Appflow

Package manifest gains both:

```json
"com.scaffold.appflow": "https://github.com/MgCohen/Scaffold.git?path=/Assets/Packages/com.scaffold.appflow#main",
"jp.hadashikick.vcontainer": "https://github.com/hadashiA/VContainer.git?path=VContainer/Assets/VContainer#1.17.0"
```

Appflow provides the layer pattern (`AppFlowRoot` / `IScopeLayer` /
`IAsyncInitializable`) on top of VContainer. Services register through
`IScopeLayer.Install(IContainerBuilder)`; lifecycle hooks come from
`IAsyncInitializable` (post-construct async init) and `IAsyncDisposable`
(teardown).

Service-to-service dependencies = VContainer constructor injection. The
slice's `Bind*()` post-ctor wiring goes away — layers register first,
construction happens during container build, ctor injection resolves deps
in topological order.

### L4 — Scaffold home: split asmdefs

- `Framework.Engine` — pure C#. Holds `GameEngine`, `GameEngineBuilder` (if
  earned). No Unity dep. Composition primitives only.
- `Framework.Bootstrap` — Unity-coupled. Holds `FrameworkAppFlowRoot`
  (MonoBehaviour `AppFlowRoot` subclass), layer installers
  (`EngineLayer`, `EntityServiceLayer` later), sandbox scene + IMGUI
  status UI.

Reason: framework-proper stays Unity-independent (pure C# tests, faster
edit-mode iteration); the Unity glue (scene wiring, LifetimeScope
MonoBehaviour, IMGUI) is its own thing under `Bootstrap`.

### L5 — Step 4 ship scope

Phases 4a–4d (see [`step4-service-phases.md`](step4-service-phases.md)):

- **4a — Scaffold.** Manifest additions, `Framework.Engine`,
  `Framework.Bootstrap`, sandbox scene with "Engine bootstrapped" IMGUI.
  No services beyond an empty `GameEngine` shell.
- **4b — EntityService worked example.** `Framework.Entities/EntityStore`
  refactored / renamed to `EntityService`; registered via layer; auto-emission
  lit up; symmetric server/client.
- **4c — Pattern doc validation.** Read this doc against what shipped.
  Adjust doc to reality.
- **4d — Step 4 closure.** Status flip, handoff for step 5+.

### L6 — Receiving side: symmetric `EntityService`

Same `EntityService` class used on both sides. Server-side: ctor with
sending capability (sends on mutations). Client-side: ctor with receiving
capability (subscribes to channels, applies incoming events). One type
covers both directions.

Asymmetric split (separate `EntityService` + `MirrorEntityService`) earns
its keep when scrub / replay / Unapply matters. Defer until step 5+ or
whenever replay forces it.

### L7 — `World` does not exist as a separate substrate

The slice's `World` (event log + block-id allocator + open-block stack)
splits across services that need its pieces:

- Event log → already gone; `Framework.Wire` channels replace it.
- Block-id allocator + open-block stack → live on the services that bracket
  dispatch (Command / Effect). Don't exist in step 4 since neither service
  exists.

`EntityService` needs no piece of `World`. Step 4 ships no `World` type.

## Bootstrap shape (sketch)

The Unity scene wiring under `Framework.Bootstrap`:

```csharp
// Framework.Bootstrap/Runtime/FrameworkAppFlowRoot.cs
[DefaultExecutionOrder(-1000)]
public sealed class FrameworkAppFlowRoot : AppFlowRoot
{
    protected override IEnumerable<IScopeLayer> GetInitialLayers()
    {
        yield return new EngineLayer();        // 4a
        yield return new EntityServiceLayer(); // 4b
    }
}
```

```csharp
// Framework.Bootstrap/Runtime/EngineLayer.cs (4a)
internal sealed class EngineLayer : IScopeLayer
{
    public void Install(IContainerBuilder builder)
    {
        builder.Register<GameEngine>(Lifetime.Singleton);
        // Wire endpoint registration TBD per Q-to-be-locked.
    }
}
```

```csharp
// Framework.Bootstrap/Runtime/EntityServiceLayer.cs (4b)
internal sealed class EntityServiceLayer : IScopeLayer
{
    public void Install(IContainerBuilder builder)
    {
        builder.Register<EntityService>(Lifetime.Singleton);
    }
}
```

Sandbox scene: one MonoBehaviour child of the `FrameworkAppFlowRoot`
GameObject, IMGUI on top, displays state pulled from injected services:

```csharp
[Inject] GameEngine _engine;
[Inject] EntityService _entities;   // wired in 4b

void OnGUI() { GUI.Label(..., $"Engine bootstrapped. {entityCount} entities..."); }
```

## Open questions

Most questions from the prior draft are absorbed by the locked decisions.
What remains, scoped to what 4a and 4b need to settle, in dependency order:

### Q1 — Wire endpoint shape: how does a service take its wire capability?

`Framework.Wire`'s `Wire.Connect()` produces a `(server, client, serverNet,
clientNet)` tuple in tests. In a production bootstrap (sandbox scene),
where does the wire pair come from and how do services receive it?

- **A — One global `Wire` registered as singleton; services take it directly.**
  `EngineLayer.Install` registers a `Wire` instance; `EntityService` takes
  it via ctor. Server/client distinction = which methods the service calls
  (Send vs. subscribe). Simple; couples to `Wire` concrete.
- **B — Server endpoint + client endpoint registered separately as named
  capabilities.** E.g. `IServerWireSender` and `IClientWireReceiver`
  interfaces; layers register the right implementation depending on which
  side they're on. Cleaner separation; needs the interfaces.
- **C — Single capability covering both directions (send + subscribe)
  exposed as one interface.** `IWireBridge` or similar. Service uses
  whichever methods it needs; the wire instance decides which side it is
  by configuration.

**Lean: B.** Server-side `EntityService` takes `IServerWireSender`;
client-side takes `IClientWireReceiver`. Two distinct contexts; two
distinct interfaces. Easier to reason about (server-only methods can't be
called on client side) and easier to test (substitute either interface in
isolation).

Lock during 4a's wire-glue work.

### Q2 — Sandbox scene: one root or server + client split?

For 4b's "server mutates → client mirrors" demo, the scene needs both
sides running.

- **A — One scene, one `AppFlowRoot`, both sides scoped under it.** Server
  `EntityService` + client `EntityService` both registered, distinguished
  by typed key or named registration. Simplest scene setup.
- **B — Two `AppFlowRoot`s under one scene.** Each is its own
  `LifetimeScope`; they share the `Wire` instance via the parent scope.
  Cleaner separation; more scene complexity.
- **C — Two scenes loaded additively.** Maximum isolation; lots of
  ceremony for a sandbox demo.

**Lean: A** for step 4 sandbox. The two services don't need full isolation
to demo the wire round-trip; named/keyed registration handles the
"server vs client" distinction inside one scope.

Lock during 4b's sandbox-scene work.

### Q3 — `GameEngine`'s actual surface in step 4

After 4a, `GameEngine` has almost nothing inside (no Commands, no Effects,
no Triggers, no `World`). What does it actually hold and expose?

- **A — Empty marker class.** No fields, no methods. Exists so the
  composition root has a singleton; future services hang off it.
- **B — Service-bag pattern.** `GameEngine` holds a dictionary or
  typed-property bag of registered services. Generic access:
  `engine.Get<EntityService>()`. Pulls registration data into the engine.
- **C — Composition-shaped properties.** Public properties for each
  service we expect to add (`EntityService`, eventual `CommandService`,
  etc.). Some are null until their feature lands.

**Lean: A.** Services are resolved from VContainer directly; `GameEngine`
doesn't need to be a service locator. Future-step services hang off
`GameEngine` as properties when they actually exist. Step 4: empty
marker, possibly with `Frame` / tick state if needed for diagnostics.

Lock during 4a's `Framework.Engine` work.

### Q4 — `GameEngineBuilder`: needed in step 4?

User flagged "we probably need a builder" — but if VContainer's
`AppFlowRoot` + layers IS the composition mechanism, `GameEngineBuilder`
might be redundant.

- **A — Skip the builder.** Layers install services into the container;
  `GameEngine` is resolved from the container; no separate builder type.
- **B — Ship the builder.** `GameEngineBuilder` is a fluent API that
  produces a configured `GameEngine` for hosts that aren't using the
  `AppFlowRoot` scene-driven bootstrap (e.g. pure unit tests, server-only
  builds).
- **C — Ship a thin builder that wraps container build.** The builder is
  effectively `new ContainerBuilder()` + layer install + `Build()` — same
  thing AppFlow does internally, just exposed for non-Unity hosts.

**Lean: A for step 4.** Tests construct `EntityService` directly with a
fake wire interface; production goes through `AppFlowRoot`. Builder lives
in a Schrödinger state — light it up if a real consumer emerges, else
defer. Don't ship anticipatory.

Lock during 4a's `Framework.Engine` work.

### Q5 — `EntityService` specifics (mostly settled by leans, but worth confirming)

Carries over from the prior Q8 sub-list:

- **Q5a — Naming.** `EntityService` (rename from `EntityStore`). **Locked.**
- **Q5b — Auto-emission seam.** `Entity` raises internal `TagSet` /
  `TagChanged` / `TagRemoved` events; `EntityService.Create` subscribes
  per entity and routes through the wire. **Lean confirmed.**
- **Q5c — `TagChangeEvent<T>`.** Add it to `Framework.Tags` for symmetric
  Set/Change/Removed trio. **Lean confirmed.**
- **Q5d — Indexed queries (`With` / `Where`).** Defer to a later step.
  **Locked.**
- **Q5e — `Destroy` + `EntityDestroyedEvent`.** Defer. **Locked.**

Lock during 4b's `EntityService` work.

## Deferred items

| Item | Lights up when |
|---|---|
| `CommandService` / `EffectService` / `TriggerService` ports | Step 5+ applying the pattern |
| `RequestService` shape | Step 5+ |
| `ISBA` registry + fixpoint | When a game forces it |
| `MatchState` + `Session` lifecycle | Step 5 ("state services") |
| `ICommandValidator<T>` chain | Step 5+ |
| Ability abstraction (unify Subscribe/SetDirect/SBA) | When a 4th category earns it |
| `Entity.Destroy` + `EntityDestroyedEvent` | When `entity-identity-model.md` lands |
| `EntityService` indexed queries (`With` / `Where`) | When an author surface needs them |
| Asymmetric `MirrorEntityService` (scrub / Unapply) | When replay / time-travel forces it |
| `GameEngineBuilder` for non-Unity hosts | When a non-Unity consumer appears |
| View-model layer | Step 7 |

## Convention summary (preliminary; finalizes in 4c)

> **A service is** a plain class that owns one concern. It holds private
> state. Construction is by VContainer constructor injection via a
> `Scaffold.Appflow` `IScopeLayer`. Cycles forbidden (use lazy interface
> resolution if forced).
>
> **Wire connection.** Services that emit take a typed wire capability
> at construction (server-side: `IServerWireSender`; client-side:
> `IClientWireReceiver`) and call it directly.
>
> **Access boundaries.** (To be finalized after 4b ships.) Tentative:
> public methods for setup / tests; scoped views for authors during
> dispatch (introduced step 5+ when author surfaces exist).
>
> **Lifecycle.** Services live for the VContainer scope's lifetime.
> Async init via `IAsyncInitializable`; teardown via `IAsyncDisposable`.
> Reset = dispose + reconstruct the scope (Appflow `Host.PopAsync` +
> `PushAsync`).

(Finalizes when 4c's validation pass runs.)
