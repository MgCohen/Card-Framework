# Vertical Slice (deleted spike)

A tag-bag ECS game engine with commands, effects, hooks, a stack, SBAs, a block-structured event log and a rewindable mirror, proven with Hearthstone-style sample cards.

**Code:** deleted. Last version at commit `b13907e`, [`Assets/Spike/ECS/VerticalSlice/`](https://github.com/MgCohen/Card-Framework/tree/b13907e/Assets/Spike/ECS/VerticalSlice). Built May 2026.

## What it does
- Models every game thing as one `Entity` class: an `int Id` plus a sparse slot-keyed dictionary of tag values. "Kind" is a marker tag (`IsCard`, `IsZone`, `IsPlayer`).
- Runs player input through a pump: FIFO pipeline of commands, LIFO effect `Stack`, and a state-based-action fixpoint between stack pops.
- Lets rules attach to command types as Before/After hooks with priority bands (`Replacement`, `PreEffect`, `OnEffect`, `Reaction`).
- Writes every mutation into an append-only `EventLog`, inside nested `BlockStart`/`BlockEnd` pairs. A `MirrorWorld` rebuilds state from the log alone and `ScrubController` rewinds it.
- Supports player choices (`Request` with typed `OptionEvent`s), zones, owners and indexed compound queries.
- Keeps the engine asmdef Unity-free: 95 EditMode tests (77 slice, 18 `QueryBench`) run headless. `SliceBootstrap` and `SliceUI` show Primary, Mirror and log in an IMGUI scene.

## How it works
```text
Enqueue(cmd) -> pump: Commands.Execute(cmd) -> Evaluate -> next queued cmd
Evaluate:    loop { SBA passes until none emit; pop one Stack frame, or stop }
Execute:     block { Before hooks resolve inline; Apply; After hooks pushed with parentBlockId }
Stack pop -> EffectService.RunInline: Validate? -> block { Run(IEffectScope) }
Entity.Set -> TagSet / TagChange / TagRemoved appended inside the open block
PlayCard.Apply -> BuildDirect(OnCast) -> zone 0 -> Stack.Push; OnCastResolved -> graveyard
```
- Effects only `Execute` commands, `Run` sub-effects, `FireDirect` per-entity triggers or `OpenRequest`. Only commands get a mutable `Entity`, via `ICommandScope.Resolve`.

## Key types
| Type | Job |
|---|---|
| `Entity` / `IEntity` | Single entity class; `IEntity` is the read-only view effects and hooks see. |
| `TagKey<T>` / `TagRegistry` | Typed tag handle with an explicit slot and a wire name; static registry rejects slot collisions. Pattern B: `[TagSlot]` keyword classes via `Tag<T>.Key`. |
| `EntityStore` | Creates and destroys entities, logs their tag events, maintains `TagIndex`, serves `With` / `Where`. |
| `EntityQuery` | `readonly struct` query, up to 4 predicates, drives iteration from the smallest index set. |
| `GameEngine` | Composition root and pump; owns pipeline, `Stack`, SBAs and all services. |
| `CommandService` / `EffectService` | Run a command (with Before/After hooks) or an effect (after `Validate`) inside a wire block; build the scopes. |
| `EffectStore` | Hook subscriptions per (command type, phase) and per-entity direct-trigger factories. |
| `Stack` / `StackFrame` | Overridable LIFO of effects (`ShouldResolve`, `ResolveAsync`). |
| `ISBA` | Read-only check that yields commands to restore a legal state (`DeathSBA`). |
| `RequestsService` / `Request` | Opens player decisions as a Request block of option events; resolves picks. |
| `MirrorWorld` / `ScrubController` | Applies and unapplies wire events to a string-keyed tag dictionary. |

## Shapes worth keeping
Two-phase hook dispatch around `Apply`, with errors recorded on the block end.
```csharp
public void Execute(Command command, long? parentOverride = null)
{
    var (id, parentId) = _world.OpenBlock(parentOverride);
    _world.Log.Append(new BlockStartEvent(id, parentId, BlockKind.Command, command.GetType().Name,
        PayloadCapture.ExtractSource(command), PayloadCapture.Of(command)));
    var outcome = BlockOutcome.Completed;
    try
    {
        var cmdScope = new CommandScope(_entities!, _store!, _engine!.Stack);
        Dispatch(command, HookPhase.Before, drainEach: true);
        command.Apply(cmdScope);
        Dispatch(command, HookPhase.After, drainEach: false);
    }
    catch { outcome = BlockOutcome.Errored; throw; }
    finally { ... _world.Log.Append(new BlockEndEvent(closingId, outcome)); }
}
```
A rule is one small class: a typed trigger, a `Validate` gate that emits no block when false, and a self-registration.
```csharp
public sealed class DivineShieldRule : Effect<DealDamage>
{
    public DivineShieldRule(DealDamage cmd) : base(cmd) { }
    public override bool Validate(IEffectScope scope)
        => Trigger.Amount > 0 && Trigger.Target.DivineShield();
    public override void Run(IEffectScope scope)
    {
        Trigger.Amount = 0;
        scope.Run(new ShieldBreak(Trigger.Target));
    }
    public static void Register(EffectStore store)
        => store.Subscribe<DealDamage>(
            new HookConfig { Phase = HookPhase.Before, Priority = HookPriority.Replacement, Source = nameof(DivineShieldRule) },
            cmd => new DivineShieldRule(cmd));
}
```
Unapply of a first write (`TagSet`) removes the key, so rewind can tell "absent" from "default".
```csharp
// MirrorWorld.Unapply
case TagSetEvent ts:
    if (_entities.TryGetValue(ts.EntityId, out var setEnt))
        setEnt.Tags.Remove(ts.Key);
    break;
```

## Behaviour it proved
- **Armor** (`ArmorTests`): armor reduces damage, floors at 0 with no Health event, never depletes, and runs after Divine Shield.
- **Divine Shield** (`DivineShieldTests`): the shield absorbs the whole hit, breaks through `ShieldBreak` → `BreakDivineShield`, and the second hit lands.
- **Backlash** (`BacklashTests`): a source with Backlash takes 1 back when its damage lands; it skips untagged sources and zeroed damage, and the retaliation does not chain.
- **Deathrattle** (`DeathrattleTests`): moving a card to a graveyard fires that card's own attached effect; different cards carry different effects on the same trigger.
- **Death SBA** (`DeathSBATests`): a battlefield minion at Health ≤ 0 moves to its owner's graveyard after the effect finishes; cascades converge; the SBA `MoveCard` block opens after the effect block closes.
- **Tactician (Choose One)** (`TacticianTests`): casting opens a request with pick, pick, cancel options; each pick resolves its command and closes the request; cancel mutates nothing.
- **Zones** (`ZoneTests`): `MoveCard` writes the zone id, first placement is `TagSet`, later moves are `TagChange`, and moves rewind cleanly.
- **Mirror** (`MirrorTests`, `MirrorInitValueTests`): replay matches Primary; rewind restores prior values; rewinding past a first write removes the key.
- **Block metadata** (`BlockMetadataTests`): ids are monotonic, nested blocks point at their parent, kind is Effect or Command, `Source` comes from a `Source` property, ends are LIFO, a throw ends the block `Errored`.
- **Effects, hooks, Heal** (`EffectTests`, `HealTests`): an effect opens a block named after its type; a Replacement hook edits the command before `Apply`; heal clamps at max and ignores damage hooks.
- **Store, players, scopes** (`EngineTests`, `PlayerTests`, `ScopeTests`): no-op writes emit nothing; "not owned by" skips unowned cards; command scopes resolve a mutable `Entity`.
- **Query bench** (`QueryBench`): indexed queries agree with LINQ; the Hearthstone compound query must beat LINQ by 5x (Benchmark category).

## Differences from the Framework
- `Entity.Set` mutates state first, then raises an event that `EntityStore` appends to the log. The log is a side effect. The Framework emits an event from a service, and only apply handlers (`WireEventHandler<T>`) write stores.
- VS tags hold any type as a boxed `object`. Framework tags are `unmanaged` only.
- VS wire events carry tag names, live `IEntity` refs and reflection-captured payloads, so they are not serializable. Framework events are `[Packed]` ints.
- VS wires the engine by hand with `Bind*()` setters in the `GameEngine` constructor. The Framework uses VContainer installers split by server and client.
- VS has the game loop: pump, `Stack`, SBAs, hooks, effects, zones, queries and rewind via `Unapply`. The Framework does not have these yet.

## Limits
- Before-phase hooks drain with `ResolveNextAsync().GetAwaiter().GetResult()`. A truly async `Stack` subclass would block there.
- Every tag value is boxed in `Dictionary<int, object?>`, and value-index lookups box the key.
- `TagRegistry` is a process-global static with no reset, so slots leak across tests and domain reloads.
- Unapply of `EntityDestroyed` recreates an empty entity; destroyed tag values are lost.
