# Vertical Slice — How to Run

## Headless tests

1. Open the project in Unity 6 (`6000.4.0f1` or matching).
2. `Window → General → Test Runner`.
3. Switch to the **EditMode** tab.
4. Run all tests under `VerticalSlice.Tests`. They should all pass.

Or via batch mode:

```
Unity.exe -batchmode -nographics -projectPath . -runTests \
  -testPlatform EditMode -assemblyNames VerticalSlice.Tests \
  -testResults Logs/test-results.xml -logFile Logs/test.log
```

## Unity scene

`Assets/Sample/VerticalSlice/Unity/Sample.unity` ships with a `SliceBootstrap`
already attached to a GameObject. Open the scene and hit Play. You can
rebuild it any time via `Tools → VerticalSlice → Build Sample Scene`.

## Initial state

The scene builds a small two-player board on Setup. No subclasses — every
game thing is an `Entity` with marker tags.

- **Players (heroes)** — `Player 1` and `Player 2`, each `IsPlayer` with
  `Hp=30 / MaxHp=30 / Armor=0`. The hero IS the player entity; HP lives
  there directly.
- **Per-player zones** — six total: `P1 Hand / P1 Battlefield / P1 Graveyard`
  and `P2 Hand / P2 Battlefield / P2 Graveyard`. Each `IsZone` (the
  graveyards also carry `IsGraveyard`) and owned by its player.
- **Minions on Battlefield** (cards: `IsCard` + `CardName` + stats):
  - P1: `Loot Hoarder` (2/1), `Chillwind Yeti` (4/5)
  - P2: `Argent Squire` (1/1), `Boulderfist Ogre` (6/7)
- **Spells in hand** (cards: `IsCard` + `CardName`, no stats; behavior lives
  in their Effect class):
  - P1 + P2 each hold one `Fireball` and one `Healing Touch`.

You should see three IMGUI panels:

- **Primary State** — the engine's live entities + tags. Players show
  hp/maxHp/armor; minions show attack/hp; zones show owner; spells show
  owner + zone.
- **Mirror State** — rebuilt purely from the wire stream, with the current
  cursor position. Independent of Primary.
- **Event Log** — every wire entry the engine has emitted, indented by block
  nesting. When you scrub back, blocks whose contents haven't been re-applied
  collapse out of view.

Below the panels: a **Zones** view that groups cards by zone for a faster
read on board state.

## Controls

P1 acts; P2 is the target side.

- **Reset** — recreates the whole board (regenerates the entire event log).
- **Fireball → P2 Hero** — `engine.Effects.Run(new Fireball(_p1, _p2))`.
  Opens a `BlockStart("Fireball")` containing a nested
  `BlockStart("DealDamage")` containing the hero's `Hp` `TagChange`.
- **Fireball → Argent Squire** — fires onto the P2 minion that carries the
  DivineShield toggle. Exercises the Replacement-band hook.
- **Fireball → Boulderfist** — plain hit on a 6/7 minion.
- **Healing Touch → P1 Hero** — heals P1 by 4 (clamps to MaxHp).
- **Set P2 Hero HP = 25** — direct tag write. Emits a single `TagChange`
  event at the top of the log (no enclosing scope — bare mutations are
  fine).
- **Divine Shield on Argent Squire** — toggle. With it on, casting Fireball
  at Argent Squire runs the Shield's Replacement-band hook, which fires a
  `ShieldBreak` effect (visible in the log as a nested block) and zeroes
  the damage.
- **P2 Hero Armor +/-** — tunable. Armor on the hero reduces incoming
  damage; runs after Shield's Replacement band.
- **Backlash on P1 Hero** — toggle. After+Reaction hook on `DealDamage`
  fires the `Backlash` effect when P1 deals damage, retaliating 1 back.
  Visible in the log as a `Backlash` block nested inside the original
  `DealDamage` block.
- **Deathrattle on Loot Hoarder** — toggle. After+Reaction hook on
  `MoveCard` to a graveyard fires the `Deathrattle` effect — 2 damage to
  every enemy *minion* (heroes are excluded; the effect filters on
  `IsCard`).
- **Move minion → graveyard buttons** — one per minion. Moves the minion
  to the owner's graveyard. If `Deathrattle` is toggled on the moving
  minion, the AOE fires nested inside the `MoveCard` block.
- **Scrub slider** — drag left to rewind, right to fast-forward. Primary is
  unaffected by scrubbing.

## Wire alphabet

Six concrete event types, no shared "Mutation" base class:

| Event | When | Apply | Unapply |
|---|---|---|---|
| `BlockStartEvent(name, payload)` | An effect or command starts running | (no-op for state) | (no-op for state) |
| `BlockEndEvent` | Same, ending | (no-op) | (no-op) |
| `EntityCreatedEvent(id)` | `world.Create()` | add to dict | remove from dict |
| `EntityDestroyedEvent(id)` | `world.Destroy(e)` | remove | add (empty) |
| `TagSetEvent(id, key, value)` | First write of a key on an entity | write key=value | **remove key** (this is the init-value rewind fix) |
| `TagChangeEvent(id, key, prev, next)` | Subsequent write | write key=next | write key=prev |

Block "kind" (Effect / Command / Trigger / etc.) is not a wire-level enum —
the BlockStart's `Name` is just `runnable.GetType().Name`. Tooling that wants
to categorize can do so by registering names.

## Authoring shape

```csharp
// Effects compose commands. Receive read-only IEffectScope.
public sealed class Fireball : Effect {
    public IEntity Caster, Target;
    public override void Run(IEffectScope scope)
        => scope.Execute(new DealDamage(Caster, Target, 6));
}

// Commands mutate state. Receive ICommandScope (resolves IEntity → mutable Entity).
public sealed class DealDamage : Command {
    public IEntity Source, Target; public int Amount;
    public override void Apply(ICommandScope scope) {
        var t = scope.Resolve(Target);
        t?.SetHp(t.Hp() - Amount);
    }
}

// Hooks subscribe via CommandService. Receive (cmd, IHookScope).
commands.Subscribe<DealDamage>(HookPhase.Before, HookPriority.Replacement,
    (cmd, scope) => {
        if (cmd.Target.DivineShield()) {
            cmd.Amount = 0;
            scope.Run(new ShieldBreak(cmd.Target));
        }
    });
```

`World` is pure substrate — no verbs. All dispatch goes through the services
on `GameEngine`.
