---
type: model
status: settled
references: [features/actions/models/02-availability-snapshot.md, features/engine/models/03-snapshot-pipeline.md, features/engine/models/02-gamerules-contract.md, features/effects/models/01-async-dispatch.md]
tags: [#presentation, #loop, #available-actions]
---

# Presentation Loop — model

## What it is

The end-to-end loop from engine to UI and back, with a focus on how player-facing affordances are computed, surfaced, and acted upon. Came out of sketching the Pile Counter sample game against the `GameRules` interface — the snapshot's shape and publication mechanism couldn't be settled in isolation, so the full presentation cycle was worked through.

```
                ┌─────────────────────────────────────────────────────┐
                │                       Engine                        │
                │  (handlers, rule, queue, drain, pipeline)           │
                └─────────────────────────────────────────────────────┘
                          ▲                            │
                          │ GameCommand                │ state mutation
                          │                            ▼
                ┌─────────────────────────────────────────────────────┐
                │                       Store                         │
                │  GameState (incl. AvailableActions snapshot)        │
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
| **Engine** | Handler dispatch, rule invocations, queue, drain, pipeline | `GameCommand`s, `GameState`, rule, handlers |
| **GameState** | Canonical, immutable game state (incl. snapshot slot) | Itself only — pure data |
| **Store** | State access, state-change subscriptions, intent submission | `GameState`, `IntentMapper`, engine |
| **IntentMapper** | `PlayerIntent` → fully resolved `GameCommand` | Game-specific; reads state to resolve |
| **AffordanceMap** | UI-friendly projection of the snapshot | Game-specific; built from `AvailableActions` |
| **View** | Rendering, drag/drop capture, intent emission | Game-specific; consumes AffordanceMap and state |

Each row down is more game-specific than the one above. The framework provides everything down to (and including) the engine and the snapshot's canonical shape; the game owns IntentMapper, AffordanceMap, and View.

### State shape

The snapshot lives in `GameState` as a first-class state slice. Per [`engine/04-gamestate-slices.md`](features/engine/models/04-gamestate-slices.md):

```csharp
// Framework-owned slice
public sealed record AvailableActionsState(
    AvailableActionsSnapshot Snapshot) : BaseState;
```

`AvailableActionsSnapshot` carries a typed `ActionTemplate` list (no scope discriminator — see [`actions/02-availability-snapshot.md`](features/actions/models/02-availability-snapshot.md) for the shape, [`engine/03-snapshot-pipeline.md`](features/engine/models/03-snapshot-pipeline.md) for production).

Treating the snapshot as state — not as a side-channel — has three consequences:

1. **Reactive UI for free.** The UI subscribes to state; snapshot updates flow through the same path as any other state change. No separate channel to wire.
2. **Replays carry it.** Snapshots restore as part of state, useful for debugging "why did the UI show this affordance?"
3. **Generic across games.** The framework ships the slot type; per-game `ActionTemplate` subtypes plug in.

### The snapshot's element type

The snapshot stores **typed `ActionTemplate` subtypes** — not resolved commands, not intents, not game-specific affordances. Each command type has a paired template type (`MoveCardCommand` ↔ `MoveCardTemplate`, etc.) that declares the action's shape: source entity, target slots, modes, parameters. Resolution to a `GameCommand` happens at the IntentMapper boundary when the player picks. Full reasoning in [`actions/02-availability-snapshot.md`](features/actions/models/02-availability-snapshot.md).

Reasoning:

- **Templates express the *space* of legal actions.** A multi-target spell is one template with multiple target slots, not 6 separate commands. Modal cards are one template with mode options. X-cost spells declare a parameter range. Flat command enumeration would be combinatorially huge or unbounded.
- **Per-action typed shapes.** Each template subtype exposes only the fields its action needs.
- **Game-specific affordance shapes belong in the UI layer, not in state.** State is generic; the projection happens at the boundary.

Trade-off accepted: the UI never reads typed templates directly for rendering. It reads its game-specific `AffordanceMap`, which is built from the snapshot once per state change.

### `AffordanceMap` (game-specific)

The `AffordanceMap` is the UI's actual reading surface. It's built from the snapshot, lives in the gateway/UI layer, and is recomputed whenever `AvailableActions` changes.

```csharp
public sealed class PileCounterAffordances
{
    private readonly Dictionary<InstanceId, IReadOnlyList<PileId>> moveTargets;

    public PileCounterAffordances(AvailableActionsSnapshot snapshot)
    {
        moveTargets = snapshot.Templates
            .OfType<MoveCardTemplate>()
            .ToDictionary(
                t => t.Card,
                t => (IReadOnlyList<PileId>)t.Destinations.Cast<PileId>().ToList());
    }

    public bool CanDrag(InstanceId card)
        => moveTargets.ContainsKey(card);

    public IReadOnlyList<PileId> ValidDrops(InstanceId card)
        => moveTargets.GetValueOrDefault(card, Array.Empty<PileId>());
}
```

The map pattern-matches on typed template subtypes — no string-keyed slot lookup, no command construction. Each template already exposes its data as named fields.

> **Boundary note.** `InstanceId` keys in `Dictionary<InstanceId, ...>` and `InstanceId card` parameters in `CanDrag`/`OnDrop` are **boundary-correct** here. The `AffordanceMap` is the presentation-side consumer of the engine's wire-form snapshot — presentation works in terms of IDs because it receives the snapshot from the boundary, not a live object graph. See [`Product/decisions/0005-objects-over-ids.md`](decisions/0005-objects-over-ids.md).

**Properties:**
- Pure projection of `AvailableActions`. No additional state.
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
        affordances = new PileCounterAffordances(store.State.Get<AvailableActionsState>().Snapshot);
        Render();
    }

    private void Render()
    {
        foreach (var pile in store.State.Get<PilesState>().Piles.Values)
        {
            DrawPileScore(pile.Score, pile.Target);
            if (pile.Cards.Count == 0) { DrawEmptySlot(pile.Id); continue; }

            var top = pile.TopCard;
            DrawCard(top, draggable: affordances.CanDrag(top.Id));
        }
    }

    public void OnDragStart(InstanceId card)
        => HighlightPiles(affordances.ValidDrops(card));

    public void OnDrop(InstanceId card, PileId destination)
    {
        if (!affordances.ValidDrops(card).Contains(destination))
            return;   // defensive — UI shouldn't have allowed this drop
        store.SubmitIntent(new MoveCardIntent(card, destination));
    }
}
```

The view never sees raw commands. It reads from `AffordanceMap` and emits `PlayerIntent`s.

### `Store` / `Gateway`

```csharp
public sealed class GameStore : IGameStore
{
    private readonly GameEngine engine;
    private readonly IIntentMapper intentMapper;

    public GameState State { get; private set; }
    public event Action OnStateChanged;

    public IntakeResult SubmitIntent(PlayerIntent intent)
    {
        // Resolve the matching template + choice and bind via mapper
        var template = LookupTemplate(intent);
        var choice   = ExtractChoice(intent);
        var command  = intentMapper.Map(template, choice);
        var result   = engine.Dispatch(command);

        if (!result.Accepted)
            ShowToast(result.Reason);

        return result;
    }
}
```

The store is the boundary between presentation and engine. It owns the `IntentMapper` and exposes a single `SubmitIntent` entry point.

### Validation layers (presentation-side breakdown)

Validation runs at multiple layers; each has a different purpose. The full project-wide breakdown is in [`Product/decisions/0007-five-validation-layers.md`](decisions/0007-five-validation-layers.md). Presentation-side view:

| Layer | What it answers | Failure mode |
|---|---|---|
| **Per-template eligibility** (rule, snapshot build) | "Should this template appear at all?" (e.g., suppress "twice in a row") | Template not added to snapshot |
| **`AffordanceMap.CanDrag` / `ValidDrops`** (UI) | "Is this drag visually allowed?" | UI silently disallows — player sees no error |
| **`IntentMapper.Map`** (gateway) | "Can this template + choice be resolved into a command?" | Throws or returns null — programmer error if UI emitted a bogus intent |
| **`handler.Validate`** (engine, intake) | "Is this command legal in the current state?" | `IntakeResult.Reject(reason)` — gateway shows toast |
| **`rule.OnIncomingCommand`** (engine, intake) | "Is the engine accepting commands right now?" | `IntakeResult.Reject(reason)` — paused, ended, etc. |

The relationship between AffordanceMap and `handler.Validate`:

- The snapshot is the rule's **best-effort approximation**: per-template eligibility filters whole-template suppression; per-binding `Validate` may run on demand if the rule wants exhaustive coverage, but it's not the default ([`engine/03 §Validation posture`](features/engine/models/03-snapshot-pipeline.md)).
- `AffordanceMap` is **prophylactic** — projects the snapshot to prevent obviously bad intents.
- `handler.Validate` at dispatch is **authoritative** — single source of truth, catches stale bindings (state changed between snapshot publication and intent dispatch) and any bindings the rule didn't pre-validate.

### `OnQueueDrained` output shape

`OnQueueDrained` returns only follow-up dispatches; the snapshot is published through the injected `IAvailableActions` service:

```csharp
public sealed record DrainResult(IReadOnlyList<GameCommand> Dispatch)
{
    public static readonly DrainResult Empty = new(Array.Empty<GameCommand>());
}

// Inside the rule (services injected via base class)
public override DrainResult OnQueueDrained()
{
    var templates = EnumerateTemplates(State);
    Actions.Set(new AvailableActionsSnapshot(templates));
    return DrainResult.Empty;
}
```

The engine dispatches each `Dispatch` command. The snapshot write is a service call — see [`engine/03 §Service-driven model`](features/engine/models/03-snapshot-pipeline.md). The rule never holds a `GameEngine` reference; it holds narrow capability services.

### Caveats

This loop was designed against Pile Counter, which has no triggers, no async decisions, no phase transitions, and no game-end state beyond a simple win check. Richer games may surface additional concerns:

- **Async decisions** — when an effect awaits player input (target picker, choice overlay), the snapshot contains only the legal-response templates; the engine's `ICommandAwaiter` handles routing. The UI currently has no explicit "you must respond here" signal from the snapshot (scope discriminator was dropped); the template set itself conveys context.
- **Multiplayer / hidden info** — different players see different affordances. The snapshot may need scoping per actor.
- **Snapshot diffing** — for performance, the engine may compute snapshot deltas rather than full recomputes. Out of scope for now.
- **Animation timing** — the UI may want to defer rendering a new state until an animation completes. State updates fire immediately; the view needs an animation queue. Presentation concern, not framework.

These extend the framework's snapshot story; they don't break the four-layer model above.

## Invariants

Model-local invariants. (See `presentation.md ## Invariants` for feature-level rules — snapshot-as-state, validation layered with handler.Validate authoritative, View never raw, intents atomic, transport-boundary filter cuts data not identity.)

- **The presentation layer subscribes; it does not poll.** State subscriptions go through the `Store`.
- **The `AffordanceMap` is rebuilt on every snapshot change.** No incremental diff machinery in the framework default; presentation may add caching as needed.
- **The View consumes `AffordanceMap`, not raw `ActionTemplate`s.** Templates are wire-form; affordances are UI-form.
- **Intents are emitted at gesture-commit (drag-end), not earlier.** Drag highlights come from the AffordanceMap; the intent is built only when the player commits.
- **The Store is the single boundary between presentation and engine.** No bypass paths from View to engine; everything goes through `Store.SubmitIntent`.
- **Validation feedback (toast / inline error) is presentation-side.** The engine surfaces `IntakeResult.Reject(reason)`; the gateway formats and displays.

## Decisions

**Project-wide ADR.** Linked: [`Product/decisions/0007-five-validation-layers.md`](decisions/0007-five-validation-layers.md) — Five validation layers, with `handler.Validate` authoritative on the engine side and `AffordanceMap` prophylactic on the presentation side. Cross-cutting (engine + actions + presentation).

**Synthesis.** This Model is a synthesis layer over decisions made in sibling features; the locked synthesis:

- **Snapshot lives in state.** `AvailableActionsState.Snapshot` (typed templates only — no scope discriminator). Source: `engine/04-gamestate-slices.md` (Q1, formerly ADR-0003 — folded).
- **Snapshot element type is typed `ActionTemplate` per command type.** Each command has a paired template carrying source + slots/modes/parameters. Source: `actions/02-availability-snapshot.md` (Q1, formerly ADR-0001 — folded).
- **Snapshot is written via `IAvailableActions`.** By the rule (default in `OnQueueDrained`) or by effects mid-execution. Engine never auto-publishes. Source: `engine/03-snapshot-pipeline.md` (Q1, formerly ADR-0004 — folded).
- **`AffordanceMap` is game-specific.** Lives in the gateway/UI layer; recomputed on every snapshot change; pattern-matches typed templates. Source: this Model's design pass (no source ADR).
- **View consumes `AffordanceMap`, not snapshot directly.** Templates are wire-form; affordances are UI-form. Source: this Model.
- **Intents are emitted at drag-end.** Drag highlights come from `AffordanceMap`; the intent is built only on commit. Source: this Model.

## Examples

### Pile Counter — end-to-end flow trace

A player makes a move:

1. **Engine idle.** Queue empty after last drain. `OnQueueDrained` fired; the rule wrote the snapshot via `Actions.Set`. `AvailableActionsState.Snapshot` updated.
2. **Store fires `OnStateChanged`.** View receives event.
3. **View recomputes AffordanceMap.** `MoveCardTemplate`s in the snapshot keyed by `Card`. UI re-renders; only draggable cards show drag handles.
4. **Player drags a card.** `OnDragStart` highlights valid drop piles from the affordance map.
5. **Player releases over a target pile.** `OnDrop` validates against affordance map (defensive), emits `MoveCardIntent(card, destination)`.
6. **Store binds the intent.** Picks the matching `ActionTemplate` from the snapshot (or builds one from the intent), wraps the player's pick in `PlayerChoice`, calls `IntentMapper.Map(template, choice)` → `MoveCardCommand(card, from, to)`.
7. **Store calls `engine.Dispatch(command)`.**
8. **Engine intake pipeline:**
   1. Pre-pipeline awaiter check: `awaiter.TryResolve(command)`. Pile Counter has no open awaits, so this is a no-op and the command falls through. (In a decision-active game this would short-circuit here; no envelope, no handler, no triggers.)
   2. Lifecycle gate: `Started`, normal command → permitted.
   3. `handler.Validate(command, state, out reason)` — passes (snapshot prevented obviously illegal intents; this catches stale bindings).
   4. `rule.OnIncomingCommand(command, origin)` — Pile Counter's rule trivially `Pending.AddLast(command)`, returns `Ok`.
9. **Drain begins.** Pipeline runs: before-fold (no triggers) → `MoveCardHandler.Handle` → after-fold (no triggers) → envelope published.
10. **State mutators fire.** Handler called `ctx.Store.Execute(new MoveCardPayload(...))`; Scaffold's payload-keyed registry fans out to `PileMoveMutator` (writes `PilesState`) and `LastMoveMutator` (writes `LastMoveState`); both commit atomically.
11. **`rule.Evaluate`** runs — checks if all piles match targets. If yes, returns `[EndMatchCommand]`; engine dispatches it next.
12. **Queue drains.** `rule.OnQueueDrained` runs, enumerates new templates, calls `Actions.Set(...)`. The `IAvailableActions` mutator writes to `AvailableActionsState`.
13. **Store fires `OnStateChanged`.** Loop returns to step 2.

## Open follow-ups

- **`IIntentMapper` shape** — refined: `Map(ActionTemplate, PlayerChoice) → GameCommand`. `PlayerChoice` shape (positional vs named, parameter typing) pin during implementation.
- **`IGameStore` shape** — sketched here as a presentation-side wrapper; final shape depends on the chosen state library (Scaffold State).
- **Snapshot revision tag** — useful for the UI to detect "I have a stale snapshot." Worth adding once we hit a concrete need.
- **FSM-shaped game state** — type-only routing in `ICommandAwaiter` is the current mechanism. The richer FSM-shaped state model is the eventual successor — would replace type-only routing with self-describing game-state phases (`PickATargetState`, `MainPhaseState`, etc.) each owning their own snapshot-generation surface.
- **Async-decision UI signal** — currently the template set conveys context (only legal-response templates during a decision). If a richer "decision-active" UI signal becomes obviously needed, add it.
- **Per-actor affordances in multiplayer** — different players see different affordances. Currently global; pin when multiplayer lands.
- **Animation timing / queue** — presentation-implementation concern; framework provides state events, not the queue.

---

*Below: provenance — this Model has no folded or elevated ADR. The presentation-loop design emerged from sketching Pile Counter against the `GameRules` interface; the decisions captured (snapshot in state, typed templates, AffordanceMap as projection, View consumes AffordanceMap, intent at drop, layered validation) are documented in §Decisions locked above. Each decision lives in its source feature's provenance — typed templates and on-demand validation in `actions/02-availability-snapshot.md`; service-driven snapshot publication in `engine/03-snapshot-pipeline.md`; five validation layers as elevated [`Product/decisions/0007`](decisions/0007-five-validation-layers.md).*

## Options considered

*n/a — the presentation loop is a synthesis of decisions made elsewhere. Each contributing decision has its own provenance in its source feature's Model. The "decisions locked" list above tracks the synthesis but adds no new options-weighed.*

## Rationale

*n/a — see `## Contracts` and `## Decisions` above. The four-layer model (Store → IntentMapper → AffordanceMap → View) is the rationale at this level.*

## Consequences

*n/a — see Contracts and `presentation.md ## Invariants`.*
