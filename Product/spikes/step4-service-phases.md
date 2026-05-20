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
| 4a | Scaffold — VContainer + Appflow in manifest; `Framework.Engine` (pure C#, `GameEngine` with `Stream` + `Network` properties); `Framework.Bootstrap` (Unity-coupled, two `AppFlowRoot` subclasses + `EngineLayer` + sandbox scene); IMGUI says "Engine bootstrapped"; **`EventStream` gains typed-bucket `Register<T>` + typed `Emit<T>` + non-generic `Emit(WireEvent)`; `IWireEventHandler<T>` lands in `Framework.Wire`** | 🧪 verify | `3c1ed43` |
| 4b | `EntityStore` + `EntityService` worked example — repository (store) + command/emit (service) split; same classes both sides; per-type Apply handlers depend on `EntityStore`; `WireForwarder` (server, `IWireEventHandler<T>`-shaped) + `NetworkInboundBridge` (client, one-liner) cross the Network ↔ EventStream boundary; sandbox extends to "create card on server → mirror in sync on client" | ⏳ | — |
| 4c | Pattern doc validation — read `definition-service-pattern.md` against the shipped code; finalize convention summary; resolve any "felt forced" items | ⏳ | — |
| 4d | Closure — phasing rows → ✅ with commit hashes; definition doc `status: in-design → active`; cross-link from `wire-refoundation-roadmap.md` step 4 line | ⏳ | — |

> **Status legend.** ⏳ not started · 🧪 code landed, awaiting Unity batch verification · ✅ verified green.

After 4d: scaffold + `EntityStore`/`EntityService` operational with
full event-sourced round-trip. Bootstrap the sandbox scene (two
`AppFlowRoot`s), press Play, create an entity + tag mutations on server,
see client mirror sync via dispatched Apply handlers reading the
client's `EntityStore`. The slice's `Engine/Pipeline/` stays as reference
for step 5+.

## Phase scope

### 4a — Scaffold + EventStream dispatch surface

**No stores or services yet — `GameEngine` carries only `Stream` +
`Network`.** This phase proves the bones work: DI substrate, layer
system, scene wiring, IMGUI feedback, **plus the typed-bucket
`EventStream` dispatch surface + `IWireEventHandler<T>` that 4b's
handlers and bridges will hang off of**.

#### Current status — 🧪 awaiting Unity verification

Code landed in `3c1ed43`. The remote-execution environment used to
write the code has no Unity install, so the headless batch tests have
**not** been run against the commit. Verification is required locally
before 4a flips to ✅.

**Run from the project root:**

```
Unity.exe -batchmode -nographics -projectPath . -runTests \
  -testPlatform EditMode \
  -testResults Logs/test-results.xml \
  -logFile Logs/test.log
```

Read `Logs/test.log` first for compile errors (the new asmdefs may
expose typos or missing references). Read `Logs/test-results.xml` for
the new test outcomes.

**Tests to expect:**

- `Framework.Wire.Tests/EventStreamDispatchTests.cs` (new, 5 tests) —
  typed dispatch, cross-type isolation, untyped runtime-type routing,
  OnAppended+history compatibility, multi-handler fan-out.
- `Framework.Wire.Tests/RoundTripTests.cs` (existing, modified) —
  mechanical `server.Add → server.Emit` rename across 25 sites; all
  existing tests should still pass.
- `Framework.Bootstrap.Tests/EngineLayerTests.cs` (new, 3 tests) —
  `LifetimeScope.Create(EngineLayer.Install, …)` resolves `GameEngine`
  with non-null `Stream` + `Network`; `IEventEmitter`,
  `IHandlerRegistry`, and `EventStream` all resolve to the same
  singleton; `engine.Stream` matches the container's `EventStream`.

**Known risk areas** (most likely to fail compile/runtime, ordered by
likelihood):

1. **VContainer factory-lambda overload.** `Register<Network>(c => new Network(c.Resolve<ITransport>()), Lifetime.Singleton)`
   — confirmed against VContainer 1.17.0 source, but exact behavior
   under `LifetimeScope.Create` (root scope, no parent) untested here.
2. **Scoped-interface registration ordering.** `.AsSelf().As<IEventEmitter>().As<IHandlerRegistry>()`
   pattern — `AsSelf()` first to keep `EventStream` resolvable as
   itself. The Appflow samples use `.As<X>().AsSelf()` (interfaces
   first). Both should be equivalent per VContainer's
   `AddInterfaceType` semantics, but verify.
3. **`NullTransport` custom event accessor.** `add { _ = value; } remove { _ = value; }`
   to suppress CS0067 "event never used." Discard-pattern syntax (C#
   7+) should compile under Unity 6's Roslyn, but it's an unusual
   shape.
4. **`[RequireComponent(typeof(AppFlowRoot))]` on `BootstrapStatusUI`**
   — `AppFlowRoot` is abstract. Unity should match by inheritance
   (any subclass satisfies the requirement), but editor UX may emit a
   warning when adding `BootstrapStatusUI` to a fresh GameObject.
5. **Asmdef name mismatches.** `Scaffold.AppFlow` and `VContainer` are
   the references I used; if the actual assembly names from the
   packages differ, the asmdefs won't resolve. Verify in the Unity
   console.
6. **`Object.DestroyImmediate` in test teardown.** Works in EditMode
   (which is where tests run); cited by Appflow's own tests, so
   should be fine.

#### Deferred to 4b

Two items from the original 4a scope landed below the line. Both
depend on entity-event traffic to be meaningful and are cleaner to
build alongside the bridge layers:

- **`SandboxTransport`** (loopback `LoopbackTransport.Pair()` wiring
  between server + client `Network` instances). 4a uses
  `NullTransport` as a placeholder so `Network` is constructable.
- **`BootstrapSandbox.unity` scene file** (the `ServerRoot` + `ClientRoot`
  GameObjects + `SandboxTransport` scene object). 4a is gated by the
  headless test surface; the scene becomes the Play-mode demo for 4b's
  end-to-end "create card → mirror in sync" deliverable.

#### Implementation reference

Below: the shape the implementation actually took. Use this section as
the spec to read the 4a commit against.

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
  - `ServerAppFlowRoot : AppFlowRoot` — installs `EngineLayer` (4b adds
    `EntityLayer` + `ServerBridgeLayer`).
  - `ClientAppFlowRoot : AppFlowRoot` — installs `EngineLayer` (4b adds
    `EntityLayer` + `ClientBridgeLayer`).
- `EngineLayer : IScopeLayer` — registers `GameEngine` + `Network` +
  `EventStream` as singletons; `EventStream` additionally exposed as
  `IEventEmitter` and `IHandlerRegistry` (same instance) so scoped
  consumers resolve the narrowed surfaces. Side-agnostic; no per-side
  delta at the engine layer.
- `BootstrapStatusUI` — MonoBehaviour with `[Inject] GameEngine _engine`,
  IMGUI panel. Instantiated under each root.
- `SandboxTransport` — scene-level MonoBehaviour (or scene init script)
  that creates an in-memory transport and injects it into both `Network`
  instances at startup. **Deferred to 4b** — 4a registers a
  `NullTransport` so `Network` is constructable; the loopback pair
  lands alongside the bridge layers when entity events show up.

**`Framework.Wire` additions:**

- `IWireEventHandler<TEvent>` — `void Apply(TEvent ev)` +
  `void Unapply(TEvent ev)` (Unapply throws `NotImplementedException`
  until the rollback feature is designed).
- `IHandlerBucket` (internal) + `HandlerBucket<T>` (internal) — typed
  per-T bucket of handlers; no `object` casts at the dictionary level.
- `IEventEmitter` (public) — scoped narrowing of `EventStream` with
  `Emit<T>(T)` + `Emit(WireEvent)`.
- `IHandlerRegistry` (public) — scoped narrowing with
  `Register<T>(IWireEventHandler<T>)`.
- `EventStream` implements both interfaces over the typed-bucket table.
  Single impl, no NoOp variant. DI binds `IEventEmitter` and
  `IHandlerRegistry` to the `EventStream` singleton; full `EventStream`
  remains resolvable for end-user code that wants the whole surface.

**Sandbox scene:** `Assets/Framework/Bootstrap/Sandbox/BootstrapSandbox.unity`
with `ServerRoot` + `ClientRoot` GameObjects (each with its
`AppFlowRoot` subclass + a `BootstrapStatusUI`) + a `SandboxTransport`
scene object. **Scene file deferred to 4b**; 4a's headless test is the
gating artifact for the dispatch surface + DI wiring.

**Tests:**
- `Framework.Wire.Tests/EventStreamDispatchTests.cs` — typed-bucket
  dispatch smoke (typed `Emit<T>` and non-generic `Emit(WireEvent)`,
  cross-type isolation, OnAppended + history compatibility,
  multi-handler fan-out).
- `Framework.Bootstrap.Tests/EngineLayerTests.cs` — VContainer scope
  built via `LifetimeScope.Create(EngineLayer.Install, …)`; asserts
  `GameEngine` resolves with non-null `Stream` + `Network`,
  `IEventEmitter` / `IHandlerRegistry` / `EventStream` all resolve to
  the same singleton, and `engine.Stream` matches the container's
  `EventStream`.

**Closure:** Unity batch tests green. (Sandbox Play-mode verification
lands with the scene file in 4b.)

### 4b — `EntityStore` + `EntityService` worked example with per-type Apply handlers

**Touches:** `Framework.Entities`, `Framework.Tags`, `Framework.Wire`,
`Framework.Bootstrap`.

**`Framework.Entities` changes:**

- The step-3 single-class `EntityStore` splits into two:
  - `EntityStore` (kept) — the repository. Reads + raw writes; never
    emits. `Insert(EntityType, int)`, `Insert(int id, EntityType, int)`
    (idempotent for re-runs), `Get(int)`, `SetTag<T>(int id, int slot, T)`,
    `RemoveTag(int id, int slot)`. The all-entities enumerable too.
  - `EntityService` (new) — the command surface. Ctor-injects
    `EntityStore` + `IEventEmitter` (scoped — emit only). Public
    `Create(EntityType, int)` delegates to `_store.Insert` then
    `_events.Emit(EntityCreatedEvent)`. `Set<T>(int entityId, TagKey<T>, T)`
    reads prev via store, picks `TagSetEvent` vs `TagChangeEvent`,
    calls `_store.SetTag`, emits. `Get(int)` delegates to store.
- `Entity` becomes data-only. No emitter ref, no `EntityService` ref,
  no `Set<T>` public method. Public readers (`Get<T>(TagKey<T>)`) +
  internal raw setters (`SetSlotRaw<T>`, `RemoveSlotRaw`,
  `TryGetSlotRaw<T>`) that only `EntityStore` calls.
- Drop the wire-aware constructor variants from 3d.

**New `Framework.Entities/Runtime/Wire/` folder** holding per-type Apply
handlers (registered on **both** sides — they cannot emit, so re-running
on server is a structurally-safe no-op):

- `EntityCreatedHandler : IWireEventHandler<EntityCreatedEvent>` —
  ctor-injects `EntityStore` + `IHandlerRegistry`. `Apply` calls
  `_store.Insert(ev.EntityId, type, defId)` (idempotent).
- `TagSetHandler<T> : IWireEventHandler<TagSetEvent<T>>` — `Apply` calls
  `_store.SetTag(ev.EntityId, ev.Slot, ev.Value)`.
- `TagChangeHandler<T> : IWireEventHandler<TagChangeEvent<T>>` — same,
  applies `Next`.
- `TagRemovedHandler : IWireEventHandler<TagRemovedEvent>` — `Apply`
  calls `_store.RemoveTag(ev.EntityId, ev.Slot)`.

Each handler is `IAsyncInitializable`; its `InitializeAsync` calls
`_handlers.Register<TEvent>(this)`. None of them inject `EntityService`
*or* `IEventEmitter` — the type system rules out emit twice over.

**`Framework.Tags` changes:**

- Add `TagChangeEvent<T>` Pattern A wire event (carries `Prev` + `Next`)
  for symmetric Set/Change/Removed trio.

**`Framework.Wire` additions:**

- `WireForwarder` (server only) — single class implementing
  `IWireEventHandler<T>` for each replicated entity event type.
  Ctor-injects `IHandlerRegistry` + `Network`. Each `Apply` is a
  one-liner: `_network.Send(ev)`. Registers itself for each type during
  `InitializeAsync`. (Manual enumeration in step 4; source-gen later —
  Q6.)
- `NetworkInboundBridge` (client only) — ctor-injects `IEventEmitter` +
  `Network`. Subscribes to `Network.OnReceived` and forwards into
  `IEventEmitter.Emit(WireEvent)` (the non-generic type-routing
  overload). One line, no switch.

**`Framework.Bootstrap` changes:**

- `EntityLayer : IScopeLayer` — both sides. Registers `EntityStore` +
  `EntityService` as singletons; `RegisterEntryPoint<...>` for each
  Apply handler (`EntityCreatedHandler`, `TagSetHandler<int>`,
  `TagSetHandler<bool>`, `TagChangeHandler<int>`, `TagChangeHandler<bool>`,
  `TagRemovedHandler`).
- `ServerBridgeLayer : IScopeLayer` — server only.
  `RegisterEntryPoint<WireForwarder>`.
- `ClientBridgeLayer : IScopeLayer` — client only.
  `RegisterEntryPoint<NetworkInboundBridge>`.
- `ServerAppFlowRoot.GetInitialLayers()` returns `EngineLayer`,
  `EntityLayer`, `ServerBridgeLayer`. `ClientAppFlowRoot` returns
  `EngineLayer`, `EntityLayer`, `ClientBridgeLayer`. Layers stack
  additively.
- `GameEngine` gains an `Entities` property (= `EntityService`, ctor
  injection).
- `BootstrapStatusUI` extended — counter of entities + recent events
  list, side label.
- Sandbox button (on server root only): "Create card" → calls
  `serverEngine.Entities.Create(EntityTypes.Card, defId: 101)` then
  `serverEngine.Entities.Set(card.Id, SampleTags.Health, 30)`. Both
  IMGUI panels show the events flowing + the client entity reconstructed.

**Tests:**

- Existing `EntityWireRoundTripTests` / `WireRoundTripTests` (migrated 3e)
  simplify — no longer hand-construct events; the service emit path
  does it inline.
- New: symmetric round-trip test — server scope mutates via `Create` +
  `Set`, client scope mirrors via Apply handlers; assert both stores
  match after a sequence of Set / Change / Remove operations.
- New: type-safety smoke — confirm `EntityCreatedHandler` doesn't see
  `EntityService` (compile-time check via the ctor signature; documents
  the no-bounce invariant).
- New: server-side idempotency — emit `EntityCreatedEvent` for an id
  that already exists in the server's store; assert no exception,
  state unchanged.

**Defer (per pattern doc):**

- `Destroy` + `EntityDestroyedEvent` emission (Q5e).
- Indexed queries (`With` / `Where`) (Q5d).
- Source-gen handler registration (Q6).

**Closure:** sandbox scene + Unity batch tests green; pattern doc's
sketch matches shipped code.

### 4c — Pattern doc validation

Read [`definition-service-pattern.md`](definition-service-pattern.md)
against the 4a + 4b implementation. The locked sections (L1–L11) should
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
