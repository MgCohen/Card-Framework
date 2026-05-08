---
type: model
status: settled
references: [features/entities/models/01-definition-instance-model.md, features/entities/models/02-runtime-surface.md, features/zones/models/01-zone-model.md]
tags: [#cards, #model]
---

# Card Model — model

## What it is

A Card is the framework's representation of a card-shaped game entity. The `Card` handle is a thin class extending the entity bridge with state-side accessors:

> Card = `InstanceWithSlices<CardDefinition>` + typed `Ref<Card> SelfRef` + state-side accessors for the public `CardRef` and `Owner`.

The handle itself is stateless beyond `SelfRef`, protected `Definition`, and `Storage`. All mutable card data — variables, modifiers, current public ref, owner, visibility — lives in Store slices. Reads happen by passing state into accessor methods (`card.GetRef(state)`, `card.GetOwner(state)`).

The framework is **data-driven**: there is one `Card` class. Different card types (Fireball, Goblin, etc.) are distinct `CardDefinition` instances with their own data fields and effect recipes — not different C# classes. The handle reads typed definition data through the entity bridge.

## Contracts

### Card class

```csharp
public class Card : InstanceWithSlices<CardDefinition>
{
    public Ref<Card> SelfRef { get; init; }

    public Card(CardDefinition def, IEntityVariableStorage storage) : base(def, storage) { }

    public CardRef GetRef(IStateScope state) =>
        state.Get<CardMapState>().RefOf(SelfRef);

    public IActor? GetOwner(IStateScope state) =>
        state.Get<OwnershipState>().OwnerOf(SelfRef);

    public override bool Equals(object? obj) =>
        obj is Card other && SelfRef.Equals(other.SelfRef);

    public override int GetHashCode() => SelfRef.GetHashCode();
}
```

`Card` is **not sealed** (project convention: default to unsealed). Game code may subclass for typed re-export of authored Definition fields or for game-side helper methods, but **per-card-type subclassing** (`FireballCard : Card`) is discouraged — it breaks the data-driven principle. Variants belong in `CardDefinition` subclasses, not in `Card` subclasses.

The class is a handle, not a record. Equality is overridden to use `SelfRef` so the same card can be looked up reliably across handle re-creation (snapshot rehydration). There is at most one *live* `Card` instance per `Ref<Card>` in the running engine — held in `store.Catalog`.

**`CardDefinition : DefinitionWithSlices`.** Inherits the framework parent class (`DefinitionWithSlices : Scaffold.EntityDefinition, ISliceProvider`); picks up a default empty `GetInitialSlices() => Array.Empty<BaseState>()`. Most card state is variables (in `EntityState` via the bridge); per-card non-variable slices are rare. Concrete `CardDefinition` subclasses override `GetInitialSlices()` only when they need additional per-card slices (e.g., a "spell-with-charges" record, an "attack history" log). The factory walks instances at spawn time and installs returned slices keyed at `card.SelfRef`. `Card` itself inherits `InstanceWithSlices<TDef>`'s default `ISliceProvider` implementation, which delegates to the (protected) definition.

### `CardRef`

```csharp
public readonly record struct CardRef(Guid Value)
{
    public static CardRef NewRef() => new(Guid.NewGuid());
}
```

`CardRef` is the temporary reference used in client-visible state — zones, indices, anything sent to or read by clients. It rotates on every move and on every visibility transition. It is **not** an `Identifier<T>`.

The two refs coexist:
- `Ref<Card>` — stable. Used for variable storage (slice key), modifier storage, ownership, visibility, history, all server-internal subscriptions, and as the catalog key.
- `CardRef` — temporary. Used as the `CardMapState` key, in `ZoneContentsState`, in client-facing slices and projections.

Rotating `CardRef` is the framework's privacy mechanism. When a card hides or moves, the old ref is retired and a fresh one issued. Observers who held the old ref see it disappear; observers who never had data behind that ref see only the new ref appear. The underlying `Ref<Card>` and live `Card` handle are stable across rotations — only the public-facing `CardRef` flips.

### `CardMapState`

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

The map holds typed refs only — *not* live `Card` handles. Live handles are held by `store.Catalog`; resolution from a `CardRef` is two-hop, both O(1). Holding `Card` objects in the slice would entangle in-process handle identity with snapshotted state; the indirection keeps the slice serialization-clean.

Mutators that rotate `CardRef` write only `CardMapState` (swap the entry for the same `Ref<Card>`). The `Card` handle, the catalog entry, and every server-internal subscription keyed on `Ref<Card>` are untouched.

### Resolution surfaces

There is one registry for stable handle resolution — `store.Catalog` — keyed by `Ref<T>`. There is no separate `CardRegistry`; per-kind ergonomics flow through type inference on `Catalog.Resolve(Ref<T>)`.

| Caller has… | Resolution call |
|---|---|
| `Card` directly | (no resolution) |
| `Ref<Card>` (server-internal) | `store.Catalog.Resolve(stableRef)` |
| `CardRef` (from zone contents, history entry, network message) | two-hop via `CardMapState` + `Catalog` |

The two-hop appears only in the third row. A small helper bundles it (its exact location — extension method on `Store`, instance method on `CardRef`, or shared utility — is non-structural and tracked as `bl-card-resolution-helper-shape` in `OPEN-ITEMS.md`):

```csharp
// Conceptual; final shape is an implementation detail.
public static Card ResolveCard(this Store store, CardRef @ref)
{
    var stableRef = store.Get<CardMapState>().InstanceOf(@ref);
    return store.Catalog.Resolve(stableRef);
}
```

Server-internal code that holds `Card` or `Ref<Card>` directly never touches this helper — it goes straight through `store.Catalog.Resolve(stableRef)`. Only code reading client-visible state (zone contents, history entries naming `CardRef`s) hits the helper.

**Snapshot/restore.** `CardMapState` is a slice and serializes with the rest of state. `store.Catalog` is in-process side-state and is rebuilt deterministically on `LoadSnapshot` — the engine walks restored `EntityState` slices and reconstitutes `Card` handles, registering each at its `Ref<Card>`. `CardMapState` entries (already restored from snapshot) remain valid as-is; two-hop resolution works immediately after rehydration.

### `OwnershipState`

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

### Card visibility — `CardVisibilityState`

Visibility uses a per-card runtime override on top of the zone's default policy (the policy itself lives on `ZoneDefinition` — see [`Product/decisions/0008-zone-visibility-model.md`](Product/decisions/0008-zone-visibility-model.md)). The override is a state slice keyed by `Ref<Card>`:

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

### Mutations

All card lifecycle operations are typed `: CompositeAction`. As `CompositeAction`s, they pass through their own trigger fold — game effects can intercept create / move / set-visibility / destroy via before/after triggers (see `triggers/`). The ones that write `CardMapState` require a fresh `CardRef` to be passed in via the payload — this ensures determinism across all sub-mutators within the composite and supports replay.

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

`DestroyCardMutation` does not need a `NewRef` — it only removes the entry from `CardMapState`, the `EntityState` slice, and the catalog entry. `CreateCardMutation` takes the `Definition` (not a pre-built `Card`) — the framework constructs the `Card` handle inside the composite via `Card.Spawn` (which wraps `StoreEntities.Spawn<CardDefinition>` and casts the ref to `Ref<Card>`), allocates the `Ref<Card>` (or honours one passed in via the payload for replay determinism), and registers in `CardMapState` / catalog atomically.

For `BypassCapacity = true` (force-effects bypassing zone defaults), use a record `with` expression rather than a separate factory: `MoveCardMutation.AtTop(card, src, dst) with { BypassCapacity = true }`. Same pattern applies to `InsertCardPayload`.

`InsertPosition` is nullable on the `CreateCardMutation` payload (`null` = "use the destination zone's default insertion behavior"); non-null on `MoveCardMutation` because callers always pass an explicit position via the factories — game code that wants the destination's default uses `new DefaultPosition()` explicitly.

#### Composite expansion (in prose, not normative API)

- **`CreateCardMutation`** — calls `Card.Spawn`; inserts `(NewRef, NewSelfRef)` into `CardMapState` (and the reverse index); registers the handle in `store.Catalog` at the new `Ref<Card>`; initializes the card's `EntityState` slice from `Definition.BaseValues` (already empty after `Spawn` — definition defaults flow through fold-time fallback); if `IntoZone` is non-null, inserts `NewRef` into that zone's `ZoneContentsState` at `Position`.
- **`MoveCardMutation`** — removes `card.GetRef(state)` from `Source.ZoneContentsState`; rotates the `CardRef` entry in `CardMapState` (`oldRef → NewRef`, both pointing at the same `card.SelfRef`); inserts `NewRef` into `Destination.ZoneContentsState` at `Position`. **Precondition:** `Card` must currently be in `Source` (mutator throws if not — see `oq-move-validation-execution-drift` in `OPEN-ITEMS.md` for the deferred concurrency question). The catalog entry and `EntityState` slice are untouched.
- **`SetCardVisibilityMutation`** — rotates the `CardRef` entry in `CardMapState`; swaps `oldRef → NewRef` in any `ZoneContentsState` containing the card; writes `CardVisibilityState[card.SelfRef] = Visibility`.
- **`DestroyCardMutation`** — removes the entry from `CardMapState`; removes the card's `CardRef` from any `ZoneContentsState`; unregisters the `EntityState` slice keyed by `card.SelfRef`; clears the catalog entry; fires the cleanup hook (per `oq-entity-destruction`, still open).

## Invariants

Model-local invariants. (See `cards.md ## Invariants` for feature-level rules — two-ID model, server-internal vs client-visible split, all-state-in-slices, ref rotation as privacy mechanism, one live Card per Ref<Card>.)

- **Equality is `SelfRef`-based.** `Card`-keyed dictionaries (e.g., `CardMapState.InstanceToRef`) work correctly across handle re-creation.
- **`CardMapState` holds typed refs only** — never live `Card` handles. The two-hop indirection keeps the slice serialization-clean.
- **Mutators that rotate `CardRef` write only `CardMapState`.** The `Card` handle, the catalog entry, and every server-internal subscription keyed on `Ref<Card>` are untouched by rotation.

## Decisions

Linked: [`Product/decisions/0008-zone-visibility-model.md`](Product/decisions/0008-zone-visibility-model.md) — Visibility model: definition policy + per-card flag + server-side `CardRef` rotation. Cross-cutting (zones + cards + presentation). Shapes the `CardRef` rotation mechanism this Model implements and the `CardVisibilityState` per-card override slice.

## Examples

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
        return store.Catalog.Resolve(selfRef);
    }
}

// Use:
var card = store.CreateCard(fireballDefinition, p1Hand);
store.Execute(MoveCardMutation.AtTop(card, p1Hand, battlefield));
```

### Reads

Reading a card from a `CardRef` (two-hop, both O(1)):

```csharp
var instanceRef = state.Get<CardMapState>().InstanceOf(someRef);
var card        = store.Catalog.Resolve(instanceRef);
```

Reading a card's current public ref or owner:

```csharp
var ref   = card.GetRef(state);      // current CardRef
var owner = card.GetOwner(state);    // Actor or null
```

Reading the cards in a zone — see `Zone.GetCards(state)` defined on the Zone class in [Zone Model § Zone Shape](features/zones/models/01-zone-model.md).

### Hide a card to its owner only

```csharp
store.Execute(SetCardVisibilityMutation.ShowToOwner(card));
// CardRef has now rotated; clients that previously held the old ref
// see it disappear in their next state delta.
```

## Open follow-ups

- `oq-move-validation-execution-drift` — concurrency between move-validate and move-execute (precondition: card must be in `Source` when `MoveCardMutation` runs).
- `oq-entity-destruction` — `Cleanup` hook for destroyed cards (per-card slice cleanup on `DestroyCardMutation`).
- `bl-card-resolution-helper-shape` — final form for the `CardRef → Card` two-hop resolution helper (extension method on `Store`, instance method on `CardRef`, or shared utility).

---

*Below: provenance — feature-internal decisions folded on 2026-05-08 from `features/cards/decisions/0001-card-shape.md` (originally landed 2026-05-01, amended 2026-05-06 to reflect the Plan A/B/C cascade and again 2026-05-06 for the open-seams pass). Single-feature scope (no project-wide ADR applies; folded into Model provenance per §4.5). The amendments updated the bridge handle name (`StateEntity<CardDefinition>` → `EntityInstance<CardDefinition>` → `InstanceWithSlices<CardDefinition>`), removed `CardRegistry` in favor of `store.Catalog`, and unsealed `Card`; the Q1–Q4 below carry the original options-considered analysis with notes on how the chosen answers evolved.*

## Options considered

### Q1 — canonical home of card existence

- **A — Engine-side handle registry only.** Rejected — not on state, no snapshot/rollback support, contradicts the Store-as-canonical pattern.
- **B — Map slice with `CardRef → Ref<Card>`; live handle held in `store.Catalog`.** Two-hop lookup, but the slice stays serialization-clean (no in-process object references). **Chosen.**
- **C — Map slice holding `Card` objects directly.** Couples slice serialization to in-process handle lifetime; rejected.

The original ADR named the live-handle registry `CardRegistry`; the open-seams pass collapsed it into the generic `store.Catalog` (single registry keyed by `Ref<T>`).

### Q2 — Dual-ID model (stable identity + ephemeral `CardRef`)

- **A — Single stable identity everywhere; transport-time alias for visibility** (option from the original Zones visibility framing). Rejected — requires per-actor transport-side machinery that differs from local execution; "play the same locally vs networked" principle pushes against this.
- **B — Single ephemeral ID, no stable identity.** Rejected — history, replay, and server-internal subscriptions need a stable cross-time identity.
- **C — Two refs: stable `Ref<Card>` (server-internal, history, serialization, slice keys) + ephemeral `CardRef` (client-visible state, rotates on hide/move).** **Chosen.**

The Plan A/B/C cascade renamed the stable identity from `InstanceId` to typed `Ref<Card>`; semantics unchanged.

### Q3 — Relationship between Card and the entity bridge

- **A — Card is a separate type, not on the bridge.** Rejected — the bridge handles variables, modifiers, definition typing; duplicating that for cards is throw-away work.
- **B — Card extends the bridge handle.** Card is a handle on the bridge with additional state-reading methods (`GetRef`, `GetOwner`). **Chosen** — see Q4.

The bridge handle name evolved across the design passes: original ADR named it `StateEntity<CardDefinition>`; the Plan A/B/C cascade renamed it to `EntityInstance<CardDefinition>`; the open-seams pass introduced the framework parent `InstanceWithSlices<CardDefinition>` (consolidates `ISliceProvider` on the Instance side and exposes `Definition` as `protected`). `Card` extends `InstanceWithSlices<CardDefinition>` in the current shape.

### Q4 — Card type's generic parameter

- **A — Generic `Card<TDef>` with per-card-type subclasses** (`Fireball : Card<FireballDefinition>`). Rejected — pushes the framework toward a class-per-card-type model.
- **B — Non-generic `Card : InstanceWithSlices<CardDefinition>`; per-card variants are different `CardDefinition` instances, not different C# classes.** **Chosen** — aligns with the framework's data-driven design (game content lives in definitions, not subclasses).

The open-seams pass also unsealed `Card` (project convention: default to unsealed). Game code may subclass for typed re-export but per-card-type subclassing is discouraged — variants belong in `CardDefinition` subclasses.

## Rationale

`Card` is a non-generic class extending `InstanceWithSlices<CardDefinition>` and carrying typed `Ref<Card> SelfRef`. It is a thin handle — no mutable data on the class itself. All mutable card state lives in Store slices. Per-card-type variants are distinct `CardDefinition` instances; the framework provides one `Card` class.

`CardMapState` is the privacy-rotation indirection layer mapping `CardRef → Ref<Card>` (and the parallel reverse index). Holds typed refs only; live `Card` handles are held in `store.Catalog`. Resolution from a `CardRef` is two-hop (both O(1)); server-internal code holding `Card` or `Ref<Card>` directly never touches the indirection.

`CardRef` is a temporary reference that rotates on every move (per zones) and on every visibility transition (per the visibility decision). It is **not** an `Identifier<T>` — it is deliberately unstable.

Two refs coexist: `Ref<Card>` (stable, used for variable storage, modifier storage, ownership, visibility, history, all server-internal subscriptions, and as the catalog key) and `CardRef` (temporary, used as `CardMapState` key, in `ZoneContentsState`, in client-visible projections).

Mutations that write `CardMapState` carry the new `CardRef` as a payload field. This ensures all sub-mutators within the composite see the same value and supports deterministic replay. `SetCardVisibilityMutation` collapses what would otherwise be separate `Hide`/`Show` mutations into one operation parameterized by target visibility.

## Consequences

- **Enables:** cards have a clear canonical home — `EntityState` slice keyed by `Ref<Card>`, plus `CardMapState` for the privacy layer (both snapshotted, both rollback for free); the framework supports privacy across reveal-then-hide cycles via server-side ref rotation; "play the same locally vs networked" — the same `CardRef` rotation runs in both modes; server-internal subscriptions reference Cards by `Card` object or `Ref<Card>` and are unaffected by ref rotation.
- **Constrains:** any state slice or projection that is sent to clients must reference cards by `CardRef`, not by `Ref<Card>` (otherwise the privacy filter doesn't work). Server-internal state must reference cards by `Card` or `Ref<Card>`, not by `CardRef` (otherwise rotations cascade through subscribers).
- **Reverts/replaces:** the original Zones visibility model's "per-actor transport alias" mechanism is replaced by server-side `CardRef` rotation. The visibility ADR ([`Product/decisions/0008-zone-visibility-model.md`](Product/decisions/0008-zone-visibility-model.md), elevated from `features/zones/decisions/0002-visibility-model.md` on 2026-05-08) reflects this.
- **Cascade:** existing references to card identity in `effects/`, `engine/`, `actions/`, `presentation/` need a sweep — server-internal vs client-visible distinction. Surfaced on demand by `/builder:lint` (per §5.7).
