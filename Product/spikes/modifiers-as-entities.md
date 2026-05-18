---
type: spike
status: active
references:
  - Product/spikes/archive/modifiers-as-entities-sketch.md
  - Product/spikes/definition-entity-setup.md
  - Product/spikes/entity-identity-model.md
tags: [#spike, #entities, #modifiers, #ecs]
sandbox: Assets/Spike/ECS/Modifiers/
---

# Modifiers as Entities

> **Status: active.** Captures the locked shape from the design pass on 2026-05-18. Replaces [`spikes/archive/modifiers-as-entities-sketch.md`](archive/modifiers-as-entities-sketch.md), which was the parked starting point — see that doc's archive banner for the diff between the sketch and this design. Sandbox at [`Assets/Spike/ECS/Modifiers/`](../../Assets/Spike/ECS/Modifiers/) — forthcoming.

## Context

Modifiers are how authored content layers over the base entity state — equipment buffs, auras, enchantments, status effects, granted keywords, conditional bonuses. Composes on top of the tag-bag ECS world locked in [`spikes/definition-entity-setup.md`](definition-entity-setup.md): single concrete `Entity` class, three-field identity (`Id`, `Type`, `DefinitionId`), sparse `TagKey<T> → value` slot bag, `EntityType.Modifier` reserved at slot 5 in the registry closure.

The 2026-05-16 parked sketch had the broad shape right ("modifiers are entities") but the wrong mechanics for two reasons that surfaced in the design pass:

- **Fold-on-read pushes server logic to clients.** Clients don't need to know how a value was computed — they need the current value plus authored tooltips. Server is the source of truth.
- **Single uniform `Effective.Read` collapses semantically distinct stats.** Attack (purely derived) and Health (gameplay-mutable + buffable) are not the same shape; one fold rule for both produces wrong gameplay on at least one.

This spike walks the fix.

## Locked decisions

The design pass walked through effect shape, refresh strategy, transient vs rebaseable slots, exclusivity, modifier storage, and policy registry. Each lock here references the question it answers.

### Q1 — Push vs pull: **push**

Server folds modifiers into entity state at attach/refresh time. Clients read plain tag values plus an authored tooltip list. No `Effective.Read` on the read path; `e.Get(Tags.Attack)` is a dict lookup.

The wire just sees normal `TagChange` events when refresh runs. Tooltip is authored content on the modifier definition, read off the modifier entity's `DefinitionId` via the catalog.

### Q2 — Refresh strategy: **recompute from base**

Two coherent answers, no middle ground: A) recompute-from-base (reset to base, replay every active modifier's effect in `Order`); B) apply/undo deltas (each modifier mutates current state at attach, undoes its own change at detach).

**A wins.** B breaks under multi-mul ordering — `+3` then `×2` then detach `+3` should give `(1×2) = 2`, but pure undo gives `(8 − 3) = 5`. Stateless modifier effects compose; stateful inverses don't.

A also matches the SabberStone "AuraUpdate" pattern that the [`entity-identity-model.md`](entity-identity-model.md) audit leans toward, and naturally handles dependent modifiers (`+3 if Defense > 5` paired with `+5 Defense`) — every refresh re-evaluates from base, so dependencies pick up changes through `Order` ordering without per-modifier dependency wiring.

### Q3 — Per-tag rebase policy: **slots are `Rebaseable | Transient`**

Refresh-from-base would be wrong for slots gameplay can mutate independently of modifiers. Damage subtracts from Health; consumption flips DivineShield. If refresh reset these to base + replayed grants, consumed shields would magically return and damage would heal between refreshes.

The fix: per-slot policy.

| Policy | Examples | Refresh behavior | Mutated by |
|---|---|---|---|
| **Rebaseable** | Attack, Cost, MaxHealth, MaxShields, Spell Damage | Reset to base; replay all `Refresh` effects in `Order` | Modifiers only |
| **Transient** | Health, Shields (current), DivineShield, Stealth, Frozen | Untouched by refresh | Gameplay (damage, consumption) + one-shot modifier `OnAttach` |

A slot is **transient** if anything other than modifiers can mutate it. Damage → Health is transient. No-one writes Attack except modifiers → Attack is rebaseable.

### Q4 — Effect surface: **`OnAttach` + `Refresh` split**

A modifier definition carries two optional effects:

- **`OnAttach`** — runs once at spawn. Writes transient slots (grant DS, bump current Health). Never re-runs.
- **`Refresh`** — runs every refresh (spawn, detach of any sibling, dependent-change). Writes rebaseable slots only by author convention. Idempotent under reset+replay.

Most modifiers fill one or the other. Composites (Blessed Champion: `+4/+4` and Divine Shield) fill both. No partitioning logic in the framework — author decides which method does what; the loop just calls each.

### Q5 — Consumption: **gameplay tag write, not modifier destroy**

A hit consumes Divine Shield by writing `Set(DS, false)` directly. The modifier-entity isn't touched. Refresh leaves the transient slot alone, so consumed state persists across modifier churn.

Multi-grant case: two HoP modifiers grant DS via idempotent `OnAttach` writes (`Set(DS, true)` over already-true is a no-op). Detach one: refresh runs, transient DS untouched, consumed state survives. Detach the other: same. The "two grants, one hit, one detach" footgun from the original sketch is dissolved by the rebaseable/transient split — not by ref counting, not by per-effect idempotence.

### Q6 — One modifier-def → one entity

Original sketch's "one def → N entities" pattern (one entity per `(Slot, Op)` row) is replaced by composite effects on a single modifier-entity. Blessed Champion is one entity, one tooltip, one wire create/destroy, one source-sweep target.

Composites compose via a `CompositeEffect` that walks its steps. Framework doesn't partition — `OnAttach` composite handles transient steps; `Refresh` composite handles rebaseable steps. Author writes them as the same shape.

### Q7 — Modifier shape: **`IModifierEffect`, free-form**

```csharp
public interface IModifierEffect
{
    void Apply(Entity target, IEffectScope scope);
}
```

The effect can do anything an effect can — set a tag, add to a tag, multiply, grant a deathrattle by registering into a downstream EffectStore, spawn a child entity, anything. No `IOp<T>` typed strategy vocabulary, no row tuples. Reuses the same effect machinery cards use for `OnCast` / Deathrattle / etc.

### Q8 — Modifier storage: **global `EntityStore`, queried by tag**

Modifier-entities live in the same `EntityStore` as cards/zones/players, with `Type=Modifier` and `ModTarget` / `ModSource` tags pointing at related entities. Queries are tag-filtered walks. Indices (`(ModTarget, ModSlot)`, `ModSource`) drop in later if the spike grows; for sandbox-scale linear scan is fine.

This composes with the locked Definition × Entity shape (everything's an entity in one store; `Type` partitions catalogs and query buckets) and the SabberStone-shape immortal-entities lean from the [`entity-identity-model.md`](entity-identity-model.md) audit.

### Q9 — Order discipline: **single int per def**

Refresh replay sequence is governed by a single `Order` int per modifier definition. Convention: Add@100, Mul@200, Override@300, Conditional@400. MTG-style layer tiers (CR 613) deferred until a game pushes on it.

## The shape, in code

```csharp
public sealed record ModifierDefinition(
    int     Id,
    string  Name,
    IModifierEffect? OnAttach = null,    // one-shot at spawn; transient writes
    IModifierEffect? Refresh  = null,    // every refresh; rebaseable writes
    int     Order   = 100,
    ModifierTooltip Tooltip  = default
) : IEntityDefinition
{
    public EntityType Type => EntityTypes.Modifier;
}

public sealed record ModifierTooltip(
    string Label,
    string? Icon = null,
    string? Description = null);
```

Refresh loop — dumb, two passes:

```csharp
public static void RefreshModifiers(this GameEngine engine, Entity target)
{
    foreach (var (slot, baseValue) in target.Base)
        if (TagPolicies.RebaseOf(slot) == Rebaseable)
            target.SetSlotRaw(slot, baseValue);

    var scope = engine.Effects.NewScope();
    foreach (var m in OrderedModifiersFor(target, engine))
        engine.Catalogs.Modifiers[m.DefinitionId].Refresh?.Apply(target, scope);
}
```

Spawn — extension on `GameEngine` per the L3 pattern locked in [`definition-entity-setup.md`](definition-entity-setup.md):

```csharp
public static Entity SpawnModifier(this GameEngine engine, ModifierDefinition def,
                                    Entity target, Entity? source = null)
{
    var m = engine.From(def);                           // L2: id + Type=Modifier + DefId
    m.Set(Tags.ModTarget, target.Id);
    if (source != null) m.Set(Tags.ModSource, source.Id);

    def.OnAttach?.Apply(target, engine.Effects.NewScope());
    engine.RefreshModifiers(target);
    return m;
}

public static void DestroyModifier(this GameEngine engine, Entity modifier)
{
    var target = engine.EntityStore.Get(modifier.Get(Tags.ModTarget));
    engine.EntityStore.Destroy(modifier.Id);
    if (target != null)
        engine.RefreshModifiers(target);
}
```

Cleanup sweeps (cosmetic helpers over the same store):

```csharp
public static void RemoveModifiersFromSource(this GameEngine engine, Entity source)
{
    foreach (var m in engine.EntityStore.ModifiersFromSource(source.Id).ToList())
        engine.DestroyModifier(m);
}

public static void RemoveModifiersTargeting(this GameEngine engine, Entity target)
{
    foreach (var m in engine.EntityStore.ModifiersTargeting(target.Id).ToList())
        engine.DestroyModifier(m);
}
```

Consumption — just a tag write:

```csharp
public void OnBeforeDamage(IHookScope scope)
{
    if (target.Get(Tags.DivineShield)) {
        target.Set(Tags.DivineShield, false);
        scope.Cancel();
    }
}
```

## Worked scenarios

### Pure transient grant — Hand of Protection (DivineShield)

```csharp
new ModifierDefinition(
    Id: 901, Name: "Hand of Protection",
    OnAttach: new SetBoolEffect(Tags.DivineShield, true),
    Tooltip: new ModifierTooltip("Divine Shield"));
```

```
Base: DS=false.
Attach HoP_A: OnAttach → DS=true. Refresh: nothing rebaseable.    DS=true
Attach HoP_B: OnAttach → DS=true (no visible change).              DS=true
Hit:         gameplay Set(DS, false), skip damage.                 DS=false
Detach HoP_A: refresh runs (no rebaseable contribs to replay).     DS=false  ✓
Detach HoP_B: refresh runs. Transient DS untouched.                DS=false  ✓
Attach HoP_C: OnAttach → DS=true.                                  DS=true   ✓
```

The "two grants, one hit, one detach" case is correct by construction. Refresh doesn't re-grant because OnAttach is one-shot and Refresh doesn't write transients.

### Pure rebaseable contribution — multi-mul ordering

```csharp
new ModifierDefinition(Id: 501, Name: "+3 ATK",
    Refresh: new AddIntEffect(Tags.Attack, 3), Order: 100);

new ModifierDefinition(Id: 502, Name: "×2 ATK",
    Refresh: new MulIntEffect(Tags.Attack, 2), Order: 200);

new ModifierDefinition(Id: 503, Name: "Set ATK 1",
    Refresh: new SetIntEffect(Tags.Attack, 1), Order: 300);
```

```
Base: Attack=1.
Attach +3:           Refresh: Attack = 1 + 3 = 4.                  ATK=4
Attach ×2:           Refresh: Attack = (1 + 3) × 2 = 8.            ATK=8
Attach Set 1:        Refresh: Attack = set(1) → 1.                 ATK=1
Detach Set 1:        Refresh: Attack = (1 + 3) × 2 = 8.            ATK=8
Detach +3:           Refresh: Attack = 1 × 2 = 2.                  ATK=2   ✓
```

Refresh-from-base composes correctly across detach order regardless of operation arithmetic.

### Composite — Blessed Champion

```csharp
new ModifierDefinition(
    Id: 1003, Name: "Blessed Champion",
    OnAttach: new CompositeEffect(new IModifierEffect[] {
        new SetBoolEffect(Tags.DivineShield, true),
        new AddIntEffect(Tags.Health, 4),                  // one-shot current bump
    }),
    Refresh: new CompositeEffect(new IModifierEffect[] {
        new AddIntEffect(Tags.Attack,    4),
        new AddIntEffect(Tags.MaxHealth, 4),
    }),
    Tooltip: new ModifierTooltip("+4/+4, Divine Shield"));
```

```
Squire 1/1, base: Attack=1, MaxHealth=1, Health=1, DS=false.

Attach Blessed:
  OnAttach: DS=true, Health += 4 → 5.
  Refresh:  Attack = 1+4 = 5, MaxHealth = 1+4 = 5.
  Final: Attack=5, MaxHealth=5, Health=5, DS=true.

Take 3 damage: Health = 5-3 = 2. (Transient write, no refresh.)
  Final: 5/(2 of 5), DS=true.

Hit (DS pops): Set(DS, false), skip damage.
  Final: 5/2 of 5, DS=false.

Detach Blessed:
  Refresh: Attack=base(1), MaxHealth=base(1). Health untouched.
  Final: 1/(2 of 1), DS=false.
  (Optional game-side clamp: Health = min(2, 1) = 1 → 1/1.)
```

Health survives detach where the original sketch's fold-on-read would have wrong-answered the damage history. DS stays consumed.

### Dependent modifiers — conditional via Order

```csharp
new ModifierDefinition(Id: 1101, Name: "Iron Plating",
    Refresh: new AddIntEffect(Tags.Defense, 5), Order: 100);

new ModifierDefinition(Id: 1102, Name: "Conditional Charge",
    Refresh: new ConditionalEffect(
        cond: e => e.Get(Tags.Defense) > 5,
        thenE: new AddIntEffect(Tags.Attack, 3),
        elseE: new AddIntEffect(Tags.Attack, 1)),
    Order: 200);                                  // runs after Iron Plating
```

```
Base: Attack=1, Defense=2.
Attach Conditional alone:
  Refresh: Defense=2, Attack: 2 > 5 false → +1 → Attack=2.

Attach Iron Plating alongside:
  Refresh: Order 100 → Defense=7. Order 200 → 7 > 5 → Attack +=3 → 4.
  Final: Attack=4, Defense=7.

Detach Iron Plating:
  Refresh: Defense=base(2). Conditional: 2 > 5 false → Attack +=1 → 2.
  Final: Attack=2, Defense=2.
```

Cross-modifier dependency works without any "depends-on" wiring — refresh re-evaluates everything every time, and `Order` ensures Defense settles before the conditional reads it.

## Pending / parked

These were design-pass topics that resolved to "punt until first implementation forces the call." Marked **temporary direction** below; revisit after sandbox + first usage.

- **Tag-policy registry shape.** Side-car registry (`TagPolicies.Set(slot, ...)`) vs field on `TagKey` vs def-attached. **Temporary**: side-car-registry shape; lean-revisit.
- **Where modifiers are stored.** Q8 locks "global `EntityStore` with `ModTarget`/`ModSource` tags" as the temporary direction, but the indexing pattern (linear scan vs `(ModTarget, ModSlot)` index vs target-side ref-list cache) is parked until query-perf surfaces.
- **`OnDetach` hook.** Not in base shape. Most modifiers don't need it (refresh handles cleanup for rebaseable; transient stays where gameplay left it). Add per-mod if a real case forces it.
- **C/M slot pairing with auto-clamp** (Health ≤ MaxHealth). Game-side responsibility for now; framework doesn't ship the clamp pass. Revisit if many games end up writing the same clamp boilerplate.
- **Cross-entity dependencies / auras** (Stormwind: +1/+1 to other minions while on battlefield). Needs a hook-on-tag-change + refresh-targets mechanism. Spawn auras with `OnAttach` writing to other entities works for the static case; dynamic "minion enters board" needs a separate hook layer. Defer.
- **Tooltip wire format.** Data shape is clear (`ModifierTooltip` struct); wire emission falls out of existing entity-wire when wired.
- **MTG-style ordering tiers** (CR 613 7-layer system). Single int sufficient for now.
- **"Card IS a modifier"** (MTG enchantments / auras / equipment as cards that attach modifier-entities on resolve). Compositional with the locked shape; nothing exercises it yet.

## How this descends from the parked sketch

The 2026-05-16 sketch (now [`archived`](archive/modifiers-as-entities-sketch.md)) had the broad "modifiers are entities" frame right but locked the wrong mechanics. The 2026-05-18 design pass walked through each major decision; the diff:

| Topic | 2026-05-16 sketch | This spike |
|---|---|---|
| Read path | Fold-on-read (`Effective.Read<T>(e, key, store)`) | Plain dict lookup (`e.Get(key)`); server pre-folds |
| Effect shape | Typed `IOp<T>` strategy objects in `ModOp` slot | Free-form `IModifierEffect` (anything an effect can do) |
| Entity-per-def | One def → N modifier-entities (one per `(Slot, Op)` row) | One def → one entity; composite effects collapse rows |
| Per-tag policy | Implicit, uniform fold for every slot | Explicit `Rebaseable \| Transient` policy; refresh respects it |
| Effect timing | Single effect surface | `OnAttach` (one-shot, transient writes) + `Refresh` (replayed, rebaseable writes) |
| Consumption | Source-sweep destroy (`RemoveModifiersFromSource(srcId)`) | Gameplay tag write (`Set(DS, false)`); sweep helpers remain for explicit "remove from source" semantics |
| Multi-grant DS | Stacking via multiple modifier-entities (ref-count semantics) | Idempotent `OnAttach` writes (same tag, no change); transient persistence handles consumption correctly |
| Ordering | `ModOrder` ascending, FIFO ties (single int) | Same: single `Order` int per def, FIFO ties |

Two of the sketch's broad locks survive intact: **modifiers are first-class entities** (same `Entity` class, `Type=Modifier`, queried alongside cards/zones/players) and **catalog-per-type** (`CatalogOf<ModifierDefinition>` slots in alongside `CatalogOf<CardDefinition>`).

## How to validate (forthcoming)

Sandbox at `Assets/Spike/ECS/Modifiers/` — not yet built. Will extend the [`EntitySetup`](definition-entity-setup.md) scene with a Modifiers group in the IMGUI panel. Scenarios to exercise:

1. Argent Squire (intrinsic DS) + Sword of Vigor (+3 ATK) + Battle Cry (×2 ATK) + Aldor Peacekeeper (set 1) — full multi-mod refresh ordering across detach order permutations.
2. Bloodfen Raptor + HoP_A + HoP_B → hit → detach in various orders. Validates "two grants, one hit, one detach" stays correct (DS stays consumed).
3. Blessed Champion on Squire — composite `OnAttach` + `Refresh`. Validates Health one-shot bump survives detach + Attack reverts cleanly.
4. Conditional dependency: "+3 if Defense > 5" alongside "+5 Defense" — validates inter-modifier dependency resolution via `Order`.

Each scenario displays the target entity's current tag bag side-by-side with the modifier-entities targeting it (def name, source, tooltip). Reset rebuilds the engine against the same catalogs — content survives, match state restarts (same pattern as the EntitySetup sandbox).

## Follow-ups visible from this spike

- **Tag-policy registry shape decision** — review after sandbox + first usage.
- **Storage indexing decision** — review when query-perf surfaces (likely production-time, not spike-time).
- **Modifier-entity wire shape** — falls out of existing entity-wire when wired; no new event kinds expected.
- **Effect-data-asset shape** (parked together with modifiers in [`definition-entity-setup.md`](definition-entity-setup.md) Q5/Q6). Modifier effects and card effects share the same shape question; co-design.
- **Cross-entity aura mechanism** (hook-on-tag-change + refresh-targets). Separate spike pass; can land independently.
