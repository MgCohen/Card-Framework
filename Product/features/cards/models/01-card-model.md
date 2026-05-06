---
type: model
status: settled
references: features/entities/models/01-definition-instance-model.md
tags: [#cards, #model]
---

# Card Model

A Card is the framework's representation of a card-shaped game entity. It uses the entity bridge (`EntityInstance<CardDefinition>`) for variables and modifiers, lives in `store.Catalog` indexed by typed `Ref<Card>`, and is fronted by a privacy-rotating `CardRef` for client-visible state. The `CardRef → Ref<Card>` indirection is `CardMapState`.

Related docs:

- [Entity, Definition, And Instance Model](features/entities/models/01-definition-instance-model.md)
- [Zone Model](features/zones/models/01-zone-model.md)
- [ADR-0001 — Card shape](features/cards/decisions/0001-card-shape.md)
- [ADR-0005 — Objects over IDs](decisions/0005-objects-over-ids.md)
- [Zones ADR-0002 — Visibility model](features/zones/decisions/0002-visibility-model.md)

## Model Summary

A Card is a thin handle over the entity bridge:

> Card = `EntityInstance<CardDefinition>` + typed `Ref<Card> SelfRef` + state-side accessors for the public `CardRef` and `Owner`.

The handle itself is stateless beyond its `SelfRef`, `Definition`, and `Storage`. All mutable card data — variables, modifiers, current public ref, owner, visibility — lives in Store slices. Reads happen by passing state into accessor methods (`card.GetRef(state)`, `card.GetOwner(state)`).

The framework is **data-driven**: there is one `Card` class. Different card types (Fireball, Goblin, etc.) are different `CardDefinition` instances with their own data fields and effect recipes — not different C# classes. The `Card` handle reads typed definition data through the entity bridge.

## Card Class

```csharp
public sealed class Card : EntityInstance<CardDefinition>
{
    public Ref<Card> SelfRef { get; init; }

    public Card(CardDefinition def, IEntityVariableStorage storage) : base(def, storage) { }

    public CardRef GetRef(IStateScope state) =>
        state.Get<CardMapState>().RefOf(SelfRef);

    public Actor? GetOwner(IStateScope state) =>
        state.Get<OwnershipState>().OwnerOf(SelfRef);

    public override bool Equals(object? obj) =>
        obj is Card other && SelfRef.Equals(other.SelfRef);

    public override int GetHashCode() => SelfRef.GetHashCode();
}
```

Card variants (Fireball, Goblin, etc.) are not separate C# classes — they are distinct `CardDefinition` instances. A "Fireball" card is a `Card` handle whose definition is a `CardDefinition` configured with Fireball-specific data and effect recipes. Per the data-driven model, the framework provides one `Card` type; game content lives in definitions, not in subclasses.

The class is a handle, not a record. Equality is overridden to use `SelfRef` so the same card can be looked up reliably by Card object across handle re-creation (e.g., snapshot rehydration). There is at most one *live* `Card` instance per `Ref<Card>` in the running engine — held in `store.Catalog` (and typically in a sibling `CardRegistry` for ergonomic typed lookup).

**`CardDefinition : Scaffold.EntityDefinition, ISliceProvider`.** Adopts `ISliceProvider` from day one with a default empty `GetInitialSlices() => Array.Empty<BaseState>()`. Most card state is variables (in `EntityState` via the bridge); per-card non-variable slices are rare. Concrete `CardDefinition` subclasses override `GetInitialSlices()` only when they need additional per-card slices (e.g., a "spell-with-charges" record, an "attack history" log). The factory walks instances at spawn time and installs returned slices keyed at `card.SelfRef`. `Card` itself inherits `EntityInstance<TDef>`'s default `ISliceProvider` implementation, which delegates to the definition.

## CardRef

`CardRef` is the temporary reference used in client-visible state — zones, indices, anything that gets sent to or read by clients. It rotates on every move and on every visibility transition.

```csharp
public readonly record struct CardRef(Guid Value)
{
    public static CardRef NewRef() => new(Guid.NewGuid());
}
```

`CardRef` is **not** an `Identifier<T>`. Identifiers are stable references; `CardRef` is deliberately unstable. The two coexist:

- `Ref<Card>` — stable. Used for variable storage (slice key), modifier storage, ownership, visibility, history, all server-internal subscriptions, and as the `CardRegistry`/catalog key.
- `CardRef` — temporary. Used as the `CardMapState` key, in `ZoneContentsState`, in client-facing slices and projections.

Rotating `CardRef` is the framework's privacy mechanism. When a card hides or moves, the old ref is retired and a fresh one issued. Observers who held the old ref see it disappear; observers who never had data behind that ref see only the new ref appear. The underlying `Ref<Card>` and live `Card` handle are stable across rotations — only the public-facing `CardRef` flips.

## CardMapState

The privacy-rotation indirection layer mapping `CardRef → Ref<Card>` (and the parallel reverse index for O(1) `RefOf`). Lives in the Store, so snapshot/rollback works for free.

```csharp
public sealed record CardMapState : IState
{
    public ImmutableDictionary<CardRef, Ref<Card>> RefToInstance { get; init; }
        = ImmutableDictionary<CardRef, Ref<Card>>.Empty;

    public ImmutableDictionary<Ref<Card>, CardRef> InstanceToRef { get; init; }
        = ImmutableDictionary<Ref<Card>, CardRef>.Empty;

    public Ref<Card> InstanceOf(CardRef @ref) => RefToInstance[@ref];

    public CardRef RefOf(Ref<Card> selfRef) => InstanceToRef[selfRef];
}
```

The map holds typed refs only — *not* live `Card` handles. Live handles are held by `store.Catalog` (and a sibling `CardRegistry` keyed by `Ref<Card>`); resolution from a `CardRef` is two-hop:

```csharp
var instanceRef = state.Get<CardMapState>().InstanceOf(cardRef);
var card        = registry.Get(instanceRef);   // or store.Catalog.Resolve(instanceRef)
```

Both hops are O(1). Holding `Card` objects in the slice would entangle in-process handle identity with snapshotted state; the indirection keeps the slice serialization-clean.

Mutators that rotate `CardRef` write only `CardMapState` (swap the entry for the same `Ref<Card>`). The `Card` handle, the catalog entry, and every server-internal subscription keyed on `Ref<Card>` are untouched.

## CardRegistry

Engine-side service holding live `Card` handles by `Ref<Card>`. Side-state on the engine (not a slice) — populated by `Card.Spawn`, cleared on engine teardown.

```csharp
public sealed class CardRegistry
{
    private readonly Dictionary<Ref<Card>, Card> cards = new();

    public Card Get(Ref<Card> r) => cards[r];
    public bool TryGet(Ref<Card> r, out Card card) => cards.TryGetValue(r, out card!);
    public void Register(Card card) => cards[card.SelfRef] = card;
    public void Unregister(Ref<Card> r) => cards.Remove(r);
}
```

The registry duplicates `store.Catalog` content for `Ref<Card>` specifically; consumers can use either. The registry exists for typed ergonomics — `registry.Get(refOfCard)` returns `Card` directly, no cast — and to allow per-kind lifecycle hooks if a game wants them.

## OwnershipState

Generic across all catalog-registered handles. Both Cards and Zones read ownership through this slice.

```csharp
public sealed record OwnershipState : IState
{
    public ImmutableDictionary<Reference, Actor> Owners { get; init; }
        = ImmutableDictionary<Reference, Actor>.Empty;

    public Actor? OwnerOf(Reference selfRef) =>
        Owners.TryGetValue(selfRef, out var actor) ? actor : null;
}
```

Owner mutations go through the Store like any other state change — there is no direct `card.Owner = ...` mutation path. Cards / zones without an owner simply have no entry in the slice; `OwnerOf(...)` returns null.

## Card Visibility

Visibility uses a per-card runtime override on top of the zone's default policy (the policy itself lives on `ZoneDefinition` — see [Zones ADR-0002](features/zones/decisions/0002-visibility-model.md)). The override is a state slice keyed by `Ref<Card>`:

```csharp
public enum CardVisibility { Hidden, VisibleToOwner, VisibleToAll }

public sealed record CardVisibilityState : IState
{
    public ImmutableDictionary<Ref<Card>, CardVisibility> Overrides { get; init; }
        = ImmutableDictionary<Ref<Card>, CardVisibility>.Empty;

    public CardVisibility? OverrideFor(Ref<Card> r) =>
        Overrides.TryGetValue(r, out var v) ? v : null;
}
```

An absent entry on the slice means "use the zone's policy default." `VisibleToOwner` requires the card to have an Owner; collapses to `Hidden` if no Owner.

Rules read effective visibility through the card object: `card.IsVisibleTo(state, actor)` combines the zone policy and per-card override.

## Mutations

All card lifecycle operations are typed `: CompositeAction`. As CompositeActions, they pass through their own trigger fold — game effects can intercept create / move / set-visibility / destroy via before/after triggers (see the triggers feature). The ones that write the map require a fresh `CardRef` to be passed in via the payload — this ensures determinism across all sub-mutators within the composite and supports replay.

```csharp
public sealed record CreateCardMutation(
    CardDefinition Definition,
    Ref<Card> NewSelfRef,
    CardRef NewRef,
    Zone? IntoZone = null,
    InsertPosition? Position = null) : CompositeAction
{
    public static CreateCardMutation Of(Store store, CardDefinition definition)
        => new(definition, store.Catalog.AllocateRef<Card>(), CardRef.NewRef());

    public static CreateCardMutation Into(Store store, CardDefinition definition, Zone zone)
        => new(definition, store.Catalog.AllocateRef<Card>(), CardRef.NewRef(), zone);
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

`DestroyCardMutation` does not need a `NewRef` — it only removes the entry from `CardMapState`, the slice from the Store, and the catalog/registry entry. `CreateCardMutation` takes the `Definition` (not a pre-built `Card`) — the framework constructs the `Card` handle inside the composite via `Card.Spawn`, allocates the `Ref<Card>` (or honours one passed in via the payload for replay determinism), and registers in `CardMapState`/registry/catalog atomically.

For `BypassCapacity = true` (force-effects bypassing zone defaults), use a record `with` expression rather than a separate factory: `MoveCardMutation.AtTop(card, src, dst) with { BypassCapacity = true }`. Same pattern applies to `InsertCardPayload`.

`InsertPosition` is nullable on the `CreateCardMutation` payload; `null` means "use the destination zone's default insertion behavior." `MoveCardMutation` takes `InsertPosition` non-null because callers always pass an explicit position via the factories — game code that wants the destination's default uses `new DefaultPosition()` explicitly.

### Composite expansion (in prose, not normative API)

- **CreateCardMutation** — calls `Card.Spawn` (which wraps `StoreEntities.Spawn<CardDefinition>` and casts the ref to `Ref<Card>`); inserts `(NewRef, NewSelfRef)` into `CardMapState`; registers the handle in `CardRegistry`; initializes the card's `EntityState` slice from `Definition.BaseValues` (already empty after `Spawn` — definition defaults flow through fold-time fallback); if `IntoZone` is non-null, inserts `NewRef` into that zone's `ZoneContentsState` at `Position`.
- **MoveCardMutation** — removes `card.GetRef(state)` from `Source.ZoneContentsState`; rotates the `CardRef` entry in `CardMapState` (`oldRef → NewRef`, both pointing at the same `card.SelfRef`); inserts `NewRef` into `Destination.ZoneContentsState` at `Position`. **Precondition:** `Card` must currently be in `Source` (mutator throws if not — see `oq-move-validation-execution-drift` in `OPEN-ITEMS.md` for the deferred concurrency question). The catalog entry, registry entry, and `EntityState` slice are untouched.
- **SetCardVisibilityMutation** — rotates the `CardRef` entry in `CardMapState`; swaps `oldRef → NewRef` in any `ZoneContentsState` containing the card; writes `CardVisibilityState[card.SelfRef] = Visibility`.
- **DestroyCardMutation** — removes the entry from `CardMapState`; removes the card's `CardRef` from any `ZoneContentsState`; unregisters the `EntityState` slice keyed by `card.SelfRef`; clears the catalog entry and `CardRegistry` entry; fires the cleanup hook (per `oq-entity-destruction`, still open).

### Construction helper

For the common case of "create a card and use it immediately," a Store extension provides sugar:

```csharp
public static class StoreExtensions
{
    public static Card CreateCard(this Store store, CardDefinition definition, Zone? intoZone = null)
    {
        var selfRef = store.Catalog.AllocateRef<Card>();
        var newRef  = CardRef.NewRef();
        store.Execute(new CreateCardMutation(definition, selfRef, newRef, intoZone));
        return store.GetService<CardRegistry>().Get(selfRef);
    }
}

// Use:
var card = store.CreateCard(fireballDefinition, p1Hand);
store.Execute(MoveCardMutation.AtTop(card, p1Hand, battlefield));
```

## Read API

Reading a card from a `CardRef` (two-hop, both O(1)):

```csharp
var instanceRef = state.Get<CardMapState>().InstanceOf(someRef);
var card        = registry.Get(instanceRef);
```

Reading the cards in a zone — see `Zone.GetCards(state)` defined on the Zone class in [Zone Model § Zone Shape](features/zones/models/01-zone-model.md).

Reading a card's current public ref or owner:

```csharp
var ref   = card.GetRef(state);      // current CardRef
var owner = card.GetOwner(state);    // Actor or null
```

## Useful Invariants

- One `Card` object per `Ref<Card>` in the running engine — held in `store.Catalog` and `CardRegistry`.
- `Ref<Card>` is stable across the card's lifetime; `CardRef` rotates on every move and visibility transition.
- All mutable card state (variables, modifiers, current public ref, owner, visibility) lives in slices.
- Server-internal references (triggers, ongoing effects, attachments, history-side identity) use the `Card` object or `Ref<Card>` — never the rotating `CardRef`.
- Client-visible state and projections (zones, indices, history entries naming a current location like `ZoneContentInserted`) reference cards by `CardRef`.
- `CardRef` rotation is the framework's privacy mechanism; observers see refs change but never receive data they aren't allowed to see.