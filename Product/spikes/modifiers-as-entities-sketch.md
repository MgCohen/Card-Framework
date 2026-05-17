---
type: spike
status: parked
tags: [#spike, #entities, #modifiers, #ecs]
---

# Modifiers as Entities — sketch (parked)

> **Status: parked.** Captured during the Definition × Entity design conversation on 2026-05-16. The shape below was settled enough to write down, but modifiers proved to be a higher layer that needs its own focused pass. Saved here so the Definition + Entity work can proceed without re-litigating modifier shape.

## Context

Working in the tag-bag ECS world of [`Assets/Spike/ECS/VerticalSlice/`](Assets/Spike/ECS/VerticalSlice/) — single concrete `Entity` class with `int Id` + sparse `TagKey<T> → value` dict ([Entity.cs](Assets/Spike/ECS/VerticalSlice/Engine/Entities/Entity.cs)), `EntityStore` indexed by tag ([EntityStore.cs](Assets/Spike/ECS/VerticalSlice/Engine/Pipeline/EntityStore.cs)). No definitions exist yet — cards are built imperatively in [SliceBootstrap.MakeMinion](Assets/Spike/ECS/VerticalSlice/Unity/SliceBootstrap.cs#L190).

The framework docs (`Product/features/entities/models/*`) describe a settled `EntityInstance<TDef>` + `EntityState (Bases, Modifiers)` shape. **That shape is valid at the theory/idea level but is not the spike's implementation target** — the spike runs a tag-bag ECS, not the bridge/store/handle model.

## What was settled

1. **Modifiers are entities. Period.** Every game's modifier shape (enchantment, equipment, buff token, aura, status effect, MTG-style permanent) reduces to "entity with modifier rows pointing at a target." No separate modifier storage; same `Entity` class, `Type = Modifier`.

2. **Entity base has three first-class fields**, not tags:
   - `int Id` — instance identity (already there)
   - `EntityType Type` — `readonly struct(int Value, string Name)`, TagKey-style open registry
   - `int DefinitionId` — 0 for generic entities

3. **Catalog-per-type.** Each `EntityType` has its own `Dictionary<int, TDef>`. Card #1 and Zone #1 don't collide.

4. **Base value + ordered modifier fold → effective value.** Base values live in tags (written at Build from the definition). Modifiers fold on read, ordered by a `ModOrder` integer ascending, FIFO ties. Add-before-multiply falls out of `Order` discipline (Add@100, Mul@200).

5. **Intrinsic vs granted is tag-vs-modifier.** "Argent Squire has Divine Shield" = tag set at Build. "Spell grants Divine Shield" = modifier-entity with `OverrideBool{true}` on the DS slot.

6. **One ModifierDefinition → N modifier-entities (one per row).** Lets `RemoveModifiersFromSource(srcId)` be a single indexed sweep that doesn't need to know which rows the def declared.

## Anchor decisions referenced

- **Int over enum for open type sets.** `EntityType` is a struct-wrapped int, declared as `static readonly` fields (TagKey pattern). Games extend freely.
- **Int over string for IDs.** `DefinitionId` is int. Names are debug-only side data.
- Both saved as feedback memories.

## The sketch

```csharp
// ---- Primitives -----------------------------------------------------

public readonly struct EntityType : IEquatable<EntityType>
{
    public int Value { get; }
    public string Name { get; }                            // debug only
    public EntityType(int v, string n) { Value = v; Name = n; }
    public bool Equals(EntityType o) => Value == o.Value;
    public override int GetHashCode() => Value;
}

public static class EntityTypes
{
    public static readonly EntityType Card     = new(1, "Card");
    public static readonly EntityType Zone     = new(2, "Zone");
    public static readonly EntityType Player   = new(3, "Player");
    public static readonly EntityType Modifier = new(4, "Modifier");
}

public abstract class TagKey { public int Slot; public string Name; }
public sealed class TagKey<T> : TagKey { public TagKey(int s, string n){Slot=s;Name=n;} }

public sealed class Entity
{
    public int        Id;
    public EntityType Type;
    public int        DefinitionId;                        // 0 = generic
    private readonly Dictionary<int, object?> _slots = new();
    public T    Get<T>(TagKey<T> k) => _slots.TryGetValue(k.Slot, out var v) && v is T t ? t : default!;
    public bool Has<T>(TagKey<T> k) => _slots.ContainsKey(k.Slot);
    public void Set<T>(TagKey<T> k, T v) => _slots[k.Slot] = v;
}
```

```csharp
// ---- Modifier ops (strategy objects, stored in a tag slot) ----------

public interface IOp { }
public interface IOp<T> : IOp { T Apply(T current); }

public readonly struct AddInt       : IOp<int>  { public int  Amount; public int  Apply(int c)  => c + Amount; }
public readonly struct MulInt       : IOp<int>  { public int  Factor; public int  Apply(int c)  => c * Factor; }
public readonly struct OverrideBool : IOp<bool> { public bool Value;  public bool Apply(bool _) => Value; }
```

```csharp
// ---- Tag inventory --------------------------------------------------

public static class Tags
{
    // Card stats
    public static readonly TagKey<int>    Cost         = new(24, "cost");
    public static readonly TagKey<int>    Attack       = new(22, "attack");
    public static readonly TagKey<int>    Health       = new(20, "health");
    public static readonly TagKey<bool>   DivineShield = new(40, "divineShield");
    public static readonly TagKey<string> Name         = new(60, "name");
    public static readonly TagKey<int>    Owner        = new(81, "owner");
    public static readonly TagKey<int>    Zone         = new(80, "zone");

    // Modifier rows (live on Type=Modifier entities)
    public static readonly TagKey<int>    ModTarget    = new(90, "modTarget");
    public static readonly TagKey<int>    ModSlot      = new(91, "modSlot");
    public static readonly TagKey<int>    ModSource    = new(92, "modSource");
    public static readonly TagKey<int>    ModOrder     = new(93, "modOrder");
    public static readonly TagKey<IOp>    ModOp        = new(94, "modOp");
}
```

```csharp
// ---- Definitions + catalogs ----------------------------------------

public sealed class CardDefinition
{
    public int    Id;
    public string Name = "";
    public int    Cost, Attack, Health;
    public bool   DivineShield;
    // OnCast factory, art, etc — hand-waved
}

public sealed class ModifierDefinition
{
    public int    Id;
    public string Name = "";
    public List<(TagKey Slot, IOp Op, int Order)> Rows = new();
}

public sealed class Catalogs
{
    public Dictionary<int, CardDefinition>     Cards     { get; } = new();
    public Dictionary<int, ModifierDefinition> Modifiers { get; } = new();
    // Zones, Players, Tokens, ...
}
```

```csharp
// ---- Store + queries -----------------------------------------------
// LINQ here for clarity; real impl uses TagIndex (Type bucket + (Target,Slot)
// index + Source index).

public sealed class EntityStore
{
    private readonly Dictionary<int, Entity> _entities = new();
    private int _nextId = 1;

    public Entity Create(EntityType type, int defId = 0)
    {
        var e = new Entity { Id = _nextId++, Type = type, DefinitionId = defId };
        _entities[e.Id] = e;
        return e;
    }
    public Entity Get(int id) => _entities[id];
    public void   Destroy(int id) => _entities.Remove(id);

    public IEnumerable<Entity> ModifiersTargeting(int targetId, int slot) =>
        _entities.Values
            .Where(e => e.Type.Equals(EntityTypes.Modifier))
            .Where(e => e.Get(Tags.ModTarget) == targetId)
            .Where(e => e.Get(Tags.ModSlot)   == slot)
            .OrderBy(e => e.Get(Tags.ModOrder));               // FIFO ties

    public IEnumerable<Entity> ModifiersFromSource(int sourceId) =>
        _entities.Values
            .Where(e => e.Type.Equals(EntityTypes.Modifier))
            .Where(e => e.Get(Tags.ModSource) == sourceId);
}
```

```csharp
// ---- Effective read -------------------------------------------------

public static class Effective
{
    public static T Read<T>(this Entity e, TagKey<T> key, EntityStore store)
    {
        var v = e.Get(key);                                    // base from tag
        foreach (var m in store.ModifiersTargeting(e.Id, key.Slot))
            v = ((IOp<T>)m.Get(Tags.ModOp)).Apply(v);
        return v;
    }
}
```

```csharp
// ---- Spawn helpers --------------------------------------------------

public static class Spawn
{
    public static Entity Card(EntityStore store, CardDefinition def, int ownerId, int zoneId)
    {
        var e = store.Create(EntityTypes.Card, def.Id);
        e.Set(Tags.Name,   def.Name);
        e.Set(Tags.Cost,   def.Cost);
        e.Set(Tags.Attack, def.Attack);
        e.Set(Tags.Health, def.Health);
        if (def.DivineShield) e.Set(Tags.DivineShield, true);
        e.Set(Tags.Owner,  ownerId);
        e.Set(Tags.Zone,   zoneId);
        return e;
    }

    // One modifier-entity per row → cleanup-by-source is a single sweep.
    public static void Modifier(EntityStore store, ModifierDefinition def, int targetId, int sourceId)
    {
        foreach (var (slot, op, order) in def.Rows)
        {
            var m = store.Create(EntityTypes.Modifier, def.Id);
            m.Set(Tags.ModTarget, targetId);
            m.Set(Tags.ModSlot,   slot.Slot);
            m.Set(Tags.ModSource, sourceId);
            m.Set(Tags.ModOrder,  order);
            m.Set(Tags.ModOp,     op);
        }
    }

    public static void RemoveModifiersFromSource(EntityStore store, int sourceId)
    {
        foreach (var m in store.ModifiersFromSource(sourceId).ToList())
            store.Destroy(m.Id);
    }
}
```

```csharp
// ---- Worked scenario -----------------------------------------------

var cat = new Catalogs();

cat.Cards[101] = new CardDefinition {
    Id = 101, Name = "Argent Squire",
    Cost = 1, Attack = 1, Health = 1,
    DivineShield = true,                                       // intrinsic = tag
};
cat.Cards[201] = new CardDefinition {
    Id = 201, Name = "Sword of Vigor",
    Cost = 3, Attack = 0, Health = 0,
};

cat.Modifiers[501] = new ModifierDefinition {
    Id = 501, Name = "Sword of Vigor: +3 ATK",
    Rows = { (Tags.Attack, new AddInt { Amount = 3 }, 100) },
};
cat.Modifiers[502] = new ModifierDefinition {
    Id = 502, Name = "Battle Cry: ×2 ATK",
    Rows = { (Tags.Attack, new MulInt { Factor = 2 }, 200) },
};

var store = new EntityStore();

var squire = Spawn.Card(store, cat.Cards[101], ownerId: 0, zoneId: 0);
var sword  = Spawn.Card(store, cat.Cards[201], ownerId: 0, zoneId: 0);

squire.Read(Tags.Attack,       store);   // 1
squire.Read(Tags.DivineShield, store);   // true

// Equip
Spawn.Modifier(store, cat.Modifiers[501], targetId: squire.Id, sourceId: sword.Id);
squire.Read(Tags.Attack, store);         // 1 + 3 = 4

// Battle Cry from some hero ability
Spawn.Modifier(store, cat.Modifiers[502], targetId: squire.Id, sourceId: 0);
squire.Read(Tags.Attack, store);         // (1 + 3) × 2 = 8     ← Add(100) before Mul(200)

// Sword breaks
Spawn.RemoveModifiersFromSource(store, sword.Id);
squire.Read(Tags.Attack, store);         // 1 × 2 = 2
```

## What the sketch asserts

1. **Definition is upstream of base.** `Spawn.Card` reads the def and writes tags. The entity doesn't consult the def at read time. `DefinitionId` is on the entity for replay / catalog lookups, not for value fall-through.
2. **Modifier-entities are the only modifier representation.** Same `Entity` class, `Type = Modifier`, rows as tags. No separate type, no parallel storage.
3. **Effective read = base tag + fold.** Ordering via `ModOrder` ascending, FIFO ties. Multiplicative-after-additive falls out of authoring `Order` ranges.
4. **One ModifierDef → N modifier-entities** (one per row) — cleanup-by-source is a single indexed sweep.
5. **Intrinsic vs granted is a tag-vs-modifier distinction.**

## What the sketch hand-waves (real impl will need)

- **TagIndex** for the modifier queries (Type bucket + `(ModTarget, ModSlot)` index + `ModSource` index) — same shape as today's [TagIndex.cs](Assets/Spike/ECS/VerticalSlice/Engine/Pipeline/TagIndex.cs).
- **Type-safe `IOp<T>` cast** in `Effective.Read`. Today's `Tags.ModOp` holds `IOp`; the cast assumes the author paired the right op kind with the right slot. Authoring-time check or a typed `TagKey<IOp<T>>` per slot family would lock this down.
- **Wire emission** for modifier-entity create/destroy. Falls out of the existing entity wire shape — no new event kinds needed.
- **OnCast / Deathrattle wiring** for cards — sits in EffectStore as today, attached at `Spawn.Card` if the def carries factories.
- **MTG-style "card *is* a modifier"** (enchantment/aura/equipment). Sketch direction: card-entity spawns a *child* modifier-entity on resolve; two entities with a parent-of relation. Not exercised in the scenario.

## Open questions parked with the sketch

- **`IOp<T>` typing discipline.** How do we keep author paths from pairing an `IOp<int>` with a `TagKey<bool>` slot? Compile-time check via typed slot families? Runtime guard? Author convention only?
- **Modifier-entity lifecycle vs source lifecycle.** Today the sweep is "source dies → all its modifiers destroyed." Aura semantics may want "aura present → modifiers exist, aura leaves → modifiers gone" with no explicit destroy call. That's a continuous-effect / continuous-modifier flavor that may need its own pass.
- **Ordering tiers.** Single `Order` int is fine for Add-then-Mul. MTG-style layer system (CR 613) needs tiers. Defer until a game pushes on it.
- **Where the modifier-entity gets its `Type=Modifier` queries indexed.** Add a per-Type bucket to `EntityStore` alongside the existing tag indices — the index pattern is already there; this just adds a new pivot.

## Why this is parked

The Definition × Entity setup question (how entities are *created* from definitions) is the foundational layer. Modifiers add a separate fold on top of that foundation. Pinning the modifier shape first wasn't blocking the Definition + Entity conversation; deferring it lets that conversation finish without entangling. Pick this back up after Definition + Entity + setup lands.
