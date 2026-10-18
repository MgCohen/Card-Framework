---
type: model
status: abandoned
archived_on: 2026-10-18
reason: "Written for the old state model (slices, Ref<T>, MatchStateSlice) and never built. Read it for the open questions, not as the contract; the replacement design comes from Product/spikes/framework-roadmap.md."
references: [features/actions/archive/01-command-pipeline.md, features/triggers/archive/02-trigger-pipeline.md, features/effects/archive/02-effect-scope.md, features/engine/archive/03-game-rules-runtime.md, decisions/0006-two-pipeline-architecture.md]
tags: [#triggers, #reactions, #model]
---

> **Archived 2026-10-18.** Written for the old state model (slices, Ref<T>, MatchStateSlice) and never built. Read it for the open questions, not as the contract; the replacement design comes from Product/spikes/framework-roadmap.md.

# Trigger — model

> **Amendment (2026-05-11) per [ADR-0013](Product/decisions/0013-engine-pipeline-orchestration.md).** `Trigger<T>.Validate(cmd, scope)` is now THE eligibility surface — was previously paired with `GameRulesRuntime.FilterTriggers` (now retired). `TriggerSystem` iterates registered triggers, calls each one's `Validate`, and folds the passing ones. No central filter, no two-step process. Game-wide eligibility policies express through per-trigger `Validate` logic or a future predicate registry; v1 is per-trigger only. Same `ITriggerSystem.OnBefore<T>` / `.OnAfter<T>` API at game install and at per-instance `AbilityDefinition.Register`; no static-vs-dynamic distinction.

## What it is

`Trigger<T>` is the **typed reaction class** game authors register to react to specific commands. It shares the same execution surface as `Effect` (`IEffectScope`), typed to the command shape it intercepts. `Validate(cmd, scope)` is the condition check (return `false` to skip); `Execute(cmd, scope)` is the reaction body. Triggers are registered through `ITriggerSystem` from `AbilityDefinition.Register`; per-instance data (`Self`, owner-specific captures) is closure-captured at registration. The fold mechanism that *runs* registered triggers lives in [`02-trigger-pipeline.md`](features/triggers/archive/02-trigger-pipeline.md); this Model owns the triggers themselves and how they get registered.

```csharp
public abstract class Trigger<T> where T : GameCommand
{
    public virtual bool Validate(T command, IEffectScope scope) => true;
    public abstract Task Execute(T command, IEffectScope scope);
}
```

## Contracts

### `Trigger<T>` — the base class

A trigger reacts to a specific command type. The type parameter is the command shape it observes; the framework's pipeline matches by type at fold time.

```csharp
public abstract class Trigger<T> where T : GameCommand
{
    public virtual bool Validate(T command, IEffectScope scope) => true;
    public abstract Task Execute(T command, IEffectScope scope);
}
```

- **`Validate`** — condition check, returning `false` skips this trigger for the current command. Other triggers in the fold are unaffected. Default returns `true` so authors override only when they have a condition.
- **`Execute`** — the reaction body. Receives the intercepted command and an `IEffectScope` (the same surface effects use; see [`effects/02-effect-scope.md`](features/effects/archive/02-effect-scope.md)).

The command-type hierarchy `T` ranges over (`GameCommand` / `GameAction` / `CompositeAction` / `GameSignal`) is owned by [`actions/01-command-pipeline.md` §`GameCommand` — type hierarchy](features/actions/archive/01-command-pipeline.md). Triggers can subscribe to any concrete subtype.

### Cancel and replace

Cancel and replace are communicated through `IEffectScope` — no return value. They are valid only in a before-fold trigger (the after-fold execution path is read-only with respect to command flow):

```csharp
// Before-fold: cancel the intercepted command
scope.Cancel("shield absorbed");

// Before-fold: swap in a different command
scope.Replace(new ShieldAbsorbAction(target));
```

Calling `scope.Cancel()` or `scope.Replace()` in an **after-fold** trigger is a no-op (the command already executed). The default (no call) is pass-through — the command continues unchanged.

The ordering invariants of cancel/replace within the fold (replace creates a fresh fold, three-outcome semantics, replacement-of-replacements) belong to the pipeline mechanism — see [`02-trigger-pipeline.md`](features/triggers/archive/02-trigger-pipeline.md).

### Authoring shapes

**Inline construction** — pass `validate` and `execute` lambdas to a constructor overload:

```csharp
new Trigger<HealActorAction>(
    validate: (cmd, scope) => cmd.Target != self,
    execute: async (cmd, scope) =>
    {
        await scope.Dispatch(new DrawCardAction { Actor = myActorId });
    });
```

**Subclassed** — derive `Trigger<T>` and override `Validate` / `Execute`:

```csharp
public sealed class DrawOnHeal : Trigger<HealActorAction>
{
    private readonly Reference _self;
    private readonly ActorId   _actor;

    public DrawOnHeal(Reference self, ActorId actor) { _self = self; _actor = actor; }

    public override bool Validate(HealActorAction cmd, IEffectScope scope)
        => cmd.Target != _self;

    public override async Task Execute(HealActorAction cmd, IEffectScope scope)
        => await scope.Dispatch(new DrawCardAction { Actor = _actor });
}
```

The choice between inline and subclassed is authoring ergonomics; both produce the same runtime trigger. Subclassed is preferred for triggers with substantive reaction logic, replay-traced fields, or test-friendly construction.

### Closure capture for per-instance data

Per-instance data (the registering entity's `SelfRef`, owning actor's `ActorId`, ability-specific configuration) is **closure-captured at registration**, not threaded through a context object. This keeps the `Trigger<T>` surface small and replay-deterministic — the captured values become part of the trigger's identity and survive snapshot/restore alongside the registration that holds them.

Per [ADR-0005](Product/decisions/0005-objects-over-ids.md), entity identity captured in a trigger's closure uses `Ref<T>` (the typed runtime ref) rather than `CardRef` (which rotates on hide/move). `Self` reads `instance.SelfRef` at registration; the captured value remains valid across `CardRef` rotations because `Ref<T>` is stable for the instance's lifetime.

### `ITriggerSystem` — the subscription surface

Card abilities register and unregister through `ITriggerSystem`. This is the surface `AbilityDefinition.Register` calls.

```csharp
public interface ITriggerSystem
{
    void OnBefore<T>(Trigger<T> trigger, Reference owner) where T : GameCommand;
    void OnAfter<T>(Trigger<T> trigger, Reference owner)  where T : GameCommand;
    void RemoveAll(Reference owner);
}
```

- `OnBefore<T>` — register the trigger to fire in the before-fold for command type `T`.
- `OnAfter<T>` — register to fire in the after-fold.
- `RemoveAll(owner)` — discharge every trigger registered under that owner. Single-call cleanup at zone-exit / entity destruction.

`owner` is the entity that registered the trigger — used by `RemoveAll` to unsubscribe all triggers belonging to that instance at once. Owner is `Reference`-typed (typically a `Ref<T>`) so it's stable across `CardRef` rotations.

`ITriggerSystem` is the game-author-facing half of the trigger system. The pipeline-internal execution surface is [`ITriggerPipeline`](features/triggers/archive/02-trigger-pipeline.md) — both interfaces are implemented by a single `TriggerSystem` class.

### `AbilityDefinition` — the per-instance lifecycle

Trigger subscriptions are registered per instance at runtime. The definition carries the subscription logic; the instance calls `Register` when it enters a trigger-active state and `Unregister` when it leaves.

```csharp
public abstract class AbilityDefinition
{
    public abstract void Register(ITriggerSystem triggers, Reference self);
    public abstract void Unregister(ITriggerSystem triggers, Reference self);
}

// Example: "whenever another card is healed, draw a card"
public sealed class WhenOtherHealedDrawAbility : AbilityDefinition
{
    public override void Register(ITriggerSystem triggers, Reference self)
    {
        triggers.OnAfter<HealActorAction>(
            new Trigger<HealActorAction>(
                validate: (cmd, scope) => cmd.Target != self,
                execute: async (cmd, scope) =>
                {
                    await scope.Dispatch(new DrawCardAction { Actor = self });
                }),
            owner: self);
    }

    public override void Unregister(ITriggerSystem triggers, Reference self)
    {
        triggers.RemoveAll(self);
    }
}
```

`Self` and any other per-instance data are closure-captured. `Context` is not passed to `Register` — it was deleted in S19; triggers receive `IEffectScope` directly inside their own `Execute`, not via a registration-time context type.

The lifecycle of when `Register`/`Unregister` is called — and what qualifies as "trigger-active" — is a deferred design topic tied to the zone model and game lifecycle (`oq-trigger-subscription-lifecycle` in [`OPEN-ITEMS.md`](ledgers/OPEN-ITEMS.md)).

## Invariants

1. **`Trigger<T>` is typed by command shape.** No untyped trigger surface; the type system enforces the match.
2. **`Validate` returning `false` skips this trigger only.** Other triggers in the fold are unaffected.
3. **`scope` is the same `IEffectScope` Effects use.** No separate trigger context type; closure capture for per-instance data.
4. **Cancel / Replace are valid only in before-fold.** After-fold calls swallow silently; the command already executed.
5. **`owner` is `Reference`-typed.** Per ADR-0005, stable across `CardRef` rotations; typically `Ref<T>` from the registering entity.
6. **`AbilityDefinition.Register` / `Unregister` is the per-instance lifecycle hook.** `RemoveAll(self)` discharges every trigger an instance owns in one call.

## Decisions

Linked: [`Product/decisions/0006-two-pipeline-architecture.md`](Product/decisions/0006-two-pipeline-architecture.md) — Two-pipeline architecture: framework command pipeline + game effect pipeline. Cross-cutting (engine + triggers + effects + actions). Shapes how the trigger fold integrates with the framework FIFO command queue.

## Examples

```csharp
// Inline before-fold trigger that prevents damage above a threshold
triggers.OnBefore<DealDamageAction>(
    new Trigger<DealDamageAction>(
        validate: (cmd, scope) => cmd.Target == self,
        execute: async (cmd, scope) =>
        {
            if (cmd.Amount > 5)
                scope.Cancel("damage cap exceeded");
        }),
    owner: self);

// After-fold trigger that draws a card on heal
triggers.OnAfter<HealActorAction>(
    new Trigger<HealActorAction>(
        validate: (cmd, scope) => cmd.Target != self,
        execute: async (cmd, scope) =>
        {
            await scope.Dispatch(new DrawCardAction { Actor = self });
        }),
    owner: self);
```

## Open follow-ups

- **Subscription lifecycle** — when `Register`/`Unregister` is called; relationship to zone transitions and game lifecycle. (`oq-trigger-subscription-lifecycle`)

---

*Below: provenance — feature-internal decision (Q2, formerly `triggers/decisions/0002-onbefore-onafter.md`) folded on 2026-05-08. Single-feature scope (the `OnBefore<T>` / `OnAfter<T>` naming and `Trigger<T>` shape are internal to triggers).*

## Options considered

### Q2 — `OnBefore<T>` / `OnAfter<T>` naming

Earlier drafts (notably the legacy "doc 17") used `CommandPreHook` and `CommandReaction` for pre/post listeners. The names hid the typed nature of the dispatch and didn't compose cleanly with the trigger fold.

- **A — Keep `CommandPreHook` / `CommandReaction`.** Rejected — the names suggest one-off hooks rather than a typed listener pair; lose the symmetry of pre/post.
- **B — Single `Trigger<T>` with a `phase` enum.** Rejected — the symmetry of pre/post is structural and worth surfacing in types.
- **C — Typed listener pair `OnBefore<T>` / `OnAfter<T>`, both folded by `ITriggerPipeline`.** **Chosen.** Type-safe registration, clearer reading of the loop.

## Rationale

Keeping `Trigger<T>` typed by command shape (rather than untyped with a `T : Type` field) gives compile-time matching and lets the framework's pipeline dispatch by exact type without a runtime `is`-check. The `OnBefore<T>` / `OnAfter<T>` split surfaces the structural difference (cancel/replace affordance vs. reaction-only) in the type system.

Closure-capture for per-instance data (rather than a context object) keeps the surface small and replay-deterministic — captured values are part of the trigger's identity.

## Consequences

- **Enables** type-safe trigger registration (compiler catches command-type mismatches); clear pre/post semantic separation; replay-deterministic per-instance data via closure-capture; uniform cleanup via `RemoveAll(self)`.
- **Constrains** trigger authors to the `Trigger<T>` surface (no untyped reactions); cancel/replace flow through `scope.Cancel` / `scope.Replace` rather than return values; `CommandPreHook` and `CommandReaction` types are retired (legacy doc-17 references folded into [`actions/01-command-pipeline.md`](features/actions/archive/01-command-pipeline.md) provenance).
