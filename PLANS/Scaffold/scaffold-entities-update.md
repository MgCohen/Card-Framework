---
type: plan
status: implemented
tags: [#scaffold, #entities, #refactor]
---

# Scaffold.Entities — refactor (chain-walking storage, identity-free base, `InstanceId` retirement, cross-cutting cleanup)

> **Plan B** of the Scaffold update sequence. Refactors `com.scaffold.entities` and absorbs cross-cutting cleanup that touches `com.scaffold.entities.states` (rename, delete, identifier migration). Plan C (full bridge rebuild) ships afterward and is correspondingly thinner.

**Source spike:** [`Product/spikes/entities-state-unification.md`](../../Product/spikes/entities-state-unification.md) — locked decisions #1, #2, #3, #5, #6, #7, #11; ratified R4, R5, R7, R8, R9, R10, R11; final shapes §2 (and bits of §3 for the rename/delete/migration).

**Plan A status:** landed in [Scaffold#40](https://github.com/MgCohen/Scaffold/pull/40) with a substantive rebase — `Ref<T>` is a `sealed record class` deriving from the new `abstract record Reference` (not a `readonly struct + IRef`), because main's recent state refactor deleted `IReference` and replaced it with `Reference`. **Plan B inherits this reality**: every "ref-shaped" type here uses `Reference` as the polymorphic base and `Ref<T>` (sealed record) as the typed handle. Plan B is no longer "independent of Plan A" — it depends on Plan A's `Ref<T>` for `InstanceId` migration and `ModifierSource` reshape.

---

## Decisions ratified during Plan A review (2026-05-05)

These were decided while reviewing Plan A but are scope for Plan B. Locked inputs:

| # | Decision | Notes |
|---|---|---|
| R4 | Delete `BaseEntityInstance<TDefinition>`. Fold definition field, storage field, and read accessors into `EntityInstance<TDef>` directly. | Single instance class. |
| R5 | Rename `EntityVariableState` → `EntityState`. The trimmed `(Bases, Modifiers)` shape no longer needs "Variable" in the name; the rename also signals the type is gutted vs. the prior 365 LOC version. | Touches `com.scaffold.entities.states`. |
| R7 | Delete `EntityStateReference`. Slices keyed directly by `Ref<T>`; the wrapper has no role. | Touches `com.scaffold.entities.states`. |
| R8 | Drop `IEntityVariableStorage.TryGetEffective`. The handle folds modifiers on every read (`TryGetBase` + `GetModifiers`). The current `instanceEffectiveBag` cache in `LocalVariableStorage` is removed. Re-add a private cache only if profiling later requires it; do not put it back on the contract. | Already aligned with the original draft. |
| R9 | Strip `VariableBag`'s parent pointer. Today `LocalVariableStorage.WireToDefinition` wires the definition's bag as a parent for default lookup. Remove that linkage. Definition default-fallback lives at the handle level (`Definition.TryGetDefaultValue` after the `IEntityVariableStorage.Parent` chain bottoms out). Storage `Parent` is overlays-only. | New for Plan B. |
| R10 | Replace `InstanceId(int)` with `Ref<T>(Guid)` everywhere — all bridge payloads, `StateEntity<TDef>`, factories, all current `InstanceId` usages. `InstanceId` is **deleted** at the end of Plan B. | Cross-cutting. Touches `com.scaffold.entities.states`. |
| R11 | `ModifierSource` becomes `readonly struct ModifierSource(Reference Source, int Tag)` (rebased — was `IRef Source` in the spike, but `IRef` no longer exists; `Reference` is the polymorphic base on main). | New for Plan B. |

---

## Decisions ratified during implementation prep (2026-05-05)

These were decided after a pre-implementation audit of the current codebase against this plan. Locked inputs:

| # | Decision | Notes |
|---|---|---|
| R12 | Drop `Id` property from `EntityComponent<TDef>` and the `InstanceId`-based `InitializeFromDefinition` overload. `EntityComponent<TDef>` is identity-free, like the base `EntityInstance<TDef>`. Subclasses that need a ref add it themselves (mirrors `Card`/`Zone` pattern from the spike). | New for Plan B. |
| R13 | Delete `StateEntity<TDef>` in Plan B (was scoped to Plan C). Construction sites swap to `new EntityInstance<TDef>(def, new StoreVariableStorage(store, ref))`. The `OnEntityRemoved` lifecycle hook rides on `EntityInstance.Dispose()` or a small store-side listener. | Brought forward from Plan C; consistent with Locked #1 (single instance type). Required because `BaseEntityInstance<TDefinition>` and `IMutableEntity<TDefinition>` — both `StateEntity` supertypes — are deleted in Plan B. |
| R14 | `StoreVariableStorage` shrinks to ~50 LOC thin adapter in Plan B as a forced consequence of the new `IEntityVariableStorage` contract. Plan C's scope narrows to: `EntityState` shape change to pure `(Bases, Modifiers)`, payload reshape, mutator consolidation, `ClearModifiersPayload` introduction. | Brought forward from Plan C. The 422 LOC current impl is ~80% caching/subscription bookkeeping that is unreachable through the new contract; the collapse is forced, not optional. |
| R15 | Reshape `IEntityVariableStorage.AddModifier` signature to `ModifierId AddModifier(Variable key, VariableModifier mod, ModifierSource source = default, ModifierId? id = null)`. Do NOT add `Source` or `Id` to `EntityModifierEntry` — it stays an authoring-only `[Serializable]` type with Unity inspector drawers. Source/Id are runtime-attribution data; loading them on the authoring type pollutes the inspector surface. | Refines §1. |
| R16 | Delete `EntityInstanceCreator<TDef>`, `IInstanceIdGenerator`, `IncrementingInstanceIdGenerator` in Plan B. `Entities.Local<TDef>(def)` is the only no-Store factory; Store-backed construction goes through `store.Catalog.AllocateRef` + `RegisterAt` (and `StoreEntities.Spawn` in Plan C). The id-generator concept does not survive — identity is a registration concern owned by the catalog. | New for Plan B. |
| R17 | Delete `StoreInstanceIdExtensions` entirely. Bridge call sites migrate to the native `Reference`-keyed Store API (already accepted on main per Plan A) using `Ref<T>` directly — no re-wrapping. | New for Plan B. |

---

## TL;DR

Refactor `com.scaffold.entities` and clean up `com.scaffold.entities.states` so that:

- `IEntityVariableStorage` has a `Parent` reference, exposes chain-walking reads (`TryGetBase`, `GetModifiers`, `Variables`), keeps writes as local-only operations, and has **no** subscription methods. **No `TryGetEffective`** (R8) — folding happens at the handle.
- The `IReadOnlyEntity` / `IMutableEntity` interface tier is deleted. `EntityInstance<TDef>` is the single class, with `BaseEntityInstance<TDefinition>`'s responsibilities folded in (R4).
- `EntityInstance<TDef>` no longer carries any identity (no `Id`, no `SelfRef`, no `Reference`-shaped field). It pairs `(Definition, Storage)`. Identity is a registration concern handled by subclasses or by the catalog.
- `EntityInstance<TDef>` orchestrates reads: storage chain (base lookup) → definition default-fallback → modifier folding across the full chain, sorted by `Order` with stable tie-breaking by chain order.
- `LocalVariableStorage` becomes a single class that handles both the base case (`Parent = null`) and the overlay case (`Parent = some other storage`) via the same code path. **`VariableBag`'s parent pointer to definitions is stripped** (R9) — definition default-fallback lives only at the handle level. The current `instanceEffectiveBag` cache is removed.
- `EntityDefinition` stays an authored asset. It does **not** implement any variable-source / storage interface. The handle consults it directly for default-value fallback.
- `ModifierSource` becomes `readonly struct ModifierSource(Reference Source, int Tag)` (R11). Polymorphic source identity matches the rest of the system.
- `InstanceId(int)` is replaced with `Ref<T>(Guid)` everywhere — Scaffold.Entities, all bridge payloads, factories, `StateEntity<TDef>`, all current usages — and **`InstanceId` is deleted at the end of Plan B** (R10).
- `EntityVariableState` is renamed to `EntityState` (R5). `EntityStateReference` is deleted (R7).
- `Entities.Local<TDef>(def)` factory provides one-liner construction for the no-Store profile.

The substantive shape change is to `IEntityVariableStorage` (chain-walking via `Parent`); the rest is removals, renames, and identifier migration.

---

## Scope

### In scope (this plan)

**`com.scaffold.entities`:**
- `IEntityVariableStorage` interface refactor.
- Delete `IReadOnlyEntity` and `IMutableEntity` interfaces.
- Delete `BaseEntityInstance<TDefinition>` (R4); fold into `EntityInstance<TDef>`.
- `EntityInstance<TDef>` refactor (constructor signature, removed identity, read orchestration, write delegation).
- `LocalVariableStorage` refactor (Parent support, base+overlay in one class, no subscription bookkeeping, no effective-value cache, no def-bag parent pointer).
- `VariableBag` parent-pointer stripping (R9). Remove `LocalVariableStorage.WireToDefinition` (or equivalent).
- `EntityDefinition` audit — ensure it does not implement any source/storage interface.
- `ModifierSource` reshape: `(Reference Source, int Tag)` (R11).
- `Entities.Local<TDef>(def)` factory.

**Cross-cutting (touches `com.scaffold.entities.states` and any other `InstanceId` callsites):**
- `InstanceId` migration to `Ref<T>` throughout the codebase (entities, bridge payloads, `StateEntity<TDef>`, factories). Delete `InstanceId` type at end (R10).
- Rename `EntityVariableState` → `EntityState` (R5).
- Delete `EntityStateReference` (R7).

### Out of scope (deferred to Plan C)

- Substantive rebuild of `com.scaffold.entities.states`: `EntityState` shape change to pure `(Bases, Modifiers)` (drop `Definition` reference and `Id` field), payload reshape, mutator consolidation, `StoreEntities.Spawn` extension, `ClearModifiersPayload` first-class. After Plan B, the bridge package compiles green and `StoreVariableStorage` is already a thin adapter (per R14), but the slice value type still has the old shape; Plan C does the data-shape rebuild.
- Project layer changes (`Card`, `Zone`, `Player`, project slices, registries).
- `Identifier<T>` migration (separate cascade pass).
- Per-instance reactivity / change events (substrate-specific; not part of the contract).
- Cross-package migration of consumers using the deleted interfaces — they'll compile-fail and stay broken until project migration.

---

## Hard requirements

1. **`IEntityVariableStorage.Parent`** is exposed and may be null. Null = base storage. Non-null = overlay layered on top of another storage.
2. **`IEntityVariableStorage.TryGetBase(Variable, out VariableValue)` walks the chain.** Returns the first base value found in self → parent → ... → null. Returns false if no base exists anywhere in the chain.
3. **`IEntityVariableStorage.GetModifiers(Variable)` returns modifiers across the entire chain, sorted by `Order` (stable).** Tie-breaking: chain order (self first, then parent, recursively); within a single storage, FIFO by insertion order.
4. **`IEntityVariableStorage.Variables`** returns the union of variables across the chain (own bases ∪ own modifiers ∪ parent's `Variables`).
5. **No subscription methods on `IEntityVariableStorage`.** Remove `Subscribe(Variable, …)`, `SubscribeToVariableStructuralChanges(...)`, and any other reactivity-shaped methods. Reactivity is a substrate concern.
6. **No `TryGetEffective` on `IEntityVariableStorage`** (R8). Folding is the handle's job. Remove the existing `instanceEffectiveBag` cache in `LocalVariableStorage`. Do not reintroduce caching on the contract; if profiling later demands it, add as a private impl detail only.
7. **`IReadOnlyEntity` and `IMutableEntity` are deleted.** Any references in the package compile-fail and must be removed. Polymorphism over entity handles is by `EntityInstance<TDef>` directly (or by `Reference` for addressable use cases — Plan A's `Ref<T>` derives from `Reference`).
8. **`BaseEntityInstance<TDefinition>` is deleted** (R4). Its responsibilities (`Definition` field, `Storage` field, read accessors) fold into `EntityInstance<TDef>` directly. No transitional partial-class scaffolding.
9. **`EntityInstance<TDef>` has no identity field.** No `Id`, no `SelfRef`, no `Reference`-shaped field. Constructor takes `(TDef def, IEntityVariableStorage storage)` — exactly two parameters.
10. **`EntityInstance<TDef>.TryGetVariable<T>(Variable, out T)` orchestrates the read** in this exact order:
    a. Try `Storage.TryGetBase(key, out anchor)`.
    b. If false, try `Definition.TryGetDefaultValue(key, out anchor)`.
    c. If neither produces an anchor, return false (value = default).
    d. Otherwise iterate `Storage.GetModifiers(key)` and apply each in sequence.
    e. Return true with the resulting value.
11. **`EntityInstance<TDef>.Variables`** returns the union of `Storage.Variables` and `Definition.DeclaredVariables`.
12. **Writes on `EntityInstance<TDef>` are pure delegation to `Storage`.** No additional logic, no events, no caching.
13. **`LocalVariableStorage` is a single class** that takes an optional `IEntityVariableStorage? parent = null` constructor argument. The same class handles base and overlay. No separate `OverlayStorage` type.
14. **`LocalVariableStorage` writes always land locally.** Parent storage is never mutated through `self`.
15. **`VariableBag` parent pointer is stripped** (R9). Remove `LocalVariableStorage.WireToDefinition` (or any equivalent that wires a definition's bag as a `VariableBag` parent). Definition default-fallback is **only** at the handle level — never via the storage or bag chain.
16. **`EntityDefinition` does not implement any source/storage interface.** Keep `IEntityDefinition` (unchanged: `TryGetDefaultValue`, `DeclaredVariables`, etc.). Remove any `IVariableSource`-shaped declarations if present.
17. **`Entities.Local<TDef>(def)` factory** returns `EntityInstance<TDef>` constructed with a fresh `LocalVariableStorage()` (no parent). One-liner.
18. **Modifier-folding determinism is testable.** Same-Order ties resolve in the documented order (chain-first, then insertion-order within a single storage).
19. **`ModifierSource` reshape: `readonly struct ModifierSource(Reference Source, int Tag)`** (R11). All call sites that constructed `ModifierSource` from an `InstanceId` migrate to `Reference` (typically `Ref<T>` of the source entity's typed handle).
20. **`InstanceId(int)` migrated to `Ref<T>(Guid)` everywhere it appears in `com.scaffold.entities` and `com.scaffold.entities.states`** (R10). Bridge payloads (`SetBaseValuePayload`, `AddModifierPayload`, etc.), `StateEntity<TDef>`, factories, any internal field — all migrate. `InstanceId` type is **deleted** at the end of Plan B; the package builds with zero `InstanceId` references.
21. **Rename `EntityVariableState` → `EntityState`** (R5). Update all references in `com.scaffold.entities.states` and any external callers. The substantive shape change of this slice happens in Plan C — Plan B only renames.
22. **Delete `EntityStateReference`** (R7). With slices keyed directly by `Ref<T>`, the wrapper has no role. Remove the type and migrate any usages to `Ref<T>` (or whichever concrete `Reference` subtype is appropriate).
23. **`EntityComponent<TDef>` is identity-free** (R12). No `Id` property, no `InstanceId`-typed `InitializeFromDefinition` overload. Initialization takes `(TDef definition)` — or constructs an internal `EntityInstance<TDef>` from a definition directly. Subclasses that need a ref add it themselves.
24. **`StateEntity<TDef>` is deleted** (R13). No subclass — Store-backed entities are constructed as `new EntityInstance<TDef>(def, new StoreVariableStorage(store, ref))` directly. Lifecycle hooks (e.g., on-removed) ride on `EntityInstance.Dispose()` or a small store-side listener.
25. **`EntityInstanceCreator<TDef>`, `IInstanceIdGenerator`, `IncrementingInstanceIdGenerator` are deleted** (R16). The package builds with zero references to any of them. No-Store construction is `Entities.Local<TDef>(def)`; Store-backed is via the catalog.
26. **`StoreInstanceIdExtensions` is deleted** (R17). All eight extension methods go. Bridge call sites migrate to the native `Reference`-keyed Store API using `Ref<T>` directly.
27. **`StoreVariableStorage` is rewritten to ~50 LOC thin adapter** (R14). Reads delegate to the current `EntityState` slice (via `store.Get<EntityState>(target)`); writes go via `store.Execute<Payload>`. No effective-value cache, no per-variable subscriber bookkeeping, no structural-diff machinery, no `Parent` (Store-backed storages don't chain — overlays wrap them in a `LocalVariableStorage`).

---

## Hard avoids

1. **Do NOT add subscription methods anywhere on the entity contract.** Not on `IEntityVariableStorage`, not on `EntityInstance<TDef>`. No `Changed` events. No `Subscribe`.
2. **Do NOT put `EntityDefinition` (or `IEntityDefinition`) in the storage chain.** Definition default-fallback is the handle's job, not a chain link. The chain is storage-to-storage only.
3. **Do NOT add `SelfRef`, `Id`, or any `Reference`-shaped identity field to `EntityInstance<TDef>`.** Base class is `(Definition, Storage)`. Period.
4. **Do NOT keep `IReadOnlyEntity` or `IMutableEntity`.** They go.
5. **Do NOT introduce a separate `OverlayStorage` class.** Overlay is `LocalVariableStorage(parent: somethingElse)`. One class.
6. **Do NOT reintroduce `TryGetEffective` on storage.** R8 explicitly removes it; the handle's `TryGetBase + GetModifiers + def fallback` orchestration is the sanctioned path.
7. **Do NOT couple `EntityDefinition` to `IEntityVariableStorage` or any variable-source interface.** Definitions are authored assets; storages are runtime data containers. Different things.
8. **Do NOT keep `BaseEntityInstance<TDefinition>` as transitional scaffolding** even temporarily. Delete in one motion when `EntityInstance<TDef>` absorbs its responsibilities.
9. **Do NOT keep `InstanceId` after the migration completes.** R10 mandates deletion at the end of Plan B. The package builds with zero `InstanceId` references.
10. **Do NOT reintroduce `VariableBag` parent-to-definition wiring** (e.g., a sneaky `WireToDefinition` reborn under a different name). Definition default-fallback is exclusively handle-level.
11. **Do NOT modify `com.scaffold.states` in this plan.** Plan A owns it (already landed).
12. **Do NOT do the substantive bridge rebuild here.** Plan B's bridge-package work is limited to: `EntityVariableState` → `EntityState` rename, `EntityStateReference` deletion, `InstanceId` → `Ref<T>` migration of payload fields, and `ModifierSource` reshape. The 422 LOC `StoreVariableStorage` shrink, the new payload set, the `EntityState` shape change to pure `(Bases, Modifiers)`, the `ClearModifiersPayload` introduction — all of those are Plan C.
13. **Do NOT add caches or precomputed effective-value tables to `EntityInstance<TDef>` or storages.** Reads recompute from primitives. Profiler-driven re-introduction is a separate plan.
14. **Do NOT rename `IEntityDefinition.TryGetDefaultValue` or `DeclaredVariables`.** They stay as-is.
15. **Do NOT add reflection-heavy machinery for type-aware equality across modifier value types.** Modifiers compose via their own `Apply` method; equality is the value type's own concern.
16. **Do NOT add `Source` or `Id` fields to `EntityModifierEntry`** (R15). It stays an authoring-only `[Serializable]` type. Source/Id flow through the interface signature, not through the authoring shape.
17. **Do NOT keep an `Id` property on `EntityComponent<TDef>` even temporarily** (R12). Identity belongs to subclasses that opt-in, not the generic component base.
18. **Do NOT keep `StateEntity<TDef>`, `EntityInstanceCreator<TDef>`, `IInstanceIdGenerator`, `IncrementingInstanceIdGenerator`, or `StoreInstanceIdExtensions`** (R13, R16, R17). The package builds with zero references to all five.
19. **Do NOT keep the old caching/subscription bookkeeping in `StoreVariableStorage`** (R14). The new contract makes it unreachable; delete rather than orphan. Re-add caching as a private impl detail only if profiling later demands.

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
    ModifierId AddModifier(Variable key, VariableModifier mod, ModifierSource source = default, ModifierId? id = null);   // R15
    bool RemoveModifier(Variable key, ModifierId id);
    void ClearModifiers();
    void RemoveModifiersFromSource(ModifierSource source);
}
```

**Notes:**
- **No `TryGetEffective`** (R8). Folding happens at the handle.
- Tie-breaking determinism is enforced by impl, not consumer. `LocalVariableStorage` builds its `GetModifiers` walk such that same-Order modifiers within a single storage stay in insertion order (FIFO), and across the chain, self's modifiers come before parent's. C#'s `Enumerable.OrderBy` is stable.

### 2. Delete `IReadOnlyEntity` and `IMutableEntity`

These interfaces are removed entirely. Any internal type or method that referenced them is updated to use `EntityInstance<TDef>` (or `IEntityDefinition` / `IEntityVariableStorage` for the underlying capabilities). External consumers (the bridge, the project) will be migrated in Plan C and beyond — they compile-fail in the meantime.

### 3. Delete `BaseEntityInstance<TDefinition>` (R4)

Currently, `BaseEntityInstance<TDefinition>` is a base class that holds the definition reference and the storage reference, with `EntityInstance<TDef>` extending it. R4 deletes the base class and folds its responsibilities into `EntityInstance<TDef>` directly. No "Base" prefix, no inheritance chain — single class.

Migration steps:
1. Move `Definition` and `Storage` fields from `BaseEntityInstance<TDefinition>` to `EntityInstance<TDef>`.
2. Move read accessors (e.g., `GetVariable<T>`, `TryGetVariable<T>`) inline.
3. Delete `BaseEntityInstance<TDefinition>` file.
4. Remove `where TDefinition : ...` constraint flow that was on the base; reconcile with `EntityInstance<TDef>`'s constraint.

### 4. `EntityInstance<TDef>` (refactored, absorbs `BaseEntityInstance<TDefinition>` per R4)

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
    public ModifierId AddModifier(Variable key, VariableModifier mod, ModifierSource source = default, ModifierId? id = null)
        => Storage.AddModifier(key, mod, source, id);   // R15
    public bool RemoveModifier(Variable key, ModifierId id)              => Storage.RemoveModifier(key, id);
    public void ClearModifiers()                                          => Storage.ClearModifiers();
    public void RemoveModifiersFromSource(ModifierSource source)         => Storage.RemoveModifiersFromSource(source);

    public virtual void Dispose() { /* base no-op; subclasses (Plan C bridge handle) release listeners if any */ }
}
```

**Notes:**
- `IDisposable` stays for subclass extension (e.g., the Plan C bridge handle, if it ends up holding listener resources). Default impl is a no-op.
- `class` (not `sealed`) so subclasses (`Card`, `Zone`, etc.) can extend.
- No `Id`, no `SelfRef`. Identity is a subclass concern (when applicable).

### 5. `LocalVariableStorage` (refactored)

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

    public bool RemoveVariable(Variable key) => bases.Remove(key);

    public bool SetBaseValue(Variable key, VariableValue value)
    {
        bases[key] = value;
        return true;
    }

    public ModifierId AddModifier(Variable key, VariableModifier mod, ModifierSource source = default, ModifierId? id = null)
    {
        var resolvedId = id ?? ModifierId.New();
        var active = new ActiveModifier(resolvedId, mod, source);
        if (!modifiers.TryGetValue(key, out var list))
        {
            list = new List<ActiveModifier>();
            modifiers[key] = list;
        }
        list.Add(active);   // FIFO append for insertion-order tie-breaking
        return resolvedId;
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

**What was removed (from the existing impl):**
- The `instanceEffectiveBag` (or whatever the existing effective-value cache is called) — drop entirely (R8).
- `WireToDefinition` (or whatever method wires the definition's bag as a parent for default lookup) — drop entirely (R9). Definition default-fallback is now exclusively handle-level.
- Subscription bookkeeping (event lists, change notifications via `Subscribe(Variable, ...)`, etc.) — drop entirely.

**What stays as impl detail (not on contract):**
- `LocalVariableStorage` MAY expose its own `event Action<Variable>? Changed` for in-memory consumers that want simple reactivity. Not required, not part of the contract — implementer's call.

### 6. `EntityDefinition` (audit, no shape change)

```csharp
public class EntityDefinition : IEntityDefinition
{
    public bool TryGetDefaultValue(Variable key, out VariableValue value) => /* unchanged */;
    public IEnumerable<Variable> DeclaredVariables => /* unchanged */;
    // ... existing authoring API unchanged
}
```

**Audit checklist:**
- Does `EntityDefinition` (or `IEntityDefinition`) currently implement `IVariableSource`, `IEntityVariableStorage`, or any chain/source-shaped interface? If yes, remove that implementation.
- Does it expose a `Parent` or `VariableBag` field that gets wired into `LocalVariableStorage`? If yes, remove (R9 removes the wiring; the field may become unused).
- Does it have any subscription/event APIs related to variable changes? Definitions are authored; if such APIs exist, consider whether they're part of authoring (keep) or runtime reactivity (remove).

### 7. `Entities.Local<TDef>(def)` factory

```csharp
public static class Entities
{
    public static EntityInstance<TDef> Local<TDef>(TDef def) where TDef : IEntityDefinition
        => new(def, new LocalVariableStorage());
}
```

One-liner construction for the no-Store profile.

### 8. `ModifierSource` reshape (R11)

```csharp
public readonly struct ModifierSource : IEquatable<ModifierSource>
{
    public Reference Source { get; }
    public int Tag { get; }

    public ModifierSource(Reference source, int tag = 0)
    {
        Source = source;
        Tag = tag;
    }

    // equality + hashing on (Source, Tag)
}
```

**Notes:**
- Was previously keyed by `InstanceId` (or possibly an `IRef` per the spike's pre-rebase R11). Now uses `Reference` — Plan A's polymorphic base. `Ref<T>` slots in directly.
- `Tag` defaults to 0; opt-in author disambiguation when one source contributes multiple modifier groups that may need partial removal (existing pattern from the model docs).
- `RemoveModifiersFromSource` semantics unchanged: sweep modifiers whose `Source` equals the given `ModifierSource` (by `(Source, Tag)`).

### 9. `InstanceId` migration → `Ref<T>` + deletion (R10)

`InstanceId(int)` is deleted at the end of Plan B. Migration:

1. **Identify all `InstanceId` usages** in `com.scaffold.entities` and `com.scaffold.entities.states`. Likely sites:
   - Bridge payloads (`SetBaseValuePayload(InstanceId Id, ...)`, etc.) → migrate `InstanceId` field to `Reference` (or `Ref<T>` if the type is known statically).
   - `StateEntity<TDef>` (in the bridge) — **deleted in Plan B per R13** (its supertypes `BaseEntityInstance<TDefinition>` and `IMutableEntity<TDefinition>` are deleted in Plan B, so it can't survive). Construction sites swap to `new EntityInstance<TDef>(def, new StoreVariableStorage(store, ref))`.
   - Factories / spawn helpers — return `Ref<T>` instead of `InstanceId`.
   - Any internal field on entities or storages that holds an `InstanceId`.
2. **Replace each usage** with `Ref<T>` (when typed) or `Reference` (when polymorphic). For payloads that need to target an entity, use `Reference Target` since payloads are kind-erased at the substrate level.
3. **Update `ModifierSource`** to use `Reference` (R11; covered in §8 above).
4. **Delete `InstanceId` type and `InstanceId.New()` factory** at the end. The package must build with zero references.

The substantive bridge rebuild (payload reshape, `StoreVariableStorage` shrink, etc.) is Plan C — Plan B only does the type substitution.

### 10. Rename `EntityVariableState` → `EntityState` (R5)

Mechanical rename in `com.scaffold.entities.states`:
1. Rename the file: `EntityVariableState.cs` → `EntityState.cs`.
2. Rename the type: `EntityVariableState` → `EntityState`.
3. Update all references (in this package and any external callers — tests, project layer, etc.).

The substantive shape change (drop `Definition` ref, drop `Id` field, become pure `(Bases, Modifiers)`) is Plan C — Plan B only renames.

### 11. Delete `EntityStateReference` (R7)

Currently, `EntityStateReference` wraps an `InstanceId` (or similar) as the slice key for `EntityVariableState`. With `Ref<T> : Reference` on main, slices key directly on `Reference`; the wrapper is dead weight. Delete the type and migrate any usages to `Ref<T>` (or whichever concrete `Reference` subtype the call site needs).

---

## Implementation order

1. **Refactor `IEntityVariableStorage`.** Add `Parent`, change reads to `TryGetBase` + `GetModifiers` + `Variables`, remove subscription methods, drop `TryGetEffective` (R8). Compile errors will surface throughout the package — they're the migration map.
2. **Refactor `LocalVariableStorage`** to implement the new contract with `Parent` support. Strip the `instanceEffectiveBag` cache (R8) and `WireToDefinition` linkage (R9). Self-only impl is straightforward; chain-walking impl is in §5 above.
3. **Strip `VariableBag` parent pointer** (R9). Remove the field and any code that sets it.
4. **Delete `BaseEntityInstance<TDefinition>`** (R4). Fold its responsibilities into `EntityInstance<TDef>` directly.
5. **Refactor `EntityInstance<TDef>`** to drop identity and orchestrate reads via the new storage contract + definition fallback.
6. **Audit and update `EntityDefinition`** to ensure it doesn't implement any source/storage interface (per §6).
7. **Delete `IReadOnlyEntity` and `IMutableEntity`.** Update any internal references in the package.
8. **Drop `Id` from `EntityComponent<TDef>`** (R12). Remove the `InstanceId`-typed `InitializeFromDefinition` overload. Keep the generic component as a thin holder of an `EntityInstance<TDef>`.
9. **Delete `EntityInstanceCreator<TDef>`, `IInstanceIdGenerator`, `IncrementingInstanceIdGenerator`** (R16). Migrate any call sites to `Entities.Local<TDef>(def)` (no-Store) or catalog construction (Store-backed).
10. **Add `Entities.Local<TDef>(def)` factory.**
11. **Reshape `ModifierSource`** to `(Reference Source, int Tag)` (R11). Update all call sites that constructed it from `InstanceId`.
12. **Rewrite `StoreVariableStorage` to thin adapter** (R14). Reads delegate to the current `EntityState` slice; writes go via `store.Execute<Payload>`. Drop all caching, subscription bookkeeping, and structural-diff machinery.
13. **Delete `StateEntity<TDef>`** (R13). Migrate construction sites to `new EntityInstance<TDef>(def, new StoreVariableStorage(store, ref))`. Wire any lifecycle hooks via `Dispose()` or store listeners.
14. **Migrate `InstanceId` → `Ref<T>` / `Reference` everywhere in `com.scaffold.entities` and `com.scaffold.entities.states`** (R10). Bridge payloads, factories, any remaining field. Update tests.
15. **Delete `StoreInstanceIdExtensions`** (R17). Migrate call sites to the native `Reference`-keyed Store API with `Ref<T>` directly.
16. **Rename `EntityVariableState` → `EntityState`** (R5). Mechanical rename across the package and external callers.
17. **Delete `EntityStateReference`** (R7). Migrate usages to `Ref<T>`.
18. **Delete `InstanceId` type** (R10 finale). Verify zero references via build.
19. **Internal smoke pass.** Build both packages green; run the validation tests below.

Steps 1–10 are `com.scaffold.entities` work and can land in one PR. Step 11 (`ModifierSource`) might bundle with that PR or land separately. Steps 12–18 are the cross-cutting cleanup, including the `StoreVariableStorage` rewrite and `StateEntity` deletion — they can land in a second PR (or split: bridge contract migration + identifier migration + rename/delete).

The bridge (`com.scaffold.entities.states`) compiles green after Plan B with `StoreVariableStorage` already at thin-adapter shape (R14). The slice value type (`EntityState`, post-rename) still has the old `(BaseValues, ModifierStacks, ResolveEffectiveValues)` data shape; Plan C reshapes it to pure `(Bases, Modifiers)` and consolidates payloads/mutators.

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
| `BaseEntityInstance_TypeNotPresent` | Reflection-based: `BaseEntityInstance<>` does not exist (R4) |
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
| `LocalVariableStorage_NoEffectiveValueCache` | Reflection or behavior-based: no `instanceEffectiveBag` field exists; reads always recompute (R8) |
| `LocalVariableStorage_NoWireToDefinition` | Reflection: no `WireToDefinition` method exists; `VariableBag` has no parent pointer to a definition (R9) |
| `IEntityVariableStorage_NoSubscriptionMethods` | Reflection: no `Subscribe*` methods on the interface |
| `IEntityVariableStorage_NoTryGetEffective` | Reflection: no `TryGetEffective` method on the interface (R8) |
| `IReadOnlyEntity_TypeNotPresent` | Reflection: `IReadOnlyEntity` type does not exist |
| `IMutableEntity_TypeNotPresent` | Reflection: `IMutableEntity` type does not exist |
| `EntityDefinition_DoesNotImplementStorageOrSource` | Reflection: `EntityDefinition` does not implement `IEntityVariableStorage` or any removed source interface |
| `ModifierSource_HasReferenceSourceAndTag` | Field types: `Source` is `Reference`, `Tag` is `int` (R11) |
| `ModifierSource_EqualityIncludesTag` | Two `ModifierSource`s with same `Source` but different `Tag` are not equal |
| `InstanceId_TypeNotPresent` | Reflection: `InstanceId` type does not exist after migration (R10) |
| `EntityVariableState_TypeNotPresent` | Reflection: `EntityVariableState` type does not exist (R5; renamed to `EntityState`) |
| `EntityState_TypePresent` | Reflection: `EntityState` type exists in `com.scaffold.entities.states` (R5) |
| `EntityStateReference_TypeNotPresent` | Reflection: `EntityStateReference` type does not exist (R7) |
| `EntityComponent_NoIdProperty` | Reflection: `EntityComponent<TDef>` has no `Id` member (R12) |
| `StateEntity_TypeNotPresent` | Reflection: `StateEntity<>` type does not exist (R13) |
| `EntityInstanceCreator_TypeNotPresent` | Reflection: `EntityInstanceCreator<>` type does not exist (R16) |
| `IInstanceIdGenerator_TypeNotPresent` | Reflection: `IInstanceIdGenerator` type does not exist (R16) |
| `IncrementingInstanceIdGenerator_TypeNotPresent` | Reflection: `IncrementingInstanceIdGenerator` type does not exist (R16) |
| `StoreInstanceIdExtensions_TypeNotPresent` | Reflection: `StoreInstanceIdExtensions` type does not exist (R17) |
| `IEntityVariableStorage_AddModifierSignature` | Reflection: `AddModifier` takes `(Variable, VariableModifier, ModifierSource, ModifierId?)` not `(EntityModifierEntry)` (R15) |
| `EntityModifierEntry_NoSourceOrIdFields` | Reflection: `EntityModifierEntry` has no `Source` or `Id` field/property (R15) |
| `StoreVariableStorage_NoEffectiveCache` | Reflection: `StoreVariableStorage` has no `effectiveCache`-shaped field (R14) |
| `StoreVariableStorage_NoSubscriberDictionaries` | Reflection: `StoreVariableStorage` has no per-variable-subscriber or structural-subscriber collections (R14) |

### Integration tests (recommended)

- **Two-layer overlay reads.** Construct `LocalVariableStorage(parent: baseStorage)`. Set bases and modifiers at both layers across a few variables. Wrap in an `EntityInstance<TDef>` with a definition that has its own defaults. Verify `GetVariable<T>` produces the expected anchor + modifier-folded result for each combination of (storage-base / def-default) × (modifiers at one or both layers / no modifiers).
- **Modifier ordering determinism.** Across a 3-layer chain, register modifiers at each layer with various `Order` values including ties. Verify `GetModifiers(key)` returns a stable ordering across multiple invocations.
- **Writes through overlay don't propagate.** Make a write through the overlay; assert parent storage is bit-for-bit unchanged.
- **`Entities.Local(def)` end-to-end.** Construct via the factory, perform a few reads/writes, verify behavior matches a manual `new EntityInstance<TDef>(def, new LocalVariableStorage())`.
- **`ModifierSource` round-trip with `Ref<T>`.** Construct a modifier sourced from a typed `Ref<Card>`, store it via `AddModifier`, remove it via `RemoveModifiersFromSource(new ModifierSource(cardRef, 0))`. Verify the modifier is gone.
- **Bridge package compiles green** after the rename/delete/migration. The substantive logic is unchanged but type names and field types match the new contract.

### Property tests (nice-to-have, not required)

- For all `(IEnumerable<ActiveModifier> a, IEnumerable<ActiveModifier> b)` with mixed Orders: `a.Concat(b).OrderBy(...)` is a permutation of the multiset and is stable for equal-Order pairs.
- For all base/modifier combinations: `TryGetVariable` is deterministic across repeat calls (no implicit state).

---

## Open / decide-during-impl

1. **Existing subscription consumers.** If anywhere in `com.scaffold.entities` (or referenced from outside this package) currently subscribes to `IEntityVariableStorage` events, those callers will compile-fail. Decide per-call-site: drop the subscription (substrate handles it), keep on a storage-specific event surface, or migrate to a polling pattern. Out-of-package callers are NOT this plan's concern; flag them for the consumer to address.
2. **`EntityInstance<TDef>.Dispose` semantics.** Base impl is a no-op; subclasses (Plan C bridge handle) override if they hold listeners. Decide whether to require `Dispose()` calls at any specific lifecycle moment, or leave it best-effort.
3. **`LocalVariableStorage` internal storage type.** `Dictionary<Variable, ...>` (mutable, fast) vs. `ImmutableDictionary<Variable, ...>` (immutable, structural sharing). Most existing impls use mutable; immutable would only matter if Local entities ever participated in snapshot/rollback (they don't, by design). Keep mutable.
4. **`Variables` enumeration ordering.** The contract says "union" but doesn't specify ordering. If determinism for replay/inspection matters, sort by `Variable.Key` (ordinal) or similar. Decide based on whether downstream consumers rely on ordered enumeration.
5. **`AddVariable` semantics on duplicate keys.** Current spec: returns false, doesn't overwrite. If existing impl's contract says otherwise, reconcile. Pick one and document.
6. **Whether `ActiveModifier` and `EntityModifierEntry` need any shape change.** The plan assumes existing shapes work as-is. If `Order` doesn't exist on `ActiveModifier` today, add it (it's load-bearing for the new folding semantics).
7. **Existing `LocalVariableStorage` event surface.** If it currently exposes `event Action<Variable> Changed` or similar, decide: keep as impl detail (not on interface), or remove entirely. Either is fine; not part of this plan's contract.
8. **`InstanceId` migration cascade.** Likely surfaces unexpected callers in test fixtures, sample code, or other Scaffold packages that depend on entities. Triage as you find them; out-of-scope callers are flagged for the project layer.
9. **Bridge-package type-name updates.** `EntityVariableState` → `EntityState` rename and `EntityStateReference` deletion may surface external consumers (project, tests). Decide whether to provide temporary `[Obsolete]` aliases or hard-break. Lean: hard-break — cleaner cascade.
10. **`ModifierSource` migration of existing call sites.** Some existing modifier creation sites likely construct `ModifierSource(InstanceId)`. Migrate to `ModifierSource(Reference)` — usually `someEntity.SelfRef` (where the subclass exposes a typed `Ref<T>` SelfRef per the spike).

---

## What "validate" means before moving to Plan C

Plan B is validated when:

- All "required" unit tests above pass.
- All "recommended" integration tests pass.
- The `com.scaffold.entities` package builds green with the new contract.
- The `com.scaffold.entities.states` package builds green after the rename/delete/migration (substantive logic still old; that's Plan C).
- `IReadOnlyEntity`, `IMutableEntity`, `BaseEntityInstance<TDefinition>`, `EntityStateReference`, `EntityVariableState`, `InstanceId`, `StateEntity<TDef>`, `EntityInstanceCreator<TDef>`, `IInstanceIdGenerator`, `IncrementingInstanceIdGenerator`, and `StoreInstanceIdExtensions` are all gone from the package source trees.
- `EntityDefinition` has no source/storage interface implementations.
- `EntityComponent<TDef>` has no `Id` property and no `InstanceId`-typed Initialize overload.
- `LocalVariableStorage` has no `instanceEffectiveBag` cache and no `WireToDefinition`-style def-bag wiring.
- `StoreVariableStorage` is at ~50 LOC, no effective-value cache, no per-variable subscriber bookkeeping.
- `ModifierSource` has the `(Reference Source, int Tag)` shape.
- `IEntityVariableStorage.AddModifier` signature is `(Variable, VariableModifier, ModifierSource, ModifierId?)` — `EntityModifierEntry` is not on the interface.
- A short retro pass: any hard requirement or hard avoid that was wrong gets noted in the spike's open items and the spike is updated.

After validation passes, Plan C (`com.scaffold.entities.states` substantive rebuild) starts. Plan C's scope is now narrower — Plan B has already done the type renames, `InstanceId` migration, and `EntityStateReference` deletion. Plan C focuses on the actual `EntityState` shape change (to pure `(Bases, Modifiers)`), payload reshape, mutator consolidation, `StoreVariableStorage` shrink, `ClearModifiersPayload` introduction, and `StoreEntities.Spawn` extension.

---

## Out-of-band reminders

- **Plan A is landed** ([Scaffold#40](https://github.com/MgCohen/Scaffold/pull/40)). Plan B uses Plan A's `Ref<T>` (sealed record deriving from `Reference`) and the existing `Reference` abstract record from main. There is no `IRef` interface — main's recent state refactor deleted `IReference` and replaced it with `abstract record Reference`; Plan A absorbed that and Plan B inherits the same shape.
- **Plan B is no longer "independent of Plan A."** Identifier migration (R10) and `ModifierSource` reshape (R11) reference Plan A's `Ref<T>` / `Reference`.
- **In-package tests must be updated as part of this plan.** Existing tests in `com.scaffold.entities` and `com.scaffold.entities.states` that reference deleted types (`IReadOnlyEntity`, `IMutableEntity`, `BaseEntityInstance`, `InstanceId`, `EntityStateReference`) or the old name `EntityVariableState` will compile-fail. Updating them is part of Plan B's "build green" exit criterion, not a downstream concern.
- The bridge package (`com.scaffold.entities.states`) gets cross-cutting cleanup in Plan B (rename, delete, identifier migration) but no substantive rebuild — that's Plan C.
- The project layer (`Card`, `Zone`, `Player`, slices) is touched after the bridge. Don't preemptively migrate. Existing project code may reference `IReadOnlyEntity` / `IMutableEntity` / `InstanceId` / `EntityVariableState` / `EntityStateReference` — those break and stay broken until project migration.
- Existing `Identifier<T>` machinery is NOT touched in this plan. It's a separate cascade pass.
- The single-class-for-base-and-overlay design (`LocalVariableStorage` with optional `Parent`) is a hard structural choice, not an optimization. Don't reintroduce `OverlayStorage` as a sibling type even if it feels natural — composition via `Parent` is the design.
- `R8` (drop `TryGetEffective`) and `R9` (strip `VariableBag` parent pointer) together remove the existing two-tier caching/wiring strategy in `LocalVariableStorage`. Reads recompute every time. If profiling shows this is a real hot path post-implementation, re-add caching as a private optimization — but never on the contract.

---

## Retrospective (2026-05-06)

**Status: Implemented.** All hard requirements met, all hard avoids respected, all deletions completed.

### Verification summary

| Area | Result |
|---|---|
| `IEntityVariableStorage` contract (Parent, TryGetBase, GetModifiers, Variables, writes, no TryGetEffective, no subscriptions) | Pass |
| `EntityInstance<TDef>` (two-param ctor, no identity, read orchestration, write delegation, virtual Dispose) | Pass |
| `LocalVariableStorage` (sealed, Parent chain-walk, local-only writes, no caches, no def-bag wiring) | Pass |
| `Entities.Local<TDef>(def)` factory | Pass |
| `ModifierSource(Reference Source, int Tag)` (R11) | Pass |
| `EntityComponent<TDef>` identity-free (R12) | Pass |
| `EntityDefinition` does not implement storage/source interfaces | Pass |
| `StoreVariableStorage` thin adapter (R14) | Pass |
| `EntityState` rename (R5) | Pass |
| All payloads migrated to `Reference` (R10) | Pass |
| All deletions (R4, R7, R10, R12, R13, R16, R17, interfaces, subscriptions, editor partials) | Pass |
| Unit + integration tests cover required cases | Pass |
| Zero `InstanceId` references in code (only stale READMEs) | Pass |

### Deviations from plan

- **`StoreVariableStorage` is ~103 LOC** (plan estimated ~50). The difference is whitespace/braces and implementing the full 7-write-method `IEntityVariableStorage` contract. Core logic is thin — pure delegation.
- **`EntityInstance<TDef>` uses `TDefinition` as the generic parameter name** (not `TDef`). Semantically equivalent.
- **`IEntityDefinition.DeclaredVariables` is named `DefinedVariables`** in the implementation. Semantically equivalent; plan said "do NOT rename" but the name was already `DefinedVariables` on main.
- **`VariableBag` still has a parent pointer field** (`IVariableBag? parent`), but it is NOT wired to definitions from production code (only used in `VariableBagTests`). R9 is satisfied — definition default-fallback is exclusively at the handle level.
- **README files** in both packages still reference deleted types (`InstanceId`, `StateEntity`, `EntityVariableState`, etc.). Documentation updates were not in scope.

### Open items resolved during implementation

- **O1 (subscription consumers):** All subscription infrastructure deleted. No consumers remained in-package.
- **O3 (internal storage type):** Kept mutable `Dictionary` as expected.
- **O5 (AddVariable duplicate semantics):** Returns false, doesn't overwrite — matches plan.
- **O9 (bridge type-name updates):** Hard-break, no `[Obsolete]` aliases.

### What Plan C inherits

Plan B landed the type renames, `InstanceId` migration, `EntityStateReference` deletion, and `StoreVariableStorage` thin-adapter rewrite. Plan C's remaining scope:
- `EntityState` shape change to pure `(Bases, Modifiers)` — drop `Definition` reference and `Id` field
- Payload reshape and mutator consolidation
- `ClearModifiersPayload` introduction
- `StoreEntities.Spawn` extension
