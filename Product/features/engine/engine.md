---
type: feature
status: settled
references: [features/entities/models/01-definition-instance-model.md, features/zones/models/01-zone-model.md, features/triggers/models/01-trigger-fold.md, features/effects/models/04-effect-flow.md]
tags: [#engine]
---

# engine — feature

## What it is

The orchestrator. Holds the **evaluation loop** that drains commands, the **rules layer contract** (`GameRulesDefinition` + `GameRulesRuntime<T>`), the **match construction** machinery (`IGameFactory`, `MatchSetup`), the **snapshot production pipeline** (`IAvailableActions` service), the **GameState slice catalog** (framework-owned + game-authored tiers), and the **match lifecycle** (`NotStarted` / `Started` / `Ended`). Other features feed into it (entities, zones, triggers, effects); the actions pipeline calls into the runtime; presentation reads the snapshot from state.

```
                    Factory.Build(MatchSetup)
                         │
                         ▼
              Engine { NotStarted, Store, Rules }
                         │
                  host.StartMatch()
                         │
                         ▼
                   Lifecycle: Started
                         │
                         ▼
        ┌──────────────────────────────────────────┐
        │   Dispatch(GameCommand)                  │
        │     → Lifecycle gate                     │
        │     → handler.Validate                   │
        │     → GameRules.OnIncomingCommand        │
        │     → queue → Drain:                     │
        │       └─ pipeline.Execute → handler      │
        │       └─ rules.Evaluate → follow-ups     │
        │     → OnQueueDrained                     │
        │       └─ Actions.Set(snapshot)           │
        └──────────────────────────────────────────┘
                         │
                         ▼ (win/loss condition)
            EndMatchCommand → Lifecycle: Ended
```

## Vocabulary

Feature-owned terms. Cross-feature terms — `GameCommand`, `GameAction`, `CompositeAction`, `GameSignal`, `Trigger<T>`, `TriggerMatch`, `TriggerPhase`, `DispatchOrigin` (defined in `triggers/`); `IEffectScope`, `IHandlerContext`, `ICommandAwaiter`, `Effect`, `Mutator`, `Store`, `IStateScope`, `IStoreScope`, `BaseState`, `IModifierFactory`, `IAvailableActions`, `AvailableActionsSnapshot`, `ActionTemplate` (defined in `effects/` and `actions/`); `IIntentMapper`, `IntentMapper`, `CommandResult`, `HistoryEnvelope`, `ICommandHandler`, `ICommandPipeline` (defined in `actions/`); `Card`, `Zone`, `OwnershipState`, `InstanceId`, `Actor`, `ActorId`, `IReference` — live in their owning features and `Product/ledgers/GLOSSARY.md`.

| Term | Meaning |
|---|---|
| `GameEngine` | The orchestrator. Holds the queue, runs the drain loop, gates lifecycle, dispatches commands. |
| `OriginCommandId` | ID of the first command in a chain. The engine threads it from each just-executed command onto every evaluator follow-up. |
| Drain | One pass of the evaluation loop: dequeue → execute → evaluate → enqueue follow-ups → repeat until queue empty → `OnQueueDrained`. |
| `IntakeResult` | Outcome of `Dispatch` and `OnIncomingCommand`: `(Accepted, Reason?)`. |
| `DrainResult` | Outcome of `OnQueueDrained`: list of follow-up commands the engine should dispatch. |
| `GameRulesDefinition` | Pure `[SerializeField]` data class; authored config (starting life, hand size, format knobs). One per game-mode variant. |
| `GameRulesRuntime<TDefinition>` | Stateful runtime brain with framework services + game-specific subsystems + typed `Definition`. DI-resolved per match. |
| `IIntakeQueue` | Live engine queue surface: `AddFirst`, `AddLast`, `Remove`, iteration. Injected into the runtime for routing decisions. |
| `IAvailableActions` | Single snapshot writer service: `Set` / `Append` / `Clear`. The runtime and effects publish through here; the engine never auto-publishes. |
| `IEntityRegistry` | Engine-owned `InstanceId → handle` registry for entities. Spawn / Get / TryGet / Has / Destroy surface. |
| `EngineLifecycle` | Enum: `NotStarted`, `Started`, `Ended`. Source of truth on `EngineState.Lifecycle`. |
| `MatchOutcome` | Closed type hierarchy: `Win(Actor)`, `Loss(Actor)`, `Draw`, `Aborted(Reason)`. |
| `EngineState` | Framework-owned state slice: `(Lifecycle, IsRunning, CurrentCommand)`. |
| `HistoryState` | Framework-owned state slice: ordered `HistoryEnvelope` log. |
| `AvailableActionsState` | Framework-owned state slice holding the published `AvailableActionsSnapshot`. |
| `MatchSetup` | Per-match input bundle: `(Rules, Actors, InitialSlices, Seed?)`. |
| `IGameFactory` | DI-resolved service that constructs a fresh `GameEngine` for a match: `Create(setup)` / `NewMatch()` (fluent). |
| `IMatchBuilder` | Fluent sugar for building a `MatchSetup` (`WithRules / WithActors / WithInitialSlices / WithSeed / Build`). |
| `StartMatchCommand` / `EndMatchCommand` | Framework-owned `GameAction`s that flow through the normal pipeline to transition `EngineLifecycle`. |

## Architecture

The feature has six Models. The execution arc connects them:

1. **Match construction** (Model 05). Host opens a per-match DI scope, resolves `IGameFactory`, calls `factory.Create(setup)` (or fluent `NewMatch().WithX().Build()`). Factory pairs `GameRulesDefinition` with `GameRulesRuntime<TDefinition>` via container open-generic resolution; framework slices auto-seed; engine returns in `NotStarted`.

2. **Match start** (Model 06). Host calls `engine.StartMatch()` → dispatches `StartMatchCommand` → handler emits `MatchStartedSignal`, flips `EngineState.Lifecycle` to `Started` via `SetEngineLifecyclePayload`. First `OnQueueDrained` runs; runtime publishes initial `AvailableActions` snapshot.

3. **Evaluation loop** (Model 01). Per `Dispatch`: lifecycle gate → `pipeline.Validate` → `GameRules.OnIncomingCommand` (intake routing) → queue → drain. Drain: dequeue → `pipeline.Execute` (before-fold → handler → after-fold → envelope) → `rules.Evaluate(justExecuted, result)` → enqueue follow-ups (with threaded `OriginCommandId`) → repeat → `OnQueueDrained`.

4. **GameRules contract** (Model 02). The runtime implements four hooks: `OnIncomingCommand` (intake flow only, never reads command body), `FilterTriggers` (availability — phase, mana, stack, target validation), `Evaluate` (state-based follow-ups; reads state, never command type), `OnQueueDrained` (idle hook — publishes snapshot via `Actions.Set`, returns follow-up commands).

5. **Snapshot production** (Model 03). Service-driven: `IAvailableActions.Set(snapshot)` is the single publication path. Default site is `OnQueueDrained`; effects mid-execution write through the same service for decision-scoped snapshots. Engine never auto-publishes. Validation is **on-demand** — handler.Validate at dispatch is authoritative; the rule may opt into per-binding pre-validation during template enumeration.

6. **State slices** (Model 04). State is a Scaffold `Store` with typed slices; no monolithic `GameState` record. Framework-owned slices (`AvailableActionsState`, `EngineState`, `HistoryState`) are auto-seeded at `Build()` and writable only via framework services. Game-authored slices (e.g. `PilesState`, `LastMoveState`) follow the same `BaseState` pattern. Reads always go through `IStateScope.Get<TState>()`.

7. **Match end** (Model 06). Either: runtime dispatches `EndMatchCommand(outcome)` from `Evaluate` (state-driven win/loss); host calls `engine.FinishMatch(outcome)` (host-driven); or `engine.Dispose` on a non-`Ended` engine (synthesizes `Aborted("Disposed")`). Handler emits `MatchEndedSignal(outcome)`, flips `Lifecycle` to `Ended`. Lifecycle gate rejects subsequent dispatches.

## Composition

| Model | Role |
|---|---|
| [`models/01-evaluation-loop.md`](models/01-evaluation-loop.md) | Engine class, drain loop, dispatch entry, OnQueueDrained, evaluator-reads-state principle |
| [`models/02-gamerules-contract.md`](models/02-gamerules-contract.md) | `GameRulesDefinition` + `GameRulesRuntime<TDefinition>` split; four contract methods; service injection; zero-state rule |
| [`models/03-snapshot-pipeline.md`](models/03-snapshot-pipeline.md) | `IAvailableActions` service-driven model; typed `ActionTemplate` per command type; on-demand validation posture |
| [`models/04-gamestate-slices.md`](models/04-gamestate-slices.md) | Framework-owned + game-authored slice tiers; subscription channels; atomic batches |
| [`models/05-game-factory.md`](models/05-game-factory.md) | Two-layer split (DI installation vs per-match factory); `IGameFactory` / `MatchSetup` / `IMatchBuilder`; container open-generic resolution |
| [`models/06-match-lifecycle.md`](models/06-match-lifecycle.md) | `EngineLifecycle` states; `StartMatch` / `FinishMatch` surface; lifecycle-as-commands; `MatchOutcome` hierarchy; disposal boundary |

## Invariants

Cross-Model rules that hold across the six Models and shape how downstream features must use the engine.

- **The engine is the single orchestrator.** All commands flow through `engine.Dispatch`, regardless of source (player intent, timer, trigger reaction, internal). The engine knows nothing about command meaning; the runtime knows.
- **Lifecycle gate is framework-owned and uniform.** `Dispatch` rejects pre-`Started` (except `StartMatchCommand` / `EndMatchCommand`) and post-`Ended` uniformly, before the runtime's `OnIncomingCommand` runs.
- **Zero game-affecting state on the runtime.** The runtime holds injected services and pure configuration. All gameplay memory lives as Scaffold slices; the runtime is replaceable at any point if its slices survive.
- **Service-injected access to state, queue, snapshot.** `GameRulesRuntime<T>` constructor injects `IStateScope`, `IAvailableActions`, `IIntakeQueue` plus game-specific subsystems. No method takes them as parameters; ephemeral data only.
- **Snapshot lives in state, written via service.** `AvailableActionsState` slice; `IAvailableActions.Set` is the only write path. Presentation reads uniformly via state subscription.
- **Framework slices auto-seeded; game slices passed.** `IGameFactory.Build` seeds framework slices internally; game authors never include them in `MatchSetup.InitialSlices`. Game-authored slices follow the same `BaseState` pattern.
- **Lifecycle mirrored on `EngineState`.** Source of truth in state; `engine.Lifecycle` is a convenience accessor.
- **Lifecycle transitions are commands.** Every `NotStarted → Started` and `* → Ended` transition flows through the normal pipeline as `StartMatchCommand` / `EndMatchCommand`. Signals (`MatchStartedSignal`, `MatchEndedSignal`) land in normal envelopes.
- **State immutability.** `GameState` slices are immutable records; mutators react to payloads via `Store.Execute(payload)`. ADR-style replay/snapshot/per-player-views are first-class.
- **Evaluator reads state, not command type.** `Evaluate` matches on game state ("any card in PlayArea must execute") rather than on `justExecuted`'s type. State-based evaluation handles all sources uniformly.

## Boundaries

**Owns:** the `GameEngine` class; `ICommandPipeline` framework half (loop wiring); the `GameRulesDefinition` + `GameRulesRuntime<T>` contract; the four `GameRules` hooks; `IIntakeQueue` service; `IAvailableActions` service; `IGameFactory` + `MatchSetup` + `IMatchBuilder`; framework-owned state slices (`AvailableActionsState`, `EngineState`, `HistoryState`); the lifecycle (`EngineLifecycle` enum, `StartMatchCommand` / `EndMatchCommand`, `MatchOutcome`); the `IEntityRegistry` service interface (implementation is engine-side construction).

**Does not own:**
- The action pipeline body (handler dispatch, before/after fold, envelope) → `actions/` (consumed via `ICommandPipeline`)
- Trigger fold mechanics, `ITriggerPipeline`, `ITriggerSystem` → `triggers/`
- Effect orchestration, `IEffectScope`, `IHandlerContext`, `ICommandAwaiter` → `effects/`
- Entity model, modifier factory implementation → `entities/`
- Card-specific shape, `Card`, `CardRef`, `CardMapState` → `cards/`
- Zone-specific shape, `Zone`, `ZoneContentsState` → `zones/`
- Animation, presentation-side async, transport filter → `presentation/`
- `IIntentMapper` implementation → game-side (registered through DI)
- Game-specific subsystems (`IStackSystem`, `IDeckSystem`, etc.) → game-side

## Dependencies

*Mirrors the front-matter `references:` array in human-readable form (per §3).*

- **Upstream:** `entities/` (foundational definition/instance model; `IEntityRegistry` consumed; modifier factory id allocation), `zones/` (zone slices coexist with engine slices), `triggers/` (type hierarchy is the engine's command vocabulary), `effects/` (handler / effect / mutator layers; `IHandlerContext`, `IEffectScope`, `ICommandAwaiter` shapes the engine consumes).
- **Downstream:** `actions/` (calls into `GameRulesRuntime`; consumes `IAvailableActions` for snapshot reads), `presentation/` (subscribes to `AvailableActionsState`, `HistoryState`; reads `EngineState.Lifecycle`).

## Open

See `ledgers/OPEN-ITEMS.md`:

- **`HistoryEnvelope` typed definition** — concrete C# record (fields: `envelopeId`, `originCommandId`, `actor`, `entries[]`). Pseudocode currently; pin when replay or multiplayer transport is designed.
- **`oq-engine-builder`** — engine-side service composition specifics (where `IEntityRegistry`, `IModifierIdGenerator`, `IInstanceIdGenerator` are constructed, registered, exposed).
- **`oq-game-definition`** — `GameDefinition` bundle + definition-declared slice schema + per-definition initial-value instantiation. Next pass moves slice schema into definitions and rolls them up per game-mode.
- **`oq-gamerules-composition`** — module composition contract for `GameRulesRuntime` (provider/policy ordering, enable-disable, conflict resolution). Service surface (`Append` / `Clear`) supports it mechanically; pin when first composed-rules game forces it.
- **MTG-strict SBA ordering hook** — pre-after-fold evaluator hook reserved for the first game that needs MTG-strict state-based-action timing. Currently a documented known difference.
- **`IIntakeQueue` shape** — final operations and `CurrentlyExecuting` location pinned during engine implementation.
- **`IntakeResult.Reason`** — free-form string vs. structured enum. Defer until a real game forces the choice.
- **Slice catalog growth** — new framework-owned slices may emerge as features mature.
- **Symmetric unsubscribe for `SubscribeAllReferences` / `SubscribeAny`** — listeners currently leak across the store's lifetime; expose unsubscribe overloads when presentation reaches for them.
- **Async-effect cancellation on match end** — when `EndMatchCommand` resolves while an effect is mid-`await`, threading a `CancellationToken` through `IHandlerContext` / `IEffectScope` is open.
- **Multi-winner / team `MatchOutcome`** — single-actor `Win` today; add `TeamWin` when a co-op game forces it.
- **Per-match scope ownership convention** — host owns the per-match DI scope; concrete pattern (`GameSession` controller, scope-builder helper, etc.) pinned when a real integration surfaces.
