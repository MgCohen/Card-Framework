---
type: spike
status: active
created: 2026-05-18
references:
  - Product/spikes/wire-refoundation-roadmap.md
  - Product/spikes/wire-typed-payloads.md
sandbox: Assets/Framework/Wire/
tags: [#spike, #wire, #autopacker, #refoundation]
---

# Wire — AutoPacker Sample (Step 1)

> Step 1 of the wire-first refoundation. Validates pack / unpack / round-trip with AutoPacker as the codec layer. First "real" implementation — lives outside `Assets/Spike/` as the framework foundation.

## Premise

Build a working wire layer using AutoPacker for encode/decode. The event log holds typed C# objects; the wire carries packed unmanaged structs. `IPackingHandler` is the context-injection seam. No hand-written codecs, no tagged-union payload type.

Replaces the `TagValue { I64 | Bytes }` design from [`wire-typed-payloads.md`](wire-typed-payloads.md) with per-type `[AutoPack] Packed` structs.

## Phases

### Phase 1 — Minimal round-trip *(done)*

One event type. Pack. Transport (in-memory queue). Unpack. Verify identity.

- `Framework.Wire` asmdef under `Assets/Framework/Wire/` referencing `Scaffold.Autopacker`.
- `EntityCreatedEvent` — single `[AutoPack]` partial class : `WireEvent`.
- `EventStream` (typed log: Add / Get / Count / OnAppended), `Network` (packed-struct queue stand-in), `Wire` (passive bridge: Egress / Ingress factories).
- Server stream → Wire.Egress → Network → Wire.Ingress → client stream.
- Tests: round-trip identity, send order, OnAppended timing.

Locked: AutoPacker `[AutoPack]` flow works end-to-end inside a new asmdef. Responsibilities split — stream is the log, network is byte transport, wire is passive plumbing.

### Phase 2 — Heterogeneous stream *(current)*

Multiple event types routed via per-type channels.

- Non-generic `WireEvent` base for the in-memory log (already in Phase 1).
- A second event type (`EntityDestroyedEvent`).
- `Channel<T>` — owns pack/unpack for one event type. Models a Unity NGO custom-message handler.
- `Network` becomes a channel registry: `Register<T>(unpacker)` allocates a channel; `Resolve(type)` routes outbound; single internal FIFO queue preserves cross-channel order.
- `Wire.Egress` resolves the channel by event type and sends via it. `Wire.Ingress` forwards `Network.OnReceived` to the sink stream.
- Tests: mixed stream round-trip in send order, direct channel subscription.

**Locked**: per-type channels as the dispatch model. No discriminator field on the packed struct, no header byte, no wire alphabet enum — the channel identity IS the discriminator. The final transport (Unity NGO custom messages) routes by message name/hash per channel, so the in-memory shape mirrors the real one.

Cross-channel ordering is preserved because all channels share the Network's FIFO queue. In real NGO, custom messages on the same NetworkManager share the underlying transport's FIFO ordering — same property, different mechanism.

Cost: registering an event type takes one line of boilerplate (`network.Register<X>(p => new X((X.Packed)p))`). A future source-gen pass can eliminate it; not Phase 2's problem.

### Phase 3 — Custom payload types

Event with a non-primitive field. Pack via AutoPacker's `[Packed(typeof(T))]` + custom conversion.

- One event carrying a `Vector3`-shaped value.
- Decide: generic event subclasses (`TagSetEvent<T>`), or single non-generic event with handler-resolved payload.

Decides: which AutoPacker conversion path is canonical — generic `Resolve<TSource, TTarget>` switch, or extension-method binding.

### Phase 4 — `IPackingHandler` with injected context

Real custom handler that takes a dependency at construction. Stand-in catalog / registry.

- `GamePackingHandler` with injected catalog.
- Event payload that resolves through the handler (e.g., a `CardDefinition ↔ int` round-trip).
- Test: pack/unpack with handler context, with two handler instances pointing at different catalogs.

Decides: handler ownership story (per-session? per-test?). Schema-version handshake (registry hash exchange) flagged but deferred.

### Phase 5 — Full alphabet

All six original `WireEvent` families round-trip with realistic payloads.

- EntityCreated / EntityDestroyed (already in)
- TagSet / TagChange / TagRemoved
- BlockStart / BlockEnd

Locks the wire alphabet for the framework. Anything that doesn't fit needs a design decision before adding.

### Phase 6 — Real transport boundary

Replace in-memory queue with byte serialization. Process boundary (child process + stdin/stdout, or two AppDomains).

- `IPackedStruct` → bytes via `MemoryMarshal.AsBytes` or AutoPacker emission helpers.
- Bytes → packed struct on receiver.
- Same test fixtures, byte-level round-trip.

Locks: wire is end-to-end blittable. Anything that depends on shared in-process state surfaces here.

## Out of scope for Step 1

- Schema versioning / migration (flagged at Phase 4, deferred).
- Mirror / replay (next step in the roadmap).
- Hooks, commands, effects, requests (later steps).
- View layer (parallel track).

## Validation

Each phase ships a passing EditMode test before the next starts. No phase is "done" until its test is green.
