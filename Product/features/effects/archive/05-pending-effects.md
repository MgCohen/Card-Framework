---
type: model
status: abandoned
archived_on: 2026-10-18
reason: "Written for the old state model (slices, Ref<T>, MatchStateSlice) and never built. Read it for the open questions, not as the contract; the replacement design comes from Product/spikes/framework-roadmap.md."
references: [features/effects/archive/01-effect.md, features/effects/archive/02-effect-scope.md, features/effects/archive/06-request-system.md, features/engine/archive/01-game-engine.md, features/engine/archive/03-game-rules-runtime.md, features/engine/archive/07-match-state.md, features/state/archive/state.md, decisions/0011-effect-orchestration-dispatch-requests.md]
tags: [#effects, #model, #pending-effects, #drain, #fsm]
---

> **Archived 2026-10-18.** Written for the old state model (slices, Ref<T>, MatchStateSlice) and never built. Read it for the open questions, not as the contract; the replacement design comes from Product/spikes/framework-roadmap.md.

# Pending Effects — model

> **Amendment (2026-05-11) per spike Q-walk locks.** Drain-policy framing clarified: when `RequestsSlice.Open` is non-empty, what pauses is **state transitions** (default `Evaluate` returns `Current`), **not** the entire drain. The engine continues to process the GameCommand queue and drain `PendingEffectsSlice`. Effects awaiting requests park on their own `await scope.Dispatch(request)` calls; other effects and other commands proceed normally. This enables concurrent requests via `Task.WhenAll` for patterns like simultaneous mulligan, multi-actor response windows, parallel auctions. The narrative below referring to "drain pauses on open requests" should be read as "state-transitions pause."

## What it is

`PendingEffectsSlice` is the framework-owned universal queue of effect instances awaiting drain. Per [ADR-0011](Product/decisions/0011-effect-orchestration-dispatch-requests.md) (D15), every effect-firing path goes through this slice: cast handlers push when a spell goes "on the stack," trigger reactions push when an `OnAfter<T>` reaction wants to fire an effect, FSM-driven runs push when a `MatchState`'s `EnterCommands` include an effect launch. The engine drains it when its drain-policy composition allows; only the engine holds an `IEffectPipeline` reference and calls `Run` on dequeued instances.

```
push paths               PendingEffectsSlice              drain path
─────────────            ──────────────────              ─────────────
cast handler ─┐                                          ┌─ engine drain loop
trigger fold ─┼─▶ ImmutableList<EffectInstance>  ─▶     │  pop top
EnterCommands ┘                                         │  effectPipeline.Run(instance, scope)
                                                        │  await IEffect.Run
                                                        └─ repeat until empty / paused
```

## Contracts

### The slice

```csharp
public sealed record PendingEffectsSlice(
    ImmutableList<EffectInstance> Pending
) : BaseState;
```

- **Framework-owned, auto-seeded** at `Build()` with `Pending = ImmutableList<EffectInstance>.Empty`.
- **Ordered.** Order on the list is the order of execution. Most card games drain **LIFO** (stack-shaped: `Pending[^1]` is the next to fire); FIFO is valid for queue-shaped games (Hearthstone-style "earliest-pushed triggers fire first"). `ImmutableList<T>` keeps the ordering decision in the runtime — not baked into the slice's shape.
- **One subscription channel.** Presentation, tooling, and replay tooling subscribe to `PendingEffectsSlice` directly; clients can render "what's about to fire?" by reading `Pending`.

### `EffectInstance`

```csharp
public sealed record EffectInstance(
    Reference Source,    // the entity that fired this effect (a Card, an Actor, a synthetic world ref)
    EffectId  Id,        // which effect on the source's effect catalog
    object?   Payload    // bound parameters captured at fire time (game-shaped)
);
```

The wire-form record. Flat, immutable, snapshot-friendly. Carries:

- **`Source`** — typed `Reference` to the entity that fired the effect. For a spell going on the stack, this is the spell card; for a triggered ability, the owning permanent; for a world-rule trigger, a synthetic `WorldRef` actor.
- **`Id`** — `EffectId` (a typed identifier; opaque to the framework). Resolves through `IEffectDefinitionCatalog` to the runnable `IEffect` definition. Sources can carry multiple effects (battlecry + deathrattle + aura); `Id` discriminates.
- **`Payload`** — game-shaped bindings captured at fire time. For a cast spell, the chosen pre-play targets, `X` cost, mode selection; for an after-fold trigger, the intercepted command. Authoring shape (typed payload classes vs `object?` bag) is `oq-effect-authoring`.

### `IEffectPipeline`

```csharp
public interface IEffectPipeline
{
    Task Run(EffectInstance instance, IEffectScope scope);
}
```

The engine's effect-firing surface. Default implementation is `InlineEffectPipeline`:

```csharp
public sealed class InlineEffectPipeline : IEffectPipeline
{
    private readonly IEffectDefinitionCatalog catalog;

    public InlineEffectPipeline(IEffectDefinitionCatalog catalog) { this.catalog = catalog; }

    public async Task Run(EffectInstance instance, IEffectScope scope)
    {
        var effect = catalog.Resolve(instance);   // builds an IEffect from (Source, Id, Payload)
        await effect.Run(scope);
    }
}
```

Games may swap implementations in DI for stack-resolution timing, deferred batching, etc., but **the only call site is the engine's drain loop** (per [ADR-0011 lock 1](Product/decisions/0011-effect-orchestration-dispatch-requests.md)). Cast handlers, triggers, and runtime code never hold the `IEffectPipeline` reference; they push to `PendingEffectsSlice` and the engine drains.

### Push payloads

Pushing to `PendingEffectsSlice` is a normal mutator dispatch through `Store.Execute`. The framework ships one payload:

```csharp
public sealed record PushPendingEffectPayload(EffectInstance Instance);
public sealed record PopPendingEffectPayload();

public sealed class PushPendingEffectMutator
    : Mutator<PendingEffectsSlice, PushPendingEffectPayload>
{
    public override PendingEffectsSlice Change(
        PendingEffectsSlice current,
        PushPendingEffectPayload payload,
        IStateScope scope)
        => new(current.Pending.Add(payload.Instance));
}

public sealed class PopPendingEffectMutator
    : Mutator<PendingEffectsSlice, PopPendingEffectPayload>
{
    public override PendingEffectsSlice Change(
        PendingEffectsSlice current,
        PopPendingEffectPayload payload,
        IStateScope scope)
        => current.Pending.IsEmpty
           ? current
           : new(current.Pending.RemoveAt(current.Pending.Count - 1));   // LIFO default
}
```

LIFO is the framework default for the `Pop` mutator. Games with FIFO semantics (Hearthstone trigger queue) ship their own payload + mutator (e.g., `PopFrontPendingEffectPayload`) and override the drain order via the runtime's `Evaluate`.

### The engine drain loop

The engine's main loop (canonical contract in [`engine/01-game-engine.md`](features/engine/archive/01-game-engine.md)) checks `PendingEffectsSlice` after every command drain. Simplified shape:

```text
engine.Drain():
  while (pending command queue is non-empty OR PendingEffectsSlice is non-empty):
    if pending command queue is non-empty:
      action = pending.Dequeue()
      pipeline.Execute(action)       // drains one GameAction
    else:
      if !DrainPolicy.AllowsEffectDrain(State):
        break                         // pause; wait for input or state change
      instance = PendingEffectsSlice.Top      // peek
      Store.Execute(new PopPendingEffectPayload())
      await effectPipeline.Run(instance, scope)
```

`DrainPolicy` is a composition of state-linked checks (see §Drain policy below); the default `Evaluate` body (per [ADR-0009](Product/decisions/0009-engine-as-fsm.md)) keeps the FSM stationary while pending effects exist, so transitions don't fire mid-drain.

### Drain policy

Whether the engine drains the next pending effect is composed:

```
DrainAllowed =
   no SBA preempts
   ∧ RequestsSlice empty (no open request blocking the actor)
   ∧ MatchState.AllowsDrain (optional game-state hook; default = true)
```

The default `Evaluate` body (per [ADR-0009](Product/decisions/0009-engine-as-fsm.md) D5 + this Model) grows two guards before the `ShouldExit` line:

```csharp
public override MatchState Evaluate(GameAction action, CommandResult result)
{
    foreach (var sba in StateBasedActions)
        if (sba.Check(State) is { } over) return over;

    if (State.Get<RequestsSlice>().Open.Count > 0)         return Current;   // pause on open requests
    if (!State.Get<PendingEffectsSlice>().Pending.IsEmpty) return Current;   // engine will drain pending effects

    var current = State.Get<MatchStateSlice>().Current;
    return current.ShouldExit(action, result, State)
        ? Flow.NextAfter(current)
        : current;
}
```

Pending-effect drain itself is the engine's loop responsibility, not `Evaluate`'s — `Evaluate` just keeps the FSM stationary while the engine works through pending effects.

**Games override `Evaluate` for custom drain timing.** MTG-style stack resolution requires both players to pass priority before draining; the override checks the game's `PriorityState` and only returns a transition-eligible state when priority has been passed twice. Hearthstone-style "drain to empty before any FSM transition" is the default.

### How effect-firing paths push

#### Cast handler

```csharp
public sealed class CastLightningBoltHandler
    : GameCommandHandler<CastLightningBoltCommand, CastResult>
{
    public override Task<CastResult> Handle(CastLightningBoltCommand cmd)
    {
        Store.Execute(new SpendManaPayload(cmd.Caster, 1));
        Store.Execute(new MoveCardToZonePayload(cmd.Card, /* stack zone */));
        Store.Execute(new PushPendingEffectPayload(
            new EffectInstance(
                Source:  cmd.Card.SelfRef,
                Id:      EffectIds.LightningBolt,
                Payload: new LightningBoltPayload(/* … */))));
        return Task.FromResult(new CastResult.Accepted());
    }
}
```

The handler is atomic — does state mutation only. No `await` on player decisions. The pushed `EffectInstance` is the deferred work; the engine drains it later.

#### Trigger reaction

A `Trigger<T>` (per [`triggers/01-trigger.md`](features/triggers/archive/01-trigger.md)) whose `Execute` wants to fire an effect pushes through the same channel — it does *not* call `IEffectPipeline.Run` directly:

```csharp
public sealed class DrawOnDamage : Trigger<DealDamageCommand>
{
    public override Task Execute(DealDamageCommand cmd, IEffectScope scope)
    {
        // …push a draw-card effect for the affected actor:
        scope.Store.Execute(new PushPendingEffectPayload(
            new EffectInstance(
                Source:  scope.Self,
                Id:      EffectIds.DrawCard,
                Payload: new DrawCardPayload(cmd.Target.Owner, count: 1))));
        return Task.CompletedTask;
    }
}
```

Same channel; engine drains.

#### FSM `EnterCommands`

A `MatchState` whose `EnterCommands` includes a push:

```csharp
public sealed record BeginningOfCombatState(IActor ActivePlayer) : MatchState
{
    public override IReadOnlyList<GameAction> EnterCommands => [
        new PushBeginningOfCombatEffectsCommand(ActivePlayer),   // ← handler pushes pending effects
    ];
}
```

The handler for `PushBeginningOfCombatEffectsCommand` enumerates the relevant effects and pushes each through `PushPendingEffectPayload`.

## Invariants

- **Only the engine drains `PendingEffectsSlice`.** Per [ADR-0011 lock 1](Product/decisions/0011-effect-orchestration-dispatch-requests.md). Cast handlers, triggers, and runtime code never call `IEffectPipeline.Run` — they push to the slice and the engine drains.
- **Strictly serial.** Only one effect's `Run` is in flight at a time per drain loop. While an effect awaits a `Response`, the drain loop progresses the response through the command pipeline (resolving the awaited Task), but does not pop a parallel sibling from `PendingEffectsSlice`. Order on the slice is order of execution.
- **Order is runtime-decided.** The slice carries `ImmutableList<EffectInstance>`; LIFO is the framework default (`PopPendingEffectMutator` removes from the tail). Games override the pop mutator or the drain order via runtime `Evaluate` for FIFO / priority-shaped policies.
- **Drain pauses on open requests.** When `RequestsSlice.Open` is non-empty, the engine does not drain `PendingEffectsSlice` and the FSM does not transition. Requests block forward progress until answered.
- **`EffectInstance` is wire-form.** Flat record. `Source` is a `Reference`; `Id` is opaque; `Payload` is a game-shaped record (typed in practice; `object?` at the framework type level pending `oq-effect-authoring`).
- **Cast handlers stay atomic from the player-input POV.** Per [ADR-0010](Product/decisions/0010-validator-handler-split.md) handlers do not await player decisions; they push effects and return. Player decisions ride the request system (per [`06-request-system.md`](features/effects/archive/06-request-system.md)).

## Decisions

Linked: [ADR-0011](Product/decisions/0011-effect-orchestration-dispatch-requests.md) — universal `PendingEffectsSlice` queue (D15); engine-only effect firing (D13); strict-serial drain policy (D16). Spike provenance: `Product/spikes/archive/fsm-architecture.md` D13 + D15.

Linked: [ADR-0009](Product/decisions/0009-engine-as-fsm.md) — the default `Evaluate` body composes SBAs preempt → request pause → pending-effects pause → `ShouldExit` + Flow. This Model owns the second and third guards.

## Examples

### Magic-style stack resolution

```text
1. Player A casts Lightning Bolt → CastLightningBoltCommand drains.
   Handler pushes EffectInstance(Source=boltCard, Id=LightningBolt, Payload=…)
   onto PendingEffectsSlice. Pending = [bolt].
2. Evaluate runs:
   • RequestsSlice empty → pass.
   • PendingEffectsSlice non-empty → return Current (stay in PriorityState).
   • Engine sees Pending non-empty; runtime override of Evaluate notes priority
     not yet passed → return Current. Engine does NOT drain pending yet.
3. Player A passes priority → PassPriorityCommand drains.
4. Evaluate runs:
   • Priority passed by all → permit drain.
5. Engine drains pending:
   • Pop EffectInstance(bolt) → Pending = [].
   • await effectPipeline.Run(bolt, scope).
     • LightningBolt.Run(scope):
       • await scope.Dispatch(new TargetRequest(...))  → RequestsSlice += request.
       • Drain pauses (RequestsSlice non-empty).
       • Player chooses target c2 → TargetResponse(id, c2) dispatched.
       • Response resolves the awaited Task. Effect resumes.
       • await scope.Dispatch(new DealDamageCommand(c2, 3))  → drains via command pipeline.
       • await scope.Dispatch(new CardDamagedSignal(c2, 3))  → history.
     • Run returns.
6. Engine: Pending empty, Requests empty → Evaluate transitions FSM normally.
```

### Hearthstone-style immediate-fire

```text
1. Player plays Knife Juggler (battlecry: deal 1 to random enemy)
   → CastBattlecryCommand drains.
   Handler pushes EffectInstance(Source=knifeJugglerInstance, Id=Battlecry, Payload=…)
   onto PendingEffectsSlice. Pending = [battlecry].
2. Evaluate runs:
   • RequestsSlice empty → pass.
   • PendingEffectsSlice non-empty → return Current (stay in current state).
3. Engine drains pending immediately (no priority-pass mechanism in Hearthstone):
   • Pop EffectInstance(battlecry) → Pending = [].
   • await effectPipeline.Run(battlecry, scope).
     • Battlecry effect: select random enemy, dispatch DealDamageCommand(target, 1).
   • Run returns.
4. Pending empty → Evaluate transitions FSM normally.
```

### Triggered reaction during damage resolution

```text
1. Inside another effect's Run:
   await scope.Dispatch(new DealDamageCommand(target, 3))
   • Damage handler runs.
   • OnAfter<DealDamageCommand> trigger "DrawOnDamage" fires.
   • Trigger.Execute pushes EffectInstance(draw) to PendingEffectsSlice.
   • Damage handler returns; envelope publishes.
2. Original effect's Run resumes; its remaining `await`s continue.
3. After the original effect's Run completes, engine drains Pending:
   • Pop the draw effect → run it.
```

The push channel is uniform; the engine drains in slice order.

## Open follow-ups

- `oq-drain-policy-composition` — whether to formalize the `DrainPolicy` composition (priority pass, SBA preempt, request pause, MatchState.AllowsDrain, custom Evaluate overrides) into its own Model or keep it folded across this Model + `engine/03-game-rules-runtime.md`. Pin when game-side overrides start composing in non-trivial ways.
- `oq-effect-authoring` — the recipe a `CardDefinition` holds for constructing an `IEffect` from `(Source, EffectId, Payload)` at fire time. Authoring vocabulary (graphs / code / hybrid) is open. This Model only pins the wire-form `EffectInstance` shape.
- `oq-effect-cancellation-on-match-end` — thread `CancellationToken` through `IEffectScope.Dispatch`; engine cancels on match-end and GCs in-flight pending. Implementation pass; resolution path is locked.
- `oq-effect-save-load` — async-coroutine effects cannot be serialized mid-`await`. Save/load is supported at quiescent points (no `Run` currently in flight); mid-effect save/load would force state-machine effects — separate spike.
- **`EffectId` shape.** Currently treated as opaque typed identifier. Pin during authoring design.
- **`Payload` typing.** `object?` at framework level is a deliberate placeholder; concrete games carry typed payload records. Authoring may tighten the framework-side shape.

---

*Below: provenance — Model authored 2026-05-11 as part of the ADR-0011 cascade. No options-weighing at this Model's scope; the surface is dictated by [ADR-0011](Product/decisions/0011-effect-orchestration-dispatch-requests.md).*

## Options considered

*n/a — descriptive Model. Choices made at ADR-0011 scope; see linked ADR for option-weighing.*

## Rationale

*n/a — see `## Contracts` and [ADR-0011](Product/decisions/0011-effect-orchestration-dispatch-requests.md).*

## Consequences

*n/a — see [ADR-0011](Product/decisions/0011-effect-orchestration-dispatch-requests.md) `## Consequences`.*
