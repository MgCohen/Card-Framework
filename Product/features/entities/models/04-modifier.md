---
type: model
status: settled
references: [features/entities/models/01-definition.md, features/entities/models/02-instance.md, features/entities/models/03-variable.md, features/entities/models/05-attachment.md]
tags: [#entities, #modifier, #effective-value, #model]
---

# Modifier — model

## What it is

The layered-fold layer of the entity model. **Modifiers** are how runtime adjustments to a variable's effective value are expressed — equipped sword grants `+3 attack`, aura applies `*2 damage`, status effect overrides a flag. Each modifier is a `VariableModifier<T>` subclass with `Apply(current) → adjusted`, a serialized `Order` for fold position, and a `ModifierSource` attribution. Modifiers stack in `EntityState.ModifierStacks` keyed at the instance's `SelfRef`; effective reads fold through them in `Order` ascending. Deterministic `ModifierId` allocation is enforced via the `IModifierFactory` so replay and multiplayer determinism hold.

```text
Definition default → BaseValues override → ModifierStacks fold → effective value

Villager base attack: 1            ← definition default
Sword attachment attack bonus: +3  ← modifier from sword (ModifierSource = sword.SelfRef)
Rage status modifier: +2           ← modifier from status-effect entity
Rainy zone context modifier: -1    ← modifier from zone-applied composition handler
Effective attack: 5
```

## Contracts

### Effective-value calculation

An effective value is calculated from layered inputs:

1. Base definition value (from `IEntityDefinition.TryGetDefaultValue`).
2. Instance base value or override (from `EntityState.BaseValues`; see [`03-variable.md`](features/entities/models/03-variable.md)).
3. Local modifiers on the instance (from `EntityState.ModifierStacks`).
4. Attached instance modifiers (modifiers contributed by an attachment entity, sourced to `attachment.SelfRef`; see [`05-attachment.md`](features/entities/models/05-attachment.md)).
5. External modifiers from other entities or systems.
6. Zone or context modifiers (composition handlers).
7. Preset-specific composition rules.

The calculation order is explicit and deterministic. Modifiers are layered and inspectable, not hidden inside one mutable value — debug tooling can name every contributor.

### `VariableModifier<T>`

```csharp
public abstract class VariableModifier
{
    public int Order { get; }   // serialized; ascending fold order
}

public abstract class VariableModifier<T> : VariableModifier
{
    public abstract T Apply(T current);
}
```

Built-in modifiers ship in `com.scaffold.entities`:

| Modifier | Inner type | Behavior |
|---|---|---|
| `IntAddModifier` | `int` | `current + amount` |
| `IntMultiplyModifier` | `int` | `current * factor` |
| `FloatAddModifier` | `float` | `current + amount` |
| `FloatMultiplyModifier` | `float` | `current * factor` |
| `BoolOverrideModifier` | `bool` | `value` (replaces current) |
| `StringAppendModifier` | `string` | `current + suffix` |

Custom modifiers subclass `VariableModifier<T>` and override `Apply`. `Order` controls fold position (lower runs first; ties FIFO by insertion).

### `ActiveModifier` — the runtime record

```csharp
public sealed record ActiveModifier(
    Variable          Key,
    VariableModifier  Modifier,
    ModifierId        Id,
    ModifierSource?   Source);
```

The fully-populated runtime record. The factory (`ctx.Modifiers.Create(...)`) is the sanctioned producer; the storage adapter's `AddModifier` accepts an `ActiveModifier` directly or the decomposed `(key, modifier, source)` shape.

### `ModifierSource` convention

`ModifierSource` is `(Reference, int)` — the contributing entity's `Ref<T>` plus an opt-in `Tag` for disambiguation:

```csharp
public sealed record ModifierSource(Reference Source, int Tag);
```

Sources include equipment instances, aura entities, status-effect entities, ongoing-effect attachments. Cleanup-on-source-death is uniform via `RemoveModifiersBySourcePayload(target, new ModifierSource(attachment.SelfRef, 0))` per affected target, or `store.RemoveModifiersFromSource(source)` for the bulk-across-all-entities sweep.

`Tag` defaults to `0` and is opt-in author disambiguation when one source contributes multiple modifier groups that may need partial removal.

### `IModifierFactory` — deterministic id allocation

```csharp
public interface IModifierFactory
{
    ActiveModifier Create(
        Variable key,
        VariableModifier modifier,
        ModifierSource? source = null);
}
```

The factory hides id allocation entirely. It generates a deterministic `ModifierId` from an engine-owned counter (snapshotted with engine state, restored on `LoadSnapshot`) and returns a fully-populated `ActiveModifier` with the id and source baked in. Handler code never names a `ModifierId` directly.

The factory is the only sanctioned producer of pre-populated `ActiveModifier`s in framework code. Constructing modifier ids ad-hoc via `Guid.NewGuid()` is **forbidden** in framework / handler code — replay and multiplayer determinism require deterministic ids.

Two-step usage — build first, then install:

```csharp
var attackBuff = ctx.Modifiers.Create(
    Vars.Attack,
    new IntAddModifier { Amount = 3 },
    new ModifierSource(sword.SelfRef, 0));

villager.Storage.AddModifier(attackBuff);
```

The local-binding step is intentional: it surfaces the modifier as a value the reader can name and reason about (passed through to triggers, logged, etc.), rather than a one-line side effect.

### `EntityState.ModifierStacks` slice

Per-entity modifier state lives as `EntityState.ModifierStacks` — an `ImmutableDictionary<Variable, ImmutableList<ActiveModifier>>` keyed externally at the entity's `SelfRef`:

```csharp
public sealed record EntityState(
    ImmutableDictionary<Variable, VariableValue>            BaseValues,
    ImmutableDictionary<Variable, ImmutableList<ActiveModifier>> ModifierStacks
) : BaseState;
```

Modifier lists are pre-sorted by insertion in `Order` ascending; `Variable` enumeration is the union of `BaseValues.Keys` and `ModifierStacks.Keys`. The `BaseValues` companion belongs to [`03-variable.md`](features/entities/models/03-variable.md).

### Mutation API

#### Default surface — `IEntityVariableStorage`

```csharp
public interface IEntityVariableStorage  // modifier-mutation members
{
    ModifierId  AddModifier(Variable key, VariableModifier modifier, ModifierSource? source = null);
    ActiveModifier AddModifier(ActiveModifier entry);   // fully-populated overload
    bool        RemoveModifier(Variable key, ModifierId id);
    void        RemoveModifiersFromSource(ModifierSource source);
    void        ClearModifiers();
}
```

| Method | Payload | Notes |
|---|---|---|
| `AddModifier(key, modifier, source?)` | `AddModifierPayload` | Allocates an id internally only for non-replay paths; prefer the factory-built overload. |
| `AddModifier(activeModifier)` | `AddModifierPayload` | Honours the pre-populated id and source unchanged. The recommended path. |
| `RemoveModifier(key, id)` | `RemoveModifierPayload` | Removes a single modifier by id. |
| `RemoveModifiersFromSource(source)` | `RemoveModifiersBySourcePayload` | Cleanup-on-source-death; sweeps every modifier on this entity attributed to the source. |
| `ClearModifiers()` | `ClearModifiersPayload` | Bulk + atomic; one slice transition, one subscriber notification. |

#### Raw payloads — when the handle isn't enough

```csharp
ctx.Store.Execute(new RemoveModifiersBySourcePayload(
    target.SelfRef, new ModifierSource(attachment.SelfRef, 0)));

ctx.Store.RemoveModifiersFromSource(source);   // StoreEntities extension; bulk sweep across all entities
```

`ClearModifiersPayload` is a single atomic transition that empties `ModifierStacks` for one entity (avoids the N-event storm of looping individual removes).

#### Batched mutations

Multiple payloads commit atomically through `Store.ExecuteBatch`. Build modifier entries via the factory (deterministic ids), then dispatch:

```csharp
var atkBuff = ctx.Modifiers.Create(Vars.Attack,  new IntAddModifier { Amount = 2 }, source);
var defBuff = ctx.Modifiers.Create(Vars.Defense, new IntAddModifier { Amount = 1 }, source);

ctx.Store.ExecuteBatch(new object[] {
    new AddModifierPayload(target.SelfRef, atkBuff.Key, atkBuff.Modifier, atkBuff.Id, atkBuff.Source),
    new AddModifierPayload(target.SelfRef, defBuff.Key, defBuff.Modifier, defBuff.Id, defBuff.Source),
});
```

When dropping to raw `AddModifierPayload` for batching, unpack the factory's `ActiveModifier` fields explicitly. Read consistency rules and atomic commit semantics live in [`02-instance.md` §Read consistency](features/entities/models/02-instance.md).

### Composition handlers

A composition handler calculates effective behavior from related instances and modifiers. Examples: equipment handler (equipped stats and abilities), stack recipe handler (whether a physical stack can craft), aura handler (continuous modifiers from nearby/attached cards), zone handler (restrictions/bonuses from current location), transformation handler (current definition overlay).

Composition handlers can be core utilities, preset-specific systems, or game-specific systems. They must be deterministic and traceable — every contribution is sourceable to a specific `Ref<T>` via `ModifierSource`.

## Invariants

1. **Effective values are explainable from base + modifiers.** Modifiers are layered and inspectable, not hidden inside one mutable value.
2. **Deterministic `ModifierId` via factory only.** `Guid.NewGuid()` for ids is forbidden in framework / handler code.
3. **`ModifierSource` is `(Reference, int)`.** The contributing entity's `Ref<T>` + opt-in `Tag` for disambiguation.
4. **Modifier lists are pre-sorted by `Order` ascending.** Insertion is FIFO for ties.
5. **`ClearModifiersPayload` is bulk and atomic.** One slice transition, one subscriber notification.
6. **`RemoveModifiersFromSource` is the cleanup-on-source-death hook.** Sweeps every modifier on this entity attributed to the source; uniform across equipment, auras, ongoing-effect attachments.

## Decisions

*n/a — no elevated ADR.* (Q6 — Composition — lives in [`05-attachment.md` §Options considered`](features/entities/models/05-attachment.md), where composition's attachment-flavored side is the primary subject.)

## Examples

For a worked equip/unequip example (modifier built via factory, installed on the wearer, swept on unequip), see [`05-attachment.md` §Equip / unequip example](features/entities/models/05-attachment.md). For an effect-orchestrated apply-aura example (factory call in the effect, install in the handler), see [`05-attachment.md` §Apply-aura example](features/entities/models/05-attachment.md).

## Open follow-ups

- **Modifier ordering policy across composition tiers.** Today `Order` is a single integer authored on the modifier subclass. When a game needs strict ordering across tiers (definition default → equipment → aura → zone → status), the `Order` integer covers it by convention; if richer tier semantics emerge, consider a `(Tier, Order)` tuple or named tiers. Defer until forced.
- **Custom modifier authoring.** Authoring tools for `VariableModifier<T>` subclasses (inspector-friendly, validation, per-game catalogs) tracked under `bl-authoring-feature`.
