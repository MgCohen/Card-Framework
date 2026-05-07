---
type: model
status: settled
references: [features/engine/models/01-evaluation-loop.md, features/engine/models/05-game-factory.md]
tags: [#engine, #gamerules, #contract]
---

# GameRules Contract — model

## What it is

The contract for the game-specific decision layer plugged into the engine. Split into two cooperating types: **`GameRulesDefinition`** (pure `[SerializeField]`-bearing data class, asset-friendly) and **`GameRulesRuntime<TDefinition>`** (stateful runtime brain with services + game-specific subsystems). The engine itself (loop, queue, pipeline wiring) is framework code described in [`engine/01-evaluation-loop.md`](features/engine/models/01-evaluation-loop.md); this Model is for the game author who has to implement both halves for a specific game.

The rules layer is the **game's decision layer**. The framework engine drives the loop; the rules layer answers four questions during that loop:

1. *Is the engine accepting commands right now? Where should this one go?* — intake flow & routing
2. *Which registered triggers are eligible for this command?* — trigger availability
3. *Given the state after this action, what runs next?* — state-based follow-up (the evaluator)
4. *The queue is empty. What can the player do, and what runs next?* — idle hook (snapshot + follow-ups)

It is **not** a place for command-specific legality, action handlers, ability logic, state mutation, or async work.

## Contracts

### The two-type split

- **`GameRulesDefinition`** — pure `[SerializeField]`-bearing data class (non-`UnityObject`). Carries authored config (starting life, hand size, format-specific knobs). Asset-friendly, serializable, replay/save/network-safe. One per game-mode variant (Standard, Commander, Pauper).
- **`GameRulesRuntime<TDefinition>`** — stateful runtime class. Holds framework services (`IStateScope`, `IAvailableActions`, `IIntakeQueue`), game-specific subsystems (e.g. `IStackSystem`, `IDeckSystem` for an MTG-like), and a typed `Definition` of `TDefinition`. Implements the four contract methods. DI-resolved per match.

The factory pairs them via DI container open-generic resolution: a game installer registers `GameRulesRuntime<MagicRulesDefinition> → MagicRulesRuntime`; the factory takes the definition out of `MatchSetup` and resolves the runtime for it. See [`engine/05-game-factory.md`](features/engine/models/05-game-factory.md).

### Position in the system

| Layer | Owner | Lives in |
|---|---|---|
| Engine loop, queue, drain | Framework | `GameEngine` |
| Pipeline (handler lookup → validate → before fold → handle → after fold → envelope) | Framework | `ICommandPipeline` |
| Trigger ordering + fold execution | Framework (swappable) | `ITriggerPipeline` |
| **Command-specific legality** + state mutation | **Per-action** | **`ICommandHandler.Validate` / `Handle`** |
| Trigger reaction logic | Per-ability | `AbilityDefinition.Register` |
| **Game-wide decisions** | **Game** | **`GameRulesRuntime<T>`** (with `GameRulesDefinition` carrying authored config) |
| State mutators (consume payloads dispatched by handlers, write slices) | Per-payload | Scaffold `Mutator<TState, TPayload>` |
| Player input → command resolution | Game | `IIntentMapper` |
| UI affordance projection | Game (presentation) | `AffordanceMap` |
| Match lifecycle (start, end, disposal) | Framework | `GameEngine` per [`engine/06`](features/engine/models/06-match-lifecycle.md) |
| Broader session lifecycle (pause, concede, disconnect, multi-match) | Game | External controller wrapping `GameEngine` |

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
    protected TDefinition        Definition { get; }
    protected IStateScope        State      { get; }   // Scaffold; read-only
    protected IAvailableActions  Actions    { get; }
    protected IIntakeQueue       Pending    { get; }

    protected GameRulesRuntime(
        TDefinition        definition,
        IStateScope        state,
        IAvailableActions  actions,
        IIntakeQueue       pending)
    {
        Definition = definition;
        State      = state;
        Actions    = actions;
        Pending    = pending;
    }

    // 1. Intake. Pure flow: paused/ended gating + routing the command into
    //    the right structure (queue, internal stash, etc.). Never reads
    //    the command body — command-specific legality is the handler's job.
    public abstract IntakeResult OnIncomingCommand(
        GameCommand    command,
        DispatchOrigin origin);

    // 2. Trigger availability. Filters registered candidates for a given command and phase.
    //    Default: identity. Override for trigger-bearing games.
    public virtual IReadOnlyList<TriggerMatch> FilterTriggers(
        IReadOnlyList<TriggerMatch> candidates,
        GameCommand                 command,
        TriggerPhase                phase)
        => candidates;

    // 3. Evaluator. Reads state after each action; returns follow-up commands.
    public abstract IReadOnlyList<GameAction> Evaluate(
        GameAction     justExecuted,
        CommandResult  result);

    // 4. Idle hook. Writes the snapshot through `Actions` (when it wants to)
    //    and returns any follow-up commands the engine should dispatch.
    public abstract DrainResult OnQueueDrained();
}
```

Methods take only what's ephemeral to the call; stable engine handles (state access, intake queue, snapshot writer) are services held on the runtime.

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
        IIntakeQueue         pending,
        IStackSystem         stack,
        IDeckSystem          decks)
        : base(definition, state, actions, pending)
    {
        _stack = stack;
        _decks = decks;
    }

    public override IntakeResult OnIncomingCommand(GameCommand c, DispatchOrigin o) => /* … */;
    public override IReadOnlyList<GameAction> Evaluate(GameAction j, CommandResult r) => /* … */;
    public override DrainResult OnQueueDrained() => /* … */;
}
```

### Injected services

| Service | Purpose |
|---|---|
| `TDefinition Definition` | The serializable data object selected for this match (`MatchSetup.Rules`). Read config knobs through it. Immutable for the runtime's lifetime. |
| `IStateScope State` | Scaffold read-only state surface. Read sliced state via `State.Get<TState>()`. Read fresh per access. |
| `IAvailableActions Actions` | Snapshot writer — `Set` / `Append` / `Clear`. The runtime (and effects) call this to publish; the engine never auto-publishes. See [`engine/03`](features/engine/models/03-snapshot-pipeline.md). |
| `IIntakeQueue Pending` | The live engine queue. Operations: `AddFirst` / `AddLast` / `Remove` / iterate. |
| Game-specific subsystems | Concrete runtimes inject whatever else they need (`IStackSystem`, `IDeckSystem`, …) via their own constructor. Registered in the game's installer. |

### `OnIncomingCommand` — intake (flow only)

Called once for every command entering the dispatch channel, from any source. It runs *after* `handler.Validate` has confirmed the command is well-formed and legal in principle, and *before* the command is on the queue. The rule answers two questions only:

1. **Is the engine accepting commands right now?** (paused, ended, locked → reject)
2. **Where should this go?** (queue back, queue front, custom routing)

It **never reads the command body**. A rule that switches on `command is FooCommand` is wrong — that logic belongs in `FooHandler.Validate`.

```csharp
public sealed record IntakeResult(bool Accepted, string? Reason = null)
{
    public static readonly IntakeResult Ok = new(true);
    public static IntakeResult Reject(string reason) => new(false, reason);
}

public enum DispatchOrigin { PlayerIntent, TriggerReaction, Timer, Internal }
```

`Pending` is the actual engine queue with full read+write access (`AddFirst`, `AddLast`, `Remove`, iteration). Mirrors the trust pattern in `FilterTriggers` — give the runtime the full data, no sandbox.

How to express common behaviors:

| Behavior | How the rule implements it |
|---|---|
| Append to queue (default) | `Pending.AddLast(command); return IntakeResult.Ok;` |
| Run before pending (instant-speed) | `Pending.AddFirst(command); return IntakeResult.Ok;` |
| Stash for later | Rule (or a state slot) keeps `command` in a side collection; release path adds it back to `Pending` from a signal handler or `OnQueueDrained` follow-up. Returns `IntakeResult.Ok` from intake. |
| Cancel a queued sibling | `Pending.Remove(otherCommand)` then add the new one. Allowed; rule's prerogative. |
| Reject | Don't touch `Pending`. `return IntakeResult.Reject("game paused");` |

### `FilterTriggers` — trigger availability

Called by the pipeline once per command, twice per execution (one before-fold, one after-fold). Receives all registered `TriggerMatch` entries for that command type and returns the eligible subset.

```csharp
public enum TriggerPhase { Before, After }
```

The split between availability (the runtime) and ordering (`ITriggerPipeline`) is deliberate. `GameRulesRuntime` says *which* triggers fire; `ITriggerPipeline` says *in what order*.

Default implementation returns the candidates unchanged. The base class ships this as a virtual default — games with no eligibility logic don't need to override it.

Example uses: phase gating (no triggers fire during setup), mana abilities (skip the trigger pipeline entirely for speed-mana costs), stack restriction (triggers suppressed while a specific resolution lock is active), target validation (a trigger requires a legal target before being considered applicable), phase-asymmetric filtering (replacements off during cleanup but reactions still fire).

### `Evaluate` — state-based follow-up

Called after every `GameAction` finishes executing. Reads current state through Scaffold `State.Get<TState>()`, returns 0..N actions to enqueue next. **Reads state, not the just-executed command type.** If the rule is "any card sitting in PlayArea must be executed," that rule fires the same way whether the card got there via `PlayCardCommand`, a copy effect, an opponent's trigger, or a token spawn. State-based evaluation handles all sources uniformly.

```csharp
public override IReadOnlyList<GameAction> Evaluate(
    GameAction justExecuted,
    CommandResult result)
{
    var playArea = State.Get<ZonesState>().GetZone(WellKnownZones.PlayArea);
    if (playArea.HasCards)
        return new[] { new ExecuteCardCommand { Source = playArea.TopCard, ... } };

    return Array.Empty<GameAction>();
}
```

`justExecuted` and `result` are passed for logging and tie-breaking purposes — not as the primary lens. If your `Evaluate` has a `switch` on `justExecuted`'s type, that's a smell. `Evaluate` is *not* the place to mutate state slots like "last command resolved" — those updates flow through state mutators reacting to commands.

**Ordering nuance vs. MTG state-based actions.** In MTG, state-based actions check **before** triggers go onto the stack. In this framework the order is: action handler runs → after-fold runs → envelope closes → `Evaluate` runs. So in the framework, after-triggers fire *on the originating action* before `Evaluate` sees the post-action state. A game wanting strict MTG SBA ordering would need to encode death triggers etc. as state-based checks inside `Evaluate` rather than as `OnAfter<DamageAction>` triggers — or accept the framework's ordering. This is a known semantic difference (formerly ADR-0006, now folded into [`engine/01`](features/engine/models/01-evaluation-loop.md) provenance).

### `OnQueueDrained` — idle hook

Called once after a drain cycle completes with the queue empty. Returns a `DrainResult` carrying any follow-up commands the engine should dispatch (phase advance, end-of-game check, etc.). The available-actions snapshot is **not** returned here — the rule writes it through the injected `Actions` service whenever it wants to publish.

```csharp
public sealed record DrainResult(IReadOnlyList<GameCommand> Dispatch)
{
    public static readonly DrainResult Empty = new(Array.Empty<GameCommand>());
}
```

The rule typically writes the snapshot before returning:

```csharp
public override DrainResult OnQueueDrained()
{
    var templates = EnumerateLegalActions(State);
    Actions.Set(new AvailableActionsSnapshot(templates));
    return DrainResult.Empty;
}
```

Typical responsibilities:

- **Snapshot publication** — enumerate templates, call `Actions.Set(snapshot)`. May filter at the per-template level. May call `Handlers.Validate` on demand to prune templates whose every binding fails.
- **Phase advance** — emit `new AdvancePhaseCommand()` in `Dispatch`.
- **Priority grant** — emit `new GrantPriorityCommand(activePlayer)` in `Dispatch`.
- **End-of-match check** — emit `new EndMatchCommand(outcome)` in `Dispatch` if the match is over. The framework-owned handler emits `MatchEndedSignal` and flips `EngineState.Lifecycle` to `Ended`. See [`engine/06 §EndMatchCommand`](features/engine/models/06-match-lifecycle.md).
- **One-shot games** — never publish past bootstrap; `OnQueueDrained` is a no-op (`return DrainResult.Empty;`).

### Handler types

Game authors implement handlers by extending `CommandHandler<TCommand, TResult>`. The pipeline resolves them through a non-generic `ICommandHandler` base; the typed abstract class bridges the two surfaces.

```csharp
// Non-generic base — the pipeline works through this surface
public interface ICommandHandler
{
    bool Validate(GameCommand command, IStateScope state, out string? reason);
    Task<CommandResult> Handle(GameCommand command, IHandlerContext ctx);
}

// Typed base game authors extend
public abstract class CommandHandler<TCommand, TResult> : ICommandHandler
    where TCommand : GameCommand
    where TResult  : CommandResult
{
    public abstract bool Validate(TCommand command, IStateScope state, out string? reason);
    public abstract Task<TResult> Handle(TCommand command, IHandlerContext ctx);

    bool ICommandHandler.Validate(GameCommand cmd, IStateScope state, out string? reason)
        => Validate((TCommand)cmd, state, out reason);
    Task<CommandResult> ICommandHandler.Handle(GameCommand cmd, IHandlerContext ctx)
        => Handle((TCommand)cmd, ctx).ContinueWith(t => (CommandResult)t.Result);
}

// Registry — resolver only; used internally by the pipeline
public interface IHandlerRegistry
{
    ICommandHandler Resolve(GameCommand command);
}
```

`IHandlerRegistry` is a pipeline-internal type. The runtime does not hold it.

### The four rejection paths — when to use which

This table is the canonical reference for command rejection across the framework.

| Need | Path | Where | Sees | Owner |
|---|---|---|---|---|
| "Don't let the player attempt this in UI" | **AvailableActions** | Presentation, before any dispatch | `GameState.AvailableActions` (snapshot) | Game UI / `AffordanceMap` |
| "Reject because the engine isn't accepting commands now" | **`OnIncomingCommand` Reject** | Engine intake (runtime) | `IntakeContext`; never command body | `GameRulesRuntime` |
| "Reject because this specific command is illegal" | **`Handler.Validate` Reject** | Engine intake (handler) | The typed `TCommand` + `GameState` | `ICommandHandler<TCommand,_>` |
| "Cancel this mid-execution based on a card effect" | **Before-fold `Cancel`** | Inside the pipeline | The constructed `GameAction`, ability `Context` | `OnBefore<T>` trigger |

Rule of thumb: **the earliest path with enough information wins.** Pushing a check upstream means fewer wasted dispatches, less duplicated logic, and clearer UI.

### What does NOT belong in the runtime

| Don't put here | Put it in |
|---|---|
| **Command-specific legality** ("can this `MoveCard` run right now?") | `ICommandHandler<TCommand,_>.Validate` |
| **Game-affecting state** (last-move memory, stash buffers, phase counters) | Scaffold state slices in the `Store` (runtime holds zero gameplay memory — locked contract) |
| **Authored configuration** (starting life, hand size, format knobs) | `GameRulesDefinition` `[SerializeField]` fields, read through `Definition.X` |
| Action handler logic ("how does damage actually resolve") | `ICommandHandler<TCommand,_>.Handle` |
| State mutation reacting to a command | Scaffold `Mutator<TState, TPayload>` registered against the payload the handler dispatches via `ctx.Store.Execute(payload)` |
| Trigger reaction logic ("when this card is damaged, draw") | `AbilityDefinition.Register` on the card |
| Async / awaits / player-input prompts | Effects (orchestrators) and decision actions |
| Game session lifecycle (start, end, pause, concede, disconnect) | Separate `IGameSession` layer (out of scope) |
| Trigger ordering | `ITriggerPipeline` |
| Player input → command resolution | `IIntentMapper` |
| UI affordances | Presentation layer; `AffordanceMap` projects from `GameState.AvailableActions` |

### Composition

A real game's runtime will grow large — phase rules, SBAs, ordering rules, available-actions computation. The base type intentionally does not bake composition in; that's an authoring concern. The `IAvailableActions` service surface (`Set` / `Append` / `Clear`) supports module composition mechanically — multiple modules can `Append` contributions and an override module can `Set` to replace. The composition *contract* (provider/policy ordering, enable-disable, conflict resolution) is deliberately undesigned for now; pin when the first composed-rules game forces it (`oq-gamerules-composition`).

For games with one rule module, extend `GameRulesRuntime<TDefinition>` directly.

## Invariants

Model-local invariants. (See `engine.md ## Invariants` for feature-level rules — zero state on runtime, service-injected access, snapshot-via-service, state immutability.)

- **All four methods are synchronous.** No `Task`, no awaits.
- **Side effects flow through narrow capability services.** `OnIncomingCommand` mutates `Pending`. `OnQueueDrained` writes through `Actions`. `Evaluate` and `FilterTriggers` remain pure observers.
- **The runtime never holds `GameEngine`.** Only the services injected via its constructor.
- **The runtime never mutates state slices directly.** State changes happen through dispatched `GameAction`s whose handlers call `ctx.Store.Execute(payload)`.
- **The definition is immutable for the runtime's lifetime.** Constructor-injected once; never reassigned.
- **Command-specific legality is not the runtime's concern.** Each `ICommandHandler<TCommand,TResult>.Validate` owns the legality of its own command.
- **No method holds onto its arguments.** Caching `TriggerMatch` lists or `command` / `result` parameters across calls is undefined.
- **Per-match lifecycle.** The runtime is constructed at `factory.Build()` and lives as long as its engine.
- **Methods may be called concurrently from different drain cycles only if the engine guarantees it.** Today the engine serializes drain cycles; the runtime does not need to be thread-safe.

## Decisions

*n/a — no elevated ADR.*

## Examples

The Pile Counter `GameRulesRuntime` implementation lives in [`support/samples/pile-counter.md`](support/samples/pile-counter.md). The runtime construction patterns (with services injected, with subsystems, paired via container open-generic resolution) are in [`engine/05-game-factory.md` §Examples](features/engine/models/05-game-factory.md).

## Open follow-ups

- **`GameState` slice catalog** — concrete catalog (`PilesState`, `LastMoveState`, `ZonesState`, `TurnState`, `AvailableActionsState`, etc.) the rule reads through `IStateScope`. Pin per-game; framework-shipped catalog stays minimal. See [`engine/04`](features/engine/models/04-gamestate-slices.md) for the framework-owned tier.
- **`IIntakeQueue` shape** — `CurrentlyExecuting` location pinned at engine implementation.
- **`IHandlerRegistry` shape** — final shape (typed lookup, error semantics, registration) resolves with action-pipeline doc.
- **`IntakeResult.Reason`** — free-form string vs. structured enum. Defer until a real game forces the choice.
- **MTG SBA ordering** — if a future game needs MTG-strict ordering, decide whether to add a pre-after-fold evaluator hook (currently a known difference, see [`engine/01`](features/engine/models/01-evaluation-loop.md) provenance).
- **`GameRulesRuntime` composition** — module composition contract (`oq-gamerules-composition`).

---

*Below: provenance — feature-internal decisions folded on 2026-05-07 from `features/engine/decisions/0001-gamerules-as-class-with-services.md` and `features/engine/decisions/0002-gamerules-zero-state.md`. Single-feature scope (the runtime shape is internal to engine; other features consume the resulting four-method contract but don't co-decide it).*

## Options considered

### Q1 — `GameRules` shape (formerly ADR-0001 — folded)

The rules brain originally drifted between "interface returning rich shapes" and "static class with global state." We need a single shape that lets the rule call into framework services (state, queue, snapshot) without inheriting framework state and without forcing the rule to wire wiring.

- **A — Pure interface (`IGameRules`).** Rejected: callers had to thread services through every method, bloating signatures and making evolution painful.
- **B — Static class with global services.** Rejected: makes per-game state impossible and breaks composition for multi-rule scenarios.
- **C — Abstract class with constructor-injected services** (Scaffold `IStateScope`, `IAvailableActions`, `IIntakeQueue`); methods take only ephemeral data. **Chosen.** Later refined into the `GameRulesDefinition` + `GameRulesRuntime<TDefinition>` split (definition carries authored config; runtime carries injected services).

### Q2 — Zero game-affecting state on runtime (formerly ADR-0002 — folded)

If `GameRules` carries game-affecting state, snapshot/restore breaks, replays diverge, and per-player views become impossible to disentangle. Need a clean separation between the rule (configuration) and gameplay memory (state slices).

- **A — Allow rules to cache derived state for performance.** Rejected: cache invalidation becomes a global concern and breaks state pattern guarantees.
- **B — Allow rules to hold transient buffers** (e.g. queued events). Rejected: duplicates `IIntakeQueue`.
- **C — Zero game-affecting state on rules; gameplay memory lives only in `GameState`** (Scaffold States). **Chosen.**

The runtime may hold *configuration* — rule modules, lookup tables, cached evaluators that are deterministic functions of inputs — and the immutable `Definition` reference. It must *not* hold gameplay memory. Test: if you reset the engine and replay all commands against a fresh `GameState`, does the runtime produce identical outputs without any of its own state being touched? It should.

## Rationale

Q1's abstract-class-with-services shape gives game authors an ergonomic surface (services available without re-passing) without coupling them to global state or static helpers. The later split into `GameRulesDefinition` + `GameRulesRuntime<TDefinition>` extends this — config becomes pure data (asset-friendly, replay-safe) while the runtime stays the behavior layer.

Q2's zero-game-state rule is the lock that makes everything else work. Snapshot/restore, replay, per-player view derivation, deterministic networking all rely on the runtime being replaceable at any moment if its slices survive. The runtime is *behavior*; gameplay memory is *state*.

## Consequences

- **Enables:** `FilterTriggers` virtual-identity defaults; clean per-game overrides; `CompositeGameRules` later without redesigning the contract; deterministic replay; snapshot/restore; trivial per-player view derivation.
- **Constrains:** framework owns service lifetime; the rule must not cache or mutate them in ways that break re-entry; any "memo" the rule wants must round-trip through state.
- **Cascade:** the four-method contract is consumed by `engine/01` (evaluation loop calls), `engine/03` (snapshot publication via `Actions.Set`), `actions/` (handler legality split), `triggers/` (`FilterTriggers` consumed by pipeline).
