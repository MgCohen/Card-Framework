# Definition × Entity Setup

How **authored content** (cards, future heroes, zone defs, modifiers) maps to **runtime entities** in the tag-bag ECS world. Locks the three-field entity identity (`Id` / `Type` / `DefinitionId`), the def→entity transformation layer, and the spawn vocabulary.

## What this spike proves

- Every entity gets three first-class fields on the base: **`Id`** (auto-allocated int), **`Type`** (struct-wrapped int, extensible — Card / Player / Zone / Modifier / …), **`DefinitionId`** (nullable int into a per-type catalog). No string identities; no kind enum on the base.
- **Catalogs are per-type**: `CatalogOf<CardDefinition>`, future `CatalogOf<HeroDefinition>` etc. Lookup is `catalogs.Cards[entity.DefinitionId]`. Defs hold authored content; entities hold runtime state.
- **Three layers of construction**, by stability of intent:
  - **L1** `EntityStore.Create(type, defId)` — primitive; assigns id only.
  - **L2** `engine.From(def)` — extension that turns a `IEntityDefinition` into a fresh entity with base tags applied. Single place that knows def→tags.
  - **L3** `SpawnCard(def, owner, zone)` / `SpawnPlayer(...)` / `SpawnHand(owner)` / etc. — gameplay vocabulary. Each verb is one well-known shape; defless helpers (zones, players today) sit beside def-driven ones.
- **String tags banished from identity.** `CardName` / `ZoneName` / `PlayerName` removed — name is a presentation concern resolved through the catalog or kind marker.

## High-level idea

```
[Authored content]                   [Runtime]
                                                                                  
 CardDefinition(id:201,                Entity { Id=42, Type=Card, DefinitionId=201 }
   Name="Argent Squire",                  ├─ IsCard, IsMinion         ← kind markers
   Kind=Minion, Cost=1,                   ├─ Cost=1, Attack=1,         ← stat tags
   Attack=1, Health=1,                    │  Health=1, MaxHealth=1     
   DivineShield=true)                     ├─ DivineShield=true         ← ability flags
        │                                 └─ Owner=jaina.Id,           ← refs
        └─ Catalogs.Cards[201]               Zone=jainaField.Id
                  ▲
                  │ engine.SpawnCard(catalogs.Cards[201], owner, zone)
                  │  → L2 engine.From(def) builds tag bag
                  │  → L1 EntityStore.Create(Card, 201) assigns id
```

## How to validate

1. Open the project in Unity.
2. Menu → `Tools/EntitySetup/Build Sandbox Scene` (generates `Sandbox.unity`).
3. Open `Sandbox.unity` and Play.
4. IMGUI surface lists every spawned entity grouped by `EntityType` — Players, Zones, Cards. Each row shows the three identity fields plus full tag bag. Confirms the def→tag transformation produces the expected runtime shape and nothing else.

## Spike doc

Built from [`Product/spikes/definition-entity-setup.md`](../../../../Product/spikes/definition-entity-setup.md) — Q1–Q10 design pass, locks for identity shape, layer split, def vocabulary, and the explicit hand-waves (effect registration, catalog loading, hero/zone defs, modifier-entities). Status: **active, locked 2026-05-16**.
