---
type: model
status: settled
last-updated: 2026-04-29
builds-on: [features/engine/notes/01-evaluation-loop.md]
tags: [#triggers, #reactions, #fold]
---

# Trigger And Reaction System

This document defines the trigger and reaction model for the Card Framework. It replaces the `CommandPreHook` placeholder from docs 18 and 20 with a proper design.

Builds on:

- [18-action-flow-snippets.md](18-action-flow-snippets.md) — pipeline and envelope model
- [20-engine-evaluation-loop.md](20-engine-evaluation-loop.md) — engine loop and GameRules evaluator
- [19-handler-mutations-and-semantics.md](19-handler-mutations-and-semantics.md) — composite actions and scope

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

An **Effect** is a separate, higher-level concept that orchestrates multiple `GameAction`s in sequence. Only effects sequence commands. A `GameAction` handler applies mutations (directly or via `CompositeAction`s) but never dispatches other `GameAction`s itself. See [22-async-decisions-and-suspension.md](22-async-decisions-and-suspension.md) for the full layer hierarchy.

---

## 2. The Trigger Fold

Every `GameCommand` that enters the pipeline passes through two trigger points:

```
OnBefore(c)        ← trigger fold; GameAction payload can be modified or cancelled
execute(c)         ← handler (GameAction only; CompositeAction inline; GameSignal skipped)
OnAfter(c, result) ← trigger fold; reactions dispatched
```

### Signatures

```csharp
void OnBefore<T>(
    Func<T, Context, bool> condition,
    Func<T, Context, T>    react)
    where T : GameCommand;

void OnAfter<T>(
    Func<T, Context, bool> condition,
    Func<T, Context, T>    react)
    where T : GameCommand;
```

The `react` function receives the current command and returns the (potentially modified) command. For `CompositeAction` and `GameSignal` the return value is ignored — they are not modifiable.

### The fold mechanic

All triggers registered for type `T` run in sequence. Each sees the output of the previous:

```
c0 = original command
c1 = trigger1.react(c0, ctx)   // only if trigger1.condition(c0) == true
c2 = trigger2.react(c1, ctx)   // only if trigger2.condition(c1) == true
...
cN = command passed to handler
```

**Cancel or replace (`GameAction` only):** A trigger can cancel the command or return a different `GameAction` type. Either stops the fold. The original command does not execute. A replacement is dispatched as a fresh command through the channel — it goes through its own full trigger fold from scratch.

---

## 3. Context

`Context` is passed to both the condition and react functions. It provides a read view of game state at the moment the trigger fires, plus a reference to the subscribing instance.

```csharp
public sealed class Context
{
    // Shape TBD — tied to the GameState contract (see open questions).
    // At minimum: read-only GameState view and the identity of the subscribing instance.
    public InstanceId Self { get; }
    public GameState State { get; }
    public void Dispatch(GameCommand command);
}
```

Calling `context.Dispatch` from inside a react function sends a reaction into the dispatch channel immediately. The engine decides what to do with it via `GameRules.OnIncomingCommand`.

---

## 4. Reactions And The Dispatch Channel

Reaction commands dispatched from inside a trigger go immediately into the same channel as everything else. There is no separate reaction queue.

```csharp
// Inside an OnAfter react function — "whenever another card is healed, draw a card":
triggers.OnAfter<HealActorAction>(
    condition: (cmd, ctx) => cmd.Target != ctx.Self,
    react: (cmd, ctx) =>
    {
        ctx.Dispatch(new DrawCardAction { Actor = ctx.GetOwner(ctx.Self) });
        return cmd;
    });
```

The channel receives the dispatched command and calls `GameRules.OnIncomingCommand` before scheduling it:

```csharp
public interface GameRules
{
    SchedulingDecision OnIncomingCommand(GameCommand command, SchedulingContext context);
    IReadOnlyList<GameAction> Evaluate(GameAction justExecuted, CommandResult result, GameState state);
    void OnQueueDrained(GameEngine engine, GameState state);
}

public enum SchedulingDecision
{
    RunNext,  // interrupt — prepend to queue, runs before current queue resumes
    Enqueue,  // append to end of current queue
    Stash,    // hold for later; game releases manually
    Discard   // silently drop
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
    public abstract void Register(ITriggerSystem triggers, InstanceId self, Context ctx);
    public abstract void Unregister(ITriggerSystem triggers, InstanceId self);
}

// Example ability: "whenever another card is healed, draw a card"
public sealed class WhenOtherHealedDrawAbility : AbilityDefinition
{
    public override void Register(ITriggerSystem triggers, InstanceId self, Context ctx)
    {
        triggers.OnAfter<HealActorAction>(
            condition: (cmd, ctx) => cmd.Target != self,
            react: (cmd, ctx) =>
            {
                ctx.Dispatch(new DrawCardAction { Actor = ctx.GetOwner(self) });
                return cmd;
            },
            owner: self);
    }

    public override void Unregister(ITriggerSystem triggers, InstanceId self)
    {
        triggers.RemoveAll(self);
    }
}
```

The lifecycle of when `Register`/`Unregister` is called — and what qualifies as "trigger-active" — is a deferred design topic tied to the zone model and GameState contract.

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

`RunPreHooks` from doc 18 is replaced by `triggerSystem.RunBefore`. The pipeline now:

```csharp
public CommandResult Execute(GameAction action)
{
    using var envelope = history.OpenEnvelope(BuildCause(action));

    var before = triggerSystem.RunBefore(action, context);
    if (before.IsCancelled)
        return history.CloseAndPublish(envelope, CommandResult.Cancelled(before.Reason));

    if (before.IsReplaced)
    {
        history.CloseAndPublish(envelope, CommandResult.Replaced);
        engine.Dispatch(before.Replacement);
        return CommandResult.Replaced;
    }

    var result = Handle(before.Command, envelope);

    triggerSystem.RunAfter(before.Command, result, context);

    return history.CloseAndPublish(envelope, result);
}
```

`CompositeAction` and `GameSignal` use a lighter path — trigger folds only, no handler, no envelope of their own; their entries fold into the parent command's envelope.

---

## 8. Open Questions

- **Context shape** — what exactly `Context` exposes to conditions and reactions. Directly tied to the `GameState` contract (checkpoint task 2).
- **Subscription lifecycle** — when `Register`/`Unregister` is called; relationship to zone transitions and instance state.
- **Trigger ordering** — what determines sequence within the fold when multiple triggers match the same type. FIFO by registration? Priority weight? Game-configurable?
- **Async suspension** — triggers that require player input (targeting, choices) still unresolved. Unblocks the broader suspension design.
- **Stash release mechanics** — MTG-style stash needs a `Release(key)` call and ordering rules. Not designed here.

---

## 9. Related

- [18-action-flow-snippets.md](18-action-flow-snippets.md) — pipeline, envelope, handler flow
- [20-engine-evaluation-loop.md](20-engine-evaluation-loop.md) — engine loop, GameRules, OnQueueDrained
- [19-handler-mutations-and-semantics.md](19-handler-mutations-and-semantics.md) — CompositeAction utilities and scope
- [06-open-questions.md](06-open-questions.md) — async suspension and other open items
