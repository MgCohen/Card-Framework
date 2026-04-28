# GameRules — The Game's Rules Brain

This document locks the contract for `GameRules`: the game-specific decision layer plugged into the engine. The engine itself (loop, queue, pipeline wiring) is framework code described in [20-engine-evaluation-loop.md](20-engine-evaluation-loop.md). This doc is for the game author who has to implement `GameRules` for a specific game.

Builds on:

- [20-engine-evaluation-loop.md](20-engine-evaluation-loop.md) — engine loop, drain cycle, pipeline wiring
- [21-trigger-reaction-system.md](21-trigger-reaction-system.md) — trigger fold, OnBefore/OnAfter, subscription
- [22-async-decisions-and-suspension.md](22-async-decisions-and-suspension.md) — async layer hierarchy, dispatch paths
- [23-replacement-prevention-effects.md](23-replacement-prevention-effects.md) — `FilterTriggers`, `ITriggerPipeline`, replacement/prevention semantics

---

## 1. What `GameRules` Is

`GameRules` is the **game's decision layer**. The framework engine drives the loop; `GameRules` answers four questions during that loop:

1. *Should this incoming command run? When?* — intake scheduling
2. *Which registered triggers are eligible for this command?* — trigger availability
3. *Given the state after this action, what runs next?* — state-based follow-up (the evaluator)
4. *The queue is empty. What now?* — idle / phase / available-actions / end-of-game

It is **not** a place for action handlers, ability logic, state mutation, or async work. See §8 for the explicit anti-scope.

### Position in the system

| Layer | Owner | Lives in |
|---|---|---|
| Engine loop, queue, drain | Framework | `GameEngine` |
| Pipeline (before fold → handler → after fold → envelope) | Framework | `ICommandPipeline` |
| Trigger ordering + fold execution | Framework (swappable) | `ITriggerPipeline` |
| Action handlers | Per-action | `CommandHandler` |
| Trigger reaction logic | Per-ability | `AbilityDefinition.Register` |
| **Game-wide decisions** | **Game** | **`GameRules`** |
| Game session lifecycle (start, end, pause) | Game | Separate `IGameSession` (out of scope here) |

---

## 2. The Interface

```csharp
public interface GameRules
{
    // 1. Intake. Decides scheduling for every dispatched command, regardless of source.
    SchedulingDecision OnIncomingCommand(GameCommand command, SchedulingContext context);

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

    // 4. Idle hook. Fires when the queue drains.
    void OnQueueDrained(GameEngine engine, GameState state);
}
```

### Contract guarantees

- **All four methods are synchronous.** No `Task`, no awaits. `GameRules` is a pure decision layer; async work belongs to handlers, effects, and `ITriggerPipeline`.
- **All four methods are observers.** They read `GameState` and return decisions. They never mutate state directly. The only way `GameRules` causes a state change is by returning a `GameAction` (from `Evaluate`) or dispatching one (from `OnQueueDrained`).
- **No method holds onto its arguments.** `GameState`, `Context`, and `TriggerMatch` lists are valid only for the call. Caching them across calls is undefined.
- **Methods may be called concurrently from different drain cycles only if the engine guarantees it.** Today the engine serializes drain cycles, so `GameRules` does not need to be thread-safe.

---

## 3. `OnIncomingCommand` — Intake Scheduling

Called once for every command entering the dispatch channel, from any source: player input, timer, trigger reaction, internal follow-up, network. It runs *before* the command is enqueued.

```csharp
public sealed record SchedulingDecision(
    SchedulingKind Kind,
    string?        Reason);   // populated only for Discard

public enum SchedulingKind { RunNext, Enqueue, Stash, Discard }

public sealed record SchedulingContext(
    GameState                       State,
    IReadOnlyList<GameCommand>      Pending,            // queue snapshot
    GameCommand?                    CurrentlyExecuting, // null if idle
    DispatchOrigin                  Origin);            // PlayerIntent | TriggerReaction | Timer | Internal

public enum DispatchOrigin { PlayerIntent, TriggerReaction, Timer, Internal }
```

### When to use which kind

| Kind | Meaning | Typical use |
|---|---|---|
| `RunNext` | Push to front of queue; runs before anything else pending | MTG instant-speed reactions, "quickdraw" mechanics |
| `Enqueue` | Append to queue (default) | Slay the Spire reactions, timers |
| `Stash` | Hold outside the queue; release later via game-specific mechanism | MTG triggers waiting for priority resolution |
| `Discard` | Drop the command; populate `Reason` for UI surfacing | Phase-violation, illegal-from-source, stale intent |

### `Reason` payload

`Reason` is a free-form string, populated only when `Kind == Discard`. The gateway / UI layer is responsible for formatting it for the player. The engine itself does not inspect or display it.

> **Open:** Whether `Reason` should be a structured enum (`PhaseLocked`, `NotYourTurn`, `IllegalAfterReplacement`, …) versus free-form text. Free-form is simpler and less coupling; structured allows localization. Defer until a real game forces the choice.

### Discard vs. before-fold Cancel vs. AvailableActions reject

Three rejection paths exist. They are not redundant — they fire at different points:

| Path | Where it runs | Sees | Use it for |
|---|---|---|---|
| **AvailableActions** (gateway) | Before any dispatch | `PlayerIntent` only | Player-facing legality the UI also surfaces |
| **`OnIncomingCommand` Discard** | At intake, before enqueue | Any `GameCommand` from any source | Phase violations, source-agnostic rejection, stale follow-ups |
| **Before-fold `Cancel`** | Inside the pipeline | The fully constructed `GameAction` mid-execution | Damage-prevention shields, ability-counter effects |

Rule of thumb: prefer the earliest path that has the information needed. If the gateway can know, reject there; if the rule requires post-dispatch state, use intake; if the rule is itself a card effect, use the before-fold.

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

Return the candidates unchanged. Games that have no eligibility logic — most simple games — implement this as a one-liner.

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

### Ordering nuance vs. MTG state-based actions

In MTG, state-based actions check **before** triggers go onto the stack. In this framework the order is:

1. Action handler runs
2. After-fold runs (per-instance triggers)
3. Envelope closes
4. `Evaluate` runs

So in the framework, after-triggers fire *on the originating action* before `Evaluate` sees the post-action state. A game wanting strict MTG SBA ordering would need to encode death triggers etc. as state-based checks inside `Evaluate` rather than as `OnAfter<DamageAction>` triggers — or accept the framework's ordering. This is a known semantic difference, not a bug.

---

## 6. `OnQueueDrained` — Idle Hook

Called once after a drain cycle completes with the queue empty. This is where game-specific phase logic lives.

Typical responsibilities:

- Phase advance (`engine.Dispatch(new AdvancePhaseCommand())`)
- Priority grant (`engine.Dispatch(new GrantPriorityCommand(activePlayer))`)
- End-of-game check (`engine.Dispatch(new EndGameCommand(...))`)
- `AvailableActionsSnapshot` publication (see [doc 20 §10](20-engine-evaluation-loop.md))
- No-op (e.g., Slay the Spire mid-turn — just await input)

`OnQueueDrained` may dispatch back into the engine. The drain re-enters and continues; it is not a special exit path.

It is the **only** place in the `GameRules` interface that holds `GameEngine`. The other three methods take state and arguments only. This is intentional: `OnQueueDrained` is the one place where dispatching new commands is the *primary* action, not an incidental return value.

---

## 7. The Three Rejection Paths — When To Use Which

This table is the canonical reference for command rejection across the framework:

| Need | Path | Method | Sees |
|---|---|---|---|
| "Don't let the player attempt this in UI" | AvailableActions | `OnQueueDrained` publishes snapshot; gateway reads it | `PlayerIntent`, `GameState` |
| "Reject this regardless of source" | Intake | `OnIncomingCommand` returns `Discard` | Any `GameCommand`, `SchedulingContext` |
| "Cancel this mid-execution based on a card effect" | Before-fold | `OnBefore<T>` trigger returns `FoldResult.Cancel` | The constructed `GameAction`, ability `Context` |

Rule of thumb: **the earliest path with enough information wins.** Pushing a check upstream means more legibility, fewer wasted dispatches, and clearer UI.

---

## 8. What Does NOT Belong In `GameRules`

Concrete anti-scope. If you're tempted to put one of these in `GameRules`, look elsewhere:

| Don't put here | Put it in |
|---|---|
| Action handler logic ("how does damage actually resolve") | `CommandHandler` for `DamageAction` |
| Trigger reaction logic ("when this card is damaged, draw") | `AbilityDefinition.Register` on the card |
| State mutation | Dispatched `GameAction` → handler |
| Async / awaits / player-input prompts | Effects (orchestrators) and decision actions |
| Game session lifecycle (start, end, pause, concede, disconnect) | Separate `IGameSession` layer (out of scope) |
| Trigger ordering | `ITriggerPipeline` |
| State storage | `GameState` (read-only here) |
| UI affordances | Gateway + `AvailableActionsSnapshot` |

`GameRules` is purely for **game-wide decisions during play**: what runs, in what order, when to stop. Any logic that belongs to a specific card, action, or ability lives on that card, action, or ability.

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

    // FilterTriggers chains each module's filter
    // OnIncomingCommand returns the first non-default decision
    // OnQueueDrained calls each module in order
}
```

This is a future authoring helper; the framework ships only the single `GameRules` interface. Games with one rule module implement it directly.

---

## 10. Open Items

- **`GameState` contract** — read-only boundary `GameRules` operates against. Blocked on canonical state decision (doc 06).
- **`SchedulingContext.Pending` shape** — whether it's a snapshot, a live view, or just a count. Depends on engine-internal queue type. Resolve when implementing.
- **`SchedulingDecision.Reason`** — free-form string vs. structured enum. Defer until a real game forces the choice.
- **`AvailableActionsSnapshot`** — currently described in [doc 20 §10](20-engine-evaluation-loop.md). Whether it formally lives on `GameRules` (publishing method) or stays a state artifact written in `OnQueueDrained` is a presentation choice; current convention is the latter.
- **MTG SBA ordering** — if a future game needs MTG-strict ordering, decide whether to add a pre-after-fold evaluator hook or document the workaround (encode SBAs inside `Evaluate` and accept the post-after-fold timing).
- **`CompositeGameRules` helper** — design when the first composed-rules game needs it.

---

## 11. Related

- [20-engine-evaluation-loop.md](20-engine-evaluation-loop.md) — the engine that drives `GameRules`
- [21-trigger-reaction-system.md](21-trigger-reaction-system.md) — trigger fold and subscription model
- [22-async-decisions-and-suspension.md](22-async-decisions-and-suspension.md) — async layer hierarchy
- [23-replacement-prevention-effects.md](23-replacement-prevention-effects.md) — `FilterTriggers`, replacement semantics
- [06-open-questions.md](06-open-questions.md) — open items
