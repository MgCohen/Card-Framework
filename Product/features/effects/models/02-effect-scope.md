---
type: model
status: settled
references: [features/effects/models/01-effect.md, features/effects/models/03-handler-context.md, features/state/state.md]
tags: [#effects, #model, #scope]
---

# Effect Scope — model

## What it is

`IEffectScope` is the runtime surface an `Effect` (per [`01-effect.md`](features/effects/models/01-effect.md)) sees inside `Execute`. Read-only state access, immediate-inline dispatch, modifier-entry construction, and the cancel/replace controls that participate in the trigger fold.

The scope is **deliberately narrow**: no mutation channel, no awaiter, no handler-side services. Effects orchestrate; handlers mutate; mutators write. This Model owns the contract that pins those boundaries on the effect side.

## Contracts

### `IEffectScope`

```csharp
public interface IEffectScope
{
    IStateScope       State     { get; }    // Scaffold; read-only
    Store             Store     { get; }    // for catalog reads only — no Execute
    IModifierFactory  Modifiers { get; }    // deterministic modifier-entry construction

    Task<TResult> Dispatch<TResult>(GameAction action) where TResult : CommandResult;
    void Cancel(string? reason = null);
    void Replace(GameAction next);   // before-fold only: swap the intercepted command
}
```

- **`State`** — Scaffold `IStateScope`. Effects read slices to choose what to dispatch next. Read-only by design.
- **`Store`** — exposed for catalog reads (`scope.Store.Catalog.Resolve(ref)`) only. Effects do not call `Execute` / `ExecuteBatch`; mutation goes through `Dispatch`-ed actions whose handlers do the writes. Use `scope.Store.Catalog.Resolve(cardRef)` or a typed registry to obtain handles for variable reads (`card.Storage.GetVariable<int>(Vars.Attack)`).
- **`Modifiers`** — modifier-entry factory, same surface as on `IHandlerContext` (per [`03-handler-context.md`](features/effects/models/03-handler-context.md)). Effects that need to construct a modifier entry to pass into a `GameAction`'s payload (e.g. `ApplyAuraAction { Buff = scope.Modifiers.Create(...) }`) use it here so the deterministic id is allocated by the effect even though the install happens in the action's handler.
- **`Dispatch`** — runs the action immediately and inline (before-fold → handler → after-fold → envelope published) per [`01-effect.md` §Two dispatch paths](features/effects/models/01-effect.md). The effect awaits each result before continuing.
- **`Cancel`** — explicit early-out for the effect itself (the effect has decided it cannot continue, e.g. a precondition no longer holds after a prior dispatch). In the trigger context (before-fold), cancels the intercepted command. Distinct from the before-fold cancelling an individual `GameAction` via result.
- **`Replace`** — before-fold only: signals the pipeline to discard the intercepted command and dispatch `next` as a fresh command through its own fold. Calling `Replace` in an after-fold trigger is a no-op (the command already executed); the pipeline implementation asserts or warns.

### Reads — `IStateScope` (Scaffold)

The state read/write surface is **not invented here** — we use the `com.scaffold.states` package's existing types. See [`state/state.md`](features/state/state.md) for the canonical mechanic. What this Model pins is which subset effects see:

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

`Replace` is meaningful only when an `IEffectScope` is participating in a before-fold trigger (per [`triggers/01-trigger-fold.md` §Before-fold outcomes](features/triggers/models/01-trigger-fold.md)):

- The intercepted command's envelope closes with `CommandResult.Replaced`.
- `next` dispatches as a fresh command via `engine.Dispatch` — its own full fold from scratch.
- B's envelope is a sibling, not a child, of A's; `OriginCommandId` threading preserves the causal link.

Outside a before-fold trigger, `Replace` is a no-op or runtime warning.

## Invariants

- **No mutation channel on `IEffectScope`.** Effects cannot call `Store.Execute`, cannot invoke mutators, cannot write slices. The only state-changing path is dispatching a `GameAction`.
- **`Store` is exposed for catalog reads only.** `Execute` / `ExecuteBatch` are off-limits to effects even though the concrete `Store` type carries them.
- **`Dispatch` is immediate-inline.** It awaits the full lifecycle of `action`. Effects cannot use `Dispatch` for fire-and-forget; that's `engine.Dispatch`'s job (used by trigger reactions and evaluator follow-ups).
- **`Cancel` ends the effect.** It does not roll back already-published envelopes; it just stops `Execute` from continuing.
- **`Replace` is before-fold-only.** Calling it elsewhere is a runtime warning / no-op.
- **`Modifiers` allocates deterministic ids.** Constructing modifier ids via `Guid.NewGuid()` is forbidden — it breaks replay and multiplayer determinism.

## Decisions

*n/a — no elevated ADR.* The scope shape is a direct consequence of the layer hierarchy (per [`01-effect.md` Q1](features/effects/models/01-effect.md)): effects orchestrate, handlers mutate, mutators write.

## Examples

### Reading state to decide what to dispatch

```csharp
public override async Task Execute(IEffectScope scope)
{
    var hand = scope.State.Get<HandState>(player.SelfRef);
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
public override async Task Execute(IEffectScope scope)
{
    var buff = scope.Modifiers.Create(
        key: "fireball-charged",
        modifier: new AddDamageBonus(2),
        source: self.SelfRef);

    await scope.Dispatch(new ApplyAuraAction { Target = enemy, Buff = buff });
}
```

The deterministic `ModifierId` is allocated when `Create` is called (engine-owned counter, snapshot-safe); the install happens inside the dispatched action's handler.

## Open follow-ups

- **`IEffectScope` extension surface** — whether games can add custom services to `scope` for ergonomics (e.g. game-specific helpers) is undesigned. Workaround today is constructor-injecting services into `Effect` subclasses.
