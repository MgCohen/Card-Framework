# AutoPacker — Proposal: typed `PackTyped` / `UnpackTyped` accessors

> **Status: draft (2026-05-19).** Asks AutoPacker to emit zero-allocation typed Pack/Unpack methods alongside today's boxed `IPackedStruct`-returning surface, plus two new generic interfaces that name them. NGO-agnostic. Enables the consuming framework to build any transport bridge (FastBufferWriter, raw bytes, in-process channel, save/replay) without boxing.
>
> Supersedes the direction in [`autopacker-fastbufferwriter.md`](autopacker-fastbufferwriter.md). That doc asked AutoPacker to emit NGO-aware methods directly; this one keeps NGO knowledge out of AutoPacker and gives the consumer the typed surface they need to build the NGO bridge themselves.

## Context

We're using AutoPacker as the codec layer for a Unity card framework's wire. The transport target is Unity NGO custom messages, which serialize through `FastBufferWriter` / `FastBufferReader` — `ref struct` over native memory, zero-allocation, expecting unmanaged structs by value.

AutoPacker today emits, per `[AutoPack]` type:

- A nested `Packed` unmanaged struct.
- `IPackedStruct Pack(IPackingHandler handler = null)` — returns the Packed struct boxed as the interface.
- `void Unpack(IPackedStruct packed, IPackingHandler handler = null)` — receives the boxed interface, casts back internally.

Both methods cross an `IPackedStruct` interface boundary. That boundary boxes the value-type `Packed` struct on the way out (Pack) and forces re-boxing if the caller has a stack-allocated `Packed` (Unpack — passing a value to an interface-typed parameter boxes).

**Today's wire works around this** by carrying the boxed `IPackedStruct` through an in-process `Queue<Envelope>`. The box from `Pack` rides the queue and arrives at `Unpack` still boxed — net 1 box per round-trip. Acceptable for in-process testing.

**Phase 6 swaps the queue for `FastBufferWriter` / `FastBufferReader`.** The new path is value-type all the way: write the struct's bytes on send, read them back into a stack `Packed` on receive. But without typed accessors:

- Send: `Pack(h)` boxes once, we cast back to `TPacked` (free unbox), write bytes.
- Receive: read bytes into stack `TPacked`, then call `Unpack(packed, h)` — the call boxes the value to satisfy the `IPackedStruct` parameter type.

Net 2 boxes per round-trip — a regression from today's 1. We'd ship a perf step backward and call it progress.

## The framework-side design this enables

`Framework.Wire` introduces a generic intermediate base `WireEvent<TPacked>` that owns the NGO bridge in one place:

```csharp
public abstract class WireEvent<TPacked> : WireEvent, IPackable<TPacked>, IUnpackable<TPacked>
    where TPacked : unmanaged, IPackedStruct
{
    public abstract TPacked PackTyped(IPackingHandler handler = null);
    public abstract void UnpackTyped(in TPacked packed, IPackingHandler handler = null);

    public sealed override void WriteToNetwork(ref FastBufferWriter w, IPackingHandler h)
    {
        var packed = PackTyped(h);
        w.WriteValueSafe(packed);          // zero box, zero alloc
    }

    public sealed override void ReadFromNetwork(ref FastBufferReader r, IPackingHandler h)
    {
        r.ReadValueSafe(out TPacked packed);
        UnpackTyped(packed, h);            // zero box, zero alloc
    }
}
```

Concrete events stay declarative:

```csharp
public partial class EntityCreatedEvent : WireEvent<EntityCreatedEvent.Packed>
{
    [Packed] public long EntityId;
}
```

AutoPacker emits `PackTyped` / `UnpackTyped` as overrides on the concrete event — same hierarchy machinery used today for boxed `Pack` / `Unpack` (the round-2 follow-up that closed Config 1). NGO knowledge lives entirely in `WireEvent<TPacked>`, which is hand-written in `Framework.Wire`. AutoPacker never references NGO.

## Proposed change

### Two new interfaces in `Scaffold.AutoPacker`

```csharp
public interface IPackable<TPacked> : IPackable where TPacked : unmanaged
{
    TPacked PackTyped(IPackingHandler handler = null);
}

public interface IUnpackable<TPacked> : IUnpackable where TPacked : unmanaged
{
    void UnpackTyped(in TPacked packed, IPackingHandler handler = null);
}
```

Each extends the existing non-generic interface so any existing `IPackable` / `IUnpackable` consumer keeps working unchanged. The generic forms add the typed-value access path.

### Generator emission per `[AutoPack]` type

For a type with nested `Packed` struct (i.e. has its own `[Packed]` fields or inherits any), emit:

```csharp
// Typed accessors — zero-box on both sides.
public TPacked PackTyped(IPackingHandler handler = null)
{
    // Same body AutoPacker generates today for Pack(), but returns Packed directly
    // instead of boxing as IPackedStruct.
    return new Packed
    {
        EntityId = this.EntityId,
        // ... handler-mapped fields go through handler.Resolve<TSource, TTarget> as today
    };
}

public void UnpackTyped(in Packed packed, IPackingHandler handler = null)
{
    // Same body as today's Unpack(IPackedStruct, IPackingHandler), but receives
    // the typed value directly — no unbox cast at entry.
    this.EntityId = packed.EntityId;
    // ...
}
```

The existing boxed surface becomes a thin wrapper around the typed one:

```csharp
public virtual IPackedStruct Pack(IPackingHandler handler = null) => PackTyped(handler);

public virtual void Unpack(IPackedStruct packed, IPackingHandler handler = null)
    => UnpackTyped((Packed)packed, handler);
```

Consumers who still call the boxed `Pack` / `Unpack` see no change. Consumers who switch to the typed surface get zero-box round-trip.

### Hierarchy emission (mirrors round-2 follow-up)

The hierarchy machinery that round-2 added for `IPackable` / `IUnpackable` extends naturally:

- If the type derives from an `[AutoPack]` ancestor whose generated `Packed` includes inherited fields, the derived's `PackTyped` / `UnpackTyped` operate on the derived's closed `Packed` shape (which already includes the inherited fields per round-2's behavior).
- If the type derives from a hand-written abstract base that declares `PackTyped` / `UnpackTyped` as abstract (e.g. `WireEvent<TPacked>` in our framework), the generator emits `override` on the derived. Same trigger logic as today's `IPackable` / `IUnpackable` user-declared-abstract-base case (the `WireCommand` test pattern in `AutoPackerTests.cs`).

### Interface declaration on the generated partial

Add `IPackable<Packed>` and `IUnpackable<Packed>` to the generated partial's interface list, alongside the existing non-generic `IPackable` / `IUnpackable` emission. Like today, the generator only adds them if the user hasn't already declared them by hand.

## Allocation accounting

| Path | Today (boxed) | Proposed (typed) |
|---|---|---|
| `Pack` returns | 1 box | 0 box |
| `Unpack` receives | 0 box (already boxed at caller) | 0 box |
| In-process queue round-trip | 1 box total | 0 box total |
| `FastBufferWriter` round-trip | 2 box total (regression vs queue) | 0 box total |
| Save / replay round-trip | 1 box (or N for replay log) | 0 box |

Existing consumers calling `Pack(h)` keep paying 1 box (same as today — the thin wrapper boxes the typed value). Anyone who opts into the typed surface drops to 0.

## Open question — hierarchy walker through a non-`[AutoPack]` generic intermediate

The framework design puts a hand-written generic intermediate between the `[AutoPack]` root and the concrete leaf:

```csharp
[AutoPack]
public abstract partial class WireEvent { }                                       // [AutoPack] root

public abstract class WireEvent<TPacked> : WireEvent { ... }                       // hand-written, NOT [AutoPack]

public partial class EntityCreatedEvent : WireEvent<EntityCreatedEvent.Packed>     // leaf, no [AutoPack]
{
    [Packed] public long EntityId;
}
```

Today's tests cover:

- `[AutoPack]` direct base → no-attribute derived: works.
- `[AutoPack]` open-generic base → closed-by-inheritance derived: works.

The case **not** covered: `[AutoPack]` root → hand-written non-`[AutoPack]` generic intermediate → no-attribute leaf. Does the walker traverse through the intermediate to discover the leaf needs codegen, or does it stop at the first non-`[AutoPack]` base?

If it stops, the framework either:

- Sprinkles `[AutoPack]` on every leaf event (re-introduces per-event boilerplate the framework was designed to avoid), or
- Asks for a walker tweak: traverse non-`[AutoPack]` intermediates to find an `[AutoPack]` ancestor.

A 10-line smoke test in `Scaffold.Autopacker.Tests` settles it:

```csharp
[AutoPack]
public abstract partial class Root { }

public abstract class Middle<T> : Root where T : unmanaged
{
    public abstract T GetIt();
}

public partial class Leaf : Middle<Leaf.SomePackedShape>
{
    [Packed] public int X;
    public override Leaf.SomePackedShape GetIt() => default;
}

[Test]
public void Leaf_Through_NonAutoPack_Intermediate_GetsCodegen()
{
    var src = new Leaf { X = 7 };
    var packed = src.Pack();
    Assert.IsNotNull(packed);
}
```

Result determines whether this proposal is two changes (typed accessors only) or three (typed accessors + walker traversal).

## Backwards compatibility

Purely additive.

- Existing `Pack(h)` / `Unpack(packed, h)` continue to work — they delegate to the new typed methods, returning / accepting through the same `IPackedStruct` boundary as today.
- Existing `IPackable` / `IUnpackable` consumers are untouched. The generic forms extend, not replace.
- Generated partials gain two new methods and two new interface declarations. No removals, no signature changes on existing surfaces.
- Hand-written `IPackable` / `IUnpackable` implementations elsewhere in user code continue to compile and run.

## Tradeoff and honest framing

This is a smaller-scoped ask than the FastBufferWriter proposal. Two new methods per `[AutoPack]` type, two new interfaces in the AutoPacker namespace, no new dependencies. The emission bodies are simpler than the existing boxed versions (they're what the current Pack/Unpack do *before* the box/unbox steps).

Why this is the right shape:

- **AutoPacker stays a structure-to-struct mapper.** It doesn't grow knowledge of Unity, NGO, or any specific transport. The typed surface is useful for any consumer that wants to skip boxing — including consumers we haven't built yet (save system, in-memory replay log, MessagePack codec, custom binary protocol).
- **NGO knowledge lives in `Framework.Wire`.** That's the right separation. The wire framework owns its transport choice; AutoPacker owns the typed shape that any transport can consume.
- **Composes with the hierarchy machinery shipped in round 2.** No new generator concepts — `PackTyped` / `UnpackTyped` follow the same trigger and override emission rules as the boxed pair.
- **No source-gen request to a third party.** You maintain AutoPacker. The framework's design no longer waits on anything external.

If this lands, `Framework.Wire`'s Phase 6 transport refactor becomes a single-repo change — frame the events, build the `WireEvent<TPacked>` bridge, drop in `FastBufferWriter`. No reflection, no boilerplate per event, no AutoPacker proposal in flight.

## What needs to change in Scaffold

1. **New interfaces** in `Scaffold.AutoPacker`:
   - `IPackable<TPacked> : IPackable` with `TPacked PackTyped(IPackingHandler = null)`.
   - `IUnpackable<TPacked> : IUnpackable` with `void UnpackTyped(in TPacked, IPackingHandler = null)`.
2. **Generator emission** per `[AutoPack]` type that has a generated `Packed`:
   - `PackTyped` returning the concrete nested `Packed`.
   - `UnpackTyped` taking `in Packed`.
   - Existing `Pack` / `Unpack` re-emitted as thin wrappers that delegate to the typed versions (preserves backwards compatibility, eliminates duplicate body emission).
3. **Hierarchy support** — emit `override` on `PackTyped` / `UnpackTyped` when the base declares them abstract, same trigger as the existing `IPackable` / `IUnpackable` override emission.
4. **Test coverage** in `Scaffold.Autopacker.Tests`:
   - Typed round-trip for the same shapes the boxed surface already covers (`PlayerState`, `SecurePayload`, `ExtendedPayload`).
   - Hierarchy override emission via a hand-written abstract `IPackable<T>` / `IUnpackable<T>` base.
   - The non-`[AutoPack]` intermediate walker case (the smoke test above).
5. **Smoke test for the walker question** — confirms whether the walker traverses non-`[AutoPack]` intermediates. If not, add a walker tweak to do so. If yes, no further change needed.

## TL;DR

- **The ask:** generator emits `TPacked PackTyped(IPackingHandler)` and `void UnpackTyped(in TPacked, IPackingHandler)` on every `[AutoPack]` type, plus two new generic interfaces in AutoPacker. Existing boxed `Pack` / `Unpack` become wrappers over the typed pair.
- **Why:** lets consumers cross any transport boundary (FastBufferWriter, raw bytes, save/replay) without boxing. AutoPacker stays NGO-agnostic; the consuming framework builds whatever bridge it needs over a typed surface.
- **Open question:** does the hierarchy walker traverse a non-`[AutoPack]` generic intermediate between the root and the leaf? A smoke test settles it; a walker tweak may or may not be needed.
- **Backwards compatibility:** purely additive. Existing consumers untouched.
- **Supersedes:** [`autopacker-fastbufferwriter.md`](autopacker-fastbufferwriter.md) — that proposal asked AutoPacker to know about NGO; this one keeps the NGO surface in the consuming framework.
