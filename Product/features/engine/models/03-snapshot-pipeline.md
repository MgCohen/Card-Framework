---
type: model
status: settled
references: [features/engine/models/02-gamerules-contract.md, features/actions/models/02-availability-snapshot.md]
tags: [#engine, #snapshot, #available-actions]
---

# Snapshot Pipeline — model

## What it is

How the available-actions snapshot is **produced and published**. The snapshot's *shape* is owned by [`actions/02-availability-snapshot.md`](features/actions/models/02-availability-snapshot.md); this Model owns its *production*.

Headline: **the rule constructs templates and writes them through a service. Validation stays at dispatch unless the rule explicitly opts a template into pre-validation. Snapshot production is its own pipeline triggered when the rule (or an effect) decides — not auto-fired by the engine on drain.**

## Contracts

### The service-driven model

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

Behind the service the engine runs an internal mutator that writes to `AvailableActionsState`. The service is the only public write path.

**Default trigger: `OnQueueDrained`.** The rule calls `Actions.Set(...)` from `OnQueueDrained` after each drain. This stays the canonical write moment for most games.

**Explicit triggers: anywhere the game wants.** Effects mid-execution can override the snapshot through the same service (target pickers, choose-one prompts, mid-resolution priority grants). Phase-driven games re-publish on every phase change by reading state inside `OnQueueDrained`. One-shot games (Stacklands) call `Actions.Set` once at bootstrap and never again.

The engine never auto-publishes. If nobody calls the service, `AvailableActions` does not change.

**Validation posture: on-demand.** The rule does **not** automatically run `handler.Validate` against every candidate while building the snapshot. Per-binding validation runs at dispatch (authoritative) and any time the rule chooses to call it during enumeration to prune templates whose every binding fails. The cost is a deliberate game-author decision, not a framework default.

### Action templates — typed per command type

Typed templates per command type with a shared base. The base carries what the snapshot machinery and presentation projection need; subtypes carry per-action shape.

```csharp
public abstract record ActionTemplate(
    Type      CommandType,    // uses the registered command type directly
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

> **Boundary note.** `InstanceId Card`, `InstanceId Spell`, and other `Identifier<T>` fields on `ActionTemplate` subtypes are **boundary-correct** per [`Product/decisions/0005-objects-over-ids.md`](decisions/0005-objects-over-ids.md). `ActionTemplate` records are snapshot wire-form — transported to the presentation layer and consumed by `IntentMapper` to build `GameCommand`s. Rule-side code constructing templates derives IDs from held objects (`card.Id`, `spell.Id`); the template stores the ID because it is the boundary form.

Two simplifications:
- **`Type CommandType` instead of an `ActionId` parallel registry.** The handler registry is already keyed by command type; IntentMapper dispatches on it; history serialises it.
- **Targets are `IReadOnlyList<EntityId>` uniformly.** The entity model says everything targetable is an entity. Whether the entity is a card, slot, player, or zone is a per-action concern.

`TargetSlot` / `ModeOption` / `ParameterSpec` from [`actions/02`](features/actions/models/02-availability-snapshot.md) remain valid building blocks; they live as fields on subtypes that need them.

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

No string-keyed slot lookup; the template's fields name themselves. `IStateScope` is injected for normal state-driven mapping (locating the card's current pile), not for decision awareness — the mapper is decision-unaware.

**Authoring cost.** Each new action shape adds a template type. That's incremental — same curve as adding a `GameCommand` + `Handler`. Pile Counter has one template; a Hearthstone-shaped game maybe ten; MTG-shaped maybe twenty. Cost scales with distinct action shapes, not with state-space size.

### Snapshot shape

```csharp
public sealed record AvailableActionsSnapshot(
    IReadOnlyList<ActionTemplate> Templates);
```

There is no `Scope` discriminator. "Is an await open?" is answered by `ICommandAwaiter` (per [`effects/04` §Decision routing — type-only](features/effects/models/04-effect-flow.md)), not the snapshot. The template set itself conveys context: during a targeting decision the snapshot contains only the legal-target templates; during free input it contains the full main-phase template set. The UI renders what the template set implies; it does not need a separate scope signal.

The FSM-shaped game-state model (`PickATargetState`, `MainPhaseState`, etc., each owning their own snapshot-generation surface) is the eventual successor to type-only routing.

### Effect-side symmetry

Effects mid-execution write the snapshot through the same `IAvailableActions` service the rule uses, exposed on `IHandlerContext`. Full contract — `IHandlerContext` shape, decision routing, await semantics — is settled in [`effects/04-effect-flow.md`](features/effects/models/04-effect-flow.md). A target-picker decision handler:

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

No `Scope` on the snapshot. The mapper is decision-unaware; the engine-level awaiter check intercepts `SelectTargetCommand` before it reaches the pipeline. The next drain's `OnQueueDrained` publishes a fresh template set.

## Invariants

Model-local invariants. (See `engine.md ## Invariants` for feature-level rules.)

- **`IAvailableActions` is the only write path.** No direct `GameState.AvailableActions = ...` assignment.
- **The engine never auto-publishes.** If nobody calls the service, the snapshot does not change.
- **Snapshot lives in state via a framework-internal mutator.** Behind `IAvailableActions.Set`, an engine mutator writes `AvailableActionsState`. Game-authored mutators may not target this slice.
- **Validation is on-demand.** The rule does not auto-validate every candidate; per-binding `handler.Validate` runs at dispatch authoritatively, on-demand during enumeration if the rule opts in.
- **Templates are typed per command type.** No string-keyed parallel registry; `Type CommandType` is the discriminator.

## Decisions

*n/a — no elevated ADR.*

## Examples

### Pile Counter under the snapshot pipeline

```csharp
public sealed class PileCounterRules : GameRulesRuntime<PileCounterRulesDefinition>
{
    public PileCounterRules(
        PileCounterRulesDefinition definition,
        IStateScope        state,
        IAvailableActions  actions,
        IIntakeQueue       pending)
        : base(definition, state, actions, pending) { }

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
            ? new[] { new EndMatchCommand(new MatchOutcome.Win(_player)) }
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

The rule visibly owns enumeration. `Actions.Set` is the publish step. No `Validate` calls during build — per-binding legality runs at dispatch. State is read fresh per access through `State.Get<TState>()`.

## Open follow-ups

- **`GameState` slice catalog** — concrete catalog of slices the rule reads (turn, phase, piles, available-actions, decisions, last-move, etc.). Tracked as a checkpoint task; framework slices live in [`engine/04`](features/engine/models/04-gamestate-slices.md).
- **FSM-shaped game state.** Type-only routing in `ICommandAwaiter` is the current mechanism. The richer FSM-shaped state model is the eventual successor.
- **Composition.** `Append` / `Clear` are on the service surface for future use. Module composition (`IActionProvider` / `IActionPolicy` / ordering / enable-disable) is deliberately undesigned — pin when first composed-rules game forces it.
- **`Source` nullability.** Sourceless `EndTurn` keeps `Source` nullable on the base. Aesthetic; defer.
- **`PlayerChoice` shape under typed templates.** With typed templates, `IntentMapper` switches on template type and unpacks named slots itself; `PlayerChoice` may collapse to positional `IReadOnlyList<EntityId>` + `IReadOnlyDictionary<string, object>` parameters. Pin when `IntentMapper` is implemented.

---

*Below: provenance — feature-internal decision folded from `features/engine/decisions/0004-snapshot-writer-iavailableactions.md`. Single-feature scope (the snapshot writer is engine-owned; effects consume the same service via `IHandlerContext` but don't co-decide the publication mechanism).*

## Options considered

### Q1 — Snapshot writer (formerly ADR-0004 — folded)

The snapshot has multiple potential writers — the rule (default `OnQueueDrained`), effects mid-execution (decision pickers), and the engine (auto-publish?). Multiple write paths fragment ordering and introduce races.

- **A — Engine auto-publishes after every command.** Rejected: auto-publication can't represent intermediate decision-scoped snapshots; forces work the rule may not want.
- **B — Rule writes directly to `GameState`.** Rejected: leaks the state-pattern boundary.
- **C — Single `IAvailableActions` service exposed to both the rule and effects** (`Set` / `Append` / `Clear`); engine never auto-publishes. **Chosen.**

## Rationale

`IAvailableActions` is the single write path. Default site is `GameRules.OnQueueDrained`. Effects override mid-execution for decision-scoped snapshots. Engine doesn't publish. This gives a single ordering invariant (snapshot mutations serialized through one service), enables effects to publish decision-scoped snapshots without bespoke wiring, and keeps the state-pattern boundary clean.

## Consequences

- **Enables:** single ordering invariant for snapshot mutations; effects publish decision-scoped snapshots through the same path as the rule; on-demand validation posture (rule decides cost).
- **Constrains:** one-shot games (Stacklands) must `Set` once at bootstrap; phase-driven games (MTG) `Set` on phase enter / priority grant; if nobody publishes, the snapshot is stale.
- **Cascade:** consumed by `effects/04-effect-flow.md` (`IHandlerContext.Actions`); `actions/02-availability-snapshot.md` (typed template shape); `presentation/01` (snapshot consumer).
