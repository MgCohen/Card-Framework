---
type: feature
status: settled
references: [features/actions/models/01-command-pipeline.md, features/triggers/models/01-trigger.md, features/effects/models/01-effect.md, features/entities/models/02-instance.md, decisions/0009-engine-as-fsm.md]
tags: [#engine]
---

# engine — feature

## What it is

The framework's orchestrator. The engine owns the **`GameEngine` class** (drain a strict-FIFO queue, execute each `GameAction` through the command pipeline, ask the runtime for the next `MatchState`, single `Dispatch` entry point), pins the **rules-layer contract** (`GameRulesDefinition` + `GameRulesRuntime<TDefinition>` — pure data + stateful brain, paired via DI container open-generic resolution), holds the **FSM machinery** (`MatchState` + `MatchStateSlice` polymorphic wrapper, `ITurnFlow` for successor knowledge, `ISBA` for cross-cutting state-driven overrides, `TransitionStateCommand` for transitions through the normal pipeline), layers the **framework slice tier** (framework-owned auto-seeded; game-authored declared via `ISliceProvider`) over the underlying state primitive ([`state`](features/state/state.md)), constructs matches via the **`IGameFactory` two-layer split** (DI installation vs per-match `MatchSetup`), and owns the **session lifecycle** (typed sealed `SessionState` hierarchy on `engine.Session` field — `NotStartedSession` / `InMatchSession` / `EndedSession` — plus framework-required `StartMatchCommand` / `EndMatchCommand`). The **`GameDefinition`** bundle ties everything together — `Rules` + `SharedZones` + `CardCatalog` + `DefaultPlayerDefinition` + `GetInitialSlices()` — and the factory walks it at `Build`. Per [ADR-0009](Product/decisions/0009-engine-as-fsm.md), the engine IS the FSM — `runtime.Evaluate` returns a `MatchState` and the engine dispatches `TransitionStateCommand` on type changes.

```
                   GameDefinition (bundle)
                   ├── Rules: GameRulesDefinition
                   ├── SharedZones: IEnumerable<ZoneDefinition>
                   ├── CardCatalog: IReadOnlyList<CardDefinition>
                   ├── DefaultPlayerDefinition: PlayerDefinition
                   └── GetInitialSlices() (game-singletons)
                              │
                              ▼
                   IGameFactory.Build(MatchSetup)
                   ├── Auto-seed framework slices (MatchStateSlice, EngineState, HistoryState)
                   │   (MatchStateSlice seeded from ITurnFlow.Initial)
                   ├── Spawn shared zones → players → per-player zones (overlay)
                   ├── Install per-spawn slices (ISliceProvider, keyed at SelfRef)
                   ├── Install game-singleton slices
                   └── Resolve GameRulesRuntime<TDefinition> via container open-generic
                              │
                              ▼
                   GameEngine (Session = NotStartedSession)
                   engine.StartMatch() → dispatch StartMatchCommand
                              │
                              ▼
       ┌──── Intake gates (framework) ───────────────────────────────┐
       │  Layer 1: Session gate (NotStarted/InMatch/Ended)            │
       │  Layer 2: MatchState.Current.Actions (valid command types)   │
       └──────────────────────────────────────────────────────────────┘
                              │
   engine.Dispatch(action) ──▶ gates ──▶ pipeline.Validate ──▶ pending.Enqueue
                                                            │
                                                            ▼
                                                       Drain loop:
                                                       while (queue):
                                                         pipeline.Execute(action)
                                                           ├── before-fold (FilterTriggers + ITriggerPipeline)
                                                           ├── handler.Handle (ctx.Store.Execute payloads)
                                                           ├── after-fold
                                                           └── envelope published
                                                         runtime.Evaluate(action, result) → MatchState
                                                           └── type-change? → enqueue TransitionStateCommand
                                                                              (handler writes MatchStateSlice + enqueues EnterCommands)
```

## Vocabulary

**Feature-owned terms only.** Cross-feature terms used by this feature — `Store`, `BaseState`, `IStateScope`, `Mutator<TState, TPayload>`, `StateChangeEvent` (defined in `state/`); `GameAction`, `GameSignal`, `CompositeAction`, `GameCommand`, `Trigger<T>`, `OnBefore`/`OnAfter`, `TriggerMatch`, `TriggerPhase`, `ITriggerPipeline` (defined in `triggers/`); `IEffectScope`, `Effect`, `ICommandPipeline`, `ICommandHandler`, `ICommandValidator`, `GameCommandHandler<,>`, `CommandValidator<>`, `SessionGate`, `MatchStateActionGate`, `PlayerIntent`, `HistoryEnvelope`, `CommandResult` (defined in `effects/` / `actions/`); `Reference`, `Ref<T>`, `IActor`, `ActorId`, `Card`, `Zone`, `Player`, `CardDefinition`, `ZoneDefinition`, `PlayerDefinition`, `InstanceWithSlices<TDef>`, `DefinitionWithSlices`, `IModifierFactory`, `ZoneContentsState`, `CardMapState`, `OwnershipState`, `EntityState` — live in their owning features or `Product/ledgers/GLOSSARY.md`; link to their canonical home, don't restate. Retired terms (`IHandlerContext`, `IAvailableActions`, `EngineLifecycle`) live in `GLOSSARY.md` §Retired terms.

| Term | Meaning |
|---|---|
| `GameEngine` | The orchestrator. Holds the framework-private FIFO queue, calls `pipeline.Execute`, calls `runtime.Evaluate`, manages the drain cycle. Owns `Session` and `Dispatch`/`StartMatch`/`FinishMatch`/`Dispose`. |
| `GameRulesDefinition` | Pure `[SerializeField]`-bearing data class (non-`UnityObject`). Per-game-mode authored config (starting life, hand size, format-specific knobs). Asset-friendly; replay/save/network-safe. |
| `GameRulesRuntime<TDefinition>` | Stateful runtime brain. Holds framework services + game-specific subsystems + a typed `Definition` + `ITurnFlow` + `IReadOnlyList<ISBA>`. DI-resolved per match via container open-generic resolution. Implements `FilterTriggers` and `Evaluate` (FSM evaluator). |
| `FilterTriggers` | Game-policy filter on which registered `TriggerMatch` candidates are eligible right now. Default: identity. Called once per command, twice per execution (Before / After). |
| `Evaluate` | FSM evaluator. Called after every `GameAction` resolves. Returns the new `MatchState`. Default body: SBAs preempt; otherwise `current.ShouldExit` + `Flow.NextAfter`. |
| `MatchState` | Abstract record carrying `Actions` (valid `GameCommand` types), `EnterCommands` (commands enqueued on entry), `ShouldExit` (predicate) plus phase-specific fields. Concrete subclasses are game-authored. See [`07-match-state.md`](features/engine/models/07-match-state.md). |
| `MatchStateSlice` | Framework-owned slice wrapping the polymorphic `MatchState`. One subscription channel; pattern-match `Current` to read phase data. Auto-seeded from `ITurnFlow.Initial` at `Build()`. |
| `TransitionStateCommand` | Framework-internal `GameAction` dispatched by the engine drain when `Evaluate` returns a different-typed state. Handler writes `MatchStateSlice` and enqueues `NewState.EnterCommands`. |
| `ITurnFlow` | Single source of turn-structure truth. `Initial` (seed state) + `NextAfter(current)` (successor). Game-authored, one per match. See [`08-turn-flow.md`](features/engine/models/08-turn-flow.md). |
| `ISBA` | State-based action — cross-cutting state-driven override that preempts `ShouldExit` / `Flow`. `Check(scope) → MatchState?` (null = no override). Game-authored list registered on the runtime. See [`09-state-based-actions.md`](features/engine/models/09-state-based-actions.md). |
| `SessionState` | Typed sealed hierarchy on `engine.Session` field — `NotStartedSession` / `InMatchSession` / `EndedSession(MatchOutcome)`. Replaces the retired `EngineLifecycle` enum. Future additions for networking: `ConnectingSession`, `ReconnectingSession`, etc. |
| `MatchOutcome` | Closed type hierarchy for end-of-match: `Win(Actor)` / `Loss(Actor)` / `Draw` / `Aborted(Reason)`. Carried on `EndMatchCommand`, `EndedSession`, and emitted via `MatchEndedSignal`. |
| `IGameFactory` | DI-resolved match-construction service. `Create(MatchSetup)` and `NewMatch()` (fluent `IMatchBuilder`). Caller never sees VContainer. |
| `MatchSetup` | Per-match data bundle: `Game` (`GameDefinition`), `Actors` (`IReadOnlyList<IActor>`), `Seed` + optional `InitialSlices` / `ZoneOverlay` / `PlayerDefinitionOverlay`. |
| `GameDefinition` | Top-level game-mode bundle. Carries `Rules`, `SharedZones`, `CardCatalog`, `DefaultPlayerDefinition`, `GetInitialSlices()`. Implements `ISliceProvider`. |
| `ISliceProvider` | Parameterless interface — `IEnumerable<BaseState> GetInitialSlices()`. Reached on Definition kinds via `DefinitionWithSlices` and on Instance kinds via `InstanceWithSlices<TDef>`; reached directly on `GameDefinition`. The factory keys per-spawn slices externally at `instance.SelfRef`. |
| Layout block | `GameDefinition.SharedZones` (game-level zones spawned once) and `PlayerDefinition.PerPlayerZones` (per-player zones spawned once per player). The factory walks both at `Build`. |
| Framework-owned slice | Auto-seeded at `Build` and writable only by framework services: `MatchStateSlice`, `EngineState`, `HistoryState`. Game authors never seed them in `MatchSetup.InitialSlices`. |
| Setup command | Ordinary `GameAction` dispatched after `engine.StartMatch()` to populate t=0 content (deal hands, advance to first phase). Identifiable only by *when* it runs. Two patterns: host-driven; runtime-driven (typically via `MatchState.EnterCommands` on the initial state). |
| `EngineState` | Framework-owned slice — `IsRunning` + `CurrentCommand?`. Drain instrumentation. Per ADR-0009 the previous `Lifecycle` field is removed; session lifecycle lives on `engine.Session`. |
| `HistoryState` | Framework-owned slice — ordered envelope log. Written by the engine-internal history mutator; read by presentation, networking, replay tooling. |

## Architecture

The feature has **eight Models**, organized as four concentric layers.

**Loop + rules-layer contract (Models 01–03):** the engine itself ([`01-game-engine.md`](features/engine/models/01-game-engine.md) — drain, `Dispatch`, runtime call cadence, two-layer intake gate, session lifecycle types) and the rules-layer contract split into two cooperating types — [`02-game-rules-definition.md`](features/engine/models/02-game-rules-definition.md) (the pure-data half) and [`03-game-rules-runtime.md`](features/engine/models/03-game-rules-runtime.md) (the runtime brain: `GameRulesRuntime<TDefinition>`, `FilterTriggers` + `Evaluate`, FSM composition, anti-scope, rejection paths).

**FSM machinery (Models 07–09):** the match's state machine. [`07-match-state.md`](features/engine/models/07-match-state.md) (`MatchState` abstract record + `MatchStateSlice` wrapper + `TransitionStateCommand`), [`08-turn-flow.md`](features/engine/models/08-turn-flow.md) (`ITurnFlow.Initial` + `NextAfter` — single source of turn-structure truth), [`09-state-based-actions.md`](features/engine/models/09-state-based-actions.md) (`ISBA.Check` — cross-cutting overrides that preempt `ShouldExit` / `Flow`). The underlying state primitive (`Store`, `BaseState`, `IStateScope`, `Mutator<TState, TPayload>`, subscription channels, `ExecuteBatch`) is owned by [`state`](features/state/state.md), not by engine. Framework slice tier conventions (framework-owned vs game-authored) live in [`state/state.md` §Slice tiers](features/state/state.md); the catalog of framework-owned slices is the §Framework-owned slices section in this feature root.

**Match construction (Models 05–06):** match construction ([`05-game-factory.md`](features/engine/models/05-game-factory.md) — `IGameFactory`, `MatchSetup`, `IMatchBuilder`, the two-layer split between DI installation and per-match factory, container open-generic resolution for `GameRulesRuntime<TDefinition>`), and the `GameDefinition` bundle + factory walk ([`06-game-definition.md`](features/engine/models/06-game-definition.md) — bundle shape, `ISliceProvider`, layout blocks, the canonical six-step Build walk). The `GameEngine` lifecycle methods + intake gates + session lifecycle types (`SessionState` hierarchy, `StartMatchCommand`/`EndMatchCommand`, signals, `MatchOutcome`) all live in [`01-game-engine.md`](features/engine/models/01-game-engine.md); setup-command patterns and the disposal split between engine and host live in this feature root (§Match setup and §Disposal split below).

**Archived (Model 04):** [`04-available-actions.md`](features/engine/archive/04-available-actions.md) — `IAvailableActions` service + `AvailableActionsState` slice. Superseded by `MatchState.Actions: IReadOnlyList<Type>` per [ADR-0009](Product/decisions/0009-engine-as-fsm.md). Presentation-side `ActionTemplate` wire-form and per-state UI projection reworked in [ADR-0012 — *forthcoming*].

The execution arc spans these:

1. **App boot.** Framework `EngineInstaller` registers `Store`, `IStateScope`, `ICommandPipeline`, `IGameFactory`, framework mutators (including `SetMatchStateMutator`), `IModifierFactory`. Game installer registers handlers, mutators, `GameRulesRuntime<TDefinition>`, `ITurnFlow`, `ISBA`s, game subsystems. (Model 05.)
2. **Match construction.** Caller resolves `IGameFactory`, calls `factory.Create(matchSetup)` (or fluent equivalent). Factory walks `GameDefinition`: auto-seed framework slices (including `MatchStateSlice` from `ITurnFlow.Initial`), spawn shared zones → players → per-player zones → overlay, install per-spawn `ISliceProvider` slices, install game-singleton slices, install manual `InitialSlices` overlay, resolve `GameRulesRuntime<TDefinition>` via container open-generic. Engine returns with `Session = NotStartedSession`. (Models 05 + 06.)
3. **Match start.** Host calls `engine.StartMatch()` → dispatches `StartMatchCommand` → handler emits `MatchStartedSignal` and flips `engine.Session` to `InMatchSession` → `Evaluate` runs and (typically) returns the current initial `MatchState`. The initial state's `EnterCommands` were already enqueued by the factory's seeding step or fire on the first transition. (Models 01 + 07.)
4. **Setup phase.** Host or initial-state `EnterCommands` dispatches setup commands (deal hands, advance to first phase). Setup commands are ordinary `GameAction`s; they flow through the pipeline like any other commands. (engine.md §Match setup.)
5. **Drain loop.** Each `engine.Dispatch(action)` runs the two-layer intake gate (Session + MatchState.Actions) → `pipeline.Validate` → enqueue. Drain dequeues, calls `pipeline.Execute` (before-fold → handler → after-fold → envelope), then `runtime.Evaluate(action, result)` for the new `MatchState`. If the type changed, engine enqueues `TransitionStateCommand(newState)`; its handler writes `MatchStateSlice` and enqueues `newState.EnterCommands`. (Models 01 + 03 + 07.)
6. **Match end.** A `MatchState` returned by `Evaluate` (typically `GameOverState` from an SBA) has `EnterCommands` including `EndMatchCommand(outcome)`, or the host calls `engine.FinishMatch(outcome)`. `EndMatchHandler` emits `MatchEndedSignal` and flips `engine.Session` to `EndedSession(outcome)`. Already-queued tail drains naturally; new dispatches reject at the Session gate. Host observes via signal in `HistoryState` or via `SessionChanged`. (Model 01.)
7. **Disposal.** Host disposes the per-match DI scope; `engine.Dispose()` ensures `FinishMatch(Aborted("Disposed"))` if not already ended and disposes the runtime if `IDisposable`. Scope teardown is the host's responsibility; engine never sees the scope object. (Models 01 + 05.)

## Composition

| Model | Role |
|---|---|
| [`01-game-engine.md`](features/engine/models/01-game-engine.md) | `GameEngine` orchestrator: drain, `Dispatch`, runtime call cadence, two-layer intake gate (`Session`, `MatchState.Actions`), session lifecycle types (`SessionState` hierarchy, `StartMatchCommand`/`EndMatchCommand`, signals, `MatchOutcome`) |
| [`02-game-rules-definition.md`](features/engine/models/02-game-rules-definition.md) | `GameRulesDefinition` — pure-data half of the rules layer (`[Serializable]` config, asset-friendly, replay/save/network-safe); `DefinitionWithSlices` and `ISliceProvider` flow through |
| [`03-game-rules-runtime.md`](features/engine/models/03-game-rules-runtime.md) | `GameRulesRuntime<TDefinition>` — runtime brain (services, `FilterTriggers`, `Evaluate` returning `MatchState`, default body composing SBAs + ShouldExit + Flow, anti-scope, five rejection paths, MTG SBA known difference) |
| [`05-game-factory.md`](features/engine/models/05-game-factory.md) | `IGameFactory` + `MatchSetup` + `IMatchBuilder`; the two-layer split between DI installation and per-match factory; container open-generic resolution for `GameRulesRuntime<TDefinition>` |
| [`06-game-definition.md`](features/engine/models/06-game-definition.md) | `GameDefinition` bundle (`Rules`, `SharedZones`, `CardCatalog`, `DefaultPlayerDefinition`, `GetInitialSlices()`); the canonical six-step Build walk; layout blocks |
| [`07-match-state.md`](features/engine/models/07-match-state.md) | `MatchState` abstract record (`Actions`, `EnterCommands`, `ShouldExit` + phase-specific fields), `MatchStateSlice` wrapper, `TransitionStateCommand` + `TransitionStateHandler`, `SetMatchStatePayload`, type-equality semantics |
| [`08-turn-flow.md`](features/engine/models/08-turn-flow.md) | `ITurnFlow` interface (`Initial`, `NextAfter`) — single source of turn-structure truth; visual-scripting-friendly transition table |
| [`09-state-based-actions.md`](features/engine/models/09-state-based-actions.md) | `ISBA.Check(scope) → MatchState?` — cross-cutting state-driven overrides; first-non-null-wins; registration order |
| ~~`04-available-actions.md`~~ | **Archived** ([`engine/archive/04-available-actions.md`](features/engine/archive/04-available-actions.md)) — `IAvailableActions` service + `AvailableActionsState` slice retired per ADR-0009; `MatchState.Actions` replaces them |

## Match setup

After `engine.StartMatch()` returns, the engine has `Session = InMatchSession`. The factory has already seeded `MatchStateSlice.Current` with `ITurnFlow.Initial`. The initial state's `EnterCommands` may fire setup work (deal hands, advance to first phase); world-empty zones populate via setup commands. **Setup commands** are the framework's pattern for populating content at t=0.

There is no special framework primitive for setup — setup commands are ordinary `GameAction`s dispatched through the normal pipeline, identifiable only by *when* they run (typically as `EnterCommands` on the initial `MatchState`, or directly after `StartMatch` returns). The framework supports two dispatch paths; games may use either or both.

### Initial-state-driven setup

The cleanest pattern: the game's `ITurnFlow.Initial` returns a setup-phase state whose `EnterCommands` carry the t=0 work.

```csharp
public sealed record InitialSetupState(IReadOnlyList<IActor> Actors) : MatchState {
    public override IReadOnlyList<GameAction> EnterCommands => Actors
        .SelectMany(a => Enumerable.Range(0, StartingHandSize)
            .Select(_ => new DrawCardCommand(a) as GameAction))
        .Append(new BeginFirstTurnCommand(Actors.First()))
        .ToList();
    public override bool ShouldExit(GameAction action, CommandResult _, IStateScope __)
        => action is BeginFirstTurnCommand;
}
```

The factory seeds `MatchStateSlice.Current = new InitialSetupState(actors)`. The first transition (or the seed itself, if the framework chooses to fire `EnterCommands` on initial seed) enqueues the setup work. After `BeginFirstTurnCommand` resolves, `ShouldExit` returns true and Flow advances to the first real phase.

### Host-driven setup

Alternative: the host dispatches setup commands directly after `StartMatch` returns. Useful for tests and scripted scenarios that bypass the runtime's setup logic.

```csharp
var engine = factory.Create(matchSetup);
engine.StartMatch(out _);

foreach (var (zoneRef, cardDefs) in puzzle.InitialDeals)
    foreach (var def in cardDefs)
        engine.Dispatch(new CreateAndInsertCardCommand(def, zoneRef), out _);

engine.Dispatch(new BeginFirstTurnCommand(firstPlayer), out _);
```

Use this when the host knows the setup steps statically — solo puzzles, tutorials, scripted scenarios, tests.

### Recommended pattern

**Initial-state-driven setup is the cleaner default** — it keeps setup logic in the same vocabulary as the rest of the FSM (states + EnterCommands), survives replay deterministically, and frees the host from needing to know game-specific setup details. Host-driven setup is the right call for tests and scripted scenarios where you specifically want to bypass the runtime's setup logic.

Setup commands flow through the pipeline like any other commands — `OnBefore<DrawCardCommand>` triggers fire on the opening-hand draws if any are registered. Games that need triggers to *not* fire during setup can guard via state (e.g., a `SetupPhase` flag in a game-singleton slice) or via the runtime's `FilterTriggers`.

### Naming convention

Framework-owned commands carry the `MatchCommand` suffix (`StartMatchCommand`, `EndMatchCommand`); framework-internal commands carry the `StateCommand` suffix (`TransitionStateCommand`). Game-authored setup commands have no required suffix; conventional names are `BeginFirstTurnCommand`, `DealOpeningHandsCommand`, `Setup<X>Command`. Choose names that read well in history envelopes — they appear in the canonical record alongside player actions.

## Disposal split

Two distinct teardowns, owned by different layers:

| Concern | Owner | When |
|---|---|---|
| Runtime instance disposal (if `IDisposable`) | `GameEngine` | `engine.Dispose()` |
| Per-match DI scope disposal | Host / outer controller | After observing match end |

`engine.Dispose()` releases what the engine itself constructed or holds:

1. If `Session is not EndedSession`, call `FinishMatch(new MatchOutcome.Aborted("Disposed"))`. The end command runs through the pipeline, `MatchEndedSignal` lands in history, `engine.Session` flips to `EndedSession`.
2. If the runtime implements `IDisposable`, dispose it.
3. Mark internal state as disposed; subsequent `Dispatch` / `StartMatch` / `FinishMatch` throw `ObjectDisposedException`.

The engine does **not** dispose the per-match DI scope. The factory's `Build` operates within an outer-owned scope ([`engine/05 §Behavior resolution`](features/engine/models/05-game-factory.md)); the same outer layer that opened the scope tears it down. A typical app-level flow:

```
1. GameSessionController opens a per-match VContainer scope.
2. factory.NewMatch().With...().Build()  → engine resolved within that scope.
3. engine.StartMatch()                   → match runs.
4. ... gameplay ...
5. Match ends (an SBA returns GameOverState whose EnterCommands include
   EndMatchCommand, host calls FinishMatch, or engine.Dispose triggers Aborted).
6. Controller observes engine.Session = EndedSession (via SessionChanged,
   or signal in history, or polling) and disposes the VContainer scope,
   which cascades through game subsystems registered scope-locally
   (IStackSystem, IDeckSystem, ...).
```

The engine surfaces `MatchEndedSignal` in history and `SessionChanged` as an event; the controller hooks either. The framework does not ship a "match-end callback" mechanism on the engine — observation is via state, history, or `SessionChanged`.

## Framework-owned slices

Three slices are auto-seeded by the framework at `Build()` and written only by framework-internal services. Game authors never declare any of these in `MatchSetup.InitialSlices` — including one throws at validation. Each slice's schema lives at the system that produces it; this table is the catalog index.

| Slice | Produced by | Schema canonical home |
|---|---|---|
| `EngineState` | Engine internals (drain start/stop, current-command tracking) | [`engine/01-game-engine.md` §The EngineState slice](features/engine/models/01-game-engine.md) |
| `MatchStateSlice` | Engine internals (factory seeds from `ITurnFlow.Initial`; `TransitionStateHandler` writes via `SetMatchStatePayload`) | [`engine/07-match-state.md`](features/engine/models/07-match-state.md) |
| `HistoryState` | Engine history mutator (post-`pipeline.Execute`) | [`actions/04-history-envelope.md` §The HistoryState slice](features/actions/models/04-history-envelope.md) |

The previous `AvailableActionsState` slice is **retired** per [ADR-0009](Product/decisions/0009-engine-as-fsm.md). Its role (valid command types for the framework intake gate) moves to `MatchStateSlice.Current.Actions`.

The framework-vs-game-authored slice tier convention itself (who seeds what, who writes what) lives in [`state/state.md` §Slice tiers](features/state/state.md) — that's a state-primitive concern, not engine-specific.

New framework-owned slices land at their producing system's canonical home and are added as a row above.

## Invariants

Cross-Model rules that hold across all eight Models and shape how downstream features must use the engine.

- **Strict-FIFO command pipeline; framework-owned routing.** Per [ADR-0006](Product/decisions/0006-two-pipeline-architecture.md), the queue is engine-private FIFO with no runtime customization. Effect-level orchestration (when each effect starts, batching/stacking) is a separate forthcoming surface ([ADR-0011 — *forthcoming*]) — it does NOT customize command routing.
- **Per-step `Evaluate`.** The engine calls `runtime.Evaluate` after every `GameAction`, not once at end of drain. `Evaluate` returns the new `MatchState`; the engine synthesizes `TransitionStateCommand` on type change.
- **Engine IS the FSM.** Per ADR-0009. Transitions flow through the command pipeline via `TransitionStateCommand`. There is no out-of-pipeline path for state changes — every transition lands in history with a normal envelope.
- **Runtime holds zero gameplay state.** Per [ADR-0002 (refreshed)](features/engine/models/03-game-rules-runtime.md). All gameplay memory lives as state slices in the Scaffold `Store`. The match's current state lives in `MatchStateSlice`, not on the runtime. Engine-internal plumbing (queue, drain flag) is NOT gameplay state — that's framework wiring.
- **State pattern is canonical.** `GameState` is multiple typed `BaseState` slices in a Scaffold `Store`. Reads always go through `IStateScope.Get<TState>()`; writes always go through `Store.Execute(payload)` → registered `Mutator<TState, TPayload>`. No `state.Piles` field; no ad-hoc mutation. `MatchStateSlice` is the only slice the framework writes via `TransitionStateCommand`; other writes flow from game handlers.
- **Framework-owned slices are auto-seeded; game-authored slices declared via `ISliceProvider`.** `MatchStateSlice`, `EngineState`, `HistoryState` install at `Build` from the framework. Game-singleton + per-spawn slices come from `GameDefinition.GetInitialSlices()` and `instance.GetInitialSlices()` (default delegates to `Definition.GetInitialSlices()`). `ISliceProvider` is parameterless; the factory keys at `instance.SelfRef`.
- **Lifecycle transitions are commands.** `engine.StartMatch()` and `engine.FinishMatch(outcome)` dispatch `StartMatchCommand` / `EndMatchCommand`. Handlers emit signals (`MatchStartedSignal`/`MatchEndedSignal`) and flip `engine.Session` directly (the field is the source of truth). No direct state-slice writes from the public engine surface for session lifecycle — but match-state transitions go through `TransitionStateCommand` → `SetMatchStatePayload` → mutator.
- **Two-layer intake gate is framework-owned.** `engine.Dispatch` consults `engine.Session` (Layer 1: NotStarted/InMatch/Ended admit different commands) and `MatchStateSlice.Current.Actions` (Layer 2: command type must appear, unless the list is empty or the command is a lifecycle/internal command). Both layers run before `pipeline.Validate`. Per [ADR-0010 — *forthcoming*] the gate moves to registered framework validators (`SessionGate`, `MatchStateActionGate`); until then the inline gate is the transitional shape. Per ADR-0006 there is no runtime-side intake hook.
- **Session lifecycle lives on `engine.Session`, not a slice.** Per ADR-0009 D12. `SessionState` is a typed sealed hierarchy. Subscribers use `SessionChanged` event or read the field. Match-FSM subscribers continue using `MatchStateSlice`.
- **Two-layer DI split.** App-level VContainer registers behaviors (handlers, mutators, runtime, ITurnFlow, ISBAs, subsystems); per-match factory accepts only data (`GameDefinition`, actors, seed, overlays). `IGameFactory` is DI-resolved; caller code never sees `IContainerBuilder` / `IObjectResolver`.
- **`GameDefinition` is the bundle.** Carries `Rules`, `SharedZones`, `CardCatalog`, `DefaultPlayerDefinition`, `GetInitialSlices()`. `MatchSetup` does not duplicate any of these — it carries the bundle plus per-match overlays.
- **Slices don't carry their primary key.** Storage is keyed externally by the factory using `instance.SelfRef`. Reference-shaped fields inside slice values are *data*, not keys.
- **Engine disposes its runtime; host disposes its scope.** `engine.Dispose` releases the runtime if `IDisposable` and ensures `FinishMatch(Aborted)` if not already ended. The per-match DI scope is opened and torn down by the host.
- **`Dispatch(GameAction)`, not `Dispatch(GameCommand)`.** `GameSignal` is emitted via `scope.Emit` from inside handlers; `CompositeAction` runs inline within a parent handler. The engine's queue holds only `GameAction`s.

## Boundaries

**Owns:**
- The `GameEngine` class (loop, queue, drain) and its public surface (`Dispatch`/`StartMatch`/`FinishMatch`/`Dispose`/`Session`/`SessionChanged`).
- The `GameRulesDefinition` + `GameRulesRuntime<TDefinition>` contract (the two-type split, the two methods, the zero-gameplay-state rule, the anti-scope, the FSM-evaluator shape).
- The FSM machinery: `MatchState` abstract record + `MatchStateSlice` wrapper, `ITurnFlow`, `ISBA`, `TransitionStateCommand` + `TransitionStateHandler`, `SetMatchStatePayload`.
- The framework-owned slice tier (`MatchStateSlice`, `EngineState`, `HistoryState`) and the auto-seeding rule.
- The `ISliceProvider` mechanism and the canonical Build walk (six steps, ordering guarantees).
- The `IGameFactory` / `MatchSetup` / `IMatchBuilder` surface and the two-layer DI split.
- The session lifecycle (`SessionState` typed hierarchy on `engine.Session`, `StartMatchCommand` / `EndMatchCommand`, `MatchOutcome` hierarchy, `SessionChanged` event).
- The two-layer intake gate (Session + MatchState.Actions; transitional shape pending validator-pipeline migration per ADR-0010).
- `GameDefinition` and the layout-block walk (`SharedZones` / per-player zones).
- Container open-generic resolution for pairing `GameRulesDefinition` to `GameRulesRuntime<TDefinition>`.

**Does not own:**
- Command-pipeline mechanics (handler resolution, validate/execute, envelope shape, `IIntentMapper` / `IntentGateway`) → `actions/`
- Trigger fold mechanics (`Trigger<T>`, `OnBefore`/`OnAfter`, subscription, `ITriggerPipeline` ordering) → `triggers/`
- Effect orchestration (`Effect`, `IEffectScope`, `ICommandAwaiter` — *rework pending under ADR-0011*, attachment-entity pattern) → `effects/`
- Handler shape (`ICommandHandler<TCommand, TResult>`, `GameCommandHandler<,>` base, `ValidatedCommandHandler<,>` combined base) and validator chain (`ICommandValidator<TCommand>`, `CommandValidator<>` base, framework defaults `SessionGate` + `MatchStateActionGate`) → `actions/`
- Entity bridge (`EntityState` slice, `InstanceWithSlices<TDef>` parent, modifier factory, `IEntityVariableStorage`) → `entities/`
- Card-specific shape (`Card`, `CardDefinition`, `CardMapState`) → `cards/`
- Zone-specific shape (`Zone`, `ZoneDefinition`, `ZoneContentsState`) → `zones/`
- Player-specific shape (`Player`, `PlayerDefinition`, `PerPlayerZones`) → `players/`
- Presentation (view bindings, animation handoff, per-state ViewModels per [ADR-0012 — *forthcoming*]) → `presentation/`
- Trigger ordering (the default `FifoPipeline`'s priority + stable tie-break, custom `ITriggerPipeline` implementations) → `triggers/`
- Game-specific subsystems (`IStackSystem`, `IDeckSystem`, etc.) — game-side authoring, registered in the game's installer.

## Dependencies

*Mirrors the front-matter `references:` array in human-readable form (per §3).*

- **Upstream:** `state/` (the underlying state primitive — `Store`, `BaseState`, `IStateScope`, `Mutator<TState, TPayload>`, subscriptions, atomic batches), `actions/` (command pipeline mechanics — the engine wraps it), `triggers/` (trigger fold runs inside `pipeline.Execute`), `effects/` (effect-side service surfaces — shape what runtime/handlers see and write), `entities/` (entity-bridge runtime surface — handles, modifier factory, slice-keying convention).
- **Downstream:** game-side rules code (defines `GameRulesDefinition` + `GameRulesRuntime<TDefinition>` + concrete `MatchState` subclasses + `ITurnFlow` + `ISBA`s + `GameDefinition` for each game-mode variant), `presentation/` (consumes `MatchStateSlice` + `engine.Session` + `HistoryState` envelopes via state subscriptions / engine event).

## Open

See `ledgers/OPEN-ITEMS.md` — engine-related questions:

- `oq-effect-pipeline` — effect-level orchestration surface (when each effect starts, whether effects nest / batch / stack). Per ADR-0006 the customizable layer where game styles diverge; designed in [ADR-0011 — *forthcoming*].
- `oq-engine-builder` — service composition (where `IModifierFactory`, deterministic ref/id generators, per-kind registries are constructed, registered, exposed). Pin during the engine implementation pass.
- `oq-gamerules-composition` — module composition contract for `GameRulesRuntime<T>` (provider/policy ordering, enable-disable, conflict resolution). Service surface supports it mechanically; pin when first composed-rules game forces it.
- ~~**Validator pipeline migration**~~ — *Resolved 2026-05-11 per [ADR-0010](Product/decisions/0010-validator-handler-split.md).* The two-layer intake gate is now expressed as two registered framework-default validators (`SessionGate`, `MatchStateActionGate`); the inline gate is retired.
- **Hybrid session lifecycle.** Per ADR-0009 D12, the long-term shape splits session orchestration between host and engine. The `Session` property + `SessionChanged` event are the framework hooks; how host code composes lobby / matchmaking / reconnect against them is host-shaped, not framework-shaped. Pin when networking lands.
- **MTG SBA ordering** — the framework's evaluation loop diverges from MTG-strict state-based-action ordering by design. If a future game needs MTG semantics, decide whether to add a pre-after-fold evaluator hook or document the workaround. Re-open trigger: first game forcing the issue.
- **`HistoryEnvelope` typed definition** — concrete C# record (fields: `envelopeId`, `originCommandId`, `actor`, `entries[]`) is pseudocode. Pin when replay or multiplayer transport is designed.
- **`EngineState` surface** — whether `IsRunning`/`CurrentCommand` are the right fields, or whether additional engine-internal state belongs here. Deferred to engine implementation pass.
- **Empty-actors mode** — whether `MatchSetup.Actors` may be empty (purely simulation-driven games). Defer until a real game forces it.
- **Networking-driven `IRandom`** — `MatchSetup.Seed` is currently `int`. When networking lands, `IRandom` (or richer determinism contract) replaces this slot.
- **Async-effect cancellation on match end** — when `EndMatchCommand` resolves while an effect is mid-`await`, the awaiting effect is still alive. Per [ADR-0011 — *forthcoming*] the resolution path threads a `CancellationToken` through `IEffectScope.Dispatch`.
- **Multi-winner / team outcomes** — `MatchOutcome.Win` is single-actor. Add `TeamWin` (or similar) when a co-op / team game forces it.
- `bl-actor-ownership-reverse-index` — `OwnershipState` is one-way today (`Reference → IActor`); reverse direction (`IActor → owned References`) deferred.
- **Slice-level subscription teardown** — `SubscribeAllReferences<T>` / `SubscribeAny` lack symmetric `Unsubscribe` overloads.
- **`IAggregateProvider` registration.** `Store.RegisterAggregate` ships in Scaffold; framework hasn't pinned a use case (computed read-only views over multiple slices).
