---
type: model
status: settled
references: [features/actions/models/05-available-actions-snapshot.md, features/engine/models/03-game-rules-runtime.md, features/engine/models/07-match-state.md, features/effects/models/01-effect.md, decisions/0007-five-validation-layers.md, decisions/0006-two-pipeline-architecture.md, decisions/0009-engine-as-fsm.md]
tags: [#presentation, #loop, #available-actions, #model]
---

> **Rework pending.** Per [ADR-0009](Product/decisions/0009-engine-as-fsm.md), the `IAvailableActions` service + `AvailableActionsState` slice are retired; valid command types live on `MatchState.Actions`. The `AffordanceMap` projection + global `IIntentMapper` described below are reworked as per-state polymorphic ViewModels (`MatchStateViewModel<TState>.Bind` + `Map`) under [ADR-0012 — *forthcoming*]. The content of this Model is preserved as-is until ADR-0012's cascade lands the per-state VM shape.

# Presentation Loop — model

## What it is

The end-to-end loop from engine to UI and back, with a focus on how player-facing affordances are computed, surfaced, and acted upon. This Model emerged from sketching the Pile Counter sample game against the `GameRulesRuntime` contract — the snapshot's shape and publication mechanism couldn't be settled in isolation, so the full presentation cycle was worked through.

```
                ┌─────────────────────────────────────────────────────┐
                │                       Engine                        │
                │  (handlers, runtime, queue, drain, pipeline)        │
                └─────────────────────────────────────────────────────┘
                          ▲                            │
                          │ GameCommand                │ state mutation
                          │                            ▼
                ┌─────────────────────────────────────────────────────┐
                │                       Store                         │
                │  GameState (incl. AvailableActionsState slice)      │
                └─────────────────────────────────────────────────────┘
                          ▲                            │
                          │ PlayerIntent               │ subscribe
                          │                            ▼
                ┌─────────────────────────────────────────────────────┐
                │                  Gateway / View                     │
                │  derives AffordanceMap from snapshot                │
                │  renders UI, captures input, emits intents          │
                └─────────────────────────────────────────────────────┘
                          ▲                            │
                          │ drag/drop                  │ render
                          ▼                            ▼
                                   Player
```

Read top-to-bottom for the *write* path (engine → state → UI), bottom-to-top for the *read* path (player → intent → command → engine).

## Contracts

### Layer responsibilities

| Layer | Owns | Knows about |
|---|---|---|
| **Engine** | Handler dispatch, runtime invocations, queue, drain, pipeline | `GameCommand`s, `GameState`, runtime, handlers |
| **Store / GameState** | Canonical, immutable game state (incl. snapshot slice) | Itself only — pure data |
| **Store wrapper** (game-supplied) | State access, state-change subscriptions, intent submission | `GameState`, `IIntentMapper`, engine |
| **`IIntentMapper`** | `(ActionTemplate, PlayerChoice)` → fully resolved `GameCommand` | Game-specific; reads state to resolve |
| **`AffordanceMap`** | UI-friendly projection of the snapshot | Game-specific; built from `AvailableActionsState.Snapshot` |
| **View** | Rendering, drag/drop capture, intent emission | Game-specific; consumes `AffordanceMap` and state |

Each row down is more game-specific than the one above. The framework provides everything down to (and including) the engine and the snapshot's canonical shape; the game owns `IIntentMapper`, `AffordanceMap`, and `View`.

### State shape

The snapshot lives in `GameState` as a framework-owned slice (per [`state/state.md`](features/state/state.md)):

```csharp
// Framework-owned slice
public sealed record AvailableActionsState(
    AvailableActionsSnapshot Snapshot) : BaseState;
```

`AvailableActionsSnapshot` carries a typed `ActionTemplate` list (no scope discriminator — see [`actions/05-available-actions-snapshot.md`](features/actions/models/05-available-actions-snapshot.md) for the shape, [`engine/04-available-actions.md`](features/engine/models/04-available-actions.md) for production).

Treating the snapshot as state — not as a side-channel — has three consequences:

1. **Reactive UI for free.** The UI subscribes to state; snapshot updates flow through the same path as any other state change. No separate channel to wire.
2. **Replays carry it.** Snapshots restore as part of state, useful for debugging "why did the UI show this affordance?"
3. **Generic across games.** The framework ships the slot type; per-game `ActionTemplate` subtypes plug in.

### The snapshot's element type

The snapshot stores **typed `ActionTemplate` subtypes** — not resolved commands, not intents, not game-specific affordances. Each command type has a paired template type (`MoveCardCommand` ↔ `MoveCardTemplate`, etc.) that declares the action's shape: source entity, target slots, modes, parameters. Resolution to a `GameCommand` happens at the `IIntentMapper` boundary when the player picks. Full reasoning in [`actions/05-available-actions-snapshot.md`](features/actions/models/05-available-actions-snapshot.md).

Reasoning:

- **Templates express the *space* of legal actions.** A multi-target spell is one template with multiple target slots, not 6 separate commands. Modal cards are one template with mode options. X-cost spells declare a parameter range. Flat command enumeration would be combinatorially huge or unbounded.
- **Per-action typed shapes.** Each template subtype exposes only the fields its action needs.
- **Game-specific affordance shapes belong in the UI layer, not in state.** State is generic; the projection happens at the boundary.

Trade-off accepted: the UI never reads typed templates directly for rendering. It reads its game-specific `AffordanceMap`, which is built from the snapshot once per state change.

### `AffordanceMap` (game-specific)

The `AffordanceMap` is the UI's actual reading surface. It's built from the snapshot, lives in the gateway/UI layer, and is recomputed whenever `AvailableActionsState.Snapshot` changes.

```csharp
public sealed class PileCounterAffordances
{
    private readonly Dictionary<Ref<Card>, IReadOnlyList<Ref<Zone>>> moveTargets;

    public PileCounterAffordances(AvailableActionsSnapshot snapshot)
    {
        moveTargets = snapshot.Templates
            .OfType<MoveCardTemplate>()
            .ToDictionary(
                t => t.Card,
                t => (IReadOnlyList<Ref<Zone>>)t.Destinations.Cast<Ref<Zone>>().ToList());
    }

    public bool CanDrag(Ref<Card> card)
        => moveTargets.ContainsKey(card);

    public IReadOnlyList<Ref<Zone>> ValidDrops(Ref<Card> card)
        => moveTargets.GetValueOrDefault(card, Array.Empty<Ref<Zone>>());
}
```

The map pattern-matches on typed template subtypes — no string-keyed slot lookup, no command construction. Each template already exposes its data as named fields.

> **Boundary note.** `Ref<Card>` and `Ref<Zone>` keys/parameters are **boundary-correct** here per [`Product/decisions/0005-objects-over-ids.md`](Product/decisions/0005-objects-over-ids.md). The `AffordanceMap` is the presentation-side consumer of the engine's wire-form snapshot — presentation works in terms of refs because it receives the snapshot from the boundary, not a live object graph.

**Properties:**

- Pure projection of the snapshot. No additional state.
- Game-specific — Pile Counter's affordance shape is "card → valid destinations." Other games will have completely different shapes (target selectors, choice prompts, etc.).
- Recomputed on every snapshot change. For Pile Counter (~12 cards, few legal moves), trivial. For larger games, can be optimized by diffing or caching, but that's a presentation concern.

### `View` (renders + captures input)

```csharp
public sealed class PileCounterView
{
    private readonly IGameStore store;
    private PileCounterAffordances affordances;

    public PileCounterView(IGameStore store)
    {
        this.store = store;
        store.OnStateChanged += RecomputeAffordances;
        RecomputeAffordances();
    }

    private void RecomputeAffordances()
    {
        affordances = new PileCounterAffordances(
            store.State.Get<AvailableActionsState>().Snapshot);
        Render();
    }

    public void OnDragStart(Ref<Card> card)
        => HighlightPiles(affordances.ValidDrops(card));

    public void OnDrop(Ref<Card> card, Ref<Zone> destination)
    {
        if (!affordances.ValidDrops(card).Contains(destination))
            return;   // defensive — UI shouldn't have allowed this drop
        store.SubmitIntent(new MoveCardIntent(card, destination));
    }
}
```

The view never sees raw commands. It reads from `AffordanceMap` and emits `PlayerIntent`s.

### Store / Gateway

```csharp
public sealed class GameStore : IGameStore
{
    private readonly GameEngine engine;
    private readonly IIntentMapper intentMapper;

    public GameState State { get; private set; }
    public event Action OnStateChanged;

    public bool SubmitIntent(PlayerIntent intent, out string? reason)
    {
        var template = LookupTemplate(intent);
        var choice   = ExtractChoice(intent);
        var command  = intentMapper.Map(template, choice);
        if (engine.Dispatch(command, out reason))
            return true;

        ShowToast(reason);
        return false;
    }
}
```

The store is the boundary between presentation and engine. It owns the `IIntentMapper` and exposes a single `SubmitIntent` entry point.

### Validation layers (presentation-side breakdown)

Validation runs at multiple layers; each has a different purpose. The full project-wide breakdown is in [`Product/decisions/0007-five-validation-layers.md`](Product/decisions/0007-five-validation-layers.md). Presentation-side view:

| Layer | What it answers | Failure mode |
|---|---|---|
| **Per-template eligibility** (runtime, snapshot build) | "Should this template appear at all?" (e.g., suppress "twice in a row") | Template not added to snapshot |
| **`AffordanceMap.CanDrag` / `ValidDrops`** (UI) | "Is this drag visually allowed?" | UI silently disallows — player sees no error |
| **`IIntentMapper.Map`** (gateway) | "Can this template + choice be resolved into a command?" | Throws or returns null — programmer error if UI emitted a bogus intent |
| **Engine lifecycle gate** (framework) | "Is the engine accepting commands right now?" | `Dispatch` returns `false` with reason `MatchNotStarted` / `MatchEnded` |
| **`handler.Validate`** (engine, intake) | "Is this command legal in the current state?" | `Dispatch` returns `false` with reason — gateway shows toast |

The relationship between `AffordanceMap` and `handler.Validate`:

- The snapshot is the runtime's **best-effort approximation**: per-template eligibility filters whole-template suppression; per-binding `Validate` may run on demand if the runtime wants exhaustive coverage, but it's not the default ([`engine/04 §Validation posture`](features/engine/models/04-available-actions.md)).
- `AffordanceMap` is **prophylactic** — projects the snapshot to prevent obviously bad intents.
- `handler.Validate` at dispatch is **authoritative** — single source of truth, catches stale bindings (state changed between snapshot publication and intent dispatch) and any bindings the runtime didn't pre-validate.

### Snapshot publication from `Evaluate`

Per [ADR-0006](Product/decisions/0006-two-pipeline-architecture.md), there is no `OnQueueDrained` callback. The runtime publishes the snapshot from inside `Evaluate` whenever it deems appropriate — typically when its own state-based follow-ups are exhausted (drain about to end):

```csharp
// Inside the runtime (services injected via base class)
public override IReadOnlyList<GameAction> Evaluate(GameAction _, CommandResult __)
{
    var followUps = ComputeStateBasedFollowUps();
    if (followUps.Any()) return followUps;

    // No follow-ups — drain about to end. Publish snapshot now.
    Actions.Set(new AvailableActionsSnapshot(EnumerateTemplates(State)));
    return Array.Empty<GameAction>();
}
```

The snapshot write is a service call — see [`engine/04 §The service-driven model`](features/engine/models/04-available-actions.md). The runtime never holds a `GameEngine` reference; it holds narrow capability services. Effects mid-execution override the snapshot through the same write path (target pickers, choose-one prompts).

## Invariants

Model-local invariants. (See `presentation.md ## Invariants` for feature-level rules — snapshot-as-state, validation layered with handler.Validate authoritative, View never raw, intents atomic, transport-boundary filter cuts data not identity.)

- **The presentation layer subscribes; it does not poll.** State subscriptions go through the `Store`.
- **The `AffordanceMap` is rebuilt on every snapshot change.** No incremental diff machinery in the framework default; presentation may add caching as needed.
- **The View consumes `AffordanceMap`, not raw `ActionTemplate`s.** Templates are wire-form; affordances are UI-form.
- **Intents are emitted at gesture-commit (drag-end), not earlier.** Drag highlights come from the `AffordanceMap`; the intent is built only when the player commits.
- **The Store is the single boundary between presentation and engine.** No bypass paths from View to engine; everything goes through `Store.SubmitIntent`.
- **Validation feedback (toast / inline error) is presentation-side.** The engine surfaces the dispatch result + reason; the gateway formats and displays.

## Decisions

**Project-wide ADRs.**

- [`Product/decisions/0007-five-validation-layers.md`](Product/decisions/0007-five-validation-layers.md) — Five validation layers, with `handler.Validate` authoritative on the engine side and `AffordanceMap` prophylactic on the presentation side. This Model owns the AffordanceMap layer (one of the five). Cross-cutting (engine + actions + presentation).
- [`Product/decisions/0006-two-pipeline-architecture.md`](Product/decisions/0006-two-pipeline-architecture.md) — Framework command pipeline + game effect pipeline split. Per ADR-0006, snapshot publication folds into `Evaluate` (no `OnQueueDrained` callback); presentation reads the published snapshot via state subscription regardless of when the runtime publishes it.

**Synthesis.** This Model is a synthesis layer over decisions made in sibling features; the locked synthesis:

- **Snapshot lives in state.** `AvailableActionsState.Snapshot` (typed templates only — no scope discriminator). Source: `state/state.md` (folded ADR — snapshot-in-state).
- **Snapshot element type is typed `ActionTemplate` per command type.** Each command has a paired template carrying source + slots/modes/parameters. Source: `actions/05-available-actions-snapshot.md` (Q1, formerly ADR-0001 — folded).
- **Snapshot is written via `IAvailableActions`.** By the runtime (default from inside `Evaluate`) or by effects mid-execution. Engine never auto-publishes. Source: `engine/04-available-actions.md` (folded ADR — snapshot-writer-iavailableactions) + ADR-0006.
- **`AffordanceMap` is game-specific.** Lives in the gateway/UI layer; recomputed on every snapshot change; pattern-matches typed templates. Source: this Model's design pass (no source ADR).
- **View consumes `AffordanceMap`, not snapshot directly.** Templates are wire-form; affordances are UI-form. Source: this Model.
- **Intents are emitted at drag-end.** Drag highlights come from `AffordanceMap`; the intent is built only on commit. Source: this Model.

## Examples

### Pile Counter — end-to-end flow trace

A player makes a move:

1. **Engine idle.** Queue empty after last drain. The runtime's `Evaluate` published the snapshot via `Actions.Set`; `AvailableActionsState.Snapshot` updated.
2. **Store fires `OnStateChanged`.** View receives event.
3. **View recomputes `AffordanceMap`.** `MoveCardTemplate`s in the snapshot keyed by `Card`. UI re-renders; only draggable cards show drag handles.
4. **Player drags a card.** `OnDragStart` highlights valid drop piles from the affordance map.
5. **Player releases over a target pile.** `OnDrop` validates against affordance map (defensive), emits `MoveCardIntent(card, destination)`.
6. **Store binds the intent.** Picks the matching `ActionTemplate` from the snapshot, wraps the player's pick in `PlayerChoice`, calls `IIntentMapper.Map(template, choice)` → `MoveCardCommand(card, from, to)`.
7. **Store calls `engine.Dispatch(action, out reason)`.**
8. **Engine intake pipeline:**
   1. Pre-pipeline awaiter check: `awaiter.TryResolve(action)`. Pile Counter has no open awaits, so this is a no-op and the command falls through.
   2. **Lifecycle gate** — `EngineState.Lifecycle == Started`, normal command → permitted.
   3. **`handler.Validate(action, state, out reason)`** — passes (snapshot prevented obviously illegal intents; this catches stale bindings).
   4. Action lands in the engine's framework-private FIFO queue.
9. **Drain begins.** Pipeline runs: before-fold (no triggers) → `MoveCardHandler.Handle` → after-fold (no triggers) → envelope published.
10. **State mutations fire.** Handler called `ctx.Store.ExecuteBatch(...)` with the framework's `MoveCardMutation` plus game-side payloads (`AdjustPileCounterPayload` × 2, `SetLastMovePayload`). All committed atomically as one batch.
11. **`runtime.Evaluate`** runs — checks if all piles match targets. If yes, returns `[EndMatchCommand]`. If not, the runtime publishes a fresh snapshot inside the same `Evaluate` call via `Actions.Set(...)` and returns `[]`.
12. **Drain reaches empty queue.** No `OnQueueDrained` callback (per ADR-0006); the snapshot was already published in step 11.
13. **Store fires `OnStateChanged`.** Loop returns to step 2.

## Open follow-ups

- **`IIntentMapper` shape** — refined: `Map(ActionTemplate, PlayerChoice) → GameCommand`. `PlayerChoice` shape (positional vs named, parameter typing) pin during implementation.
- **`IGameStore` shape** — sketched here as a presentation-side wrapper; final shape depends on the chosen state library (Scaffold State).
- **Snapshot revision tag** — useful for the UI to detect "I have a stale snapshot." Worth adding once we hit a concrete need.
- **FSM-shaped game state** — type-only routing in `ICommandAwaiter` is the current mechanism. The richer FSM-shaped state model is the eventual successor — would replace type-only routing with self-describing game-state phases (`PickATargetState`, `MainPhaseState`, etc.) each owning their own snapshot-generation surface.
- **Async-decision UI signal** — currently the template set conveys context (only legal-response templates during a decision). If a richer "decision-active" UI signal becomes obviously needed, add it.
- **Per-actor affordances in multiplayer** — different players see different affordances. Currently global; pin when multiplayer lands.
- **Animation timing / queue** — presentation-implementation concern; framework provides state events, not the queue.
- **Async decisions, multiplayer, snapshot diffing** — the loop was designed against Pile Counter (no triggers, no async decisions, no phase transitions). Richer games may surface additional concerns; the four-layer model holds.

---

*Below: provenance — this Model has no folded or elevated ADR. The presentation-loop design emerged from sketching Pile Counter against the `GameRulesRuntime` contract; the decisions captured (snapshot in state, typed templates, AffordanceMap as projection, View consumes AffordanceMap, intent at drop, layered validation) are documented in `## Decisions` above. Each contributing decision lives in its source feature's provenance — typed templates and on-demand validation in `actions/05-available-actions-snapshot.md`; service-driven snapshot publication in `engine/04-available-actions.md`; five validation layers as elevated [ADR-0007](Product/decisions/0007-five-validation-layers.md); two-pipeline architecture as project-wide [ADR-0006](Product/decisions/0006-two-pipeline-architecture.md).*

## Options considered

*n/a — purely descriptive Model. The presentation loop is a synthesis of decisions made elsewhere; each contributing decision has its own provenance in its source feature's Model. The "Synthesis" list under `## Decisions` above tracks the synthesis but adds no new options-weighed at this Model's scope.*

## Rationale

*n/a — see `## Contracts` and `## Decisions` above. The four-layer model (Store → IntentMapper → AffordanceMap → View) is the rationale at this level.*

## Consequences

*n/a — see `## Contracts` and `presentation.md ## Invariants`.*
