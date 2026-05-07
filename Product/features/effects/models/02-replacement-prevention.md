---
type: model
status: settled
references: [features/effects/models/01-async-dispatch.md, features/triggers/models/01-trigger-fold.md, features/engine/models/01-evaluation-loop.md]
tags: [#effects, #replacement, #prevention]
---

# Replacement And Prevention Effects — model

## What it is

Rules-level semantics for replacement and prevention effects — how the before-fold is interpreted, how competing replacements are handled, where game-specific ordering policy lives. The before-fold has three outcomes: **modification** (same type, different property values), **cancellation** (event stopped, handler never runs), **substitution** (different `GameAction` dispatched in place). Replacement-of-replacements is handled by the fold itself — no special mechanism. Ordering is settled deterministically via fold-ordered priority (`int` weight on each registration; lower runs first; ties break by stable rule).

## Contracts

### Three before-fold outcomes

Every `OnBefore<T>` trigger produces one of three outcomes:

| Outcome | What happens | Example |
|---|---|---|
| **Modification** | Same type, different property values | Reduce `DamageAction.Amount` from 5 to 3 |
| **Cancellation** | Event is stopped; handler never runs | Shield absorbs hit; action cancelled |
| **Substitution** | Different `GameAction` type dispatched in place | `DamageAction` → `HealAction` |

All three go through the same `OnBefore<T>` surface — no separate API for each. Triggers communicate their outcome through `IEffectScope` rather than a return value:

```csharp
// Subscription — takes a Trigger<T>
// `owner` is a stable subscription key — InstanceId survives CardRef rotations.
// Trigger subscriptions outlive individual object references; this use is boundary-correct
// per Product/decisions/0005-objects-over-ids.md §Stable subscription keys.
void OnBefore<T>(Trigger<T> trigger, InstanceId owner) where T : GameAction;

// Inside Trigger<T>.Execute:
scope.Cancel("shield absorbed");           // cancellation
scope.Replace(new ShieldAbsorbAction(…));  // substitution
// (returning without a scope call) = modification or no-op
```

`FoldResult` (`Keep`/`Cancel`/`Replace`) is a pipeline-internal discriminator — it is not part of the game-author API. The pipeline reads scope state after `Execute` returns to determine which outcome occurred.

### Replacement-of-replacements is handled by the fold

No special mechanism is needed. The fold evaluates each trigger's condition against the *current command* — the output of all prior triggers in the fold sequence.

```
c0 = DamageAction { Amount: 5 }

trigger B condition: (c) => c is DamageAction    → true on c0
trigger B react:     DamageAction → HealAction   → c1 = HealAction { Amount: 5 }

trigger C condition: (c) => c is DamageAction    → FALSE on c1 — does not fire
trigger D condition: (c) => c is HealAction      → true on c1  — fires normally
```

If B substitutes `DamageAction` with `HealAction`, any trigger watching for `DamageAction` no longer applies. Any trigger watching for `HealAction` applies as if it was always the event. This is correct by construction — no loop detection or special casing required.

### `ITriggerPipeline` — owns the mechanical process

The trigger execution mechanics are extracted behind `ITriggerPipeline`. This interface owns: collecting registered subscriptions that match a command type, ordering them (FIFO, priority, choice-dispatch), running the fold.

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
    InstanceId                             Owner,      // stable subscription key — boundary-correct per ADR-0005
    int                                    Priority,    // lower runs first; default 0
    Func<GameCommand, IEffectScope, bool>  Condition,
    Func<GameCommand, IEffectScope, Task>  Execute);
```

The `available` list is passed in — the pipeline does not query the trigger registry directly. The registry is queried upstream and filtered by `GameRules` before the pipeline receives it (see "GameRules.FilterTriggers" below).

Both methods are `async Task`. The default implementation completes synchronously via `Task.FromResult` — no allocation overhead for the common case. The async signature exists so that custom pipelines can genuinely await a `Decision` without special casing.

#### Priority convention

Priority is an `int` on each `OnBefore`/`OnAfter` registration call, defaulting to `0`. Lower value runs first in the fold. The default FIFO pipeline sorts by priority ascending before running (stable sort preserves registration order among ties). A game that wants to change the convention can override sorting inside its custom `ITriggerPipeline` — the value is just data.

```csharp
// Normal ability — default priority
triggers.OnBefore<DamageAction>(condition, react, owner: self);

// Self-preservation ability — runs before other replacements
triggers.OnBefore<DamageAction>(condition, react, owner: self, priority: -10);
```

#### Default implementation

The framework ships a `FifoPipeline` that stable-sorts by `Priority` ascending, then runs the fold, and returns completed tasks. Games that need MTG-style controller-choice ordering swap in a custom implementation that:

1. Detects multiple applicable triggers
2. Dispatches a `ChooseReplacementOrderAction` (a normal `Decision`, same pattern as `RequestTargetAction`)
3. Awaits the result
4. Runs the fold in the chosen order

### `GameRules.FilterTriggers` — availability policy

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

Example filter uses: phase gating (no triggers fire during setup), mana abilities (skip trigger pipeline entirely for speed-mana costs), stack restriction (triggers suppressed while resolution lock active), target validation (a trigger requires a legal target before being considered applicable).

`GameRules` never holds a reference to `ITriggerPipeline` or the trigger registry. It only receives a list of `TriggerMatch` candidates and returns a filtered subset.

### Updated pipeline shape

The command pipeline now calls `GameRules.FilterTriggers` before handing candidates to `ITriggerPipeline`:

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

### Prevention as modification

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

**Stateful prevention** ("prevent the next N damage") is an ongoing effect that manages its own state — it reduces the payload and decrements an internal counter, deleting itself when exhausted. See [`03-ongoing-effects.md`](features/effects/models/03-ongoing-effects.md).

### History when an action is replaced

When A is replaced by B:

- A's envelope is closed and published with `CommandResult.Replaced` — it appears in history as a command that was dispatched but did not execute.
- B is dispatched fresh via `engine.Dispatch`. It goes through `GameRules.OnIncomingCommand` and gets its own full fold from scratch. B's envelope is a sibling, not a child, of A's.

This means the history shows both: "A was replaced" and "B executed." The causal link between them is preserved via `OriginCommandId` threading if A and B share the same originating intent.

### Extension points summary

| What you want to customize | Where |
|---|---|
| FIFO → priority → controller-choice ordering | Swap `ITriggerPipeline` implementation |
| Phase gating, mana-ability skipping, target validation | Override `GameRules.FilterTriggers` |
| MTG controller-choice for competing replacements | Custom `ITriggerPipeline` that dispatches a `Decision` |
| Self-replacement priority | Custom `ITriggerPipeline` sorting by `owner == affectedEntity` |

## Invariants

Model-local invariants. (See `effects.md ## Invariants` for feature-level rules.)

- **`FoldResult` is pipeline-internal.** Game authors don't return it; they call `scope.Cancel` / `scope.Replace`.
- **Replacement-of-replacements is automatic.** No loop detection; the fold re-evaluates conditions against the current command after each trigger.
- **`available` is passed in to the pipeline.** The pipeline never queries the trigger registry directly. Registry ↔ availability ↔ ordering are three separate concerns.
- **Replacement creates a fresh fold.** `engine.Dispatch(replacement)` starts the new command's full fold from scratch — including its own `OnIncomingCommand` routing.

## Decisions

*n/a — no elevated ADR.*

## Examples

```csharp
// Self-preservation: shield ability prevents damage to self
triggers.OnBefore<DamageAction>(
    new Trigger<DamageAction>(
        validate: (cmd, scope) => cmd.Target == self,
        execute: (cmd, scope) =>
        {
            scope.Cancel("shield absorbed");
            return Task.CompletedTask;
        }),
    owner: self,
    priority: -10);   // runs before normal-priority replacements

// Conversion: redirect any damage to the player as healing instead
triggers.OnBefore<DamageAction>(
    new Trigger<DamageAction>(
        validate: (cmd, scope) => cmd.Target == myActor,
        execute: (cmd, scope) =>
        {
            scope.Replace(new HealActorAction { Target = myActor, Amount = cmd.Amount });
            return Task.CompletedTask;
        }),
    owner: self);
```

## Open follow-ups

- **Trigger ordering within the fold** — `ITriggerPipeline` owns ordering, but the default (FIFO by registration time, then priority weight) may not match all games. The `priority` weight is the current extension point; richer ordering schemes (controller-choice, conditional priority) live in custom implementations.

---

*Below: provenance — feature-internal decisions folded from `features/effects/decisions/0003-replacement-priority.md`. Single-feature scope (replacement priority lives within the trigger fold mechanism owned by triggers; effects decides how priorities resolve, and triggers' fold consumes them).*

## Options considered

### Q1 — Replacement priority (formerly ADR-0003 — folded)

Replacement effects ("if X would happen, instead Y") and prevention effects ("X doesn't happen") need a deterministic priority story. Without one, two replacements in the same fold produce nondeterministic outcomes.

- **A — Order replacements by registration time.** Rejected: registration order isn't a deterministic, replay-safe property.
- **B — Player-controlled tie-breaks at every collision.** Rejected: spams decisions for negligible gain in floor cases.
- **C — Fold-ordered priority — replacements declare priority ranks; the trigger fold consumes them in rank order; ties break by stable, explicit ordering (e.g., source entity ID).** **Chosen.** Player-controlled tie-breaks are an opt-in extension via custom `ITriggerPipeline`.

## Rationale

Replacements carry an explicit priority. The trigger fold processes them in rank order. Ties break by a stable, replay-safe rule (e.g., source entity ID). Player-controlled tie-breaks are an opt-in extension. This makes replacement-heavy games (MTG, Hearthstone) deterministic and replayable while keeping the floor case (Slay-the-Spire-style) cheap.

## Consequences

- **Enables:** deterministic replay of replacement-heavy effects; clear priority story for rule authors; opt-in MTG-style controller-choice via custom pipeline.
- **Constrains:** rule authors must assign priorities; no "implicit" defaults for floor cases that aren't `0`.
- **Cascade:** the priority weight is consumed by `ITriggerPipeline` implementations; games that want different ordering swap the pipeline rather than touching trigger registration.
