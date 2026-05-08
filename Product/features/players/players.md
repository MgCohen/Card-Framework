---
type: feature
status: settled
references: [features/entities/models/02-runtime-surface.md, features/zones/models/01-zone-model.md, features/engine/models/05-game-factory.md, features/engine/models/07-game-definition.md]
tags: [#players]
---

# players — feature

## What it is

`Player` is the framework's baseline subtype of actor representing a seated, entity-backed participant. The feature defines `Player`'s shape (entity-on-the-bridge with typed `Ref<Player> SelfRef` and `IActor` identity), `PlayerDefinition`'s authoring surface (variable bag for primary state, `ISliceProvider` for non-variable per-player slices, `PerPlayerZones` layout block), and the per-player slice-shape conventions framework Models follow.

```
                       IActor (interface, ActorId)
                              │
              ┌───────────────┴───────────────┐
              │                               │
   Player : InstanceWithSlices       World, Environment, Bot,
   <PlayerDefinition>, IActor         Market : record (ActorId)
   ├ Ref<Player> SelfRef              (lightweight, non-entity)
   ├ ActorId Id
   ├ EntityState (variables)
   ├ Per-player slices (via ISliceProvider)
   └ PerPlayerZones (hand, deck, …)
```

## Vocabulary

**Feature-owned terms only.** Cross-feature terms used by this feature — `InstanceWithSlices<TDef>`, `DefinitionWithSlices`, `IEntityVariableStorage`, `EntityState`, `Variable`, `VariableValue`, `ISliceProvider` (defined in `entities/`); `ZoneDefinition` (defined in `zones/`); `Reference`, `Ref<T>`, `Store`, `OwnershipState`, `MatchSetup`, `IGameFactory`, `GameDefinition` (defined in `engine/`); `IState` — live in their owning features or `Product/ledgers/GLOSSARY.md`; link to their canonical home, don't restate.

| Term | Meaning |
|---|---|
| `IActor` | Interface carrying `ActorId Id`. The polymorphic actor type framework code uses for "any actor." Implementers are either entity-backed (`Player`) or lightweight records (`World`, `Environment`, `Bot`, `Market`). |
| `ActorId` | Identifier of an actor. Used for command attribution, history envelopes, per-actor query APIs. |
| `Player` | `class Player : InstanceWithSlices<PlayerDefinition>, IActor` (unsealed). Entity-backed seated participant. Carries both `Ref<Player> SelfRef` (typed runtime identity) and `ActorId` (actor identity). One live `Player` per `Ref<Player>` in `store.Catalog`. |
| `PlayerDefinition` | `abstract class PlayerDefinition : DefinitionWithSlices`. Authored data: variable defaults via the entity-bridge bag (StartingLife, etc.), per-player non-variable slices via `GetInitialSlices()`, per-player zones via `PerPlayerZones`. |
| `PerPlayerZones` | Player's layout block: `IEnumerable<ZoneDefinition>` declaring per-player zones (hand, deck, discard). The factory spawns one `Zone` per entry per player actor at `Build`. |
| Per-player-keyed slice | Slice keyed at `player.SelfRef`. Default shape for substantial primary per-player state. Mirrors `EntityState` / `ZoneContentsState` precedent. |
| Singleton-with-dict slice | Slice with `IReadOnlyDictionary<IActor, T>` field. For cross-cutting indexes that read holistically across players (leaderboard, ready-status). Mirrors `OwnershipState` precedent. |

## Architecture

The feature has one Model. See `models/01-player-model.md` for the `IActor` interface, `Player` class shape, `PlayerDefinition` authoring surface, the per-player slice-shape convention (per-player-keyed default vs singleton-with-dict for cross-cutting), and the `Player.Spawn` mutation flow.

## Composition

| Model | Role |
|---|---|
| [`01-player-model.md`](features/players/models/01-player-model.md) | `IActor` interface, `Player` and `PlayerDefinition` shapes, variable bag for primary state, `ISliceProvider` adoption, per-player slice conventions, mutation flow |

## Invariants

Cross-Model rules that hold across the Model and shape how other features must reference players.

- **Two-ID model.** `Player` carries both `Ref<Player> SelfRef` (typed runtime identity for catalog/state-keying/subscriptions) and `ActorId Id` (actor identity for command attribution, history, per-actor queries). Both stable for the player's lifetime; mapping held on the handle itself.
- **`Player` is on the entity bridge.** `Player : InstanceWithSlices<PlayerDefinition>, IActor`. Variables (life, mana count, energy) live in `EntityState` via the bridge.
- **`PlayerDefinition` adopts `ISliceProvider` from day one.** Symmetric with `ZoneDefinition` and `CardDefinition` (per the open-seams pass). Default empty `GetInitialSlices()`; concrete subclasses override for game-specific per-player non-variable state.
- **Non-entity actors are lightweight records.** `World`, `Environment`, `Bot`, `Market` implement `IActor` directly with just an `ActorId`. They cannot own variables or per-instance slices; their participation is purely as `IActor` values in framework slices like `OwnershipState`.
- **Per-player zones are structural.** Declared in `PlayerDefinition.PerPlayerZones`; instantiated per player actor at `Build`. Stable over the match lifetime in normal play.
- **One live `Player` per `Ref<Player>`.** Held in `store.Catalog`; equality is `SelfRef`-based.

## Boundaries

**Owns:** the `IActor` interface; `Player` class; `PlayerDefinition` shape (variable bag + `GetInitialSlices()` + `PerPlayerZones`); per-player slice-shape conventions (per-player-keyed default vs singleton-with-dict); `Player.Spawn` mutation flow.

**Does not own:**
- Entity bridge — `InstanceWithSlices<TDef>`, `DefinitionWithSlices`, `IEntityVariableStorage` → `entities/`
- Zone semantics, `ZoneDefinition` → `zones/`
- Match-construction wiring — `MatchSetup.Actors`, the `Build` walk that calls `Player.Spawn` → `engine/05-game-factory.md` + `engine/07-game-definition.md`
- Card-ownership runtime semantics — `OwnershipState` keyed `Reference → IActor`; mutation lives in → `cards/` and `effects/`
- Command attribution — `GameAction.Actor` carries `ActorId`; pipeline wiring → `actions/`
- Per-actor visibility filtering at the transport boundary → `presentation/`

## Dependencies

*Mirrors the front-matter `references:` array in human-readable form (per §3).*

- **Upstream:** `entities/` (entity bridge — `InstanceWithSlices<TDef>` and `DefinitionWithSlices`); `zones/` (`PerPlayerZones` declares `ZoneDefinition`s); `engine/` (factory consumes player layout at `Build`; `IActor` referenced by `OwnershipState`-keyed slices and by `MatchSetup.Actors`).
- **Downstream:** `effects/`, `actions/`, `presentation/` — all reference players for command attribution, target selection, and per-player projections.

## Open

See `ledgers/OPEN-ITEMS.md`:

- `bl-actor-ownership-reverse-index` — deferred reverse-direction lookup on `OwnershipState` (raised by Q5 of the domain modeling design pass; awaiting consumer pressure).
- `ActorId → Ref<Player>` index — not framework-shipped; games that need it build a small index slice during setup. Pin if a real game forces a framework-side helper.
- Player destruction mid-match — not currently a framework primitive. Most games run with fixed actor catalogs. Games that need it model as game-side state (e.g., a "conceded" flag) rather than catalog removal.
