---
type: spike
status: locked
created: 2026-05-20
references:
  - Product/spikes/network-architecture.md
  - Assets/Framework/Wire/Runtime/Network/
sandbox: Assets/Framework/Session/, Assets/Framework/Relay/
tags: [#spike, #session, #network, #design]
---

# Session Architecture

> Design pass on `Framework.Session` (+ sibling `Framework.Relay`) — the slices that own peer lifecycle, discovery, identity, and the bridge into `Framework.Wire`. Wire knows how bytes move once peers exist; Session is how peers come into existence. **All decisions locked 2026-05-20** — implementation is mechanical from here. This doc is the working spec.

## Goal

After this slice lands, a developer using the framework can write:

```csharp
// Solo — in-process, no networking. For dev, bot harness, single-player.
var session = await Session.StartSolo();

// LAN / same-device — direct IP via NGO UTP.
var session = await Session.StartHostDirect(port: 7787);
var session = await Session.JoinDirect(ip: "192.168.1.42", port: 7787);

// Online — NGO UTP over Unity Relay. Host gets a join code to share.
var (session, joinCode) = await Session.StartHostRelay();
var session = await Session.JoinRelay(joinCode);
```

…then register channels on `session.Network` exactly as Wire's tests do today, and play. One uniform API across solo, LAN, same-device, and online. No special-casing in gameplay code for which mode is active.

The deliverable verifies end-to-end: a `Sample/` scene with Host / Join buttons that opens a session, registers a channel, and sends a probe event. Working that scene proves the connection layer real on a real device against a real peer.

## What we are coming from

`Framework.Wire` already terminates cleanly at `ITransport`:

```
Network → Channel<T> → Dispatcher → ITransport
                                        ├── LoopbackTransport (in-process, paired)
                                        └── NgoTransport       (NGO named messages)
```

Both transports exist and work. `NgoTransportPlayTests.Host_Broadcast_RoundTrips_Through_NgoTransport` proves the NGO path round-trips a typed event — but only in host-solo mode, with NGO already started by the test setup. There is **no path today from "I want to play" to "I have a connected `Network`."** That entire gap is what Session fills.

The cleanly relevant pieces from Wire that Session consumes unchanged:

- `Network` (Wire owns channel registry + dispatcher).
- `EventStream` (append-only log of `WireEvent`).
- `NgoTransport(NetworkManager nm, string messageName, ulong? targetClientId)` — constructed *after* NGO is listening.
- `LoopbackTransport.Pair()` — used directly for solo.
- Channel discriminator ordering convention: both ends must `Register<T>()` the same types in the same order (Q-C in `network-architecture.md`). Session does not change this — but it does reserve the first two channel ids for itself (see Identity below).

## Slice layout

**Split into two sibling features** so Relay's three Unity Services packages stay an opt-in cost. Each feature is a clean vertical slice per `docs/feature-layout.md`.

### Framework.Session — core (no Services deps)

Covers solo, LAN, same-device, and anyone who pre-configures a `NetworkManager` themselves (including Relay).

```
Assets/Framework/Session/
  Runtime/    Framework.Session.asmdef
    Session.cs                 // façade — public factories + private bring-up helpers
    Peer.cs                    // readonly struct: Seat, NetworkId, IsLocal, IsHost
    SessionState.cs            // enum: Connecting | Connected | Disconnected
    DisconnectReason.cs        // enum: LocalShutdown | HostShutdown | NetworkError
    Membership/
      PeerWelcomeEvent.cs      // host → joiner (point-to-point): "you are seat N, roster"
      PeerJoinedEvent.cs       // host → all: "seat N joined"
      PeerLeftEvent.cs         // host → all: "seat N left"
  PlayTests/  Framework.Session.PlayTests.asmdef
    SoloSessionTests.cs
    DirectLoopbackTests.cs     // host + client on 127.0.0.1, two NetworkManagers per test
    PeerRosterTests.cs         // join/leave seat assignment
  Sample/     Framework.Session.Sample.asmdef
    HostJoinHarness.cs         // MonoBehaviour driving the Host/Join scene
    HostJoinHarness.unity
```

Asmdef references (Runtime):

```
Framework.Wire
Unity.Netcode.Runtime
Unity.Netcode.Transports.UTP
Unity.Collections
Scaffold.Autopacker            // membership events are WireEvents
UniTask
```

No `Modes/` folder — bring-up logic (configure UTP for direct ip:port, call StartHost/Client, build NgoTransport) is private static helpers inside `Session.cs`. No `PeerRoster` type — Session keeps a private `List<Peer>` directly. Trims ~6 files vs. the first sketch with zero loss of clarity.

### Framework.Relay — opt-in online

Depends on `Framework.Session`. Adds the Relay factories. Anyone who doesn't want Unity Services doesn't import this package.

```
Assets/Framework/Relay/
  Runtime/    Framework.Relay.asmdef
    Relay.cs                   // public static — Host() / Join(joinCode)
    UnityServicesBoot.cs       // idempotent UnityServices.Initialize + anonymous sign-in
    RelayDisconnectReason.cs   // (if needed beyond DisconnectReason — TBD)
  PlayTests/  Framework.Relay.PlayTests.asmdef
    UnityServicesBootTests.cs  // idempotent boot, no network — covers what we can in CI
    // Real Relay round-trip is manual via the Session Sample scene + creds; not in CI.
```

Asmdef references (Runtime):

```
Framework.Session
Unity.Netcode.Runtime
Unity.Netcode.Transports.UTP
Unity.Services.Core
Unity.Services.Authentication
Unity.Services.Relay
UniTask
```

### How they bolt together

`Session` exposes two public "bring-up from a pre-configured NetworkManager" hooks. The direct/solo factories use them internally; `Relay` configures NM with relay allocation data and then calls into the same hooks. Single bring-up path, no duplicated lifecycle code, no `InternalsVisibleTo`.

```csharp
// In Framework.Session
public sealed class Session
{
    public static UniTask<Session> StartSolo();
    public static UniTask<Session> StartHostDirect(ushort port, string listenAddress = "0.0.0.0");
    public static UniTask<Session> JoinDirect(string ip, ushort port);

    // Public hooks — Relay (or any custom transport setup) uses these.
    public static UniTask<Session> StartHost(NetworkManager preConfigured);
    public static UniTask<Session> Join(NetworkManager preConfigured);
}

// In Framework.Relay
public static class Relay
{
    public static async UniTask<(Session session, string joinCode)> Host(int maxConnections = 4)
    {
        await UnityServicesBoot.EnsureSignedIn();
        var (nm, utp) = Session.CreateNetworkManager();              // public helper on Session
        var allocation = await RelayService.Instance.CreateAllocationAsync(maxConnections);
        var joinCode   = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
        utp.SetRelayServerData(new RelayServerData(allocation, "dtls"));
        var session = await Session.StartHost(nm);
        return (session, joinCode);
    }

    public static async UniTask<Session> Join(string joinCode)
    {
        await UnityServicesBoot.EnsureSignedIn();
        var (nm, utp) = Session.CreateNetworkManager();
        var alloc = await RelayService.Instance.JoinAllocationAsync(joinCode);
        utp.SetRelayServerData(new RelayServerData(alloc, "dtls"));
        return await Session.Join(nm);
    }
}
```

`Session.CreateNetworkManager()` is a small public factory that returns a fresh `(NetworkManager, UnityTransport)` pair on a `DontDestroyOnLoad` GameObject — the one piece Relay needs to share with Session. Everything else stays inside Session.

## Public API surface

```csharp
// Framework.Session
public sealed class Session : IDisposable
{
    // Wire side — gameplay registers channels here, exactly as today.
    public Network Network { get; }
    public EventStream Stream { get; }

    // Identity.
    public Peer Self { get; }
    public IReadOnlyList<Peer> Peers { get; }
    public bool IsHost => Self.IsHost;

    // State.
    public SessionState State { get; }

    // Events. Raised on the Unity main thread.
    public event Action<Peer> PeerJoined;
    public event Action<Peer> PeerLeft;
    public event Action<DisconnectReason> Disconnected;

    // Factories. All async; startup failures throw, post-connect drops raise Disconnected.
    public static UniTask<Session> StartSolo();
    public static UniTask<Session> StartHostDirect(ushort port, string listenAddress = "0.0.0.0");
    public static UniTask<Session> JoinDirect(string ip, ushort port);

    // Lower-level hooks. Used by Framework.Relay and any custom transport setup.
    public static UniTask<Session> StartHost(NetworkManager preConfigured);
    public static UniTask<Session> Join(NetworkManager preConfigured);
    public static (NetworkManager nm, UnityTransport utp) CreateNetworkManager();

    public void Dispose();   // tears down NGO, transport, NetworkManager GO
}

// Framework.Relay
public static class Relay
{
    public static UniTask<(Session session, string joinCode)> Host(int maxConnections = 4);
    public static UniTask<Session> Join(string joinCode);
}

public readonly struct Peer
{
    public int Seat { get; }              // 0 = host, 1..N joiners in connection order
    public ulong NetworkId { get; }       // NGO ClientId; 0 for solo
    public bool IsLocal { get; }
    public bool IsHost => Seat == 0;
}

public enum SessionState  { Connecting, Connected, Disconnected }

public enum DisconnectReason
{
    LocalShutdown,        // we called Dispose
    HostShutdown,         // (client only) host went away cleanly
    NetworkError,         // UTP error, timeout, etc.
    RelayError,           // (relay only) allocation or join failed mid-flight
}
```

Channel registration on `session.Network` follows Wire's existing rule: same types in the same order on host and joiner. Session reserves discriminator ids 0–2 for its own membership events (registered in the Session constructor before the user touches `Network`); user channels start at id 3.

## Composition (per-mode)

Every factory ends up at the same shape — `Network` over `ITransport`. They differ only in how the transport is brought up. Direct and Relay both flow through the same `Session.StartHost(nm) / Session.Join(nm)` core — the slice splits only at the "configure the NetworkManager" step.

### Solo

Trivial. No NGO involvement.

```
LoopbackTransport (a, _) = LoopbackTransport.Pair()
// We only use 'a' — there is no peer. Session.Self is a single host peer at seat 0.
session._network = new Network(a);
session._roster.AddHost(self);
session.State = Connected;
```

`Send` on an unpaired loopback would NRE on `_peer`. Solo never calls send-to-peer — but to be safe, the solo path uses a degenerate transport that no-ops `Send`. We add a `LoopbackTransport.Solo()` static returning a single transport with `_peer = self` so writes silently land in the same process's own receive path (host echoes to itself; tests that use solo mode see their own sends). This keeps the gameplay code identical to multi-peer mode.

### Direct host

```
1. Create GameObject "Framework.Session.NetworkManager".
2. Add NetworkManager + UnityTransport components.
3. utp.SetConnectionData(listenAddress, port, listenAddress);
4. nm.NetworkConfig = new NetworkConfig { NetworkTransport = utp, TickRate = 30 };
5. Wire NGO callbacks (OnClientConnected/Disconnected/ServerStarted) BEFORE StartHost.
6. nm.StartHost();
7. await UntilNgoReady (callback or one frame yield).
8. transport = new NgoTransport(nm);     // registers named message handler
9. _network = new Network(transport);
10. _network.Register<PeerWelcomeEvent>();   // ids 0–2 reserved
    _network.Register<PeerJoinedEvent>();
    _network.Register<PeerLeftEvent>();
11. roster.AddHost(self at seat 0, NetworkId = nm.LocalClientId);
12. State = Connected.
```

### Direct join

Same as host but `utp.SetConnectionData(ip, port)` and `nm.StartClient()`. On `OnClientConnectedCallback(localClientId)`, register the membership channels (same order as host), set state to `Connecting`. Stay in `Connecting` until `PeerWelcomeEvent` arrives from the host — only then does the joiner know its `Self.Seat` and the current roster. State flips to `Connected` at that point.

### Relay host (lives in Framework.Relay)

```csharp
await UnityServicesBoot.EnsureSignedIn();                                // initialize + anonymous auth, idempotent
var (nm, utp) = Session.CreateNetworkManager();                          // public helper on Session
var allocation = await RelayService.Instance.CreateAllocationAsync(maxConnections);
var joinCode   = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
utp.SetRelayServerData(new RelayServerData(allocation, "dtls"));
var session = await Session.StartHost(nm);                               // shared bring-up
return (session, joinCode);
```

Any failure between `EnsureSignedIn` and `StartHost` surfaces as a thrown exception; partial state (GameObject created, services half-booted) gets cleaned up in a `try/catch` before rethrowing.

### Relay join (lives in Framework.Relay)

```csharp
await UnityServicesBoot.EnsureSignedIn();
var (nm, utp) = Session.CreateNetworkManager();
var alloc = await RelayService.Instance.JoinAllocationAsync(joinCode);
utp.SetRelayServerData(new RelayServerData(alloc, "dtls"));
return await Session.Join(nm);
```

## Identity & seats

Seats are session-scoped, monotonic, host-assigned. The host is canonically seat 0; joiners receive their seat number from the host on connect.

**Why host-authoritative seat assignment.** Joiners cannot infer their own seat — they don't know who else is connected when they arrive, and NGO's `ClientId` is an opaque transport handle, not a gameplay identity. Host owns the roster, broadcasts changes, gates state transitions.

**Flow on join:**

```
Host                                                       Joining client
----                                                       ---------------
NetworkManager.OnClientConnectedCallback(id)
  roster.AddClient(seat = next, networkId = id)            (NGO completed handshake)
  send PeerWelcomeEvent {                                  NetworkManager.OnClientConnectedCallback(localId)
    yourSeat = newSeat,                                      // we now know our NetworkId
    roster = [(seat0, hostId), (seat1, ...), ...]            State = Connecting
  } directly to id                                           (waiting for Welcome)
  broadcast PeerJoinedEvent {                              channel.OnReceived(Welcome) →
    seat = newSeat, networkId = id                           Self = Peer(yourSeat, localId, true)
  } to everyone else                                         roster = welcome.roster
                                                             State = Connected
                                                             raise PeerJoined for each existing peer
                                                             // app code can now Send/Register safely
existing clients receive PeerJoinedEvent →
  roster.AddClient(...)
  raise PeerJoined(newPeer)
```

**Flow on leave:**

```
Host                                                       Other clients
----                                                       --------------
NetworkManager.OnClientDisconnectCallback(id)
  peer = roster.FindByNetworkId(id)
  roster.Remove(peer)
  broadcast PeerLeftEvent { seat = peer.Seat }             channel.OnReceived(Left) →
  raise PeerLeft(peer) locally                               roster.RemoveBySeat(seat)
                                                             raise PeerLeft(peer)
```

**Flow when host vanishes (clients):**

```
NetworkManager.OnClientDisconnectCallback(localClientId)   // NGO surfaces this to clients
  State = Disconnected
  raise Disconnected(HostShutdown or NetworkError)
  // distinguishing the two: if host sent a clean Shutdown, NGO sets DisconnectReason
  // string on NetworkManager; we map "" / null → NetworkError and anything else → HostShutdown.
```

Seats never get recycled within a session. If seat 2 leaves and a new peer joins, they get seat 3. Keeps causal logs unambiguous. Reconnect (deferred) will eventually let a seat be reclaimed by the same identity, but identity-token plumbing is out of scope here.

## PeerWelcomeEvent / PeerJoinedEvent / PeerLeftEvent

These are first-class `WireEvent`s registered on the Session's `Network` before user channels. They use `[AutoPack]` like every other wire event:

```csharp
[AutoPack]
public sealed partial class PeerWelcomeEvent : WireEvent
{
    public int    YourSeat;
    public ulong  YourNetworkId;
    public RosterEntry[] Roster;   // existing peers including host
}

[AutoPack]
public readonly partial struct RosterEntry
{
    public readonly int   Seat;
    public readonly ulong NetworkId;
}

[AutoPack] public sealed partial class PeerJoinedEvent : WireEvent { public int Seat; public ulong NetworkId; }
[AutoPack] public sealed partial class PeerLeftEvent   : WireEvent { public int Seat; }
```

`PeerWelcomeEvent` is the only message sent point-to-point (host → one joiner) — that's why `NgoTransport` already takes an optional `ulong? targetClientId`. Session constructs a *second* transient `NgoTransport(nm, messageName, targetClientId: joinerId)` for the welcome write, or — simpler — sends through the same shared transport but tags the Welcome event so others ignore it. **Decision: use a per-recipient transient transport for Welcome.** Cleaner separation; no "ignore if not for me" branching in the receive path. The transient transport shares NGO's named-message channel with the broadcast transport — NGO multiplexes by recipient on its side.

`PeerJoinedEvent` and `PeerLeftEvent` broadcast normally through the shared transport.

## End-to-end flows

### Solo

```
A.StartSolo() →
  build LoopbackTransport.Solo()
  Network over it
  register 3 membership channels (no-op — no one to send to, but maintains discriminator alignment)
  Self = Peer(seat 0, networkId 0, local, host)
  Peers = [Self]
  State = Connected
  return session
caller registers user channels, runs game.
A.Dispose() → release loopback (nothing to tear down).
```

### Direct two-peer round trip

```
HostApp.StartHostDirect(7787) → Session in Connected state, Self = (0, hostNgoId, host).
JoinApp.JoinDirect(host.lan.ip, 7787) →
  GameObject + NM + UTP + connection data set
  hook OnClientConnectedCallback
  StartClient
  NGO completes UDP handshake
  callback fires (localClientId known)
  build NgoTransport, build Network, register 3 membership channels
  State = Connecting, wait for Welcome
host receives OnClientConnectedCallback(joinerNgoId)
  roster.AddClient(seat=1, nid=joinerNgoId)
  send PeerWelcomeEvent point-to-point
  broadcast PeerJoinedEvent (only host hears it here — joiner gets it via Welcome)
joiner receives Welcome → Self = (1, ...), roster filled, State = Connected, raise PeerJoined(host)
host raises PeerJoined(joiner) locally
both sides can now session.Network.Send(...) any registered user event and it round-trips.
```

### Relay two-peer round trip

Same as Direct except for the bring-up tail described above. The Wire layer cannot tell the difference — NGO + UTP + RelayServerData looks identical at the named-message layer.

### Host disconnect (clients side)

```
Host calls session.Dispose() →
  nm.Shutdown() (sends NGO disconnect to clients)
  destroy NM GameObject
  raise Disconnected(LocalShutdown) locally
Client NM raises OnClientDisconnectCallback(localId) →
  raise Disconnected(HostShutdown)
  State = Disconnected
  Network is now dead — sends will throw; receives stop
  user code is expected to surface a "lobby left" UI and Dispose the session
```

### Client disconnect (host side)

```
Client calls session.Dispose() →
  nm.Shutdown()
  raise Disconnected(LocalShutdown) locally
Host NM raises OnClientDisconnectCallback(clientId) →
  roster.RemoveByNetworkId(clientId)
  broadcast PeerLeftEvent(seat)
  raise PeerLeft(peer) locally
other clients receive PeerLeftEvent →
  roster.RemoveBySeat
  raise PeerLeft locally
```

## Error model

All factory methods are `UniTask<…>` and throw on failure. The factory is responsible for catching mid-flight failures, cleaning up the partial state it created (GameObject, NM, Relay allocation, signed-in services), and rethrowing a typed exception.

Custom exception types kept narrow:

```csharp
public sealed class SessionStartupException : Exception {
    public SessionStartupException(string msg, Exception inner = null) : base(msg, inner) {}
}

public sealed class SessionJoinException : Exception {
    public enum Kind { InvalidJoinCode, AllocationNotFound, RelayUnavailable, NgoConnectFailed, Timeout }
    public Kind Reason { get; }
    public SessionJoinException(Kind reason, string msg, Exception inner = null) : base(msg, inner) { Reason = reason; }
}
```

Relay failure mapping (RelayService throws `RelayServiceException` with a `Reason` enum we translate). Auth failure is its own thing — `EnsureSignedIn` catches and wraps. NGO `StartHost`/`StartClient` returning `false` becomes `NgoConnectFailed`. Connect timeout is enforced by the factory: 10 s default, configurable per-mode via overloads later.

Disconnect *after* `State = Connected` doesn't throw — it raises `Disconnected(reason)` and flips state. Sends after disconnect throw `InvalidOperationException("session disconnected")` synchronously.

## Threading model

NGO callbacks fire on the Unity main thread. Relay/Services SDK use `UniTask` — `await` resumes on the main thread by default. All Session events are raised on the main thread; user code can touch Unity API freely in handlers.

Session keeps no background threads. No locks. Single-threaded by construction.

## NetworkManager ownership

Session creates its own `GameObject` with `NetworkManager` + `UnityTransport` attached. Marked `DontDestroyOnLoad` so scene loads inside a session don't kill the connection. Disposed on `session.Dispose()`.

If the project already has a scene-placed `NetworkManager.Singleton`, Session refuses to start (`SessionStartupException("a NetworkManager is already active")`) rather than fighting it. An overload `Session.AttachTo(NetworkManager existing)` is conceivable later; not in scope here.

## UnityServicesBoot

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

First Relay call in process pays the auth cost (~few hundred ms typical). Subsequent Relay calls return instantly. Solo and Direct modes never touch this — Unity Services is loaded lazily, only when needed.

## Sample harness

`HostJoinHarness.unity` — single scene. UI Toolkit or uGUI, whichever is already idiomatic in this project (TBD on first contact).

UI:

```
[ status: <state> | <self> | <peer count> ]
[ Host Solo    ]
[ Host LAN     ] [ Port:  7787 ]
[ Join LAN     ] [ IP: 127.0.0.1 ] [ Port: 7787 ]
[ Host Relay   ]                              → shows generated join code
[ Join Relay   ] [ Code: ____ ]
[ Send Probe   ]   (after connected — registers one channel and round-trips)
[ Log: scrolling list of session + wire events ]
```

This scene is the manual deliverable that proves "I can play with someone." Two builds (or editor + build) on two devices, Host LAN on one, Join LAN on the other → green status on both → Send Probe round-trips. Same with Relay, two devices on different networks.

## Tests

### Framework.Session.PlayTests (in-editor)

- `SoloSessionTests`
  - StartSolo gives `Self = seat 0, host=true, local=true`.
  - User channel registration + send-receive (loopback echo).
  - Dispose flips state to `Disconnected`, raises event once.
- `DirectLoopbackTests`
  - Two `Session`s in the same EditMode test, both pointed at `127.0.0.1:<random port>`. Host on one, Join on the other.
  - Roster after join: both see two peers, agree on seats.
  - User event round-trips host → client and client → host.
  - Client `Dispose` → host raises `PeerLeft` with correct seat.
  - Host `Dispose` → client raises `Disconnected(HostShutdown)`.
- `PeerRosterTests`
  - Three peers join sequentially; seats are 0, 1, 2.
  - Middle peer leaves; new joiner becomes seat 3 (no recycling).

NGO requires a real `NetworkManager` per peer in the same process; UTP supports this since loopback connections multiplex by port. Two NMs in one test use distinct ports.

### Framework.Relay.PlayTests

- `UnityServicesBootTests` — idempotent boot semantics, no network calls. Real Relay round-trip needs creds we don't have in CI; manual verification via the Sample scene is the truth.

### What we don't test in EditMode

- Real Relay round-trip — needs Unity Services creds. Manual via Sample scene.
- Real LAN multi-device — same; manual verification on the Sample scene.

## Out of scope

Captured explicitly so they don't accidentally creep in. All of these go into a `FOLLOW-UPS.md` at the slice root on landing:

- **LAN discovery (UDP broadcast).** Direct mode requires manual IP entry. Adding mDNS / broadcast discovery is a clean later addition; doesn't change the Session API.
- **Reconnect / backfill.** Needs (a) durable peer identity (token per peer survives a transport drop), (b) sequence numbers on `WireEvent` so a reconnecting peer can ask for "everything after N", (c) host-side log retention beyond `EventStream` (already retains everything in-memory; durability TBD). Each is a separate pass. Wire-format break for sequence numbers is acknowledged and accepted.
- **Dedicated server.** Phase 2. Same Session API, just "host process with no local player attached." Relay goes away; UTP listens on a public address; auth/identity stops being anonymous.
- **Spectators.** No "non-player" peers in phase 1. Add a `Peer.Role` enum later.
- **Multiple simultaneous sessions in one process.** API doesn't preclude it, but NGO's `NetworkManager.Singleton` does. Out of scope until we have a reason.
- **Bot drivers.** Out of scope per project decision — bots live above the Session API, pushing intents into a solo or host session through normal gameplay code.

## Decision summary

| # | Decision | Lock |
|---|---|---|
| 1 | Slice split | `Framework.Session` (core) + `Framework.Relay` (opt-in online); each a full vertical slice with Runtime + PlayTests + Sample |
| 2 | Authority model (phase 1) | Host-authoritative listen-server |
| 3 | Online infra | Unity Relay only |
| 4 | Local play | Direct IP — covers same-LAN-two-devices and same-device-two-clients |
| 5 | Solo | Loopback transport, single self-echoing peer |
| 6 | Seat assignment | Host = 0; joiners ascending in connect order; no recycling |
| 7 | Membership events | `PeerWelcome` (point-to-point), `PeerJoined`, `PeerLeft` |
| 8 | Channel id reservation | Session reserves ids 0–2; user channels from 3 |
| 9 | Async surface | `UniTask` on all factories |
| 10 | Threading | Main thread only |
| 11 | NetworkManager ownership | Session creates and owns its NM GameObject; `Session.CreateNetworkManager()` is the public factory the Relay slice (or any custom transport) reuses |
| 12 | UnityServices init | Idempotent, lazy; lives in `Framework.Relay`, never paid by Session-only consumers |
| 13 | Error model | Factories throw; in-session failures raise `Disconnected(reason)` |
| 14 | Sequence number on WireEvent | Deferred (no phase-1 wire-format change) |
| 15 | Sample scene | One Host/Join harness — manual cross-device verification deliverable |
| 16 | `Session` shape | Plain `sealed class : IDisposable`. Owns its `NetworkManager` GameObject as an implementation detail — Session itself is a service, not a scene object |
| 17 | `EventStream` ownership | Session owns one and auto-appends every received `WireEvent` from `Network.OnReceived` — the log "just exists" once connected; users who don't care can ignore it |
| 18 | `PeerWelcomeEvent` delivery | Transient per-recipient `NgoTransport` for the point-to-point write. No recipient-filter branching in the receive path |
| 19 | Out of scope | LAN discovery, reconnect/backfill, dedicated server, spectators, multi-session, bots |
