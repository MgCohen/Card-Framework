---
type: adr
status: accepted
last-updated: 2026-05-01
tags: [#adr, #zones, #visibility, #history]
---

# ADR-0002 — Visibility model: definition policy + per-card flag + per-actor transport alias

Date: 2026-05-01
Context-doc: features/zones/models/01-zone-model.md
Proposal: session-20260501-1602-zone-model-internal-opens

## Context

The zone Model settled the two-channel visibility model (membership visibility and detail visibility) and named the need for actor-specific configuration, but left the API shape, runtime mechanism, and history filtering all open. Five sub-questions:

1. Do rules ever read visibility, or is it purely transport?
2. How does the framework support privacy across reveal-then-hide cycles (re-hide tracking)?
3. Where does zone visibility policy live (definition, instance state, both)?
4. What shape is the per-card runtime flag?
5. How is movement that some viewers can't see recorded in history?

The user's framing: server is omniscient; visibility is a send-time filter. "Visible to client" = "client has the card's data." Hidden cards may be sent as count-only or as ID-only with no data. Re-hiding a previously-revealed card swaps its InstanceId at the transport boundary so the client can't correlate with old data.

## Options considered

- **A — Predicates only, no framework-provided filter view.** Rejected for boundary use: presentation and network would re-implement filtering at every call site.
- **B — Snapshot-only filter, no runtime predicates.** Rejected: rules genuinely branch on visibility ("when opponent reveals X, draw a card"); a predicate primitive is unavoidable.
- **C — Predicates + transport-side filter built on the same primitives.** Chosen.

For ID-swap mechanism:
- **A — Per-actor transport alias** (server keeps stable canonical InstanceId; per-actor alias map at the network boundary). Chosen.
- **B — Simulation-level ID reissue** (mutate InstanceId on re-hide; cascade through slices, triggers, effects). Rejected: invasive; breaks the "InstanceId stable across moves" invariant; forces every subscriber to follow ID changes.
- **C — No framework support.** Rejected: privacy across reveal cycles is a recurring pattern that games would reinvent badly.

For zone visibility policy:
- **A — Definition-only.** Chosen. Most games don't need runtime-mutable zone-wide visibility transitions. The per-card flag covers per-card runtime variation.
- **B — Definition + optional runtime override slice.** Rejected as YAGNI; can be added later if pressure arises.

For per-card flag shape:
- **A — Boolean.** Rejected: insufficient — `Hidden` (nobody sees) is genuinely different from `VisibleToOwner` (owner sees, others don't).
- **B — Per-channel boolean (membership + detail).** Rejected: per-card membership-visible-yet-detail-hidden is rare; membership is a zone-level concern.
- **C — Per-actor map (`Dictionary<ActorId, VisibilityMode>`).** Rejected: heavier than needed; asymmetric reveals are uncommon and can be modeled separately.
- **D — Three-value enum `{ Hidden, VisibleToOwner, VisibleToAll }`; absent entry = use zone policy.** Chosen.

For history filtering:
- **A — Single canonical history; transport translates IDs and gates card-detail entries on visibility.** Chosen.
- **B — Per-actor history streams generated already-filtered.** Rejected: duplicates history N times; replay/debug/AI all need the canonical stream.

## Decision

**Two-channel visibility model.** Membership visibility (does actor know which instances are in the zone?) and detail visibility (given a known instance, can actor see its definition and variables?) are both expressed.

**Zone visibility policy is authored on the `ZoneDefinition`.** The policy expresses per-actor-role rules across the two channels — e.g., "visible to owner, count-only to others" (hand), "hidden to all" (deck contents), "visible to all" (battlefield). The policy is static; runtime variation comes only from the per-card flag.

**Per-card flag** lives on a per-card state slice as enum `{ Hidden, VisibleToOwner, VisibleToAll }`. Absent entry = use the zone's policy default. `VisibleToOwner` requires the card to have an Owner; collapses to `Hidden` if no Owner.

**Rules read visibility via the card object:** `card.IsVisibleTo(state, actor)` delegates to the slice. Visibility-conditional rules and triggers branch on this read.

**Re-hide via per-actor transport alias.** Simulation-side `InstanceId` stays stable forever (preserves the entities Model invariant: "zone movement preserves the same instance"). At the network boundary, the server maintains a per-actor `InstanceId → EphemeralId` map. When a card transitions to "newly hidden" relative to actor X, the server generates a fresh ephemeral ID for that actor's view. Triggers, effects, slices, and history all reference the canonical InstanceId; ID translation happens only in the transport layer.

**History is a single canonical record on the server.** `HistoryEnvelope` entries use canonical InstanceIds and full data. Per-actor transport applies two transformations:

- **ID translation** — canonical InstanceIds → per-actor ephemeral IDs (using the same alias map as live state).
- **Detail gating** — card-detail entries (`VariableChanged<T>`, modifier changes) are sent only to actors who have detail visibility on the affected card. Movement entries (`ZoneContentInserted`, `ZoneContentRemoved`, relocate) are always sent — the *fact* of movement is public.

For genuinely-secret movements (where even the fact must be hidden from observers), the game models the interaction differently — not via a special "secret history" mode in the framework.

## Consequences

- **Enables:** server-side rules can branch on visibility with a one-line predicate; transport filtering is a single layer over canonical state and history; reveal-then-hide privacy works without breaking simulation-side invariants.
- **Constrains:** the zone definition's visibility policy is static — runtime whole-zone visibility transitions are expressed by mutating per-card flags, not by mutating zone state. If a game needs both, that's a future ADR.
- **Reverts/replaces:** the Open Questions in `features/zones/models/01-zone-model.md` "what is the exact visibility query API?" and "how should history record hidden movement?" are answered here.
- **Audit:** asymmetric reveals (visible to one specific opponent only) are deferred — a game that needs them models the reveal as a transient game-state slice ("opponent X currently knows card Y") separate from the canonical visibility flag.
