# Async Decisions And Suspension

This document defines how the engine handles effects that require player input — targeting, choices, mulligans — and establishes the layer hierarchy that makes async orchestration tractable.

Builds on:

- [20-engine-evaluation-loop.md](20-engine-evaluation-loop.md) — engine loop, `GameRules`, `OnQueueDrained`
- [21-trigger-reaction-system.md](21-trigger-reaction-system.md) — type hierarchy, trigger fold
- [18-action-flow-snippets.md](18-action-flow-snippets.md) — pipeline, envelope, handler flow
- [19-handler-mutations-and-semantics.md](19-handler-mutations-and-semantics.md) — composite actions and scope

---

## 1. The Layer Hierarchy

Three distinct layers handle game logic, and each has exactly one job:

```
Effect
  └── orchestrates multiple GameActions — the only layer that can sequence commands
        └── GameAction applies StateMutations — atomic, single envelope, synchronous handler
              └── CompositeAction groups StateMutations — reusable unit inside a handler
```

| Layer | Job | Can sequence commands? | Async? | Envelope |
|---|---|---|---|---|
| **Effect** | Orchestrate commands, thread results | Yes — only layer that can | Yes | None of its own |
| **`GameAction`** | Apply state changes, return a result | No | No — synchronous | One per action |
| **`CompositeAction`** | Reusable state-change group inside a handler | No | No | Folds into parent |

This hierarchy is the key constraint. Only effects sequence commands. A `GameAction` handler may use `CompositeAction`s internally, but it never dispatches other `GameAction`s — that is the effect's responsibility.

---

## 2. Where Async Lives

Async lives **only at the effect layer**. An effect awaits each command's `CommandResult` before dispatching the next. Commands and composites remain synchronous throughout.

```csharp
// Effect — async orchestration only
public async Task Execute(IEffectScope scope)
{
    var targetResult  = await scope.Dispatch(new RequestTargetAction { Filter = EnemyFilter });
    var damageResult  = await scope.Dispatch(new DealDamageAction   { Target = targetResult.Target, Amount = 6 });
    await scope.Dispatch(new MoveToDiscardAction { Source = source });
}
```

Each `scope.Dispatch` call submits a `GameAction` to the engine, awaits its completion, and returns the `CommandResult`. The effect resumes when the command finishes — including when that command required player input.

---

## 3. Decisions As Commands

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

The effect receives the `CommandResult` and passes the resolved target directly into the next dispatch. No partial envelopes, no suspension inside a handler, no special engine state for in-progress decisions.

---

## 4. Concrete Scenarios

### Scenario A — Targeted Spell

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

### Scenario B — Choose One (Discover)

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

### Scenario C — Mulligan

Mulligan is a pre-game session lifecycle event, not a mid-command decision. It does not go through the normal command pipeline. It is a session setup phase where each actor is presented a `MulliganDecision` directly, responds, and the session advances once all actors have confirmed.

Whether mulligan is modelled as a specialized `GameAction` dispatched during session setup, or as a separate session lifecycle step, is a deferred design question tied to game lifecycle (checkpoint task 12).

### Scenario D — Mid-Chain Triggered Target

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

---

## 5. What The Client Sees

Because each `GameAction` publishes its own complete envelope before the next begins:

- The client always sees fully committed state — no partial or speculative state.
- A decision command's envelope can carry a `DecisionOpenedSignal` that tells the client to show the appropriate UI (target highlights, choice overlay, etc.).
- While the decision is open, the client has all prior envelopes and can render the current state accurately.
- When the decision resolves, the result is in the `CommandResult` and recorded in history.

No partial envelopes needed. No mid-execution state visible to the client.

---

## 6. Envelope Shape For Decisions

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

---

## 7. Effect Scope

The effect interacts with the engine through an `IEffectScope`:

```csharp
public interface IEffectScope
{
    Task<TResult> Dispatch<TResult>(GameAction action) where TResult : CommandResult;
}
```

The effect never touches the engine queue directly. `IEffectScope.Dispatch` submits the command, drives the engine through that command's full lifecycle (before fold → handler → after fold → envelope published), and returns the result.

---

## 8. Open Questions

- **Effect registration** — where is an effect defined? On the card definition as a typed class? As a reference from the handler? How does the engine know which effect to run for a given `GameAction`?
- **Effect cancellation** — if the `OnBefore<T>` fold cancels a `GameAction` mid-effect, does the effect stop? Does it receive a cancelled `CommandResult` and decide itself?
- **Nested decisions** — can an effect dispatch a `RequestTargetAction` while another decision is already open (e.g. Scenario D with concurrent triggers)? How does the engine queue shape handle this?
- **Multiplayer visibility** — in a multiplayer game, does the `DecisionOpenedSignal` need to reach other clients before the deciding player has responded? Or is it sufficient for the full envelope to arrive after resolution?
- **Mulligan lifecycle** — does mulligan go through the command pipeline at all, or is it a separate session setup phase? (Deferred to game lifecycle design, checkpoint task 12.)
- **Effect as a first-class type** — should `Effect` be a named type in the framework, or is it just a convention for how game code is structured? Does it live on the definition?

---

## 9. Related

- [20-engine-evaluation-loop.md](20-engine-evaluation-loop.md) — engine loop, Stash scheduling
- [21-trigger-reaction-system.md](21-trigger-reaction-system.md) — type hierarchy, trigger fold
- [19-handler-mutations-and-semantics.md](19-handler-mutations-and-semantics.md) — CompositeAction and scope utilities
- [06-open-questions.md](06-open-questions.md) — async suspension open item
