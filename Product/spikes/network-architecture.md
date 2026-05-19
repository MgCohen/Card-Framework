---
type: spike
status: design-pass
created: 2026-05-19
references:
  - Product/spikes/wire-autopacker-sample.md
  - Product/spikes/wire-refoundation-roadmap.md
sandbox: Assets/Framework/Wire/
tags: [#spike, #wire, #network, #design]
---

# Network Architecture

> Design pass on the network architecture before Phase 6 implementation. Phases 1–5 built the in-process wire on a "shared Network" simplification; the real production target requires per-end Network + transport. This doc settles the design questions explicitly so implementation is mechanical. No code yet — decisions only.

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

- **IDispatcher composition.** `Channel<T>` takes an internal `IDispatcher { IPackingHandler Handler; void Receive(WireEvent); }` instead of full `Network`. `Network` implements the interface via explicit interface implementation. Channel sees the minimum it needs; future Network restructuring doesn't ripple into Channel. *Applied and verified — 276/276 tests passing.*
- **Channel-as-discriminator (Q-B).** Each registered channel has a stable id derived from the channel itself. That id is what the receiver uses to route incoming bytes back to a typed channel.
- **Channel owns type-specific ops (Q-E).** Pack, unpack, discriminator — all live on `Channel<T>`. Network is dispatch + transport only.
- **Registry sync by type matching (Q-C).** Both ends register the same C# types; channel ids derive from type identity (registration order, type-name hash, or explicit id — see implementation note below).
- **Wire shape (Q-F).** `Wire` stays as the `EventStream` ↔ `Network` bridge. With per-end Network, the constructor splits into two overloads (outbound vs inbound).

## Open decisions

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
public sealed class Network : IDispatcher
{
    public Network(ITransport transport, IPackingHandler handler = null);

    public Channel<T> Register<T>() where T : WireEvent, new();
    public void Send(WireEvent ev);

    public event Action<WireEvent> OnReceived;
}

public delegate void TransportReceiveCallback(FastBufferReader reader);

public interface ITransport
{
    void Send(in FastBufferWriter writer);
    event TransportReceiveCallback OnReceived;
}

public sealed class LoopbackTransport : ITransport
{
    public static (LoopbackTransport server, LoopbackTransport client) Pair();
    // Send to one end invokes OnReceived on the other with a matching FastBufferReader.
}

public sealed class Wire
{
    public Wire(EventStream source, Network network);   // outbound
    public Wire(Network network, EventStream sink);     // inbound
}
```

### Open implementation question: Channel<T> ↔ Packed-type lookup

`FastBufferWriter.WriteValueSafe<T>(in T)` requires the concrete unmanaged `T` at compile time. But `Channel<T>` only knows `T : WireEvent, new()` — the nested `Packed` struct is per-event and not in the generic constraint.

Three resolutions:

1. **Two-param Channel** — `Channel<TEvent, TPacked>`. Register becomes `network.Register<EntityCreatedEvent, EntityCreatedEvent.Packed>()`. Verbose but no reflection.
2. **Reflection at Register** — find `typeof(T).GetNestedType("Packed")` once, build typed read/write delegates via `Expression.Compile` or `DynamicMethod`, cache them. No call-site verbosity but a small reflective build at startup. (Works on Mono; uncertain on IL2CPP.)
3. **Scaffold change** — AutoPacker emits static helpers on each `[AutoPack]` type: `static void WritePacked(ref FastBufferWriter, IPackedStruct)` and `static IPackedStruct ReadPacked(ref FastBufferReader)`. Channel uses them via reflection-discovered method or C# 11 static abstract interface member. No call-site verbosity, no reflection at runtime.

**Lean:** #2 for now (reflection-built delegates cached at Register). It keeps the existing `network.Register<EntityCreatedEvent>()` ergonomics. If IL2CPP turns out to break it, fall back to #1 or escalate to #3 as a follow-up Scaffold proposal.

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
clientNetwork.Pump();
// clientStream[0] is the EntityCreatedEvent
```

### What the in-process helper looks like

For tests that don't care about the boundary — same coverage as today's `new Wire(server, client, network)`:

```csharp
var (serverStream, clientStream, network) = Wire.Connect(handler);
// Internally builds two Networks + loopback transport + wires + helper-managed registration.
// Returned `network` is a thin handle for Register and Pump.
```

This keeps simple tests simple. Two-end tests still available for things that *do* care about the boundary.

## Implementation plan

### Phase 6a — architecture refactor + FastBufferWriter transport

Single load-bearing phase. FastBufferWriter is the medium from day one — no intermediate `IPackedStruct`-over-loopback stage. NGO source is available in `Library/PackageCache/` for reference.

- `Framework.Wire.asmdef` drops `noEngineReferences`, references `Unity.Netcode`.
- `ITransport` interface (using `FastBufferWriter` / `FastBufferReader`).
- `LoopbackTransport` — `Send(in FastBufferWriter)` on one end → `OnReceived(FastBufferReader)` on the other with a matching reader pointed at the same buffer (or a copy if lifetime requires).
- `Network` refactored to per-end. Takes `ITransport` + `IPackingHandler` at construction.
- `Channel<T>` learns `Pack(ref FastBufferWriter, T)` and `Unpack(FastBufferReader) → T`. Discriminator byte/short prepended.
- `Wire` constructors split into outbound (`source, network`) / inbound (`network, sink`).
- `Wire.Connect(handler)` helper builds the loopback-paired two-Network setup for in-process tests.
- Tests migrated. Existing test coverage preserved; new test exercises explicit two-end setup.

Validation: 276 existing tests stay green, plus new tests covering two-end + FastBufferWriter round-trip.

### Phase 6b — NGO transport

- `NgoTransport : ITransport` wraps `NetworkManager.CustomMessagingManager`. Send via `SendNamedMessage` (or unnamed); receive via the manager's message handler.
- Framework code above the transport line doesn't change.
- Validation: a Unity Play-mode test (not EditMode) that spins up a NetworkManager and round-trips an event between two simulated peers.

## Out of scope, deferred

- **Channel registry hash exchange / handshake.** Both ends must currently register the same C# types in the same order to get matching discriminator ids. A registry-hash handshake at connect time would detect drift; deferred to a later pass.
- **Reliability / ordering guarantees.** `ITransport` contract is "best-effort delivery"; ordering preservation is the transport's responsibility (NGO's reliable-sequenced channel will provide it).
- **Cross-process tests.** `LoopbackTransport` in the same process catches state-isolation issues. Real cross-process testing (child process + stdin/stdout, or AppDomains) is deferred — useful but not blocking framework development.
- **Open-generic event types.** Pending upstream Scaffold change (see [`autopacker-open-generics.md`](../proposals/autopacker-open-generics.md)). Doesn't interact with network architecture.

## Decision summary

| # | Question | Locked |
|---|---|---|
| — | IDispatcher composition | ✅ Channel takes `IDispatcher`, not full `Network`. *Applied and verified.* |
| A | Network identity | ✅ Per-end (server + client construct their own) |
| B | Discriminator | ✅ Channel id assigned at registration, sent as header byte/short |
| C | Registry sync | ✅ Both ends register same types; ids derive from registration |
| D | Transport abstraction | ✅ `ITransport` interface; `LoopbackTransport` for tests, `NgoTransport` for production |
| E | Type-specific ops location | ✅ `Channel<T>` owns pack, unpack, discriminator |
| F | Wire shape | ✅ Stays; constructor splits into outbound + inbound |
| G | Migration | ✅ Replace + `Wire.Connect` helper for in-process case |

All eight settled. Implementation can proceed mechanically through phases 6a → 6b → 6c.
