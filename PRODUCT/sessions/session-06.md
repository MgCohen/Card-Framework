---
type: session
status: backfilled
last-updated: 2026-04-29
date: 2026-04-27
tags: [#session, #backfill, #async, #effects]
---

# Session 06 — 2026-04-27 (backfilled)

## Part 1 — Content

### Active question coming in
How does the engine pause for player input mid-effect without breaking replay?

### Work done
- Doc 22 — async decisions and suspension design exploration (`95bd694`).
- Clarified `Effect` / `GameAction` / `CompositeAction` hierarchy; rewrote doc 22 (`7354ea3`).
- Locked async/dispatch model and refreshed checkpoint and open questions (`6f57580`).

### Decisions settled
- Layer hierarchy: only effects sequence commands; `GameAction` is synchronous; `CompositeAction` is inline state-change group.
- Two dispatch paths: `engine.Dispatch` (queued) and `scope.Dispatch` (immediate, awaited).
- Decisions modelled as commands with explicit decision IDs threaded through state.
- Captured later as `features/effects/decisions/0001-async-layer-hierarchy.md` and `0002-suspension-model.md`.

### Active question going out
Replacement / prevention / ongoing effects — what's the rules-level model?

## Part 2 — Links & open threads

### References
- Docs touched: `features/effects/notes/01-async-dispatch.md` (new doc 22).
- Related: PR #8 (`811a37c`).
