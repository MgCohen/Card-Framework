---
type: adr
status: accepted
last-updated: 2026-04-29
tags: [#adr, #effects, #async]
---

# ADR-0001 — Async layer hierarchy

Date: 2026-04-29
Context-doc: features/effects/models/01-async-dispatch.md
Proposal: features/effects/models/01-async-dispatch.md

## Context
Async behavior in a card game spans several distinct kinds: animation handoff (presentation), decision wait (player input), simulation pause (rule), and resolver yield (engine). Treating all of them with one `Task` returned from a handler smears responsibilities.

## Options considered
- A: Single async layer (`Task<Result>` everywhere) — rejected because it conflates animation, decision, and simulation pauses.
- B: No async — rejected because real games need decisions and animation handoff.
- C: Layered hierarchy: presentation-async (animation), decision-async (player input via `IAvailableActions` decision-scoped snapshot), engine-async (resolver yield). Each layer has its own continuation/handle. Chosen.

## Decision
Async behavior is layered. Each layer owns its own continuation (animation, decision, engine yield). Effects compose across layers via the handler context, never by smashing them into one `Task`.

## Consequences
- Enables clean handoffs and per-layer testability.
- Constrains: handlers and effects must declare which layer they're awaiting.
