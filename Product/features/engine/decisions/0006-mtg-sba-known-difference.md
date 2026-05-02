---
type: adr
status: accepted
tags: [#adr, #engine, #mtg, #known-difference]
---

# ADR-0006 — MTG-strict state-based-action ordering is a known semantic difference

Date: 2026-04-29
Context-doc: features/engine/models/02-gamerules-contract.md
Proposal: features/engine/models/02-gamerules-contract.md

## Context
Magic: the Gathering's rules engine performs state-based actions (SBAs) in a tight loop *between* trigger fold and reaction dispatch. Our evaluation loop folds triggers and dispatches reactions in one phase. This is a real semantic difference and we need to acknowledge it rather than pretend it doesn't exist.

## Options considered
- A: Add a pre-after-fold evaluator hook to the loop — deferred until a game actually needs MTG-strict SBA ordering.
- B: Rebuild the loop around an MTG-strict model — rejected because it pessimizes the floor case and most games don't care.
- C: Document as a known difference; implement a workaround (manual SBA pass before reactions) on a per-game basis. Chosen for now.

## Decision
The evaluation loop diverges from MTG-strict SBA ordering by design. Games that need MTG semantics implement the SBA pass themselves until the framework adds the hook. When a real game forces the issue, revisit option A.

## Consequences
- Enables a simpler default loop for the floor sample and Stacklands-class games.
- Constrains: porting MTG-class rules requires a manual SBA pass.
- Re-open trigger: first game that requires MTG-strict ordering.
