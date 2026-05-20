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
> handler-based event command pattern**; specific services (Commands,
> Effects, Triggers, Request, State) get the pattern applied in step 5+.

## Phase breakdown

| Phase | Scope | Status | Commit |
|---|---|---|---|
| 4a | Scaffold — VContainer + Appflow in manifest; `Framework.Engine` (pure C#, empty `GameEngine` shell); `Framework.Bootstrap` (Unity-coupled, `FrameworkAppFlowRoot` + `EngineLayer` + sandbox scene); IMGUI says "Engine bootstrapped"; **`WireEventDispatcher` + `IWireEventHandler<T>` infrastructure in `Framework.Wire`** | ⏳ | — |
| 4b | `EntityService` worked example — single class both sides; `Entity` raises internal events; Emit handlers (server) + Apply handlers (client) registered via per-side layers; sandbox extends to "create card → mirror in sync" | ⏳ | — |
| 4c | Pattern doc validation — read `definition-service-pattern.md` against the shipped code; finalize convention summary; resolve any "felt forced" items | ⏳ | — |
| 4d | Closure — phasing rows → ✅ with commit hashes; definition doc `status: in-design → active`; cross-link from `wire-refoundation-roadmap.md` step 4 line | ⏳ | — |

After 4d: scaffold + `EntityService` operational with full handler-based
event round-trip. Bootstrap a scene, press Play, create an entity + tag
mutations on server, see client mirror sync via dispatched handlers. The
slice's `Engine/Pipeline/` stays as reference for step 5+.

## Phase scope

### 4a — Scaffold + dispatcher infrastructure

**No services beyond an empty `GameEngine` stub.** This phase proves
the bones work: DI substrate, layer system, scene wiring, IMGUI feedback,
**plus the `WireEventDispatcher` infrastructure that 4b's handlers will
hang off of**.

**Manifest changes:**

```json
"com.scaffold.appflow": "https://github.com/MgCohen/Scaffold.git?path=/Assets/Packages/com.scaffold.appflow#main",
"jp.hadashikick.vcontainer": "https://github.com/hadashiA/VContainer.git?path=VContainer/Assets/VContainer#1.17.0"
```

**New asmdef: `Framework.Engine`** (`Assets/Framework/Engine/`)

- Pure C#; no Unity dep.
- `GameEngine` — empty marker class (per Q3 lean). May gain
  diagnostics fields (frame counter, ready state) if useful for IMGUI.
- No `GameEngineBuilder` in step 4 (per Q4 lean — defer until a non-Unity
  consumer needs it).
- Test asmdef ships too (may be empty in 4a).

**New asmdef: `Framework.Bootstrap`** (`Assets/Framework/Bootstrap/`)

- Unity-coupled; depends on VContainer, Scaffold.Appflow,
  `Framework.Engine`, `Framework.Wire`.
- `FrameworkAppFlowRoot : AppFlowRoot` — MonoBehaviour, root composition.
  Overrides `GetInitialLayers()` returning `EngineLayer` (and later
  `Server*Layer` / `Client*Layer` in 4b).
- `EngineLayer : IScopeLayer` — registers `GameEngine` + `WireEventDispatcher`
  as singletons.
- `BootstrapStatusUI` — MonoBehaviour with `[Inject] GameEngine _engine`,
  IMGUI panel.

**`Framework.Wire` additions:**

- `IWireEventHandler<TEvent>` interface — `void Handle(TEvent ev)`.
- `WireEventDispatcher` — subscribes to `EventStream.OnAppended`;
  registers typed handlers; routes by runtime type via
  `Dictionary<Type, List<Action<WireEvent>>>`.
- (Possibly) `WireHandlerInstaller<TEvent> : IAsyncInitializable` — a
  bridge that registers a resolved handler with the dispatcher at scope
  init. Details settle here.

**Sandbox scene:** `Assets/Framework/Bootstrap/Sandbox/BootstrapSandbox.unity`
with the `FrameworkAppFlowRoot` GameObject + a child `BootstrapStatusUI`
showing **"Engine bootstrapped. 0 entities. 0 services registered."**

**Tests:** Edit-mode test that constructs a VContainer scope via a layer,
resolves `GameEngine` + `WireEventDispatcher`, asserts non-null. No Unity
scene dep.

**Closure:** sandbox scene runs in Play mode, IMGUI shows the
"bootstrapped" message, Unity batch tests green.

### 4b — `EntityService` worked example with handler pattern

**Touches:** `Framework.Entities`, `Framework.Tags`, `Framework.Bootstrap`.

**`Framework.Entities` changes:**

- Rename `EntityStore` → `EntityService`. Drop the wire-aware
  constructor variants from 3d — service is now pure C#, no wire ref.
- `EntityService` exposes one event: `public event Action<Entity>? OnCreated`.
- `Entity` gains three internal events:
  - `internal event Action<TagKey, object>? OnTagSet`
  - `internal event Action<TagKey, object, object>? OnTagChanged`
  - `internal event Action<TagKey, object>? OnTagRemoved`
  - Raised inside `Entity.Set` / `Entity.Remove` as appropriate.
- `Entity.Set` distinguishes first-write (raises `OnTagSet`) from
  subsequent write (raises `OnTagChanged` with prev + next) — like slice.
- No back-reference from `Entity` to `EntityService`. The service
  subscribes to each Entity's events implicitly via per-side Emit
  handlers (which subscribe to `EntityService.OnCreated`, then attach to
  the Entity's events).

**New `Framework.Entities/Runtime/Wire/` folder** holding the handlers:

- `EmitEntityCreated` (server) — `entities.OnCreated` → `stream.Add(EntityCreatedEvent)`.
- `EmitTagSet<T>` (server, generic) — attaches to each Entity's `OnTagSet`; emits `TagSetEvent<T>` if value matches T.
- `EmitTagChange<T>` (server, generic) — `OnTagChanged`; emits `TagChangeEvent<T>`.
- `EmitTagRemoved` (server) — `OnTagRemoved`; emits `TagRemovedEvent`.
- `ApplyEntityCreated : IWireEventHandler<EntityCreatedEvent>` (client)
  — calls `entities.Create(presetId, type, defId)`.
- `ApplyTagSet<T> : IWireEventHandler<TagSetEvent<T>>` (client, generic).
- `ApplyTagChange<T> : IWireEventHandler<TagChangeEvent<T>>` (client, generic).
- `ApplyTagRemoved : IWireEventHandler<TagRemovedEvent>` (client).

**`Framework.Tags` changes:**

- Add `TagChangeEvent<T>` Pattern A wire event (carries `Prev` + `Next`)
  for symmetric Set/Change/Removed trio.

**`Framework.Bootstrap` changes:**

- `ServerEntityLayer : IScopeLayer` — registers `EntityService` +
  `WireEventDispatcher` + all `Emit*` handlers via `RegisterEntryPoint`.
- `ClientEntityLayer : IScopeLayer` — registers `EntityService` +
  `WireEventDispatcher` + all `Apply*` handlers (each as
  `IWireEventHandler<TEvent>` + the dispatcher-installer bridge).
- `FrameworkAppFlowRoot.GetInitialLayers()` adds both (or two child
  scopes — per Q2 lock).
- `BootstrapStatusUI` extended — counter of entities + recent events list,
  side label.
- Sandbox button: "Create card on server" → calls
  `serverEntities.Create(EntityTypes.Card, defId: 101).Set(SampleTags.Health, 30)`.
  IMGUI shows the events flowing + the client entity reconstructed.

**Tests:**

- Existing `EntityWireRoundTripTests` / `WireRoundTripTests` (migrated 3e)
  simplify substantially — no longer hand-construct events; `EmitX`
  handlers do it.
- New: symmetric round-trip test — server scope mutates, client scope
  mirrors; assert both bags match after a sequence of Set / Change /
  Remove operations.

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
