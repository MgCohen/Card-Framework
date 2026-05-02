---
type: feature
status: settled
tags: [#feature, #effects]
---

# effects — feature

## Purpose
Effects with lifetime: async/suspending dispatch, replacement and prevention, and continuous/ongoing effects. Owns the layered model that lets cards alter the action pipeline without the pipeline knowing about each card.

## Boundaries
- **Owned:** async-decision suspension model, replacement-priority and prevention rules, continuous/ongoing effect semantics, effect lifetimes.
- **Not owned:** the pipeline these effects modify (`features/actions/`), trigger registration (`features/triggers/`), the engine loop they live within (`features/engine/`).

## Composition
- `features/effects/models/01-async-dispatch.md` — async decisions and suspension (formerly doc 22).
- `features/effects/models/02-replacement-prevention.md` — replacement and prevention effects (formerly doc 23).
- `features/effects/models/03-ongoing-effects.md` — continuous and ongoing effects (formerly doc 24).

## Public surface
Effect registration, async-decision continuation handles, replacement/prevention hooks, continuous-effect registration and shutdown. Game-side rules code registers effects through these.

## Dependencies
- **Upstream:** `features/triggers/`, `features/actions/`, `features/engine/`.
- **Downstream:** game-side rules code.

## Settled decisions
ADRs in `features/effects/decisions/` (backfilled in phase 5):
- async layer hierarchy, suspension model, replacement priority.

## Open
See `ledgers/OPEN-ITEMS.md` for effects-related questions.
