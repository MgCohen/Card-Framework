---
type: feature
status: settled
references: [features/entities/models/02-runtime-surface.md, features/cards/models/01-card-model.md]
tags: [#zones]
---

# zones — feature

## What it is

The framework's container primitive. A **`Zone`** is a thin handle extending the entity bridge (`InstanceWithSlices<ZoneDefinition>`) with typed `Ref<Zone> SelfRef`. Zones share the polymorphic `Reference` type with cards and players, so targeting works uniformly ("pick a card or a zone"). Mutable zone data lives in Store slices: framework-owned `ZoneContentsState` (membership) and `OwnershipState` (owner), plus per-zone-type runtime slices for typed values (counter, recipe progress).

Zones use the entity bridge for any variables they declare. Most zones (hand, deck, discard) declare zero variables and pay no cost. The moment a concept needs stat-bearing semantics with deep modifier stacks, it belongs on a different entity that may have a zone associated with it.

```
                Zone handle (class)
                       │
              InstanceWithSlices<ZoneDefinition>
                       │
         ┌─────────────┼─────────────────────┐
         │             │                     │
   ZoneContentsState  OwnershipState   Per-zone-type
   (Zone →            (Reference →     runtime slice
    list<CardRef>)    Actor)           (game-authored,
                                        e.g. PileState)
         │
   ZoneDefinition (authored data)
   - capacity
   - ordering
   - insertion behavior
   - default visibility policy
```

## Vocabulary

**Feature-owned terms only.** Cross-feature terms used by this feature — `InstanceWithSlices<TDef>`, `IEntityVariableStorage`, `Card`, `CardRef`, `CardMapState`, `CardVisibility` (cards/), `IActor`, `Reference`, `Ref<T>`, `IState`, `Store`, `CompositeAction` — live in their owning features or `Product/ledgers/GLOSSARY.md`; link to their canonical home, don't restate.

| Term | Meaning |
|---|---|
| `Zone` | Thin handle extending `InstanceWithSlices<ZoneDefinition>`. Carries `Ref<Zone> SelfRef`. One live `Zone` per `Ref<Zone>` in a running engine; held in `store.Catalog`. **Not sealed**. |
| `ZoneDefinition` | Authored data: capacity, ordering rules, insertion behavior, default visibility policy. Different zone types vary by definition data, not class hierarchy. |
| `ZoneContentsState` | Framework-owned state slice keyed by `Ref<Zone>`; holds an ordered `ImmutableList<CardRef>`. The single canonical home for zone membership. |
| Per-zone-type runtime slice | Game-authored typed slice keyed by `Ref<Zone>` (e.g. `PileState { Counter, Target }`, `RecipeStackState { Progress, Active }`). Independent slices; no shared shape. |
| `InsertCardPayload` | Single payload carrying zone insertion: `Zone`, `Card`, optional `Position`, optional `BypassCapacity`. |
| `InsertPosition` | Sealed record hierarchy: `DefaultPosition`, `AtTop`, `AtBottom`, `AtIndex(int)`. Each variant is its own type. |
| `ZoneVisibilityPolicy` | Sealed record hierarchy on `ZoneDefinition`: `AllPublic`, `AllHidden`, `OwnerSees`, `OwnerSeesOthersCount`, `OwnerCountOthersNothing`, `Custom`. |
| Membership visibility | First visibility channel: can this actor know which instances are in this zone and how arranged? |
| Detail visibility | Second visibility channel: given a known instance, can this actor resolve its definition and variable data? |

## Architecture

The feature has one Model. See `models/01-zone-model.md` for the handle, the three state slice categories (`ZoneContentsState`, `OwnershipState`, per-zone-type runtime), the insertion API (`InsertCardPayload` + `InsertPosition` hierarchy), the visibility model (`ZoneVisibilityPolicy` + per-card override), history filtering, and the zone-movement flow.

## Composition

| Model | Role |
|---|---|
| [`01-zone-model.md`](features/zones/models/01-zone-model.md) | Zone handle, three state slice categories, insertion API, visibility model, history filtering, zone-movement flow |

## Invariants

Cross-Model rules that hold across the Model and shape how other features must reference zones.

- **Zones are thin handles.** Implement `InstanceWithSlices<ZoneDefinition>`; carry typed `Ref<Zone> SelfRef`; state lives in slices; one live `Zone` per `Ref<Zone>` in `store.Catalog`.
- **Zones share the polymorphic `Reference` namespace with cards.** Targeting and references work uniformly across cards and zones.
- **Zones do not own broad gameplay legality.** Game/action rules answer "can this actor move this card?"; zones answer "where does an approved instance go?"
- **Zone membership references cards by current `CardRef`.** Refs rotate on every move (per `cards/` mutation contract) and every visibility transition (per `Product/decisions/0008-zone-visibility-model.md`).
- **Movement is approved by game/action rules and organized by zones.** Simulation-driven, not view-driven.
- **Server is canonical; visibility filtering at the transport boundary cuts data, never alters identity.** All observers see the same canonical `CardRef`; privacy comes from server-side ref rotation.

## Boundaries

**Owns:** the `Zone` class and `ZoneDefinition` type; `ZoneContentsState` slice; `InsertCardPayload` + `InsertPosition` sealed hierarchy; the visibility *policy* hierarchy on `ZoneDefinition`; zone-side history filtering rules; the zone-movement flow (validate → approve → organize → record).

**Does not own:**
- Card identity, `CardRef`, `CardMapState`, `CardVisibilityState` → `cards/`
- Entity bridge and per-entity variables → `entities/`
- `OwnershipState` slice — defined in `cards/`, generic across all `Reference`-bearing handles
- Action pipeline, validation of move legality → `actions/`
- Animation of zone transitions → `presentation/`
- Trigger fold around zone mutations → `triggers/`
- Spatial layout, screen coordinates → `presentation/`
- `store.Catalog` registry mechanism → `engine/`

## Dependencies

*Mirrors the front-matter `references:` array in human-readable form (per §3).*

- **Upstream:** `entities/` (foundational bridge model and runtime spawn surface; zones share the `Reference` polymorphic type), `cards/` (zones contain cards; reads `CardMapState` to resolve refs).
- **Downstream:** `actions/` (validates moves; consumes `InsertCardPayload` via composite mutations), `effects/` (observes zone-membership changes), `engine/` (state-slice infrastructure; `store.Catalog`; framework slices), `presentation/` (consumes zone-keyed state via `CardRef`; applies visibility filter at transport boundary).

## Open

See `ledgers/OPEN-ITEMS.md`:

- `bl-card-location-index` — derived `CardRef → Zone` index for reverse "what zone is this card in?" queries (deferred until concrete consumer surfaces).
- `bl-spatial-rule-attachment` — when games need spatial coordinates as rules data rather than presentation layout.
- `bl-zone-visibility-custom-policy-serialization` — serialization story for `Custom` zone-visibility-policy delegates.
- Slot/group structure on contents (battlefields with fixed rows or grouped piles) — deferred; flat `IReadOnlyList<CardRef>` is the current shape.
- Per-actor asymmetric reveals (visible to player A but not player B) — deferred; games model as transient game-state slice.
