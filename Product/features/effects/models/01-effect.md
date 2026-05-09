---
type: model
status: settled
references: [features/triggers/models/01-trigger-fold.md, features/actions/models/01-command-pipeline.md, features/effects/models/02-effect-scope.md, features/effects/models/04-command-awaiter.md, features/engine/models/01-evaluation-loop.md]
tags: [#effects, #model]
---

# Effect — model

## What it is

`Effect` is the orchestrator: a per-instance, first-class type that sequences `GameAction`s by calling `scope.Dispatch(action)`, threads results across calls, and composes across layers (animation, decision, engine yield) without collapsing them into a single `Task<Result>`.

```
Effect
  └── orchestrates multiple GameActions — the only layer that can sequence commands
        └── GameAction applies StateMutations — atomic, single envelope, synchronous handler
              └── CompositeAction groups StateMutations — reusable unit inside a handler
```

## Contracts

### The `Effect` type

```csharp
public abstract class Effect
{
    public virtual bool Validate(IEffectScope scope) => true;
    public abstract Task Execute(IEffectScope scope);
}
```

- **First-class type.** Not a convention or a delegate.
- **Per-instance.** Each card *instance* owns its own `Effect` object(s). The card *definition* holds the recipe — how the effect is constructed for new instances. The recipe shape (data, code, graph, or hybrid) is out of scope here; tracked as a follow-up.
- **Internals are opaque to the framework.** The engine only needs `Execute(scope)`; how an effect is authored, parameterized, or composed is the authoring layer's concern.
- **Many per definition.** A card can carry several effects (battlecry + deathrattle + ongoing aura). Each is a separate `Effect` with its own trigger condition. Subscription lifecycle is the trigger feature's concern.
- **`Effect.Validate` is a pre-execution gate.** Returning `false` prevents `Execute` from running. The default always passes — simple effects don't override it.

Effects can have per-instance state (counters, accumulators) but where that state lives — on the `Effect` object, on the entity's `EntityState`, or on a dedicated slice — is deliberately undesigned at this level. This Model only pins the type's execution surface.

### The layer hierarchy

| Layer | Job | Can sequence commands? | Async? | Envelope |
|---|---|---|---|---|
| **Effect** | Orchestrate commands, thread results | Yes — only layer that can | Yes | None of its own |
| **`GameAction`** | Apply state changes, return a result | No | No — synchronous | One per action |
| **`CompositeAction`** | Reusable state-change group inside a handler | No | No | Folds into parent |

Only effects sequence commands. A `GameAction` handler may use `CompositeAction`s internally, but it never dispatches other `GameAction`s — that is the effect's responsibility.

### Two dispatch paths

There are two ways a `GameAction` can be executed, and they are not interchangeable:

| Path | How | When it runs | Envelope | Used by |
|---|---|---|---|---|
| **`engine.Dispatch(action)`** | Queued, processed after current drain step | Later | Own envelope, independent | `GameRules.Evaluate`, trigger reactions, player input |
| **`scope.Dispatch(action)`** | Executed **immediately**, inline | Right now | Own envelope, published before next line runs | Effects |

`scope.Dispatch` is not deferred. The call executes the action's full lifecycle (before-fold → handler → after-fold → envelope published) synchronously in the calling task. The effect `await`s it and cannot advance to the next line until the action finishes — including if that action is waiting for player input (per [`04-command-awaiter.md`](features/effects/models/04-command-awaiter.md)).

```csharp
// Effect — each line executes and completes before the next begins
public async Task Execute(IEffectScope scope)
{
    var targetResult = await scope.Dispatch(new RequestTargetAction { Filter = EnemyFilter });
    // execution cannot reach here until the player has chosen a target
    await scope.Dispatch(new DealDamageAction { Target = targetResult.Target, Amount = 6 });
    // execution cannot reach here until damage is applied
    await scope.Dispatch(new MoveToDiscardAction { Source = source });
}
```

The engine is effectively paused on `await pipeline.Execute(ExecuteCardAction)` for the entire duration of the effect chain. It will not dequeue the next engine-queued action until the effect — including any player input it waits for — has fully completed. Whether this is acceptable is game-specific.

### `IEffectScope`

The effect interacts with the pipeline through an `IEffectScope`. The full interface is defined in [`02-effect-scope.md`](features/effects/models/02-effect-scope.md). Effect authoring uses three of its members:

- `scope.State` — read slices to choose what to dispatch next.
- `scope.Dispatch(action)` — execute an action immediately and inline; awaits its `CommandResult`.
- `scope.Cancel(reason)` — explicit early-out when the effect can no longer continue.

`engine.Dispatch` and `scope.Dispatch` are not the same thing. An effect always uses the scope. Trigger reactions and evaluator follow-ups always use the engine. Mixing them would break the execution model.

## Invariants

- **Only Effects sequence commands.** A `GameAction` handler never dispatches other `GameAction`s. `CompositeAction` is the in-handler reuse mechanism.
- **`scope.Dispatch` is synchronous-inline; `engine.Dispatch` is queued.** They are not interchangeable — effects always use `scope`; trigger reactions and evaluator follow-ups always use `engine`.
- **Effects cannot mutate state directly.** The only state-changing channel is dispatching a `GameAction` whose handler writes via mutators.
- **`Effect` is a first-class type.** Authored as an abstract class, not a convention or delegate.
- **The engine is paused for the duration of an effect chain.** Awaiting a decision blocks the engine FIFO until the effect returns.

## Decisions

*n/a — no elevated ADR.* (The async-layer hierarchy applies to a single feature; folded as Q1 below.)

## Examples

### Targeted spell (Fireball)

```
Effect: PlayFireballEffect
  target ← Dispatch(RequestTargetAction { Filter: EnemyFilter })
            → envelope: decision opened, player chose target X
  Dispatch(DealDamageAction { Target: target.Target, Amount: 6 })
            → envelope: 6 damage applied to X
  Dispatch(MoveToDiscardAction { Source: fireball })
            → envelope: fireball moved to discard
```

Three envelopes. All clean. No command suspends. The effect awaits each one in sequence. Decision mechanic (how `RequestTargetAction.Handle` awaits the player) lives in [`04-command-awaiter.md`](features/effects/models/04-command-awaiter.md).

### Choose one (Discover)

```
Effect: PlayDiscoverEffect
  Dispatch(PayCostAction { Actor: player, Amount: manaCost })
            → envelope: cost paid, card moved from hand
  choice ← Dispatch(RequestChoiceAction { Options: [A, B, C] })
            → envelope: choice presented, player chose B
  Dispatch(ApplyChoiceEffectAction { Choice: choice.Selected })
            → envelope: chosen effect applied
```

Cost is paid before the choice is shown. The envelope for paying cost is published and visible to the client immediately.

### Mulligan

Mulligan is a pre-game session lifecycle event, not a mid-command decision. It does not go through the normal command pipeline. It is a session setup phase where each actor is presented a `MulliganDecision` directly, responds, and the session advances once all actors have confirmed.

Whether mulligan is modelled as a specialized `GameAction` dispatched during session setup, or as a separate session lifecycle step, is a deferred design question tied to game lifecycle (checkpoint task 12).

### Mid-chain triggered target

```
Effect: PlaySpellEffect
  Dispatch(ApplySpellPartOneAction { ... })
            → envelope: first effect applied
            → trigger fires: OnAfterApplySpellPartOne → triggered ability needs a target

Triggered ability is itself an effect:
  target ← Dispatch(RequestTargetAction { Filter: AnyFilter })
            → envelope: triggered ability's decision
  Dispatch(ApplyTriggeredAbilityAction { Target: target.Target })
            → envelope: triggered ability resolved
```

Each envelope is independent. The triggered ability dispatches through the normal channel — `engine.Dispatch` validates and enqueues onto the framework FIFO. Effect-level orchestration (whether the triggered effect runs immediately, batches, or stacks) is game-driven via state plus `runtime.Evaluate` per [ADR-0006](Product/decisions/0006-two-pipeline-architecture.md). No special case at the dispatch channel.

## Open follow-ups

- **Effect authoring & registration.** The "recipe" a definition holds for instantiating an `Effect` (data, code, graph, or hybrid) is deliberately undesigned. Tracked as a future authoring-feature Model.
- **Effect per-instance state.** Where ongoing-effect counters live — on the `Effect` object, on the entity's `EntityState`, or on a dedicated slice. Left to ongoing-effects/authoring follow-up. [`entities/02-runtime-surface.md` §Attachment-entity usage](features/entities/models/02-runtime-surface.md) settles the attachment-entity shape; this Model only reaffirms that effects do not get a hidden mutation channel.
- **Mulligan lifecycle** — pipeline action vs separate session setup phase. Tied to game lifecycle.

---

*Below: provenance — how the layer hierarchy was chosen. Single-feature scope (no project-wide ADR applies; folded into Model provenance per §4.5 on 2026-05-08). Original ADR-0001 landing 2026-04-29. Effect type contract absorbed from `04-effect-flow.md` on 2026-05-09 during the load-bearing extraction pass.*

## Options considered

### Q1 — Async layer hierarchy

Async behavior in a card game spans several distinct kinds: animation handoff (presentation), decision wait (player input), simulation pause (rule), and resolver yield (engine). Treating all of them with one `Task` returned from a handler smears responsibilities.

- **A — Single async layer (`Task<Result>` everywhere).** Rejected — conflates animation, decision, and simulation pauses.
- **B — No async.** Rejected — real games need decisions and animation handoff.
- **C — Layered hierarchy.** Presentation-async (animation), decision-async (player input via the awaiter), engine-async (resolver yield). Each layer has its own continuation/handle. **Chosen.**

## Rationale

Layered async means each kind of pause has its own surface: presentation owns animation handoff, decisions are `GameAction`s with their own envelope (per [`04-command-awaiter.md`](features/effects/models/04-command-awaiter.md)), the engine yields between drain steps. Effects compose across these layers via `IEffectScope`, never by collapsing them into a single returned `Task`. This keeps testability per layer and makes the dispatch contract uniform regardless of which layer is currently waiting.

## Consequences

- **Enables** clean handoffs and per-layer testability; deterministic envelope publishing per dispatched action; client visibility of fully-committed state at all times.
- **Constrains** handlers and effects to declare which layer they're awaiting; only effects can sequence commands; effect chains pause the engine FIFO for their full duration (acceptable trade-off, game-specific tuning).
