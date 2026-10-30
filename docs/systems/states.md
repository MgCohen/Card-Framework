# States

Tracks which phase the match is in, and mirrors that phase to every client.

**Code:** `Assets/States/` · assembly `Framework.States` · depends on `Framework.Core`, `Framework.Wire`, `Scaffold.Autopacker`, `Scaffold.AppFlow`, `Scaffold.Records`

## What it does
- Defines a game state as a `GameState` record keyed by an int slot (a `TypeKey`).
- Ships three baseline states: `MatchNotStartedState` (0), `PlayingState` (1), `MatchEndedState` (2).
- Holds one cursor, `StateStore.Current`, which starts on `MatchNotStartedState`.
- Changes the cursor through one slot-tagged event, `StatePushEvent`, on one wire channel.
- Lets readers see the cursor through `IReadOnlyStateStore`.

## How it works
1. Server: game code calls `StateService.Push<PlayingState>()`.
2. Server: the service emits `StatePushEvent { Slot = 1 }` into the `EventStream`.
3. Server: `HandlerRegistry` dispatches it to `StatePushHandler`, which looks the slot up in `GameStateRegistry` and calls the internal `StateStore.Set`.
4. `WireForwarder` sends the event to the client.
5. Client: `NetworkInboundBridge` emits it into the client stream, and the same `StatePushHandler` writes the client `StateStore`.

The client has no `StateService`. `StateInstaller` registers the service on the server only.

## Key types
| Type | Job |
|---|---|
| `GameState` | Abstract record. Base for every state, keyed by slot. |
| `MatchNotStartedState`, `PlayingState`, `MatchEndedState` | Baseline states. |
| `GameStateRegistry` | Maps slot to state instance. `WithBaselines()` registers the three baselines. |
| `StateService` | Server-only authoring API. `Push<TState>()` emits the event. |
| `StatePushEvent` | Wire event. Carries only `Slot`. |
| `StatePushHandler` | Internal apply handler. The only caller of `StateStore.Set`. |
| `StateStore` / `IReadOnlyStateStore` | Holds `Current`. |

## Example
From `Assets/States/Tests/StateServiceTests.cs`:

```csharp
var (server, client, _) = Build();

server.Service.Push<PlayingState>();
Assert.IsInstanceOf<PlayingState>(client.Store.Current);

server.Service.Push<MatchEndedState>();
Assert.IsInstanceOf<MatchEndedState>(server.Store.Current);
Assert.IsInstanceOf<MatchEndedState>(client.Store.Current);
```

## Gaps
- No transition rules. `Push` accepts any state in any order, including re-entering the current one.
- No enter or exit hooks. A push only overwrites `Current`.
- "Push" suggests a stack, but `StateStore` keeps only `Current`. There is no pop and no history.
- `StateInstaller` registers `GameStateRegistry.WithBaselines()` as a fixed instance. There is no installer hook for game-defined states.
- Nothing in runtime code calls `Push`. No game loop moves the match from `MatchNotStartedState` to `PlayingState`.
