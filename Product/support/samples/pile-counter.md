---
type: sample
status: locked
references: [features/engine/archive/01-game-engine.md, features/engine/archive/03-game-rules-runtime.md, features/engine/archive/05-game-factory.md, features/engine/archive/06-game-definition.md, features/engine/archive/07-match-state.md, features/engine/archive/08-turn-flow.md, features/engine/archive/09-state-based-actions.md, features/actions/archive/01-command-pipeline.md, features/actions/archive/03-command-handler.md, features/actions/archive/06-command-validator.md, features/effects/archive/02-effect-scope.md, features/cards/archive/01-card-model.md, features/zones/archive/01-zone-model.md, features/players/archive/01-player-model.md, features/entities/archive/02-instance.md, features/presentation/archive/01-match-state-view-model.md, features/presentation/archive/02-view-model-registry.md, decisions/0009-engine-as-fsm.md, decisions/0010-validator-handler-split.md, decisions/0011-effect-orchestration-dispatch-requests.md, decisions/0012-presentation-per-state-view-models.md]
tags: [#sample, #pile-counter]
---

# Pile Counter — Reference Sample Game

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

After a move resolves, if `pile.Counter == pile.Target` for **all four piles** simultaneously, the game ends and the player wins. Post-cascade orchestration (per [ADR-0009 amendment](Product/features/engine/archive/07-match-state.md)): `PilePlayingState.IsCompleted(action, result, scope)` returns true when the win condition holds; `runtime.Evaluate` consults `ITurnFlow.NextAfter` which returns the framework-shipped `EndOfMatchState(MatchOutcome.Win(player))`; the engine emits `TransitionStateCommand`; `TransitionStateHandler` writes `MatchStateSlice.Current = EndOfMatchState` and enqueues `oldState.OnExit` then `newState.OnEnter` (both empty for Pile Counter); after the transition handler completes, the engine pattern-matches `MatchStateSlice.Current is EndOfMatchState` and **auto-dispatches `EndMatchCommand(outcome)`** — no game-side observer required; the framework's `EndMatchHandler` flips `engine.Session = EndedSession(outcome)` and emits `MatchEndedSignal`. Every subsequent `Dispatch` hits the framework-default `SessionGate` validator and rejects.

There is no lose condition. The puzzle is always solvable from setup; the player keeps moving until they hit it.

---

## 7. What Pile Counter Tests In The Framework

This game forces the framework to express:

- **Zones with stack ordering and a derived score.** The score is a per-zone value cached in a `PileState` slice keyed at the pile's `Ref<Zone>`. Maintaining the cache atomically with each move tests the per-zone-type slice + mutator pattern from [`zones/01-zone-model.md`](features/zones/archive/01-zone-model.md).
- **A single action type with legality rules.** Tests the validator/handler split per [ADR-0010](decisions/0010-validator-handler-split.md) — `MoveCardHandler` extends `ValidatedCommandHandler<MoveCardCommand, MoveCardResult>` with four rejection paths, plus the framework defaults `SessionGate` + `MatchStateActionGate`. Tests the action-gate triad (`MatchState.Actions` + `ActionsOpen`) as the framework's prophylactic legality surface ([`engine/07-match-state.md`](features/engine/archive/07-match-state.md)). Tests per-state VM observable affordance projection ([`presentation/01-match-state-view-model.md`](features/presentation/archive/01-match-state-view-model.md)) and how legality interacts with the "can't move same card twice in a row" memory.
- **`IsCompleted`-driven win check + framework-orchestrated session close.** `PilePlayingState.IsCompleted(action, result, scope)` returns true when all pile counters match their targets; `ITurnFlow.NextAfter` returns the framework-shipped `EndOfMatchState(MatchOutcome.Win(player))`; engine auto-dispatches `EndMatchCommand(outcome)` after the transition lands. Tests the `IsCompleted` + `ITurnFlow` + `EndOfMatchState` auto-emit pattern end-to-end ([`engine/07-match-state.md`](features/engine/archive/07-match-state.md), [`engine/08-turn-flow.md`](features/engine/archive/08-turn-flow.md)). ISBA retired from the framework per ADR-0009 refresh; cross-cutting overrides not needed for Pile Counter.
- **Per-game enumeration without a per-game index.** The runtime's `IsCompleted` reads all piles via `scope.GetAll<PileState>()`; the playing VM iterates `scope.GetAll<ZoneContentsState>()` to compute affordances. No `PileRefsState` slice — tests Scaffold's `GetAll<TState>` enumeration as the canonical pattern.
- **Inline composite mutations via `IEffectScope.Execute`.** `MoveCardComposite : CompositeMutation` (per [ADR-0013](decisions/0013-engine-pipeline-orchestration.md); renamed from the pre-cascade `MoveCardMutation : CompositeAction`) bundles the zone-move payload triple (`RemoveFromZonePayload`, `AddToZonePayload`, last-moved write). The handler runs it inline via `Scope.Execute(...)` — no separate command envelope, no validator chain, no trigger fold. Tests the `CompositeMutation` separation from the `GameCommand` family.
- **Move history of size 1.** "Last card moved" is a sliver of state that doesn't fit on a card or a zone — it's *game-level* memory. It lives in a singleton `LastMoveState` slice declared by the `GameDefinition`'s `GetInitialSlices()`.
- **`GameDefinition` bundle composition.** The puzzle's deck, pile targets, and initial deal flow through a `PileCounterGameDefinition` bundle. Tests the bundle shape from [`engine/06-game-definition.md`](features/engine/archive/06-game-definition.md).
- **Per-state ViewModels + ViewModelRegistry.** `PilePlayingViewModel : MatchStateViewModel<PilePlayingState>` and `EndOfMatchViewModel : MatchStateViewModel<EndOfMatchState>`; presenter rebinds via `ViewModelRegistry` on type change. Per-VM `Map(DropCardIntent) → MoveCardCommand` replaces the retired global `IIntentMapper` per [ADR-0012](decisions/0012-presentation-per-state-view-models.md).
- **Match setup via `OnEnter`.** `PilePlayingState.OnEnter(scope)` returns the setup commands that spawn each card per `puzzle.Deal`. `engine.StartMatch()` transitions `NotStartedSession → InMatchSession`; `MatchStateSlice.Current` seeds from `ITurnFlow.Initial = new PilePlayingState()`; the engine drains `OnEnter` commands as part of the initial transition.

This game does **not** test:

- Triggers, abilities, replacement effects, ongoing effects.
- Async decisions (no targeting, no choice prompts).
- Multi-player or priority.
- Phase transitions.
- Card variant subclasses (cards are distinguished only by their `Number` variable; no per-card-type `CardDefinition` subclass).

We will revisit those features against more elaborate sample games once the floor is solid.

---

## 8. Resolved Setup Decisions

- **Deck size:** 12 cards.
- **Numbering:** each card's `Number` is in `[1..10]`; duplicates allowed (and inevitable since 12 > 10).
- **Distribution:** 3 cards per pile, even split.
- **"Twice in a row" rule:** by card *instance* identity (`Ref<Card>`). Two cards sharing the same `Number` are still distinct.
- **Empty piles:** allowed. An empty pile has counter `0` and still has a target. Winning is possible with an empty pile if its target is `0`.
- **Puzzle source:** an external **puzzle factory** produces a complete `(deck, deal, targets)` triple. Internally the factory uses random generation plus a solvability check, but that is opaque to the game — the runtime just receives the triple as setup input. No target generation, shuffling, or solvability logic lives inside the framework.
- **Undo:** out of scope.
- **Restart / new puzzle:** session-lifecycle concern, out of scope for the runtime.

All setup decisions are now resolved. The §9 sketch implements them against the canonical Models.

---

## 9. Rules Layer Implementation Sketch

This is the working sketch of Pile Counter against the framework as of the post-FSM-arch cascade (ADRs 0009–0012 landed 2026-05-10/11; design-pass locks 2026-05-12). When the framework changes, update this sketch first and propagate to the affected feature Models.

### 9.1 State shape

State lives as Scaffold `BaseState` slices, each owning one concern. There is no single-record `GameState`; reads go through `IStateScope.Get<TState>()`. See [`effects/02-effect-scope.md`](features/effects/archive/02-effect-scope.md) for the binding.

```csharp
// Per-pile slice — Counter is the cached sum of card Numbers in the pile;
// Target is the authored win condition. Keyed externally at pile.SelfRef
// (Ref<Zone>) by the factory walk per [zones/01](features/zones/archive/01-zone-model.md).
public sealed record PileState(int Counter, int Target) : BaseState;

// Singleton — the "last moved card" memory. Game-level; doesn't fit on a card
// or a zone. Declared by PileCounterGameDefinition.GetInitialSlices().
public sealed record LastMoveState(LastMove? Move) : BaseState;

public sealed record LastMove(Ref<Card> Card, Ref<Zone> From, Ref<Zone> To);

// The Card variable catalog. One variable in this game.
public static class Vars
{
    public static readonly Variable Number = new("number", "int");
}
```

Framework-owned slices that Pile Counter reads/writes through:

- **`ZoneContentsState`** — keyed at each pile's `Ref<Zone>`. Holds `ImmutableList<CardRef>`. Source of truth for "which cards are in which pile, in what order." Enumerated via `scope.GetAll<ZoneContentsState>()` — no game-side index slice.
- **`CardMapState`** — singleton. Maps `CardRef ↔ Ref<Card>` for resolution.
- **`EntityState`** (per card) — keyed at each card's `Ref<Card>`. Holds the `Number` variable. Read via `card.Storage.GetVariable<int>(Vars.Number)`.
- **`MatchStateSlice`** — singleton. Holds `MatchState Current` per [`engine/07-match-state.md`](features/engine/archive/07-match-state.md) — `PilePlayingState` during play, framework-shipped `EndOfMatchState(MatchOutcome)` at end. Framework auto-seeded from `ITurnFlow.Initial` at `Build()`.
- **`HistoryState`** — framework-owned; auto-seeded. Receives an envelope per command.
- **`PendingEffectsSlice`** / **`RequestsSlice`** / **`AnswersSlice`** — framework-owned; auto-seeded. Empty for Pile Counter (no effects, no async decisions). The engine's drain-policy guards (per [ADR-0011](decisions/0011-effect-orchestration-dispatch-requests.md)) treat them as no-ops.

Slices are immutable; mutators return new records. The runtime holds no game-affecting state.

> **Note (post-cascade).** The pre-cascade sketch carried a game-side `PileRefsState` singleton holding `IReadOnlyList<Ref<Zone>>` so the runtime / snapshot builder could iterate piles in order. Per the 2026-05-12 design pass (Q2 = A, reaffirmed in a second pass after PR #141), it retires — the canonical pattern is `scope.GetAll<TState>()`, locked into `IStateScope` in [`effects/02-effect-scope.md`](features/effects/02-effect-scope.md) and demonstrated by the canonical Pile Counter VM example in [`presentation/01 §Pile Counter — single playing-state VM`](features/presentation/archive/01-match-state-view-model.md). If ordering stability becomes a concern, add an explicit `Index` field to `PileState` — Scaffold's keying typically preserves insertion order from the `SharedZones` walk so this is rarely needed.

### 9.2 Commands, intents, results, payloads

```csharp
// Player intent — game-defined typed gesture record. Per [ADR-0012](decisions/0012-presentation-per-state-view-models.md),
// PlayerIntent lives in presentation/ feature-root vocabulary; the per-state
// MatchStateViewModel<TState>.Map consumes it. Carries (Card, From, To); the
// gesture producer (drag UI / debug button matrix) populates all three at
// gesture-start — no scope scan at Map time (per 2026-05-12 design pass Q3 = A).
public sealed record DropCardIntent(
    Ref<Card> Card,
    Ref<Zone> From,
    Ref<Zone> To) : PlayerIntent;

// Engine command (fully resolved). Refs at the boundary — per ADR-0005,
// command payloads are addressable by ref because they cross
// serialization/replay/network boundaries.
public sealed record MoveCardCommand(
    Ref<Card> Card,
    Ref<Zone> From,
    Ref<Zone> To) : GameAction;

// Result
public sealed record MoveCardResult(Ref<Card> MovedCard) : CommandResult;

// Game-side payloads — fan out to game-authored mutators when the handler
// commits the move.
public sealed record AdjustPileCounterPayload(Ref<Zone> Pile, int Delta)
    : IPayloadReference
{
    public Reference GetReference() => Pile;
}

public sealed record SetLastMovePayload(LastMove Move);
```

The framework primitive is **generic** `MoveCardCommand` — not `MoveTopCardCommand`. The "top only" rule is a Pile Counter constraint enforced in the validator, not a framework concept. `EndMatchCommand` is framework-owned ([`engine/01`](features/engine/archive/01-game-engine.md)); the engine **auto-dispatches** it when `MatchStateSlice.Current` transitions to `EndOfMatchState` (per ADR-0009 amendment item 4) — no game-side wiring. See §9.3 and §9.5.

**Command family (4 siblings per [ADR-0013](decisions/0013-engine-pipeline-orchestration.md)).** `GameCommand` is the abstract base; concrete siblings are `GameAction`, `GameSignal`, `Request<TResponse>`, `Response`. `CompositeAction` retires from the family — composite mutations are now a separate concept (see below). Pile Counter only authors `GameAction`s + the framework's signals.

**Composite mutation (out-of-family per ADR-0013).** The zone-move (remove from source, rotate `CardRef`, insert into destination) goes through `MoveCardComposite : CompositeMutation` — a state-mutation cluster that runs **inline** via `Scope.Execute(...)`. Not a `GameCommand`: no queue, no validator chain, no trigger fold, no envelope. See [`cards/01-card-model.md` § Mutations](features/cards/archive/01-card-model.md) and ADR-0013 §3.

### 9.3 Match-state shape

Pile Counter has no phase structure — one game-authored state (`PilePlayingState`); the win condition transitions to the framework-shipped `EndOfMatchState(MatchOutcome)`. Both extend `MatchState` per [`engine/07-match-state.md`](features/engine/archive/07-match-state.md) (ADR-0009 amendment shape: action-gate triad, `OnEnter` / `OnExit` methods, `IsCompleted` predicate).

```csharp
public sealed record PilePlayingState(IActor Player, Puzzle Puzzle) : MatchState
{
    public override IReadOnlyList<Type> Actions     => [typeof(MoveCardCommand)];
    public override bool                ActionsOpen => false;   // whitelist mode

    // OnEnter — engine drains these after the slice is written. For Pile Counter,
    // setup spawns each card per puzzle.Deal into its target pile.
    public override IReadOnlyList<GameAction> OnEnter(IStateScope scope)
        => Puzzle.Deal
            .SelectMany((pile, pileIdx) => pile
                .Select(cardIdx => new CreateCardAction(
                    Definition: Puzzle.Deck[cardIdx],
                    Zone:       scope.GetAll<PileState>().ElementAt(pileIdx).Reference)))
            .ToArray();

    // OnExit — nothing to clean up in this game.
    // (Default base returns [].)

    // IsCompleted — win check. Replaces the pre-cascade PileCounterWinSBA per
    // the ADR-0009 refresh (ISBA retires from framework).
    public override bool IsCompleted(
        GameAction    action,
        CommandResult result,
        IStateScope   scope)
        => scope.GetAll<PileState>().All(p => p.Counter == p.Target);
}
```

`EndOfMatchState(MatchOutcome Outcome)` is **framework-shipped** (per ADR-0009 amendment item 4). Pile Counter does *not* author its own terminal state — it returns the framework one from `ITurnFlow.NextAfter` (see §9.4). When the engine pattern-matches `MatchStateSlice.Current is EndOfMatchState` after the transition handler completes, it auto-dispatches `EndMatchCommand(outcome)` — no game-side observer required.

`PilePlayingState.Actions` lists the single command type valid during play; `ActionsOpen = false` switches the framework's `MatchStateActionGate` validator into whitelist mode (per ADR-0010). Anything not in `Actions` rejects automatically.

### 9.4 Turn flow

Pile Counter's turn structure is trivial — one game-authored state (`PilePlayingState`) that transitions directly to the framework-shipped `EndOfMatchState` when the win condition holds. `ITurnFlow.NextAfter` is consulted by the default `runtime.Evaluate` body once `PilePlayingState.IsCompleted` returns true. No `ISBA` — per the ADR-0009 refresh, ISBA retires from the framework; cross-cutting overrides aren't needed for this game.

```csharp
public sealed class PileCounterTurnFlow : ITurnFlow
{
    private readonly IActor _player;
    private readonly Puzzle _puzzle;

    public PileCounterTurnFlow(IReadOnlyList<IActor> actors, Puzzle puzzle)
    {
        _player = actors.Single();
        _puzzle = puzzle;
    }

    public MatchState Initial => new PilePlayingState(_player, _puzzle);

    public MatchState NextAfter(MatchState current) => current switch
    {
        PilePlayingState => new EndOfMatchState(new MatchOutcome.Win(_player)),
        _                => current,
    };
}
```

See [`engine/08-turn-flow.md`](features/engine/archive/08-turn-flow.md) for the flow contract. The flow is consulted only when `IsCompleted` returns true; same-typed returns are no-ops (transition-loop prevention).

### 9.5 Win-condition orchestration — IsCompleted → Flow → EndOfMatchState → auto-emit

Pile Counter's match-end flow threads the post-cascade framework mechanisms end-to-end:

```
PilePlayingState.IsCompleted returns true (all piles match)
              │
              ▼  runtime.Evaluate (default body)
        Flow.NextAfter(PilePlayingState) → EndOfMatchState(Win(player))
              │
              ▼  engine drain detects type change
        TransitionStateCommand(EndOfMatchState)
              │
              ▼  TransitionStateHandler
   1. enqueue oldState.OnExit(scope)        ← PilePlayingState's OnExit (empty)
   2. Store.Execute(SetMatchStatePayload(EndOfMatchState))
   3. enqueue newState.OnEnter(scope)       ← EndOfMatchState's OnEnter (empty)
              │
              ▼  engine pattern-match auto-emit
   MatchStateSlice.Current is EndOfMatchState
   → engine auto-dispatches EndMatchCommand(outcome)
              │
              ▼  EndMatchHandler
   engine.Session = EndedSession(outcome)
   emit MatchEndedSignal
              │
              ▼  SessionGate now rejects further dispatches
```

Three invariants worth calling out:

- **Engine does NOT call `runtime.Evaluate` after `TransitionStateCommand`** (transition-loop prevention invariant per ADR-0009 amendment item 3). Without this, `IsCompleted` would fire again from inside the transition and would re-enqueue the same transition.
- **`EndMatchCommand` is auto-dispatched** (ADR-0009 amendment item 4): the engine pattern-matches `MatchStateSlice.Current is EndOfMatchState` after the transition handler completes and synthesizes the command. The game doesn't author any wiring; `EndOfMatchState` is just the signal that the match is over.
- **`EndedSession` is terminal** — rematch is dispose-and-recreate via `GameFactory.Build`, not a state-machine reset (per ADR-0009 amendment).

### 9.6 Move validator + handler

Per [ADR-0010](decisions/0010-validator-handler-split.md), command-specific legality lives in `ICommandValidator<TCommand>` separate from the handler. The common case — one validator + one handler for the same command — uses the combined `ValidatedCommandHandler<TCommand, TResult>` base. `Store` + `Scope` come from the base via `[Inject]`; no `IHandlerContext`. See [`actions/03-command-handler.md`](features/actions/archive/03-command-handler.md) and [`actions/06-command-validator.md`](features/actions/archive/06-command-validator.md).

```csharp
public sealed class MoveCardHandler
    : ValidatedCommandHandler<MoveCardCommand, MoveCardResult>
{
    public override bool Validate(MoveCardCommand move, out string? reason)
    {
        if (move.From.Equals(move.To))
        { reason = "source and destination must differ"; return false; }

        var sourceContents = Store.Get<ZoneContentsState>(move.From).Cards;
        if (sourceContents.Count == 0)
        { reason = "source pile is empty"; return false; }

        // Top card check — last in the list is the top.
        var topCardRef = Store.Get<CardMapState>().RefOf(move.Card);
        if (!sourceContents[^1].Equals(topCardRef))
        { reason = "only the top card of a pile can be moved"; return false; }

        if (Store.Get<LastMoveState>().Move?.Card.Equals(move.Card) == true)
        { reason = "can't move the same card twice in a row"; return false; }

        reason = null; return true;
    }

    public override Task<MoveCardResult> Handle(MoveCardCommand move)
    {
        var card = Store.Catalog.Resolve(move.Card);
        var number = card.Storage.GetVariable<int>(Vars.Number);

        // Composite mutation cluster — runs inline via Scope.Execute. The
        // composite's Apply expands to RemoveFromZonePayload + AddToZonePayload
        // (+ CardRef rotation). Not a command: no envelope, no fold.
        Scope.Execute(MoveCardComposite.AtTop(move.Card, move.From, move.To));

        // Game-side payloads — payload-keyed fan-out to mutators in the same
        // command's mutation batch. Two pile-counter writes (source -, dest +)
        // plus the last-move singleton.
        Store.ExecuteBatch(new IPayloadReference[]
        {
            new AdjustPileCounterPayload(move.From, -number),
            new AdjustPileCounterPayload(move.To,   +number),
        });
        Store.Execute(new SetLastMovePayload(new LastMove(move.Card, move.From, move.To)));

        return Task.FromResult(new MoveCardResult(move.Card));
    }
}
```

Two framework-default validators run before `MoveCardHandler.Validate`:

- **`SessionGate`** — rejects when `engine.Session` is not `InMatchSession` (lifecycle commands bypass).
- **`MatchStateActionGate`** — rejects when `ActionsOpen == false && command.GetType() ∉ MatchStateSlice.Current.Actions` (per ADR-0009 amendment item 1's action-gate triad); lifecycle commands + `TransitionStateCommand` bypass.

Both register first in DI order; Pile Counter's `MoveCardHandler.Validate` (as the validator side of the combined base) runs only after both pass. Short-circuit on first reject; rejection reason surfaces via `engine.Dispatch`'s `out string? reason`.

The handler issues `Scope.Execute(MoveCardComposite)` inline (composite mutation; no envelope, no fold) plus `Store.ExecuteBatch` for the pile-counter payloads and `Store.Execute` for the last-move singleton. Scaffold's payload-keyed registry runs every mutator registered for each payload and commits per the engine's mutation-batch policy.

### 9.7 State mutators

Each mutator owns one slice; multiple mutators can register against the same payload type. Mutator type parameters are the **payload**, never the command.

```csharp
// Per-pile counter — keyed by Ref<Zone> via IPayloadReference.
public class PileCounterMutator : Mutator<PileState, AdjustPileCounterPayload>
{
    public override PileState Change(PileState state, AdjustPileCounterPayload payload, IStateScope scope)
        => state with { Counter = state.Counter + payload.Delta };
}

// Singleton — overwrites the last-move record.
public class LastMoveMutator : Mutator<LastMoveState, SetLastMovePayload>
{
    public override LastMoveState Change(LastMoveState state, SetLastMovePayload payload, IStateScope scope)
        => new LastMoveState(payload.Move);
}
```

`PileCounterMutator` runs against the `PileState` slice keyed at `payload.Pile`. The framework's payload-keyed dispatch routes `AdjustPileCounterPayload` to the slice instance whose key matches `payload.GetReference()`. Two `AdjustPileCounterPayload`s in one batch (one for source, one for destination) update both piles atomically.

The framework's `MoveCardComposite` (post-ADR-0013 rename) handles `ZoneContentsState` and `CardMapState` updates via its inline `Apply(scope)` expansion — see [`cards/01-card-model.md` § Composite expansion](features/cards/archive/01-card-model.md). No game-authored mutator is needed for the zone move itself.

### 9.8 Rules definition + runtime

Per [ADR-0009](decisions/0009-engine-as-fsm.md) (amended), the runtime is structurally an FSM evaluator. The default `Evaluate` body composes request pause → pending-effect pause → `IsCompleted` + `Flow.NextAfter` — Pile Counter overrides nothing. `ISBA` retired from the framework per the ADR-0009 refresh; `runtime.FilterTriggers` retired per [ADR-0013](decisions/0013-engine-pipeline-orchestration.md). See [`engine/03-game-rules-runtime.md`](features/engine/archive/03-game-rules-runtime.md).

```csharp
// Authored data — Pile Counter has no per-mode knobs yet, so the definition
// is empty. A future "Hard Mode" variant would add [SerializeField] fields here
// (move limit, scoring multiplier, …) and ship as a separate definition asset.
[Serializable]
public class PileCounterRulesDefinition : GameRulesDefinition
{
    public static readonly PileCounterRulesDefinition Default = new();
}

// Runtime brain — generic on its definition. No game-specific subsystems
// (Pile Counter has no stack, no decks, no priority). No StateBasedActions
// (ISBA retired); no FilterTriggers (retired per ADR-0013).
public sealed class PileCounterRulesRuntime : GameRulesRuntime<PileCounterRulesDefinition>
{
    protected override ITurnFlow Flow { get; }

    public PileCounterRulesRuntime(
        PileCounterRulesDefinition definition,
        IStateScope                state,
        PileCounterTurnFlow        flow)
        : base(definition, state)
    {
        Flow = flow;
    }

    // Evaluate omitted — default body covers it:
    //   request pause → pending-effect pause → current.IsCompleted + Flow.NextAfter.
    // For Pile Counter, IsCompleted does the win check; Flow.NextAfter returns
    // EndOfMatchState(Win); engine auto-emits EndMatchCommand on the transition.
}
```

The runtime constructor takes its typed definition, Scaffold's `IStateScope`, and the `ITurnFlow` implementation — all DI-resolved. Per [ADR-0009](decisions/0009-engine-as-fsm.md), `IAvailableActions` is not injected; legal-command surfacing lives on `MatchState.Actions` + `ActionsOpen`. Per [ADR-0006](decisions/0006-two-pipeline-architecture.md), `IIntakeQueue` is gone; the command queue is engine-private FIFO.

### 9.9 Per-state ViewModels

Per [ADR-0012](decisions/0012-presentation-per-state-view-models.md), each `MatchState` subtype has a typed `MatchStateViewModel<TState>`. The presenter subscribes to `MatchStateSlice`; on type change it rebinds via `ViewModelRegistry`. The per-state `Map(intent) → GameCommand` replaces the retired global `IIntentMapper`. See [`presentation/01-match-state-view-model.md`](features/presentation/archive/01-match-state-view-model.md) and [`presentation/02-view-model-registry.md`](features/presentation/archive/02-view-model-registry.md).

```csharp
public sealed class PilePlayingViewModel : MatchStateViewModel<PilePlayingState>
{
    private readonly ObservableProperty<IReadOnlyDictionary<Ref<Card>, IReadOnlyList<Ref<Zone>>>> moveTargets = new();

    public IObservable<IReadOnlyDictionary<Ref<Card>, IReadOnlyList<Ref<Zone>>>> MoveTargets => moveTargets;

    public override void Bind(PilePlayingState state, IStateScope scope)
    {
        // Compute per-card valid destinations from current zone contents + last-move rule.
        var piles    = scope.GetAll<ZoneContentsState>().ToList();
        var lastMove = scope.Get<LastMoveState>().Move;

        moveTargets.Value = piles
            .Where(p => p.Cards.Count > 0)
            .Select(p => new
            {
                Top = p.Cards[^1],
                Destinations = piles
                    .Where(other => other.Reference != p.Reference)
                    .Select(o => (Ref<Zone>)o.Reference)
                    .ToList()
            })
            .Where(x => !x.Top.Equals(lastMove?.Card))
            .ToDictionary(
                x => x.Top,
                x => (IReadOnlyList<Ref<Zone>>)x.Destinations);
    }

    public override GameCommand Map(PlayerIntent intent) => intent switch
    {
        DropCardIntent d => new MoveCardCommand(d.Card, d.From, d.To),
        _ => throw new InvalidOperationException($"Unhandled intent: {intent.GetType().Name}"),
    };
}

public sealed class EndOfMatchViewModel : MatchStateViewModel<EndOfMatchState>
{
    private readonly ObservableProperty<MatchOutcome> outcome = new();
    public IObservable<MatchOutcome> Outcome => outcome;

    public override void Bind(EndOfMatchState state, IStateScope scope)
        => outcome.Value = state.Outcome;

    public override GameCommand Map(PlayerIntent intent)
        => throw new InvalidOperationException("No player input in EndOfMatchState.");
}
```

The `EndOfMatchViewModel` binds against the framework-shipped `EndOfMatchState` — Pile Counter doesn't author its own terminal state. The presenter's `ViewModelRegistry` maps `EndOfMatchState → EndOfMatchViewModel` (see §9.11). After the match ends, `engine.Session` is already `EndedSession(outcome)` and `SessionGate` rejects further dispatches; the VM just renders the outcome.

`PilePlayingViewModel.MoveTargets` is the affordance projection — the view binds drag handles / drop highlights to it directly. No intermediate `AffordanceMap`; no scope scan at `Map` time (the intent carries `Card + From + To`).

### 9.10 Engine intake order

Per [ADR-0010](decisions/0010-validator-handler-split.md), `engine.Dispatch` delegates entirely to the registered validator chain — no inline lifecycle gate. Framework-default validators (`SessionGate`, `MatchStateActionGate`) register first; game-shipped validators (like `MoveCardHandler.Validate` via the combined `ValidatedCommandHandler` base) append:

```text
Dispatch(command)
  resolve all ICommandValidator<TCommand> for typeof(command)
  for each validator in registration order:                          ← framework defaults first
    if !validator.Validate(command, out reason) → reject             ← short-circuit
  enqueue onto framework-private FIFO
  if !running → Drain()
```

The validator chain is uniform across the entire 4-sibling `GameCommand` family (`GameAction`, `GameSignal`, `Request<TResponse>`, `Response`) per [ADR-0011](decisions/0011-effect-orchestration-dispatch-requests.md) as amended by [ADR-0013](decisions/0013-engine-pipeline-orchestration.md) (`CompositeAction` retires from the family; composites are now `CompositeMutation` and run inline via `Scope.Execute`, bypassing the validator chain). For Pile Counter, only `GameAction` matters in practice — no signals beyond the framework's, no requests, no responses.

### 9.11 Bootstrap

```csharp
// Authored card definition — variant-free; one CardDefinition shape covers
// the whole deck. Per-card variation (the Number) lives in the variable bag.
public class PileCounterCardDefinition : CardDefinition
{
    public int Number { get; init; }

    public override IEnumerable<Variable> DefinedVariables => new[] { Vars.Number };

    public override bool TryGetDefaultValue(Variable key, out VariableValue value)
    {
        if (key == Vars.Number) { value = new IntVariableValue(Number); return true; }
        value = default!; return false;
    }
}

// Authored zone definition — one PileDefinition per pile, parameterized by
// its target. AllPublic visibility (battlefield-style); top-insertion is
// the default for stack-shaped piles.
public class PileDefinition : ZoneDefinition
{
    public int Target { get; init; }

    public PileDefinition()
    {
        DefaultPosition = new InsertPosition.AtTop();
        Visibility      = new ZoneVisibilityPolicy.AllPublic();
    }

    public override IEnumerable<BaseState> GetInitialSlices()
    {
        yield return new PileState(Counter: 0, Target: Target);
    }
}

// The puzzle data the factory produces.
public sealed record Puzzle(
    IReadOnlyList<PileCounterCardDefinition> Deck,
    IReadOnlyList<int>                       Targets,    // 4 entries
    IReadOnlyList<IReadOnlyList<int>>        Deal,       // 4 lists of 3 deck-indexes
    int                                      Seed);

public interface IPuzzleFactory { Puzzle Create(); }

// The GameDefinition bundle — consolidates Rules + CardCatalog + SharedZones
// + DefaultPlayerDefinition + game-singleton slices. Per [engine/06](features/engine/archive/06-game-definition.md).
public class PileCounterGameDefinition : GameDefinition
{
    private readonly Puzzle _puzzle;

    public PileCounterGameDefinition(Puzzle puzzle) => _puzzle = puzzle;

    public override GameRulesDefinition           Rules                   => PileCounterRulesDefinition.Default;
    public override IReadOnlyList<CardDefinition> CardCatalog             => _puzzle.Deck;
    public override IEnumerable<ZoneDefinition>   SharedZones             =>
        _puzzle.Targets.Select(target => new PileDefinition { Target = target });
    public override PlayerDefinition              DefaultPlayerDefinition => SoloPlayerDefinition.Default;

    public override IEnumerable<BaseState> GetInitialSlices()
    {
        yield return new LastMoveState(Move: null);
        // PileState slices come from each PileDefinition.GetInitialSlices() at spawn.
        // Framework slices (MatchStateSlice, HistoryState, PendingEffectsSlice,
        //   RequestsSlice, AnswersSlice) auto-seeded.
    }
}

// Solo-player baseline — empty PerPlayerZones (Pile Counter has no per-player
// state beyond the actor identity).
public class SoloPlayerDefinition : PlayerDefinition
{
    public static readonly SoloPlayerDefinition Default = new();

    public override IEnumerable<ZoneDefinition> PerPlayerZones => Array.Empty<ZoneDefinition>();
}

public class SoloPlayer : Player
{
    public SoloPlayer(SoloPlayerDefinition def, IEntityVariableStorage storage) : base(def, storage) { }
}

public static class PileCounterBootstrap
{
    // IGameFactory is DI-resolved; caller never sees VContainer.
    // Per-game behaviors (handler, validators, mutators, runtime, turn flow,
    // per-state ViewModels) are registered in PileCounterInstaller below.
    public static GameEngine Create(IPuzzleFactory puzzleFactory, IGameFactory gameFactory)
    {
        var puzzle = puzzleFactory.Create();

        var engine = gameFactory.NewMatch()
            .WithGame(new PileCounterGameDefinition(puzzle))    // bundle: Rules + zones + cards + GetInitialSlices()
            .WithActors(new SoloActor(ActorId.Player1))         // entity-backed Player spawned at Build via DefaultPlayerDefinition
            .WithSeed(puzzle.Seed)
            .Build();

        engine.StartMatch(out _);
        // NotStartedSession → InMatchSession; emits MatchStartedSignal.
        // MatchStateSlice.Current seeded from ITurnFlow.Initial = new PilePlayingState(player, puzzle);
        // PilePlayingState.OnEnter(scope) fires: returns CreateCardAction list spawning each card per puzzle.Deal.

        return engine;
    }
}

// Separate file — registered once at app startup.
public class PileCounterInstaller : IInstaller
{
    public void Install(IContainerBuilder builder)
    {
        // Combined validator + handler (one Register covers both — ValidatedCommandHandler base).
        builder.Register<MoveCardHandler>          (Lifetime.Singleton).AsImplementedInterfaces();

        // Game-authored mutators.
        builder.Register<PileCounterMutator>       (Lifetime.Singleton).AsImplementedInterfaces();
        builder.Register<LastMoveMutator>          (Lifetime.Singleton).AsImplementedInterfaces();

        // Runtime + turn flow.
        builder.Register<GameRulesRuntime<PileCounterRulesDefinition>, PileCounterRulesRuntime>(Lifetime.Transient);
        builder.Register<PileCounterTurnFlow>      (Lifetime.Singleton);
        // No PileCounterWinSBA — ISBA retired from framework per ADR-0009 refresh.
        // Win check lives in PilePlayingState.IsCompleted; transition driven by ITurnFlow.

        // Per-state ViewModels + registry. EndOfMatchState is framework-shipped;
        // the game just registers its VM.
        builder.Register<PilePlayingViewModel>     (Lifetime.Transient);
        builder.Register<EndOfMatchViewModel>      (Lifetime.Transient);
        builder.Register<ViewModelRegistry>        (Lifetime.Singleton).WithFactory(sp =>
            new ViewModelRegistryBuilder(sp.Resolve<IStateScope>(), sp)
                .Register<PilePlayingState, PilePlayingViewModel>()
                .Register<EndOfMatchState,  EndOfMatchViewModel>()
                .Build());
    }
}
```

Match construction is a two-layer split (locked in [`engine/05-game-factory.md`](features/engine/archive/05-game-factory.md)):

- **App-level installer** — the framework's `EngineInstaller` registers `IStateScope`, `ICommandPipeline`, `Store`, `IGameFactory`, the framework-default validators (`SessionGate`, `MatchStateActionGate`), framework-internal handlers (`StartMatchHandler`, `EndMatchHandler`, `TransitionStateHandler`, `CreateCardHandler`), and framework mutators (entity-bridge, `CardMapState`, `OwnershipState`, `CardVisibilityState`, `ZoneContentsState`, `SetMatchStateMutator`). The game's installer (`PileCounterInstaller` above) registers the validated handler, game-authored mutators, the runtime against its definition's closed generic (`GameRulesRuntime<PileCounterRulesDefinition> → PileCounterRulesRuntime`), the turn flow, and the per-state ViewModels + registry. Each runs once per app. See [`engine/05` § Registration contract](features/engine/archive/05-game-factory.md).
- **Per-match factory** — the bootstrap call site asks `IGameFactory` for a new game, supplying only per-match data: the `GameDefinition` bundle, actor catalog, RNG seed. VContainer never appears in this code.

The framework's `MoveCardComposite : CompositeMutation` (post-ADR-0013 rename) handles the zone-move expansion (remove from source + rotate `CardRef` + insert to dest) inline via `Scope.Execute(...)`; the game-side payloads (`AdjustPileCounterPayload` × 2, `SetLastMovePayload`) drive the cached-counter and last-move-memory mutators. After `Build` returns the engine is in `NotStartedSession`; `engine.StartMatch()` transitions to `InMatchSession` and seeds `MatchStateSlice.Current = new PilePlayingState(player, puzzle)`, whose `OnEnter(scope)` enqueues the per-card `CreateCardAction` list from `puzzle.Deal`.

---

## 10. Framework Decisions Surfaced From This Sketch

These are decisions the Pile Counter sketch forced or confirmed. Each affects the framework, not just this game.

- **Generic primitive + rule-enforced constraint.** Framework ships generic `MoveCardCommand`; "top only" is enforced in the validator side of `MoveCardHandler`. No `MoveTopCardCommand` primitive.
- **Validator / handler split.** Per [ADR-0010](decisions/0010-validator-handler-split.md), `ICommandValidator<TCommand>` is independent of `ICommandHandler<TCommand, TResult>`. Common case (one validator + one handler for the same command) uses the combined `ValidatedCommandHandler<TCommand, TResult>` base. `Store` is `[Inject]`-property on the base; no `IHandlerContext` parameter.
- **`Validate` signature is Try-pattern.** `bool Validate(TCommand, out string? reason)`. Uniform across all `GameCommand` siblings.
- **Five validation layers** ([ADR-0007](decisions/0007-five-validation-layers.md) refined by ADRs 0010 / 0012): (1) per-state VM affordance projection (UI prophylactic), (2) `Map(intent)` well-formedness (game), (3) framework-default validators `SessionGate` + `MatchStateActionGate`, (4) registered command validators (game-shipped legality — authoritative), (5) before-fold `Cancel` (effect-driven, mid-execution).
- **Rule holds zero game-affecting state.** `LastMove` lives in its own `LastMoveState` slice, written by a registered `Mutator<LastMoveState, SetLastMovePayload>`. Locked as contract.
- **State mutators are payload-keyed, not command-keyed** (Scaffold State pattern). Handlers issue `Store.Execute(payload)` or `Store.ExecuteBatch(...)`; every `Mutator<TState, TPayload>` registered for that payload type runs and commits atomically. Handlers don't enumerate ancillary state slots.
- **`CompositeMutation` is out-of-`GameCommand`-family.** Per [ADR-0013](decisions/0013-engine-pipeline-orchestration.md). `MoveCardComposite : CompositeMutation` (post-cascade rename of `MoveCardMutation : CompositeAction`) bundles the zone-move payload triple; runs **inline** via `Scope.Execute(...)`. Not a command: no queue, no validator chain, no trigger fold, no envelope. Pre-cascade `CompositeAction` retires as a `GameCommand` sibling.
- **Player input layer is per-state, not global.** Per [ADR-0012](decisions/0012-presentation-per-state-view-models.md): `PlayerIntent` (presentation feature-root vocabulary; game-defined typed records) → `MatchStateViewModel<TState>.Map(intent)` → `GameCommand` (engine). The global `IIntentMapper` retires; per-state VMs own the mapping. `DropCardIntent` carries `(Card, From, To)` — the gesture producer populates all three at gesture-start (2026-05-12 design pass Q3 = A).
- **Match-state legality lives on the action-gate triad (`Actions` + `ActionsOpen`).** Per [ADR-0009](decisions/0009-engine-as-fsm.md) amendment item 1. `ActionsOpen` defaults to `true` (anything passes); states that want whitelist mode set `ActionsOpen = false` + populate `Actions: IReadOnlyList<Type>`. `MatchStateActionGate` validator: open passes; closed + type in list passes; closed + type not in list rejects. Pile Counter's `PilePlayingState` uses closed + `[typeof(MoveCardCommand)]`; `EndOfMatchState` ships closed + empty (rejects everything).
- **Engine IS the FSM, with transition-loop prevention.** Per ADR-0009: `runtime.Evaluate(action, result) → MatchState`. Engine compares the returned type to `MatchStateSlice.Current.GetType()`; on type change, dispatches `TransitionStateCommand` through the normal pipeline. **Engine does NOT call `runtime.Evaluate` after `TransitionStateCommand`** (transition-loop prevention invariant per ADR-0009 amendment item 3). `TransitionStateHandler` writes the slice, enqueues `oldState.OnExit` then `newState.OnEnter` results.
- **Match-end orchestration via `EndOfMatchState` + engine auto-emit.** Framework ships `EndOfMatchState(MatchOutcome Outcome) : MatchState` (`ActionsOpen = false`, terminal). Game's `ITurnFlow.NextAfter` returns it when `MatchState.IsCompleted` signals "we're done." After the transition handler completes, **the engine pattern-matches `MatchStateSlice.Current is EndOfMatchState` and auto-dispatches `EndMatchCommand(outcome)`** — no game-side observer or `OnEnter` carrying `EndMatchCommand`. Per ADR-0009 amendment item 4. (This refines the 2026-05-12 design-pass Q1 = E: the spirit of "terminal state drives session close" survives, but the wiring is now data-driven via the framework-shipped state, not via game-authored `EnterCommands`.)
- **Win check via `MatchState.IsCompleted`.** Pile Counter's `PilePlayingState.IsCompleted(action, result, scope)` reads `scope.GetAll<PileState>()` and returns `piles.All(p => p.Counter == p.Target)`. When true, `ITurnFlow.NextAfter(PilePlayingState)` returns `EndOfMatchState(Win(player))`. **`ISBA` retired from framework** per ADR-0009 refresh; cross-cutting state-driven overrides aren't load-bearing for the floor sample.
- **Lifecycle hooks are methods, not properties** (ADR-0009 amendment item 2). `EnterCommands` (virtual property) → `OnEnter(scope) → IReadOnlyList<GameAction>` (virtual method) with mirror `OnExit(scope) → IReadOnlyList<GameAction>`. `ShouldExit` renames to `IsCompleted` (predicate, not directive). Method shape lets states compute lists from current scope.
- **Successor knowledge lives in `ITurnFlow.NextAfter`.** Pile Counter's `PileCounterTurnFlow.Initial = new PilePlayingState(player, puzzle)`; `NextAfter` pattern-matches `PilePlayingState → EndOfMatchState(Win(player))`. Flow is consulted only when `IsCompleted` returns true.
- **Session lifecycle is an engine field, not a Store slice.** Per ADR-0009 D12: `engine.Session : SessionState` (sealed hierarchy — `NotStartedSession` / `InMatchSession` / `EndedSession`). The framework-default `SessionGate` validator reads `engine.Session` directly. `EndedSession` is terminal — rematch is dispose-and-recreate via `GameFactory.Build`, not a state-machine reset.
- **Per-state polymorphic ViewModels + `ViewModelRegistry`.** Per ADR-0012. Each `MatchState` subtype has a typed `MatchStateViewModel<TState>` (game-authored, or registered against framework-shipped states like `EndOfMatchState`); presenter subscribes to `MatchStateSlice` and rebinds via `ViewModelRegistry` on type change. Per-VM `Map(intent) → GameCommand` replaces the retired global `IIntentMapper`.
- **Per-game enumeration via `scope.GetAll<TState>()`.** No per-game zone-refs index slice — Pile Counter's `IsCompleted` reads `scope.GetAll<PileState>()`; the playing VM reads `scope.GetAll<ZoneContentsState>()`. Locked in 2026-05-12 design pass Q2 = A (reaffirmed second-pass after PR #141 introduced `PileRefsState` and was reverted; `IStateScope.GetAll<TState>` is the canonical API on [`effects/02:78`](features/effects/02-effect-scope.md)).
- **Engine-driven pipeline orchestration.** Per [ADR-0013](decisions/0013-engine-pipeline-orchestration.md). The engine holds `ITriggerPipeline` (no longer the action pipeline); engine's drain pattern-matches command type and routes per the 4-sibling table (`GameAction` / `GameSignal` / `Request<TResponse>` / `Response`). `scope.Dispatch(GameCommand)` is the single command-routing surface; `scope.Emit` retires. `runtime.FilterTriggers` retires; eligibility lives on `Trigger<T>.Validate`.
- **Two-pipeline architecture** ([ADR-0006](decisions/0006-two-pipeline-architecture.md)). Command pipeline is framework-FIFO with the validator chain at intake; effect-level orchestration is the separate surface defined by [ADR-0011](decisions/0011-effect-orchestration-dispatch-requests.md) (`PendingEffectsSlice` + async `IEffect.Run` coroutines + request system). Pile Counter exercises only the command pipeline.
- **`GameDefinition` bundle composition.** `PileCounterGameDefinition` consolidates Rules + CardCatalog + SharedZones + DefaultPlayerDefinition + game-singleton slices. Per-puzzle variation flows through the constructor. [`engine/06`](features/engine/archive/06-game-definition.md).
- **Match setup via `OnEnter`.** `PilePlayingState.OnEnter(scope)` returns the setup commands (`CreateCardAction` per `puzzle.Deal`). `engine.StartMatch()` transitions to `InMatchSession`; `MatchStateSlice.Current` seeds from `ITurnFlow.Initial`; the initial state's `OnEnter` results drain through the normal pipeline.
- **Engine intake order.** Per ADR-0010: `engine.Dispatch(command)` resolves all `ICommandValidator<TCommand>` for the command's runtime type, runs them in DI registration order, short-circuits on first reject. Framework defaults (`SessionGate`, `MatchStateActionGate`) register first; games append. No inline lifecycle gate.
- **Rules layer is split into `GameRulesDefinition` (data) + `GameRulesRuntime<TDefinition>` (runtime).** Definition is `[Serializable]` data; runtime constructor takes the definition + `IStateScope` + `ITurnFlow` (no `StateBasedActions` — ISBA retired; no `FilterTriggers` — retired per ADR-0013) + game-specific subsystems. One-method contract (`Evaluate`); default `Evaluate` body composes request pause → pending-effect pause → `IsCompleted` + `Flow.NextAfter`. Per-match factory uses container open-generic resolution.
- **Definition / Instance layered on framework parents.** `CardDefinition` / `ZoneDefinition` / `PlayerDefinition` inherit from `DefinitionWithSlices`; `Card` / `Zone` / `Player` inherit from `InstanceWithSlices<TDef>`. `Definition` is `protected` on the instance side.

---

## 11. Open Questions From This Sketch

Tracked alongside the sketch so they don't get lost. Numbers are stable; resolve in place.

### Resolved (in subsequent docs)

- ~~**Q1 — State pattern: stateless vs stateful rule.**~~ Resolved by [`engine/03 §Q1`](features/engine/archive/03-game-rules-runtime.md). Services injected at construction; methods take only ephemeral data; state read through Scaffold `IStateScope.Get<TState>(...)`.
- ~~**Q3 — `EndGameCommand` semantics.**~~ Resolved by the `oq-engine-match-lifecycle` design pass, refined twice post-cascade. Current shape (per ADR-0009 amendment item 4): `PilePlayingState.IsCompleted` returns true → `ITurnFlow.NextAfter` returns the framework-shipped `EndOfMatchState(MatchOutcome.Win(player))` → engine emits `TransitionStateCommand` → `TransitionStateHandler` writes slice + enqueues `OnExit`/`OnEnter` → engine pattern-matches `MatchStateSlice.Current is EndOfMatchState` and **auto-dispatches `EndMatchCommand(outcome)`** → `EndMatchHandler` flips `engine.Session = EndedSession(outcome)` and emits `MatchEndedSignal` → `SessionGate` rejects subsequent dispatches. No game-side `PileGameOverState` or `EnterCommands` wiring. The 2026-05-12 design-pass Q1 = E's *spirit* (terminal state drives session close) survives; the *wiring* moves to framework auto-emit. See §9.5 for the full sequence.
- ~~**Q8 — `IHandlerContext` shape.**~~ Originally resolved as a three-service context (`Store` + `IAvailableActions` + `IDecisionResolver`). Subsequently **retired** by [ADR-0010](decisions/0010-validator-handler-split.md): `IHandlerContext` archived (see [`effects/archive/03-handler-context.md`](features/effects/archive/03-handler-context.md)); handler / validator abstract bases inject `Store` directly via `[Inject] protected Store`. `IAvailableActions` separately retired by ADR-0009 (legal-command surfacing on `MatchState.Actions`). Decision resolution moved to the request system per [ADR-0011](decisions/0011-effect-orchestration-dispatch-requests.md) (`IRequestService` + `RequestsSlice` + `AnswersSlice`). Pile Counter uses neither (no async decisions).
- ~~**Q9 — `IHandlerRegistry` shape.**~~ Resolved session-20260501-1132 Q1: registry is resolver-only (`ICommandHandler Resolve(GameCommand)`); validate/execute live on `ICommandPipeline`; game authors extend `CommandHandler<TCommand, TResult>`.
- ~~**Q10 — `IntakeContext.Pending` type.**~~ `IntakeContext`, `Pending`, and `IIntakeQueue` all deleted per [ADR-0006](decisions/0006-two-pipeline-architecture.md). Command queue is engine-private FIFO with no runtime service.
- ~~**Q13 — `FilterTriggers` default impl.**~~ Initially resolved with a virtual identity default. Subsequently **retired entirely** per [ADR-0013](decisions/0013-engine-pipeline-orchestration.md): `runtime.FilterTriggers` removed from `GameRulesRuntime`; trigger eligibility moves into the trigger itself via `Trigger<T>.Validate(cmd, scope)`. One eligibility surface, not two.
- ~~**Q17 — `IGameStateProvider` shape.**~~ Folded away. The framework binds directly to Scaffold `IStateScope` (read) and concrete `Store` (read + execute mutators) — see [`effects/02-effect-scope.md`](features/effects/archive/02-effect-scope.md). Handler/validator abstract bases inject `Store` directly per [ADR-0010](decisions/0010-validator-handler-split.md). No invented provider type.
- ~~**Q18 — Effect-side service symmetry.**~~ Resolved post-cascade: `IEffectScope` (per [ADR-0011](decisions/0011-effect-orchestration-dispatch-requests.md) and [`effects/02-effect-scope.md`](features/effects/archive/02-effect-scope.md)) carries `IStateScope` + single async `Dispatch<TResult>(GameCommand)` + `Cancel`. Handlers (per [ADR-0010](decisions/0010-validator-handler-split.md)) inject `Store` via the abstract base. Symmetry: both reach state via `IStateScope`; effects write/queue through `Dispatch`, handlers write through `Store`.
- ~~**Q19 — `PileRefsAt` index source.**~~ Resolved post-cascade, reaffirmed twice. Canonical pattern is `scope.GetAll<TState>()` — `IsCompleted` reads `scope.GetAll<PileState>()`; the playing VM reads `scope.GetAll<ZoneContentsState>()`. No per-game index slice needed; pre-cascade `PileRefsState` retires. Locked 2026-05-12 design pass (Q2 = A); reaffirmed in a second pass after PR #141 reintroduced `PileRefsState` and was reverted. `IStateScope.GetAll<TState>` is the canonical API on [`effects/02:78`](features/effects/02-effect-scope.md). `bl-pile-refs-index` softens to `defer` — see [`ledgers/OPEN-ITEMS.md`](ledgers/OPEN-ITEMS.md).
- ~~**Q20 — Player-intent shape for "move card from pile to pile".**~~ Resolved 2026-05-12 design pass (Q3 = A). `DropCardIntent(Ref<Card> Card, Ref<Zone> From, Ref<Zone> To)` — gesture producer populates all three at gesture-start (drag UI knows source from drag-origin; debug button matrix knows source from button position). VM `Map` projects directly to `MoveCardCommand` with no scope reads. Decouples intent vocabulary from the "top-only" rule (which lives in the validator).

### Deferred (need decisions, not blocked)

- **Q2 — Action pipeline cleanup.** The handler-lookup → validate → execute → trigger-fold flow is scattered across multiple Models. Needs a dedicated doc that draws the full pipeline explicitly.
- **Q4 — Snapshot revision tag.** Useful for the UI to detect "I have a stale snapshot." Add when a concrete need surfaces (e.g., async render).

### Implementation details (non-blocking)

- ~~**Q5 — `AvailableActions` mutator implementation.**~~ Retired by [ADR-0009](decisions/0009-engine-as-fsm.md). The `IAvailableActions` service + `AvailableActionsState` slice are gone; legal-command surfacing lives on `MatchState.Actions: IReadOnlyList<Type>`. `MatchStateActionGate` (framework-default validator per [ADR-0010](decisions/0010-validator-handler-split.md)) consults it at intake. `engine/04-available-actions.md` archived.
- ~~**Q6 — `IIntentMapper` final shape.**~~ Retired by [ADR-0012](decisions/0012-presentation-per-state-view-models.md). Per-state `MatchStateViewModel<TState>.Map(intent) → GameCommand` replaces the global mapper. `ActionTemplate` + `PlayerChoice` also retire (typed-descriptor wire form replaced by per-VM observables). `actions/02-intent-mapper.md` + `actions/05-available-actions-snapshot.md` archived. For Pile Counter, see §9.9 — `PilePlayingViewModel.Map(DropCardIntent)`.
- **Q7 — `IGameStore` final shape.** Sketched as a presentation-side wrapper exposing `State`, `OnStateChanged`, `SubmitIntent`. Final shape depends on the chosen state library.
- ~~**Q11 — `IntakeResult.Reason`**~~ — `IntakeResult` deleted per [ADR-0006](decisions/0006-two-pipeline-architecture.md); rejection now flows through the try-get pattern (`bool Dispatch(GameAction, out string? reason)`). The reason remains a free-form string; structured enum would be a follow-up if a real game forces it.
- **Q12 — Re-validation at dequeue.** Validate at intake plus a second time before handler runs (state may have changed). Currently OK as double-validate.

### Out of scope for Pile Counter (will surface in richer games)

- **Q14 — Async decision affordances.** When an effect awaits player input (target picker, choice overlay), the affordance shape may need to express "the player must respond *here*" vs. "the player has free input." Pile Counter has no async; deferred.
- **Q15 — Multiplayer / hidden info snapshots.** Different players see different affordances. Snapshot may need scoping per actor. Pile Counter is single-player; deferred.
- **Q16 — Snapshot diffing.** For performance, the engine may compute snapshot deltas rather than full recomputes. Deferred.

---

## 12. How This Doc Is Used

When designing or revisiting a framework piece, sketch its Pile Counter implementation alongside the framework type. If the sketch is awkward, the framework is wrong. If the sketch is trivial, the framework is at least adequate for the floor.

Whenever the framework shape changes, update §9 in this doc *first*, then propagate to the affected feature Models. The sketch is the source of truth for what "actual game code" looks like; abstract framework docs follow from it.

If a question opens during sketching, add it to §11 with a stable Q-number. Resolve in place when answered. This keeps the open list local to where the sketch lives.
