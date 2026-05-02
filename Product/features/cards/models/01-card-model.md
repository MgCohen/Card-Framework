---
type: model
status: settled
last-updated: 2026-05-01
builds-on: features/entities/models/01-definition-instance-model.md
tags: [#cards, #model]
---

# Card Model

A Card is the framework's representation of a card-shaped game entity. It uses the entity bridge (`StateEntity<TCardDef>`) for variables and modifiers, lives canonically in `CardMapState` keyed by a temporary `CardRef`, and exposes its current ref and owner through state-reading methods.

Related docs:

- [Entity, Definition, And Instance Model](features/entities/models/01-definition-instance-model.md)
- [Zone Model](features/zones/models/01-zone-model.md)
- [ADR-0001 — Card shape](features/cards/decisions/0001-card-shape.md)
- [ADR-0005 — Objects over IDs](decisions/0005-objects-over-ids.md)
- [Zones ADR-0002 — Visibility model](features/zones/decisions/0002-visibility-model.md)

## Model Summary

A Card is a thin handle over the entity bridge:

> Card = `StateEntity<TCardDef>` + `IReference` + state-side properties (current `CardRef`, `Owner`).

The handle itself is stateless beyond its `InstanceId` and Definition reference. All mutable card data — variables, modifiers, current ref, owner, visibility — lives in Store slices. Reads happen by passing state into accessor methods (`card.GetRef(state)`, `card.GetOwner(state)`).

## Card Class

```csharp
public abstract class Card : StateEntity<CardDefinition>, IReference
{
    public InstanceId Id => InstanceId;     // satisfies IReference

    public CardRef GetRef(IStateScope state) =>
        state.Get<CardMapState>().RefOf(this);

    public Actor? GetOwner(IStateScope state) =>
        state.Get<OwnershipState>().OwnerOf(this);
}
```

Game-specific card subclasses extend `Card` (or `StateEntity<TConcreteDef>` directly) and pin the typed definition:

```csharp
public sealed record FireballDefinition : CardDefinition { /* ... */ }

public sealed class Fireball : Card { /* typed access via StateEntity<CardDefinition> bridge */ }
```

The class is a handle, not a record. There is at most one `Card` instance per `InstanceId` in the running engine — referenced from `CardMapState` and shared by all framework code that operates on that card.

## CardRef

`CardRef` is the temporary reference used in client-visible state — zones, indices, anything that gets sent to or read by clients. It rotates on every move and on every visibility transition.

```csharp
public readonly record struct CardRef(Guid Value)
{
    public static CardRef NewRef() => new(Guid.NewGuid());
}
```

`CardRef` is **not** an `Identifier<T>`. Identifiers are stable references; `CardRef` is deliberately unstable. The two coexist:

- `InstanceId` — stable. Used for variable storage, modifier storage, ownership, visibility, history, all server-internal subscriptions.
- `CardRef` — temporary. Used as the `CardMapState` key, in `ZoneContentsState`, in client-facing slices and projections.

Rotating `CardRef` is the framework's privacy mechanism. When a card hides or moves, the old ref is retired and a fresh one issued. Observers who held the old ref see it disappear; observers who never had data behind that ref see only the new ref appear.

## CardMapState

The single canonical slice mapping `CardRef` to `Card` instances. Replaces what would otherwise be an engine-side handle registry — putting it on the Store gives snapshot/rollback for free.

```csharp
public sealed record CardMapState : IState
{
    public ImmutableDictionary<CardRef, Card> Cards { get; init; }
        = ImmutableDictionary<CardRef, Card>.Empty;

    public Card GetCard(CardRef @ref) => Cards[@ref];

    public CardRef RefOf(Card card) =>
        Cards.First(kv => kv.Value.InstanceId == card.InstanceId).Key;
}
```

The map holds `Card` objects directly (not `InstanceId` values). Lookups are one-hop. The Card object's lifetime is owned by the map — when a card is created, its handle is constructed and inserted; when destroyed, removed.

## OwnershipState

Generic across all `IReference` instances. Both Cards and Zones read ownership through this slice.

```csharp
public sealed record OwnershipState : IState
{
    public ImmutableDictionary<InstanceId, Actor> Owners { get; init; }
        = ImmutableDictionary<InstanceId, Actor>.Empty;

    public Actor? OwnerOf(IReference @ref) =>
        Owners.TryGetValue(@ref.Id, out var actor) ? actor : null;
}
```

Owner mutations go through the Store like any other state change — there is no direct `card.Owner = ...` mutation path. Cards / zones without an owner simply have no entry in the slice; `OwnerOf(...)` returns null.

## Mutations

All card lifecycle operations are composite mutations. The ones that write the map require a fresh `CardRef` to be passed in via the payload — this ensures determinism across all sub-mutators within the composite and supports replay.

```csharp
public sealed record CreateCardMutation(Card Card, CardRef NewRef) : CompositeAction;

public sealed record MoveCardMutation(
    Card Card,
    CardRef NewRef,
    Zone Source,
    Zone Destination,
    InsertPosition Position,
    bool BypassCapacity = false) : CompositeAction
{
    public static MoveCardMutation AtTop(Card card, Zone source, Zone destination)
        => new(card, CardRef.NewRef(), source, destination, new AtTop());

    public static MoveCardMutation AtBottom(Card card, Zone source, Zone destination)
        => new(card, CardRef.NewRef(), source, destination, new AtBottom());

    public static MoveCardMutation AtIndex(Card card, Zone source, Zone destination, int index)
        => new(card, CardRef.NewRef(), source, destination, new AtIndex(index));
}

public sealed record HideCardMutation(Card Card, CardRef NewRef) : CompositeAction
{
    public static HideCardMutation Of(Card card) => new(card, CardRef.NewRef());
}

public sealed record ShowCardMutation(
    Card Card,
    CardRef NewRef,
    CardVisibility ShowAs = CardVisibility.VisibleToAll) : CompositeAction
{
    public static ShowCardMutation Of(Card card, CardVisibility showAs = CardVisibility.VisibleToAll)
        => new(card, CardRef.NewRef(), showAs);
}

public sealed record DestroyCardMutation(Card Card) : CompositeAction;
```

`DestroyCardMutation` does not need a `NewRef` — it only removes from `CardMapState`.

### Composite expansion (in prose, not normative API)

- **CreateCardMutation** — inserts `(NewRef, Card)` into `CardMapState`; initializes the card's `EntityVariableState` slice from `Definition.BaseValues`; if the card enters a zone immediately, also inserts `NewRef` into that zone's `ZoneContentsState`.
- **MoveCardMutation** — removes `card.GetRef(state)` from `Source.ZoneContentsState`; rotates the entry in `CardMapState` (`oldRef → NewRef`); inserts `NewRef` into `Destination.ZoneContentsState` at `Position`; rebinds `CardLocationIndex` for the card.
- **HideCardMutation** / **ShowCardMutation** — rotates the entry in `CardMapState`; swaps `oldRef → NewRef` in any `ZoneContentsState` containing the card; rebinds `CardLocationIndex`; writes `CardVisibilityState[card.InstanceId]`.
- **DestroyCardMutation** — removes the entry from `CardMapState`; removes the card from any `ZoneContentsState`; removes from `CardLocationIndex`; removes per-card slices keyed by `InstanceId` (variables, modifiers, visibility); fires the cleanup hook (per `oq-entity-destruction`, still open).

## Read API

Reading a card from a `CardRef`:

```csharp
var card = state.Get<CardMapState>().GetCard(someRef);
```

Reading the cards in a zone:

```csharp
public IEnumerable<Card> GetCards(this Zone zone, IStateScope state)
{
    var contents = state.Get<ZoneContentsState>(zone);
    var map = state.Get<CardMapState>();
    return contents.Cards.Select(map.GetCard);
}
```

Reading a card's current ref or owner:

```csharp
var ref = card.GetRef(state);      // current CardRef
var owner = card.GetOwner(state);  // Actor or null
```

## CardLocationIndex

Derived aggregate slice keyed by `CardRef → Zone`. Maintained atomically by the same mutators that write `ZoneContentsState`.

```csharp
public sealed record CardLocationIndex : IState
{
    public ImmutableDictionary<CardRef, Zone> Locations { get; init; }
        = ImmutableDictionary<CardRef, Zone>.Empty;

    public Zone? LocationOf(CardRef @ref) =>
        Locations.TryGetValue(@ref, out var zone) ? zone : null;
}
```

Backs reverse queries: "what zone is this card in?" is O(1).

## Useful Invariants

- One `Card` object per `InstanceId` in the running engine — referenced from `CardMapState` and shared.
- `InstanceId` is stable across the card's lifetime; `CardRef` rotates on every move and visibility transition.
- All mutable card state (variables, modifiers, current ref, owner, visibility) lives in slices.
- Server-internal references (triggers, ongoing effects, attachments, history) use the `Card` object or its `InstanceId` — never `CardRef`.
- Client-visible state and projections (zones, indices, history sent to clients) reference cards by `CardRef`.
- `CardRef` rotation is the framework's privacy mechanism; observers see refs change but never receive data they aren't allowed to see.