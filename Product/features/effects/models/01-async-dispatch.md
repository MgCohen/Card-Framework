---
type: model
status: settled
references: [features/engine/models/01-evaluation-loop.md, features/triggers/models/01-trigger-fold.md, features/actions/models/01-command-pipeline.md]
tags: [#effects, #async, #suspension]
---

# Async Decisions And Suspension

## What it is

How the engine handles effects that require player input — targeting, choices, mulligans — and the layer hierarchy that makes async orchestration tractable. Three distinct layers participate, each with one job: **Effects** orchestrate `GameAction`s (the only layer that can sequence commands), **`GameAction` handlers** apply state changes (synchronous, return a `CommandResult`), and **`CompositeAction`s** group state mutations for reuse inside a handler. Decisions are not a special pipeline concept — they're `GameAction`s whose handlers await player response.

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

This hierarchy is the key constraint. Only effects sequence commands. A `GameAction` handler may use `CompositeAction`s internally, but it never dispatches other `GameAction`s — that is the effect's responsibility.

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
    public required InstanceId Target { get; init; }
}
```

The effect receives the `CommandResult` and passes the resolved target directly into the next dispatch. No partial envelopes, no suspension inside a handler, no special engine state for in-progress decisions. The mid-execution mechanics (how the handler awaits, how player intent resolves the await) are settled in [`04-effect-flow.md`](features/effects/models/04-effect-flow.md).

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

The effect interacts with the pipeline through an `IEffectScope`:

```csharp
public interface IEffectScope
{
    IEntityRegistry   Entities  { get; }    // entity handle lookup (read paths)
    IModifierFactory  Modifiers { get; }    // deterministic modifier-entry construction
    Task<TResult> Dispatch<TResult>(GameAction action) where TResult : CommandResult;
}
```

`IEffectScope.Dispatch` runs the action **immediately and inline** — before fold → handler → after fold → envelope published — and returns the `CommandResult`. The effect is suspended at the `await` until the action completes. The engine queue is not involved; the action never enters it.

`engine.Dispatch` and `scope.Dispatch` are not the same thing. An effect always uses the scope. Trigger reactions and evaluator follow-ups always use the engine. Mixing them would break the execution model. Full `IEffectScope` definition (including `Cancel` and `Replace` for trigger contexts, and `State` for read access) lives in [`04-effect-flow.md` §4.3](features/effects/models/04-effect-flow.md).

## Invariants

Model-local invariants. (See `effects.md ## Invariants` for feature-level rules.)

- **The engine queue holds only `engine.Dispatch`-ed actions.** `scope.Dispatch` runs inline; the action never enters the queue.
- **A single `await scope.Dispatch(...)` blocks the calling effect until the entire action lifecycle (before-fold → handler → after-fold → envelope) completes.** Including any decision the action requires.
- **Decision envelopes are independent.** Each dispatched action publishes its own complete envelope; no partial envelopes; no mid-execution state visible to the client.
- **`DecisionOpenedSignal` / `DecisionResolvedSignal` are `GameSignal`s.** They pass through trigger folds but have no handler.

## Examples

**Scenario A — Targeted Spell.**

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

**Scenario B — Choose One (Discover).**

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

**Scenario C — Mulligan.** Mulligan is a pre-game session lifecycle event, not a mid-command decision. It does not go through the normal command pipeline. It is a session setup phase where each actor is presented a `MulliganDecision` directly, responds, and the session advances once all actors have confirmed. Whether mulligan is modelled as a specialized `GameAction` dispatched during session setup, or as a separate session lifecycle step, is a deferred design question tied to game lifecycle.

**Scenario D — Mid-Chain Triggered Target.**

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

Each envelope is independent. The triggered ability dispatches through the normal channel. `GameRules.OnIncomingCommand` decides scheduling (RunNext, Enqueue, Stash). No special case needed.

## Open follow-ups

- **Effect registration** — where is an effect defined? On the card definition as a typed class? As a reference from the handler? How does the engine know which effect to run for a given `GameAction`?
- **Effect cancellation** — if the `OnBefore<T>` fold cancels a `GameAction` mid-effect, does the effect stop? Does it receive a cancelled `CommandResult` and decide itself? (Settled in [`04-effect-flow.md` §Cancellation](features/effects/models/04-effect-flow.md): the effect inspects the result and decides.)
- **Nested decisions** — can an effect dispatch a `RequestTargetAction` while another decision is already open (e.g. Scenario D with concurrent triggers)? How does the engine queue shape handle this? (Partial: Model 04 settles single-open per type-hierarchy as runtime assert.)
- **Multiplayer visibility** — in a multiplayer game, does the `DecisionOpenedSignal` need to reach other clients before the deciding player has responded? Or is it sufficient for the full envelope to arrive after resolution?
- **Mulligan lifecycle** — pipeline action vs separate session setup phase. Deferred to game lifecycle.
- **Effect as a first-class type** — should `Effect` be a named type in the framework, or is it just a convention? (Settled first-class in [`04-effect-flow.md`](features/effects/models/04-effect-flow.md); recipe shape still open.)

---

*Below: provenance — feature-internal decisions folded from `features/effects/decisions/0001-async-layer-hierarchy.md`. Single-feature scope (the layer hierarchy is internal to effects; other features consume the resulting types but don't co-decide them).*

## Options considered

### Q1 — Async layer hierarchy (formerly ADR-0001 — folded)

Async behavior in a card game spans several distinct kinds: animation handoff (presentation), decision wait (player input), simulation pause (rule), and resolver yield (engine). Treating all of them with one `Task` returned from a handler smears responsibilities.

- **A — Single async layer (`Task<Result>` everywhere).** Rejected: conflates animation, decision, and simulation pauses.
- **B — No async.** Rejected: real games need decisions and animation handoff.
- **C — Layered hierarchy: presentation-async (animation), decision-async (player input via `IAvailableActions` decision-scoped snapshot), engine-async (resolver yield). Each layer has its own continuation/handle.** **Chosen.**

## Rationale

Async behavior is layered. Each layer owns its own continuation (animation, decision, engine yield). Effects compose across layers via the handler context, never by smashing them into one `Task`. This is the foundational shape that makes the rest of the effects feature tractable — replacement, prevention, ongoing effects, and decisions all build on it.

## Consequences

- **Enables:** clean handoffs and per-layer testability; effects compose across layers without conflating their semantics; decision-as-command pattern works (handler awaits player; effect awaits handler).
- **Constrains:** handlers and effects must declare which layer they're awaiting. The two-dispatch-paths split (`engine.Dispatch` vs `scope.Dispatch`) is non-negotiable — using the wrong one breaks the execution model.
- **Cascade:** the layer hierarchy is consumed by `triggers/01-trigger-fold` (the `GameCommand` type hierarchy reflects it), `engine/01-evaluation-loop` (the queue holds only engine-dispatched commands), and `actions/01-command-pipeline` (handlers execute synchronously within a single envelope).
