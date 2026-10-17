---
type: model
status: superseded
superseded_by: ../docs/architecture.md
archived_on: 2026-10-18
reason: "Instances are Entity ids with Tag<T> data; InstanceWithSlices and EntityInstance were never built."
references: [features/entities/archive/01-definition.md, features/entities/archive/03-variable.md, features/entities/archive/04-modifier.md, features/entities/archive/05-attachment.md, features/cards/archive/01-card-model.md, features/zones/archive/01-zone-model.md, features/players/archive/01-player-model.md, features/engine/archive/06-game-definition.md]
tags: [#entities, #instance, #handle, #lifecycle, #model]
---

> **Archived 2026-10-18.** Superseded by [`docs/architecture.md`](docs/architecture.md). Instances are Entity ids with Tag<T> data; InstanceWithSlices and EntityInstance were never built. The body below is the old model, kept as history.

# Instance — model

## What it is

The runtime half of the entity model. An **instance** is a runtime entity created from a [`Definition`](features/entities/archive/01-definition.md), referenced by a typed **`Ref<TInstance>`**, and exposed through the framework handle **`InstanceWithSlices<TDef>`**. The handle pairs `(Definition, Storage)`, exposes `ISliceProvider`, and is registered in the Store's catalog. Variables and modifiers live in the canonical `EntityState` slice keyed at the instance's `SelfRef`; the handle is in-process side-state, not snapshotted.

```csharp
// Scaffold (external): Scaffold.Entities
public class EntityInstance<TDef> where TDef : IEntityDefinition
{
    public TDef                   Definition { get; }
    public IEntityVariableStorage Storage    { get; }
}

// Card Framework parent — adds ISliceProvider, hides Definition from external consumers
public class InstanceWithSlices<TDef> : Scaffold.EntityInstance<TDef>, ISliceProvider
    where TDef : IEntityDefinition
{
    protected new TDef Definition => base.Definition;

    public virtual IEnumerable<BaseState> GetInitialSlices()
        => Definition is ISliceProvider provider
            ? provider.GetInitialSlices()
            : Array.Empty<BaseState>();
}
```

`InstanceWithSlices<TDef>` is identity-free at the base. Concrete subclasses (`Card`, `Zone`, `Player`) carry typed `Ref<TSubclass> SelfRef` — the stable runtime identity. They are **not sealed** (project convention: default to unsealed); games may subclass for typed re-export or game-side helpers.

## Contracts

### Instance concept

An instance is a runtime entity created from a definition. Conceptual fields:

- Typed `Ref<TInstance>` (the runtime identity).
- Definition reference.
- Current relationships, local variable overrides, local modifiers, visibility state, lifecycle state.

The instance does not duplicate every definition field; it references its definition and stores only runtime differences in `EntityState`.

Examples: this specific copy of Fireball in a player's hand; this specific villager on the board; this specific sword attached to a villager; this specific hand zone for player 1.

### `Ref<T>` and instance identity

There is no separate "identity" concept. The instance is the durable runtime object; its typed `Ref<TInstance>` is a lookup/reference value.

`Ref<T>` is a sealed-record subtype of `Reference` (the polymorphic addressable type in `Scaffold.States`), backed by a `Guid`. It is stable across the instance's lifetime. Refs are allocated via `store.Catalog.AllocateRef<T>()` (typically inside `StoreEntities.Spawn<TDef>`) and resolved via `store.Catalog.Resolve(ref)`. Mutations that create instances (e.g., `CreateCardMutation`) carry the allocated `Ref<T>` in the payload for replay determinism.

**`Ref<T>` vs `ActorId`.** Concrete subclasses carry `Ref<TSubclass> SelfRef` — typed runtime identity backed by `Guid`. `ActorId` is a distinct concept identifying *who issues* a command, used in command attribution and history envelopes. `Player` carries both — `ActorId` on the `IActor` interface, `Ref<Player>` on the entity handle. Non-entity actors (`World`, `Environment`, `Bot`, `Market`) implement `IActor` directly with just an `ActorId`. See [`GLOSSARY.md`](ledgers/GLOSSARY.md).

### Zone movement preserves the instance

**Default rule:** moving an entity to another zone preserves the same instance and the same `Ref<T>`. Zone movement is an event that may trigger rules, update visibility/ordering/modifiers/presentation — but does not inherently destroy/recreate the entity.

Game-specific systems may override this default explicitly: a Magic-like preset may treat certain zone changes as replacement; a transform effect may replace a definition or create a new instance; a clone effect may create a new instance with selected copied state; a token leaving play may be destroyed instead of preserved. Explicit game behavior, not a hidden property of all zones.

### `Definition` access discipline

`Definition` is `protected` on `InstanceWithSlices<TDef>` so subclasses (framework or game-side) can read it; external consumers cannot navigate to `instance.Definition`. The framework convention is to read authored data through the variable bag (`card.Storage.GetVariable<int>(Vars.Cost)`); the variable channel round-trips through serialization, replay, and snapshot cleanly.

For typed-non-variable authored data (typed enums, complex records on the definition), game code may subclass the framework handle and re-expose what it needs:

```csharp
public class VillagerHandle : Card
{
    public VillagerJob Job => ((VillagerDefinition)base.Definition).Job;
}
```

This pattern is allowed but **discouraged** — typed `Definition` reads couple the consumer to the typed Definition subclass and break under transformation (definition swap). Prefer modelling game-specific authored data as variables in the bag.

### Lifecycle — `store.Catalog` and `StoreEntities.Spawn`

The Store owns a `Catalog` (in-process side-state, not snapshotted) that holds live handles by `Ref<T>`. The framework primitive is the `StoreEntities.Spawn<TDef>` extension:

```csharp
public static class StoreEntities
{
    public static (EntityInstance<TDef> Instance, Ref<EntityInstance<TDef>> Ref)
        Spawn<TDef>(this Store store, TDef def) where TDef : IEntityDefinition;
}

public interface ICatalog
{
    Ref<T> AllocateRef<T>() where T : class;
    void   RegisterAt<T>(Ref<T> r, T obj) where T : class;
    T      Resolve<T>(Ref<T> r) where T : class;
    bool   Unregister<T>(Ref<T> r) where T : class;
}
```

#### Spawn

`store.Spawn<TDef>(definition)`:

1. Allocates a fresh `Ref<EntityInstance<TDef>>` via `store.Catalog.AllocateRef<EntityInstance<TDef>>()`.
2. Builds the per-instance `StoreVariableStorage(store, ref)`.
3. Registers an empty `EntityState` slice keyed by the new ref.
4. Constructs the `EntityInstance<TDef>` handle pairing `(definition, storage)`.
5. Registers the handle in the catalog (`store.Catalog.RegisterAt(ref, instance)`).
6. Returns `(instance, ref)`.

The new entity inherits *all* its variable values from the definition (empty `BaseValues` map → reads fall back to `definition.TryGetDefaultValue(var)`). To override a base value at spawn time, call `instance.Storage.SetBase(...)` or dispatch `SetBaseValuePayload` immediately after spawn.

Typed subclass helpers (`Card.Spawn`, `Zone.Spawn`, `Player.Spawn`) wrap `StoreEntities.Spawn` and additionally cast the ref to the typed shape (`Ref<Card>`):

```csharp
public class Card : InstanceWithSlices<CardDefinition>
{
    public Ref<Card> SelfRef { get; init; }

    public static Card Spawn(Store store, CardDefinition def)
    {
        var (instance, r) = store.Spawn(def);
        var card = new Card(...) { SelfRef = r.Cast<Card>() };
        store.Catalog.RegisterAt(card.SelfRef, card);
        return card;
    }
}
```

#### Resolve

`store.Catalog.Resolve(ref)` returns the live handle, or throws if the ref is unknown. For tolerant lookup, use `TryResolve`. Per-kind game-specific registries can be thin wrappers around the catalog, but `CardRegistry` itself is retired (per the open-seams pass) — `store.Catalog.Resolve(refOfCard)` is the canonical resolution path.

#### Destroy

Destroying an entity:

1. Dispose any per-handle subscriptions (storage adapter, presentation views).
2. Unregister the slice (`store.UnregisterSlice<EntityState>(ref)`).
3. Unregister the catalog entry (`store.Catalog.Unregister(ref)`).
4. Drop any per-kind registry entry.

The framework Cleanup hook handles ongoing-effect attachments automatically — see [`05-attachment.md` §Cleanup is automatic](features/entities/archive/05-attachment.md). The exact lifecycle (what "destroyed" means, when Cleanup fires relative to the destruction envelope, what happens to in-flight dispatches that reference the destroyed entity) is a deferred topic (`oq-entity-destruction`).

#### Snapshot / restore

Snapshots contain canonical `EntityState` slices, never handles. The catalog is in-process side-state and is repopulated alongside snapshot load:

1. The engine walks the restored slices and reconstitutes catalog entries.
2. For each slice, the engine re-creates an `EntityInstance<TDef>` using the same `Ref<T>` and the definition associated with that ref.
3. The mapping `Ref<T> → definition` is engine-managed (typically a sibling slice that records "what was spawned" so the restore knows which `TDef` to instantiate).

Snapshot rollback to a state where an entity still existed re-creates its row and re-issues the handle. Subscribers attached to old handles are gone — projections that need to bind on rollback must re-subscribe through the catalog.

### Slice-level subscriptions and lifetime

For every change to the entity's `EntityState` slice (per-mutation granularity), subscribe to the slice directly:

```csharp
store.Subscribe<EntityState>(card.SelfRef, (oldState, newState, ev) => {
    // ev: StateChangeEvent.Updated | StateChangeEvent.Removed
    // newState: the post-mutation slice (or empty for removal)
});
```

This is the lowest-level surface — engines, debuggers, and replay tools use it. Per-variable subscriptions (effective-value reactivity) and per-modifier-stack subscriptions live in [`03-variable.md`](features/entities/archive/03-variable.md) and [`04-modifier.md`](features/entities/archive/04-modifier.md) respectively.

When a card / zone / player is destroyed, the slice is unregistered and the catalog entry is cleared. Slice subscribers receive a removal event; the handle is no longer resolvable through the catalog. Subscribers attached through the storage adapter are automatically released when the storage is disposed.

For long-lived consumers that need to react symmetrically to the spawn they bound against (presentation views, telemetry), watch the slice directly via `store.Subscribe<EntityState>(selfRef, ...)` and react to the removal event.

### Read consistency

The storage adapter reads directly from the canonical `EntityState` slice through its pure-read methods. There is no per-handle effective-value cache — the slice is the source of truth, mutators return new records via `this with { ... }`, and reads see the latest record on every call. All Store mutations and all subscriber notifications are serialized on a single thread.

Implications for handler code:

- **Read-after-write within a single handler is fresh.** `card.Storage.AddModifier(...)` followed by `card.Storage.GetVariable<T>(...)` returns the post-mutation effective value.
- **`ExecuteBatch` commits once after all payloads run.** Reads inside an `ExecuteBatch` block are not allowed (the bridge runs the whole batch atomically); reads after the batch see the final state.
- **No "stale read" race for handler code.** Single-threaded serialization guarantees no inter-handler races.

### Engine bridge boundary

These are integration concerns the engine owns:

- **Single mutator registration per Store.** `EntityBridgeContext.RegisterMutators(storeBuilder)` runs once at engine bootstrap, registering all seven entity-bridge mutators (see [`03-variable.md`](features/entities/archive/03-variable.md) and [`04-modifier.md`](features/entities/archive/04-modifier.md) for the per-payload list).
- **Catalog and re-hydration.** `LoadSnapshot` rehydrates the catalog directly; the engine reconstitutes catalog entries from `EntityState` slices and the definition lookup as needed.
- **Disposal on entity death.** The engine releases the catalog entry and any per-handle subscriptions on the entity-removal path so short-lived tokens do not retain handlers.

## Invariants

1. **Every instance carries a typed `Ref<TInstance>`.** Stable for the instance's lifetime; allocated by `store.Catalog.AllocateRef<T>()`; resolved by `store.Catalog.Resolve(ref)`.
2. **Every instance references a base definition.** `Definition` lives on the handle (not in state); transformation creates a new handle for the same `Ref<T>`.
3. **`InstanceWithSlices<TDef>` is not a `State`.** Snapshots only contain canonical `EntityState` rows; handles are re-created from the catalog after `LoadSnapshot`.
4. **Zone movement preserves the instance by default.** Per-game systems can override explicitly (replacement, transformation).
5. **Concrete handle subclasses are unsealed.** Project convention; supports game-side typed re-export.

## Decisions

*n/a — no elevated ADR.* (Provenance Q3, Q4 below; see also [`01-definition.md` §Options considered`](features/entities/archive/01-definition.md) for Q1, Q2, Q5.)

## Examples

For worked equip/unequip and apply-aura examples that exercise the spawn → variable read → modifier install → destroy lifecycle end-to-end, see [`05-attachment.md` §Examples](features/entities/archive/05-attachment.md).

## Open follow-ups

- **`oq-entity-destruction`** — what "destroyed" means for the Store; when the framework Cleanup hook fires; what happens to in-flight dispatches referencing the destroyed entity.
- **`Spawn` with caller-supplied `Ref<T>`.** Currently `store.Spawn<TDef>(def)` allocates the ref internally. A `SpawnAt<TDef>(def, ref)` overload may be needed for save-state restoration paths that must reuse specific refs. Pin when the snapshot/load flow lands.
- **Multi-store entities.** Currently `StoreEntities.Spawn` is parameterized on a single `Store`. Multi-store games (player-private stores, instance-shard stores) would need catalog partitioning. Out of scope until a multi-store game arrives.
- **`oq-engine-builder`** — engine-side service composition (where `IModifierFactory`, the deterministic ref/id generators, and per-kind registries are constructed, registered, exposed).

---

*Below: provenance — two Core Model decisions (formerly "doc 8") that shape this Model. Single-feature scope; folded per §4.5 on 2026-05-08. Original landing 2026-04-29.*

## Options considered

### Q3 — Canonical state

What is the canonical source of truth: entity instances, Store slices, or a composed model where each owns different concerns?

- **A — Entities are canonical; Store is auxiliary.** Rejected — duplicates state, requires two-way sync, no snapshot/rollback for entity variables.
- **B — Store is canonical; Entities is the API.** Per-entity variables live as `EntityState` slices in the Store. `EntityInstance<TDef>` is a thin handle over `StoreVariableStorage`. Implemented by `com.scaffold.entities.states` (the bridge package). **Chosen.**
- **C — Composed model with split ownership.** Rejected — adds coordination overhead without clear benefit.

Reasoning: one source of truth (no duplication, no two-way sync); snapshot/rollback comes from the Store for free; polymorphic substitution (any code holding `EntityInstance<TDef>` works against either store-backed or overlay-backed handles); modifier ordering, source attribution, atomic batches, and coalesced subscriber notifications all come from the Store + bridge.

### Q4 — Instances and zones

How should an instance behave when a card changes zones?

- **A — Bake Magic-style zone-change replacement into all games.** Rejected — many games don't need it; should be opt-in per game.
- **B — Preserve the same instance across zone changes by default; per-game overrides.** **Chosen** — zone changes are observable facts, not instance replacement by default.

The instance is the durable runtime object; its `Ref<T>` is a lookup/reference value. Zone movement can trigger effects, modifiers, visibility changes, or game-specific replacement rules. The base system preserves the same instance by default.

## Rationale

The two instance-side decisions converge on the runtime shape:

- **Q3 (Store canonical, Entities as API)** gives one source of truth for per-entity state; snapshot/restore, polymorphic storage, and atomic batches fall out for free.
- **Q4 (preserve instance across zone movement)** is the right default for non-MTG games; explicit per-game overrides handle games that need replacement semantics.

`InstanceWithSlices<TDef>` consolidates the slice-provider contract on the Instance side and tightens `Definition` exposure; the spawn loop walks `instance.GetInitialSlices()` (default delegates to the definition) and installs slices keyed at `instance.SelfRef`.

## Consequences

- **Enables** snapshot/rollback for per-entity variables; deterministic replay (refs allocated through the catalog, `EntityState` rebuilt from slices); polymorphic code via `IEntityVariableStorage`; framework-supplied lifecycle (Cleanup hook on destruction sweeps source-attributed modifiers); cross-game neutrality (no MTG semantics baked in).
- **Constrains** all per-entity state mutations to route through bridge payloads or the storage adapter (no direct field writes); `Definition` lives on the handle, not in state (transformation requires new handle); subscribers attached to old handles are gone after rollback (must re-subscribe through the catalog).
