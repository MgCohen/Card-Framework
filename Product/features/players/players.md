---
type: feature
status: settled
tags: [#feature, #players]
---

# players — feature

## Purpose
`Player` is the framework's baseline subtype of actor representing a seated, entity-backed participant. The feature defines `Player`'s shape (entity-on-the-bridge with typed `Ref<Player> SelfRef` and `IActor` identity), `PlayerDefinition`'s authoring surface (variable bag for primary state, `ISliceProvider` for non-variable per-player slices, `PerPlayerZones` layout block), and the per-player slice-shape conventions framework Models follow.

## Boundaries
- **Owned:** `Player : InstanceWithSlices<PlayerDefinition>, IActor`, `PlayerDefinition` shape, `PerPlayerZones` layout block, per-player slice-shape conventions (per-player-keyed default vs singleton-with-dict for cross-cutting indexes), the `IActor` interface itself.
- **Not owned:** match-construction wiring (lives in `features/engine/05-game-factory.md` + `engine/07-game-definition.md`), zone semantics (`features/zones/`), card-ownership runtime semantics (`OwnershipState` keyed `Reference → IActor`; mutation lives in `features/cards/` and `features/effects/`), command attribution (lives in `features/actions/`).

## Composition
- `features/players/models/01-player-model.md` — canonical Model. `Player`/`PlayerDefinition` shapes, variable bag for primary state, `ISliceProvider` adoption, per-player slice conventions, `IActor` interface.

## Public surface
- `IActor` — interface carrying `ActorId Id`. The polymorphic actor type framework code uses for "any actor."
- `Player : InstanceWithSlices<PlayerDefinition>, IActor` — entity-backed seated participant.
- `PlayerDefinition : DefinitionWithSlices` — authored data; variable defaults via the bag, per-player non-variable slices via `GetInitialSlices()`, per-player zones via `PerPlayerZones`.
- Non-entity actor records (`World`, `Environment`, `Bot`, `Market`) — lightweight `IActor` implementers carrying just an `ActorId`. Game-side or framework-provided as small records.

## Dependencies
- **Upstream:** `features/entities/` (entity bridge — `InstanceWithSlices<TDef>` and `DefinitionWithSlices`); `features/zones/` (`PerPlayerZones` declares `ZoneDefinition`s); `features/engine/` (factory consumes player layout at `Build`; `IActor` referenced by `OwnershipState` keyed slices).
- **Downstream:** `features/effects/`, `features/actions/`, `features/presentation/` — all reference players for command attribution, target selection, and per-player projections.

## Settled decisions
- Player on the entity bridge (Q1 from the domain modeling design pass, 2026-05-06).
- `IActor` as interface (Q3).
- `PlayerDefinition` adopts `ISliceProvider` from day one (Q2).
- Per-player slice shape convention — per-player-keyed default; singleton-with-dict for cross-cutting indexes (Q6).
- Actor-aware singletons start with default/null and are populated by setup commands after `StartMatch` (Q7).

ADRs in `features/players/decisions/` will land if any sub-question forces a separate ADR; currently none.

## Open
See `ledgers/OPEN-ITEMS.md` for player-related questions. `bl-actor-ownership-reverse-index` is the deferred reverse-direction lookup for `OwnershipState` (raised by Q5 of the design pass; awaiting consumer pressure).
