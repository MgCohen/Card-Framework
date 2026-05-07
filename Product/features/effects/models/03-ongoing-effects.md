---
type: model
status: settled
references: [features/effects/models/01-async-dispatch.md, features/entities/models/01-definition-instance-model.md, features/triggers/models/01-trigger-fold.md, features/effects/models/02-replacement-prevention.md]
tags: [#effects, #continuous, #ongoing]
---

# Continuous And Ongoing Effects — model

## What it is

How ongoing effects are modelled in the Card Framework — effects that persist across multiple actions or turns, managing their own state and lifetime. An ongoing effect is an **attachment entity**: an entity that is created, associated with a host, subscribes to triggers, ticks or reacts over time, and eventually destroys itself. No new pipeline concept is needed — the attachment uses the same entity, trigger, and action machinery as everything else.

```
Create attachment entity
  → Attach to host
  → Subscribe to relevant triggers
  → React each time trigger fires (apply effect, update internal state)
  → When done condition met: dispatch DestroySelf
  → On destruction: framework calls Cleanup → Unregister all subscriptions
```

## Contracts

### Attachments are entities

Attachments follow the same entity model as cards, tokens, and equipment (see [`entities/01-definition-instance-model.md`](features/entities/models/01-definition-instance-model.md)):

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

    // `self` is a stable subscription key — entities use their InstanceId as the trigger owner
    // so subscriptions survive CardRef rotations. Boundary-correct per Product/decisions/0005-objects-over-ids.md.
    public override void Register(ITriggerSystem triggers, InstanceId self)
    {
        triggers.OnAfter<TurnStartSignal>(
            new Trigger<TurnStartSignal>(
                validate: (_, _) => true,
                execute: async (_, scope) =>
                {
                    // Read via the entity handle — IReadOnlyEntity surface
                    var attachment = scope.Entities.Get<HealOverTimeDefinition>(self);
                    var remaining = attachment.GetVariable<int>(Vars.TurnsRemaining);

                    await scope.Dispatch(new HealActorAction { Target = scope.GetHost(self), Amount = HealAmount });
                    await scope.Dispatch(new DecrementTurnsRemainingAction { Target = self });   // handler does the SetBaseValuePayload write
                    if (remaining - 1 <= 0)
                        await scope.Dispatch(new DestroyEntityAction { Target = self });
                }),
            owner: self);
    }
}
```

### Common patterns

**Duration-based (N turns).** Subscribe to `TurnStartSignal`. Decrement a counter variable each tick. Dispatch `DestroyEntityAction` on the attachment when counter reaches zero.

**Condition-based (until end of turn).** Subscribe to `TurnEndSignal`. Dispatch `DestroyEntityAction` on self unconditionally when the signal fires.

**Source-linked / aura (while source is in play).** The source entity creates the attachment on the target when the aura comes into effect. When the source is destroyed or leaves the relevant zone, it dispatches `DestroyEntityAction` on the attachment. The attachment does not need to poll — it is driven by the source's own lifecycle.

**Global effect (affects all entities this turn).** Same pattern — the attachment is created and associated with a global game entity (e.g., the match state or the active player entity) rather than a specific card.

### Turn hooks

Turn lifecycle events (`TurnStartSignal`, `TurnEndSignal`, `PhaseChangeSignal`, etc.) are `GameSignal`s emitted by the turn management system — typically dispatched from inside a turn-advancing `GameAction`'s handler or from `GameRules.OnQueueDrained`.

Attachments subscribe to these signals via `OnAfter<TSignal>` exactly like any other trigger. No special attachment API is needed.

```csharp
// Turn manager emits the signal — attachment subscribes like anything else
scope.Emit(new TurnStartSignal { ActiveActor = currentActor });
```

The full turn lifecycle design is a separate topic (game lifecycle). For ongoing effects, the only requirement is that turn signals exist as `GameSignal`s and flow through the pipeline.

### Cleanup on destruction

All entities have a **framework-level Cleanup hook** that is called automatically when the entity is destroyed. Cleanup calls `Unregister` on all subscriptions owned by that entity.

This is a lifecycle guarantee, not a trigger reaction. An attachment does not need an `OnAfter<DestroyEntityAction>` subscription to clean up its triggers — destruction handles it.

The canonical cleanup verb for modifiers contributed by an attachment entity is `RemoveModifiersBySourcePayload(target.Id, new ModifierSource(attachment.Id))`, dispatched per affected target by the attachment's destruction handler (or by the framework Cleanup hook). Per [`entities/01` §"Mutation API conventions"](features/entities/models/01-definition-instance-model.md), `ModifierSource.Source` is the attachment's `InstanceId`, so this sweep is uniform across equipment, auras, status effects, and other ongoing-effect attachments.

The exact lifecycle design (what "destroyed" means, when Cleanup fires relative to the destruction envelope, what happens to in-flight dispatches that reference the destroyed entity) is a **deferred topic** — entity lifecycle needs a dedicated design pass. The pattern here relies on Cleanup existing; the mechanism is not fully specified yet.

### Creating and attaching

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
3. Calls `definition.Register(triggers, newInstance)` to activate subscriptions.

Attachment storage — whether attachments live in a sub-zone on the host, a global attachment registry, or a dedicated zone — is a detail tied to the zone model and is not locked here.

### Future: attachments as modifier carriers

Once the stats/variables system is redesigned, attachments may serve as modifier carriers: an attachment holds a stat delta, and the host entity reads its effective stat by aggregating its own base value plus the deltas from all attached modifiers. This would make ongoing stat modifications ("+2 attack while enchanted") fit the same attachment model without a separate modifier system. Out of scope for the current session.

## Invariants

Model-local invariants. (See `effects.md ## Invariants` for feature-level rules — subscription ownership in particular.)

- **Attachments are entities, not a separate type.** Same definition / instance / variable / lifecycle machinery as cards and tokens.
- **Subscriptions are owned by the attachment's `InstanceId`.** Cleanup-on-destruction unregisters every subscription owned by that instance.
- **The cleanup verb for modifiers is `RemoveModifiersBySourcePayload(target.Id, new ModifierSource(attachment.Id))`.** Uniform across equipment, auras, status effects, ongoing-effect attachments.
- **Turn signals are `GameSignal`s.** No special attachment-only event channel.

## Decisions

*n/a — no elevated ADR.*

## Examples

See the `HealOverTimeDefinition` example above for the full attachment-entity pattern (definition + register + subscription + tick + self-destruct).

For aura-style attachments where the source's lifecycle drives destruction:

```csharp
// Source's destruction handler
public sealed class DestroyAuraSourceHandler : ICommandHandler<DestroyAuraSourceCommand, DestroyResult>
{
    public Task<DestroyResult> Handle(DestroyAuraSourceCommand cmd, IHandlerContext ctx)
    {
        // Find all attachments sourced to this aura source and dispatch their destruction
        foreach (var attachmentId in ctx.Store.Get<AttachmentRegistry>().AttachmentsOf(cmd.Source))
            ctx.Store.Execute(new DestroyEntityPayload(attachmentId));

        ctx.Entities.Destroy(cmd.Source);
        return Task.FromResult(new DestroyResult(cmd.Source));
    }
}
```

## Open follow-ups

- **Entity lifecycle / destruction** (`oq-entity-destruction`) — what "destroyed" means precisely, when Cleanup fires relative to the destruction envelope, and what happens to in-flight dispatches referencing a destroyed entity. Prerequisite for fully specifying attachment cleanup.
- **Attachment storage** — sub-zone on host, global registry, or dedicated zone. Tied to zone model.
- **Attachment visibility** — are attachments visible to players? Are they shown in UI? Game-specific, but the framework needs to not accidentally hide them.

---

*Below: provenance — this Model has no folded or elevated ADR. The attachment-entity pattern is a direct consequence of the entities Model + the trigger system; the only design choice was to model ongoing effects as attachment entities rather than a parallel mechanism, and that choice is implicit in not introducing a parallel mechanism.*

## Options considered

*n/a — purely descriptive Model. No design alternatives were weighed at this Model's scope; the attachment-entity pattern follows from the entities Model and the trigger fold.*

## Rationale

*n/a — see Contracts.*

## Consequences

*n/a — see Contracts and `effects.md ## Invariants`.*
