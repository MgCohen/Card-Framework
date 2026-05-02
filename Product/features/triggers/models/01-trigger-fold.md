---
type: model
status: settled
references: [features/engine/models/01-evaluation-loop.md]
tags: [#triggers, #reactions, #fold]
---

# Trigger And Reaction System

This document defines the trigger and reaction model for the Card Framework. It replaces the `CommandPreHook` placeholder from docs 18 and 20 with a proper design.

Builds on:

- [features/actions/models/01-command-pipeline.md](features/actions/models/01-command-pipeline.md) — pipeline and envelope model
- [features/engine/models/01-evaluation-loop.md](features/engine/models/01-evaluation-loop.md) — engine loop and GameRules evaluator
- [features/actions/models/01-command-pipeline.md](features/actions/models/01-command-pipeline.md) — composite actions and scope

---

## 1. The Type Hierarchy

Everything that flows through the pipeline shares a base type: `GameCommand`. This is the common surface the trigger system subscribes to.

```csharp
abstract record GameCommand;                     // base — all pipeline-dispatchable types

abstract record GameAction    : GameCommand;     // executable, modifiable, queued through engine
abstract record CompositeAction : GameCommand;   // reusable StateMutation group, runs inline within a handler
abstract record GameSignal    : GameCommand;     // not executable — trigger surface only
```

The previous `GameCommand` (the executable action type from docs 18–20) is now `GameAction`. `SemanticEvent` is now `GameSignal`.

### What each type is

| Type | Executed by | Modifiable in before fold | Goes through engine queue |
|---|---|---|---|
| `GameAction` | Handler, via engine queue | Yes | Yes |
| `CompositeAction` | Inline, within parent handler | No | No |
| `GameSignal` | Nothing — no execution step | No | No |

### CompositeAction vs Effect

`CompositeAction` is a reusable group of `StateMutation`s used *inside a single handler*. It does not orchestrate other `GameAction`s — it only groups state changes for reuse and trigger-fold participation. Example: `MoveCardComposite` encapsulates a remove-then-insert as one logical state-change unit.

An **Effect** is a separate, higher-level concept that orchestrates multiple `GameAction`s in sequence. Only effects sequence commands. A `GameAction` handler applies mutations (directly or via `CompositeAction`s) but never dispatches other `GameAction`s itself. See [features/effects/models/01-async-dispatch.md](features/effects/models/01-async-dispatch.md) for the full layer hierarchy.

---

## 2. The Trigger Fold

Every `GameCommand` that enters the pipeline passes through two trigger points:

```
OnBefore(c)        ← trigger fold; GameAction payload can be modified or cancelled
execute(c)         ← handler (GameAction only; CompositeAction inline; GameSignal skipped)
OnAfter(c, result) ← trigger fold; reactions dispatched
```

### `Trigger<T>` — the base type

A trigger is an effect that reacts to a specific command type. It shares the same execution surface as `Effect` (`IEffectScope`), typed to the command it intercepts.

```csharp
public abstract class Trigger<T> where T : GameCommand
{
    public virtual bool Validate(T command, IEffectScope scope) => true;
    public abstract Task Execute(T command, IEffectScope scope);
}
```

`Validate` is the condition check — return `false` to skip this trigger for the current command. `Execute` is the reaction body. Both receive the intercepted command and an `IEffectScope`; `Self` and any other per-instance data are closure-captured at registration.

Cancel and replace are communicated through the scope — no return value needed:

```csharp
// Before-fold: cancel the intercepted command
scope.Cancel("shield absorbed");

// Before-fold: swap in a different command
scope.Replace(new ShieldAbsorbAction(target));
```

Calling `scope.Cancel()` or `scope.Replace()` in an **after-fold** trigger is a no-op (the command already executed). The default (no call) is pass-through — the command continues unchanged.

**Inline construction:**
```csharp
new Trigger<HealActorAction>(
    validate: (cmd, scope) => cmd.Target != self,
    execute: async (cmd, scope) =>
    {
        await scope.Dispatch(new DrawCardAction { Actor = myActorId });
    });
```

**Subclassed:**
```csharp
public sealed class DrawOnHeal : Trigger<HealActorAction>
{
    private readonly InstanceId _self;
    private readonly ActorId    _actor;

    public DrawOnHeal(InstanceId self, ActorId actor) { _self = self; _actor = actor; }

    public override bool Validate(HealActorAction cmd, IEffectScope scope)
        => cmd.Target != _self;

    public override async Task Execute(HealActorAction cmd, IEffectScope scope)
        => await scope.Dispatch(new DrawCardAction { Actor = _actor });
}
```

### Subscription — `ITriggerSystem`

Card abilities register and unregister through `ITriggerSystem`. This is the surface `AbilityDefinition.Register` calls.

```csharp
public interface ITriggerSystem
{
    void OnBefore<T>(Trigger<T> trigger, InstanceId owner) where T : GameCommand;
    void OnAfter<T>(Trigger<T> trigger, InstanceId owner)  where T : GameCommand;
    void RemoveAll(InstanceId owner);
}
```

`owner` is the entity that registered the trigger — used by `RemoveAll` to unsubscribe all triggers belonging to that instance at once.

### Execution — `ITriggerPipeline`

The pipeline runs the trigger fold through `ITriggerPipeline`. This is a pipeline-internal surface; game authors never call it directly. One `TriggerSystem` class implements both interfaces. The authoritative shape is defined in [effects/02 §3](features/effects/models/02-replacement-prevention.md):

```csharp
public interface ITriggerPipeline
{
    Task<TriggerFoldResult> RunBefore(
        GameCommand                    command,
        IReadOnlyList<TriggerMatch>    available,
        IEffectScope                   scope);

    Task RunAfter(
        GameCommand                    command,
        CommandResult                  result,
        IReadOnlyList<TriggerMatch>    available,
        IEffectScope                   scope);
}

public sealed record TriggerFoldResult(
    GameCommand  FinalCommand,
    bool         IsCancelled,
    string?      CancelReason,
    bool         IsReplaced,
    GameAction?  Replacement);
```

The `available` list is passed in — the pipeline does not query the trigger registry directly. The registry is queried upstream and filtered by `GameRules.FilterTriggers` before the pipeline receives it. Both methods are `async Task`; the default `FifoPipeline` completes synchronously via `Task.FromResult`.

### The fold mechanic

All triggers registered for type `T` run in sequence. Each sees the output of the previous (or the scope state left by the previous):

```
c0 = original command
trigger1.Validate(c0, scope) → true → trigger1.Execute(c0, scope)  // may call scope.Cancel/Replace
trigger2.Validate(c1, scope) → true → trigger2.Execute(c1, scope)
...
cN = command passed to handler (unless scope.Cancel was called)
```

**Cancel or replace (`GameAction` only):** A trigger calls `scope.Cancel()` or `scope.Replace(next)` during `Execute`. Either stops the fold. The original command does not execute. A replacement dispatches as a fresh command through the channel — it goes through its own full trigger fold from scratch.

---

## 3. Context — Deleted

`Context` is deleted (S19). Triggers receive `IEffectScope` directly — the same surface effects use. The subscribing entity's identity (`Self`) and any per-instance data are closure-captured at registration, not carried on a context type. State reads go through `scope.State.Get<TState>()`. Reactions dispatch through `scope.Dispatch(...)`.

See [features/effects/models/04-effect-flow.md §4.3](features/effects/models/04-effect-flow.md) for the full `IEffectScope` definition.

---

## 4. Reactions And The Dispatch Channel

Reaction commands dispatched from inside a trigger go immediately into the same channel as everything else. There is no separate reaction queue.

```csharp
// "Whenever another card is healed, draw a card"
var self   = myInstanceId;
var myActor = myActorId;

triggers.OnAfter<HealActorAction>(
    new Trigger<HealActorAction>(
        validate: (cmd, scope) => cmd.Target != self,
        execute: async (cmd, scope) =>
        {
            await scope.Dispatch(new DrawCardAction { Actor = myActor });
        }),
    owner: self);
```

The channel receives the dispatched command and calls `GameRules.OnIncomingCommand` before scheduling it:

```csharp
public interface GameRules
{
    IntakeResult OnIncomingCommand(GameCommand command, DispatchOrigin origin);
    IReadOnlyList<GameAction> Evaluate(GameAction justExecuted, CommandResult result);
    DrainResult OnQueueDrained();
    IReadOnlyList<TriggerMatch> FilterTriggers(IReadOnlyList<TriggerMatch> candidates, GameCommand command, TriggerPhase phase);
}

public enum DispatchOrigin
{
    PlayerIntent,      // from player input
    TriggerReaction,   // from a trigger's react function
    Timer,            // from a timer service
    Internal          // from game internals
}
```

### Game-style scheduling examples

| Style | `OnIncomingCommand` behaviour |
|---|---|
| Slay the Spire | `RunNext` — triggered effects interrupt and resolve immediately |
| Hearthstone | `Enqueue` — triggered effects resolve after the current command finishes |
| MTG | `Stash` until priority resolves, then release onto stack in LIFO order |

The pipeline is the same in all three cases. Only `GameRules` changes.

---

## 5. Per-Instance Registration

Trigger subscriptions are registered per instance at runtime. The definition carries the subscription logic; the instance calls `Register` when it enters a trigger-active state and `Unregister` when it leaves.

```csharp
public abstract class AbilityDefinition
{
    public abstract void Register(ITriggerSystem triggers, InstanceId self);
    public abstract void Unregister(ITriggerSystem triggers, InstanceId self);
}

// Example ability: "whenever another card is healed, draw a card"
public sealed class WhenOtherHealedDrawAbility : AbilityDefinition
{
    public override void Register(ITriggerSystem triggers, InstanceId self)
    {
        triggers.OnAfter<HealActorAction>(
            new Trigger<HealActorAction>(
                validate: (cmd, scope) => cmd.Target != self,
                execute: async (cmd, scope) =>
                {
                    await scope.Dispatch(new DrawCardAction { Actor = self });
                }),
            owner: self);
    }

    public override void Unregister(ITriggerSystem triggers, InstanceId self)
    {
        triggers.RemoveAll(self);
    }
}
```

`Self` and any other per-instance data are closure-captured. `Context` is not passed to `Register` — it was deleted in S19; see §3.

The lifecycle of when `Register`/`Unregister` is called — and what qualifies as "trigger-active" — is a deferred design topic tied to the zone model and game lifecycle.

---

## 6. Emitting GameSignals And CompositeActions From A Handler

Handlers emit `GameSignal`s and execute `CompositeAction`s through the scope. Both pass through their own trigger folds before/after:

```csharp
// Inside a handler:
scope.Emit(new ArmorBrokenSignal(target));          // GameSignal — before/after triggers fire; no handler
scope.Execute(new MoveCardComposite(source, zone)); // CompositeAction — before/after triggers fire; executes inline
```

`scope.Emit` and `scope.Execute` are the scope-level equivalents of `engine.Dispatch` for non-queued types.

A handler should never dispatch another `GameAction` directly. If sequencing multiple `GameAction`s is needed, that is an **Effect**'s responsibility — not a handler's. Handlers apply state changes (via mutations and `CompositeAction`s) and return a `CommandResult`. Effects orchestrate the sequence of commands around and between handlers.

---

## 7. Updated Pipeline Shape

`RunPreHooks` from doc 18 is replaced by `ITriggerPipeline.RunBefore`. The pipeline holds `ITriggerPipeline` (the execution surface); card abilities hold `ITriggerSystem` (the subscription surface). One `TriggerSystem` class implements both.

```csharp
public async Task<CommandResult> Execute(GameAction action)
{
    using var envelope = history.OpenEnvelope(BuildCause(action));

    var candidates = triggerRegistry.GetMatches(action);
    var available  = rules.FilterTriggers(candidates, action, TriggerPhase.Before);
    var scope      = scopeFactory.CreateForTrigger();
    var before     = await triggerPipeline.RunBefore(action, available, scope);

    if (before.IsCancelled)
        return history.CloseAndPublish(envelope, CommandResult.Cancelled(before.CancelReason));

    if (before.IsReplaced)
    {
        history.CloseAndPublish(envelope, CommandResult.Replaced);
        engine.Dispatch(before.Replacement);
        return CommandResult.Replaced;
    }

    var result = Handle(before.FinalCommand, envelope);

    await triggerPipeline.RunAfter(before.FinalCommand, result, available, scope);

    return history.CloseAndPublish(envelope, result);
}
```

`CompositeAction` and `GameSignal` use a lighter path — trigger folds only, no handler, no envelope of their own; their entries fold into the parent command's envelope.

---

## 8. Open Questions

- **Subscription lifecycle** — when `Register`/`Unregister` is called; relationship to zone transitions and game lifecycle.
- **Trigger ordering** — what determines sequence within the fold when multiple triggers match the same type. FIFO by registration? Priority weight? Game-configurable?
- **Async suspension** — triggers that require player input (targeting, choices) still unresolved. Unblocks the broader suspension design.
- **Stash release mechanics** — MTG-style stash needs a `Release(key)` call and ordering rules. Not designed here.
- ~~**`ITriggerSystem` vs `ITriggerPipeline` unification** — two competing shapes remain in play; see `OPEN-ITEMS.md`.~~ → resolved session-20260501-1132: split into `ITriggerSystem` (subscription) and `ITriggerPipeline` (execution); one `TriggerSystem` class implements both. Shape reconciled with effects/02 §3 in session-20260501-1545 (ad-20260501-025 discharged).

---

## 9. Related

- [features/actions/models/01-command-pipeline.md](features/actions/models/01-command-pipeline.md) — pipeline, envelope, handler flow
- [features/engine/models/01-evaluation-loop.md](features/engine/models/01-evaluation-loop.md) — engine loop, GameRules, OnQueueDrained
- [features/actions/models/01-command-pipeline.md](features/actions/models/01-command-pipeline.md) — CompositeAction utilities and scope
- [ledgers/OPEN-ITEMS.md](ledgers/OPEN-ITEMS.md) — async suspension and other open items
