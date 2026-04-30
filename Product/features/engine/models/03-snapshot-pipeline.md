---
type: model
status: settled
last-updated: 2026-04-29
builds-on: [features/engine/models/02-gamerules-contract.md, features/actions/models/02-availability-snapshot.md]
tags: [#engine, #snapshot, #available-actions]
---

# Snapshot Production Pipeline — How AvailableActions Gets Built

This document defines **how** the available-actions snapshot is produced and published. [Doc 28](features/actions/models/02-availability-snapshot.md) settled the snapshot's *shape*; this doc settles its *production*. Together they replace the snapshot-build line in [doc 27 §9](features/presentation/models/01-presentation-loop.md) and the `DrainResult.Snapshot` slot in [doc 25 §6](features/engine/models/02-gamerules-contract.md).

Builds on:

- [features/engine/models/02-gamerules-contract.md](features/engine/models/02-gamerules-contract.md) — `GameRules` interface; `OnQueueDrained`
- [support/samples/pile-counter.md](support/samples/pile-counter.md) — sample game; concrete sketch
- [features/presentation/models/01-presentation-loop.md](features/presentation/models/01-presentation-loop.md) — presentation loop; snapshot consumer
- [features/actions/models/02-availability-snapshot.md](features/actions/models/02-availability-snapshot.md) — snapshot shape (templates)

---

## 1. Why This Doc Exists

Doc 28 defined `ActionAvailability` as a template carrying `Source`, `TargetSlots`, `Modes`, `Parameters`, but left three production questions open (doc 28 §6, §8; checkpoint task #18):

1. Where action enumeration lives — handler, rule, descriptor, composition.
2. Eager vs. lazy binding validation.
3. How snapshot production composes with the rest of the per-command pipeline.

This doc resolves all three. Headline: **the rule constructs templates and writes them through a service. Validation stays at dispatch unless the rule explicitly opts a template into pre-validation. Snapshot production is its own pipeline triggered when the rule (or an effect) decides — not auto-fired by the engine on drain.**

---

## 2. The Service-Driven Model

Snapshot production is *not* a return value the engine collects from `OnQueueDrained` and writes to state. The rule (and effects) write the snapshot directly through a service the engine provides.

```csharp
public interface IAvailableActions
{
    void Set    (AvailableActionsSnapshot snapshot);
    void Append (IReadOnlyList<ActionTemplate> additional);
    void Clear  ();
}
```

- **`Set`** — replace the snapshot wholesale. The default writer.
- **`Append`** — additive contribution; useful for future composition where multiple rule modules each contribute templates.
- **`Clear`** — erase the snapshot (between phases, between decisions, etc.).

Behind the service the engine runs an internal mutator that writes to `GameState.AvailableActions`. The service is the only public write path.

### Default trigger: `OnQueueDrained`

The rule calls `Actions.Set(...)` from `OnQueueDrained` after each drain. This stays the canonical write moment for most games.

### Explicit triggers: anywhere the game wants

Effects mid-execution can override the snapshot through the same service (target pickers, choose-one prompts, mid-resolution priority grants). Phase-driven games re-publish on every phase change by reading state inside `OnQueueDrained`. One-shot games (Stacklands) call `Actions.Set` once at bootstrap and never again.

The engine never auto-publishes. If nobody calls the service, `AvailableActions` does not change.

### Validation posture: on-demand

The rule does **not** automatically run `handler.Validate` against every candidate while building the snapshot. Doc 27 §9's "snapshot is built by running `Validate` against every candidate" line is retired. Per-binding validation runs at dispatch (authoritative) and any time the rule chooses to call it during enumeration to prune templates whose every binding fails. The cost is a deliberate game-author decision, not a framework default.

---

## 3. Action Templates — Typed Per Command Type

Doc 28 §2 sketched a single generic `ActionAvailability` record. We refine it: typed templates per command type with a shared base. The base carries what the snapshot machinery and presentation projection need; subtypes carry per-action shape.

```csharp
public abstract record ActionTemplate(
    Type      CommandType,    // was ActionId — uses the registered command type directly
    EntityId? Source);

public sealed record MoveCardTemplate(
    InstanceId              Card,
    IReadOnlyList<EntityId> Destinations)
    : ActionTemplate(typeof(MoveCardCommand), Card);

public sealed record CastSpellTemplate(
    InstanceId                Spell,
    IReadOnlyList<EntityId>   Targets,
    IntRange?                 XCost,
    IReadOnlyList<ModeOption> Modes)
    : ActionTemplate(typeof(CastSpellCommand), Spell);
```

Two simplifications from doc 28's draft:

- **`Type CommandType` instead of `ActionId`.** The handler registry is already keyed by command type ([doc 25 §6](features/engine/models/02-gamerules-contract.md)); IntentMapper dispatches on it; history serialises it. No parallel string-id registry.
- **Targets are `IReadOnlyList<EntityId>` uniformly.** Doc 10's instance/entity model already says everything targetable is an entity. Whether the entity is a card, slot, player, or zone is a per-action concern; the snapshot layer doesn't need to know.

`TargetSlot` / `ModeOption` / `ParameterSpec` from doc 28 §2 remain valid building blocks; they live as fields on subtypes that need them. A `MoveCardTemplate` doesn't carry `Modes` or `Parameters` because it doesn't need them — typed shapes let each action surface only what it actually has.

### IntentMapper becomes typed

```csharp
public sealed class PileCounterIntentMapper : IIntentMapper
{
    public GameCommand Map(ActionTemplate template, PlayerChoice choice) => template switch
    {
        MoveCardTemplate m => new MoveCardCommand(
            Card: m.Card,
            From: state.LocateCard(m.Card),
            To:   (PileId)choice.Targets[0]),
        _ => throw new InvalidOperationException($"unknown template: {template}")
    };
}
```

No string-keyed slot lookup; the template's fields name themselves.

### Authoring cost

Each new action shape adds a template type. That's incremental — same curve as adding a `GameCommand` + `Handler`. Pile Counter has one template; a Hearthstone-shaped game maybe ten; MTG-shaped maybe twenty. The cost scales with distinct action shapes, not with state-space size.

---

## 4. Snapshot Scope

The snapshot carries a scope discriminator so the presentation layer knows whether the player is responding to free input or a specific prompt:

```csharp
public sealed record AvailableActionsSnapshot(
    SnapshotScope                 Scope,
    IReadOnlyList<ActionTemplate> Templates);

public abstract record SnapshotScope;
public sealed record FreeInput                       : SnapshotScope;
public sealed record DecisionPrompt(DecisionId Id)   : SnapshotScope;
```

`FreeInput` — main-phase snapshot; the player is free to pick any template. `DecisionPrompt` — the engine is paused waiting for a specific decision (target picker, choose-one). The UI renders a different mode.

This is **transitional.** A richer model — a typed game-state machine where `PickACardState` / `ResolveStackState` / `MainPhaseState` each own their own validation surface — is the eventual direction. We've used FSM-shaped state in other games; the simple scope flag here is enough for the floor and ceiling cases doc 28 sketched. Revisit when async decisions force the issue (see [§9](#9-open-items)).

---

## 5. `GameRules` Shape Change — Services Injected

Methods take only what's ephemeral to the call. Everything that's a stable engine handle (state access, queue, handlers, snapshot writer) is a constructor-injected service.

```csharp
public abstract class GameRules
{
    protected IGameStateProvider State    { get; }
    protected IAvailableActions  Actions  { get; }
    protected IIntakeQueue       Pending  { get; }
    protected IHandlerRegistry   Handlers { get; }

    protected GameRules(
        IGameStateProvider state,
        IAvailableActions  actions,
        IIntakeQueue       pending,
        IHandlerRegistry   handlers)
    {
        State    = state;
        Actions  = actions;
        Pending  = pending;
        Handlers = handlers;
    }

    public abstract IntakeResult OnIncomingCommand(
        GameCommand    command,
        DispatchOrigin origin);

    public abstract IReadOnlyList<TriggerMatch> FilterTriggers(
        IReadOnlyList<TriggerMatch> candidates,
        GameCommand                 command,
        TriggerPhase                phase);

    public abstract IReadOnlyList<GameAction> Evaluate(
        GameAction    justExecuted,
        CommandResult result);

    public abstract DrainResult OnQueueDrained();
}
```

### What moved

| Was a parameter | Now an injected service |
|---|---|
| `GameState state` | `IGameStateProvider.Current` |
| `IntakeContext.Pending` (live queue) | `IIntakeQueue` |
| `IHandlerRegistry handlers` | `IHandlerRegistry` |
| (new) snapshot write | `IAvailableActions` |

### What stays as a parameter

Anything truly ephemeral to *this* call: `command`, `origin`, `candidates`, `phase`, `justExecuted`, `result`.

### Contract changes

- **`IntakeContext` collapses.** It carried `Pending` + `CurrentlyExecuting` + `Origin`. `Pending` is now the injected `IIntakeQueue`; `CurrentlyExecuting` lives on the queue or state provider; `Origin` is a method parameter.
- **`DrainResult` shrinks** to just dispatch:
  ```csharp
  public sealed record DrainResult(IReadOnlyList<GameCommand> Dispatch)
  {
      public static readonly DrainResult Empty = new(Array.Empty<GameCommand>());
  }
  ```
  Snapshot publication moved to `Actions.Set(...)`.
- **"Observer-shaped, never side-effects" loosens for `OnQueueDrained`.** Doc 25 §2's contract guarantee keeps for `Evaluate` and `FilterTriggers`. `OnIncomingCommand` already mutates `Pending`. `OnQueueDrained` now also writes through `Actions`. Both are deliberate: side effects flow through narrow capability services, not through a held `GameEngine` reference. The "rule never holds the engine" guarantee is preserved.
- **"Zero game-affecting state" still holds.** Services are configuration; gameplay memory still lives in `GameState`. The rule holds service handles, not data.

### Why service-injected, not parameter-passed

Three reasons:

1. **Effect-side symmetry.** Effects mid-execution also need to write the snapshot ([§7](#7-effect-side-symmetry-deferred)). Services give us one write path the rule and effects share. Parameter-passing would force the engine to build a different surface for effects.
2. **Method signatures shrink to "what's ephemeral."** Cleaner reading; method intent is obvious.
3. **Resolves [doc 26 Q1](support/samples/pile-counter.md) (state-pattern decision).** No method takes `state`; all read it from the provider. The asymmetry between `OnIncomingCommand` (no state) and the others (state passed) goes away.

---

## 6. Pile Counter Under the New Shape

```csharp
public sealed class PileCounterRules : GameRules
{
    public PileCounterRules(
        IGameStateProvider state,
        IAvailableActions  actions,
        IIntakeQueue       pending,
        IHandlerRegistry   handlers)
        : base(state, actions, pending, handlers) { }

    public override IntakeResult OnIncomingCommand(GameCommand command, DispatchOrigin origin)
    {
        Pending.AddLast(command);
        return IntakeResult.Ok;
    }

    public override IReadOnlyList<TriggerMatch> FilterTriggers(
        IReadOnlyList<TriggerMatch> candidates,
        GameCommand command,
        TriggerPhase phase)
        => candidates;

    public override IReadOnlyList<GameAction> Evaluate(GameAction _, CommandResult __)
        => State.Current.Piles.All(p => p.Score == p.Target)
            ? new[] { new EndGameCommand(Outcome.Win) }
            : Array.Empty<GameAction>();

    public override DrainResult OnQueueDrained()
    {
        var s = State.Current;
        var templates = new List<ActionTemplate>();

        foreach (var pile in s.Piles.Where(p => p.Cards.Count > 0))
        {
            var top = pile.TopCard;
            if (s.LastMove?.Card == top.Id) continue;   // per-template suppression

            var destinations = s.Piles
                .Where(p => p.Id != pile.Id)
                .Select(p => (EntityId)p.Id)
                .ToList();

            templates.Add(new MoveCardTemplate(top.Id, destinations));
        }

        Actions.Set(new AvailableActionsSnapshot(new FreeInput(), templates));
        return DrainResult.Empty;
    }
}
```

The rule visibly owns enumeration. `Actions.Set` is the publish step. No `Validate` calls during build — per-binding legality runs at dispatch.

---

## 7. Effect-Side Symmetry (Deferred)

Effects mid-execution must write the snapshot the same way the rule does — a target picker shows different actions than free input. The clean shape: effects receive the same `IAvailableActions` service the rule has, probably through `IHandlerContext`:

```csharp
public async Task<EntityId> PickTarget(IHandlerContext ctx, IReadOnlyList<EntityId> legal)
{
    ctx.Actions.Set(new AvailableActionsSnapshot(
        Scope:     new DecisionPrompt(ctx.DecisionId),
        Templates: new[] { new ChooseTargetTemplate(legal) }));

    var choice = await ctx.AwaitPlayerChoice();
    // next drain's OnQueueDrained overwrites back to FreeInput
    return choice.Targets[0];
}
```

The full effect-flow contract — context shape, decision-id threading, await semantics, suspension model — is not designed in one place yet. Tracked as an explicit follow-up; see [§9](#9-open-items).

---

## 8. Implications Across Existing Docs

Mechanical follow-up edits, not done in this doc.

| Doc | Section | Change |
|---|---|---|
| 25 | §2 (Interface) | Methods shed `state` / `handlers` / `IntakeContext` parameters; rule becomes abstract class with injected services |
| 25 | §2 (Contract guarantees) | "Observer-shaped" relaxes for `OnIncomingCommand` (already mutates `Pending`) and `OnQueueDrained` (now writes via `Actions`); narrow-capability services replace held engine reference |
| 25 | §3 (`OnIncomingCommand`) | `IntakeContext` collapses; `Pending` is injected `IIntakeQueue`; `Origin` is a parameter |
| 25 | §6 (`OnQueueDrained`) | Returns `DrainResult { Dispatch }` only; snapshot writes through `Actions.Set` |
| 25 | §10 (Open Items) | State-pattern decision (Q1) resolved: services injected |
| 26 | §9.1 (state shape) | `AvailableActions` type is `AvailableActionsSnapshot` (Scope + Templates) |
| 26 | §9.6 (the rule) | Rewrite per [§6](#6-pile-counter-under-the-new-shape) |
| 26 | §9.7 (engine intake) | `IntakeContext` collapses; intake takes `command` + `origin` |
| 26 | §9.8 (bootstrap) | Expand: engine builder constructs services and injects into the rule's constructor |
| 26 | §11 Q1 | Resolved (services injected) |
| 27 | §4 (state shape) | `AvailableActions` becomes `AvailableActionsSnapshot` |
| 27 | §6 (AffordanceMap) | Reads typed `ActionTemplate` subtypes; pattern-matches on type instead of stringly-named slots |
| 27 | §9 (validation layers) | Snapshot construction is approximate; `Validate` runs at dispatch (authoritative) and on-demand if the rule opts in |
| 27 | §10 (end-to-end trace) | Step 12 is `Actions.Set`, not engine-writes-snapshot |
| 27 | §11 (decisions locked) | Snapshot element type is `ActionTemplate` (typed); writer is the service |
| 27 | §12 (`OnQueueDrained` output) | `DrainResult.Snapshot` is removed |
| 28 | §2 (the shape) | Refine to typed `ActionTemplate` subtypes with shared base; `CommandType` field; entity-list slots |
| 28 | §6 (eligibility) | Eager-vs-lazy framing replaced by "on-demand by the rule" |

---

## 9. Open Items

- **`IGameStateProvider` shape.** `State.Current` returns the immutable `GameState` of right now. Subscriptions probably live on the Store (presentation), not the provider. Confirm at engine implementation.
- **`IIntakeQueue` shape.** Same operations as the old `LinkedList<GameCommand>` Pending: `AddFirst` / `AddLast` / `Remove` / iterate. `CurrentlyExecuting` lives here as a property, or on the state provider. Pin during engine implementation.
- **Effect-side symmetry.** Effects need `IAvailableActions` mid-execution. Likely through `IHandlerContext.Actions`. Needs a dedicated effect-flow doc that walks the full lifecycle (decision id threading, await semantics, suspension). Tracked as task #20.
- **Snapshot scope as a state machine.** Current `FreeInput` / `DecisionPrompt` discriminator is transitional. The richer FSM-shaped game-state model (typed `PickACardState`, `ResolveStackState`, etc., each owning its own validation) is the eventual direction. Revisit when the first async-decision-heavy game arrives.
- **Composition.** `Append` / `Clear` are on the service surface for future use. Module composition (`IActionProvider` / `IActionPolicy` / ordering / enable-disable) is deliberately undesigned — pin when first composed-rules game forces it.
- **Bootstrap wiring.** Engine builder constructs services and the rule. Sketched in [doc 26 §9.8](support/samples/pile-counter.md); expand there to show service-injection wiring. Not blocking; cosmetic.
- **`Source` nullability.** Sourceless `EndTurn` keeps `Source` nullable on the base. Aesthetic; defer ([doc 28 §8](features/actions/models/02-availability-snapshot.md)).
- **`PlayerChoice` shape under typed templates.** Doc 28's `PlayerChoice` had named-string targets/parameters. With typed templates, IntentMapper switches on template type and unpacks named slots itself; `PlayerChoice` may collapse to positional `IReadOnlyList<EntityId>` + `IReadOnlyDictionary<string, object>` parameters. Pin when IntentMapper is implemented.

---

## 10. Related

- [features/engine/models/02-gamerules-contract.md](features/engine/models/02-gamerules-contract.md) — `GameRules` interface (this doc reshapes it)
- [support/samples/pile-counter.md](support/samples/pile-counter.md) — sample game; concrete sketch
- [features/presentation/models/01-presentation-loop.md](features/presentation/models/01-presentation-loop.md) — presentation loop; snapshot consumer
- [features/actions/models/02-availability-snapshot.md](features/actions/models/02-availability-snapshot.md) — snapshot shape (templates)
- [features/effects/models/01-async-dispatch.md](features/effects/models/01-async-dispatch.md) — effect/decision flow context for [§7](#7-effect-side-symmetry-deferred)
- [features/entities/models/01-definition-instance-model.md](features/entities/models/01-definition-instance-model.md) — entity model; informs uniform `EntityId` slots
