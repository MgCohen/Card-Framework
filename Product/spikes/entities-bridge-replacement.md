---
type: spike
status: draft
tags: [#spike, #entities, #scaffold, #variables, #modifiers]
---

# Entities-Bridge Replacement Sketch

> **Status: draft / exploratory.** Sketches captured during the `oq-game-definition` domain modeling pass when the existing `Scaffold.Entities` + `Scaffold.Entities.States` packages started feeling like they were fighting the immutable / redux-shaped direction the framework is settling into. **Not approved, not denied** — captured for later iteration. No doc patches based on this spike yet.

## Why this came up

While walking the per-kind domain modeling for `oq-game-definition`, the slice-provider mechanism (Q1 lock) raised a hierarchy question:

> "The base definition we were using was `EntityDefinition` from the entity Scaffold package."

Pulling on that thread surfaced a deeper question: is the `com.scaffold.entities` + `com.scaffold.entities.states` bridge adding value commensurate with its cost, given the framework's redux-shaped direction? Or is it producing more noise than benefit — fighting the immutable model with a mutable-shaped API surface?

This spike captures the research findings, the proposed replacement shape, and the honest tradeoff read.

## What the bridge + entities packages provide today

From `entities/02-runtime-surface.md` and `scaffold-state-fit.md`:

### `com.scaffold.entities` (base package)

- **Value types.** `Variable` (Key + `PayloadTypeId` string), `VariableValue` abstract base + concrete subclasses (`IntVariableValue`, `FloatVariableValue`, …) tagged with `[VariableValueId("…")]` for serialization, `VariableModifier<T>` abstract + ~6 concrete modifiers (`IntAddModifier`, `IntMultiplyModifier`, `BoolOverrideModifier`, …), `EntityModifierEntry` pre-built (key, modifier) pair.
- **Definition contract.** `IEntityDefinition` interface (`DefinedVariables`, `TryGetDefaultValue`); `EntityDefinition` concrete = **`ScriptableObject` with `[SerializeField]` `VariableBag` + Inspector authoring + `VariableSO` asset references**.
- **Handle abstraction.** `IReadOnlyEntity<TDef>` (`GetVariable<T>`, `Subscribe`, `SubscribeToVariableStructuralChanges`); `IMutableEntity<TDef>` (`AddModifier`, `AddVariable`, `RemoveVariable`, `RemoveModifier`, `ClearModifiers`).

### `com.scaffold.entities.states` (bridge)

- **Slice.** `EntityVariableState` (`BaseValues` dict + `ModifierStacks` dict), keyed by `InstanceId`.
- **Handle implementation.** `StateEntity<TDef> : BaseEntityInstance<TDef>, IMutableEntity<TDef>, IDisposable`.
- **Internal storage.** `StoreVariableStorage` — per-instance effective-value cache, per-variable subscription mux.
- **Factory.** `EntityStateFactory.Create(...)`.
- **6 mutation payloads.** `AddModifierPayload`, `RemoveModifierPayload`, `RemoveModifiersBySourcePayload`, `SetBaseValuePayload`, `AddEntityVariablePayload`, `RemoveEntityVariablePayload`.
- **Modifier id factory.** `IModifierFactory` for deterministic `ModifierId` generation.

### Engine plumbing layered on top

- `IEntityRegistry` — `Spawn / Get / TryGet / Has / Destroy`.

## Cost / value audit

| Provides | Real value? |
|---|---|
| Variable / VariableValue / Modifier types | Yes — but Unity-coupled and tag-driven (`[VariableValueId]`, `PayloadTypeId` strings). We could rebuild ~20 lines, type-safe via generics. |
| `EntityDefinition` (ScriptableObject) | **Net-negative for simulation core.** Drags Unity into core; `scaffold-state-fit.md` already flags this as a known concern. |
| `IReadOnlyEntity` / `IMutableEntity` polymorphism | **Dead weight.** Designed for "authoring instance vs state-backed" interchangeability; framework has no plain-`EntityInstance` consumer. |
| `IMutableEntity.AddModifier` etc. | **Friction tax.** Reads as mutation; secretly dispatches a payload. Half the mutation API (`SetBaseValue`, `RemoveModifiersBySource`, batches) drops to raw payloads anyway — dual surface. |
| `EntityVariableState` slice + immutable storage | Yes — but it's a ~50-line record we own anyway. Bridge wraps it; we could just expose it directly. |
| Per-rebuild effective-value cache (`StoreVariableStorage`) | Maybe-yes. Compute-on-read is cheap for small modifier stacks; cache adds invalidation complexity that compounds. Defer until profiling forces it. |
| Per-variable subscriptions (`Subscribe(key, …)`) | Maybe-yes. Replaceable with slice-level subscriptions + consumer-side diff. Less ergonomic for "watch one variable," but consistent with the rest of the redux model. |
| `IEntityRegistry` Spawn/Get/Destroy | Yes — but "spawn" is `store.Execute(CreateEntityPayload)`, "get" is `state.Get<EntityVariableState>(id)`, "destroy" is a payload. Doesn't need a registry abstraction. |
| `EntityModifierEntry` pre-built pairs | Lightweight — easy to keep our own. |
| `IModifierFactory` deterministic id | Yes — but ~10 lines of "engine counter, snapshot it." Ours. |

## Replacement sketch — pure-framework, immutable, redux-native

### Value types

```csharp
// Type-safe variable key — generic on T at the type level.
public readonly record struct Variable<T>(string Key);

// Modifier — typed.
public abstract record Modifier<T>
{
    public int Order { get; init; }
    public abstract T Apply(T current);
}

public sealed record AddInt    (int Amount) : Modifier<int> { public override int Apply(int c) => c + Amount; }
public sealed record MulInt    (int Factor) : Modifier<int> { public override int Apply(int c) => c * Factor; }
public sealed record Override<T>(T Value)   : Modifier<T>   { public override T   Apply(T _) => Value; }

// Stored modifier — id, target variable key, modifier (boxed), source attribution.
public sealed record ActiveModifier(
    ModifierId      Id,
    string          VariableKey,
    object          Modifier,
    int             Order,
    ModifierSource? Source = null);

public readonly record struct ModifierId(Guid Value)
{
    public static ModifierId New() => new(Guid.NewGuid());
}

public readonly record struct ModifierSource(InstanceId Source, int Tag = 0);
```

### Slice

```csharp
public sealed record EntityVariableState(
    InstanceId                          Owner,
    ImmutableDictionary<string, object> BaseValues,
    ImmutableList<ActiveModifier>       Modifiers) : BaseState
{
    public T GetEffective<T>(Variable<T> v, EntityDefinition def)
    {
        var baseVal = BaseValues.TryGetValue(v.Key, out var bv)
            ? (T)bv
            : def.GetDefault(v);

        return Modifiers
            .Where(m => m.VariableKey == v.Key)
            .OrderBy(m => m.Order)
            .Aggregate(baseVal, (cur, m) => ((Modifier<T>)m.Modifier).Apply(cur));
    }
}
```

### Mutation payloads

```csharp
public sealed record AddModifierPayload            (InstanceId Owner, ActiveModifier Modifier);
public sealed record RemoveModifierPayload         (InstanceId Owner, ModifierId Id);
public sealed record RemoveModifiersBySourcePayload(InstanceId Owner, ModifierSource Source);
public sealed record SetBaseValuePayload<T>        (InstanceId Owner, Variable<T> Variable, T Value);
public sealed record AddBaseValuePayload<T>        (InstanceId Owner, Variable<T> Variable, T InitialValue);
public sealed record RemoveBaseValuePayload        (InstanceId Owner, string VariableKey);
```

### Definition base (Unity-free)

```csharp
public abstract class EntityDefinition
{
    public DefinitionId Id   { get; init; }
    public string       Name { get; init; }

    public abstract bool TryGetDefault<T>(Variable<T> v, out T value);
    public T GetDefault<T>(Variable<T> v) =>
        TryGetDefault(v, out var x)
            ? x
            : throw new KeyNotFoundException(v.Key);
}

public sealed class CardDefinition : EntityDefinition
{
    public string Title       { get; init; }
    public string ArtAssetId  { get; init; }
    public int    BaseCost    { get; init; }
    public int    BaseDamage  { get; init; }

    public override bool TryGetDefault<T>(Variable<T> v, out T value)
    {
        if (v.Key == Vars.Cost.Key)   { value = (T)(object)BaseCost;   return true; }
        if (v.Key == Vars.Damage.Key) { value = (T)(object)BaseDamage; return true; }
        value = default!;
        return false;
    }
}
```

### Reads at the call site

```csharp
// Direct slice read (no handle, no cache).
int damage = state.Get<EntityVariableState>(card.InstanceId)
                  .GetEffective(Vars.Damage, card.Definition);

// Sugar via extension method (still no handle in flight).
public static T GetEffective<T>(this Card c, IStateScope s, Variable<T> v)
    => s.Get<EntityVariableState>(c.InstanceId).GetEffective(v, c.Definition);

// usage:
int damage = card.GetEffective(state, Vars.Damage);
```

### Writes at the call site

```csharp
// Single-payload mutation.
store.Execute(new AddModifierPayload(
    card.InstanceId,
    new ActiveModifier(
        Id:          ModifierId.New(),
        VariableKey: Vars.Damage.Key,
        Modifier:    new AddInt(3) { Order = 100 },
        Order:       100,
        Source:      new ModifierSource(sword.InstanceId))));

// Source-bulk removal.
store.Execute(new RemoveModifiersBySourcePayload(
    card.InstanceId,
    new ModifierSource(sword.InstanceId)));

// Atomic batch.
store.ExecuteBatch(new object[]
{
    new AddModifierPayload(target.InstanceId, atkBuff),
    new AddModifierPayload(target.InstanceId, defBuff),
    new IncrementTurnCounterPayload(),  // game-authored payload on a different slice
});
```

### Total framework code

Roughly 200-300 lines for the variables/modifiers primitive — fully owned, immutable-first, redux-native, no Unity coupling in the simulation core.

## What we'd lose

- **Per-variable effective-value subscriptions** — `entity.Subscribe(Vars.Attack, …)`. Replacement: subscribe to the slice (`store.Subscribe<EntityVariableState>(instanceId, …)`); consumers diff effective values themselves. Less ergonomic for "watch one variable."
- **Per-rebuild effective-value cache.** Replacement: compute-on-read. Add memoization later if profiling shows need.
- **Auto-coalesced structural-change events.** Replacement: same as the per-variable case — slice subscription + diff.
- **Scaffold ScriptableObject authoring infrastructure.** Replacement: code-defined definitions until `bl-authoring-feature` lands; then the eventual authoring layer ScriptableObject-wraps pure-data definitions cleanly.
- **Built-in modifier types** (`IntAddModifier`, etc.). Replacement: ship a small set ourselves. ~20 lines.

## What we'd gain

- **Unity-free simulation core.** `EntityDefinition` no longer ScriptableObject. Authoring layer can use Unity (ScriptableObject wrappers); core can be Unity-free. Resolves the open issue called out in `scaffold-state-fit.md`.
- **Single mutation surface.** `Store.Execute(payload)`. No dual surface. No `IMutableEntity` to learn alongside the raw-payload fallback.
- **No handle abstraction.** Cards, zones, players are slice keys. Read-only by default; mutation is "here's a payload, dispatch it."
- **Type-safe variable keys.** `Variable<int>` instead of `Variable + IVariableValue<int>`. Compile-time mismatch errors instead of runtime tag drift.
- **Smaller surface to learn.** Glossary loses ~10 entries (`StateEntity`, `IReadOnlyEntity`, `IMutableEntity`, `IEntityRegistry`, `IModifierFactory`, `EntityModifierEntry`, `VariableSO`, `StoreVariableStorage`, `BaseEntityInstance`, etc.).
- **Aligns with locked Q1.** `ISliceProvider<TInstance>` works directly with raw slices; we don't need handles.

## Honest recommendation (not a verdict — just my read)

**Lean: drop the entities + entities.states packages. Keep the states package. Build the variables/modifiers primitive ourselves.**

Reasons in order of weight:

1. **Unity coupling is a real architectural problem.** The spike `scaffold-state-fit.md` already flagged it. Custom variables = no Unity in the simulation core; authoring layer can wrap with Unity later.
2. **Mutable-shaped surface over immutable storage is friction tax compounded daily.** Every contributor learns "AddModifier is actually dispatch a payload." With native redux shape, the cognitive model is uniform.
3. **Polymorphic abstraction has no consumer.** `IReadOnlyEntity` polymorphism for "authoring vs state-backed" — framework has one consumer (state-backed). Dead weight.
4. **Pre-implementation timing.** No code on the entities packages today. This is the cheapest moment to make the cut.
5. **Replacement is small and bounded.** ~200-300 lines, ~5 framework types. Resistance to accretion is the only required discipline.

## Risks / things to think about

- **Custom modifier systems can grow into bespoke complexity.** Real risk. Mitigation: ship the v1 surface tiny — `Variable<T>`, `Modifier<T>`, `EntityVariableState`, four payloads. Don't pre-build composition handlers, multi-source policies, percentage-of-base modifiers, etc. until forced.
- **Authoring story.** Today, ScriptableObject auth comes for free. With custom `EntityDefinition`, authoring is "code-defined" until `bl-authoring-feature` lands. Acceptable for now — but it's a real near-term ergonomic regression for manual card-data entry.
- **Doc cascade is large.** `entities/01`, `entities/02`, `cards/01`, glossary, pile-counter, `effects/04` — all reference bridge surface heavily. Migration is real work, but the `oq-game-definition` cascade already touches these.
- **Source attribution + modifier ordering.** Bridge has `(Source, Tag)` cleanup semantics + ascending-Order FIFO ties. Replacement preserves both — handful of lines.
- **`AddBaseValue` discipline.** The bridge has subtle rules — `WithVariable` is a no-op when the key already exists in `BaseValues`; definition-declared variables stay as definition defaults until written. Replacement has to either preserve this rule or document why it diverges.
- **Boxed `object` storage in `BaseValues` and `ActiveModifier.Modifier`.** The replacement uses `object` for runtime polymorphism (since the slice can't be generic on each variable's type). This is a small allocation cost; profiling against the bridge's typed dictionary is worth doing if performance matters.
- **Subscriber ergonomics regression.** Bridge gives "subscribe to one variable's effective value" for free. Replacement requires consumer-side diffing. For presentation views watching a single stat, this is real friction. May need to add back a thin `EffectiveValueSubscription<T>` helper later — additive, no architectural cost.

## Sequencing

If this lands as a real decision, it should be its own design pass — call it `oq-drop-entities-bridge` or similar. Resolve **before** resuming `oq-game-definition` Stages 2-3, because it reshapes:

- What `CardDefinition` extends (no longer Scaffold's `EntityDefinition`).
- What "card slices" means (`EntityVariableState` becomes our own type).
- The Definition hierarchy question that surfaced during the domain modeling pass (`Definition` base, common parent or not).

Once that lands, the domain modeling pass and `oq-game-definition` Stages 2-3 build on a clean foundation.

## Open questions for later iteration

- Does `Variable<T>` carry serialization metadata (currently the bridge uses `PayloadTypeId` strings for the wire form), or do we punt that to `bl-authoring-feature`?
- How does the engine generate deterministic `ModifierId`s without an `IModifierFactory` injection point — does it become a payload-construction concern or stay engine-allocated at dispatch?
- Should the replacement preserve the "definition-declared variables vs ad-hoc instance variables" distinction the bridge enforces (`AddEntityVariablePayload` only for non-definition keys), or treat all variables uniformly?
- If we keep `IModifierFactory` (or equivalent) for deterministic ids, where does it live in the new surface — `IHandlerContext`, an engine service, both?

## Related

- [`Product/spikes/scaffold-state-fit.md`](spikes/scaffold-state-fit.md) — original Scaffold-package mapping spike that locked in the bridge direction.
- [`Product/features/entities/models/01-definition-instance-model.md`](features/entities/models/01-definition-instance-model.md) — current entity / definition / instance model, references the bridge.
- [`Product/features/entities/models/02-runtime-surface.md`](features/entities/models/02-runtime-surface.md) — current runtime surface; the doc most affected if we drop the bridge.
- [`Product/features/cards/models/01-card-model.md`](features/cards/models/01-card-model.md) — `Card : StateEntity<CardDefinition>` inherits the bridge; would change.
- [`Product/SESSION-CHECKPOINT.md`](SESSION-CHECKPOINT.md) — where the `oq-game-definition` design pass is suspended; this spike is upstream of resuming it.
