---
type: spike
status: accepted
tags: [#spike, #scaffold, #entities, #state]
---

# Entities × State Unification

> **Status: accepted.** Working design for how `Scaffold.States`, `Scaffold.Entities`, and `Scaffold.Entities.States` combine in this project. Captures package-level changes plus project-level shapes. Self-contained.
>
> **Post-merge rebase notice (2026-05-05):** main shipped a state refactor that deleted the `IReference` empty marker and replaced it with `public abstract record Reference`. Plan A landed on top of that base — `Ref<T>` is now `sealed record Ref<T>(Guid Id) : Reference` and there is **no `IRef` interface**. Plan B's "Final shapes" §1 (locked, written), §3 (bridge code samples), and parts of "What changes, where" / "Open items" still reference the old `IRef : IReference` shape — those are stale and will be rebased as Plan B begins. Plan A's surface and `com.scaffold.states` row in "What changes" are already updated.

## TL;DR

Three packages are involved: `com.scaffold.states` (Redux-style immutable Store), `com.scaffold.entities` (definitions, instances, variables, modifiers), and `com.scaffold.entities.states` (existing bridge — to be **rebuilt**). The current bridge works but is noisy (~900 LOC of adapter code). This spike defines a leaner shape that:

- Adds a small **`Ref<T>` + `IRef` identity primitive** to `Scaffold.States`, backed by a `Guid`. The in-process catalog lives as a sub-API on the Store (`store.Catalog`). This subsumes the previous `InstanceId` / `IReference` / `Identifier<T>` machinery and gives any object normalized identity for free. Catalog-registered subclasses carry their own typed `Ref<TSubclass>`; the generic `EntityInstance<TDef>` base stays identity-free.
- Keeps a **single, unified** entity API (`EntityInstance<TDef>`) for all consumers. Storage is pluggable: same instance API works against in-memory storage *or* Store-backed storage.
- Trims the storage contract: reads, writes, and a `Parent` for chain-based overlays. No per-variable subscriptions. Adapter shrinks from ~422 LOC to ~50 LOC.
- Keeps `EntityState` as pure data `(Bases, Modifiers)`. The slice key carries identity; the value is just variables and modifiers. Definition lives on the live handle, not in any slice. The catalog persists across in-process rollback, so handles outlive snapshot loads without rehydration.
- Recursive `Parent` lookup is **storage-to-storage only** (overlays/ghosts). Definition fallback is the handle's job, not part of the chain.
- Modifier folding semantics are explicit: first base value found in the chain (or the definition's default if no chain base exists) anchors; modifiers across the entire chain merge by `Order` and apply to that anchor.
- Drops in-instance change events; reactivity is a substrate concern.
- Drops the `IReadOnlyEntity` / `IMutableEntity` interface tier — `EntityInstance<TDef>` plus `IRef` polymorphism is enough.

---

## Context

### The three packages

| Package | Role | Touched by this spike? |
|---|---|---|
| `com.scaffold.states` | Store, slices, mutators, payloads, snapshots, subscriptions | **Yes** — add `Ref<T>` / `IRef` / `ICatalogged`, plus an in-process `ICatalog` exposed as `Store.Catalog` (sub-property, not flat methods on `Store`). |
| `com.scaffold.entities` | Definitions, instances, variables, modifiers, modifier algebra | **Yes** — refactor `IEntityVariableStorage` (add `Parent`, drop subscriptions). Delete the `IReadOnlyEntity` / `IMutableEntity` interface tier. `EntityDefinition` no longer participates in the variable-source chain. |
| `com.scaffold.entities.states` | Bridges entities to Store | **Yes** — rebuild from scratch. ~900 LOC → ~200 LOC. |

### The current bridge's noise (what we are removing)

Today `com.scaffold.entities.states` ships:
- `EntityVariableState` (365 LOC) — the canonical immutable state, holding a live definition reference.
- `StateEntity<TDef>` (109 LOC) — a separate entity subclass for Store-backed instances.
- `StoreVariableStorage` (422 LOC) — adapter doing per-variable subscription multiplexing, effective-value caching, structural-change diffing, type-aware equality across 6 value types.
- 6 payloads + 6 mutators + factory + bridge context + ops.

The 422 LOC adapter is doing real work, but it's all in service of an `IEntityVariableStorage` contract that demands per-variable subscriptions — which the Store doesn't natively expose. **The fix is to trim the contract, not to make the adapter cleverer.**

---

## Conceptual model

```
Definition  →  authored asset, source of truth, defines bases.
                Held by reference inside an Instance; never lives in a slice.

Instance    →  flyweight on top of Definition. ONE unit/copy/element in play.
                Most instances live in the catalog (Store-backed gameplay path).
                Local-profile instances exist outside any catalog — fine, as long
                as they never need to be referenced from a slice.

Ref<T>      →  the runtime identity primitive. Wire-shaped (backed by Guid),
                equatable, stable within process. Cross-run stability requires
                the registered object to implement ICatalogged (otherwise auto-id
                is run-local). Returned by the catalog at registration. Used as
                slice keys and as values inside slices that reference the registered object.

IRef        →  non-generic marker for "anything that's a Ref". For polymorphic APIs
                that take "any addressable thing".
```

Identity is for things you have many of. Definitions are unique assets — no runtime id. Instances are what you have many of — they register and get a `Ref<T>` back.

**Invariant: slices never hold live objects.** They carry `Ref<T>` (a stable id wrapper); the catalog resolves refs to live objects. Within a process, the catalog persists across snapshot/rollback — so slice rollback doesn't invalidate refs and handles don't need rehydration.

### Why `EntityInstance` survives the refactor

A previous draft proposed dropping `EntityInstance` entirely and treating "the entity" as just a state slice. That was wrong for this project, because:

1. **Flyweight is a real domain need.** Five Fireballs in hand = one `FireballDefinition` + five `Ref<Card>`s + five live `Card` instances. The instance is the meeting point of stable identity and shared definition.
2. **One uniform API across the codebase matters.** Gameplay code shouldn't split into "places that use entity sugar" and "places that dispatch raw payloads."
3. **Storage pluggability earns its keep.** The same instance API works against `LocalVariableStorage` (in-memory, no Store) or `StoreVariableStorage` (Store-backed). The `IEntityVariableStorage` contract is the variation point.

### Definition vs Instance, in this project's terms

- `CardDefinition` — authored ScriptableObject. Consumed at construction time. May also be registered in the catalog for asset/content concerns, but the bridge doesn't require it.
- `Card` — the runtime handle. Same as the conceptual "CardInstance"; we just call it `Card`. Extends `EntityInstance<CardDefinition>` and adds typed accessors as sugar. Registered in the catalog at spawn → returns `Ref<Card>`.

---

## Locked decisions

1. **Single instance type.** `EntityInstance<TDef>` is the only entity class. No `StateEntity<TDef>` subclass. Storage choice (Local vs Store) is the only thing that varies.
2. **Drop the `IReadOnlyEntity` / `IMutableEntity` interface tier.** They existed to abstract over plain-vs-Store-backed entities; that distinction now lives in `IEntityVariableStorage`. Polymorphism uses `IRef`, `IEntityDefinition`, or `EntityInstance<TDef>` directly. (Small ADR-0005 amendment: drop the `IReadOnlyEntity ⊃ IReference` line; concretes implement `IRef` via their `Ref<T>`.)
3. **Trim the storage contract.** Reads, writes, and a single `Parent` reference for overlay chains. No subscription methods.
4. **No `Changed` event on the entity.** Reactivity is a substrate concern. Store-backed consumers use `store.Subscribe<EntityState>(ref, …)`. Local consumers can subscribe to their storage if it exposes events.
5. **Recursive lookup via `IEntityVariableStorage.Parent`** — storage-to-storage only. Read walks: own bases/modifiers → parent storage → ... → null. Enables overlays/ghosts/copies as a `Parent`-parametrized construction of the same storage class. `EntityDefinition` is **not** in the chain; the handle does definition default-fallback after the chain bottoms out.
6. **Modifier folding semantics.** Reads gather modifiers across the entire `Parent` chain and sort by the modifier's `Order` field. The first base value found in the chain anchors the calculation; if no base exists anywhere in the chain, the definition's default value anchors instead. Modifiers always apply to whichever anchor is found. **Sort is stable; ties break by chain order (self first, then parent, recursively), and within a single storage by insertion order** — consistent with the existing model's FIFO-on-equal-Order rule. This makes overlay = "ghost reflects real buffs unless explicitly overridden" with deterministic ordering for replay.
7. **Definition lives on the handle. No definition slice.** `EntityInstance<TDef>.Definition` is the typed asset the handle holds for ergonomic access. The catalog (in the Store) holds the live handle — and persists across in-process rollback — so the definition reference survives without any state-level tracking. `EntityState` is pure data: `(Bases, Modifiers)` — the slice key carries the identity.
8. **Transformation = new handle, ref-swap in slices.** Changing an entity's definition means constructing a new `Card` (or `Zone`, etc.) with the new definition, registering it (new `Ref<Card>`), and swapping the ref in whatever slice cares (e.g., `CardMapState`). Old handle remains in the catalog as orphaned entry until process exit. Within-run rollback works for free because the slice change is atomic and the old handle is still resolvable.
9. **Serialization is a separate translation layer.** `EntityState` and other slices are pure data and wire-ready. Cross-run save/load is out of scope for this spike; when it arrives, the projection layer will translate `Ref<T>` to/from a wire-shaped id and rebuild the catalog at boot.
10. **Factories hide construction.** `Entities.Local(def)`, `store.Spawn(def)`, `Card.Spawn(store, def)` are one-liners. Two-step register (`AllocateRef` + `RegisterAt`) is the underlying primitive; `Register` shorthand exists for the simple case.
11. **`Card`, `Zone`, `Player` extend `EntityInstance<TDef>`** as subclasses for sugar accessors. Same API surface as any other entity.
12. **Card registry is a side dictionary on a registry service**, not a `State` slice. Handles are object refs; the side dictionary mirrors the catalog's `Ref<Card>` → `Card` mapping for fast typed lookup. It's not snapshotted.
13. **`Ref<T>` + `IRef` are the runtime identity primitives, in `Scaffold.States`.** They subsume the previous `InstanceId`, `IReference` (ADR-0005), and `Identifier<T>` machinery for runtime gameplay code. Existing types may persist in compatibility layers but stop being load-bearing for runtime identity.
14. **Catalog persists across rollback (not in snapshot state).** `LoadSnapshot` rolls back slice contents; the catalog and its registered handles remain. Refs in restored slices still resolve to the same live handles. Cross-run save/load is the future translation concern.
15. **Slice keys are `Ref<T>`.** Same `Ref<T>` flows as values into other slices that reference the registered object — normalized state idiom. Catalog-registered subclasses (`Card`, `Zone`) hold their own typed `Ref<TSubclass>` field for ergonomic access; the generic base `EntityInstance<TDef>` does not carry a `SelfRef` (identity is a registration concern, not an entity-shape concern).
16. **Object self-identifies via `ICatalogged` when possible; auto-id fallback otherwise.** Objects implementing `ICatalogged.Key` provide the ref's underlying `Guid` (asset GUID, declared key cast to Guid, etc.). Objects without it get a fresh `Guid.NewGuid()` at register-time. Auto-id is run-local; cross-run survival requires `ICatalogged`.
17. **Catalog API lives under `Store.Catalog` (sub-property), not directly on `Store`.** Same lifetime as `Store`, scoped surface via `ICatalog`. Keeps `Store`'s own API focused on state/dispatch.
18. **`ClearModifiersPayload` is first-class.** Not synthesized client-side from `RemoveModifierPayload` batches.

---

## Ratified during plan review (2026-05-05)

These decisions resolve integration gaps between the spike's design and the current Scaffold.States codebase. They refine — but do not contradict — the locked decisions above. Most are folded directly into the "Final shapes" section; the rest are noted here.

| # | Decision | Refines / adds |
|---|---|---|
| R1 | `Store.Catalog` is added inline to the existing `Store.cs` (which is `sealed`). Not a `partial` extension. The catalog is constructed in `Store`'s existing constructor; `StoreBuilder` is untouched. | Locked #17 |
| R2 | `IRef : IReference`. Every existing `IReference`-typed Store API (`Get`, `Subscribe`, `RegisterSlice`, `Execute`, etc.) accepts `Ref<T>` automatically — no overloads. `IReference` may be retired in a later cascade pass. | new |
| R3 | `IPayloadReference.GetReference()` return type stays `IReference`. Bridge payloads with an `IRef Target` field implement it as `return Target;` — works because of R2. | new |
| R4 | Delete `BaseEntityInstance<TDefinition>`. Fold its responsibilities into `EntityInstance<TDef>` directly. | Locked #1 |
| R5 | Rename `EntityVariableState` → `EntityState`. The trimmed `(Bases, Modifiers)` shape no longer needs "Variable" in the name; rename also signals the type is gutted vs. the prior 365 LOC version. | Locked #7 |
| R6 | `StoreBuilder` is **not** modified. No `.WithCatalog()` step. The catalog is always present, constructed by `Store` itself. | Locked #17 |
| R7 | Delete `EntityStateReference`. Slices keyed directly by `Ref<T>`; the wrapper has no role. | Locked #15 |
| R8 | Drop `IEntityVariableStorage.TryGetEffective`. The handle folds modifiers on every read (`TryGetBase` + `GetModifiers`). The current `instanceEffectiveBag` cache in `LocalVariableStorage` is removed. Re-add a private cache only if profiling later requires it; do not put it back on the contract. | Locked #5 |
| R9 | Strip `VariableBag`'s parent pointer. Today `LocalVariableStorage.WireToDefinition` wires the definition's bag as a parent for default lookup. Remove that linkage. Definition default-fallback lives at the handle level. Storage `Parent` is overlays only. | Locked #5 |
| R10 | Replace `InstanceId(int)` with `Ref<T>(Guid)` everywhere — bridge payloads, factories, all current `InstanceId` usages. `InstanceId` is deleted at the end of Plan B. | Locked #13 |
| R11 | `ModifierSource` becomes `readonly struct ModifierSource(IRef Source, int Tag)`. Polymorphic source identity matches the rest of the system. | new |
| R12 | The no-key `Store.Get<TState>()` overload (which uses `Reference.Null` for global/singleton state) is preserved. Global slices coexist with ref-keyed slices. | new |
| R13 | `Register(obj)` for `ICatalogged` is idempotent on same-object: a second call returns the existing `Ref<T>`. A different object with the same `Key` throws. | Locked #16 |
| R14 | `Ref<T>.ToString()` returns `$"Ref<{typeof(T).Name}>({Id:N})"` for diagnostics. | new |

---

## Final shapes

### 1. Scaffold.States — new primitives

```csharp
// Marker for "anything that's a typed Ref". Polymorphic APIs take this when
// they don't care about the underlying type. Extends the existing IReference
// marker (R2) so every IReference-typed Store API accepts Ref<T> without
// overloads.
public interface IRef : IReference
{
    Guid Id { get; }
}

// Typed, stable handle. Returned by the catalog at registration.
public readonly struct Ref<T> : IRef, IEquatable<Ref<T>>
{
    public Guid Id { get; }
    internal Ref(Guid id) { Id = id; }                  // catalog mints normally
    public static Ref<T> FromGuid(Guid id) => new(id);  // public escape hatch for cross-run rebuild

    // equality + hashing on Id
    public override string ToString() => $"Ref<{typeof(T).Name}>({Id:N})";
}

// Optional: objects can self-identify. If implemented, the catalog uses Key as
// the ref's underlying Guid. If not implemented, the catalog mints a fresh Guid.
public interface ICatalogged
{
    Guid Key { get; }
}

// Catalog is exposed as a scoped sub-API on the Store, not as direct methods on Store.
// Same lifetime as the Store; isolated surface so Store's own API stays focused.
public interface ICatalog
{
    Ref<T> AllocateRef<T>();                    // reserve a Guid, no object yet
    void RegisterAt<T>(Ref<T> @ref, T obj);     // bind object to existing ref
    Ref<T> Register<T>(T obj);                  // shorthand: AllocateRef + RegisterAt
    T Resolve<T>(Ref<T> @ref);
    bool TryResolve<T>(Ref<T> @ref, out T obj);
    void Unregister<T>(Ref<T> @ref);            // for handles that genuinely die
}

public sealed class Store    // existing declaration, unchanged — Catalog is added inline (R1)
{
    public ICatalog Catalog { get; }    // sub-property; the catalog's API lives here
    // ... existing members
}
```

Notes:

- `Catalog` is added **inline** to the existing `Store.cs` (which is `sealed`). Not a `partial` extension. The instance is constructed inside `Store`'s existing constructor (`this.Catalog = new Catalog();`); `StoreBuilder` is untouched (R1, R6).
- `Register(obj)` for an `ICatalogged` object is **idempotent on same-object**: if `obj.Key` is already bound to the same `obj`, the existing `Ref<T>` is returned. If it's bound to a different object, `Register` throws — colliding `Key`s across distinct objects is a programmer error (R13). Non-`ICatalogged` registration mints a fresh `Guid.NewGuid()`. Auto-id is run-local — anything that needs cross-run survival must use `ICatalogged`.
- The catalog is in-process side-state on the Store. NOT included in snapshots. `LoadSnapshot` does not touch it. Refs in restored slices still resolve to live handles registered in the current run.
- Transformation, snapshot rebuild, and any other "I want this exact ref to point at a freshly-constructed object" path uses `AllocateRef` then `RegisterAt`.
- Slice access works automatically: because `IRef : IReference` (R2), every existing `IReference`-typed Store API (`Get`, `Subscribe`, `RegisterSlice`, `Execute`, etc.) accepts `Ref<T>` without overloads. The no-key `Get<TState>()` overload (using `Reference.Null` for global/singleton state) is unchanged (R12).

### 2. Scaffold.Entities — refactored

```csharp
// Storage contract. Reads (chain-walking), writes (local), optional Parent for overlay chains.
// No subscriptions. No definition reference. No IReadOnlyEntity / IMutableEntity tier.
public interface IEntityVariableStorage
{
    IEntityVariableStorage? Parent { get; }   // null at base; another storage for overlays

    // Reads — chain-walking. Definition default-fallback is handled by the handle.
    bool TryGetBase(Variable key, out VariableValue value);          // walks chain; returns first base
    IEnumerable<ActiveModifier> GetModifiers(Variable key);          // walks chain; sorted by Order
    IEnumerable<Variable> Variables { get; }                          // union across chain

    // Writes — local only. Parent storage is never mutated by writes through self.
    bool AddVariable(Variable key, VariableValue initial);
    bool RemoveVariable(Variable key);
    bool SetBaseValue(Variable key, VariableValue value);
    ModifierId AddModifier(EntityModifierEntry entry);
    bool RemoveModifier(Variable key, ModifierId id);
    void ClearModifiers();
    void RemoveModifiersFromSource(ModifierSource source);
}

// EntityDefinition stays the authored asset. It is NOT in the storage chain;
// it is consulted by the handle as the definition default-fallback after chain reads.
// VariableBag's parent pointer is stripped (R9) — definition fallback lives at the handle level.
public class EntityDefinition : IEntityDefinition
{
    public bool TryGetDefaultValue(Variable key, out VariableValue value) => /* defaults lookup */;
    public IEnumerable<Variable> DeclaredVariables => /* declared variables */;
    // ... existing authoring API
}

// The single instance handle (R4 — BaseEntityInstance<TDefinition> is deleted; its
// responsibilities — definition field, storage field, read accessors — are folded into
// EntityInstance<TDef> directly). Storage is injected. Definition lives here.
// The base class does NOT carry a SelfRef — identity is a registration concern, not an
// entity-shape concern. Subclasses (Card, Zone) that are catalog-registered declare their
// own typed Ref<TSubclass> as a field. Local-profile entities have no ref concept (correct,
// because they are not in any catalog).
public class EntityInstance<TDef> : IDisposable
    where TDef : IEntityDefinition
{
    public TDef Definition { get; }
    public IEntityVariableStorage Storage { get; }

    public EntityInstance(TDef def, IEntityVariableStorage storage)
    { Definition = def; Storage = storage; }

    // Reads — orchestrate storage chain + definition fallback + modifier folding.
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
        => TryGetVariable<T>(key, out var v) ? v : throw new KeyNotFoundException(key.ToString());

    public IEnumerable<Variable> Variables
        => Storage.Variables.Union(Definition.DeclaredVariables);

    // Writes — pure delegation to Storage.
    public bool AddVariable(Variable key, VariableValue initial)         => Storage.AddVariable(key, initial);
    public bool RemoveVariable(Variable key)                              => Storage.RemoveVariable(key);
    public bool SetBaseValue(Variable key, VariableValue value)           => Storage.SetBaseValue(key, value);
    public ModifierId AddModifier(EntityModifierEntry entry)              => Storage.AddModifier(entry);
    public bool RemoveModifier(Variable key, ModifierId id)               => Storage.RemoveModifier(key, id);
    public void ClearModifiers()                                           => Storage.ClearModifiers();
    public void RemoveModifiersFromSource(ModifierSource source)          => Storage.RemoveModifiersFromSource(source);

    public void Dispose() { /* release per-instance Store listener if applicable */ }
}

// Default in-memory storage. Single class covers BOTH the base case (Parent = null)
// and the overlay/ghost case (Parent = some other storage). No separate OverlayStorage type.
// R8: TryGetEffective is gone from the contract — folding happens at the handle.
// R9: VariableBag's parent pointer to definition is stripped — bag is local data only;
//      definition default-fallback lives at the handle, not at the storage or bag level.
public sealed class LocalVariableStorage : IEntityVariableStorage
{
    public IEntityVariableStorage? Parent { get; }

    public LocalVariableStorage(IEntityVariableStorage? parent = null) { Parent = parent; }

    // Reads:
    //   TryGetBase → self bases dict; if missing AND Parent != null, recurse Parent.TryGetBase
    //   GetModifiers → concat self modifiers + (Parent?.GetModifiers ?? []); sort by Order
    //   Variables → union of self.Bases ∪ self.Modifiers ∪ (Parent?.Variables ?? [])
    // Writes always land locally; Parent untouched.
}

// Construction sugar for the no-Store profile.
public static class Entities
{
    public static EntityInstance<TDef> Local<TDef>(TDef def) where TDef : IEntityDefinition
        => new(def, new LocalVariableStorage());   // no catalog, no ref
}
```

### 3. Scaffold.Entities.States — rebuilt thin

```csharp
// The canonical immutable record stored in the Store, one per entity ref
// (Ref<Card>, Ref<Zone>, etc., or the generic Ref<EntityInstance<TDef>> when no subclass exists).
// Pure data — no live references. Wire-ready as-is. The slice key carries the identity.
// R5: renamed from EntityVariableState → EntityState — the trimmed (Bases, Modifiers) shape
// no longer needs "Variable" in the name; the rename signals the type is gutted vs. the
// prior 365 LOC version.
// R7: EntityStateReference is deleted — slices key directly by Ref<T>.
public sealed record EntityState(
    ImmutableDictionary<Variable, VariableValue> Bases,
    ImmutableDictionary<Variable, ImmutableList<ActiveModifier>> Modifiers
) : State
{
    public static EntityState Empty
        => new(ImmutableDictionary<Variable, VariableValue>.Empty,
               ImmutableDictionary<Variable, ImmutableList<ActiveModifier>>.Empty);

    // Pure reads — operate on whatever this record holds. Definition default-fallback
    // is the handle's job, not this record's. Single-slice; no chain.
    public bool TryGetBase(Variable key, out VariableValue value);
    public IEnumerable<ActiveModifier> GetModifiers(Variable key);   // sorted by Order
    public IEnumerable<Variable> Variables { get; }                   // union of Bases / Modifiers

    // Pure functional updates
    public EntityState WithBase(Variable key, VariableValue value);
    public EntityState WithoutBase(Variable key);
    public EntityState WithVariable(Variable key, VariableValue initial);
    public EntityState WithoutVariable(Variable key);
    public EntityState WithModifier(Variable key, ActiveModifier mod);
    public EntityState WithoutModifier(Variable key, ModifierId id);
    public EntityState WithoutAllModifiers();
    public EntityState WithoutModifiersFromSource(ModifierSource source);
}

// Payloads — just data. Target is an IRef so payloads are kind-erased at the substrate
// level; the bridge mutator does the typed slice access. R3: GetReference() returns Target
// directly — type-compatible because IRef : IReference (R2). R10: InstanceId is gone;
// targets are Ref<T> (or any IRef). R11: ModifierSource now wraps an IRef, not InstanceId.
public sealed record SetBaseValuePayload         (IRef Target, Variable Var, VariableValue Value) : IPayloadReference { public IReference GetReference() => Target; }
public sealed record AddModifierPayload          (IRef Target, Variable Var, ActiveModifier Mod)  : IPayloadReference { public IReference GetReference() => Target; }
public sealed record RemoveModifierPayload       (IRef Target, Variable Var, ModifierId ModId)    : IPayloadReference { public IReference GetReference() => Target; }
public sealed record AddVariablePayload          (IRef Target, Variable Var, VariableValue Init)  : IPayloadReference { public IReference GetReference() => Target; }
public sealed record RemoveVariablePayload       (IRef Target, Variable Var)                       : IPayloadReference { public IReference GetReference() => Target; }
public sealed record ClearModifiersPayload       (IRef Target)                                     : IPayloadReference { public IReference GetReference() => Target; }
public sealed record RemoveModifiersFromSourcePayload(IRef Target, ModifierSource Source)         : IPayloadReference { public IReference GetReference() => Target; }

// One mutator class with overloads (or seven tiny mutators) — each is a 1-2 line delegation
// to the corresponding EntityState.With… method. ~60 LOC total.

// Thin adapter. No caches, no per-variable multiplexing, no events, no lifecycle bookkeeping,
// no Parent (Store-backed storages don't chain — overlays wrap them in a LocalVariableStorage).
public sealed class StoreVariableStorage : IEntityVariableStorage
{
    private readonly Store store;
    private readonly IRef target;
    private EntityState Slice => store.Get<EntityState>(target);

    public StoreVariableStorage(Store store, IRef target) { this.store = store; this.target = target; }

    public IEntityVariableStorage? Parent => null;
    public bool TryGetBase(Variable key, out VariableValue value)        => Slice.TryGetBase(key, out value);
    public IEnumerable<ActiveModifier> GetModifiers(Variable key)         => Slice.GetModifiers(key);
    public IEnumerable<Variable> Variables                                 => Slice.Variables;

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

// Spawn helper. Returns (handle, ref) since the base EntityInstance no longer carries SelfRef.
// Subclass spawn helpers (Card.Spawn, Zone.Spawn) keep the ref internally on the typed handle.
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

### 4. This project — Card layer

```csharp
public sealed class Card : EntityInstance<CardDefinition>
{
    // Card is catalog-registered, so it carries its own typed Ref<Card>.
    // The base EntityInstance<TDef> has no SelfRef.
    public Ref<Card> SelfRef { get; }

    public Card(Ref<Card> selfRef, CardDefinition def, IEntityVariableStorage storage)
        : base(def, storage)
    {
        SelfRef = selfRef;
    }

    // Sugar accessors — pure reads through the same orchestration as any other variable.
    public int   Cost   => GetVariable<int>(CardVars.Cost);
    public int   Attack => GetVariable<int>(CardVars.Attack);
    public int   Health => GetVariable<int>(CardVars.Health);
    public Actor Owner  => GetVariable<Actor>(CardVars.Owner);

    public static Card Spawn(Store store, CardDefinition def)
    {
        var r = store.Catalog.AllocateRef<Card>();
        var card = new Card(r, def, new StoreVariableStorage(store, r));
        store.Catalog.RegisterAt(r, card);
        store.AddState(r, EntityState.Empty);
        return card;
    }
}

// Same shape for Zone, Player, Equipment, etc. Each commits to its own typed Ref<TSubclass>
// as a subclass-level field; the base EntityInstance<TDef> stays identity-free.
```

Project code uses `Ref<Card>` as the slice key type and as the value type in any cross-slice reference. Identity lives on the catalog-registered subclass, not on the generic base — code that holds only a base-typed `EntityInstance<CardDefinition>` doesn't have a ref to query, which is correct (identity belongs to whoever registered the object).

### 5. This project — CardRef ↔ Ref<Card> ↔ Card mapping

Two state slices, one side dictionary, one resolution path.

```csharp
// Slice 1 (Store): rotating public ref ↔ stable Ref<Card> (privacy mechanism). Pure data.
public sealed record CardMapState(
    ImmutableDictionary<CardRef, Ref<Card>> RefToInstance,
    ImmutableDictionary<Ref<Card>, CardRef> InstanceToRef
) : State;

// Slice 2 (Store): per-entity variable state — one EntityState per Ref<Card>.

// Side dictionary (engine-side service, NOT in the Store): Ref<Card> → typed Card handle.
// Mirrors the catalog's typed view for fast lookup; the catalog already has the same mapping
// generically (store.Catalog.Resolve<Card>(r)). The registry is convenience for code that knows it
// wants a Card specifically and avoids casts.
public sealed class CardRegistry
{
    private readonly Dictionary<Ref<Card>, Card> cards = new();
    public Card Get(Ref<Card> r) => cards[r];
    public void Register(Card card) => cards[card.SelfRef] = card;
    public void Unregister(Ref<Card> r) => cards.Remove(r);
}

// Resolution:
public Card Resolve(CardRef cardRef)
{
    var r = store.Get<CardMapState>().RefToInstance[cardRef];
    return registry.Get(r);
}
```

The `Card` handle outlives `CardRef` rotations. Only the public ref changes when visibility flips; the instance and its `EntityState` slice are stable. Within-run rollback restores the slice contents; the catalog and registry are untouched, so handles remain valid.

### 6. Overlay storage — the recursion payoff

Ghost cards / preview / what-if simulations are first-class. **No new class.** An overlay is a `LocalVariableStorage` constructed with `Parent` set to the storage you want to layer on top of.

```csharp
// Same LocalVariableStorage class, parametrized by Parent.
// Ghost shares the original Card's SelfRef — it's a parallel "view" of the same identity,
// NOT a separately-registered handle. The catalog still resolves card.SelfRef to the original
// Card; the ghost is ephemeral and never enters the catalog. If the ghost needs to be
// addressable as a distinct entity (slice membership, subscription target), allocate a fresh
// Ref<Card> and register the ghost normally instead.
var ghost = new Card(card.SelfRef, card.Definition, new LocalVariableStorage(parent: card.Storage));
ghost.SetBaseValue(CardVars.Cost, IntValue.Of(0));     // local-only override

ghost.GetVariable<int>(CardVars.Cost);    // 0 — overlay wins for base; chain modifiers still apply
ghost.GetVariable<int>(CardVars.Attack);  // walks to card.Storage; reflects real buffs
ghost.GetVariable<int>(CardVars.Health);  // not in overlay or parent storage → falls back to ghost.Definition's default; chain modifiers still apply
```

Read semantics, by example. Card has base=10 Attack, +3 modifier. Ghost overlays base=5 Attack, +2 modifier. Both modifiers stack:

```
Ghost.GetVariable<int>(Attack):
  Storage.TryGetBase(Attack):
    overlay.bases[Attack] = 5  → anchor = 5
  Storage.GetModifiers(Attack):
    overlay.mods + card.Storage.mods, sorted by Order → [+3, +2]  (assume Order says +3 first)
  Apply: 5 + 3 = 8; 8 + 2 = 10
  → 10
```

The base override (5) anchors the calculation; both layers' modifiers apply in `Order`-sorted sequence. Ghost reflects real buffs unless explicitly overridden; modifiers always stack on whatever anchor wins.

---

## Consumer code, side-by-side

### Profile A — generic Scaffold.Entities consumer (no Store)

```csharp
var villager = Entities.Local(villagerDef);

int hp = villager.GetVariable<int>(Vars.Health);
villager.SetBaseValue(Vars.Health, IntValue.Of(10));
var modId = villager.AddModifier(new EntityModifierEntry(Vars.Attack, new IntAddModifier(3)));
villager.RemoveModifier(Vars.Attack, modId);

// Reactivity (if needed): subscribe to LocalVariableStorage's own event surface,
// which it MAY expose. Not part of the storage contract.
```

### Profile B — this card framework (Store-backed)

```csharp
var card = Card.Spawn(store, fireballDef);

// Read — identical surface
int cost = card.GetVariable<int>(CardVars.Cost);
int cost2 = card.Cost;                       // sugar accessor

// Mutate — identical surface
card.SetBaseValue(CardVars.Cost, IntValue.Of(2));
var modId = card.AddModifier(new EntityModifierEntry(CardVars.Cost, new IntAddModifier(-1)));

// Reactivity — substrate-native. Use the Store directly.
store.Subscribe<EntityState>(card.SelfRef, (_, _, _) => view.MarkDirty());

// Time-travel — free, at the Store level.
var snap = store.SaveSnapshot();
// ... gameplay ...
store.LoadSnapshot(snap);
// `card` handle is still valid. The catalog persists across rollback, so the handle and
// its definition reference are untouched. Reads through it return rolled-back variable
// values from the restored EntityState slice.
```

The reads/writes are identical. Only the reactivity path differs — and correctly so, because reactivity is a substrate property.

### Per-variable reactivity (rare; build on top)

If a consumer genuinely needs per-variable change callbacks, they build a small tracker once and reuse:

```csharp
public sealed class EntityVariableTracker<T>
{
    public EntityVariableTracker(
        EntityInstance<TDef> entity,
        Variable key,
        Action<Action> subscribe)   // caller supplies the substrate's subscription mechanism
    {
        T last = entity.GetVariable<T>(key);
        subscribe(() => {
            var current = entity.GetVariable<T>(key);
            if (!EqualityComparer<T>.Default.Equals(current, last))
            {
                last = current;
                Changed?.Invoke(current);
            }
        });
    }
    public event Action<T> Changed;
}

// Wired up at call site:
new EntityVariableTracker<int>(card, CardVars.Cost,
    cb => store.Subscribe<EntityState>(card.SelfRef, (_, _, _) => cb()));
```

Lives in the consumer project, not in any Scaffold package.

---

## What changes, where

### `com.scaffold.states` (additive)

| Change | Reason |
|---|---|
| Add `Ref<T>` (`sealed record` wrapping `Guid`, deriving from `Reference`); no `IRef` marker — `Reference` is the polymorphic base on main (R2, rebased) | Typed identity primitive used as slice keys and as cross-slice reference values; existing `Reference`-typed Store APIs accept `Ref<T>` for free |
| Add `ICatalogged` for objects that self-identify (returns `Guid`) | Stable across-run keys for objects with natural identity (asset GUID, declared key) |
| Add `ICatalog` and expose as `Store.Catalog` (inline property in existing `Store.cs`, not partial; R1, R6) | Catalog API scoped under its own surface; Store stays sealed and `StoreBuilder` is untouched |
| Catalog state is **not** snapshotted | Snapshots roll back slice contents; catalog and live handles remain |
| `IPayloadReference.GetReference()` returns `Reference` (R3, rebased) | Payloads with `Ref<T> Target` plug into existing mutator wiring via `return Target;` because `Ref<T> : Reference` |
| No-key `Get<TState>()` overload preserved (R12) | Global/singleton slices keyed by `Reference.Null` continue to work alongside ref-keyed slices |

### `com.scaffold.entities` (refactor)

| Change | Reason |
|---|---|
| Add `Parent` + chain-walking reads (`TryGetBase`, `GetModifiers`) to `IEntityVariableStorage` | Overlays/ghosts as `Parent`-parametrized construction; deterministic modifier folding across chain |
| Drop `TryGetEffective` from `IEntityVariableStorage` (R8) | Handle folds on every read; effective-value caching is dropped — re-add as private optimization only if profiling demands |
| Remove `Subscribe(Variable, …)` and `SubscribeToVariableStructuralChanges(...)` from `IEntityVariableStorage` | Reactivity is substrate-specific |
| Delete `BaseEntityInstance<TDefinition>` (R4) | Single `EntityInstance<TDef>` class absorbs definition field, storage field, read accessors |
| Delete `IReadOnlyEntity` / `IMutableEntity` interfaces | Single `EntityInstance<TDef>` class makes them redundant; polymorphism uses `Reference` (or the typed `Ref<T>` handle) |
| Strip `VariableBag`'s parent pointer (R9) | Definition default-fallback is handle-level only; storage `Parent` is overlays only — single layering mechanism |
| `EntityDefinition` stays a definition — does **not** implement any source/storage interface | Definition is consulted by the handle, not part of the storage chain |
| Simplify `LocalVariableStorage`: optional `Parent` ctor arg, drops subscription bookkeeping, drops effective-value cache | Single class covers base + overlay |
| `EntityInstance<TDef>` orchestrates: chain reads → definition fallback → modifier application | Default-value lookup lives at one well-defined level; modifier semantics are explicit |
| `ModifierSource` reshape: `(Reference Source, int Tag)` (R11, rebased) | Polymorphic source identity matches the rest of the system; `InstanceId` is gone |
| Replace `InstanceId(int)` with `Ref<T>(Guid)` everywhere; delete `InstanceId` at end of Plan B (R10) | Single identity primitive across runtime gameplay code |
| Add `Entities.Local<TDef>(def)` factory | One-liner construction, no Store required |

### `com.scaffold.entities.states` (rebuild)

| Change | Reason |
|---|---|
| Delete `StateEntity<TDef>` | `EntityInstance<TDef>` + `StoreVariableStorage` covers it |
| Delete current `StoreVariableStorage` (422 LOC) | Replaced by ~50 LOC thin adapter |
| Rename `EntityVariableState` → `EntityState` (R5) and reshape to pure `(Bases, Modifiers)` — drop the `Definition` reference and the `Id` field (key is the slice key, not duplicated in the value) | Wire-ready immutable record; definition lives on the handle; rename reflects the new shape |
| Delete `EntityStateReference` (R7) | Slices keyed directly by `Ref<T>`; the wrapper has no role |
| Add `ClearModifiersPayload` + matching mutator and `EntityState.WithoutAllModifiers` | First-class clear operation; replaces client-side batch synthesis |
| Payloads target `IRef` (not a typed `InstanceId`) — `GetReference() => Target` (R3, R10) | Substrate-level kind-erasure; bridge mutator does typed slice access; works because `IRef : IReference` |
| Modifier folding walks the full `Parent` chain and merges by `Order`; first chain base or definition default anchors | Deterministic stacking; explicit modifier semantics |
| Delete `EntityStateFactory`, `EntityBridgeContext`, `StoreInstanceIdExtensions`, `StateEntityOps` (folded into `StoreEntities` extension class) | Consolidation |
| Add `store.Spawn<TDef>(def)` extension | One-liner construction; uses two-step register internally |

### This project (consume)

| Change | Reason |
|---|---|
| `Card`, `Zone`, `Player` extend `EntityInstance<TDef>` directly | Unified API |
| Sugar accessors (`Card.Cost`, etc.) call `GetVariable<T>(CardVars.X)` | Discoverability |
| `Card.Spawn(store, def)` static factory | Construction sugar |
| `CardMapState` slice for `CardRef ↔ Ref<Card>` | Privacy mechanism |
| `CardRegistry` side dictionary (engine service) for `Ref<Card> → Card` typed lookup | Mirrors catalog typed view; convenience for project code |
| Reactivity goes through `store.Subscribe<EntityState>(card.SelfRef, …)` | Idiomatic Store usage |
| Build `EntityVariableTracker<T>` helper if per-variable reactivity needed | Opt-in convenience, lives in consumer code |
| ADR-0005 amendment: drop the `IReadOnlyEntity ⊃ IReference` line; concretes expose `Ref<TSubclass> SelfRef` (an `IRef`) for addressable use cases | Interface tier removed; identity migrated to Scaffold primitive |

---

## Open items / decide later

1. **Cross-run save/load adapter.** Out of scope. With slices already `Ref<T>`-keyed (and refs backed by `Guid`), the adapter mostly handles catalog rebuild at boot — registering objects under their previously-used Guids via `AllocateRef` / `RegisterAt`.
2. **`LocalVariableStorage` event surface.** Each impl decides what it exposes (since not on contract). Probably an `event Action<Variable>` or `event Action` for in-memory consumers that want reactivity. Not specified here.
3. **Non-generic handle interface.** Skipped for now. If generic rebuild/inspector code eventually wants "any catalog-registered entity handle," add a small marker like `IRegisteredEntity { IRef SelfRef; IEntityDefinition Definition; IEntityVariableStorage Storage; }` — implemented by Card, Zone, etc. (not by the base `EntityInstance<TDef>`).
4. **Catalog telemetry / debug introspection.** Useful: list all Refs of type T, dump catalog contents, etc. Implementer decides; not part of the contract.
5. **`Unregister` semantics for orphaned transformation handles.** Within-run, transformation creates a new handle and leaves the old one in the catalog. Across very long sessions, that accumulates. A periodic cleanup pass or explicit `Unregister` at well-known lifecycle moments may be needed. Defer until the cardinality matters.

---

## Estimated impact

| Bucket | Before | After |
|---|---|---|
| `com.scaffold.entities.states` total LOC | ~900 | ~200 |
| Number of types in bridge | 18+ | ~10 (counting the slice, payloads, mutators, adapter, spawn helper) |
| Adapter (`StoreVariableStorage`) LOC | 422 | ~50 |
| Hops per mutation | 5–6 | 3 |
| Special "Store-backed entity" class | `StateEntity<TDef>` | none — plain `EntityInstance<TDef>` |
| Entity-handle interface tier | `IReadOnlyEntity` + `IMutableEntity` (+ subtypes) | gone — `EntityInstance<TDef>` + `Ref<TDef>` (which is a `Reference`) |
| `EntityState` shape | `(Id, Definition, Bases, Modifiers)` — holds live ref | `(Bases, Modifiers)` — pure data, wire-ready |
| Definition assignment record | inside `EntityState` (live ref) | none — definition lives on the handle, catalog persists across rollback |
| Modifier ordering across overlays | unspecified | merged by `Order` across the full `Parent` chain; first chain base or definition default anchors |
| Storage classes for base + overlay | 2 (`Local…`, `Overlay…`) | 1 (`LocalVariableStorage`, parametrized by `Parent`) |
| `ClearModifiers` operation | client-side batch of `RemoveModifierPayload`s | first-class `ClearModifiersPayload` |
| Card registry | undecided (state slice vs side dict) | side dict on engine service; mirrors catalog |
| Runtime identity primitive | `InstanceId` + `IReference` + `Identifier<T>` (parallel mechanisms) | `Ref<T> : Reference` (single Scaffold primitive; `IReference` already removed in main's state refactor) |
| Snapshot rehydration of handles | engine responsibility (rebuild from slices) | none — catalog persists across rollback |
| Per-variable subscription multiplexing | Built into adapter | Opt-in helper at call sites |
