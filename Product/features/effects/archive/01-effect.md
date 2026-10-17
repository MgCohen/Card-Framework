---
type: model
status: abandoned
archived_on: 2026-10-18
reason: "Written for the old state model (slices, Ref<T>, MatchStateSlice) and never built. Read it for the open questions, not as the contract; the replacement design comes from Product/spikes/framework-roadmap.md."
references: [features/triggers/archive/01-trigger.md, features/actions/archive/01-command-pipeline.md, features/effects/archive/02-effect-scope.md, features/effects/archive/05-pending-effects.md, features/effects/archive/06-request-system.md, features/engine/archive/01-game-engine.md, decisions/0011-effect-orchestration-dispatch-requests.md]
tags: [#effects, #model]
---

> **Archived 2026-10-18.** Written for the old state model (slices, Ref<T>, MatchStateSlice) and never built. Read it for the open questions, not as the contract; the replacement design comes from Product/spikes/framework-roadmap.md.

# Effect — model

## What it is

`IEffect` is the per-instance orchestrator: a first-class type whose `Run(scope) : Task` runs start-to-finish as one async method. Internal `await scope.Dispatch(...)` calls sequence `GameAction`s, `GameSignal`s, `CompositeAction`s, and `Request<TResponse>`s; the C# async runtime holds the continuation across `await`s; there is no state-machine `Step` field, no re-pushing, no engine-side bookkeeping. Per [ADR-0011](Product/decisions/0011-effect-orchestration-dispatch-requests.md), effects fire only via the engine draining [`PendingEffectsSlice`](features/effects/archive/05-pending-effects.md) — cast handlers, trigger reactions, and FSM-driven runs push instances onto the slice; the engine pops and invokes `Run` through the registered `IEffectPipeline`.

```
push paths              PendingEffectsSlice            engine drain
─────────               ──────────────────             ────────────
cast handler ─┐                                        ┌─ pop top
trigger fold ─┼─▶  ImmutableList<EffectInstance>  ─▶  │  catalog.Resolve(instance) → IEffect
EnterCommands ┘                                       │  await effect.Run(scope)
                                                      └─ repeat until empty / paused
```

## Contracts

### The `IEffect` interface

```csharp
public interface IEffect
{
    Task Run(IEffectScope scope);
}
```

- **One method.** Runs start-to-finish. Internal `await scope.Dispatch(...)` for sub-commands, signals, and request answers. The C# async runtime carries the continuation across awaits.
- **No state-machine `Step` field.** Authors write effects as plain async methods; control flow is procedural; readers reconstruct execution from the source code, not from a `Step` enum.
- **Internals are opaque to the framework.** The engine only needs `Run(scope)`; how an effect is authored, parameterized, or composed is the authoring layer's concern (`oq-effect-authoring`).
- **Many per definition.** A card can carry several effects (battlecry + deathrattle + ongoing aura). Each is a separate `IEffect` discriminated by `EffectId` in the source's effect catalog. Subscription lifecycle is the trigger feature's concern.
- **Pre-execution gate retained.** The framework's `IEffectPipeline.Run(instance, scope)` may consult a per-instance `Validate(scope) → bool` on the runnable effect (resolved through `IEffectDefinitionCatalog`); returning `false` skips `Run`. Default passes. Shape pending `oq-effect-authoring`.

```csharp
// Author shape — one async method.
public sealed class LightningBolt : IEffect
{
    public async Task Run(IEffectScope scope)
    {
        var validIds = ComputeTargets(scope.Store, asker: scope.Caster);
        var response = await scope.Dispatch(new TargetRequest(
            Id:           RequestId.NewId(),
            Asker:        scope.Caster,
            ValidTargets: validIds));

        await scope.Dispatch(new DealDamageCommand(response.Choice, 3));
        await scope.Dispatch(new CardDamagedSignal(response.Choice, 3));
    }
}
```

### Engine-only firing

Per [ADR-0011 lock 1](Product/decisions/0011-effect-orchestration-dispatch-requests.md), the engine is the only layer that calls `IEffectPipeline.Run`. Cast handlers, trigger reactions, and runtime code never hold the pipeline reference. Effects start when the engine pops an `EffectInstance` from [`PendingEffectsSlice`](features/effects/archive/05-pending-effects.md) and invokes `Run` through the registered pipeline.

```csharp
// Engine drain loop, simplified:
while (PendingEffectsSlice.Pending.Count > 0 && drainPolicy.AllowsDrain(State))
{
    var instance = PendingEffectsSlice.Pending[^1];               // LIFO default
    Store.Execute(new PopPendingEffectPayload());
    await effectPipeline.Run(instance, scope);                    // ← only call site
}
```

This rolls back the 2026-05-08 effect-pipeline spike Q1 lock (cast handlers and the trigger fold both calling `IEffectPipeline.RunEffect`). The new rule is structural: one place in the system holds the reference.

### The layer hierarchy

| Layer | Job | Can sequence commands? | Async? | Envelope |
|---|---|---|---|---|
| **`IEffect`** | Orchestrate commands, thread results | Yes — only layer that can | Yes — async coroutine | None of its own |
| **`GameAction`** | Apply state changes, return a result | No | No — atomic from player-input POV (per [ADR-0010](Product/decisions/0010-validator-handler-split.md)) | One per action |
| **`CompositeAction`** | Reusable state-change group inside a handler | No | No | Folds into parent |

Only effects sequence commands. A `GameAction` handler may use `CompositeAction`s internally, but it never dispatches other `GameAction`s — that is the effect's responsibility. Handlers are atomic from the player-input POV: they push effects onto `PendingEffectsSlice` and return; the engine drains the effect later.

### Single `Dispatch` surface for the 5-sibling `GameCommand` family

Per [ADR-0011 lock 2](Product/decisions/0011-effect-orchestration-dispatch-requests.md), `IEffectScope.Dispatch` is a single async entry point routing every `GameCommand` subtype. The full surface lives in [`02-effect-scope.md`](features/effects/archive/02-effect-scope.md); from this Model's POV, an effect uses it for:

| Sub-command kind | What the effect does | What it gets back |
|---|---|---|
| `GameAction` | `await scope.Dispatch(new DealDamageCommand(...))` | `CommandResult` subtype |
| `CompositeAction` | `await scope.Dispatch(new MoveCardComposite(...))` | unit |
| `GameSignal` | `await scope.Dispatch(new CardDamagedSignal(...))` | unit |
| `Request<TResponse>` | `await scope.Dispatch(new TargetRequest(...))` | `TResponse` (typed) |
| `Response` | rarely from effects; emitted by VMs | unit |

`Request<TResponse>` is the player-decision channel — see [`06-request-system.md`](features/effects/archive/06-request-system.md). The effect calls `await scope.Dispatch(request)`; the request system opens the request in `RequestsSlice`, returns a pending `Task<TResponse>`; the player's response arrives as a `Response` command, the request system records it and resolves the awaited Task with the typed response.

### Concurrency: strictly serial per drain loop

Only one effect's `Run` is in flight at a time. While an effect awaits a `Response`, the drain loop progresses the response through the command pipeline (resolving the awaited Task), but **does not pop a parallel sibling from `PendingEffectsSlice`**. Order on the slice is order of execution.

### Save / load mid-effect

Not supported by default. A C# `Task` continuation is not serializable. Save/load happens at quiescent points (no `Run` currently in flight; the engine drained `PendingEffectsSlice` to empty *or* paused on `RequestsSlice` non-empty — open requests carry serializable state). If a game later forces mid-effect save/load, that requires state-machine effects — a separate spike (`oq-effect-save-load`).

### Cancellation

When the first scenario forces it (match-end during mid-effect await; disconnect; abort), thread `CancellationToken` through `IEffectScope.Dispatch`. The engine cancels on match-end transition and GCs `RequestsSlice` / `AnswersSlice`. Resolution path locked; implementation pass tracked as `oq-effect-cancellation-on-match-end`.

## Invariants

- **One method per effect.** `Task Run(IEffectScope scope)`. No state-machine `Step` field; no manual re-pushing across drain steps; the async runtime holds continuations.
- **Effects fire only via the engine draining `PendingEffectsSlice`.** Per [ADR-0011](Product/decisions/0011-effect-orchestration-dispatch-requests.md). Cast handlers, triggers, and runtime code never call `IEffectPipeline.Run` directly — they push and return.
- **`scope.Dispatch` is the single async surface for the 5-sibling `GameCommand` family.** Routes by kind; returns the typed result for that kind. See [`02-effect-scope.md`](features/effects/archive/02-effect-scope.md).
- **Effects cannot mutate state directly.** The only state-changing channel is dispatching a `GameAction` whose handler writes via mutators.
- **`IEffect` is a first-class type.** Authored as an interface implementation. Internals (typed bindings, recipe shape) are the authoring layer's concern.
- **Strictly serial per drain loop.** One `Run` in flight at a time. Awaiting a `Response` does not yield draining to a parallel sibling.
- **Save/load only at quiescent points.** Mid-effect `Task` continuations cannot be serialized.

## Decisions

Linked: [ADR-0011](Product/decisions/0011-effect-orchestration-dispatch-requests.md) — async-coroutine `IEffect.Run` (D16); engine-only firing (D13); single async `Dispatch` for the 5-sibling family (D14); `PendingEffectsSlice` as the universal queue (D15); request system for player decisions (D17). Spike provenance: `Product/spikes/archive/fsm-architecture.md` D13 + D16.

## Examples

### Targeted spell (Lightning Bolt) — full mid-effect flow

```csharp
public sealed class LightningBolt : IEffect
{
    public async Task Run(IEffectScope scope)
    {
        var validIds = scope.Store
            .QueryCreaturesAndPlayers()
            .Select(e => e.Reference)
            .ToList();

        var response = await scope.Dispatch(new TargetRequest(
            Id:           RequestId.NewId(),
            Asker:        scope.Caster,
            ValidTargets: validIds));

        await scope.Dispatch(new DealDamageCommand(response.Choice, 3));
        await scope.Dispatch(new CardDamagedSignal(response.Choice, 3));
    }
}

// The cast handler does NOT call the effect pipeline. It pushes onto PendingEffectsSlice.
public sealed class CastLightningBoltHandler
    : GameCommandHandler<CastLightningBoltCommand, CastResult>
{
    public override Task<CastResult> Handle(CastLightningBoltCommand cmd)
    {
        Store.Execute(new SpendManaPayload(cmd.Caster, 1));
        Store.Execute(new MoveCardToZonePayload(cmd.Card, /* stack zone */));
        Store.Execute(new PushPendingEffectPayload(
            new EffectInstance(
                Source:  cmd.Card.SelfRef,
                Id:      EffectIds.LightningBolt,
                Payload: new LightningBoltPayload(cmd.Caster))));
        return Task.FromResult(new CastResult.Accepted());
    }
}
```

The handler is atomic (per [ADR-0010](Product/decisions/0010-validator-handler-split.md)) — no awaits on player decisions. The pushed `EffectInstance` is the deferred work. The engine drains it after the cast handler returns and the FSM permits (priority pass in MTG; immediate in Hearthstone).

### Choose one (Discover) — multi-request shape

```csharp
public sealed class Discover : IEffect
{
    public async Task Run(IEffectScope scope)
    {
        var options = GenerateThreeOptions(scope.Store, scope.Caster);
        var choice = await scope.Dispatch(new ChooseOneRequest(
            Id:      RequestId.NewId(),
            Asker:   scope.Caster,
            Options: options));

        await scope.Dispatch(new ResolveDiscoverChoiceCommand(
            scope.Caster, choice.SelectedOption));
    }
}
```

The author writes one async method. `ChooseOneRequest` is a game-shipped `Request<ChooseOneResponse>` pair. Same pattern; same drain pause; same Task resolution.

### Trigger reaction firing an effect

A `Trigger<T>` whose reaction is to fire an effect pushes onto `PendingEffectsSlice` — same channel as cast handlers, not a direct `IEffectPipeline.Run` call:

```csharp
public sealed class DrawOnDamage : Trigger<DealDamageCommand>
{
    public override Task Execute(DealDamageCommand cmd, IEffectScope scope)
    {
        scope.Store.Execute(new PushPendingEffectPayload(
            new EffectInstance(
                Source:  scope.Self,
                Id:      EffectIds.DrawCard,
                Payload: new DrawCardPayload(cmd.Target.Owner, count: 1))));
        return Task.CompletedTask;
    }
}
```

The trigger's `Execute` body runs synchronously (or with minimal awaits); the actual draw-card effect fires later when the engine drains.

## Open follow-ups

- `oq-effect-authoring` — how a `CardDefinition` holds the *recipe* that constructs an `IEffect` from `(Source, EffectId, Payload)` at fire time. Authoring vocabulary (graphs / code / hybrid) is the open design surface.
- `oq-effect-cancellation-on-match-end` — thread `CancellationToken` through `IEffectScope.Dispatch`; engine cancels on match-end and GCs in-flight pending. Implementation pass; resolution path locked.
- `oq-effect-save-load` — async-coroutine effects cannot be serialized mid-`await`. Save/load is supported at quiescent points; mid-effect save/load would force state-machine effects — separate spike.
- **Effect per-instance state** — where ongoing-effect counters live (Effect object, `EntityState`, dedicated slice). Left to ongoing-effects + authoring follow-up. [`entities/05-attachment.md`](features/entities/archive/05-attachment.md) settles the attachment-entity shape; this Model only pins that effects do not get a hidden mutation channel.
- **`EffectId` shape.** Currently treated as opaque typed identifier. Pin during authoring design.

---

*Below: provenance — Model originally landed 2026-05-09 (load-bearing extraction pass) with the per-instance `Effect : Validate + Execute` shape; reshaped 2026-05-11 by [ADR-0011](Product/decisions/0011-effect-orchestration-dispatch-requests.md) to the async-coroutine `IEffect.Run` shape. Q1 below is the original async-layer-hierarchy decision; it survives in spirit (each kind of pause has its own surface) — the refinement is that the per-effect Task is the *single* async unit per drain pop, not a multi-step state-machine.*

## Options considered

### Q1 — Async layer hierarchy

Async behavior in a card game spans several distinct kinds: animation handoff (presentation), decision wait (player input), simulation pause (rule), and resolver yield (engine). Treating all of them with one `Task` returned from a handler smears responsibilities.

- **A — Single async layer (`Task<Result>` everywhere).** Rejected — conflates animation, decision, and simulation pauses.
- **B — No async.** Rejected — real games need decisions and animation handoff.
- **C — Layered hierarchy.** Presentation-async (animation), decision-async (player input, originally via the awaiter; now via the request system per [ADR-0011](Product/decisions/0011-effect-orchestration-dispatch-requests.md) D17), engine-async (drain loop). Each layer has its own continuation/handle. **Chosen.**

### Q2 — Single-method async coroutine vs state-machine effect (per ADR-0011 D16)

The 2026-05-08 effect-pipeline spike shaped effects as multi-step records with a `Step` field; each step ran, pushed itself back onto a queue, and the engine advanced step-by-step on subsequent drain pops. This made manual continuation bookkeeping load-bearing.

- **A — State-machine effect.** Step field, manual re-push, engine-side step bookkeeping. Rejected — the C# async runtime can carry continuations across `await`s natively; manual bookkeeping multiplies authoring cost for no incremental benefit.
- **B — Single async coroutine method (`Task Run(scope)`).** Chosen per [ADR-0011](Product/decisions/0011-effect-orchestration-dispatch-requests.md) D16. The trade is save/load mid-effect not being supported by default (`Task` continuations aren't serializable); if a game later forces mid-effect save/load, that becomes a separate spike (`oq-effect-save-load`).

## Rationale

Layered async means each kind of pause has its own surface: presentation owns animation handoff, requests carry player decisions through the request system (per [ADR-0011](Product/decisions/0011-effect-orchestration-dispatch-requests.md) D17), the engine yields between drain steps. Effects compose across these layers via `IEffectScope`. The single-async-coroutine shape (Q2) keeps authoring trivial — one method, plain procedural code — and pushes save/load complexity to the boundary where it actually appears (a real game that forces mid-effect save/load).

## Consequences

- **Enables** clean handoffs and per-layer testability; deterministic envelope publishing per dispatched action; client visibility of fully-committed state at all times; readable effect source code (no `Step` enums, no re-pushing).
- **Constrains** handlers and effects to declare which layer they're awaiting; only effects can sequence commands; effect chains pause the engine's pending-effect drain for their full duration (acceptable trade-off; game-specific tuning via runtime `Evaluate` overrides).
- **Constrains** save/load to quiescent points (no in-flight `Run`); mid-effect save/load is a separate spike if a game forces it.
