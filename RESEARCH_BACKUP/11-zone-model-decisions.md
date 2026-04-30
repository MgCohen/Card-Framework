# Zone Model Decisions

This document answers the Zones questions from [Open Questions](06-open-questions.md). These are early research decisions, not final implementation contracts.

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

