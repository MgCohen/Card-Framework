---
type: feature
status: abandoned
archived_on: 2026-10-18
reason: "Written for the old state model (slices, Ref<T>, MatchStateSlice) and never built. Read it for the open questions, not as the contract; the replacement design comes from Product/spikes/framework-roadmap.md."
references: [features/presentation/archive/01-match-state-view-model.md, features/presentation/archive/02-view-model-registry.md, features/engine/archive/07-match-state.md, features/actions/archive/01-command-pipeline.md, features/effects/archive/06-request-system.md, decisions/0012-presentation-per-state-view-models.md, decisions/0009-engine-as-fsm.md]
tags: [#presentation]
---

> **Archived 2026-10-18.** Written for the old state model (slices, Ref<T>, MatchStateSlice) and never built. Read it for the open questions, not as the contract; the replacement design comes from Product/spikes/framework-roadmap.md.

# presentation — feature

## What it is

The reactive layer between game state and the player. Per [ADR-0012](Product/decisions/0012-presentation-per-state-view-models.md), presentation is organized around **per-state polymorphic ViewModels**: each `MatchState` subtype (per [ADR-0009](Product/decisions/0009-engine-as-fsm.md)) has a typed `MatchStateViewModel<TState>` that **binds** to the typed state record (sets up observables / view bindings) and **maps** player gestures (`PlayerIntent`) to `GameCommand`s. A `ViewModelRegistry` maps state types to VM factories; the presenter subscribes to `MatchStateSlice` and rebinds the active VM on every state-type change. The pre-cascade `AffordanceMap` projection + global `IIntentMapper` + `AvailableActionsSnapshot` wire form retire — the VM's own observables are the affordance surface; the VM's `Map(intent)` is the gesture-to-command translation.

```
                Engine
                  │ (state mutations + envelopes)
                  ▼
            Store + GameState
            (incl. MatchStateSlice, RequestsSlice, HistoryState)
                  │ subscribe
                  ▼
        ┌─────────────────────────────────────────────────────┐
        │  Presentation layer                                 │
        │                                                     │
        │   Presenter subscribes to MatchStateSlice           │
        │           │                                         │
        │           │ on state-type change                    │
        │           ▼                                         │
        │   ViewModelRegistry.Create(state)                   │
        │           │                                         │
        │           ▼                                         │
        │   MatchStateViewModel<TState> (typed)               │
        │     • Bind(state, scope) — read typed state,        │
        │       set up observables                            │
        │     • observables expose UI affordances             │
        │     • Map(intent) → GameCommand                     │
        │           │                                         │
        │           ▼                                         │
        │   View (renders + captures gestures)                │
        │           │ raw input                               │
        │           ▼                                         │
        │   IntentGateway: gesture → typed PlayerIntent       │
        │           │                                         │
        │           ▼                                         │
        │   ActiveVM.Map(intent) → GameCommand                │
        └─────────────────────────────────────────────────────┘
                  │
                  ▼
              engine.Dispatch
```

The boundary is bi-directional: state and history flow down (server → client semantics), intents flow up (client → server semantics). The same loop runs locally and over the network — the only difference is that the network transport applies a per-actor visibility filter on the way down.

## Vocabulary

**Feature-owned terms only.** Cross-feature terms used by this feature — `GameCommand`, `GameAction`, `Response` (defined in `actions/` / `triggers/`); `MatchState`, `MatchStateSlice`, `SessionState`, `RequestsSlice`, `Request<TResponse>` (defined in `engine/` / `effects/`); `IStateScope`, `Store`, `HistoryState`, `EngineState` (defined in `state/` / `engine/`); `Effect`, `IEffectScope` (defined in `effects/`); `Card`, `CardRef`, `Zone`, `Reference`, `Ref<T>`, `IActor`, `ActorId` — live in their owning features or `Product/ledgers/GLOSSARY.md`; link to their canonical home, don't restate.

| Term | Meaning |
|---|---|
| `MatchStateViewModel<TState>` | Per-state polymorphic ViewModel. Typed abstract base; concrete game-defined subclasses per `MatchState` subtype carry `Bind(state, scope)` (set up observables from typed state) + `Map(intent) → GameCommand` (translate gestures). Canonical home: [`01-match-state-view-model.md`](features/presentation/archive/01-match-state-view-model.md). |
| `ViewModelRegistry` | Sealed lookup mapping `MatchState` types to VM factories. Presenter calls `Create(state)` on every state-type change. Many-to-one supported (two state types sharing one VM). Canonical home: [`02-view-model-registry.md`](features/presentation/archive/02-view-model-registry.md). |
| `PlayerIntent` | Game-defined abstract base for typed gesture records (`TapCardIntent`, `DonePressedIntent`, etc.). The boundary between the gesture system and the active VM's `Map`. Framework provides the marker; concrete intents are game-shipped. Pre-cascade lived in `actions/`; ADR-0012 moves ownership here. |
| `IntentGateway` | Thin entry layer translating raw device gestures (mouse, touch, controller) into typed `PlayerIntent` records, then routing through the active VM (looked up via `ViewModelRegistry` keyed on `MatchStateSlice.Current.GetType()`). Game-supplied; framework provides the contract through `MatchStateViewModel<TState>` + `ViewModelRegistry`. |
| `View` | Renders UI from the active VM's observables (the affordance surface); captures input gestures and feeds them to the gateway. Game-supplied (Unity scenes, web components, terminal renderer, etc.). |
| Presentation loop | The end-to-end cycle: state-type change → registry creates new VM → typed `Bind` runs → view re-binds via template selection → input gesture → typed `PlayerIntent` → active VM's `Map` → `GameCommand` → `engine.Dispatch` → state change → loop. |
| Request-mode | When `RequestsSlice.Open` (per [`effects/06-request-system.md`](features/effects/archive/06-request-system.md)) has an open `Request<TResponse>` targeting the VM's actor, the same `Map(intent)` returns a `Response` instead of a normal `GameAction`. Same surface; different return type from the same `GameCommand` base. |
| Per-actor visibility filter | Transport-boundary mechanism that cuts data the actor isn't allowed to see (per [`Product/decisions/0008-zone-visibility-model.md`](Product/decisions/0008-zone-visibility-model.md)). All observers see the same canonical `CardRef`; the filter cuts the data behind the ref. |

**Retired terms** (live in `Product/ledgers/GLOSSARY.md` §Retired terms):

- `AffordanceMap` — UI-side projection from `AvailableActionsSnapshot`. Replaced by VM-exposed observables (the VM is the projection).
- `IIntentMapper` (global) — single game-supplied switch over every `ActionTemplate`. Replaced by per-VM `Map(intent)`.
- `AvailableActionsSnapshot` + `ActionTemplate` (wire form) — typed-descriptor wire format. Replaced by `MatchState.Actions: IReadOnlyList<Type>` (intake gate, per ADR-0009) + per-state VM observables (rich projection).
- `PlayerChoice` — separate choice-data record paired with `ActionTemplate`. Replaced — the choice is encoded in the typed intent record (`TapCardIntent.Card`, `PickOptionIntent.OptionIndex`, etc.).

## Architecture

The feature has two Models post-ADR-0012. Each is the canonical home for one load-bearing C# type:

1. **`MatchStateViewModel<TState>`** ([`01-match-state-view-model.md`](features/presentation/archive/01-match-state-view-model.md)) — the per-state polymorphic VM base; typed `Bind` + `Map`; lifecycle (rebind on type change, observable subscriptions for same-type updates); request-mode integration; UI prophylactic role in the five-validation-layer model.
2. **`ViewModelRegistry`** ([`02-view-model-registry.md`](features/presentation/archive/02-view-model-registry.md)) — sealed type → factory lookup; builder pattern for game-side composition; presenter subscription wiring; many-to-one support.

The execution arc: the presenter (host-side glue) subscribes to `MatchStateSlice` (per [`engine/07-match-state.md`](features/engine/archive/07-match-state.md)); on every state-type change it asks the registry for a new VM, disposes the old one, sets `CurrentVM` (the view re-binds via standard MVVM template selection). The view exposes the active VM's observable properties as the affordance surface — no intermediate `AffordanceMap`. Raw device gestures pass through an `IntentGateway` that produces typed `PlayerIntent` records; the active VM's `Map(intent)` translates them to `GameCommand`s; the gateway calls `engine.Dispatch`. In request-mode (per [`effects/06-request-system.md`](features/effects/archive/06-request-system.md)), the same `Map` returns a `Response` instead.

The pre-cascade four-layer model (Store / IntentMapper / AffordanceMap / View) survives in spirit — separation between state / projection / view — but the projection step changes from `AffordanceMap (game-specific)` to per-state VM observables. The canonical pre-cascade Model is archived at [`01-presentation-loop.md`](features/presentation/archive/01-presentation-loop.md).

## Composition

| Model | Role |
|---|---|
| [`01-match-state-view-model.md`](features/presentation/archive/01-match-state-view-model.md) | `MatchStateViewModel<TState>` — typed abstract VM base; `Bind` + `Map`; request-mode integration |
| [`02-view-model-registry.md`](features/presentation/archive/02-view-model-registry.md) | `ViewModelRegistry` — type → factory lookup; builder pattern; presenter wiring |
| ~~[`01-presentation-loop.md`]~~ | **Archived 2026-05-11** — pre-cascade four-layer loop (Store / IntentMapper / AffordanceMap / View). Superseded by ADR-0012. |

## Invariants

Cross-Model rules that hold across both Models and shape how downstream features must interact with presentation.

- **One VM type per `MatchState` subtype.** Each game-defined `MatchState` subclass has a registered VM factory in the `ViewModelRegistry`. Many-to-one is supported via separate registry entries pointing at the same factory.
- **Presenter rebinds on state-type change.** `MatchStateSlice.Current.GetType()` changes → dispose active VM → registry.Create → bind new VM. Same-type updates do NOT rebind; the active VM observes them via its own observable subscriptions set up in `Bind`.
- **VM observables are the affordance surface.** No separate `AffordanceMap`. The view binds to the VM's observable properties via standard MVVM template selection.
- **VM `Map` is mode-scoped.** Per-state typed `switch` on `PlayerIntent` subtypes. Unhandled types throw `InvalidOperationException` (or return a no-op `GameCommand` — author's choice; throwing is the default for early programmer-error detection).
- **Request-mode takes precedence in `Map`.** When `RequestsSlice.Open` has an open `Request<TResponse>` for the VM's actor, `Map` returns a `Response` matching the open request's type; in-state intents are ignored or routed appropriately.
- **VM is prophylactic, not authoritative.** Authoritative legality is the framework's validator chain at dispatch (per ADR-0010 if landed, or `handler.Validate` pre-cascade). The VM shouldn't surface a UI affordance that would be rejected, but stale UI state is caught by validator-side checks; the VM doesn't claim final authority.
- **The View consumes the VM, not state directly.** Snapshot-driven rendering goes through the VM. Game-side bypass paths to the Store are anti-patterns.
- **Intents are emitted at gesture-commit.** Drag highlights come from VM observables (cheap to query); the typed `PlayerIntent` is built only when the player commits to the gesture.
- **Per-actor visibility filtering happens at the transport boundary, server-side.** All observers see the same canonical `CardRef`; the filter cuts data, never identity. Privacy across reveal-then-hide cycles uses server-side ref rotation (per [ADR-0008](Product/decisions/0008-zone-visibility-model.md)).

## Boundaries

**Owns:** the per-state polymorphic ViewModel pattern (`MatchStateViewModel<TState>`); the `ViewModelRegistry` lookup + builder; presenter subscription wiring (host-side glue, but framework provides the contract); `PlayerIntent` marker base + typed-intent-record convention; `IntentGateway` contract (gesture → typed intent → VM.Map → dispatch); the transport-boundary visibility filter (server-side, cuts data per actor); presentation-side validation feedback (toast, inline error rendering).

**Does not own:**
- `MatchState` shape, `MatchStateSlice` slice, `Actions: IReadOnlyList<Type>` intake gate → `engine/`
- `GameCommand` / `GameAction` / `Response` type hierarchy → `triggers/` / `effects/`
- `Request<TResponse>` / `RequestsSlice` / `AnswersSlice` request system → `effects/`
- Engine pipeline, `GameRulesRuntime` → `engine/`
- Effect orchestration, `IEffectScope.Dispatch` → `effects/`
- Validator chain (intake gates + game-shipped legality) → `actions/` (per ADR-0010 if landed)
- Card / zone / entity shapes → `cards/` / `zones/` / `entities/`
- Visibility *policy* (lives on `ZoneDefinition`) → `zones/`
- Visibility *mechanism* (CardRef rotation, per-card flag) → `cards/` (per [ADR-0008](Product/decisions/0008-zone-visibility-model.md))
- Animation orchestration / scene graph (game-specific implementation detail)
- Input device handling, gesture recognition (game-supplied via the `IntentGateway` / `View`)
- MVVM toolkit choice, observable library, `ViewModel` framework base class (host-shape per `oq-view-model-host-shape`)

## Dependencies

*Mirrors the front-matter `references:` array in human-readable form (per §3).*

- **Upstream:** `engine/` (`MatchStateSlice`, `MatchState`, `Store` subscription mechanism), `actions/` (`GameCommand` type hierarchy, dispatch pipeline), `effects/` (`RequestsSlice` for request-mode mapping, `Response` as one of the `GameCommand` siblings), `cards/` / `zones/` / `entities/` (consumed via state subscriptions).
- **Downstream:** game-specific card prefabs, UI scenes, terminal/web/native renderers (none of these are framework-owned).

## Open

See `ledgers/OPEN-ITEMS.md`:

- **`oq-view-model-host-shape`** — framework `ViewModel` base class shape (animation hooks, observable wiring, disposal). Pin when the host MVVM toolkit is chosen.
- **`bl-interaction-feature`** — `interaction/` feature for the gesture-to-intent layer (raw device input → typed `PlayerIntent`). Currently the host wires this; a dedicated interaction feature would normalize across renderers.
- **Multi-actor concurrent VMs** — today's design is one-VM-active-at-a-time per presenter. Multi-actor games (simultaneous mulligans, parallel target picks) need a per-actor presenter + per-actor `RequestsSlice` query helpers. Pin when multiplayer lands.
- **VM caching** — every state-type change resolves a fresh VM. For games with frequent transitions in/out of the same state type, caching might be cheaper. Pin if profile shows allocation pressure.
- **Wildcard / inheritance-walking registry lookup** — today's exact-type match keeps the surface small. If real games need a "fallback for any `SetupPhaseState` subtype" pattern, add a configurable second-pass walk. Defer until forced.
- **Snapshot revision tag** — useful for the UI to detect "I have a stale snapshot." With per-state VMs the question reshapes — VM observables carry state; revision tags would be per-observable. Pin if drift between view and store becomes a real concern.
- **Animation timing** — the UI may want to defer rendering a new state until an animation completes. State updates fire immediately; the View needs an animation queue. Presentation-implementation concern; the framework provides the state events, not the queue.
