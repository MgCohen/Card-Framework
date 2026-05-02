---
type: adr
status: accepted
last-updated: 2026-05-01
tags: [#adr, #general, #conventions, #api]
---

# ADR-0005 — Objects over IDs in framework APIs

Date: 2026-05-01
Context-doc: ledgers/GLOSSARY.md
Proposal: session-20260501-1602-zone-model-internal-opens

## Context

Framework API surfaces inconsistently mix domain objects (`StateEntity<TDef>`, `Zone`, `Player`) and `Identifier<T>` values (`InstanceId`, `ZoneId`, `ActorId`). Code that holds an ID re-looks up the underlying object on every use, obscures intent at call sites, and forces the framework to expose ID-based read/write helpers in parallel with object-based ones.

The zone-model design pass (session-20260501-1602) surfaced this concretely. Zone reads were initially drafted as `state.ContentsOf(zoneId)` extension methods. The user pushed back: "if zone is an object, not a slice, why are we using `zoneId` instead of the zone itself?" The fix — zone is a thin object used directly as a slice key (`state.Get<X>(zone)`) with utility methods on the handle — generalized cleanly. The same pattern applies to entities, actors, players, and any future addressable framework concept.

## Options considered

- **A — IDs primary, objects optional.** Framework APIs take `Identifier<T>` everywhere; objects (`StateEntity`, `Zone`) are convenience wrappers callers may use or skip. Rejected: drives ID-centric idioms throughout the codebase, encourages re-lookup, and obscures intent at call sites.
- **B — Objects everywhere, IDs hidden internally.** No API surface accepts `Identifier<T>`. Rejected: serialization, persistence, network transport, and history envelopes genuinely need IDs as their wire form. Hiding them entirely creates conversion seams at every boundary.
- **C — Objects in framework APIs; IDs reserved for boundaries.** Code that operates on the simulation holds and passes objects. `Identifier<T>` appears only at boundaries (serialization, persistence, network, type-mismatch fallback). Chosen.

## Decision

**Framework APIs prefer objects.** Code holds and passes domain objects (`Zone`, `Card`, `StateEntity<TDef>`, `Player`, `Actor`, etc.); methods on those objects perform the queries and dispatches that previously took raw `Identifier<T>` values. Slice access uses the object as the key (e.g., `state.Get<ZoneContentsState>(zone)`).

**`IReference` is the framework marker** for objects that carry an identity in the `Identifier<Instance>` namespace. Anything addressable in the framework — Zones, Cards (via `IReadOnlyEntity` ⊃ `IReference`), and any future addressable entity — implements it. Polymorphic APIs that take "any addressable thing" type their parameter as `IReference`.

```csharp
public interface IReference
{
    InstanceId Id { get; }
}
```

The hierarchy:
- `IReference` — base; carries `Id` only.
- `IReadOnlyEntity` extends `IReference` — entity-bridge handle; also exposes variables, modifiers, definition typing.
- `IMutableEntity` extends `IReadOnlyEntity` — entity-bridge handle that also writes.

Concrete implementations:
- `Zone` → `IReference` directly.
- `Card` (which extends `StateEntity<TCardDef>`) → `IReadOnlyEntity` (and thus `IReference` transitively).

`Identifier<T>` is reserved for:

- **Serialization** — history envelopes, snapshots, save/load.
- **Network transport** — wire form for remote play and replay.
- **Persistence** — definition IDs, instance IDs in storage.
- **Type-mismatch boundaries** — places where the object isn't reachable (e.g., a payload deserialized before its target object exists).
- **Stable subscription keys** — long-lived subscriptions (triggers, ongoing effects, attachments) reference cards by `InstanceId` so they survive `CardRef` rotations.

Existing `Identifier<T>` types stay; this ADR is behavioral, not a rename. The convention is enforced by code review and by Model docs that describe their APIs in object-first terms.

## Consequences

- **Enables:** consistent call-site ergonomics across the framework; fewer parallel ID-vs-object API surfaces; clearer separation between simulation-internal (object-first) and boundary-facing (ID-first) code.
- **Constrains:** framework Models must describe their APIs in object-first terms. New APIs that take `Identifier<T>` in non-boundary positions are a code-review smell unless the type-mismatch case is justified.
- **Audit pass:** existing Models (`engine/`, `effects/`, `actions/`, `presentation/`, samples) need a sweep to align ID-taking APIs to object-first form. This audit may surface its own cascade.
- **Reverts/replaces:** none. Existing `Identifier<T>` types remain in the type system; only their idiomatic use is constrained.
