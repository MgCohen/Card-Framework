---
type: model
status: settled
last-updated: 2026-05-01
builds-on: [features/effects/models/01-async-dispatch.md, features/engine/models/03-snapshot-pipeline.md]
tags: [#effects, #handler-context, #decisions, #async]
---

# Effect Flow — Services, Decisions, And Await Semantics

This document settles the full mid-execution lifecycle of an effect: what services it sees, how decisions originate and thread through the pipeline, what `await` resumes, and which layer is allowed to mutate state. It pulls together the handler-context placeholders that have been hand-waved across `01-async-dispatch.md`, `03-snapshot-pipeline.md` §7, and the sketch in `support/samples/pile-counter.md` §9.4–9.5 into a single canonical contract.

Builds on:

- [features/effects/models/01-async-dispatch.md](features/effects/models/01-async-dispatch.md) — layer hierarchy, two dispatch paths, `IEffectScope`
- [features/engine/models/03-snapshot-pipeline.md](features/engine/models/03-snapshot-pipeline.md) — `IAvailableActions`, snapshot shape (templates-only; scope discriminator dropped S15)
- [features/actions/models/01-command-pipeline.md](features/actions/models/01-command-pipeline.md) — gateway, intent mapper, command pipeline

---

## 1. Problem

`01-async-dispatch.md` settled `IEffectScope.Dispatch` (effects await `GameAction` results inline) and decisions-as-commands. `03-snapshot-pipeline.md` §7 sketched effect-side symmetry — effects writing the snapshot through `IAvailableActions` — but referenced `IHandlerContext.Actions`, `ctx.DecisionId`, `ctx.AwaitPlayerChoice()` without ever defining `IHandlerContext`. The Pile Counter sample wrote `ctx.Mutate.MoveCardBetweenPiles(...)` and `IStateMutator<MoveCardCommand>` as placeholders, with the Q8 acknowledgement that the shape was undesigned.

Three pieces remain unsettled:

1. **Service surface** for effects and handlers mid-execution: what each layer can read, mutate, dispatch, await.
2. **Decision identity and threading**: who mints the id, how it flows from the requesting effect through the snapshot, the player response, and back to the awaiting effect.
3. **Mutation discipline**: who is allowed to change `GameState`, and through what surface.

This Model pins all three.

---

## 2. The Three Layers, Recapped With Capabilities

Three layers participate in mid-execution game logic. Each has exactly one surface and a deliberately narrow capability set.

| Layer | Read state | Mutate state | Dispatch action | Await player |
|---|---|---|---|---|
| **`Effect`** | Yes (via `scope.State`) | **No** | Yes (`scope.Dispatch`) | No (only by dispatching a decision action) |
| **`ICommandHandler`** | Yes (via `ctx.Store`) | Yes — by invoking mutators | No | Yes (decision handlers only) |
| **`Mutator<TState, TPayload>`** | Yes (current state in; sibling slices via `IStateScope`) | **Yes — only producer of new state** | No | No |

Three principles fall out of this table and are framework invariants:

- **Mutation flows through mutators, never directly.** A mutator's `Execute(state, payload)` returning a new `GameState` is the only path that produces state. Handlers don't write state inline; effects can't write state at all.
- **Only handlers invoke mutators.** Mutators are not auto-fired off command dispatch. The handler for a command decides which mutators to run and with what payload.
- **Effects orchestrate, never mutate.** An effect's job is to sequence dispatches and thread their results. State change happens entirely inside the dispatched actions' handlers.

---

## 3. The `Effect` Type

```csharp
public abstract class Effect
{
    public virtual bool Validate(IEffectScope scope) => true;
    public abstract Task Execute(IEffectScope scope);
}
```

- **First-class type.** Not a convention or a delegate.
- **Per-instance.** Each card *instance* owns its own `Effect` object(s). The card *definition* holds the recipe — how the effect is constructed for new instances. The recipe shape (data, code, graph, or hybrid) is out of scope here; tracked as a follow-up in [§9](#9-open-follow-ups).
- **Internals are opaque to the framework.** The engine only needs `Execute(scope)`; how an effect is authored, parameterized, or composed is the authoring layer's concern.
- **Many per definition.** A card can carry several effects (e.g. battlecry + deathrattle + ongoing aura). Each is a separate `Effect` with its own trigger condition. Subscription lifecycle is the trigger feature's concern (see open question on trigger subscription lifecycle).

Effects can have per-instance state (counters, accumulators) but where that state lives — on the `Effect` object, on the entity's `EntityVariableState`, or on a dedicated slice — is deliberately undesigned at this level. The Model only pins the execution surface.

---

## 4. Service Surfaces

The state read/write surface is **not invented here** — we use the `com.scaffold.states` package's existing types. The framework only adds engine-internal services that don't exist upstream (`IAvailableActions`, `ICommandAwaiter`).

### 4.1 Reads — `IStateScope` (Scaffold)

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
- **`GameState` is not a single record.** State lives as multiple slices (typed `BaseState` subclasses) keyed in the store. There is no `state.Piles` or `state.LastMove` field — those reads become `scope.Get<PilesState>(pileRef)` / `scope.Get<LastMoveState>()`. The catalog of slice types is its own open thread (checkpoint task #2).

### 4.2 Writes — `Store.Execute<TPayload>` + `Mutator<TState, TPayload>` (Scaffold)

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

The shape this gives us:

- **Payload is a dedicated type, not the command.** A "last move" mutator takes a `LastMovePayload`, never `MoveCardCommand`. Payloads are framework-owned; commands are pipeline-owned.
- **One payload, many mutators.** Mutators are registered per `TPayload` at engine bootstrap. When the handler dispatches one payload through `Store.Execute(payload)`, **every mutator registered for that payload type runs**, each producing its own slice's new state. The handler does not enumerate which mutators exist; the registry does.
- **Mutators read sibling slices through `IStateScope`.** A mutator computing the new `LastMoveState` can look up the current `PilesState` via the `scope` parameter on `Change`.
- **Atomic commit.** All mutators that fire from one `Execute` call commit together via the runner's overlay. No half-applied state is visible.

### 4.3 `IEffectScope`

```csharp
public interface IEffectScope
{
    IStateScope State { get; }    // Scaffold; read-only
    Task<TResult> Dispatch<TResult>(GameAction action) where TResult : CommandResult;
    void Cancel(string? reason = null);
    void Replace(GameAction next);   // before-fold only: swap the intercepted command
}
```

- **`State`** — Scaffold `IStateScope`. Effects read slices to choose what to dispatch next. They cannot mutate; they have no `Store` reference.
- **`Dispatch`** — runs the action immediately and inline (before-fold → handler → after-fold → envelope published) per `01-async-dispatch.md` §7. The effect awaits each result before continuing.
- **`Cancel`** — explicit early-out for the effect itself (the effect has decided it cannot continue, e.g. a precondition no longer holds after a prior dispatch). In the trigger context (before-fold), cancels the intercepted command. Distinct from the before-fold cancelling an individual `GameAction` via result; [see §7](#7-cancellation).
- **`Replace`** — before-fold only: signals the pipeline to discard the intercepted command and dispatch `next` as a fresh command through its own fold. Calling `Replace` in an after-fold trigger is a no-op (the command already executed); the pipeline implementation asserts or warns.

### 4.4 `IHandlerContext`

```csharp
public interface IHandlerContext
{
    Store                 Store     { get; }   // Scaffold; read + execute mutators
    IAvailableActions     Actions   { get; }   // mid-execution snapshot writes
    ICommandAwaiter       Resolver  { get; }   // register and resolve decision awaits
    IModifierIdGenerator  Modifiers { get; }   // deterministic ModifierId allocation
}
```

- **`Store`** — concrete Scaffold `Store`. Read via `ctx.Store.Get<TState>()` (inherited from `IStateScope`); write via `ctx.Store.Execute(payload)`. Scaffold does not expose the execute methods on `IStoreScope`; the concrete class is the surface.
- **No `Mutate` surface.** The retired `ctx.Mutate.MoveCardBetweenPiles(...)` line in pile-counter §9.4 is replaced by a single `ctx.Store.Execute(new MoveCardPayload(...))` call.
- **Entity-variable mutations.** For modifier / base-value / variable mutations against entity slices, prefer the `IMutableEntity` surface on a `StateEntity` handle (obtained from the engine's entity registry): `entity.AddModifier(var, modifier, source)` synthesizes the bridge payload and dispatches it. Raw `ctx.Store.Execute(payload)` remains the documented fallback for batch authoring, history-replay tooling, and handle-less paths. Convention details and the six bridge payloads are spelled out in [`entities/01` §"Mutation API conventions"](features/entities/models/01-definition-instance-model.md).
- **`Modifiers`** — deterministic `ModifierId` allocator. Handlers call `ctx.Modifiers.NextId()` to mint the id passed into `AddModifierPayload`. `ModifierId.New()` is forbidden in handler code — `Guid.NewGuid()` is non-deterministic and breaks replay / multiplayer. The counter is engine-owned, snapshotted with engine state, and restored on `LoadSnapshot`.

> **Follow-up.** `IHandlerContext` aggregates three services into one parameter. An alternative is to constructor-inject the services on each handler and drop the context type entirely — fewer indirections, friendlier to DI, lets each handler declare exactly what it needs. Whether that scales better across games (more handlers? more services? composition with abilities?) is undesigned. Revisit when a second game stresses the surface; recorded in [§9](#9-open-follow-ups).

### 4.5 `ICommandAwaiter`

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

> **Follow-up.** The TCS-backed type-predicate shape is the simplest thing that works for the await/resume mechanic and is **not load-bearing on the rest of this Model**. It has not been stress-tested against error/timeout semantics, multi-actor decisions, and suspension/resumption across engine ticks (save/load). Full review when the FSM-shaped state model in [§9](#9-open-follow-ups) lands.

---

## 5. Decision Routing — Type-Only

### 5.1 No decision id

The awaiter registers a `cmd is TCommand` predicate — the generic type parameter is the routing key. There is **no `DecisionId`**, no `CommandId` carried by the awaiter or the snapshot. The mapper produces a normal `SelectTargetCommand` or `PickOptionCommand` without any special annotation; the engine's pre-pipeline check pattern-matches it.

This removes the "where does a decision id originate?" question entirely, and eliminates the `DecisionPrompt(CommandId)` snapshot scope field that previously carried it. The snapshot never needs to signal that a decision is open — the awaiter answers that directly.

### 5.2 At most one open awaiter per type hierarchy

While a decision-handler's `Handle` is awaiting `Resolver.Wait<TCommand>()`, the dispatched action has not completed. Its after-fold has not run. No triggered effect is running. The next `scope.Dispatch` line in the requesting effect cannot start until this one returns. Sequential nesting only.

Single-open per type-hierarchy is a runtime assert. Two `Wait` registrations with overlapping type hierarchies is a programming error.

### 5.3 End-to-end thread

A targeting decision (`RequestTargetAction` → player picks `enemyX` → `DealDamageAction`):

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

---

## 6. The Uniform Boundary

The gateway / intake layer treats all player input identically. Every intent maps to a `GameCommand` via `IntentMapper.Map` and enters `engine.Dispatch`. No scope branch at the gateway; the mapper is decision-unaware. This restores [ADR-0006](features/actions/decisions/0006-player-intent-mapper.md).

### Gateway flow

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

### Engine-level pre-pipeline awaiter check (Q7)

On every `engine.Dispatch`, the engine runs the awaiter check **before** the normal pipeline:

```csharp
// Engine.Dispatch (simplified)
if (awaiter.TryResolve(command))
    return;   // short-circuit: no envelope, no handler, no triggers (Q4)

// normal five-validation-layer pipeline (ADR-0007):
BeginPipeline(command);
```

If the awaiter matches (`cmd is TCommand`), it completes `Wait<TCommand>()` and the engine short-circuits. The matched command never reaches the handler or trigger fold. One logical event (the request action) produces one envelope.

Non-matching commands fall through to the normal pipeline; all five validation layers (ADR-0007) gate them as usual.

### `ICommandHandler.Handle` returns `Task<TResult>`

For decision handlers to await `Resolver.Wait<TCommand>()`, the handler signature must be async-capable:

```csharp
public interface ICommandHandler<TCommand, TResult>
    where TCommand : GameCommand
    where TResult  : CommandResult
{
    bool Validate(TCommand command, IStateScope state, out string? reason);
    Task<TResult> Handle(TCommand command, IHandlerContext ctx);
}
```

Most handlers complete synchronously and return `Task.FromResult(...)`. Only decision handlers genuinely await. This is consistent with `ITriggerPipeline.RunBefore/RunAfter` returning `Task` for the same reason ([`02-replacement-prevention.md`](features/effects/models/02-replacement-prevention.md) §3).

---

## 7. Cancellation

Two cancellation cases exist; both surface through the same channel.

**Action cancelled by the before-fold.** A registered `OnBefore<T>` returns `FoldResult.Cancel(reason)`. The action's handler never runs. The effect's `await scope.Dispatch(...)` resolves with a `CommandResult` carrying the cancellation. The effect inspects the result and decides whether to continue:

```csharp
var damage = await scope.Dispatch(new DealDamageAction { Target = t, Amount = 6 });
if (damage.IsCancelled) return;        // shield absorbed; effect ends gracefully
await scope.Dispatch(new MoveToDiscardAction { Source = source });
```

**Effect aborts itself.** The effect calls `scope.Cancel()` when it knows it cannot continue (e.g. the targeted entity has been removed by a previous dispatch). The effect's `Execute` task completes; no further dispatches run.

The framework invariant: cancellation of one `GameAction` never automatically cancels the orchestrating effect. The effect is the only place that decides whether the chain should continue. This matches the resolution from session 13 of open question Q2 in `01-async-dispatch.md` §8.

`CommandResult` shape for cancellation (flag on the base record vs. typed `CancelledResult` subtype) is a small decision deferred to command-pipeline cleanup; either works for the model here.

---

## 8. Examples

### 8.1 Pick-a-target effect (Fireball)

The effect orchestrates the targeting decision, then the damage dispatch. It does **not** move itself to discard — that is the spell-cast/resolution flow's responsibility (the effect is invoked from inside that flow, not the other way around).

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

### 8.2 Pile Counter under the new shape (replaces sample §9.4)

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

The previous sample §9.5 line `LastMoveMutator : IStateMutator<MoveCardCommand>` is incorrect under this model on two counts: (1) `IStateMutator` is not the real type — Scaffold's `Mutator<TState, TPayload>` is; (2) the type parameter must be a dedicated payload, not the command. Both are recorded as alignment debt.

---

## 9. Open follow-ups

- **Effect authoring & registration.** The "recipe" a definition holds for instantiating an `Effect` (data, code, graph, or hybrid) is deliberately undesigned. Tracked as a future authoring-feature Model.
- **Effect per-instance state.** Where ongoing-effect counters live — on the `Effect` object, on the entity's `EntityVariableState`, or on a dedicated slice — is left to the ongoing-effects/authoring follow-up. `03-ongoing-effects.md` already settled the attachment-entity shape; this Model only reaffirms that effects do not get a hidden mutation channel.
- **`CommandResult` cancellation shape.** Flag on the base record vs. typed `CancelledResult` subtype. Cosmetic; pin during command-pipeline cleanup.
- **`IHandlerContext` vs. constructor-injected services on handlers.** Aggregating `Store`, `Actions`, `Resolver` into a context type is one option; constructor-injecting them per handler (and dropping the context entirely) is another. The latter declares each handler's needs explicitly and is friendlier to DI, but multiplies wiring cost as services grow. Decision is undesigned — revisit when a second game stresses the surface or when handler count crosses a threshold that makes the cost visible. Hooked from [§4.4](#44-ihandlercontext).
- **`ICommandAwaiter` shape — full review.** The TCS-backed type-predicate shape is the simplest mechanic that satisfies the await/resume contract. It is **not load-bearing on this Model's invariants**, but it has not been stress-tested against: error/timeout semantics, multi-actor decisions, and suspension/resumption across engine ticks (save/load). Needs a focused review when the FSM-shaped state model in the next bullet lands. Hooked from [§4.5](#45-icommandawaiter).
- **FSM-shaped game state.** Type-only routing in `ICommandAwaiter` is the current mechanism for recognizing in-progress decision commands. The FSM-shaped state model (`PickATargetState`, `MainPhaseState`, etc., each owning their own snapshot-generation surface) is the eventual successor — it would replace type-only routing with richer, self-describing game-state phases. Revisit when the first async-decision-heavy game arrives. Carries forward from `03-snapshot-pipeline.md` §9.
- **Suspension across engine ticks (save/load).** What state must persist if a game saves mid-decision is deferred — the resolver's TCS plus the dispatched action's cause envelope are the candidates, but the persistence shape depends on game-lifecycle design (checkpoint task #12).
- **`GameState` slice catalog.** The Scaffold State binding ([§4.1](#41-reads--istatescope-scaffold)) makes "what slices exist" a concrete question (turn, phase, piles, available-actions, decisions, last-move, etc.). Tracked as checkpoint task #2; this Model does not pin the catalog.

---

## 10. Invariants

Pinned by this Model and referenceable from downstream docs:

1. **No decision id.** Type-only routing: the `TCommand` type parameter in `ICommandAwaiter.Wait<TCommand>()` is the routing key. There is no `DecisionId` and no `CommandId` carried by the awaiter or the snapshot.
2. **At most one open awaiter per type hierarchy.** Single-open is a runtime assert. Sequential nesting only.
3. **Uniform boundary.** All player input goes `intent → IntentMapper.Map(template, choice) → GameCommand → engine.Dispatch`. The mapper switches on template subtype only and is decision-unaware. The engine's pre-pipeline awaiter check intercepts matched commands before the normal pipeline.
4. **Mutation only through mutators.** Scaffold's `Mutator<TState, TPayload>.Change` is the only producer of new slice state. Only handlers invoke mutators (via `Store.Execute(payload)`); effects cannot mutate.
5. **Mutator payloads are dedicated types**, not the command. One payload may fan out to N registered mutators; `Store.Execute(payload)` runs every mutator registered for that payload type and commits atomically.
6. **`IHandlerContext` exposes** Scaffold `Store`, `IAvailableActions`, `ICommandAwaiter`. No mutate-helper surface; no decision-id field.
7. **`IEffectScope` exposes** Scaffold `IStateScope`, `Dispatch`, `Cancel`, `Replace`. No mutator/store surface; no awaiter surface. `Replace` is meaningful only in before-fold triggers; calling it in after-fold or standalone effects is a no-op / runtime warning.
8. **`ICommandHandler.Handle` returns `Task<TResult>`**. Synchronous handlers return `Task.FromResult(...)`.
9. **`Effect.Validate` is a pre-execution gate.** `virtual bool Validate(IEffectScope scope) => true`. Returning `false` prevents `Execute` from running. The default always passes — simple effects don't override it.

---

## 11. Related

- [features/effects/models/01-async-dispatch.md](features/effects/models/01-async-dispatch.md) — layer hierarchy, `IEffectScope.Dispatch`, decisions-as-commands
- [features/effects/models/02-replacement-prevention.md](features/effects/models/02-replacement-prevention.md) — `FoldResult.Cancel`, `Task`-returning pipeline
- [features/engine/models/03-snapshot-pipeline.md](features/engine/models/03-snapshot-pipeline.md) — `IAvailableActions`, snapshot scope discriminator
- [features/actions/models/01-command-pipeline.md](features/actions/models/01-command-pipeline.md) — gateway, `IIntentMapper`, command pipeline
- [support/samples/pile-counter.md](support/samples/pile-counter.md) — sample game; §9.4–9.5 carry alignment debt against this Model
- [ledgers/ALIGNMENT-DEBT.md](ledgers/ALIGNMENT-DEBT.md) — cascade entries from this Model (S15 §6 patch discharged S16)
