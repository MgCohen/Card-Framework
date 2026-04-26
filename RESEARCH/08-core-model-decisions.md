# Core Model Decisions

This document answers the Core Model questions from [Open Questions](06-open-questions.md). These are early research decisions, not final implementation contracts.

## Status

The direction for the core model is now:

- Do not enforce a hard Scaffold boundary yet.
- Start with key-value variables; do not hardcode variable names into the framework.
- Let later exploration decide whether Store, Entities, a hybrid, or a custom project model should become canonical.
- Card instances are independent from zone membership.
- Every entity should have a base definition.
- Instances are definitions plus modifiers, with attachments and equipment modeled as their own instances.

## Dependency Boundary

Question: should the simulation core depend directly on Scaffold packages, or should Scaffold be used only in Unity-facing adapters?

Decision: no enforced boundary yet.

Scaffold should be allowed wherever it genuinely fits:

- In simulation, if the package model supports deterministic rules cleanly.
- In Unity-facing authoring or presentation, if runtime simulation benefits from a separate model.
- In both, if conversion and shared IDs stay simple.
- In neither, if the framework needs a custom model.

Reasoning:

At this stage, the important rule is not "pure core at all costs." The important rule is to avoid forcing an architecture before we know whether Scaffold Entities and Scaffold States help or fight each other.

Implication:

Early docs should describe boundaries by responsibility, not by package dependency. We can still keep presentation, rules, authoring, and networking conceptually separated without deciding package references yet.

Confidence: medium.

Follow-up:

Validate whether `com.scaffold.entities` can represent runtime card state comfortably, and whether `com.scaffold.states` should wrap, mirror, or replace that state.

## Value Model

Question: should card/entity values be stored primarily as flexible variables/tags, typed state records, or a hybrid?

Decision: start with simple key-value variables, regardless of whether the implementation uses Scaffold Entities, Store, or a custom model.

The starting model should assume entities can carry variable entries shaped conceptually like:

```text
VariableKey -> VariableValue
```

Examples of possible keys a game or preset might define:

- `cost`.
- `attack`.
- `health`.
- `energy`.
- `zone`.
- `owner`.
- `controller`.
- `visibility`.
- `stack_count`.
- `recipe_progress`.
- `exhausted`.

These are examples, not required built-in fields.

Reasoning:

Key-value variables are flexible enough for the early framework and align with Scaffold Entities' definition/instance/modifier model without requiring the framework to know every possible game value ahead of time. This keeps the model data-driven whether we adopt Scaffold Entities directly, wrap it, or create a custom representation.

Implication:

Typed concepts can exist as optional convenience wrappers, validators, schemas, or preset conventions over key-value variables. They should not become required hardcoded fields in the base framework.

Confidence: medium.

Follow-up:

Define how variable keys are registered, typed, validated, displayed, and versioned without making the base framework depend on a fixed variable list.

## Canonical State

Question: what is the canonical source of truth: entity instances, Store slices, or a composed model where each owns different concerns?

Decision: unresolved, but keep Entities and Store both on the table.

If Store and Entities work well together, use them together. If they fight, choose one canonical path or create a custom project-specific style.

Possible directions:

- Entities are canonical, Store observes or indexes them.
- Store is canonical, entity instances are a convenient authoring/runtime facade.
- Definitions use Scaffold Entities, runtime state uses Store.
- A custom card framework model borrows ideas from both.

Reasoning:

This is the most important Core Model uncertainty. Deciding too early could either duplicate state or make simple card behavior hard to express.

Implication:

Upcoming docs should describe concepts without pretending the storage implementation is decided.

Confidence: low.

Follow-up:

Design a small scenario later and model it both ways:

- Card definition with base cost and attack.
- Runtime card instance in hand.
- Cost modifier attached by an aura/equipment.
- Zone movement from hand to play area.
- History emitted for the move and value changes.

## Instances And Zones

Question: how should an instance behave when a card changes zones?

Decision: do not model "identity" as a separate concept. The instance is the durable runtime object; its instance ID is only a lookup/reference value.

A card instance remains the same instance when it moves between zones unless a game-specific rule or effect explicitly replaces it, transforms it, destroys it, or recreates it.

Reasoning:

The framework should not bake Magic's zone-change replacement behavior into all games. Some games need that behavior, but many do not. Zone changes should be observable facts, not instance replacement by default.

Implication:

Zone movement can trigger effects, modifiers, visibility changes, or game-specific replacement rules. The base system should preserve the same instance by default.

Confidence: high.

Follow-up:

The Zone Model doc should include optional replacement policies for games that need zone movement to create or replace instances.

## Entity Definitions

Question: do all entities have definitions, or can runtime-only entities exist without definitions?

Decision: all entities should have a base definition.

This applies broadly:

- Cards.
- Tokens.
- Zones.
- Players or actors, if represented as entities.
- Relics.
- Equipment.
- Resources.
- Villagers.
- Enemies.
- Card packs.
- Temporary helper objects, if they need to be referenced as instances.

Reasoning:

Definitions make authoring, debugging, save/load, replay, networking, and tool inspection easier. Even generated runtime objects can point to a generated or system definition.

Implication:

The framework should support lightweight system definitions for tokens and temporary entities instead of allowing definitionless entities.

Confidence: high.

Follow-up:

Define the minimum fields required for a base definition.

## Instances, Modifiers, Attachments, And Composition

Question: how do attachments, equipment, stacked cards, tokens, and transformed cards relate to a base card instance?

Decision: an instance is a base definition plus modifiers. Attachments and equipment are usually separate instances from their own definitions.

A card instance should not absorb every attached thing into itself. Instead:

- A sword attached to a villager is an equipment instance attached to a villager instance.
- An aura modifier may be an effect or relationship from another instance.
- A token is an instance created from a token definition.
- A transformed card may be the same instance with a transformation modifier, or a game-specific replacement with a new definition and new instance.
- A stack may be a relationship between instances, a zone-like container, or a game-specific composed object.

Reasoning:

Composition keeps instances explicit. It also supports Stacklands-like physical composition, RPG equipment, TCG auras, attached cards, and relic/passive systems without making the base card object enormous.

Implication:

The framework needs composition handlers that can calculate effective values from:

- The base definition.
- Local instance modifiers.
- Attached instances.
- External effects.
- Zone or context modifiers.
- Game-specific systems.

Confidence: medium-high.

Follow-up:

The Entity Model doc should distinguish base values, local modifiers, external modifiers, attachments, and effective values.

## Revised Working Model

The current working model is:

```mermaid
flowchart TB
  definition[Base Definition] --> instance[Runtime Instance]
  instance --> localMods[Local Modifiers]
  attachment[Attached Instance] --> composition[Composition Handler]
  localMods --> composition
  instance --> composition
  zone[Current Zone / Relationships] --> composition
  external[External Effects] --> composition
  composition --> effective[Effective Values]
```



The exact storage for this model is still open. The conceptual model is accepted enough to inform the next documents.

## Decisions To Carry Forward

- Do not require a pure non-Scaffold simulation boundary yet.
- Use key-value variable-based entities as the first mental model.
- Keep canonical state open until Store and Entities are compared.
- Preserve the same instance across zone changes by default.
- Require every entity to have a definition.
- Model attachments/equipment/composition as relationships between instances, not as hidden fields on one card.

