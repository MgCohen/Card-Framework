---
type: feature
status: settled
last-updated: 2026-04-29
tags: [#feature, #actions]
---

# actions — feature

## Purpose
The vocabulary and pipeline by which a player intent becomes a validated, executed, observable game change. Owns the `PlayerIntent → Pipeline approval → Command resolution → State mutation → History envelope` flow, plus the AvailableActions snapshot that tells presentation what is currently legal.

## Boundaries
- **Owned:** action shapes (intent / request / command / history), the pipeline contract, validation layers, handler return-shape semantics, AvailableActions snapshot shape.
- **Not owned:** rules brain (lives in `features/engine/`, called *into* by the pipeline), zone primitives (`features/zones/`), trigger fan-out (`features/triggers/`), animation handoff (`features/presentation/`).

## Composition
- `features/actions/models/01-command-pipeline.md` — canonical Model. Merges and supersedes docs 14, 15, 17, 18, 19 (provenance retained as labeled sections below the divider).
- `features/actions/models/02-availability-snapshot.md` — the snapshot shape used by presentation; mapping rules from snapshot to UI affordances.

## Public surface
PlayerIntent, Action, Command, History envelope, ValidationResult, AvailableActions snapshot. Other features call into the pipeline via well-typed services; presentation reads the snapshot.

## Dependencies
- **Upstream:** `features/entities/`, `features/zones/`, `features/engine/` (called for rules brain), `features/triggers/` (fan-out hooks).
- **Downstream:** `features/presentation/`.

## Settled decisions
ADRs in `features/actions/decisions/` (backfilled in phase 5):
- typed action template, on-demand validation, OnIncomingCommand-only flow, validation in handler, generic primitive rule constraint, player intent mapper, five validation layers, intent vocabulary rename.

## Open
See `ledgers/OPEN-QUESTIONS.md` for action-flow questions.
