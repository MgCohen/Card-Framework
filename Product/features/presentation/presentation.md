---
type: feature
status: settled
references: [features/actions/models/01-command-pipeline.md, features/actions/models/02-availability-snapshot.md, features/engine/models/03-snapshot-pipeline.md, features/effects/models/04-effect-flow.md]
tags: [#presentation]
---

# presentation — feature

## What it is

The reactive layer between game state and the player. Presentation **subscribes** to the engine's `AvailableActionsState` and `HistoryState` slices, **projects** typed `ActionTemplate`s into a game-specific **`AffordanceMap`**, **renders** UI from current state + affordances, **captures** drag/drop/click input, and **emits** `PlayerIntent`s back into the engine. The framework provides the snapshot shape and the dispatch path; the game ships the gateway, mapper, affordance projection, and view.

```
                Engine
                  │ (state mutations + envelopes)
                  ▼
            Store + GameState
            (incl. AvailableActionsState, HistoryState)
                  │ subscribe
                  ▼
        ┌─────────────────────────────────────────────┐
        │  Presentation layer                         │
        │                                             │
        │   AvailableActionsSnapshot                  │
        │           │                                 │
        │           ▼                                 │
        │   AffordanceMap (game-specific projection)  │
        │           │                                 │
        │           ▼                                 │
        │   View (renders + captures gestures)        │
        │           │                                 │
        │           ▼                                 │
        │   PlayerIntent                              │
        └─────────────────────────────────────────────┘
                  │
                  ▼
            IntentGateway → IIntentMapper → engine.Dispatch
```

The boundary is bi-directional: state and history flow down (server → client semantics), intents flow up (client → server semantics). The same loop runs locally and over the network — the only difference is that the network transport applies a per-actor visibility filter on the way down.

## Vocabulary

**Feature-owned terms only.** Cross-feature terms used by this feature — `PlayerIntent`, `IIntentMapper`, `PlayerChoice`, `ActionTemplate`, `AvailableActionsSnapshot`, `IntentGateway`, `GameCommand`, `CommandResult`, `HistoryEnvelope` (defined in `actions/`); `IAvailableActions`, `AvailableActionsState`, `HistoryState`, `EngineState`, `IStateScope`, `Store`, `GameRulesRuntime` (defined in `engine/`); `Effect`, `IEffectScope`, `ICommandAwaiter` (defined in `effects/`); `Card`, `CardRef`, `Zone`, `Reference`, `Ref<T>`, `IActor`, `ActorId` — live in their owning features or `Product/ledgers/GLOSSARY.md`; link to their canonical home, don't restate.

| Term | Meaning |
|---|---|
| `AffordanceMap` | Game-specific projection of `AvailableActionsSnapshot` into UI-friendly shape (e.g., `Dictionary<Ref<Card>, IReadOnlyList<Ref<Zone>>>` for "card → valid drop targets"). Pure projection; no additional state; recomputed on every snapshot change. |
| `View` | Renders UI from current state and the `AffordanceMap`; captures input gestures; emits `PlayerIntent`s. Game-supplied (Unity scenes, web components, terminal renderer, etc.). |
| Presentation loop | The end-to-end cycle: state change → snapshot subscribe → AffordanceMap recompute → render → input → intent → engine.Dispatch → state change. |
| Per-actor visibility filter | Transport-boundary mechanism that cuts data the actor isn't allowed to see (per [`Product/decisions/0008-zone-visibility-model.md`](Product/decisions/0008-zone-visibility-model.md)). All observers see the same canonical `CardRef`; the filter cuts the data behind the ref. |

## Architecture

The feature has one Model. See `models/01-presentation-loop.md` for the four-layer model (Store → IntentMapper → AffordanceMap → View), snapshot-driven reactive flow, presentation-side validation layering (prophylactic, not authoritative), and transport-boundary visibility filter responsibility.

## Composition

| Model | Role |
|---|---|
| [`models/01-presentation-loop.md`](models/01-presentation-loop.md) | The four-layer loop (Store / IntentMapper / AffordanceMap / View); snapshot-driven reactive flow; validation layering on the presentation side; transport-boundary visibility filter |

## Invariants

Cross-Model rules that hold across the Model and shape how other features must interact with presentation.

- **Snapshot is canonical state, not side-channel.** Presentation reads `AvailableActionsState` from the Store via subscription. No separate channel to wire.
- **Validation is layered, with `handler.Validate` as authoritative.** The snapshot is best-effort; UI is prophylactic. Per-binding `Validate` runs at dispatch always. UI rejection feedback (toast, inline error) is presentation-side; the engine surfaces only the dispatch result + reason.
- **The View never consumes raw `ActionTemplate`s.** It always projects through the game-specific `AffordanceMap`. Templates are wire-form; affordances are UI-form.
- **Intents are atomic at drop-time.** Drag highlights come from the `AffordanceMap` (cheap to query); the `PlayerIntent` is built only when the player commits to the gesture.
- **Per-actor visibility filtering happens at the transport boundary, server-side.** All observers see the same canonical `CardRef`; the filter cuts data, never identity. Privacy across reveal-then-hide cycles uses server-side ref rotation (per [ADR-0008](Product/decisions/0008-zone-visibility-model.md)).

## Boundaries

**Owns:** the presentation loop (Store subscription → AffordanceMap → View); the `AffordanceMap` pattern (game-specific projection of `ActionTemplate`s); the `View` contract (renders state + affordances, emits `PlayerIntent`s); the transport-boundary visibility filter (server-side, cuts data per actor); presentation-side validation feedback (toast, inline error rendering).

**Does not own:**
- `PlayerIntent` shape, `IIntentMapper` contract, `IntentGateway` → `actions/`
- `AvailableActionsSnapshot` shape, `ActionTemplate` typed subtypes → `actions/` and `engine/`
- `IAvailableActions` service (writer) → `engine/`
- Engine pipeline, `GameRulesRuntime` → `engine/`
- Effect orchestration, decision routing → `effects/`
- Card/zone/entity shapes → `cards/`, `zones/`, `entities/`
- Visibility *policy* (lives on `ZoneDefinition`) → `zones/`
- Visibility *mechanism* (CardRef rotation, per-card flag) → `cards/` (per [ADR-0008](Product/decisions/0008-zone-visibility-model.md))
- Animation orchestration / scene graph (game-specific implementation detail)
- Input device handling, gesture recognition (game-supplied via the `View`)

## Dependencies

*Mirrors the front-matter `references:` array in human-readable form (per §3).*

- **Upstream:** `actions/` (`PlayerIntent` and `IIntentMapper` contracts; `AvailableActionsSnapshot` shape), `engine/` (`AvailableActionsState`, `HistoryState`, `EngineState` slices; `Store` subscription mechanism), `effects/` (`DecisionOpenedSignal` / `DecisionResolvedSignal` for decision UI rendering), `cards/`, `zones/`, `entities/` (consumed via card-keyed / zone-keyed state subscriptions).
- **Downstream:** game-specific card prefabs, UI scenes, terminal/web/native renderers (none of these are framework-owned).

## Open

See `ledgers/OPEN-ITEMS.md`:

- **`bl-interaction-feature`** — `interaction/` feature for the gesture-to-intent layer (between presentation gestures and `PlayerIntent`). Currently the `View` emits intents directly; a dedicated interaction layer would normalize gesture handling across renderers.
- **Snapshot revision tag** — useful for the UI to detect "I have a stale snapshot." Worth adding once we hit a concrete need.
- **Animation timing** — the UI may want to defer rendering a new state until an animation completes. State updates fire immediately; the View needs an animation queue. Presentation-implementation concern; the framework provides the state events, not the queue.
- **Multiplayer / hidden info per-actor affordances** — different players see different affordances. The snapshot may need scoping per actor in multi-actor games. Currently global; pin when multiplayer lands.
- **Async-decision UI signal** — the snapshot dropped its scope discriminator (per `engine/03-snapshot-pipeline.md` §Snapshot shape); "you must respond here" is conveyed by the template set itself. If a richer "decision-active" UI signal becomes obviously needed, add it (likely as part of the FSM-shaped game state successor to type-only routing).
