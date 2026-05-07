---
type: model
status: settled
references: [features/effects/models/04-effect-flow.md, features/engine/models/02-gamerules-contract.md]
tags: [#engine, #state, #slices]
---

# GameState Slice Catalog

## What it is

The catalog of state slices the framework owns, the pattern for game-authored slices, and the rules for how each tier is seeded and accessed. State lives as multiple typed slices in a Scaffold `Store` — there is no `GameState` class, no `state.Piles`, no `state.LastMove`. Every read goes through `scope.Get<TState>()`.

```csharp
scope.Get<TState>()
scope.Get<TState>(reference)
scope.GetAll<TState>()
```

where `TState : BaseState`.

## Contracts

### Two tiers

| Tier | Who defines | Who seeds | Who writes |
|---|---|---|---|
| **Framework-owned** | Framework | `IGameFactory.Build` auto-seeds (see [`engine/05`](features/engine/models/05-game-factory.md)) | Framework-internal services / mutators only |
| **Game-authored** | Game author | `MatchSetup.InitialSlices` / `IMatchBuilder.WithInitialSlices(...)` | Game-registered `Mutator<TState, TPayload>` |

Framework-owned slices are invisible to the game author at bootstrap — they are always present, always written by the framework, and readable by anyone via `IStateScope`. The current catalog is not a fixed ceiling; it reflects what the framework needs today and will grow or shrink as the framework matures.

### Framework-owned slices

#### `AvailableActionsState`

```csharp
public sealed record AvailableActionsState(
    AvailableActionsSnapshot Snapshot) : BaseState;
```

- **Purpose.** Holds the current available-actions snapshot published by `GameRulesRuntime.OnQueueDrained`.
- **Written by.** `IAvailableActions` service (framework-internal mutator). Game authors write through the service, never directly.
- **Read by.** Gateway (pre-dispatch check), presentation layer, `GameRulesRuntime.OnQueueDrained` for re-build.
- **Seeded.** `AvailableActionsState(AvailableActionsSnapshot.Empty)` at `Build()`.

#### `EngineState`

```csharp
public sealed record EngineState(
    EngineLifecycle Lifecycle,
    bool            IsRunning,
    GameCommand?    CurrentCommand) : BaseState;
```

- **Purpose.** Tracks engine execution state — match lifecycle phase, whether a drain is in progress, and which command is currently executing. Load-bearing for the framework's lifecycle gate, inspection, debugging, and future replay/save support.
- **`Lifecycle`** — `NotStarted | Started | Ended`. Source of truth for the framework's intake gate. Mirrored on `engine.Lifecycle` for host convenience. Written by the framework-internal `SetEngineLifecyclePayload` mutator from `StartMatchHandler` / `EndMatchHandler` (see [`engine/06-match-lifecycle.md`](features/engine/models/06-match-lifecycle.md)).
- **Written by.** Engine internals only. Not writable by game authors.
- **Read by.** The engine's `Dispatch` lifecycle gate, tooling, debugger, and any game logic that needs to know "is the match active?" or "is the engine currently draining?".
- **Seeded.** `EngineState(Lifecycle: EngineLifecycle.NotStarted, IsRunning: false, CurrentCommand: null)` at `Build()`.

#### `HistoryState`

```csharp
public sealed record HistoryState(
    IReadOnlyList<HistoryEnvelope> Envelopes) : BaseState;
```

- **Purpose.** Holds the ordered envelope log. Load-bearing for replay, multiplayer transport, and the presentation history loop.
- **Written by.** Engine-internal history mutator (fires after every `pipeline.Execute` call, appending the closed envelope). Not writable by game authors.
- **Read by.** Presentation layer (history loop), networking (transport), replay tooling.
- **Seeded.** `HistoryState(ImmutableList<HistoryEnvelope>.Empty)` at `Build()`.
- **Note.** `HistoryEnvelope` is currently pseudocode (see [OPEN-ITEMS.md — HistoryEnvelope typed definition](ledgers/OPEN-ITEMS.md)). The slice exists and is seeded; its payload type is pinned when that open question resolves.

### Game-authored slice pattern

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

Initial slice values flow into match construction through the factory; mutators are registered in the game's VContainer installer:

```csharp
// Game installer (one-time, app-level):
builder.Register<PileMoveMutator> (Lifetime.Singleton).AsImplementedInterfaces();
builder.Register<LastMoveMutator> (Lifetime.Singleton).AsImplementedInterfaces();

// Match construction (per match):
factory.NewMatch()
    .WithInitialSlices(
        new PilesState(initialPiles),
        new LastMoveState(Move: null))
    .Build();
```

The factory registers framework-owned slices internally at `Build`. Game authors never seed `AvailableActionsState`, `EngineState`, or `HistoryState`.

### Reading state

All consumers read state through `IStateScope.Get<TState>()` regardless of tier:

```csharp
// GameRulesRuntime reading game-authored slice
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

Enumerate every slice of a type via:

```csharp
foreach (var (reference, state) in scope.EnumerateAll<PileState>()) { … }   // (ref, state) pairs
foreach (var pile in scope.GetAll<PileState>()) { … }                       // states only
```

### Subscriptions

Slice-level subscriptions go through the `Store` directly (effects and `GameRulesRuntime` see only `IStateScope`, which is read-only). Handler signature is uniform:

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

For per-entity variable subscriptions specifically, use the typed surfaces on `IReadOnlyEntity<TDef>` and `StateEntity<TDef>` — see [`features/entities/models/02-runtime-surface.md` §Subscriptions](features/entities/models/02-runtime-surface.md).

**Lifetime.** The store does not return an `IDisposable` from these slice-level methods (unlike the entity handle's `Subscribe`, which does). Callers that need symmetric teardown hold the handler delegate and call `Unsubscribe` on disposal. `SubscribeAllReferences<TState>` and `SubscribeAny` follow the same pattern but currently lack symmetric `Unsubscribe` overloads — those listeners live for the lifetime of the store.

### Atomic batches

`Store.ExecuteBatch` commits multiple payloads as one observable step:

```csharp
store.ExecuteBatch(IReadOnlyList<object> payloads);
```

Guarantees:

- **Single atomic commit.** All registered mutators for every payload run, then the runner's overlay commits in one shot. No half-applied state is observable.
- **One subscriber notification per affected slice per batch.** A slice that's mutated twice in the same batch fires `Updated` once after commit, with the final value.
- **Mixed payload types are allowed.** The same batch can carry framework payloads, entity payloads, and game-authored payloads.
- **Reads inside the batch are not allowed.** `ExecuteBatch` runs the whole sequence before returning; mutators read sibling slices via the `IStateScope scope` parameter on `Mutator.Change`; external read-mutate-read sequences must be split across separate `Execute` / `ExecuteBatch` calls.
- **Ordering is the iteration order of the supplied list.**

For entity-side specifics (factory output flowing into raw `AddModifierPayload`, `IMutableEntity` not exposing batch composition), see [`features/entities/models/02-runtime-surface.md` §Batched mutations](features/entities/models/02-runtime-surface.md).

## Invariants

Model-local invariants. (See `engine.md ## Invariants` for feature-level rules — state immutability, framework slices auto-seeded, snapshot-via-service.)

1. **No single GameState record.** State is a Scaffold `Store`; reads always go through `scope.Get<TState>()`.
2. **Framework-owned slices are auto-seeded at `Build()`.** Game authors never yield them in `WithInitialSlices`.
3. **Framework-owned slices are written only by framework services.** No game-authored mutator may target `AvailableActionsState`, `EngineState`, or `HistoryState` directly.
4. **Game-authored slices follow the same `BaseState` immutable record pattern.**
5. **Uniform read surface.** `IStateScope.Get<TState>()` is the only read path for all tiers.
6. **Subscriptions go through the `Store`.** `IStateScope` is read-only by design.
7. **Batches are atomic and observable as a single step.**

## Examples

```csharp
// Game-authored slice + custom helper
public sealed record PilesState(IReadOnlyDictionary<PileId, Pile> Piles) : BaseState
{
    public PilesState WithCardMoved(InstanceId card, PileId from, PileId to)
    {
        var src = Piles[from];
        var dst = Piles[to];
        var moved = src.TopCard;   // assumes top-only; PileMoveMutator validates
        var newSrc = src.WithoutTop();
        var newDst = dst.WithCardOnTop(moved);
        return new PilesState(Piles.SetItem(from, newSrc).SetItem(to, newDst));
    }
}

// Mutator that fires on MoveCardPayload
public sealed class PileMoveMutator : Mutator<PilesState, MoveCardPayload>
{
    public override PilesState Change(PilesState state, MoveCardPayload payload, IStateScope scope)
        => state.WithCardMoved(payload.Card, payload.From, payload.To);
}
```

## Open follow-ups

- **`HistoryEnvelope` typed definition** — concrete C# record (fields: `envelopeId`, `originCommandId`, `actor`, `entries[]`). Pseudocode currently. Pin when replay or multiplayer transport is designed.
- **`EngineState` surface** — whether `IsRunning`/`CurrentCommand` are the right fields, or whether additional engine-internal state belongs here, deferred to engine implementation.
- **Slice catalog growth** — as features land (lifecycle, zones, entities bridge), new game-authored slice types will emerge.
- **Symmetric unsubscribe for `SubscribeAllReferences` / `SubscribeAny`** — both currently leak listeners. Tooling-only callers don't need teardown today; if presentation reaches for these channels, expose unsubscribe overloads.
- **`IAggregateProvider` registration** — `Store.RegisterAggregate` ships in Scaffold but the framework hasn't pinned a use case. Document when the first one lands.

---

*Below: provenance — feature-internal decisions folded from `features/engine/decisions/0003-snapshot-in-state.md` and `features/engine/decisions/0005-state-pattern.md`. Single-feature scope (state-pattern + snapshot-in-state are engine-owned; entities/cards/zones consume the resulting Store mechanism but don't co-decide the immutability + slice pattern).*

## Options considered

### Q1 — Snapshot location (formerly ADR-0003 — folded)

The snapshot of currently-legal actions has to flow to presentation, network, and replay. Where does the snapshot *live*? In the rule? In a side channel? Or in canonical state?

- **A — Side-channel observable owned by the rule.** Rejected: bypasses the state pattern, breaking replay and per-player views.
- **B — Recompute on every read by presentation.** Rejected: cost is unbounded for complex games and tying it to a frame loop hides bugs.
- **C — `AvailableActionsState` slice in the canonical Store; written via `IAvailableActions` service.** **Chosen.**

### Q2 — State pattern (formerly ADR-0005 — folded)

The framework needs a state model that supports replay, per-player views, deterministic networking, and zero state held by the rule. Scaffold States is available with a redux-like immutable model.

- **A — Mutable POCO state mutated in handlers.** Rejected: breaks replay; forces deep cloning for views.
- **B — Custom event-sourced state.** Rejected: duplicates Scaffold infrastructure for no incremental benefit.
- **C — Scaffold States: `GameState` immutable, redux-like; mutators react to commands; `IAvailableActions` writes via internal mutator.** **Chosen.**

## Rationale

Q1: snapshot in state means presentation and other consumers read it via the same state subscription mechanism they use for everything else. Writes through `IAvailableActions` keep one ordering invariant (handled in [`engine/03`](features/engine/models/03-snapshot-pipeline.md)).

Q2: immutable state with mutator-react-to-payload gives deterministic replay, snapshot/restore, per-player views, and trivial state inspection — all from one model. The Store is canonical; entities are accessed via Entities-shaped handles through `com.scaffold.entities.states`.

## Consequences

- **Enables:** uniform observation, replay, snapshot-driven UIs (Q1); deterministic replay, snapshot/restore, per-player views (Q2); the framework's per-actor view derivation comes for free from immutable slices.
- **Constrains:** every snapshot mutation must round-trip through the writer service (Q1); every state change must be expressible as a mutator over a payload — ad-hoc mutation isn't possible (Q2); framework slices have one writer (the framework); game-authored slices must follow the `BaseState` pattern.
- **Reverts/replaces:** earlier mutable-POCO state sketches are retired (Q2); side-channel observable patterns for snapshot are forbidden (Q1).
- **Cascade:** consumed by every Model that holds state (`entities/01`, `cards/01`, `zones/01`, etc.); the per-feature Models declare their game-authored slices following this contract.
