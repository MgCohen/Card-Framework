---
type: model
status: abandoned
archived_on: 2026-10-18
reason: "Written for the old state model (slices, Ref<T>, MatchStateSlice) and never built. Read it for the open questions, not as the contract; the replacement design comes from Product/spikes/framework-roadmap.md."
references: [features/engine/archive/05-game-factory.md, features/state/archive/state.md, features/engine/archive/02-game-rules-definition.md, features/zones/archive/01-zone-model.md, features/cards/archive/01-card-model.md, features/players/archive/01-player-model.md, features/entities/archive/01-definition.md, features/entities/archive/02-instance.md]
tags: [#engine, #game-definition, #slices, #factory]
---

> **Archived 2026-10-18.** Written for the old state model (slices, Ref<T>, MatchStateSlice) and never built. Read it for the open questions, not as the contract; the replacement design comes from Product/spikes/framework-roadmap.md.

# Game Definition — model

## What it is

The top-level bundle for a game-mode variant. **`GameDefinition`** carries `Rules` (a `GameRulesDefinition`), `SharedZones` (the game-level zone layout), `CardCatalog` (the cards usable in this mode), `DefaultPlayerDefinition` (applied to all player actors unless overridden), and `GetInitialSlices()` (game-singleton slices). It implements **`ISliceProvider`** — a parameterless interface every framework definition kind implements (via `DefinitionWithSlices`) to declare its initial slices. The factory walks `GameDefinition` at `Build` in a six-step canonical order: auto-seed framework slices → spawn shared zones → spawn players → spawn per-player zones → install per-spawn slices keyed at `instance.SelfRef` → install game-singletons → install manual `InitialSlices` overlay.

```csharp
public abstract class GameDefinition : ISliceProvider
{
    public abstract GameRulesDefinition           Rules                   { get; }
    public abstract IReadOnlyList<CardDefinition> CardCatalog             { get; }
    public abstract IEnumerable<ZoneDefinition>   SharedZones             { get; }
    public abstract PlayerDefinition              DefaultPlayerDefinition { get; }

    public virtual IEnumerable<BaseState> GetInitialSlices()
        => Array.Empty<BaseState>();
}

public interface ISliceProvider
{
    IEnumerable<BaseState> GetInitialSlices();
}
```

## Contracts

### Concepts

| Term | Definition |
|---|---|
| `GameDefinition` | Top-level bundle for a game-mode variant. Carries `Rules`, `SharedZones`, `CardCatalog`, `DefaultPlayerDefinition`, and `GetInitialSlices()`. Implements `ISliceProvider`. |
| `ISliceProvider` | Parameterless interface — `IEnumerable<BaseState> GetInitialSlices()`. Reached on Definition kinds via the framework parent `DefinitionWithSlices` (which `CardDefinition` / `ZoneDefinition` / `PlayerDefinition` inherit) and directly on `GameDefinition`. Reached on Instance kinds via the framework parent `InstanceWithSlices<TDef>` (which `Card` / `Zone` / `Player` inherit). The instance's default implementation delegates to the (protected) definition. |
| Layout block | The `SharedZones` collection on `GameDefinition` and the `PerPlayerZones` collection on `PlayerDefinition`. The factory walks these at `Build` and spawns one `Zone` per shared-zone definition (game-level) and one `Zone` per `(player, perPlayerZone)` pair (per-player). |
| `MatchSetup` overlay | Three optional fields on `MatchSetup` — `InitialSlices`, `ZoneOverlay`, `PlayerDefinitionOverlay` — that allow per-match deviations from the standard layout for tests, scripted scenarios, and asymmetric games. |
| External slice keying | Convention: framework slices don't carry their own primary key; the factory keys slices externally using the spawned instance's `SelfRef` (or no key for game-singletons). Reference-shaped fields inside slice values are *data*, not keys. |

### `GameDefinition`

```csharp
public abstract class GameDefinition : ISliceProvider
{
    public abstract GameRulesDefinition           Rules                   { get; }
    public abstract IReadOnlyList<CardDefinition> CardCatalog             { get; }
    public abstract IEnumerable<ZoneDefinition>   SharedZones             { get; }
    public abstract PlayerDefinition              DefaultPlayerDefinition { get; }

    public virtual IEnumerable<BaseState> GetInitialSlices()
        => Array.Empty<BaseState>();
}
```

Concrete game-mode definitions subclass `GameDefinition` and provide:

- **`Rules`** — the `GameRulesDefinition` that pairs (via container open-generic resolution) with the `GameRulesRuntime<TDefinition>` for this mode. See [`engine/02-game-rules-definition.md`](features/engine/archive/02-game-rules-definition.md) and [`engine/03-game-rules-runtime.md`](features/engine/archive/03-game-rules-runtime.md).
- **`CardCatalog`** — the cards available in this mode (the format/banlist). Per-match deck composition is separate; setup commands or specific game flows decide which cards land in which zones. The catalog is reference data.
- **`SharedZones`** — game-level zone definitions. The factory spawns one `Zone` per definition at `Build`. Examples: stack, battlefield, market, shared discard.
- **`DefaultPlayerDefinition`** — the `PlayerDefinition` applied to every player actor in `MatchSetup.Actors`. Asymmetric games override per-actor via `MatchSetup.PlayerDefinitionOverlay`.
- **`GetInitialSlices()`** — game-singleton slices to install at `Build`. Examples: `TurnState`, `PhaseState`, `PriorityState`, `LastMoveState`. Slices that need actor refs at construction (e.g., `TurnState.Active`) start with default/null and get populated by setup commands after `engine.StartMatch()`.

### `ISliceProvider`

```csharp
public interface ISliceProvider
{
    IEnumerable<BaseState> GetInitialSlices();
}
```

The single, uniform interface for declaring initial slices. Reached through the framework parents:

- **`GameDefinition`** — implements `ISliceProvider` directly. Game-singleton slices (no keying).
- **`DefinitionWithSlices`** — framework parent (`: Scaffold.EntityDefinition, ISliceProvider`). `ZoneDefinition`, `PlayerDefinition`, `CardDefinition` inherit and override `GetInitialSlices()` to declare per-instance slices, installed at the instance's `SelfRef`.
- **`InstanceWithSlices<TDef>`** — framework parent (`: Scaffold.EntityInstance<TDef>, ISliceProvider`). `Card`, `Zone`, `Player` inherit. The default implementation delegates to the (protected) `Definition.GetInitialSlices()`. Concrete entity-instance subclasses override only when spawn-time customization is needed (rare).

The factory walks instances at spawn and installs returned slices at the instance's `SelfRef`. The interface is parameterless because slices don't carry their own primary key — the factory keys externally.

### Layout blocks

```csharp
public abstract class GameDefinition
{
    public abstract IEnumerable<ZoneDefinition> SharedZones { get; }
    // … other fields
}

public abstract class PlayerDefinition : DefinitionWithSlices
{
    public abstract IEnumerable<ZoneDefinition> PerPlayerZones { get; }
    // … inherits ISliceProvider via DefinitionWithSlices
}
```

The layout is **declarative**: `GameDefinition.SharedZones` declares game-level zones (instantiated once); `PlayerDefinition.PerPlayerZones` declares per-player zones (instantiated once per player actor). The factory walks both at `Build` and spawns the corresponding `Zone` instances.

Per-match deviations flow through `MatchSetup.ZoneOverlay`:

```csharp
public sealed record MatchSetup
{
    // …
    public IEnumerable<(ZoneDefinition Def, IActor? Owner)>? ZoneOverlay { get; init; }
}
```

Each overlay entry is a `(ZoneDefinition, owner)` pair. Owner is the `IActor` the zone is structurally assigned to; `null` means a shared overlay zone with no owner. Use cases: scripted scenario tests (extra zones for setup), Stacklands sandbox modes, MTG limited-format puzzles.

### Build walk

```text
1. Auto-seed framework slices (MatchStateSlice, EngineState, HistoryState, PendingEffectsSlice, RequestsSlice, AnswersSlice, …)
   — declared by per-feature Models, not enumerated here.

2. Spawn per-spawn instances:

   2a. Game-shared zones:
       foreach zoneDef in matchSetup.Game.SharedZones
           zone = store.Spawn(zoneDef)               // owner = null (game-level)

   2b. Player actors (entity-backed):
       foreach actor in matchSetup.Actors where actor is Player-shaped
           playerDef = matchSetup.PlayerDefinitionOverlay.GetValueOrDefault(actor)
                       ?? matchSetup.Game.DefaultPlayerDefinition
           player = store.Spawn(playerDef)
           // (player.Id ← actor.Id; mapping handled by Player.Spawn)

   2c. Per-player zones (one per player per layout entry):
       foreach player in spawnedPlayers
           foreach zoneDef in player.Definition.PerPlayerZones
               zone = store.Spawn(zoneDef)           // owner = player (structural)

   2d. Zone overlay (per-match deviations):
       foreach (zoneDef, owner) in matchSetup.ZoneOverlay ?? []
           zone = store.Spawn(zoneDef)               // owner from overlay

3. Per-spawn slice installation:

   foreach instance in spawnedInstances             // zones, players (cards spawn later via setup commands)
       foreach slice in instance.GetInitialSlices()
           store.Install(slice, key: instance.SelfRef)

4. Game-singleton slices:

   foreach slice in matchSetup.Game.GetInitialSlices()
       store.Install(slice)                         // unkeyed

5. Manual overlay (test/script escape hatch):

   foreach slice in matchSetup.InitialSlices ?? []
       store.Install(slice)

6. Cards: NOT seeded here. Spawned via CreateCardMutation during setup commands
   (after engine.StartMatch). Each CreateCardMutation internally calls
   card.GetInitialSlices() (default delegates to CardDefinition.GetInitialSlices)
   and installs at card.SelfRef. EntityState (variables) is seeded automatically
   from the definition's variable bag.
```

**Ordering guarantees:**

1. **Per-spawn instances spawn first** (steps 2a–2d), then **game-singletons install** (step 4). This means singleton slices that need to reference instance Refs as data — e.g., a future `TurnOrderState` carrying `ImmutableList<Ref<Player>>` — can read the spawned players' Refs from the catalog if they need to. Most actor-aware singletons (`TurnState.Active`, `PriorityState.Holder`) start with default/null and get populated by setup commands instead.
2. **Within step 2**, ordering is shared-zones → players → per-player-zones → overlay. Per-player zones spawn after their owning player so the structural ownership relationship is deterministic.
3. **Manual `InitialSlices` overlay runs last** (step 5). Test scripts can install slices that reference any instance spawned in steps 2–4.

### `InstanceWithSlices<TDef>` instance-delegates-to-definition

```csharp
// Scaffold (external)
public class EntityInstance<TDef> where TDef : IEntityDefinition
{
    public TDef                   Definition { get; }
    public IEntityVariableStorage Storage    { get; }
}

// Card Framework parent — adds ISliceProvider, hides Definition externally
public class InstanceWithSlices<TDef> : Scaffold.EntityInstance<TDef>, ISliceProvider
    where TDef : IEntityDefinition
{
    protected new TDef Definition => base.Definition;

    public virtual IEnumerable<BaseState> GetInitialSlices()
        => Definition is ISliceProvider provider
            ? provider.GetInitialSlices()
            : Array.Empty<BaseState>();
}
```

The default implementation lets the factory's spawn loop work instance-centric — `foreach instance: foreach slice in instance.GetInitialSlices(): store.Install(slice, key: instance.SelfRef)`. Concrete entity-instance subclasses (`Card`, `Zone`, `Player` — all unsealed) override only when spawn-time customization is needed (e.g., a card spawned with custom seeded counters that aren't in the definition's defaults). Rare.

### Slices don't carry their primary key

Convention pinned by this Model:

- `EntityState(BaseValues, ModifierStacks)` — keyed externally by entity ref.
- `ZoneContentsState(ImmutableList<CardRef>)` — keyed externally by zone ref.
- `OwnershipState(ImmutableDictionary<Reference, IActor> Owners)` — singleton dict.
- `PileState(Counter, Target)` — keyed externally by zone ref. **Not** `PileState(Zone, Counter, Target)`.

Reference-shaped fields inside slice *values* (e.g., `LastMoveState(Ref<Card>, From, To)`) are *data*, not keys.

This convention is what lets `ISliceProvider` be parameterless: providers yield slice values; the factory keys.

### Ownership and zone assignment

The framework distinguishes **structural ownership** (declarative, set at `Build`) from **runtime ownership** (mutable):

- **Zone ownership is structural.** A zone "belongs to" its actor by virtue of where it was declared — `GameDefinition.SharedZones` (game-level, no actor) or a specific `PlayerDefinition.PerPlayerZones` (instantiated per player). The factory records this assignment when spawning. It's stable over the match lifetime in normal play; games that need dynamic zone reassignment build their own per-game indexes.
- **Card ownership is runtime, mutable.** Tracked in `OwnershipState` (`Reference → IActor`). Cards transfer between players in some games (MTG ownership-vs-control); `OwnershipState` follows the transfers.

`OwnershipState` is one-way today (forward direction only). Reverse-direction queries ("what does this player own?") are deferred — see `bl-actor-ownership-reverse-index` in [`OPEN-ITEMS.md`](ledgers/OPEN-ITEMS.md). Until that lands, reverse queries are O(N) filters or game-specific aggregates.

## Invariants

1. **`ISliceProvider` is parameterless.** The interface contract is `IEnumerable<BaseState> GetInitialSlices()`. Implementers don't see the instance's ref; the factory keys externally.
2. **Slices don't carry their primary key.** Storage is keyed by the factory using the instance's `SelfRef`. Reference-shaped fields inside slice values are data, not keys.
3. **`InstanceWithSlices<TDef>` delegates to its (protected) definition by default.** Spawn-time override on the instance side is optional and rare.
4. **The factory walks per-spawn instances before game singletons.** Singleton slices that need actor refs as data start with default/null; setup commands populate after `StartMatch`.
5. **Layout is declarative.** `GameDefinition.SharedZones` and `PlayerDefinition.PerPlayerZones` declare the standard layout; `MatchSetup.ZoneOverlay` is the per-match escape hatch.
6. **Zone ownership is structural; card ownership is runtime.** Zones are assigned to actors at `Build` based on layout-block declarations and overlay; cards are tracked in `OwnershipState` (mutable). `OwnershipState` reverse direction is deferred (`bl-actor-ownership-reverse-index`).
7. **`GameDefinition` is the bundle.** It carries `Rules`, `SharedZones`, `CardCatalog`, `DefaultPlayerDefinition`, and `GetInitialSlices()`. `MatchSetup` carries the bundle and the per-match overlays.

## Decisions

*n/a — no elevated ADR.* (The bundle shape and Build-walk ordering are descriptive — they sit on top of [`engine/05`](features/engine/archive/05-game-factory.md) (factory two-layer split), [`state/state.md`](features/state/archive/state.md) (slice tier model), and the entity-bridge `InstanceWithSlices<TDef>` / `DefinitionWithSlices` parent classes from [`entities/01`](features/entities/archive/01-definition.md). No new options were considered for this Model itself.)

## Examples

### Pile Counter `GameDefinition`

```csharp
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
    }
}
```

Per-puzzle variation flows through the constructor — different `Puzzle` values produce different `SharedZones` sequences and different `CardCatalog`s. The bundle is serializable in principle (puzzle data + format = bundle), supporting save/load and replay.

### Magic-like `GameDefinition`

```csharp
public sealed class MagicGameDefinition : GameDefinition
{
    public override GameRulesDefinition           Rules                   => MagicRulesDefinition.Standard;
    public override IReadOnlyList<CardDefinition> CardCatalog             => StandardLegalCards.All;
    public override IEnumerable<ZoneDefinition>   SharedZones             => new ZoneDefinition[]
    {
        new BattlefieldDefinition(),
        new StackDefinition(),
        new ExileDefinition(),
        new CommandZoneDefinition(),
    };
    public override PlayerDefinition              DefaultPlayerDefinition => StandardPlayerDefinition.Default;

    public override IEnumerable<BaseState> GetInitialSlices()
    {
        yield return new TurnState     (Active: null!, Number: 1);
        yield return new PhaseState    (Current: ((MagicRulesDefinition)Rules).StartingPhase);
        yield return new PriorityState (Holder: null);
        yield return new StackState    (Entries: ImmutableList<StackEntry>.Empty);
    }
}

public sealed class StandardPlayerDefinition : PlayerDefinition
{
    public override IEnumerable<ZoneDefinition> PerPlayerZones => new ZoneDefinition[]
    {
        new HandDefinition       { Capacity = 7 },
        new LibraryDefinition    (),
        new GraveyardDefinition  (),
    };

    // Variables (StartingLife, StartingHandSize) declared via the entity-bridge
    // variable bag; flow through DefinedVariables / TryGetDefaultValue.

    public override IEnumerable<BaseState> GetInitialSlices()
    {
        yield return new ManaPoolState(Pool: ImmutableDictionary<ManaColor, int>.Empty);
    }
}
```

`TurnState.Active` is `null!` at `Build`; the host's setup commands populate it after `engine.StartMatch()` (typically by dispatching a `BeginFirstTurnCommand` that picks the active player by some game-specific rule).

### Asymmetric game with `PlayerDefinitionOverlay`

```csharp
var p1 = new SeatedPlayer(ActorId.Player1);
var p2 = new SeatedPlayer(ActorId.Player2);

var engine = factory.Create(new MatchSetup
{
    Game   = new HearthstoneGameDefinition(),
    Actors = new IActor[] { p1, p2 },
    PlayerDefinitionOverlay = new Dictionary<IActor, PlayerDefinition>
    {
        [p1] = new MageHeroDefinition(),
        [p2] = new WarriorHeroDefinition(),
    },
    Seed = 12345,
});
```

The overlay replaces `Game.DefaultPlayerDefinition` for the specified actors. Each player gets the per-player zones (hand, deck) declared on their specific hero definition.

## Open follow-ups

- **`bl-actor-ownership-reverse-index`** — `OwnershipState` is one-way today (`Reference → IActor`). Reverse direction (`IActor → owned References`) deferred until a consumer surfaces.
- **Card seeding pattern** — cards are spawned via `CreateCardMutation` during setup commands (after `engine.StartMatch()`), not at `Build`. Whether to add a `MatchSetup.InitialDeals` shape (deck-list-driven seeding) for solo puzzles vs. leaving it to setup commands is undecided. Defer until the second non-trivial game forces the question.
- **Definition versioning** — `GameDefinition` is asset-friendly via `GameRulesDefinition` (`[SerializeField]`-bearing data class). Per-asset versioning (migration of stale definitions, replay compatibility) is the future authoring layer's concern (`bl-authoring-feature`).

---

*Below: provenance.*

## Options considered

*n/a — descriptive Model. The bundle shape, `ISliceProvider` interface, and the canonical Build walk are the natural composition of three earlier decisions: (1) [`engine/03 §Q1 (rules-layer shape)`](features/engine/archive/03-game-rules-runtime.md) — `GameRulesDefinition` is the data half of the runtime split, carried on `GameDefinition.Rules`; (2) the factory's two-layer split ([`engine/05`](features/engine/archive/05-game-factory.md)) — per-match data flows through the factory, not through DI; (3) the slice tier model ([`state/state.md`](features/state/archive/state.md)) — framework-owned vs game-authored. No new options-weighing was needed for this Model.*

## Rationale

`GameDefinition` ties the three earlier decisions together into a single authored bundle. Per-puzzle / per-format variation flows through the constructor (different `Puzzle` data → different `SharedZones` and `CardCatalog`); per-match deviations flow through `MatchSetup` overlays (`ZoneOverlay`, `PlayerDefinitionOverlay`, `InitialSlices`). The bundle is serializable in principle (puzzle data + format = bundle), supporting save/load and replay.

`ISliceProvider` is parameterless because slices don't carry their own primary key — the factory keys externally at `instance.SelfRef`. This convention is what lets a game author yield slice values without knowing the instance's identity in advance; it's also what lets the same `PileDefinition` produce N `PileState` slices keyed by N different `Ref<Zone>`s without any per-instance wiring.

The Build-walk ordering (per-spawn instances before game-singletons) is what lets future actor-aware singletons (`TurnState.Active`, `PriorityState.Holder`) start with default/null and get populated by setup commands after `StartMatch` — when those singletons need to reference instance refs as data, the refs have already been allocated.

## Consequences

- **Enables** asset-friendly game-mode bundles (a `GameDefinition` is the unit of persistence/replay/network); per-puzzle / per-format variation through the constructor; per-match deviations through `MatchSetup` overlays; uniform `ISliceProvider`-based slice declaration across `Card` / `Zone` / `Player` / `GameDefinition`; deterministic Build-walk ordering (per-spawn first, game-singletons second, manual overlay last).
- **Constrains** every framework definition kind to inherit from `DefinitionWithSlices` (which provides the `ISliceProvider` default empty implementation); slice values must NOT carry their own primary key (the factory keys externally); singleton slices that need actor refs start with default/null and get populated by setup commands; cards are NOT seeded at `Build` (they spawn via `CreateCardMutation` during setup commands after `StartMatch`).
