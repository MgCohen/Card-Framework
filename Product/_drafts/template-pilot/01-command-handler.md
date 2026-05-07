---
type: model
status: draft
references: [features/commands/commands.md, features/triggers/models/01-command-types.md, features/state/models/01-mutator.md]
tags: [#commands, #handler]
---

# Command Handler

## What it is

The contract every game-command handler implements. One handler per `GameCommand` type, registered in DI, owns the resolution path: read state, decide outcome, invoke mutators to apply the change, return a typed result. Handlers are the only layer that bridges from intent-shaped commands to state-shaped mutations.

```csharp
public interface ICommandHandler<TCommand, TResult>
    where TCommand : GameCommand
{
    bool    Validate(TCommand command, IStateScope state, out string? reason);
    TResult Execute (TCommand command, IHandlerContext ctx);
}
```

## Contracts

Generic per `(TCommand, TResult)` pair. `TCommand` extends the framework's `GameCommand` base (owned by `triggers/`); `TResult` is the handler's typed return — game-defined records like `DamageResult`, `MoveResult`, or `Unit` for void operations.

`IHandlerContext` is a capability-bag passed to `Execute` — `ctx.Store` (state read), `ctx.Mutate` (mutator invocation), `ctx.History` (entry emission). Contract owned by `effects/04-effect-flow`.

**`Validate(command, state, out reason) → bool`**
- Side-effect-free. Reads state only, returns `false + reason` for rejection (e.g., "actor frozen", "not enough mana").
- Called by `Pipeline.Validate` at intake. Failed validation produces no envelope.
- May be called repeatedly by snapshot pre-validation — must be cheap.

**`Execute(command, ctx) → TResult`**
- The only mutating path. Reads state via `ctx.Store`, computes outcome, calls `ctx.Mutate.Invoke<TPayload>(payload)` for each state change.
- Return value populates `HistoryEnvelope.Result`.
- May *not* dispatch other `GameCommand`s — that's an effect's job.
- May *not* await player input — exception: decision-handlers (see `oq-decision-handler-shape` below).

**Forbidden:**
- Direct state mutation (must go through mutators).
- Sequencing other commands (must be done by an effect).
- Side effects in `Validate`.

## Invariants

- One handler per `TCommand` type. DI registration enforces this; multiple registrations are an error at container build.
- `Validate` and `Execute` see consistent state — the engine guarantees no concurrent mutation between them within a single command flow.
- Handler return type matches the `TResult` the pipeline awaits; `Pipeline.Execute<TCommand, TResult>` is typed end-to-end.

(Cross-Model invariants — single intake, one envelope per action, mutator-only writes — are pinned in `commands.md ## Invariants`.)

## Decisions

- `Product/decisions/0005-objects-over-ids.md` — handlers receive `Card`, `Zone`, `Actor` object handles, not raw `InstanceId`s, in command payloads.

## Examples

`DealDamageHandler : ICommandHandler<DealDamageCommand, DamageResult>`:

```csharp
public bool Validate(DealDamageCommand cmd, IStateScope state, out string? reason)
{
    if (cmd.Amount <= 0) { reason = "non-positive damage"; return false; }
    if (!state.Get<EntityVariableState>(cmd.Target.Id).Has(Vars.Health))
        { reason = "target has no health"; return false; }
    reason = null;
    return true;
}

public DamageResult Execute(DealDamageCommand cmd, IHandlerContext ctx)
{
    var target = ctx.Store.Get<EntityVariableState>(cmd.Target.Id);
    var shield = target.Get<int>(Vars.Shield);
    var hp     = target.Get<int>(Vars.Health);

    var shieldHit = Math.Min(cmd.Amount, shield);
    var hpHit     = cmd.Amount - shieldHit;

    if (shieldHit > 0)
        ctx.Mutate.Invoke(new ReduceShield(cmd.Target.Id, shieldHit));
    if (hpHit > 0)
        ctx.Mutate.Invoke(new ReduceHealth(cmd.Target.Id, hpHit));

    var killed = hp - hpHit <= 0;
    if (killed)
        ctx.Mutate.Invoke(new MarkDestroyed(cmd.Target.Id));

    return new DamageResult(shieldHit, hpHit, killed);
}
```

The handler reads (`Store.Get`), computes (split shield/health), mutates (`Mutate.Invoke`), returns. Three mutators called in one `Execute`; envelope captures all three changes plus the result.

## Open follow-ups

- **Decision handlers.** Some handlers must await player input (target-picking, modal choices). Treat as a special sub-interface, a marker, or just `Execute` returning an awaitable? Captured as `oq-decision-handler-shape`.

---

*Below: provenance.*

## Options considered

- **A** — Single non-generic `ICommandHandler` returning `object?`. *Rejected:* loses type safety; effects awaiting handler results need casts; snapshot pre-validation can't type the response.
- **B** — Class-per-command, no shared interface (`DealDamageHandler` is just a class). *Rejected:* no DI registration story; pipeline can't dispatch generically.
- **C — Generic `ICommandHandler<TCommand, TResult>`.** *Chosen.*

## Rationale

The generic shape gives effects `await scope.Dispatch<TCmd, TRes>(cmd) → TRes` without runtime casts and matches the typed `ActionTemplate<TCommand>` shape from `presentation/`. DI's open-generic resolution covers registration cleanly.

## Consequences

- **Enables:** typed effect chaining; typed snapshot mapping; type-checked mutator-invocation patterns inside `Execute`.
- **Constrains:** every command type must have a paired result type (use `Unit` for void operations); multi-result commands need an ad-hoc record.
