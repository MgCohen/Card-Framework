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

You should see three IMGUI panels:

- **Primary State** — the engine's live entities + tags. No "Card" subclass —
  every entity is an `Entity`; "kind" is the `IsCard` tag.
- **Mirror State** — rebuilt purely from the wire stream, with the current
  cursor position. Independent of Primary.
- **Event Log** — every wire entry the engine has emitted, indented by block
  nesting. When you scrub back, blocks whose contents haven't been re-applied
  collapse out of view.

Above the panels:

- **Reset** — recreates the two entities (regenerates the entire event log).
- **Cast Fireball** — `engine.Effects.Run(new Fireball(caster, target))`.
  Opens a `BlockStart("Fireball")`, which contains a nested
  `BlockStart("DealDamage")`, which contains the Hp `TagChange`.
- **Cast Healing Potion** — `engine.Effects.Run(new HealingPotion(...))`.
- **Target HP = 25** — direct tag write. Emits a single `TagChange` event
  at the top of the log (no enclosing scope — bare mutations are fine).
- **Divine Shield on Target** — toggle. With it on, casting Fireball runs
  the Shield's Replacement-band hook, which fires a `ShieldBreak` effect
  (visible in the log as a nested block) and zeroes the damage.
- **Target Armor +/-** — tunable. Armor reduces incoming damage; runs after
  Shield's Replacement band.
- **Backlash on Caster** — toggle. After+Reaction hook on `DealDamage` runs
  the `Backlash` effect, which retaliates 1 damage. Visible in the log as
  a `Backlash` block nested inside the original `DealDamage` block.
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
