---
type: sample
status: locked
references: [features/engine/models/01-game-engine.md, features/engine/models/03-game-rules-runtime.md, features/engine/models/05-game-factory.md, features/engine/models/06-game-definition.md, features/engine/models/07-match-state.md, features/engine/models/08-turn-flow.md, features/engine/models/09-state-based-actions.md, features/actions/models/01-command-pipeline.md, features/actions/models/03-command-handler.md, features/actions/models/06-command-validator.md, features/effects/models/02-effect-scope.md, features/cards/models/01-card-model.md, features/zones/models/01-zone-model.md, features/players/models/01-player-model.md, features/entities/models/02-instance.md, features/presentation/models/01-match-state-view-model.md, features/presentation/models/02-view-model-registry.md, decisions/0009-engine-as-fsm.md, decisions/0010-validator-handler-split.md, decisions/0011-effect-orchestration-dispatch-requests.md, decisions/0012-presentation-per-state-view-models.md]
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

After a move resolves, if `pile.Counter == pile.Target` for **all four piles** simultaneously, the game ends and the player wins. The orchestration threads three settled framework mechanisms: the game's `PileCounterWinSBA` (per [`engine/09-state-based-actions.md`](features/engine/models/09-state-based-actions.md)) returns `PileGameOverState(MatchOutcome.Win(player))`; the engine routes it through `TransitionStateCommand`; `TransitionStateHandler` writes `MatchStateSlice.Current = PileGameOverState` and enqueues `PileGameOverState.EnterCommands = [new EndMatchCommand(outcome)]` (per [`engine/07-match-state.md`](features/engine/models/07-match-state.md)); the framework's `EndMatchHandler` flips `engine.Session = EndedSession(outcome)` and emits `MatchEndedSignal` (per [`engine/01-game-engine.md`](features/engine/models/01-game-engine.md)). Every subsequent `Dispatch` hits the framework-default `SessionGate` validator and rejects.

There is no lose condition. The puzzle is always solvable from setup; the player keeps moving until they hit it.

---

## 7. What Pile Counter Tests In The Framework

This game forces the framework to express:

- **Zones with stack ordering and a derived score.** The score is a per-zone value cached in a `PileState` slice keyed at the pile's `Ref<Zone>`. Maintaining the cache atomically with each move tests the per-zone-type slice + mutator pattern from [`zones/01-zone-model.md`](features/zones/models/01-zone-model.md).
- **A single action type with legality rules.** Tests the validator/handler split per [ADR-0010](decisions/0010-validator-handler-split.md) — `MoveCardHandler` extends `ValidatedCommandHandler<MoveCardCommand, MoveCardResult>` with four rejection paths, plus the framework defaults `SessionGate` + `MatchStateActionGate`. Tests `MatchState.Actions: IReadOnlyList<Type>` as the framework's prophylactic legality surface ([`engine/07-match-state.md`](features/engine/models/07-match-state.md)). Tests per-state VM observable affordance projection ([`presentation/01-match-state-view-model.md`](features/presentation/models/01-match-state-view-model.md)) and how legality interacts with the "can't move same card twice in a row" memory.
- **State-based action for win check + framework-orchestrated session close.** Classic SBA job: `PileCounterWinSBA.Check` reads state, returns `PileGameOverState` when the win condition holds; the engine routes the terminal state through `TransitionStateCommand`; `PileGameOverState.EnterCommands` carries `EndMatchCommand(outcome)` which flips `engine.Session` to `EndedSession`. Tests the `ISBA` + `MatchState.EnterCommands` patterns end-to-end ([`engine/09-state-based-actions.md`](features/engine/models/09-state-based-actions.md), [`engine/07-match-state.md`](features/engine/models/07-match-state.md)).
- **Per-game enumeration without a per-game index.** The win SBA reads all piles via `scope.GetAll<PileState>()`; the playing VM iterates `scope.GetAll<ZoneContentsState>()` to compute affordances. No pile-refs index slice — tests Scaffold's `GetAll<TState>` enumeration as the canonical pattern.
- **Move history of size 1.** "Last card moved" is a sliver of state that doesn't fit on a card or a zone — it's *game-level* memory. It lives in a singleton `LastMoveState` slice declared by the `GameDefinition`'s `GetInitialSlices()`.
- **`GameDefinition` bundle composition.** The puzzle's deck, pile targets, and initial deal flow through a `PileCounterGameDefinition` bundle. Tests the bundle shape from [`engine/06-game-definition.md`](features/engine/models/06-game-definition.md).
- **Per-state ViewModels + ViewModelRegistry.** `PilePlayingViewModel : MatchStateViewModel<PilePlayingState>` and `PileGameOverViewModel : MatchStateViewModel<PileGameOverState>`; presenter rebinds via `ViewModelRegistry` on type change. Per-VM `Map(DropCardIntent) → MoveCardCommand` replaces the retired global `IIntentMapper` per [ADR-0012](decisions/0012-presentation-per-state-view-models.md).
- **Match setup at `Build()`.** The factory walks `puzzle.Deal` and spawns each card directly during `Build()`; `engine.StartMatch()` then transitions `NotStartedSession → InMatchSession` and seeds `MatchStateSlice.Current` from `ITurnFlow.Initial = new PilePlayingState()`.

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

State lives as Scaffold `BaseState` slices, each owning one concern. There is no single-record `GameState`; reads go through `IStateScope.Get<TState>()`. See [`effects/02-effect-scope.md`](features/effects/models/02-effect-scope.md) for the binding.

```csharp
// Per-pile slice — Counter is the cached sum of card Numbers in the pile;
// Target is the authored win condition. Keyed externally at pile.SelfRef
// (Ref<Zone>) by the factory walk per [zones/01](features/zones/models/01-zone-model.md).
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
- **`MatchStateSlice`** — singleton. Holds `MatchState Current` per [`engine/07-match-state.md`](features/engine/models/07-match-state.md) — `PilePlayingState` during play, `PileGameOverState` at end. Framework auto-seeded from `ITurnFlow.Initial` at `Build()`.
- **`HistoryState`** — framework-owned; auto-seeded. Receives an envelope per command.
- **`PendingEffectsSlice`** / **`RequestsSlice`** / **`AnswersSlice`** — framework-owned; auto-seeded. Empty for Pile Counter (no effects, no async decisions). The engine's drain-policy guards (per [ADR-0011](decisions/0011-effect-orchestration-dispatch-requests.md)) treat them as no-ops.

Slices are immutable; mutators return new records. The runtime holds no game-affecting state.

> **Note (post-cascade).** The pre-cascade sketch carried a game-side `PileRefsState` singleton holding `IReadOnlyList<Ref<Zone>>` so the runtime / snapshot builder could iterate piles in order. Per the 2026-05-12 design pass (Q2 = A), it retires — the canonical pattern is `scope.GetAll<TState>()` (see [`engine/09 §Pile Counter — win-condition SBA`](features/engine/models/09-state-based-actions.md), [`presentation/01 §Pile Counter — single playing-state VM`](features/presentation/models/01-match-state-view-model.md)). If ordering stability becomes a concern, add an explicit `Index` field to `PileState` — Scaffold's keying typically preserves insertion order from the `SharedZones` walk so this is rarely needed.

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

The framework primitive is **generic** `MoveCardCommand` — not `MoveTopCardCommand`. The "top only" rule is a Pile Counter constraint enforced in the validator, not a framework concept. `EndMatchCommand` is framework-owned ([`engine/01`](features/engine/models/01-game-engine.md)); Pile Counter dispatches it via `PileGameOverState.EnterCommands` — see §9.3 and §9.5.

The actual zone move (remove from source, rotate `CardRef`, insert into destination) goes through the framework's `MoveCardMutation` `CompositeAction` — see [`cards/01-card-model.md` § Mutations](features/cards/models/01-card-model.md). The handler doesn't write `ZoneContentsState` directly; it issues the composite.

### 9.3 Match-state shape

Pile Counter has no phase structure — one perpetual `PilePlayingState`; the win condition transitions to `PileGameOverState`. Both extend `MatchState` per [`engine/07-match-state.md`](features/engine/models/07-match-state.md).

```csharp
public sealed record PilePlayingState : MatchState
{
    public override IReadOnlyList<Type> Actions => [typeof(MoveCardCommand)];
    // ShouldExit always false — transitions are SBA-driven (win check in §9.4).
    // No EnterCommands — initial state seeded at Build() from ITurnFlow.Initial.
}

public sealed record PileGameOverState(MatchOutcome Outcome) : MatchState
{
    public override IReadOnlyList<Type>       Actions       => [];
    public override IReadOnlyList<GameAction> EnterCommands => [
        new EndMatchCommand(Outcome),
    ];
}
```

`PilePlayingState.Actions` lists the single command type valid during play — the framework's `MatchStateActionGate` validator (per [ADR-0010](decisions/0010-validator-handler-split.md)) rejects anything else automatically. `PileGameOverState.EnterCommands` carries `EndMatchCommand(outcome)` so session-close orchestrates through the framework's pipeline; see §9.5 for the full sequence.

### 9.4 Turn flow + state-based actions

Pile Counter's turn structure is trivial — one perpetual state, no phase advancement. `ITurnFlow.NextAfter` is never reached because `PilePlayingState.ShouldExit` always returns false; transitions to `PileGameOverState` happen via `ISBA`.

```csharp
public sealed class PileCounterTurnFlow : ITurnFlow
{
    public MatchState Initial               => new PilePlayingState();
    public MatchState NextAfter(MatchState) => throw new InvalidOperationException(
        "PileCounter never exits PilePlayingState via ShouldExit; transitions are SBA-driven.");
}

public sealed class PileCounterWinSBA : ISBA
{
    private readonly IActor _player;

    public PileCounterWinSBA(IReadOnlyList<IActor> actors)
        => _player = actors.Single();

    public MatchState? Check(IStateScope scope)
    {
        var piles = scope.GetAll<PileState>();
        return piles.All(p => p.Counter == p.Target)
            ? new PileGameOverState(new MatchOutcome.Win(_player))
            : null;
    }
}
```

See [`engine/08-turn-flow.md`](features/engine/models/08-turn-flow.md) for the flow contract and [`engine/09-state-based-actions.md`](features/engine/models/09-state-based-actions.md) for SBA semantics (pure observers, first non-null wins, registration-order priority).

### 9.5 Win-condition orchestration — SBA → transition → end-match

Pile Counter's match-end flow stitches three settled framework mechanisms together end-to-end:

```
PileCounterWinSBA.Check returns PileGameOverState(outcome)
              │
              ▼  engine drain detects type change
        TransitionStateCommand(PileGameOverState)
              │
              ▼  TransitionStateHandler
   1. Store.Execute(SetMatchStatePayload(PileGameOverState))
   2. enqueue PileGameOverState.EnterCommands = [EndMatchCommand(outcome)]
              │
              ▼  EndMatchHandler
   engine.Session = EndedSession(outcome)
   emit MatchEndedSignal
              │
              ▼  SessionGate now rejects further dispatches
```

The SBA returns the terminal state; the engine routes it through `TransitionStateCommand` (framework-internal; bypasses the validator chain per [`engine/01`](features/engine/models/01-game-engine.md)). `TransitionStateHandler` writes the slice, *then* enqueues `EnterCommands` in order. The framework-owned `EndMatchCommand` flips `engine.Session` to `EndedSession(outcome)` and emits `MatchEndedSignal`. Every subsequent `Dispatch` hits the framework-default `SessionGate` validator and rejects.

No game-side observer is needed for session close — the orchestration is entirely data-driven via `PileGameOverState.EnterCommands`. Per [`engine/01:276`](features/engine/models/01-game-engine.md): *"Runtime/SBA drives by returning a `GameOverState`-shaped `MatchState`, whose `EnterCommands` typically include `EndMatchCommand(outcome)`. … All paths reach `EndMatchHandler.Handle`."*

### 9.6 Move validator + handler

Per [ADR-0010](decisions/0010-validator-handler-split.md), command-specific legality lives in `ICommandValidator<TCommand>` separate from the handler. The common case — one validator + one handler for the same command — uses the combined `ValidatedCommandHandler<TCommand, TResult>` base. `Store` comes from the base via `[Inject]`; no `IHandlerContext`. See [`actions/03-command-handler.md`](features/actions/models/03-command-handler.md) and [`actions/06-command-validator.md`](features/actions/models/06-command-validator.md).

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
        var card   = Store.Catalog.Resolve(move.Card);
        var source = Store.Catalog.Resolve(move.From);
        var dest   = Store.Catalog.Resolve(move.To);

        var number = card.Storage.GetVariable<int>(Vars.Number);

        // Atomic batch: the framework's MoveCardMutation handles the actual zone
        // move (ZoneContentsState updates + CardRef rotation); the game-side
        // payloads update the cached pile counters and the last-move memory.
        Store.ExecuteBatch(new object[]
        {
            MoveCardMutation.AtTop(card, source, dest),
            new AdjustPileCounterPayload(move.From, -number),
            new AdjustPileCounterPayload(move.To,   +number),
            new SetLastMovePayload(new LastMove(move.Card, move.From, move.To)),
        });

        return Task.FromResult(new MoveCardResult(move.Card));
    }
}
```

Two framework-default validators run before `MoveCardHandler.Validate`:

- **`SessionGate`** — rejects when `engine.Session` is not `InMatchSession` (lifecycle commands bypass).
- **`MatchStateActionGate`** — rejects when `command.GetType()` is not in `MatchStateSlice.Current.Actions` (during `InMatchSession`); lifecycle commands + `TransitionStateCommand` bypass.

Both register first in DI order; Pile Counter's `MoveCardHandler.Validate` (as the validator side of the combined base) runs only after both pass. Short-circuit on first reject; rejection reason surfaces via `engine.Dispatch`'s `out string? reason`.

The handler issues one `ExecuteBatch` call. Scaffold's payload-keyed registry runs every mutator registered for each payload in the batch and commits them atomically. Slice subscribers fire once per affected slice with the post-batch state.

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

The framework's `MoveCardMutation` handles `ZoneContentsState` and `CardMapState` updates via its composite expansion — see [`cards/01-card-model.md` § Composite expansion](features/cards/models/01-card-model.md). No game-authored mutator is needed for the zone move itself.

### 9.8 Rules definition + runtime

Per [ADR-0009](decisions/0009-engine-as-fsm.md), the runtime is structurally an FSM evaluator. The default `Evaluate` body composes SBAs preempt → request pause → pending-effect pause → `ShouldExit` + `Flow.NextAfter` — Pile Counter overrides nothing. See [`engine/03-game-rules-runtime.md`](features/engine/models/03-game-rules-runtime.md).

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
// (Pile Counter has no stack, no decks, no priority).
public sealed class PileCounterRulesRuntime : GameRulesRuntime<PileCounterRulesDefinition>
{
    protected override ITurnFlow            Flow              { get; }
    protected override IReadOnlyList<ISBA>  StateBasedActions { get; }

    public PileCounterRulesRuntime(
        PileCounterRulesDefinition definition,
        IStateScope                state,
        PileCounterTurnFlow        flow,
        PileCounterWinSBA          winSBA)
        : base(definition, state)
    {
        Flow              = flow;
        StateBasedActions = [winSBA];
    }

    // FilterTriggers omitted — base virtual identity default suffices (no triggers).
    // Evaluate omitted — default body covers it:
    //   SBAs preempt (win check) → request pause → pending-effect pause →
    //   current.ShouldExit + Flow.NextAfter.
    // For Pile Counter, the win SBA is the only decision point; ShouldExit
    // always returns false so Flow is never consulted.
}
```

The runtime constructor takes its typed definition, Scaffold's `IStateScope`, the `ITurnFlow` implementation, and the win SBA — all DI-resolved. Per [ADR-0009](decisions/0009-engine-as-fsm.md), `IAvailableActions` is not injected; legal-command surfacing lives on `MatchState.Actions`. Per [ADR-0006](decisions/0006-two-pipeline-architecture.md), `IIntakeQueue` is gone; the command queue is engine-private FIFO.

### 9.9 Per-state ViewModels

Per [ADR-0012](decisions/0012-presentation-per-state-view-models.md), each `MatchState` subtype has a typed `MatchStateViewModel<TState>`. The presenter subscribes to `MatchStateSlice`; on type change it rebinds via `ViewModelRegistry`. The per-state `Map(intent) → GameCommand` replaces the retired global `IIntentMapper`. See [`presentation/01-match-state-view-model.md`](features/presentation/models/01-match-state-view-model.md) and [`presentation/02-view-model-registry.md`](features/presentation/models/02-view-model-registry.md).

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

public sealed class PileGameOverViewModel : MatchStateViewModel<PileGameOverState>
{
    private readonly ObservableProperty<MatchOutcome> outcome = new();
    public IObservable<MatchOutcome> Outcome => outcome;

    public override void Bind(PileGameOverState state, IStateScope scope)
        => outcome.Value = state.Outcome;

    public override GameCommand Map(PlayerIntent intent)
        => throw new InvalidOperationException("No player input in PileGameOverState.");
}
```

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

The validator chain is uniform across the entire `GameCommand` family (`GameAction`, `CompositeAction`, `GameSignal`, `Request<TResponse>`, `Response`) per [ADR-0011](decisions/0011-effect-orchestration-dispatch-requests.md). For Pile Counter, only `GameAction` matters in practice — there are no composites authored by the game, no signals beyond the framework's, no requests, no responses.

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
// + DefaultPlayerDefinition + game-singleton slices. Per [engine/06](features/engine/models/06-game-definition.md).
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
    // SBAs, per-state ViewModels) are registered in PileCounterInstaller below.
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
        // MatchStateSlice.Current seeded from ITurnFlow.Initial = new PilePlayingState();
        // PilePlayingState.EnterCommands fires (empty for this game).

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

        // Runtime, turn flow, SBA.
        builder.Register<GameRulesRuntime<PileCounterRulesDefinition>, PileCounterRulesRuntime>(Lifetime.Transient);
        builder.Register<PileCounterTurnFlow>      (Lifetime.Singleton);
        builder.Register<PileCounterWinSBA>        (Lifetime.Singleton).AsImplementedInterfaces();

        // Per-state ViewModels + registry.
        builder.Register<PilePlayingViewModel>     (Lifetime.Transient);
        builder.Register<PileGameOverViewModel>    (Lifetime.Transient);
        builder.Register<ViewModelRegistry>        (Lifetime.Singleton).WithFactory(sp =>
            new ViewModelRegistryBuilder(sp.Resolve<IStateScope>(), sp)
                .Register<PilePlayingState,  PilePlayingViewModel>()
                .Register<PileGameOverState, PileGameOverViewModel>()
                .Build());
    }
}
```

Match construction is a two-layer split (locked in [`engine/05-game-factory.md`](features/engine/models/05-game-factory.md)):

- **App-level installer** — the framework's `EngineInstaller` registers `IStateScope`, `ICommandPipeline`, `Store`, `IGameFactory`, the framework-default validators (`SessionGate`, `MatchStateActionGate`), framework-internal handlers (`StartMatchHandler`, `EndMatchHandler`, `TransitionStateHandler`), and framework mutators (entity-bridge, `CardMapState`, `OwnershipState`, `CardVisibilityState`, `ZoneContentsState`, `SetMatchStateMutator`). The game's installer (`PileCounterInstaller` above) registers the validated handler, game-authored mutators, the runtime against its definition's closed generic (`GameRulesRuntime<PileCounterRulesDefinition> → PileCounterRulesRuntime`), the turn flow, the SBA, and the per-state ViewModels + registry. Each runs once per app. See [`engine/05` § Registration contract](features/engine/models/05-game-factory.md).
- **Per-match factory** — the bootstrap call site asks `IGameFactory` for a new game, supplying only per-match data: the `GameDefinition` bundle, actor catalog, RNG seed. VContainer never appears in this code.

The framework's `MoveCardMutation` `CompositeAction` handles the zone-move expansion (remove + rotate `CardRef` + insert) atomically; the game-side mutators (`PileCounterMutator` × 2, `LastMoveMutator`) update cached counters and the last-move memory in the same `ExecuteBatch`. After `Build` returns the engine is in `NotStartedSession`; `engine.StartMatch()` transitions to `InMatchSession` and seeds `MatchStateSlice.Current = new PilePlayingState()`.

---

## 10. Framework Decisions Surfaced From This Sketch

These are decisions the Pile Counter sketch forced or confirmed. Each affects the framework, not just this game.

- **Generic primitive + rule-enforced constraint.** Framework ships generic `MoveCardCommand`; "top only" is enforced in the validator side of `MoveCardHandler`. No `MoveTopCardCommand` primitive.
- **Validator / handler split.** Per [ADR-0010](decisions/0010-validator-handler-split.md), `ICommandValidator<TCommand>` is independent of `ICommandHandler<TCommand, TResult>`. Common case (one validator + one handler for the same command) uses the combined `ValidatedCommandHandler<TCommand, TResult>` base. `Store` is `[Inject]`-property on the base; no `IHandlerContext` parameter.
- **`Validate` signature is Try-pattern.** `bool Validate(TCommand, out string? reason)`. Uniform across all `GameCommand` siblings.
- **Five validation layers** ([ADR-0007](decisions/0007-five-validation-layers.md) refined by ADRs 0010 / 0012): (1) per-state VM affordance projection (UI prophylactic), (2) `Map(intent)` well-formedness (game), (3) framework-default validators `SessionGate` + `MatchStateActionGate`, (4) registered command validators (game-shipped legality — authoritative), (5) before-fold `Cancel` (effect-driven, mid-execution).
- **Rule holds zero game-affecting state.** `LastMove` lives in its own `LastMoveState` slice, written by a registered `Mutator<LastMoveState, SetLastMovePayload>`. Locked as contract.
- **State mutators are payload-keyed, not command-keyed** (Scaffold State pattern). Handlers issue `Store.Execute(payload)` or `Store.ExecuteBatch(...)`; every `Mutator<TState, TPayload>` registered for that payload type runs and commits atomically. Handlers don't enumerate ancillary state slots.
- **Framework `MoveCardMutation` (CompositeAction) handles zone moves.** Game-authored mutators handle game-side concerns (counter cache, last-move memory). The split is structural — see [`cards/01-card-model.md`](features/cards/models/01-card-model.md).
- **Player input layer is per-state, not global.** Per [ADR-0012](decisions/0012-presentation-per-state-view-models.md): `PlayerIntent` (presentation feature-root vocabulary; game-defined typed records) → `MatchStateViewModel<TState>.Map(intent)` → `GameCommand` (engine). The global `IIntentMapper` retires; per-state VMs own the mapping. `DropCardIntent` carries `(Card, From, To)` — the gesture producer populates all three at gesture-start (2026-05-12 design pass Q3 = A).
- **Match-state legality lives on `MatchState.Actions: IReadOnlyList<Type>`.** Per [ADR-0009](decisions/0009-engine-as-fsm.md). The framework's `MatchStateActionGate` validator rejects commands whose type isn't in the current state's `Actions`. No `AvailableActionsState` slice; no `IAvailableActions` service; no wire-form `ActionTemplate`. Richer affordance projections live as observables on the per-state `MatchStateViewModel<TState>`.
- **Engine IS the FSM.** Per ADR-0009: `runtime.Evaluate(action, result) → MatchState`. Engine compares the returned type to `MatchStateSlice.Current.GetType()`; on type change, dispatches `TransitionStateCommand` through the normal pipeline. Same-type returns are no-ops. `TransitionStateHandler` writes the slice and enqueues the new state's `EnterCommands`.
- **Match-end orchestration via `EnterCommands`.** Pile Counter's `PileGameOverState.EnterCommands = [new EndMatchCommand(Outcome)]`. The SBA returns the terminal state; `TransitionStateHandler` enqueues the EnterCommand; `EndMatchHandler` flips `engine.Session = EndedSession(outcome)`. Documented as canonical per [`engine/01:276`](features/engine/models/01-game-engine.md); locked in 2026-05-12 design pass Q1 = E.
- **Cross-cutting state-driven overrides via `ISBA`.** Per ADR-0009. Pile Counter's win check is `PileCounterWinSBA` (returns `PileGameOverState` when all piles match targets). Registered in DI order on the runtime; first non-null wins. SBAs are pure observers — no state writes, no dispatch.
- **Successor knowledge lives in `ITurnFlow.NextAfter`.** Per ADR-0009. Pile Counter's `PileCounterTurnFlow.Initial = new PilePlayingState()`; `NextAfter` throws because `ShouldExit` always returns false (SBA-driven transitions only).
- **Session lifecycle is an engine field, not a Store slice.** Per ADR-0009 D12: `engine.Session : SessionState` (sealed hierarchy — `NotStartedSession` / `InMatchSession` / `EndedSession`). Replaces the retired `EngineLifecycle` enum on `EngineState`. The framework-default `SessionGate` validator reads `engine.Session` directly.
- **Per-state polymorphic ViewModels + `ViewModelRegistry`.** Per ADR-0012. Each `MatchState` subtype has a typed `MatchStateViewModel<TState>` (game-authored); presenter subscribes to `MatchStateSlice` and rebinds via `ViewModelRegistry` on type change. Per-VM `Map(intent) → GameCommand` replaces the retired global `IIntentMapper`.
- **Per-game enumeration via `scope.GetAll<TState>()`.** No per-game zone-refs index slice — Pile Counter's win SBA reads `scope.GetAll<PileState>()`; the playing VM reads `scope.GetAll<ZoneContentsState>()`. Locked in 2026-05-12 design pass Q2 = A (retires the pre-cascade `PileRefsState`).
- **Two-pipeline architecture** ([ADR-0006](decisions/0006-two-pipeline-architecture.md)). Command pipeline is framework-FIFO with the validator chain at intake; effect-level orchestration is the separate surface defined by [ADR-0011](decisions/0011-effect-orchestration-dispatch-requests.md) (`PendingEffectsSlice` + async `IEffect.Run` coroutines + request system). Pile Counter exercises only the command pipeline.
- **`GameDefinition` bundle composition.** `PileCounterGameDefinition` consolidates Rules + CardCatalog + SharedZones + DefaultPlayerDefinition + game-singleton slices. Per-puzzle variation flows through the constructor. [`engine/06`](features/engine/models/06-game-definition.md).
- **Match setup at `Build()`.** The factory walks `puzzle.Deal` and spawns each card directly during `Build()`. `engine.StartMatch()` transitions to `InMatchSession`; `MatchStateSlice.Current` seeds from `ITurnFlow.Initial`; the initial state's `EnterCommands` fire on the first drain (empty for Pile Counter).
- **Engine intake order.** Per ADR-0010: `engine.Dispatch(command)` resolves all `ICommandValidator<TCommand>` for the command's runtime type, runs them in DI registration order, short-circuits on first reject. Framework defaults (`SessionGate`, `MatchStateActionGate`) register first; games append. No inline lifecycle gate.
- **Rules layer is split into `GameRulesDefinition` (data) + `GameRulesRuntime<TDefinition>` (runtime).** Definition is `[Serializable]` data; runtime constructor takes the definition + `IStateScope` + `ITurnFlow` + `IReadOnlyList<ISBA>` + game-specific subsystems. Two-method contract (`FilterTriggers` + `Evaluate`); default `Evaluate` body composes SBAs preempt → request pause → pending-effect pause → `ShouldExit` + `Flow.NextAfter`. Per-match factory uses container open-generic resolution.
- **Definition / Instance layered on framework parents.** `CardDefinition` / `ZoneDefinition` / `PlayerDefinition` inherit from `DefinitionWithSlices`; `Card` / `Zone` / `Player` inherit from `InstanceWithSlices<TDef>`. `Definition` is `protected` on the instance side.

---

## 11. Open Questions From This Sketch

Tracked alongside the sketch so they don't get lost. Numbers are stable; resolve in place.

### Resolved (in subsequent docs)

- ~~**Q1 — State pattern: stateless vs stateful rule.**~~ Resolved by [`engine/03 §Q1`](features/engine/models/03-game-rules-runtime.md). Services injected at construction; methods take only ephemeral data; state read through Scaffold `IStateScope.Get<TState>(...)`.
- ~~**Q3 — `EndGameCommand` semantics.**~~ Resolved by the `oq-engine-match-lifecycle` design pass, refined post-cascade. Pile Counter's `PileCounterWinSBA` returns `PileGameOverState(MatchOutcome.Win(player))`; the engine routes the terminal state through `TransitionStateCommand`; `PileGameOverState.EnterCommands = [new EndMatchCommand(outcome)]` is enqueued by `TransitionStateHandler`; `EndMatchHandler` flips `engine.Session = EndedSession(outcome)` and emits `MatchEndedSignal`. `SessionGate` rejects subsequent dispatches. Locked as canonical per [`engine/01:276`](features/engine/models/01-game-engine.md) and the 2026-05-12 design pass (Q1 = E). See §9.5 for the full sequence.
- ~~**Q8 — `IHandlerContext` shape.**~~ Originally resolved as a three-service context (`Store` + `IAvailableActions` + `IDecisionResolver`). Subsequently **retired** by [ADR-0010](decisions/0010-validator-handler-split.md): `IHandlerContext` archived (see [`effects/archive/03-handler-context.md`](features/effects/archive/03-handler-context.md)); handler / validator abstract bases inject `Store` directly via `[Inject] protected Store`. `IAvailableActions` separately retired by ADR-0009 (legal-command surfacing on `MatchState.Actions`). Decision resolution moved to the request system per [ADR-0011](decisions/0011-effect-orchestration-dispatch-requests.md) (`IRequestService` + `RequestsSlice` + `AnswersSlice`). Pile Counter uses neither (no async decisions).
- ~~**Q9 — `IHandlerRegistry` shape.**~~ Resolved session-20260501-1132 Q1: registry is resolver-only (`ICommandHandler Resolve(GameCommand)`); validate/execute live on `ICommandPipeline`; game authors extend `CommandHandler<TCommand, TResult>`.
- ~~**Q10 — `IntakeContext.Pending` type.**~~ `IntakeContext`, `Pending`, and `IIntakeQueue` all deleted per [ADR-0006](decisions/0006-two-pipeline-architecture.md). Command queue is engine-private FIFO with no runtime service.
- ~~**Q13 — `FilterTriggers` default impl.**~~ `GameRulesRuntime<TDefinition>` ships a virtual identity default ([`engine/03-game-rules-runtime.md` §FilterTriggers](features/engine/models/03-game-rules-runtime.md)); games with no eligibility logic don't override it.
- ~~**Q17 — `IGameStateProvider` shape.**~~ Folded away. The framework binds directly to Scaffold `IStateScope` (read) and concrete `Store` (read + execute mutators) — see [`effects/02-effect-scope.md`](features/effects/models/02-effect-scope.md). Handler/validator abstract bases inject `Store` directly per [ADR-0010](decisions/0010-validator-handler-split.md). No invented provider type.
- ~~**Q18 — Effect-side service symmetry.**~~ Resolved post-cascade: `IEffectScope` (per [ADR-0011](decisions/0011-effect-orchestration-dispatch-requests.md) and [`effects/02-effect-scope.md`](features/effects/models/02-effect-scope.md)) carries `IStateScope` + single async `Dispatch<TResult>(GameCommand)` + `Cancel`. Handlers (per [ADR-0010](decisions/0010-validator-handler-split.md)) inject `Store` via the abstract base. Symmetry: both reach state via `IStateScope`; effects write/queue through `Dispatch`, handlers write through `Store`.
- ~~**Q19 — `PileRefsAt` index source.**~~ Resolved post-cascade. The canonical pattern is `scope.GetAll<TState>()` — the win SBA reads `scope.GetAll<PileState>()`; the playing VM reads `scope.GetAll<ZoneContentsState>()`. No per-game index slice needed; the pre-cascade `PileRefsState` retires. Locked in the 2026-05-12 design pass (Q2 = A). `bl-pile-refs-index` softens/closes as a consequence — see [`ledgers/OPEN-ITEMS.md`](ledgers/OPEN-ITEMS.md).
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
