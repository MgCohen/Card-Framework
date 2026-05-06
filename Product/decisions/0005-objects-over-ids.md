---
type: adr
status: accepted
tags: [#adr, #general, #conventions, #api]
---

# ADR-0005 — Objects over IDs in framework APIs

Date: 2026-05-01
Context-doc: ledgers/GLOSSARY.md
Proposal: session-20260501-1602-zone-model-internal-opens

## Amendment — 2026-05-06 (Plan A/B/C cascade)

The "addressable thing" type has changed at the Scaffold layer. Plan A landed `Reference` (an abstract record in `Scaffold.States`) and typed `Ref<T> : Reference` subrecords. The old empty-marker `IReference` interface and the `Identifier<Instance>`/`InstanceId` family for runtime entities have been removed. Concrete handles now carry typed `Ref<TSubclass> SelfRef` fields; polymorphic APIs that take "any addressable thing" type the parameter as `Reference`.

Spirit of the ADR is unchanged: framework APIs still prefer objects over IDs; IDs are still reserved for boundaries. The only thing that changed is the C# type backing "addressable" — `Reference` (abstract record) instead of `IReference` (empty interface marker). `Identifier<T>` for non-entity domains (definition IDs, actor IDs, chain IDs) is unaffected; `Identifier<T>` migration for any remaining runtime-gameplay users is a separate cascade.

The hierarchy below has been updated to reflect the new shape.

---

## Context

Framework API surfaces inconsistently mix domain objects (`Card`, `Zone`, `Player`, `EntityInstance<TDef>`) and `Identifier<T>` values (`ActorId`, `DefinitionId`). Code that holds an ID re-looks up the underlying object on every use, obscures intent at call sites, and forces the framework to expose ID-based read/write helpers in parallel with object-based ones.

The zone-model design pass (session-20260501-1602) surfaced this concretely. Zone reads were initially drafted as `state.ContentsOf(zoneId)` extension methods. The user pushed back: "if zone is an object, not a slice, why are we using `zoneId` instead of the zone itself?" The fix — zone is a thin object used directly as a slice key (`state.Get<X>(zone)`) with utility methods on the handle — generalized cleanly. The same pattern applies to entities, actors, players, and any future addressable framework concept.

## Options considered

- **A — IDs primary, objects optional.** Framework APIs take `Identifier<T>` everywhere; objects (`StateEntity`, `Zone`) are convenience wrappers callers may use or skip. Rejected: drives ID-centric idioms throughout the codebase, encourages re-lookup, and obscures intent at call sites.
- **B — Objects everywhere, IDs hidden internally.** No API surface accepts `Identifier<T>`. Rejected: serialization, persistence, network transport, and history envelopes genuinely need IDs as their wire form. Hiding them entirely creates conversion seams at every boundary.
- **C — Objects in framework APIs; IDs reserved for boundaries.** Code that operates on the simulation holds and passes objects. `Identifier<T>` appears only at boundaries (serialization, persistence, network, type-mismatch fallback). Chosen.

## Decision

**Framework APIs prefer objects.** Code holds and passes domain objects (`Zone`, `Card`, `EntityInstance<TDef>`, `Player`, `Actor`, etc.); methods on those objects perform the queries and dispatches that previously took raw `Identifier<T>` values. Slice access uses the object as the key (e.g., `state.Get<ZoneContentsState>(zone)`).

**`Reference` is the framework's polymorphic addressable type.** Anything addressable in the framework carries a typed `Ref<TSubclass> : Reference` identity. Polymorphic APIs that take "any addressable thing" type their parameter as `Reference`. Concrete entity handles (`Card`, `Zone`, `Player`) carry typed `Ref<TSubclass> SelfRef` fields.

```csharp
// Scaffold.States
public abstract record Reference;
public sealed record Ref<T>(Guid Value) : Reference where T : class;
```

The hierarchy:
- `Reference` — abstract record base; the polymorphic addressable type.
- `Ref<T>` — sealed record subtype, backed by `Guid`; the typed runtime identity for catalog-registered handles.
- `EntityInstance<TDef>` — single entity handle class; pairs `(Definition, Storage)`. Identity-free at the base.

Concrete implementations:
- `Card : EntityInstance<CardDefinition>` carries `Ref<Card> SelfRef`.
- `Zone : EntityInstance<ZoneDefinition>` carries `Ref<Zone> SelfRef`.
- `Player : EntityInstance<PlayerDefinition>` carries `Ref<Player> SelfRef`.

Catalog-registered handles are resolved via `store.Catalog.Resolve(ref)`. Subscriptions key on the typed `Ref<T>` (e.g., `store.Subscribe<EntityState>(card.SelfRef, ...)`) and survive `CardRef` rotations.

`Identifier<T>` is reserved for:

- **Serialization** — history envelopes, snapshots, save/load.
- **Network transport** — wire form for remote play and replay.
- **Persistence** — definition IDs in storage.
- **Type-mismatch boundaries** — places where the object isn't reachable (e.g., a payload deserialized before its target object exists).

Runtime-entity identity has migrated off `Identifier<Instance>` to `Ref<T>` / `Reference`. Other `Identifier<T>` users (definition IDs, actor IDs, chain IDs) are unaffected by this amendment; their migration is a separate cascade if it ever happens. The convention is enforced by code review and by Model docs that describe their APIs in object-first terms.

## Consequences

- **Enables:** consistent call-site ergonomics across the framework; fewer parallel ID-vs-object API surfaces; clearer separation between simulation-internal (object-first) and boundary-facing (ID-first) code.
- **Constrains:** framework Models must describe their APIs in object-first terms. New APIs that take `Identifier<T>` in non-boundary positions are a code-review smell unless the type-mismatch case is justified.
- **Audit pass:** existing Models (`engine/`, `effects/`, `actions/`, `presentation/`, samples) need a sweep to align ID-taking APIs to object-first form. This audit may surface its own cascade.
- **Reverts/replaces:** none. Existing `Identifier<T>` types remain in the type system; only their idiomatic use is constrained.
