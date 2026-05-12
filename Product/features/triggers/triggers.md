---
type: feature
status: settled
references: [features/actions/models/01-command-pipeline.md, features/engine/models/01-game-engine.md, features/engine/models/03-game-rules-runtime.md, features/effects/models/02-effect-scope.md, decisions/0006-two-pipeline-architecture.md]
tags: [#triggers]
---

# triggers — feature

> **Amendment (2026-05-11) per [ADR-0013](Product/decisions/0013-engine-pipeline-orchestration.md).** Three structural changes land in this feature:
>
> 1. **Engine holds `ITriggerPipeline`, not the action pipeline.** The fold is invoked from `engine`'s drain switch at the before-fold and after-fold points. The action pipeline (single-concern under ADR-0013) no longer references triggers at runtime.
> 2. **`runtime.FilterTriggers` retires.** Eligibility moves fully into `Trigger<T>.Validate(cmd, scope)` (per-trigger). `TriggerSystem` internally walks its registry, calls `Validate` per candidate, and folds the passing ones. No upstream filter call.
> 3. **Folds run for all 4 `GameCommand` siblings.** `GameAction`, `GameSignal`, **`Request<TResponse>`**, and **`Response`** all participate in the OnBefore/OnAfter fold — needed for real Magic-style patterns: `OnBefore<TargetRequest>` for redirect/shroud, `OnAfter<TargetRequest>` for "when-targeted" reactions, `OnBefore<Response>` to constrain answers, `OnAfter<Response>` for reactions on an answered request. The pre-cascade `CompositeAction` sibling retires; its replacement `CompositeMutation` lives outside the `GameCommand` family (reached via `scope.Execute`) and does not participate in the fold.
>
> Triggers feature still owns: `Trigger<T>`, `ITriggerSystem`, `ITriggerPipeline`, fold semantics, in-memory registry, register/unregister lifecycle via `AbilityDefinition`. Same `OnBefore<T>` / `OnAfter<T>` API is used at game install (most triggers) and at instance spawn/leave (per-card abilities) — one mechanism, varying timing; no "static vs dynamic" categories.

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

The feature has **two Models**, organized along the type / orchestrator split:

- **[`01-trigger.md`](features/triggers/models/01-trigger.md)** — the **type** half: `Trigger<T>` reaction class (`Validate` + `Execute`); `ITriggerSystem` subscription surface; `AbilityDefinition.Register` per-instance lifecycle; closure-capture for per-instance data. Q2 provenance (`OnBefore<T>` / `OnAfter<T>` naming).
- **[`02-trigger-pipeline.md`](features/triggers/models/02-trigger-pipeline.md)** — the **orchestrator** half: `ITriggerPipeline` (`RunBefore` / `RunAfter`); `TriggerFoldResult`; `TriggerMatch`; the fold mechanic; three before-fold outcomes (modify/cancel/replace); replacement-of-replacements; priority + ordering; `FifoPipeline` default + custom impls. Q1 + Q3 provenance.

The flow shown in `## What it is` is the load-bearing structure. The cross-Model synthesis sections below (game-style scheduling, history-when-replaced, reactions-dispatch-channel, signal/composite emit, pipeline integration) describe how `Trigger<T>` and `ITriggerPipeline` interact with the rest of the framework.

## Composition

| Model | Role |
|---|---|
| [`01-trigger.md`](features/triggers/models/01-trigger.md) | `Trigger<T>` (reaction class with `Validate` + `Execute`); `ITriggerSystem` subscription surface; `AbilityDefinition.Register/Unregister` per-instance lifecycle; closure-capture pattern for per-instance data |
| [`02-trigger-pipeline.md`](features/triggers/models/02-trigger-pipeline.md) | `ITriggerPipeline` (the fold orchestrator); `TriggerFoldResult` + `TriggerMatch` supports; three before-fold outcomes; replacement-of-replacements; priority + ordering; `FifoPipeline` default + game-customized impls |

## Reactions and the dispatch channel

Reaction commands dispatched from inside a trigger go through the same channel as everything else — `engine.Dispatch`. There is no separate reaction queue.

```csharp
// "Whenever another card is healed, draw a card"
var self    = mySelfRef;     // Ref<TSubclass> — typed identity of the registering entity
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

The channel receives the dispatched action, validates it through `pipeline.Validate`, and enqueues it on the engine's strict-FIFO command queue. Per [ADR-0006](Product/decisions/0006-two-pipeline-architecture.md) the runtime does **not** gate intake or route commands — command-queue routing is framework-owned. The `GameRulesRuntime` contract that supplies `FilterTriggers` (upstream availability filter) and `Evaluate` (state-based follow-ups) lives in [`engine/03-game-rules-runtime.md`](features/engine/models/03-game-rules-runtime.md).

### Game-style scheduling

The differentiation between game styles lives at the **effect** level, not at command-queue routing. Per [ADR-0006](Product/decisions/0006-two-pipeline-architecture.md), commands are framework-FIFO; what differs is *when* triggered effects start.

| Style | How triggered effects are scheduled |
|---|---|
| Slay the Spire | Reactions dispatched from `scope.Dispatch` enter framework FIFO; with the queue empty at trigger-fire time they run as the next command — "on the spot" semantics. |
| Hearthstone | Same FIFO behaviour. Reactions queue normally and run after the current command's pipeline completes. |
| MTG | The stack is a **game-state zone**, not the engine queue. Triggered abilities push themselves into the stack zone via state mutation; `runtime.Evaluate` watches the stack and dispatches `ResolveTopOfStack`-style follow-ups. Resolution semantics (LIFO, priority passes) live in game state plus `Evaluate`. |

The pipeline is the same in all three cases. Effect-level orchestration is the customizable layer; the command queue is not.

## History when replaced

When command A is replaced by B in the before-fold:

- A's envelope is closed and published with `CommandResult.Replaced` — appears in history as a command that was dispatched but did not execute.
- B is dispatched fresh via `engine.Dispatch` (validates + enqueues onto the framework FIFO command queue). B gets its own full fold from scratch. B's envelope is a sibling, not a child, of A's.

History shows both: "A was replaced" and "B executed." The causal link between them is preserved via `OriginCommandId` threading if A and B share the same originating intent. See [`actions/04-history-envelope.md`](features/actions/models/04-history-envelope.md) for envelope shape.

## Emitting `GameSignal`s and `CompositeAction`s from a handler

Handlers emit `GameSignal`s and execute `CompositeAction`s through the scope. Both pass through their own trigger folds before/after — the same `ITriggerPipeline` runs them, with no handler step between the folds for these types:

```csharp
// Inside a handler:
scope.Emit(new ArmorBrokenSignal(target));          // GameSignal — before/after triggers fire; no handler
scope.Execute(new MoveCardComposite(source, zone)); // CompositeAction — before/after triggers fire; executes inline
```

`scope.Emit` and `scope.Execute` are the scope-level equivalents of `engine.Dispatch` for non-queued types. A handler should never dispatch another `GameAction` directly — that's an `Effect`'s responsibility. `CompositeAction` and `GameSignal` use a lighter path through the pipeline: trigger folds only, no handler, no envelope of their own; their entries fold into the parent command's envelope.

## Pipeline integration

The full `pipeline.Execute(action)` shape (envelope open → filter → before-fold → handler → after-fold → envelope close) is owned by [`actions/01-command-pipeline.md` §Pipeline mechanics](features/actions/models/01-command-pipeline.md). Trigger-side notes:

- The action pipeline holds `ITriggerPipeline` (execution surface); card abilities hold `ITriggerSystem` (subscription surface). One `TriggerSystem` class implements both.
- Before/after folds run only the filtered `available` list; the upstream filter is `GameRulesRuntime.FilterTriggers` ([`engine/03-game-rules-runtime.md`](features/engine/models/03-game-rules-runtime.md)).
- Replacement-of-replacement is fold-natural: each fold step's condition checks the *current* command, so substituting `DealDamageAction → HealAction` automatically routes through `HealAction`-watching triggers and skips `DealDamageAction`-watching ones.

## Invariants

Cross-Model rules that hold across both Models and shape how downstream features must use triggers.

- **Phase order is strict.** Before → Execute → After. No phases overlap, no re-entry within a single command.
- **Replacement creates a fresh fold.** When a before-fold trigger calls `scope.Replace(next)`, the original command does not execute; `next` dispatches as a fresh command through the channel and runs through its own full trigger fold from scratch.
- **After-phase `scope.Cancel` / `scope.Replace` are no-ops.** The command already executed.
- **Handlers never dispatch `GameAction` directly.** Sequencing multiple `GameAction`s is an `Effect`'s responsibility. Triggers may dispatch reactions; effects may sequence; handlers may not.
- **Registry is queried upstream and filtered by `GameRulesRuntime.FilterTriggers` before the pipeline receives candidates.** The pipeline does not query the trigger registry itself.
- **`ITriggerSystem` and `ITriggerPipeline` are split surfaces.** Subscription (game code) vs. execution (pipeline internals). One `TriggerSystem` class implements both.
- **`owner` is `Reference`-typed.** Per ADR-0005, stable across `CardRef` rotations.

## Boundaries

**Owns:** `Trigger<T>` base class; `ITriggerSystem` and `ITriggerPipeline` interfaces; `TriggerMatch`, `TriggerFoldResult`, `TriggerPhase` types; the trigger fold mechanic (sequential fold, cancel/replace via scope, fresh-fold-on-replace); priority + ordering policy; `AbilityDefinition.Register` / `Unregister` contract.

**Does not own:**

- The `GameCommand` / `GameAction` / `CompositeAction` / `GameSignal` type hierarchy → [`actions/01-command-pipeline.md`](features/actions/models/01-command-pipeline.md)
- Command pipeline (envelope, handler dispatch, history) → `actions/`
- Engine evaluation loop, framework FIFO routing → `engine/`
- `GameRulesRuntime` (the runtime that supplies `FilterTriggers` and `Evaluate`) → `engine/`
- Effect orchestration, async dispatch, `IEffectScope` → `effects/`
- Per-game richer trigger ordering (controller-choice, named tiers) — owners of custom `ITriggerPipeline` implementations express it; the framework default is FIFO with `Priority` ascending.

## Dependencies

*Mirrors the front-matter `references:` array in human-readable form.*

- **Upstream:** `actions/` (pipeline that hosts the trigger fold; `HistoryEnvelope` shape; `CommandResult`; `GameCommand` type hierarchy), `engine/` (evaluation loop, framework-FIFO routing per ADR-0006, `GameRulesRuntime` contract — `FilterTriggers`, `Evaluate`), `effects/` (`IEffectScope` — the shared execution surface).
- **Downstream:** `effects/` (effects subscribe via the same `IEffectScope` surface triggers use; replacement-prevention layer in `effects/02` builds on the fold), game-side rules code (registers abilities via `AbilityDefinition`).

## Open

See `ledgers/OPEN-ITEMS.md`:

- **Subscription lifecycle** — when `Register`/`Unregister` is called; relationship to zone transitions and game lifecycle.
- **Trigger ordering** — what determines sequence within the fold beyond the `Priority` int. Default is FIFO tie-break; richer policies deferred.
- **Async suspension** — triggers that require player input (targeting, choices) inside the fold. Currently fold runs synchronously to completion.
- **Stash release mechanics** — MTG-style stash needs a `Release(key)` call and ordering rules. Not designed here.
