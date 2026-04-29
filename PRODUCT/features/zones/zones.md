---
type: feature
status: settled
last-updated: 2026-04-29
tags: [#feature, #zones]
---

# zones — feature

## Purpose
Zones are first-class containers for entities (decks, hands, fields, discard piles, bench, etc.). The feature defines how zones are declared, how membership and ordering work, and how movement between zones is observed.

## Boundaries
- **Owned:** zone declaration, membership lookup, movement primitives, zone-shaped queries.
- **Not owned:** card identity (lives in `features/entities/`), legality of moves (lives in `features/actions/`), animation of zone transitions (lives in `features/presentation/`).

## Composition
- `features/zones/notes/01-zone-model.md` — canonical Model. Provenance from doc 11 (zone model decisions) preserved as a section below the divider.

## Public surface
Zone identifiers, membership queries, and zone-aware accessors. Actions reference zones by identifier; effects observe zone-membership changes.

## Dependencies
- **Upstream:** `features/entities/` — zones contain instances.
- **Downstream:** `features/actions/`, `features/effects/`, `features/presentation/`.

## Settled decisions
ADRs in `features/zones/decisions/` (backfilled in phase 5).

## Open
See `general/OPEN-QUESTIONS.md` for zone-related questions.
