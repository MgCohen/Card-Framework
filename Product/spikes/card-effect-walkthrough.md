---
type: spike
status: draft
tags: [#spike, #refoundation, #effects, #commands, #blocks, #walkthrough]
---

# Card Effect Walkthrough — Reference Cards → Commands / Tags / Blocks (v2)

> **Status: draft, v2.** Rewrite of the original coverage-check after the architectural locks from the refoundation chat. Decomposes representative cards from Hearthstone (HS), Legends of Runeterra (LoR), and Magic: The Gathering (MTG) into the locked model and surfaces remaining open questions. Not a design lock; the model itself is what's being verified.

## Locks this revision assumes

These are decisions the refoundation chat has settled. They constrain everything that follows.

1. **Entity model.** Everything is an `Entity` with an `(TagKey<T> → typed value)` slot bag. Subclasses (`Card : Entity`, `Modifier : Entity`, `Zone : Entity`, `Player : Entity`, `Match : Entity`) carry domain methods and typed `[Tag]` partial properties (source-gen routes through the bag). No fixed enum for tag keys — keys are typed singletons declared anywhere, with stable wire strings.
2. **Modifiers are entities.** Buffs, auras, keyword grants, durations, and conditional effects are all `Modifier : Entity` instances. The target's `Modifiers` tag is a list of modifier IDs. Effective stats (`Attack`, `MaxHp`, etc.) are stored on the target and recomputed whenever the modifier list mutates — the recompute emits TagChanges the UI can animate.
3. **Commands open Blocks.** Every state change is caused by a command. Commands declare a `BlockType` (`PLAY`, `POWER`, `TRIGGER`, `ATTACK`, `DEATHS`, `FATIGUE`, `REVEAL_CARD`, …). Blocks nest when a command issued during another command's pipeline opens its own block. Block type is the framework's discriminator; the command payload is the command's serialized form for display.
4. **Handlers are typed and lean.** Effect handlers are methods decorated with `[On(EngineEvent.X, Self = ?)]`. Signature is `(World, TCommand)`. No context wrappers (`EffectContext`, `DeathContext`, `PlayContext` — all removed). Predicates inline. Source-gen wires the registration.
5. **Hook priority bands.** `Replacement` (mutate / cancel the command) → `PreEffect` → `OnEffect` → `Reaction` (issue new commands).
6. **Server-authoritative everything; dumb client.** Client never computes legality, never predicts, never runs engine logic. All state mutation, target validation, RNG, and legal-move enumeration happen on the server. Client renders the stream.
7. **One ordered log; multiple message families.** Wire alphabet:
   ```
   State deltas:    BlockStart(type, payload), BlockEnd, TagChange,
                    FullEntity, ShowEntity, HideEntity, ChangeEntity
   Player input:    Prompt (server → client), Respond (client → server),
                    Resolved (server → all clients, opponent-visible echo)
   ```
   All interleaved in one ordered stream. Per-recipient projection happens server-side; each client receives its tailored view.
8. **Choices and Options are unified into `Prompt`.** One packet shape, one lifecycle. The `kind` field discriminates (`TurnAction`, `Mulligan`, `Modal`, `Discover`, `Target`). Turn-action prompts supersede on legal-move change; resolution prompts resolve when answered.
9. **Replays apply TagChanges; they do not re-run commands.** Commands' payloads in `BlockStart` are display metadata. RNG result, choice resolution, and any nondeterminism are already baked into the stream as TagChanges.

## Wire alphabet (the entire log grammar)

```
BlockStart(blockType, commandPayload)
BlockEnd
TagChange(entityId, key, prev, next)
FullEntity(id, tags{…})       // new entity visible to recipient
ShowEntity(id, tags{…})       // known entity, tags now visible
HideEntity(id)                // known entity, tags now hidden
ChangeEntity(id, newDef)      // transform — id preserved, definition swapped
Prompt(id, player, kind, options[])
Respond(promptId, picks[])    // client → server
Resolved(promptId, picks[])   // server → all clients, opponent-visible echo
```

That's it. Visibility (`FullEntity` / `ShowEntity` / `HideEntity`) is kept distinct from `TagChange` because the semantics differ — `TagChange` means *state mutated*, the visibility entries mean *recipient's information access changed*.

## Block types (canonical small set)

| `BlockType` | Opens for | Notes |
|---|---|---|
| `PLAY` | A card is played from hand (cast, summon, equip). | Wraps the cast and any spell-on-stack lifecycle. |
| `POWER` | A spell effect or game-mechanical command resolves (damage, heal, draw, create, modifier, move). | The most common block. |
| `TRIGGER` | A triggered ability fires in response to an engine event. | Always nested under the event that triggered it. |
| `ATTACK` | A character declares an attack against a target. | Wraps the combat damage exchange. |
| `DEATHS` | The deaths-processing sweep runs. | Per game's simultaneous-event model (open question). |
| `FATIGUE` | Drawing from an empty deck deals fatigue damage. | Always nested under a `DrawCard` command. |
| `REVEAL_CARD` | A hidden entity becomes visible (mulligan reveal, top-of-deck reveal). | Wraps the `ShowEntity` plus any consequence. |
| `MULLIGAN` | The mulligan window. | Opens once per player per game start. |
| `TURN` | An entire player turn. | Outermost during play; phase changes nest inside. |

Framework-extensible. New games can add block types (e.g. LoR's `CHAMPION_LEVEL` for level-up animations).

## Snippet conventions

- `[Tag]` partial properties on entity subclasses are source-gen'd into bag access. Stable wire keys via `[Tag(Key = "…")]` (analyzer-enforced; no implicit key derivation).
- `[On(EngineEvent.X, Self = bool)]` registers a hook. `Self = true` filters to the entity instance the handler is defined on.
- Hook signature is always `(World w, TCommand cmd)`. The command class IS the context. No wrapper types.
- `w.Execute(new XCommand(...))` issues a command. The command opens a block of its declared `BlockType`.
- Block-tree diagrams below use indentation for nesting. Each line is one wire entry.
- `Defs.X` references a static `CardDefinition` / `ModifierDefinition` asset (the typed static side; not bag-backed).

---

## 1. Fireball (HS) — vanilla targeted damage

**Card text:** *Spell. 4 mana. Deal 6 damage.*

```csharp
public sealed partial class Fireball : Card
{
    [On(EngineEvent.AfterResolveSpell, Self = true)]
    public void Cast(World w, ResolveSpell cmd)
        => w.Execute(new DealDamage(source: this, target: cmd.Targets[0], amount: 6));
}
```

```
BlockStart(PLAY, PlayCard{spell: Fireball#17, targets: [hero#3]})
  TagChange(Fireball#17, zone, hand, stack)
  BlockStart(POWER, ResolveSpell{Fireball, [hero#3]})
    BlockStart(POWER, DealDamage{src: 17, tgt: 3, amount: 6})
      TagChange(hero#3, hp, 30, 24)
    BlockEnd
  BlockEnd
  TagChange(Fireball#17, zone, stack, graveyard)
BlockEnd
```

Targets are selected at cast time and persist on the spell entity (`Tags.Targets` — list of `Ref<Entity>`). Target validation runs at both cast time and resolve time (the resolution can find a target gone if e.g. it died in between).

---

## 2. Blessing of Kings (HS) — modifier as entity

**Card text:** *Spell. 4 mana. Give a minion +4/+4.*

```csharp
public sealed partial class BlessingOfKings : Card
{
    [On(EngineEvent.AfterResolveSpell, Self = true)]
    public void Cast(World w, ResolveSpell cmd)
    {
        w.Execute(new CreateEntity<Modifier>(Defs.BlessingBuff, init: m =>
        {
            m.Target   = cmd.Targets[0];
            m.Source   = this;
            m.AtkDelta = +4;
            m.HpDelta  = +4;
            m.Duration = Duration.Permanent;
        }));
    }
}
```

```
BlockStart(POWER, ResolveSpell{Blessing, [minion#42]})
  BlockStart(POWER, CreateEntity{def: BlessingBuff})
    FullEntity(modifier#88, {kind: Modifier, target: 42, source: Blessing#13,
                             atkDelta: +4, hpDelta: +4, duration: Permanent})
    TagChange(minion#42, modifiers, [], [+88])
    TagChange(minion#42, attack, 2, 6)        ← recompute
    TagChange(minion#42, maxHp, 3, 7)         ← recompute
    TagChange(minion#42, hp, 3, 7)            ← recompute (current hp clipped to maxHp)
  BlockEnd
BlockEnd
```

The modifier is a real entity with its own ID. When silenced, transformed, or expired, the modifier is destroyed and the same recompute pipeline emits the inverse TagChanges. Modifier search is uniform: `w.Entities<Modifier>().Where(m => m.Target == card)`.

---

## 3. Knife Juggler (HS) — triggered + RNG

**Card text:** *2-mana 3/2. After you summon a minion, deal 1 damage to a random enemy.*

```csharp
public sealed partial class KnifeJuggler : Card
{
    [On(EngineEvent.AfterPlay)]
    public void Trigger(World w, PlayCard cmd)
    {
        if (cmd.Card.Id == this.Id) return;
        if (cmd.Card.Controller != this.Controller) return;
        if (!cmd.Card.IsMinion) return;

        var enemies = w.Characters(of: w.Opponent(this.Controller));
        if (enemies.Count == 0) return;

        var pick = w.Rng.Pick(enemies);
        w.Execute(new DealDamage(source: this, target: pick, amount: 1));
    }
}
```

```
BlockStart(PLAY, PlayCard{anyAllyMinion})
  TagChange(anyAllyMinion, zone, hand, battlefield)
  BlockStart(TRIGGER, TriggeredBy{src: Juggler#9, event: AfterPlay})
    TagChange(match, rngTick, 141, 142)
    BlockStart(POWER, DealDamage{src: 9, tgt: enemy#5, amount: 1})
      TagChange(enemy#5, hp, 30, 29)
    BlockEnd
  BlockEnd
BlockEnd
```

RNG state lives on the `Match` entity (`Tags.RngSeed`, `Tags.RngTick`). Every draw advances the tick. Replays re-derive the same draws by position. No separate `RngDraw` wire entry — the result manifests as the TagChanges it caused.

---

## 4. Argent Squire (HS) / Divine Shield — replacement effect

**Card text:** *1-mana 1/1. Divine Shield* (prevent first damage; lose shield).

```csharp
public sealed partial class ArgentSquire : Card
{
    [On(EngineEvent.AfterCreate, Self = true)]
    public void Init(World w, CreateEntity cmd) => DivineShield = true;

    [Tag] public partial bool DivineShield { get; set; }
}

public static class DivineShieldRule
{
    [On(EngineEvent.BeforeDamage, Priority = HookPriority.Replacement)]
    public static void Block(World w, DealDamage cmd)
    {
        if (cmd.Amount <= 0) return;
        if (cmd.Target is not Card c || !c.DivineShield) return;

        cmd.Amount = 0;
        c.DivineShield = false;
    }
}
```

```
BlockStart(POWER, DealDamage{src: spell, tgt: squire#11, amount: 6})
  TagChange(squire#11, divineShield, true, false)
  (no hp change — cmd.Amount became 0, equality-skip elides the write)
BlockEnd
```

Keyword behavior is a single static hook, not per-card code. The Replacement-band hook fires during `BeforeDamage`, mutates `cmd.Amount`, writes the shield tag. The Apply phase sees `Amount == 0` and the `Entity.Set` equality-skip makes the `Hp` write a no-op.

---

## 5. Tirion Fordring (HS) — deathrattle + create entity + attach

**Card text:** *6-mana 6/6. Divine Shield, Taunt. Deathrattle: equip a 5/3 Ashbringer.*

```csharp
public sealed partial class Tirion : Card
{
    [On(EngineEvent.AfterCreate, Self = true)]
    public void Init(World w, CreateEntity cmd)
    {
        DivineShield = true;
        Taunt = true;
    }

    [On(EngineEvent.AfterDeath, Self = true)]
    public void Deathrattle(World w, DestroyEntity cmd)
    {
        w.Execute(new CreateEntity<Weapon>(Defs.Ashbringer, init: weapon =>
        {
            weapon.Controller = this.Controller;
            weapon.Zone = this.Controller.WeaponZone;
        }));
    }

    [Tag] public partial bool DivineShield { get; set; }
    [Tag] public partial bool Taunt { get; set; }
}
```

```
BlockStart(POWER, DealDamage{… lethal damage to Tirion …})
  TagChange(Tirion, hp, 1, 0)
BlockEnd
BlockStart(DEATHS, ProcessDeaths{[Tirion]})
  TagChange(Tirion, zone, battlefield, graveyard)
  BlockStart(TRIGGER, TriggeredBy{src: Tirion, event: AfterDeath})
    BlockStart(POWER, CreateEntity{def: Ashbringer})
      (if old weapon existed: nested DestroyEntity block here)
      FullEntity(weapon#88, {kind: Weapon, controller: 1, zone: weaponZone,
                             attack: 5, durability: 3})
    BlockEnd
  BlockEnd
BlockEnd
```

Weapon-zone capacity rules (one weapon at a time → old one destroys on replace) belong on the zone definition, not on the deathrattle. The deathrattle just creates into the zone; the zone enforces capacity. Same pattern applies to hand cap, board cap.

---

## 6. Druid of the Claw (HS) — Choose One via unified Prompt

**Card text:** *5-mana 4/4. Choose One — gain Charge; or +2 Health and Taunt.*

```csharp
public sealed partial class DruidOfTheClaw : Card
{
    [On(EngineEvent.AfterResolveSpell, Self = true)]
    public async Task Resolve(World w, ResolveSpell cmd)
    {
        var pick = await w.Prompt(
            player: this.Controller,
            kind: PromptKind.Modal,
            options: [
                new Option.Modal("charge",  "Charge"),
                new Option.Modal("tauntHp", "+2 Health and Taunt"),
            ]);

        var def = pick.OptionId == "charge" ? Defs.ChargeBuff : Defs.TauntHpBuff;
        w.Execute(new CreateEntity<Modifier>(def, m =>
        {
            m.Target = this;
            m.Source = this;
            if (pick.OptionId == "charge")
            {
                m.GrantsKeyword = Keyword.Charge;
            }
            else
            {
                m.HpDelta = +2;
                m.GrantsKeyword = Keyword.Taunt;
            }
        }));
    }
}
```

```
BlockStart(PLAY, PlayCard{Druid})
  TagChange(Druid, zone, hand, battlefield)
  BlockStart(POWER, ResolveSpell{Druid})
    Prompt(id: 71, player: 1, kind: Modal,
           options: [{id: "charge"}, {id: "tauntHp"}])
    ← stream flushes; engine awaits Respond
    Respond(71, ["tauntHp"])
    Resolved(71, ["tauntHp"])         ← opponent-visible echo
    BlockStart(POWER, CreateEntity{def: TauntHpBuff})
      FullEntity(modifier#92, {target: Druid, source: Druid, hpDelta: +2,
                               grants: Taunt})
      TagChange(Druid, modifiers, [], [+92])
      TagChange(Druid, maxHp, 4, 6)
      TagChange(Druid, hp, 4, 6)
      TagChange(Druid, taunt, false, true)
    BlockEnd
  BlockEnd
BlockEnd
```

Same `Prompt` packet carries a turn-action menu at the start of a player's priority (`kind: TurnAction`), a mulligan prompt (`kind: Mulligan`), a target prompt (`kind: Target`). One lifecycle. The block stays open across the await; events committed so far flush to subscribers (the UI can render the modal immediately).

---

## 7. Mystic Shot (LoR) — speed-gated stack interaction

**Card text:** *2-mana Burst spell. Deal 2 to anything.*

```csharp
public sealed partial class MysticShot : Card
{
    [Tag(Key = "spellSpeed")]
    public partial SpellSpeed Speed { get; }    // init from definition

    [On(EngineEvent.AfterResolveSpell, Self = true)]
    public void Cast(World w, ResolveSpell cmd)
        => w.Execute(new DealDamage(source: this, target: cmd.Targets[0], amount: 2));
}
```

```
BlockStart(PLAY, PlayCard{MysticShot, speed: Burst, targets: [unit#4]})
  TagChange(MysticShot, zone, hand, stack)
  BlockStart(POWER, ResolveSpell{MysticShot, [unit#4]})
    BlockStart(POWER, DealDamage{src: shot, tgt: 4, amount: 2})
      TagChange(unit#4, hp, 4, 2)
    BlockEnd
  BlockEnd
  TagChange(MysticShot, zone, stack, graveyard)
BlockEnd
```

The stack is a real `Zone` entity. Non-burst spells move `hand → stack`, wait for priority passes, then resolve and move `stack → graveyard`. Burst spells move `hand → stack → graveyard` in one atomic block. Priority is a tag on the `Match` entity consulted by the cast pipeline.

---

## 8. Ezreal (LoR) — counter + transform

**Card text:** *Ezreal (3-cost Champion). When you target an enemy with a spell or skill 8+ times, level him up.*

```csharp
public sealed partial class EzrealBase : Card
{
    [Tag] public partial int TargetCount { get; set; }

    [On(EngineEvent.AfterTarget)]
    public void Count(World w, TargetEntity cmd)
    {
        if (cmd.Target.Controller == this.Controller) return;
        if (cmd.Source.Controller != this.Controller) return;

        TargetCount += 1;
        if (TargetCount >= 8)
            w.Execute(new ChangeEntity(this, into: Defs.EzrealLeveledUp));
    }
}
```

```
BlockStart(PLAY, PlayCard{someSpell, targets: [enemy#5]})
  …
  BlockStart(TRIGGER, TriggeredBy{src: Ezreal, event: AfterTarget})
    TagChange(Ezreal, targetCount, 7, 8)
    BlockStart(POWER, ChangeEntity{Ezreal → LeveledEzreal})
      ChangeEntity(Ezreal, def: LeveledEzreal)
      TagChange(Ezreal, attack, 3, 4)
      TagChange(Ezreal, maxHp, 4, 5)
      TagChange(Ezreal, hp, 4, 5)
      (modifiers retained or cleared per game rules — game-config decision)
    BlockEnd
  BlockEnd
BlockEnd
```

`ChangeEntity` is a distinct wire entry from `TagChange` because the entity's *definition* changes — clients may need to rebind art, audio, and the entity's class identity. The ID is preserved; references to it (modifiers, history, opponents' targeting) stay valid.

---

## 9. Counterspell (MTG) — stack manipulation

**Card text:** *Instant. Counter target spell.*

```csharp
public sealed partial class Counterspell : Card
{
    [On(EngineEvent.AfterResolveSpell, Self = true)]
    public void Cast(World w, ResolveSpell cmd)
    {
        if (cmd.Targets[0] is not Card spellOnStack) return;
        if (spellOnStack.Zone != w.Stack) return;
        w.Execute(new MoveZone(spellOnStack, to: spellOnStack.Controller.Graveyard));
    }
}
```

```
BlockStart(PLAY, PlayCard{Counterspell, target: fireball#19})
  TagChange(Counterspell, zone, hand, stack)
  BlockStart(POWER, ResolveSpell{Counterspell, [fireball#19]})
    BlockStart(POWER, MoveZone{entity: 19, to: graveyard})
      TagChange(fireball#19, zone, stack, graveyard)
    BlockEnd
  BlockEnd
  TagChange(Counterspell, zone, stack, graveyard)
BlockEnd
```

No special `CounterSpell` command needed. Counter is just `MoveZone` to graveyard. The stack-resolution loop checks `Zone == w.Stack` before invoking a spell's resolve, so a countered spell never runs its effect — its precondition fails.

---

## 10. Lord of Atlantis (MTG) — continuous static aura

**Card text:** *2-mana 2/2 Merfolk. Other Merfolk get +1/+1 and have islandwalk.*

```csharp
public sealed partial class LordOfAtlantis : Card
{
    [On(EngineEvent.AfterZoneChange)]
    public void OnAnyZoneChange(World w, MoveZone cmd)
    {
        if (this.Zone != Zones.Battlefield) return;

        // Self entered battlefield: scan existing Merfolk and grant.
        if (cmd.Entity.Id == this.Id && cmd.To == Zones.Battlefield)
        {
            foreach (var m in w.OnBattlefield<Card>()
                                .Where(c => c.Id != this.Id && c.HasSubtype(Sub.Merfolk)))
                GrantTo(w, m);
            return;
        }

        // Another Merfolk entered: grant to it.
        if (cmd.To == Zones.Battlefield
            && cmd.Entity is Card other
            && other.Id != this.Id
            && other.HasSubtype(Sub.Merfolk))
            GrantTo(w, other);
    }

    [On(EngineEvent.BeforeZoneChange, Self = true)]
    public void OnSelfLeave(World w, MoveZone cmd)
    {
        if (cmd.From != Zones.Battlefield || cmd.To == Zones.Battlefield) return;
        foreach (var mod in w.Entities<Modifier>().Where(m => m.Source.Id == this.Id))
            w.Execute(new DestroyEntity(mod));
    }

    void GrantTo(World w, Card target)
    {
        w.Execute(new CreateEntity<Modifier>(Defs.LordBuff, m =>
        {
            m.Target = target;
            m.Source = this;
            m.AtkDelta = +1;
            m.HpDelta = +1;
            m.GrantsKeyword = Keyword.Islandwalk;
            m.Duration = Duration.WhileSourcePresent;
        }));
    }
}
```

```
BlockStart(PLAY, PlayCard{Lord, into: battlefield})
  TagChange(Lord, zone, hand, battlefield)
  BlockStart(TRIGGER, TriggeredBy{src: Lord, event: AfterZoneChange[self]})
    BlockStart(POWER, CreateEntity{def: LordBuff, target: merfolk1})
      FullEntity(mod#101, {target: m1, source: Lord, atk: +1, hp: +1, grants: Islandwalk})
      TagChange(m1, modifiers, [], [+101])
      TagChange(m1, attack, 2, 3)
      TagChange(m1, maxHp, 2, 3)
      TagChange(m1, hp, 2, 3)
    BlockEnd
    BlockStart(POWER, CreateEntity{def: LordBuff, target: merfolk2}) … BlockEnd
  BlockEnd
BlockEnd
```

The verbose two-hook pattern (self-entered scan + others-entering listen + self-leaving sweep) is the cost of "modifiers-as-entities" for continuous effects. Strong candidate for a framework helper (`[Aura(Affects = "Merfolk", Grants = "+1/+1, islandwalk")]`) that generates this boilerplate. Out of scope for this spike; flagged as an open question.

---

## 11. Wrath of God (MTG) — mass destroy

**Card text:** *4-mana Sorcery. Destroy all creatures.*

```csharp
public sealed partial class WrathOfGod : Card
{
    [On(EngineEvent.AfterResolveSpell, Self = true)]
    public void Cast(World w, ResolveSpell cmd)
    {
        foreach (var creature in w.OnBattlefield<Card>()
                                    .Where(c => c.HasSupertype(Sub.Creature)))
            w.Execute(new DestroyEntity(creature));
    }
}
```

```
BlockStart(POWER, ResolveSpell{Wrath})
  BlockStart(POWER, DestroyEntity{c1}) ▸ TagChange(c1, hp, x, 0) BlockEnd
  BlockStart(POWER, DestroyEntity{c2}) ▸ TagChange(c2, hp, x, 0) BlockEnd
  BlockStart(POWER, DestroyEntity{c3}) ▸ TagChange(c3, hp, x, 0) BlockEnd
BlockEnd
BlockStart(DEATHS, ProcessDeaths{[c1, c2, c3]})
  TagChange(c1, zone, battlefield, graveyard)
  TagChange(c2, zone, battlefield, graveyard)
  TagChange(c3, zone, battlefield, graveyard)
  BlockStart(TRIGGER, TriggeredBy{src: c1, event: AfterDeath}) … BlockEnd
  BlockStart(TRIGGER, TriggeredBy{src: c2, event: AfterDeath}) … BlockEnd
  BlockStart(TRIGGER, TriggeredBy{src: c3, event: AfterDeath}) … BlockEnd
BlockEnd
```

The snippet above is **MTG-style two-pass** (all marked-as-dying, then all zone-moves, then all triggers). Hearthstone-style is sequential (per-entity death + trigger). The block separation makes this configurable — `DEATHS` is its own block opened by the framework's deaths-sweep, which the game can configure as one-shot-all-then-triggers or per-entity-sequential. Decision still open.

---

## 12. Untap step / phase change — turn structure as tag writes

```csharp
public sealed record AdvancePhase(Phase Next) : Command
{
    public override BlockType BlockType => BlockType.POWER;

    public override void Apply(World w)
    {
        w.Match.CurrentPhase = Next;
        if (Next == Phase.Untap)
        {
            foreach (var c in w.OnBattlefield<Card>()
                                .Where(c => c.Controller == w.Match.ActivePlayer))
                c.Tapped = false;
        }
    }
}
```

```
BlockStart(TURN, AdvanceTurn{player: 2})
  BlockStart(POWER, AdvancePhase{Untap})
    TagChange(match, currentPhase, cleanup, untap)
    TagChange(match, activePlayer, 1, 2)
    TagChange(creatureA, tapped, true, false)
    TagChange(creatureB, tapped, true, false)
    BlockStart(TRIGGER, TriggeredBy{event: OnUntapStep}) … BlockEnd
  BlockEnd
  BlockStart(POWER, AdvancePhase{Upkeep}) … BlockEnd
  …
BlockEnd
```

The `Match` is an entity whose tags carry global turn state (`CurrentPhase`, `ActivePlayer`, `PriorityPlayer`, `TurnNumber`, `RngSeed`, `RngTick`). Designers writing "at the beginning of your upkeep" subscribe to `OnPhaseChange` filtering on phase and controller.

---

## Aggregate catalogues

### Command surface

Two layers:

**Primitives** (the engine's atomic verbs):

| Command | Purpose | BlockType |
|---|---|---|
| `SetTag<T>(entity, key, value)` | Direct tag write for cases that don't deserve a named command | `POWER` |
| `MoveZone(entity, to, position?)` | The only legitimate way to change `Tags.Zone`. Subsumes draw, discard, return, mill, exile, equip, counter | `POWER` |
| `CreateEntity<T>(def, init?)` | Instantiate a new entity from a definition | `POWER` |
| `DestroyEntity(entity)` | Mark an entity for death; the deaths-sweep handles the zone move and triggers | `POWER` |
| `ChangeEntity(entity, newDef)` | Transform — same ID, new definition | `POWER` |
| `Prompt(player, kind, options)` | Suspend and await player input | (within parent block) |
| `AdvancePhase(next)` | Phase progression | `POWER` (typically within `TURN`) |

**Semantic conveniences** (typed for hook discrimination, decompose into primitives):

| Command | Decomposes to | Why it exists |
|---|---|---|
| `DealDamage` | `SetTag(target, hp, hp - amount)` | Hooks need to distinguish damage from healing, spell damage from combat damage |
| `Heal` | `SetTag(target, hp, hp + amount)` | Inverse of damage; separate hook surface |
| `DrawCard(player, count)` | `MoveZone(topOfDeck, hand)` × N | Distinct event for "draw triggers"; fatigue logic |
| `PlayCard(card, target?)` | Validate; `MoveZone(card, destination)`; resolve | The entire cast pipeline; opens a `PLAY` block |
| `ResolveSpell(spell)` | Run the spell's effect | Opens a `POWER` block; triggers `AfterResolveSpell` |
| `Attack(attacker, target)` | Combat damage exchange | Opens an `ATTACK` block |
| `Discard(card)` | `MoveZone(card, owner.Graveyard)` | Distinct event for discard triggers |
| `Counter(spellOnStack)` | `MoveZone(spell, controller.Graveyard)` | Distinct hook surface for "uncounterable" |

The semantic conveniences are *open-ended*. Games add as many as their hook surface needs (LoR's `IndirectDamage`, MTG's `Combat Damage` distinct from `Noncombat Damage`, etc.). The wire cost is small because the payload is just `{type, params}`.

### Tag catalogue (what the bag holds)

All `TagKey<T>` statics declared near the type that owns them. No enum.

**Identity**: `Definition` (Ref), `Owner` (Ref<Player>), `Controller` (Ref<Player>), `Kind` (EntityKind tag).

**Spatial**: `Zone` (Ref<Zone>), `Position` (int).

**Effective stats** (stored, recomputed on modifier change): `Attack`, `MaxHp`, `Hp`, `Cost`, `Durability`, plus game-specific (`Power`, `Toughness`, `SpellSpeed`).

**Base stats** (the unmodified value, for recomputation): `BaseAttack`, `BaseMaxHp`, `BaseCost`.

**Modifier ledger**: `Modifiers : ImmutableList<Ref<Modifier>>` on the target. The `Modifier` entity carries `Target`, `Source`, `Duration`, plus payload (`AtkDelta`, `HpDelta`, `GrantsKeyword`, `CostDelta`, …).

**Keywords**: `Taunt`, `DivineShield`, `Stealth`, `Charge`, `Rush`, `Windfury`, `Lifesteal`, `Poisonous`, `Frozen`, `Silenced`, `Tapped`, `Elusive`, ... (booleans or small enums; one tag per keyword).

**Counters**: per-card or global (`TargetCount`, `SpellsCastThisGame`, `DamageDealtThisTurn`). Designer-defined.

**Combat state**: `AttackedThisTurn`, `AttacksRemaining`, `SummoningSick`.

**Visibility** (server-side; clients don't see this tag — it drives projection): `VisibleTo : ImmutableSet<Ref<Player>>`.

**Match-level** (on the `Match` entity): `CurrentPhase`, `ActivePlayer`, `PriorityPlayer`, `TurnNumber`, `RngSeed`, `RngTick`.

### Engine event catalogue

Pre/post pairs. Hook priority bands: `Replacement` → `PreEffect` → `OnEffect` → `Reaction`.

| Pre | Post | Carried Command |
|---|---|---|
| `BeforeDamage` | `AfterDamage` | `DealDamage` |
| `BeforeHeal` | `AfterHeal` | `Heal` |
| `BeforePlay` | `AfterPlay` | `PlayCard` |
| `BeforeCast` | `AfterCast` | (spell movement onto stack) |
| `BeforeResolveSpell` | `AfterResolveSpell` | `ResolveSpell` |
| `BeforeDeath` | `AfterDeath` | `DestroyEntity` |
| `BeforeCreate` | `AfterCreate` | `CreateEntity` |
| `BeforeZoneChange` | `AfterZoneChange` | `MoveZone` |
| `BeforeTransform` | `AfterTransform` | `ChangeEntity` |
| `BeforePhaseChange` | `AfterPhaseChange` | `AdvancePhase` |
| `BeforeTarget` | `AfterTarget` | `TargetEntity` |
| `BeforeAttack` | `AfterAttack` | `Attack` |
| `BeforePrompt` | `AfterPrompt` | `Prompt` |

Convention: hook on `AfterX` for "react to" effects (triggered abilities), hook on `BeforeX` with `Priority = Replacement` for "prevent / modify" effects (Divine Shield, indestructible, cost modifiers).

### Wire entry kinds (already enumerated above)

```
BlockStart, BlockEnd, TagChange, FullEntity, ShowEntity, HideEntity, ChangeEntity,
Prompt, Respond, Resolved
```

Ten kinds. Same alphabet for every game.

---

## How effects look in code (cohesive snippet)

```csharp
// Static definition asset — typed, not bag-backed.
public sealed class FireballDefinition : CardDefinition
{
    public override int  BaseCost => 4;
    public override Type Behavior => typeof(Fireball);
}

// Runtime entity — bag-backed, with effect handlers.
public sealed partial class Fireball : Card
{
    [On(EngineEvent.AfterResolveSpell, Self = true)]
    public void Cast(World w, ResolveSpell cmd)
        => w.Execute(new DealDamage(source: this, target: cmd.Targets[0], amount: 6));
}

// Modifier — entity, with its own tags.
public sealed partial class Modifier : Entity
{
    [Tag] public partial Ref<Entity> Target        { get; set; }
    [Tag] public partial Ref<Entity> Source        { get; set; }
    [Tag] public partial Duration    Duration      { get; set; }
    [Tag] public partial int         AtkDelta      { get; set; }
    [Tag] public partial int         HpDelta       { get; set; }
    [Tag] public partial int         CostDelta     { get; set; }
    [Tag] public partial Keyword?    GrantsKeyword { get; set; }
}

// Keyword behavior — single static rule, registered globally.
public static class DivineShieldRule
{
    [On(EngineEvent.BeforeDamage, Priority = HookPriority.Replacement)]
    public static void Block(World w, DealDamage cmd)
    {
        if (cmd.Amount > 0 && cmd.Target is Card c && c.DivineShield)
        {
            cmd.Amount = 0;
            c.DivineShield = false;
        }
    }
}

// Command — typed C# record, declares its BlockType, has typed Apply.
public sealed record DealDamage(Entity Source, Entity Target, int Amount) : Command
{
    public override BlockType BlockType => BlockType.POWER;
    public override void Apply(World w) => ((Character)Target).Hp -= Amount;
}
```

---

## Open questions

The walkthrough shook out the cross-cutting decisions that still need to land. Ranked by how much they constrain everything else.

1. **Simultaneous-events model.** MTG-style two-pass (all zone-writes, then all triggers, in one `DEATHS` block) vs. Hearthstone-style sequential (one death → triggers → next death). The `DEATHS` block separation makes this configurable per game, but a default is needed. **Recommendation:** default to Hearthstone-style sequential; let games opt into two-pass via a config flag on the deaths-sweep.
2. **Aura sugar.** The Lord of Atlantis snippet (§10) is verbose because the two-hook + sweep pattern is mechanical. A framework helper / source-gen attribute (`[Aura(Affects = …, Grants = …)]`) would emit the boilerplate. Worth scoping a dedicated spike.
3. **Async pipeline contract.** `Prompt` returns `Task<PromptResponse>`. The outer block stays open across the `await`. **Open:** does the block flush its accumulated events partially when it suspends (so the UI can render the modal immediately), or does it batch and ship only on `BlockEnd`? Hearthstone partial-flushes; I'd default to the same.
4. **Visibility model details.** `Tags.VisibleTo : ImmutableSet<Ref<Player>>` is the server-side projection input. **Open:** per-tag visibility for cases like face-down cards (the entity is visible, but its stats are hidden), or per-entity only? Hearthstone has per-entity; LoR has both (face-down landmarks). **Recommendation:** start per-entity; add per-tag overlay if a game forces it.
5. **Modifier defaults across silence / transform / bounce.** What does the framework do by default when a buffed minion is silenced? Transformed? Returned to hand? Each game answers differently. **Recommendation:** default = silence removes all modifiers; transform keeps modifiers on the entity (since ID is preserved); bounce removes all modifiers (re-summoned card has fresh state).
6. **Counter durability / charges as tags.** A weapon's `Durability` is just a tag; combat damage decrements it; reaching 0 triggers destroy. No special "weapon system." Worth confirming the model handles the edge cases (deathrattles on weapons, weapon-buff cards like Captain Greenskin).
7. **Position within a zone.** `Tags.Position : int` on each entity, or `Zone.Members : ImmutableList<Ref<Entity>>` on the zone? The latter localizes position to the zone but means every move is a two-write update (entity's `Zone` + zone's `Members`). The former is one write but makes "what's in this zone" a query, not a lookup. **Recommendation:** zone owns the members list; entity's `Zone` tag is a backref for fast "where am I" queries.
8. **Player-input timeouts.** A `Prompt` open across an `await` has no built-in timeout. The framework needs a policy: idle timeout → server picks a default option (mulligan keep-all, choose-one pick-first)? Connection-loss → pause vs. forfeit? **Recommendation:** policy is per-game-config; the framework provides the hook points.

---

## What's next (suggested ordering)

1. **ADR or design lock for the wire alphabet.** The ten-entry log grammar is the protocol contract; everything downstream depends on it. Should not move once samples start.
2. **Auras spike** — design the helper/attribute. Lord of Atlantis is representative of dozens of MTG/HS/LoR cards.
3. **Simultaneous-events ADR.** Pick the default; document the config knob.
4. **Visibility spike.** Map every "hidden" mechanic across the three reference games (hand, deck, opponent's mulligan, face-down landmarks, top-decked predicted cards) and confirm `VisibleTo` + projection handles all of them.
5. **Modifier semantics ADR.** Lock the defaults for silence / transform / bounce; document per-game overrides.

These five are the cross-cutting locks that constrain everything else. After they settle, sample vertical-slice work can start without risk of architectural rework.
