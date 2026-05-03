---
type: model
status: settled
references: [features/engine/models/02-gamerules-contract.md, features/actions/models/02-availability-snapshot.md]
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

> **ADR-0005 boundary note.** `InstanceId Card`, `InstanceId Spell`, and other `Identifier<T>` fields on `ActionTemplate` subtypes are **boundary-correct**. `ActionTemplate` records are snapshot wire-form — transported to the presentation layer and consumed by `IntentMapper` to build `GameCommand`s. Rule-side code constructing templates derives IDs from held objects (`card.Id`, `spell.Id`); the template stores the ID because it is the boundary form. See [ADR-0005](decisions/0005-objects-over-ids.md).

Two simplifications from doc 28's draft:

- **`Type CommandType` instead of `ActionId`.** The handler registry is already keyed by command type ([doc 25 §6](features/engine/models/02-gamerules-contract.md)); IntentMapper dispatches on it; history serialises it. No parallel string-id registry.
- **Targets are `IReadOnlyList<EntityId>` uniformly.** Doc 10's instance/entity model already says everything targetable is an entity. Whether the entity is a card, slot, player, or zone is a per-action concern; the snapshot layer doesn't need to know.

`TargetSlot` / `ModeOption` / `ParameterSpec` from doc 28 §2 remain valid building blocks; they live as fields on subtypes that need them. A `MoveCardTemplate` doesn't carry `Modes` or `Parameters` because it doesn't need them — typed shapes let each action surface only what it actually has.

### IntentMapper becomes typed

```csharp
public sealed class PileCounterIntentMapper : IIntentMapper
{
    private readonly IStateScope state;
    public PileCounterIntentMapper(IStateScope state) => this.state = state;

    public GameCommand Map(ActionTemplate template, PlayerChoice choice) => template switch
    {
        MoveCardTemplate m => new MoveCardCommand(
            Card: m.Card,
            From: LocateCard(state.Get<PilesState>(), m.Card),
            To:   (PileId)choice.Targets[0]),
        _ => throw new InvalidOperationException($"unknown template: {template}")
    };

    private static PileId LocateCard(PilesState piles, InstanceId card)
        => piles.Piles.First(kv => kv.Value.Cards.Any(c => c.Id == card)).Key;
}
```

No string-keyed slot lookup; the template's fields name themselves. `IStateScope` is injected for normal state-driven mapping (locating the card's current pile), not for decision awareness — the mapper is decision-unaware (S15).

### Authoring cost

Each new action shape adds a template type. That's incremental — same curve as adding a `GameCommand` + `Handler`. Pile Counter has one template; a Hearthstone-shaped game maybe ten; MTG-shaped maybe twenty. The cost scales with distinct action shapes, not with state-space size.

---

## 4. Snapshot Shape

The snapshot carries only the available templates:

```csharp
public sealed record AvailableActionsSnapshot(
    IReadOnlyList<ActionTemplate> Templates);
```

There is no `Scope` discriminator (`FreeInput` / `DecisionPrompt` are deleted — S15 Q6). "Is an await open?" is answered by `ICommandAwaiter`, not the snapshot. The template set itself conveys context: during a targeting decision the snapshot contains only the legal-target templates; during free input it contains the full main-phase template set. The UI renders what the template set implies; it does not need a separate scope signal.

The FSM-shaped game-state model (`PickATargetState`, `MainPhaseState`, etc., each owning their own snapshot-generation surface) is the eventual successor to type-only routing. See [§9](#9-open-items).

---

## 5. Rules Layer Shape — Services Injected

> **Note:** This section is historical. The single-type `GameRules` shown below has since been split into `GameRulesDefinition` (data) + `GameRulesRuntime<TDefinition>` (runtime). The service-injection rationale below still holds — it describes why services live on the runtime instead of being passed per-call. See [`engine/02 §1–§2`](features/engine/models/02-gamerules-contract.md) for the canonical contract.

Methods take only what's ephemeral to the call. Everything that's a stable engine handle (state access, queue, and snapshot writer) is a constructor-injected service.

```csharp
public abstract class GameRules
{
    protected IStateScope        State    { get; }   // Scaffold; read-only
    protected IAvailableActions  Actions  { get; }
    protected IIntakeQueue       Pending  { get; }

    protected GameRules(
        IStateScope        state,
        IAvailableActions  actions,
        IIntakeQueue       pending)
    {
        State    = state;
        Actions  = actions;
        Pending  = pending;
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
| `GameState state` | Scaffold `IStateScope` (read sliced state via `State.Get<TState>()`) |
| `IntakeContext.Pending` (live queue) | `IIntakeQueue` |
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
3. **Resolves [doc 26 Q1](support/samples/pile-counter.md) (state-pattern decision).** No method takes `state`; all read it through the injected `IStateScope` via `State.Get<TState>()`. The asymmetry between `OnIncomingCommand` (no state) and the others (state passed) goes away.

---

## 6. Pile Counter Under the New Shape

```csharp
public sealed class PileCounterRules : GameRules
{
    public PileCounterRules(
        IStateScope        state,
        IAvailableActions  actions,
        IIntakeQueue       pending)
        : base(state, actions, pending) { }

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
        => State.Get<PilesState>().Piles.Values.All(p => p.Score == p.Target)
            ? new[] { new EndGameCommand(Outcome.Win) }
            : Array.Empty<GameAction>();

    public override DrainResult OnQueueDrained()
    {
        var piles    = State.Get<PilesState>().Piles.Values;
        var lastMove = State.Get<LastMoveState>().Move;
        var templates = new List<ActionTemplate>();

        foreach (var pile in piles.Where(p => p.Cards.Count > 0))
        {
            var top = pile.TopCard;
            if (lastMove?.Card == top.Id) continue;   // per-template suppression

            var destinations = piles
                .Where(p => p.Id != pile.Id)
                .Select(p => (EntityId)p.Id)
                .ToList();

            templates.Add(new MoveCardTemplate(top.Id, destinations));
        }

        Actions.Set(new AvailableActionsSnapshot(templates));
        return DrainResult.Empty;
    }
}
```

The rule visibly owns enumeration. `Actions.Set` is the publish step. No `Validate` calls during build — per-binding legality runs at dispatch. State is read fresh per access through `State.Get<TState>()`; slice types (`PilesState`, `LastMoveState`) live in [support/samples/pile-counter.md §9.1](support/samples/pile-counter.md).

---

## 7. Effect-Side Symmetry (Resolved)

Effects mid-execution write the snapshot through the same `IAvailableActions` service the rule uses, exposed on `IHandlerContext`. The full contract — `IHandlerContext` shape, decision-id threading, await semantics — is settled in [features/effects/models/04-effect-flow.md](features/effects/models/04-effect-flow.md). Headlines: `IHandlerContext` carries `Store`, `Actions`, `Resolver` (no `Mutate`, no decision-id field — the action's `CommandId` *is* the decision id); decision handlers `await ctx.Resolver.Await(action.Id)`.

A target-picker decision handler under that model:

```csharp
public sealed class RequestTargetHandler
    : ICommandHandler<RequestTargetAction, RequestTargetResult>
{
    public async Task<RequestTargetResult> Handle(RequestTargetAction action, IHandlerContext ctx)
    {
        var legal = ComputeLegalTargets(ctx.Store, action.Filter);

        ctx.Actions.Set(new AvailableActionsSnapshot(
            new[] { new ChooseTargetTemplate(legal) }));

        var cmd = await ctx.Resolver.Wait<SelectTargetCommand>();
        return new RequestTargetResult { Target = cmd.Target };
    }
}
```

No `Scope` on the snapshot (per [§4](#4-snapshot-shape) and [04-effect-flow §5](features/effects/models/04-effect-flow.md)). The mapper is decision-unaware; the engine-level awaiter check intercepts `SelectTargetCommand` before it reaches the pipeline. The next drain's `OnQueueDrained` publishes a fresh template set.

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

- ~~**`IGameStateProvider` shape.**~~ Folded away — `GameRules` (and effect/handler contexts) bind directly to Scaffold `IStateScope` (read) / `Store` (read + execute mutators); see [04-effect-flow §4.1–§4.2](features/effects/models/04-effect-flow.md).
- **`GameState` slice catalog.** With the Scaffold State binding, "what slices exist" is concrete: turn, phase, piles, available-actions, decisions, last-move, etc. Tracked as checkpoint task #2.
- **`IIntakeQueue` shape.** Same operations as the old `LinkedList<GameCommand>` Pending: `AddFirst` / `AddLast` / `Remove` / iterate. `CurrentlyExecuting` lives here as a property, or on a sibling slice. Pin during engine implementation.
- ~~**Effect-side symmetry.**~~ Resolved by [04-effect-flow](features/effects/models/04-effect-flow.md). `IHandlerContext` exposes `Store` + `Actions` + `Resolver`; decision id ≡ action's `CommandId`; await/resume via `Resolver.Await(id)` / `TryResolve(id, choice)`. Documented in [§7](#7-effect-side-symmetry-resolved) above.
- **FSM-shaped game state.** `FreeInput` / `DecisionPrompt` were dropped (S15 Q6). Type-only routing in `ICommandAwaiter` is the current mechanism. The richer FSM-shaped state model (typed `PickATargetState`, `MainPhaseState`, etc., each owning their own snapshot-generation surface) is the eventual successor. Revisit when the first async-decision-heavy game arrives. See [04-effect-flow §9](features/effects/models/04-effect-flow.md).
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
