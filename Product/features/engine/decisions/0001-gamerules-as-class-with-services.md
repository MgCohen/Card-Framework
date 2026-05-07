---
type: adr
status: accepted
tags: [#adr, #engine, #gamerules]
---

# ADR-0001 — GameRules is an abstract class with services injected

Date: 2026-04-29
Context-doc: features/engine/models/02-gamerules-contract.md
Proposal: features/engine/models/02-gamerules-contract.md, features/engine/models/03-snapshot-pipeline.md

## Amendment — 2026-05-07 (triggers + dispatch design pass)

The original "Option C" listed `IIntakeQueue` among the injected services. Per ADR-0006 (two-pipeline architecture), `IIntakeQueue` is deleted — the command queue is engine-private FIFO, with no runtime service surface for command-queue routing. The injected-services list is now `IStateScope` + `IAvailableActions`; the runtime contract methods are `FilterTriggers` and `Evaluate` (plus a forthcoming effect-pipeline surface from the next design pass).

The shape of the ADR — abstract class, services injected at construction, methods take only ephemeral data — is unchanged.

## Context
The rules brain originally drifted between "interface returning rich shapes" and "static class with global state." We need a single shape that lets the rule call into framework services (state, snapshot writer) without inheriting framework state and without forcing the rule to wire wiring.

## Options considered
- A: Pure interface (`IGameRules`) — rejected because callers had to thread services through every method, bloating signatures and making evolution painful.
- B: Static class with global services — rejected because it makes per-game state impossible and breaks composition for multi-rule scenarios.
- C: Abstract class with constructor-injected services (Scaffold `IStateScope`, `IAvailableActions`); methods take only ephemeral data. Chosen.

## Decision
`GameRulesRuntime<T>` is an abstract class. Services are injected at construction. Public methods take only the ephemeral data needed to do their job. Composition (multi-rule games) is supported mechanically because services are passed through, not held statically.

## Consequences
- Enables `FilterTriggers` virtual-identity defaults and clean per-game overrides.
- Enables `CompositeGameRules` later without redesigning the contract.
- Constrains: framework owns service lifetime; the rule must not cache or mutate them in ways that break re-entry.
