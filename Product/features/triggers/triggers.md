---
type: feature
status: settled
references: [features/actions/models/01-command-pipeline.md, features/engine/models/01-evaluation-loop.md, features/engine/models/02-gamerules-contract.md]
tags: [#triggers]
---

# triggers — feature

## What it is

The framework's reactive fan-out. When a `GameCommand` flows through the pipeline, **`OnBefore<T>`** triggers run first (they may modify, cancel, or replace the command), then the handler executes (`GameAction` only), then **`OnAfter<T>`** triggers run (they may dispatch reactions). Triggers are typed by the command shape they observe; the **trigger fold** runs them in sequence so the framework — not the trigger author — handles aggregation, ordering, and replacement uniformly.

```
              GameCommand enters pipeline
                         │
                         ▼
      ┌────────────────────────────────────────┐
      │ Registry query → GameRulesRuntime.     │
      │ FilterTriggers → available triggers    │
      └────────────────────────────────────────┘
                         │
                         ▼
            OnBefore<T> fold        ← may modify / cancel / replace
                         │
                         ▼
              handler.Execute        ← GameAction only
                         │
                         ▼
            OnAfter<T> fold          ← may dispatch reactions
                         │
                         ▼
                    HistoryEnvelope
```

This replaces the older `CommandPreHook` / `CommandReaction` placeholder design.

## Vocabulary

**Feature-owned terms only.** Cross-feature terms used by this feature — `GameCommand`, `GameAction`, `CompositeAction`, `GameSignal`, `IEffectScope`, `Effect` (effects/), `HistoryEnvelope`, `CommandResult` (actions/), `GameRulesRuntime`, `FilterTriggers`, `Evaluate` (engine/), `Reference`, `Ref<T>`, `IActor`, `ActorId` — live in their owning features or `Product/ledgers/GLOSSARY.md`; link to their canonical home, don't restate.

| Term | Meaning |
|---|---|
| `Trigger<T>` | A reactive listener typed by command shape `T`. Has `Validate(cmd, scope)` (condition) and `Execute(cmd, scope)` (reaction body). Shares execution surface with `Effect`. |
| `OnBefore<T>` / `OnAfter<T>` | The two registration phases. Before-fold may modify/cancel/replace; after-fold may dispatch reactions. |
| `ITriggerSystem` | Subscription surface: `OnBefore<T>(...)`, `OnAfter<T>(...)`, `RemoveAll(owner)`. What `AbilityDefinition.Register` calls. |
| `ITriggerPipeline` | Pipeline-internal execution surface: `RunBefore`, `RunAfter`. Game authors never call directly. One `TriggerSystem` class implements both interfaces. |
| `TriggerMatch` | A pairing of a registered trigger with the command type it matches. Filtered by `GameRulesRuntime.FilterTriggers` before reaching the pipeline. |
| `TriggerFoldResult` | The outcome of a before-fold: `(FinalCommand, IsCancelled, CancelReason, IsReplaced, Replacement)`. |
| `TriggerPhase` | Enum: `Before` or `After`. Passed to `GameRulesRuntime.FilterTriggers` so the rule can apply phase-specific availability policy. |
| `AbilityDefinition` | Authored type that registers/unregisters per-instance triggers via `ITriggerSystem`. `Register(triggers, self)` / `Unregister(triggers, self)`. |

## Architecture

The feature has one Model. See `models/01-trigger-fold.md` for the type hierarchy (`GameCommand` and its three subtypes), the trigger fold mechanic, the two registration surfaces (`ITriggerSystem` for game code, `ITriggerPipeline` for pipeline-internal execution), and the per-instance registration pattern via `AbilityDefinition`. The flow shown in `## What it is` is the load-bearing structure.

## Composition

| Model | Role |
|---|---|
| [`models/01-trigger-fold.md`](models/01-trigger-fold.md) | Type hierarchy, trigger fold mechanic, subscription + execution surfaces, per-instance registration, pipeline-shape integration |

## Invariants

Cross-Model rules that hold across the Model and shape how other features must use triggers.

- **Phase order is strict.** Before → Execute → After. No phases overlap, no re-entry within a single command.
- **Replacement creates a fresh fold.** When a before-fold trigger calls `scope.Replace(next)`, the original command does not execute; `next` dispatches as a fresh command through the channel and runs through its own full trigger fold from scratch.
- **After-phase `scope.Cancel` / `scope.Replace` are no-ops.** The command already executed. Trigger authors writing in after-phase reaction bodies see those calls swallowed.
- **Handlers never dispatch `GameAction` directly.** Sequencing multiple `GameAction`s is an `Effect`'s responsibility. Handlers apply state changes (mutations, `CompositeAction`s) and return a `CommandResult`. Triggers may dispatch reactions; effects may sequence; handlers may not.
- **Registry is queried upstream and filtered by `GameRulesRuntime.FilterTriggers` before the pipeline receives candidates.** The pipeline does not query the trigger registry itself. This keeps availability policy (phase gating, mana fast-path, stack lock) game-controlled.
- **`ITriggerSystem` and `ITriggerPipeline` are split surfaces.** Subscription (game code) vs. execution (pipeline internals). One `TriggerSystem` class implements both.

## Boundaries

**Owns:** the `GameCommand` / `GameAction` / `CompositeAction` / `GameSignal` type hierarchy; `Trigger<T>` base type; `ITriggerSystem` and `ITriggerPipeline` interfaces; `TriggerMatch`, `TriggerFoldResult`, `TriggerPhase` types; the trigger fold mechanic (sequential fold, cancel/replace via scope, fresh-fold-on-replace); `AbilityDefinition.Register` / `Unregister` contract.

**Does not own:**
- Command pipeline (envelope, handler dispatch, history) → `actions/`
- Engine evaluation loop, framework FIFO routing → `engine/`
- `GameRulesRuntime` (the runtime that supplies `FilterTriggers` and `Evaluate`) → `engine/`
- Effect orchestration, async dispatch → `effects/`
- Per-Model trigger ordering / priority weighting (deferred — see Open)

## Dependencies

*Mirrors the front-matter `references:` array in human-readable form (per §3).*

- **Upstream:** `actions/` (pipeline that hosts the trigger fold; `HistoryEnvelope` shape; `CommandResult`), `engine/` (evaluation loop, framework-FIFO routing per ADR-0006, `GameRulesRuntime` contract — `FilterTriggers`, `Evaluate`).
- **Downstream:** `effects/` (effects subscribe via the same `IEffectScope` surface triggers use; replacement-prevention layer in `effects/02` builds on the fold), game-side rules code (registers abilities via `AbilityDefinition`).

## Open

See `ledgers/OPEN-ITEMS.md`:
- **Subscription lifecycle** — when `Register`/`Unregister` is called; relationship to zone transitions and game lifecycle.
- **Trigger ordering** — what determines sequence within the fold when multiple triggers match the same type. FIFO by registration? Priority weight? Game-configurable?
- **Async suspension** — triggers that require player input (targeting, choices) still unresolved. Unblocks the broader suspension design.
- **Stash release mechanics** — MTG-style stash needs a `Release(key)` call and ordering rules. Not designed here.
