---
type: adr
status: accepted
last-updated: 2026-05-01
tags: [#adr, #cards, #model]
---

# ADR-0001 — Card shape: StateEntity handle + CardMapState + CardRef rotation

Date: 2026-05-01
Context-doc: features/cards/models/01-card-model.md
Proposal: session-20260501-1602-zone-model-internal-opens

## Context

The zone-model design pass surfaced that the framework had no explicit answer to "where does a card live as data?" The entities Model implied cards exist as `StateEntity<TDef>` handles, but those handles were anchored in an engine-side `InstanceId → (definition, store)` registry — engine-state, not Store-state. That registry has to be re-built on `LoadSnapshot` and lives outside the snapshot/rollback system.

The design-pass conclusion: cards should live on a Store slice, like everything else. Their handles are class instances (per the existing entities Model), but the canonical "this card exists" record needs to be on snapshotted state.

A second concern surfaced from the visibility design (Zones ADR-0002): client visibility is best supported by the framework giving cards a temporary reference (`CardRef`) that can rotate when the card hides or moves. The simulation-side `InstanceId` stays stable; the `CardRef` is the wire-form reference that changes.

## Options considered

For the canonical home of card existence:

- **A — Engine-side handle registry only.** Reject — not on state, no snapshot/rollback support, contradicts the Store-as-canonical pattern.
- **B — Map slice with `CardRef → InstanceId`; reconstitute Card via the entity bridge.** Possible, but adds a lookup hop on every card access. The bridge already constructs handles — the slice can just hold the constructed Card.
- **C — Map slice with `CardRef → Card` directly.** Card is a class (reference type); the map holds and owns the handle's lifetime; one-hop lookup. Chosen.

For the dual-ID model (`InstanceId` + `CardRef`):

- **A — Single stable `InstanceId` everywhere; transport-time alias for visibility** (option (b) from Zones ADR-0002, original). Rejected: requires per-actor transport-side machinery that differs from local execution; "play the same locally vs networked" principle pushes against this.
- **B — Single ephemeral ID, no stable identity.** Rejected: history, replay, and server-internal subscriptions need a stable cross-time identity.
- **C — Two IDs: stable `InstanceId` (server-internal, history, serialization) + ephemeral `CardRef` (client-visible state, rotates on hide/move).** Chosen.

For the relationship between Card and the entity bridge:

- **A — Card is a separate type, not on the bridge.** Rejected: the bridge handles variables, modifiers, definition typing — duplicating that for cards is throw-away work.
- **B — Card extends `StateEntity<CardDefinition>`.** Card is a non-generic handle on the bridge with additional state-reading methods (`GetRef`, `GetOwner`). Chosen — see "Card is non-generic" below.

For the Card type's generic parameter:

- **A — Generic `Card<TDef>` with per-card-type subclasses** (`Fireball : Card<FireballDefinition>`). Rejected: pushes the framework toward a class-per-card-type model.
- **B — Non-generic `Card : StateEntity<CardDefinition>`; per-card variants are different `CardDefinition` instances, not different C# classes.** Chosen — aligns with the framework's data-driven design (game content lives in definitions, not subclasses).

## Decision

**Card is a non-generic class extending `StateEntity<CardDefinition>` and implementing `IReference`.** It is a thin handle — no mutable data on the class itself. All mutable card state (variables, modifiers, current ref, owner, visibility) lives in Store slices. Per-card-type variants (Fireball, Goblin, etc.) are distinct `CardDefinition` instances; the framework provides one `Card` class.

```csharp
public sealed class Card : StateEntity<CardDefinition>, IReference
{
    public InstanceId Id => InstanceId;

    public CardRef GetRef(IStateScope state) =>
        state.Get<CardMapState>().RefOf(this);

    public Actor? GetOwner(IStateScope state) =>
        state.Get<OwnershipState>().OwnerOf(this);

    public override bool Equals(object? obj) =>
        obj is Card other && InstanceId.Equals(other.InstanceId);

    public override int GetHashCode() => InstanceId.GetHashCode();
}
```

Equality is overridden to use `InstanceId` so the same card can be looked up reliably across handle re-creation (snapshot rehydration, etc.) and so `Card`-keyed dictionaries (`CardMapState.RefByCard`) work correctly.

**`CardMapState` is the canonical "this card exists" slice.** Maps `CardRef → Card` and the parallel reverse index `Card → CardRef`. Holds `Card` objects directly (one live Card handle per `InstanceId`). Replaces the engine-side handle registry — putting the registration on the Store gives snapshot/rollback for free.

```csharp
public sealed record CardMapState : IState
{
    public ImmutableDictionary<CardRef, Card> Cards { get; init; }
        = ImmutableDictionary<CardRef, Card>.Empty;

    public ImmutableDictionary<Card, CardRef> RefByCard { get; init; }
        = ImmutableDictionary<Card, CardRef>.Empty;
}
```

The reverse index makes `RefOf(card)` O(1) — important because `card.GetRef(state)` is read inside every mutation that rotates the ref. Mutators write both dictionaries atomically.

**`CardRef` is a temporary reference that rotates** on every move (per Zones ADR-0003) and on every visibility transition (per Zones ADR-0002). It is **not** an `Identifier<T>` — it is deliberately unstable.

```csharp
public readonly record struct CardRef(Guid Value)
{
    public static CardRef NewRef() => new(Guid.NewGuid());
}
```

**Two IDs coexist:**
- `InstanceId` — stable across the card's lifetime. Used for variable storage, modifier storage, ownership, visibility, history, all server-internal subscriptions. `InstanceId.NewId()` factory generates fresh values.
- `CardRef` — temporary. Used as the `CardMapState` key, in `ZoneContentsState`, in client-visible projections.

**Ownership lives on a generic `OwnershipState` slice** keyed by `InstanceId` and applicable to any `IReference` instance (cards, zones, future addressable entities). Owner reads go through `card.GetOwner(state)` or `zone.GetOwner(state)`.

**`CardVisibilityState` slice** holds per-card visibility overrides as `CardVisibility` enum (`Hidden | VisibleToOwner | VisibleToAll`); absent entry = use zone policy. See [Card Model § Card Visibility](features/cards/models/01-card-model.md).

**Card construction** is owned by the framework. Game code provides a `CardDefinition`; `CreateCardMutation(Definition, NewInstanceId, NewRef, IntoZone?)` constructs the `Card` handle inside the composite (where the Store reference is available). Game code never calls `new Card(...)` directly.

**Mutations** that write `CardMapState` (`Create`, `Move`, `SetCardVisibility`) carry the new `CardRef` as a payload field. This ensures all sub-mutators within the composite see the same value and supports deterministic replay — the recorded payload in history carries the ref directly. `SetCardVisibilityMutation` collapses what would otherwise be separate `Hide` and `Show` mutations into one operation parameterized by target visibility (with `Hide`/`ShowAll`/`ShowToOwner` factories).

## Consequences

- **Enables:** cards have a clear canonical home on the Store (snapshot/rollback works); the framework supports privacy across reveal-then-hide cycles via server-side ref rotation; "play the same locally vs networked" — the same `CardRef` rotation runs in both modes; server-internal subscriptions (triggers, effects, attachments, history) reference Cards by `Card` object or `InstanceId` and are unaffected by ref rotation.
- **Constrains:** any state slice or projection that is sent to clients must reference cards by `CardRef`, not by `InstanceId` (otherwise the privacy filter doesn't work). Server-internal state must reference cards by `Card` or `InstanceId`, not by `CardRef` (otherwise rotations cascade through subscribers).
- **Reverts:** Zones ADR-0002 (visibility model) original "per-actor transport alias" mechanism is replaced by server-side `CardRef` rotation. ADR-0002 has been updated to reflect this.
- **Audit:** Existing references to card identity in `effects/`, `engine/`, `actions/`, `presentation/` need a sweep — server-internal vs client-visible distinction. Tracked in alignment-debt entries (`ad-20260501-040` through `ad-20260501-047`).
