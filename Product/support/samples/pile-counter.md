---
type: sample
status: locked
references: [features/engine/models/01-game-engine.md, features/engine/models/03-game-rules-runtime.md, features/engine/models/05-game-factory.md, features/engine/models/06-game-definition.md, features/engine/models/07-match-state.md, features/engine/models/08-turn-flow.md, features/actions/models/01-command-pipeline.md, features/actions/models/03-command-handler.md, features/actions/models/06-command-validator.md, features/effects/models/02-effect-scope.md, features/cards/models/01-card-model.md, features/zones/models/01-zone-model.md, features/players/models/01-player-model.md, features/entities/models/02-instance.md, features/presentation/models/01-match-state-view-model.md, decisions/0009-engine-as-fsm.md, decisions/0010-validator-handler-split.md, decisions/0011-effect-orchestration-dispatch-requests.md, decisions/0013-engine-pipeline-orchestration.md]
tags: [#sample, #pile-counter]
---

# Pile Counter — Reference Sample Game

This document defines a small, deliberately simple game we use as a working reference while shaping the framework. Whenever a framework concept feels under- or over-specified, we sketch its implementation in **Pile Counter** and see where the seams are.

The game has no triggers, no abilities, no players-vs-players, no async decisions. That's intentional — it's the floor of what the framework must support, before any of the harder features pile on. As of 2026-05-11, this sketch reflects the post-FSM-arch + ADR-0013 + Q-walk-locked vocabulary.

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

After a move resolves, if `pile.Counter == pile.Target` for **all four piles** simultaneously, the game ends and the player wins. `runtime.Evaluate` returns the framework-shipped `EndOfMatchState(MatchOutcome.Win(player))`; the engine completes the transition normally and auto-dispatches `EndMatchCommand(outcome)`. `EndMatchCommand`'s handler flips `engine.Session = EndedSession(outcome)` and emits `MatchEndedSignal`. See [`engine/01-game-engine.md`](features/engine/models/01-game-engine.md) §Match-end auto-emit (the banner) and [`engine/07-match-state.md`](features/engine/models/07-match-state.md).

There is no lose condition. The puzzle is always solvable from setup; the player keeps moving until they hit it.

---

## 7. What Pile Counter Tests In The Framework

This game forces the framework to express:

- **Zones with stack ordering and a derived score.** The score is a per-zone value cached in a `PileState` slice keyed at the pile's `Ref<Zone>`. Maintaining the cache atomically with each move tests the per-zone-type slice + mutator pattern from [`zones/01-zone-model.md`](features/zones/models/01-zone-model.md).
- **A single action type with legality rules.** Tests `ICommandValidator<MoveCardAction>` per [ADR-0010](decisions/0010-validator-handler-split.md), and how legality interacts with the "can't move same card twice in a row" memory.
- **State-based win check.** Classic `Evaluate` job: after each action, read state, decide if the game is over by returning `EndOfMatchState(MatchOutcome.Win(player))`. The engine auto-dispatches `EndMatchCommand` per [ADR-0009 refresh](decisions/0009-engine-as-fsm.md). No host orchestrator required.
- **Move history of size 1.** "Last card moved" is a sliver of state that doesn't fit on a card or a zone — it's *game-level* memory. It lives in a singleton `LastMoveState` slice declared by the `GameDefinition`'s `GetInitialSlices()`.
- **`GameDefinition` bundle composition.** The puzzle's deck, pile targets, and initial deal flow through a `PileCounterGameDefinition` bundle. Tests the bundle shape from [`engine/06-game-definition.md`](features/engine/models/06-game-definition.md).
- **`MatchState` action-gate triad.** Pile Counter has a single `PilePlayingState : MatchState` with `Actions = [typeof(MoveCardAction)]` + `ActionsOpen = false` — exercises the post-cascade whitelist mode of `MatchStateActionGate` per the [`engine/07`](features/engine/models/07-match-state.md) banner.
- **Per-state VM with `scope.Subscribe<T>`.** A single `PilePlayingViewModel : MatchStateViewModel<PilePlayingState>` exposes observables for the four piles + computed valid moves. Exercises the post-cascade subscription contract per [ADR-0012](decisions/0012-presentation-per-state-view-models.md) + the spike-Q1 lock.
- **Match setup via `OnEnter`.** `PilePlayingState.OnEnter(scope)` returns the `CreateCardAction` list that spawns each card into its target pile per `puzzle.Deal`. Exercises the lifecycle hook from the [`engine/07`](features/engine/models/07-match-state.md) banner.

This game does **not** test:

- Triggers, abilities, replacement effects, ongoing effects.
- Async decisions (no targeting, no choice prompts; no `Request<TResponse>` traffic).
- Concurrent requests (no second player, no simultaneous decisions).
- Multi-player or priority.
- Phase transitions (only one `MatchState` plus the framework's `EndOfMatchState`).
- Card variant subclasses (cards are distinguished only by their `Number` variable; no per-card-type `CardDefinition` subclass).
- `OnExit` hooks (Pile Counter has nothing to clean up on state exit).

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
- **Restart / new puzzle:** session-lifecycle concern, out of scope for the runtime. Per ADR-0009 refresh (rematch model), `EndedSession` is terminal; new puzzle = `engine.Dispose()` + `GameFactory.Build(...)` for a fresh instance.

All setup decisions are now resolved. The §9 sketch implements them against the canonical Models.

---

## 9. Rules Layer Implementation Sketch

This is the working sketch of Pile Counter against the framework as of this writing. When the framework changes, update this sketch first and propagate to the affected feature Models.

### 9.1 State shape

State lives as Scaffold `BaseState` slices, each owning one concern. There is no single-record `GameState`; reads go through `IStateScope.Get<TState>()`.

```csharp
// Per-pile slice — Counter is the cached sum of card Numbers in the pile;
// Target is the authored win condition. Keyed externally at pile.SelfRef
// (Ref<Zone>) by the factory walk per zones/01.
public sealed record PileState(int Counter, int Target) : BaseState;

// Singleton — the "last moved card" memory. Game-level; doesn't fit on a card
// or a zone. Declared by PileCounterGameDefinition.GetInitialSlices().
public sealed record LastMoveState(LastMove? Move) : BaseState;
public sealed record LastMove(Ref<Card> Card, Ref<Zone> From, Ref<Zone> To);

// Game-side singleton index — list of the four spawned pile refs. Seeded by
// the factory's Build walk; consumed by the VM and runtime to enumerate piles
// without scanning ownership. Per D-PileRefs in PLANS.
public sealed record PileRefsState(IReadOnlyList<Ref<Zone>> Piles) : BaseState;

// The Card variable catalog. One variable in this game.
public static class Vars {
    public static readonly Variable Number = new("number", "int");
}
```

Framework-owned slices Pile Counter reads/writes through:

- **`ZoneContentsState`** — keyed at each pile's `Ref<Zone>`. Holds `ImmutableList<CardRef>`. Source of truth for "which cards are in which pile, in what order."
- **`CardMapState`** — singleton. Maps `CardRef ↔ Ref<Card>` for resolution.
- **`EntityState`** (per card) — keyed at each card's `Ref<Card>`. Holds the `Number` variable. Read via `card.Storage.GetVariable<int>(Vars.Number)`.
- **`MatchStateSlice`** — framework singleton (per ADR-0009). Wraps the polymorphic `MatchState` (Pile Counter's `PilePlayingState` or `EndOfMatchState`).
- **`HistoryState`**, **`PendingEffectsSlice`**, **`RequestsSlice`**, **`AnswersSlice`** — framework-owned auto-seeded; Pile Counter doesn't write them but `HistoryState` records every command.

Slices are immutable; mutators return new records. The runtime holds zero gameplay state.

### 9.2 Match states

Pile Counter has one game-defined `MatchState` plus the framework-shipped `EndOfMatchState`:

```csharp
// The single play state. ActionsOpen = false + whitelist = MoveCardAction:
// MatchStateActionGate rejects anything except moves while in this state.
public sealed record PilePlayingState(IActor Player) : MatchState {
    public override IReadOnlyList<Type> Actions     => [typeof(MoveCardAction)];
    public override bool                ActionsOpen => false;

    // Initial setup: spawn each card into its target pile per the puzzle's
    // deal. CreateCardActions go through the normal pipeline; framework's
    // CreateCardHandler routes them through the spawn + zone-add path.
    public override IReadOnlyList<GameAction> OnEnter(IStateScope scope) {
        // Setup ran on the first OnEnter; subsequent re-entries (none in
        // Pile Counter, but pattern-clean) return empty.
        var alreadySpawned = scope.Get<CardMapState>().Refs.Count > 0;
        if (alreadySpawned) return Array.Empty<GameAction>();

        var def    = scope.Get<MatchSetupState>().Definition as PileCounterGameDefinition;
        var puzzle = def!.Puzzle;
        var piles  = scope.Get<PileRefsState>().Piles;
        var deck   = puzzle.Deck;

        var setup = new List<GameAction>(deck.Count);
        for (int p = 0; p < puzzle.Deal.Count; p++)
            foreach (var deckIdx in puzzle.Deal[p])
                setup.Add(new CreateCardAction(deck[deckIdx], piles[p], InsertPosition.AtTop));
        return setup;
    }

    // IsCompleted: did this state's win condition fire? Returns true when
    // all pile counters equal their targets. Engine's default Evaluate then
    // calls Flow.NextAfter, which returns EndOfMatchState(Win(Player)).
    public override bool IsCompleted(GameAction action, CommandResult result, IStateScope scope) {
        var piles = scope.Get<PileRefsState>().Piles;
        foreach (var pileRef in piles) {
            var pile = scope.Get<PileState>(pileRef);
            if (pile.Counter != pile.Target) return false;
        }
        return true;
    }
}
```

The framework-shipped `EndOfMatchState(MatchOutcome.Win(player))` is what `Flow.NextAfter(PilePlayingState)` returns. Engine recognizes it after the transition and auto-dispatches `EndMatchCommand`. Game authors don't write that wiring.

### 9.3 Commands, payloads, results

```csharp
// Player intent — game-defined; lives in presentation. Carries source and
// destination refs picked by the player's drag-and-drop gesture.
public sealed record MoveCardIntent(Ref<Card> Card, Ref<Zone> Destination) : PlayerIntent;

// GameCommand. Data-only fields per the hard rule. Refs at the boundary
// because envelopes round-trip through replay / save / network (ADR-0005).
public sealed record MoveCardAction(
    Ref<Card> Card,
    Ref<Zone> From,
    Ref<Zone> To) : GameAction;

public sealed record MoveCardResult(Ref<Card> MovedCard) : CommandResult;

// Single semantic payload. N mutators register against it — each writes one
// slice (per spike Q5 lock: single payload + fan-out, not ExecuteBatch of
// per-slice payloads). The Card-Number lookup is done once in the handler
// and baked into the payload as data.
public sealed record MoveCardPayload(
    Ref<Card> Card,
    Ref<Zone> From,
    Ref<Zone> To,
    int       Number);
```

No `MoveTopCardCommand` framework primitive; "top only" is a Pile Counter constraint enforced by the validator.

The actual zone-content rewrite uses the framework's `MoveCardComposite : CompositeMutation` (post-ADR-0013 rename of the former `CompositeAction`). The composite is reached via `scope.Execute(...)` from inside the handler — distinct from `scope.Dispatch(...)`.

### 9.4 Validator + handler

Per [ADR-0010](decisions/0010-validator-handler-split.md), validator and handler are separate interfaces. Validator owns legality; handler owns mutation dispatch.

```csharp
// Legality. Property-injected Store comes from the CommandValidator<> base
// per ADR-0010 — no IHandlerContext, no ctx.Store.
public sealed class MoveCardValidator : CommandValidator<MoveCardAction> {
    public override bool Validate(MoveCardAction move, out string? reason) {
        if (move.From.Equals(move.To)) {
            reason = "source and destination must differ"; return false;
        }
        var sourceContents = Store.Get<ZoneContentsState>(move.From).Cards;
        if (sourceContents.Count == 0) {
            reason = "source pile is empty"; return false;
        }
        // Top of the pile = last in the list.
        var topRef = Store.Get<CardMapState>().RefOf(move.Card);
        if (!sourceContents[^1].Equals(topRef)) {
            reason = "only the top card of a pile can be moved"; return false;
        }
        if (Store.Get<LastMoveState>().Move?.Card.Equals(move.Card) == true) {
            reason = "can't move the same card twice in a row"; return false;
        }
        reason = null;
        return true;
    }
}

// Handler. Property-injected Store; constructor injection for additional
// services (none here). Single semantic payload; fan-out mutators handle
// the per-slice writes. Zone-content rewrite via CompositeMutation.
public sealed class MoveCardHandler : GameCommandHandler<MoveCardAction, MoveCardResult> {
    public override Task<MoveCardResult> Handle(MoveCardAction move) {
        var card   = Store.Catalog.Resolve(move.Card);
        var number = card.Storage.GetVariable<int>(Vars.Number);

        // 1. Inline mutation cluster for the zone move (framework-shipped).
        //    Removes from source.ZoneContents, rotates CardRef, inserts at
        //    destination.ZoneContents — all in this scope, no envelope of
        //    its own.
        Scope.Execute(MoveCardComposite.AtTop(move.Card, move.From, move.To));

        // 2. Single semantic payload — fan-out mutators update the cached
        //    pile counters (×2: source and dest) and the last-move memory.
        Store.Execute(new MoveCardPayload(move.Card, move.From, move.To, number));

        return Task.FromResult(new MoveCardResult(move.Card));
    }
}
```

The handler keeps two operations distinct:

- `Scope.Execute(CompositeMutation)` — inline expansion in the handler's scope (framework's `MoveCardComposite` rewrites `ZoneContentsState` and `CardMapState`).
- `Store.Execute(MoveCardPayload)` — payload-keyed dispatch; multiple mutators register against this single payload and each writes its own slice. No `ExecuteBatch` of per-slice payloads.

### 9.5 State mutators

Three mutators register against `MoveCardPayload`. Each writes one slice. Per spike Q5 lock: `Store.Execute(payload)` fans out — all three run; subscribers fire once per affected slice with the post-mutation state.

```csharp
// Per-pile counter — source delta. Keyed by Ref<Zone>; payload carries the
// source ref as the IPayloadReference target.
public sealed class AdjustSourceCounterMutator : Mutator<PileState, MoveCardPayload> {
    public override PileState Change(PileState state, MoveCardPayload p, IStateScope scope)
        => p.From == /* match this slice's key */
            ? state with { Counter = state.Counter - p.Number }
            : state;
}

// Per-pile counter — destination delta.
public sealed class AdjustDestCounterMutator : Mutator<PileState, MoveCardPayload> {
    public override PileState Change(PileState state, MoveCardPayload p, IStateScope scope)
        => p.To == /* match this slice's key */
            ? state with { Counter = state.Counter + p.Number }
            : state;
}

// Singleton — overwrites the last-move record.
public sealed class LastMoveMutator : Mutator<LastMoveState, MoveCardPayload> {
    public override LastMoveState Change(LastMoveState state, MoveCardPayload p, IStateScope scope)
        => new LastMoveState(new LastMove(p.Card, p.From, p.To));
}
```

The per-pile mutators each filter on whether the payload's `From` / `To` matches their slice's key (Scaffold's payload-keyed dispatch will run both against each `PileState` instance; the no-op branch returns `state` unchanged). Alternative shapes: split `MoveCardPayload` into `RemoveFromPilePayload(Ref<Zone>, int)` + `AddToPilePayload(Ref<Zone>, int)` (two payloads, each with its own keyed mutator) — clean if the per-pile filter feels awkward. Pile Counter is fine either way; pick during implementation.

### 9.6 The runtime

```csharp
// Authored data — Pile Counter has no per-mode knobs yet, so the definition
// is empty. A future "Hard Mode" variant would add [SerializeField] fields
// here (move limit, scoring multiplier, …) and ship as a separate asset.
[Serializable]
public sealed class PileCounterRulesDefinition : GameRulesDefinition {
    public static readonly PileCounterRulesDefinition Default = new();
}

// Runtime brain — generic on its definition. Single contract method
// (Evaluate); no FilterTriggers (retired per ADR-0013); no StateBasedActions
// (retired per ADR-0009 refresh).
public sealed class PileCounterRulesRuntime : GameRulesRuntime<PileCounterRulesDefinition> {
    private readonly IActor _player;
    protected override ITurnFlow Flow { get; }

    public PileCounterRulesRuntime(
        PileCounterRulesDefinition definition,
        IStateScope                state,
        IReadOnlyList<IActor>      actors,
        PileCounterTurnFlow        flow)
        : base(definition, state)
    {
        _player = actors.Single();
        Flow    = flow;
    }

    // Default Evaluate body suffices:
    //   - Pause on open requests / pending effects (Pile Counter has neither,
    //     so this is always no-op).
    //   - current.IsCompleted? → Flow.NextAfter(current) : current
    //
    // PilePlayingState.IsCompleted returns true when all piles match targets;
    // Flow returns EndOfMatchState(Win(Player)); engine auto-dispatches
    // EndMatchCommand. Override unnecessary.
}

// Turn flow — straightforward successor table. PilePlayingState → EndOfMatch
// (Win) on completion. EndOfMatchState is terminal so NextAfter isn't called
// on it.
public sealed class PileCounterTurnFlow : ITurnFlow {
    public MatchState NextAfter(MatchState current) => current switch {
        PilePlayingState p => new EndOfMatchState(new MatchOutcome.Win(p.Player)),
        _ => throw new InvalidOperationException($"Unhandled state: {current}")
    };
}
```

The runtime is intentionally thin. Pile Counter's game logic lives entirely in:
- The validator (legality)
- The handler (mutation dispatch)
- The state's `IsCompleted` predicate (win check)
- The turn flow (`PilePlayingState → EndOfMatchState`)

There is no overridden `Evaluate` — the default composition handles everything.

### 9.7 Presentation

A single per-state ViewModel handles Pile Counter's UI. Per [ADR-0012](decisions/0012-presentation-per-state-view-models.md) + spike Q1 lock, the VM uses `scope.Subscribe<T>(handler) → IDisposable`:

```csharp
public sealed record MoveAffordance(
    Ref<Card> Card,
    Ref<Zone> From,
    IReadOnlyList<Ref<Zone>> Destinations);

public sealed record MoveCardIntent(Ref<Card> Card, Ref<Zone> Destination) : PlayerIntent;

public sealed class PilePlayingViewModel : MatchStateViewModel<PilePlayingState> {
    private readonly CompositeDisposable _subs = new();   // host-MVVM-toolkit-dependent
    private readonly ObservableProperty<IReadOnlyList<MoveAffordance>> _moves = new();
    private IStateScope? _scope;

    public IObservable<IReadOnlyList<MoveAffordance>> Moves => _moves;

    public override void Bind(PilePlayingState state, IStateScope scope) {
        _scope = scope;
        _subs.Add(scope.Subscribe<ZoneContentsState>(_ => Recompute()));
        _subs.Add(scope.Subscribe<LastMoveState>   (_ => Recompute()));
        Recompute();
    }

    private void Recompute() {
        var scope    = _scope!;
        var piles    = scope.Get<PileRefsState>().Piles;
        var lastMove = scope.Get<LastMoveState>().Move;
        var moves    = new List<MoveAffordance>();

        foreach (var sourceRef in piles) {
            var contents = scope.Get<ZoneContentsState>(sourceRef).Cards;
            if (contents.Count == 0) continue;

            var topCardRef = contents[^1];
            var topInstance = scope.Get<CardMapState>().InstanceOf(topCardRef);
            if (lastMove?.Card.Equals(topInstance) == true) continue;    // twice-in-a-row suppression

            var destinations = piles.Where(p => !p.Equals(sourceRef)).ToList();
            moves.Add(new MoveAffordance(topInstance, sourceRef, destinations));
        }
        _moves.Value = moves;
    }

    public override GameCommand Map(PlayerIntent intent) => intent switch {
        MoveCardIntent m => new MoveCardAction(
            Card: m.Card,
            From: LocateCard(_scope!, m.Card),
            To:   m.Destination),
        _ => throw new InvalidOperationException($"Unhandled intent: {intent.GetType().Name}")
    };

    private static Ref<Zone> LocateCard(IStateScope scope, Ref<Card> card) {
        var cardRef = scope.Get<CardMapState>().RefOf(card);
        foreach (var pileRef in scope.Get<PileRefsState>().Piles)
            if (scope.Get<ZoneContentsState>(pileRef).Cards.Contains(cardRef))
                return pileRef;
        throw new InvalidOperationException($"card {card} not in any pile");
    }

    // Host MVVM toolkit decides VM lifetime; framework doesn't call Dispose.
}
```

The pre-cascade `IIntentMapper` / `ActionTemplate` / `AvailableActionsSnapshot` machinery retires entirely — the VM owns both the affordance projection (`Moves` observable) and the intent-to-command translation (`Map`). When the win condition fires and the FSM transitions to `EndOfMatchState`, the presenter rebinds to an `EndOfMatchViewModel : MatchStateViewModel<EndOfMatchState>` (framework-shipped or game-shipped; renders the win banner + "new puzzle" button).

### 9.8 Bootstrap

```csharp
// Authored card definition — variant-free; one CardDefinition shape covers
// the whole deck. Per-card variation (the Number) lives in the variable bag.
public sealed class PileCounterCardDefinition : CardDefinition {
    public int Number { get; init; }
    public override IEnumerable<Variable> DefinedVariables => new[] { Vars.Number };
    public override bool TryGetDefaultValue(Variable key, out VariableValue value) {
        if (key == Vars.Number) { value = new IntVariableValue(Number); return true; }
        value = default!; return false;
    }
}

// Authored zone definition — one PileDefinition per pile, parameterized by
// its target. AllPublic visibility (battlefield-style); top-insertion is
// the default for stack-shaped piles.
public sealed class PileDefinition : ZoneDefinition {
    public int Target { get; init; }
    public PileDefinition() {
        DefaultPosition = new InsertPosition.AtTop();
        Visibility      = new ZoneVisibilityPolicy.AllPublic();
    }
    public override IEnumerable<BaseState> GetInitialSlices() {
        yield return new PileState(Counter: 0, Target: Target);
    }
}

// Puzzle data — produced by the puzzle factory; data-only.
public sealed record Puzzle(
    IReadOnlyList<PileCounterCardDefinition> Deck,
    IReadOnlyList<int>                       Targets,    // 4 entries
    IReadOnlyList<IReadOnlyList<int>>        Deal,       // 4 lists of 3 deck-indexes
    int                                      Seed);

public interface IPuzzleFactory { Puzzle Create(); }

// The GameDefinition bundle.
public sealed class PileCounterGameDefinition : GameDefinition {
    public Puzzle Puzzle { get; }
    public PileCounterGameDefinition(Puzzle puzzle) => Puzzle = puzzle;

    public override GameRulesDefinition           Rules                   => PileCounterRulesDefinition.Default;
    public override IReadOnlyList<CardDefinition> CardCatalog             => Puzzle.Deck;
    public override IEnumerable<ZoneDefinition>   SharedZones             =>
        Puzzle.Targets.Select(target => new PileDefinition { Target = target });
    public override PlayerDefinition              DefaultPlayerDefinition => SoloPlayerDefinition.Default;

    public override IEnumerable<BaseState> GetInitialSlices() {
        yield return new LastMoveState(Move: null);
        yield return new PileRefsState(Piles: Array.Empty<Ref<Zone>>());  // factory walk populates
        // PileState slices come from each PileDefinition.GetInitialSlices() at spawn.
        // MatchStateSlice seeded by StartMatchHandler → TransitionStateCommand →
        // initial PilePlayingState (returned by Flow's bootstrap or by an
        // ITurnFlow.Initial pattern; see engine/08).
    }
}

// Solo-player baseline — empty PerPlayerZones (Pile Counter has no per-player
// state beyond the actor identity).
public sealed class SoloPlayerDefinition : PlayerDefinition {
    public static readonly SoloPlayerDefinition Default = new();
    public override IEnumerable<ZoneDefinition> PerPlayerZones => Array.Empty<ZoneDefinition>();
}

public sealed class SoloPlayer : Player {
    public SoloPlayer(SoloPlayerDefinition def, IEntityVariableStorage storage) : base(def, storage) { }
}

public static class PileCounterBootstrap {
    // IGameFactory is DI-resolved; caller never sees VContainer.
    public static GameEngine Create(IPuzzleFactory puzzleFactory, IGameFactory gameFactory) {
        var puzzle = puzzleFactory.Create();

        var engine = gameFactory.NewMatch()
            .WithGame  (new PileCounterGameDefinition(puzzle))
            .WithActors(new SoloActor(ActorId.Player1))
            .WithSeed  (puzzle.Seed)
            .Build();

        // After Build, engine.Session = NotStartedSession. StartMatch() flips
        // it to InMatchSession and triggers the initial MatchState's OnEnter
        // (which dispatches the CreateCardAction setup per puzzle.Deal).
        engine.StartMatch(out _);
        return engine;
    }
}

// Game-side DI installer.
public sealed class PileCounterInstaller : IInstaller {
    public void Install(IContainerBuilder builder) {
        // Validators + handlers (per ADR-0010).
        builder.Register<MoveCardValidator>        (Lifetime.Singleton).AsImplementedInterfaces();
        builder.Register<MoveCardHandler>          (Lifetime.Singleton).AsImplementedInterfaces();

        // Game-authored mutators (single payload, fan-out registration).
        builder.Register<AdjustSourceCounterMutator>(Lifetime.Singleton).AsImplementedInterfaces();
        builder.Register<AdjustDestCounterMutator>  (Lifetime.Singleton).AsImplementedInterfaces();
        builder.Register<LastMoveMutator>           (Lifetime.Singleton).AsImplementedInterfaces();

        // Runtime + turn flow.
        builder.Register<PileCounterTurnFlow>      (Lifetime.Transient);
        builder.Register<GameRulesRuntime<PileCounterRulesDefinition>, PileCounterRulesRuntime>(Lifetime.Transient);

        // Per-state view model registration (per ADR-0012 ViewModelRegistry).
        builder.Register<PilePlayingViewModel>     (Lifetime.Transient).AsImplementedInterfaces();
        // EndOfMatchViewModel registered separately (framework-shipped or game-shipped).
    }
}
```

Match construction is the two-layer split locked in [`engine/05-game-factory.md`](features/engine/models/05-game-factory.md):

- **App-level installer** — the framework's `EngineInstaller` registers `IStateScope`, `ICommandPipeline`, `Store`, `IGameFactory`, framework validators (`SessionGate`, `MatchStateActionGate`), framework mutators (entity-bridge, `CardMapState`, `OwnershipState`, `CardVisibilityState`, `ZoneContentsState`, `MatchStateSlice`, `RequestsSlice`, `AnswersSlice`, `PendingEffectsSlice`, `HistoryState`, `EngineState`), framework handlers (`StartMatchHandler`, `EndMatchHandler`, `TransitionStateHandler`), `ITriggerPipeline`, `ITriggerSystem`, `IEffectPipeline`. Per-game `PileCounterInstaller` (above) appends. Each runs once per app.
- **Per-match factory** — the bootstrap asks `IGameFactory` for a new game, supplying only per-match data: the `GameDefinition` bundle, actor catalog, RNG seed.

`MoveCardComposite` (framework-shipped `CompositeMutation`) handles the zone-move expansion atomically; the game's three mutators update cached counters and last-move memory in the same `Store.Execute(MoveCardPayload)` fan-out. After `Build` returns, `engine.Session = NotStartedSession`; `engine.StartMatch()` flips it to `InMatchSession` and the initial `PilePlayingState`'s `OnEnter(scope)` dispatches the setup actions per `puzzle.Deal`.

---

## 10. Framework Decisions Surfaced From This Sketch

These are decisions the Pile Counter sketch forces or confirms. Each affects the framework, not just this game.

- **Generic primitive + rule-enforced constraint.** Framework ships generic `MoveCardAction`; "top only" is a Pile Counter constraint in `MoveCardValidator.Validate`. No `MoveTopCardAction` primitive.
- **Validator/handler split.** Per [ADR-0010](decisions/0010-validator-handler-split.md). `ICommandValidator<MoveCardAction>` owns legality; `GameCommandHandler<MoveCardAction, MoveCardResult>` owns mutation dispatch. Property-injected `Store` on both bases.
- **Single semantic payload + fan-out mutators** (per spike Q5 lock). One `MoveCardPayload` carries all the data; three mutators register against it and each writes one slice. `Store.Execute(payload)` fans out atomically. No `ExecuteBatch` of per-slice payloads.
- **`CompositeMutation` for inline mutation clusters.** Framework-shipped `MoveCardComposite` rewrites `ZoneContentsState` and `CardMapState` via `scope.Execute(...)` — distinct from `scope.Dispatch(...)` (which routes through the engine drain). Per ADR-0013, `CompositeMutation` is **not** a `GameCommand` sibling.
- **Match state is a first-class FSM concept.** Per [ADR-0009](decisions/0009-engine-as-fsm.md), `PilePlayingState : MatchState` carries the action-gate triad + lifecycle hooks; `runtime.Evaluate(action, result) → MatchState` is the FSM evaluator. Engine auto-dispatches `EndMatchCommand` on `EndOfMatchState`.
- **Per-state ViewModel + `scope.Subscribe<T>`.** Per [ADR-0012](decisions/0012-presentation-per-state-view-models.md) + spike Q1 lock. The VM owns affordance projection (`Moves` observable) and intent-to-command translation (`Map`). No global `IIntentMapper`, no `ActionTemplate` wire form. Per-VM `CompositeDisposable` for subscription cleanup; framework doesn't call `Dispose` on the VM.
- **Engine-orchestrated pipeline.** Per [ADR-0013](decisions/0013-engine-pipeline-orchestration.md). Engine pattern-matches command type and routes to validator chain / trigger fold / handler. Pile Counter has no triggers; the fold is a no-op pass-through. `runtime.FilterTriggers` retired; `StateBasedActions` retired (Magic-style; not a framework concept).
- **Validator chain layering.** `SessionGate` + `MatchStateActionGate` (framework defaults) → game-shipped `MoveCardValidator` (per ADR-0010). `MatchStateActionGate` reads `PilePlayingState.Actions = [typeof(MoveCardAction)]` + `ActionsOpen = false` — rejects anything else.
- **`GameDefinition` bundle composition.** `PileCounterGameDefinition` consolidates Rules + CardCatalog + SharedZones + DefaultPlayerDefinition + game-singleton slices (`LastMoveState`, `PileRefsState`). Per-puzzle variation flows through the constructor. Per [`engine/06`](features/engine/models/06-game-definition.md).
- **Match setup via `OnEnter`.** The initial `MatchState`'s `OnEnter(scope)` dispatches the `CreateCardAction` list per `puzzle.Deal`. No `StartMatchHandler`-side setup logic. Per [`engine/07`](features/engine/models/07-match-state.md) banner.
- **Data-only fields hard rule.** No `Func<>` / `Predicate<>` / delegates on any `GameCommand` / `CompositeMutation` / payload / slice. The "twice in a row" rule is computed from `LastMoveState` data, not captured in a closure. Required for replay round-trip.
- **Match-end is engine-self-emitted.** No host orchestrator. `Evaluate` returns `EndOfMatchState`; engine auto-dispatches `EndMatchCommand`; `engine.Session = EndedSession(outcome)`; `SessionGate` rejects everything from then on. Per [`engine/01`](features/engine/models/01-game-engine.md) banner.
- **Rematch is dispose-and-recreate.** Per ADR-0009 refresh. No `RestartMatchCommand`. `engine.Dispose()` + `gameFactory.NewMatch()...Build()` for a fresh instance.

---

## 11. Open Items From This Sketch

Most pre-cascade Qs were resolved during the spike Q-walk (2026-05-11). Remaining items, post-cascade:

### Implementation-time picks (non-blocking)

- **Q1 — `MoveCardPayload` shape.** Two options surfaced in §9.5: single payload + fan-out with per-key filters in each mutator, OR split into `RemoveFromPilePayload` + `AddToPilePayload` (two payloads, each with a single keyed mutator). Both work. Pick during implementation; trivially refactorable later.
- **Q2 — `EndOfMatchViewModel` shipping.** Framework-shipped (terminal VM rendering outcome + rematch surface) vs game-shipped (per-game terminal UI). Lean: framework ships a baseline; games override for richer end-of-match presentation. Pin when presentation work begins.

### Out of scope for Pile Counter (will surface in richer games)

- **Async decision affordances.** Pile Counter has no `Request<TResponse>` traffic. Magic-style targeting, choose-one prompts, mulligan flows surface this when sample games grow.
- **Multiplayer / hidden info snapshots.** Pile Counter is single-player. Different-player affordances + visibility filtering surface with multiplayer samples.
- **Trigger ordering within fold.** Pile Counter has no triggers. Magic-style controller-choice replacement order surfaces in TCG-style samples.
- **Per-effect undo / unwind.** Pile Counter has no undo. Effect-author unwind patterns (compensating actions) surface in games where mid-effect cancellation matters.

---

## 12. How This Doc Is Used

When designing or revisiting a framework piece, sketch its Pile Counter implementation alongside the framework type. If the sketch is awkward, the framework is wrong. If the sketch is trivial, the framework is at least adequate for the floor.

Whenever the framework shape changes, update §9 in this doc *first*, then propagate to the affected feature Models. The sketch is the source of truth for what "actual game code" looks like; abstract framework docs follow from it.

If a question opens during sketching, add it to §11. Resolve in place when answered. This keeps the open list local to where the sketch lives.
