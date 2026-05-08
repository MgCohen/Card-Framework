---
type: model
status: settled
references: [features/effects/models/04-effect-flow.md, features/engine/models/02-gamerules-contract.md, features/entities/models/02-runtime-surface.md]
tags: [#engine, #state, #slices]
---

# GameState Slice Catalog — model

## What it is

The slice mechanism: how state is structured, the framework-vs-game-authored tier split, the read surface, the four subscription channels, and atomic batches. There is no `GameState` class; state lives as multiple typed `BaseState` slices in a Scaffold `Store`. **Framework-owned** slices (auto-seeded at `Build`, writable only by framework services) are catalogued here as an index — each schema lives with its producing system. **Game-authored** slices are declared via `ISliceProvider` and registered with mutators in the game's installer. Subscriptions go through the `Store` directly across four channels; atomic batches commit through `Store.ExecuteBatch`.

```csharp
// Game-authored shape — example:
public sealed record PileState(int Counter, int Target) : BaseState;
public sealed record LastMoveState(LastMove? Move) : BaseState;
```

## Contracts

### No single GameState record

State lives as multiple typed slices in a Scaffold `Store`. There is no `GameState` class, no `state.Piles`, no `state.LastMove`. Every read goes through:

```csharp
scope.Get<TState>()
scope.Get<TState>(reference)
scope.GetAll<TState>()
```

where `TState : BaseState`. This binding is settled in [`effects/04 §Service surfaces`](features/effects/models/04-effect-flow.md).

### Two tiers

| Tier | Who defines | Who seeds | Who writes |
|---|---|---|---|
| **Framework-owned** | Framework | `IGameFactory.Build` auto-seeds (see [`engine/05`](features/engine/models/05-game-factory.md)) | Framework-internal services / mutators only |
| **Game-authored** | Game author | `MatchSetup.InitialSlices` / `IMatchBuilder.WithInitialSlices(...)` | Game-registered `Mutator<TState, TPayload>` |

Framework-owned slices are invisible to the game author at bootstrap — they are always present, always written by the framework, and readable by anyone via `IStateScope`. The current catalog is not a fixed ceiling; it reflects what the framework needs today and will grow or shrink as the framework matures.

### Framework-owned slices

The framework auto-seeds three slices at `Build()`. Each slice's schema lives with the system that produces it; this Model only catalogs them. Game authors never declare any of these in `MatchSetup.InitialSlices`.

| Slice | Produced by | Schema canonical home |
|---|---|---|
| `AvailableActionsState` | `IAvailableActions` service | [`engine/03 §The AvailableActionsState slice`](features/engine/models/03-snapshot-pipeline.md) |
| `EngineState` | Engine internals (drain start/stop, lifecycle handlers) | [`engine/01 §The EngineState slice`](features/engine/models/01-evaluation-loop.md) |
| `HistoryState` | Engine history mutator (post-`pipeline.Execute`) | [`actions/01 §The HistoryState slice`](features/actions/models/01-command-pipeline.md) |

The catalog is not a fixed ceiling; it reflects what the framework needs today and will grow or shrink as the framework matures. New framework-owned slices land at their producing system's canonical home and are added as a row above.

### Game-authored slice pattern

Game authors define slices as immutable records:

```csharp
// Per-zone-type slice — keyed externally at pile.SelfRef (Ref<Zone>) by the
// factory walk per zones/01.
public sealed record PileState(int Counter, int Target) : BaseState;

// Singleton — no keying.
public sealed record LastMoveState(LastMove? Move) : BaseState;
```

Rules:

- **One slice, one concern.** Each slice owns exactly one piece of game state.
- **Immutable.** All mutations produce a new record via `with { }` or a helper method.
- **No cross-slice mutation.** A mutator owns one slice's output. It may read sibling slices through the `IStateScope scope` parameter on `Change`, but it never writes them.
- **Slices don't carry their own primary key.** The factory keys per-instance slices externally using the spawned instance's `SelfRef`. Reference-shaped fields inside slice values are *data*, not keys.

Initial slice values come from `ISliceProvider.GetInitialSlices()` on the relevant Definition kind (per [`engine/07-game-definition.md`](features/engine/models/07-game-definition.md)). The factory's Build walk installs each returned slice keyed at the instance's `SelfRef` (per-zone, per-card, per-player) or unkeyed (game-singleton). Mutators are registered in the game's VContainer installer; the factory collection-resolves them at `Build`:

```csharp
// Game installer (one-time, app-level):
builder.Register<PileCounterMutator> (Lifetime.Singleton).AsImplementedInterfaces();
builder.Register<LastMoveMutator>    (Lifetime.Singleton).AsImplementedInterfaces();

// Per-zone PileState slices come from PileDefinition.GetInitialSlices() at
// spawn — game authors don't seed them through MatchSetup.InitialSlices.
// Game-singleton slices come from PileCounterGameDefinition.GetInitialSlices().
```

The factory registers framework-owned slices internally at `Build`. Game authors never seed `AvailableActionsState`, `EngineState`, or `HistoryState`. See [`engine/05-game-factory.md`](features/engine/models/05-game-factory.md) for the full match-construction contract.

### Reading state

All consumers read state through `IStateScope.Get<TState>(...)` regardless of tier:

```csharp
// GameRulesRuntime reading per-zone slice
var pile = state.Get<PileState>(pileRef);

// Trigger condition reading framework-owned slice
var snap = scope.State.Get<AvailableActionsState>().Snapshot;

// Presentation reading history
var log = scope.Get<HistoryState>().Envelopes;
```

There is no special accessor for framework slices. `IStateScope` is the uniform read surface for everything.

For ref-keyed slices (e.g. one `PileState` per pile zone, keyed by `Ref<Zone>`), pass the reference:

```csharp
var pile = scope.Get<PileState>(pileRef);
```

Enumerate every slice of a type — and recover the reference for each — via:

```csharp
foreach (var (reference, state) in scope.EnumerateAll<PileState>()) { … }   // (ref, state) pairs
foreach (var pile in scope.GetAll<PileState>()) { … }                      // states only
```

### Subscriptions

Slice-level subscriptions go through the `Store` directly (effects and `GameRulesRuntime` see only `IStateScope`, which is read-only). The handler signature is uniform across every channel:

```csharp
Action<Reference, TState, StateChangeEvent> handler;

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

// 2. Specific ref-keyed slice — e.g. one PileState by Ref<Zone>.
store.Subscribe<PileState>(pileRef, handler);

// 3. Every reference for a type — fires for any pile, any entity, any …
store.SubscribeAllReferences<PileState>(handler);

// 4. Every event on every slice — debug / tooling channel only.
store.SubscribeAny((reference, baseState, ev) => { … });

// Symmetric unsubscribe
store.Unsubscribe<PileState>(pileRef, handler);
```

Use cases by channel:

| Channel | Typical caller | Example |
|---|---|---|
| `Subscribe<TState>(handler)` | Presentation, projections watching a singleton | "Re-render when `TurnState` changes" |
| `Subscribe<TState>(reference, handler)` | Per-instance view binding | "Re-render this pile's score when its `PileState` changes" |
| `SubscribeAllReferences<TState>` | Engine, broad projections | "Watch every entity's `EntityState` for analytics" |
| `SubscribeAny` | Debugger, replay recorder | "Log every state change with reference + event type" |

For per-entity variable subscriptions specifically (handle-level `Subscribe(key, …)`), use the storage adapter on the `InstanceWithSlices<TDef>` handle — see [`entities/02 §Subscriptions`](features/entities/models/02-runtime-surface.md). The slice-level `Subscribe<EntityState>(entityRef, …)` is the lowest-level view.

**Lifetime.** The store does not return an `IDisposable` from these slice-level methods (unlike the entity handle's `Subscribe`, which does). Callers that need symmetric teardown hold the handler delegate and call `Unsubscribe` on disposal:

```csharp
private readonly Action<Reference, PileState, StateChangeEvent> pileHandler;

public PileView(Store store, Ref<Zone> pileRef) {
    pileHandler = (_, state, ev) => Render(state);
    store.Subscribe<PileState>(pileRef, pileHandler);
}

public void Dispose() {
    store.Unsubscribe<PileState>(pileRef, pileHandler);
}
```

`SubscribeAllReferences<TState>` and `SubscribeAny` follow the same pattern but currently lack symmetric `Unsubscribe` overloads — those listeners live for the lifetime of the store. Use them only for engine-level / tooling listeners that don't need teardown.

### Atomic batches

`Store.ExecuteBatch` commits multiple payloads as one observable step:

```csharp
store.ExecuteBatch(IReadOnlyList<object> payloads);
```

Guarantees:

- **Single atomic commit.** All registered mutators for every payload run, then the runner's overlay commits in one shot. No half-applied state is observable to subscribers, mutators, or reads.
- **One subscriber notification per affected slice per batch.** A slice that's mutated twice in the same batch fires `Updated` once after commit, with the final value. Per-variable subscriptions on `IEntityVariableStorage` follow the same coalescing rule.
- **Mixed payload types are allowed.** The same batch can carry framework payloads, entity payloads (`AddModifierPayload`, `SetBaseValuePayload`, …), and game-authored payloads. Each payload's registered mutators target their own slices.
- **Reads inside the batch are not allowed.** `ExecuteBatch` runs the whole sequence before returning; mutators read sibling slices via the `IStateScope scope` parameter on `Mutator.Change`, but external read-mutate-read sequences must be split across separate `Execute` / `ExecuteBatch` calls.
- **Ordering is the iteration order of the supplied list.** The runner processes payloads in the order they appear in `payloads`.

Use cases: applying multiple modifiers from a single ability in one observable step; restoring entity and game-level state during snapshot load; history-replay reconstruction; any sequence where intermediate state should not be visible to subscribers.

For the entity-side specifics (factory output flowing into raw `AddModifierPayload`, the storage adapter not exposing batch composition), see [`entities/02 §Mutation API`](features/entities/models/02-runtime-surface.md).

## Invariants

1. **No single `GameState` record.** State is a Scaffold `Store`; reads always go through `scope.Get<TState>()`.
2. **Framework-owned slices are auto-seeded at `Build()`.** Game authors never yield them in `WithInitialSlices`.
3. **Framework-owned slices are written only by framework services.** No game-authored mutator may target `AvailableActionsState`, `EngineState`, or `HistoryState` directly.
4. **Game-authored slices follow the same pattern.** `BaseState` subclass, immutable record, registered at bootstrap via `WithInitialSlices` + `WithMutators`.
5. **Uniform read surface.** `IStateScope.Get<TState>()` is the only read path for all tiers — no convenience accessors, no named properties.
6. **Subscriptions go through the `Store`.** `IStateScope` is read-only by design; observation requires the concrete `Store` reference (handlers via `ctx.Store`, presentation via direct injection).
7. **Batches are atomic and observable as a single step.** Use `ExecuteBatch` whenever intermediate state should not leak to subscribers.

## Decisions

*n/a — no elevated ADR.* (The slice-catalog tiers and subscription channels are descriptive — the underlying decisions are pinned in [`engine/01-evaluation-loop.md`](features/engine/models/01-evaluation-loop.md) Q1 (state pattern) and [`engine/03-snapshot-pipeline.md`](features/engine/models/03-snapshot-pipeline.md) Q1 (snapshot in state). This Model only catalogs.)

## Examples

See "Reading state" and the per-channel "Use cases" tables above; the Pile Counter sample ([`support/samples/pile-counter.md`](support/samples/pile-counter.md)) exercises:

- Game-authored per-zone slice (`PileState`) keyed externally at `pile.SelfRef`.
- Game-authored singleton (`LastMoveState`) keyed unmapped.
- Framework-owned `AvailableActionsState` written via `Actions.Set` from `Evaluate`.
- Framework-owned `HistoryState` appended automatically as envelopes close.
- Framework-owned `EngineState.Lifecycle` flipped via the framework `SetEngineLifecyclePayload` mutator from `StartMatchHandler` / `EndMatchHandler`.
- Atomic batch combining framework `MoveCardMutation` (CompositeAction) with game-side payloads (`AdjustPileCounterPayload`, `SetLastMovePayload`) in one `ExecuteBatch` call.

## Open follow-ups

- **Slice catalog growth** — as features land, new game-authored slice types will emerge and framework-owned slices may be added or removed. The catalog index above is updated when a new framework slice lands; per-slice details (schema, fields, write/read paths, open questions) live with the producing system.
- **Symmetric unsubscribe for `SubscribeAllReferences` / `SubscribeAny`.** Both currently leak listeners across the store's lifetime. Tooling-only callers don't need teardown today; if presentation reaches for these channels, expose unsubscribe overloads.
- **`IAggregateProvider` registration.** `Store.RegisterAggregate` ships in Scaffold but the framework hasn't pinned a use case (computed read-only views over multiple slices). Document when the first one lands.

---

*Below: provenance.*

## Options considered

*n/a — descriptive Model. The slice-catalog shape sits on top of the state-pattern decision (folded as [`engine/01 §Q1`](features/engine/models/01-evaluation-loop.md)) and the snapshot-in-state decision (folded as [`engine/03 §Q1`](features/engine/models/03-snapshot-pipeline.md)). No new options were considered for the catalog itself.*

## Rationale

The two-tier model (framework-owned vs game-authored) gives game authors a clean default — the framework slices "just exist" and aren't game-author concern — while keeping authoring boilerplate minimal: declare a `BaseState` record, register a mutator, optionally provide initial values via `ISliceProvider`. The four subscription channels span every reasonable observation pattern (singleton change, specific-ref change, all-refs-for-type, all-events) without adding ceremony for the common cases. Atomic batches via `ExecuteBatch` give a single primitive for "commit multiple payloads as one observable step" — necessary for snapshot restore, multi-payload abilities, and undo-log compositions.

## Consequences

- **Enables** uniform read surface across all tiers; clean ownership boundaries (framework vs game) for slices and mutators; single atomic commit for multi-payload mutations; per-channel observation patterns spanning singleton / specific-ref / all-refs / all-events.
- **Constrains** framework-owned slices to one canonical write path each (`IAvailableActions` for snapshot, framework mutator for lifecycle, history mutator for envelopes); slice values cannot carry their own primary key — the factory keys externally; subscriptions on `SubscribeAllReferences` / `SubscribeAny` channels currently leak listeners (tooling-only until symmetric unsubscribe arrives).
