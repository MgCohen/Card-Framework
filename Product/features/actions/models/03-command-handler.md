---
type: model
status: settled
references: [features/actions/models/01-command-pipeline.md, features/actions/models/04-history-envelope.md, features/state/state.md, features/engine/models/04-gamestate-slices.md, decisions/0007-five-validation-layers.md]
tags: [#actions, #model, #handler]
---

# Command Handler — model

## What it is

`ICommandHandler<TCommand, TResult>` is the per-command resolver. `Validate(command, state) → bool` is the **authoritative legality check** at dispatch; `Handle(command, ctx) → Task<TResult>` reads state, decides outcome, calls `ctx.Store.Execute(payload)` to fan out to mutators, and returns a typed `CommandResult`.

```text
pipeline.Validate
  → handler.Validate(command, state)        // authoritative legality
pipeline.Execute (drain)
  → handler.Handle(command, ctx)            // game logic
       → ctx.Store.Execute(payload)         // fan out to mutators (state/state.md)
       → return new TResult(...)
```

## Contracts

### `ICommandHandler<TCommand, TResult>`

```csharp
public interface ICommandHandler<TCommand, TResult>
    where TCommand : GameCommand
    where TResult  : CommandResult
{
    bool Validate(TCommand command, IStateScope state, out string? reason);
    Task<TResult> Handle(TCommand command, IHandlerContext ctx);
}

// Game authors typically extend the typed abstract base
public abstract class CommandHandler<TCommand, TResult> : ICommandHandler { ... }
```

Each `GameCommand` type has exactly one registered handler. Most handlers complete synchronously and return `Task.FromResult(...)`; only decision handlers genuinely await (per [`effects/04-command-awaiter.md`](features/effects/models/04-command-awaiter.md)).

`handler.Validate` is one of the five validation layers ([ADR-0007](Product/decisions/0007-five-validation-layers.md)) — and the **authoritative** one.

### Writes — through `ctx.Store.Execute(payload)`

Handlers do not write state inline. They call `ctx.Store.Execute(payload)`; Scaffold's registry fans out to every mutator registered for `TPayload` and commits atomically. The `Store` / `Mutator<TState, TPayload>` mechanic is owned by [`state/state.md`](features/state/state.md).

```csharp
public Task<MoveCardResult> Handle(MoveCardCommand cmd, IHandlerContext ctx)
{
    ctx.Store.Execute(new MoveCardPayload(cmd.Card, cmd.From, cmd.To));
    return Task.FromResult(new MoveCardResult(cmd.Card, cmd.From, cmd.To));
}
```

Payloads are dedicated record types (`MoveCardPayload`), not commands.

### `CommandResult`

Structured outcome from a handler. Subtypes per command. Examples: approved/rejected/cancelled flag, actual destination, was-redirected, damage amount, shield-vs-health damage, drawn instance ID, created instance IDs, opened decision ID.

Used for: branching inside effects (`if (damage.IsCancelled) return;`), trigger/reaction conditions, UI feedback, history envelope contents, tests. Populates `HistoryEnvelope.Result` (per [`04-history-envelope.md`](features/actions/models/04-history-envelope.md)).

Cancellation shape (flag on the base record vs. typed `CancelledResult` subtype) is a small decision deferred to command-pipeline cleanup; either works.

### `IHandlerContext`

The handler's runtime surface — `Store` (read + execute mutators + catalog), `Actions` (snapshot writes), `Resolver` (decision awaits), `Modifiers`. Defined in [`effects/03-handler-context.md`](features/effects/models/03-handler-context.md).

## Invariants

- **`handler.Validate` is authoritative at dispatch.** Pre-template eligibility, AffordanceMap, IntentMapper, and the engine lifecycle gate are advisory or specialized; the handler is final.
- **One handler per command type.** DI registration is closed-generic (`ICommandHandler<DealDamageCommand, DamageResult>`). Multiple registrations for the same `TCommand` are an error at container build.
- **Handlers return typed `CommandResult` subtypes.** Effects branch on these.
- **Handlers are the only layer that issues mutators.** Effects sequence commands; mutators write state; handlers bridge by calling `ctx.Store.Execute(payload)`.
- **Handlers never dispatch other `GameAction`s.** Sequencing belongs to effects (per [`effects/01-effect.md`](features/effects/models/01-effect.md)).
- **`Handle` returns `Task<TResult>`.** Synchronous handlers return `Task.FromResult(...)`; only decision handlers genuinely await.

## Decisions

Linked: [`Product/decisions/0007-five-validation-layers.md`](Product/decisions/0007-five-validation-layers.md) — five validation layers, with `handler.Validate` authoritative on the engine side.

## Examples

### Pile Counter handler

```csharp
public sealed class MoveCardHandler : ICommandHandler<MoveCardCommand, MoveCardResult>
{
    public bool Validate(MoveCardCommand move, IStateScope state, out string? reason)
    {
        // ... existing legality checks (top-only, source has card, target exists)
        reason = null;
        return true;
    }

    public Task<MoveCardResult> Handle(MoveCardCommand move, IHandlerContext ctx)
    {
        var card   = ctx.Store.Catalog.Resolve(move.Card);
        var source = ctx.Store.Catalog.Resolve(move.From);
        var dest   = ctx.Store.Catalog.Resolve(move.To);
        var number = card.Storage.GetVariable<int>(Vars.Number);

        ctx.Store.ExecuteBatch(new object[]
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

The handler does not enumerate which mutators run; the registry routes by payload type. Adding a third reactive slice (move counter, undo log) is a new payload + mutator + `Register` line; the handler does not change.

## Open follow-ups

- **`CommandResult` cancellation shape** — flag on the base record vs typed `CancelledResult` subtype. Cosmetic; pin during command-pipeline cleanup.
- **`IHandlerContext` vs constructor-injected services on handlers** — context aggregation vs explicit per-handler service declaration. Revisit when a second game stresses the surface.

---

*Below: provenance — feature-internal decision folded on 2026-05-08; extracted from `01-command-pipeline.md` on 2026-05-09.*

## Options considered

### Q1 — Validation authority in the handler (formerly ADR-0004 — folded)

Validation was previously distributed across the rule and the handler with no clear authority. When two layers disagreed, behavior depended on dispatch order.

- **A — Validation lives entirely in the rule.** Rejected — the rule shouldn't have to know command shapes; couples too tightly.
- **B — Validation lives entirely in handlers, with the rule delegating.** Rejected only because gating concerns (pause/end) belong upstream.
- **C — `ICommandHandler.Validate` is authoritative at dispatch; the rule does flow-level gating only.** **Chosen.** Other layers (per-template eligibility, AffordanceMap, IntentMapper, engine lifecycle gate) are advisory or specialized; the handler is final. Promoted to project-wide [ADR-0007](Product/decisions/0007-five-validation-layers.md) with the full layer breakdown.

## Rationale

`handler.Validate`-is-authoritative gives one canonical answer to "is this command valid right now?" — extended in ADR-0007 into the explicit five-layer model. Handler implementations carry the bulk of validation logic; this is correct because handlers already know command shapes and need state context.

## Consequences

- **Enables:** one canonical legality answer; simple advisory-vs-authoritative split across the five layers.
- **Constrains:** handler implementations carry the bulk of validation logic; the rule cannot reject by body at intake.
- **Cascade:** consumed by `engine/02-gamerules-contract.md` (lifecycle gate is the flow-level layer); `presentation/01-presentation-loop.md` (`AffordanceMap` is the UI-side advisory layer).
