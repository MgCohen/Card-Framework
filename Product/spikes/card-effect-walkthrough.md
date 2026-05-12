---
type: spike
status: draft
tags: [#spike, #refoundation, #effects, #commands, #blocks, #walkthrough]
---

# Card Effect Walkthrough — Reference Cards → Commands / Tags / Blocks

> **Status: draft.** Coverage check for the in-flight refoundation pivot (Entity + TagKey + Block + Command). Not a design lock. Picks representative cards from Hearthstone (HS), Legends of Runeterra (LoR), and Magic: The Gathering (MTG) and decomposes each into the model proposed in conversation:
>
> - Entities are `Card : Entity`, `Zone : Entity`, `Player : Entity`, etc., all backed by a `(TagKey<T> → typed value)` slot array.
> - Tag writes go through `Entity.Set<T>(TagKey<T>, T)`; every write enqueues a `TagChangeEvent` into the current Block.
> - Commands open Blocks; commands are the only legitimate cause of tag writes outside of hydration.
> - Hooks subscribe to named engine events (`BeforeDamage`, `AfterPlay`, …) with pre/post slots, LoR-style.
> - The committed Block envelope is what the framework consumes (history, network, replay, UI animation).
>
> Goal: confirm that every mechanic in the reference games maps cleanly to (effect handler → commands → tag writes → block), and surface the cases where it doesn't.

## Snippet conventions

- `Card`, `Zone`, `Player`, `Stack`, `Match` are sealed subclasses of `Entity`. Tag access is via `[Tag]` partial properties (source-gen routes through the bag). Hand-written examples below for clarity.
- Effect handlers are methods on a card-specific class, decorated `[On(EngineEvent.X)]` for triggered abilities or overriding a base method like `Resolve` for one-shot spells.
- `ctx.World.Execute(new XCommand(...))` is how a handler issues a command. Commands open their own Block.
- Tag writes are shown as typed property mutation (`target.Hp -= 6`). Each write is implicitly a `TagChangeEvent(target, "hp", old, new)` appended to the current Block.
- Block notation: `Block(Cause = …) ▸ [event, event, SubBlock(…), …]`.
- "Engine events fired" lists the named hooks the command pipeline invokes; this is the surface authors subscribe to.

---

## 1. Fireball (HS) — vanilla targeted damage

**Card text:** *Spell. 4 mana. Deal 6 damage.*

```csharp
public sealed partial class Fireball : Card
{
    public override void Resolve(EffectContext ctx)
    {
        var target = ctx.RequireTarget<IDamageable>();
        ctx.World.Execute(new DealDamage(source: this, target, amount: 6));
    }
}
```

- **Commands issued:** `DealDamage(source, target, amount)`.
- **Tag writes inside DealDamage.Apply:** `target.Hp -= 6`.
- **Engine events fired:** `BeforeDamage(cmd)` → `AfterDamage(cmd)`.
- **Block envelope:**
  ```
  Block(Cause = SpellResolution{spell: Fireball#17, targets: [hero#3]})
   ▸ SubBlock(Cause = DealDamage{src: 17, tgt: 3, amount: 6})
       ▸ TagChange(entity: 3, key: "hp", 30 → 24)
  ```
- **New requirements surfaced:** target acquisition has to be a separate step that runs before `Resolve` — the target is selected at *cast* time and persists on the spell entity until resolution, because re-targeting on resolution is not legal in most games. Suggests a `Tags.Targets` slot on spell entities (list of `Ref<Entity>`).

---

## 2. Blessing of Kings (HS) — permanent stat buff

**Card text:** *Spell. 4 mana. Give a minion +4/+4.*

```csharp
public sealed partial class BlessingOfKings : Card
{
    public override void Resolve(EffectContext ctx)
    {
        var target = ctx.RequireTarget<Card>();
        ctx.World.Execute(new AttachModifier(
            target,
            modifier: new StatModifier(atk: +4, hp: +4, source: this),
            duration: Duration.Permanent));
    }
}
```

- **Commands issued:** `AttachModifier(target, modifier, duration)`.
- **Tag writes inside Apply:**
  - Appends to `target.Modifiers` (a tag of type `ImmutableList<Modifier>`).
  - Recomputes `target.Attack`, `target.MaxHp`, `target.Hp` from `target.BaseAttack`/`target.BaseHp` + modifiers (Hp adjustment writes the new value; clipping is its own concern).
- **Engine events fired:** `BeforeModifierAttach(cmd)` → `AfterModifierAttach(cmd)`. Recomputation triggers no extra engine event; the resulting tag changes ride in the same Block.
- **Block envelope:**
  ```
  Block(SpellResolution{Blessing, [minion#42]})
   ▸ SubBlock(AttachModifier{target: 42, mod: +4/+4, dur: Permanent})
       ▸ TagChange(42, "modifiers", […], [+{+4/+4}])
       ▸ TagChange(42, "attack", 2, 6)
       ▸ TagChange(42, "maxHp", 3, 7)
       ▸ TagChange(42, "hp", 3, 7)
  ```
- **New requirements surfaced:** there must be a modifier ledger per entity (stored as a tag whose value is an immutable list). Effective values like `Attack` are *not* stored as the base — they're stored as the *current effective value*, recomputed when the modifier list mutates. Folding the modifier into the tag write is what the tag-bag spike calls for; the ledger is what makes the inverse (modifier expires) tractable.

---

## 3. Knife Juggler (HS) — triggered + RNG + targeting

**Card text:** *2-mana 3/2 minion. After you summon a minion, deal 1 damage to a random enemy.*

```csharp
public sealed partial class KnifeJuggler : Card
{
    [On(EngineEvent.AfterPlay, Filter = nameof(IsAllyMinion))]
    public void Trigger(PlayContext play, EffectContext ctx)
    {
        var enemies = ctx.World.Characters(controller: Opponent(this.Controller));
        if (enemies.Count == 0) return;
        var pick = ctx.World.Rng.Pick(enemies);
        ctx.World.Execute(new DealDamage(source: this, target: pick, amount: 1));
    }

    bool IsAllyMinion(PlayContext p) =>
        p.Card.Id != this.Id && p.Card.Controller == this.Controller && p.Card.IsMinion;
}
```

- **Commands issued:** `DealDamage`. (RNG itself is not a command; it's a deterministic draw against the match's seeded RNG, written into the Block as evidence.)
- **Tag writes:** `pick.Hp -= 1` inside DealDamage.
- **Engine events fired:** the *outer* play that triggered this is `AfterPlay`. The handler issues `DealDamage`, which opens its own SubBlock with its own Before/After events.
- **Block envelope:**
  ```
  Block(PlayResolution{player: 1, card: anyMinion})
   ▸ TagChange(anyMinion, "zone", "hand", "battlefield")
   ▸ EngineEvent(AfterPlay, anyMinion)            ← KnifeJuggler trigger fires here
   ▸ SubBlock(TriggeredAbility{src: Juggler#9, trigger: AfterPlay})
       ▸ RngDraw(seed-tick: 142, picked: enemy#5)
       ▸ SubBlock(DealDamage{src: 9, tgt: 5, amount: 1})
           ▸ TagChange(5, "hp", 30, 29)
  ```
- **New requirements surfaced:**
  - `RngDraw` is a Block event in its own right (so replay can re-derive the same pick, and so the client can animate "the knife flew at this target").
  - `Filter` on `[On(...)]` lets the hook reject events it doesn't care about without writing imperative early-returns. Source-gen can lower this to a fast predicate.
  - Triggered abilities open SubBlocks under the event that triggered them, not under the original outer cause — this preserves causality in the history tree.

---

## 4. Argent Squire (HS) / Divine Shield — replacement effect

**Card text:** *1-mana 1/1 minion. Divine Shield* (the first time it would take damage, prevent it instead and remove the shield).

```csharp
// Card just sets the keyword tag at creation.
public sealed partial class ArgentSquire : Card
{
    public override void OnCreate(EffectContext ctx) => DivineShield = true;

    [Tag] public partial bool DivineShield { get; set; }
}

// The keyword's behaviour is a single global hook, not per-card.
public static class DivineShieldKeyword
{
    [On(EngineEvent.BeforeDamage, Priority = HookPriority.Replacement)]
    public static void Block(DealDamage cmd)
    {
        if (cmd.Amount <= 0) return;
        if (cmd.Target is not Card c || !c.DivineShield) return;

        cmd.Amount = 0;
        c.DivineShield = false;
    }
}
```

- **Commands issued:** none by the keyword itself — it mutates the in-flight command and writes a tag.
- **Tag writes (inside the original DealDamage's Block, before Apply):** `target.DivineShield = false`. Apply then sees `cmd.Amount == 0` and skips the `Hp` write (the equality-skip guard in `Entity.Set` makes this a true no-op).
- **Engine events fired:** the hook *runs during* `BeforeDamage`; it doesn't emit anything new.
- **Block envelope (if shield was up):**
  ```
  Block(SpellResolution{Fireball, [squire#11]})
   ▸ SubBlock(DealDamage{src: spell, tgt: 11, amount: 6})
       ▸ TagChange(11, "divineShield", true, false)    ← from the hook
       ▸ (no hp change; cmd.Amount became 0)
  ```
- **New requirements surfaced:**
  - Hooks have **priority bands** (`Replacement`, `PreDamage`, `OnDamage`, `Reaction`). Replacement-band hooks run first and can mutate the command.
  - The Block faithfully records the shield breaking even though no `Hp` change occurred. This is exactly the information the UI needs to play the shield-shatter animation.

---

## 5. Tirion Fordring (HS) — deathrattle + create entity + attach

**Card text:** *6-mana 6/6 minion. Divine Shield, Taunt. Deathrattle: equip a 5/3 Ashbringer.*

```csharp
public sealed partial class Tirion : Card
{
    public override void OnCreate(EffectContext ctx)
    {
        DivineShield = true;
        Taunt = true;
    }

    [On(EngineEvent.AfterDeath, Filter = nameof(IsSelf))]
    public void Deathrattle(DeathContext death, EffectContext ctx)
    {
        var hero = this.Controller.Hero;
        ctx.World.Execute(new CreateEntity(
            definition: Defs.Ashbringer,
            owner: hero.Controller,
            into: hero.WeaponZone));
    }

    bool IsSelf(DeathContext d) => d.Entity.Id == this.Id;

    [Tag] public partial bool DivineShield { get; set; }
    [Tag] public partial bool Taunt { get; set; }
}
```

- **Commands issued:** `CreateEntity(definition, owner, into)`. The weapon-zone semantics (a player can hold one weapon; equipping while one exists destroys the old) is enforced by the **weapon zone's** capacity rule, not by the deathrattle. So creating into a full weapon zone implicitly executes a child `DestroyEntity` first.
- **Tag writes (Tirion path):** `Tirion.Hp` previously written to 0, `Tirion.Zone = "graveyard"`. Death triggers run on `AfterDeath`.
- **Tag writes (Ashbringer path):** new entity allocated → `Tags.Definition`, `Tags.Owner`, `Tags.Zone`, base stats. If old weapon existed, that entity's `Zone` writes to `"graveyard"`.
- **Block envelope:**
  ```
  Block(DealDamage{… → Tirion lethal …})
   ▸ TagChange(Tirion, "hp", 1, 0)
   ▸ TagChange(Tirion, "zone", "battlefield", "graveyard")
   ▸ EngineEvent(AfterDeath, Tirion)
   ▸ SubBlock(Deathrattle{Tirion})
       ▸ SubBlock(CreateEntity{def: Ashbringer, owner: 1, into: weaponZone})
           ▸ (optional) SubBlock(DestroyEntity{oldWeapon}) ▸ …
           ▸ EntityCreated(newId: 88, def: Ashbringer)
           ▸ TagChange(88, "zone", null, "weaponZone")
           ▸ TagChange(88, "attack", 0, 5)
           ▸ TagChange(88, "durability", 0, 3)
  ```
- **New requirements surfaced:**
  - `EntityCreated` is its own Block event (sibling to `TagChange`) — the client needs to know an entity exists before it can render tag changes on it. Initial-state tag changes follow under the same SubBlock so the entity appears fully formed in one envelope.
  - Zones have **capacity / replacement rules** (weapon-zone, hand-cap, board-cap). These belong on the zone definition, not on every card that puts something into the zone. Suggests Zones are real entities with rules attached, not naked lists.
  - "Hero" and "weapon zone" are entities the player owns. The player entity has tag references to its hero, hero power, weapon zone, hand zone, deck zone, graveyard zone, board zone. Same uniform shape.

---

## 6. Druid of the Claw (HS) — Choose One (modal + async player choice)

**Card text:** *5-mana 4/4 minion. Choose One — gain Charge; or +2 Health and Taunt.*

```csharp
public sealed partial class DruidOfTheClaw : Card
{
    public override async Task Resolve(EffectContext ctx)
    {
        var mode = await ctx.World.Execute(new RequestChoice(
            prompter: this.Controller,
            prompt: "Choose One",
            options: [Mode.Charge, Mode.TauntAndHealth]));

        switch (mode)
        {
            case Mode.Charge:
                ctx.World.Execute(new AttachModifier(this,
                    new ChargeKeyword(), Duration.Permanent));
                break;
            case Mode.TauntAndHealth:
                ctx.World.Execute(new AttachModifier(this,
                    new StatModifier(hp: +2), Duration.Permanent));
                ctx.World.Execute(new SetTag(this, Tags.Taunt, true));
                break;
        }
    }

    enum Mode { Charge, TauntAndHealth }
}
```

- **Commands issued:** `RequestChoice(prompter, prompt, options)`, then one of `AttachModifier` / `SetTag` based on the result.
- **Tag writes:**
  - `RequestChoice.Apply` writes `Tags.PendingChoice` on the prompted player — this is what the client polls / observes to know "the game is waiting for me to pick."
  - On response: `Tags.PendingChoice` clears; the chosen mode is recorded as a Block event (`ChoiceMade`) for replay; the resulting modifier/tag writes follow.
- **Engine events fired:** `BeforeChoice` / `AfterChoice` (rare hook surface, exists for cards that modify available options — e.g. "your Choose One cards do both").
- **Block envelope:**
  ```
  Block(SpellResolution{DruidOfTheClaw})
   ▸ TagChange(druid#7, "zone", "hand", "battlefield")
   ▸ SubBlock(RequestChoice{player: 1, prompt: "Choose One", opts: [...]})
       ▸ TagChange(player#1, "pendingChoice", null, ChoicePrompt{…})
       ▸ [BLOCK PAUSES — committed events flush, pipeline awaits client response]
       ▸ ChoiceMade{player: 1, picked: TauntAndHealth}
       ▸ TagChange(player#1, "pendingChoice", ChoicePrompt{…}, null)
   ▸ SubBlock(AttachModifier{druid, +0/+2}) ▸ …
   ▸ SubBlock(SetTag{druid, taunt, true}) ▸ …
  ```
- **New requirements surfaced:**
  - **The command pipeline is async-aware.** `RequestChoice` returns a `Task` that completes when the player responds. The outer Block stays open across the await, but the committed-so-far events flush to subscribers immediately on the pause so the UI can render the prompt.
  - **A Block can flush partially.** Subscribers receive Block updates incrementally (the framework can model this as "the Block has a sequence of envelopes that share a Block ID" rather than one atomic dump). The wire protocol matters here — discuss separately.
  - **There is a "Cousin" effect of Choose One: kicker / alternative cost / modal-double-faced cards.** All collapse to: the cast entity carries a mode tag chosen at cast time; the effect reads it during resolve.

---

## 7. Mystic Shot (LoR) — speed-gated stack interaction

**Card text:** *2-mana Burst spell. Deal 2 to anything.*

```csharp
public sealed partial class MysticShot : Card
{
    [Tag(Key = "spellSpeed")]
    public partial SpellSpeed Speed { get; }  // burst, init from definition

    public override void Resolve(EffectContext ctx)
    {
        var target = ctx.RequireTarget<IDamageable>();
        ctx.World.Execute(new DealDamage(source: this, target, amount: 2));
    }
}
```

- **Commands issued:** `DealDamage`.
- **Tag writes:** standard `target.Hp -= 2`. Plus `MysticShot.Zone` transitions `hand → stack → graveyard` over the cast/resolve lifecycle.
- **Engine events:** `BeforeCast` / `AfterCast` (with speed visible on the spell), `BeforeDamage` / `AfterDamage`.
- **Block envelope:**
  ```
  Block(CastSpell{spell: MysticShot, speed: Burst, targets: [unit#4]})
   ▸ TagChange(MysticShot, "zone", "hand", "stack")
   ▸ SubBlock(ResolveSpell{MysticShot})
       ▸ SubBlock(DealDamage{src: shot, tgt: 4, amount: 2}) ▸ …
   ▸ TagChange(MysticShot, "zone", "stack", "graveyard")
  ```
- **New requirements surfaced:**
  - LoR's stack is a real **Zone entity** (`stack`). Casting a non-burst spell writes its zone to `"stack"`, waits for priority passes, then runs resolution and writes zone to `"graveyard"`. Burst spells move `hand → stack → graveyard` in one atomic Block.
  - Priority and speed are tags on the match / spell, consulted by the cast pipeline.

---

## 8. Ezreal (LoR Champion) — counter accumulation + transform

**Card text:** *Ezreal (3-cost Champion). When you target an enemy with a spell or skill 8+ times, level him up* (transforms into a different card with new ability: "When I'm summoned or you target an enemy, deal 2 to the strongest enemy").

```csharp
public sealed partial class EzrealBase : Card
{
    [Tag] public partial int TargetCount { get; set; }

    [On(EngineEvent.AfterTarget, Filter = nameof(EnemyTargetedByAlly))]
    public void Count(TargetContext t, EffectContext ctx)
    {
        TargetCount += 1;
        if (TargetCount >= 8 && Zone == Zones.Hand /* or wherever Ez is */)
        {
            ctx.World.Execute(new TransformEntity(
                this, into: Defs.EzrealLeveledUp));
        }
    }

    bool EnemyTargetedByAlly(TargetContext t) =>
        t.Target.Controller != this.Controller
        && t.Source.Controller == this.Controller;
}
```

- **Commands issued:** `TransformEntity(target, intoDefinition)`. (`AfterTarget` is an engine event; counting isn't a command.)
- **Tag writes:**
  - `TargetCount` is incremented on every qualifying target.
  - `TransformEntity.Apply` writes `Tags.Definition` to the new definition; recomputes base stats and keywords from the new definition; preserves the runtime id (so references to the entity stay valid post-transform). The `Modifiers` ledger usually clears (transformed cards typically lose modifiers — depends on game rules).
- **Engine events fired:** `BeforeTransform` / `AfterTransform`.
- **Block envelope:**
  ```
  Block(CastSpell{somethingTargetingEnemy})
   ▸ … damage etc …
   ▸ EngineEvent(AfterTarget, …)
   ▸ SubBlock(TriggeredAbility{Ezreal, AfterTarget})
       ▸ TagChange(Ezreal, "targetCount", 7, 8)
       ▸ SubBlock(TransformEntity{Ezreal → LeveledEzreal})
           ▸ TagChange(Ezreal, "definition", base, leveled)
           ▸ TagChange(Ezreal, "attack", 3, 4)
           ▸ TagChange(Ezreal, "hp", 4, 5)
           ▸ TagChange(Ezreal, "modifiers", […], [])
  ```
- **New requirements surfaced:**
  - **Counters live in the bag.** No special "counter system" needed — it's just a numeric tag the card increments.
  - **Transforms preserve identity.** The entity id stays the same; the `Definition` tag swaps. This is critical because other entities may hold tag references to this id (e.g. "this card was created by Ezreal"). Same pattern works for HS hero-power transforms, MTG morph flips, etc.
  - **Targeting is itself an engine event,** not just a parameter of damage. The `AfterTarget` event fires once per target during cast/resolve, independent of whether anything happens to that target. Lets cards count "times I was targeted" without coupling to damage/heal/etc.

---

## 9. Counterspell (MTG) — stack manipulation

**Card text:** *Instant. Counter target spell.*

```csharp
public sealed partial class Counterspell : Card
{
    public override void Resolve(EffectContext ctx)
    {
        var spellOnStack = ctx.RequireTarget<Card>(t => t.Zone == Zones.Stack);
        ctx.World.Execute(new CounterSpell(target: spellOnStack));
    }
}
```

```csharp
public sealed record CounterSpell(Card Target) : Command
{
    public override void Apply(World w)
    {
        // Move target from stack directly to its owner's graveyard,
        // without ever running its Resolve.
        Target.Zone = Zones.Graveyard;  // tag write
    }
}
```

- **Commands issued:** `CounterSpell(target)`.
- **Tag writes:** `target.Zone = "graveyard"`. Crucially, the engine's resolution loop must check `Zone == "stack"` before running a spell's `Resolve` — a countered spell is no longer on the stack, so it never resolves.
- **Engine events fired:** `BeforeCounter` / `AfterCounter`. Replacement-band hooks here let "uncounterable" spells (`Cavern of Souls`-style) reject the command.
- **Block envelope:**
  ```
  Block(CastSpell{Counterspell, targets: [fireball#19 on stack]})
   ▸ TagChange(Counterspell, "zone", "hand", "stack")
   ▸ SubBlock(ResolveSpell{Counterspell})
       ▸ SubBlock(CounterSpell{target: 19})
           ▸ TagChange(19, "zone", "stack", "graveyard")
   ▸ TagChange(Counterspell, "zone", "stack", "graveyard")
  ```
- **New requirements surfaced:**
  - **The stack is just a zone with LIFO resolution semantics.** Pushing a spell = `MoveZone(spell, hand → stack)`. Resolving the top of the stack = a phase the engine drives, not something a card does.
  - **Counter is `Zone := graveyard` plus a precondition check at resolution time** ("is this thing still on the stack?"). No special "cancelled" flag needed; the zone *is* the truth.
  - Replacement-band hooks gate the command before Apply runs, same pattern as Divine Shield.

---

## 10. Lord of Atlantis (MTG) — continuous static aura

**Card text:** *2-mana 2/2 Merfolk. Other Merfolk get +1/+1 and have islandwalk.*

This is the case the chat already flagged as the stress point. Sketch:

```csharp
public sealed partial class LordOfAtlantis : Card
{
    private readonly List<Card> _granted = new();

    [On(EngineEvent.OnZoneEnter, Filter = nameof(SelfOnBattlefield))]
    public void Apply(ZoneChangeContext z, EffectContext ctx)
    {
        foreach (var m in ctx.World.OnBattlefield<Card>()
                             .Where(c => c.Id != this.Id && c.HasSubtype(Sub.Merfolk)))
        {
            Grant(m, ctx);
        }
    }

    [On(EngineEvent.AfterZoneEnter)]
    public void OnAnyEnter(ZoneChangeContext z, EffectContext ctx)
    {
        if (this.Zone != Zones.Battlefield) return;
        if (z.Entity is not Card c) return;
        if (c.Id == this.Id || !c.HasSubtype(Sub.Merfolk)) return;
        if (z.To != Zones.Battlefield) return;
        Grant(c, ctx);
    }

    [On(EngineEvent.OnZoneLeave, Filter = nameof(SelfLeavingBattlefield))]
    public void Revoke(ZoneChangeContext z, EffectContext ctx)
    {
        foreach (var m in _granted) ctx.World.Execute(
            new RemoveModifier(m, sourceId: this.Id));
        _granted.Clear();
    }

    void Grant(Card m, EffectContext ctx)
    {
        ctx.World.Execute(new AttachModifier(m,
            new StatModifier(atk: +1, hp: +1, source: this),
            Duration.WhileSourcePresent));
        ctx.World.Execute(new AttachModifier(m,
            new KeywordModifier(Sub.Islandwalk, source: this),
            Duration.WhileSourcePresent));
        _granted.Add(m);
    }

    bool SelfOnBattlefield(ZoneChangeContext z) =>
        z.Entity.Id == this.Id && z.To == Zones.Battlefield;

    bool SelfLeavingBattlefield(ZoneChangeContext z) =>
        z.Entity.Id == this.Id && z.From == Zones.Battlefield;
}
```

- **Commands issued:** `AttachModifier`, `RemoveModifier`. Many of them — one per affected Merfolk per granted keyword/stat.
- **Tag writes:** the modifier ledger and the recomputed effective stats on every affected entity.
- **Block envelope (Lord enters with 2 Merfolk already present):**
  ```
  Block(PlayCard{Lord, into: battlefield})
   ▸ TagChange(Lord, "zone", "hand", "battlefield")
   ▸ EngineEvent(AfterZoneEnter, Lord)
   ▸ SubBlock(TriggeredAbility{Lord, OnZoneEnter[self]})
       ▸ SubBlock(AttachModifier{merfolk1, +1/+1, src: Lord}) ▸ …
       ▸ SubBlock(AttachModifier{merfolk1, islandwalk, src: Lord}) ▸ …
       ▸ SubBlock(AttachModifier{merfolk2, +1/+1, src: Lord}) ▸ …
       ▸ SubBlock(AttachModifier{merfolk2, islandwalk, src: Lord}) ▸ …
  ```
- **Stress points surfaced:**
  - **Static auras force a scan-on-entry + listen-for-future-entries pattern.** Two hooks per aura source (own-enter applies retroactively; any-enter applies prospectively; own-leave revokes). This is workable but verbose — strong candidate for a framework helper / source-gen pattern (`[Aura(Affects = "Merfolk", Grants = "+1/+1, islandwalk")]`).
  - **`Duration.WhileSourcePresent` requires modifier ↔ source linkage.** Modifiers carry a `sourceId`; when the source leaves the battlefield, the engine sweeps modifiers with that source. Single sweep hook on `OnZoneLeave` handles all "while-X-present" durations.
  - **Per-entity grant tracking (`_granted`) is mutable state on the Lord that isn't a tag.** Two options: (a) keep it in the bag as `Tags.GrantedTo` (immutable list); (b) accept that some handler-local state exists outside the bag. The bag-only purist choice is (a) — it survives serialization for free. Recommend (a).
  - **MTG layers (combat damage uses base power, lifelink applies to damage dealt, etc.) are not modelled by this.** Layered/timestamped continuous effects in MTG are notoriously complex; the framework should explicitly punt on full MTG layer correctness and target the simpler Hearthstone/LoR model where the effective value is always whatever the bag says now.

---

## 11. Wrath of God (MTG) — mass destroy

**Card text:** *4-mana Sorcery. Destroy all creatures. They can't be regenerated.*

```csharp
public sealed partial class WrathOfGod : Card
{
    public override void Resolve(EffectContext ctx)
    {
        foreach (var creature in ctx.World.OnBattlefield<Card>()
                                     .Where(c => c.HasSupertype(Sub.Creature)))
        {
            ctx.World.Execute(new DestroyEntity(creature, regenerable: false));
        }
    }
}
```

- **Commands issued:** N `DestroyEntity` commands (one per creature).
- **Tag writes:** `creature.Zone = "graveyard"` for each. Triggers `AfterDeath` per entity → deathrattles/dies-triggers fire as nested SubBlocks.
- **Engine events:** `BeforeDeath` / `AfterDeath` per creature.
- **Block envelope:**
  ```
  Block(SpellResolution{Wrath})
   ▸ SubBlock(DestroyEntity{c1}) ▸ TagChange(c1, "zone", "battlefield", "graveyard")
                                    EngineEvent(AfterDeath, c1)
                                    ▸ … any deathrattles …
   ▸ SubBlock(DestroyEntity{c2}) ▸ …
   ▸ SubBlock(DestroyEntity{c3}) ▸ …
  ```
- **New requirements surfaced:**
  - **Simultaneous deaths.** In MTG, all creatures die at the same time and dies-triggers go on the stack in APNAP order *after* all are dead. In Hearthstone, deaths process sequentially. The framework should pick one and document it; either is implementable, but the ordering decision shapes how `Wrath`'s loop runs. The snippet above is Hearthstone-style (sequential); MTG-style would split into two passes: zone-write all, then fire all `AfterDeath`s.
  - **A "regenerable" parameter on DestroyEntity** is a command parameter, not a tag — it gates the entity's regeneration-replacement hooks at the moment of destruction.

---

## 12. Untap step / phase change — turn structure as tag writes

**MTG turn structure:** Beginning phase (Untap → Upkeep → Draw) → Main 1 → Combat (5 sub-steps) → Main 2 → End phase (End step → Cleanup).

```csharp
public sealed record AdvancePhase(Phase NextPhase) : Command
{
    public override void Apply(World w)
    {
        w.Match.CurrentPhase = NextPhase;  // tag write on the Match entity

        if (NextPhase == Phase.Untap)
        {
            foreach (var c in w.OnBattlefield<Card>()
                                .Where(c => c.Controller == w.Match.ActivePlayer))
            {
                c.Tapped = false;
            }
        }
    }
}
```

- **Commands issued:** `AdvancePhase(next)`. Engine queues these in order; phase-skipping happens via skip-this-phase hooks on `BeforePhaseChange`.
- **Tag writes:** `match.CurrentPhase`. On `Untap`: every active-player permanent's `Tapped` flips. On `Cleanup`: end-of-turn modifiers are swept (the modifier ledger entries with `Duration.UntilEndOfTurn` are removed; effective stats recomputed).
- **Engine events:** `BeforePhaseChange` / `AfterPhaseChange`, plus phase-specific (`OnUntapStep`, `OnUpkeep`, etc.) for cards that care about a particular phase.
- **Block envelope (start of untap):**
  ```
  Block(AdvancePhase{Cleanup → Untap, activePlayer: 2})
   ▸ TagChange(match, "currentPhase", "cleanup", "untap")
   ▸ TagChange(match, "activePlayer", 1, 2)
   ▸ TagChange(creatureA, "tapped", true, false)
   ▸ TagChange(creatureB, "tapped", true, false)
   ▸ EngineEvent(AfterPhaseChange, Untap)
   ▸ … any "at the beginning of untap step" triggered abilities open SubBlocks …
  ```
- **New requirements surfaced:**
  - **The match itself is an entity** with tags: `currentPhase`, `activePlayer`, `turnNumber`, `priorityPlayer`, … This is what designers reach for when they write "at the beginning of your upkeep" — they subscribe to `OnPhaseChange` filtering on `phase == Upkeep && controller == self`.
  - **Bulk tag writes in one Block are common.** A mass untap touches N permanents in one envelope; the wire protocol should compress these (delta batching).

---

## Aggregate catalogues

### Command surface (minimum viable set)

| Command | Purpose |
|---|---|
| `DealDamage(source, target, amount)` | The most common write. |
| `Heal(source, target, amount)` | Inverse; separate so hooks can distinguish. |
| `AttachModifier(target, modifier, duration)` | Add a stat/keyword/cost modifier. |
| `RemoveModifier(target, modifierId or sourceId)` | Inverse. |
| `MoveZone(entity, toZone, position?)` | Change an entity's zone (hand→stack, stack→graveyard, draws, discards, returns to hand, etc). The single primitive that handles cast/resolve/draw/discard/return/exile. |
| `CreateEntity(definition, owner, into)` | Materialize a new entity from a definition into a zone. |
| `DestroyEntity(entity, regenerable?)` | Move to graveyard with full death pipeline. |
| `TransformEntity(target, intoDefinition)` | Same id, new definition. |
| `SetTag<T>(target, key, value)` | Direct tag write for cases that don't deserve a named command (taunt toggle, frozen flag). |
| `AdvancePhase(next)` | Turn/phase progression. |
| `RequestChoice(prompter, prompt, options)` | Async player input. |
| `RngDraw(rngContext, distribution)` | Recorded random draw — yields a value and a Block event for replay. |
| `CounterSpell(targetOnStack)` | Stack manipulation — could be expressed as `MoveZone` + a precondition, but a named command makes the hook surface (`BeforeCounter`) cleaner. |

Commands that don't need to be primitives because they decompose: `Draw` = `MoveZone(top-of-deck, hand)`; `Discard` = `MoveZone(handCard, graveyard)`; `Return` = `MoveZone(boardCard, hand)`; `Mill` = `MoveZone(top-of-deck, graveyard)`; `Exile` = `MoveZone(*, exile)`; `Equip` = `MoveZone(equipment, weaponZone)`; `Mulligan` = batch `MoveZone`s.

### Tag catalogue (what the bag holds)

Grouped by purpose. None of these are enum-keyed — they're `TagKey<T>` statics declared near the type that owns them.

**Identity / classification**
- `Tags.Definition : CardDefinition` — pointer to the static asset.
- `Tags.Owner : Ref<Player>` — who owns this entity (decklist ownership).
- `Tags.Controller : Ref<Player>` — who currently controls it (mind-controlled minions etc.).

**Spatial**
- `Tags.Zone : Ref<Zone>` — what zone this entity is in.
- `Tags.Position : int` — index within the zone (board left-to-right, deck order, stack depth).

**Intrinsic stats (the bag holds the *effective* value)**
- `Tags.Attack : int`, `Tags.MaxHp : int`, `Tags.Hp : int`, `Tags.Cost : int`, `Tags.Durability : int`.
- Game-specific: `Tags.SpellSpeed : SpellSpeed` (LoR), `Tags.Toughness : int` (MTG), `Tags.Power : int`.

**Base stats (the unmodified value, for recomputation)**
- `Tags.BaseAttack`, `Tags.BaseMaxHp`, `Tags.BaseCost`.

**Modifier ledger**
- `Tags.Modifiers : ImmutableList<Modifier>` — the source-of-truth for recomputing effective stats. Each `Modifier` carries `sourceId`, `payload`, `duration`.

**Keywords (booleans or small enums; keep flat, don't fold into a single bitfield unless network pressure forces it)**
- `Tags.Taunt`, `Tags.DivineShield`, `Tags.Stealth`, `Tags.Charge`, `Tags.Rush`, `Tags.Windfury`, `Tags.Lifesteal`, `Tags.Poisonous`, `Tags.Frozen`, `Tags.Silenced`, `Tags.Tapped`.

**Counters / accumulators**
- `Tags.TargetCount` (Ezreal-style), `Tags.SpellsCastThisGame`, `Tags.DamageDealtThisTurn`, etc. — designer-defined per card or globally.

**Combat / turn state**
- `Tags.AttackedThisTurn`, `Tags.AttacksRemaining`, `Tags.SummoningSick`.

**Choice/prompt state**
- `Tags.PendingChoice : ChoicePrompt?` on Player — the "I owe a response" marker.

**Match-level (on the Match entity)**
- `Tags.CurrentPhase : Phase`, `Tags.ActivePlayer : Ref<Player>`, `Tags.PriorityPlayer : Ref<Player>`, `Tags.TurnNumber : int`, `Tags.RngSeed : ulong`.

**Visibility (Hearthstone "show/hide" pattern)**
- `Tags.VisibleTo : ImmutableSet<Ref<Player>>` — who can see this entity's tag values. Hand-zone entities have `{owner}`, board entities have `{all}`, deck entities have `{}`.

### Block-cause catalogue

What can open a Block. (Commands open Blocks; some non-command system events do too.)

- `CommandBlock(cmd)` — every command. The most common.
- `PhaseBlock(phase, activePlayer)` — phase transitions.
- `TriggeredAbilityBlock(source, trigger, triggerContext)` — when a hook issues commands, those commands' Blocks nest under this.
- `DeathrattleBlock`, `BattlecryBlock` — sugared sub-types of `TriggeredAbilityBlock` with named causes (helps history rendering).
- `StackResolutionBlock(stackEntry)` — popping a thing off the stack and running its effect.
- `MulliganBlock(player)`, `DrawStepBlock(player)`, `CleanupBlock(player)` — phase-internal scopes.
- `CombatDamageBlock(attacker, blocker)` — combat damage step; both directions of damage live in one envelope so the UI can animate the clash.
- `MatchStartBlock`, `MatchEndBlock` — top-level wrappers.

### Engine event catalogue

Pre/post pairs. Hook priority bands: `Replacement` → `PreEffect` → `OnEffect` → `Reaction`.

- `BeforeDamage` / `AfterDamage`
- `BeforeHeal` / `AfterHeal`
- `BeforePlay` / `AfterPlay` (card moves from hand to its destination — battlefield, stack, or resolved-into-graveyard)
- `BeforeCast` / `AfterCast` (spell goes onto the stack)
- `BeforeResolve` / `AfterResolve` (spell's effect runs)
- `BeforeDeath` / `AfterDeath`
- `BeforeEntityCreate` / `AfterEntityCreate`
- `OnZoneEnter` / `OnZoneLeave` (with from/to in context)
- `BeforeTransform` / `AfterTransform`
- `BeforeModifierAttach` / `AfterModifierAttach`
- `BeforePhaseChange` / `AfterPhaseChange`
- `OnAttackDeclared` / `OnBlockDeclared`
- `BeforeCounter` / `AfterCounter`
- `BeforeChoice` / `AfterChoice`
- `OnTarget` / `AfterTarget`
- `OnTurnStart` / `OnTurnEnd`
- `OnMatchStart` / `OnMatchEnd`

### Event-stream event kinds (what lives inside a committed Block)

- `TagChangeEvent(entityId, key, prev, next)`
- `EntityCreatedEvent(id, definitionRef)`
- `EntityDestroyedEvent(id)`
- `EngineEventMarker(name, payload)` — for animation/history rendering (e.g. "Frostbolt resolved against minion#4")
- `ChoiceRequestedEvent`, `ChoiceMadeEvent`
- `RngDrawEvent(seedTick, distribution, result)`
- `SubBlock(block)` — nested Block as an event in the parent
- `BlockMetaEvent(cause, timestamp, actor)` — once per block, at open

---

## Stress points and gaps

1. **Continuous static effects (auras).** Workable with the "scan-on-enter + listen-for-future + revoke-on-leave" pattern, but verbose. Worth a framework helper or source-gen attribute. Layered MTG-style ordering is out of scope.
2. **Simultaneous events.** Wrath-style mass deaths need a "phase 1: write zones, phase 2: fire triggers" split for MTG correctness. Hearthstone-style sequential is simpler. **Decision needed:** which model does the framework canonicalize on?
3. **Async player choice mid-resolution.** Requires the command pipeline to be `Task`-based and Blocks to flush partially. Wire protocol implication: a Block has a stable ID and a sequence of envelopes that may interleave with input.
4. **Hidden information.** `Tags.VisibleTo` on every entity, plus per-tag visibility for tags whose value is hidden even though the entity is visible (face-down cards on the battlefield show stats hidden but position visible). The network layer filters per-recipient before shipping a Block.
5. **Replacement effects with priority.** `HookPriority.Replacement` band handles the common case (Divine Shield, Counterspell). Conflicts (two replacement effects on the same event) need a tie-breaker rule (controller's choice, timestamp). Workable but needs a documented policy.
6. **Modifier semantics across transforms / silence / zone changes.** What happens to a +4/+4 buff when the buffed minion is bounced to hand and replayed? Silenced? Transformed? Each game answers differently. The modifier ledger plus duration rules can express any of these, but the framework needs *defaults* and per-game *overrides*.
7. **The stack as a real zone.** Sound model, but turn-based games that don't have a stack (Hearthstone) shouldn't pay for it. The stack zone is optional per-game; the priority/resolution loop is framework code that runs only if a stack zone exists.
8. **RNG and replay.** Every `RngDraw` is a Block event with seed+tick+result. Replays re-issue the same draws by position. Hidden RNG (opponent's coin flip) ships only the result to clients, not the seed.
9. **Cost modification.** "Costs (1) less" is a modifier on `Tags.Cost`, recomputed on play attempt. Standard modifier pattern.
10. **Targeting validation.** Targets must be validated at *cast* time *and* at *resolve* time (target may have moved, been counterspelled, gained "untargetable"). Both checks live in the cast pipeline; the spell entity's `Tags.Targets` list is updated in the validity check at resolution.

---

## How effects look in code (cohesive snippet)

```csharp
// Definition asset (static): card text, cost, base stats, art ref.
// Lives in a ScriptableObject / JSON / similar. Not bag-backed.
public sealed class FireballDefinition : CardDefinition
{
    public override int  BaseCost => 4;
    public override Type Behavior => typeof(Fireball);
}

// Runtime behaviour (bag-backed entity subclass + effect handlers).
public sealed partial class Fireball : Card
{
    // No special tags beyond what Card already has — Fireball is vanilla.
    public override void Resolve(EffectContext ctx)
    {
        var target = ctx.RequireTarget<IDamageable>();
        ctx.World.Execute(new DealDamage(source: this, target, amount: 6));
    }
}

// Triggered ability example: Knife Juggler.
public sealed partial class KnifeJuggler : Card
{
    [On(EngineEvent.AfterPlay, Filter = nameof(IsAllyMinion))]
    public void Trigger(PlayContext p, EffectContext ctx)
    {
        var enemies = ctx.World.Characters(of: Opponent(this.Controller));
        if (enemies.Count == 0) return;
        var pick = ctx.World.Rng.Pick(enemies);
        ctx.World.Execute(new DealDamage(source: this, target: pick, amount: 1));
    }

    bool IsAllyMinion(PlayContext p) =>
        p.Card.Id != this.Id
        && p.Card.Controller == this.Controller
        && p.Card.IsMinion;
}

// Keyword example: divine shield is a tag + a global engine hook.
public sealed partial class CardWithKeywords : Card
{
    [Tag] public partial bool Taunt        { get; set; }
    [Tag] public partial bool DivineShield { get; set; }
    [Tag] public partial bool Stealth      { get; set; }
    // ...
}

public static class DivineShieldRule
{
    [On(EngineEvent.BeforeDamage, Priority = HookPriority.Replacement)]
    public static void Block(DealDamage cmd)
    {
        if (cmd.Amount > 0 && cmd.Target is Card c && c.DivineShield)
        {
            cmd.Amount = 0;
            c.DivineShield = false;
        }
    }
}

// Command example: AttachModifier.
public sealed record AttachModifier(Entity Target, Modifier Mod, Duration Dur) : Command
{
    public override void Apply(World w)
    {
        var ledger = Target.Modifiers;
        Target.Modifiers = ledger.Add(new ModifierEntry(Mod, Dur, sourceId: Mod.SourceId));
        Recompute.AffectedStats(Target, Mod);
    }
}
```

---

## What I'd like settled next (in rough priority)

1. **Simultaneous-events model** — MTG-style two-pass vs. Hearthstone-style sequential. Drives Wrath, board clears, mass deaths.
2. **Aura sugar** — do we accept the verbose two-hook pattern (snippet #10) or invest in a framework helper / source-gen attribute up front?
3. **Async pipeline + partial Block flush** — the wire/serialization contract for "the Block is open, awaiting input."
4. **Visibility model** — per-entity `VisibleTo` only, or per-tag visibility for face-down stats?
5. **Modifier defaults across zone changes** — what does the framework do by default when a buffed minion is bounced / silenced / transformed?

These are the cross-cutting decisions that constrain everything downstream. Each is a separate spike.
