---
type: spike
status: active
created: 2026-05-30
references:
  - Product/spikes/archive/wire-refoundation-roadmap.md
  - Product/spikes/definition-service-pattern.md
  - Product/spikes/wire-block-events.md
  - Product/spikes/state-service.md
  - Product/decisions/0011-effect-orchestration-dispatch-requests.md
sandbox: Assets/Framework/Requests/
tags: [#spike, #requests, #wire, #service, #async]
---

# Request Service — typed async player decisions

> **Status: active.** Design walk + build both landed 2026-05-30. The L1–L6
> shape ships in `Framework.Requests` (commit `8e03405`); the mirror
> round-trip is verified by `RequestServiceTests` (6 scenarios). Builds on
> the step-4 service pattern (Store + Service + per-type apply handler), the
> AutoPacker fix that enables `[Packed]` field inheritance through generic
> intermediates, and the WireEvent flatten (commit `19c76c3`) that dropped
> the `WireEvent<TPacked>` typed intermediate in favor of AutoPacker emitting
> typed interfaces directly on each leaf. TPacked now appears only at
> `Network.Register<T, TPacked>()`.

## TL;DR

Requests are typed async player decisions. Each Request/Response pair is a
**single wire-event class** — no separate "data class + wire event" split.
The server-side `RequestService.Open(request) → Task<TResponse>` emits the
Request wire event and returns a Task that resolves with the typed Response
when the matching Response wire event arrives. Both Request and Response
inherit framework base classes that supply shared `[Packed]` metadata via
AutoPacker inheritance. Stores on both sides; service server-only.

## Where this slots

Step 5a of [`wire-refoundation-roadmap.md`](archive/wire-refoundation-roadmap.md).

**Pre-step-5a landed:**
- The service pattern — Store + Service + per-type apply handler
  ([`definition-service-pattern.md`](definition-service-pattern.md)).
- Block bracket wire events
  ([`wire-block-events.md`](wire-block-events.md)). **Note:** the thin
  `RequestOpenedEvent`/`RequestClosedEvent`/`ResponseEvent` envelope events
  from that spike are superseded by this design (see L2 — typed wire makes
  envelopes redundant). The Block side (Command/Effect/Trigger brackets)
  is unaffected.
- Role-gated registration; server-only services; both-side stores.

**After this:**
- Step 5b — State service (similar shape, see
  [`state-service.md`](state-service.md)).
- Step 5c+ — EffectService — the actual consumer of `await service.Open(...)`.

## Locked decisions

### L1 — Resolution is async/typed: `Task<TResponse>` via TCS

`RequestService.Open(request)` returns `Task<TResponse>`. Callers `await`
directly. When the matching Response wire event arrives, the server-side
handler completes the `TaskCompletionSource<TResponse>` with the typed
response.

```csharp
var response = await scope.Requests.Open(new PickCardsRequest { ... });
// response is typed PickCardsResponse — no cast, no dictionary lookup
```

Rejected: sync resolution with a `Resolve(picked, scope)` callback on the
Request. Couples Request to a dispatch surface, doesn't compose with async
effects (the future `IEffect.Run(scope) : Task` pattern from ADR-0011),
and the typed-response delivery would still need a separate mechanism.

### L2 — Request and Response ARE wire events (single-class shape)

No separate "Request data class + RequestWireEvent." The Request class
itself extends `WireEvent` and is what travels on the wire. Same for
Response. AutoPacker handles serialization via `[Packed]` fields and emits
typed `IPackable<T.Packed>` / `IUnpackable<T.Packed>` on each leaf.

Replaces the wire-block-events thin envelopes — `RequestOpenedEvent`,
`RequestClosedEvent`, `ResponseEvent` are removed in favor of per-game
typed Request and Response wire events.

Consequence: the wire alphabet grows by **two events per request kind**
(`PickCardsRequest` + `PickCardsResponse`). Mechanical, scales fine.

### L3 — Base classes with shared `[Packed]` fields (DRY via inheritance)

Framework provides a two-level hierarchy for Request and a single
non-generic base for Response. `[Packed]` only works on fields (not
properties), so the shared metadata lives as plain public fields on the
non-generic base — direct access works on both sides, no interface
ceremony required.

```csharp
// Non-generic Request base — holds the shared [Packed] fields. Used as
// the dictionary key type in RequestStore (Dictionary<int, Request>)
// and as the bound for service signatures that don't need TResponse.
public abstract partial class Request : WireEvent
{
    [Packed] public int  Id;
    [Packed] public int  ParentId;
    [Packed] public int  SourceId;
    [Packed] public int  ResponderId;
    [Packed] public bool IsCancellable;
}

// Generic wrapper — adds TResponse for typed inference at the service
// surface (`Open<TResponse>(Request<TResponse>) → Task<TResponse>`).
// No [Packed] fields of its own.
public abstract partial class Request<TResponse> : Request
    where TResponse : Response
{
    public Type ResponseType => typeof(TResponse);
}

// Single non-generic Response base — holds the RequestId correlator.
// No TResponse-style wrapper needed since Response isn't parameterized
// on a return type.
public abstract partial class Response : WireEvent
{
    [Packed] public int RequestId;
}
```

Concrete leaves add only kind-specific fields:

```csharp
public partial class PickCardsRequest : Request<PickCardsResponse>
{
    [Packed] public int PickCount;
    // CardIds — see Deferred / int[] handler
}

public partial class PickCardsResponse : Response
{
    [Packed] public int Picked;
    // SelectedIds — see Deferred / int[] handler
}
```

**Requirements:** AutoPacker must support `[Packed]` field aggregation
through abstract intermediates (the `BaseStamped → DerivedStamped`
pattern, extended to a generic wrapper between them). Both behaviors
are verified — AutoPacker fix commit `86b336b4` covered the original
case; the WireEvent flatten in `19c76c3` simplified the chain so the
intermediate no longer forwards TPacked at all.

**Why no interfaces:** an earlier draft used `IRequest`/`IResponse`
markers, but `[Packed]` requires fields (not properties), and interfaces
can't declare fields. Implementing the interface contract via property
accessors that delegate to backing fields adds ceremony for no payoff —
the non-generic base classes give us the same polymorphic-dispatch
surface (`Dictionary<int, Request>`) with cleaner game-side ergonomics
(direct field access on shared metadata).

### L4 — Thin RequestStore on both sides; server-only Dictionary for live TCS state

`RequestStore` holds `Dictionary<int, Request>` keyed by `Request.Id`
(`Request` is the non-generic base from L3). Reconstructable purely from
the Request wire event — both server and client share the shape.

Server-only `RequestService` keeps an additional
`Dictionary<int, IPendingRequest>` for live Request + TCS state:

```csharp
internal interface IPendingRequest { void Cancel(); }

internal sealed class PendingRequest<TResponse> : IPendingRequest
    where TResponse : Response
{
    public TaskCompletionSource<TResponse> Tcs { get; } = new();
    public void Cancel() => Tcs.TrySetCanceled();
}
```

When Response handler.Apply runs server-side, it dispatches to
`RequestService.Complete(response)` which `SetResult`s the TCS — the
awaiting caller resumes with the typed response.

Same handler runs on the client; the client has no service to dispatch to,
just drops the request from its store.

### L5 — No AnswersStore

The wire IS the audit trail. Once a Response wire event applies, the
Request is removed from RequestStore and the TCS resolves; the response
payload exists only on the wire log and (briefly) on the awaiter's stack.
Replay re-applies wire events through the same RequestService path to
reproduce server state deterministically.

Skipping `AnswersStore` keeps memory bounded and store count minimal. If
a consumer later needs queryable past answers (debug overlay, spectator
view, trigger fired after-mulligan), `AnswerStore` is additive — one
store + an `Insert` call inside the existing Apply handler.

ADR-0011's `AnswersSlice` was designed for an architecture where the wire
wasn't the source of truth. Post-step-4 reshape, the wire is — so the
second store is redundant for the replay use case.

### L6 — Role-gated registration; `GameEngine.Requests` is the discoverable handle

Matches the Tags/Entities pattern from the step-4 reshape.

| Side   | Resolves                                                              |
|--------|-----------------------------------------------------------------------|
| Server | `RequestService`, `RequestStore`, all Request/Response apply handlers |
| Client | `RequestStore`, all Request/Response apply handlers — **not** `RequestService` |

`GameEngine.Requests` (server-only) gives the discoverable composition
handle. Client code that needs to read open requests goes through
`RequestStore` directly — no engine handle on the client (per step-4 reshape).

## Concrete shape

### Wire-level (framework, in `Framework.Requests`)

```csharp
public abstract partial class Request : WireEvent
{ /* per L3 — five shared [Packed] fields: Id, ParentId, SourceId, ResponderId, IsCancellable */ }

public abstract partial class Request<TResponse> : Request
    where TResponse : Response
{ /* ResponseType => typeof(TResponse) — no [Packed] of its own */ }

public abstract partial class Response : WireEvent
{ /* per L3 — RequestId [Packed] field */ }
```

### Service (framework, server-only)

```csharp
public sealed class RequestService
{
    private readonly RequestStore _store;
    private readonly IEventEmitter _events;
    private readonly Dictionary<int, IPendingRequest> _pending = new();
    private int _nextId = 1;

    public RequestService(RequestStore store, IEventEmitter events)
    { _store = store; _events = events; }

    public Task<TResponse> Open<TResponse>(Request<TResponse> request)
        where TResponse : Response
    {
        request.Id = _nextId++;
        var pending = new PendingRequest<TResponse>();
        _pending[request.Id] = pending;
        _events.Emit(request);     // Apply handler inserts into RequestStore
        return pending.Tcs.Task;
    }

    internal void Complete<TResponse>(TResponse response)
        where TResponse : Response
    {
        if (!_pending.Remove(response.RequestId, out var pending)) return;
        ((PendingRequest<TResponse>)pending).Tcs.TrySetResult(response);
    }

    internal void CancelAllPending()
    {
        foreach (var p in _pending.Values) p.Cancel();
        _pending.Clear();
    }
}
```

### Store (framework, both sides)

```csharp
public sealed class RequestStore
{
    private readonly Dictionary<int, Request> _open = new();

    public IReadOnlyDictionary<int, Request> Open => _open;

    public T Get<T>(int id) where T : Request =>
        _open.TryGetValue(id, out var req) ? req as T : null;

    internal void Insert(Request req) => _open[req.Id] = req;
    internal void Remove(int id)      => _open.Remove(id);
}
```

### Per-type handlers (game-side, one per concrete Request/Response)

```csharp
// Request handler — store insert on Apply, remove on Unapply
public sealed class PickCardsRequestHandler
    : IWireEventHandler<PickCardsRequest>
{
    private readonly RequestStore _store;
    public PickCardsRequestHandler(RequestStore store) => _store = store;
    public void Apply  (PickCardsRequest ev) => _store.Insert(ev);
    public void Unapply(PickCardsRequest ev) => _store.Remove(ev.Id);
}

// Response handler — store remove on Apply; server-only Complete on TCS
public sealed class PickCardsResponseHandler
    : IWireEventHandler<PickCardsResponse>
{
    private readonly RequestStore _store;
    private readonly RequestService _service;   // null on client (not registered)
    public PickCardsResponseHandler(RequestStore store,
                                    RequestService service = null)
    { _store = store; _service = service; }

    public void Apply(PickCardsResponse ev)
    {
        _store.Remove(ev.RequestId);
        _service?.Complete(ev);
    }
    public void Unapply(PickCardsResponse ev) { /* see Deferred — needs cached request */ }
}
```

Per-concrete-type handlers is the verbose default. A generic base class
`RequestApplyHandler<TRequest>` / `ResponseApplyHandler<TResponse>` that
internalizes the Insert/Remove/Complete pattern is an obvious ergonomics
improvement once a game ships 3+ Request types — left as a follow-up.

### Layer (framework)

```csharp
public sealed class RequestInstaller : IInstaller
{
    private readonly bool _isServer;
    public RequestInstaller(bool isServer) => _isServer = isServer;

    public void Install(IContainerBuilder builder)
    {
        builder.Register<RequestStore>(Lifetime.Singleton);
        if (_isServer) builder.Register<RequestService>(Lifetime.Singleton);
    }
}
```

`FrameworkAppFlowRoot` adds `new RequestInstaller(_isServer)` alongside the
other layers. `EngineInstaller` exposes `GameEngine.Requests` on the server
side.

## Wire grammar example

Server opens a Pick-2-of-3 request; client responds; server resumes:

```
SERVER                                              CLIENT
------                                              ------
service.Open(PickCardsRequest{PickCount=2, ...})
  request.Id = 5
  pending[5] = PendingRequest<PickCardsResponse>
  emit PickCardsRequest{Id=5, ResponderId=42, ...}
    Handler.Apply → store.Insert(req5)
                                                    PickCardsRequest{Id=5,...} arrives
                                                    Handler.Apply → store.Insert(req5)
                                                    UI subscribes to store,
                                                    renders picker

                                                    Player picks; client emits
                                                    PickCardsResponse{RequestId=5,...}
                                                    Handler.Apply → store.Remove(5)
                                                                    (no service → no Complete)

PickCardsResponse{RequestId=5,...} arrives
Handler.Apply
  store.Remove(5)
  service.Complete(response)
    pending[5].Tcs.SetResult(response)
    pending.Remove(5)
awaiting effect resumes with typed PickCardsResponse
```

Both sides converge: RequestStore empty, server pending empty, the
awaiter holds the typed response.

## Test fixture (verification surface)

A concrete Request/Response pair lives in `Framework.Requests.Tests`:

```csharp
public partial class TestPickRequest
    : Request<TestPickRequest.Packed, TestPickResponse>
{
    [Packed] public int OptionCount;
}

public partial class TestPickResponse : Response<TestPickResponse.Packed>
{
    [Packed] public int Picked;
}

// plus TestPickRequestHandler / TestPickResponseHandler
```

Mirror test verifies the round-trip:

1. Server calls `service.Open(new TestPickRequest { OptionCount = 3 })` → returns `Task<TestPickResponse>`.
2. Assert both `server.RequestStore` and `client.RequestStore` contain the request (mirrored via wire).
3. Test simulates the client responding via the loopback wire.
4. Assert both stores drop the request.
5. Assert the `Task` resolves with the typed response (`response.Picked == 1`).

Concrete Request/Response types must register with the Network on both
sides (per the existing `Wire.Tests` pattern). A framework-side
convenience `RequestRegistration<TRequest, TResponse>` helper could
batch this — left as a follow-up if game-side bootstrap gets verbose.

## Deferred items

| Item | Lights up when |
|---|---|
| `int[]` / variable-length field handler — production `IPackingHandler` for arrays of entity IDs or definition refs | First real Request with an array field (mechanism validated 2026-05-30 via toy hash handler) |
| Generic base handlers (`RequestApplyHandler<TRequest>` / `ResponseApplyHandler<TResponse>`) to remove per-type boilerplate | 3+ concrete Request types ship in any single game |
| Cancellation flow — how `IsCancellable = true` translates to a Cancel command/event | First user-cancellable Request lands |
| Match-end pending cleanup — `RequestService.CancelAllPending()` hook | MatchService lands and emits a "match ended" signal |
| Unapply for Response handlers — needs the original Request still in scope to re-insert | Replay/scrubbing tooling matures (currently neither side scrubs across a response) |
| Client → server response delivery mechanism — the wire today is server → client one-direction (per WireForwarder/NetworkInboundBridge); responses need a symmetric path or separate intent submission | First multi-process integration test; tests today use loopback |

## Landed (2026-05-30, commit `8e03405`)

Step 5a build shipped exactly as scoped:

1. ✅ `Framework.Requests` asmdef (Runtime + Tests).
2. ✅ `Request` (non-generic, holds shared `[Packed]` metadata) +
   `Request<TResponse>` (typed wrapper) + `Response` (non-generic) abstract
   base classes.
3. ✅ `RequestStore` (`Dictionary<int, Request>`, both sides) /
   `RequestService` (server-only Open / Complete / CancelAllPending) /
   internal `PendingRequest<T>` + `IPendingRequest`.
4. ✅ `RequestInstaller` + `FrameworkAppFlowRoot` registration with
   role-gating (matches `TagInstaller` / `EntityInstaller` shape).
5. ✅ `GameEngine.Requests` + `GameEngine.RequestStore` properties
   (server-only handle).
6. ✅ Test fixture — `TestPickRequest` / `TestPickResponse` concrete pair,
   `TestPickRequestHandler` / `TestPickResponseHandler` apply handlers,
   `RequestServiceTests` with 6 scenarios:
   - Open inserts request on both sides; task is pending.
   - Response drops both stores + resolves typed task with the right payload.
   - Monotonic id allocation; out-of-order resolution wakes the right awaiter.
   - TResponse is typed at the call site (no cast / no dictionary).
   - `CancelAllPending` cancels parked TCSs.
   - Unmatched Response is a no-op.

`GameplayLayerTests` also updated: server scope resolves `Requests` /
`RequestStore`; client scope has `RequestStore` but `RequestService`
throws (the "client cannot author requests" structural rule).

**342/342 EditMode tests pass** (+6 new from `RequestServiceTests`).

Implementation matched the spec without forcing any shape adjustments —
no shape surprises during the build.
