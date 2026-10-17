---
type: feature
status: abandoned
archived_on: 2026-10-18
reason: "Written for the old state model (slices, Ref<T>, MatchStateSlice) and never built. Read it for the open questions, not as the contract; the replacement design comes from Product/spikes/framework-roadmap.md."
references: [features/entities/archive/02-instance.md, features/zones/archive/01-zone-model.md]
tags: [#cards]
---

> **Archived 2026-10-18.** Written for the old state model (slices, Ref<T>, MatchStateSlice) and never built. Read it for the open questions, not as the contract; the replacement design comes from Product/spikes/framework-roadmap.md.

# cards — feature

## What it is

The framework's card primitive. A **`Card`** is a thin handle extending the entity bridge (`InstanceWithSlices<CardDefinition>`) with typed `Ref<Card> SelfRef`, fronted by a privacy-rotating **`CardRef`** for client-visible state. All mutable card data — variables, modifiers, current public ref, owner, visibility — lives in Store slices. Per-card-type variants (Fireball, Goblin, …) are different `CardDefinition` instances, not different C# classes; the framework provides one `Card` class.

```
                Card handle (class)
                       │
              InstanceWithSlices<CardDefinition>   ← entity bridge (entities/)
                       │
         ┌─────────────┼──────────────┬──────────────────┐
         │             │              │                  │
   CardMapState  store.Catalog  OwnershipState   CardVisibilityState
   (CardRef ↔     (Ref<Card> →   (Reference →    (Ref<Card> →
    Ref<Card>)     live Card)     IActor)         override)
         │
   Mutations (CompositeAction)
   Create / Move / SetCardVisibility / Destroy
```

## Vocabulary

**Feature-owned terms only.** Cross-feature terms used by this feature — `InstanceWithSlices<TDef>`, `CardDefinition`, `IEntityVariableStorage`, `Ref<T>`, `Reference`, `IActor`, `IState`, `Store`, `CompositeAction` — live in their owning features or `Product/ledgers/GLOSSARY.md`; link to their canonical home, don't restate.

| Term | Meaning |
|---|---|
| `Card` | Thin handle extending `InstanceWithSlices<CardDefinition>`. Carries `Ref<Card> SelfRef`. One live `Card` per `Ref<Card>` in a running engine; held in `store.Catalog`. |
| `CardRef` | Temporary client-visible reference (`Guid`-backed `record struct`). Rotates on every move and on every visibility transition. **Not** an `Identifier<T>`. |
| `CardMapState` | Privacy-rotation indirection layer. Maps `CardRef → Ref<Card>` (and reverse index for O(1) `RefOf`). Holds typed refs only — never live `Card` handles. |
| `OwnershipState` | Generic state slice keyed by `Reference`; cards and zones (any catalog-registered handle) share it. |
| `CardVisibilityState` | Per-card visibility override slice keyed by `Ref<Card>`; absent entry = use the zone's policy. |
| `CardVisibility` | Enum: `Hidden \| VisibleToOwner \| VisibleToAll`. |

## Architecture

The feature has one Model. See `01-card-model.md` for the handle, the four state slices (`CardMapState`, `OwnershipState`, `CardVisibilityState`, plus the entity-bridge `EntityState` keyed by `Ref<Card>`), the four `CompositeAction` mutations (`Create`, `Move`, `SetCardVisibility`, `Destroy`), the resolution surfaces (`store.Catalog` + two-hop helper), and the read API.

## Composition

| Model | Role |
|---|---|
| [`01-card-model.md`](features/cards/archive/01-card-model.md) | Card handle, four state slices, four mutations, resolution surfaces, read API |

## Invariants

Cross-Model rules that hold across the Model and shape how other features must reference cards.

- **Two-ID model.** `Ref<Card>` is stable across the card's lifetime; `CardRef` rotates on every move and visibility transition. The two are not interchangeable.
- **Server-internal vs client-visible split.** Server-internal references (triggers, ongoing effects, attachments, history identifying the card itself) use `Card` or `Ref<Card>`. Client-visible references and projections (zones, indices, history naming a current location) use `CardRef`.
- **All mutable card state in slices.** Variables, modifiers, current public ref, owner, visibility — all live in Store slices. The handle holds no mutable data beyond `SelfRef`, protected `Definition`, and `Storage`.
- **`CardRef` rotation is the privacy mechanism.** Observers see refs change but never receive data they aren't allowed to see; the same rotation runs locally and across networks.
- **One live `Card` per `Ref<Card>`.** Held in `store.Catalog`; equality is `SelfRef`-based.

## Boundaries

**Owns:** the `Card` class and `CardRef` types; `CardMapState`, `CardVisibilityState`; the four card-lifecycle `CompositeAction` mutations; the `card.GetRef`, `card.GetOwner`, `card.IsVisibleTo` accessors.

**Does not own:**
- Entity bridge and variable/modifier storage → `entities/`
- Zone containment, insertion, capacity → `zones/`
- Visibility *policy* (the per-zone default) → `zones/` (`ZoneDefinition`)
- `OwnershipState` slice mechanism — defined here, but generic across all catalog-registered handles → shared with `zones/`, `players/`
- Action pipeline, command handlers → `actions/`
- Trigger fold semantics around card mutations → `triggers/`
- Effect orchestration of card lifecycle → `effects/`
- `store.Catalog` registry mechanism → `engine/`

## Dependencies

*Mirrors the front-matter `references:` array in human-readable form (per §3).*

- **Upstream:** `entities/` (the entity bridge `InstanceWithSlices<TDef>`, storage adapter `IEntityVariableStorage`, runtime spawn surface), `zones/` (containment + visibility policy + insertion API; cards are placed into zones).
- **Downstream:** `actions/` (consumes mutations as `CompositeAction`), `effects/` (subscribes to card lifecycle), `engine/` (state slice infrastructure; `store.Catalog`), `presentation/` (consumes card-keyed state via `CardRef`).

## Open

See `ledgers/OPEN-ITEMS.md`:

- `oq-move-validation-execution-drift` — concurrency between move-validate and move-execute when state drifts between the two.
- `oq-entity-destruction` — `Cleanup` hook for destroyed cards (per-card slice cleanup on `DestroyCardMutation`).
- `bl-card-resolution-helper-shape` — final form for the `CardRef → Card` two-hop resolution helper.
