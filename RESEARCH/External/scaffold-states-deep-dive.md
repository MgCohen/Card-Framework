# com.scaffold.states — Deep Dive

**Purpose:** Source-level analysis of the Scaffold States package as a candidate backing store for the Card Framework simulation state layer. This is a technical reference, not a design decision — use it alongside the framework design docs when evaluating integration depth.

**Source read:** 2026-04-27. Repo: [https://github.com/MgCohen/Scaffold](https://github.com/MgCohen/Scaffold) under `Assets/Packages/com.scaffold.states/`.

**Related framework docs:** [04-scaffold-package-fit.md](../04-scaffold-package-fit.md) (high-level fit summary), [19-handler-mutations-and-semantics.md](../19-handler-mutations-and-semantics.md) (StateMutation / ResolutionScope design), [20-engine-evaluation-loop.md](../20-engine-evaluation-loop.md) (GameState contract).

---

## 1. Package Structure

```
com.scaffold.states/
├── Runtime/
│   ├── Store.cs                     ← central API
│   ├── Abstractions/
│   │   ├── IReference.cs            ← key type for slice addressing
│   │   ├── IStateScope.cs           ← read interface (Get, GetAll)
│   │   ├── IStoreScope.cs           ← store-level scope
│   │   ├── IStateEventHandler.cs    ← subscription surface
│   │   ├── IStateEventDeferralController.cs
│   │   └── ISubscription.cs
│   ├── State/
│   │   ├── BaseState.cs             ← root base type
│   │   ├── State.cs                 ← abstract record : BaseState (canonical slices)
│   │   ├── AggregateState.cs        ← abstract record : BaseState (computed slices)
│   │   ├── Slice.cs                 ← canonical slice keyed by (IReference, Type)
│   │   ├── AggregateSlice.cs        ← derived/computed slice
│   │   ├── Snapshot.cs              ← Map<IReference, Type, State> for save/load
│   │   └── StateChangeEvent.cs      ← enum { Created, Updated, Removed }
│   ├── Mutators/
│   │   └── Mutator.cs               ← abstract base classes for state changers
│   ├── Pipeline/
│   │   ├── MutatorRunner.cs         ← executes mutators against scratchpad
│   │   ├── MutatorRegistry.cs       ← payload-type → mutator binding registry
│   │   ├── IPayloadMutatorBinding.cs
│   │   ├── IStoreScratchpad.cs      ← overlay interface
│   │   └── RegisteredMutator.cs
│   ├── Builders/                    ← StoreBuilder and composition helpers
│   └── Events/                      ← DeferredStateEventHandler, subscriptions
├── Tests/
├── Samples/
└── package.json
```

**Dependencies:** `com.scaffold.maps` (typed 2-key map), `com.scaffold.records`.

---

## 2. Core Types — Source

### State (immutable record base)

```csharp
public abstract record State : BaseState { }
public abstract record AggregateState : BaseState { }
```

State is a C# `record` — value semantics, structural equality, mutation returns a new instance. This is the "immutable-ish" claim in the README: mutators return new state, the store replaces slices, it never mutates in place.

### Mutator (the change logic)

```csharp
public abstract class Mutator<TState> where TState : State
{
    public abstract TState Change(TState state, IStateScope scope);
}

public abstract class Mutator<TState, TPayload> where TState : State
{
    public abstract TState Change(TState state, TPayload payload, IStateScope scope);
}
```

A `Mutator` is a stateless class that takes current state and (optionally) a payload, then returns the next state. It receives `IStateScope` so it can read other slices during the mutation (e.g., read ZoneContentsState while applying a VariableState change).

### IStateScope (read-only during mutation)

```csharp
public interface IStateScope
{
    TState Get<TState>() where TState : BaseState;
    TState Get<TState>(IReference? reference) where TState : BaseState;
    IEnumerable<TState> GetAll<TState>() where TState : BaseState;
}
```

Read-only during a mutator run. The `Get` calls during a mutation read from the **scratchpad overlay first**, then fall back to the canonical store. This means a batch of mutations within one runner sees its own in-progress writes — exactly right for sequential Apply calls in one handler.

### StateChangeEvent

```csharp
public enum StateChangeEvent { Created, Updated, Removed }
```

Fired via `IStateEventHandler.Notify` after each slice write. Subscriptions receive `(IReference, TState, StateChangeEvent)`.

### Snapshot

```csharp
public class Snapshot : Map<IReference, Type, State>
{
    public void Set(IReference reference, State state) { ... }
    public TState Get<TState>() where TState : State { ... }
    public TState Get<TState>(IReference reference) where TState : State { ... }
}
```

A typed 2-key map. `SaveSnapshot()` copies all canonical slice rows into a `Snapshot`. `LoadSnapshot(snapshot)` applies all entries then **prunes slices not in the snapshot** (full restore semantics, not merge).

---

## 3. The Staged-Commit Pipeline

This is the most important mechanical detail.

```csharp
// MutatorRunner — internal; pooled by Store
internal sealed class MutatorRunner : IStateScope, IPoolable
{
    private readonly IStoreScratchpad scratchpad;  // overlay, not the canonical store

    internal void RunMutatorWithoutCommit<TState>(IReference r, Mutator<TState> mutator)
    {
        TState stateOut = mutator.Change(Get<TState>(r), this);
        scratchpad.SetPending(r, stateOut);        // writes to overlay only
    }

    internal void CommitOverlay()
    {
        scratchpad.Commit();                        // flips overlay into canonical store
    }
}
```

```csharp
// Store.ExecuteMutator — public entry point
public void ExecuteMutator<TState>(IReference? reference, Mutator<TState> mutator)
{
    MutatorRunner runner = mutatorRunnerPool.Take();
    try
    {
        runner.RunMutatorWithoutCommit(r, mutator);
        runner.CommitOverlay();                     // all-or-nothing
    }
    finally
    {
        mutatorRunnerPool.Return(runner);           // resets scratchpad on return
    }
}
```

**Key properties of this pipeline:**
- All mutations within one `runner` session accumulate in the overlay without touching canonical slices.
- `Get` on the runner reads overlay first, then falls back — sequential mutations see their own prior writes.
- `CommitOverlay()` is atomic from the outside: canonical slices update only after all mutations are staged.
- If the runner is returned to the pool without `CommitOverlay()`, the scratchpad resets — implicit rollback.
- `ExecuteBatch(IReadOnlyList<object>)` runs all payloads through one runner before committing — batch is one atomic unit.

### DeferredStateEventHandler

Buffers subscriber notifications during a defer scope. Two modes:
- **PreserveAll** — replays all notifications in order after defer ends.
- **LatestPerKey** — coalesces to one notification per `(reference, type)` pair.

This is the "suppress notifications mid-handler, flush at envelope close" mechanism.

---

## 4. Slices and Addressing

Every state entry is a `Slice` keyed by `(IReference, Type)`. `IReference` is the identity key — any type implementing `IReference` can serve as an address.

This maps directly to framework IDs:
- `InstanceId as IReference` → per-instance state (variables, modifiers, relationships)
- `ZoneId as IReference` → per-zone state (zone contents)
- `ActorId as IReference` → per-actor state (hand size, energy, priority)
- `Reference.Null` → global/session state (phase, turn counter, active actor)

`AggregateSlice` is a computed slice that does not hold canonical data. It builds its result from other slices on demand. This is the mechanism for derived/effective values.

---

## 5. Alignment With Framework Design

| Framework Concept | Scaffold States Mechanism | Notes |
|---|---|---|
| `StateMutation` record | `TPayload` in `Mutator<TState, TPayload>` | Payload IS the mutation data |
| StateMutation logic (apply) | `Mutator<TState, TPayload>.Change(state, payload, scope)` | Stateless applier class |
| `ResolutionScope.Apply` | `MutatorRunner.RunMutatorWithoutCommit` | Stages to overlay |
| Staged-then-commit scope | `MutatorRunner` + scratchpad overlay + `CommitOverlay()` | Already built |
| All-or-nothing scope | Pool return without commit = implicit rollback | Needs explicit contract |
| Sequential `Apply` within one handler | Runner reads overlay first — sees prior writes | Native behavior |
| `GameState` read view | `IStateScope` — `Get<TState>(reference)` | GameState wraps this with typed game-domain methods |
| Per-instance state | `Slice` keyed by `InstanceId as IReference` | Native |
| Per-zone state | `Slice` keyed by `ZoneId as IReference` | Native |
| `StateChange` HistoryEntry | Store subscription notifications → adapter → `StateChange` | Thin adapter layer needed |
| Effective value / composition | `AggregateSlice` / `AggregateState` | Computes from multiple source slices |
| History / undo / save-load | `Snapshot` — `SaveSnapshot()` / `LoadSnapshot()` | Full restore semantics built in |
| Suppress mid-handler notifications | `DeferredStateEventHandler` | Flush at envelope close |
| Batch mutations (one atomic unit) | `ExecuteBatch(IReadOnlyList<object>)` | One runner, one commit |

---

## 6. What Needs To Be Built On Top

The store is a mechanism, not a complete state layer. These pieces are framework-specific and must be built:

### 6a. State slice types

Game-domain `State` records per concern:

```csharp
// Zone membership — keyed by ZoneId
public sealed record ZoneContentsState : State
{
    public ImmutableList<InstanceId> Contents { get; init; } = ImmutableList<InstanceId>.Empty;
}

// Instance variables — keyed by InstanceId
public sealed record InstanceVariableState : State
{
    public ImmutableDictionary<string, object> Variables { get; init; } = ImmutableDictionary<string, object>.Empty;
}

// Instance modifiers — keyed by InstanceId
public sealed record InstanceModifierState : State
{
    public ImmutableList<Modifier> Modifiers { get; init; } = ImmutableList<Modifier>.Empty;
}

// Session / turn state — keyed by Reference.Null
public sealed record SessionState : State
{
    public ActorId ActiveActor { get; init; }
    public int TurnNumber { get; init; }
    // phase, priority, etc.
}
```

### 6b. Mutator implementations per StateMutation

For each `StateMutation` record type, a corresponding `Mutator<TState, TStateMutation>`:

```csharp
// StateMutation record (data / payload)
public sealed record RemoveFromZoneMutation(ZoneId Zone, InstanceId Instance) : StateMutation;

// Mutator (logic / applier)
public sealed class RemoveFromZoneMutator : Mutator<ZoneContentsState, RemoveFromZoneMutation>
{
    public override ZoneContentsState Change(ZoneContentsState state, RemoveFromZoneMutation payload, IStateScope scope)
    {
        return state with { Contents = state.Contents.Remove(payload.Instance) };
    }
}
```

This is two artifacts per mutation type. The payload record travels in commands and history; the mutator class is registered once at startup.

### 6c. ResolutionScope ↔ MutatorRunner wiring

`ResolutionScope` needs to hold (or borrow) a `MutatorRunner` for the duration of a handler. `scope.Apply(mutation)` calls `runner.RunMutatorWithoutCommit`. At handler end, the pipeline calls `runner.CommitOverlay()`.

The open question is whether `ResolutionScope` borrows from the store's internal pool (tight coupling) or whether the pipeline creates/manages a runner externally and passes it into the scope (looser coupling, more testable).

### 6d. StateChange adapter

Store subscriptions (`IStateEventHandler`) must be translated into `StateChange` HistoryEntry objects and folded into the current open envelope:

```csharp
// Wired at startup — translates store notifications to history
store.SubscribeAny((reference, state, changeEvent) =>
{
    var entry = StateChangeEntryFactory.Create(reference, state, changeEvent);
    if (entry != null)
        currentEnvelope.Append(entry);
});
```

The factory maps `(reference, stateType, StateChangeEvent)` to typed `StateChange` subtypes: `ZoneContentInserted`, `ZoneContentRemoved`, `VariableChanged<T>`, etc.

The `DeferredStateEventHandler` should be active during handler execution and flushed when the envelope closes — so the HistoryEntry order matches mutation order, not notification-dispatch order.

### 6e. GameState facade

`GameState` wraps `IStateScope` and adds typed game-domain query methods:

```csharp
public sealed class GameState
{
    private readonly IStateScope scope;

    public ZoneView GetZone(ZoneId id) => new ZoneView(scope.Get<ZoneContentsState>(id), scope);
    public InstanceView GetInstance(InstanceId id) => new InstanceView(scope.Get<InstanceVariableState>(id), scope);
    public T GetVariable<T>(InstanceId id, string key) => (T)scope.Get<InstanceVariableState>(id).Variables[key];
    public ActorId ActiveActor => scope.Get<SessionState>().ActiveActor;
    // priority state, phase, etc.
}
```

`GameState` passed to `GameRules.Evaluate` reads from the canonical store (after commit), not from the runner's overlay — so rules see the committed post-command state.

---

## 7. Open Questions (As Of 2026-04-27)

These were open after the source review. They are inputs for checkpoint task 2 (GameState contract) and task 9 (StateChange and state overall).

1. **Fat vs thin slices** — One `InstanceState` per instance with all concerns, or separate `VariableState`, `ModifierState`, `RelationshipState` slices per instance? Separate slices are cleaner for mutators and subscriptions but multiply slice count for large games.

2. **ResolutionScope / MutatorRunner coupling** — Does scope borrow from the store's internal pool (simplest) or does the pipeline manage runners externally (more testable, looser coupling)?

3. **Rollback contract** — Implicit rollback (return runner without commit on handler exception) needs to be the explicit contract. Does the pipeline guarantee this, or does the scope implement a try/finally?

4. **StateChange adapter ordering** — The `DeferredStateEventHandler` must flush in mutation order. Verify that PreserveAll mode preserves order when the scratchpad commits multiple slice writes at once.

5. **AggregateState for effective values** — Effective value computation (base + modifiers from multiple sources) is a natural fit for `AggregateSlice`. Confirm this does not create circular dependencies between slices.

6. **Two artifacts per mutation (payload + mutator)** — Each `StateMutation` needs a record and a `Mutator` class. Consider whether a source generator or registration convention reduces boilerplate.

7. **`com.scaffold.entities` overlap** — Entities has its own variable bag and modifier system. If Entities is used for authoring/definitions, does the runtime simulation also use its instance variables, or do we maintain a separate `InstanceVariableState` slice in the store? One canonical path is strongly preferred.

---

## 8. Related

- [04-scaffold-package-fit.md](../04-scaffold-package-fit.md) — high-level fit table for all Scaffold packages
- [19-handler-mutations-and-semantics.md](../19-handler-mutations-and-semantics.md) — `StateMutation`, `ResolutionScope`, composite operations
- [20-engine-evaluation-loop.md](../20-engine-evaluation-loop.md) — `GameState` passed to `GameRules`, read-only contract
- [21-trigger-reaction-system.md](../21-trigger-reaction-system.md) — `Context.State` read view in triggers
- [06-open-questions.md](../06-open-questions.md) — Scaffold integration open questions
