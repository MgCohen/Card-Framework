# Wire Typed Payloads

Resolves the sub-decision deferred from `wire-network-protocol.md` lock 7: **how wire-event field values get typed** once the Newtonsoft stub is replaced by a real binary serializer. Two open surfaces: tag values on `TagSetEvent` / `TagChangeEvent`, and `BlockStartEvent.Payload`.

## What this spike proves

- The wire alphabet can stay **closed** (six concrete `WireEvent` types) while the space of declarable `TagKey<T>` value types stays **open** — via per-`T` codecs registered alongside tag declarations.
- `TagSetEvent.Value` and `TagChangeEvent.Prev/Next` move off `object?` to a typed payload that round-trips a binary serializer without reflection at the hot path.
- The shape is **headless-testable**: codec round-trip tests, end-to-end (apply → wire → mirror → unapply), independent of Unity and independent of the VerticalSlice asmdef.
- Decoupling the spike from `VerticalSlice/` lets the wire shape iterate without disturbing the 71-test slice. Migration of the slice itself is deferred — this spike validates the design first.

## High-level idea

```
                    declare:                wire:
  TagKey<int>  ──┬──▶ slot 7, name "Health"  ┬──▶ TagSetEvent(slot=7, payload=Int(30))
                 │                            │
  TagKey<bool> ──┼──▶ slot 9, name "DS"      ┼──▶ TagChangeEvent(slot=9, prev=Bool(true), next=Bool(false))
                 │                            │
  TagKey<T>    ──┘─── per-T codec  ──────────┘
                      (Read/Write)
```

Each value type has a registered codec; `TagKey<T>` declaration also registers its codec. The wire reader/writer dispatches on slot → codec → typed payload. No `object` boxing on the hot path; the codec table is the single extension point.

## How to validate

**Headless tests** — `Window → General → Test Runner → EditMode`, run `Spike.TypedPayloads.Tests`:
- `TagValueTests` — typed payload contracts per T.
- `CodecRoundtripTests` — serialize → deserialize equality.
- `EndToEndTests` — apply through pipeline, emit wire, mirror rebuild, unapply.

**Interactive scene** — Menu → `Tools/TypedPayloads/Build Sample Scene`, open `Unity/Sample.unity`, hit Play. IMGUI sandbox shows typed payloads moving across the wire in real time.

## Spike doc

Built from [`Product/spikes/wire-typed-payloads.md`](../../../../Product/spikes/wire-typed-payloads.md) — context, alternatives considered (reflection dict vs codec table vs source-gen), and the deferred follow-ups (string-tag→slot migration in VerticalSlice, `BlockStartEvent.Payload`, source-gen, protobuf). Status: **active**. Parent: `wire-network-protocol.md` (lock 7).
