---
type: model
status: settled
last-updated: 2026-05-01
builds-on: root
tags: [#zones, #model]
---

# Zone Model

This document outlines the conceptual model for zones. The internal sub-questions left open in earlier drafts ("Open Questions Left For Later") were settled in session-20260501-1602; see ADR-0001 (zone shape), ADR-0002 (visibility), and ADR-0003 (insertion API) for the locked verdicts.

Related docs:

- [Core Glossary](ledgers/GLOSSARY.md)
- [Entity, Definition, And Instance Model](features/entities/models/01-definition-instance-model.md)
- [Card Model](features/cards/models/01-card-model.md)
- [ADR-0001 — Zone shape](features/zones/decisions/0001-zone-shape.md)
- [ADR-0002 — Visibility model](features/zones/decisions/0002-visibility-model.md)
- [ADR-0003 — Insertion API](features/zones/decisions/0003-insertion-api.md)
- [ADR-0005 — Objects over IDs](decisions/0005-objects-over-ids.md)

## Model Summary

A zone is a group of cards. It carries identity, a definition that describes how it organizes its contents, an optional owner, and the runtime data that backs its behavior (membership and any per-zone-type values like a counter or recipe progress).

The working model is:

> Zone = thin object (id + definition + owner) + contents slice + per-zone-type runtime slice.

Zones are groups of cards. They can represent structured game concepts like hands, decks, discard piles, battlefields, stacks, shops, and production slots. **Card packs are not zones** — a pack is an entity that, when activated, generates new card instances; it does not contain pre-existing cards. **Reward offers, drafts, and choice surfaces are not zones either** — those are decisions, modeled through the action/decision system.

Zones should not own broad gameplay legality. A larger game/action rules layer decides whether an actor may move an instance into, out of, or within a zone. Once a move is approved, the zone owns how its contents are arranged and what bookkeeping happens.

**Zones do not have stats.** A zone can carry runtime values (a counter, recipe progress) but does not participate in the entity-variable bridge or modifier-stack machinery — it has no `EntityVariableState`, no `IReadOnlyEntity` polymorphism, no modifiers. The moment a concept needs stats with modifier stacks, it is no longer a zone — it is a different entity that may have a zone associated with it.

## Zone Shape

A zone is a class implementing `IReference` with two structural fields:

```csharp
public sealed class Zone : IReference
{
    public InstanceId Id { get; }
    public ZoneDefinition Definition { get; }

    public Actor? GetOwner(IStateScope state) =>
        state.Get<OwnershipState>().OwnerOf(this);

    public IEnumerable<Card> GetCards(IStateScope state)
    {
        var contents = state.Get<ZoneContentsState>(this);
        var map = state.Get<CardMapState>();
        return contents.Cards.Select(map.GetCard);
    }
    // ... other state-reading methods
}
```

Zone is a class (handle), not a record. There is one `Zone` instance per `InstanceId` in the running engine, shared by all framework code that operates on that zone. Mutable zone data (contents, owner, per-zone-type values) lives in slices.

The zone shares the `Identifier<Instance>` namespace with cards (and any other `IReference`-implementing entity). This makes targeting work uniformly: an effect that picks "a card or a zone" takes `IReference` and handles both through the same ID space.

Zone runtime state lives across:

1. **`ZoneContentsState`** — generic, framework-owned. Keyed by the zone. Holds an ordered `ImmutableList<CardRef>` referencing cards via their current `CardRef`. Per the [Card Model](features/cards/models/01-card-model.md), `CardRef` rotates on every move — the contents list is updated atomically by the same mutators that rotate the ref. When a game needs richer per-entry structure (slot keys, group keys, spatial coordinates), it adds fields on its own design pass — the framework doesn't pre-define those hooks.
2. **`OwnershipState`** — generic, framework-owned, applicable to any `IReference` instance. `Dictionary<InstanceId, Actor>`. Both Cards and Zones read ownership through this slice.
3. **Per-zone-type runtime slices** — game-authored. Carry typed values for that zone type — e.g., `PileState { Counter }`, `RecipeStackState { Progress }`. Independent slices; no shared shape across zone types.

Zone configuration lives on the `ZoneDefinition`: capacity, ordering rules, insertion behavior, default visibility policy. Different zone types vary by definition data, not by class hierarchy.

See [ADR-0001](features/zones/decisions/0001-zone-shape.md) for full reasoning.

### Zone Definition

The authored base data for a kind of zone.

Examples:

- Player hand zone definition.
- Draw pile zone definition.
- Discard pile zone definition.
- MTG stack zone definition.
- Stacklands physical stack zone definition.
- Shop row zone definition.

### Zone Object (Runtime)

The runtime zone in a session — a `Zone` value carrying its `Id`, `Definition`, and `Owner`.

Examples:

- Player 1 hand.
- Player 2 hand.
- Current encounter draw pile.
- Shared battlefield.
- Active rules stack.
- This specific physical stack on a board.

## Core Responsibilities

Zones should be described by responsibilities, not a fixed trait checklist.


| Responsibility     | Meaning                                                                                            |
| ------------------ | -------------------------------------------------------------------------------------------------- |
| Contents           | Which instances are in the zone.                                                                   |
| Arrangement        | How contents are ordered, grouped, stacked, slotted, or otherwise addressed.                       |
| Capacity / Size    | Default size limits, count limits, slot limits, or similar organizational constraints.             |
| Insertion Behavior | Where an approved incoming instance goes: top, bottom, next slot, chosen index, grouped pile, etc. |
| Removal Behavior   | How contents close gaps, reorder, unstack, or update after an instance leaves.                     |
| Visibility         | What each actor/viewer can know about membership and details.                                      |
| Change Bookkeeping | What state changes or history facts are emitted when contents change.                              |


This avoids forcing all zones into a universal boolean trait list while keeping legality outside the zone.

## Legality Boundary

The zone should not answer broad questions like:

- Can this card type go here?
- Is this actor allowed to move this card here?
- Can this card be played from this source zone?
- Does this effect ignore the normal maximum hand size?
- Is this target valid?

Those are game/action rules questions.

The zone may answer narrower organization questions like:

- How many instances are currently here?
- What is the default capacity?
- Where should an approved instance be inserted?
- If this zone is full, what is the default organizational result?
- Can the requested index/slot exist?
- What changed when an instance was added or removed?

Capacity should be treated as an overridable/default organizational constraint, not an absolute law. A game rule or effect may force a move, ignore a limit, replace a limit, or choose a special insertion mode.

This means a zone can provide a default "this is full" answer, but that answer is data for the action rules to interpret. It should not prevent a higher-priority game effect from force-pushing an instance into the zone.

## Contents And Arrangement

A zone must be able to answer what it contains.

Arrangement can vary by game:

- Ordered list.
- Unordered set.
- Indexed slots.
- Top/bottom pile.
- Stack order.
- Grouped piles.
- Hidden sequence.
- Game-specific structure.

Spatial coordinates are not required by the core zone model. If a game needs spatial placement as rules data, that should be added by a spatial preset or game-specific model. If spatial placement is only visual, it belongs to presentation/layout.

## Stack Zones

Stacks should be modeled as zones with custom organization.

### MTG-Style Rules Stack

An MTG-style stack zone may define:

- Ordered contents.
- Top/bottom insertion behavior.
- How contents are addressed for later resolution.
- Who may see stack contents.

Priority, legal actions, and resolution are scheduler/action rules. The stack zone can organize pending spell/ability instances, but it should not own the full priority system.

### Stacklands-Style Physical Stack

A Stacklands-style physical stack zone may define:

- Which instances are stacked together.
- Which instance is top/bottom.
- How new approved instances are inserted.
- How instances are removed or unstacked.

Recipe matching, timers, production, automatic combat, and work are game systems that may observe or query the stack zone. The visual pile can be handled by presentation, but the fact that those instances are grouped together belongs to simulation.

## Read API

Per [ADR-0005](decisions/0005-objects-over-ids.md), framework APIs prefer the zone object over its ID. The zone is used directly as a slice key, and utility methods on the zone take state per call. There is no `Zones` injected service.

```csharp
// slice access (zone is the key)
state.Get<ZoneContentsState>(zone);
state.Get<PileState>(zone);              // game-authored per-zone-type slice

// utility methods on the zone (sugar for slice reads)
zone.GetCards(state);                    // resolves CardRefs through CardMapState
zone.Count(state);
zone.IsFull(state);                      // reads definition.Capacity vs current count
zone.GetOwner(state);                    // reads OwnershipState
```

The `Zone` object is stateless beyond its identity — it does not hold a scope reference. Methods accept state per call, so a zone reference is timeless and can be passed across scope boundaries safely.

For reverse queries — "what zone is this card in?" — the framework maintains a derived `CardLocationIndex` slice keyed by `CardRef → Zone` (per the [Card Model](features/cards/models/01-card-model.md)). The index is updated atomically by the same mutators that write `ZoneContentsState`.

## Insertion API

A single payload carries zone insertion, with optional override fields for game effects that need to bypass zone defaults. See [ADR-0003](features/zones/decisions/0003-insertion-api.md) for full reasoning.

```csharp
record InsertCardPayload(
  Zone Zone,
  Card Card,
  InsertPosition Position = default,    // DefaultPosition uses zone definition
  bool BypassCapacity = false);
```

`InsertPosition` is a sealed record hierarchy — each variant is its own type, so cases-with-data and cases-without-data don't share a single enum:

```csharp
abstract record InsertPosition;
sealed record DefaultPosition  : InsertPosition;
sealed record AtTop            : InsertPosition;
sealed record AtBottom         : InsertPosition;
sealed record AtIndex(int Idx) : InsertPosition;
```

Static factories provide call-site readability:

- `InsertCardPayload.AtTop(zone, card)`
- `InsertCardPayload.AtBottom(zone, card)`
- `InsertCardPayload.AtIndex(zone, card, i)`
- `InsertCardPayload.Default(zone, card)` — explicit "use zone default"
- `InsertCardPayload.Force(...)` — variant that sets `BypassCapacity = true`

The composite `MoveCardMutation` (remove + insert as one logical move, defined in the [Card Model](features/cards/models/01-card-model.md)) carries the same `InsertPosition` and `BypassCapacity` and forwards them to the insert side. It also carries a fresh `CardRef` — every move rotates the ref per the conservative privacy policy.

"Forced" at the mutation level means bypassing zone defaults. The separate axis — "this insertion can't be cancelled by replacement effects" — lives at the `GameAction` / trigger-fold layer, not here.

## Visibility Model

Visibility uses two channels:


| Channel               | Question It Answers                                                                | Example                                                                        |
| --------------------- | ---------------------------------------------------------------------------------- | ------------------------------------------------------------------------------ |
| Membership Visibility | Can this actor know which instance IDs are in this zone and how they are arranged? | A player sees the exact cards in their hand; an opponent may only see a count. |
| Detail Visibility     | Given a known instance ID, can this actor see its definition and variable data?    | A face-down card may be visible as an instance but hide its definition.        |


### Zone visibility policy (definition-only)

The zone's default visibility policy lives on the `ZoneDefinition` as authored data. The policy expresses per-actor-role rules across the two channels — e.g., "visible to owner, count-only to others" (hand), "hidden to all" (deck contents), "visible to all" (battlefield). The policy is static per zone definition; runtime variation comes only from the per-card flag.

### Per-card visibility flag

Per-card runtime overrides live on `CardVisibilityState` keyed by `InstanceId`:

```csharp
public enum CardVisibility { Hidden, VisibleToOwner, VisibleToAll }
```

An absent entry on the slice means "use the zone's policy default." `VisibleToOwner` requires the card to have an Owner; collapses to `Hidden` if no Owner.

### Rules-side read

Rules read visibility through the card object:

```csharp
card.IsVisibleTo(state, actor)
```

Visibility-conditional rules and triggers branch on this read. The predicate combines the zone's policy and the per-card flag.

### Server / client model

The server is omniscient and stores canonical state. Visibility is applied as a **send-time filter** that **cuts data**, never alters identity. The server only sends to a client what that actor is allowed to see:

- **Full data** — actor can see the card; client renders normally.
- **CardRef only** — actor knows a card exists at this `CardRef` but not what it is; client renders a hidden-card placeholder.
- **Nothing** — actor doesn't know the card exists (e.g., opponent's deck contents); client only knows the count if the zone policy reveals it.

The same filter runs locally and over the network — there is no per-actor ID divergence. All observers see the same canonical `CardRef` for a card; the only difference is whether they receive the card's data behind that ref.

### Re-hide via server-side CardRef rotation

Per the [Card Model](features/cards/models/01-card-model.md), every card carries two IDs:

- `InstanceId` — stable across the card's lifetime; used for variable storage, modifier storage, history, server-internal subscriptions.
- `CardRef` — temporary; the slice key in `CardMapState` and the reference used in `ZoneContentsState`. Rotates on every move and on every visibility transition.

When a card hides, `HideCardMutation { Card, NewRef }` rotates the `CardRef` server-side: `CardMapState` swaps the old ref out and the new ref in, every `ZoneContentsState` containing the old ref swaps it for the new ref, `CardLocationIndex` rebinds, and `CardVisibilityState[card.InstanceId]` updates. All observers — local and networked alike — see the new canonical ref. An observer who previously held the old ref sees it disappear; they cannot correlate the new ref with the old.

The simulation-side `InstanceId` stays stable forever (preserves the entities Model invariant). Triggers, effects, attachments, and history all reference the `Card` object or its `InstanceId` — they are unaffected by ref rotation.

See [ADR-0002](features/zones/decisions/0002-visibility-model.md) for full reasoning.

## History Filtering

The server records a single canonical `HistoryEnvelope` stream with full `InstanceId`s and full data. Per-actor history views are produced at the transport boundary by applying detail gating:

- **CardRef-bearing entries are passed through** — entries that reference cards by `CardRef` (e.g., `ZoneContentInserted(zone, ref)`, `CardRefRotated(oldRef, newRef)`) are sent to all observers. Since all observers see the same canonical `CardRef`, no translation is needed; the ref shown matches what the actor sees in live state.
- **Detail gating** — card-detail entries (`VariableChanged<T>`, modifier changes) are sent only to actors who have detail visibility on the affected card. **Movement entries** and **ref rotations** are always sent — the *fact* of movement and rotation is public.

For genuinely-secret movements (where even the fact must be hidden from observers), the game models the interaction differently — the framework does not provide a "secret history" mode.

See [ADR-0002](features/zones/decisions/0002-visibility-model.md) §History.

## Zone Movement

Zone movement is a simulation-driven game action.

The zone model should not assume presentation can directly move cards. A drag/drop interaction can request movement, but the simulation decides whether it is allowed.

Expected flow:

```mermaid
sequenceDiagram
  participant View as Presentation / Interaction
  participant Sim as Simulation
  participant Rules as Game / Action Rules
  participant Zone as Zone Model
  participant Hist as History

  View->>Sim: PlayerIntent move instance to zone
  Sim->>Rules: Validate movement intent
  Rules-->>Sim: Approved or rejected
  Sim->>Zone: Apply approved removal/insertion
  Zone-->>Sim: Organization result
  Sim->>Hist: Record movement result
```



The exact names for request, command, task, event, and history are still pending. The important rule is that movement is approved by simulation/game rules, while zones organize approved content changes.

## What Zones Should Not Own Yet

Zones should not own these concerns by default:

- Visual layout.
- Screen-space or world-space coordinates.
- Pointer interactions.
- Animation.
- Broad gameplay legality.
- Card type legality.
- Actor permission rules.
- Generated reward choices.
- Draft choice semantics.
- Networking transport.
- Final actor/player ownership rules.

Some game-specific zone types may incorporate spatial or reward behavior later, but the base zone model should stay focused on contents, organization, visibility, capacity defaults, and change bookkeeping.

## Zone Examples


| Zone           | Contents                         | Organization                                         | Visibility Direction                               | External Game Systems                             |
| -------------- | -------------------------------- | ---------------------------------------------------- | -------------------------------------------------- | ------------------------------------------------- |
| Hand           | Card instances                   | Ordered or player-arranged list; optional size limit | Owner sees details; others may see count only      | Playability, discard rules, maximum hand effects. |
| Deck           | Card instances                   | Ordered hidden pile                                  | Owner/opponent may see count but not order/details | Draw, shuffle, search, mill.                      |
| Discard        | Card instances                   | Ordered or unordered pile                            | Usually public details                             | Search, recycle, graveyard effects.               |
| Battlefield    | Card/unit instances              | Slots, rows, groups, or unordered presence           | Usually public details                             | Combat, presence, auras, targeting.               |
| Rules Stack    | Pending spell/ability instances  | Ordered stack, usually top-insertion                 | Usually public details                             | Priority, legal additions, resolution.            |
| Physical Stack | Card/resource instances          | Stack order/group                                    | Depends on game                                    | Recipes, timers, work, combat.                    |
| Shop Row       | Offer instances                  | Indexed list                                         | Usually public details                             | Purchase rules, refresh rules, pricing.           |


Note: card packs are *not* zones. A pack is an entity with a generator effect; opening it executes an effect that creates new card instances and places them somewhere. Reward offers, drafts, and choice surfaces are also not zones — they flow through the action/decision system.

## Deferred

- **Spatial rule attachment** — when games (e.g., Stacklands-style) need spatial coordinates as rules data rather than as presentation layout. The framework does not pre-define a hook for this; a spatial-rules game's design pass adds the appropriate fields to the contents shape (or a parallel slice) when the requirements are concrete. See `bl-spatial-rule-attachment` in `BACKLOG.md`.
- **Slot / group structure on contents** — battlefields with fixed rows or grouped piles. Same logic as spatial: no pre-defined hook; games add the fields when forced. The current contents shape is a flat `IReadOnlyList<Card>`.
- **Per-actor asymmetric reveals** — visibility states like "card visible to player A but not player B" are not first-class in the visibility model; games that need them model the reveal as a transient game-state slice. Per-actor map shape on the visibility flag was rejected as YAGNI.

## Useful Invariants

- Zones are thin handles implementing `IReference`; their state lives in slices.
- Zones share the `Identifier<Instance>` namespace with cards.
- Zones do not have stats. Stat-bearing things are not zones.
- Zones own contents, organization, capacity defaults, and change bookkeeping.
- Zones do not own broad gameplay legality.
- Zone membership references cards by their current `CardRef`; refs rotate on every move (per the Card Model).
- Stack concepts can be modeled as zones with custom organization.
- Spatial placement is not required in the base zone model.
- Visibility uses membership and detail channels; rules read it via `card.IsVisibleTo(state, actor)`.
- Server is canonical; visibility filtering at the transport boundary cuts data, never alters identity. Privacy across reveal-then-hide cycles is achieved by server-side `CardRef` rotation that all observers see.
- Movement between zones is approved by game/action rules and organized by zones.
- Card packs and reward offers are not zones.


---

# Provenance: Zone Model Decisions (from doc 11)

Atomic decisions captured below also become ADRs in `features/zones/decisions/` per phase 5 of the migration.

# Zone Model Decisions

This document answers the Zones questions from [Open Questions](ledgers/OPEN-QUESTIONS.md). These are early research decisions, not final implementation contracts.

## Status

The direction for zones is now:

- Zones should be richer models, not just a list of boolean traits.
- Spatial placement is not a core zone concern for now.
- MTG-style stacks and Stacklands-style stacks can both be modeled as zones with custom organization.
- Zone visibility should be configurable per actor/player, but actor/player ownership concepts still need their own pass.
- Zone movement is a normal simulation-driven game action, not a special out-of-band primitive.
- Temporary choices, generated offers, drafts, and rewards are mostly action/decision-model questions and stay out of scope for this batch.

## Zone Shape

Question: what zone traits are mandatory in core?

Decision: avoid designing zones as a trait checklist for now. A zone should be a richer model with contents, organization behavior, capacity/size behavior, visibility, and change bookkeeping.

Reasoning:

A trait-only model risks becoming a pile of booleans like `ordered`, `hidden`, `stackable`, `owned`, `public`, `capacityLimited`, and so on. That can describe simple zones, but it may become awkward for zones like:

- A Magic stack.
- A Stacklands physical stack.
- A reward offer.
- A shop row.
- A production slot.
- A card pack.
- A shared battlefield with slots.

Implication:

The zone model should talk about responsibilities and policies rather than fixed traits. A zone can still expose configuration, but the base framework should not assume every zone is assembled from the same fixed trait set.

Confidence: medium.

Follow-up:

Define the minimum zone model as a set of responsibilities:

- What instances are in the zone.
- How those instances are ordered or grouped.
- How new instances are inserted or removed after the simulation has approved a change.
- Whether the zone has default capacity/size behavior, and how that behavior can be overridden by game effects.
- What change notifications or history facts are emitted when contents change.
- What data is visible to which actors.

Do not put game legality in the zone. Questions like "can this card type go here?" or "is this actor allowed to move this card here?" belong to the game/action rules layer.

## Spatial Placement

Question: how should spatial board positions relate to zone membership?

Decision: spatial placement is outside the core zone model for now.

Zones are primarily simulation logic. Spatial placement should be:

- View-defined, when it is only presentation layout.
- Game-specific, when physical position is part of the rules.
- A later spatial-system concern, not a mandatory zone concern.

Reasoning:

The framework should not force every zone to understand world positions, UI layout, or 3D card placement. Most zones only need simulation membership and ordering. Stacklands-like games may need spatial rules, but that should be modeled deliberately by a spatial preset or game-specific zone type.

Implication:

The core zone model may support a generic position/order value, but not require spatial coordinates.

Confidence: high.

Follow-up:

When we design presentation/interaction, define how views map zone contents to UI/2D/3D layouts without changing zone semantics.

## Stacks

Question: are stacks zones, relationships, or both?

Decision: model stacks as zones with custom organization.

This applies to both:

- MTG-style rules stack.
- Stacklands-style physical card stack.

If multiple cards are in the same stack zone, the relationship is already represented by zone membership. The zone owns the organization that makes those members behave like a stack.

Reasoning:

Treating stacks as zones keeps the model simple:

- The stack contains instances.
- The stack can define ordering.
- The stack can define insertion and removal behavior.
- The stack can define size/capacity behavior, if relevant.
- The stack can expose visibility differently per actor.

Relationships may still exist for convenience or derived views, but the primary model should be zone membership.

Implication:

Do not create a separate universal "stack relationship" concept yet. Start with stack zones and allow game rules or preset systems to decide what actions a stack enables.

Confidence: medium-high.

Follow-up:

The Rules/Scheduler docs should later distinguish a rules stack zone from an effect scheduler data structure.

## Visibility

Question: how should hidden, partially visible, and viewer-specific state be modeled?

Decision: zones should support actor-specific visibility configuration, but actor/player/ownership semantics are still unresolved.

Early visibility model should assume at least two channels:

1. **Membership visibility**: whether an actor can know which instance IDs are in the zone, and in what order/grouping.
2. **Detail visibility**: whether an actor can resolve the definition and variable data behind a visible instance ID.

Examples:

- A player may know their hand contains instance IDs A, B, C and see all details.
- An opponent may know the hand has three cards but not know the instance IDs or definitions.
- A deck may reveal count but hide order and details.
- A discard pile may reveal membership, order, and full details.
- A face-down card zone may reveal an instance exists but hide definition and variables.

Reasoning:

This separates "something is there" from "I know what it is." That separation will matter for presentation, networking, replay, AI, hidden information, and spectator/debug tools.

Implication:

Do not model visibility as a single `hidden` boolean on a zone. Visibility should eventually be queryable per actor and per data channel.

Confidence: medium.

Follow-up:

Define actors, players, ownership, controller, and viewers before finalizing visibility APIs.

## Zone Movement

Question: should zone movement be a generic event, a task, or a specialized rules primitive?

Decision: zone movement is a normal simulation-driven game action.

Moving a card, changing a card, using a card, drawing a card, discarding a card, or placing a card into a stack all change game state. They should flow through the same general action pipeline once that pipeline is defined.

Expected shape:

1. Presentation or a system creates an intent/request.
2. Game/action rules validate whether that movement or action is allowed.
3. Simulation applies the approved state change.
4. The destination/source zones organize their contents after the change.
5. History/events record the result.
6. Triggers/reactions may respond.

Reasoning:

Zone movement should not be a direct view operation or a special shortcut. Dragging a card might request movement, but the simulation approves and performs the actual move. The zone can say how to place the instance once the move is approved, but it should not be the authority for whether the move is legal.

Implication:

Do not define final `Event`, `Task`, or `Action` semantics in the zone docs. The zone docs should only state that movement is simulation driven and uses the same action flow as other game changes.

Confidence: high.

Follow-up:

The next major research batch should define action/request/decision/task/event/history terminology and flow.

## Temporary Choices And Generated Views

Question: how do we model temporary views like reward offers, shops, card packs, draft picks, and generated choices?

Decision: out of scope for the zone system for now.

Generated choices are a bigger action/decision question. Some of these may eventually be zones, but we should not force that answer yet.

Examples:

- A reward offer might be a temporary zone.
- A draft pick might be a decision with options, not a zone.
- A card pack might be a zone, an effect, or a generated content source.
- A shop row might be a zone plus economic rules.

Reasoning:

Trying to classify every temporary view as a zone now would mix zone modeling with action generation, decisions, rewards, economy, and presentation.

Implication:

Leave these cases open until the action/decision model exists.

Confidence: high.

Follow-up:

Revisit temporary/generated zones after command, decision, option, and action flow docs are written.

## Decisions To Carry Forward

- Zones are richer organization models, not just trait bags.
- Spatial placement is not a core zone requirement.
- Stacks are zones with custom organization.
- Visibility needs at least membership and detail channels.
- Zone visibility should be actor-specific later.
- Zone movement is simulation driven and approved through normal action flow.
- Game/action rules own legality; zones own contents, ordering, capacity defaults, and change bookkeeping.
- Generated choices are deferred to the action/decision model.

