---
type: spike
status: superseded
tags: [#spike, #domain-model, #slices, #definitions, #aggregates]
---

# Domain Model Proposal — Slices, Definitions, Aggregates

> **Status: superseded (2026-05-06).** Resolved by the domain modeling design pass; 13 questions locked and patched into the canonical Models. Locked decisions live in [`features/engine/models/06-game-definition.md`](features/engine/models/06-game-definition.md), [`features/players/models/01-player-model.md`](features/players/models/01-player-model.md), and the cascade across `entities/01`, `entities/02`, `cards/01`, `zones/01`, `engine/05`, plus glossary and ADR-0005 amendments. Key shifts from the proposal: the entities-bridge spike was resolved superseded (bridge stays); `Zone` and `Player` are on the bridge per cascade; `ISliceProvider` is parameterless and on both Definition and Instance (instance delegates to definition); `Actor` is `IActor` interface; ownership is two-tier (structural zone assignment + runtime `OwnershipState`); `MatchSetup` carries `(GameDefinition, Actors, Seed)` + three named overlays. Original proposal preserved below for historical context.

> **Original status: draft / exploratory.** Snapshot of the domain model proposal sketched during the `oq-game-definition` design pass after Q1 locked. **Not approved, not denied** — captured for iteration. Intended as the input to Stage 2 (per-kind `ISliceProvider` implementations) and Stage 3 (zone layout, `GameDefinition` shape, asset composition) of the suspended design pass.

## Context

This proposal surveys what slices each definition kind would declare under the Q1 mechanism (`ISliceProvider<TInstance>` per-spawn-keyed surface + unparameterized variant on `GameDefinition` for singletons). Scope is slices / states / aggregates only — commands, handlers, mutators, services, runtime composition are out of scope (already designed elsewhere or deferred).

**Caveat on the Definition hierarchy:** my original sketch put `EntityDefinition : Definition` (a framework `Definition` parent). After the user flagged that `EntityDefinition` is from Scaffold's package and we don't control it, the corrected hierarchy below drops the framework `Definition` parent. There's a separate open question — whether to drop the entities bridge entirely — captured in [`spikes/entities-bridge-replacement.md`](spikes/entities-bridge-replacement.md). If that lands, the hierarchy reshapes again and the slice surface changes per-kind.

## 1. Definition hierarchy

```
Scaffold.EntityDefinition  (external — provides ID, name, variable infra)
└── CardDefinition         (variable-bearing; existing)

ZoneDefinition             (framework-side — independent base, implements ISliceProvider<Zone>)
└── HandDefinition / DeckDefinition / PileDefinition / StackDefinition / BattlefieldDefinition / …

GameRulesDefinition        (framework-side — pure config knobs, independent)

GameDefinition             (framework-side — top-level bundle, independent)
```

No common framework `Definition` parent. Definition kinds share `ISliceProvider<TInstance>` only where applicable. A framework `IDefinition` marker interface is optional — purely ergonomic, not load-bearing.

```csharp
// EntityDefinition is Scaffold's; we don't redefine it.
public abstract class CardDefinition : Scaffold.EntityDefinition { }
// CardDefinition does NOT implement ISliceProvider<Card> today (no current consumer; revisit when needed).

public abstract class ZoneDefinition : ISliceProvider<Zone>
{
    public int? Capacity { get; init; }
    public InsertPosition DefaultPosition { get; init; }
    public ZoneVisibilityPolicy Visibility { get; init; }

    public virtual IEnumerable<BaseState> GetInitialSlices(Zone zone)
        => Array.Empty<BaseState>();
}

public abstract class GameRulesDefinition { /* [SerializeField] knobs */ }

public abstract class GameDefinition          // top-level bundle, sibling
{
    public abstract GameRulesDefinition           Rules        { get; }
    public abstract IReadOnlyList<CardDefinition> CardCatalog  { get; }
    public abstract IReadOnlyList<ZoneDefinition> ZoneCatalog  { get; }

    public virtual IEnumerable<BaseState> GetInitialSlices()
        => Array.Empty<BaseState>();

    // Stage 3: intrinsic zone layout block lives here.
}
```

## 2. Slice inventory

### 2.1 Framework-owned (locked — inventory only, no work this pass)

| Slice | Keying | Purpose |
|---|---|---|
| `AvailableActionsState` | singleton | snapshot |
| `EngineState` | singleton | lifecycle / running flag / current command |
| `HistoryState` | singleton | envelope log |
| `CardMapState` | singleton (holds dict) | `CardRef ↔ Card` |
| `OwnershipState` | singleton (holds dict) | `InstanceId → Actor` |
| `CardVisibilityState` | singleton (holds dict) | per-card visibility override |
| `ZoneContentsState` | per-zone | ordered `CardRef[]` |
| `EntityVariableState` | per-instance | base values + modifier stacks |

### 2.2 Game-authored — game-singleton

Owned by `GameDefinition.GetInitialSlices()` (and/or values derived from `GameRulesDefinition` knobs). Examples — each game picks its subset:

```csharp
public sealed record TurnState         (Actor Active, int Number)                 : BaseState;
public sealed record PhaseState        (PhaseId Current)                          : BaseState;
public sealed record PriorityState     (Actor? Holder)                            : BaseState;
public sealed record StackState        (ImmutableList<StackEntry> Entries)        : BaseState;   // MTG-like
public sealed record LastMoveState     (LastMove? Move)                           : BaseState;   // pile-counter
public sealed record MatchScoreState   (ImmutableDictionary<Actor, int> Scores)   : BaseState;
public sealed record InitiativeState   (ImmutableList<Actor> Order, int Cursor)   : BaseState;
```

Concrete game examples:

```csharp
public sealed class PileCounterGameDefinition : GameDefinition
{
    public override IEnumerable<BaseState> GetInitialSlices()
    {
        yield return new LastMoveState(Move: null);
    }
}

public sealed class MagicGameDefinition : GameDefinition
{
    public override IEnumerable<BaseState> GetInitialSlices()
    {
        yield return new TurnState     (Active: null!, Number: 1);
        yield return new PhaseState    (Current: ((MagicRulesDefinition)Rules).StartingPhase);
        yield return new PriorityState (Holder: null);
        yield return new StackState    (Entries: ImmutableList<StackEntry>.Empty);
    }
}
```

**Note.** `TurnState.Active` and similar actor-dependent fields are either patched by setup commands after `StartMatch` (already locked: factory returns `NotStarted`, setup runs as commands) or `GetInitialSlices()` becomes parameterized to take the actor list. **Open Q for Stage 2/3** — see §5 below.

### 2.3 Game-authored — zone-keyed

Owned by concrete `ZoneDefinition.GetInitialSlices(Zone)`.

```csharp
public sealed record PileState         (Zone Zone, int Counter, int Target)               : BaseState;
public sealed record RecipeStackState  (Zone Zone, int Progress, RecipeDef? Active)       : BaseState;
public sealed record DeckState         (Zone Zone, int RemainingShuffleSeed)              : BaseState;
public sealed record BattlefieldState  (Zone Zone, ImmutableDictionary<int,Actor?> Slots) : BaseState;
```

Concrete:

```csharp
public sealed class PileDefinition : ZoneDefinition
{
    public int Target { get; init; }

    public override IEnumerable<BaseState> GetInitialSlices(Zone zone)
    {
        yield return new PileState(Zone: zone, Counter: 0, Target: Target);
    }
}

public sealed class RecipeStackDefinition : ZoneDefinition
{
    public override IEnumerable<BaseState> GetInitialSlices(Zone zone)
    {
        yield return new RecipeStackState(Zone: zone, Progress: 0, Active: null);
    }
}
```

### 2.4 Game-authored — actor-keyed (the Player slot)

Two shapes worth weighing — both legitimate, both used by framework slices today:

**A. Singleton-with-dict** (matches `OwnershipState` / `CardVisibilityState` precedent):

```csharp
public sealed record PlayerHealthState (ImmutableDictionary<Actor,int>      Healths) : BaseState;
public sealed record ManaPoolState     (ImmutableDictionary<Actor,ManaPool> Pools)   : BaseState;
public sealed record HandSizeLimitState(ImmutableDictionary<Actor,int>      Limits)  : BaseState;
```

Reads: `state.Get<PlayerHealthState>().Healths[actor]`. Writes touch one slice — one `Updated` event per change.

**B. Per-actor-keyed** (matches `ZoneContentsState` / `EntityVariableState` precedent):

```csharp
public sealed record PlayerHealthState (Actor Actor, int Health)        : BaseState;
public sealed record ManaPoolState     (Actor Actor, ManaPool Pool)     : BaseState;
public sealed record HandSizeLimitState(Actor Actor, int Limit)         : BaseState;
```

Reads: `state.Get<PlayerHealthState>(actor).Health`. Finer subscription granularity — "subscribe to *this* player's health."

**Lean.** Per-actor-keyed (B) for substantial per-player aggregates (mana, resources, hand-limit knobs). Singleton-with-dict (A) for cross-cutting indexes that read holistically (a "score leaderboard" presentation read). Same split as framework slices already use.

**Instantiation today.** No `PlayerDefinition`, so per-actor slices come from one of:

- `GameDefinition.GetInitialSlices(IReadOnlyList<Actor> actors)` — parameterize the singleton method.
- Setup commands populate after `StartMatch` (consistent with `engine/05 §EngineStateAtReturn`).
- Reintroduce `PlayerDefinition` (revisit the deferred Q5 from the design pass).

### 2.5 Game-authored — card-keyed

None today. Cards' state surface is fully covered by the entity-variable bridge (`EntityVariableState` keyed by `InstanceId`). If a future game needs per-card non-variable game-authored slices, `CardDefinition` adopts `ISliceProvider<Card>` then — additive, no migration cost.

(If the entities bridge is dropped per [`spikes/entities-bridge-replacement.md`](spikes/entities-bridge-replacement.md), `EntityVariableState` becomes our own type but the keyed-by-InstanceId shape is preserved.)

## 3. Aggregates

`Store.RegisterAggregate` ships in Scaffold; framework hasn't pinned a use case (`state.md §Open` follow-up).

| Candidate | Reads | Status |
|---|---|---|
| `CardLocationIndex` (Card → Zone reverse) | every `ZoneContentsState` | Backlog `bl-card-location-index` — defer |
| `EffectiveValueAggregate` (per entity) | `EntityVariableState` | Already covered by per-rebuild cache in `StateEntity` (or compute-on-read if bridge drops) — no aggregate needed |
| `LegalTargetIndex` | game-specific | Game-side, not framework |
| `PlayerByTeamIndex` | `OwnershipState` + game-specific team slice | Game-side |

**Recommendation.** No framework aggregates introduced this pass. Mechanism stays available; concrete uses land per game when consumers surface. Worth one paragraph in the eventual Model: "aggregates are a tool we have, not deployed yet."

## 4. Other types

**Already typed (no work):** `Identifier<T>`, `InstanceId`, `ActorId`, `CardRef`, `Card`, `Zone`, `Actor`, `Player`, `Variable`, `VariableValue`, `VariableModifier`, `EntityModifierEntry`, `BaseState`, `IReference`. *(Some of these change shape if the entities-bridge replacement spike lands.)*

**Pseudocode (existing OPEN-ITEMS, flagged but not in this pass):** `HistoryEnvelope`, `EnvelopeCause` — `oq-history-envelope`.

**New types this proposal introduces:**

```csharp
public abstract class GameDefinition { … }              // bundle
public interface ISliceProvider<TInstance>              // mechanism (Q1 lock)
    where TInstance : IReference
{
    IEnumerable<BaseState> GetInitialSlices(TInstance instance);
}

// Supporting value types referenced by the slice examples above:
public readonly record struct PhaseId(string Value);
public sealed record StackEntry(Card Card, ResolutionData Data);
public sealed record ManaPool(ImmutableDictionary<ManaColor, int> Pool);
public sealed record LastMove(Card Card, Zone From, Zone To);
public enum ZoneVisibilityPolicy { Public, OwnerOnly, OwnerCountOnly, Hidden }
```

## 5. Open questions surfaced (for Stage 2/3)

- **Q-actor-shape.** Singleton-with-dict (A) vs per-actor-keyed (B) for per-player slices. Lean B for substantial state, A for cross-cutting indexes. Need a framework recommended default.
- **Q-actor-instantiation.** No `PlayerDefinition` today. Three paths: parameterize `GameDefinition.GetInitialSlices(actors)`, setup-command population, or reintroduce `PlayerDefinition`.
- **Q-zone-layout.** Stage 3 — how the factory gets its `(ZoneDefinition, Zone)` pairs at `Build`. Intrinsic block on `GameDefinition`, per-match overlay on `MatchSetup`, or both?
- **Q-definition-base.** No framework `Definition` base in the corrected hierarchy. Optional `IDefinition` marker interface — purely ergonomic, defer until a consumer surfaces.
- **Q-game-definition-base.** `GameDefinition` is sketched as a sibling bundle, not derived from `EntityDefinition` or any other definition kind. It aggregates other definitions vs being authored data for one instance — different semantics, different role.

## 6. Factory loop at `Build()` — pseudocode

```text
1. Auto-seed framework slices (locked: AvailableActionsState, EngineState, HistoryState, …)

2. Game-level singletons:
     foreach slice in gameDef.GetInitialSlices()
         store.Install(slice)                                  // unkeyed

3. Per-zone slices (from the zone layout — Stage 3):
     foreach (zoneDef, zone) in resolveZoneLayout(gameDef, matchSetup)
         foreach slice in zoneDef.GetInitialSlices(zone)
             store.Install(slice, key: zone)

4. Manual overlay (escape hatch — tests, scripted scenarios):
     foreach slice in matchSetup.InitialSlices ?? []
         store.Install(slice)

5. Cards: NOT seeded here. Spawned via CreateCardMutation during setup commands
   (after engine.StartMatch). Each CreateCardMutation internally calls
   cardDef.GetInitialSlices(card) and installs keyed by InstanceId.
   (Today, card slice content is the EntityVariableState seeded from BaseValues.
    No additional ISliceProvider<Card> declared — see §2.5.)
```

The `resolveZoneLayout` call is the load-bearing missing piece — Stage 3 design surface.

## 7. Sequencing dependency

If the entities-bridge replacement (separate spike) lands, this proposal's §1 hierarchy and §2.5 card-keyed section both shift slightly — `CardDefinition` no longer extends Scaffold's `EntityDefinition`, and `EntityVariableState` becomes a framework-owned type. The rest of the inventory (zone-keyed, actor-keyed, game-singleton) is unaffected.

Either:
- Resolve the bridge spike first, then come back to this proposal for Stage 2/3.
- Punt the bridge spike, take this proposal as-is (with Scaffold dependencies intact), and resume Stage 2/3.

## Related

- [`Product/SESSION-CHECKPOINT.md`](SESSION-CHECKPOINT.md) — `oq-game-definition` design pass suspended after Q1 lock; this proposal is the input to Stages 2-3.
- [`Product/spikes/entities-bridge-replacement.md`](spikes/entities-bridge-replacement.md) — sibling spike on whether to drop the entities + entities.states packages. Affects §1 hierarchy and §2.5 of this proposal if it lands.
- [`Product/features/state/state.md`](features/state/state.md) — current slice tier model (framework-owned vs game-authored).
- [`Product/features/engine/models/05-game-factory.md`](features/engine/models/05-game-factory.md) — current factory + `MatchSetup`; Q1 lock reshapes this.
- [`Product/features/zones/models/01-zone-model.md`](features/zones/models/01-zone-model.md) — `ZoneDefinition` and per-zone-type runtime slices.
- [`Product/features/cards/models/01-card-model.md`](features/cards/models/01-card-model.md) — `CardDefinition`, `CardMapState`, current entity-bridge wiring.
- [`Product/features/entities/models/01-definition.md`](features/entities/models/01-definition.md) — definitions as authored base data; entity bridge mutation conventions.
- [`Product/ledgers/GLOSSARY.md`](ledgers/GLOSSARY.md) — `Player`, `Actor`, `Zone`, `Card`, related vocabulary.
