---
type: model
status: settled
references: [features/effects/models/01-async-dispatch.md]
tags: [#effects, #continuous, #ongoing]
---

# Continuous And Ongoing Effects

This document defines how ongoing effects are modelled in the Card Framework — effects that persist across multiple actions or turns, managing their own state and lifetime.

Builds on:

- [features/entities/models/01-definition-instance-model.md](features/entities/models/01-definition-instance-model.md) — entity, definition, instance identity
- [features/triggers/models/01-trigger-fold.md](features/triggers/models/01-trigger-fold.md) — trigger subscription, AbilityDefinition
- [features/effects/models/02-replacement-prevention.md](features/effects/models/02-replacement-prevention.md) — ITriggerPipeline, fold mechanics

---

## 1. Core Model

An ongoing effect is an **attachment entity** — an entity that is created, associated with a host, subscribes to triggers, ticks or reacts over time, and eventually destroys itself.

No new pipeline concept is needed. The attachment uses the same entity, trigger, and action machinery as everything else.

```
Create attachment entity
  → Attach to host
  → Subscribe to relevant triggers
  → React each time trigger fires (apply effect, update internal state)
  → When done condition met: dispatch DestroySelf
  → On destruction: framework calls Cleanup → Unregister all subscriptions
```

---

## 2. Attachments Are Entities

Attachments follow the same entity model as cards, tokens, and equipment (doc 10):

- They have a `Definition` — the blueprint describing what they do.
- They have a typed `Ref<TAttachment>` — a stable runtime identity.
- They carry variables — used for counters, remaining charges, elapsed turns, etc.
- They are **findable and referenceable** by other game systems and effects.
- Targetability (can other effects select them as targets?) is game-specific. The framework does not enforce it either way.

```csharp
// Example definition
public sealed class HealOverTimeDefinition : EntityDefinition
{
    public int HealAmount  { get; init; }
    public int TurnCount   { get; init; }

    // ADR-0005: `self` is a stable subscription key — entities use their typed Ref<T> as the trigger owner
    // so subscriptions survive CardRef rotations. Boundary-correct per ADR-0005.
    public override void Register(ITriggerSystem triggers, Ref<HealOverTime> self, Context ctx)
    {
        triggers.OnAfter<TurnStartSignal>(
            condition: (_, _) => true,
            react: (_, scope) =>
            {
                // Read via the storage adapter
                var attachment = scope.Store.Catalog.Resolve(self);
                var remaining = attachment.Storage.GetVariable<int>(Vars.TurnsRemaining);

                scope.Dispatch(new HealActorAction { Target = scope.GetHost(self), Amount = HealAmount });
                scope.Dispatch(new DecrementTurnsRemainingAction { Target = self });   // handler does the SetBaseValuePayload write
                if (remaining - 1 <= 0)
                    scope.Dispatch(new DestroyEntityAction { Target = self });
            },
            owner: self);
    }
}
```

---

## 3. Common Patterns

### Duration-based (N turns)

Subscribe to `TurnStartSignal`. Decrement a counter variable each tick. Dispatch `DestroyEntityAction` on the attachment when counter reaches zero.

### Condition-based (until end of turn)

Subscribe to `TurnEndSignal`. Dispatch `DestroyEntityAction` on self unconditionally when the signal fires.

### Source-linked / aura (while source is in play)

The source entity creates the attachment on the target when the aura comes into effect. When the source is destroyed or leaves the relevant zone, it dispatches `DestroyEntityAction` on the attachment. The attachment does not need to poll — it is driven by the source's own lifecycle.

### Global effect (affects all entities this turn)

Same pattern — the attachment is created and associated with a global game entity (e.g., the match state or the active player entity) rather than a specific card.

---

## 4. Turn Hooks

Turn lifecycle events (`TurnStartSignal`, `TurnEndSignal`, `PhaseChangeSignal`, etc.) are `GameSignal`s emitted by the turn management system — typically dispatched from inside a turn-advancing `GameAction`'s handler or from `GameRules.OnQueueDrained`.

Attachments subscribe to these signals via `OnAfter<TSignal>` exactly like any other trigger. No special attachment API is needed.

```csharp
// Turn manager emits the signal — attachment subscribes like anything else
scope.Emit(new TurnStartSignal { ActiveActor = currentActor });
```

The full turn lifecycle design is a separate topic (task 12 — game lifecycle). For ongoing effects, the only requirement is that turn signals exist as `GameSignal`s and flow through the pipeline.

---

## 5. Cleanup On Destruction

All entities have a **framework-level Cleanup hook** that is called automatically when the entity is destroyed. Cleanup calls `Unregister` on all subscriptions owned by that entity.

This is a lifecycle guarantee, not a trigger reaction. An attachment does not need an `OnAfter<DestroyEntityAction>` subscription to clean up its triggers — destruction handles it.

The canonical cleanup verb for modifiers contributed by an attachment entity is `RemoveModifiersBySourcePayload(target.SelfRef, new ModifierSource(attachment.SelfRef, 0))`, dispatched per affected target by the attachment's destruction handler (or by the framework Cleanup hook). The bulk variant `store.RemoveModifiersFromSource(source)` sweeps every entity in one call. Per [`entities/01` §"Mutation API conventions"](features/entities/models/01-definition-instance-model.md), `ModifierSource.Source` is the attachment's `Reference`, so this sweep is uniform across equipment, auras, status effects, and other ongoing-effect attachments.

The exact lifecycle design (what "destroyed" means, when Cleanup fires relative to the destruction envelope, what happens to in-flight dispatches that reference the destroyed entity) is a **deferred topic** — entity lifecycle needs a dedicated design pass. The pattern here relies on Cleanup existing; the mechanism is not fully specified yet.

---

## 6. Creating And Attaching

Creating an attachment is a normal `GameAction`:

```csharp
new CreateAndAttachAction
{
    Definition  = new HealOverTimeDefinition { HealAmount = 1, TurnCount = 3 },
    Host        = targetCreature
}
```

The handler:
1. Instantiates the entity via `StoreEntities.Spawn<TDef>` (allocates `Ref<T>`, registers the empty `EntityState` slice, registers the handle in the catalog).
2. Associates it with the host (stored as a relationship on the host entity or in a dedicated attachment registry).
3. Calls `definition.Register(triggers, newInstance, ctx)` to activate subscriptions.

Attachment storage — whether attachments live in a sub-zone on the host, a global attachment registry, or a dedicated zone — is a detail tied to the zone model and is not locked here.

---

## 7. Future: Attachments As Modifier Carriers

In the future, once the stats/variables system is redesigned, attachments may serve as modifier carriers: an attachment holds a stat delta, and the host entity reads its effective stat by aggregating its own base value plus the deltas from all attached modifiers. This would make ongoing stat modifications ("+2 attack while enchanted") fit the same attachment model without a separate modifier system.

This is out of scope for the current session and depends on the stats redesign work.

---

## 8. Open Questions

- **Entity lifecycle / destruction** — what "destroyed" means precisely, when Cleanup fires relative to the destruction envelope, and what happens to in-flight dispatches referencing a destroyed entity. Prerequisite for fully specifying attachment cleanup.
- **Attachment storage** — sub-zone on host, global registry, or dedicated zone. Tied to zone model.
- **Attachment visibility** — are attachments visible to players? Are they shown in UI? Game-specific, but the framework needs to not accidentally hide them.

---

## 9. Related

- [features/entities/models/01-definition-instance-model.md](features/entities/models/01-definition-instance-model.md) — entity/definition/instance model
- [features/zones/models/01-zone-model.md](features/zones/models/01-zone-model.md) — zone model; attachment storage may extend this
- [features/triggers/models/01-trigger-fold.md](features/triggers/models/01-trigger-fold.md) — trigger subscription and lifecycle
- [ledgers/OPEN-ITEMS.md](ledgers/OPEN-ITEMS.md) — open items
