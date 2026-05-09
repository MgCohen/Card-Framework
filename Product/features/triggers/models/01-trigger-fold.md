---
type: model
status: settled
references: [features/actions/models/01-command-pipeline.md, features/engine/models/01-game-engine.md, features/engine/models/03-game-rules-runtime.md, features/effects/models/02-effect-scope.md, decisions/0006-two-pipeline-architecture.md]
tags: [#triggers, #reactions, #fold, #model]
---

# Trigger And Reaction System — model

## What it is

The trigger and reaction model for the Card Framework. Every `GameCommand` that enters the pipeline passes through a **before-fold** (where triggers may modify, cancel, or replace the command), then the handler executes (`GameAction` only), then an **after-fold** (where triggers may dispatch reactions). Triggers are typed by the command shape they observe and aggregated through a single fold so the framework handles ordering and replacement uniformly.

```
OnBefore(c)        ← trigger fold; GameAction payload can be modified or cancelled
execute(c)         ← handler (GameAction only; CompositeAction inline; GameSignal skipped)
OnAfter(c, result) ← trigger fold; reactions dispatched
```

This Model replaces the `CommandPreHook` placeholder from earlier drafts (folded into `actions/01-command-pipeline.md` provenance).

## Contracts

### The type hierarchy

Everything that flows through the pipeline shares a base type: `GameCommand`. This is the common surface the trigger system subscribes to.

```csharp
abstract record GameCommand;                     // base — all pipeline-dispatchable types

abstract record GameAction      : GameCommand;   // executable, modifiable, queued through engine
abstract record CompositeAction : GameCommand;   // reusable StateMutation group, runs inline within a handler
abstract record GameSignal      : GameCommand;   // not executable — trigger surface only
```

| Type | Executed by | Modifiable in before fold | Goes through engine queue |
|---|---|---|---|
| `GameAction` | Handler, via engine queue | Yes | Yes |
| `CompositeAction` | Inline, within parent handler | No | No |
| `GameSignal` | Nothing — no execution step | No | No |

**`CompositeAction` vs `Effect`.** `CompositeAction` is a reusable group of `StateMutation`s used *inside a single handler*. It does not orchestrate other `GameAction`s — it only groups state changes for reuse and trigger-fold participation. Example: `MoveCardComposite` encapsulates a remove-then-insert as one logical state-change unit. An **Effect** is a separate, higher-level concept that orchestrates multiple `GameAction`s in sequence. Only effects sequence commands. A `GameAction` handler applies mutations (directly or via `CompositeAction`s) but never dispatches other `GameAction`s itself. See [`features/effects/models/01-effect.md`](features/effects/models/01-effect.md) for the full layer hierarchy.

### `Trigger<T>` — the base type

A trigger is an effect that reacts to a specific command type. It shares the same execution surface as `Effect` (`IEffectScope`), typed to the command it intercepts.

```csharp
public abstract class Trigger<T> where T : GameCommand
{
    public virtual bool Validate(T command, IEffectScope scope) => true;
    public abstract Task Execute(T command, IEffectScope scope);
}
```

`Validate` is the condition check — return `false` to skip this trigger for the current command. `Execute` is the reaction body. Both receive the intercepted command and an `IEffectScope`; `Self` and any other per-instance data are closure-captured at registration.

Cancel and replace are communicated through the scope — no return value needed:

```csharp
// Before-fold: cancel the intercepted command
scope.Cancel("shield absorbed");

// Before-fold: swap in a different command
scope.Replace(new ShieldAbsorbAction(target));
```

Calling `scope.Cancel()` or `scope.Replace()` in an **after-fold** trigger is a no-op (the command already executed). The default (no call) is pass-through — the command continues unchanged.

**Inline construction:**

```csharp
new Trigger<HealActorAction>(
    validate: (cmd, scope) => cmd.Target != self,
    execute: async (cmd, scope) =>
    {
        await scope.Dispatch(new DrawCardAction { Actor = myActorId });
    });
```

**Subclassed:**

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

### Subscription — `ITriggerSystem`

Card abilities register and unregister through `ITriggerSystem`. This is the surface `AbilityDefinition.Register` calls.

```csharp
public interface ITriggerSystem
{
    void OnBefore<T>(Trigger<T> trigger, Reference owner) where T : GameCommand;
    void OnAfter<T>(Trigger<T> trigger, Reference owner)  where T : GameCommand;
    void RemoveAll(Reference owner);
}
```

`owner` is the entity that registered the trigger — used by `RemoveAll` to unsubscribe all triggers belonging to that instance at once.

### Execution — `ITriggerPipeline`

The pipeline runs the trigger fold through `ITriggerPipeline`. This is a pipeline-internal surface; game authors never call it directly. One `TriggerSystem` class implements both `ITriggerSystem` and `ITriggerPipeline`.

```csharp
public interface ITriggerPipeline
{
    Task<TriggerFoldResult> RunBefore(
        GameCommand                    command,
        IReadOnlyList<TriggerMatch>    available,
        IEffectScope                   scope);

    Task RunAfter(
        GameCommand                    command,
        CommandResult                  result,
        IReadOnlyList<TriggerMatch>    available,
        IEffectScope                   scope);
}

public sealed record TriggerFoldResult(
    GameCommand  FinalCommand,
    bool         IsCancelled,
    string?      CancelReason,
    bool         IsReplaced,
    GameAction?  Replacement);

// Each registration wraps a Trigger<T> as non-generic delegates for pipeline dispatch.
public sealed record TriggerMatch(
    Reference                              Owner,      // ADR-0005: stable subscription key — Ref<T> survives CardRef rotations
    int                                    Priority,   // lower runs first; default 0
    Func<GameCommand, IEffectScope, bool>  Condition,
    Func<GameCommand, IEffectScope, Task>  Execute);
```

The `available` list is passed in — the pipeline does not query the trigger registry directly. The registry is queried upstream and filtered by `GameRulesRuntime.FilterTriggers` before the pipeline receives it. Both methods are `async Task`; the default `FifoPipeline` completes synchronously via `Task.FromResult`. The replacement-prevention semantics consumed by the fold (three before-fold outcomes, replace-of-replacement) are owned by [`triggers/01-trigger-fold.md`](features/triggers/models/01-trigger-fold.md).

### The fold mechanic

All triggers registered for type `T` run in sequence. Each sees the output of the previous (or the scope state left by the previous):

```
c0 = original command
trigger1.Validate(c0, scope) → true → trigger1.Execute(c0, scope)  // may call scope.Cancel/Replace
trigger2.Validate(c1, scope) → true → trigger2.Execute(c1, scope)
...
cN = command passed to handler (unless scope.Cancel was called)
```

**Cancel or replace (`GameAction` only):** A trigger calls `scope.Cancel()` or `scope.Replace(next)` during `Execute`. Either stops the fold. The original command does not execute. A replacement dispatches as a fresh command through the channel — it goes through its own full trigger fold from scratch.

### Before-fold outcomes

Every `OnBefore<T>` trigger produces one of three outcomes, communicated through `IEffectScope` rather than a return value:

| Outcome | What happens | Example |
|---|---|---|
| **Modification** | Same type, different property values | Reduce `DealDamageAction.Amount` from 5 to 3 |
| **Cancellation** | Event is stopped; handler never runs | Shield absorbs hit; action cancelled |
| **Substitution** | Different `GameAction` type dispatched in place | `DealDamageAction` → `HealAction` |

```csharp
// Inside Trigger<T>.Execute:
scope.Cancel("shield absorbed");           // cancellation
scope.Replace(new ShieldAbsorbAction(…));  // substitution
// (returning without a scope call) = modification or no-op
```

Modification is expressed by replacing the command with a copy that has different property values (records are immutable):

```csharp
var reduced = cmd with { Amount = Math.Max(0, cmd.Amount - shieldValue) };
scope.Replace(reduced);
```

`FoldResult` (`Keep`/`Cancel`/`Replace`) is a pipeline-internal discriminator on `TriggerFoldResult` — not part of the game-author API. The pipeline reads scope state after `Execute` returns to determine which outcome occurred.

### Replacement-of-replacements is fold-natural

No special mechanism is needed. The fold evaluates each trigger's condition against the *current command* — the output of all prior triggers in the fold sequence:

```
c0 = DealDamageAction { Amount: 5 }

trigger B condition: (c) => c is DealDamageAction → true on c0
trigger B react:     DealDamageAction → HealAction → c1 = HealAction { Amount: 5 }

trigger C condition: (c) => c is DealDamageAction → FALSE on c1 — does not fire
trigger D condition: (c) => c is HealAction      → true on c1  — fires normally
```

If B substitutes `DealDamageAction` with `HealAction`, any trigger watching for `DealDamageAction` no longer applies. Any trigger watching for `HealAction` applies as if it was always the event. Correct by construction — no loop detection or special casing required.

### Priority and ordering

Priority is an `int` on each `OnBefore`/`OnAfter` registration call, defaulting to `0`. Lower value runs first in the fold. The default `FifoPipeline` sorts by priority ascending before running (stable sort preserves registration order among ties). A game that wants to change the convention overrides sorting inside its custom `ITriggerPipeline` — the value is just data.

```csharp
// Normal ability — default priority
triggers.OnBefore<DealDamageAction>(condition, react, owner: self);

// Self-preservation ability — runs before other replacements
triggers.OnBefore<DealDamageAction>(condition, react, owner: self, priority: -10);
```

The framework ships a `FifoPipeline : ITriggerPipeline` that stable-sorts by `Priority` ascending, runs the fold, and returns completed tasks. Games that need MTG-style controller-choice ordering swap in a custom implementation that:

1. Detects multiple applicable triggers
2. Dispatches a `ChooseReplacementOrderAction` (a normal decision, same pattern as `RequestTargetAction`)
3. Awaits the result
4. Runs the fold in the chosen order

### History when replaced

When command A is replaced by B:

- A's envelope is closed and published with `CommandResult.Replaced` — appears in history as a command that was dispatched but did not execute.
- B is dispatched fresh via `engine.Dispatch` (validates + enqueues onto the framework FIFO command queue). B gets its own full fold from scratch. B's envelope is a sibling, not a child, of A's.

History shows both: "A was replaced" and "B executed." The causal link between them is preserved via `OriginCommandId` threading if A and B share the same originating intent. See [`actions/04-history-envelope.md`](features/actions/models/04-history-envelope.md) for envelope shape.

### Context — deleted

`Context` is deleted (S19). Triggers receive `IEffectScope` directly — the same surface effects use. The subscribing entity's identity (`Self`) and any per-instance data are closure-captured at registration, not carried on a context type. State reads go through `scope.State.Get<TState>()`. Reactions dispatch through `scope.Dispatch(...)`.

See [`features/effects/models/02-effect-scope.md` §4.3](features/effects/models/02-effect-scope.md) for the full `IEffectScope` definition.

### Reactions and the dispatch channel

Reaction commands dispatched from inside a trigger go through the same channel as everything else — `engine.Dispatch`. There is no separate reaction queue.

```csharp
// "Whenever another card is healed, draw a card"
var self    = mySelfRef;     // Ref<TSubclass> — typed identity of the registering entity
var myActor = myActorId;

triggers.OnAfter<HealActorAction>(
    new Trigger<HealActorAction>(
        validate: (cmd, scope) => cmd.Target != self,
        execute: async (cmd, scope) =>
        {
            await scope.Dispatch(new DrawCardAction { Actor = myActor });
        }),
    owner: self);
```

The channel receives the dispatched action, validates it through `pipeline.Validate`, and enqueues it on the engine's strict-FIFO command queue. Per [ADR-0006](Product/decisions/0006-two-pipeline-architecture.md) the runtime does **not** gate intake or route commands — command-queue routing is framework-owned.

The runtime contract relevant to the dispatch channel:

```csharp
public abstract class GameRulesRuntime<TDefinition>
    where TDefinition : GameRulesDefinition
{
    public virtual IReadOnlyList<TriggerMatch> FilterTriggers(
        IReadOnlyList<TriggerMatch> candidates,
        GameCommand                 command,
        TriggerPhase                phase)
        => candidates;

    public virtual IReadOnlyList<GameAction> Evaluate(
        GameAction    justExecuted,
        CommandResult result)
        => Array.Empty<GameAction>();

    // Effect-pipeline orchestration surface — separate design pass; tracked as oq-effect-pipeline.
}
```

See [`engine/03-game-rules-runtime.md`](features/engine/models/03-game-rules-runtime.md) for the canonical contract.

#### Game-style scheduling examples

The differentiation between game styles lives at the **effect** level, not at command-queue routing. Per [ADR-0006](Product/decisions/0006-two-pipeline-architecture.md), commands are framework-FIFO; what differs is *when* triggered effects start.

| Style | How triggered effects are scheduled |
|---|---|
| Slay the Spire | Reactions dispatched from `scope.Dispatch` enter framework FIFO; with the queue empty at trigger-fire time they run as the next command — "on the spot" semantics. |
| Hearthstone | Same FIFO behaviour. Reactions queue normally and run after the current command's pipeline completes. |
| MTG | The stack is a **game-state zone**, not the engine queue. Triggered abilities push themselves into the stack zone via state mutation; `runtime.Evaluate` watches the stack and dispatches `ResolveTopOfStack`-style follow-ups. Resolution semantics (LIFO, priority passes) live in game state plus `Evaluate`. |

The pipeline is the same in all three cases. Effect-level orchestration is the customizable layer; the command queue is not.

### Per-instance registration

Trigger subscriptions are registered per instance at runtime. The definition carries the subscription logic; the instance calls `Register` when it enters a trigger-active state and `Unregister` when it leaves.

```csharp
public abstract class AbilityDefinition
{
    public abstract void Register(ITriggerSystem triggers, Reference self);
    public abstract void Unregister(ITriggerSystem triggers, Reference self);
}

// Example ability: "whenever another card is healed, draw a card"
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

`Self` and any other per-instance data are closure-captured. `Context` is not passed to `Register` — it was deleted in S19.

The lifecycle of when `Register`/`Unregister` is called — and what qualifies as "trigger-active" — is a deferred design topic tied to the zone model and game lifecycle.

### Emitting `GameSignal`s and `CompositeAction`s from a handler

Handlers emit `GameSignal`s and execute `CompositeAction`s through the scope. Both pass through their own trigger folds before/after:

```csharp
// Inside a handler:
scope.Emit(new ArmorBrokenSignal(target));          // GameSignal — before/after triggers fire; no handler
scope.Execute(new MoveCardComposite(source, zone)); // CompositeAction — before/after triggers fire; executes inline
```

`scope.Emit` and `scope.Execute` are the scope-level equivalents of `engine.Dispatch` for non-queued types. A handler should never dispatch another `GameAction` directly — that's an `Effect`'s responsibility.

### Pipeline integration

The full `pipeline.Execute(action)` shape (envelope open → filter → before-fold → handler → after-fold → envelope close) is owned by [`actions/01-command-pipeline.md` §Pipeline mechanics](features/actions/models/01-command-pipeline.md). Trigger-side notes:

- The pipeline holds `ITriggerPipeline` (execution surface); card abilities hold `ITriggerSystem` (subscription surface). One `TriggerSystem` class implements both.
- `CompositeAction` and `GameSignal` use a lighter path — trigger folds only, no handler, no envelope of their own; their entries fold into the parent command's envelope.

## Invariants

Model-local invariants. (See `triggers.md ## Invariants` for feature-level rules — strict phase order, fresh fold on replace, after-phase no-ops, handlers don't dispatch, registry filtered upstream, split surfaces.)

- **`Trigger<T>` is typed by command shape.** No untyped trigger surface; the type system enforces the match.
- **`Validate` returning `false` skips this trigger only.** Other triggers in the fold are unaffected.
- **`scope` is the same `IEffectScope` Effects use.** No separate trigger context type; closure capture for per-instance data.
- **Reactions enter the same dispatch channel as all other commands.** No separate reaction queue; framework-FIFO routing per ADR-0006.
- **`AbilityDefinition.Register` / `Unregister` is the per-instance lifecycle hook.** `RemoveAll(self)` discharges every trigger an instance owns.

## Decisions

Linked: [`Product/decisions/0006-two-pipeline-architecture.md`](Product/decisions/0006-two-pipeline-architecture.md) — Two-pipeline architecture: framework command pipeline + game effect pipeline. Cross-cutting (engine + triggers + effects + actions). Shapes how the trigger fold integrates with the framework FIFO command queue (the runtime does not gate intake or route commands; effect-level orchestration is the customizable layer where game styles like Slay the Spire / Hearthstone / MTG diverge).

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

A traced before-fold with replacement:

```
Original: DealDamageAction(target=goblin, amount=3)
  trigger1 (Shield ability on goblin):
    Validate → true (target matches)
    Execute  → scope.Replace(new ShieldAbsorbAction(goblin))
  → fold stops; ShieldAbsorbAction dispatches as fresh command
    → runs through its own full before-fold from scratch
    → no handler runs for the original DealDamageAction
```

## Open follow-ups

- **Subscription lifecycle** — when `Register`/`Unregister` is called; relationship to zone transitions and game lifecycle.
- **Trigger ordering** — what determines sequence within the fold when multiple triggers match the same type. FIFO by registration? Priority weight? Game-configurable?
- **Async suspension** — triggers that require player input (targeting, choices) still unresolved. Unblocks the broader suspension design.
- **Stash release mechanics** — MTG-style stash needs a `Release(key)` call and ordering rules. Not designed here.
- ~~**`ITriggerSystem` vs `ITriggerPipeline` unification** — two competing shapes remain in play; see `OPEN-ITEMS.md`.~~ → resolved session-20260501-1132: split into `ITriggerSystem` (subscription) and `ITriggerPipeline` (execution); one `TriggerSystem` class implements both. Shape reconciled with effects/02 §3 in session-20260501-1545 (ad-20260501-025 discharged).

---

*Below: provenance — feature-internal decisions folded on 2026-05-08 from `features/triggers/decisions/0001-trigger-fold-semantics.md` and `features/triggers/decisions/0002-onbefore-onafter.md` (both originally landed 2026-04-29). Single-feature scope (the trigger fold mechanism is internal to triggers; engine and actions consume the resulting types but don't co-decide them).*

## Options considered

### Q1 — Trigger fold semantics (formerly ADR-0001 — folded)

When multiple triggers fire on one event, they need to be aggregated into a single decision (or sequence of dispatches) without callers writing event-loop code. The model needs to handle ordering, priority, and replacement uniformly.

- **A — Synchronous broadcast, first listener wins.** Rejected — no story for replacement, prevention, or priority.
- **B — Per-event subscription lists with manual aggregation.** Rejected — every event reinvents the loop; no shared semantics.
- **C — A fold over registered triggers, producing an aggregated `TriggerFoldResult` consumed by the engine.** **Chosen.** Priority and replacement/prevention are expressed as fold steps. The engine consumes the result; effects don't write the loop.

### Q3 — Replacement priority (formerly `effects/02 §Q1` — folded 2026-05-09)

> Originally a single-feature decision in `triggers/01-trigger-fold.md`. Folded here when before-fold was dissolved (per the type-required Model rule — before-fold is a step inside `ITriggerPipeline.RunBefore`, not a model of its own).

Replacement effects ("if X would happen, instead Y") and prevention effects ("X doesn't happen") need a deterministic priority story. Without one, two replacements in the same fold produce nondeterministic outcomes.

- **A — Order replacements by registration time.** Rejected — registration order isn't a deterministic, replay-safe property across save/load and multiplayer.
- **B — Player-controlled tie-breaks at every collision.** Rejected — spams decisions for negligible gain in floor cases.
- **C — Fold-ordered priority.** Replacements declare priority ranks; the trigger fold consumes them in rank order; ties break by stable, explicit ordering (e.g., source entity ID). **Chosen.**

Explicit priority on each registration plus stable tie-break gives deterministic replay across replacement-heavy effects without forcing a player decision at every collision. Games that want player-controlled ordering opt in via a custom `ITriggerPipeline` that dispatches a `ChooseReplacementOrderAction`. Authoring cost is small (one `priority` argument with a default of 0); the determinism payoff is large.

### Q2 — `OnBefore<T>` / `OnAfter<T>` (formerly ADR-0002 — folded)

Earlier drafts (notably the legacy "doc 17") used `CommandPreHook` and `CommandReaction` for pre/post listeners. The names hid the typed nature of the dispatch and didn't compose cleanly with the trigger fold.

- **A — Keep `CommandPreHook` / `CommandReaction`.** Rejected — the names suggest one-off hooks rather than a typed listener pair; lose the symmetry of pre/post.
- **B — Single `Trigger<T>` with a `phase` enum.** Rejected — the symmetry of pre/post is structural and worth surfacing in types.
- **C — Typed listener pair `OnBefore<T>` / `OnAfter<T>`, both folded by `ITriggerPipeline`.** **Chosen.** Type-safe registration, clearer reading of the loop.

## Rationale

The fold mechanic (Q1) gives the framework a single uniform shape for trigger aggregation: every event flows through the same fold, and replacement / cancellation are expressed as fold contributions rather than ad-hoc event-loop code. The typed listener pair (Q2) makes the symmetry of pre/post visible in the type system — `OnBefore<T>` and `OnAfter<T>` are different surfaces because they have different semantic affordances (cancel/replace vs. react), and the type signature carries that.

Together: trigger authors write `Trigger<T>` instances and register them under one of two phases. The framework handles every other concern (ordering, fold execution, replacement-as-fresh-fold, reactions-into-same-channel, registry filtering by `GameRulesRuntime`).

## Consequences

- **Enables:** uniform handling of replacement, prevention, and priority via a single fold mechanism; type-safe trigger registration; clear pre/post semantic separation; consistent dispatch channel for reactions (no separate reaction queue); game-controlled availability via `FilterTriggers` (phase, mana, stack lock).
- **Constrains:** trigger authors think in terms of fold contributions, not direct dispatch; cancel/replace flow through `scope.Cancel` / `scope.Replace`, not return values; `CommandPreHook` and `CommandReaction` types are retired (legacy doc 17 references folded into `actions/01-command-pipeline.md` provenance).
- **Cascade:** the `TriggerFoldResult` shape is consumed by `actions/01-command-pipeline.md` (pipeline branches on `IsCancelled` / `IsReplaced`); the `GameRulesRuntime.FilterTriggers` and framework-FIFO routing contracts are owned by `engine/03-game-rules-runtime.md` and shaped by [ADR-0006](Product/decisions/0006-two-pipeline-architecture.md).
