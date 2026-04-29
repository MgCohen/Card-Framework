# Hearthstone Clone – High-Level Architecture Plan

## Context
Building the system/logic layer of a Hearthstone-like card game on top of the Scaffold framework. No UI or presentation — pure game logic, state, and rules. The goal is a deterministic, headless-testable simulation that maps cleanly onto Scaffold's State/Commands/Effects/Turn/Stack systems.

---

## Core Scaffold Primitives Used

| Primitive | Scaffold Source | Role in Hearthstone |
|---|---|---|
| `Store` + `Mutator<T>` | `State/Runtime/Store.cs` | All game state, immutable |
| `Command` | `Effects/Models/Command.cs` | Atomic state mutations |
| `Effect` | `Effects/Models/Effect.cs` | Multi-step game workflows |
| `IGameEvents` | `Effects/Abstractions/IGameEvents.cs` | Trigger/reaction system |
| `ITurnHandler` | `Turn/Runtime/ITurnHandler.cs` | Phase/step management |
| `PlayService` / `IStackHandler` | `Stack/Runtime/PlayService.cs` | Stack resolution + priority |
| `IEventBus` | `Infra/Events/Runtime/Contracts/IEventBus.cs` | Domain event notifications |
| `IReference` | `State/Runtime/Abstractions/IReference.cs` | Entity identity in Store |

---

## Folder & Namespace Structure

```
Assets/Scripts/Game/Hearthstone/
  Data/               Scaffold.Hearthstone.Data
    Cards/            — CardDefinition, CardType enum
    Keywords/         — Keyword enum, KeywordSet
    Abilities/        — IBattlecry, IDeathrattle, ISecret, IInspire
  State/              Scaffold.Hearthstone.State
    References/       — PlayerRef, MinionRef, CardRef, GameRef
    Records/          — PlayerState, HeroState, HandState, DeckState,
                        BoardState, MinionState, GameState, WeaponState
  Commands/           Scaffold.Hearthstone.Commands
    Hero/             — DealHeroDamageCommand, HealHeroCommand,
                        GainArmorCommand, EquipWeaponCommand
    Minion/           — SummonMinionCommand, DealMinionDamageCommand,
                        DestroyMinionCommand, SilenceMinionCommand
    Card/             — DrawCardCommand, DiscardCardCommand,
                        AddCardToHandCommand
    Turn/             — GainManaCommand, SpendManaCommand,
                        RefillManaCommand, IncrementAttacksCommand
    Combat/           — MarkAttackedCommand, ResetAttackCountCommand
  Effects/            Scaffold.Hearthstone.Effects
    Turn/             — StartTurnEffect, EndTurnEffect, DrawCardEffect
    Combat/           — AttackEffect, HeroPowerEffect
    Card/             — PlayMinionEffect, PlaySpellEffect, PlayWeaponEffect
    Death/            — MinionDeathEffect, WeaponBreakEffect
  Triggers/           Scaffold.Hearthstone.Triggers
    — BattlecryTrigger, DeathrattleTrigger, SecretTrigger
    — TriggerController (IController, IInitialize, IDispose)
  Rules/              Scaffold.Hearthstone.Rules
    — AttackValidator, PlayValidator, TargetValidator
  Game/               Scaffold.Hearthstone.Game
    — GameController (IController), MatchSetup, MatchConfig
    — GameRef, TurnPhase, TurnStep
```

---

## 1. State Layer (Immutable Records in Store)

### References (IReference)
```csharp
record GameRef     : IReference          // single game session
record PlayerRef   : IReference          // identifies a player (P1/P2)
record MinionRef   : IReference          // identifies a minion on board
record CardRef     : IReference          // identifies a card instance
```

### State Records (abstract State base)
```csharp
// Per-player slices — all registered under PlayerRef
record HeroState   : State   // CurrentHealth, MaxHealth, Armor, IsHeroPowerUsed, AttacksThisTurn
record ManaState   : State   // CurrentMana, MaxMana
record HandState   : State   // IReadOnlyList<CardRef> Cards (max 10)
record DeckState   : State   // IReadOnlyList<CardRef> Cards (draw from top)
record BoardState  : State   // IReadOnlyList<MinionRef> Minions (max 7)
record WeaponState : State   // Attack, Durability (nullable — hero may have no weapon)

// Per-minion slices — registered under MinionRef
record MinionState : State   // BaseAttack, CurrentAttack, BaseHealth, CurrentHealth,
                             //   Keywords (flags: Taunt, DivineShield, Charge, Rush,
                             //   Windfury, Stealth, Lifesteal, Poisonous, Reborn),
                             //   AttacksThisTurn, MaxAttacksPerTurn,
                             //   HasSummoningSickness, OwnerRef (PlayerRef),
                             //   DefinitionId (string — links to CardDefinition)

// Per-card slices — registered under CardRef
record CardState   : State   // DefinitionId, CurrentCost (may differ from base cost),
                             //   OwnerRef (PlayerRef)

// Global game slice — registered under GameRef
record GameState   : State   // Phase (enum), ActivePlayerRef, WinnerRef (nullable),
                             //   TurnNumber
```

---

## 2. Card Data Model (Non-State, Immutable Definitions)

```csharp
// Pure data — not in Store, loaded from ScriptableObjects or JSON
record CardDefinition
    string Id, string Name, int Cost, CardType Type,
    int? Attack, int? Health,          // minion only
    KeywordSet Keywords,
    string BattlecryId,                // null = no battlecry
    string DeathrattleId,              // null = no deathrattle
    string SecretId                    // null = not a secret

enum CardType { Minion, Spell, Weapon }

// Keyword flags
[Flags] enum Keyword
    None, Taunt, DivineShield, Charge, Rush, Windfury,
    Stealth, Lifesteal, Poisonous, Reborn, Inspire

// Ability interfaces (implemented by named classes, resolved by Id)
interface IBattlecry   { Task Execute(EffectContext ctx, PlayerRef owner, MinionRef? target); }
interface IDeathrattle { Task Execute(EffectContext ctx, MinionRef source); }
interface ISecret      { bool IsTriggered(ContextEvent evt); Task Execute(EffectContext ctx); }
```

---

## 3. Commands Layer (Atomic Mutations)

Each command extends `Command`, holds only the data needed, and executes one `store.Execute<TState>(mutator)` call.

| Command | Mutates | Description |
|---|---|---|
| `DealHeroDamageCommand(PlayerRef, int)` | `HeroState` | Reduces health (absorbs armor first) |
| `HealHeroCommand(PlayerRef, int)` | `HeroState` | Restores health (capped at max) |
| `GainArmorCommand(PlayerRef, int)` | `HeroState` | Adds armor |
| `DrawCardCommand(PlayerRef)` | `DeckState`, `HandState` | Moves top card to hand (or fatigue) |
| `SpendManaCommand(PlayerRef, int)` | `ManaState` | Reduces current mana |
| `GainManaCommand(PlayerRef)` | `ManaState` | Increments max (capped at 10) |
| `RefillManaCommand(PlayerRef)` | `ManaState` | Sets current = max |
| `SummonMinionCommand(MinionRef, PlayerRef, int slot)` | `BoardState`, `MinionState` | Places minion on board |
| `DealMinionDamageCommand(MinionRef, int)` | `MinionState` | Reduces health (divine shield check) |
| `HealMinionCommand(MinionRef, int)` | `MinionState` | Restores health (capped) |
| `DestroyMinionCommand(MinionRef)` | `BoardState`, `MinionState` | Removes minion |
| `SilenceMinionCommand(MinionRef)` | `MinionState` | Strips keywords + resets stats to base |
| `MarkAttackedCommand(MinionRef or PlayerRef)` | `MinionState` / `HeroState` | Increments AttacksThisTurn |
| `ResetAttackCountsCommand(PlayerRef)` | all `MinionState` on board, `HeroState` | Clears attack counts at turn start |
| `RemoveSummoningSicknessCommand(PlayerRef)` | all `MinionState` owned by player | Clears HasSummoningSickness |
| `EquipWeaponCommand(PlayerRef, CardRef)` | `WeaponState`, `HeroState` | Equips weapon, destroys old |
| `DamageWeaponCommand(PlayerRef, int)` | `WeaponState` | Reduces durability |
| `AddCardToHandCommand(PlayerRef, CardRef)` | `HandState` | Adds card (overdraw = burn) |
| `MarkHeroPowerUsedCommand(PlayerRef)` | `HeroState` | Sets IsHeroPowerUsed = true |
| `ResetHeroPowerCommand(PlayerRef)` | `HeroState` | Sets IsHeroPowerUsed = false |

---

## 4. Effects Layer (Orchestrated Workflows)

Each effect extends `Effect`, calls `ExecuteCommand()` (which fires pre/post triggers), and handles validation.

### Turn Effects
```
StartTurnEffect(PlayerRef)
    → GainManaCommand, RefillManaCommand
    → DrawCardEffect
    → RemoveSummoningSicknessCommand
    → ResetAttackCountsCommand
    → ResetHeroPowerCommand

EndTurnEffect(PlayerRef)
    → (fire EndOfTurn triggers via IGameEvents)
    → GameState update (next player, increment turn)
    → StartTurnEffect(opponent)

DrawCardEffect(PlayerRef)
    → DrawCardCommand (or FatigueDamageEffect if deck empty)
```

### Card Play Effects
```
PlayMinionEffect(PlayerRef, CardRef, int slot, MinionRef? target)
    → SpendManaCommand
    → RemoveFromHandCommand
    → SummonMinionCommand
    → BattlecryEffect (if card has battlecry)
    → (IEventBus: MinionPlayedEvent)

PlaySpellEffect(PlayerRef, CardRef, ITarget? target)
    → SpendManaCommand
    → RemoveFromHandCommand
    → SpellEffect (resolved via CardDefinition.SpellId)
    → (IEventBus: SpellCastEvent)

PlayWeaponEffect(PlayerRef, CardRef)
    → SpendManaCommand
    → RemoveFromHandCommand
    → EquipWeaponCommand

HeroPowerEffect(PlayerRef, ITarget? target)
    → SpendManaCommand (cost 2)
    → MarkHeroPowerUsedCommand
    → (execute hero power logic)
```

### Combat Effects
```
AttackEffect(attacker: MinionRef | PlayerRef, defender: MinionRef | PlayerRef)
    → Validate (AttackValidator: taunt, range, summoning sickness, attack count)
    → MarkAttackedCommand(attacker)
    → DealDamageEffect(defender, attackerAttack) — handles divine shield
    → DealDamageEffect(attacker, defenderAttack) — counterattack
    → DamageWeaponCommand if hero attacked with weapon
    → DeathCheckEffect

DealDamageEffect(target, amount)
    → DealHeroDamageCommand or DealMinionDamageCommand
    → (Lifesteal: HealHeroCommand on attacker's owner)

DeathCheckEffect()
    → For each minion with Health <= 0: MinionDeathEffect
    → For hero with Health <= 0: GameOverEffect
    → Simultaneous deaths resolved in board-order

MinionDeathEffect(MinionRef)
    → IEventBus: MinionDiedEvent
    → DestroyMinionCommand
    → DeathrattleEffect (if minion has deathrattle)
    → (Reborn: SummonMinionCommand with 1 HP)
```

---

## 5. Trigger System (via IGameEvents)

`IGameEvents.SubscribeTo<TCommand>(callback)` fires before/after every command execution. This is the seam for all reactive card abilities.

```
TriggerController : IController
    Initialized with: IGameEvents, EffectDirector, Store

    On Initialize():
        // Secrets
        gameEvents.SubscribeTo<SummonMinionCommand>(CheckSecrets)
        gameEvents.SubscribeTo<DealHeroDamageCommand>(CheckSecrets)
        // Inspire (fires after hero power)
        gameEvents.SubscribeTo<MarkHeroPowerUsedCommand>(FireInspire)
```

Each `ISecret` implementation checks `bool IsTriggered(ContextEvent)` — only the first matching secret fires, then it leaves play.

`IDeathrattle` implementations are invoked directly from `MinionDeathEffect` by looking up `CardDefinition.DeathrattleId` in an `IAbilityRegistry`.

---

## 6. Turn Structure

Maps to Scaffold's `TurnHandler` with `CurrentPhase` and `CurrentStep` as `IReference` values.

```
Phases (IReference records):
    MulliganPhase   — swap cards before game starts
    MainPhase       — play cards, attack
    EndPhase        — end-of-turn triggers, pass turn

Steps within MainPhase:
    ActionStep      — waiting for player input
    ResolutionStep  — stack resolving
```

Turn progression is driven by `EndTurnEffect`, which updates `TurnState` via `ITurnHandler.Temporary_PassTurn()` and then fires `StartTurnEffect` for the next player.

---

## 7. Stack & Priority

Hearthstone uses a simplified stack (no true interruptible stack like MTG). Mapping:

- **`PlayService`** manages the play window.
- When a player plays a card/attacks: `PlayService.Play(action)` pushed to stack.
- Priority auto-passes after each action (no opponent response window for most actions).
- **Secrets** are the exception: `TriggerController` checks secrets via `IGameEvents` before the effect completes.
- Stack resolves immediately: `PlayService.CloseWindow()` → `ResolveStack()`.

---

## 8. Game Initialization

```
MatchConfig
    PlayerRef[] Players (2)
    DeckList[] Decks (one per player)
    bool IsCoinFlip

GameController : IController
    async Initialize():
        1. Build Store with all state slices for both players
           (HeroState, ManaState, HandState, DeckState, BoardState, WeaponState per PlayerRef)
        2. Register TriggerController
        3. Run MulliganEffect (3 or 4 card draw, swap option)
        4. Determine first player (coin flip or config)
        5. Fire StartTurnEffect(firstPlayer)

IAbilityRegistry
    — Maps string Id → IBattlecry / IDeathrattle / ISecret implementations
    — Registered at startup; new cards = new definition + new ability class, zero core changes
```

---

## 9. Validation Layer

```
AttackValidator
    — Attacker has attacks remaining (AttacksThisTurn < MaxAttacksPerTurn)
    — Attacker doesn't have summoning sickness (or has Charge)
    — Attacker has attack > 0
    — If opponent has Taunt minions, defender must be one of them
    — Rush minions can only attack minions (not hero)

PlayValidator
    — Player has enough mana (CurrentMana >= card cost)
    — Board not full (BoardState.Minions.Count < 7) for minions
    — Hand not full (HandState.Cards.Count < 10) for drawn cards
    — Card requires target: TargetValidator confirms target validity
    — Hero power: not already used this turn
```

---

## 10. Domain Events (IEventBus — for presentation/logging/tests)

```csharp
record MinionPlayedEvent   : ContextEvent  // PlayerRef, MinionRef, int slot
record MinionDiedEvent     : ContextEvent  // MinionRef, PlayerRef owner
record SpellCastEvent      : ContextEvent  // PlayerRef, CardRef
record AttackEvent         : ContextEvent  // attacker, defender
record CardDrawnEvent      : ContextEvent  // PlayerRef, CardRef
record TurnStartedEvent    : ContextEvent  // PlayerRef
record TurnEndedEvent      : ContextEvent  // PlayerRef
record HeroDamagedEvent    : ContextEvent  // PlayerRef, int amount
record GameOverEvent       : ContextEvent  // PlayerRef winner
record SecretRevealedEvent : ContextEvent  // PlayerRef, string secretId
```

---

## Key Design Decisions

1. **Commands = one store mutation each** — atomic and replayable.
2. **Effects orchestrate, Commands mutate** — Effects never touch Store directly.
3. **`IGameEvents` for triggers** — any command can be reacted to without coupling.
4. **`IAbilityRegistry`** — new cards need zero core changes.
5. **`MinionState` per `MinionRef`** — each minion is a first-class Store entity, not embedded in `BoardState`.
6. **`DeathCheckEffect` after every damage sequence** — batch death processing, matches Hearthstone rules.
7. **Fully async API** — `GameController` exposes `async Task PlayCard(...)`, `async Task Attack(...)`, `async Task EndTurn(...)` for headless testing.

---

## Verification

- Unit test each `Command`: construct state, execute, assert new state.
- Unit test each `Effect` with mock `Store`: verify commands fire in correct order.
- Integration test full turn: `StartTurnEffect` → play minion → attack → `EndTurnEffect`.
- Test triggers: play Battlecry minion, verify battlecry fires; kill Deathrattle minion, verify deathrattle fires.
- Test win condition: hero to 0 HP → `GameOverEvent` fires, `GameState.WinnerRef` set.
- All tests headless — no Unity Editor required.
