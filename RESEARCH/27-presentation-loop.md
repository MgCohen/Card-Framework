# Presentation Loop and Available Actions Flow

This document captures the end-to-end loop from engine to UI and back, with a focus on how player-facing affordances are computed, surfaced, and acted upon. It came out of sketching the Pile Counter sample game (doc 26) against the `GameRules` interface (doc 25) — the snapshot's shape and publication mechanism couldn't be settled in isolation, so we worked through the full presentation cycle.

Builds on:

- [25-gamerules-shape.md](25-gamerules-shape.md) — `GameRules` four-method interface, `OnQueueDrained`, snapshot ownership question
- [26-pile-counter-sample-game.md](26-pile-counter-sample-game.md) — concrete sample game used to ground the design
- [22-async-decisions-and-suspension.md](22-async-decisions-and-suspension.md) — layer hierarchy

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
    public IReadOnlyList<Pile>           Piles            { get; }
    public LastMove?                     LastMove         { get; }
    public IReadOnlyList<GameCommand>    AvailableActions { get; }   // canonical, generic
}
```

Treating the snapshot as state — not as a side-channel — has three consequences:

1. **Reactive UI for free.** The UI subscribes to state; snapshot updates flow through the same path as any other state change. No separate channel to wire.
2. **Replays carry it.** Snapshots restore as part of state, useful for debugging "why did the UI show this affordance?"
3. **Generic across games.** `IReadOnlyList<GameCommand>` works for any game; framework ships this shape.

---

## 5. The Snapshot's Element Type

The snapshot stores **fully resolved `GameCommand`s**, not intents or game-specific affordances.

Reasoning:

- `GameCommand`s are the canonical, framework-level vocabulary. The pipeline, history, and snapshot all speak the same language — entries in the snapshot match the shape of envelopes in history.
- Intents are lossier (they elide things like `From` that are derivable from state). Snapshot consumers may want the full resolution.
- Game-specific affordance shapes belong in the UI layer, not in state. State is generic; the projection happens at the boundary.

Trade-off accepted: the UI never reads `MoveCardCommand` directly. It reads its game-specific `AffordanceMap`, which is built from the snapshot once per state change.

---

## 6. AffordanceMap (Game-Specific)

The `AffordanceMap` is the UI's actual reading surface. It's built from the snapshot, lives in the gateway/UI layer, and is recomputed whenever `AvailableActions` changes.

```csharp
public sealed class PileCounterAffordances
{
    private readonly Dictionary<InstanceId, IReadOnlyList<PileId>> moveTargets;

    public PileCounterAffordances(IReadOnlyList<GameCommand> snapshot)
    {
        moveTargets = snapshot
            .OfType<MoveCardCommand>()
            .GroupBy(c => c.Card)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<PileId>)g.Select(c => c.To).ToList());
    }

    public bool CanDrag(InstanceId card)
        => moveTargets.ContainsKey(card);

    public IReadOnlyList<PileId> ValidDrops(InstanceId card)
        => moveTargets.GetValueOrDefault(card, Array.Empty<PileId>());
}
```

**Properties:**
- Pure projection of `AvailableActions`. No additional state.
- Game-specific — Pile Counter's affordance shape is "card → valid destinations." Other games will have completely different shapes (target selectors, choice prompts, etc.).
- Recomputed on every snapshot change. For Pile Counter (~12 cards, few legal moves), trivial. For larger games, can be optimized by diffing or caching, but that's a presentation concern.

---

## 7. View (Renders + Captures Input)

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
        affordances = new PileCounterAffordances(store.State.AvailableActions);
        Render();
    }

    private void Render()
    {
        foreach (var pile in store.State.Piles)
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

---

## 8. Store / Gateway

```csharp
public sealed class GameStore : IGameStore
{
    private readonly GameEngine engine;
    private readonly IIntentMapper intentMapper;

    public GameState State { get; private set; }
    public event Action OnStateChanged;

    public IntakeResult SubmitIntent(PlayerIntent intent)
    {
        var command = intentMapper.Map(intent);
        var result  = engine.Dispatch(command, DispatchOrigin.PlayerIntent);

        if (!result.Accepted)
            ShowToast(result.Reason);

        return result;
    }
}
```

The store is the boundary between presentation and engine. It owns the `IntentMapper` and exposes a single `SubmitIntent` entry point.

---

## 9. Validation Layers

Validation runs at multiple layers; each has a different purpose.

| Layer | What it answers | Failure mode |
|---|---|---|
| **`AffordanceMap.CanDrag` / `ValidDrops`** (UI) | "Is this drag visually allowed?" | UI silently disallows — player sees no error |
| **`IntentMapper.Map`** (gateway) | "Can this intent be resolved into a command?" | Throws or returns null — programmer error if UI emitted a bogus intent |
| **`handler.Validate`** (engine, intake) | "Is this command legal in the current state?" | `IntakeResult.Reject(reason)` — gateway shows toast |
| **`rule.OnIncomingCommand`** (engine, intake) | "Is the engine accepting commands right now?" | `IntakeResult.Reject(reason)` — paused, ended, etc. |

The relationship between AffordanceMap and `handler.Validate`:

- They aren't duplicated logic. The snapshot was built by *running* `handler.Validate` against every candidate command. The AffordanceMap is a projection of that same answer.
- AffordanceMap is **prophylactic** — prevents bad intents from being sent.
- `handler.Validate` is **authoritative** — single source of truth, catches stale-state cases (state changed between affordance computation and intent dispatch).

---

## 10. End-to-End Flow Trace

A player makes a move in Pile Counter:

1. **Engine idle.** Queue empty after last drain. `OnQueueDrained` fired; snapshot was written to `state.AvailableActions`.
2. **Store fires `OnStateChanged`.** View receives event.
3. **View recomputes AffordanceMap.** `MoveCardCommand`s in the snapshot grouped by `Card`. UI re-renders; only draggable cards show drag handles.
4. **Player drags a card.** `OnDragStart` highlights valid drop piles from the affordance map.
5. **Player releases over a target pile.** `OnDrop` validates against affordance map (defensive), emits `MoveCardIntent(card, destination)`.
6. **Store receives intent.** Calls `IntentMapper.Map(intent)` → `MoveCardCommand(card, from, to)`.
7. **Store calls `engine.Dispatch(command, PlayerIntent)`.**
8. **Engine intake pipeline:**
   1. `handler.Validate(command, state, out reason)` — passes (UI prevented illegal intents).
   2. `rule.OnIncomingCommand(command, ctx)` — Pile Counter's rule trivially `AddLast`s the command, returns `Ok`.
9. **Drain begins.** Pipeline runs: before-fold (no triggers) → `MoveCardHandler.Handle` → after-fold (no triggers) → envelope published.
10. **State mutators fire.** `MoveCardBetweenPiles` mutator updates pile contents; `LastMoveMutator` writes `state.LastMove`.
11. **`rule.Evaluate`** runs — checks if all piles match targets. If yes, returns `[EndGameCommand]`; engine dispatches it next.
12. **Queue drains.** `rule.OnQueueDrained` runs, computes new snapshot (calling `handler.Validate` against every candidate), engine writes to `state.AvailableActions`.
13. **Store fires `OnStateChanged`.** Loop returns to step 2.

---

## 11. Decisions Locked

- **Snapshot lives in state.** `GameState.AvailableActions: IReadOnlyList<GameCommand>`. No separate channel, no push API. Engine writes via mutator after each drain.
- **Snapshot element type is `GameCommand`.** Canonical, generic, framework-defined. Mirrors history.
- **`AffordanceMap` is game-specific.** Lives in gateway/UI layer. Recomputed on every snapshot change.
- **View consumes AffordanceMap, not snapshot directly.** The view never touches raw commands.
- **Intents are emitted at drag-end, not earlier.** Drag highlights come from AffordanceMap; the intent is built only when the player drops.
- **Validation has four layers.** AffordanceMap (prophylactic) → IntentMapper (well-formedness) → `handler.Validate` (authoritative legality) → `rule.OnIncomingCommand` (engine flow).
- **`handler.Validate` is the single source of truth for legality.** The snapshot is built by running it; AffordanceMap is a projection of its answers.

---

## 12. `OnQueueDrained` Output Shape

To keep the rule pure (returns data, never side-effects), `OnQueueDrained` returns a result rather than dispatching directly:

```csharp
public sealed record DrainResult(
    IReadOnlyList<GameCommand> Snapshot,    // legal player actions
    IReadOnlyList<GameCommand> Dispatch);   // follow-ups (phase advance, end-game check)

public interface GameRules
{
    DrainResult OnQueueDrained(GameState state);
    // … other methods …
}
```

Engine processes the result:
- Writes `Snapshot` into `state.AvailableActions` via internal mutator.
- Dispatches each `Dispatch` command.

This unifies the rule's interface around "observer returning data" — `Evaluate` returns commands, `OnQueueDrained` returns commands plus a snapshot, `FilterTriggers` returns a filtered list, `OnIncomingCommand` returns an intake result. The rule never holds a `GameEngine` reference.

(This supersedes doc 25 §6, which had `OnQueueDrained` taking `GameEngine` and dispatching directly. Doc 25 will be amended to match.)

---

## 13. Caveats

This loop was designed against Pile Counter, which has no triggers, no async decisions, no phase transitions, and no game-end state beyond a simple win check. Richer games may surface additional concerns:

- **Async decisions** — when an effect awaits player input (target picker, choice overlay), the affordance shape may need to express "the player must respond *here*" vs. "the player has free input." Currently AffordanceMap is purely about free input.
- **Multiplayer / hidden info** — different players see different affordances. The snapshot may need scoping per actor.
- **Snapshot diffing** — for performance, the engine may compute snapshot deltas rather than full recomputes. Out of scope for now.
- **Animation timing** — the UI may want to defer rendering a new state until an animation completes. State updates fire immediately; the view needs an animation queue. Presentation concern, not framework.

These extend the framework's snapshot story; they don't break the four-layer model above.

---

## 14. Open Items

- **`AvailableActions` mutator implementation** — does the engine ship this internally, or does each game wire it? (Probably framework-shipped, since the snapshot type is canonical.)
- **`IIntentMapper` shape** — out of scope here; deferred to action-pipeline cleanup (checkpoint #5).
- **`IGameStore` shape** — sketched here as a presentation-side wrapper; final shape depends on the chosen state library (Scaffold State).
- **Snapshot revision tag** — useful for the UI to detect "I have a stale snapshot." Worth adding once we hit a concrete need.

---

## 15. Related

- [25-gamerules-shape.md](25-gamerules-shape.md) — `GameRules` interface (will be amended for `DrainResult`)
- [26-pile-counter-sample-game.md](26-pile-counter-sample-game.md) — sample game used as the working reference
- [20-engine-evaluation-loop.md](20-engine-evaluation-loop.md) — engine loop, drain cycle
- [22-async-decisions-and-suspension.md](22-async-decisions-and-suspension.md) — layer hierarchy, two dispatch paths
