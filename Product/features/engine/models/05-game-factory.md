---
type: model
status: settled
references: [features/engine/models/01-evaluation-loop.md, features/engine/models/02-gamerules-contract.md, features/engine/models/04-gamestate-slices.md]
tags: [#engine, #factory, #bootstrap]
---

# Game Factory — Match Construction

## Problem

Building a `GameEngine` for a single match requires assembling several inputs: per-game behavior types (handlers, mutators, `GameRules` implementations, `IIntentMapper`), per-match data (initial slice values, actor catalog, RNG seed, rules selection), and framework services (`IStateScope`, `IAvailableActions`, `IIntakeQueue`, `ICommandPipeline`). The earlier sketch in `support/samples/pile-counter.md §9.8` mixed all of these into a single fluent builder (`new GameEngineBuilder().WithHandlers<T>().WithMutators<T1, T2>().WithRule<T>().WithIntentMapper<T>().Build(container)`), which conflated DI installation with per-match configuration and pushed the container through to the call site.

This Model defines a clean two-layer split between **DI installation** (the app-level VContainer installer registering every behavior type) and **match construction** (a per-match factory that takes only match-specific data) and locks the factory's API surface.

## Concepts

| Term | Definition |
|---|---|
| `IGameFactory` | DI-resolved service that constructs a fresh `GameEngine` for a match. Holds container-resolved framework + game services as dependencies; the caller asks the factory for a new game without ever seeing VContainer. |
| `MatchSetup` | Per-match data bundle the factory consumes. Carries the rules selection, actor catalog, initial slice values, and RNG seed. |
| `IMatchBuilder` | Fluent builder surface exposed by the factory as ergonomic sugar over `MatchSetup`. Either path produces the same `GameEngine`. |
| `GameRules` | Serializable data class carrying both behavior and configuration. `[SerializeField]`-bearing but not a `UnityObject`. Wrapped by a Unity-side asset (e.g. `GameRulesAsset : ScriptableObject`) for editor assignability. (See [`engine/02-gamerules-contract.md`](features/engine/models/02-gamerules-contract.md) for the legacy constructor-injected shape; reconciliation is open.) |
| `Player : Actor` | Framework-provided baseline subtype representing a seated participant. Concrete shape is minimal — game-specific player data lives in game-authored slices keyed by `ActorId`. |
| Per-game behavior | Handlers, mutators, `GameRules` implementations, `IIntentMapper` — same set every match; lives in the game's VContainer installer. |
| Per-match data | Initial slice values, actor catalog, rules selection, seed — varies every match; flows through the factory. |

## Contracts

### Two-layer split

The framework commits to two distinct registration boundaries:

**App-level VContainer installation (one-time, per app):**
- Framework `EngineInstaller` registers `IStateScope`, `IAvailableActions`, `IIntakeQueue`, `ICommandPipeline`, `Store`, and `IGameFactory`.
- Game installer registers every `ICommandHandler<TCommand, TResult>`, every `Mutator<TState, TPayload>`, every `GameRules` implementation, `IIntentMapper`, and any other game services.

**Per-match construction (every match):**
- Caller resolves `IGameFactory` from the container.
- Caller calls `factory.Create(matchSetup)` (or fluent equivalent) with per-match data.
- Factory returns a configured `GameEngine` ready for first `Dispatch`.

### `IGameFactory`

```csharp
public interface IGameFactory
{
    GameEngine     Create(MatchSetup setup);
    IMatchBuilder  NewMatch();
}
```

The factory itself is constructor-injected by VContainer with whatever it needs to resolve behaviors at match time (the resolver, registries, per-game catalog services). Game authors do not implement `IGameFactory` — the framework ships the implementation.

### `MatchSetup`

```csharp
public sealed record MatchSetup
{
    public required GameRules              Rules         { get; init; }
    public required IReadOnlyList<Actor>   Actors        { get; init; }
    public required IEnumerable<BaseState> InitialSlices { get; init; }
    public          int?                   Seed          { get; init; }
}
```

Fields:

- **`Rules`** — the serializable rules data object. Selection is data; the factory resolves the runtime implementation from the data.
- **`Actors`** — the actor catalog for this match. `Player` instances go in alongside other Actor kinds (`World`, `Environment`, `Bot`, `Market`).
- **`InitialSlices`** — game-authored slice values at t=0. Framework-owned slices are seeded automatically; the caller does not include them.
- **`Seed`** — optional RNG seed for deterministic randomness. Networking will eventually pin a richer `IRandom` contract; for now, the value enters the factory here.

### `IMatchBuilder`

```csharp
public interface IMatchBuilder
{
    IMatchBuilder WithRules        (GameRules rules);
    IMatchBuilder WithActors       (IEnumerable<Actor> actors);
    IMatchBuilder WithActors       (params Actor[] actors);
    IMatchBuilder WithInitialSlices(IEnumerable<BaseState> slices);
    IMatchBuilder WithInitialSlices(params BaseState[] slices);
    IMatchBuilder WithSeed         (int seed);
    GameEngine    Build            ();
}
```

`WithActors` and `WithInitialSlices` are overloaded for both literal (`params`) and computed (`IEnumerable`) call sites.

### Framework auto-seeded slices

`Build` auto-seeds every framework-owned slice declared by per-feature Models with the initial values declared there. The full list is not enumerated here — [`engine/04 §3`](features/engine/models/04-gamestate-slices.md) (and as more features land, the cards/zones/etc. Models) is the source of truth. Game authors never include framework slices in `MatchSetup.InitialSlices`; doing so throws at validation.

### Behavior resolution at `Build`

The factory:

1. Resolves the `GameRules` runtime implementation from the registered rules data object.
2. Hydrates the rules instance with framework services (`IStateScope`, `IAvailableActions`, `IIntakeQueue`).
3. Collection-resolves every `Mutator` from the container; primes the `Store`.
4. Resolves `ICommandPipeline` (which resolves handlers from the container at dispatch time).
5. Resolves `IIntentMapper` if the game registered one.
6. Constructs the `Store` with framework + game-authored slice initial values.
7. Constructs the `GameEngine` with pipeline, rules, store.

The factory does not register behaviors itself — those came in via the installer. The factory only assembles them.

### Validation

`Build` walks every check before returning, and throws a single aggregate exception if any fail (collect-all, not fail-fast):

- The registered `GameRules` data object resolves to a runtime implementation in the container.
- `Actors` is non-empty.
- No two `InitialSlices` share the same `BaseState` subtype.
- No `InitialSlices` entry is a framework-owned slice (caller-seeded framework slices are an error, not silently overridden).
- All required framework services are resolvable.
- Every game-authored slice in `InitialSlices` has at least one `Mutator` registered against it (warning, not error — orphan slice may be intentional).

The exception carries the full error list, not just the first.

### Engine state at return

After `Build` returns, the `GameEngine` is **idle and dormant** — no drain is in progress, framework slices are seeded, game-authored slices hold their initial values. The first `Dispatch` call triggers the first `Drain`. Bootstrap commands (turn start, opening hand deal, etc.) are sent by the caller via `Dispatch` after `Build`, not folded into the factory.

### Mode-specific command vocabularies

The factory does not accept a per-match handler pool. Vocabulary differences between game modes (Tutorial disabling certain commands, Sandbox enabling debug commands) are expressed in `GameRules.OnIncomingCommand` rejection and in the available-actions snapshot. Every match resolves the full installed handler catalog from the container.

## Invariants

1. **The factory is DI-resolved.** Callers never `new` a `GameFactory`; they ask the container for `IGameFactory`. VContainer never appears in the caller's match-construction code.
2. **Per-game behaviors are installer-only.** Handlers, mutators, `GameRules` implementations, `IIntentMapper` are registered exactly once per app in the installer. The factory never accepts them as match parameters.
3. **Per-match data is factory-only.** Initial slice values, actors, rules selection, and seed enter through `MatchSetup` (or `IMatchBuilder`) on every match. They are never registered globally.
4. **Framework slices are auto-seeded.** Game authors never include `AvailableActionsState`, `EngineState`, `HistoryState`, or any framework-owned slice in `MatchSetup.InitialSlices`.
5. **Validation is collect-all.** `Build` reports every detected error in a single aggregate exception; it does not throw on the first issue.
6. **Engine is dormant at return.** No commands have been dispatched; no drain has run. Bootstrap is the caller's responsibility post-`Build`.
7. **Mode-specific command vocabularies are rules-mediated.** The factory does not accept a per-match handler pool; vocabulary differences live in `GameRules` and the snapshot.

## Examples

### Pile Counter under the new shape

```csharp
public static class PileCounterBootstrap
{
    public static GameEngine Create(IPuzzleFactory puzzleFactory, IGameFactory gameFactory)
    {
        var puzzle = puzzleFactory.Create();

        return gameFactory.NewMatch()
            .WithRules(PileCounterRules.Default)               // serializable data object
            .WithActors(new SoloPlayer(ActorId.Player1))       // Player : Actor baseline
            .WithInitialSlices(BuildInitialSlices(puzzle))     // IEnumerable form
            .WithSeed(puzzle.Seed)
            .Build();
    }

    private static IEnumerable<BaseState> BuildInitialSlices(Puzzle p)
    {
        var piles = p.Deal.ToDictionary(
            kv => kv.Key,
            kv => new Pile(
                Id:     kv.Key,
                Target: p.Targets[kv.Key],
                Cards:  kv.Value.Select(id => p.Deck.First(c => c.Id == id)).ToList()));

        yield return new PilesState(piles);
        yield return new LastMoveState(Move: null);
        // Framework slices (AvailableActionsState, EngineState, HistoryState) auto-seeded.
    }
}
```

The game's installer (separate file) registers behaviors:

```csharp
public sealed class PileCounterInstaller : IInstaller
{
    public void Install(IContainerBuilder builder)
    {
        builder.Register<MoveCardHandler>          (Lifetime.Singleton).AsImplementedInterfaces();
        builder.Register<PileMoveMutator>          (Lifetime.Singleton).AsImplementedInterfaces();
        builder.Register<LastMoveMutator>          (Lifetime.Singleton).AsImplementedInterfaces();
        builder.Register<PileCounterRules>         (Lifetime.Transient);
        builder.Register<PileCounterIntentMapper>  (Lifetime.Singleton).AsImplementedInterfaces();
    }
}
```

The framework's `EngineInstaller` runs alongside, registering `IStateScope`, `IGameFactory`, etc.

### `MatchSetup` direct form (tests and scripted scenarios)

```csharp
var engine = factory.Create(new MatchSetup
{
    Rules         = PileCounterRules.Default,
    Actors        = new[] { new SoloPlayer(ActorId.Player1) },
    InitialSlices = new BaseState[]
    {
        new PilesState(testPiles),
        new LastMoveState(Move: null),
    },
    Seed = 12345,
});
```

## Open follow-ups

- **`oq-game-definition`** — `GameDefinition` bundle + definition-declared slice schema + per-definition initial-value instantiation. The current factory accepts caller-enumerated initial slices; this works but couples the caller to the rule's implicit slice expectations. The next pass moves slice schema declaration into definitions (zones, cards, players, rules) and rolls them up into a per-game-mode `GameDefinition`. References this Model and `engine/04`.
- **`GameRules` runtime/definition reconciliation** — [`engine/02-gamerules-contract.md`](features/engine/models/02-gamerules-contract.md) defines `GameRules` as an abstract class with constructor-injected services. This Model assumes `GameRules` is a serializable data object hydrated post-construction. The two are not yet reconciled. Resolve in a follow-up that may split `GameRules` into a runtime wrapper (services) + definition (data), or move services from constructor to an `Initialize` hook.
- **Empty-actors mode** — whether `Actors` may be empty (purely simulation-driven games like a cellular automaton) is undecided. Defer until a real game forces it.
- **Networking-driven `IRandom`** — `Seed` is currently an `int`. When networking lands, `IRandom` (or a richer determinism contract) replaces this slot.
- **Bootstrap commands** — whether the factory accepts a `BootstrapCommands` list (turn-start, opening-hand-deal) and dispatches them inside `Build`, or whether bootstrap is purely the caller's responsibility post-`Build`. Currently the latter.

---

## Options considered

- **A — Caller-`new`'d builder, container injected at `Build(container)`** — the original pile-counter §9.8 sketch. Rejected: leaks VContainer into every bootstrap call site; the builder is forced to know how to resolve services; conflates DI installation with match construction.
- **B — Caller-`new`'d builder, container injected at construction** — `new GameEngineBuilder(container).With...().Build()`. Rejected: same DI leak as A, just earlier in the chain. No structural improvement.
- **C — DI-resolved factory** — chosen. Caller never sees the container; framework + game services arrive via constructor injection on the factory.

### Sub-decision — handler pool scoping

- **Build-time scoped pool** — factory takes `WithCommandPool(...)`; pipeline rejects out-of-pool commands at intake. Rejected: duplicates `GameRules.OnIncomingCommand`'s authority over intake gating; introduces a parallel filter the rule and pool must agree on by convention.
- **Install-time fixed pool** — chosen. Every match resolves the full installed handler catalog; mode-specific vocabularies are expressed in `GameRules` intake rejection and the available-actions snapshot.

### Sub-decision — rules selection form

- **Type-token** (`WithRules<TutorialRules>()`) — rejected. Compiler-checked but doesn't survive serialization; replay/save/network paths require translation.
- **String/typed ID** — rejected. Smallest data form but carries no per-instance config; mode variants would proliferate as separate classes.
- **Serializable data object** — chosen. `GameRules` is itself the serializable data object (Unity `[SerializeField]` attributes, non-`UnityObject`); a Unity wrapper provides editor assignability. Carries config alongside selection so mode variants share implementation classes.

### Sub-decision — player/actor shape

- **Framework-minimal `WithActors(IEnumerable<Actor>)`** — chosen call-site form. Single entry point; players go in as `Actor` instances.
- **Framework-defined `Player` baseline** — also chosen. `Player : Actor` is a framework-provided concrete subtype. Games extend it for game-specific player data, or attach data via game-authored slices keyed by `ActorId`.
- **Definition-driven `WithPlayers(IEnumerable<PlayerDefinition>)`** — rejected. Forcing a framework `PlayerDefinition` either bloats the framework or constrains games (Stacklands-style games have actors like World/Environment that aren't seated participants).

### Sub-decision — `WithInitialSlices` shape

- **`params BaseState[]` only** — rejected. Awkward for computed lists.
- **`IEnumerable<BaseState>` only** — rejected. Awkward for literals.
- **Both overloads** — chosen. Both call sites are real and already in use across the docs.

### Sub-decision — `Build` validation

- **Fail-fast on first error** — rejected. Trains caller into fix-and-rerun loops.
- **Collect-all aggregate exception** — chosen. `Build` runs once per match; walking all checks is negligible cost compared to seeing every problem at once.
- **Warn-only for soft issues** — rejected. Adds channel/format complexity for a benefit aggregate-error already covers (errors and warnings can ride in the same aggregate).

### Sub-decision — framework auto-seed inventory location

- **Builder Model owns the canonical inventory** — rejected. Drifts as new framework slices land in cards/zones/etc. Models.
- **Per-feature Models declare; builder Model states the rule** — chosen. Single source of truth per slice; builder Model states "framework slices declared by per-feature Models are seeded at Build" without owning a list.

## Rationale

The two-layer split is the load-bearing decision. Once accepted, every other choice falls out:

- Behaviors are stable across matches, so they belong in DI (registered once, resolved on demand).
- Per-match data isn't typeable, so it must enter as values through the factory.
- The factory itself is DI-resolved because it needs services to do its job; resolving the factory through DI keeps VContainer out of the caller's match-construction code entirely.

This split also opens the future path to data-driven match construction: a serialized `MatchSetup` (plus eventual `GameDefinition`) loads from disk / network / replay file and the factory reconstructs the engine. None of this is possible if behavior types are part of the per-match input.

## Consequences

- **Pile-counter bootstrap shrinks.** The §9.8 sketch's seven `With…` calls collapse to four (rules, actors, initial slices, seed). Behaviors move to the installer.
- **`engine/02-gamerules-contract.md` needs reconciliation.** The current `GameRules` constructor signature (`(IStateScope, IAvailableActions, IIntakeQueue)`) is incompatible with serializable-data semantics. A follow-up either splits the type or moves services to an init hook.
- **Glossary updates.** `Player` shifts from "game-specific" to "framework-provided baseline." `IGameFactory`, `MatchSetup` are added.
- **No call site sees VContainer.** Every bootstrap path goes through `IGameFactory`. The `IObjectResolver` parameter that pile-counter §9.8 currently accepts disappears.
- **Test ergonomics improve.** Tests construct `MatchSetup` directly with hand-built initial slices and inject a mock `IGameFactory` if they want to bypass DI; behavior still goes through real handlers/mutators registered in a test installer.

## ADR

*n/a — feature-internal decision; no project-wide ADR opened.*
