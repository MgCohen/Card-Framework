---
type: adr
status: accepted
tags: [#adr, #engine, #snapshot, #service]
---

# ADR-0004 — Snapshot writer is the IAvailableActions service

Date: 2026-04-29
Context-doc: features/engine/models/03-snapshot-pipeline.md
Proposal: features/engine/models/03-snapshot-pipeline.md

## Context
The snapshot has multiple potential writers — the runtime (the typical default site), effects mid-execution (decision pickers), and the engine (auto-publish?). Multiple write paths fragment ordering and introduce races.

## Options considered
- A: Engine auto-publishes after every command — rejected because auto-publication can't represent intermediate decision-scoped snapshots, and it forces work the runtime may not want.
- B: Runtime writes directly to `GameState` — rejected because it leaks the state-pattern boundary.
- C: Single `IAvailableActions` service exposed to both the runtime and effects (`Set` / `Append` / `Clear`); engine never auto-publishes. Chosen.

## Decision
`IAvailableActions` is the single write path. The runtime calls `Actions.Set(...)` from inside `Evaluate` whenever it deems appropriate (per [ADR-0006](decisions/0006-two-pipeline-architecture.md), `OnQueueDrained` is gone — typical moment is when the runtime's own state-based follow-ups are exhausted). Effects override mid-execution for decision-scoped snapshots. Engine doesn't publish.

## Consequences
- Enables single ordering invariant: snapshot mutations are serialized through one service.
- Enables effects to publish decision-scoped snapshots without bespoke wiring.
- Constrains: one-shot games (Stacklands) must `Set` once at bootstrap; phase-driven games (MTG) `Set` on phase enter / priority grant.
