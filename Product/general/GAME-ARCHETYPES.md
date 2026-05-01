---
type: reference
status: settled
last-updated: 2026-04-29
tags: [#general, #archetypes]
---

# Game Archetypes

This note compares representative card-game families so the framework can aim at reusable foundations instead of a single game's rules.

## Structured TCGs: Magic: The Gathering

Magic is useful as the high-complexity reference. Its official comprehensive rules define formal zones, priority, stack resolution, turn structure, triggered abilities, replacement effects, continuous effects, and state-based actions.

Relevant official rule anchors from the current comprehensive rules:

- Rule 400.1: zones include library, hand, battlefield, graveyard, stack, exile, and command.
- Rule 117.1: priority determines which player can act.
- Rule 405.1: spells and abilities wait on the stack before resolving.
- Rule 500.1: turns are divided into phases and steps.
- Rule 500.2: priority phases or steps end only when the stack is empty and all players pass in succession.
- Rule 603.1: triggered abilities have a trigger condition and effect.
- Rule 614.1: replacement effects continuously watch for events and replace them.
- Rule 704.3: state-based actions are checked before priority, performed simultaneously, and repeated until stable.

Framework lessons:

- Zones must be first-class, configurable, and capable of hidden/public/ordered/shared/player-owned semantics.
- The rules scheduler needs more than "play card, resolve card"; it needs priority windows, pending triggers, immediate checks, and event replacement.
- Effects should be able to watch, replace, prevent, queue, and resolve other effects.
- Zone changes need careful handling. In Magic, many zone changes effectively replace one game object with another for rules purposes.
- A framework should allow stack-based games, even if simpler games do not use a stack.

Sources:

- [Magic Comprehensive Rules](https://media.wizards.com/2026/downloads/MagicCompRules%2020260227.txt)
- [Magic rules mirror](https://www.mtg.wtf/help/rules)

## Hearthstone-Style Simulators

Prior notes in [spikes/](spikes/) explored SabberStone, Fireplace, and Brimstone. The recurring pattern is an authoritative simulator that computes legal options, accepts option-index selections, resolves through a task/action queue, and emits an ordered history stream.

Key lessons:

- Clients should not send "deal 3 damage"; they should send "I choose option 7 with target 42" from a server-authored option set.
- A task/action queue is a practical way to avoid recursive effect resolution.
- PowerHistory-style logs are valuable beyond networking: replay, animation, debugging, tests, AI, and rollback can all consume them.
- Hearthstone-style games can often avoid client prediction because the client is a reactive shell.
- SabberStone's published goals include available-option calculation, game cloning, declarative tasks, and game tree search.

Sources:

- [SabberStone overview](https://hearthsim.info/sabberstone)
- [Kettle protocol](https://hearthsim.info/kettle/)

## Roguelike Deckbuilders: Slay The Spire

Slay the Spire combines card combat with run progression, map choices, relics, rewards, shops, upgrades, statuses, curses, and encounter-local rules.

Important mechanics from the researched gameplay references:

- The player starts each combat turn with a default hand draw and energy budget.
- Cards are drawn from a deck, discarded at turn end, and reshuffled from discard when draw is empty.
- Cards can be attacks, skills, powers, statuses, or curses.
- Powers usually become encounter-long passive state after being played.
- Status cards are temporary combat bloat; curses persist across the run.
- Relics are persistent modifiers that react to combat and run events.
- Rewards and map choices are part of the card system, not just external progression.

Framework lessons:

- The framework needs scoped state: run-level, encounter-level, turn-level, card-instance-level, and transient effect-level.
- Cards are not only combat actions; they can be rewards, penalties, deck-building objects, shop goods, upgrade targets, or event outcomes.
- Passive systems like relics need the same event/trigger infrastructure as card abilities.
- Not every game needs multiplayer or hidden opponent state, but deterministic replay and action history still matter for testing and tools.
- Encounter-local copies and run-persistent deck entries need clear instance/reference boundaries.

Source:

- [Slay the Spire gameplay reference](https://slay-the-spire.fandom.com/wiki/Gameplay)

## Spatial Card Simulations: Stacklands

Stacklands treats cards as physical board objects. The player drags cards onto one another to harvest, craft, build, feed villagers, fight creatures, and expand the village.

Important researched mechanics:

- Cards are dragged and stacked on a board to interact.
- Stacks can represent recipes, tasks, combat groups, storage, production chains, or physical proximity.
- Time matters through Moon cycles, production timers, feeding, portals, and combat.
- Ideas act as recipe cards.
- Coins and resources are themselves cards.
- Card packs create cards and progression.
- Combat is automatic once compatible cards meet.

Framework lessons:

- A card framework should not assume cards only live in hand/deck/battlefield zones.
- Spatial relation, stack membership, proximity, and physical grouping may be rules data.
- Some games need time-based processing instead of player-priority turns.
- Drag/drop can be the primary rules input, but legality still belongs to the simulation.
- Zones may be abstract areas, physical containers, freeform boards, stacks, or dynamic groups.

Sources:

- [Stacklands overview](https://en.wikipedia.org/wiki/Stacklands)
- [Stacklands wiki](https://stacklands.fandom.com/wiki/Stacklands_Wiki)

## Cross-Archetype Takeaways

- **Cards are entities, not just data rows.** A definition says what a card is; an instance says where this copy is, who owns it, what changed, and what it is doing now.
- **Zones are organization objects.** A zone can own contents, ordering, visibility channels, capacity defaults, and insertion/removal behavior. Game/action rules decide whether something is allowed to move there.
- **Effects need a scheduler.** The scheduler may be stack-based, queue-based, turn-step-based, tick-based, or a mix.
- **Game action should be request/validate/resolve.** Presentation requests intent; rules validate and resolve.
- **Reactive history is central.** Animation, UI, replays, tests, AI, save/load, and networking all benefit from the same ordered stream.
- **Framework presets may matter.** A single universal engine might be less useful than a common core plus presets for TCG, deckbuilder, and spatial simulation patterns.