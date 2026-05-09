---
type: model
status: settled
references: [features/triggers/models/01-trigger-fold.md, features/actions/models/01-command-pipeline.md, features/engine/models/01-evaluation-loop.md, features/effects/models/04-effect-flow.md]
tags: [#effects, #async, #suspension]
---

# Async Dispatch — model

## What it is

How the engine handles effects that require player input — targeting, choices, mulligans — and the layer hierarchy that makes async orchestration tractable. Three distinct layers handle game logic, each with exactly one job; two dispatch paths (`engine.Dispatch` vs `scope.Dispatch`) carry different semantics and are not interchangeable; decisions are normal `GameAction`s that open UI, await player response, and return the chosen value in their `CommandResult`.

```
Effect
  └── orchestrates multiple GameActions — the only layer that can sequence commands
        └── GameAction applies StateMutations — atomic, single envelope, synchronous handler
              └── CompositeAction groups StateMutations — reusable unit inside a handler
```

## Contracts

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

`scope.Dispatch` is not deferred. The call executes the action's full lifecycle (before fold → handler → after fold → envelope published) synchronously in the calling task. The effect `await`s it and cannot advance to the next line until the action finishes — including if that action is waiting for player input.

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

### Decisions as commands

A decision (choose a target, pick an option) is not a special pipeline concept. It is a `GameAction` that:

- Opens a decision visible to the relevant actor.
- Waits for the player to respond.
- Returns the chosen value in its `CommandResult`.
- Has its own envelope recording that the decision was opened and resolved.

```csharp
public sealed record RequestTargetAction : GameAction
{
    public required TargetFilter Filter { get; init; }
}

public sealed record RequestTargetResult : CommandResult
{
    public required Reference Target { get; init; }
}
```

The effect receives the `CommandResult` and passes the resolved target directly into the next dispatch. No partial envelopes, no suspension inside a handler, no special engine state for in-progress decisions.

### What the client sees

Because each `GameAction` publishes its own complete envelope before the next begins:

- The client always sees fully committed state — no partial or speculative state.
- A decision command's envelope can carry a `DecisionOpenedSignal` that tells the client to show the appropriate UI (target highlights, choice overlay, etc.).
- While the decision is open, the client has all prior envelopes and can render the current state accurately.
- When the decision resolves, the result is in the `CommandResult` and recorded in history.

No partial envelopes needed. No mid-execution state visible to the client.

### Envelope shape for decisions

A decision command's envelope is lightweight:

```
HistoryEnvelope
  cause: { actor, chain ID, origin intent }
  entries:
    - DecisionOpenedSignal { type: Target, filter: EnemyFilter }
    - DecisionResolvedSignal { chosen: instanceId X }
  result: RequestTargetResult { Target: X }
```

`DecisionOpenedSignal` and `DecisionResolvedSignal` are `GameSignal`s — they pass through the trigger fold (other cards may react to a player being forced to choose), but have no handler.

### `IEffectScope`

The effect interacts with the pipeline through an `IEffectScope`. The full interface is defined in [`04-effect-flow.md` §IEffectScope](features/effects/models/04-effect-flow.md); this Model uses one member:

`scope.Dispatch(action)` runs the action **immediately and inline** — before fold → handler → after fold → envelope published — and returns the `CommandResult`. The effect is suspended at the `await` until the action completes. The engine queue is not involved; the action never enters it.

`engine.Dispatch` and `scope.Dispatch` are not the same thing. An effect always uses the scope. Trigger reactions and evaluator follow-ups always use the engine. Mixing them would break the execution model.

## Invariants

- **Only Effects sequence commands.** A `GameAction` handler never dispatches other `GameAction`s. `CompositeAction` is the in-handler reuse mechanism.
- **`scope.Dispatch` is synchronous-inline; `engine.Dispatch` is queued.** They are not interchangeable — effects always use `scope`; trigger reactions and evaluator follow-ups always use `engine`.
- **Effects cannot mutate state directly.** The only state-changing channel is dispatching a `GameAction` whose handler writes via mutators.
- **Decisions are normal `GameAction`s.** No partial envelopes, no special "in-progress decision" engine state. The decision command produces one envelope that records both `DecisionOpenedSignal` and `DecisionResolvedSignal` plus the result.
- **The engine is paused for the duration of an effect chain.** Awaiting a decision blocks the engine FIFO until the effect returns.

## Decisions

*n/a — no elevated ADR.* (The async-layer hierarchy applies to a single feature; folded as Q1 below.)

## Examples

### Targeted spell

```
Effect: PlayFireballEffect
  target ← Dispatch(RequestTargetAction { Filter: EnemyFilter })
            → envelope: decision opened, player chose target X
  Dispatch(DealDamageAction { Target: target.Target, Amount: 6 })
            → envelope: 6 damage applied to X
  Dispatch(MoveToDiscardAction { Source: fireball })
            → envelope: fireball moved to discard
```

Three envelopes. All clean. No command suspends. The effect awaits each one in sequence.

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

Cost is paid before the choice is shown. The envelope for paying cost is published and visible to the client immediately. The client sees the card leave hand before the choice overlay appears.

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

- **Effect registration** — where is an effect defined? On the card definition as a typed class? As a reference from the handler? How does the engine know which effect to run for a given `GameAction`? Tracked in [`04-effect-flow.md`](features/effects/models/04-effect-flow.md) and the future authoring-feature Model.
- **Effect cancellation** — if the `OnBefore<T>` fold cancels a `GameAction` mid-effect, does the effect stop? Settled in [`04-effect-flow.md` §Cancellation](features/effects/models/04-effect-flow.md): the effect's `await scope.Dispatch(...)` resolves with a cancelled `CommandResult` — the effect inspects and decides whether to continue.
- **Nested decisions** — can an effect dispatch a `RequestTargetAction` while another decision is already open? `04-effect-flow.md` settles single-open-per-type-hierarchy as a runtime assert; broader nesting model still open.
- **Multiplayer visibility** — in a multiplayer game, does the `DecisionOpenedSignal` need to reach other clients before the deciding player has responded? Or is it sufficient for the full envelope to arrive after resolution? See `OPEN-ITEMS.md`.
- **Mulligan lifecycle** — does mulligan go through the command pipeline at all, or is it a separate session setup phase? (Deferred to game lifecycle design, checkpoint task 12.)
- **Effect as a first-class type** — settled in [`04-effect-flow.md`](features/effects/models/04-effect-flow.md): `Effect` is a first-class abstract type. Recipe-on-definition shape still open.

---

*Below: provenance — how the layer hierarchy was chosen. Single-feature scope (no project-wide ADR applies; folded into Model provenance per §4.5 on 2026-05-08). Original ADR-0001 landing 2026-04-29.*

## Options considered

### Q1 — Async layer hierarchy

Async behavior in a card game spans several distinct kinds: animation handoff (presentation), decision wait (player input), simulation pause (rule), and resolver yield (engine). Treating all of them with one `Task` returned from a handler smears responsibilities.

- **A — Single async layer (`Task<Result>` everywhere).** Rejected — conflates animation, decision, and simulation pauses.
- **B — No async.** Rejected — real games need decisions and animation handoff.
- **C — Layered hierarchy.** Presentation-async (animation), decision-async (player input via `IAvailableActions` decision-scoped snapshot), engine-async (resolver yield). Each layer has its own continuation/handle. **Chosen.**

## Rationale

Layered async means each kind of pause has its own surface: presentation owns animation handoff, decisions are `GameAction`s with their own envelope, the engine yields between drain steps. Effects compose across these layers via `IEffectScope`, never by collapsing them into a single returned `Task`. This keeps testability per layer and makes the dispatch contract uniform regardless of which layer is currently waiting.

## Consequences

- **Enables** clean handoffs and per-layer testability; deterministic envelope publishing per dispatched action; client visibility of fully-committed state at all times.
- **Constrains** handlers and effects to declare which layer they're awaiting; only effects can sequence commands; effect chains pause the engine FIFO for their full duration (acceptable trade-off, game-specific tuning).
