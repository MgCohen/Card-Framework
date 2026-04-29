---
type: session
status: backfilled
last-updated: 2026-04-29
date: 2026-04-26
tags: [#session, #backfill, #foundations]
---

# Session 01 — 2026-04-26 (backfilled)

## Part 1 — Content

### Active question coming in
What does this framework even need to be? — first pass.

### Work done
- Initial check-in and `RESEARCH/` scaffold (commits: `c7c020f`, `0772b35`).
- First framework wave: vision, archetypes, capability map, architecture, scaffold-fit (`a1a988f`).
- Engine definition and initial roadmap (`5c07eec`).
- First session checkpoint (`9f3f4db`).
- Doc 18 alignment with engine/evaluator model (`adbeac7`); envelope-grouping fix (`9701d37`).

### Decisions settled
- Layered framework (simulation core / authoring / presentation / input / integration).
- `Game = ruleset+content`, `Session = run`, definition/instance separation.

### Active question going out
Action vocabulary still loose; what is `Request` vs `Command` vs `Action`?

## Part 2 — Links & open threads

### Followups created
- Promote: action-flow vocabulary (became session 02 work).

### References
- Docs touched (now in PRODUCT/): `general/VISION.md`, `general/GAME-ARCHETYPES.md`, `general/CAPABILITY-MAP.md`, `general/ARCHITECTURE.md`, `general/ROADMAP.md`, `spikes/scaffold-state-fit.md`, `features/actions/notes/01-command-pipeline.md` (formerly doc 18).
- Related: PR #1 (`c1a86bb`).
