# Framework Capability Map

This is a broad inventory of systems the framework may eventually need. It is intentionally high-level and should be refined into packages, milestones, and tests later.

## Core Concepts


| Capability            | Purpose                                               | Notes                                                                                       |
| --------------------- | ----------------------------------------------------- | ------------------------------------------------------------------------------------------- |
| Game Session          | Owns one playable simulation instance.                | Could represent a match, encounter, run, or board simulation.                               |
| Player / Actor        | Represents decision makers.                           | Human, AI, enemy intent, automated village worker, environment, or system actor.            |
| Entity                | Runtime instance for cards and card-adjacent objects. | Cards, players, zones, piles, tokens, relics, resources, villagers, enemies, stack entries. |
| Definition            | Shared authored data.                                 | Card definitions, zone definitions, game templates, effect definitions, visual definitions. |
| Instance              | Per-session copy of a definition.                     | Tracks owner, controller, zone, modifiers, current values, visibility, state flags.         |
| Variable / Tag / Stat | Dynamic values attached to entities.                  | Health, cost, attack, exhausted, face-down, stack height, recipe progress, energy.          |
| Zone                  | Simulation model that contains or groups instances and organizes those contents. | Hand, deck, discard, battlefield, rules stack, physical stack, shop, pack, reward offer.    |
| Relationship          | Links between entities.                               | Owner, controller, attached-to, equipped-by, stacked-on, targeting, source, parent zone.    |


## Simulation Systems


| Capability               | Purpose                                           | Examples                                                                           |
| ------------------------ | ------------------------------------------------- | ---------------------------------------------------------------------------------- |
| State Store              | Canonical state model and mutation surface.       | Current zones, entity values, active turn, run data.                               |
| PlayerIntent             | Represents external or system intent.             | Drag card, play card, choose reward, end turn, buy pack.                           |
| AvailableAction          | Optional engine-exposed affordance.               | Playable card, valid destination, activatable ability.                             |
| Game Command             | High-level approved or approvable rules command.  | Deal damage, draw card, move instance, play card.                                  |
| StateMutation            | Low-level already-approved mutation.              | Set variable, insert into zone, add modifier, create instance.                     |
| Validator                | Determines legality.                              | Can pay cost, valid target, recipe match, destination is legal, actor has priority. |
| Action / Task Queue      | Resolves effects in deterministic order.          | Draw, damage, move zone, spawn, harvest, craft, discard, destroy.                  |
| Scheduler                | Governs when work happens.                        | Turn/phase/priority, immediate queue drain, real-time tick, delayed timer.         |
| Event Stream             | Emits facts about state changes and rules events. | Card moved, entity created, damage dealt, choice opened, timer completed.          |
| Trigger System           | Reacts to events and queues work.                 | Whenever card drawn, after card enters zone, at start of turn, when stacked.       |
| Replacement / Prevention | Changes events before commit.                     | Prevent damage, enter tapped, modify card draw, redirect target, replace death.    |
| Continuous Effects       | Ongoing derived modification.                     | Buffs, auras, relic passives, equipment bonuses, global rules.                     |
| State-Based Checks       | Stabilizes invalid or terminal state.             | Destroy dead units, lose game at zero health, remove empty stacks, feed villagers. |
| Choice / Decision System | Creates explicit decision windows.                | Legal options, target choices, modal cards, card rewards, branching events.        |
| Randomness               | Controlled random operations.                     | Shuffle, random reward, random target, pack contents, procedural map.              |
| History / Replay         | Ordered log of authoritative events.              | Animation, replay, rollback, tests, debugging, networking.                         |
| History Envelope         | Groups state changes under cause metadata.        | One card play caused cost spend, zone moves, damage, triggers, and discard.        |


## Card Authoring Systems


| Capability             | Purpose                                  | Notes                                                                             |
| ---------------------- | ---------------------------------------- | --------------------------------------------------------------------------------- |
| Card Catalog           | Registry of definitions.                 | IDs must be stable for save/load/network/replay.                                  |
| Card Schema            | Common definition fields.                | Name, cost, type, tags, values, visuals, behaviors, rarity.                       |
| Effect Definition      | Data or script hook for behavior.        | Simple effects should be data-authored; complex effects may use custom code.      |
| Keyword System         | Reusable named rules.                    | Taunt, exhaust, retain, flying, stackable, edible, worker, cursed.                |
| Template / Archetype   | Preset card shapes.                      | Creature, spell, power, relic, resource, villager, structure, pack.               |
| Game Definition        | Declares game-specific rule modules.     | Turn structure, zones, validators, starting state, actor model.                   |
| Zone Definition        | Declares zone organization semantics.    | Contents, arrangement, visibility, capacity defaults, insertion/removal behavior, change bookkeeping. |
| Interaction Definition | Declares possible physical interactions. | Drag to zone, drag onto entity, click to inspect, hold to target, stack to craft. |


## Presentation Systems


| Capability             | Purpose                                  | Notes                                                                |
| ---------------------- | ---------------------------------------- | -------------------------------------------------------------------- |
| Card View Model        | Reactive presentation state.             | Reads simulation/history and exposes visible card state.             |
| Card View              | UI, 2D, or 3D representation.            | Should not own rules.                                                |
| Zone View              | Visual container for cards.              | Hand fan, grid, pile, stack, freeform board, discard pile, shop row. |
| Interaction Controller | Converts input gestures to intent.       | Pointer down, drag, hover, drop, select target, cancel.              |
| Animation Queue        | Plays state/history in a readable order. | May lag behind simulation for better feel.                           |
| Visibility / Reveal    | Controls what each viewer can see.       | Opponent hand, face-down cards, fogged board objects.                |
| Layout Engine          | Places cards visually.                   | Hand curves, pile offsets, 3D board anchors, stack compression.      |
| Feedback               | Explains legality and affordance.        | Highlights legal targets, invalid drop reason, priority indicator.   |


## Tooling Systems


| Capability          | Purpose                                                                      |
| ------------------- | ---------------------------------------------------------------------------- |
| Definition Editor   | Create and edit cards, zones, game templates, effects, and visuals.          |
| Simulation Debugger | Inspect state, history, pending tasks, triggers, and decisions.              |
| Scenario Runner     | Load a known state and test a command/effect quickly.                        |
| Replay Viewer       | Step through history and inspect state transitions.                          |
| Rule Trace          | Explain why an action was legal/illegal and which effects modified it.       |
| Validation Reports  | Detect broken card definitions, missing assets, bad IDs, invalid references. |


## Multiplayer-Ready Systems


| Capability            | Purpose                                                             | Phase                         |
| --------------------- | ------------------------------------------------------------------- | ----------------------------- |
| Command Envelope      | Portable action request with actor, session, sequence, and payload. | Later                         |
| History Envelope      | Portable ordered state/event stream.                                | Later                         |
| Snapshot              | Full state image for join/reconnect/rollback.                       | Later                         |
| Visibility Filter     | Per-viewer state/history filtering.                                 | Earlier if hidden info exists |
| Deterministic RNG     | Seeded and recorded random source.                                  | Early                         |
| Versioned Definitions | Ensure clients and server agree on card/game data.                  | Later                         |
| Transport Adapter     | Maps envelopes to network messages.                                 | Later                         |


## Candidate Package Slices

These names are exploratory, not final implementation commitments.


| Slice                      | Responsibility                                                        |
| -------------------------- | --------------------------------------------------------------------- |
| CardFramework.Core         | Definitions, instances, instance IDs, zones, values, state contracts. |
| CardFramework.Rules        | commands, validators, scheduler, tasks, effects, triggers, decisions. |
| CardFramework.History      | event/history model, snapshots, replay utilities.                     |
| CardFramework.Authoring    | ScriptableObject or schema-based authoring helpers and validation.    |
| CardFramework.Presentation | view models, card views, zone views, animation/history readers.       |
| CardFramework.Interaction  | input gestures and intent composition.                                |
| CardFramework.Networking   | optional command/history transport adapters.                          |
| CardFramework.Presets      | templates for TCG, deckbuilder, and spatial card simulation games.    |


## Capability Priority

Early foundation should likely focus on:

1. Entities, definitions, instances, zones, and state.
2. Commands, validation, task/effect queue, and history.
3. Reactive presentation model and card/zone views.
4. Authoring workflow for simple cards and zones.
5. Preset rule templates.
6. Multiplayer adapter once the command/history boundary is stable.