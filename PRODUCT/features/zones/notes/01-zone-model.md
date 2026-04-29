---
type: model
status: settled
last-updated: 2026-04-29
builds-on: root
tags: [#zones, #model]
---

# Zone Model

This document outlines the conceptual model for zones after the decisions in [Zone Model Decisions](features/zones/notes/01-zone-model.md).

Related docs:

- [Core Glossary](general/GLOSSARY.md)
- [Entity, Definition, And Instance Model](features/entities/notes/01-definition-instance-model.md)
- [Zone Model Decisions](features/zones/notes/01-zone-model.md)

## Model Summary

A zone is a simulation model that contains or groups instances and knows how to organize those contents.

The working model is:

> Zone = definition + zone instance + contents + organization behavior + visibility + change bookkeeping.

Zones are not only containers. They can represent structured game concepts like hands, decks, discard piles, battlefields, stacks, shops, card packs, production slots, and temporary areas.

Zones should not own broad gameplay legality. A larger game/action rules layer should decide whether an actor may move an instance into, out of, or within a zone. Once a move is approved, the zone owns how its contents are arranged and what bookkeeping happens.

## Zone Definitions And Zone Instances

Like other entities, zones should have definitions and instances.

### Zone Definition

The authored base data for a kind of zone.

Examples:

- Player hand zone definition.
- Draw pile zone definition.
- Discard pile zone definition.
- MTG stack zone definition.
- Stacklands physical stack zone definition.
- Shop row zone definition.
- Card pack zone definition.

### Zone Instance

The runtime zone in a session.

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

## Visibility Model

Visibility should not be a single boolean.

The early model should separate two channels:


| Channel               | Question It Answers                                                                | Example                                                                        |
| --------------------- | ---------------------------------------------------------------------------------- | ------------------------------------------------------------------------------ |
| Membership Visibility | Can this actor know which instance IDs are in this zone and how they are arranged? | A player sees the exact cards in their hand; an opponent may only see a count. |
| Detail Visibility     | Given a known instance ID, can this actor see its definition and variable data?    | A face-down card may be visible as an instance but hide its definition.        |


Later, visibility may include more channels:

- Count visibility.
- Order visibility.
- Group visibility.
- Definition visibility.
- Variable visibility.
- Modifier visibility.
- Relationship visibility.
- History visibility.

Actor/player/ownership semantics are not finalized yet, so this doc only establishes the need for per-viewer visibility.

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
| Card Pack      | Generated or contained instances | Hidden bundle or reveal sequence                     | May hide contents until opened                     | Pack opening and generation rules.                |


## Open Questions Left For Later

- Are zones always entity instances, or can some zones be lightweight session records?
- Is zone membership best represented as a variable, relationship, Store slice, or zone-owned collection?
- How do actor, player, owner, controller, and viewer differ?
- How do game/action rules query zone organization without delegating legality to the zone?
- What does a forced/override insertion API look like?
- What is the exact visibility query API?
- How should history record hidden movement?
- How should generated rewards and choices relate to zones?
- How should spatial rule systems attach to zone content?

## Useful Invariants

- Zones are simulation models.
- Zones can be instances with definitions.
- Zones own contents, organization, capacity defaults, and change bookkeeping.
- Zones do not own broad gameplay legality.
- Zone membership does not replace an instance.
- Stack concepts can be modeled as zones with custom organization.
- Spatial placement is not required in the base zone model.
- Visibility needs separate membership and detail channels.
- Movement between zones is approved by game/action rules and organized by zones.


---

# Provenance: Zone Model Decisions (from doc 11)

Atomic decisions captured below also become ADRs in `features/zones/decisions/` per phase 5 of the migration.

# Zone Model Decisions

This document answers the Zones questions from [Open Questions](general/OPEN-QUESTIONS.md). These are early research decisions, not final implementation contracts.

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

