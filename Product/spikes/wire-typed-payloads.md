---
type: spike
status: active
forward-link: TBD
references: [Product/spikes/wire-network-protocol.md]
tags: [#spike, #wire, #serialization, #typing, #source-gen, #tags]
---

# Wire Typed Payloads — spike

## Premise

Resolve the deferred sub-decision from `wire-network-protocol.md` lock 7: how do wire-event field values get *typed* once we move off the Newtonsoft stub onto a real binary serializer (protobuf/FlatBuffers/custom). Two specific surfaces are open:

1. **Tag values** — today `TagSetEvent.Value` and `TagChangeEvent.Prev/Next` are `object?`. Polymorphic `object` doesn't survive a typed binary wire.
2. **`BlockStartEvent.Payload`** — today an `IReadOnlyDictionary<string, object?>` produced by reflection-walking the runnable's public properties. Same problem, same shape of fix.

Both must become typed without losing the open-ended extensibility we get from declaring `TagKey<T>` for arbitrary `T`. The wire alphabet must stay closed; the space of declarable tag value types must stay open.

## Context — where this is anchored

- **Parent spike: `wire-network-protocol.md` (status: deferred 2026-05-15).** Locked seven decisions; lock 7 ("WireEvent classes ARE the serialization format") committed to a unified engine + wire record. Sub-decision under lock 7 (typed payloads vs reflection dict) was deferred. This spike is that sub-decision resolving.
- **Current state on disk:**
  - `Assets/Sample/VerticalSlice/Engine/Stream/WireEvent.cs` — six concrete `WireEvent` subclasses, all pure-data, no methods beyond `Kind` discriminator + `ToString`.
  - `TagSetEvent.Value` and `TagChangeEvent.Prev/Next` are `object?`.
  - `TagSetEvent.Key` / `TagChangeEvent.Key` are `string` (tag name, not slot id).
  - `BlockStartEvent.Payload` is `IReadOnlyDictionary<string, object?>` populated via `PayloadCapture.Of` (reflection).
  - `Entity.Set` raises internal events; `EntityStore` subscribes and appends wire events. `Entity` knows nothing about the wire — recursion is impossible by construction (Mirror writes its own dict, never goes through `Entity.Set`).
- **Constraints inherited from parent spike:**
  - Wire = network protocol (lock 7). One class family for engine log + transport.
  - Server-authoritative (lock 1). Clients consume wire events; never produce.
  - No methods on wire events (P4 from vertical-slice locks). Consumer-side dispatch via type switch.

## Constraints driving this spike

Stated up front so the design space is bounded:

1. The wire event MUST be serializable and network-friendly. `object?` is out.
2. The system MUST be extensible — new tags AND new value types can be declared by downstream consumers without modifying the framework.
3. Strings are NOT needed on the wire. Display text is always inferrable client-side (content-id lookup, setup-time entity metadata, etc.).
4. Custom converters, protobuf, and source generation are all on the table.
5. Lock 7 stands — no split between "engine event" and "wire DTO." One class family.

## Prior art consulted

### SabberStone (Hearthstone simulator, C#)

Source pulled from `HearthSim/SabberStone/master/SabberStoneCore/src/Kettle/PowerHistory.cs` and `src/Model/Tag.cs`. Findings:

- **All tag values are `int`. Strictly.** `PowerHistoryEntity.Tags : IDictionary<GameTag, int>`. `PowerHistoryTagChange.Value : int`. No `object`, no union, no per-type variants.
- **`GameTag` is a closed C# enum** allocating slot ids. Tag identity is the enum value.
- **`Tag.TypedTags`** is a `Dictionary<GameTag, Type>` mapping ~15 specific tags (`ZONE`, `CARDTYPE`, `PLAYSTATE`, `STEP`, `RARITY`, `FACTION`, …) to the C# enum type whose int value they encode. Every entry in the table is an enum type — no struct, no class, no custom type.
- **Non-int values don't exist at the tag level.** Custom data either (a) packs into an int, (b) decomposes into multiple tags, or (c) lives outside the tag stream as typed fields on a specific event subclass.
- **Strings exist only as content ids** — `PowerHistoryBlockStart.EffectCardId : string` is a symbolic code like `"EX1_543"`. Display names ("Reno Jackson") are resolved client-side from content tables. User-typed text (player names) isn't carried as tag values at all — it's metadata on `PowerEntity` at session-start.
- **`BlockStart` has fully-typed fields** — `BlockType`, `Index`, `Source`, `Target`, `EffectCardId`. NOT a generic payload dict. Each block-kind has its own typed shape.
- **No handlers / no visitor pattern.** `IPowerHistoryEntry { PowerType; Print(); }`. Discriminator + debug formatter, nothing else. Consumers each do their own `switch (entry.PowerType)`.

### Legends of Runeterra (Riot, closed-source)

From `Product/research/legends-of-runeterra-architecture.md`: server-authoritative C#/Unity, closed binary protocol, no public RE coverage of wire grammar. Architecture *plausibly* similar to Hearthstone's entity+tag + delta-stream pattern (inference, not confirmed). Confirms the "fully dumb client + server-authored stream" framing but adds no new information about value typing.

### Brimstone (Hearthstone simulator)

`PowerHistory` is the engine's record. Same unified-record pattern as SabberStone — engine logs directly into `PowerHistory`; no separate engine-side event type.

### Fireplace (Hearthstone simulator, Python)

Does NOT use BlockStart/BlockEnd. Server-side `game_state` dict + `KettleSerializer` produces JSON diffs on mutation events. Different pattern — skips the unified-record concept entirely.

### Summary

Across the three open-source Hearthstone simulators we have research on: **none split engine event from wire DTO.** Two use a unified record family (SabberStone, Brimstone); one skips the abstraction (Fireplace). The "engine event vs wire DTO" split I floated mid-discussion was not actually present in any reference codebase — it was a hypothesis I couldn't back. Stance A/C (unified record) is the well-trodden path.

## Options considered

### Tag value typing

| # | Shape | Pros | Cons |
|---|---|---|---|
| 1 | **All-int + string intern table** (SabberStone-strict) | Most compact wire; zero polymorphism; matches the best reference codebase | Requires intern-table infrastructure for any string; can't represent custom structs without packing into int; forces content-id discipline for all named entities |
| 2 | **Tagged union `oneof { i64, bytes }`** (protobuf-native) | Closed wire alphabet; `bytes` is the escape hatch for any custom T; doesn't force intern-table on day one | One discriminator byte per value; `bytes` is opaque to naive consumers without the codec |
| 3 | **One concrete event per value type** (`TagSetIntEvent`, `TagSetBoolEvent`, …) | No discriminator field; each event fully typed | Wire alphabet doubles per value type; combinatorial; adding a value kind is a breaking wire change |

### Engine-event vs wire-DTO split

| # | Shape | Verdict |
|---|---|---|
| A | **Unified — WireEvent IS the wire** (lock 7 today) | Already locked. Every reference codebase does this. |
| B | **Split — rich engine event, separate typed wire DTO** | Dropped. No reference codebase exemplifies this; doubles surface area for a hypothetical need. |
| C | **Unified, but typed fields from day one** (A done right) | Selected. Stub serializer (Newtonsoft) still works on typed structs; protobuf pass becomes "swap serializer" not "redesign alphabet." |

## Outcome / proposed shape

**Selected: option 2 (tagged union) + option C (typed fields now).** Source-gen produces per-slot codecs from `TagKey<T>` declarations; the wire carries `i64 | bytes`; custom types route through `bytes`. SabberStone-strict (option 1) is the long-term end-state if content modeling matures, but the spike commits to the tagged-union shape because it preserves extensibility without forcing the intern-table infrastructure upfront.

### The sketch

```csharp
// =====================================================================
// Framework — hand-written
// =====================================================================

// ---------- Wire alphabet (pure data, no methods) --------------------

public abstract class WireEvent { public abstract WireKind Kind { get; } }

public enum WireKind : byte {
    EntityCreated, EntityDestroyed,
    TagSet, TagChange, TagRemoved,
    BlockStart, BlockEnd,
}

public sealed class EntityCreatedEvent : WireEvent {
    public long EntityId { get; }
    public EntityCreatedEvent(long id) { EntityId = id; }
    public override WireKind Kind => WireKind.EntityCreated;
}

public sealed class TagSetEvent : WireEvent {
    public long     EntityId { get; }
    public int      Slot     { get; }
    public TagValue Value    { get; }
    public TagSetEvent(long id, int slot, TagValue value)
        { EntityId = id; Slot = slot; Value = value; }
    public override WireKind Kind => WireKind.TagSet;
}

public sealed class TagChangeEvent : WireEvent {
    public long     EntityId { get; }
    public int      Slot     { get; }
    public TagValue Prev     { get; }
    public TagValue Next     { get; }
    public TagChangeEvent(long id, int slot, TagValue prev, TagValue next)
        { EntityId = id; Slot = slot; Prev = prev; Next = next; }
    public override WireKind Kind => WireKind.TagChange;
}

// ---------- TagValue: the only two wire-level value variants ---------

public readonly struct TagValue {
    public enum K : byte { I64, Bytes }

    public readonly K                     Kind;
    public readonly long                  IntValue;    // valid when Kind == I64
    public readonly ReadOnlyMemory<byte>  BytesValue;  // valid when Kind == Bytes

    private TagValue(K k, long i, ReadOnlyMemory<byte> b)
        { Kind = k; IntValue = i; BytesValue = b; }

    public static TagValue I64  (long v)                 => new(K.I64,   v, default);
    public static TagValue Bytes(ReadOnlyMemory<byte> b) => new(K.Bytes, 0, b);
}

// ---------- TagKey<T> + registry -------------------------------------

public abstract class TagKey {
    public int    Slot      { get; }
    public string Name      { get; }
    public Type   ValueType { get; }
    protected TagKey(int slot, string name, Type t)
        { Slot = slot; Name = name; ValueType = t; }
}

public sealed class TagKey<T> : TagKey {
    internal TagKey(int slot, string name) : base(slot, name, typeof(T)) { }
}

public static class TagRegistry {
    private static int _next = 1;
    private static readonly Dictionary<int, TagKey> _bySlot = new();

    public static TagKey<T> Allocate<T>(string name) {
        var k = new TagKey<T>(_next++, name);
        _bySlot[k.Slot] = k;
        return k;
    }
    public static TagKey BySlot(int slot) => _bySlot[slot];
}

// ---------- Codec interface + registry -------------------------------

public interface ITagCodec<T> {
    TagValue Encode(T value);
    T        Decode(TagValue wire);
}

public static class TagCodecs {
    private static readonly Dictionary<int, object> _bySlot = new();

    public static void Register<T>(TagKey<T> key, ITagCodec<T> codec)
        => _bySlot[key.Slot] = codec;

    public static ITagCodec<T> Get<T>(TagKey<T> key)
        => (ITagCodec<T>)_bySlot[key.Slot];

    // Type-erased encode — emitted as a switch by source gen.
    public static TagValue Encode(TagKey key, object value) => /* generated */ throw null!;
}

// ---------- Attribute the source gen looks for -----------------------

[AttributeUsage(AttributeTargets.Struct)]
public sealed class WireSerializableAttribute : Attribute { }
```

```csharp
// =====================================================================
// User code — hand-written (game/sample assembly)
// =====================================================================

[WireSerializable]
public readonly struct Vector3 {
    public readonly float X, Y, Z;
    public Vector3(float x, float y, float z) { X = x; Y = y; Z = z; }
}

public static class Tags {
    public static readonly TagKey<int>     Health   = TagRegistry.Allocate<int>("Health");
    public static readonly TagKey<bool>    IsMinion = TagRegistry.Allocate<bool>("IsMinion");
    public static readonly TagKey<Vector3> Pos      = TagRegistry.Allocate<Vector3>("Pos");
}
```

```csharp
// =====================================================================
// Source-gen output — emitted at build time, one file per assembly
// =====================================================================

// Built-in primitive codecs — emitted for int/long/bool/short/byte/enum.
internal sealed class _IntCodec : ITagCodec<int> {
    public TagValue Encode(int v)      => TagValue.I64(v);
    public int      Decode(TagValue w) => (int)w.IntValue;
}
internal sealed class _BoolCodec : ITagCodec<bool> {
    public TagValue Encode(bool v)     => TagValue.I64(v ? 1 : 0);
    public bool     Decode(TagValue w) => w.IntValue != 0;
}

// Custom struct codec — emitted because Vector3 has [WireSerializable].
// Walks public readonly fields in declared order, packs little-endian.
internal sealed class _Vector3Codec : ITagCodec<Vector3> {
    public TagValue Encode(Vector3 v) {
        var buf = new byte[12];
        var s   = buf.AsSpan();
        BinaryPrimitives.WriteSingleLittleEndian(s,      v.X);
        BinaryPrimitives.WriteSingleLittleEndian(s[4..], v.Y);
        BinaryPrimitives.WriteSingleLittleEndian(s[8..], v.Z);
        return TagValue.Bytes(buf);
    }
    public Vector3 Decode(TagValue w) {
        var s = w.BytesValue.Span;
        return new Vector3(
            BinaryPrimitives.ReadSingleLittleEndian(s),
            BinaryPrimitives.ReadSingleLittleEndian(s[4..]),
            BinaryPrimitives.ReadSingleLittleEndian(s[8..]));
    }
}

// Type-erased encoder dispatcher — emitted as a switch on slot id.
internal static class _TagCodecsDispatch {
    public static TagValue Encode(TagKey key, object value) => key.Slot switch {
        1 => TagValue.I64((int)value),                                  // Health
        2 => TagValue.I64(((bool)value) ? 1 : 0),                       // IsMinion
        3 => new _Vector3Codec().Encode((Vector3)value),                // Pos
        _ => throw new InvalidOperationException($"slot {key.Slot}"),
    };
}

// Registry installer — runs once at assembly load.
public static class _GeneratedTagCodecs {
    [ModuleInitializer]
    internal static void Install() {
        TagCodecs.Register(Tags.Health,   new _IntCodec());
        TagCodecs.Register(Tags.IsMinion, new _BoolCodec());
        TagCodecs.Register(Tags.Pos,      new _Vector3Codec());
    }
}
```

```csharp
// =====================================================================
// EntityStore — translating Entity events into wire events
// =====================================================================

private void OnTagChanged(Entity e, TagKey key, object prev, object next) {
    var wirePrev = TagCodecs.Encode(key, prev);
    var wireNext = TagCodecs.Encode(key, next);
    _log.Append(new TagChangeEvent(e.Id, key.Slot, wirePrev, wireNext));
}
```

```csharp
// =====================================================================
// Mirror — applies wire events, decodes on read
// =====================================================================

public sealed class MirrorEntity {
    public long Id { get; }
    public Dictionary<int, TagValue> Tags { get; } = new();   // stores wire form

    public T Get<T>(TagKey<T> key) {
        var wire = Tags[key.Slot];
        return TagCodecs.Get(key).Decode(wire);
    }
}

public sealed class MirrorWorld {
    public void Apply(WireEvent ev) {
        switch (ev) {
            case TagSetEvent ts:    _entities[ts.EntityId].Tags[ts.Slot] = ts.Value; break;
            case TagChangeEvent tc: _entities[tc.EntityId].Tags[tc.Slot] = tc.Next;  break;
            // ...
        }
    }
}
```

```protobuf
// =====================================================================
// Protobuf surface — the wire schema
// =====================================================================

message TagValue {
  oneof v {
    int64 i = 1;
    bytes b = 2;
  }
}

message TagSetEvent {
  int64    entity_id = 1;
  int32    slot      = 2;
  TagValue value     = 3;
}

message TagChangeEvent {
  int64    entity_id = 1;
  int32    slot      = 2;
  TagValue prev      = 3;
  TagValue next      = 4;
}

// + EntityCreated, EntityDestroyed, TagRemoved, BlockStart, BlockEnd
```

### End-to-end trace (single Vector3 tag write)

```
game code:        entity.Set(Tags.Pos, new Vector3(1.0f, 2.5f, 3.0f))
   ↓
Entity.Set:       slot[3] = boxed Vector3
                  fires internal TagChanged(entity, key=Pos, prev, next)
   ↓
EntityStore.OnTagChanged:
                  TagCodecs.Encode(key=Pos, value=...) → dispatched by slot 3 → _Vector3Codec
                    returns TagValue.Bytes([01 00 80 3F  00 00 20 40  00 00 40 40])
                  EventLog.Append(new TagChangeEvent(entityId, 3, prevWire, nextWire))
   ↓
serializer:       protobuf writes TagChangeEvent { entity_id, slot, prev, next }
   ↓
[wire — black box]
   ↓
deserializer:     protobuf reads back TagChangeEvent
   ↓
MirrorWorld.Apply:
                  mirrorEntity.Tags[3] = tc.Next      // stores TagValue raw, no decode
   ↓
consumer read:    mirrorEntity.Get(Tags.Pos)
                    → TagCodecs.Get(Tags.Pos).Decode(TagValue.Bytes(...))
                    → returns Vector3(1.0, 2.5, 3.0)
```

## Invariants

These must hold for the design to work:

1. **Wire alphabet is closed.** The set of `WireEvent` subclasses and the set of `TagValue` variants (`I64`, `Bytes`) are framework-controlled. Downstream consumers do NOT add variants.
2. **Tag declaration space is open.** Any assembly can declare `TagKey<T>` for any `T` that has a codec.
3. **No `object?` reaches the wire.** Every wire event has typed fields. `TagValue` is the only place wire-level polymorphism lives, and it's a closed two-variant union.
4. **No `string`-valued tags.** Strings are not a wire-level value kind. Display text resolves client-side from content tables; entity-level names live as setup-time metadata on `EntityCreatedEvent` (or equivalent), not as tag-change events.
5. **Mirror stores `TagValue` raw, decodes on read.** Apply is type-erased; decode is type-directed by `TagKey<T>` at read time.
6. **No methods on `WireEvent`.** Pure data. Consumers dispatch via `switch` on concrete type.
7. **Source gen produces deterministic codec output.** Same `TagKey<T>` declarations on client and server → identical codec registry → no decode drift. Registry hash exchanged at session start (deferred to transport pass).

## Source-gen requirements

The generator does four things at build time:

1. **Discover every `TagRegistry.Allocate<T>(...)` call site** across the compilation (Roslyn syntax walker).
2. **Decide codec emission per `T`:**
   - `T ∈ {int, long, short, byte, bool}` → emit primitive codec → `I64` variant.
   - `T : System.Enum` (int-backed) → emit cast codec → `I64` variant.
   - `T` is a struct with `[WireSerializable]` → emit field-walk codec → `Bytes` variant.
   - Otherwise → compile-time diagnostic pointing at the declaration site: *"Tag value type X needs `[WireSerializable]` or a manual `ITagCodec<X>` registration."*
3. **Emit a type-erased dispatch switch** on slot id for the `TagCodecs.Encode(TagKey, object)` entry point used by `EntityStore` (since `key` is `TagKey` not `TagKey<T>` at that call site).
4. **Emit a `[ModuleInitializer]`** that registers every codec into `TagCodecs` at assembly load.

Same pattern extended to `BlockStartEvent.Payload` (deferred to next pass — see below).

## Open items / cost call-outs

- **`BlockStartEvent.Payload` is the parallel surface.** Today it's a reflection-walked `Dictionary<string, object?>`. Same fix applies: each runnable type (Command/Effect/Request) declares a payload struct; source gen emits the codec; `BlockStartEvent` carries `(BlockKind kind, TagValue payload)` or a dedicated `Payload : bytes` field with the same dispatch shape as tag values. Not sketched in detail here — same machinery, different call sites.
- **Source-gen dependency is new.** The slice today has zero source generation. This spike commits to introducing a Roslyn generator project. If we want to defer, the intermediate shape is identical except codecs are *hand-registered* at bootstrap (~10-line manual installer per project) and `[WireSerializable]` becomes a documentation convention rather than a build-checked contract.
- **Codec registry sync between client and server.** Mismatched `TagKey` declarations → silently wrong decodes. Mitigation: exchange a registry hash at session-start handshake (deferred to transport pass).
- **String-valued tags in the current sample must migrate.**
  - `Tags.CardName : string` → `Tags.CardDefId : int`, with client-side content lookup.
  - `Tags.ZoneName : string` → property of the zone entity, carried once at entity creation as metadata, not a per-card tag.
  - `Tags.PlayerName : string` → same — metadata on the player entity, not a tag-change.
- **Bytes-variant values are opaque to naive consumers.** A network analyzer sees `TagValue { b: <12 bytes> }` for `Vector3` without the codec table. Acceptable for closed-ecosystem games; awkward if we ever publish the protocol externally. Future-self problem.
- **`[ModuleInitializer]` Unity availability.** Source-gen-emitted module initializers work in Unity 2022+ with .NET Standard 2.1; verify against the project's Unity version before committing. The spike implementation sidesteps this by using an explicit `Codecs.Install()` call at bootstrap — when source-gen lands, the installer becomes a `[ModuleInitializer]` and the explicit call goes away.
- **`BinaryPrimitives.Single*` is .NET 5+, not on Unity 2.1.** Source-gen emitting codecs for `float`-bearing structs cannot use `WriteSingleLittleEndian` / `ReadSingleLittleEndian` — those don't exist on Unity's .NET Standard 2.1 surface. Use bit-cast via `BitConverter.SingleToInt32Bits` + `BinaryPrimitives.WriteInt32LittleEndian` (both 2.1) for identical wire bytes. Same pattern for `double` ↔ `Int64Bits`. Confirmed in the spike implementation under Unity 6000.4.0f1.

## When this lands

This spike informs (eventually) one of:

- A **Model** doc capturing the typed-payload wire shape as a settled design.
- An **ADR** if a more durable record is wanted.

Implementation order, when picked up:

1. Add `TagValue`, `ITagCodec<T>`, `TagCodecs` registry to the engine.
2. Rewrite `TagSetEvent` / `TagChangeEvent` / `TagRemovedEvent` to use `TagValue` instead of `object?`.
3. Migrate the three string-valued tags off the tag stream (content id + metadata moves).
4. Hand-register codecs in a manual installer (defer source gen).
5. Apply same shape to `BlockStartEvent.Payload`.
6. Add source gen project; replace manual installer with generated one.
7. Swap Newtonsoft stub for protobuf when transport pass lands.

Steps 1–4 are the load-bearing part; the rest is mechanical.

---

## What actually shipped

The spike's shape (steps 1, 2, 4 from the plan above) landed at `Assets/Spike/TypedPayloads/` as an independent, headless-testable spike — deliberately decoupled from `Assets/Sample/VerticalSlice/` so iteration on the wire shape doesn't disturb the existing 71-test slice. Step 3 (string-tag migration) is a VerticalSlice change, not a spike change; deferred to its own pass. Steps 5–7 (BlockStart payload, source-gen, protobuf) are explicitly future work.

### Layout

```
Assets/Spike/TypedPayloads/
├── Engine/                         (Spike.TypedPayloads.Engine — noEngineRefs)
│   ├── WireKind.cs                 byte-sized discriminator
│   ├── WireEvents.cs               base + EntityCreated/Destroyed, TagSet/Change/Removed
│   ├── TagValue.cs                 readonly struct { I64 | Bytes }
│   ├── TagKey.cs                   non-generic base + TagKey<T>
│   ├── TagRegistry.cs              name-stable slot allocator (same-name same-type → same key)
│   ├── ITagCodec.cs                ITagCodec + ITagCodec<T> + TagCodec<T> base
│   ├── TagCodecs.cs                global slot → codec registry
│   ├── WireSerializableAttribute   marker (currently doc-only; source-gen hook)
│   ├── Entity.cs                   slot dict + internal TagSet/Changed/Removed events
│   ├── EntityStore.cs              subscribes, codec-encodes, appends wire events
│   ├── EventLog.cs                 append-only IReadOnlyList<WireEvent>
│   ├── MirrorEntity.cs             stores TagValue raw, decodes on Get<T>
│   └── MirrorWorld.cs              Apply / Unapply switch on concrete event type
│
├── Sample/                         (Spike.TypedPayloads.Sample — refs Engine, noEngineRefs)
│   ├── Vector3.cs                  [WireSerializable] readonly struct, 3 floats — local, not UnityEngine.Vector3
│   ├── Tags.cs                     Health:int / IsMinion:bool / Pos:Vector3
│   └── Codecs.cs                   explicit Install() — the stand-in for source-gen output
│
└── Tests/                          (Spike.TypedPayloads.Tests — Editor + nunit)
    ├── TagValueTests.cs            6 cases — variants + equality + bytes-content compare
    ├── CodecRoundtripTests.cs      9 cases — int/bool/Vector3 round-trip, type-erased path, missing-slot throw
    └── EndToEndTests.cs            9 cases — full pipeline, Apply/Unapply symmetry, destroy + remove
```

### Validation

Under Unity **6000.4.0f1** batch-mode:

- Initial compile failed on `BinaryPrimitives.WriteSingleLittleEndian` / `ReadSingleLittleEndian` — confirmed not available on Unity's .NET Standard 2.1 surface. Fixed by bit-casting via `BitConverter.SingleToInt32Bits` + `BinaryPrimitives.WriteInt32LittleEndian` (both 2.1). Identical bytes on the wire. Same approach will need to be source-gen output for any `float`/`double`-bearing struct.
- **24/24 spike tests pass.** TagValueTests (6), CodecRoundtripTests (9), EndToEndTests (9).
- **119/119 EditMode tests project-wide pass.** No regression against the existing 95 tests in `VerticalSlice` + `CardPresentation`.

### Deviations from the original sketch

- **`Codecs.Install()` is an explicit bootstrap call**, not a `[ModuleInitializer]`. The attribute path works under Unity 6 but the explicit call is easier to reason about while we don't have source-gen yet — the installer becomes a `[ModuleInitializer]` when the generator lands.
- **`TagRegistry.Allocate<T>` is name-stable.** Same name + same type → returns the existing key (idempotent). Same name + different type → throws. Catches accidental shadowing across assemblies at startup rather than producing two slots that look the same.
- **`ITagCodec` has a non-generic surface** (`EncodeObject` / `DecodeObject`) in addition to the typed `ITagCodec<T>`. The non-generic surface is what `EntityStore` uses since its internal-event signature is type-erased; the typed surface is what `MirrorEntity.Get<T>` uses for on-read decoding. `TagCodec<T>` abstract base implements the non-generic side by delegating to the typed methods, so codec authors only override `Encode` + `Decode`.

### Open questions for the iteration pass

These came out of the build but aren't blockers — flagged for when we revisit:

- **`TagValue.GetHashCode` for the `Bytes` variant is `Length`** — cheap but weak. Switch to a content hash if `TagValue` ever ends up as a dictionary key in a hot path.
- **`MirrorWorld.Unapply` of `EntityDestroyed` re-creates an empty entity.** Fine for the spike (no test relies on full state-restore-through-destroy) but not correct for a real replay. The fix needs the wire to carry the pre-destruction tag snapshot, or the consumer to checkpoint before destruction. Tracked as part of the eventual replay/scrub design.
- **Codec registry is global static state.** Works for one process; first thing that surfaces when we want client + server in the same test fixture is "whose registry are we sharing?" Deferred until needed.

### Status note

This spike's shape is built and green; the doc stays `active` until we either fold the findings into a Model (graduating the spike) or commit to the source-gen pass that closes out steps 5–7. New work on the wire — `BlockStart.Payload` migration, source-gen project, transport — should land as its own spike → model → ADR per workflow, not as further appends here.
