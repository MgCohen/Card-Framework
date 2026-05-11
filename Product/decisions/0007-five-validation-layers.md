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

## Amendment — 2026-05-11 (validator / handler split per ADR-0010)

Per [ADR-0010](decisions/0010-validator-handler-split.md), the "authoritative legality at dispatch" layer changes shape: instead of a single `ICommandHandler<TCommand, TResult>.Validate` method, it is a **registered `ICommandValidator<TCommand>` chain** running in DI order with short-circuit-on-reject. Framework defaults `SessionGate` and `MatchStateActionGate` ride the same chain (replacing the inline lifecycle gate + match-state-actions gate). The five-layer principle is unchanged in spirit — five named layers, one authoritative answer composed at dispatch — but the named layers refresh:

| Pre-ADR-0010 layer | Post-ADR-0010 expression |
|---|---|
| Per-template eligibility (runtime) | Per-state `MatchState.Actions` (engine) — refined by [ADR-0009](decisions/0009-engine-as-fsm.md); UI-side projection per [ADR-0012](decisions/0012-presentation-per-state-view-models.md) |
| `AffordanceMap` (presentation) | Per-state `MatchStateViewModel<TState>` affordances per [ADR-0012](decisions/0012-presentation-per-state-view-models.md) |
| `IIntentMapper.Map` well-formedness (actions) | Per-VM `Map(intent)` per [ADR-0012](decisions/0012-presentation-per-state-view-models.md) (well-formedness role unchanged) |
| `handler.Validate` authoritative (actions) | Registered `ICommandValidator<TCommand>` chain (per [ADR-0010](decisions/0010-validator-handler-split.md)); authoritative role unchanged |
| Engine lifecycle gate (framework, ADR-0006) | `SessionGate` validator (framework default in the chain) |
| *(new)* — | `MatchStateActionGate` validator (framework default in the chain; cheap prophylactic type gate from ADR-0009's `MatchState.Actions`) |

`MatchStateActionGate` is technically a sixth checkpoint structurally, but it shares the same "framework-owned flow gate" role as `SessionGate` and lives in the same chain as a co-default; the canonical reference is the five-rejection-paths table in [`engine/03-game-rules-runtime.md`](features/engine/models/03-game-rules-runtime.md). The five-layer mental model is preserved (UI affordance / well-formedness / framework gates / authoritative legality / mid-execution cancel).

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
