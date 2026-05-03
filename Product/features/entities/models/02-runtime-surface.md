---
type: model
status: settled
references:
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

**`InstanceId` vs `ActorId`.** This handle is keyed on `InstanceId` — a typed reference into the Store (`Identifier<Instance>`). `ActorId` is a distinct concept identifying *who issues* a command, used in command attribution, history envelopes (`HistoryEnvelope.actor`), and per-actor available-action queries (`GetActionsFor(ActorId)`). An actor that is *also* an entity (a player with health/mana variables) carries both ids; system / AI / environment actors have an `ActorId` but no `InstanceId`. See [`GLOSSARY.md`](ledgers/GLOSSARY.md) — `Actor`, `ActorId`, `Instance ID` rows.

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

**Identity and definition.** `entity.Id` returns the `InstanceId`. `entity.Definition` (inherited from `BaseEntityInstance<TDef>`) returns the typed definition — useful for reading authored fields:

```csharp
var villager = ctx.Entities.Get<VillagerDefinition>(id);
VillagerJob job = villager.Definition.Job;   // authored field on the typed definition
```

**Polymorphism.** Code that should work against either authoring instances or state-backed entities should hold the interface:

```csharp
void Inspect<TDef>(IReadOnlyEntity<TDef> e) where TDef : IEntityDefinition { ... }
```

### 2.1 Direct slice access

`GetVariable<T>` returns the **effective** value (with all modifiers applied). For raw inspection — base values without fold, full modifier stacks, single modifiers by id — read the canonical slice directly:

```csharp
EntityVariableState state = ctx.Store.Get<EntityVariableState>(entityId);

// Base value only (no modifier fold). Falls back to definition default if missing.
VariableValue? baseAttack =
    state.BaseValues.TryGetValue(Vars.Attack, out var bv)
        ? bv
        : (definition.TryGetDefaultValue(Vars.Attack, out var dv) ? dv : null);

// Full modifier stack on a variable, ordered (ascending Modifier.Order, FIFO ties).
IReadOnlyList<ActiveModifier> attackMods =
    state.ModifierStacks.TryGetValue(Vars.Attack, out var stack)
        ? stack
        : Array.Empty<ActiveModifier>();

// Single modifier by id.
ActiveModifier? specific = attackMods.FirstOrDefault(m => m.Id.Equals(targetId));
```

`ActiveModifier` carries `Id` (`ModifierId`), `Modifier` (`VariableModifier`), and optional `Source` (`ModifierSource?`). Slice-direct reads are the right tool for debug inspectors, history-replay tooling, and projection layers that need raw state. Gameplay code prefers the effective-value reads on the handle.

---

## 3. Subscriptions

Three subscription channels on the entity, plus one lifecycle event.

### 3.1 Per-variable effective-value change

```csharp
IDisposable Subscribe(Variable key, Action<VariableValue> onChange);
void        Unsubscribe(Variable key, Action<VariableValue> onChange);
```

Fires whenever the effective value for `key` changes (after fold). The first callback fires synchronously with the current value when subscribing. Returns `IDisposable` that, when disposed, unregisters. Use this for "react to attack stat changes."

### 3.2 Variable structural changes

```csharp
IDisposable SubscribeToVariableStructuralChanges(
    Action<VariableStructuralChange, Variable, VariableValue?> handler);
```

Fires `Added` / `Removed` events when a variable enters or leaves the entity's effective key set. Coalesced over the atomic batch — a `Remove`-and-`Re-Add` of the same variable in one `ExecuteBatch` produces no event. Use this for "this entity now has a Stunned variable" / "the Stunned variable was removed."

### 3.3 Per-mutation slice events (uncoalesced)

For every `Add`/`Remove` event in a batch (not coalesced), subscribe to the slice directly:

```csharp
store.Subscribe<EntityVariableState>(entityId, (_, newState, ev) => {
    // ev: StateChangeEvent.Updated | StateChangeEvent.Removed
    // newState: the post-mutation slice
});
```

This is the lowest-level surface — engines, debuggers, and replay tools use it. Presentation usually wants the coalesced channels above instead.

### 3.4 Entity removal

```csharp
event Action OnEntityRemoved;   // on StateEntity<TDef>
```

Fires when the entity's canonical row is removed (via `Entities.Destroy(id)` or rollback past spawn). Subscribe when a long-lived consumer needs to react symmetrically to the spawn it bound against:

```csharp
entity.OnEntityRemoved += () => { /* tear down view, unbind, log, … */ };
```

The framework's per-variable and structural subscriptions are released automatically — manual `OnEntityRemoved` handling is only for callers that hold references beyond the entity itself (presentation views, telemetry).

### 3.5 Lifetime

`StateEntity.Dispose()` (called by `Entities.Destroy(id)`) tears down the underlying `StoreVariableStorage`, which clears every per-variable and structural subscription registered through the handle. Manual `Unsubscribe` / disposing the `IDisposable` from `Subscribe` is the correct discharge for shorter-lived listeners (e.g. presentation views unloading while the entity is still alive).

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

### 4.5 Batched mutations

Multiple payloads can commit atomically through `Store.ExecuteBatch`. Use this when several writes should be visible to subscribers as a single step:

```csharp
// Build modifier entries via the factory (deterministic ids).
var atkBuff = ctx.Modifiers.Create(Vars.Attack,  new IntAddModifier { Amount = 2 }, source);
var defBuff = ctx.Modifiers.Create(Vars.Defense, new IntAddModifier { Amount = 1 }, source);

ctx.Store.ExecuteBatch(new object[] {
    new AddModifierPayload(target.Id, atkBuff.Key, atkBuff.Modifier!, atkBuff.Id!.Value, atkBuff.Source),
    new AddModifierPayload(target.Id, defBuff.Key, defBuff.Modifier!, defBuff.Id!.Value, defBuff.Source),
    new IncrementTurnCounterPayload(),   // a game-authored payload writing a non-entity slice
});
```

The factory returns an `EntityModifierEntry` with `Id` and `Source` populated. When dropping to raw `AddModifierPayload` for batching, unpack those fields explicitly (the `!` operators are safe — factory output always sets them). Game authors who batch frequently can wrap this in a small helper.

Guarantees:

- **Atomic commit.** All registered mutators for every payload run, then the runner's overlay commits in one shot. No half-applied state is observable.
- **One slice update event per affected slice per batch.** `EntityVariableState`'s `StoreVariableStorage` rebuilds its caches once after the batch commits; per-variable subscribers fire once for changed values; structural subscribers see coalesced add/remove diffs (a remove-and-re-add of the same variable produces no event).
- **Mixed payload types are allowed.** The same batch can carry entity payloads (`AddModifierPayload`, `SetBaseValuePayload`, …), game-authored payloads, and framework payloads. Each payload's mutators target their own slices.
- **Reads inside the batch are not allowed.** `ExecuteBatch` runs the whole sequence before returning; there is no opportunity to read between payloads. Callers that need read-mutate-read sequences must split into separate `Execute` calls.
- **Ordering is the iteration order of the supplied collection.** The runner processes payloads in the order they're enumerated.

Use cases: applying multiple modifiers from a single ability in one observable step; restoring entity state during snapshot load; history-replay reconstruction; any sequence where intermediate state should not be visible to subscribers.

The `IMutableEntity` surface does not expose batch composition — its methods each call `Store.Execute` for a single payload. To batch entity mutations alongside non-entity ones, drop to raw payloads as shown above. The factory still applies: build modifier entries through `ctx.Modifiers.Create(...)` and then construct `AddModifierPayload` from the entry's fields.

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

## 8. Worked Examples

### 8.1 Equip / Unequip — single-handler mutation

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

### 8.2 Apply-aura — modifier built in effect, installed in handler

When a modifier comes from an effect orchestrator (target selection, pre-conditions, choosing parameters) but the install lands in a separate action's handler, the modifier entry travels through the action payload as a value. The factory call sits in the effect (so the deterministic `ModifierId` is allocated at the right point in the command stream); the handler just installs the pre-built entry.

```csharp
// Action payload — carries the pre-built modifier across the boundary.
public sealed record ApplyDamageAmpAction(
    InstanceId          Target,
    EntityModifierEntry Modifier) : GameAction;

public sealed record ApplyResult(InstanceId Target) : CommandResult;

// Effect — picks target, builds the buff, dispatches the install action.
public sealed class ApplyDamageAmpEffect : Effect
{
    public override async Task Execute(IEffectScope scope)
    {
        var pick = await scope.Dispatch<RequestTargetResult>(new RequestTargetAction
        {
            Filter = EnemyFilter.InRange(scope.SourceInstance, range: 3),
        });
        if (pick.Target is null) return;

        // Built in the effect — id is deterministic, source is this effect's host.
        var ampBuff = scope.Modifiers.Create(
            Vars.DamageDealt,
            new IntAddModifier { Amount = 2, Order = 200 },
            new ModifierSource(scope.SourceInstance));

        await scope.Dispatch<ApplyResult>(new ApplyDamageAmpAction
        {
            Target   = pick.Target.Value,
            Modifier = ampBuff,
        });
    }
}

// Handler — installs the pre-built entry. No id allocation here; the factory
// already produced one in the effect.
public sealed class ApplyDamageAmpHandler : ICommandHandler<ApplyDamageAmpAction, ApplyResult>
{
    public Task<ApplyResult> Handle(ApplyDamageAmpAction cmd, IHandlerContext ctx)
    {
        var enemy = ctx.Entities.Get<EnemyDefinition>(cmd.Target);
        enemy.AddModifier(cmd.Modifier);
        return Task.FromResult(new ApplyResult(cmd.Target));
    }
}
```

Why the factory call belongs in the effect, not the handler:

- **Determinism is per-decision-point, not per-handler-call.** The id is allocated at the moment the effect *decides* to install the buff. Re-running the same effect against the same state must produce the same id. If the handler called the factory, the id would shift if upstream effects added intervening modifiers.
- **The modifier is part of the action's identity.** It's serialized into the history envelope; debugger / replay tooling can render "ApplyDamageAmp targeted goblin#42 with buff M-7" as a complete record.
- **Handlers stay narrow.** They install what they're told, validate the target, and return. They don't make modifier-shape decisions.

Use this pattern whenever the buff parameters depend on effect-side state (target choice, source attribution, computed magnitude) but the install must be a discrete observable command (replay, network sync, history clarity). For self-contained "buff this entity right now" cases where there's no target picking or pre-computation, the equip/unequip pattern in §8.1 — single handler doing build + install — is simpler.

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
