---
type: sample
status: locked
last-updated: 2026-04-29
builds-on: [features/engine/models/02-gamerules-contract.md, features/actions/models/01-command-pipeline.md]
tags: [#sample, #pile-counter]
---

# Pile Counter — Reference Sample Game

> **Status: locked.** Re-checked against `features/engine/models/02-gamerules-contract.md` during migration phase 5 — the four-part lock-bar (§4.4 of PRODUCT-BUILDER-PROPOSAL) holds: (1) Subject and Projection are coherent; (2) projection covers the contracts the GameRules class exposes; (3) "what this does not exercise" remains honest; (4) no breaking renames since session 9 lock. If a future Model edit invalidates the projection, downgrade to `draft` and re-lock.

This document defines a small, deliberately simple game we use as a working reference while shaping the framework. Whenever a framework concept feels under- or over-specified, we sketch its implementation in **Pile Counter** and see where the seams are.

The game has no triggers, no abilities, no players-vs-players, no async decisions. That's intentional — it's the floor of what the framework must support, before any of the harder features pile on.

---

## 1. Concept

A single-player puzzle. The deck is split across four visible piles; the player slides cards between piles, trying to make each pile's total score equal its target.

---

## 2. Components

### Card

- **Number** — integer in `[1..10]`. Numbers may repeat across the deck (12 cards into a 1..10 range guarantees at least two duplicates).
- **Image** — purely cosmetic; the framework treats it as opaque.

The deck contains **12 cards**. Each card is a distinct *instance* even if two share the same `Number`.

### Pile (Zone)

- **Stack semantics** — cards are stacked; only the top card is visible to the player.
- **Score** — the sum of every card's `Number` in the pile. Visible.
- **Target** — a fixed integer assigned at setup. Visible.
- **Top card** — the most recently added card. Its `Number` is visible; cards beneath are hidden.

There are exactly four piles. They are positional/cosmetic — no pile is special by identity.

### Game

- 12 cards, 4 piles, 4 target scores.
- No turn timer, no move limit, no opponent.

---

## 3. Setup

1. Build the deck (12 cards, each with a `Number` in `[1..10]`; duplicates allowed).
2. Shuffle.
3. Distribute cards across the four piles — exactly 3 cards per pile.
4. Assign each pile a target score. Targets are provided as setup input — see open questions for where they come from.
5. Compute initial pile scores (sum of `Number`s per pile).
6. Game enters its main loop.

---

## 4. Player Actions

There is exactly one action: **MoveTopCard**.

- **Input:** source pile, destination pile.
- **Legality:**
  - Source pile must be non-empty.
  - Source ≠ destination.
  - The card being moved (the top of the source) must not be the *same card instance* moved on the immediately previous move. Two distinct cards that happen to share the same `Number` are not the same card — only instance identity matters.

That is the entire action surface. No other player input exists during play.

---

## 5. Game Loop

```
loop:
    player picks (source, destination)
    if move illegal → reject, no state change
    else:
        top card of source moves to top of destination
        recompute scores for source and destination
        if all four pile scores equal their targets → win, game ends
        else → continue
```

No phases beyond "play until won." No automatic advancement, no idle effects.

---

## 6. Win Condition

After a move resolves, if `pile.Score == pile.Target` for **all four piles** simultaneously, the game ends and the player wins.

There is no lose condition. The puzzle is always solvable from setup; the player keeps moving until they hit it.

---

## 7. What Pile Counter Tests In The Framework

This game forces the framework to express:

- **Zones with stack ordering and a derived score.** The score is a *computed* per-zone value — the framework needs a way to expose this without forcing the game author to recompute it manually after every change. (Or to recompute manually but cleanly.)
- **A single action type with legality rules.** Tests `OnIncomingCommand` rejection, `AvailableActions` (the gateway needs to tell the UI which moves are legal), and how legality interacts with the "can't move same card twice in a row" memory.
- **State-based win check.** Classic `Evaluate` job: after each action, read state, decide if the game is over, dispatch an `EndGameCommand` (or equivalent).
- **Move history of size 1.** "Last card moved" is a sliver of state that doesn't fit on a card or a zone — it's *game-level* memory. Where does it live?

This game does **not** test:

- Triggers, abilities, replacement effects, ongoing effects.
- Async decisions (no targeting, no choice prompts).
- Multi-player or priority.
- Phase transitions.
- Card definitions vs. instances (cards are pure data — number + image).

We will revisit those features against more elaborate sample games once the floor is solid.

---

## 8. Resolved Setup Decisions

- **Deck size:** 12 cards.
- **Numbering:** each card's `Number` is in `[1..10]`; duplicates allowed (and inevitable since 12 > 10).
- **Distribution:** 3 cards per pile, even split.
- **"Twice in a row" rule:** by card *instance* identity. Two cards sharing the same `Number` are still distinct.
- **Empty piles:** allowed. An empty pile has score `0` and still has a target. Winning is possible with an empty pile if its target is `0`.
- **Puzzle source:** an external **puzzle factory** produces a complete `(deck, deal, targets)` triple. Internally the factory uses random generation plus a solvability check, but that is opaque to the game — `GameRules` just receives the triple as setup input. No target generation, shuffling, or solvability logic lives inside the framework.
- **Undo:** out of scope.
- **Restart / new puzzle:** session-lifecycle concern, out of scope for `GameRules`.

All setup decisions are now resolved. We can sketch `GameRules` against this spec.

---

## 9. `GameRules` Implementation Sketch

This is the working sketch of Pile Counter against the framework as of this writing. It informs (and is informed by) docs 25 and 27. When the framework changes, update this sketch first and propagate.

### 9.1 State shape

```csharp
public sealed class GameState
{
    public IReadOnlyList<Pile>        Piles            { get; }
    public LastMove?                  LastMove         { get; }   // null at game start
    public AvailableActionsSnapshot   AvailableActions { get; }   // typed templates + scope
}

public sealed record LastMove(InstanceId Card, PileId From, PileId To);

public sealed class Pile
{
    public PileId               Id     { get; }
    public int                  Target { get; }
    public IReadOnlyList<Card>  Cards  { get; }   // top of stack at end
    public Card                 TopCard => Cards[^1];
    public int                  Score   => Cards.Sum(c => c.Number);
}
```

`AvailableActionsSnapshot` carries `Scope` + typed `ActionTemplate` list — see [doc 29 §3–§4](features/engine/models/03-snapshot-pipeline.md).

State is immutable, redux-like. All game-affecting memory lives here — the rule holds none.

### 9.2 Commands, intents, results

```csharp
// Player intent (UI emits this on drop)
public sealed record MoveCardIntent(InstanceId Card, PileId Destination) : PlayerIntent;

// Engine commands (fully resolved)
public sealed record MoveCardCommand(InstanceId Card, PileId From, PileId To) : GameCommand;
public sealed record EndGameCommand(Outcome Outcome) : GameCommand;

// Result
public sealed record MoveCardResult(InstanceId MovedCard) : CommandResult;

public enum Outcome { Win }
```

Note the framework primitive is **generic** `MoveCardCommand` — not `MoveTopCardCommand`. The "top only" rule is a Pile Counter constraint enforced in `Validate`, not a framework concept.

### 9.3 Intent mapper (game-specific)

```csharp
public sealed class PileCounterIntentMapper : IIntentMapper
{
    private readonly IGameStateProvider state;
    public PileCounterIntentMapper(IGameStateProvider state) => this.state = state;

    public GameCommand Map(ActionTemplate template, PlayerChoice choice) => template switch
    {
        MoveCardTemplate m => new MoveCardCommand(
            Card: m.Card,
            From: state.Current.LocateCard(m.Card),
            To:   (PileId)choice.Targets[0]),
        _ => throw new InvalidOperationException($"unknown template: {template}")
    };
}
```

The mapper switches on the typed template (no string-keyed slot lookup) and resolves the implicit `From` by reading current state. See [doc 29 §3](features/engine/models/03-snapshot-pipeline.md) for the typed-template binding pattern.

### 9.4 Handler (validate + handle)

Handlers own command-specific legality and the state mutation.

```csharp
public interface ICommandHandler<TCommand, TResult>
    where TCommand : GameCommand
    where TResult : CommandResult
{
    bool Validate(TCommand command, GameState state, out string? reason);
    TResult Handle(TCommand command, IHandlerContext ctx);
}

public sealed class MoveCardHandler : ICommandHandler<MoveCardCommand, MoveCardResult>
{
    public bool Validate(MoveCardCommand move, GameState state, out string? reason)
    {
        if (move.From == move.To)
        { reason = "source and destination must differ"; return false; }

        var source = state.Piles[move.From];
        if (source.Cards.Count == 0)
        { reason = "source pile is empty"; return false; }

        if (source.TopCard.Id != move.Card)
        { reason = "only the top card of a pile can be moved"; return false; }

        if (state.LastMove?.Card == move.Card)
        { reason = "can't move the same card twice in a row"; return false; }

        reason = null; return true;
    }

    public MoveCardResult Handle(MoveCardCommand move, IHandlerContext ctx)
    {
        ctx.Mutate.MoveCardBetweenPiles(move.Card, move.From, move.To);
        return new MoveCardResult(move.Card);
    }
}
```

### 9.5 State mutators

The Scaffold State package wires command-reactive mutators that produce new immutable state.

```csharp
public sealed class LastMoveMutator : IStateMutator<MoveCardCommand>
{
    public GameState Reduce(GameState state, MoveCardCommand cmd)
        => state with { LastMove = new LastMove(cmd.Card, cmd.From, cmd.To) };
}
```

The handler doesn't have to know about `LastMove`; `LastMoveMutator` reacts to `MoveCardCommand` automatically.

### 9.6 The rule

```csharp
public sealed class PileCounterRules : GameRules
{
    public PileCounterRules(
        IGameStateProvider state,
        IAvailableActions  actions,
        IIntakeQueue       pending,
        IHandlerRegistry   handlers)
        : base(state, actions, pending, handlers) { }

    public override IntakeResult OnIncomingCommand(GameCommand command, DispatchOrigin origin)
    {
        // Pure flow: no pause, no global lockouts, no stashing in this game.
        Pending.AddLast(command);
        return IntakeResult.Ok;
    }

    public override IReadOnlyList<TriggerMatch> FilterTriggers(
        IReadOnlyList<TriggerMatch> candidates,
        GameCommand command, TriggerPhase phase)
        => candidates;   // no triggers in this game

    public override IReadOnlyList<GameAction> Evaluate(GameAction _, CommandResult __)
    {
        if (State.Current.Piles.All(p => p.Score == p.Target))
            return new[] { new EndGameCommand(Outcome.Win) };
        return Array.Empty<GameAction>();
    }

    public override DrainResult OnQueueDrained()
    {
        var s = State.Current;
        var templates = new List<ActionTemplate>();

        foreach (var pile in s.Piles.Where(p => p.Cards.Count > 0))
        {
            var top = pile.TopCard;
            if (s.LastMove?.Card == top.Id) continue;   // per-template suppression

            var destinations = s.Piles
                .Where(p => p.Id != pile.Id)
                .Select(p => (EntityId)p.Id)
                .ToList();

            templates.Add(new MoveCardTemplate(top.Id, destinations));
        }

        Actions.Set(new AvailableActionsSnapshot(new FreeInput(), templates));
        return DrainResult.Empty;
    }
}
```

The rule constructs typed `ActionTemplate` entries (per-template enumeration). Per-binding legality runs at dispatch via `MoveCardHandler.Validate`; the rule does *not* pre-validate every (source × destination) combination during snapshot build. The "twice in a row" rule is per-template suppression — it skips the whole template for that card. See [doc 29 §2–§3](features/engine/models/03-snapshot-pipeline.md).

Almost everything else Pile Counter cares about lives elsewhere — handler `Validate` for legality, mutators for state changes, presentation layer for affordances.

### 9.7 Engine intake order

```csharp
public IntakeResult Dispatch(GameCommand command, DispatchOrigin origin)
{
    // 1. Command-specific legality (handler).
    if (!handlers.Validate(command, state, out var reason))
        return IntakeResult.Reject(reason);

    // 2. Flow gating + routing (rule). Rule reads state via its injected provider
    //    and mutates the queue via its injected IIntakeQueue.
    var result = rules.OnIncomingCommand(command, origin);

    if (result.Accepted && !IsDraining && pending.Count > 0)
        BeginDrain();

    return result;
}
```

Validate first (catches malformed commands cheaply), then rule's flow check. Both can reject; both produce the same `IntakeResult` shape. `IntakeContext` is gone — `Pending` is the rule's injected service, `Origin` is the only ephemeral input.

### 9.8 Bootstrap

```csharp
public sealed record Puzzle(
    IReadOnlyList<Card>                                    Deck,
    IReadOnlyDictionary<PileId, IReadOnlyList<InstanceId>> Deal,
    IReadOnlyDictionary<PileId, int>                       Targets);

public interface IPuzzleFactory { Puzzle Create(); }

public static class PileCounterBootstrap
{
    public static GameEngine Create(IPuzzleFactory factory)
    {
        var puzzle       = factory.Create();
        var initialState = BuildInitialState(puzzle);

        // Engine constructs the four services and injects them into the rule.
        var builder = new GameEngineBuilder()
            .WithInitialState(initialState)
            .WithHandlers(new MoveCardHandler())
            .WithMutators(new LastMoveMutator())
            .WithRule(services => new PileCounterRules(
                services.State,
                services.Actions,
                services.Pending,
                services.Handlers))
            .WithIntentMapper(services => new PileCounterIntentMapper(services.State));

        return builder.Build();
    }

    private static GameState BuildInitialState(Puzzle p)
    {
        var piles = p.Deal.Select(kv => new Pile(
            id:     kv.Key,
            target: p.Targets[kv.Key],
            cards:  kv.Value.Select(id => p.Deck.First(c => c.Id == id)).ToList()
        )).ToList();

        return new GameState(
            piles,
            lastMove:         null,
            availableActions: AvailableActionsSnapshot.Empty);
    }
}
```

The engine builder constructs the four canonical services (`IGameStateProvider`, `IAvailableActions`, `IIntakeQueue`, `IHandlerRegistry`) and exposes them through a `services` bag the game uses to wire the rule and intent mapper. After `Build` returns, the engine is idle. First `OnQueueDrained` runs, the rule calls `Actions.Set(...)`, presentation layer renders. See [doc 27](features/presentation/models/01-presentation-loop.md) for the presentation loop and [doc 29 §5](features/engine/models/03-snapshot-pipeline.md) for the service-injection rationale.

---

## 10. Framework Decisions Surfaced From This Sketch

These are decisions the Pile Counter sketch forced or confirmed. Each affects the framework, not just this game.

- **Generic primitive + rule-enforced constraint.** Framework ships generic `MoveCardCommand`; "top only" is enforced in `Validate`. No `MoveTopCardCommand` primitive.
- **Validation belongs to the handler.** `ICommandHandler.Validate(cmd, state, out reason)`. The rule never switches on command types.
- **`Validate` signature is Try-pattern.** `bool Validate(..., out string? reason)`.
- **Rule holds zero game-affecting state.** `LastMove` lives in `GameState`, written by a mutator. Locked as contract.
- **State mutators react to commands** (Scaffold State pattern). Handlers don't have to know about ancillary state slots.
- **Player input layer is three-tier:** `PlayerIntent` (UI) → `IIntentMapper` → `GameCommand` (engine).
- **`OnIncomingCommand` is purely flow.** Pause/lockout/stash routing only. Never inspects command body.
- **Snapshot lives in `GameState.AvailableActions: AvailableActionsSnapshot`.** Typed templates with scope discriminator. The rule writes through `IAvailableActions.Set` ([doc 29](features/engine/models/03-snapshot-pipeline.md)).
- **`AffordanceMap` is game-specific** and lives in the UI/gateway layer. Computed from snapshot. Framework doesn't define it.
- **`OnQueueDrained` returns `DrainResult { Dispatch }` only.** Snapshot publication moved to the injected `IAvailableActions` service. Supersedes earlier `DrainResult { Snapshot, Dispatch }`.
- **Four rejection paths:** AvailableActions (UI prophylactic) / `OnIncomingCommand` (engine flow) / `handler.Validate` (command legality, authoritative) / Before-fold `Cancel` (card-effect).
- **Setup is a factory + bootstrap external to the engine.** Factory produces a `Puzzle` value; bootstrap translates to initial state, constructs the canonical services, and wires the rule + intent mapper through them.
- **Engine intake order:** `handler.Validate` first, then `rule.OnIncomingCommand`. Validate is cheap and catches malformed commands before the rule has to think about routing.
- **`GameRules` is service-injected.** `IGameStateProvider`, `IAvailableActions`, `IIntakeQueue`, `IHandlerRegistry` all on the constructor; methods take only ephemeral data ([doc 29 §5](features/engine/models/03-snapshot-pipeline.md)).

---

## 11. Open Questions From This Sketch

Tracked alongside the sketch so they don't get lost. Numbers are stable; resolve in place.

### Resolved (in subsequent docs)

- ~~**Q1 — State pattern: stateless vs stateful rule.**~~ Resolved by [doc 29 §5](features/engine/models/03-snapshot-pipeline.md). Services injected at construction; methods take only ephemeral data; state read via `State.Current`.
- ~~**Q10 — `IntakeContext.Pending` type.**~~ `IntakeContext` collapsed; `Pending` is the injected `IIntakeQueue` ([doc 29 §5](features/engine/models/03-snapshot-pipeline.md)).
- ~~**Q13 — `FilterTriggers` default impl.**~~ `GameRules` is now an abstract class ([doc 25 §2](features/engine/models/02-gamerules-contract.md), [doc 29 §5](features/engine/models/03-snapshot-pipeline.md)); virtual default is straightforward.

### Deferred (need decisions, not blocked)

- **Q2 — Action pipeline cleanup.** The handler-lookup → validate → execute → trigger-fold flow is scattered across docs 18/19/22/25. Needs a dedicated doc that draws the full pipeline explicitly.
- **Q3 — `EndGameCommand` semantics.** When the rule dispatches `EndGameCommand` from `Evaluate`, what does the engine do? Stop accepting commands? Tear down? Bleeds into session lifecycle (checkpoint #12).
- **Q4 — Snapshot revision tag.** Useful for the UI to detect "I have a stale snapshot." Add when a concrete need surfaces (e.g., async render).

### Implementation details (non-blocking)

- **Q5 — `AvailableActions` mutator implementation.** Framework-shipped — the `IAvailableActions` service writes the slot through an internal mutator ([doc 29 §2](features/engine/models/03-snapshot-pipeline.md)). Confirm at engine implementation.
- **Q6 — `IIntentMapper` final shape.** Now `Map(ActionTemplate, PlayerChoice) → GameCommand` (typed templates, [doc 29 §3](features/engine/models/03-snapshot-pipeline.md)). `PlayerChoice` shape pin during implementation.
- **Q7 — `IGameStore` final shape.** Sketched as a presentation-side wrapper exposing `State`, `OnStateChanged`, `SubmitIntent`. Final shape depends on the chosen state library.
- **Q8 — `IHandlerContext` shape.** Used as `ctx.Mutate.MoveCardBetweenPiles(...)` — purely placeholder. Will likely also expose `ctx.Actions` (effect-side `IAvailableActions`) per [doc 29 §7](features/engine/models/03-snapshot-pipeline.md). Resolves with action pipeline cleanup (Q2).
- **Q9 — `IHandlerRegistry` shape.** Sketched as `handlers.Validate(command, state, out reason)` — needs a typed lookup mechanism (probably keyed by `Type`). Resolves with Q2.
- **Q11 — `IntakeResult.Reason`** — free-form string vs. structured enum. Defer until a real game forces it.
- **Q12 — Re-validation at dequeue.** Validate at intake plus a second time before handler runs (state may have changed). Currently OK as double-validate.
- **Q17 — `IGameStateProvider` shape.** `State.Current` returns the immutable state; subscriptions probably live on the Store. Pin at engine implementation ([doc 29 §9](features/engine/models/03-snapshot-pipeline.md)).
- **Q18 — Effect-side service symmetry.** Effects need `IAvailableActions` (and possibly `IGameStateProvider`) mid-execution. Likely through `IHandlerContext`. Needs a dedicated effect-flow doc ([doc 29 §7](features/engine/models/03-snapshot-pipeline.md)).

### Out of scope for Pile Counter (will surface in richer games)

- **Q14 — Async decision affordances.** When an effect awaits player input (target picker, choice overlay), the affordance shape may need to express "the player must respond *here*" vs. "the player has free input." Pile Counter has no async; deferred.
- **Q15 — Multiplayer / hidden info snapshots.** Different players see different affordances. Snapshot may need scoping per actor. Pile Counter is single-player; deferred.
- **Q16 — Snapshot diffing.** For performance, the engine may compute snapshot deltas rather than full recomputes. Deferred.

---

## 12. How This Doc Is Used

When designing or revisiting a framework piece, sketch its Pile Counter implementation alongside the framework type. If the sketch is awkward, the framework is wrong. If the sketch is trivial, the framework is at least adequate for the floor.

Whenever the framework shape changes, update §9 in this doc *first*, then propagate to docs 25 and 27. The sketch is the source of truth for what "actual game code" looks like; abstract framework docs follow from it.

If a question opens during sketching, add it to §11 with a stable Q-number. Resolve in place when answered. This keeps the open list local to where the sketch lives.
