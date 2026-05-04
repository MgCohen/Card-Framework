---
type: feature
status: settled
tags: [#feature, #engine]
---

# engine — feature

## Purpose
The orchestrator: evaluation loop, the rules layer contract (`GameRulesDefinition` + `GameRulesRuntime<T>`), match construction, and the snapshot production pipeline. Hosts the rules brain that the actions pipeline calls into. Owns the loop that drains commands, fans triggers, and produces the AvailableActions snapshot.

## Boundaries
- **Owned:** evaluation loop, `GameRulesDefinition`/`GameRulesRuntime<T>` shapes and zero-state requirement, match construction (factory + setup), snapshot production pipeline, snapshot-in-state location.
- **Not owned:** the action pipeline shape (`features/actions/`), trigger/effect mechanics (`features/triggers/`, `features/effects/`), entity model (`features/entities/`).

## Composition
- `features/engine/models/01-evaluation-loop.md` — orchestrator and evaluation loop (formerly doc 20).
- `features/engine/models/02-gamerules-contract.md` — rules layer contract: definition + runtime split, zero-state, services (formerly doc 25).
- `features/engine/models/03-snapshot-pipeline.md` — snapshot production pipeline (formerly doc 29).
- `features/engine/models/04-gamestate-slices.md` — GameState slice catalog: framework-owned vs game-authored tiers, store subscriptions, atomic batches.
- `features/engine/models/05-game-factory.md` — match construction: `IGameFactory`, `MatchSetup`, `IMatchBuilder`, two-layer split between DI installation and per-match factory; pairs `GameRulesDefinition` to `GameRulesRuntime<T>` via container open-generic resolution.
- `features/engine/models/06-match-lifecycle.md` — match lifecycle: `EngineLifecycle` (`NotStarted`/`Started`/`Ended`), `StartMatch`/`FinishMatch`/`Dispose` surface, framework-owned `StartMatchCommand`/`EndMatchCommand` flowing through the normal pipeline, `MatchOutcome` hierarchy, `MatchStartedSignal`/`MatchEndedSignal`.

## Public surface
`GameRulesDefinition` + `GameRulesRuntime<T>` (data + runtime split), `IGameFactory` / `MatchSetup` / `IMatchBuilder`, evaluation loop entry points, snapshot writer (`IAvailableActions`), state-pattern services. The actions pipeline calls into the runtime; presentation reads the snapshot from state.

## Dependencies
- **Upstream:** `features/entities/`, `features/zones/`, `features/triggers/`, `features/effects/`.
- **Downstream:** `features/actions/`, `features/presentation/`.

## Settled decisions
ADRs in `features/engine/decisions/` (backfilled in phase 5):
- Rules layer split into `GameRulesDefinition` + `GameRulesRuntime<T>` (paired via DI container open-generic resolution), zero-state requirement on the runtime, snapshot in state, snapshot writer = `IAvailableActions`, state pattern, MTG-SBA known difference.

## Open
See `ledgers/OPEN-ITEMS.md` for engine-related questions.
