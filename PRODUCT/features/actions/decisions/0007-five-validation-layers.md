---
type: adr
status: accepted
last-updated: 2026-04-29
tags: [#adr, #actions, #validation]
---

# ADR-0007 — Five validation layers, with handler.Validate authoritative

Date: 2026-04-29
Context-doc: features/actions/notes/01-command-pipeline.md
Proposal: features/presentation/notes/01-presentation-loop.md, features/actions/notes/01-command-pipeline.md

## Context
"Where is the validation?" was unanswerable because validation was scattered. We needed an explicit list of layers, with a single authoritative one, so each layer's role is clear.

## Options considered
- A: Single validation layer (handler only) — rejected because UI can't gate buttons accurately and AffordanceMap has no role.
- B: Two layers (rule + handler) — rejected because real-world gating involves UI, mapper, and intake.
- C: Five named layers — per-template eligibility (rule), AffordanceMap (UI), IntentMapper (well-formedness), `handler.Validate` (authoritative), `OnIncomingCommand` (flow). Chosen.

## Decision
Five layers, each with a defined role. `handler.Validate` is authoritative at dispatch; the others narrow the search space, gate UI, ensure well-formedness, or enforce game-level flow.

## Consequences
- Enables clear responsibilities per layer; no overlap, no gaps.
- Constrains: contributors must place new validation in the correct layer; "where does this go?" is the gating question.
