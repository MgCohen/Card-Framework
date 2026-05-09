---
type: model
status: settled
references: [features/effects/models/01-effect.md, features/triggers/models/01-trigger-fold.md, features/engine/models/02-gamerules-contract.md]
tags: [#effects, #replacement, #prevention]
---

# Replacement And Prevention — model

## What it is

The rules-level semantics for replacement and prevention effects — how the before-fold is interpreted, how competing replacements are handled, and where game-specific ordering policy lives. Every `OnBefore<T>` trigger produces one of three outcomes (Modification / Cancellation / Substitution) communicated through `IEffectScope`; `ITriggerPipeline` owns the mechanical fold (collect, order, run); `GameRulesRuntime.FilterTriggers` owns availability policy.

```
candidates ── rules.FilterTriggers ──▶ available
                                          │
                                          ▼
                          triggerPipeline.RunBefore(action, available, scope)
                                          │
                          ┌───────────────┼───────────────┐
                          │               │               │
                       (modify)        (cancel)        (replace)
                          │               │               │
                          ▼               ▼               ▼
                   handler runs       envelope:       envelope:
                   on mutated cmd     Cancelled       Replaced; engine.Dispatch(next)
```

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
// ADR-0005: `owner` is a stable subscription key — Ref<T> survives CardRef rotations.
// Trigger subscriptions outlive individual object references; this use is boundary-correct per ADR-0005.
void OnBefore<T>(Trigger<T> trigger, Reference owner) where T : GameAction;

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

### `ITriggerPipeline` — role in the before-fold

Trigger execution is owned by `ITriggerPipeline` (interface body, `TriggerFoldResult`, `TriggerMatch` defined in [`triggers/01-trigger-fold.md` §Execution](features/triggers/models/01-trigger-fold.md)). For replacement / prevention, what matters here:

- The pipeline receives an already-filtered `available` list — `GameRulesRuntime.FilterTriggers` decides eligibility upstream (see "Availability policy" below).
- The pipeline orders the matches (FIFO, priority, choice-dispatch) and runs the fold.
- Triggers communicate Modification / Cancellation / Substitution through `IEffectScope`; the pipeline reads scope state after each `Execute` to determine the outcome.

Both `RunBefore` and `RunAfter` are `async Task` so custom pipelines can genuinely await a decision (e.g. controller-choice ordering) without special casing.

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

### `GameRulesRuntime.FilterTriggers` — availability policy

`GameRules` decides which registered triggers are eligible to fire for a given command in the current game state. It does not collect, order, or execute them.

```csharp
public abstract class GameRulesRuntime<TDefinition>
    where TDefinition : GameRulesDefinition
{
    // Returns the subset of candidates that are eligible right now.
    // Default: return candidates unchanged.
    public virtual IReadOnlyList<TriggerMatch> FilterTriggers(
        IReadOnlyList<TriggerMatch> candidates,
        GameCommand                 command,
        TriggerPhase                phase)
        => candidates;

    // State-based follow-up actions (see engine/02-gamerules-contract.md).
    public virtual IReadOnlyList<GameAction> Evaluate(
        GameAction    justExecuted,
        CommandResult result)
        => Array.Empty<GameAction>();

    // Effect-pipeline orchestration surface — separate design pass; tracked as oq-effect-pipeline.
}
```

This separates two distinct concerns:

| Concern | Owner |
|---|---|
| "Which triggers are registered for this type?" | Trigger registry (internal to pipeline) |
| "Which of those are eligible right now?" | `GameRules.FilterTriggers` |
| "In what order do they run?" | `ITriggerPipeline` |
| "What does each trigger do?" | `AbilityDefinition` |

`GameRules` never holds a reference to `ITriggerPipeline` or the trigger registry. It only receives a list of `TriggerMatch` candidates and returns a filtered subset.

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

**Stateful prevention** ("prevent the next N damage") is an ongoing effect that manages its own state — it reduces the payload and decrements an internal counter, deleting itself when exhausted. Ongoing effects are covered in [`03-ongoing-effects.md`](features/effects/models/03-ongoing-effects.md); stateful prevention is one instance of them.

### History when an action is replaced

When A is replaced by B:

- A's envelope is closed and published with `CommandResult.Replaced` — it appears in history as a command that was dispatched but did not execute.
- B is dispatched fresh via `engine.Dispatch` (validates + enqueues onto the framework FIFO command queue). B gets its own full fold from scratch. B's envelope is a sibling, not a child, of A's.

This means the history shows both: "A was replaced" and "B executed." The causal link between them is preserved via `OriginCommandId` threading if A and B share the same originating intent.

### Extension points summary

| What you want to customize | Where |
|---|---|
| FIFO → priority → controller-choice ordering | Swap `ITriggerPipeline` implementation |
| Phase gating, mana-ability skipping, target validation | Override `GameRules.FilterTriggers` |
| MTG controller-choice for competing replacements | Custom `ITriggerPipeline` that dispatches a `Decision` |
| Self-replacement priority | Custom `ITriggerPipeline` sorting by `owner == affectedEntity` |

## Invariants

- **Three outcomes exhaust before-fold semantics.** Modification, cancellation, substitution — no fourth case. All communicated through `IEffectScope` (`scope.Cancel` / `scope.Replace` / no-call), not return values.
- **Replacement-of-replacement is fold-natural.** No loop detection or special casing — each trigger evaluates its condition against the current command (the output of prior triggers).
- **Priority is total ordering with stable tie-break.** Lower priority runs first; ties break stably (registration order in default `FifoPipeline`). Determinism is required for replay.
- **Pipeline never queries the registry.** `ITriggerPipeline` receives the `available` list pre-filtered. The registry → `GameRules.FilterTriggers` → pipeline pipeline is one-directional.
- **Replaced commands re-fold from scratch.** `engine.Dispatch(before.Replacement)` enqueues `next` as a fresh command; it goes through its own full fold (not the parent's continuation).
- **Filter ≠ order ≠ execute.** Three concerns, three owners: `GameRules` filters availability, `ITriggerPipeline` orders, `AbilityDefinition` executes. They do not share interfaces.

### Example filter uses

- **Phase gating** — no triggers fire during setup
- **Mana abilities** — skip the trigger pipeline entirely for speed-mana costs
- **Stack restriction** — triggers suppressed while a specific resolution lock is active
- **Target validation** — a trigger requires a legal target to exist before it is considered applicable

## Decisions

*n/a — no elevated ADR.* (Replacement priority applies to a single feature; folded as Q1 below.)

## Examples

See [`triggers/models/01-trigger-fold.md`](features/triggers/models/01-trigger-fold.md) for the full subscription / fold mechanics. The contracts above are exercised in:

- **Shield absorbing damage** — `OnBefore<DamageAction>` calls `scope.Cancel("shield absorbed")`; the `DealDamageHandler` never runs; the original envelope publishes with `CommandResult.Cancelled`.
- **Damage-to-heal substitution** — a "convert damage to healing" ability calls `scope.Replace(new HealAction { Amount = original.Amount, Target = original.Target })`; the original envelope publishes `Replaced`; `engine.Dispatch(healAction)` queues the `HealAction` for fresh fold.
- **MTG controller-choice ordering** — the game ships a `MtgPipeline : ITriggerPipeline` that, when `available.Count > 1`, dispatches `ChooseReplacementOrderAction { Replacements: available.Select(t => t.Owner) }`, awaits the player's chosen ordering, then runs the fold in that order.
- **Phase gating** — `GameRulesRuntime.FilterTriggers` returns `Array.Empty<TriggerMatch>()` while `state.Phase == Phase.Setup`, blocking all triggers during setup without modifying registrations.

## Open follow-ups

- **`CommandResult` cancellation shape** — flag on the base record vs typed `CancelledResult` subtype. Cosmetic; pin during command-pipeline cleanup. (Tracked in [`actions/03-command-handler.md`](features/actions/models/03-command-handler.md).)
- **Trigger ordering stability across save/load** — `FifoPipeline`'s registration-order tie-break depends on subscription registration sequence; whether that's stable across snapshots is tied to `oq-engine-builder`.

---

*Below: provenance — how replacement priority was settled. Single-feature scope (no project-wide ADR applies; folded into Model provenance per §4.5 on 2026-05-08). Original ADR-0003 landing 2026-04-29.*

## Options considered

### Q1 — Replacement priority

Replacement effects ("if X would happen, instead Y") and prevention effects ("X doesn't happen") need a deterministic priority story. Without one, two replacements in the same fold produce nondeterministic outcomes.

- **A — Order replacements by registration time.** Rejected — registration order isn't a deterministic, replay-safe property across save/load and multiplayer.
- **B — Player-controlled tie-breaks at every collision.** Rejected — spams decisions for negligible gain in floor cases.
- **C — Fold-ordered priority.** Replacements declare priority ranks; the trigger fold consumes them in rank order; ties break by stable, explicit ordering (e.g., source entity ID). **Chosen.**

## Rationale

Explicit priority on each registration plus stable tie-break gives deterministic replay across replacement-heavy effects without forcing a player decision at every collision. Games that want player-controlled ordering opt in via a custom `ITriggerPipeline` that dispatches a `ChooseReplacementOrderAction`; the framework default is silent and deterministic. Authoring cost is small (one `priority` argument with a default of 0); the determinism payoff is large.

## Consequences

- **Enables** deterministic replay of replacement-heavy effects; per-game customization (FIFO, priority, MTG-style player choice) via `ITriggerPipeline` swap; predictable testing of replacement chains.
- **Constrains** rule authors to assign priorities for non-default ordering; no implicit defaults beyond `0`; tie-break rule (registration order in `FifoPipeline`) must hold across snapshots.
