# GameRules — The Game's Rules Brain

This document locks the contract for `GameRules`: the game-specific decision layer plugged into the engine. The engine itself (loop, queue, pipeline wiring) is framework code described in [20-engine-evaluation-loop.md](20-engine-evaluation-loop.md). This doc is for the game author who has to implement `GameRules` for a specific game.

Builds on:

- [20-engine-evaluation-loop.md](20-engine-evaluation-loop.md) — engine loop, drain cycle, pipeline wiring
- [21-trigger-reaction-system.md](21-trigger-reaction-system.md) — trigger fold, OnBefore/OnAfter, subscription
- [22-async-decisions-and-suspension.md](22-async-decisions-and-suspension.md) — async layer hierarchy, dispatch paths
- [23-replacement-prevention-effects.md](23-replacement-prevention-effects.md) — `FilterTriggers`, `ITriggerPipeline`, replacement/prevention semantics
- [26-pile-counter-sample-game.md](26-pile-counter-sample-game.md) — the working sample game; drives this contract's shape
- [27-presentation-loop.md](27-presentation-loop.md) — UI loop, snapshot in state, four validation layers

---

## 1. What `GameRules` Is

`GameRules` is the **game's decision layer**. The framework engine drives the loop; `GameRules` answers four questions during that loop:

1. *Is the engine accepting commands right now? Where should this one go?* — intake flow & routing
2. *Which registered triggers are eligible for this command?* — trigger availability
3. *Given the state after this action, what runs next?* — state-based follow-up (the evaluator)
4. *The queue is empty. What can the player do, and what runs next?* — idle hook (snapshot + follow-ups)

It is **not** a place for command-specific legality, action handlers, ability logic, state mutation, or async work. See §8 for the explicit anti-scope.

### Position in the system

| Layer | Owner | Lives in |
|---|---|---|
| Engine loop, queue, drain | Framework | `GameEngine` |
| Pipeline (handler lookup → validate → before fold → handle → after fold → envelope) | Framework | `ICommandPipeline` |
| Trigger ordering + fold execution | Framework (swappable) | `ITriggerPipeline` |
| **Command-specific legality** + state mutation | **Per-action** | **`ICommandHandler.Validate` / `Handle`** |
| Trigger reaction logic | Per-ability | `AbilityDefinition.Register` |
| **Game-wide decisions** | **Game** | **`GameRules`** |
| State mutators (react to commands, update state slots) | Per-action | `IStateMutator<TCommand>` |
| Player input → command resolution | Game | `IIntentMapper` |
| UI affordance projection | Game (presentation) | `AffordanceMap` (see doc 27) |
| Game session lifecycle (start, end, pause) | Game | Separate `IGameSession` (out of scope here) |

---

## 2. The Interface

```csharp
public interface GameRules
{
    // 1. Intake. Pure flow: paused/ended gating + routing the command into
    //    the right structure (queue, internal stash, etc.). Never reads
    //    the command body — command-specific legality is the handler's job.
    IntakeResult OnIncomingCommand(
        GameCommand   command,
        IntakeContext context);

    // 2. Trigger availability. Filters registered candidates for a given command and phase.
    IReadOnlyList<TriggerMatch> FilterTriggers(
        IReadOnlyList<TriggerMatch> candidates,
        GameCommand                 command,
        TriggerPhase                phase,
        GameState                   state);

    // 3. Evaluator. Reads state after each action; returns follow-up commands.
    IReadOnlyList<GameAction> Evaluate(
        GameAction     justExecuted,
        CommandResult  result,
        GameState      state);

    // 4. Idle hook. Returns the available-actions snapshot and any follow-up
    //    commands to dispatch. The engine handles publication and dispatch;
    //    the rule never side-effects.
    DrainResult OnQueueDrained(GameState state, IHandlerRegistry handlers);
}
```

### Contract guarantees

- **All four methods are synchronous.** No `Task`, no awaits. `GameRules` is a pure decision layer; async work belongs to handlers, effects, and `ITriggerPipeline`.
- **All four methods are observer-shaped — return data, never side-effect.** Intake returns an `IntakeResult` and may mutate the live queue handed to it via `IntakeContext.Pending` (the rule's only authority over engine state). `Evaluate` and `OnQueueDrained` return commands as data; the engine dispatches them. `FilterTriggers` returns a filtered list. **The rule never holds `GameEngine`.**
- **`GameRules` never mutates `GameState` directly.** State changes happen through dispatched `GameAction`s and the state-mutator pipeline (see [doc 26 §9.5](26-pile-counter-sample-game.md)).
- **`GameRules` holds zero game-affecting state.** This is a contract guarantee, not a suggestion. The rule may hold *configuration* — rule modules, cached evaluators, lookup tables — never gameplay memory. Anything game-affecting (last-move, phase, stash buffers, etc.) lives in `GameState`. (Stash routing in intake uses `IntakeContext.Pending` directly, not internal rule storage.)
- **Command-specific legality is not the rule's concern.** Each `ICommandHandler<TCommand,TResult>.Validate` owns the legality of its own command. The rule is generic across game logic.
- **No method holds onto its arguments.** `GameState`, `IntakeContext`, `TriggerMatch` lists, and the `IHandlerRegistry` reference are valid only for the call. Caching them across calls is undefined.
- **Methods may be called concurrently from different drain cycles only if the engine guarantees it.** Today the engine serializes drain cycles, so `GameRules` does not need to be thread-safe.

---

## 3. `OnIncomingCommand` — Intake (Flow Only)

Called once for every command entering the dispatch channel, from any source: player input, timer, trigger reaction, internal follow-up, network. It runs *after* `handler.Validate` has confirmed the command is well-formed and legal in principle (see [§7](#7-rejection-paths-when-to-use-which) and [doc 26 §9.7](26-pile-counter-sample-game.md)), and *before* the command is on the queue.

The rule answers two questions only:

1. **Is the engine accepting commands right now?** (paused, ended, locked → reject)
2. **Where should this go?** (queue back, queue front, custom routing)

It **never reads the command body**. A rule that switches on `command is FooCommand` is wrong — that logic belongs in `FooHandler.Validate`.

```csharp
public sealed record IntakeResult(bool Accepted, string? Reason = null)
{
    public static readonly IntakeResult Ok = new(true);
    public static IntakeResult Reject(string reason) => new(false, reason);
}

public sealed record IntakeContext(
    LinkedList<GameCommand>  Pending,             // live engine queue; rule may mutate
    GameCommand?             CurrentlyExecuting,  // null when engine is idle
    DispatchOrigin           Origin);             // PlayerIntent | TriggerReaction | Timer | Internal

public enum DispatchOrigin { PlayerIntent, TriggerReaction, Timer, Internal }
```

### Why the live queue (no scheduler wrapper)

`IntakeContext.Pending` is the actual engine queue, exposed as a `LinkedList<GameCommand>` so the rule has full read+write access — `AddFirst`, `AddLast`, `Remove`, iteration. This mirrors the trust pattern in `FilterTriggers` (give the rule the full data, no sandbox). An earlier draft used an `ICommandScheduler` wrapper to gate the API; we dropped it because there's no other API gating in `GameRules`, and stashing isn't a framework primitive — a rule that wants stashing just keeps an internal list (or its own state slot) and `AddFirst`/`AddLast`s on whatever signal releases it.

### How to express common behaviors

| Behavior | How the rule implements it |
|---|---|
| Append to queue (default) | `ctx.Pending.AddLast(command); return IntakeResult.Ok;` |
| Run before pending (instant-speed) | `ctx.Pending.AddFirst(command); return IntakeResult.Ok;` |
| Stash for later | Rule (or a state slot) keeps `command` in a side collection; release path adds it back to `Pending` from a signal handler or `OnQueueDrained` follow-up. Returns `IntakeResult.Ok` from intake. |
| Cancel a queued sibling | `ctx.Pending.Remove(otherCommand)` then add the new one. Allowed; rule's prerogative. |
| Reject | Don't touch `Pending`. `return IntakeResult.Reject("game paused");` |

The engine treats `Accepted = true` as "the rule took ownership" — whether the command is on the queue, stashed elsewhere, or routed via a custom structure is opaque. `Accepted = false` means "drop this command and surface `Reason` to the gateway/UI."

### What does NOT belong in `OnIncomingCommand`

Anything that depends on what the command *is* — phase requirements, turn ownership, source/target legality, ability prerequisites — belongs in `handler.Validate`. The rule's intake is purely about *temporal* flow (when to process), never about *what* is being processed.

Examples of correct intake reasoning:
- "The game is paused — reject everything."
- "Game has ended — reject."
- "This is a `TriggerReaction` while a stack item is resolving — stash it for later release."

Examples of *wrong* intake reasoning (these belong in `Validate`):
- "This is a `PlayCardCommand` and we're in cleanup phase." (Phase requirement is `PlayCardHandler.Validate`'s job.)
- "This is a `MoveCardCommand` and the source pile is empty." (Pile state check is `MoveCardHandler.Validate`'s job.)
- "It's not this player's turn." (Turn ownership is the action's `Validate`.)

### `Reason` payload

`Reason` is a free-form string, populated only when `Accepted == false`. The gateway / UI layer is responsible for formatting it for the player. The engine itself does not inspect or display it.

> **Open:** Whether `Reason` should be a structured enum (`Paused`, `GameEnded`, …) versus free-form text. Free-form is simpler; structured allows localization. Defer until a real game forces the choice.

---

## 4. `FilterTriggers` — Trigger Availability

Called by the pipeline once per command, twice per execution (one before-fold, one after-fold). Receives all registered `TriggerMatch` entries for that command type and returns the eligible subset.

```csharp
public enum TriggerPhase { Before, After }
```

The split between availability (`GameRules`) and ordering (`ITriggerPipeline`) is deliberate — see [doc 23 §3–4](23-replacement-prevention-effects.md). `GameRules` says *which* triggers fire; `ITriggerPipeline` says *in what order*.

### Why the phase parameter

Some games want different availability rules before vs. after:

- During a "no replacements" phase, suppress before-fold triggers but keep after-fold reactions.
- During spell resolution, suppress after-fold triggers from the resolving permanent itself.

A single filter that cannot tell which phase it is in cannot express these without inspecting the command. Adding the enum costs nothing and makes the rule readable.

### Default implementation

Return the candidates unchanged. Games that have no eligibility logic — most simple games — implement this as a one-liner. Likely to become a virtual default once `GameRules` settles into an abstract base class rather than a pure interface (see open items).

### Example uses

- **Phase gating** — no triggers fire during setup
- **Mana abilities** — skip the trigger pipeline entirely for speed-mana costs
- **Stack restriction** — triggers suppressed while a specific resolution lock is active
- **Target validation** — a trigger requires a legal target to exist before it is considered applicable
- **Phase-asymmetric filtering** — replacements off during cleanup but reactions still fire

---

## 5. `Evaluate` — State-Based Follow-Up

Called after every `GameAction` finishes executing. Reads current state, returns 0..N actions to enqueue next.

The single most important point about `Evaluate`: **it reads state, not the just-executed command type.** If the rule is "any card sitting in PlayArea must be executed," that rule fires the same way whether the card got there via `PlayCardCommand`, a copy effect, an opponent's trigger, or a token spawn. State-based evaluation handles all sources uniformly.

```csharp
public IReadOnlyList<GameAction> Evaluate(
    GameAction justExecuted,
    CommandResult result,
    GameState state)
{
    var playArea = state.GetZone(WellKnownZones.PlayArea);
    if (playArea.HasCards)
        return new[] { new ExecuteCardCommand { Source = playArea.TopCard, ... } };

    return Array.Empty<GameAction>();
}
```

`justExecuted` and `result` are passed for logging and tie-breaking purposes — not as the primary lens. If your `Evaluate` has a `switch` on `justExecuted`'s type, that's a smell: the rule is probably state-based and the type-match is just shorthand that misses other paths into the same state.

`Evaluate` is *not* the place to mutate state slots like "last command resolved." Those updates flow through state mutators reacting to commands (see [doc 26 §9.5](26-pile-counter-sample-game.md)). `Evaluate` reads state and decides what runs next; it never writes.

### Ordering nuance vs. MTG state-based actions

In MTG, state-based actions check **before** triggers go onto the stack. In this framework the order is:

1. Action handler runs
2. After-fold runs (per-instance triggers)
3. Envelope closes
4. `Evaluate` runs

So in the framework, after-triggers fire *on the originating action* before `Evaluate` sees the post-action state. A game wanting strict MTG SBA ordering would need to encode death triggers etc. as state-based checks inside `Evaluate` rather than as `OnAfter<DamageAction>` triggers — or accept the framework's ordering. This is a known semantic difference, not a bug.

---

## 6. `OnQueueDrained` — Idle Hook

Called once after a drain cycle completes with the queue empty. Returns a `DrainResult` carrying two outputs: the available-actions snapshot for the player, and any follow-up commands the engine should dispatch (phase advance, end-of-game check, etc.).

```csharp
public sealed record DrainResult(
    IReadOnlyList<GameCommand> Snapshot,    // legal player actions; written to GameState.AvailableActions
    IReadOnlyList<GameCommand> Dispatch);   // follow-ups to enqueue
```

The engine processes the result:
- Writes `Snapshot` into `GameState.AvailableActions` via an internal mutator. The presentation layer reads it from there (see [doc 27](27-presentation-loop.md)).
- Dispatches each `Dispatch` command. If any are accepted, the drain re-enters and continues; idle is not a special exit path.

### Why return data instead of holding `GameEngine`

An earlier draft handed `OnQueueDrained` a `GameEngine` reference so it could call `engine.Dispatch(...)` and `engine.PublishAvailableActions(...)` directly. That made `OnQueueDrained` the only side-effecting method on the interface, and split snapshot publication across two output channels (the engine vs. the rule). Returning `DrainResult` keeps the rule purely observer-shaped — every method returns data, the engine handles all side effects — and unifies snapshot + dispatch into one structured return.

### Typical responsibilities

- **Snapshot computation** — enumerate candidate commands, run `handler.Validate` against each, return the legal subset.
- **Phase advance** — emit `new AdvancePhaseCommand()` in `Dispatch`.
- **Priority grant** — emit `new GrantPriorityCommand(activePlayer)` in `Dispatch`.
- **End-of-game check** — emit `new EndGameCommand(...)` in `Dispatch` if the game is over.
- **No-op idle** — return `new DrainResult(snapshot, [])` and just wait for input.

### Why the rule needs `IHandlerRegistry`

The snapshot builder walks candidate commands and calls `handler.Validate` for each. The handler registry is the lookup that finds the right `Validate` implementation per command type (typically keyed by `Type`). It is **read-only** here — the rule never invokes `Handle` directly.

```csharp
public interface IHandlerRegistry
{
    bool Validate(GameCommand command, GameState state, out string? reason);
}
```

(The full handler-dispatch shape is the subject of a forthcoming action-pipeline doc; see open items.)

---

## 7. The Four Rejection Paths — When To Use Which

This table is the canonical reference for command rejection across the framework. Doc 27 §9 has a layer-by-layer breakdown; this table summarises the routing decision.

| Need | Path | Where | Sees | Owner |
|---|---|---|---|---|
| "Don't let the player attempt this in UI" | **AvailableActions** | Presentation, before any dispatch | `GameState.AvailableActions` (snapshot) | Game UI / `AffordanceMap` |
| "Reject because the engine isn't accepting commands now" | **`OnIncomingCommand` Reject** | Engine intake (rule) | `IntakeContext`; never command body | `GameRules` |
| "Reject because this specific command is illegal" | **`Handler.Validate` Reject** | Engine intake (handler) | The typed `TCommand` + `GameState` | `ICommandHandler<TCommand,_>` |
| "Cancel this mid-execution based on a card effect" | **Before-fold `Cancel`** | Inside the pipeline | The constructed `GameAction`, ability `Context` | `OnBefore<T>` trigger |

### Order at intake

The engine runs `Handler.Validate` first (cheap, content-specific, catches malformed commands), then `OnIncomingCommand` (flow). Either can reject; the engine surfaces a single `IntakeResult` to the caller. See [doc 26 §9.7](26-pile-counter-sample-game.md) for the engine intake snippet.

### Rule of thumb

**The earliest path with enough information wins.** Pushing a check upstream means fewer wasted dispatches, less duplicated logic, and clearer UI:

- If the UI can know it's illegal before submitting → it shouldn't appear in the snapshot, and `Validate` doesn't even need to run.
- If the command is valid in principle but the engine isn't ready → the rule's intake rejects.
- If the command is malformed against current state → `Validate` rejects.
- If a card effect must cancel mid-resolution → before-fold cancel.

### Single source of truth

`Handler.Validate` is the *authoritative* legality check. The snapshot is built by running `Validate` against every candidate; the UI's `AffordanceMap` is a projection of the same answers. There is no duplicated legality logic across layers — each layer either calls `Validate` or projects its results.

---

## 8. What Does NOT Belong In `GameRules`

Concrete anti-scope. If you're tempted to put one of these in `GameRules`, look elsewhere:

| Don't put here | Put it in |
|---|---|
| **Command-specific legality** ("can this `MoveCard` run right now?") | `ICommandHandler<TCommand,_>.Validate` |
| **Game-affecting state** (last-move memory, stash buffers, phase counters) | `GameState` (rule holds zero gameplay memory — locked contract) |
| Action handler logic ("how does damage actually resolve") | `ICommandHandler<TCommand,_>.Handle` |
| State mutation reacting to a command | `IStateMutator<TCommand>` (Scaffold State) |
| Trigger reaction logic ("when this card is damaged, draw") | `AbilityDefinition.Register` on the card |
| Async / awaits / player-input prompts | Effects (orchestrators) and decision actions |
| Game session lifecycle (start, end, pause, concede, disconnect) | Separate `IGameSession` layer (out of scope) |
| Trigger ordering | `ITriggerPipeline` |
| Player input → command resolution | `IIntentMapper` |
| UI affordances | Presentation layer; `AffordanceMap` projects from `GameState.AvailableActions` (see [doc 27](27-presentation-loop.md)) |

`GameRules` is purely for **game-wide flow decisions during play**: when to accept commands, which triggers fire, what runs next based on state, what the snapshot looks like. Any logic that belongs to a specific card, action, or ability lives on that card, action, or ability.

### The "zero game-affecting state" rule

The rule may hold *configuration* — rule modules, lookup tables, cached evaluators that are deterministic functions of inputs. It must *not* hold gameplay memory. Test: if you reset the engine and replay all commands against a fresh `GameState`, does the rule produce identical outputs without any of its own state being touched? It should. If a private field on the rule influences an `IntakeResult` or `Evaluate` output, that field belongs in `GameState` instead.

---

## 9. Composition

A real game's `GameRules` will grow large — phase rules, SBAs, ordering rules, available-actions computation. The interface intentionally does not bake composition in; that's an authoring concern, not a framework primitive.

Recommended pattern (helper, not interface):

```csharp
public sealed class CompositeGameRules : GameRules
{
    private readonly IReadOnlyList<IRuleModule> modules;

    public IReadOnlyList<GameAction> Evaluate(GameAction action, CommandResult result, GameState state)
        => modules.SelectMany(m => m.Evaluate(action, result, state)).ToList();

    public DrainResult OnQueueDrained(GameState state, IHandlerRegistry handlers)
    {
        var snapshot = modules.SelectMany(m => m.OnQueueDrained(state, handlers).Snapshot).ToList();
        var dispatch = modules.SelectMany(m => m.OnQueueDrained(state, handlers).Dispatch).ToList();
        return new DrainResult(snapshot, dispatch);
    }

    // FilterTriggers chains each module's filter
    // OnIncomingCommand walks modules; first one that mutates the queue or rejects wins
}
```

This is a future authoring helper; the framework ships only the single `GameRules` interface. Games with one rule module implement it directly.

---

## 10. Open Items

- **`GameState` contract** — read-only boundary `GameRules` operates against. Likely Scaffold State package; blocked on canonical state decision (doc 06).
- **State-pattern decision** — does `OnIncomingCommand` receive `GameState` as a parameter (consistent with the other methods) or does the rule hold a constructor-injected reference? Drives the final shape of `IntakeContext`. (Doc 26 Q1.)
- **Action pipeline cleanup** — handler lookup, validate, execute, trigger folds need to be drawn explicitly in one place rather than scattered across docs 18/19/22/25. (Doc 26 Q2.)
- **`IHandlerRegistry` shape** — sketched here as `Validate(command, state, out reason)`. Final shape (typed lookup, error semantics, registration) resolves with the action-pipeline doc. (Doc 26 Q9.)
- **`IntakeContext.Pending` type** — currently `LinkedList<GameCommand>`. Confirm at engine implementation. (Doc 26 Q10.)
- **`IntakeResult.Reason`** — free-form string vs. structured enum. Defer until a real game forces the choice. (Doc 26 Q11.)
- **`FilterTriggers` default impl** — identity for trigger-less games. Probably means `GameRules` becomes an abstract base class with virtuals rather than a pure interface. Revisit at final rule shape. (Doc 26 Q13.)
- **MTG SBA ordering** — if a future game needs MTG-strict ordering, decide whether to add a pre-after-fold evaluator hook or document the workaround (encode SBAs inside `Evaluate` and accept the post-after-fold timing).
- **`CompositeGameRules` helper** — design when the first composed-rules game needs it.

### Resolved by docs 26 / 27 (no longer open)

- ~~`AvailableActionsSnapshot` ownership~~ — lives in `GameState.AvailableActions: IReadOnlyList<GameCommand>`; engine writes via internal mutator after each drain. (Doc 27 §11.)
- ~~Intake telemetry / scheduler observability~~ — moot; the rule mutates `IntakeContext.Pending` directly, no scheduler wrapper to observe. The queue itself is the trace.

---

## 11. Related

- [20-engine-evaluation-loop.md](20-engine-evaluation-loop.md) — the engine that drives `GameRules`
- [21-trigger-reaction-system.md](21-trigger-reaction-system.md) — trigger fold and subscription model
- [22-async-decisions-and-suspension.md](22-async-decisions-and-suspension.md) — async layer hierarchy
- [23-replacement-prevention-effects.md](23-replacement-prevention-effects.md) — `FilterTriggers`, replacement semantics
- [26-pile-counter-sample-game.md](26-pile-counter-sample-game.md) — sample game; concrete `GameRules` implementation against this contract
- [27-presentation-loop.md](27-presentation-loop.md) — UI loop, snapshot-in-state, four validation layers, `DrainResult`
- [06-open-questions.md](06-open-questions.md) — global open questions tracker
