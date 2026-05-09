---
type: sample
status: locked
references: [features/engine/models/03-game-rules-runtime.md, features/engine/models/05-game-factory.md, features/engine/models/01-game-engine.md, features/engine/models/06-game-definition.md, features/actions/models/01-command-pipeline.md, features/effects/models/02-effect-scope.md, features/cards/models/01-card-model.md, features/zones/models/01-zone-model.md, features/players/models/01-player-model.md, features/entities/models/02-instance.md]
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

After a move resolves, if `pile.Counter == pile.Target` for **all four piles** simultaneously, the game ends and the player wins. The framework-owned `EndMatchCommand(MatchOutcome.Win(player))` is dispatched by the runtime's `Evaluate`; the framework `EndMatchHandler` emits `MatchEndedSignal` and flips `EngineState.Lifecycle` to `Ended`. See [`engine/01-game-engine.md`](features/engine/models/01-game-engine.md).

There is no lose condition. The puzzle is always solvable from setup; the player keeps moving until they hit it.

---

## 7. What Pile Counter Tests In The Framework

This game forces the framework to express:

- **Zones with stack ordering and a derived score.** The score is a per-zone value cached in a `PileState` slice keyed at the pile's `Ref<Zone>`. Maintaining the cache atomically with each move tests the per-zone-type slice + mutator pattern from [`zones/01-zone-model.md`](features/zones/models/01-zone-model.md).
- **A single action type with legality rules.** Tests `handler.Validate` rejection (per-command legality), `AvailableActions` (the gateway needs to tell the UI which moves are legal), and how legality interacts with the "can't move same card twice in a row" memory.
- **State-based win check.** Classic `Evaluate` job: after each action, read state, decide if the game is over, dispatch the framework-owned `EndMatchCommand(outcome)`. The framework handler emits `MatchEndedSignal` and flips `EngineState.Lifecycle` to `Ended` — see [`engine/01-game-engine.md`](features/engine/models/01-game-engine.md).
- **Move history of size 1.** "Last card moved" is a sliver of state that doesn't fit on a card or a zone — it's *game-level* memory. It lives in a singleton `LastMoveState` slice declared by the `GameDefinition`'s `GetInitialSlices()`.
- **`GameDefinition` bundle composition.** The puzzle's deck, pile targets, and initial deal flow through a `PileCounterGameDefinition` bundle. Tests the bundle shape from [`engine/06-game-definition.md`](features/engine/models/06-game-definition.md).
- **Match setup commands.** After `engine.StartMatch()`, the runtime dispatches `CreateCardMutation` calls to spawn each card into its target pile. Tests the setup-command pattern from [`engine.md §Match setup`](features/engine/engine.md).

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

This is the working sketch of Pile Counter against the framework as of this writing. When the framework changes, update this sketch first and propagate to the affected feature Models.

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

- **`ZoneContentsState`** — keyed at each pile's `Ref<Zone>`. Holds `ImmutableList<CardRef>`. Source of truth for "which cards are in which pile, in what order."
- **`CardMapState`** — singleton. Maps `CardRef ↔ Ref<Card>` for resolution.
- **`EntityState`** (per card) — keyed at each card's `Ref<Card>`. Holds the `Number` variable. Read via `card.Storage.GetVariable<int>(Vars.Number)`.
- **`AvailableActionsState`** — singleton. Written by the runtime via `IAvailableActions.Set(...)`.
- **`EngineState`**, **`HistoryState`** — framework-owned; auto-seeded.

Slices are immutable; mutators return new records. The runtime holds no game-affecting state.

### 9.2 Commands, intents, results, payloads

```csharp
// Player intent (UI emits this on drop)
public sealed record MoveCardIntent(Ref<Card> Card, Ref<Zone> Destination) : PlayerIntent;

// Engine command (fully resolved). Refs at the boundary — per ADR-0005,
// command payloads are addressable by ref because they cross
// serialization/replay/network boundaries.
public sealed record MoveCardCommand(
    Ref<Card> Card,
    Ref<Zone> From,
    Ref<Zone> To) : GameAction;

// EndMatchCommand is framework-owned (engine/01 §Transitions are commands). Pile Counter does not
// declare its own end-of-match command; Evaluate dispatches the framework's.

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

Note the framework primitive is **generic** `MoveCardCommand` — not `MoveTopCardCommand`. The "top only" rule is a Pile Counter constraint enforced in `Validate`, not a framework concept.

The actual zone move (remove from source, rotate `CardRef`, insert into destination) goes through the framework's `MoveCardMutation` `CompositeAction` — see [`cards/01-card-model.md` § Mutations](features/cards/models/01-card-model.md). The handler doesn't write `ZoneContentsState` directly; it issues the composite.

### 9.3 Intent mapper (game-specific)

```csharp
public class PileCounterIntentMapper : IIntentMapper
{
    private readonly IStateScope state;
    public PileCounterIntentMapper(IStateScope state) => this.state = state;

    public GameCommand Map(ActionTemplate template, PlayerChoice choice) => template switch
    {
        MoveCardTemplate m => new MoveCardCommand(
            Card: m.Card,
            From: LocateCard(state, m.Card),
            To:   (Ref<Zone>)choice.Targets[0]),
        _ => throw new InvalidOperationException($"unknown template: {template}")
    };

    // Reverse-look the card's current pile by scanning ZoneContentsState slices.
    // Pile Counter has 4 zones — scan is trivial. Larger games can build a
    // CardLocationIndex per `bl-card-location-index` in OPEN-ITEMS.md.
    private static Ref<Zone> LocateCard(IStateScope state, Ref<Card> card)
    {
        var map = state.Get<CardMapState>();
        var cardRef = map.RefOf(card);
        // Iterate the zone catalog the game knows about — game-side index of pile refs.
        foreach (var pileRef in PileCounterGameDefinition.PileRefsAt(state))
            if (state.Get<ZoneContentsState>(pileRef).Cards.Contains(cardRef))
                return pileRef;
        throw new InvalidOperationException($"card {card} not in any pile");
    }
}
```

The mapper switches on the typed template (no string-keyed slot lookup) and resolves the implicit `From` by scanning zone contents. The `IStateScope` is injected for state-driven mapping; the mapper is decision-unaware (per ADR-0006). See [`engine/04 §3`](features/engine/models/04-available-actions.md).

### 9.4 Handler (validate + handle)

Handlers own command-specific legality and dispatch the mutations / payloads that drive state change. `Validate` reads slices through `IStateScope`; `Handle` returns `Task<TResult>`. See [`actions/03-command-handler.md`](features/effects/models/02-effect-scope.md) for the handler signature, §4.4 for `IHandlerContext`.

```csharp
public class MoveCardHandler : CommandHandler<MoveCardCommand, MoveCardResult>
{
    public bool Validate(MoveCardCommand move, IStateScope state, out string? reason)
    {
        if (move.From.Equals(move.To))
        { reason = "source and destination must differ"; return false; }

        var sourceContents = state.Get<ZoneContentsState>(move.From).Cards;
        if (sourceContents.Count == 0)
        { reason = "source pile is empty"; return false; }

        // Top card check — last in the list is the top.
        var topCardRef = state.Get<CardMapState>().RefOf(move.Card);
        if (!sourceContents[^1].Equals(topCardRef))
        { reason = "only the top card of a pile can be moved"; return false; }

        if (state.Get<LastMoveState>().Move?.Card.Equals(move.Card) == true)
        { reason = "can't move the same card twice in a row"; return false; }

        reason = null; return true;
    }

    public Task<MoveCardResult> Handle(MoveCardCommand move, IHandlerContext ctx)
    {
        var card   = ctx.Store.Catalog.Resolve(move.Card);
        var source = ctx.Store.Catalog.Resolve(move.From);
        var dest   = ctx.Store.Catalog.Resolve(move.To);

        var number = card.Storage.GetVariable<int>(Vars.Number);

        // Atomic batch: the framework's MoveCardMutation handles the actual zone
        // move (ZoneContentsState updates + CardRef rotation); the game-side
        // payloads update the cached pile counters and the last-move memory.
        ctx.Store.ExecuteBatch(new object[]
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

The handler issues one `ExecuteBatch` call. Scaffold's payload-keyed registry runs every mutator registered for each payload in the batch and commits them atomically. Slice subscribers fire once per affected slice with the post-batch state. See [`effects/03-handler-context.md`](features/effects/models/02-effect-scope.md) for `IHandlerContext`'s shape.

### 9.5 State mutators

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

### 9.6 The rule

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
// (Pile Counter has no stack, no decks, no priority). Holds a reference
// to the solo player resolved from the seated-actor list and the per-pile
// `Ref<Zone>` list resolved from the GameDefinition at construction.
//
// Per ADR-0006, the contract has two methods: FilterTriggers and Evaluate.
// Pile Counter omits FilterTriggers (uses base default) and uses Evaluate
// for both state-based follow-ups (win check) and snapshot publication.
public sealed class PileCounterRulesRuntime : GameRulesRuntime<PileCounterRulesDefinition>
{
    private readonly IActor                   _player;
    private readonly IReadOnlyList<Ref<Zone>> _pileRefs;

    public PileCounterRulesRuntime(
        PileCounterRulesDefinition definition,
        IStateScope                state,
        IAvailableActions          actions,
        IReadOnlyList<IActor>      actors)
        : base(definition, state, actions)
    {
        _player   = actors.Single();
        _pileRefs = PileCounterGameDefinition.PileRefsAt(state);
    }

    // FilterTriggers omitted — base class ships a virtual identity default.

    public override IReadOnlyList<GameAction> Evaluate(GameAction _, CommandResult __)
    {
        var pileStates = _pileRefs.Select(r => State.Get<PileState>(r));

        // Win check: all piles match their target?
        if (pileStates.All(p => p.Counter == p.Target))
            return new[] { new EndMatchCommand(new MatchOutcome.Win(_player)) };

        // No state-based follow-ups. Publish snapshot at this moment.
        Actions.Set(BuildSnapshot());
        return Array.Empty<GameAction>();
    }

    private AvailableActionsSnapshot BuildSnapshot()
    {
        var lastMove  = State.Get<LastMoveState>().Move;
        var templates = new List<ActionTemplate>();

        foreach (var sourceRef in _pileRefs)
        {
            var contents = State.Get<ZoneContentsState>(sourceRef).Cards;
            if (contents.Count == 0) continue;

            var topCardRef  = contents[^1];
            var topInstance = State.Get<CardMapState>().InstanceOf(topCardRef);
            if (lastMove?.Card.Equals(topInstance) == true) continue;   // per-template suppression

            var destinations = _pileRefs
                .Where(p => !p.Equals(sourceRef))
                .Cast<Reference>()
                .ToList();

            templates.Add(new MoveCardTemplate(topInstance, destinations));
        }

        return new AvailableActionsSnapshot(templates);
    }
}
```

The runtime constructor takes its typed `PileCounterRulesDefinition` plus Scaffold's `IStateScope` (read-only) and `IAvailableActions` (snapshot writer). `State.Get<TState>(...)` reads each slice fresh per access; `LastMoveState.Move` is nullable for the game-start case. Per [ADR-0006](decisions/0006-two-pipeline-architecture.md), the runtime no longer holds `IIntakeQueue` — the command queue is engine-private FIFO with no runtime service.

The runtime constructs typed `ActionTemplate` entries (per-template enumeration). Per-binding legality runs at dispatch via `MoveCardHandler.Validate`; the runtime does *not* pre-validate every (source × destination) combination during snapshot build. The "twice in a row" rule is per-template suppression — it skips the whole template for that card. See [`engine/04 §2–§3`](features/engine/models/04-available-actions.md).

### 9.7 Engine intake order

```csharp
public bool Dispatch(GameAction action, out string? reason)
{
    // 1. Lifecycle gate — framework-only check.
    var lifecycle = store.Get<EngineState>().Lifecycle;
    if (lifecycle == EngineLifecycle.Ended)        { reason = "MatchEnded"; return false; }
    if (lifecycle == EngineLifecycle.NotStarted
        && action is not StartMatchCommand
        && action is not EndMatchCommand)          { reason = "MatchNotStarted"; return false; }

    // 2. Command-specific legality — pipeline resolves the handler and delegates to Validate.
    if (!pipeline.Validate(action, store, out reason))
        return false;

    // 3. Enqueue onto framework-private FIFO. Drain if not already running.
    pending.Enqueue(action);
    if (!running) Drain();

    reason = null;
    return true;
}
```

The lifecycle gate runs first (cheap, framework-only), then `pipeline.Validate` (resolves handler, delegates to `handler.Validate`). Either can reject — surfaced via the try-get pattern (`bool` + `out string? reason`). Per [ADR-0006](decisions/0006-two-pipeline-architecture.md), the runtime no longer participates in intake gating; the command queue is framework-private FIFO and game-specific routing customization lives at the effect-pipeline level (separate design pass; tracked as `oq-effect-pipeline`).

### 9.8 Bootstrap

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
// + DefaultPlayerDefinition + game-singleton slices. Per [engine/07](features/engine/models/06-game-definition.md).
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
        // Framework slices (AvailableActionsState, EngineState, HistoryState) auto-seeded.
    }

    // Game-side helper for runtime + intent mapper to enumerate spawned pile refs.
    // The factory walk spawns one Zone per SharedZones entry; we read them from
    // the catalog via a dedicated index slice that PileCounterRulesRuntime maintains
    // (or via OwnershipState scan filtered to the four PileDefinition entries).
    public static IReadOnlyList<Ref<Zone>> PileRefsAt(IStateScope state)
        => /* engine-side index slice; populated during Build walk */
           throw new NotImplementedException("populated by Build walk index");
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
    // Per-game behaviors (handlers, mutators, GameRulesRuntime, IIntentMapper) are
    // registered in PileCounterInstaller below.
    public static GameEngine Create(IPuzzleFactory puzzleFactory, IGameFactory gameFactory)
    {
        var puzzle = puzzleFactory.Create();

        var engine = gameFactory.NewMatch()
            .WithGame(new PileCounterGameDefinition(puzzle))    // bundle: Rules + zones + cards + GetInitialSlices()
            .WithActors(new SoloActor(ActorId.Player1))         // entity-backed Player spawned at Build via DefaultPlayerDefinition
            .WithSeed(puzzle.Seed)
            .Build();

        engine.StartMatch();   // dispatches StartMatchCommand → MatchStartedSignal → Lifecycle = Started

        // Setup commands — runtime-driven setup is the recommended pattern (per
        // engine.md §Match setup). PileCounterRulesRuntime's first OnQueueDrained dispatches
        // CreateCardMutation calls to spawn each card into its target pile per
        // puzzle.Deal. After setup completes the runtime publishes the initial
        // AvailableActions snapshot.

        return engine;
    }
}

// Separate file — registered once at app startup.
public class PileCounterInstaller : IInstaller
{
    public void Install(IContainerBuilder builder)
    {
        builder.Register<MoveCardHandler>          (Lifetime.Singleton).AsImplementedInterfaces();
        builder.Register<PileCounterMutator>       (Lifetime.Singleton).AsImplementedInterfaces();
        builder.Register<LastMoveMutator>          (Lifetime.Singleton).AsImplementedInterfaces();
        builder.Register<GameRulesRuntime<PileCounterRulesDefinition>, PileCounterRulesRuntime>(Lifetime.Transient);
        builder.Register<PileCounterIntentMapper>  (Lifetime.Singleton).AsImplementedInterfaces();
    }
}
```

Match construction is a two-layer split (locked in [`engine/05-game-factory.md`](features/engine/models/05-game-factory.md)):

- **App-level installer** — the framework's `EngineInstaller` registers `IStateScope`, `IAvailableActions`, `ICommandPipeline`, `Store`, and `IGameFactory` plus framework mutators (entity-bridge, `CardMapState`, `OwnershipState`, `CardVisibilityState`, `ZoneContentsState`). Per [ADR-0006](decisions/0006-two-pipeline-architecture.md) there is no `IIntakeQueue` — the command queue is engine-private FIFO. The game's installer (`PileCounterInstaller` above) registers the handler, game-authored mutators, the runtime against its definition's closed generic (`GameRulesRuntime<PileCounterRulesDefinition> → PileCounterRulesRuntime`), and the intent mapper. Each runs once per app. See [`engine/05` § Registration contract](features/engine/models/05-game-factory.md).
- **Per-match factory** — the bootstrap call site asks `IGameFactory` for a new game, supplying only per-match data: the `GameDefinition` bundle, actor catalog, RNG seed. VContainer never appears in this code.

The framework's `MoveCardMutation` `CompositeAction` handles the zone-move expansion (remove + rotate `CardRef` + insert) atomically; the game-side mutators (`PileCounterMutator` × 2, `LastMoveMutator`) update cached counters and the last-move memory in the same `ExecuteBatch`. After `Build` returns the engine is in `EngineLifecycle.NotStarted`; `engine.StartMatch()` opens it, and runtime-driven setup deals the cards. See [`presentation/01`](features/presentation/models/01-presentation-loop.md) for the presentation loop.

---

## 10. Framework Decisions Surfaced From This Sketch

These are decisions the Pile Counter sketch forced or confirmed. Each affects the framework, not just this game.

- **Generic primitive + rule-enforced constraint.** Framework ships generic `MoveCardCommand`; "top only" is enforced in `Validate`. No `MoveTopCardCommand` primitive.
- **Validation belongs to the handler.** `ICommandHandler.Validate(cmd, state, out reason)`. The rule never switches on command types.
- **`Validate` signature is Try-pattern.** `bool Validate(..., out string? reason)`.
- **Rule holds zero game-affecting state.** `LastMove` lives in its own `LastMoveState` slice, written by a registered `Mutator<LastMoveState, SetLastMovePayload>`. Locked as contract.
- **State mutators are payload-keyed, not command-keyed** (Scaffold State pattern). Handlers issue `Store.Execute(payload)` or `Store.ExecuteBatch(...)`; every `Mutator<TState, TPayload>` registered for that payload type runs and commits atomically. Handlers don't enumerate ancillary state slots.
- **Framework `MoveCardMutation` (CompositeAction) handles zone moves.** Game-authored mutators handle game-side concerns (counter cache, last-move memory). The split is structural — see [`cards/01-card-model.md`](features/cards/models/01-card-model.md).
- **Player input layer is three-tier:** `PlayerIntent` (UI) → `IIntentMapper` → `GameCommand` (engine).
- **Two-pipeline architecture** ([ADR-0006](decisions/0006-two-pipeline-architecture.md)). Command pipeline is framework-FIFO with per-command primitives (`scope.Modify` / `Cancel` / `Replace` / `Dispatch`); effect pipeline (game-customizable; `oq-effect-pipeline`) is the forthcoming surface for "when does each effect start." The runtime does not own command-queue routing.
- **Snapshot lives in an `AvailableActionsState` slice carrying `AvailableActionsSnapshot`.** Typed templates. The runtime writes through `IAvailableActions.Set` from inside `Evaluate` whenever it deems appropriate (typically when its own follow-ups exhaust). See [`engine/03`](features/engine/models/04-available-actions.md).
- **`AffordanceMap` is game-specific** and lives in the UI/gateway layer. Computed from snapshot. Framework doesn't define it.
- **Three rejection paths:** AvailableActions (UI prophylactic) / Engine lifecycle gate (framework, `NotStarted`/`Ended`) / `handler.Validate` (per-command legality, authoritative) / Before-fold `Cancel` (card-effect).
- **`GameDefinition` bundle composition.** `PileCounterGameDefinition` consolidates Rules + CardCatalog + SharedZones + DefaultPlayerDefinition + game-singleton slices. Per-puzzle variation flows through the constructor. [`engine/07`](features/engine/models/06-game-definition.md).
- **Match setup runs after StartMatch via runtime-driven setup commands.** The runtime's first `Evaluate` dispatches `CreateCardMutation` calls per `puzzle.Deal`. [`engine.md §Match setup`](features/engine/engine.md).
- **Engine intake order (try-get):** lifecycle gate → `pipeline.Validate` → enqueue. `engine.Dispatch(GameAction action, out string? reason)` returns `bool`; matches `pipeline.Validate`'s shape.
- **Rules layer is split into `GameRulesDefinition` (data) + `GameRulesRuntime<TDefinition>` (runtime).** Definition is `[SerializeField]` data; runtime constructor takes the definition + Scaffold `IStateScope` + `IAvailableActions` + any game-specific subsystems. Two-method contract (`FilterTriggers` + `Evaluate`); per-match factory uses container open-generic resolution ([`engine/02-game-rules-definition.md` §The two-type split](features/engine/models/02-game-rules-definition.md), [`engine/03-game-rules-runtime.md`](features/engine/models/03-game-rules-runtime.md), [`engine/05`](features/engine/models/05-game-factory.md)).
- **Definition / Instance layered on framework parents.** `CardDefinition` / `ZoneDefinition` / `PlayerDefinition` inherit from `DefinitionWithSlices`; `Card` / `Zone` / `Player` inherit from `InstanceWithSlices<TDef>`. `Definition` is `protected` on the instance side. Per the open-seams design pass.

---

## 11. Open Questions From This Sketch

Tracked alongside the sketch so they don't get lost. Numbers are stable; resolve in place.

### Resolved (in subsequent docs)

- ~~**Q1 — State pattern: stateless vs stateful rule.**~~ Resolved by [`engine/04 §5`](features/engine/models/04-available-actions.md). Services injected at construction; methods take only ephemeral data; state read through Scaffold `IStateScope.Get<TState>(...)`.
- ~~**Q3 — `EndGameCommand` semantics.**~~ Resolved by the `oq-engine-match-lifecycle` design pass. Pile Counter dispatches the framework-owned `EndMatchCommand(MatchOutcome.Win(player))`; the framework handler emits `MatchEndedSignal` and flips `EngineState.Lifecycle` to `Ended`. Engine rejects subsequent dispatches via the lifecycle gate. See [`engine/01-game-engine.md`](features/engine/models/01-game-engine.md).
- ~~**Q8 — `IHandlerContext` shape.**~~ Resolved by [`effects/03-handler-context.md`](features/effects/models/02-effect-scope.md). Three services on the context: concrete Scaffold `Store` (read + execute mutators), `IAvailableActions` (snapshot writes), `IDecisionResolver` (open/resolve decisions). No `Mutate` surface; handler writes via `ctx.Store.Execute(payload)` or `ctx.Store.ExecuteBatch(...)`. No `CurrentDecisionId` field; the decision id ≡ the action's `CommandId`.
- ~~**Q9 — `IHandlerRegistry` shape.**~~ Resolved session-20260501-1132 Q1: registry is resolver-only (`ICommandHandler Resolve(GameCommand)`); validate/execute live on `ICommandPipeline`; game authors extend `CommandHandler<TCommand, TResult>`.
- ~~**Q10 — `IntakeContext.Pending` type.**~~ `IntakeContext`, `Pending`, and `IIntakeQueue` all deleted per [ADR-0006](decisions/0006-two-pipeline-architecture.md). Command queue is engine-private FIFO with no runtime service.
- ~~**Q13 — `FilterTriggers` default impl.**~~ `GameRulesRuntime<TDefinition>` ships a virtual identity default ([`engine/03-game-rules-runtime.md` §FilterTriggers](features/engine/models/03-game-rules-runtime.md)); games with no eligibility logic don't override it.
- ~~**Q17 — `IGameStateProvider` shape.**~~ Folded away. The framework binds directly to Scaffold `IStateScope` (read) and concrete `Store` (read + execute mutators) — see [`effects/02-effect-scope.md` and `effects/03-handler-context.md`](features/effects/models/02-effect-scope.md). No invented provider type.
- ~~**Q18 — Effect-side service symmetry.**~~ Resolved by [`effects/04`](features/effects/models/02-effect-scope.md): `IEffectScope` carries `IStateScope` + `Dispatch` + `Cancel`; `IHandlerContext` carries `Store` + `IAvailableActions` + `IDecisionResolver`.
- ~~**Q19 — `PileRefsAt` index source.**~~ Open-seams pass surfaced this implicitly: needs an engine-side index slice (`PileIndexState` or similar) populated during the factory's Build walk. Captured in `bl-pile-refs-index` (TBD).

### Deferred (need decisions, not blocked)

- **Q2 — Action pipeline cleanup.** The handler-lookup → validate → execute → trigger-fold flow is scattered across multiple Models. Needs a dedicated doc that draws the full pipeline explicitly.
- **Q4 — Snapshot revision tag.** Useful for the UI to detect "I have a stale snapshot." Add when a concrete need surfaces (e.g., async render).

### Implementation details (non-blocking)

- **Q5 — `AvailableActions` mutator implementation.** Framework-shipped — the `IAvailableActions` service writes the slot through an internal mutator ([`engine/04 §2`](features/engine/models/04-available-actions.md)). Confirm at engine implementation.
- **Q6 — `IIntentMapper` final shape.** Now `Map(ActionTemplate, PlayerChoice) → GameCommand` (typed templates, [`engine/04 §3`](features/engine/models/04-available-actions.md)). `PlayerChoice` shape pin during implementation.
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
