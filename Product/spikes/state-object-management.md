---
type: spike
status: open
tags: [#spike, #state, #entities, #engine]
---

# State / Object Management Audit

> **Status: open.** Research audit of how other ecosystems separate behavior-bearing instances from immutable store state, and how they auto-register slices on object creation. Intended to seed a decision on initialization paths and instance lifecycle in the Card Framework.

Two open questions drive this spike:

1. **Object management.** Where do behavior-bearing instances live, given that store slices must be immutable / serializable? Adjacent registry? Middleware? Normalization?
2. **Auto-registration on creation.** When an object is created, how does the framework guarantee its slice(s) are registered (and torn down) without manual wiring? Are there "recipes" for this?

Today the framework already encodes some of the answers — see `features/entities/models/02-runtime-surface.md` (`StateEntity<TDef>`, `IEntityRegistry.Spawn`) and `features/engine/models/05-game-factory.md` (DI + factory split). This audit positions those choices against the wider ecosystem.

---

## TL;DR

- The dominant pattern across every ecosystem is **"ID-as-handle" + a registry/lookup table.** ECS calls them `Entity` IDs, Akka calls them `IActorRef`, Orleans calls them `GrainId`, RTK calls them keys in `entities[id]`. The framework is already on this path: `InstanceId` + `IEntityRegistry` + `StateEntity<TDef>`.
- **Almost no production system puts behavior-bearing instances inside the immutable store.** Even MobX-State-Tree (the closest "objects ARE state" approach) keeps two parallel views: a mutable instance graph and an automatically-derived immutable snapshot tree.
- **For C# specifically**, Fluxor and Redux.NET keep state classes as plain immutable records and treat services / view models / hubs as DI-managed singletons or transients living outside the store.
- **For card games specifically**, the standard representation is "every entity is a tag bag (id → {tag: value})" stored in zones (Hearthstone protocol, Sabberstone, HearthSim's BoardState). Card *behavior* lives in a static card-definition registry; per-instance state is just data — close to the framework's existing `Definition` vs `EntityVariableState` split.
- **Auto-registration on object creation falls into three families:**
  - **Prefab/archetype/bundle manifests** (Unity prefabs, Bevy `Bundle`, Flecs prefabs, ScriptableObjects).
  - **Factory + DI lifecycle hooks** (Zenject `PlaceholderFactory`, MS DI scopes, VContainer).
  - **Dynamic reducer injection** at the store level (RTK `combineSlices().inject()`, redux-dynamic-modules, `replaceReducer`).
- **In modern Redux Toolkit (2.0+)** the official answer is `combineSlices` + `slice.injectInto(rootReducer)`. Cleanest "open-ended store" API; translates well to C#. Note the doc warning: *"the added reducer state will not show up in your store until the next action is dispatched"* — directly relevant to the in-flight-dispatch open item in `OPEN-ITEMS.md`.
- **Source generators are the C# native answer** to "auto-wire on declaration." Injectio / AutoRegisterInject use attributes scanned at compile time; the same trick maps to a `[StateSlice]` attribute emitting store-registration code.
- **Orleans is the closest large-scale precedent** for "objects with identity, behavior, and state where state can be persisted/restored." Grains have a permanent `GrainId`, methods (behavior), and pluggable state persistence; runtime activates/deactivates on demand.
- **No clean winner.** The choice depends on whether the framework should feel like Redux, like ECS, or like an actor system. Recommended path is hybrid: **Option A backbone (already in flight) + Option B (source-gen recipes) or Option C (ScriptableObject manifests) for ergonomics.**

---

## 1. Problem 1 — instance vs state separation

### 1.1 Redux Toolkit `createEntityAdapter` (JS)

Stores entities as `{ ids: [], entities: { [id]: T } }`. Entries are **plain serializable objects** — no methods. Behavior lives in selectors, thunks, components. The "instance" is reconstituted by selectors as needed. Purest Redux answer: there is no instance, only state and code that operates on it.

### 1.2 redux-orm / normalizr (JS)

Sits on top of normalized Redux state. Model classes look OO (`Card.create(...)`, `card.update(...)`) but are **session-scoped facades** over immutable state. Each session reads relational state once, you call methods, the session emits a new immutable state. Instance is throwaway; ID is the durable handle.

### 1.3 MobX-State-Tree (JS)

The most relevant counter-example. From the docs: *"the tree consists of mutable, but strictly protected objects... from this living tree, immutable, structurally shared, snapshots are generated automatically."* Two parallel views: a mutable instance graph (`card.flip()`) and an immutable snapshot tree (`getSnapshot(card)`). `onSnapshot` / `onPatch` give Redux-equivalent observability. Even here there are two views — the framework just hides the seam.

### 1.4 Fluxor (C# / Blazor)

Near-direct port of Redux. State classes are plain immutable records, reducers are static `[ReducerMethod]`s, behavior lives in `Effects` and DI-injected services. Card-style instances would live as DI services or factory-spawned transients. Fluxor takes no position on instance lifetime.

### 1.5 Cortex.Net (C#)

Direct .NET port of MobX with weaving so plain C# properties become observable. Docs: *"with Cortex.Net you don't need to normalize your data, making the library very suitable for very complex domain models."* C# analogue of the MST answer: instances ARE the state; observability is bolted on. Cost: losing the explicit immutable-snapshot guarantee.

### 1.6 Unity ECS / Bevy / Flecs

All three converge: **an entity is just an ID** (often a 64-bit handle with index + generation counter). Components are pure data structs in archetype tables; systems are stateless functions iterating over component sets. There is no `Card` object with methods — there's a `CardComponent` data struct on entity 17, and a `CardPlaySystem` processing `(CardComponent, InHandTag)` pairs. The "where do instances live" question dissolves.

### 1.7 Akka / Orleans (actor systems)

Akka separates `IActorRef` (address/identity) from the actor instance (mutable behavior + state). Restarting an actor swaps the instance but the `IActorRef` stays valid. Orleans goes further with the **virtual actor** model: *"actors are purely logical entities that always exist, virtually... an actor cannot be explicitly created nor destroyed."* `GrainId` is durable identity; runtime materializes the grain on demand and persists state externally through pluggable storage providers. **Strongest precedent for the framework's design.**

### 1.8 Card-game engines (Sabberstone, HearthSim, Forge, XMage)

HearthSim docs: *"every entity in the game is a key-value store of tags, and every single entity has an entity ID. Entities are placed in Zones during an entity's lifecycle, and it will move between zones, which is represented by the ZONE tag."* Sabberstone (C#) and Fireplace (Python) split: a static **CardDef** registry by card ID provides behavior (Effects/Tasks); per-game `Entity` instances carry only the mutable tag bag. boardgame.io is Redux-flavored and represents cards as primitive IDs in arrays inside its `G` state object.

**Summary rule across systems:** *Identity is durable and small (an ID). Behavior is associated with the type, not the instance, and lives in code. Per-instance state is plain data, keyed by ID, and is the only thing in the store.*

---

## 2. Problem 2 — auto-registration on creation

### 2.1 `replaceReducer` + injector (legacy Redux)

The original Redux store exposes `store.replaceReducer(newRoot)`. The `injectReducer` pattern wraps this: keep a `Map<string, Reducer>` next to the store, expose `store.injectReducer(key, reducer)` which mutates the map and rebuilds the combined reducer.

```js
// Legacy redux dynamic reducer injection
function createReducerManager(initialReducers) {
  const reducers = { ...initialReducers };
  let combined = combineReducers(reducers);
  return {
    reduce: (state, action) => combined(state, action),
    add: (key, reducer) => {
      reducers[key] = reducer;
      combined = combineReducers(reducers);
    },
    remove: (key) => {
      delete reducers[key];
      combined = combineReducers(reducers);
    }
  };
}
```

### 2.2 RTK 2.0 `combineSlices().inject()` (modern Redux)

The official answer today. `combineSlices(staticSlice).withLazyLoadedSlices<LazyLoadedSlices>()` returns a reducer that exposes `inject(slice)`; slices expose `slice.injectInto(rootReducer)`. **The store reducer reference does not change** (no `replaceReducer` call). Doc note: *"the added reducer state will not show up in your store until the next action is dispatched"* — directly relevant to in-flight dispatch handling.

```ts
const rootReducer = combineSlices(usersSlice)
  .withLazyLoadedSlices<{ cards: CardsState }>();

// later, when a Card object spawns:
const injectedCards = cardsSlice.injectInto(rootReducer);
// reducer reference is stable; selectors fall back to initial state until dispatch
```

### 2.3 redux-dynamic-modules (Microsoft)

Pre-RTK-2 library. A "module" bundles `{id, reducerMap, middlewares, sagas}`. A `<DynamicModuleLoader>` React component adds the module on mount and removes on unmount. **This binds slice lifetime to component lifetime — the closest JS-world precedent for "ensure slice X is registered when object X exists, torn down when X dies."**

### 2.4 Unity prefabs + Mirror / PUN / NGO networked spawn

Mirror: `RegisterPrefab` once with `NetworkManager`, then `NetworkServer.Spawn(go)` instantiates on the server and replicates to clients. Each `NetworkBehaviour` declares `[SyncVar]` fields auto-replicated. Photon PUN follows the same shape with `IPunPrefabPool` + `PhotonNetwork.Instantiate`. **The prefab is a manifest** that lists both components (behavior) and replicated fields (state) — registering the prefab tells the system "these state buckets exist."

### 2.5 ECS bundles / archetypes / prefabs (Bevy, Flecs, Unity DOTS)

Bevy `Bundle` is a tuple of components; `commands.spawn(MyBundle::default())` inserts them all atomically. Flecs prefabs: `world.entity().is_a(prefab)` adds an `IsA` relationship; inheritable components are shared, instance-specific ones copied. Unity DOTS uses `EntityArchetype` and `EntityManager.Instantiate(prefab)`. **Unifying principle: spawning a typed template implicitly registers all the right component buckets.**

```rust
// Bevy bundle example - one spawn call atomically inserts all components
#[derive(Bundle)]
struct CardBundle {
    card: Card,
    in_hand: InHand,
    transform: Transform,
}

commands.spawn(CardBundle::default());
```

### 2.6 DI container as lifecycle manager (Zenject, MS DI, VContainer)

Zenject's `PlaceholderFactory<TParam, T>` is canonical: declare `Card.Factory : PlaceholderFactory<CardData, Card>`, bind in an `Installer`, then `_factory.Create(data)` constructs a fully injected object. **Key move:** a custom `IInitializable` / `IDisposable` on the constructed object can register/unregister the corresponding store slice — lifetime of the object becomes lifetime of the slice. VContainer (already in use) supports the same hooks.

### 2.7 C# source generators for auto-registration

Injectio / AutoRegisterInject use attributes (`[Service]`, `[Singleton]`) and a Roslyn source generator (`ForAttributeWithMetadataName`) to emit `IServiceCollection.AddSingleton<...>()` calls at compile time. Maps cleanly: declare `[StateSlice(typeof(CardState))] public class Card`, generator emits a registration method called by the store at startup. Eliminates reflection and runtime surprises.

### 2.8 ScriptableObject / data-driven manifests

Unity ScriptableObjects let designers author asset files pairing a behavior class (script reference, event channel) with default data. **The asset IS the manifest** — load it and you get both the schema for state and the hooks for behavior. boardgame.io's planned "Game Objects" API and Hearthstone's `CardDefs.xml` are the same pattern in different syntax.

---

## 3. Pattern catalog

| Pattern | Where seen | Pros | Cons | Fit for the framework |
|---|---|---|---|---|
| **ID-as-handle + external registry** | ECS, Akka, Orleans, RTK entity adapter | Tiny identity, easy serialization, replication-friendly | Indirection on every access; selectors / lookups required | Excellent baseline — already in flight |
| **Living tree with derived snapshots** | MobX-State-Tree, Cortex.Net | Ergonomic OO mutation; immutable history for free | Magic (weaving); two-view debugging | Strong if OO feel + Redux benefits desired |
| **Tag bag per entity (key-value)** | Hearthstone, Sabberstone, HearthSim | Trivially serializable; schemaless; matches game-engine semantics | Type safety lost; selectors must reconstruct typed views | Idiomatic for card games — close to `EntityVariableState` |
| **Dynamic reducer injection** | RTK `combineSlices`, redux-dynamic-modules | Slice lifetime can match object lifetime; supports plugins | "When does state appear" race conditions | Translates to C# easily — `ConcurrentDictionary<string, Mutator>` |
| **Prefab/bundle/archetype manifest** | Unity prefabs, Bevy bundles, Flecs prefabs, NGO | Declarative; one source of truth; designer-authorable | Couples authoring tool to runtime | Excellent if designer-driven cards matter |
| **Factory + DI lifecycle hook** | Zenject, MS DI scopes, VContainer | Battle-tested; integrates with existing DI; explicit | Verbose; coupling to DI choice | Natural fit — VContainer + `IGameFactory` already present |
| **Source-generator auto-registration** | Injectio, AutoRegisterInject | Zero-runtime cost; refactor-safe; visible at type definition | Roslyn complexity; build-time tooling | Strong fit — gives `[StateSlice]` ergonomics |
| **Virtual actor / activation-on-demand** | Orleans grains | Identity perpetual; state persists across activations; scalable | Heavy infra; designed for distributed | Useful if multiplayer is on the roadmap |

---

## 4. Recommended paths

The framework's design (mutable instances + Redux-style store, both bound by ID via `InstanceId`) is essentially a local re-implementation of the Orleans/ECS philosophy. Four concrete paths, listed against where the framework is today.

### Option A — Pure ID-as-handle, instances as DI services

Store holds slices keyed by `InstanceId`. Instances live in `IEntityRegistry`. Each instance has constructor-injected store reference and dispatches actions to mutate its own slice; reads always go through the store. `Spawn` calls `store.RegisterSlice(id, initialState, mutators)`; `Destroy` calls `store.UnregisterSlice(id)`. Mirrors RTK `combineSlices().inject()` directly.

```csharp
// Sketch: generalize spawn to register N slices atomically
public sealed class EntityRegistry : IEntityRegistry
{
    private readonly Store _store;
    private readonly Dictionary<InstanceId, IDisposable> _sliceLeases = new();

    public StateEntity<TDef> Spawn<TDef>(TDef definition) where TDef : EntityDefinition
    {
        var id = InstanceId.NewFor(definition);
        var lease = _store.RegisterSlices(id, definition.DeclareInitialSlices(id));
        _sliceLeases[id] = lease;
        return new StateEntity<TDef>(id, definition, _store);
    }

    public void Destroy(InstanceId id)
    {
        if (_sliceLeases.Remove(id, out var lease)) lease.Dispose();
    }
}
```

- **Best when:** maximum Redux-purity matters; want to serialize the store standalone.
- **Tradeoff:** lots of `store.GetState<CardState>(id)` indirection; awkward without selector helpers.
- **Distance from today:** small. Gap is generalizing slice registration so spawn registers *more than just* `EntityVariableState`.

### Option B — Source-generated `[Stateful]` recipes

Author declares the entity once; a Roslyn source generator emits registration into a static `SliceCatalog`, partial `OnCreated` / `OnDestroyed` that auto-register / unregister slices, and a strongly-typed `State` accessor. The "recipe" is the attribute + data class together. Closest C# analogue to a Bevy bundle or Flecs prefab.

```csharp
// Author writes this:
[Stateful(typeof(CardState))]
[Stateful(typeof(CardModifierState))]
public partial class Card : StateEntity<CardDef>
{
    public void Flip() => Store.Execute(new FlipCard(Id));
}

// Generator emits this (rough shape):
partial class Card
{
    public CardState State => Store.Get<CardState>(Id);
    public CardModifierState Modifiers => Store.Get<CardModifierState>(Id);

    static Card() => SliceCatalog.Register<Card>(
        new[] { typeof(CardState), typeof(CardModifierState) });

    partial void OnCreated() => Store.RegisterSlices(Id, SliceCatalog.For<Card>());
    partial void OnDestroyed() => Store.UnregisterSlices(Id);
}
```

- **Best when:** team is comfortable with source generators; designer-friendly ergonomics matter.
- **Tradeoff:** generators are notoriously hard to debug; build-time errors confuse new contributors.
- **Distance from today:** medium. New tooling, architecturally a code-gen layer over Option A.

### Option C — ScriptableObject / manifest-driven prefab pattern

Each card type is a ScriptableObject (Unity) holding (a) serialized `CardData` (state defaults + schema) and (b) reference to the C# class implementing behavior. `IGameFactory.Spawn(prefabId)` reads the manifest, instantiates the behavior class, and registers slices initialized from prefab defaults. Mirrors Mirror's networked-prefab registration and Hearthstone's CardDefs.

```csharp
[CreateAssetMenu(menuName = "Cards/Card Definition")]
public sealed class CardDef : EntityDefinition
{
    [SerializeField] private CardState _initialState;
    [SerializeField] private SerializableType _behaviorType; // implements IEntityBehavior

    public override IEnumerable<SliceSeed> DeclareInitialSlices(InstanceId id)
    {
        yield return SliceSeed.Of(id, _initialState);
        yield return SliceSeed.Of(id, EntityVariableState.Empty);
    }

    public override IEntityBehavior CreateBehavior(IObjectResolver di) =>
        (IEntityBehavior)di.Resolve(_behaviorType.Value);
}
```

- **Best when:** designers will author cards without touching code; engine is Unity already.
- **Tradeoff:** ties framework to engine asset pipeline; harder headless (AI bot, server).
- **Distance from today:** medium. `Definition` already plays this role for behavior; this option formalizes it as a manifest with explicit slice declarations.

### Option D — MST-style living tree with auto-snapshotting (Cortex.Net or hand-rolled)

Instances ARE the state; properties are observable; an interceptor takes immutable snapshots after each action for time-travel / serialization. No registry needed because the object graph IS the registry.

- **Best when:** ergonomics over Redux orthodoxy; willing to take Cortex.Net-style dependency.
- **Tradeoff:** loses "the store is the only source of truth"; weaving magic obscures debugging; networking / sync becomes harder.
- **Distance from today:** large. Contradicts the canonical-state model. Probably not a fit.

**Recommendation seed:** the strongest hybrids are **A + B** (Redux store + source-gen recipes) or **A + C** (Redux store + ScriptableObject manifests). Option A gives the architectural backbone (~70% built); B or C gives the auto-registration ergonomics. **Avoid mixing D with A** — the philosophies fight each other.

---

## 5. Open questions for follow-up

- Should slice registration be **atomic across multiple slices** (Bevy-bundle style), or one-at-a-time (RTK `inject` style)? Atomic avoids partial-spawn states but complicates the recipe API.
- Is there a need for **slice lifetime independent of instance lifetime** (e.g., a slice that survives entity destruction for replay/history)? If yes, leases can't be 1:1 with instance lifetime.
- How does dynamic registration interact with the **in-flight dispatch open item** (`OPEN-ITEMS.md`)? RTK explicitly defers visibility until the next dispatch — does the framework adopt the same rule?
- If Option B (source generators) is chosen, **how do designers without C# fluency author cards?** Probably forces a hybrid with Option C anyway.
- Does **multiplayer / replication** (Orleans, Mirror) belong in scope now? If yes, the registry abstraction should be designed with remote handles in mind from the start.

---

## 6. Sources

**Redux ecosystem**
- https://redux-toolkit.js.org/api/createEntityAdapter
- https://redux.js.org/usage/code-splitting
- https://redux-toolkit.js.org/api/combineSlices
- https://github.com/reduxjs/redux-toolkit/blob/master/docs/api/combineSlices.mdx
- https://github.com/microsoft/redux-dynamic-modules
- https://github.com/redux-orm/redux-orm
- https://blog.isquaredsoftware.com/2016/10/practical-redux-part-1-redux-orm-basics/

**MobX / MST**
- https://mobx-state-tree.js.org/intro/philosophy
- https://mobx-state-tree.js.org/concepts/snapshots
- https://github.com/jspuij/Cortex.Net

**C# Flux / Redux**
- https://github.com/mrpmorris/Fluxor
- https://github.com/GuillaumeSalles/redux.NET
- https://code-maze.com/fluxor-for-state-management-in-blazor/

**ECS**
- https://en.wikipedia.org/wiki/Entity_component_system
- https://unity.com/ecs
- https://bevy-cheatbook.github.io/programming/ec.html
- https://docs.rs/bevy/latest/bevy/ecs/bundle/trait.Bundle.html
- https://github.com/SanderMertens/flecs
- https://www.flecs.dev/flecs/md_docs_2PrefabsManual.html
- https://ajmmertens.medium.com/doing-a-lot-with-a-little-ecs-identifiers-25a72bd2647

**Actor systems**
- https://doc.akka.io/libraries/akka-core/current/typed/actor-lifecycle.html
- https://learn.microsoft.com/en-us/dotnet/orleans/overview
- https://github.com/dotnet/orleans

**Card engines**
- https://hearthsim.info/docs/gamestate-protocol/
- https://github.com/HearthSim/SabberStone
- https://card-forge.github.io/forge/
- https://github.com/magefree/mage
- https://github.com/boardgameio/boardgame.io
- https://github.com/boardgameio/boardgame.io/issues/455

**Unity prefabs / networking**
- https://mirror-networking.gitbook.io/docs/manual/guides/gameobjects/custom-spawnfunctions
- https://docs-multiplayer.unity3d.com/netcode/current/basics/object-spawning/
- https://doc.photonengine.com/pun/current/gameplay/instantiation
- https://unity.com/how-to/separate-game-data-logic-scriptable-objects
- https://docs.unity3d.com/Manual/class-ScriptableObject.html

**DI / source generators**
- https://github.com/modesttree/Zenject/blob/master/Documentation/Factories.md
- https://github.com/loresoft/Injectio
- https://github.com/dotnet/roslyn/blob/main/docs/features/source-generators.md
- https://devblogs.microsoft.com/dotnet/introducing-c-source-generators/
