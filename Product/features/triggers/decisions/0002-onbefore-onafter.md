---
type: adr
status: accepted
tags: [#adr, #triggers, #onbefore, #onafter]
---

# ADR-0002 — OnBefore<T> / OnAfter<T> replace CommandPreHook/Reaction

Date: 2026-04-29
Context-doc: features/triggers/models/01-trigger-fold.md
Proposal: features/triggers/models/01-trigger-fold.md, features/engine/models/01-evaluation-loop.md

## Context
Earlier docs (notably 17) used `CommandPreHook` and `CommandReaction` for pre/post listeners. The names hid the typed nature of the dispatch and didn't compose cleanly with the trigger fold.

## Options considered
- A: Keep `CommandPreHook` / `CommandReaction` — rejected because the names suggest one-off hooks rather than a typed listener pair.
- B: Single `Trigger<T>` with a `phase` enum — rejected because the symmetry of pre/post is structural and worth surfacing in types.
- C: Typed listener pair `OnBefore<T>` / `OnAfter<T>`, both folded by `ITriggerPipeline`. Chosen.

## Decision
Pre-event triggers are `OnBefore<T>`. Post-event triggers are `OnAfter<T>`. Both are typed by command/event shape; both fold through `ITriggerPipeline`.

## Consequences
- Enables type-safe trigger registration and clearer reading of the loop.
- Reverts/replaces: `CommandPreHook` and `CommandReaction` references in legacy doc 17 (folded into `01-command-pipeline.md` provenance).
