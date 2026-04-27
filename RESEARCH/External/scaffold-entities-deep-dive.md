# com.scaffold.entities — Deep Dive

**Purpose:** Source-level analysis of the Scaffold Entities package as a candidate for the Card Framework definition/instance model, variable system, and modifier resolution. Companion to the States deep dive — the two must be read together because they overlap significantly.

**Source read:** 2026-04-27. Repo: [https://github.com/MgCohen/Scaffold](https://github.com/MgCohen/Scaffold) under `Assets/Packages/com.scaffold.entities/`.

**Related framework docs:** [04-scaffold-package-fit.md](../04-scaffold-package-fit.md) (high-level fit table), [10-entity-definition-instance-model.md](../10-entity-definition-instance-model.md) (framework entity model), [09-core-glossary.md](../09-core-glossary.md) (Variable, Modifier, Definition, Instance, EffectiveValue terms).

**Also read:** [scaffold-states-deep-dive.md](scaffold-states-deep-dive.md) — open question 7 there directly concerns the overlap between these two packages.

---

## 1. Package Structure

```
com.scaffold.entities/
├── Runtime/
│   ├── Core/
│   │   ├── Variables/
│   │   │   ├── Values/
│   │   │   │   ├── FloatVariableValue.cs
│   │   │   │   ├── IntVariableValue.cs
│   │   │   │   ├── BoolVariableValue.cs
│   │   │   │   └── StringVariableValue.cs
│   │   │   ├── Variable.cs           ← key record(string Key, VariableValueType Type)
│   │   │   ├── VariableValue.cs      ← abstract base with Combine()
│   │   │   ├── VariableSO.cs         ← ScriptableObject key for authoring
│   │   │   ├── VariableEntry.cs      ← (VariableSO, VariableValue) pair
│   │   │   └── VariableValueType.cs  ← enum: String, Int, Float, Bool
│   │   ├── VariableBags/
│   │   │   ├── VariableBag.cs        ← chained key-value bag with parent lookup
│   │   │   └── IVariableBag.cs
│   │   ├── EntityDefinition.cs       ← ScriptableObject base for authored definitions
│   │   ├── EntityInstance.cs         ← runtime instance (plain C# class)
│   │   ├── EntityComponent.cs        ← MonoBehaviour variant
│   │   ├── EntityInstanceCreator.cs  ← factory with ID generation
│   │   ├── InstanceId.cs             ← record InstanceId(int Id)
│   │   ├── IReadOnlyEntity.cs        ← read + subscribe interface
│   │   ├── IEntity.cs                ← adds AddVariable / RemoveVariable
│   │   ├── IInstance.cs              ← adds AddModifier / RemoveModifier / ClearModifiers
│   │   ├── EntityModifierEntry.cs    ← (Variable key, VariableValue modifierValue) pair
│   │   ├── VariableModifierHandler.cs ← internal modifier accumulation and Combine
│   │   └── VariableNotifier.cs       ← internal per-variable subscription dispatcher
│   └── Behavior/
│       ├── IEntityBehavior.cs
│       ├── EntityBehaviorRunner.cs
│       └── IEntityFrameInputProvider.cs
├── Editor/                            ← inspector drawers, authoring tools
├── Tests/
└── package.json
```

**Dependencies:** `UnityEngine` (hard — ScriptableObject, MonoBehaviour, SerializeField throughout).

---

## 2. Core Types — Source

### EntityDefinition (authored base data)

```csharp
public class EntityDefinition : ScriptableObject
{
    [SerializeField] private VariableBag bag = new VariableBag();
    internal VariableBag Bag => bag;
    internal IReadOnlyList<VariableEntry> Entries => bag.Entries;

    public bool TryGetBaseValue(Variable key, out VariableValue value) =>
        bag.TryGetBase(key, out value);

    public void AddVariable(VariableSO variable, VariableValue defaultValue) { ... }
    internal void RebuildLookup() => bag.RebuildCache();
}
```

`EntityDefinition` is a `ScriptableObject` — a Unity editor asset. Carries the authoritative base variable values. Has no runtime behavior of its own.

### Variable (key type)

```csharp
public record Variable(string Key, VariableValueType Type = VariableValueType.String);
```

Used as a dictionary key. String-keyed, typed. The `VariableSO` is a `ScriptableObject` wrapper for authoring-time reference (stable GUID, inspector drag-drop); at runtime, `Variable` (the record) is the actual key.

### VariableValue (modifier-aware value base)

```csharp
[Serializable]
public abstract class VariableValue
{
    public abstract VariableValueType Type { get; }
    public abstract VariableValue Combine(IReadOnlyList<VariableValue> contributions);
}
```

The `Combine` method is how modifier stacking works — base value receives a list of modifier contributions and returns the effective value. Each concrete type (`FloatVariableValue`, etc.) implements its own combination logic (additive, multiplicative, override, clamp, etc.).

### InstanceId

```csharp
public record InstanceId(int Id);
```

Simple int-wrapping record. Not the same type as the framework's `Identifier<T>` pattern — needs reconciliation.

### Three-Bag Chain (the resolution model)

Every `EntityInstance<TDefinition>` has three bags wired as a parent chain:

```
definitionBag (from EntityDefinition)
    ↑ parent
instanceBaseBag    ← runtime additions / base overrides; fires OnVariableAdded/Removed
    ↑ parent
instanceEffectiveBag  ← post-modifier values (silent writes, no events); read target
```

`TryResolve(key)` calls `instanceEffectiveBag.TryGetBase(key)`, which walks the parent chain automatically. The effective bag only has an entry when at least one modifier is active — otherwise the chain falls through to the base bag, which falls through to the definition bag.

### VariableBag

```csharp
public sealed class VariableBag : IVariableBag
{
    [NonSerialized] private IVariableBag parent;
    [SerializeField] private List<VariableEntry> entries;
    [NonSerialized] private Dictionary<Variable, VariableValue> localCache;

    public bool TryGetBase(Variable key, out VariableValue value)
    {
        EnsureCache();
        if (localCache.TryGetValue(key, out value)) return true;
        return parent != null && parent.TryGetBase(key, out value);
    }

    // Internal silent writes (no event) for effective bag
    internal void SetLocalSilent(Variable key, VariableValue value) { ... }
    internal bool RemoveLocalSilent(Variable key) { ... }

    // Public writes fire events
    public bool Add(Variable key, VariableValue initialBase) { /* fires OnVariableAdded */ }
    public bool Remove(Variable key) { /* fires OnVariableRemoved */ }
}
```

The cache is a `Dictionary<Variable, VariableValue>` with lazy initialization. Serialized entries (from the ScriptableObject) are loaded via `RebuildCache()`.

### EntityModifierEntry

```csharp
[Serializable]
public sealed class EntityModifierEntry
{
    public Variable Key => variableKey ?? (Variable)variable;  // runtime or authored key
    public VariableValue ModifierValue => modifierValue;

    [SerializeField] private VariableSO variable;     // authoring-time reference
    [NonSerialized] private Variable? variableKey;    // runtime-only key (no SO)
    [SerializeReference] private VariableValue modifierValue;
}
```

Supports both authoring-time (VariableSO reference) and runtime-only (Variable record) key paths.

### VariableModifierHandler (modifier accumulation and Combine)

```csharp
internal sealed class VariableModifierHandler
{
    private readonly Dictionary<Variable, List<EntityModifierEntry>> modifiersByVariable;

    internal VariableValue GetEffective(Variable key, VariableValue baseValue)
    {
        if (!modifiersByVariable.TryGetValue(key, out var bucket) || bucket.Count == 0)
            return baseValue;

        FillScratch(bucket);
        return scratch.Count == 0 ? baseValue : baseValue.Combine(scratch);
    }
}
```

After any `AddModifier` / `RemoveModifier`, `EntityInstance` calls `RecalculateAndNotify(key)`:

```csharp
private void RecalculateAndNotify(Variable key)
{
    if (!instanceBaseBag.TryGetBase(key, out VariableValue baseValue)) return;
    VariableValue effective = modifierHandler.GetEffective(key, baseValue);

    if (modifierHandler.HasModifiersFor(key))
        instanceEffectiveBag.SetLocalSilent(key, effective);   // silent write to effective bag
    else
        instanceEffectiveBag.RemoveLocalSilent(key);

    notifier.Notify(key, effective);                            // fires all subscribers immediately
}
```

This is **immediate and imperative** — no staging, no commit phase.

---

## 3. Interface Hierarchy

```
IReadOnlyEntity<TDefinition>   GetValue, TryGetValue, GetVariable, Subscribe, Unsubscribe,
                               SubscribeToVariableAdded, SubscribeToVariableRemoved, Id
        ↑
IEntity<TDefinition>           + AddVariable, RemoveVariable
        ↑
IInstance<TDefinition>         + AddModifier, RemoveModifier, ClearModifiers
```

`EntityInstance<TDefinition>` implements `IInstance`. `EntityComponent<TDefinition>` (MonoBehaviour) also implements it. Both share the same bag/modifier/notifier internals.

### EntityInstanceCreator

```csharp
public sealed class EntityInstanceCreator<TDefinition> where TDefinition : EntityDefinition
{
    public EntityInstance<TDefinition> Create(TDefinition definition) { ... }
    public TEntity CreateOnGameObject<TEntity>(GameObject go, TDefinition def) { ... }
}
```

Takes an `IInstanceIdGenerator`; default is `IncrementingInstanceIdGenerator` (int counter).

---

## 4. Alignment With Framework Design

| Framework Concept | Scaffold Entities Mechanism | Notes |
|---|---|---|
| `Definition` (authored base) | `EntityDefinition : ScriptableObject` | Unity-coupled; must be a Unity asset |
| `Instance` (runtime entity) | `EntityInstance<TDefinition>` | Plain C# class; acceptable for simulation |
| `InstanceId` | `record InstanceId(int Id)` | Conflicts with framework `Identifier<T>` pattern |
| `Variable` key | `record Variable(string Key, Type)` | Strong match — string-keyed, typed |
| Base variable value | `VariableEntry` in `definitionBag` | Definition bag = source of base values |
| Runtime variable override | `instanceBaseBag.Add(key, value)` | Instance-level base override |
| `Modifier` | `EntityModifierEntry` + `VariableModifierHandler` | Matches concept; apply is immediate |
| `EffectiveValue` | `instanceEffectiveBag` + `VariableValue.Combine` | Three-bag chain resolution |
| Per-variable subscription | `IReadOnlyEntity.Subscribe(key, onChange)` | Immediate callback on register; good match |
| `StateChange` HistoryEntry | ❌ None — fires `VariableNotifier` directly | Adapter needed to translate to history |
| Transactional scope | ❌ None — `AddModifier` is immediate | Breaks `ResolutionScope` all-or-nothing |
| `StateMutation` pipeline | ❌ Not present | Modifier changes bypass the store pipeline |

---

## 5. Unity Coupling — The Hard Boundary

**Everything in Entities has some Unity dependency:**

| File | Unity Coupling |
|---|---|
| `EntityDefinition` | `ScriptableObject` — must be a Unity asset |
| `EntityComponent<T>` | `MonoBehaviour` — must be on a GameObject |
| `VariableSO` | `ScriptableObject` — Unity asset for key identity |
| `EntityModifierEntryAsset` | `ScriptableObject` — Unity asset for authoring modifiers |
| `EntityInstance<T>` | `[Serializable]`, `[SerializeField]`, `UnityEngine` namespace import |
| `VariableValue` | `[Serializable]`, `UnityEngine` import |
| `VariableBag` | `[Serializable]`, `[SerializeField]` |

`EntityInstance<T>` can be constructed and used in code without a running Unity scene, but its serialization attributes and `UnityEngine` imports mean the assembly always depends on the Unity runtime. A Unity-free simulation core cannot reference it directly.

---

## 6. What Needs To Be Decided

### The Core Tension With States

Entities owns per-instance variable state with its own immediate notification path. States has a staged-commit transactional pipeline. Using both in the simulation creates a fork:

- **Path A — Entities for authoring, States for runtime variables/modifiers**
  - `EntityDefinition` ScriptableObjects define cards/zones/entities in the editor
  - At session start, definitions are converted to pure runtime records
  - Instance variables and modifiers live in Store slices (`InstanceVariableState`, `InstanceModifierState`)
  - Modifier application goes through `ResolutionScope` → `Mutator` → staged commit → `StateChange` history
  - **Pro:** single mutation pipeline, complete history, transactional scope intact
  - **Con:** conversion step at session start; lose Entities' three-bag chain and Combine logic at runtime; must reimplement modifier resolution in Mutators

- **Path B — Entities owns variables and modifiers; States owns everything else**
  - Instance variables and modifiers live in `EntityInstance<T>`; accessed via `IReadOnlyEntity`
  - Zone contents, session state, phase, priority live in Store slices
  - `ResolutionScope.Apply(AddModifierMutation)` directly calls `instance.AddModifier(...)` under the hood — bypasses scratchpad/commit
  - **Pro:** use Entities' proven three-bag/Combine logic; less to build
  - **Con:** modifier state is outside the Store; StateChange history for variable changes requires an ad hoc adapter; transactional scope is broken for modifier mutations; two state authorities

- **Path C — Entities for both authoring and runtime; wrapper provides the mutation seam**
  - Keep `EntityInstance<T>` as the runtime object
  - Wrap modifier/variable mutations behind a scope that stages them, then flushes in one call
  - Custom staging layer records `StateChange` entries before forwarding to `instance.AddModifier`
  - **Pro:** use Entities throughout; no conversion step
  - **Con:** significant wrapper complexity; Unity assembly dependency in simulation core; staged wrapper is non-trivial to get right

**Recommended reading order for this decision:** States deep dive section 6 (open question 7) → framework doc 10 (entity/instance model) → framework doc 08 (core model decisions, dependency boundary question).

---

## 7. What Entities Does Well (Worth Keeping Regardless Of Path)

1. **Authoring-time variable definition** — `VariableSO` + Inspector tools + `OnValidate` type enforcement is mature and saves a lot of custom editor work for card designers.

2. **Three-bag chain concept** — even if not used as implemented, the `definition bag → instance base → effective` model is the right mental model for the framework's base/modifier/effective value architecture.

3. **`VariableValue.Combine(contributions)`** — letting each value type own its combination logic is the right design. This should be adopted in whatever modifier resolution is built.

4. **Per-variable subscription with immediate-callback on register** — `Subscribe(key, onChange)` fires immediately with the current value. This is the right contract for presentation layers.

5. **Runtime-only modifier entry** — `EntityModifierEntry(Variable key, VariableValue value)` (no SO) is a clean path for programmatic modifiers from effects.

---

## 8. Open Questions (As Of 2026-04-27)

1. **Which path for runtime variable/modifier state?** A, B, or C above. This is the single most consequential decision — it determines whether Entities is runtime or authoring-only.

2. **InstanceId reconciliation** — Scaffold uses `record InstanceId(int Id)`; the framework uses `Identifier<T>`. Options: adopt Scaffold's type; wrap it; keep them in separate layers with a conversion at the boundary.

3. **Definition conversion at session start (Path A)** — If definitions are ScriptableObjects, what does the pure runtime representation look like? A plain record with the same variable bag data? Who owns the conversion? When does it happen?

4. **`VariableValue.Combine` adoption** — If we go Path A and build `InstanceVariableState` slices in the store, how do we implement modifier combination? Port `Combine` to the `Mutator` layer, or keep `VariableValue` as a dependency from the simulation into Entities?

5. **Unity-free core boundary** — The earlier open question (doc 06, doc 08) asked whether the simulation core should be Unity-free. If yes, `EntityInstance<T>` cannot be a simulation type. If no (Unity-always), Path B or C become more viable.

6. **Behavior system** — `IEntityBehavior` / `EntityBehaviorRunner` was not read in depth. It may be relevant to the trigger/reaction system or to the per-instance ability registration model (doc 21, section 5). Deferred.

---

## 9. Related

- [scaffold-states-deep-dive.md](scaffold-states-deep-dive.md) — the other half of the state architecture; open question 7 is directly about this overlap
- [04-scaffold-package-fit.md](../04-scaffold-package-fit.md) — high-level fit table for all Scaffold packages
- [10-entity-definition-instance-model.md](../10-entity-definition-instance-model.md) — framework's entity/definition/instance/variable/modifier model
- [08-core-model-decisions.md](../08-core-model-decisions.md) — Unity-free core boundary question
- [06-open-questions.md](../06-open-questions.md) — Scaffold integration questions
