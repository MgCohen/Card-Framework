---
type: model
status: abandoned
archived_on: 2026-10-18
reason: "Written for the old state model (slices, Ref<T>, MatchStateSlice) and never built. Read it for the open questions, not as the contract; the replacement design comes from Product/spikes/framework-roadmap.md."
references: [features/entities/archive/01-definition.md, features/entities/archive/02-instance.md, features/entities/archive/03-variable.md, features/entities/archive/04-modifier.md, features/actions/archive/03-command-handler.md]
tags: [#entities, #attachment, #composition, #ongoing-effects, #model]
---

> **Archived 2026-10-18.** Written for the old state model (slices, Ref<T>, MatchStateSlice) and never built. Read it for the open questions, not as the contract; the replacement design comes from Product/spikes/framework-roadmap.md.

# Attachment — model

## What it is

The composition layer of the entity model. **Attachments and equipment are usually their own instances** — a sword on a villager is a `Ref<Sword>` related to a `Ref<Villager>`, not a hidden field on the villager handle. Tokens are full instances of token definitions. Transformation is explicit (catalog re-registration or modifier overlay). Ongoing effects (auras, status effects, prevention shields) are realized as attachment entities that subscribe to triggers, react over time, and dispatch `DestroyEntityAction` on themselves when done — uniform with equipment cleanup. The framework Cleanup hook unregisters every subscription and modifier owned by a destroyed attachment.

```text
Villager Instance
  definition: villager.base
  attached instances:
    - iron_sword instance     (contributes ModifierSource = sword.SelfRef)
    - leather_helmet instance (contributes ModifierSource = helmet.SelfRef)
    - heal_over_time instance (subscribes to TurnStartSignal; destroys self after N turns)
```

## Contracts

### Attachments and equipment

Attachments and equipment are usually their own instances. The host does not become a different opaque object when a sword is attached. A composition handler reads the relationships and calculates effective values such as attack, job, speed, available actions.

Benefits:

- Attachments have their own `Ref<T>` and lifecycle.
- They can move, break, transform, or be removed.
- They can have effects of their own.
- Debugging can trace exactly where a modifier came from (via [`04-modifier.md` §`ModifierSource`](features/entities/archive/04-modifier.md)).

### Tokens and generated entities

Tokens have definitions — authored, generated, preset-provided, or temporary game-setup definitions. Avoid definitionless runtime entities; even a tiny system definition is useful for debugging and replay (see [`01-definition.md`](features/entities/archive/01-definition.md) Q5).

### Transformation and replacement

Transformation needs explicit game-specific semantics. Possible models:

- **Same instance, new definition reference.** Replace the handle in the catalog (`store.Catalog.RegisterAt(selfRef, newHandle)`) for the same `Ref<T>`. There is no state-level `SetDefinitionPayload`.
- **Same instance, transformation modifier layered over base definition.** Use a modifier with `ModifierSource = transformationEntity.SelfRef`; cleanup via `RemoveModifiersFromSource` when transformation ends.
- **New instance that replaces the old instance.** Spawn a new entity, transfer relevant state, destroy the original.
- **Attached overlay instance that changes effective values.** A transformation entity contributes modifiers to the host; cleanup via `RemoveModifiersFromSource`.

No single choice is built into the framework. The framework exposes the primitives needed for presets/games to define their own transformation semantics.

### Attachment-entity usage (ongoing effects)

Ongoing effects (auras, stateful prevention, status effects, equipment buffs) are realized as **attachment entities**: an entity instance that is created, associated with a host, subscribes to triggers, ticks or reacts over time, and dispatches `DestroyEntityAction` on itself when done. No separate ongoing-effect machinery — the attachment uses `EntityDefinition` + `Ref<T>` + variables + subscriptions like any other entity.

```
Create attachment entity (CreateAndAttachAction)
  → Spawn via store.Spawn<TDef>; register handle in catalog
  → Associate with host (relationship slice or attachment registry)
  → definition.Register(triggers, self, ctx) — subscribe to relevant signals
  → React each time trigger fires (apply effect, update internal state)
  → When done condition met: scope.Dispatch(DestroyEntityAction { Target = self })
  → Cleanup hook unregisters every subscription owned by self
```

```csharp
public sealed class HealOverTimeDefinition : EntityDefinition
{
    public int HealAmount { get; init; }
    public int TurnCount  { get; init; }

    // ADR-0005: `self` is a stable subscription key — entities use their typed Ref<T>
    // as the trigger owner so subscriptions survive CardRef rotations.
    public override void Register(ITriggerSystem triggers, Ref<HealOverTime> self, Context ctx)
    {
        triggers.OnAfter<TurnStartSignal>(
            condition: (_, _) => true,
            react: (_, scope) =>
            {
                var attachment = scope.Store.Catalog.Resolve(self);
                var remaining  = attachment.Storage.GetVariable<int>(Vars.TurnsRemaining);

                scope.Dispatch(new HealActorAction { Target = scope.GetHost(self), Amount = HealAmount });
                scope.Dispatch(new DecrementTurnsRemainingAction { Target = self });
                if (remaining - 1 <= 0)
                    scope.Dispatch(new DestroyEntityAction { Target = self });
            },
            owner: self);
    }
}
```

#### Common patterns

- **Duration-based (N turns).** Subscribe to `TurnStartSignal`. Decrement a counter variable each tick. Dispatch `DestroyEntityAction` when counter reaches zero.
- **Condition-based (until end of turn).** Subscribe to `TurnEndSignal`. Dispatch `DestroyEntityAction` on self unconditionally when the signal fires.
- **Source-linked / aura (while source is in play).** The source entity creates the attachment on the target. When the source is destroyed or leaves the relevant zone, it dispatches `DestroyEntityAction` on the attachment. The attachment does not poll — it is driven by the source's own lifecycle.
- **Global effect (affects all entities this turn).** The attachment is created and associated with a global game entity (match state or active player) rather than a specific card.
- **Stateful prevention ("prevent the next N damage").** Attachment with `OnBefore<DealDamageAction>` subscription that reads a `Remaining` variable, calls `scope.Replace` with a reduced-amount copy, decrements the counter, and dispatches `DestroyEntityAction` when exhausted.

### Creating and attaching

`CreateAndAttachAction` is a normal `GameAction`:

```csharp
new CreateAndAttachAction
{
    Definition = new HealOverTimeDefinition { HealAmount = 1, TurnCount = 3 },
    Host       = targetCreature
}
```

Its handler:

1. Spawns the entity via `ctx.Store.Spawn<TDef>` (allocates `Ref<T>`, registers `EntityState`, registers handle in catalog — see [`02-instance.md` §Lifecycle](features/entities/archive/02-instance.md)).
2. Associates it with the host (relationship slice on the host, or a dedicated attachment registry).
3. Calls `definition.Register(triggers, newInstance, ctx)` to activate subscriptions.

Attachment storage shape (sub-zone on host, global registry, or dedicated zone) is tied to the zone model and not locked here — tracked as `oq-attachment-storage`.

### Cleanup is automatic

The framework Cleanup hook (per [`02-instance.md` §Destroy](features/entities/archive/02-instance.md)) unregisters every subscription owned by the destroyed attachment via `RemoveAll(self)`. Modifiers contributed by the attachment use `ModifierSource = attachment.SelfRef`, so cleanup is uniform via `RemoveModifiersBySourcePayload` (per-target) or `store.RemoveModifiersFromSource(source)` (bulk) — same machinery used by equipment, auras, and any other entity that contributes modifiers (see [`04-modifier.md`](features/entities/archive/04-modifier.md)).

The exact lifecycle (what "destroyed" means, when Cleanup fires relative to the destruction envelope, what happens to in-flight dispatches that reference the destroyed entity) is a deferred topic (`oq-entity-destruction`).

## Invariants

1. **Attachments and equipment are usually separate instances.** Composition handlers calculate effective behavior from related instances + modifiers.
2. **Tokens have definitions.** No definitionless runtime entities (per [`01-definition.md`](features/entities/archive/01-definition.md) Q5).
3. **Transformation is explicit.** Choose between catalog re-registration, modifier overlay, new-instance replacement, or attached overlay — per game.
4. **Ongoing effects are attachment entities.** No separate ongoing-effect machinery; uses `EntityDefinition` + `Ref<T>` + subscriptions like any other entity.
5. **Attachment cleanup is uniform.** `RemoveAll(self)` for subscriptions; `RemoveModifiersFromSource` for contributed modifiers — same machinery as equipment/auras.

## Decisions

*n/a — no elevated ADR.* (Provenance Q6 below.)

## Examples

### Equip / unequip — single-handler mutation

A villager with `Attack = 1` (definition default) equips a sword granting `+3 attack`; effective attack reads as `4`. On unequip, the sword's modifier is swept and effective attack reverts to `1`.

```csharp
public static class Vars
{
    public static readonly Variable Attack = new("attack", "int");
}

public sealed class VillagerDefinition : EntityDefinition
{
    // Authored: defines Vars.Attack with default value 1
}

public sealed class SwordDefinition : EntityDefinition { /* ... */ }

public sealed record EquipSwordCommand(Ref<Villager> Wearer, Ref<Sword> Sword) : GameAction;
public sealed record EquipResult(int AttackBefore, int AttackAfter) : CommandResult;

public sealed class EquipSwordHandler
    : GameCommandHandler<EquipSwordCommand, EquipResult>
{
    private readonly IModifierFactory modifiers;
    public EquipSwordHandler(IModifierFactory modifiers) { this.modifiers = modifiers; }

    // Store comes from the GameCommandHandler<,> base via [Inject] (per ADR-0010).
    public override Task<EquipResult> Handle(EquipSwordCommand cmd)
    {
        var villager = Store.Catalog.Resolve(cmd.Wearer);
        var sword    = Store.Catalog.Resolve(cmd.Sword);

        var before = villager.Storage.GetVariable<int>(Vars.Attack);   // 1 (definition default)

        // Build the +3 attack buff, sourced to the sword instance
        var attackBuff = modifiers.Create(
            Vars.Attack,
            new IntAddModifier { Amount = 3, Order = 100 },
            new ModifierSource(sword.SelfRef, 0));

        // Install it on the villager via the storage adapter
        villager.Storage.AddModifier(attackBuff);

        var after = villager.Storage.GetVariable<int>(Vars.Attack);    // 4

        return Task.FromResult(new EquipResult(before, after));
    }
}

public sealed record UnequipSwordCommand(Ref<Villager> Wearer, Ref<Sword> Sword) : GameAction;
public sealed record UnequipResult(int AttackAfter) : CommandResult;

public sealed class UnequipSwordHandler
    : GameCommandHandler<UnequipSwordCommand, UnequipResult>
{
    public override Task<UnequipResult> Handle(UnequipSwordCommand cmd)
    {
        var villager = Store.Catalog.Resolve(cmd.Wearer);

        // Sweep every modifier on the wearer that came from the sword
        villager.Storage.RemoveModifiersFromSource(new ModifierSource(cmd.Sword, 0));

        // Destroy the sword instance — slice unregistered, catalog entry cleared
        Store.Destroy(cmd.Sword);

        var after = villager.Storage.GetVariable<int>(Vars.Attack);    // 1 (back to definition default)

        return Task.FromResult(new UnequipResult(after));
    }
}
```

For attachment models where the sword *itself* is the attached entity, the framework Cleanup hook performs the source-sweep automatically on destroy — the explicit `RemoveModifiersFromSource` shown here is the long form, useful when the unequip semantics differ (e.g. sword survives but stops contributing).

### Apply-aura — modifier built in effect, installed in handler

When a modifier comes from an effect orchestrator (target selection, pre-conditions, choosing parameters) but the install lands in a separate action's handler, the modifier entry travels through the action payload as a value. The factory call sits in the effect (so the deterministic `ModifierId` is allocated at the right point in the command stream); the handler just installs the pre-built entry.

```csharp
public sealed record ApplyDamageAmpAction(
    Ref<Enemy>      Target,
    ActiveModifier  Modifier) : GameAction;

public sealed record ApplyResult(Ref<Enemy> Target) : CommandResult;

public sealed class ApplyDamageAmpEffect : Effect
{
    public override async Task Execute(IEffectScope scope)
    {
        var pick = await scope.Dispatch<RequestTargetResult>(new RequestTargetAction
        {
            Filter = EnemyFilter.InRange(scope.Source, range: 3),
        });
        if (pick.Target is null) return;

        // Built in the effect — id is deterministic, source is this effect's host.
        var ampBuff = scope.Modifiers.Create(
            Vars.DamageDealt,
            new IntAddModifier { Amount = 2, Order = 200 },
            new ModifierSource(scope.Source.SelfRef, 0));

        await scope.Dispatch<ApplyResult>(new ApplyDamageAmpAction
        {
            Target   = pick.Target.Value,
            Modifier = ampBuff,
        });
    }
}

public sealed class ApplyDamageAmpHandler
    : GameCommandHandler<ApplyDamageAmpAction, ApplyResult>
{
    public override Task<ApplyResult> Handle(ApplyDamageAmpAction cmd)
    {
        var enemy = Store.Catalog.Resolve(cmd.Target);
        enemy.Storage.AddModifier(cmd.Modifier);
        return Task.FromResult(new ApplyResult(cmd.Target));
    }
}
```

Why the factory call belongs in the effect, not the handler:

- **Determinism is per-decision-point, not per-handler-call.** The id is allocated at the moment the effect *decides* to install the buff. Re-running the same effect against the same state must produce the same id.
- **The modifier is part of the action's identity.** It's serialized into the history envelope; debugger / replay tooling can render "ApplyDamageAmp targeted goblin#42 with buff M-7" as a complete record.
- **Handlers stay narrow.** They install what they're told, validate the target, and return. They don't make modifier-shape decisions.

Use this pattern whenever the buff parameters depend on effect-side state (target choice, source attribution, computed magnitude) but the install must be a discrete observable command (replay, network sync, history clarity). For self-contained "buff this entity right now" cases where there's no target picking or pre-computation, the equip/unequip pattern — single handler doing build + install — is simpler.

## Open follow-ups

- **`oq-attachment-storage`** — where attachments (equipment, auras, buffs) live: sub-zone on host, global registry, or dedicated zone. Load-bearing for the ongoing-effects attachment model.
- **`oq-entity-destruction`** — what "destroyed" means for the Store; when the framework Cleanup hook fires; what happens to in-flight dispatches that reference the destroyed entity.

---

*Below: provenance — the composition decision (Q6, formerly "doc 8") that shapes this Model. Single-feature scope; folded per §4.5 on 2026-05-08. Original landing 2026-04-29.*

## Options considered

### Q6 — Composition (modifiers, attachments, transformation)

How do attachments, equipment, stacked cards, tokens, and transformed cards relate to a base card instance?

- **A — Absorb everything into the base card object.** Rejected — explodes object size; loses provenance.
- **B — Instance is base definition + modifiers; attachments and equipment are usually separate instances.** **Chosen** — sword attached to villager is an equipment instance attached to a villager instance; aura modifier may be an effect or relationship from another instance; token is an instance from a token definition; transformed card may be the same instance with a transformation modifier or a game-specific replacement; stack may be a relationship between instances.

Composition keeps instances explicit and supports Stacklands-like physical composition, RPG equipment, TCG auras, attached cards, and relic/passive systems without making the base card object enormous.

## Rationale

Q6's choice cascades through the framework:

- **Attachments as separate instances** gives clean cleanup (`ModifierSource` attribution + `RemoveModifiersFromSource`), inspectable composition (debugger names every contributor), and uniform machinery between equipment, auras, and ongoing-effect attachments.
- **Tokens as full instances with definitions** keeps save/load and replay tractable.
- **Transformation as explicit per-game choice** avoids baking MTG-style replacement semantics into all games.

The ongoing-effects-as-attachment-entities pattern reuses the same machinery rather than introducing a separate "buff system" — same `Ref<T>`, same `EntityState`, same trigger subscriptions, same Cleanup hook.

## Consequences

- **Enables** clean composition (sword attached to villager is an explicit relationship, not a hidden field); uniform cleanup-on-source-death (`RemoveModifiersFromSource` works the same for equipment, auras, and ongoing-effect attachments); ongoing-effects without a separate buff system; per-game transformation semantics.
- **Constrains** games that need composition to model attachments as separate instances (cannot hide them inside the host); attachment storage shape (sub-zone vs registry vs dedicated zone) is per-game (tracked as `oq-attachment-storage`); transformation requires game-side choice between catalog re-registration and modifier overlay.
