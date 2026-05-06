---
type: model
status: settled
references: [features/engine/models/05-game-factory.md, features/engine/models/04-gamestate-slices.md, features/engine/models/02-gamerules-contract.md, features/zones/models/01-zone-model.md, features/cards/models/01-card-model.md, features/players/models/01-player-model.md, features/entities/models/01-definition-instance-model.md]
tags: [#engine, #game-definition, #slices, #factory]
---

# Game Definition — bundle, slice declaration, factory walk

## Problem

Constructing an engine for a match needs a coherent description of the game-mode being played: which rules apply, which zones exist (game-shared and per-player), which cards are usable, which player shape applies, and which game-singleton slices to seed at t=0. The previous factory Model accepted an unstructured `MatchSetup.InitialSlices` enumerable for game-singleton state — caller-driven, with no validation against the rules' implicit slice expectations.

This Model defines the **`GameDefinition`** bundle that owns the game-mode shape, the **`ISliceProvider`** mechanism every framework definition kind implements to declare its initial slices, and the **factory walk** that ties them together at `Build`.

## Concepts

| Term | Definition |
|---|---|
| `GameDefinition` | Top-level bundle for a game-mode variant. Carries `Rules` (a `GameRulesDefinition`), `SharedZones` (the game-level zone layout), `CardCatalog` (the cards usable in this mode), `DefaultPlayerDefinition` (applied to all player actors unless overridden), and `GetInitialSlices()` (game-singleton slices). Implements `ISliceProvider`. |
| `ISliceProvider` | Parameterless interface — `IEnumerable<BaseState> GetInitialSlices()`. Implemented on every framework definition kind (`CardDefinition`, `ZoneDefinition`, `PlayerDefinition`, `GameDefinition`) and on `EntityInstance<TDef>` itself (instance delegates to definition by default). |
| Layout block | The `SharedZones` collection on `GameDefinition` and the `PerPlayerZones` collection on `PlayerDefinition`. The factory walks these at `Build` and spawns one `Zone` per shared-zone definition (game-level) and one `Zone` per `(player, perPlayerZone)` pair (per-player). |
| `MatchSetup` overlay | Three optional fields on `MatchSetup` — `InitialSlices`, `ZoneOverlay`, `PlayerDefinitionOverlay` — that allow per-match deviations from the standard layout for tests, scripted scenarios, and asymmetric games. |
| External slice keying | Convention: framework slices don't carry their own primary key; the factory keys slices externally using the spawned instance's `SelfRef` (or no key for game-singletons). Reference-shaped fields inside slice values are *data*, not keys. |

## Contracts

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

- **`Rules`** — the `GameRulesDefinition` that pairs (via container open-generic resolution) with the `GameRulesRuntime<TDefinition>` for this mode. See [`engine/02-gamerules-contract.md`](features/engine/models/02-gamerules-contract.md).
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

The single, uniform interface for declaring initial slices. Implemented by:

- **`GameDefinition`** — game-singleton slices (no keying).
- **`ZoneDefinition`** — per-zone slices, installed at `zone.SelfRef`.
- **`PlayerDefinition`** — per-player non-variable slices, installed at `player.SelfRef`. Variables go in `EntityState` automatically via the bridge.
- **`CardDefinition`** — per-card non-variable slices (rare; default is empty), installed at `card.SelfRef`.
- **`EntityInstance<TDef>`** — default implementation delegates to `Definition.GetInitialSlices()`. Concrete entity-instance subclasses (`Card`, `Zone`, `Player`) override only when spawn-time customization is needed (rare).

The factory walks instances at spawn and installs returned slices at the instance's `SelfRef`. The interface is parameterless because slices don't carry their own primary key — the factory keys externally.

### Layout blocks

```csharp
public abstract class GameDefinition
{
    public abstract IEnumerable<ZoneDefinition> SharedZones { get; }
    // … other fields
}

public abstract class PlayerDefinition : Scaffold.EntityDefinition, ISliceProvider
{
    public abstract IEnumerable<ZoneDefinition> PerPlayerZones { get; }
    // … inherits ISliceProvider
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
1. Auto-seed framework slices (AvailableActionsState, EngineState, HistoryState, …)
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

### `EntityInstance<TDef>` instance-delegates-to-definition

```csharp
public class EntityInstance<TDef> : ISliceProvider where TDef : IEntityDefinition
{
    public TDef                   Definition { get; }
    public IEntityVariableStorage Storage    { get; }

    public virtual IEnumerable<BaseState> GetInitialSlices()
        => Definition is ISliceProvider provider
            ? provider.GetInitialSlices()
            : Array.Empty<BaseState>();
}
```

The default implementation lets the factory's spawn loop work instance-centric — `foreach instance: foreach slice in instance.GetInitialSlices(): store.Install(slice, key: instance.SelfRef)`. Concrete entity-instance subclasses override only when spawn-time customization is needed (e.g., a card spawned with custom seeded counters that aren't in the definition's defaults). Rare.

### Slices don't carry their primary key

Convention pinned by this Model:

- `EntityState(BaseValues, ModifierStacks)` — keyed externally by entity ref.
- `ZoneContentsState(ImmutableList<CardRef>)` — keyed externally by zone ref.
- `OwnershipState(ImmutableDictionary<Reference, IActor> Owners)` — singleton dict.
- `PileState(Counter, Target)` — keyed externally by zone ref. **Not** `PileState(Zone, Counter, Target)`.

Reference-shaped fields inside slice *values* (e.g., `LastMoveState(Ref<Card>, From, To)`) are *data*, not keys.

This convention is what lets `ISliceProvider` be parameterless: providers yield slice values; the factory keys.

## Ownership and zone assignment

The framework distinguishes **structural ownership** (declarative, set at `Build`) from **runtime ownership** (mutable):

- **Zone ownership is structural.** A zone "belongs to" its actor by virtue of where it was declared — `GameDefinition.SharedZones` (game-level, no actor) or a specific `PlayerDefinition.PerPlayerZones` (instantiated per player). The factory records this assignment when spawning. It's stable over the match lifetime in normal play; games that need dynamic zone reassignment build their own per-game indexes.
- **Card ownership is runtime, mutable.** Tracked in `OwnershipState` (`Reference → IActor`). Cards transfer between players in some games (MTG ownership-vs-control); `OwnershipState` follows the transfers.

`OwnershipState` is one-way today (forward direction only). Reverse-direction queries ("what does this player own?") are deferred — see `bl-actor-ownership-reverse-index` in [`OPEN-ITEMS.md`](ledgers/OPEN-ITEMS.md). Until that lands, reverse queries are O(N) filters or game-specific aggregates.

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

## Invariants

1. **`ISliceProvider` is parameterless.** The interface contract is `IEnumerable<BaseState> GetInitialSlices()`. Implementers don't see the instance's ref; the factory keys externally.
2. **Slices don't carry their primary key.** Storage is keyed by the factory using the instance's `SelfRef`. Reference-shaped fields inside slice values are data, not keys.
3. **`EntityInstance<TDef>` delegates to its definition by default.** Spawn-time override on the instance side is optional and rare.
4. **The factory walks per-spawn instances before game singletons.** Singleton slices that need actor refs as data start with default/null; setup commands populate after `StartMatch`.
5. **Layout is declarative.** `GameDefinition.SharedZones` and `PlayerDefinition.PerPlayerZones` declare the standard layout; `MatchSetup.ZoneOverlay` is the per-match escape hatch.
6. **Zone ownership is structural; card ownership is runtime.** Zones are assigned to actors at `Build` based on layout-block declarations and overlay; cards are tracked in `OwnershipState` (mutable). `OwnershipState` reverse direction is deferred (`bl-actor-ownership-reverse-index`).
7. **`GameDefinition` is the bundle.** It carries `Rules`, `SharedZones`, `CardCatalog`, `DefaultPlayerDefinition`, and `GetInitialSlices()`. `MatchSetup` carries the bundle and the per-match overlays.

## Related

- [`features/engine/models/05-game-factory.md`](features/engine/models/05-game-factory.md) — `IGameFactory`, `MatchSetup`, `IMatchBuilder`, `Build` validation, behavior resolution.
- [`features/engine/models/02-gamerules-contract.md`](features/engine/models/02-gamerules-contract.md) — `GameRulesDefinition` + `GameRulesRuntime<TDefinition>` contract (carried inside `GameDefinition`).
- [`features/engine/models/04-gamestate-slices.md`](features/engine/models/04-gamestate-slices.md) — slice tier model, framework-owned vs game-authored, subscription channels.
- [`features/zones/models/01-zone-model.md`](features/zones/models/01-zone-model.md) — `ZoneDefinition` shape; layout-block participation.
- [`features/cards/models/01-card-model.md`](features/cards/models/01-card-model.md) — `CardDefinition` shape; per-card non-variable slices via `ISliceProvider`.
- [`features/players/models/01-player-model.md`](features/players/models/01-player-model.md) — `PlayerDefinition` shape; per-player zones; variable bag for primary state.
- [`features/entities/models/01-definition-instance-model.md`](features/entities/models/01-definition-instance-model.md) + [`features/entities/models/02-runtime-surface.md`](features/entities/models/02-runtime-surface.md) — `EntityInstance<TDef>` and the `ISliceProvider` instance-delegates-to-definition default.
