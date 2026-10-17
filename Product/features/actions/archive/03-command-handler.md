---
type: model
status: superseded
superseded_by: ../docs/architecture.md
archived_on: 2026-10-18
reason: "Built as CommandHandler<T>.Handle(T) returning void with ctor-injected services; no async GameCommandHandler or injected Store."
references: [features/actions/archive/01-command-pipeline.md, features/actions/archive/04-history-envelope.md, features/actions/archive/06-command-validator.md, features/state/archive/state.md, decisions/0007-five-validation-layers.md, decisions/0010-validator-handler-split.md]
tags: [#actions, #model, #handler]
---

> **Archived 2026-10-18.** Superseded by [`docs/architecture.md`](docs/architecture.md). Built as CommandHandler<T>.Handle(T) returning void with ctor-injected services; no async GameCommandHandler or injected Store. The body below is the old model, kept as history.

# Command Handler — model

## What it is

`ICommandHandler<TCommand, TResult>` is the per-command resolver. `Handle(command) → Task<TResult>` reads state through the property-injected `Store`, decides outcome, calls `Store.Execute(payload)` to fan out to mutators, and returns a typed `CommandResult`. Validation lives in [`06-command-validator.md`](features/actions/archive/06-command-validator.md) — a separate `ICommandValidator<TCommand>` interface per [ADR-0010](Product/decisions/0010-validator-handler-split.md). One class may implement both via the combined `ValidatedCommandHandler<TCommand, TResult>` base.

```text
pipeline.Validate
  → resolve all ICommandValidator<TCommand>           // see 06-command-validator.md
  → run in registration order; short-circuit on reject
pipeline.Execute (drain)
  → handler.Handle(command)                            // this Model
     → Store.Execute(payload)                          // fan out to mutators (state/state.md)
     → return new TResult(...)
```

## Contracts

### `ICommandHandler<TCommand, TResult>`

```csharp
public interface ICommandHandler<TCommand, TResult>
    where TCommand : GameCommand
    where TResult  : CommandResult
{
    Task<TResult> Handle(TCommand command);
}
```

Each `GameCommand` type has exactly one registered handler. Most handlers complete synchronously and return `Task.FromResult(...)`. Handler bodies are atomic from the player-input POV — no awaiting player decisions inside `Handle` (player decisions flow through the request system per [ADR-0011](Product/decisions/0011-effect-orchestration-dispatch-requests.md)). The `Task<TResult>` return shape stays for two reasons: (a) effect orchestrators still `await scope.Dispatch(...)` against handler completion, and (b) framework-internal asynchronous work (transition handlers enqueueing follow-ups, for example) composes naturally on the same shape.

### `GameCommandHandler<TCommand, TResult>` — the abstract base

```csharp
public abstract class GameCommandHandler<TCommand, TResult>
    : ICommandHandler<TCommand, TResult>
    where TCommand : GameCommand
    where TResult  : CommandResult
{
    [Inject] protected Store Store { get; private set; } = null!;
    public abstract Task<TResult> Handle(TCommand command);
}
```

Property-injected `Store`. Subclasses override `Handle`; additional services land via the subclass constructor. Per [ADR-0010](Product/decisions/0010-validator-handler-split.md), this base replaces the retired `IHandlerContext` aggregator — handlers no longer receive a `ctx` parameter; they read `Store` from the base.

### `ValidatedCommandHandler<TCommand, TResult>` — combined base

```csharp
public abstract class ValidatedCommandHandler<TCommand, TResult>
    : GameCommandHandler<TCommand, TResult>, ICommandValidator<TCommand>
    where TCommand : GameCommand
    where TResult  : CommandResult
{
    public abstract bool Validate(TCommand command, out string? reason);
}
```

Used when one class authors both the legality check and the resolution logic for a single command type. Full surface details on `ICommandValidator<TCommand>` live in [`06-command-validator.md`](features/actions/archive/06-command-validator.md).

### Writes — through `Store.Execute(payload)`

Handlers do not write state inline. They call `Store.Execute(payload)`; Scaffold's registry fans out to every mutator registered for `TPayload` and commits atomically. The `Store` / `Mutator<TState, TPayload>` mechanic is owned by [`state/state.md`](features/state/archive/state.md).

```csharp
public sealed class MoveCardHandler
    : GameCommandHandler<MoveCardCommand, MoveCardResult>
{
    public override Task<MoveCardResult> Handle(MoveCardCommand cmd)
    {
        Store.Execute(new MoveCardPayload(cmd.Card, cmd.From, cmd.To));
        return Task.FromResult(new MoveCardResult(cmd.Card, cmd.From, cmd.To));
    }
}
```

Payloads are dedicated record types (`MoveCardPayload`), not commands. The handler does not enumerate which mutators run; the registry routes by payload type. Adding a third reactive slice (move counter, undo log, etc.) is a new payload + mutator + `Register` line; the handler does not change.

### Constructor injection for additional services

```csharp
public sealed class ApplyAuraHandler
    : GameCommandHandler<ApplyAuraCommand, ApplyAuraResult>
{
    private readonly IModifierFactory modifiers;

    public ApplyAuraHandler(IModifierFactory modifiers)
    {
        this.modifiers = modifiers;
    }

    // Store comes from the base via [Inject]; modifiers via constructor on top.
    public override Task<ApplyAuraResult> Handle(ApplyAuraCommand cmd)
    {
        var target = Store.Catalog.Resolve(cmd.Target);
        target.Storage.AddModifier(cmd.Buff);
        return Task.FromResult(new ApplyAuraResult(cmd.Target));
    }
}
```

`Store` is the universal handler need (property-injected); per-handler services are explicit. The previously-aggregated `IHandlerContext` is gone.

### `CommandResult`

Structured outcome from a handler. Subtypes per command. Examples: approved/rejected/cancelled flag, actual destination, was-redirected, damage amount, shield-vs-health damage, drawn instance ID, created instance IDs, opened decision ID.

Used for: branching inside effects (`if (damage.IsCancelled) return;`), trigger/reaction conditions, UI feedback, history envelope contents, tests. Populates `HistoryEnvelope.Result` (per [`04-history-envelope.md`](features/actions/archive/04-history-envelope.md)).

Cancellation shape (flag on the base record vs. typed `CancelledResult` subtype) is a small decision deferred to command-pipeline cleanup; either works.

## Invariants

- **The validator chain is authoritative at dispatch.** Per [ADR-0010](Product/decisions/0010-validator-handler-split.md), per-command legality is the registered `ICommandValidator<TCommand>` chain (see [`06-command-validator.md`](features/actions/archive/06-command-validator.md)). The handler itself owns *resolution*, not legality — though one class may carry both via `ValidatedCommandHandler<,>`.
- **One handler per command type.** DI registration is closed-generic (`ICommandHandler<DealDamageCommand, DamageResult>`). Multiple registrations for the same `TCommand` are an error at container build.
- **Handlers return typed `CommandResult` subtypes.** Effects branch on these.
- **Handlers are the only layer that issues mutators.** Effects sequence commands; mutators write state; handlers bridge by calling `Store.Execute(payload)`.
- **Handlers never dispatch other `GameAction`s.** Sequencing belongs to effects (per [`effects/01-effect.md`](features/effects/archive/01-effect.md)).
- **Handler bodies are atomic from the player-input POV.** No awaits on player decisions inside `Handle`. Player decisions are routed through the request system per [ADR-0011](Product/decisions/0011-effect-orchestration-dispatch-requests.md).
- **`Handle` returns `Task<TResult>`.** Synchronous handlers return `Task.FromResult(...)`; the framework-internal transition handler enqueues follow-ups inside `Handle` and still returns synchronously.
- **`Store` access is via the base.** Handlers read `Store` through the property-injected `GameCommandHandler<,>` base; the retired `IHandlerContext` is no longer a parameter.

## Decisions

Linked: [ADR-0010](Product/decisions/0010-validator-handler-split.md) — `Validate` split out into `ICommandValidator<TCommand>`; `IHandlerContext` retired; `GameCommandHandler<,>` and `ValidatedCommandHandler<,>` bases land. Spike provenance: `Product/spikes/archive/fsm-architecture.md` D10 + D11.

Linked: [ADR-0007](Product/decisions/0007-five-validation-layers.md) — five validation layers principle. Refined by ADR-0010: the "authoritative" layer is the registered validator chain (typically game-shipped legality validators).

## Examples

### Pile Counter handler — validator on the combined base

```csharp
public sealed class MoveCardHandler
    : ValidatedCommandHandler<MoveCardCommand, MoveCardResult>
{
    public override bool Validate(MoveCardCommand move, out string? reason)
    {
        var card     = Store.Catalog.Resolve(move.Card);
        var contents = Store.Get<ZoneContentsState>(move.From);
        if (card is null)                            { reason = "UnknownCard";  return false; }
        if (!contents.Cards.Contains(move.Card))     { reason = "NotInSource";  return false; }
        reason = null;
        return true;
    }

    public override Task<MoveCardResult> Handle(MoveCardCommand move)
    {
        var card   = Store.Catalog.Resolve(move.Card);
        var source = Store.Catalog.Resolve(move.From);
        var dest   = Store.Catalog.Resolve(move.To);
        var number = card.Storage.GetVariable<int>(Vars.Number);

        Store.ExecuteBatch(new object[]
        {
            MoveCardMutation.AtTop(card, source, dest),
            new AdjustPileCounterPayload(move.From, -number),
            new AdjustPileCounterPayload(move.To,   +number),
            new SetLastMovePayload(new LastMove(move.Card, move.From, move.To)),
        });

        return Task.FromResult(new MoveCardResult(move.Card));
    }
}
```

The framework's `SessionGate` and `MatchStateActionGate` (per [`06-command-validator.md`](features/actions/archive/06-command-validator.md)) run before this validator. Game-shipped legality lives in `Validate`. Adding a reactive slice (move counter, undo log) is a new payload + mutator + `Register` line; the handler does not change.

### Split handler — reusable validator

```csharp
// Game-shipped reusable validator: any TargetedCommand requires the actor to own the target.
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

// Handler does only resolution.
public sealed class TapCardHandler : GameCommandHandler<TapCardCommand, TapCardResult>
{
    public override Task<TapCardResult> Handle(TapCardCommand cmd)
    {
        Store.Execute(new SetTappedPayload(cmd.Target, true));
        return Task.FromResult(new TapCardResult(cmd.Target));
    }
}
```

`ActorOwnsTargetValidator` registers against every command derived from `TargetedCommand`. The handler-side class focuses on the write path.

## Open follow-ups

- **`CommandResult` cancellation shape** — flag on the base record vs typed `CancelledResult` subtype. Cosmetic; pin during command-pipeline cleanup.
- **Cross-cutting validators on game-shipped base types** — registering `CommandValidator<TargetedCommand>` and having it cover every derived runtime type is supported by the closed-generic resolution; pattern confirmation lives in [`06-command-validator.md`](features/actions/archive/06-command-validator.md) Open follow-ups.
- **Diagnose surface** — multi-reason rejection feedback affordance; deferred per ADR-0010. Tracked alongside [`06-command-validator.md`](features/actions/archive/06-command-validator.md).

---

*Below: provenance — feature-internal decision folded on 2026-05-08; extracted from `01-command-pipeline.md` on 2026-05-09. The 2026-05-11 ADR-0010 cascade split `Validate` out into [`06-command-validator.md`](features/actions/archive/06-command-validator.md), retired the `IHandlerContext` aggregator (archived at `Product/features/effects/archive/03-handler-context.md`), and reframed the Model around the property-injected `GameCommandHandler<,>` base.*

## Options considered

### Q1 — Validation authority (formerly ADR-0004 — folded; refined by ADR-0010 2026-05-11)

Validation was previously distributed across the rule and the handler with no clear authority. When two layers disagreed, behavior depended on dispatch order.

- **A — Validation lives entirely in the rule.** Rejected — the rule shouldn't have to know command shapes; couples too tightly.
- **B — Validation lives entirely in handlers, with the rule delegating.** Rejected only because gating concerns (pause/end) belong upstream.
- **C — `ICommandHandler.Validate` is authoritative at dispatch; the rule does flow-level gating only.** **Chosen at the time.** Promoted to project-wide [ADR-0007](Product/decisions/0007-five-validation-layers.md) with the full layer breakdown.
- **D — Split `Validate` from `Handle` into a separate `ICommandValidator<TCommand>`; the registered validator chain is authoritative; framework defaults (`SessionGate`, `MatchStateActionGate`) live in the chain alongside game-shipped validators.** **Chosen 2026-05-11 per [ADR-0010](Product/decisions/0010-validator-handler-split.md).** The five-layer principle is unchanged in spirit; the *named* authoritative layer becomes the chain (typically the game-shipped legality validators at the end of the chain) rather than a single handler method.

## Rationale

The validator chain gives one canonical *answer* to "is this command valid right now?" while letting multiple layers contribute. Framework gates (session, match-state-actions) compose with game-shipped legality (cost, ownership, line-of-sight) without overlapping. Handler implementations focus on resolution; reusable legality (creature-tap rule, mana-cost rule) lives once in a `CommandValidator<>` shared across every command type it applies to. The five-layer model from [ADR-0007](Product/decisions/0007-five-validation-layers.md) is preserved — refined into "named layers, each one or more validators in the chain" rather than "named layers, the handler one is special-cased."

## Consequences

- **Enables:** one canonical legality answer composed from multiple validators; framework defaults and game-shipped validators share one registry; reusable legality across commands.
- **Constrains:** validators must be independent (no shared scratchpad); registration order matters and is convention by default.
- **Cascade:** consumed by [`engine/01`](features/engine/archive/01-game-engine.md) (intake gates are now registered validators); [`engine/03`](features/engine/archive/03-game-rules-runtime.md) (rejection-paths refresh); [`presentation/01`](features/presentation/archive/01-match-state-view-model.md) (the per-state-VM presentation layer is the UI-side affordance gate; the framework-side legality is the validator chain).
