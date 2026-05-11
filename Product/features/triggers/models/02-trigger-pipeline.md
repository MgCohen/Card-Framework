---
type: model
status: settled
references: [features/triggers/models/01-trigger.md, features/actions/models/01-command-pipeline.md, features/effects/models/02-effect-scope.md, features/engine/models/03-game-rules-runtime.md, decisions/0006-two-pipeline-architecture.md]
tags: [#triggers, #pipeline, #fold, #model]
---

# Trigger Pipeline — model

> **Amendment (2026-05-11) per [ADR-0013](Product/decisions/0013-engine-pipeline-orchestration.md).** Two changes:
>
> 1. **Engine holds `ITriggerPipeline`**, not the action pipeline. The fold is invoked from the engine's drain switch (before-fold + after-fold for `GameAction`; both folds for `GameSignal`; bypassed for `Request` / `Response` / `CompositeAction`).
> 2. **No upstream `FilterTriggers` filter call.** `ITriggerPipeline.RunBefore` / `RunAfter` now own filtering internally — they walk the in-memory registry for type `T`, call each candidate's `Trigger<T>.Validate`, and fold the passing ones. The "candidate list filtered upstream" narrative below reflects the pre-cascade shape.
>
> Registry storage is in-memory in `TriggerSystem` for v1. Migration to a Store-resident slice deferred until the slice-linked-services pattern formalizes.

## What it is

`ITriggerPipeline` is the **pipeline-internal execution surface** that runs the trigger fold. It receives a filtered candidate list (filtered upstream by `GameRulesRuntime.FilterTriggers`) and executes them in priority order, producing a `TriggerFoldResult` for the before-fold or running reactions to completion for the after-fold. Game authors never call this directly — `ITriggerSystem` is their surface; `ITriggerPipeline` is the framework's. The framework ships a default `FifoPipeline`; games that need richer ordering (MTG-style controller-choice replacement order) swap in a custom implementation.

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
```

## Contracts

### `ITriggerPipeline` interface

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
```

The `available` list is **passed in** — the pipeline does not query the trigger registry directly. The registry is queried upstream by the action pipeline (per [`actions/01-command-pipeline.md`](features/actions/models/01-command-pipeline.md)) and filtered by `GameRulesRuntime.FilterTriggers` before the pipeline receives it. Both methods are `async Task`; the default `FifoPipeline` completes synchronously via `Task.FromResult`.

`ITriggerPipeline` and [`ITriggerSystem`](features/triggers/models/01-trigger.md) are split surfaces: subscription (game code) vs. execution (pipeline internals). One `TriggerSystem` class implements both interfaces.

### `TriggerFoldResult` — the before-fold outcome

```csharp
public sealed record TriggerFoldResult(
    GameCommand  FinalCommand,
    bool         IsCancelled,
    string?      CancelReason,
    bool         IsReplaced,
    GameAction?  Replacement);
```

The action pipeline branches on `IsCancelled` and `IsReplaced` to decide what runs next. `FinalCommand` is the command after fold-side modifications (records are immutable, so "modification" is a `with` rewrite landing in `FinalCommand`).

### `TriggerMatch` — registration in pipeline form

```csharp
public sealed record TriggerMatch(
    Reference                              Owner,      // ADR-0005: stable subscription key
    int                                    Priority,   // lower runs first; default 0
    Func<GameCommand, IEffectScope, bool>  Condition,
    Func<GameCommand, IEffectScope, Task>  Execute);
```

Each registration wraps a `Trigger<T>` (typed) as **non-generic delegates** for pipeline dispatch (`Func<GameCommand, ...>`). The `Owner` is the `Reference`-typed subscription key (`Ref<T>` survives `CardRef` rotations per ADR-0005).

### The fold mechanic

All triggers in `available` run in sequence. Each sees the output of the previous (or the scope state left by the previous):

```
c0 = original command
trigger1.Validate(c0, scope) → true → trigger1.Execute(c0, scope)  // may call scope.Cancel/Replace
trigger2.Validate(c1, scope) → true → trigger2.Execute(c1, scope)
...
cN = command passed to handler (unless scope.Cancel was called)
```

A trigger calls `scope.Cancel()` or `scope.Replace(next)` during `Execute`. Either stops the fold. The original command does not execute. A replacement dispatches as a fresh command through the channel — it goes through its own full trigger fold from scratch. `FoldResult` (`Keep`/`Cancel`/`Replace`) is a pipeline-internal discriminator — not part of the game-author API. The pipeline reads scope state after `Execute` returns to determine which outcome occurred.

### Three before-fold outcomes

Every `OnBefore<T>` trigger produces one of three outcomes, communicated through `IEffectScope` rather than a return value:

| Outcome | What happens | Example |
|---|---|---|
| **Modification** | Same type, different property values | Reduce `DealDamageAction.Amount` from 5 to 3 |
| **Cancellation** | Event is stopped; handler never runs | Shield absorbs hit; action cancelled |
| **Substitution** | Different `GameAction` type dispatched in place | `DealDamageAction` → `HealAction` |

```csharp
// Inside Trigger<T>.Execute:
scope.Cancel("shield absorbed");           // cancellation
scope.Replace(new ShieldAbsorbAction(…));  // substitution
// (returning without a scope call)         // modification or no-op
```

Modification is expressed by replacing the command with a copy that has different property values (records are immutable):

```csharp
var reduced = cmd with { Amount = Math.Max(0, cmd.Amount - shieldValue) };
scope.Replace(reduced);
```

### Replacement-of-replacements is fold-natural

No special mechanism is needed. The fold evaluates each trigger's condition against the *current command* — the output of all prior triggers in the fold sequence:

```
c0 = DealDamageAction { Amount: 5 }

trigger B condition: (c) => c is DealDamageAction → true on c0
trigger B react:     DealDamageAction → HealAction → c1 = HealAction { Amount: 5 }

trigger C condition: (c) => c is DealDamageAction → FALSE on c1 — does not fire
trigger D condition: (c) => c is HealAction      → true on c1  — fires normally
```

If B substitutes `DealDamageAction` with `HealAction`, any trigger watching for `DealDamageAction` no longer applies. Any trigger watching for `HealAction` applies as if it was always the event. Correct by construction — no loop detection or special casing required.

### Priority and ordering

Priority is an `int` on each `OnBefore`/`OnAfter` registration call, defaulting to `0`. Lower value runs first in the fold. The default `FifoPipeline` sorts by priority ascending before running (stable sort preserves registration order among ties). A game that wants to change the convention overrides sorting inside its custom `ITriggerPipeline` — the value is just data.

```csharp
// Normal ability — default priority
triggers.OnBefore<DealDamageAction>(condition, react, owner: self);

// Self-preservation ability — runs before other replacements
triggers.OnBefore<DealDamageAction>(condition, react, owner: self, priority: -10);
```

The framework ships a `FifoPipeline : ITriggerPipeline` that stable-sorts by `Priority` ascending, runs the fold, and returns completed tasks. Games that need MTG-style controller-choice ordering swap in a custom implementation that:

1. Detects multiple applicable triggers.
2. Dispatches a `ChooseReplacementOrderAction` (a normal decision, same pattern as `RequestTargetAction`).
3. Awaits the result.
4. Runs the fold in the chosen order.

## Invariants

1. **`available` is filtered upstream.** The pipeline does not query the trigger registry directly; it consumes the filtered list passed in.
2. **Replacement creates a fresh fold.** When a before-fold trigger calls `scope.Replace(next)`, the original command does not execute; `next` dispatches as a fresh command through the channel and runs through its own full trigger fold from scratch.
3. **Cancel / Replace are valid only in before-fold.** After-fold calls swallow silently.
4. **Modification is immutable rewrite.** `FinalCommand` is the post-fold command produced via `with` rewrites; the original record is never mutated in place.
5. **Priority ties break by stable sort.** Registration order preserved among equal-priority triggers in the default `FifoPipeline`.
6. **`ITriggerPipeline` is swappable.** Custom implementations express richer ordering (MTG-style controller choice, alternative tie-break rules); the contract is the two methods + the result shape.

## Decisions

Linked: [`Product/decisions/0006-two-pipeline-architecture.md`](Product/decisions/0006-two-pipeline-architecture.md) — Two-pipeline architecture. Shapes the relationship between the trigger fold and the framework FIFO command queue (the runtime does not gate intake or route commands; effect-level orchestration is the customizable layer).

## Examples

```
Original: DealDamageAction(target=goblin, amount=3)
  trigger1 (Shield ability on goblin):
    Validate → true (target matches)
    Execute  → scope.Replace(new ShieldAbsorbAction(goblin))
  → fold stops; ShieldAbsorbAction dispatches as fresh command
    → runs through its own full before-fold from scratch
    → no handler runs for the original DealDamageAction
```

For inline + subclassed `Trigger<T>` authoring shapes that feed this pipeline, see [`01-trigger.md` §Authoring shapes](features/triggers/models/01-trigger.md).

## Open follow-ups

- **Trigger ordering** — what determines sequence within the fold when multiple triggers match the same type beyond the `Priority` int. FIFO by registration is the default tie-break; richer policies (priority-cohorts, controller choice for ties, registration-time-stamped ordering for replay) are deferred. (`oq-trigger-ordering`)
- **Async suspension** — triggers that require player input (targeting, choices) inside the fold. Currently the fold runs synchronously to completion via `Task.FromResult`; suspension semantics tie to the broader effect-pipeline design.
- **Stash release mechanics** — MTG-style stash needs a `Release(key)` call and ordering rules. Not designed here.

---

*Below: provenance — feature-internal decisions (Q1 + Q3, formerly `triggers/decisions/0001-trigger-fold-semantics.md` + `effects/02 §Q1`) folded on 2026-05-08 / 2026-05-09.*

## Options considered

### Q1 — Trigger fold semantics

When multiple triggers fire on one event, they need to be aggregated into a single decision (or sequence of dispatches) without callers writing event-loop code. The model needs to handle ordering, priority, and replacement uniformly.

- **A — Synchronous broadcast, first listener wins.** Rejected — no story for replacement, prevention, or priority.
- **B — Per-event subscription lists with manual aggregation.** Rejected — every event reinvents the loop; no shared semantics.
- **C — A fold over registered triggers, producing an aggregated `TriggerFoldResult` consumed by the engine.** **Chosen.** Priority and replacement/prevention are expressed as fold steps. The engine consumes the result; effects don't write the loop.

### Q3 — Replacement priority

> Originally a single-feature decision in `effects/02-effect-scope.md` Q1 (when before-fold lived in effects). Folded here on 2026-05-09 when the trigger pipeline became the canonical home for fold ordering (per the type-required Model rule — before-fold is a step inside `ITriggerPipeline.RunBefore`).

Replacement effects ("if X would happen, instead Y") and prevention effects ("X doesn't happen") need a deterministic priority story. Without one, two replacements in the same fold produce nondeterministic outcomes.

- **A — Order replacements by registration time.** Rejected — registration order isn't a deterministic, replay-safe property across save/load and multiplayer.
- **B — Player-controlled tie-breaks at every collision.** Rejected — spams decisions for negligible gain in floor cases.
- **C — Fold-ordered priority.** Replacements declare priority ranks; the trigger fold consumes them in rank order; ties break by stable, explicit ordering. **Chosen.**

Explicit priority on each registration plus stable tie-break gives deterministic replay across replacement-heavy effects without forcing a player decision at every collision. Games that want player-controlled ordering opt in via a custom `ITriggerPipeline` that dispatches a `ChooseReplacementOrderAction`. Authoring cost is small (one `priority` argument with a default of 0); the determinism payoff is large.

## Rationale

The fold mechanic (Q1) gives the framework a single uniform shape for trigger aggregation: every event flows through the same fold, and replacement / cancellation are expressed as fold contributions rather than ad-hoc event-loop code.

Fold-ordered priority (Q3) keeps replay determinism without forcing a user decision at every replacement collision; custom `ITriggerPipeline` implementations recover game-specific ordering (MTG controller choice) through dispatched decision actions.

## Consequences

- **Enables** uniform handling of replacement, prevention, and priority via a single fold mechanism; replay-deterministic ordering via priority ranks + stable tie-break; custom orderings via swappable `ITriggerPipeline` implementations.
- **Constrains** trigger authors to think in terms of fold contributions, not direct dispatch; cancel/replace flow through `scope.Cancel` / `scope.Replace` rather than return values.
- **Cascade:** the `TriggerFoldResult` shape is consumed by [`actions/01-command-pipeline.md`](features/actions/models/01-command-pipeline.md) (pipeline branches on `IsCancelled` / `IsReplaced`); the `GameRulesRuntime.FilterTriggers` upstream filter and framework-FIFO routing contracts are owned by [`engine/03-game-rules-runtime.md`](features/engine/models/03-game-rules-runtime.md) and shaped by [ADR-0006](Product/decisions/0006-two-pipeline-architecture.md).
