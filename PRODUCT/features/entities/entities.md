---
type: feature
status: settled
last-updated: 2026-04-29
tags: [#feature, #entities]
---

# entities — feature

## Purpose
Definitions and instances for everything the framework treats as a "thing": cards, players, attachments, equipment, modifiers. Establishes the definition→instance separation and the per-entity variable model that downstream features (zones, actions, effects) build on.

## Boundaries
- **Owned:** definition shape, instance shape, per-entity variables/modifiers, attachments-as-instances pattern.
- **Not owned:** zone membership (lives in `features/zones/`), legality (lives in `features/actions/`), runtime effects on entities (lives in `features/effects/`).

## Composition
- `features/entities/notes/01-definition-instance-model.md` — canonical Model: Definition / Instance / per-entity variables / Scaffold-bridge approach.

## Public surface
Definition and Instance types (with IDs and variable handles), Entity-shaped accessors over the canonical Store. Other features address entities through these handles, never as raw fields.

## Dependencies
- **Upstream:** none — entities are foundational.
- **Downstream:** `features/zones/`, `features/actions/`, `features/effects/`, `features/triggers/`, `features/engine/`, `features/presentation/`.

## Settled decisions
ADRs (backfilled in phase 5) live in `features/entities/decisions/`. Provenance for the original "core model" decisions (formerly doc 8) is folded into `01-definition-instance-model.md`.

## Open
See `general/OPEN-QUESTIONS.md` for entity-related questions.
