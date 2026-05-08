---
type: adr
status: accepted
tags: [#adr, #cards, #model]
---

# ADR-0001 — Card shape: EntityInstance handle + CardMapState + CardRef rotation

Date: 2026-05-01 (amended 2026-05-06 to reflect Plan A/B/C cascade)
Context-doc: features/cards/models/01-card-model.md
Proposal: session-20260501-1602-zone-model-internal-opens

## Amendment — 2026-05-06 (Plan A/B/C cascade)

Original ADR text named the bridge handle `StateEntity<CardDefinition>` and the empty marker `IReference`; both have been replaced. The bridge handle is now `EntityInstance<TDef>` (identity-free at the base, with concrete subclasses carrying typed `Ref<TSubclass> SelfRef`); the polymorphic addressable type is `Reference` (an abstract record). `CardMapState` is the privacy-rotation indirection layer mapping `CardRef → Ref<Card>`; live `Card` handles are held in `store.Catalog` and `CardRegistry`. The decision and consequences below have been rewritten to reflect this; the original options-considered analysis still stands.

## Amendment — 2026-05-06 (open-seams pass)

Two further refinements landed the same day:

1. **Framework parent class layering.** `Card` now extends `InstanceWithSlices<CardDefinition>` rather than `Scaffold.EntityInstance<CardDefinition>` directly. `InstanceWithSlices<TDef>` is the framework parent that consolidates the `ISliceProvider` contract on the Instance side and exposes `Definition` as `protected`. The discussion below referencing `EntityInstance<CardDefinition>` should be read as `InstanceWithSlices<CardDefinition>` for new code; behaviorally equivalent.
2. **`CardRegistry` removed.** `store.Catalog` is now the single registry for stable handle resolution. Resolution from a public `CardRef` is two-hop: `CardRef → CardMapState → Ref<Card> → Catalog → Card`. The Card Model's "Resolution surfaces" section pins the mechanism.
3. **`Card` is unsealed.** Project convention (open-seams Q4): default to unsealed unless there's a strong reason to seal. Game code may subclass for typed re-export of authored Definition fields.

## Context

The zone-model design pass surfaced that the framework had no explicit answer to "where does a card live as data?" The entities Model implied cards exist as bridge-handle instances, but those handles were anchored in an engine-side `InstanceId → (definition, store)` registry — engine-state, not Store-state. That registry has to be re-built on `LoadSnapshot` and lives outside the snapshot/rollback system.

The design-pass conclusion: card existence should be reflected on snapshotted state. Their handles are class instances (per the existing entities Model), but the canonical "this card exists" record needs to be on snapshotted state.

A second concern surfaced from the visibility design (Zones ADR-0002): client visibility is best supported by the framework giving cards a temporary reference (`CardRef`) that can rotate when the card hides or moves. The simulation-side identity (typed `Ref<Card>`) stays stable; the `CardRef` is the wire-form reference that changes.

## Options considered

For the canonical home of card existence:

- **A — Engine-side handle registry only.** Reject — not on state, no snapshot/rollback support, contradicts the Store-as-canonical pattern.
- **B — Map slice with `CardRef → Ref<Card>`; live handle held in catalog/registry.** Two-hop lookup, but the slice stays serialization-clean (no in-process object references). Chosen.
- **C — Map slice holding `Card` objects directly.** Couples slice serialization to in-process handle lifetime; rejected.

For the dual-ID model (stable identity + ephemeral `CardRef`):

- **A — Single stable identity everywhere; transport-time alias for visibility** (option (b) from Zones ADR-0002, original). Rejected: requires per-actor transport-side machinery that differs from local execution; "play the same locally vs networked" principle pushes against this.
- **B — Single ephemeral ID, no stable identity.** Rejected: history, replay, and server-internal subscriptions need a stable cross-time identity.
- **C — Two refs: stable `Ref<Card>` (server-internal, history, serialization, slice keys) + ephemeral `CardRef` (client-visible state, rotates on hide/move).** Chosen.

For the relationship between Card and the entity bridge:

- **A — Card is a separate type, not on the bridge.** Rejected: the bridge handles variables, modifiers, definition typing — duplicating that for cards is throw-away work.
- **B — Card extends `EntityInstance<CardDefinition>`.** Card is a non-generic handle on the bridge with additional state-reading methods (`GetRef`, `GetOwner`). Chosen.

For the Card type's generic parameter:

- **A — Generic `Card<TDef>` with per-card-type subclasses** (`Fireball : Card<FireballDefinition>`). Rejected: pushes the framework toward a class-per-card-type model.
- **B — Non-generic `Card : EntityInstance<CardDefinition>`; per-card variants are different `CardDefinition` instances, not different C# classes.** Chosen — aligns with the framework's data-driven design (game content lives in definitions, not subclasses).

## Decision

**Card is a non-generic class extending `EntityInstance<CardDefinition>` and carrying typed `Ref<Card> SelfRef`.** It is a thin handle — no mutable data on the class itself. All mutable card state (variables, modifiers, current public ref, owner, visibility) lives in Store slices. Per-card-type variants (Fireball, Goblin, etc.) are distinct `CardDefinition` instances; the framework provides one `Card` class.

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

Equality is overridden to use `SelfRef` so the same card can be looked up reliably across handle re-creation (snapshot rehydration, etc.).

**`CardMapState` is the privacy-rotation indirection layer.** Maps `CardRef → Ref<Card>` and the parallel reverse index `Ref<Card> → CardRef`. Holds typed refs only — never live `Card` handles.

```csharp
public sealed record CardMapState : IState
{
    public ImmutableDictionary<CardRef, Ref<Card>> RefToInstance { get; init; }
        = ImmutableDictionary<CardRef, Ref<Card>>.Empty;

    public ImmutableDictionary<Ref<Card>, CardRef> InstanceToRef { get; init; }
        = ImmutableDictionary<Ref<Card>, CardRef>.Empty;
}
```

Live `Card` handles are held in `store.Catalog` (generic, by `Ref<Card>`) and a sibling `CardRegistry` (typed ergonomics). The reverse index in `CardMapState` makes `RefOf(selfRef)` O(1); `Resolve(cardRef)` is two hops, both O(1): slice → `Ref<Card>` → `Card`. Mutators write both dictionaries in `CardMapState` atomically.

**`CardRef` is a temporary reference that rotates** on every move (per Zones ADR-0003) and on every visibility transition (per Zones ADR-0002). It is **not** an `Identifier<T>` — it is deliberately unstable.

```csharp
public readonly record struct CardRef(Guid Value)
{
    public static CardRef NewRef() => new(Guid.NewGuid());
}
```

**Two refs coexist:**
- `Ref<Card>` — stable across the card's lifetime. Used for variable storage (slice key), modifier storage, ownership, visibility, history, all server-internal subscriptions, and as the catalog/registry key. Allocated by `store.Catalog.AllocateRef<Card>()`.
- `CardRef` — temporary. Used as the `CardMapState` key, in `ZoneContentsState`, in client-visible projections.

**Ownership lives on a generic `OwnershipState` slice** keyed by `Reference` and applicable to any catalog-registered handle (cards, zones, players, future addressable entities). Owner reads go through `card.GetOwner(state)` or `zone.GetOwner(state)`.

**`CardVisibilityState` slice** holds per-card visibility overrides as `CardVisibility` enum (`Hidden | VisibleToOwner | VisibleToAll`) keyed by `Ref<Card>`; absent entry = use zone policy. See [Card Model § Card Visibility](features/cards/models/01-card-model.md).

**Card construction** is owned by the framework. Game code provides a `CardDefinition`; `CreateCardMutation(Definition, NewSelfRef, NewRef, IntoZone?)` constructs the `Card` handle via `Card.Spawn` (which wraps `StoreEntities.Spawn<CardDefinition>` and casts the ref to `Ref<Card>`) inside the composite. Game code never calls `new Card(...)` directly.

**Mutations** that write `CardMapState` (`Create`, `Move`, `SetCardVisibility`) carry the new `CardRef` as a payload field. This ensures all sub-mutators within the composite see the same value and supports deterministic replay — the recorded payload in history carries the ref directly. `SetCardVisibilityMutation` collapses what would otherwise be separate `Hide` and `Show` mutations into one operation parameterized by target visibility (with `Hide`/`ShowAll`/`ShowToOwner` factories).

## Consequences

- **Enables:** cards have a clear canonical home — `EntityState` slice keyed by `Ref<Card>`, plus `CardMapState` for the privacy layer (both snapshotted, both rollback for free); the framework supports privacy across reveal-then-hide cycles via server-side ref rotation; "play the same locally vs networked" — the same `CardRef` rotation runs in both modes; server-internal subscriptions (triggers, effects, attachments, history) reference Cards by `Card` object or `Ref<Card>` and are unaffected by ref rotation.
- **Constrains:** any state slice or projection that is sent to clients must reference cards by `CardRef`, not by `Ref<Card>` (otherwise the privacy filter doesn't work). Server-internal state must reference cards by `Card` or `Ref<Card>`, not by `CardRef` (otherwise rotations cascade through subscribers).
- **Reverts:** Zones ADR-0002 (visibility model) original "per-actor transport alias" mechanism is replaced by server-side `CardRef` rotation. ADR-0002 has been updated to reflect this.
- **Audit:** Existing references to card identity in `effects/`, `engine/`, `actions/`, `presentation/` need a sweep — server-internal vs client-visible distinction. Tracked in alignment-debt entries (`ad-20260501-040` through `ad-20260501-047`).
