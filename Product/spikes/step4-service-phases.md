---
type: spike
status: in-design
created: 2026-05-20
references:
  - Product/spikes/wire-refoundation-roadmap.md
  - Product/spikes/definition-service-pattern.md
  - Product/spikes/step3-entity-phases.md
sandbox: Assets/Framework/Engine/, Assets/Framework/Bootstrap/
tags: [#spike, #services, #phasing, #session-handoff, #handlers]
---

# Step 4 — Service pattern phasing

> Session-handoff record. The 7-step refoundation roadmap names step 4 as
> "Service pattern"; the locked design lives in
> [`definition-service-pattern.md`](definition-service-pattern.md). Step 4
> ships **the scaffold + `EntityService` worked example with the
> service-emit + per-type Apply-handler pattern**; specific services
> (Commands, Effects, Triggers, Request, State) get the pattern applied
> in step 5+.

## Phase breakdown

| Phase | Scope | Status | Commit |
|---|---|---|---|
| 4a | Scaffold — VContainer + Appflow in manifest; `Framework.Engine` (pure C#, empty `GameEngine` shell); `Framework.Bootstrap` (Unity-coupled, `FrameworkAppFlowRoot` + `EngineLayer` + sandbox scene); IMGUI says "Engine bootstrapped"; **`EventStream` gains `Register<T>` + `Emit` dispatch surface; `IWireEventHandler<T>` + `IEventEmitter` + `NoOpEventEmitter` land in `Framework.Wire`** | ⏳ | — |
| 4b | `EntityService` worked example — single class both sides with `IEventEmitter`-injected emit + author/apply API split; per-type Apply handler classes register on `EventStream`; `WireForwarder` (server) + `NetworkInboundBridge` (client) cross the Network ↔ EventStream boundary; sandbox extends to "create card → mirror in sync" | ⏳ | — |
| 4c | Pattern doc validation — read `definition-service-pattern.md` against the shipped code; finalize convention summary; resolve any "felt forced" items | ⏳ | — |
| 4d | Closure — phasing rows → ✅ with commit hashes; definition doc `status: in-design → active`; cross-link from `wire-refoundation-roadmap.md` step 4 line | ⏳ | — |

After 4d: scaffold + `EntityService` operational with full service-emit
+ per-type Apply round-trip. Bootstrap a scene, press Play, create an
entity + tag mutations on server, see client mirror sync via dispatched
Apply handlers. The slice's `Engine/Pipeline/` stays as reference for
step 5+.

## Phase scope

### 4a — Scaffold + EventStream dispatch surface

**No services beyond an empty `GameEngine` stub.** This phase proves
the bones work: DI substrate, layer system, scene wiring, IMGUI feedback,
**plus the `EventStream` dispatch surface + `IWireEventHandler<T>` +
`IEventEmitter` that 4b's handlers will hang off of**.

**Manifest changes:**

```json
"com.scaffold.appflow": "https://github.com/MgCohen/Scaffold.git?path=/Assets/Packages/com.scaffold.appflow#main",
"jp.hadashikick.vcontainer": "https://github.com/hadashiA/VContainer.git?path=VContainer/Assets/VContainer#1.17.0"
```

**New asmdef: `Framework.Engine`** (`Assets/Framework/Engine/`)

- Pure C#; no Unity dep.
- References `Framework.Wire` (and in 4b, `Framework.Entities` +
  `Framework.Tags`) so `GameEngine` can carry typed service properties
  (per L11).
- `GameEngine` — composition-shaped class. In 4a: `Stream` + `Network`
  properties. (4b adds `Entities`.)
- No `GameEngineBuilder` in step 4 (Q4 — defer until a non-Unity
  consumer needs it).
- Test asmdef ships too (may be empty in 4a).

**New asmdef: `Framework.Bootstrap`** (`Assets/Framework/Bootstrap/`)

- Unity-coupled; depends on VContainer, Scaffold.Appflow,
  `Framework.Engine`, `Framework.Wire`.
- Two `AppFlowRoot` subclasses (Q2 — two-roots-per-scene shape):
  - `ServerAppFlowRoot : AppFlowRoot` — installs `EngineLayer` (and
    later `ServerEntityLayer` in 4b).
  - `ClientAppFlowRoot : AppFlowRoot` — installs `EngineLayer` (and
    later `ClientEntityLayer` in 4b).
- `EngineLayer : IScopeLayer` — registers `GameEngine` + `EventStream`
  + `Network` as singletons. Side-agnostic; the per-side bindings for
  `IEventEmitter` come from the entity layers in 4b.
- `BootstrapStatusUI` — MonoBehaviour with `[Inject] GameEngine _engine`,
  IMGUI panel. Instantiated under each root.
- `SandboxTransport` — scene-level MonoBehaviour (or scene init script)
  that creates an in-memory transport and injects it into both `Network`
  instances at startup.

**`Framework.Wire` additions:**

- `IWireEventHandler<TEvent>` — `void Apply(TEvent ev)` +
  `void Unapply(TEvent ev)` (Unapply throws `NotImplementedException`
  until the rollback feature is designed).
- `IEventEmitter` interface — `void Emit(WireEvent ev)`.
- `NoOpEventEmitter` — drops on the floor; client-side binding.
- `EventStream` gains `Register<TEvent>(IWireEventHandler<TEvent>)` +
  `Emit(WireEvent)` over an internal
  `Dictionary<Type, List<Action<WireEvent>>>` dispatch table.
- (Possibly) extension method to expose `EventStream`-as-`IEventEmitter`
  for the server-side DI binding (`stream.AsEmitter()`).

**Sandbox scene:** `Assets/Framework/Bootstrap/Sandbox/BootstrapSandbox.unity`
with `ServerRoot` + `ClientRoot` GameObjects (each with its
`AppFlowRoot` subclass + a `BootstrapStatusUI`) + a `SandboxTransport`
scene object. Both status panels show **"Engine bootstrapped. 0
entities."** with their side label.

**Tests:** Edit-mode test that constructs a VContainer scope via
`EngineLayer`, resolves `GameEngine` + `EventStream` + `Network`,
asserts non-null and that `GameEngine.Stream` / `GameEngine.Network`
return the registered singletons. Smoke test for `EventStream.Register`
+ `Emit` dispatch (one fake handler, one fake event type). No Unity
scene dep.

**Closure:** sandbox scene runs in Play mode, both IMGUI panels show
the "bootstrapped" message, Unity batch tests green.

### 4b — `EntityService` worked example with per-type Apply handlers

**Touches:** `Framework.Entities`, `Framework.Tags`, `Framework.Wire`,
`Framework.Bootstrap`.

**`Framework.Entities` changes:**

- Rename `EntityStore` → `EntityService`. Drop the wire-aware
  constructor variants from 3d.
- `EntityService` ctor-injects `IEventEmitter`. Public `Create` mutates
  state and calls `_emitter.Emit(new EntityCreatedEvent { ... })`.
- Internal `ApplyCreated(int id, EntityType type, int defId)` for client
  Apply handlers — mutates without emitting.
- `Entity` ctor-injects `IEventEmitter` too. Public `Set<T>` mutates +
  emits `TagSetEvent<T>` (first write) or `TagChangeEvent<T>`
  (subsequent write) inline.
- Internal `Entity.ApplyTagSet<T>(int slot, T value)` +
  `ApplyTagRemoved(int slot)` — mutate without emitting.
- No `OnCreated` / `OnTagSet` domain events — emission is direct via
  `IEventEmitter`, no intermediate seam.

**New `Framework.Entities/Runtime/Wire/` folder** holding per-type Apply
handlers (client only; server registers none of these):

- `EntityCreatedHandler : IWireEventHandler<EntityCreatedEvent>` —
  `Apply` calls `entities.ApplyCreated(...)`. `Unapply` throws.
- `TagSetHandler<T> : IWireEventHandler<TagSetEvent<T>>` — `Apply`
  calls `entities.Get(id)?.ApplyTagSet(slot, value)`.
- `TagChangeHandler<T> : IWireEventHandler<TagChangeEvent<T>>` — same,
  applies `Next`.
- `TagRemovedHandler : IWireEventHandler<TagRemovedEvent>` — `Apply`
  calls `entities.Get(id)?.ApplyTagRemoved(slot)`.

Each handler is `IAsyncInitializable`; its `InitializeAsync` calls
`_stream.Register<TEvent>(this)`.

**`Framework.Tags` changes:**

- Add `TagChangeEvent<T>` Pattern A wire event (carries `Prev` + `Next`)
  for symmetric Set/Change/Removed trio.

**`Framework.Wire` additions:**

- `WireForwarder` (server only) — implements `IWireEventHandler<TEvent>`
  for each known entity event type; on `Apply`, calls `Network.Send(ev)`.
  Registers itself for each type during `InitializeAsync`. (Manual
  enumeration in step 4; source-gen later — Q6.)
- `NetworkInboundBridge` (client only) — subscribes to `Network`'s
  typed receive surface for each event type; on receive, calls
  `EventStream.Emit(ev)`. Symmetric to `WireForwarder`.

**`Framework.Bootstrap` changes:**

- `ServerEntityLayer : IScopeLayer` — registers `EntityService`;
  binds `IEventEmitter` → real `EventStream` (e.g. `stream.AsEmitter()`);
  `RegisterEntryPoint<WireForwarder>`. No Apply handlers.
- `ClientEntityLayer : IScopeLayer` — registers `EntityService`;
  binds `IEventEmitter` → `NoOpEventEmitter`;
  `RegisterEntryPoint<NetworkInboundBridge>`; per-type
  `RegisterEntryPoint<EntityCreatedHandler>` /
  `<TagSetHandler<int>>` / `<TagSetHandler<bool>>` /
  `<TagChangeHandler<int>>` / `<TagChangeHandler<bool>>` /
  `<TagRemovedHandler>` (manual list; source-gen later).
- `ServerAppFlowRoot.GetInitialLayers()` adds `EngineLayer` +
  `ServerEntityLayer`; `ClientAppFlowRoot.GetInitialLayers()` adds
  `EngineLayer` + `ClientEntityLayer`.
- `GameEngine` gains an `Entities` property (ctor injection).
- `BootstrapStatusUI` extended — counter of entities + recent events list,
  side label.
- Sandbox button (on server root only): "Create card" → calls
  `serverEngine.Entities.Create(EntityTypes.Card, defId: 101).Set(SampleTags.Health, 30)`.
  Both IMGUI panels show the events flowing + the client entity
  reconstructed.

**Tests:**

- Existing `EntityWireRoundTripTests` / `WireRoundTripTests` (migrated 3e)
  simplify substantially — no longer hand-construct events; the service
  emit path does it inline.
- New: symmetric round-trip test — server scope mutates via `Create` +
  `Set`, client scope mirrors via Apply handlers; assert both bags match
  after a sequence of Set / Change / Remove operations.
- New: loop-safety test — invoke client `EntityService.Create` directly;
  verify the no-op emitter swallows the emit (nothing reaches
  `EventStream` on the client).

**Defer (per pattern doc):**

- `Destroy` + `EntityDestroyedEvent` emission (Q5e).
- Indexed queries (`With` / `Where`) (Q5d).
- Source-gen handler registration (Q6).

**Closure:** sandbox scene + Unity batch tests green; pattern doc's
sketch matches shipped code.

### 4c — Pattern doc validation

Read [`definition-service-pattern.md`](definition-service-pattern.md)
against the 4a + 4b implementation. The locked sections (L1–L8) should
hold. Convention summary at the bottom of the doc gets finalized.

For anything that felt forced — call it out. Adjust either:

- Pattern doc (the convention was wrong → relax it), or
- Implementation (the pattern is right → fix the code).

Tests stay green. Note shifts in the commit message.

**Closure:** pattern doc accurately describes the shipped code.

### 4d — Step 4 closure

- Phasing rows → ✅ with commit hashes.
- `definition-service-pattern.md` banner: `status: in-design → active`.
- Cross-link from `wire-refoundation-roadmap.md` step 4 line to both
  spike docs.
- Brief handoff note: scaffold + pattern proven; step 5+ applies the
  pattern to Commands / Effects / Triggers / Request / State.

**Closure:** step 5 session can pick up cold from these docs.

## Deferred items

Carried forward; nothing in this list is in scope for step 4:

| Item | Lights up when |
|---|---|
| `CommandService` / `EffectService` / `TriggerService` ports | Step 5+ |
| `RequestService` shape | Step 5+ |
| `ISBA` registry + fixpoint | When a game forces it |
| `MatchState` + `Session` lifecycle | Step 5 |
| `ICommandValidator<T>` chain | Step 5+ |
| Ability abstraction (unify Subscribe/SetDirect/SBA) | When a 4th category earns it |
| `Entity.Destroy` + `EntityDestroyedEvent` | When `entity-identity-model.md` lands |
| `EntityService` indexed queries | When an author surface needs them |
| Source-gen per-T handler registration | When manual enumeration bites |
| `GameEngineBuilder` (non-Unity hosts) | When a non-Unity consumer appears |
| Preview / rollback buffering on Emit handlers | When the speculation feature is designed |
| View-model layer | Step 7 |

## When this lands

Step 4 graduates from "in-progress" to "done" when 4d ships green. The
slice's `Engine/Pipeline/` folder stays intact as reference. Step 5
(existing services reviewed — applying the pattern to Commands / Effects
/ Triggers / Request / State) picks up from here.
