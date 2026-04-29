---
type: adr
status: accepted
last-updated: 2026-04-29
tags: [#adr, #engine, #gamerules, #state]
---

# ADR-0002 — GameRules holds zero game-affecting state

Date: 2026-04-29
Context-doc: features/engine/notes/02-gamerules-contract.md
Proposal: features/engine/notes/02-gamerules-contract.md

## Context
If `GameRules` carries game-affecting state, snapshot/restore breaks, replays diverge, and per-player views become impossible to disentangle. We need a clean separation between the rule (configuration) and gameplay memory (state).

## Options considered
- A: Allow rules to cache derived state for performance — rejected because cache invalidation becomes a global concern and breaks state pattern guarantees.
- B: Allow rules to hold transient buffers (e.g. queued events) — rejected because it duplicates `IIntakeQueue`.
- C: Zero game-affecting state on rules; gameplay memory lives only in `GameState` (Scaffold States). Chosen.

## Decision
The rule may hold injected services and pure configuration only. All gameplay memory lives in `GameState`. The rule reads state through `IGameStateProvider` and writes by dispatching commands or by calling `IAvailableActions`.

## Consequences
- Enables deterministic replay, snapshot/restore, and trivial per-player view derivation.
- Constrains: any "memo" the rule wants must round-trip through state.
