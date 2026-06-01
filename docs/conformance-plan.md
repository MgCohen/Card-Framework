# Conformance plan — lookup-pattern migration

Staged implementation plan to bring `Assets/Framework/` onto the lookup-pattern standard
decided in the 2026-05-31 design pass.

**Read first (context):**
- [lookup-patterns.md](lookup-patterns.md) — the standard being conformed to (Registry · Router / Store · Cell, `TypeKey`, dual code/data authoring, store write-internal/read-public).
- [framework-review.md](framework-review.md) — the findings each PR closes (referenced by ID, e.g. B1, D1).
- [feature-layout.md](feature-layout.md) — asmdef / vertical-slice rules.
- [coding-standards.md](coding-standards.md) — `int`/`Guid` identity, `int`-backed open type-sets.

**Working rules (every PR):**
- Cut a branch from `main`; PR back into `main`. One PR per stage below.
- **Verify in headless batch mode before claiming done** — the run is the authority:
  ```
  Unity.exe -batchmode -nographics -projectPath . -runTests \
    -testPlatform EditMode -testCategory "!Benchmark" \
    -assemblyNames <the touched .Tests asms> \
    -testResults Logs/test-results.xml -logFile Logs/test.log
  ```
  Read `Logs/test-results.xml` (pass/fail) and `Logs/test.log` (compile errors / stacks).
- Do **not** strip comments in these PRs (separate track, finding A1). Touch only what the stage scopes.
- Leave the `Block*` events in Wire (concept, not yet earned — see Q1).

**Dependency order:** PR1 (subtraction) → PR2 (foundation) → {PR3, PR4, PR5 siblings} → PR6 (store discipline). PRs 3/4/5 may land in any order once PR2 is in. **The series is complete after PR6** — PR7 was reclassified out (see below).

---

## PR1 — Subtraction + event homes

**Goal:** delete dead/legacy symbols and relocate Entity events to their feature. Pure subtraction + one move; shrinks the surface before the structural PRs. Closes **E1, E2, E4, E3(partial), D1(partial)**.

**Delete (grep-confirmed dead / legacy):**
- `Request<TResponse>.ResponseType` — [Requests/Runtime/Request.Typed.cs](Assets/Framework/Requests/Runtime/Request.Typed.cs). Property never read; the file may then be empty of members — keep `Request<TResponse>` as the typed base (it still adds `TResponse` for `Open` inference), just drop the property.
- `Network.GetChannel<T>()` — [Wire/Runtime/Network/Network.cs](Assets/Framework/Wire/Runtime/Network/Network.cs). Zero callers; `Register` already returns the channel.
- `Vec3` — [Wire/Runtime/Primitives/Vec3.cs](Assets/Framework/Wire/Runtime/Primitives/Vec3.cs) (+ `.meta`). Only a Wire test fixture uses it — update/remove that fixture (`Wire/Tests/Fixtures/EntityMovedEvent.cs`) or give it a local test-only vector.
- `EntityDestroyedEvent` — [Wire/Runtime/Events/EntityDestroyedEvent.cs](Assets/Framework/Wire/Runtime/Events/EntityDestroyedEvent.cs). No destroy service/handler exists; rebuild it *with* the destroy path when earned. Remove its round-trip tests too.
- Wire's legacy request vocabulary — `RequestOpenedEvent`, `RequestClosedEvent`, `ResponseEvent` under [Wire/Runtime/Events/](Assets/Framework/Wire/Runtime/Events/). The graduated `Requests` feature owns the real `Request`/`Response`; these Wire copies are only used by Wire's own round-trip tests. Remove the events + those tests.

**Move (D1):**
- `EntityCreatedEvent` from `Framework.Wire` → `Framework.Entities` (new `Entities/Runtime/Events/`). It's emitted by `EntityService` and applied by `EntityCreatedHandler`, both already in `Framework.Entities` — only the event type is misplaced.
- Update namespace `Framework.Wire` → `Framework.Entities` on the type; fix `using`s in `EntityService`, `EntityCreatedHandler`, and any tests.
- Confirm `Framework.Entities.asmdef` already references `Framework.Wire` (it does) — the event still derives from `WireEvent` and uses `[Packed]`, so it needs Wire + Scaffold.Autopacker. **Check `Framework.Entities.asmdef` references `Scaffold.Autopacker`** (currently it references `Framework.Wire`, `Framework.Tags`, `Scaffold.AppFlow` — may need Autopacker added for the `[Packed]` attribute + generated `.Packed` struct).

**Gotchas:**
- Wire's round-trip tests (`Wire/Tests/RoundTripTests.cs`, `TwoEndTests.cs`, `EventStreamDispatchTests.cs`) lean heavily on these event types as convenient fixtures. Don't delete coverage — re-point them at Wire's own synthetic fixtures (`Wire/Tests/Fixtures/*`). The goal is Wire tests prove *transport*, not domain events.
- AutoPacker generates a nested `.Packed` struct per `[Packed]` event; moving the event regenerates it under the new namespace. Rebuild and confirm no stale generated refs.

**Touched test asms:** `Framework.Wire.Tests`, `Framework.Entities.Tests`.
**Done when:** both test asms green in batch mode; no refs to the deleted types remain (grep clean); `EntityCreatedEvent` resolves from `Framework.Entities`.

---

## PR2 — `TypeKey` + `SlotRegistry<T>` foundation

**Goal:** add the shared substrate the type-set PRs build on. Pure addition, no existing callers changed. Closes nothing on its own; unblocks PR3/4/5. (C3/I2 land when the old registries are deleted in 3/4.)

**Where:** these are shared primitives. Per [feature-layout.md](feature-layout.md), pure-data zero-behavior shared types go in `Assets/Framework/Core/`. `TypeKey` is a bare record (data); `SlotRegistry<T>` has behavior but is generic infra. Decide at build time: **`Core` if it should be feature-agnostic, or duplicate-free home in each feature.** Lean: `Framework.Core` (new feature/asmdef `Framework.Core`) holding `TypeKey` + `SlotRegistry<T>`, referenced by Entities/Tags/States. Confirm no circular asmdef refs (Core must depend on nothing framework-side).

**Add `TypeKey`:**
```csharp
public abstract record TypeKey(int Slot);
```

**Add `SlotRegistry<TKey>` (instance, not static):**
```csharp
public class SlotRegistry<TKey> where TKey : TypeKey
{
    readonly Dictionary<int, TKey>  _bySlot = new();
    readonly Dictionary<Type, TKey> _byType = new();   // code members only

    public void Register<T>() where T : TKey, new()
    {
        var k = new T();
        Add(k);
        _byType[typeof(T)] = k;
    }

    public void Register(TKey member) => Add(member);   // data path

    public TKey BySlot(int slot)                 => _bySlot[slot];
    public bool TryBySlot(int slot, out TKey k)  => _bySlot.TryGetValue(slot, out k);
    public T    Of<T>() where T : TKey           => (T)_byType[typeof(T)];

    void Add(TKey member)
    {
        if (_bySlot.TryGetValue(member.Slot, out var clash))
            throw new InvalidOperationException(
                $"Slot {member.Slot} already claimed by {clash.GetType().Name}.");
        _bySlot[member.Slot] = member;
    }
}
```

**Notes:**
- `Register<T>` populates both maps (slot + type); `Register(instance)` only the slot map (data members have no compile-time type → no `Of<T>`).
- Keep `TryBySlot` even with no caller yet — it's standard surface (finding E5).
- Add a focused `Framework.Core.Tests` proving: register-by-type, register-by-instance, dup-slot throws, `BySlot`/`Of<T>` resolve, `TryBySlot` miss returns false.

**Touched test asms:** `Framework.Core.Tests` (new).
**Done when:** new asm green; nothing else references it yet.

---

## PR3 — EntityType → records

**Goal:** convert `EntityType` to the record/registry standard. Closes **D2, I2(partial), C3(partial)**; deletes the static table. Depends on **PR2**.

**Change `EntityType`** ([Entities/Runtime/EntityType.cs](Assets/Framework/Entities/Runtime/EntityType.cs)):
- From `readonly struct EntityType` (Slot + Name + `IEquatable`) → `public abstract record EntityType(int Slot) : TypeKey(Slot);`
- Drop `Name` (the record type name is the identity; `ToString` is free). Audit `Name` users — `EntityType.ToString()` returned `Name`; anything logging it now gets the type name.

**Baselines** → records (replaces the `EntityTypes` static table, [Entities/Runtime/EntityTypes.cs](Assets/Framework/Entities/Runtime/EntityTypes.cs), **delete it**):
```csharp
public sealed record Unknown  : EntityType(0);
public sealed record Player   : EntityType(2);
public sealed record Zone     : EntityType(3);
public sealed record Card     : EntityType(4);
public sealed record Modifier : EntityType(5);
```
(Slot numbers preserved from the old table.)

**Registry** → instance ([Entities/Runtime/Registry/EntityTypeRegistry.cs](Assets/Framework/Entities/Runtime/Registry/EntityTypeRegistry.cs)):
- Replace the static class with `sealed class EntityTypeRegistry : SlotRegistry<EntityType>` (or just use `SlotRegistry<EntityType>` directly if no entity-specific members are needed — lean: a thin subclass for naming/DI clarity).

**Registration** → composition root ([Bootstrap/Runtime/EntityLayer.cs](Assets/Framework/Bootstrap/Runtime/EntityLayer.cs)):
- Register `EntityTypeRegistry` as a scoped singleton.
- Register the five baseline records via a build callback (mirror the `RegisterStateType` extension pattern in [HandlerRegistrationExtensions.cs](Assets/Framework/Bootstrap/Runtime/HandlerRegistrationExtensions.cs)).

**Consumers:**
- `EntityCreatedHandler` ([Entities/Runtime/Handlers/EntityCreatedHandler.cs](Assets/Framework/Entities/Runtime/Handlers/EntityCreatedHandler.cs)) — inject `EntityTypeRegistry`; `EntityTypeRegistry.BySlot(slot)` static call → `_registry.BySlot(ev.TypeSlot)`. **This is the I2 fix** (handler no longer reaches a global).
- `Entity.Type` ([Entities/Runtime/Entity.cs](Assets/Framework/Entities/Runtime/Entity.cs)) stays `EntityType` (now a record ref instead of a struct value — semantic shift from `==` to `is`/record-equality; audit comparisons).
- `IEntityDefinition.Type`, `EntityStoreExtensions.From`, `Catalog` — `EntityType` is still the type; only its kind (struct→record) changed. Verify `Catalog<TDef>`'s `Dictionary<TDef,int>` isn't affected (it keys on TDef, not EntityType).
- Tests: `EntityTypeRegistryTests`, `EntityTypeTests`, `EngineReservedSlotsTests`, `EntityStoreTests`, `EntityTests` — update to records + injected registry. `EngineReservedSlotsTests` specifically encodes the static-global reserved-slot behavior (I2 symptom) — revisit its assertions against the now-scoped registry.

**Gotchas:**
- `EntityType` struct→record is value→reference. Default equality changes (record structural equality on `Slot`, but two different records with same slot won't occur due to dup-guard). Hashing/dictionary use of `EntityType` as a key: check `EntityTypeRegistry` no longer needs the old `Equals/GetHashCode` (those were on the struct).
- Static-field registration is gone → registration now only happens if the layer runs. Tests that used `EntityTypes.Card` to trigger registration must now register explicitly (or resolve from the container).

**Touched test asms:** `Framework.Entities.Tests`, `Framework.Bootstrap.Tests`.
**Done when:** both green; `EntityTypes` table deleted; handler uses injected registry; no static `EntityTypeRegistry` references remain.

---

## PR4 — Tags → records + typed store (no boxing)

**Goal:** Tag keys to records, TagStore to per-value-type maps, registry to instance, instance-based API. Closes **B1, D2(tags), I2(tags)**. Depends on **PR2**. (T1/T2/T3 from the design pass.)

**Pre-check (T1 assumption):** confirm `Scaffold.Maps.Map<int,int,V>` stores a value-type `V` **inline** (no per-value boxing). Read the package source under `Library/PackageCache/com.scaffold.maps*` or `C:\Unity\Scaffold`. If it boxes internally, T1's design needs revisiting before coding.

**Tag key hierarchy** ([Tags/Runtime/TagKey.cs](Assets/Framework/Tags/Runtime/TagKey.cs)) — class → record:
```csharp
public abstract record Tag(int Slot) : TypeKey(Slot)
{
    public abstract Type ValueType { get; }
}
public abstract record Tag<T>(int Slot) : Tag(Slot) where T : unmanaged
{
    public override Type ValueType => typeof(T);
}
```
- Remove the ctor self-registration side-effect (the old `TagKey` called `TagRegistry.RegisterInstance(this)` in its ctor — **gone**; registration is explicit now).
- Concrete tags become records: `public sealed record Power : Tag<int>(10);` (games declare their own).

**Registry** → instance ([Tags/Runtime/Registry/TagRegistry.cs](Assets/Framework/Tags/Runtime/TagRegistry.cs)): `SlotRegistry<Tag>` (or thin subclass). Drop `RegisterInstance`; the `All` accessor — check callers before removing (grep showed none in runtime).

**TagStore** ([Tags/Runtime/TagStore.cs](Assets/Framework/Tags/Runtime/TagStore.cs)) — `Map<int,int,object>` → per-type maps:
```csharp
public sealed class TagStore
{
    readonly Dictionary<Type, ITagMap> _byType = new();

    interface ITagMap { bool Has(int e, int s); bool Remove(int e, int s); }
    sealed class TagMap<T> : ITagMap where T : unmanaged
    {
        readonly Map<int,int,T> _m = new();
        public T  Get(int e, int s) => _m[e, s];
        public void Set(int e, int s, T v) => _m[e, s] = v;
        public bool Has(int e, int s) => _m.Contains(e, s);
        public bool Remove(int e, int s) => _m.Remove(e, s);
    }
    // typed Set/Get<T> route to the T map; Has/Remove route via the key's ValueType.
    // value-type maps created at RegisterTagValueType<T>() (see below).
}
```
- **Note ITagMap/TagMap are nested** (allowed — they're the implementation of one Store, not free-floating types).
- `Has`/`Remove` are untyped (take slot) — they must find the right `TagMap` without knowing `T`. Route via `TagRegistry.BySlot(slot).ValueType` → look up `_byType[valueType]` → call through `ITagMap`.

**API** — `TagService` ([Tags/Runtime/TagService.cs](Assets/Framework/Tags/Runtime/TagService.cs)) keeps instance-based shape (T3); only the key type changes `TagKey<T>` → `Tag<T>`:
- `Get<T>(int e, Tag<T> key)`, `Set<T>(int e, Tag<T> key, T value)`, `Has(int e, Tag key)`, `Remove(int e, Tag key)`. The dedupe/emit logic is unchanged.
- Handlers `TagSetHandler<T>`/`TagChangeHandler<T>`/`TagRemovedHandler` — write through the typed store; signatures follow `T`.

**Registration** ([Bootstrap/Runtime/TagLayer.cs](Assets/Framework/Bootstrap/Runtime/TagLayer.cs) + [HandlerRegistrationExtensions.cs](Assets/Framework/Bootstrap/Runtime/HandlerRegistrationExtensions.cs)):
- `RegisterTagValueType<T>()` already wires Set+Change handlers; **also create the `TagMap<T>`** in the store for that `T` (the per-type map's creation point). Removed handler stays registered here (D6 asymmetry can be tidied but isn't required).
- Register `TagRegistry` as scoped singleton; register baseline tag value-types (`int`, `bool`) as today.

**Gotchas:**
- The store must be told which value types exist *before* any tag op. `RegisterTagValueType<T>` is that hook — ensure it runs at build (it does, via the layer).
- `Has`/`Remove` depend on the registry to recover `T` from slot → the store needs the registry (inject it) **or** the service resolves `T` and calls a typed store path. Lean: service already has the typed key on `Remove(Tag key)` via `key.ValueType` — but `Remove` is non-generic. Decide at code time whether store holds a registry ref or whether `Has`/`Remove` move to where `T`/the key is known. Simplest: store injects `TagRegistry`.

**Touched test asms:** `Framework.Tags.Tests` (if any), `Framework.Bootstrap.Tests`, `Framework.Entities.Tests` (uses `SampleTagsContainer`/tag round-trips). Check `Entities/Tests/SampleTagsContainer.cs`, `MirrorIntegrationTests`, `WireRoundTripTests` for tag usage.
**Done when:** green; no boxing in TagStore (values stored in `Map<int,int,T>`); no ctor self-registration; keys are records.

---

## PR5 — State → slot-based wire

**Goal:** move State from type-discriminated to slot-discriminated wire; kinds = Registry, cursor = Cell. Closes **F1**; collapses N channels/handlers → 1. Depends on **PR2**. *Reworks tested code — the "fully bold" cost; re-confirm worth before starting (see self-review).* 

**Kinds** ([States/Runtime/GameState.cs](Assets/Framework/States/Runtime/GameState.cs)) — add slots, split file (F1):
```csharp
// GameState.cs
public abstract record GameState(int Slot) : TypeKey(Slot);
// MatchNotStartedState.cs, PlayingState.cs, MatchEndedState.cs — one per file
public sealed record MatchNotStartedState : GameState(0);
public sealed record PlayingState         : GameState(1);
public sealed record MatchEndedState      : GameState(2);
```
- **Behavior note:** `GameState` is slated to gain `OnEnter`/`OnExit`/`Actions` in the 5c pipeline. Slots don't block that — behavior is added later on the record. Keep zero-data for now.

**Wire** ([States/Runtime/Events/StatePushEvent.cs](Assets/Framework/States/Runtime/Events/StatePushEvent.cs)) — generic-per-type → single slotted event:
```csharp
public partial class StatePushEvent : WireEvent { [Packed] public int Slot; }
```
- This collapses `StatePushEvent<TState>` (one channel + handler per state) into one channel + one handler.

**Handler** ([States/Runtime/Handlers/StatePushHandler.cs](Assets/Framework/States/Runtime/Handlers/StatePushHandler.cs)) — one handler, resolves via registry:
```csharp
public override void Apply(StatePushEvent ev) => _store.Set(_registry.BySlot(ev.Slot));
```

**Registry** — new `SlotRegistry<GameState>` instance (States had no registry before; the type-discriminated wire did the work). Register baselines + games register their own.

**Service** ([States/Runtime/StateService.cs](Assets/Framework/States/Runtime/StateService.cs)) — `Push<TState>()` stays as the ergonomic verb but now emits a slotted event:
```csharp
public void Push<TState>() where TState : GameState, new()
    => _events.Emit(new StatePushEvent { Slot = new TState().Slot });
// or Push(GameState) instance overload for data-resolved states
```

**Store** ([States/Runtime/StateStore.cs](Assets/Framework/States/Runtime/StateStore.cs)) — it's a **Cell**; `Current` get / internal `Set`. Initialize to the registered `MatchNotStartedState`. Mostly unchanged; just recognized as Cell + resolves initial via registry.

**Registration** ([Bootstrap/Runtime/StateLayer.cs](Assets/Framework/Bootstrap/Runtime/StateLayer.cs) + extension) — `RegisterStateType<T>` previously wired a `StatePushHandler<T>` + channel per state. Now: register the **one** handler + **one** channel once, and `RegisterStateType<T>` just registers the kind into the `SlotRegistry<GameState>`.

**Gotchas:**
- This is the biggest behavior change to working, fully-tested code. `StateServiceTests` exercises the per-type push round-trip — rewrite for the slotted event.
- One channel for all states means the channel registration moves out of per-state into the layer's fixed setup. Confirm `Network.Register<StatePushEvent, StatePushEvent.Packed>()` happens once.
- Zero-data `StatePushEvent<TState>` previously had an empty `.Packed`; now there's a real `int Slot` field — AutoPacker generates a non-empty `.Packed`. Verify round-trip.

**Touched test asms:** `Framework.States.Tests`, `Framework.Bootstrap.Tests`.
**Done when:** green; single `StatePushEvent`/handler/channel; `GameState.cs` split; `StateStore` documented as Cell.

---

## PR6 — Store discipline (visibility + allocation + verbs + engine surface)

**Status: landed.** Scope refined during the design pass vs the original write-up below:
- **Verb alignment collapsed to one rename** (`Catalog.GetById → ById`). The audit found the
  inserts genuinely diverge by dup-contract (idempotent `EntityStore.Insert` / strict
  `Catalog.Add` / overwrite `RequestStore.Insert`) and the typed `RequestStore.Get<T>` is a
  different contract, not a misnamed read — so those names stay. `TryGetById` left as-is.
- **No `Store<T>` base class.** Only the read pair is identical across stores; too thin to
  justify a base over three different containers. Standardized by convention in
  [lookup-patterns.md](lookup-patterns.md) instead (mirrors Router's "contract not class").
- **No `InternalsVisibleTo` changes.** Wire's round-trip tests use their own fixture `Catalog`
  (not the real `Catalog<TDef>`), and every real-store writer is same-asm or already IVT'd.
- **Read-only views live per-feature** (`IReadOnlyEntityStore`, etc.), not in `Engine` — Engine
  references the features, not vice-versa.

**Goal:** enforce the Store contract uniformly. Closes **I4, G2**; aligns verbs; makes write-internal/read-public real. Best after stores have settled into their record/typed forms (PR3/4/5).

**EntityStore allocation → service (I4)** ([Entities/Runtime/EntityStore.cs](Assets/Framework/Entities/Runtime/EntityStore.cs), [EntityService.cs](Assets/Framework/Entities/Runtime/EntityService.cs)):
- Remove `AllocateId()` and the auto-id `Insert(EntityType, int)` from the store. `EntityService` owns `_nextId` and allocates (mirrors `RequestService`).
- Store keeps only explicit-id `Insert(int id, …)` (idempotent), `ById`, `TryById`, `Remove`.
- The auto-id `Insert` was test-convenience — update `EntityStoreTests` to allocate via service or pass explicit ids. `EntityStoreExtensions.From` (used by Modifiers spike + tests) currently calls auto-id `Insert` — re-point to explicit-id or a test helper.

**Verb alignment (all Stores):** pick canonical `Insert(id,…)` / `ById` / `TryById` / `Remove`. Current divergence:
- `EntityStore.Insert`/`ById`/`TryGetById` → `TryById`.
- `Catalog.Add`/`GetById`/`TryGetById` → `Insert`/`ById`/`TryById` (keep `GetId` reverse).
- `RequestStore.Insert`/`Get<T>`/`Remove` → `ById`? (note `Get<T>` does a typed cast — keep typed variant but align the name).
- `TagStore.Set`/`Get` — composite-key Store; `Set`/`Get` read naturally for key-value, **exempt from id-verb alignment** (it's `(entity,slot)→value`, not `id→entry`). Document the exemption.

**Write-internal / read-public (G2 enabler):**
- Make all store **mutators `internal`** (`Insert`/`Remove`/`Set`): `EntityStore`, `Catalog`, `RequestStore`, `TagStore`, `StateStore.Set` (already internal). Reads stay `public`.
- Apply-handlers reach internals via `InternalsVisibleTo` — confirm each feature's `AssemblyInfo.cs` exposes internals to the handler assembly (handlers are in the same asm today, so internal access is free; verify).

**GameEngine surface (G2)** ([Engine/Runtime/GameEngine.cs](Assets/Framework/Engine/Runtime/GameEngine.cs)):
- Stop exposing writable stores. Expose **services** (author) + **read-only store views** for query.
- Add minimal read-only interfaces (`IReadOnlyEntityStore { Entity ById(int); bool TryById(int, out Entity); }`, etc.) implemented by the stores; `GameEngine` exposes those, not the concrete stores.
- Keep `Stream`/`Network` as today (substrate).

**Gotchas:**
- `EntityService.Create` currently does emit-then-`_store.ById(id)` read-back (D3/H1). With allocation on the service, the flow is: service allocates id → emits → handler inserts → service returns `ById(id)`. Still relies on synchronous dispatch; not fixing that fragility here (separate track), just moving allocation. Note it.
- Read-only views: don't over-build. One interface per store with the actual read methods consumers use. Client UI also benefits (reads, never writes).

**Touched test asms:** `Framework.Entities.Tests`, `Framework.Requests.Tests`, `Framework.Tags.Tests`, `Framework.Bootstrap.Tests`, `Framework.Engine` consumers.
**Done when:** green; store mutators internal; `GameEngine` exposes services + read-only views only; verbs aligned; allocation lives in services.

---

## PR7 — Session lobby collections — RECLASSIFIED (not a conformance gap)

**Dropped from the conformance track.** The original framing — "bring `Session`'s ad-hoc
collections onto the Store shape" — assumed the collections want to *be* Stores. They don't.
Classification is by **responsibility, not structure** (see [lookup-patterns.md](lookup-patterns.md)
→ "Classify by responsibility, not structure"): `Session`'s job is coordinating the network lobby
lifecycle, the `_peers`/`_players`/`_ready` collections are internal bookkeeping, they can't carry
the Store write-discipline (Session writes them itself in receive callbacks — no handler-sole-writer),
and its public `Peers`/`Players` are read projections. Session is the boundary adapter onto Unity
Netcode and is correctly outside this standard.

So the lookup-conformance series **ends at PR6.** The only Session work left — collapsing the
`_peers` List + `IndexBySeat`/`IndexByNetworkId` linear scans into seat-keyed lookups, deduping
peer-removal — is *internal hygiene* on the separate Session-decomposition track (G1, C4, C5, C7),
driven by Session's own size/dup pain, not by lookup conformance. Listed under "Out of scope" below.

---

## Out of scope (separate tracks)

Tracked in [framework-review.md](framework-review.md) → Disposition:
- **Comment strip (A1)** — own pass after conformance; strip headers, keep one-line invariant warnings inline.
- **Non-lookup cleanups** — Wire (C1 dual Emit, C2 wiring paths, I1 unbounded history, I3 byte-id cap, H2), Session/Multiplayer (G1, C4, C5, C7, D4, D5, **+ lobby-collection keying** — the former PR7, now Session-hygiene), Bootstrap (C6, D6, I5), Entities/Requests (E8, F2, H3, H4, D3/H1). Each its own focused PR when prioritized.
