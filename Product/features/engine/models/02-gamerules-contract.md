---
type: model
status: settled
references: [features/engine/models/01-evaluation-loop.md, features/engine/models/05-game-factory.md, features/triggers/models/01-trigger-fold.md, features/effects/models/02-replacement-prevention.md]
tags: [#engine, #gamerules, #contract]
---

# GameRules Contract — model

## What it is

The contract for the game-specific decision layer plugged into the engine — split into two cooperating types: **`GameRulesDefinition`** (pure `[SerializeField]`-bearing data, asset-friendly, replay/save/network-safe) and **`GameRulesRuntime<TDefinition>`** (stateful runtime brain holding framework services, game-specific subsystems, and a typed `Definition`). The runtime is DI-resolved per match via container open-generic resolution. The two contract methods today — `FilterTriggers` (which registered triggers are eligible) and `Evaluate` (state-based follow-up) — both have sensible defaults so games override only what they need. The effect-pipeline orchestration surface is intentionally absent — that's the next design pass (`oq-effect-pipeline`).

```csharp
[Serializable]
public abstract class GameRulesDefinition { /* [SerializeField] config fields on subclasses */ }

public abstract class GameRulesRuntime<TDefinition>
    where TDefinition : GameRulesDefinition
{
    protected TDefinition       Definition { get; }
    protected IStateScope       State      { get; }   // Scaffold; read-only
    protected IAvailableActions Actions    { get; }

    public virtual IReadOnlyList<TriggerMatch> FilterTriggers(
        IReadOnlyList<TriggerMatch> candidates,
        GameCommand                 command,
        TriggerPhase                phase) => candidates;

    public virtual IReadOnlyList<GameAction> Evaluate(
        GameAction    justExecuted,
        CommandResult result) => Array.Empty<GameAction>();
}
```

## Contracts

### What the rules layer is

The rules layer is the **game's decision layer**. The framework engine drives the loop; the rules layer answers two questions during that loop:

1. *Which registered triggers are eligible for this command?* — trigger availability
2. *Given the state after this action, what runs next?* — state-based follow-up (the evaluator)

It is **not** a place for command-specific legality, action handlers, ability logic, state mutation, or async work. See "Anti-scope" below for the explicit list.

Per [ADR-0006](Product/decisions/0006-two-pipeline-architecture.md), the runtime does **not** own command-queue routing — that's framework FIFO. Effect-level orchestration (when each effect starts, whether effects nest / batch / stack) is a forthcoming surface; tracked as `oq-effect-pipeline`. The current runtime contract reflects only what's settled today.

### The two-type split

- **`GameRulesDefinition`** — pure `[SerializeField]`-bearing data class (non-`UnityObject`). Carries authored config (starting life, hand size, format-specific knobs). Asset-friendly, serializable, replay/save/network-safe. One per game-mode variant (Standard, Commander, Pauper).
- **`GameRulesRuntime<TDefinition>`** — stateful runtime class. Holds framework services (`IStateScope`, `IAvailableActions`), game-specific subsystems (e.g. `IStackSystem`, `IDeckSystem` for an MTG-like), and a typed `Definition` of `TDefinition`. Implements the contract methods. DI-resolved per match.

The factory pairs them via DI container open-generic resolution: a game installer registers `GameRulesRuntime<MagicRulesDefinition> → MagicRulesRuntime`; the factory takes the definition out of `GameDefinition.Rules` and resolves the runtime for it. See [`engine/05-game-factory.md`](features/engine/models/05-game-factory.md) for the construction flow.

### Position in the system

| Layer | Owner | Lives in |
|---|---|---|
| Engine loop, queue, drain | Framework | `GameEngine` (engine-private FIFO) |
| Pipeline (handler lookup → validate → before-fold → handle → after-fold → envelope) | Framework | `ICommandPipeline` |
| Trigger ordering + fold execution | Framework (swappable) | `ITriggerPipeline` |
| **Command-specific legality** + state mutation | **Per-action** | **`ICommandHandler.Validate` / `Handle`** |
| Trigger reaction logic | Per-ability | `AbilityDefinition.Register` |
| **Game-wide decisions** | **Game** | **`GameRulesRuntime<T>`** (with `GameRulesDefinition` carrying authored config) |
| State mutators (consume payloads dispatched by handlers, write slices) | Per-payload | Scaffold `Mutator<TState, TPayload>` |
| Player input → command resolution | Game | `IIntentMapper` |
| UI affordance projection | Game (presentation) | `AffordanceMap` |
| Match lifecycle (start, end, disposal) | Framework | `GameEngine` per [`engine/01`](features/engine/models/01-evaluation-loop.md) (surface + gate) and [`engine/06`](features/engine/models/06-match-lifecycle.md) (lifecycle types) |
| Effect-level orchestration (when each effect starts) | **Game** (forthcoming) | `GameRulesRuntime<T>` (separate design pass; tracked as `oq-effect-pipeline`) |
| Broader session lifecycle (pause, concede, disconnect, multi-match) | Game | External controller wrapping `GameEngine` (out of scope here) |

### The interface

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
| `IStateScope State` | Scaffold read-only state surface. Read sliced state via `State.Get<TState>()`. Read fresh per access. See [`effects/04` §Service surfaces](features/effects/models/04-effect-flow.md). |
| `IAvailableActions Actions` | Snapshot writer — `Set` / `Append` / `Clear`. The runtime (and effects) call this to publish; the engine never auto-publishes. See [`engine/03 §The service-driven model`](features/engine/models/03-snapshot-pipeline.md). |
| Game-specific subsystems | Concrete runtimes inject whatever else they need (`IStackSystem`, `IDeckSystem`, …) via their own constructor. Registered in the game's installer. The framework knows nothing about them. |

`IIntakeQueue` is **not** in the injected services list — per [ADR-0006](Product/decisions/0006-two-pipeline-architecture.md), the command queue is engine-private FIFO with no runtime service surface for routing.

### Contract guarantees

- **All contract methods are synchronous.** No `Task`, no awaits. The runtime is a pure decision layer; async work belongs to handlers, effects, and `ITriggerPipeline`.
- **Side effects flow through narrow capability services.** `FilterTriggers` is a pure observer (returns data, never side-effects). `Evaluate` is observer-shaped *except* it may opt to call `Actions.Set(...)` to publish the available-actions snapshot at any moment it deems appropriate. **The runtime never holds `GameEngine`** — only the services injected via its constructor.
- **The runtime never mutates state slices directly.** State changes happen through dispatched `GameAction`s whose handlers call `ctx.Store.Execute(payload)`, which fans out to the registered Scaffold `Mutator<TState, TPayload>`s. `Actions.Set` writes the `AvailableActionsState` slice through an internal mutator — that's the only slice the runtime writes, and it goes through the same pipeline.
- **The runtime holds zero gameplay state.** Service handles, lookup tables, cached evaluators that are deterministic functions of inputs are fine — *gameplay memory* (last-move, phase, stash buffers, …) lives as a state slice in the Scaffold `Store`. Game-specific subsystems injected into the runtime (e.g. `IStackSystem`) follow the same rule: they are *behavior*, not gameplay memory.
- **The definition is immutable for the runtime's lifetime.** It is constructor-injected once and never reassigned. Mutable per-match knobs belong in slices.
- **Command-specific legality is not the runtime's concern.** Each `ICommandHandler<TCommand,TResult>.Validate` owns the legality of its own command. The runtime is generic across game logic.
- **No method holds onto its arguments.** `TriggerMatch` lists and `command` / `result` parameters are valid only for the call. (Injected services and the definition *are* held — that's their purpose.)
- **Per-match lifecycle.** The runtime is constructed at `factory.Build()` and lives as long as its engine. The engine owns disposal of everything under its lifecycle.
- **The runtime does not own command-queue routing.** Per ADR-0006, the engine queue is framework-private FIFO. Routing differentiation that earlier drafts attempted to express through the runtime (instant-speed, stash, cancel-sibling) is either expressed at framework level via `IEffectScope` per-command primitives (modify/cancel/replace/dispatch), or — for effect-level batching/stacking — through game-state zones plus `Evaluate` watching them. The latter is the forthcoming effect-pipeline surface.
- **Methods may be called concurrently from different drain cycles only if the engine guarantees it.** Today the engine serializes drain cycles, so the runtime does not need to be thread-safe.

### `FilterTriggers` — trigger availability

Called by the pipeline once per command, twice per execution (one before-fold, one after-fold). Receives all registered `TriggerMatch` entries for that command type and returns the eligible subset.

```csharp
public enum TriggerPhase { Before, After }
```

The split between availability (the runtime) and ordering (`ITriggerPipeline`) is deliberate — see [`effects/02 §ITriggerPipeline`](features/effects/models/02-replacement-prevention.md). `GameRulesRuntime` says *which* triggers fire; `ITriggerPipeline` says *in what order*.

**Why the phase parameter.** Some games want different availability rules before vs. after:

- During a "no replacements" phase, suppress before-fold triggers but keep after-fold reactions.
- During spell resolution, suppress after-fold triggers from the resolving permanent itself.

A single filter that cannot tell which phase it is in cannot express these without inspecting the command. Adding the enum costs nothing and makes the rule readable.

**Default implementation.** Return the candidates unchanged. The base class ships this as a virtual default — games with no eligibility logic don't need to override it.

**Example uses:**

- **Phase gating** — no triggers fire during setup
- **Mana abilities** — skip the trigger pipeline entirely for speed-mana costs
- **Stack restriction** — triggers suppressed while a specific resolution lock is active
- **Target validation** — a trigger requires a legal target to exist before it is considered applicable
- **Phase-asymmetric filtering** — replacements off during cleanup but reactions still fire

### `Evaluate` — state-based follow-up

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

`Evaluate` is *not* the place to mutate state slots. State updates flow through state mutators reacting to commands. `Evaluate` reads state and decides what runs next; it never writes — except to publish an available-actions snapshot through `Actions.Set(...)` (which itself goes through the framework's payload pipeline).

**Drain-end logic folds into `Evaluate`.** Per ADR-0006, there is no `OnQueueDrained` callback. The runtime detects "queue is about to empty" by observing state plus its own follow-up emptiness:

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

### Where command routing lives (it's not here)

Earlier drafts of this contract included an `OnIncomingCommand` (a.k.a. `Route`) method intended to let the runtime gate intake and route commands at the queue level. Per ADR-0006, that method is deleted. Reasoning:

- **Command-queue routing isn't what games actually customize.** Walking through Stacklands, Hearthstone, and MTG concretely showed that the framework FIFO is sufficient for command-level scheduling. What differs across game styles is *effect-level* orchestration (when an effect starts, whether effects nest / batch / stack).
- **Per-command primitives are framework-level.** Modify, cancel, replace, dispatch — these operate on the in-flight command via `IEffectScope` during the trigger fold. They don't need a runtime hook.
- **Pause/ended gating moved to the lifecycle gate.** Framework checks `EngineState.Lifecycle` at `engine.Dispatch` and rejects pre-`StartMatch`/post-`FinishMatch` dispatches uniformly. See [`engine/01 §Lifecycle gate`](features/engine/models/01-evaluation-loop.md).
- **Game-flow legality lives in `pipeline.Validate`.** Handler-side validation (`handler.Validate`) catches command-specific legality.

The forthcoming effect-pipeline surface is where game-level scheduling decisions live. Until it lands, MTG-style stack semantics is expressible via game-state zones (the stack is a `ZoneContentsState`) plus `Evaluate` watching that zone and dispatching `ResolveTopOfStack`-style follow-ups.

### Three rejection paths

This table is the canonical reference for command rejection across the framework.

| Need | Path | Where | Sees | Owner |
|---|---|---|---|---|
| "Don't let the player attempt this in UI" | **AvailableActions** | Presentation, before any dispatch | `AvailableActionsState.Snapshot` | Game UI / `AffordanceMap` |
| "Reject because the engine isn't accepting commands" | **Lifecycle gate** | Engine `Dispatch` (framework) | `EngineState.Lifecycle` | Framework |
| "Reject because this specific command is illegal" | **`Handler.Validate` Reject** | Engine intake (handler) | The typed `TCommand` + `IStateScope` | `ICommandHandler<TCommand,_>` |
| "Cancel this mid-execution based on a card effect" | **Before-fold `Cancel`** | Inside the pipeline | The constructed `GameAction`, ability `Context` | `OnBefore<T>` trigger |

The engine's `Dispatch` runs the lifecycle gate first (cheap, framework-only), then `pipeline.Validate` (resolves handler, delegates to `handler.Validate`). Either can reject; the engine surfaces a single `bool + reason` to the caller via the try-get pattern (`bool Dispatch(GameAction action, out string? reason)`).

**Rule of thumb: the earliest path with enough information wins.** Pushing a check upstream means fewer wasted dispatches, less duplicated logic, and clearer UI:

- If the UI can know it's illegal before submitting → it shouldn't appear in the snapshot, and `Validate` doesn't even need to run.
- If the engine isn't ready (`NotStarted` / `Ended`) → the lifecycle gate rejects.
- If the command is malformed against current state → `Validate` rejects.
- If a card effect must cancel mid-resolution → before-fold cancel.

`Handler.Validate` is the *authoritative* legality check, run at dispatch. The snapshot is the runtime's best-effort approximation built from per-template eligibility. The UI's `AffordanceMap` is a projection of the snapshot. The authoritative check happens at dispatch — if the snapshot is approximate, dispatch catches stale bindings and surfaces the rejection (toast).

### Anti-scope — what does NOT belong on the runtime

Concrete anti-scope. If you're tempted to put one of these in `GameRulesRuntime`, look elsewhere:

| Don't put here | Put it in |
|---|---|
| **Command-specific legality** ("can this `MoveCard` run right now?") | `ICommandHandler<TCommand,_>.Validate` |
| **Gameplay state** (last-move memory, stash buffers, phase counters) | Scaffold state slices in the `Store` (per "zero gameplay state" — see Q2 below) |
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

The runtime may hold *configuration* — rule modules, lookup tables, cached evaluators that are deterministic functions of inputs — and the immutable `Definition` reference. It must *not* hold gameplay memory. Test: if you reset the engine and replay all commands against a fresh `GameState`, does the runtime produce identical outputs without any of its own state being touched? It should. If a private field on the runtime influences an `Evaluate` output, that field belongs in `GameState` instead.

Engine-internal plumbing (the pending command queue, drain-cycle running flag) is **not** gameplay state — that's framework wiring living on the engine, not on the runtime. Per Q2 below.

### Composition

A real game's runtime will grow large — phase rules, SBAs, ordering rules, available-actions computation. The base type intentionally does not bake composition in; that's an authoring concern, not a framework primitive.

The `IAvailableActions` service surface (`Set` / `Append` / `Clear`, see [`engine/03 §The service-driven model`](features/engine/models/03-snapshot-pipeline.md)) was designed to support module composition mechanically — multiple modules can `Append` contributions and an override module can `Set` to replace. The composition *contract* (provider/policy ordering, enable-disable, conflict resolution) is deliberately undesigned for now; pin when the first composed-rules game forces it. A future modular-rule tier (likely double-generic on definition + module) will compose alongside the central runtime — see `oq-gamerules-composition`.

For games with one rule module, extend `GameRulesRuntime<TDefinition>` directly. The constructor takes the definition + two framework services + any game-specific subsystems; the contract methods do the work.

## Invariants

1. **Two-type split with container open-generic resolution.** `GameRulesDefinition` (data) + `GameRulesRuntime<TDefinition>` (runtime); paired via `container.Resolve(typeof(GameRulesRuntime<>).MakeGenericType(setup.Game.Rules.GetType()))`.
2. **Synchronous contract methods.** `FilterTriggers` and `Evaluate` are synchronous — no `Task`, no awaits. Async lives in handlers / effects / `ITriggerPipeline`.
3. **Zero gameplay state on the runtime.** Configuration is fine; gameplay memory is not. Engine-internal plumbing is not gameplay state.
4. **Runtime never mutates state directly.** Mutation flows through `Store.Execute(payload)` → `Mutator<TState, TPayload>`. The only "write" the runtime performs is `Actions.Set`, which itself goes through an internal mutator.
5. **Runtime never holds `GameEngine`.** Side effects flow through narrow capability services (`IStateScope`, `IAvailableActions`).
6. **Definition is immutable.** Constructor-injected once; never reassigned.
7. **No method holds onto its arguments.** Per-call data (candidates, command, result) is valid only for the call.
8. **Single-phase construction.** No `Initialize` hook; the constructor's return is when every dependency is bound.
9. **Three rejection paths** are the canonical legality model: AvailableActions (UI), Lifecycle gate (framework), `handler.Validate` (per-command), before-fold `Cancel` (mid-execution). The earliest path with enough information wins.

## Decisions

*n/a — no elevated ADR.* (All three engine ADRs that shape this Model — runtime-as-class-with-services, zero gameplay state, MTG-SBA known difference — are single-feature scope and folded as Q1–Q3 below.)

## Examples

### Pile Counter runtime

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

## Open follow-ups

- **Effect pipeline shape** — the substantive open design surface. How does the runtime express effect-level orchestration (when each effect starts, whether nesting / batching / stacking)? Per ADR-0006, separate from command routing. Tracked as `oq-effect-pipeline`.
- **`GameState` slice catalog** — concrete catalog of slices the rule reads through `IStateScope`. Pin per-game; framework-shipped catalog stays minimal. See [`engine/04-gamestate-slices.md`](features/engine/models/04-gamestate-slices.md).
- **`IHandlerRegistry` shape** — final shape (typed lookup, error semantics, registration) resolves with the action-pipeline doc.
- **MTG SBA ordering** — if a future game needs MTG-strict ordering, decide whether to add a pre-after-fold evaluator hook or document the workaround (encode SBAs inside `Evaluate` and accept the post-after-fold timing). See Q3 below.
- **`GameRulesRuntime` composition** — module composition contract (provider/policy ordering, enable-disable). Service surface supports it mechanically; pin when first composed game forces it. Tracked as `oq-gamerules-composition`.

---

*Below: provenance — three engine-feature decisions that shape this Model. All single-feature scope (no project-wide ADR applies; folded into Model provenance per §4.5 on 2026-05-08). Originals 2026-04-29; ADRs 0001 and 0002 carry 2026-05-07 amendments per the triggers + dispatch design pass.*

## Options considered

### Q1 — Rules layer shape (services injected vs. parameter-passed)

The rules brain originally drifted between "interface returning rich shapes" and "static class with global state." We needed a single shape that lets the rule call into framework services (state, snapshot writer) without inheriting framework state and without forcing the rule to wire wiring.

- **A — Pure interface (`IGameRules`).** Rejected — callers had to thread services through every method, bloating signatures and making evolution painful.
- **B — Static class with global services.** Rejected — makes per-game state impossible and breaks composition for multi-rule scenarios.
- **C — Abstract class with constructor-injected services (Scaffold `IStateScope`, `IAvailableActions`); methods take only ephemeral data.** **Chosen.**

**Amendment (2026-05-07).** The original Option C listed `IIntakeQueue` among the injected services. Per ADR-0006 (two-pipeline architecture), `IIntakeQueue` is deleted — the command queue is engine-private FIFO, with no runtime service surface for command-queue routing. The injected-services list is now `IStateScope` + `IAvailableActions`; the runtime contract methods are `FilterTriggers` and `Evaluate` (plus a forthcoming effect-pipeline surface from the next design pass). The shape — abstract class, services injected at construction, methods take only ephemeral data — is unchanged.

### Q2 — Zero gameplay state on the runtime

If `GameRulesRuntime` carries gameplay state, snapshot/restore breaks, replays diverge, and per-player views become impossible to disentangle. We need a clean separation between the rule (configuration) and gameplay memory (state slices in the Scaffold `Store`).

- **A — Allow rules to cache derived state for performance.** Rejected — cache invalidation becomes a global concern and breaks state-pattern guarantees.
- **B — Allow rules to hold transient buffers (e.g. queued events) as gameplay memory.** Rejected — gameplay buffers belong in state slices (snapshot-roundtrippable), not on the runtime. (Original wording cited `IIntakeQueue`; that type is gone per ADR-0006, but the principle stands.)
- **C — Zero gameplay state on rules; gameplay memory lives only in `GameState` (Scaffold States).** **Chosen.**

**Amendment (2026-05-07).** The original wording said "zero **game-affecting** state." Refining: the rule holds zero **gameplay** state — gameplay memory (last move, phase, score, etc.) lives in Scaffold state slices. Engine-internal plumbing (most notably the pending command queue) is **not** gameplay state; that's framework wiring, and where it physically lives (engine-private field, framework-owned slice, or runtime base class) is an engine-implementation detail, not a violation of this rule. Game-state buffers that *should* round-trip through snapshots (e.g., MTG stack zone, Hearthstone trigger queue) live as game-authored state slices, not as fields on the runtime.

### Q3 — MTG-strict state-based-action ordering

Magic: the Gathering's rules engine performs state-based actions (SBAs) in a tight loop *between* trigger fold and reaction dispatch. Our evaluation loop folds triggers and dispatches reactions in one phase. This is a real semantic difference and we need to acknowledge it rather than pretend it doesn't exist.

- **A — Add a pre-after-fold evaluator hook to the loop.** Deferred — until a game actually needs MTG-strict SBA ordering.
- **B — Rebuild the loop around an MTG-strict model.** Rejected — pessimizes the floor case and most games don't care.
- **C — Document as a known difference; implement a workaround (manual SBA pass before reactions) on a per-game basis.** **Chosen for now.**

The evaluation loop diverges from MTG-strict SBA ordering by design. Games that need MTG semantics implement the SBA pass themselves until the framework adds the hook. When a real game forces the issue, revisit option A.

In the framework the order is:

1. Action handler runs
2. After-fold runs (per-instance triggers)
3. Envelope closes
4. `Evaluate` runs

So in the framework, after-triggers fire *on the originating action* before `Evaluate` sees the post-action state. A game wanting strict MTG SBA ordering would need to encode death triggers etc. as state-based checks inside `Evaluate` rather than as `OnAfter<DamageAction>` triggers — or accept the framework's ordering. This is a known semantic difference, not a bug.

## Rationale

The three decisions converge on a coherent shape:

- **Q1 (services injected)** lets `FilterTriggers` and `Evaluate` shed boilerplate parameters and lets composition (multi-module rules) work mechanically — services pass through, not statically held.
- **Q2 (zero gameplay state)** is what makes deterministic replay, snapshot/restore, and per-player view derivation trivial. Reset the engine, replay the commands, get the same outputs — because gameplay memory is in the Store, not on the runtime.
- **Q3 (MTG SBA as known difference)** keeps the floor case simple. Adding a pre-after-fold evaluator hook would constrain the loop's shape for every game; documenting the difference and providing a workaround keeps the loop minimal until a real game forces the question.

The single-phase DI construction (no `Initialize` hook) falls out naturally from Q1: when the constructor returns, every dependency is bound. The "no caching, no buffers, no held engine reference" rule falls out from Q2.

## Consequences

- **Enables** `FilterTriggers` virtual-identity defaults and clean per-game overrides; `CompositeGameRules` later without redesigning the contract; deterministic replay, snapshot/restore, and trivial per-player view derivation; engine-internal plumbing (queue, flags, etc.) lives wherever the engine implementation prefers without violating zero-gameplay-state; a simpler default loop for the floor sample and Stacklands-class games.
- **Constrains** the framework to own service lifetime (the rule must not cache or mutate them in ways that break re-entry); any *gameplay* memo the rule wants to remember must round-trip through state; porting MTG-class rules requires a manual SBA pass (re-open trigger: first game forcing it).
