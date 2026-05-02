---
type: feature
status: settled
tags: [#feature, #presentation]
---

# presentation — feature

## Purpose
The reactive layer: how UI, 2D, or 3D card views consume the AvailableActions snapshot and react to history events. Owns the presentation loop that translates snapshot state into card affordances and animation handoffs.

## Boundaries
- **Owned:** presentation loop, snapshot-to-affordance mapping, animation handoff hooks.
- **Not owned:** rules truth (always lives in the simulation/engine), input gestures themselves (those become PlayerIntents handled by `features/actions/`).

## Composition
- `features/presentation/models/01-presentation-loop.md` — presentation loop and AvailableActions flow (formerly doc 27).

## Public surface
Snapshot subscribers, history-event subscribers, animation callback registration. Game-side card prefabs read from these.

## Dependencies
- **Upstream:** `features/actions/`, `features/engine/`.
- **Downstream:** game-side card prefabs, UI scenes.

## Settled decisions
ADRs in `features/presentation/decisions/` (none yet — backfill not in scope per §4 of MIGRATION-GUIDE.md).

## Open
See `ledgers/OPEN-QUESTIONS.md` for presentation-related questions.
