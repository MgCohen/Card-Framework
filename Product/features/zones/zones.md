---
type: feature
status: settled
references: [features/entities/models/01-definition-instance-model.md, features/cards/models/01-card-model.md]
tags: [#zones]
---

# zones — feature

## What it is

The framework's container primitive. A **zone** is a thin handle implementing `IReference` that groups cards with declared organization (capacity, ordering, insertion behavior, default visibility policy). Zones share the `Identifier<Instance>` namespace with cards, so polymorphic targeting ("pick a card or a zone") works through one ID type. Mutable zone data lives in Store slices: framework-owned `ZoneContentsState` (membership) and `OwnershipState` (owner), plus per-zone-type runtime slices for typed values (counter, recipe progress).

Zones do **not** participate in the entity-variable bridge or modifier-stack machinery. The moment a concept needs stats with modifier stacks, it is no longer a zone — it is a different entity that may have a zone associated with it.

```
                Zone handle (class)
                       │
              IReference (Id, Definition)
                       │
         ┌─────────────┼─────────────────────┐
         │             │                     │
   ZoneContentsState  OwnershipState   Per-zone-type
   (Zone →            (InstanceId →    runtime slice
    list<CardRef>)    Actor)           (game-authored,
         │                              e.g. PileState)
         ▼
   ZoneDefinition (authored)
   - capacity
   - ordering
   - insertion behavior
   - visibility policy
```

## Vocabulary

Feature-owned terms. Cross-feature terms — `Card`, `CardRef`, `CardMapState`, `CardVisibility` (cards/), `Actor`, `IReference`, `InstanceId`, `IState`, `Identifier<T>` — live in their owning features and `Product/ledgers/GLOSSARY.md`.

| Term | Meaning |
|---|---|
| `Zone` | Thin non-generic handle (`Id`, `Definition`); implements `IReference`. One live instance per `InstanceId` in a running engine. |
| `ZoneDefinition` | Authored data: capacity, ordering rules, insertion behavior, default visibility policy. Different zone types vary by definition data, not class hierarchy. |
| `ZoneContentsState` | Framework-owned state slice keyed by zone; holds an ordered `ImmutableList<CardRef>`. The single canonical home for zone membership. |
| Per-zone-type runtime slice | Game-authored typed slice (e.g. `PileState { Counter }`, `RecipeStackState { Progress }`). Independent slices; no shared shape. |
| `InsertCardPayload` | Single payload carrying zone insertion: `Zone`, `Card`, optional `Position`, optional `BypassCapacity`. |
| `InsertPosition` | Sealed record hierarchy: `DefaultPosition`, `AtTop`, `AtBottom`, `AtIndex(int)`. Each variant is its own type. |
| Membership visibility | First visibility channel: can this actor know which instances are in this zone and how arranged? |
| Detail visibility | Second visibility channel: given a known instance, can this actor resolve its definition and variable data? |

## Architecture

The feature has one Model — the Zone Model — which defines the handle, the three state slice categories (`ZoneContentsState`, `OwnershipState`, per-zone-type runtime), the insertion API, the visibility model, and the zone-movement flow.

Zones organize content; they do not own legality. **Movement is approved by game/action rules and organized by zones** — `actions/` validates whether an actor may move an instance; once approved, the zone's mutator updates `ZoneContentsState` and rotates the `CardRef` (per `cards/` mutation contract). Visibility is a coordinated mechanism across cards, zones, and presentation — see [`decisions/0006-zone-visibility-model.md`](decisions/0006-zone-visibility-model.md).

## Composition

| Model | Role |
|---|---|
| [`models/01-zone-model.md`](models/01-zone-model.md) | Zone handle, three state slices, insertion API, visibility model, history filtering, zone-movement flow |

## Invariants

Cross-Model rules that hold across the Model and shape how other features must reference zones.

- **Zones are thin handles.** Implement `IReference`; state lives in slices; one live `Zone` per `InstanceId`.
- **Zones share the `Identifier<Instance>` namespace with cards.** Polymorphic targeting and references work uniformly across cards and zones.
- **Zones do not have stats.** No `EntityVariableState`, no modifier stacks. Stat-bearing things are not zones.
- **Zones do not own broad gameplay legality.** Game/action rules answer "can this actor move this card?"; zones answer "where does an approved instance go?"
- **Zone membership references cards by current `CardRef`.** Refs rotate on every move (per `cards/` mutation contract) and every visibility transition (per [`0006-zone-visibility-model`](decisions/0006-zone-visibility-model.md)).
- **Movement is approved by game/action rules and organized by zones.** Simulation-driven, not view-driven.
- **Server is canonical; visibility filtering at the transport boundary cuts data, never alters identity.** All observers see the same canonical `CardRef`; privacy comes from server-side ref rotation.

## Boundaries

**Owns:** the `Zone` class and `ZoneDefinition` type; `ZoneContentsState` slice; `InsertCardPayload` + `InsertPosition` sealed hierarchy; the visibility *policy* on `ZoneDefinition`; zone-side history filtering rules; the zone-movement flow (validate → approve → organize → record).

**Does not own:**
- Card identity, `CardRef`, `CardMapState`, `CardVisibilityState` → `cards/`
- Entity bridge and per-entity variables → `entities/`
- `OwnershipState` (shared across all `IReference`) — defined in `cards/` Model
- Action pipeline, validation of move legality → `actions/`
- Animation of zone transitions → `presentation/`
- Trigger fold around zone mutations → `triggers/`
- Spatial layout, screen coordinates → `presentation/`

## Dependencies

*Mirrors the front-matter `references:` array in human-readable form (per §3).*

- **Upstream:** `entities/` (foundational definition/instance model; zones share `Identifier<Instance>` namespace), `cards/` (zones contain cards; reads `CardMapState` to resolve refs).
- **Downstream:** `actions/` (validates moves; consumes `InsertCardPayload` via composite mutations), `effects/` (observes zone-membership changes), `engine/` (state-slice infrastructure; framework slices), `presentation/` (consumes zone-keyed state via `CardRef`; applies visibility filter at transport boundary).

## Open

See `ledgers/OPEN-ITEMS.md`:
- `bl-card-location-index` — derived `CardRef → Zone` index for reverse "what zone is this card in?" queries (deferred until concrete consumer surfaces).
- `bl-spatial-rule-attachment` — when games need spatial coordinates as rules data rather than presentation layout.
- Slot/group structure on contents (battlefields with fixed rows or grouped piles) — deferred; flat `IReadOnlyList<Card>` is the current shape.
- Per-actor asymmetric reveals (visible to player A but not player B) — deferred; games model as transient game-state slice.
