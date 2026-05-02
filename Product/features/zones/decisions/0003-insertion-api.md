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

- **A — Single payload with optional override fields.** `InsertCardPayload { Zone, Card, Position?, BypassCapacity? }`. Null/default = use zone definition's default behavior; explicit values override. Sugar via static factories: `InsertCardPayload.AtTop(zone, card)`, `InsertCardPayload.ForceAt(zone, card, slot)`. Chosen.
- **B — Two payloads.** `InsertCardPayload` for standard, `ForceInsertCardPayload` for overrides. Rejected: duplicates mutator logic; effects pick which payload to use; framework grows two parallel paths for one concept.
- **C — Multiple targeted payloads.** `InsertAtTopPayload`, `InsertAtBottomPayload`, `InsertAtSlotPayload`, `ForceInsertPayload`. Rejected: reads nicely at call sites but multiplies framework surface; forces a "which payload?" decision on every call.

## Decision

A single `InsertCardPayload` carries the operation:

```text
InsertCardPayload {
  Zone:           Zone           // the destination zone (per ADR-0005, object not Id)
  Card:           StateEntity    // the card to insert
  Position?:      InsertPosition // null = use zone definition's default
  BypassCapacity?:bool           // null/false = respect default; true = override
}
```

`InsertPosition` is a discriminated union:

```text
InsertPosition =
  | Top
  | Bottom
  | AtIndex(int)
  | AtSlot(SlotKey)
```

The zone definition decides which positions are meaningful for that zone type — inserting `AtSlot` on an unslotted zone is an authoring error.

Static factories provide call-site readability without payload sprawl:
- `InsertCardPayload.AtTop(zone, card)`
- `InsertCardPayload.AtBottom(zone, card)`
- `InsertCardPayload.AtIndex(zone, card, i)`
- `InsertCardPayload.AtSlot(zone, card, slotKey)`
- `InsertCardPayload.Force(...)` — variant that sets `BypassCapacity = true`

The composite `RelocateInstanceMutation` (remove + insert as one logical move) carries the same `InsertOptions` and forwards them to the insert side. A relocate that wants to push to top of destination uses `RelocateInstanceMutation { Source, Destination, Card, InsertPosition: Top }`.

## Consequences

- **Enables:** one mutator, one payload type, one mental model for insertion. Sugar provides readability where call sites benefit. Composite relocate forwards options without duplicating the override surface.
- **Constrains:** override semantics live on the payload — game effects that want to "force" an insertion express it through `BypassCapacity = true` and explicit `Position`, not via a separate payload type.
- **Reverts/replaces:** the Open Question in `features/zones/models/01-zone-model.md` "what does a forced/override insertion API look like?" is answered here.
- **Audit:** the `ForceInsertCardPayload` shape that may have been sketched in earlier drafts is retired in favour of the single-payload form.
