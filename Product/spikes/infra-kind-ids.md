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
> state, a block kind. Its id is the **slot**: an authored number for the type, not for an
> instance. Instance ids (`EntityId`, `RequestId`, `BlockId`, `MessageId`) stay in
> `infra-record-ids.md`. No code changes yet.

## 1. TL;DR

1. Keep the standard in `docs/lookup-patterns.md`: a kind is a record over a `TypeKey` base, and a `SlotRegistry` maps slots to members.
2. The slot stays an authored number. It is the only identity on the wire and in data. The record type is the identity in code.
3. Each kind space gets a typed slot struct: `TagSlot`, `EntityTypeSlot`, `StateSlot`, `BlockKindSlot`. The base becomes `TypeKey<TSlot>`, and the registry becomes `SlotRegistry<TKey, TSlot>`.
4. Every kind space has a filled, frozen registry. Today `TagRegistry` is empty, so no code checks tag slots.
5. Games add kinds through one installer call. The framework keeps slots 0–999. Games use 1000 and up.
6. Two gate rules: KIND-01 (slots are unique in each space) and KIND-02 (the framework and games stay in their slot ranges).

### Decisions

1. (2026-11-01) Option D: the authored number stays the one wire identity. The slot is typed for each kind space. A gate rule checks that slots are unique. The options that remove the number (a count at composition, a hash of the type name) are rejected. Saves, replays and data need a number that does not change across builds.
2. (2026-11-01) Kind ids and record ids are two different patterns. They get two spikes and two pages.

## 2. Current kinds

| Kind space | Root record | Members today | Registry | Read by slot | Where |
|---|---|---|---|---|---|
| tag | `Tag`, `Tag<T>` | none in the framework; samples `HealthTag(2001)`, `CostTag(2002)`, `IsCardTag(2003)` | `TagRegistry`, **empty** | never: `TagStore` keys by `(entityId, slot)` | `Tags/Runtime/Tag.cs:6,11`; `Testing/Support/SampleTags.cs:9-11`; `Bootstrap/Runtime/TagInstaller.cs:15` |
| entity type | `EntityType` | `Unknown(0)`, `Player(2)`, `Zone(3)`, `Card(4)`, `Modifier(5)` | `EntityTypeRegistry.WithBaselines` | `EntityCreatedHandler` | `Entities/Runtime/EntityType.cs:5-11`; `Registry/EntityTypeRegistry.cs:7-16`; `Handlers/EntityCreatedHandler.cs:17` |
| game state | `GameState` | `MatchNotStartedState(0)`, `PlayingState(1)`, `MatchEndedState(2)` | `GameStateRegistry.WithBaselines` | `StatePushHandler` | `States/Runtime/GameState.cs:5`; `Registry/GameStateRegistry.cs:7-14`; `Handlers/StatePushHandler.cs:16` |
| block kind | none: `enum BlockKind : byte` | `None(0)`, `Command(1)`, `Effect(2)`, `Trigger(3)` | none | cast to `byte` | `Wire/Runtime/Events/BlockKind.cs:3`; `BlockScope.cs:15` |
| VS tag | `TagKey<T>` class, or `[TagSlot(n)] class X : ITagKey<T>` | 19 static keys in `Sample/Tags.cs`, `Deathrattle(100)` | static `TagRegistry` | — | `Spike/ECS/VerticalSlice/Engine/Tags/TagKey.cs`, `TagSlotAttribute.cs`; `Sample/Deathrattle.cs:34` |

Paths without a prefix are under `Assets/Framework/`. Five findings:

1. **Tag slots have no check.** `TagInstaller` registers an empty `TagRegistry`. `TagService` and `TagStore` use `key.Slot` directly. Two tags with the same slot and the same value type share one storage cell. No error shows.
2. **Games cannot add kinds.** `EntityInstaller` and `StateInstaller` register `WithBaselines()` as an instance. No installer call adds a game member.
3. **A clash shows late.** `SlotRegistry.Add` throws when the registry fills, when a match starts. Nothing checks in the gate.
4. **Bare `int` slots.** `TagSetEvent.Slot`, `EntityCreatedEvent.TypeSlot` and `StatePushEvent.Slot` are all `int`. A tag slot can go where a state slot goes.
5. **No slot ranges.** The framework uses 0–5. The samples use 1001 and 2001. Nothing keeps a game out of the framework numbers.

The VS has two authoring paths and a static registry. `lookup-patterns.md` forbids a static registry. The VS is the reference for what not to keep, and phase 8 deletes it.

## 3. Proposal

### 3.1 Parts

| Part | Home | Role |
|---|---|---|
| `TypeKey<TSlot>` | `Framework.Core` | base record of every kind |
| `TagSlot`, `EntityTypeSlot`, `StateSlot`, `BlockKindSlot` | the feature of each kind | typed slot, `readonly struct` around `int` |
| `SlotRegistry<TKey, TSlot>` | `Framework.Core` | maps slot and type to member; frozen after composition |
| `AddKind<TRegistry, TKind>()` | `Framework.Bootstrap` | installer call that adds one code member |
| KIND-01, KIND-02 | rulebook | unique slots; slot ranges |

### 3.2 Base record and typed slot

```csharp
namespace Framework.Core
{
    public abstract record TypeKey<TSlot>(TSlot Slot) where TSlot : unmanaged, IEquatable<TSlot>;
}

namespace Framework.Tags
{
    public readonly struct TagSlot : IEquatable<TagSlot>
    {
        public readonly int Value;
        public TagSlot(int value) => Value = value;
        public bool Equals(TagSlot other) => Value == other.Value;
        // ==, !=, Equals(object), GetHashCode, ToString => $"Tag@{Value}"
    }

    public abstract record Tag : TypeKey<TagSlot>
    {
        protected Tag(int slot) : base(new TagSlot(slot)) { }
        public abstract Type ValueType { get; }
    }

    public abstract record Tag<T> : Tag where T : unmanaged
    {
        protected Tag(int slot) : base(slot) { }
        public override Type ValueType => typeof(T);
    }
}
```

The slot struct has no `None`. Slot 0 can be a real member (`Unknown() : EntityType(0)`). There is no implicit conversion from `int`. The protected `int` constructor of the root record is the only place where a bare number becomes a slot.

Before step 1 lands, a compile test in Core must prove that a positional member (`HealthTag() : Tag<int>(2001)`) can call a protected base constructor of a non-positional root.

**Why one struct for each space, not `Slot<TKind>`.** A closed generic name in an AutoPacker `Packed` struct has no test today. Four named structs also match the typed record ids (`EntityId`, `BlockId`).

### 3.3 Registry

```csharp
public class SlotRegistry<TKey, TSlot>
    where TKey : TypeKey<TSlot> where TSlot : unmanaged, IEquatable<TSlot>
{
    public void Register<T>() where T : TKey, new();   // code member
    public void Register(TKey member);                 // data member
    public void Freeze();                              // after composition; Register then throws

    public TKey BySlot(TSlot slot);
    public bool TryBySlot(TSlot slot, out TKey key);
    public T    Of<T>() where T : TKey;                // code members only
}
```

The body is the same as today, plus `Freeze`. Each kind space keeps its own sealed subclass (`TagRegistry : SlotRegistry<Tag, TagSlot>`) with a static `WithBaselines()`.

### 3.4 Define a new member

A member is one line. It is registered in an installer:

```csharp
public sealed record PoisonTag() : Tag<int>(1001);         // game code

builder.AddKind<TagRegistry, PoisonTag>();                 // game installer
```

`AddKind` records the member type. The feature installer builds the registry with `WithBaselines()`, then adds each recorded member, then calls `Freeze()`. `WireRegistrar.RegisterAll` uses the same build-callback approach. A member from data calls `Register(instance)` in the same callback, before `Freeze`.

For a tag, `Register` also checks that `ValueType` is a registered tag value type (`RegisterTagValueType<T>`). Without that check, a `Tag<float>` compiles but has no wire channel.

### 3.5 Define a new kind space

Four files and one installer line:

1. `XSlot` struct, next to the kind.
2. `abstract record X : TypeKey<XSlot>` with a protected `int` constructor.
3. `sealed class XRegistry : SlotRegistry<X, XSlot>` with `WithBaselines()`.
4. The baseline members, one record each.
5. In the feature installer: build, fill and freeze the registry, and register it as an instance.

`BlockKind` is the next kind space. The block piece moves it from an `enum` to this shape (`infra-block-wire.md`).

### 3.6 Read a kind

| Where | Path | Call |
|---|---|---|
| Wire event field | slot | `[Packed] public TagSlot Slot;` |
| Apply handler, data load, save | slot → member | `Registry.BySlot(slot)` |
| Feature and game code | type → member | `Registry.Of<PoisonTag>()`, or a static alias (`GameTags.Poison`) |
| Branch on a received member | type test | `if (state is PlayingState)` |
| Generic verb (only where a consumer needs it) | type argument | `StateService.Push<PlayingState>()` |

Tags are a special case. `TagStore` needs only the slot and `T`, so a tag apply handler does not call `BySlot`. The registry is the authority for clashes and for data lookup.

### 3.7 Slot ranges

| Range | Owner |
|---|---|
| 0–999 | framework baselines |
| 1000 and up | game and test members |

The range applies to each kind space separately. Current framework slots (0–5) and sample slots (1001+, 2001+) already fit.

## 4. Call-site migration

| Current code | New code | Change needed |
|---|---|---|
| `abstract record TypeKey(int Slot)` | `abstract record TypeKey<TSlot>(TSlot Slot)` | Core |
| `SlotRegistry<TKey>` | `SlotRegistry<TKey, TSlot>` + `Freeze` | Core; three registry subclasses |
| `abstract record Tag(int Slot) : TypeKey(Slot)` | `Tag : TypeKey<TagSlot>`, protected `int` ctor | the same for `EntityType`, `GameState` |
| `HealthTag() : Tag<int>(2001)` | no change | — |
| `[Packed] int Slot` on Tag and State events, `int TypeSlot` on `EntityCreatedEvent` | `TagSlot`, `StateSlot`, `EntityTypeSlot` | apply handlers |
| `TagStore` `Map<int, int, T>` | `Map<EntityId, TagSlot, T>` | after `EntityId` lands (`infra-record-ids.md`) |
| `builder.Register<TagRegistry>(Lifetime.Singleton)` (empty) | build, fill, freeze, `RegisterInstance` | `TagInstaller`; the same for Entity and State |
| no game hook | `builder.AddKind<TRegistry, TKind>()` | `HandlerRegistrationExtensions` or a new `KindRegistrationExtensions` |
| `enum BlockKind : byte` | `BlockKind : TypeKey<BlockKindSlot>` | the block piece |

## 5. What stays specialized, and why

- **Slot allocation.** No allocator makes slots, because a counted or hashed slot changes across builds (decision 1).
- **Definition ids (`Catalog<TDef>`).** A definition is data for an instance, not a kind. Its id comes from data. A typed `DefinitionId` is a later step.
- **Channel ids (`Network`).** A channel id is a router key that both sides compute from the sorted registration. It is a `byte`, and it is not authored.
- **Value types of tags.** A value type is code only. It needs a wire channel and a handler for each `T`. It is not a kind.

## 6. Rulebook rules to propose

Proposals only. `rulebook.json` does not change in this spike.

| Id | Rule | Why | Check |
|---|---|---|---|
| KIND-01 | Slots are unique in each kind space. No two sealed `TypeKey<TSlot>` subtypes with the same `TSlot` have the same slot. | A clash shows only at match start today, and never for tags. | `attached`: a reflection test in `Framework.Testing.Integration.Tests`. For each sealed subtype with a public parameterless ctor, make one instance, group by `TSlot`, and fail on a duplicate. |
| KIND-02 | Framework members use slots 0–999. Members outside `Framework.*` use 1000 and up. | A game member must not take a framework slot that a later framework version needs. | The same test as KIND-01. |
| KIND-03 | Slots on the wire are typed. No `WireEvent` subtype in `Framework.*` has a public `int` field whose name ends in `Slot`. | A bare `int` lets a tag slot go where a state slot goes. | `attached`: the reflection test for ID-04 in `infra-record-ids.md`. |

## 7. Migration steps

Each step is one PR. Each step keeps `powershell -NoProfile -File Tools/Rulebook/gate.ps1` green. Steps 2–4 touch wire events, so they also run `-With playmode`.

1. **Core.** Add `TypeKey<TSlot>`, `SlotRegistry<TKey, TSlot>` with `Freeze`, and the compile test from §3.2. Keep the old `TypeKey` until step 4.
2. **States.** Add `StateSlot`. Move `GameState`, `GameStateRegistry`, `StatePushEvent` and `StatePushHandler`. This is the smallest space, so it proves the shape.
3. **Entities.** Add `EntityTypeSlot`. Move `EntityType`, `EntityTypeRegistry`, `EntityCreatedEvent.TypeSlot` and the handler.
4. **Tags.** Add `TagSlot`. Move `Tag`, `Tag<T>`, the three tag events and handlers. Fill and freeze `TagRegistry`, with the value-type check. Register the sample tags in the test installers. Delete the old `TypeKey` and `SlotRegistry<TKey>`.
5. **Game hook.** Add `AddKind<TRegistry, TKind>()` and its tests.
6. **Rules.** Add the KIND-01/02/03 test and write the rules into `rulebook.json`, in a separate PR for user review.
7. **Docs.** Update `docs/lookup-patterns.md` (typed slot, freeze, `AddKind`, ranges, and remove "no framework lookup is off-standard") and `docs/architecture.md`.

## 8. Interfaces for the other pieces

- **Block and wire.** `BlockKind : TypeKey<BlockKindSlot>`, `BlockKindRegistry : SlotRegistry<BlockKind, BlockKindSlot>`, and `[Packed] public BlockKindSlot Kind` on `BlockStartEvent`. The block spike shows `[Packed] public int Kind` today.
- **Record ids.** `TagStore` keys become `(EntityId, TagSlot)`. The order of the two migrations does not matter.
- **Type-keyed router.** A router key is a C# `Type` or a channel `byte`, not a slot. No shared interface.

## 9. Open questions

1. **`AddKind` home.** Put it in `HandlerRegistrationExtensions` or in a new `KindRegistrationExtensions` in Bootstrap?
2. **Data members.** Which installer reads data members (tag keys from assets) before `Freeze`? This depends on the data-authoring work.
3. **Debug names.** The VS `TagKey` has a `Name` for logs. The record type name gives the same in code, but a data member has no type. Add an optional name to `Register(member)`?
