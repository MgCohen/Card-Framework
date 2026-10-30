# Requests

Lets the server ask a player for a decision and `await` a typed answer.

**Code:** `Assets/Requests/` · assembly `Framework.Requests` · depends on `Framework.Wire`, `Scaffold.Autopacker`, `Scaffold.AppFlow`

## What it does
- Defines `Request` and `Response` as `WireEvent` base classes. `Request<TResponse>` ties a request to its answer type.
- `RequestService.Open` gives the request a monotonic id, emits it, and returns `Task<TResponse>`.
- `RequestStore` holds the open requests by id, on server and client, so UI can show what is pending.
- `RequestService.Complete` resolves the awaiter when a matching `Response` arrives. An unmatched response is a no-op.
- `CancelAllPending` cancels every awaiting task.

## How it works
```text
server: RequestService.Open(req)  -> id = next, pending[id] = TCS, Emit(req)
server: EventStream -> <caller's request handler> -> RequestStore.Insert(req)
        WireForwarder -> client: same handler -> client RequestStore.Insert(req)
server: Emit(response)  (today: emitted directly on the server stream)
server: <caller's response handler> -> RequestStore.Remove(id) + RequestService.Complete(response)
        client: same handler -> RequestStore.Remove(id)   (no service on the client)
awaiter: Task<TResponse> completes inline with the typed response
```

## Key types
| Type | Job |
|---|---|
| `Request` | Wire event base. Fields `Id`, `ParentId`, `SourceId`, `ResponderId`, `IsCancellable`. |
| `Request<TResponse>` | Binds a request to its response type for typed `await`. |
| `Response` | Wire event base. Field `RequestId`. |
| `RequestService` | Server-only. `Open`, internal `Complete`, `CancelAllPending`. |
| `RequestStore` / `IReadOnlyRequestStore` | Open requests by id. Internal `Insert` and `Remove`. |
| `PendingRequest<TResponse>` | Internal. Wraps the `TaskCompletionSource`. |

## Example
From `Assets/Testing/Integration/BootstrapWiringIntegrationTests.cs`:

```csharp
Task<DrawCardResponse> draw = engine.Requests.Open(
    new DrawCardRequest { ResponderId = player.Id, Count = 1 });
Assert.IsFalse(draw.IsCompleted, "request stays pending until answered");

engine.Stream.Emit(new DrawCardResponse { RequestId = 1, CardDefinitionId = 42, Cost = 3 });
var answer = await draw;
Assert.AreEqual(0, engine.RequestStore.Open.Count, "store drops the request on answer");
```

## Gaps
- The feature ships no apply handlers. Each game writes one handler per concrete `Request` and `Response` type. `RequestInstaller` registers only the store and service.
- The test fixture `DrawCardResponseHandler` calls `RequestService.Complete` from an apply handler. Rule ARCH-06 forbids that in runtime code (it exempts `Framework.Testing.*`), so the fixture pattern cannot move into the framework as is.
- No client to server path. A response the client sends reaches the server `Network` but never the server stream. `Client_Sent_Response_Does_Not_Complete_Task_Yet` pins this.
- `CancelAllPending` cancels the tasks but emits nothing, so both `RequestStore`s keep the cancelled requests. It also ignores `IsCancellable`.
- `ParentId` and `SourceId` are carried but never read.
