---
type: adr
status: accepted
last-updated: 2026-04-29
tags: [#adr, #triggers, #fold]
---

# ADR-0001 — Trigger fold semantics

Date: 2026-04-29
Context-doc: features/triggers/models/01-trigger-fold.md
Proposal: features/triggers/models/01-trigger-fold.md

## Context
When multiple triggers fire on one event, they need to be aggregated into a single decision (or sequence of dispatches) without callers writing event-loop code. We needed a model that handles ordering, priority, and replacement uniformly.

## Options considered
- A: Synchronous broadcast, first listener wins — rejected because it has no story for replacement, prevention, or priority.
- B: Per-event subscription lists with manual aggregation — rejected because every event reinvents the loop.
- C: A *fold* over registered triggers, producing an aggregated `TriggerResult` consumed by the engine. Chosen.

## Decision
Triggers are aggregated by a fold (`ITriggerPipeline`/`FilterTriggers`) that produces a single `TriggerResult`. Priority and replacement/prevention are expressed as fold steps. The engine consumes the result; effects don't write the loop.

## Consequences
- Enables uniform handling of replacement, prevention, and priority.
- Constrains: trigger authors think in terms of fold contributions, not direct dispatch.
