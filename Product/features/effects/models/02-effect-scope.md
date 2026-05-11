---
type: model
status: settled
references: [features/effects/models/01-effect.md, features/effects/models/05-pending-effects.md, features/effects/models/06-request-system.md, features/actions/models/01-command-pipeline.md, features/state/state.md, decisions/0011-effect-orchestration-dispatch-requests.md]
tags: [#effects, #model, #scope, #dispatch]
---

# Effect Scope — model

## What it is

`IEffectScope` is the runtime surface an `IEffect` (per [`01-effect.md`](features/effects/models/01-effect.md)) sees inside `Run`. Read-only state access, **a single async `Dispatch<TResult>(GameCommand)` surface routing every kind in the 5-sibling `GameCommand` family**, modifier-entry construction, and the cancel/replace controls that participate in the trigger fold. Per [ADR-0011](Product/decisions/0011-effect-orchestration-dispatch-requests.md) D14, the previously action-only `Dispatch` becomes the canonical surface for `GameAction`, `CompositeAction`, `GameSignal`, `Request<TResponse>`, and `Response` — the router pattern-matches on the runtime type and returns the typed result for that kind.

The scope stays **deliberately narrow**: no mutation channel, no awaiter (retired per ADR-0011), no handler-side services. Effects orchestrate; handlers mutate; mutators write. This Model owns the contract that pins those boundaries on the effect side.

## Contracts

### `IEffectScope`

```csharp
public interface IEffectScope
{
    IStateScope       State     { get; }    // Scaffold; read-only
    Store             Store     { get; }    // for catalog reads only — no Execute
    IModifierFactory  Modifiers { get; }    // deterministic modifier-entry construction
    IActor            Caster    { get; }    // the actor who fired this effect

    // Single async surface — routes every GameCommand subtype.
    Task<TResult> Dispatch<TResult>(GameCommand command);

    // Effect-author controls.
    void Cancel(string? reason = null);
    void Replace(GameAction next);   // before-fold only: swap the intercepted command
}
```

- **`State`** — Scaffold `IStateScope`. Effects read slices to choose what to dispatch next. Read-only by design.
- **`Store`** — exposed for catalog reads (`scope.Store.Catalog.Resolve(ref)`) only. Effects do not call `Execute` / `ExecuteBatch`; mutation goes through `Dispatch`-ed actions whose handlers do the writes.
- **`Modifiers`** — modifier-entry factory. Effects that need to construct a modifier entry to pass into a `GameAction`'s payload (e.g. `ApplyAuraAction { Buff = scope.Modifiers.Create(...) }`) use it here so the deterministic id is allocated by the effect even though the install happens in the action's handler.
- **`Caster`** — the `IActor` that fired the effect (the actor on whose behalf the effect is running). Used for request `Asker` field, source-of-effect attribution, ownership checks.
- **`Dispatch<TResult>(command)`** — single async entry. Routes by runtime type per the table below. The effect awaits each result before continuing.
- **`Cancel`** — explicit early-out for the effect itself (a precondition no longer holds after a prior dispatch). In the trigger context (before-fold), cancels the intercepted command.
- **`Replace`** — before-fold only: signals the pipeline to discard the intercepted command and dispatch `next` as a fresh command through its own fold. Calling `Replace` in an after-fold trigger or in a non-trigger effect context is a no-op (the command already executed, or there is no intercepted command); the pipeline asserts or warns.

### Routing by runtime type

Per [ADR-0011](Product/decisions/0011-effect-orchestration-dispatch-requests.md) D14, `Dispatch` pattern-matches on the command's runtime type:

| Kind | Routing | `TResult` returned |
|---|---|---|
| `GameAction` | validators → `OnBefore<T>` → handler → `OnAfter<T>` → envelope | typed `CommandResult` subtype for the command |
| `CompositeAction` | inline expansion in parent scope (no envelope of its own) | unit (`Task`) |
| `GameSignal` | validators → write to history | unit (`Task`) |
| `Request<TResponse>` | validators → `IRequestService.Open(request)` → return pending `Task<TResponse>` | `TResponse` (typed) — see [`06-request-system.md`](features/effects/models/06-request-system.md) |
| `Response` | validators → `IRequestService.RecordAnswer(id, response)` → resolve the awaited Task | unit (`Task`) |

Validation runs uniformly across all kinds (per [ADR-0010](Product/decisions/0010-validator-handler-split.md)); short-circuit on first reject; one reason returned.

`GameAction` dispatches synchronously-inline from the effect's perspective — the action's full lifecycle (before-fold → handler → after-fold → envelope) runs in the calling Task and resolves before `await` returns. `Request<TResponse>` dispatches **suspend** the effect until the player's `Response` arrives (potentially many drain steps later); the engine progresses other commands while the awaited Task is pending.

### Reads — `IStateScope` (Scaffold)

The state read/write surface is not invented here — we use the `com.scaffold.states` package's existing types. See [`state/state.md`](features/state/state.md) for the canonical mechanic. What this Model pins is which subset effects see:

```csharp
// from Scaffold.States
public interface IStateScope
{
    TState Get<TState>() where TState : BaseState;
    TState Get<TState>(Reference? reference) where TState : BaseState;
    IEnumerable<TState> GetAll<TState>() where TState : BaseState;
}
```

Effects get `IStateScope` (read-only) on `scope.State`. Anything that needs to mutate goes through a dispatched action's handler.

### Replace semantics

`Replace` is meaningful only when an `IEffectScope` is participating in a before-fold trigger (per [`triggers/02-trigger-pipeline.md` §Three before-fold outcomes](features/triggers/models/02-trigger-pipeline.md)):

- The intercepted command's envelope closes with `CommandResult.Replaced`.
- `next` dispatches as a fresh command via `engine.Dispatch` — its own full fold from scratch.
- B's envelope is a sibling, not a child, of A's; `OriginCommandId` threading preserves the causal link.

Outside a before-fold trigger, `Replace` is a no-op or runtime warning.

### Engine-only firing — what's NOT on `IEffectScope`

There is no `scope.RunEffect(instance)` or `scope.FireEffect(...)` method. Per [ADR-0011 lock 1](Product/decisions/0011-effect-orchestration-dispatch-requests.md), the engine is the only layer that calls `IEffectPipeline.Run`. Effects that want to fire a sub-effect push onto `PendingEffectsSlice` (per [`05-pending-effects.md`](features/effects/models/05-pending-effects.md)) — typically by dispatching a small `GameAction` whose handler does the push. The engine drains the pushed effect on a subsequent drain step.

This is a deliberate constraint. Letting `IEffectScope` carry a `RunEffect` would re-introduce the layering violation (two non-engine layers holding pipeline references). The push-and-defer pattern keeps ordering observable in state.

### Cancellation

When the first scenario forces it (match-end during mid-effect await; disconnect; abort), thread `CancellationToken` through `IEffectScope.Dispatch`:

```csharp
// Future shape — implementation pass per oq-effect-cancellation-on-match-end.
public interface IEffectScope
{
    Task<TResult> Dispatch<TResult>(GameCommand command, CancellationToken ct = default);
    // …
}
```

The engine cancels on match-end transition and GCs `RequestsSlice` / `AnswersSlice`. Effects that are mid-`await` see a `TaskCanceledException` (or sentinel response — pending). Resolution path locked; implementation pass tracked as `oq-effect-cancellation-on-match-end`.

## Invariants

- **No mutation channel on `IEffectScope`.** Effects cannot call `Store.Execute`, cannot invoke mutators, cannot write slices. The only state-changing path is dispatching a `GameAction` (whose handler writes) or dispatching a `Request`/`Response` (whose router writes via `IRequestService`).
- **`Store` is exposed for catalog reads only.** `Execute` / `ExecuteBatch` are off-limits to effects even though the concrete `Store` type carries them.
- **`Dispatch` is the single async surface for the 5-sibling `GameCommand` family** (per [ADR-0011](Product/decisions/0011-effect-orchestration-dispatch-requests.md) D14). Routes by runtime type; returns the typed result for that kind. No parallel surfaces for requests / signals / composites.
- **`GameAction` dispatch is synchronous-inline; `Request<TResponse>` dispatch suspends.** From the effect's POV, both are `await`ed. The engine progresses other commands while a `Request` await is pending; it does not pop a parallel pending-effect sibling (strictly serial per drain loop).
- **`Cancel` ends the effect.** It does not roll back already-published envelopes; it just stops `Run` from continuing.
- **`Replace` is before-fold-only.** Calling it elsewhere is a runtime warning / no-op.
- **`Modifiers` allocates deterministic ids.** Constructing modifier ids via `Guid.NewGuid()` is forbidden — it breaks replay and multiplayer determinism.
- **No `RunEffect` / `FireEffect` on the scope.** Per [ADR-0011 lock 1](Product/decisions/0011-effect-orchestration-dispatch-requests.md), the engine is the only layer that calls `IEffectPipeline.Run`. Effects schedule sub-effects by pushing to `PendingEffectsSlice` (via a small intermediary `GameAction`).

## Decisions

Linked: [ADR-0011](Product/decisions/0011-effect-orchestration-dispatch-requests.md) — single async `Dispatch<TResult>(GameCommand)` surface (D14); engine-only firing (D13); routing-by-kind table. Spike provenance: `Product/spikes/fsm-architecture.md` D14.

## Examples

### Reading state to choose what to dispatch

```csharp
public async Task Run(IEffectScope scope)
{
    var hand = scope.State.Get<HandState>(scope.Caster.SelfRef);
    if (hand.Cards.Count == 0)
    {
        scope.Cancel("no cards in hand to discard");
        return;
    }

    await scope.Dispatch(new DiscardAction { Card = hand.Cards[0] });
}
```

### Constructing a modifier on the effect side, applying it on the handler side

```csharp
public async Task Run(IEffectScope scope)
{
    var buff = scope.Modifiers.Create(
        key:      "fireball-charged",
        modifier: new AddDamageBonus(2),
        source:   scope.Caster.SelfRef);

    await scope.Dispatch(new ApplyAuraAction { Target = enemy, Buff = buff });
}
```

The deterministic `ModifierId` is allocated when `Create` is called (engine-owned counter, snapshot-safe); the install happens inside the dispatched action's handler.

### Multi-kind dispatch in one effect

```csharp
public async Task Run(IEffectScope scope)
{
    // 1. Open a player decision (Request<TResponse>) — suspends until answered.
    var pick = await scope.Dispatch(new TargetRequest(
        Id:           RequestId.NewId(),
        Asker:        scope.Caster,
        ValidTargets: scope.Store.QueryCreatures().Select(c => c.Reference).ToList()));

    // 2. Apply damage (GameAction) — synchronously-inline.
    await scope.Dispatch(new DealDamageCommand(pick.Choice, 3));

    // 3. Emit a semantic signal (GameSignal) — history record, may fire triggers.
    await scope.Dispatch(new CardDamagedSignal(pick.Choice, 3));
}
```

All three calls use the same `Dispatch` surface; the router resolves the right routing per runtime type; the effect's source code reads as straight procedural code.

## Open follow-ups

- **`IEffectScope` extension surface** — whether games can add custom services to `scope` for ergonomics (e.g. game-specific helpers) is undesigned. Workaround today is constructor-injecting services into `IEffect` subclasses.
- `oq-effect-cancellation-on-match-end` — thread `CancellationToken` through `Dispatch`. Resolution path locked; implementation pass.
- **Tightening `Dispatch<TResult>(GameCommand)` generics.** The current shape leaves `TResult` loose at the call site. A future ergonomic pass might add overloads or type-mapping constraints (`Dispatch<TRequest, TResponse>` where `TRequest : Request<TResponse>`) so that `Request` dispatches type-check `TResponse` at compile time. Not load-bearing.

---

*Below: provenance — Model originally landed 2026-05-09 (load-bearing extraction pass) with the action-only `Task<TResult> Dispatch<TResult>(GameAction action)` shape; reshaped 2026-05-11 by [ADR-0011](Product/decisions/0011-effect-orchestration-dispatch-requests.md) D14 to the single async surface routing all five `GameCommand` siblings. No options-weighing at this Model's scope; the shape follows from the ADR.*

## Options considered

*n/a — descriptive Model. Choices made at ADR-0011 scope; see linked ADR for option-weighing.*

## Rationale

*n/a — see `## Contracts` and [ADR-0011](Product/decisions/0011-effect-orchestration-dispatch-requests.md).*

## Consequences

*n/a — see [ADR-0011](Product/decisions/0011-effect-orchestration-dispatch-requests.md) `## Consequences`.*
