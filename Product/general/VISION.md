---
type: reference
status: settled
tags: [#general, #vision, #scope]
---

# Vision and Scope

## Goal

Build a Unity card-game framework that can support a wide range of card-based games, from highly structured competitive card games to roguelike deckbuilders and spatial card simulations.

The framework should provide reusable foundations for:

- Authoring card definitions, card instances, zones, rules, effects, and interactions.
- Running deterministic card-game simulations.
- Presenting cards as UI, 2D objects, or 3D objects.
- Supporting drag, targeting, selection, stacking, hovering, inspection, and other card interactions.
- Reacting to state, value, and zone changes in a composable way.
- Letting each game define its own rules without rewriting the whole engine.
- Adding multiplayer later through a transport adapter that observes commands, state changes, and history.

## Design North Star

The core question is not "how do we build Magic, Slay the Spire, or Stacklands?" It is:

> What shared foundation lets a game define its own card rules, card spaces, card affordances, and card flows without fighting the framework?

That suggests a layered framework:

1. **Simulation core** - deterministic rules, state, entities, zones, effects, triggers, choices, and history.
2. **Authoring layer** - definitions, schemas, card catalogs, zone definitions, interaction definitions, and game templates.
3. **Presentation layer** - reactive views for UI cards, 2D cards, 3D cards, boards, piles, stacks, hands, and spatial play areas.
4. **Input layer** - pointer/drag/drop/target/selection gestures translated into game-intent requests.
5. **Integration layer** - optional networking, persistence, replay, AI, tools, and diagnostics.

## Initial Non-Goals

- No code implementation in this research phase.
- No commitment yet to one final rules model.
- No multiplayer transport implementation yet.
- No final asset format or editor tooling commitment yet.
- No attempt to support every edge case from every game on day one.

## Important Constraints

- The core simulation should not depend on Unity scene objects.
- Presentation should react to state/history instead of owning gameplay truth.
- Authoring should be data-friendly, but advanced cards must still be able to run custom logic.
- Multiplayer readiness should come from deterministic commands and history, not from networking inside the rules engine.
- The framework must allow games that are not only card games, but card-based games with wider systems around cards.

## Success Shape

A future game built on the framework should be able to define:

- Card definitions and card instance behavior.
- Zone types, organization behavior, and movement flow.
- Turn, phase, priority, or real-time tick structure.
- Legal action generation and validation.
- Effects, triggers, replacement/prevention rules, and reactions.
- Visual card prefabs or UI templates.
- How physical interaction maps to game commands.
- Whether the simulation is local-only, replayable, AI-driven, or networked.

## Risks To Keep Visible

- A fully generic card-game framework can become too abstract to be usable.
- Magic-style rules, Slay-style combat, and Stacklands-style spatial simulation may require different scheduling models.
- Data-driven card authoring can become brittle if every special case escapes into custom code.
- Multiplayer can become invasive if command, history, and instance-reference boundaries are not planned early.
- Presentation can leak rules if drag/drop code decides legality instead of asking the simulation.

## Working Principle

Favor a small set of strong invariants over a giant hierarchy of game-specific abstractions:

- Cards and related objects are represented as instances.
- Definitions and instances are separate.
- Zones are first-class.
- All meaningful changes produce observable events/history.
- Player input becomes a request, not a direct mutation.
- Rules own legality.
- Presentation owns feel.