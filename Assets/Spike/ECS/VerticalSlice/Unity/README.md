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

`Assets/Spike/ECS/VerticalSlice/Unity/Sample.unity` ships with a `SliceBootstrap`
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

- **Reset** — rebuilds the whole world (regenerates the entire event log).
- **Jaina casts: Fireball → Garrosh** — Garrosh has 5 starting armor, so
  `ArmorRule` reduces 6 dmg to 1 net dmg. Demos the Armor hook.
- **Jaina casts: Fireball → Argent Squire** — Squire has intrinsic Divine
  Shield, so `DivineShieldRule` zeroes the damage and fires a `ShieldBreak`
  effect (nested block). Demos the DS hook.
- **Jaina casts: Healing Touch → Spite Imp** — heals 8, clamped at MaxHealth.
- **Jaina casts: Tactician → Yeti  (Spite Imp)** — Choose-One request. Spite
  Imp is the source; picking the damage option runs `DealDamage(SpiteImp,
  Yeti, 6)`, which triggers `BacklashRule` (Yeti deals 1 back to Spite Imp).
- **Garrosh casts: Fireball → Spite Imp** — straight 6 dmg, no rule fires
  (Garrosh is the source; he has no Backlash). Spite Imp goes to -3 HP.
- **Abomination → Graveyard** — sends Abomination to its owner's graveyard.
  The `DeathrattleBridge` fires `DamageAllEnemies` (2 dmg to enemy hero +
  every enemy minion on a battlefield zone).
- **Tomb Tender → Graveyard** — fires `HealAllAllies` (3 heal to every
  friendly minion on a battlefield zone, skipping the dying minion).
- **Scrub slider** — drag left to rewind, right to fast-forward. Primary is
  unaffected by scrubbing.

Each rule has a dedicated showcase minion / hero: Garrosh (Armor), Argent
Squire (Divine Shield), Spite Imp (Backlash), Abomination + Tomb Tender
(Deathrattle). Toggles for these properties were removed — the abilities
are intrinsic to specific entities now.

## Wire alphabet

Six concrete event types, no shared "Mutation" base class:

| Event | When | Apply | Unapply |
|---|---|---|---|
| `BlockStartEvent(id, parentId, kind, name, source, payload)` | An effect or command starts running | (no-op for state) | (no-op for state) |
| `BlockEndEvent(id, outcome)` | Same, ending | (no-op) | (no-op) |
| `EntityCreatedEvent(id)` | `world.Create()` | add to dict | remove from dict |
| `EntityDestroyedEvent(id)` | `world.Destroy(e)` | remove | add (empty) |
| `TagSetEvent(id, key, value)` | First write of a key on an entity | write key=value | **remove key** (this is the init-value rewind fix) |
| `TagChangeEvent(id, key, prev, next)` | Subsequent write | write key=next | write key=prev |

Block metadata:
- **`id`** — monotonic block id assigned at open time. `BlockEndEvent.Id`
  matches the `BlockStartEvent.Id` of the block being closed.
- **`parentId`** — id of the enclosing open block (null at top level).
  Gives an explicit causal chain without scanning positionally.
- **`kind`** — `Effect` (opened by `EffectService.Run`) or `Command`
  (opened by `CommandService.Execute`). Set by the dispatch site; not
  declared on the runnable type.
- **`name`** — `runnable.GetType().Name` (e.g. `"Fireball"`,
  `"DealDamage"`).
- **`source`** — convention-based extraction: if the runnable has a
  public `Source` property of `IEntity` type, it's captured here;
  otherwise null. Provenance for animation routing.
- **`outcome`** — `Completed` on normal exit, `Errored` on exception
  escape, `Cancelled` reserved for future request/timeout work.

## Authoring shape

```csharp
// Effects compose commands. Receive read-only IEffectScope.
public sealed class Fireball : Effect {
    public IEntity Source, Target;
    public override void Run(IEffectScope scope)
        => scope.Execute(new DealDamage(Source, Target, 6));
}

// Commands mutate state. Receive ICommandScope (resolves IEntity → mutable Entity).
public sealed class DealDamage : Command {
    public IEntity Source, Target; public int Amount;
    public override void Apply(ICommandScope scope) {
        var t = scope.Resolve(Target);
        t?.SetHealth(t.Health() - Amount);
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
