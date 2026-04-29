---
type: feature
status: settled
last-updated: 2026-04-29
tags: [#feature, #engine]
---

# engine — feature

## Purpose
The orchestrator: evaluation loop, the GameRules contract, and the snapshot production pipeline. Hosts the rules brain that the actions pipeline calls into. Owns the loop that drains commands, fans triggers, and produces the AvailableActions snapshot.

## Boundaries
- **Owned:** evaluation loop, GameRules class shape and zero-state requirement, snapshot production pipeline, snapshot-in-state location.
- **Not owned:** the action pipeline shape (`features/actions/`), trigger/effect mechanics (`features/triggers/`, `features/effects/`), entity model (`features/entities/`).

## Composition
- `features/engine/models/01-evaluation-loop.md` — orchestrator and evaluation loop (formerly doc 20).
- `features/engine/models/02-gamerules-contract.md` — GameRules contract: shape, zero-state, services (formerly doc 25).
- `features/engine/models/03-snapshot-pipeline.md` — snapshot production pipeline (formerly doc 29).

## Public surface
GameRules class, evaluation loop entry points, snapshot writer (`IAvailableActions`), state-pattern services. The actions pipeline calls into GameRules; presentation reads the snapshot from state.

## Dependencies
- **Upstream:** `features/entities/`, `features/zones/`, `features/triggers/`, `features/effects/`.
- **Downstream:** `features/actions/`, `features/presentation/`.

## Settled decisions
ADRs in `features/engine/decisions/` (backfilled in phase 5):
- GameRules as class with services, GameRules zero-state, snapshot in state, snapshot writer = IAvailableActions, state pattern, MTG-SBA known difference.

## Open
See `ledgers/OPEN-QUESTIONS.md` for engine-related questions.
