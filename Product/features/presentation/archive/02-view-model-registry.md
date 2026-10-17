---
type: model
status: abandoned
archived_on: 2026-10-18
reason: "Written for the old state model (slices, Ref<T>, MatchStateSlice) and never built. Read it for the open questions, not as the contract; the replacement design comes from Product/spikes/framework-roadmap.md."
references: [features/presentation/archive/01-match-state-view-model.md, features/presentation/archive/presentation.md, features/engine/archive/07-match-state.md, features/state/archive/state.md, decisions/0012-presentation-per-state-view-models.md, decisions/0009-engine-as-fsm.md]
tags: [#presentation, #view-model, #registry, #fsm, #model]
---

> **Archived 2026-10-18.** Written for the old state model (slices, Ref<T>, MatchStateSlice) and never built. Read it for the open questions, not as the contract; the replacement design comes from Product/spikes/framework-roadmap.md.

# View Model Registry — model

## What it is

`ViewModelRegistry` is the sealed lookup that maps `MatchState` types to factories producing the corresponding `MatchStateViewModel<TState>` (per [`01-match-state-view-model.md`](features/presentation/archive/01-match-state-view-model.md)). Per [ADR-0012](Product/decisions/0012-presentation-per-state-view-models.md), the presenter subscribes to `MatchStateSlice`; on a state-type change it calls `registry.Create(slice.Current)` and binds the returned VM. Many-to-one is supported — two `MatchState` types can register the same factory if they share UI shape.

```
MatchStateSlice.Current changes type
              │
              ▼
        registry.Create(state)
              │
              │ Dictionary<Type, Func<MatchStateViewModel>> lookup
              ▼
        new VM instance ─▶ typed Bind(state, scope) runs inside factory
              │
              ▼
        presenter sets CurrentVM (view rebinds via template selection)
```

## Contracts

### `ViewModelRegistry`

```csharp
public sealed class ViewModelRegistry
{
    private readonly IReadOnlyDictionary<Type, Func<MatchState, MatchStateViewModel>> factories;

    public ViewModelRegistry(IReadOnlyDictionary<Type, Func<MatchState, MatchStateViewModel>> factories)
        => this.factories = factories;

    public MatchStateViewModel Create(MatchState state)
    {
        if (!factories.TryGetValue(state.GetType(), out var factory))
            throw new KeyNotFoundException(
                $"No ViewModel registered for MatchState type '{state.GetType().Name}'. " +
                $"Register a factory via ViewModelRegistryBuilder.Register<{state.GetType().Name}, …>().");
        return factory(state);
    }
}
```

- **Sealed.** Game-side composition happens through the builder (below) or DI registration, not via subclassing.
- **`Create(state)` returns a non-generic `MatchStateViewModel` base reference.** The typed `Bind` runs *inside* the factory closure (which captures the concrete VM type); the presenter holds the non-generic reference.
- **Missing registration is an exception at first transition.** The framework doesn't pre-validate that every `MatchState` subtype has a VM (it can't — `MatchState` subtypes are game-defined and discovered at registration time). A `KeyNotFoundException` at the first `Create` call is the failure mode.

### Factory shape

Each factory takes a `MatchState` (boxed; the concrete subtype is known at registration time) and returns a `MatchStateViewModel` (non-generic base). Inside the factory, the concrete VM type is constructed and its typed `Bind(TState, IStateScope)` runs:

```csharp
// Inside a factory closure registered for DeclareAttackersState:
state =>
{
    var vm = new DeclareAttackersViewModel();
    vm.Bind((DeclareAttackersState)state, /* scope from closure */);
    return vm;
};
```

The cast is safe — the registry's `Create` keys on `state.GetType()`, and the factory was registered against that exact type. The `IStateScope` typically comes from a closure captured at registration time (the host's per-match `IStateScope` reference).

### `ViewModelRegistryBuilder` — game-side composition

```csharp
public sealed class ViewModelRegistryBuilder
{
    private readonly Dictionary<Type, Func<MatchState, MatchStateViewModel>> factories = new();
    private readonly IStateScope scope;
    private readonly IServiceProvider services;

    public ViewModelRegistryBuilder(IStateScope scope, IServiceProvider services)
    {
        this.scope    = scope;
        this.services = services;
    }

    public ViewModelRegistryBuilder Register<TState, TViewModel>()
        where TState     : MatchState
        where TViewModel : MatchStateViewModel<TState>
    {
        factories[typeof(TState)] = state =>
        {
            var vm = services.GetRequiredService<TViewModel>();
            vm.Bind((TState)state, scope);
            return vm;
        };
        return this;
    }

    public ViewModelRegistryBuilder Register<TState1, TState2, TViewModel>()
        where TState1    : MatchState
        where TState2    : MatchState
        where TViewModel : MatchStateViewModel
    {
        Func<MatchState, MatchStateViewModel> factory = state =>
        {
            var vm = services.GetRequiredService<TViewModel>();
            // Many-to-one — the shared VM reads its own state-specific paths.
            // Typed Bind dispatched via runtime type-check inside the shared VM.
            BindByRuntimeType(vm, state, scope);
            return vm;
        };
        factories[typeof(TState1)] = factory;
        factories[typeof(TState2)] = factory;
        return this;
    }

    public ViewModelRegistry Build() => new(factories);

    private static void BindByRuntimeType(MatchStateViewModel vm, MatchState state, IStateScope scope)
    {
        // Reflection or generic dispatch to call the right typed Bind overload.
        // Concrete mechanism is host-shape; the framework provides the abstract Bind contract.
    }
}
```

**Single-state registration.** Each state-VM pair lands as one `Register<TState, TViewModel>()` call. Two type-parameters; the builder enforces the `MatchStateViewModel<TState>` constraint at compile time.

**Many-to-one registration.** Several states sharing one VM use one of these overloads (concrete shape may collapse via params or fluent chaining; the example above shows one direction). The shared VM author handles the per-state binding via runtime type-check inside `Bind`.

### Installer integration

Games author a single registration block in their composition root, typically alongside DI:

```csharp
// Game installer (Unity / VContainer / .NET DI).
services.AddTransient<DeclareAttackersViewModel>();
services.AddTransient<DeclareBlockersViewModel>();
services.AddTransient<MainPhaseViewModel>();
services.AddTransient<EndPhaseViewModel>();
services.AddTransient<GameOverViewModel>();

services.AddSingleton<ViewModelRegistry>(sp =>
{
    var scope = sp.GetRequiredService<IStateScope>();
    return new ViewModelRegistryBuilder(scope, sp)
        .Register<DeclareAttackersState, DeclareAttackersViewModel>()
        .Register<DeclareBlockersState,  DeclareBlockersViewModel>()
        .Register<MainPhase1State, MainPhase2State, MainPhaseViewModel>()   // many-to-one
        .Register<EndPhaseState,         EndPhaseViewModel>()
        .Register<GameOverState,         GameOverViewModel>()
        .Build();
});
```

The framework provides the builder + registry contract; the host wires DI registration and per-match scope.

### Presenter wiring

```csharp
// Host presenter.
public sealed class MatchPresenter
{
    private readonly Store store;
    private readonly ViewModelRegistry registry;
    private MatchStateViewModel? activeVM;
    private Type? currentStateType;

    public MatchStateViewModel? CurrentVM { get; private set; }
    public event Action? CurrentVMChanged;

    public MatchPresenter(Store store, ViewModelRegistry registry)
    {
        this.store    = store;
        this.registry = registry;
        store.Subscribe<MatchStateSlice>(OnMatchStateChanged);
        OnMatchStateChanged(null, store.Get<MatchStateSlice>(), StateChangeEvent.Updated);   // seed
    }

    private void OnMatchStateChanged(Reference? _, MatchStateSlice slice, StateChangeEvent ev)
    {
        if (ev != StateChangeEvent.Updated && ev != StateChangeEvent.Created) return;

        var newType = slice.Current.GetType();
        if (newType == currentStateType) return;     // same-type field update — no rebind

        (activeVM as IDisposable)?.Dispose();
        activeVM         = registry.Create(slice.Current);   // typed Bind runs inside
        currentStateType = newType;
        CurrentVM        = activeVM;
        CurrentVMChanged?.Invoke();
    }
}
```

The presenter:
1. Subscribes to `MatchStateSlice` once at construction (and seeds the initial VM).
2. On every `Updated`/`Created` event, checks if `current.GetType()` differs from the cached `currentStateType`.
3. If different: dispose the active VM, ask the registry for a new one (typed `Bind` runs inside), update `CurrentVM`, fire `CurrentVMChanged`.
4. The view re-binds via standard MVVM template selection on `CurrentVM`.

Same-type updates (a `MainPhaseState` updated with new hand contents, for example) flow through the active VM's own observable subscriptions; the presenter does not rebind.

### Lifecycle hooks

- **Construction.** Registry is constructed once per match (or once per host, depending on the host's MVVM toolkit). Typically singleton in the per-match DI scope.
- **Per-VM instance.** Resolved transient from DI inside the factory closure. Allocation cost per state transition; for typical match lengths (tens to hundreds of transitions) this is negligible.
- **Disposal.** The presenter calls `Dispose` on the outgoing VM before constructing the new one. If a VM implements `IDisposable` (typical for observable subscriptions), the framework respects it.

## Invariants

- **Sealed registry.** Game-side composition happens through the builder; not via subclassing.
- **`Create` keys on `state.GetType()`.** Exact-type match. Inheritance hierarchies in `MatchState` subtypes are not walked; each concrete subtype needs its own registration (or shares a factory via many-to-one registration).
- **Missing registration throws at first transition.** Not pre-validated; the failure mode is `KeyNotFoundException` from `Create`.
- **Same-type updates do NOT rebind.** The presenter checks type identity; same-type state changes flow through the active VM's observable subscriptions.
- **VMs are transient.** Resolved from DI per-transition; the registry holds factories, not instances.
- **Many-to-one is first-class.** Two `MatchState` subtypes registering against the same factory is a supported pattern, not a workaround.

## Decisions

Linked: [ADR-0012](Product/decisions/0012-presentation-per-state-view-models.md) — `ViewModelRegistry` maps state type → factory; presenter subscribes to `MatchStateSlice` and rebinds on type change. Spike provenance: `Product/spikes/archive/fsm-architecture.md` D7.

Linked: [ADR-0009](Product/decisions/0009-engine-as-fsm.md) — `MatchStateSlice` shape this registry binds against; same-type semantics from D6.

## Examples

### Minimal — Pile Counter

```csharp
services.AddTransient<PilePlayingViewModel>();
services.AddTransient<PileGameOverViewModel>();

services.AddSingleton<ViewModelRegistry>(sp =>
{
    var scope = sp.GetRequiredService<IStateScope>();
    return new ViewModelRegistryBuilder(scope, sp)
        .Register<PilePlayingState,  PilePlayingViewModel>()
        .Register<PileGameOverState, PileGameOverViewModel>()
        .Build();
});
```

Two states, two VMs. Adding a difficulty mode that introduces an `EasyPilePlayingState` subtype = new state + new (or shared) VM + one registration line.

### Many-to-one — MTG main phases

```csharp
services.AddTransient<MainPhaseViewModel>();
services.AddTransient<DeclareAttackersViewModel>();
// …

new ViewModelRegistryBuilder(scope, sp)
    .Register<MainPhase1State, MainPhase2State, MainPhaseViewModel>()
    .Register<DeclareAttackersState, DeclareAttackersViewModel>()
    // …
    .Build();
```

`MainPhase1State` and `MainPhase2State` register against the same factory; one `MainPhaseViewModel` handles both via its typed `Bind`.

### Inheritance — not walked

```csharp
public abstract record SetupPhaseState : MatchState { /* … */ }
public sealed   record MulliganState : SetupPhaseState { /* … */ }
public sealed   record InitialHandState : SetupPhaseState { /* … */ }
```

Registering `Register<SetupPhaseState, SetupPhaseViewModel>()` does **NOT** catch `MulliganState` or `InitialHandState` — the registry's lookup is exact-type. Each concrete subtype needs its own registration (or a many-to-one entry covering both).

## Open follow-ups

- **Wildcard / inheritance-walking lookup.** Today's exact-type match keeps the surface small. If real games need a "fallback for any `SetupPhaseState` subtype" pattern, add a configurable second-pass walk. Defer until forced.
- **VM caching.** Today every state-type change resolves a fresh VM. For games with frequent transitions in/out of the same state type, caching the VM and re-binding might be cheaper than constructing fresh. Pin if profile shows allocation pressure.
- **Multi-presenter / per-actor.** Multi-actor games need one presenter per actor (each with their own active VM). The registry is shareable across presenters; the per-actor `IStateScope` differs (filtered visibility). Tracked alongside `bl-interaction-feature`.
- **Builder ergonomics.** The current `Register<TState, TViewModel>()` and many-to-one overloads are minimal. A richer builder (`RegisterAll`, `Conditional`, attribute-based discovery) is host-shape concern.
- **Registry validation tooling.** A development-mode pass that walks all `MatchState` subtypes in the loaded assemblies and reports unregistered types would catch missing entries before the first transition. Concrete shape pending; tracked informally.

---

*Below: provenance — Model authored 2026-05-11 as part of the ADR-0012 cascade. No options-weighing at this Model's scope; the registry shape follows from [ADR-0012](Product/decisions/0012-presentation-per-state-view-models.md).*

## Options considered

*n/a — descriptive Model. Choices made at ADR-0012 scope.*

## Rationale

*n/a — see `## Contracts` and [ADR-0012](Product/decisions/0012-presentation-per-state-view-models.md).*

## Consequences

*n/a — see [ADR-0012](Product/decisions/0012-presentation-per-state-view-models.md) `## Consequences`.*
