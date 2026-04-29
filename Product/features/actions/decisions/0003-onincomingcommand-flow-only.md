---
type: adr
status: accepted
last-updated: 2026-04-29
tags: [#adr, #actions, #flow]
---

# ADR-0003 — OnIncomingCommand is purely flow

Date: 2026-04-29
Context-doc: features/actions/models/01-command-pipeline.md
Proposal: features/actions/models/01-command-pipeline.md, features/engine/models/02-gamerules-contract.md

## Context
`OnIncomingCommand` initially mixed three concerns: pause/end gating, queue routing, and command-body inspection. This forced the rule to know about every command shape and made composition fragile.

## Options considered
- A: Allow `OnIncomingCommand` to read command bodies for early validation — rejected because it duplicates handler-side validation and couples the rule to every command type.
- B: Replace `OnIncomingCommand` with per-command callbacks — rejected because it loses the flow-level concerns (pause/end).
- C: `OnIncomingCommand` is purely flow: pause/end gating + queue routing; never reads the body. Chosen.

## Decision
`OnIncomingCommand` checks game-level gating and routes the command into the intake queue. Body inspection happens in `handler.Validate`.

## Consequences
- Enables a simple, command-shape-agnostic flow check.
- Constrains: the rule can't reject a command based on body contents at intake; the handler does that at dispatch.
