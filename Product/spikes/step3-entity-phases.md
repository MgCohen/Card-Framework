---
type: spike
status: in-progress
created: 2026-05-19
references:
  - Product/spikes/wire-refoundation-roadmap.md
  - Product/spikes/definition-entity-setup.md
  - Product/spikes/entity-identity-model.md
sandbox: Assets/Framework/Entities/
tags: [#spike, #entities, #phasing, #session-handoff]
---

# Step 3 — Entity layer phasing

> Session-handoff record. The 7-step refoundation roadmap names step 3 as
> "Entities + definitions + catalogs"; the locked design lives in
> [`definition-entity-setup.md`](definition-entity-setup.md). This doc layers
> implementation phasing on top — what's shipped, what's pending, what's deferred.

## Phase breakdown

| Phase | Scope | Status | Commit |
|---|---|---|---|
| 3a | `EntityType` + `EntityTypeRegistry` + engine-reserved `EntityTypes` | ✅ | `b0407f5` |
| 3b | `Entity` + `EntityStore` + Pattern B extensions | ✅ | `4ac0be2` |
| 3c | `IEntityDefinition` + `Catalog<TDef>` + `EntityStore.From<TDef>` | ✅ | `f197df1` |
| 3d | Wire integration — `EntityCreatedEvent` gains `TypeSlot` + `DefinitionId`; `long → int` | ✅ | `440300c` |
| 3e | Cleanup — migrate bag tests to real `Entity`, delete `TagBagProbe`, delete spike | ⏳ | — |

After 3d: 397/397 EditMode + 1/1 PlayMode pass.

## What landed per phase

### 3a — Identity primitives

`Runtime/EntityType.cs` (`readonly struct`, `Slot` + `Name`, equality on Slot only),
`Runtime/EntityTypes.cs` (engine-reserved: 0 Unknown, 2 Player, 3 Zone, 4 Card, 5
Modifier — slot 1 reserved for future Game-as-entity),
`Runtime/Registry/EntityTypeRegistry.cs` (process-global; `Register` / `BySlot` /
`TryGetBySlot`). No external asmdef refs.

**Locked:** struct (not `record struct`); no operator overloads; engine-reserved
slots fire on first touch of `EntityTypes`.

### 3b — Entity + EntityStore + Pattern B

`Runtime/Entity.cs` (class, internal ctor, `Id` / `Type` / `DefinitionId` + sparse
tag bag with Pattern A `Set/Get/Has/Remove<T>` on `TagKey<T>`),
`Runtime/EntityStore.cs` (id allocation from 1; `Create` / `ById` / `TryGetById`),
`Runtime/EntityExtensions.cs` (Pattern B sugar via extensions — generic
`Set<TTag,T>` / `Get<TTag,T>` / `Has<TTag>` / `Remove<TTag>`). Tests assembly
declares local sample `[TagSlot] DeathrattleTestTag` + named-field `Tag` container.

**Locked:** `Entity` is reference type (not handle/struct); ID starts at 1 (0 = no
entity); Pattern B is extension methods with two type args at call site (no
per-value-type overloads); no emission, no `Destroy`, no `Slots` enumerator, no
`Equals` override.

### 3c — Definitions + catalogs

`Runtime/IEntityDefinition.cs` (minimum interface — `Id` + `Type`),
`Runtime/Catalog.cs` (`Catalog<TDef> where TDef : IEntityDefinition` — open
constraint; `Add` / `GetById` / `TryGetById` / `GetId`; throws on duplicate Id
and on Id==0),
`Runtime/EntityStoreExtensions.cs` (L2 `From<TDef>(this EntityStore, TDef)`).
Tests use record `CardTestDef` via Scaffold.Records (IsExternalInit polyfill).

**Locked:** interface is the two fields only (no `Name`, no typed `DefinitionId`
wrapper); `Catalog<TDef>` constraint stays open (struct or class defs); L2 lives
in its own static class per "extension class per target type"; `Packages/manifest.json`
gains `com.scaffold.records`.

### 3d — Wire integration

`Framework.Wire/Runtime/Events/EntityCreatedEvent.cs`: `long EntityId → int` +
adds `int TypeSlot` + `int DefinitionId`.
`Framework.Wire/Runtime/Events/EntityDestroyedEvent.cs`: `long EntityId → int`.
`Framework.Entities/Runtime/EntityStore.cs`: added `Create(int id, EntityType, int)`
overload for receiver-side reconstruction; `_nextId` advances past presets.
`Framework.Entities/Runtime/EntityType.cs`: ctor flipped to `internal` (closure
rule — instances come from `EntityTypeRegistry.Register`); `AssemblyInfo.cs`
adds `InternalsVisibleTo` for the test assembly.
`Framework.Entities/Runtime/Catalog.cs`: `Add` throws on `Id==0`.
New `EntityWireRoundTripTests.cs`: end-to-end through `Wire.Connect`.

**Locked:** full `long → int` alignment + new fields, single wire-format break;
no auto-emission from `EntityStore.Create`; no `ForSlotOrUnknown` (tests use
strict `BySlot`); no `Catalog` wiring through `IPackingHandler`; test fixtures
(`CardPlayedEvent`, `EntityLabeledEvent`, `EntityMovedEvent`) keep `long
EntityId` — they're fixtures, not framework events.

## Phase 3e — Cleanup (pending)

Goal: drop the test stub and spike folder now that real `Entity` carries their
workload.

To migrate / delete:

1. **`Framework.Tags.Tests/TagBagProbe.cs`** — delete. Bag-mechanic + wire-roundtrip
   tests get rewritten against real `Entity`.
2. **`Framework.Tags.Tests/SetGetTests.cs`** — move to `Framework.Entities.Tests`,
   swap `TagBagProbe` for `Entity`.
3. **`Framework.Tags.Tests/WireRoundTripTests.cs`** — same: move, swap. (Verifies
   the wire round-trip of `TagSetEvent<T>` / `TagRemovedEvent` through a real
   entity's `Set` / `Remove` mutations.)
4. **`Assets/Spike/ECS/EntitySetup/`** — entire spike folder (runtime + tests +
   Unity sandbox bootstrap). Delete. git history preserves.
5. **Unity sandbox** (`EntitySetupBootstrap.cs` + `Editor/SetupSceneBuilder.cs` in
   the spike) — evaluate separately. Lean: delete with the rest; sandbox scene
   demos are content-authoring concerns, not framework.

**Sister cleanup (optional separate commit):**

- **`Assets/Spike/ECS/TypedPayloads/`** — superseded by `Framework.Wire` since
   Phase 5 of the wire roadmap. Same delete pattern. Skip if scope-creep, do
   alongside if convenient.

After 3e:

- `Framework.Tags.Tests` shrinks to `RegistrationTests` only (pure registry).
- `Framework.Entities.Tests` owns bag-mechanic + wire-roundtrip end-to-end.
- Spike folder for EntitySetup gone.

**Important:** when moving SetGetTests / WireRoundTripTests into the entities
test assembly, the local sample tags (`SampleDeathrattle` / `SamplePoison` /
`SampleTags.Health` / `SampleTags.IsCard` / etc.) come with them — they're tag
slot declarations that the bag tests assert against. The test container `Tag.*`
named-field declarations move too.

## Deferred items (logged so future phases don't re-debate)

| Item | Lights up when |
|---|---|
| Auto-emission from `EntityStore.Create` (and `Set` / `Remove`) | Service-pattern step (4+) — hook/scope shape needed. |
| `EntityTypeRegistry.ForSlotOrUnknown` | Cross-version replay / mirror needs graceful-degrade. |
| `Catalog<TDef>` wiring through `IPackingHandler` | Ref-typed wire fields with definitions land in framework (not just test fixtures). |
| `Entity.Destroy` / `IsAlive` / generation | `entity-identity-model.md` (still draft) resolves SabberStone-vs-ECS-handle. |
| `Entity.Slots` read-only enumerator | Indexed-query store (step 6+) needs iteration. |
| Source-gen for Pattern B `Tag.X` named-field declarations | When tag count makes manual one-liners unviable. |
| Kill `EntityType` via source-gen-emitted per-definition catalogs | Identity-allocation revisit during/after step 4. |
| Test fixtures (`CardPlayedEvent` etc.) `long → int` alignment | When all-int rule wants enforcement past framework events. |

## When this lands

Step 3 graduates from "in-progress" to "done" when 3e ships green. After that,
this doc + `definition-entity-setup.md` can fold into a Model under
`Product/features/entities/` or graduate to an ADR. Until then this is the
session-handoff record — pick up at 3e from here.
