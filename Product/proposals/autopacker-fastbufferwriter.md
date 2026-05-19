# AutoPacker — Proposal: `IFastBufferPackable` for direct NGO-aligned packing

> **Status: draft (2026-05-19).** Asks AutoPacker to emit FastBufferWriter / FastBufferReader serialization helpers on every `[AutoPack]` type, so downstream code can pack/unpack directly into NGO's native buffers without reflection or double-copy.

## Context

We're using AutoPacker as the codec layer for a Unity card framework's wire. Our framework's transport target is **Unity NGO custom messages**, which use `FastBufferWriter` (write side) and `FastBufferReader` (read side) — `ref struct` types that wrap native buffers, zero-allocation, designed for unmanaged-struct write/read. NGO custom message handlers already receive a `FastBufferReader` directly.

AutoPacker today emits, per `[AutoPack]` type:

- A nested `Packed` struct (unmanaged, blittable).
- `IPackedStruct Pack(IPackingHandler)` — returns the Packed struct via the `IPackable` interface.
- `void Unpack(IPackedStruct, IPackingHandler)` — via the `IUnpackable` interface.

To serialize into a `FastBufferWriter`, the consumer has to do this dance:

```csharp
var packed = (T_Packed)ev.Pack(handler);  // needs concrete T_Packed at compile time
writer.WriteValueSafe(packed);
```

The problem is `T_Packed` (the nested `Packed` struct). It's per-`[AutoPack]` type and not exposed through any interface. In our framework's generic dispatch code (`Channel<T> where T : WireEvent, new()`), we don't have `T_Packed` statically — we only have `T`. Three workarounds, all unsatisfying:

1. **Two type params on Channel** — `Channel<TEvent, TPacked>`. Verbose at every Register call site.
2. **Reflection at Register** — walk `typeof(T)` for the nested `Packed`, build typed delegates via `Expression.Compile`. Works on Mono, uncertain on IL2CPP, brings reflection back into a layer we just got reflection-free.
3. **AutoPacker emits the FastBuffer helpers directly** — what this proposal asks for.

## Proposed change

### New interface in `Scaffold.AutoPacker` (or a satellite package)

```csharp
public interface IFastBufferPackable
{
    void WriteTo(ref FastBufferWriter writer, IPackingHandler handler = null);
    void ReadFrom(ref FastBufferReader reader, IPackingHandler handler = null);
}
```

`FastBufferWriter` / `FastBufferReader` come from `Unity.Netcode` (NGO). Both are `ref struct`, both methods take them by `ref` (matches NGO's idiomatic usage — position state advances per write/read).

### Generator emission per `[AutoPack]` type

The generator adds `IFastBufferPackable` to the partial class's interface list and emits:

```csharp
public void WriteTo(ref FastBufferWriter writer, IPackingHandler handler = null)
{
    var packed = (Packed)Pack(handler);
    writer.WriteValueSafe(packed);
}

public void ReadFrom(ref FastBufferReader reader, IPackingHandler handler = null)
{
    reader.ReadValueSafe(out Packed packed);
    Unpack(packed, handler);
}
```

Two extra emissions per `[AutoPack]` type. Both delegate to the existing emitted machinery (`Pack`, `Unpack`, the nested `Packed` struct). Zero new code paths inside AutoPacker's logic — just two new method bodies that compose what's already there with FastBuffer's `WriteValueSafe<T>` / `ReadValueSafe<T>`.

### NGO dependency — packaging options

`FastBufferWriter` lives in `Unity.Netcode`, so emitting these methods requires AutoPacker (or whatever assembly emits them) to reference NGO. Three plausible ways to handle this:

1. **AutoPacker takes an optional NGO dependency.** If NGO is present in the consuming project, the generator emits FastBuffer methods; otherwise it skips them. Single package, conditional emission.
2. **Satellite package: `Scaffold.AutoPacker.Netcode`.** Lives alongside core AutoPacker, depends on both AutoPacker and NGO. Adds the FastBuffer emission and the `IFastBufferPackable` interface. Consumers who don't use NGO never see it.
3. **AutoPacker core takes a hard NGO dependency.** Cleanest for users but couples AutoPacker to NGO forever.

We'd be happy with #1 or #2. #2 is probably the right packaging boundary — keeps AutoPacker's core engine-free for non-NGO consumers and explicitly opts in to the FastBuffer surface.

## Consumer-side impact

Our framework's `Channel<T>` becomes:

```csharp
public sealed class Channel<T> where T : WireEvent, IFastBufferPackable, new()
{
    readonly IDispatcher _dispatcher;
    internal Channel(IDispatcher dispatcher) => _dispatcher = dispatcher;

    internal void Pack(ref FastBufferWriter writer, T ev)
        => ev.WriteTo(ref writer, _dispatcher.Handler);

    internal void Deliver(FastBufferReader reader)
    {
        var ev = new T();
        ev.ReadFrom(ref reader, _dispatcher.Handler);
        _dispatcher.Receive(ev);
    }
}
```

Zero reflection. Zero ceremony at call sites. The constraint `where T : WireEvent, IFastBufferPackable, new()` says exactly what we need from T, and the AutoPacker-generated partials satisfy it.

End-user authoring is unchanged from today:

```csharp
public partial class EntityCreatedEvent : WireEvent
{
    [Packed] public long EntityId;
}
```

The `IFastBufferPackable` implementation flows in via the generator, just like `IPackable` and `IUnpackable` do today (now that proposal-2 hierarchy support landed). Concrete events declare nothing about FastBuffer.

## Backwards compatibility

Purely additive. Every `[AutoPack]` type that works today continues to work unchanged. The new `WriteTo` / `ReadFrom` methods exist on the generated partial in addition to the existing `Pack` / `Unpack` / Packed-struct / constructor emissions. The `IFastBufferPackable` interface is new — no existing consumer breaks.

If packaging option #2 (satellite package), consumers who don't use NGO see zero change. If packaging option #1 (optional dependency in core), the generator emits the FastBuffer methods only when NGO is present.

## Open questions for the maintainer

- **Packaging.** Satellite package vs optional dependency in core. We don't have a strong preference; the satellite is probably cleaner separation but more files for you to maintain.

- **Where the interface lives.** `Scaffold.AutoPacker.IFastBufferPackable` (in core) vs `Scaffold.AutoPacker.Netcode.IFastBufferPackable` (in satellite). Follows from the packaging decision.

- **Whether to extend `IUnpackable` instead of adding `IFastBufferPackable`.** Could add a `void Unpack(ref FastBufferReader, IPackingHandler)` overload to `IUnpackable`. Pro: single interface for "things that can be hydrated." Con: existing `IUnpackable` consumers (hand-written, not generator-emitted) suddenly have to implement a new method. New interface is the safer additive choice.

- **Handler-aware variant.** We're showing `WriteTo` / `ReadFrom` with `IPackingHandler handler = null`. Mirrors the existing `Pack(handler)` and `Unpack(packed, handler)` signatures. If you want symmetry with `Pack`/`Unpack`'s exact shape, that's the call to make.

- **Pre-write size accounting.** `FastBufferWriter` requires the caller to size its buffer up front (via `new FastBufferWriter(size, ...)`) or call `TryBeginWrite(size)` before writing. The generated `WriteTo` could optionally include a `static int GetSize()` or `static int GetSize(in T)` that returns `sizeof(Packed)` so callers can size their buffers correctly. Not strictly required (the consumer can use `Marshal.SizeOf<T.Packed>()` or just over-allocate) but would be a nice addition.

## Tradeoff and honest framing

This is a smaller ask than the open-generics one. The emission is two extra methods per `[AutoPack]` type; the bodies are mechanical compositions of code that's already emitted. The hard parts are:

1. Taking the NGO dependency (or designing the satellite-package layout).
2. Deciding whether to extend `IUnpackable` or add `IFastBufferPackable`.

Both are decisions, not implementation work.

If you ship this, we drop our reflection-based fallback entirely and our framework becomes fully reflection-free end-to-end. That's the goal we've been building toward across all the previous AutoPacker rounds.

## TL;DR for the maintainer

- **The ask:** generator emits `WriteTo(ref FastBufferWriter, IPackingHandler?)` and `ReadFrom(ref FastBufferReader, IPackingHandler?)` on every `[AutoPack]` type, satisfying a new `IFastBufferPackable` interface. Bodies are 2 lines each, composing existing emissions with `FastBufferWriter.WriteValueSafe<T>` / `FastBufferReader.ReadValueSafe<T>`.
- **The use case:** zero-reflection dispatch into NGO custom messages. Our framework's `Channel<T>` becomes `where T : WireEvent, IFastBufferPackable, new()` — clean constraint, no per-type ceremony.
- **The hardest part:** packaging (where the NGO dependency lives). Satellite package or optional dependency both work for us.

Composes with everything shipped in the previous three rounds (IUnpackable, hierarchy support). Doesn't conflict with the open-generics proposal — both are independent additive changes.
