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

1. Keep the standard in `docs/lookup-patterns.md`: a kind is a record over a `TypeKey` base, and a registry maps slots to members.
2. The slot stays an authored `int`. It is the only identity on the wire and in data. The record is the identity in code.
3. The slot is `private` to `TypeKey`. The registry is nested in `TypeKey` (`TypeKey.Registry<TKey>`), so it is the only reader: `SlotOf(key)` and `BySlot(slot)`.
4. Code members register through a scan (`RegisterCodeMembers()`, reflection in v1, a source generator in v2). Data members register through `Register(member)`. Then the registry freezes.
5. `Tag<T>` is concrete. `PoisonTag : Tag<int>(1001)` and `new Tag<int>(1002)` both work.
6. The framework keeps slots 0–999. Games use 1000 and up.
7. Three gate rules: KIND-01 (unique slots), KIND-02 (slot ranges), KIND-03 (a kind root record is not positional).

### Decisions

1. (2026-11-01) The authored number stays the one wire identity. A gate rule checks that slots are unique. The options that remove the number (a count at composition, a hash of the type name) are rejected. Saves, replays and data need a number that does not change across builds.
2. (2026-11-01) Kind ids and record ids are two different patterns. They get two spikes and two pages.
3. (2026-11-01) No typed slot struct. The slot is an `int` and only the registry reads it. This replaces the slot structs (`TagSlot`, `EntityTypeSlot`, `StateSlot`) from decision 1.
4. (2026-11-01) Scope follows the two-tab page template (§2). Block kind, tag values, the state stack, the wire path and the asset format are out.
5. (2026-11-01) The registry is nested in `TypeKey`, and the slot is `private`. No `internal` slot and no friend assembly.
6. (2026-11-01) No `AddKind` installer call. v1: a reflection scan calls `Register<T>()` for each sealed member type. v2: a source generator writes the same list. Kinds are an exception to the "explicit registration" rule.
7. (2026-11-01) `Tag<T>` is a concrete record with a public `int` constructor, so data tags are instances. Data members have no name: code holds a reference, or uses `BySlot`.

## 2. Scope

| Concern | In / Out | Owner if out |
|---|---|---|
| Kind root record and its members (`Tag`, `EntityType`, `GameState`) | in | — |
| Slot number for each member: unique in its kind space, in the correct range | in | — |
| Registry: slot to member, member to slot, type to member | in | — |
| Registration at composition: code members, data members, then freeze | in | — |
| Slot access: only the registry reads the slot | in | — |
| Members from data, added before the freeze | in | — |
| Rules: unique slots, slot ranges, no positional root record | in | — |
| Instance ids (`EntityId`, `RequestId`, `BlockId`, `MessageId`) and their allocation | out | `infra-record-ids.md` |
| Wire event fields, packing and apply handlers | out | `Framework.Wire` and each feature |
| Asset format for data members | out | data-authoring work |
| Tag values: value types, `TagStore`, value checks | out | `Framework.Tags` |
| Game state stack (`Push`, `Pop`, current state) | out | `Framework.States` |
| `BlockKind`: change from an `enum` to a kind space | out | `infra-block-wire.md` (it can use this pattern) |
| Card definition ids in `Catalog` | out | `Framework.Entities` (Catalog) |
| Handler lookup by message type | out | `infra-type-router.md` |
| VS `TagKey` and `[TagSlot]` attribute | out | VS spike (deletable, not promoted) |

Boundary: the pattern receives kind records from code and from data at composition. It gives each feature a frozen registry. The feature turns a member into a slot (`SlotOf`) and a slot into a member (`BySlot`). What the feature does with the slot is out of scope.

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
| Sealed registry subclass over `SlotRegistry<TKey>` | all three | Tag: empty; others: a hand-written `WithBaselines()` list |
| Feature code reads `key.Slot` | all three | Number of sites |


## 4. Proposal

### 4.1 API

| Audience | Entry point | Does |
|---|---|---|
| author | `sealed record X() : Root(slot)` | Defines a code member. |
| author | `new Tag<int>(slot)` | Defines a data member. Only roots that allow data, for example tags. |
| author | `abstract record Root : TypeKey` + `TypeKey.Registry<Root>` | Defines a kind space. Feature authors only. |
| composition | `registry.RegisterCodeMembers()` | Finds every sealed member type and registers it. |
| composition | `registry.Register(member)`, `Freeze()` | Adds a data member; closes the registry. |
| runtime | `registry.SlotOf(key)`, `BySlot(slot)` | Member to slot, slot to member. |
| runtime | `registry.Of<T>()`, `is T` | Type to member; branch on a member. |

Shape:

```csharp
public abstract record TypeKey
{
    protected TypeKey(int slot);
    private readonly int _slot;                         // only Registry reads it

    public class Registry<TKey> where TKey : TypeKey
    {
        public void RegisterCodeMembers();              // scan: Register<T>() for each sealed member
        public void Register<T>() where T : TKey, new(); // one code member
        public void Register(TKey member);              // one data member
        public void Freeze();                           // Register then throws

        public int  SlotOf(TKey key);                   // member → slot
        public TKey BySlot(int slot);                   // slot → member
        public bool TryBySlot(int slot, out TKey key);
        public T    Of<T>() where T : TKey;             // type → code member
        public IReadOnlyCollection<TKey> Members { get; }
    }
}
```

Call site:

```csharp
public sealed record PoisonTag() : Tag<int>(1001);     // code tag
Tag<int> shield = new Tag<int>(1002);                  // data tag
if (entity.Type is Card) { ... }
```

Access:

| Bypass route | What stops it |
|---|---|
| Read the slot with no registry | The slot is `private` to `TypeKey`. Only the nested `Registry` reads it. |
| A positional root `record X(int Slot) : TypeKey(Slot)` makes a new public `Slot` | KIND-03. |
| Pass a tag to the entity type registry | `SlotOf` takes only `TKey`. The code does not compile. |
| Add a member during a match | `Freeze()`. `Register` then throws. |
| Two members with the same slot | `Register` throws at composition. KIND-01 fails the gate. |
| A game member in the framework range | KIND-02 fails the gate. |
| A data tag that was never registered | In the editor and dev builds, `SlotOf` checks that the member is registered. |

### 4.2 Parts

| Part | Home | Serves |
|---|---|---|
| `TypeKey` with a private slot | `Framework.Core` | define a member |
| `TypeKey.Registry<TKey>` | `Framework.Core` | `SlotOf`, `BySlot`, `Of<T>`, `Freeze` |
| Code member scan | `Framework.Core` | `RegisterCodeMembers()` |
| Data members: concrete `Tag<T>` | `Framework.Tags` | `new Tag<int>(slot)`, `Register(member)` |
| Kind space: root record + registry subclass | the feature | define a kind space |
| KIND-01, KIND-02, KIND-03 | rulebook | slot checks in the gate |

**TypeKey and Registry.** A nested type can read the private members of its outer type. So only the registry can read the slot.

```csharp
public abstract record TypeKey
{
    protected TypeKey(int slot) => _slot = slot;
    private readonly int _slot;

    public class Registry<TKey> where TKey : TypeKey
    {
        public int SlotOf(TKey key) => key._slot;    // one field read
        // Register, Freeze, BySlot, Of<T> as today
    }
}
```

**Kind space.** The root is not positional, because a positional parameter makes a new public `Slot` property. Member lines do not change. There is no hand-written baseline list.

```csharp
public abstract record EntityType : TypeKey
{
    protected EntityType(int slot) : base(slot) { }
}
public sealed class EntityTypeRegistry : TypeKey.Registry<EntityType> { }
public sealed record Card() : EntityType(4);
```

Before step 1 lands, a compile test must prove two things: a positional member (`Card() : EntityType(4)`) can call the protected constructor of a non-positional root, and code outside `TypeKey` cannot read `_slot`.

**Code member scan.** The scan finds each sealed subtype of `TKey` with a parameterless constructor and calls the `Register<T>()` path for it. A source generator replaces the scan in v2, with the same result.

```csharp
public void RegisterCodeMembers()
{
    foreach (var type in AllTypes())
        if (type.IsSealed && typeof(TKey).IsAssignableFrom(type)
            && type.GetConstructor(Type.EmptyTypes) != null)
            RegisterType(type);                     // same path as Register<T>()
}
```

**Data members.** A data tag is an instance, not a class. Code holds a reference to it. An asset can hold a list of them, and a loop registers each one before the freeze. Record equality includes the slot, so two instances with the same slot and `T` are equal.

```csharp
public record Tag<T> : Tag where T : unmanaged
{
    public Tag(int slot) : base(slot) { }
    public override Type ValueType => typeof(T);
}

foreach (var tag in tagAsset.Tags) tags.Register(tag);
```

`EntityType` and `GameState` stay abstract. Each root chooses whether it accepts data members.

**Slot ranges.**

| Range | Owner |
|---|---|
| 0–999 | framework members |
| 1000 and up | game and test members |

The range applies to each kind space separately. Current framework slots (0–5) and sample slots (1001+, 2001+) already fit.

### 4.3 Architecture

```
composition, both sides
TagInstaller.Install
├─ new TagRegistry()
├─ RegisterCodeMembers() → Register<PoisonTag>()   (clash → throw)
├─ foreach tag in asset → Register(tag)
├─ Freeze()
└─ builder.RegisterInstance(tags)

runtime
TagService.Set(entity, GameTags.Poison, 3)
└─ TagRegistry.SlotOf(GameTags.Poison) → 1001      (the feature uses the slot)
```

### 4.4 Trade-offs

| Loss | Why it matters | Mitigation |
|---|---|---|
| Slot numbers stay hand-written | An author must pick a free number for each new member. | A clash throws at composition and fails the gate. The number does not change across builds. |
| Registration is not explicit | `docs/lookup-patterns.md` asks for explicit registration. | Kinds become a written exception. `registry.Members` lists them. The v2 source generator gives a visible list. |
| Reflection scan at startup | The scan costs time, and IL2CPP stripping can remove a member type that no code uses. | The scan runs once at composition. A `link.xml` keeps the kind assemblies. The source generator removes both costs. |
| Data tags have no type | `Of<T>` and `is` do not find a data tag. | Code keeps a reference to the instance, or uses `BySlot`. |
| Root records are not positional | A root needs a protected constructor in place of one line. | Only a feature author writes a root. KIND-03 catches a positional root. |
| Frozen registries | No kind can be added during a match. | Data loads before composition, through `Register(member)`. |
| Reserved range 0–999 | A game cannot use low numbers. | The range is per kind space. |

## 5. Call-site migration

| Current code | New code | Change needed |
|---|---|---|
| `abstract record TypeKey(int Slot)` | `abstract record TypeKey` with protected ctor and `private int _slot` | Core |
| `SlotRegistry<TKey>` | `TypeKey.Registry<TKey>` + `SlotOf`, `Freeze`, `RegisterCodeMembers`, `Members` | Core; three registry subclasses |
| `abstract record Tag(int Slot) : TypeKey(Slot)` | `Tag : TypeKey`, protected `int` ctor | the same for `EntityType`, `GameState` |
| `abstract record Tag<T>(int Slot)` | concrete `record Tag<T>` with a public `int` ctor | Tags |
| `HealthTag() : Tag<int>(2001)` | no change | — |
| `key.Slot` in `TagService`, `type.Slot` in `EntityService`, `new TState().Slot` in `StateService` | `registry.SlotOf(key)` | inject the registry |
| `SampleTags.Health.Slot` in integration tests | `tags.SlotOf(SampleTags.Health)` | tests |
| `WithBaselines()` lists; empty `TagRegistry` | `new XRegistry()`, `RegisterCodeMembers()`, data loop, `Freeze()`, `RegisterInstance` | three feature installers |

Wire fields stay `[Packed] int`. Apply handlers do not change.

## 6. Rulebook rules to propose

Proposals only. `rulebook.json` does not change in this spike.

| Id | Rule | Why | Check |
|---|---|---|---|
| KIND-01 | Slots are unique in each kind space. | A clash shows only at match start today, and never for tags. | `attached`: a test in `Framework.Testing.Integration.Tests` builds each registry with `RegisterCodeMembers()`. `Register` throws on a clash. |
| KIND-02 | Framework members use slots 0–999. Members outside `Framework.*` use 1000 and up. | A game member must not take a framework slot that a later framework version needs. | The same test: for each entry in `Members`, check `SlotOf` against the assembly name. |
| KIND-03 | A kind root record (a direct abstract subtype of `TypeKey`) is not positional. | A positional `Slot` parameter makes a public property and bypasses the registry. | The same test: fail if the root declares a public `Slot` property. |

## 7. Migration steps

Each step is one PR. Each step keeps `powershell -NoProfile -File Tools/Rulebook/gate.ps1` green.

1. **Core.** Change `TypeKey` to the protected ctor and the private slot. Nest the registry, and add `SlotOf`, `Freeze`, `RegisterCodeMembers` and `Members`. Add the compile test from §4.2.
2. **States.** Move `GameState` to the non-positional root. `StateService` uses `SlotOf`. The installer builds the registry with the scan and freezes it. This is the smallest space, so it proves the shape.
3. **Entities.** The same for `EntityType`, `EntityService` and `EntityTypeRegistry`.
4. **Tags.** The same for `Tag` and `TagService`. Make `Tag<T>` concrete. Fill and freeze `TagRegistry`, with the value-type check.
5. **Rules.** Add the KIND-01/02/03 test and write the rules into `rulebook.json`, in a separate PR for user review.
6. **Docs.** Update `docs/lookup-patterns.md` (private slot, nested registry, scan, data members, freeze, ranges, the exception to explicit registration, and remove "no framework lookup is off-standard") and `docs/architecture.md`.
7. **v2.** Replace the scan with a source generator.

## 8. Interfaces for the other pieces

- **Block and wire.** If the block piece moves `BlockKind` to a kind space, it uses this shape: `BlockKind : TypeKey`, `BlockKindRegistry : TypeKey.Registry<BlockKind>`, `[Packed] int Kind`, `SlotOf` on emit.
- **Record ids.** `TagStore` keys become `(EntityId, int slot)`. The order of the two migrations does not matter.
- **Type-keyed router.** A router key is a C# `Type` or a channel `byte`, not a slot. No shared interface.

## 9. Open questions

1. **Scan scope.** Scan all loaded assemblies, or only assemblies that reference `Framework.Core`? The second is faster.
2. **Data asset.** Which asset type holds data tags, and how does it serialize `Tag<T>`? This depends on the data-authoring work.
