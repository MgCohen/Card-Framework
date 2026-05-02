---
type: model
status: settled
references: [features/effects/models/01-async-dispatch.md]
tags: [#effects, #replacement, #prevention]
---

# Replacement And Prevention Effects

This document defines the rules-level semantics for replacement and prevention effects — how the before-fold is interpreted, how competing replacements are handled, and where game-specific ordering policy lives.

Builds on:

- [features/triggers/models/01-trigger-fold.md](features/triggers/models/01-trigger-fold.md) — trigger fold, OnBefore/OnAfter, cancel/replace mechanic
- [features/engine/models/01-evaluation-loop.md](features/engine/models/01-evaluation-loop.md) — GameRules interface, scheduling decisions
- [features/effects/models/01-async-dispatch.md](features/effects/models/01-async-dispatch.md) — layer hierarchy, Effect/GameAction/Decision

---

## 1. Three Before-Fold Outcomes

Every `OnBefore<T>` trigger produces one of three outcomes:

| Outcome | What happens | Example |
|---|---|---|
| **Modification** | Same type, different property values | Reduce `DamageAction.Amount` from 5 to 3 |
| **Cancellation** | Event is stopped; handler never runs | Shield absorbs hit; action cancelled |
| **Substitution** | Different `GameAction` type dispatched in place | `DamageAction` → `HealAction` |

Modification and cancellation were implicit in the fold design (doc 21). Substitution is the replacement case.

All three go through the same `OnBefore<T>` surface — no separate API for each. Triggers communicate their outcome through `IEffectScope` rather than a return value:

```csharp
// Subscription — takes a Trigger<T>
// ADR-0005: `owner` is a stable subscription key — InstanceId survives CardRef rotations.
// Trigger subscriptions outlive individual object references; this use is boundary-correct per ADR-0005 §Stable subscription keys.
void OnBefore<T>(Trigger<T> trigger, InstanceId owner) where T : GameAction;

// Inside Trigger<T>.Execute:
scope.Cancel("shield absorbed");           // cancellation
scope.Replace(new ShieldAbsorbAction(…));  // substitution
// (returning without a scope call) = modification or no-op
```

`FoldResult` (`Keep`/`Cancel`/`Replace`) is a pipeline-internal discriminator — it is not part of the game-author API. The pipeline reads scope state after `Execute` returns to determine which outcome occurred.

---

## 2. Replacement-Of-Replacements Is Handled By The Fold

No special mechanism is needed. The fold evaluates each trigger's condition against the *current command* — the output of all prior triggers in the fold sequence.

```
c0 = DamageAction { Amount: 5 }

trigger B condition: (c) => c is DamageAction    → true on c0
trigger B react:     DamageAction → HealAction   → c1 = HealAction { Amount: 5 }

trigger C condition: (c) => c is DamageAction    → FALSE on c1 — does not fire
trigger D condition: (c) => c is HealAction      → true on c1  — fires normally
```

If B substitutes `DamageAction` with `HealAction`, any trigger watching for `DamageAction` no longer applies. Any trigger watching for `HealAction` applies as if it was always the event. This is correct by construction — no loop detection or special casing required.

---

## 3. ITriggerPipeline — Owns The Mechanical Process

The trigger execution mechanics are extracted behind `ITriggerPipeline`. This interface owns:

- Collecting registered subscriptions that match a command type
- Ordering them (FIFO, priority, choice-dispatch)
- Running the fold

```csharp
public interface ITriggerPipeline
{
    Task<TriggerFoldResult> RunBefore(
        GameCommand command,
        IReadOnlyList<TriggerMatch> available,
        IEffectScope scope);

    Task RunAfter(
        GameCommand command,
        CommandResult result,
        IReadOnlyList<TriggerMatch> available,
        IEffectScope scope);
}

public sealed record TriggerFoldResult(
    GameCommand    FinalCommand,
    bool           IsCancelled,
    string?        CancelReason,
    bool           IsReplaced,
    GameAction?    Replacement);

// Each registration wraps a Trigger<T> as non-generic delegates for pipeline dispatch.
public sealed record TriggerMatch(
    InstanceId                             Owner,      // ADR-0005: stable subscription key — boundary-correct; survives CardRef rotations
    int                                    Priority,    // lower runs first; default 0
    Func<GameCommand, IEffectScope, bool>  Condition,
    Func<GameCommand, IEffectScope, Task>  Execute);
```

The `available` list is passed in — the pipeline does not query the trigger registry directly. The registry is queried upstream and filtered by `GameRules` before the pipeline receives it (see §4).

Both methods are `async Task`. The default implementation completes synchronously via `Task.FromResult` — no allocation overhead for the common case. The async signature exists so that custom pipelines can genuinely await a `Decision` without special casing.

### Priority convention

Priority is an `int` on each `OnBefore`/`OnAfter` registration call, defaulting to `0`. Lower value runs first in the fold. The default FIFO pipeline sorts by priority ascending before running (stable sort preserves registration order among ties). A game that wants to change the convention can override sorting inside its custom `ITriggerPipeline` — the value is just data.

```csharp
// Normal ability — default priority
triggers.OnBefore<DamageAction>(condition, react, owner: self);

// Self-preservation ability — runs before other replacements
triggers.OnBefore<DamageAction>(condition, react, owner: self, priority: -10);
```

### Default implementation

The framework ships a `FifoPipeline` that stable-sorts by `Priority` ascending, then runs the fold, and returns completed tasks. Games that need MTG-style controller-choice ordering swap in a custom implementation that:

1. Detects multiple applicable triggers
2. Dispatches a `ChooseReplacementOrderAction` (a normal `Decision`, same pattern as `RequestTargetAction`)
3. Awaits the result
4. Runs the fold in the chosen order

---

## 4. GameRules.FilterTriggers — Availability Policy

`GameRules` decides which registered triggers are eligible to fire for a given command in the current game state. It does not collect, order, or execute them.

```csharp
public interface GameRules
{
    // Returns the subset of candidates that are eligible right now.
    // Default: return candidates unchanged.
    IReadOnlyList<TriggerMatch> FilterTriggers(
        IReadOnlyList<TriggerMatch> candidates,
        GameCommand command,
        TriggerPhase phase);

    // ... settled 4-method shape (see engine/02-gamerules-contract.md)
    IntakeResult OnIncomingCommand(GameCommand command, DispatchOrigin origin);
    IReadOnlyList<GameAction> Evaluate(GameAction justExecuted, CommandResult result);
    DrainResult OnQueueDrained();
}
```

This separates two distinct concerns:

| Concern | Owner |
|---|---|
| "Which triggers are registered for this type?" | Trigger registry (internal to pipeline) |
| "Which of those are eligible right now?" | `GameRules.FilterTriggers` |
| "In what order do they run?" | `ITriggerPipeline` |
| "What does each trigger do?" | `AbilityDefinition` |

### Example filter uses

- **Phase gating** — no triggers fire during setup
- **Mana abilities** — skip the trigger pipeline entirely for speed-mana costs
- **Stack restriction** — triggers suppressed while a specific resolution lock is active
- **Target validation** — a trigger requires a legal target to exist before it is considered applicable

`GameRules` never holds a reference to `ITriggerPipeline` or the trigger registry. It only receives a list of `TriggerMatch` candidates and returns a filtered subset.

---

## 5. Updated Pipeline Shape

The command pipeline (doc 21 §7) now calls `GameRules.FilterTriggers` before handing candidates to `ITriggerPipeline`:

```csharp
public async Task<CommandResult> Execute(GameAction action)
{
    using var envelope = history.OpenEnvelope(BuildCause(action));

    // 1. Collect all registered matches for this type
    var candidates = triggerRegistry.GetMatches(action);

    // 2. GameRules filters for availability
    var available = rules.FilterTriggers(candidates, action, state);

    // 3. ITriggerPipeline orders and runs the before-fold
    var scope  = scopeFactory.CreateForTrigger();
    var before = await triggerPipeline.RunBefore(action, available, scope);

    if (before.IsCancelled)
        return history.CloseAndPublish(envelope, CommandResult.Cancelled(before.CancelReason));

    if (before.IsReplaced)
    {
        history.CloseAndPublish(envelope, CommandResult.Replaced);
        engine.Dispatch(before.Replacement);   // fresh command, own fold from scratch
        return CommandResult.Replaced;
    }

    var result = Handle(before.FinalCommand, envelope);

    // 4. ITriggerPipeline runs the after-fold
    await triggerPipeline.RunAfter(before.FinalCommand, result, available, scope);

    return history.CloseAndPublish(envelope, result);
}
```

---

## 6. Prevention As Modification

Simple prevention is modification with a zero floor:

```csharp
triggers.OnBefore<DamageAction>(
    new Trigger<DamageAction>(
        validate: (cmd, scope) => cmd.Target == self,
        execute: (cmd, scope) =>
        {
            // Modification: return without calling Cancel/Replace — fold continues with mutated cmd.
            // (Mutating cmd in place isn't possible on a record; use scope.Replace with modified copy.)
            var reduced = cmd with { Amount = Math.Max(0, cmd.Amount - shieldValue) };
            scope.Replace(reduced);
            return Task.CompletedTask;
        }),
    owner: self);
```

**Stateful prevention** ("prevent the next N damage") is an ongoing effect that manages its own state — it reduces the payload and decrements an internal counter, deleting itself when exhausted. Ongoing effects are a deferred design topic; stateful prevention is one instance of them.

---

## 7. History When An Action Is Replaced

When A is replaced by B:

- A's envelope is closed and published with `CommandResult.Replaced` — it appears in history as a command that was dispatched but did not execute.
- B is dispatched fresh via `engine.Dispatch`. It goes through `GameRules.OnIncomingCommand` and gets its own full fold from scratch. B's envelope is a sibling, not a child, of A's.

This means the history shows both: "A was replaced" and "B executed." The causal link between them is preserved via `OriginCommandId` threading (doc 20 §3) if A and B share the same originating intent.

---

## 8. Extension Points Summary

| What you want to customize | Where |
|---|---|
| FIFO → priority → controller-choice ordering | Swap `ITriggerPipeline` implementation |
| Phase gating, mana-ability skipping, target validation | Override `GameRules.FilterTriggers` |
| MTG controller-choice for competing replacements | Custom `ITriggerPipeline` that dispatches a `Decision` |
| Self-replacement priority | Custom `ITriggerPipeline` sorting by `owner == affectedEntity` |

---

## 9. Open Questions

- **Trigger ordering within the fold** — `ITriggerPipeline` owns ordering, but the default (FIFO by registration time) may not match all games. A `priority` weight on registration is a likely extension to the subscription API.

---

## 10. Related

- [features/triggers/models/01-trigger-fold.md](features/triggers/models/01-trigger-fold.md) — trigger fold, subscription, OnBefore/OnAfter
- [features/engine/models/01-evaluation-loop.md](features/engine/models/01-evaluation-loop.md) — GameRules, scheduling, engine loop
- [features/effects/models/01-async-dispatch.md](features/effects/models/01-async-dispatch.md) — async layer, Decisions
- [ledgers/OPEN-QUESTIONS.md](ledgers/OPEN-QUESTIONS.md) — open items
