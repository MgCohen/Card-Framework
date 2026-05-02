---
type: adr
status: accepted
tags: [#adr, #actions, #naming, #rename]
---

# ADR-0008 — Intent vocabulary: OriginRequest → OriginIntent etc.

Date: 2026-04-29
Context-doc: features/actions/models/01-command-pipeline.md
Proposal: features/actions/models/01-command-pipeline.md, decisions/0002-naming-alignment-conventions.md

## Context
Earlier docs used `Request` / `OriginRequest` for what the player asks for, and `Command` for what the engine executes. "Request" overloaded against HTTP-style semantics and didn't read clearly when validation was distributed. We renamed to `Intent`.

## Options considered
- A: Keep `Request` everywhere — rejected because the term was conflicting with the on-demand-validation framing.
- B: Use `Proposal` / `Action` / `Command` as a three-tier vocabulary — rejected because it added a layer for no semantic gain.
- C: `PlayerIntent` (UI), `Action` / `ActionTemplate` (snapshot), `GameCommand` (executed). Source/origin fields renamed `OriginIntent`. Chosen.

## Decision
The action-flow vocabulary is `Intent → Action(Template) → Command`. Origin/source fields adopt the `Intent` form. This is the load-bearing rename that cascaded across docs 14, 15, 17, 18, 19, 27, 28.

## Consequences
- Enables consistent reading of the pipeline: an *intent* becomes a typed *action* and dispatches as a *command*.
- Constrains: any code or doc still using `Request` is out of date; cross-ref sweep (phase 6) and naming-conventions ADR (`decisions/0002`) carry the rename.
