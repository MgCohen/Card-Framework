# Card Framework Research Index

This folder is the working area for initial thinking, research, and outline work. Nothing here should be treated as final implementation guidance yet.

## Current Research Set

- [Vision and Scope](00-vision-and-scope.md) - what this framework is trying to become, non-goals for now, and guiding principles.
- [Game Archetypes](01-game-archetypes.md) - lessons from structured TCGs, roguelike deckbuilders, physical card simulations, and Hearthstone-style simulators.
- [Capability Map](02-framework-capability-map.md) - the broad systems a general card-game framework may need.
- [Architecture Directions](03-architecture-directions.md) - candidate architecture boundaries and data-flow direction.
- [Scaffold Package Fit](04-scaffold-package-fit.md) - how existing Scaffold packages might support the framework.
- [Initial Roadmap](05-initial-roadmap.md) - high-level phases and sequencing.
- [Open Questions](06-open-questions.md) - design decisions to defer, research further, or prototype later.
- [Current Recommendations](07-current-recommendations.md) - opinionated synthesis from this research pass.

## Existing Notes

Earlier research has been preserved under [Old Docs](Old%20Docs/). The strongest recurring ideas from those notes are:

- A deterministic core simulation that can run without Unity presentation.
- Server-authoritative multiplayer later, with clients sending selections from precomputed legal options.
- Entity/tag or definition/instance modeling for cards, zones, players, and game objects.
- A task/action queue for resolving card effects, triggered effects, and cascades.
- An ordered history/event stream that presentation and networking can observe.
- A strict separation between rules, presentation, authoring, and transport.

## Working Assumption

For this research pass, multiplayer should influence the architecture but not dominate the first implementation step. If the simulation is deterministic, data-driven, and event/history oriented, a network layer can later attach to the same command and history boundaries.