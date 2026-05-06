---
type: plan
status: partially-landed
tags: [#project, #card-framework, #integration, #scaffold]
---

# Card Framework — project integration (consume new Scaffold packages, project-side migration, ADR + model-doc cascade)

> **Plan D** of the Scaffold update sequence. Project-side integration after Plans A/B/C land in the Scaffold repo. Builds the project's `Card` / `Zone` / `Player` layer on the new `EntityInstance<TDef>` foundation, wires the project's privacy/registry slices, migrates any existing project code that referenced now-deleted Scaffold types, amends ADR-0005, and cascades model docs to reflect the new shape.
>
> **Status as of 2026-05-06:** Phase 2 (documentation cascade — ADR-0005 amendment, GLOSSARY update, model doc sweep across `entities/`, `cards/`, `zones/`, `effects/`, `triggers/`, `engine/`, `actions/`, `presentation/`) is **landed**. Phase 1 (project runtime migration — `Card`/`Zone`/`Player` C# code, `CardMapState`/`CardRegistry` services) is **deferred** — the Card Framework repo is doc-models-only at this stage; runtime migration becomes relevant when the repo grows code.

**Source spike:** [`Product/spikes/entities-state-unification.md`](../../Product/spikes/entities-state-unification.md) — locked decisions #11, #12, #15; final shapes §4 and §5; "What changes, where" project table; conceptual Profile B section.

**Plan A + B status:** landed in [Scaffold#40](https://github.com/MgCohen/Scaffold/pull/40) as a single PR.
**Plan C status:** landed in [Scaffold#45](https://github.com/MgCohen/Scaffold/pull/45).

After Plans A/B/C, the Scaffold side provides:
- `Ref<T>` (sealed record : `Reference`), `ICatalog` exposed via `Store.Catalog`, `ICatalogged` (Plan A).
- `IEntityVariableStorage` with chain-walking reads + `Parent`, identity-free `EntityInstance<TDef>`, single `LocalVariableStorage`, `ModifierSource(Reference?, int)`, no `IReadOnlyEntity` / `IMutableEntity` / `BaseEntityInstance` / `InstanceId` (Plan B).
- `EntityState` as `sealed record : State` with `BaseValues`/`ModifierStacks` fields and `With*`/`TryGetBase`/`GetModifiers`/`Variables` methods, 7 payloads (including `ClearModifiersPayload`), 7 mutators using `Mutator<EntityState, TPayload>`, ~90 LOC `StoreVariableStorage`, `StoreEntities.Spawn<TDef>` extension, no `StateEntity<TDef>` / `EntityStateFactory` (Plan C).

---

## TL;DR

Project-side work to consume the migrated Scaffold packages:

- `Card`, `Zone`, `Player` extend `EntityInstance<TDef>` directly. Each subclass carries its own typed `Ref<TSubclass> SelfRef` field (the base has none). Each adds sugar accessors (`Card.Cost`, `Card.Attack`, etc.) and a typed `Spawn` factory (`Card.Spawn(store, def)`).
- `CardDefinition`, `ZoneDefinition`, `PlayerDefinition` are authored ScriptableObjects extending `EntityDefinition`. They MAY implement `ICatalogged` (asset-GUID-keyed) if they need cross-run-stable refs in the catalog; otherwise they're consumed directly by `Card.Spawn` without catalog registration.
- `CardVars`, `ZoneVars`, `PlayerVars` static classes hold the project's variable-key constants.
- `CardMapState` slice maps `CardRef ↔ Ref<Card>` for the privacy/rotation mechanism. Pure data.
- `CardRegistry` engine-side service holds `Ref<Card> → Card` typed lookup, mirroring the catalog's typed view.
- Subscription patterns flow through `store.Subscribe<EntityState>(card.SelfRef, ...)` — substrate-native.
- Migrate any existing project code that referenced `IReadOnlyEntity` / `IMutableEntity` / `BaseEntityInstance` / `StateEntity<TDef>` / `InstanceId` / `EntityVariableState` / `EntityStateReference` / old `StoreVariableStorage` / old `EntityStateFactory` etc. — those types are gone from Scaffold.
- ADR-0005 amendment: drop the `IReadOnlyEntity ⊃ IReference` line; concretes expose `Ref<TSubclass> SelfRef` (an `IRef`-shaped — actually `Reference`-derived — addressable).
- Model doc cascade: update `features/entities/`, `features/cards/`, `features/zones/`, `features/effects/`, `features/triggers/`, `features/engine/`, `features/presentation/`, `features/actions/` to reflect the new shape — replace stale type names, drop references to deleted types, update the conceptual model where the old shape leaked in.

---

## Scope

### In scope (this plan)

**Project runtime code:**
- `Card`, `Zone`, `Player` (and any other entity subclass) — extend `EntityInstance<TDef>`, add typed `Ref<TSubclass> SelfRef`, sugar accessors, typed `Spawn` factory.
- `CardDefinition`, `ZoneDefinition`, `PlayerDefinition` — review against `EntityDefinition` (the current `IEntityDefinition` contract).
- `CardVars`, `ZoneVars`, `PlayerVars` — variable-key constants.
- `CardMapState` slice — `CardRef ↔ Ref<Card>`.
- `CardRef` value type — rotating public ref (privacy mechanism). Keep existing if it exists; verify it's compatible with the new identity story.
- `CardRegistry` engine service — typed `Ref<Card> → Card` lookup.
- Migration of existing project code that referenced deleted Scaffold types.
- Subscription wiring through `store.Subscribe<EntityState>(card.SelfRef, ...)` patterns.
- Optional: `EntityVariableTracker<T>` helper if per-variable reactivity is needed in any project flow.

**Documentation cascade:**
- `Product/decisions/0005-objects-over-ids.md` (ADR-0005) amendment.
- `Product/features/entities/models/01-definition-instance-model.md` — update references to deleted types and shape changes.
- `Product/features/entities/models/02-runtime-surface.md` — same.
- `Product/features/cards/models/*.md` — same.
- `Product/features/zones/models/*.md` — same.
- `Product/features/effects/models/*.md` — same.
- `Product/features/triggers/models/*.md` — same.
- `Product/features/engine/models/*.md` — same.
- `Product/features/presentation/models/*.md` — same.
- `Product/features/actions/models/*.md` — same.
- `Product/ledgers/GLOSSARY.md` — add new terms (`Ref<T>`, `IRef`/`Reference`, `ICatalog`, `ICatalogged`, `EntityInstance<TDef>`); retire stale terms (`StateEntity<TDef>`, `EntityVariableState`, `IReadOnlyEntity`, `IMutableEntity`, `InstanceId`, `EntityStateReference`).

### Out of scope (deferred)

- Anything in the Scaffold repo (Plans A, B, C own those).
- New gameplay features unrelated to the migration.
- Cross-run save/load adapter (still future).
- Replacing `Identifier<T>` family beyond what entity-side migration touches.
- Visual / presentation-layer changes that aren't directly downstream of the entity model.
- Workflow housekeeping (SESSION-CHECKPOINT, /builder:end-session, etc.) — handled separately.

---

## Hard requirements

**Project runtime:**

1. **`Card`, `Zone`, `Player` extend `EntityInstance<TDef>`** with their respective definitions (`CardDefinition`, `ZoneDefinition`, `PlayerDefinition`). Single inheritance; no `BaseEntityInstance` (deleted in Plan B).
2. **Each subclass declares its own typed `SelfRef`** as `public Ref<TSubclass> SelfRef { get; }` (a `Reference`). The base class has no `SelfRef`. The subclass's ctor takes `(Ref<TSubclass> selfRef, TDef def, IEntityVariableStorage storage)` and passes `(def, storage)` to the base.
3. **Sugar accessors on each subclass** call `GetVariable<T>(VarsClass.Key)` for the project's variable schema. E.g., `Card.Cost => GetVariable<int>(CardVars.Cost);`.
4. **Each subclass has a static `Spawn(Store, TDef)` factory** that:
   - `var r = store.Catalog.AllocateRef<TSubclass>();`
   - `var instance = new TSubclass(r, def, new StoreVariableStorage(store, r));`
   - `store.Catalog.RegisterAt(r, instance);`
   - `store.AddState(r, EntityState.Empty);`
   - `return instance;`
5. **`CardVars`, `ZoneVars`, `PlayerVars`** are static classes holding `Variable` keys for the project's schema. Keys consistent with whatever key-derivation/equality `Variable` uses today.
6. **`CardMapState` slice** is `record CardMapState(ImmutableDictionary<CardRef, Ref<Card>> RefToInstance, ImmutableDictionary<Ref<Card>, CardRef> InstanceToRef) : State;`. Slice key is `Reference.Null` (singleton-style) or whatever the existing `CardMapState` keying convention is — consistent with how the slice is accessed.
7. **`CardRegistry` engine service** holds `Dictionary<Ref<Card>, Card> cards` with `Get(Ref<Card>)`, `Register(Card)` (uses `card.SelfRef`), `Unregister(Ref<Card>)`. Side-state on the engine, not in the Store.
8. **All existing project code that referenced deleted Scaffold types compiles green again.** Specifically: `IReadOnlyEntity`, `IMutableEntity`, `BaseEntityInstance<TDefinition>`, `StateEntity<TDef>`, `InstanceId`, `EntityVariableState`, `EntityStateReference`, `EntityStateFactory`, `StoreInstanceIdExtensions`, old `StoreVariableStorage` API. All migrated to new shapes. Note: `EntityBridgeContext` and `StateEntityOps` may still exist after Plan C (Plan C decides their fate); if they survived under new names, project code should reference the new names.
9. **Subscription patterns use `store.Subscribe<EntityState>(card.SelfRef, ...)`.** No bespoke per-variable subscription multiplexing in project code (that pattern is gone).
10. **Resolution path is one-step from `CardRef`:** `var r = store.Get<CardMapState>().RefToInstance[cardRef]; return registry.Get(r);`. Two slice accesses (CardMapState + CardRegistry). Document this convention in the project.
11. **Card / Zone / Player handles outlive `CardRef` rotations.** Only the public ref rotates on visibility flips; the underlying `Ref<Card>` and the live `Card` handle are stable.
12. **Within-run rollback works for free.** Catalog persists across `LoadSnapshot`. Project code holding a `Card` reference continues to resolve correctly post-rollback (reads return rolled-back `EntityState` values; the handle and `Definition` reference are untouched).

**Documentation:**

13. **ADR-0005 amendment** (`Product/decisions/0005-objects-over-ids.md`):
    - Drop the `IReadOnlyEntity ⊃ IReference` hierarchy line.
    - Replace with: concretes (`Card`, `Zone`, etc.) expose `Ref<TSubclass> SelfRef` (a `Reference`/`IRef`-shaped value).
    - Note that `IReference` itself was removed in main's recent state refactor, replaced by `abstract record Reference`.
    - Note that `Identifier<T>` is being phased out in favor of `Ref<T>` for runtime gameplay (cross-cutting cascade, separate from this migration).
14. **Model doc cascade is complete:** every model doc in `Product/features/{entities,cards,zones,effects,triggers,engine,presentation,actions}/models/` that referenced now-deleted/renamed types is updated. No stale references to: `IReadOnlyEntity`, `IMutableEntity`, `BaseEntityInstance`, `StateEntity<TDef>`, `InstanceId`, `EntityVariableState`, `EntityStateReference`, old `StoreVariableStorage` (the 422 LOC version), `EntityStateFactory`, `EntityBridgeContext`.
15. **`GLOSSARY.md`** updated:
    - Add: `Ref<T>`, `Reference` (abstract record), `ICatalog`, `ICatalogged`, `EntityInstance<TDef>` (clarified post-refactor), `Catalog` (the side-state on Store), `EntityState` (clarified post-refactor).
    - Retire (move to retired-terms section if one exists, or just remove): `StateEntity<TDef>`, `EntityVariableState`, `IReadOnlyEntity`, `IMutableEntity`, `BaseEntityInstance`, `InstanceId`, `EntityStateReference`, the old `StoreVariableStorage` shape.

---

## Hard avoids

1. **Do NOT add `SelfRef`, `Id`, or any `Reference`-shaped identity field to the base `EntityInstance<TDef>` class.** That's a Scaffold-side decision (Plan B); project subclasses carry their own typed refs.
2. **Do NOT reintroduce per-variable subscription patterns in project code.** No wrapping `EntityVariableTracker<T>` around every variable read; build it on-demand only where genuinely needed (UI hot-paths). Most consumers should subscribe to the slice directly.
3. **Do NOT register handles in the Store as state.** `CardRegistry` is engine-side service, not a `State` slice. (Locked decision #12.)
4. **Do NOT keep `[Obsolete]` aliases** for deleted Scaffold types in project code. Hard-migrate. Easier to audit completeness.
5. **Do NOT add a project-side wrapper around `Ref<Card>` to hide it.** `Ref<Card>` IS the addressable identity in the project; treat it as a first-class value type.
6. **Do NOT modify Scaffold packages from this plan.** Plans A/B/C own those.
7. **Do NOT migrate `Identifier<T>` family in this plan.** Only entities-related identifiers (`InstanceId`, `EntityStateReference`) are in scope. `Identifier<T>` for non-entity domains (zones-as-records, slot keys, etc., if any) stays put — separate cascade.
8. **Do NOT skip the model doc cascade.** Stale doc references are subtle and accumulate. Sweep them as part of this plan, not "later."
9. **Do NOT add new gameplay features as part of this plan.** Migration only. New features land separately so the migration is reviewable in isolation.
10. **Do NOT couple `CardRegistry` to the Store's snapshot lifecycle.** It's side-state; not snapshotted; doesn't need rebuild on `LoadSnapshot` because the catalog persists too.

---

## Deliverables — concrete shapes

### 1. `Card` (and `Zone`, `Player`)

```csharp
public sealed class Card : EntityInstance<CardDefinition>
{
    // Card is catalog-registered, carries its own typed Ref<Card>.
    // Base EntityInstance<TDef> has no SelfRef (per Plan B).
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
    // ... whatever else the project's card schema uses

    public static Card Spawn(Store store, CardDefinition def)
    {
        var r = store.Catalog.AllocateRef<Card>();
        var card = new Card(r, def, new StoreVariableStorage(store, r));
        store.Catalog.RegisterAt(r, card);
        store.AddState(r, EntityState.Empty);
        return card;
    }
}
```

Same shape applies to `Zone : EntityInstance<ZoneDefinition>` (with `Ref<Zone> SelfRef`, `ZoneVars`-based accessors, `Zone.Spawn`), `Player : EntityInstance<PlayerDefinition>`, etc.

### 2. Definitions (`CardDefinition`, `ZoneDefinition`, `PlayerDefinition`)

Existing authored ScriptableObjects that extend `EntityDefinition` (or whatever the post-Plan-B name is). Keep their existing authoring API; just verify they don't implement any deleted source/storage interface.

```csharp
public class CardDefinition : EntityDefinition
{
    // Existing authoring fields: defaults for Cost / Attack / Health / Owner-default / etc.
    // Visual references, behavior references, tags — all unchanged.
}
```

Optional: implement `ICatalogged` if you want asset-GUID-stable refs for definitions registered in the catalog (e.g., for save/load alignment). For runtime gameplay, definitions don't need to be in the catalog — they're held by the live `Card` handle directly.

### 3. Variable-key statics (`CardVars`, `ZoneVars`, `PlayerVars`)

```csharp
public static class CardVars
{
    public static readonly Variable Cost   = new(/* key derivation per project convention */);
    public static readonly Variable Attack = new(/* ... */);
    public static readonly Variable Health = new(/* ... */);
    public static readonly Variable Owner  = new(/* ... */);
    // ... whatever else
}
```

Whatever pattern the existing project already uses for variable-key declaration. Probably already exists; verify it's consistent with the new `Variable` shape (if Plan B touched it).

### 4. `CardMapState` slice

```csharp
public sealed record CardMapState(
    ImmutableDictionary<CardRef, Ref<Card>> RefToInstance,
    ImmutableDictionary<Ref<Card>, CardRef> InstanceToRef
) : State
{
    public static CardMapState Empty
        => new(ImmutableDictionary<CardRef, Ref<Card>>.Empty,
               ImmutableDictionary<Ref<Card>, CardRef>.Empty);

    // Functional updates — at minimum:
    public CardMapState WithMapping(CardRef cardRef, Ref<Card> instanceRef);
    public CardMapState WithoutCard(Ref<Card> instanceRef);
    public CardMapState RotateRef(Ref<Card> instanceRef, CardRef newCardRef);
}
```

Plus payload(s) and mutator(s) for rotation events. The exact mutation surface depends on existing project conventions for the privacy mechanism.

### 5. `CardRegistry` engine service

```csharp
public sealed class CardRegistry
{
    private readonly Dictionary<Ref<Card>, Card> cards = new();

    public Card Get(Ref<Card> r) => cards[r];
    public bool TryGet(Ref<Card> r, out Card card) => cards.TryGetValue(r, out card);
    public void Register(Card card) => cards[card.SelfRef] = card;
    public void Unregister(Ref<Card> r) => cards.Remove(r);
}

// Resolution from CardRef:
public Card Resolve(CardRef cardRef)
{
    var r = store.Get<CardMapState>().RefToInstance[cardRef];
    return registry.Get(r);
}
```

Equivalent for `ZoneRegistry`, `PlayerRegistry` if the project uses analogous privacy/rotation mechanisms for those types.

### 6. Subscription patterns

```csharp
// Replace old per-variable subscription patterns with substrate-native slice subscriptions.
store.Subscribe<EntityState>(card.SelfRef, (oldState, newState, ctx) => view.MarkDirty());

// Card-map-state subscriptions for visibility flips:
store.Subscribe<CardMapState>(/* whatever key */, (oldMap, newMap, ctx) => /* refresh visible cards */);
```

For per-variable change detection in UI hot-paths, build `EntityVariableTracker<T>` on-demand:

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

// Usage:
new EntityVariableTracker<int>(card, CardVars.Cost,
    cb => store.Subscribe<EntityState>(card.SelfRef, (_, _, _) => cb()));
```

Lives in the project, not in Scaffold. Build only where genuinely needed (probably only UI binding code).

### 7. Project code migration audit

For each currently-referenced deleted/renamed Scaffold type, sweep project code:

| Deleted/renamed type | Migration target |
|---|---|
| `IReadOnlyEntity` / `IMutableEntity` | Concrete `Card` / `Zone` / `Player` (or generic `EntityInstance<TDef>`). Polymorphism via `Reference` for "any addressable thing." |
| `BaseEntityInstance<TDefinition>` | `EntityInstance<TDef>` directly. |
| `StateEntity<TDef>` | `EntityInstance<TDef>` (or its subclass `Card`/`Zone`/etc.) with `StoreVariableStorage`. |
| `InstanceId` | `Ref<TSubclass>` (when typed) or `Reference` (when polymorphic). |
| `EntityVariableState` | `EntityState`. |
| `EntityStateReference` | `Ref<TSubclass>` directly; the wrapper is gone. |
| Old `StoreVariableStorage` (~422 LOC API) | New `StoreVariableStorage` (~90 LOC). API surface is similar — read methods + write methods. No per-variable subscription wiring. |
| `EntityStateFactory` | `StoreEntities.Spawn<TDef>` extension. |
| `EntityBridgeContext` | May survive Plan C (mutator registration). If renamed, update references. |
| `StoreInstanceIdExtensions` | Deleted in Plan B; the extensions evaporate. |
| `StateEntityOps` | Bulk `RemoveModifiersFromSource` may have moved to `StoreEntities`. Check Plan C's retro. |
| `ModifierSource(InstanceId)` constructor | `ModifierSource(Reference, int)` — typically `someEntity.SelfRef`. |

For each migration: search project for type references, refactor to new shape, run tests.

### 8. ADR-0005 amendment

Edit `Product/decisions/0005-objects-over-ids.md` to:

- Add a "Superseded in part" or "Updated" header section noting the migration date and the change.
- Drop the `IReadOnlyEntity ⊃ IReference` line in the hierarchy.
- Replace with: "Concrete entity handles (`Card`, `Zone`, `Player`) carry typed `Ref<TSubclass> SelfRef` fields. `Ref<T>` is a sealed record deriving from `Reference` (the abstract-record polymorphic base in `Scaffold.States`)."
- Note that `IReference` itself was removed in main's recent state refactor and replaced with `Reference`.
- Note that `Identifier<T>` is being phased out in favor of `Ref<T>` for runtime gameplay; full migration is a separate cascade.
- Preserve the spirit of ADR-0005 (objects over IDs in framework APIs). The only thing that changes is the type that backs "addressable thing" — `Reference` now, not the empty `IReference` marker.

### 9. Model doc cascade

Sweep each of the following directories for stale type references:

- `Product/features/entities/models/`
- `Product/features/cards/models/`
- `Product/features/zones/models/`
- `Product/features/effects/models/`
- `Product/features/triggers/models/`
- `Product/features/engine/models/`
- `Product/features/presentation/models/`
- `Product/features/actions/models/`

For each doc, search-and-replace stale references:

| Stale | Replace with |
|---|---|
| `StateEntity<TDef>` | `EntityInstance<TDef>` (or concrete `Card` / `Zone` / etc.) |
| `EntityVariableState` | `EntityState` |
| `IReadOnlyEntity` / `IMutableEntity` | `EntityInstance<TDef>` (or `Reference` for "any addressable") |
| `BaseEntityInstance` | `EntityInstance<TDef>` |
| `InstanceId` | `Ref<T>` or `Reference` |
| `EntityStateReference` | `Ref<T>` |
| `IReference` (the empty marker) | `Reference` (abstract record) |
| `ModifierSource(InstanceId)` | `ModifierSource(Reference, int)` |
| References to old `StoreVariableStorage` complexity (caching, subscription multiplexing) | New thin-adapter description |
| References to `EntityStateFactory` etc. | `StoreEntities.Spawn<TDef>` |

This is mechanical mostly. Where the stale reference was load-bearing for an actual conceptual point (not just a name), update the conceptual framing too — e.g., "the bridge holds a per-variable subscription cache" → drop entirely (no longer true).

### 10. `GLOSSARY.md` update

Add new terms:
- **`Ref<T>`** — typed sealed record derived from `Reference`; the runtime identity primitive backed by `Guid`.
- **`Reference`** — abstract record base in `Scaffold.States`; replaces the old `IReference` marker.
- **`IRef`** — (if still mentioned in any docs) note this was a brief design proposal; the implemented shape is `Reference` / `Ref<T>`.
- **`ICatalog`** — sub-property on `Store` providing register/resolve for arbitrary objects.
- **`ICatalogged`** — opt-in interface for objects with a stable cross-run `Guid Key`.
- **`EntityInstance<TDef>`** — single entity handle class; pairs `(Definition, Storage)`. Identity-free at the base.
- **`Catalog`** — in-process side-state on Store; not snapshotted.
- **`EntityState`** — sealed record slice `(BaseValues, ModifierStacks) : State`; replaces `EntityVariableState`. Pure data with functional `With*` updates.

Retire (move to retired-terms list or just delete):
- `StateEntity<TDef>`
- `EntityVariableState`
- `IReadOnlyEntity`
- `IMutableEntity`
- `BaseEntityInstance<TDefinition>`
- `InstanceId`
- `EntityStateReference`
- `IReference` (empty marker)
- `EntityStateFactory`, `EntityBridgeContext`, `StoreInstanceIdExtensions`, `StateEntityOps`

---

## Implementation order

**Phase 1 — Project runtime migration** (gets the project compiling green):

1. **Pin Scaffold to the post-Plan-C version.** Update package manifest / version pin so the project consumes the new Scaffold.
2. **Audit & inventory.** Grep project for references to all deleted/renamed types. Build a migration checklist.
3. **Migrate `Card`** to extend `EntityInstance<CardDefinition>` directly. Add typed `Ref<Card> SelfRef`. Add `Card.Spawn`. Update sugar accessors.
4. **Migrate `Zone`, `Player`, and any other entity subclass** to the same pattern.
5. **Verify `CardDefinition`, `ZoneDefinition`, `PlayerDefinition`** still work against the new `EntityDefinition` contract.
6. **Migrate `CardMapState`** (if it existed before, update the value type to `Ref<Card>` instead of `InstanceId` etc.).
7. **Migrate `CardRegistry`** (rebuild as side-dict keyed by `Ref<Card>`).
8. **Migrate any payloads/mutators in the project** that referenced old `StateEntity<TDef>` / `InstanceId` / etc.
9. **Migrate subscription wiring** to `store.Subscribe<EntityState>(card.SelfRef, ...)` patterns.
10. **Build green.** Project compiles. All existing project tests pass.

**Phase 2 — Documentation cascade**:

11. **Amend ADR-0005** with the noted changes.
12. **Sweep model docs** for stale type references. PR per feature directory or one big PR — reviewer's call.
13. **Update GLOSSARY.md** with new terms and retired terms.
14. **Optional retro pass**: any spike open items that surfaced during impl get folded back into the spike.

Phase 1 is one-or-more PRs depending on how many entity types and how much project code. Phase 2 is documentation sweeps.

---

## Validation

### Unit tests (required)

| Test | Asserts |
|---|---|
| `Card_Spawn_ReturnsHandleWithSelfRef` | `var c = Card.Spawn(store, def); c.SelfRef != default` |
| `Card_Spawn_RegistersInCatalog` | `store.Catalog.Resolve(c.SelfRef) == c` |
| `Card_Spawn_AddsEmptyEntityState` | `store.Get<EntityState>(c.SelfRef) == EntityState.Empty` |
| `Card_SugarAccessor_ReadsThroughVariable` | `c.Cost` returns the same as `c.GetVariable<int>(CardVars.Cost)` |
| `Card_BaseHasNoSelfRef` | Reflection: base `EntityInstance<CardDefinition>` has no `SelfRef`; only `Card` declares it |
| `Card_Definition_IsHeldDirectly` | `c.Definition` returns the def passed to Spawn (no slice indirection) |
| `Zone_Spawn_AnalogousToCard` | Same shape for Zone |
| `Player_Spawn_AnalogousToCard` | Same shape for Player |
| `CardMapState_RefToInstance_Roundtrip` | After `WithMapping(cardRef, instanceRef)`, `RefToInstance[cardRef] == instanceRef` |
| `CardMapState_InstanceToRef_Roundtrip` | And `InstanceToRef[instanceRef] == cardRef` |
| `CardMapState_WithoutCard_RemovesBothDirections` | Removing one removes both entries |
| `CardMapState_RotateRef_UpdatesBothDirections` | Atomic ref rotation updates both maps |
| `CardRegistry_Register_GetRoundtrip` | After `registry.Register(card)`, `registry.Get(card.SelfRef) == card` |
| `CardRegistry_Unregister_RemovesEntry` | After unregister, `Get` throws (or `TryGet` returns false) |
| `CardRegistry_NotInStore` | `CardRegistry` is not registered as a State slice (snapshot has no `CardRegistry` entry) |
| `Resolve_FromCardRef_TwoStepLookup` | `Resolve(cardRef)` chains through `CardMapState` then `CardRegistry`, returns the Card |
| `WithinRunRollback_HandleStillResolves` | Spawn card, snapshot, mutate, LoadSnapshot, `card` reference still valid; reads return rolled-back values |
| `WithinRunRollback_CatalogUntouched` | Same scenario, `store.Catalog.Resolve(card.SelfRef) == card` after rollback |
| `Migration_NoStateEntityReferences` | Reflection over project assemblies: zero references to `StateEntity<>` |
| `Migration_NoIReadOnlyEntityReferences` | Same for `IReadOnlyEntity` |
| `Migration_NoIMutableEntityReferences` | Same for `IMutableEntity` |
| `Migration_NoBaseEntityInstanceReferences` | Same for `BaseEntityInstance<>` |
| `Migration_NoInstanceIdReferences` | Same for `InstanceId` |
| `Migration_NoEntityVariableStateReferences` | Same for `EntityVariableState` |
| `Migration_NoEntityStateReferenceReferences` | Same for `EntityStateReference` |

### Integration tests (recommended)

- **End-to-end card lifecycle through the project API.** Spawn card via `Card.Spawn`. Set base values via sugar method. Apply modifiers. Read effective values. Move card between zones (zone-membership slice update). Snapshot. Mutate further. Rollback. Verify post-rollback state matches snapshot moment.
- **CardRef rotation through visibility flip.** Spawn a card. Move from hand (visible to player) to opponent's hand (visible only to opponent). `CardMapState` rotates `CardRef`; underlying `Ref<Card>` and `Card` handle stay stable.
- **Subscription notification on EntityState change.** Subscribe via `store.Subscribe<EntityState>(card.SelfRef, ...)`. Mutate. Verify subscriber fires.
- **`ModifierSource` round-trip with project entity.** Apply modifier sourced from `someAttachment.SelfRef`. Remove attachment via `RemoveModifiersFromSource(new ModifierSource(attachment.SelfRef, 0))`. Verify modifier is gone.

### Documentation tests (recommended)

- **`builder:lint`** (or whatever doc-linter the workflow uses) passes on all model docs after the cascade.
- **No unresolved doc references.** No model doc still references `StateEntity<TDef>`, `EntityVariableState`, etc.
- **`GLOSSARY.md` is internally consistent** — new terms have entries, retired terms either gone or in a "retired" section.

---

## Open / decide-during-impl

1. **Scaffold version pin.** Decide which Scaffold release/tag the project pins to. Likely a new minor/major version once Plans A/B/C all merge. Update the project's `package.json` / manifest accordingly.
2. **`CardDefinition` as `ICatalogged`?** Whether to register definitions in the catalog. Usually not necessary for runtime gameplay (definitions are held by handles), but useful for cross-run save/load alignment. Decide based on save/load roadmap.
3. **`CardVars` key derivation.** Confirm the `Variable` type's key-derivation pattern is unchanged (and whether `CardVars` needs any updates). Likely unchanged but verify.
4. **`EntityVariableTracker<T>` deployment.** Where does it live in the project (`Runtime/Util/`, `Runtime/Reactivity/`, etc.)? Build only when first consumer needs it.
5. **`CardRef` privacy mechanism preservation.** The project's existing `CardRef` rotation logic (visibility flips, etc.) should work unchanged on top of `CardMapState`. Verify no hidden coupling to deleted `InstanceId` / `EntityStateReference`.
6. **Migration cascade depth.** If project code references `StateEntity<TDef>` deep in handlers, effects, triggers, etc., migration may surface dozens of files. Triage by file count per feature directory; PR per feature if scope is large.
7. **Model doc archive vs rewrite.** Some old model docs may be irrelevant post-migration (e.g., docs about the old subscription multiplexing). Decide: archive the doc in `_archive/` vs. delete vs. rewrite. Lean: archive with a "superseded by [new doc]" note.
8. **Project test fixtures.** Existing test fixtures that constructed `StateEntity<TDef>` directly (bypassing factories) need migration to the new spawn pattern. Audit `Tests/` directory.
9. **Entity teardown lifecycle.** When a `Card` is removed from gameplay, who calls `store.Catalog.Unregister(card.SelfRef)` and `registry.Unregister(card.SelfRef)`? The spike defers this (open item #5), but Plan D should at least identify the lifecycle hooks — whether it's `Card.Dispose()`, an explicit `Card.Destroy(store)` method, a store-side listener, or just the caller's responsibility. Document the convention even if the implementation is simple.
10. **`Card` sealed vs unsealed.** Plan D's spec says `public sealed class Card : EntityInstance<CardDefinition>`. If the project ever needs spell-specific subclasses (e.g., `SpellCard : Card`), `sealed` prevents that. Decide: sealed (spells are data-driven via different `CardDefinition` variants) or unsealed (spells are subclasses). Lean: sealed — data-driven via definitions is the idiomatic Scaffold pattern.
11. **`StoreEntities.Spawn<TDef>` vs typed `Card.Spawn`.** Both exist — `StoreEntities.Spawn` registers as `Ref<EntityInstance<TDef>>` while `Card.Spawn` registers as `Ref<Card>`. These produce differently-typed catalog entries for conceptually the same thing. Decide: use only typed spawn factories (`Card.Spawn`, `Zone.Spawn`) and don't call `StoreEntities.Spawn` from project code, or accept both coexist and document the distinction. Lean: typed factories only — `StoreEntities.Spawn` is the generic fallback for non-subclassed entities.

---

## What "validate" means before declaring the migration complete

Plan D is validated when:

- **Phase 1:**
  - All "required" unit tests above pass.
  - All "recommended" integration tests pass.
  - The project builds green against the post-Plan-C Scaffold version.
  - All existing project tests pass (no regressions).
  - Reflection-based migration audit passes: zero references to deleted/renamed Scaffold types.
- **Phase 2:**
  - ADR-0005 amended; the amendment is approved (PR merged).
  - Model doc cascade is complete; `builder:lint` passes.
  - `GLOSSARY.md` updated with new terms; retired terms cleaned up.
- **Retro:**
  - Spike's open items are revisited; anything that surfaced during impl is folded back.
  - Spike status updated if substantive changes were needed.

After Plan D validates, the migration is complete. The project is on the new Scaffold foundation. Plans A/B/C/D are all closed.

---

## Out-of-band reminders

- Plans A+B are already landed ([Scaffold#40](https://github.com/MgCohen/Scaffold/pull/40)). Plan C must be landed before this plan starts. Project compiles will fail against the new Scaffold otherwise.
- This plan is **migration-only**. No new gameplay features. New features land in separate PRs after the migration is complete.
- The model doc cascade (Phase 2) is real work. Don't skip it. Stale docs accumulate and confuse readers, especially during onboarding. Spend the time.
- `Identifier<T>` family migration is **not** in scope for this plan — only entity-related identifiers. If `Identifier<T>` shows up in zones/slots/non-entity domains, leave alone (separate cascade).
- The catalog persists across rollback. Project code that holds `Card` references for long durations (across snapshots, gameplay phases) can rely on those references staying valid — no rehydration needed. This is a meaningful capability shift from the old design.
- After this lands, the framework's identity model is unified: `Ref<T>` everywhere for runtime gameplay, with `Reference` as the polymorphic base. Any future framework-level features should adopt this pattern from the start.
