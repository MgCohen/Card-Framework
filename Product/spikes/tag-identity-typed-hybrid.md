---
type: spike
status: active
forward-link: TBD
tags: [#spike, #tags, #identity, #vertical-slice, #keywords]
---

# Tag identity — typed hybrid

> **Status: active.** Working design replacing the current `TagKey<T>` static-instance system with a hybrid: static-instance tags for plain stats/flags, dual-role classes for keywords (Deathrattle, future Poison/OnCast). Same slot space; one analyzer-checked int per logical tag.

## Premise

Today the vertical slice declares every tag as `static readonly TagKey<T>` in `Assets/Sample/VerticalSlice/Sample/Tags.cs`. The pattern works cleanly for stat tags (Health, Attack) and kind markers (IsCard, IsMinion), but breaks down for **keywords** — Backlash carries a flag *and* a separate effect class; Deathrattle has no flag at all, only a payload class routed through `CardEffectStore`. The asymmetry is visible at every call site and makes "query the world by keyword" awkward.

We want:

1. Stat/flag tags to **stay simple** — no class explosion, no mandatory attributes.
2. Keywords to be a **single class** — that class is the tag-type handle, the query handle, and the event-payload constructor.
3. Be able to write `world.Entities.Where<Deathrattle>()`.
4. The **slot** (runtime int address) to be deterministic across same-build runs — author-assigned, analyzer-checked.
5. **`false` to be a meaningful value** — silenced ≠ absent; `Remove<T>()` is the only operation that clears a tag.

This spike captures the agreed shape so we can iterate without losing context. It sits **downstream of [entity-store-extraction.md](entity-store-extraction.md)**, which validated the indexed-query infrastructure (10.3–10.9× LINQ at Hearthstone scale, ~5% mutation overhead) and locked **tag-as-typed-key with value-as-identity** as the foundational model. This spike inherits those decisions and extends them with two additions: explicit-int slots at declaration, and a dual-role class pattern for keywords whose payload class already exists.

## Context — what exists today

[TagKey.cs](Assets/Sample/VerticalSlice/Engine/Tags/TagKey.cs):

```csharp
public abstract class TagKey {
    public string Name { get; }
    public int Slot { get; }
    public abstract Type ValueType { get; }
    protected TagKey(string name) { Name = name; Slot = TagRegistry.Register(this); }
}

public sealed class TagKey<T> : TagKey {
    public TagKey(string name) : base(name) { }
    public override Type ValueType => typeof(T);
}
```

Three roles bundled into one class:

1. **Slot** — registration-order int. Used as the entity's storage address (`_slots[key.Slot]` in [Entity.cs:32-54](Assets/Sample/VerticalSlice/Engine/Entities/Entity.cs:32)).
2. **Value-type binding** — `<T>` on the generic gives compile-time type safety on Get/Set.
3. **Wire label** — `Name` (string). Used by `TagSetEvent` / `TagChangeEvent` and `ToString()`.

Current usage shapes:

- **Stat/flag tags** — one static field per tag in `Tags.cs`. Clean.
- **Backlash** — `Tags.Backlash` flag (`TagKey<bool>`) + separate `class Backlash : Effect` retaliation. Two pieces.
- **Deathrattle** — no flag, payload-only `class Deathrattle { IEntity Source }` routed via `CardEffectStore`. One piece, but no queryability.

### Why this isn't enough

- **Asymmetric keyword surface.** Backlash and Deathrattle are conceptually peers ("keywords with gameplay effect") but have different shapes. New direct-trigger keywords don't fit either.
- **No type-keyed queries.** `world.Entities.Where<Deathrattle>()` is impossible because Deathrattle isn't a tag.
- **Wire-string dead weight.** `Name` exists for cross-build wire stability, but the slice is same-build single-process — we don't actually need it.

## Locked decisions

1. **Pattern A is the default; Pattern B is opportunistic.** One slot space, two registration paths.
   - **Pattern A** — `static readonly TagKey<T> X = new(slot)` in `Tags.cs`. The default for every tag. Preserves value-as-identity (the property entity-store-extraction relies on for effects-as-data and untyped wire).
   - **Pattern B** — `[TagSlot(N)] class X : ITagKey<T> { ... }`. *Only* when a keyword needs a class for payload reasons (Deathrattle, future Poison). The class doubles as the event payload AND the tag handle. **At registration the registry synthesizes a hidden `TagKey<bool>` singleton at the same slot, so value-as-identity still works for Pattern B keywords** — `Tag.Deathrattle` returns that singleton; `scope.With(Tag.Deathrattle)` flows through the existing indexed-query API unchanged.
   - Analyzer enforces slot uniqueness across both patterns.
2. **Slot is an int, author-assigned at declaration.** Roslyn analyzer flags duplicates at build time. No auto-allocation.
3. **`Name` (wire string) retained for now — removal deferred to a follow-up spike.** Entity-store-extraction's bench + extraction work assumes the current `(int slot, string Key, object Value)` event shape; bundling a wire-format change here risks expanding scope. The follow-up spike must also accommodate **designer-authored content** referencing tags by stable string label at the content-import boundary (string → slot resolution at load time, slot-only on the wire).
4. **Wire events keep `string Key`** — deferred to the follow-up. Today's shape is preserved unchanged through the typed-hybrid migration.
5. **`Set` always takes a value.** No `Tag<T>()` / `Untag<T>()` shortcuts. `false` is a real state ("silenced"). `Remove<T>()` is the only operation that clears the slot.
6. **`Where<T>()` = active.** Predicate is `Get<bool>() == true`. Silenced tags filtered out. Separate `WhereHas<T>()` for "tagged regardless of value."
7. **Dual-role class fields are transient event payload, not persistent state.** `class Poison : ITagKey<int> { Source, Amount }` stores the int at the slot; Source/Amount live only in the firing event. Persistent richer state requires either a record-valued tag or multiple flat tags.
8. **No source generation yet.** Registry is a runtime dict. Swap to generated lookup tables only if measurement says it matters.
9. **Only keyword keywords are dual-role for now.** Future poison/buff classes may join Pattern B. Plain data tags stay in Pattern A.
10. **Wire-architecture invariants — locked.** The migration is decoupled from the eventual network-serializer design provided three properties hold at both ends:
    - **Flat events.** Wire events are records of primitives + the closed value set (#2). No graph references, no nested object pointers. The six existing event types + `TagRemovedEvent` satisfy this; future additions must too.
    - **Closed value-type set.** Tag values come from a known, small set — `bool`, `int`, `string`, `EntityId` today. New value types are added deliberately; **no `object` escape hatch** for arbitrary user data.
    - **TagRegistry is authoritative at both ends.** Sender and receiver share the same registry (same build), so slot ↔ name ↔ value-type resolves identically. This is what makes "swap `string Key` for `int Slot` on the wire" a mechanical follow-up rather than a redesign.
    
    Pattern B introduces **zero new wire event types**. `entity.Set<Deathrattle>(true)` flows through `TagSetEvent` identically to Pattern A (the synthesized `TagKey<bool>` is wire-indistinguishable). Payload classes (`new Deathrattle(card)`) flow through the existing `BlockStartEvent.Payload` reflection-capture, same as every other effect.

## Final shape

### 1. The base — slot-explicit, Name retained (T3 deferred)

```csharp
public abstract class TagKey
{
    public int Slot { get; }
    public string Name { get; }                              // retained; removal deferred
    public abstract Type ValueType { get; }
    protected TagKey(int slot, string name)
    {
        Slot = slot;
        Name = name;
        TagRegistry.RegisterInstance(this);
    }
    public override string ToString() => Name;
}

public sealed class TagKey<T> : TagKey
{
    public TagKey(int slot, string name) : base(slot, name) { }
    public override Type ValueType => typeof(T);
}
```

Both slot and name explicit at the call site. The slot is the runtime identity; the name is the wire label (kept for now, drops in the follow-up Name→Slot spike).

### 2. Pattern A — static instances with explicit slot

```csharp
public static class Tags
{
    // Kind markers — slot range 1-19
    public static readonly TagKey<bool>   IsCard         = new( 1, "isCard");
    public static readonly TagKey<bool>   IsMinion       = new( 2, "isMinion");
    public static readonly TagKey<bool>   IsSpell        = new( 3, "isSpell");
    public static readonly TagKey<bool>   IsZone         = new( 4, "isZone");
    public static readonly TagKey<bool>   IsPlayer       = new( 5, "isPlayer");
    public static readonly TagKey<bool>   IsBattlefield  = new( 6, "isBattlefield");
    public static readonly TagKey<bool>   IsGraveyard    = new( 7, "isGraveyard");

    // Stats — slot range 20-39
    public static readonly TagKey<int>    Health         = new(20, "health");
    public static readonly TagKey<int>    MaxHealth      = new(21, "maxHealth");
    public static readonly TagKey<int>    Attack         = new(22, "attack");
    public static readonly TagKey<int>    Armor          = new(23, "armor");
    public static readonly TagKey<int>    Cost           = new(24, "cost");

    // Plain ability flags (non-keyword) — slot range 40-59
    public static readonly TagKey<bool>   Backlash       = new(40, "backlash");
    public static readonly TagKey<bool>   DivineShield   = new(41, "divineShield");

    // Names — slot range 60-79
    public static readonly TagKey<string> ZoneName       = new(60, "zoneName");
    public static readonly TagKey<string> PlayerName     = new(61, "playerName");
    public static readonly TagKey<string> CardName       = new(62, "cardName");

    // Entity-id refs — slot range 80-99
    public static readonly TagKey<int>    Zone           = new(80, "zone");
    public static readonly TagKey<int>    Owner          = new(81, "owner");
}
```

Slot ranges are an organizational convention, not enforced — but reserving `100+` for Pattern B keeps the two patterns from accidentally colliding.

### 3. Pattern B — dual-role class

```csharp
public interface ITagKey<T> { }   // marker only — T = slot value type

[AttributeUsage(AttributeTargets.Class)]
public sealed class TagSlotAttribute : Attribute
{
    public int Slot { get; }
    public string? DebugName { get; }
    public TagSlotAttribute(int slot, string? debugName = null)
    {
        Slot = slot;
        DebugName = debugName;
    }
}

[TagSlot(100)]
public sealed class Deathrattle : ITagKey<bool>
{
    public IEntity Source { get; }
    public Deathrattle(IEntity source) { Source = source; }
}

// Future shape — illustrative, not yet implemented:
[TagSlot(101)]
public sealed class Poison : ITagKey<int>
{
    public IEntity Source { get; }
    public int Amount { get; }
    public Poison(IEntity source, int amount) { Source = source; Amount = amount; }
}
```

The class plays three roles **simultaneously**:

- **Type handle for tag ops.** `entity.Set<Deathrattle>(true)`, `entity.Has<Deathrattle>()`.
- **Query handle.** `world.Entities.Where<Deathrattle>()`, equivalently `scope.With(Tag.Deathrattle)`.
- **Event payload.** `new Deathrattle(card)` passed to `scope.ScheduleDirect`.

Slot lives on the attribute; value type lives on the interface generic; payload data lives in the constructor.

**At registration, the registry synthesizes a hidden `TagKey<bool>` singleton at the declared slot**, exposed two equivalent ways:

```csharp
Tag<Deathrattle>.Key   // generic-static accessor — JIT-specialized per type, cached
Tag.Deathrattle        // named field on the non-generic Tag container
```

Both resolve to the same `TagKey<bool>` instance. The indexed-query store sees it as an ordinary Pattern A tag at slot 100; no parallel surface needed. *How* `Tag.Deathrattle` materializes (sourcegen, partial classes, manual one-liner per keyword) is a mechanism decision deferred to migration time.

**Important:** the class's instance fields (`Source`, `Amount`) are **transient event payload only**, not persistent entity state. `card.Set<Poison>(3)` stores the int `3` at slot 101. The Source/Amount on `new Poison(source, 3)` exist only on the firing event — they do NOT persist on the entity. If we want richer persistent state, two options:

- **Record-valued tag**: `ITagKey<PoisonState>` where `PoisonState` is a record with Source + Amount. One slot, structured value.
- **Multiple tags**: `Poison` (bool), `PoisonSource` (int), `PoisonAmount` (int). Three slots, flat data.

Both work. Decide per-keyword.

### 4. Registry — one slot space, two registration paths

```csharp
public static class TagRegistry
{
    public readonly record struct TagInfo(int Slot, Type DeclaringType, Type ValueType, string DebugName);

    private static readonly Dictionary<int, TagInfo>  _bySlot   = new();
    private static readonly Dictionary<Type, TagInfo> _byType   = new();
    private static readonly HashSet<int>              _claimed  = new();

    // Pattern A — called from TagKey base constructor
    internal static void RegisterInstance(TagKey key)
    {
        Claim(key.Slot, key.GetType());
        var info = new TagInfo(key.Slot, key.GetType(), key.ValueType, key.GetType().Name);
        _bySlot[key.Slot] = info;
        _byType[key.GetType()] = info;
    }

    // Pattern B — called from boot for every [TagSlot] class
    public static void RegisterType(Type tagClass)
    {
        var attr = tagClass.GetCustomAttribute<TagSlotAttribute>()
                   ?? throw new InvalidOperationException($"{tagClass.Name} missing [TagSlot]");
        var iface = Array.Find(tagClass.GetInterfaces(),
            i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ITagKey<>))
            ?? throw new InvalidOperationException($"{tagClass.Name} doesn't implement ITagKey<>");
        var valueType = iface.GetGenericArguments()[0];
        Claim(attr.Slot, tagClass);
        var info = new TagInfo(attr.Slot, tagClass, valueType, attr.DebugName ?? tagClass.Name);
        _bySlot[attr.Slot] = info;
        _byType[tagClass] = info;
    }

    public static TagInfo Of<T>()       => _byType[typeof(T)];
    public static TagInfo Of(int slot)  => _bySlot[slot];

    private static void Claim(int slot, Type owner)
    {
        if (!_claimed.Add(slot))
            throw new InvalidOperationException($"Slot {slot} double-claimed (offender: {owner.Name})");
    }
}
```

Boot wires Pattern B once:

```csharp
// In SliceBootstrap or similar:
foreach (var t in typeof(Deathrattle).Assembly.GetTypes())
    if (t.GetCustomAttribute<TagSlotAttribute>() != null)
        TagRegistry.RegisterType(t);
```

Reflection at startup is fine; can become sourcegen later without changing the public surface.

### 5. Entity API

```csharp
public interface IEntity
{
    // Pattern A — value-passing (unchanged in shape)
    T    Get<T>(TagKey<T> key);
    bool Has<T>(TagKey<T> key);
    void Set<T>(TagKey<T> key, T value);
    void Remove<T>(TagKey<T> key);                                   // NEW — three-state

    // Pattern B — type-passing
    T    Get<TTag, T>()              where TTag : ITagKey<T>;
    bool Has<TTag>();                                                // slot-present check
    void Set<TTag, T>(T value)       where TTag : ITagKey<T>;
    void Remove<TTag>();                                             // clears the slot

    // Convenience overloads for common Ts (sidestep `Set<Deathrattle, bool>` ugliness):
    // void Set<TTag>(bool v)   where TTag : ITagKey<bool>;
    // void Set<TTag>(int v)    where TTag : ITagKey<int>;
    // void Set<TTag>(string v) where TTag : ITagKey<string>;
}
```

World queries as extensions:

```csharp
public static class WorldQueryExtensions
{
    // "Active" — Get<bool>() == true. The gameplay default.
    public static IEnumerable<IEntity> Where<TTag>(this IEnumerable<IEntity> src)
        where TTag : ITagKey<bool>
        => src.Where(e => e.Get<TTag, bool>());

    // "Tagged regardless of value" — includes silenced.
    public static IEnumerable<IEntity> WhereHas<TTag>(this IEnumerable<IEntity> src)
        where TTag : ITagKey<bool>
        => src.Where(e => e.Has<TTag>());
}
```

### 6. Call sites

```csharp
// ── Pattern A — unchanged from today
entity.Set(Tags.Health, 30);
var hp = entity.Get(Tags.Health);
scope.Where(Tags.Backlash);                                  // engine API stays

// ── Pattern B — keyword as a type, dual-role.
// Two equivalent access surfaces; both resolve to the synthesized TagKey<bool> at slot 100.
card.Set<Deathrattle>(true);                                 // type-parameter sugar
card.Set(Tag.Deathrattle, true);                             // value-as-identity (named field)
card.Set(Tag<Deathrattle>.Key, true);                        // value-as-identity (generic-static)
card.Set<Deathrattle>(false);                                // silenced — still tagged
card.Has<Deathrattle>();                                     // true (silenced OR active)
card.Get<Deathrattle, bool>();                               // true | false
card.Remove<Deathrattle>();                                  // strip entirely
scope.ScheduleDirect(card, new Deathrattle(card));           // payload — same class
scope.With(Tag.Deathrattle);                                 // existing indexed-query API
foreach (var e in world.Entities.Where<Deathrattle>()) ...   // active deathrattles
foreach (var e in world.Entities.WhereHas<Deathrattle>()) ...// includes silenced
```

### 7. Wire events

**Deferred — see T3 in the iteration log.** Today's `TagSetEvent` / `TagChangeEvent` carrying `string Key` is preserved unchanged through this migration. The follow-up spike on Name→Slot must address designer-authored content (string label at content-import boundary, slot internally). The only new wire event introduced by *this* spike is `TagRemovedEvent`, paired with the three-state semantics:

```csharp
public sealed class TagRemovedEvent : WireEvent
{
    public int EntityId { get; }
    public string Key { get; }                      // consistent with TagSetEvent / TagChangeEvent
    public object? Prev { get; }
    public override string Kind => "TagRemoved";
}
```

Mirror's Unapply semantics for `TagRemovedEvent`: restore `Prev` at the slot (re-add the entry), pairing with the existing TagSet/TagChange Unapply behaviour.

## What we win

- **Symmetry across keywords.** Deathrattle, future Poison, future OnCast all live as single classes — type handle + payload constructor + queryability in one place.
- **Type-keyed queries.** `world.Entities.Where<Deathrattle>()` works. Compile-time enforcement via `ITagKey<bool>` interface constraint.
- **Deterministic slots.** Author-assigned ints, analyzer-checked. No registration-order drift between test runs or scene loads. Foundation for future flat-array storage (`object?[] _slots` indexed by slot).
- **Three-state semantics first-class.** Active / silenced / removed are distinct and explicit via `Set(true)` / `Set(false)` / `Remove`. Matches gameplay (silence effects, dispel-without-clear, "tagged but disabled" cases). New `TagRemovedEvent` on the wire.
- **Aligned with entity-store-extraction.** Both registration paths produce TagKey<bool> instances the indexed-query store already knows how to bucket; no parallel query surface needed.
- **Pattern A unchanged at call sites.** `entity.Set(Tags.Health, 30)` reads identical today and tomorrow. Migration cost concentrated on the keyword surface.

## What we lose

1. **Manual slot management.** Adding a tag means picking the next free int. Annoying but bounded. Analyzer catches duplicates; gaps from removed tags can be reused or left empty.

2. **Reflection at boot.** Pattern B's registration scans assemblies for `[TagSlot]` classes. One-time at startup; negligible at runtime. Verify on IL2CPP/AOT for Unity build targets. Sourcegen can eliminate later.

3. **One extra static lookup per Pattern B keyword.** `Tag.Deathrattle` / `Tag<Deathrattle>.Key` resolve through a registry-populated singleton instead of a direct field read. Generic-static caching makes it free after first touch; named-field access depends on the materialization mechanism (sourcegen = direct field; partial classes = direct field; manual one-liner = direct field). Either way, essentially zero.

## API surface summary

| Operation                       | Pattern A (value)                 | Pattern B (type)                              |
|---------------------------------|-----------------------------------|-----------------------------------------------|
| Read                            | `e.Get(Tags.Health)` → int        | `e.Get<Deathrattle, bool>()` → bool           |
| Write                           | `e.Set(Tags.Health, 30)`          | `e.Set<Deathrattle>(true)` (via overload)     |
| Presence (slot-has)             | `e.Has(Tags.Backlash)`            | `e.Has<Deathrattle>()`                        |
| Remove (clear slot)             | `e.Remove(Tags.Backlash)`         | `e.Remove<Deathrattle>()`                     |
| Query world (active)            | `scope.Where(Tags.Backlash)`      | `world.Entities.Where<Deathrattle>()`         |
| Query world (incl. silenced)    | (Pattern A's flag is binary)      | `world.Entities.WhereHas<Deathrattle>()`      |
| As event payload                | n/a (Pattern A is data-only)      | `new Deathrattle(source)` — class doubles up  |

## Open items

1. **Pattern A `Remove` API + wire event shape.** New `TagRemovedEvent`. Decide mirror's Unapply semantics (restore previous value vs. unset).
2. **Pattern B `Set` overloads.** Need per-value-type overloads to skip `Set<Deathrattle, bool>(true)`. Three should cover the slice (bool / int / string). Inline on `IEntity` or extension methods?
3. **Slot range conventions.** Locked or informal? Informal is simpler; conventions add discipline.
4. **Boot-time registration discovery.** Manual `RegisterType(typeof(X))` calls or reflective scan? Start reflective; defer sourcegen.
5. **Debug name override.** Do we need `[TagSlot(100, "deathrattle")]` in practice? Drop the optional parameter if never used.
6. **Migration of `Where(TagKey<bool> marker)` engine APIs.** [ICommandScope.cs:28](Assets/Sample/VerticalSlice/Engine/Commands/ICommandScope.cs:28), [IEffectScope.cs:56](Assets/Sample/VerticalSlice/Engine/Effects/IEffectScope.cs:56), [IHookScope.cs:62](Assets/Sample/VerticalSlice/Engine/Hooks/IHookScope.cs:62), [World.cs:100](Assets/Sample/VerticalSlice/Engine/Pipeline/World.cs:100) all take `TagKey<bool>` today. Keep as-is for Pattern A, add parallel generic versions for Pattern B, or unify?
7. **DeathrattleBridge relationship to the flag.** [DeathrattleBridge.cs:29](Assets/Sample/VerticalSlice/Sample/DeathrattleBridge.cs:29) fires the payload unconditionally. Under the new design, does the bridge pre-check `cmd.Card.Has<Deathrattle>()`? Or does `CardEffectStore` absence handle silently? Locks down the relationship between "tag flag" and "attached effect entry."
8. **Backlash classification.** Stays in Pattern A (plain flag at slot 40) for now. Reconsider if/when the retaliation amount or behavior becomes per-card.
9. **Slot range for future-Poison-style.** Reserve `200+` for Pattern B "stateful keywords" if/when they arrive, separate from `100+` "boolean keywords"?

## Iteration log

### T1–T4 walkthrough (2026-05-15)

Read against [entity-store-extraction.md](entity-store-extraction.md) (`validated`) and [tag-bag-design.md](tag-bag-design.md) (older draft, now `concluded` and forward-linked here). Four tension points resolved:

| # | Tension | Resolution |
|---|---|---|
| T1 | Pattern B (`class : ITagKey<T>`) reintroduces type-as-identity, conflicting with entity-store-extraction's value-as-identity lock. | **Synthesize a hidden `TagKey<bool>` singleton at registration** for every Pattern B class, at the declared slot. Value-as-identity preserved; effects-as-data still works for keywords; the class is opportunistic (only when a payload class already exists). |
| T2 | Indexed-query surface integration — `scope.With(TagKey<bool>)` takes instances; Pattern B has none. | Resolved by T1. Sugar exposed two equivalent ways: **`Tag<Deathrattle>.Key`** (generic-static, JIT-specialized) and **`Tag.Deathrattle`** (named field). Both resolve to the synthesized singleton. Mechanism for `Tag.Deathrattle` materialization deferred to migration time. |
| T3 | My spike proposed dropping wire `Name` (string Key) for `int Slot`. Entity-store-extraction's bench assumes the existing string-keyed event shape. | **Deferred.** Today's `string Key` shape preserved through this migration. Follow-up spike covers Name → Slot + **designer-authored content** (stable string label at content-import boundary; slot internally). |
| T4 | `tag-bag-design.md` recommended Option D (per-tag class everywhere), now overtaken. | Flipped to `status: concluded` with forward-links to entity-store-extraction (rationale) and this spike (migration shape). |

## Outcome

TBD. Likely folds into a tag-identity model under `Product/features/tags/` once the migration ships. May graduate to an ADR if the wire-format and designer-authoring decisions land as project-level locks.

## When this lands

This spike is graduated when the migration of `Tags.cs` to explicit-int slots and the introduction of the first Pattern B class (Deathrattle) ship green on the test suite. Forward link will point at the resulting model or ADR.
