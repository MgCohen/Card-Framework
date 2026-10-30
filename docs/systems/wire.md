# Wire

Wire carries every state change as a `WireEvent`: it records events in a stream, dispatches them to apply handlers, and ships them between server and client.

**Code:** `Assets/Wire/` · assembly `Framework.Wire` · depends on `Scaffold.Autopacker`, `Unity.Netcode.Runtime`, `Unity.Collections` (no Framework assemblies)

## What it does
- Keeps an append-only `EventStream` of `WireEvent`s and raises `OnAppended` for each one.
- Routes each event by runtime type to every `WireEventHandler<T>` registered for it (`HandlerRegistry`).
- Packs each event into a fixed-size struct (`[AutoPack]` / `[Packed]`) and sends it over an `ITransport` with a one-byte channel id.
- Provides transports: `NgoTransport` (Netcode named messages), `LoopbackTransport` (in-process pair), `NullTransport` (drops everything).
- Brackets work in blocks: `BlockScope` emits `BlockStartEvent` and `BlockEndEvent` with a `BlockKind` and `BlockOutcome`.
- Holds no domain events. Features declare their own (rulebook ARCH-09).

## How it works
```text
SERVER                                          CLIENT
service (e.g. TagService)
  └─ IEventEmitter.Emit(ev)
EventStream.Emit ── append ── OnAppended
  ├─ HandlerRegistry.Dispatch ─> WireEventHandler<T>.Apply ─> store (internal writer)
  └─ WireForwarder ─> Network.Send
        └─ NetworkChannel<T,TPacked>.Send: [id byte][packed bytes]
              └─ ITransport ───────────────> ITransport.OnReceived
                                               └─ Dispatcher reads id, channel unpacks
                                                  └─ Network.OnReceived
                                                     └─ NetworkInboundBridge
                                                        └─ EventStream.Emit (client)
                                                           └─ HandlerRegistry ─> same handlers ─> same stores
```
1. Only the server registers services and `WireForwarder`. Only the client registers `NetworkInboundBridge` (`EngineInstaller`).
2. On both ends `HandlerRegistry` subscribes to the stream, so the same apply handler writes the same store on each side.
3. Apply handlers are the only callers of internal store writers (ARCH-05) and never call services (ARCH-06).
4. Dispatch is synchronous. A service can read the store right after `Emit` returns.
5. `WireRegistrar.RegisterAll` runs on both ends at build time. It finds every concrete `WireEvent` by reflection, sorts by full name, and assigns channel ids in that order. Open generic events (`TagSetEvent<T>`) are closed over `int` and `bool`.

## Key types
| Type | Job |
|---|---|
| `WireEvent` | Abstract `[AutoPack]` base of every networked event. |
| `EventStream` | Ordered event history. Implements `IEventEmitter`. Raises `OnAppended`. |
| `WireEventHandler<T>` | Apply handler base. Subclasses override `Apply(T)`. |
| `HandlerRegistry` | Groups handlers by event type and calls `Apply` on each append. |
| `Network` | Owns the channel table. `Register<T,TPacked>()` adds a channel; `Send` looks one up by type. |
| `NetworkChannel<T,TPacked>` | Packs and unpacks one event type. Raises a typed `OnReceived`. |
| `Dispatcher` | Reads the id byte from the transport and hands the bytes to that channel. |
| `ITransport` | Byte pipe. `NgoTransport`, `LoopbackTransport`, `NullTransport`. |
| `WireForwarder` / `NetworkInboundBridge` | Server stream to network; network to client stream. |
| `BlockScope` | Allocates block ids and emits block start and end events. |

## Example
From `Assets/Wire/Tests/RoundTripTests.cs`. `Wire.Connect()` builds a loopback server/client pair with both bridges:
```csharp
var w = Wire.Connect();
w.ServerNetwork.Register<PingEvent, PingEvent.Packed>();
w.ClientNetwork.Register<PingEvent, PingEvent.Packed>();

w.ServerStream.Emit(new PingEvent { Value = 42 });

Assert.AreEqual(1, w.ClientStream.Count);
```

## Gaps
- `EventStream.Emit` is public. Any code holding `GameEngine.Stream` or an `IEventEmitter` can emit, and no rule restricts emitters.
- `WireEventHandler<T>.Unapply` throws `NotImplementedException`. There is no rollback.
- `BlockScope.Open` always writes `ParentId = 0`. Nested blocks lose their parent.
- Traffic is server to client only. A client send reaches the server `Network` but never enters the server stream (`TwoEndTests.Client_Send_Does_Not_Enter_Server_Stream`).
- `WireRegistrar` is a temporary reflection scan. Channel ids depend on both ends loading the same event types, the cap is 256 channels, and generic events support only `int` and `bool`.
