---
type: model
status: settled
references: [features/engine/models/03-game-rules-runtime.md, features/actions/models/05-available-actions-snapshot.md, features/effects/models/04-command-awaiter.md]
tags: [#engine, #snapshot, #available-actions]
---

# Available Actions — model

## What it is

How the available-actions snapshot is **produced** and **published**. The shape of `AvailableActionsSnapshot` is settled in [`actions/05-available-actions-snapshot.md`](features/actions/models/05-available-actions-snapshot.md); this Model settles its production: the runtime constructs typed `ActionTemplate`s and writes them through the `IAvailableActions` service. Validation stays at dispatch unless the rule explicitly opts a template into pre-validation. Snapshot production is its own pipeline triggered when the rule (or an effect) decides — not auto-fired by the engine on drain.

```csharp
public interface IAvailableActions
{
    void Set    (AvailableActionsSnapshot snapshot);    // wholesale replace
    void Append (IReadOnlyList<ActionTemplate> additional);   // additive; future composition
    void Clear  ();                                     // erase between phases / decisions
}
```

```
runtime.Evaluate (or effect mid-execution)
        │
        ▼
   Actions.Set(snapshot)
        │
        ▼   (internal mutator)
   AvailableActionsState slice
        │
        ▼
   IntentGateway / presentation read via IStateScope
```

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

### The `AvailableActionsState` slice

`IAvailableActions` writes to a framework-owned `BaseState` slice — the canonical home for the published snapshot. Reads go through `IStateScope.Get<AvailableActionsState>()` like any other slice.

```csharp
public sealed record AvailableActionsState(
    AvailableActionsSnapshot Snapshot) : BaseState;
```

- **Purpose.** Holds the current available-actions snapshot. Per [ADR-0006](Product/decisions/0006-two-pipeline-architecture.md), publication is purely runtime-driven — typically from inside `Evaluate` when the runtime detects its own follow-ups are exhausted, or from effects mid-execution.
- **Written by.** `IAvailableActions` service via a framework-internal mutator. Game authors write through the service, never directly.
- **Read by.** Gateway (pre-dispatch check), presentation layer, `GameRulesRuntime.Evaluate` for re-build.
- **Seeded.** `AvailableActionsState(AvailableActionsSnapshot.Empty)` at `Build()` (framework auto-seeds; never declared in `MatchSetup.InitialSlices`).

The slice is a framework-owned tier slice — see [`state/state.md` §Slice tiers](features/state/state.md) for the framework-vs-game-authored convention and the auto-seeding rule.

### Default trigger: `runtime.Evaluate` at drain-end

Per [ADR-0006](Product/decisions/0006-two-pipeline-architecture.md), `OnQueueDrained` is deleted. The runtime calls `Actions.Set(...)` from inside `Evaluate` when it detects "queue is about to empty" — its own state-based follow-ups are exhausted, no more work to enqueue. This is the canonical write moment for most games.

```csharp
public override IReadOnlyList<GameAction> Evaluate(GameAction action, CommandResult result)
{
    var followUps = ComputeStateBasedFollowUps();
    if (followUps.Any()) return followUps;

    // No follow-ups. Publish snapshot at this moment.
    Actions.Set(BuildSnapshot());
    return Array.Empty<GameAction>();
}
```

### Explicit triggers: anywhere the game wants

Effects mid-execution can override the snapshot through the same service (target pickers, choose-one prompts, mid-resolution priority grants). Phase-driven games re-publish on every phase change by reading state inside `Evaluate`. One-shot games (Stacklands) call `Actions.Set` once at bootstrap and never again.

The engine never auto-publishes. If nobody calls the service, `AvailableActions` does not change.

### Validation posture: on-demand

The rule does **not** automatically run `handler.Validate` against every candidate while building the snapshot. Per-binding validation runs at dispatch (authoritative) and any time the rule chooses to call it during enumeration to prune templates whose every binding fails. The cost is a deliberate game-author decision, not a framework default.

### Action templates — typed per command type

Typed templates per command type with a shared base. The base carries what the snapshot machinery and presentation projection need; subtypes carry per-action shape.

```csharp
public abstract record ActionTemplate(
    Type      CommandType,    // uses the registered command type directly
    EntityId? Source);

public sealed record MoveCardTemplate(
    Ref<Card>                Card,
    IReadOnlyList<Reference> Destinations)
    : ActionTemplate(typeof(MoveCardCommand), Card);

public sealed record CastSpellTemplate(
    Ref<Card>                 Spell,
    IReadOnlyList<Reference>  Targets,
    IntRange?                 XCost,
    IReadOnlyList<ModeOption> Modes)
    : ActionTemplate(typeof(CastSpellCommand), Spell);
```

> **ADR-0005 boundary note.** `Ref<Card> Card`, `Ref<Card> Spell`, and other `Reference`-typed fields on `ActionTemplate` subtypes are **boundary-correct**. `ActionTemplate` records are snapshot wire-form — transported to the presentation layer and consumed by `IntentMapper` to build `GameCommand`s. Rule-side code constructing templates derives refs from held objects (`card.SelfRef`, `spell.SelfRef`); the template stores the ref because it is the boundary form. See [ADR-0005](Product/decisions/0005-objects-over-ids.md).

Two simplifications:

- **`Type CommandType`.** The handler registry is already keyed by command type; `IntentMapper` dispatches on it; history serialises it. No parallel string-id registry.
- **Targets are `IReadOnlyList<Reference>` uniformly.** The entity model already says everything targetable is an entity. Whether the entity is a card, slot, player, or zone is a per-action concern; the snapshot layer doesn't need to know.

`TargetSlot` / `ModeOption` / `ParameterSpec` remain valid building blocks; they live as fields on subtypes that need them. A `MoveCardTemplate` doesn't carry `Modes` or `Parameters` because it doesn't need them — typed shapes let each action surface only what it actually has.

### IntentMapper becomes typed

```csharp
public class PileCounterIntentMapper : IIntentMapper
{
    private readonly IStateScope state;
    public PileCounterIntentMapper(IStateScope state) => this.state = state;

    public GameCommand Map(ActionTemplate template, PlayerChoice choice) => template switch
    {
        MoveCardTemplate m => new MoveCardCommand(
            Card: m.Card,
            From: LocateCard(state, m.Card),
            To:   (Ref<Zone>)choice.Targets[0]),
        _ => throw new InvalidOperationException($"unknown template: {template}")
    };

    private static Ref<Zone> LocateCard(IStateScope state, Ref<Card> card)
    {
        var cardRef = state.Get<CardMapState>().RefOf(card);
        foreach (var pileRef in PileCounterGameDefinition.PileRefsAt(state))
            if (state.Get<ZoneContentsState>(pileRef).Cards.Contains(cardRef))
                return pileRef;
        throw new InvalidOperationException($"card {card} not in any pile");
    }
}
```

No string-keyed slot lookup; the template's fields name themselves. `IStateScope` is injected for normal state-driven mapping (locating the card's current pile), not for decision awareness — the mapper is decision-unaware.

**Authoring cost.** Each new action shape adds a template type. That's incremental — same curve as adding a `GameCommand` + `Handler`. Pile Counter has one template; a Hearthstone-shaped game maybe ten; MTG-shaped maybe twenty. The cost scales with distinct action shapes, not with state-space size.

### Snapshot shape

The snapshot carries only the available templates:

```csharp
public sealed record AvailableActionsSnapshot(
    IReadOnlyList<ActionTemplate> Templates);
```

There is no `Scope` discriminator (`FreeInput` / `DecisionPrompt` are deleted). "Is an await open?" is answered by `ICommandAwaiter` ([`effects/04-command-awaiter.md`](features/effects/models/04-command-awaiter.md)), not the snapshot. The template set itself conveys context: during a targeting decision the snapshot contains only the legal-target templates; during free input it contains the full main-phase template set. The UI renders what the template set implies; it does not need a separate scope signal.

The FSM-shaped game-state model (`PickATargetState`, `MainPhaseState`, etc., each owning their own snapshot-generation surface) is the eventual successor to type-only routing. See "Open follow-ups."

### Effect-side symmetry

Effects mid-execution write the snapshot through the same `IAvailableActions` service the rule uses, exposed on `IHandlerContext`. `IHandlerContext` shape lives in [`effects/03-handler-context.md`](features/effects/models/03-handler-context.md); type-only decision routing and await semantics live in [`effects/04-command-awaiter.md`](features/effects/models/04-command-awaiter.md). Headlines: `IHandlerContext` carries `Store`, `Actions`, `Resolver` (no `Mutate` surface, no decision-id field — type-only routing answers "is a decision open?"); decision handlers `await ctx.Resolver.Wait<TCommand>()`.

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

No `Scope` on the snapshot. The mapper is decision-unaware; the engine-level awaiter check intercepts `SelectTargetCommand` before it reaches the pipeline. The next drain's `Evaluate` publishes a fresh template set.

## Invariants

1. **`IAvailableActions` is the only public write path.** Runtime + effects call `Set` / `Append` / `Clear`; nobody writes `AvailableActionsState` directly. Behind the service is an internal mutator going through the same `Store.Execute(payload)` pipeline as everything else.
2. **Engine never auto-publishes.** If nobody calls the service, the snapshot does not change. One-shot games call `Set` once at bootstrap; phase-driven games re-publish on phase change; effects override mid-execution.
3. **No `Scope` discriminator on the snapshot.** "Is a decision open?" is answered by `ICommandAwaiter`, not the snapshot. `FreeInput` / `DecisionPrompt` are deleted.
4. **Validation is on-demand.** No automatic `handler.Validate` sweep during snapshot build. The rule chooses whether to pre-validate; dispatch is authoritative.
5. **Templates are typed per command type with a shared base.** `ActionTemplate` carries `CommandType` + `Source?`; subtypes carry per-action shape (`MoveCardTemplate`, `CastSpellTemplate`, `ChooseTargetTemplate`).
6. **`Reference`-typed fields on templates are boundary-correct (ADR-0005).** Templates are wire-form; rule-side code derives refs from held objects (`card.SelfRef`).
7. **Effect-side symmetry.** Effects use the same `IAvailableActions` service through `ctx.Actions`. Type-only decision routing means no decision-id is threaded — the awaiter recognises the command type and resumes.

## Decisions

*n/a — no elevated ADR.* (The two decisions that shape this Model — snapshot-in-state and snapshot-writer-as-`IAvailableActions` — are single-feature scope and folded as Q1–Q2 below.)

## Examples

### Pile Counter under the new shape

```csharp
public sealed class PileCounterRulesRuntime : GameRulesRuntime<PileCounterRulesDefinition>
{
    private readonly IActor                   _player;
    private readonly IReadOnlyList<Ref<Zone>> _pileRefs;

    public PileCounterRulesRuntime(
        PileCounterRulesDefinition definition,
        IStateScope                state,
        IAvailableActions          actions,
        IReadOnlyList<IActor>      actors)
        : base(definition, state, actions)
    {
        _player   = actors.Single();
        _pileRefs = PileCounterGameDefinition.PileRefsAt(state);
    }

    // FilterTriggers omitted — base class ships a virtual identity default.

    public override IReadOnlyList<GameAction> Evaluate(GameAction _, CommandResult __)
    {
        var pileStates = _pileRefs.Select(r => State.Get<PileState>(r));

        // Win check: all piles match their target?
        if (pileStates.All(p => p.Counter == p.Target))
            return new[] { new EndMatchCommand(new MatchOutcome.Win(_player)) };

        // No state-based follow-ups. Publish snapshot at this moment.
        Actions.Set(BuildSnapshot());
        return Array.Empty<GameAction>();
    }

    private AvailableActionsSnapshot BuildSnapshot()
    {
        var lastMove  = State.Get<LastMoveState>().Move;
        var templates = new List<ActionTemplate>();

        foreach (var sourceRef in _pileRefs)
        {
            var contents = State.Get<ZoneContentsState>(sourceRef).Cards;
            if (contents.Count == 0) continue;

            var topCardRef  = contents[^1];
            var topInstance = State.Get<CardMapState>().InstanceOf(topCardRef);
            if (lastMove?.Card.Equals(topInstance) == true) continue;   // per-template suppression

            var destinations = _pileRefs
                .Where(p => !p.Equals(sourceRef))
                .Cast<Reference>()
                .ToList();

            templates.Add(new MoveCardTemplate(topInstance, destinations));
        }

        return new AvailableActionsSnapshot(templates);
    }
}
```

The runtime visibly owns enumeration. `Actions.Set` is the publish step. No `Validate` calls during build — per-binding legality runs at dispatch. State is read fresh per access through `State.Get<TState>(...)`; slice types (`PileState` per-pile, `LastMoveState` singleton) and the runtime's full sketch live in [`support/samples/pile-counter.md`](support/samples/pile-counter.md). Per ADR-0006, there is no `OnIncomingCommand` and no `OnQueueDrained` — the runtime contributes via `Evaluate` only, and the framework FIFO handles command queueing.

### Rules-layer shape — historical note

> **Historical.** The single-type `GameRules` from earlier drafts has since been split into `GameRulesDefinition` (data) + `GameRulesRuntime<TDefinition>` (runtime), and per ADR-0006 the runtime no longer holds `IIntakeQueue` or implements `OnIncomingCommand` / `OnQueueDrained`. The service-injection rationale below still holds — it describes why services live on the runtime instead of being passed per-call. See [`engine/03 §The interface`](features/engine/models/03-game-rules-runtime.md) for the canonical contract.

Methods take only what's ephemeral to the call. Everything that's a stable engine handle (state access, snapshot writer) is a constructor-injected service.

The reasoning, in three points:

1. **Effect-side symmetry.** Effects mid-execution also need to write the snapshot. Services give us one write path the rule and effects share. Parameter-passing would force the engine to build a different surface for effects.
2. **Method signatures shrink to "what's ephemeral."** Cleaner reading; method intent is obvious.
3. **State-pattern decision falls out.** No method takes `state`; all read it through the injected `IStateScope` via `State.Get<TState>()`. The asymmetry between methods that need state and those that don't goes away.

## Open follow-ups

- **`GameState` slice catalog.** The Scaffold State binding makes "what slices exist" a concrete question (turn, phase, piles, available-actions, decisions, last-move, etc.). Tracked in [`state/state.md`](features/state/state.md).
- **FSM-shaped game state.** `FreeInput` / `DecisionPrompt` were dropped. Type-only routing in `ICommandAwaiter` is the current mechanism. The richer FSM-shaped state model (typed `PickATargetState`, `MainPhaseState`, etc., each owning their own snapshot-generation surface) is the eventual successor. Revisit when the first async-decision-heavy game arrives. See [`effects/04-command-awaiter.md` §Open follow-ups](features/effects/models/04-command-awaiter.md).
- **Composition.** `Append` / `Clear` are on the service surface for future use. Module composition (`IActionProvider` / `IActionPolicy` / ordering / enable-disable) is deliberately undesigned — pin when first composed-rules game forces it.
- **`Source` nullability.** Sourceless `EndTurn` keeps `Source` nullable on the base. Aesthetic; defer ([`actions/05 §Open`](features/actions/models/05-available-actions-snapshot.md)).
- **`PlayerChoice` shape under typed templates.** With typed templates, `IntentMapper` switches on template type and unpacks named slots itself; `PlayerChoice` may collapse to positional `IReadOnlyList<Reference>` + `IReadOnlyDictionary<string, object>` parameters. Pin when `IntentMapper` is implemented.

---

*Below: provenance — two engine-feature decisions that shape this Model. Both single-feature scope (no project-wide ADR applies; folded into Model provenance per §4.5 on 2026-05-08). Originals 2026-04-29.*

## Options considered

### Q1 — Where does the snapshot live?

The snapshot of currently-legal actions has to flow to presentation, network, and replay. Where does the snapshot *live*? In the rule? In a side channel? Or in canonical state?

- **A — Side-channel observable owned by the rule.** Rejected — bypasses the state pattern, breaking replay and per-player views.
- **B — Recompute on every read by presentation.** Rejected — cost is unbounded for complex games and tying it to a frame loop hides bugs.
- **C — `AvailableActionsState` slice in `GameState`, written via the `IAvailableActions` service.** **Chosen.**

### Q2 — Snapshot writer surface

The snapshot has multiple potential writers — the runtime (the typical default site), effects mid-execution (decision pickers), and the engine (auto-publish?). Multiple write paths fragment ordering and introduce races.

- **A — Engine auto-publishes after every command.** Rejected — auto-publication can't represent intermediate decision-scoped snapshots, and it forces work the runtime may not want.
- **B — Runtime writes directly to `AvailableActionsState`.** Rejected — leaks the state-pattern boundary.
- **C — Single `IAvailableActions` service exposed to both the runtime and effects (`Set` / `Append` / `Clear`); engine never auto-publishes.** **Chosen.**

The runtime calls `Actions.Set(...)` from inside `Evaluate` whenever it deems appropriate (per ADR-0006, `OnQueueDrained` is gone — typical moment is when the runtime's own state-based follow-ups are exhausted). Effects override mid-execution for decision-scoped snapshots. Engine doesn't publish.

## Rationale

The two decisions converge on the service-driven model. **Q1 (snapshot in state)** is what gives uniform observation, replay, and snapshot-driven UIs — presentation reads the snapshot through the same state-subscription mechanism it uses for everything else. **Q2 (single service writer)** is what gives a single ordering invariant: snapshot mutations are serialized through one service, used by both the runtime and effects. The combination eliminates parallel write paths and makes the "is a decision open?" question belong to the awaiter mechanism rather than to a snapshot scope discriminator.

The on-demand validation posture is consistent with this: the rule decides when to pre-validate; dispatch is the authoritative check. No automatic per-template `Validate` sweep during build.

## Consequences

- **Enables** uniform observation, replay, and snapshot-driven UIs; effects publishing decision-scoped snapshots without bespoke wiring; a single ordering invariant on snapshot mutations; dropping the `Scope` discriminator entirely (the awaiter answers "is a decision open?").
- **Constrains** every snapshot mutation to round-trip through the writer service (no direct state writes); one-shot games (Stacklands) must `Set` once at bootstrap; phase-driven games (MTG) `Set` on phase enter / priority grant; effects mid-execution must call `Actions.Set` to override the snapshot for decision pickers.
