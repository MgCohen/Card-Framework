---
type: model
status: settled
references: [features/effects/models/05-handler-context.md, features/engine/models/02-gamerules-contract.md, features/entities/models/02-runtime-surface.md]
tags: [#engine, #state, #slices]
---

# Framework Slice Tier — model

## What it is

The framework's tier conventions over the underlying state primitive ([`features/state/state.md`](features/state/state.md)). State itself — `Store`, `BaseState`, `IStateScope`, `Mutator<TState, TPayload>`, subscriptions, atomic batches — is owned by the `state` feature; this Model layers framework-specific rules on top: a two-tier split (**framework-owned** auto-seeded at `Build` and writable only by framework services; **game-authored** declared via `ISliceProvider` and registered with mutators in the game's installer), the catalog index of framework-owned slices, and the external-keying convention.

## Contracts

### Two tiers

| Tier | Who defines | Who seeds | Who writes |
|---|---|---|---|
| **Framework-owned** | Framework | `IGameFactory.Build` auto-seeds (see [`engine/05`](features/engine/models/05-game-factory.md)) | Framework-internal services / mutators only |
| **Game-authored** | Game author | `ISliceProvider.GetInitialSlices()` on the relevant Definition (see [`engine/07`](features/engine/models/07-game-definition.md)); manual `MatchSetup.InitialSlices` overlay | Game-registered `Mutator<TState, TPayload>` |

Framework-owned slices are invisible to the game author at bootstrap — they are always present, always written by the framework, and readable by anyone via `IStateScope`. Including a framework-owned slice in `MatchSetup.InitialSlices` throws at validation. The current catalog is not a fixed ceiling; it reflects what the framework needs today and will grow or shrink as the framework matures.

### Framework-owned slices

The framework auto-seeds three slices at `Build()`. Each slice's schema lives with the system that produces it; this Model only catalogs them. Game authors never declare any of these in `MatchSetup.InitialSlices`.

| Slice | Produced by | Schema canonical home |
|---|---|---|
| `AvailableActionsState` | `IAvailableActions` service | [`engine/03 §The AvailableActionsState slice`](features/engine/models/03-snapshot-pipeline.md) |
| `EngineState` | Engine internals (drain start/stop, lifecycle handlers) | [`engine/01 §The EngineState slice`](features/engine/models/01-evaluation-loop.md) |
| `HistoryState` | Engine history mutator (post-`pipeline.Execute`) | [`actions/04-history-envelope.md` §The HistoryState slice](features/actions/models/04-history-envelope.md) |

New framework-owned slices land at their producing system's canonical home and are added as a row above.

### External keying

Slices don't carry their own primary key. The factory keys per-instance slices externally using the spawned instance's `SelfRef` (per-zone, per-card, per-player) or unkeyed (game-singleton). Reference-shaped fields inside slice values are *data*, not keys. This is a framework convention, not a Scaffold-level rule — the underlying `Store` is happy to key however the caller asks; the framework standardizes on external keying so per-instance slices can be enumerated by Definition kind.

## Invariants

1. **Framework-owned slices are auto-seeded at `Build()`.** Game authors never yield them in `MatchSetup.InitialSlices`; doing so throws at validation.
2. **Framework-owned slices are written only by framework services.** No game-authored mutator may target `AvailableActionsState`, `EngineState`, or `HistoryState` directly.
3. **Game-authored slices register via `ISliceProvider`.** Per-instance slices come from the relevant Definition's `GetInitialSlices()` at spawn; game-singleton slices come from `GameDefinition.GetInitialSlices()`.
4. **Slices don't carry their own primary key.** Storage is keyed externally by the factory at `instance.SelfRef`. Reference-shaped fields inside slices are data, not keys.

## Decisions

*n/a — no elevated ADR.* (Tier conventions are descriptive — the underlying state pattern is settled in [`features/state/state.md`](features/state/state.md). This Model only layers framework rules on top.)

## Examples

The Pile Counter sample ([`support/samples/pile-counter.md`](support/samples/pile-counter.md)) exercises:

- Framework-owned `AvailableActionsState` written via `Actions.Set` from `Evaluate` (auto-seeded; never declared by the game).
- Framework-owned `HistoryState` appended automatically as envelopes close.
- Framework-owned `EngineState.Lifecycle` flipped via the framework `SetEngineLifecyclePayload` mutator from `StartMatchHandler` / `EndMatchHandler`.
- Game-authored per-zone slice (`PileState`) keyed externally at `pile.SelfRef`, sourced from `PileDefinition.GetInitialSlices()` at spawn.
- Game-authored singleton (`LastMoveState`) keyed unmapped, sourced from `PileCounterGameDefinition.GetInitialSlices()`.

For the Scaffold-level mechanism (read surface, mutator pattern, subscriptions, batches), see [`features/state/state.md`](features/state/state.md).

## Open follow-ups

- **Slice catalog growth** — as features land (zones, entities bridge), new game-authored slice types will emerge and the framework slice count may change. Update the catalog index above when a new framework-owned slice lands.
