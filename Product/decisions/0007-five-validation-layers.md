---
type: adr
status: accepted
references: [features/actions/models/01-command-pipeline.md, features/engine/models/02-gamerules-contract.md, features/presentation/models/01-presentation-loop.md]
tags: [#adr, #actions, #engine, #presentation, #validation]
---

# ADR-0007 — Five validation layers, with handler.Validate authoritative

Date: 2026-04-29 (originally landed as `features/actions/decisions/0007`; elevated to project-wide on 2026-05-07 per §4.5 — names 5 layers each *owned* by a different feature; structure constrains how those owned surfaces behave together).

Context-doc: `features/actions/models/01-command-pipeline.md`

## Context

"Where is the validation?" was unanswerable because validation was scattered. The framework needed an explicit list of layers, with a single authoritative one, so each layer's role is clear.

This is project-wide because the five layers live across **three** features: `engine/` owns rule-side eligibility (per-template) and `OnIncomingCommand` (flow); `actions/` owns `IntentMapper` (well-formedness) and `handler.Validate` (authoritative); `presentation/` owns `AffordanceMap` (UI gating). Each feature's owned surface must implement compatibly — a single ADR captures the contract.

## Options considered

- **A — Single validation layer (handler only).** Rejected: UI can't gate buttons accurately; AffordanceMap has no role.
- **B — Two layers (rule + handler).** Rejected: real-world gating involves UI, mapper, and intake.
- **C — Five named layers**, each with a defined role: per-template eligibility (rule, in `OnQueueDrained`), AffordanceMap (UI), `IntentMapper` (well-formedness), `handler.Validate` (authoritative at dispatch), `OnIncomingCommand` (flow gate). **Chosen.**

## Decision

Five layers, each with a defined role.

| Layer | Owner feature | Concern | When |
|---|---|---|---|
| **Per-template eligibility** | `engine/` (`GameRulesRuntime.OnQueueDrained`) | "Which templates are even relevant right now?" — phase, owner, available cards | Snapshot construction |
| **`AffordanceMap`** | `presentation/` | "Which templates are actionable for this actor in this UI context?" — UI affordance projection | UI render |
| **`IIntentMapper.Map`** | `actions/` | "Convert this template + choice into a well-formed `GameCommand`" — well-formedness, no game-rules logic | Intent → command translation |
| **`ICommandHandler.Validate`** | `actions/` | "Is this specific command legal *right now* against current state?" — **authoritative** | Engine intake (every dispatch) |
| **`GameRulesRuntime.OnIncomingCommand`** | `engine/` | "Is the engine accepting commands at all? Where should this go?" — flow / pause / queue routing; never reads command body | Engine intake (every dispatch) |

`handler.Validate` is authoritative at dispatch; the others narrow the search space, gate UI, ensure well-formedness, or enforce game-level flow.

The earliest path with enough information wins:
- If the UI can know it's illegal before submitting → it shouldn't appear in the snapshot, and `Validate` doesn't even need to run.
- If the command is valid in principle but the engine isn't ready → the rule's intake rejects.
- If the command is malformed against current state → `Validate` rejects.
- If a card effect must cancel mid-resolution → before-fold cancel.

`handler.Validate` is the *authoritative* legality check, run at dispatch. The snapshot is the rule's best-effort approximation built from per-template eligibility; per-binding `Validate` may be called on demand by the rule, but it is not the default. The UI's `AffordanceMap` is a projection of the snapshot. The authoritative check happens at dispatch — if the snapshot is approximate, dispatch catches stale bindings and surfaces the rejection.

## Consequences

- **Enables:** clear responsibilities per layer; no overlap, no gaps; predictable rejection paths; UI can gate buttons accurately without duplicating rule logic; dispatch-time authority means the snapshot can be approximate without correctness risk.
- **Constrains:** contributors must place new validation in the correct layer; "where does this go?" is the gating question — putting validation in the wrong layer creates duplication or gaps.
- **Cascade:** consumed by `engine/02-gamerules-contract.md` (the four-method runtime contract leaves room for these layers); `actions/01-command-pipeline.md` (the pipeline orders them); `presentation/01-presentation-loop.md` (`AffordanceMap` consumes the snapshot and projects to UI).
