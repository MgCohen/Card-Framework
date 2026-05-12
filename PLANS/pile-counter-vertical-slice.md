---
type: plan
status: active
tags: [#plan, #pile-counter, #vertical-slice]
---

# Pile Counter — Vertical Slice Implementation Plan

The first end-to-end exercise of the settled framework. Implements [`Product/support/samples/pile-counter.md`](../Product/support/samples/pile-counter.md) §9 against current `Assets/` (empty). Tests every contract under real game pressure now that the FSM-arch cascade + ADR-0013 + spike Q-walk locks have landed.

## Locked decisions

1. **D-PileRefs** — game-side singleton slice (`PileRefsState : BaseState` holding `IReadOnlyList<Ref<Zone>>`), declared by `PileCounterGameDefinition.GetInitialSlices()` with empty initial value. The factory's Build walk seeds it after spawning the four `PileDefinition` zones. Resolves [`bl-pile-refs-index`](../Product/ledgers/OPEN-ITEMS.md) as game-side; framework-generic shape deferred until a second consumer surfaces.
2. **D-Presentation** — debug-only IMGUI runtime panel: button matrix for `Source → Destination` moves driven by `PilePlayingViewModel.Moves`, text readouts for top card / counter / target per pile, log lines for signals + rejections, "New puzzle" button. No drag-and-drop, no UI Toolkit. Wires the VM through a minimal presenter that observes `MatchStateSlice` + looks up `PilePlayingViewModel` via `ViewModelRegistry`.
3. **D-Post-cascade-build** — slice builds **directly** against the post-cascade architecture (ADR-0009 / ADR-0010 / ADR-0011 / ADR-0013 all landed; ISBA retired from framework; lifecycle hooks are methods; engine auto-emits `EndMatchCommand` on `EndOfMatchState`). No intermediate phase that targets pre-cascade vocab.

## Phases

### Phase 0 — Wiring (≈ 0.5d)

- Browse `C:\Unity\Scaffold\Assets\Packages\` and pull required `com.scaffold.*` packages into `Packages/manifest.json` (state, DI bindings, whatever Scaffold ships for `BaseState` / `Store` / `Mutator<TState, TPayload>`). Cross-check against `C:\Unity\Gear-Engine\Packages\manifest.json` for the canonical wiring per `DOCS/scaffold.md`.
- Asmdefs: `CardFramework.Runtime`, `CardFramework.Engine`, `CardFramework.PileCounter`, `CardFramework.PileCounter.Debug`.
- Empty Unity scene with a bootstrap `GameObject` hosting the VContainer scope.

**Exit:** project compiles; Scaffold types resolve; an empty scene loads.

### Phase 1 — Framework primitives (≈ 2d)

Build top-down, smallest first. Verify each layer compiles before the next.

1. **References.** `Reference`, `Ref<T>`, `CardRef` (`readonly record struct` with `NewRef()` factory). [`entities/02-instance.md`](../Product/features/entities/models/02-instance.md), [`cards/01-card-model.md`](../Product/features/cards/models/01-card-model.md).
2. **Command hierarchy (4 siblings).** `GameCommand` (abstract record) → `GameAction`, `GameSignal`, `Request<TResponse>`, `Response`. `CommandResult` base. `CompositeMutation` (separate; out-of-family, lives on `IEffectScope.Execute`). Per [ADR-0013](../Product/decisions/0013-engine-pipeline-orchestration.md) routing table. [`actions/01-command-pipeline.md`](../Product/features/actions/models/01-command-pipeline.md).
3. **Entity bridge.** `IEntityDefinition`, `DefinitionWithSlices`, `InstanceWithSlices<TDef>` (`protected Definition`), `ISliceProvider`. `Variable`, `VariableValue`, `IntVariableValue`. [`entities/01-definition.md`](../Product/features/entities/models/01-definition.md), [`entities/02-instance.md`](../Product/features/entities/models/02-instance.md), [`entities/03-variable.md`](../Product/features/entities/models/03-variable.md).
4. **Framework slices.** `MatchStateSlice` (wraps polymorphic `MatchState` per [ADR-0009](../Product/decisions/0009-engine-as-fsm.md)), `HistoryState`, `ZoneContentsState`, `CardMapState` (bidirectional `RefOf`/`InstanceOf`), `OwnershipState`, `CardVisibilityState`, `PendingEffectsSlice`, `RequestsSlice`, `AnswersSlice`. `EngineState` minimal (no `EngineLifecycle` enum — `SessionState` hierarchy lives on `engine.Session` as a typed field per ADR-0009 D12). [`engine/01`](../Product/features/engine/models/01-game-engine.md), [`engine/07-match-state.md`](../Product/features/engine/models/07-match-state.md), [`cards/01`](../Product/features/cards/models/01-card-model.md), [`zones/01`](../Product/features/zones/models/01-zone-model.md), [`actions/04-history-envelope.md`](../Product/features/actions/models/04-history-envelope.md), [`effects/05-pending-effects.md`](../Product/features/effects/models/05-pending-effects.md), [`effects/06-request-system.md`](../Product/features/effects/models/06-request-system.md).
5. **Composite mutations.** `MoveCardComposite : CompositeMutation` (post-ADR-0013 rename of the former `MoveCardMutation : CompositeAction`) with `.AtTop(card, source, dest)` factory + `Apply(scope)` expansion to `ZoneContentsState` removes/inserts and `CardMapState` rotation. `CreateCardAction` (NOT a composite; it's a `GameAction` that spawns a card instance + adds it to a zone via its handler). [`cards/01 § Mutations`](../Product/features/cards/models/01-card-model.md).
6. **Zone supports.** `ZoneVisibilityPolicy` (sealed-record hierarchy: `AllPublic`, `AllHidden`, `OwnerSees`, …), `InsertPosition` (sealed-record hierarchy: `AtTop`, `AtBottom`, …). [`zones/01-zone-model.md`](../Product/features/zones/models/01-zone-model.md).
7. **Validators + handlers (split per ADR-0010).** `ICommandValidator<TCommand>` interface + `CommandValidator<TCommand>` abstract base (with `[Inject] Store`). `ICommandHandler<TCommand, TResult>` interface + `GameCommandHandler<TCommand, TResult>` abstract base (with `[Inject] Store` + `[Inject] IEffectScope` for `scope.Execute` access). Validator chain composition: `ICommandPipeline.Validate` resolves every registered validator for the command's runtime type, runs in DI order, short-circuits on first reject. [`actions/01`](../Product/features/actions/models/01-command-pipeline.md), [`actions/03`](../Product/features/actions/models/03-command-handler.md), [`actions/06`](../Product/features/actions/models/06-command-validator.md).
8. **State surface + effect scope.** `IStateScope` (read; `Get<T>` + `Subscribe<T>(handler) → IDisposable` per spike Q1 lock). `IEffectScope` (`Dispatch<TResult>(GameCommand)` + `Execute(CompositeMutation)` — two distinct surfaces per ADR-0013). `Store` (read + `Execute(payload)` for mutator dispatch). [`effects/02-effect-scope.md`](../Product/features/effects/models/02-effect-scope.md). No `IHandlerContext` (retired per ADR-0010).
9. **MatchState + ITurnFlow + framework states.** `MatchState` abstract record with `Actions: IReadOnlyList<Type>`, `ActionsOpen: bool` (action-gate triad per spike Q1 lock), `OnEnter(scope) → IReadOnlyList<GameAction>`, `OnExit(scope) → IReadOnlyList<GameAction>`, `IsCompleted(action, result, scope) → bool`. `ITurnFlow.NextAfter(MatchState) → MatchState`. Framework-shipped: `EndOfMatchState(MatchOutcome)` (terminal; `ActionsOpen = false`); `TransitionStateCommand(MatchState)` + `TransitionStateHandler` (writes `MatchStateSlice.Current`, enqueues `newState.OnEnter` results). `MatchOutcome` hierarchy (`Win`, `Loss`, `Draw`, `Aborted`). [`engine/07-match-state.md`](../Product/features/engine/models/07-match-state.md), [`engine/08-turn-flow.md`](../Product/features/engine/models/08-turn-flow.md).
10. **Session lifecycle + lifecycle commands.** `SessionState` typed hierarchy (`NotStartedSession`, `InMatchSession`, `EndedSession(MatchOutcome)`) on `engine.Session` field. `StartMatchCommand`, `EndMatchCommand(MatchOutcome)`, `MatchStartedSignal`, `MatchEndedSignal` + framework `StartMatchHandler` (sets `engine.Session = InMatchSession`; emits `MatchStartedSignal`) / `EndMatchHandler` (sets `engine.Session = EndedSession(outcome)`; emits `MatchEndedSignal`). Per [ADR-0009 D12](../Product/decisions/0009-engine-as-fsm.md). [`engine/01 § Session lifecycle`](../Product/features/engine/models/01-game-engine.md).
11. **Trigger system (pass-through for Pile Counter, but wired).** `Trigger<T>` abstract class (`Validate` + `Execute`), `ITriggerSystem` (subscription), `ITriggerPipeline` (execution surface — engine invokes per ADR-0013). In-memory registry in `TriggerSystem`. Pile Counter has no triggers; the fold is a no-op pass-through but must compile and run. [`triggers/01-trigger.md`](../Product/features/triggers/models/01-trigger.md), [`triggers/02-trigger-pipeline.md`](../Product/features/triggers/models/02-trigger-pipeline.md).
12. **Engine (orchestrator).** `GameEngine.Dispatch(GameCommand, out string?) → bool` with try-get pattern. Drain loop: pattern-match command type → validator chain → trigger before-fold → handler → trigger after-fold → `runtime.Evaluate` (for `GameAction` only). Engine skips `Evaluate` after `TransitionStateCommand` (transition-loop prevention). Engine pattern-matches `MatchStateSlice.Current is EndOfMatchState` after transition handler completes and auto-dispatches `EndMatchCommand(outcome)`. Engine holds `ITriggerPipeline` reference. Per [ADR-0013](../Product/decisions/0013-engine-pipeline-orchestration.md) per-type routing table. [`engine/01`](../Product/features/engine/models/01-game-engine.md).
13. **Framework validators.** `SessionGate` (rejects when `engine.Session` doesn't admit the command), `MatchStateActionGate` (rejects when `ActionsOpen == false && command.GetType() ∉ MatchStateSlice.Current.Actions`). Both register first in the validator chain. Lifecycle commands (`StartMatchCommand` / `EndMatchCommand` / `TransitionStateCommand`) bypass `MatchStateActionGate`. [`actions/06`](../Product/features/actions/models/06-command-validator.md).
14. **Factory.** `IGameFactory.NewMatch().WithGame().WithActors().WithSeed().Build()` builder. Open-generic resolution for `GameRulesRuntime<TDefinition>`. `EngineInstaller` registers: framework validators (`SessionGate`, `MatchStateActionGate`); framework mutators (`ZoneContentsState`, `CardMapState`, `OwnershipState`, `CardVisibilityState`, `MatchStateSlice`, `RequestsSlice`, `AnswersSlice`, `PendingEffectsSlice`, `HistoryState`); framework handlers (`StartMatchHandler`, `EndMatchHandler`, `TransitionStateHandler`, `CreateCardHandler`); `ITriggerPipeline` impl; `IEffectPipeline` impl. [`engine/05`](../Product/features/engine/models/05-game-factory.md), [`engine/06`](../Product/features/engine/models/06-game-definition.md).

**Exit:** smoke test — register a dummy `NoOpAction` + validator + handler, dispatch through `GameEngine`, observe envelope in `HistoryState`. No game code yet.

### Phase 2 — Pile Counter game code (≈ 1d)

Mechanical translation of [pile-counter.md §9](../Product/support/samples/pile-counter.md). One file per concept; same names as the sketch.

- **Slices.** `PileState(Counter, Target)`, `LastMoveState(Move?)`, `LastMove`, `PileRefsState(IReadOnlyList<Ref<Zone>>)` (per D-PileRefs).
- **Variables.** `Vars.Number`.
- **MatchStates.** `PilePlayingState(IActor Player)` with `Actions = [typeof(MoveCardAction)]` + `ActionsOpen = false` + `OnEnter` returning the setup `CreateCardAction` list per `puzzle.Deal` + `IsCompleted` returning true when all pile counters equal targets. `EndOfMatchState` is framework-shipped (used directly).
- **Turn flow.** `PileCounterTurnFlow : ITurnFlow` — `PilePlayingState → EndOfMatchState(Win(Player))`.
- **Commands.** `MoveCardIntent : PlayerIntent`, `MoveCardAction : GameAction`, `MoveCardResult : CommandResult`.
- **Payloads.** `MoveCardPayload(Card, From, To, Number)` (single semantic payload per spike Q5 lock; mutators fan out).
- **Validator.** `MoveCardValidator : CommandValidator<MoveCardAction>` — four rules (source≠destination, source non-empty, top-only, not-same-as-last-move).
- **Handler.** `MoveCardHandler : GameCommandHandler<MoveCardAction, MoveCardResult>` — `Scope.Execute(MoveCardComposite.AtTop(...))` for the zone-move expansion + `Store.Execute(new MoveCardPayload(...))` for the fan-out write.
- **Mutators.** `AdjustSourceCounterMutator : Mutator<PileState, MoveCardPayload>`, `AdjustDestCounterMutator : Mutator<PileState, MoveCardPayload>`, `LastMoveMutator : Mutator<LastMoveState, MoveCardPayload>`. (Alternative: split `MoveCardPayload` into `RemoveFromPilePayload` + `AddToPilePayload` for cleaner keying — pick during implementation.)
- **ViewModel.** `PilePlayingViewModel : MatchStateViewModel<PilePlayingState>` — uses `scope.Subscribe<ZoneContentsState>` + `scope.Subscribe<LastMoveState>` to recompute the `Moves` observable; `Map(PlayerIntent) → GameCommand` translates `MoveCardIntent` to `MoveCardAction`. `CompositeDisposable` for subscription cleanup.
- **Rules.** `PileCounterRulesDefinition : GameRulesDefinition` (empty), `PileCounterRulesRuntime : GameRulesRuntime<PileCounterRulesDefinition>` — wires `Flow : PileCounterTurnFlow` + the solo player ref. No `Evaluate` override needed (default body suffices). No `FilterTriggers` (retired per ADR-0013). No `StateBasedActions` (retired per ADR-0009 refresh).
- **Definitions.** `PileCounterCardDefinition : CardDefinition`, `PileDefinition : ZoneDefinition`, `SoloPlayerDefinition : PlayerDefinition`, `SoloPlayer : Player`.
- **Bundle.** `PileCounterGameDefinition : GameDefinition` + `GetInitialSlices` yielding `LastMoveState(null)` and `PileRefsState(empty)`. Factory Build-walk populates `PileRefsState` after spawning the four piles.
- **Installer.** `PileCounterInstaller` registering validator, handler, mutators, runtime (open-generic), turn flow, view model (against `ViewModelRegistry`).
- **Bootstrap.** `PileCounterBootstrap.Create(puzzleFactory, gameFactory)`.

**Exit:** edit-mode test — construct a fixed `Puzzle`, build the engine, `engine.StartMatch()`, dispatch one `MoveCardAction`, assert `PileState`/`LastMoveState`/`ZoneContentsState` all updated atomically (one command-boundary notification per slice).

### Phase 3 — Puzzle factory (≈ 0.5d)

`IPuzzleFactory.Create() → Puzzle(Deck, Targets, Deal, Seed)`. Random generator + brute-force solver (search space ≤ a few hundred positions for 12 cards across 4 piles; trivially exhaustible). External to framework per sample §8.

**Exit:** factory produces a guaranteed-solvable puzzle; solver finds the winning sequence in unit test.

### Phase 4 — Debug runtime panel (≈ 0.5d)

One IMGUI script attached to the bootstrap GameObject. Minimal presenter that:
- Subscribes to `MatchStateSlice` to know whether the current state is `PilePlayingState` (active mode) or `EndOfMatchState` (terminal).
- When `PilePlayingState` is current, asks `ViewModelRegistry.Create(state)` for a `PilePlayingViewModel`; subscribes to `Moves`.
- When `EndOfMatchState` is current, renders the win banner + "new puzzle" button.

Panel widgets:

- **State readout** — per pile: top card `Number`, `Counter`, `Target`. Singleton: `LastMove`, `engine.Session`, current `MatchStateSlice.Current.GetType().Name`. Available moves count.
- **Move button matrix** — 4×4 grid of `Move A→B` buttons. Each button looks up the top card of source via the VM's `Moves` projection, builds a `MoveCardIntent`, calls `vm.Map(intent)` to get the `GameCommand`, dispatches via `GameEngine.Dispatch`. Greys out when the move isn't in the projection (twice-in-a-row suppression + empty piles).
- **Log panel** — append on every signal (`MatchStartedSignal`, `MatchEndedSignal`) and every dispatch rejection (the `reason` string from `engine.Dispatch`).
- **New puzzle button** — `engine.Dispose()`, ask factory for a new puzzle, bootstrap fresh. Per ADR-0009 refresh (rematch model: terminal `EndedSession`; dispose-and-recreate).

**Exit:** press Play, see piles populate via `PilePlayingState.OnEnter` setup, click moves, watch counters update, hit win → `EndOfMatchState` → log shows `MatchEndedSignal(Win)`.

### Phase 5 — Verification (≈ 0.5d)

- **Unit tests.** `MoveCardValidator` covers all four rejection paths + happy path. `AdjustSourceCounterMutator` + `AdjustDestCounterMutator` correctly apply deltas. `LastMoveMutator` overwrites singleton.
- **Integration test.** Pre-known solvable puzzle → scripted sequence of `MoveCardAction`s → assert `MatchEndedSignal(Win)` fires; assert all pile counters equal targets at end; assert `engine.Session` ends as `EndedSession(Win)`; assert `MatchStateSlice.Current is EndOfMatchState`.
- **Match-end auto-emit test.** Verify engine auto-dispatches `EndMatchCommand` on `EndOfMatchState` without host involvement (test harness doesn't call `engine.FinishMatch` manually).
- **Transition-loop prevention test.** Verify engine doesn't call `runtime.Evaluate` after `TransitionStateCommand` (instrument the runtime; assert call count on the transition is zero).
- **Manual exercise.** Play through three random puzzles in editor. Confirm: signals fire in order; VM observables update on every command boundary; no Unity console errors; no allocations in steady state (rough eyeball).
- **Drift audit.** Walk the slice against [pile-counter.md §9](../Product/support/samples/pile-counter.md). Any divergence — file as OPEN-ITEMS or patch the Model.

**Exit:** all tests green; manual playthrough completes; drift audit clean or items filed.

## Out of scope

Effects, triggers, abilities, async decisions (no `Request<TResponse>` traffic), multi-player, hidden info, drag-and-drop UI, polished visuals, save/load, replay, undo, restart-puzzle UX beyond a bootstrap "new puzzle" button. All revisit against richer sample games once the floor holds.

## Dependencies

- Scaffold packages (Phase 0).
- VContainer (already implicit in `IGameFactory` / installer pattern; pulled by Scaffold or directly).
- All referenced Models are `settled`; ADR-0009 / ADR-0010 / ADR-0011 / ADR-0013 cascades all landed.

## Open items this plan does not resolve

- [`bl-interaction-feature`](../Product/ledgers/OPEN-ITEMS.md) — gesture-to-intent feature. Slice ships debug-only (IMGUI matrix); the formal `IntentGateway` design surfaces with the next presentation pass.
- [`bl-authoring-feature`](../Product/ledgers/OPEN-ITEMS.md) — Pile Counter has no effects, so authoring vocabulary is untested by the slice.
- [`oq-history-envelope`](../Product/ledgers/OPEN-ITEMS.md) — slice will force a concrete `HistoryEnvelope` shape; resolution surfaces during Phase 1 step 12 (engine drain wiring).
- [`oq-validator-defaults`](../Product/ledgers/OPEN-ITEMS.md) — Pile Counter exercises both framework defaults (`SessionGate` + `MatchStateActionGate`); whether a third default joins them surfaces during the slice.
- [`oq-trigger-subscription-entry`](../Product/ledgers/OPEN-ITEMS.md) — Pile Counter has no triggers, so the trigger-subscription lifecycle (per-card register/unregister on zone entry) isn't exercised. Will surface with the first trigger-bearing sample.
