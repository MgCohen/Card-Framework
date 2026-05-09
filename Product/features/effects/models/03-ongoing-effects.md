---
type: model
status: settled
references: [features/effects/models/01-effect.md, features/entities/models/01-definition-instance-model.md, features/triggers/models/01-trigger-fold.md, features/effects/models/02-replacement-prevention.md]
tags: [#effects, #continuous, #ongoing]
---

# Ongoing Effects — model

## What it is

How ongoing effects — effects that persist across multiple actions or turns, managing their own state and lifetime — are modelled. An ongoing effect is an **attachment entity**: an entity that is created, associated with a host, subscribes to triggers, ticks or reacts over time, and eventually destroys itself. No new pipeline concept is needed — the attachment uses the same entity, trigger, and action machinery as everything else.

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

Attachments follow the same entity model as cards, tokens, and equipment ([`entities/01`](features/entities/models/01-definition-instance-model.md)):

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

### Common patterns

**Duration-based (N turns).** Subscribe to `TurnStartSignal`. Decrement a counter variable each tick. Dispatch `DestroyEntityAction` on the attachment when counter reaches zero.

**Condition-based (until end of turn).** Subscribe to `TurnEndSignal`. Dispatch `DestroyEntityAction` on self unconditionally when the signal fires.

**Source-linked / aura (while source is in play).** The source entity creates the attachment on the target when the aura comes into effect. When the source is destroyed or leaves the relevant zone, it dispatches `DestroyEntityAction` on the attachment. The attachment does not need to poll — it is driven by the source's own lifecycle.

**Global effect (affects all entities this turn).** Same pattern — the attachment is created and associated with a global game entity (e.g., the match state or the active player entity) rather than a specific card.

### Turn hooks

Turn lifecycle events (`TurnStartSignal`, `TurnEndSignal`, `PhaseChangeSignal`, etc.) are `GameSignal`s emitted by the turn management system — typically from inside a turn-advancing `GameAction`'s handler. Phase-advance follow-ups themselves are dispatched from `runtime.Evaluate` (state-based; see [`engine/02-gamerules-contract.md`](features/engine/models/02-gamerules-contract.md)).

Attachments subscribe to these signals via `OnAfter<TSignal>` exactly like any other trigger. No special attachment API is needed.

```csharp
// Turn manager emits the signal — attachment subscribes like anything else
scope.Emit(new TurnStartSignal { ActiveActor = currentActor });
```

The full turn lifecycle design is a separate topic (task 12 — game lifecycle). For ongoing effects, the only requirement is that turn signals exist as `GameSignal`s and flow through the pipeline.

### Cleanup on destruction

All entities have a **framework-level Cleanup hook** that is called automatically when the entity is destroyed. Cleanup calls `Unregister` on all subscriptions owned by that entity.

This is a lifecycle guarantee, not a trigger reaction. An attachment does not need an `OnAfter<DestroyEntityAction>` subscription to clean up its triggers — destruction handles it.

The canonical cleanup verb for modifiers contributed by an attachment entity is `RemoveModifiersBySourcePayload(target.SelfRef, new ModifierSource(attachment.SelfRef, 0))`, dispatched per affected target by the attachment's destruction handler (or by the framework Cleanup hook). The bulk variant `store.RemoveModifiersFromSource(source)` sweeps every entity in one call. Per [`entities/01` "Mutation API conventions"](features/entities/models/01-definition-instance-model.md), `ModifierSource.Source` is the attachment's `Reference`, so this sweep is uniform across equipment, auras, status effects, and other ongoing-effect attachments.

The exact lifecycle design (what "destroyed" means, when Cleanup fires relative to the destruction envelope, what happens to in-flight dispatches that reference the destroyed entity) is a **deferred topic** (`oq-entity-destruction`). The pattern here relies on Cleanup existing; the mechanism is not fully specified yet.

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

1. Instantiates the entity via `StoreEntities.Spawn<TDef>` (allocates `Ref<T>`, registers the empty `EntityState` slice, registers the handle in the catalog).
2. Associates it with the host (stored as a relationship on the host entity or in a dedicated attachment registry).
3. Calls `definition.Register(triggers, newInstance, ctx)` to activate subscriptions.

Attachment storage — whether attachments live in a sub-zone on the host, a global attachment registry, or a dedicated zone — is a detail tied to the zone model and is not locked here.

## Invariants

- **Attachments are entities, not a separate concept.** Same `Definition` / typed `Ref<T>` / `EntityState` shape as cards, tokens, equipment. No new pipeline machinery.
- **Subscriptions are owned by `Ref<T>`.** The attachment uses its typed `Ref<TAttachment>` as the `owner` argument on every `OnBefore`/`OnAfter` registration. This is the stable subscription key per [ADR-0005](Product/decisions/0005-objects-over-ids.md).
- **Cleanup is automatic.** The framework Cleanup hook unregisters every subscription owned by a destroyed entity — attachment authors do not need to write `OnAfter<DestroyEntityAction>` cleanup code.
- **Modifier cleanup uses `ModifierSource = attachment.SelfRef`.** Modifiers contributed by an attachment carry the attachment's `Ref` as their source; cleanup is uniform via `RemoveModifiersBySourcePayload` (per-target) or `store.RemoveModifiersFromSource` (bulk).
- **Attachments dispatch `DestroyEntityAction` on themselves to terminate.** No "self-delete" primitive — termination is a normal `GameAction` with full envelope and trigger fold.

## Decisions

*n/a — no elevated ADR.* (The attachment-entity pattern is descriptive; it sits on top of `entities/01` and `triggers/01` without introducing a new project-wide decision.)

## Examples

### Heal-over-time aura (3 turns, +1 heal)

Definition above. The flow:

1. Source card resolves; its handler dispatches `CreateAndAttachAction { Definition = new HealOverTimeDefinition(...), Host = target }`.
2. `Register` subscribes `OnAfter<TurnStartSignal>` with `owner = self` (the new attachment's `Ref<HealOverTime>`).
3. Each turn start, the trigger fires: dispatches `HealActorAction`, decrements `TurnsRemaining` via `DecrementTurnsRemainingAction`, dispatches `DestroyEntityAction` if exhausted.
4. On destruction, the framework Cleanup hook unregisters the `OnAfter<TurnStartSignal>` subscription. No further ticks fire.

### Aura while source is in play

A creature's static "+1/+1 to adjacent allies" aura:

1. Creature ETB handler dispatches `CreateAndAttachAction` per adjacent ally, host = ally.
2. Each attachment registers as a modifier source on its host: `ally.Storage.AddModifier(scope.Modifiers.Create(Vars.Attack, new IntAddModifier(+1), source: new ModifierSource(attachment.SelfRef, 0)))`.
3. Creature's destruction handler iterates its attachments and dispatches `DestroyEntityAction` for each.
4. Each attachment's destruction triggers `store.RemoveModifiersFromSource(new ModifierSource(attachment.SelfRef, 0))` — modifier removed from the host atomically.

### Stateful prevention ("prevent the next 3 damage")

Attachment with `OnBefore<DamageAction>` subscription that:

1. Reads `Remaining` variable.
2. If `Remaining > 0`, dispatches a `Replace` with `cmd with { Amount = max(0, cmd.Amount - Remaining) }` and decrements `Remaining` to zero.
3. If `Remaining == 0`, dispatches `DestroyEntityAction { Target = self }`.

Single-instance shape per [`02-replacement-prevention.md`](features/effects/models/02-replacement-prevention.md) §"Prevention as modification" — the attachment is the place that owns the counter.

## Open follow-ups

- **Entity lifecycle / destruction** (`oq-entity-destruction`) — what "destroyed" means precisely, when Cleanup fires relative to the destruction envelope, and what happens to in-flight dispatches referencing a destroyed entity. Prerequisite for fully specifying attachment cleanup.
- **Attachment storage** — sub-zone on host, global registry, or dedicated zone. Tied to zone model.
- **Attachment visibility** — are attachments visible to players? Are they shown in UI? Game-specific, but the framework needs to not accidentally hide them.
- **Attachments as modifier carriers** — once the stats/variables system is redesigned, attachments may serve as modifier carriers: an attachment holds a stat delta, and the host entity reads its effective stat by aggregating its own base value plus the deltas from all attached modifiers. Out of scope for the current session; depends on the stats redesign work.

---

*Below: provenance.*

## Options considered

*n/a — descriptive Model. The attachment-entity shape sits on top of `entities/01` (definitions/instances/refs/variables/modifiers) and `triggers/01` (subscriptions, fold). No new decision was made for this Model; the only choice is "ongoing effects are entities, not a new pipeline concept" which falls out of the entity model directly.*

## Rationale

Modelling ongoing effects as attachment entities reuses existing machinery (definitions, refs, variables, modifiers, subscriptions, Cleanup) instead of introducing a parallel "effect lifecycle" pipeline. Authoring an aura is the same shape as authoring a card; the framework does not need a separate registry, scheduler, or lifecycle hook for ongoing effects. Polling is replaced by trigger subscriptions; termination is a normal `GameAction` with full envelope and fold.

## Consequences

- **Enables** uniform cleanup across all ongoing-effect shapes (auras, equipment, status, prevention counters); polymorphic targeting (any code that handles entities handles attachments); deterministic replay (attachment lifecycle is in the envelope stream).
- **Constrains** ongoing effects to declare a `Definition` and use `OnAfter`/`OnBefore` subscriptions; depends on `oq-entity-destruction` settling for full cleanup determinism; attachment storage shape is delegated to zone model / future authoring feature.
