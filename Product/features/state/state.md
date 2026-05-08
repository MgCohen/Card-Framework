---
type: feature
status: settled
references: [root]
tags: [#state]
---

# state — feature

## What it is

The framework's state primitive — the convention layer over the **`com.scaffold.states`** Unity package (Scaffold.State). Game state is **not** a single record; it lives as multiple typed `BaseState` slices in a Scaffold `Store`. Reads go through `IStateScope.Get<TState>()`; writes go through registered `Mutator<TState, TPayload>` instances dispatched via `Store.Execute(payload)`. Subscriptions and atomic batches are first-class. This feature is the canonical home for the slice-authoring pattern, the read/write surfaces, the four subscription channels, and the atomic-batch semantics. Framework-specific tier conventions (framework-owned vs game-authored, the auto-seed rule, the catalog index) layer on top in [`engine/04`](features/engine/models/04-gamestate-slices.md).

```
                    Store (Scaffold.State)
                         │
        ┌────────────────┼────────────────┐
        │                │                │
   IStateScope      Store.Execute    Subscribe<T>(...)
   (read-only)      (payload)        (4 channels)
        │                │                │
        ▼                ▼                ▼
   Get<T>(...)      Mutator<T,P>     handler(ref, state, ev)
   GetAll<T>        .Change → new T
   EnumerateAll<T>      │
                        ▼
                 Store.ExecuteBatch([...])
                 single observable commit
```

## Vocabulary

**Feature-owned terms only.** Scaffold-package types are surfaced here as our integration points; refer to the `com.scaffold.states` package for low-level details we don't restate.

| Term | Meaning |
|---|---|
| `Store` | Scaffold's state container. Holds every slice, dispatches mutators, fires subscriber notifications. The concrete write surface. |
| `BaseState` | Marker base for slice records. Every game-state slice subclasses `BaseState`. Immutable record by convention. |
| `IStateScope` | Read-only view over the `Store`. Injected into runtime, handlers, mutators, effects. The uniform read surface. |
| `Mutator<TState, TPayload>` | Payload-keyed slice-change function. `Change(current, payload, scope) → new TState`. Registered in the container installer; collection-resolved by the engine at `Build`. |
| `StateChangeEvent` | Subscription event flag — `Created` / `Updated` / `Removed`. Passed to every subscription handler. |
| `Slice` | Informal term for a `BaseState` subclass — one typed piece of state with one concern. |

## Architecture

*n/a — feature-root only; the integration is described inline below. Scaffold.State's own documentation covers the package internals.*

## Composition

*n/a — feature-root only; no Models. A framework-specific extension to Scaffold.State would earn a model when it lands.*

## Reading state

`IStateScope` is the uniform read surface. Every consumer reads through it; nothing else reads slices directly.

```csharp
// Singleton slice (no key)
var engine = scope.Get<EngineState>();

// Ref-keyed slice (one PileState per pile zone)
var pile = scope.Get<PileState>(pileRef);

// All slices of a type, states only
foreach (var pile in scope.GetAll<PileState>()) { … }

// All slices of a type, with their reference
foreach (var (pileRef, pileState) in scope.EnumerateAll<PileState>()) { … }
```

There is no special accessor for any tier — framework-owned slices and game-authored slices read the same way. There is no `state.Piles` field or named property; the `Get<T>` family is the only path.

## Writing state

State changes flow through registered mutators. The `Store.Execute(payload)` call dispatches to the `Mutator<TState, TPayload>` registered for that payload type.

```csharp
// Mutator definition
public sealed class AdjustPileCounterMutator
    : Mutator<PileState, AdjustPileCounterPayload>
{
    public override PileState Change(
        PileState        current,
        AdjustPileCounterPayload payload,
        IStateScope      scope)        // read sibling slices here, never write them
    {
        return current with { Counter = current.Counter + payload.Delta };
    }
}

// Caller (a handler)
ctx.Store.Execute(new AdjustPileCounterPayload(pileRef, delta: +1));
```

Three rules govern mutator authoring:

- **Mutators are the only producer of new slice state.** Handlers don't write state inline; effects can't write state at all.
- **One mutator owns one slice's output.** `Change` returns a new `TState` for *its* slice. It may read sibling slices through the `IStateScope scope` parameter, but it never writes them.
- **Mutators are payload-keyed.** Multiple mutators can register for the same `TState` if they take different payloads. The dispatch is by payload type, not slice type.

## Subscriptions

Slice-level subscriptions go through the `Store` directly (not through `IStateScope`, which is read-only by design). The handler signature is uniform across every channel:

```csharp
Action<Reference, TState, StateChangeEvent> handler;

public enum StateChangeEvent {
    Created,   // slice registered for this reference
    Updated,   // slice value replaced
    Removed,   // slice unregistered
}
```

Four channels, ordered by specificity:

```csharp
// 1. Singleton (default-ref) slice — e.g. EngineState, TurnState.
store.Subscribe<EngineState>(handler);

// 2. Specific ref-keyed slice — e.g. one PileState by Ref<Zone>.
store.Subscribe<PileState>(pileRef, handler);

// 3. Every reference for a type — fires for any pile, any entity, any …
store.SubscribeAllReferences<PileState>(handler);

// 4. Every event on every slice — debug / tooling channel only.
store.SubscribeAny((reference, baseState, ev) => { … });

// Symmetric unsubscribe (channels 1 + 2)
store.Unsubscribe<PileState>(pileRef, handler);
```

Use cases by channel:

| Channel | Typical caller | Example |
|---|---|---|
| `Subscribe<T>(handler)` | Presentation, projections watching a singleton | "Re-render when `TurnState` changes" |
| `Subscribe<T>(reference, handler)` | Per-instance view binding | "Re-render this pile's score when its `PileState` changes" |
| `SubscribeAllReferences<T>` | Engine, broad projections | "Watch every entity's `EntityState` for analytics" |
| `SubscribeAny` | Debugger, replay recorder | "Log every state change with reference + event type" |

**Lifetime.** The `Store` does not return an `IDisposable` from these slice-level methods (unlike per-entity variable subscriptions on `InstanceWithSlices<TDef>`, which do). Callers that need symmetric teardown hold the handler delegate and call `Unsubscribe` on disposal:

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

`SubscribeAllReferences<T>` and `SubscribeAny` follow the same pattern but currently lack symmetric `Unsubscribe` overloads — those listeners live for the lifetime of the store. Use them only for engine-level / tooling listeners that don't need teardown. (Open follow-up — see §Open.)

For per-entity variable subscriptions specifically (handle-level `Subscribe(key, …)`), use the storage adapter on the `InstanceWithSlices<TDef>` handle — see [`entities/02 §Subscriptions`](features/entities/models/02-runtime-surface.md). The slice-level `Subscribe<EntityState>(entityRef, …)` is the lowest-level view.

## Atomic batches

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

## Slice authoring

Game authors define slices as immutable records subclassing `BaseState`:

```csharp
// Per-zone slice — keyed externally at pile.SelfRef (Ref<Zone>) by the
// factory walk per zones/01.
public sealed record PileState(int Counter, int Target) : BaseState;

// Singleton — no keying.
public sealed record LastMoveState(LastMove? Move) : BaseState;
```

Authoring rules:

- **One slice, one concern.** Each slice owns exactly one piece of game state.
- **Immutable.** All mutations produce a new record via `with { }` or a helper method. Mutators return new instances; never mutate-in-place.
- **No cross-slice mutation.** A mutator owns one slice's output. It may *read* sibling slices through the `IStateScope scope` parameter on `Change`, but it never writes them.
- **Slices don't carry their own primary key.** Storage is keyed externally by the factory using the spawned instance's `SelfRef`. Reference-shaped fields inside slice values are *data*, not keys.

Initial slice values come from `ISliceProvider.GetInitialSlices()` on the relevant Definition kind (per [`engine/07-game-definition.md`](features/engine/models/07-game-definition.md)). Mutators are registered in the game's VContainer installer; the engine's factory collection-resolves them at `Build`:

```csharp
// Game installer (one-time, app-level):
builder.Register<PileCounterMutator> (Lifetime.Singleton).AsImplementedInterfaces();
builder.Register<LastMoveMutator>    (Lifetime.Singleton).AsImplementedInterfaces();
```

Per-instance slices (one `PileState` per pile zone, one `EntityState` per card, …) come from each Definition's `GetInitialSlices()` at spawn — game authors don't seed them through `MatchSetup.InitialSlices`. Game-singleton slices come from `GameDefinition.GetInitialSlices()`.

## Invariants

- **No single `GameState` record.** State is a Scaffold `Store` of typed slices; reads always go through `IStateScope.Get<TState>()`.
- **Mutation flows through mutators, never directly.** `Mutator<TState, TPayload>.Change` is the only producer of new slice state. Handlers don't write inline; effects can't write at all.
- **Only handlers invoke mutators.** Mutators are not auto-fired off command dispatch. The handler for a command decides which mutators to run with what payload.
- **Slices are immutable.** Every change produces a new record instance.
- **No cross-slice mutation.** A mutator's `Change` returns its slice's new value. Sibling slices are read-only via the `IStateScope` parameter.
- **Slices don't carry their own primary key.** Storage is keyed externally by the factory at `instance.SelfRef`.
- **Subscriptions go through the `Store`.** `IStateScope` is read-only by design; observation requires the concrete `Store` reference (handlers via `ctx.Store`, presentation via direct injection).
- **Batches are atomic and observable as a single step.** Use `ExecuteBatch` whenever intermediate state should not leak to subscribers.

## Boundaries

**Owns:**
- The Scaffold.State integration vocabulary (`Store`, `BaseState`, `IStateScope`, `Mutator<TState, TPayload>`, `StateChangeEvent`).
- The read surfaces (`Get<T>` family, enumeration helpers).
- The write surface (`Store.Execute(payload)` and the mutator pattern).
- The four subscription channels and the lifetime/unsubscribe rules.
- `Store.ExecuteBatch` semantics (atomic commit, coalescing, mixed payloads, no reads-during-batch).
- The slice-authoring pattern (immutability, no cross-slice mutation, no own-key, payload-keyed mutators).

**Does not own:**
- Framework-vs-game tier split (auto-seeded framework slices, declared game-authored slices) → `engine/04`.
- Catalog index of framework-owned slices → `engine/04`.
- `ISliceProvider` contract (parameterless `GetInitialSlices()`) → `engine/07`.
- Specific framework slice schemas (`AvailableActionsState`, `EngineState`, `HistoryState`) → their producing models.
- Per-entity variable storage (`IEntityVariableStorage`, the entity-side `Subscribe(key, …)` surface) → `entities/02`.
- The `Store`'s low-level package internals (memory layout, dispatch implementation) → `com.scaffold.states` package documentation.

## Dependencies

*Mirrors the front-matter `references:` array in human-readable form.*

- **Upstream:** Scaffold.State package (`com.scaffold.states` — see [`DOCS/scaffold.md`](DOCS/scaffold.md) for how Scaffold packages are sourced and updated).
- **Downstream:** every feature that reads or writes game state — `engine` (lifecycle, snapshot, factory), `actions` (history envelopes, command pipeline), `effects` (mutation discipline, handler context), `entities` (entity-bridge slices), `triggers`, `presentation`, and game authors.

## Open

- **Symmetric unsubscribe for `SubscribeAllReferences` / `SubscribeAny`.** Both currently leak listeners across the store's lifetime. Tooling-only callers don't need teardown today; if presentation reaches for these channels, expose unsubscribe overloads.
- **`IAggregateProvider` registration.** `Store.RegisterAggregate` ships in Scaffold but the framework hasn't pinned a use case (computed read-only views over multiple slices). Document when the first one lands.
