---
type: adr
status: accepted
last-updated: 2026-05-01
tags: [#adr, #zones, #insertion]
---

# ADR-0003 — Insertion API: single payload with optional override fields

Date: 2026-05-01
Context-doc: features/zones/models/01-zone-model.md
Proposal: session-20260501-1602-zone-model-internal-opens

## Context

Zone insertion has two axes that game effects sometimes need to control beyond the zone's default behavior:

1. **Position** — where in the zone the new card lands (top, bottom, specific index, specific slot).
2. **Capacity** — whether the insertion respects the zone's default capacity limit or bypasses it.

The zone Model leaves both as game-rules concerns ("once a move is approved, the zone owns how its contents are arranged") but doesn't specify the API shape for overriding defaults.

A separate axis — "this insertion can't be cancelled by replacement effects" — lives at the `GameAction` / trigger-fold layer, not at the zone mutation layer. This ADR is only about the zone-side mutation API for overriding default insertion behavior.

## Options considered

- **A — Single payload with optional override fields.** `InsertCardPayload { Zone, Card, Position, BypassCapacity }`. `Position` and `BypassCapacity` defaults mean "use zone definition's default"; explicit values override. Sugar via static factories. Chosen.
- **B — Two payloads.** `InsertCardPayload` for standard, `ForceInsertCardPayload` for overrides. Rejected: duplicates mutator logic; effects pick which payload to use; framework grows two parallel paths for one concept.
- **C — Multiple targeted payloads.** `InsertAtTopPayload`, `InsertAtBottomPayload`, `InsertAtSlotPayload`, `ForceInsertPayload`. Rejected: reads nicely at call sites but multiplies framework surface; forces a "which payload?" decision on every call.

For the `Position` field shape:
- **Discriminated union mixing parameterless and parameterized variants** (e.g., `Top | Bottom | AtIndex(int) | AtSlot(SlotKey)`). Rejected: structurally awkward — cases-with-data and cases-without-data on the same type read as "functions on an enum" in C#, even when expressed as a sealed hierarchy in trivia.
- **Enum + optional `Index` field** (`enum InsertMode { ZoneDefault, Top, Bottom, AtIndex }; int? Index`). Rejected: invalid combinations are constructible (`Mode = Top, Index = 5`); flat shape but loses type safety.
- **Sealed record hierarchy.** `abstract record InsertPosition; sealed record AtTop/AtBottom/AtIndex(int)/DefaultPosition`. Each variant is its own type with its own data. Chosen.

## Decision

A single `InsertCardPayload` carries the operation:

```csharp
record InsertCardPayload(
  Zone Zone,                                      // destination zone (object, per ADR-0005)
  Card Card,                                      // the card to insert
  InsertPosition? Position = null,                // null = use zone definition's default
  bool BypassCapacity = false);                   // false = respect zone's default capacity
```

`InsertPosition` is a sealed record hierarchy — each variant is its own type, so cases-with-data and cases-without-data don't share a single enum (which would mix "function on enum" semantics awkwardly):

```csharp
abstract record InsertPosition;
sealed record DefaultPosition  : InsertPosition;     // use zone definition's insertion behavior
sealed record AtTop            : InsertPosition;     // first position
sealed record AtBottom         : InsertPosition;     // last position
sealed record AtIndex(int Idx) : InsertPosition;     // explicit index
```

Pattern matching at the consumer side stays clean:

```csharp
var index = payload.Position switch {
  AtTop          => 0,
  AtBottom       => contents.Count,
  AtIndex at     => at.Idx,
  DefaultPosition => zone.Definition.DefaultInsertion.Resolve(contents),
};
```

The zone definition decides which positions are meaningful for that zone type — explicit `AtIndex(i)` outside the zone's index range is an authoring error.

Static factories provide call-site readability for the position axis:
- `InsertCardPayload.AtTop(zone, card)`
- `InsertCardPayload.AtBottom(zone, card)`
- `InsertCardPayload.AtIndex(zone, card, i)`
- `InsertCardPayload.Default(zone, card)` — explicit "use zone default"

The `BypassCapacity` axis uses C#'s `with` expression rather than a separate `Force` factory — `InsertCardPayload.AtTop(zone, card) with { BypassCapacity = true }`. This avoids method-explosion (no `Force × Position` cartesian product) and keeps the override self-documenting at the call site.

The composite `RelocateInstanceMutation` (remove + insert as one logical move) carries the same `InsertPosition` and `BypassCapacity` and forwards them to the insert side. A relocate that pushes to top of destination uses `RelocateInstanceMutation { Source, Destination, Card, Position: new AtTop() }`.

## Consequences

- **Enables:** one mutator, one payload type, one mental model for insertion. Sugar provides readability where call sites benefit. Composite relocate forwards options without duplicating the override surface.
- **Constrains:** override semantics live on the payload — game effects that want to "force" an insertion express it through `BypassCapacity = true` and explicit `Position`, not via a separate payload type.
- **Reverts/replaces:** the Open Question in `features/zones/models/01-zone-model.md` "what does a forced/override insertion API look like?" is answered here.
- **Audit:** the `ForceInsertCardPayload` shape that may have been sketched in earlier drafts is retired in favour of the single-payload form.
