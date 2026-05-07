---
type: adr
status: accepted
tags: [#adr, #engine, #gamerules, #state]
---

# ADR-0002 — GameRules holds zero gameplay state

Date: 2026-04-29
Context-doc: features/engine/models/02-gamerules-contract.md
Proposal: features/engine/models/02-gamerules-contract.md

## Amendment — 2026-05-07 (triggers + dispatch design pass)

The original wording said "zero **game-affecting** state." Refining: the rule holds zero **gameplay** state — gameplay memory (last move, phase, score, etc.) lives in Scaffold state slices. Engine-internal plumbing (most notably the pending command queue) is **not** gameplay state; that's framework wiring, and where it physically lives (engine-private field, framework-owned slice, or runtime base class) is an engine-implementation detail, not a violation of this ADR.

The original "rejected option B" referenced `IIntakeQueue`. Per ADR-0006, `IIntakeQueue` is deleted — the command queue is engine-private FIFO, not a runtime service. The spirit of "rules don't hold queued events as gameplay memory" is unchanged; the mechanism reference is stale. Game-state buffers that *should* round-trip through snapshots (e.g., MTG stack zone, Hearthstone trigger queue) live as game-authored state slices, not as fields on the runtime.

## Context
If `GameRules` carries gameplay state, snapshot/restore breaks, replays diverge, and per-player views become impossible to disentangle. We need a clean separation between the rule (configuration) and gameplay memory (state slices in the Scaffold `Store`).

## Options considered
- A: Allow rules to cache derived state for performance — rejected because cache invalidation becomes a global concern and breaks state pattern guarantees.
- B: Allow rules to hold transient buffers (e.g. queued events) as gameplay memory — rejected because gameplay buffers belong in state slices (snapshot-roundtrippable), not on the runtime. (Original wording cited `IIntakeQueue`; that type is gone per ADR-0006, but the principle stands.)
- C: Zero gameplay state on rules; gameplay memory lives only in `GameState` (Scaffold States). Chosen.

## Decision
The rule may hold injected services and pure configuration only. All gameplay memory lives as Scaffold state slices in the `Store`. The rule reads through Scaffold `IStateScope` (`State.Get<TState>()`) and writes by dispatching commands (whose handlers call `Store.Execute(payload)`) or by calling `IAvailableActions`. See [04-effect-flow §4.1–§4.2](features/effects/models/04-effect-flow.md).

Engine-internal plumbing (the pending command queue, drain-cycle running flag, etc.) is not gameplay state and does not violate this ADR — it's framework wiring whose physical home is an engine-implementation choice.

## Consequences
- Enables deterministic replay, snapshot/restore, and trivial per-player view derivation.
- Constrains: any *gameplay* memo the rule wants must round-trip through state.
- Permits: engine-internal plumbing (queue, flags, etc.) lives wherever the engine implementation prefers, since it isn't gameplay memory.
