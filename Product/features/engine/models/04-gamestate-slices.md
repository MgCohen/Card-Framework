---
type: model
status: settled
last-updated: 2026-05-01
builds-on: [features/effects/models/04-effect-flow.md, features/engine/models/02-gamerules-contract.md]
tags: [#engine, #state, #slices]
---

# GameState Slice Catalog

This document pins the catalog of state slices the framework owns, the pattern for game-authored slices, and the rules for how each tier is seeded and accessed. It resolves checkpoint task #2 and the `GameState slice catalog` open question in `OPEN-QUESTIONS.md`.

Builds on:

- [features/effects/models/04-effect-flow.md](features/effects/models/04-effect-flow.md) — Scaffold `IStateScope`/`Store` binding, mutation discipline
- [features/engine/models/02-gamerules-contract.md](features/engine/models/02-gamerules-contract.md) — `GameRules` service injection, `IAvailableActions`

---

## 1. No Single GameState Record

State lives as multiple typed slices in a Scaffold `Store`. There is no `GameState` class, no `state.Piles`, no `state.LastMove`. Every read goes through:

```csharp
scope.Get<TState>()
scope.Get<TState>(reference)
scope.GetAll<TState>()
```

where `TState : BaseState`. This is settled in [04-effect-flow §4.1](features/effects/models/04-effect-flow.md).

---

## 2. Two Tiers

| Tier | Who defines | Who seeds | Who writes |
|---|---|---|---|
| **Framework-owned** | Framework | `GameEngineBuilder.Build()` auto-seeds | Framework-internal services / mutators only |
| **Game-authored** | Game author | `builder.WithInitialSlices(...)` | Game-registered `Mutator<TState, TPayload>` |

Framework-owned slices are invisible to the game author at bootstrap — they are always present, always written by the framework, and readable by anyone via `IStateScope`. The current catalog is not a fixed ceiling; it reflects what the framework needs today and will grow or shrink as the framework matures.

---

## 3. Framework-Owned Slices

### 3.1 `AvailableActionsState`

```csharp
public sealed record AvailableActionsState(
    AvailableActionsSnapshot Snapshot) : BaseState;
```

- **Purpose.** Holds the current available-actions snapshot published by `GameRules.OnQueueDrained`.
- **Written by.** `IAvailableActions` service (framework-internal mutator). Game authors write through the service, never directly.
- **Read by.** Gateway (pre-dispatch check), presentation layer, `GameRules.OnQueueDrained` for re-build.
- **Seeded.** `AvailableActionsState(AvailableActionsSnapshot.Empty)` at `Build()`.

### 3.2 `EngineState`

```csharp
public sealed record EngineState(
    bool           IsRunning,
    GameCommand?   CurrentCommand) : BaseState;
```

- **Purpose.** Tracks engine execution state — whether a drain is in progress and which command is currently executing. Load-bearing for inspection, debugging, and future replay/save support.
- **Written by.** Engine internals (updated on drain start/stop and before/after each `pipeline.Execute`). Not writable by game authors.
- **Read by.** Tooling, debugger, and any game logic that needs to know "is the engine currently draining?"
- **Seeded.** `EngineState(IsRunning: false, CurrentCommand: null)` at `Build()`.

### 3.3 `HistoryState`

```csharp
public sealed record HistoryState(
    IReadOnlyList<HistoryEnvelope> Envelopes) : BaseState;
```

- **Purpose.** Holds the ordered envelope log — every command that has executed, with its cause, actor, and result entries. Load-bearing for replay, multiplayer transport, and the presentation history loop.
- **Written by.** Engine-internal history mutator (fires after every `pipeline.Execute` call, appending the closed envelope). Not writable by game authors.
- **Read by.** Presentation layer (history loop), networking (transport), replay tooling.
- **Seeded.** `HistoryState(ImmutableList<HistoryEnvelope>.Empty)` at `Build()`.
- **Note.** `HistoryEnvelope` is currently pseudocode (see [OPEN-QUESTIONS.md — HistoryEnvelope typed definition](ledgers/OPEN-QUESTIONS.md)). The slice exists and is seeded; its payload type is pinned when that open question resolves.

---

## 4. Game-Authored Slice Pattern

Game authors define slices as immutable records:

```csharp
public sealed record PilesState(
    IReadOnlyDictionary<PileId, Pile> Piles) : BaseState;

public sealed record LastMoveState(LastMove? Move) : BaseState;
```

Rules:
- **One slice, one concern.** Each slice owns exactly one piece of game state.
- **Immutable.** All mutations produce a new record via `with { }` or a helper method (e.g. `WithCardMoved`).
- **No cross-slice mutation.** A mutator owns one slice's output. It may read sibling slices through the `IStateScope scope` parameter on `Change`, but it never writes them.

Slices are registered at bootstrap via mutators:

```csharp
builder
    .WithInitialSlices(
        new PilesState(initialPiles),
        new LastMoveState(Move: null))
    .WithMutators(
        new PileMoveMutator(),    // Mutator<PilesState,    MoveCardPayload>
        new LastMoveMutator());   // Mutator<LastMoveState, MoveCardPayload>
```

The engine registers framework-owned slices internally at `Build()`. Game authors never seed `AvailableActionsState`, `EngineState`, or `HistoryState`.

---

## 5. Reading State

All consumers read state through `IStateScope.Get<TState>()` regardless of tier:

```csharp
// GameRules reading game-authored slice
var piles = state.Get<PilesState>().Piles.Values;

// Trigger condition reading framework-owned slice
var snap = scope.State.Get<AvailableActionsState>().Snapshot;

// Presentation reading history
var log = scope.Get<HistoryState>().Envelopes;
```

There is no special accessor for framework slices. `IStateScope` is the uniform read surface for everything.

---

## 6. Invariants

1. **No single GameState record.** State is a Scaffold `Store`; reads always go through `scope.Get<TState>()`.
2. **Framework-owned slices are auto-seeded at `Build()`.** Game authors never yield them in `WithInitialSlices`.
3. **Framework-owned slices are written only by framework services.** No game-authored mutator may target `AvailableActionsState`, `EngineState`, or `HistoryState` directly.
4. **Game-authored slices follow the same pattern.** `BaseState` subclass, immutable record, registered at bootstrap via `WithInitialSlices` + `WithMutators`.
5. **Uniform read surface.** `IStateScope.Get<TState>()` is the only read path for all tiers — no convenience accessors, no named properties.

---

## 7. Open Follow-Ups

- **`HistoryEnvelope` typed definition** — the concrete C# record for `HistoryEnvelope` (fields: `envelopeId`, `originCommandId`, `actor`, `entries[]`) is pseudocode. Pin when replay or multiplayer transport is designed. ([OPEN-QUESTIONS.md](ledgers/OPEN-QUESTIONS.md))
- **`EngineState` surface** — whether `IsRunning`/`CurrentCommand` are the right fields, or whether additional engine-internal state (pending queue snapshot, awaiter state) belongs here, is deferred to the engine implementation pass.
- **Slice catalog growth** — as features land (lifecycle, zones, entities bridge), new game-authored slice types will emerge. The framework slice count may also change. Update this doc when a new framework-owned slice is formally added.

---

## 8. Related

- [features/effects/models/04-effect-flow.md §4.1](features/effects/models/04-effect-flow.md) — `IStateScope` / `Store` binding
- [features/engine/models/02-gamerules-contract.md](features/engine/models/02-gamerules-contract.md) — `IAvailableActions` service, `GameRules` constructor
- [features/engine/models/03-snapshot-pipeline.md](features/engine/models/03-snapshot-pipeline.md) — snapshot production, `IAvailableActions.Set`
- [support/samples/pile-counter.md](support/samples/pile-counter.md) — working example of game-authored slices (`PilesState`, `LastMoveState`)
- [ledgers/OPEN-QUESTIONS.md](ledgers/OPEN-QUESTIONS.md) — `HistoryEnvelope` typed definition
