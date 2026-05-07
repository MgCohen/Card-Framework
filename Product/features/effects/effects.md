---
type: feature
status: settled
references: [features/triggers/models/01-trigger-fold.md, features/actions/models/01-command-pipeline.md, features/engine/models/01-evaluation-loop.md, features/engine/models/03-snapshot-pipeline.md, features/entities/models/02-runtime-surface.md]
tags: [#effects]
---

# effects — feature

## What it is

The framework's lifetime layer. **Effects** orchestrate sequences of `GameAction`s — they're the only layer that can sequence commands. They consume player decisions inline (await results from `scope.Dispatch`), participate in **replacement and prevention** through the trigger fold, and persist as **ongoing attachment entities** that subscribe to triggers and tick over time. The full mid-execution shape (services, decisions, await semantics, mutation discipline) is settled in Model 04.

```
        Effect (orchestrator)
              │
              ▼  scope.Dispatch (immediate, inline; awaits CommandResult)
       GameAction → before-fold → handler → after-fold → envelope
              │       (may cancel/replace)            │
              │                                       │
              ▼                                       ▼
       Decision-as-command:                    Reactions queued via
       handler awaits                          engine.Dispatch (separate
       Resolver.Wait<TCmd>()                   envelope; own fold)
              │
              ▼
       Player intent → IntentMapper →
       SelectTargetCommand → engine.Dispatch
       → awaiter.TryResolve matches → handler resumes
```

Effects are not authored as `Task<Result>` returned from a handler — that would conflate animation, decision, and simulation pauses. Each layer (presentation animation, decision player input, engine resolver yield) owns its own continuation; effects compose across layers via `IEffectScope`.

## Vocabulary

Feature-owned terms. Cross-feature terms — `GameCommand`, `GameAction`, `CompositeAction`, `GameSignal`, `Trigger<T>`, `OnBefore<T>`, `OnAfter<T>`, `TriggerMatch`, `TriggerFoldResult`, `ITriggerSystem`, `ITriggerPipeline`, `TriggerPhase`, `DispatchOrigin` (defined in `triggers/`); `GameRules`, `OnIncomingCommand`, `Evaluate`, `OnQueueDrained`, `IAvailableActions`, `AvailableActionsSnapshot`, `Store`, `Mutator<TState, TPayload>`, `IStateScope`, `IStoreScope` (defined in `engine/`); `IntentMapper`, `PlayerIntent`, `IIntentMapper`, `IntakeResult`, `DrainResult`, `CommandResult`, `HistoryEnvelope` (defined in `actions/`); `IEntityRegistry`, `IModifierFactory`, `EntityModifierEntry`, `ModifierId`, `ModifierSource`, `IReadOnlyEntity`, `StateEntity<TDef>` (defined in `entities/`); `InstanceId`, `Card`, `Zone` — live in their owning features and `Product/ledgers/GLOSSARY.md`.

| Term | Meaning |
|---|---|
| `Effect` | First-class abstract type. Per-instance orchestrator. `Validate(scope) → bool` + `Execute(scope) → Task`. Internals opaque to the framework. |
| `IEffectScope` | Effect's runtime surface: `State` (read-only), `Entities`, `Modifiers`, `Dispatch`, `Cancel`, `Replace`. No mutation channel. |
| `IHandlerContext` | Handler's runtime surface: `Store` (read + execute mutators), `Actions` (snapshot writes), `Resolver` (decision awaits), `Entities`, `Modifiers`. |
| `ICommandAwaiter` | Decision-handler's await/resume mechanic. `Wait<TCommand>(ct)` registers a `cmd is TCommand` predicate; `TryResolve(cmd)` matches on every dispatch. |
| Layer hierarchy | Effect → GameAction → CompositeAction. Only Effect can sequence; only handlers invoke mutators; effects never mutate. |
| Two dispatch paths | `engine.Dispatch(action)` (deferred, queued, own envelope) vs `scope.Dispatch(action)` (immediate, inline, effect awaits). Not interchangeable. |
| Decision-as-command | A decision (target, choice, etc.) is a `GameAction` that opens UI, awaits player response, returns chosen value in `CommandResult`. Type-only routing — no `DecisionId`. |
| Replacement | Before-fold outcome where a trigger calls `scope.Replace(next)` — original action's envelope closes with `Replaced`; `next` dispatches as fresh command through its own full fold. |
| Prevention | Modification with a zero floor (e.g. shield reduces damage to 0). Stateful prevention is an ongoing-effect attachment that owns its own state. |
| Attachment entity | Ongoing effect modeled as an entity (definition, `InstanceId`, variables, subscriptions). Created via `CreateAndAttachAction`, destroyed via `DestroyEntityAction`; framework Cleanup hook unregisters subscriptions. |
| Mutator payload | Dedicated record type (e.g. `MoveCardPayload`), not the command. One payload may fan out to N registered mutators; `Store.Execute(payload)` runs every one and commits atomically. |

## Architecture

The feature has four Models. The execution arc spans them:

1. **Dispatch initiation** (Model 01 §Two Dispatch Paths). An effect calls `scope.Dispatch(action)` — runs the action's full lifecycle inline (before-fold → handler → after-fold → envelope published). The effect awaits and cannot proceed until the action completes (including any decision it requires).
2. **Decision routing** (Model 04 §Decision Routing). When an action's handler awaits player input, it calls `ctx.Resolver.Wait<TCommand>()`, writes a snapshot via `ctx.Actions.Set(...)`, suspends. Player intent → `IntentMapper.Map` → `SelectTargetCommand` → `engine.Dispatch` → pre-pipeline `awaiter.TryResolve` matches → handler resumes. Type-only routing; no `DecisionId`.
3. **Replacement / prevention** (Model 02). Before-fold runs through `ITriggerPipeline.RunBefore` (after `GameRules.FilterTriggers` filters availability). Triggers may `scope.Cancel(reason)` (action stopped), call `scope.Replace(next)` (action replaced), or modify the command (return without scope call). Replacement creates a fresh fold for `next`.
4. **Handler execution** (Model 04 §Mutation Discipline). If not cancelled/replaced, the handler runs. Handlers invoke mutators via `ctx.Store.Execute(payload)`; one payload fans out to all registered mutators atomically. Effects do not mutate.
5. **Ongoing effects** (Model 03). Attachment entities subscribe to triggers (`OnAfter<TurnStartSignal>`, etc.), tick or react over time, dispatch `DestroyEntityAction` on themselves when done. The framework Cleanup hook calls `RemoveAll(self)` on subscriptions and `RemoveModifiersBySourcePayload(target, attachment.Id)` for any modifiers sourced to the destroyed attachment.
6. **After-fold + envelope** (Model 02 §Updated Pipeline Shape). After the handler returns, `ITriggerPipeline.RunAfter` runs reactions (which dispatch via `engine.Dispatch` — separate envelopes). The original envelope publishes with the `CommandResult`.

## Composition

| Model | Role |
|---|---|
| [`models/01-async-dispatch.md`](models/01-async-dispatch.md) | Layer hierarchy (Effect / GameAction / CompositeAction); two dispatch paths; decisions-as-commands; `IEffectScope` |
| [`models/02-replacement-prevention.md`](models/02-replacement-prevention.md) | Three before-fold outcomes; `ITriggerPipeline` ownership; `GameRules.FilterTriggers` availability; replacement-of-replacement via fold |
| [`models/03-ongoing-effects.md`](models/03-ongoing-effects.md) | Attachment-entity model; common patterns (duration, condition, source-linked, global); turn hooks; Cleanup |
| [`models/04-effect-flow.md`](models/04-effect-flow.md) | Mid-execution lifecycle: service surfaces (`IEffectScope`, `IHandlerContext`, `ICommandAwaiter`); type-only decision routing; mutation discipline; uniform gateway boundary |

## Invariants

Cross-Model rules that hold across the four Models and shape how downstream features must use effects.

- **Layer separation.** Only Effects sequence `GameAction`s; only handlers invoke mutators; effects never mutate. A `GameAction` handler may use `CompositeAction`s internally but never dispatches other `GameAction`s — that's the effect's responsibility.
- **Dispatch semantics are not interchangeable.** `scope.Dispatch` is immediate/inline (effect awaits result); `engine.Dispatch` is deferred/queued (own envelope). Effects always use `scope`; trigger reactions and evaluator follow-ups always use `engine`.
- **Mutation discipline.** Mutators (`Mutator<TState, TPayload>.Change`) are the only producer of new slice state. Only handlers invoke mutators. Effects cannot mutate. Mutator payloads are dedicated types, not commands.
- **Subscription ownership.** Attachment entities (and any entity that registers triggers) own their subscriptions via `InstanceId`. Cleanup-on-destruction unregisters every subscription owned by that instance — no manual cleanup required.
- **Fold determinism.** Replacements carry explicit priority. The before-fold processes them in rank order; ties break by stable, replay-safe rule (e.g., source entity ID). No registration-time-based ordering, no nondeterminism.
- **Type-only decision routing.** `ICommandAwaiter.Wait<TCommand>()` registers a type predicate. There is no `DecisionId`, no `CommandId` carried by the awaiter or the snapshot. The mapper is decision-unaware.
- **At most one open awaiter per type-hierarchy.** Sequential nesting only. Single-open is a runtime assert.
- **Uniform boundary.** All player input goes `intent → IntentMapper.Map(template, choice) → GameCommand → engine.Dispatch`. The mapper switches on template subtype only. Engine pre-pipeline awaiter check intercepts matched commands before the normal pipeline.
- **Cancellation never automatically cancels the orchestrating effect.** If a dispatched action is cancelled by the before-fold, the effect's `await scope.Dispatch(...)` resolves with a cancelled `CommandResult` — the effect inspects and decides whether to continue.

## Boundaries

**Owns:** the layer hierarchy contract (Effect / GameAction / CompositeAction); two-dispatch-paths semantics; decisions-as-commands shape; `IEffectScope`, `IHandlerContext`, `ICommandAwaiter` interfaces; ongoing-effect attachment-entity pattern + Cleanup hook contract; before-fold three-outcomes (modify / cancel / replace); replacement-of-replacement via fold; `Effect.Validate` pre-execution gate.

**Does not own:**
- `GameCommand` type hierarchy (the type system) → `triggers/`
- Trigger registration, fold execution mechanics → `triggers/`
- `GameRules.FilterTriggers` (availability policy) → `engine/`
- Engine `OnIncomingCommand` routing, queue, drain → `engine/`
- `IAvailableActions`, snapshot shape → `engine/`
- Pipeline envelope, history entries → `actions/`
- `IIntentMapper.Map` implementation → `actions/`
- `IEntityRegistry` and `IModifierFactory` implementations → `entities/`
- Animation handoff, presentation-side async → `presentation/`

## Dependencies

- **Upstream:** `triggers/` (type hierarchy, `OnBefore`/`OnAfter`, `ITriggerPipeline`, fold mechanic), `actions/` (pipeline, envelope, `IIntentMapper`), `engine/` (`GameRules`, `IAvailableActions`, evaluation loop, `Store`, `Mutator`), `entities/` (entity registry, modifier factory, attachment-entity model).
- **Downstream:** game-side rules code (defines `Effect` subclasses, `AbilityDefinition`s, ongoing-effect attachments), `presentation/` (consumes `DecisionOpenedSignal` / `DecisionResolvedSignal` to render decision UI).

## Open

See `ledgers/OPEN-ITEMS.md`:

- **Effect authoring & registration** — the "recipe" a definition holds for instantiating an `Effect` (data, code, graph, or hybrid). Tracked as a future authoring-feature Model.
- **Effect per-instance state** — where ongoing-effect counters live (Effect object, `EntityVariableState`, dedicated slice). Left to ongoing-effects/authoring follow-up.
- **`CommandResult` cancellation shape** — flag on base record vs typed `CancelledResult` subtype. Cosmetic; pin during command-pipeline cleanup.
- **`IHandlerContext` vs constructor-injected services** — context aggregation vs explicit per-handler service declaration. Revisit when a second game stresses the surface.
- **`ICommandAwaiter` shape — full review** — TCS-backed type-predicate not stress-tested against error/timeout, multi-actor decisions, suspension across engine ticks. Needs focused review.
- **FSM-shaped game state** — type-only routing's eventual successor; richer self-describing game-state phases (`PickATargetState`, `MainPhaseState`, etc.).
- **Suspension across engine ticks (save/load)** — what state must persist if a game saves mid-decision. Depends on game-lifecycle design.
- **Entity destruction lifecycle** (`oq-entity-destruction`) — what "destroyed" means, when Cleanup fires, what happens to in-flight dispatches referencing destroyed entities.
- **Multiplayer visibility on `DecisionOpenedSignal`** — must other clients see the signal before the deciding player responds, or is full envelope on resolution sufficient?
- **Mulligan lifecycle** — pipeline action vs separate session setup phase. Tied to game lifecycle (task 12).
- **Effect as first-class type vs convention** — partially settled in Model 04 (first-class), but recipe-on-definition shape still open.
- **Nested decisions** — concurrent overlapping awaiters (Model 04 settles single-open per type-hierarchy as runtime assert; broader nesting model still open).
- **`04-effect-flow.md` density** — flagged for possible split into 2 Models as a follow-up; not in migration scope.
