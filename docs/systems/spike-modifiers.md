# Modifiers (deleted spike)

A standalone sandbox for stat modifiers stored as entities and folded into tags by recomputing from base values.

**Code:** deleted. Last version at commit `b13907e`, [`Assets/Spike/ECS/Modifiers/`](https://github.com/MgCohen/Card-Framework/tree/b13907e/Assets/Spike/ECS/Modifiers). Built May 2026.

## What it does
- Stores each modifier as an `Entity` with `Type = Modifier`, a `DefinitionId`, and `ModTarget` / `ModSource` tags. It lives in the same `EntityStore` as cards.
- Pushes results into the target's tags. Readers call `e.Get(Tags.Attack)` and never fold.
- Splits slots into `Rebaseable` (Attack, Cost, MaxHealth, Defense) and `Transient` (Health, DivineShield, everything else).
- Splits modifier effects into `OnAttach` (runs once, writes transient slots) and `Refresh` (replayed on every refresh, writes rebaseable slots).
- Orders replay by one `Order` int per definition, with entity id as the tie-break.
- Proves four scenarios: two Hand of Protection grants and one hit, `+3` / `x2` / `set 1` ordering, the Blessed Champion composite, and a conditional that depends on Defense. `ModifiersHeadlessValidation` checks 38 assertions from an editor menu. There are no NUnit tests.

## How it works
1. `SpawnCard` writes rebaseable stats with `SetBase` (current and base) and transient stats with `Set`.
2. `SpawnModifier(def, target)` creates the modifier entity and tags `ModTarget`. It runs `def.OnAttach` once on the target, then calls `RefreshModifiers(target)`.
3. `RefreshModifiers` resets every slot that has a base value and a `Rebaseable` policy. Then it runs `def.Refresh` for each modifier targeting the entity, sorted by `Order`, then id.
4. `DestroyModifier` removes the entity and refreshes the old target. Transient writes such as a consumed Divine Shield survive.
5. Gameplay (damage, a hit that pops Divine Shield) writes transient tags directly and never touches modifiers.

## Key types
| Type | Job |
|---|---|
| `ModifierDefinition` | Catalog record: `OnAttach`, `Refresh`, `Order` (default 100), `Tooltip`. |
| `IModifierEffect` | `Apply(Entity target, IEffectScope scope)`. Free-form effect. |
| `SetIntEffect` / `AddIntEffect` / `MulIntEffect` / `SetBoolEffect` | Primitive effects on one tag. |
| `CompositeEffect` / `ConditionalEffect` | Run steps in order; branch on a predicate over the target. |
| `ModifierEngine` | Extension methods: `SpawnModifier`, `DestroyModifier`, `RefreshModifiers`, `OrderedModifiersFor`, source and target sweeps. |
| `TagPolicies` | Static side-car map from tag slot to `RebasePolicy`. Unknown slots are `Transient`. |
| `Entity` | Id, `Type`, `DefinitionId`, plus a current slot bag and a base slot bag. |
| `EntityStore` | Entity dictionary with linear `ModifiersTargeting` and `ModifiersFromSource` queries. |
| `CatalogOf<TDef>` | Definitions by id. `Catalogs` holds cards and modifiers. |

## Shapes worth keeping
Refresh is reset-to-base plus ordered replay. Removing `+3` under `x2` gives `1*2 = 2`, which delta undo cannot.
```csharp
public static void RefreshModifiers(this GameEngine engine, Entity target)
{
    foreach (var (slot, baseValue) in target.Base)
    {
        if (TagPolicies.RebaseOf(slot) == RebasePolicy.Rebaseable)
            target.SetSlotRaw(slot, baseValue);
    }
    var scope = engine.Effects.NewScope();
    foreach (var m in engine.OrderedModifiersFor(target))
    {
        if (engine.Catalogs.Modifiers.TryGet(m.DefinitionId, out var def))
            def.Refresh?.Apply(target, scope);
    }
}
```
One definition covers both halves of a composite buff, so it stays one entity with one tooltip.
```csharp
c.Modifiers.Add(new ModifierDefinition(
    MOD_BLESSED, "Blessed Champion",
    OnAttach: new CompositeEffect(
        new SetBoolEffect(Tags.DivineShield, true),
        new AddIntEffect (Tags.Health,       4)),
    Refresh:  new CompositeEffect(
        new AddIntEffect(Tags.Attack,    4),
        new AddIntEffect(Tags.MaxHealth, 4)),
    Order:    150,
    Tooltip:  new ModifierTooltip("+4/+4, Divine Shield")));
```

## Relation to the Framework
- Informs a future Framework modifiers feature. None exists yet. It already has `EntityStore`, `EntityType`, `Catalog<TDef>` and an unmanaged `TagStore`.
- Every write in `OnAttach` and `Refresh` must go through `TagService`, so it becomes a `TagSetEvent` that the apply handler writes. Reset-then-replay would emit a burst of intermediate values. Fold into a scratch buffer and emit only the final changed tags.
- Base values need a home the client can rebuild, such as a base tag store fed by its own events, or a read from the card definition.
- `IModifierEffect` must take an entity id and services, not a mutable `Entity`. `TagPolicies` should become per-engine or a property of the `Tag` key. Detach needs an entity-destroy event, which the Framework does not have (only `EntityCreatedEvent`).

## Limits
- No wire, events or client. All writes are direct dictionary mutations on a server-side object.
- Refresh runs only on spawn and destroy of a modifier. A change in a dependency (for example a gameplay write to Defense) does not trigger a refresh. Auras across entities are not built.
- `ConditionalEffect` takes a C# lambda, so definitions are code, not data. `IEffectScope` is an empty stub.
- Queries are linear scans. Tag slots are hand-numbered, and values are boxed `object`.
