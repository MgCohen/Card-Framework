---
type: adr
status: accepted
references: [features/cards/models/01-card-model.md, features/zones/models/01-zone-model.md, features/presentation/models/01-presentation-loop.md]
tags: [#adr, #zones, #visibility, #history, #cards, #cross-cutting]
---

# ADR-0006 — Visibility model: definition policy + per-card flag + server-side CardRef rotation

Date: 2026-05-01 (elevated from `features/zones/decisions/0002-visibility-model.md` to project-wide on 2026-05-08 per §4.5 two-feature rule — cross-cutting across cards + zones + presentation)
Context-docs: `features/cards/models/01-card-model.md`, `features/zones/models/01-zone-model.md`, `features/presentation/models/01-presentation-loop.md`
Proposal: session-20260501-1602-zone-model-internal-opens

## Context

The zone Model settled the two-channel visibility model (membership visibility and detail visibility) and named the need for actor-specific configuration, but left the API shape, runtime mechanism, and history filtering all open. Five sub-questions:

1. Do rules ever read visibility, or is it purely transport?
2. How does the framework support privacy across reveal-then-hide cycles (re-hide tracking)?
3. Where does zone visibility policy live (definition, instance state, both)?
4. What shape is the per-card runtime flag?
5. How is movement that some viewers can't see recorded in history?

Initial framing: server is omniscient; visibility is a send-time filter. "Visible to client" = "client has the card's data." Hidden cards may be sent as count-only or as ID-only with no data.

A guiding principle surfaced during the design pass: **the simulation should play the same locally and over the network. The only allowed difference is information being cut to actors who shouldn't see it — not data being changed per actor.** This forecloses the original "per-actor transport alias" mechanism (which changed IDs per actor) in favour of server-side `CardRef` rotation (canonical state changes; all observers see the same IDs; visibility filter only cuts data, never alters IDs).

## Options considered

For the visibility query API:
- **A — Predicates only, no framework-provided filter view.** Rejected for boundary use: presentation and network would re-implement filtering at every call site.
- **B — Snapshot-only filter, no runtime predicates.** Rejected: rules genuinely branch on visibility ("when opponent reveals X, draw a card"); a predicate primitive is unavoidable.
- **C — Predicates + transport-side filter built on the same primitives.** Chosen.

For the privacy mechanism (re-hide tracking):
- **A — Per-actor transport alias.** Server keeps stable canonical identity; per-actor alias map at the network boundary; clients receive aliased IDs. Rejected: changes data per actor (the ID one client sees ≠ canonical ID), violates the "same locally and networked" principle, requires transport-only machinery that doesn't run locally.
- **B — Simulation-level identity reissue.** Mutate the stable identity on re-hide; cascade through slices, triggers, effects. Rejected: invasive; breaks the "stable across moves" invariant; forces every subscriber to follow id changes.
- **C — Two refs: stable `Ref<Card>` + ephemeral `CardRef` rotated server-side.** Server canonical state has both refs. `Ref<Card>` is stable forever (used for variable storage slice key, modifier storage, history, server-internal subscriptions). `CardRef` is ephemeral and rotates on every visibility transition (and on every move per ADR-0003). All observers see the same `CardRef` — local and remote alike. The privacy filter cuts data, not refs. Chosen.

For zone visibility policy:
- **A — Definition-only.** Chosen. Most games don't need runtime-mutable zone-wide visibility transitions. The per-card flag covers per-card runtime variation.
- **B — Definition + optional runtime override slice.** Rejected as YAGNI; can be added later if pressure arises.

For per-card flag shape:
- **A — Boolean.** Rejected: insufficient — `Hidden` (nobody sees) is genuinely different from `VisibleToOwner` (owner sees, others don't).
- **B — Per-channel boolean (membership + detail).** Rejected: per-card membership-visible-yet-detail-hidden is rare; membership is a zone-level concern.
- **C — Per-actor map (`Dictionary<ActorId, VisibilityMode>`).** Rejected: heavier than needed; asymmetric reveals are uncommon and can be modeled separately.
- **D — Three-value enum `{ Hidden, VisibleToOwner, VisibleToAll }`; absent entry = use zone policy.** Chosen.

For history filtering:
- **A — Single canonical history; transport translates rotated `CardRef`s and gates card-detail entries on visibility.** Chosen.
- **B — Per-actor history streams generated already-filtered.** Rejected: duplicates history N times; replay/debug/AI all need the canonical stream.

## Decision

**Two-channel visibility model.** Membership visibility (does actor know which instances are in the zone?) and detail visibility (given a known instance, can actor see its definition and variables?) are both expressed.

**Zone visibility policy is authored on the `ZoneDefinition`.** The policy expresses per-actor-role rules across the two channels — e.g., "visible to owner, count-only to others" (hand), "hidden to all" (deck contents), "visible to all" (battlefield). The policy is static; runtime variation comes only from the per-card flag.

**Per-card flag** lives on `CardVisibilityState` (defined in the [Card Model § Card Visibility](features/cards/models/01-card-model.md)) as a `CardVisibility` enum (`Hidden | VisibleToOwner | VisibleToAll`) keyed by `Ref<Card>`. Absent entry = use the zone's policy default. `VisibleToOwner` requires the card to have an Owner; collapses to `Hidden` if no Owner.

**Rules read visibility via the card object:** `card.IsVisibleTo(state, actor)` delegates to the slice and combines with the zone's policy. Visibility-conditional rules and triggers branch on this read.

**Re-hide via server-side `CardRef` rotation** (per Card Model). The simulation-side `Ref<Card>` is stable forever (preserves the entities Model invariant). The `CardRef` is the temporary client-visible reference and rotates on every visibility transition. When a card transitions visibility:

1. A new `CardRef` is generated by the dispatching mutation (`SetCardVisibilityMutation { Card, NewRef, Visibility }` — typically constructed via `Hide` / `ShowAll` / `ShowToOwner` factories).
2. `CardMapState` swaps the entry: old `CardRef` out, new `CardRef` in (still pointing at the same `Ref<Card>`).
3. Every `ZoneContentsState` containing the old ref swaps it for the new ref.
4. `CardVisibilityState[card.SelfRef]` updates to the new visibility level.

All observers — local and networked — see the canonical new `CardRef`. There is no per-actor ID variation. The visibility filter at the transport boundary cuts the *data* sent (variables, modifiers, effective values) to actors who can't see the card, leaving the `CardRef` itself untouched.

Conservative rotation policy: every visibility transition rotates the ref (Hide → Hidden, Show → VisibleToAll, ShowToOwner → VisibleToOwner — all rotate). This prevents observers from learning continuity across reveal cycles.

**Per ADR-0003 (insertion API), every move also rotates the `CardRef`.** This is the same mechanism — `MoveCardMutation { Card, NewRef, ... }` carries a fresh ref. Hide and move share the privacy property: an observer who briefly saw a card cannot identify it later by its old ref.

**History is a single canonical record on the server.** `HistoryEnvelope` entries use canonical `Ref<Card>`s and full data. Per-actor transport applies two transformations:

- **CardRef translation in entries** — when an entry references a card by `CardRef` (e.g., `ZoneContentInserted(zone, ref)`), the transport ensures the ref shown matches the current canonical ref the actor sees. Since all actors see the same canonical ref, this is identity-translation in most cases — but for actors who didn't have the old ref (because they couldn't see the card before), the rotation still appears as "a card with this new ref entered the zone."
- **Detail gating** — card-detail entries (`VariableChanged<T>`, modifier changes) are sent only to actors who have detail visibility on the affected card. Movement entries (`ZoneContentInserted`, `ZoneContentRemoved`, relocate, ref rotation) are always sent — the *fact* of movement and ref rotation is public.

For genuinely-secret movements (where even the fact must be hidden from observers), the game models the interaction differently — not via a special "secret history" mode in the framework.

## Consequences

- **Enables:** server-side rules can branch on visibility with a one-line predicate; the same `CardRef` rotation runs locally and over the wire — no per-actor ID divergence; reveal-then-hide privacy works without breaking simulation-side invariants on `Ref<Card>`; the framework's transport filter is purely a *data cutter*, not a transformer.
- **Constrains:** server-internal subscriptions (triggers, effects, attachments, history references intended for subscriber dispatch) must use `Card` objects or `Ref<Card>` — never `CardRef` — otherwise rotations break them. Client-visible state and projections must use `CardRef` — otherwise the privacy mechanism doesn't apply.
- **Reverts:** the originally-considered "per-actor transport alias" privacy mechanism (Option A in the per-actor mechanism section above) was reversed in the same session per the "play the same locally vs networked" principle. The `Ephemeral Instance ID` glossary entry that referred to the old transport-alias model is deprecated; the framework no longer maintains a per-actor alias map.
- **Audit:** asymmetric reveals (visible to one specific opponent only) are deferred — a game that needs them models the reveal as a transient game-state slice ("opponent X currently knows card Y") separate from the canonical visibility flag.
