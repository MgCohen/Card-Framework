---
type: feature
status: settled
references: [features/triggers/models/01-trigger-fold.md, features/actions/models/01-command-pipeline.md, features/engine/models/02-gamerules-contract.md, features/entities/models/02-runtime-surface.md]
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
       Resolver.Wait<TCmd>()                   envelope; framework FIFO)
              │
              ▼
       Player intent → IntentMapper →
       SelectTargetCommand → engine.Dispatch
       → awaiter.TryResolve matches → handler resumes
```

Effects are not authored as `Task<Result>` returned from a handler — that would conflate animation, decision, and simulation pauses. Each layer (presentation animation, decision player input, engine resolver yield) owns its own continuation; effects compose across layers via `IEffectScope`.

## Vocabulary

**Feature-owned terms only.** Cross-feature terms used by this feature — `GameCommand`, `GameAction`, `CompositeAction`, `GameSignal`, `Trigger<T>`, `OnBefore`/`OnAfter`, `TriggerMatch`, `TriggerFoldResult`, `ITriggerSystem`, `ITriggerPipeline`, `TriggerPhase` (defined in `triggers/`); `GameRulesRuntime`, `Evaluate`, `IAvailableActions`, `AvailableActionsSnapshot`, `Store`, `Mutator<TState, TPayload>`, `IStateScope`, `IStoreScope` (defined in `engine/`); `IntentMapper`, `PlayerIntent`, `IIntentMapper`, `CommandResult`, `HistoryEnvelope` (defined in `actions/`); `IModifierFactory`, `ActiveModifier`, `ModifierId`, `ModifierSource`, `IEntityVariableStorage`, `InstanceWithSlices<TDef>`, `EntityState` (defined in `entities/`); `Reference`, `Ref<T>`, `Card`, `Zone` — live in their owning features or `Product/ledgers/GLOSSARY.md`; link to their canonical home, don't restate.

| Term | Meaning |
|---|---|
| `Effect` | First-class abstract type. Per-instance orchestrator. `Validate(scope) → bool` + `Execute(scope) → Task`. Internals opaque to the framework. |
| `IEffectScope` | Effect's runtime surface: `State` (read-only), `Store` (catalog reads), `Modifiers`, `Dispatch`, `Cancel`, `Replace`. No mutation channel. |
| `IHandlerContext` | Handler's runtime surface: `Store` (read + execute mutators + catalog), `Actions` (snapshot writes), `Resolver` (decision awaits), `Modifiers`. |
| `ICommandAwaiter` | Decision-handler's await/resume mechanic. `Wait<TCommand>(ct)` registers a `cmd is TCommand` predicate; `TryResolve(cmd)` matches on every dispatch. |
| Layer hierarchy | Effect → GameAction → CompositeAction. Only Effect can sequence; only handlers invoke mutators; effects never mutate. |
| Two dispatch paths | `engine.Dispatch(action)` (deferred, framework-FIFO, own envelope) vs `scope.Dispatch(action)` (immediate, inline, effect awaits). Not interchangeable. |
| Decision-as-command | A decision (target, choice, etc.) is a `GameAction` that opens UI, awaits player response, returns chosen value in `CommandResult`. Type-only routing — no `DecisionId`. |
| Replacement | Before-fold outcome where a trigger calls `scope.Replace(next)` — original action's envelope closes with `Replaced`; `next` dispatches as fresh command through its own full fold. |
| Prevention | Modification with a zero floor (e.g. shield reduces damage to 0). Stateful prevention is an ongoing-effect attachment that owns its own state. |
| Attachment entity | Ongoing effect modeled as an entity (definition, `Ref<T>`, variables, subscriptions). Created via `CreateAndAttachAction`, destroyed via `DestroyEntityAction`; framework Cleanup hook unregisters subscriptions. |
| Mutator payload | Dedicated record type (e.g. `MoveCardPayload`), not the command. One payload may fan out to N registered mutators; `Store.Execute(payload)` runs every one and commits atomically. |

## Architecture

The feature has four Models. Each is the canonical home for one load-bearing C# type of the effects layer:

1. **`Effect`** ([`01-effect.md`](features/effects/models/01-effect.md)) — the orchestrator type; layer hierarchy (Effect / GameAction / CompositeAction); two dispatch paths (`engine.Dispatch` vs `scope.Dispatch`).
2. **`IEffectScope`** ([`02-effect-scope.md`](features/effects/models/02-effect-scope.md)) — the effect's runtime surface: read-only `State`, catalog `Store`, `Modifiers`, `Dispatch`, `Cancel`, `Replace`. No mutation channel.
3. **`IHandlerContext`** ([`03-handler-context.md`](features/effects/models/03-handler-context.md)) — the handler's runtime surface: `Store` (read + execute mutators + catalog), `Actions` (snapshot writes), `Resolver` (decision awaits), `Modifiers`.
4. **`ICommandAwaiter`** ([`04-command-awaiter.md`](features/effects/models/04-command-awaiter.md)) — the suspension/resume mechanic for decisions; type-only routing; the uniform boundary; engine pre-pipeline awaiter check.

The execution arc: an `Effect` (1) calls `scope.Dispatch(action)` on its `IEffectScope` (2); the dispatched action's handler runs through the trigger fold (per [`triggers/01-trigger-fold.md`](features/triggers/models/01-trigger-fold.md)) — replacement / prevention semantics live there as the three before-fold outcomes. The handler executes via its `IHandlerContext` (3), awaiting player input through `ICommandAwaiter` (4) for decisions. Ongoing effects (auras, stateful prevention, durations) are realized as **attachment entities** — see [`entities/02-runtime-surface.md` §Attachment-entity usage](features/entities/models/02-runtime-surface.md). Pipeline shape lives in [`actions/01-command-pipeline.md` §Pipeline mechanics](features/actions/models/01-command-pipeline.md).

### Three layers, capabilities

| Layer | Read state | Mutate state | Dispatch action | Await player |
|---|---|---|---|---|
| **`Effect`** | Yes (via `scope.State`) | **No** | Yes (`scope.Dispatch`) | No (only by dispatching a decision action) |
| **`ICommandHandler`** | Yes (via `ctx.Store`) | Yes — by invoking mutators | No | Yes (decision handlers only) |
| **`Mutator<TState, TPayload>`** | Yes (current state in; sibling slices via `IStateScope`) | **Yes — only producer of new state** | No | No |

Three principles fall out of this table:

- **Mutation flows through mutators, never directly.** A mutator's `Change(state, payload, scope)` returning a new `TState` is the only path that produces state. Handlers don't write state inline; effects can't write state at all.
- **Only handlers invoke mutators.** Mutators are not auto-fired off command dispatch. The handler for a command decides which mutators to run and with what payload.
- **Effects orchestrate, never mutate.** An effect's job is to sequence dispatches and thread their results. State change happens entirely inside the dispatched actions' handlers.

### Cancellation discipline

Two cancellation cases exist; both surface through the same channel.

**Action cancelled by the before-fold.** A registered `OnBefore<T>` returns `FoldResult.Cancel(reason)`. The action's handler never runs. The effect's `await scope.Dispatch(...)` resolves with a `CommandResult` carrying the cancellation. The effect inspects the result and decides whether to continue:

```csharp
var damage = await scope.Dispatch(new DealDamageAction { Target = t, Amount = 6 });
if (damage.IsCancelled) return;        // shield absorbed; effect ends gracefully
await scope.Dispatch(new MoveToDiscardAction { Source = source });
```

**Effect aborts itself.** The effect calls `scope.Cancel()` when it knows it cannot continue (e.g. the targeted entity has been removed by a previous dispatch). The effect's `Execute` task completes; no further dispatches run.

The framework invariant: **cancellation of one `GameAction` never automatically cancels the orchestrating effect.** The effect is the only place that decides whether the chain should continue.

## Composition

| Model | Role |
|---|---|
| [`01-effect.md`](features/effects/models/01-effect.md) | `Effect` type + layer hierarchy + two dispatch paths |
| [`02-effect-scope.md`](features/effects/models/02-effect-scope.md) | `IEffectScope` (effect's runtime surface) |
| [`03-handler-context.md`](features/effects/models/03-handler-context.md) | `IHandlerContext` (handler's runtime surface) |
| [`04-command-awaiter.md`](features/effects/models/04-command-awaiter.md) | `ICommandAwaiter` + type-only decision routing + uniform boundary |

Cross-feature content this feature consumes but does not own: before-fold outcomes (modify / cancel / replace) and replacement-of-replacement live in [`triggers/01-trigger-fold.md`](features/triggers/models/01-trigger-fold.md); ongoing-effect attachment-entity pattern lives in [`entities/02-runtime-surface.md`](features/entities/models/02-runtime-surface.md).

## Invariants

Cross-Model rules that hold across the four Models and shape how downstream features must use effects.

- **Layer separation.** Only Effects sequence `GameAction`s; only handlers invoke mutators; effects never mutate. A `GameAction` handler may use `CompositeAction`s internally but never dispatches other `GameAction`s — that's the effect's responsibility.
- **Dispatch semantics are not interchangeable.** `scope.Dispatch` is immediate/inline (effect awaits result); `engine.Dispatch` is deferred/FIFO (own envelope). Effects always use `scope`; trigger reactions and evaluator follow-ups always use `engine`.
- **Mutation discipline.** Mutators (`Mutator<TState, TPayload>.Change`) are the only producer of new slice state. Only handlers invoke mutators. Effects cannot mutate. Mutator payloads are dedicated types, not commands.
- **Subscription ownership.** Attachment entities (and any entity that registers triggers) own their subscriptions via `Ref<T>`. Cleanup-on-destruction unregisters every subscription owned by that instance — no manual cleanup required.
- **Fold determinism.** Replacements carry explicit priority. The before-fold processes them in rank order; ties break by stable, replay-safe rule (e.g., source entity ref). No registration-time-based ordering, no nondeterminism.
- **Type-only decision routing.** `ICommandAwaiter.Wait<TCommand>()` registers a type predicate. There is no `DecisionId`, no `CommandId` carried by the awaiter or the snapshot. The mapper is decision-unaware.
- **At most one open awaiter per type-hierarchy.** Sequential nesting only. Single-open is a runtime assert.
- **Uniform boundary.** All player input goes `intent → IntentMapper.Map(template, choice) → GameCommand → engine.Dispatch`. The mapper switches on template subtype only. Engine pre-pipeline awaiter check intercepts matched commands before the normal pipeline.
- **Cancellation never automatically cancels the orchestrating effect.** If a dispatched action is cancelled by the before-fold, the effect's `await scope.Dispatch(...)` resolves with a cancelled `CommandResult` — the effect inspects and decides whether to continue.

## Boundaries

**Owns:** the `Effect` abstract type; layer hierarchy contract (Effect / GameAction / CompositeAction); two-dispatch-paths semantics; decisions-as-commands shape; `IEffectScope`, `IHandlerContext`, `ICommandAwaiter` interfaces; `Effect.Validate` pre-execution gate; mutation discipline (effects-can't-mutate); cancellation discipline (cancel never auto-cancels orchestrating effect).

**Does not own:**
- `GameCommand` type hierarchy (the type system) → `triggers/`
- Trigger registration, fold execution mechanics, before-fold outcomes, replacement priority → `triggers/`
- Attachment-entity usage pattern (ongoing effects realized as entities) → `entities/`
- `GameRulesRuntime.FilterTriggers` (availability policy) → `engine/`
- Engine framework FIFO routing, drain → `engine/`
- `IAvailableActions`, snapshot shape → `engine/`
- Pipeline envelope, history entries → `actions/`
- `IIntentMapper.Map` implementation → `actions/`
- `IModifierFactory` implementation → `entities/`
- `store.Catalog` mechanism → `engine/`
- Animation handoff, presentation-side async → `presentation/`

## Dependencies

*Mirrors the front-matter `references:` array in human-readable form (per §3).*

- **Upstream:** `triggers/` (type hierarchy, `OnBefore`/`OnAfter`, `ITriggerPipeline`, fold mechanic), `actions/` (pipeline, envelope, `IIntentMapper`), `engine/` (`GameRulesRuntime`, `IAvailableActions`, evaluation loop, `Store`, `Mutator`), `entities/` (entity bridge, modifier factory, attachment-entity pattern via `InstanceWithSlices<TDef>`).
- **Downstream:** game-side rules code (defines `Effect` subclasses, `AbilityDefinition`s, ongoing-effect attachments), `presentation/` (consumes `DecisionOpenedSignal` / `DecisionResolvedSignal` to render decision UI).

## Open

See `ledgers/OPEN-ITEMS.md`:

- **Effect authoring & registration** — the "recipe" a definition holds for instantiating an `Effect` (data, code, graph, or hybrid). Tracked as a future authoring-feature Model.
- **Effect per-instance state** — where ongoing-effect counters live (Effect object, `EntityState`, dedicated slice). Left to ongoing-effects/authoring follow-up.
- **`CommandResult` cancellation shape** — flag on base record vs typed `CancelledResult` subtype. Cosmetic; pin during command-pipeline cleanup.
- **`IHandlerContext` vs constructor-injected services** — context aggregation vs explicit per-handler service declaration. Revisit when a second game stresses the surface.
- **`ICommandAwaiter` shape — full review** — TCS-backed type-predicate not stress-tested against error/timeout, multi-actor decisions, suspension across engine ticks. Needs focused review.
- **FSM-shaped game state** — type-only routing's eventual successor; richer self-describing game-state phases (`PickATargetState`, `MainPhaseState`, etc.).
- **Suspension across engine ticks (save/load)** — what state must persist if a game saves mid-decision. Depends on game-lifecycle design.
- **Entity destruction lifecycle** (`oq-entity-destruction`) — what "destroyed" means, when Cleanup fires, what happens to in-flight dispatches referencing destroyed entities.
- **Multiplayer visibility on `DecisionOpenedSignal`** — must other clients see the signal before the deciding player responds, or is full envelope on resolution sufficient?
- **Mulligan lifecycle** — pipeline action vs separate session setup phase. Tied to game lifecycle.
- **Effect as first-class type vs convention** — partially settled in Model 04 (first-class), but recipe-on-definition shape still open.
- **Nested decisions** — concurrent overlapping awaiters (Model 04 settles single-open per type-hierarchy as runtime assert; broader nesting model still open).
- **`oq-effect-pipeline`** — effect-level orchestration surface (when each effect starts, whether effects nest / batch / stack). Per ADR-0006 this is the customizable layer where game styles diverge; design ongoing in `spikes/effect-pipeline.md`.
