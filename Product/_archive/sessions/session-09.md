---
type: session
schema-version: 1
status: backfilled
date: 2026-04-28
tags: [#session, #backfill, #snapshot, #templates]
---

# Session 09 — 2026-04-28 (backfilled)

## Part 1 — Content

### Active question coming in
What's the snapshot's exact shape? Flat `GameCommand` enumeration doesn't scale.

### Work done
- Doc 28 — ActionAvailability snapshot shape (`39ffa14`).

### Decisions settled
- Snapshot element type is a typed `ActionTemplate` per command type, with shared abstract base. Captured later as `features/actions/decisions/0001-typed-action-template.md`.
- `ActionId` retired in favour of using the registered command `Type` directly.
- Targets are `IReadOnlyList<EntityId>` uniformly.
- IntentMapper switches on typed template subtype. Captured later as `features/actions/decisions/0006-player-intent-mapper.md`.

### Active question going out
Snapshot *production* — when is it built, who builds it, and is validation eager or lazy?

## Part 2 — Links & open threads

### References
- Docs touched: `features/actions/models/02-availability-snapshot.md` (new doc 28).
- Related: PR #12 (`d6a7043`).
