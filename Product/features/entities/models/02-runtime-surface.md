---
type: model
status: settled
last-updated: 2026-05-02
builds-on:
  - features/entities/models/01-definition-instance-model.md
tags: [#entities, #runtime, #api]
---

# Entity Runtime Surface

Concrete API for reading, writing, spawning, and destroying entities at runtime through the `com.scaffold.entities` + `com.scaffold.entities.states` packages. Companion to [`01-definition-instance-model.md`](features/entities/models/01-definition-instance-model.md), which holds the conceptual model and the mutation-payload conventions; this doc tells a game author what to actually call.

Audience: handler authors, effect authors, presentation projections, replay/snapshot tooling.

---

## 1. The Handle — `StateEntity<TDef>`

Every entity at runtime is referenced through a typed handle:

```csharp
StateEntity<TDef> : BaseEntityInstance<TDef>, IMutableEntity<TDef>, IDisposable
    where TDef : IEntityDefinition
```

The handle is a thin object over a per-instance `EntityVariableState` slice in the Store. It exposes everything callers need: read access, mutation, subscriptions, identity. Callers never construct it directly — the engine's `IEntityRegistry` (§5) hands them out.

`StateEntity<TDef>` is **not** a `State`. Snapshots only contain canonical `EntityVariableState` rows; handles are re-created from the registry on `LoadSnapshot`.

---

## 2. Read API — `IReadOnlyEntity<TDef>`

```csharp
public interface IReadOnlyEntity<out TDef> where TDef : IEntityDefinition
{
    InstanceId Id { get; }

    T    GetVariable<T>(Variable key);
    bool TryGetVariable<T>(Variable key, out T value);

    IDisposable Subscribe(Variable key, Action<VariableValue> onChange);
    void        Unsubscribe(Variable key, Action<VariableValue> onChange);

    IDisposable SubscribeToVariableStructuralChanges(
        Action<VariableStructuralChange, Variable, VariableValue?> handler);
}
```

**Inner-type reads.** `GetVariable<T>` and `TryGetVariable<T>` take the *inner* value type, not the wrapper:

```csharp
int hp = entity.GetVariable<int>(Vars.Health);            // ✅
FloatVariableValue v = entity.GetVariable<float>(Vars.Speed);  // ❌ wrong: ask for float, not the wrapper
```

The handle resolves the effective value (definition default → instance base override → modifier fold) before unwrapping. Authors never see `VariableValue` subclasses on the read side unless they're working with structural-change events.

**Effective vs. base.** `GetVariable<T>` returns the **effective** value (with all modifiers applied). To inspect raw base values or individual modifiers, read the slice directly: `store.Get<EntityVariableState>(entityId)` returns the canonical record exposing `BaseValues` and `ModifierStacks`.

**Polymorphism.** Code that should work against either authoring instances or state-backed entities should hold the interface:

```csharp
void Inspect<TDef>(IReadOnlyEntity<TDef> e) where TDef : IEntityDefinition { ... }
```

---

## 3. Subscriptions

Two subscription channels:

- **Per-variable value change** — `Subscribe(key, Action<VariableValue> onChange)`. Fires whenever the effective value for `key` changes (after fold). The first callback fires synchronously with the current value when subscribing. Returns `IDisposable` that, when disposed, unregisters. Equivalent imperative form: `Unsubscribe(key, onChange)`.
- **Structural changes** — `SubscribeToVariableStructuralChanges(handler)`. Fires `Added`/`Removed` events when a variable enters or leaves the entity's effective key set. Coalesced over the atomic batch — a `Remove`-and-`Re-Add` of the same variable in one `ExecuteBatch` produces no event.

Use the per-variable channel for "react to attack stat changes." Use the structural channel for "this entity now has a Stunned variable" / "the Stunned variable was removed."

For per-mutation granularity (every `Add`/`Remove` event from a single batch), subscribe to the slice directly:

```csharp
store.Subscribe<EntityVariableState>(entityId, (_, state, ev) => { ... });
```

This is the engine's path; presentation rarely needs it.

**Lifetime.** Subscriptions are released automatically when the entity is destroyed (via `StateEntity.Dispose()` and the underlying `StoreVariableStorage.Dispose()`). Manual `Unsubscribe` / disposing the handle returned from `Subscribe` is the correct discharge for shorter-lived listeners (e.g. presentation views unloading).

---

## 4. Mutation API

### 4.1 Default surface — `IMutableEntity<TDef>`

**Handlers and framework code prefer the entity handle over raw payloads.** Holding a `StateEntity<TDef>` and calling its mutator methods is the recommended default. Raw `ctx.Store.Execute(payload)` is the documented fallback only for batch authoring, history-replay tooling, and paths that hold an `InstanceId` but not a handle (see §4.2).

```csharp
public interface IMutableEntity<TDef> : IReadOnlyEntity<TDef>
    where TDef : IEntityDefinition
{
    bool       AddVariable(Variable key, VariableValue initialBase);
    bool       RemoveVariable(Variable key);

    ModifierId AddModifier(EntityModifierEntry entry);

    bool       RemoveModifier(Variable key, ModifierId id);
    void       ClearModifiers();
}
```

Each method synthesizes the corresponding bridge payload (per [`01-definition-instance-model.md` §"Mutation API conventions"](features/entities/models/01-definition-instance-model.md)) and dispatches it via `Store.Execute`. Card Framework conventions on top of this surface:

- **Always build modifier entries through the `Modifiers` factory.** `ctx.Modifiers.Create(key, modifier, source)` returns an `EntityModifierEntry` with a deterministic `ModifierId` and the source attribution baked in. Constructing entries directly via `new EntityModifierEntry(key, modifier)` falls back to `ModifierId.New()` (`Guid.NewGuid()`) inside `AddModifier` and is forbidden in handler/framework code — replay and multiplayer determinism require deterministic ids. See §4.3.
- **`AddVariable` returns `false` when the variable already exists.** It does not overwrite. Callers that want "set base value, creating the variable if missing" should branch: try `AddVariable`; if it returns `false`, dispatch `SetBaseValuePayload` directly (see §4.2).
- **`RemoveVariable` returns `true` when something was actually removed.** Includes both base values and any modifier stacks for that variable.
- **`ClearModifiers` is bulk and atomic** — single `ExecuteBatch` containing one `RemoveModifierPayload` per active modifier. Use sparingly; prefer source-bulk removal for cleanup-on-attachment-death (§4.2).

### 4.2 Raw payloads — when the handle isn't enough

`IMutableEntity` does **not** expose `SetBaseValue` or `RemoveModifiersBySource`. For those, dispatch the raw payload through `ctx.Store.Execute(...)`:

```csharp
// Out-of-band base value change (level-up, save load, authoring) — Q3
ctx.Store.Execute(new SetBaseValuePayload(
    entityId, Vars.MaxHealth, new IntVariableValue(50)));

// Sweep all modifiers from a single source (attachment death) — Q4
ctx.Store.Execute(new RemoveModifiersBySourcePayload(
    targetId, new ModifierSource(attachmentId)));
```

Other raw-payload cases:

- Batch authoring (loading a card collection from JSON, applying a save snapshot).
- History replay tooling that reconstitutes mutations from a recorded log.
- Any path that holds an `InstanceId` but not a handle.

### 4.3 The `Modifiers` factory

```csharp
public interface IModifierFactory
{
    EntityModifierEntry Create(
        Variable key,
        VariableModifier modifier,
        ModifierSource? source = null);
}
```

The factory hides id allocation entirely. It generates a deterministic `ModifierId` from an engine-owned counter (snapshotted with engine state, restored on `LoadSnapshot`) and returns a fully-populated `EntityModifierEntry` with the id and source baked in. Handler code never names a `ModifierId` directly.

`EntityModifierEntry` (Scaffold) carries optional `Id` and `Source` fields; when `AddModifier` receives an entry with these set, the bridge honours them and skips `ModifierId.New()`. The factory is the only sanctioned producer of pre-populated entries in framework code.

Two-step usage — build first, then install:

```csharp
var attackBuff = ctx.Modifiers.Create(
    Vars.Attack,
    new IntAddModifier { Amount = 3 },
    new ModifierSource(sword.Id));

villager.AddModifier(attackBuff);
```

The local-binding step is intentional: it surfaces the modifier as a value the reader can name and reason about (passed through to triggers, logged, etc.), rather than a one-line side effect.

### 4.4 Instance-only variable example (`AddEntityVariablePayload` discipline)

Reserved for variables *not* declared in the entity's definition. Concrete case: a generated token gets a `corruption_level` counter unique to this token, not part of `TokenDefinition.DefinedVariables`.

```csharp
var token = ctx.Entities.Spawn(new ImpDefinition());
token.AddVariable(Vars.CorruptionLevel, new IntVariableValue(0));

// later — increment the per-instance counter via a modifier
var corruptionStep = ctx.Modifiers.Create(
    Vars.CorruptionLevel,
    new IntAddModifier { Amount = 1 });
token.AddModifier(corruptionStep);
```

If `Vars.CorruptionLevel` were declared in `ImpDefinition.DefinedVariables`, calling `AddVariable` would return `false` (slice's `WithVariable` is a no-op when the key already exists in `BaseValues`). Definition-declared variables stay as definition defaults until written.

---

## 5. Lifecycle — `IEntityRegistry`

The engine owns an `InstanceId → handle` registry. It's the single creation/destruction point for entities and the lookup surface for handlers, effects, and projections.

```csharp
public interface IEntityRegistry
{
    StateEntity<TDef> Spawn<TDef>(TDef definition)              where TDef : IEntityDefinition;
    StateEntity<TDef> Get<TDef>(InstanceId id)                  where TDef : IEntityDefinition;
    bool              TryGet<TDef>(InstanceId id, out StateEntity<TDef> entity)
                                                                where TDef : IEntityDefinition;
    bool              Has(InstanceId id);
    bool              Destroy(InstanceId id);
}
```

The registry is exposed on both surfaces handlers and effects encounter:

```csharp
// IHandlerContext (per effects/04 §4.4)
IEntityRegistry Entities { get; }

// IEffectScope (per effects/04 §4.3)
IEntityRegistry Entities { get; }
```

### 5.1 Spawn

`Entities.Spawn<TDef>(definition)`:

1. Allocates a fresh `InstanceId` via the engine's deterministic `IInstanceIdGenerator`.
2. Calls `EntityStateFactory.Create<TDef>(definition, store, instanceId)`, which:
   - Registers an empty `EntityVariableState.Empty` slice keyed by the new id.
   - Builds the per-instance `StoreVariableStorage` (subscribes to the slice, primes caches).
   - Constructs the `StateEntity<TDef>` handle.
3. Records the handle in the registry.
4. Returns the handle.

The new entity inherits *all* its variable values from the definition (empty `BaseValues` map → reads fall back to `definition.TryGetDefaultValue(var)`). To override a base value at spawn time, dispatch `SetBaseValuePayload` immediately after spawn.

### 5.2 Get / TryGet

`Get<TDef>(id)` throws if the id is unknown or the registered handle's definition type doesn't match `TDef`. Use `TryGet<TDef>` for tolerant lookup; it returns `false` on either failure.

`Has(id)` is the cheapest existence check — no type parameter, no allocation, no exception.

### 5.3 Destroy

`Entities.Destroy(id)`:

1. Calls `StateEntity.Dispose()` on the handle (releases the per-instance Store listener; fires the handle's `OnEntityRemoved` event; subscribers tear down).
2. Calls `store.UnregisterSlice<EntityVariableState>(id)` (drops the canonical row).
3. Removes the handle from the registry.

Returns `true` if something was destroyed, `false` if the id was unknown.

The framework Cleanup hook (per [`effects/03` §5](features/effects/models/03-ongoing-effects.md)) handles ongoing-effect attachments automatically — when an attachment entity is destroyed, every modifier sourced to it is swept from every target via `RemoveModifiersBySourcePayload(target, new ModifierSource(attachment.Id))`.

### 5.4 Snapshot / restore

Snapshots contain canonical `EntityVariableState` slices, never handles. On `LoadSnapshot`:

1. The engine walks the restored slices and reconstructs the registry.
2. For each slice, the engine re-creates a `StateEntity<TDef>` via `EntityStateFactory.Create(...)` using the same `InstanceId` and the definition associated with that id.
3. The mapping `InstanceId → definition` is engine-managed (typically a sibling slice that records "what was spawned" so the restore knows which `TDef` to instantiate).

Snapshot rollback to a state where an entity still existed re-creates its row and re-issues the handle. Subscribers attached to old handles are gone — projections that need to bind on rollback must re-subscribe through `IEntityRegistry`.

---

## 6. Read Consistency and Cache Rebuild

The handle reads through a per-rebuild effective-value cache (`StoreVariableStorage.effectiveCache`). Cache invalidation is **eager and synchronous**:

- Every change to the `EntityVariableState` slice fires a Store event subscribed by `StoreVariableStorage`.
- The handler rebuilds the cache before returning; per-variable subscribers fire for changed values; structural subscribers fire for added/removed keys.
- All of this is synchronous within the originating `Store.Execute` / `Store.ExecuteBatch` call.

Implications for handler code:

- **Read-after-write within a single handler is fresh.** `entity.AddModifier(...)` followed by `entity.GetVariable<T>(...)` returns the post-mutation effective value.
- **`ExecuteBatch` rebuilds once after all payloads commit.** Reads inside an `ExecuteBatch` block are not allowed (the bridge runs the whole batch atomically); reads after the batch see the final state.
- **No "stale read" race for handler code.** All Store mutations and all subscriber notifications are serialized on a single thread.

If a `RemoveVariable` payload is followed by an `AddVariable` for the same key in a single batch, the structural channel sees no event (coalesced). The per-variable subscriber sees the new value if it differs from the old. This is the intended default for continuous-effects cleanup.

---

## 7. Authoring Reference

This Model commits to using Scaffold's authoring types. Game-author guide for declaring the artifacts that handler/effect code references at runtime:

### 7.1 `Variable` — the key

```csharp
public sealed class Variable {
    public string Key            { get; }   // human-readable identifier
    public string PayloadTypeId  { get; }   // stable id for the value's wire type
}
```

Two strings — the human-readable `Key` (used for ordering, debugging) and a `PayloadTypeId` that pins the variable's value type for serialization and modifier compatibility. Authored either as:

- **`VariableSO`** — `ScriptableObject` carrying a `PayloadTypeId` with inspector-driven type selection. Authored as a `.asset` file; referenced by `EntityDefinition`s.
- **Code-defined** — `static readonly Variable Vars.Attack = new("attack", "int");` for keys that don't need ScriptableObject authoring.

Convention: a game's variable catalog lives as static fields on a `Vars` class (e.g. `Vars.Attack`, `Vars.Health`). Mixing inline-string keys at call sites is a smell — leads to typo drift.

### 7.2 `VariableValue` — the value wrapper

Abstract base; concrete subclasses are tagged with `[VariableValueId("...")]` and implement `IVariableValue<T>` for inner-type access:

```csharp
[VariableValueId("int")]
public sealed class IntVariableValue : VariableValue, IVariableValue<int> { ... }

[VariableValueId("float")]
public sealed class FloatVariableValue : VariableValue, IVariableValue<float> { ... }

// Also: BoolVariableValue, LongVariableValue, DoubleVariableValue, StringVariableValue
```

Read paths return the inner type (`int`, `float`, etc.). Write paths take the wrapper:

```csharp
ctx.Store.Execute(new SetBaseValuePayload(id, Vars.Health, new IntVariableValue(10)));
```

### 7.3 `VariableModifier<T>` — the modifier

```csharp
public abstract class VariableModifier {
    public int Order { get; }   // serialized; ascending fold order
}

public abstract class VariableModifier<T> : VariableModifier {
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

### 7.4 `EntityModifierEntry` — the (key, modifier) pair

```csharp
public sealed class EntityModifierEntry {
    public Variable        Key      { get; }
    public VariableModifier Modifier { get; }
    public EntityModifierEntry(Variable key, VariableModifier modifier);
}
```

The argument to `IMutableEntity.AddModifier`. Pre-builds the (key, modifier) pair for serialization-friendly authoring (cards declare modifier entries; effects construct them at runtime).

### 7.5 `IEntityDefinition` and `EntityDefinition`

```csharp
public interface IEntityDefinition {
    IEnumerable<Variable> DefinedVariables { get; }
    bool                  TryGetDefaultValue(Variable key, out VariableValue value);
}
```

Definitions declare which variables an instance has and what their default values are. Reads fall back to `TryGetDefaultValue` for any variable not in the instance's `BaseValues` map; this is what lets an empty-baseline instance still report `entity.GetVariable<int>(Vars.Attack) == 1`.

`EntityDefinition` is the standard `[Serializable]` ScriptableObject implementation, with an internal `VariableBag` and Inspector authoring. Game-specific definition classes inherit from it:

```csharp
public sealed class VillagerDefinition : EntityDefinition {
    public VillagerJob Job { get; init; }   // game-specific authored fields
}
```

Definitions can be Unity assets, code-constructed at boot, or hybrid. The Card Framework does not yet pin an authoring layer on top of `EntityDefinition` — see backlog item `bl-authoring-feature`.

---

## 8. Worked Example — Equip/Unequip

A villager with `Attack = 1` (definition default) equips a sword granting `+3 attack`; effective attack reads as `4`. On unequip, the sword's modifier is swept and effective attack reverts to `1`.

```csharp
public static class Vars {
    public static readonly Variable Attack = new("attack", "int");
}

public sealed class VillagerDefinition : EntityDefinition {
    // Authored: defines Vars.Attack with default value 1
}

public sealed class SwordDefinition : EntityDefinition { /* ... */ }

public sealed record EquipSwordCommand(InstanceId Wearer, InstanceId Sword) : GameAction;
public sealed record EquipResult(int AttackBefore, int AttackAfter) : CommandResult;

public sealed class EquipSwordHandler : ICommandHandler<EquipSwordCommand, EquipResult>
{
    public Task<EquipResult> Handle(EquipSwordCommand cmd, IHandlerContext ctx)
    {
        var villager = ctx.Entities.Get<VillagerDefinition>(cmd.Wearer);
        var sword    = ctx.Entities.Get<SwordDefinition>(cmd.Sword);

        var before = villager.GetVariable<int>(Vars.Attack);   // 1 (definition default)

        // Build the +3 attack buff, sourced to the sword instance
        var attackBuff = ctx.Modifiers.Create(
            Vars.Attack,
            new IntAddModifier { Amount = 3, Order = 100 },
            new ModifierSource(sword.Id));

        // Install it on the villager via the IMutableEntity surface
        villager.AddModifier(attackBuff);

        var after = villager.GetVariable<int>(Vars.Attack);    // 4

        return Task.FromResult(new EquipResult(before, after));
    }
}

public sealed record UnequipSwordCommand(InstanceId Wearer, InstanceId Sword) : GameAction;
public sealed record UnequipResult(int AttackAfter) : CommandResult;

public sealed class UnequipSwordHandler : ICommandHandler<UnequipSwordCommand, UnequipResult>
{
    public Task<UnequipResult> Handle(UnequipSwordCommand cmd, IHandlerContext ctx)
    {
        // Sweep every modifier on the wearer that came from the sword
        ctx.Store.Execute(new RemoveModifiersBySourcePayload(
            cmd.Wearer,
            new ModifierSource(cmd.Sword)));

        // Destroy the sword instance — handle disposed, slice unregistered
        ctx.Entities.Destroy(cmd.Sword);

        var villager = ctx.Entities.Get<VillagerDefinition>(cmd.Wearer);
        var after = villager.GetVariable<int>(Vars.Attack);    // 1 (back to definition default)

        return Task.FromResult(new UnequipResult(after));
    }
}
```

For attachment models where the sword *itself* is the attached entity (per [`effects/03` §2](features/effects/models/03-ongoing-effects.md)), the framework Cleanup hook performs the source-sweep automatically on `Destroy` — the explicit `RemoveModifiersBySourcePayload` shown here is the long form, useful when the unequip semantics differ (e.g. sword survives but stops contributing).

---

## 9. Open items

- **`Spawn` with caller-supplied `InstanceId`.** Currently `Entities.Spawn<TDef>(def)` allocates the id internally. A `SpawnWithId<TDef>(def, id)` overload may be needed for save-state restoration paths that must reuse specific ids. Pin when the snapshot/load flow lands.
- **Engine-side service composition.** Where `IEntityRegistry`, `IModifierIdGenerator`, and `IInstanceIdGenerator` are constructed, registered, and exposed is engine-builder territory — see `oq-engine-builder`.
- **Authoring layer on top of `EntityDefinition`.** Custom-game-definition serialization, validation, asset migration, definition versioning are deferred — `bl-authoring-feature`.
- **Multi-store entities.** Currently `EntityStateFactory.Create` is parameterized on a single `Store`. Multi-store games (player-private stores, instance-shard stores) would need registry partitioning. Out of scope until a multi-store game arrives.

---

## 10. Related

- [`features/entities/models/01-definition-instance-model.md`](features/entities/models/01-definition-instance-model.md) — conceptual model, payload conventions, bridge resolution.
- [`features/effects/models/04-effect-flow.md`](features/effects/models/04-effect-flow.md) §4.3, §4.4 — `IEffectScope` and `IHandlerContext` surfaces.
- [`features/effects/models/03-ongoing-effects.md`](features/effects/models/03-ongoing-effects.md) §5 — Cleanup hook + `RemoveModifiersBySourcePayload` pattern.
- `com.scaffold.entities` — `Variable`, `VariableValue`, `VariableModifier`, `IReadOnlyEntity`, `IMutableEntity`, `EntityDefinition`.
- `com.scaffold.entities.states` — `EntityVariableState`, `StateEntity`, `EntityStateFactory`, six bridge payloads.
