---
type: adr
status: accepted
tags: [#adr, #zones, #model]
---

# ADR-0001 — Zone shape: definition + contents slice + per-zone-type runtime slice

Date: 2026-05-01 (amended 2026-05-06 to reflect Plan A/B/C cascade)
Context-doc: features/zones/models/01-zone-model.md
Proposal: session-20260501-1602-zone-model-internal-opens

## Amendment — 2026-05-06 (Plan A/B/C cascade)

Original ADR text named the bridge handle `StateEntity<TDef>` and the empty marker `IReference`; both have been replaced. The bridge handle is now `EntityInstance<TDef>` (identity-free at the base, with concrete subclasses carrying typed `Ref<TSubclass> SelfRef`); the polymorphic addressable type is `Reference` (an abstract record). The decision below has been updated; the original options-considered analysis still stands.

**Spirit of the ADR is preserved.** Zones still don't drag in heavy modifier-stack expectations. Practically, post-Plan-A/B/C the entity-bridge surface is small enough that zones now extend `EntityInstance<ZoneDefinition>` for the typed identity (`Ref<Zone>`) and uniform variable storage — most zones declare zero variables and pay no cost. The "no modifier-stack machinery" rule from the user's original framing applies as a design intent: stat-bearing semantics belong on different entities, not on zones.

## Context

The zone Model settled high-level direction (responsibilities-not-traits, two visibility channels, zone movement is normal action flow) but left "are zones entities or lightweight records?" and "where does membership live?" unanswered. Both are load-bearing: triggers, effects, presentation, history, and targeting all depend on what a zone *is* in the type system.

Two pulls in tension. **Zone-as-entity** treats a zone as `EntityInstance<TZoneDef>` polymorphic with cards, with `EntityState` slices — uniform and consistent with the entity Model. **Lightweight record** treats a zone as a plain Store slice — no machinery, but breaks polymorphic targeting ("pick a card or a zone") and forces a parallel `ZoneId` namespace.

User constraint surfaced during the design pass: zones are groups of cards. They can have runtime values (a counter), but the moment something has stats with modifier stacks, it's not a zone anymore — it's a different entity that has a zone inside. Decisive: **no rich modifier-stack semantics on zones**, even if the bridge technically supports it.

## Options considered

- **A — Zones as `StateEntity<TZoneDef>` (full bridge with the old, heavy adapter).** Rejected at the time — the bridge was 422 LOC of caching/multiplexing, too heavy for a no-variables zone.
- **B — Zones as plain Store slices keyed by a separate `ZoneId` type.** Pure data. Rejected: breaks free targeting (effects that pick "a card or a zone" need a unified ID namespace), and forces every game to re-implement membership data structures.
- **C — Zones as thin objects sharing the entity bridge.** Zone is `(Definition, Storage, SelfRef)`. State lives across the `EntityState` slice (often empty), a generic framework-owned `ZoneContentsState`, and per-zone-type runtime slices for typed values (counter, recipe progress, etc.). Targeting works uniformly because cards and zones share `Reference` / `Ref<T>`. Chosen — and after Plan C trimmed the bridge to ~90 LOC, the cost of "always on the bridge" is negligible.

## Decision

A **zone** is `sealed class Zone : EntityInstance<ZoneDefinition>` carrying typed `Ref<Zone> SelfRef`. Zones share the `Reference` polymorphic type with cards and players.

```csharp
public sealed class Zone : EntityInstance<ZoneDefinition>
{
    public Ref<Zone> SelfRef { get; init; }

    public Zone(ZoneDefinition def, IEntityVariableStorage storage) : base(def, storage) { }

    public Actor? GetOwner(IStateScope state) =>
        state.Get<OwnershipState>().OwnerOf(SelfRef);

    public IEnumerable<Card> GetCards(IStateScope state)
    {
        var contents = state.Get<ZoneContentsState>(this);
        var map      = state.Get<CardMapState>();
        var registry = state.Get<CardRegistry>();
        return contents.Cards.Select(refToCard => registry.Get(map.InstanceOf(refToCard)));
    }
    // ... other state-reading methods
}
```

Zone is a class (handle), not a record. There is one `Zone` instance per `Ref<Zone>` in the running engine, held in `store.Catalog`. Mutable zone data (contents, owner, declared variables) lives in slices.

Zone runtime state lives across:

1. **`EntityState`** (framework-owned). Keyed by `Ref<Zone>`. Holds any variables the zone definition declares; for most zones (hand, deck, discard), this is empty.
2. **`ZoneContentsState`** (framework-owned, generic). Keyed by the zone. Holds an ordered `ImmutableList<CardRef>` referencing cards via their current `CardRef` (per Card Model — refs rotate on every move). When a game needs richer per-entry structure (slot keys, group keys, spatial coordinates), it adds those fields on its own design pass — the framework does not pre-define hooks for them. Defer-by-default keeps the core shape minimal.
3. **`OwnershipState`** (framework-owned, generic across all catalog-registered handles). Holds `Reference → Actor`. Both Cards and Zones read ownership through this slice.
4. **Per-zone-type runtime slices** (game-authored). Carry typed values for that zone type — e.g., `PileState { Counter }`, `RecipeStackState { Progress }`. Independent slices; no shared shape.

Zone configuration (capacity, ordering, insertion behavior, default visibility policy) lives on the **`ZoneDefinition`** as authored data. Different zone types vary by definition, not by class hierarchy.

Per ADR-0005, framework APIs take the zone object directly: `state.Get<ZoneContentsState>(zone)`, `zone.GetCards(state)`. The zone is used as a slice key; utility methods on the zone take state per call.

Reverse-query aggregates (e.g., a `CardRef → Zone` index) are deferred until a concrete consumer surfaces. See `bl-card-location-index` in `OPEN-ITEMS.md`.

## Consequences

- **Enables:** zones and cards share the `Reference` polymorphic type, so polymorphic targeting and reference handling work uniformly; membership has a single canonical home; per-zone-type runtime values have a typed home; the bridge cost is negligible after Plan C.
- **Constrains:** zones should not be used as the home for rich stat-bearing semantics with deep modifier stacks. A game concept like that should be modeled as a different entity (an actor, a system entity) that has a zone associated with it. Zone owner is read-only on the handle (mutations go through `OwnershipState`).
- **Reverts/replaces:** the Open Questions in `features/zones/models/01-zone-model.md` "are zones always entity instances, or can some zones be lightweight session records?" and "is zone membership best represented as a variable, relationship, Store slice, or zone-owned collection?" are answered here.
