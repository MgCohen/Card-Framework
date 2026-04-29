---
type: adr
status: accepted
last-updated: 2026-04-29
tags: [#adr, #effects, #replacement, #priority]
---

# ADR-0003 — Replacement priority: deterministic, fold-ordered

Date: 2026-04-29
Context-doc: features/effects/notes/02-replacement-prevention.md
Proposal: features/effects/notes/02-replacement-prevention.md, features/triggers/notes/01-trigger-fold.md

## Context
Replacement effects ("if X would happen, instead Y") and prevention effects ("X doesn't happen") need a deterministic priority story. Without one, two replacements in the same fold produce nondeterministic outcomes.

## Options considered
- A: Order replacements by registration time — rejected because registration order isn't a deterministic, replay-safe property.
- B: Player-controlled tie-breaks at every collision — rejected because it spams decisions for negligible gain in floor cases.
- C: Fold-ordered priority — replacements declare priority ranks; the trigger fold consumes them in rank order; ties break by stable, explicit ordering (e.g., source entity ID). Chosen.

## Decision
Replacements carry an explicit priority. The trigger fold processes them in rank order. Ties break by a stable, replay-safe rule (e.g., source entity ID). Player-controlled tie-breaks are an opt-in extension.

## Consequences
- Enables deterministic replay of replacement-heavy effects.
- Constrains: rule authors must assign priorities; no "implicit" defaults.
