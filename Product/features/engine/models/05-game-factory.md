---
type: model
status: settled
references: [features/engine/models/01-evaluation-loop.md, features/engine/models/02-gamerules-contract.md, features/engine/models/04-gamestate-slices.md, features/engine/models/06-match-lifecycle.md, features/engine/models/07-game-definition.md]
tags: [#engine, #factory, #bootstrap]
---

# Game Factory — model

## What it is

The match-construction layer. Building a `GameEngine` for a single match needs three separate inputs: **per-game behavior types** (handlers, mutators, `GameRulesRuntime<T>` implementations, `IIntentMapper`), **per-match data** (initial slice values, actor catalog, RNG seed, rules selection via `GameDefinition`), and **framework services** (`IStateScope`, `IAvailableActions`, `ICommandPipeline`). This Model defines a clean two-layer split between **DI installation** (the app-level VContainer installer registering every behavior type, one-time) and **match construction** (a per-match factory that takes only match-specific data). The factory is itself DI-resolved; caller code never sees `IContainerBuilder` / `IObjectResolver`.

```csharp
public interface IGameFactory
{
    GameEngine    Create(MatchSetup setup);
    IMatchBuilder NewMatch();
}
```

```
App-level installer (one-time):           Per-match factory (every match):
  EngineInstaller — framework services      caller resolves IGameFactory
  GameInstaller   — handlers, mutators,     factory.NewMatch().With...().Build()
                    GameRulesRuntime<T>,    or factory.Create(MatchSetup)
                    IIntentMapper, …                   │
                                                       ▼
                                              GameEngine in NotStarted
```

## Contracts

### Concepts

| Term | Definition |
|---|---|
| `IGameFactory` | DI-resolved service that constructs a fresh `GameEngine` for a match. Holds container-resolved framework + game services as dependencies; the caller asks the factory for a new game without ever seeing VContainer. |
| `MatchSetup` | Per-match data bundle the factory consumes. Carries the rules selection (via `GameDefinition`), actor catalog, initial slice values, and RNG seed. |
| `IMatchBuilder` | Fluent builder surface exposed by the factory as ergonomic sugar over `MatchSetup`. Either path produces the same `GameEngine`. |
| `GameRulesDefinition` | Pure `[SerializeField]`-bearing data class (non-`UnityObject`) carrying authored config for a game-mode variant. Asset-friendly; replay/save/network-safe. Wrapped by a Unity-side asset (e.g. `GameRulesAsset : ScriptableObject`) for editor assignability. See [`engine/02-gamerules-contract.md`](features/engine/models/02-gamerules-contract.md). |
| `GameRulesRuntime<TDefinition>` | Stateful runtime class holding framework services, game-specific subsystems, and a typed `Definition`. Implements the contract methods. DI-resolved per match via container open-generic resolution. See [`engine/02-gamerules-contract.md`](features/engine/models/02-gamerules-contract.md). |
| `Player` | Framework-provided baseline `IActor` shape (entity-backed via `InstanceWithSlices<PlayerDefinition>`). Game-specific player data lives in game-authored slices keyed by `ActorId`. |
| Per-game behavior | Handlers, mutators, `GameRules` implementations, `IIntentMapper` — same set every match; lives in the game's VContainer installer. |
| Per-match data | Initial slice values, actor catalog, rules selection (via `GameDefinition`), seed — varies every match; flows through the factory. |

### Two-layer split

The framework commits to two distinct registration boundaries:

**App-level VContainer installation (one-time, per app):**
- Framework `EngineInstaller` registers `IStateScope`, `IAvailableActions`, `ICommandPipeline`, `Store`, and `IGameFactory`. (Per [ADR-0006](decisions/0006-two-pipeline-architecture.md), there is no `IIntakeQueue` — the command queue is engine-private FIFO.)
- Game installer registers every `ICommandHandler<TCommand, TResult>`, every `Mutator<TState, TPayload>`, every `GameRulesRuntime<TDefinition>` implementation (closed-generic, e.g. `Register<GameRulesRuntime<MagicRulesDefinition>, MagicRulesRuntime>()`), `IIntentMapper`, any game-specific subsystems the runtime depends on (`IStackSystem`, `IDeckSystem`, …), and any other game services.

**Per-match construction (every match):**
- Caller resolves `IGameFactory` from the container.
- Caller calls `factory.Create(matchSetup)` (or fluent equivalent) with per-match data.
- Factory returns a configured `GameEngine` ready for first `Dispatch`.

### Registration contract

The full inventory of what each side registers, organized by bucket. This is the load-bearing reference game authors use when writing their installer.

#### Framework-auto-registered (no game action required)

The framework `EngineInstaller` registers every framework service the engine needs at runtime:

| Service / type | Lifetime | Purpose |
|---|---|---|
| `Store` | Singleton (per match scope) | Canonical state container; owns slices and the catalog |
| `IStateScope` | Resolves through `Store` | Read-only state surface |
| `IAvailableActions` | Singleton | Snapshot service consumed by `GameRulesRuntime` |
| `IIntakeQueue` | Singleton | Pending command queue (shape per `oq-intake-queue`) |
| `ICommandPipeline` | Singleton | Dispatch + before/after fold orchestration |
| `IGameFactory` | Singleton | The factory itself |
| Framework mutators for `EntityState`, `CardMapState`, `OwnershipState`, `CardVisibilityState`, `ZoneContentsState`, `EngineState`, `HistoryState`, `AvailableActionsState`, `LastMoveState` | Singleton, `AsImplementedInterfaces` | Bridge primitives + framework-owned slices |
| `IModifierFactory` | Singleton | Deterministic modifier-id allocator |

Game installers **do not** register any of these. Doing so produces a `Build` validation error (duplicate registration).

#### Game-required (must be registered for `Build` to succeed)

| Registration | Form | Purpose |
|---|---|---|
| `GameRulesRuntime<TDefinition>` for the game's definition type | `Register<GameRulesRuntime<TPileCounterRulesDefinition>, PileCounterRulesRuntime>(Lifetime.Transient)` | The game-specific runtime brain |
| One `Mutator<TState, TPayload>` per game-authored slice | `Register<PileCounterMutator>(Lifetime.Singleton).AsImplementedInterfaces()` | Each slice declared via `ISliceProvider` needs at least one mutator (warning, not error — see Validation below) |
| One `ICommandHandler<TCommand, TResult>` per game-authored `GameAction` | `Register<TMoveCardHandler>(Lifetime.Singleton).AsImplementedInterfaces()` | Handler resolution at dispatch time |

`Build` throws an aggregate exception if any required registration is missing.

#### Game-optional

| Registration | Purpose |
|---|---|
| `IIntentMapper` implementation | Required only if the game uses the gateway intent → action flow; pure-headless games may omit |
| Game-specific subsystems (`IStackSystem`, `IDeckSystem`, `IPuzzleFactory`, …) | Whatever the runtime constructor declares as dependencies |
| Per-actor / per-game services | Game's choice |

#### Resolution at `Build`

The factory resolves registrations in this order:

1. `MatchSetup.Game.Rules` (a `GameRulesDefinition` instance) is registered into the per-match scope so the runtime constructor can take it as a typed parameter.
2. `GameRulesRuntime<TDefinition>` resolved via container open-generic resolution: `container.Resolve(typeof(GameRulesRuntime<>).MakeGenericType(setup.Game.Rules.GetType()))`.
3. `IEnumerable<Mutator>` collection-resolved; primes the `Store`.
4. `ICommandPipeline` resolved (handlers resolve at dispatch time).
5. `IIntentMapper` resolved when present.

The factory itself never sees `IContainerBuilder` or `IObjectResolver` — those are owned by the host or app-level controller. The factory is constructor-injected by VContainer with the resolver it needs.

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
    public required GameDefinition         Game           { get; init; }
    public required IReadOnlyList<IActor>  Actors         { get; init; }
    public          int?                   Seed           { get; init; }

    // Optional overlays — escape hatches for tests, scripted scenarios, asymmetric games.
    public IEnumerable<BaseState>?                      InitialSlices            { get; init; }
    public IEnumerable<(ZoneDefinition Def, IActor? Owner)>? ZoneOverlay         { get; init; }
    public IReadOnlyDictionary<IActor, PlayerDefinition>?    PlayerDefinitionOverlay { get; init; }
}
```

Fields:

- **`Game`** — the `GameDefinition` bundle. Carries `Rules` (a `GameRulesDefinition`), `SharedZones` (game-level zone layout), `CardCatalog` (the cards usable in this mode), `DefaultPlayerDefinition` (applied to all player actors unless overridden), and `GetInitialSlices()` (game-singleton slices). See [Game Definition](features/engine/models/07-game-definition.md).
- **`Actors`** — the actor catalog for this match. `Player` instances (entity-backed, `IActor`) go in alongside non-entity actors (`World`, `Environment`, `Bot`, `Market` — lightweight records implementing `IActor`).
- **`Seed`** — optional RNG seed for deterministic randomness. Networking will eventually pin a richer `IRandom` contract; for now, the value enters the factory here.

Optional overlays:

- **`InitialSlices`** — additional game-authored slice values at t=0 beyond what `GameDefinition.GetInitialSlices()` returns. Test/scripted-scenario escape hatch. Framework-owned slices are seeded automatically; the caller does not include them.
- **`ZoneOverlay`** — additional `(ZoneDefinition, owner)` pairs to spawn beyond the standard layout. The owner is the `IActor` the zone is structurally assigned to (`null` for game-shared overlay zones). Used for sandbox modes, MTG limited-format puzzles, scripted setups.
- **`PlayerDefinitionOverlay`** — per-actor `PlayerDefinition` overrides for asymmetric games (different heroes per player). Symmetric games leave this empty and the factory applies `Game.DefaultPlayerDefinition` to every player actor.

`Rules` no longer lives directly on `MatchSetup` — it's carried inside `GameDefinition`. The serializable definition selection is the `GameDefinition` concrete type; the factory resolves the matching `GameRulesRuntime<TDefinition>` from the container by `Game.Rules.GetType()`.

### `IMatchBuilder`

```csharp
public interface IMatchBuilder
{
    IMatchBuilder WithGame                   (GameDefinition game);
    IMatchBuilder WithActors                 (IEnumerable<IActor> actors);
    IMatchBuilder WithActors                 (params IActor[] actors);
    IMatchBuilder WithSeed                   (int seed);

    // Overlays
    IMatchBuilder WithInitialSlices          (IEnumerable<BaseState> slices);
    IMatchBuilder WithInitialSlices          (params BaseState[] slices);
    IMatchBuilder WithZoneOverlay            (IEnumerable<(ZoneDefinition Def, IActor? Owner)> overlay);
    IMatchBuilder WithPlayerDefinitionOverlay(IReadOnlyDictionary<IActor, PlayerDefinition> overlay);

    GameEngine    Build                      ();
}
```

`WithActors` and `WithInitialSlices` are overloaded for both literal (`params`) and computed (`IEnumerable`) call sites.

### Framework auto-seeded slices

`Build` auto-seeds every framework-owned slice declared by per-feature Models with the initial values declared there. The full list is not enumerated here — [`engine/04 §Framework-owned slices`](features/engine/models/04-gamestate-slices.md) (and as more features land, the cards/zones/etc. Models) is the source of truth. Game authors never include framework slices in `MatchSetup.InitialSlices`; doing so throws at validation.

### Behavior resolution at `Build`

The factory operates **inside** a per-match DI scope opened by the host (or app-level controller) — scope lifetime is the host's responsibility, not the factory's (see [`engine/06 §Disposal boundary`](features/engine/models/06-match-lifecycle.md)). The factory:

1. Registers the `MatchSetup.Game.Rules` definition instance into the surrounding scope (so the runtime constructor can take its typed `TDefinition` parameter).
2. Resolves `GameRulesRuntime<TDefinition>` via container open-generic resolution — `container.Resolve(typeof(GameRulesRuntime<>).MakeGenericType(setup.Game.Rules.GetType()))` — picking up the definition, framework services, and any game-specific subsystems in one constructor call.
3. Collection-resolves every `Mutator` from the container; primes the `Store`.
4. Resolves `ICommandPipeline` (which resolves handlers from the container at dispatch time).
5. Resolves `IIntentMapper` if the game registered one.
6. Constructs the `Store` and walks the slice-installation pseudocode from [Game Definition §Build walk](features/engine/models/07-game-definition.md): framework slices auto-seeded, then per-spawn instances (zones, players) spawned from the layout blocks + overlays, then game-singletons from `GameDefinition.GetInitialSlices()`, then manual `MatchSetup.InitialSlices` overlay.
7. Constructs the `GameEngine` with pipeline, runtime, store. The engine disposes the runtime instance on its own `Dispose`; the surrounding DI scope is owned and torn down by the host (see [`engine/06 §Disposal boundary`](features/engine/models/06-match-lifecycle.md)).

The factory does not register behaviors itself — those came in via the installer. The factory only assembles them. The runtime is single-phase constructed: when its constructor returns, every dependency is bound; there is no separate `Initialize` hook.

**Build ordering corollary.** Per-spawn instances (zones, players) are spawned *before* game-singleton slices install. Singleton slices that need to reference instance Refs as data — e.g., a `TurnState.Active` field carrying `Ref<Player>` — start with default/null values; setup commands populate them after `engine.StartMatch()`. See [Game Definition §Actor-aware singletons](features/engine/models/07-game-definition.md).

### Validation

`Build` walks every check before returning, and throws a single aggregate exception if any fail (collect-all, not fail-fast):

- The `MatchSetup.Game.Rules` definition's concrete type has a registered `GameRulesRuntime<TDefinition>` implementation in the container.
- `Actors` is non-empty.
- No two slices declared by `Game.GetInitialSlices()` + `MatchSetup.InitialSlices` share the same `BaseState` subtype.
- No `MatchSetup.InitialSlices` entry is a framework-owned slice (caller-seeded framework slices are an error, not silently overridden).
- `MatchSetup.PlayerDefinitionOverlay` keys are all in `MatchSetup.Actors` (every overridden actor exists).
- `MatchSetup.ZoneOverlay` owners (when non-null) are all in `MatchSetup.Actors`.
- All required framework services are resolvable.
- Every game-authored slice in the merged install set has at least one `Mutator` registered against it (warning, not error — orphan slice may be intentional).

The exception carries the full error list, not just the first.

### Engine state at return

After `Build` returns, the `GameEngine` is in `EngineLifecycle.NotStarted` — framework slices are seeded (including `EngineState { Lifecycle: NotStarted, IsRunning: false, CurrentCommand: null }`), game-authored slices hold their initial values, and the lifecycle gate rejects any `Dispatch` that isn't `StartMatchCommand` / `EndMatchCommand`. The host calls `engine.StartMatch()` to transition to `Started` and open the engine for normal play; that call internally dispatches `StartMatchCommand`, which emits `MatchStartedSignal` and flips the lifecycle. Match-specific setup (deal opening hands, advance to first phase) is *not* a factory or `StartMatch` parameter — it is the host's or runtime's responsibility, expressed as ordinary commands dispatched after `StartMatch`. See [`engine/06-match-lifecycle.md`](features/engine/models/06-match-lifecycle.md).

### Mode-specific command vocabularies

The factory does not accept a per-match handler pool. Vocabulary differences between game modes (Tutorial disabling certain commands, Sandbox enabling debug commands) are expressed in `handler.Validate` rejection (per-command legality) and in the available-actions snapshot (UI prophylactic). Per [ADR-0006](decisions/0006-two-pipeline-architecture.md) the runtime no longer has an `OnIncomingCommand` hook; mode-specific vocabulary lives in handlers and the snapshot. Every match resolves the full installed handler catalog from the container.

## Invariants

1. **The factory is DI-resolved.** Callers never `new` a `GameFactory`; they ask the container for `IGameFactory`. VContainer never appears in the caller's match-construction code.
2. **Per-game behaviors are installer-only.** Handlers, mutators, `GameRulesRuntime<TDefinition>` implementations, `IIntentMapper`, and game-specific subsystems are registered exactly once per app in the installer. The factory never accepts them as match parameters.
3. **Per-match data is factory-only.** `GameDefinition`, actors, seed, and overlays enter through `MatchSetup` (or `IMatchBuilder`) on every match. They are never registered globally.
4. **`GameDefinition` is the bundle.** It carries `Rules`, `SharedZones`, `CardCatalog`, `DefaultPlayerDefinition`, and `GetInitialSlices()`. `MatchSetup` does not duplicate any of these — it carries the bundle and the per-match overlays.
5. **Framework slices are auto-seeded.** Game authors never include `AvailableActionsState`, `EngineState`, `HistoryState`, or any framework-owned slice in `GameDefinition.GetInitialSlices()` or `MatchSetup.InitialSlices`.
6. **Validation is collect-all.** `Build` reports every detected error in a single aggregate exception; it does not throw on the first issue.
7. **Engine is in `NotStarted` at return.** No commands have been dispatched; no drain has run; the lifecycle gate rejects all `Dispatch` calls except `StartMatchCommand` / `EndMatchCommand`. The host calls `engine.StartMatch()` to open the engine; match-specific setup is dispatched as ordinary commands after that.
8. **Mode-specific command vocabularies are rules-mediated.** The factory does not accept a per-match handler pool; vocabulary differences live in `GameRulesRuntime` and the snapshot.
9. **Single-phase runtime construction.** `GameRulesRuntime<TDefinition>` is fully constructed by the container in one shot — no two-phase Initialize. Definition + framework services + game subsystems all flow through the constructor.
10. **Engine disposes its runtime instance.** `engine.Dispose` releases the runtime if it implements `IDisposable`. The per-match DI scope is owned and torn down by the host, not the engine — see [`engine/06 §Disposal boundary`](features/engine/models/06-match-lifecycle.md).
11. **Build orders per-spawn first, singletons second.** Zones (from layout blocks + overlay) and players (from actors + definition overlay) spawn before game-singleton slices install. Singletons that need actor refs as data start with default/null values; setup commands populate them after `engine.StartMatch()`.

## Decisions

*n/a — no elevated ADR.* (The factory's two-layer split is a single-feature decision; folded below the divider as the chosen option set.)

## Examples

### Pile Counter under the new shape

```csharp
public static class PileCounterBootstrap
{
    public static GameEngine Create(IPuzzleFactory puzzleFactory, IGameFactory gameFactory)
    {
        var puzzle = puzzleFactory.Create();

        return gameFactory.NewMatch()
            .WithGame(new PileCounterGameDefinition(puzzle))   // bundle: Rules + zones + cards + GetInitialSlices()
            .WithActors(new SoloPlayer(ActorId.Player1))       // Player : InstanceWithSlices<PlayerDefinition>, IActor
            .WithSeed(puzzle.Seed)
            .Build();
    }
}

// The game-mode bundle. Per-puzzle variation flows through the constructor —
// the `GameDefinition` carries the puzzle's piles/targets/deck as part of
// `SharedZones` + `GetInitialSlices()`.
public sealed class PileCounterGameDefinition : GameDefinition
{
    private readonly Puzzle puzzle;

    public PileCounterGameDefinition(Puzzle puzzle) => this.puzzle = puzzle;

    public override GameRulesDefinition           Rules                   => PileCounterRulesDefinition.Default;
    public override IReadOnlyList<CardDefinition> CardCatalog             => puzzle.Deck;
    public override IEnumerable<ZoneDefinition>   SharedZones             =>
        puzzle.Targets.Select(kv => new PileDefinition { Target = kv.Value });
    public override PlayerDefinition              DefaultPlayerDefinition => SoloPlayerDefinition.Default;

    public override IEnumerable<BaseState> GetInitialSlices()
    {
        yield return new LastMoveState(Move: null);
        // Framework slices (AvailableActionsState, EngineState, HistoryState) auto-seeded.
        // Per-zone slices come from each PileDefinition.GetInitialSlices() at spawn.
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
        builder.Register<PileCounterMutator>       (Lifetime.Singleton).AsImplementedInterfaces();
        builder.Register<LastMoveMutator>          (Lifetime.Singleton).AsImplementedInterfaces();
        builder.Register<GameRulesRuntime<PileCounterRulesDefinition>, PileCounterRulesRuntime>(Lifetime.Transient);
        builder.Register<PileCounterIntentMapper>  (Lifetime.Singleton).AsImplementedInterfaces();
    }
}
```

The framework's `EngineInstaller` runs alongside, registering `IStateScope`, `IGameFactory`, etc.

### `MatchSetup` direct form (tests and scripted scenarios)

```csharp
var engine = factory.Create(new MatchSetup
{
    Game   = new PileCounterGameDefinition(testPuzzle),
    Actors = new IActor[] { new SoloPlayer(ActorId.Player1) },
    Seed   = 12345,
    InitialSlices = new BaseState[]
    {
        // Optional overlay — tests can inject extra slices beyond what the
        // GameDefinition declares. Used sparingly.
    },
});
```

## Open follow-ups

- **Empty-actors mode** — whether `Actors` may be empty (purely simulation-driven games like a cellular automaton) is undecided. Defer until a real game forces it.
- **Networking-driven `IRandom`** — `Seed` is currently an `int`. When networking lands, `IRandom` (or a richer determinism contract) replaces this slot.
- **`bl-actor-ownership-reverse-index`** — `OwnershipState` is one-way today (`Reference → IActor`). Reverse direction (`IActor → owned References`) is deferred until a consumer surfaces. See [`engine/07 §Ownership and zone assignment`](features/engine/models/07-game-definition.md).

---

*Below: provenance — how the factory's two-layer split and per-axis sub-decisions were settled. Single-feature scope (no project-wide ADR applies; folded into Model provenance per §4.5 on 2026-05-08).*

## Options considered

- **A — Caller-`new`'d builder, container injected at `Build(container)`** — the original pile-counter §9.8 sketch. Rejected: leaks VContainer into every bootstrap call site; the builder is forced to know how to resolve services; conflates DI installation with match construction.
- **B — Caller-`new`'d builder, container injected at construction** — `new GameEngineBuilder(container).With...().Build()`. Rejected: same DI leak as A, just earlier in the chain. No structural improvement.
- **C — DI-resolved factory** — chosen. Caller never sees the container; framework + game services arrive via constructor injection on the factory.

### Sub-decision — handler pool scoping

- **Build-time scoped pool** — factory takes `WithCommandPool(...)`; pipeline rejects out-of-pool commands at intake. Rejected: duplicates `handler.Validate`'s authority over per-command legality; introduces a parallel filter that handlers and pool must agree on by convention.
- **Install-time fixed pool** — chosen. Every match resolves the full installed handler catalog; mode-specific vocabularies are expressed in `handler.Validate` rejection and the available-actions snapshot.

### Sub-decision — rules selection form

- **Type-token** (`WithRules<TutorialRules>()`) — rejected. Compiler-checked but doesn't survive serialization; replay/save/network paths require translation.
- **String/typed ID** — rejected. Smallest data form but carries no per-instance config; mode variants would proliferate as separate classes.
- **Serializable data object** — chosen. The data is `GameRulesDefinition` (Unity `[SerializeField]` attributes, non-`UnityObject`); a Unity wrapper provides editor assignability. Behavior lives separately on `GameRulesRuntime<TDefinition>` (DI-resolved per match). Mode variants share runtime classes via different definition assets.

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

### Sub-decision — runtime/definition reconciliation

- **Single type with `Initialize(services)` hook** — rejected. Two-phase construction creates a "half-initialized" window and forces every method to guard against pre-init calls.
- **Services passed per-call** — rejected. Bloats every contract method's signature and breaks the locked four-method surface.
- **Definition + Runtime split, generic-parameterized, single-phase DI construction** — chosen. `GameRulesDefinition` is pure data; `GameRulesRuntime<TDefinition>` is the stateful runtime. Container open-generic resolution pairs them at `Build`. See [`engine/02 §The two-type split` and §The interface](features/engine/models/02-gamerules-contract.md) for the full contract.

## Rationale

The two-layer split is the load-bearing decision. Once accepted, every other choice falls out:

- Behaviors are stable across matches, so they belong in DI (registered once, resolved on demand).
- Per-match data isn't typeable, so it must enter as values through the factory.
- The factory itself is DI-resolved because it needs services to do its job; resolving the factory through DI keeps VContainer out of the caller's match-construction code entirely.

This split also opens the future path to data-driven match construction: a serialized `MatchSetup` (plus eventual `GameDefinition`) loads from disk / network / replay file and the factory reconstructs the engine. None of this is possible if behavior types are part of the per-match input.

## Consequences

- **Pile-counter bootstrap shrinks.** The §9.8 sketch's seven `With…` calls collapse to four (rules, actors, initial slices, seed). Behaviors move to the installer.
- **`engine/02-gamerules-contract.md` reshape.** Resolved alongside this Model — `GameRules` split into `GameRulesDefinition` (data) + `GameRulesRuntime<TDefinition>` (runtime). Factory uses container open-generic resolution to pair them.
- **Glossary updates.** `Player` shifts from "game-specific" to "framework-provided baseline." `IGameFactory`, `MatchSetup`, `GameRulesDefinition`, `GameRulesRuntime` are added/refreshed.
- **No call site sees VContainer.** Every bootstrap path goes through `IGameFactory`. The `IObjectResolver` parameter that pile-counter §9.8 currently accepts disappears.
- **Test ergonomics improve.** Tests construct `MatchSetup` directly with hand-built initial slices and inject a mock `IGameFactory` if they want to bypass DI; behavior still goes through real handlers/mutators registered in a test installer.
