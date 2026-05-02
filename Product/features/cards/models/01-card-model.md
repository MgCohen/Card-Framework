---
type: model
status: settled
last-updated: 2026-05-01
builds-on: features/entities/models/01-definition-instance-model.md
tags: [#cards, #model]
---

# Card Model

A Card is the framework's representation of a card-shaped game entity. It uses the entity bridge (`StateEntity<CardDefinition>`) for variables and modifiers, lives canonically in `CardMapState` keyed by a temporary `CardRef`, and exposes its current ref and owner through state-reading methods.

Related docs:

- [Entity, Definition, And Instance Model](features/entities/models/01-definition-instance-model.md)
- [Zone Model](features/zones/models/01-zone-model.md)
- [ADR-0001 — Card shape](features/cards/decisions/0001-card-shape.md)
- [ADR-0005 — Objects over IDs](decisions/0005-objects-over-ids.md)
- [Zones ADR-0002 — Visibility model](features/zones/decisions/0002-visibility-model.md)

## Model Summary

A Card is a thin handle over the entity bridge:

> Card = `StateEntity<CardDefinition>` + `IReference` + state-side accessors for `CardRef` and `Owner`.

The handle itself is stateless beyond its `InstanceId` and Definition reference. All mutable card data — variables, modifiers, current ref, owner, visibility — lives in Store slices. Reads happen by passing state into accessor methods (`card.GetRef(state)`, `card.GetOwner(state)`).

The framework is **data-driven**: there is one `Card` class. Different card types (Fireball, Goblin, etc.) are different `CardDefinition` instances with their own data fields and effect recipes — not different C# classes. The `Card` handle reads typed definition data through the entity bridge.

## Card Class

```csharp
public sealed class Card : StateEntity<CardDefinition>, IReference
{
    public InstanceId Id => InstanceId;     // satisfies IReference

    public CardRef GetRef(IStateScope state) =>
        state.Get<CardMapState>().RefOf(this);

    public Actor? GetOwner(IStateScope state) =>
        state.Get<OwnershipState>().OwnerOf(this);

    public override bool Equals(object? obj) =>
        obj is Card other && InstanceId.Equals(other.InstanceId);

    public override int GetHashCode() => InstanceId.GetHashCode();
}
```

Card variants (Fireball, Goblin, etc.) are not separate C# classes — they are distinct `CardDefinition` instances. A "Fireball" card is a `Card` handle whose definition is a `CardDefinition` configured with Fireball-specific data and effect recipes. Per the data-driven model, the framework provides one `Card` type; game content lives in definitions, not in subclasses.

The class is a handle, not a record. Equality is overridden to use `InstanceId` so the same card can be looked up reliably by Card object across handle re-creation (e.g., snapshot rehydration). There is at most one *live* `Card` instance per `InstanceId` in the running engine — referenced from `CardMapState` and shared by all framework code that operates on that card.

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

    public ImmutableDictionary<Card, CardRef> RefByCard { get; init; }
        = ImmutableDictionary<Card, CardRef>.Empty;

    public Card GetCard(CardRef @ref) => Cards[@ref];

    public CardRef RefOf(Card card) => RefByCard[card];
}
```

The map holds `Card` objects directly (not `InstanceId` values). Lookups are one-hop. The Card object's lifetime is owned by the map — when a card is created, its handle is constructed and inserted; when destroyed, removed.

The parallel `RefByCard` index makes `RefOf(card)` O(1) — important because `card.GetRef(state)` is called inside every mutation that rotates the ref. The index uses `Card`'s `InstanceId`-based equality so it remains correct across handle re-creation. Mutators that write `CardMapState` update both dictionaries atomically.

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

## Card Visibility

Visibility uses a per-card runtime override on top of the zone's default policy (the policy itself lives on `ZoneDefinition` — see [Zones ADR-0002](features/zones/decisions/0002-visibility-model.md)). The override is a state slice keyed by `InstanceId`:

```csharp
public enum CardVisibility { Hidden, VisibleToOwner, VisibleToAll }

public sealed record CardVisibilityState : IState
{
    public ImmutableDictionary<InstanceId, CardVisibility> Overrides { get; init; }
        = ImmutableDictionary<InstanceId, CardVisibility>.Empty;

    public CardVisibility? OverrideFor(InstanceId id) =>
        Overrides.TryGetValue(id, out var v) ? v : null;
}
```

An absent entry on the slice means "use the zone's policy default." `VisibleToOwner` requires the card to have an Owner; collapses to `Hidden` if no Owner.

Rules read effective visibility through the card object: `card.IsVisibleTo(state, actor)` combines the zone policy and per-card override.

## Mutations

All card lifecycle operations are typed `: CompositeAction`. As CompositeActions, they pass through their own trigger fold — game effects can intercept create / move / set-visibility / destroy via before/after triggers (see the triggers feature). The ones that write the map require a fresh `CardRef` to be passed in via the payload — this ensures determinism across all sub-mutators within the composite and supports replay.

```csharp
public sealed record CreateCardMutation(
    CardDefinition Definition,
    InstanceId NewInstanceId,
    CardRef NewRef,
    Zone? IntoZone = null,
    InsertPosition? Position = null) : CompositeAction
{
    public static CreateCardMutation Of(CardDefinition definition)
        => new(definition, InstanceId.NewId(), CardRef.NewRef());

    public static CreateCardMutation Into(CardDefinition definition, Zone zone)
        => new(definition, InstanceId.NewId(), CardRef.NewRef(), zone);
}

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

public sealed record SetCardVisibilityMutation(
    Card Card,
    CardRef NewRef,
    CardVisibility Visibility) : CompositeAction
{
    public static SetCardVisibilityMutation Hide(Card card)
        => new(card, CardRef.NewRef(), CardVisibility.Hidden);

    public static SetCardVisibilityMutation ShowAll(Card card)
        => new(card, CardRef.NewRef(), CardVisibility.VisibleToAll);

    public static SetCardVisibilityMutation ShowToOwner(Card card)
        => new(card, CardRef.NewRef(), CardVisibility.VisibleToOwner);
}

public sealed record DestroyCardMutation(Card Card) : CompositeAction;
```

`DestroyCardMutation` does not need a `NewRef` — it only removes from `CardMapState`. `CreateCardMutation` takes the `Definition` (not a pre-built `Card`) — the framework constructs the `Card` handle inside the composite, where the Store reference is available.

For `BypassCapacity = true` (force-effects bypassing zone defaults), use a record `with` expression rather than a separate factory: `MoveCardMutation.AtTop(card, src, dst) with { BypassCapacity = true }`. Same pattern applies to `InsertCardPayload`.

`InsertPosition` is nullable on the `CreateCardMutation` payload; `null` means "use the destination zone's default insertion behavior." `MoveCardMutation` takes `InsertPosition` non-null because callers always pass an explicit position via the factories — game code that wants the destination's default uses `new DefaultPosition()` explicitly.

### Composite expansion (in prose, not normative API)

- **CreateCardMutation** — constructs the `Card` handle from `(NewInstanceId, Definition, store)`; inserts `(NewRef, Card)` into `CardMapState` (and updates `RefByCard`); initializes the card's `EntityVariableState` slice from `Definition.BaseValues`; if `IntoZone` is non-null, inserts `NewRef` into that zone's `ZoneContentsState` at `Position`.
- **MoveCardMutation** — removes `card.GetRef(state)` from `Source.ZoneContentsState`; rotates the entry in `CardMapState` (`oldRef → NewRef`); inserts `NewRef` into `Destination.ZoneContentsState` at `Position`. **Precondition:** `Card` must currently be in `Source` (mutator throws if not — see `oq-move-validation-execution-drift` in `OPEN-QUESTIONS.md` for the deferred concurrency question).
- **SetCardVisibilityMutation** — rotates the entry in `CardMapState`; swaps `oldRef → NewRef` in any `ZoneContentsState` containing the card; writes `CardVisibilityState[card.InstanceId] = Visibility`.
- **DestroyCardMutation** — removes the entry from `CardMapState`; removes the card from any `ZoneContentsState`; removes per-card slices keyed by `InstanceId` (variables, modifiers, visibility); fires the cleanup hook (per `oq-entity-destruction`, still open).

### Construction helper

For the common case of "create a card and use it immediately," a Store extension provides sugar:

```csharp
public static class StoreExtensions
{
    public static Card CreateCard(this Store store, CardDefinition definition, Zone? intoZone = null)
    {
        var ref_ = CardRef.NewRef();
        store.Execute(new CreateCardMutation(definition, InstanceId.NewId(), ref_, intoZone));
        return store.Get<CardMapState>().GetCard(ref_);
    }
}

// Use:
var card = store.CreateCard(fireballDefinition, p1Hand);
store.Execute(MoveCardMutation.AtTop(card, p1Hand, battlefield));
```

## Read API

Reading a card from a `CardRef`:

```csharp
var card = state.Get<CardMapState>().GetCard(someRef);
```

Reading the cards in a zone — see `Zone.GetCards(state)` defined on the Zone class in [Zone Model § Zone Shape](features/zones/models/01-zone-model.md).

Reading a card's current ref or owner:

```csharp
var ref = card.GetRef(state);      // current CardRef
var owner = card.GetOwner(state);  // Actor or null
```

## Useful Invariants

- One `Card` object per `InstanceId` in the running engine — referenced from `CardMapState` and shared.
- `InstanceId` is stable across the card's lifetime; `CardRef` rotates on every move and visibility transition.
- All mutable card state (variables, modifiers, current ref, owner, visibility) lives in slices.
- Server-internal references (triggers, ongoing effects, attachments, history-side identity such as `CardRefRotated`) use the `Card` object or its `InstanceId` — never the rotating `CardRef`.
- Client-visible state and projections (zones, indices, history entries naming a current location like `ZoneContentInserted`) reference cards by `CardRef`.
- `CardRef` rotation is the framework's privacy mechanism; observers see refs change but never receive data they aren't allowed to see.