---
type: feature
status: settled
references: [root]
tags: [#entities]
---

# entities — feature

## What it is

The framework's foundational entity primitive. Every runtime "thing" — cards, players, attachments, equipment, modifiers, zones — is an *instance* of a *definition*. The instance has a typed `Ref<TInstance>` for lookup/reference, plus relationships and modifiers; its effective values are calculated from definition defaults, instance base overrides, local modifiers, attached instances, and external effects. The framework is data-driven: definitions are authored, instances are runtime, and per-instance state lives in canonical Store slices.

```
                    Definition
                    (authored data;
                     DefinitionWithSlices)
                         │
                         ▼
                  Instance (typed Ref<TInstance>)
                         │
              ┌──────────┼─────────────────┐
              │          │                 │
        EntityState   Attached         External effects
        (Store slice) instances        (zones, auras)
        - BaseValues     │
        - ModifierStacks │
              │          ▼
              ▼      Composition
        Effective    handler
        values  ◄────┘
        (definition default → base override → modifier fold)
```

## Vocabulary

**Feature-owned terms only.** Cross-feature terms used by this feature — `Reference`, `Ref<T>`, `IActor`, `ActorId`, `IState`, `Store`, `BaseState`, `IPayloadReference` — live in their owning features or `Product/ledgers/GLOSSARY.md`; link to their canonical home, don't restate.

| Term | Meaning |
|---|---|
| `Definition` | Shared authored data describing what an entity *is* before runtime state. Stable definition ID. |
| `Instance` | Runtime entity created from a definition. Has a typed `Ref<TInstance>`, may carry per-instance variables, modifiers, relationships. |
| `Variable` / `VariableValue` | Key + typed value pair. Variables are identified by `Variable` (`Key` + `PayloadTypeId`); values use typed wrappers (`IntVariableValue`, `FloatVariableValue`, etc.). |
| `VariableModifier` | Layered modifier that adjusts an effective value. Subclasses (`IntAddModifier`, `BoolOverrideModifier`, …) implement `Apply(current)`. Ordered by `Order` (ascending fold; FIFO ties). |
| `EntityState` | Canonical per-entity state slice. `BaseValues` (override-only map) + `ModifierStacks` (keyed by `Variable`). Keyed by the entity's `Ref<T>`. |
| `InstanceWithSlices<TDef>` | Framework parent class (extends `Scaffold.EntityInstance<TDef>`, implements `ISliceProvider`). Concrete subclasses (`Card`, `Zone`, `Player`) carry typed `Ref<TSubclass> SelfRef` and inherit. Re-created from the catalog after `LoadSnapshot`; not state itself. |
| `DefinitionWithSlices` | Framework parent class for `Scaffold.EntityDefinition` that adds `ISliceProvider`. Default empty `GetInitialSlices()`; concrete definitions (`CardDefinition`, etc.) override to declare per-instance slices. |
| `IEntityVariableStorage` | The mutation/read surface attached to an instance handle. `StoreVariableStorage` is store-backed; `LocalVariableStorage` is overlay-backed for previews/hypotheticals. |
| `IModifierFactory` | Engine-owned factory producing fully-populated `ActiveModifier`s with deterministic `ModifierId`s. The only sanctioned producer of pre-populated modifier entries in framework code. |
| `ModifierId` | Deterministic id for a modifier. Generated from an engine-owned counter snapshotted with engine state. |
| `ModifierSource` | `(Reference, int)` — the attachment entity's `Ref<T>` plus an opt-in `Tag`. Source attribution for cleanup-on-attachment-death sweeps. |
| `store.Catalog` | Sub-property on `Store` providing register/resolve for arbitrary objects keyed by `Ref<T>`. In-process side-state on the Store; not snapshotted. |

## Architecture

The feature has **five Models**, organized per concept axis along the entity bridge:

- **[`01-definition.md`](features/entities/models/01-definition.md)** — the authored half: `IEntityDefinition`, `EntityDefinition`, `DefinitionWithSlices`, the canonical `ISliceProvider` interface declaration. Q1 / Q2 / Q5 provenance.
- **[`02-instance.md`](features/entities/models/02-instance.md)** — the runtime half: `InstanceWithSlices<TDef>` handle, `Ref<T>` identity, zone-movement default, lifecycle via `store.Catalog` + `StoreEntities.Spawn`, slice-level subscriptions, read consistency. Q3 / Q4 provenance.
- **[`03-variable.md`](features/entities/models/03-variable.md)** — the key-value layer: `Variable` + `VariableValue` authoring, `EntityState.BaseValues` slice, read/mutation API for variables, per-variable subscriptions, payload disciplines. Q2 reference.
- **[`04-modifier.md`](features/entities/models/04-modifier.md)** — the layered-fold layer: `VariableModifier<T>`, `ActiveModifier`, `ModifierSource`, `IModifierFactory`, `EntityState.ModifierStacks` slice, modifier mutation API, effective-value semantics, composition handlers.
- **[`05-attachment.md`](features/entities/models/05-attachment.md)** — the composition layer: attachments/equipment, tokens, transformation, ongoing effects as attachment entities, automatic cleanup. Q6 provenance. Worked examples (equip/unequip + apply-aura).

Three flows tie the Models together: **spawn → base → modifier** (handle constructed empty, definition defaults flow through fall-back, base overrides via `SetBase`, modifier layers fold), **identity preservation across zone moves** (`Ref<T>` stable; per-game opt-in for MTG-style replacement), and **snapshot / restore** (`EntityState` slices restore; handles re-created via `StoreEntities.Spawn` from definition lookup; subscribers must re-bind through the catalog).

## Composition

| Model | Role |
|---|---|
| [`01-definition.md`](features/entities/models/01-definition.md) | Authored half: `IEntityDefinition` / `EntityDefinition` / `DefinitionWithSlices`; canonical home for `ISliceProvider`; definition-level conventions (stable IDs, declared variables, default values) |
| [`02-instance.md`](features/entities/models/02-instance.md) | Runtime half: `InstanceWithSlices<TDef>` handle; `Ref<T>` identity; zone movement; `Definition` access discipline; lifecycle (spawn / resolve / destroy / snapshot); slice-level subscriptions; read consistency |
| [`03-variable.md`](features/entities/models/03-variable.md) | Key-value layer: `Variable` + `VariableValue`; `EntityState.BaseValues` slice; read/mutation API for variables; per-variable subscriptions; `AddEntityVariablePayload` + `SetBaseValuePayload` disciplines |
| [`04-modifier.md`](features/entities/models/04-modifier.md) | Layered-fold layer: `VariableModifier<T>` + `ActiveModifier`; `ModifierSource`; `IModifierFactory`; `EntityState.ModifierStacks` slice; modifier mutation API; effective-value semantics; composition handlers |
| [`05-attachment.md`](features/entities/models/05-attachment.md) | Composition layer: attachments/equipment as separate instances; tokens; transformation; ongoing effects as attachment entities; cleanup machinery; worked examples (equip/unequip + apply-aura) |

## Invariants

Cross-Model rules that hold across both Models and shape how downstream features must use entities.

- **Store is canonical; entities is the API.** Per-entity variables live as `EntityState` slices in the Store, keyed by the entity's `Ref<T>`. Reads and writes go through the storage adapter on the handle (or raw payloads for cases the adapter doesn't expose). No two-way sync, no duplicate state.
- **Definition immutability on the handle.** `Definition` lives on the handle (as `protected`), not in `EntityState`. Transformation creates a new handle for the same `Ref<T>`. There is no state-level `SetDefinitionPayload`.
- **Modifier source = attachment entity's `Ref<T>` + opt-in `Tag`.** Cleanup-on-source-death is a uniform `RemoveModifiersBySourcePayload(target, new ModifierSource(attachment.SelfRef, 0))` per affected target (or `store.RemoveModifiersFromSource(source)` for the bulk-across-all-entities sweep).
- **Deterministic `ModifierId` via factory only.** Ad-hoc `ModifierId` allocation via `Guid.NewGuid()` is forbidden in framework / handler code. The `IModifierFactory` allocates ids from an engine-owned counter snapshotted with engine state, so replay and multiplayer determinism hold.
- **Read consistency is eager and synchronous.** Read-after-write within a single handler is fresh; subscriber notifications are serialized on a single thread.

## Boundaries

**Owns:** the definition / instance / variable / modifier conceptual model; `EntityState` slice and the seven bridge mutation payloads; `InstanceWithSlices<TDef>` and `DefinitionWithSlices` framework parent classes; `IEntityVariableStorage` read/mutation surface; subscription channels; `IModifierFactory` contract; effective-value resolution semantics (definition default → base override → modifier fold).

**Does not own:**
- Card-specific shape (`Card`, `CardRef`, `CardMapState`) → `cards/`
- Zone membership and zone-shape (`Zone`, `ZoneContentsState`) → `zones/`
- Player-specific shape (`Player`, `PlayerDefinition`, `PerPlayerZones`) → `players/`
- Action pipeline, command handlers → `actions/`
- Effect orchestration, attachment lifecycle (Cleanup hook fires from `effects/`) → `effects/`
- Trigger fold semantics → `triggers/`
- Engine-side service composition (`store.Catalog` mechanism, factory) → `engine/`

## Dependencies

*Mirrors the front-matter `references:` array in human-readable form (per §3).*

- **Upstream:** none — entities are foundational. Sits at the bottom of the feature dependency graph.
- **Downstream:** `cards/` (`Card` extends `InstanceWithSlices<CardDefinition>`), `zones/` (`Zone` extends `InstanceWithSlices<ZoneDefinition>`), `players/` (`Player` extends `InstanceWithSlices<PlayerDefinition>`), `actions/` (handlers consume `store.Catalog`), `effects/` (effects consume `IEffectScope.Store.Catalog`), `triggers/` (modifier-bearing entities subscribe to triggers), `engine/` (engine constructs and exposes the catalog), `presentation/` (consumes entity state via slice subscriptions).

## Open

See `ledgers/OPEN-ITEMS.md` — entity-related questions:

- `oq-entity-destruction` — what "destroyed" means for the Store; when the framework Cleanup hook fires; what happens to in-flight dispatches referencing the destroyed entity.
- `oq-engine-builder` — engine-side service composition (where `IModifierFactory`, the deterministic ref/id generators, and per-kind registries are constructed, registered, exposed).
- `bl-authoring-feature` — authoring layer on top of `EntityDefinition` (custom-game-definition serialization, validation, asset migration, definition versioning).
- Multi-store entities — currently `StoreEntities.Spawn` is parameterized on a single `Store`. Multi-store games (player-private stores, instance-shard stores) would need catalog partitioning. Out of scope until a multi-store game arrives.
