# Entity, Definition, And Instance Model

This document expands the Core Model decisions into a concrete conceptual model for entities, definitions, instances, instance IDs, variables, modifiers, and composition.

Related docs:

- [Core Model Decisions](08-core-model-decisions.md)
- [Core Glossary](09-core-glossary.md)
- [Scaffold Package Fit](04-scaffold-package-fit.md)

## Model Summary

The working model is:

> Every runtime entity is an instance of a base definition. The instance has an instance ID for lookup/reference, plus relationships and modifiers. Its effective values are calculated from its definition, local state, attached instances, external effects, and context.

This model should support:

- Structured card games.
- Roguelike deckbuilders.
- Spatial card simulations.
- Cards and card-adjacent objects.
- Future multiplayer, replay, and debugging.

## Definitions

A definition is shared authored data. It describes what an entity is before runtime state changes.

Examples:

- A card definition.
- A token definition.
- A zone definition.
- A villager definition.
- An equipment definition.
- A relic definition.
- A card pack definition.
- A system helper definition.

Minimum conceptual fields:

- Stable definition ID.
- Display name or debug name.
- Entity kind or category.
- Base variables.
- Optional tags or keywords.
- Optional visual references.
- Optional behavior/effect references.
- Optional authoring metadata.

Definition IDs should be stable because they are needed for save/load, replay, debugging, and future networking.

## Instances

An instance is a runtime entity created from a definition.

Examples:

- This specific copy of Fireball in a player's hand.
- This specific villager on the board.
- This specific sword attached to a villager.
- This specific token created by an effect.
- This specific hand zone for player 1.
- This specific shop offer zone.

Minimum conceptual fields:

- Instance ID.
- Definition ID.
- Current relationships.
- Local variable overrides or current values.
- Local modifiers.
- Visibility state.
- Lifecycle state.

The instance should not duplicate every definition field. It should reference its definition and store only runtime differences.

## Instance IDs And Zone Movement

There should not be a separate conceptual model called "identity." The instance is the thing. An instance may have an `Instance ID` so systems can look it up, reference it, serialize it, or include it in history.

The `Instance ID` can be a typed record, integer, GUID-like value, or package-specific reference. That is an implementation detail. Conceptually, it is just a value associated with the instance.

Default rule:

> Moving an entity to another zone preserves the same instance and the same instance ID.

This means:

- A card in deck, hand, discard, or board can remain the same instance.
- Zone movement is an event and may trigger rules.
- Zone movement can update visibility, ordering, modifiers, or presentation.
- Zone movement does not inherently destroy/recreate the entity.

Game-specific systems may override this default:

- A Magic-like preset may treat certain zone changes as replacing one instance with another for rules purposes.
- A transform effect may replace an instance's definition or create a new instance.
- A clone/copy effect may create a new instance from an existing definition and selected copied state.
- A token leaving play may be destroyed instead of preserved.

This should be explicit game behavior, not a hidden property of all zones.

## Variables

The first value model should be key-value variable-based, inspired by Scaffold Entities but not dependent on it.

Variables represent values attached to definitions or instances:

```text
VariableKey -> VariableValue
```

The base framework should not hardcode specific variable names. A game, preset, schema, or authoring system should define the available keys, value types, default values, validation rules, and display metadata.

Examples:

- `cost`.
- `attack`.
- `health`.
- `owner`.
- `controller`.
- `zone`.
- `visibility`.
- `energy`.
- `progress`.
- `stack_count`.
- `exhausted`.

These are examples only, not required framework fields.

Variable-based modeling is useful because:

- It is flexible across game types.
- It can mirror Scaffold Entities' variable bags if that package fits.
- It supports modifiers naturally.
- It avoids overbuilding typed state before patterns are proven.

Typed wrappers can still exist later for convenience. For example, a deckbuilder preset may expose `energy` through typed helper APIs, but the underlying base model remains key-value based.

## Values And Modifiers

An effective value should be calculated from layered inputs.

Conceptual layers:

1. Base definition value.
2. Instance base value or override.
3. Local modifiers on the instance.
4. Attached instance modifiers.
5. External modifiers from other entities or systems.
6. Zone or context modifiers.
7. Preset-specific composition rules.

Example:

```text
Villager base attack: 1
Sword attachment attack bonus: +3
Rage status modifier: +2
Rainy zone context modifier: -1
Effective attack: 5
```

The calculation order must eventually be explicit and deterministic. For now, the important decision is that modifiers are layered and inspectable rather than hidden inside one mutable value.

## Attachments And Equipment

Attachments and equipment should usually be their own instances.

Example:

```text
Villager Instance
  definition: villager.base
  attached instances:
    - iron_sword instance
    - leather_helmet instance
```

The villager does not become a different opaque object just because a sword is attached. A composition handler reads the relationships and calculates effective values such as attack, job, speed, or available actions.

Benefits:

- Attachments can have their own instance IDs and lifecycle.
- Attachments can move, break, transform, or be removed.
- Attachments can have effects of their own.
- Debugging can show exactly where a modifier came from.

## Tokens And Generated Entities

Tokens should still have definitions.

This can work through:

- Authored token definitions.
- Generated system definitions.
- Preset-provided definitions.
- Temporary definitions created by game setup.

Avoid definitionless runtime entities. Even a tiny system definition is useful for debugging and replay.

## Transformation And Replacement

Transformation needs explicit game-specific semantics.

Possible transformation models:

- Same instance, new definition reference.
- Same instance, transformation modifier layered over base definition.
- New instance that replaces the old instance.
- Attached overlay instance that changes effective values.

No single choice should be built into the entire framework. The base framework should expose the primitives needed for presets or games to define their own transformation semantics.

## Composition Handlers

A composition handler calculates effective behavior from related instances and modifiers.

Examples:

- Equipment handler computes equipped stats and abilities.
- Stack recipe handler computes whether a physical stack can craft something.
- Aura handler computes continuous modifiers from nearby or attached cards.
- Zone handler computes restrictions or bonuses from current location.
- Transformation handler computes current definition overlay.

Composition handlers may be:

- Core utilities for common patterns.
- Preset-specific systems.
- Game-specific systems.

They should be deterministic and traceable.

## Store And Entities Relationship

The canonical storage model is not decided yet.

Open options:

- Scaffold Entities as canonical runtime instances.
- Scaffold States as canonical runtime state.
- Entities for definitions and authoring, Store for simulation state.
- Store for global/session state, Entities for per-entity variable state.
- A custom model that borrows from both.

The next design work should avoid depending on one answer too early. Docs should speak in terms of concepts unless they are specifically about package integration.

## What The Framework Should Not Assume Yet

- That all games have decks and hands.
- That zones replace instances.
- That every card has cost, attack, and health.
- That all effects are card effects.
- That all entities are visible.
- That all state is multiplayer replicated.
- That presentation objects are entities.
- That a Unity `GameObject` is the same thing as a simulation entity.

## Useful Invariants

These are good early invariants:

- Every entity is represented by an instance.
- Every instance has an instance ID or reference value for lookups.
- Every entity references a base definition.
- Zone membership is a relationship or state, not the instance itself.
- Effective values should be explainable from base values plus modifiers.
- Attachments and equipment are usually separate instances.
- Generated entities still use definitions.
- Game-specific systems can replace, transform, destroy, or recreate instances explicitly.

## Next Questions

This model pushes the next important questions into the Zone and State docs:

- Is a zone always an entity instance, or can some zones be lightweight session records?
- Is zone membership a variable, relationship, Store slice, or combination?
- How does ordered or grouped arrangement work inside zones?
- How do visibility and ownership interact?
- What exact comparison should we make between Scaffold Entities and Scaffold States?

