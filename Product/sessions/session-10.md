---
type: session
schema-version: 1
status: backfilled
last-updated: 2026-04-29
date: 2026-04-29
tags: [#session, #backfill, #snapshot, #service]
---

# Session 10 — 2026-04-29 (backfilled)

## Part 1 — Content

### Active question coming in
Snapshot production — when is it built, who builds it, and what's the validation posture?

### Work done
- Doc 29 — snapshot production pipeline (`81b5a99`).

### Decisions settled
- Service-driven publication via `IAvailableActions` (`Set` / `Append` / `Clear`). Captured later as `features/engine/decisions/0004-snapshot-writer-iavailableactions.md`.
- Default site is `GameRules.OnQueueDrained`; effects override mid-execution; engine never auto-publishes.
- On-demand validation; not eager / not lazy. Captured later as `features/actions/decisions/0002-on-demand-validation.md`.
- `GameRules` shape change — abstract class with constructor-injected services. Captured later as `features/engine/decisions/0001-gamerules-as-class-with-services.md`.

### Active question going out
The whole research workflow itself is missing — what's the canonical doc layer, and how do sessions flow into it?

## Part 2 — Links & open threads

### References
- Docs touched: `features/engine/models/03-snapshot-pipeline.md` (new doc 29). Cascade edits to docs 25, 26, 27, 28.
- Related: PR #13 (`9b3dc3e`).
