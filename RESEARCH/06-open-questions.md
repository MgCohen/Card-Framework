# Open Questions

These are unresolved design questions worth answering before implementation or during small focused spikes.

## Core Model

- Should the pure simulation core depend directly on Scaffold packages, or should Scaffold be used only in Unity-facing adapters?
- Should card/entity values be stored primarily as flexible variables/tags, typed state records, or a hybrid?
- What is the canonical source of truth: entity instances, Store slices, or a composed model where each owns different concerns?
- How should identity behave when a card changes zones?
- Do all entities have definitions, or can runtime-only entities exist without definitions?
- How do attachments, equipment, stacked cards, tokens, and transformed cards relate to base card identity?

## Zones

- What zone traits are mandatory in core?
- How should spatial board positions relate to zone membership?
- Are stacks zones, relationships, or both?
- How should hidden, partially visible, and viewer-specific state be modeled?
- Should zone movement be a generic event, a task, or a specialized rules primitive?
- How do we model temporary views like reward offers, shops, card packs, draft picks, and generated choices?

## Rules And Scheduling

- Is the first scheduler a simple deterministic queue, or should a stack/priority model exist from the start?
- Can queue, stack, turn-phase, and real-time tick scheduling share one abstraction?
- How do replacement/prevention effects hook in before an event commits?
- How are simultaneous events represented?
- When exactly are state-based checks run?
- How do continuous effects and derived values recompute deterministically?
- Should effects be async, synchronous, or simulation-step based?

## Authoring

- Should card effects be authored as data, code, visual graphs, or a mix?
- What is the smallest useful effect language?
- How do we make simple cards easy while keeping complex cards possible?
- How do definitions reference visuals, sounds, animations, and gameplay logic without coupling everything together?
- What validation should run in editor?
- How are definition IDs versioned for save/load and multiplayer?

## Presentation

- What is the common card presentation contract across UI, 2D, and 3D?
- Should animation consume raw history, presentation events, or a transformed animation timeline?
- How much visual state is allowed to exist outside canonical simulation state?
- How do we handle animation lag while the simulation has already advanced?
- How does input ask for legal targets or drop destinations?
- What should a failed action look like when the gesture was visually allowed but rules reject it?

## Multiplayer

- What is the minimum command envelope needed for future network transport?
- What is the minimum history envelope needed for replay and transport?
- When should visibility filtering happen: in core, adapter, or both?
- Should clients maintain a full filtered Store or only presentation state?
- How are reconnect snapshots generated and applied?
- How are definition mismatches detected?
- Do we need rollback/prediction for any target game type, or can authoritative reactive play be the default?

## Scaffold Integration

- Is `com.scaffold.entities` suitable for runtime simulation, or better for authoring only?
- Should `com.scaffold.states` become the canonical match state store?
- How do `com.scaffold.entities` variable modifiers and `com.scaffold.states` mutators interact without duplicating state?
- Should MVVM models be generated from simulation state/history, or should they subscribe directly to Store slices?
- When do we introduce AutoPacker: after local simulation, after multiplayer design, or only when payloads are stable?

## Presets

- What belongs in the common core vs a TCG preset?
- What belongs in a deckbuilder preset vs a generic zone/effect system?
- What belongs in a spatial simulation preset vs generic drag/stack interaction?
- Can presets be composed, or are they mutually exclusive modes?
- How do games override preset behavior without forking the framework?

## Testing And Tooling

- What is the first testable scenario?
- What must be deterministic in tests?
- Should tests assert final state, full history, or both?
- How do we snapshot a scenario in a human-readable way?
- What debug tools are required before complex effect work begins?
- How do we trace "why was this action legal/illegal?"

## Research Biases To Watch

- Do not overfit to Hearthstone just because the old docs explored SabberStone deeply.
- Do not overfit to Magic's full rules complexity before simpler schedulers prove themselves.
- Do not treat Stacklands as "just presentation"; spatial placement and stacking are rules.
- Do not make multiplayer early implementation work, but do keep command/history/identity boundaries multiplayer-friendly.
- Do not let data-driven authoring prevent custom code for genuinely complex cards.

## Likely First Decisions

The first practical decisions should probably be:

1. Pure core dependency boundary.
2. Canonical state model.
3. Entity/definition/instance identity rules.
4. Zone taxonomy.
5. Command/task/event/history terminology.
6. First scheduler model.
7. First game preset target for a vertical slice.
