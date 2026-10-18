---
type: model
status: abandoned
archived_on: 2026-10-18
reason: "Written for the old state model (slices, Ref<T>, MatchStateSlice) and never built. Read it for the open questions, not as the contract; the replacement design comes from Product/spikes/framework-roadmap.md."
references: [features/entities/archive/01-definition.md, features/entities/archive/02-instance.md, features/engine/archive/05-game-factory.md, features/engine/archive/06-game-definition.md, features/zones/archive/01-zone-model.md, decisions/0005-objects-over-ids.md]
tags: [#players, #actor, #entity, #player-definition, #model]
---

> **Archived 2026-10-18.** Written for the old state model (slices, Ref<T>, MatchStateSlice) and never built. Read it for the open questions, not as the contract; the replacement design comes from Product/spikes/framework-roadmap.md.

# Player Model — model

## What it is

A `Player` is the framework's baseline subtype of actor representing a seated, entity-backed participant. This Model defines `Player`'s shape on the entity bridge, the `IActor` polymorphic-actor interface, the `PlayerDefinition` authoring surface, and the conventions framework Models follow when declaring per-player state.

```
IActor (interface, carries ActorId)
├── Player : InstanceWithSlices<PlayerDefinition>, IActor   (entity-backed seated participant)
└── World, Environment, Bot, Market : record (ActorId)      (lightweight non-entity actors)
```

> Player = entity-on-the-bridge + IActor. Variables (life, mana count, energy) live in `EntityState` via the bridge. Non-variable per-player game state lives in slices declared by `PlayerDefinition.GetInitialSlices()`, keyed at `player.SelfRef`. Per-player zones (hand, deck, discard) come from `PlayerDefinition.PerPlayerZones`, instantiated once per player actor at `Build`.

The framework provides one `Player` class. Per-game-mode variants (Mage hero vs Warrior hero, default symmetric player vs asymmetric role) are different `PlayerDefinition` instances — data-driven, not class-hierarchy-driven.

## Contracts

### `IActor`

```csharp
public interface IActor
{
    ActorId Id { get; }
}
```

The polymorphic actor type. Anything that can own things in the simulation implements `IActor`. Concrete implementers are either entity-backed or lightweight records:

```csharp
// Entity-backed
public class Player : InstanceWithSlices<PlayerDefinition>, IActor
{
    public Ref<Player> SelfRef { get; init; }
    public ActorId     Id      { get; init; }
}

// Non-entity-backed (lightweight; game-side or framework-provided)
public sealed record World      (ActorId Id) : IActor;
public sealed record Environment(ActorId Id) : IActor;
public sealed record Bot        (ActorId Id) : IActor;
public sealed record Market     (ActorId Id) : IActor;
```

`IActor`'s purpose stays narrow: "anything that can own things." It's used by:

- `OwnershipState` — keys `Reference → IActor` for runtime mutable card ownership.
- `MatchSetup.Actors` — the per-match actor catalog (`IReadOnlyList<IActor>`).
- Command attribution — `GameAction.Actor` carries `ActorId`; `IActor.Id` is the bridge.
- History envelopes — `HistoryEnvelope.Cause.Actor` is the originating actor.

If a richer Actor surface is ever needed (capabilities, role-typed dispatch, etc.), `IActor` can grow. For now it stays minimal.

### `Player` class

```csharp
public class Player : InstanceWithSlices<PlayerDefinition>, IActor
{
    public Ref<Player> SelfRef { get; init; }
    public ActorId     Id      { get; init; }   // satisfies IActor

    public Player(PlayerDefinition def, IEntityVariableStorage storage)
        : base(def, storage) { }

    public override bool Equals(object? obj) =>
        obj is Player other && SelfRef.Equals(other.SelfRef);

    public override int GetHashCode() => SelfRef.GetHashCode();
}
```

`Player` is **not sealed** (project convention: default to unsealed). Game code may subclass for typed re-export of authored Definition fields or game-specific helper methods; per-player-type subclassing for asymmetric games (`MageHero : Player`, `WarriorHero : Player`) is also acceptable but consider whether `PlayerDefinitionOverlay` (per-actor `PlayerDefinition` overrides) is the cleaner mechanism.

`Player` carries two identities:

- **`SelfRef : Ref<Player>`** — typed runtime identity. Allocated by `store.Catalog.AllocateRef<Player>()` at spawn. Used for entity-state slice keying, per-player slice keying, server-internal subscriptions, and any catalog lookup.
- **`Id : ActorId`** — actor identity. Used for command attribution, history envelopes, per-actor query APIs, and as a pre-existing handle the `MatchSetup.Actors` list provides.

Both refs are stable for the player's lifetime. There's at most one live `Player` instance per `Ref<Player>` in the running engine — held in `store.Catalog`.

### `PlayerDefinition`

```csharp
public abstract class PlayerDefinition : DefinitionWithSlices
{
    // Per-player zones declared at the framework level (hand, deck, discard, …).
    // The factory spawns one Zone per entry, per player actor, at Build.
    public abstract IEnumerable<ZoneDefinition> PerPlayerZones { get; }

    // Per-player non-variable slices. Default empty (inherited from
    // DefinitionWithSlices); concrete subclasses override to declare
    // game-specific per-player state.
    public override IEnumerable<BaseState> GetInitialSlices()
        => Array.Empty<BaseState>();

    // Variables (StartingLife, StartingMana, etc.) flow through the entity-bridge
    // variable bag — declared via DefinedVariables / TryGetDefaultValue on the
    // base. Concrete subclasses populate the bag with their authored defaults.
}
```

`PlayerDefinition` inherits the framework parent `DefinitionWithSlices : Scaffold.EntityDefinition, ISliceProvider` (per the open-seams pass — symmetric with `ZoneDefinition` and `CardDefinition`). Most per-player primary state is variables (in `EntityState` via the bridge); `GetInitialSlices()` is reserved for non-variable per-player state — a complex `ManaPool` record, a list of per-player abilities, custom per-player status enums.

`PerPlayerZones` is the player's layout block. The factory walks it at `Build` and spawns one `Zone` per entry per player actor (so a Magic player with `[Hand, Library, Graveyard]` gets three zones spawned at `Build`). See [Game Definition § Build walk](features/engine/archive/06-game-definition.md).

### Per-player slice-shape convention

Two shapes coexist in framework slices; this Model documents which to use when:

#### Per-player-keyed (default for substantial primary state)

```csharp
public sealed record ManaPoolState(ImmutableDictionary<ManaColor, int> Pool) : BaseState;
// installed at player.SelfRef during Build via PlayerDefinition.GetInitialSlices()
```

Read: `state.Get<ManaPoolState>(player.SelfRef).Pool[ManaColor.Red]`. Subscribe: `store.Subscribe<ManaPoolState>(player.SelfRef, ...)` — fires only on changes to *this* player's slice.

**Use when:** the slice is the player's primary game state, presentation views subscribe per-player, the slice is substantial enough that consumers want fine-grained subscription. Mirrors `EntityState` / `ZoneContentsState` precedent.

#### Singleton-with-dict (for cross-cutting indexes)

```csharp
public sealed record LeaderboardState(ImmutableDictionary<IActor, int> Scores) : BaseState;
// installed as a game-singleton via GameDefinition.GetInitialSlices()
```

Read: `state.Get<LeaderboardState>().Scores[someActor]`. Subscribe: `store.Subscribe<LeaderboardState>(...)` — fires on any player's score change.

**Use when:** the slice reads holistically across all players (presentation always renders the full leaderboard), per-player subscription granularity isn't valuable, or the data is naturally cross-cutting (a "ready-up" status, a turn-order list). Mirrors `OwnershipState` / `CardVisibilityState` precedent.

#### Convention summary

| Use case | Shape | Declared by |
|---|---|---|
| Substantial per-player game state (mana, hand limit, role status) | Per-player-keyed slice | `PlayerDefinition.GetInitialSlices()` |
| Cross-cutting index reading all players holistically (leaderboard, ready-status, turn order) | Singleton-with-dict slice | `GameDefinition.GetInitialSlices()` |
| Scalar primary stats (life, mana count, energy, poison) | Variable in `EntityState` | `PlayerDefinition` variable bag |

Authors can use either shape — neither is forbidden. The convention is a *recommended default* for the common cases.

### Mutations

Per-player state mutations follow the same patterns as other entity instances:

- **Variables** — `player.Storage.SetBase(key, value)` / `player.Storage.AddModifier(...)` / etc. Routes through the entity-bridge payloads (`SetBaseValuePayload`, `AddModifierPayload`, …). See [`features/entities/archive/02-instance.md`](features/entities/archive/02-instance.md).
- **Per-player slices** — game-defined payloads. The `PlayerDefinition`'s declared slice has at least one `Mutator` registered (game-side); `Build` validation warns if not.

Player creation flows through `Player.Spawn` (the typed wrapper around `StoreEntities.Spawn<PlayerDefinition>`):

```csharp
public class Player : InstanceWithSlices<PlayerDefinition>, IActor
{
    public static Player Spawn(Store store, PlayerDefinition def, ActorId actorId)
    {
        var (instance, r) = store.Spawn(def);
        var player = new Player(...) { SelfRef = r.Cast<Player>(), Id = actorId };
        store.Catalog.RegisterAt(player.SelfRef, player);
        return player;
    }
}
```

The factory calls `Player.Spawn` once per player actor in `MatchSetup.Actors` during the `Build` walk (per [Game Definition § Build walk](features/engine/archive/06-game-definition.md), step 2b). The `ActorId` from the actor entry in `MatchSetup` becomes the player's `Id`; `SelfRef` is freshly allocated.

Player destruction mid-match is not currently a framework primitive — most games run for the full match with a fixed actor catalog. Games that need it model it as game-side state (a "conceded" flag) rather than catalog removal.

### Read API

```csharp
// Resolve a Player from its SelfRef
var player = store.Catalog.Resolve(playerRef);

// Resolve a Player by ActorId — game-side helper, not framework-mandated.
// Games that need this build a small index slice (Dict<ActorId, Ref<Player>>)
// during setup. Pattern: PlayerByActorIndex singleton.

// Read a variable
int life = player.Storage.GetVariable<int>(Vars.Life);

// Read a per-player slice
var pool = state.Get<ManaPoolState>(player.SelfRef).Pool;

// Read the player's zones
var hand = state.Get<...>(/* the player's hand zone, resolved game-side */);
```

The framework does not provide an `ActorId → Ref<Player>` index out of the box — games that need it build their own. The deferred `bl-actor-ownership-reverse-index` (raised by Q5 of the design pass) covers a related but distinct question: reverse lookup on `OwnershipState`.

### Concrete example — Magic Standard

```csharp
public sealed class StandardPlayerDefinition : PlayerDefinition
{
    public int StartingLife    { get; init; } = 20;
    public int StartingHandSize{ get; init; } = 7;
    public int LibrarySize     { get; init; } = 60;

    public override IEnumerable<ZoneDefinition> PerPlayerZones => new ZoneDefinition[]
    {
        new HandDefinition       { Capacity = StartingHandSize },
        new LibraryDefinition    (),
        new GraveyardDefinition  (),
    };

    public override IEnumerable<Variable> DefinedVariables => new[]
    {
        Vars.Life, Vars.PoisonCounters, Vars.HandSizeLimit,
    };

    public override bool TryGetDefaultValue(Variable key, out VariableValue value)
    {
        if (key == Vars.Life)            { value = new IntVariableValue(StartingLife); return true; }
        if (key == Vars.PoisonCounters)  { value = new IntVariableValue(0);            return true; }
        if (key == Vars.HandSizeLimit)   { value = new IntVariableValue(StartingHandSize); return true; }
        value = default!;
        return false;
    }

    public override IEnumerable<BaseState> GetInitialSlices()
    {
        // Mana pool is a complex record — too rich for a single Variable, so
        // it lives in its own per-player slice keyed at player.SelfRef.
        yield return new ManaPoolState(Pool: ImmutableDictionary<ManaColor, int>.Empty);
    }
}

public sealed record ManaPoolState(ImmutableDictionary<ManaColor, int> Pool) : BaseState;
```

## Invariants

Model-local invariants. (See `players.md ## Invariants` for feature-level rules — two-ID model, Player-on-bridge, ISliceProvider adoption, non-entity-actors-as-records, structural-zones, one-Player-per-Ref.)

- **Both `Ref<Player>` and `ActorId` are stable for the player's lifetime.** The mapping is held on the `Player` handle itself; no separate slice needed.
- **`PlayerDefinition.GetInitialSlices()` defaults to empty.** Concrete subclasses override only when declaring game-specific per-player non-variable state. Most per-player primary state is variables in the bag.
- **Per-player slice-shape convention is a recommendation, not enforcement.** Per-player-keyed for substantial primary state; singleton-with-dict for cross-cutting indexes.
- **Non-entity actors (`World`, `Environment`, `Bot`, `Market`) cannot own variables or per-instance slices.** Their participation is purely as `IActor` values in framework slices like `OwnershipState`.

## Decisions

*n/a — no elevated ADR.*

The decisions that shape this Model (Player on the entity bridge, `IActor` as interface, `PlayerDefinition` adopts `ISliceProvider`, per-player slice convention, actor-aware-singletons-start-default-and-populate-via-setup-commands) were settled inline during the domain modeling design pass (2026-05-06) — captured here in the Model body and below-divider provenance, not in a per-feature ADR file. None has crossed the §4.5 two-feature scope threshold for elevation.

## Examples

See the **Concrete example — Magic Standard** sub-section under `## Contracts` for a worked-through `PlayerDefinition` showing variable bag declarations, `PerPlayerZones`, and `GetInitialSlices()` returning a per-player slice (`ManaPoolState`).

Per-player state read patterns:

```csharp
var player = store.Catalog.Resolve(playerRef);
int life   = player.Storage.GetVariable<int>(Vars.Life);
var pool   = state.Get<ManaPoolState>(player.SelfRef).Pool;
```

Cross-cutting index read pattern:

```csharp
var leaderboard = state.Get<LeaderboardState>().Scores;
int score       = leaderboard[someActor];
```

## Open follow-ups

- `bl-actor-ownership-reverse-index` — reverse lookup on `OwnershipState` (raised by Q5 of the design pass; awaiting consumer pressure).
- `ActorId → Ref<Player>` index — not framework-shipped; games that need it build a small index slice. Pin if a real game forces a framework-side helper.
- Player destruction mid-match — not a framework primitive. Games model as game-side state (conceded flag).
- Richer `IActor` surface (capabilities, role-typed dispatch) — `IActor` stays minimal until pressure forces growth.

---

*Below: provenance — this Model has no folded or elevated ADR. The Player design emerged from the domain modeling design pass (2026-05-06 session, Q1–Q7 in the session log) — Q1 (Player on entity bridge), Q2 (PlayerDefinition adopts ISliceProvider), Q3 (IActor as interface), Q4 (PerPlayerZones layout block), Q5 (deferred OwnershipState reverse index), Q6 (per-player slice-shape convention: per-player-keyed default, singleton-with-dict for cross-cutting), Q7 (actor-aware singletons start default and are populated by setup commands after StartMatch). The design pass did not produce per-feature ADRs because the decisions are tightly coupled to the entity-bridge / open-seams cascade that landed in main; the synthesis is captured in the Model body above.*

## Options considered

*n/a — purely descriptive Model. The Q1–Q7 decisions from the domain modeling design pass were captured inline during that pass; the substantive content (Player on the bridge, IActor as interface, slice conventions, etc.) lives in the Model body's `## Contracts` section. No options-considered analysis at this Model's scope to fold into provenance.*

## Rationale

*n/a — see `## Contracts` and `players.md ## Invariants`. The two-ID design (typed `Ref<Player>` + `ActorId`) and the entity-bridge participation are the load-bearing rationale at this level.*

## Consequences

*n/a — see `## Contracts` and `players.md ## Invariants`. The framework now has a baseline seated-participant subtype and a clear convention for per-player vs cross-cutting state.*
