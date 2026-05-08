---
type: model
status: settled
references: [features/entities/models/01-definition-instance-model.md, features/effects/models/04-effect-flow.md, features/effects/models/03-ongoing-effects.md]
tags: [#entities, #runtime, #api, #model]
---

# Entity Runtime Surface — model

## What it is

Concrete API for reading, writing, spawning, and destroying entities at runtime through the `com.scaffold.entities` + `com.scaffold.entities.states` packages. Companion to [`01-definition-instance-model.md`](features/entities/models/01-definition-instance-model.md), which holds the conceptual model and the mutation-payload conventions; this Model tells a game author what to actually call.

Audience: handler authors, effect authors, presentation projections, replay/snapshot tooling.

## Contracts

### The handle — `InstanceWithSlices<TDef>`

Every entity at runtime is referenced through a typed handle. The base handle is `Scaffold.EntityInstance<TDef>` (external — pairs `(Definition, Storage)` with no identity). The framework introduces a parent — `InstanceWithSlices<TDef>` — that consolidates the `ISliceProvider` contract on the Instance side and tightens `Definition`'s exposure:

```csharp
// Scaffold (external): Scaffold.Entities
public class EntityInstance<TDef> where TDef : IEntityDefinition
{
    public TDef                   Definition { get; }
    public IEntityVariableStorage Storage    { get; }
}

// Card Framework parent — adds ISliceProvider, hides Definition from external consumers
public class InstanceWithSlices<TDef> : Scaffold.EntityInstance<TDef>, ISliceProvider
    where TDef : IEntityDefinition
{
    // Definition is shadowed as protected at this layer.
    // Subclasses can read it; external consumers cannot navigate to it.
    protected new TDef Definition => base.Definition;

    // Default: instance delegates to its definition for initial slices.
    // Override in subclasses for spawn-time customization (rare).
    public virtual IEnumerable<BaseState> GetInitialSlices()
        => Definition is ISliceProvider provider
            ? provider.GetInitialSlices()
            : Array.Empty<BaseState>();
}
```

`InstanceWithSlices<TDef>` is identity-free at the base. Concrete subclasses (`Card`, `Zone`, `Player`) carry typed `Ref<TSubclass> SelfRef` — the stable runtime identity. They are **not sealed** (project convention: default to unsealed); games may subclass for typed re-export or game-side helpers. Storage is `IEntityVariableStorage` — typically `StoreVariableStorage(store, selfRef)` for store-backed variables, or `LocalVariableStorage(parent)` for overlays.

The handle exposes read access, mutation, subscriptions, and slice declaration. Concrete subclasses add their typed identity. Spawn through `store.Spawn<TDef>(def)` (the `StoreEntities.Spawn` extension) or the typed subclass helper (`Card.Spawn`, `Zone.Spawn`, `Player.Spawn`). Live handles are resolved from the catalog: `store.Catalog.Resolve(selfRef)`.

`InstanceWithSlices<TDef>` is **not** a `State`. Snapshots only contain canonical `EntityState` rows; handles are re-created from the catalog after `LoadSnapshot`.

**`Definition` access discipline.** `Definition` is `protected` on `InstanceWithSlices<TDef>` so subclasses (framework or game-side) can read it; external consumers cannot navigate to `instance.Definition`. Game code that genuinely needs typed authored-field reads can subclass `Card` / `Zone` / `Player` and re-expose `Definition` (or specific fields) — this is allowed, but **discouraged** in framework conventions. Prefer reading authored data through the variable bag (`card.Storage.GetVariable<int>(Vars.Cost)`); the variable channel round-trips through serialization, replay, and snapshot cleanly. Direct typed `Definition` reads couple the consumer to the typed Definition subclass and break under transformation (definition swap).

**`ISliceProvider` on both Definition and Instance.** `DefinitionWithSlices` (the framework parent for `Scaffold.EntityDefinition`) and `InstanceWithSlices<TDef>` (the framework parent for `Scaffold.EntityInstance<TDef>`) both implement `ISliceProvider`. The default instance implementation delegates to the definition; this lets the factory's spawn loop work instance-centric (`foreach instance: foreach slice in instance.GetInitialSlices(): store.Install(slice, key: instance.SelfRef)`) without reaching back into definitions per spawn. Concrete subclasses override on the instance side only when spawn-time customization is needed (rare). See [Game Definition](features/engine/models/07-game-definition.md) for the factory's spawn-walk pseudocode.

**Slices don't carry their own primary key.** Storage is keyed externally by the factory using the instance's `SelfRef`. Reference-shaped fields inside slice *values* (e.g., `LastMoveState(Ref<Card>, From, To)`) are *data*, not keys. This convention applies uniformly across framework slices (`EntityState`, `ZoneContentsState`, `OwnershipState`, etc.).

**`Ref<T>` vs `ActorId`.** Concrete subclasses carry `Ref<TSubclass> SelfRef` — typed runtime identity backed by `Guid`, allocated by `store.Catalog.AllocateRef<T>()`. `ActorId` is a distinct concept identifying *who issues* a command, used in command attribution, history envelopes (`HistoryEnvelope.actor`), and per-actor available-action queries (`GetActionsFor(ActorId)`). `Player` carries both — `ActorId` on the `IActor` interface, `Ref<Player>` on the entity handle. Non-entity actors (`World`, `Environment`, `Bot`, `Market`) implement `IActor` directly with just an `ActorId`. See [`GLOSSARY.md`](ledgers/GLOSSARY.md) — `IActor`, `ActorId`, `Ref<T>` rows.

### Read API — `IEntityVariableStorage`

```csharp
public interface IEntityVariableStorage
{
    IEntityVariableStorage? Parent { get; }

    bool TryGetBase(Variable key, out VariableValue value);
    IEnumerable<ActiveModifier> GetModifiers(Variable key);
    IEnumerable<Variable> Variables { get; }

    bool TryGetVariable<T>(Variable key, out T value);
    T    GetVariable<T>(Variable key);

    IDisposable Subscribe(Variable key, Action<VariableValue> onChange);
    void        Unsubscribe(Variable key, Action<VariableValue> onChange);
}
```

Read access flows through `entity.Storage`. The storage adapter resolves the effective value (definition default → instance base override → modifier fold) and exposes lower-level inspection (raw base, raw modifier list, key set). `StoreVariableStorage` is the store-backed adapter; `LocalVariableStorage(parent)` is the overlay adapter for previews or hypothetical states.

**Inner-type reads.** `GetVariable<T>` and `TryGetVariable<T>` take the *inner* value type, not the wrapper:

```csharp
int hp = card.Storage.GetVariable<int>(Vars.Health);          // correct
FloatVariableValue v = card.Storage.GetVariable<float>(Vars.Speed);  // wrong: ask for float, not the wrapper
```

Authors never see `VariableValue` subclasses on the read side unless they're working with the lower-level inspection methods.

**Identity and definition.** Concrete handles expose `SelfRef` publicly. `Definition` is `protected` (per "The handle" above) — external consumers cannot navigate to `instance.Definition`. The framework convention is to read authored data through the variable bag:

```csharp
var villager = store.Catalog.Resolve(villagerRef);
int hp = villager.Storage.GetVariable<int>(Vars.Health);   // through the bag (preferred)
```

For typed-non-variable authored data (typed enums, complex records on the definition), game code may subclass the framework handle and expose what it needs:

```csharp
// Game-side: opt-in re-export via subclass.
public class VillagerHandle : Card
{
    // Subclass-only access to the protected Definition; re-export at the game's discretion.
    public VillagerJob Job => ((VillagerDefinition)base.Definition).Job;
}
```

This pattern is allowed but discouraged — typed `Definition` reads couple the consumer to the typed Definition subclass and break under transformation (definition swap). Prefer modelling game-specific authored data as variables in the bag.

**Polymorphism.** Code that should work against either store-backed or overlay-backed handles holds the storage interface or the entity handle:

```csharp
void Inspect<TDef>(InstanceWithSlices<TDef> e) where TDef : IEntityDefinition { ... }
```

#### Direct slice access

`GetVariable<T>` returns the **effective** value (with all modifiers applied). For raw inspection — base values without fold, full modifier stacks, single modifiers by id — use the storage's pure-read methods or read the canonical slice directly:

```csharp
// Through the storage adapter (preferred — works for both store-backed and overlay):
if (card.Storage.TryGetBase(Vars.Attack, out var baseAttack)) { /* ... */ }
IEnumerable<ActiveModifier> attackMods = card.Storage.GetModifiers(Vars.Attack);

// Through the slice directly (debug, replay, projection layers):
EntityState state = ctx.Store.Get<EntityState>(card.SelfRef);
state.TryGetBase(Vars.Attack, out var bv);
IEnumerable<ActiveModifier> mods = state.GetModifiers(Vars.Attack);  // pre-sorted by Order
ActiveModifier? specific = mods.FirstOrDefault(m => m.Id.Equals(targetId));
```

`ActiveModifier` carries `Id` (`ModifierId`), `Modifier` (`VariableModifier`), and optional `Source` (`ModifierSource?`). Slice-direct reads are the right tool for debug inspectors, history-replay tooling, and projection layers that need raw state. Gameplay code prefers the effective-value reads on the storage adapter.

### Subscriptions

Three subscription channels on the entity, plus one lifecycle event.

#### Per-variable effective-value change

```csharp
IDisposable Subscribe(Variable key, Action<VariableValue> onChange);
void        Unsubscribe(Variable key, Action<VariableValue> onChange);
```

Fires whenever the effective value for `key` changes (after fold). The first callback fires synchronously with the current value when subscribing. Returns `IDisposable` that, when disposed, unregisters. Use this for "react to attack stat changes."

#### Slice-level subscription

For every change to the entity's `EntityState` slice (per-mutation granularity), subscribe to the slice directly:

```csharp
store.Subscribe<EntityState>(card.SelfRef, (oldState, newState, ev) => {
    // ev: StateChangeEvent.Updated | StateChangeEvent.Removed
    // newState: the post-mutation slice (or empty for removal)
});
```

This is the lowest-level surface — engines, debuggers, and replay tools use it. Presentation usually wants per-variable subscriptions for effective-value reactivity. Diff `oldState` against `newState` for "this entity now has a Stunned variable" / "the Stunned variable was removed" semantics.

#### Atomic clears

`ClearModifiers` dispatches a single `ClearModifiersPayload` rather than looping individual remove payloads — slice subscribers fire once for the bulk transition rather than N times. Use when wiping all modifiers on an entity (cleanse, reset, end-of-turn buff clear).

#### Entity removal

When a card / zone / player is destroyed, the slice is unregistered (`store.UnregisterSlice<EntityState>(selfRef)`) and the catalog entry is cleared. Slice subscribers receive a removal event; the handle is no longer resolvable through the catalog. Subscribers attached through the storage adapter are automatically released when the storage is disposed.

For long-lived consumers that need to react symmetrically to the spawn they bound against (presentation views, telemetry), watch the slice directly via `store.Subscribe<EntityState>(selfRef, ...)` and react to the removal event.

#### Lifetime

The framework releases per-variable subscriptions when the storage adapter is disposed (typically on entity destroy). Manual `Unsubscribe` / disposing the `IDisposable` from `Subscribe` is the correct discharge for shorter-lived listeners (e.g. presentation views unloading while the entity is still alive).

### Mutation API

#### Default surface — `IEntityVariableStorage`

**Handlers and framework code prefer the storage adapter over raw payloads.** Holding an `InstanceWithSlices<TDef>` (or a concrete subclass like `Card`) and calling methods on its `Storage` is the recommended default. Raw `ctx.Store.Execute(payload)` is the documented fallback only for batch authoring, history-replay tooling, and paths that hold a `Ref<T>` but not a handle (see "Raw payloads" below).

```csharp
public interface IEntityVariableStorage  // mutation surface (read members elided)
{
    bool        AddVariable(Variable key, VariableValue initialBase);
    bool        RemoveVariable(Variable key);
    void        SetBase(Variable key, VariableValue value);

    ModifierId  AddModifier(Variable key, VariableModifier modifier, ModifierSource? source = null);
    bool        RemoveModifier(Variable key, ModifierId id);
    void        RemoveModifiersFromSource(ModifierSource source);
    void        ClearModifiers();
}
```

Each method synthesizes the corresponding bridge payload (per [`01-definition-instance-model.md` §"Mutation API conventions"](features/entities/models/01-definition-instance-model.md)) and dispatches it via `Store.Execute`. Card Framework conventions on top of this surface:

- **Always build modifier ids through the `Modifiers` factory.** `ctx.Modifiers.Create(key, modifier, source)` returns an `ActiveModifier` with a deterministic `ModifierId` baked in. The storage adapter's `AddModifier` overload accepts a fully-populated `ActiveModifier`; constructing the id ad-hoc via `Guid.NewGuid()` is forbidden in handler/framework code — replay and multiplayer determinism require deterministic ids. See "The Modifiers factory" below.
- **`AddVariable` returns `false` when the variable already exists.** It does not overwrite. Callers that want "set base value, creating the variable if missing" should branch: try `AddVariable`; if it returns `false`, call `SetBase`.
- **`RemoveVariable` returns `true` when something was actually removed.** Includes both base values and any modifier stacks for that variable.
- **`ClearModifiers` is bulk and atomic** — dispatches a single `ClearModifiersPayload` rather than looping individual removes. One slice transition, one subscriber notification.
- **`RemoveModifiersFromSource` is the cleanup-on-source-death hook** — equipment unequipped, attachment destroyed, ongoing-effect ended. Sweeps every modifier on this entity attributed to the source.

#### Raw payloads — when the handle isn't enough

For batch composition, replay tooling, and paths without a handle, dispatch the raw payload through `ctx.Store.Execute(...)`:

```csharp
// Out-of-band base value change (level-up, save load, authoring)
ctx.Store.Execute(new SetBaseValuePayload(
    villager.SelfRef, Vars.MaxHealth, new IntVariableValue(50)));

// Sweep all modifiers from a single source (attachment death)
ctx.Store.Execute(new RemoveModifiersBySourcePayload(
    target.SelfRef, new ModifierSource(attachment.SelfRef, 0)));

// Bulk sweep across all entities (e.g., aura that affects everyone)
ctx.Store.RemoveModifiersFromSource(source);   // StoreEntities extension
```

Other raw-payload cases:

- Batch authoring (loading a card collection from JSON, applying a save snapshot).
- History replay tooling that reconstitutes mutations from a recorded log.
- Any path that holds a `Ref<T>` but not a handle.

#### The `Modifiers` factory

```csharp
public interface IModifierFactory
{
    ActiveModifier Create(
        Variable key,
        VariableModifier modifier,
        ModifierSource? source = null);
}
```

The factory hides id allocation entirely. It generates a deterministic `ModifierId` from an engine-owned counter (snapshotted with engine state, restored on `LoadSnapshot`) and returns a fully-populated `ActiveModifier` with the id and source baked in. Handler code never names a `ModifierId` directly.

The factory is the only sanctioned producer of pre-populated `ActiveModifier`s in framework code. The storage adapter's `AddModifier` accepts either a fully-populated `ActiveModifier` (which it honours unchanged) or the decomposed `(key, modifier, source)` shape.

Two-step usage — build first, then install:

```csharp
var attackBuff = ctx.Modifiers.Create(
    Vars.Attack,
    new IntAddModifier { Amount = 3 },
    new ModifierSource(sword.SelfRef, 0));

villager.Storage.AddModifier(attackBuff);
```

The local-binding step is intentional: it surfaces the modifier as a value the reader can name and reason about (passed through to triggers, logged, etc.), rather than a one-line side effect.

#### Instance-only variable example (`AddEntityVariablePayload` discipline)

Reserved for variables *not* declared in the entity's definition. Concrete case: a generated token gets a `corruption_level` counter unique to this token, not part of `TokenDefinition.DefinedVariables`.

```csharp
var (token, _) = ctx.Store.Spawn(new ImpDefinition());
token.Storage.AddVariable(Vars.CorruptionLevel, new IntVariableValue(0));

// later — increment the per-instance counter via a modifier
var corruptionStep = ctx.Modifiers.Create(
    Vars.CorruptionLevel,
    new IntAddModifier { Amount = 1 });
token.Storage.AddModifier(corruptionStep);
```

If `Vars.CorruptionLevel` were declared in `ImpDefinition.DefinedVariables`, calling `AddVariable` would return `false` (slice's `WithVariable` is a no-op when the key already exists in `BaseValues`). Definition-declared variables stay as definition defaults until written.

#### Batched mutations

Multiple payloads can commit atomically through `Store.ExecuteBatch`. Use this when several writes should be visible to subscribers as a single step:

```csharp
// Build modifier entries via the factory (deterministic ids).
var atkBuff = ctx.Modifiers.Create(Vars.Attack,  new IntAddModifier { Amount = 2 }, source);
var defBuff = ctx.Modifiers.Create(Vars.Defense, new IntAddModifier { Amount = 1 }, source);

ctx.Store.ExecuteBatch(new object[] {
    new AddModifierPayload(target.SelfRef, atkBuff.Key, atkBuff.Modifier, atkBuff.Id, atkBuff.Source),
    new AddModifierPayload(target.SelfRef, defBuff.Key, defBuff.Modifier, defBuff.Id, defBuff.Source),
    new IncrementTurnCounterPayload(),   // a game-authored payload writing a non-entity slice
});
```

The factory returns an `ActiveModifier` with `Id` and `Source` populated. When dropping to raw `AddModifierPayload` for batching, unpack those fields explicitly. Game authors who batch frequently can wrap this in a small helper.

Guarantees:

- **Atomic commit.** All registered mutators for every payload run, then the runner's overlay commits in one shot. No half-applied state is observable.
- **One slice update event per affected slice per batch.** Slice subscribers fire once with the post-batch state; a remove-and-re-add of the same variable produces a single net event.
- **Mixed payload types are allowed.** The same batch can carry entity payloads (`AddModifierPayload`, `SetBaseValuePayload`, …), game-authored payloads, and framework payloads. Each payload's mutators target their own slices.
- **Reads inside the batch are not allowed.** `ExecuteBatch` runs the whole sequence before returning; there is no opportunity to read between payloads. Callers that need read-mutate-read sequences must split into separate `Execute` calls.
- **Ordering is the iteration order of the supplied collection.** The runner processes payloads in the order they're enumerated.

The storage adapter does not expose batch composition — its methods each call `Store.Execute` for a single payload. To batch entity mutations alongside non-entity ones, drop to raw payloads as shown above. The factory still applies: build modifier entries through `ctx.Modifiers.Create(...)` and then construct `AddModifierPayload` from the entry's fields.

### Lifecycle — `store.Catalog` and `StoreEntities.Spawn`

The Store owns a `Catalog` (in-process side-state, not snapshotted) that holds live handles by `Ref<T>`. Spawn pathway is the `StoreEntities.Spawn<TDef>` extension (a Scaffold framework primitive); destroy is the matching unregister pathway. Per-kind registries (e.g., `CardRegistry`) sit on top when a game wants typed lookup helpers.

```csharp
// Scaffold framework primitive
public static class StoreEntities
{
    public static (EntityInstance<TDef> Instance, Ref<EntityInstance<TDef>> Ref)
        Spawn<TDef>(this Store store, TDef def) where TDef : IEntityDefinition;
}

// Catalog (sub-property on Store)
public interface ICatalog
{
    Ref<T> AllocateRef<T>() where T : class;
    void   RegisterAt<T>(Ref<T> r, T obj) where T : class;
    T      Resolve<T>(Ref<T> r) where T : class;
    bool   Unregister<T>(Ref<T> r) where T : class;
}
```

Project-side, typed subclass spawn helpers wrap `StoreEntities.Spawn` and return the typed handle:

```csharp
public class Card : InstanceWithSlices<CardDefinition>
{
    public Ref<Card> SelfRef { get; init; }

    public static Card Spawn(Store store, CardDefinition def)
    {
        var (instance, r) = store.Spawn(def);
        var card = new Card(...) { SelfRef = r.Cast<Card>() };
        store.Catalog.RegisterAt(card.SelfRef, card);
        return card;
    }
}
```

Handlers and effects access the catalog through their context surfaces:

```csharp
// IHandlerContext / IEffectScope
Store Store { get; }   // store.Catalog.Resolve(ref) for lookup
```

Game-specific registries (e.g., `CardRegistry` keyed `Ref<Card> → Card`) are engine services on top of the catalog when typed lookup is ergonomic.

#### Spawn

`store.Spawn<TDef>(definition)`:

1. Allocates a fresh `Ref<EntityInstance<TDef>>` via `store.Catalog.AllocateRef<EntityInstance<TDef>>()`.
2. Builds the per-instance `StoreVariableStorage(store, ref)`.
3. Registers an empty `EntityState` slice keyed by the new ref.
4. Constructs the `EntityInstance<TDef>` handle pairing `(definition, storage)`.
5. Registers the handle in the catalog (`store.Catalog.RegisterAt(ref, instance)`).
6. Returns `(instance, ref)`.

The new entity inherits *all* its variable values from the definition (empty `BaseValues` map → reads fall back to `definition.TryGetDefaultValue(var)`). To override a base value at spawn time, call `instance.Storage.SetBase(...)` or dispatch `SetBaseValuePayload` immediately after spawn.

Typed subclass helpers (`Card.Spawn`) wrap this and additionally cast the ref to the typed subclass shape (`Ref<Card>`) and store it in `SelfRef`.

#### Resolve

`store.Catalog.Resolve(ref)` returns the live handle, or throws if the ref is unknown. For tolerant lookup, use `TryResolve` (or check via your typed registry). Per-kind registries (`CardRegistry.Get(refOfCard)`) are typically thin wrappers that throw on unknown.

#### Destroy

Destroying an entity:

1. Dispose any per-handle subscriptions (storage adapter, presentation views).
2. Unregister the slice (`store.UnregisterSlice<EntityState>(ref)`).
3. Unregister the catalog entry (`store.Catalog.Unregister(ref)`).
4. Drop any per-kind registry entry.

The framework Cleanup hook (per [`effects/03 §5`](features/effects/models/03-ongoing-effects.md)) handles ongoing-effect attachments automatically — when an attachment entity is destroyed, every modifier sourced to it is swept from every target via `RemoveModifiersBySourcePayload(target, new ModifierSource(attachment.SelfRef, 0))` or via the bulk `store.RemoveModifiersFromSource(source)` extension.

#### Snapshot / restore

Snapshots contain canonical `EntityState` slices, never handles. The catalog is in-process side-state and is repopulated alongside snapshot load:

1. The engine walks the restored slices and reconstitutes catalog entries.
2. For each slice, the engine re-creates an `EntityInstance<TDef>` using the same `Ref<T>` and the definition associated with that ref.
3. The mapping `Ref<T> → definition` is engine-managed (typically a sibling slice that records "what was spawned" so the restore knows which `TDef` to instantiate).

Snapshot rollback to a state where an entity still existed re-creates its row and re-issues the handle. Subscribers attached to old handles are gone — projections that need to bind on rollback must re-subscribe through the catalog.

### Read consistency

The storage adapter reads directly from the canonical `EntityState` slice through its pure-read methods (`TryGetBase`, `GetModifiers`, `Variables`). There is no per-handle effective-value cache — the slice is the source of truth, mutators return new records via `this with { ... }`, and reads see the latest record on every call.

- Every change to the `EntityState` slice fires a Store event subscribed by per-variable subscribers; the storage adapter routes the notification through the `Subscribe` channel.
- All of this is synchronous within the originating `Store.Execute` / `Store.ExecuteBatch` call.

Implications for handler code:

- **Read-after-write within a single handler is fresh.** `card.Storage.AddModifier(...)` followed by `card.Storage.GetVariable<T>(...)` returns the post-mutation effective value.
- **`ExecuteBatch` commits once after all payloads run.** Reads inside an `ExecuteBatch` block are not allowed (the bridge runs the whole batch atomically); reads after the batch see the final state.
- **No "stale read" race for handler code.** All Store mutations and all subscriber notifications are serialized on a single thread.

If a `RemoveVariable` payload is followed by an `AddVariable` for the same key in a single batch, slice subscribers see one net event with the post-batch state. Per-variable subscribers see the new value only if it differs from the old. This is the intended default for continuous-effects cleanup; `ClearModifiersPayload` similarly produces a single notification rather than N individual remove events.

### Authoring reference

This Model commits to using Scaffold's authoring types. Game-author guide for declaring the artifacts that handler/effect code references at runtime:

#### `Variable` — the key

```csharp
public sealed class Variable {
    public string Key            { get; }   // human-readable identifier
    public string PayloadTypeId  { get; }   // stable id for the value's wire type
}
```

Two strings — the human-readable `Key` (used for ordering, debugging) and a `PayloadTypeId` that pins the variable's value type for serialization and modifier compatibility. Authored either as `VariableSO` (`ScriptableObject`-based with inspector authoring) or code-defined (`static readonly Variable Vars.Attack = new("attack", "int");`).

Convention: a game's variable catalog lives as static fields on a `Vars` class (e.g. `Vars.Attack`, `Vars.Health`). Mixing inline-string keys at call sites is a smell — leads to typo drift.

#### `VariableValue` — the value wrapper

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

#### `VariableModifier<T>` — the modifier

```csharp
public abstract class VariableModifier {
    public int Order { get; }   // serialized; ascending fold order
}

public abstract class VariableModifier<T> : VariableModifier {
    public abstract T Apply(T current);
}
```

Built-in modifiers ship in `com.scaffold.entities`:

| Modifier | Inner type | Behavior |
|---|---|---|
| `IntAddModifier` | `int` | `current + amount` |
| `IntMultiplyModifier` | `int` | `current * factor` |
| `FloatAddModifier` | `float` | `current + amount` |
| `FloatMultiplyModifier` | `float` | `current * factor` |
| `BoolOverrideModifier` | `bool` | `value` (replaces current) |
| `StringAppendModifier` | `string` | `current + suffix` |

Custom modifiers subclass `VariableModifier<T>` and override `Apply`. `Order` controls fold position (lower runs first; ties FIFO by insertion).

#### `ActiveModifier` — the (key, modifier, id, source) record

```csharp
public sealed record ActiveModifier(
    Variable          Key,
    VariableModifier  Modifier,
    ModifierId        Id,
    ModifierSource?   Source);
```

The fully-populated runtime record. The factory (`ctx.Modifiers.Create(...)`) is the sanctioned producer; the storage adapter's `AddModifier` accepts an `ActiveModifier` directly or the decomposed `(key, modifier, source)` shape (allocating an id internally only for non-replay paths). For serialization-friendly authoring, cards declare the (key, modifier) shape and the engine populates `Id`/`Source` at install time.

#### `IEntityDefinition` and `EntityDefinition`

```csharp
public interface IEntityDefinition {
    IEnumerable<Variable> DefinedVariables { get; }
    bool                  TryGetDefaultValue(Variable key, out VariableValue value);
}
```

Definitions declare which variables an instance has and what their default values are. Reads fall back to `TryGetDefaultValue` for any variable not in the instance's `BaseValues` map; this is what lets an empty-baseline instance still report `entity.GetVariable<int>(Vars.Attack) == 1`.

`EntityDefinition` is the standard `[Serializable]` ScriptableObject implementation, with an internal `VariableBag` and Inspector authoring. Game-specific definition classes inherit from it. Definitions can be Unity assets, code-constructed at boot, or hybrid. The Card Framework does not yet pin an authoring layer on top of `EntityDefinition` — see `bl-authoring-feature`.

## Invariants

Model-local invariants. (See `entities.md ## Invariants` for feature-level rules.)

- **`InstanceWithSlices<TDef>` is not a `State`.** Snapshots only contain canonical `EntityState` rows; handles are re-created from the catalog after `LoadSnapshot`.
- **Inner-type reads.** `GetVariable<T>` and `TryGetVariable<T>` take the inner value type, never the wrapper.
- **`AddVariable` does not overwrite.** Returns `false` when the variable already exists; callers that want set-or-create branch on the return.
- **Factory-only `ModifierId`.** The `IModifierFactory` is the only sanctioned producer of pre-populated `ActiveModifier` in framework/handler code. Ad-hoc `Guid.NewGuid()` for ids is forbidden.
- **Reads-inside-batch are not allowed.** `ExecuteBatch` runs the whole sequence atomically; callers needing read-mutate-read split into separate `Execute` calls.
- **Single mutator registration per Store.** `EntityBridgeContext.RegisterMutators(storeBuilder)` runs once at engine bootstrap, registering all seven mutators (including `ClearModifiersMutator`).

## Decisions

*n/a — no elevated ADR.*

## Examples

### Equip / unequip — single-handler mutation

A villager with `Attack = 1` (definition default) equips a sword granting `+3 attack`; effective attack reads as `4`. On unequip, the sword's modifier is swept and effective attack reverts to `1`.

```csharp
public static class Vars {
    public static readonly Variable Attack = new("attack", "int");
}

public sealed class VillagerDefinition : EntityDefinition {
    // Authored: defines Vars.Attack with default value 1
}

public sealed class SwordDefinition : EntityDefinition { /* ... */ }

public sealed record EquipSwordCommand(Ref<Villager> Wearer, Ref<Sword> Sword) : GameAction;
public sealed record EquipResult(int AttackBefore, int AttackAfter) : CommandResult;

public sealed class EquipSwordHandler : ICommandHandler<EquipSwordCommand, EquipResult>
{
    public Task<EquipResult> Handle(EquipSwordCommand cmd, IHandlerContext ctx)
    {
        var villager = ctx.Store.Catalog.Resolve(cmd.Wearer);
        var sword    = ctx.Store.Catalog.Resolve(cmd.Sword);

        var before = villager.Storage.GetVariable<int>(Vars.Attack);   // 1 (definition default)

        // Build the +3 attack buff, sourced to the sword instance
        var attackBuff = ctx.Modifiers.Create(
            Vars.Attack,
            new IntAddModifier { Amount = 3, Order = 100 },
            new ModifierSource(sword.SelfRef, 0));

        // Install it on the villager via the storage adapter
        villager.Storage.AddModifier(attackBuff);

        var after = villager.Storage.GetVariable<int>(Vars.Attack);    // 4

        return Task.FromResult(new EquipResult(before, after));
    }
}

public sealed record UnequipSwordCommand(Ref<Villager> Wearer, Ref<Sword> Sword) : GameAction;
public sealed record UnequipResult(int AttackAfter) : CommandResult;

public sealed class UnequipSwordHandler : ICommandHandler<UnequipSwordCommand, UnequipResult>
{
    public Task<UnequipResult> Handle(UnequipSwordCommand cmd, IHandlerContext ctx)
    {
        var villager = ctx.Store.Catalog.Resolve(cmd.Wearer);

        // Sweep every modifier on the wearer that came from the sword
        villager.Storage.RemoveModifiersFromSource(new ModifierSource(cmd.Sword, 0));

        // Destroy the sword instance — slice unregistered, catalog entry cleared
        ctx.Store.Destroy(cmd.Sword);

        var after = villager.Storage.GetVariable<int>(Vars.Attack);    // 1 (back to definition default)

        return Task.FromResult(new UnequipResult(after));
    }
}
```

For attachment models where the sword *itself* is the attached entity (per [`effects/03 §2`](features/effects/models/03-ongoing-effects.md)), the framework Cleanup hook performs the source-sweep automatically on destroy via `store.RemoveModifiersFromSource(source)` — the explicit `RemoveModifiersFromSource` shown here is the long form, useful when the unequip semantics differ (e.g. sword survives but stops contributing).

### Apply-aura — modifier built in effect, installed in handler

When a modifier comes from an effect orchestrator (target selection, pre-conditions, choosing parameters) but the install lands in a separate action's handler, the modifier entry travels through the action payload as a value. The factory call sits in the effect (so the deterministic `ModifierId` is allocated at the right point in the command stream); the handler just installs the pre-built entry.

```csharp
// Action payload — carries the pre-built modifier across the boundary.
public sealed record ApplyDamageAmpAction(
    Ref<Enemy>      Target,
    ActiveModifier  Modifier) : GameAction;

public sealed record ApplyResult(Ref<Enemy> Target) : CommandResult;

// Effect — picks target, builds the buff, dispatches the install action.
public sealed class ApplyDamageAmpEffect : Effect
{
    public override async Task Execute(IEffectScope scope)
    {
        var pick = await scope.Dispatch<RequestTargetResult>(new RequestTargetAction
        {
            Filter = EnemyFilter.InRange(scope.Source, range: 3),
        });
        if (pick.Target is null) return;

        // Built in the effect — id is deterministic, source is this effect's host.
        var ampBuff = scope.Modifiers.Create(
            Vars.DamageDealt,
            new IntAddModifier { Amount = 2, Order = 200 },
            new ModifierSource(scope.Source.SelfRef, 0));

        await scope.Dispatch<ApplyResult>(new ApplyDamageAmpAction
        {
            Target   = pick.Target.Value,
            Modifier = ampBuff,
        });
    }
}

// Handler — installs the pre-built entry. No id allocation here; the factory
// already produced one in the effect.
public sealed class ApplyDamageAmpHandler : ICommandHandler<ApplyDamageAmpAction, ApplyResult>
{
    public Task<ApplyResult> Handle(ApplyDamageAmpAction cmd, IHandlerContext ctx)
    {
        var enemy = ctx.Store.Catalog.Resolve(cmd.Target);
        enemy.Storage.AddModifier(cmd.Modifier);
        return Task.FromResult(new ApplyResult(cmd.Target));
    }
}
```

Why the factory call belongs in the effect, not the handler:

- **Determinism is per-decision-point, not per-handler-call.** The id is allocated at the moment the effect *decides* to install the buff. Re-running the same effect against the same state must produce the same id. If the handler called the factory, the id would shift if upstream effects added intervening modifiers.
- **The modifier is part of the action's identity.** It's serialized into the history envelope; debugger / replay tooling can render "ApplyDamageAmp targeted goblin#42 with buff M-7" as a complete record.
- **Handlers stay narrow.** They install what they're told, validate the target, and return. They don't make modifier-shape decisions.

Use this pattern whenever the buff parameters depend on effect-side state (target choice, source attribution, computed magnitude) but the install must be a discrete observable command (replay, network sync, history clarity). For self-contained "buff this entity right now" cases where there's no target picking or pre-computation, the equip/unequip pattern — single handler doing build + install — is simpler.

## Open follow-ups

- **`Spawn` with caller-supplied `Ref<T>`.** Currently `store.Spawn<TDef>(def)` allocates the ref internally. A `SpawnAt<TDef>(def, ref)` overload may be needed for save-state restoration paths that must reuse specific refs. Pin when the snapshot/load flow lands.
- **Engine-side service composition.** Where `CardRegistry`, `IModifierFactory`, and the deterministic ref/id generators are constructed, registered, and exposed is engine-builder territory — see `oq-engine-builder`.
- **Authoring layer on top of `EntityDefinition`.** Custom-game-definition serialization, validation, asset migration, definition versioning are deferred — `bl-authoring-feature`.
- **Multi-store entities.** Currently `StoreEntities.Spawn` is parameterized on a single `Store`. Multi-store games (player-private stores, instance-shard stores) would need catalog partitioning. Out of scope until a multi-store game arrives.

---

*Below: provenance — this Model has no folded or elevated ADR. The runtime-surface design follows directly from the conceptual decisions in [`01-definition-instance-model.md`](features/entities/models/01-definition-instance-model.md) (Q1–Q6 below that Model's divider) and the framework parent-class conventions established in the open-seams cascade. There is no separate ADR for the runtime API surface; substantive decisions (handle-vs-payloads default, factory-only ModifierId, AddVariable-no-overwrite semantics, eager-and-synchronous read consistency) are captured in the Model body's `## Contracts` section.*

## Options considered

*n/a — purely descriptive Model. The runtime-surface decisions follow directly from the conceptual model in `01-definition-instance-model.md` and the open-seams cascade conventions; no separate options-considered analysis at this Model's scope.*

## Rationale

*n/a — see `## Contracts` and `entities.md ## Invariants`. The handle-as-default-mutation-surface and the factory-only-ModifierId rule are the load-bearing rationale at this level.*

## Consequences

*n/a — see `## Contracts` and `entities.md ## Invariants`.*
