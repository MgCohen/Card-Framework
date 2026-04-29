---
type: model
status: settled
last-updated: 2026-04-29
builds-on: [features/engine/notes/01-evaluation-loop.md]
tags: [#engine, #gamerules, #contract]
---

# GameRules — The Game's Rules Brain

This document locks the contract for `GameRules`: the game-specific decision layer plugged into the engine. The engine itself (loop, queue, pipeline wiring) is framework code described in [features/engine/notes/01-evaluation-loop.md](features/engine/notes/01-evaluation-loop.md). This doc is for the game author who has to implement `GameRules` for a specific game.

Builds on:

- [features/engine/notes/01-evaluation-loop.md](features/engine/notes/01-evaluation-loop.md) — engine loop, drain cycle, pipeline wiring
- [features/triggers/notes/01-trigger-fold.md](features/triggers/notes/01-trigger-fold.md) — trigger fold, OnBefore/OnAfter, subscription
- [features/effects/notes/01-async-dispatch.md](features/effects/notes/01-async-dispatch.md) — async layer hierarchy, dispatch paths
- [features/effects/notes/02-replacement-prevention.md](features/effects/notes/02-replacement-prevention.md) — `FilterTriggers`, `ITriggerPipeline`, replacement/prevention semantics
- [support/samples/pile-counter.md](support/samples/pile-counter.md) — the working sample game; drives this contract's shape
- [features/presentation/notes/01-presentation-loop.md](features/presentation/notes/01-presentation-loop.md) — UI loop, snapshot in state, four validation layers
- [features/engine/notes/03-snapshot-pipeline.md](features/engine/notes/03-snapshot-pipeline.md) — service-driven snapshot publication; reshapes `OnQueueDrained` and the rule's dependencies

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

`GameRules` is an abstract class with constructor-injected services. Methods take only what's ephemeral to the call; stable engine handles (state access, intake queue, handlers, snapshot writer) are services held on the rule. See [doc 29 §5](features/engine/notes/03-snapshot-pipeline.md) for the rationale.

```csharp
public abstract class GameRules
{
    protected IGameStateProvider State    { get; }
    protected IAvailableActions  Actions  { get; }
    protected IIntakeQueue       Pending  { get; }
    protected IHandlerRegistry   Handlers { get; }

    protected GameRules(
        IGameStateProvider state,
        IAvailableActions  actions,
        IIntakeQueue       pending,
        IHandlerRegistry   handlers)
    {
        State    = state;
        Actions  = actions;
        Pending  = pending;
        Handlers = handlers;
    }

    // 1. Intake. Pure flow: paused/ended gating + routing the command into
    //    the right structure (queue, internal stash, etc.). Never reads
    //    the command body — command-specific legality is the handler's job.
    public abstract IntakeResult OnIncomingCommand(
        GameCommand    command,
        DispatchOrigin origin);

    // 2. Trigger availability. Filters registered candidates for a given command and phase.
    public abstract IReadOnlyList<TriggerMatch> FilterTriggers(
        IReadOnlyList<TriggerMatch> candidates,
        GameCommand                 command,
        TriggerPhase                phase);

    // 3. Evaluator. Reads state after each action; returns follow-up commands.
    public abstract IReadOnlyList<GameAction> Evaluate(
        GameAction     justExecuted,
        CommandResult  result);

    // 4. Idle hook. Writes the snapshot through `Actions` (when it wants to)
    //    and returns any follow-up commands the engine should dispatch.
    public abstract DrainResult OnQueueDrained();
}
```

### Injected services

| Service | Purpose |
|---|---|
| `IGameStateProvider State` | `State.Current` returns the immutable `GameState` of right now. Read fresh per access. |
| `IAvailableActions Actions` | Snapshot writer — `Set` / `Append` / `Clear`. The rule (and effects) call this to publish; the engine never auto-publishes. See [doc 29 §2](features/engine/notes/03-snapshot-pipeline.md). |
| `IIntakeQueue Pending` | The live engine queue. Same operations the old `IntakeContext.Pending` exposed: `AddFirst` / `AddLast` / `Remove` / iterate. |
| `IHandlerRegistry Handlers` | Read-only handler lookup. The rule may call `Validate` on demand (e.g., to prune templates whose every binding fails); never invokes `Handle`. |

### Contract guarantees

- **All four methods are synchronous.** No `Task`, no awaits. `GameRules` is a pure decision layer; async work belongs to handlers, effects, and `ITriggerPipeline`.
- **Side effects flow through narrow capability services.** `OnIncomingCommand` mutates `Pending`. `OnQueueDrained` writes through `Actions`. `Evaluate` and `FilterTriggers` remain pure observer (return data, never side-effect). **The rule never holds `GameEngine`** — only the four narrow services above. (This relaxes the "all four methods observer-shaped" framing of earlier drafts; see [doc 29 §5](features/engine/notes/03-snapshot-pipeline.md).)
- **`GameRules` never mutates `GameState` directly.** State changes happen through dispatched `GameAction`s and the state-mutator pipeline (see [doc 26 §9.5](support/samples/pile-counter.md)). `Actions.Set` writes the `AvailableActions` slot through an internal mutator — that's the only state slot the rule writes, and it goes through the same pipeline.
- **`GameRules` holds zero game-affecting state.** This is a contract guarantee, not a suggestion. Services are *configuration*, not gameplay memory. The rule may hold service handles, lookup tables, cached evaluators — never gameplay memory. Anything game-affecting (last-move, phase, stash buffers, etc.) lives in `GameState`.
- **Command-specific legality is not the rule's concern.** Each `ICommandHandler<TCommand,TResult>.Validate` owns the legality of its own command. The rule is generic across game logic.
- **No method holds onto its arguments.** `TriggerMatch` lists and `command` / `result` parameters are valid only for the call. Caching them across calls is undefined. (Injected services *are* held — that's their purpose.)
- **Methods may be called concurrently from different drain cycles only if the engine guarantees it.** Today the engine serializes drain cycles, so `GameRules` does not need to be thread-safe.

---

## 3. `OnIncomingCommand` — Intake (Flow Only)

Called once for every command entering the dispatch channel, from any source: player input, timer, trigger reaction, internal follow-up, network. It runs *after* `handler.Validate` has confirmed the command is well-formed and legal in principle (see [§7](#7-rejection-paths-when-to-use-which) and [doc 26 §9.7](support/samples/pile-counter.md)), and *before* the command is on the queue.

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

public enum DispatchOrigin { PlayerIntent, TriggerReaction, Timer, Internal }
```

The intake queue is the injected `IIntakeQueue Pending` service. `CurrentlyExecuting` lives on the queue (or on the state provider — pin during engine implementation). `Origin` is the only ephemeral input and is passed as a parameter.

### Why the live queue (no scheduler wrapper)

`Pending` is the actual engine queue, with full read+write access — `AddFirst`, `AddLast`, `Remove`, iteration. This mirrors the trust pattern in `FilterTriggers` (give the rule the full data, no sandbox). An earlier draft used an `ICommandScheduler` wrapper to gate the API; we dropped it because there's no other API gating in `GameRules`, and stashing isn't a framework primitive — a rule that wants stashing just keeps an internal list (or its own state slot) and `AddFirst`/`AddLast`s on whatever signal releases it.

### How to express common behaviors

| Behavior | How the rule implements it |
|---|---|
| Append to queue (default) | `Pending.AddLast(command); return IntakeResult.Ok;` |
| Run before pending (instant-speed) | `Pending.AddFirst(command); return IntakeResult.Ok;` |
| Stash for later | Rule (or a state slot) keeps `command` in a side collection; release path adds it back to `Pending` from a signal handler or `OnQueueDrained` follow-up. Returns `IntakeResult.Ok` from intake. |
| Cancel a queued sibling | `Pending.Remove(otherCommand)` then add the new one. Allowed; rule's prerogative. |
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

The split between availability (`GameRules`) and ordering (`ITriggerPipeline`) is deliberate — see [doc 23 §3–4](features/effects/notes/02-replacement-prevention.md). `GameRules` says *which* triggers fire; `ITriggerPipeline` says *in what order*.

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

Called after every `GameAction` finishes executing. Reads current state via `State.Current`, returns 0..N actions to enqueue next.

The single most important point about `Evaluate`: **it reads state, not the just-executed command type.** If the rule is "any card sitting in PlayArea must be executed," that rule fires the same way whether the card got there via `PlayCardCommand`, a copy effect, an opponent's trigger, or a token spawn. State-based evaluation handles all sources uniformly.

```csharp
public override IReadOnlyList<GameAction> Evaluate(
    GameAction justExecuted,
    CommandResult result)
{
    var playArea = State.Current.GetZone(WellKnownZones.PlayArea);
    if (playArea.HasCards)
        return new[] { new ExecuteCardCommand { Source = playArea.TopCard, ... } };

    return Array.Empty<GameAction>();
}
```

`justExecuted` and `result` are passed for logging and tie-breaking purposes — not as the primary lens. If your `Evaluate` has a `switch` on `justExecuted`'s type, that's a smell: the rule is probably state-based and the type-match is just shorthand that misses other paths into the same state.

`Evaluate` is *not* the place to mutate state slots like "last command resolved." Those updates flow through state mutators reacting to commands (see [doc 26 §9.5](support/samples/pile-counter.md)). `Evaluate` reads state and decides what runs next; it never writes.

### Ordering nuance vs. MTG state-based actions

In MTG, state-based actions check **before** triggers go onto the stack. In this framework the order is:

1. Action handler runs
2. After-fold runs (per-instance triggers)
3. Envelope closes
4. `Evaluate` runs

So in the framework, after-triggers fire *on the originating action* before `Evaluate` sees the post-action state. A game wanting strict MTG SBA ordering would need to encode death triggers etc. as state-based checks inside `Evaluate` rather than as `OnAfter<DamageAction>` triggers — or accept the framework's ordering. This is a known semantic difference, not a bug.

---

## 6. `OnQueueDrained` — Idle Hook

Called once after a drain cycle completes with the queue empty. Returns a `DrainResult` carrying any follow-up commands the engine should dispatch (phase advance, end-of-game check, etc.). The available-actions snapshot is **not** returned here — the rule writes it through the injected `Actions` service whenever it wants to publish (typically once per drain). See [doc 29](features/engine/notes/03-snapshot-pipeline.md) for the full snapshot-production model.

```csharp
public sealed record DrainResult(IReadOnlyList<GameCommand> Dispatch)
{
    public static readonly DrainResult Empty = new(Array.Empty<GameCommand>());
}
```

The engine processes the result by dispatching each `Dispatch` command. If any are accepted, the drain re-enters and continues; idle is not a special exit path.

The rule typically writes the snapshot before returning:

```csharp
public override DrainResult OnQueueDrained()
{
    var templates = EnumerateLegalActions(State.Current);
    Actions.Set(new AvailableActionsSnapshot(new FreeInput(), templates));
    return DrainResult.Empty;
}
```

### Why a service, not a return value

An earlier draft returned `DrainResult { Snapshot, Dispatch }` and let the engine write the snapshot. That broke down once we wanted *effects* (mid-execution) to publish their own snapshots — target pickers, choose-one prompts, mid-resolution priority grants. A single write path through `IAvailableActions` lets the rule and effects share the same publication mechanism. The engine never auto-publishes; if nobody calls `Actions.Set`, `AvailableActions` does not change.

### Typical responsibilities

- **Snapshot publication** — enumerate templates, call `Actions.Set(snapshot)`. May filter at the per-template level (e.g., suppress "twice in a row"). May call `Handlers.Validate` on demand to prune templates whose every binding fails — that's an opt-in, not a default.
- **Phase advance** — emit `new AdvancePhaseCommand()` in `Dispatch`.
- **Priority grant** — emit `new GrantPriorityCommand(activePlayer)` in `Dispatch`.
- **End-of-game check** — emit `new EndGameCommand(...)` in `Dispatch` if the game is over.
- **One-shot games** — never publish past bootstrap; `OnQueueDrained` is a no-op (`return DrainResult.Empty;`).

### `IHandlerRegistry`

The handler registry is the lookup that finds the right `Validate` implementation per command type. It is **read-only** — the rule never invokes `Handle` directly.

```csharp
public interface IHandlerRegistry
{
    bool Validate(GameCommand command, GameState state, out string? reason);
}
```

The rule uses it only when it explicitly chooses to pre-validate bindings (e.g., to prune templates with no legal bindings). The default flow does not call `Validate` during snapshot construction — per-binding legality is enforced at dispatch.

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

The engine runs `Handler.Validate` first (cheap, content-specific, catches malformed commands), then `OnIncomingCommand` (flow). Either can reject; the engine surfaces a single `IntakeResult` to the caller. See [doc 26 §9.7](support/samples/pile-counter.md) for the engine intake snippet.

### Rule of thumb

**The earliest path with enough information wins.** Pushing a check upstream means fewer wasted dispatches, less duplicated logic, and clearer UI:

- If the UI can know it's illegal before submitting → it shouldn't appear in the snapshot, and `Validate` doesn't even need to run.
- If the command is valid in principle but the engine isn't ready → the rule's intake rejects.
- If the command is malformed against current state → `Validate` rejects.
- If a card effect must cancel mid-resolution → before-fold cancel.

### Single source of truth

`Handler.Validate` is the *authoritative* legality check, run at dispatch. The snapshot is the rule's best-effort approximation built from per-template eligibility; per-binding `Validate` may be called on demand by the rule, but it is not the default. The UI's `AffordanceMap` is a projection of the snapshot. The authoritative check happens at dispatch — if the snapshot is approximate, dispatch catches stale bindings and surfaces the rejection (toast). See [doc 29 §2](features/engine/notes/03-snapshot-pipeline.md) for the on-demand validation posture.

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
| UI affordances | Presentation layer; `AffordanceMap` projects from `GameState.AvailableActions` (see [doc 27](features/presentation/notes/01-presentation-loop.md)) |

`GameRules` is purely for **game-wide flow decisions during play**: when to accept commands, which triggers fire, what runs next based on state, what the snapshot looks like. Any logic that belongs to a specific card, action, or ability lives on that card, action, or ability.

### The "zero game-affecting state" rule

The rule may hold *configuration* — rule modules, lookup tables, cached evaluators that are deterministic functions of inputs. It must *not* hold gameplay memory. Test: if you reset the engine and replay all commands against a fresh `GameState`, does the rule produce identical outputs without any of its own state being touched? It should. If a private field on the rule influences an `IntakeResult` or `Evaluate` output, that field belongs in `GameState` instead.

---

## 9. Composition

A real game's `GameRules` will grow large — phase rules, SBAs, ordering rules, available-actions computation. The interface intentionally does not bake composition in; that's an authoring concern, not a framework primitive.

The `IAvailableActions` service surface (`Set` / `Append` / `Clear`, see [doc 29 §2](features/engine/notes/03-snapshot-pipeline.md)) was designed to support module composition mechanically — multiple modules can `Append` contributions and an override module can `Set` to replace. The composition *contract* (provider/policy ordering, enable-disable, conflict resolution) is deliberately undesigned for now; pin when the first composed-rules game forces it.

For games with one rule module, implement `GameRules` directly. The constructor takes the four injected services; the four methods do the work.

---

## 10. Open Items

- **`GameState` contract** — read-only boundary `GameRules` operates against. Likely Scaffold State package; blocked on canonical state decision (doc 06).
- **`IGameStateProvider` shape** — `State.Current` returns the immutable state. Subscriptions probably live on the Store (presentation), not the provider. Confirm at engine implementation. ([Doc 29 §9](features/engine/notes/03-snapshot-pipeline.md).)
- **`IIntakeQueue` shape** — same operations the old `LinkedList<GameCommand>` exposed. `CurrentlyExecuting` lives on the queue or on the state provider. Pin at engine implementation. ([Doc 29 §9](features/engine/notes/03-snapshot-pipeline.md).)
- **Action pipeline cleanup** — handler lookup, validate, execute, trigger folds need to be drawn explicitly in one place rather than scattered across docs 18/19/22/25. (Doc 26 Q2.)
- **`IHandlerRegistry` shape** — sketched here as `Validate(command, state, out reason)`. Final shape (typed lookup, error semantics, registration) resolves with the action-pipeline doc. (Doc 26 Q9.)
- **`IntakeResult.Reason`** — free-form string vs. structured enum. Defer until a real game forces the choice. (Doc 26 Q11.)
- **`FilterTriggers` default impl** — identity for trigger-less games. Now possible since `GameRules` is an abstract class — provide a virtual default. (Doc 26 Q13.)
- **Effect-side service symmetry** — effects mid-execution need the same `IAvailableActions` (and possibly `IGameStateProvider`) the rule has. Likely through `IHandlerContext`. Needs an effect-flow doc covering the full lifecycle. ([Doc 29 §7](features/engine/notes/03-snapshot-pipeline.md).)
- **MTG SBA ordering** — if a future game needs MTG-strict ordering, decide whether to add a pre-after-fold evaluator hook or document the workaround (encode SBAs inside `Evaluate` and accept the post-after-fold timing).
- **`GameRules` composition** — module composition contract (provider/policy ordering, enable-disable). Service surface supports it mechanically; pin when first composed game forces it.

### Resolved

- ~~`AvailableActionsSnapshot` ownership~~ — lives in `GameState.AvailableActions`; rule writes through `Actions.Set` (doc 29 §2). Engine never auto-publishes.
- ~~Intake telemetry / scheduler observability~~ — moot; the rule mutates `Pending` directly, no scheduler wrapper to observe. The queue itself is the trace.
- ~~State-pattern decision~~ — services injected at construction; methods take only ephemeral data. State is read via `State.Current` ([doc 29 §5](features/engine/notes/03-snapshot-pipeline.md)).
- ~~`IntakeContext.Pending` type~~ — collapsed into the injected `IIntakeQueue` service ([doc 29 §5](features/engine/notes/03-snapshot-pipeline.md)).
- ~~Snapshot element type~~ — typed `ActionTemplate` per command type; carries `CommandType` and `Source` on the base ([doc 29 §3](features/engine/notes/03-snapshot-pipeline.md)).

---

## 11. Related

- [features/engine/notes/01-evaluation-loop.md](features/engine/notes/01-evaluation-loop.md) — the engine that drives `GameRules`
- [features/triggers/notes/01-trigger-fold.md](features/triggers/notes/01-trigger-fold.md) — trigger fold and subscription model
- [features/effects/notes/01-async-dispatch.md](features/effects/notes/01-async-dispatch.md) — async layer hierarchy
- [features/effects/notes/02-replacement-prevention.md](features/effects/notes/02-replacement-prevention.md) — `FilterTriggers`, replacement semantics
- [support/samples/pile-counter.md](support/samples/pile-counter.md) — sample game; concrete `GameRules` implementation against this contract
- [features/presentation/notes/01-presentation-loop.md](features/presentation/notes/01-presentation-loop.md) — UI loop, snapshot-in-state, four validation layers
- [features/actions/notes/02-availability-snapshot.md](features/actions/notes/02-availability-snapshot.md) — snapshot shape (typed templates)
- [features/engine/notes/03-snapshot-pipeline.md](features/engine/notes/03-snapshot-pipeline.md) — service-driven snapshot publication; reshapes this doc's `OnQueueDrained` and rule dependencies
- [general/OPEN-QUESTIONS.md](general/OPEN-QUESTIONS.md) — global open questions tracker
