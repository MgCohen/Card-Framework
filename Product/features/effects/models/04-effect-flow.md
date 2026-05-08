---
type: model
status: settled
references: [features/effects/models/01-async-dispatch.md, features/engine/models/03-snapshot-pipeline.md, features/actions/models/01-command-pipeline.md]
tags: [#effects, #handler-context, #decisions, #async]
---

# Effect Flow — model

## What it is

The full mid-execution lifecycle of an effect: what services it sees, how decisions originate and thread through the pipeline, what `await` resumes, and which layer is allowed to mutate state. Pulls together the handler-context placeholders that have been hand-waved across `01-async-dispatch.md`, the snapshot pipeline, and earlier samples into a single canonical contract.

```
Effect.Execute(scope)
  └─ scope.Dispatch(RequestTargetAction { Filter: EnemyFilter })
        └─ engine routes → RequestTargetHandler.Handle(action, ctx)
              ctx.Actions.Set(new AvailableActionsSnapshot(new[] { new ChooseTargetTemplate(legal) }));
              var cmd = await ctx.Resolver.Wait<SelectTargetCommand>();   // suspended
              return new RequestTargetResult { Target = cmd.Target };
        ▲
        │   Player intent → IntentMapper.Map(template, choice) → SelectTargetCommand
        │   engine.Dispatch(SelectTargetCommand)
        │     pre-pipeline: awaiter.TryResolve(SelectTargetCommand) → match → short-circuit
        │     Wait<SelectTargetCommand> task completes → handler resumes
        │
  └─ await scope.Dispatch(DealDamageAction { Target: cmd.Target, Amount: 6 })
```

Three layers participate, each with a deliberately narrow capability set: **`Effect`** orchestrates and reads (no mutation), **`ICommandHandler`** reads and mutates via mutators (and may await player input), **`Mutator<TState, TPayload>`** is the only producer of new slice state.

## Contracts

### Three layers, capabilities

| Layer | Read state | Mutate state | Dispatch action | Await player |
|---|---|---|---|---|
| **`Effect`** | Yes (via `scope.State`) | **No** | Yes (`scope.Dispatch`) | No (only by dispatching a decision action) |
| **`ICommandHandler`** | Yes (via `ctx.Store`) | Yes — by invoking mutators | No | Yes (decision handlers only) |
| **`Mutator<TState, TPayload>`** | Yes (current state in; sibling slices via `IStateScope`) | **Yes — only producer of new state** | No | No |

Three principles fall out of this table and are framework invariants:

- **Mutation flows through mutators, never directly.** A mutator's `Change(state, payload, scope)` returning a new `TState` is the only path that produces state. Handlers don't write state inline; effects can't write state at all.
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
- **Per-instance.** Each card *instance* owns its own `Effect` object(s). The card *definition* holds the recipe — how the effect is constructed for new instances. The recipe shape (data, code, graph, or hybrid) is out of scope here; tracked as a follow-up.
- **Internals are opaque to the framework.** The engine only needs `Execute(scope)`; how an effect is authored, parameterized, or composed is the authoring layer's concern.
- **Many per definition.** A card can carry several effects (e.g. battlecry + deathrattle + ongoing aura). Each is a separate `Effect` with its own trigger condition. Subscription lifecycle is the trigger feature's concern.

Effects can have per-instance state (counters, accumulators) but where that state lives — on the `Effect` object, on the entity's `EntityState`, or on a dedicated slice — is deliberately undesigned at this level. The Model only pins the execution surface.

### Service surfaces

The state read/write surface is **not invented here** — we use the `com.scaffold.states` package's existing types. The framework only adds engine-internal services that don't exist upstream (`IAvailableActions`, `ICommandAwaiter`).

#### Reads — `IStateScope` (Scaffold)

```csharp
// from Scaffold.States
public interface IStateScope
{
    TState Get<TState>() where TState : BaseState;
    TState Get<TState>(Reference? reference) where TState : BaseState;
    IEnumerable<TState> GetAll<TState>() where TState : BaseState;
}

public interface IStoreScope : IStateScope
{
    IStateEventHandler Events { get; }
}
```

- `IStateScope` — read-only access to slices. Handed to anything that should not mutate (effects, `GameRules`, presentation, projections, mutators reading sibling slices).
- `IStoreScope` — adds the event handler for subscriptions. Used where presentation/observers need it.
- **`GameState` is not a single record.** State lives as multiple slices (typed `BaseState` subclasses) keyed in the store. There is no `state.Piles` or `state.LastMove` field — those reads become `scope.Get<PileState>(pileRef)` / `scope.Get<LastMoveState>()`. The catalog of slice types is its own open thread (checkpoint task #2).

#### Writes — `Store.Execute<TPayload>` + `Mutator<TState, TPayload>` (Scaffold)

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

The framework hands handlers the concrete `Store`; they call `ctx.Store.Execute(payload)` to write. Effects do **not** receive `Store` for writes — they get `IStateScope` (read-only) on `IEffectScope`.

The shape this gives us:

- **`TPayload` types are boundary-correct for `Reference` / `Ref<T>` use.** Mutator payloads are the Store's wire form — they persist, serialize, and transport analogously to `GameCommand`s. `Ref<Card>`, `Ref<Zone>`, and the polymorphic `Reference` are boundary-correct in payload records per [ADR-0005](Product/decisions/0005-objects-over-ids.md). Handler code *building* a payload should derive refs from held objects (`card.SelfRef`, `zone.SelfRef`) rather than storing raw refs in locals.
- **Payload is a dedicated type, not the command.** A "last move" mutator takes a `LastMovePayload`, never `MoveCardCommand`. Payloads are framework-owned; commands are pipeline-owned.
- **One payload, many mutators.** Mutators are registered per `TPayload` at engine bootstrap. When the handler dispatches one payload through `Store.Execute(payload)`, **every mutator registered for that payload type runs**, each producing its own slice's new state. The handler does not enumerate which mutators exist; the registry does.
- **Mutators read sibling slices through `IStateScope`.** A mutator computing the new `LastMoveState` can look up the current `ZoneContentsState`, `PileState`, etc. via the `scope` parameter on `Change`.
- **Atomic commit.** All mutators that fire from one `Execute` call commit together via the runner's overlay. No half-applied state is visible.

#### `IEffectScope`

```csharp
public interface IEffectScope
{
    IStateScope       State     { get; }    // Scaffold; read-only
    Store             Store     { get; }    // for catalog reads only — no Execute
    IModifierFactory  Modifiers { get; }    // deterministic modifier-entry construction

    Task<TResult> Dispatch<TResult>(GameAction action) where TResult : CommandResult;
    void Cancel(string? reason = null);
    void Replace(GameAction next);   // before-fold only: swap the intercepted command
}
```

- **`State`** — Scaffold `IStateScope`. Effects read slices to choose what to dispatch next.
- **`Store`** — exposed for catalog reads (`scope.Store.Catalog.Resolve(ref)`) only. Effects do not call `Execute`/`ExecuteBatch`; mutation goes through `Dispatch`-ed actions whose handlers do the writes. Use `scope.Store.Catalog.Resolve(cardRef)` or a typed registry to obtain handles for variable reads (`card.Storage.GetVariable<int>(Vars.Attack)`).
- **`Modifiers`** — modifier-entry factory, same surface as on `IHandlerContext`. Effects that need to construct a modifier entry to pass into a `GameAction`'s payload (e.g. an `ApplyAuraAction { Buff = scope.Modifiers.Create(...) }`) use it here so the deterministic id is allocated by the effect even though the install happens in the action's handler.
- **`Dispatch`** — runs the action immediately and inline (before-fold → handler → after-fold → envelope published) per [`01-async-dispatch.md`](features/effects/models/01-async-dispatch.md). The effect awaits each result before continuing.
- **`Cancel`** — explicit early-out for the effect itself (the effect has decided it cannot continue, e.g. a precondition no longer holds after a prior dispatch). In the trigger context (before-fold), cancels the intercepted command. Distinct from the before-fold cancelling an individual `GameAction` via result.
- **`Replace`** — before-fold only: signals the pipeline to discard the intercepted command and dispatch `next` as a fresh command through its own fold. Calling `Replace` in an after-fold trigger is a no-op (the command already executed); the pipeline implementation asserts or warns.

#### `IHandlerContext`

```csharp
public interface IHandlerContext
{
    Store              Store     { get; }   // Scaffold; read + execute mutators + catalog
    IAvailableActions  Actions   { get; }   // mid-execution snapshot writes
    ICommandAwaiter    Resolver  { get; }   // register and resolve decision awaits
    IModifierFactory   Modifiers { get; }   // deterministic modifier-entry construction
}
```

- **`Store`** — concrete Scaffold `Store`. Read via `ctx.Store.Get<TState>()` (inherited from `IStateScope`); write via `ctx.Store.Execute(payload)`; resolve handles via `ctx.Store.Catalog.Resolve(ref)`; spawn entities via `ctx.Store.Spawn<TDef>(def)` (the `StoreEntities.Spawn` extension). Scaffold does not expose the execute methods on `IStoreScope`; the concrete class is the surface.
- **No `Mutate` surface.** Handlers issue `ctx.Store.Execute(payload)` / `ctx.Store.ExecuteBatch(...)` calls. The pile-counter sample's handler issues an atomic batch combining the framework's `MoveCardMutation` (CompositeAction) with game-side payloads (`AdjustPileCounterPayload`, `SetLastMovePayload`).
- **Entity-variable mutations.** For modifier / variable / structural mutations against entity slices, prefer the `IEntityVariableStorage` surface on a handle obtained via `ctx.Store.Catalog.Resolve(ref)`. Build modifier entries through the `Modifiers` factory and pass them to `card.Storage.AddModifier(entry)`. Raw `ctx.Store.Execute(payload)` remains the documented fallback for batch authoring, history-replay tooling, and refless paths. Full handler-side surface in [`entities/02-runtime-surface.md`](features/entities/models/02-runtime-surface.md).
- **Spawn pathway.** `ctx.Store.Spawn<TDef>(def)` (the `StoreEntities.Spawn` extension) allocates a deterministic `Ref<EntityInstance<TDef>>`, registers an empty `EntityState` slice, constructs the handle with `StoreVariableStorage`, and registers it in `ctx.Store.Catalog`. Returns `(instance, ref)`. Typed subclass spawn helpers (`Card.Spawn`) wrap this. See [`entities/02`](features/entities/models/02-runtime-surface.md).
- **`Modifiers`** — modifier factory. `Create(key, modifier, source)` returns a fully-populated `ActiveModifier` with a deterministic `ModifierId` and optional source baked in. The id comes from an engine-owned counter (snapshotted, restored on `LoadSnapshot`). Constructing modifier ids ad-hoc via `Guid.NewGuid()` is forbidden in handler / framework code — it breaks replay and multiplayer determinism.

#### `ICommandAwaiter`

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

The awaiter registers a `cmd is TCommand` predicate — the generic type parameter is the routing key. There is **no `DecisionId`**, no `CommandId` carried by the awaiter or the snapshot. The mapper produces a normal `SelectTargetCommand` or `PickOptionCommand` without any special annotation; the engine's pre-pipeline check pattern-matches it.

This removes the "where does a decision id originate?" question entirely, and eliminates any `DecisionPrompt(CommandId)` snapshot scope field that previously carried it. The snapshot never needs to signal that a decision is open — the awaiter answers that directly.

While a decision-handler's `Handle` is awaiting `Resolver.Wait<TCommand>()`, the dispatched action has not completed. Its after-fold has not run. No triggered effect is running. The next `scope.Dispatch` line in the requesting effect cannot start until this one returns. **Sequential nesting only.**

Single-open per type-hierarchy is a runtime assert. Two `Wait` registrations with overlapping type hierarchies is a programming error.

### The uniform boundary

The gateway / intake layer treats all player input identically. Every intent maps to a `GameCommand` via `IntentMapper.Map` and enters `engine.Dispatch`. No scope branch at the gateway; the mapper is decision-unaware.

```
intent → IntentMapper.Map(template, choice) → GameCommand → engine.Dispatch
```

`IIntentMapper.Map` switches on the typed template subtype only:

```csharp
public GameCommand Map(ActionTemplate template, PlayerChoice choice) => template switch
{
    MoveCardTemplate     m => new MoveCardCommand(m.Card, state.LocateCard(m.Card), (Ref<Zone>)choice.Targets[0]),
    ChooseTargetTemplate t => new SelectTargetCommand(choice.Targets[0]),
    _ => throw new InvalidOperationException($"unknown template: {template}")
};
```

No snapshot scope inspection. The mapper produces `SelectTargetCommand` whether or not a decision is open — it does not know.

#### Engine-level pre-pipeline awaiter check

On every `engine.Dispatch`, the engine runs the awaiter check **before** the normal pipeline:

```csharp
// Engine.Dispatch (simplified)
if (awaiter.TryResolve(command))
    return;   // short-circuit: no envelope, no handler, no triggers

// normal five-validation-layer pipeline (ADR-0007):
BeginPipeline(command);
```

If the awaiter matches (`cmd is TCommand`), it completes `Wait<TCommand>()` and the engine short-circuits. The matched command never reaches the handler or trigger fold. One logical event (the request action) produces one envelope.

Non-matching commands fall through to the normal pipeline; all five validation layers ([ADR-0007](Product/decisions/0007-five-validation-layers.md)) gate them as usual.

#### `ICommandHandler.Handle` returns `Task<TResult>`

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

Most handlers complete synchronously and return `Task.FromResult(...)`. Only decision handlers genuinely await. This is consistent with `ITriggerPipeline.RunBefore/RunAfter` returning `Task` for the same reason ([`02-replacement-prevention.md`](features/effects/models/02-replacement-prevention.md)).

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

1. **No decision id.** Type-only routing: the `TCommand` type parameter in `ICommandAwaiter.Wait<TCommand>()` is the routing key. There is no `DecisionId` and no `CommandId` carried by the awaiter or the snapshot.
2. **At most one open awaiter per type hierarchy.** Single-open is a runtime assert. Sequential nesting only.
3. **Uniform boundary.** All player input goes `intent → IntentMapper.Map(template, choice) → GameCommand → engine.Dispatch`. The mapper switches on template subtype only and is decision-unaware. The engine's pre-pipeline awaiter check intercepts matched commands before the normal pipeline.
4. **Mutation only through mutators.** Scaffold's `Mutator<TState, TPayload>.Change` is the only producer of new slice state. Only handlers invoke mutators (via `Store.Execute(payload)`); effects cannot mutate.
5. **Mutator payloads are dedicated types**, not the command. One payload may fan out to N registered mutators; `Store.Execute(payload)` runs every mutator registered for that payload type and commits atomically.
6. **`IHandlerContext` exposes** Scaffold `Store`, `IAvailableActions`, `ICommandAwaiter`. No mutate-helper surface; no decision-id field.
7. **`IEffectScope` exposes** Scaffold `IStateScope`, `Dispatch`, `Cancel`, `Replace`. No mutator/store surface; no awaiter surface. `Replace` is meaningful only in before-fold triggers; calling it in after-fold or standalone effects is a no-op / runtime warning.
8. **`ICommandHandler.Handle` returns `Task<TResult>`**. Synchronous handlers return `Task.FromResult(...)`.
9. **`Effect.Validate` is a pre-execution gate.** `virtual bool Validate(IEffectScope scope) => true`. Returning `false` prevents `Execute` from running. The default always passes — simple effects don't override it.

## Decisions

*n/a — no elevated ADR.* (The original suspension-model ADR-0002 is superseded by type-only routing, which applies to a single feature; the supersede + the chosen mechanic are folded as Q1 below.)

## Examples

### Pick-a-target effect (Fireball)

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

### Pile-counter handler (atomic batch)

One batch, two distinct payload types, three game-side mutator runs (counter delta on source pile, counter delta on destination pile, last-move record). The handler does a single `ExecuteBatch` call; Scaffold's registry fans out per payload type and commits everything atomically alongside the framework `MoveCardMutation`'s composite expansion.

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
        var card   = ctx.Store.Catalog.Resolve(move.Card);
        var source = ctx.Store.Catalog.Resolve(move.From);
        var dest   = ctx.Store.Catalog.Resolve(move.To);
        var number = card.Storage.GetVariable<int>(Vars.Number);

        // Atomic batch — framework MoveCardMutation handles the zone move;
        // game-side payloads update the cached counters and last-move memory.
        ctx.Store.ExecuteBatch(new object[]
        {
            MoveCardMutation.AtTop(card, source, dest),
            new AdjustPileCounterPayload(move.From, -number),
            new AdjustPileCounterPayload(move.To,   +number),
            new SetLastMovePayload(new LastMove(move.Card, move.From, move.To)),
        });

        return Task.FromResult(new MoveCardResult(move.Card));
    }
}

public sealed record AdjustPileCounterPayload(Ref<Zone> Pile, int Delta) : IPayloadReference
{
    public Reference GetReference() => Pile;
}

public sealed record SetLastMovePayload(LastMove Move);

// Registered mutators — each fires from its matching payload.
public class PileCounterMutator : Mutator<PileState, AdjustPileCounterPayload>
{
    public override PileState Change(PileState state, AdjustPileCounterPayload payload, IStateScope scope)
        => state with { Counter = state.Counter + payload.Delta };
}

public class LastMoveMutator : Mutator<LastMoveState, SetLastMovePayload>
{
    public override LastMoveState Change(LastMoveState state, SetLastMovePayload payload, IStateScope scope)
        => new LastMoveState(payload.Move);
}

// Registered at app bootstrap via the game's IInstaller.
```

The handler does not enumerate which mutators run. The registry routes by payload type. Each mutator owns one slice and reads sibling slices through `scope` if it needs to. The framework's `MoveCardMutation` `CompositeAction` handles the zone-move expansion (`ZoneContentsState` updates + `CardMapState` rotation) — game-authored mutators only handle game-side concerns. Adding a third reactive slice (move counter, undo log) is a new payload + mutator + `Register` line; the handler does not change.

## Open follow-ups

- **Effect authoring & registration.** The "recipe" a definition holds for instantiating an `Effect` (data, code, graph, or hybrid) is deliberately undesigned. Tracked as a future authoring-feature Model.
- **Effect per-instance state.** Where ongoing-effect counters live — on the `Effect` object, on the entity's `EntityState`, or on a dedicated slice — is left to the ongoing-effects/authoring follow-up. [`03-ongoing-effects.md`](features/effects/models/03-ongoing-effects.md) settles the attachment-entity shape; this Model only reaffirms that effects do not get a hidden mutation channel.
- **`CommandResult` cancellation shape.** Flag on the base record vs. typed `CancelledResult` subtype. Cosmetic; pin during command-pipeline cleanup.
- **`IHandlerContext` vs. constructor-injected services on handlers.** Aggregating `Store`, `Actions`, `Resolver` into a context type is one option; constructor-injecting them per handler (and dropping the context entirely) is another. The latter declares each handler's needs explicitly and is friendlier to DI, but multiplies wiring cost as services grow. Decision is undesigned — revisit when a second game stresses the surface or when handler count crosses a threshold that makes the cost visible.
- **`ICommandAwaiter` shape — full review.** The TCS-backed type-predicate shape is the simplest mechanic that satisfies the await/resume contract. It is **not load-bearing on this Model's invariants**, but it has not been stress-tested against: error/timeout semantics, multi-actor decisions, and suspension/resumption across engine ticks (save/load). Needs a focused review when the FSM-shaped state model lands.
- **FSM-shaped game state.** Type-only routing in `ICommandAwaiter` is the current mechanism for recognizing in-progress decision commands. The FSM-shaped state model (`PickATargetState`, `MainPhaseState`, etc., each owning their own snapshot-generation surface) is the eventual successor — it would replace type-only routing with richer, self-describing game-state phases. Revisit when the first async-decision-heavy game arrives.
- **Suspension across engine ticks (save/load).** What state must persist if a game saves mid-decision is deferred — the resolver's TCS plus the dispatched action's cause envelope are the candidates, but the persistence shape depends on game-lifecycle design (checkpoint task #12).
- **`GameState` slice catalog.** The Scaffold State binding makes "what slices exist" a concrete question (turn, phase, piles, available-actions, decisions, last-move, etc.). Tracked as checkpoint task #2; this Model does not pin the catalog.

---

*Below: provenance — how the suspension model was settled and then superseded. Single-feature scope (no project-wide ADR applies; folded into Model provenance per §4.5 on 2026-05-08). Original ADR-0002 landing 2026-04-29; superseded by the type-only routing decision below on the same Model timeline.*

## Options considered

### Q1 — Suspension model

When an effect needs a player choice mid-resolution, the simulation must suspend and resume cleanly across snapshot/restore and replay. Implicit continuations break replay and make per-player views ambiguous.

- **A — Implicit continuations via captured async state.** Rejected — captured state lives outside `GameState` and breaks replay.
- **B — Block the simulation thread until decision arrives.** Rejected — locks presentation and networking.
- **C — Explicit decision IDs threaded through state; effect publishes a `DecisionPrompt`-scoped snapshot, suspends, and resumes when a matching player intent arrives.** Originally chosen (ADR-0002, 2026-04-29). **Superseded** by the type-only routing decision.
- **D — Type-only routing via `ICommandAwaiter.Wait<TCommand>()`; decision handler awaits a `cmd is TCommand` predicate; engine pre-pipeline awaiter check short-circuits matched commands. No `DecisionId`, no snapshot scope discriminator.** **Chosen.**

The suspension *requirement* (snapshot/restore-safe, replayable, multiplayer-correct) is unchanged; the *mechanism* is simpler. The `DecisionPrompt` snapshot scope discriminator is dropped; the handler-side awaiter is the resume mechanism instead.

## Rationale

Type-only routing eliminates two things that the original ID-threaded model required: (1) where the decision id originates, and (2) a snapshot scope discriminator that signals "a decision is open." Both questions disappear when the awaiter answers them: the engine's pre-pipeline `awaiter.TryResolve(cmd)` check matches by type, completes the TCS, and short-circuits. The mapper stays decision-unaware; the snapshot stays scope-agnostic.

The trade is single-open-per-type-hierarchy: two concurrent decisions for the same command type are not supported. Sequential nesting is fine. This is a hard constraint, but in practice every decision in flight is one specific question for one specific actor, so it has not bitten any sample game. If multi-actor or overlapping decisions become load-bearing, the FSM-shaped state model (Open follow-ups) is the chosen successor.

## Consequences

- **Enables** snapshot/restore and replay across decisions (no async state outside the envelope stream); transparent multiplayer routing of decisions to the right actor (the awaiting handler is the actor's handler); a drastically simpler mapper (decision-unaware); a drastically simpler snapshot (no scope discriminator).
- **Constrains** every decision to be a normal `GameAction` whose handler awaits `Resolver.Wait<TCommand>()`; one open awaiter per type hierarchy at a time (runtime assert); the `ICommandAwaiter` mechanism is not stress-tested against multi-actor / error / timeout — full review tracked in Open follow-ups.
