---
type: model
status: settled
references: [features/entities/models/01-definition.md, features/entities/models/02-instance.md, features/entities/models/04-modifier.md]
tags: [#entities, #variable, #state, #model]
---

# Variable — model

## What it is

The key-value layer of the entity model. **Variables** are how authored data + per-instance state surface as runtime values. Each `Variable` is a `(Key, PayloadTypeId)` pair authored once on a `Vars` static catalog; values are wrapped in typed `VariableValue` subclasses for serialization. Variable state lives in `EntityState.BaseValues` — an *override-only* map keyed at the instance's `SelfRef`. Reads on an instance fall back to the definition's default when the variable isn't in the map; writes go through the storage adapter (or raw payloads) and produce slice transitions.

```text
VariableKey -> VariableValue
```

The base framework does not hardcode specific variable names. A game, preset, schema, or authoring system defines available keys, value types, default values, validation rules, and display metadata.

## Contracts

### Variable concept

Examples (illustrative, not required): `cost`, `attack`, `health`, `owner`, `controller`, `zone`, `visibility`, `energy`, `progress`, `stack_count`, `exhausted`.

Typed wrappers can exist later for convenience (e.g., a deckbuilder preset exposing `energy` through typed helper APIs), but the underlying base model remains key-value.

### `Variable` — the key

```csharp
public sealed class Variable
{
    public string Key            { get; }   // human-readable identifier
    public string PayloadTypeId  { get; }   // stable id for the value's wire type
}
```

Two strings — the human-readable `Key` (used for ordering, debugging) and a `PayloadTypeId` that pins the variable's value type for serialization and modifier compatibility. Authored either as `VariableSO` (`ScriptableObject`-based with inspector authoring) or code-defined:

```csharp
public static class Vars
{
    public static readonly Variable Attack = new("attack", "int");
    public static readonly Variable Health = new("health", "int");
}
```

**Convention:** a game's variable catalog lives as static fields on a `Vars` class. Mixing inline-string keys at call sites is a smell — leads to typo drift.

### `VariableValue` — the value wrapper

Abstract base; concrete subclasses are tagged with `[VariableValueId("...")]` and implement `IVariableValue<T>` for inner-type access:

```csharp
[VariableValueId("int")]
public sealed class IntVariableValue : VariableValue, IVariableValue<int> { ... }
// Also: BoolVariableValue, LongVariableValue, FloatVariableValue, DoubleVariableValue, StringVariableValue
```

Read paths return the inner type (`int`, `float`, etc.). Write paths take the wrapper:

```csharp
ctx.Store.Execute(new SetBaseValuePayload(id, Vars.Health, new IntVariableValue(10)));
```

### `EntityState.BaseValues` — the canonical slice

Per-entity variable state lives as `EntityState.BaseValues` — an `ImmutableDictionary<Variable, VariableValue>` keyed externally at the entity's `SelfRef`:

```csharp
public sealed record EntityState(
    ImmutableDictionary<Variable, VariableValue>            BaseValues,
    ImmutableDictionary<Variable, ImmutableList<ActiveModifier>> ModifierStacks
) : BaseState;
```

`BaseValues` is **override-only**. Effective reads on the handle fall back to `definition.TryGetDefaultValue(var)` for any variable not in the map; a freshly-spawned instance has an empty `BaseValues` and inherits everything from its definition. The `ModifierStacks` companion belongs to [`04-modifier.md`](features/entities/models/04-modifier.md).

### Read API

Read access flows through `entity.Storage`. The storage adapter resolves the effective value (definition default → instance base override → modifier fold) and exposes lower-level inspection (raw base, raw modifier list, key set):

```csharp
public interface IEntityVariableStorage  // read members
{
    bool TryGetBase(Variable key, out VariableValue value);
    IEnumerable<ActiveModifier> GetModifiers(Variable key);
    IEnumerable<Variable> Variables { get; }

    bool TryGetVariable<T>(Variable key, out T value);
    T    GetVariable<T>(Variable key);

    IDisposable Subscribe(Variable key, Action<VariableValue> onChange);
    void        Unsubscribe(Variable key, Action<VariableValue> onChange);
}
```

`StoreVariableStorage` is the store-backed adapter; `LocalVariableStorage(parent)` is the overlay adapter for previews or hypothetical states.

**Inner-type reads.** `GetVariable<T>` and `TryGetVariable<T>` take the *inner* value type, not the wrapper:

```csharp
int hp = card.Storage.GetVariable<int>(Vars.Health);          // correct
FloatVariableValue v = card.Storage.GetVariable<float>(Vars.Speed);  // wrong: ask for float, not the wrapper
```

Authors never see `VariableValue` subclasses on the read side unless they're working with the lower-level inspection methods.

### Direct slice access

`GetVariable<T>` returns the **effective** value (with all modifiers applied). For raw inspection — base values without fold, full modifier stacks — read the canonical slice directly:

```csharp
EntityState state = ctx.Store.Get<EntityState>(card.SelfRef);
state.TryGetBase(Vars.Attack, out var bv);
```

Slice-direct reads are the right tool for debug inspectors, history-replay tooling, and projection layers that need raw state. Gameplay code prefers the effective-value reads on the storage adapter.

### Mutation API

#### Default surface — `IEntityVariableStorage`

```csharp
public interface IEntityVariableStorage  // variable-mutation members
{
    bool        AddVariable(Variable key, VariableValue initialBase);
    bool        RemoveVariable(Variable key);
    void        SetBase(Variable key, VariableValue value);
}
```

Each method synthesizes the corresponding bridge payload and dispatches via `Store.Execute`:

| Method | Payload | Notes |
|---|---|---|
| `AddVariable(key, initial)` | `AddEntityVariablePayload(EntityRef, Variable, InitialValue)` | Returns `false` when the variable already exists (no overwrite). |
| `RemoveVariable(key)` | `RemoveEntityVariablePayload(EntityRef, Variable)` | Returns `true` when something was actually removed (base or modifiers). |
| `SetBase(key, value)` | `SetBaseValuePayload(EntityRef, Variable, Value)` | Out-of-band base override; bypasses modifier-source tracking. |

#### Raw payloads — when the handle isn't enough

For batch composition, replay tooling, and paths without a handle, dispatch the raw payload through `ctx.Store.Execute(...)`:

```csharp
ctx.Store.Execute(new SetBaseValuePayload(
    villager.SelfRef, Vars.MaxHealth, new IntVariableValue(50)));
```

Other raw-payload cases: batch authoring (loading a card collection from JSON), history-replay tooling, any path that holds a `Ref<T>` but not a handle.

#### Payload disciplines

- **`AddEntityVariablePayload` discipline.** Reserved for variables *not* declared in the entity's definition — per-instance ad-hoc state (token counters, custom buff bookkeeping). Definition-declared variables stay as definition defaults until a `SetBaseValuePayload` overrides the base or an `AddModifierPayload` stacks a modifier. The slice's `WithVariable` is a no-op when the variable already exists in `BaseValues`, encoding this rule on the storage side.
- **`SetBaseValuePayload` discipline.** Live, but reserved for *out-of-band* base changes: definition-default override at instance creation, level-up / progression hooks, save-state load, authoring tools. Effect-tracked changes always go through modifiers (see [`04-modifier.md`](features/entities/models/04-modifier.md)) so source attribution and removal are preserved.

### Per-variable subscriptions

```csharp
IDisposable Subscribe(Variable key, Action<VariableValue> onChange);
void        Unsubscribe(Variable key, Action<VariableValue> onChange);
```

Fires whenever the effective value for `key` changes (after fold). The first callback fires synchronously with the current value when subscribing. Returns `IDisposable` that, when disposed, unregisters. Use this for "react to attack stat changes."

The framework releases per-variable subscriptions when the storage adapter is disposed (typically on entity destroy). Manual `Unsubscribe` / disposing the `IDisposable` from `Subscribe` is the correct discharge for shorter-lived listeners (e.g. presentation views unloading while the entity is still alive).

For per-mutation granularity (slice-level), see [`02-instance.md` §Slice-level subscriptions](features/entities/models/02-instance.md).

### Instance-only variable example

Reserved for variables *not* declared in the entity's definition. Concrete case: a generated token gets a `corruption_level` counter unique to this token, not part of `TokenDefinition.DefinedVariables`.

```csharp
var (token, _) = ctx.Store.Spawn(new ImpDefinition());
token.Storage.AddVariable(Vars.CorruptionLevel, new IntVariableValue(0));

// later — increment via a modifier (per 04-modifier.md)
var corruptionStep = ctx.Modifiers.Create(
    Vars.CorruptionLevel,
    new IntAddModifier { Amount = 1 });
token.Storage.AddModifier(corruptionStep);
```

If `Vars.CorruptionLevel` were declared in `ImpDefinition.DefinedVariables`, calling `AddVariable` would return `false` (slice's `WithVariable` is a no-op when the key already exists in `BaseValues`). Definition-declared variables stay as definition defaults until written.

## Invariants

1. **Variables are key-value at the framework layer.** No hardcoded variable names; games/presets/schemas declare their catalogs.
2. **`EntityState.BaseValues` is override-only.** Effective reads fall back to the definition's `TryGetDefaultValue` when a variable isn't in the map.
3. **Inner-type reads.** `GetVariable<T>` and `TryGetVariable<T>` take the inner value type, never the wrapper.
4. **`AddVariable` does not overwrite.** Returns `false` when the variable already exists; callers that want set-or-create branch on the return.
5. **`AddEntityVariablePayload` is for definition-undeclared variables only.** Definition-declared variables stay as defaults; `SetBaseValuePayload` overrides bases for out-of-band changes (level-up, save load).

## Decisions

*n/a — no elevated ADR.* (Q2 — Value model — lives in [`01-definition.md` §Options considered`](features/entities/models/01-definition.md).)

## Examples

For worked equip/unequip and apply-aura examples exercising variables alongside modifiers, see [`05-attachment.md` §Examples](features/entities/models/05-attachment.md).

## Open follow-ups

- **Typed variable wrappers.** A future authoring layer may expose typed helper APIs (`card.Energy` instead of `card.Storage.GetVariable<int>(Vars.Energy)`). Tracked under `bl-authoring-feature`.
- **Variable migration / versioning.** Save-load compatibility when a definition's variable catalog changes between versions. Authoring-layer concern.

---

*Below: provenance — n/a. This Model is purely descriptive (its shape follows directly from the Models referenced in `## Contracts` above and the project-wide ADRs they cite); no separate options-weighing at this Model's scope.*

## Options considered

*n/a — descriptive Model.*

## Rationale

*n/a — see `## Contracts` above and the feature root's `## Invariants`.*

## Consequences

*n/a — see `## Contracts` above and the feature root's `## Invariants`.*
