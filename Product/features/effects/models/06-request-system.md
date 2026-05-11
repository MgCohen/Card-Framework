---
type: model
status: settled
references: [features/effects/models/01-effect.md, features/effects/models/02-effect-scope.md, features/effects/models/05-pending-effects.md, features/actions/models/01-command-pipeline.md, features/actions/models/06-command-validator.md, features/engine/models/01-game-engine.md, features/state/state.md, decisions/0011-effect-orchestration-dispatch-requests.md, decisions/0010-validator-handler-split.md]
tags: [#effects, #model, #requests, #player-input, #state-linked-service]
---

# Request System — model

## What it is

The state-linked service for player decisions mid-effect. An effect calls `await scope.Dispatch(new TargetRequest(...))`; the request system opens the request in `RequestsSlice`, returns a `Task<TResponse>` pending; clients render a picker from the slice; the player's response dispatches as a `Response` command; the system records the answer in `AnswersSlice`, clears the request from `RequestsSlice`, and resolves the awaited Task with the typed response. The pattern follows the state-linked-service shape pinned by [ADR-0011](Product/decisions/0011-effect-orchestration-dispatch-requests.md) D17: **slice owns the data; injectable façade is the only writer; engine reads for orchestration; clients subscribe**.

```
Effect.Run                                Engine                          Client (subscribed)
─────────                                 ──────                          ───────────────────
await scope.Dispatch(request)
        │
        ▼
   Dispatch router
        │
        ├──▶ validators (per ADR-0010)
        ├──▶ IRequestService.Open(request)
        │         │
        │         ▼
        │    RequestsSlice += request  ────────────────────────────────▶ render picker
        │                                                                     │
        └──▶ returns Task<TResponse> (pending)                                 │
        │                                                                     │
        ⋮ (drain pauses; RequestsSlice non-empty)                              │
        │                                                                     ▼
        │                                                              player chooses
        │                                                              dispatch Response
        │                                                                     │
        ▼                                                                     ▼
   Dispatch router  ◀──────────────────────────────────────────────  engine.Dispatch
        │
        ├──▶ validators
        ├──▶ IRequestService.RecordAnswer(id, response)
        │         │
        │         ▼
        │    AnswersSlice += response;  RequestsSlice -= id
        │
        └──▶ resolves the pending Task<TResponse> with the typed response

Effect.Run resumes with TResponse value.
```

## Contracts

### `RequestId`

```csharp
public readonly record struct RequestId(Guid Value)
{
    public static RequestId NewId() => new(Guid.NewGuid());
}
```

Stable identifier for the lifetime of an open request and its recorded answer. Allocated when the request is opened. `RequestId.NewId()` uses an engine-owned counter / RNG so replay reproduces the same ids deterministically (the framework's deterministic-ref discipline; same pattern as `ModifierId` allocation).

### `Request` / `Request<TResponse>` / `Response`

The three new `GameCommand` siblings (per [ADR-0011](Product/decisions/0011-effect-orchestration-dispatch-requests.md) D14). All inherit from `GameCommand`; they ride the same `Dispatch` router and validator chain as every other command.

```csharp
public abstract record Request : GameCommand
{
    public RequestId    Id    { get; init; }
    public IActor       Asker { get; init; }
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
```

`Request<TResponse>` is the generic specialization the framework's `Dispatch` overload binds to:

```csharp
public interface IEffectScope
{
    // For Request<TResponse>, TResult is bound to TResponse via the routing pattern.
    Task<TResult> Dispatch<TResult>(GameCommand command);
}
```

When an effect calls `await scope.Dispatch(new TargetRequest(...))`, the router pattern-matches on `Request<TResponse>` and the returned `Task<TResult>` carries the typed `TResponse` payload.

### `RequestsSlice` — "still asking"

```csharp
public sealed record RequestsSlice(
    ImmutableDictionary<RequestId, Request> Open
) : BaseState;
```

- **Framework-owned, auto-seeded** at `Build()` with `Open = ImmutableDictionary<RequestId, Request>.Empty`.
- **Indexed by `RequestId`.** Multi-actor parallel requests (each player chooses simultaneously) are supported by the dictionary, but querying "any open request for actor X" is O(N) until a parallel actor-index slice surfaces (`oq-requests-actor-index`).
- **The wire format clients see.** UI subscribes to `RequestsSlice`; renders pickers from `Open` entries; in request-mode, the active VM (per [ADR-0012](Product/decisions/0012-presentation-per-state-view-models.md)) returns a `Response` from `Map(intent)`.
- **Drain pauses on non-empty.** Per [`05-pending-effects.md`](features/effects/models/05-pending-effects.md), the default `Evaluate` body returns `Current` while `RequestsSlice.Open.Count > 0` — FSM does not transition; pending effects do not drain.

### `AnswersSlice` — "answered, awaiting consumer"

```csharp
public sealed record AnswersSlice(
    ImmutableDictionary<RequestId, Response> By
) : BaseState;
```

- **Framework-owned, auto-seeded** at `Build()` with `By = ImmutableDictionary<RequestId, Response>.Empty`.
- **From the effect-author's POV, invisible.** `await scope.Dispatch(request)` resolves directly with the typed `TResponse`; authors never read `AnswersSlice` directly.
- **Exists for replay.** A recorded match can be re-played by re-dispatching the recorded responses against open requests in the same order. The slice is the audit trail of "what was the player asked, what did they answer?"
- **Match-end clears the slice.** `EndMatchCommand`'s handler GCs both `RequestsSlice` and `AnswersSlice` (per `oq-effect-cancellation-on-match-end`'s resolution path).

### `IRequestService`

```csharp
public interface IRequestService
{
    void Open       (Request request);                        // mutates RequestsSlice
    void RecordAnswer(RequestId id, Response answer);         // mutates AnswersSlice + clears RequestsSlice
    void Cancel     (RequestId id);                           // clears Open[id]; no answer recorded
}
```

The engine-internal façade. **The only writer for `RequestsSlice` and `AnswersSlice`.** Reached by the `Dispatch` router on `Request<TResponse>` and `Response` routes, not by effect-author code. Effects interact with requests *only* by calling `await scope.Dispatch(request)`.

The service's writes go through normal payload+mutator dispatch (`Store.Execute(new OpenRequestPayload(request))` etc.); the façade is sugar on top so the router can call `Open` / `RecordAnswer` / `Cancel` symbolically.

### Framework-shipped request/response pair — `TargetRequest`

```csharp
public sealed record TargetRequest(
    RequestId                Id,
    IActor                   Asker,
    IReadOnlyList<Reference> ValidTargets
) : Request<TargetResponse>;

public sealed record TargetResponse(
    RequestId Id,
    Reference Choice
) : Response;
```

The framework ships one canonical pair (`TargetRequest` / `TargetResponse`). Games define their own pairs for game-specific shapes (split N damage between targets, choose ordered list, choose a number, etc.).

```csharp
public sealed record SplitDamageRequest(
    RequestId                Id,
    IActor                   Asker,
    int                      Total,
    IReadOnlyList<Reference> ValidTargets
) : Request<SplitDamageResponse>;

public sealed record SplitDamageResponse(
    RequestId                                Id,
    ImmutableDictionary<Reference, int>      Allocation
) : Response;
```

Each pair is a typed serializable record — wire-friendly for replay and network. **No predicates in requests.** All fields are serializable references and values. Game-side logic computes the valid set *before* opening (e.g., `IReadOnlyList<Reference> ValidTargets`); the request carries the IDs, not a `Func<Reference, bool>` filter.

### Validation rides the ADR-0010 chain

Per [`features/actions/models/06-command-validator.md`](features/actions/models/06-command-validator.md), requests and responses ride the same `ICommandValidator<TCommand>` registry as every other command. Framework ships validators for framework-shipped pairs; games ship validators for their own pairs.

```csharp
public sealed class TargetResponseValidator : CommandValidator<TargetResponse>
{
    public override bool Validate(TargetResponse r, out string? reason)
    {
        if (!Store.Get<RequestsSlice>().Open.TryGetValue(r.Id, out var req))
        { reason = "no such request"; return false; }
        if (req is not TargetRequest tr)
        { reason = "type mismatch"; return false; }
        if (!tr.ValidTargets.Contains(r.Choice))
        { reason = "invalid target"; return false; }
        reason = null;
        return true;
    }
}
```

### Routing — `Dispatch` semantics

The `Dispatch` router (per [`features/actions/models/01-command-pipeline.md`](features/actions/models/01-command-pipeline.md) §Pipeline mechanics) treats `Request<TResponse>` and `Response` as two of the five `GameCommand` kinds:

| Command kind | Routing | Returns |
|---|---|---|
| `GameAction` | validators → before-fold → handler → after-fold → envelope | `CommandResult` |
| `CompositeAction` | inline expansion in parent scope | unit |
| `GameSignal` | validators → write to history | unit |
| `Request<TResponse>` | validators → `IRequestService.Open(request)` → return `Task<TResponse>` (pending) | `TResponse` |
| `Response` | validators → `IRequestService.RecordAnswer(id, response)` → resolve the awaited Task | unit |

`Request<TResponse>` does **not** go through `OnBefore<T>` / `OnAfter<T>` triggers — requests are not actions; they don't modify state directly (only via the service writing to `RequestsSlice`). Same for `Response` — answering a request flows through the service to mutate `AnswersSlice` + clear `RequestsSlice`. Triggers can subscribe to `OnAfter<TargetResponse>` if a game wants reactions to player choices, but the architecture doesn't force it.

### Flow — mid-effect target

```text
1. Effect.Run executes:
     var validIds = ComputeTargets(scope.Store, asker: scope.Caster);
     var response = await scope.Dispatch(new TargetRequest(
         Id:           RequestId.NewId(),
         Asker:        scope.Caster,
         ValidTargets: validIds));
   Dispatch routes Request<TargetResponse>:
     • validators run (framework + game-shipped).
     • IRequestService.Open(request) → RequestsSlice += request.
     • Returns Task<TargetResponse> (pending).
   Effect's Run is suspended on the awaited Task.

2. RequestsSlice has the open request. Drain policy pauses (per 05-pending-effects.md).

3. Client subscribed to RequestsSlice renders a picker showing only the validIds.
   Player taps a target.

4. VM (in request-mode, per [ADR-0012](Product/decisions/0012-presentation-per-state-view-models.md)) maps the gesture:
     return new TargetResponse(currentRequestId, chosenRef);
   The Response is dispatched into the engine.

5. Dispatch routes Response:
     • validators run (TargetResponseValidator: id known, choice valid).
     • IRequestService.RecordAnswer(id, response):
         → AnswersSlice += response (atomic mutation).
         → RequestsSlice -= id (atomic mutation; same batch).
     • Resolves the awaited Task with TargetResponse(c2).

6. Effect.Run resumes:
     await scope.Dispatch(new DealDamageCommand(response.Choice, 3));
     await scope.Dispatch(new CardDamagedSignal(response.Choice, 3));
   …continues to completion.
```

### Cancel and timeout

- **`IRequestService.Cancel(id)`** — clears `RequestsSlice.Open[id]` without recording an answer. The awaited Task is cancelled (the effect's `await` throws a `TaskCanceledException` or completes with a sentinel response — concrete shape pending `oq-effect-cancellation-on-match-end`). Used on match-end, disconnect, and explicit abort.
- **Timeout** — not part of the framework; if a game wants timer-driven requests, it ships a timer service that calls `Cancel(id)` after a deadline. Tracked informally.

### Match-end GC

When `EndMatchCommand`'s handler flips `engine.Session` to `EndedSession`, it dispatches a cleanup mutator that empties both `RequestsSlice` and `AnswersSlice`. In-flight effect `Run`s that were awaiting responses receive cancellation (per the request cancel path above). Implementation pass; resolution path locked.

## Invariants

- **`IRequestService` is the only writer.** `RequestsSlice` and `AnswersSlice` are mutated exclusively through `Open` / `RecordAnswer` / `Cancel`. No direct payload dispatch outside the service.
- **No predicates in requests.** All `Request<TResponse>` fields are serializable references and values. Game-side logic computes the valid set before opening; the request carries the IDs.
- **`RequestId` is engine-owned and deterministic.** Allocation uses the same engine counter / RNG that ensures replay reproducibility.
- **Drain pauses on open requests.** `RequestsSlice.Open` non-empty → `Evaluate` returns `Current`; engine does not drain `PendingEffectsSlice`; FSM does not transition.
- **Effects do not see `AnswersSlice`.** `await scope.Dispatch(request)` resolves directly with the typed `TResponse`. `AnswersSlice` exists for replay tooling and recorded-match playback.
- **Validation is uniform** — `Request<TResponse>` and `Response` ride the same `ICommandValidator<TCommand>` chain as actions and signals (per [ADR-0010](Product/decisions/0010-validator-handler-split.md)).
- **No before-fold / after-fold on requests and responses.** Triggers may subscribe to `OnAfter<TargetResponse>` etc. if a game wants reactions, but requests don't modify state directly so before-fold cancel/replace doesn't apply.

## Decisions

Linked: [ADR-0011](Product/decisions/0011-effect-orchestration-dispatch-requests.md) — state-linked request system (D17); 5-sibling `GameCommand` family (D14); single async `Dispatch` surface (D14). Spike provenance: `Product/spikes/archive/fsm-architecture.md` D14 + D17.

Linked: [ADR-0010](Product/decisions/0010-validator-handler-split.md) — uniform validator chain across every `GameCommand` subtype, including `Request<TResponse>` and `Response`.

## Examples

### Lightning Bolt — mid-effect target

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
```

The author writes one async method. The `await` on `TargetRequest` suspends `Run` until the player chooses; the response resolves the Task; execution resumes.

### Split-damage — game-shipped pair

```csharp
public sealed record SplitDamageRequest(
    RequestId                Id,
    IActor                   Asker,
    int                      Total,
    IReadOnlyList<Reference> ValidTargets
) : Request<SplitDamageResponse>;

public sealed record SplitDamageResponse(
    RequestId                            Id,
    ImmutableDictionary<Reference, int>  Allocation
) : Response;

public sealed class SplitDamageResponseValidator : CommandValidator<SplitDamageResponse>
{
    public override bool Validate(SplitDamageResponse r, out string? reason)
    {
        if (!Store.Get<RequestsSlice>().Open.TryGetValue(r.Id, out var req))
        { reason = "no such request"; return false; }
        if (req is not SplitDamageRequest sd)
        { reason = "type mismatch"; return false; }
        if (r.Allocation.Values.Sum() != sd.Total)
        { reason = $"allocation sum {r.Allocation.Values.Sum()} != total {sd.Total}"; return false; }
        if (!r.Allocation.Keys.All(k => sd.ValidTargets.Contains(k)))
        { reason = "invalid target in allocation"; return false; }
        reason = null;
        return true;
    }
}
```

The validator catches malformed responses; the effect resumes only when the chain accepts.

### Concurrent multi-actor decisions

```csharp
public async Task Run(IEffectScope scope)
{
    // Open one request per player simultaneously.
    var requests = scope.Store
        .GetAll<Player>()
        .Select(p => new MulliganRequest(
            Id:     RequestId.NewId(),
            Asker:  p,
            Hand:   scope.Store.Get<HandState>(p.SelfRef).Cards))
        .ToList();

    // Fire all dispatches; await them all.
    var tasks = requests.Select(r => scope.Dispatch(r)).ToList();
    var responses = await Task.WhenAll(tasks);

    // Apply each player's mulligan decision.
    foreach (var (req, resp) in requests.Zip(responses))
    {
        if (resp.Keep) continue;
        await scope.Dispatch(new MulliganCommand(req.Asker, resp.Replacements));
    }
}
```

Multiple open requests live in `RequestsSlice.Open` simultaneously; each resolves independently as its `Response` arrives. The drain stays paused until *all* are answered (per the drain-policy guard).

## Open follow-ups

- `oq-effect-cancellation-on-match-end` — `IRequestService.Cancel(id)` plus match-end GC of both slices; thread `CancellationToken` through `IEffectScope.Dispatch`. Implementation pass; resolution path locked.
- **`RequestsSlice` keyed by `RequestId` only vs by actor.** Multi-actor parallel requests (each player chooses simultaneously) are supported by the dictionary; querying "any open request for actor X" is O(N). Add a parallel actor index when a real query consumer surfaces. Tracked informally as `oq-requests-actor-index`.
- **Authoring vocabulary for game-shipped pairs.** How games author and register `Request<TResponse>` / `Response` pairs (typed records, builder DSL, code-gen) is open. Folded into `bl-authoring-feature`.
- **Cancel sentinel shape.** Whether a cancelled `await scope.Dispatch(request)` throws `TaskCanceledException` or resolves with a sentinel `Response` subtype is pending `oq-effect-cancellation-on-match-end`. Either works; pick during implementation.
- **Pre-play targeting.** A card whose play-condition requires a chosen target *before* the play-card command fires (target is a parameter of the play, not a request inside the resolution) likely folds into a richer `PlayCardCommand(card, target)` whose cast validator accepts the target. Differs from mid-effect targeting; today's request system covers mid-effect only. Tracked as `oq-targeting-pre-play`.

---

*Below: provenance — Model authored 2026-05-11 as part of the ADR-0011 cascade. No options-weighing at this Model's scope; the surface is dictated by [ADR-0011](Product/decisions/0011-effect-orchestration-dispatch-requests.md).*

## Options considered

*n/a — descriptive Model. Choices made at ADR-0011 scope; see linked ADR for option-weighing.*

## Rationale

*n/a — see `## Contracts` and [ADR-0011](Product/decisions/0011-effect-orchestration-dispatch-requests.md).*

## Consequences

*n/a — see [ADR-0011](Product/decisions/0011-effect-orchestration-dispatch-requests.md) `## Consequences`.*
