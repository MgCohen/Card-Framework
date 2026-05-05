---
type: plan
status: proposed
tags: [#scaffold, #entities, #refactor]
---

# Scaffold.Entities — refactor (no subscriptions, no `IReadOnly`/`IMutable` tier, identity-free base, chain-walking storage)

> **Plan B** of the Scaffold update sequence. Refactors `com.scaffold.entities`. Mostly *removals* and contract trimming, with one substantive shape change to `IEntityVariableStorage` (chain-walking via `Parent`). Plan B is technically independent of Plan A — neither references types from the other — but we land them sequentially: A first, then B, then the bridge (Plan C) which depends on both.

**Source spike:** [`Product/spikes/entities-state-unification.md`](../../Product/spikes/entities-state-unification.md) — locked decisions #1, #2, #3, #5, #6, #7, #11; final shapes §2.

---

## TL;DR

Refactor `com.scaffold.entities` so that:

- `IEntityVariableStorage` has a `Parent` reference, exposes chain-walking reads (`TryGetBase`, `GetModifiers`, `Variables`), keeps writes as local-only operations, and has **no** subscription methods.
- The `IReadOnlyEntity` / `IMutableEntity` interface tier is deleted. `EntityInstance<TDef>` is the single class.
- `EntityInstance<TDef>` no longer carries any identity (no `Id`, no `SelfRef`, no `IRef`). It pairs `(Definition, Storage)`. Identity is a registration concern handled by subclasses or by the catalog (Plan A / Plan C).
- `EntityInstance<TDef>` orchestrates reads: storage chain (base lookup) → definition default-fallback → modifier folding across the full chain, sorted by `Order` with stable tie-breaking by chain order.
- `LocalVariableStorage` becomes a single class that handles both the base case (`Parent = null`) and the overlay case (`Parent = some other storage`) via the same code path.
- `EntityDefinition` stays an authored asset. It does **not** implement any variable-source / storage interface. The handle consults it directly for default-value fallback.
- `Entities.Local<TDef>(def)` factory provides one-liner construction for the no-Store profile.

Hard "removals only / no new dependencies" plan. No types from `com.scaffold.states` are referenced. The bridge (Plan C) wires this to the catalog later.

---

## Scope

### In scope (this plan)
- `IEntityVariableStorage` interface refactor.
- Delete `IReadOnlyEntity` and `IMutableEntity` interfaces.
- `EntityInstance<TDef>` refactor (constructor signature, removed identity, read orchestration, write delegation).
- `LocalVariableStorage` refactor (Parent support, base+overlay in one class, no subscription bookkeeping).
- `EntityDefinition` audit — ensure it does not implement any source/storage interface.
- `Entities.Local<TDef>(def)` factory.

### Out of scope (deferred)
- Anything in `com.scaffold.states` (Plan A) or `com.scaffold.entities.states` (Plan C).
- Per-instance reactivity / change events (substrate-specific; not part of the contract).
- Project-layer changes to `Card`, `Zone`, `Player`, slices, registries.
- Cross-package migration of existing call sites to the new contract.
- Existing `IEntityDefinition.TryGetDefaultValue` / `DeclaredVariables` — unchanged, keep as-is.

---

## Hard requirements

1. **`IEntityVariableStorage.Parent`** is exposed and may be null. Null = base storage. Non-null = overlay layered on top of another storage.
2. **`IEntityVariableStorage.TryGetBase(Variable, out VariableValue)` walks the chain.** Returns the first base value found in self → parent → ... → null. Returns false if no base exists anywhere in the chain.
3. **`IEntityVariableStorage.GetModifiers(Variable)` returns modifiers across the entire chain, sorted by `Order` (stable).** Tie-breaking rules: chain order (self first, then parent, recursively); within a single storage, FIFO by insertion order — consistent with the existing single-storage semantics.
4. **`IEntityVariableStorage.Variables`** returns the union of variables across the chain (own bases ∪ own modifiers ∪ parent's `Variables`).
5. **No subscription methods on `IEntityVariableStorage`.** Remove `Subscribe(Variable, …)`, `SubscribeToVariableStructuralChanges(...)`, and any other reactivity-shaped methods. Reactivity is a substrate concern; storages MAY expose their own events as implementation detail (not part of the contract).
6. **`IReadOnlyEntity` and `IMutableEntity` are deleted.** Any references in the package compile-fail and must be removed. Polymorphism over entity handles is by `EntityInstance<TDef>` directly (or, when Plan A is in place, by `IRef` for addressable use cases).
7. **`EntityInstance<TDef>` has no identity field.** No `Id`, no `SelfRef`, no `IRef`. Constructor takes `(TDef def, IEntityVariableStorage storage)` — exactly two parameters.
8. **`EntityInstance<TDef>.TryGetVariable<T>(Variable, out T)` orchestrates the read** in this exact order:
   a. Try `Storage.TryGetBase(key, out anchor)`.
   b. If false, try `Definition.TryGetDefaultValue(key, out anchor)`.
   c. If neither produces an anchor, return false (value = default).
   d. Otherwise iterate `Storage.GetModifiers(key)` and apply each in sequence.
   e. Return true with the resulting value.
9. **`EntityInstance<TDef>.Variables`** returns the union of `Storage.Variables` and `Definition.DeclaredVariables`.
10. **Writes on `EntityInstance<TDef>` are pure delegation to `Storage`.** No additional logic, no events, no caching.
11. **`LocalVariableStorage` is a single class** that takes an optional `IEntityVariableStorage? parent = null` constructor argument. The same class handles base and overlay. No separate `OverlayStorage` type.
12. **`LocalVariableStorage` writes always land locally.** Parent storage is never mutated through `self`.
13. **`EntityDefinition` does not implement any source/storage interface.** Keep `IEntityDefinition` (unchanged: `TryGetDefaultValue`, `DeclaredVariables`, etc.). Remove any `IVariableSource`-shaped declarations if present.
14. **`Entities.Local<TDef>(def)` factory** returns `EntityInstance<TDef>` constructed with a fresh `LocalVariableStorage()` (no parent). One-liner.
15. **Modifier-folding determinism is testable.** A unit test must verify that same-Order ties resolve in the documented order (chain-first, then insertion-order).

---

## Hard avoids

1. **Do NOT add subscription methods anywhere on the entity contract.** Not on `IEntityVariableStorage`, not on `EntityInstance<TDef>`. No `Changed` events. No `Subscribe`.
2. **Do NOT put `EntityDefinition` (or `IEntityDefinition`) in the storage chain.** Definition default-fallback is the handle's job, not a chain link. The chain is storage-to-storage only.
3. **Do NOT add `SelfRef`, `Id`, or any identity field to `EntityInstance<TDef>`.** Base class is `(Definition, Storage)`. Period.
4. **Do NOT keep `IReadOnlyEntity` or `IMutableEntity`.** They go.
5. **Do NOT introduce a separate `OverlayStorage` class.** Overlay is `LocalVariableStorage(parent: somethingElse)`. One class.
6. **Do NOT introduce `TryGetEffective` on storage.** The combined "base + modifier folding" lives on the handle (`EntityInstance<TDef>.TryGetVariable`), so the storage's split surface (`TryGetBase` + `GetModifiers`) lets the handle slot in the definition default-fallback at the right point.
7. **Do NOT couple `EntityDefinition` to `IEntityVariableStorage` or any variable-source interface.** Definitions are authored assets; storages are runtime data containers. Different things.
8. **Do NOT modify `com.scaffold.states` or `com.scaffold.entities.states` in this plan.** Plan A and Plan C own those.
9. **Do NOT add caches or precomputed effective-value tables to `EntityInstance<TDef>` or to storages.** Reads recompute from primitives. If a profiler later proves caching is needed, that's a separate plan.
10. **Do NOT rename `IEntityDefinition.TryGetDefaultValue` or `DeclaredVariables`.** They stay as-is.
11. **Do NOT add reflection-heavy machinery for type-aware equality across modifier value types.** Modifiers compose via their own `Apply` method; equality is the value type's own concern.

---

## Deliverables — concrete shapes

### 1. `IEntityVariableStorage` (refactored)

```csharp
public interface IEntityVariableStorage
{
    IEntityVariableStorage? Parent { get; }   // null at base; another storage for overlays

    // Reads — chain-walking. Definition default-fallback is the handle's job, not the chain's.
    bool TryGetBase(Variable key, out VariableValue value);              // walks self → parent → ... → null; returns first base
    IEnumerable<ActiveModifier> GetModifiers(Variable key);              // walks chain; sorted by Order; stable on ties (chain-first, then insertion)
    IEnumerable<Variable> Variables { get; }                              // union across the chain

    // Writes — local only. Parent is never mutated through self.
    bool AddVariable(Variable key, VariableValue initial);
    bool RemoveVariable(Variable key);
    bool SetBaseValue(Variable key, VariableValue value);
    ModifierId AddModifier(EntityModifierEntry entry);
    bool RemoveModifier(Variable key, ModifierId id);
    void ClearModifiers();
    void RemoveModifiersFromSource(ModifierSource source);
}
```

**Notes on tie-breaking:** the spec is enforced by the impl, not the consumer. `LocalVariableStorage` builds its `GetModifiers` walk such that same-Order modifiers within a single storage stay in insertion order (FIFO), and across the chain, self's modifiers come before parent's. C#'s `OrderBy` is stable, so `selfMods.Concat(parentMods.GetModifiers(key)).OrderBy(m => m.Order)` already produces the correct ordering — assuming `selfMods` itself is FIFO-ordered.

### 2. Delete `IReadOnlyEntity` and `IMutableEntity`

These interfaces are removed entirely. Any internal type or method that referenced them is updated to use `EntityInstance<TDef>` (or `IEntityDefinition` / `IEntityVariableStorage` for the underlying capabilities). External consumers (the bridge, the project) will be migrated in Plan C and beyond.

If any other interface in the package transitively references these (e.g., a base interface like `IEntity`), audit and either remove or keep based on whether it earns its keep without `IReadOnlyEntity`/`IMutableEntity`.

### 3. `EntityInstance<TDef>` (refactored)

```csharp
public class EntityInstance<TDef> : IDisposable
    where TDef : IEntityDefinition
{
    public TDef Definition { get; }
    public IEntityVariableStorage Storage { get; }

    public EntityInstance(TDef def, IEntityVariableStorage storage)
    {
        Definition = def;
        Storage = storage;
    }

    // Reads — orchestrate storage chain + definition default-fallback + modifier folding.
    public bool TryGetVariable<T>(Variable key, out T value)
    {
        bool hasAnchor =
            Storage.TryGetBase(key, out var anchor)
            || Definition.TryGetDefaultValue(key, out anchor);

        if (!hasAnchor) { value = default!; return false; }

        foreach (var mod in Storage.GetModifiers(key))
            anchor = mod.Apply(anchor);

        value = anchor.As<T>();
        return true;
    }

    public T GetVariable<T>(Variable key)
        => TryGetVariable<T>(key, out var v)
           ? v
           : throw new KeyNotFoundException(key.ToString());

    public IEnumerable<Variable> Variables
        => Storage.Variables.Union(Definition.DeclaredVariables);

    // Writes — pure delegation.
    public bool AddVariable(Variable key, VariableValue initial)        => Storage.AddVariable(key, initial);
    public bool RemoveVariable(Variable key)                             => Storage.RemoveVariable(key);
    public bool SetBaseValue(Variable key, VariableValue value)          => Storage.SetBaseValue(key, value);
    public ModifierId AddModifier(EntityModifierEntry entry)             => Storage.AddModifier(entry);
    public bool RemoveModifier(Variable key, ModifierId id)              => Storage.RemoveModifier(key, id);
    public void ClearModifiers()                                          => Storage.ClearModifiers();
    public void RemoveModifiersFromSource(ModifierSource source)         => Storage.RemoveModifiersFromSource(source);

    public virtual void Dispose() { /* base no-op; bridge subclass releases listeners if any */ }
}
```

**Notes:**
- `IDisposable` stays on the base for subclass extension (e.g., the Plan C bridge handle, if it ends up holding listener resources). Default impl is a no-op.
- `Definition` and `Storage` are public for sugar-accessor patterns and for direct inspection from tooling.
- `class` (not `sealed`) so subclasses (`Card`, `Zone`, etc.) can extend.
- No `Id`, no `SelfRef`. Those are subclass concerns when applicable.

### 4. `LocalVariableStorage` (refactored)

```csharp
public sealed class LocalVariableStorage : IEntityVariableStorage
{
    public IEntityVariableStorage? Parent { get; }

    private readonly Dictionary<Variable, VariableValue> bases = new();
    private readonly Dictionary<Variable, List<ActiveModifier>> modifiers = new();   // FIFO insertion order

    public LocalVariableStorage(IEntityVariableStorage? parent = null) { Parent = parent; }

    // Reads
    public bool TryGetBase(Variable key, out VariableValue value)
    {
        if (bases.TryGetValue(key, out value)) return true;
        if (Parent is not null) return Parent.TryGetBase(key, out value);
        value = default;
        return false;
    }

    public IEnumerable<ActiveModifier> GetModifiers(Variable key)
    {
        var local = modifiers.TryGetValue(key, out var list) ? (IEnumerable<ActiveModifier>)list : Array.Empty<ActiveModifier>();
        var parentMods = Parent?.GetModifiers(key) ?? Enumerable.Empty<ActiveModifier>();
        // Self-first concatenation, stable sort by Order. C# OrderBy is stable; ties retain
        // the source-enumeration order (self's FIFO first, then parent's chain-first ordering).
        return local.Concat(parentMods).OrderBy(m => m.Order);
    }

    public IEnumerable<Variable> Variables
    {
        get
        {
            var keys = new HashSet<Variable>(bases.Keys);
            keys.UnionWith(modifiers.Keys);
            if (Parent is not null) keys.UnionWith(Parent.Variables);
            return keys;
        }
    }

    // Writes — local only. Parent untouched.
    public bool AddVariable(Variable key, VariableValue initial)
    {
        if (bases.ContainsKey(key)) return false;
        bases[key] = initial;
        return true;
    }

    public bool RemoveVariable(Variable key)
        => bases.Remove(key);

    public bool SetBaseValue(Variable key, VariableValue value)
    {
        bases[key] = value;
        return true;
    }

    public ModifierId AddModifier(EntityModifierEntry entry)
    {
        var id = entry.Id ?? ModifierId.New();
        var active = new ActiveModifier(id, entry.Modifier, entry.Source);
        if (!modifiers.TryGetValue(entry.Key, out var list))
        {
            list = new List<ActiveModifier>();
            modifiers[entry.Key] = list;
        }
        list.Add(active);   // FIFO append for insertion-order tie-breaking
        return id;
    }

    public bool RemoveModifier(Variable key, ModifierId id)
    {
        if (!modifiers.TryGetValue(key, out var list)) return false;
        return list.RemoveAll(m => m.Id == id) > 0;
    }

    public void ClearModifiers() => modifiers.Clear();

    public void RemoveModifiersFromSource(ModifierSource source)
    {
        foreach (var list in modifiers.Values)
            list.RemoveAll(m => Equals(m.Source, source));
    }
}
```

**Notes:**
- Internal storage shape (Dictionary + List) is illustrative; implementer can swap in `ImmutableDictionary`-backed forms if desired, but the contract is what matters.
- `OrderBy` stability is the load-bearing piece for tie-breaking. Verify the Unity/C# runtime delivers stable LINQ sort (it does for `Enumerable.OrderBy`).
- No subscription bookkeeping. If existing code on `LocalVariableStorage` had `event Action<Variable> Changed` or similar, it MAY remain as an impl-specific surface (not on the interface), but is not required by this plan.

### 5. `EntityDefinition` (audit, no shape change)

```csharp
public class EntityDefinition : IEntityDefinition
{
    public bool TryGetDefaultValue(Variable key, out VariableValue value) => /* unchanged */;
    public IEnumerable<Variable> DeclaredVariables => /* unchanged */;
    // ... existing authoring API unchanged
}
```

**Audit checklist:**
- Does `EntityDefinition` (or `IEntityDefinition`) currently implement `IVariableSource`, `IEntityVariableStorage`, or any chain/source-shaped interface? If yes, remove that implementation. The handle does the def fallback explicitly; no chain plumbing.
- Does it expose a `Parent`? If yes, remove (definitions are not in the chain).
- Does it have any subscription/event APIs related to variable changes? Definitions are authored; if such APIs exist, consider whether they're part of authoring (keep) or runtime reactivity (remove).

### 6. `Entities.Local<TDef>(def)` factory

```csharp
public static class Entities
{
    public static EntityInstance<TDef> Local<TDef>(TDef def) where TDef : IEntityDefinition
        => new(def, new LocalVariableStorage());
}
```

One-liner construction for the no-Store profile. No catalog, no ref. Used by tests, editor previews, and any non-Store consumer of Scaffold.Entities.

---

## Implementation order

1. **Refactor `IEntityVariableStorage`.** Add `Parent`, change reads to `TryGetBase` + `GetModifiers` + `Variables`, remove subscription methods. Compile errors will surface throughout the package — they're the migration map.
2. **Refactor `LocalVariableStorage`** to implement the new contract with `Parent` support. Self-only impl is straightforward; chain-walking impl is in §4 above.
3. **Refactor `EntityInstance<TDef>`** to drop identity and orchestrate reads via the new storage contract + definition fallback.
4. **Audit and update `EntityDefinition`** to ensure it doesn't implement any source/storage interface (per §5).
5. **Delete `IReadOnlyEntity` and `IMutableEntity`.** Update any internal references in the package.
6. **Add `Entities.Local<TDef>(def)` factory.**
7. **Internal smoke pass.** Build the package; run tests in the validation section.

Steps 1–4 are tightly coupled and can land in one PR — they're all the same shape change rippling through the package. Step 5 (interface deletion) might be the same PR or a follow-up, depending on how many internal call sites need migration. Step 6 is trivial and can be in either PR.

The bridge (Plan C) and project consumers will see compile errors against this package's old surface — that's expected and is what Plan C / project migration handles.

---

## Validation

### Unit tests (required)

| Test | Asserts |
|---|---|
| `EntityInstance_Read_AnchorsOnStorageBase` | Storage has base; def has default; no modifiers → returns storage's base value |
| `EntityInstance_Read_AnchorsOnDefDefault_WhenStorageBaseAbsent` | Storage has no base; def has default → returns def's default |
| `EntityInstance_Read_NoAnchor_ReturnsFalse` | Storage has no base; def has no default → `TryGetVariable` returns false |
| `EntityInstance_Read_AppliesModifierToStorageBase` | Base + 1 modifier → returns base + modifier applied |
| `EntityInstance_Read_AppliesModifierToDefDefault_WhenNoStorageBase` | No storage base; def default present; 1 modifier → returns (def default + modifier) |
| `EntityInstance_Variables_UnionsStorageAndDefDeclared` | Returns variables present in either storage or definition |
| `EntityInstance_Writes_DelegateToStorage` | Each write method (`AddVariable`, `SetBaseValue`, etc.) lands the change in storage |
| `EntityInstance_NoIdNoSelfRef` | Compile-time check: `EntityInstance<TDef>` has no `Id` or `SelfRef` member |
| `LocalVariableStorage_NoParent_TryGetBase_OwnOnly` | Returns own base only; chain is null |
| `LocalVariableStorage_WithParent_TryGetBase_OwnWins` | Both layers have the base → returns own |
| `LocalVariableStorage_WithParent_TryGetBase_FallsBackToParent` | Own missing; parent has → returns parent's |
| `LocalVariableStorage_WithParent_TryGetBase_DeepChain` | 3+ layers; first base anywhere wins |
| `LocalVariableStorage_NoParent_NoBase_ReturnsFalse` | Returns false; no chain, no base |
| `LocalVariableStorage_GetModifiers_SelfFirst` | Self's modifiers come before parent's at equal `Order` |
| `LocalVariableStorage_GetModifiers_OrderTie_FifoWithinSelf` | Same `Order` within one storage → insertion order |
| `LocalVariableStorage_GetModifiers_DeepChain_StableOrder` | 3+ layers, mixed Orders → deterministic stable output |
| `LocalVariableStorage_Variables_UnionsChain` | Chain's variables all enumerated |
| `LocalVariableStorage_Writes_LocalOnly` | Writing to overlay does not affect parent |
| `LocalVariableStorage_AddVariable_DuplicateLocal_ReturnsFalse` | Existing key not overwritten by `AddVariable` |
| `LocalVariableStorage_SetBaseValue_OverwritesAddVariable` | `SetBaseValue` always lands the value, replacing existing |
| `LocalVariableStorage_RemoveModifiersFromSource_RemovesAcrossKeys` | All modifiers from a source are removed regardless of which variable they target |
| `IEntityVariableStorage_NoSubscriptionMethods` | Reflection-based check: no `Subscribe*` methods on the interface |
| `IReadOnlyEntity_TypeNotPresent` | Reflection-based check: `IReadOnlyEntity` type does not exist |
| `IMutableEntity_TypeNotPresent` | Reflection-based check: `IMutableEntity` type does not exist |
| `EntityDefinition_DoesNotImplementStorageOrSource` | Reflection check: `EntityDefinition` does not implement `IEntityVariableStorage` or any removed source interface |

### Integration tests (recommended)

- **Two-layer overlay reads.** Construct `LocalVariableStorage(parent: baseStorage)`. Set bases and modifiers at both layers across a few variables. Wrap in an `EntityInstance<TDef>` with a definition that has its own defaults. Verify `GetVariable<T>` produces the expected anchor + modifier-folded result for each combination of (storage-base / def-default) × (modifiers at one or both layers / no modifiers).
- **Modifier ordering determinism.** Across a 3-layer chain, register modifiers at each layer with various `Order` values including ties. Verify `GetModifiers(key)` returns a stable ordering across multiple invocations and across runs.
- **Writes through overlay don't propagate.** Make a write through the overlay; assert parent storage is bit-for-bit unchanged.
- **`Entities.Local(def)` end-to-end.** Construct via the factory, perform a few reads/writes, verify behavior matches a manual `new EntityInstance<TDef>(def, new LocalVariableStorage())`.

### Property tests (nice-to-have, not required)

- For all `(IEnumerable<ActiveModifier> a, IEnumerable<ActiveModifier> b)` with mixed Orders: `a.Concat(b).OrderBy(...)` is a permutation of the multiset and is stable for equal-Order pairs.
- For all base/modifier combinations: `TryGetVariable` is deterministic across repeat calls (no implicit state).

---

## Open / decide-during-impl

1. **Existing subscription consumers.** If anywhere in `com.scaffold.entities` (or referenced from outside this package) currently subscribes to `IEntityVariableStorage` events, those callers will compile-fail. Decide per-call-site: drop the subscription (substrate handles it), keep on a storage-specific event surface, or migrate to a polling pattern. Out-of-package callers are NOT this plan's concern; flag them for the consumer to address.
2. **`EntityInstance<TDef>.Dispose` semantics.** Base impl is a no-op; subclasses (Plan C `StoreVariableStorage`-backed) override if they hold listeners. Decide whether to require `Dispose()` calls at any specific lifecycle moment, or leave it best-effort.
3. **`LocalVariableStorage` internal storage type.** `Dictionary<Variable, ...>` (mutable, fast) vs. `ImmutableDictionary<Variable, ...>` (immutable, structural sharing). Most existing impls use mutable; immutable would only matter if Local entities ever participated in snapshot/rollback (they don't, by design). Keep mutable.
4. **`Variables` enumeration ordering.** The contract says "union" but doesn't specify ordering. If determinism for replay/inspection matters, sort by `Variable.Key` (ordinal) or similar. Decide based on whether downstream consumers rely on ordered enumeration.
5. **`AddVariable` semantics on duplicate keys.** Current spec: returns false, doesn't overwrite. If existing impl's contract says otherwise, reconcile. Pick one and document.
6. **Whether `ActiveModifier` and `EntityModifierEntry` need any shape change.** The plan assumes existing shapes work as-is. If `Order` doesn't exist on `ActiveModifier` today, add it (it's load-bearing for the new folding semantics).
7. **Existing `LocalVariableStorage` event surface.** If it currently exposes `event Action<Variable> Changed` or similar, decide: keep as impl detail (not on interface), or remove entirely. Either is fine; not part of this plan's contract.
8. **Caching / perf.** If existing `LocalVariableStorage` has any precomputed effective-value caches, remove them — reads always recompute. If profiler later shows hot paths, that's a separate plan.

---

## What "validate" means before moving to Plan C

Plan B is validated when:

- All "required" unit tests above pass.
- All "recommended" integration tests pass.
- The `com.scaffold.entities` package builds green with the new contract.
- `IReadOnlyEntity` and `IMutableEntity` are gone from the package source tree.
- `EntityDefinition` has no source/storage interface implementations.
- A short retro pass on this plan: any hard requirement or hard avoid that was wrong gets noted in the spike's open items, and the spike is updated.

After validation passes, Plan C (`com.scaffold.entities.states` rebuild) starts. Plan C ties Plan A's catalog primitive and Plan B's storage contract together via `StoreVariableStorage`, the new `EntityState` slice, and the seven payloads.

---

## Out-of-band reminders

- Plan A (Scaffold.States primitives) is being landed in parallel / before this. Plan B does NOT reference any types from Plan A — `IRef`, `Ref<T>`, `ICatalog` are not used here. Plan C is where they meet.
- The bridge package (`com.scaffold.entities.states`) is touched in Plan C, not here. Don't preemptively change bridge code; expect it to compile-fail against the new contract until Plan C lands.
- The project layer (`Card`, `Zone`, `Player`, slices) is touched after the bridge. Same rule — don't preemptively migrate. Existing project code may reference `IReadOnlyEntity` / `IMutableEntity` / old `LocalVariableStorage` surface; those break and stay broken until project migration.
- Existing `Identifier<T>` / `InstanceId` / `IReference` machinery is NOT touched in this plan. They're entirely outside `com.scaffold.entities`'s scope.
- The single-class-for-base-and-overlay design (`LocalVariableStorage` with optional `Parent`) is a hard structural choice, not an optimization. Don't reintroduce `OverlayStorage` as a sibling type even if it feels natural — composition via `Parent` is the design.
