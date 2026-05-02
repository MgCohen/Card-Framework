---
type: session
schema-version: 1
status: backfilled
date: 2026-04-29
tags: [#session, #backfill, #workflow, #meta]
---

# Session 11 — 2026-04-29 (backfilled)

## Part 1 — Content

### Active question coming in
What's the canonical doc layer, and how do sessions, ADRs, and ledgers fit together?

### Work done
- Workflow proposal authored across many iterations (commits ranging from `45d8e03` through `985f6eb`):
  - Doc types, templates, session structure (`45d8e03`).
  - Topic-first folders, per-topic counters, `general/` tier (`e0eccfd`).
  - Three-lifetimes for questions, two-part session notes (`5827f8c`).
  - Stripped migration scaffolding from the proposal — scoped to target design (`32ce766`).
  - Split triggers/effects into separate topics (`5c1467b`).
  - Abstract sample template, lock-bar; project-agnostic topic names (`7113ccf`).
  - `features/`, `general/` absorbs foundations, `spikes/`, `support/` (`94942c4`).
  - Multiple consistency / inconsistency-fix passes (`7f88e3e`, `827798b`, …).
- First pass on workflow docs (`6a5ed2b`); full product builder planning (`cbf52fd`).
- Prep work + `RESEARCH/` → `RESEARCH_BACKUP/` duplication for safety (`67804d9`).
- Renamed canonical knowledge folder to `Product/` in proposal (`8c2a9e7`).

### Decisions settled
- Workflow shape: `general/` + `features/` + `spikes/` + `support/` + `sessions/`.
- 7 doc types: spike, feature, model, sample, adr, session, checkpoint.
- Three-lifetime question handling (durable, scratch, in-flight).
- Captured later as the workflow-adoption ADR `decisions/0001-adopt-workflow.md` (Phase 8).

### Active question going out
Migrate. Move 28 docs, prior art, and the sample into the new structure.

## Part 2 — Links & open threads

### References
- Docs touched: `ProductBuilder/PRODUCT-BUILDER-PROPOSAL.md`, `ProductBuilder/MIGRATION-GUIDE.md`, `ProductBuilder/FLOW-SIMULATION.md`, `ProductBuilder/AI-FIRST-AUTOMATION.md`, `ProductBuilder/PRODUCT-BUILDER-GAPS.md`.
- Related: PR #14 (`aa2ff71`), PR #15 (`b1dd2c0`).
