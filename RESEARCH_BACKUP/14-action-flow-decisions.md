# Action Flow Decisions

This document captures the current direction for action flow, based on the Card Framework research so far and the DeckMech command-pipeline sample summarized in [DeckMech Command Pipeline Reference](13-deckmech-command-pipeline-reference.md).

These are early research decisions, not implementation contracts.

## Status

The direction for action flow is now:

- The framework should provide a generic pipeline shape, not one universal rules engine.
- Each game or preset can decide how strict, networked, stacked, local, or permissive the pipeline is.
- UI is visual/reactive and should not own gameplay truth.
- Player interaction produces intent/request objects.
- The engine may approve, reject, replace, redirect, or ignore those requests depending on the game's rules.
- Approved requests resolve into game commands and state mutations.
- State changes are batched into an envelope with cause metadata so UI, animation, logs, networking, and debugging can understand why changes happened.
- Possible actions/options may exist and may be shown by the UI, but the base framework should still support attempts that are validated by the engine.
- Naming is not settled. The "action" family of terms needs its own discussion before implementation terminology spreads.

## Generic Pipeline, Game-Specific Rules

Decision: define an abstract action pipeline that every game can pass through, while allowing games to supply very different policy layers.

Examples:

- A Magic-like game may route an action through priority, stack, replacement effects, triggers, and networking before state changes.
- A Slay-like local game may validate against local values, energy, targets, relics, and encounter state before state changes.
- A Stacklands-like game may approve most movement automatically, update a physical stack zone, and let other systems observe that change.

The common shape is:

```text
intent/request -> game/preset pipeline -> approved command(s) -> state mutations -> batched history -> reactive UI
```

Reasoning:

The framework cannot hardcode the full rules of every card-based game. It can provide stable surfaces for routing, validation, resolution, mutation, history, and observation. Each game fills those surfaces with its own rules.

Implication:

The base action flow should be open enough that a preset can be strict, permissive, local, server-authoritative, stack-based, or real-time.

Confidence: high.

## UI And Interaction Boundary

Decision: UI is purely visual/reactive with respect to game state.

The UI can:

- Render current state.
- Render known possible actions.
- Highlight legal or suggested targets if the game exposes that data.
- Let the player drag, drop, click, target, inspect, cancel, or choose.
- Optimistically show a drag preview while waiting for approval.

The UI should not:

- Decide final legality.
- Mutate simulation state directly.
- Own card/zone truth.
- Assume every failed move is impossible to attempt.

Reasoning:

Different games want different UX constraints. A strict game may only let players drag legal cards. A sandbox-like game may let players try anything and reject or snap back if needed. The base framework should support both by making interaction produce an intent/request, not a state mutation.

Implication:

Failed requests should produce a result the presentation can react to:

- Snap back.
- Show invalid feedback.
- Show no-op.
- Ask for another target.
- Display reason, if the game chooses to expose one.

The result should be structured even when empty or default. This gives UI/UX a stable surface for rejected requests, no-ops, and accepted requests.

Confidence: high.

## Possible Actions And Free Attempts

Decision: support both precomputed possible actions and freeform attempted actions.

Some games generate explicit action options:

- Play this card.
- Move this card to this zone.
- Activate this ability.
- Choose option 2.
- Pass priority.

Other games may let the player attempt almost anything and validate after the request is submitted.

The base framework should not require one model exclusively.

Reasoning:

Hearthstone/SabberStone-style systems are built around server-authored option lists. Stacklands-style interaction feels more direct and spatial. Magic-like games may use both: actions available through priority plus choices during resolution.

Implication:

Action flow should support:

- `AvailableAction` or `DecisionOption` discovery.
- UI highlighting from known options.
- Attempted actions that are not in a precomputed list.
- Rejection or approval by the engine.
- Matching a request against an active option set when the game requires it.

Confidence: high.

## Actors And Entities

Decision: `Actor` and `Entity` are overlapping roles, not exclusive categories.

An entity is anything with variables/key-value state. An actor is anything that can own, control, view, or decide things. A player can be both:

- Entity: because it can have variables, resources, flags, score, life, mana, permissions, or status.
- Actor: because it can own cards, control instances, see hidden information, and make decisions.

Systems may also be actors:

- Environment actor.
- Neutral market actor.
- Dungeon actor.
- Stack/system actor.
- Automated enemy actor.

Reasoning:

Ownership, visibility, control, and decision-making are not limited to human players. Some cards or zones may belong to a system, encounter, shop, neutral board, or AI actor.

Implication:

The actor model should eventually define:

- Ownership.
- Control.
- Visibility/viewer relation.
- Decision authority.
- Source/cause metadata.

But it should not replace the entity model. Actors can be entities when they need variables.

Confidence: medium-high.

## Definitions, Metadata, And Effects

Decision: definitions should contain baseline variables and metadata; effects should start with a simple executable interface.

For cards, an early mental model is:

```text
Card Definition =
  baseline variables
  metadata
  visual/editor metadata
  effect entry point
```

The effect entry point can initially be a simple serializable class with an `Execute`-style method. Later it can be backed by visual scripting, databases, graphs, scripts, or generated logic.

Reasoning:

Effects will become complex, but the framework needs a simple entry point first. Complexity should hide behind an interface so authoring can evolve without rewriting the pipeline.

Implication:

Do not overdesign effect authoring now. Define a small execution boundary that can later support:

- Simple serialized classes.
- Triggered effects.
- Replacement/prevention effects.
- Visual scripting.
- Data-driven effects.
- Custom code.

For the immediate research phase, effects can be treated as a simple placeholder entry point such as `Trigger()` or `Execute()` that proves the pipeline can invoke something. Proper effect authoring, triggers, visual scripting, and reaction timing should be designed later.

Confidence: medium-high.

## Approval And Command Generation

Decision: an interaction intent/request is not necessarily the same thing as the command that mutates state.

Possible flow:

```text
Player drags card A to zone B
  -> Interaction creates request: Move/Use/Drop/Play A toward B
  -> Engine validates or matches against possible actions
  -> Engine approves, rejects, replaces, or redirects
  -> Approved request resolves into one or more commands/state mutations
```

The request might already contain enough information to become a command, or the engine might create the command after validation.

Reasoning:

In some games, "drag card to zone" maps directly to "move instance." In others, it maps to "play card," which may move to stack, pay cost, choose targets, resolve effects, and then move to discard.

Implication:

The framework should distinguish:

- What the user or system attempted.
- What the game approved.
- What commands/state mutations actually happened.

Confidence: high.

## Batched History Envelopes

Decision: state changes from one approved action should be grouped into a history/result envelope.

The envelope should preserve cause metadata:

- What request/action caused this.
- Which actor caused it.
- Which source instance caused it.
- Which command or handler produced it.
- Which state mutations happened.
- Which events/history entries were emitted.
- What result was returned.

Reasoning:

Raw state changes tell the UI that something changed, but not why. For animations, logs, networking, debug traces, and player clarity, we need the boundary object that says "this damage happened because this actor used this card on that target."

Implication:

The framework should batch changes at meaningful action boundaries. UI can then animate or present transitions as coherent groups instead of reacting to every low-level mutation independently.

Confidence: high.

## Deferred Or Grouped Notifications

Decision: state mutation can happen internally in steps, but external observers should usually receive grouped notifications at action boundaries.

This matches the DeckMech discovery: layering command resolution and grouping the resulting state changes makes UI and animation easier to schedule.

Reasoning:

Many card effects cause several changes:

- Pay cost.
- Move card.
- Deal damage.
- Apply modifier.
- Trigger reaction.
- Draw card.
- Move source to discard.

If presentation reacts to these as unrelated updates, it loses context. A history envelope lets presentation know the sequence and cause.

Implication:

The eventual state/event system should support:

- Internal state mutation and reads during command resolution.
- Deferred external notification.
- A history envelope containing ordered low-level changes.
- Optional animation timeline generation from the envelope.

Confidence: high.

## Decisions To Carry Forward

- Provide a generic action pipeline shape; games supply the rules.
- UI emits intents/requests and reacts to state/history.
- Rejections and no-ops should still return structured results.
- Possible action lists are optional, not mandatory for all games.
- Actor and Entity are overlapping roles.
- Definitions contain variables plus metadata.
- Effects exist, but deep effect design is deferred; use a minimal placeholder entry point for now.
- Approval and mutation are distinct.
- One approved action should produce a grouped history/result envelope.
- Low-level state changes should carry cause context through that envelope.

