---
type: model
status: settled
last-updated: 2026-04-29
builds-on: [features/effects/notes/01-async-dispatch.md]
tags: [#effects, #continuous, #ongoing]
---

# Continuous And Ongoing Effects

This document defines how ongoing effects are modelled in the Card Framework — effects that persist across multiple actions or turns, managing their own state and lifetime.

Builds on:

- [10-entity-definition-instance-model.md](10-entity-definition-instance-model.md) — entity, definition, instance identity
- [21-trigger-reaction-system.md](21-trigger-reaction-system.md) — trigger subscription, AbilityDefinition
- [23-replacement-prevention-effects.md](23-replacement-prevention-effects.md) — ITriggerPipeline, fold mechanics

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
- They have an `InstanceId` — a stable runtime identity.
- They carry variables — used for counters, remaining charges, elapsed turns, etc.
- They are **findable and referenceable** by other game systems and effects.
- Targetability (can other effects select them as targets?) is game-specific. The framework does not enforce it either way.

```csharp
// Example definition
public sealed class HealOverTimeDefinition : EntityDefinition
{
    public int HealAmount  { get; init; }
    public int TurnCount   { get; init; }

    public override void Register(ITriggerSystem triggers, InstanceId self, Context ctx)
    {
        triggers.OnAfter<TurnStartSignal>(
            condition: (_, _) => true,
            react: (_, ctx) =>
            {
                var remaining = ctx.State.GetVariable(self, Vars.TurnsRemaining);
                ctx.Dispatch(new HealActorAction { Target = ctx.GetHost(self), Amount = HealAmount });
                ctx.Dispatch(new SetVariableAction { Target = self, Key = Vars.TurnsRemaining, Value = remaining - 1 });
                if (remaining - 1 <= 0)
                    ctx.Dispatch(new DestroyEntityAction { Target = self });
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
1. Instantiates the entity (assigns `InstanceId`, initializes variables from the definition).
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

- [10-entity-definition-instance-model.md](10-entity-definition-instance-model.md) — entity/definition/instance model
- [12-zone-model.md](12-zone-model.md) — zone model; attachment storage may extend this
- [21-trigger-reaction-system.md](21-trigger-reaction-system.md) — trigger subscription and lifecycle
- [06-open-questions.md](06-open-questions.md) — open items
