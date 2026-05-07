---
type: model
status: settled
references: [features/entities/models/01-definition-instance-model.md, features/zones/models/01-zone-model.md]
tags: [#cards, #model]
---

# Card Model

## What it is

A Card is the framework's representation of a card-shaped game entity. The `Card` handle is a thin class extending the entity bridge with state-side accessors:

> Card = `StateEntity<CardDefinition>` + `IReference` + state-side accessors for `CardRef` and `Owner`.

The handle itself is stateless beyond its `InstanceId` and `Definition` reference. All mutable card data — variables, modifiers, current ref, owner, visibility — lives in Store slices. Reads happen by passing state into accessor methods (`card.GetRef(state)`, `card.GetOwner(state)`, `card.IsVisibleTo(state, actor)`).

The framework is **data-driven**: there is one `Card` class. Different card types (Fireball, Goblin, etc.) are distinct `CardDefinition` instances with their own data fields and effect recipes — not different C# classes. The handle reads typed definition data through the entity bridge.

## Contracts

### Card class

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

The class is a handle, not a record. Equality is overridden to use `InstanceId` so the same card looks up reliably across handle re-creation (snapshot rehydration) and so `Card`-keyed dictionaries (e.g. `CardMapState.RefByCard`) work correctly. There is at most one *live* `Card` instance per `InstanceId` in a running engine — referenced from `CardMapState` and shared by all framework code that operates on that card.

Card variants are not separate C# classes — they are distinct `CardDefinition` instances. A "Fireball" card is a `Card` handle whose definition is configured with Fireball-specific data and effect recipes. Per the data-driven model, the framework provides one `Card` type; game content lives in definitions, not in subclasses.

### `CardRef`

```csharp
public readonly record struct CardRef(Guid Value)
{
    public static CardRef NewRef() => new(Guid.NewGuid());
}
```

`CardRef` is the temporary reference used in client-visible state — zones, indices, anything sent to or read by clients. It rotates on every move and on every visibility transition. It is **not** an `Identifier<T>`; identifiers are stable, `CardRef` is deliberately unstable.

The two coexist:
- `InstanceId` — stable. Used for variable storage, modifier storage, ownership, visibility, history, all server-internal subscriptions.
- `CardRef` — temporary. Used as the `CardMapState` key, in `ZoneContentsState`, in client-facing slices and projections.

Rotating `CardRef` is the framework's privacy mechanism. When a card hides or moves, the old ref is retired and a fresh one issued. Observers who held the old ref see it disappear; observers who never had data behind that ref see only the new ref appear.

### `CardMapState`

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

The single canonical slice mapping `CardRef` to `Card` instances. Replaces what would otherwise be an engine-side handle registry — putting it on the Store gives snapshot/rollback for free. Lookups are one-hop. The map holds `Card` objects directly (not `InstanceId` values); the Card object's lifetime is owned by the map.

The parallel `RefByCard` index makes `RefOf(card)` O(1) — important because `card.GetRef(state)` is called inside every mutation that rotates the ref. The index uses `Card`'s `InstanceId`-based equality so it remains correct across handle re-creation. Mutators that write `CardMapState` update both dictionaries atomically.

### `OwnershipState`

```csharp
public sealed record OwnershipState : IState
{
    public ImmutableDictionary<InstanceId, Actor> Owners { get; init; }
        = ImmutableDictionary<InstanceId, Actor>.Empty;

    public Actor? OwnerOf(IReference @ref) =>
        Owners.TryGetValue(@ref.Id, out var actor) ? actor : null;
}
```

Generic across all `IReference` instances. Both Cards and Zones read ownership through this slice. Owner mutations go through the Store like any other state change — there is no direct `card.Owner = ...` mutation path. Cards / zones without an owner simply have no entry in the slice; `OwnerOf(...)` returns null.

### `CardVisibilityState`

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

Per-card runtime override on top of the zone's default policy (the policy itself lives on `ZoneDefinition` — see [`decisions/0006-zone-visibility-model.md`](decisions/0006-zone-visibility-model.md)). An absent entry on the slice means "use the zone's policy default." `VisibleToOwner` requires the card to have an `Owner`; collapses to `Hidden` if no `Owner`.

Rules read effective visibility through the card object: `card.IsVisibleTo(state, actor)` combines the zone policy and per-card override.

### Mutations

All card lifecycle operations are typed `: CompositeAction`. As `CompositeAction`s, they pass through their own trigger fold — game effects can intercept create / move / set-visibility / destroy via before/after triggers (see `triggers/`). The ones that write `CardMapState` require a fresh `CardRef` to be passed in via the payload — this ensures determinism across all sub-mutators within the composite and supports replay.

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

`InsertPosition` is nullable on the `CreateCardMutation` payload (`null` = "use the destination zone's default insertion behavior"); non-null on `MoveCardMutation` because callers always pass an explicit position via the factories — game code that wants the destination's default uses `new DefaultPosition()` explicitly.

#### Composite expansion (in prose, not normative API)

- **`CreateCardMutation`** — constructs the `Card` handle from `(NewInstanceId, Definition, store)`; inserts `(NewRef, Card)` into `CardMapState` (and updates `RefByCard`); initializes the card's `EntityVariableState` slice from `Definition.BaseValues`; if `IntoZone` is non-null, inserts `NewRef` into that zone's `ZoneContentsState` at `Position`.
- **`MoveCardMutation`** — removes `card.GetRef(state)` from `Source.ZoneContentsState`; rotates the entry in `CardMapState` (`oldRef → NewRef`); inserts `NewRef` into `Destination.ZoneContentsState` at `Position`. **Precondition:** `Card` must currently be in `Source` (mutator throws if not — see `oq-move-validation-execution-drift` in `OPEN-ITEMS.md` for the deferred concurrency question).
- **`SetCardVisibilityMutation`** — rotates the entry in `CardMapState`; swaps `oldRef → NewRef` in any `ZoneContentsState` containing the card; writes `CardVisibilityState[card.InstanceId] = Visibility`.
- **`DestroyCardMutation`** — removes the entry from `CardMapState`; removes the card from any `ZoneContentsState`; removes per-card slices keyed by `InstanceId` (variables, modifiers, visibility); fires the cleanup hook (per `oq-entity-destruction`, still open).

### Read API

```csharp
// Read a card from a CardRef:
var card = state.Get<CardMapState>().GetCard(someRef);

// Read a card's current ref or owner:
var ref = card.GetRef(state);      // current CardRef
var owner = card.GetOwner(state);  // Actor or null
```

Reading the cards in a zone — see `Zone.GetCards(state)` defined on the Zone class in [Zone Model § Zone Shape](features/zones/models/01-zone-model.md).

## Invariants

- **One live `Card` per `InstanceId`.** The `CardMapState` owns the handle's lifetime; references are shared, not copied. Equality is `InstanceId`-based.

(See `cards.md ## Invariants` for feature-level rules — two-ID model, server-internal vs client-visible split, all-state-in-slices, ref rotation as privacy mechanism.)

## Examples

```csharp
// Create a card and play it from hand to battlefield.
var card = store.CreateCard(fireballDefinition, p1Hand);
store.Execute(MoveCardMutation.AtTop(card, p1Hand, battlefield));

// Hide a card to its owner only (rotates the CardRef).
store.Execute(SetCardVisibilityMutation.ShowToOwner(card));

// Read a card's current ref and owner.
var currentRef = card.GetRef(state);
var owner = card.GetOwner(state);
```

The `Store.CreateCard` extension is the convenience entry point for the common "create a card and use it immediately" case:

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
```

## Open follow-ups

- `oq-move-validation-execution-drift` — concurrency between move-validate and move-execute (precondition: card must be in `Source` when `MoveCardMutation` runs).
- `oq-entity-destruction` — `Cleanup` hook for destroyed cards (per-card slice cleanup on `DestroyCardMutation`).

---

*Below: provenance — what was considered, why we chose this, what it constrains. Originally landed as `features/cards/decisions/0001-card-shape.md` (2026-05-01); folded into Model provenance per §4.5 on 2026-05-07 (single-feature scope — decision is about Card shape; entities and zones provide abstractions this consumes, not co-decisions).*

The zone-model design pass surfaced that the framework had no explicit answer to "where does a card live as data?" The entities Model implied cards exist as `StateEntity<TDef>` handles, but those handles were anchored in an engine-side `InstanceId → (definition, store)` registry — engine-state, not Store-state. That registry has to be re-built on `LoadSnapshot` and lives outside the snapshot/rollback system. A second concern surfaced from the visibility design ([`decisions/0006-zone-visibility-model.md`](decisions/0006-zone-visibility-model.md)): client visibility is best supported by the framework giving cards a temporary reference (`CardRef`) that can rotate when the card hides or moves.

## Options considered

### Q1 — canonical home of card existence

- **A — Engine-side handle registry only.** Rejected — not on state, no snapshot/rollback support, contradicts the Store-as-canonical pattern.
- **B — Map slice with `CardRef → InstanceId`; reconstitute Card via the entity bridge.** Possible, but adds a lookup hop on every card access. The bridge already constructs handles — the slice can just hold the constructed Card.
- **C — Map slice with `CardRef → Card` directly.** Card is a class (reference type); the map holds and owns the handle's lifetime; one-hop lookup. **Chosen.**

### Q2 — dual-ID model (`InstanceId` + `CardRef`)

- **A — Single stable `InstanceId` everywhere; transport-time alias for visibility** (option (b) from `decisions/0006-zone-visibility-model.md`, original). Rejected: requires per-actor transport-side machinery that differs from local execution; "play the same locally vs networked" principle pushes against this.
- **B — Single ephemeral ID, no stable identity.** Rejected: history, replay, and server-internal subscriptions need a stable cross-time identity.
- **C — Two IDs: stable `InstanceId` (server-internal, history, serialization) + ephemeral `CardRef` (client-visible state, rotates on hide/move).** **Chosen.**

### Q3 — relationship between Card and the entity bridge

- **A — Card is a separate type, not on the bridge.** Rejected: the bridge handles variables, modifiers, definition typing — duplicating that for cards is throw-away work.
- **B — Card extends `StateEntity<CardDefinition>`.** Card is a non-generic handle on the bridge with additional state-reading methods (`GetRef`, `GetOwner`). **Chosen** — see Q4.

### Q4 — Card type's generic parameter

- **A — Generic `Card<TDef>` with per-card-type subclasses** (`Fireball : Card<FireballDefinition>`). Rejected: pushes the framework toward a class-per-card-type model.
- **B — Non-generic `Card : StateEntity<CardDefinition>`; per-card variants are different `CardDefinition` instances, not different C# classes.** **Chosen** — aligns with the framework's data-driven design (game content lives in definitions, not subclasses).

## Rationale

`Card` is a non-generic class extending `StateEntity<CardDefinition>` and implementing `IReference`. It is a thin handle — no mutable data on the class itself. All mutable card state lives in Store slices. Per-card-type variants are distinct `CardDefinition` instances; the framework provides one `Card` class.

`CardMapState` is the canonical "this card exists" slice, mapping `CardRef → Card` plus the parallel reverse index. Holds `Card` objects directly (one live handle per `InstanceId`). Replaces the engine-side handle registry — putting the registration on the Store gives snapshot/rollback for free.

`CardRef` is a temporary reference that rotates on every move (per [`features/zones/models/01-zone-model.md` § Insertion API](features/zones/models/01-zone-model.md) and the move mutation contract) and on every visibility transition (per [`decisions/0006-zone-visibility-model.md`](decisions/0006-zone-visibility-model.md)). It is **not** an `Identifier<T>` — it is deliberately unstable.

Two IDs coexist: `InstanceId` (stable, used for variable storage, modifier storage, ownership, visibility, history, all server-internal subscriptions) and `CardRef` (temporary, used as `CardMapState` key, in `ZoneContentsState`, in client-visible projections).

Mutations that write `CardMapState` carry the new `CardRef` as a payload field. This ensures all sub-mutators within the composite see the same value and supports deterministic replay. `SetCardVisibilityMutation` collapses what would otherwise be separate `Hide`/`Show` mutations into one operation parameterized by target visibility.

## Consequences

- **Enables:** cards have a clear canonical home on the Store (snapshot/rollback works); the framework supports privacy across reveal-then-hide cycles via server-side ref rotation; "play the same locally vs networked" — the same `CardRef` rotation runs in both modes; server-internal subscriptions reference Cards by `Card` object or `InstanceId` and are unaffected by ref rotation.
- **Constrains:** any state slice or projection that is sent to clients must reference cards by `CardRef`, not by `InstanceId` (otherwise the privacy filter doesn't work). Server-internal state must reference cards by `Card` or `InstanceId`, not by `CardRef` (otherwise rotations cascade through subscribers).
- **Reverts/replaces:** [`decisions/0006-zone-visibility-model.md`](decisions/0006-zone-visibility-model.md) original "per-actor transport alias" mechanism is replaced by server-side `CardRef` rotation. The zones ADR has been updated to reflect this.
- **Cascade:** existing references to card identity in `effects/`, `engine/`, `actions/`, `presentation/` need a sweep — server-internal vs client-visible distinction. Surfaced on demand by `/builder:lint` (per §5.7).
