# P8 spec: peers

P8 copies the role wiring of the Unity Framework into Base, with better names and one change: a server sends to many clients. Each peer is its own host. A role flag at the composition root selects the services that the host registers. The server registers a sender on its `WireLog`, and a client registers a receiver on its transport.

Branch `p8-peers`, cut from `p7-cards`. PR into `p7-cards`.

## Source in the Unity Framework

| Unity Framework | Base | Change |
|---|---|---|
| `EngineInstaller(isServer, transport)` | Role flag at the composition root | Same `if` |
| `WireForwarder`: `stream.OnAppended += network.Send` | `WireSender`: `log.Appended` → each client transport | Many transports |
| `NetworkInboundBridge`: `network.OnReceived += emitter.Emit` | `WireReceiver`: frame → `WireCatalog.ApplyAsync` | Async apply, in order |
| `Dispatcher`: `[1-byte id][payload]` | `WireCatalog.Write` and `Read` (P6) | None |
| `LoopbackTransport.Pair` | `InProcessTransport.Pair` | Name only |
| `MatchPair`: two scopes, `isServer` true and false | `PeerHost`: one server, N clients | N clients |
| Build callback resolves the bridge | The code that makes the `Match` resolves the role service | Per match |

## Composition

```csharp
// Server host: one transport for each client
IHost server = VsaBuilder.Create().UseGenericHost().Services(s =>
{
    foreach (ITransport client in clientSides) s.AddSingleton(client);
    s.AddScoped<WireSender>();
}).Build();

// Client host: one transport to the server
IHost client = VsaBuilder.Create().UseGenericHost().Services(s =>
{
    s.AddSingleton(serverSide);
    s.AddScoped<WireReceiver>();
}).Build();

using Match match = new Match(host.Services);
match.Services.GetRequiredService<WireSender>();      // or WireReceiver: subscribes in its constructor
```

`PeerHost` (in `Tests/Testing`) holds this `if (role == PeerRole.Server)` code. `App/Host/Program.cs` does not change: it has no match and no transport yet.

## Types

```csharp
// Plugins/Peers/Handlers/Commands
public enum PeerRole { Server, Client }

public sealed class WireSender : IDisposable          // server, scoped
{
    public WireSender(WireLog log, WireCatalog catalog, IEnumerable<ITransport> clients);
    // Sends WireHello(catalog.Hash) to each client, then one frame for each Appended entry.
}

public sealed class WireReceiver : IDisposable        // client, scoped
{
    public WireReceiver(WireCatalog catalog, ICommandSender bus, WireLog log, ITransport server);
    public Task Idle { get; }                         // completes when the received frames are applied
    // OnReceived copies the frame; frames apply one at a time, in order.
}

// Plugins/Wire
public sealed record WireHello(ulong Hash);           // fixed catalog id 4; WireLog.Append rejects it
public ulong WireCatalog.Hash { get; }                // FNV-1a 64 of the catalog keys in id order

// Plugins/Network: LoopbackTransport becomes InProcessTransport

// Tests/Testing
public sealed class Peer : IDisposable                // an app host and its Match
{
    public static Peer Start(PeerRole role, IEnumerable<ITransport> transports, Action<IServiceCollection>? services = null);
    public Match Match { get; }
}

public sealed class PeerHost : IDisposable
{
    public static PeerHost Start(int clients);
    public Peer Server { get; }
    public IReadOnlyList<Peer> Clients { get; }
    public Task IdleAsync();                          // each client receiver is idle
}
```

## Receiver rules

| Case | Result |
|---|---|
| First frame is `WireHello` with an equal hash | Entries apply |
| `WireHello` with a different hash | Fault; no entry applies |
| Entry before `WireHello` | Fault |
| Entry gives an `Error` | Fault with the entry |
| Client `WireLog` | `RecordStep` fills it; equal to the server log |
| Client `Appended` | No sender on a client, so no forward |

A fault faults `Idle`, and the receiver applies no more frames.

## Items from the brief

| # | Item | Decision |
|---|---|---|
| 1 | `Match` as World owner | Keep `Match`. One `Match` in each peer host |
| 2 | `Reader` after `Dispose` | `Reader`, `Services`, `Commands` throw `ObjectDisposedException` |
| 3 | Friflo threads | Rule in docs: one thread for each peer host, as in Unity |
| 4 | No forward on a client | The client registers no `WireSender` |
| 5 | Hash in the handshake | `WireHello` frame |
| 6 | Channel and dispatch | The catalog id is the channel, as the Unity `Dispatcher` |
| 7 | Wire types in the handler layer | Stay; `Peers.Handlers.Commands` uses that layer |
| 8 | Lines over 120 characters | Fix all five |

## Tests

| Test | Project |
|---|---|
| Done check: server, client A, client B; `CreateCard`, `MoveCard`, `ChangeResource`; equal Worlds and logs | Peers.Tests |
| Client sends nothing (`RecordingTransport.Sent` empty) | Peers.Tests |
| Frames: `WireHello` first, then one frame for each entry | Peers.Tests |
| Different keys: `Idle` faults, client World empty | Peers.Tests |
| Entry before `WireHello`: `Idle` faults | Peers.Tests |
| `Match` after `Dispose`: `Reader`, `Services` throw | Ecs.Tests |
| `WireCatalog.Hash`: equal keys equal; other keys differ | Wire.Tests |
| Catalog limit: 255 types pass; the 5 built-in types make the count odd | Wire.Tests |
| `WireLog.Append` rejects `WireHello` | Wire.Tests |

## Non-goals

- No client commands to the server. Reason: no code uses them in P8.
- No late join and no disconnect. Reason: no user yet.
- No `Unapply` check (plan done check 2). Reason: the base has no `Unapply`.
- No Trace, Unity or NGO code. Reason: brief scope.
