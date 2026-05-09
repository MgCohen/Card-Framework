---
type: adr
status: accepted
references: [features/actions/models/01-command-pipeline.md, features/presentation/models/01-presentation-loop.md, features/engine/models/03-game-rules-runtime.md]
tags: [#adr, #actions, #validation, #cross-cutting]
---

# ADR-0007 — Five validation layers, with handler.Validate authoritative

Date: 2026-04-29 (elevated from `features/actions/decisions/0007-five-validation-layers.md` to project-wide on 2026-05-08 per §4.5 two-feature rule — cross-cutting across actions + engine + presentation, with each layer owned by a different feature)
Context-docs: `features/actions/models/01-command-pipeline.md`, `features/presentation/models/01-presentation-loop.md`, `features/engine/models/03-game-rules-runtime.md`
Proposal: features/presentation/models/01-presentation-loop.md, features/actions/models/01-command-pipeline.md

## Amendment — 2026-05-07 (triggers + dispatch design pass)

Per [ADR-0006](decisions/0006-two-pipeline-architecture.md), the fifth layer is now the **engine lifecycle gate** (framework, checks `EngineState.Lifecycle`) rather than `OnIncomingCommand` (deleted). The five-layer principle is unchanged; only the name and owner of the flow-level layer shifted from runtime to framework. The other four layers (per-template eligibility, AffordanceMap, IntentMapper, `handler.Validate`) are unaffected.

## Context
"Where is the validation?" was unanswerable because validation was scattered. We needed an explicit list of layers, with a single authoritative one, so each layer's role is clear.

## Options considered
- A: Single validation layer (handler only) — rejected because UI can't gate buttons accurately and AffordanceMap has no role.
- B: Two layers (runtime + handler) — rejected because real-world gating involves UI, mapper, and intake.
- C: Five named layers — per-template eligibility (runtime), AffordanceMap (UI), IntentMapper (well-formedness), `handler.Validate` (authoritative), engine lifecycle gate (framework flow). Chosen.

## Decision
Five layers, each with a defined role. `handler.Validate` is authoritative at dispatch; the others narrow the search space, gate UI, ensure well-formedness, or enforce engine-level flow.

## Consequences
- Enables clear responsibilities per layer; no overlap, no gaps.
- Constrains: contributors must place new validation in the correct layer; "where does this go?" is the gating question.
