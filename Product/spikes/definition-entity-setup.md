---
type: spike
status: active
tags: [#spike, #entities, #definition, #ecs]
references:
  - Product/spikes/modifiers-as-entities.md
sandbox: Assets/Spike/ECS/EntitySetup/
---

# Definition × Entity Setup

> **Status: active.** Captures the locked shape from the design pass on 2026-05-16. Live sandbox at [`Assets/Spike/ECS/EntitySetup/`](Assets/Spike/ECS/EntitySetup/) — open in Unity, run `Tools/EntitySetup/Build Sandbox Scene`, Play.

## Context

Working in the tag-bag ECS world of [`Assets/Spike/ECS/VerticalSlice/`](Assets/Spike/ECS/VerticalSlice/). The slice's `Entity` is a single concrete class with `int Id` + sparse `TagKey<T> → value` dict; the `EntityStore` indexes those tags ([Entity.cs](Assets/Spike/ECS/VerticalSlice/Engine/Entities/Entity.cs), [EntityStore.cs](Assets/Spike/ECS/VerticalSlice/Engine/Pipeline/EntityStore.cs)).

Before this pass, the slice had no definition concept — cards were built imperatively in [SliceBootstrap.MakeMinion](Assets/Spike/ECS/VerticalSlice/Unity/SliceBootstrap.cs#L190). This spike works out **how authored content (cards, future zones, future heroes, future modifiers) relates to runtime entities** in the tag-bag world.

The framework-level docs in [`Product/features/entities/models/`](Product/features/entities/models/) describe a `EntityInstance<TDef>` handle + `EntityState (Bases, Modifiers)` slice model. **That shape is valid at the theory level but not the spike's implementation target** — the spike runs a tag-bag ECS, not the bridge/store/handle model.

## Locked decisions

The design pass walked Q1 through Q10. Each lock here references the question it answers.

### Identity (Q on entity base)

**Three first-class fields on `Entity`**, HS-shape:

| Field | Type | Role |
|---|---|---|
| `Id` | `int` | Instance identity — auto-allocated by `EntityStore` |
| `Type` | `EntityType` | Catalog partition — tells which dict to look up `DefinitionId` against |
| `DefinitionId` | `int` | Content reference — `0` for defless / generic entities |

`EntityType` is a `readonly record struct(int Slot, string Name)` — open registry, TagKey-style. Equality on `Slot` only; `Name` is debug-resolved via `EntityTypeRegistry`. Games extend by declaring more `static readonly EntityType` fields. **Not an enum.** (`feedback_int_over_enum_for_open_types`.)

All identity/ref/bookkeeping is `int` (or `Guid` if cross-run stability is needed). No `string` IDs. No `long`. (`feedback_int_ids_over_strings`.)

The full registry shape is locked in the [2026-05-18 closure](#closure-2026-05-18--entitytype-registry).

### Q1 — Definition representation: **plain C# + SO wrapper**

Definitions are plain C# records at runtime (`CardDefinition`, future `ZoneDefinition`, etc.). ScriptableObject wrappers exist for editor authoring + serialization, with `ToRuntime()` producing the record. The catalog stores the records; SOs are an *authoring source*.

### Q2 — One definition shape per type: **distinct typed records**

`CardDefinition`, `ZoneDefinition`, `PlayerDefinition` — separate records per `EntityType`. Pairs naturally with catalog-per-type. Fields stay tight to what each type actually needs.

### Q3 — Spawn shape: **free static factory functions**

Defs stay pure data. Spawning lives in extension methods on `GameEngine`. No `Build()` on defs, no `EntitySpawner` service.

### Q4 — Generic / defless entities: **stay defless for now**

Zones / players don't have definitions yet. Bootstrap builds them inline via helpers (`SpawnHand`, `SpawnBattlefield`, `SpawnPlayer`). Zones will likely earn defs later; player→hero def is flagged as a follow-up (see "Open" below).

### Q5 — Effects on the def: **data-asset list**

Card def carries `IReadOnlyList<CardEffect>`. Effects are themselves data-assets carrying all required definitions. Registration mechanics (how the spawn helper wires them into an EffectStore) deferred along with the modifier shape — see [`modifiers-as-entities.md`](modifiers-as-entities.md).

### Q6 — SO authoring of effects: **parked**

Deferred with the broader effect-as-data-asset question.

### Q7 — Catalog ownership: **injected**

`Catalogs` is constructed outside the engine and passed in via the engine's constructor. Content lifetime ≠ match lifetime; reset rebuilds the engine against the same catalog. No globals, full test isolation.

### Q9 — Def vs spawn responsibilities

- **Def** = content + base values. Has zero runtime concerns and zero dealing-rule semantics.
- **Spawn** = creates the instance and handles placement.

Owner / zone / face-up-down / per-instance overrides all live at the spawn call, not the def.

### Q10 — Layered Spawn API

Three layers:

1. **L1 — `EntityStore.Create(type, defId)`.** Defless primitive. Allocates id, stamps fields, registers in lookup. Always public.
2. **L2 — `engine.From(def)`.** Generic def-driven helper. Reads the `IEntityDefinition` base contract. Today plumbing-only; earns its keep when the base contract grows base parameters.
3. **L3 — `engine.SpawnCard(def, owner, zone)`, `engine.SpawnHand(owner)`, `engine.SpawnPlayer(...)`, etc.** Type-specific extension methods. Framework ships a few; games add their own with the same pattern.

**Defless entities still get L3 helpers** when their shape is conceptually complete (Hand / Battlefield / Graveyard / Player). Reaching into raw `EntityStore.Create()` at the bootstrap level is reserved for truly bespoke entities.

### String tags out

`CardName`, `ZoneName`, `PlayerName` all removed. Content-named entities (cards) get their name from the def via `catalogs.Cards[entity.DefinitionId].Name`. Category-named entities (zones) get their "name" from the kind marker (`IsHand` / `IsBattlefield` / `IsGraveyard`) as a presentation constant. Player names push toward a HeroDefinition (open follow-up).

## The shape, in code

Live sandbox under [`Assets/Spike/ECS/EntitySetup/`](Assets/Spike/ECS/EntitySetup/):

| File | Role |
|---|---|
| `EntityType.cs` | Struct-wrapped int + `EntityTypes` static class |
| `TagKey.cs` | Typed tag keys (slot + name) |
| `Tags.cs` | Tag inventory — kind markers, stats, ability flags, refs |
| `Entity.cs` | Three-field base + sparse slot dict |
| `EntityStore.cs` | L1 primitive — `Create(type, defId)` |
| `Definition.cs` | `IEntityDefinition`, `CardKind` enum, `CardDefinition` record, `CardEffect` placeholder |
| `Catalog.cs` | `CatalogOf<TDef>` + `Catalogs` per-type bag |
| `Engine.cs` | `GameEngine` + L2 `From(def)` extension |
| `Spawn.cs` | L3 — `SpawnCard` (def-driven), `SpawnPlayer` / `SpawnHand` / `SpawnBattlefield` / `SpawnGraveyard` (defless) |
| `EntitySetupBootstrap.cs` | MonoBehaviour + IMGUI dump showing every spawned entity's three-field identity + tag bag |
| `Editor/SetupSceneBuilder.cs` | One-shot scene builder under `Tools/EntitySetup/Build Sandbox Scene` |

Sketch of the full setup flow (extracted from `EntitySetupBootstrap`):

```csharp
// Content — built once.
var catalogs = new Catalogs();
catalogs.Cards.Add(new CardDefinition(
    Id: 201, Name: "Argent Squire", Kind: CardKind.Minion,
    Cost: 1, Attack: 1, Health: 1, DivineShield: true,
    Effects: Array.Empty<CardEffect>()));

// Engine — rebuilt on reset, same catalogs.
var engine = new GameEngine(catalogs);

// Defless entities — L3 helpers wrapping L1.
var jaina      = engine.SpawnPlayer(health: 30, armor: 0);
var jainaHand  = engine.SpawnHand(owner: jaina);
var jainaField = engine.SpawnBattlefield(owner: jaina);

// Def-driven entities — L3 helpers consuming the catalog, going via L2.
var squire = engine.SpawnCard(catalogs.Cards[201], owner: jaina, zone: jainaField);

// At this point, squire has:
//   Id           = 1+n (auto-allocated)
//   Type         = EntityTypes.Card
//   DefinitionId = 201
//   tags         = IsCard, IsMinion, Cost=1, Attack=1, Health=1, MaxHealth=1,
//                  DivineShield=true, Owner=jaina.Id, Zone=jainaField.Id
//
// No string name on the entity. UI reads catalogs.Cards[201].Name when it
// needs the label.
```

## What's hand-waved / deferred

- **`IEntityDefinition` base contract contents.** Today `Id` + `Type` only. Candidates that may earn their place: `Name` (debug/display), a base-tag enumeration, asset/visual refs. Will surface when there's pressure; L2 `From(def)` is the single place that would consume them.
- **Effect registration mechanics.** Spawn iterates `def.Effects` as a placeholder; nothing connects them to an EffectStore yet. Bundled with the parked modifier work.
- **Catalog loading flow.** Code-defined only in the spike. SO wrapping + `Resources.LoadAll` / manifest / explicit registration is a separate concern.
- **HeroDefinition / PlayerDefinition.** "Jaina" / "Garrosh" are content. Promoting Player to def-driven is the natural follow-up; today bootstrap fakes it with no name tag (UI would render "Player 1" / "Player 2" until heroes earn defs).
- **Zone definitions.** Defless for now. Hand / Battlefield / Graveyard are categories, not authored content. When zones get defs (custom visibility rules, capacity, special behaviors), `SpawnZone(def, owner)` joins the family alongside the defless helpers.
- **Modifier-entities.** See [`modifiers-as-entities.md`](modifiers-as-entities.md). The Definition × Entity shape locked here is the foundation modifiers compose on top of.
- **Presentation / VM side.** How [`Assets/Spike/ECS/CardPresentation/`](Assets/Spike/ECS/CardPresentation/) reaches the catalog for Art / BodyText is a separate spike concern.

## Validating the sandbox

1. Open the project in Unity.
2. Run `Tools/EntitySetup/Build Sandbox Scene`. This generates `Assets/Spike/ECS/EntitySetup/Sandbox.unity`.
3. Open `Sandbox.unity` and Play.
4. The IMGUI surface lists every spawned entity grouped by `EntityType` — Players, Zones, Cards. Each row shows the three identity fields (`Id`, `Type`, `Def → catalog name`) and the full tag bag.
5. Click **Reset** to rebuild the engine. Notice the entity count restarts but the catalog count is unchanged — content survives, match state doesn't.

What you should see exercised by playing:

- Two players, three zones each, six cards distributed across hands and battlefields.
- Same `CardDefinition` (Fireball, id 101) spawned twice — into Jaina's hand and Garrosh's hand. Same `DefinitionId`, different `Id` and different `Owner` / `Zone` tags. Def is placement-agnostic; spawn handles placement.
- Argent Squire's `DivineShield` tag set from the def's intrinsic.
- No `CardName` / `ZoneName` / `PlayerName` tags anywhere. The IMGUI panel reads card names through the catalog by `DefinitionId`.

## Closure 2026-05-18 — EntityType registry

Follow-up design pass after the spike landed. Sets the registry shape, the engine/game split, and the marker-tag retirement that the original Q1–Q10 pass left implicit.

### Identity rule

The discriminator for "field vs tag" is now explicit:

> **Anything fixed for the entity's lifetime is a field. Anything mutable is a tag.**

`Id`, `Type`, `DefinitionId` qualify. Future facets must pass the immutability test before being promoted; `Owner` and `Zone` are mutable and stay tags forever.

### Pattern split (mirrors the tag pattern split)

- **Pattern A — code-defined.** Static fields on a game-side class. Primary mechanism.
- **Pattern B — dropped.** A keyword-class form (`record Card : EntityType(1)`) doesn't earn its keep here — EntityType has no typed payload (unlike Pattern B tags, which carry `T`). Pattern B for entity types is just Pattern A with extra ceremony.
- **Pattern C — designer-authored.** `EntityTypeAsset` ScriptableObject with `Slot`, `Name`, optional `DefType` binding, optional `Presentation`. Mirrors TagAuthoring's Pattern C shape. Defless-by-design is allowed (a Pattern C type may have no paired def).

### Engine-reserved slots

The engine ships these. Games extend.

| Slot | Name | Notes |
|---|---|---|
| 0 | `Unknown` | Sentinel; mirror falls back here for unknown ints on the wire |
| 1 | *(reserved)* | Game-as-entity — pending StateMachine spike review |
| 2 | `Player` | |
| 3 | `Zone` | |
| 4 | `Card` | |
| 5 | `Modifier` | Engine-reserved; concrete shape TBD when the modifier spike runs |

No range convention beyond engine reservations — collision is guarded by `EntityTypeRegistry`. Range discipline can be introduced later if it bites.

### API surface

```csharp
public readonly record struct EntityType(int Slot, string Name);
// Equality on Slot only; Name is debug-resolved via EntityTypeRegistry.
```

`EntityTypeRegistry` mirrors `TagRegistry` — process-global today, scoped per-World eventually.

### Wire

`EntityCreatedEvent` carries `(Id, Type.Slot, DefinitionId)` so the mirror knows kind at creation. The name is *not* on the wire — the receiver resolves it through its local registry. Unknown slots survive as `Unknown(N)` with the original int preserved for debug; cross-version replay degrades gracefully rather than crashing.

### Marker tags retired

With `Type` first-class, `IsCard` / `IsZone` / `IsPlayer` are pure redundancy and go. Sub-kinds *within* a Type stay as tags — the natural shape is one enum tag (`CardKind = Minion | Spell | Weapon`) rather than three boolean markers. Rule: **Type partitions catalogs; sub-kinds are tags.**

### Notes carried over

- `DefinitionId` stays `int` with `0` = defless (preserved from Q4).
- The original Q-pass used `Value` for the `EntityType` int field. The closure locks `Slot` for consistency with `TagKey.Slot` and the broader registry vocabulary.

## Follow-ups visible from this spike

- **`EntityType.Game` decision.** Slot 1 reserved; whether the match becomes an entity (vs. an ambient `MatchSingleton`) belongs to the StateMachine spike.
- **Modifier shape.** Slot 5 reserved; whether modifiers attach as separate entities or as tags-on-host is the next big call.
- **HeroDefinition / PlayerDefinition Q.** "Jaina" is content; she earns a def.
- **`IEntityDefinition` base parameters Q.** What pressure makes us add fields beyond `Id` + `Type`?
- **Catalog loading flow.** SO scan vs explicit registration vs manifest.
- **Effect data-asset shape.** Bundled with the modifier-as-entities work.
- **Pattern C slot governance.** Hand-picked today; auto-allocation if/when content teams hit the friction.
- **Presentation / VM integration.** Card presentation reads through the catalog.
