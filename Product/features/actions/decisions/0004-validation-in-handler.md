---
type: adr
status: accepted
tags: [#adr, #actions, #validation, #handler]
---

# ADR-0004 — Validation is authoritative in the handler

Date: 2026-04-29
Context-doc: features/actions/models/01-command-pipeline.md
Proposal: features/actions/models/01-command-pipeline.md

## Context
Validation was previously distributed across the rule and the handler with no clear authority. When two layers disagreed, behavior depended on dispatch order. We needed a single authoritative checkpoint.

## Options considered
- A: Validation lives entirely in the rule — rejected because the rule shouldn't have to know command shapes; couples too tightly.
- B: Validation lives entirely in handlers, with the rule delegating — rejected only because gating concerns (pause/end) belong upstream.
- C: `ICommandHandler<T,R>.Validate` is authoritative at dispatch; the rule does flow-level gating only. Chosen.

## Decision
`handler.Validate` is the single authoritative validity check at dispatch. Other layers (per-template eligibility, AffordanceMap, IntentMapper, OnIncomingCommand) are advisory or specialized; the handler is final.

## Consequences
- Enables one canonical answer to "is this command valid right now?"
- Constrains: handler implementations carry the bulk of validation logic, kept local to the command shape.
