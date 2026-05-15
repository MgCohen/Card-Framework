---
type: spike
status: deferred
tags: [#spike, #effects, #hooks, #registration, #deferred]
---

# Hook Config — Effect Registration Redesign

> **Status: deferred (2026-05-14).** Design captured here for implementation after current vertical-slice work settles. Not yet in code. Companion to `vertical-slice-plan.md` (concluded). Lays out the shape we'd port to once the in-flight changes around effects, requests, and bridges land.

A redesign of how hook subscriptions and per-card triggers are registered in the engine. Collapses the current two-store split (`EffectService.Register` + `CardEffectStore`) into a single `EffectStore`, and introduces an extensible `HookConfig` subclass pattern so games can express "when is this hook active right now" as data the framework can read, not a closure it can't.

## Why this exists

The slice landed three distinct paths into "an effect runs":

1. **Global hook rules** — `Effect<TCommand>` rules subscribed via `EffectService.Register<TCmd>(phase, prio, factory)`. Each rule (DivineShield, Armor, Backlash) carries a `static Register(EffectService)` that hardcodes phase, priority, and the `cmd => new XRule(cmd)` factory. Bootstrap calls them in a list.
2. **Per-card direct triggers** — `Effect<TTrigger>` factories pinned to specific entities via `CardEffectStore.Set<TTrigger>(card, factory)`. Keyed by `(entityId, triggerType)`. Engine bridges fire them via `scope.FireDirect<TTrigger>(card, payload)`.
3. **Bridges** — plain `CommandService.Subscribe` handlers that translate raw commands into typed direct-trigger payloads (`DeathrattleBridge` is the example).

This works for the slice's small card pool. It surfaces tension as soon as we look at a Hearthstone-shaped game with 60+ cards, most of which carry triggers and most of which spend most of their lives in the deck or graveyard (i.e. inactive).

The two pressure points:

- **Two parallel "register an effect" APIs** for what are essentially the same thing — both produce `Effect`s, both go through the same `EffectService.Run` dispatch. The split is a call-site distinction (game-rule vs card-ability), not a storage one.
- **Activation gating.** A card in your deck shouldn't fire its on-play battlefield trigger. With the current shape, every per-card hook would either (a) be subscribed unconditionally and gate inside `Validate`, or (b) need register/unregister machinery as the card moves zones. Neither is satisfying as written.

## Investigations that led here

### 1. Validate-only gating vs register/unregister

First instinct was register/unregister: subscribe when the card enters the battlefield, unsubscribe when it leaves, so the active subscription set always reflects what's live. We rejected this.

| | Validate-gate | Register / unregister |
|---|---|---|
| Author cost | 1 line in `Validate` | rule needs lifecycle wiring + handle |
| Framework cost | zero (already built) | new concept: handles, zone-change observers, ordering rules |
| Hot path | factory allocates + Validate runs, aborts cleanly | only live rules visible |
| Atomicity | predicate evaluated at fire time → always current | window between zone-change and unsubscribe; subscription set mutates during in-flight blocks — known card-engine footgun |
| Diagnostics | "why didn't X fire?" → re-run Validate manually | "X isn't subscribed right now" — clearer |
| Wire | nothing emitted (Validate fails pre-block) | nothing emitted |

Register/unregister introduces a meta-bus (you need to watch zone-changes globally to know when to flip subscriptions) and a hard atomicity question (what happens when a card enters Zone X mid-block — does its hook fire for the in-flight command?). Validate-as-gate sidesteps both.

### 2. The perf math

Worst-case sketch for a Hearthstone-shaped game:

- ~200 total subscribers across all cards in both decks.
- Subscribers are keyed by command type — a `DealDamage` fire only walks `DealDamage` subs, not all 200.
- A turn fires maybe 6 distinct command types, ~10 subs each → ~50 Validate calls per turn.
- ~50 small Effect allocations per turn (~32 bytes each) ≈ 1.6 KB/turn.

That's noise. Unity gen-0 GC eats it without a hiccup; a turn takes seconds (player thinking time). The perf justification for inventing register/unregister evaporates. **What's left is purely ergonomics:** 30 cards each writing `if (!self.InZone(battlefield)) return false;` at the top of `Validate` is duplicate code, and the footgun is forgetting it on card #31 and getting a ghost trigger from the graveyard.

### 3. Where the gate lives

If we want to eliminate the duplication without inventing lifecycle, the gate has to be a first-class part of the registration. Options considered:

- **Optional `gate:` Func parameter** on `Subscribe`. Works, but: closures aren't introspectable (diagnostics can't read "this sub is gated by zone=Battlefield"), and every call site re-writes the lambda. The reuse story is weak.
- **HookConfig data class with a virtual `IsActive` method** (chosen). Phase, priority, and activation bundle into one descriptor. Game subclasses once per gating pattern (`MinionHookConfig`, `OwnerTurnHookConfig`, etc.). Framework passes context, asks "are you live?", trusts the answer. Configs are inspectable; reuse is real; framework knows nothing game-specific.

## The design

### Framework

```csharp
// Bundle of what IsActive can read. Tiny; growable later without breaking signatures.
public readonly struct HookContext
{
    public Command       Command { get; init; }   // the in-flight trigger, untyped
    public IEffectScope  Scope   { get; init; }   // world reads, turn state, queries
}

public abstract class HookConfig
{
    public HookPhase    Phase    { get; init; }
    public HookPriority Priority { get; init; }

    // Default: always on. Base config works for game-wide rules.
    public virtual bool IsActive(in HookContext ctx) => true;
}

public sealed class EffectStore
{
    // Hook subscription. Game-wide if config is base HookConfig; gated if subclassed.
    public Subscription Subscribe<TCommand>(
        HookConfig config,
        Func<TCommand, Effect> factory
    ) where TCommand : Command;

    // Direct trigger — entity-keyed by definition (FireDirect targets an entity).
    public void SetDirect<TTrigger>(IEntity owner, Func<TTrigger, Effect> factory)
        where TTrigger : class;

    public void Unsubscribe(Subscription handle);
    public void ClearDirect<TTrigger>(IEntity owner) where TTrigger : class;
}
```

Framework knows nothing about zones, cards, owners, turn state, or what "active" means. It hands every hook a `HookContext` (payload + scope) and asks the config.

### Game side — caster lives on the subclass

Self / caster / owner is captured on the game-defined subclass at construction time. The framework doesn't need a generic `Owner` slot on the base — different games carry different references.

```csharp
// "Active while this minion is on the battlefield."
public sealed class MinionHookConfig : HookConfig
{
    public IEntity Self { get; }
    public IEntity Zone { get; }
    public MinionHookConfig(IEntity self, IEntity zone, HookPhase p, HookPriority pr)
    {
        Self = self; Zone = zone; Phase = p; Priority = pr;
    }
    public override bool IsActive(in HookContext ctx) => Self.InZone(Zone);
}

// "Active only on Self's owner's turn."
public sealed class OwnerTurnHookConfig : HookConfig
{
    public IEntity Self { get; }
    public IEntity Battlefield { get; }
    public OwnerTurnHookConfig(IEntity self, IEntity bf, HookPhase p, HookPriority pr)
    {
        Self = self; Battlefield = bf; Phase = p; Priority = pr;
    }
    public override bool IsActive(in HookContext ctx)
        => Self.InZone(Battlefield)
        && ctx.Scope.TurnState().ActivePlayer() == Self.OwnerId();
}

// "Active only when MY OWNER takes damage." — reads Self + the in-flight command.
public sealed class OnOwnerDamagedConfig : HookConfig
{
    public IEntity Self { get; }
    public IEntity Battlefield { get; }
    public OnOwnerDamagedConfig(IEntity self, IEntity bf, HookPhase p, HookPriority pr)
    {
        Self = self; Battlefield = bf; Phase = p; Priority = pr;
    }
    public override bool IsActive(in HookContext ctx)
    {
        if (!Self.InZone(Battlefield)) return false;
        return ctx.Command is DealDamage dd
            && dd.Target.OwnerId() == Self.OwnerId();
    }
}
```

Three configs, three different "what does active mean" answers — none of which the framework knows about. Every game writes its own subclasses for the gating patterns it cares about; games that don't gate on zone simply don't write a zone config.

### Dispatch loop

```
on TCommand fires at phase P, with scope S:
  ctx = new HookContext { Command = cmd, Scope = S }
  for each sub where config.Phase == P, ordered by config.Priority:
    if !sub.config.IsActive(in ctx): continue       // no factory call, no alloc
    effect = sub.factory(cmd)
    Effects.Run(effect)                              // Validate + block + Run
```

`IsActive` is the cheap pre-filter. `Validate` (on the `Effect` itself) keeps its job — per-fire correctness checks (`Amount > 0`, `target has shield`, etc.). Distinct concerns: `IsActive` is "should this hook be considered at all," `Validate` is "given that we're firing, do the per-call conditions hold?"

### Call sites

```csharp
// Game-wide rule — base HookConfig, always active
store.Subscribe<DealDamage>(
    new HookConfig { Phase = Before, Priority = Replacement },
    cmd => new DivineShieldRule(cmd));

// Card ability — minion-in-zone
store.Subscribe<SummonMinion>(
    new MinionHookConfig(knifeJuggler, battlefield, After, Reaction),
    cmd => new KnifeJugglerPing(knifeJuggler, cmd));

// Card ability — owner's turn only
store.Subscribe<DrawCard>(
    new OwnerTurnHookConfig(loreCho, battlefield, After, Reaction),
    cmd => new LoreChoMimic(loreCho, cmd));

// Card ability — gated by command target
store.Subscribe<DealDamage>(
    new OnOwnerDamagedConfig(acolyte, battlefield, After, Reaction),
    cmd => new AcolyteDrawCard(acolyte, cmd));
```

## What this collapses

- `CardEffectStore` → folded into `EffectStore` (as `SetDirect`). One place for all effect bindings.
- `EffectService.Register<T>` → folded into `EffectStore.Subscribe`. Game-wide rules and card-owned hooks use the same surface; the difference is which `HookConfig` subclass they pass.
- `EffectService` keeps just `Run` + dispatch, reading from `EffectStore`. Subscription state stops being split across two services.
- The "two registration APIs" question dissolves entirely. Everything is `Subscribe(HookConfig, factory)` or `SetDirect(entity, factory)`.

## What stays separate

- **Direct triggers stay un-configged.** `SetDirect(entity, factory)` is fine as is. Direct triggers are entity-bound by definition (the bridge picks which entity to fire on), no phase/priority, and the bridge already gates on which entity reaches `FireDirect`. Adding a config there would be ceremony for no gain.
- **Bridges stay as plain `CommandService.Subscribe` handlers.** They translate raw commands to `FireDirect` calls; they're not hooks themselves. No HookConfig involved.
- **Game-side `CardEffects` helper still exists** — but shrinks. It wraps `EffectStore` for per-card lifecycle bookkeeping (auto-cleanup of subs when an entity is destroyed). The gate ergonomics move out of the helper into the HookConfig subclasses, where they belong.

## Open questions to resolve at implementation time

1. **Typed vs untyped `IsActive`.** Sketched `IsActive(in HookContext)` with `Command` (untyped base) inside; game pattern-matches if it needs command fields. Alternative: `HookConfig<TCommand>` generic for type safety. Untyped wins on storage simplicity (one bucket per phase, not per phase × command-type); typed wins on call-site clarity. Lean: untyped.
2. **`in HookContext` (struct, no copy) vs `HookContext` (class, GC'd).** Sketched as struct + `in`. Zero alloc per dispatch, matters because dispatch is hot-path-adjacent. No reason to box it.
3. **Does `HookConfig` carry a name for diagnostics?** A `string Source { get; init; }` would let block-start logs say *which* config gated *which* fire. Cheap optional field. Lean: yes, add from day one.
4. **Mutability.** Sketched `init`-only properties. If a game wants "temporarily disable this sub," they add a mutable `Enabled` flag in their subclass and check it in `IsActive`. Framework stays out of mutable config state.
5. **Subscription handles for game-wide rules too.** If `Subscribe` always returns a handle, even rules can be removed (useful for "this card disables Backlash for the rest of the turn" effects). Lean: yes, uniform.
6. **Whether `OnEntityDestroyed` cleanup is framework-driven or game-driven.** Lean: game-driven. The game-side `CardEffects` helper subscribes to entity-destroyed events and cleans up its tracked subs. Framework doesn't need to know the helper exists.

## Why defer

Three reasons:

1. **The current vertical slice still has in-flight work touching effects, requests, and bridges.** Restructuring `EffectService` + `CardEffectStore` mid-flight would conflict with that work and make either change harder to review.
2. **No card in the slice today triggers the boilerplate problem.** The slice has 6 entities and a handful of rules. Validate-only is fine at this scale; the redesign earns its keep when the card pool grows.
3. **The shape locked above is ready to drop in.** Once the in-flight work settles, this is a contained migration: collapse two stores into one, introduce `HookConfig` + `HookContext`, port the three existing rules (DivineShield, Armor, Backlash) to base `HookConfig` (no gate), keep direct triggers and bridges unchanged. No new game behavior — just structural cleanup with an extension point added.

## Migration order, when implementing

1. Introduce `HookContext`, `HookConfig`, and `EffectStore` (new file). Don't wire it yet.
2. Move `CardEffectStore.Set/Clear/Has/Build` into `EffectStore.SetDirect/ClearDirect/...`. Update `EffectService.RunResolution` and `EffectScope.FireDirect` to read from the new store. Delete `CardEffectStore`.
3. Move `EffectService.Register<T>` into `EffectStore.Subscribe`. `EffectService.Run` keeps its dispatch role but reads subscription state from the store. The `Register<T>` helper on `EffectService` can stick around as a thin shim if useful, or be removed and call sites updated.
4. Update dispatch in `CommandService` (or wherever hooks fire) to construct a `HookContext` and call `config.IsActive(in ctx)` before invoking the factory.
5. Port the three sample rules to use base `HookConfig` directly (their `static Register` methods become one-liners that pass a base config).
6. Add a sample game-side `MinionHookConfig` to demonstrate subclassing — even if no slice card uses it yet, having one in the sample keeps the pattern visible.

No data migration, no wire format changes, no behavior changes for existing cards.
