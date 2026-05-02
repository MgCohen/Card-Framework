---
type: adr
status: accepted
tags: [#adr, #engine, #snapshot, #state]
---

# ADR-0003 — AvailableActionsSnapshot lives in GameState

Date: 2026-04-29
Context-doc: features/engine/models/03-snapshot-pipeline.md
Proposal: features/presentation/models/01-presentation-loop.md, features/engine/models/03-snapshot-pipeline.md

## Context
The snapshot of currently-legal actions has to flow to presentation, network, and replay. Where does the snapshot *live*? In the rule? In a side channel? Or in canonical state?

## Options considered
- A: Side-channel observable owned by the rule — rejected because it bypasses the state pattern, breaking replay and per-player views.
- B: Recompute on every read by presentation — rejected because cost is unbounded for complex games and tying it to a frame loop hides bugs.
- C: `GameState.AvailableActions: AvailableActionsSnapshot` (Scope + typed templates), written via the `IAvailableActions` service. Chosen.

## Decision
The snapshot lives in `GameState` as `AvailableActions`. Presentation and other consumers read it via the same state subscription mechanism they use for everything else. Writes go through `IAvailableActions`.

## Consequences
- Enables uniform observation, replay, and snapshot-driven UIs.
- Constrains: every snapshot mutation must round-trip through the writer service (no direct `GameState.AvailableActions = …`).
