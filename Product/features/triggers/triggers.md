---
type: feature
status: settled
tags: [#feature, #triggers]
---

# triggers — feature

## Purpose
Triggers and reactions — the system by which `OnBefore<T>` and `OnAfter<T>` listeners observe and respond to commands. Replaces the older `CommandPreHook` / `CommandReaction` design; this is the canonical fan-out for "when X happens, do Y."

## Boundaries
- **Owned:** trigger registration, fold/aggregate semantics, OnBefore/OnAfter contract, ordering and priority rules.
- **Not owned:** the command pipeline itself (`features/actions/`), the engine evaluation loop (`features/engine/`), effect lifetimes (`features/effects/`).

## Composition
- `features/triggers/models/01-trigger-fold.md` — canonical Model (formerly doc 21).

## Public surface
`OnBefore<T>`, `OnAfter<T>` registration; trigger fold/aggregator types. Effects and game-rules code register through these.

## Dependencies
- **Upstream:** `features/actions/`, `features/engine/`.
- **Downstream:** `features/effects/`, game-side rules code.

## Settled decisions
ADRs in `features/triggers/decisions/` (backfilled in phase 5):
- trigger fold semantics, OnBefore/OnAfter design.

## Open
See `ledgers/OPEN-QUESTIONS.md` for trigger-related questions.
