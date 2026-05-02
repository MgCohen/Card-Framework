---
type: adr
status: accepted
last-updated: 2026-05-01
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

A **zone** is a thin object with three structural fields: `Id` (`Identifier<Instance>`), `Definition` (a `ZoneDefinition`), and optional `Owner` (`Actor`). The zone shares the `Identifier<Instance>` namespace with cards so targeting and references work uniformly.

Zone runtime state lives in two kinds of slice:

1. **`ZoneContentsState`** (framework-owned, generic). Keyed by the zone object. Holds an ordered list of `ContentEntry { InstanceId, SlotKey?, GroupKey? }`. The same shape supports ordered lists, slotted zones, and grouped piles — interpretation lives in the zone definition.
2. **Per-zone-type runtime slices** (game-authored). Carry typed values for that zone type — e.g., `PileState { Counter }`, `RecipeStackState { Progress }`. Independent slices; no shared shape.

Zone configuration (capacity, ordering, insertion behavior, default visibility policy) lives on the **`ZoneDefinition`** as authored data. Different zone types vary by definition, not by class hierarchy.

Per ADR-0005, framework APIs take the zone object directly: `state.Get<ZoneContentsState>(zone)`, `zone.GetCard(state, slot)`. The zone is used as a slice key; utility methods on the zone take state per call.

Reverse-query aggregates (e.g., a `CardLocationIndex` keyed by card → zone) may be maintained as derived slices, kept in sync atomically by the same mutators that write `ZoneContentsState`.

## Consequences

- **Enables:** zones participate in targeting and references uniformly with cards (one ID namespace); membership has a single canonical home; per-zone-type runtime values have a typed home; no modifier-stack baggage.
- **Constrains:** zones can never carry stats with modifier stacks. A game concept that needs stat machinery must be modeled as a different entity (an actor, a system entity) that has a zone associated with it, not as a zone with stats.
- **Reverts/replaces:** the Open Questions in `features/zones/models/01-zone-model.md` "are zones always entity instances, or can some zones be lightweight session records?" and "is zone membership best represented as a variable, relationship, Store slice, or zone-owned collection?" are answered here.
- **Audit:** the entities Model line "Game-level state (turn, phase, available actions, decisions, zones-as-records) lives in plain Store slices alongside the entity slices" is reaffirmed by this ADR — zones are slice-records, not entities.
