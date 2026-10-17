---
type: adr
status: accepted
references: [features/presentation/archive/01-match-state-view-model.md, features/presentation/archive/02-view-model-registry.md, features/presentation/archive/presentation.md, features/actions/archive/01-command-pipeline.md, features/engine/archive/01-game-engine.md, features/engine/archive/03-game-rules-runtime.md, features/engine/archive/07-match-state.md, spikes/archive/fsm-architecture.md, decisions/0007-five-validation-layers.md, decisions/0009-engine-as-fsm.md]
tags: [#adr, #presentation, #engine, #fsm, #view-model, #intent]
---

# ADR-0012 — Presentation as per-state polymorphic ViewModels; global `IIntentMapper` + `AffordanceMap` + `ActionTemplate` retired

> **Superseded in part 2026-10-18 by [ADR-0014](Product/decisions/0014-service-first-state-model.md#effect-on-earlier-adrs).** The slice-based parts of this decision (`MatchStateSlice`, `PendingEffectsSlice`, `Ref<T>`, Scaffold `Store`) no longer match the code. ADR-0014 lists what survives.

Date: 2026-05-11 (drafted from `Product/spikes/archive/fsm-architecture.md` D6–D9; sub-ADR D of the FSM-arch cascade following ADR-0009 + ADR-0010 + ADR-0011)
Context-docs: new Models `presentation/01-match-state-view-model.md`, `presentation/02-view-model-registry.md`, rewritten `presentation/presentation.md`, archived `actions/02-intent-mapper.md`, archived `actions/05-available-actions-snapshot.md`
Proposal: `Product/spikes/archive/fsm-architecture.md`

## Context

After ADR-0009 (engine IS the FSM), the polymorphic match state lives in `MatchStateSlice` and every match phase / decision mode / cleanup state is a concrete `MatchState` subclass. The pre-cascade presentation layer pre-dated that shape:

1. **Global `IIntentMapper`.** One game-supplied service held a single `switch` over every `ActionTemplate` subtype the game ever shipped. Adding a phase grew the switch. Cross-cutting state ("are we in target-picker mode? in resolve-combat mode?") was inferred indirectly from the snapshot's template set, not represented directly. The mapper was structurally a per-state map flattened into one global table — the wrong shape once `MatchState` exists.
2. **`AffordanceMap` (game-specific projection).** A presentation-side projection from `AvailableActionsSnapshot` into a UI-shaped dictionary (`Ref<Card> → IReadOnlyList<Ref<Zone>>` etc.). Recomputed on every snapshot change. With per-state ViewModels the projection moves *onto the VM* — each state binds its own observables; "what can be dragged where?" is the VM's responsibility for the current state, not a global map.
3. **`AvailableActionsSnapshot` + `ActionTemplate` wire form.** The pre-ADR-0009 picture had the runtime publishing a typed `IReadOnlyList<ActionTemplate>` describing the legal-action *space* (per-action source, target slots, modes, parameters). After ADR-0009 the legal *command types* live on `MatchState.Actions: IReadOnlyList<Type>`; the rich typed-descriptor wire form (with parameter constraints, target slots, etc.) is unused — the per-state VM projects state into bindings directly, with no intermediate wire format.

The 2026-05-10 open-questions walk on `Product/spikes/archive/fsm-architecture.md` settled the structural direction across D6–D9. ADR-0009 deferred the presentation-side rewrite to this ADR; the dependency is solely on `MatchState` (already landed). Independent of ADR-0010 and ADR-0011 structurally — though it shares cosmetic surfaces with both (the `actions/01-command-pipeline.md` diagram in particular).

## Options considered

- **A — Keep the global `IIntentMapper` + `AffordanceMap` + typed `ActionTemplate`; introduce a "current state" discriminator field on the snapshot that the mapper switches on first.** Rejected — encodes the FSM as a string-or-enum tag *next to* the slice that already carries the FSM. Per-state polymorphism via `MatchStateSlice.Current` is the cleaner shape; the discriminator is structurally a workaround.
- **B — Per-state ViewModels (`MatchStateViewModel<TState>`) keep typed `ActionTemplate` as the wire format and project it themselves.** Rejected — `MatchState.Actions: IReadOnlyList<Type>` (per ADR-0009) is the framework's "what command types apply here?" surface; the typed-descriptor wire form has no consumer once VMs are per-state. The richer descriptor (parameter constraints, slot data) belongs *inside* the VM's per-state bindings, not in a wire format the VM has to parse.
- **C — Per-state polymorphic `MatchStateViewModel<TState>` carrying `Bind(state, scope)` and `Map(intent) → GameCommand`; `ViewModelRegistry` maps `MatchState` type → factory; presenter subscribes to `MatchStateSlice` and rebinds the active VM on type change. Global `IIntentMapper`, `AffordanceMap`, `ActionTemplate` wire form retire. `PlayerIntent` survives as a game-defined intent base; `PlayerChoice` retires (the choice is encoded in the intent type/data).** **Chosen.**

## Decision

Four structural locks (mapping to D6–D9 of the FSM-arch spike).

**1. `MatchStateViewModel<TState>` — per-state polymorphic ViewModel.** Each match phase / decision mode / cleanup state authors a concrete `MatchStateViewModel<TState>` subclass. The VM owns:

- **`Bind(TState state, IStateScope scope)`** — called once when the FSM transitions into this state. Reads the typed state record + the wider `IStateScope`; sets up observables / view bindings. Idempotent.
- **`Map(PlayerIntent intent) → GameCommand`** — translates a gesture-shaped `PlayerIntent` into a `GameCommand` (most often a `GameAction`; in request-mode, a `Response` per [ADR-0011](Product/decisions/0011-effect-orchestration-dispatch-requests.md)).

```csharp
public abstract class MatchStateViewModel<TState> : ViewModel where TState : MatchState
{
    public abstract void        Bind (TState state, IStateScope scope);
    public abstract GameCommand Map  (PlayerIntent intent);
}
```

The polymorphism is in the type parameter, not in the body — concrete subclasses are typed to a specific `MatchState` subtype and the framework wires the typed `Bind` for that state's runtime type.

**2. `ViewModelRegistry` maps `MatchState` type → factory.** A sealed registry, one per match (or per host, depending on lifecycle), maps each `MatchState` subtype to a factory that constructs the matching VM. Presenter subscribes to `MatchStateSlice`; on a state-type change, asks the registry for a fresh VM and binds it.

```csharp
public sealed class ViewModelRegistry
{
    private readonly Dictionary<Type, Func<MatchStateViewModel>> factories;
    public ViewModelRegistry(Dictionary<Type, Func<MatchStateViewModel>> factories)
        => this.factories = factories;

    public MatchStateViewModel Create(MatchState state)
        => factories[state.GetType()]();
}
```

**Many-to-one is free.** Two `MatchState` types can share one VM by registering both keys against the same factory (e.g., `MainPhase1State` and `MainPhase2State` both → `MainPhaseViewModel`). Same VM type; different state-record fields read in `Bind`.

**3. Per-VM `Map(intent)` replaces the global `IIntentMapper`.** Each VM owns a small intent map scoped to its mode — typically a `switch` on a handful of `PlayerIntent` subtypes that make sense for the current state.

```csharp
public sealed class DeclareAttackersViewModel : MatchStateViewModel<DeclareAttackersState>
{
    public override void Bind(DeclareAttackersState state, IStateScope scope) { /* … */ }

    public override GameCommand Map(PlayerIntent intent) => intent switch {
        TapCardIntent     t => new SelectAttackerCommand   (t.Card),
        UntapCardIntent   u => new DeselectAttackerCommand (u.Card),
        DonePressedIntent   => new CommitDeclarationCommand(),
        _ => throw new InvalidOperationException(intent.GetType().Name),
    };
}
```

The VM in request-mode (per ADR-0011 — when `RequestsSlice.Open` has an open request targeting the VM's actor) maps the gesture to a `Response`:

```csharp
public override GameCommand Map(PlayerIntent intent)
{
    var open = scope.Get<RequestsSlice>().Open;
    if (open.TryGetValue(currentRequestId, out var req) && req is TargetRequest tr)
    {
        return intent switch {
            TapCardIntent t => new TargetResponse(tr.Id, t.Card),
            _ => throw new InvalidOperationException(intent.GetType().Name),
        };
    }
    return intent switch { /* … normal in-state intents */ };
}
```

Smaller switches; mode-scoped; adding a phase doesn't grow a central table — it adds a new VM registration.

**4. `AffordanceMap`, global `IIntentMapper`, `ActionTemplate` wire form, `PlayerChoice` retire.** What presentation drops, with replacements:

| Dropped | Replacement |
|---|---|
| `AffordanceMap` | VM-exposed observables. The VM is the projection — its observable properties are the affordance surface the view binds to. |
| Global `IIntentMapper` | Per-VM `Map(intent)`. |
| `IAvailableActions` service | None. Already retired by [ADR-0009](Product/decisions/0009-engine-as-fsm.md). |
| `AvailableActionsState` slice | None. Already subsumed by `MatchStateSlice` per ADR-0009. |
| `AvailableActionsSnapshot` (wire form) | `MatchState.Actions: IReadOnlyList<Type>` for the type-only intake gate (per ADR-0009); per-state VM observables for the rich UI surface. |
| `ActionTemplate` (typed wire form per command) | None. Per-state VM bindings carry the per-action shape directly. Richer wire form (parameter constraints, slot data) revisited only if a real cross-process consumer surfaces. |
| `PlayerChoice` (separate choice-data record) | None. The choice is encoded in the intent type and data (`TapCardIntent.Card`, `PickOptionIntent.OptionIndex`, etc.). |
| Snapshot-as-state pattern in presentation | State-as-state pattern. Presenter subscribes to `MatchStateSlice` directly; on state-type change, asks the registry for the new VM. |

`PlayerIntent` survives as a game-defined intent base — typed gesture records (`TapCardIntent`, `DonePressedIntent`, etc.) that the active VM's `Map` dispatches on. `IntentGateway` survives as the thin entry layer translating raw input gestures into typed `PlayerIntent`s, then routing through the active VM; the canonical home moves from `actions/` to presentation feature-root vocabulary.

## Consequences

- **Enables** mode-scoped intent handling. The set of legal gestures in any state is the set of `PlayerIntent` subtypes the active VM's `Map` switch handles — no global table to grow as phases land.
- **Enables** richer per-state UI projection. Each VM exposes its own observable properties (lists of valid targets, highlight masks, mode pickers, parameter sliders) shaped to its state; the view binds via standard MVVM template selection. No game-wide `AffordanceMap` projection step.
- **Enables** request-mode interaction. A VM in a normal phase + an open `Request<TResponse>` for the VM's actor (per [ADR-0011](Product/decisions/0011-effect-orchestration-dispatch-requests.md)) maps the gesture to a `Response`. Same `Map(intent) → GameCommand` surface; the return type covers both `GameAction` and `Response` since they share `GameCommand`.
- **Enables** many-to-one VM reuse. Two structurally-similar `MatchState` types (e.g., `MainPhase1State` + `MainPhase2State`) register the same VM factory; differences come out via the typed `Bind` reading state-specific fields.
- **Enables** test friendliness. Each VM is a small, focused class with a typed `Bind` and a switch-shaped `Map`. Testing the per-state intent surface is mechanical.
- **Constrains** every match state to have a registered VM. A missing registration is a `KeyNotFoundException` at the registry call — caught at the first transition into the un-registered state. Surface mechanism for "what's registered?" introspection is the registry's `factories` dictionary.
- **Constrains** the framework command pipeline diagram. The pre-cascade pipeline showed `PlayerIntent → IntentMapper.Map(template, choice) → GameCommand`; post-cascade it shows `PlayerIntent → ActiveVM.Map(intent) → GameCommand`. Visually the same boundary; semantically the mapping function moved from a global service to the per-state VM.
- **Retires** `actions/02-intent-mapper.md` (canonical home of the global `IIntentMapper`) and `actions/05-available-actions-snapshot.md` (canonical home of `AvailableActionsSnapshot` / `ActionTemplate` / `PlayerChoice`). Both archived to `Product/features/actions/archive/`.
- **Retires** `presentation/01-presentation-loop.md` (the pre-cascade four-layer loop synthesis Model — Store / IntentMapper / AffordanceMap / View). Archived to `Product/features/presentation/archive/`. The post-cascade loop synthesis (Store / ViewModelRegistry / per-state VM / View) lives on the rewritten `presentation/presentation.md` feature root.
- **Reverts / replaces** the pre-cascade presentation-loop synthesis. The four-layer model survives in spirit — separation between state / projection / view — but the projection step changes from `AffordanceMap (game-specific)` to per-state VM observables.
- **Refines** [ADR-0007](Product/decisions/0007-five-validation-layers.md): the per-state ViewModel is the UI-side prophylactic gate (replacing the `AffordanceMap` row in the five-layer model). Authoritative legality remains the registered validator chain (per ADR-0010 if landed, or `handler.Validate` on the base branch — same role). The five-layer model survives; layer names refresh.
- **Defers** the framework `ViewModel` base class shape (animation hooks, observable wiring, disposal). The ADR pins the *contract* (`Bind` + `Map`); the *implementation* (MVVM toolkit choice, observable library, etc.) is host-side. Tracked informally as `oq-view-model-host-shape`.
- **Defers** `IntentGateway` lifecycle ownership. Currently shown as a presentation-feature-root concept; concrete shape (game-supplied component pumping device gestures → typed `PlayerIntent` → active VM `Map` → `engine.Dispatch`) tracks alongside `bl-interaction-feature`.
- **Defers** richer-than-types wire form for `MatchState.Actions`. ADR-0009 left `Actions: IReadOnlyList<Type>`; if a real cross-process consumer (replay viewer, network thin client) needs more than command type ids, revisit. Until then, the per-state VM is the rich-projection surface in-process.
- **Defers** multi-actor / per-player concurrent VMs. Today's `ViewModelRegistry` is one-VM-active-at-a-time per presenter; multi-actor games where each player sees their own VM (e.g., simultaneous mulligans) need a per-actor presenter + a per-actor `RequestsSlice` query helper. Tracked informally; pin when multiplayer lands.

## Notes

- **Sub-ADR cascade position.** This is sub-ADR D of the FSM-arch cascade — the last of the four. Depends on ADR-0009's `MatchState` shape (already merged); structurally independent of ADR-0010 and ADR-0011. Conflict surface at merge time: the cosmetic `actions/01-command-pipeline.md` diagram and feature roots — all three of ADR-0010 / ADR-0011 / ADR-0012 touch them. Resolution by composition.
- **Spike provenance.** `Product/spikes/archive/fsm-architecture.md` D6–D9 is the historical source. The FSM-architecture spike was archived in the post-cascade consolidation PR (2026-05-11) as `status: superseded` referencing 0009 / 0010 / 0011 / 0012; the same PR swept GLOSSARY + OPEN-ITEMS to repoint stale spike references at the landed ADRs. `Product/general/ARCHITECTURE.md` rewrite is a follow-up PR.
- **`PlayerIntent` ownership.** Moves from `actions/` (where it sat alongside the now-retired `IIntentMapper`) to presentation feature-root vocabulary. `IntentGateway` likewise. The intent base is a marker type — game-defined records derive from it; each `MatchStateViewModel<TState>.Map` switches on the typed subtypes its state cares about.
- **`PlayerChoice` retired.** The pre-cascade `PlayerChoice(TemplateIndex, ModeIndex, Targets, Parameters)` flat record carried the player's pick alongside the `ActionTemplate` it was bound against. Per-state VMs absorb this — each VM authors its own intent records carrying typed per-gesture data. No central wire format.
- **`IntentGateway` shape.** Today: thin entry layer translating raw device gestures (mouse, touch, controller) into typed `PlayerIntent` records, then routing through the active VM (looked up via `ViewModelRegistry` keyed on `MatchStateSlice.Current.GetType()`). Concrete implementation is game-supplied; framework provides the contract through `MatchStateViewModel<TState>` + `ViewModelRegistry`.
- **Five-validation-layer model survives.** ADR-0007's named layers refresh: the "AffordanceMap" layer becomes "per-state ViewModel affordance projection"; the "engine lifecycle gate" + "MatchState actions gate" become registered validators (per ADR-0010 if landed; inline gates pre-merge); the "IntentMapper well-formedness" layer is absorbed into the per-state VM's `Map` (an invalid intent for the current state throws or returns a no-op `GameCommand`).
