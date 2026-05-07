---
type: feature
status: settled
references: [features/actions/models/01-command-pipeline.md, features/engine/models/01-evaluation-loop.md]
tags: [#triggers]
---

# triggers — feature

## What it is

The framework's reactive fan-out. When a command flows through the pipeline, **`OnBefore<T>`** triggers run first (they may modify, cancel, or replace the command), then the handler executes (`GameAction` only), then **`OnAfter<T>`** triggers run (they may dispatch reactions). Triggers are typed by the command shape they observe; the **trigger fold** runs them in sequence so the framework — not the trigger author — handles aggregation, ordering, and replacement uniformly.

```
              GameCommand enters pipeline
                         │
                         ▼
      ┌────────────────────────────────────────┐
      │ Registry query → GameRules.FilterTriggers │
      │ → available triggers list              │
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

Feature-owned terms. Cross-feature terms — `GameCommand`, `GameAction`, `CompositeAction`, `GameSignal` (the type hierarchy is defined here but consumed across `actions/`, `engine/`, `effects/`); `IEffectScope`, `Effect` (defined in `effects/`); `HistoryEnvelope` (defined in `actions/`); `GameRules`, `IntakeResult`, `DrainResult` (defined in `engine/`); `InstanceId`, `ActorId` — live in their owning features and `Product/ledgers/GLOSSARY.md`.

| Term | Meaning |
|---|---|
| `Trigger<T>` | A reactive listener typed by command shape `T`. Has `Validate(cmd, scope)` (condition) and `Execute(cmd, scope)` (reaction body). Shares execution surface with `Effect`. |
| `OnBefore<T>` / `OnAfter<T>` | The two registration phases. Before-fold may modify/cancel/replace; after-fold may dispatch reactions. |
| `ITriggerSystem` | Subscription surface: `OnBefore<T>(...)`, `OnAfter<T>(...)`, `RemoveAll(owner)`. What `AbilityDefinition.Register` calls. |
| `ITriggerPipeline` | Pipeline-internal execution surface: `RunBefore`, `RunAfter`. Game authors never call directly. One `TriggerSystem` class implements both interfaces. |
| `TriggerMatch` | A pairing of a registered trigger with the command type it matches. Filtered by `GameRules.FilterTriggers` before reaching the pipeline. |
| `TriggerFoldResult` | The outcome of a before-fold: `(FinalCommand, IsCancelled, CancelReason, IsReplaced, Replacement)`. |
| `TriggerPhase` | Enum: `Before` or `After`. Passed to `GameRules.FilterTriggers` so the rule can apply phase-specific availability policy. |
| `DispatchOrigin` | Enum: `PlayerIntent`, `TriggerReaction`, `Timer`, `Internal`. Carried with each dispatch so `OnIncomingCommand` can route by origin. |
| `AbilityDefinition` | Authored type that registers/unregisters per-instance triggers via `ITriggerSystem`. `Register(triggers, self)` / `Unregister(triggers, self)`. |

## Architecture

The feature has one Model — the Trigger Fold Model — which defines the type hierarchy (`GameCommand` and its three subtypes), the trigger fold mechanic, the two registration surfaces (`ITriggerSystem` for game code, `ITriggerPipeline` for pipeline-internal execution), and the per-instance registration pattern via `AbilityDefinition`.

The flow above is the load-bearing structure: registry-query → `GameRules.FilterTriggers` (availability policy: phase gating, mana fast-path, stack lock, target validation) → `ITriggerPipeline` runs the fold → if cancelled/replaced, branch; otherwise handler executes → after-fold runs reactions. Reactions dispatched from inside a trigger flow through the same channel as everything else (no separate reaction queue); the channel routes them via `GameRules.OnIncomingCommand` based on `DispatchOrigin`.

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
- **Registry is queried upstream and filtered by `GameRules.FilterTriggers` before the pipeline receives candidates.** The pipeline does not query the trigger registry itself. This keeps availability policy (phase gating, mana fast-path, stack lock) game-controlled.
- **`ITriggerSystem` and `ITriggerPipeline` are split surfaces.** Subscription (game code) vs. execution (pipeline internals). One `TriggerSystem` class implements both.

## Boundaries

**Owns:** the `GameCommand` / `GameAction` / `CompositeAction` / `GameSignal` type hierarchy; `Trigger<T>` base type; `ITriggerSystem` and `ITriggerPipeline` interfaces; `TriggerMatch`, `TriggerFoldResult`, `TriggerPhase`, `DispatchOrigin` types; the trigger fold mechanic (sequential fold, cancel/replace via scope, fresh-fold-on-replace); `AbilityDefinition.Register` / `Unregister` contract.

**Does not own:**
- Command pipeline (envelope, handler dispatch, history) → `actions/`
- Engine evaluation loop, intake routing → `engine/`
- `GameRules` (the rule that calls `FilterTriggers` etc.) → `engine/`
- Effect orchestration, async dispatch → `effects/`
- Per-Model trigger ordering / priority weighting (deferred — see Open)

## Dependencies

- **Upstream:** `actions/` (pipeline that hosts the trigger fold; `HistoryEnvelope` shape), `engine/` (evaluation loop, `GameRules` contract — `OnIncomingCommand`, `FilterTriggers`).
- **Downstream:** `effects/` (effects subscribe via the same `IEffectScope` surface triggers use; replacement-prevention layer in `effects/02` builds on the fold), game-side rules code (registers abilities via `AbilityDefinition`).

## Open

See `ledgers/OPEN-ITEMS.md`:
- **Subscription lifecycle** — when `Register`/`Unregister` is called; relationship to zone transitions and game lifecycle.
- **Trigger ordering** — what determines sequence within the fold when multiple triggers match the same type. FIFO by registration? Priority weight? Game-configurable?
- **Async suspension** — triggers that require player input (targeting, choices) still unresolved. Unblocks the broader suspension design.
- **Stash release mechanics** — MTG-style stash needs a `Release(key)` call and ordering rules. Not designed here.
