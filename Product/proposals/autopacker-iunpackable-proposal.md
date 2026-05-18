# AutoPacker — Proposal: `IUnpackable<T>` for symmetric, reflection-free unpack

> One-line summary: AutoPacker today gives downstream code a polymorphic *pack* path (`IPackable.Pack`) but only a constructor-based *unpack* path. Adding an `IUnpackable<TSelf>` interface with a static abstract `FromPacked` method — and emitting it from the generator alongside the existing `IPackable` implementation — closes the asymmetry and eliminates reflection in every consumer that wants to round-trip generic `T : [AutoPack]` values through a wire / replay / dispatch layer.

## Context

We're building a card-game framework on top of AutoPacker. AutoPacker is the codec layer for our wire format: every event that crosses the network is an `[AutoPack]` partial class that lives in a typed `EventStream` on the sender, gets packed to its unmanaged `Packed` struct, transits a `Network` (in-memory queue today, Unity NGO custom messages later), and gets unpacked back into a typed event on the receiver.

The core consumer shape on the receiver side is a generic channel registered per event type:

```csharp
public sealed class Channel<T> where T : WireEvent, IPackable
{
    public event Action<T> OnReceived;
    internal void Deliver(IPackedStruct packed) { /* needs to construct T from packed */ }
}

// Usage:
network.Register<EntityCreatedEvent>();
network.Register<EntityDestroyedEvent>();
// ...
```

This pattern (one generic channel class, registered per event type, no per-type subclass or per-type lambda) is the cleanest API we can give framework users. It only works if `Channel<T>.Deliver` can construct a `T` from an `IPackedStruct` without runtime reflection.

## The asymmetry

AutoPacker emits two halves of the round-trip:

| Direction | What AutoPacker emits | Polymorphic? |
|---|---|---|
| Pack | `IPackable.Pack(handler)` — interface instance method on the partial | ✅ yes |
| Unpack | `T(Packed packed, IPackingHandler handler = null)` — constructor on the partial | ❌ no |

The unpack side is a constructor. There's no interface contract, no static factory, no instance hydrate method — nothing a generic consumer can dispatch through. That forces every consumer who wants typed unpack in a `Channel<T>`-style API into one of:

1. **Reflection** — `ConstructorInfo.Invoke` per call. Allocates, slow, requires `NonPublic` flag depending on emission. IL2CPP-fragile if AOT strips the constructor.
2. **`Activator.CreateInstance(typeof(T), packed, null)`** — same problem, different syntax. Also fails when the generated ctor's optional `IPackingHandler` parameter forces arg-count gymnastics.
3. **Per-event lambda passed at registration** — `Register<T>(p => new T((T.Packed)p))`. Surfaces internal types in user code and undoes the "generator owns it" benefit.
4. **Per-event subclass with `Unpack` override** — boilerplate every consumer reinvents.

All four are downstream of a missing piece of the AutoPacker contract. The pack side has the right shape; the unpack side doesn't.

## Proposed change

### 1. Add a new interface to the contracts assembly

```csharp
namespace Scaffold.AutoPacker
{
    public interface IUnpackable<TSelf> where TSelf : IUnpackable<TSelf>
    {
        static abstract TSelf FromPacked(IPackedStruct packed, IPackingHandler handler = null);
    }
}
```

Mirrors `IPackable` semantically. The `TSelf` curiously-recurring type parameter is what lets a generic consumer write `T.FromPacked(...)` against `where T : IUnpackable<T>` and have the C# compiler dispatch through the static abstract member with no reflection.

### 2. Update `AutoPackerGenerator` to emit two additions per `[AutoPack]` type

For each `[AutoPack]` partial class `TFoo`, in addition to everything the generator emits today:

**(a)** Add `IUnpackable<TFoo>` to the partial's interface list alongside the existing `IPackable`:

```csharp
partial class EntityCreatedEvent : IPackable, IUnpackable<EntityCreatedEvent>
```

**(b)** Emit a static `FromPacked` method whose body delegates to the constructor the generator already emits:

```csharp
public static EntityCreatedEvent FromPacked(IPackedStruct packed, IPackingHandler handler = null)
    => new EntityCreatedEvent((Packed)packed, handler);
```

Both emissions are mechanical — no new analysis, no decisions per type. The generator already knows the type name and the nested `Packed` struct; this is two more lines of code-gen per `[AutoPack]` target.

## What downstream code looks like after

### Event author writes the same thing they write today

The `[AutoPack]` partial class is unchanged. The generator adds the `IUnpackable<TSelf>` implementation automatically; the author never types `FromPacked` or `IUnpackable` by hand.

```csharp
[AutoPack]
public partial class EntityCreatedEvent : WireEvent
{
    [Packed] public long EntityId;
}
```

### A generic channel becomes reflection-free

The entire `Channel<T>` class — no `System.Reflection` import, no `ConstructorInfo`, no `Activator`, no `GetMethod`, no `CreateDelegate`, no static initializer, no per-type helper:

```csharp
using System;
using Scaffold.AutoPacker;

namespace Framework.Wire
{
    public sealed class Channel<T> where T : WireEvent, IPackable, IUnpackable<T>
    {
        public event Action<T> OnReceived;
        internal event Action<WireEvent> OnDelivered;

        internal Channel() { }

        internal void Deliver(IPackedStruct packed)
        {
            var ev = T.FromPacked(packed);
            OnReceived?.Invoke(ev);
            OnDelivered?.Invoke(ev);
        }
    }
}
```

`T.FromPacked(packed)` is a language-native call through the static abstract interface member. The JIT (or IL2CPP at AOT time) resolves it per closed generic instantiation; there is no runtime metadata walk.

### A registry/dispatch layer constrains symmetrically

```csharp
public Channel<T> Register<T>() where T : WireEvent, IPackable, IUnpackable<T>
{
    // ... allocate channel, register dispatch, etc.
}
```

The constraint pair `IPackable, IUnpackable<T>` makes the round-trip requirement explicit in the API surface. If a consumer registers a type missing `[AutoPack]`, the compiler refuses at the call site with a clear constraint-not-satisfied error, not a runtime `MissingMethodException`.

### Registration site reads exactly as it does today

```csharp
network.Register<EntityCreatedEvent>();
```

No new ceremony for the framework user.

## Properties this gives us

- **Zero reflection at any phase** — init or per-call. No `System.Reflection` usage anywhere in the consumer.
- **Per-Deliver cost**: one static-abstract dispatch (≈ virtual call cost) + the `(Packed)packed` unbox inside the generated `FromPacked`. No allocations on the hot path.
- **IL2CPP friendly**: every closed generic `Channel<T>` instantiation is monomorphized at AOT time; dispatch resolves at link-time, no method-stripping concerns, no `[Preserve]` attributes required.
- **Compile-time check at the registration site**: missing `[AutoPack]` becomes a compile error, not a runtime failure deep in delivery.
- **Discoverable**: `FromPacked` is a normal public static method, visible in IDE autocomplete, documentable via XML doc comments, no "secret reflection ritual."

## Compatibility considerations

- **C# language version**: static abstract interface members require `LangVersion 11` or higher. AutoPacker's contracts DLL needs to compile against a TFM that supports it (`net7.0`+, or a Unity-bundled compiler at that level — Unity 6 qualifies).
- **Existing AutoPacker users**: the change is purely additive. Existing consumers calling the constructor directly (`new T((T.Packed)packed)`) continue to work unchanged — `FromPacked` is a new alternative path, not a replacement.
- **Older-target consumers**: if the maintainer wants to keep older C# target frameworks compiling, two options:
  - Ship `IUnpackable<T>` only in a separate "modern" contracts assembly that older targets don't reference. Older consumers continue to see the same surface they see today.
  - Ship the contracts assembly at the higher TFM and document the bump. Existing consumers on old TFMs would need to upgrade.
  - That's a packaging call for the maintainer; not something the change itself forces.

## Minimal generator diff (sketch)

For reference — the change in the generator is roughly this shape (pseudo-code):

```csharp
// In whatever method emits the partial class body for an [AutoPack] type:
//
// Existing emission appends ": IPackable" and the Pack method + constructor.
// Add to that:

emitter.AppendInterface($"IUnpackable<{typeName}>");

emitter.AppendMethod($@"
    public static {typeName} FromPacked(IPackedStruct packed, IPackingHandler handler = null)
        => new {typeName}((Packed)packed, handler);
");
```

If there's a more idiomatic codegen path in your generator architecture, the same two emissions (interface + static method) can be slotted into it. The body of `FromPacked` is always identical except for the type name.

## TL;DR for the maintainer

- New interface, one method: `IUnpackable<TSelf>.FromPacked(IPackedStruct, IPackingHandler)`.
- Generator emits two extra lines per `[AutoPack]` type (interface declaration + static method body that wraps the existing constructor).
- Closes the round-trip symmetry with `IPackable.Pack`.
- Lets generic consumer code be reflection-free and AOT-clean.
- Purely additive; no behavior change for any existing AutoPacker user.

Happy to discuss alternative shapes (e.g., an instance `Hydrate` method instead of a static factory, or a runtime `AutoPackerRegistry.Unpackers` dictionary as a complement for non-generic `Type`-keyed dispatch). Static abstract is the path that gives the cleanest *generic* consumer surface; the others address different dispatch scenarios.
