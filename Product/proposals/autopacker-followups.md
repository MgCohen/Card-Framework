# AutoPacker — Follow-up Proposals (Round 2)

> **Status: both proposals shipped and verified (2026-05-19).** Scaffold landed Proposal A and the full Proposal B (base trigger + override emission + auto-emitted `: IPackable, IUnpackable` on the base partial) in one update. Verified end-to-end by Framework.Wire's 7-test suite under Config 1 — `[AutoPack]` only on the abstract base, derived partials with just `[Packed]` fields and no attribute, all round-trips passing. Doc preserved as historical record of the proposal + experiments.
>
> ---
>
> Two additions building on the `IUnpackable` change shipped in round 1. Both are oriented toward eliminating remaining boilerplate and asymmetry as AutoPacker takes on more weight in our framework. Neither is breaking — purely additive.
>
> 1. **`IUnpackable.Unpack(IPackedStruct, IPackingHandler)` overload** — closes the handler-side asymmetry that round 1 didn't address.
> 2. **AutoPacker hierarchy support** — let `[AutoPack]` flow through a base class so derived types just declare `[Packed]` fields, matching the `CommunityToolkit.Mvvm` pattern. The downstream user writes zero Pack/Unpack code at any layer.

---

## Proposal A — Handler-aware `Unpack` overload

### Context

Round 1 added `IUnpackable` with `void Unpack(IPackedStruct packed)`. That fixed the *construction* asymmetry — generic consumer code can now do `new T(); ev.Unpack(packed)` with zero reflection. Confirmed working in our framework.

But the *handler* asymmetry is still open:

| Side | API | Handler support |
|---|---|---|
| Pack | `IPackable.Pack(IPackingHandler? handler = null)` | ✅ optional handler parameter |
| Unpack | `IUnpackable.Unpack(IPackedStruct packed)` | ❌ no handler parameter |

When a `[AutoPack]` type has a `[Packed(typeof(int))]` field whose source type needs handler-driven conversion (e.g. `string`, or any reference type with a custom `IPackingHandler.Resolve<TSource, TTarget>` mapping), the unpack path through `IUnpackable` doesn't have a way to thread the handler in.

The generated constructor `T(Packed packed, IPackingHandler? handler = null)` does accept a handler. But going through the constructor means consumers fall back to reflection (`Activator.CreateInstance` / `ConstructorInfo.Invoke`) to construct generically — undoing the zero-reflection win that motivated round 1 in the first place.

### Proposed change

Add a handler-aware overload to `IUnpackable`:

```csharp
public interface IUnpackable
{
    void Unpack(IPackedStruct packed);
    void Unpack(IPackedStruct packed, IPackingHandler handler);  // NEW
}
```

Generator emits the new overload on every `[AutoPack]` partial alongside the existing one. Body delegates to the same internal hydration logic, just threading the handler through to any mapped-field `Resolve` calls — exactly the way `Pack(IPackingHandler)` already does on the encode side.

### Consumer-side impact

The handler-aware path through a generic `Channel<T>` becomes:

```csharp
internal override void Deliver(IPackedStruct packed)
{
    var ev = new T();
    ev.Unpack(packed, _network.Handler);   // handler threaded, no reflection
    _network.Receive(ev);
}
```

Symmetric with how `Network.Send` already passes `Network.Handler` into `Pack`. Whole framework round-trip stays reflection-free even for handler-mapped payloads.

### Open question for the maintainer

Whether to make this **two overloads** (`Unpack(packed)` and `Unpack(packed, handler)`) or **one method with an optional parameter** (`Unpack(packed, handler = null)`). Both achieve the same surface. We don't have a strong preference — `Pack` already uses the optional-parameter style, so mirroring that would be consistent.

---

## Proposal B — AutoPacker hierarchy support

### Context

Today, every `[AutoPack]` type must carry the attribute on its own declaration, and AutoPacker emits `Pack` / `Unpack` only on that declaration. In a framework that builds a *hierarchy* of similar types (events, commands, state records — any base + many concrete derivatives), this leaves no way to express the contract through inheritance. Downstream code is forced into one of two bad shapes:

**Shape 1 — Per-type `[AutoPack]`.** Every concrete event repeats the attribute. The base class doesn't participate. Constraints in generic code need every interface listed:

```csharp
where T : WireEvent, IPackable, IUnpackable, new()
```

And the `((IPackable)ev).Pack()` cast lives at every dispatch site because the base type doesn't expose the interface.

**Shape 2 — Declare interfaces on the base.** But the base is then forced to declare `Pack` / `Unpack` as abstract (C# requires it; abstract classes don't silently defer interface implementation). Derived `[AutoPack]` types emit `public Pack` / `public Unpack` *without* `override` — the methods *hide* the abstract base members instead of satisfying them. Compile error CS0534 on every derived type:

```
Assets/Framework/Wire/EntityCreatedEvent.cs(6,26): error CS0534:
  'EntityCreatedEvent' does not implement inherited abstract member
  'WireEvent.Pack(IPackingHandler)'
```

Both shapes are dead ends.

`CommunityToolkit.Mvvm` solved the analogous problem by making the generator hierarchy-aware: `[ObservableObject]` on a base contributes the `INotifyPropertyChanged` plumbing, derived classes with `[ObservableProperty]` fields get properties + notifications emitted on top. Inheritance composes cleanly because the generator owns the whole picture.

### Proposed change

Make AutoPacker hierarchy-aware. Three coordinated behaviors, all in the generator:

1. **`[AutoPack]` on an abstract base class emits abstract `Pack` / `Unpack`** when the class has no `[Packed]` fields. The base class becomes the interface-implementation host — the downstream user *never writes Pack or Unpack at any layer*. (If the base has its own `[Packed]` fields, the generator could emit virtual `Pack` / `Unpack` instead so derived classes can chain via `base.Pack()` — but for the common "marker base" case, abstract is the right answer.)

2. **`[AutoPack]` on a class deriving from another `[AutoPack]` class emits `public override Pack` / `public override Unpack`.** The generator detects the inherited abstract or virtual members and emits the correct modifier. No CS0534.

3. **`[AutoPack]` on a base class triggers generation on derived partials with `[Packed]` fields**, even if those partials don't carry `[AutoPack]` themselves. Matches `CommunityToolkit.Mvvm`'s opt-in-by-hierarchy pattern — the base annotates the whole tree as wire-packable.

The three behaviors compose into a single coherent feature. Each is mechanical — they're all things the generator already knows enough to do; it just doesn't walk the inheritance edge today.

### Consumer-side impact

The shape that's blocked today becomes the natural shape:

```csharp
[AutoPack]
public abstract class WireEvent : IPackable, IUnpackable { }
// Generator emits abstract Pack / Unpack on WireEvent. We write nothing.

public partial class EntityCreatedEvent : WireEvent
{
    [Packed] public long EntityId;
}
// Generator emits `public override Pack` / `public override Unpack` on EntityCreatedEvent,
// triggered by inheritance from the [AutoPack] base + the [Packed] field.
```

The downstream user writes **zero** Pack / Unpack methods, zero attribute on derived types, and zero interface declarations on derived types. The type system flows the interface constraint through inheritance:

```csharp
public Channel<T> Register<T>() where T : WireEvent, new() { ... }  // IPackable, IUnpackable implied
public void Send(WireEvent ev) => _inbound.Enqueue(... ev.Pack() ...);  // no cast
```

### Backwards compatibility

Purely additive. Every shape that works today continues to work:

- `[AutoPack]` on a standalone class with no base hierarchy → emits public `Pack` / `Unpack` (current behavior).
- `[AutoPack]` on a derived class whose base does *not* have `[AutoPack]` → emits public `Pack` / `Unpack` (current behavior; nothing to override).
- `[AutoPack]` on a derived class whose base *does* have `[AutoPack]` → emits **`override`** `Pack` / `Unpack` (new). This is the *correct* shape in this case — the previous non-override emission was a latent bug.

If a derived partial carries `[AutoPack]` itself in addition to inheriting from an `[AutoPack]` base, that's a redundant but harmless declaration — same emission either way.

### Open questions for the maintainer

- **Abstract vs virtual on the base.** When the base has no `[Packed]` fields, abstract is the right choice — forces derived to implement, no useless empty `Pack` body. When the base has its own `[Packed]` fields, virtual makes sense so derived can call `base.Pack()` for the inherited fields. Reasonable default: abstract when no fields, virtual when fields.
- **Detection scope.** Whether to detect "this base has `[AutoPack]`" by syntax walking the declaration's base list, or via the semantic model. Semantic model catches transitive inheritance correctly; syntax walking is faster but only one level. For shallow hierarchies (base → concrete), syntax walking is fine; for deeper hierarchies it'd miss.
- **Mid-hierarchy `[Packed]` fields.** If a `[AutoPack]` base has its own `[Packed]` fields and a derived also has `[Packed]` fields, does the derived's `Packed` struct extend the base's, or replace it? Defer until someone actually has the use case.

---

## Empirical observations (2026-05-18 — superseded by 2026-05-19 update)

> **All three configs now pass** as of the 2026-05-19 Scaffold update. Config 1 is the chosen end state. The configs below describe what we tested mid-evaluation and what was missing at that time — kept as a record of the iteration, not as current status.

### Config 1 — `[AutoPack]` on WireEvent only; derived classes without `[AutoPack]`

```csharp
[AutoPack]
public abstract partial class WireEvent : IPackable, IUnpackable { }

public partial class EntityCreatedEvent : WireEvent
{
    [Packed] public long EntityId;
}
```

**Compile result:** ✅ clean. No CS0535 / CS0533 / CS0534.

**Runtime result:** ❌ all round-trip tests fail. Sent `EntityId = 42`, received `EntityId = 0`. The derived class is *not* getting its own Pack/Unpack emission triggered by inheritance from a `[AutoPack]` base — it inherits the empty methods AutoPacker emitted on `WireEvent` (which has no `[Packed]` fields).

→ **The base-class trigger part of Proposal B has not shipped.** Derived partials with `[Packed]` fields don't auto-generate based on `[AutoPack]` on an ancestor.

### Config 2 — `[AutoPack]` on both WireEvent and derived classes

```csharp
[AutoPack]
public abstract partial class WireEvent : IPackable, IUnpackable { }

[AutoPack]
public partial class EntityCreatedEvent : WireEvent
{
    [Packed] public long EntityId;
}
```

**Compile result:** ✅ clean. (Confirms `override` emission works at the language level — the derived `Pack`/`Unpack` don't conflict with the base's emitted methods.)

**Runtime result:** ❌ all round-trip tests still fail with `EntityId = 0` after round-trip.

→ **The override-aware emission has a runtime bug.** Either the derived's `override Pack` body doesn't include the `[Packed] EntityId`, or the derived's emitted `Packed` struct is empty / wrong, or `Pack` is dispatching to the base's empty implementation instead of the derived override. The code compiles but the body is incorrect.

We can't easily inspect the generator's output from outside to pinpoint which, but the behavior is: a class deriving from a `[AutoPack]` base, with `[AutoPack]` itself and a `[Packed]` field, packs to an empty struct.

### Config 3 — `[AutoPack]` only on derived; WireEvent plain abstract class

```csharp
public abstract class WireEvent { }

[AutoPack]
public partial class EntityCreatedEvent : WireEvent
{
    [Packed] public long EntityId;
}
```

**Compile result:** ✅ clean.
**Runtime result:** ✅ all tests pass. This is the working state we ship.

The cost is that `WireEvent` doesn't carry the `IPackable, IUnpackable` interfaces, so every generic constraint has to spell them out (`where T : WireEvent, IPackable, IUnpackable, new()`) and `Network.Send` carries the `((IPackable)ev).Pack()` cast.

### What this means for the maintainer

- Proposal A is fully done.
- Proposal B's *abstract / virtual emission on empty `[AutoPack]` base* worked enough to compile but the emission was effectively a no-op (empty `Packed`, empty methods). Config 1 result.
- Proposal B's *override emission on derived `[AutoPack]` with an `[AutoPack]` base* works at the compile level but produces an incorrect runtime body. Config 2 result. This is the most surprising finding — the override compiles cleanly, so something is being emitted; whatever it is, it doesn't read/write the derived's `[Packed]` field.
- Proposal B's *base-class trigger* (generate on derived partials without their own `[AutoPack]`) is not in place. Config 1 result.

We'd appreciate if the maintainer could investigate Config 2 specifically — the override emission seems closest to right but isn't quite. Even just confirming "this generates an empty body when the base also has `[AutoPack]`" or "this generates `base.Pack()` instead of the actual body" would help us decide whether to wait or work around.

---

## TL;DR for the maintainer

- **Proposal A:** add `Unpack(IPackedStruct, IPackingHandler)` to `IUnpackable` + emit on the generator. Closes the handler-side asymmetry. — ✅ **Shipped and verified.**
- **Proposal B:** make AutoPacker hierarchy-aware — emit abstract/virtual `Pack`/`Unpack` on a base `[AutoPack]` class, emit `override` on derived `[AutoPack]` (or inherited) classes with the correct body that reads the derived's own `[Packed]` fields, and let `[AutoPack]` on a base trigger generation on derived partials with `[Packed]` fields. — ✅ **Shipped and verified.** End state in our framework: `[AutoPack]` only on the abstract `WireEvent` base; concrete events are `partial class X : WireEvent { [Packed] ... }` with no attribute and no method declarations.

Both landed cleanly. Thanks for the quick turnaround.
