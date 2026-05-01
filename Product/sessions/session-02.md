---
type: session
schema-version: 1
status: backfilled
last-updated: 2026-04-29
date: 2026-04-27
tags: [#session, #backfill, #naming, #renames]
---

# Session 02 — 2026-04-27 (backfilled)

## Part 1 — Content

### Active question coming in
Action vocabulary is overloaded: `Request`, `Command`, `Action`, `Effect` all bleed together.

### Work done
- Terminology review pass; identified rename and glossary gaps (`4162086`).
- Renamed `OriginRequest → OriginIntent`, `PlayerRequest → PlayerIntent` across all docs (`6d64d29`).
- First session checkpoint refresh (`7e2afca`).

### Decisions settled
- `PlayerIntent` (UI) → `Action`/`ActionTemplate` (snapshot) → `GameCommand` (executed). Captured later as `features/actions/decisions/0008-intent-vocabulary.md` and `decisions/0002-naming-alignment-conventions.md`.

### Active question going out
Composite mutations — should there be a `CompositeAction` / scope helper, or is sequential `Apply` enough?

## Part 2 — Links & open threads

### Followups created
- Doc 19 composite-operation revision (session 03).

### References
- Docs touched: `ledgers/GLOSSARY.md`, `features/actions/models/01-command-pipeline.md` (provenance).
- Related: PR #2 (`3f8a4ae`).
