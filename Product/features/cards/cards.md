---
type: feature
status: draft
references: [features/entities/models/01-definition-instance-model.md, features/zones/models/01-zone-model.md]
tags: [#cards]
---

# cards — feature

## What it is

The framework's card primitive. A `Card` is a thin handle that extends the entity bridge (`StateEntity<CardDefinition>`) and implements `IReference`. All mutable card data — variables, modifiers, current ref, owner, visibility — lives in Store slices, not on the handle. The handle reads typed definition data through the entity bridge; per-card-type variants (Fireball, Goblin) are different `CardDefinition` instances, not different C# classes.

The feature is **data-driven**: there is one `Card` class. Game content lives in definitions and the effect recipes attached to definitions, not in subclasses.

```
                    Card handle (class)
                          │
                  StateEntity<CardDefinition>   ← entity bridge (entities/)
                          │
              ┌───────────┼───────────────────┐
              │           │                   │
       CardMapState  OwnershipState  CardVisibilityState
       (CardRef ↔    (InstanceId →   (InstanceId →
        Card)         Actor)          override)
              │
       Mutations (CompositeAction)
       Create / Move / SetCardVisibility / Destroy
```

## Vocabulary

**Feature-owned terms only.** Cross-feature terms used by this feature — `StateEntity<TDef>`, `CardDefinition`, `IReference`, `InstanceId`, `Actor`, `CompositeAction`, `IState` — live in their owning features or `Product/ledgers/GLOSSARY.md`; link to their canonical home, don't restate.

| Term | Meaning |
|---|---|
| `Card` | Thin non-generic handle on the entity bridge. One live instance per `InstanceId` in a running engine. |
| `CardRef` | Temporary `Guid`-based wire-form reference; rotates on every move and visibility transition. Not an `Identifier<T>`. |
| `CardMapState` | Canonical state slice; bidirectional `CardRef ↔ Card` map. Owns the lifetime of the live `Card` handle. |
| `OwnershipState` | Generic state slice keyed by `InstanceId`; cards and zones (any `IReference`) share it. |
| `CardVisibilityState` | Per-card visibility override slice; absent entry = use the zone's policy. |
| `CardVisibility` | Enum: `Hidden \| VisibleToOwner \| VisibleToAll`. |

## Architecture

The feature has one Model. See `models/01-card-model.md` for the handle, three state slices (`CardMapState`, `OwnershipState`, `CardVisibilityState`), four `CompositeAction` mutations (`Create`, `Move`, `SetCardVisibility`, `Destroy`), and the read API.

## Composition

| Model | Role |
|---|---|
| [`models/01-card-model.md`](models/01-card-model.md) | Card handle, three state slices, four mutations, read API |

## Invariants

Feature-level rules that hold across the Model and shape how other features must reference cards.

- **Two-ID model.** `InstanceId` is stable across the card's lifetime; `CardRef` rotates on every move and visibility transition. The two are not interchangeable.
- **Server-internal vs client-visible split.** Server-internal references (triggers, ongoing effects, attachments, history identifying the card itself) use `Card` or `InstanceId`. Client-visible references and projections (zones, indices, history naming a current location) use `CardRef`.
- **All mutable card state in slices.** Variables, modifiers, current ref, owner, visibility — all live in Store slices. The handle holds no mutable data.
- **`CardRef` rotation is the privacy mechanism.** Observers see refs change but never receive data they aren't allowed to see; the same rotation runs locally and across networks.

## Boundaries

**Owns:** the `Card` class and `CardRef` types; `CardMapState`, `CardVisibilityState`; the four card-lifecycle `CompositeAction` mutations; the `card.GetRef`, `card.GetOwner`, `card.IsVisibleTo` accessors.

**Does not own:**
- Entity bridge and variable/modifier storage → `entities/`
- Zone containment, insertion, capacity → `zones/`
- Visibility *policy* (the per-zone default) → `zones/` (`ZoneDefinition`)
- Action pipeline, command handlers → `actions/`
- Trigger fold semantics around card mutations → `triggers/`
- Effect orchestration of card lifecycle → `effects/`

## Dependencies

*Mirrors the front-matter `references:` array in human-readable form (per §3).*

- **Upstream:** `entities/` (the entity bridge `StateEntity<TDef>`), `zones/` (containment + visibility policy + insertion API).
- **Downstream:** `actions/` (consumes mutations as `CompositeAction`), `effects/` (subscribes to card lifecycle), `engine/` (state slice infrastructure), `presentation/` (consumes card-keyed state via `CardRef`).

## Open

See `ledgers/OPEN-ITEMS.md`:

- `oq-move-validation-execution-drift` — concurrency between move-validate and move-execute when state drifts between the two.
- `oq-entity-destruction` — `Cleanup` hook for destroyed cards (per-card slice cleanup on `DestroyCardMutation`).
