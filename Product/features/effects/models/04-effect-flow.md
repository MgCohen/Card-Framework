---
type: model
status: settled
references: [features/effects/models/01-async-dispatch.md, features/engine/models/03-snapshot-pipeline.md, features/actions/models/01-command-pipeline.md, features/entities/models/02-runtime-surface.md]
tags: [#effects, #handler-context, #decisions, #async]
---

# Effect Flow — model

## What it is

The full mid-execution lifecycle of an effect: what services it sees, how decisions originate and thread through the pipeline, what `await` resumes, and which layer is allowed to mutate state. Pulls together the handler-context placeholders that have been hand-waved across `01-async-dispatch.md`, `engine/03-snapshot-pipeline.md`, and earlier samples into a single canonical contract.

Three pieces this Model pins:

1. **Service surface** for effects and handlers mid-execution: what each layer can read, mutate, dispatch, await.
2. **Decision identity and threading**: who mints the id, how it flows from the requesting effect through the snapshot, the player response, and back to the awaiting effect. (Settled: type-only routing — no `DecisionId`.)
3. **Mutation discipline**: who is allowed to change `GameState`, and through what surface.

## Contracts

### The three layers, recapped with capabilities

Three layers participate in mid-execution game logic. Each has exactly one surface and a deliberately narrow capability set.

| Layer | Read state | Mutate state | Dispatch action | Await player |
|---|---|---|---|---|
| **`Effect`** | Yes (via `scope.State`) | **No** | Yes (`scope.Dispatch`) | No (only by dispatching a decision action) |
| **`ICommandHandler`** | Yes (via `ctx.Store`) | Yes — by invoking mutators | No | Yes (decision handlers only) |
| **`Mutator<TState, TPayload>`** | Yes (current state in; sibling slices via `IStateScope`) | **Yes — only producer of new state** | No | No |

Three principles fall out of this table and are framework invariants:

- **Mutation flows through mutators, never directly.** A mutator's `Change(state, payload, scope)` returning a new `GameState` is the only path that produces state. Handlers don't write state inline; effects can't write state at all.
- **Only handlers invoke mutators.** Mutators are not auto-fired off command dispatch. The handler for a command decides which mutators to run and with what payload.
- **Effects orchestrate, never mutate.** An effect's job is to sequence dispatches and thread their results. State change happens entirely inside the dispatched actions' handlers.

### The `Effect` type

```csharp
public abstract class Effect
{
    public virtual bool Validate(IEffectScope scope) => true;
    public abstract Task Execute(IEffectScope scope);
}
```

- **First-class type.** Not a convention or a delegate.
- **Per-instance.** Each card *instance* owns its own `Effect` object(s). The card *definition* holds the recipe — how the effect is constructed for new instances. The recipe shape (data, code, graph, or hybrid) is out of scope here.
- **Internals are opaque to the framework.** The engine only needs `Execute(scope)`; how an effect is authored, parameterized, or composed is the authoring layer's concern.
- **Many per definition.** A card can carry several effects (e.g. battlecry + deathrattle + ongoing aura). Each is a separate `Effect` with its own trigger condition.

Effects can have per-instance state (counters, accumulators) but where that state lives — on the `Effect` object, on the entity's `EntityVariableState`, or on a dedicated slice — is deliberately undesigned at this level. The Model only pins the execution surface.

### Reads — `IStateScope` (Scaffold)

```csharp
// from Scaffold.States
public interface IStateScope
{
    TState Get<TState>() where TState : BaseState;
    TState Get<TState>(IReference? reference) where TState : BaseState;
    IEnumerable<TState> GetAll<TState>() where TState : BaseState;
}

public interface IStoreScope : IStateScope
{
    IStateEventHandler Events { get; }
}
```

- `IStateScope` — read-only access to slices. Handed to anything that should not mutate (effects, `GameRules`, presentation, projections, mutators reading sibling slices).
- `IStoreScope` — adds the event handler for subscriptions. Used where presentation/observers need it.
- **`GameState` is not a single record.** State lives as multiple slices (typed `BaseState` subclasses) keyed in the store. There is no `state.Piles` or `state.LastMove` field — those reads become `scope.Get<PilesState>(pileRef)` / `scope.Get<LastMoveState>()`. The catalog of slice types is its own open thread.

### Writes — `Store.Execute<TPayload>` + `Mutator<TState, TPayload>` (Scaffold)

Mutation goes through Scaffold's `Store` and its registered-mutator pipeline.

```csharp
// from Scaffold.States — concrete class
public sealed class Store : IStoreScope
{
    // Payload-keyed: looks up every mutator registered for TPayload, runs them all,
    // commits the overlay atomically. This is the path the handler uses.
    public void Execute<TPayload>(TPayload payload);

    // Direct mutator invocation — bypasses the registry. Used when a single, known
    // mutator must run (rare in handler code; mostly engine bootstrap and tests).
    public void ExecuteMutator<TState>(Mutator<TState> mutator) where TState : State;
    public void ExecuteMutator<TState, TPayload>(Mutator<TState, TPayload> mutator, TPayload payload)
        where TState : State;

    public void RegisterMutator<TState, TPayload>(Mutator<TState, TPayload> mutator) where TState : State;
}

public abstract class Mutator<TState, TPayload> where TState : State
{
    public abstract TState Change(TState state, TPayload payload, IStateScope scope);
}
```

The framework hands handlers the concrete `Store`; they call `ctx.Store.Execute(payload)` to write. Effects do **not** receive `Store` — they get `IStateScope` (read-only) on `IEffectScope`.

The shape this gives:

- **`TPayload` types are boundary-correct for `Identifier<T>` use.** Mutator payloads are the Store's wire form — they persist, serialize, and transport analogously to `GameCommand`s. `InstanceId`, `PileId`, and similar `Identifier<T>` values in payload records are boundary-correct per [`Product/decisions/0005-objects-over-ids.md`](decisions/0005-objects-over-ids.md). Handler code *building* a payload should derive IDs from held objects (`entity.Id`, `zone.Id`) rather than storing raw IDs in locals.
- **Payload is a dedicated type, not the command.** A "last move" mutator takes a `LastMovePayload`, never `MoveCardCommand`. Payloads are framework-owned; commands are pipeline-owned.
- **One payload, many mutators.** Mutators are registered per `TPayload` at engine bootstrap. When the handler dispatches one payload through `Store.Execute(payload)`, **every mutator registered for that payload type runs**, each producing its own slice's new state. The handler does not enumerate which mutators exist; the registry does.
- **Mutators read sibling slices through `IStateScope`.** A mutator computing the new `LastMoveState` can look up the current `PilesState` via the `scope` parameter on `Change`.
- **Atomic commit.** All mutators that fire from one `Execute` call commit together via the runner's overlay. No half-applied state is visible.

### `IEffectScope`

```csharp
public interface IEffectScope
{
    IStateScope       State     { get; }    // Scaffold; read-only
    IEntityRegistry   Entities  { get; }    // entity handle lookup (read paths)
    IModifierFactory  Modifiers { get; }    // deterministic modifier-entry construction

    Task<TResult> Dispatch<TResult>(GameAction action) where TResult : CommandResult;
    void Cancel(string? reason = null);
    void Replace(GameAction next);   // before-fold only: swap the intercepted command
}
```

- **`State`** — Scaffold `IStateScope`. Effects read slices to choose what to dispatch next. They cannot mutate; they have no `Store` reference.
- **`Entities`** — engine entity registry, same surface as on `IHandlerContext`. Effects use `ctx.Entities.Get<TDef>(id)` to obtain `IReadOnlyEntity` handles for variable reads (`entity.GetVariable<int>(Vars.Attack)`). Effects do not mutate entities; mutation goes through `Dispatch`-ed actions whose handlers do the writes.
- **`Modifiers`** — modifier-entry factory, same surface as on `IHandlerContext`. Effects that need to construct a modifier entry to pass into a `GameAction`'s payload (e.g. an `ApplyAuraAction { Buff = scope.Modifiers.Create(...) }`) use it here so the deterministic id is allocated by the effect even though the install happens in the action's handler.
- **`Dispatch`** — runs the action immediately and inline (before-fold → handler → after-fold → envelope published) per [`01-async-dispatch.md`](features/effects/models/01-async-dispatch.md). The effect awaits each result before continuing.
- **`Cancel`** — explicit early-out for the effect itself (the effect has decided it cannot continue, e.g. a precondition no longer holds after a prior dispatch). In the trigger context (before-fold), cancels the intercepted command. Distinct from the before-fold cancelling an individual `GameAction` via result; see Cancellation below.
- **`Replace`** — before-fold only: signals the pipeline to discard the intercepted command and dispatch `next` as a fresh command through its own fold. Calling `Replace` in an after-fold trigger is a no-op (the command already executed); the pipeline implementation asserts or warns.

### `IHandlerContext`

```csharp
public interface IHandlerContext
{
    Store              Store     { get; }   // Scaffold; read + execute mutators
    IAvailableActions  Actions   { get; }   // mid-execution snapshot writes
    ICommandAwaiter    Resolver  { get; }   // register and resolve decision awaits
    IEntityRegistry    Entities  { get; }   // entity handle lookup / spawn / destroy
    IModifierFactory   Modifiers { get; }   // deterministic modifier-entry construction
}
```

- **`Store`** — concrete Scaffold `Store`. Read via `ctx.Store.Get<TState>()` (inherited from `IStateScope`); write via `ctx.Store.Execute(payload)`. Scaffold does not expose the execute methods on `IStoreScope`; the concrete class is the surface.
- **No `Mutate` surface.** Earlier sketches' `ctx.Mutate.MoveCardBetweenPiles(...)` line is replaced by a single `ctx.Store.Execute(new MoveCardPayload(...))` call.
- **Entity-variable mutations.** For modifier / variable / structural mutations against entity slices, prefer the `IMutableEntity` surface on a `StateEntity<TDef>` handle obtained via `ctx.Entities.Get<TDef>(id)`. Build modifier entries through the `Modifiers` factory and pass them to `entity.AddModifier(entry)`. Raw `ctx.Store.Execute(payload)` remains the documented fallback for `SetBaseValuePayload` / `RemoveModifiersBySourcePayload` (not exposed on `IMutableEntity`), batch authoring, history-replay tooling, and handle-less paths. Full handler-side surface in [`entities/02-runtime-surface.md`](features/entities/models/02-runtime-surface.md).
- **`Entities`** — engine entity registry. `Spawn<TDef>(definition)` allocates a deterministic `InstanceId`, registers the canonical `EntityVariableState.Empty` slice via `EntityStateFactory`, and returns a handle. `Get<TDef>(id)` / `TryGet<TDef>(id, out)` / `Has(id)` cover lookup; `Destroy(id)` disposes the handle and unregisters the slice. See [`entities/02 §Lifecycle`](features/entities/models/02-runtime-surface.md).
- **`Modifiers`** — modifier-entry factory. `Create(key, modifier, source)` returns a fully-populated `EntityModifierEntry` with a deterministic `ModifierId` and optional source baked in. The id comes from an engine-owned counter (snapshotted, restored on `LoadSnapshot`). Constructing `EntityModifierEntry` directly is forbidden in handler / framework code.

### `ICommandAwaiter`

```csharp
public interface ICommandAwaiter
{
    Task<TCommand> Wait<TCommand>(CancellationToken ct = default)
        where TCommand : GameCommand;
    bool TryResolve(GameCommand cmd);
}
```

- **`Wait<TCommand>(ct)`** — registers a closure-captured `cmd is TCommand` predicate backed by a `TaskCompletionSource<TCommand>`. Returns the `Task`. Used by decision handlers to pause until a matching command arrives. Single-open per type-hierarchy is a runtime assert.
- **`TryResolve(cmd)`** — pattern-matches `cmd` against every registered predicate. On a match, completes the associated TCS with the concrete command; returns `true`. Returns `false` if no predicate matches (the engine falls through to the normal pipeline). Called by the engine's pre-pipeline check on every `Dispatch`.

The awaiter is a normal engine service exposed on `IHandlerContext`. Decision handlers call `Wait`; effects never see this surface directly — they observe decisions only through `scope.Dispatch` returning a `CommandResult`.

### Decision routing — type-only

**No decision id.** The awaiter registers a `cmd is TCommand` predicate — the generic type parameter is the routing key. There is **no `DecisionId`**, no `CommandId` carried by the awaiter or the snapshot. The mapper produces a normal `SelectTargetCommand` or `PickOptionCommand` without any special annotation; the engine's pre-pipeline check pattern-matches it.

This removes the "where does a decision id originate?" question entirely, and eliminates the `DecisionPrompt(CommandId)` snapshot scope field that previously carried it. The snapshot never needs to signal that a decision is open — the awaiter answers that directly.

This decision *reverses* the original suspension model that proposed explicit `DecisionId`s threaded through state. See below-divider provenance Q1 for the supersession trail.

**At most one open awaiter per type hierarchy.** While a decision-handler's `Handle` is awaiting `Resolver.Wait<TCommand>()`, the dispatched action has not completed. Its after-fold has not run. No triggered effect is running. The next `scope.Dispatch` line in the requesting effect cannot start until this one returns. Sequential nesting only. Single-open per type-hierarchy is a runtime assert.

**End-to-end thread.** A targeting decision (`RequestTargetAction` → player picks `enemyX` → `DealDamageAction`):

```
Effect.Execute(scope)
  └─ scope.Dispatch(RequestTargetAction { Filter: EnemyFilter })

Engine routes → RequestTargetHandler.Handle(action, ctx):
  var legal = ComputeLegalTargets(ctx.Store, action.Filter);
  ctx.Actions.Set(new AvailableActionsSnapshot(
      new[] { new ChooseTargetTemplate(legal) }));
  var cmd = await ctx.Resolver.Wait<SelectTargetCommand>();
  return new RequestTargetResult { Target = cmd.Target };

Engine paused on the await. Queue does not advance.

Player intent → IntentMapper switches on ChooseTargetTemplate:
  → SelectTargetCommand { Target: enemyX }

engine.Dispatch(SelectTargetCommand):
  Pre-pipeline awaiter check: awaiter.TryResolve(SelectTargetCommand) → match
  Short-circuits (no envelope, no handler, no triggers).
  Wait<SelectTargetCommand> task completes with the command.

Original handler resumes:
  return new RequestTargetResult { Target: enemyX }

After-fold runs, envelope published, scope.Dispatch's await resolves:
Effect continues:
  └─ await scope.Dispatch(DealDamageAction { Target: enemyX, Amount: 6 })
```

Type only. No snapshot scope consulted. No decision id in flight.

### The uniform boundary

The gateway / intake layer treats all player input identically. Every intent maps to a `GameCommand` via `IntentMapper.Map` and enters `engine.Dispatch`. No scope branch at the gateway; the mapper is decision-unaware.

```
intent → IntentMapper.Map(template, choice) → GameCommand → engine.Dispatch
```

`IIntentMapper.Map` switches on the typed template subtype only:

```csharp
public GameCommand Map(ActionTemplate template, PlayerChoice choice) => template switch
{
    MoveCardTemplate     m => new MoveCardCommand(m.Card, state.LocateCard(m.Card), (PileId)choice.Targets[0]),
    ChooseTargetTemplate t => new SelectTargetCommand(choice.Targets[0]),
    _ => throw new InvalidOperationException($"unknown template: {template}")
};
```

No snapshot scope inspection. The mapper produces `SelectTargetCommand` whether or not a decision is open — it does not know.

**Engine-level pre-pipeline awaiter check.** On every `engine.Dispatch`, the engine runs the awaiter check **before** the normal pipeline:

```csharp
// Engine.Dispatch (simplified)
if (awaiter.TryResolve(command))
    return;   // short-circuit: no envelope, no handler, no triggers

// normal validation pipeline:
BeginPipeline(command);
```

If the awaiter matches (`cmd is TCommand`), it completes `Wait<TCommand>()` and the engine short-circuits. The matched command never reaches the handler or trigger fold. One logical event (the request action) produces one envelope.

Non-matching commands fall through to the normal pipeline; all validation layers gate them as usual.

**`ICommandHandler.Handle` returns `Task<TResult>`.** For decision handlers to await `Resolver.Wait<TCommand>()`, the handler signature must be async-capable:

```csharp
public interface ICommandHandler<TCommand, TResult>
    where TCommand : GameCommand
    where TResult  : CommandResult
{
    bool Validate(TCommand command, IStateScope state, out string? reason);
    Task<TResult> Handle(TCommand command, IHandlerContext ctx);
}
```

Most handlers complete synchronously and return `Task.FromResult(...)`. Only decision handlers genuinely await. This is consistent with `ITriggerPipeline.RunBefore/RunAfter` returning `Task` for the same reason.

### Cancellation

Two cancellation cases exist; both surface through the same channel.

**Action cancelled by the before-fold.** A registered `OnBefore<T>` returns `FoldResult.Cancel(reason)`. The action's handler never runs. The effect's `await scope.Dispatch(...)` resolves with a `CommandResult` carrying the cancellation. The effect inspects the result and decides whether to continue:

```csharp
var damage = await scope.Dispatch(new DealDamageAction { Target = t, Amount = 6 });
if (damage.IsCancelled) return;        // shield absorbed; effect ends gracefully
await scope.Dispatch(new MoveToDiscardAction { Source = source });
```

**Effect aborts itself.** The effect calls `scope.Cancel()` when it knows it cannot continue (e.g. the targeted entity has been removed by a previous dispatch). The effect's `Execute` task completes; no further dispatches run.

The framework invariant: cancellation of one `GameAction` never automatically cancels the orchestrating effect. The effect is the only place that decides whether the chain should continue.

`CommandResult` shape for cancellation (flag on the base record vs. typed `CancelledResult` subtype) is a small decision deferred to command-pipeline cleanup; either works for the model here.

## Invariants

Model-local invariants. (See `effects.md ## Invariants` for feature-level rules — these capture the load-bearing pins for this Model.)

1. **No decision id.** Type-only routing: the `TCommand` type parameter in `ICommandAwaiter.Wait<TCommand>()` is the routing key. There is no `DecisionId` and no `CommandId` carried by the awaiter or the snapshot.
2. **At most one open awaiter per type hierarchy.** Single-open is a runtime assert. Sequential nesting only.
3. **Uniform boundary.** All player input goes `intent → IntentMapper.Map(template, choice) → GameCommand → engine.Dispatch`. The mapper switches on template subtype only and is decision-unaware. The engine's pre-pipeline awaiter check intercepts matched commands before the normal pipeline.
4. **Mutation only through mutators.** Scaffold's `Mutator<TState, TPayload>.Change` is the only producer of new slice state. Only handlers invoke mutators (via `Store.Execute(payload)`); effects cannot mutate.
5. **Mutator payloads are dedicated types**, not the command. One payload may fan out to N registered mutators; `Store.Execute(payload)` runs every mutator registered for that payload type and commits atomically.
6. **`IHandlerContext` exposes** Scaffold `Store`, `IAvailableActions`, `ICommandAwaiter`, `IEntityRegistry`, `IModifierFactory`. No mutate-helper surface; no decision-id field.
7. **`IEffectScope` exposes** Scaffold `IStateScope`, `IEntityRegistry`, `IModifierFactory`, `Dispatch`, `Cancel`, `Replace`. No `Store` reference; no awaiter surface. `Replace` is meaningful only in before-fold triggers; calling it elsewhere is a no-op / runtime warning.
8. **`ICommandHandler.Handle` returns `Task<TResult>`**. Synchronous handlers return `Task.FromResult(...)`.
9. **`Effect.Validate` is a pre-execution gate.** `virtual bool Validate(IEffectScope scope) => true`. Returning `false` prevents `Execute` from running. The default always passes — simple effects don't override it.

## Decisions

*n/a — no elevated ADR.*

## Examples

### Pick-a-target effect (Fireball)

The effect orchestrates the targeting decision, then the damage dispatch. It does **not** move itself to discard — that is the spell-cast/resolution flow's responsibility.

```csharp
public sealed class FireballEffect : Effect
{
    public override async Task Execute(IEffectScope scope)
    {
        var pick = await scope.Dispatch(new RequestTargetAction { Filter = EnemyFilter });
        if (pick.IsCancelled) return;

        await scope.Dispatch(new DealDamageAction { Target = pick.Target, Amount = 6 });
    }
}

public sealed class RequestTargetHandler
    : ICommandHandler<RequestTargetAction, RequestTargetResult>
{
    public bool Validate(RequestTargetAction a, IStateScope state, out string? r)
    { r = null; return true; }

    public async Task<RequestTargetResult> Handle(RequestTargetAction action, IHandlerContext ctx)
    {
        var legal = ComputeLegalTargets(ctx.Store, action.Filter);

        ctx.Actions.Set(new AvailableActionsSnapshot(
            new[] { new ChooseTargetTemplate(legal) }));

        var cmd = await ctx.Resolver.Wait<SelectTargetCommand>();

        return new RequestTargetResult { Target = cmd.Target };
    }
}
```

### Pile Counter — one payload, many mutators

One payload, two registered mutators. The handler does a single `Execute` call; Scaffold's registry fans out to every mutator registered for `MoveCardPayload` and commits them atomically.

```csharp
public sealed class MoveCardHandler : ICommandHandler<MoveCardCommand, MoveCardResult>
{
    public bool Validate(MoveCardCommand move, IStateScope state, out string? reason)
    {
        // ... existing legality checks (top-only, source has card, target exists)
        reason = null;
        return true;
    }

    public Task<MoveCardResult> Handle(MoveCardCommand move, IHandlerContext ctx)
    {
        ctx.Store.Execute(new MoveCardPayload(move.Card, move.From, move.To));
        return Task.FromResult(new MoveCardResult(move.Card));
    }
}

public sealed record MoveCardPayload(InstanceId Card, PileId From, PileId To);

// Registered mutators — both fire from one MoveCardPayload.
public sealed class PileMoveMutator : Mutator<PilesState, MoveCardPayload>
{
    public override PilesState Change(PilesState state, MoveCardPayload payload, IStateScope scope)
        => state.WithCardMoved(payload.Card, payload.From, payload.To);
}

public sealed class LastMoveMutator : Mutator<LastMoveState, MoveCardPayload>
{
    public override LastMoveState Change(LastMoveState state, MoveCardPayload payload, IStateScope scope)
        => new LastMoveState(payload.Card, payload.From, payload.To);
}

// Registered at engine bootstrap:
//   store.RegisterMutator(new PileMoveMutator());
//   store.RegisterMutator(new LastMoveMutator());
```

The handler does not enumerate which mutators run. The registry routes by payload type. Each mutator owns one slice (`PilesState`, `LastMoveState`) and reads sibling slices through `scope` if it needs to. Adding a third reactive slice (e.g., a move counter, an undo log) is a new mutator + a `RegisterMutator` line at bootstrap — the handler does not change.

## Open follow-ups

- **Effect authoring & registration.** The "recipe" a definition holds for instantiating an `Effect` (data, code, graph, or hybrid) is deliberately undesigned. Tracked as a future authoring-feature Model.
- **Effect per-instance state.** Where ongoing-effect counters live — on the `Effect` object, on the entity's `EntityVariableState`, or on a dedicated slice — is left to the ongoing-effects/authoring follow-up.
- **`CommandResult` cancellation shape.** Flag on the base record vs. typed `CancelledResult` subtype. Cosmetic; pin during command-pipeline cleanup.
- **`IHandlerContext` vs. constructor-injected services on handlers.** Aggregating `Store`, `Actions`, `Resolver` into a context type is one option; constructor-injecting them per handler (and dropping the context entirely) is another. The latter declares each handler's needs explicitly and is friendlier to DI, but multiplies wiring cost as services grow. Decision is undesigned — revisit when a second game stresses the surface or when handler count crosses a threshold that makes the cost visible.
- **`ICommandAwaiter` shape — full review.** The TCS-backed type-predicate shape is the simplest mechanic that satisfies the await/resume contract. It is **not load-bearing on this Model's invariants**, but it has not been stress-tested against: error/timeout semantics, multi-actor decisions, and suspension/resumption across engine ticks (save/load). Needs a focused review when the FSM-shaped state model in the next bullet lands.
- **FSM-shaped game state.** Type-only routing in `ICommandAwaiter` is the current mechanism for recognizing in-progress decision commands. The FSM-shaped state model (`PickATargetState`, `MainPhaseState`, etc., each owning their own snapshot-generation surface) is the eventual successor — it would replace type-only routing with richer, self-describing game-state phases. Revisit when the first async-decision-heavy game arrives.
- **Suspension across engine ticks (save/load).** What state must persist if a game saves mid-decision is deferred — the resolver's TCS plus the dispatched action's cause envelope are the candidates, but the persistence shape depends on game-lifecycle design.
- **`GameState` slice catalog.** The Scaffold State binding makes "what slices exist" a concrete question (turn, phase, piles, available-actions, decisions, last-move, etc.). This Model does not pin the catalog.

---

*Below: provenance — feature-internal decisions for this Model. The major historical decision here is the **supersession of the original suspension model**: ADR-0002 (`features/effects/decisions/0002-suspension-model.md`, accepted 2026-04-29) settled explicit `DecisionId`s threaded through state; this Model reverses that choice in favor of type-only routing (settled in §Decision routing — type-only). The original ADR's content is folded as Q1 below with the historical framing intact and the chosen-then-rejected status made explicit. Single-feature scope.*

## Options considered

### Q1 — Suspension and decision routing (originally ADR-0002 — superseded by this Model)

When an effect needs a player choice mid-resolution, the simulation must suspend and resume cleanly across snapshot/restore and replay. Implicit continuations break replay and make per-player views ambiguous.

**The original choice (ADR-0002, accepted 2026-04-29):**

- **A — Implicit continuations via captured async state.** Rejected: the captured state lives outside `GameState` and breaks replay.
- **B — Block the simulation thread until decision arrives.** Rejected: locks presentation and networking.
- **C — Explicit decision IDs threaded through state; effect publishes a `DecisionPrompt`-scoped snapshot, suspends, and resumes when a matching player intent arrives.** Originally chosen as ADR-0002.

**The supersession (this Model, settled 2026 in the §Decision routing — type-only section):**

ADR-0002's "explicit DecisionId" mechanism added a state field (`DecisionPrompt(CommandId)`) and required the snapshot to carry decision-scope metadata so the engine could match an incoming player intent to the awaiting decision. In practice, this duplicated information that the engine could derive from the awaiter's type predicate. The simpler shape — `awaiter.Wait<TCommand>()` registers a closure-captured `cmd is TCommand` predicate; `engine.Dispatch` runs `awaiter.TryResolve(command)` before the normal pipeline; matching commands short-circuit — eliminates `DecisionId` entirely. The mapper becomes decision-unaware; the snapshot never needs a decision-scope discriminator.

- **D — Type-only routing via `ICommandAwaiter`.** No `DecisionId`. Single-open-per-type-hierarchy is a runtime assert. The TCS-backed predicate is the simplest thing that meets the await/resume contract. **Chosen** (supersedes ADR-0002 Option C).

The original ADR-0002 file was folded into this Model on 2026-05-07 as part of the template migration. The historical decision and its supersession are preserved here; the original ADR is not retained as a separate document because the substance lives intact in this provenance.

### Q2 — Service surface for handlers

The handler needs a way to read/write state, write the snapshot, and (for decision handlers) await player input.

- **A — Pass concrete services as parameters on every handler method.** Rejected: parameter list grows linearly with services; ergonomic regression.
- **B — Constructor-inject services per handler.** Considered; defers to the authoring layer (open follow-up). Friendlier to DI but multiplies wiring cost.
- **C — Aggregate services in a single `IHandlerContext` type.** **Chosen for the v1 surface.** Keeps the handler signature simple; the context type itself is the place to add services. Constructor-injection remains an option for a future authoring layer.

### Q3 — Mutation surface

How handlers write state.

- **A — Per-mutation helper methods on a `ctx.Mutate.X(...)` surface.** Rejected: exposes mutator details to handlers; couples handler code to mutator inventory; awkward to extend (every new mutator needs a helper).
- **B — Single `ctx.Store.Execute(payload)` call; payload is a dedicated type; registered mutators fan out.** **Chosen.** Decouples handlers from mutator inventory; payload is the wire form (replay-safe); one payload, N mutators; atomic commit.

### Q4 — Mutator payload typing

Should the payload type be the command type, or a dedicated payload record?

- **A — `Mutator<TState, TCommand>` keyed on command.** Rejected: confuses pipeline-owned (commands) with framework-owned (payloads); commands carry intent metadata that mutators don't need.
- **B — `Mutator<TState, TPayload>` keyed on a dedicated payload record.** **Chosen.** Payloads are framework-owned; commands are pipeline-owned. Handlers translate command intent into payload data.

## Rationale

The chosen direction across Q1–Q4 produces a coherent shape:

- **Type-only decision routing** (Q1) eliminates decision-id ceremony; the engine pre-pipeline awaiter check is the single matching point; the mapper doesn't need to know decisions exist.
- **Aggregated `IHandlerContext`** (Q2) keeps handler signatures readable; can be replaced by constructor-injection later if a game's wiring stresses it.
- **`Store.Execute(payload)` with registered mutators** (Q3) decouples handler from mutator inventory; adding a reactive slice is a new mutator + bootstrap line.
- **Dedicated payload types** (Q4) keeps the wire form clean and replay-safe; commands are pipeline-owned, payloads are framework-owned.

## Consequences

- **Enables:** snapshot/restore and replay across decisions (no ID metadata to persist beyond what's already in state); transparent multiplayer routing (mapper handles all input the same way); deterministic replay of mutations (payload-keyed registry); ergonomic handler code (one context type, `Store.Execute(payload)` for writes); decoupled mutator authoring (new mutator + bootstrap line, no handler changes).
- **Constrains:** every async-aware handler returns `Task<TResult>`; sequential decision nesting only (single-open per type-hierarchy); `IEffectScope` has no mutation surface (effects orchestrate, never mutate); `IHandlerContext` aggregates services (alternative is per-handler constructor injection — open follow-up).
- **Reverts/replaces:** ADR-0002 (`features/effects/decisions/0002-suspension-model.md`, accepted 2026-04-29) is superseded by this Model's §Decision routing — type-only. The DecisionId mechanism is no longer used; the snapshot scope discriminator (`DecisionPrompt(CommandId)`) is dropped from `engine/03-snapshot-pipeline.md`.
- **Cascade:** the `IHandlerContext` aggregation is consumed by every handler; changing it (constructor-injection alternative) would touch every handler implementation. The type-only routing is consumed by `actions/01-command-pipeline` (gateway flow) and `engine/03-snapshot-pipeline` (snapshot shape, scope discriminator dropped).
