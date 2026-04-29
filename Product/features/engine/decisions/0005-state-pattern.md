---
type: adr
status: accepted
last-updated: 2026-04-29
tags: [#adr, #engine, #state, #scaffold]
---

# ADR-0005 — State pattern: immutable, redux-like, on Scaffold States

Date: 2026-04-29
Context-doc: features/engine/models/01-evaluation-loop.md
Proposal: features/engine/models/01-evaluation-loop.md, features/entities/models/01-definition-instance-model.md

## Context
The framework needs a state model that supports replay, per-player views, deterministic networking, and zero state held by the rule. We had Scaffold States available with a redux-like immutable model.

## Options considered
- A: Mutable POCO state mutated in handlers — rejected because it breaks replay and forces deep cloning for views.
- B: Custom event-sourced state — rejected because it duplicates Scaffold infrastructure for no incremental benefit.
- C: Scaffold States: `GameState` immutable, redux-like; mutators react to commands. `IAvailableActions` writes via internal mutator. Chosen.

## Decision
`GameState` is immutable. Mutators react to commands. The Store is canonical; entities are accessed via Entities-shaped handles through `com.scaffold.entities.states`. `IAvailableActions` writes via an internal mutator like every other state slice.

## Consequences
- Enables deterministic replay, snapshot/restore, and per-player views.
- Constrains: every state change must be expressible as a mutator over a command; ad-hoc mutation isn't possible.
- Reverts/replaces: earlier mutable-POCO sketches in doc 8.
