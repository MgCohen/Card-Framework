# Current Recommendations

This is the opinionated synthesis from the first research pass. It should be challenged as the project evolves.

## Recommendation 1: Start With A Deterministic Local Core

The first serious milestone should not be multiplayer, editor tooling, or a complete UI. It should be a deterministic local simulation core that can be tested headlessly.

Reasoning:

- Multiplayer becomes easier if local simulation already accepts commands and emits ordered history.
- Presentation becomes cleaner if it reacts to state/history instead of owning rules.
- Card effects become debuggable if every change has a trace.
- AI, replay, and tests all benefit from the same deterministic core.

## Recommendation 2: Use A Queue-Based Scheduler First

Start with a queue-based scheduler similar to the Hearthstone/SabberStone/Slay direction, then design stack/priority and real-time tick schedulers as extensions.

Reasoning:

- A queue is enough to prove cards, zones, commands, effects, triggers, choices, and history.
- It keeps the first vertical slice achievable.
- It can still support cascades and nested effects.
- Magic-style priority can be added more cleanly after the base command/history model is understood.

## Recommendation 3: Treat Zones As A Core Primitive

Zones should not be hardcoded as deck/hand/discard. They should be configurable organization models that game/action rules can query.

Reasoning:

- Magic needs formal zones with visibility, ordering, and stack organization.
- Slay needs combat piles, run deck, rewards, shops, exhaust, and relic inventories.
- Stacklands needs freeform spatial areas, physical stacks, production groups, and card packs.
- Multiplayer needs zone visibility and hidden-information filtering.

## Recommendation 4: Explore Scaffold Entities Before Drawing A Hard Boundary

`com.scaffold.entities` looks directly relevant to the current Core Model direction: definitions, runtime instances, variables, modifiers, and composition. Do not force it to be authoring-only or simulation-only yet.

Reasoning:

- The current preferred value model is simple variables inspired by Scaffold Entities.
- The project may benefit from using the same concepts for authoring and runtime.
- Scaffold States may still be better for snapshots, mutation history, or canonical state.
- We do not yet know whether Entities and Store complement each other or duplicate each other.

This is not a final package decision. It is a research direction: test the fit before inventing a custom model.

## Recommendation 5: Make History A First-Class Output

The framework should not treat history as "network messages later." It should be a core simulation product.

History can support:

- Reactive presentation.
- Animation sequencing.
- Replay.
- Debug traces.
- Test assertions.
- Save/restore diagnostics.
- AI search and evaluation.
- Multiplayer replication.

## Recommendation 6: Model Player Input As Decision Responses

For structured games, the rules engine should generate legal choices. Presentation should display them and return a selected option, not construct arbitrary effects.

For spatial games, drag/drop can still become a decision response:

- "Move entity A to zone B."
- "Stack entity A on entity B."
- "Assign worker A to task target B."
- "Use card A with target B."

The rules engine then validates and resolves.

## Recommendation 7: Ship Presets, Not One Giant Abstract Game

The framework should have a common core and multiple presets.

Likely presets:

- Simple deck/card queue game.
- Roguelike deckbuilder.
- Structured TCG.
- Spatial card simulation.

Reasoning:

- Users need practical starting points.
- Presets keep common systems concrete.
- Games can adopt a preset, replace pieces, or compose features.

## Recommendation 8: Defer Network Transport, Not Network Shape

No network implementation yet, but define core concepts as if they may cross a wire later:

- Stable entity IDs.
- Stable definition IDs.
- Explicit command/decision envelopes.
- Ordered history.
- Snapshots.
- Viewer-specific visibility.
- Deterministic random source.

## Suggested First Vertical Slice

A good first implementation target later would be a small local deckbuilder combat prototype:

- One player.
- Draw pile, hand, discard, exhaust.
- Energy.
- A few card definitions.
- Play card command.
- Target validation.
- Damage/block/status effects.
- End turn flow.
- Simple enemy intent.
- History-driven presentation.

Why this target:

- It exercises most core systems.
- It avoids hidden information and multiplayer.
- It does not require Magic's full priority model.
- It can later grow toward Slay-style relics/rewards or Hearthstone-style opponents.

## Alternate First Vertical Slice

If the priority is physical interaction feel, choose a Stacklands-like slice instead:

- Freeform board zone.
- Drag cards.
- Stack cards.
- Recipe validation.
- Timer-driven production.
- Auto-combat or auto-harvest.
- History-driven animation.

This better tests spatial systems, but it may delay structured rule-engine work.

## Current Preferred Path

1. Define vocabulary and invariants.
2. Design local simulation loop.
3. Choose the first vertical slice.
4. Design authoring conversion.
5. Design presentation and interaction around history.
6. Add presets.
7. Add multiplayer adapter later.

The most important early discipline is keeping rules, authoring, presentation, and networking separate enough that each can evolve without rewriting the others.