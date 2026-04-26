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
- [Core Model Decisions](08-core-model-decisions.md) - answered Core Model questions and current direction.
- [Core Glossary](09-core-glossary.md) - working vocabulary for future research docs.
- [Entity, Definition, And Instance Model](10-entity-definition-instance-model.md) - conceptual model for definitions, instances, instance IDs, values, modifiers, and composition.
- [Zone Model Decisions](11-zone-model-decisions.md) - answered Zone questions and current direction.
- [Zone Model](12-zone-model.md) - conceptual model for zone definitions, instances, contents, visibility, and movement.
- [DeckMech Command Pipeline Reference](13-deckmech-command-pipeline-reference.md) - compact summary of the local command-pipeline sample and refactor plan.
- [Action Flow Decisions](14-action-flow-decisions.md) - current direction for generic action pipelines, intents, actors, approval, and batched history.
- [Action, Request, Command, And History Model](15-action-request-command-history-model.md) - vocabulary and pipeline model for requests, commands, results, reactions, and history envelopes.
- [Action Terminology Discussion](16-action-terminology-discussion.md) - discussion frame for clarifying overlapping action/command/effect terms.
- [Action Definition Proposal](17-action-definition-proposal.md) - proposed concrete shapes and names for requests, commands, mutations, results, hooks, effects, and history.
- [Action Flow Snippets](18-action-flow-snippets.md) - high-level C#-style snippets showing drag, play, heal, envelope, and animation flow.
- [Handler Mutations And Semantics](19-handler-mutations-and-semantics.md) - sequential `Apply`, automatic vs manual semantic events, and composite/nested operations vs snippet verbosity.

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