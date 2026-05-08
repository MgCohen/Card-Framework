---
type: model
status: settled
references: [features/actions/models/02-availability-snapshot.md, features/engine/models/03-snapshot-pipeline.md]
tags: [#presentation, #loop, #available-actions]
---

# Presentation Loop and Available Actions Flow

This document captures the end-to-end loop from engine to UI and back, with a focus on how player-facing affordances are computed, surfaced, and acted upon. It came out of sketching the Pile Counter sample game (doc 26) against the `GameRules` interface (doc 25) — the snapshot's shape and publication mechanism couldn't be settled in isolation, so we worked through the full presentation cycle.

Builds on:

- [features/engine/models/02-gamerules-contract.md](features/engine/models/02-gamerules-contract.md) — canonical `GameRulesRuntime<T>` contract
- [support/samples/pile-counter.md](support/samples/pile-counter.md) — concrete sample game used to ground the design
- [features/effects/models/01-async-dispatch.md](features/effects/models/01-async-dispatch.md) — layer hierarchy

---

## 1. Why This Doc Exists

While sketching Pile Counter's `GameRules`, two questions stalled progress:

- **A.** *How does the available-actions snapshot get published?* — push API on engine, state slot, or separate channel?
- **B.** *What's the element type?* — `GameCommand`, `PlayerIntent`, or a game-specific affordance shape?

Neither could be answered without picking how the UI consumes the snapshot. The UI's needs determine the snapshot's shape; the snapshot's shape determines how the rule produces it. So we sketched the full presentation loop and worked outward.

---

## 2. The Loop

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

---

## 3. Layer Responsibilities

| Layer | Owns | Knows about |
|---|---|---|
| **Engine** | Handler dispatch, rule invocations, queue, drain, pipeline | `GameCommand`s, `GameState`, rule, handlers |
| **GameState** | Canonical, immutable game state (incl. snapshot slot) | Itself only — pure data |
| **Store** | State access, state-change subscriptions, intent submission | `GameState`, `IntentMapper`, engine |
| **IntentMapper** | `PlayerIntent` → fully resolved `GameCommand` | Game-specific; reads state to resolve |
| **AffordanceMap** | UI-friendly projection of the snapshot | Game-specific; built from `AvailableActions` |
| **View** | Rendering, drag/drop capture, intent emission | Game-specific; consumes AffordanceMap and state |

Each row down is more game-specific than the one above. The framework provides everything down to (and including) the engine and the snapshot's canonical shape; the game owns IntentMapper, AffordanceMap, and View.

---

## 4. State Shape

The snapshot lives in `GameState` as a first-class slot:

```csharp
public sealed class GameState
{
    public IReadOnlyList<Pile>        Piles            { get; }
    public LastMove?                  LastMove         { get; }
    public AvailableActionsSnapshot   AvailableActions { get; }   // canonical
}
```

`AvailableActionsSnapshot` carries a typed `ActionTemplate` list (no scope discriminator — dropped S15 Q6; see [doc 28](features/actions/models/02-availability-snapshot.md) for the shape, [doc 29](features/engine/models/03-snapshot-pipeline.md) for production).

Treating the snapshot as state — not as a side-channel — has three consequences:

1. **Reactive UI for free.** The UI subscribes to state; snapshot updates flow through the same path as any other state change. No separate channel to wire.
2. **Replays carry it.** Snapshots restore as part of state, useful for debugging "why did the UI show this affordance?"
3. **Generic across games.** The framework ships the slot type; per-game `ActionTemplate` subtypes plug in.

---

## 5. The Snapshot's Element Type

The snapshot stores **typed `ActionTemplate` subtypes** — not resolved commands, not intents, not game-specific affordances. Each command type has a paired template type (`MoveCardCommand` ↔ `MoveCardTemplate`, etc.) that declares the action's shape: source entity, target slots, modes, parameters. Resolution to a `GameCommand` happens at the IntentMapper boundary when the player picks. See [doc 28](features/actions/models/02-availability-snapshot.md) for the shape and [doc 29 §3](features/engine/models/03-snapshot-pipeline.md) for the typed-template rationale.

Reasoning:

- **Templates express the *space* of legal actions.** A multi-target spell is one template with multiple target slots, not 6 separate commands. Modal cards are one template with mode options. X-cost spells declare a parameter range. Flat command enumeration would be combinatorially huge or unbounded.
- **Per-action typed shapes.** Each template subtype exposes only the fields its action needs — Pile Counter's `MoveCardTemplate` has `Card` + `Destinations`, no modes or parameters; a `CastSpellTemplate` carries modes + X-cost. No string-keyed slot lookup; IntentMapper switches on type.
- **Game-specific affordance shapes belong in the UI layer, not in state.** State is generic; the projection happens at the boundary.

Trade-off accepted: the UI never reads typed templates directly for rendering. It reads its game-specific `AffordanceMap`, which is built from the snapshot once per state change.

---

## 6. AffordanceMap (Game-Specific)

The `AffordanceMap` is the UI's actual reading surface. It's built from the snapshot, lives in the gateway/UI layer, and is recomputed whenever `AvailableActions` changes.

```csharp
public class PileCounterAffordances
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

> **ADR-0005 boundary note.** `Ref<Card>` and `Ref<Zone>` keys in the dictionary and parameters of `CanDrag`/`OnDrop` are **boundary-correct** here. The `AffordanceMap` is the presentation-side consumer of the engine's wire-form snapshot — presentation works in terms of refs because it receives the snapshot from the boundary, not a live object graph. See [ADR-0005](decisions/0005-objects-over-ids.md).

**Properties:**
- Pure projection of `AvailableActions`. No additional state.
- Game-specific — Pile Counter's affordance shape is "card → valid destinations." Other games will have completely different shapes (target selectors, choice prompts, etc.).
- Recomputed on every snapshot change. For Pile Counter (~12 cards, few legal moves), trivial. For larger games, can be optimized by diffing or caching, but that's a presentation concern.

---

## 7. View (Renders + Captures Input)

```csharp
public class PileCounterView
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
        affordances = new PileCounterAffordances(store.State.AvailableActions);
        Render();
    }

    private void Render()
    {
        foreach (var pileRef in PileCounterGameDefinition.PileRefsAt(store.State))
        {
            var pile     = store.State.Get<PileState>(pileRef);
            var contents = store.State.Get<ZoneContentsState>(pileRef).Cards;
            DrawPileScore(pile.Counter, pile.Target);

            if (contents.Count == 0) { DrawEmptySlot(pileRef); continue; }

            var topRef      = contents[^1];
            var topInstance = store.State.Get<CardMapState>().InstanceOf(topRef);
            DrawCard(topRef, draggable: affordances.CanDrag(topInstance));
        }
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

---

## 8. Store / Gateway

```csharp
public sealed class GameStore : IGameStore
{
    private readonly GameEngine engine;
    private readonly IIntentMapper intentMapper;

    public GameState State { get; private set; }
    public event Action OnStateChanged;

    public bool SubmitIntent(PlayerIntent intent, out string? reason)
    {
        var action = intentMapper.Map(intent);
        if (engine.Dispatch(action, out reason))
            return true;

        ShowToast(reason);
        return false;
    }
}
```

The store is the boundary between presentation and engine. It owns the `IntentMapper` and exposes a single `SubmitIntent` entry point.

---

## 9. Validation Layers

Validation runs at multiple layers; each has a different purpose.

| Layer | What it answers | Failure mode |
|---|---|---|
| **Per-template eligibility** (runtime, snapshot build) | "Should this template appear at all?" (e.g., suppress "twice in a row") | Template not added to snapshot |
| **`AffordanceMap.CanDrag` / `ValidDrops`** (UI) | "Is this drag visually allowed?" | UI silently disallows — player sees no error |
| **`IntentMapper.Map`** (gateway) | "Can this template + choice be resolved into a command?" | Throws or returns null — programmer error if UI emitted a bogus intent |
| **Engine lifecycle gate** (framework) | "Is the engine accepting commands right now?" | `Dispatch` returns `false` with reason `MatchNotStarted` / `MatchEnded` |
| **`handler.Validate`** (engine, intake) | "Is this command legal in the current state?" | `Dispatch` returns `false` with reason — gateway shows toast |

The relationship between AffordanceMap and `handler.Validate`:

- The snapshot is the rule's **best-effort approximation**: per-template eligibility filters whole-template suppression; per-binding `Validate` may run on demand if the rule wants exhaustive coverage, but it's not the default ([doc 29 §2](features/engine/models/03-snapshot-pipeline.md)).
- AffordanceMap is **prophylactic** — projects the snapshot to prevent obviously bad intents.
- `handler.Validate` at dispatch is **authoritative** — single source of truth, catches stale bindings (state changed between snapshot publication and intent dispatch) and any bindings the rule didn't pre-validate.

---

## 10. End-to-End Flow Trace

A player makes a move in Pile Counter:

1. **Engine idle.** Queue empty after last drain. The runtime's `Evaluate` published the snapshot via `Actions.Set`; `state.AvailableActions` updated.
2. **Store fires `OnStateChanged`.** View receives event.
3. **View recomputes AffordanceMap.** `MoveCardTemplate`s in the snapshot keyed by `Card`. UI re-renders; only draggable cards show drag handles.
4. **Player drags a card.** `OnDragStart` highlights valid drop piles from the affordance map.
5. **Player releases over a target pile.** `OnDrop` validates against affordance map (defensive), emits `MoveCardIntent(card, destination)`.
6. **Store binds the intent.** Picks the matching `ActionTemplate` from the snapshot (or builds one from the intent), wraps the player's pick in `PlayerChoice`, calls `IntentMapper.Map(template, choice)` → `MoveCardCommand(card, from, to)`.
7. **Store calls `engine.Dispatch(action, out reason)`.**
8. **Engine intake pipeline:**
   0. Pre-pipeline awaiter check: `awaiter.TryResolve(action)`. Pile Counter has no open awaits, so this is a no-op and the command falls through.
   1. **Lifecycle gate** — `EngineState.Lifecycle == Started`, normal command → permitted.
   2. **`handler.Validate(action, state, out reason)`** — passes (snapshot prevented obviously illegal intents; this catches stale bindings).
   3. Action lands in the engine's framework-private FIFO queue.
9. **Drain begins.** Pipeline runs: before-fold (no triggers) → `MoveCardHandler.Handle` → after-fold (no triggers) → envelope published.
10. **State mutations fire.** Handler called `ctx.Store.ExecuteBatch(...)` with the framework's `MoveCardMutation` (CompositeAction — handles `ZoneContentsState` + `CardMapState` rotation atomically) plus game-side payloads (`AdjustPileCounterPayload` × 2 for source/destination counters, `SetLastMovePayload` for the last-move memory). All committed atomically as one batch; one slice update event per affected slice.
11. **`runtime.Evaluate`** runs — checks if all piles match targets. If yes, returns `[EndMatchCommand]`; engine enqueues it. If not, the runtime publishes a fresh snapshot inside the same `Evaluate` call via `Actions.Set(...)` and returns `[]`.
12. **Drain reaches empty queue.** No `OnQueueDrained` callback (per [ADR-0006](decisions/0006-two-pipeline-architecture.md)); the snapshot was already published in step 11.
13. **Store fires `OnStateChanged`.** Loop returns to step 2.

---

## 11. Decisions Locked

- **Snapshot lives in state.** `GameState.AvailableActions: AvailableActionsSnapshot` (typed templates only — no scope discriminator). No separate channel, no push API.
- **Snapshot element type is typed `ActionTemplate` per command type.** Each command has a paired template; templates carry source + slots/modes/parameters. See [doc 28](features/actions/models/02-availability-snapshot.md), [doc 29 §3](features/engine/models/03-snapshot-pipeline.md).
- **Snapshot is written via the `IAvailableActions` service** by the runtime (typically from inside `Evaluate` when its own follow-ups are exhausted) or effects (mid-execution overrides). Engine never auto-publishes. Per [ADR-0006](decisions/0006-two-pipeline-architecture.md), publication is purely runtime-driven; there is no framework-mandated moment.
- **`AffordanceMap` is game-specific.** Lives in gateway/UI layer. Recomputed on every snapshot change. Pattern-matches typed templates.
- **View consumes AffordanceMap, not snapshot directly.** The view never touches raw templates or commands.
- **Intents are emitted at drag-end, not earlier.** Drag highlights come from AffordanceMap; the intent is built only when the player drops.
- **Validation has five layers.** Per-template eligibility (runtime) → AffordanceMap (prophylactic) → IntentMapper (well-formedness) → engine lifecycle gate (framework) → `handler.Validate` (authoritative legality).
- **`handler.Validate` at dispatch is the single source of truth for legality.** The snapshot is the runtime's best-effort approximation; per-binding `Validate` runs on demand only if the runtime opts in.

---

## 12. Snapshot Publication From `Evaluate`

Per [ADR-0006](decisions/0006-two-pipeline-architecture.md), there is no `OnQueueDrained` callback. The runtime publishes the snapshot from inside `Evaluate` whenever it deems appropriate — typically when its own state-based follow-ups are exhausted (drain about to end):

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

The snapshot write is a service call — see [doc 29 §2](features/engine/models/03-snapshot-pipeline.md). The runtime never holds a `GameEngine` reference; it holds narrow capability services. Effects mid-execution override the snapshot through the same write path (target pickers, choose-one prompts).

---

## 13. Caveats

This loop was designed against Pile Counter, which has no triggers, no async decisions, no phase transitions, and no game-end state beyond a simple win check. Richer games may surface additional concerns:

- **Async decisions** — when an effect awaits player input (target picker, choice overlay), the snapshot contains only the legal-response templates; the engine's `ICommandAwaiter` handles routing. The UI currently has no explicit "you must respond here" signal from the snapshot (scope discriminator was dropped S15 Q6); the template set itself conveys context.
- **Multiplayer / hidden info** — different players see different affordances. The snapshot may need scoping per actor.
- **Snapshot diffing** — for performance, the engine may compute snapshot deltas rather than full recomputes. Out of scope for now.
- **Animation timing** — the UI may want to defer rendering a new state until an animation completes. State updates fire immediately; the view needs an animation queue. Presentation concern, not framework.

These extend the framework's snapshot story; they don't break the four-layer model above.

---

## 14. Open Items

- ~~**`AvailableActions` mutator implementation**~~ — Resolved. Framework-ships an internal mutator behind the `IAvailableActions` service ([doc 29 §2](features/engine/models/03-snapshot-pipeline.md)).
- **`IIntentMapper` shape** — refined: `Map(ActionTemplate, PlayerChoice) → GameCommand` ([doc 29 §3](features/engine/models/03-snapshot-pipeline.md)). `PlayerChoice` shape (positional vs named, parameter typing) pin during implementation.
- **`IGameStore` shape** — sketched here as a presentation-side wrapper; final shape depends on the chosen state library (Scaffold State).
- **Snapshot revision tag** — useful for the UI to detect "I have a stale snapshot." Worth adding once we hit a concrete need.
- **FSM-shaped game state** — `FreeInput`/`DecisionPrompt` were dropped (S15 Q6). FSM-shaped state model is the eventual successor to type-only routing ([doc 29 §9](features/engine/models/03-snapshot-pipeline.md)).

---

## 15. Related

- [features/engine/models/02-gamerules-contract.md](features/engine/models/02-gamerules-contract.md) — `GameRules` interface
- [support/samples/pile-counter.md](support/samples/pile-counter.md) — sample game used as the working reference
- [features/engine/models/01-evaluation-loop.md](features/engine/models/01-evaluation-loop.md) — engine loop, drain cycle
- [features/effects/models/01-async-dispatch.md](features/effects/models/01-async-dispatch.md) — layer hierarchy, two dispatch paths
- [features/actions/models/02-availability-snapshot.md](features/actions/models/02-availability-snapshot.md) — snapshot shape (typed templates)
- [features/engine/models/03-snapshot-pipeline.md](features/engine/models/03-snapshot-pipeline.md) — service-driven snapshot publication
