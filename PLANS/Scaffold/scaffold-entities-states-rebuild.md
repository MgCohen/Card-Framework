---
type: plan
status: proposed
tags: [#scaffold, #entities-states, #bridge, #rebuild]
---

# Scaffold.Entities.States — bridge rebuild (substantive shape change, payload reshape, adapter shrink, factory consolidation)

> **Plan C** of the Scaffold update sequence. Substantive rebuild of `com.scaffold.entities.states`. Plan B already did the cross-cutting cleanup (rename `EntityVariableState` → `EntityState`, delete `EntityStateReference`, migrate `InstanceId` → `Reference` field types in payloads/`StateEntity`, reshape `ModifierSource`). Plan C does the actual logic rebuild: gut `EntityState` to pure data, replace the old 6 payloads/mutators with 7 (add `ClearModifiersPayload`), shrink `StoreVariableStorage` from ~422 LOC to ~50 LOC, delete `StateEntity<TDef>`, add `StoreEntities.Spawn<TDef>` extension, delete the old factory/context machinery.

**Source spike:** [`Product/spikes/entities-state-unification.md`](../../Product/spikes/entities-state-unification.md) — locked decisions #1, #4, #6, #7, #14, #15, #18; final shapes §3.

**Plan A status:** landed in [Scaffold#40](https://github.com/MgCohen/Scaffold/pull/40). Plan C uses `Ref<T>` (sealed record : `Reference`), `ICatalog` exposed via `Store.Catalog`, and the `Reference` polymorphic base.

**Plan B status:** assumed landed before this plan starts. Plan C inherits Plan B's outputs: `IEntityVariableStorage` with chain-walking reads (`TryGetBase` + `GetModifiers` + `Variables`, no `TryGetEffective`); identity-free `EntityInstance<TDef>` (no `SelfRef` on base); single `LocalVariableStorage` class; `ModifierSource(Reference Source, int Tag)`; `InstanceId` deleted; `EntityVariableState` renamed to `EntityState` (still old shape); `EntityStateReference` deleted; `IReadOnlyEntity` / `IMutableEntity` / `BaseEntityInstance` deleted.

---

## TL;DR

Rebuild `com.scaffold.entities.states` so that:

- `EntityState` becomes pure data: `(ImmutableDictionary<Variable, VariableValue> Bases, ImmutableDictionary<Variable, ImmutableList<ActiveModifier>> Modifiers)`. Drop the `Definition` reference (lives on the handle now). Drop the `Id` field (slice key carries identity).
- Add `EntityState.With...` functional update methods (8 of them, including `WithoutAllModifiers`).
- Add `EntityState` pure read methods that operate on the slice's own data (no chain, no def fallback): `TryGetBase`, `GetModifiers`, `Variables`.
- Replace the old 6 payloads with 7: `SetBaseValuePayload`, `AddModifierPayload`, `RemoveModifierPayload`, `AddVariablePayload`, `RemoveVariablePayload`, `ClearModifiersPayload` (**new**), `RemoveModifiersFromSourcePayload`. All target `Reference`. Each implements `IPayloadReference.GetReference() => Target`.
- Replace the old 6 mutators with 7 — each is a 1-line delegation to the corresponding `EntityState.With...` method. Group as one class or seven tiny ones; implementer's call.
- Replace the old 422 LOC `StoreVariableStorage` with a ~50 LOC thin adapter implementing the new `IEntityVariableStorage` contract. No caching, no per-variable subscription multiplexing, no structural-change diffing, no type-aware equality across value types. Just slice reads + payload dispatches.
- Delete `StateEntity<TDef>` (Plan B kept it field-migrated; Plan C kills the class).
- Add `StoreEntities.Spawn<TDef>(this Store store, TDef def)` extension method that returns `(EntityInstance<TDef> Instance, Ref<EntityInstance<TDef>> Ref)`. Uses two-step register (`AllocateRef` + `RegisterAt`) internally.
- Delete `EntityStateFactory`, `EntityBridgeContext`, `StoreInstanceIdExtensions`, `StateEntityOps`. Their responsibilities fold into the spawn helper or evaporate.

Net result: the bridge package shrinks from ~900 LOC to ~200 LOC.

---

## Scope

### In scope (this plan)

- `EntityState` shape change: pure `(Bases, Modifiers)` record.
- `EntityState` pure read methods: `TryGetBase`, `GetModifiers`, `Variables`.
- `EntityState` functional updates: 8 `With...` methods.
- 7 payloads (replace old 6, add `ClearModifiersPayload`). Each `IPayloadReference` with `Reference Target`.
- 7 mutators (or grouped class) — pure delegations to `EntityState.With...`.
- New `StoreVariableStorage` (~50 LOC). Thin adapter implementing the new `IEntityVariableStorage` contract.
- Delete `StateEntity<TDef>` entirely.
- `StoreEntities.Spawn<TDef>(Store, TDef)` extension. Returns `(handle, ref)` tuple.
- Delete `EntityStateFactory`, `EntityBridgeContext`, `StoreInstanceIdExtensions`, `StateEntityOps`.

### Out of scope (deferred to Plan D)

- Project-layer changes (`Card`, `Zone`, `Player`, project slices, `CardMapState`, `CardRegistry`).
- Project-specific spawn helpers (`Card.Spawn`).
- ADR-0005 amendment.
- Model doc cascade.
- Migration of project code that referenced now-deleted types (`StateEntity<TDef>`, old payloads, etc.).

---

## Hard requirements

1. **`EntityState` is `record (ImmutableDictionary<Variable, VariableValue> Bases, ImmutableDictionary<Variable, ImmutableList<ActiveModifier>> Modifiers) : State`.** No `Id`, no `Definition`. Pure data, wire-ready.
2. **`EntityState.Empty`** static property returns the all-empty record.
3. **`EntityState` pure read methods** (single-slice; no chain; no definition-fallback):
   - `bool TryGetBase(Variable key, out VariableValue value)` — lookup in `Bases` dict.
   - `IEnumerable<ActiveModifier> GetModifiers(Variable key)` — return `Modifiers[key]` sorted by `Order` (stable; FIFO on ties).
   - `IEnumerable<Variable> Variables { get; }` — union of `Bases.Keys` and `Modifiers.Keys`.
4. **`EntityState` functional updates** — 8 methods, each returns a new `EntityState`:
   - `WithBase(Variable, VariableValue)`
   - `WithoutBase(Variable)`
   - `WithVariable(Variable, VariableValue)` — adds to Bases if not present (no-op when already present).
   - `WithoutVariable(Variable)` — removes from Bases AND removes all modifiers for that key.
   - `WithModifier(Variable, ActiveModifier)` — appends to the variable's modifier list (FIFO; stable on `Order` ties).
   - `WithoutModifier(Variable, ModifierId)` — removes by id.
   - `WithoutAllModifiers()` — clears all modifier stacks.
   - `WithoutModifiersFromSource(ModifierSource)` — removes modifiers whose `Source == arg` across all variables.
5. **7 payloads, each `IPayloadReference` with `Reference Target`:**
   - `SetBaseValuePayload(Reference Target, Variable Var, VariableValue Value)`
   - `AddModifierPayload(Reference Target, Variable Var, ActiveModifier Mod)`
   - `RemoveModifierPayload(Reference Target, Variable Var, ModifierId ModId)`
   - `AddVariablePayload(Reference Target, Variable Var, VariableValue Init)`
   - `RemoveVariablePayload(Reference Target, Variable Var)`
   - `ClearModifiersPayload(Reference Target)` — **new**
   - `RemoveModifiersFromSourcePayload(Reference Target, ModifierSource Source)`
6. **Each payload implements `GetReference() => Target`.** Type-compatible because `Ref<T> : Reference`.
7. **7 mutators** (or 1 grouped class with 7 methods) — each is a 1-2 line delegation to the corresponding `EntityState.With...` method. ~60 LOC total.
8. **`StoreVariableStorage : IEntityVariableStorage` is a thin adapter (~50 LOC).** No caches, no per-variable subscription multiplexing, no structural-change diffing, no type-aware equality across value types, no lifecycle bookkeeping beyond the constructor and the optional `Dispose`. Reads delegate to `Slice.X`; writes dispatch payloads.
9. **`StoreVariableStorage.Parent` returns `null`.** Store-backed storages don't chain — overlays are constructed by wrapping a `StoreVariableStorage` in a `LocalVariableStorage` via the latter's `Parent` ctor arg.
10. **`StoreVariableStorage` constructor takes `(Store store, Reference target)`.** `target` is the slice key for this entity's `EntityState`.
11. **Delete `StateEntity<TDef>` entirely.** No transitional alias, no `[Obsolete]` shim. Project code that references it will compile-fail until Plan D migration.
12. **`StoreEntities.Spawn<TDef>(this Store store, TDef def) where TDef : IEntityDefinition`** returns `(EntityInstance<TDef> Instance, Ref<EntityInstance<TDef>> Ref)`. Uses Plan A's two-step register pattern: `AllocateRef` → construct handle with `StoreVariableStorage(store, r)` → `RegisterAt` → `AddState(r, EntityState.Empty)` → return tuple.
12b. **Delete `EntityStateFactory`, `EntityBridgeContext`, `StoreInstanceIdExtensions`, `StateEntityOps`.** Their responsibilities are absorbed by `StoreEntities.Spawn` or removed entirely.

---

## Hard avoids

1. **Do NOT keep the `Definition` reference inside `EntityState`.** Definition lives on the handle. The slice is pure data.
2. **Do NOT keep an `Id` field inside `EntityState`.** The slice key carries identity. Duplicating it inside the value invites bugs (key/value mismatch).
3. **Do NOT preserve any of the old `StoreVariableStorage` complexity** (per-variable subscription multiplexing, effective-value caches, structural-change diffing, type-aware equality across value types). All gone. Reads recompute every time.
4. **Do NOT add subscription methods to `StoreVariableStorage` or to the bridge surface.** Reactivity is the substrate's job — consumers use `store.Subscribe<EntityState>(ref, ...)` directly.
5. **Do NOT add caching back as part of the contract.** If a profiler later proves a hot path, a private cache inside `StoreVariableStorage` may be reintroduced — but never on `IEntityVariableStorage`.
6. **Do NOT keep `StateEntity<TDef>` as an alias or `[Obsolete]` wrapper.** Hard-break the type.
7. **Do NOT keep `EntityStateFactory` / `EntityBridgeContext` / `StoreInstanceIdExtensions` / `StateEntityOps`** as transitional helpers. Delete; consumers go through `StoreEntities.Spawn` or do nothing (the responsibilities evaporate).
8. **Do NOT modify `com.scaffold.states` or `com.scaffold.entities` in this plan.** Plans A and B own those.
9. **Do NOT touch project layer code** (`Card`, `Zone`, `Player`, slices, registries). Plan D does that.
10. **Do NOT introduce `EntityState` constructor variants beyond `Empty` and the record's positional ctor.** Functional updates go through `With...` methods.
11. **Do NOT make `StoreVariableStorage` aware of definition default-fallback.** That's the handle's orchestration. The adapter just reads/writes the slice.
12. **Do NOT synthesize `ClearModifiersPayload` from a batch of `RemoveModifierPayload`s.** It's first-class; the mutator clears `EntityState.Modifiers` in one transition.

---

## Deliverables — concrete shapes

### 1. `EntityState` — gutted to pure data

```csharp
public sealed record EntityState(
    ImmutableDictionary<Variable, VariableValue> Bases,
    ImmutableDictionary<Variable, ImmutableList<ActiveModifier>> Modifiers
) : State
{
    public static EntityState Empty
        => new(ImmutableDictionary<Variable, VariableValue>.Empty,
               ImmutableDictionary<Variable, ImmutableList<ActiveModifier>>.Empty);

    // Pure reads — single slice, no chain, no definition fallback.
    public bool TryGetBase(Variable key, out VariableValue value)
        => Bases.TryGetValue(key, out value);

    public IEnumerable<ActiveModifier> GetModifiers(Variable key)
        => Modifiers.TryGetValue(key, out var list)
           ? list.OrderBy(m => m.Order)   // stable; FIFO on ties via record's order in ImmutableList
           : Enumerable.Empty<ActiveModifier>();

    public IEnumerable<Variable> Variables
    {
        get
        {
            var keys = new HashSet<Variable>(Bases.Keys);
            keys.UnionWith(Modifiers.Keys);
            return keys;
        }
    }

    // Functional updates.
    public EntityState WithBase(Variable key, VariableValue value)
        => this with { Bases = Bases.SetItem(key, value) };

    public EntityState WithoutBase(Variable key)
        => this with { Bases = Bases.Remove(key) };

    public EntityState WithVariable(Variable key, VariableValue initial)
        => Bases.ContainsKey(key) ? this : this with { Bases = Bases.Add(key, initial) };

    public EntityState WithoutVariable(Variable key)
        => this with { Bases = Bases.Remove(key), Modifiers = Modifiers.Remove(key) };

    public EntityState WithModifier(Variable key, ActiveModifier mod)
    {
        var list = Modifiers.TryGetValue(key, out var existing) ? existing : ImmutableList<ActiveModifier>.Empty;
        return this with { Modifiers = Modifiers.SetItem(key, list.Add(mod)) };
    }

    public EntityState WithoutModifier(Variable key, ModifierId id)
    {
        if (!Modifiers.TryGetValue(key, out var list)) return this;
        var filtered = list.RemoveAll(m => m.Id == id);
        return this with { Modifiers = Modifiers.SetItem(key, filtered) };
    }

    public EntityState WithoutAllModifiers()
        => this with { Modifiers = ImmutableDictionary<Variable, ImmutableList<ActiveModifier>>.Empty };

    public EntityState WithoutModifiersFromSource(ModifierSource source)
    {
        var newMods = Modifiers;
        foreach (var (key, list) in Modifiers)
        {
            var filtered = list.RemoveAll(m => Equals(m.Source, source));
            if (filtered != list) newMods = newMods.SetItem(key, filtered);
        }
        return this with { Modifiers = newMods };
    }
}
```

**Notes:**
- All read/update methods operate on the slice's own data only. No chain (the chain is at `IEntityVariableStorage` level — `LocalVariableStorage`'s `Parent` walking). No definition fallback (the handle does that).
- `GetModifiers` sort: `OrderBy(m => m.Order)` is stable; ties fall back to `ImmutableList`'s element order, which preserves insertion order (FIFO). Combined, this matches the spike's locked decision #6 within a single slice.
- Functional updates use record `with` syntax. Each is one line; the whole record is ~60 LOC including the methods.

### 2. Payloads

```csharp
public sealed record SetBaseValuePayload         (Reference Target, Variable Var, VariableValue Value)  : IPayloadReference { public Reference GetReference() => Target; }
public sealed record AddModifierPayload          (Reference Target, Variable Var, ActiveModifier Mod)   : IPayloadReference { public Reference GetReference() => Target; }
public sealed record RemoveModifierPayload       (Reference Target, Variable Var, ModifierId ModId)     : IPayloadReference { public Reference GetReference() => Target; }
public sealed record AddVariablePayload          (Reference Target, Variable Var, VariableValue Init)   : IPayloadReference { public Reference GetReference() => Target; }
public sealed record RemoveVariablePayload       (Reference Target, Variable Var)                       : IPayloadReference { public Reference GetReference() => Target; }
public sealed record ClearModifiersPayload       (Reference Target)                                     : IPayloadReference { public Reference GetReference() => Target; }
public sealed record RemoveModifiersFromSourcePayload(Reference Target, ModifierSource Source)         : IPayloadReference { public Reference GetReference() => Target; }
```

**Notes:**
- 7 payloads. Plan B already migrated the field type from `InstanceId` to `Reference`; Plan C adds `ClearModifiersPayload` to round out the set.
- Each `GetReference() => Target` is a one-line implementation, type-compatible because `Ref<T> : Reference`.

### 3. Mutators

```csharp
// Option A: one grouped class with seven methods. Or option B: seven tiny mutator classes,
// one per payload. Implementer's call. Either way, each method is a 1-2 line delegation
// to the corresponding EntityState.With... method.

public sealed class EntityStateMutator
{
    public EntityState Apply(EntityState state, SetBaseValuePayload p)
        => state.WithBase(p.Var, p.Value);

    public EntityState Apply(EntityState state, AddModifierPayload p)
        => state.WithModifier(p.Var, p.Mod);

    public EntityState Apply(EntityState state, RemoveModifierPayload p)
        => state.WithoutModifier(p.Var, p.ModId);

    public EntityState Apply(EntityState state, AddVariablePayload p)
        => state.WithVariable(p.Var, p.Init);

    public EntityState Apply(EntityState state, RemoveVariablePayload p)
        => state.WithoutVariable(p.Var);

    public EntityState Apply(EntityState state, ClearModifiersPayload p)
        => state.WithoutAllModifiers();

    public EntityState Apply(EntityState state, RemoveModifiersFromSourcePayload p)
        => state.WithoutModifiersFromSource(p.Source);
}
```

**Notes:**
- ~60 LOC including class scaffolding.
- Mutator registration with the Store (`MutatorRegistry`) follows whatever convention `Scaffold.States` uses — usually one `Register<TPayload, TState>(Func<TState, TPayload, TState>)` call per payload at engine bootstrap. Confirm with Plan A's wiring.

### 4. `StoreVariableStorage` — thin adapter

```csharp
public sealed class StoreVariableStorage : IEntityVariableStorage
{
    private readonly Store store;
    private readonly Reference target;
    private EntityState Slice => store.Get<EntityState>(target);

    public StoreVariableStorage(Store store, Reference target)
    {
        this.store = store;
        this.target = target;
    }

    // No chain — Store-backed storages don't have parents. Overlays wrap them via LocalVariableStorage(parent: this).
    public IEntityVariableStorage? Parent => null;

    // Reads — delegate to the slice.
    public bool TryGetBase(Variable key, out VariableValue value)  => Slice.TryGetBase(key, out value);
    public IEnumerable<ActiveModifier> GetModifiers(Variable key)   => Slice.GetModifiers(key);
    public IEnumerable<Variable> Variables                           => Slice.Variables;

    // Writes — dispatch payloads.
    public bool AddVariable(Variable key, VariableValue initial)
    { store.Execute(new AddVariablePayload(target, key, initial)); return true; }

    public bool SetBaseValue(Variable key, VariableValue value)
    { store.Execute(new SetBaseValuePayload(target, key, value)); return true; }

    public ModifierId AddModifier(EntityModifierEntry entry)
    {
        var mid = entry.Id ?? ModifierId.New();
        store.Execute(new AddModifierPayload(target, entry.Key, new ActiveModifier(mid, entry.Modifier, entry.Source)));
        return mid;
    }

    public bool RemoveModifier(Variable key, ModifierId mid)
    { store.Execute(new RemoveModifierPayload(target, key, mid)); return true; }

    public bool RemoveVariable(Variable key)
    { store.Execute(new RemoveVariablePayload(target, key)); return true; }

    public void ClearModifiers()
    { store.Execute(new ClearModifiersPayload(target)); }

    public void RemoveModifiersFromSource(ModifierSource src)
    { store.Execute(new RemoveModifiersFromSourcePayload(target, src)); }
}
```

**Notes:**
- ~50 LOC. No caches, no events, no per-variable subscription multiplexing, no structural-change diffing, no type-aware equality, no `IDisposable` complexity (default no-op).
- Reads always re-fetch the slice via `store.Get<EntityState>(target)` — cheap, no caching.

### 5. Delete `StateEntity<TDef>`

Hard-delete. No alias, no `[Obsolete]`. Project code that referenced it will compile-fail until Plan D migrates `Card`, `Zone`, `Player` to extend `EntityInstance<TDef>` directly.

### 6. `StoreEntities.Spawn<TDef>` extension

```csharp
public static class StoreEntities
{
    public static (EntityInstance<TDef> Instance, Ref<EntityInstance<TDef>> Ref) Spawn<TDef>(this Store store, TDef def)
        where TDef : IEntityDefinition
    {
        var r = store.Catalog.AllocateRef<EntityInstance<TDef>>();
        var instance = new EntityInstance<TDef>(def, new StoreVariableStorage(store, r));
        store.Catalog.RegisterAt(r, instance);
        store.AddState(r, EntityState.Empty);
        return (instance, r);
    }
}
```

**Notes:**
- Returns a tuple because the base `EntityInstance<TDef>` no longer carries `SelfRef` (per Plan B). Project subclasses that DO have a typed SelfRef (e.g., `Card`, `Zone`) provide their own typed spawn helpers (`Card.Spawn`) — that's Plan D.
- Two-step register: `AllocateRef` first so `StoreVariableStorage` can be constructed with the ref, then `RegisterAt` once the handle exists.

### 7. Delete `EntityStateFactory`, `EntityBridgeContext`, `StoreInstanceIdExtensions`, `StateEntityOps`

Hard-delete. Their responsibilities:

- `EntityStateFactory` — replaced by `StoreEntities.Spawn`.
- `EntityBridgeContext` — replaced by direct `Store` access (the catalog is on `Store.Catalog`; no separate context needed).
- `StoreInstanceIdExtensions` — `InstanceId` is gone (Plan B); the extensions evaporate.
- `StateEntityOps` — replaced by direct `EntityInstance<TDef>` API + payload dispatch via `StoreVariableStorage`.

Any internal call sites within the bridge package that referenced these get rewired to the new shapes.

---

## Implementation order

1. **Reshape `EntityState`** to pure `(Bases, Modifiers)` — drop `Definition` and `Id` fields. Add the 8 `With...` methods and the 3 read methods.
2. **Add `ClearModifiersPayload`** + matching mutator method + `EntityState.WithoutAllModifiers`.
3. **Reshape the existing 6 payloads** to the spec'd record signatures (Plan B already migrated `InstanceId` → `Reference`; this step finalizes parameter names and `IPayloadReference.GetReference() => Target`).
4. **Replace the existing 6 mutators** with the 7-method `EntityStateMutator` (or seven tiny mutator classes). Wire registration with the Store at engine bootstrap.
5. **Replace `StoreVariableStorage`** with the new ~50 LOC thin adapter implementing the new `IEntityVariableStorage` contract from Plan B.
6. **Delete `StateEntity<TDef>`.** Hard-break.
7. **Add `StoreEntities.Spawn<TDef>`** extension.
8. **Delete `EntityStateFactory`, `EntityBridgeContext`, `StoreInstanceIdExtensions`, `StateEntityOps`.** Rewire any internal call sites.
9. **Build green.** Update in-package tests as needed.
10. **Validation pass.** Run unit + integration tests below.

Steps 1–4 can land in one PR (slice + payload + mutator changes). Steps 5–8 in a second PR (storage + factory consolidation). Step 9 verified within both PRs.

---

## Validation

### Unit tests (required)

| Test | Asserts |
|---|---|
| `EntityState_Empty_HasNoBasesNoModifiers` | `EntityState.Empty.Bases.Count == 0 && Empty.Modifiers.Count == 0` |
| `EntityState_NoIdField` | Reflection: `EntityState` has no property/field named `Id` (R: spike #7 — slice key carries identity) |
| `EntityState_NoDefinitionField` | Reflection: `EntityState` has no property/field of type `IEntityDefinition` or similar |
| `EntityState_WithBase_AddsToBasesAndReturnsNew` | `Empty.WithBase(k, v).Bases[k] == v` and `Empty.Bases.Count == 0` (immutability) |
| `EntityState_WithoutBase_RemovesEntry` | `state.WithBase(k, v).WithoutBase(k).Bases.ContainsKey(k) == false` |
| `EntityState_WithVariable_NoOpIfPresent` | `state.WithBase(k, v).WithVariable(k, otherV).Bases[k] == v` (no overwrite) |
| `EntityState_WithoutVariable_RemovesBaseAndModifiers` | `state.WithBase(k, v).WithModifier(k, m).WithoutVariable(k)` has no entry for `k` in either dict |
| `EntityState_WithModifier_AppendsFifo` | Two modifiers added at same `Order` → enumerated in insertion order |
| `EntityState_WithoutModifier_RemovesById` | `state.WithModifier(k, m).WithoutModifier(k, m.Id).Modifiers[k]` does not contain `m` |
| `EntityState_WithoutAllModifiers_ClearsEverything` | `state.WithModifier(...).WithoutAllModifiers().Modifiers.Count == 0` |
| `EntityState_WithoutModifiersFromSource_RemovesAcrossKeys` | Modifiers from same source on different variables → all removed |
| `EntityState_TryGetBase_FromBasesDict` | Returns true with the value when `Bases` contains the key |
| `EntityState_TryGetBase_FalseWhenAbsent` | Returns false when `Bases` does not contain the key |
| `EntityState_GetModifiers_SortedByOrder_StableOnTies` | Mixed `Order` values → ascending; ties → insertion order |
| `EntityState_Variables_UnionsBasesAndModifiers` | Returns union of `Bases.Keys` and `Modifiers.Keys` |
| `Payload_GetReference_ReturnsTarget` | For each of the 7 payloads: `payload.GetReference() == payload.Target` |
| `ClearModifiersPayload_TypePresent` | Reflection: `ClearModifiersPayload` exists and has `Reference Target` field (new in Plan C) |
| `Mutator_SetBaseValue_AppliesWithBase` | `mutator.Apply(empty, new SetBaseValuePayload(target, k, v))` returns `Empty.WithBase(k, v)` |
| `Mutator_AddModifier_AppliesWithModifier` | Same shape for `AddModifierPayload` |
| `Mutator_RemoveModifier_AppliesWithoutModifier` | Same |
| `Mutator_AddVariable_AppliesWithVariable` | Same |
| `Mutator_RemoveVariable_AppliesWithoutVariable` | Same |
| `Mutator_ClearModifiers_AppliesWithoutAllModifiers` | Same |
| `Mutator_RemoveModifiersFromSource_AppliesWithoutModifiersFromSource` | Same |
| `StoreVariableStorage_Parent_IsNull` | Construct one and assert `.Parent == null` |
| `StoreVariableStorage_TryGetBase_DelegatesToSlice` | Set up slice with a base via `AddState`/`SetBaseValuePayload`; storage returns it |
| `StoreVariableStorage_GetModifiers_DelegatesToSlice` | Same shape for modifiers |
| `StoreVariableStorage_Variables_DelegatesToSlice` | Same shape for variables enumeration |
| `StoreVariableStorage_AddVariable_DispatchesPayload` | Spy on Store; verify `AddVariablePayload(target, k, v)` was dispatched |
| `StoreVariableStorage_SetBaseValue_DispatchesPayload` | Same shape |
| `StoreVariableStorage_AddModifier_DispatchesPayload_ReturnsId` | Verify `AddModifierPayload` dispatched and returned `ModifierId` matches |
| `StoreVariableStorage_RemoveModifier_DispatchesPayload` | Same |
| `StoreVariableStorage_RemoveVariable_DispatchesPayload` | Same |
| `StoreVariableStorage_ClearModifiers_DispatchesPayload` | `ClearModifiersPayload(target)` dispatched (single payload, NOT a batch) |
| `StoreVariableStorage_RemoveModifiersFromSource_DispatchesPayload` | Same |
| `StoreVariableStorage_NoCachesNoSubscriptions` | Reflection: no fields of type `Dictionary<Variable, ...>` for caching, no subscription bookkeeping |
| `StateEntity_TypeNotPresent` | Reflection: `StateEntity<>` does not exist (deleted) |
| `EntityStateFactory_TypeNotPresent` | Reflection: `EntityStateFactory` does not exist |
| `EntityBridgeContext_TypeNotPresent` | Reflection: `EntityBridgeContext` does not exist |
| `StoreInstanceIdExtensions_TypeNotPresent` | Reflection: `StoreInstanceIdExtensions` does not exist |
| `StateEntityOps_TypeNotPresent` | Reflection: `StateEntityOps` does not exist |
| `StoreEntities_Spawn_AllocatesRefAndRegistersHandle` | After `var (e, r) = store.Spawn(def)`: `store.Catalog.Resolve(r) == e` |
| `StoreEntities_Spawn_AddsEmptyEntityState` | After spawn: `store.Get<EntityState>(r) == EntityState.Empty` |
| `StoreEntities_Spawn_HandleStorageIsStoreBacked` | Returned `e.Storage` is a `StoreVariableStorage` with `Parent == null` |
| `StoreEntities_Spawn_HandleHasNoSelfRef` | Returned `e` has no `SelfRef` member (base `EntityInstance<TDef>` per Plan B) |

### Integration tests (recommended)

- **End-to-end variable lifecycle.** Spawn an entity. Set a base via the handle. Add a modifier. Read effective value via the handle (orchestrated through the new chain reads + def fallback + modifier folding). Remove modifier. Clear modifiers. Remove variable.
- **Snapshot/rollback round-trip.** Spawn, set bases, add modifiers, snapshot. Modify after snapshot. Load snapshot. Verify slice contents match the snapshot moment; verify the `EntityInstance<TDef>` handle still resolves through the (untouched) catalog.
- **Overlay over Store-backed.** Spawn an entity (store-backed). Wrap its `Storage` in a `LocalVariableStorage(parent: card.Storage)`. Construct a second handle with the overlay storage. Verify reads chain correctly: own bases win; parent bases fall back; modifiers fold across both layers.
- **`ClearModifiersPayload` is atomic.** Add 5 modifiers across 3 variables. Dispatch `ClearModifiersPayload`. Verify `EntityState` rolls forward in one transition (single notification, not 5 separate ones — test depends on Store's batching semantics).

### Property tests (nice-to-have, not required)

- For all `(Variable k, ActiveModifier m1, ActiveModifier m2)`: `state.WithModifier(k, m1).WithModifier(k, m2).GetModifiers(k)` enumerates `[m1, m2]` in insertion order regardless of `m1.Order` vs `m2.Order` if equal.
- For all `EntityState`: `state.WithoutAllModifiers().GetModifiers(k).Count() == 0` for any `k`.

---

## Open / decide-during-impl

1. **Mutator class shape.** One grouped `EntityStateMutator` class with seven methods, vs. seven tiny mutator classes. Pick whichever matches Scaffold.States's existing mutator-registration pattern. Behavior is identical.
2. **`Mutator` registration timing.** Engine bootstrap registers the mutators with the Store's `MutatorRegistry`. Decide who owns this — the bridge package's static registration class, or an explicit call from the consumer. Lean: explicit consumer call (`store.RegisterEntityMutators()`) so consumers control timing and can choose not to register if they want a different mutation surface. Confirm against Plan A's wiring conventions.
3. **`StoreVariableStorage.Dispose()` semantics.** Default no-op (no listeners to release). If profiling later adds caching, `Dispose()` may need to release that. For Plan C: no-op.
4. **`ActiveModifier.Order` field assumption.** Plan B's open item #6 noted: "If `Order` doesn't exist on `ActiveModifier` today, add it." Plan C assumes it exists (Plan B added it if needed). If still missing, add as part of this plan.
5. **`ImmutableList` performance for modifier removal.** `RemoveAll` on `ImmutableList` is O(n). For typical card-game scale (single-digit modifiers per variable), fine. Revisit only if profiling demands.
6. **Concurrency.** Inherit whatever `Store` provides. No additional guarantees.
7. **Bridge package's own test suite update.** Existing tests against `StateEntity<TDef>`, the old `StoreVariableStorage`, the old factories, etc. all break. Update or replace as part of this plan.

---

## What "validate" means before moving to Plan D

Plan C is validated when:

- All "required" unit tests above pass.
- All "recommended" integration tests pass.
- The `com.scaffold.entities.states` package builds green.
- `StateEntity<TDef>`, `EntityStateFactory`, `EntityBridgeContext`, `StoreInstanceIdExtensions`, `StateEntityOps` are all gone from the package source tree.
- `StoreVariableStorage` is at or near ~50 LOC.
- A short retro pass on this plan: any hard requirement or hard avoid that was wrong gets noted in the spike's open items, and the spike is updated.

After validation passes, Plan D (Card Framework project integration) starts. Plan D's scope is the project layer — `Card`, `Zone`, `Player`, project slices, ADR amendment, model doc cascade.

---

## Out-of-band reminders

- Plan A and Plan B are landed before this. Plan C uses Plan A's `Ref<T>` / `Reference` / `ICatalog` and Plan B's new `IEntityVariableStorage` contract / `EntityInstance<TDef>` / `ModifierSource` shape.
- The project layer (`Card`, `Zone`, `Player`, `CardMapState`, `CardRegistry`) is touched in Plan D, not here. After Plan C, the Card Framework project will compile-fail against the new bridge (deleted `StateEntity<TDef>`, deleted factories). Plan D fixes that.
- In-package tests in `com.scaffold.entities.states` that reference deleted types must be updated as part of this plan. "Build green" is the exit criterion.
- The thin-adapter `StoreVariableStorage` deliberately drops every optimization the old 422 LOC version had (caching, subscription multiplexing, structural diffing). If a profiler later proves a hot path, re-add caching as a private impl detail — but never on the `IEntityVariableStorage` contract.
- Mutator registration with the Store happens at engine bootstrap. The bridge provides the mutators; the consumer wires them. Confirm the wiring pattern against Plan A's Store / `MutatorRegistry` conventions.
