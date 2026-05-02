---
type: adr
status: accepted
tags: [#adr, #zones, #model]
---

# ADR-0001 — Zone shape: definition + contents slice + per-zone-type runtime slice

Date: 2026-05-01
Context-doc: features/zones/models/01-zone-model.md
Proposal: session-20260501-1602-zone-model-internal-opens

## Context

The zone Model settled high-level direction (responsibilities-not-traits, two visibility channels, zone movement is normal action flow) but left "are zones entities or lightweight records?" and "where does membership live?" unanswered. Both are load-bearing: triggers, effects, presentation, history, and targeting all depend on what a zone *is* in the type system.

Two pulls in tension. **Zone-as-entity** treats a zone as a `StateEntity<TZoneDef>` polymorphic with cards, with `EntityVariableState` slices and modifier stacks — uniform and consistent with the entity Model, but drags in machinery zones don't need. **Lightweight record** treats a zone as a plain Store slice — no machinery, but breaks polymorphic targeting ("pick a card or a zone") and forces a parallel `ZoneId` namespace.

User constraint surfaced during the design pass: zones are groups of cards. They can have runtime values (a counter), but the moment something has stats with modifier stacks, it's not a zone anymore — it's a different entity that has a zone inside. Decisive: **no modifier-stack machinery on zones.**

## Options considered

- **A — Zones as `StateEntity<TZoneDef>`.** Full entity machinery. Rejected: drags in modifier stacks and the entity-variable bridge for a model that doesn't use either. Stat-bearing things are not zones.
- **B — Zones as plain Store slices keyed by a separate `ZoneId` type.** Pure data. Rejected: breaks free targeting (effects that pick "a card or a zone" need a unified ID namespace), and forces every game to re-implement membership data structures.
- **C — Zones as thin objects sharing the `Identifier<Instance>` namespace.** Zone is `(Id, Definition, Owner)`. State lives in two slices: a generic framework-owned `ZoneContentsState` for membership, and per-zone-type runtime slices for typed values (counter, recipe progress, etc.). No `EntityVariableState`, no modifier stacks, no entity-bridge participation. Targeting works uniformly because cards and zones share the ID namespace. Chosen.

## Decision

A **zone** is a class implementing `IReference` with two structural fields: `Id` (`InstanceId`) and `Definition` (a `ZoneDefinition`). The zone shares the `Identifier<Instance>` namespace with cards (and any other `IReference`-implementing entity) so targeting and references work uniformly.

```csharp
public sealed class Zone : IReference
{
    public InstanceId Id { get; }
    public ZoneDefinition Definition { get; }

    public Actor? GetOwner(IStateScope state) =>
        state.Get<OwnershipState>().OwnerOf(this);

    public IEnumerable<Card> GetCards(IStateScope state)
    {
        var contents = state.Get<ZoneContentsState>(this);
        var map = state.Get<CardMapState>();
        return contents.Cards.Select(map.GetCard);
    }
    // ... other state-reading methods
}
```

Zone is a class (handle), not a record. There is one `Zone` instance per `InstanceId` in the running engine, shared by all framework code that operates on that zone. Mutable zone data (contents, owner, per-zone-type values) lives in slices.

Zone runtime state lives across:

1. **`ZoneContentsState`** (framework-owned, generic). Keyed by the zone. Holds an ordered `ImmutableList<CardRef>` referencing cards via their current `CardRef` (per Card Model — refs rotate on every move). When a game needs richer per-entry structure (slot keys, group keys, spatial coordinates), it adds those fields on its own design pass — the framework does not pre-define hooks for them. Defer-by-default keeps the core shape minimal.
2. **`OwnershipState`** (framework-owned, generic across all `IReference`). Holds `InstanceId → Actor`. Both Cards and Zones read ownership through this slice.
3. **Per-zone-type runtime slices** (game-authored). Carry typed values for that zone type — e.g., `PileState { Counter }`, `RecipeStackState { Progress }`. Independent slices; no shared shape.

Zone configuration (capacity, ordering, insertion behavior, default visibility policy) lives on the **`ZoneDefinition`** as authored data. Different zone types vary by definition, not by class hierarchy.

Per ADR-0005, framework APIs take the zone object directly: `state.Get<ZoneContentsState>(zone)`, `zone.GetCards(state)`. The zone is used as a slice key; utility methods on the zone take state per call.

Reverse-query aggregates (e.g., a `CardRef → Zone` index) are deferred until a concrete consumer surfaces. See `bl-card-location-index` in `BACKLOG.md`.

## Consequences

- **Enables:** zones and cards both implement `IReference` and share the `InstanceId` namespace, so polymorphic targeting and reference handling work uniformly; membership has a single canonical home; per-zone-type runtime values have a typed home; no modifier-stack baggage on zones.
- **Constrains:** zones can never carry stats with modifier stacks. A game concept that needs stat machinery must be modeled as a different entity (an actor, a system entity) that has a zone associated with it, not as a zone with stats. Zone owner is read-only on the handle (mutations go through `OwnershipState`).
- **Reverts/replaces:** the Open Questions in `features/zones/models/01-zone-model.md` "are zones always entity instances, or can some zones be lightweight session records?" and "is zone membership best represented as a variable, relationship, Store slice, or zone-owned collection?" are answered here.
- **Audit:** the entities Model line "Game-level state (turn, phase, available actions, decisions, zones-as-records) lives in plain Store slices alongside the entity slices" is reaffirmed by this ADR — zones are slice-records, not entities.
