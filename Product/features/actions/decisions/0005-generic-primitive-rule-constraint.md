---
type: adr
status: accepted
tags: [#adr, #actions, #commands, #primitives]
---

# ADR-0005 — Generic command primitives, rule-enforced constraints

Date: 2026-04-29
Context-doc: features/actions/models/01-command-pipeline.md
Proposal: features/actions/models/01-command-pipeline.md

## Context
Specialized commands like `MoveTopCardCommand` accumulate quickly: top-card, bottom-card, random-card, n-cards, etc. Each one duplicates `MoveCardCommand` plus a constraint baked into the type. This explodes the command catalog and fragments handler logic.

## Options considered
- A: Ship specialized commands per common pattern — rejected for combinatorial explosion of command types.
- B: Ship one parameterized command (`MoveCardCommand` with a "selector" enum) — rejected because the selector becomes a stringly-typed sub-language inside the command.
- C: Ship generic primitives (`MoveCardCommand`); rules enforce constraints via per-template eligibility and `Validate`. Chosen.

## Decision
The framework ships generic primitives. "Top card", "random card", "from hand only" are constraints expressed by the rule when constructing templates and validated in handlers. The framework does not ship `MoveTopCardCommand`.

## Consequences
- Enables a small, stable command catalog.
- Constrains: rules carry more constraint logic, but it lives in the right place (the rule already knows the game).
