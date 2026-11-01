---
type: spike
status: proposed
created: 2026-11-01
references:
  - docs/lookup-patterns.md
  - docs/coding-standards.md
  - Product/spikes/infra-record-ids.md
  - Product/spikes/infra-block-wire.md
sandbox: Assets/Framework/Core/Runtime/
tags: [#spike, #infra, #kinds, #registry, #wire]
---

# Shared infra — kind ids

> **Status: proposed.** This spike came from a split of `infra-record-ids.md` (2026-11-01).
> A **kind** is a type that names one member of an open set: a tag, an entity type, a game
> state. Its id is the **slot**: an authored number for the type, not for an instance.
> Page: https://claude.ai/artifact/CPgJfPpSDPmpWKah1tQPFb. No code changes yet.

## 1. TL;DR

1. Keep the standard in `docs/lookup-patterns.md`: a kind is a record over a `TypeKey` base, and a `SlotRegistry` maps slots to members.
2. The slot stays an authored `int`. It is the only identity on the wire and in data. The record type is the identity in code.
3. The slot is `internal` to `Framework.Core`. The registry is the only public reader: `SlotOf(key)` and `BySlot(slot)`.
4. Every kind space has a filled, frozen registry. Today `TagRegistry` is empty, so no code checks tag slots.
5. Games add members through `builder.AddKind<TRegistry, TKind>()`. The framework keeps slots 0–999. Games use 1000 and up.
6. Three gate rules: KIND-01 (unique slots), KIND-02 (slot ranges), KIND-03 (a kind root record is not positional).

### Decisions

1. (2026-11-01) The authored number stays the one wire identity. A gate rule checks that slots are unique. The options that remove the number (a count at composition, a hash of the type name) are rejected. Saves, replays and data need a number that does not change across builds.
2. (2026-11-01) Kind ids and record ids are two different patterns. They get two spikes and two pages.
3. (2026-11-01) No typed slot struct. The slot is an `int`, `internal` to Core, and only the registry reads it. This replaces the slot structs (`TagSlot`, `EntityTypeSlot`, `StateSlot`) from decision 1. Code almost never touches a slot, so a struct for each space costs more than it protects.
4. (2026-11-01) Scope follows the two-tab page template (§2). Block kind, tag values, the state stack and wire packing are out.

## 2. Scope

| Concern | In / Out | Owner if out |
|---|---|---|
| Kind root record and its members (`Tag`, `EntityType`, `GameState`) | in | — |
| Slot number for each member: unique in its kind space, in the correct range | in | — |
| Registry: slot to member, member to slot, type to member | in | — |
| Registration at composition: baselines, game members, then freeze | in | — |
| Slot access: only the registry reads the slot | in | — |
| Members from data, added before the freeze | in | — |
| Rules: unique slots, slot ranges, no positional root record | in | — |
| Instance ids (`EntityId`, `RequestId`, `BlockId`, `MessageId`) and their allocation | out | `infra-record-ids.md` |
| Wire event fields and packing of the slot `int` | out | `Framework.Wire` (AutoPacker) |
| Tag values: value types, `TagStore`, value checks | out | `Framework.Tags` |
| Game state stack (`Push`, `Pop`, current state) | out | `Framework.States` |
| `BlockKind`: change from an `enum` to a kind space | out | `infra-block-wire.md` (it can use this pattern) |
| Card definition ids in `Catalog` | out | `Framework.Entities` (Catalog) |
| Handler lookup by message type | out | `infra-type-router.md` |
| VS `TagKey` and `[TagSlot]` attribute | out | VS spike (deletable, not promoted) |

Boundary: the pattern receives kind records from framework features and games at composition. It gives each feature a frozen registry. The feature writes `SlotOf(key)` into its wire event and reads `BySlot(int)` in its apply handler. The next system is `Framework.Wire`.

## 3. Today

### 3.1 Uses

| Kind space | Root record | Members today | Registry | Read by slot | Where |
|---|---|---|---|---|---|
| tag | `Tag`, `Tag<T>` | none in the framework; samples `HealthTag(2001)`, `CostTag(2002)`, `IsCardTag(2003)` | `TagRegistry`, **empty** | never: `TagStore` keys by `(entityId, slot)` | `Tags/Runtime/Tag.cs:6,11`; `Testing/Support/SampleTags.cs:9-11`; `Bootstrap/Runtime/TagInstaller.cs:15` |
| entity type | `EntityType` | `Unknown(0)`, `Player(2)`, `Zone(3)`, `Card(4)`, `Modifier(5)` | `EntityTypeRegistry.WithBaselines` | `EntityCreatedHandler` | `Entities/Runtime/EntityType.cs:5-11`; `Registry/EntityTypeRegistry.cs:7-16`; `Handlers/EntityCreatedHandler.cs:17` |
| game state | `GameState` | `MatchNotStartedState(0)`, `PlayingState(1)`, `MatchEndedState(2)` | `GameStateRegistry.WithBaselines` | `StatePushHandler` | `States/Runtime/GameState.cs:5`; `Registry/GameStateRegistry.cs:7-14`; `Handlers/StatePushHandler.cs:16` |

Paths are under `Assets/Framework/`. `key.Slot` is public. Runtime code reads it at 10 sites (`TagService.cs:18-50`, `EntityService.cs:23`, `StateService.cs:15`). Tests read it at 3 sites (`TwoSidedMatchIntegrationTests.cs:45-47`).

Five findings:

1. **Tag slots have no check.** `TagInstaller` registers an empty `TagRegistry`. Two tags with the same slot and the same value type share one storage cell. No error shows.
2. **Games cannot add kinds.** `EntityInstaller` and `StateInstaller` register `WithBaselines()` as an instance. No installer call adds a game member.
3. **A clash shows late.** `SlotRegistry.Add` throws when the registry fills, when a match starts. Nothing checks in the gate.
4. **Any code reads the slot.** `Slot` is a public positional property, so code can use a slot with no registry.
5. **No slot ranges.** The framework uses 0–5. The samples use 1001 and 2001. Nothing keeps a game out of the framework numbers.

### 3.2 What repeats

| Common part | Seen in | Varies between uses |
|---|---|---|
| Root record over `TypeKey(int Slot)` | Tag, EntityType, GameState | Extra members: `Tag.ValueType` |
| Sealed member with a fixed slot | all three | Slot numbers; tags are samples only |
| Sealed registry subclass over `SlotRegistry<TKey>` | all three | Tag: empty; others: `WithBaselines()` |
| Installer registers the registry | all three | Tag: by type; others: as an instance |
| `[Packed] int` slot field on a wire event | all three | Name: `Slot` or `TypeSlot` |
| Apply handler reads the slot | all three | Tag: slot as a store key; others: `BySlot` |

## 4. Proposal

### 4.1 API

| Audience | Entry point | Does |
|---|---|---|
| author | `sealed record X() : Root(slot)` | Defines one member with its slot. |
| author | `abstract record Root : TypeKey` | Defines a kind space (feature authors only). |
| composition | `builder.AddKind<TRegistry, TKind>()` | Adds one code member to a kind space. |
| composition | `registry.Register(member)`, `Freeze()` | Adds a data member; closes the registry. |
| runtime | `registry.SlotOf(key)` | Member to slot, for a wire field. |
| runtime | `registry.BySlot(slot)`, `TryBySlot` | Slot to member, in an apply handler or a data load. |
| runtime | `registry.Of<T>()`, `is T` | Type to member; branch on a member. |

Shape:

```csharp
public abstract record TypeKey
{
    protected TypeKey(int slot);            // the only way to set a slot
    internal int Slot { get; }              // only Framework.Core reads it
}

public class SlotRegistry<TKey> where TKey : TypeKey
{
    public void Register<T>() where T : TKey, new();   // code member
    public void Register(TKey member);                 // data member
    public void Freeze();                              // Register then throws
    public int  SlotOf(TKey key);                      // member → slot
    public TKey BySlot(int slot);                      // slot → member
    public bool TryBySlot(int slot, out TKey key);
    public T    Of<T>() where T : TKey;                // type → member
}

public static void AddKind<TRegistry, TKind>(this IContainerBuilder builder);
```

Call site:

```csharp
public sealed record PoisonTag() : Tag<int>(1001);    // game code
builder.AddKind<TagRegistry, PoisonTag>();             // game installer
if (entity.Type is Card) { ... }                       // game code
```

Access:

| Bypass route | What stops it |
|---|---|
| Read `key.Slot` outside Core | The property is `internal`. The code does not compile. |
| A positional root `record X(int Slot) : TypeKey(Slot)` makes a new public `Slot` | KIND-03. |
| Pass a tag to the entity type registry: `_types.SlotOf(poison)` | `SlotOf` takes only `TKey`. The code does not compile. |
| Read a state slot with the entity type registry: `_types.BySlot(ev.StateSlot)` | Only the wire field name. This is framework code, one apply handler for each kind space. |
| Add a member during a match | `Freeze()`. `Register` then throws. |
| Two members with the same slot | KIND-01 fails the gate. `Register` also throws. |
| A game member in the framework range | KIND-02 fails the gate. |

### 4.2 Parts

| Part | Home | Serves |
|---|---|---|
| `TypeKey` with an internal slot | `Framework.Core` | define a member, `SlotOf` |
| `SlotRegistry<TKey>` with `SlotOf` and `Freeze` | `Framework.Core` | all runtime entry points |
| Kind space: root record + registry subclass | the feature | define a kind space |
| `AddKind<TRegistry, TKind>()` | `Framework.Bootstrap` | add a game member |
| KIND-01, KIND-02, KIND-03 | rulebook | slot checks in the gate |

**TypeKey.** The root record is not positional. A positional parameter makes a new public property, so the slot is visible again. Member lines do not change.

```csharp
public abstract record TypeKey
{
    protected TypeKey(int slot) => Slot = slot;
    internal int Slot { get; }
}
public abstract record EntityType : TypeKey
{
    protected EntityType(int slot) : base(slot) { }
}
public sealed record Card() : EntityType(4);
```

Before step 1 lands, a compile test must prove two things: a positional member (`Card() : EntityType(4)`) can call the protected constructor of a non-positional root, and a feature assembly cannot read `Slot`.

**SlotRegistry.** The body is the same as today, plus `SlotOf` and `Freeze`. `SlotOf` is one field read, so the tag hot path keeps its speed.

```csharp
public int  SlotOf(TKey key) => key.Slot;
public void Freeze()         => _frozen = true;
void Add(TKey member)
{
    if (_frozen) throw new InvalidOperationException("Registry is frozen.");
    // clash check as today
}
```

**Kind space.** A feature that needs a new open set writes a root record, a sealed registry subclass with `WithBaselines()`, and its baseline members. There is no slot struct.

**AddKind.** The feature installer builds the registry with `WithBaselines()`, adds each recorded member, then calls `Freeze()`. `WireRegistrar.RegisterAll` uses the same build-callback approach. A data member calls `Register(member)` in the same callback. For a tag, `Register` also checks that the value type has a wire channel (`RegisterTagValueType<T>`).

```csharp
var tags = TagRegistry.WithBaselines();
foreach (var add in builder.KindsFor<TagRegistry>()) add(tags);
tags.Freeze();
builder.RegisterInstance(tags);
```

**Slot ranges.**

| Range | Owner |
|---|---|
| 0–999 | framework baselines |
| 1000 and up | game and test members |

The range applies to each kind space separately. Current framework slots (0–5) and sample slots (1001+, 2001+) already fit.

### 4.3 Architecture

```
composition, both sides
TagInstaller.Install
├─ TagRegistry.WithBaselines()
├─ AddKind members → TagRegistry.Register<PoisonTag>()   (clash → throw)
├─ TagRegistry.Freeze()
└─ builder.RegisterInstance(tags)

server
TagService.Set(entity, GameTags.Poison, 3)
├─ TagRegistry.SlotOf(GameTags.Poison) → 1001
└─ EventStream.Emit(TagSetEvent<int> { Slot: 1001 })
   ├─ TagSetHandler<int>.Apply → TagStore.Set(entity, 1001, 3)
   └─ WireForwarder → Network.Send

client
NetworkInboundBridge → EventStream.Emit(StatePushEvent { Slot: 1 })
└─ StatePushHandler.Apply
   └─ GameStateRegistry.BySlot(1) → PlayingState
```

### 4.4 Trade-offs

| Loss | Why it matters | Mitigation |
|---|---|---|
| Slot numbers stay hand-written | An author must pick a free number for each new member. | KIND-01 fails the gate on a clash. The number does not change across builds. |
| The slot is a bare `int` on the wire | An apply handler can call `BySlot` on the wrong registry. | Only framework code touches the wire field, one handler for each kind space. |
| Root records are not positional | A root needs a protected constructor in place of one line. | Only a feature author writes a root. KIND-03 catches a positional root. |
| Slot readers need the registry | Each service that writes a wire field injects its registry. | The registry is in the same feature. `SlotOf` costs one field read. |
| Frozen registries | No kind can be added during a match. | Content loads before composition, through `Register(member)`. |
| Reserved range 0–999 | A game cannot use low numbers. | The range is per kind space. |

## 5. Call-site migration

| Current code | New code | Change needed |
|---|---|---|
| `abstract record TypeKey(int Slot)` | `abstract record TypeKey` with protected ctor, `internal int Slot` | Core |
| `SlotRegistry<TKey>` | + `SlotOf`, `Freeze` | Core |
| `abstract record Tag(int Slot) : TypeKey(Slot)` | `Tag : TypeKey`, protected `int` ctor | the same for `EntityType`, `GameState` |
| `HealthTag() : Tag<int>(2001)` | no change | — |
| `key.Slot` in `TagService`, `type.Slot` in `EntityService`, `new TState().Slot` in `StateService` | `registry.SlotOf(key)` | inject the registry |
| `SampleTags.Health.Slot` in integration tests | `tags.SlotOf(SampleTags.Health)` | tests |
| `builder.Register<TagRegistry>(Lifetime.Singleton)` (empty) | build, fill, freeze, `RegisterInstance` | `TagInstaller`; the same for Entity and State |
| no game hook | `builder.AddKind<TRegistry, TKind>()` | `HandlerRegistrationExtensions` or a new `KindRegistrationExtensions` |

Wire fields stay `[Packed] int`. Apply handlers do not change.

## 6. Rulebook rules to propose

Proposals only. `rulebook.json` does not change in this spike.

| Id | Rule | Why | Check |
|---|---|---|---|
| KIND-01 | Slots are unique in each kind space. | A clash shows only at match start today, and never for tags. | `attached`: a reflection test in `Framework.Testing.Integration.Tests` (a friend of Core). For each sealed `TypeKey` subtype with a public parameterless ctor, make one instance, group by kind root, and fail on a duplicate. |
| KIND-02 | Framework members use slots 0–999. Members outside `Framework.*` use 1000 and up. | A game member must not take a framework slot that a later framework version needs. | The same test. |
| KIND-03 | A kind root record (a direct abstract subtype of `TypeKey`) is not positional. | A positional `Slot` parameter makes a public property and bypasses the registry. | The same test: fail if the root declares a public `Slot` property. |

## 7. Migration steps

Each step is one PR. Each step keeps `powershell -NoProfile -File Tools/Rulebook/gate.ps1` green.

1. **Core.** Change `TypeKey` to the protected ctor and the internal slot. Add `SlotOf`, `Freeze` and the compile test from §4.2. Add `InternalsVisibleTo` for the integration test assembly.
2. **States.** Move `GameState` to the non-positional root. `StateService` uses `SlotOf`. Build, fill and freeze `GameStateRegistry`. This is the smallest space, so it proves the shape.
3. **Entities.** The same for `EntityType`, `EntityService` and `EntityTypeRegistry`.
4. **Tags.** The same for `Tag` and `TagService`. Fill and freeze `TagRegistry`, with the value-type check. Register the sample tags in the test installers.
5. **Game hook.** Add `AddKind<TRegistry, TKind>()` and its tests.
6. **Rules.** Add the KIND-01/02/03 test and write the rules into `rulebook.json`, in a separate PR for user review.
7. **Docs.** Update `docs/lookup-patterns.md` (internal slot, `SlotOf`, freeze, `AddKind`, ranges, and remove "no framework lookup is off-standard") and `docs/architecture.md`.

## 8. Interfaces for the other pieces

- **Block and wire.** If the block piece moves `BlockKind` to a kind space, it uses this shape: `BlockKind : TypeKey`, `BlockKindRegistry : SlotRegistry<BlockKind>`, `[Packed] int Kind`, `SlotOf` on emit.
- **Record ids.** `TagStore` keys become `(EntityId, int slot)`. The order of the two migrations does not matter.
- **Type-keyed router.** A router key is a C# `Type` or a channel `byte`, not a slot. No shared interface.

## 9. Open questions

1. **`AddKind` home.** Put it in `HandlerRegistrationExtensions` or in a new `KindRegistrationExtensions` in Bootstrap?
2. **Data members.** Which installer reads data members (tag keys from assets) before `Freeze`? This depends on the data-authoring work.
3. **Debug names.** A data member has no type name for logs. Add an optional name to `Register(member)`?
