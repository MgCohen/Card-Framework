---
type: spike
status: draft
tags: [#spike, #effects, #effect-pipeline, #runtime, #scope]
---

# Effect Pipeline — Design Sketch

> **Status: draft / iterating.** Working sketch for the effect-pipeline shape. Captures the decisions from the effect-pipeline design pass (2026-05-07 session). **Self-contained** — readable standalone. **Not yet propagated to canonical Models** by design — Models stay frozen until this sketch settles. When iteration finishes, the resolution path is to create ADR-0007 and patch `effects/04`, `effects/01`, `triggers/01`, `engine/01`, `engine/02`, plus ledgers.

## TL;DR

- Two pipelines (per [ADR-0006](decisions/0006-two-pipeline-architecture.md)): **command pipeline** (framework-FIFO, locked) and **effect pipeline** (game-customizable, this sketch).
- **`IEffectPipeline` is an injected service**, symmetric with `ICommandPipeline`. Default = inline; games swap in DI.
- **`EffectDefinition` (recipe) + `EffectInstance` (runtime record).** Definition lives on the source's data; instance is the flat immutable wire-form `(Source, EffectId, Bindings)`.
- **State holds `EffectInstance` records** (immutable). MTG stack zone, Hearthstone trigger queue = game-authored slices over these records.
- **Runtime stays at `FilterTriggers + Evaluate`.** No `ScheduleEffect` method.
- **Chain scope only at effect-run time.** Drain doesn't construct scopes. `pipeline.Execute` takes optional `chainScope`.
- **Re-entrance via `Fired` set** on the chain scope.
- **Two execution verbs.** `IEffectPipeline.RunEffect` (scheduling decision) and `EffectInstance.Execute(scope)` extension (direct execution, used by resolution handlers).
- **Triggers compile to effects.** `Trigger<T>` is authoring sugar; the fold wraps a matched trigger as an `EffectInstance` and calls `IEffectPipeline.RunEffect` — same path as active effects.

## Context (locked from prior sessions)

The locked context this sketch must respect:

- Three-level model — Commands / Effects / Triggers ⊆ Effects (per [ADR-0006](decisions/0006-two-pipeline-architecture.md)).
- Command pipeline is framework-FIFO; `engine.Dispatch(GameAction, out string? reason)` is try-get; `runtime` contract is `FilterTriggers + Evaluate`; no `OnIncomingCommand` / `OnQueueDrained` / `IIntakeQueue` / `Route` / etc.
- `IEffectScope.Dispatch` runs `GameAction`s **inline** — pipeline.Execute → fold → handler → fold → envelope, awaited synchronously by the calling effect.
- `engine.Dispatch` is queued — framework-FIFO, runs after current `pipeline.Execute` returns.
- Mutation discipline (per [`effects.md` §Mutation discipline](features/effects/effects.md)) — only handlers invoke mutators (via `Store.Execute(payload)`); effects orchestrate, never mutate directly.
- `IHandlerContext` exposes `Store`, `Actions` (snapshot writer), `Resolver` (decision awaiter), `Modifiers`. **No `RunEffect` surface today** — that's the documented gap this sketch closes.
- ADR-0002 (refreshed) — runtime holds zero **gameplay** state. Engine-internal plumbing is fine on the engine; gameplay state lives in Scaffold slices.

## Big decisions

**D1 — `IEffectPipeline` is an injected service**, symmetric with `ICommandPipeline`. Framework ships `InlineEffectPipeline` as the default (runs effects immediately). Games swap implementations in DI: `MtgStackEffectPipeline`, `HearthstoneTriggerQueueEffectPipeline`, etc. The runtime contract is unaffected — there's no `ScheduleEffect` method on `GameRulesRuntime<T>`.

**D2 — State holds `EffectInstance` records, not Effect objects with behavior.** What's "in flight" (on a stack zone, in a trigger queue) is an immutable record carrying `(Source, EffectId, Bindings)`. This keeps state snapshot-friendly and aligns with the existing record-on-slice pattern. The user's concern about "mutable effects on state" is addressed structurally — the wire form is pure data.

**D3 — Naming: `EffectDefinition` + `EffectInstance`.**
- `EffectDefinition` — the recipe. Authored. Carries `Validate` + `Execute` as lambdas. Constructible inline (like `Trigger<T>`) or subclassable. Lives on the source's definition (e.g., on `CardDefinition`).
- `EffectInstance` — the runtime record. Flat, immutable, snapshot-friendly. Flows through the pipeline, lands on state, gets resolved.

**D4 — Effects are authored without required subclasses.** `EffectDefinition` is constructible with lambdas, mirroring `Trigger<T>`'s authoring pattern. Subclassing is allowed for typed convenience but not required. Aligns with "effects are data-driven; subclasses are for commands."

**D5 — Runtime stays at `FilterTriggers + Evaluate`.** No `ScheduleEffect` method. Effect-pipeline orchestration is a service (D1), not a runtime method. The runtime continues to drift toward "state evaluator" — `FilterTriggers` is the next candidate to revisit (long-term, not in this sketch).

**D6 — Chain scope created only when an effect runs.** Drain doesn't construct scopes. `pipeline.Execute(action, chainScope?)` takes an optional scope; null means top-level (called from drain), non-null means called from `scope.Dispatch` inside an effect. The trigger fold inherits the calling chain.

**D7 — Re-entrance prevention via the chain's `Fired` set.** When an effect runs, it's added to the chain's `Fired`. If the same effect is scheduled again within the chain, `RunEffect` short-circuits (no infinite loops).

**D8 — Two execution verbs.**
- `IEffectPipeline.RunEffect(EffectInstance, IEffectScope? parentChain)` — scheduling decision (inline vs deferred). Used by handlers (via `ctx.RunEffect`), the trigger fold, and effects (via `scope.RunEffect`).
- `EffectInstance.Execute(scope)` extension — direct execution that bypasses scheduling. Used by resolution handlers (e.g., `ResolveTopOfStackHandler` pops the stack and calls `Execute` directly to avoid pushing the popped instance back via the MTG pipeline's scheduling logic).

**D9 — Triggers compile into effects.** `Trigger<T>` stays as authoring sugar with a typed payload. The fold wraps a matched trigger as an `EffectInstance` (intercepted command captured in `Bindings`) and calls `IEffectPipeline.RunEffect`. From that point forward, the path is identical to active effects.

**D10 — `ctx.CurrentChain` is NOT exposed publicly.** The chain scope is bound implicitly when the handler context is constructed (drain → null; `scope.Dispatch` from inside an effect → the calling chain). `ctx.RunEffect(instance)` uses the bound chain internally; handlers don't need to thread it through.

**D11 — Handler envelope IS the atomicity boundary.** Every `Store.Execute(payload)` inside a single handler is buffered; subscribers receive one logical step when the envelope closes. The mutator runs eagerly (state changes computed in order; later payloads see earlier ones in the buffer), but external visibility is deferred to envelope close. Handler-throws roll the buffer back. `Store.ExecuteBatch(...)` becomes a niche tool for cross-handler grouping (rare; `scope.Dispatch` already provides that). Implication: a before-fold trigger reading state mid-handler sees pre-handler state — correct semantic, worth pinning.

**D12 — Validation lives in optional `IValidator<TCmd>`s, not on the handler.** `ICommandHandler<TCmd, TResult>` exposes only `Handle`. Commands with preconditions register one or more `IValidator<TCmd>` services; the pipeline runs them in registration order before `Handle`, fail-fast on the first false. Commands with no preconditions register none and skip the step. Snapshot legality (`IAvailableActions`) consumes the same validators. Cascade impact: drops `Validate` from `ICommandHandler`, rewires `actions/01`'s pipeline contract, and rewires `actions/02`'s snapshot legality. Validator ordering / composition rules deferred (see follow-ups).

## Type hierarchy

```csharp
// ─────────────────────────────────────────────────────────────
// Authoring layer — lives on definitions / per app
// ─────────────────────────────────────────────────────────────

// The recipe. Authored inline with lambdas, or subclassed for typed convenience.
public class EffectDefinition
{
    public EffectId Id { get; }
    public Func<EffectInstance, IEffectScope, bool>  Validate { get; }
    public Func<EffectInstance, IEffectScope, Task>  Execute  { get; }

    public EffectDefinition(
        EffectId id,
        Func<EffectInstance, IEffectScope, Task> execute,
        Func<EffectInstance, IEffectScope, bool>? validate = null)
    {
        Id        = id;
        Execute   = execute;
        Validate  = validate ?? ((_, _) => true);
    }
}

// Triggers — authoring sugar over EffectDefinition with a typed payload.
// Receives `self` (the wrapped EffectInstance carrying registration metadata —
// Source = registration owner, Bindings = bound parameters) plus the typed
// command. The fold matches a trigger to an incoming GameCommand, wraps it as
// an EffectInstance, and invokes Execute via the same pipeline path as active
// effects. The wrap mechanism is runtime-internal (gap #6).
public abstract class Trigger<T> where T : GameCommand
{
    public EffectId Id { get; }
    protected Trigger(EffectId id) => Id = id;

    public virtual bool Validate(EffectInstance self, T command, IEffectScope scope) => true;
    public abstract Task Execute(EffectInstance self, T command, IEffectScope scope);
}

// ─────────────────────────────────────────────────────────────
// Wire form — records on state / pipeline / wire transport
// ─────────────────────────────────────────────────────────────

public sealed record EffectInstance(
    Reference      Source,        // ref to the source entity (card, ability holder)
    EffectId       Id,             // which of the source's effects (sources have many)
    EffectBindings Bindings);      // bound at fire time

public sealed record EffectBindings(
    IReadOnlyList<Reference>? Targets            = null,
    int?                      XCost              = null,
    string?                   Mode               = null,
    GameCommand?              InterceptedCommand = null);   // populated for triggered instances

// ─────────────────────────────────────────────────────────────
// Resolution helper — direct execution (D8 second verb)
// ─────────────────────────────────────────────────────────────

public static class EffectInstanceExtensions
{
    public static Task Execute(this EffectInstance instance, IEffectScope scope)
    {
        // scope.State.Resolve(reference) is assumed to provide catalog lookup —
        // IStateScope is the read-only surface (state slices + handle resolve).
        var source = scope.State.Resolve(instance.Source);
        var def    = source.Definition.GetEffect(instance.Id);

        return def.Validate(instance, scope)
            ? def.Execute(instance, scope)
            : Task.CompletedTask;
    }
}
```

## Service surfaces

```csharp
// ─────────────────────────────────────────────────────────────
// The two pipelines, symmetric — both injected via DI
// ─────────────────────────────────────────────────────────────

public interface ICommandPipeline
{
    bool Validate(GameCommand command, IStateScope state, out string? reason);

    // chainScope: null at drain (top-level commands);
    //             non-null when called from scope.Dispatch inside an effect.
    Task<CommandResult> Execute(GameAction action, IEffectScope? chainScope);
}

public interface IEffectPipeline
{
    // The single scheduling-decision entry point.
    // parentChain: null = start a fresh chain; non-null = extend existing.
    Task RunEffect(EffectInstance instance, IEffectScope? parentChain);
}

// ─────────────────────────────────────────────────────────────
// Framework default — runs inline (Stacklands semantics)
// ─────────────────────────────────────────────────────────────

public sealed class InlineEffectPipeline : IEffectPipeline
{
    private readonly IEffectScopeFactory _scopes;

    public async Task RunEffect(EffectInstance instance, IEffectScope? parent)
    {
        // Re-entrance guard within an existing chain.
        if (parent is not null && parent.Chain.Fired.Contains(instance))
            return;

        var chain = parent is null
            ? _scopes.NewRoot(instance)             // fresh chain rooted at instance
            : _scopes.Extend(parent, instance);     // extend the existing chain's Fired set

        await instance.Execute(chain);
    }
}

// ─────────────────────────────────────────────────────────────
// Handler context — sync, atomic, may mutate. Chain bound implicitly.
// Surface trimmed to common base case (Q2 rule); rare-use services
// (IModifierFactory, IAvailableActions, ICommandAwaiter) → DI.
// ─────────────────────────────────────────────────────────────

public interface IHandlerContext
{
    Store Store { get; }                            // slice reads + mutator dispatch (Store.Execute)
    void  Emit(GameSignal signal);                  // explicit-meaning history channel

    // Sugar over IEffectPipeline.RunEffect(instance, currentChain).
    // currentChain is bound when the context is constructed:
    //   - null when the handler runs a drain-initiated command
    //   - the calling chain when the handler runs inside scope.Dispatch
    // Placement on the handler (vs forcing a wrapping effect) is pending — see concern #2.
    Task RunEffect(EffectInstance instance);
}

// ─────────────────────────────────────────────────────────────
// The chain scope — what an effect's Execute receives.
// Async, orchestrates, never mutates directly (mutation discipline:
// only handlers invoke mutators; effects receive IStateScope, not Store).
// ─────────────────────────────────────────────────────────────

public interface IEffectScope
{
    IStateScope State { get; }                      // slice reads + catalog resolve

    // Inline dispatch — runs the action's full pipeline NOW; this scope is passed through.
    Task<TResult> Dispatch<TResult>(GameAction action) where TResult : CommandResult;

    // Await a player/system decision (target picker, choice, mode, etc.).
    // Renamed from `Resolver`/`ICommandAwaiter` so the call site reads as the intent.
    Task<TResult> Decide<TResult>(/* decision request shape, TBD */) where TResult : DecisionResult;

    // Schedule a sub-effect via the pipeline. Pipeline decides inline vs deferred.
    Task RunEffect(EffectInstance instance);

    // Chain metadata — root + re-entrance set. Tucked away so the top-level surface stays small.
    IChainInfo Chain { get; }

    // Before-fold trigger primitives. Runtime-checked phase: throws when invoked outside
    // before-fold. Deliberate runtime-check (vs type-narrowed subtype) — revisit if
    // false-positive misuse becomes a real bug source.
    void Cancel(string? reason = null);
    void Replace(GameAction next);
}

// Chain-info sub-property — kept off the top-level scope surface.
public interface IChainInfo
{
    EffectInstance?              Origin { get; }    // root of this chain (debug + game-author tracing)
    IReadOnlySet<EffectInstance> Fired  { get; }    // re-entrance guard
}
```

## The flow

```
External entry (gateway / timer / re-entry):
  engine.Dispatch(action, out reason)
    │
    ├─ lifecycle gate (NotStarted/Ended)
    ├─ pipeline.Validate
    └─ pending.Enqueue(action)              // engine-private FIFO

Drain:
  while (queue not empty):
    var action = pending.Dequeue()
    var result = pipeline.Execute(action, chainScope: null)   // ← null at top-level
                                       │
                                       ├─ open envelope                   // begins atomicity scope (D11)
                                       ├─ before-fold:
                                       │    foreach trigger in available:
                                       │      var triggerInstance = WrapAsInstance(trigger, action)
                                       │      effectPipeline.RunEffect(triggerInstance, null)
                                       │        ↳ default: starts fresh chain → trigger.Execute
                                       │        ↳ MTG: pushes onto stack zone (state)
                                       ├─ handler.Handle(action, ctx where ctx.RunEffect uses chain=null)
                                       │    handler may: ctx.RunEffect(activeInstance)
                                       ├─ after-fold: same shape as before
                                       └─ close envelope                  // commit buffered payloads
    var followUps = runtime.Evaluate(action, result)
    foreach (f in followUps) pending.Enqueue(f)

Inside an effect's Execute (called from a chain):
  scope.Dispatch(subAction)
    └─ pipeline.Execute(subAction, chainScope: this)   // ← chain passed through
        ├─ before-fold (uses this chain — fold's RunEffect calls extend Fired)
        ├─ handler.Handle(subAction, ctx where ctx.RunEffect uses chain=this)
        ├─ after-fold (uses this chain)
        └─ close envelope

  scope.RunEffect(instance)
    └─ effectPipeline.RunEffect(instance, parentChain: this)
        ↳ default: extends this chain (adds instance to Fired) → instance.Execute(extendedChain)
        ↳ MTG: pushes onto state (this chain not extended; future drain step resolves separately)
```

## Validation surface (proposed)

Per D12: validation moves off the handler into separate `IValidator<TCmd>` services. The handler interface drops to a single method.

```csharp
// Updated handler interface — Handle only.
public interface ICommandHandler<TCmd, TResult>
    where TCmd   : GameAction
    where TResult: CommandResult
{
    Task<TResult> Handle(TCmd cmd, IHandlerContext ctx);
}

// New validator interface — optional, registered per command type.
public interface IValidator<TCmd> where TCmd : GameAction
{
    bool Validate(TCmd cmd, IStateScope state, out string? reason);
}
```

Pipeline integration (sketch):

```csharp
public bool Validate(GameCommand command, IStateScope state, out string? reason)
{
    reason = null;
    if (command is not GameAction action) return true;

    // Open-generic resolve all IValidator<TCmd> registered for the command's runtime type.
    foreach (var validator in services.ResolveValidators(action.GetType()))
        if (!validator.Validate(action, state, out reason))
            return false;

    return true;
}
```

Sample validators replacing the inline `Validate` from earlier handler sketches:

```csharp
public sealed class MoveCardSourceMembershipValidator : IValidator<MoveCardAction>
{
    public bool Validate(MoveCardAction cmd, IStateScope state, out string? reason)
    {
        if (!cmd.From.GetContents(state).Contains(cmd.Card.GetRef(state)))
        { reason = "card not in source zone"; return false; }
        reason = null;
        return true;
    }
}

public sealed class DealDamageNonNegativeValidator : IValidator<DealDamageToPlayerAction>
{
    public bool Validate(DealDamageToPlayerAction cmd, IStateScope state, out string? reason)
    {
        if (cmd.Amount < 0) { reason = "amount cannot be negative"; return false; }
        reason = null;
        return true;
    }
}
```

`ApplyBuffAction` registers no validator — always-legal at dispatch.

## Framework conveniences (assumed by samples)

- **Implicit `EntityInstance<TDef>` → `Ref<TDef>` conversion.** Payloads/actions accepting a `Ref<T>` can take a handle directly:
  ```csharp
  public static implicit operator Ref<TDef>(EntityInstance<TDef> instance) => instance.SelfRef;
  ```
  Samples below pass handles where refs are expected (`new SetBaseValuePayload(player, ...)` rather than `(player.SelfRef, ...)`).
- **Trigger registration** (per-instance via authoring layer) is out of scope here — see `oq-effect-authoring` and gap #6. Samples show the authored `Trigger<T>` shape; the wrap-to-`EffectInstance` step is runtime-internal.

## Sample commands

Three handler shapes covering the common base cases. Listed before effects because effects compose over commands.

### `MoveCardAction` — multiple slice payloads, atomic via envelope

Handler dispatches the slice-level payloads itself (instead of wrapping them in a `CompositeAction` like `MoveCardMutation`). Per D11, the handler envelope is the atomicity boundary — payloads buffer and commit on envelope close, so subscribers see one logical step without an explicit batch.

```csharp
public sealed record MoveCardAction(
    Card Card,
    Zone From,
    Zone To,
    InsertPosition? Position = null) : GameAction;

public sealed record MoveCardResult() : CommandResult;

public sealed class MoveCardHandler : ICommandHandler<MoveCardAction, MoveCardResult>
{
    // Preconditions live in MoveCardSourceMembershipValidator (per D12).
    public Task<MoveCardResult> Handle(MoveCardAction cmd, IHandlerContext ctx)
    {
        // Three slice mutators dispatched in order. The handler envelope (D11)
        // buffers them; subscribers see one logical step on envelope close.
        //   1. Remove old CardRef from source zone (ZoneContentsState).
        //   2. Rotate the CardRef in CardMapState (privacy — every move rotates).
        //   3. Insert new CardRef into destination zone (ZoneContentsState).
        var oldRef = cmd.Card.GetRef(ctx.Store);
        var newRef = CardRef.NewRef();

        ctx.Store.Execute(new RemoveCardFromZonePayload(cmd.From, oldRef));
        ctx.Store.Execute(new RotateCardRefPayload(cmd.Card, newRef));
        ctx.Store.Execute(new InsertCardPayload(cmd.To, cmd.Card, cmd.Position ?? new DefaultPosition()));

        return Task.FromResult(new MoveCardResult());
    }
}
```

### `DealDamageToPlayerAction` — read + mutate + emit

The representative shape: read state, dispatch a payload, emit a `GameSignal` so triggers can react.

```csharp
public sealed record DealDamageToPlayerAction(
    Player Target,
    int Amount,
    Reference? Source = null) : GameAction;

public sealed record DealDamageResult(int Dealt) : CommandResult;

public sealed class DealDamageToPlayerHandler
    : ICommandHandler<DealDamageToPlayerAction, DealDamageResult>
{
    // Preconditions live in DealDamageNonNegativeValidator (per D12).
    public Task<DealDamageResult> Handle(DealDamageToPlayerAction cmd, IHandlerContext ctx)
    {
        // Domain payload — mutator handles the variable bookkeeping (whether
        // health is base value, a modifier stack, or a damage counter is the
        // game's choice; the handler doesn't care).
        ctx.Store.Execute(new ApplyDamagePayload(cmd.Target, cmd.Amount));
        ctx.Emit(new DamageDealtSignal(cmd.Target, cmd.Amount, cmd.Source));

        return Task.FromResult(new DealDamageResult(Dealt: cmd.Amount));
    }
}
```

### `ApplyBuffAction` — DI-injected service usage

`IModifierFactory` is no longer on `IHandlerContext` (per Q2 — not a base-case service). Handlers that need it inject it. Same pattern for `ICommandAwaiter` if a sync handler unusually needed one (it shouldn't, but the pattern is the same).

```csharp
public sealed record ApplyBuffAction(
    EntityInstance<EntityDefinition> Target,
    Variable Key,
    int Amount,
    Reference Source) : GameAction;

public sealed record ApplyBuffResult(ModifierId Id) : CommandResult;

public sealed class ApplyBuffHandler : ICommandHandler<ApplyBuffAction, ApplyBuffResult>
{
    private readonly IModifierFactory _modifiers;
    public ApplyBuffHandler(IModifierFactory modifiers) => _modifiers = modifiers;

    // No validator registered for ApplyBuffAction — always-legal at dispatch (per D12).
    public Task<ApplyBuffResult> Handle(ApplyBuffAction cmd, IHandlerContext ctx)
    {
        var entry = _modifiers.Create(
            cmd.Key,
            new IntAddModifier { Amount = cmd.Amount },
            new ModifierSource(cmd.Source, Order: 0));

        ctx.Store.Execute(new AddModifierPayload(cmd.Target, entry));

        return Task.FromResult(new ApplyBuffResult(entry.Id));
    }
}
```

## Sample effects

> **Authoring boundary.** Each card's `Effects` list is treated as *already populated* by the time the runtime reads it. *How* it gets populated — code-side registration, serialized asset import, definition editor — is the authoring layer's job (`oq-effect-authoring`) and out of scope here. This spike pins the *shape* of an `EffectDefinition`; not how the catalog gets filled.

Five shapes covering the trigger-and-effect verb space: active-with-target, active-no-target, before-fold `Replace`, after-fold `Dispatch`, before-fold `Cancel`.

Effects are authored as subclasses of `EffectDefinition` with lambdas baked into the constructor — readable, reusable, addressable by type.

### Lightning Bolt — active effect with target

```csharp
public sealed class LightningBoltDamageEffect : EffectDefinition
{
    public LightningBoltDamageEffect()
        : base(
            id: EffectIds.LightningBoltDamage,
            execute: async (instance, scope) =>
            {
                var target = scope.State.Resolve<Player>(instance.Bindings.Targets![0]);
                await scope.Dispatch<DealDamageResult>(
                    new DealDamageToPlayerAction(target, Amount: 3, Source: instance.Source));
            })
        // Validate omitted — defaults to "always valid"
    { }
}

// Card definition — `Effects` is treated as already populated. The literal
// initializer is illustrative only; the spike makes no claim about who/how
// filled the list (authoring concern).
public sealed class LightningBoltCardDefinition : CardDefinition
{
    public override IReadOnlyList<EffectDefinition> Effects { get; }
        = new EffectDefinition[] { new LightningBoltDamageEffect() };
}
```

### Draw a card — active effect, no target

Floor case: a single `Dispatch`, no decision, no target binding. Source is the player drawing.

```csharp
public sealed class DrawCardEffect : EffectDefinition
{
    public DrawCardEffect()
        : base(
            id: EffectIds.DrawCard,
            execute: async (instance, scope) =>
            {
                var owner = scope.State.Resolve<Player>(instance.Source);
                var deck  = owner.Deck;     // declared on PlayerDefinition.PerPlayerZones
                var hand  = owner.Hand;
                var top   = deck.GetTopCard(scope.State);

                if (top is null) return;    // empty deck — game-side rule decides what happens

                await scope.Dispatch<MoveCardResult>(
                    new MoveCardAction(top, From: deck, To: hand, Position: Position.AtTop));
            })
    { }
}
```

### Bloodlust — before-fold trigger using `Replace`

A passive: the controller's outgoing damage is +1. Authored as a `Trigger<T>` over `DealDamageToPlayerAction`; registered (per-instance) as before-fold on the controller's actor.

```csharp
public sealed class IncreaseOutgoingDamageTrigger : Trigger<DealDamageToPlayerAction>
{
    public IncreaseOutgoingDamageTrigger() : base(EffectIds.IncreaseOutgoingDamage) { }

    public override Task Execute(
        EffectInstance self,
        DealDamageToPlayerAction command,
        IEffectScope scope)
    {
        // self.Source is the registration owner — typed payload, no cast needed.
        if (command.Source != self.Source) return Task.CompletedTask;

        scope.Replace(command with { Amount = command.Amount + 1 });   // ← Replace verb (before-fold only)
        return Task.CompletedTask;
    }
}
```

### Vampiric — after-fold trigger using `Dispatch`

When the controller deals damage, heal them by half (round down). After-fold: damage already applied, this is a reaction.

```csharp
public sealed class HealOwnerOnDamageTrigger : Trigger<DealDamageToPlayerAction>
{
    public HealOwnerOnDamageTrigger() : base(EffectIds.HealOwnerOnDamage) { }

    public override async Task Execute(
        EffectInstance self,
        DealDamageToPlayerAction command,
        IEffectScope scope)
    {
        if (command.Source != self.Source || command.Amount <= 0) return;

        // HealActorAction handler is symmetric to DealDamageToPlayerHandler —
        // dispatches an ApplyHealPayload, emits HealedSignal.
        var owner = scope.State.Resolve<Player>(self.Source);
        await scope.Dispatch<HealActorResult>(
            new HealActorAction(owner, command.Amount / 2));
    }
}
```

### Lockdown — before-fold trigger using `Cancel`

A passive that silences a marked actor: any cast attempt by them is cancelled. The silenced actor is bound at registration time (carried in `self.Bindings.Targets`).

```csharp
public sealed class SilenceActorTrigger : Trigger<CastSpellAction>
{
    public SilenceActorTrigger() : base(EffectIds.SilenceActor) { }

    public override Task Execute(
        EffectInstance self,
        CastSpellAction command,
        IEffectScope scope)
    {
        var silenced = (Ref<Player>)self.Bindings.Targets![0];
        if (command.Caster.SelfRef != silenced) return Task.CompletedTask;

        scope.Cancel(reason: "silenced");                          // ← Cancel verb (before-fold only)
        return Task.CompletedTask;
    }
}
```

### Coverage map

| Shape | Authored as | Sample |
|---|---|---|
| Active effect, with target, single Dispatch | `EffectDefinition` | Lightning Bolt |
| Active effect, no target | `EffectDefinition` | Draw a Card |
| Before-fold trigger, `Replace` verb | `Trigger<DealDamageToPlayerAction>` | Bloodlust (+1 damage) |
| After-fold trigger, `Dispatch` reaction | `Trigger<DealDamageToPlayerAction>` | Vampiric (heal-on-damage) |
| Before-fold trigger, `Cancel` verb | `Trigger<CastSpellAction>` | Lockdown (silence) |

What's still uncovered: `scope.Decide` (target picker / choice prompt) and `scope.RunEffect` (sub-effect scheduling from inside a chain). Surface for both. Defer until those design questions settle (`Decide` shape is TBD; `RunEffect` placement depends on concern #2).

A handler that *casts* the spell builds the `EffectInstance` with bound parameters and asks the pipeline to schedule it:

```csharp
public sealed class CastSpellHandler : ICommandHandler<CastSpellCommand, CastSpellResult>
{
    // Mana-cost / target-legality / timing checks live in IValidator<CastSpellCommand>s (per D12).
    public async Task<CastSpellResult> Handle(CastSpellCommand cmd, IHandlerContext ctx)
    {
        // 1) Pay costs and move card to game-state stack zone.
        ctx.Store.Execute(new PayManaPayload(cmd.Caster, cmd.ManaCost));
        ctx.Store.Execute(new MoveCardToZonePayload(cmd.Spell, fromZone: hand, toZone: stack));

        // 2) Build the EffectInstance with bound targets.
        var instance = new EffectInstance(
            Source:   cmd.Spell,
            Id:       EffectIds.LightningBoltDamage,
            Bindings: new EffectBindings(Targets: new[] { cmd.Target }));

        // 3) Schedule it. Pipeline decides what happens next.
        //    Stacklands: runs inline (effect's Execute fires here).
        //    MTG: pushes onto stack state; resolves later via runtime.Evaluate.
        await ctx.RunEffect(instance);

        return new CastSpellResult();
    }
}
```

## Concrete game implementations

### Stacklands — defaults work

No services to swap; framework's `InlineEffectPipeline` is the registered `IEffectPipeline`. Triggers and active effects run inline.

```csharp
public sealed class StacklandsRulesRuntime : GameRulesRuntime<StacklandsRulesDefinition>
{
    // No FilterTriggers override (default identity).
    public override IReadOnlyList<GameAction> Evaluate(GameAction _, CommandResult __)
    {
        var followUps = ComputeStateBasedFollowUps();
        if (followUps.Any()) return followUps;

        // No follow-ups — drain about to end. Publish snapshot.
        Actions.Set(BuildSnapshot());
        return Array.Empty<GameAction>();
    }
}

// Installer:
//   Framework EngineInstaller registers InlineEffectPipeline as IEffectPipeline.
//   No game override needed.
```

### MTG-style — stack zone via service-side scheduling

State holds `EffectInstance` records on a slice. The MTG `IEffectPipeline` pushes onto state instead of running inline. The runtime's `Evaluate` watches state and dispatches resolution actions. Resolution handlers pop state and run via direct `Execute`.

```csharp
// ─── State ───
public sealed record StackContentsState(
    ImmutableList<EffectInstance> Items)
    : BaseState
{
    public EffectInstance? Top    => Items.IsEmpty ? null : Items[^1];
    public bool            IsEmpty => Items.IsEmpty;
}

public sealed record PushToStackPayload(EffectInstance Instance);
public sealed record PopFromStackPayload();

public sealed class PushToStackMutator : Mutator<StackContentsState, PushToStackPayload>
{
    public override StackContentsState Change(
        StackContentsState s, PushToStackPayload p, IStateScope _)
        => new(s.Items.Add(p.Instance));
}

public sealed class PopFromStackMutator : Mutator<StackContentsState, PopFromStackPayload>
{
    public override StackContentsState Change(
        StackContentsState s, PopFromStackPayload _, IStateScope __)
        => new(s.Items.RemoveAt(s.Items.Count - 1));
}

// ─── MTG Effect pipeline — push onto state ───
public sealed class MtgStackEffectPipeline : IEffectPipeline
{
    private readonly Store _store;

    public Task RunEffect(EffectInstance instance, IEffectScope? parent)
    {
        // Re-entrance guard still applies to the calling chain.
        if (parent is not null && parent.Chain.Fired.Contains(instance))
            return Task.CompletedTask;

        _store.Execute(new PushToStackPayload(instance));
        return Task.CompletedTask;   // chain doesn't extend; future drain step resolves
    }
}

// ─── MTG Runtime — Evaluate drives stack resolution ───
public sealed class MtgRulesRuntime : GameRulesRuntime<MtgRulesDefinition>
{
    public override IReadOnlyList<GameAction> Evaluate(GameAction _, CommandResult __)
    {
        var stack    = State.Get<StackContentsState>();
        var priority = State.Get<PriorityState>();

        if (stack.IsEmpty)
        {
            Actions.Set(BuildSnapshot());
            return Array.Empty<GameAction>();
        }

        if (priority.BothPassed)
            return new[] { new ResolveTopOfStackAction() };

        if (!priority.Granted)
            return new[] { new GrantPriorityAction(priority.NextToAct) };

        return Array.Empty<GameAction>();   // priority outstanding; await input
    }
}

// ─── Resolution handler — pops state, runs via direct Execute ───
public sealed class ResolveTopOfStackHandler
    : ICommandHandler<ResolveTopOfStackAction, ResolveTopOfStackResult>
{
    private readonly IEffectScopeFactory _scopes;

    // Stack-not-empty precondition lives in IValidator<ResolveTopOfStackAction> (per D12).
    public async Task<ResolveTopOfStackResult> Handle(
        ResolveTopOfStackAction _, IHandlerContext ctx)
    {
        var stack = ctx.Store.Get<StackContentsState>();
        var top   = stack.Top!;
        ctx.Store.Execute(new PopFromStackPayload());

        // Build a fresh chain rooted at `top`, then call Execute DIRECTLY.
        // Going through ctx.RunEffect would route via MtgStackEffectPipeline,
        // which would push `top` back onto the stack — infinite loop.
        var chain = _scopes.NewRoot(top);
        await top.Execute(chain);

        return new ResolveTopOfStackResult();
    }
}

// Installer:
//   builder.Register<IEffectPipeline, MtgStackEffectPipeline>(Lifetime.Singleton);
//   (replaces framework's InlineEffectPipeline)
```

### Hearthstone-like — service-side trigger queue

Same shape, different state slice and different scheduling logic. Effects nest mid-execution while the queue is "actively resolving"; otherwise inline.

```csharp
public sealed class HearthstoneTriggerQueueEffectPipeline : IEffectPipeline
{
    private readonly Store _store;

    public async Task RunEffect(EffectInstance instance, IEffectScope? parent)
    {
        if (parent is not null && parent.Chain.Fired.Contains(instance))
            return;

        var queue = _store.Get<TriggerQueueState>();
        if (queue.IsResolving)
        {
            _store.Execute(new EnqueueTriggerPayload(instance));
            return;
        }

        // Idle: run inline.
        var chain = parent is null ? _scopes.NewRoot(instance) : _scopes.Extend(parent, instance);
        await instance.Execute(chain);
    }
}
```

## Traced flow — MTG cast Lightning Bolt with reactive trigger

```
─── Drain step 1: cast ───
host.Dispatch(CastSpellCommand { spell: lightning_bolt, target: enemy }, out _)

  pipeline.Execute(cast, chainScope=null)
    handler:
      • PayManaPayload                                    [state mutation]
      • MoveCardToZonePayload → stack zone               [state mutation; UI shows card on stack]
      • build EffectInstance(source=lb, id=Damage, bindings.targets=[enemy])
      • ctx.RunEffect(instance)
          → effectPipeline.RunEffect(instance, parent=null)
          → MtgStackEffectPipeline pushes onto StackContentsState
          → returns immediately (no chain extended)
    after-fold: no triggers
  runtime.Evaluate → stack non-empty, priority not granted
                  → [GrantPriorityAction(active)]

─── Drain step 2 (and onward): priority resolves ───
... players pass priority over multiple drain steps ...

─── Drain step N: resolution ───
runtime.Evaluate → stack non-empty, priority both passed
                → [ResolveTopOfStackAction]

  pipeline.Execute(resolve, chainScope=null)
    handler:
      top = StackContentsState.Top  (LightningBoltInstance)
      Store.Execute(PopFromStackPayload)
      var chain = _scopes.NewRoot(top)             // Origin: top, Fired: {top}
      await top.Execute(chain)
        ↳ LightningBoltDef.Execute(top, chain)
            ↳ chain.Dispatch(DealDamageAction { target=enemy, amount=3 })
                ↳ pipeline.Execute(damage, chainScope=chain)
                    handler: damage applied (state mutation)
                    after-fold:
                      Acolyte's "when damaged → draw" trigger fires
                      var triggerInstance = WrapAsInstance(acolyteTrigger, damage)
                      effectPipeline.RunEffect(triggerInstance, parent=chain)
                        → MtgStackEffectPipeline pushes onto StackContentsState
                        → returns
                      envelope closes
                returns
            returns (LightningBolt's effect done)
      handler returns
  runtime.Evaluate → stack non-empty (newly-added trigger), priority resets
                  → [GrantPriorityAction(...)]

─── Future drain steps: resolve the triggered ability ───
... priority round again, eventually ResolveTopOfStackAction pops the trigger,
    builds a fresh chain rooted at the trigger, runs Execute,
    which dispatches DrawCardAction, etc.
```

Two distinct chains across the trace:

1. **chain-1** — rooted at `LightningBoltInstance`. Lives across `top.Execute` and the nested `Dispatch` and the fold's `RunEffect` call. Re-entrance guard: if `LightningBoltInstance` somehow triggered again in this chain, it'd be blocked.
2. **chain-2** — rooted at the Acolyte trigger when *its* turn to resolve comes (separate drain step). Distinct chain, distinct `Fired` set.

The MTG stack itself is a state slice. The runtime watches it via `Evaluate`. The `IEffectPipeline` service decides what `RunEffect` does. Symmetric, snapshot-friendly, no hidden runtime state.

## Where this leaves the surfaces

```csharp
// Runtime — stays at two methods (per ADR-0006).
public abstract class GameRulesRuntime<TDefinition>
{
    public virtual IReadOnlyList<TriggerMatch> FilterTriggers(...)        => candidates;
    public virtual IReadOnlyList<GameAction>   Evaluate(action, result)   => Empty;
}

// Two services, both injected via DI, swappable per game:
//   - ICommandPipeline (framework-shipped; not normally swapped)
//   - IEffectPipeline  (framework default = inline; games swap for stack/queue)

// One framework helper:
//   - EffectInstance.Execute(scope) extension — direct execution from resolution handlers.
```

## Open follow-ups (NOT settled here)

- **`oq-effect-authoring`** — the recipe inside `EffectDefinition.Execute` is a delegate, but the *authoring vocabulary* is unsettled: closed-set primitives ("deal damage / draw N / apply modifier") composed into effects, free-form code, graph, hybrid? Authoring feature scope.
- **`oq-runtime-as-state-evaluator`** — long-term direction. After this sketch lands, `FilterTriggers` is the next candidate to revisit. Runtime might collapse further toward pure state evaluation + state-machine transitions.
- **`oq-session-state-machine`** — richer engine lifecycle (Connecting / Started / Running / Reconnecting / Ended / ShowingResults) on top of the current 3-state `EngineLifecycle`.
- **`EffectId` shape** — pin during authoring design; for now treat as opaque identifier.
- **Trigger binding shape** — `Bindings.InterceptedCommand: GameCommand?` is the simplest carrier for the typed payload that a `Trigger<T>` was originally parameterized on. Whether bindings should also carry richer trigger context (Self ref, registration owner, fold phase, timestamp) is unsettled.
- **`EffectChainScope` factory shape** — `_scopes.NewRoot(instance)` / `_scopes.Extend(parent, instance)` is one shape. Could also be static methods on `EffectChainScope` itself. Mechanical detail; pin during implementation.
- **`EffectDefinition.Validate`** — semantics under re-entrance and cancellation. Probably "Validate=false → skip silently"; aligns with `Trigger<T>.Validate`.
- **Whether `Trigger<T>` survives** as a separate authoring type, or collapses entirely into `EffectDefinition` with `InterceptedCommand` in bindings. Probably keeps as authoring sugar — typed payload is ergonomic for game authors.
- **Validator ordering / composition.** D12 says "registration order, fail-fast on first false." Open: explicit ordering hooks (priority weight, named groups), how a snapshot-side aggregate compose `reason` strings across multiple failures, sync-vs-async (probably sync), and whether validators can mutate or only read.

## Known content gaps in this sketch

Surfaced in the post-write review (2026-05-07). These are *exposition* gaps — places where the sketch is unclear, incomplete, or makes forward references to unwritten helpers — that need to close before this can graduate to Models. Distinct from the genuine open design questions in "Open follow-ups" above.

**Self-containedness gaps:**

1. **Forward references to undefined helpers.**
   - `IEffectScopeFactory` — used in `InlineEffectPipeline` (`_scopes.NewRoot`, `_scopes.Extend`) and `ResolveTopOfStackHandler`, but the type is never declared. Either define it or fold its job onto `IEffectPipeline` directly.
   - `WrapAsInstance(trigger, command)` — referenced in the flow diagram and trace, only described in prose. No code sketch of the central trigger-to-EffectInstance wiring.
   - `BuildSnapshot()`, `ComputeStateBasedFollowUps()` — referenced in runtime examples without flagging them as game-side placeholders.

2. **Types referenced but not introduced.**
   - `EffectId` — used everywhere; semantics never pinned (string? enum? typed identifier?). Listed in "Open follow-ups" but the reader hits it cold.
   - `BaseState`, `Mutator<,>`, `Store`, `IStateScope`, `IModifierFactory`, `ICommandAwaiter`, `IAvailableActions`, `Reference` — borrowed from existing framework. A "Prerequisites / referenced framework types" section near the top would close the gap.
   - `PriorityState.BothPassed` / `Granted` / `NextToAct` — game-side slice; assumed shape never declared. Worth a "(example slice; game-defined)" note inline.

3. **ADR-0006 referenced but not summarized inline.** Context section says "per ADR-0006" without inline-summarizing the three-level model + pipeline split. One paragraph in the Context section would make the spike actually standalone.

**Clarity gaps:**

4. ~~**`Origin` field semantics opaque.**~~ *Resolved.* `Origin` and `Fired` moved to `IEffectScope.Chain` (sub-property). `Chain.Origin` is documented as debug + game-author tracing; `Chain.Fired` is the re-entrance guard the runtime consults.

5. **"Why two verbs" rationale buried.** D8 explains the mechanism but the *reason* (resolution-handler-via-RunEffect would re-push onto the MTG stack and loop) lives 200 lines later in the resolution-handler comment. Pull that "why" up to D8.

6. **Trigger-as-EffectInstance is hand-waved.** The single most subtle bit of the design — *how a `Trigger<T>` registration becomes an `EffectInstance` with a stable `EffectId` and a captured intercepted command* — gets one comment in the type hierarchy and a `WrapAsInstance(...)` placeholder. Needs at least a sketch of:
   - How `AbilityDefinition.Register` produces an `EffectDefinition` on the owner.
   - Where the `EffectId` for a registered trigger comes from.
   - How the fold wraps a matched trigger as `EffectInstance` (Source = trigger owner, Id = generated, Bindings.InterceptedCommand = matched command).
   - How `ResolveTopOfStackHandler.top.Execute(chain)` knows to pull the trigger logic out of the source's definition.

7. **Stacklands section too thin.** Just "no override needed" — needs a simple inline trigger example (e.g., "when this card enters play, deal 1 damage to neighbor") to illustrate the default path with lambda authoring and balance against MTG/Hearthstone weight.

8. **Naming inconsistency.** "chain scope" (informal) / `IEffectScope` (interface) / `EffectChainScope` (implementation never sketched). Pick one and use consistently; sketch `EffectChainScope` if it's load-bearing.

**Smaller polish:**

9. **`StacklandsRulesDefinition` / `MtgRulesDefinition` / `HearthstoneRulesDefinition`** — extended in runtime examples but never sketched. Either show one (canonical authoring shape) or note "definition class is per-game data; out of scope here."

10. **Trace is long.** A higher-level "what changed at each step" summary above the detailed MTG trace would help skimming.

11. **D2's "existing record-on-slice pattern"** — referenced without explanation; readers without `effects/04` open won't know what's meant.

---

## What this sketch does NOT settle

Out of scope for this iteration:

- **Effect authoring / registration.** How `EffectDefinition`s get authored, registered, serialized, or attached to a card's `Effects` list. The spike pins the *shape* of `EffectDefinition`; how the list gets populated lives in the authoring layer (`oq-effect-authoring`).
- The exact API surface for constructing `EffectChainScope` instances.
- How `IEffectScope.Dispatch` interacts with cancellation propagation through the chain.
- Effect persistence across saves (snapshot-mid-stack) — relies on `EffectInstance` being serializable, which the record shape implies but hasn't been stress-tested.
- Multi-actor decision interleaving inside effects (e.g., MTG split-second).
- Replacement effects involving multiple competing replacers — handled by the existing trigger-fold ordering (per `effects/02-replacement-prevention.md`); this sketch doesn't add new constraints there.

## When this lands

(Reference for future model conversion — not action-now.)

When iteration settles, propagate to canonical Models:

- **New ADR:** `Product/decisions/0007-effect-pipeline-shape.md` — locks the architecture.
- `Product/features/effects/effects.md` — substantial revision (new types, services, chain semantics, two execution verbs).
- `Product/features/effects/models/01-effect.md` — alignment with chain shape; resolves "Effect registration" + "Effect as first-class type" open questions.
- `Product/features/triggers/models/01-trigger-fold.md` — `Trigger<T>` compiles into `EffectDefinition`; fold delegates to `IEffectPipeline.RunEffect`.
- `Product/features/engine/models/02-gamerules-contract.md` — confirm runtime stays at two methods; remove "forthcoming effect-pipeline surface" forward-references.
- `Product/features/engine/models/01-evaluation-loop.md` — `pipeline.Execute(action, chainScope?)` signature; envelope-as-atomicity-boundary semantic per D11.
- `Product/features/actions/models/01-command-pipeline.md` — drop `Validate` from `ICommandHandler`; pipeline gains `IValidator<TCmd>` resolution per D12.
- `Product/features/actions/models/05-available-actions-snapshot.md` — snapshot legality consumes `IValidator<TCmd>`s instead of handler-side `Validate`.
- `Product/ledgers/OPEN-ITEMS.md` — resolve `oq-effect-pipeline` by deletion; add `oq-effect-authoring`, `oq-runtime-as-state-evaluator`, `oq-session-state-machine`.
- `Product/ledgers/GLOSSARY.md` — entries for `EffectDefinition`, `EffectInstance`, `EffectBindings`, `IEffectPipeline`, chain scope, `Fired` set.
