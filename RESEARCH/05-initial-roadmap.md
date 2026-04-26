# Initial Roadmap

This roadmap is intentionally high-level. It describes learning and construction phases, not implementation tasks.

## Phase 0: Research Skeleton

Goal: define the shape of the project before building systems.

Outputs:

- Research index and concept docs.
- Archetype comparison.
- Capability map.
- Architecture direction.
- Scaffold package fit analysis.
- Open questions list.

Exit criteria:

- We can explain the framework's layers.
- We can name the major systems and why they exist.
- We have a short list of first architecture bets and deferred decisions.

Status: current phase.

## Phase 1: Core Vocabulary And Invariants

Goal: settle the language and non-negotiable invariants of the framework.

Questions to answer:

- What exactly is an entity?
- What is a card definition vs a card instance?
- What is a zone?
- How do ownership, control, visibility, and physical placement relate?
- What is a command, task, event, trigger, and history entry?
- What does deterministic mean for this framework?

Possible docs:

- Core glossary.
- Entity and identity model.
- Zone model.
- Event/history model.
- Command/decision model.

Exit criteria:

- A simple game can be described using the vocabulary without implementation details.
- We know which concepts belong in core vs presets.

## Phase 2: Minimal Local Simulation Shape

Goal: design the first implementable local simulation loop, still without committing to Magic-level complexity.

Suggested target: a small Hearthstone/Slay-like loop because it exercises cards, zones, choices, effects, triggers, and history without requiring full stack priority.

Conceptual flow:

1. Build session from game definition.
2. Create actors, zones, and initial card instances.
3. Generate legal decisions.
4. Accept a chosen option.
5. Validate it.
6. Resolve through a task/effect queue.
7. Emit history.
8. Update presentation from history/state.
9. Repeat until game over.

Exit criteria:

- The core loop is documented.
- We know what must be testable headlessly.
- We know where authoring plugs in.

## Phase 3: Authoring Model

Goal: decide how games define cards, zones, effects, visuals, and templates.

Areas to explore:

- ScriptableObject authoring vs schema/JSON-like definitions vs hybrid.
- Stable IDs and definition versioning.
- Effect authoring patterns: data-only, graph-like, script-backed, or compiled custom logic.
- Validation for missing references, invalid tags, broken effects, and unsupported zone moves.
- Preset templates for common card categories.

Exit criteria:

- A designer can define a simple card without touching the rules engine.
- A programmer can add a complex effect without breaking data-driven cards.
- Definitions can be loaded into a deterministic runtime model.

## Phase 4: Presentation And Interaction Model

Goal: design how cards look and feel without giving presentation rule authority.

Areas to explore:

- Card view models from state/history.
- UI, 2D, and 3D card view adapters.
- Zone layout models.
- Drag/drop, click, hover, target, inspect, stack, and cancel gestures.
- Legal target highlighting and invalid action feedback.
- Animation queue driven by history.

Exit criteria:

- A card can be represented in multiple visual modes.
- Input becomes a command/decision response.
- Rules remain authoritative over legality.

## Phase 5: Preset Rule Kits

Goal: make the framework practical by composing common patterns.

Candidate presets:

- **Simple deck game**: deck, hand, discard, draw, play, resolve.
- **Roguelike deckbuilder**: energy, encounter state, relics, rewards, upgrades, exhaust.
- **Structured TCG**: phases, priority, stack, targeting, combat, state checks.
- **Spatial card sim**: freeform board, stacking, timers, recipes, auto-workers/combat.

Exit criteria:

- Presets reuse shared primitives.
- A project can start from a preset and override specific systems.
- Presets clarify which abstractions are truly common.

## Phase 6: Tooling And Diagnostics

Goal: make complex card interactions understandable.

Areas to explore:

- State inspector.
- History viewer.
- Rule trace for legality and replacement/prevention.
- Trigger queue inspector.
- Scenario runner.
- Replay viewer.
- Definition validation reports.

Exit criteria:

- A developer can answer "why did this card do that?"
- A test can reproduce a bug from a saved state/history pair.

## Phase 7: Multiplayer Adapter

Goal: attach networking to stable command/history/snapshot boundaries.

Keep this as a second major step, but preserve readiness from the beginning.

Areas to explore:

- Server-authoritative session.
- Command and decision response envelopes.
- Per-viewer history filtering.
- Snapshot for join/reconnect.
- Definition hash/version agreement.
- Deterministic RNG logging.
- AutoPacker or equivalent payload packing.

Exit criteria:

- Core rules do not reference the transport.
- Networked play uses the same command/history model as local play.
- Hidden information and invalid actions are handled at the authoritative boundary.

## Phase 8: Scale And Hardening

Goal: make the framework robust enough for real games.

Areas to explore:

- Performance with many entities/cards/effects.
- Save/load and migration.
- Long-running simulations.
- AI and automated testing.
- Modding or external definition packs.
- Cross-platform input and presentation.
- Documentation and onboarding.

Exit criteria:

- The framework supports production workflows, not only demos.
- Common bugs can be debugged through tools.
- Games can choose how much of the framework they adopt.

## Suggested Immediate Next Research Docs

After this initial set, the next useful documents are probably:

1. Core glossary and invariants.
2. Entity/definition/instance model.
3. Zone taxonomy and movement semantics.
4. Command/decision/history model.
5. Scheduler comparison: queue, stack/priority, turn phases, real-time tick.
6. Authoring model options.
7. Presentation interaction model.

## Roadmap Principle

Build the framework from stable boundaries outward:

1. Core vocabulary.
2. Deterministic local simulation.
3. Reactive presentation.
4. Authoring tools.
5. Presets.
6. Multiplayer.
7. Advanced tooling and scale.
