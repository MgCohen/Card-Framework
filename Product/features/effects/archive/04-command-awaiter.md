---
type: model
status: superseded
superseded_by: [decisions/0011-effect-orchestration-dispatch-requests.md, features/effects/archive/06-request-system.md, features/effects/archive/02-effect-scope.md]
archived_on: 2026-05-11
reason: "`ICommandAwaiter` retired by ADR-0011. The mechanism (type-only routing for player-decision awaits inside handler bodies) is wholly replaced by the state-linked request system (`Request<TResponse>` / `Response` / `RequestsSlice` / `AnswersSlice` / `IRequestService`); decision routing moves from handler `await` to effect `await scope.Dispatch(request)`. Handler bodies become atomic from the player-input POV per ADR-0010, and player decisions ride the request system through the single `IEffectScope.Dispatch` surface."
references: [features/effects/archive/01-effect.md, features/effects/archive/06-request-system.md, features/effects/archive/02-effect-scope.md, features/actions/archive/01-command-pipeline.md, features/engine/archive/01-game-engine.md]
tags: [#effects, #model, #awaiter, #decisions, #archived]
---

> **Archived 2026-05-11.** Superseded by [ADR-0011](Product/decisions/0011-effect-orchestration-dispatch-requests.md). `ICommandAwaiter` is gone; the type-only routing pattern documented here is replaced by the state-linked request system. Decision routing moves from handler-side `await ctx.Resolver.Wait<TCommand>()` to effect-side `await scope.Dispatch(request)`; the canonical home for the new shape is [`features/effects/archive/06-request-system.md`](features/effects/archive/06-request-system.md) (request mechanism) and [`features/effects/archive/02-effect-scope.md`](features/effects/archive/02-effect-scope.md) (the unified `Dispatch` surface). The `IHandlerContext` references throughout this Model are also stale — per [ADR-0010](Product/decisions/0010-validator-handler-split.md) the aggregator is gone (archived at `Product/features/effects/archive/03-handler-context.md`). Content below is preserved as historical record; treat code samples and contract descriptions as obsolete.

# Command Awaiter — model

## What it is

`ICommandAwaiter` is the suspension/resume mechanism for player decisions. A decision handler calls `ctx.Resolver.Wait<TCommand>()` and suspends; player intent enters as a normal `GameCommand`; the engine's pre-pipeline awaiter check pattern-matches by type, completes the awaiting `Task`, and short-circuits the normal pipeline.

Type-only routing eliminates the need for a `DecisionId` or any snapshot-scope discriminator: the type parameter `TCommand` is the routing key.

```
Effect.Execute (per 01-effect.md)
  └─ scope.Dispatch(RequestTargetAction { ... })
        └─ engine routes → RequestTargetHandler.Handle(action, ctx)
              ctx.Actions.Set(new AvailableActionsSnapshot(...));
              var cmd = await ctx.Resolver.Wait<SelectTargetCommand>();   // suspended
              return new RequestTargetResult { Target = cmd.Target };
        ▲
        │   Player intent → IntentMapper.Map(template, choice) → SelectTargetCommand
        │   engine.Dispatch(SelectTargetCommand)
        │     pre-pipeline: awaiter.TryResolve(SelectTargetCommand) → match → short-circuit
        │     Wait<SelectTargetCommand> task completes → handler resumes
```

## Contracts

### `ICommandAwaiter`

```csharp
public interface ICommandAwaiter
{
    Task<TCommand> Wait<TCommand>(CancellationToken ct = default)
        where TCommand : GameCommand;
    bool TryResolve(GameCommand cmd);
}
```

- **`Wait<TCommand>(ct)`** — registers a closure-captured `cmd is TCommand` predicate backed by a `TaskCompletionSource<TCommand>`. Returns the `Task`. Used by decision handlers to pause until a matching command arrives. Single-open per type-hierarchy is a runtime assert.
- **`TryResolve(cmd)`** — pattern-matches `cmd` against every registered predicate. On a match, completes the associated TCS with the concrete command; returns `true`. Returns `false` if no predicate matches (the engine falls through to the normal pipeline). Called by the engine's pre-pipeline check on every `Dispatch`.

The awaiter is exposed on `IHandlerContext` (per [`03-handler-context.md`](features/effects/archive/03-handler-context.md)). Decision handlers call `Wait`; effects never see this surface directly — they observe decisions only through `scope.Dispatch` returning a `CommandResult`.

### Decision routing — type-only

The awaiter registers a `cmd is TCommand` predicate — the generic type parameter is the routing key. There is **no `DecisionId`**, no `CommandId` carried by the awaiter or the snapshot. The mapper produces a normal `SelectTargetCommand` or `PickOptionCommand` without any special annotation; the engine's pre-pipeline check pattern-matches it.

This removes the "where does a decision id originate?" question entirely, and eliminates any `DecisionPrompt(CommandId)` snapshot scope field that previously carried it. The snapshot never needs to signal that a decision is open — the awaiter answers that directly.

While a decision-handler's `Handle` is awaiting `Resolver.Wait<TCommand>()`, the dispatched action has not completed. Its after-fold has not run. No triggered effect is running. The next `scope.Dispatch` line in the requesting effect cannot start until this one returns. **Sequential nesting only.**

Single-open per type-hierarchy is a runtime assert. Two `Wait` registrations with overlapping type hierarchies is a programming error.

### The uniform boundary

The gateway / intake layer treats all player input identically. Every intent maps to a `GameCommand` via `IntentMapper.Map` and enters `engine.Dispatch`. No scope branch at the gateway; the mapper is decision-unaware.

```
intent → IntentMapper.Map(template, choice) → GameCommand → engine.Dispatch
```

`IIntentMapper.Map` (per [`actions/02-intent-mapper.md`](features/actions/archive/02-intent-mapper.md)) switches on the typed template subtype only:

```csharp
public GameCommand Map(ActionTemplate template, PlayerChoice choice) => template switch
{
    MoveCardTemplate     m => new MoveCardCommand(m.Card, state.LocateCard(m.Card), (Ref<Zone>)choice.Targets[0]),
    ChooseTargetTemplate t => new SelectTargetCommand(choice.Targets[0]),
    _ => throw new InvalidOperationException($"unknown template: {template}")
};
```

No snapshot scope inspection. The mapper produces `SelectTargetCommand` whether or not a decision is open — it does not know.

### Engine-level pre-pipeline awaiter check

On every `engine.Dispatch`, the engine runs the awaiter check **before** the normal pipeline (per [`engine/01-game-engine.md`](features/engine/archive/01-game-engine.md)):

```csharp
// Engine.Dispatch (simplified)
if (awaiter.TryResolve(command))
    return;   // short-circuit: no envelope, no handler, no triggers

// normal five-validation-layer pipeline (ADR-0007):
BeginPipeline(command);
```

If the awaiter matches (`cmd is TCommand`), it completes `Wait<TCommand>()` and the engine short-circuits. The matched command never reaches the handler or trigger fold. One logical event (the request action) produces one envelope.

Non-matching commands fall through to the normal pipeline; all five validation layers ([ADR-0007](Product/decisions/0007-five-validation-layers.md)) gate them as usual.

### Decisions as `GameAction`s

A decision (choose a target, pick an option) is not a special pipeline concept. It is a `GameAction` that:

- Opens a decision visible to the relevant actor (via `ctx.Actions.Set(snapshot)` from the handler).
- Waits for the player to respond (via `ctx.Resolver.Wait<TCommand>()`).
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

The effect receives the `CommandResult` and passes the resolved target directly into the next dispatch. No partial envelopes, no suspension inside an effect, no special engine state for in-progress decisions.

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

### `ICommandHandler.Handle` returns `Task<TResult>`

For decision handlers to await `Resolver.Wait<TCommand>()`, the handler signature must be async-capable. The signature is owned by [`actions/03-command-handler.md`](features/actions/archive/03-command-handler.md); most handlers complete synchronously and return `Task.FromResult(...)`. Only decision handlers genuinely await.

## Invariants

- **Type-only routing.** The `TCommand` type parameter in `ICommandAwaiter.Wait<TCommand>()` is the routing key. There is no `DecisionId` and no `CommandId` carried by the awaiter or the snapshot.
- **At most one open awaiter per type hierarchy.** Single-open is a runtime assert. Sequential nesting only.
- **Uniform boundary.** All player input goes `intent → IntentMapper.Map(template, choice) → GameCommand → engine.Dispatch`. The mapper switches on template subtype only and is decision-unaware. The engine's pre-pipeline awaiter check intercepts matched commands before the normal pipeline.
- **Decisions are normal `GameAction`s.** No partial envelopes, no special "in-progress decision" engine state.
- **Effects do not see the awaiter.** The awaiter lives on `IHandlerContext`, not `IEffectScope`. Effects observe decisions only through the `CommandResult` returned by `scope.Dispatch`.

## Decisions

*n/a — no elevated ADR.* The original suspension-model ADR-0002 is superseded by type-only routing, which applies to a single feature; the supersede + the chosen mechanic are folded as Q1 below.

## Examples

### Pick-a-target (Fireball internals)

```csharp
public sealed class RequestTargetHandler
    : ICommandHandler<RequestTargetAction, RequestTargetResult>
{
    public async Task<RequestTargetResult> Handle(RequestTargetAction action, IHandlerContext ctx)
    {
        var legal = ComputeLegalTargets(ctx.Store, action.Filter);

        ctx.Actions.Set(new AvailableActionsSnapshot(
            new[] { new ChooseTargetTemplate(legal) }));

        var cmd = await ctx.Resolver.Wait<SelectTargetCommand>();

        return new RequestTargetResult { Target = cmd.Target };
    }
}
```

The handler suspends on `Wait<SelectTargetCommand>`. Whatever the player picks (from the published `ChooseTargetTemplate`), `IntentMapper.Map` produces a `SelectTargetCommand`; the engine's pre-pipeline `awaiter.TryResolve` matches and resumes.

## Open follow-ups

- **`ICommandAwaiter` shape — full review.** The TCS-backed type-predicate shape is the simplest mechanic that satisfies the await/resume contract. Not stress-tested against: error/timeout semantics, multi-actor decisions, suspension/resumption across engine ticks (save/load). Needs a focused review when the FSM-shaped state model lands.
- **FSM-shaped game state.** Type-only routing is the current mechanism for recognizing in-progress decision commands. The FSM-shaped state model (`PickATargetState`, `MainPhaseState`, etc., each owning their own snapshot-generation surface) is the eventual successor — it would replace type-only routing with richer, self-describing game-state phases. Revisit when the first async-decision-heavy game arrives.
- **Suspension across engine ticks (save/load).** What state must persist if a game saves mid-decision is deferred — the resolver's TCS plus the dispatched action's cause envelope are the candidates, but the persistence shape depends on game-lifecycle design (checkpoint task #12).
- **Multiplayer visibility on `DecisionOpenedSignal`** — must other clients see the signal before the deciding player responds, or is full envelope on resolution sufficient?
- **Nested decisions** — concurrent overlapping awaiters. Single-open per type-hierarchy is settled as runtime assert; broader nesting model still open.

---

*Below: provenance — how the suspension model was settled and then superseded. Single-feature scope (no project-wide ADR applies; folded into Model provenance per §4.5 on 2026-05-08; extracted from `04-effect-flow.md` on 2026-05-09 during the load-bearing extraction pass). Original ADR-0002 landing 2026-04-29; superseded by the type-only routing decision below on the same Model timeline.*

## Options considered

### Q1 — Suspension model

When an effect needs a player choice mid-resolution, the simulation must suspend and resume cleanly across snapshot/restore and replay. Implicit continuations break replay and make per-player views ambiguous.

- **A — Implicit continuations via captured async state.** Rejected — captured state lives outside `GameState` and breaks replay.
- **B — Block the simulation thread until decision arrives.** Rejected — locks presentation and networking.
- **C — Explicit decision IDs threaded through state; effect publishes a `DecisionPrompt`-scoped snapshot, suspends, and resumes when a matching player intent arrives.** Originally chosen (ADR-0002, 2026-04-29). **Superseded** by the type-only routing decision.
- **D — Type-only routing via `ICommandAwaiter.Wait<TCommand>()`; decision handler awaits a `cmd is TCommand` predicate; engine pre-pipeline awaiter check short-circuits matched commands. No `DecisionId`, no snapshot scope discriminator.** **Chosen.**

The suspension *requirement* (snapshot/restore-safe, replayable, multiplayer-correct) is unchanged; the *mechanism* is simpler. The `DecisionPrompt` snapshot scope discriminator is dropped; the handler-side awaiter is the resume mechanism instead.

## Rationale

Type-only routing eliminates two things that the original ID-threaded model required: (1) where the decision id originates, and (2) a snapshot scope discriminator that signals "a decision is open." Both questions disappear when the awaiter answers them: the engine's pre-pipeline `awaiter.TryResolve(cmd)` check matches by type, completes the TCS, and short-circuits. The mapper stays decision-unaware; the snapshot stays scope-agnostic.

The trade is single-open-per-type-hierarchy: two concurrent decisions for the same command type are not supported. Sequential nesting is fine. This is a hard constraint, but in practice every decision in flight is one specific question for one specific actor, so it has not bitten any sample game. If multi-actor or overlapping decisions become load-bearing, the FSM-shaped state model (Open follow-ups) is the chosen successor.

## Consequences

- **Enables** snapshot/restore and replay across decisions (no async state outside the envelope stream); transparent multiplayer routing of decisions to the right actor (the awaiting handler is the actor's handler); a drastically simpler mapper (decision-unaware); a drastically simpler snapshot (no scope discriminator).
- **Constrains** every decision to be a normal `GameAction` whose handler awaits `Resolver.Wait<TCommand>()`; one open awaiter per type hierarchy at a time (runtime assert); the `ICommandAwaiter` mechanism is not stress-tested against multi-actor / error / timeout — full review tracked in Open follow-ups.
