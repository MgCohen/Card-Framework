---
type: model
status: settled
references: [features/presentation/models/02-view-model-registry.md, features/presentation/presentation.md, features/engine/models/07-match-state.md, features/actions/models/01-command-pipeline.md, features/effects/models/06-request-system.md, features/state/state.md, decisions/0012-presentation-per-state-view-models.md, decisions/0009-engine-as-fsm.md, decisions/0007-five-validation-layers.md]
tags: [#presentation, #view-model, #fsm, #intent, #model]
---

# Match State View Model — model

## What it is

`MatchStateViewModel<TState>` is the **per-state polymorphic ViewModel**: a typed abstract class authored once per `MatchState` subtype, carrying `Bind(state, scope)` (read typed state + set up observables) and `Map(intent) → GameCommand` (translate gestures to commands). Per [ADR-0012](Product/decisions/0012-presentation-per-state-view-models.md), each match phase / decision mode / cleanup state has its own VM; the presenter subscribes to `MatchStateSlice` and rebinds the active VM on every state-type change via [`ViewModelRegistry`](features/presentation/models/02-view-model-registry.md).

```
                  Store / MatchStateSlice.Current
                              │
                              │ state-type change
                              ▼
                  ViewModelRegistry.Create(state)
                              │
                              ▼
                  MatchStateViewModel<TState> (typed)
                  ├─ Bind(state, scope) ───────────▶ observables set up
                  │                                   view binds via MVVM template selection
                  │
                  └─ Map(intent) ──────────────────▶ GameCommand (or Response in request-mode)
                                                    engine.Dispatch
```

## Contracts

### `MatchStateViewModel<TState>` — the typed base

```csharp
public abstract class MatchStateViewModel<TState> : ViewModel
    where TState : MatchState
{
    public abstract void        Bind (TState state, IStateScope scope);
    public abstract GameCommand Map  (PlayerIntent intent);
}
```

- **`Bind(state, scope)`** — called once when the FSM transitions *into* this state. The presenter resolves the typed `TState` via the `ViewModelRegistry`'s factory; the VM reads typed phase-specific fields directly (no cast). The `IStateScope` is the wider read-only state surface — the VM can read sibling slices (`HandState`, `ZoneContentsState`, `RequestsSlice`, etc.) to populate observables. Idempotent for testing.
- **`Map(intent)`** — translates a `PlayerIntent` (game-defined typed gesture record) into a `GameCommand`. Most often returns a `GameAction`; in request-mode (a `Request<TResponse>` open for the VM's actor per [ADR-0011 — *forthcoming*]/landed via the request system), returns a `Response`. Throws `InvalidOperationException` for intent types the VM doesn't recognize in its current mode.

The framework provides the abstract base. Game authors derive concrete VMs typed to specific `MatchState` subtypes:

```csharp
public sealed class DeclareAttackersViewModel : MatchStateViewModel<DeclareAttackersState>
{
    private readonly ObservableProperty<IReadOnlyList<Ref<Card>>> selectedAttackers = new();

    public IObservable<IReadOnlyList<Ref<Card>>> SelectedAttackers => selectedAttackers;

    public override void Bind(DeclareAttackersState state, IStateScope scope)
    {
        selectedAttackers.Value = state.SelectedAttackers;
        // …additional observable setup (valid targets, highlight masks, etc.)
    }

    public override GameCommand Map(PlayerIntent intent) => intent switch
    {
        TapCardIntent     t => new SelectAttackerCommand   (t.Card),
        UntapCardIntent   u => new DeselectAttackerCommand (u.Card),
        DonePressedIntent   => new CommitDeclarationCommand(),
        _ => throw new InvalidOperationException($"Unhandled intent: {intent.GetType().Name}"),
    };
}
```

The `ViewModel` base class (animation hooks, observable wiring, disposal) is the host-side MVVM toolkit's concern; the framework only pins the `Bind` + `Map` contract. Tracked as `oq-view-model-host-shape`.

### `PlayerIntent` — game-defined typed gesture base

`PlayerIntent` is the abstract base for game-defined gesture records the active VM dispatches on. Each game ships its own intent set; concrete records carry typed per-gesture data:

```csharp
public abstract record PlayerIntent;

// Game-defined examples:
public sealed record TapCardIntent      (Ref<Card> Card)        : PlayerIntent;
public sealed record UntapCardIntent    (Ref<Card> Card)        : PlayerIntent;
public sealed record PickOptionIntent   (int OptionIndex)       : PlayerIntent;
public sealed record DonePressedIntent  ()                      : PlayerIntent;
public sealed record CancelPressedIntent()                      : PlayerIntent;
```

`PlayerIntent` ownership lives at the presentation feature-root vocabulary level (per [`features/presentation/presentation.md`](features/presentation/presentation.md)) — it's the boundary between the gesture system and the per-state VM's `Map`. Pre-cascade, `PlayerIntent` lived in `actions/`; ADR-0012 moves it here.

The pre-cascade `PlayerChoice` separate-data record retires — the choice is encoded in the typed intent record (`TapCardIntent.Card`, `PickOptionIntent.OptionIndex`, etc.).

### Request-mode `Map`

When `RequestsSlice.Open` (per [ADR-0011 — *forthcoming*]/landed; see [`features/effects/models/06-request-system.md`](features/effects/models/06-request-system.md)) has an open `Request<TResponse>` targeting the VM's actor, the same `Map` returns a `Response` instead of a normal `GameAction`. The `GameCommand` return type covers both — `Response` is one of the five `GameCommand` siblings per the request system.

```csharp
public override GameCommand Map(PlayerIntent intent)
{
    var open = scope.Get<RequestsSlice>().Open;
    var pending = open.Values.FirstOrDefault(r => r.Asker.Id == actorId);

    if (pending is TargetRequest tr)
    {
        return intent switch
        {
            TapCardIntent t when tr.ValidTargets.Contains(t.Card)
                => new TargetResponse(tr.Id, t.Card),
            CancelPressedIntent
                => throw new InvalidOperationException("Target request cannot be cancelled"),
            _ => throw new InvalidOperationException($"Unhandled intent in target mode: {intent.GetType().Name}"),
        };
    }

    // No open request — normal in-state mapping.
    return intent switch
    {
        TapCardIntent t => new SelectAttackerCommand(t.Card),
        // …
        _ => throw new InvalidOperationException($"Unhandled intent: {intent.GetType().Name}"),
    };
}
```

The VM checks `RequestsSlice` before its normal switch; an open request takes precedence (player input is committed to answering the request first). Closed sets of valid intents per mode keep the switch small.

### Lifecycle — bind on transition, dispose on transition

The presenter subscribes to `MatchStateSlice`. On a state-type change:

1. Dispose the active VM (release observable subscriptions; tear down any host-side resources).
2. Ask the registry for a new VM: `var vm = registry.Create(newState)`.
3. Cast-into the typed `Bind`: the registry returns a non-generic base reference; the registry's factory closure knows the typed `TState`, so the typed `Bind` runs against the correct subtype.
4. Set the presenter's `CurrentViewModel` observable; the view rebinds via MVVM template selection.

```csharp
// Presenter setup (host code).
store.Subscribe<MatchStateSlice>((reference, slice, ev) =>
{
    if (ev != StateChangeEvent.Updated) return;

    var newType = slice.Current.GetType();
    if (newType == currentViewModelType) return;          // same type — no rebind

    activeViewModel?.Dispose();
    activeViewModel        = registry.Create(slice.Current);   // typed Bind runs inside Create
    currentViewModelType   = newType;
    presenter.CurrentVM    = activeViewModel;             // view rebinds via template selection
});
```

The framework provides the contract; the host wires the subscription + dispose loop.

### Same-type updates do NOT rebind

Per [ADR-0009](Product/decisions/0009-engine-as-fsm.md), `MatchStateSlice.Current` may update with a same-typed state (e.g., `DeclareAttackersState` with a different `SelectedAttackers` list). These updates do **not** trigger a new `Create` — the existing VM observes them via its observable subscriptions (typically a sub-subscription on `MatchStateSlice` filtering by `current is TState`, set up in `Bind`). The presenter only rebinds on a *type* change.

This matches the engine's `TransitionStateCommand`-on-type-change semantic and keeps same-type field updates lightweight.

### Many-to-one — VM reuse across structurally-similar states

Two `MatchState` types that share UI shape register the same VM factory in the registry — see [`features/presentation/models/02-view-model-registry.md`](features/presentation/models/02-view-model-registry.md). Common example: `MainPhase1State` and `MainPhase2State` both → `MainPhaseViewModel`. The typed `Bind` reads state-specific fields; observable setup is identical.

### Where the per-state VM sits in the five validation layers

Per [ADR-0007](Product/decisions/0007-five-validation-layers.md) (refined by ADR-0012):

| Layer | Owner | Role |
|---|---|---|
| **Per-state VM affordance projection** | This Model (game-defined VM subclasses) | UI-side prophylactic — the VM doesn't surface affordances that would be illegal |
| `Map(intent)` well-formedness | This Model | Throws / no-ops for intents the current mode doesn't recognize |
| Framework intake gates (session, match-state-actions) | `actions/` validators (per ADR-0010 if landed) or `engine.Dispatch` inline (pre-merge) | Reject if engine not accepting or command not in `MatchState.Actions` |
| Registered command validators (game-shipped legality) | `actions/` validators | Authoritative legality check at dispatch |
| Before-fold `Cancel` (mid-execution) | `triggers/` | Effect-driven cancel after handler entry |

The per-state VM is **prophylactic** — it shouldn't surface a UI affordance that would be rejected at dispatch. If a state changes between gesture and dispatch (race), the validator chain catches it; the VM doesn't claim authority.

## Invariants

- **One VM type per `MatchState` subtype (or shared across many states).** Each `MatchState` subtype has at least one registered VM factory. Sharing one VM across multiple state types is supported via separate registry entries with the same factory.
- **`Bind` runs once per transition into the state.** Same-type updates do NOT re-bind; the VM observes them via observable subscriptions set up in `Bind`.
- **`Map` is mode-scoped.** The VM only handles the `PlayerIntent` subtypes that make sense in its state. Unhandled types throw `InvalidOperationException` (or return a no-op `GameCommand` — author's choice; throwing is the default for catching programmer error early).
- **Request-mode takes precedence.** When `RequestsSlice.Open` has an open request for the VM's actor, `Map` returns a `Response` matching the open request's `ResponseType`; in-state intents are ignored or re-routed.
- **VM observables are the affordance surface.** No separate `AffordanceMap`. The view binds to the VM's observable properties via standard MVVM template selection.
- **VM is prophylactic, not authoritative.** Authoritative legality remains the validator chain at dispatch.
- **`PlayerIntent` is game-defined.** Framework provides only the marker base; concrete intents are game-shipped typed records.

## Decisions

Linked: [ADR-0012](Product/decisions/0012-presentation-per-state-view-models.md) — per-state polymorphic ViewModels; global `IIntentMapper` + `AffordanceMap` + `ActionTemplate` retired; `PlayerIntent` survives. Spike provenance: `Product/spikes/fsm-architecture.md` D6–D9.

Linked: [ADR-0009](Product/decisions/0009-engine-as-fsm.md) — `MatchState` / `MatchStateSlice` shape this Model binds against.

Linked: [ADR-0007](Product/decisions/0007-five-validation-layers.md) — five-validation-layer model; this Model is the UI-side prophylactic gate (replacing the retired `AffordanceMap` layer).

## Examples

### Pile Counter — single playing-state VM

```csharp
public sealed class PilePlayingViewModel : MatchStateViewModel<PilePlayingState>
{
    private readonly ObservableProperty<IReadOnlyDictionary<Ref<Card>, IReadOnlyList<Ref<Zone>>>> moveTargets = new();

    public IObservable<IReadOnlyDictionary<Ref<Card>, IReadOnlyList<Ref<Zone>>>> MoveTargets => moveTargets;

    public override void Bind(PilePlayingState state, IStateScope scope)
    {
        // Compute per-card valid destinations from current zone contents + last-move rule.
        var piles = scope.GetAll<ZoneContentsState>().ToList();
        var lastMove = scope.Get<LastMoveState>().Move;

        moveTargets.Value = piles
            .Where(p => p.Cards.Count > 0)
            .Select(p => new
            {
                Top = p.Cards[^1],
                Destinations = piles
                    .Where(other => other.Reference != p.Reference && other.Cards.Count == 0
                                                 || other.Cards.Count > 0)
                    .Select(o => (Ref<Zone>)o.Reference)
                    .ToList()
            })
            .Where(x => !x.Top.Equals(lastMove?.Card))
            .ToDictionary(
                x => x.Top,
                x => (IReadOnlyList<Ref<Zone>>)x.Destinations);
    }

    public override GameCommand Map(PlayerIntent intent) => intent switch
    {
        DropCardIntent d => new MoveCardCommand(d.Card, d.From, d.To),
        _ => throw new InvalidOperationException($"Unhandled intent: {intent.GetType().Name}"),
    };
}
```

The VM's observable property (`MoveTargets`) is the affordance projection. The view binds drag handles and drop highlights to it directly — no intermediate `AffordanceMap`.

### Hearthstone-shaped — three VMs across the turn

```csharp
public sealed class BeginningOfTurnViewModel : MatchStateViewModel<HsBeginningOfTurnState>
{
    // State carries no player input — Actions = []. Bind is mostly cosmetic.
    public override void Bind(HsBeginningOfTurnState state, IStateScope scope) { /* animation cue */ }
    public override GameCommand Map(PlayerIntent intent)
        => throw new InvalidOperationException("No player input in BeginningOfTurnState");
}

public sealed class MainPhaseViewModel : MatchStateViewModel<HsMainPhaseState>
{
    // Reads HandState, ManaState, BoardState; exposes draggable cards + valid attack targets.
    public override void Bind(HsMainPhaseState state, IStateScope scope) { /* … */ }
    public override GameCommand Map(PlayerIntent intent) => intent switch
    {
        PlayCardIntent  p => new PlayCardCommand (p.Card, p.Target),
        AttackIntent    a => new AttackCommand   (a.Attacker, a.Defender),
        EndTurnIntent     => new EndTurnCommand  (),
        _ => throw new InvalidOperationException($"Unhandled: {intent.GetType().Name}"),
    };
}

public sealed class EndPhaseViewModel : MatchStateViewModel<HsEndPhaseState>
{
    // Setup-only state. Bind shows "end of turn" animation; Map throws.
    public override void Bind(HsEndPhaseState state, IStateScope scope) { /* animation */ }
    public override GameCommand Map(PlayerIntent intent)
        => throw new InvalidOperationException("No player input in EndPhaseState");
}
```

Three VMs cover the turn loop. Adding a new phase = new `MatchState` subclass + new VM + one registration line.

### MTG-shaped — target request mid-resolution

```csharp
public sealed class MainPhaseViewModel : MatchStateViewModel<MainPhaseState>
{
    private IStateScope scope;
    private readonly IActor actor;

    public MainPhaseViewModel(IActor actor) { this.actor = actor; }

    public override void Bind(MainPhaseState state, IStateScope scope)
    {
        this.scope = scope;
        // …observables for hand, board, mana pool…
    }

    public override GameCommand Map(PlayerIntent intent)
    {
        // Check request-mode first — an open TargetRequest for our actor takes precedence.
        var open = scope.Get<RequestsSlice>().Open;
        var pending = open.Values.FirstOrDefault(r => r.Asker.Id == actor.Id);

        if (pending is TargetRequest tr)
        {
            return intent switch
            {
                TapCardIntent t when tr.ValidTargets.Contains(t.Card)
                    => new TargetResponse(tr.Id, t.Card),
                _ => throw new InvalidOperationException(
                        $"In TargetRequest mode; only valid-target taps accepted"),
            };
        }

        // Normal main-phase mapping.
        return intent switch
        {
            PlayCardIntent  p => new PlayCardCommand (p.Card),
            AttackIntent    a => new DeclareAttackerCommand(a.Attacker),
            EndTurnIntent     => new EndTurnCommand  (),
            _ => throw new InvalidOperationException($"Unhandled: {intent.GetType().Name}"),
        };
    }
}
```

The same VM handles normal play *and* mid-effect target requests — the request-mode branch checks `RequestsSlice` before the in-state switch. UI rendering reads `RequestsSlice` directly to overlay a target picker (the slice is the wire-format clients see).

## Open follow-ups

- `oq-view-model-host-shape` — framework `ViewModel` base class shape (animation hooks, observable wiring, disposal). Pin when the host MVVM toolkit is chosen; today this Model only pins the `Bind` + `Map` contract.
- `bl-interaction-feature` — `interaction/` feature for the gesture-to-intent layer (raw device input → typed `PlayerIntent`). Currently the host wires this; a dedicated interaction feature would normalize across renderers.
- **Multi-actor concurrent VMs.** Today's design is one-VM-active-at-a-time per presenter. Multi-actor games where each player sees their own VM (simultaneous mulligans, parallel target picks) need a per-actor presenter + per-actor `RequestsSlice` query helpers. Pin when multiplayer lands.
- **VM disposal on match-end.** `EndMatchCommand` → `EndedSession` flips the engine into the terminal session state; the presenter's active VM should dispose. Concrete shape ties to `oq-effect-cancellation-on-match-end`'s GC pass over `RequestsSlice` / `AnswersSlice`.

---

*Below: provenance — Model authored 2026-05-11 as part of the ADR-0012 cascade. Renamed from `01-presentation-loop.md` (process-shaped title → type-shaped) per `_docs/model.md` audit obligations; pre-cascade content preserved at [`features/presentation/archive/01-presentation-loop.md`](features/presentation/archive/01-presentation-loop.md).*

## Options considered

*n/a — descriptive Model. Choices made at ADR-0012 scope; see linked ADR for option-weighing.*

## Rationale

*n/a — see `## Contracts` and [ADR-0012](Product/decisions/0012-presentation-per-state-view-models.md).*

## Consequences

*n/a — see [ADR-0012](Product/decisions/0012-presentation-per-state-view-models.md) `## Consequences`.*
