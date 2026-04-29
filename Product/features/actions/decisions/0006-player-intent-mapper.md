---
type: adr
status: accepted
last-updated: 2026-04-29
tags: [#adr, #actions, #intent, #mapper]
---

# ADR-0006 — PlayerIntent → IIntentMapper → GameCommand

Date: 2026-04-29
Context-doc: features/actions/models/01-command-pipeline.md
Proposal: features/actions/models/01-command-pipeline.md, features/actions/models/02-availability-snapshot.md

## Context
UI gestures (drag, click, target) produce something that is *not yet* a `GameCommand`. We needed a layer that takes UI intent + the chosen template and produces a well-formed command, without UI knowing command shapes.

## Options considered
- A: UI constructs `GameCommand` directly — rejected because UI ends up carrying command-shape knowledge for every game.
- B: Reflection-driven slot filling — rejected because it's fragile and runtime.
- C: `IIntentMapper.Map(template, choice) → GameCommand`, switch on typed template subtype, returns the well-formed command. Chosen.

## Decision
`PlayerIntent` carries UI-level data; `IIntentMapper` consumes a typed `ActionTemplate` plus a `PlayerChoice` and returns the matching `GameCommand`. Each game ships its own mapper; the framework provides the contract.

## Consequences
- Enables UI that only knows templates and choices — not command shapes.
- Enables compile-time exhaustiveness via the typed switch.
- Constrains: every command needs a paired template (already covered by ADR-0001).
