---
type: adr
status: accepted
tags: [#adr, #actions, #validation]
---

# ADR-0002 — On-demand validation; not eager / not lazy

Date: 2026-04-29
Context-doc: features/actions/models/02-availability-snapshot.md
Proposal: features/actions/models/02-availability-snapshot.md, features/engine/models/03-snapshot-pipeline.md

## Context
Doc 28 §6 framed eligibility as "eager pre-validation vs lazy evaluation." Both extremes have problems: eager pays the cost up front for everything; lazy means presentation can offer commands the rule won't accept.

## Options considered
- A: Eager pre-validation of every binding during snapshot build — rejected because cost grows with `targets × candidates × validators`.
- B: Lazy evaluation only at dispatch — rejected because UI can't gate buttons accurately.
- C: On-demand validation: per-template eligibility filters whole templates; per-binding `Validate` runs at dispatch (authoritative) and on-demand by the rule when it wants exhaustive coverage. Chosen.

## Decision
The rule does not auto-validate during build. Eligibility filters at the template level cheaply remove whole categories. Per-binding validation runs at dispatch and, when needed, the rule walks the candidate space to publish a fully-validated snapshot.

## Consequences
- Enables snapshot builds at floor-game cost, with opt-in exhaustive checks for games that demand them.
- Constrains: presentation must accept that "template offered" is weaker than "guaranteed valid"; AffordanceMap layer absorbs UI-side gating.
