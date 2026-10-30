# Multiplayer

Runs the lobby: hosting or joining a session, seats, player data, ready flags and the match start signal.

**Code:** `Assets/Multiplayer/` · assembly `Framework.Multiplayer` · depends on `Framework.Wire`, `Scaffold.Autopacker`, `Unity.Netcode.Runtime`, `Unity.Netcode.Transports.UTP`, `Unity.Networking.Transport`, `Unity.Collections`, `UniTask`

## What it does
- Starts a session as solo (in-process loopback), as host, or as a joining client, over Netcode for GameObjects.
- Gives each peer a seat. The host is seat 0. Joiners get 1, 2, 3 and so on.
- Collects `PlayerData` from each peer. The host turns it into a `PlayerContext` through a `ContextResolver` and broadcasts it.
- Tracks ready flags and `AllReady`.
- Moves through `SessionState`: `Connecting`, `InLobby`, `InMatch`, `Disconnected`. Raises C# events for joins, leaves, context, ready, state and disconnect.

## How it works
Each `Session` owns its own `Network` and seven lobby channels. Lobby traffic does not go through the `EventStream`.

1. Host: `StartHost` calls `NetworkManager.StartHost`, takes seat 0 and enters `InLobby`.
2. Client: `Join` calls `StartClient`, enters `Connecting` and waits up to 10 s for a welcome.
3. Host, on connect: sends `PeerJoinedEvent` for existing peers, assigns a seat, sends `PeerWelcomeEvent` and `PeerJoinedEvent` for the newcomer, then replays known contexts and ready flags.
4. Client: on its `PeerWelcomeEvent`, records `Self` and enters `InLobby`.
5. Any peer: `SetSelfData` sends `PlayerDataEvent`. The host resolves it and sends `PlayerContextEvent`. Every peer stores the context.
6. Any peer: `SetSelfReady` sends `PeerReadyChangedEvent`. The host rebroadcasts each change.
7. Host: `StartMatch` sends `MatchStartEvent`. Every peer moves to `InMatch`.
8. `Dispose` raises `Disconnected(LocalShutdown)` once and shuts down and destroys the `NetworkManager`.

## Key types
| Type | Job |
|---|---|
| `Session<TData, TContext>` | The lobby. Start, join, lobby actions, events, teardown. |
| `Session` | Static shortcuts for `Session<PlayerData, PlayerContext>`. |
| `PeerRoster<TContext>` | Peers, contexts and ready flags by seat. |
| `Peer` | Seat, network id, `IsLocal`. `IsHost` means seat 0. |
| `PlayerData` / `PlayerContext` | What a peer submits / what the host decides. |
| `ContextResolver<TData, TContext>` | Host-side function from data to context. |
| `SessionState`, `DisconnectReason`, `JoinFailureKind` | State and failure enums. |
| `SessionStartupException`, `SessionJoinException` | Start and join failures. |
| `PeerWelcomeEvent`, `PeerJoinedEvent`, `PeerLeftEvent`, `PlayerDataEvent`, `PlayerContextEvent`, `PeerReadyChangedEvent`, `MatchStartEvent` | Lobby wire events. |

## Example
From `Assets/Multiplayer/PlayTests/SoloSessionTests.cs`:

```csharp
using var session = await Session.StartSolo();

session.SetSelfReady(true);
Assert.IsTrue(session.AllReady);

session.StartMatch();
Assert.AreEqual(SessionState.InMatch, session.State);
```

## Gaps
- `Multiplayer/Runtime/Session.cs` is about 430 lines. One class holds startup, NGO callbacks, channel wiring, lobby logic and teardown.
- Nothing connects a session to Bootstrap. Reaching `InMatch` does not build a `GameplayLayer`, and the session's `Network` is not the one the gameplay scope uses.
- `StartMatch` does not check `AllReady`. Only the sample UI does.
- Tests cover solo and a host on loopback. No test joins a second peer, so the welcome, join, leave and timeout paths are untested.
- Tests are PlayMode only (`Framework.Multiplayer.PlayTests`), so they run only with `-With playmode`.
