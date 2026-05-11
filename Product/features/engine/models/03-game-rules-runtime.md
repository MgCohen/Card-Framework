---
type: model
status: settled
references: [features/engine/models/01-game-engine.md, features/engine/models/02-game-rules-definition.md, features/engine/models/05-game-factory.md, features/engine/models/06-game-definition.md, features/engine/models/07-match-state.md, features/engine/models/08-turn-flow.md, features/engine/models/09-state-based-actions.md, features/actions/models/03-command-handler.md, features/actions/models/06-command-validator.md, features/effects/models/05-pending-effects.md, features/effects/models/06-request-system.md, features/triggers/models/01-trigger.md, decisions/0009-engine-as-fsm.md, decisions/0010-validator-handler-split.md, decisions/0011-effect-orchestration-dispatch-requests.md]
tags: [#engine, #gamerules, #runtime, #fsm]
---

# Game Rules Runtime — model

> **Amendment (2026-05-11) per [ADR-0013](Product/decisions/0013-engine-pipeline-orchestration.md).** `FilterTriggers` retires from the runtime surface. Trigger eligibility moves entirely into `Trigger<T>.Validate(cmd, scope)` (see [`triggers/01`](features/triggers/models/01-trigger.md)). The runtime no longer participates in the trigger fold's filtering pass. The runtime's remaining contract method is `Evaluate(action, result) → MatchState`. All inline references to `FilterTriggers` in this Model — interface declaration, examples, the dedicated section, the rejection-paths table, the Q3 SBA-ordering discussion — should be read as describing the pre-cascade shape; the canonical post-cascade behavior is "trigger filtering happens inside `TriggerSystem` via each `Trigger<T>.Validate`."

## What it is

The behavior half of the rules-layer split. **`GameRulesRuntime<TDefinition>`** is the stateful runtime brain plugged into the engine — generic on its [`GameRulesDefinition`](features/engine/models/02-game-rules-definition.md) so concrete runtimes get a typed `Definition` without casting. Holds framework services (`IStateScope`), the immutable `Definition`, an [`ITurnFlow`](features/engine/models/08-turn-flow.md), and a registered list of [`ISBA`](features/engine/models/09-state-based-actions.md)s. DI-resolved per match via container open-generic resolution. Two contract methods today — `FilterTriggers` (which registered triggers are eligible) and `Evaluate` (returns the new `MatchState`) — both have sensible defaults so games override only what they need. Per [ADR-0009](Product/decisions/0009-engine-as-fsm.md) the runtime IS the FSM evaluator: `Evaluate` returns a `MatchState`; the engine routes transitions through `TransitionStateCommand`.

```csharp
public abstract class GameRulesRuntime<TDefinition>
    where TDefinition : GameRulesDefinition
{
    protected TDefinition                  Definition        { get; }
    protected IStateScope                  State             { get; }
    protected abstract ITurnFlow           Flow              { get; }
    protected abstract IReadOnlyList<ISBA> StateBasedActions { get; }

    public virtual IReadOnlyList<TriggerMatch> FilterTriggers(
        IReadOnlyList<TriggerMatch> candidates,
        GameCommand                 command,
        TriggerPhase                phase) => candidates;

    public virtual MatchState Evaluate(
        GameAction    justExecuted,
        CommandResult result)
    {
        // SBAs preempt — cross-cutting overrides (game-over, legend rule, lethal damage).
        foreach (var sba in StateBasedActions)
            if (sba.Check(State) is { } over) return over;

        var current = State.Get<MatchStateSlice>().Current;

        // Drain-policy composition per ADR-0011:
        //   • Pause on open requests (RequestsSlice non-empty).
        //   • Stay stationary while pending effects exist; the engine will drain them.
        if (State.Get<RequestsSlice>().Open.Count > 0)         return current;
        if (!State.Get<PendingEffectsSlice>().Pending.IsEmpty) return current;

        return current.ShouldExit(justExecuted, result, State)
            ? Flow.NextAfter(current)
            : current;
    }
}
```

## Contracts

### What the rules layer is

The rules layer is the **game's decision layer**. The framework engine drives the loop; the rules layer answers two questions during that loop:

1. *Which registered triggers are eligible for this command?* — trigger availability.
2. *Given the state after this action, what `MatchState` runs next?* — FSM evaluation (default composition: SBAs preempt; then `current.ShouldExit` + `Flow.NextAfter`).

It is **not** a place for command-specific legality, action handlers, ability logic, state mutation, or async work. See "Anti-scope" below for the explicit list.

Per [ADR-0006](Product/decisions/0006-two-pipeline-architecture.md), the runtime does **not** own command-queue routing — that's framework FIFO. Per [ADR-0009](Product/decisions/0009-engine-as-fsm.md), the FSM evaluator shape replaces the previous `Evaluate → IReadOnlyList<GameAction>` contract. Per [ADR-0011](Product/decisions/0011-effect-orchestration-dispatch-requests.md), effect-level orchestration runs through `PendingEffectsSlice` (the engine drains; cast handlers and triggers push) — the default `Evaluate` body composes SBAs preempt → request pause → pending-effect pause → `ShouldExit` + Flow. Games override `Evaluate` for custom drain timing (MTG-style stack resolution gating drain on priority pass).

### Position in the system

| Layer | Owner | Lives in |
|---|---|---|
| Engine loop, queue, drain | Framework | `GameEngine` (engine-private FIFO) |
| Pipeline (handler lookup → validate → before-fold → handle → after-fold → envelope) | Framework | `ICommandPipeline` |
| Trigger ordering + fold execution | Framework (swappable) | `ITriggerPipeline` |
| **Command-specific legality** | **Per-command** | **`ICommandValidator<TCommand>`** ([`actions/06`](features/actions/models/06-command-validator.md)) — registered chain, short-circuit, framework defaults (`SessionGate`, `MatchStateActionGate`) run first |
| **State mutation reacting to a command** | **Per-action** | **`ICommandHandler.Handle`** ([`actions/03`](features/actions/models/03-command-handler.md)) — `GameCommandHandler<,>` base with `[Inject] Store` |
| Trigger reaction logic | Per-ability | `AbilityDefinition.Register` |
| **Game-wide FSM decisions** | **Game** | **`GameRulesRuntime<T>`** (this Model; with [`GameRulesDefinition`](features/engine/models/02-game-rules-definition.md) carrying authored config, [`ITurnFlow`](features/engine/models/08-turn-flow.md) carrying turn structure, and [`ISBA`](features/engine/models/09-state-based-actions.md)s carrying cross-cutting overrides) |
| State mutators (consume payloads dispatched by handlers, write slices) | Per-payload | Scaffold `Mutator<TState, TPayload>` |
| Per-state intent mapping | Game (presentation) | `MatchStateViewModel<TState>.Map` — [ADR-0012](Product/decisions/0012-presentation-per-state-view-models.md) |
| Match lifecycle (start, end, disposal) | Framework | `GameEngine` per [`engine/01`](features/engine/models/01-game-engine.md) (surface + gates + session lifecycle types) |
| Effect-level orchestration (when each effect starts) | **Game** (internals) / **Framework** (structural surface) | Structural surface landed in [ADR-0011](Product/decisions/0011-effect-orchestration-dispatch-requests.md) — async coroutines, `PendingEffectsSlice` queue, single async `Dispatch`. Internals (recipe shape, authoring vocab) remain open under `oq-effect-pipeline` and live on `GameRulesRuntime<T>`. |
| Broader session lifecycle (lobby, matchmaking, reconnect) | Host (per ADR-0009 D12 hybrid future) | External controller wrapping `GameEngine`; framework provides `Session` + `SessionChanged` hooks |

### The interface

```csharp
// Stateful runtime brain. Generic on its definition type so concrete
// runtimes get a typed Definition without casting. DI-resolved per match.
public abstract class GameRulesRuntime<TDefinition>
    where TDefinition : GameRulesDefinition
{
    protected TDefinition       Definition { get; }
    protected IStateScope       State      { get; }   // Scaffold; read-only

    // Game-supplied FSM structure. Both abstract — concrete runtimes wire these via DI.
    protected abstract ITurnFlow            Flow              { get; }
    protected abstract IReadOnlyList<ISBA>  StateBasedActions { get; }

    protected GameRulesRuntime(
        TDefinition definition,
        IStateScope state)
    {
        Definition = definition;
        State      = state;
    }

    // 1. Trigger availability. Filters registered candidates for a given command and phase.
    //    Default: identity. Override for trigger-bearing games.
    public virtual IReadOnlyList<TriggerMatch> FilterTriggers(
        IReadOnlyList<TriggerMatch> candidates,
        GameCommand                 command,
        TriggerPhase                phase)
        => candidates;

    // 2. FSM evaluator. Reads state after each action; returns the new MatchState.
    //    Default body: SBAs preempt; otherwise current.ShouldExit + Flow.NextAfter.
    //    Override only for custom orchestration (e.g., stack-resolution timing).
    public virtual MatchState Evaluate(
        GameAction    justExecuted,
        CommandResult result)
    {
        // SBAs preempt — cross-cutting overrides (game-over, legend rule, lethal damage).
        foreach (var sba in StateBasedActions)
            if (sba.Check(State) is { } over) return over;

        var current = State.Get<MatchStateSlice>().Current;

        // Drain-policy composition per ADR-0011:
        //   • Pause on open requests (RequestsSlice non-empty).
        //   • Stay stationary while pending effects exist; the engine will drain them.
        if (State.Get<RequestsSlice>().Open.Count > 0)         return current;
        if (!State.Get<PendingEffectsSlice>().Pending.IsEmpty) return current;

        return current.ShouldExit(justExecuted, result, State)
            ? Flow.NextAfter(current)
            : current;
    }
}
```

The Definition shape itself — `[Serializable]`-attributed pure data — is documented in [`engine/02-game-rules-definition.md`](features/engine/models/02-game-rules-definition.md). Concrete runtimes extend the typed base and wire their `ITurnFlow` + `IReadOnlyList<ISBA>`:

```csharp
public sealed class StandardMagicRulesRuntime : GameRulesRuntime<StandardMagicRulesDefinition>
{
    protected override ITurnFlow             Flow              { get; }
    protected override IReadOnlyList<ISBA>   StateBasedActions { get; }

    private readonly IStackSystem _stack;
    private readonly IDeckSystem  _decks;

    public StandardMagicRulesRuntime(
        StandardMagicRulesDefinition definition,
        IStateScope                  state,
        StandardMtgTurnFlow          flow,
        LethalDamageSBA              lethal,
        LegendRuleSBA                legend,
        EmptyLibrarySBA              drawFromEmpty,
        IStackSystem                 stack,
        IDeckSystem                  decks)
        : base(definition, state)
    {
        Flow              = flow;
        StateBasedActions = [lethal, legend, drawFromEmpty];
        _stack = stack;
        _decks = decks;
    }

    public override IReadOnlyList<TriggerMatch> FilterTriggers(
        IReadOnlyList<TriggerMatch> candidates,
        GameCommand                 command,
        TriggerPhase                phase)
        => /* … */;

    // Optional: override Evaluate for custom orchestration (stack-resolution timing).
}
```

### Injected services

| Service | Purpose |
|---|---|
| `TDefinition Definition` | The serializable data object selected for this match (`GameDefinition.Rules`). Read config knobs through it. Immutable for the runtime's lifetime. Shape lives in [`engine/02`](features/engine/models/02-game-rules-definition.md). |
| `IStateScope State` | Scaffold read-only state surface. Read sliced state via `State.Get<TState>()`. Read fresh per access. See [`effects/02-effect-scope.md`](features/effects/models/02-effect-scope.md). |
| `ITurnFlow Flow` (abstract; game-supplied) | Successor map for the FSM. See [`08-turn-flow.md`](features/engine/models/08-turn-flow.md). |
| `IReadOnlyList<ISBA> StateBasedActions` (abstract; game-supplied) | Cross-cutting state-driven overrides. See [`09-state-based-actions.md`](features/engine/models/09-state-based-actions.md). |
| Game-specific subsystems | Concrete runtimes inject whatever else they need (`IStackSystem`, `IDeckSystem`, …) via their own constructor. Registered in the game's installer. The framework knows nothing about them. |

`IAvailableActions` is **not** in the injected services list anymore — per [ADR-0009](Product/decisions/0009-engine-as-fsm.md), valid command types live on `MatchState.Actions` directly. The framework's intake gate consults that list; no separate snapshot service exists. `IIntakeQueue` is also gone (per ADR-0006); the command queue is engine-private FIFO with no runtime service surface for routing.

### Contract guarantees

- **All contract methods are synchronous.** No `Task`, no awaits. The runtime is a pure decision layer; async work belongs to handlers, effects, and `ITriggerPipeline`.
- **`Evaluate` is observer-shaped.** It reads state through the injected `IStateScope`, runs registered SBAs against state, optionally consults `current.ShouldExit`, and returns a `MatchState`. It does **not** mutate state and does **not** dispatch commands. State changes happen through dispatched `GameAction`s whose handlers call `ctx.Store.Execute(payload)`. Match-state changes happen through the engine routing the returned state through `TransitionStateCommand`.
- **The runtime never mutates state slices directly.** Per ADR-0009, the only "state write" the runtime triggers is by returning a different-typed `MatchState` from `Evaluate`, which the engine routes through `TransitionStateCommand` → `SetMatchStatePayload` → framework-internal mutator. All other writes flow from handlers.
- **The runtime holds zero gameplay state.** Service handles, lookup tables, cached evaluators that are deterministic functions of inputs are fine — *gameplay memory* (last-move, phase, stash buffers) lives as a state slice in the Scaffold `Store`. Game-specific subsystems injected into the runtime (e.g. `IStackSystem`) follow the same rule: they are *behavior*, not gameplay memory.
- **The definition is immutable for the runtime's lifetime.** It is constructor-injected once and never reassigned. Mutable per-match knobs belong in slices.
- **Command-specific legality is not the runtime's concern.** Each `ICommandValidator<TCommand>` (per [`actions/06`](features/actions/models/06-command-validator.md)) owns the legality of its own command — typically authored on the combined `ValidatedCommandHandler<TCommand, TResult>` base. The runtime is generic across game logic. The runtime's "what commands are valid" answer goes on `MatchState.Actions` — a list of types, not a per-command predicate.
- **No method holds onto its arguments.** `TriggerMatch` lists and `command` / `result` parameters are valid only for the call. (Injected services and the definition *are* held — that's their purpose.)
- **Per-match lifecycle.** The runtime is constructed at `factory.Build()` and lives as long as its engine. The engine owns disposal of everything under its lifecycle.
- **The runtime does not own command-queue routing.** Per ADR-0006, the engine queue is framework-private FIFO.
- **Methods may be called concurrently from different drain cycles only if the engine guarantees it.** Today the engine serializes drain cycles, so the runtime does not need to be thread-safe.

### `FilterTriggers` — trigger availability

Called by the pipeline once per command, twice per execution (one before-fold, one after-fold). Receives all registered `TriggerMatch` entries for that command type and returns the eligible subset.

```csharp
public enum TriggerPhase { Before, After }
```

The split between availability (the runtime) and ordering (`ITriggerPipeline`) is deliberate — see [`triggers/02-trigger-pipeline.md`](features/triggers/models/02-trigger-pipeline.md). `GameRulesRuntime` says *which* triggers fire; `ITriggerPipeline` says *in what order*.

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

### `Evaluate` — FSM evaluator

Called after every `GameAction` finishes executing. Reads current state through `State.Get<TState>()`, runs registered SBAs, optionally consults `current.ShouldExit`, and returns the new `MatchState`. The engine compares the returned type to the current state's type and synthesizes `TransitionStateCommand` if different.

The single most important point about `Evaluate`: **it reads state, not the just-executed command type.** If the rule is "any card sitting in PlayArea must be executed," that rule fires the same way whether the card got there via `PlayCardCommand`, a copy effect, an opponent's trigger, or a token spawn. State-based evaluation handles all sources uniformly.

```csharp
public override MatchState Evaluate(
    GameAction    justExecuted,
    CommandResult result)
{
    // SBAs preempt — cross-cutting overrides like game-over, legend rule.
    foreach (var sba in StateBasedActions)
        if (sba.Check(State) is { } over) return over;

    // Default path — consult the current state's exit predicate, then Flow.
    var current = State.Get<MatchStateSlice>().Current;
    return current.ShouldExit(justExecuted, result, State)
        ? Flow.NextAfter(current)
        : current;
}
```

`justExecuted` and `result` are passed for logging and `ShouldExit` predicate use — not as the primary lens for transition logic. If your `Evaluate` override has a `switch` on `justExecuted`'s type to decide transitions, that's a smell: the rule is probably state-based and the type-match is just shorthand that misses other paths into the same state.

`Evaluate` is **not** the place to mutate state slots. State updates flow through state mutators reacting to commands. `Evaluate` reads state and decides what `MatchState` runs next; the engine handles transitions through `TransitionStateCommand`.

**Custom orchestration.** Games that need to interleave their own logic between SBAs / ShouldExit / Flow override `Evaluate` directly. MTG-style stack-resolution timing, multi-step decision sequencing, etc. The base body is the *default composition*; overriding lets the runtime weave its own checks in. The drain-policy composition per [ADR-0011](Product/decisions/0011-effect-orchestration-dispatch-requests.md) (pause if `RequestsSlice` is non-empty; drain `PendingEffectsSlice` if non-empty) is one such composition.

### Where command routing lives (it's not here)

Earlier drafts of this contract included an `OnIncomingCommand` (a.k.a. `Route`) method intended to let the runtime gate intake and route commands at the queue level. Per ADR-0006, that method is deleted. Reasoning:

- **Command-queue routing isn't what games actually customize.** Walking through Stacklands, Hearthstone, and MTG concretely showed that the framework FIFO is sufficient for command-level scheduling. What differs across game styles is *effect-level* orchestration (when an effect starts, whether effects nest / batch / stack).
- **Per-command primitives are framework-level.** Modify, cancel, replace, dispatch — these operate on the in-flight command via `IEffectScope` during the trigger fold. They don't need a runtime hook.
- **Match-state and session gating moved to the framework intake gate.** Framework checks `engine.Session` and `MatchStateSlice.Current.Actions` at `engine.Dispatch` and rejects ineligible dispatches uniformly. See [`engine/01 §Intake gates`](features/engine/models/01-game-engine.md).
- **Game-flow legality lives in `pipeline.Validate`.** Per [ADR-0010](Product/decisions/0010-validator-handler-split.md), the registered validator chain (game-shipped `ICommandValidator<TCommand>` after the framework defaults) catches command-specific legality.

The effect-pipeline surface ([ADR-0011](Product/decisions/0011-effect-orchestration-dispatch-requests.md)) is where game-level scheduling decisions live. MTG-style stack semantics is expressible via game-state zones (the stack is a `ZoneContentsState`) plus `Evaluate` overrides watching that zone and returning custom `MatchState`s.

### Five rejection paths

This table is the canonical reference for command rejection across the framework. Per [ADR-0010](Product/decisions/0010-validator-handler-split.md) the four pre-execution paths are all expressed through registered `ICommandValidator<TCommand>` chains and the per-state ViewModel; the mid-execution path lives in the before-fold.

| Need | Path | Where | Sees | Owner |
|---|---|---|---|---|
| "Don't let the player attempt this in UI" | **Per-state ViewModel** | Presentation, before any dispatch | `MatchStateSlice.Current` + game-projected affordances | Game UI / `MatchStateViewModel<TState>` — [ADR-0012](Product/decisions/0012-presentation-per-state-view-models.md) |
| "Reject because the engine isn't accepting commands" | **`SessionGate` validator** | Validator chain (framework default, registers first) | `engine.Session` via `IEngineSession` capability | Framework — see [`actions/06`](features/actions/models/06-command-validator.md) |
| "Reject because this command type isn't valid in the current match state" | **`MatchStateActionGate` validator** | Validator chain (framework default, registers after `SessionGate`) | `MatchStateSlice.Current.Actions` | Framework + game-authored `MatchState` — see [`actions/06`](features/actions/models/06-command-validator.md) |
| "Reject because this specific command is illegal" | **Game-shipped `ICommandValidator<TCommand>`** | Validator chain (game-shipped, registers after framework defaults) | The typed `TCommand` + `Store` via DI | `ICommandValidator<TCommand>` ([`actions/06`](features/actions/models/06-command-validator.md)) — typically the handler authored as `ValidatedCommandHandler<TCommand, TResult>` or as a reusable `CommandValidator<TCommand>` |
| "Cancel this mid-execution based on a card effect" | **Before-fold `Cancel`** | Inside the pipeline (after validators pass; during handler invocation) | The constructed `GameAction`, ability `Context` | `OnBefore<T>` trigger |

The engine's `Dispatch` delegates to `pipeline.Validate`, which runs the registered validator chain in DI order (framework defaults first; game-shipped after). Short-circuit on first reject; one reason returned. Any layer can reject; the engine surfaces a single `bool + reason` to the caller via the try-get pattern (`bool Dispatch(GameAction action, out string? reason)`).

**Rule of thumb: the earliest path with enough information wins.** Pushing a check upstream means fewer wasted dispatches, less duplicated logic, and clearer UI:

- If the UI can know it's illegal before submitting → the per-state VM doesn't surface the affordance, and validation doesn't even need to run.
- If the engine isn't ready (`NotStartedSession` / `EndedSession`) → `SessionGate` rejects.
- If the command type isn't valid in the current state → `MatchStateActionGate` rejects.
- If the command is malformed against current state → a game-shipped validator rejects.
- If a card effect must cancel mid-resolution → before-fold cancel.

The validator chain is *authoritative*: framework defaults handle session / match-state gating; game-shipped validators handle command-specific legality (cost, ownership, line-of-sight, etc.). The match-state actions list is a cheap prophylactic gate — coarse, type-only, no rich state read. The per-state ViewModel projection is the UI's best-effort affordance map. If the affordance is stale, dispatch catches it and surfaces the rejection (toast).

### Anti-scope — what does NOT belong on the runtime

Concrete anti-scope. If you're tempted to put one of these in `GameRulesRuntime`, look elsewhere:

| Don't put here | Put it in |
|---|---|
| **Command-specific legality** ("can this `MoveCard` run right now?") | `ICommandValidator<TCommand>` ([`actions/06`](features/actions/models/06-command-validator.md)) — typically authored as a `ValidatedCommandHandler<TCommand, TResult>` or a reusable `CommandValidator<TCommand>` |
| **Gameplay state** (last-move memory, stash buffers, phase counters) | Scaffold state slices in the `Store` (per "zero gameplay state" — see Q2 below) |
| **Authored configuration** (starting life, hand size, format knobs) | `GameRulesDefinition` `[SerializeField]` fields ([`engine/02`](features/engine/models/02-game-rules-definition.md)), read through `Definition.X` |
| **Command-queue routing** (where this command goes in the queue) | Framework (engine FIFO; per ADR-0006). For effect-level batching/stacking, use a game-state zone plus `Evaluate` watching it. |
| Action handler logic ("how does damage actually resolve") | `ICommandHandler<TCommand, TResult>.Handle` ([`actions/03`](features/actions/models/03-command-handler.md)) — extend `GameCommandHandler<,>` |
| State mutation reacting to a command | Scaffold `Mutator<TState, TPayload>` registered against the payload the handler dispatches via `ctx.Store.Execute(payload)` |
| Trigger reaction logic ("when this card is damaged, draw") | `AbilityDefinition.Register` on the card |
| Async / awaits / player-input prompts | Effects (orchestrators) and per-state ViewModels (presentation) |
| Game session lifecycle (start, end, pause, concede, disconnect) | `GameEngine.Session` field + host orchestration (per ADR-0009 D12 hybrid future) |
| Trigger ordering | `ITriggerPipeline` |
| Player input → command resolution | Per-state `MatchStateViewModel<TState>.Map` — [ADR-0012](Product/decisions/0012-presentation-per-state-view-models.md) |
| UI affordances | Per-state ViewModel projection — [ADR-0012](Product/decisions/0012-presentation-per-state-view-models.md) |
| Match-state structure (phase shape, transitions) | `ITurnFlow` ([`08-turn-flow.md`](features/engine/models/08-turn-flow.md)) + concrete `MatchState` subclasses ([`07-match-state.md`](features/engine/models/07-match-state.md)) |
| Cross-cutting state-driven overrides (game-over, legend rule) | `ISBA` ([`09-state-based-actions.md`](features/engine/models/09-state-based-actions.md)) |

The runtime is purely for **game-wide FSM evaluation during play**: which triggers are eligible to fire, what `MatchState` runs next given current state, and effect-level orchestration (structural surface per [ADR-0011](Product/decisions/0011-effect-orchestration-dispatch-requests.md); recipe-shape internals open under `oq-effect-pipeline`). Any logic that belongs to a specific card, action, or ability lives on that card, action, or ability. Any structural turn knowledge lives in `ITurnFlow` and concrete `MatchState`s. Any cross-cutting state check lives in `ISBA`.

The runtime may hold *configuration* — rule modules, lookup tables, cached evaluators that are deterministic functions of inputs — and the immutable `Definition` reference. It must *not* hold gameplay memory. Test: if you reset the engine and replay all commands against a fresh `GameState`, does the runtime produce identical outputs without any of its own state being touched? It should. If a private field on the runtime influences an `Evaluate` output, that field belongs in `GameState` instead.

Engine-internal plumbing (the pending command queue, drain-cycle running flag) is **not** gameplay state — that's framework wiring living on the engine, not on the runtime. Per Q2 below.

### Composition

A real game's runtime will grow large — Flow, multiple SBAs, trigger filtering, custom Evaluate orchestration. The base type intentionally does not bake composition in; that's an authoring concern, not a framework primitive.

For games with one rule module, extend `GameRulesRuntime<TDefinition>` directly. The constructor takes the definition + state + game-specific subsystems; the abstract `Flow` and `StateBasedActions` properties are wired via DI. The contract methods do the work; the default `Evaluate` body composes Flow + SBAs cleanly.

The composition *contract* for multi-module rules (provider/policy ordering, enable-disable, conflict resolution) is deliberately undesigned for now; pin when the first composed-rules game forces it. A future modular-rule tier (likely double-generic on definition + module) will compose alongside the central runtime — see `oq-gamerules-composition`.

## Invariants

1. **Two-type split with container open-generic resolution.** [`GameRulesDefinition`](features/engine/models/02-game-rules-definition.md) (data) + `GameRulesRuntime<TDefinition>` (runtime); paired via `container.Resolve(typeof(GameRulesRuntime<>).MakeGenericType(setup.Game.Rules.GetType()))`.
2. **Synchronous contract methods.** `FilterTriggers` and `Evaluate` are synchronous — no `Task`, no awaits. Async lives in handlers / effects / `ITriggerPipeline`.
3. **Zero gameplay state on the runtime.** Configuration is fine; gameplay memory is not. Engine-internal plumbing is not gameplay state.
4. **Runtime never mutates state directly.** Mutation flows through `Store.Execute(payload)` → `Mutator<TState, TPayload>`. The runtime's only "write" is by returning a different-typed `MatchState` from `Evaluate`; the engine routes that through `TransitionStateCommand` → `SetMatchStatePayload`.
5. **Runtime never holds `GameEngine`.** Side effects flow through narrow capability services (`IStateScope`; injected SBAs / Flow / definition).
6. **Definition is immutable.** Constructor-injected once; never reassigned.
7. **No method holds onto its arguments.** Per-call data (candidates, command, result) is valid only for the call.
8. **Single-phase construction.** No `Initialize` hook; the constructor's return is when every dependency is bound.
9. **`Evaluate` returns a `MatchState`, not a list.** Per ADR-0009. Multi-action follow-ups now flow through `MatchState.EnterCommands` (enqueued by `TransitionStateHandler`) or through commands dispatched by handlers / effects.
10. **Five rejection paths** are the canonical legality model: Per-state VM (UI), `SessionGate` (framework validator), `MatchStateActionGate` (framework validator), game-shipped `ICommandValidator<TCommand>` (per-command legality), before-fold `Cancel` (mid-execution). The earliest path with enough information wins. Per [ADR-0010](Product/decisions/0010-validator-handler-split.md).

## Decisions

*Provenance: [ADR-0009](Product/decisions/0009-engine-as-fsm.md) — D1 (Evaluate → MatchState), D2 (MatchStateSlice + abstract MatchState), D4 (ITurnFlow on the runtime), D5 (SBAs on the runtime). [ADR-0010](Product/decisions/0010-validator-handler-split.md) — D10–D11 (rejection paths refresh: command-specific legality moves from `handler.Validate` to `ICommandValidator<TCommand>`; framework intake gates become registered validators). Other invariants below (Q1–Q3) are single-feature scope, refreshed for ADR-0009.*

## Examples

### Pile Counter runtime

```csharp
public sealed class PileCounterRulesRuntime : GameRulesRuntime<PileCounterRulesDefinition>
{
    protected override ITurnFlow            Flow              { get; }
    protected override IReadOnlyList<ISBA>  StateBasedActions { get; }

    public PileCounterRulesRuntime(
        PileCounterRulesDefinition definition,
        IStateScope                state,
        PileCounterTurnFlow        flow,
        PileCounterWinSBA          winCheck)
        : base(definition, state)
    {
        Flow              = flow;
        StateBasedActions = [winCheck];
    }

    // FilterTriggers omitted — base class ships a virtual identity default.
    // Evaluate omitted — base class default body suffices (SBAs preempt; otherwise stay
    // in the current state since PilePlayingState.ShouldExit always returns false).
}
```

Two observations:

- **No custom `Evaluate`.** The default body composes Flow + SBAs + ShouldExit; Pile Counter's win check lives entirely in the SBA.
- **No `OnIncomingCommand`, no `IAvailableActions`.** The framework FIFO handles command queueing; valid command types live on `MatchState.Actions`; presentation reads `MatchStateSlice` directly.

See [`support/samples/pile-counter.md`](support/samples/pile-counter.md) for the full sample including handlers, mutators, bootstrap, and the Flow + SBA shapes.

### Custom Evaluate for MTG-style orchestration

```csharp
public sealed class StandardMagicRulesRuntime : GameRulesRuntime<StandardMagicRulesDefinition>
{
    protected override ITurnFlow             Flow              { get; }
    protected override IReadOnlyList<ISBA>   StateBasedActions { get; }

    private readonly IStackSystem _stack;

    // ... constructor wiring ...

    public override MatchState Evaluate(
        GameAction    justExecuted,
        CommandResult result)
    {
        // SBAs preempt (lethal damage, legend rule, etc.).
        foreach (var sba in StateBasedActions)
            if (sba.Check(State) is { } over) return over;

        var current = State.Get<MatchStateSlice>().Current;

        // Drain-policy composition per ADR-0011 — same guards the default body uses.
        if (State.Get<RequestsSlice>().Open.Count > 0)         return current;
        if (!State.Get<PendingEffectsSlice>().Pending.IsEmpty) return current;

        // MTG-style stack-resolution timing: if priority has been passed twice
        // and the stack has a resolvable instance, transition to ResolveStackTopState.
        // (The "stack" is a game-state zone; priority tracking is a game-state slice.)
        if (_stack.HasResolvable(State))
            return new ResolveStackTopState(_stack.Top(State));

        // Default tail: current.ShouldExit + Flow.NextAfter.
        return current.ShouldExit(justExecuted, result, State)
            ? Flow.NextAfter(current)
            : current;
    }
}
```

The override weaves stack-resolution between SBAs and the default ShouldExit/Flow composition.

## Open follow-ups

- **Effect pipeline internals** — structural surface landed in [ADR-0011](Product/decisions/0011-effect-orchestration-dispatch-requests.md) (async coroutines, `PendingEffectsSlice` queue, single async `Dispatch`, request system). What lives *inside* an effect (authoring vocabulary, primitive set, graph vs free-form, recipe shape, definition catalog) remains open under `oq-effect-pipeline`. Per ADR-0006, this surface is separate from command routing.
- **`GameState` slice catalog** — concrete catalog of slices the rule reads through `IStateScope`. Pin per-game; framework-shipped catalog stays minimal. See [`state/state.md`](features/state/state.md).
- **`IHandlerRegistry` shape** — final shape (typed lookup, error semantics, registration) resolves with the action-pipeline doc.
- **MTG SBA ordering** — if a future game needs MTG-strict ordering, decide whether to add a pre-after-fold evaluator hook or document the workaround (encode SBAs inside `Evaluate` and accept the post-after-fold timing). See Q3 below.
- **`GameRulesRuntime` composition** — module composition contract (provider/policy ordering, enable-disable). Service surface supports it mechanically; pin when first composed game forces it. Tracked as `oq-gamerules-composition`.

---

*Below: provenance — three engine-feature decisions that shape this Model. All single-feature scope (no project-wide ADR applies; folded into Model provenance per §4.5 on 2026-05-08). Originals 2026-04-29; ADRs 0001 and 0002 carry 2026-05-07 amendments per the triggers + dispatch design pass; 2026-05-10 amendments per ADR-0009.*

## Options considered

### Q1 — Rules layer shape (services injected vs. parameter-passed)

The rules brain originally drifted between "interface returning rich shapes" and "static class with global state." We needed a single shape that lets the rule call into framework services (state) without inheriting framework state and without forcing the rule to wire wiring.

- **A — Pure interface (`IGameRules`).** Rejected — callers had to thread services through every method, bloating signatures and making evolution painful.
- **B — Static class with global services.** Rejected — makes per-game state impossible and breaks composition for multi-rule scenarios.
- **C — Abstract class with constructor-injected services (Scaffold `IStateScope`); methods take only ephemeral data; game-supplied `ITurnFlow` and `IReadOnlyList<ISBA>` as abstract properties wired via DI.** **Chosen.**

The two-type split into `GameRulesDefinition` (data; [`engine/02`](features/engine/models/02-game-rules-definition.md)) + `GameRulesRuntime<TDefinition>` (this Model; runtime brain) is an outcome of Q1 — the runtime *needs* services injected; the data half doesn't. Splitting them lets each carry only what it needs and gives replay / save / network the asset-friendly serializable shape they require.

**Amendment (2026-05-07).** The original Option C listed `IIntakeQueue` among the injected services. Per ADR-0006 (two-pipeline architecture), `IIntakeQueue` is deleted — the command queue is engine-private FIFO, with no runtime service surface for command-queue routing.

**Amendment (2026-05-10).** Per ADR-0009, `IAvailableActions` is also removed from the injected services list. Valid command types live on `MatchState.Actions`; the framework's intake gate reads them from `MatchStateSlice`. The runtime's "what's next" answer is a `MatchState` (returned from `Evaluate`), not a snapshot pushed through a service. New abstract properties `Flow` (`ITurnFlow`) and `StateBasedActions` (`IReadOnlyList<ISBA>`) join the base type; concrete runtimes wire them via DI.

### Q2 — Zero gameplay state on the runtime

If `GameRulesRuntime` carries gameplay state, snapshot/restore breaks, replays diverge, and per-player views become impossible to disentangle. We need a clean separation between the rule (configuration) and gameplay memory (state slices in the Scaffold `Store`).

- **A — Allow rules to cache derived state for performance.** Rejected — cache invalidation becomes a global concern and breaks state-pattern guarantees.
- **B — Allow rules to hold transient buffers (e.g. queued events) as gameplay memory.** Rejected — gameplay buffers belong in state slices (snapshot-roundtrippable), not on the runtime. (Original wording cited `IIntakeQueue`; that type is gone per ADR-0006, but the principle stands.)
- **C — Zero gameplay state on rules; gameplay memory lives only in `GameState` (Scaffold States).** **Chosen.**

**Amendment (2026-05-07).** The original wording said "zero **game-affecting** state." Refining: the rule holds zero **gameplay** state — gameplay memory (last move, phase, score, etc.) lives in Scaffold state slices. Engine-internal plumbing (most notably the pending command queue) is **not** gameplay state; that's framework wiring, and where it physically lives (engine-private field, framework-owned slice, or runtime base class) is an engine-implementation detail, not a violation of this rule.

**Amendment (2026-05-10).** Per ADR-0009, the polymorphic match state lives in `MatchStateSlice` (framework-owned). The runtime computes the new state from current state + Flow + SBAs but does not hold the current state itself — it reads `MatchStateSlice.Current` via `IStateScope` each time. The "zero gameplay state" rule still holds and is now reinforced structurally.

### Q3 — MTG-strict state-based-action ordering

Magic: the Gathering's rules engine performs state-based actions (SBAs) in a tight loop *between* trigger fold and reaction dispatch. Our evaluation loop runs SBAs at the start of `Evaluate`, which fires after the action handler and after the after-fold. This is a real semantic difference and we need to acknowledge it rather than pretend it doesn't exist.

- **A — Add a pre-after-fold evaluator hook to the loop.** Deferred — until a game actually needs MTG-strict SBA ordering.
- **B — Rebuild the loop around an MTG-strict model.** Rejected — pessimizes the floor case and most games don't care.
- **C — Document as a known difference; implement a workaround (encode SBAs as state-driven checks inside `Evaluate` or as game-state mutators reacting to damage/death triggers) on a per-game basis.** **Chosen for now.**

The evaluation loop diverges from MTG-strict SBA ordering by design. Games that need MTG semantics implement the SBA pass themselves until the framework adds the hook. When a real game forces the issue, revisit option A.

In the framework the order is:

1. Action handler runs
2. After-fold runs (per-instance triggers)
3. Envelope closes
4. `Evaluate` runs (SBAs preempt; then ShouldExit + Flow)

So in the framework, after-triggers fire *on the originating action* before `Evaluate`'s SBAs see the post-action state. A game wanting strict MTG SBA ordering would need to encode death triggers etc. as state-based checks inside `Evaluate` rather than as `OnAfter<DamageAction>` triggers — or accept the framework's ordering. This is a known semantic difference, not a bug.

## Rationale

The three decisions converge on a coherent shape:

- **Q1 (services injected)** lets `FilterTriggers` and `Evaluate` shed boilerplate parameters and lets composition (multi-module rules) work mechanically — services pass through, not statically held. The two-type split (Definition data half / Runtime behavior half) is what falls out cleanly when the runtime needs services and the data doesn't. The FSM shape per ADR-0009 sits naturally on this: `Flow` and `StateBasedActions` are game-supplied abstract properties; `Evaluate` composes them with a sensible default.
- **Q2 (zero gameplay state)** is what makes deterministic replay, snapshot/restore, and per-player view derivation trivial. Reset the engine, replay the commands, get the same outputs — because gameplay memory is in the Store. Per ADR-0009, the *match state itself* is in the Store too (via `MatchStateSlice`), so the rule never needs to hold "what state are we in."
- **Q3 (MTG SBA as known difference)** keeps the floor case simple. Adding a pre-after-fold evaluator hook would constrain the loop's shape for every game; documenting the difference and providing a workaround keeps the loop minimal until a real game forces the question.

The single-phase DI construction (no `Initialize` hook) falls out naturally from Q1: when the constructor returns, every dependency is bound. The "no caching, no buffers, no held engine reference" rule falls out from Q2.

## Consequences

- **Enables** `FilterTriggers` virtual-identity defaults and clean per-game overrides; composite rules later without redesigning the contract; deterministic replay, snapshot/restore, and trivial per-player view derivation; engine-internal plumbing (queue, flags) lives wherever the engine implementation prefers without violating zero-gameplay-state; a simpler default loop for the floor sample and Stacklands-class games; the FSM shape from ADR-0009 with a default `Evaluate` body that's mostly correct out of the box.
- **Constrains** the framework to own service lifetime (the rule must not cache or mutate them in ways that break re-entry); any *gameplay* memo the rule wants to remember must round-trip through state; porting MTG-class rules requires a manual SBA pass (re-open trigger: first game forcing it); the runtime no longer has an `IAvailableActions` to write to — presentation reads valid command types from `MatchState.Actions` directly, and per-state ViewModels project the affordance form (per [ADR-0012](Product/decisions/0012-presentation-per-state-view-models.md)).
