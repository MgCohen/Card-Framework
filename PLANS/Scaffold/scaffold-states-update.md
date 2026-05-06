---
type: plan
status: proposed
tags: [#scaffold, #states, #catalog, #ref]
---

# Scaffold.States — `Ref<T>` + Catalog primitive

> **Plan A** of the Scaffold update sequence. Adds a typed-ref + in-process catalog primitive to `com.scaffold.states`. Pure additive — no removals, no breaking changes to existing APIs in this plan. Plan B (Scaffold.Entities refactor) depends on this landing first.

**Source spike:** [`Product/spikes/entities-state-unification.md`](../../Product/spikes/entities-state-unification.md) — locked decisions #13, #14, #15, #16, #17; final shapes §1.

---

## Rebase notes (2026-05-05, post-merge)

Plan was originally drafted against an `IReference` empty marker. Main has since merged a refactor that **deletes `IReference`** and replaces it with `public abstract record Reference` (sealed inner `NullReference`). Slice APIs (`Get`, `Subscribe`, `RegisterSlice`, `Execute`, etc.) and `IPayloadReference.GetReference()` now take `Reference` directly. `Map<Reference, Type, Slice>` keys on `Reference`'s record value equality. The `Store` ctor now also has a second overload accepting `IMutatorDispatcher?`.

Net effect on this plan: **simpler.** The marker-chain scaffolding (`IRef : IReference`) the plan was inventing is replaced by ordinary inheritance — `sealed record Ref<T>(Guid Id) : Reference`. Record auto-generates equality, hash, and ctor. The non-generic `IRef` interface is dropped: `Reference` is already the polymorphic base. The one substantive cost: `Ref<T>` becomes a record class (heap allocation per ref), not a struct. Acceptable at project scale; revisit only if profiling surfaces an issue.

Affected sections below: TL;DR, Scope, Hard requirements (1, 3, 4, 10), Hard avoids (2, 5, 8), §1 (deleted), §2, §5, §6 (collapsed), validation table, open items. Decisions table re-ratified inline with **Status** column.

---

## Decisions ratified during review (2026-05-05)

These resolve gaps surfaced in plan review against the current Scaffold.States codebase. Changes folded into the relevant sections below; Plan B-bound items noted in "Out-of-band reminders."

| # | Decision | Affects | Status |
|---|---|---|---|
| 1 | Inline `Catalog` property in `Store.cs`. No `partial` declaration. | §5 | Stands |
| 2 | ~~`IRef : IReference`~~ → **`Ref<T> : Reference` directly.** No `IRef` interface. `Reference` is already the polymorphic base on main. | §1, §6 | **Rebased** |
| 3 | `IPayloadReference.GetReference()` (now returns `Reference`) — payloads with `Ref<T> Target` field `return Target;`, type-compatible because `Ref<T> : Reference`. | §6 | **Rebased** |
| 4 | (Plan B) Delete `BaseEntityInstance<TDefinition>`; fold into `EntityInstance<TDef>`. | Plan B | Stands |
| 5 | (Plan B) Rename `EntityVariableState` → `EntityState`. | Plan B | Stands |
| 6 | `Store` constructs its own `Catalog()` internally. `StoreBuilder` untouched (no `.WithCatalog()` step). Wire into **both** ctor overloads (the second now takes `IMutatorDispatcher?`). | §5 | **Updated** |
| 7 | (Plan B) Delete `EntityStateReference`. Slices keyed directly by `Ref<T>`. | Plan B | Stands |
| 8 | (Plan B) Drop `IEntityVariableStorage.TryGetEffective`. Handle folds modifiers on every read. | Plan B | Stands |
| 9 | (Plan B) Strip `VariableBag` parent pointer. Definition default-fallback is handle-level only. Storage `Parent` is overlays only. | Plan B | Stands |
| 10 | (Plan B) Replace `InstanceId` with `Ref<T>` everywhere. `InstanceId` deleted at end of Plan B. | Plan B | Stands |
| 11 | (Plan B) `ModifierSource(Reference Source, int Tag)` (was `IRef Source`). | Plan B | **Rebased** |
| 12 | No-key `Store.Get<TState>()` overload untouched. `Reference.Null` continues to serve global/singleton slices. | §6 | Stands |
| 13 | `Register(obj)` for `ICatalogged` is idempotent on same-object; throws on different-object-same-Key. | §4 | Stands |
| 14 | `Ref<T>.ToString()` format. Record default is `Ref { Id = ... }`; the plan's preferred format is `$"Ref<{typeof(T).Name}>({Id:N})"`. Override `ToString()` to keep the preferred format. | §2 | **Updated** |

---

## TL;DR

Add `Ref<T>` (a `sealed record` wrapping a `Guid`, deriving from the existing `Reference` abstract record), `ICatalogged`, and an `ICatalog` interface exposed as `Store.Catalog`. The catalog API: `AllocateRef`, `RegisterAt`, `Register`, `Resolve`, `TryResolve`, `Unregister`. Catalog state is in-process side-state on the `Store`; **not** included in snapshots. Slice access already keys on `Reference`, so `Ref<T>` flows in for free.

Goal: any object can be registered with the Store's catalog, returns a stable typed handle (`Ref<T>`), resolvable back to the object. Refs flow into slices as keys and as values; the catalog persists across `LoadSnapshot` so refs in restored slices stay valid.

---

## Scope

### In scope (this plan)
- New types: `Ref<T>`, `ICatalogged`, `ICatalog`.
- `Store.Catalog` sub-property exposing `ICatalog`.
- Snapshot save/load contract: catalog untouched (verified, not implemented — already true by construction).

### Out of scope (deferred)
- Removing `Identifier<T>` family or any existing key types. Coexist for now.
- Wire-form serialization of refs (cross-run save/load).
- Catalog telemetry / introspection (`list all Refs of type T`, dumps).
- Periodic cleanup of orphaned entries (e.g., from transformation ref-swap).
- Thread-safety guarantees beyond what `Store` already provides.
- Anything in Scaffold.Entities (Plan B) or the bridge (Plan C).

---

## Hard requirements

1. **`Ref<T>` is a `sealed record class` deriving from `Reference`.** Record auto-generates value-equality, hash, ctor, and copy semantics. One heap allocation per ref at construction time; reference copies are pointer-cheap thereafter. (The earlier "must be a struct" rule is dropped — it was scaffolding for the previous `IReference` design.)
2. **`Ref<T>` wraps a `Guid` directly.** No separate id wrapper type. The `Guid` is the underlying stable id — auto-id minted via `Guid.NewGuid()`, asset/declared keys provided as `Guid` via `ICatalogged`.
3. **`Ref<T>` equality follows record default: `(EqualityContract, Id)`.** Two `Ref<T>` with the same `Guid` and same `T` equate. Two `Ref<Foo>` and `Ref<Bar>` with the same `Guid` do NOT equate (different `EqualityContract`). This is stricter than the original "Guid only" rule and is intentional — typed slice keys for the same Guid under different `T` are distinct slices.
4. **`Reference` is the polymorphic base.** Already shipped on main. Polymorphic APIs (slice keys, payload targets) accept `Reference`. `Ref<T>` flows in for free.
5. **Catalog is in-process side-state, exposed via `Store.Catalog`** (sub-property of type `ICatalog`). NOT a flat addition of methods to `Store`. Same lifetime as `Store`.
6. **Catalog is NOT included in `SaveSnapshot`. `LoadSnapshot` does not touch it.**
7. **Refs in restored slices resolve correctly after `LoadSnapshot`.** Because the catalog persists, the registered objects are still there. This is the load-bearing rollback story.
8. **`Register` derives the ref's `Guid` from `ICatalogged.Key` when the object implements it; mints `Guid.NewGuid()` otherwise.** Auto-id is run-local; objects needing cross-run survival must implement `ICatalogged`.
9. **Two-step registration is the underlying primitive.** `AllocateRef<T>()` reserves a Guid; `RegisterAt<T>(ref, obj)` binds the object. `Register<T>(obj)` is the shorthand for the simple case. Two-step is required for construction patterns where the object needs its own ref at construction time (entity spawn).
10. **Slice access (`Get<TState>`, `Subscribe<TState>`, `RegisterSlice`, `Execute`) accepts `Reference`.** Already true on main. `Ref<T> : Reference` slots in directly. No new overloads, no signature changes.
11. **`ICatalogged.Key` returns `Guid`.** No abstraction layer.

---

## Hard avoids

1. **Do NOT include catalog state in `SaveSnapshot` / `LoadSnapshot`.**
2. **Do NOT add a struct wrapper layer to keep `Ref<T>` a value type.** Record class is the agreed shape on top of the merged `Reference` base. Allocation cost is acceptable for project scale; struct-wrapping adds boxing/indirection on every slice op for hypothetical perf wins that haven't been measured.
3. **Do NOT introduce a `StableId` (or similar) wrapper type.** `Ref<T>` wraps `Guid` directly. If we later need mixed key shapes, introduce a wrapper *then*.
4. **Do NOT introduce `AnonymousRef` or any other "fake ref" type.** Identity is a registration concern. Things not registered have no ref. (See Plan B for how `EntityInstance<TDef>` base handles the no-catalog case: it doesn't carry a SelfRef at all.)
5. **Do NOT couple `Ref<T>` equality to anything beyond `(EqualityContract, Id)`.** Don't override `Equals` to ignore `T` (would create surprising slice-key collisions across types), don't override to compare more fields (none exist).
6. **Do NOT add catalog methods directly to `Store`.** The catalog API lives under `Store.Catalog` (an `ICatalog` property). This is a hard structural choice.
7. **Do NOT auto-generate `Guid` non-deterministically when `ICatalogged` is implemented.** Auto-id is the fallback path only. If `ICatalogged.Key` is provided, that's the ref's Guid.
8. **Do NOT remove or rename existing identity types in this plan.** `Identifier<T>` / `InstanceId` stay. (`IReference` is already gone — removed in the recent main refactor, not this plan.) Migration of remaining types is a later cascade pass.
9. **Do NOT add subscription-on-catalog-changes machinery.** The catalog is a data store. Reactivity is at the slice level, not the catalog level.
10. **Do NOT auto-Unregister.** Catalog entries are explicit-lifetime. Implementer decides when to call `Unregister`.

---

## Deliverables — concrete shapes

### 1. ~~`IRef`~~ — REMOVED

No non-generic ref interface. `Reference` (the existing abstract record on main) already plays the polymorphic-base role. `Ref<T>` derives from `Reference` directly.

### 2. `Ref<T>`

```csharp
public sealed record Ref<T>(Guid Id) : Reference
{
    public override string ToString() => $"Ref<{typeof(T).Name}>({Id:N})";
}
```

**Notes:**
- Record auto-generates `Equals(Ref<T>)`, `Equals(object)`, `GetHashCode`, `==`, `!=`, copy ctor, deconstructor. The plan's previous hand-rolled equality vanishes.
- Equality is on `(EqualityContract, Id)` — same `Guid` + same `T` → equal; different `T` → not equal.
- Public ctor (auto-generated from positional record syntax). Catalog gatekeeping is convention, not language-enforced — anyone can `new Ref<Foo>(guid)`. Acceptable: the plan's "internal ctor + FromGuid escape hatch" was an artifact of the struct era; with the record class, direct construction is fine and the catalog stays the meaningful identity authority via `Register`/`Resolve`.
- `ToString()` is overridden to keep the format from review #14 (`Ref<Foo>(abc...)` rather than `Ref { Id = abc... }`).

### 3. `ICatalogged`

```csharp
public interface ICatalogged
{
    Guid Key { get; }
}
```

Optional. Objects implementing this provide the catalog's stable id. Use cases:
- ScriptableObject definitions returning the asset GUID via `AssetDatabase.AssetPathToGUID(...)` cast to `Guid`.
- Named entities returning a declared key cast to `Guid` (e.g., GUID derived from a stable string).
- Anything with a natural cross-run-stable identity.

Objects NOT implementing this get an auto-id (`Guid.NewGuid()`) from the catalog at register-time.

### 4. `ICatalog`

```csharp
public interface ICatalog
{
    // Two-step register — for construction patterns that need the ref before the object is fully built.
    Ref<T> AllocateRef<T>();
    void RegisterAt<T>(Ref<T> @ref, T obj);

    // One-step shorthand — derives Guid from ICatalogged.Key if present, else mints Guid.NewGuid().
    Ref<T> Register<T>(T obj);

    // Resolution.
    T Resolve<T>(Ref<T> @ref);                          // throws if not found
    bool TryResolve<T>(Ref<T> @ref, out T obj);

    // Explicit lifetime end.
    void Unregister<T>(Ref<T> @ref);
}
```

**Behavioral requirements:**
- `AllocateRef<T>()`: reserves a fresh `Guid`; returns `new Ref<T>(id)`. The catalog stores `(ref, typeof(T), AllocatedSentinel)` — `TryResolve` returns false until `RegisterAt` lands.
- `RegisterAt<T>(ref, obj)`: looks up `(ref, typeof(T))` and binds `obj` to that slot. Throws if no entry exists at `(ref, typeof(T))` (either never allocated, or allocated under a different `T`), or if a different object is already bound (idempotent re-binds of the same object are fine).
- `Register<T>(obj)`:
  - If `obj is ICatalogged c`, use `c.Key` as the `Guid`.
    - If `(new Ref<T>(Key), typeof(T))` is already bound to **the same `obj`**, return the existing `Ref<T>` (idempotent).
    - If it is bound to a **different** object, throw — different objects with colliding `Key` is a programmer error.
  - Else, mint via `Guid.NewGuid()`.
  - Insert at `(ref, typeof(T), obj)`, return `new Ref<T>(id)`.
- `Resolve<T>(ref)`: looks up `(ref, typeof(T))`. Throws (`KeyNotFoundException` or similar) if no entry under that exact `(ref, T)` pair. Returns the object cast to `T`. **Type enforcement is automatic** — passing a `Ref<U>` registered under `T ≠ U` produces a key miss, not a silent cast.
- `TryResolve<T>(ref, out obj)`: false-with-default if no entry under `(ref, typeof(T))`.
- `Unregister<T>(ref)`: removes `(ref, typeof(T))`. Subsequent `Resolve` fails. Idempotent (no-op on already-removed).

### 5. `Store.Catalog`

```csharp
public sealed class Store    // existing declaration, unchanged
{
    public ICatalog Catalog { get; }    // inline addition
    // ... existing members
}
```

**Notes:**
- Property is added inline to the existing `Store.cs`. **Not** a partial class extension — `Store` stays `sealed`.
- The catalog instance is constructed inside the `Store` constructor (`this.Catalog = new Catalog();`). `Store` has two ctor overloads now (the second adds `IMutatorDispatcher?`); the simple overload chains to the full one, so the catalog is initialized exactly once in the full ctor.
- `StoreBuilder` is untouched — no `.WithCatalog()` step; if a swap reason ever appears (test spy, telemetry decorator), promote then.
- Same lifetime as the `Store`. The property is read-only — no runtime swap.
- Implementation holds the catalog state inside the concrete `ICatalog` impl, behind the interface. Not visible at the API surface.

**Internal storage:** `Map<Reference, Type, object>` (the same composite-key primitive `Store` uses for slices). Primary key is the ref, secondary key is `typeof(T)` from the registering call, value is the bound object (or an `AllocatedSentinel` for `AllocateRef`-only entries). Type-mismatch enforcement comes free from the secondary key — `Resolve<U>` of a `Ref<U>` registered as `T` produces a key miss. No bespoke `Dictionary<Guid, (Type, object)>` bookkeeping.

### 6. Slice access

**Already satisfied — nothing to do.** Slice APIs on main already key on `Reference`:

- `Get<TState>(Reference?)`, `TryGet<TState>(Reference?, out TState)`
- `Subscribe<TState>(Reference, Action<…>)` (and overloads)
- `RegisterSlice(Reference?, State)`, `UnregisterSlice<TState>(Reference?)`
- `Execute<TPayload>(Reference?, TPayload)`, `ExecuteMutator<TState>(Reference?, …)`
- `RegisterAggregate(Reference?, IAggregateProvider)`, `UnregisterAggregate<TAggregate>(Reference?)`

`Ref<T>` derives from `Reference`, so it slots in as a key for free. The internal `Map<Reference, Type, Slice>` keys on `Reference`'s record value equality, which is the right thing for `Ref<T>`.

The no-key `Get<TState>()` overload (which uses `Reference.Null` for global/singleton state) is untouched. Global slices keyed by `Reference.Null` continue to work alongside ref-keyed slices.

`IPayloadReference.GetReference()` returns `Reference`. Payloads with a `Ref<T> Target` field implement `GetReference()` as `return Target;` — type-compatible because `Ref<T> : Reference`.

### 7. Snapshot contract

`SaveSnapshot()` produces a snapshot that does NOT contain catalog state.
`LoadSnapshot(snap)` restores slice contents and does NOT touch the catalog.

This is true by construction: `Snapshot` is `Map<Reference, Type, State>` and the catalog is state on `Store`, not on `Snapshot`. Adding the catalog won't change either type's shape, so the invariant holds; tests just need to assert it.

**Test invariant:** register an object, take a snapshot, register another object, load the snapshot. Both objects still resolve (the second one because the catalog wasn't rolled back).

---

## Implementation order

1. **`Ref<T>`** — pure additive. Standalone. Single file.
2. **`ICatalogged`** — interface only, no logic.
3. **`ICatalog`** — interface declaration only (no impl yet).
4. **Catalog implementation behind `ICatalog`** — internal dictionary + the six methods. Hardest piece.
5. **`Store.Catalog` property** — wire the impl into both `Store` ctor overloads.
6. **Snapshot contract** — verify `SaveSnapshot` / `LoadSnapshot` do not touch the catalog. No code change expected; assert via test.

Steps 1–3 can land in one PR (small additive types). Step 4–5 in another PR (catalog impl + Store wiring + tests). Step 6 verified within step 4–5's PR.

(The previous "step 6: slice access accepts `IRef`" is gone — already satisfied by `Ref<T> : Reference`.)

---

## Validation

### Unit tests (required)

| Test | Asserts |
|---|---|
| `Register_NonICatalogged_AssignsAutoId` | Auto-Guid minted; `Resolve` returns the registered object |
| `Register_ICatalogged_UsesKey` | `Ref<T>.Id` equals the `ICatalogged.Key` value |
| `Register_TwoEquivalentICatalogged_ReturnsSameRef` | Same `Key` → same `Guid` → equal `Ref<T>` |
| `Register_TwoNonICatalogged_ReturnsDifferentRefs` | Auto-Guids are unique within process |
| `Register_ICatalogged_DifferentObjectSameKey_Throws` | Programmer-error path is rejected |
| `AllocateThenRegisterAt_ResolvesSuccessfully` | Two-step path works |
| `RegisterAt_BeforeAllocate_Throws` | Can't bind to a ref that wasn't allocated |
| `RegisterAt_DifferentObjectAtBoundRef_Throws` | Re-binding to a different object is rejected |
| `RegisterAt_SameObjectAtBoundRef_Idempotent` | Re-binding to the same object is fine |
| `Resolve_UnregisteredRef_Throws` | After `Unregister`, `Resolve` fails |
| `TryResolve_UnregisteredRef_ReturnsFalse` | `TryResolve` returns false-with-default |
| `Resolve_RegisteredUnderDifferentT_ReturnsFalse` | `Register<Card>(card)` then `TryResolve<Zone>(new Ref<Zone>(card.Self.Id), ...)` returns false — `(ref, typeof(T))` secondary key disambiguates |
| `RegisterAt_AllocatedUnderDifferentT_Throws` | `AllocateRef<Card>()` then `RegisterAt(new Ref<Zone>(allocated.Id), zoneObj)` throws — no entry at `(Ref<Zone>(g), typeof(Zone))` |
| `RefEquality_SameGuidSameT_AreEqual` | `Ref<Foo>(g)` equates `Ref<Foo>(g)` |
| `RefEquality_SameGuidDifferentT_NotEqual` | `Ref<Foo>(g)` does NOT equate `Ref<Bar>(g)` (record `EqualityContract` discriminates) |
| `RefHash_SameGuidSameT_SameHash` | Hash codes consistent with equality |
| `RefAsReference_FlowsIntoSliceAPIs` | `store.RegisterSlice(someRef, fooState)` then `store.Get<FooState>(someRef)` returns the state — no overloads, just polymorphism |
| `Snapshot_DoesNotContainCatalog` | Snapshot type has no catalog state in it (structural assertion) |
| `LoadSnapshot_DoesNotTouchCatalog` | After `LoadSnapshot`, refs registered between save and load still resolve |
| `LoadSnapshot_ReverseDirection_RefsStillResolve` | After `LoadSnapshot` rolling back across registrations, originally-registered refs still resolve |
| `Store_Catalog_PropertyAlwaysSet` | `store.Catalog` is non-null after `Store` construction (both ctor overloads) |

### Integration tests (recommended)

- Slice value containing refs: store a slice that contains `Ref<T>` as a value; snapshot/rollback; verify slice value's ref still resolves.
- A small end-to-end: register two objects, store a slice keyed by one and referencing the other in a value, snapshot, register a third, load — original two still resolve, third does not appear in any slice but is still in the catalog.
- Payload `IPayloadReference`: a payload with `Ref<T> Target` returns it via `GetReference()` and `Execute(payload)` routes to the keyed slice.

### Property tests (nice-to-have, not required)

- For all `(Guid a, Guid b)`: `new Ref<T>(a) == new Ref<T>(b)` iff `a == b`.
- Register/Unregister cycles maintain catalog consistency.

---

## Open / decide-during-impl

1. ~~**Internal catalog storage shape.**~~ **Resolved (post-review, 2026-05-05):** `Map<Reference, Type, object>` from `Scaffold.Maps`. Reuses the slice-store primitive; type enforcement is automatic via the secondary key.
2. ~~**`Store` already partial?**~~ **Resolved (review #1):** add the `Catalog` property inline to the existing `Store.cs`; do not introduce a partial declaration. `Store` stays `sealed`.
3. ~~**`Ref<T>` syntax — `readonly record struct` vs. hand-rolled.**~~ **Resolved by rebase:** record class deriving from `Reference`. No struct option.
4. **Concurrency.** Single-threaded sim assumed. If `Store` is already thread-safe, mirror that on the catalog (lock or `ConcurrentDictionary`). Otherwise, no extra guarantees.
5. **`Unregister` while ref is referenced from a slice.** No automatic cleanup. The catalog entry is gone; slice values referencing it become dangling (`TryResolve` returns false). Document the hazard; don't try to prevent it.
6. **`ICatalog` impl name.** `Catalog` (concrete class) or something more specific (`StoreCatalog`, `InProcessCatalog`). Implementer's call.
7. **Sentinel for "allocated-not-bound" entries.** A dedicated `private static readonly object AllocatedSentinel = new();` stored as the value at `(ref, typeof(T))` until `RegisterAt` overwrites it. `TryResolve` treats the sentinel as "not present."

---

## What "validate" means before moving to Plan B

Plan A is validated when:

- All "required" unit tests above pass.
- All "recommended" integration tests pass.
- An ad-hoc REPL test: register an arbitrary object, store its ref in a slice, snapshot, register more, load, ref still resolves.
- The Scaffold.States build is green and existing tests are unaffected.
- A short retro pass on this plan: any hard requirement or hard avoid that was wrong gets noted in the spike's open items, and the spike is updated.

After validation passes, Plan B (Scaffold.Entities refactor) starts.

---

## Out-of-band reminders

- The bridge package (`com.scaffold.entities.states`) is touched in Plan C, not Plan B and not here. Don't preemptively change bridge code while implementing this plan.
- The project layer (`Card`, `Zone`, `Player`, slices) is touched after the bridge. Same rule — don't preemptively migrate.
- Existing `Identifier<T>` / `InstanceId` machinery stays untouched in this plan. (`IReference` is already gone — main's recent state refactor removed it; nothing for Plan A to do there.) Migration of `Identifier<T>` / `InstanceId` is a later cascade pass after Plans A, B, C all land.
- Plan B (Scaffold.Entities) will NOT use `AnonymousRef` — `EntityInstance<TDef>` base has no `SelfRef`. Identity belongs to catalog-registered subclasses (`Card`, `Zone`), not the base class. Plan A does not produce or expose any "fake ref" types.

### Inherited by Plan B (ratified during Plan A review, 2026-05-05; rebased post-merge)

These were decided while reviewing Plan A but apply to Plan B's scope. Plan B's author should treat them as locked inputs:

- **Delete `BaseEntityInstance<TDefinition>`** ([file](../../Assets/Packages/com.scaffold.entities/Runtime/Core/Instance/BaseEntityInstance.cs)). Fold its responsibilities (definition field, storage field, read accessors) into `EntityInstance<TDef>` directly. Single class.
- **Rename `EntityVariableState` → `EntityState`.** The trimmed `(Bases, Modifiers)` shape no longer needs "Variable" in the name; the rename also signals the type is gutted vs. the old 365 LOC version.
- **Delete `EntityStateReference`** ([file](../../Assets/Packages/com.scaffold.entities.states/Runtime/EntityStateReference.cs)). With slices keyed directly by `Ref<T>`, the wrapper has no role.
- **Drop `IEntityVariableStorage.TryGetEffective`.** The handle folds modifiers on every read; storage exposes `TryGetBase` + `GetModifiers` only. The current `instanceEffectiveBag` cache in `LocalVariableStorage` goes away. Re-add a private cache only if profiling later shows it's needed; do not re-introduce it on the contract.
- **Strip `VariableBag`'s parent pointer.** Today `LocalVariableStorage.WireToDefinition` wires the definition's bag as a parent for default lookup. Remove that linkage. Definition default-fallback lives at the handle level (`Definition.TryGetDefaultValue` after the `IEntityVariableStorage.Parent` chain bottoms out). Storage `Parent` is overlays-only.
- **Replace `InstanceId(int)` with `Ref<T>(Guid)` everywhere.** All bridge payloads, `StateEntity<TDef>`, factories — migrate to `Ref<T>` (which is a `Reference`). `InstanceId` is deleted at the end of Plan B.
- **`ModifierSource` shape:** `readonly struct ModifierSource(Reference Source, int Tag)` (rebased — was `IRef Source`). Polymorphic source identity matches the rest of the system; `Reference` is the polymorphic base on main.

---

## Retrospective (2026-05-05, post-impl)

Plan A landed as written. PR #40, commits `5d52174` (original plan) → `6c85284` (rebase) → `9b90061` (impl + 21 unit tests) → integration tests in this commit.

**Hard requirements / hard avoids:** all matched as written. None had to be revised during implementation.

**Open items, status after impl:**

| # | Item | Status |
|---|---|---|
| 1 | Internal storage shape | **Closed** — `Map<Reference, Type, object>` from `Scaffold.Maps` (decided pre-impl) |
| 2 | `Store` partial class | **Closed** — inline property, ratified during plan review |
| 3 | `Ref<T>` syntax | **Closed** — record class, ratified during rebase |
| 4 | Concurrency | **Open (deferred)** — current impl assumes single-threaded use; `Map`/`Dictionary` underneath has no special locking. Revisit if Store ever becomes thread-safe. |
| 5 | `Unregister` while ref still referenced from a slice | **Open (deferred)** — no auto-cleanup, no in-code documentation yet. The `TryResolve` returns false / `Resolve` throws is the only guard. Add an xml-doc note on `ICatalog.Unregister` next time the file is touched. |
| 6 | `ICatalog` impl name | **Closed** — `Catalog` (the simplest option; it's `internal`, so external naming is via `ICatalog` anyway) |
| 7 | Sentinel for allocated-not-bound | **Closed** — `private static readonly object AllocatedSentinel = new();` stored as the value at `(ref, typeof(T))` |

**Implementation notes worth carrying into Plan B:**

- The catalog's `Map<Reference, Type, object>` storage means **type enforcement is automatic** via the `(ref, typeof(T))` secondary key. `Resolve<U>` of a `Ref<U>` registered as `T` returns `false` from `TryResolve` / throws `KeyNotFoundException` from `Resolve`. No `InvalidCastException` path. Plan B authors writing entity factories that pre-allocate refs need to register at the same `T` they allocated under.
- Tests reach the internal `Catalog` class via the existing `[assembly: InternalsVisibleTo("Scaffold.States.Tests")]` in `Runtime/AssemblyInfo.cs`. Plan B tests will need to do the same if they touch internals of `Scaffold.Entities` / `Scaffold.Entities.States`.
- Per Decision 1 (Catalog storage choice during review), the type-mismatch enforcement test pair (`Resolve_RegisteredUnderDifferentT_ReturnsFalse`, `RegisterAt_AllocatedUnderDifferentT_Throws`) pin the secondary-key behavior. Useful regression tests if anyone tries to "simplify" the catalog to `Dictionary<Guid, object>` later.

**Spike updates:** the spike's `com.scaffold.states` row in §"What changes, where" has been updated to match the as-built shape. The spike's deeper Plan B sections (§"Final shapes" §3 bridge code, etc.) still reference the pre-rebase `IRef`/`IReference` model; those will be swept when Plan B is prepared.
