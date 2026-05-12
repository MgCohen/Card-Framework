---
type: plan
status: active
tags: [#plan, #pile-counter, #vertical-slice]
---

# Pile Counter — Vertical Slice Implementation Plan

The first end-to-end exercise of the settled framework. Implements [`Product/support/samples/pile-counter.md`](../Product/support/samples/pile-counter.md) §9 against current `Assets/` (empty). Tests every contract under real game pressure. Refreshed 2026-05-12 against the landed FSM-arch cascade (ADRs 0009–0012) + 2026-05-12 design-pass locks.

## Locked decisions

1. **D-Presentation** — debug-only. IMGUI runtime panel: button matrix for `Source → Destination` moves, text readouts for top card / counter / target per pile, log lines for signals + rejections, "New puzzle" button. No drag-and-drop, no UI Toolkit. Real-UI gesture system is `bl-interaction-feature` scope.

The pre-cascade `D-PileRefs` and `D-Cascade-order` locks retract — the 2026-05-12 design pass (Q2 = A) replaces `PileRefsState` with `scope.GetAll<TState>()`, and the cascade has landed so there is no "pre-cascade" vs "post-cascade" sequencing.

## Phases

### Phase 0 — Wiring (≈ 0.5d)

- Browse `C:\Unity\Scaffold\Assets\Packages\` and pull required `com.scaffold.*` packages into `Packages/manifest.json` (state, DI bindings, whatever Scaffold ships for `BaseState` / `Store` / `Mutator<TState, TPayload>`). Cross-check against `C:\Unity\Gear-Engine\Packages\manifest.json` for the canonical wiring per `DOCS/scaffold.md`.
- Asmdefs: `CardFramework.Runtime`, `CardFramework.Engine`, `CardFramework.PileCounter`, `CardFramework.PileCounter.Debug`.
- Empty Unity scene with a bootstrap `GameObject` hosting the VContainer scope.

**Exit:** project compiles; Scaffold types resolve; an empty scene loads.

### Phase 1 — Framework primitives (≈ 2.5d)

Build top-down, smallest first. Verify each layer compiles before the next.

1. **References.** `Reference`, `Ref<T>`, `CardRef` (`readonly record struct` with `NewRef()` factory). [`entities/02-instance.md`](../Product/features/entities/models/02-instance.md), [`cards/01-card-model.md`](../Product/features/cards/models/01-card-model.md).
2. **Command hierarchy.** `GameCommand` (abstract record) → `GameAction`, `CompositeAction`, `GameSignal`, `Request<TResponse>`, `Response` (five siblings per [ADR-0011](../Product/decisions/0011-effect-orchestration-dispatch-requests.md)). `CommandResult` base. [`actions/01-command-pipeline.md`](../Product/features/actions/models/01-command-pipeline.md).
3. **Entity bridge.** `IEntityDefinition`, `DefinitionWithSlices`, `InstanceWithSlices<TDef>` (`protected Definition`), `ISliceProvider`. `Variable`, `VariableValue`, `IntVariableValue`. [`entities/01-definition.md`](../Product/features/entities/models/01-definition.md), [`entities/02-instance.md`](../Product/features/entities/models/02-instance.md), [`entities/03-variable.md`](../Product/features/entities/models/03-variable.md).
4. **Framework slices.** `HistoryState`, `ZoneContentsState`, `CardMapState` (bidirectional `RefOf`/`InstanceOf`), `OwnershipState`, `CardVisibilityState`, plus the FSM/effect framework slices: `MatchStateSlice(MatchState Current)` per [`engine/07`](../Product/features/engine/models/07-match-state.md), `PendingEffectsSlice` per [`effects/05`](../Product/features/effects/models/05-pending-effects.md), `RequestsSlice` + `AnswersSlice` per [`effects/06`](../Product/features/effects/models/06-request-system.md). No `EngineState.Lifecycle`, no `AvailableActionsState` (retired by ADR-0009).
5. **Match-state machinery.** `MatchState` abstract record with `Actions: IReadOnlyList<Type>` + `EnterCommands: IReadOnlyList<GameAction>` + `ShouldExit(action, result, scope): bool` virtuals. `MatchStateSlice` wrapper. `TransitionStateCommand(MatchState NewState) : GameAction` + `TransitionResult : CommandResult` + framework-internal `TransitionStateHandler` (writes slice via `SetMatchStatePayload` then enqueues `EnterCommands`). `SetMatchStateMutator`. `ITurnFlow.Initial` + `NextAfter`. `ISBA.Check`. [`engine/07`](../Product/features/engine/models/07-match-state.md), [`engine/08`](../Product/features/engine/models/08-turn-flow.md), [`engine/09`](../Product/features/engine/models/09-state-based-actions.md).
6. **Composite mutations.** `MoveCardMutation : CompositeAction` with `.AtTop(card, source, dest)` factory + expansion to `ZoneContentsState` removes/inserts and `CardMapState` rotation. [`cards/01 § Mutations`](../Product/features/cards/models/01-card-model.md).
7. **Zone supports.** `ZoneVisibilityPolicy` (sealed-record hierarchy: `AllPublic`, `AllHidden`, `OwnerSees`, …), `InsertPosition` (sealed-record hierarchy: `AtTop`, `AtBottom`, …). [`zones/01-zone-model.md`](../Product/features/zones/models/01-zone-model.md).
8. **Pipeline + validator/handler split.** `IHandlerRegistry` (`Resolve(GameCommand) → ICommandHandler`), `ICommandPipeline` (`Execute`). `ICommandValidator<TCommand>` interface + `CommandValidator<TCommand>` abstract base (`[Inject] protected Store`). `GameCommandHandler<TCommand, TResult>` abstract base. `ValidatedCommandHandler<TCommand, TResult>` combined base for the common case. [`actions/01`](../Product/features/actions/models/01-command-pipeline.md), [`actions/03`](../Product/features/actions/models/03-command-handler.md), [`actions/06`](../Product/features/actions/models/06-command-validator.md).
9. **Framework-default validators.** `SessionGate` (reads `engine.Session`) + `MatchStateActionGate` (reads `MatchStateSlice.Current.Actions`). Register first in DI order so they short-circuit before any game validators. Lifecycle commands + `TransitionStateCommand` bypass `MatchStateActionGate`. [`actions/06`](../Product/features/actions/models/06-command-validator.md), [ADR-0010](../Product/decisions/0010-validator-handler-split.md).
10. **State surface.** `IStateScope` (read; plus `GetAll<TState>()` enumeration). Handler/validator abstract bases inject `Store` directly via `[Inject]` — no `IHandlerContext`. [`effects/02-effect-scope.md`](../Product/features/effects/models/02-effect-scope.md).
11. **Session lifecycle.** Sealed `SessionState` hierarchy (`NotStartedSession` / `InMatchSession` / `EndedSession(MatchOutcome)`). `StartMatchCommand`, `EndMatchCommand(MatchOutcome)`, `MatchStartedSignal`, `MatchEndedSignal` + framework `StartMatchHandler` / `EndMatchHandler` (flips `engine.Session` field; emits signal via `ctx.Emit`). [`engine/01 § Session lifecycle`](../Product/features/engine/models/01-game-engine.md), [ADR-0009](../Product/decisions/0009-engine-as-fsm.md).
12. **Engine.** `GameEngine.Dispatch(GameAction, out string?) → bool` delegates entirely to the validator chain (short-circuit on first reject). Drain loop: Phase 1 GameAction queue (per-step `pipeline.Execute` → `runtime.Evaluate` → type-change check → optional `TransitionStateCommand` enqueue); Phase 2 `PendingEffectsSlice` drain when drain-policy allows. `engine.StartMatch(out reason)` / `engine.FinishMatch(outcome, out reason)` / `engine.Dispose()` wrap the lifecycle commands. `engine.Session : SessionState` field + `SessionChanged` event. [`engine/01`](../Product/features/engine/models/01-game-engine.md).
13. **Factory.** `IGameFactory.NewMatch().WithGame().WithActors().WithSeed().Build()` builder. Open-generic resolution for `GameRulesRuntime<TDefinition>`. `EngineInstaller` registers `IStateScope`, `ICommandPipeline`, `Store`, `IGameFactory`, framework-default validators (`SessionGate`, `MatchStateActionGate`), framework-internal handlers (`StartMatchHandler`, `EndMatchHandler`, `TransitionStateHandler`), and framework mutators (`ZoneContentsState`, `CardMapState`, `OwnershipState`, `CardVisibilityState`, `SetMatchStateMutator`, `HistoryState`). Seeds `MatchStateSlice` from `ITurnFlow.Initial` at `Build()`. [`engine/05`](../Product/features/engine/models/05-game-factory.md), [`engine/06`](../Product/features/engine/models/06-game-definition.md).
14. **Presentation primitives.** `PlayerIntent` abstract base (lives in presentation feature-root vocab per ADR-0012). `MatchStateViewModel<TState>` abstract class with `Bind(state, scope)` + `Map(intent) → GameCommand`. `ViewModelRegistry` sealed lookup + `ViewModelRegistryBuilder` (`Register<TState, TViewModel>()` + many-to-one overload). [`presentation/01`](../Product/features/presentation/models/01-match-state-view-model.md), [`presentation/02`](../Product/features/presentation/models/02-view-model-registry.md).

**Exit:** smoke test — register a dummy `NoOpCommand` + `ValidatedCommandHandler<NoOpCommand, NoOpResult>`, dispatch through `GameEngine` while in a minimal `IdleMatchState` (with `Actions = [typeof(NoOpCommand)]`), observe envelope in `HistoryState`, observe no rejection from `SessionGate` or `MatchStateActionGate`. Verify `engine.StartMatch()` flips `engine.Session` to `InMatchSession`; verify `engine.FinishMatch(outcome)` flips to `EndedSession`. No game code yet.

### Phase 2 — Pile Counter game code (≈ 1.5d)

Mechanical translation of [pile-counter.md §9](../Product/support/samples/pile-counter.md). One file per concept; same names as the sketch.

- **Slices.** `PileState(int Counter, int Target)`, `LastMoveState(LastMove? Move)`, `LastMove(Card, From, To)`. No `PileRefsState` — enumeration via `scope.GetAll<TState>()` per design-pass Q2 = A.
- **Variables.** `Vars.Number`.
- **Match states.** `PilePlayingState : MatchState` with `Actions => [typeof(MoveCardCommand)]`. `PileGameOverState(MatchOutcome Outcome) : MatchState` with `Actions => []` and `EnterCommands => [new EndMatchCommand(Outcome)]` (per design-pass Q1 = E). [`engine/07`](../Product/features/engine/models/07-match-state.md), [pile-counter.md §9.3](../Product/support/samples/pile-counter.md).
- **Turn flow + SBA.** `PileCounterTurnFlow : ITurnFlow` (`Initial => new PilePlayingState()`; `NextAfter` throws — SBA-driven). `PileCounterWinSBA : ISBA` (reads `scope.GetAll<PileState>()`; returns `PileGameOverState(MatchOutcome.Win(player))` when all match). [pile-counter.md §9.4](../Product/support/samples/pile-counter.md).
- **Intent.** `DropCardIntent(Ref<Card> Card, Ref<Zone> From, Ref<Zone> To) : PlayerIntent` — carries all three at gesture-start per design-pass Q3 = A.
- **Commands.** `MoveCardCommand(Card, From, To) : GameAction`, `MoveCardResult(MovedCard) : CommandResult`.
- **Payloads.** `AdjustPileCounterPayload(Ref<Zone>, int) : IPayloadReference`, `SetLastMovePayload(LastMove)`.
- **Validator + handler.** `MoveCardHandler : ValidatedCommandHandler<MoveCardCommand, MoveCardResult>` per ADR-0010 — combined base, four `Validate` rules (source≠destination, source non-empty, top-only, not-same-as-last-move); `Handle` issues one `Store.ExecuteBatch` of `[MoveCardMutation.AtTop, AdjustPileCounterPayload×2, SetLastMovePayload]`. `Store` from base via `[Inject]`; no `IHandlerContext` parameter.
- **Mutators.** `PileCounterMutator : Mutator<PileState, AdjustPileCounterPayload>`, `LastMoveMutator : Mutator<LastMoveState, SetLastMovePayload>`.
- **Per-state ViewModels.** `PilePlayingViewModel : MatchStateViewModel<PilePlayingState>` — `Bind` computes per-card destinations from `scope.GetAll<ZoneContentsState>()` + `LastMoveState` into an `ObservableProperty<IReadOnlyDictionary<Ref<Card>, IReadOnlyList<Ref<Zone>>>>`; `Map(DropCardIntent d) => new MoveCardCommand(d.Card, d.From, d.To)`. `PileGameOverViewModel : MatchStateViewModel<PileGameOverState>` — `Bind` reads `state.Outcome` into an observable; `Map` throws. [pile-counter.md §9.9](../Product/support/samples/pile-counter.md), [`presentation/01`](../Product/features/presentation/models/01-match-state-view-model.md).
- **Rules.** `PileCounterRulesDefinition : GameRulesDefinition` (empty data class for now). `PileCounterRulesRuntime : GameRulesRuntime<PileCounterRulesDefinition>` — `protected override ITurnFlow Flow { get; }` + `protected override IReadOnlyList<ISBA> StateBasedActions { get; }` wired from the constructor; **no `Evaluate` override** (default body covers SBAs → request pause → pending-effect pause → `ShouldExit` + `Flow.NextAfter`); no `BuildSnapshot`, no first-`Evaluate` setup hook. [pile-counter.md §9.8](../Product/support/samples/pile-counter.md), [`engine/03`](../Product/features/engine/models/03-game-rules-runtime.md).
- **Definitions.** `PileCounterCardDefinition : CardDefinition` (`DefinedVariables`, `TryGetDefaultValue`), `PileDefinition : ZoneDefinition` (`DefaultPosition = AtTop`, `Visibility = AllPublic`, `GetInitialSlices yield PileState`), `SoloPlayerDefinition : PlayerDefinition` (empty `PerPlayerZones`), `SoloPlayer : Player`.
- **Bundle.** `PileCounterGameDefinition : GameDefinition` with `GetInitialSlices` yielding `LastMoveState` only (no `PileRefsState`; framework slices auto-seeded). `Puzzle` record + `IPuzzleFactory` (built in Phase 3). The factory walks `puzzle.Deal` and spawns each card directly during `Build()` — no runtime-driven setup commands. `PileCounterBootstrap.Create(puzzleFactory, gameFactory)` returns the engine post-`StartMatch`.
- **Installer.** `PileCounterInstaller` registers: `MoveCardHandler` (combined validator+handler, `AsImplementedInterfaces`); game-authored mutators (`PileCounterMutator`, `LastMoveMutator`); runtime via open-generic (`GameRulesRuntime<PileCounterRulesDefinition> → PileCounterRulesRuntime`); `PileCounterTurnFlow` + `PileCounterWinSBA`; per-state VMs (`PilePlayingViewModel`, `PileGameOverViewModel`); `ViewModelRegistry` built via `ViewModelRegistryBuilder` registering `PilePlayingState → PilePlayingViewModel` + `PileGameOverState → PileGameOverViewModel`.

**Exit:** edit-mode test — construct a fixed `Puzzle`, build the engine, `engine.StartMatch()`, dispatch one `MoveCardCommand`, assert `PileState`/`LastMoveState`/`ZoneContentsState` all updated atomically. Force a known-winning sequence; assert `MatchStateSlice.Current is PileGameOverState`, then assert `engine.Session is EndedSession`, then assert subsequent `Dispatch` rejects with `SessionGate` reason.

### Phase 3 — Puzzle factory (≈ 0.5d)

`IPuzzleFactory.Create() → Puzzle(Deck, Targets, Deal, Seed)`. Random generator + brute-force solver (search space ≤ a few hundred positions for 12 cards across 4 piles; trivially exhaustible). External to framework per sample §8.

**Exit:** factory produces a guaranteed-solvable puzzle; solver finds the winning sequence in unit test.

### Phase 4 — Debug runtime panel (≈ 0.5d)

One IMGUI script attached to the bootstrap GameObject. Subscribes to `MatchStateSlice`; binds the active VM through `ViewModelRegistry`. No interaction widgets beyond:

- **State readout** — per pile: top card `Number`, `Counter`, `Target`. Singleton: `LastMoveState.Move`, `engine.Session` (via `SessionChanged` event), `MatchStateSlice.Current.GetType().Name`, `MatchStateSlice.Current.Actions` count.
- **Move button matrix** — 4×4 grid of `Move A→B` buttons. Each button position encodes `(source, destination)`. On click: read source's top card from `Store.Get<ZoneContentsState>(source).Cards[^1]`; resolve to `Ref<Card>` via `CardMapState`; construct `new DropCardIntent(card, source, destination)`; pass to the active VM's `Map(intent)`; dispatch the returned `GameCommand` via `engine.Dispatch`. Greys out when the resulting command would fail validation (probe via a dry-run validator query, or just dispatch and surface the `reason` if rejected).
- **Log panel** — append on every signal (`MatchStartedSignal`, `MatchEndedSignal`) and every dispatch rejection (the `reason` string from `engine.Dispatch`'s `out` parameter).
- **New puzzle button** — `engine.Dispose()` (synthesizes `MatchOutcome.Aborted` if not already ended), ask factory for a new puzzle, bootstrap fresh.

**Exit:** press Play, see piles populate, click moves, watch counters update, hit win state (panel reads `PileGameOverState` and `EndedSession`).

### Phase 5 — Verification (≈ 0.5d)

- **Unit tests.** `MoveCardHandler.Validate` covers all four rejection paths + happy path. `PileCounterMutator` correctly applies delta. `LastMoveMutator` overwrites singleton. `PileCounterWinSBA.Check` returns null mid-game and `PileGameOverState(Win)` when all piles match. `PileCounterTurnFlow.NextAfter` throws (never called).
- **Integration test.** Pre-known solvable puzzle → scripted sequence of `MoveCardCommand`s → assert `MatchEndedSignal(Win)` fires; assert all pile counters equal targets; assert `MatchStateSlice.Current is PileGameOverState`; assert `engine.Session is EndedSession(MatchOutcome.Win(player))`; assert post-end `Dispatch` rejects with `SessionGate` reason.
- **Validator-chain test.** Dispatch from `NotStartedSession` → assert `SessionGate` reject. Dispatch a `StartMatchCommand` clone twice → assert second rejects. Dispatch a non-`MoveCardCommand` while in `PilePlayingState` → assert `MatchStateActionGate` reject. Dispatch a `MoveCardCommand` that fails one of the four game rules → assert `MoveCardHandler.Validate` reject reason surfaces.
- **VM-rebind test.** Subscribe a stand-in presenter to `MatchStateSlice`; assert `ViewModelRegistry.Create` returns `PilePlayingViewModel` on `InMatch` seed; force win; assert presenter rebinds to `PileGameOverViewModel` exactly once; assert same-type field updates (none expected for Pile Counter, but verify no spurious rebinds).
- **Manual exercise.** Play through three random puzzles in editor. Confirm: signals fire in order; VM observable updates after every move; no Unity console errors; no allocations in steady state (rough eyeball).
- **Drift audit.** Walk the slice against [pile-counter.md §9](../Product/support/samples/pile-counter.md). Any divergence — file as OPEN-ITEMS or patch the Model.

**Exit:** all tests green; manual playthrough completes; drift audit clean or items filed.

## Out of scope

Effects, triggers, abilities, async decisions, multi-player, hidden info, drag-and-drop UI, polished visuals, save/load, replay, undo, restart-puzzle UX beyond a bootstrap button. All revisit against richer sample games once the floor holds.

The post-cascade framework slices for effects/requests (`PendingEffectsSlice`, `RequestsSlice`, `AnswersSlice`) get registered + auto-seeded in Phase 1 step 4 but are exercised only as no-ops — Pile Counter has no effects to drain and no async decisions to open. Real effect-pipeline coverage waits for a sample game that uses it (likely the second sample).

## Dependencies

- Scaffold packages (Phase 0).
- VContainer (already implicit in `IGameFactory` / installer pattern; pulled by Scaffold or directly).
- All referenced Models settled — engine 01–09, actions 01/03/04/06, effects 01/02/05/06, presentation 01/02, plus cards 01, zones 01, entities 01–05, players 01, state. ADRs 0006/0007/0009/0010/0011/0012 all accepted.

## Open items this plan does not resolve

- [`bl-interaction-feature`](../Product/ledgers/OPEN-ITEMS.md#bl-interaction-feature) — gesture-to-intent feature (raw input → typed `PlayerIntent`). Slice ships debug-only (button matrix produces `DropCardIntent` directly), so the feature design defers.
- [`bl-authoring-feature`](../Product/ledgers/OPEN-ITEMS.md#bl-authoring-feature) — Pile Counter has no effects, so authoring vocabulary (`IEffect.Run` recipe shape) is untested by the slice.
- [`oq-history-envelope`](../Product/ledgers/OPEN-ITEMS.md#oq-history-envelope) — slice will force a concrete `HistoryEnvelope` shape; resolution surfaces during Phase 1 step 12 (engine drain wiring).
- [`oq-validator-defaults`](../Product/ledgers/OPEN-ITEMS.md#oq-validator-defaults) — registration-order policy + possible third framework-default validator. Slice ships with the convention "framework defaults register first"; whether that's enforced (attribute / DI extension) vs. documented stays open.
- [`oq-view-model-host-shape`](../Product/ledgers/OPEN-ITEMS.md#oq-view-model-host-shape) — the abstract `ViewModel` base class shape (animation hooks, observable wiring, disposal). Slice uses a minimal stand-in; concrete shape pins when the host MVVM toolkit is chosen.
