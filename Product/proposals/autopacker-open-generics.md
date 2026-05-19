# AutoPacker — Proposal: Open-Generic `[AutoPack]` Types

> **Status: draft (2026-05-19).** New ask building on the IUnpackable + hierarchy-support work that shipped in round 1 and round 2. Asks AutoPacker to handle `[AutoPack] partial class Foo<T>` — generic event types with the type parameter participating in `[Packed]` fields.

## Context

Our framework uses AutoPacker to emit pack/unpack code for "wire events" — typed C# objects that carry mutations across a server/client boundary. AutoPacker's existing per-type emission has worked great for fixed-shape events (`EntityCreatedEvent { long EntityId }`, `BlockStartEvent { byte Kind }`, etc.).

We're now hitting a category where the existing approach pushes back: **events that carry a typed value of varying type**. Concretely, tag mutations:

```csharp
// What we want, conceptually:
//   "Set entity 1's Health (slot 7) to 30"
//   "Set entity 1's Position (slot 8) to Vec3(1, 2, 3)"
//   "Set entity 1's Owner (slot 9) to EntityRef(42)"
```

The shape is the same — `entity + slot + value` — but the *value's type* changes per tag.

We've considered two existing options and found both unsatisfying:

### Option A: Specialized events per value type *(works today, multiplies files)*

```csharp
[AutoPack] public partial class IntTagSetEvent     : WireEvent { [Packed] int E; [Packed] int S; [Packed] int    V; }
[AutoPack] public partial class BoolTagSetEvent    : WireEvent { [Packed] int E; [Packed] int S; [Packed] bool   V; }
[AutoPack] public partial class Vec3TagSetEvent    : WireEvent { [Packed] int E; [Packed] int S; [Packed] Vec3   V; }
[AutoPack] public partial class EntityRefTagSetEvent : WireEvent { [Packed] int E; [Packed] int S; [Packed(typeof(int))] EntityRef V; }
// ... one trio (Set/Change/Removed) per value type
```

This works because AutoPacker handles each as a separate fixed-shape type. The downsides: file count grows linearly with the value-type set, the framework user has to know which specialized type to use at each call site, and channel registration must list every variant.

### Option B: Non-generic event with a discriminated payload *(what we have in `Framework.Tags` today)*

```csharp
[AutoPack] public partial class TagSetEvent : WireEvent
{
    [Packed] public int      EntityId;
    [Packed] public int      Slot;
    [Packed] public TagValue Value;       // readonly struct { long Raw; }
}

// Type recovered at decode time via slot → TagKey.ValueType lookup,
// via a hand-written TagValueCodec that packs T → long and back.
```

This works for primitives that fit in 8 bytes (bool, int, long, float, double, enum-int). It doesn't fit composite values (`Vec3` is 12 bytes, custom structs, etc.) without an escape hatch — typically a `bytes`-style variant or a parallel mechanism.

### Option C: Open-generic event type *(what we'd actually like)*

```csharp
[AutoPack]
public partial class TagSetEvent<T> : WireEvent
{
    [Packed] public int EntityId;
    [Packed] public int Slot;
    [Packed] public T   Value;
}
```

Same shape, the type parameter carries the value type. The framework's per-type channel model (`Channel<TagSetEvent<int>>`, `Channel<TagSetEvent<Vec3>>`) then naturally specializes per `T`. No file explosion, no 8-byte ceiling, no hand-written codec — the value is a real typed field.

We can't ship Option C today because AutoPacker doesn't process open-generic `[AutoPack]` types. The generator emits one Packed struct per declared class, but for `TagSetEvent<T>` it would need to emit one Packed struct *per closed instantiation* (`TagSetEvent<int>.Packed`, `TagSetEvent<Vec3>.Packed`, etc.), and the field type for `Value` in each Packed struct has to be the concrete `T`.

## Proposed change

Extend AutoPacker to handle open-generic `[AutoPack]` types by **discovering closed instantiations across the compilation and emitting per-instantiation code for each.**

### Generator behavior

For an open-generic type `[AutoPack] partial class Foo<T1, T2, ...>`:

1. **Scan the compilation** for closed instantiations of `Foo<...>`. Sources to consider:
   - Direct construction: `new TagSetEvent<int>()`
   - Type references in generic constraints: `Channel<TagSetEvent<Vec3>>`
   - Method type parameters: `void Register<TagSetEvent<long>>()`
   - `typeof(TagSetEvent<bool>)`
   - Inheritance: `class Foo : TagSetEvent<EntityRef>` (closed-by-inheritance)

2. **For each discovered closed instantiation**, emit:
   - A nested `Packed` struct with field types substituted to the concrete `T` (e.g., `TagSetEvent<Vec3>.Packed { int E; int S; Vec3 V; }`)
   - `Pack`, `Unpack`, ctor — same shape as today's emission, but bound to the concrete `T`.

3. **Inheritance / hierarchy compose** as in round 2: `[AutoPack]` on a base class triggers emission on derived partials; overrides emit `override`. Same rules apply when the base or derived is generic.

### Explicit opt-in fallback

If full compilation-scan discovery is too costly or fragile, an explicit attribute would also be usable downstream:

```csharp
[AutoPack]
[AutoPackInstantiate(typeof(int), typeof(bool), typeof(long), typeof(Vec3), typeof(EntityRef))]
public partial class TagSetEvent<T> : WireEvent
{
    [Packed] public int EntityId;
    [Packed] public int Slot;
    [Packed] public T   Value;
}
```

The attribute lists the closed instantiations to emit code for, sidestepping the discovery problem entirely. Less magical than auto-discovery; easier to implement; downstream user knows exactly what they're getting. **We'd be happy with this fallback** — it solves the use case without forcing you to take on the harder discovery problem.

## Consumer-side impact

For our framework, this would let us write:

```csharp
[AutoPack]
public partial class TagSetEvent<T> : WireEvent
{
    [Packed] public int EntityId;
    [Packed] public int Slot;
    [Packed] public T   Value;
}

[AutoPack]
public partial class TagRemovedEvent : WireEvent
{
    [Packed] public int EntityId;
    [Packed] public int Slot;
}
```

And register per-value-type channels:

```csharp
network.Register<TagSetEvent<int>>();
network.Register<TagSetEvent<bool>>();
network.Register<TagSetEvent<Vec3>>();
network.Register<TagSetEvent<EntityRef>>();
network.Register<TagRemovedEvent>();
```

The `TagValueCodec` + `TagValue` machinery in `Framework.Tags` becomes unnecessary — the value lives as a real typed field, no manual long-packing, no slot→type lookup at decode. Tag-bag round-trips become:

```csharp
serverBag.Set(SampleTags.Position, new Vec3(1, 2, 3));  // dispatches to TagSetEvent<Vec3> channel
// ... wire ...
clientBag.Get(SampleTags.Position)                       // returns Vec3 directly, type-safe
```

Composite values (`Vec3`, custom structs) just work — they're typed fields, AutoPacker handles them like any other `[Packed]` field already does for `EntityMovedEvent { Vec3 Position }`.

## Backwards compatibility

Purely additive. Non-generic `[AutoPack]` types continue to emit exactly as today. The new behavior only activates when AutoPacker sees an `[AutoPack]` type with type parameters.

The explicit `[AutoPackInstantiate(...)]` fallback path is also additive — types without it would either get auto-discovery or compile-time diagnostic ("open-generic [AutoPack] type needs [AutoPackInstantiate] to specify closed instantiations").

## Open questions for the maintainer

- **Discovery vs explicit opt-in.** Auto-discovery is more magical but matches AutoPacker's existing "scan and emit" model. Explicit `[AutoPackInstantiate]` is less magical, easier to implement, and downstream users always know exactly which instantiations exist. Either works for us; explicit opt-in is the smaller ask.

- **Cross-assembly discovery.** If `TagSetEvent<T>` is in assembly A and `TagSetEvent<Vec3>` is used in assembly B, does A's generator emit `TagSetEvent<Vec3>.Packed` or does B's? Cleanest answer is probably "wherever the closed instantiation is referenced" — so emission lives with the user-of-the-instantiation, not the definition. Same direction as `Channel<T>` instantiation in .NET — JIT/AOT handles it per closed type.

- **Reflection-created instantiations.** Code that does `Activator.CreateInstance(typeof(TagSetEvent<>).MakeGenericType(t))` at runtime can't be discovered statically. Acceptable to fail at runtime for those; the explicit `[AutoPackInstantiate]` attribute is the escape hatch.

- **Inheritance interaction.** If we have `public partial class HealthTagSetEvent : TagSetEvent<int>`, does the generator emit Pack/Unpack on `HealthTagSetEvent` (closed-by-inheritance), or does it require `TagSetEvent<int>` to be declared explicitly first? Probably the former — the inheriting class is itself a closed instantiation.

- **Constraint propagation.** If the open generic has constraints (`where T : unmanaged` or `where T : struct`), the closed instantiations are bounded. Worth declaring in the API to avoid surprise emissions.

## Tradeoff and honest framing

We acknowledge this is the biggest ask of the three rounds. The previous two (handler-aware Unpack, hierarchy support) were mostly mechanical. Open generics is a real generator-architecture decision.

If the discovery side is too costly, we'd take the `[AutoPackInstantiate]` explicit-opt-in path alone — it's still a major usability win even without auto-discovery. Downstream consumers (us) accept the cost of listing instantiations in one place; that's better than the file-per-type explosion or the 8-byte payload ceiling we hit today.

## TL;DR for the maintainer

- **The ask:** `[AutoPack]` on open-generic types (`Foo<T>`). Generator emits per closed instantiation discovered in the compilation (or per types explicitly listed via a new `[AutoPackInstantiate(...)]` attribute).
- **The use case:** typed tag values without per-type file explosion or per-value-type custom codec. Works for primitives *and* composite structs like `Vec3`.
- **Acceptable middle ground:** if full auto-discovery is too costly, the explicit `[AutoPackInstantiate]` attribute alone is sufficient. Smaller implementation cost, equivalent downstream benefit.

Purely additive. Composes with the IUnpackable + hierarchy support already shipped. Happy to discuss alternative shapes or split the work.
