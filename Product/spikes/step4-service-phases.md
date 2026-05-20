---
type: spike
status: in-design
created: 2026-05-20
references:
  - Product/spikes/wire-refoundation-roadmap.md
  - Product/spikes/definition-service-pattern.md
  - Product/spikes/step3-entity-phases.md
sandbox: Assets/Framework/Engine/, Assets/Framework/Bootstrap/
tags: [#spike, #services, #phasing, #session-handoff]
---

# Step 4 — Service pattern phasing

> Session-handoff record. The 7-step refoundation roadmap names step 4 as
> "Service pattern"; the locked design lives in
> [`definition-service-pattern.md`](definition-service-pattern.md). Step 4
> ships **the scaffold + one worked example (`EntityService`)**; specific
> services (Commands, Effects, Triggers, Request, State) get the pattern
> applied in step 5+.

## Phase breakdown

| Phase | Scope | Status | Commit |
|---|---|---|---|
| 4a | Scaffold — VContainer + Appflow in manifest; `Framework.Engine` (pure C#, empty `GameEngine` shell); `Framework.Bootstrap` (Unity-coupled, `FrameworkAppFlowRoot` + `EngineLayer` + sandbox scene); IMGUI says "Engine bootstrapped" | ⏳ | — |
| 4b | `EntityService` worked example — refactor `Framework.Entities/EntityStore` → `EntityService`; `EntityServiceLayer` registers it; auto-emission lit up; symmetric server/client; sandbox demo extends to "1 entity, 3 tag events emitted, mirror in sync" | ⏳ | — |
| 4c | Pattern doc validation — read `definition-service-pattern.md` against the shipped code; adjust convention summary; resolve any remaining Q leans against reality | ⏳ | — |
| 4d | Closure — phasing rows → ✅ with commit hashes; definition doc `status: active`; cross-link from `wire-refoundation-roadmap.md` step 4 line | ⏳ | — |

After 4d: scaffold + `EntityService` operational. Bootstrap a scene, press
Play, see entity events round-trip server → client through the wire. The
slice's `Engine/Pipeline/` stays as reference for step 5+.

## Phase scope

### 4a — Scaffold

**No services beyond an empty `GameEngine` stub.** This phase proves the
bones work: DI substrate, layer system, scene wiring, IMGUI feedback.

**Manifest changes:**

```json
"com.scaffold.appflow": "https://github.com/MgCohen/Scaffold.git?path=/Assets/Packages/com.scaffold.appflow#main",
"jp.hadashikick.vcontainer": "https://github.com/hadashiA/VContainer.git?path=VContainer/Assets/VContainer#1.17.0"
```

**New asmdef: `Framework.Engine`** (`Assets/Framework/Engine/`)

- Pure C#; no Unity dep.
- `GameEngine` — empty marker class for now (per Q3 lean). May gain
  diagnostics fields (frame counter, ready state) if useful for the 4a
  IMGUI panel.
- No `GameEngineBuilder` in step 4 (per Q4 lean — defer until a non-Unity
  consumer needs it).
- Test asmdef ships too, even if empty in 4a.

**New asmdef: `Framework.Bootstrap`** (`Assets/Framework/Bootstrap/`)

- Unity-coupled; depends on `VContainer`, `Scaffold.Appflow`, `Framework.Engine`.
- `FrameworkAppFlowRoot : AppFlowRoot` — MonoBehaviour, root composition.
  Overrides `GetInitialLayers()` returning `EngineLayer`.
- `EngineLayer : IScopeLayer` — registers `GameEngine` as singleton in the
  container.
- `BootstrapStatusUI` — MonoBehaviour with `[Inject] GameEngine _engine`,
  IMGUI panel reading state.

**Sandbox scene:** `Assets/Framework/Bootstrap/Sandbox/BootstrapSandbox.unity`
with the `FrameworkAppFlowRoot` GameObject + a child `BootstrapStatusUI`
showing **"Engine bootstrapped. 0 entities. 0 services registered."**
(Service count is informational; resolves via `IObjectResolver` introspection
or a hand-counted display field.)

**Wire endpoint registration (per Q1 lock during 4a):** the layer
registers `IServerWireSender` + `IClientWireReceiver` (or whatever
interfaces win Q1) so 4b can constructor-inject them into `EntityService`.
For 4a's empty-shell state, no service consumes them yet, but the
registration plumbing is in place.

**Tests:** Edit-mode test that constructs a VContainer scope via a layer,
resolves `GameEngine`, asserts non-null. No Unity scene dep; the layer
machinery is testable headless.

**Commit pattern:** Single commit `feat(engine,bootstrap): Phase 4a —
scaffold with VContainer + Appflow + sandbox scene` or split into two
(`feat(deps): import VContainer + Scaffold.Appflow` + `feat(engine):
Phase 4a scaffold`). TBD by diff size.

**Closure:** sandbox scene runs in Play mode, IMGUI shows the
"bootstrapped" message, Unity batch tests green.

### 4b — EntityService worked example

**Touches:** `Framework.Entities`, `Framework.Tags`, `Framework.Bootstrap`.
Possibly minor `Framework.Wire` if Q1's chosen interfaces need helpers.

**`Framework.Entities` changes:**

- Rename `EntityStore` → `EntityService`. Update `EntityWireRoundTripTests`
  + the migrated 3e tests.
- `EntityService` ctor takes the wire capabilities (interface names per
  Q1 lock). Emission moves inside the service: `Create` → emits
  `EntityCreatedEvent`; tag mutations route through subscribed Entity
  events → emit `TagSetEvent<T>` / `TagChangeEvent<T>` / `TagRemovedEvent`.
- `Entity` gains internal `TagSet` / `TagChanged` / `TagRemoved` events
  (slot-keyed, untyped) for the service to subscribe on `Create` (per
  Q5b lock). Public mutation surface (`Set` / `Remove`) stays unchanged.
- Symmetric server/client (per L6): client-side `EntityService`
  constructed with a receiving capability subscribes to incoming events
  and applies them to its local bag.

**`Framework.Tags` changes:**

- Add `TagChangeEvent<T>` Pattern A wire event (carries `Prev` + `Next`)
  for symmetric Set/Change/Removed trio (per Q5c lock). Mirrors slice's
  `TagSetEvent` + `TagChangeEvent` split (slice has both already).

**`Framework.Bootstrap` changes:**

- New `EntityServiceLayer : IScopeLayer` registering `EntityService`.
  Added to `FrameworkAppFlowRoot.GetInitialLayers()` after `EngineLayer`.
- `BootstrapStatusUI` extended: counter of entities created, tag-event
  log scroll, server vs client side panel.
- Sandbox scene gets a button: "Create card on server" → calls
  `serverEntities.Create(EntityTypes.Card, definitionId: 101).Set(SampleTags.Health, 30)`.
  IMGUI shows the events flowing + the client entity reconstructed.

**Tests:**

- `EntityWireRoundTripTests` / `WireRoundTripTests` (migrated 3e): no
  longer hand-construct `TagSetEvent` after each mutation — `EntityService`
  emits automatically. Tests simplify substantially.
- New: symmetric round-trip — server `EntityService` mutates, client
  `EntityService` mirrors; assert both bags match after a sequence of
  Set / Change / Remove operations.

**Defer (stay deferred per pattern doc):**

- `Destroy` + `EntityDestroyedEvent` emission (Q5e).
- Indexed queries (`With` / `Where`) (Q5d).
- Asymmetric mirror with Unapply (L6 — deferred).

**Closure:** sandbox scene + Unity batch tests green; pattern doc's
sketch validates against the shipped code.

### 4c — Pattern doc validation

Read [`definition-service-pattern.md`](definition-service-pattern.md)
against the 4a + 4b implementation. The locked sections (L1–L7) ought
to hold; the convention-summary draft block at the end of the doc gets
finalized.

For anything that felt forced during 4a or 4b — call it out. Adjust
either:

- Pattern doc (the convention was wrong → relax it), or
- Implementation (the pattern is right → fix the code).

Tests stay green. Note shifts in the commit message.

**Closure:** pattern doc accurately describes the shipped code; no
outstanding "felt forced" items.

### 4d — Step 4 closure

- Phasing rows → ✅ with commit hashes.
- `definition-service-pattern.md` banner: `status: in-design → active`.
  (Or `done` once step 5 starts.)
- Cross-link from `wire-refoundation-roadmap.md` step 4 line to both
  spike docs.
- Brief handoff note for step 5+: "scaffold is in place; pattern is
  proven via `EntityService`; apply the pattern to Commands / Effects /
  Triggers / Request / State as their step lands."

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
| Asymmetric `MirrorEntityService` (scrub / Unapply) | When replay forces it |
| `GameEngineBuilder` (non-Unity hosts) | When a non-Unity consumer appears |
| View-model layer | Step 7 |

## When this lands

Step 4 graduates from "in-progress" to "done" when 4d ships green. The
slice's `Engine/Pipeline/` folder stays intact as reference (per L5).
Step 5 (existing services reviewed — applying the pattern to Commands /
Effects / Triggers / Request / State) picks up from here.
