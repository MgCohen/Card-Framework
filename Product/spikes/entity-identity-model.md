---
type: spike
status: draft
forward-link: TBD
references:
  - Product/spikes/wire-typed-payloads.md
  - Product/spikes/definition-entity-setup.md
  - Product/spikes/card-presentation.md
  - Product/research/sabberstone-architecture.md
tags: [#spike, #entity, #identity, #rollback, #ecs, #handles]
---

# Entity Identity Model — spike (draft)

## Premise

The wire/event-sourcing direction (locked in `wire-typed-payloads.md`) means game state is reconstructable by replaying events forward and backward. That has a consequence we haven't designed for: **what is an "entity reference" in a world where rollback can destroy an entity that something outside the event stream is holding?**

Three concrete failure modes the current spikes exhibit:

1. **Silent post-destroy writes.** In `TypedPayloads`, `EntityStore.Destroy(e)` removes the entity from the dict and unsubscribes from its tag events. The held reference is still alive; `e.Set(...)` still mutates the slot bag but no wire event is emitted. Holder's view and authoritative stream silently diverge.
2. **Unrewindable destroy.** `MirrorWorld.Unapply(EntityDestroyedEvent)` re-creates an empty entity because the prior tag bag isn't on the wire — explicitly flagged as broken at [`MirrorWorld.cs:55-59`](../../Assets/Spike/ECS/TypedPayloads/Engine/MirrorWorld.cs).
3. **Unenforced "go through the store" discipline.** `CardEntity` has public mutable fields; `World.SetCardZone` is the *intended* writer, but the type doesn't prevent direct mutation.

This spike resolves: **what is an Entity, and what shape does identity take, such that rollback is correct by construction?**

## Audit of current spikes

Three entity implementations on disk, three different facets of the same problem.

### 1. `Spike/ECS/EntitySetup/Entity.cs` — construction-flow spike

- `class Entity`, public mutable `Id`/`Type`/`DefinitionId`, private slot bag.
- `EntityStore.Destroy(id)` = `_entities.Remove(id)`. Held references survive intact, fully writable.
- Constructed via def-driven `Spawn*` extension methods on `GameEngine`.

### 2. `Spike/ECS/TypedPayloads/Engine/Entity.cs` — wire-emission spike

- `class Entity` with **internal events** that `EntityStore` subscribes to per-instance for wire emission.
- `EntityStore.Destroy(e)`: removes from dict, unsubscribes events. Held reference is still callable — `Set` mutates the bag silently with no wire entry. **Worst failure mode of the three.**
- Mirror side has the unrewindable-destroy gap.

### 3. `Spike/ECS/CardPresentation/World/CardEntity.cs` + `World.cs` — VM/intent spike

- `class CardEntity` with **public fields** (no encapsulation).
- Intended writes go through `World.SetCardZone(cardId, zoneId)`, but nothing enforces it.
- **But:** `CardViewModel` keys on `CardId` (not a reference) and looks up via `World.GetCard(id)` each refresh. The *consumer pattern* is already handle-shaped, even though the type isn't.

### What's wrong with all three

All three are the **broken hybrid**: class-typed entities with no liveness story. Reference-type ergonomics, no reference-type validity guarantees, no generational protection, silent divergence after destroy. They are neither of the two coherent answers below.

## Research — how mainstream systems handle this

### SabberStone (Hearthstone simulator, C#) — Answer A: "no destroy, ever"

Sources: `SabberStoneCore/src/Model/Entities/Entity.cs`, `Game.cs`, `Zones/BoardZone.cs`, `Kettle/PowerHistory.cs`.

- **`Entity` is a `partial class`**, not sealed, not abstract. `Minion`/`Spell`/`Hero`/`Player` derive from it. Reference type, heap-allocated, passed by reference.
- **Tags live on the instance.** Each `Entity` holds an `EntityData` field — a wrapped `Dictionary<GameTag, int>`. There is **no centralized tag store keyed by entity id**. The entity *is* the bag.
- **Identity is dual.** Stable `int Id` set at construction (used for the wire / PowerHistory). `Game.IdEntityDic` is an int-indexed lookup table, but it stores references, not tag data.
- **There is no destroy.** Entities are **never removed from `IdEntityDic`**. "Death" is modeled as:
  - a `ToBeDestroyed` flag (mirrors into a `TO_BE_DESTROYED` tag)
  - a `ZONE` tag change (e.g. → `GRAVEYARD`)
  - `BoardZone.Remove` just unlinks from the board array; the entity stays in `IdEntityDic`
- The same object lives in the registry for the entire life of the `Game`.
- **No generation counter. No `IsAlive`. No `IsDisposed`.** Not needed — nothing is ever destroyed or recycled.
- **Wire emission is on `Entity`'s indexer.** `this[GameTag t] = value` calls `Game.PowerHistory.Add(...)` directly when `_history` is true and the effective value changed. `PowerHistoryTagChange` carries `EntityId`, not a reference — the wire is id-based even though the in-memory API is ref-based.

**Why this avoids the stale-reference hazard:** by elimination. You can't have a stale reference because nothing is destroyed. The worst you get is *semantic* staleness (you hold a ref to a minion now in the graveyard); detect it by reading `ZONE` or `ToBeDestroyed`. The reference itself is always safe to dereference.

### Mainstream ECS — Answer B: "value handle with generation"

Surveyed Bevy, Unity DOTS, EnTT, flecs, specs. Plus Unity GameObject as the cautionary contrast.

| System | Entity type | Generation encoding | Stale access behavior |
|---|---|---|---|
| **Bevy (Rust)** | `struct Entity { index: NonMaxU32, generation: u32 }` (8 bytes, value) | Separate field | `Result<_, InvalidEntityError>` |
| **Unity DOTS (C#)** | `struct Entity { int Index; int Version; }` (8 bytes, value) | Separate field | `ArgumentException` |
| **EnTT (C++)** | `uint32_t` (or 64-bit), strong typedef | Bit-packed (20 id / 12 gen typical) | UB in release, assert in debug — caller must `valid()` |
| **flecs (C)** | `uint64_t` | Bit-packed (low 32 id / high 32 gen+flags) | Explicit failure (`ecs_is_alive` guards) |
| **specs (Rust)** | `struct Entity(u32, Generation)` (value) | Separate field | `Option<&T>` returns `None` |
| **Unity GameObject** | `class`, heap reference | None — overloaded `==` to detect destroyed native peer | `MissingReferenceException`; `?.` bypasses the overload (footgun) |

**Five of six converge** (everything except Unity GameObject):

1. Entity = small copyable value handle, 32 or 64 bits. Never a heap object.
2. Identity = `(index, generation)` — either two adjacent fields or bit-packed inside one integer. Cosmetic difference.
3. Generation is bumped on destroy. Reused slots get a new generation; old handles are detectably stale.
4. Validity = generation comparison, not a registry lookup.
5. Stale access never silently hits the recycled occupant. Differ only in *how* they refuse: `Result`/`Option`, exception, explicit failure, or UB-with-debug-assert.

**Divergences.** EnTT skips the check in release for zero-cost access (caller must `valid()`); the others pay a generation comparison on every accessor. Unity GameObject is the cautionary tale — heap reference + overloaded `==` is exactly the wrong shape for rollback, because the reference graph outlives the entity and "is it alive" requires a side channel.

**For rollback:** the eager-check side maps better. Replay logs naturally contain stale handles by construction; you want detection, not silent reuse.

## The decision space

There are **two coherent answers**, not a spectrum. Our current spikes occupy the incoherent middle.

### Answer A — Immortal entities (SabberStone)

- `Entity` is a class. Lives for the duration of the match. Never removed from the registry.
- Tags live on the instance (`Dictionary<int slot, object value>` or similar).
- "Destroy" is a tag change (e.g. `Zone → Removed`). Not a real destroy.
- Wire alphabet: `EntityCreated`, `TagSet`, `TagChange`, `TagRemoved`. **No `EntityDestroyed`.**
- Wire emission can stay on the entity's setter (with the encapsulation fixes), or move to the store.
- Identity stable across rollback by design.

**Pros**
- Rewinding "destroy" is trivial — there's nothing to rewind, just a tag change with `Prev`/`Next` already on the wire.
- The `MirrorWorld.Unapply(EntityDestroyed)` gap disappears entirely. The "carry the prior tag bag on the wire" problem disappears.
- Identity is stable across rollback. Stale references are not a memory hazard.
- Matches SabberStone (our closest reference codebase) and the wire spike's PowerHistory-shaped grammar.
- Smaller migration: keep `class Entity`, fix encapsulation, remove `Destroy` from the API.

**Cons**
- Entities accumulate within a match (bounded by match length — not a real memory issue for card games; SabberStone runs thousands of AI sims this way).
- "Is this still relevant?" becomes a semantic question (`Zone == Removed`?), not a reference-validity question. Authors must check.
- Doesn't generalize beyond bounded-lifetime domains (would not fit a long-running open-world ECS — but that's not our problem).

### Answer B — Generational handles (Bevy / DOTS / EnTT / flecs / specs)

- `Entity` becomes `readonly struct EntityHandle(int Index, int Generation)`. Always passed by value.
- Tags live in the store, columnar (per-slot dictionaries) or per-entity row.
- `Get/Set/Has/Remove` are store methods that take the handle. Every accessor checks generation.
- `Destroy` bumps the slot's generation; held handles stop resolving.
- Wire alphabet keeps `EntityDestroyed` — but mirror still needs the prior tag bag to rewind, so the existing gap is *not* solved by this path alone.

**Pros**
- Stale references are detected by construction. The whole class of bugs the current spikes exhibit becomes impossible.
- This is the canonical ECS answer; if we ever generalize beyond a card game, it's the right shape.
- Aligns with Unity DOTS conventions if we ever wire to the Burst/Jobs world.

**Cons**
- Larger migration. Author surface changes from `e.Set(key, v)` to `store.Set(h, key, v)` (or a fluent `EntityRef` wrapper).
- `EntityDestroyed` wire-event rewind still requires snapshotting tags, independent of the handle work.
- Owner/Zone tags currently store `int Id` — would want to store handles to validate generation on chain-walks. More refactor.
- Indirection on every access vs. SabberStone's direct dictionary access.

### What's neither A nor B (and is what we have)

Class entity + `Destroy` that removes from the registry + held references survive = **silent divergence**. None of the surveyed reference codebases does this. It is the failure mode the spike is intended to eliminate.

## Author's read (pre-design-pass)

Lean: **Answer A**, with the encapsulation issues fixed.

Reasoning:
- Card games are the sweet-spot domain for "never destroy" — bounded match length, server-authoritative, replay-friendly. SabberStone proves it scales to thousands of concurrent simulations.
- The PowerHistory-shaped wire grammar we adopted in `wire-typed-payloads.md` matches A's emission model exactly: id-keyed events, no `EntityDestroyed`, tag changes carry their own rewind data.
- A *removes* an existing broken thing (the unrewindable destroy) rather than adding a new mechanism (generational handles + accessor checks).
- The encapsulation fixes that A still needs (private slot bag, store-only writers, no public mutable fields on `CardEntity`) are scoped fixes we'd want regardless of which path we pick.
- B is the right answer for a general ECS; we are not a general ECS, we are a card-game framework whose lifetime model is naturally bounded.

The decision should not be made on this lean alone — there are open questions below that should drive a design pass.

## Open sub-questions for design pass

1. **Defless entity removal.** Even under A, do we want to physically remove entities ever (mulligan replacements, deck shuffling artifacts, hidden infrastructure entities)? If yes, A has its own destroy-shaped corner.
2. **Zone-as-the-tombstone.** Is `Zone == Removed` (or absent zone tag) the canonical "this entity no longer matters" signal? Does that compose with the zone system we already have?
3. **Identity across matches.** Is `Id` unique within a match, or globally? Affects replay archiving.
4. **Authoring surface.** Stay with `e.Set(key, v)` (entity-as-writer) or move to `store.Set(e, key, v)` (store-as-writer)? Wire-emit lives wherever the writer lives.
5. **Encapsulation level on the entity class.** Internal-only setters? Private slot bag with `internal` access for the store? `CardEntity`'s public-fields problem.
6. **Mirror-side parity.** If we drop `EntityDestroyed` from the wire, `MirrorWorld` needs the corresponding simplification. Confirm no consumer depends on the explicit "destroyed" signal.
7. **Owner / Zone tags.** Stay `int Id` (under A, ids are stable forever, so this is safe) or move to a typed `EntityRef`-style wrapper for clarity?
8. **B as a future migration path.** If we pick A now and later need B (because we generalize to non-card domains), how big is that migration? Worth thinking through once, even if we don't take the road.

## Status

- [x] Audit of current spikes complete
- [x] SabberStone deep-dive complete
- [x] Mainstream ECS survey complete (Bevy, DOTS, EnTT, flecs, specs, Unity GameObject)
- [x] Decision space framed (A vs B vs the broken hybrid we have)
- [x] Author lean documented (A)
- [ ] Design pass on the 8 open sub-questions
- [ ] Prototype the chosen path against one of the existing spikes (likely `TypedPayloads`, since that's where the failure mode is sharpest)
- [ ] Migrate the other two spikes to the same model

## Pointers into current code

- `Assets/Spike/ECS/EntitySetup/Entity.cs` — construction-flow entity (class, Destroy = dict-remove)
- `Assets/Spike/ECS/EntitySetup/EntityStore.cs`
- `Assets/Spike/ECS/TypedPayloads/Engine/Entity.cs` — wire-emitting entity (the silent-divergence case)
- `Assets/Spike/ECS/TypedPayloads/Engine/EntityStore.cs`
- `Assets/Spike/ECS/TypedPayloads/Engine/MirrorWorld.cs` — see lines 55-59 for the unrewindable-destroy gap
- `Assets/Spike/ECS/CardPresentation/World/CardEntity.cs` — public-fields entity
- `Assets/Spike/ECS/CardPresentation/World/World.cs` — intended single-writer, not enforced
- `Assets/Spike/ECS/CardPresentation/CardViewModel.cs` — accidental handle-pattern precedent (id-keyed lookup)

Note: there's also a newer `Assets/Spike/ECS/StateMachine/Engine/Entity.cs` and `Assets/Spike/ECS/VerticalSlice/Engine/Entities/Entity.cs` not audited in this doc — worth a look during design pass to confirm the same diagnosis applies.
