---
type: model
status: superseded
superseded_by: ../docs/architecture.md
archived_on: 2026-10-18
reason: "Only IEntityDefinition + Catalog<TDef> exist; the Scaffold.Entities definition model was not carried over."
references: [features/entities/archive/02-instance.md, features/entities/archive/03-variable.md, features/entities/archive/04-modifier.md, features/entities/archive/05-attachment.md, features/engine/archive/06-game-definition.md]
tags: [#entities, #definition, #slice-provider, #model]
---

> **Archived 2026-10-18.** Superseded by [`docs/architecture.md`](docs/architecture.md). Only IEntityDefinition + Catalog<TDef> exist; the Scaffold.Entities definition model was not carried over. The body below is the old model, kept as history.

# Definition — model

## What it is

The authored half of the entity model. A **`Definition`** is shared, immutable data describing what an entity *is* before runtime state — fields, defaults, declared variables, slice declarations. Every runtime entity references a Definition; tokens and generated entities use lightweight system definitions rather than going definitionless.

The framework uses three layers on the Definition side:

1. **`IEntityDefinition`** (Scaffold-external) — the contract: declared variables + default values.
2. **`EntityDefinition`** (Scaffold-external) — the standard `[Serializable]` ScriptableObject implementation.
3. **`DefinitionWithSlices`** (framework-introduced) — extends `Scaffold.EntityDefinition` and adds **`ISliceProvider`**. Concrete framework definition kinds (`CardDefinition`, `ZoneDefinition`, `PlayerDefinition`) inherit from it.

```csharp
public interface ISliceProvider
{
    IEnumerable<BaseState> GetInitialSlices();
}

public abstract class DefinitionWithSlices : Scaffold.EntityDefinition, ISliceProvider
{
    public virtual IEnumerable<BaseState> GetInitialSlices() => Array.Empty<BaseState>();
}
```

## Contracts

### Definition concept

A definition is shared authored data describing what an entity is before runtime state. Conceptual fields:

- Stable definition ID (needed for save/load, replay, debugging, networking).
- Display name or debug name.
- Entity kind or category.
- Base variables + default values (declared via `IEntityDefinition.DefinedVariables` + `TryGetDefaultValue`).
- Optional tags, keywords, visual references, behavior/effect references, authoring metadata.

Examples: card definitions, token definitions, zone definitions, villager definitions, equipment definitions, relic definitions, card pack definitions, system helper definitions.

### `IEntityDefinition` and `EntityDefinition`

```csharp
public interface IEntityDefinition
{
    IEnumerable<Variable> DefinedVariables { get; }
    bool TryGetDefaultValue(Variable key, out VariableValue value);
}
```

Definitions declare which variables an instance has and what their default values are. Reads on an instance fall back to `TryGetDefaultValue` for any variable not in the instance's `BaseValues` map; this is what lets a freshly-spawned instance still report `entity.GetVariable<int>(Vars.Attack) == 1` without writing to its slice.

`EntityDefinition` is the Scaffold standard `[Serializable]` ScriptableObject implementation, with an internal `VariableBag` and inspector authoring. Game-specific definition classes inherit from it. Definitions can be Unity assets, code-constructed at boot, or hybrid.

### `DefinitionWithSlices` — framework parent for `ISliceProvider`

`Scaffold.EntityDefinition` is external; we don't modify it in place. The framework introduces a parent class — `DefinitionWithSlices : Scaffold.EntityDefinition, ISliceProvider` — that consolidates the slice-provider contract on the Definition side. The framework definition kinds (`CardDefinition`, `ZoneDefinition`, `PlayerDefinition`) inherit from `DefinitionWithSlices` and pick up a default empty `GetInitialSlices() => Array.Empty<BaseState>()`. Concrete subclasses override to declare per-instance slices:

```csharp
public sealed class PileDefinition : ZoneDefinition
{
    public int Target { get; init; }

    public override IEnumerable<BaseState> GetInitialSlices()
    {
        yield return new PileState(Counter: 0, Target: Target);
    }
}
```

`GameDefinition` is independent of `Scaffold.EntityDefinition` and implements `ISliceProvider` directly (it's a top-level bundle, not an entity definition). See [`engine/06-game-definition.md`](features/engine/archive/06-game-definition.md).

### `ISliceProvider` — canonical home

`ISliceProvider` is the parameterless interface every slice-declaring kind implements:

```csharp
public interface ISliceProvider
{
    IEnumerable<BaseState> GetInitialSlices();
}
```

Reached on Definition kinds via `DefinitionWithSlices` (this Model), on Instance kinds via [`InstanceWithSlices<TDef>`](features/entities/archive/02-instance.md) (which defaults to delegating to its definition), and directly on `GameDefinition` ([`engine/06`](features/engine/archive/06-game-definition.md)).

The interface is parameterless because **slices don't carry their own primary key** — the factory keys externally using the spawned instance's `SelfRef` (or no key for game-singletons). Reference-shaped fields *inside* slice values are data, not keys. This convention is what lets a `PileDefinition` produce N `PileState` slices keyed by N different `Ref<Zone>`s without per-instance wiring.

### Definition immutability

Once authored, a Definition is immutable for the lifetime of any instance referencing it. The instance carries `Definition` on the handle (not in `EntityState`). Transformation creates a new handle for the same `Ref<T>` rather than mutating the existing definition; see [`02-instance.md` §Definition access discipline](features/entities/archive/02-instance.md).

There is no state-level `SetDefinitionPayload`. Definition swap is a catalog operation: `store.Catalog.RegisterAt(selfRef, newHandle)`.

## Invariants

1. **Every entity has a definition.** No definitionless runtime entities; tokens and temporary system entities use lightweight system definitions.
2. **Definition IDs are stable.** Required for save/load, replay, debugging, networking.
3. **Definitions are immutable for any instance's lifetime.** Mutable per-instance state lives in `EntityState` slices keyed at the instance's `SelfRef`.
4. **`ISliceProvider` is parameterless.** Implementers yield slice values; the factory keys externally.
5. **Definition lives on the handle, not in state.** Definition swap is a catalog re-registration, not a state mutation.

## Decisions

*n/a — no elevated ADR.* (Provenance Q1, Q2, Q5 below.)

## Examples

```csharp
public sealed class VillagerDefinition : EntityDefinition
{
    // Authored: declares Vars.Attack with default 1, Vars.Health with default 3.
    // Inherited DefinedVariables / TryGetDefaultValue surface those.
}

public sealed class PileDefinition : ZoneDefinition
{
    public int Target { get; init; }

    public override IEnumerable<BaseState> GetInitialSlices()
    {
        yield return new PileState(Counter: 0, Target: Target);
    }
}
```

For per-game slice declaration via `GameDefinition.GetInitialSlices()`, see [`engine/06-game-definition.md`](features/engine/archive/06-game-definition.md).

## Open follow-ups

- **Authoring layer on top of `EntityDefinition`** — custom-game-definition serialization, validation, asset migration, definition versioning. Tracked under `bl-authoring-feature` in [`OPEN-ITEMS.md`](ledgers/OPEN-ITEMS.md).

---

*Below: provenance — three Core Model decisions (formerly "doc 8") that shape this Model. Single-feature scope; folded per §4.5 on 2026-05-08. Original landing 2026-04-29.*

## Options considered

### Q1 — Dependency boundary

Should the simulation core depend directly on Scaffold packages, or should Scaffold be used only in Unity-facing adapters?

- **A — Strict purity boundary.** Scaffold confined to Unity adapters. Rejected — forces architecture before we know whether Scaffold helps or fights.
- **B — No enforced boundary yet.** Scaffold allowed wherever it genuinely fits. **Chosen** — early docs describe boundaries by responsibility, not by package dependency.

Resolved by `com.scaffold.entities.states`: per-entity variables are canonical `EntityState` slices in the Store, exposed through `EntityInstance<TDef>` handles backed by `StoreVariableStorage` ([`02-instance.md`](features/entities/archive/02-instance.md), [`03-variable.md`](features/entities/archive/03-variable.md)).

### Q2 — Value model

Should card/entity values be stored primarily as flexible variables/tags, typed state records, or a hybrid?

- **A — Typed state records as first model.** Rejected — too rigid; assumes a fixed variable list.
- **B — Hybrid from the start.** Rejected — overbuilds before patterns are proven.
- **C — Key-value variables first; typed wrappers later as convenience.** **Chosen** — flexible across game types; mirrors Scaffold Entities' variable bags; avoids hardcoding game values into the framework.

Implication: typed concepts can exist as optional convenience wrappers, validators, schemas, or preset conventions over key-value variables. They should not become required hardcoded fields in the base framework. See [`03-variable.md`](features/entities/archive/03-variable.md) for the runtime API.

### Q5 — Entity definitions

Do all entities have definitions, or can runtime-only entities exist without definitions?

- **A — Allow definitionless runtime entities.** Rejected — breaks save/load, replay, networking, debugging.
- **B — All entities have a base definition.** **Chosen** — applies broadly: cards, tokens, zones, players/actors, relics, equipment, resources, villagers, enemies, card packs, temporary helper objects.

The framework supports lightweight system definitions for tokens and temporary entities instead of allowing definitionless entities.

## Rationale

The three definition-side decisions converge on a coherent shape:

- **Q1 (no enforced boundary)** lets the bridge package (`com.scaffold.entities.states`) emerge naturally without forcing premature architecture.
- **Q2 (key-value variables)** keeps the value model flexible across game types and aligns with Scaffold's variable-bag approach.
- **Q5 (definitions for everything)** makes authoring, debugging, save/load, replay, networking, and tool inspection tractable.

`DefinitionWithSlices` consolidates the `ISliceProvider` contract on the Definition side so the factory's spawn-walk has a uniform surface: walk every spawned instance's `GetInitialSlices()` (which defaults to delegating to the definition) and install slices keyed at the instance's `SelfRef`.

## Consequences

- **Enables** asset-friendly authoring (definitions can be Unity assets, code-constructed, or hybrid); deterministic save/load and replay (stable definition IDs); per-instance slices declared at the definition (a `PileDefinition` declares its `PileState` once); polymorphic substitution via `IEntityVariableStorage` (overlay-vs-store-backed).
- **Constrains** every framework definition kind to inherit from `DefinitionWithSlices`; slice values must NOT carry their own primary key; transformation (definition swap) requires a new handle, not a state mutation.
