---
type: spike
status: locked
created: 2026-05-20
updated: 2026-05-20
references:
  - Product/spikes/network-architecture.md
  - Assets/Framework/Wire/Runtime/Network/
sandbox: Assets/Framework/Multiplayer/
tags: [#spike, #multiplayer, #network, #lobby, #matchmaking, #design]
---

# Multiplayer Architecture

> Design pass on `Framework.Multiplayer` — the slice that owns peer lifecycle, lobby formation, player-data exchange, ready-up, and match-start signalling. Wire knows how bytes move once peers exist; Multiplayer is how peers come into existence and how they get coordinated up to the moment a match starts. **Gameplay traffic is explicitly NOT in scope** — that's the job of the gameplay layer Wire ships separately, layered on the same NetworkManager Multiplayer brings up. All decisions locked 2026-05-20 (revised from the 2026-05-20 `session-architecture.md` walk; see [§Provenance](#provenance) for the scope reshape).

## Goal

After this slice lands, a developer using the framework can write:

```csharp
// Local solo — in-process, no networking. For dev, bot harness, single-player.
var session = await Session.StartSolo();

// Online solo — server runs the simulation, one player connected. Same API.
var session = await Session.StartHostDirect(port: 7787);
// (or via Relay, see below — still single-peer when nobody joins)

// LAN / same-device — direct IP via NGO UTP.
var session = await Session.StartHostDirect(port: 7787);
var session = await Session.JoinDirect(ip: "192.168.1.42", port: 7787);

// Online — NGO UTP over Unity Relay. Host gets a join code to share.
var (session, joinCode) = await Relay.Host();
var session            = await Relay.Join(joinCode);

// Game-specific PlayerData / PlayerContext via generics:
var session = await Session.StartHostDirect<MyData, MyContext>(
    7787, contextResolver: data => new MyContext { ... });
```

…then exchange player data, gate match start on ready, and hand off to gameplay:

```csharp
session.SetSelfData(new MyData { DisplayName = "Alice", LoadoutId = "deck-3" });
session.SetSelfReady(true);

session.PlayerContextChanged += (peer, ctx) => RefreshLobbyRow(peer, ctx);
session.ReadyChanged         += (peer, ready) => RefreshReadyIcon(peer, ready);
session.StateChanged         += state =>
{
    if (state == SessionState.InMatch)
        BootGameplay(session.NetworkManager, session.Players);
};

if (session.IsHost && session.AllReady)
    session.StartMatch();
```

The gameplay layer then builds its own Wire `Network` on `session.NetworkManager` for moves, animations, etc. Multiplayer never sees that traffic.

The deliverable verifies end-to-end: a `Sample/` scene with Host / Join + lobby UI (player rows, ready toggles, Start button) that connects two devices, exchanges profiles, gates the match start on ready, and emits the start signal. Working that scene proves the lobby layer real on a real device against a real peer.

## What we are coming from

`Framework.Wire` already terminates cleanly at `ITransport`:

```
Network → Channel<T> → Dispatcher → ITransport
                                        ├── LoopbackTransport (in-process, paired)
                                        └── NgoTransport       (NGO named messages)
```

Both transports exist and work. `NgoTransportPlayTests.Host_Broadcast_RoundTrips_Through_NgoTransport` proves the NGO path round-trips a typed event in host-solo mode. There is **no path today from "I want to play" to "I have a connected match-ready session."** That entire gap is what Multiplayer fills.

A prior design pass (originally titled "Session Architecture", same file) sketched this as `Framework.Session` with a public `Session.Network` for gameplay channel registration. User clarification — "this part of the network should not be subscribing to gameplay events; this is matchmaking setup and everything around the game" — narrowed the scope and reshaped the slice. The membership protocol (PeerWelcome / Joined / Left) and NGO bring-up survive unchanged; the public surface drops `Network` and `EventStream`, adds player-data exchange, ready-up, and match-start.

The cleanly relevant pieces from Wire that Multiplayer consumes unchanged:

- `NgoTransport(NetworkManager nm, string messageName, ulong? targetClientId)` — Multiplayer constructs one internally for membership events with `messageName = "Framework.Multiplayer"`; gameplay constructs its own with a different name on the same NM.
- `LoopbackTransport.Pair()` / `LoopbackTransport.Solo()` — used directly for the Solo factory.
- Channel discriminator ordering convention: both ends `Register<T>()` the same types in the same order. Multiplayer's internal Wire `Network` only registers its membership/lobby/ready event types — the gameplay layer's `Network` is independent and lives in a separate transport.

## Scope boundary

| In scope | Out of scope |
|---|---|
| Peer connection lifecycle (bring-up, handshake, disconnect detection) | Gameplay event channels (moves, card plays, animations) |
| Roster (Peer / Self / Peers / IsHost) and seat assignment | Match logic, turn flow, win conditions |
| Player data exchange (PlayerData → PlayerContext via host resolver) | Match history / replay log (`EventStream` is gameplay's concern) |
| Ready-up (SetSelfReady / ReadyStates / AllReady) | Reconnect / backfill (deferred) |
| Match-start signalling (StartMatch + StateChanged transition to `InMatch`) | Post-match flow (someone won, return to lobby — deferred to a follow-up pass) |
| Lobby discovery (later milestone; shape locked, implementation deferred) | LAN auto-discovery (deferred) |
| NetworkManager ownership and bring-up across Direct / Relay / future paths | Dedicated server mode (deferred) |

The clean rule: if it happens **between "I want to play" and "the match has started"**, it's Multiplayer's job. Once `SessionState == InMatch`, Multiplayer steps back to "passive observer" — it still tracks peer disconnects and surfaces `Disconnected`, but it doesn't process ready toggles or profile updates anymore (locked in Q7).

## Slice layout

**One umbrella feature with sub-asmdefs** so optional Unity Services deps stay opt-in but related concepts live in one folder. (Reversed from the prior "two siblings" plan; see [§Provenance](#provenance).)

```
Assets/Framework/Multiplayer/
  Runtime/
    Framework.Multiplayer.asmdef               // core — no UGS deps
      Session.cs                               // generic: Session<TData, TContext> + non-generic default
      Peer.cs                                  // readonly struct: Seat, NetworkId, IsLocal, IsHost
      PlayerData.cs                            // base type clients submit
      PlayerContext.cs                         // base type host broadcasts (resolved)
      SessionState.cs                          // enum: Connecting | InLobby | InMatch | Disconnected
      DisconnectReason.cs                      // enum: LocalShutdown | HostShutdown | NetworkError | RelayError
      SessionStartupException.cs
      SessionJoinException.cs
      JoinFailureKind.cs                       // enum: NgoConnectFailed | Timeout | InvalidJoinCode | …
      Membership/
        PeerWelcomeEvent.cs                    // host → joiner: "you are seat N, your network id"
        PeerJoinedEvent.cs                     // host → all: "seat N joined"
        PeerLeftEvent.cs                       // host → all: "seat N left"
      Lobby/
        PlayerDataEvent.cs                     // client → host: submission
        PlayerContextEvent.cs                  // host → all: resolved broadcast (one per peer)
        PeerReadyEvent.cs                      // client → host: ready toggle
        ReadyBroadcastEvent.cs                 // host → all: aggregated ready set
        MatchStartEvent.cs                     // host → all: "transition to InMatch"
    Relay/
      Framework.Multiplayer.Relay.asmdef       // +UGS Relay/Auth/Core
        Relay.cs                               // public static: Host() / Join(joinCode)
        UnityServicesBoot.cs                   // idempotent init + anonymous sign-in
    Lobby/
      Framework.Multiplayer.Lobby.asmdef       // (later milestone) +UGS Lobby
    Discovery/
      Framework.Multiplayer.Discovery.asmdef   // (later milestone) browseable listings
  PlayTests/  Framework.Multiplayer.PlayTests.asmdef
    SoloSessionTests.cs
    DirectLoopbackTests.cs                     // host + client on 127.0.0.1, two NMs per test
    PeerRosterTests.cs                         // join/leave seat assignment
    PlayerDataTests.cs                         // submit → resolve → broadcast round-trip
    ReadyTests.cs                              // aggregation, AllReady, gating
    MatchStartTests.cs                         // host-only, state transition, event order
  Sample/     Framework.Multiplayer.Sample.asmdef
    LobbyHarness.cs                            // MonoBehaviour driving the lobby scene
    LobbyHarness.unity
```

Core asmdef references (`Framework.Multiplayer.asmdef`):

```
Framework.Wire
Unity.Netcode.Runtime
Unity.Netcode.Transports.UTP
Unity.Collections
Scaffold.Autopacker            // lobby events are WireEvents
UniTask
```

Relay sub-asmdef adds `Unity.Services.Core`, `Unity.Services.Authentication`, `Unity.Services.Relay`. Lobby/Discovery sub-asmdefs will add their respective UGS packages when those milestones land.

**Why one feature with sub-asmdefs.** Direct, Relay, future Lobby/Discovery are all *entry paths* into the same connection state machine — they share `Peer`, `Session`, `PlayerData`, `PlayerContext`, `JoinCode` (as a string). Splitting them into sibling features forced those types across `references:` boundaries; consolidating preserves the optional-dep property (sub-asmdefs) while keeping cohesion. Trade-off accepted: `docs/feature-layout.md` needs a small amendment to legitimize multi-asmdef features.

### How the sub-asmdefs bolt together

Multiplayer core exposes two public "bring-up from a pre-configured NetworkManager" hooks. The Direct factories use them internally; Relay configures the NM with relay allocation data and then calls into the same hooks. Single bring-up path, no duplicated lifecycle code.

```csharp
// In Framework.Multiplayer (core)
public class Session<TData, TContext>
    where TData    : PlayerData,    new()
    where TContext : PlayerContext, new()
{
    public static UniTask<Session<TData, TContext>> StartSolo(
        ContextResolver<TData, TContext> contextResolver = null);

    public static UniTask<Session<TData, TContext>> StartHostDirect(
        ushort port,
        string listenAddress = "0.0.0.0",
        ContextResolver<TData, TContext> contextResolver = null);

    public static UniTask<Session<TData, TContext>> JoinDirect(string ip, ushort port);

    // Public hooks — Relay (or any custom transport setup) uses these.
    public static UniTask<Session<TData, TContext>> StartHost(
        NetworkManager preConfigured,
        ContextResolver<TData, TContext> contextResolver = null);
    public static UniTask<Session<TData, TContext>> Join(NetworkManager preConfigured);

    // Public NM factory — Relay reuses this; no InternalsVisibleTo needed.
    public static (NetworkManager nm, UnityTransport utp) CreateNetworkManager();
}

// Non-generic default for the common case (no game-specific extension).
public sealed class Session : Session<PlayerData, PlayerContext> { }

public delegate TContext ContextResolver<in TData, out TContext>(TData submission)
    where TData    : PlayerData
    where TContext : PlayerContext;

// In Framework.Multiplayer.Relay
public static class Relay
{
    public static async UniTask<(Session<TData, TContext> session, string joinCode)>
        Host<TData, TContext>(
            int maxConnections = 4,
            ContextResolver<TData, TContext> contextResolver = null)
        where TData    : PlayerData,    new()
        where TContext : PlayerContext, new()
    {
        await UnityServicesBoot.EnsureSignedIn();
        var (nm, utp) = Session.CreateNetworkManager();
        var allocation = await RelayService.Instance.CreateAllocationAsync(maxConnections);
        var joinCode   = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
        utp.SetRelayServerData(new RelayServerData(allocation, "dtls"));
        var session = await Session<TData, TContext>.StartHost(nm, contextResolver);
        return (session, joinCode);
    }

    public static async UniTask<Session<TData, TContext>> Join<TData, TContext>(string joinCode)
        where TData    : PlayerData,    new()
        where TContext : PlayerContext, new()
    {
        await UnityServicesBoot.EnsureSignedIn();
        var (nm, utp) = Session.CreateNetworkManager();
        var alloc = await RelayService.Instance.JoinAllocationAsync(joinCode);
        utp.SetRelayServerData(new RelayServerData(alloc, "dtls"));
        return await Session<TData, TContext>.Join(nm);
    }

    // Convenience overloads using the non-generic default Session / PlayerData / PlayerContext exist alongside.
}
```

`Session.CreateNetworkManager()` is the one piece Relay needs to share with the core — a public factory returning a fresh `(NetworkManager, UnityTransport)` on a `DontDestroyOnLoad` GameObject. Everything else stays inside the appropriate sub-asmdef.

## Public API surface

```csharp
// Framework.Multiplayer (core)
public class Session<TData, TContext> : IDisposable
    where TData    : PlayerData,    new()
    where TContext : PlayerContext, new()
{
    // Bridge to gameplay. Gameplay constructs its own Wire Network on a separate
    // NgoTransport(this.NetworkManager, "Game") — see Q2 verdict and side note in §Provenance.
    public NetworkManager NetworkManager { get; }

    // Identity & roster.
    public Peer Self { get; }
    public IReadOnlyList<Peer> Peers { get; }
    public bool IsHost => Self.IsHost;

    // Lobby state.
    public SessionState State { get; }
    public IReadOnlyDictionary<int, TContext> Players { get; }    // seat → resolved context
    public IReadOnlyDictionary<int, bool> ReadyStates { get; }    // seat → ready
    public bool AllReady { get; }

    // Events. All raised on the Unity main thread.
    public event Action<Peer> PeerJoined;
    public event Action<Peer> PeerLeft;
    public event Action<Peer, TContext> PlayerContextChanged;     // includes self when host broadcasts
    public event Action<Peer, bool> ReadyChanged;
    public event Action<SessionState> StateChanged;
    public event Action<DisconnectReason> Disconnected;

    // Lobby actions.
    public void SetSelfData(TData data);                          // any peer, lobby phase
    public void SetSelfReady(bool ready);                         // any peer, lobby phase
    public void StartMatch();                                     // host-only; requires State == InLobby

    // Factories (see Composition section).
    public static UniTask<Session<TData, TContext>> StartSolo(ContextResolver<TData, TContext> resolver = null);
    public static UniTask<Session<TData, TContext>> StartHostDirect(ushort port, string listenAddress = "0.0.0.0", ContextResolver<TData, TContext> resolver = null);
    public static UniTask<Session<TData, TContext>> JoinDirect(string ip, ushort port);
    public static UniTask<Session<TData, TContext>> StartHost(NetworkManager preConfigured, ContextResolver<TData, TContext> resolver = null);
    public static UniTask<Session<TData, TContext>> Join(NetworkManager preConfigured);
    public static (NetworkManager nm, UnityTransport utp) CreateNetworkManager();

    public void Dispose();   // tears down NGO, transport, NetworkManager GO
}

// Non-generic default. Use this when game doesn't need to extend PlayerData/PlayerContext.
public sealed class Session : Session<PlayerData, PlayerContext> { }

// Framework.Multiplayer.Relay
public static class Relay
{
    public static UniTask<(Session session, string joinCode)> Host(int maxConnections = 4);
    public static UniTask<Session> Join(string joinCode);

    // Generic counterparts for games extending PlayerData/PlayerContext:
    public static UniTask<(Session<TData, TContext> session, string joinCode)>
        Host<TData, TContext>(int maxConnections = 4, ContextResolver<TData, TContext> resolver = null)
        where TData : PlayerData, new() where TContext : PlayerContext, new();
    public static UniTask<Session<TData, TContext>> Join<TData, TContext>(string joinCode)
        where TData : PlayerData, new() where TContext : PlayerContext, new();
}

public readonly struct Peer
{
    public int Seat { get; }              // 0 = host, 1..N joiners in connection order
    public ulong NetworkId { get; }       // NGO ClientId; 0 for solo
    public bool IsLocal { get; }
    public bool IsHost => Seat == 0;
}

// Base player types. Games subclass these for extra fields (and pass the subclass as the Session generic).
public class PlayerData
{
    public ulong NetworkId { get; set; }
    public string PlayerId { get; set; }    // app-level unique id (e.g. account id)
    public string DisplayName { get; set; }
}

public class PlayerContext
{
    public ulong NetworkId { get; set; }
    public int Seat { get; set; }
    public string PlayerId { get; set; }
    public string DisplayName { get; set; }
}

public enum SessionState { Connecting, InLobby, InMatch, Disconnected }

public enum DisconnectReason
{
    LocalShutdown,        // we called Dispose
    HostShutdown,         // (client only) host went away cleanly
    NetworkError,         // UTP error, timeout, etc.
    RelayError,           // (relay only) allocation or join failed mid-flight
}
```

Channel registration on Multiplayer's internal `NgoTransport` follows Wire's existing rule: both ends `Register<T>()` the same membership/lobby event types in the same order. Gameplay's own `Network` (on a separate `NgoTransport(nm, "Game")`) has its own discriminator namespace and never collides — NGO routes by message name (one bucket per transport).

## Lobby data flow — submit / resolve / broadcast

The flow when a player updates their data (initial submission or any subsequent edit):

```
client                                      host (server-authoritative)
------                                      ----
SetSelfData(myData)
  send PlayerDataEvent(myData) → host
                                              receive PlayerDataEvent →
                                                _pendingSubmissions[seat] = myData
                                                resolved = contextResolver(myData)
                                                _players[seat] = resolved
                                                broadcast PlayerContextEvent(resolved)
                                                raise PlayerContextChanged(peer, resolved) locally

all peers (including the submitter and host self) receive PlayerContextEvent →
  _players[ev.Seat] = ev.context
  raise PlayerContextChanged(peer, ev.context)
```

**Key properties:**

- **Server-authoritative.** Only the host runs `contextResolver`. Clients never construct their own `PlayerContext` — they receive the resolved version. This is what lets the host expand thin client submissions ("I picked loadout-3") into rich contexts ("loadout-3 = these 30 cards with these stats") before anyone sees them.
- **One-way fanout.** Client → host submission is a single message; host → all broadcast is one message per resolved peer (multiple peers' contexts can also be batched into a `PlayerContextEvent[]` if measurement shows it helps — defer).
- **Re-submissions are idempotent at the protocol level.** Each `PlayerDataEvent` replaces the prior submission for that seat; resolver runs again; broadcast fires again. Lightweight enough for things like "player switched decks in the lobby."
- **Default identity case.** If a game doesn't supply a `contextResolver`, the default is `data => new PlayerContext { /* copy base fields */ }`. The framework's `Session<PlayerData, PlayerContext>` default works out of the box without server-side expansion logic.
- **Extension via generics.** Game declares `MyData : PlayerData` and `MyContext : PlayerContext` with extra fields, uses `Session<MyData, MyContext>`. Subclass-with-cast-at-boundary was the user's preferred shape but is deferred — Wire's `WireEvent` has no polymorphic discriminator on the wire (channel id is type-scoped, not subclass-aware). The generic-typed Session sidesteps that until polymorphic wire events land. See `bl-polymorphic-wire-events` in OPEN-ITEMS once the cascade lands.

## Ready-up

```
client                                      host
------                                      ----
SetSelfReady(true)
  send PeerReadyEvent(true) → host
                                              receive PeerReadyEvent →
                                                _ready[seat] = ready
                                                broadcast ReadyBroadcastEvent(seat, ready)
                                                raise ReadyChanged(peer, ready) locally

all peers receive ReadyBroadcastEvent →
  _ready[seat] = ready
  raise ReadyChanged(peer, ready)
```

`AllReady` returns `true` when every peer in `Peers` has a `_ready[seat] == true`. Host can gate its "Start Match" button on `IsHost && AllReady`, but the framework doesn't enforce that — `StartMatch()` only requires `State == InLobby`; the application chooses whether to require unanimous ready.

Resubmitting `SetSelfReady(true)` when already ready is a no-op. Resubmitting a different value is broadcast normally. Disconnecting clears the entry from `_ready` (via `PeerLeftEvent`).

Ready is a separate channel from PlayerData/Context (rather than a field on `PlayerData`) because it flips multiple times in a lobby; bundling it into PlayerData would force a full context re-broadcast on every toggle. Lightweight wire shape pays for itself.

## Match start

Host only:

```csharp
public void StartMatch()
{
    if (!IsHost) throw new InvalidOperationException("StartMatch is host-only");
    if (State != SessionState.InLobby) throw new InvalidOperationException($"StartMatch requires InLobby, got {State}");

    broadcast MatchStartEvent();
    TransitionTo(SessionState.InMatch);     // raises StateChanged
}

// On every peer (host self-echo + clients):
void OnMatchStart(MatchStartEvent ev)
{
    if (State != SessionState.InLobby) return;       // self-echo or replay safety
    TransitionTo(SessionState.InMatch);
}
```

After the transition:

- `Session` stops processing `SetSelfData` and `SetSelfReady` (calls throw `InvalidOperationException`).
- `PeerJoinedEvent` / `PeerLeftEvent` still flow — peer disconnects during a match are tracked and surface via `PeerLeft` and `Disconnected`.
- The gameplay layer is expected to bootstrap on `StateChanged → InMatch` and start its own `Network` on `session.NetworkManager`.

What happens *after* a match (someone won, return to lobby, retire the session) is **deferred to a follow-up pass.** The terminal state Multiplayer models for now is `InMatch`. A future design walk will decide whether Session re-enters `InLobby`, fires an `OnMatchEnded`, exposes a `LastMatchOutcome`, etc. — likely entangled with the framework's existing state-machine work.

## Composition (per-mode)

Every factory ends up at the same shape — `NetworkManager` ready to host Multiplayer's internal `NgoTransport` (and, in parallel, the gameplay layer's). They differ only in how the transport is brought up. Direct and Relay both flow through the same `StartHost(nm)` / `Join(nm)` core.

### Solo

Trivial. No NGO involvement. Single peer.

```
LoopbackTransport solo = LoopbackTransport.Solo()
session._network = new Network(solo)                   // internal — for lobby events only
register membership + lobby + ready + match-start channels
Self = Peer(seat 0, networkId 0, local, host)
Peers = [Self]
State = InLobby
contextResolver = caller-supplied (or default identity)
```

`Players` and `ReadyStates` are populated as the local peer calls `SetSelfData` / `SetSelfReady` — solo's degenerate transport echoes sends back to the same process, so the host-side resolver runs and the broadcast lands on the same Session instance. `AllReady` is trivially true once the single peer is ready. `StartMatch()` works the same way.

### Direct host

```
1. Create GameObject "Framework.Multiplayer.NetworkManager".
2. Add NetworkManager + UnityTransport components.
3. utp.SetConnectionData(listenAddress, port, listenAddress);
4. nm.NetworkConfig = new NetworkConfig { NetworkTransport = utp, TickRate = 30 };
5. Wire NGO callbacks (OnClientConnected/Disconnected) BEFORE StartHost.
6. nm.StartHost();
7. transport = new NgoTransport(nm, "Framework.Multiplayer");     // registers handler
8. _network = new Network(transport);
9. Register membership + lobby + ready + match-start channels
10. roster.AddHost(self at seat 0, NetworkId = nm.LocalClientId)
11. State = InLobby
```

`NetworkManager` property exposes `nm` so gameplay can build its own `Network` on a separate `NgoTransport(nm, "Game")` later.

### Direct join

Same as host but `utp.SetConnectionData(ip, port)` and `nm.StartClient()`. Joiner registers the membership/lobby/ready channels synchronously after `StartClient` returns (closes the race where the host's first sends arrive before the joiner's handler is registered). State starts at `Connecting`, transitions to `InLobby` on Welcome.

### Relay host / Relay join (lives in `Framework.Multiplayer.Relay`)

Same as Direct except UTP is configured via `SetRelayServerData(...)` instead of `SetConnectionData(...)`. See the snippet in [§How the sub-asmdefs bolt together](#how-the-sub-asmdefs-bolt-together).

## Identity & seats

Seats are session-scoped, monotonic, host-assigned. The host is canonically seat 0; joiners receive their seat number from the host on connect.

**Why host-authoritative seat assignment.** Joiners cannot infer their own seat — they don't know who else is connected when they arrive, and NGO's `ClientId` is an opaque transport handle, not a gameplay identity. Host owns the roster, broadcasts changes, gates state transitions.

**Flow on join:**

```
Host                                                       Joining client
----                                                       ---------------
NetworkManager.OnClientConnectedCallback(id)
  for each existing peer P in roster:                      (NGO completed handshake)
    broadcast PeerJoinedEvent {                            NetworkManager.OnClientConnectedCallback(localId)
      seat = P.Seat, networkId = P.NetworkId                 // we now know our NetworkId
    }                                                        State = Connecting
  roster.AddClient(seat = next, networkId = id)              (waiting for Welcome)
  broadcast PeerWelcomeEvent {                             channel.OnReceived(PeerJoined for P) →
    yourSeat = newSeat, yourNetworkId = id                   if state == Connecting: silently add to roster
  }                                                          (no PeerJoined event raise during Connecting)
  broadcast PeerJoinedEvent {                              channel.OnReceived(Welcome) →
    seat = newSeat, networkId = id                           if YourNetworkId != localId: drop
  }                                                          Self = Peer(yourSeat, localId, true)
  raise PeerJoined(newPeer) locally                          add Self to roster
                                                             State = InLobby
                                                             // app code can now SetSelfData/SetSelfReady
existing clients receive backfill PeerJoinedEvents          channel.OnReceived(PeerJoined for newPeer) →
  → already in roster, drop                                  ev.NetworkId == localId → drop (self-echo)
existing clients receive Welcome →
  YourNetworkId != localId → drop
existing clients receive PeerJoinedEvent(newPeer) →
  not in roster, add, raise PeerJoined(newPeer)
host self-echo on its own broadcasts →
  ev.NetworkId == localId or seat already in roster → drop
```

Receive-side contract: a `PeerJoinedEvent` arriving while `State == Connecting` populates the roster silently. `PeerJoined` raises only fire once the joiner transitions to `InLobby`, and only for peers that arrive after that point. Pre-existing peers are just facts queryable via `Peers`. Keeps user code from seeing a flurry of join events at startup.

**Flow on leave:**

```
Host                                                       Other clients
----                                                       --------------
NetworkManager.OnClientDisconnectCallback(id)
  peer = roster.FindByNetworkId(id)
  roster.Remove(peer)
  _players.Remove(peer.Seat)
  _ready.Remove(peer.Seat)
  broadcast PeerLeftEvent { seat = peer.Seat }             channel.OnReceived(Left) →
  raise PeerLeft(peer) locally                               roster.RemoveBySeat(seat)
                                                             _players.Remove(seat); _ready.Remove(seat)
                                                             raise PeerLeft(peer)
```

**Flow when host vanishes (clients):**

```
NetworkManager.OnClientDisconnectCallback(localClientId)
  State = Disconnected
  raise Disconnected(HostShutdown or NetworkError)
  // if NetworkManager.DisconnectReason is non-empty → HostShutdown; else → NetworkError
```

Seats never get recycled within a session. If seat 2 leaves and a new peer joins, they get seat 3. Keeps causal logs unambiguous. Reconnect (deferred) will eventually let a seat be reclaimed by the same identity, but identity-token plumbing is out of scope here.

## Membership + lobby events

First-class `WireEvent`s registered on Multiplayer's internal `Network` before any user channels. They use `[AutoPack]` like every other wire event. Concrete shapes:

```csharp
[AutoPack] public partial class PeerWelcomeEvent : WireEvent<PeerWelcomeEvent.Packed>
{
    [Packed] public int YourSeat;
    [Packed] public ulong YourNetworkId;
}

[AutoPack] public partial class PeerJoinedEvent : WireEvent<PeerJoinedEvent.Packed>
{
    [Packed] public int Seat;
    [Packed] public ulong NetworkId;
}

[AutoPack] public partial class PeerLeftEvent : WireEvent<PeerLeftEvent.Packed>
{
    [Packed] public int Seat;
}

// Lobby channels. Concrete shapes parameterized on TData/TContext via the generic Session.
// In the default (non-generic) Session these resolve to PlayerData/PlayerContext directly.

[AutoPack] public partial class PlayerDataEvent : WireEvent<PlayerDataEvent.Packed>
{
    [Packed] public PlayerData Data;          // game subclass via generic Session<TData, TContext>
}

[AutoPack] public partial class PlayerContextEvent : WireEvent<PlayerContextEvent.Packed>
{
    [Packed] public PlayerContext Context;
}

[AutoPack] public partial class PeerReadyEvent : WireEvent<PeerReadyEvent.Packed>
{
    [Packed] public bool Ready;
}

[AutoPack] public partial class ReadyBroadcastEvent : WireEvent<ReadyBroadcastEvent.Packed>
{
    [Packed] public int Seat;
    [Packed] public bool Ready;
}

[AutoPack] public partial class MatchStartEvent : WireEvent<MatchStartEvent.Packed> { }
```

All events broadcast through Multiplayer's shared internal `NgoTransport`. `PeerWelcomeEvent` is logically point-to-point but broadcast-with-filter rather than targeted — receivers check `ev.YourNetworkId == _nm.LocalClientId` and gate on `State == Connecting`. ~16 bytes of overhead per peer join; lets us avoid a transient per-recipient transport (NgoTransport's ctor registers a named-message handler keyed on message name, so two transports with the same name would conflict).

## End-to-end flows

### Solo

```
A.StartSolo() →
  build LoopbackTransport.Solo()
  Network over it (internal — for lobby events only)
  register membership + lobby + ready + match-start channels
  Self = Peer(seat 0, networkId 0, local, host)
  Peers = [Self]
  State = InLobby
caller calls SetSelfData(myData) →
  echo loops back; resolver runs; _players[0] = resolved
caller calls SetSelfReady(true) →
  echo loops back; _ready[0] = true; AllReady = true
caller calls StartMatch() →
  MatchStartEvent echoes; State = InMatch
caller wires gameplay on session.NetworkManager (degenerate — no NGO bring-up actually happened in Solo;
  gameplay must handle the "no NetworkManager" case or use the same LoopbackTransport)
A.Dispose() → release loopback (nothing to tear down)
```

Note: in Solo, `session.NetworkManager` is `null` (or a no-op stand-in) since no NGO is involved. Gameplay layer's Solo bring-up is its own concern; the convention is that gameplay's Solo path also uses a loopback rather than building NGO. Worth flagging as a small open thread for the gameplay layer to design against.

### Direct two-peer round trip with lobby

```
HostApp.StartHostDirect(7787, contextResolver: data => Resolve(data))
  → Session in InLobby state, Self = (0, hostNgoId, host)
JoinApp.JoinDirect(host.lan.ip, 7787)
  → Session in Connecting state, waiting for Welcome
host receives OnClientConnectedCallback(joinerNgoId)
  broadcast PeerJoinedEvent(seat=0, nid=hostNgoId)   // backfill
  roster.AddClient(seat=1, nid=joinerNgoId)
  broadcast PeerWelcomeEvent(yourSeat=1, yourNetworkId=joinerNgoId)
  broadcast PeerJoinedEvent(seat=1, nid=joinerNgoId)
  raise PeerJoined(joiner) locally
joiner receives (in NGO send order):
  PeerJoinedEvent(host)   → silent roster add
  PeerWelcomeEvent        → Self = (1, ...), State = InLobby
  PeerJoinedEvent(joiner) → self-echo, drop
HostApp.SetSelfData(new MyData { DisplayName = "Alice", LoadoutId = "deck-3" })
  → PlayerDataEvent broadcast; resolver runs; PlayerContextEvent broadcast for seat 0
JoinApp.SetSelfData(new MyData { DisplayName = "Bob", LoadoutId = "deck-1" })
  → host receives, resolver runs, broadcasts PlayerContextEvent for seat 1
  → both peers now have Players[0], Players[1] populated and matching
HostApp.SetSelfReady(true) → ReadyBroadcastEvent(0, true) fanout
JoinApp.SetSelfReady(true) → host receives PeerReadyEvent, broadcasts ReadyBroadcastEvent(1, true)
  → AllReady = true on both sides
HostApp.StartMatch()
  → broadcasts MatchStartEvent
  → State = InMatch on both peers; StateChanged fires
  → gameplay layer on each peer builds its own Network on session.NetworkManager
```

### Relay two-peer round trip

Same as Direct except for the bring-up tail described above. Wire/NGO cannot tell the difference once UTP is configured — NGO + UTP + RelayServerData looks identical at the named-message layer.

### Host disconnect (clients side)

```
Host calls session.Dispose() →
  nm.Shutdown() (sends NGO disconnect to clients)
  destroy NM GameObject
  raise Disconnected(LocalShutdown) locally
Client NM raises OnClientDisconnectCallback(localId) →
  State = Disconnected; StateChanged fires
  raise Disconnected(HostShutdown)
  // user code is expected to surface a "lobby left" UI and Dispose the session
```

### Client disconnect (host side)

```
Client calls session.Dispose() →
  nm.Shutdown()
  raise Disconnected(LocalShutdown) locally
Host NM raises OnClientDisconnectCallback(clientId) →
  roster.RemoveByNetworkId(clientId)
  _players.Remove(seat); _ready.Remove(seat)
  broadcast PeerLeftEvent(seat)
  raise PeerLeft(peer) locally
other clients receive PeerLeftEvent →
  roster.RemoveBySeat; _players.Remove(seat); _ready.Remove(seat)
  raise PeerLeft locally
```

## Error model

All factory methods are `UniTask<…>` and throw on failure. The factory catches mid-flight failures, cleans up the partial state it created (GameObject, NM, Relay allocation, signed-in services), and rethrows a typed exception.

```csharp
public sealed class SessionStartupException : Exception {
    public SessionStartupException(string msg, Exception inner = null) : base(msg, inner) {}
}

public sealed class SessionJoinException : Exception {
    public JoinFailureKind Reason { get; }
    public SessionJoinException(JoinFailureKind reason, string msg, Exception inner = null) : base(msg, inner) { Reason = reason; }
}

public enum JoinFailureKind { NgoConnectFailed, Timeout, InvalidJoinCode, AllocationNotFound, RelayUnavailable }
```

Relay failure mapping (RelayService throws `RelayServiceException` with a `Reason` enum we translate). Auth failure is its own thing — `EnsureSignedIn` catches and wraps. NGO `StartHost`/`StartClient` returning `false` becomes `NgoConnectFailed`. Connect timeout is enforced by the factory: 10 s default, configurable per-mode via overloads later.

Disconnect *after* `State == InLobby` (or `InMatch`) doesn't throw — it raises `Disconnected(reason)` and flips state. Calls to `SetSelfData` / `SetSelfReady` / `StartMatch` after disconnect throw `InvalidOperationException("session disconnected")` synchronously.

## Threading model

NGO callbacks fire on the Unity main thread. Relay/Services SDK use `UniTask` — `await` resumes on the main thread by default. All Session events are raised on the main thread; user code can touch Unity API freely in handlers.

Session keeps no background threads. No locks. Single-threaded by construction.

## NetworkManager ownership

Session creates its own `GameObject` with `NetworkManager` + `UnityTransport` attached. Marked `DontDestroyOnLoad` so scene loads inside a session don't kill the connection. Disposed on `session.Dispose()`.

If the project already has a scene-placed `NetworkManager.Singleton`, `CreateNetworkManager()` refuses to start (`SessionStartupException`) rather than fighting it. An overload `Session.AttachTo(NetworkManager existing)` is conceivable later; not in scope here.

Gameplay code accesses the NM via `session.NetworkManager` and constructs its own `NgoTransport(nm, "Game")` for gameplay traffic. The two transports coexist on the same NM because NGO routes by message name (one named-message bucket per transport).

## UnityServicesBoot (in `Framework.Multiplayer.Relay`)

Idempotent helper. Many sessions in one process must not re-initialize.

```csharp
internal static class UnityServicesBoot
{
    static UniTask<bool> _booted;

    public static UniTask EnsureSignedIn()
    {
        if (_booted.Status == UniTaskStatus.Succeeded) return UniTask.CompletedTask;
        if (_booted.Status != UniTaskStatus.Pending) _booted = BootOnce();
        return _booted.ContinueWith(_ => { });
    }

    static async UniTask<bool> BootOnce()
    {
        await UnityServices.InitializeAsync();
        if (!AuthenticationService.Instance.IsSignedIn)
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
        return true;
    }
}
```

First Relay call in a process pays the auth cost (~few hundred ms typical). Subsequent Relay calls return instantly. Solo and Direct modes never touch this — UGS is loaded lazily, only when the Relay sub-asmdef is imported.

## Sample harness

`LobbyHarness.unity` — single scene. UI Toolkit or uGUI, whichever is already idiomatic in the project (TBD on first contact).

UI:

```
[ status: <state> | seat <self.seat> | peers: <count> | allReady: <bool> ]
[ Host Solo    ]
[ Host LAN     ] [ Port:  7787 ]
[ Join LAN     ] [ IP: 127.0.0.1 ] [ Port: 7787 ]
[ Host Relay   ]                              → shows generated join code
[ Join Relay   ] [ Code: ____ ]

— lobby phase (State == InLobby) —
[ DisplayName: ____ ] [ LoadoutId: ____ ] [ Update profile ]
[ Ready ☐ / Ready ☑ ]
[ Player rows: ]
  ┌─────────────────────────────────────────────────────┐
  │ Seat │ NetworkId │ DisplayName │ LoadoutId │ Ready │
  ├─────────────────────────────────────────────────────┤
  │ 0    │ ...       │ Alice       │ deck-3    │ ☑     │
  │ 1    │ ...       │ Bob         │ deck-1    │ ☐     │
  └─────────────────────────────────────────────────────┘
[ Start Match ] (host-only; enabled when AllReady)

— match phase (State == InMatch) —
[ "Match started — gameplay layer takes over from here." ]
[ Send Probe ]   (gameplay-layer test: registers a channel on session.NetworkManager and round-trips)
[ Log: scrolling list of session + gameplay events ]
```

This scene is the manual deliverable that proves "I can lobby and start a match with someone." Two builds (or editor + build) on two devices → Host LAN on one, Join LAN on the other → both set profiles → both ready → host starts → both transition to `InMatch`. Same with Relay across networks.

## Tests

### Framework.Multiplayer.PlayTests (in-editor)

- `SoloSessionTests`
  - StartSolo gives `Self = seat 0, host=true, local=true`, `State == InLobby`.
  - `SetSelfData` triggers resolver and populates `Players[0]`.
  - `SetSelfReady` triggers aggregation and flips `AllReady` to true.
  - `StartMatch` transitions to `InMatch`; `StateChanged` raised.
  - Dispose flips state to `Disconnected`, raises event once.

- `DirectLoopbackTests`
  - Two `Session`s in the same EditMode test, both pointed at `127.0.0.1:<random port>`. Host on one, Join on the other.
  - Roster after join: both see two peers, agree on seats.
  - `SetSelfData` round-trips host → client → host (via resolver) and client → host → all peers (via resolver).
  - `SetSelfReady` aggregation: when both peers ready, `AllReady` is true on both sides.
  - `StartMatch` from host: both peers transition to `InMatch`.
  - Client `Dispose` → host raises `PeerLeft` with correct seat; host's `Players`/`ReadyStates` shrink.
  - Host `Dispose` → client raises `Disconnected(HostShutdown)`.

- `PeerRosterTests`
  - Three peers join sequentially; seats are 0, 1, 2.
  - Middle peer leaves; new joiner becomes seat 3 (no recycling).

- `PlayerDataTests`
  - Game-specific `Session<MyData, MyContext>` with custom resolver.
  - Submitting `MyData { LoadoutId = "deck-3" }` produces a `MyContext` with resolved loadout details broadcast to all peers.
  - Re-submission replaces prior data; re-broadcast fires.

- `ReadyTests`
  - `AllReady` false when only some peers ready.
  - `AllReady` true when all peers ready (including host).
  - Disconnect of a non-ready peer reaching `AllReady` (other peers all ready).
  - Re-ready of already-ready is a no-op (no extra event).

- `MatchStartTests`
  - Non-host calling `StartMatch` throws.
  - Host calling `StartMatch` when `State != InLobby` throws.
  - Host calling `StartMatch` from `InLobby` transitions all peers to `InMatch` and raises `StateChanged`.
  - Calls to `SetSelfData` / `SetSelfReady` after match start throw.

NGO requires a real `NetworkManager` per peer in the same process; UTP supports this since loopback connections multiplex by port. Two NMs in one test use distinct ports. (Note: multi-NM-per-process under NGO is asserted by NGO docs but worth empirical verification on first run; if it fails, fall back to single-NM tests using Wire's `LoopbackTransport.Pair` to exercise the protocol logic.)

### Framework.Multiplayer.Relay.PlayTests

- `UnityServicesBootTests` — idempotent boot semantics, no network calls. Real Relay round-trip needs creds we don't have in CI; manual verification via the Sample scene is the truth.

### What we don't test in EditMode

- Real Relay round-trip — needs Unity Services creds. Manual via Sample scene.
- Real LAN multi-device — same; manual verification on the Sample scene.

## Out of scope

Captured explicitly so they don't accidentally creep in. Tracked in OPEN-ITEMS / FOLLOW-UPS on landing:

- **Discovery (browseable lobby listings).** Locked in scope conceptually (Q1=C); implementation deferred to a later milestone. When it lands, it'll live in `Framework.Multiplayer.Discovery` and produce join codes that feed back into the existing factories — no surface changes to core Session.
- **LAN discovery (UDP broadcast / mDNS).** Direct mode requires manual IP entry. Adding mDNS / broadcast discovery is a clean later addition; doesn't change the Session API.
- **Post-match flow.** What happens after `StartMatch` — return to lobby, retire session, expose match outcome. Deferred to a follow-up design pass; likely entangled with the framework's existing state-machine work.
- **Polymorphic player data extension.** Q5 picked generic-typed Session over subclass-with-cast because Wire has no polymorphic discriminator on `WireEvent`. Worth revisiting as a Wire-layer enhancement; tracked as `bl-polymorphic-wire-events` in OPEN-ITEMS.
- **Reconnect / backfill.** Needs durable peer identity, sequence numbers on WireEvent, host-side log retention. Each is a separate pass.
- **Dedicated server.** Phase 2. Same Session API, "host process with no local player attached." Out of scope until needed.
- **Spectators.** No "non-player" peers in phase 1. Add a `Peer.Role` enum later.
- **Multiple simultaneous sessions in one process.** API doesn't preclude it, but NGO's `NetworkManager.Singleton` does. Out of scope until we have a reason.
- **Bot drivers.** Out of scope per project decision — bots live above the Session API, pushing intents through normal gameplay code.
- **In-lobby chat.** Could ride Multiplayer's internal `NgoTransport` later as another lobby-phase channel, but no current requirement. Defer.

## Decision summary

| # | Decision | Lock |
|---|---|---|
| 1 | Slice shape | Umbrella `Framework.Multiplayer` with sub-asmdefs (`Framework.Multiplayer` core, `Framework.Multiplayer.Relay`, future `.Lobby` / `.Discovery`). Reversed from the prior sibling-features plan. |
| 2 | Scope | Connection + roster + lifecycle + lobby orchestration + (later) discovery. **Gameplay channels are NOT in scope** — gameplay layer builds its own Wire `Network` on `session.NetworkManager`. |
| 3 | Authority model (phase 1) | Host-authoritative listen-server. |
| 4 | Online infra | Unity Relay only (in the Relay sub-asmdef). |
| 5 | Local play | Direct IP — covers same-LAN-two-devices and same-device-two-clients. |
| 6 | Solo | Single-peer multiplayer over `LoopbackTransport.Solo()`. Same API. "Online solo" (server + 1) reduces to Direct/Relay with one peer. |
| 7 | Seat assignment | Host = 0; joiners ascending in connect order; no recycling. |
| 8 | Membership events | `PeerWelcome` (broadcast, receiver-filtered), `PeerJoined`, `PeerLeft`. |
| 9 | Player data shape | Server-authoritative two-phase: client submits `PlayerData`, host's `contextResolver` builds `PlayerContext`, host broadcasts to all. |
| 10 | Player data extension | Generic-typed `Session<TData, TContext>` with `Session` default at `<PlayerData, PlayerContext>`. Subclass-with-cast deferred (tracked as `bl-polymorphic-wire-events`). |
| 11 | Ready-up | First-class API: `SetSelfReady`, `ReadyStates`, `AllReady`, `ReadyChanged`. Separate channel from PlayerData/Context (dynamic state, cheap to broadcast). |
| 12 | Match start | Host-only `StartMatch()`. `SessionState` widened to `Connecting / InLobby / InMatch / Disconnected` with generic `StateChanged` event. Post-match deferred. |
| 13 | Handoff to gameplay | `session.NetworkManager` exposed as a property. Gameplay constructs its own `NgoTransport(nm, "Game")` and `Network` on top. NGO routes by message name; the two transports never collide. |
| 14 | EventStream relationship | Session has **no** `EventStream`. Events surface via `.NET` events only. EventStream is purely a gameplay concept. |
| 15 | Discovery shape | Decoupled via join-code primitive. Discovery lives in its own sub-asmdef (later milestone); communicates with Session only via the join-code string. |
| 16 | Async surface | `UniTask` on all factories. |
| 17 | Threading | Main thread only. |
| 18 | NetworkManager ownership | Session creates and owns its NM GameObject; `Session.CreateNetworkManager()` is the public factory the Relay sub-asmdef (or any custom transport setup) reuses. |
| 19 | UGS init | Idempotent, lazy; lives in `Framework.Multiplayer.Relay`, never paid by core consumers. |
| 20 | Error model | Factories throw `SessionStartupException` / `SessionJoinException(JoinFailureKind)`; in-session failures raise `Disconnected(reason)`. |
| 21 | Sequence numbers on WireEvent | Deferred (no phase-1 wire-format change). |
| 22 | Sample scene | One lobby harness with Host/Join/profile/ready/StartMatch UI — manual cross-device verification deliverable. |
| 23 | `Session` shape | Plain `sealed class : IDisposable`. Owns its `NetworkManager` GameObject as an implementation detail. |
| 24 | `PeerWelcomeEvent` delivery | Broadcast through the shared `NgoTransport`. Receivers filter via `ev.YourNetworkId == _nm.LocalClientId` and gate on `State == Connecting`. ~16 bytes overhead per peer-join; no Wire changes. |
| 25 | Out of scope | Discovery impl (deferred), LAN auto-discovery, post-match flow, polymorphic player data, reconnect/backfill, dedicated server, spectators, multi-session, bots, lobby chat. |

## Provenance

This file supersedes `session-architecture.md` (same path, renamed 2026-05-20). The original locked design treated `Framework.Session` as a host for gameplay channels — public `Session.Network` was the central surface. User clarification narrowed scope to matchmaking-adjacent concerns and broadened it to include lobby / profile / ready / match-start. The 9-Q `/builder:design-pass` walk on 2026-05-20 reshaped the slice: umbrella feature `Framework.Multiplayer` with sub-asmdefs (Q4), no `Network` on the public surface (Q2), no `EventStream` either (Q9), widened `SessionState` (Q7), generic-typed `Session<TData, TContext>` (Q5), first-class ready (Q6), full matchmaking scope with discovery deferred (Q1, Q3).

What survived the reshape: the membership protocol (Welcome / Joined / Left + backfill + receiver-filter), NGO bring-up via `CreateNetworkManager` + `StartHost(nm)` / `Join(nm)`, the Direct factories, seat assignment, threading model, error model, NetworkManager ownership, UnityServicesBoot (now under the Relay sub-asmdef).

## Implementation status

The patch cascade landed on branch `claude/basic-network-implementation-DOA3z` between 2026-05-20 commits `8d5b89a` (mechanical rename) and `7557f68` (sample cleanup). Compileable first cut covers: core types (`Session<TData, TContext>` + `Session` facade, `Peer`, `PlayerData`, `PlayerContext`, `ContextResolver`, `SessionState`, `DisconnectReason`, `JoinFailureKind`, `SessionStartupException`, `SessionJoinException`), all 7 wire events (3 membership + 4 lobby), the host/joiner handshake protocol, the resolve/broadcast lobby flow, ready aggregation with host re-broadcast, match-start signalling, PlayTests for Solo + host-only Direct, and an OnGUI sample harness driver.

**Deviations from the spec above as shipped:**

- `PlayerData` / `PlayerContext` ship with `ulong NetworkId` + `ulong PlayerId` only. The spec sketched `string DisplayName` too, but AutoPacker has no native string support — strings need an `IPackingHandler` the framework doesn't ship. Folded into the polymorphic-wire-events deferral.
- `Session` is a `static class` facade rather than `sealed class Session : Session<PlayerData, PlayerContext>`. Inheritance would propagate the generic class's static return types (`UniTask<Session<PlayerData, PlayerContext>>`) into the facade signatures, which reads as a leaky abstraction. Same call-site experience either way.
- `MatchStartEvent` carries a `byte Reserved` field — uncertainty whether AutoPacker generates valid code for zero-`[Packed]` events. Placeholder until verified.

**Not yet shipped (need Editor or external decisions):**

- Compile + EditMode test verification.
- `Framework.Multiplayer.Relay` sub-asmdef + `Relay.cs` + `UnityServicesBoot.cs` — gated on adding `com.unity.services.{core, authentication, relay}` to `Packages/manifest.json`.
- `LobbyHarness.unity` scene file — Editor-authored wrapper around the existing `LobbyHarness.cs` MonoBehaviour.
- Multi-NM-per-process PlayTests (`PeerRosterTests`, two-peer `DirectLoopbackTests` round-trip, cross-peer `PlayerDataTests` / `ReadyTests`) — need empirical Editor verification that NGO permits the pattern.
- `bl-polymorphic-wire-events` OPEN-ITEMS entry + Multiplayer / PlayerData / PlayerContext / LobbyPhase / MatchStarting / Ready glossary additions — deferred housekeeping.
