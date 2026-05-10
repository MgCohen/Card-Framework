---
type: plan
status: active
tags: [#plan, #pile-counter, #vertical-slice]
---

# Pile Counter — Vertical Slice Implementation Plan

The first end-to-end exercise of the settled framework. Implements [`Product/support/samples/pile-counter.md`](../Product/support/samples/pile-counter.md) §9 against current `Assets/` (empty). Tests every contract under real game pressure before the FSM-architecture ADR-0009 cascade lands.

## Locked decisions

1. **D-PileRefs** — game-side singleton slice (`PileRefsState : BaseState` holding `IReadOnlyList<Ref<Zone>>`), declared by `PileCounterGameDefinition.GetInitialSlices()`. Seeded by `PileCounterRulesRuntime` on first `Evaluate` after the runtime-driven setup commands spawn the piles. Resolves [`bl-pile-refs-index`](../Product/ledgers/OPEN-ITEMS.md#bl-pile-refs-index) as game-side; framework-generic shape deferred until a second consumer surfaces.
2. **D-Cascade-order** — build pre-cascade. ADR-0009 (FSM-arch promotion) runs as a separate thread; slice migration to post-cascade surfaces (`Evaluate → MatchState`, `SessionState`, `MatchState.Actions`, per-VM intent mapping) is **Phase 6**.
3. **D-Presentation** — debug-only. IMGUI runtime panel: button matrix for `Source → Destination` moves, text readouts for top card / counter / target per pile, log lines for signals + rejections, "New puzzle" button. No drag-and-drop, no UI Toolkit.

## Phases

### Phase 0 — Wiring (≈ 0.5d)

- Browse `C:\Unity\Scaffold\Assets\Packages\` and pull required `com.scaffold.*` packages into `Packages/manifest.json` (state, DI bindings, whatever Scaffold ships for `BaseState` / `Store` / `Mutator<TState, TPayload>`). Cross-check against `C:\Unity\Gear-Engine\Packages\manifest.json` for the canonical wiring per `DOCS/scaffold.md`.
- Asmdefs: `CardFramework.Runtime`, `CardFramework.Engine`, `CardFramework.PileCounter`, `CardFramework.PileCounter.Debug`.
- Empty Unity scene with a bootstrap `GameObject` hosting the VContainer scope.

**Exit:** project compiles; Scaffold types resolve; an empty scene loads.

### Phase 1 — Framework primitives (≈ 2d)

Build top-down, smallest first. Verify each layer compiles before the next.

1. **References.** `Reference`, `Ref<T>`, `CardRef` (`readonly record struct` with `NewRef()` factory). [`entities/02-instance.md`](../Product/features/entities/models/02-instance.md), [`cards/01-card-model.md`](../Product/features/cards/models/01-card-model.md).
2. **Command hierarchy.** `GameCommand` (abstract record) → `GameAction`, `CompositeAction`, `GameSignal`. `CommandResult` base. [`actions/01-command-pipeline.md`](../Product/features/actions/models/01-command-pipeline.md).
3. **Entity bridge.** `IEntityDefinition`, `DefinitionWithSlices`, `InstanceWithSlices<TDef>` (`protected Definition`), `ISliceProvider`. `Variable`, `VariableValue`, `IntVariableValue`. [`entities/01-definition.md`](../Product/features/entities/models/01-definition.md), [`entities/02-instance.md`](../Product/features/entities/models/02-instance.md), [`entities/03-variable.md`](../Product/features/entities/models/03-variable.md).
4. **Framework slices.** `EngineState` (with `EngineLifecycle` enum — pre-cascade), `HistoryState`, `ZoneContentsState`, `CardMapState` (bidirectional `RefOf`/`InstanceOf`), `OwnershipState`, `CardVisibilityState`, `AvailableActionsState`. [`engine/01`](../Product/features/engine/models/01-game-engine.md), [`cards/01`](../Product/features/cards/models/01-card-model.md), [`zones/01`](../Product/features/zones/models/01-zone-model.md), [`actions/04`](../Product/features/actions/models/04-history-envelope.md), [`actions/05`](../Product/features/actions/models/05-available-actions-snapshot.md).
5. **Composite mutations.** `MoveCardMutation : CompositeAction` with `.AtTop(card, source, dest)` factory + expansion to `ZoneContentsState` removes/inserts and `CardMapState` rotation. `CreateCardMutation` for setup. [`cards/01 § Mutations`](../Product/features/cards/models/01-card-model.md).
6. **Zone supports.** `ZoneVisibilityPolicy` (sealed-record hierarchy: `AllPublic`, `AllHidden`, `OwnerSees`, …), `InsertPosition` (sealed-record hierarchy: `AtTop`, `AtBottom`, …). [`zones/01-zone-model.md`](../Product/features/zones/models/01-zone-model.md).
7. **Pipeline.** `IHandlerRegistry` (`Resolve(GameCommand) → ICommandHandler`), `ICommandPipeline` (`Validate` / `Execute`), `CommandHandler<TCommand, TResult>` abstract base. [`actions/01`](../Product/features/actions/models/01-command-pipeline.md), [`actions/03`](../Product/features/actions/models/03-command-handler.md).
8. **State surface.** `IStateScope` (read), `IHandlerContext` (`Store` + `IAvailableActions` + `IDecisionResolver`). [`effects/02-effect-scope.md`](../Product/features/effects/models/02-effect-scope.md), [`effects/03-handler-context.md`](../Product/features/effects/models/03-handler-context.md). Stub `IDecisionResolver` — Pile Counter never calls it.
9. **Lifecycle commands.** `StartMatchCommand`, `EndMatchCommand`, `MatchStartedSignal`, `MatchEndedSignal` + framework `StartMatchHandler` / `EndMatchHandler`. [`engine/01 § Transitions`](../Product/features/engine/models/01-game-engine.md).
10. **Engine.** `GameEngine.Dispatch(GameAction, out string?) → bool` with try-get pattern: lifecycle gate → `pipeline.Validate` → enqueue → drain. [`engine/01`](../Product/features/engine/models/01-game-engine.md).
11. **Factory.** `IGameFactory.NewMatch().WithGame().WithActors().WithSeed().Build()` builder. Open-generic resolution for `GameRulesRuntime<TDefinition>`. `EngineInstaller` registering framework mutators (`ZoneContentsState`, `CardMapState`, `OwnershipState`, `CardVisibilityState`, `AvailableActionsState`, `EngineState`, `HistoryState`). [`engine/05`](../Product/features/engine/models/05-game-factory.md), [`engine/06`](../Product/features/engine/models/06-game-definition.md).

**Exit:** smoke test — register a dummy `NoOpCommand` + handler, dispatch through `GameEngine`, observe envelope in `HistoryState`. No game code yet.

### Phase 2 — Pile Counter game code (≈ 1d)

Mechanical translation of [pile-counter.md §9](../Product/support/samples/pile-counter.md). One file per concept; same names as the sketch.

- **Slices.** `PileState(Counter, Target)`, `LastMoveState(Move?)`, `LastMove`, `PileRefsState(IReadOnlyList<Ref<Zone>>)` (per D-PileRefs).
- **Variables.** `Vars.Number`.
- **Commands.** `MoveCardIntent : PlayerIntent`, `MoveCardCommand : GameAction`, `MoveCardResult : CommandResult`.
- **Payloads.** `AdjustPileCounterPayload(Ref<Zone>, int) : IPayloadReference`, `SetLastMovePayload(LastMove)`.
- **Handler.** `MoveCardHandler : CommandHandler<MoveCardCommand, MoveCardResult>` — four `Validate` rules (source≠destination, source non-empty, top-only, not-same-as-last-move); `Handle` issues one `ctx.Store.ExecuteBatch` of `[MoveCardMutation.AtTop, AdjustPileCounterPayload×2, SetLastMovePayload]`.
- **Mutators.** `PileCounterMutator : Mutator<PileState, AdjustPileCounterPayload>`, `LastMoveMutator : Mutator<LastMoveState, SetLastMovePayload>`.
- **Action template.** `MoveCardTemplate(Ref<Card>, IReadOnlyList<Reference>) : ActionTemplate(typeof(MoveCardCommand), Card)`.
- **Intent mapper.** `PileCounterIntentMapper : IIntentMapper` with `LocateCard` reverse-scan over `PileRefsState`.
- **Rules.** `PileCounterRulesDefinition : GameRulesDefinition`, `PileCounterRulesRuntime : GameRulesRuntime<PileCounterRulesDefinition>` — first-`Evaluate` seeds `PileRefsState` + dispatches `CreateCardMutation` per `puzzle.Deal`; subsequent `Evaluate` runs win check then `Actions.Set(BuildSnapshot())`.
- **Definitions.** `PileCounterCardDefinition : CardDefinition` (`DefinedVariables`, `TryGetDefaultValue`), `PileDefinition : ZoneDefinition` (`DefaultPosition = AtTop`, `Visibility = AllPublic`, `GetInitialSlices yield PileState`), `SoloPlayerDefinition : PlayerDefinition` (empty `PerPlayerZones`), `SoloPlayer : Player`.
- **Bundle.** `PileCounterGameDefinition : GameDefinition` + `GetInitialSlices` yielding `LastMoveState` and `PileRefsState`. `PileCounterInstaller` registering handler, mutators, runtime (open-generic), intent mapper. `PileCounterBootstrap.Create(puzzleFactory, gameFactory)`.

**Exit:** edit-mode test — construct a fixed `Puzzle`, build the engine, dispatch one `MoveCardCommand`, assert `PileState`/`LastMoveState`/`ZoneContentsState` all updated atomically.

### Phase 3 — Puzzle factory (≈ 0.5d)

`IPuzzleFactory.Create() → Puzzle(Deck, Targets, Deal, Seed)`. Random generator + brute-force solver (search space ≤ a few hundred positions for 12 cards across 4 piles; trivially exhaustible). External to framework per sample §8.

**Exit:** factory produces a guaranteed-solvable puzzle; solver finds the winning sequence in unit test.

### Phase 4 — Debug runtime panel (≈ 0.5d)

One IMGUI script attached to the bootstrap GameObject. No interaction widgets beyond:

- **State readout** — per pile: top card `Number`, `Counter`, `Target`. Singleton: `LastMove`, `EngineLifecycle`. Available templates count.
- **Move button matrix** — 4×4 grid of `Move A→B` buttons. Each button looks up the top card of source, builds a `MoveCardIntent`, runs it through `PileCounterIntentMapper`, dispatches via `GameEngine.Dispatch`. Greys out when the resulting command would fail validation.
- **Log panel** — append on every signal (`MatchStartedSignal`, `MatchEndedSignal`) and every dispatch rejection (the `reason` string).
- **New puzzle button** — tear down engine, ask factory for a new puzzle, bootstrap fresh.

**Exit:** press Play, see piles populate, click moves, watch counters update, hit win state.

### Phase 5 — Verification (≈ 0.5d)

- **Unit tests.** `MoveCardHandler.Validate` covers all four rejection paths + happy path. `PileCounterMutator` correctly applies delta. `LastMoveMutator` overwrites singleton.
- **Integration test.** Pre-known solvable puzzle → scripted sequence of `MoveCardCommand`s → assert `MatchEndedSignal(Win)` fires; assert all pile counters equal targets at end.
- **Manual exercise.** Play through three random puzzles in editor. Confirm: signals fire in order; snapshot publishes after every move; no Unity console errors; no allocations in steady state (rough eyeball).
- **Drift audit.** Walk the slice against [pile-counter.md §9](../Product/support/samples/pile-counter.md). Any divergence — file as OPEN-ITEMS or patch the Model.

**Exit:** all tests green; manual playthrough completes; drift audit clean or items filed.

### Phase 6 — Post-slice cascade (deferred)

Sequenced after Phase 5, separate branch:

- Promote [`spikes/fsm-architecture.md`](../Product/spikes/fsm-architecture.md) to `Product/decisions/0009-engine-as-fsm-and-dispatch-unification.md` per the spike's "When this lands" section.
- Patch engine Models (01, 03, 04) for D1 (`Evaluate → MatchState`), D2/D9 (`MatchState.Actions: IReadOnlyList<Type>`), D12 (`SessionState`), D13 (engine-only effect firing).
- Migrate slice runtime: `PileCounterRulesRuntime.Evaluate` returns `MatchState`; `EngineLifecycle` becomes `engine.Session`; intake gate reads `MatchState.Actions` instead of `pipeline.Validate` for available-actions filtering; `PileCounterIntentMapper` retires in favor of per-VM mapping (D8).
- Flip sample status §7 narrative from "pre-cascade" to "implemented."

## Out of scope

Effects, triggers, abilities, async decisions, multi-player, hidden info, drag-and-drop UI, polished visuals, save/load, replay, undo, restart-puzzle UX beyond a bootstrap button. All revisit against richer sample games once the floor holds.

## Dependencies

- Scaffold packages (Phase 0).
- VContainer (already implicit in `IGameFactory` / installer pattern; pulled by Scaffold or directly).
- All 22 referenced Models are `settled`; no design gates pending.

## Open items this plan does not resolve

- [`bl-interaction-feature`](../Product/ledgers/OPEN-ITEMS.md#bl-interaction-feature) — gesture-to-intent feature. Slice ships debug-only, so the feature design defers.
- [`bl-authoring-feature`](../Product/ledgers/OPEN-ITEMS.md#bl-authoring-feature) — Pile Counter has no effects, so authoring vocabulary is untested by the slice.
- [`oq-history-envelope`](../Product/ledgers/OPEN-ITEMS.md#oq-history-envelope) — slice will force a concrete `HistoryEnvelope` shape; resolution surfaces during Phase 1 step 10.
- [`oq-validator-defaults`](../Product/ledgers/OPEN-ITEMS.md#oq-validator-defaults) — slice's `pipeline.Validate` uses handler-only validation; framework-default validators (`MatchStateActionGate`, `SessionGate`) defer until ADR-0009.
