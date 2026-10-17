---
type: spike
status: locked
created: 2026-05-19
references:
  - Product/spikes/wire-autopacker-sample.md
  - Product/spikes/archive/wire-refoundation-roadmap.md
  - Product/proposals/autopacker-typed-accessors.md
sandbox: Assets/Framework/Wire/
tags: [#spike, #wire, #network, #design]
---

# Network Architecture

> Design pass on the network architecture before Phase 6 implementation. Phases 1–5 built the in-process wire on a "shared Network" simplification; the real production target requires per-end Network + transport. All decisions locked — implementation is mechanical from here.

## Where we are coming from

Phases 1–5 in [`wire-autopacker-sample.md`](wire-autopacker-sample.md) built the wire layer in-process:

- One `Network` instance shared between server-side `EventStream` and client-side `EventStream`.
- Internal `Queue<Envelope>` is the transport.
- `Channel<T>` per event type, registered on Network.
- Pack/unpack via AutoPacker; handler injected at Network construction.
- `Wire` binds both streams to the shared Network in its constructor.

That shape works for in-process testing but is the wrong abstraction for the real target (server + client as distinct processes, bridged by Unity NGO custom messages). Phase 6's stated scope ("replace in-memory queue with byte serialization") implicitly requires Network to split into per-end instances, a new transport abstraction, channel-registry-sync, and a discriminator scheme. None of those are mechanical additions — they're architecture decisions.

This doc settles them.

## Locked already

- **Network composition (Q-Composition).** Network is the orchestrator façade. It composes a `Dispatcher` (sub-service that owns `ITransport` + `IPackingHandler` + channel registry) and a `Dictionary<Type, Channel>` for the boxed `Send(WireEvent)` lookup. Channel takes the Dispatcher as a direct ref (no `IDispatcher` interface — base class abstract methods are enough). Strictly downward dependencies: `Network → Channel → Dispatcher → ITransport`; on receive `ITransport → Dispatcher → Channel`. No back-references.
- **Channel-as-discriminator (Q-B).** Each registered channel has a stable `byte` id derived from registration order. That id is what the receiver uses to route incoming bytes back to a typed channel.
- **Channel owns type routing + serialization (Q-E).** Channel<T> knows its discriminator, owns its `OnReceived` event, and handles the `Channel.Send(T)` / `Channel.DeliverFromBytes(reader)` round-trip via the Dispatcher. Pack/unpack codec itself lives on `WireEvent<TPacked>` (typed accessors).
- **Registry sync by type matching (Q-C).** Both ends register the same C# types in the same order; channel ids match by position.
- **Wire shape (Q-F).** `Wire` stays as the `EventStream` ↔ `Network` bridge. With per-end Network, the constructor splits into two overloads (outbound vs inbound).
- **Synchronous delivery (Q-Sync).** `LoopbackTransport.Send` immediately invokes the peer's `OnReceived` — no buffering, no `Pump` step. Production transports (`NgoTransport`) deliver on their own cadence; framework provides no `Pump` method.
- **byte discriminator (Q-DiscWidth).** 1-byte prefix per packet, 256 max channels. Tighter packets, fits card-game cardinality with comfortable margin. Migration to `short` is a wire-format-breaking change if ever needed.
- **Per-send writer, per-channel cached size (Q-Sizing).** `Channel<T>` caches `_sizeWithDisc = 1 + Unsafe.SizeOf<TPacked>()` at construction. `Channel.Send` allocates a new `FastBufferWriter` per call against `Allocator.Temp` (Unity's per-frame bump allocator). Idiomatic NGO pattern; no writer-pool or persistent buffer state.

## Decision rationale

All decisions below are now locked (see Decision summary at the bottom). This section preserves the option space and tradeoffs that drove each lock.

### Q-A: Network identity — per-end vs shared

| Option | Shape | Tradeoff |
|---|---|---|
| **A-1: per-end** | `Network` represents one end of a connection. Each side constructs its own. `ITransport` bridges them. | Matches NGO, TCP, every real network. Test setup requires two `Network`s. Honest abstraction. |
| A-2: shared bridge with internal split | `Network` stays as today's shape (bridges server↔client). Internally it has two "Connection" objects. | Tests don't change shape. But the "I am the wire" framing lies about distributed reality. The internal split has no real consumers — it's a fiction the in-process tests maintain. |

**Lean: A-1 (per-end).** The shared shape is an in-process simplification; per-end is the production shape. Test rewrite is mechanical (two-network setup is closer to how real networked-game tests would look anyway). The G-3 migration helper below softens the transition.

### Q-D: Transport abstraction — medium and shape

**Medium.** We're going to use Unity NGO custom messages as the eventual real transport. NGO's serialization primitive is `FastBufferWriter` / `FastBufferReader` — `ref struct`s wrapping a native buffer, zero-allocation, designed for unmanaged-struct write/read. Custom message handlers already receive a `FastBufferReader` directly.

So the transport medium is **`FastBufferWriter` / `FastBufferReader`**, not raw `byte[]` / `ReadOnlySpan<byte>`. Reason: aligns with NGO directly. No double-copy through `byte[]` and back into NGO's buffers. The `LoopbackTransport` for tests uses the same primitives so behavior matches real NGO exactly.

**Shape.** A small interface — but with delegate parameters because `FastBufferReader` / `FastBufferWriter` are `ref struct` types and can't be generic-type-args (i.e. can't appear in `Action<T>`):

```csharp
public delegate void TransportReceiveCallback(FastBufferReader reader);

public interface ITransport
{
    void Send(in FastBufferWriter writer);
    event TransportReceiveCallback OnReceived;
}
```

Loopback for tests, NGO for production. Network takes `ITransport` at construction.

**Asmdef implication.** `FastBufferWriter` lives in `Unity.Netcode`. Using it in `Framework.Wire` requires the asmdef to reference `Unity.Netcode` — which means dropping `"noEngineReferences": true` from `Framework.Wire.asmdef`. That's the honest call: the wire layer was already practically Unity-targeted (we use `[AutoPack]` via Scaffold which runs in Unity); pretending engine-free now blocks the FastBufferWriter alignment. Accept the engine reference.

Alternative considered and rejected: keep `Framework.Wire` engine-free, expose `ITransport` with `ReadOnlySpan<byte>`, have `NgoTransport` bridge spans to FastBufferWriter internally. Loses the no-double-copy benefit and gains nothing — the wire is going to be used in Unity regardless.

### Q-G: Migration story

| Option | Shape | Tradeoff |
|---|---|---|
| G-1: replace | Today's `Network` becomes per-end. Existing tests rewrite. | Cleanest end state. Per-end is the only model. |
| G-2: coexist | Keep today's `Network` for in-process, add `Network2` for distributed. | Two patterns forever — bad. |
| **G-3: replace + helper** | Replace with per-end. Add `Wire.Connect(serverStream, clientStream, opts)` helper that internally builds two `Network`s + `LoopbackTransport`. Tests that don't care about the boundary use the helper. | Architectural change without forcing in-process consumers to deal with two-Network setup unnecessarily. |

**Lean: G-3.** Replace is the right move; the helper softens the in-process case without dishonest abstraction.

## The shape if all leans accepted

```csharp
// Orchestrator façade. Public API surface.
public sealed class Network
{
    public Network(ITransport transport, IPackingHandler handler = null);

    public Channel<T> Register<T>() where T : WireEvent, new();
    public void Send(WireEvent ev);                       // boxed entry, type → channel lookup

    public event Action<WireEvent> OnReceived;            // relayed from Dispatcher.OnAnyReceived
}

// Send/receive engine. Owns transport, handler, and the channel registry by id.
public sealed class Dispatcher
{
    public event Action<WireEvent> OnAnyReceived;

    internal void Register(byte id, Channel ch);
    internal void Send(in FastBufferWriter writer);       // called by Channel.Send after it writes its bytes
}

// Type-router + serializer. One per registered event type.
public abstract class Channel
{
    internal abstract void SendBoxed(WireEvent ev);
    internal abstract WireEvent DeliverFromBytes(ref FastBufferReader r, IPackingHandler h);
}

public sealed class Channel<T> : Channel where T : WireEvent, new()
{
    public event Action<T> OnReceived;
    public void Send(T ev);
}

// Transport contract. Loopback for tests, NGO for production.
public delegate void TransportReceiveCallback(FastBufferReader reader);

public interface ITransport
{
    void Send(in FastBufferWriter writer);
    event TransportReceiveCallback OnReceived;
}

public sealed class LoopbackTransport : ITransport
{
    public static (LoopbackTransport server, LoopbackTransport client) Pair();
    // Send to one end synchronously invokes OnReceived on the other.
}

// EventStream ↔ Network bridge. Constructor splits per direction.
public sealed class Wire
{
    public Wire(EventStream source, Network network);     // outbound
    public Wire(Network network, EventStream sink);       // inbound

    public static (EventStream serverStream, EventStream clientStream,
                   Network serverNetwork, Network clientNetwork)
        Connect(IPackingHandler handler = null);          // in-process loopback pair for tests
}
```

### Channel<T> ↔ Packed-type lookup — resolved via AutoPacker typed accessors

`FastBufferWriter.WriteValueSafe<T>(in T)` requires the concrete unmanaged `T` at compile time. `Channel<T>` only knows `T : WireEvent, new()` — the nested `Packed` struct is per-event and not in the generic constraint.

**Resolution:** AutoPacker shipped typed accessors (see [`autopacker-typed-accessors.md`](../proposals/autopacker-typed-accessors.md)) — `PackTyped(handler) → TPacked` and `UnpackTyped(in TPacked, handler)` on every `[AutoPack]` type, plus `IPackable<TPacked>` / `IUnpackable<TPacked>` interfaces and `override` emission through hand-written generic abstract intermediates.

This unlocks a `WireEvent<TPacked>` intermediate base in `Framework.Wire` that knows both sides of the bridge at compile time:

```csharp
public abstract class WireEvent
{
    public abstract void WriteToNetwork(ref FastBufferWriter w, IPackingHandler h);
    public abstract void ReadFromNetwork(ref FastBufferReader r, IPackingHandler h);
}

public abstract class WireEvent<TPacked> : WireEvent, IPackable<TPacked>, IUnpackable<TPacked>
    where TPacked : unmanaged, IPackedStruct
{
    public abstract TPacked PackTyped(IPackingHandler h = null);
    public abstract void UnpackTyped(in TPacked p, IPackingHandler h = null);

    public sealed override void WriteToNetwork(ref FastBufferWriter w, IPackingHandler h)
    {
        var packed = PackTyped(h);
        w.WriteValueSafe(packed);
    }

    public sealed override void ReadFromNetwork(ref FastBufferReader r, IPackingHandler h)
    {
        r.ReadValueSafe(out TPacked packed);
        UnpackTyped(packed, h);
    }
}
```

Concrete events change a single token in the inheritance clause:

```csharp
public partial class EntityCreatedEvent : WireEvent<EntityCreatedEvent.Packed>
{
    [Packed] public long EntityId;
}
```

AutoPacker emits `PackTyped` / `UnpackTyped` as `override` on the leaf (verified by `AutoPacker_HandWrittenAbstractTypedBase_DerivedOverridesAndRoundTrips` in the AutoPacker test suite).

`Channel<T>` stays single-type-parameter; dispatch is virtual through the non-generic `WireEvent`. No reflection, no two-type-param register call. Zero-box round-trip (verified by `AutoPacker_PackTyped_ReturnsConcretePackedWithoutBoxing` + `AutoPacker_UnpackTyped_RestoresStateWithoutBoxing`).

### What an explicit two-end test looks like

```csharp
var catalog = new Catalog();
var serverHandler = new CatalogPackingHandler(catalog);
var clientHandler = new CatalogPackingHandler(catalog);

var (serverTransport, clientTransport) = LoopbackTransport.Pair();

var serverNetwork = new Network(serverTransport, serverHandler);
var clientNetwork = new Network(clientTransport, clientHandler);

// Same registration order on both ends — channel ids match.
serverNetwork.Register<EntityCreatedEvent>();
clientNetwork.Register<EntityCreatedEvent>();
serverNetwork.Register<TagSetEvent>();
clientNetwork.Register<TagSetEvent>();

var serverStream = new EventStream();
var clientStream = new EventStream();

new Wire(serverStream, serverNetwork);   // outbound
new Wire(clientNetwork, clientStream);   // inbound

serverStream.Add(new EntityCreatedEvent { EntityId = 42 });
// clientStream[0] is the EntityCreatedEvent — delivery is synchronous via LoopbackTransport.
```

### What the in-process helper looks like

For tests that don't care about the boundary — same coverage as today's `new Wire(server, client, network)`:

```csharp
var (serverStream, clientStream, serverNet, clientNet) = Wire.Connect(handler);
// Internally builds two Networks + loopback transport + wires.
// Caller registers event types on each network (same order on both ends).
```

This keeps simple tests simple. Two-end tests still available for things that *do* care about the boundary.

## Implementation plan

### Phase 6a — `WireEvent<TPacked>` bridge + FastBufferWriter transport

Single load-bearing phase. FastBufferWriter is the medium from day one — no intermediate `IPackedStruct`-over-loopback stage. AutoPacker's typed accessors (now shipped) carry the codec; this phase wires them to NGO's buffer primitives. NGO source is available in `Library/PackageCache/` for reference.

**Bridge layer (new, AutoPacker-unlocked):**

- `WireEvent` (non-generic abstract) declares `WriteToNetwork(ref FastBufferWriter, IPackingHandler)` / `ReadFromNetwork(ref FastBufferReader, IPackingHandler)`.
- `WireEvent<TPacked>` intermediate base implements both via `PackTyped` / `UnpackTyped` (zero-box). Declares the typed methods abstract.
- Seven existing event classes (`EntityCreatedEvent`, `EntityDestroyedEvent`, `BlockStartEvent`, `BlockEndEvent`, + test fixtures `EntityMovedEvent`, `EntityLabeledEvent`, `CardPlayedEvent`) get a one-token base-clause change to `: WireEvent<X.Packed>`. AutoPacker emits the typed `override` automatically.

**Network / Dispatcher / Channel split (Q-Composition):**

- `Network.asmdef` drops `noEngineReferences`, references `Unity.Netcode.Runtime`.
- `ITransport` interface (using `FastBufferWriter` / `FastBufferReader`).
- `LoopbackTransport` — `Send(in FastBufferWriter)` synchronously invokes the peer's `OnReceived` with a matching reader. **No `Pump`, no buffering** — delivery is synchronous (Q-Sync).
- `Dispatcher` (new). Holds `ITransport` + `IPackingHandler` + `Channel[256]` indexed by id. Exposes `Send(in FastBufferWriter)` for channels to call after they write their bytes. Hooks `ITransport.OnReceived`, reads the 1-byte discriminator (Q-DiscWidth), routes to the matching channel via `Channel.DeliverFromBytes`. Fires `OnAnyReceived(WireEvent)` after each delivery.
- `Network` becomes the orchestrator. Takes `ITransport` + `IPackingHandler` at construction; internally constructs `Dispatcher` and registers channels into it. Drops `Queue<Envelope>` and `Pump`. Public `Send(WireEvent)` does a `Dictionary<Type, Channel>` lookup and forwards to `Channel.SendBoxed`. `OnReceived` event relays from `Dispatcher.OnAnyReceived`.
- `Channel<T>` (single-type-parameter, `where T : WireEvent, new()`). Caches `_id` (byte) and `_sizeWithDisc = 1 + Unsafe.SizeOf<TPacked>()` at construction (Q-Sizing — TPacked discovered via a one-time `new T().PackedSize` read against the virtual property on `WireEvent`). `Send(T ev)` allocates a per-send `FastBufferWriter` against `Allocator.Temp`, writes discriminator + calls `ev.WriteToNetwork`, hands the writer to `Dispatcher.Send`, disposes. `DeliverFromBytes(ref reader, handler)` constructs `new T()`, calls `ev.ReadFromNetwork`, fires `OnReceived`, returns the event for Dispatcher's `OnAnyReceived` relay.
- `Wire` constructors split into outbound (`source, network`) / inbound (`network, sink`).
- `Wire.Connect(handler)` helper builds the loopback-paired two-Network setup for in-process tests, returning both streams + both networks.

**Tests:** existing 11 wire tests migrate via `Wire.Connect` helper (no `Pump` calls anymore — delivery is synchronous). The one test asserting deferred-pump behavior rewrites to assert synchronous delivery on `Send`. New test exercises explicit two-end setup with paired `LoopbackTransport`.

Validation: existing 346 EditMode tests stay green (11 wire tests + 29 AutoPacker self-tests + rest), plus new two-end + FastBufferWriter round-trip tests.

### Phase 6b — NGO transport

- `NgoTransport : ITransport` wraps `NetworkManager.CustomMessagingManager`. Send via `SendNamedMessage` (or unnamed); receive via the manager's message handler.
- Framework code above the transport line doesn't change.
- Validation: a Unity Play-mode test (not EditMode) that spins up a NetworkManager and round-trips an event between two simulated peers.

## Out of scope, deferred

- **Channel registry hash exchange / handshake.** Both ends must currently register the same C# types in the same order to get matching discriminator ids. A registry-hash handshake at connect time would detect drift; deferred to a later pass.
- **Reliability / ordering guarantees.** `ITransport` contract is "best-effort delivery"; ordering preservation is the transport's responsibility (NGO's reliable-sequenced channel will provide it).
- **Cross-process tests.** `LoopbackTransport` in the same process catches state-isolation issues. Real cross-process testing (child process + stdin/stdout, or AppDomains) is deferred — useful but not blocking framework development.
- **Open-generic event types.** Shipped in Scaffold (see [`autopacker-open-generics.md`](../proposals/autopacker-open-generics.md)). Doesn't interact with network architecture.

## Decision summary

| # | Question | Locked |
|---|---|---|
| Composition | Network/Dispatcher/Channel split | ✅ Network = orchestrator. Dispatcher = send/receive engine, holds transport + handler + channel registry. Channel = type-router + serializer (abstract base, not interface). Strictly downward deps. |
| A | Network identity | ✅ Per-end (server + client construct their own) |
| B | Discriminator | ✅ Channel id assigned at registration (registration order = id) |
| C | Registry sync | ✅ Both ends register same types in same order; ids match by position |
| D | Transport abstraction | ✅ `ITransport` with `FastBufferWriter`/`FastBufferReader`; `LoopbackTransport` for tests, `NgoTransport` for production |
| E | Type-specific ops location | ✅ `WireEvent<TPacked>` owns pack/unpack codec via AutoPacker typed accessors; `Channel<T>` owns discriminator id + per-channel size cache + writer allocation |
| F | Wire shape | ✅ Stays; constructor splits into outbound + inbound; static `Connect` helper for in-process pair |
| G | Migration | ✅ Replace today's `Network` shape; `Wire.Connect` softens the in-process case |
| Sync | Delivery timing | ✅ Synchronous in `LoopbackTransport.Send`. No `Pump` method. Production transports deliver on their own cadence. |
| DiscWidth | Discriminator width | ✅ `byte` (256 channels max). Tighter packets; sufficient for card-game cardinality. |
| Sizing | Buffer sizing | ✅ Per-send `FastBufferWriter` against `Allocator.Temp`; `_sizeWithDisc` cached per channel at construction. |

All settled. Implementation can proceed mechanically.
