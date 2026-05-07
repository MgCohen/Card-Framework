---
type: feature
status: settled
references: root
tags: [#entities]
---

# entities — feature

## What it is

The framework's foundational entity primitive. Every runtime "thing" — cards, players, attachments, equipment, modifiers, zones — is an *instance* of a *definition*. The instance has an `InstanceId` for lookup/reference, plus relationships and modifiers; its effective values are calculated from definition defaults, instance base overrides, local modifiers, attached instances, and external effects. The framework is data-driven: definitions are authored, instances are runtime, and per-instance state lives in canonical Store slices.

```
                    Definition
                    (authored data)
                         │
                         ▼
                  Instance (InstanceId)
                         │
              ┌──────────┼─────────────────┐
              │          │                 │
        EntityVariable  Attached      External effects
        State (Store)   instances     (zones, auras)
        - BaseValues     │
        - ModifierStacks │
              │          ▼
              ▼      Composition
        Effective    handler
        values  ◄────┘
        (definition default → base override → modifier fold)
```

## Vocabulary

**Feature-owned terms only.** Cross-feature terms used by this feature — `Actor`, `ActorId`, `Identifier<T>`, `IReference`, `IState`, `Store` — live in their owning features or `Product/ledgers/GLOSSARY.md`; link to their canonical home, don't restate.

| Term | Meaning |
|---|---|
| `Definition` | Shared authored data describing what an entity *is* before runtime state. Stable definition ID. |
| `Instance` | Runtime entity created from a definition. Has an `InstanceId`, may carry per-instance variables, modifiers, relationships. |
| `InstanceId` | Typed `Identifier<Instance>`. Stable across the instance's lifetime. Used for variable storage, modifier storage, history, server-internal subscriptions. |
| `Variable` / `VariableValue` | Key + typed value pair. Variables are identified by `Variable` (`Key` + `PayloadTypeId`); values use typed wrappers (`IntVariableValue`, `FloatVariableValue`, etc.). |
| `VariableModifier` | Layered modifier that adjusts an effective value. Subclasses (`IntAddModifier`, `BoolOverrideModifier`, …) implement `Apply(current)`. Ordered by `Order` (ascending fold; FIFO ties). |
| `EntityVariableState` | Canonical per-entity state slice. `BaseValues` (override-only map) + `ModifierStacks` (keyed by `Variable`). |
| `StateEntity<TDef>` | Thin runtime handle implementing `IReadOnlyEntity<TDef>` + `IMutableEntity<TDef>` over `StoreVariableStorage`. Re-created on `LoadSnapshot`; not state itself. |
| `EntityDefinition` | Base type for game-authored definitions; declares `DefinedVariables` and `TryGetDefaultValue`. |
| `IEntityRegistry` | Engine-owned `InstanceId → handle` registry. The single creation/destruction point and lookup surface. |
| `IModifierFactory` | Engine-owned factory producing `EntityModifierEntry` with deterministic `ModifierId`s. The only sanctioned producer of pre-populated modifier entries. |
| `ModifierId` | Deterministic id for a modifier. Generated from an engine-owned counter snapshotted with engine state. |
| `ModifierSource` | Attachment entity's `InstanceId` (and optional `Tag`). Source attribution for cleanup-on-attachment-death sweeps. |

## Architecture

Two Models. The conceptual model lives in `01-definition-instance-model.md` (definitions, instances, variables, modifiers, attachments, the bridge boundary); the runtime API surface lives in `02-runtime-surface.md` (`StateEntity<TDef>` handle, read API, subscriptions, mutation surface, `IEntityRegistry`). Three flows tie them together: **spawn → base → modifier** (handle constructed empty, base overrides, modifier layers fold), **identity preservation across zone moves** (`InstanceId` stable; per-game opt-in for MTG-style replacement), and **snapshot / restore** (slices restore; handles re-created via `EntityStateFactory`; subscribers must re-bind).

## Composition

| Model | Role |
|---|---|
| [`models/01-definition-instance-model.md`](models/01-definition-instance-model.md) | Conceptual model: definitions, instances, variables, modifiers, attachments, composition; bridge boundary (Store + entities); mutation payload conventions |
| [`models/02-runtime-surface.md`](models/02-runtime-surface.md) | Concrete API: `StateEntity<TDef>` handle, read API, subscriptions, mutation surface, `IEntityRegistry`, authoring reference, worked examples |

## Invariants

Cross-Model rules that hold across both Models and shape how downstream features must use entities.

- **Store is canonical; entities is the API.** Per-entity variables live as `EntityVariableState` slices in the Store. Reads and writes go through the `StateEntity<TDef>` handle (or raw payloads for the cases the handle doesn't expose). No two-way sync, no duplicate state.
- **Definition immutability on the handle.** `Definition` lives on the handle, not in `EntityVariableState`. Transformation creates a new `StateEntity<TNewDef>` for the same `InstanceId`. There is no state-level `SetDefinitionPayload`.
- **Modifier source = attachment entity's `InstanceId`.** Cleanup-on-source-death is a uniform `RemoveModifiersBySourcePayload(target, new ModifierSource(attachment.Id))` per affected target. Source attribution is how the framework cleans up modifiers when their attachments die.
- **Deterministic `ModifierId` via factory only.** `ModifierId.New()` (`Guid.NewGuid()`) is forbidden in framework / handler code. The `IModifierFactory` allocates ids from an engine-owned counter snapshotted with engine state, so replay and multiplayer determinism hold.
- **Effective-value cache rebuild is eager and synchronous.** Read-after-write within a single handler is fresh; subscriber notifications are serialized on a single thread.

## Boundaries

**Owns:** the definition / instance / variable / modifier conceptual model; `EntityVariableState` slice and the six bridge mutation payloads; `StateEntity<TDef>` handle + `IReadOnlyEntity` / `IMutableEntity` surfaces; subscription channels; `IEntityRegistry` and `IModifierFactory` contracts; effective-value cache semantics.

**Does not own:**
- Card-specific shape (`Card`, `CardRef`, `CardMapState`) → `cards/`
- Zone membership and zone-shape (`ZoneContentsState`, `Zone`) → `zones/`
- Action pipeline, command handlers → `actions/`
- Effect orchestration, attachment lifecycle (Cleanup hook fires from `effects/`) → `effects/`
- Trigger fold semantics → `triggers/`
- Engine-side service composition (where `IEntityRegistry` is constructed and registered) → `engine/`

## Dependencies

*Mirrors the front-matter `references:` array in human-readable form (per §3).*

- **Upstream:** none — entities are foundational. Sits at the bottom of the feature dependency graph.
- **Downstream:** `cards/` (Card extends `StateEntity<CardDefinition>`), `zones/` (zones share `Identifier<Instance>` namespace), `actions/` (handlers consume `IEntityRegistry`), `effects/` (effects consume `IEffectScope.Entities`), `triggers/` (modifier-bearing entities subscribe to triggers), `engine/` (engine constructs and exposes the registry), `presentation/` (consumes entity state via slice subscriptions).

## Open

See `ledgers/OPEN-ITEMS.md` — entity-related questions:

- `oq-entity-destruction` — what "destroyed" means for the Store; when the framework Cleanup hook fires; what happens to in-flight dispatches referencing the destroyed entity.
- `oq-engine-builder` — engine-side service composition (where `IEntityRegistry`, `IModifierIdGenerator`, `IInstanceIdGenerator` are constructed, registered, exposed).
- `bl-authoring-feature` — authoring layer on top of `EntityDefinition` (custom-game-definition serialization, validation, asset migration, definition versioning).
