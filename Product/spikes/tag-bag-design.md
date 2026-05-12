---
type: spike
status: draft
tags: [#spike, #tag-bag, #refoundation]
---

# Tag-Bag Design — Shaping the Key→Value Pair

> **Status: draft.** One-decision spike for the event-sourced refoundation under discussion. Scope is *only* the C# shape of an entity's `(key → typed value)` pair. The event/envelope mechanics, the `TagChange` payload shape, the merge protocol, and the historical replay surface are siblings and out of scope here; they assume whichever shape this spike settles on.

## Context

The framework currently runs on Scaffold State: a `Store` of immutable slices, per-payload `Mutator<TState, TPayload>` writers, and an entities-bridge that exposes per-instance variables (`Variable` + `VariableValue`) and layered modifiers (`VariableModifier<T>` over `EntityState.ModifierStacks`). Variables are `(Key: string, PayloadTypeId: string)` value objects authored on a per-game `Vars` catalog; reads land via `entity.Storage.GetVariable<int>(Vars.Health)` and writes synthesize `SetBaseValuePayload` (see [`features/entities/models/03-variable.md`](features/entities/models/03-variable.md), [`features/entities/models/04-modifier.md`](features/entities/models/04-modifier.md)).

The team is weighing a pivot to a **Hearthstone-shaped, event-sourced** model. Under that pivot:

- Every entity is a flat bag of `(key → typed value)` tags. There is no separate `BaseValues` / `ModifierStacks` split — the effective value is whatever the bag currently says.
- Mutation is `TagChange(entityId, key, oldValue, newValue)`. The historian records **envelopes** (groups of tag changes per command/effect, nestable into blocks).
- Modifier composition is folded *into* the tag write: the system that decides "this minion's effective ATK after the +3 buff" writes that ATK as a new value into the bag, then writes it back when the buff drops.

The prior art doc on SabberStone ([`research/sabberstone-architecture.md`](../research/sabberstone-architecture.md) §2) calls this out explicitly: "An `Entity` is essentially a container for a collection of properties called `GameTag`s … State changes occur by modifying an entity's tags rather than changing explicit properties." SabberStone (and Brimstone, per [`research/brimstone-architecture.md`](../research/brimstone-architecture.md) §1) both use a **single `GameTag` enum** as the key and a **raw `int`** as the value — including for tags whose semantic value is a reference (entity IDs are stored as the target's integer ID), an enum (`ZONE`, `CARDTYPE`), or a bool (any non-zero is true).

The pivot raises one decision that has to land before the envelope/replay/network designs can settle: **how is the key→value pair shaped in C#?** This spike compares four options against the axes the refoundation will lean on (authoring ergonomics, refactor safety, serialization, debug inspection, modifier composition, performance, extensibility, migration from the current `Variable` machinery), and lands a recommendation.

---

## 1. Options compared

Four candidates. The first three are "pure" shapes; the fourth is a hybrid the team called out specifically.

### Option A — Strongly-typed tag classes (one C# type per tag)

Each tag is its own sealed C# type, parameterized on its inner value type. The bag is keyed by `System.Type` (or a closed-over `TagKey<T>` token).

```csharp
public abstract class Tag { }                       // marker
public abstract class Tag<T> : Tag { }              // value-typed base

public sealed class Atk    : Tag<int>  { }
public sealed class Health : Tag<int>  { }
public sealed class Zone   : Tag<ZoneId> { }        // enum-valued tag
public sealed class Owner  : Tag<EntityId> { }      // ref-valued tag
public sealed class Frozen : Tag<bool> { }

// On the entity:
public interface IEntity
{
    T    Get<TTag, T>()                 where TTag : Tag<T>;
    bool TryGet<TTag, T>(out T value)   where TTag : Tag<T>;
    void Set<TTag, T>(T value)          where TTag : Tag<T>;
    IEnumerable<(Type Key, object Value)> All();
}
```

Authoring:

```csharp
// declare a tag (once, per game or per framework module)
public sealed class Atk : Tag<int> { }

// read
int atk = minion.Get<Atk, int>();

// write (publishes TagChange event)
minion.Set<Atk, int>(7);

// list all tags on entity (for inspectors, serialization)
foreach (var (key, value) in minion.All())
    Debug.Log($"{key.Name} = {value}");
```

Serialization needs a name table: the bag stores `Type` keys, but JSON wants strings. Either (a) the tag type's `Name` becomes the string, or (b) each tag carries a `[TagKey("ATK")]` attribute for stable wire names.

### Option B — String-keyed registry (close to current `Variable`)

Each tag is a `Tag("atk", typeof(int))` value object stored on a per-game catalog. The bag is `Dictionary<Tag, object>` (or `Dictionary<string, ITagValue>`).

```csharp
public sealed record Tag(string Key, Type ValueType);

public static class Tags
{
    public static readonly Tag Atk    = new("atk",    typeof(int));
    public static readonly Tag Health = new("health", typeof(int));
    public static readonly Tag Zone   = new("zone",   typeof(ZoneId));
    public static readonly Tag Owner  = new("owner",  typeof(EntityId));
    public static readonly Tag Frozen = new("frozen", typeof(bool));
}

public interface IEntity
{
    T    Get<T>(Tag key);
    bool TryGet<T>(Tag key, out T value);
    void Set<T>(Tag key, T value);   // validates T against key.ValueType
    IEnumerable<(Tag Key, object Value)> All();
}
```

Authoring:

```csharp
// declare
public static readonly Tag Atk = new("atk", typeof(int));

// read / write
int atk = minion.Get<int>(Tags.Atk);
minion.Set(Tags.Atk, 7);

// list
foreach (var (tag, value) in minion.All())
    Debug.Log($"{tag.Key} = {value}");
```

This is the shape the framework already ships. `Tag` here is one rename away from today's `Variable`. JSON serialization is trivial (`{"atk": 7, "health": 5, …}`); roundtripping requires the catalog to look up `ValueType` per key.

### Option C — Enum-keyed registry (Hearthstone / SabberStone)

A single framework-wide `GameTag` enum lists every tag. The bag is `Dictionary<GameTag, int>` (or `Dictionary<GameTag, long>` to hold most ref/enum/bool encodings as integers).

```csharp
public enum GameTag
{
    ATK,
    HEALTH,
    ZONE,        // value is (int)ZoneId
    OWNER,       // value is entity.Id (int)
    FROZEN,      // value is 0 or 1
    EXHAUSTED,
    COST,
    // …
}

public interface IEntity
{
    int  Get(GameTag key);
    bool TryGet(GameTag key, out int value);
    void Set(GameTag key, int value);
    IReadOnlyDictionary<GameTag, int> All { get; }
}
```

Authoring:

```csharp
// declaration: edit the enum
public enum GameTag { …, ATK, HEALTH, … }

// read / write — everything is int
int atk = minion.Get(GameTag.ATK);
minion.Set(GameTag.ATK, 7);
minion.Set(GameTag.FROZEN, 1);          // bool encoded
minion.Set(GameTag.OWNER, ownerEntityId.Value);   // ref encoded

// list
foreach (var (tag, value) in minion.All)
    Debug.Log($"{tag} = {value}");
```

JSON is trivial: enum name → int. Value semantics are uniform (`int`), so the bag is array-of-int-sized and cache-friendly; SabberStone leans hard on this. The cost is uniformity discipline: bools, refs, and small enums all encode as `int`; non-int values (string card names, lists of card refs, structured data) live *outside* the tag bag in side maps.

### Option D — Hybrid (typed tag class + registered string/enum key)

Strongly-typed C# tag classes (Option A) that *additionally* register themselves under a stable string (or int) key for serialization and reflective inspection. The bag is keyed by the registered key under the hood; the API exposes the typed surface for handler code.

```csharp
public abstract class Tag<T>
{
    public string Key { get; }                              // stable wire name
    public Type   ValueType => typeof(T);
    protected Tag(string key) { Key = key; TagRegistry.Register(this); }
}

public sealed class Atk    : Tag<int>    { public static readonly Atk    Instance = new("atk");    private Atk(string k) : base(k) {} }
public sealed class Health : Tag<int>    { public static readonly Health Instance = new("health"); private Health(string k) : base(k) {} }
public sealed class Owner  : Tag<EntityId> { public static readonly Owner Instance = new("owner"); private Owner(string k) : base(k) {} }

public interface IEntity
{
    T    Get<T>(Tag<T> key);
    bool TryGet<T>(Tag<T> key, out T value);
    void Set<T>(Tag<T> key, T value);
    IEnumerable<(string Key, object Value)> All();   // by registered key
}
```

Authoring:

```csharp
// declaration — once per tag
public sealed class Atk : Tag<int>
{
    public static readonly Atk Instance = new("atk");
    private Atk(string k) : base(k) { }
}

// read / write — fully type-safe at the call site
int atk = minion.Get(Atk.Instance);
minion.Set(Atk.Instance, 7);

// serialize — uses the registered string key under the hood
var json = serializer.Serialize(minion);   // {"atk":7,"health":5,"owner":42}

// list — debugger-friendly names
foreach (var (key, value) in minion.All())
    Debug.Log($"{key} = {value}");
```

This is what most production-grade tag systems converge to (the SabberStone enum is a degenerate form: the "type" is implicit-by-convention; the registered key is the enum value). The hybrid pays one-time declaration cost per tag in exchange for compile-time call-site safety *and* clean serialization.

### Option matrix

| Axis | A — Typed classes | B — String registry | C — Enum registry | D — Hybrid (typed + registered) |
|---|---|---|---|---|
| Declaration cost per tag | High (one class) | Low (one `new Tag(...)` line) | Low (one enum entry) | High (one class with key) |
| Read/write call-site safety | Compile-time | Runtime cast | Runtime cast | Compile-time |
| Refactor safety (rename) | IDE rename works | Find/replace strings | IDE rename works | IDE rename + key migration |
| Refactor safety (delete) | Compile-time errors | Silent until runtime | Compile-time | Compile-time |
| JSON serialization | Needs name table | Trivial | Trivial (enum name) | Trivial (registered key) |
| Wire format stability | Tied to type name unless attributed | Stable (string is the key) | Stable (enum int) | Stable (registered key, decoupled from type name) |
| Debug-time inspection | Type name shows | String shows | Enum name shows | Registered key shows |
| Non-int values | Native | Native (via `Type`) | Encode as int (loses type) | Native |
| Per-tag access (hot path) | Dict\<Type, object\> + box on int | Dict\<Tag, object\> + box | int[] indexable | Dict + typed unbox |
| Extensibility (game-specific tags) | Subclass `Tag<T>` | Add to game catalog | Edit framework enum or run parallel enum | Subclass + register |
| Migration cost from current `Variable` | High (rewrite all sites) | Trivial (rename `Variable`→`Tag`) | High (introduce enum, rewrite sites) | Medium (typed wrappers over current Variable infra) |

---

## 2. Trade-offs across axes

### 2.1 Authoring ergonomics

The Pile Counter sketch ([`support/samples/pile-counter.md`](../support/samples/pile-counter.md) §9) is the floor for what authors do: declare one or two variables on a `Vars` static, read them as `card.Storage.GetVariable<int>(Vars.Number)`. All four options handle that case cleanly. Where they diverge is when the catalog grows past ten or twenty tags:

- **A and D** front-load declaration cost — every tag is a class. With Roslyn source generators (`[Tag("atk")] partial class Atk`), this drops to a one-liner. Without generators, it's noticeable boilerplate.
- **B** has the lowest declaration friction. Adding a tag is one line on a static class.
- **C** has the lowest *per-tag* friction but the highest *cross-game* friction: shipping a deckbuilder + a TCG + a spatial card game means three games' tags coexist on one enum (or each game runs a parallel enum and the framework picks a base offset, which is what real Hearthstone clones do — see SabberStone's `GameTag` having ~600 entries because Hearthstone is one game).

For framework code itself, **A/D** read better — `entity.Get<Atk, int>()` or `entity.Get(Atk.Instance)` over `entity.Get<int>(Tags.Atk)` — but the gap is small.

### 2.2 Refactor safety

This is where typed shapes earn their boilerplate.

- **A/D.** Rename `Atk` → `Attack` and the IDE renames every reference. Delete `Atk` and the compiler points at every dead call site. Move a tag between assemblies and the build catches it.
- **B.** The string `"atk"` is opaque to the compiler. Rename is find/replace across strings; typos drift in silently; deleting `Tags.Atk` only fails the sites that read the field, not the sites that still type `"atk"` inline (the framework's existing convention discourages inline strings — see [`features/entities/models/03-variable.md`](features/entities/models/03-variable.md) §`Variable` — but discouragement isn't enforcement).
- **C.** Rename is IDE-safe, delete is compile-time, but the enum is global. Adding a tag requires editing the framework if the enum is framework-owned; running parallel enums per game muddies the wire format (the int value of `MyGame.GameTag.HARVEST_COUNT` may collide with the framework's int value at the same ordinal).

### 2.3 JSON / binary serialization

Event-sourcing makes serialization a hot path. Envelopes flow through history, network, replay, save/load.

- **A.** Bag is keyed by `Type`. JSON needs `Type → name` mapping. Two paths: lean on `type.FullName` (fragile across renames/refactors) or attach a `[TagKey("atk")]` attribute (works but is "Option D's registered key snuck in through the back").
- **B.** `{"atk": 7}`. The catalog provides the `Type` for deserialization. This is the cleanest pure shape for serialization.
- **C.** `{"ATK": 7}` (or `{"0": 7}` if numeric). Cleanest possible. The enum *is* the wire format.
- **D.** Same as B at the wire level — registered key is the string. The typed class layer is invisible to JSON. Best of A and B.

Binary serialization (Hearthstone protocol shape — see [`research/sabberstone-architecture.md`](../research/sabberstone-architecture.md) §4) packs `(tagId: int, value: int)` pairs. C is natively this; D maps to it by emitting the registered int. A and B both need a `name → int` table to hit binary parity.

### 2.4 Debug-time inspection

A live game has thousands of tag values flying through history. Debuggers need to show meaningful labels.

- **A/D.** Tag class name shows in the watch window (`Atk`, `Health`). `(Tag, object)` projections look right in inspectors.
- **B.** The `Tag` record's `Key` string shows. Fine.
- **C.** Enum names show (debuggers expand `(GameTag)1` to `GameTag.ATK`). Fine. Value is always `int`, so a `Frozen=1` entry doesn't show as `true` — the inspector has to know to format it.

All four are tolerable; **C is meaningfully worse for non-int tags** because the bag has lost the type.

### 2.5 Modifier-fold ergonomics

This is the load-bearing axis for this framework specifically. The current shape (per [`04-modifier.md`](../features/entities/models/04-modifier.md)) is `effective = base + Σ(modifiers)`, with modifiers as a separate layered list keyed by `Variable`. The Hearthstone shape *eliminates* the base/modifier split: the bag stores the *current effective* value, and the system that adds a +3 buff writes `Atk = base + 3` into the bag, then writes `Atk = base` when the buff drops.

Both shapes work in any of A/B/C/D. The interesting question is **whether the chosen key shape forces a separate "modifier stack" structure or lets modifiers themselves be tags.**

- **A/D.** Modifiers can be tags: introduce `AtkBonus : Tag<int>` alongside `Atk : Tag<int>`. The fold is `effective_atk = Get<Atk>() + Get<AtkBonus>()`. Multi-source bonuses need either (a) per-source bonus tags (`AtkBonusFromSword`, `AtkBonusFromAura`) — noisy — or (b) bonus tags whose value is a *list* of `(source, amount)` pairs. The list shape needs a generic value type, which A/D handle natively (`Tag<ImmutableList<(EntityId, int)>>`).
- **B.** Same as A/D — modifier tags coexist with base tags. The value type is part of the registered `Tag`.
- **C.** Value is `int`. Per-source modifiers can't live in the bag (no list values); they live in a parallel side structure or get collapsed into a running sum (the Hearthstone choice — `Atk` is always the current effective, lineage is lost from the entity itself, recoverable only by replaying the envelope history). SabberStone takes exactly this hit: the entity forgets where the +3 came from; tracing it back means walking the envelope log.

For a framework that wants modifiers to be **layered and inspectable, not hidden inside one mutable value** (per `04-modifier.md` §Invariants 1), C is *structurally* a step back. A/B/D can carry the layered shape directly.

The cleanest expression in A/D:

```csharp
public sealed class Atk      : Tag<int> { public static readonly Atk Instance = new("atk"); }
public sealed class AtkStack : Tag<ImmutableList<Mod>> { public static readonly AtkStack Instance = new("atk.stack"); }
// effective:
int Effective(IEntity e) =>
    e.Get(Atk.Instance) + e.Get(AtkStack.Instance).Sum(m => m.Amount);
```

The bag stays flat; the fold is a thin read-side helper; modifiers are still inspectable per-source.

### 2.6 Performance

Tag access is in the hot path: every legality check, every aura recompute, every render-side projection reads tags. Approximate per-access cost:

- **A.** `Dictionary<Type, object>` — hash on `Type` (fast), boxed `int` value (alloc on write, unbox on read).
- **B.** `Dictionary<Tag, ITagValue>` — hash on `Tag` record (`Key.GetHashCode()` cached), allocation on the value wrapper.
- **C.** `int[]` indexed by `(int)GameTag` — no hash, no boxing, contiguous in memory. ~order of magnitude faster than the dictionary shapes.
- **D.** Same as A under the hood (`Dictionary<Type, object>` or `Dictionary<string, object>`), with a typed adapter.

For SabberStone-scale (~600 tags, AI simulators running millions of tree-search branches per second), the C shape is non-negotiable. For a generic framework where a typical entity has 5–30 active tags and the simulation throughput is "one player move per second," the dictionary shapes are fine. Brimstone's design pressure (see [`research/brimstone-architecture.md`](../research/brimstone-architecture.md) §1 — "the `Game` state is heavily optimized to be cloned and branched thousands of times per second") is what pushes C; it isn't this framework's pressure today.

### 2.7 Extensibility (game-specific tags)

The framework ships generic primitives; games add their own.

- **A/B/D.** A game declares `public sealed class HarvestCount : Tag<int> { … }` or `public static readonly Tag HarvestCount = new(...)` in its own assembly. The framework never knows or cares. Clean.
- **C.** The framework's `GameTag` enum is closed. A game either (a) edits the framework, (b) runs a parallel `MyGameTag` enum and the framework picks `int GameTagId` as a sum type with a range partition, or (c) reserves a `Custom` slot in the framework enum and packs its game-specific ID into the value. All three are awkward. SabberStone-style projects sidestep this by *being* a single-game project.

For a multi-game framework (per [`general/VISION.md`](../general/VISION.md)), C's closed enum is a real cost. The framework would have to ship its enum as a `partial enum` (C# allows but it's awkward) or as an enum-like `static class` of constants, at which point the "enum" advantages start to evaporate.

### 2.8 Migration cost from the current `Variable`

Today's variable shape (per [`03-variable.md`](../features/entities/models/03-variable.md)) is `(string Key, string PayloadTypeId)` with `VariableValue` subclasses. This is almost exactly **Option B** — same authoring pattern (`public static readonly Variable Attack = new("attack", "int")`), same call-site shape (`storage.GetVariable<int>(Vars.Attack)`).

- **B.** Trivial rename. `Variable` → `Tag`, `VariableValue` → drop in favor of plain `object` + type-checked accessors. Existing `Vars` catalogs port one-for-one.
- **D.** Wrap the existing `Variable` in a typed class. The static catalog grows from one-line records to one-class-per-tag, but the wire format and call-site shape can stay close. Existing `GetVariable<int>(Vars.Foo)` becomes `Get(Foo.Instance)`. The transition can be incremental — typed wrappers and string-keyed access can coexist, and the framework can ship both surfaces until games migrate.
- **A.** Same boilerplate as D but loses the registered key — sites that serialize today break.
- **C.** Heaviest. Every existing `Variable` becomes an enum entry; non-int variables (the `BoolVariableValue`, `FloatVariableValue`, `StringVariableValue` ladder per `03-variable.md`) all need to collapse to `int` plus side maps. Game-specific tags need a new extensibility mechanism.

The bridge structure of today's `EntityState` (`BaseValues` + `ModifierStacks`) is **orthogonal** to this choice — it dissolves under the pivot regardless of which key shape lands, because the Hearthstone model collapses base/modifier into one bag and pushes layering into the envelope log. That dissolution is the bigger migration; the key-shape decision rides on top of it.

---

## 3. Modifier composition — worked side-by-side

`Villager.Atk = 1 (def) + 3 (sword) + 2 (rage) − 1 (rainy zone) = 5` (per `04-modifier.md`'s worked example).

**A / D — modifiers as tags (preferred shape).** Each contributing source writes a tag; the effective value is a read-side fold:

```csharp
public sealed class Atk        : Tag<int>                                   { /* base */ }
public sealed class AtkMods    : Tag<ImmutableList<AtkMod>>                 { /* stack */ }
public sealed record AtkMod(EntityId Source, int Amount, int Order);

// Sword equip:
var mods = villager.Get(AtkMods.Instance);
villager.Set(AtkMods.Instance, mods.Add(new AtkMod(sword.Id, +3, 10)));

// Read effective:
int EffectiveAtk(IEntity e) =>
    e.Get(Atk.Instance) +
    e.Get(AtkMods.Instance).OrderBy(m => m.Order).Sum(m => m.Amount);

// Sword unequip — uniform sweep by source:
villager.Set(AtkMods.Instance,
    villager.Get(AtkMods.Instance).Where(m => m.Source != sword.Id).ToImmutableList());
```

This preserves today's invariants: layered, inspectable, source-attributed, deterministic order. The `AtkMods` tag is itself an event-sourced value — adding/removing a mod produces a `TagChange(AtkMods, oldList, newList)` event in the envelope.

**B — same shape, looser typing.** Identical to A/D structurally:

```csharp
public static readonly Tag Atk     = new("atk",      typeof(int));
public static readonly Tag AtkMods = new("atk.mods", typeof(ImmutableList<AtkMod>));

int EffectiveAtk(IEntity e) =>
    e.Get<int>(Tags.Atk) +
    e.Get<ImmutableList<AtkMod>>(Tags.AtkMods).Sum(m => m.Amount);
```

**C — Hearthstone shape, no stack tag.** `Atk` is the current effective; modifier lineage lives in the envelope log. The call site that *adds* the buff writes `Atk = old + 3`; the call site that *removes* it walks the envelope log (or holds a side-table `Dictionary<EntityId, List<ActiveBuff>>`) and writes `Atk = old - 3`. Inspection of "where did this +3 come from" is *not* an entity-local read — it requires the envelope/log machinery:

```csharp
// Sword equip:
minion.Set(GameTag.ATK, minion.Get(GameTag.ATK) + 3);
// (Buff bookkeeping lives outside the tag bag, in a parallel buff table.)

// Sword unequip:
minion.Set(GameTag.ATK, minion.Get(GameTag.ATK) - 3);
```

This is the SabberStone shape; it's compact and fast, but it **collapses** the framework's existing modifier invariant (per `04-modifier.md` §Invariant 1). Recovering that invariant requires a parallel modifier stack structure outside the tag bag — at which point C is structurally B/D with worse value polymorphism.

**The chosen shape does not force a separate "modifier stack" structure.** Modifiers can live as tags (whose value is a list of contributions), provided the value type can be a list — i.e., provided the bag isn't `int`-only. Among the options, **only C forces the modifier stack to live outside the bag**.

---

## 4. Recommendation

**Adopt Option D — Hybrid typed-class + registered string key.**

### Rationale

1. **Refactor safety is non-optional once the catalog passes ~30 tags.** The framework's `bl-authoring-feature` plus all in-flight game-side `Vars` catalogs make typo drift and silent rename failures a real cost. Typed classes catch every miss at compile time. The strongest argument *against* B (the cheap and obvious choice) is the experience of every project that has reached real scale on string keys — including game frameworks that started with strings and ended up retrofitting strong types one tag at a time (`*GameTag.cs` generators are a common late-arrival).

2. **Serialization stays clean.** The registered string key is the wire format. Renaming the C# class doesn't break replays, saves, or network packets — the key is the contract; the class is just one binding to it.

3. **The framework's existing modifier invariant survives.** Modifier composition can stay layered and source-attributed via list-valued mod stack tags. Only C structurally forces collapsing this; A/B/D preserve it.

4. **Migration from the current `Variable` is incremental, not catastrophic.** Today's `Variable("attack", "int")` shape maps to `Atk.Instance` with `Key = "attack"`. The framework can ship `Tag<T>` alongside the current `Variable` for one release, generate typed wrappers from existing `Vars` catalogs, and let games migrate per-tag rather than per-game. The wire format on disk and over the network stays stable across the migration.

5. **Extensibility for game-specific tags is structural, not by enum-edit.** Games declare their own `Tag<T>` subclasses in their own assemblies. The framework never has to know.

6. **The Hearthstone enum trick is for one game's hot path, not a multi-game framework.** SabberStone's `GameTag` enum + `int` value works because Hearthstone is one game with one tag namespace and the simulator's bottleneck is AI tree search at millions of nodes/sec. This framework's bottleneck is "make Pile Counter, a TCG, a deckbuilder, and a spatial card game all work" (per [`general/VISION.md`](../general/VISION.md)) — that's the inverse pressure.

The one cost is per-tag boilerplate at declaration. A Roslyn source generator (`[Tag("atk")] partial class Atk : Tag<int>` → generated `Instance` field + registry registration) closes the gap. Until generators land, the boilerplate is "one class per tag" — acceptable for ~50–100 framework tags, lighter than C's enum-edit ceremony for game-side tags.

### What this *isn't*

- It isn't an event/envelope-shape decision. The TagChange payload, the envelope grouping, the historian, the merge-and-replay machinery all assume Option D's `(string key, T value)` pair as the input vocabulary and are designed in sibling spikes.
- It isn't a commitment to drop `VariableModifier<T>` immediately. Modifiers can live as list-valued tags *or* as the current `ModifierStacks` slice, at the framework's pace. The key-shape decision is the wedge that makes either path viable.

---

## 5. Open questions

These need follow-up design passes; none block adopting D.

- **Q-ref-tag-serialization.** A `Tag<EntityId>` (or `Tag<Ref<Card>>`) needs to roundtrip through JSON, binary, and replay. Same machinery as today's `Ref<T>` per ADR-0005 — the typed `Ref` already serializes — but the tag-value wrapper has to plumb through. Probably a `ITagValueSerializer` registered per value type, mirroring the current `VariableValue` registry.
- **Q-list-valued-tags.** Mod stack tags as `Tag<ImmutableList<Mod>>` work, but the framework should decide whether list-valued tags are first-class (the "modifiers are tags" path) or a special parallel mechanism (the "modifiers live in `ModifierStacks` slice" path). Affects whether the envelope shape sees a single `TagChange(AtkMods, oldList, newList)` event or N decomposed events.
- **Q-source-generators.** Whether to ship a `[Tag("atk")]` attribute + Roslyn generator now or wait for the boilerplate to bite. Cheap to add later, but adding it earlier makes the boilerplate argument moot from day one.
- **Q-tag-registry-lifetime.** The hybrid's `TagRegistry.Register(...)` runs at static-init time. Determinism across replay requires the registry to be stable across runs — same set, same order, same key strings. Probably needs an explicit `AssertSealed()` call at engine `Build()` time.
- **Q-presets-and-catalogs.** Whether tag *catalogs* are themselves authoring-tier data (a deckbuilder preset ships its own tag catalog distinct from the framework's). Loose end carried over from `bl-authoring-feature` — same shape regardless of which option here lands.

---

## 6. Migration shape

Authors migrate from `public static readonly Variable Attack = new("attack", "int")` to `public sealed class Attack : Tag<int> { public static readonly Attack Instance = new("attack"); private Attack(string k) : base(k) {} }` — same wire key `"attack"`, same inner type `int`, same per-game `Vars` (or new `Tags`) static catalog. Call sites change from `card.Storage.GetVariable<int>(Vars.Attack)` to `card.Get(Attack.Instance)`. A compatibility shim can keep `GetVariable<T>(Variable)` working against a typed bag for one release while games rename per-tag; the historian and persisted state stay readable across the cut because the registered key is the contract, not the C# type name.
