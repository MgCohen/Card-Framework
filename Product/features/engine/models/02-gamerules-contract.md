---
type: model
status: settled
references: [features/engine/models/01-evaluation-loop.md, features/engine/models/05-game-factory.md, decisions/0006-two-pipeline-architecture.md]
tags: [#engine, #gamerules, #contract]
---

# GameRules — Definition + Runtime

This document locks the contract for the game-specific decision layer plugged into the engine. That layer is split into two types — `GameRulesDefinition` (pure serializable data) and `GameRulesRuntime<TDefinition>` (stateful runtime brain). The engine itself (loop, queue, pipeline wiring) is framework code described in [features/engine/models/01-evaluation-loop.md](features/engine/models/01-evaluation-loop.md). This doc is for the game author who has to implement both halves for a specific game.

Builds on:

- [features/engine/models/01-evaluation-loop.md](features/engine/models/01-evaluation-loop.md) — engine loop, drain cycle, pipeline wiring
- [decisions/0006-two-pipeline-architecture.md](decisions/0006-two-pipeline-architecture.md) — three-level model (commands / effects / triggers ⊆ effects); command pipeline framework-owned, effect orchestration game-owned
- [features/triggers/models/01-trigger-fold.md](features/triggers/models/01-trigger-fold.md) — trigger fold, OnBefore/OnAfter, subscription
- [features/effects/models/01-async-dispatch.md](features/effects/models/01-async-dispatch.md) — async layer hierarchy, dispatch paths
- [features/effects/models/02-replacement-prevention.md](features/effects/models/02-replacement-prevention.md) — `FilterTriggers`, `ITriggerPipeline`, replacement/prevention semantics
- [support/samples/pile-counter.md](support/samples/pile-counter.md) — the working sample game; drives this contract's shape
- [features/presentation/models/01-presentation-loop.md](features/presentation/models/01-presentation-loop.md) — UI loop, snapshot in state, four validation layers
- [features/engine/models/03-snapshot-pipeline.md](features/engine/models/03-snapshot-pipeline.md) — service-driven snapshot publication

---

## 1. What The Rules Layer Is

The rules layer is the **game's decision layer**. The framework engine drives the loop; the rules layer answers two questions during that loop:

1. *Which registered triggers are eligible for this command?* — trigger availability
2. *Given the state after this action, what runs next?* — state-based follow-up (the evaluator)

It is **not** a place for command-specific legality, action handlers, ability logic, state mutation, or async work. See §7 for the explicit anti-scope.

Per [ADR-0006](decisions/0006-two-pipeline-architecture.md), the runtime does **not** own command-queue routing — that's framework FIFO. Effect-level orchestration (when each effect starts, whether effects nest / batch / stack) is a forthcoming surface; tracked as `oq-effect-pipeline`. The current runtime contract reflects only what's settled today.

### The two-type split

The layer is two cooperating types:

- **`GameRulesDefinition`** — pure `[SerializeField]`-bearing data class (non-`UnityObject`). Carries authored config (starting life, hand size, format-specific knobs). Asset-friendly, serializable, replay/save/network-safe. One per game-mode variant (Standard, Commander, Pauper).
- **`GameRulesRuntime<TDefinition>`** — stateful runtime class. Holds framework services (`IStateScope`, `IAvailableActions`), game-specific subsystems (e.g. `IStackSystem`, `IDeckSystem` for an MTG-like), and a typed `Definition` of `TDefinition`. Implements the contract methods. DI-resolved per match.

The factory pairs them via DI container open-generic resolution: a game installer registers `GameRulesRuntime<MagicRulesDefinition> → MagicRulesRuntime`; the factory takes the definition out of `GameDefinition.Rules` and resolves the runtime for it. See [`engine/05-game-factory.md`](features/engine/models/05-game-factory.md) for the construction flow.

### Position in the system

| Layer | Owner | Lives in |
|---|---|---|
| Engine loop, queue, drain | Framework | `GameEngine` (engine-private FIFO) |
| Pipeline (handler lookup → validate → before fold → handle → after fold → envelope) | Framework | `ICommandPipeline` |
| Trigger ordering + fold execution | Framework (swappable) | `ITriggerPipeline` |
| **Command-specific legality** + state mutation | **Per-action** | **`ICommandHandler.Validate` / `Handle`** |
| Trigger reaction logic | Per-ability | `AbilityDefinition.Register` |
| **Game-wide decisions** | **Game** | **`GameRulesRuntime<T>`** (with `GameRulesDefinition` carrying authored config) |
| State mutators (consume payloads dispatched by handlers, write slices) | Per-payload | Scaffold `Mutator<TState, TPayload>` |
| Player input → command resolution | Game | `IIntentMapper` |
| UI affordance projection | Game (presentation) | `AffordanceMap` |
| Match lifecycle (start, end, disposal) | Framework | `GameEngine` per [`engine/06-match-lifecycle.md`](features/engine/models/06-match-lifecycle.md) |
| Effect-level orchestration (when each effect starts) | **Game** (forthcoming) | `GameRulesRuntime<T>` (separate design pass; tracked as `oq-effect-pipeline`) |
| Broader session lifecycle (pause, concede, disconnect, multi-match) | Game | External controller wrapping `GameEngine` (out of scope here) |

---

## 2. The Interface

The rules layer is two types:

```csharp
// Pure data. Asset-friendly. Replay/save/network-safe.
[Serializable]
public abstract class GameRulesDefinition
{
    // Concrete subclasses add [SerializeField] config fields.
}

// Stateful runtime brain. Generic on its definition type so concrete
// runtimes get a typed Definition without casting. DI-resolved per match.
public abstract class GameRulesRuntime<TDefinition>
    where TDefinition : GameRulesDefinition
{
    protected TDefinition       Definition { get; }
    protected IStateScope       State      { get; }   // Scaffold; read-only
    protected IAvailableActions Actions    { get; }

    protected GameRulesRuntime(
        TDefinition       definition,
        IStateScope       state,
        IAvailableActions actions)
    {
        Definition = definition;
        State      = state;
        Actions    = actions;
    }

    // 1. Trigger availability. Filters registered candidates for a given command and phase.
    //    Default: identity. Override for trigger-bearing games.
    public virtual IReadOnlyList<TriggerMatch> FilterTriggers(
        IReadOnlyList<TriggerMatch> candidates,
        GameCommand                 command,
        TriggerPhase                phase)
        => candidates;

    // 2. Evaluator. Reads state after each action; returns follow-up actions.
    //    Default: empty. Override for games that need state-based follow-ups.
    //    May call Actions.Set(...) to publish a snapshot at any moment it deems appropriate
    //    (typically when its own follow-ups are exhausted and the queue is about to empty).
    public virtual IReadOnlyList<GameAction> Evaluate(
        GameAction    justExecuted,
        CommandResult result)
        => Array.Empty<GameAction>();

    // Effect-pipeline orchestration surface — separate design pass; tracked as oq-effect-pipeline.
}
```

Two methods on the contract today; both have sensible defaults so games override only what they need. The effect-pipeline orchestration surface (when an effect starts, nesting/batching policy) is intentionally absent — that's the next design pass.

Concrete runtimes extend the typed base and add their own subsystems:

```csharp
public sealed class MagicRulesRuntime : GameRulesRuntime<MagicRulesDefinition>
{
    private readonly IStackSystem _stack;
    private readonly IDeckSystem  _decks;

    public MagicRulesRuntime(
        MagicRulesDefinition definition,
        IStateScope          state,
        IAvailableActions    actions,
        IStackSystem         stack,
        IDeckSystem          decks)
        : base(definition, state, actions)
    {
        _stack = stack;
        _decks = decks;
    }

    public override IReadOnlyList<TriggerMatch> FilterTriggers(
        IReadOnlyList<TriggerMatch> candidates,
        GameCommand                 command,
        TriggerPhase                phase)
        => /* … */;

    public override IReadOnlyList<GameAction> Evaluate(
        GameAction    justExecuted,
        CommandResult result)
        => /* … */;
}
```

### Injected services

| Service | Purpose |
|---|---|
| `TDefinition Definition` | The serializable data object selected for this match (`GameDefinition.Rules`). Read config knobs through it. Immutable for the runtime's lifetime. |
| `IStateScope State` | Scaffold read-only state surface. Read sliced state via `State.Get<TState>()`. Read fresh per access. See [04-effect-flow §4.1](features/effects/models/04-effect-flow.md). |
| `IAvailableActions Actions` | Snapshot writer — `Set` / `Append` / `Clear`. The runtime (and effects) call this to publish; the engine never auto-publishes. See [doc 29 §2](features/engine/models/03-snapshot-pipeline.md). |
| Game-specific subsystems | Concrete runtimes inject whatever else they need (`IStackSystem`, `IDeckSystem`, …) via their own constructor. Registered in the game's installer. The framework knows nothing about them. |

`IIntakeQueue` is **not** in the injected services list — per [ADR-0006](decisions/0006-two-pipeline-architecture.md), the command queue is engine-private FIFO with no runtime service surface for routing.

### Contract guarantees

- **All contract methods are synchronous.** No `Task`, no awaits. The runtime is a pure decision layer; async work belongs to handlers, effects, and `ITriggerPipeline`.
- **Side effects flow through narrow capability services.** `FilterTriggers` is a pure observer (returns data, never side-effects). `Evaluate` is observer-shaped *except* it may opt to call `Actions.Set(...)` to publish the available-actions snapshot at any moment it deems appropriate. **The runtime never holds `GameEngine`** — only the services injected via its constructor.
- **The runtime never mutates state slices directly.** State changes happen through dispatched `GameAction`s whose handlers call `ctx.Store.Execute(payload)`, which fans out to the registered Scaffold `Mutator<TState, TPayload>`s ([04-effect-flow §4.2](features/effects/models/04-effect-flow.md)). `Actions.Set` writes the `AvailableActionsState` slice through an internal mutator — that's the only slice the runtime writes, and it goes through the same pipeline.
- **The runtime holds zero gameplay state.** Per ADR-0002 (refreshed). Service handles, lookup tables, cached evaluators that are deterministic functions of inputs are fine — *gameplay memory* (last-move, phase, stash buffers, …) lives as a state slice in the Scaffold `Store`. Game-specific subsystems injected into the runtime (e.g. `IStackSystem`) follow the same rule: they are *behavior*, not gameplay memory.
- **The definition is immutable for the runtime's lifetime.** It is constructor-injected once and never reassigned. Mutable per-match knobs belong in slices.
- **Command-specific legality is not the runtime's concern.** Each `ICommandHandler<TCommand,TResult>.Validate` owns the legality of its own command. The runtime is generic across game logic.
- **No method holds onto its arguments.** `TriggerMatch` lists and `command` / `result` parameters are valid only for the call. (Injected services and the definition *are* held — that's their purpose.)
- **Per-match lifecycle.** The runtime is constructed at `factory.Build()` and lives as long as its engine. The engine owns disposal of everything under its lifecycle.
- **The runtime does not own command-queue routing.** Per ADR-0006, the engine queue is framework-private FIFO. Routing differentiation that earlier drafts attempted to express through the runtime (instant-speed, stash, cancel-sibling) is either expressed at framework level via `IEffectScope` per-command primitives (modify/cancel/replace/dispatch), or — for effect-level batching/stacking — through game-state zones plus `Evaluate` watching them. The latter is the forthcoming effect-pipeline surface.
- **Methods may be called concurrently from different drain cycles only if the engine guarantees it.** Today the engine serializes drain cycles, so the runtime does not need to be thread-safe.

---

## 3. `FilterTriggers` — Trigger Availability

Called by the pipeline once per command, twice per execution (one before-fold, one after-fold). Receives all registered `TriggerMatch` entries for that command type and returns the eligible subset.

```csharp
public enum TriggerPhase { Before, After }
```

The split between availability (the runtime) and ordering (`ITriggerPipeline`) is deliberate — see [doc 23 §3–4](features/effects/models/02-replacement-prevention.md). `GameRulesRuntime` says *which* triggers fire; `ITriggerPipeline` says *in what order*.

### Why the phase parameter

Some games want different availability rules before vs. after:

- During a "no replacements" phase, suppress before-fold triggers but keep after-fold reactions.
- During spell resolution, suppress after-fold triggers from the resolving permanent itself.

A single filter that cannot tell which phase it is in cannot express these without inspecting the command. Adding the enum costs nothing and makes the rule readable.

### Default implementation

Return the candidates unchanged. The base class ships this as a virtual default — games with no eligibility logic don't need to override it.

### Example uses

- **Phase gating** — no triggers fire during setup
- **Mana abilities** — skip the trigger pipeline entirely for speed-mana costs
- **Stack restriction** — triggers suppressed while a specific resolution lock is active
- **Target validation** — a trigger requires a legal target to exist before it is considered applicable
- **Phase-asymmetric filtering** — replacements off during cleanup but reactions still fire

---

## 4. `Evaluate` — State-Based Follow-Up

Called after every `GameAction` finishes executing. Reads current state through `State.Get<TState>()`, returns 0..N actions to enqueue next.

The single most important point about `Evaluate`: **it reads state, not the just-executed command type.** If the rule is "any card sitting in PlayArea must be executed," that rule fires the same way whether the card got there via `PlayCardCommand`, a copy effect, an opponent's trigger, or a token spawn. State-based evaluation handles all sources uniformly.

```csharp
public override IReadOnlyList<GameAction> Evaluate(
    GameAction    justExecuted,
    CommandResult result)
{
    var playArea = State.Get<ZoneContentsState>(WellKnownZones.PlayArea);
    if (playArea.HasCards)
        return new[] { new ExecuteCardCommand { Source = playArea.TopCard, ... } };

    return Array.Empty<GameAction>();
}
```

`justExecuted` and `result` are passed for logging and tie-breaking purposes — not as the primary lens. If your `Evaluate` has a `switch` on `justExecuted`'s type, that's a smell: the rule is probably state-based and the type-match is just shorthand that misses other paths into the same state.

`Evaluate` is *not* the place to mutate state slots. State updates flow through state mutators reacting to commands ([04-effect-flow §4.2](features/effects/models/04-effect-flow.md)). `Evaluate` reads state and decides what runs next; it never writes — except to publish an available-actions snapshot through `Actions.Set(...)` (which itself goes through the framework's payload pipeline).

### Drain-end logic folds into `Evaluate`

Per [ADR-0006](decisions/0006-two-pipeline-architecture.md), there is no `OnQueueDrained` callback. The runtime detects "queue is about to empty" by observing state plus its own follow-up emptiness:

```csharp
public override IReadOnlyList<GameAction> Evaluate(GameAction action, CommandResult result)
{
    var stateBased = ComputeStateBasedFollowUps();
    if (stateBased.Any()) return stateBased;

    // No state-based follow-ups. Drain is about to end at this point —
    // publish snapshot and return any phase-advance / end-match actions.
    Actions.Set(BuildSnapshot());
    return MaybePhaseAdvanceOrEndMatchActions();
}
```

The runtime can publish a snapshot at any moment by calling `Actions.Set(...)` — this is the single publication path. Effects mid-execution may also publish for target-picker / choose-one prompts. See [`engine/03-snapshot-pipeline.md`](features/engine/models/03-snapshot-pipeline.md).

### Ordering nuance vs. MTG state-based actions

In MTG, state-based actions check **before** triggers go onto the stack. In this framework the order is:

1. Action handler runs
2. After-fold runs (per-instance triggers)
3. Envelope closes
4. `Evaluate` runs

So in the framework, after-triggers fire *on the originating action* before `Evaluate` sees the post-action state. A game wanting strict MTG SBA ordering would need to encode death triggers etc. as state-based checks inside `Evaluate` rather than as `OnAfter<DamageAction>` triggers — or accept the framework's ordering. This is a known semantic difference, not a bug.

---

## 5. Where Command Routing Lives (It's Not Here)

Earlier drafts of this contract included an `OnIncomingCommand` (a.k.a. `Route`) method intended to let the runtime gate intake and route commands at the queue level. Per [ADR-0006](decisions/0006-two-pipeline-architecture.md), that method is deleted. Reasoning:

- **Command-queue routing isn't what games actually customize.** Walking through Stacklands, Hearthstone, and MTG concretely showed that the framework FIFO is sufficient for command-level scheduling. What differs across game styles is *effect-level* orchestration (when an effect starts, whether effects nest / batch / stack).
- **Per-command primitives are framework-level.** Modify, cancel, replace, dispatch — these operate on the in-flight command via `IEffectScope` during the trigger fold. They don't need a runtime hook.
- **Pause/ended gating moved to the lifecycle gate.** Framework checks `EngineState.Lifecycle` at `engine.Dispatch` and rejects pre-`StartMatch`/post-`FinishMatch` dispatches uniformly. See [`engine/06-match-lifecycle.md`](features/engine/models/06-match-lifecycle.md) §7.
- **Game-flow legality lives in `pipeline.Validate`.** Handler-side validation (`handler.Validate`) catches command-specific legality.

The forthcoming effect-pipeline surface is where game-level scheduling decisions live. Until it lands, MTG-style stack semantics is expressible via game-state zones (the stack is a `ZoneContentsState`) plus `Evaluate` watching that zone and dispatching `ResolveTopOfStack`-style follow-ups.

---

## 6. The Three Rejection Paths — When To Use Which

This table is the canonical reference for command rejection across the framework.

| Need | Path | Where | Sees | Owner |
|---|---|---|---|---|
| "Don't let the player attempt this in UI" | **AvailableActions** | Presentation, before any dispatch | `AvailableActionsState.Snapshot` | Game UI / `AffordanceMap` |
| "Reject because the engine isn't accepting commands" | **Lifecycle gate** | Engine `Dispatch` (framework) | `EngineState.Lifecycle` | Framework |
| "Reject because this specific command is illegal" | **`Handler.Validate` Reject** | Engine intake (handler) | The typed `TCommand` + `IStateScope` | `ICommandHandler<TCommand,_>` |
| "Cancel this mid-execution based on a card effect" | **Before-fold `Cancel`** | Inside the pipeline | The constructed `GameAction`, ability `Context` | `OnBefore<T>` trigger |

### Order at intake

The engine's `Dispatch` runs the lifecycle gate first (cheap, framework-only), then `pipeline.Validate` (resolves handler, delegates to `handler.Validate`). Either can reject; the engine surfaces a single `bool + reason` to the caller via the try-get pattern (`bool Dispatch(GameAction action, out string? reason)`).

### Rule of thumb

**The earliest path with enough information wins.** Pushing a check upstream means fewer wasted dispatches, less duplicated logic, and clearer UI:

- If the UI can know it's illegal before submitting → it shouldn't appear in the snapshot, and `Validate` doesn't even need to run.
- If the engine isn't ready (`NotStarted` / `Ended`) → the lifecycle gate rejects.
- If the command is malformed against current state → `Validate` rejects.
- If a card effect must cancel mid-resolution → before-fold cancel.

### Single source of truth

`Handler.Validate` is the *authoritative* legality check, run at dispatch. The snapshot is the runtime's best-effort approximation built from per-template eligibility. The UI's `AffordanceMap` is a projection of the snapshot. The authoritative check happens at dispatch — if the snapshot is approximate, dispatch catches stale bindings and surfaces the rejection (toast). See [doc 29 §2](features/engine/models/03-snapshot-pipeline.md).

---

## 7. What Does NOT Belong In The Runtime

Concrete anti-scope. If you're tempted to put one of these in `GameRulesRuntime`, look elsewhere:

| Don't put here | Put it in |
|---|---|
| **Command-specific legality** ("can this `MoveCard` run right now?") | `ICommandHandler<TCommand,_>.Validate` |
| **Gameplay state** (last-move memory, stash buffers, phase counters) | Scaffold state slices in the `Store` (per ADR-0002, runtime holds zero gameplay state) |
| **Authored configuration** (starting life, hand size, format knobs) | `GameRulesDefinition` `[SerializeField]` fields, read through `Definition.X` |
| **Command-queue routing** (where this command goes in the queue) | Framework (engine FIFO; per ADR-0006). For effect-level batching/stacking, use a game-state zone plus `Evaluate` watching it. |
| Action handler logic ("how does damage actually resolve") | `ICommandHandler<TCommand,_>.Handle` |
| State mutation reacting to a command | Scaffold `Mutator<TState, TPayload>` registered against the payload the handler dispatches via `ctx.Store.Execute(payload)` |
| Trigger reaction logic ("when this card is damaged, draw") | `AbilityDefinition.Register` on the card |
| Async / awaits / player-input prompts | Effects (orchestrators) and decision actions |
| Game session lifecycle (start, end, pause, concede, disconnect) | Separate `IGameSession` layer (out of scope) |
| Trigger ordering | `ITriggerPipeline` |
| Player input → command resolution | `IIntentMapper` |
| UI affordances | Presentation layer; `AffordanceMap` projects from `AvailableActionsState` |

The runtime is purely for **game-wide flow decisions during play**: which triggers are eligible to fire, what runs next based on state, and (forthcoming) effect-level orchestration. Any logic that belongs to a specific card, action, or ability lives on that card, action, or ability. Any authored knob lives on the `GameRulesDefinition`.

### The "zero gameplay state" rule

The runtime may hold *configuration* — rule modules, lookup tables, cached evaluators that are deterministic functions of inputs — and the immutable `Definition` reference. It must *not* hold gameplay memory. Test: if you reset the engine and replay all commands against a fresh `GameState`, does the runtime produce identical outputs without any of its own state being touched? It should. If a private field on the runtime influences an `Evaluate` output, that field belongs in `GameState` instead.

Engine-internal plumbing (the pending command queue, drain-cycle running flag) is **not** gameplay state — that's framework wiring living on the engine, not on the runtime. Per the ADR-0002 amendment.

---

## 8. Composition

A real game's runtime will grow large — phase rules, SBAs, ordering rules, available-actions computation. The base type intentionally does not bake composition in; that's an authoring concern, not a framework primitive.

The `IAvailableActions` service surface (`Set` / `Append` / `Clear`, see [doc 29 §2](features/engine/models/03-snapshot-pipeline.md)) was designed to support module composition mechanically — multiple modules can `Append` contributions and an override module can `Set` to replace. The composition *contract* (provider/policy ordering, enable-disable, conflict resolution) is deliberately undesigned for now; pin when the first composed-rules game forces it. A future modular-rule tier (likely double-generic on definition + module) will compose alongside the central runtime — see `oq-gamerules-composition` in OPEN-ITEMS.

For games with one rule module, extend `GameRulesRuntime<TDefinition>` directly. The constructor takes the definition + two framework services + any game-specific subsystems; the contract methods do the work.

---

## 9. Pile Counter Example

```csharp
public sealed class PileCounterRulesRuntime : GameRulesRuntime<PileCounterRulesDefinition>
{
    private readonly IActor _player;

    public PileCounterRulesRuntime(
        PileCounterRulesDefinition definition,
        IStateScope                state,
        IAvailableActions          actions,
        IReadOnlyList<IActor>      actors)
        : base(definition, state, actions)
    {
        _player = actors.Single();
    }

    // FilterTriggers omitted — base class ships a virtual identity default.

    public override IReadOnlyList<GameAction> Evaluate(GameAction _, CommandResult __)
    {
        var piles = State.Get<PilesState>().Piles.Values;

        // Win check: all piles match their target?
        if (piles.All(p => p.Score == p.Target))
            return new[] { new EndMatchCommand(new MatchOutcome.Win(_player)) };

        // No state-based follow-ups. Publish snapshot at this moment.
        Actions.Set(BuildAvailableActionsSnapshot(piles));
        return Array.Empty<GameAction>();
    }

    private AvailableActionsSnapshot BuildAvailableActionsSnapshot(IEnumerable<Pile> piles)
    {
        var lastMove = State.Get<LastMoveState>().Move;
        var templates = new List<ActionTemplate>();

        foreach (var pile in piles.Where(p => p.Cards.Count > 0))
        {
            var top = pile.TopCard;
            if (lastMove?.Card == top.Id) continue;   // per-template suppression

            var destinations = piles
                .Where(p => p.Id != pile.Id)
                .Select(p => (PileId)p.Id)
                .ToList();

            templates.Add(new MoveCardTemplate(top.Id, destinations));
        }

        return new AvailableActionsSnapshot(templates);
    }
}
```

Two observations:

- **`OnIncomingCommand` is gone.** The framework FIFO handles command queueing; no per-command routing override needed.
- **Snapshot publication folds into `Evaluate`.** The runtime decides "now's a good moment" by detecting that its own state-based follow-ups are empty. No separate callback.

See [`support/samples/pile-counter.md`](support/samples/pile-counter.md) for the full sample including handlers, mutators, and bootstrap.

---

## 10. Open Items

- **Effect pipeline shape** — the substantive open design surface. How does the runtime express effect-level orchestration (when each effect starts, whether nesting / batching / stacking)? Per ADR-0006, separate from command routing. Tracked as `oq-effect-pipeline`.
- **`GameState` slice catalog** — concrete catalog of slices (per-zone `PileState`, `LastMoveState`, `TurnState`, `AvailableActionsState`, framework-owned `ZoneContentsState` / `CardMapState` / `OwnershipState` / `EntityState`, etc.) the rule reads through `IStateScope`. Pin per-game; framework-shipped catalog stays minimal. See `engine/04-gamestate-slices.md`.
- **Action pipeline cleanup** — handler lookup, validate, execute, trigger folds need to be drawn explicitly in one place rather than scattered across docs.
- **`IHandlerRegistry` shape** — final shape (typed lookup, error semantics, registration) resolves with the action-pipeline doc.
- **MTG SBA ordering** — if a future game needs MTG-strict ordering, decide whether to add a pre-after-fold evaluator hook or document the workaround (encode SBAs inside `Evaluate` and accept the post-after-fold timing).
- **`GameRulesRuntime` composition** — module composition contract (provider/policy ordering, enable-disable). Service surface supports it mechanically; pin when first composed game forces it. Tracked as `oq-gamerules-composition`.

### Resolved

- ~~Engine start/finish-match lifecycle~~ — resolved in [`engine/06-match-lifecycle.md`](features/engine/models/06-match-lifecycle.md). `GameEngine` exposes `StartMatch` / `FinishMatch` / `Lifecycle` / `Dispose` via the try-get pattern; transitions are framework-owned `StartMatchCommand` / `EndMatchCommand`.
- ~~`GameRules` runtime/definition reconciliation~~ — split into `GameRulesDefinition` (data) + `GameRulesRuntime<TDefinition>` (runtime); factory uses DI container open-generic resolution. See §1 and §2 above.
- ~~`FilterTriggers` default impl~~ — base ships a virtual identity default. See §3.
- ~~`AvailableActionsSnapshot` ownership~~ — lives in `AvailableActionsState`; runtime writes through `Actions.Set` (doc 29 §2). Engine never auto-publishes.
- ~~`OnIncomingCommand` (a.k.a. `Route`)~~ — deleted per ADR-0006. Command routing is framework-FIFO; effect-level orchestration is the forthcoming surface.
- ~~`OnQueueDrained`~~ — deleted per ADR-0006. Drain-end logic folds into `Evaluate`; snapshot publication is purely runtime-driven via `Actions.Set`.
- ~~`IIntakeQueue`~~ — deleted per ADR-0006. Engine queue is framework-private FIFO with no runtime service.
- ~~`IntakeResult` / `IntakeContext` / `DispatchOrigin` / `DrainResult`~~ — deleted; subsumed by the try-get `Dispatch` pattern, the lifecycle gate, and `Evaluate`/`Actions.Set` direct paths.
- ~~State-pattern decision~~ — services injected at construction; methods take only ephemeral data. State is read through Scaffold `IStateScope.Get<TState>()`.
- ~~Effect-side service symmetry~~ — resolved by [04-effect-flow](features/effects/models/04-effect-flow.md). `IHandlerContext` carries `Store` + `Actions` + `Resolver`; `IEffectScope` carries `IStateScope` + `Dispatch` + `Cancel` + `Replace`.
- ~~Snapshot element type~~ — typed `ActionTemplate` per command type; carries `CommandType` and `Source` on the base ([doc 29 §3](features/engine/models/03-snapshot-pipeline.md)).
- ~~`oq-intake-queue`~~ — resolved by deletion (answered by ADR-0006).
- ~~`oq-stash-release`~~ — resolved by deletion. Stash mechanics are effect-level (game-state buffer in a zone, released via `Evaluate`-driven dispatch), not command-queue routing. Tracked under `oq-effect-pipeline` if needed.

---

## 11. Related

- [decisions/0006-two-pipeline-architecture.md](decisions/0006-two-pipeline-architecture.md) — the three-level model and command-vs-effect pipeline split
- [features/engine/models/01-evaluation-loop.md](features/engine/models/01-evaluation-loop.md) — the engine that drives the runtime
- [features/triggers/models/01-trigger-fold.md](features/triggers/models/01-trigger-fold.md) — trigger fold and subscription model
- [features/effects/models/01-async-dispatch.md](features/effects/models/01-async-dispatch.md) — async layer hierarchy
- [features/effects/models/02-replacement-prevention.md](features/effects/models/02-replacement-prevention.md) — `FilterTriggers`, replacement semantics
- [support/samples/pile-counter.md](support/samples/pile-counter.md) — sample game; concrete `GameRulesRuntime` + `GameRulesDefinition` implementation against this contract
- [features/presentation/models/01-presentation-loop.md](features/presentation/models/01-presentation-loop.md) — UI loop, snapshot-in-state, four validation layers
- [features/actions/models/02-availability-snapshot.md](features/actions/models/02-availability-snapshot.md) — snapshot shape (typed templates)
- [features/engine/models/03-snapshot-pipeline.md](features/engine/models/03-snapshot-pipeline.md) — service-driven snapshot publication; informs this doc's `Evaluate`-side snapshot publishing
- [ledgers/OPEN-ITEMS.md](ledgers/OPEN-ITEMS.md) — global open questions tracker
