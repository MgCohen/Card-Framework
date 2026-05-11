---
type: adr
status: accepted
references: [features/effects/models/01-effect.md, features/effects/models/02-effect-scope.md, features/effects/models/05-pending-effects.md, features/effects/models/06-request-system.md, features/actions/models/01-command-pipeline.md, features/engine/models/01-game-engine.md, features/engine/models/03-game-rules-runtime.md, spikes/archive/fsm-architecture.md, decisions/0006-two-pipeline-architecture.md, decisions/0009-engine-as-fsm.md, decisions/0010-validator-handler-split.md]
tags: [#adr, #effects, #engine, #dispatch, #requests, #fsm]
---

# ADR-0011 — Effect orchestration; unified `Dispatch`; `PendingEffectsSlice`; async-coroutine effects; request system

Date: 2026-05-11 (drafted from `Product/spikes/archive/fsm-architecture.md` D13–D17; sub-ADR C of the FSM-arch cascade following ADR-0009 + ADR-0010)
Context-docs: `Product/features/effects/models/01-effect.md`, `Product/features/effects/models/02-effect-scope.md`, new Models `effects/05-pending-effects.md`, `effects/06-request-system.md`, `Product/features/actions/models/01-command-pipeline.md`, `Product/features/engine/models/01-game-engine.md`, `Product/features/engine/models/03-game-rules-runtime.md`
Proposal: `Product/spikes/archive/fsm-architecture.md`

## Context

After ADR-0009 (engine IS the FSM) and ADR-0010 (validator/handler split), five intertwined questions remained open at the effect-orchestration layer. The pre-ADR-0011 surface had:

1. **Effect-pipeline ownership smeared across layers.** The earlier `spikes/effect-pipeline.md` Q1 lock had cast handlers and the trigger fold *both* calling `IEffectPipeline.RunEffect` directly. That meant two layers held references to the effect pipeline and could schedule effects without going through a single canonical channel — a layering violation that made effect ordering hard to reason about and replay hard to reconstruct deterministically.
2. **No first-class "pending effects" shape.** When a cast handler created an effect to fire later (Magic stack semantics, Hearthstone trigger queue, anywhere effects defer past their cast handler), the framework had no canonical container. Each game shipped its own queue/zone slice; cross-cutting tooling (replay, debugger) had nothing to inspect.
3. **Effects as state-machines.** The 2026-05-08 pass left effects shaped as multi-step records that needed re-pushing onto a queue to advance — a `Step` field, manual state-machine bookkeeping, "what step is this effect on?" tracking. Authors writing a multi-step effect (`pick target → deal damage → emit signal`) had to manage the bookkeeping explicitly; readers of an effect's source code reconstructed control flow from `Step` numbers and re-push points.
4. **Dispatch surface for effects was action-only.** `scope.Dispatch(action)` took a `GameAction` and returned `Task<TResult>`; there was no canonical channel for an effect to dispatch a `GameSignal`, expand a `CompositeAction`, or — most critically — open a request and `await` the player's response. Decision routing went through `ICommandAwaiter` on `IHandlerContext` instead (the pattern documented in the now-retired `effects/04-command-awaiter.md`), which forced handler bodies to be async and player-input-aware (the opposite of what ADR-0010 made structural).
5. **No state-linked request system.** Player decisions mid-effect (target picker, choose-one prompt, split-damage allocation) ran through `ICommandAwaiter` — type-only routing on the handler's await. That mechanism had no `RequestId`, no inspectable queue of open requests, no replay shape (a saved match couldn't be re-played by re-dispatching the recorded responses), no separation between "still asking" and "answered, awaiting consumer." It was the right primitive for the simpler shape but stopped scaling once requests started to compose (concurrent multi-actor decisions, replay determinism, network round-trips).

The 2026-05-10 open-questions walk on `Product/spikes/archive/fsm-architecture.md` settled the structural direction across D13–D17. This ADR ratifies all five locks and cascades them into the canonical Models.

## Options considered

- **A — Keep the 2026-05-08 Q1 lock; let cast handlers and the trigger fold each call `IEffectPipeline.RunEffect`; iterate on effect-as-state-machine + a richer `ICommandAwaiter`.** Rejected — the layering violation is structural (two non-engine layers holding the pipeline reference), the effect-as-state-machine shape forces authors into bookkeeping work that the C# async runtime can do for free, and `ICommandAwaiter` can't grow into a replayable request shape without breaking its single-actor type-only contract.
- **B — Engine-only effect firing + universal `PendingEffectsSlice`; keep effects as state-machine `Step` records; keep `ICommandAwaiter`.** Rejected — addresses the layering and queue questions but leaves effects as bookkeeping work and player-input routing in handler bodies. Two-thirds solution.
- **C — Engine-only firing (D13) + 5-sibling `GameCommand` family with single async `Dispatch<T>(GameCommand)` surface (D14) + `PendingEffectsSlice` as the universal queue (D15) + async-coroutine effects (`IEffect.Run(scope) : Task`, D16) + state-linked request system (`Request<TResponse>` / `Response` / `RequestsSlice` / `AnswersSlice` / `IRequestService`, D17).** **Chosen.**

## Decision

Five structural locks (mapping to D13–D17 of the FSM-arch spike).

**1. Only the engine fires effects.** The effect-pipeline reference lives in *exactly one place* in the system — the engine's drain loop. Cast handlers, trigger reactions, FSM-driven effect runs (a `MatchState` whose `EnterCommands` includes a push), and any other source push effect instances onto `PendingEffectsSlice` (lock 3); the engine notices and drains. The 2026-05-08 spike's Q1 lock (cast handlers and the trigger fold each calling `IEffectPipeline.RunEffect`) is rolled back. Triggers are not a layering issue — `Dispatch(command)` internally runs `OnBefore<T>` / `OnAfter<T>` fold around the handler; trigger reactions, when they need to fire effects, push to `PendingEffectsSlice` instead of calling the pipeline directly.

**2. The `GameCommand` family expands to five siblings; `IEffectScope.Dispatch` becomes a single async surface.**

```csharp
GameCommand                     // abstract base
├── GameAction                  // executable; has handler; returns CommandResult
├── CompositeAction             // reusable group of mutations; inline expansion
├── GameSignal                  // emit-only; history record
├── Request<TResponse>          // opens a player-decision request
│   ├── Request                 // non-generic base (Id, Asker, ResponseType)
│   └── Request<TResponse> : Request where TResponse : Response
└── Response                    // answers a Request by Id
```

```csharp
public interface IEffectScope {
    Task<TResult> Dispatch<TResult>(GameCommand command);
    // …read-only state surface, modifier factory, cancel/replace — unchanged.
}
```

One async entry point routes every kind. The router pattern-matches on the command's runtime type:

| Kind | Routing | Returns |
|---|---|---|
| `GameAction` | validators (per ADR-0010) → `OnBefore<T>` → handler → `OnAfter<T>` → envelope | `CommandResult` |
| `CompositeAction` | inline expansion in parent scope | unit |
| `GameSignal` | validators → write to history | unit |
| `Request<TResponse>` | validators → `IRequestService.Open(request)` → mutate `RequestsSlice` → return `Task<TResponse>` (pending) | `TResponse` |
| `Response` | validators → match by `Id` → `IRequestService.RecordAnswer(id, response)` → mutate `AnswersSlice` + clear `RequestsSlice` → resolve the awaited Task | unit |

Validation is uniform across all kinds (per [ADR-0010](Product/decisions/0010-validator-handler-split.md)). `Dispatch` is async because routing may run the trigger fold (whose reactions may push effects, which may suspend on requests) and because routing `Request<TResponse>` returns a `Task<TResponse>` that completes when the matching `Response` arrives.

**3. `PendingEffectsSlice` is the universal effect queue.** One framework slice holds the ordered queue of effect instances awaiting drain. Every effect-firing path goes through this slice — cast handlers push, trigger reactions push, FSM-driven runs push, runtime code that wants to schedule an effect pushes.

```csharp
public sealed record PendingEffectsSlice(
    ImmutableList<EffectInstance> Pending
) : BaseState;

public sealed record EffectInstance(
    Reference Source,
    EffectId  Id,
    object?   Payload
);
```

`ImmutableList<EffectInstance>` keeps the ordering decision in the runtime — most card games drain LIFO (stack-shaped) so the top of the list is next to fire; FIFO is also valid for queue-shaped games. The engine drains this slice when its drain-policy composition allows; canonical Model at [`features/effects/models/05-pending-effects.md`](features/effects/models/05-pending-effects.md).

**Drain-policy composition.** Whether to drain the next pending effect is composed from state-linked services. The default `Evaluate` body (per ADR-0009) grows two pre-`ShouldExit` guards:

```csharp
public override MatchState Evaluate(GameAction action, CommandResult result)
{
    foreach (var sba in StateBasedActions)
        if (sba.Check(State) is { } over) return over;

    if (State.Get<RequestsSlice>().Open.Count > 0)         return Current;   // pause on open requests
    if (!State.Get<PendingEffectsSlice>().Pending.IsEmpty) return Current;   // engine will drain pending effects

    var current = State.Get<MatchStateSlice>().Current;
    return current.ShouldExit(action, result, State)
        ? Flow.NextAfter(current)
        : current;
}
```

When `PendingEffectsSlice` is non-empty, the engine drains it before any FSM transition — concretely: dequeue the top pending instance, call `effectPipeline.Run(instance, scope)`, await; repeat until empty or until a drain-policy guard pauses. Games override `Evaluate` to insert custom orchestration (MTG-style stack resolution timing requires both players pass priority before draining).

**4. Effects are async coroutines — one method, runs start-to-finish.** The `IEffect` contract collapses to a single async method; the C# async runtime holds continuations across awaits; no state-machine `Step` field, no re-pushing, no engine-side bookkeeping.

```csharp
public interface IEffect {
    Task Run(IEffectScope scope);
}
```

Authors write effects as a single async method that calls `await scope.Dispatch(...)` for sub-commands, signals, and request answers:

```csharp
public sealed class LightningBolt : IEffect
{
    public async Task Run(IEffectScope scope)
    {
        var validIds = ComputeTargets(scope.Store, asker: scope.Caster);
        var response = await scope.Dispatch(new TargetRequest(scope.Caster, validIds));
        await scope.Dispatch(new DealDamageCommand(response.Choice, 3));
        await scope.Dispatch(new CardDamagedSignal(response.Choice, 3));
    }
}
```

**Concurrency rule: strictly serial per drain loop.** Only one effect's `Run` is in flight at a time. While an effect is awaiting a `Response`, the drain loop progresses (the response flows through the command pipeline, validates, resolves the awaited Task), but no other pending effect is popped from `PendingEffectsSlice`. Order on the slice is the order of execution.

**Save/load mid-effect not supported by default.** A C# `Task` continuation is not serializable. Save/load can happen at quiescent points — no effect's `Run` currently in flight, `RequestsSlice` empty (or all open requests carry serializable state — they do, per lock 5). If a game later forces mid-effect save/load, that requires state-machine effects — a separate spike. Tracked as `oq-effect-save-load`.

**5. The request system is the state-linked service for player decisions.** Two slices + one engine-internal service replace `ICommandAwaiter`'s type-only routing. The whole shape is wire-friendly: every request and response is a typed serializable record, every `RequestId` is a `Guid`-backed struct, every "what's the player being asked?" answer comes from `RequestsSlice` — which is the wire format clients see.

```csharp
public sealed record RequestsSlice(
    ImmutableDictionary<RequestId, Request> Open
) : BaseState;

public sealed record AnswersSlice(
    ImmutableDictionary<RequestId, Response> By
) : BaseState;

public readonly record struct RequestId(Guid Value)
{
    public static RequestId NewId() => new(Guid.NewGuid());
}

public abstract record Request : GameCommand
{
    public RequestId Id    { get; init; }
    public IActor    Asker { get; init; }
    public abstract Type ResponseType { get; }
}

public abstract record Request<TResponse> : Request
    where TResponse : Response
{
    public override Type ResponseType => typeof(TResponse);
}

public abstract record Response : GameCommand
{
    public RequestId Id { get; init; }
}

public interface IRequestService
{
    void Open       (Request request);                       // mutates RequestsSlice
    void RecordAnswer(RequestId id, Response answer);        // mutates AnswersSlice + clears RequestsSlice
    void Cancel     (RequestId id);                          // clears Open[id]; no answer recorded
}
```

**No predicates in requests.** All request fields are serializable references and values — `IReadOnlyList<Reference> ValidTargets`, `int Total`, `int Min`, `int Max`, `string Prompt`, etc. Game-side logic computes the valid set *before* opening the request; the request carries the IDs, not a `Func<Reference, bool>` filter. Same serialization-discipline pattern as ADR-0009's `MatchState.Actions: IReadOnlyList<Type>`.

**Validation rides the ADR-0010 chain.** Framework ships validators for framework-shipped request/response pairs (`TargetResponseValidator` — "is `Id` known? Is `Choice` in the valid set?"); games ship validators for their own pairs. Same DI registry, same short-circuit semantics, same registration-order rule.

**Two slices, different lifecycles.** `RequestsSlice.Open` is "still asking" — clients subscribe to render pickers from this slice. `AnswersSlice.By` is "answered, awaiting consumer" — exists for replay (a recorded match can be re-played by re-dispatching the recorded responses against open requests in the same order). From the effect-author's POV, `AnswersSlice` is invisible — `await scope.Dispatch(request)` resolves directly with the typed `TResponse`. Match-end clears both slices.

Canonical Model at [`features/effects/models/06-request-system.md`](features/effects/models/06-request-system.md).

## Consequences

- **Enables** structural layering of effect ownership — exactly one place in the system holds an `IEffectPipeline` reference (the engine's drain loop). Cast handlers, trigger reactions, FSM-driven runs all route through `PendingEffectsSlice`. Effects fire in a single canonical order observable in state.
- **Enables** authors writing multi-step effects as plain async methods. No `Step` field, no re-pushing, no manual continuation tracking. The C# async runtime carries the continuation across `await`s. Reading an effect's source code reads like ordinary procedural code.
- **Enables** typed mid-effect requests. `await scope.Dispatch(new TargetRequest(...))` returns a typed `TargetResponse` directly; the request system handles the open/answer/resolve cycle through state and the `Dispatch` router. UIs subscribe to `RequestsSlice` and render pickers; responses dispatch through the same async surface as everything else.
- **Enables** replay determinism for player decisions. Every request and response is a serializable record; the recorded sequence of `Response` dispatches against open requests in `RequestsSlice` reproduces the exact same match. `AnswersSlice` is the wire-form audit trail.
- **Enables** ADR-0010's "handler bodies atomic from the player-input POV" invariant structurally. Handlers no longer await player decisions — they push effects onto `PendingEffectsSlice` and return immediately. Effects (running in their own async-coroutine scope) own the long-running orchestration.
- **Enables** a uniform `GameCommand` family across the dispatch surface. Validators, the trigger fold, the history envelope, and the `Dispatch` router all see the same five subtypes. No parallel infrastructure for requests/responses; they ride the same machinery as actions and signals.
- **Constrains** every effect-firing path to push to `PendingEffectsSlice`. Cast handlers cannot `await` an effect directly inside `Handle` (that would be a re-introduction of the layering violation); they push and return.
- **Constrains** `Request<TResponse>` and `Response` to carry only serializable references and values. No `Func` fields, no predicates, no delegates. Game-side logic computes the valid set before opening; the request is wire data.
- **Constrains** effects to a strictly serial concurrency model per drain loop. Only one `Run` is in flight at a time. While an effect awaits a `Response`, the drain loop progresses (the response flows through the command pipeline), but `PendingEffectsSlice` does not pop a parallel sibling.
- **Constrains** save/load to quiescent points (no effect's `Run` currently in flight). If a game later forces mid-effect save/load, would require state-machine effects — a separate spike (`oq-effect-save-load`).
- **Retires** `ICommandAwaiter` and the canonical Model that documented it (`Product/features/effects/models/04-command-awaiter.md`) — archived. Decision routing lives on the request system; type-only routing is gone.
- **Retires** the 2026-05-08 effect-pipeline spike (`Product/spikes/effect-pipeline.md`). The ADR + new Models inherit the still-locked Q2–Q10 verdicts (pure split, off-state subscription registry, definition catalog, chain scope, decisions-as-actions, envelope-as-atomicity, validators-off-handler); the Q1 unification verdict is rolled back per lock 1; everything else is reshaped by D13–D17.
- **Reverts / replaces** the 2026-05-08 Q1 lock (cast handlers + trigger fold both calling `IEffectPipeline.RunEffect`). Lock 1 here is the rollback.
- **Defers** the effect-authoring vocabulary (`oq-effect-authoring`). `IEffect.Run(scope) : Task` is the locked runtime contract; how a `CardDefinition` holds the *recipe* that constructs `IEffect` instances at cast time — code-side registration, serialized asset, definition editor, hybrid — is the authoring feature's concern.
- **Defers** mid-effect cancellation token threading (`oq-effect-cancellation-on-match-end`). The locked resolution path threads `CancellationToken` through `IEffectScope.Dispatch`; engine cancels on match-end and GCs `RequestsSlice` / `AnswersSlice`. Implementation pass; no further design decisions needed.
- **Defers** mid-effect save/load (`oq-effect-save-load`). Async-coroutine effects cannot serialize mid-`await`; if a game forces this, would force state-machine effects.
- **Defers** drain-policy composition Model (`oq-drain-policy-composition`). The composition rule is documented in [`features/effects/models/05-pending-effects.md`](features/effects/models/05-pending-effects.md) §Drain policy; whether it grows into its own Model depends on how complex game-side overrides become.
- **Defers** pre-play targeting (`oq-targeting-pre-play`). A card whose play-condition requires a chosen target *before* the play-card command fires (the target is a parameter of the play, not a request inside the resolution) folds into a richer `PlayCardCommand(card, target)` whose cast validator accepts the target. Differs from mid-effect target requests; today's request system covers mid-effect only.

## Notes

- **Sub-ADR cascade position.** This is sub-ADR C of the FSM-arch cascade — structurally independent of ADR-0009 (engine as FSM) and ADR-0010 (validator/handler split); ADR-0012 (presentation per-state ViewModels) lands last and depends on ADR-0009 only. The validator chain from ADR-0010 is the dispatch-time gate for *every* command in the 5-sibling family, including `Request<TResponse>` and `Response`. The two ADRs (0010 + 0011) merge in either order; whichever lands second resolves conflicts in `actions/01-command-pipeline.md`, `effects/04-command-awaiter.md`, the feature roots, and the ledgers.
- **Spike provenance.** `Product/spikes/archive/fsm-architecture.md` D13–D17 is the historical source for this ADR. The 2026-05-08 effect-pipeline spike — archived in this ADR's PR to `Product/spikes/archive/effect-pipeline.md` — is wholly superseded. The FSM-architecture spike itself was archived in the post-cascade consolidation PR (2026-05-11) once ADR-0012 had landed its Model cascade.
- **Effect-pipeline service shape.** The framework ships `InlineEffectPipeline` as the default `IEffectPipeline` — `Run(instance, scope)` resolves the `EffectDefinition` for `(Source, EffectId)` and invokes its `IEffect.Run(scope)`. Games may swap implementations in DI for stack-resolution timing, deferred batching, etc., but the *only call site* remains the engine's drain loop (per lock 1).
- **`EffectDefinitionCatalog`.** Resolution from `(Source, EffectId)` to a runnable `IEffect` instance happens through an engine-plumbing `IEffectDefinitionCatalog` (Q5 of the 2026-05-08 spike survives). Per-card-bound effects live on `CardDefinition.Effects`; game-bound effects (world rules) live on a synthetic actor's effect list. Catalog shape is engine-internal; authoring vocabulary is `oq-effect-authoring`.
- **Triggers continue working as today.** `Trigger<T>` / `ITriggerPipeline` / `OnBefore<T>` / `OnAfter<T>` are unchanged. Trigger reactions, when they need to fire effects, push to `PendingEffectsSlice` (same channel as cast handlers). The trigger pipeline's own internals — fold ordering, before-fold cancel/replace/modify — are out of scope for this ADR.
- **`CommandResult` typing for `Dispatch`.** `Task<TResult> Dispatch<TResult>(GameCommand command)` is a generic single entry. The type system's `TResult` is loose at the call site — `GameAction` dispatches resolve with a typed `CommandResult` subtype; `Request<TResponse>` dispatches resolve with `TResponse`; `GameSignal` / `CompositeAction` / `Response` dispatches resolve with a unit type (`Task` or a unit-shaped `CommandResult`). Tightening to `Dispatch<TCommand, TResult>` with type-mapping constraints is a future ergonomic pass; not load-bearing.
- **Pre-play targeting deferred (`oq-targeting-pre-play`).** Today's request system covers mid-effect targeting only. A card whose play-condition requires a chosen target before the cast handler fires is a different shape — likely a richer `PlayCardCommand(card, target)` whose cast validator accepts the target. Settle when the first game forces it.
- **Cancellation tokens.** When the first scenario forces it (match-end during mid-effect await; disconnect; abort), thread `CancellationToken` through `IEffectScope.Dispatch`. Existing open item `oq-effect-cancellation-on-match-end` carries the implementation task.
