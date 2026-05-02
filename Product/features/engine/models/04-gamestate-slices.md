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
- **Note.** `HistoryEnvelope` is currently pseudocode (see [OPEN-QUESTIONS.md — HistoryEnvelope typed definition](../ledgers/OPEN-QUESTIONS.md)). The slice exists and is seeded; its payload type is pinned when that open question resolves.

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

For ref-keyed slices (e.g. one `PileState` per `PileId`), pass the reference:

```csharp
var hand = scope.Get<PileState>(PileId.Hand);
```

Enumerate every slice of a type — and recover the reference for each — via:

```csharp
foreach (var (reference, state) in scope.EnumerateAll<PileState>()) { … }   // (ref, state) pairs
foreach (var pile in scope.GetAll<PileState>()) { … }                      // states only
```

---

## 6. Subscriptions

Slice-level subscriptions go through the `Store` directly (effects and `GameRules` see only `IStateScope`, which is read-only). The handler signature is uniform across every channel:

```csharp
Action<IReference, TState, StateChangeEvent> handler;

public enum StateChangeEvent {
    Created,   // slice registered for this reference
    Updated,   // slice value replaced
    Removed,   // slice unregistered
}
```

Four subscription channels, ordered by specificity:

```csharp
// 1. Singleton (default-ref) slice — e.g. EngineState, TurnState.
store.Subscribe<EngineState>(handler);

// 2. Specific ref-keyed slice — e.g. one PileState by PileId.
store.Subscribe<PileState>(PileId.Hand, handler);

// 3. Every reference for a type — fires for any pile, any entity, any …
store.SubscribeAllReferences<PileState>(handler);

// 4. Every event on every slice — debug / tooling channel only.
store.SubscribeAny((reference, baseState, ev) => { … });

// Symmetric unsubscribe
store.Unsubscribe<PileState>(PileId.Hand, handler);
```

Use cases by channel:

| Channel | Typical caller | Example |
|---|---|---|
| `Subscribe<TState>(handler)` | Presentation, projections watching a singleton | "Re-render when `TurnState` changes" |
| `Subscribe<TState>(reference, handler)` | Per-instance view binding | "Re-render this pile's stack when its `PileState` changes" |
| `SubscribeAllReferences<TState>` | Engine, broad projections | "Watch every entity's `EntityVariableState` for analytics" |
| `SubscribeAny` | Debugger, replay recorder | "Log every state change with reference + event type" |

For per-entity variable subscriptions specifically (handle-level `Subscribe(key, …)`, structural changes, `OnEntityRemoved`), use the typed surfaces on `IReadOnlyEntity<TDef>` and `StateEntity<TDef>` — see [`features/entities/models/02-runtime-surface.md` §3](features/entities/models/02-runtime-surface.md). The slice-level `Subscribe<EntityVariableState>(entityId, …)` is the lowest-level uncoalesced view; entities/02 §3.3 documents when to reach for it.

### 6.1 Lifetime

The store does not return an `IDisposable` from these slice-level methods (unlike the entity handle's `Subscribe`, which does). Callers that need symmetric teardown hold the handler delegate and call `Unsubscribe` on disposal:

```csharp
private readonly Action<IReference, PileState, StateChangeEvent> handHandler;

public HandView(Store store) {
    handHandler = (_, state, ev) => Render(state);
    store.Subscribe<PileState>(PileId.Hand, handHandler);
}

public void Dispose() {
    store.Unsubscribe<PileState>(PileId.Hand, handHandler);
}
```

`SubscribeAllReferences<TState>` and `SubscribeAny` follow the same pattern but currently lack symmetric `Unsubscribe` overloads — those listeners live for the lifetime of the store. Use them only for engine-level / tooling listeners that don't need teardown.

---

## 7. Atomic Batches

`Store.ExecuteBatch` commits multiple payloads as one observable step:

```csharp
store.ExecuteBatch(IReadOnlyList<object> payloads);
```

Guarantees:

- **Single atomic commit.** All registered mutators for every payload run, then the runner's overlay commits in one shot. No half-applied state is observable to subscribers, mutators, or reads.
- **One subscriber notification per affected slice per batch.** A slice that's mutated twice in the same batch fires `Updated` once after commit, with the final value. Per-variable and structural subscriptions on `IReadOnlyEntity<TDef>` follow the same coalescing rule.
- **Mixed payload types are allowed.** The same batch can carry framework payloads, entity payloads (`AddModifierPayload`, `SetBaseValuePayload`, …), and game-authored payloads. Each payload's registered mutators target their own slices.
- **Reads inside the batch are not allowed.** `ExecuteBatch` runs the whole sequence before returning; mutators read sibling slices via the `IStateScope scope` parameter on `Mutator.Change`, but external read-mutate-read sequences must be split across separate `Execute` / `ExecuteBatch` calls.
- **Ordering is the iteration order of the supplied list.** The runner processes payloads in the order they appear in `payloads`.

Use cases: applying multiple modifiers from a single ability in one observable step; restoring entity and game-level state during snapshot load; history-replay reconstruction; any sequence where intermediate state should not be visible to subscribers.

For the entity-side specifics (factory output flowing into raw `AddModifierPayload`, `IMutableEntity` not exposing batch composition), see [`features/entities/models/02-runtime-surface.md` §4.5](features/entities/models/02-runtime-surface.md).

---

## 8. Invariants

1. **No single GameState record.** State is a Scaffold `Store`; reads always go through `scope.Get<TState>()`.
2. **Framework-owned slices are auto-seeded at `Build()`.** Game authors never yield them in `WithInitialSlices`.
3. **Framework-owned slices are written only by framework services.** No game-authored mutator may target `AvailableActionsState`, `EngineState`, or `HistoryState` directly.
4. **Game-authored slices follow the same pattern.** `BaseState` subclass, immutable record, registered at bootstrap via `WithInitialSlices` + `WithMutators`.
5. **Uniform read surface.** `IStateScope.Get<TState>()` is the only read path for all tiers — no convenience accessors, no named properties.
6. **Subscriptions go through the `Store`.** `IStateScope` is read-only by design; observation requires the concrete `Store` reference (handlers via `ctx.Store`, presentation via direct injection).
7. **Batches are atomic and observable as a single step.** Use `ExecuteBatch` whenever intermediate state should not leak to subscribers.

---

## 9. Open Follow-Ups

- **`HistoryEnvelope` typed definition** — the concrete C# record for `HistoryEnvelope` (fields: `envelopeId`, `originCommandId`, `actor`, `entries[]`) is pseudocode. Pin when replay or multiplayer transport is designed. ([OPEN-QUESTIONS.md](../ledgers/OPEN-QUESTIONS.md))
- **`EngineState` surface** — whether `IsRunning`/`CurrentCommand` are the right fields, or whether additional engine-internal state (pending queue snapshot, awaiter state) belongs here, is deferred to the engine implementation pass.
- **Slice catalog growth** — as features land (lifecycle, zones, entities bridge), new game-authored slice types will emerge. The framework slice count may also change. Update this doc when a new framework-owned slice is formally added.
- **Symmetric unsubscribe for `SubscribeAllReferences` / `SubscribeAny`.** Both currently leak listeners across the store's lifetime. Tooling-only callers don't need teardown today; if presentation reaches for these channels, expose unsubscribe overloads.
- **`IAggregateProvider` registration.** `Store.RegisterAggregate` ships in Scaffold but the framework hasn't pinned a use case (computed read-only views over multiple slices). Document when the first one lands.

---

## 10. Related

- [features/effects/models/04-effect-flow.md §4.1](features/effects/models/04-effect-flow.md) — `IStateScope` / `Store` binding
- [features/effects/models/04-effect-flow.md §4.2](features/effects/models/04-effect-flow.md) — `Store.Execute<TPayload>` + `Mutator<TState, TPayload>`
- [features/entities/models/02-runtime-surface.md §3](features/entities/models/02-runtime-surface.md) — entity-handle subscriptions
- [features/entities/models/02-runtime-surface.md §4.5](features/entities/models/02-runtime-surface.md) — entity-side batch composition
- [features/engine/models/02-gamerules-contract.md](features/engine/models/02-gamerules-contract.md) — `IAvailableActions` service, `GameRules` constructor
- [features/engine/models/03-snapshot-pipeline.md](features/engine/models/03-snapshot-pipeline.md) — snapshot production, `IAvailableActions.Set`
- [support/samples/pile-counter.md](support/samples/pile-counter.md) — working example of game-authored slices (`PilesState`, `LastMoveState`)
- [ledgers/OPEN-QUESTIONS.md](../ledgers/OPEN-QUESTIONS.md) — `HistoryEnvelope` typed definition
