---
type: model
status: settled
references: [features/effects/models/04-effect-scope.md, features/effects/models/06-command-awaiter.md, features/actions/models/03-command-handler.md, features/state/state.md, features/entities/models/02-runtime-surface.md]
tags: [#effects, #model, #handler-context]
---

# Handler Context — model

## What it is

`IHandlerContext` is the runtime surface a `GameAction` handler (per [`actions/03-command-handler.md`](features/actions/models/03-command-handler.md)) sees inside `Handle`. Wider than `IEffectScope`: handlers may mutate state via `Store.Execute(payload)`, write the available-actions snapshot, register decision awaits, and construct modifier entries.

The contrast pins the layer separation: effects orchestrate (read + dispatch), handlers execute (read + mutate + await), mutators write.

## Contracts

### `IHandlerContext`

```csharp
public interface IHandlerContext
{
    Store              Store     { get; }   // Scaffold; read + execute mutators + catalog
    IAvailableActions  Actions   { get; }   // mid-execution snapshot writes
    ICommandAwaiter    Resolver  { get; }   // register and resolve decision awaits
    IModifierFactory   Modifiers { get; }   // deterministic modifier-entry construction
}
```

- **`Store`** — concrete Scaffold `Store`. Read via `ctx.Store.Get<TState>()` (inherited from `IStateScope`); write via `ctx.Store.Execute(payload)`; resolve handles via `ctx.Store.Catalog.Resolve(ref)`; spawn entities via `ctx.Store.Spawn<TDef>(def)` (the `StoreEntities.Spawn` extension). Scaffold does not expose the execute methods on `IStoreScope`; the concrete class is the surface.
- **`Actions`** — `IAvailableActions` service. Decision handlers and effects-mid-execution use `ctx.Actions.Set(snapshot)` to publish a fresh `AvailableActionsSnapshot`. See [`engine/03-snapshot-pipeline.md`](features/engine/models/03-snapshot-pipeline.md) for production semantics.
- **`Resolver`** — `ICommandAwaiter`. Decision handlers call `ctx.Resolver.Wait<TCommand>()` to suspend until a matching command arrives (per [`06-command-awaiter.md`](features/effects/models/06-command-awaiter.md)).
- **`Modifiers`** — modifier factory. `Create(key, modifier, source)` returns a fully-populated `ActiveModifier` with a deterministic `ModifierId` and optional source baked in. The id comes from an engine-owned counter (snapshotted, restored on `LoadSnapshot`). Constructing modifier ids ad-hoc via `Guid.NewGuid()` is forbidden in handler / framework code — it breaks replay and multiplayer determinism.

### Writes — through `ctx.Store.Execute(payload)`

The `Store` / `Mutator<TState, TPayload>` mechanic (payload-keyed dispatch, fan-out, atomic commit, sibling reads via `IStateScope`) is owned by [`state/state.md`](features/state/state.md). Handler-side discipline:

- **Handlers are the only mutator caller.** Effects do not see `Store.Execute`.
- **No `Mutate` helper on the context.** The handler issues `ctx.Store.Execute(payload)` / `ctx.Store.ExecuteBatch(...)` calls directly.
- **Payloads are dedicated types, not commands.** `LastMovePayload`, never `MoveCardCommand`.
- **`TPayload` carries `Reference` / `Ref<T>` directly** — payloads are boundary-correct per [ADR-0005](Product/decisions/0005-objects-over-ids.md).

### Entity-variable mutations

For modifier / variable / structural mutations against entity slices, prefer the `IEntityVariableStorage` surface on a handle obtained via `ctx.Store.Catalog.Resolve(ref)`. Build modifier entries through the `Modifiers` factory and pass them to `card.Storage.AddModifier(entry)`. Raw `ctx.Store.Execute(payload)` remains the documented fallback for batch authoring, history-replay tooling, and refless paths. Full handler-side surface in [`entities/02-runtime-surface.md`](features/entities/models/02-runtime-surface.md).

### Spawn pathway

`ctx.Store.Spawn<TDef>(def)` (the `StoreEntities.Spawn` extension) allocates a deterministic `Ref<EntityInstance<TDef>>`, registers an empty `EntityState` slice, constructs the handle with `StoreVariableStorage`, and registers it in `ctx.Store.Catalog`. Returns `(instance, ref)`. Typed subclass spawn helpers (`Card.Spawn`) wrap this. See [`entities/02-runtime-surface.md`](features/entities/models/02-runtime-surface.md).

## Invariants

- **`IHandlerContext` is wider than `IEffectScope`.** Handlers see `Store` (full surface), `Actions`, `Resolver`. Effects see read-only state + `Dispatch` + `Cancel` / `Replace`.
- **No mutate-helper surface on the context.** Handlers go through `ctx.Store.Execute(payload)` directly.
- **Decision-await belongs to handlers, not effects.** Effects observe decisions only through the `CommandResult` returned by `scope.Dispatch`.
- **Snapshot writes are mid-execution.** Decision handlers and effects-during-execution call `ctx.Actions.Set(...)` to publish a fresh snapshot; the framework does not auto-publish on every drain step (per ADR-0006).
- **Modifier ids come from the factory.** Ad-hoc `Guid.NewGuid()` is forbidden.

## Decisions

*n/a — no elevated ADR.* The context shape is the consumer side of layer separation (Effect / GameAction / CompositeAction), pinned in [`01-effect.md`](features/effects/models/01-effect.md).

## Examples

### Decision handler — open snapshot, await player

```csharp
public sealed class RequestTargetHandler
    : ICommandHandler<RequestTargetAction, RequestTargetResult>
{
    public bool Validate(RequestTargetAction a, IStateScope state, out string? r)
    { r = null; return true; }

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

### Mutating handler — atomic batch with modifier

```csharp
public Task<ApplyAuraResult> Handle(ApplyAuraAction cmd, IHandlerContext ctx)
{
    var target = ctx.Store.Catalog.Resolve(cmd.Target);
    target.Storage.AddModifier(cmd.Buff);  // Buff was constructed via scope.Modifiers.Create
    return Task.FromResult(new ApplyAuraResult(cmd.Target));
}
```

## Open follow-ups

- **`IHandlerContext` vs constructor-injected services on handlers.** Aggregating `Store`, `Actions`, `Resolver` into a context type is one option; constructor-injecting them per handler (and dropping the context entirely) is another. The latter declares each handler's needs explicitly and is friendlier to DI, but multiplies wiring cost as services grow. Decision is undesigned — revisit when a second game stresses the surface or when handler count crosses a threshold that makes the cost visible.
