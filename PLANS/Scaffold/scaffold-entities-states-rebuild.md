---
type: plan
status: proposed
tags: [#scaffold, #entities-states, #bridge, #cleanup]
---

# Scaffold.Entities.States — bridge cleanup (pure-data trim, ClearModifiers first-class, read methods, factory consolidation)

> **Plan C** of the Scaffold update sequence. Targeted cleanup of `com.scaffold.entities.states` after Plan B's implementation. Plan B landed far more than originally scoped — `EntityState` is already a `sealed record : State` with `With*` functional update methods, mutators already use `Mutator<TState, TPayload>` with immutable-return semantics, payloads are already `sealed record` with `Reference EntityRef`, and `StoreVariableStorage` is already a ~90 LOC thin adapter. Plan C finishes the job: trim `EntityState` to pure data (remove `ResolveEffectiveValues`), add `TryGetBase`/`GetModifiers`/`Variables` pure reads, add `WithoutAllModifiers` + `ClearModifiersPayload`, add `StoreEntities.Spawn`, consolidate factory/ops classes.

**Source spike:** [`Product/spikes/entities-state-unification.md`](../../Product/spikes/entities-state-unification.md) — locked decisions #1, #4, #6, #7, #14, #15, #18; final shapes §3.

**Plan A status:** landed in [Scaffold#40](https://github.com/MgCohen/Scaffold/pull/40). `Ref<T>` (sealed record : `Reference`), `ICatalog` exposed via `Store.Catalog`.

**Plan B status:** landed in [Scaffold#40](https://github.com/MgCohen/Scaffold/pull/40). Plans A + B shipped as a single PR.

---

## What Plan B actually shipped (verified against PR #40 HEAD, commit `250ec605`)

This section documents the starting state that Plan C inherits, verified against the actual source files.

### `EntityState` (already refactored)

`sealed record EntityState(IReadOnlyDictionary<Variable, VariableValue> BaseValues, IReadOnlyDictionary<Variable, IReadOnlyList<ActiveModifier>> ModifierStacks) : State`

Already has:
- `Empty` static property
- `WithModifier(Variable, ActiveModifier)` — insertion-sorted by `Order`
- `WithoutModifier(Variable, ModifierId)`
- `WithBaseValue(Variable, VariableValue)`
- `WithVariable(Variable, VariableValue)` — no-op if key exists
- `WithoutVariable(Variable)` — removes base AND modifiers for that key
- `WithoutModifiersFromSource(ModifierSource)` — sweep across all variables
- `ResolveEffectiveValues(IEntityDefinition)` — old effective-value computation (to be removed)

Does NOT yet have:
- `TryGetBase`, `GetModifiers`, `Variables` pure read methods
- `WithoutAllModifiers()`
- `WithoutBase(Variable)` (distinct from `WithoutVariable` which also removes modifiers)

Uses copy-on-write with mutable `Dictionary` internally (not `ImmutableDictionary`). This is snapshot-safe because mutators return new records via `this with { ... }`.

### Payloads (already `sealed record`)

- `AddModifierPayload(Reference EntityRef, Variable Variable, VariableModifier Modifier, ModifierId ModifierId, ModifierSource? Source)` — decomposes modifier parts
- `SetBaseValuePayload(Reference EntityRef, Variable Variable, VariableValue Value)`
- `RemoveModifierPayload(Reference EntityRef, Variable Variable, ModifierId ModifierId)`
- `AddEntityVariablePayload(Reference EntityRef, Variable Variable, VariableValue InitialValue)`
- `RemoveEntityVariablePayload(Reference EntityRef, Variable Variable)`
- `RemoveModifiersBySourcePayload(Reference EntityRef, ModifierSource Source)`

All implement `IPayloadReference` with `GetReference() => EntityRef`.

No `ClearModifiersPayload` yet — `StoreVariableStorage.ClearModifiers()` loops individual `RemoveModifierPayload` dispatches.

### Mutators (already `Mutator<TState, TPayload>`)

6 mutator classes, each `[Mutator] internal sealed class ... : Mutator<EntityState, TPayload>` with `Change()` returning new `EntityState` via the `With*` methods. Registered through `EntityBridgeContext.RegisterMutators(StoreBuilder)`.

Dispatched by `GeneratedMutatorDispatcher : IMutatorDispatcher` which routes via `store.ExecuteMutator(ref, mutator, payload)`.

### `StoreVariableStorage` (~90 LOC thin adapter)

Already implements `IEntityVariableStorage` with:
- `Parent => null`
- Reads delegate to `store.Get<EntityState>(entityRef)` fields
- Writes dispatch payloads
- `ClearModifiers()` loops individual removes (the one functional gap)

### Other surviving types

- `EntityStateFactory` — has `Create<TDef>(TDef def, Store store, Reference entityRef)` that registers empty slice + constructs entity
- `EntityBridgeContext` — has `RegisterMutators(StoreBuilder)` that wires all 6 mutators
- `StateEntityOps` — has `RemoveModifiersFromSource(Store, ModifierSource)` bulk sweep across ALL entities using `store.EnumerateAllPairs<EntityState>()` + `store.ExecuteBatch`
- `GeneratedMutatorDispatcher` — routes 6 payload types to mutators

### Already deleted by Plan B

`StateEntity<TDef>`, `EntityStateReference`, `StoreInstanceIdExtensions`, `EntityVariableState` (renamed to `EntityState`), `InstanceId`, `IReadOnlyEntity`, `IMutableEntity`, `BaseEntityInstance`, `IInstanceIdGenerator`, `IncrementingInstanceIdGenerator`, `EntityInstanceCreator`.

---

## TL;DR

Finish the `com.scaffold.entities.states` cleanup:

- Add `EntityState.TryGetBase`, `GetModifiers`, `Variables` pure read methods so `StoreVariableStorage` can delegate to them instead of reaching into dictionary fields directly.
- Add `EntityState.WithoutAllModifiers()` + `WithoutBase(Variable)`.
- Remove `EntityState.ResolveEffectiveValues(IEntityDefinition)` — handle does folding now.
- Add `ClearModifiersPayload` + matching `ClearModifiersMutator`. Wire into dispatcher.
- Update `StoreVariableStorage.ClearModifiers()` to dispatch `ClearModifiersPayload` (single atomic transition, not N individual removes).
- Update `StoreVariableStorage` reads to delegate to `EntityState`'s new pure read methods.
- Add `StoreEntities.Spawn<TDef>` extension method.
- Delete `EntityStateFactory` (replaced by `StoreEntities.Spawn`).
- Decide on `EntityBridgeContext` — keep, rename, or fold registration into `GeneratedMutatorDispatcher`.
- Decide on `StateEntityOps` — keep the bulk `RemoveModifiersFromSource` operation or move it.

---

## Scope

### In scope (this plan)

- `EntityState` additions: `TryGetBase`, `GetModifiers`, `Variables`, `WithoutAllModifiers`, `WithoutBase`.
- `EntityState` removal: `ResolveEffectiveValues`.
- `ClearModifiersPayload` + `ClearModifiersMutator` + dispatcher wiring.
- `StoreVariableStorage` update: delegate reads to `EntityState` methods, `ClearModifiers` dispatches single payload.
- `StoreEntities.Spawn<TDef>` extension.
- Delete `EntityStateFactory`.
- Decide on `EntityBridgeContext` fate (registration pathway must survive in some form).
- Decide on `StateEntityOps` fate (bulk operation must survive or be documented as removed).
- Update in-package tests.

### Out of scope (deferred to Plan D)

- Project-layer changes (`Card`, `Zone`, `Player`, project slices, `CardMapState`, `CardRegistry`).
- Project-specific spawn helpers (`Card.Spawn`).
- ADR-0005 amendment.
- Model doc cascade.
- Field renames (`BaseValues` → `Bases`, `ModifierStacks` → `Modifiers`, `EntityRef` → `Target`). These are cosmetic and can be done in Plan C or deferred — not load-bearing.
- `ImmutableDictionary` conversion. The current copy-on-write `IReadOnlyDictionary` backed by mutable `Dictionary` is snapshot-safe and performant at card-game scale. Convert only if profiling or structural-sharing needs arise.

---

## Hard requirements

1. **`EntityState` has `TryGetBase(Variable, out VariableValue)` pure read method.** Looks up in `BaseValues`. Returns false when absent.
2. **`EntityState` has `GetModifiers(Variable)` pure read method.** Returns `ModifierStacks[key]` already sorted by `Order` (insertion-order sort maintained by `WithModifier`). Returns empty when absent.
3. **`EntityState` has `Variables` pure read property.** Returns union of `BaseValues.Keys` and `ModifierStacks.Keys`.
4. **`EntityState` has `WithoutAllModifiers()`.** Returns `this with { ModifierStacks = empty }`.
5. **`EntityState` has `WithoutBase(Variable)`.** Removes from `BaseValues` only; does NOT remove modifiers (unlike `WithoutVariable` which removes both).
6. **`EntityState.ResolveEffectiveValues(IEntityDefinition)` is removed.** The handle does folding now; this method is dead weight and couples the slice to `IEntityDefinition`.
7. **`ClearModifiersPayload(Reference EntityRef)` exists as a first-class payload.** Implements `IPayloadReference`.
8. **`ClearModifiersMutator : Mutator<EntityState, ClearModifiersPayload>` exists.** Delegates to `EntityState.WithoutAllModifiers()`.
9. **`GeneratedMutatorDispatcher` routes `ClearModifiersPayload` to its mutator.** Same pattern as the other 6.
10. **`StoreVariableStorage.ClearModifiers()` dispatches a single `ClearModifiersPayload`.** NOT a loop of individual `RemoveModifierPayload`s. Single atomic transition.
11. **`StoreVariableStorage` reads delegate to `EntityState` methods.** `TryGetBase` → `Slice.TryGetBase(key, out value)`, `GetModifiers` → `Slice.GetModifiers(key)`, `Variables` → `Slice.Variables`. No longer reaches into `BaseValues`/`ModifierStacks` dictionaries directly.
12. **`StoreEntities.Spawn<TDef>(this Store store, TDef def)` extension exists.** Returns `(EntityInstance<TDef> Instance, Ref<EntityInstance<TDef>> Ref)`. Uses two-step register pattern: `AllocateRef` → construct handle with `StoreVariableStorage(store, r)` → `RegisterAt` → register empty `EntityState` slice → return tuple.
13. **`EntityStateFactory` is deleted.** Replaced by `StoreEntities.Spawn`.
14. **Mutator registration pathway survives.** Whether via `EntityBridgeContext`, a renamed `EntityStateMutators` static class, or folded into the dispatcher — consumers must have a way to register entity mutators with the `StoreBuilder`. The method signature stays `RegisterMutators(StoreBuilder)` or equivalent.

---

## Hard avoids

1. **Do NOT remove the `StateEntityOps.RemoveModifiersFromSource(Store, ModifierSource)` bulk operation without providing a replacement.** It sweeps all entities in the store and uses `store.ExecuteBatch`. If deleted, the capability is lost. Either keep the class (renamed if desired), move the method to `StoreEntities`, or document explicitly that consumers now handle this themselves.
2. **Do NOT change `EntityState`'s record field names (`BaseValues`, `ModifierStacks`) in this plan.** Renames are cosmetic and break all call sites (mutators, tests, storage adapter) for no functional benefit. If desired, do as a separate mechanical rename pass.
3. **Do NOT convert to `ImmutableDictionary` in this plan.** The current copy-on-write pattern is correct and performant. `ImmutableDictionary` is an optimization decision (structural sharing) that should be profiling-driven.
4. **Do NOT change payload field names (`EntityRef` → `Target`, `Variable` → `Var`, etc.) in this plan.** Same rationale as #2 — cosmetic rename, breaks all call sites, no functional gain.
5. **Do NOT change `AddModifierPayload`'s decomposed shape** `(Reference, Variable, VariableModifier, ModifierId, ModifierSource?)` to bundled `ActiveModifier`. The decomposed shape matches how `StoreVariableStorage.AddModifier` receives data from the `IEntityVariableStorage` contract and lets the mutator construct `ActiveModifier`. Changing it requires reshaping both the storage adapter and the mutator for no behavioral difference.
6. **Do NOT modify `com.scaffold.states` or `com.scaffold.entities` in this plan.** Plans A and B own those.
7. **Do NOT touch project layer code** (`Card`, `Zone`, `Player`, slices, registries). Plan D does that.
8. **Do NOT synthesize `ClearModifiersPayload` from a batch of `RemoveModifierPayload`s.** First-class; single state transition.

---

## Deliverables — concrete shapes

### 1. `EntityState` additions

```csharp
// Add to existing EntityState record:

public bool TryGetBase(Variable key, out VariableValue value)
{
    if (BaseValues.TryGetValue(key, out var bv) && bv != null)
    {
        value = bv;
        return true;
    }
    value = default!;
    return false;
}

public IEnumerable<ActiveModifier> GetModifiers(Variable key)
{
    if (ModifierStacks.TryGetValue(key, out var bucket) && bucket != null)
        return bucket;  // already insertion-sorted by Order via WithModifier
    return Array.Empty<ActiveModifier>();
}

public IEnumerable<Variable> Variables
{
    get
    {
        var keys = new HashSet<Variable>(BaseValues.Keys);
        foreach (var k in ModifierStacks.Keys) keys.Add(k);
        return keys;
    }
}

public EntityState WithoutBase(Variable key)
{
    if (!BaseValues.ContainsKey(key)) return this;
    var next = CreateMutableValues(BaseValues);
    next.Remove(key);
    return this with { BaseValues = next };
}

public EntityState WithoutAllModifiers()
    => this with { ModifierStacks = new Dictionary<Variable, IReadOnlyList<ActiveModifier>>() };
```

**Notes:**
- `GetModifiers` does NOT re-sort — `WithModifier` already inserts in `Order` position via `ComputeModifierInsertIndex`. The list is pre-sorted.
- `WithoutBase` is distinct from `WithoutVariable`: it removes the base value only, leaving modifiers intact. Use case: reset a variable's override while keeping active buffs.

### 2. `EntityState` removal

Delete `ResolveEffectiveValues(IEntityDefinition)` and all its helper methods:
- `ResolveEffectiveValues`
- `CollectKeysUnion`
- `PopulateEffectiveFromKeys`
- `TryAddFoldedEffective`
- `ResolveBaseValueForVariable`

These couple the slice to `IEntityDefinition` and compute effective values — the handle's job now.

### 3. `ClearModifiersPayload` + mutator

```csharp
// Payload
public sealed record ClearModifiersPayload(Reference EntityRef) : IPayloadReference
{
    public Reference GetReference() => EntityRef;
}

// Mutator
[Mutator]
internal sealed class ClearModifiersMutator : Mutator<EntityState, ClearModifiersPayload>
{
    public override EntityState Change(EntityState state, ClearModifiersPayload payload, IStateScope scope)
    {
        return state.WithoutAllModifiers();
    }
}
```

### 4. `GeneratedMutatorDispatcher` update

Add `ClearModifiersPayload` routing:

```csharp
// Add to TryDispatch:
if (payload is ClearModifiersPayload clearMods)
{
    store.ExecuteMutator(clearMods.EntityRef, clearModifiers, clearMods);
    return true;
}
```

And add field: `private readonly ClearModifiersMutator clearModifiers = new();`

### 5. `StoreVariableStorage` update

Two changes:

**a) Reads delegate to `EntityState` methods instead of reaching into dictionary fields:**

```csharp
// Before:
public bool TryGetBase(Variable key, out VariableValue value)
{
    if (Slice.BaseValues.TryGetValue(key, out var bv) && bv != null) { value = bv; return true; }
    value = default!; return false;
}

// After:
public bool TryGetBase(Variable key, out VariableValue value) => Slice.TryGetBase(key, out value);
public IEnumerable<ActiveModifier> GetModifiers(Variable key) => Slice.GetModifiers(key);
public IEnumerable<Variable> Variables => Slice.Variables;
```

**b) `ClearModifiers` dispatches single payload:**

```csharp
// Before (loops N individual removes):
public void ClearModifiers()
{
    var s = Slice;
    foreach (var kv in s.ModifierStacks)
        foreach (var mod in kv.Value)
            store.Execute(new RemoveModifierPayload(entityRef, kv.Key, mod.Id));
}

// After (single atomic dispatch):
public void ClearModifiers()
{
    store.Execute(new ClearModifiersPayload(entityRef));
}
```

### 6. `StoreEntities.Spawn<TDef>` extension

```csharp
public static class StoreEntities
{
    public static (EntityInstance<TDef> Instance, Ref<EntityInstance<TDef>> Ref) Spawn<TDef>(this Store store, TDef def)
        where TDef : IEntityDefinition
    {
        var r = store.Catalog.AllocateRef<EntityInstance<TDef>>();
        var storage = new StoreVariableStorage(store, r);
        store.RegisterSlice(r, EntityState.Empty);
        var instance = new EntityInstance<TDef>(def, storage);
        store.Catalog.RegisterAt(r, instance);
        return (instance, r);
    }
}
```

**Notes:**
- Mirrors `EntityStateFactory.Create` but uses two-step register and returns the ref.
- Slice is registered before the entity is constructed (consistent with `EntityStateFactory.Create`'s current behavior).
- Returns tuple because base `EntityInstance<TDef>` has no `SelfRef`. Typed subclass spawn helpers (`Card.Spawn`) are Plan D.

### 7. Delete `EntityStateFactory`

Hard-delete. `StoreEntities.Spawn` replaces it. Internal call sites (e.g., in tests) migrate to `StoreEntities.Spawn` or direct construction.

### 8. `EntityBridgeContext` / `StateEntityOps` decisions

**`EntityBridgeContext`:** Rename to `EntityStateMutators` (or keep as-is). The `RegisterMutators(StoreBuilder)` method stays — it's the registration pathway. Add `ClearModifiersMutator` to the registration list. The name change is optional but signals the narrower role (just mutator registration, not a "context").

**`StateEntityOps`:** Keep the `RemoveModifiersFromSource(Store, ModifierSource)` bulk method. It's useful and has no replacement. Move it to `StoreEntities` if consolidating, or keep on `StateEntityOps` — implementer's call. The key requirement is that the capability survives.

---

## Implementation order

1. **Add `EntityState` pure read methods** (`TryGetBase`, `GetModifiers`, `Variables`) + `WithoutAllModifiers` + `WithoutBase`.
2. **Remove `ResolveEffectiveValues`** and its helpers from `EntityState`.
3. **Add `ClearModifiersPayload`** + `ClearModifiersMutator`.
4. **Wire `ClearModifiersPayload`** into `GeneratedMutatorDispatcher`.
5. **Register `ClearModifiersMutator`** in `EntityBridgeContext.RegisterMutators`.
6. **Update `StoreVariableStorage`**: reads → delegate to `EntityState` methods; `ClearModifiers` → single `ClearModifiersPayload` dispatch.
7. **Add `StoreEntities.Spawn<TDef>`** extension.
8. **Delete `EntityStateFactory`.** Migrate call sites (tests, etc.) to `StoreEntities.Spawn` or direct construction.
9. **Move `StateEntityOps.RemoveModifiersFromSource`** to `StoreEntities` (or keep on `StateEntityOps` — implementer's call). If moved, delete `StateEntityOps`.
10. **Update in-package tests.** Migrate factory calls, add tests for new methods.
11. **Build green + validation pass.**

This is one PR. The diff is small: ~15 LOC added to `EntityState`, ~30 LOC removed (`ResolveEffectiveValues` + helpers), 1 new payload + 1 new mutator (~15 LOC), dispatcher update (~5 LOC), 3-line `StoreVariableStorage` simplification, ~10 LOC `StoreEntities.Spawn`, 1 file deletion. Total net: roughly break-even on LOC.

---

## Validation

### Unit tests (required)

| Test | Asserts |
|---|---|
| `EntityState_TryGetBase_ReturnsValue` | `state.WithBaseValue(k, v).TryGetBase(k, out val)` returns true, `val == v` |
| `EntityState_TryGetBase_FalseWhenAbsent` | `EntityState.Empty.TryGetBase(k, out _)` returns false |
| `EntityState_GetModifiers_ReturnsPreSorted` | Modifiers added at mixed Orders come back sorted; ties in insertion order |
| `EntityState_GetModifiers_EmptyWhenAbsent` | Returns empty enumerable, not null |
| `EntityState_Variables_UnionsBasesAndModifiers` | Variable in `BaseValues` only, variable in `ModifierStacks` only, variable in both → all present in `Variables` |
| `EntityState_WithoutAllModifiers_ClearsEverything` | `state.WithModifier(a, m1).WithModifier(b, m2).WithoutAllModifiers().ModifierStacks` is empty |
| `EntityState_WithoutAllModifiers_PreservesBases` | Bases are unchanged after `WithoutAllModifiers` |
| `EntityState_WithoutBase_RemovesBaseOnly` | Removes from `BaseValues`; modifiers for that key are untouched |
| `EntityState_WithoutBase_NoOpWhenAbsent` | Returns same instance (reference equality) when key not in `BaseValues` |
| `EntityState_ResolveEffectiveValues_Removed` | Reflection: `EntityState` has no `ResolveEffectiveValues` method |
| `ClearModifiersPayload_Exists` | Reflection: type exists with `Reference EntityRef` property |
| `ClearModifiersPayload_GetReference` | `new ClearModifiersPayload(ref).GetReference() == ref` |
| `ClearModifiersMutator_DelegatesToWithoutAllModifiers` | `mutator.Change(state, payload, scope)` returns `state.WithoutAllModifiers()` |
| `Dispatcher_RoutesClearModifiersPayload` | `store.Execute(new ClearModifiersPayload(ref))` clears all modifiers on that entity's slice |
| `StoreVariableStorage_ClearModifiers_SingleDispatch` | After calling `ClearModifiers()`, a single `ClearModifiersPayload` was dispatched (not N individual `RemoveModifierPayload`s). Can be verified by checking that the result is equivalent to `WithoutAllModifiers`. |
| `StoreVariableStorage_TryGetBase_DelegatesToSliceMethod` | Behavioral: matches `EntityState.TryGetBase` result |
| `StoreVariableStorage_GetModifiers_DelegatesToSliceMethod` | Behavioral: matches `EntityState.GetModifiers` result |
| `StoreVariableStorage_Variables_DelegatesToSliceMethod` | Behavioral: matches `EntityState.Variables` result |
| `StoreEntities_Spawn_ReturnsHandleAndRef` | `var (e, r) = store.Spawn(def); e != null && r != default` |
| `StoreEntities_Spawn_RegistersInCatalog` | `store.Catalog.Resolve(r) == e` |
| `StoreEntities_Spawn_RegistersEmptySlice` | `store.Get<EntityState>(r) == EntityState.Empty` |
| `StoreEntities_Spawn_StorageIsStoreBacked` | `e.Storage is StoreVariableStorage` with `Parent == null` |
| `EntityStateFactory_TypeNotPresent` | Reflection: `EntityStateFactory` does not exist (deleted) |

### Integration tests (recommended)

- **End-to-end variable lifecycle via `StoreEntities.Spawn`.** Spawn entity. Set base via handle. Add modifier. Read effective value (storage → definition fallback → modifier fold). Remove modifier. Clear modifiers. Remove variable. All reads return expected values at each step.
- **Snapshot round-trip.** Spawn, set bases + modifiers, snapshot, mutate, `LoadSnapshot`. Verify rolled-back slice contents. Verify handle still resolves through catalog.
- **`ClearModifiers` atomicity.** Add 5 modifiers across 3 variables. Subscribe to `EntityState` on that ref. Dispatch `ClearModifiers`. Verify subscriber fires once (not 5 times).
- **Overlay over Store-backed.** Spawn entity. Wrap `Storage` in `LocalVariableStorage(parent: entity.Storage)`. Reads chain correctly: overlay base wins, parent base falls back, modifiers fold across both layers.

---

## Open / decide-during-impl

1. **`EntityBridgeContext` fate.** Keep as-is, rename to `EntityStateMutators`, or fold into `GeneratedMutatorDispatcher`. The registration method must survive in some form. Lean: keep as-is — it works, the name is fine, changing it is churn.
2. **`StateEntityOps` fate.** Keep as standalone class, or move `RemoveModifiersFromSource` to `StoreEntities`. Lean: move to `StoreEntities` and delete `StateEntityOps` — consolidates the bridge's public API surface into one class.
3. **`GetModifiers` re-sort.** Current `WithModifier` already inserts in sorted position. `GetModifiers` can return the list directly without `OrderBy`. But `StoreVariableStorage.GetModifiers` currently does `.OrderBy(m => m.Modifier.Order)`. After Plan C, `StoreVariableStorage` delegates to `EntityState.GetModifiers` which should NOT re-sort if the list is pre-sorted. Verify the insertion-sort in `WithModifier` is stable for equal-Order ties and document accordingly.
4. **`ModifierSource?` nullability.** `AddModifierPayload.Source` is `ModifierSource?` (nullable). `ActiveModifier.Source` is presumably also nullable. The `WithoutModifiersFromSource` sweep checks `am.Source.HasValue && am.Source.Value.Equals(source)`. Plan C preserves this — no changes to the nullable semantics.

---

## What "validate" means before moving to Plan D

Plan C is validated when:

- All "required" unit tests above pass.
- All "recommended" integration tests pass.
- `com.scaffold.entities.states` builds green.
- `EntityStateFactory` is deleted.
- `EntityState` has no `ResolveEffectiveValues` method.
- `ClearModifiersPayload` + `ClearModifiersMutator` exist and are wired.
- `StoreVariableStorage.ClearModifiers()` dispatches a single `ClearModifiersPayload`.
- `StoreVariableStorage` reads delegate to `EntityState` methods.
- `StoreEntities.Spawn<TDef>` exists and works.
- The bulk `RemoveModifiersFromSource(Store, ModifierSource)` operation is preserved (on `StoreEntities` or `StateEntityOps`).
- A short retro pass: any hard requirement or hard avoid that was wrong gets noted.

After validation, Plan D (Card Framework project integration) starts. Plan D's scope: `Card`/`Zone`/`Player` on the new foundation, `CardMapState`, `CardRegistry`, ADR amendment, model doc cascade.

---

## Out-of-band reminders

- Plans A and B are already landed as one PR ([Scaffold#40](https://github.com/MgCohen/Scaffold/pull/40)). Plan C uses Plan A's `Ref<T>` / `Reference` / `ICatalog` and Plan B's `IEntityVariableStorage` contract / `EntityInstance<TDef>` / `ModifierSource` shape.
- The project layer (`Card`, `Zone`, `Player`, `CardMapState`, `CardRegistry`) is touched in Plan D, not here.
- In-package tests must be updated as part of this plan. "Build green" is the exit criterion.
- Field renames (`BaseValues` → `Bases`, `ModifierStacks` → `Modifiers`, `EntityRef` → `Target`) and `ImmutableDictionary` conversion are explicitly deferred. They are cosmetic/optimization decisions that can be bundled into a separate mechanical PR if desired — they're not load-bearing for Plan D.
- The current `EntityState` uses copy-on-write with mutable `Dictionary` internally. This is correct — each `With*` method creates a new `Dictionary` copy, mutates the copy, and returns a new record. The Store holds the old record reference in snapshots; new mutations produce new records.
