---
type: feature
status: settled
references: [features/actions/models/01-command-pipeline.md, features/triggers/models/01-trigger-fold.md, features/effects/models/01-effect.md, features/entities/models/02-runtime-surface.md]
tags: [#engine]
---

# engine — feature

## What it is

The framework's orchestrator. The engine owns the **`GameEngine` class** (drain a strict-FIFO queue, execute each `GameAction` through the command pipeline, ask the runtime what runs next, single `Dispatch` entry point), pins the **rules-layer contract** (`GameRulesDefinition` + `GameRulesRuntime<TDefinition>` — pure data + stateful brain, paired via DI container open-generic resolution), produces the **AvailableActions snapshot** through a service-driven pipeline (`IAvailableActions.Set`/`Append`/`Clear`), layers the **framework slice tier** (framework-owned auto-seeded; game-authored declared via `ISliceProvider`) over the underlying state primitive ([`state`](features/state/state.md)), constructs matches via the **`IGameFactory` two-layer split** (DI installation vs per-match `MatchSetup`), and owns the **match lifecycle system** (FSM + gate + framework-required `StartMatchCommand`/`EndMatchCommand`). The **`GameDefinition`** bundle ties everything together — `Rules` + `SharedZones` + `CardCatalog` + `DefaultPlayerDefinition` + `GetInitialSlices()` — and the factory walks it at `Build`.

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
                   ├── Auto-seed framework slices (AvailableActionsState, EngineState, HistoryState)
                   ├── Spawn shared zones → players → per-player zones (overlay)
                   ├── Install per-spawn slices (ISliceProvider, keyed at SelfRef)
                   ├── Install game-singleton slices
                   └── Resolve GameRulesRuntime<TDefinition> via container open-generic
                              │
                              ▼
                   GameEngine (NotStarted)
                   engine.StartMatch() → dispatch StartMatchCommand
                              │
                              ▼
       ┌──── Lifecycle gate (NotStarted/Started/Ended) ────┐
       │                                                    │
   engine.Dispatch(action) ──▶ pipeline.Validate ──▶ pending.Enqueue
                                                            │
                                                            ▼
                                                       Drain loop:
                                                       while (queue):
                                                         pipeline.Execute(action)
                                                           ├── before-fold (FilterTriggers + ITriggerPipeline)
                                                           ├── handler.Handle (ctx.Store.Execute payloads)
                                                           ├── after-fold
                                                           └── envelope published
                                                         runtime.Evaluate(action, result)
                                                           → 0..N follow-ups (queue)
                                                           → maybe Actions.Set(snapshot)
```

## Vocabulary

**Feature-owned terms only.** Cross-feature terms used by this feature — `Store`, `BaseState`, `IStateScope`, `Mutator<TState, TPayload>`, `StateChangeEvent` (defined in `state/`); `GameAction`, `GameSignal`, `CompositeAction`, `GameCommand`, `Trigger<T>`, `OnBefore`/`OnAfter`, `TriggerMatch`, `TriggerPhase`, `ITriggerPipeline` (defined in `triggers/`); `IEffectScope`, `IHandlerContext`, `Effect`, `ICommandPipeline`, `ICommandHandler`, `IIntentMapper`, `ActionTemplate`, `AvailableActionsSnapshot`, `PlayerIntent`, `HistoryEnvelope`, `CommandResult` (defined in `effects/` / `actions/`); `Reference`, `Ref<T>`, `IActor`, `ActorId`, `Card`, `Zone`, `Player`, `CardDefinition`, `ZoneDefinition`, `PlayerDefinition`, `InstanceWithSlices<TDef>`, `DefinitionWithSlices`, `IModifierFactory`, `ZoneContentsState`, `CardMapState`, `OwnershipState`, `EntityState` — live in their owning features or `Product/ledgers/GLOSSARY.md`; link to their canonical home, don't restate.

| Term | Meaning |
|---|---|
| `GameEngine` | The orchestrator. Holds the framework-private FIFO queue, calls `pipeline.Execute`, calls `runtime.Evaluate`, manages the drain cycle. Owns `Lifecycle` and `Dispatch`/`StartMatch`/`FinishMatch`/`Dispose`. |
| `GameRulesDefinition` | Pure `[SerializeField]`-bearing data class (non-`UnityObject`). Per-game-mode authored config (starting life, hand size, format-specific knobs). Asset-friendly; replay/save/network-safe. |
| `GameRulesRuntime<TDefinition>` | Stateful runtime brain. Holds framework services + game-specific subsystems + a typed `Definition`. DI-resolved per match via container open-generic resolution. Implements `FilterTriggers` and `Evaluate`. |
| `FilterTriggers` | Game-policy filter on which registered `TriggerMatch` candidates are eligible right now. Default: identity. Called once per command, twice per execution (Before / After). |
| `Evaluate` | State-based follow-up. Called after every `GameAction` resolves. Reads state, returns 0..N follow-up actions to enqueue. May call `Actions.Set(...)` to publish the snapshot. |
| `IAvailableActions` | Snapshot writer service — `Set`/`Append`/`Clear`. The single write path for `AvailableActionsState`. Runtime calls from `Evaluate`; effects call mid-execution; engine never auto-publishes. |
| `EngineLifecycle` | `NotStarted` / `Started` / `Ended`. Mirrored on `EngineState.Lifecycle`. The framework's lifecycle gate consults this on every `Dispatch`. |
| `MatchOutcome` | Closed type hierarchy for end-of-match: `Win(Actor)` / `Loss(Actor)` / `Draw` / `Aborted(Reason)`. Carried on `EndMatchCommand` and emitted via `MatchEndedSignal`. |
| `IGameFactory` | DI-resolved match-construction service. `Create(MatchSetup)` and `NewMatch()` (fluent `IMatchBuilder`). Caller never sees VContainer. |
| `MatchSetup` | Per-match data bundle: `Game` (`GameDefinition`), `Actors` (`IReadOnlyList<IActor>`), `Seed` + optional `InitialSlices` / `ZoneOverlay` / `PlayerDefinitionOverlay`. |
| `GameDefinition` | Top-level game-mode bundle. Carries `Rules`, `SharedZones`, `CardCatalog`, `DefaultPlayerDefinition`, `GetInitialSlices()`. Implements `ISliceProvider`. |
| `ISliceProvider` | Parameterless interface — `IEnumerable<BaseState> GetInitialSlices()`. Reached on Definition kinds via `DefinitionWithSlices` and on Instance kinds via `InstanceWithSlices<TDef>`; reached directly on `GameDefinition`. The factory keys per-spawn slices externally at `instance.SelfRef`. |
| Layout block | `GameDefinition.SharedZones` (game-level zones spawned once) and `PlayerDefinition.PerPlayerZones` (per-player zones spawned once per player). The factory walks both at `Build`. |
| Framework-owned slice | Auto-seeded at `Build` and writable only by framework services: `AvailableActionsState`, `EngineState`, `HistoryState`. Game authors never seed them in `MatchSetup.InitialSlices`. |
| Setup command | Ordinary `GameAction` dispatched after `engine.StartMatch()` to populate t=0 content (deal hands, advance to first phase). Identifiable only by *when* it runs. Two patterns: host-driven, runtime-driven. |
| `EngineState` | Framework-owned slice — `Lifecycle` + `IsRunning` + `CurrentCommand?`. Source of truth for the lifecycle gate; mirrored on `engine.Lifecycle`. |
| `HistoryState` | Framework-owned slice — ordered envelope log. Written by the engine-internal history mutator; read by presentation, networking, replay tooling. |

## Architecture

The feature has **six Models**, organized as three concentric layers.

**Loop & rules-layer contract (Models 01–03):** the engine itself ([`01-game-engine.md`](features/engine/models/01-game-engine.md) — drain, `Dispatch`, runtime call cadence, lifecycle types) and the rules-layer contract split into two cooperating types — [`02-game-rules-definition.md`](features/engine/models/02-game-rules-definition.md) (the pure-data half: `[Serializable]`, `[SerializeField]`-bearing config, asset-friendly, replay/save/network-safe) and [`03-game-rules-runtime.md`](features/engine/models/03-game-rules-runtime.md) (the runtime brain: `GameRulesRuntime<TDefinition>`, the two contract methods `FilterTriggers` + `Evaluate`, anti-scope, three rejection paths).

**Available actions (Model 04):** snapshot production ([`04-available-actions.md`](features/engine/models/04-available-actions.md) — `IAvailableActions` service, typed `ActionTemplate` per command type, on-demand validation posture, `AvailableActionsState` slice). The underlying state primitive (`Store`, `BaseState`, `IStateScope`, `Mutator<TState, TPayload>`, subscription channels, `ExecuteBatch`) is owned by [`state`](features/state/state.md), not by engine. Framework slice tier conventions (framework-owned vs game-authored) live in [`state/state.md` §Slice tiers](features/state/state.md); the catalog of framework-owned slices is the §Framework-owned slices section in this feature root.

**Match construction (Models 05–06):** match construction ([`05-game-factory.md`](features/engine/models/05-game-factory.md) — `IGameFactory`, `MatchSetup`, `IMatchBuilder`, the two-layer split between DI installation and per-match factory, container open-generic resolution for `GameRulesRuntime<TDefinition>`), and the `GameDefinition` bundle + factory walk ([`06-game-definition.md`](features/engine/models/06-game-definition.md) — bundle shape, `ISliceProvider`, layout blocks, the canonical six-step Build walk). The `GameEngine` lifecycle methods + intake gate behavior + lifecycle types (`EngineLifecycle` enum, `StartMatchCommand`/`EndMatchCommand`, signals, `MatchOutcome`) all live in [`01-game-engine.md`](features/engine/models/01-game-engine.md); setup-command patterns and the disposal split between engine and host live in this feature root (§Match setup and §Disposal split below).

The execution arc spans these:

1. **App boot.** Framework `EngineInstaller` registers `Store`, `IStateScope`, `IAvailableActions`, `ICommandPipeline`, `IGameFactory`, framework mutators, `IModifierFactory`. Game installer registers handlers, mutators, `GameRulesRuntime<TDefinition>`, `IIntentMapper`, game subsystems. (Model 05.)
2. **Match construction.** Caller resolves `IGameFactory`, calls `factory.Create(matchSetup)` (or fluent equivalent). Factory walks `GameDefinition`: auto-seed framework slices, spawn shared zones → players → per-player zones → overlay, install per-spawn `ISliceProvider` slices, install game-singleton slices, install manual `InitialSlices` overlay, resolve `GameRulesRuntime<TDefinition>` via container open-generic. Engine returns in `NotStarted`. (Models 05 + 06.)
3. **Match start.** Host calls `engine.StartMatch()` → dispatches `StartMatchCommand` → handler emits `MatchStartedSignal` and flips `EngineState.Lifecycle` → `Evaluate` runs and (typically) publishes initial snapshot via `Actions.Set`. (Model 01.)
4. **Setup phase.** Host or runtime dispatches setup commands (deal hands, advance to first phase). Setup commands are ordinary `GameAction`s; they flow through the trigger fold like any other commands. (engine.md §Match setup.)
5. **Drain loop.** Each `engine.Dispatch(action)` lifecycle-gates → `pipeline.Validate` → enqueue. Drain dequeues, calls `pipeline.Execute` (before-fold via `FilterTriggers` + `ITriggerPipeline` → handler → after-fold → envelope published), then `runtime.Evaluate(action, result)` for state-based follow-ups. The runtime publishes snapshots via `Actions.Set` from inside `Evaluate`. (Models 01 + 03 + 04.)
6. **Match end.** Runtime returns `EndMatchCommand` from `Evaluate` (state-based win condition), or host calls `engine.FinishMatch(outcome)`. `EndMatchHandler` emits `MatchEndedSignal` and flips lifecycle. Already-queued tail drains naturally; new dispatches reject. Host observes via signal in `HistoryState` or state subscription on `EngineState.Lifecycle`. (Model 01.)
7. **Disposal.** Host disposes the per-match DI scope; `engine.Dispose()` ensures `FinishMatch(Aborted("Disposed"))` if not already ended and disposes the runtime if `IDisposable`. Scope teardown is the host's responsibility; engine never sees the scope object. (Models 01 + 05.)

## Match setup

After `engine.StartMatch()` returns, the engine is in `Started` but the world is content-empty: zones spawned by `Build` exist but hold no cards; players exist with default variable values; game-singleton slices that needed actor refs (e.g. `TurnState.Active`) are still null. **Setup commands** are the framework's pattern for populating this content.

There is no special framework primitive for setup — setup commands are ordinary `GameAction`s dispatched through the normal pipeline, identifiable only by *when* they run (between `StartMatchCommand` and the first player-driven action). The framework supports two dispatch paths; games may use either or both.

### Host-driven setup

The simplest pattern: the host (the code that called `engine.StartMatch()`) dispatches setup commands directly after `StartMatch` returns.

```csharp
var engine = factory.Create(matchSetup);
engine.StartMatch(out _);                              // dispatches StartMatchCommand

// Host-driven setup commands
foreach (var (zoneRef, cardDefs) in puzzle.InitialDeals)
    foreach (var def in cardDefs)
        engine.Dispatch(new CreateAndInsertCardCommand(def, zoneRef), out _);

engine.Dispatch(new BeginFirstTurnCommand(firstPlayer), out _);

// At this point, normal play begins.
```

Use this when the host knows the setup steps statically — solo puzzles, tutorials, scripted scenarios, tests.

### Runtime-driven setup

Alternative: the `GameRulesRuntime` returns setup commands from its first `Evaluate` invocation. The drain after `StartMatchCommand` is the runtime's first opportunity to act; commands returned from `Evaluate` queue immediately and run before the host's next `Dispatch`.

```csharp
public class StandardMagicRulesRuntime : GameRulesRuntime<MagicRulesDefinition>
{
    public override IReadOnlyList<GameAction> Evaluate(GameAction _, CommandResult __)
    {
        if (State.Get<SetupPhaseState>().Complete) return Array.Empty<GameAction>();

        var actions = new List<GameAction>();

        // Deal opening hands, decide first player, advance to first phase, …
        foreach (var player in GetPlayers(State))
            for (int i = 0; i < Definition.StartingHandSize; i++)
                actions.Add(new DrawCardCommand(player));

        actions.Add(new BeginFirstTurnCommand(ChooseFirstPlayer(State)));
        actions.Add(new MarkSetupCompleteCommand());

        return actions;
    }
}
```

Use this when setup logic is rule-defined (depends on game data, varies by mode) rather than host-defined. The runtime owns the timing and the order; the host doesn't need to know what setup looks like. Per ADR-0002 the runtime holds zero gameplay state — track setup completion via a state slice (e.g. `SetupPhaseState`), not a runtime field.

### Recommended pattern

For most games, **runtime-driven setup is the cleaner default** — it keeps setup logic next to the game's other rules, survives replay (the runtime re-runs the same logic deterministically given the same `Definition` and seed), and frees the host from needing to know game-specific setup details. Host-driven setup is the right call for tests and scripted scenarios where you specifically want to bypass the runtime's setup logic.

Setup commands flow through the trigger fold like any other commands — `OnBefore<DrawCardCommand>` triggers fire on the opening-hand draws if any are registered. This is usually the desired behavior; games that need triggers to *not* fire during setup can guard via game state (e.g. a `SetupPhase` flag in `EngineState` or a per-game state slice).

### Naming convention

Framework-owned commands carry the `MatchCommand` suffix (`StartMatchCommand`, `EndMatchCommand`). Game-authored setup commands have no required suffix; conventional names are `BeginFirstTurnCommand`, `DealOpeningHandsCommand`, `Setup<X>Command`. Choose names that read well in history envelopes — they appear in the canonical record alongside player actions.

## Disposal split

Two distinct teardowns, owned by different layers:

| Concern | Owner | When |
|---|---|---|
| Runtime instance disposal (if `IDisposable`) | `GameEngine` | `engine.Dispose()` |
| Per-match DI scope disposal | Host / outer controller | After observing match end |

`engine.Dispose()` releases what the engine itself constructed or holds:

1. If `Lifecycle != Ended`, call `FinishMatch(new MatchOutcome.Aborted("Disposed"))`. The end command runs through the pipeline, `MatchEndedSignal` lands in history, lifecycle flips.
2. If the runtime implements `IDisposable`, dispose it.
3. Mark internal state as disposed; subsequent `Dispatch` / `StartMatch` / `FinishMatch` throw `ObjectDisposedException`.

The engine does **not** dispose the per-match DI scope. The factory's `Build` operates within an outer-owned scope ([`engine/05 §Behavior resolution`](features/engine/models/05-game-factory.md)); the same outer layer that opened the scope tears it down. A typical app-level flow:

```
1. GameSessionController opens a per-match VContainer scope.
2. factory.NewMatch().With...().Build()  → engine resolved within that scope.
3. engine.StartMatch()                   → match runs.
4. ... gameplay ...
5. Match ends (runtime dispatches EndMatchCommand, host calls FinishMatch,
   or engine.Dispose triggers Aborted).
6. Controller observes Lifecycle = Ended (via state subscription, or signal,
   or polling) and disposes the VContainer scope, which cascades through
   game subsystems registered scope-locally (IStackSystem, IDeckSystem, ...).
```

The engine surfaces `MatchEndedSignal` in history; the controller hooks it however it likes. The framework does not ship a "match-end callback" mechanism on the engine — observation is via state or history, both of which already exist.

## Framework-owned slices

Three slices are auto-seeded by the framework at `Build()` and written only by framework-internal services. Game authors never declare any of these in `MatchSetup.InitialSlices` — including one throws at validation. Each slice's schema lives at the system that produces it; this table is the catalog index.

| Slice | Produced by | Schema canonical home |
|---|---|---|
| `EngineState` | Engine internals (drain start/stop, lifecycle handlers) | [`engine/01-game-engine.md` §The EngineState slice](features/engine/models/01-game-engine.md) |
| `AvailableActionsState` | `IAvailableActions` service | [`engine/04-available-actions.md` §The AvailableActionsState slice](features/engine/models/04-available-actions.md) |
| `HistoryState` | Engine history mutator (post-`pipeline.Execute`) | [`actions/04-history-envelope.md` §The HistoryState slice](features/actions/models/04-history-envelope.md) |

The framework-vs-game-authored slice tier convention itself (who seeds what, who writes what) lives in [`state/state.md` §Slice tiers](features/state/state.md) — that's a state-primitive concern, not engine-specific.

New framework-owned slices land at their producing system's canonical home and are added as a row above.

## Invariants

Cross-Model rules that hold across all seven Models and shape how downstream features must use the engine.

- **Strict-FIFO command pipeline; framework-owned routing.** Per [ADR-0006](Product/decisions/0006-two-pipeline-architecture.md), the queue is engine-private FIFO with no runtime customization. Effect-level orchestration (when each effect starts, batching/stacking) is a separate forthcoming surface (`oq-effect-pipeline`) — it does NOT customize command routing.
- **Per-step `Evaluate`.** The engine calls `runtime.Evaluate` after every `GameAction`, not once at end of drain. The runtime detects "queue is about to empty" by observing state plus its own follow-up emptiness. There is no `OnQueueDrained` callback.
- **Runtime holds zero gameplay state.** Per [ADR-0002 (refreshed)](features/engine/models/03-game-rules-runtime.md). All gameplay memory (last-move, phase, score, MTG-stack contents) lives as state slices in the Scaffold `Store`. Engine-internal plumbing (queue, drain flag) is NOT gameplay state — that's framework wiring.
- **Snapshot is single-write-path.** `IAvailableActions.Set`/`Append`/`Clear` is the only public write path for `AvailableActionsState`. Runtime publishes from `Evaluate`; effects publish mid-execution for decision pickers; the engine never auto-publishes. If nobody calls the service, the snapshot does not change.
- **State pattern is canonical.** `GameState` is multiple typed `BaseState` slices in a Scaffold `Store`. Reads always go through `IStateScope.Get<TState>()`; writes always go through `Store.Execute(payload)` → registered `Mutator<TState, TPayload>`. No `state.Piles` field; no ad-hoc mutation.
- **Framework-owned slices are auto-seeded; game-authored slices declared via `ISliceProvider`.** `AvailableActionsState`, `EngineState`, `HistoryState` install at `Build` from the framework. Game-singleton + per-spawn slices come from `GameDefinition.GetInitialSlices()` and `instance.GetInitialSlices()` (default delegates to `Definition.GetInitialSlices()`). `ISliceProvider` is parameterless; the factory keys at `instance.SelfRef`.
- **Lifecycle transitions are commands.** `engine.StartMatch()` and `engine.FinishMatch(outcome)` dispatch `StartMatchCommand` / `EndMatchCommand`. Handlers emit signals (`MatchStartedSignal`/`MatchEndedSignal`) and flip `EngineState.Lifecycle` via the framework-internal `SetEngineLifecyclePayload` mutator. No direct state writes from the public engine surface.
- **Lifecycle gate is uniform and framework-owned.** `engine.Dispatch` gates on `Lifecycle` before `pipeline.Validate`. `NotStarted` admits only `StartMatchCommand`/`EndMatchCommand`; `Ended` admits nothing. Per ADR-0006 the runtime no longer has an intake hook.
- **Two-layer DI split.** App-level VContainer registers behaviors (handlers, mutators, runtime, intent mapper, subsystems); per-match factory accepts only data (`GameDefinition`, actors, seed, overlays). `IGameFactory` is DI-resolved; caller code never sees `IContainerBuilder` / `IObjectResolver`.
- **`GameDefinition` is the bundle.** Carries `Rules`, `SharedZones`, `CardCatalog`, `DefaultPlayerDefinition`, `GetInitialSlices()`. `MatchSetup` does not duplicate any of these — it carries the bundle plus per-match overlays.
- **Slices don't carry their primary key.** Storage is keyed externally by the factory using `instance.SelfRef`. Reference-shaped fields inside slice values are *data*, not keys.
- **Engine disposes its runtime; host disposes its scope.** `engine.Dispose` releases the runtime if `IDisposable` and ensures `FinishMatch(Aborted)` if not already ended. The per-match DI scope is opened and torn down by the host.
- **`Dispatch(GameAction)`, not `Dispatch(GameCommand)`.** `GameSignal` is emitted via `scope.Emit` from inside handlers; `CompositeAction` runs inline within a parent handler. The engine's queue holds only `GameAction`s.

## Boundaries

**Owns:**
- The `GameEngine` class (loop, queue, drain) and its public surface (`Dispatch`/`StartMatch`/`FinishMatch`/`Dispose`/`Lifecycle`).
- The `GameRulesDefinition` + `GameRulesRuntime<TDefinition>` contract (the two-type split, the two methods, the zero-gameplay-state rule, the anti-scope of what does NOT belong on the runtime).
- The snapshot production pipeline (`IAvailableActions`, `AvailableActionsSnapshot`, the runtime-and-effects write path, on-demand validation posture).
- The framework-owned slice tier (`AvailableActionsState`, `EngineState`, `HistoryState`) and the auto-seeding rule.
- The `ISliceProvider` mechanism and the canonical Build walk (six steps, ordering guarantees).
- The `IGameFactory` / `MatchSetup` / `IMatchBuilder` surface and the two-layer DI split.
- The match lifecycle (`EngineLifecycle` enum, `StartMatchCommand` / `EndMatchCommand`, `MatchOutcome` hierarchy, lifecycle gate, intake gates table).
- `GameDefinition` and the layout-block walk (`SharedZones` / per-player zones).
- Container open-generic resolution for pairing `GameRulesDefinition` to `GameRulesRuntime<TDefinition>`.

**Does not own:**
- Command-pipeline mechanics (handler resolution, validate/execute, envelope shape, `IIntentMapper` / `IntentGateway`) → `actions/`
- Trigger fold mechanics (`Trigger<T>`, `OnBefore`/`OnAfter`, subscription, `ITriggerPipeline` ordering) → `triggers/`
- Effect orchestration (`Effect`, `IEffectScope`, `IHandlerContext`, `ICommandAwaiter`, attachment-entity pattern) → `effects/`
- Entity bridge (`EntityState` slice, `InstanceWithSlices<TDef>` parent, modifier factory, `IEntityVariableStorage`) → `entities/`
- Card-specific shape (`Card`, `CardDefinition`, `CardMapState`) → `cards/`
- Zone-specific shape (`Zone`, `ZoneDefinition`, `ZoneContentsState`) → `zones/`
- Player-specific shape (`Player`, `PlayerDefinition`, `PerPlayerZones`) → `players/`
- Presentation (`AffordanceMap`, view bindings, animation handoff) → `presentation/`
- Trigger ordering (the default `FifoPipeline`'s priority + stable tie-break, custom `ITriggerPipeline` implementations) → `triggers/`
- Game-specific subsystems (`IStackSystem`, `IDeckSystem`, etc.) — game-side authoring, registered in the game's installer.

## Dependencies

*Mirrors the front-matter `references:` array in human-readable form (per §3).*

- **Upstream:** `state/` (the underlying state primitive — `Store`, `BaseState`, `IStateScope`, `Mutator<TState, TPayload>`, subscriptions, atomic batches), `actions/` (command pipeline mechanics — the engine wraps it), `triggers/` (trigger fold runs inside `pipeline.Execute`), `effects/` (effect-side service surfaces — `IEffectScope` / `IHandlerContext` / `ICommandAwaiter` shape what runtime/handlers see and write), `entities/` (entity-bridge runtime surface — handles, modifier factory, slice-keying convention).
- **Downstream:** game-side rules code (defines `GameRulesDefinition` + `GameRulesRuntime<TDefinition>` + `GameDefinition` for each game-mode variant), `presentation/` (consumes `AvailableActionsState` snapshot + `EngineState.Lifecycle` + `HistoryState` envelopes via state subscriptions).

## Open

See `ledgers/OPEN-ITEMS.md` — engine-related questions:

- `oq-effect-pipeline` — effect-level orchestration surface (when each effect starts, whether effects nest / batch / stack). Per ADR-0006 the customizable layer where game styles diverge; design ongoing in `spikes/effect-pipeline.md`.
- `oq-engine-builder` — service composition (where `IModifierFactory`, deterministic ref/id generators, per-kind registries are constructed, registered, exposed). Pin during the engine implementation pass.
- `oq-gamerules-composition` — module composition contract for `GameRulesRuntime<T>` (provider/policy ordering, enable-disable, conflict resolution). Service surface supports it mechanically; pin when first composed-rules game forces it.
- **MTG SBA ordering** — the framework's evaluation loop diverges from MTG-strict state-based-action ordering by design. If a future game needs MTG semantics, decide whether to add a pre-after-fold evaluator hook or document the workaround (encode SBAs as state-based checks inside `Evaluate`). Re-open trigger: first game forcing the issue.
- **`HistoryEnvelope` typed definition** — concrete C# record (fields: `envelopeId`, `originCommandId`, `actor`, `entries[]`) is pseudocode. Pin when replay or multiplayer transport is designed.
- **`EngineState` surface** — whether `IsRunning`/`CurrentCommand` are the right fields, or whether additional engine-internal state (pending queue snapshot, awaiter state) belongs here. Deferred to engine implementation pass.
- **Empty-actors mode** — whether `MatchSetup.Actors` may be empty (purely simulation-driven games). Defer until a real game forces it.
- **Networking-driven `IRandom`** — `MatchSetup.Seed` is currently `int`. When networking lands, `IRandom` (or richer determinism contract) replaces this slot.
- **Async-effect cancellation on match end** — when `EndMatchCommand` resolves while an effect is mid-`await`, the awaiting effect is still alive. Whether to thread a `CancellationToken` through `IHandlerContext` / `IEffectScope` is an effect-flow concern.
- **Multi-winner / team outcomes** — `MatchOutcome.Win` is single-actor. Add `TeamWin` (or similar) when a co-op / team game forces it.
- `bl-actor-ownership-reverse-index` — `OwnershipState` is one-way today (`Reference → IActor`); reverse direction (`IActor → owned References`) deferred.
- **Slice-level subscription teardown** — `SubscribeAllReferences<T>` / `SubscribeAny` lack symmetric `Unsubscribe` overloads.
- **`IAggregateProvider` registration.** `Store.RegisterAggregate` ships in Scaffold; framework hasn't pinned a use case (computed read-only views over multiple slices).
