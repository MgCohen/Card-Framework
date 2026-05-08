---
type: adr
status: superseded
tags: [#adr, #actions, #flow]
superseded-by: decisions/0006-two-pipeline-architecture.md
---

# ADR-0003 — OnIncomingCommand is purely flow (SUPERSEDED)

Date: 2026-04-29
Superseded: 2026-05-07 (triggers + dispatch design pass)
Context-doc: features/actions/models/01-command-pipeline.md
Proposal: features/actions/models/01-command-pipeline.md, features/engine/models/02-gamerules-contract.md

## Status — superseded

`OnIncomingCommand` itself is deleted per [ADR-0006 — Two-pipeline architecture](decisions/0006-two-pipeline-architecture.md). Walking through Stacklands / Hearthstone / MTG-like game styles concretely showed that command-queue routing isn't what games actually customize — what differs is *effect-level* orchestration (when each effect starts, whether effects nest / batch / stack). Per-command primitives (modify / cancel / replace / dispatch) are framework-level via `IEffectScope` during the trigger fold; pause/end gating moved to the framework lifecycle gate on `engine.Dispatch`; game-flow legality lives in `pipeline.Validate`.

The "purely flow" principle this ADR locked is preserved in spirit — the framework still distinguishes flow-level concerns from per-command body inspection — but the named hook is gone. New work targets the forthcoming effect-pipeline contract (`oq-effect-pipeline`) for game-customizable orchestration.

The original text is preserved below as historical record.

---

## Context (historical)
`OnIncomingCommand` initially mixed three concerns: pause/end gating, queue routing, and command-body inspection. This forced the rule to know about every command shape and made composition fragile.

## Options considered (historical)
- A: Allow `OnIncomingCommand` to read command bodies for early validation — rejected because it duplicates handler-side validation and couples the rule to every command type.
- B: Replace `OnIncomingCommand` with per-command callbacks — rejected because it loses the flow-level concerns (pause/end).
- C: `OnIncomingCommand` is purely flow: pause/end gating + queue routing; never reads the body. Chosen at the time.

## Decision (historical, superseded)
`OnIncomingCommand` checks game-level gating and routes the command into the intake queue. Body inspection happens in `handler.Validate`.

## Consequences (historical)
- Enables a simple, command-shape-agnostic flow check.
- Constrains: the rule can't reject a command based on body contents at intake; the handler does that at dispatch.
