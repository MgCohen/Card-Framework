---
type: spike
status: concluded
last-updated: 2026-05-02
---

# CCGKit Audit

Comparative study of CCGKit (shipped Asset Store framework) vs Card Framework (design system). Goals: understand architectural differences, identify learnable patterns, surface constraint-driven divergences.

---

## Executive Summary

**What they are:**
- **CCGKit:** Mature, production implementation of a CCG framework. Complete with networking, UI, game loop, serialization, and demo game. Published on Unity Asset Store.
- **Card Framework:** Markdown-driven design system specifying the architecture for a CCG framework. In design phase; no implementation yet.

**Same feature footprint:** Both decompose the problem into: entities, cards, zones, actions, effects, triggers, engine, presentation. This validates that decomposition.

**Key divergence:** CCGKit uses **ID-based reference patterns** with **concrete lists** (`List<Card>`, `List<Effect>`); Card Framework designs around **object-oriented composition** with **explicit Definition/Instance separation** and **variable-based state**.

---

## 1. Architecture & Design Maturity

### CCGKit
- **Shipped product.** Real constraints: networking, serialization, performance, Editor UI, replay. Every design choice has a backward-compatibility story.
- **Hierarchical mutable state.** `GameState` is the root; it contains `List<PlayerInfo>`, each contains `List<RuntimeCard>`, etc.
- **ID-based identity.** Every resource (Card, CardType, Stat, Zone) carries a static `currentId` counter and an instance ID. References are int-based.
- **Reflection + serialization.** Uses FullSerializer for game state save/load. Reflection-heavy attribute system for Editor UI.
- **Monolithic class hierarchy.** Large inheritance trees (e.g., `Effect` → `PlayerEffect` / `CardEffect`, with 10+ concrete subclasses each).
- **Trigger/condition system.** Triggers are abstract with subclasses per event type (OnCardEntered, OnPlayerTurnStarted, etc.). Conditions are separate filters.

### Card Framework
- **Design system, not code.** Specs the contract; no implementation pressure. Can choose patterns based on ideal architecture, not backward compat.
- **Immutable event-sourced state.** Designed around mutations/events, with snapshots and rollback (not yet specified in detail, but mentioned as planned).
- **Definition/Instance separation.** Definitions are authored data (stable IDs, shared). Instances are runtime entities (instance IDs, mutable local state). Clear boundary.
- **Variable-based state.** No hardcoded fields like `attack` or `health`. Instead, `VariableKey → VariableValue` bags. Inspired by Scaffold Entities.
- **Composition over inheritance.** Effects and triggers described as behavior specs, not an inheritance forest. Details to be filled in during implementation.
- **Modifiers as first-class.** Value calculation is **layered**: base + instance override + local modifiers + attached + external + context. Debuggable.

---

## 2. Entity Model

### CCGKit's Approach
```csharp
// Unified Card class
public class Card : Resource {
    public int cardTypeId;           // Reference to definition
    public List<Cost> costs;         // Runtime state
    public List<Stat> stats;         // Runtime state
    public List<Property> properties; // Runtime state
    public List<RuntimeKeyword> keywords;
    public List<Ability> abilities;
}

// Mutable from the start
card.stats[0].value = 5;  // Direct mutation
```

**Advantages:**
- Simple to reason about at a glance.
- Easy to serialize (flat, known structure).
- Fast random access (array of objects).

**Constraints:**
- Definition and instance state are mixed. Hard to reset a card to definition state.
- No natural way to attach temporary modifiers without mutating the card.
- Adding a new stat type requires editing the Card class (not extensible).

### Card Framework's Approach
```csharp
// Separation at the design level
Definition {
    definition_id: stable,
    display_name: string,
    base_variables: { cost: 3, attack: 2, health: 5 }
}

Instance {
    instance_id: unique,
    definition_id: reference,
    variable_overrides: { health: 3 },  // Only changed values
    modifiers: [
        { source: "poison", health: -1 },
        { source: "rage", attack: +2 }
    ],
    attachments: [ /* other instances */ ]
}

// Effective value calculation
Effective(instance, "attack") = base(2) + override(0) + modifiers(+2) = 4
```

**Advantages:**
- Definition is immutable authored data.
- Modifiers are inspectable; you know exactly where every +1 comes from.
- Instances don't duplicate definition fields.
- Supports snapshot rollback (save Definition, discard Instance mods).
- Natural support for attachments as separate instances with their own state.

**Constraints:**
- Slightly more indirection to compute effective values.
- Requires agreement on "modifier resolution order" (still an open design question in Card Framework).

---

## 3. Ability & Effect System

### CCGKit
```csharp
public class Ability {
    public string name;
    public AbilityType type;        // Triggered or Activated
    public Effect effect;
    public Target target;
}

// Triggered abilities bind a Trigger
public class TriggeredAbility : Ability {
    public Trigger trigger;
}

// Effects are an inheritance forest
public abstract class Effect { }
public class PlayerEffect : Effect { }
public class CardEffect : Effect { }
// ... with 10+ concrete subclasses per category

// Conditions are separate
public abstract class Condition { }
public class PlayerStatCondition : Condition { }
```

**Pattern:** Ability = (Trigger) + Effect + Target. Each component is a class hierarchy.

### Card Framework
```
Ability {
    kind: triggered | activated
    trigger: Trigger Spec
    effect: Effect Spec
    target: Target Spec
    optional_costs: Cost[]
    zone_restrictions: []
}

Effect {
    kind: text | reference to defined effect
    modifiers: Modifier[]
    targets: [ player | card | zone | ... ]
}

Trigger {
    event: card_entered_zone | player_stat_changed | turn_started
    conditions: [ condition1, condition2 ]
}
```

**Pattern:** Behavioral specs, not code hierarchy. Designed to be authored (YAML, JSON, or UI-driven).

**Comparison:**
- CCGKit: Add a new ability type? Subclass `Ability`. Add a new effect? Subclass `Effect`. Tight coupling.
- Card Framework: New ability type? Add a new Trigger event type and compose. New effect? Reference a newly defined effect in a database. Looser coupling.

---

## 4. State & Networking

### CCGKit
- **Mutable, centralized GameState.** The server holds the source of truth.
- **Network replication.** `EffectSolverHandler`, `TurnSequenceHandler`, `NetCard`, `NetZone` marshal mutations over the wire.
- **Full game-state serialize/deserialize.** Uses FullSerializer to package state for clients.
- **No explicit rollback mechanism.** State mutations are forward-only.

**Reality:** Networking adds complexity; every state change must be ordered and communicated.

### Card Framework
- **Event-sourced immutable state.** Planned, but not yet specified.
- **Mutations are events/commands.** Each mutation (CreateCard, MoveToDeck, ApplyModifier) is a self-contained message.
- **Snapshots for efficiency.** Store periodic snapshots; replay events from last snapshot on restore.
- **Natural rollback.** Load old snapshot, discard mutations.

**Planned but incomplete:** The exact command/mutation vocabulary is still being designed.

---

## 5. Feature Decomposition: Aligned & Divergent

Both projects break the problem into the same logical features:

| Feature | CCGKit | Card Framework |
|---------|--------|----------------|
| **Entities** | Card, PlayerInfo, Token | Definition, Instance, Variable |
| **Cards** | Card (list of costs/stats/abilities) | Card Definition + Card Instance |
| **Zones** | GameZoneType (with owner, visibility) | Zone Definition + Zone Instance |
| **Actions** | GameActions (ShuffleCards, DrawCard) | Command/Mutation pattern |
| **Effects** | Effect hierarchy (PlayerEffect, CardEffect) | Effect Spec + Effect application |
| **Triggers** | Trigger hierarchy (OnCardEntered, OnStatChanged) | Trigger Spec + Event model |
| **Engine** | EffectSolver (Resolve, AreTargetsAvailable) | Mutation resolver + effect evaluator |
| **Presentation** | CardView, BoardCreature, animations | Presentation contract (TBD) |

**Insight:** The feature decomposition is **not arbitrary**. Both teams arrived at the same boundaries, suggesting they're natural to the problem domain.

---

## 6. Differences Caused by Project Constraints

### CCGKit's Constraints (Implementation Pressure)
1. **Networking.** Every state change must be communicable. ID-based references are simpler to network than object graphs.
2. **Save/load.** Full game state must survive serialization. FullSerializer handles it, but requires concrete classes and known types.
3. **Editor authoring.** Card abilities are authored in the Inspector. Reflection-based attribute system makes this tractable.
4. **Performance.** Lists of pre-allocated objects; fast iteration.
5. **Backward compat.** Published on Asset Store; can't break existing games.

### Card Framework's Freedoms (Design Phase)
1. **No networking yet.** Can use pure OOP without ID indirection.
2. **No serialization debt.** Can spec the contract and choose serialization later (e.g., Scaffold Entities, ECS, event sourcing).
3. **No Editor UI yet.** Can design for code/config; Inspector tooling comes later.
4. **Can rethink from scratch.** No shipping customers; can choose the "right" architecture.
5. **Immutability is free.** No cost to saying "snapshots and rollback" if you're not implementing yet.

---

## 7. What They're Doing Well

### CCGKit
1. **Clear separation of zones and their visibility rules.** The `ZoneOwner` / `ZoneType` / `ZoneOwnerVisibility` / `ZoneOpponentVisibility` model is elegant and covers the common cases (deck, hand, board, discard, public/private).
2. **Trigger + Effect + Target composability.** Despite the inheritance, the three-part model (what causes → what happens → where) is sound.
3. **Networking infrastructure.** Handler pattern for TurnSequence, Combat, EffectSolving shows how to decompose a turn into serializable transactions.
4. **Working demo.** Shipped game with UI, multiplayer, economy. Proves the architecture isn't just theory.
5. **Extensibility points.** Custom Triggers, Effects, and Conditions are straightforward to add without modifying core.

### Card Framework
1. **Definition/Instance boundary.** Explicitly separates authored data from runtime state. Enables modifiers, attachments, and snapshots as first-class citizens.
2. **Variable-based flexibility.** No hardcoded `attack`, `health`, `energy` fields. A game can define its own stat schema. Supports diverse game types (structured CCG, roguelike deckbuilder, spatial sim).
3. **Immutable state design.** Snapshot + replay gives you free replay, debugging, undo, and time travel. CCGKit doesn't have this.
4. **Modifier explicitness.** Layered modifier calculation (base + override + local + attached + external + context) is debuggable. A tool can show "why is this card's attack 7?".
5. **Composition over inheritance.** No Effect hierarchy forest. Behavior is data + a resolver, not a class per effect type.

---

## 8. Cross-Learning: What Each Can Learn

### Card Framework Can Learn From CCGKit
1. **Zone visibility is complex.** CCGKit nails the distinction between owner visibility, opponent visibility, zone type (static/dynamic), zone owner (player/shared). Card Framework's zone spec should adopt this vocabulary exactly.
2. **Networking is non-optional.** Design for it from day one, even if implementation is deferred. ID-based references are simpler to network; don't wait until you've built a pure OOP graph.
3. **Replay isn't free.** Event sourcing sounds good in theory. CCGKit learned that every mutation must be ordered, deterministic, and communicable. The command/mutation vocabulary can't be an afterthought.
4. **Testing needs attention.** A framework ships with test suites (or should). Card Framework should spec how a game validates rules, not leave it to implementation.

### CCGKit Can Learn From Card Framework (For a v2)
1. **Separate Definition from Instance.** Right now, Card mixes both. Future versions could have an immutable CardDefinition and a mutable CardInstance with only local changes. This would unlock snapshots and clean reset.
2. **Make modifiers explicit.** Instead of mutating `card.stats[0].value`, keep a `List<Modifier>` and calculate effective values on read. Debugging becomes much easier ("show me all modifiers on this card").
3. **Prefer composition to inheritance.** Instead of an Effect class hierarchy, have an Effect data type (with kind, targets, modifiers) and a single Resolver. Add new effects without touching the framework.
4. **Design for event sourcing.** Store mutations as events, not mutable state. This gives you replay, undo, and debugging for free. Network comm becomes trivial (send events, not state).
5. **Zone movement preserves instance identity.** Right now, it's unclear whether a card in hand is the same object as when it was in deck. Spec it. This unlocks "attach a modifier to a card in deck and it persists when drawn".

---

## 9. Specific Insights

### ID-Based References vs. Object References
- **CCGKit:** Uses int IDs (`cardTypeId`, `statId`, `zoneId`). Why? Networking, serialization, determinism. You can't send an object reference over the wire.
- **Card Framework:** Designs around object references (`Definition<Card>`, `Instance<Card>`). Why? OOP is simpler to reason about in code. The cost of indirection is deferred.
- **Tradeoff:** If Card Framework ever does networking/saving, it may need to add ID layers. This is a debt that materializes at implementation time.

### Immutable vs. Mutable State
- **CCGKit:** Mutable. Cards are objects that change. Turns happen; state advances.
- **Card Framework:** Immutable (planned). Each mutation produces a new snapshot. Old snapshots are kept for replay.
- **Tradeoff:** CCGKit is simpler operationally (no snapshot cost). Card Framework is better for replay, undo, and debugging. Only evidence at scale would show which is faster.

### Effects as Code vs. Effects as Data
- **CCGKit:** Effect subclasses in C#. New effect type = new class. Tight coupling to the framework language.
- **Card Framework:** Effects as data (specs in YAML/JSON/UI). Resolver interprets them. New effect type = new entry in a config.
- **Tradeoff:** CCGKit is more flexible for complex effects (full C# logic). Card Framework is better for content creators (no coding). Most games need both (hardcoded "special" effects + content-defined effects).

### Variable Names as Strings vs. Strong Typing
- **CCGKit:** `GetIntProperty("cost")`, `GetStringProperty("name")`. String keys, weak typing.
- **Card Framework:** `VariableKey` is a type. Schema defines available keys and their types upfront.
- **Tradeoff:** Card Framework's approach catches typos and is more refactorable. But it's less flexible for one-off custom variables. A practical implementation would offer both (enum-based for common keys, string-based for custom).

---

## 10. Questions for Card Framework

1. **Modifier resolution order.** When multiple modifiers apply to the same variable (e.g., attack), in what order are they summed? Does it matter? (CCGKit sidesteps this by not having modifiers.)
2. **Attachments and detachment.** When an attached instance is removed, do its modifiers vanish immediately or at end-of-turn? (CCGKit doesn't have attachments as instances; equipment is part of a card definition.)
3. **Snapshot rollback boundaries.** Can you snapshot in the middle of a mutation sequence? Or only at safe points (e.g., end of turn)? (CCGKit doesn't do snapshots; this is pure design.)
4. **Determinism and replay.** If you serialize and replay mutations, do you guarantee the same game state? Or are there sources of entropy (e.g., random choices)? (CCGKit uses reflection-based replay; Card Framework hasn't specified.)

---

## 11. Recommendations

### For Card Framework Implementation
1. **Adopt CCGKit's zone visibility model verbatim.** It works; don't redesign.
2. **Early decision on Definition/Instance persistence.** How are definitions stored? Hot-reloaded from YAML? Compiled into assets? This affects the whole architecture.
3. **Spec the command/mutation vocabulary early.** Even if you don't implement it, write down: "CreateCard(defId, ownerId, zone)", "MoveToDeck(instanceId, position)", etc. Test that these cover all game actions.
4. **Plan for content-creator effects.** Don't assume all effects are code. Spec a simple data-driven effect format (e.g., "modify stat X by amount Y"). Make complex effects subclass from that.
5. **Add a networking chapter.** Even if you're not building networking yet, note "IDs must be deterministic and referenceable across the network." This prevents a redesign later.

### For CCGKit Users / Future Maintainers
1. **Consider a Definition/Instance refactor for v2.** It's a breaking change, but unlocks modifiers, snapshots, and cleaner reset logic.
2. **Make modifiers explicit.** Move from `card.stats[i].value = X` to `card.modifiers.Add(new StatModifier(...))`. Calculate effective value on read.
3. **Event-source the server.** Store mutations, not state. This buys you replay and debugging for free.
4. **Invest in authoring tools.** Effects are data, not code. Build a UI so content creators don't need to touch C#.

---

## 13. Zone Control & Visibility

### CCGKit's Zone Model
```csharp
public class GameZoneType : Resource {
    public ZoneOwner owner;                    // Player or Shared
    public ZoneType type;                      // Static or Dynamic
    public ZoneOwnerVisibility ownerVisibility;  // Visible or Hidden
    public ZoneOpponentVisibility opponentVisibility;
    public bool hasMaxSize;
    public int maxSize;
}
```

**Approach:** Zones are defined by ownership, type, and visibility enums. A `Deck` is Static-Player-Hidden. A `Board` is Dynamic-Shared-Visible.

**Strength:** Simple, covers the common cases, and is declarative.

**Weakness:** Assumes a 2-player model. Adding a third player would require rethinking visibility (enum can't scale). Zone-specific behavior (e.g., "shuffle on draw") is defined in the handler layer, not the zone.

### Card Framework's Zone Model
```csharp
Zone = handle (id + definition) + contents slice + per-zone-type slice

Zone Definition:
  - capacity/sizing rules
  - insertion behavior (top, bottom, specific slot)
  - removal behavior (close gap, unstack)
  - visibility policy (by actor/role)
  - change bookkeeping (what events fire)

Zone Runtime:
  - ZoneContentsState: ordered ImmutableList<CardRef>
  - OwnershipState: Dictionary<InstanceId, Actor>
  - Per-zone-type slices: game-authored values (counter, recipe progress)
```

**Approach:** Zones are data (definition) + slices (state). Visibility is a method, not an enum. Zone organization is customizable without class hierarchy.

**Strength:** 
- Visibility policy can be complex (per-actor, per-role, context-dependent).
- Zones can scale beyond 2 players.
- Per-zone-type state (recipe progress, counter) lives in separate slices, not mixed into the zone.

**Weakness:** More indirection to query; requires explicit slice reading.

### Key Difference
- **CCGKit:** Zone ownership/visibility is an enum, fast to reason about, but limited.
- **Card Framework:** Zone is thin handle + state slices, more flexible but requires explicit state reads.

**Cross-learning:** CCGKit's enum-based visibility is elegant for 2-player games. Card Framework's visibility-as-method scales to N players. If CCGKit ever supports 3+ players, it would need to adopt a policy/method-based approach.

---

## 14. Card Definitions & Instances

### CCGKit's Card Model
```csharp
// No explicit distinction at runtime
public class Card : Resource {
    public int cardTypeId;              // Reference to library
    public List<Cost> costs;            // Mutable, per-instance
    public List<Stat> stats;            // Mutable, per-instance
    public List<Property> properties;   // Mutable, per-instance
    public List<RuntimeKeyword> keywords;
    public List<Ability> abilities;     // Mutable, per-instance
}

// Definition is stored separately (GameConfiguration.cardTypes)
public class GameConfiguration {
    public List<CardType> cardTypes;    // The authored library
    public List<Card> cards;             // Runtime instances
}
```

**Relationship:** A `Card` has a `cardTypeId` pointing to a `CardType` in the library. At runtime, the Card mixes definition and instance state.

**Strength:** Simple; everything is in one object. Easy to serialize.

**Weakness:** Can't reset a card to definition state without re-querying the library. Modifiers are mutations (`card.stats[0].value -= 1`), not composable.

### Card Framework's Card Model
```csharp
// Explicit separation
CardDefinition {
    definition_id: stable,
    name: string,
    base_costs: Cost[],
    base_stats: Stat[],
    keywords: Keyword[],
    abilities: Ability[],
}

CardInstance {
    instance_id: unique,
    definition_id: reference,
    current_zone: Zone,
    variable_overrides: Dict<VarKey, VarValue>,
    modifiers: Modifier[],
    attachments: InstanceId[],  // other instances attached to this
    visibility_state: VisibilityData,
    lifecycle_state: LifecycleState,
}

// Calculate effective values
GetStat(instance, "attack") =
    base(definition.stats["attack"]) 
    + override(instance.variable_overrides["attack"]) 
    + sum(modifiers where key=="attack")
    + sum(attachments.modifiers)
```

**Relationship:** A `CardInstance` holds a reference to `CardDefinition` and only stores local changes.

**Strength:**
- Definition is immutable and shared.
- Modifiers are inspectable.
- Can snapshot and rollback to last definition state.
- Attachments are instances with their own modifiers.

**Weakness:** More indirection; need to understand modifier ordering.

### Key Difference
| Aspect | CCGKit | Card Framework |
|--------|--------|----------------|
| **Reset to definition** | Re-query library | Load definition, discard overrides |
| **Add temporary modifier** | Mutate stats directly | Add Modifier; recalculate on read |
| **Inspect "why is attack 5?"** | Hard; woven into stats | Easy; list modifiers + calculate |
| **Attachments** | Not supported as instances | First-class instances with modifiers |
| **Snapshot** | Serialize entire Card | Discard instance state; keep definition |

---

## 15. Action Mapping & Intent Pipeline

### CCGKit's Action System
```csharp
// Game actions are abstract
public abstract class GameAction {
    public virtual void Resolve(GameState state, PlayerInfo player) { }
    public virtual void Resolve(GameState state, RuntimeCard card) { }
}

// Concrete actions:
public class DecreasePlayerStatAction : GameAction {
    public int statId;
    public Value value;
    public override void Resolve(GameState state, PlayerInfo player) {
        player.stats[statId].baseValue -= value.GetValue(state, player);
    }
}

public class MoveCardsAction : GameAction {
    public List<int> cardIds;
    public int fromZoneId;
    public int toZoneId;
    public override void Resolve(GameState state) { ... }
}
```

**Pipeline:** UI → input handler → match to action → resolve.

**Flow:**
1. Player clicks "play card."
2. Handler looks up playable actions for this card.
3. Calls `action.Resolve(state, card)`.
4. Action mutates state directly.

**Strength:** Simple dispatch; action knows how to apply itself.

**Weakness:** No validation layer before resolution. No history/undo. Hard to hook middleware (e.g., "intercept cost increases").

### Card Framework's Command Pipeline
```csharp
PlayerIntent (raw request)
  → IntentMapper (resolve actor, zone, context)
  → GameCommand (typed, high-level)
  → Pipeline.Validate (handler.Validate)
  → Pre-hooks / Before-trigger fold (cancel, replace, modify)
  → Handler.Execute (apply state mutations)
  → Post-hooks / After-trigger fold (reactions, follow-ups)
  → History envelope (grouped facts)
  → Evaluator.Evaluate (what's next?)
  → AvailableActions snapshot

Example:
PlayCardIntent {
    actor: Player1,
    instance_id: card123,
    target_zone: Battlefield
}
↓
PlayCardCommand {
    actor: Player1,
    card_instance: card123,
    target: Battlefield
}
↓ Validate (check hand legality, can afford cost)
↓ Before-triggers (apply cost modifiers, check prevention)
↓ Handler (apply cost, move to battlefield)
↓ After-triggers (trigger card abilities, enemy reactions)
↓ History envelope
↓ Evaluate (stack auto-resolves? next action?)
```

**Layers:**
1. **PlayerIntent:** What the actor *attempted*.
2. **GameCommand:** What the rules *approve*.
3. **StateMutation:** Approved fact to be *applied*.
4. **CommandResult:** What *actually happened*.
5. **History:** Ordered *facts* for replay/debug.

**Strength:**
- Clear validation boundary (handler decides legality).
- Hooks for interception (cost modifiers, prevention).
- History is first-class (replay, undo, debug).
- Evaluator makes state-driven decisions (vs. hardcoded action order).

**Weakness:** More moving parts; harder to debug a specific issue without understanding the full pipeline.

### Key Difference
| Aspect | CCGKit | Card Framework |
|--------|--------|----------------|
| **Validation** | In handler (if at all) | Dedicated validate layer |
| **Hooks** | No pre/post hooks | Before-trigger, After-trigger folds |
| **History** | No; state mutates in place | Yes; history envelope with causality |
| **Rejection** | Action fails silently | Command rejected; reason provided |
| **Replay** | No built-in support | Yes; replay history envelope |
| **Undo** | Not supported | Yes; rollback to snapshot |

---

## 16. Ordering, Validation, & The Pipeline

### CCGKit's Validation
```csharp
// No global validation layer. Each action may or may not check legality.
public class DecreasePlayerStatAction : GameAction {
    public override void Resolve(GameState state, PlayerInfo player) {
        player.stats[statId].baseValue -= value.GetValue(state, player);
        // No check: is this card playable? Can the player afford it?
    }
}

// Validation happens in the UI or in-handler
public abstract class ServerHandler : MonoBehaviour {
    // Some handlers may do validation; others don't
    public virtual void OnMessageReceived(NetworkMessage msg) { }
}
```

**Reality:** Validation is scattered. Some handlers check; others don't. The framework doesn't enforce a gate.

**Ordering:** Actions resolve in the order they're dispatched. No automatic trigger ordering or stack.

### Card Framework's Validation (4 Layers)
```csharp
// Layer 1: Intent-to-Command mapping
IntentMapper.FromPlayerIntent(intent)
    → resolve actor, zone, context
    → build strongly-typed GameCommand

// Layer 2: Pre-validation (can this be dispatched?)
GameRules.OnIncomingCommand(command)
    → is game paused? 
    → is this actor's priority? 
    → route to queue, stash, or discard

// Layer 3: Handler validation (is this legal?)
Handler.Validate(command, state)
    → check: can afford cost?
    → check: target exists?
    → check: no prevention active?
    → return rejection reason if invalid

// Layer 4: Before-trigger fold (can this be modified/cancelled?)
ForEachTrigger(OnBefore<PlayCard>)
    → cost increase? → modify command payload
    → prevention? → cancel command
    → redirect? → replace target

Then: Handler executes (now-approved command)
Then: After-trigger fold (reactions)
```

**Ordering:** Automatic via the engine loop + trigger subscription.

### CCGKit doesn't have a global ordering system. Triggers are instance-based; there's no "stack" to coordinate multiple reactions.

### Card Framework's Ordering
```csharp
// Engine loop handles ordering
GameRules.Evaluate(action, result)
    → Check state: does shield break?
    → Check state: is game won?
    → Return follow-up commands to enqueue

// Trigger ordering via FilterTriggers
GameRules.FilterTriggers(candidates, command, phase)
    → Return sorted/filtered TriggerMatch list
    → Engine executes in order

// Trigger phases: BeforeResolve, AfterResolve, etc.
// (prevents ambiguity about when triggers fire)
```

**Key insight:** Card Framework uses **state-driven follow-ups** (Evaluate) rather than implicit reaction chains. This is more explicit and debuggable.

### Key Difference
| Aspect | CCGKit | Card Framework |
|--------|--------|----------------|
| **Validation gates** | Ad-hoc, per-handler | 4 layers: intent → command → handler → trigger fold |
| **Rejection handling** | Fails silently or logs | Explicit reason; caller decides |
| **Trigger ordering** | Instance-based (ad-hoc) | Subscription + FilterTriggers |
| **Stack** | No explicit stack | Enqueued follow-ups + Evaluate loop |
| **Determinism** | Depends on execution order | Deterministic; engine controls order |

---

## 17. Phase System & Turn Structure

### CCGKit's Turn/Phase Model
```csharp
// Implicit turn structure. Server handles turn transitions.
public class TurnSequenceHandler : ServerHandler {
    public override void RegisterNetworkHandlers() {
        NetworkServer.RegisterHandler<StopTurnMessage>(OnStopTurn);
    }

    protected virtual void OnStopTurn(NetworkConnection conn, StopTurnMessage msg) {
        server.StopTurn();  // Server-side turn end
    }
}

// Turn duration is a timer, not explicit game state
public class GameManager : MonoBehaviour {
    public float turnDuration = 30f;  // seconds
}

// Phases are implicit in the game logic
// (no explicit phase enum; it's woven into handlers)
```

**Reality:** No explicit phase system. Turn ends when timer expires or player clicks "end turn." What happens during a turn is hardcoded in handlers.

**Strength:** Simple; no state machine complexity.

**Weakness:** Hard to add new phases (e.g., "combat phase," "cleanup phase") without refactoring. No way to block/unblock commands per phase.

### Card Framework's Phase & Idle Hook Model
```csharp
// Game-authored enum; framework doesn't define phases
public enum GamePhase {
    Setup,
    DrawPhase,
    MainPhase,
    CombatPhase,
    ResolutionPhase,
    EndPhase
}

// Phases live in game state
public class GameState {
    public GamePhase CurrentPhase { get; set; }
    public Actor CurrentActor { get; set; }
}

// GameRules controls phase transitions via OnQueueDrained
public abstract class GameRules {
    public abstract DrainResult OnQueueDrained() {
        // Queue is empty. What now?
        // Options:
        // 1. Advance phase
        // 2. Grant priority to a player
        // 3. Run automatic trigger (end-of-turn cleanup)
        // 4. Publish snapshot + await input
        
        return new DrainResult(
            dispatch: commands_to_run,
            snapshot: available_actions
        );
    }
}

// Legality is phase-aware
public abstract class GameRules {
    public abstract IReadOnlyList<GameCommand> FilterAvailable(GamePhase phase) {
        // Return only commands legal in this phase
        // Example: PlayCard only legal in MainPhase
    }
}
```

**Approach:** Phases are explicit game state. `OnQueueDrained` is the hook where the game decides what happens next.

**Strength:**
- Phases are explicit and queryable.
- Easy to block commands per phase.
- Clean separation: engine drains queue, game decides next phase.

**Weakness:** Requires game to implement `OnQueueDrained` correctly (if it's wrong, game logic is wrong).

### Key Difference
| Aspect | CCGKit | Card Framework |
|--------|--------|----------------|
| **Phase definition** | Implicit (woven into handlers) | Explicit game state |
| **Phase transitions** | Timer or handler decision | GameRules.OnQueueDrained |
| **Command blocking** | Hard-coded in handlers | FilterAvailable per phase |
| **Turn timer** | Global `turnDuration` | Custom in GameRules |
| **Idle state** | Awaiting network message | OnQueueDrained + snapshot publish |

---

## 18. Command Patterns & Architecture

### CCGKit's Command Pattern (Implicit)
```csharp
// No explicit "command" type. Handlers are the de facto commands.
// Network message → Handler → GameAction → Resolve

public abstract class GameAction {
    public virtual void Resolve(GameState state, ...) { }
}

// Ordering is implicit in the demo
// Example flow:
// 1. Player submits "play card"
// 2. Handler.OnPlayCard calls card.ability.Resolve
// 3. Ability returns an Effect
// 4. Effect is applied immediately
// 5. Next handler runs
```

**Reality:** No explicit command queue. Handlers run as they arrive (network message) or are called synchronously (UI).

**Ordering:** Actions are resolved synchronously; no queuing.

### Card Framework's Command Pattern (Explicit)
```csharp
// Explicit GameCommand type
public abstract record GameCommand {
    public required ActorId Actor { get; init; }
    public required OriginId OriginCommandId { get; init; }
}

public sealed record PlayCardCommand : GameCommand {
    public required CardRef Card { get; init; }
    public required Zone TargetZone { get; init; }
    public IReadOnlyList<IReference> Targets { get; init; }
}

// Handler implements validation + execution
public sealed class PlayCardHandler : ICommandHandler<PlayCardCommand> {
    public bool Validate(PlayCardCommand cmd, IStateScope state, out string? reason) {
        // Check legality
    }

    public CommandResult Handle(PlayCardCommand cmd, IStateScope state) {
        // Execute; return result
    }
}

// Engine manages the queue
public sealed class GameEngine {
    private readonly Queue<GameAction> pending = new();

    public void Dispatch(GameCommand command) {
        // Validate
        // Enqueue (don't run immediately)
    }

    private void Drain() {
        while (pending.Count > 0) {
            var action = pending.Dequeue();
            var result = pipeline.Execute(action);
            var followUps = rules.Evaluate(action, result);
            foreach (var next in followUps)
                pending.Enqueue(next);
        }
    }
}
```

**Approach:** Commands are explicit, queued, and processed in order.

**Strength:**
- Commands are replayable (each is a fact).
- Deterministic ordering.
- Easy to debug ("what commands ran in what order?").

**Weakness:** Requires discipline; if GameRules.Evaluate is wrong, the command chain breaks.

### Key Difference
| Aspect | CCGKit | Card Framework |
|--------|--------|----------------|
| **Command type** | Implicit (GameAction) | Explicit (record types) |
| **Queueing** | No; runs synchronously | Yes; explicit queue |
| **Ordering guarantee** | Implicit in handler order | Explicit engine.Drain() |
| **Replay** | No | Yes; command sequence is deterministic |
| **Debugging** | Hard; state is mutated | Easy; command + result logged |

---

## 19. Engine Architecture: Engine → Rules → Command Pipeline Flow

### CCGKit's Flow
```
Player Input (UI)
  ↓
GameManager / Handler (network or local)
  ↓
Match to GameAction subclass
  ↓
Action.Resolve(state)
  ↓
State mutates directly
  ↓
Next action or await input
```

**Characteristics:**
- Imperative: "do this now."
- Synchronous: actions run immediately.
- State mutation is direct (no intermediary).
- No explicit queue.
- Handlers know about game state (tight coupling).

### Card Framework's Flow
```
Player Input / System / Trigger
  ↓
IntentMapper.FromPlayerIntent(intent)
  ↓
GameCommand (typed, high-level)
  ↓
GameEngine.Dispatch(command)
  ↓
Validate? → GameRules.OnIncomingCommand(origin)
  ↓ (enqueue if accepted)
Drain Loop (when idle):
  ├─ Dequeue command
  ├─ Pipeline.Execute(command):
  │   ├─ Handler.Validate(command, state)
  │   ├─ Before-trigger fold (cancel/modify)
  │   ├─ Handler.Execute(command) → StateMutations
  │   ├─ After-trigger fold (reactions)
  │   ├─ History envelope
  │
  ├─ GameRules.Evaluate(command, result)
  │   → return follow-up commands
  │
  └─ Enqueue follow-ups
    
When empty:
  ↓
GameRules.OnQueueDrained()
  ├─ Phase logic
  ├─ Snapshot publication
  ├─ Return any follow-ups
```

**Characteristics:**
- Declarative: "queue this; I'll run when ready."
- Asynchronous: commands are queued; engine drains when idle.
- State mutation is mediated (StateMutation is approved before apply).
- Explicit queue + loop.
- GameRules is a service layer, not in the handler.

### Key Architectural Difference
| Aspect | CCGKit | Card Framework |
|--------|--------|----------------|
| **Control flow** | Imperative (direct call) | Declarative (event queue) |
| **State mutations** | Direct mutation | Approved → StateMutation → apply |
| **Middleware** | No explicit hooks | Before-trigger, After-trigger folds |
| **Queueing** | No; implicit FIFO | Yes; explicit GameEngine queue |
| **Game logic** | In handlers | In GameRules (separate service) |
| **Replay** | Not supported | Yes; command sequence is log |
| **Phase transitions** | Implicit in handlers | Explicit in GameRules.OnQueueDrained |
| **Determinism** | Depends on handler order | Guaranteed by engine.Drain() order |

---

## 20. UI/Simulation Boundary

### CCGKit's Boundary
```csharp
// Loose separation. UI reads game state directly.

public class CardView : MonoBehaviour {
    public void PopulateWithInfo(RuntimeCard card) {
        // Read card state directly
        var libraryCard = gameConfig.GetCard(card.cardId);
        nameText.text = libraryCard.name;
        costText.text = libraryCard.costs[0].value.ToString();
    }
}

public class GameScreen : MonoBehaviour {
    public void Update() {
        // Poll game state and update UI
        hand = gameState.players[0].hand;
        foreach (var card in hand) {
            cardViews[i].PopulateWithInfo(card);
        }
    }
}

// Input is direct
public class HandCard : MonoBehaviour {
    public void OnPointerDown() {
        // Directly call handler
        gameManager.PlayCard(card);
    }
}
```

**Characteristics:**
- UI reads `GameState` directly (no snapshot).
- Input is imperative (direct method call).
- UI updates are polling-based (Update loop).
- No explicit event stream.

**Strength:** Simple; no adapter layer.

**Weakness:** 
- UI can read stale state (polling lag).
- Hard to show "legal actions" (UI doesn't know what's valid).
- Tight coupling: UI depends on GameState shape.

### Card Framework's Boundary
```csharp
// Explicit snapshot-based boundary

// Framework provides AvailableActions snapshot
public interface IAvailableActions {
    IReadOnlyList<AvailableAction> CurrentActions { get; }
    IReadOnlyDictionary<Identifier, EntityView> Entities { get; }
}

// Game implements presentation layer
public sealed class GamePresentation {
    private readonly IAvailableActions snapshot;
    private readonly IHistoryObserver historyObserver;

    public void OnSnapshotUpdated(IAvailableActions newSnapshot) {
        // Render snapshot (not GameState)
        foreach (var action in newSnapshot.CurrentActions) {
            button.SetActive(true);  // Show legal card
        }
    }

    public void OnHistoryEvent(HistoryEntry entry) {
        // React to facts (card moved, variable changed)
        // Trigger animations, sounds, etc.
    }
}

// Input is deferred
public sealed class CardButton : MonoBehaviour {
    public void OnClick() {
        // Submit intent; let engine decide
        gateway.Submit(new PlayCardIntent(card, targetZone));
    }
}
```

**Characteristics:**
- Presentation reads `IAvailableActions` snapshot (not GameState).
- Snapshot is published by GameRules after queue drains.
- Input submits PlayerIntent; engine validates.
- UI is reactive to history events (not polling).

**Strength:**
- UI always has fresh, correct state.
- UI only shows legal actions.
- UI is loosely coupled (snapshot, not GameState).
- Easy to replay (history events are the log).

**Weakness:** Extra layer; need snapshot publishing discipline.

### Key Boundary Differences
| Aspect | CCGKit | Card Framework |
|--------|--------|----------------|
| **What UI reads** | GameState (direct) | IAvailableActions snapshot |
| **Input handling** | Direct method call | Submit PlayerIntent |
| **Update style** | Polling (Update loop) | Event-driven (history events) |
| **Legal actions** | UI guesses (hard-coded) | Snapshot lists them (from engine) |
| **Coupling** | Tight (UI depends on GameState) | Loose (UI depends on snapshot interface) |
| **Replay** | No; state mutates in place | Yes; replay history events |
| **Latency** | Real-time but may be stale | One frame delay (queued → drained) |

### Example: Playing a card

**CCGKit:**
```csharp
// UI
cardView.OnClick()
  → gameManager.PlayCard(card)
  → handler.PlayCard(state, card)
    → state.hand.Remove(card)
    → state.board.Add(card)
    → card.abilities.Resolve(state)
// UI must poll next frame to see changes
```

**Card Framework:**
```csharp
// UI
cardButton.OnClick()
  → gateway.Submit(PlayCardIntent(card, board))
// (returns immediately; engine queues)

// Engine (drain loop)
  → Validate: PlayCardHandler
  → Before-triggers: cost modifiers
  → Execute: move to board, apply cost
  → After-triggers: card abilities
  → Evaluate: shield breaks?
  → History envelope published

// Presentation (subscribes to events)
  → OnCardMoved(card, hand, board)
  → OnVariableChanged(player, mana, -3)
  → OnHistoryEnvelope(...)
  → Animate card movement
  → Update snapshot
  → OnSnapshotUpdated(...) → UI refreshes
```

---

## 21. Summary Table: Structural Comparison

| Dimension | CCGKit | Card Framework |
|-----------|--------|----------------|
| **Zone visibility** | Enum (Owner/Type/Visibility) | Policy/method (scalable) |
| **Card model** | Mixed definition + instance | Separated + variable-based |
| **Modifiers** | Direct mutation | Composable + inspectable |
| **Action validation** | Ad-hoc per handler | 4 layers (intent → command → validate → trigger) |
| **Hooks** | None | Before-trigger, After-trigger folds |
| **Ordering** | Implicit in handler chain | Explicit engine loop + FilterTriggers |
| **Phases** | Implicit in game logic | Explicit GamePhase + GameRules.OnQueueDrained |
| **Commands** | Implicit (GameAction) | Explicit (record types) |
| **Queue** | No | Yes; engine.Drain() |
| **Engine loop** | Synchronous, imperative | Asynchronous, declarative |
| **Determinism** | Depends on handler order | Guaranteed by engine order |
| **UI/Sim boundary** | Direct state read | Snapshot-based |
| **Input handling** | Direct method call | Intent → Command submission |
| **Snapshot** | No | Yes; published per drain cycle |
| **Replay** | Not supported | Yes; command sequence |
| **History** | No first-class history | Yes; history envelope with causality |

---

## 22. Effect Resolution — Stack vs Immediate

### CCGKit: No Stack, Immediate Resolution

CCGKit's `EffectSolver` resolves everything synchronously in-place. There is no stack, no queue, and no priority system for competing effects.

```csharp
// EffectSolver — immediate resolution
public void ActivateAbility(PlayerInfo player, RuntimeCard card, int abilityIndex)
{
    var libraryCard = config.GetCard(card.cardId);
    var ability = libraryCard.abilities[abilityIndex];
    
    // Collect targets
    var targets = GetCardTargets(state, card, ability.target);
    
    // Apply effect to each target immediately
    foreach (var target in targets)
    {
        ability.effect.Resolve(state, target);
    }
}
```

**How triggers fire:**
- `EffectSolver.SetTriggers(card)` registers stat-change callbacks on each card's stats.
- When a stat changes (`stat.onValueChanged`), the EffectSolver iterates every card in the owning player's zones, finds `TriggeredAbility`s whose trigger matches, and resolves them.
- No ordering guarantee — depends on iteration order of `List<RuntimeCard>`.

**How combat works:**
```csharp
// Direct stat mutation
public void FightPlayer(PlayerInfo attacker, RuntimeCard attackingCard, PlayerInfo target)
{
    target.namedStats["Life"].baseValue -= attackingCard.namedStats["Attack"].effectiveValue;
}

public void FightCreature(RuntimeCard attacker, RuntimeCard defender)
{
    defender.namedStats["Life"].baseValue -= attacker.namedStats["Attack"].effectiveValue;
    attacker.namedStats["Life"].baseValue -= defender.namedStats["Attack"].effectiveValue;
}
```

**What's missing:**
- No replacement effects (can't intercept damage and redirect it).
- No prevention effects (can't absorb or nullify).
- No async decisions during resolution (can't prompt "choose a target" mid-effect).
- No effect sequencing (can't say "deal damage, then if it killed something, draw a card").
- Triggers can chain, but uncontrolled — a trigger can modify a stat, which fires another trigger, with no depth limit or cycle detection.

### Card Framework: Three-Layer Effect Hierarchy

Card Framework separates effects into three layers with distinct capabilities:

```
Effect (orchestrates commands, async, no envelope of its own)
  └── GameAction (atomic, synchronous, one envelope)
        └── CompositeAction (reusable mutation group, folds into parent)
```

**Key innovation:** Only `Effect` can dispatch multiple `GameAction`s in sequence. A handler never dispatches other `GameAction`s — it only applies mutations.

```csharp
// Effect — async orchestration with decisions
public async Task Execute(IEffectScope scope)
{
    // Step 1: Ask player to choose a target
    var targetResult = await scope.Dispatch(new RequestTargetAction { Filter = EnemyFilter });
    // (engine pauses here until player responds)
    
    // Step 2: Deal damage to the chosen target
    var dmgResult = await scope.Dispatch(new DealDamageAction { 
        Target = targetResult.Target, Amount = 6 
    });
    
    // Step 3: If damage killed the target, draw a card
    if (dmgResult.TargetDied)
        await scope.Dispatch(new DrawCardAction { Actor = self });
}
```

**Replacement / prevention — handled by the trigger fold:**
```csharp
// Before-fold: shield absorbs damage
OnBefore<DamageAction>(
    condition: (cmd, scope) => cmd.Target == self,
    react: (cmd, scope) => {
        scope.Cancel("shield absorbed");
        scope.Dispatch(new RemoveShieldAction { Target = self });
    },
    owner: shieldEntityId
);
```

### Comparison

| Aspect | CCGKit | Card Framework |
|--------|--------|----------------|
| **Resolution model** | Immediate, synchronous | Three layers: Effect → GameAction → CompositeAction |
| **Async decisions** | Not possible | Built-in; `await scope.Dispatch(RequestTargetAction)` |
| **Replacement** | Not possible | Before-fold: `scope.Replace(...)` |
| **Prevention** | Not possible | Before-fold: `scope.Cancel(...)` |
| **Trigger chaining** | Recursive stat callbacks, uncontrolled | Dispatched as new commands, engine-managed |
| **Effect sequencing** | Not possible | Effect dispatches multiple actions in order |
| **Cycle protection** | None | Engine controls drain; re-entry is queued |

**Takeaway for Card Framework:** The three-layer hierarchy is the single most architecturally distinctive feature compared to CCGKit. CCGKit can't model "deal damage; if it killed, draw" as a first-class concept — it would require ad-hoc code in each handler. The Card Framework makes this a general pattern.

---

## 23. Targeting System

### CCGKit: Class-Per-Target with Enum Dispatch

CCGKit has a large `EffectTarget` enum (17 values) and a parallel class hierarchy:

```
Target (abstract base)
  ├── PlayerTargetBase (+ List<PlayerCondition>)
  │     ├── PlayerTarget          → EffectTarget.Player
  │     ├── OpponentTarget        → EffectTarget.Opponent
  │     ├── TargetPlayer          → EffectTarget.TargetPlayer (user-chosen)
  │     ├── RandomPlayer          → EffectTarget.RandomPlayer
  │     └── AllPlayers            → EffectTarget.AllPlayers
  └── CardTargetBase (+ List<CardCondition>)
        ├── ThisCard              → EffectTarget.ThisCard
        ├── PlayerCard            → EffectTarget.PlayerCard (user-chosen)
        ├── OpponentCard          → EffectTarget.OpponentCard (user-chosen)
        ├── TargetCard            → EffectTarget.TargetCard (user-chosen)
        ├── RandomPlayerCard      → EffectTarget.RandomPlayerCard
        ├── RandomOpponentCard    → EffectTarget.RandomOpponentCard
        ├── RandomCard            → EffectTarget.RandomCard
        ├── AllPlayerCards        → EffectTarget.AllPlayerCards
        ├── AllOpponentCards      → EffectTarget.AllOpponentCards
        └── AllCards              → EffectTarget.AllCards
```

**Two marker interfaces distinguish computed from user-chosen:**
- `IComputedTarget` — resolved by the solver (Player, Opponent, Random*, All*).
- `IUserTarget` — requires player input (TargetPlayer, PlayerCard, OpponentCard, TargetCard).

**Conditions** filter after target collection: `List<PlayerCondition>` / `List<CardCondition>` on each target base. Each condition is a predicate: `bool IsTrue(PlayerInfo)` or `bool IsTrue(RuntimeCard)`.

**Strengths:**
- The computed/user-target split is a good idea — makes it clear which effects need player input.
- Conditions as separate objects are composable.

**Weaknesses:**
- One class per target mode is a lot of boilerplate (17 enum values → ~15 classes).
- Can't target zones, equipment, or non-card entities — only players and cards.
- No multi-target support (can't say "choose 2 enemies").

### Card Framework: Entity-Uniform Targeting via Snapshot Templates

Card Framework doesn't have a Target class hierarchy. Instead, targeting is wired through `ActionTemplate` subtypes with `IReadOnlyList<EntityId>` for legal targets:

```csharp
// Snapshot advertises legal targets per action
public sealed record CastSpellTemplate(
    InstanceId                Spell,
    IReadOnlyList<EntityId>   Targets,     // any entity: card, player, zone
    IntRange?                 XCost,
    IReadOnlyList<ModeOption> Modes)
    : ActionTemplate(typeof(CastSpellCommand), Spell);
```

Targeting decisions are commands:
```csharp
// During effect execution — choose a target
var result = await scope.Dispatch(new RequestTargetAction {
    Filter = new TargetFilter { 
        IncludeZones = true,
        ConditionFn = e => e.HasTag("enemy") 
    }
});
```

**Strengths:**
- Targets are `EntityId` — anything in the entity system can be targeted (cards, players, zones, attachments).
- Multi-target is natural (multiple slots on a template).
- No class hierarchy per target mode.
- Filter is a predicate, not a class.

**Weaknesses:**
- The computed/user-target distinction (CCGKit's `IComputedTarget` / `IUserTarget`) is implicit — it depends on whether the effect dispatches a `RequestTargetAction` or resolves targets directly.

**Cross-learning:** CCGKit's `IComputedTarget` / `IUserTarget` marker interface is worth stealing. Card Framework should consider making it explicit on `ActionTemplate` whether a target slot is auto-resolved or player-chosen — this helps AI and network code distinguish "I need to wait for input" from "I can compute this."

---

## 24. Stat System — Base / Effective / Modifier

### CCGKit: Base + Modifier + Clamp

```csharp
public class Stat {
    public int baseValue;          // permanent value
    public int originalValue;      // definition value (for UI color coding)
    public int minValue, maxValue; // clamping bounds
    public List<Modifier> modifiers;
    
    public int effectiveValue =>
        Mathf.Clamp(baseValue + modifiers.Sum(m => m.value), minValue, maxValue);
    
    public Action<int, int> onValueChanged;  // fires on baseValue change
    
    public void OnEndTurn() {
        // Decrement modifier durations; remove expired ones
        modifiers.RemoveAll(m => { m.duration--; return m.duration <= 0 && !m.IsPermanent(); });
    }
}

public class Modifier {
    public int value;
    public int duration;  // 0 = permanent
}
```

**Strengths:**
- `effectiveValue` is computed on read (not cached/stale).
- Modifier duration tracks turn-by-turn expiry.
- `onValueChanged` callback enables trigger wiring.
- `originalValue` enables UI diffing (show red/green).

**Weaknesses:**
- Modifiers track only +/- amount and duration. No "source" (can't answer "who gave this +2?").
- `onValueChanged` only fires on `baseValue` changes, not when modifiers are added/removed (effective value can change silently).
- Duration is in turns, but there's no "until end of turn" vs "until next turn" distinction.
- No multiplication or other modifier operations — additive only.

### Card Framework: Layered Value Calculation

Card Framework hasn't locked a concrete `Stat` class, but the design specifies:

```
Effective(instance, key) =
    1. Base definition value
    2. Instance base override
    3. Local modifiers on instance
    4. Attached instance modifiers
    5. External modifiers from other entities
    6. Zone/context modifiers
    7. Preset-specific composition rules
```

Each modifier carries a source identifier for debugging.

**Strengths:**
- Layers are ordered and explicit.
- Every modifier has a source — debuggable ("this +2 came from the sword attachment").
- Supports multiplicative/replacement operations (layer 7).
- Supports zone modifiers ("all creatures in this zone get +1 attack").
- Supports cross-entity modifiers ("your hero power costs 1 less").

**Weaknesses:**
- The modifier ordering/composition hasn't been concretely locked (still design). Could be complex to implement efficiently.
- No built-in change callback equivalent to CCGKit's `onValueChanged`.

**What Card Framework should steal from CCGKit:**
1. **`originalValue`** — storing the definition value alongside the current value for UI diffing is a practical touch. Easy to derive from the definition, but worth making first-class.
2. **Duration-based modifier expiry** — CCGKit's `Modifier.duration` with `OnEndTurn()` cleanup is simple and works. Card Framework's attachment-entity model is more powerful but also heavier.

---

## 25. Cost System

### CCGKit: PayResourceCost

CCGKit has a single cost type: pay N of a player stat.

```csharp
public class PayResourceCost : Cost {
    public int statId;  // which player stat to deduct
    public int value;   // fixed amount
}
```

**Validation** happens in `EffectSolverHandler.OnActivateAbility()`:
```csharp
if (sourcePlayer.stats[payResourceCost.statId].effectiveValue >= statCost) {
    sourcePlayer.stats[payResourceCost.statId].baseValue -= statCost;
    server.effectSolver.ActivateAbility(sourcePlayer, card, 0);
}
```

**Weaknesses:**
- Fixed cost only (no "X" costs, no multi-resource costs).
- Cost is deducted by mutating `baseValue` directly — no middleware can intercept ("your spells cost 1 less").
- No separation between "check affordability" and "pay the cost."

### Card Framework: Cost as Part of the Command Pipeline

In Card Framework, cost payment is a `GameAction` dispatched by an effect:

```csharp
public async Task Execute(IEffectScope scope)
{
    // Before-triggers can modify the cost
    await scope.Dispatch(new PayCostAction { 
        Actor = player, 
        Costs = card.Definition.Costs 
    });
    // If PayCostAction was cancelled by a before-trigger, the effect stops
    // If a before-trigger modified the amount, the modified amount is paid
}
```

Because cost payment goes through the trigger fold, effects like "your spells cost 1 less" are natural:
```csharp
OnBefore<PayCostAction>(
    condition: (cmd, _) => cmd.Costs.Any(c => c.ResourceType == Mana),
    react: (cmd, scope) => {
        cmd with { Costs = cmd.Costs.Select(c => c with { Amount = c.Amount - 1 }) };
    }
);
```

**Strengths:**
- Multi-resource costs, X-costs, alternative costs are all expressible.
- Cost modifiers hook into the before-fold — no special cost-reduction system needed.
- Affordability check is part of `PayCostHandler.Validate`.

**Cross-learning:** CCGKit's simplicity works for Hearthstone-like games (one resource). Card Framework's approach is necessary for MTG-like games (multi-resource, X-costs, alternative costs, cost reduction). If Card Framework only ever targets simple games, the extra machinery is wasted.

---

## 26. Networking Architecture

### CCGKit: Mirror-Based Authoritative Server

CCGKit uses [Mirror](https://mirror-networking.com/) for networking. The server is authoritative — clients send input, server resolves, server broadcasts results.

```
Client                          Server                          Client
  │                               │                               │
  ├── MoveCardMessage ──────────► │                               │
  │                               ├── Validate                    │
  │                               ├── Resolve (mutate state)      │
  │                               ├── Broadcast ────────────────► │
  │  ◄── CardMovedMessage ────────┤── CardMovedMessage ──────────►│
  │                               │                               │
  ├── FightPlayerMessage ────────►│                               │
  │                               ├── Apply damage                │
  │  ◄── PlayerAttackedMessage ───┤── PlayerAttackedMessage ─────►│
```

**Key patterns:**
- **Separate messages for owner vs opponent:** `GetPlayerNetworkState()` sends full card data to the owner; `GetOpponentNetworkState()` respects zone visibility (hidden zones send only card count, not card data).
- **RNG seed sharing:** The server creates a seed and sends it to all clients in `StartGameMessage`. This enables deterministic client-side prediction.
- **Full state sync at turn boundaries:** Every `StartTurnMessage` carries the complete `NetPlayerInfo` for both players. This is a brute-force approach that guarantees consistency.

**Strengths:**
- Simple and robust — full state sync at every turn means desync is impossible.
- Visibility enforcement is server-side (opponent can't cheat by reading hidden zone data).
- Mirror handles connection/disconnection/transport.

**Weaknesses:**
- Full state sync is expensive — sends everything, not just deltas.
- No delta/patch mechanism — can't efficiently send "card X moved from zone A to zone B."
- Hard to add spectator mode or N-player support (assumes exactly 2 players).
- Coroutine-based turn timing is fragile (depends on Unity frame timing).

### Card Framework: Not Yet Specified

Card Framework hasn't specified networking. But the architecture is designed to be friendly to it:

- **Commands are serializable facts** — they're records with value-type fields. Sending a command log across the network is natural.
- **History envelopes are the broadcast** — instead of sending full state, send the envelope ("card X moved, variable Y changed"). Clients apply the same state changes.
- **Snapshot is the client view** — instead of computing player-vs-opponent visibility in the network layer, the snapshot already encodes what each actor can see.

**What Card Framework should learn from CCGKit's networking:**
1. **Full state sync is a valid fallback.** Even with delta-based networking, having a "send everything" mode for reconnection or desync recovery is necessary.
2. **Visibility enforcement must be server-side.** The snapshot should be produced per-player on the server, never on the client. CCGKit does this correctly.
3. **RNG seed sharing.** If effects involve randomness, share the seed so clients can predict outcomes locally. Card Framework should carry RNG seeds in command payloads.
4. **Separate wire types.** CCGKit's `NetCard`, `NetStat`, `NetZone` are not the same as `RuntimeCard`, `Stat`, `RuntimeZone`. This separation is important — the wire types strip implementation details and enforce visibility. Card Framework should have similar "wire form" types distinct from simulation types.

---

## 27. Authoring & Editor Tooling

### CCGKit: JSON + Custom Inspector

CCGKit stores all game configuration as JSON files in `Assets/Resources/`:
- `game_properties.json` — turn duration, deck size limits, start/end actions, end-game conditions.
- `game_zones.json` — zone definitions (name, owner, type, visibility, capacity).
- `player_stats.json` — player stat definitions (life, mana, etc.).
- `card_types.json` — card type definitions (creature, spell) with per-type stats.
- `keywords.json` — keyword definitions (e.g., Impetus, Provoke).
- `card_library.json` — card sets with individual card definitions.

**Custom editor** (`CCGKitEditor.cs`) provides a tabbed Inspector for editing all of these. The editor uses reflection-based attributes (`[PlayerStatField]`, `[CardTypeField]`, `[GameZoneField]`) to render dropdown pickers that reference other definitions by ID.

**Strengths:**
- Complete authoring pipeline in-editor. Game designers don't need to edit JSON by hand.
- Reflection-based attribute system means adding a new stat or zone type automatically updates all editor dropdowns.
- JSON is human-readable for debugging.

**Weaknesses:**
- No visual card designer (no card preview, no drag-and-drop ability builder).
- No validation of cross-references (e.g., a card referencing a deleted stat ID).
- FullSerializer is a third-party dependency with its own quirks.

### Card Framework: Not Yet Specified

Card Framework's authoring story is still open. The design docs reference "game, preset, schema, or authoring system" as responsible for defining available keys, value types, and validation rules.

**What Card Framework should learn:**
1. **JSON definitions are a strong starting point.** CCGKit's approach works and is simple. Don't over-engineer authoring until the runtime model is stable.
2. **Reflection-based attribute pickers are valuable.** Being able to annotate a field with `[PlayerStatField]` and have the editor render a dropdown is a practical pattern.
3. **Validation at authoring time matters.** CCGKit doesn't do it (and suffers for it). Card Framework should plan for "validate all cross-references on save."
4. **Wire format ≠ authoring format.** CCGKit's JSON is both the authoring format and the runtime format. Card Framework should consider separating these (e.g., YAML for authoring, binary for runtime).

---

## 28. Ongoing / Continuous Effects

### CCGKit: Duration-Based Modifier Expiry

CCGKit's only concept of "ongoing" is the `Modifier.duration` field. A modifier with `duration > 0` expires after N turns (decremented in `Stat.OnEndTurn()`). A modifier with `duration == 0` is permanent.

There is no concept of:
- An aura ("while this creature is in play, all allies get +1").
- A condition-linked effect ("until end of turn" vs "until this card leaves play").
- An effect that monitors game state and reacts over time.
- An effect that can be selectively removed by another effect.

### Card Framework: Attachment Entities with Trigger Subscriptions

Card Framework models ongoing effects as **attachment entities** — full instances with their own `InstanceId`, variables, and trigger subscriptions.

```csharp
// Heal-over-time: subscribes to TurnStartSignal, heals, counts down, self-destructs
public sealed class HealOverTimeDefinition : EntityDefinition
{
    public override void Register(ITriggerSystem triggers, InstanceId self, Context ctx)
    {
        triggers.OnAfter<TurnStartSignal>(
            react: (_, scope) => {
                scope.Dispatch(new HealActorAction { Target = scope.GetHost(self), Amount = 3 });
                scope.Dispatch(new DecrementTurnsRemainingAction { Target = self });
                if (remaining - 1 <= 0)
                    scope.Dispatch(new DestroyEntityAction { Target = self });
            },
            owner: self);
    }
}
```

**Common patterns:**
| Pattern | Mechanism |
|---------|-----------|
| Duration (N turns) | Decrement counter variable on TurnStartSignal; destroy at 0 |
| Until end of turn | Subscribe to TurnEndSignal; destroy unconditionally |
| While source in play | Source creates attachment; source's destruction triggers attachment destruction |
| Global this turn | Attach to match entity; destroy on TurnEndSignal |

**Key advantage over CCGKit:** Ongoing effects are inspectable entities. You can query "what ongoing effects are on this creature?" and get back a list of attachment instances, each with their own variables and trigger subscriptions. CCGKit's modifiers are anonymous `{value, duration}` pairs — you can't inspect them or interact with them.

---

## 29. Data Configuration vs Runtime Types (Wire Separation)

### CCGKit's Wire Types

CCGKit maintains a strict separation between runtime types and network types:

| Runtime | Wire (Network) |
|---------|----------------|
| `RuntimeCard` | `NetCard` (cardId, instanceId, stats[], keywords[]) |
| `Stat` | `NetStat` (statId, baseValue, originalValue, minValue, maxValue, modifiers[]) |
| `RuntimeZone` | `NetStaticZone` / `NetDynamicZone` |
| `RuntimeKeyword` | `NetKeyword` |
| `PlayerInfo` | `NetPlayerInfo` (stats[], staticZones[], dynamicZones[]) |

The server converts runtime types to wire types in `GetPlayerNetworkState()` / `GetOpponentNetworkState()`, applying visibility rules (hidden zones send card count only, not card data).

**This is well done.** The wire types are flat, serializable structs with no behaviour. The runtime types carry references, callbacks, and mutable state. The two never mix.

### Card Framework's Boundary: ADR-0005

Card Framework's [ADR-0005 (Objects over IDs)](decisions/0005-objects-over-ids.md) addresses this directly. Inside the simulation, entities are objects (passed by reference). At boundaries (snapshots, network, serialization), entities are IDs.

```
Simulation: Zone zone, Card card → objects, passed directly
Snapshot:   InstanceId, EntityId  → IDs, boundary form
Network:    same IDs + wire-form payloads
```

**What Card Framework should carry forward from CCGKit:** The explicit `NetCard` / `NetStat` pattern is worth copying. Having distinct wire-form records (not just "serialize the runtime object") forces you to think about visibility, bandwidth, and what the client actually needs.

---

## 30. End-Game Conditions & Game Lifecycle

### CCGKit

End-game conditions are data-driven. `GameProperties` carries a `List<EndGameCondition>`. Each condition is checked after every action; when one returns `true`, the game ends.

```csharp
// Condition: a player stat reaches a threshold
public class PlayerStatEndGameCondition : EndGameCondition {
    public int statId;
    public ConditionOperator op;  // <, ==, >=, etc.
    public int value;
    
    public override bool IsTrue(PlayerInfo player) =>
        ApplyOperator(player.stats[statId].effectiveValue, op, value);
}

// Condition: a zone has N cards
public class CardsInZoneEndGameCondition : EndGameCondition {
    public int zoneId;
    public ConditionOperator op;
    public int numCards;
    
    public override bool IsTrue(PlayerInfo player) =>
        ApplyOperator(player.zones[zoneId].cards.Count, op, numCards);
}
```

**Checked by the server after every turn and after every effect resolution.**

### Card Framework

No explicit end-game system yet. But the design supports it naturally:

- **GameRules.Evaluate** runs after every command. A game can check win/loss conditions there and dispatch a `GameEndCommand`.
- **GameRules.OnQueueDrained** runs when the queue empties. A game can check for state-based conditions (like MTG's state-based actions: "if a player has 0 life, that player loses").

**What Card Framework should learn:**
- CCGKit's data-driven end-game conditions are elegant. You define "Life <= 0 → Loss" in the config, not in code. Card Framework should support this pattern (conditions as data, checked in `Evaluate`).

---

## 31. Random Number Generation & Determinism

### CCGKit: Seeded RNG

CCGKit creates a seeded RNG at game start and shares the seed with all clients:

```csharp
var rngSeed = System.Environment.TickCount;
effectSolver = new EffectSolver(gameState, rngSeed);

// Shared via StartGameMessage
msg.rngSeed = rngSeed;
```

**This enables client-side prediction:** both client and server can compute the same random outcomes from the same seed, as long as they call the RNG in the same order.

**Weakness:** If the call order ever diverges (e.g., due to a bug), the RNG desyncs and all subsequent random outcomes differ. No recovery mechanism.

### Card Framework: Not Yet Specified

Card Framework hasn't locked an RNG strategy, but the command-log architecture is friendly to it. Each command that involves randomness should carry the resolved random values in its payload (or carry a sub-seed), so replay produces the same outcome deterministically.

**Recommendation:** Don't share a global seed. Instead, carry resolved random values in the `CommandResult` / `HistoryEntry`. This is more robust than relying on call-order synchronization.

---

## 32. What Card Framework Has That CCGKit Doesn't (Reference for Future Systems)

These are Card Framework concepts that have no CCGKit equivalent. They're recorded here so future implementation sessions can reference them without re-deriving.

| Card Framework Concept | What It Enables | CCGKit Equivalent |
|------------------------|----------------|-------------------|
| **Effect → GameAction → CompositeAction hierarchy** | Async decisions mid-resolution, effect sequencing | None — effects are immediate |
| **Before/After trigger fold** | Replacement, prevention, cost modification | None — no interception layer |
| **Trigger priority + `FilterTriggers`** | Deterministic ordering of competing triggers | None — triggers fire in iteration order |
| **`IEffectScope.Cancel()` / `.Replace()`** | Shield absorption, damage redirection | None |
| **`RequestTargetAction` as a command** | Player choices during resolution are pipeline citizens | Targeting is pre-resolution only |
| **AvailableActions snapshot** | UI shows exactly what's legal | UI guesses based on hand/board |
| **ActionTemplate with typed subtypes** | Multi-target, modal, X-cost actions | Single-target, fixed-cost only |
| **History envelope with causality** | Replay, debugging, animation sequencing | No history |
| **Attachment entities as ongoing effects** | Inspectable buffs/debuffs with lifecycle | Anonymous `{value, duration}` modifiers |
| **OwnershipState as separate slice** | Any entity (card, zone) can be owned | Ownership is a field on PlayerInfo |
| **CardRef rotation on zone move** | Stale-reference detection across boundaries | None — references can dangle |
| **Zone-as-handle + state-slices** | Per-zone-type runtime data without class hierarchy | Flat `GameZoneType` class |

---

## 33. CCGKit Input → Action Flow — Actual Code Path

This section traces the concrete code path for the most common player action: **playing a card from hand to board**. Every snippet is from real CCGKit source; the numbered steps show exactly how input maps to action.

### Step 1: Raycast Hit Detection (DemoHumanPlayer.Update)

Every frame, the player's `Update()` raycasts from the mouse position and looks for `CardView` components:

```csharp
// DemoHumanPlayer.Update() — runs every frame
if (Input.GetMouseButtonDown(0))
{
    var hits = Physics2D.RaycastAll(mousePos, Vector2.zero);
    var hitCards = new List<GameObject>();
    foreach (var hit in hits)
    {
        if (hit.collider.gameObject.GetComponent<CardView>() != null &&
            !hit.collider.gameObject.GetComponent<CardView>().isPreview &&
            hit.collider.gameObject.GetComponent<CardView>().CanBePlayed(this))
        {
            hitCards.Add(hit.collider.gameObject);
        }
    }
    if (hitCards.Count > 0)
    {
        // Pick topmost card by sorting order
        hitCards = hitCards.OrderBy(x => x.GetComponent<SortingGroup>().sortingOrder).ToList();
        var topmostCardView = hitCards[hitCards.Count - 1].GetComponent<CardView>();
        topmostCardView.GetComponent<HandCard>().OnSelected();
    }
}
```

**What's happening:** Physics raycast → filter to `CardView` → check `CanBePlayed` → call `HandCard.OnSelected()`.

### Step 2: Affordability Check (CardView.CanBePlayed)

`CanBePlayed` is the **only validation gate** before the card enters the play flow:

```csharp
// CardView.cs — base check
public virtual bool CanBePlayed(DemoHumanPlayer owner)
{
    return owner.isActivePlayer && owner.manaStat.effectiveValue >= manaCost;
}

// CreatureCardView.cs — adds board capacity check
public override bool CanBePlayed(DemoHumanPlayer owner)
{
    var board = owner.boardZone;
    if (board.maxCards > 0 && board.numCards == board.maxCards)
        return false;
    return base.CanBePlayed(owner);
}
```

**What's happening:** Two checks — (1) is it my turn? (2) can I afford the mana cost? Creatures add (3) is the board full?

**No server validation.** This is a client-side-only check. The server trusts the client's decision.

### Step 3: Drag Gesture (HandCard)

`HandCard` is a MonoBehaviour attached to each card in hand. It manages the drag-to-board gesture:

```csharp
// HandCard.cs
public void OnSelected()
{
    if (ownerPlayer.isActivePlayer && cardView.CanBePlayed(ownerPlayer))
    {
        startedDrag = true;
        initialPos = transform.position;
        ownerPlayer.isCardSelected = true;
    }
}

// Drag follows mouse every frame
private void Update()
{
    if (startedDrag)
        transform.position = Camera.main.ScreenToWorldPoint(Input.mousePosition);
}

// Drop — check if card landed on the board zone collider
public void OnMouseUp()
{
    startedDrag = false;
    ownerPlayer.isCardSelected = false;

    if (boardZone.GetComponent<BoxCollider2D>().bounds.Contains(transform.position))
    {
        ownerPlayer.PlayCard(cardView);   // ← THIS IS THE ENTRY POINT
    }
    else
    {
        transform.position = initialPos;  // snap back to hand
    }
}
```

**What's happening:** Drag card → drop on board collider → call `DemoHumanPlayer.PlayCard(cardView)`. If dropped elsewhere, snap back.

### Step 4: Card Type Branching (DemoHumanPlayer.PlayCard)

`PlayCard` branches on card type (Creature vs Spell), then handles targeting and ability detection:

```csharp
// DemoHumanPlayer.PlayCard(CardView card)
public void PlayCard(CardView card)
{
    if (!card.CanBePlayed(this)) { card.ResetToInitialPosition(); return; }

    var libraryCard = gameConfig.GetCard(card.card.cardId);
    var cardType = gameConfig.cardTypes.Find(x => x.id == libraryCard.cardTypeId);

    if (cardType.name == "Creature")
    {
        // 1. Instantiate board creature visual
        var boardCreature = Instantiate(boardCreaturePrefab);
        boardCreature.GetComponent<BoardCreature>().PopulateWithInfo(card.card);

        // 2. Remove from hand visuals, add to board visuals
        playerHandCards.Remove(card);
        playerBoardCards.Add(boardCreature.GetComponent<BoardCreature>());
        Destroy(card.gameObject);

        // 3. Rearrange board, then check for enter-zone triggered abilities
        RearrangeBottomBoard(() =>
        {
            // 4. Look for OnCardEnteredZone triggers that need a user target
            var triggeredAbilities = libraryCard.abilities.FindAll(x => x is TriggeredAbility);
            TriggeredAbility targetableAbility = null;
            foreach (var ability in triggeredAbilities)
            {
                var triggeredAbility = ability as TriggeredAbility;
                var trigger = triggeredAbility.trigger as OnCardEnteredZoneTrigger;
                if (trigger != null &&
                    trigger.zoneId == boardZone.zoneId &&
                    triggeredAbility.target is IUserTarget)  // ← needs player input
                {
                    targetableAbility = triggeredAbility;
                    break;
                }
            }

            // 5. Preemptively move card in local state
            playerInfo.namedZones["Hand"].RemoveCard(card.card);
            playerInfo.namedZones["Board"].AddCard(card.card);

            // 6. If ability needs targeting → show targeting arrow → wait for target
            if (targetableAbility != null &&
                effectSolver.AreTargetsAvailable(targetableAbility.effect, card.card, targetableAbility.target))
            {
                var targetingArrow = Instantiate(spellTargetingArrowPrefab);
                targetingArrow.onTargetSelected += () =>
                {
                    // 7a. TARGET SELECTED: send to server with target info
                    base.PlayCard(card.card, targetingArrow.targetInfo);
                    effectSolver.MoveCard(netIdentity, card.card, "Hand", "Board",
                        targetingArrow.targetInfo.ToArray());
                };
                targetingArrow.Begin(boardCreature.transform.localPosition);
            }
            else
            {
                // 7b. NO TARGETING: send to server immediately
                base.PlayCard(card.card);
                effectSolver.MoveCard(netIdentity, card.card, "Hand", "Board");
            }
        });
    }
    else if (cardType.name == "Spell")
    {
        // Similar flow: animate to spell pivot, check abilities, send to server
        // ...
    }
}
```

**What's happening:** Card type determines the visual flow. Abilities are scanned for `IUserTarget` triggers. If found, a targeting arrow waits for player input before proceeding.

### Step 5: Cost Payment + Network Message (Player.PlayCard)

`base.PlayCard` is the core `Player` class method that pays costs and sends the network message:

```csharp
// Player.cs — the base class
public void PlayCard(RuntimeCard card, List<int> targetInfo = null)
{
    // 1. Pay costs by mutating local stat directly
    var libraryCard = GameManager.Instance.config.GetCard(card.cardId);
    PayResourceCosts(libraryCard.costs.ConvertAll(cost => cost as PayResourceCost));

    // 2. Send network message to server
    SendMoveCardMessage(card, targetInfo);
}

public void PayResourceCosts(List<PayResourceCost> costs)
{
    costs.ForEach(cost => {
        if (cost != null)
            playerInfo.stats[cost.statId].baseValue -= cost.value;  // direct mutation
    });
}

public void SendMoveCardMessage(RuntimeCard card, List<int> targetInfo = null)
{
    var msg = new MoveCardMessage();
    msg.playerNetId = netIdentity;
    msg.cardInstanceId = card.instanceId;
    msg.originZoneId = playerInfo.namedZones["Hand"].zoneId;
    msg.destinationZoneId = playerInfo.namedZones["Board"].zoneId;
    if (targetInfo != null)
        msg.targetInfo = targetInfo.ToArray();
    NetworkClient.Send<MoveCardMessage>(msg);
}
```

**What's happening:** Client pays costs locally (optimistic update), then sends a `MoveCardMessage` to the server with card instance ID, origin zone, destination zone, and optional target info.

### Step 6: Server Receives + Resolves (EffectSolverHandler)

The server's `EffectSolverHandler` receives the activation message. For abilities specifically:

```csharp
// EffectSolverHandler.OnActivateAbility
public virtual void OnActivateAbility(NetworkConnection conn, ActivateAbilityMessage msg)
{
    var sourcePlayer = server.gameState.players.Find(x => x.netId == msg.playerNetId);
    var card = sourcePlayer.zones[msg.zoneId].cards.Find(x => x.instanceId == msg.cardInstanceId);

    var libraryCard = GameManager.Instance.config.GetCard(card.cardId);
    var cost = libraryCard.costs.Find(x => x is PayResourceCost);

    // Server-side cost validation
    if (cost != null)
    {
        var statCost = (cost as PayResourceCost).value;
        if (sourcePlayer.stats[(cost as PayResourceCost).statId].effectiveValue >= statCost)
        {
            sourcePlayer.stats[(cost as PayResourceCost).statId].baseValue -= statCost;
            server.effectSolver.ActivateAbility(sourcePlayer, card, 0);
        }
        // else: silently ignored — no error sent back to client
    }

    // Broadcast to all clients
    NetworkServer.SendToAll<ActivateAbilityMessage>(msg);
}
```

**What's happening:** Server validates cost, applies it, resolves the ability via EffectSolver, and broadcasts to all clients.

### Step 7: Effect Resolution (EffectSolver.MoveCard)

The EffectSolver handles the zone transition and triggers:

```csharp
// EffectSolver — called by both client (prediction) and server (authoritative)
effectSolver.MoveCard(netIdentity, card, "Hand", "Board", targetInfo);

// Inside EffectSolver.MoveCard:
// 1. Remove card from origin zone
// 2. Add card to destination zone
// 3. Fire OnCardLeftZone triggers
// 4. Fire OnCardEnteredZone triggers (this resolves enter-board abilities)
// 5. Check destroy conditions
```

### Complete Flow Diagram

```
 ┌──────────────────────────────────────────────────────────────────┐
 │  CLIENT                                                          │
 │                                                                  │
 │  Update() raycast                                                │
 │    ↓                                                             │
 │  CardView hit? → CanBePlayed()?                                  │
 │    ↓ yes                                                         │
 │  HandCard.OnSelected() → startedDrag                             │
 │    ↓                                                             │
 │  Update() — card follows mouse                                   │
 │    ↓                                                             │
 │  OnMouseUp() — card over board collider?                         │
 │    ↓ yes                                                         │
 │  DemoHumanPlayer.PlayCard(cardView)                              │
 │    ├─ Look up card type (Creature / Spell)                       │
 │    ├─ Instantiate board visual                                   │
 │    ├─ Scan abilities for IUserTarget triggers                    │
 │    │    ├─ Found → show SpellTargetingArrow                      │
 │    │    │           ↓ player selects target                      │
 │    │    │           onTargetSelected fires                       │
 │    │    └─ Not found → proceed immediately                       │
 │    ↓                                                             │
 │  base.PlayCard(card, targetInfo)                                 │
 │    ├─ PayResourceCosts() — mutate local stats                    │
 │    └─ SendMoveCardMessage() ─────────────────────────┐           │
 │                                                       │           │
 │  effectSolver.MoveCard() — client prediction          │           │
 │    ├─ Remove from Hand zone                           │           │
 │    ├─ Add to Board zone                               │           │
 │    └─ Fire triggers locally                           │           │
 │                                                       │           │
 └───────────────────────────────────────────────────────┼───────────┘
                                                         │
                      ┌──── MoveCardMessage ─────────────┘
                      ↓
 ┌──────────────────────────────────────────────────────────────────┐
 │  SERVER                                                          │
 │                                                                  │
 │  Server receives MoveCardMessage                                 │
 │    ↓                                                             │
 │  effectSolver.MoveCard() — authoritative                         │
 │    ├─ Validate (cost check in some handlers)                     │
 │    ├─ Remove from Hand zone                                      │
 │    ├─ Add to Board zone                                          │
 │    ├─ Fire OnCardEnteredZone triggers → resolve abilities        │
 │    ├─ Check end-game conditions                                  │
 │    └─ Check destroy conditions                                   │
 │                                                                  │
 │  Broadcast CardMovedMessage to all clients ──────────────────┐   │
 │                                                               │   │
 └───────────────────────────────────────────────────────────────┼───┘
                                                                 │
                      ┌──── CardMovedMessage ────────────────────┘
                      ↓
 ┌──────────────────────────────────────────────────────────────────┐
 │  OPPONENT CLIENT                                                 │
 │                                                                  │
 │  DemoHumanPlayer.OnCardMoved(msg)                                │
 │    ├─ Look up card type from msg.card.cardId                     │
 │    ├─ Remove random card from opponent hand visuals              │
 │    ├─ Instantiate BoardCreature / SpellCardView                  │
 │    ├─ If msg.targetInfo → show OpponentTargetingArrow            │
 │    └─ effectSolver.MoveCard() — apply state + triggers           │
 │                                                                  │
 └──────────────────────────────────────────────────────────────────┘
```

### Key Observations

1. **No command object.** The "action" is distributed across: UI gesture (`HandCard`), affordability check (`CanBePlayed`), visual setup + ability scan (`DemoHumanPlayer.PlayCard`), cost payment + network send (`Player.PlayCard`), and server resolution (`EffectSolver`). There is no single object that represents "play this card."

2. **Validation is scattered.** `CanBePlayed` runs on the client. `EffectSolverHandler` does a second cost check on the server (but only for activated abilities, not for card plays). The server doesn't validate "is this the active player?" for card plays — it trusts the client.

3. **Client predicts optimistically.** The client calls `effectSolver.MoveCard()` locally before the server confirms. If the server disagrees, there's no rollback mechanism.

4. **Targeting is UI-level, not engine-level.** The targeting arrow is a Unity MonoBehaviour that waits for player input. The engine doesn't know a decision is pending — it's purely a UI concern. The decision result (target info) is attached to the network message as an `int[]`.

5. **Card type branching is hardcoded.** `if (cardType.name == "Creature")` / `else if (cardType.name == "Spell")` appears in multiple places. Adding a third card type (Enchantment, Artifact) requires editing `DemoHumanPlayer.PlayCard`, `OnCardMoved`, and several other methods.

### How Card Framework Handles This Differently

| CCGKit step | Card Framework equivalent |
|-------------|---------------------------|
| Raycast → `CanBePlayed` | UI reads `AvailableActions` snapshot — legal cards are already known |
| `HandCard.OnSelected` + drag | Game-specific UI; submits `PlayerIntent` on drop |
| `DemoHumanPlayer.PlayCard` (800 lines of branching) | `IntentMapper.FromPlayerIntent()` → typed `PlayCardCommand` |
| Ability scan for `IUserTarget` | Effect dispatches `RequestTargetAction` during resolution |
| `base.PlayCard` → `PayResourceCosts` + `SendMoveCardMessage` | Pipeline: `Validate` → `Before-fold` → `Handle` → `After-fold` → envelope |
| Server `EffectSolverHandler` | `ICommandHandler<PlayCardCommand>.Handle()` |
| Client-side `effectSolver.MoveCard` prediction | History envelope applied by presentation subscriber |
| `OnCardMoved` (opponent visual update) | Presentation subscribes to `OnCardMoved` history event |

The fundamental difference: CCGKit's input mapping is **imperative spaghetti** — each card type has its own branch, targeting is a UI concern, cost payment is scattered across client and server, and there's no single "this is the action" object. Card Framework's input mapping is **declarative pipeline** — every action is a typed command that flows through the same validation/execution/history path regardless of card type.

---

## 12. Conclusion

**Different starting points, same problem domain.**

- **CCGKit** solves a fully-specified problem with real constraints (networking, shipping, backward compat). It's pragmatic and proven.
- **Card Framework** is designing the architecture from first principles with fewer constraints. It has the luxury of choosing clean patterns.

**Neither is "wrong."** CCGKit's synchronous, imperative approach is the right tradeoff for a shipped, real-time game. Card Framework's asynchronous, declarative approach is the right tradeoff for a modular, replayable, debuggable architecture.

**The structural divergences:**
1. **Queueing:** Card Framework's explicit queue + engine loop vs. CCGKit's synchronous handlers.
2. **Snapshot boundary:** Card Framework publishes snapshots; CCGKit reads state directly.
3. **Validation layers:** Card Framework's 4-layer pipeline vs. CCGKit's ad-hoc per-handler checks.
4. **Phase system:** Card Framework's explicit GamePhase + OnQueueDrained vs. CCGKit's implicit handler logic.

**All of these are architecture choices, not right/wrong.** CCGKit optimizes for simplicity and directness. Card Framework optimizes for modularity, replay, and debugging.

**Next steps:**
1. **Zone visibility:** Adopt Card Framework's policy-based approach for N-player games.
2. **Command pipeline:** If CCGKit ever needs replay/undo, add a command queue layer.
3. **Snapshot model:** Consider publishing AvailableActions for cleaner UI decoupling.
4. **Phase system:** Make phases explicit (even if just an enum) rather than implicit in handlers.
5. **Validation gates:** Unify on 3-4 layers (intent → command → handler → trigger fold).
6. **Terminology:** Use "Command" for explicit types; avoid "Action" (ambiguous).

