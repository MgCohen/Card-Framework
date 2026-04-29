---
type: adr
status: accepted
last-updated: 2026-04-29
tags: [#adr, #effects, #async, #suspension]
---

# ADR-0002 — Suspension model: explicit decision IDs, snapshot-driven resume

Date: 2026-04-29
Context-doc: features/effects/notes/01-async-dispatch.md
Proposal: features/effects/notes/01-async-dispatch.md

## Context
When an effect needs a player choice mid-resolution, the simulation must suspend and resume cleanly across snapshot/restore and replay. Implicit continuations break replay and make per-player views ambiguous.

## Options considered
- A: Implicit continuations via captured async state — rejected because the captured state lives outside `GameState` and breaks replay.
- B: Block the simulation thread until decision arrives — rejected because it locks presentation and networking.
- C: Explicit decision IDs threaded through state; effect publishes a `DecisionPrompt`-scoped snapshot, suspends, and resumes when a matching player intent arrives. Chosen.

## Decision
Suspension is modeled as a typed decision in `GameState`. Effects write a decision-scoped `AvailableActionsSnapshot` via `IAvailableActions`, suspend, and resume on dispatch of a matching command. Decision identity is explicit and replayable.

## Consequences
- Enables snapshot/restore and replay across decisions.
- Enables transparent multiplayer routing of decisions to the right actor.
- Constrains: every decision must carry an explicit ID; no anonymous awaits.
