---
type: feature
status: settled
references: root
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

The feature has two Models. The conceptual model lives in `01-definition-instance-model.md` (definitions, instances, variables, modifiers, attachments, the bridge boundary, mutation-payload conventions); the runtime API surface lives in `02-runtime-surface.md` (`InstanceWithSlices<TDef>` handle, read API, subscriptions, mutation surface, `Modifiers` factory, lifecycle via `store.Catalog` + `StoreEntities.Spawn`). Three flows tie the Models together: **spawn → base → modifier** (handle constructed empty, definition defaults flow through fall-back, base overrides via `SetBase`, modifier layers fold), **identity preservation across zone moves** (`Ref<T>` stable; per-game opt-in for MTG-style replacement), and **snapshot / restore** (`EntityState` slices restore; handles re-created via `StoreEntities.Spawn` from definition lookup; subscribers must re-bind through the catalog).

## Composition

| Model | Role |
|---|---|
| [`models/01-definition-instance-model.md`](models/01-definition-instance-model.md) | Conceptual model: definitions, instances, variables, modifiers, attachments, composition; bridge boundary (Store + entities); mutation payload conventions |
| [`models/02-runtime-surface.md`](models/02-runtime-surface.md) | Concrete API: `InstanceWithSlices<TDef>` handle, `IEntityVariableStorage` read/mutation surface, subscriptions, `Modifiers` factory, lifecycle (`store.Catalog` + `StoreEntities.Spawn`), authoring reference, worked examples |

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
