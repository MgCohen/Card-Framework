---
type: model
status: settled
references: [features/actions/models/01-command-pipeline.md, features/actions/models/03-command-handler.md, features/engine/models/01-game-engine.md, features/engine/models/07-match-state.md, features/state/state.md, decisions/0007-five-validation-layers.md, decisions/0010-validator-handler-split.md]
tags: [#actions, #model, #validator]
---

# Command Validator — model

## What it is

`ICommandValidator<TCommand>` is the per-command legality interface. Validators are independent — each receives only the command and answers a yes/no question against the store (read via DI on the abstract base). The pipeline resolves every registered validator for the command's runtime type, runs them in DI registration order, and short-circuits on the first rejection. Authoring a validator that is also a handler uses the combined `ValidatedCommandHandler<TCommand, TResult>` base.

Validators are uniform across the entire `GameCommand` family — `GameAction`, `CompositeAction`, `GameSignal`, and (per ADR-0011 — *forthcoming*) `Request<TResponse>` and `Response`. The same registry, the same short-circuit semantics, the same registration-order rule. Framework defaults (`SessionGate`, `MatchStateActionGate`) register first; games append.

```text
Dispatch(command)
  resolve all ICommandValidator<TCommand> for typeof(command)
  for each validator in registration order:
    if !validator.Validate(command, out reason) → reject
  route by kind (handler / composite / signal / request / response)
```

## Contracts

### `ICommandValidator<TCommand>` — the interface

```csharp
public interface ICommandValidator<TCommand>
    where TCommand : GameCommand
{
    bool Validate(TCommand command, out string? reason);
}
```

One method. Returns `true` with `reason = null` on accept; `false` with a non-null `reason` on reject. The pipeline surfaces the first non-null reason to the caller (`engine.Dispatch`'s try-get `out string? reason`).

The signature carries no state parameter — validators read state through DI on the abstract base, not as a parameter. This keeps the interface minimal and makes validators reusable across compositions (a validator that needs no state can ignore the base; one that needs state inherits the base).

### `CommandValidator<TCommand>` — the abstract base

```csharp
public abstract class CommandValidator<TCommand>
    : ICommandValidator<TCommand>
    where TCommand : GameCommand
{
    [Inject] protected Store Store { get; private set; } = null!;
    public abstract bool Validate(TCommand command, out string? reason);
}
```

Property-injected `Store`. Subclasses override `Validate`; nothing else to wire. Validators that need additional services (a modifier factory, a game-specific lookup) declare them via the constructor on the concrete subclass — same pattern as handlers.

### `ValidatedCommandHandler<TCommand, TResult>` — combined base

Common case: one class authors both `Validate` and `Handle` for a single command type. Use the combined base to avoid registering two classes for the same command.

```csharp
public abstract class ValidatedCommandHandler<TCommand, TResult>
    : GameCommandHandler<TCommand, TResult>, ICommandValidator<TCommand>
    where TCommand : GameCommand
    where TResult  : CommandResult
{
    public abstract bool Validate(TCommand command, out string? reason);
}
```

DI registration produces a single instance that satisfies both `ICommandValidator<TCommand>` and `ICommandHandler<TCommand, TResult>`. Use when validation and handling logic share private helpers; split into separate classes when validation is reusable across commands (creature-tap rule applies to several command types) or when separation aids testing.

### Pipeline integration

The framework's `ICommandPipeline.Validate` (per [`01-command-pipeline.md`](features/actions/models/01-command-pipeline.md)) resolves every registered `ICommandValidator<TCommand>` for the runtime type of the dispatched command, runs them in DI registration order, and short-circuits on first reject. Concretely:

```csharp
// ICommandPipeline.Validate (simplified)
public bool Validate(GameCommand command, out string? reason)
{
    var validators = container.ResolveAll(typeof(ICommandValidator<>).MakeGenericType(command.GetType()));
    foreach (var v in validators) {
        if (!((ICommandValidator)v).Validate(command, out reason))
            return false;       // short-circuit
    }
    reason = null;
    return true;
}
```

No validator registered for a command type → validation is a no-op for that dispatch (accept-by-default). Validation is opt-in per command type.

### Framework-default validators

Two ship with the framework, register first, and consult engine-owned state. They replace the inline two-layer intake gate that lived in `engine.Dispatch` between ADR-0009 and ADR-0010.

#### `SessionGate` — Layer 1: session-level

```csharp
public sealed class SessionGate : CommandValidator<GameCommand>
{
    private readonly IEngineSession session;
    public SessionGate(IEngineSession session) { this.session = session; }

    public override bool Validate(GameCommand command, out string? reason)
    {
        if (session.Current is EndedSession) {
            reason = "MatchEnded";
            return false;
        }
        if (session.Current is NotStartedSession
            && command is not StartMatchCommand
            && command is not EndMatchCommand) {
            reason = "MatchNotStarted";
            return false;
        }
        reason = null;
        return true;
    }
}
```

Reads through a narrow `IEngineSession` capability — engine-owned read view of `engine.Session`. Rejects when the engine is not accepting commands (`NotStartedSession` except lifecycle commands; `EndedSession` for anything).

#### `MatchStateActionGate` — Layer 2: match-state-actions

```csharp
public sealed class MatchStateActionGate : CommandValidator<GameCommand>
{
    public override bool Validate(GameCommand command, out string? reason)
    {
        if (command is StartMatchCommand or EndMatchCommand or TransitionStateCommand) {
            reason = null;
            return true;       // lifecycle commands bypass
        }
        var current = Store.Get<MatchStateSlice>().Current;
        if (current.Actions.Count > 0 && !current.Actions.Contains(command.GetType())) {
            reason = $"NotInMatchStateActions:{current.GetType().Name}";
            return false;
        }
        reason = null;
        return true;
    }
}
```

Reads `MatchStateSlice.Current` (per [`engine/07-match-state.md`](features/engine/models/07-match-state.md)). Rejects when the command's runtime type isn't in `current.Actions`, unless `Actions` is empty (open state) or the command is a framework lifecycle command.

Both gates are registered before any game-shipped validator. Convention; whether the framework enforces order is `oq-validator-defaults`.

### Validator policy

- **Short-circuit on first reject.** First validator to reject wins; remaining validators do not run. One `reason` returned to the caller.
- **Registration order = run order.** DI registration order is the run order. Reorder by changing registration. Framework defaults register first; games append after.
- **Optional registration.** A command type with no registered validator skips validation (accept-by-default).
- **Independent.** Each validator receives only the command. No shared context bag between validators or with the handler. State (when needed) comes via DI on the base.
- **Uniform across `GameCommand` kinds.** Same machinery for actions, requests, responses, signals, composites.

### What validators do NOT do

- **Validators do not mutate state.** No `Store.Execute(payload)`. Pure read.
- **Validators do not dispatch commands.** No `engine.Dispatch` from inside `Validate`. Sequencing belongs to effects.
- **Validators do not consume player input.** No awaits, no decisions. Validators answer synchronously against the store.
- **Validators do not produce more than one reason at a time.** The dispatch-time surface is `out string? reason` — one reason wins. The `Diagnose(command)` UI affordance (deferred per ADR-0010) is the place to surface multiple.

### Diagnose surface (deferred)

A `Diagnose(command) → IReadOnlyList<string>` capability that runs *all* validators non-short-circuit and returns every rejection reason is a candidate UI affordance. Useful for "why can't I play this?" tooltips listing every failing layer. Built on the same registry as `Validate`; lives off the dispatch path. Lands when the first UI consumer needs it. Tracked informally in [ADR-0010](Product/decisions/0010-validator-handler-split.md) §Defers.

## Invariants

- **One method per validator.** `bool Validate(TCommand command, out string? reason)`. No state parameter on the interface; state via DI on the base.
- **Independent.** Validators share no per-dispatch context. State comes from the store via DI; transient cross-validator coordination is not a supported pattern.
- **Short-circuit on first reject.** First failing validator decides; remaining validators do not run.
- **Registration order = run order.** Framework-default registrations precede game-shipped registrations by convention.
- **Optional.** Commands with no registered validator skip validation. Validation is opt-in per command type.
- **Uniform across `GameCommand` kinds.** Same registry, same semantics for every subtype.
- **Pure read.** Validators do not mutate state, do not dispatch, do not await.

## Decisions

Linked: [ADR-0010](Product/decisions/0010-validator-handler-split.md) — validator / handler split; `IHandlerContext` retired; framework intake validators. Spike provenance: `Product/spikes/fsm-architecture.md` D10 + D11.

Linked: [ADR-0007](Product/decisions/0007-five-validation-layers.md) — five validation layers principle. Refined by ADR-0010: the "authoritative" layer is the registered validator chain (not a single `handler.Validate`); the engine-flow-gate and match-state-action-gate layers are now framework validators in the chain.

## Examples

### Reusable single-purpose validator (no handler)

```csharp
// Game-shipped legality check; not tied to a single command.
public sealed class ActorOwnsTargetValidator : CommandValidator<TargetedCommand>
{
    public override bool Validate(TargetedCommand cmd, out string? reason)
    {
        var ownership = Store.Get<OwnershipState>();
        if (ownership.OwnerOf(cmd.Target) != cmd.Actor) {
            reason = "TargetNotOwned";
            return false;
        }
        reason = null;
        return true;
    }
}
```

Registered against the cross-cutting `TargetedCommand` base; runs for every command whose runtime type derives from `TargetedCommand`. Adding a new targeted command picks up the rule for free.

### Combined validator + handler

```csharp
public sealed class MoveCardHandler
    : ValidatedCommandHandler<MoveCardCommand, MoveCardResult>
{
    public override bool Validate(MoveCardCommand move, out string? reason)
    {
        var card = Store.Catalog.Resolve(move.Card);
        if (card is null) { reason = "UnknownCard";  return false; }
        var contents = Store.Get<ZoneContentsState>(move.From);
        if (!contents.Cards.Contains(move.Card)) { reason = "NotInSource"; return false; }
        reason = null;
        return true;
    }

    public override Task<MoveCardResult> Handle(MoveCardCommand move)
    {
        Store.ExecuteBatch(new object[] {
            MoveCardMutation.AtTop(
                Store.Catalog.Resolve(move.Card),
                Store.Catalog.Resolve(move.From),
                Store.Catalog.Resolve(move.To)),
            new SetLastMovePayload(new LastMove(move.Card, move.From, move.To)),
        });
        return Task.FromResult(new MoveCardResult(move.Card));
    }
}
```

One DI registration satisfies both `ICommandValidator<MoveCardCommand>` and `ICommandHandler<MoveCardCommand, MoveCardResult>`. The framework's `SessionGate` and `MatchStateActionGate` run before this validator; this validator runs before this handler.

### Validator chain in action — Pile Counter dispatch

```text
Player drags top of Pile A to Pile B
  → engine.Dispatch(MoveCardCommand(card, A, B))
     → pipeline.Validate runs registered validators in order:
        1. SessionGate                   → ok (InMatchSession)
        2. MatchStateActionGate          → ok (PilePlayingState.Actions contains MoveCardCommand)
        3. MoveCardHandler (as validator) → ok (card exists, in source)
     → no rejection; route to handler.Handle
     → enqueue + drain
```

If the match has ended, `SessionGate` short-circuits with `"MatchEnded"`; no other validator runs. If the player attempts a `SelectAttackerCommand` while in `PilePlayingState` (a Magic-style command in a Pile Counter game), `MatchStateActionGate` short-circuits with `"NotInMatchStateActions:PilePlayingState"`.

## Open follow-ups

- `oq-validator-defaults` — final list of framework-default validators (`SessionGate` + `MatchStateActionGate` are canonical; is a third needed?), whether registration order is enforced (vs by convention), and the `[ValidatorOrder]`-style affordance for finer control if it surfaces.
- **`Diagnose` surface** — multi-reason UI feedback affordance. Deferred per [ADR-0010](Product/decisions/0010-validator-handler-split.md). Pin when the first UI consumer needs richer rejection feedback than the single-reason try-get pattern provides.
- **Validator on `GameSignal`s** — uniform support is structural (signals go through the same pipeline), but no canonical use case has surfaced. Wait for one.
- **Cross-cutting validators on base types.** Registering a validator against `TargetedCommand` (a game-shipped command base) and having it run for every derived runtime type is supported by the closed-generic resolution pattern; the framework-internal `ResolveAll` over the runtime type covers it. Confirm at first concrete implementation.

---

*Below: provenance — feature-internal Model authored 2026-05-11 as part of the ADR-0010 cascade. No options-weighing at this Model's scope; the surface is dictated by ADR-0010.*

## Options considered

*n/a — descriptive Model. Choices made at ADR-0010 scope; see linked ADR for option-weighing.*

## Rationale

*n/a — see `## Contracts` and [ADR-0010](Product/decisions/0010-validator-handler-split.md).*

## Consequences

*n/a — see [ADR-0010](Product/decisions/0010-validator-handler-split.md) `## Consequences`.*
