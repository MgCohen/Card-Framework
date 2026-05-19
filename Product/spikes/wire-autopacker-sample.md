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

### Phase 2 — Heterogeneous stream *(done)*

Multiple event types routed via per-type channels, with zero reflection in the channel.

- A second event type (`EntityDestroyedEvent`).
- `Channel<T>` — concrete sealed class, one constraint set: `where T : WireEvent, IPackable, IUnpackable, new()`. Deliver path is `new T(); ev.Unpack(packed);` — language-native, no reflection, no per-event subclass, no factory lambda.
- `Network` owns the channel registry. `Register<T>()` allocates a channel and returns it for direct typed subscription. Single internal FIFO queue keyed by `Type → Action<IPackedStruct>` preserves cross-channel order.
- `Wire` is the connector: constructor wires `server.OnAppended → network.Send` and `network.OnReceived → client.Add`. No registration method, no state.
- Tests: mixed stream round-trip in send order, direct channel subscription, OnAppended timing.

**Locked**:
- **Per-type channels as the dispatch model.** No discriminator field on the packed struct, no header byte, no wire alphabet enum — the channel identity IS the discriminator. The final transport (Unity NGO custom messages) routes by message name/hash per channel; the in-memory shape mirrors the real one.
- **Cross-channel ordering preserved** because all channels share the Network's FIFO queue. In real NGO, custom messages on the same NetworkManager share the underlying transport's FIFO ordering — same property, different mechanism.
- **Channel reflection-free** because AutoPacker now emits `IUnpackable.Unpack` on every `[AutoPack]` partial. See [`proposals/autopacker-iunpackable-proposal.md`](../proposals/autopacker-iunpackable-proposal.md) for the upstream change.

Registering an event type takes one line: `network.Register<EntityCreatedEvent>()`. No subclass, no lambda, no factory. The `new()` + `IUnpackable` constraints carry the whole construction contract.

### Phase 3 — Custom payload types *(done)*

Extended the wire to carry non-primitive payloads through `IPackingHandler`. Two new event shapes proven end-to-end.

**Added:**

- `Vec3` — local `public readonly struct Vec3 { float X, Y, Z }`. Not `UnityEngine.Vector3` (decouples wire from Unity types).
- `EntityMovedEvent { long EntityId; Vec3 Position }` — composite unmanaged payload. `[Packed]` handles nested unmanaged structs natively, no handler involvement.
- `EntityLabeledEvent { long EntityId; [Packed(typeof(int))] string Label }` — handler-mapped reference field. AutoPacker stores `Label` as `int` in the packed struct; conversion goes through the registered `IPackingHandler`.
- `SimplePackingHandler : IPackingHandler` — placeholder handler. Bidirectional intern table for `string ↔ int`. Pass-through clause for matching `TSource == TTarget` (necessary — handler is called for *every* packed field, including primitives).

**Threaded handler through Network and Channel:**

- `Network` constructor takes an optional `IPackingHandler` (`new Network(handler)`; defaults to `null`).
- `Network.Send` calls `ev.Pack(_handler)`.
- `Channel.Deliver` calls `ev.Unpack(packed, _network.Handler)` via an internal accessor.
- `IUnpackable.Unpack(IPackedStruct, IPackingHandler)` overload (proposal A) made this possible — confirmed working.

**Locked decisions:**

- **Handler ownership** — Network-owned. One handler per `Network` instance, applies to every channel. Phase 4's catalog-backed handler will slot in here without API changes.
- **Conversion-path canonical** — generic `Resolve<TSource, TTarget>` switch with a pass-through fallback for matching types. Extension-method binding remains available for tighter per-type control if needed later.
- **Mapped payload example** — `string ↔ int` via bidirectional intern table. Phase 4 swaps in catalog-driven (e.g. `CardDefinitionRef ↔ int`) without changing the wire shape.

**Tests (7/7 passing):**

- `Composite_Payload_RoundTrips` — `Vec3` field round-trips.
- `Handler_Mapped_Payload_RoundTrips` — `string` field round-trips through intern-table handler, including repeated-value de-duplication.
- `Mixed_Stream_With_All_Event_Types_Preserves_Order` — interleaved created/moved/labeled/destroyed events round-trip in send order.
- All four pre-existing tests continue to pass.

**Out of scope, deferred:**

- Generic event subclasses (`TagSetEvent<T>`) — AutoPacker may or may not handle open generics; not needed under per-type channels. Defer until a real use case demands it.
- Real catalog injection — Phase 4.
- Schema-version handshake (registry hash exchange between client/server) — Phase 6 transport-boundary concern.

### Phase 4 — `IPackingHandler` with injected context *(done)*

Real custom handler that takes a dependency at construction. Catalog-backed resolution proven end-to-end.

**Added:**

- `CardDefinition { int Id; string Name }` — authored content, *not* `[AutoPack]`. Plain reference type.
- `Catalog` — minimal id ↔ ref mapping. `Add` / `GetById` / `GetId`. Enough to demonstrate injection; not a real catalog implementation.
- `CatalogPackingHandler : IPackingHandler` — takes `Catalog` in constructor. `Resolve<CardDefinition, int>` → `_catalog.GetId(def)`; `Resolve<int, CardDefinition>` → `_catalog.GetById(id)`. Pass-through for matching types.
- `CardPlayedEvent { long EntityId; [Packed(typeof(int))] CardDefinition Definition }` — reference-typed payload routed through the catalog handler.

**Locked decisions:**

- **Handler ownership** — Network-owned (locked in Phase 3, confirmed in Phase 4). One handler per `Network`; the handler holds the catalog reference.
- **Reference identity** — `CatalogPackingHandler` returns the catalog's stored instance, not a copy. Tests assert reference identity (`AreSame`) after round-trip — the received `CardDefinition` *is* the catalog's instance, not a structurally-equal copy.
- **Catalog-isolated drift** — two handlers pointing at different catalogs each return their own catalog's content for the same id. Documented as a unit test, not a round-trip; the schema-version handshake that would unify them is a Phase 6 concern.

**Deviation from original plan:** the proposed "two distinct `Network` instances bridged together" test required adding two new public members to `Network` (a packed-struct egress event + an ingress method) to bridge them. Skipped — Phase 6's real transport boundary is the natural place to split server/client. Replaced with two handler-level unit tests that exercise the catalog-injection mechanism directly. Same coverage, no new API.

**Tests (10/10 passing):**

- `Catalog_Backed_Payload_RoundTrips` — `CardPlayedEvent` round-trips with `CatalogPackingHandler(catalog)`; received `Definition` is reference-identical to the catalog's instance.
- `Two_Handlers_Same_Catalog_Resolve_Identically` — two handler instances pointing at the same catalog produce identical results.
- `Handlers_With_Different_Catalogs_Are_Isolated` — two catalogs with the same id mapping to different definitions; each handler returns its own catalog's instance.
- All 7 pre-existing tests continue to pass.

**Out of scope, deferred:**

- Two-process / two-network test (server + client as separate `Network` instances). Phase 6.
- Schema-version handshake. Phase 6.
- Multiple definition types (`HeroDefinition`, `ModifierDefinition`). Mechanism is identical; defer until needed.

### Phase 5 — Full alphabet *(done)*

Wire alphabet locked. Phase 5's actual contribution was **block brackets** and the **alphabet review**; tag mutations were already in place in `Framework.Tags` (parallel work that this spike initially overlooked).

**Added by Phase 5 (in `Framework.Wire/`):**

- `BlockStartEvent` — `byte Kind`. Opens an atomic bracket.
- `BlockEndEvent` — `byte Kind`. Closes; `Kind` echoes start for defensive verification of nesting.

**Already in place (from `Framework.Tags` work, in `Framework.Tags/`):**

- `TagSetEvent` — `int EntityId; int Slot; TagValue Value`. Non-generic; value packed into 8-byte `TagValue.Raw` via `TagValueCodec`.
- `TagRemovedEvent` — `int EntityId; int Slot`.
- `TagValue` — `readonly struct { long Raw; }`. Blittable wrapper.
- Type recovery on the receive side via `TagKey.ValueType` lookup at decode time.
- No `TagChangeEvent`: change is modeled as remove + set.

**Course correction:** Phase 5 originally shipped `IntTagSetEvent` / `IntTagChangeEvent` / `IntTagRemovedEvent` in `Framework.Wire/`, unaware that `Framework.Tags` already had a different (non-generic, codec-driven) tag-event design. Code review caught the duplication; the `Int*` events were removed and `Framework.Tags`'s `TagSetEvent` / `TagRemovedEvent` / `TagValue` were moved from `Framework.Wire/` to `Framework.Tags/` where they architecturally belong. Lesson: domain events (tags, modifiers) belong in their domain modules; only infrastructure + universal-lifecycle events belong in `Framework.Wire`.

**Locked decisions:**

- **Tag value typing strategy** — *non-generic with codec*. `TagSetEvent` carries `TagValue Value` (8-byte blittable); `TagValueCodec` packs/unpacks typed values into/from the long. Type recovered at decode via slot→type lookup. This is `Framework.Tags`'s design, accepted as canonical.
- **Block kind** — `byte` enum-like discriminator on `BlockStartEvent` / `BlockEndEvent`. Block kinds are bounded; specialized events per kind would multiply faster than benefit.
- **Block pairing** — positional LIFO. `BlockEnd.Kind` echoes the matching `BlockStart.Kind` for defensive verification; nesting structure is by depth.

**Wire alphabet, closed:**

| Category | Events | Lives in |
|---|---|---|
| Entity lifecycle | `EntityCreatedEvent`, `EntityDestroyedEvent` | `Framework.Wire` |
| Tag mutations | `TagSetEvent`, `TagRemovedEvent` (+ `TagValue`, `TagValueCodec`) | `Framework.Tags` |
| Block brackets | `BlockStartEvent`, `BlockEndEvent` | `Framework.Wire` |
| Content events | `EntityMovedEvent`, `EntityLabeledEvent`, `CardPlayedEvent` (test fixtures) | `Framework.Wire/Tests/Fixtures` |

Domain modules own their own wire events. Anything new that doesn't fit one of these categories needs an alphabet-extension design pass.

**Tests:**

- `Framework.Wire.Tests` — 11/11. Adds `Block_Brackets_RoundTrip`, `Nested_Blocks_Preserve_Order_And_Kind`, `Mixed_Alphabet_Stream_Preserves_Order` (covers entity lifecycle + block brackets + composite payload + handler-mapped payload).
- `Framework.Tags.Tests` — 15/15. Pre-existing tag wire-roundtrip tests continue to pass after the move.

Removed from Phase 3 / Phase 5: `Client_Stream_Preserves_Send_Order_Across_Channels`, `Mixed_Stream_With_All_Event_Types_Preserves_Order`, and the three `Int*` round-trip tests — all subsumed or redundant.

**Out of scope, deferred:**

- `TagChangeEvent` (the user's design uses remove + set; revisit if a real use case wants atomic transition).
- Composite-value tag events (`Vec3` etc. — `TagValueCodec` would need extension or a parallel mechanism).
- `TagRegistry` (slot↔name handshake between client/server).
- Rich block payloads beyond `Kind`.
- Mirror / replay from the stream.

### Phase 6 — Real transport boundary *(next)*

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
