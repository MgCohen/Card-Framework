---
type: spike
status: proposed
created: 2026-10-31
references:
  - docs/lookup-patterns.md
  - docs/coding-standards.md
  - Product/spikes/framework-roadmap.md
  - Product/spikes/entity-identity-model.md
  - Product/spikes/request-service.md
  - Product/spikes/command-service.md
  - Product/decisions/0014-service-first-state-model.md
sandbox: Assets/Framework/Core/Runtime/Ids/
tags: [#spike, #infra, #ids, #records, #wire, #rollback]
---

# Shared infra — record ids and id allocation

> **Status: proposed.** One of four shared-infrastructure spikes for the
> intent / effect / command / event pipeline. The other three are the
> type-keyed router, the block and wire system, and the command pipeline.
> This spike owns instance id spaces, id allocation and the record read names.
> Page: https://claude.ai/artifact/66MvZgwWo8hSEeKJ6KMDms. No code changes yet.

## 1. TL;DR

1. One `IdAllocator<TId>` per id space. It is server-only, a DI singleton in the match scope, and it starts at 1 (0 is `None`). One service calls `Next()`.
2. Ids on the wire are typed `readonly struct`s around `int` (`EntityId`, `RequestId`, `BlockId`, `MessageId`). The wire cost is zero bytes, and the pack cost is the same as for `int` (evidence in §3.2).
3. Keep "no `Store<T>` base". Standardize the read pair `ById` / `TryGetById` over the typed id, by convention and by rule.
4. No code outside the owning asmdef can make an id from an `int` or read the `int` back.
5. Out of scope: inverse events (rollback) and "messages hold ids" (pipeline). See §2a and the appendix.

### Decisions (2026-10-31)

The user made these decisions for all four infra spikes:

1. Ids are typed `readonly struct`s around `int`: `EntityId`, `RequestId`, `BlockId`, `MessageId`. This spike's proposal is accepted.
2. Shared infrastructure lives in `Framework.Core`: routers, `IdAllocator<TId>`, `EntityId`. `Framework.Wire` references `Framework.Core`. This needs an ARCH-02 edit. This spike does not edit `rulebook.json`.
3. A new `Framework.Blocks` feature owns all `Block*` types and `BlockId`. `Framework.Wire` does not.
4. The async core uses `System.Threading.Tasks.Task`, not UniTask.
5. The block events carry `CauseId` only. The nesting parent comes from the order of the brackets. There is no `ParentId` on the wire and no parent override.
6. An intent opens no block.
7. Hooks exist only at the command level.

### Decisions (2026-11-01)

8. Kind ids (tag, entity type, game state and block kind slots) are a different pattern from record ids. They moved to `infra-kind-ids.md`. This spike covers instance ids only.
9. Scope (§2a) is approved. "Events carry their inverse" and "messages hold ids" are out. Their design text moves to the appendix and is kept for the owners.
10. Typed ids have no public `int`. The constructor and `Value` are `internal` to the owning asmdef. `Framework.Core` makes the entity allocator (`EntityIds.NewAllocator()`), because `EntityId` lives in Core and `EntityService` lives in Entities. Tests get `InternalsVisibleTo` grants on the allowlist rule, as other features do today.
11. An allocator can rewind, but only from the top of a mark stack (last in, first out). Each space sets a policy at registration: `Forward` (default) or `Rewindable`. Undo of a record in the middle leaves a gap; the counter does not move. Ids are never pooled, and there is no generation in the id.
12. Object pooling and disposal are a v2 concern of the feature stores. The idea of a record base nested in the allocator is kept in appendix A.4.

## 2a. Scope

| Concern | In / out | Owner if out |
|---|---|---|
| Typed instance id structs | in | — |
| Access to the `int` inside an id | in | — |
| Allocation: one server allocator for each space | in | — |
| Determinism: start value, save, load | in | — |
| Rewind policy for each space (top of the stack only) | in | — |
| Store read pair over a typed id | in (names only) | — |
| Typed ids on the wire | in | — |
| Events carry their inverse | out | Rollback (no page yet). Design text in the appendix. |
| Messages hold ids, not refs | out | Pipeline (`infra-command-pipeline.md`). Design text in the appendix. |
| Closed requests, request cross-space fields | out | Requests feature (§9 Q2, Q3) |
| Kind slots | out | `infra-kind-ids.md` |
| Block nesting and `BlockStore` | out | `infra-block-wire.md` |
| Client correlation id | out | Pipeline, phase 2 (§9 Q4) |
| Channel ids, seats, definition ids | out | Wire, Multiplayer, Entities catalog (§5) |
| Object pooling and disposal | out | Feature stores, v2 (appendix A.4) |

Boundary: the owning service on the server asks for a new id. The pattern gives back a typed id. The service puts it in its wire event. Stores and the wire use the id after that, and never make one.

## 2. Current shapes

| Copy | Id space | Owner | Wire or local | Where |
|---|---|---|---|---|
| `EntityService._nextId++` | entity | `EntityService` (server) | wire (`EntityCreatedEvent.EntityId`, tag events) | `Entities/Runtime/EntityService.cs:9,19` |
| `RequestService._nextId++` | request | `RequestService` (server) | wire (`Request.Id`, `Response.RequestId`) | `Requests/Runtime/RequestService.cs:11,21` |
| `BlockScope._nextId++` | block | `BlockScope` (server) | wire (`BlockStartEvent.Id`, `BlockEndEvent.Id`) | `Wire/Runtime/Events/BlockScope.cs:8,14` |
| `Network._nextId++` (byte) | channel | `Network` (both sides) | wire header byte, positional from `WireRegistrar` sort | `Wire/Runtime/Network/Network.cs:12,29` |
| `PeerRoster._nextSeat++` | seat | `PeerRoster` via `Session` (host) | wire (`PeerWelcomeEvent.YourSeat`), lobby lifetime | `Multiplayer/Runtime/PeerRoster.cs:10,27`; `Session.cs:255` |
| VS `World._nextBlockId++` (`long`) | block | `World` (shared by effects and commands) | log | `Spike/ECS/VerticalSlice/Engine/Pipeline/World.cs:31,49` |
| VS `RequestsService._nextId++` | request | `RequestsService` | log | `.../Pipeline/RequestsService.cs:25,37` |
| VS `EntityStore._nextId++` | entity | **the store** (breaks "a Store never allocates ids") | log | `.../Pipeline/EntityStore.cs:45,74` |
| `SlotRegistry<TKey>`, `TypeKey(int Slot)` | slot (frozen kinds; see `infra-kind-ids.md`) | composition root | wire (`TypeSlot`, `Slot`) | `Core/Runtime/SlotRegistry.cs:6`, `TypeKey.cs:3` |
| `EntityTypeRegistry`, `GameStateRegistry`, `TagRegistry` | slot | composition root | wire | `Entities/Runtime/Registry/EntityTypeRegistry.cs:5`, `States/.../GameStateRegistry.cs:5`, `Tags/.../TagRegistry.cs:5` |
| `Catalog<TDef>.Add` | definition | authored data (`def.Id`) | wire (`DefinitionId`) | `Entities/Runtime/Catalog.cs:10-22` |
| `Entity.Id` | entity | record | — | `Entities/Runtime/Entity.cs:5` |
| `Request.Id`, `Response.RequestId` | request | record = event | wire | `Requests/Runtime/Request.cs:8`, `Response.cs:8` |
| `EntityStore.ById` / `TryGetById` | entity | store read | — | `Entities/Runtime/EntityStore.cs:18-21` |
| `RequestStore.Get<T>(int)` | request | store read (typed, `null` on miss) | — | `Requests/Runtime/RequestStore.cs:13` |
| `TagRemovedEvent` (no `Prev`) | — | event | wire | `Tags/Runtime/Events/TagRemovedEvent.cs:8-9` |
| `StatePushEvent` (no `Prev`) | — | event | wire | `States/Runtime/Events/StatePushEvent.cs:8` |
| VS `TagRemovedEvent` (has `Prev`) | — | event | log | `.../Engine/Stream/WireEvent.cs:142-151` |
| VS `MirrorWorld.Unapply` | — | mirror | — | `Spike/ECS/VerticalSlice/Mirror/MirrorWorld.cs:64-97` |
| `WireEventHandler<T>.Unapply` throws | — | handler base | — | `Wire/Runtime/Stream/WireEventHandler.cs:11` |
| VS `DealDamage.Target : IEntity` | — | queued command holds a ref | — | `Spike/ECS/VerticalSlice/Sample/DealDamage.cs:20-21` |
| Framework `SetHealth.EntityId : int` | — | command holds an id | — | `Framework/Testing/Support/SampleCommands.cs:13` |

Framework paths are under `Assets/Framework/`. Three findings:

- Five counters have the same shape: a private `int` field and `++`. Four start at 1. `Network` starts at 0 and casts to `byte`. None can be reset or restored. None is shared, so a second block emitter (effects) needs the comment at `BlockScope.cs:7` to stay true.
- Every id is a bare `int`. `Request.ParentId`, `Request.SourceId`, `Request.ResponderId` and `BlockStartEvent.SourceId` do not say which id space they use.
- Two events cannot be undone from their own fields: `TagRemovedEvent` and `StatePushEvent`.

## 3. Generalized contract

### 3.1 Id spaces

| Space | Type | Home asmdef | Allocated by | On the wire | Lifetime |
|---|---|---|---|---|---|
| entity | `EntityId` | `Framework.Core` | `EntityService` | yes | match, never reused |
| request | `RequestId` | `Framework.Requests` | `RequestService` | yes | match, never reused |
| block | `BlockId` | `Framework.Blocks` | `BlockService` (today `BlockScope`) | yes (`Id`, `CauseId`) | match, never reused |
| message | `MessageId` | pipeline asmdef | pipeline `Enqueue` | no (local, see §8) | match |
| channel | `byte` | `Framework.Wire` | `Network.Register` | header byte | process, frozen |
| seat | `int` | `Framework.Multiplayer` | `PeerRoster` | yes (lobby events) | lobby session |
| slot | see `infra-kind-ids.md` | — | — | — | — |
| definition | `int` | authored | nobody (data) | yes | build |

`IdAllocator<TId>` and `EntityId` live in `Framework.Core` (decision 2). `EntityId` must be in Core because `Framework.Tags` uses it and Tags cannot reference Entities (Entities references Tags). `BlockId` lives in `Framework.Blocks` with the block events (decision 3). Then `Framework.Wire` holds no block types, and the ARCH-09 `Block*` exception can go.

### 3.2 Typed id

```csharp
namespace Framework.Core
{
    public readonly struct EntityId : IEquatable<EntityId>
    {
        public static readonly EntityId None = default;

        internal readonly int Value;

        internal EntityId(int value) => Value = value;

        public bool IsNone => Value == 0;

        public bool Equals(EntityId other)      => Value == other.Value;
        public override bool Equals(object obj) => obj is EntityId other && Equals(other);
        public override int GetHashCode()       => Value;
        public override string ToString()       => $"Entity#{Value}";

        public static bool operator ==(EntityId a, EntityId b) => a.Value == b.Value;
        public static bool operator !=(EntityId a, EntityId b) => a.Value != b.Value;
    }
}
```

`RequestId`, `BlockId` and `MessageId` have the same shape. There is no implicit conversion to or from `int`, because an implicit conversion removes the type safety. The constructor and `Value` are `internal` (decision 10). AutoPacker copies the struct as raw bytes, so the wire needs no access to `Value`. Unity C# 9 has no `record struct`, so the members are written by hand.

**Why typed.** `docs/coding-standards.md` allows "a `readonly struct` wrapper around `int`, TagKey-style". `Scaffold.Maps` `Index<TPrimary,TSecondary>` uses `EqualityComparer<T>.Default`, so an `IEquatable` key does not box. The cost is churn at call sites. Code outside the owner cannot index arrays by id; it uses a dictionary.

**AutoPacker cost (verified 2026-10-31).** A typed id costs zero extra wire bytes. It also takes the same pack path as `int`. The generator ships only as `AutoPackerGenerator.dll`, so the evidence comes from the compiled IL and the package files:

1. The generator accepts an unmanaged user struct in a `[Packed]` field with no target type. Only managed fields fail (CSG002): `com.scaffold.autopacker/README.md:20,27`. A test packs and restores such a field (`Vector3 SpawnPoint`): `Tests/AutoPackerTests.cs:13,231-241`.
2. The `Packed` field has the same type as the source field. In the compiled `Framework.Wire.dll`, `BlockStartEvent::Id` and `BlockStartEvent/Packed::Id` both have the field signature `int`. So an `EntityId` field gives an `EntityId` field in `Packed` (4 bytes, alignment 4, the same layout).
3. Each field is copied through `IPackingHandler.Resolve<TSource,TTarget>`, also for `int`. `BlockStartEvent/Packed::.ctor` calls `Resolve<int,int>` once for each field. A typed id calls `Resolve<EntityId,EntityId>`.
4. `DefaultPackingHandler.Resolve` (in `AutoPackerContracts.dll`) does: return `default` on null, then `source is TTarget t ? t : Convert.ChangeType(...)`. When `TSource` is `TTarget`, the `is` branch runs. This is the same for `int` and for a typed id. `Convert.ChangeType` never runs.
5. `NetworkChannel` copies `sizeof(TPacked)` raw bytes: `Wire/Runtime/Network/NetworkChannel.cs:38,48,62`.

A side finding that is not caused by typed ids: `Network` is built with no handler (`Bootstrap/Runtime/EngineInstaller.cs:22`). Thus each generated `Pack` and `Unpack` creates a new `DefaultPackingHandler`. The `is` check on a generic value type can also box. This cost is the same for `int` today. If a benchmark shows it, pass one shared handler to `Network`. That fix is outside this spike.

**Why not one generic `Id<TSpace>`.** A phantom generic needs a marker type for each space (there is no `Block` class). It also puts a closed generic name into the AutoPacker `Packed` struct, which no current test covers. Four small named structs are clearer and carry no generator risk.

### 3.3 Allocator

```csharp
namespace Framework.Core
{
    public sealed class IdAllocator<TId> where TId : unmanaged, IEquatable<TId>
    {
        readonly Func<int, TId> _make;
        int _next;

        public IdAllocator(Func<int, TId> make, int first = 1)
        {
            _make = make;
            _next = first;
        }

        public int Watermark => _next;

        public TId Next() => _make(_next++);

        public void Restore(int watermark)
        {
            if (watermark < _next)
                throw new InvalidOperationException($"Watermark {watermark} is below {_next}; ids are never reused.");
            _next = watermark;
        }
    }
}
```

Registration, server only, in the installer that registers the owning service:

```csharp
if (_isServer)
{
    builder.Register(_ => EntityIds.NewAllocator(), Lifetime.Singleton);
    builder.Register<EntityService>(Lifetime.Singleton);
}
```

`EntityIds.NewAllocator()` lives in `Framework.Core` beside `EntityId`, so Entities never sees the constructor. The other spaces pass their own lambda, because the constructor is internal to the same asmdef: `new IdAllocator<RequestId>(v => new RequestId(v))`.

Each id space has its own closed type, so VContainer resolves each one without keys. The client scope has no allocator. A client that tries to allocate fails at resolve time, the same as a service today.

### 3.3a Rewind policy

```csharp
public enum IdPolicy { Forward, Rewindable }

public readonly struct IdMark { internal readonly int Watermark, Depth; ... }

// IdAllocator<TId>, added members
public IdAllocator(Func<int, TId> make, IdPolicy policy = IdPolicy.Forward, int first = 1);
public IdMark Mark();               // pushes a mark
public void   RewindTo(IdMark mark); // mark must be the top mark; policy must be Rewindable; sets _next back, pops
public void   Keep(IdMark mark);     // mark must be the top mark; pops, keeps the ids
```

| Undo shape | Ids above the mark | Allocator |
|---|---|---|
| Undo from the top (server lookahead, cancelled sequence) | all removed | `RewindTo(mark)` |
| Remove a record in the middle | later records still use ids | no change; the id is a gap |
| Client view scrub, replay | — | no allocator on the client |

Contract on the caller (rollback): call `RewindTo` only after all records above the mark are undone, and only in a scope that sent none of those ids on the wire. The allocator cannot see the stores. A debug check can ask each store for its highest id in use. `Restore` (load) stays forward only.

Why no id pool: an `int` holds about 2 billion ids, and a match uses thousands. Reuse breaks logs, replays, saves and old `CauseId` links. If ids were reused, the id would need a generation (`{ Index, Generation }`); a typed id could hold it, a bare `int` could not.

### 3.4 Determinism

- **Seed.** No random seed. Every allocator starts at 1 in each new match scope. Two runs that allocate in the same order get the same ids.
- **Order.** The pump runs one message at a time, so allocation order is the pump order. An `await` on a request must resume on the pump. Then ids allocated after the response follow the arrival order of the response.
- **Replay.** Replay runs events through apply handlers. Apply handlers never allocate (ARCH-06 forbids service calls). The ids come from the events.
- **Two-sided tests.** `MatchPair.Start` builds two scopes over `LoopbackTransport.Pair()`. Only the server scope has allocators. Client ids are copies of server ids by construction.
- **Save and load.** A save stores each allocator `Watermark`. Load builds fresh stores from the event stream, then calls `Restore`. `Restore` moves forward only, so a bad save cannot reissue a live id.
- **Rollback.** A client view scrub or replay does not touch allocators, because the client has none. A server undo that runs from the top of the stack can rewind a `Rewindable` allocator (§3.3a). Ids that the wire already sent are never reused.

### 3.5 Record and store read contract

A record is a type whose identity is one typed id. The contract is a convention, not an interface:

- The record exposes its own id as `Id` of its typed id. A wire record (`Request`) uses a `[Packed]` field. A store record (`Entity`) uses a get-only property.
- A field that points to another record is named `<Space>Id` and has that typed id (`EntityCreatedEvent.EntityId`, `Response.RequestId`).
- `None` (value 0) means "no record". Do not use `-1` or nullable ids.

The store read pair, over the typed id:

```csharp
public interface IReadOnlyEntityStore
{
    Entity ById(EntityId id);
    bool TryGetById(EntityId id, out Entity entity);
}
```

**No shared `IRecord<TId>` or `IReadOnlyStore<TId,T>`.** `request-service.md` L3 rejected interfaces on wire records, because `[Packed]` needs fields and an interface can only declare properties. No current consumer is polymorphic over stores. The read pair is two lines. So the "no `Store<T>` base" decision from `docs/lookup-patterns.md` still fits: conform by convention and enforce with a rule (§6). `RequestStore.Get<T>` stays as a different contract (typed read, `null` on miss), as `lookup-patterns.md` already says.

## 4. Call-site migration

| Current code | New code | Change needed |
|---|---|---|
| `int _nextId = 1;` + `var id = _nextId++;` in `EntityService` | inject `IdAllocator<EntityId>`; `var id = _ids.Next();` | ctor parameter; `EntityInstaller` registers allocator (server) |
| `request.Id = _nextId++;` in `RequestService` | `request.Id = _ids.Next();` | `Request.Id : RequestId`; `RequestInstaller` registers allocator |
| `Dictionary<int, IPendingRequest> _pending` in `RequestService` | `Dictionary<RequestId, IPendingRequest>` | `Open`, `Complete` (key from `Response.RequestId`) |
| `var id = _nextId++;` in `Wire` `BlockScope` | `var id = _ids.Next();` in `Framework.Blocks` `BlockService`, returns `BlockId` | `BlockService` replaces `BlockScope`; the Blocks installer registers the allocator (server) |
| `[Packed] public int Id; ParentId; SourceId;` on `BlockStartEvent`, `int Id` on `BlockEndEvent` | `[Packed] public BlockId Id;`, `[Packed] public BlockId CauseId;` (no `ParentId`) | events move to `Framework.Blocks`; tests that read `Id` or `ParentId` |
| `[Packed] public int EntityId;` on Entity and Tag events | `[Packed] public EntityId EntityId;` | event fields; apply handlers |
| `Entity.Id : int` | `Entity.Id : EntityId` | `EntityStore` `Dictionary<EntityId, Entity>` |
| `ById(int)` / `TryGetById(int, out …)` | `ById(EntityId)` / `TryGetById(EntityId, out …)` | `EntityStore`, `IReadOnlyEntityStore`, `EntityService` |
| `TagService.Set<T>(int entityId, …)`, `TagStore.Get<T>(int, int)` | `EntityId entityId` | `TagStore` maps become `Map<EntityId,int,T>` |
| `RequestStore.Get<T>(int id)`, `Dictionary<int, Request>` | `Get<T>(RequestId id)`, `Dictionary<RequestId, Request>` | `IReadOnlyRequestStore.Open` key type |
| `Response.RequestId : int` | `RequestId` | `RequestService.Complete` |
| `Catalog<TDef>`, `Network`, `PeerRoster` | no change | see §5 |

## 5. What stays specialized, and why

- **Kind slots.** A slot is an id for a type, not for an instance. See `infra-kind-ids.md`.
- **Definition ids (`Catalog<TDef>`).** The id comes from data (`def.Id`). `Catalog.Add` is strict. A typed `DefinitionId` is a possible later step, and it is not in this spike.
- **Channel ids (`Network`).** A channel id is a Router key, not a record id. It is a `byte`, it has a capacity limit, and both sides must compute the same id from the sorted registration. `WireRegistrar` replacement is its own roadmap item.
- **Seats (`PeerRoster`).** Seat 0 is the host (`Peer.IsHost`), and `Session` is a coordinator, not a Store (`lookup-patterns.md`). Seats live for the lobby, not the match. The seat → player-entity map is phase 3 work. Seat can use `IdAllocator<T>` later, but it gets no benefit now.
- **VerticalSlice.** No migration. Phase 8 deletes it. Its `TagRemovedEvent.Prev` and `MirrorWorld` are the reference for appendix A.1.
- **`RequestStore.Get<T>`.** A typed read with `null` on miss stays a different contract.

## 6. Rulebook rules to propose

Proposals only. `rulebook.json` does not change in this spike.

| Id | Rule | Why | Check |
|---|---|---|---|
| ID-01 | A Store never allocates ids. No `*Store` type calls `IdAllocator<T>.Next`. | The client has stores and no allocators. A store that allocates gives different ids on each side. | `il.forbiddenCalls`, callers `type: *Store`, callee `IdAllocator`1.Next`. Confirm `callers` accepts a type glob (ARCH-06 uses `subclassOf`). |
| ID-02 | Each id space has one allocating caller. `IdAllocator<T>.Next` is called only from the owning service listed for `T`. | Two callers on one space make ids collide or depend on call order. | `il.callersOnly`, callee `IdAllocator`1.Next`, allowed callers `EntityService`, `RequestService`, `BlockService`, the pipeline `Enqueue`. |
| ID-03 | Allocators are server-only. The client scope cannot resolve any `IdAllocator<T>`. | Clients take ids from events. A client allocator is a divergence bug. | `attached`: a test in `BootstrapWiringIntegrationTests`. |
| ID-04 | Ids on the wire are typed. No `WireEvent` subtype in `Framework.*` has a public `int` field whose name ends in `Id`. (`infra-kind-ids.md` KIND-03 adds `Slot` to the same test.) | A bare `int` id lets an entity id go into a request slot without a compile error. | `attached`: a reflection test over `WireEvent` subtypes. Or a new kind `type.memberTypes` with `baseType`. |
| ID-05 | `RewindTo` runs only in a scope with no wire. A `Rewindable` allocator is not registered in a scope that has `Network`. | A rewind after the wire sent ids gives the client two records with one id. | `attached`: a test in `BootstrapWiringIntegrationTests`. Status `planned` until rollback is scheduled. |
| EV-01 (handed off) | An event can be undone from its own fields. Each state-changing event is a Create, Set, Change(`Prev`,`Next`), Remove(`Prev`) or cell Replace(`Prev`,`Next`). | Rollback and `Unapply` must not read state that the event already changed. | `attached`: one round-trip test for each event: snapshot, `Apply`, `Unapply`, compare. Status `planned` until rollback is scheduled. |
| MSG-01 (handed off) | Queued messages hold ids, not live refs. No `Command` subtype has a field or property of type `Entity` or `Request`. | A queued ref can describe state that changed before the handler runs. Client intents cannot carry refs. | `attached`: a reflection test over `Command` subtypes. Or the `type.memberTypes` + `baseType` kind. |

## 7. Migration steps

Each step is one PR. Each step keeps `powershell -NoProfile -File Tools/Rulebook/gate.ps1` green. Steps 2–5 touch wire events, so they also run `-With playmode`.

1. **Add `IdAllocator<TId>`** in `Core/Runtime/Ids/` with unit tests in `Framework.Core.Tests` (starts at 1, `Next` is monotonic, `Restore` forward only). No callers. No new asmdef reference yet.
2. **Add `BlockId`** in `Framework.Blocks`. This step comes after the block piece creates `Framework.Blocks` and moves the `Block*` types out of Wire. That PR also makes the ARCH-02 and ARCH-09 edits with user approval. Add a Blocks test that packs and unpacks a block event with a `BlockId` field. This proves AutoPacker for typed ids before a wide change. Change `BlockStartEvent` (`Id`, `CauseId`) and `BlockEndEvent` (`Id`), inject `IdAllocator<BlockId>` into `BlockService`, and update the block tests.
3. **Add `RequestId`** in `Requests/Runtime/`. Change `Request.Id`, `Response.RequestId`, `RequestStore`, `IReadOnlyRequestStore`, `RequestService`, `RequestInstaller` and the request tests. `Request.ParentId`, `SourceId` and `ResponderId` stay `int` until §9 Q2 is answered.
4. **Add `EntityId`** in `Core/Runtime/`. Change `Entity`, `EntityStore`, `IReadOnlyEntityStore`, `EntityService`, `EntityCreatedEvent`, `EntityInstaller`. Tags still takes `int` in this step. Thus Core gives `Framework.Entities` a temporary `InternalsVisibleTo` grant, so Entities can pass `id.Value` to Tags.
5. **Move Tags to `EntityId`.** Change the Tag events, `TagStore` maps, `TagService`, `IReadOnlyTagStore` and the apply handlers. Remove the temporary grant from step 4.
6. **(Handed off to the pipeline.) Move sample commands to typed ids.** Change `Framework/Testing/Support/SampleCommands.cs`, `Commands/Tests/SampleCommands.cs`, and the acceptance bindings. The pictures do not change, because they name `#1`, not types.
7. **(Handed off to rollback.) Add the inverse fields.** `StatePushEvent.PrevSlot`; `TagRemovedEvent<T>` with `Prev`; the per-`T` handler and channel in `TagInstaller`; and tests that the fields hold the earlier value. `Unapply` stays unimplemented.
8. **Propose the rules.** Write §6 into `rulebook.json` with their linked tests, in a separate PR for user review. ID-03, ID-04 and MSG-01 can be `enforced` at once. EV-01 stays `planned`.
9. **Update docs.** Update `docs/architecture.md` (id spaces), `docs/lookup-patterns.md` (typed read pair), and the live-ref text in `command-service.md`.

## 8. Interfaces for the other three pieces

**Block and wire system.**

- `BlockId` in `Framework.Blocks`, with `BlockId.None` for "no cause". Use it for `BlockStartEvent.Id`, `BlockEndEvent.Id` and `BlockStartEvent.CauseId`. There is no `ParentId` field.
- `IdAllocator<BlockId>` from `Framework.Core`, a server singleton. Only `BlockService` (today `BlockScope`) calls `Next`. Effects and triggers open blocks through `BlockService.Run`. They never inject the allocator (ID-02).
- The nesting parent comes from the order of the brackets, not from an id. The block piece owns nesting. This piece owns only the id.
- EV-01: block events create and close a `BlockRecord` in `BlockStore`. `Unapply` of start calls `Remove`. `Unapply` of end calls `Reopen`. The block piece owns these handlers.

**Command pipeline.**

- `MessageId` (pipeline asmdef) and `IdAllocator<MessageId>` from `Framework.Core`. `Enqueue` allocates one id for each queued intent and each pushed `StackFrame`.
- `MessageId` is local to the server. An intent opens no block (decision 6), so an intent id never appears in a block event. It goes on the wire only if the deferred "block payload + source capture" item adds it to `BlockStartEvent`.
- Messages hold typed ids (MSG-01). The handler resolves records through read views (`IReadOnlyEntityStore.ById`).
- Client submit (phase 2) needs a correlation id that the client makes. That is a separate per-client space. See §9 Q4.
- The async core uses `System.Threading.Tasks.Task` (decision 4). The pump must resume `await` continuations on the pump, for example with `TaskCompletionSource` made without `RunContinuationsAsynchronously`, or with a pump `SynchronizationContext`. Then allocation order is deterministic (§3.4).

**Type-keyed router.**

- No new interface. Router keys are `Type` or `byte`, not record ids. Typed ids give the router nothing to dispatch on, and the router does not see ids.
- The router and `IdAllocator<TId>` both live in `Framework.Core` (decision 2). They have no dependency on each other.

**All pieces.**

- An event that changes a store follows the appendix A.1 table (owner: rollback).
- A store has `ById(TId)` and `TryGetById(TId, out T)`, and no allocator (ID-01).

## 9. Open questions

1. ~~**Allocator home.**~~ **Closed 2026-10-31:** `Framework.Core` (decision 2). `Framework.Wire` references `Framework.Core`, and ARCH-02 changes to allow it.
2. **Typed cross-space fields.** Which space do `Request.ParentId`, `Request.SourceId` and `Request.ResponderId` use? `ResponderId` is a seat or a player entity, and that depends on phase 3. Is `BlockStartEvent.CauseId` always a `BlockId`, or can a cause also be a request? The block piece decides.
3. **Closed requests.** Keep a `Closed` set in `RequestStore` so a Response can be undone (appendix A.1)? Or put the full request data in the Response? This affects `request-service.md` L5 ("no AnswersStore").
4. **Client correlation id.** Does a client intent carry a client-made `SubmitId` for accept and reject replies? Who owns it: one allocator per client scope?
5. **Save watermarks.** Store the watermarks in the save, or compute them on load from the highest id in the stream?
6. **Id scope across matches.** Are ids unique only in a match (`entity-identity-model.md` Q3)? A replay archive would then need a match id.
7. **`DefinitionId`.** Make definition ids typed in this effort, or wait for phase 6?
8. **Seat migration.** Move `PeerRoster` to `IdAllocator<int>` now for one pattern, or leave it until phase 3 links seats to player entities?

## Appendix: handed off

These concerns are out of scope (§2a). The text stays here for the owners until they take it.

### A.1 Events carry their inverse

Every event that changes a store holds enough data for `Unapply` to restore the earlier state. Nothing outside the event is read.

| Event kind | Fields | `Unapply` |
|---|---|---|
| Set (first write) | key, `Value` | remove key (a Set implies "absent before") |
| Change | key, `Prev`, `Next` | write `Prev` |
| Remove | key, `Prev` | write `Prev` |
| Create record | id, all record fields | delete record |
| Replace a cell | `Prev`, `Next` | write `Prev` |
| Block bracket (`BlockStart`, `BlockEnd`) | `Id`, header | `BlockStore.Remove` / `Reopen` (owned by the block piece) |

Changes this needs:

- `TagRemovedEvent` becomes `TagRemovedEvent<T> where T : unmanaged` with `[Packed] T Prev`. It gets one channel for each value type, like `TagSetEvent<T>` and `TagChangeEvent<T>`.
- `StatePushEvent` gets `[Packed] StateSlot PrevSlot`. `StateSlot` comes from `infra-kind-ids.md`. Until that step lands, the field is `int`.
- A Response closes a request, and the request is not in the Response. Recommended: `RequestStore` keeps closed requests in a `Closed` set, so `Unapply` moves the request back to `Open`. This matches "Answer A" in `entity-identity-model.md` (records are not destroyed in a match).

The wire cost is `sizeof(T)` on tag remove and 4 bytes on state push. `WireEventHandler<T>.Unapply` can stay unimplemented. This spike only makes it implementable.

### A.2 Messages hold ids, not refs

```csharp
public sealed class DealDamage : Command
{
    public EntityId Target;
    public int      Amount;
}

internal sealed class DealDamageHandler : CommandHandler<DealDamage>
{
    public override Task Handle(DealDamage cmd, ICommandScope scope)
    {
        _tags.Set(cmd.Target, Health, _tags.Get(cmd.Target, Health) - cmd.Amount);
        return Task.CompletedTask;
    }
}
```

Reasons:

1. A queue puts a gap between "make the message" and "handle the message". A live ref can describe a state that changed in that gap.
2. Phase 2 client intents come over the wire. A client has no server objects, so an intent can only carry ids. One shape for server and client messages means one validator path.
3. Replay and logs need a message that can be written down.
4. Framework sample commands already hold `int EntityId`. Only the VerticalSlice spike uses `IEntity` refs.

The handler signature is the pipeline one (`infra-command-pipeline.md`). The cost is one store lookup in the handler. `command-service.md` says "commands carry references freely". That text depends on inline `Execute` (L3) and expires when the queue lands.

### A.3 Migration rows

| Current code | New code | Change needed |
|---|---|---|
| `TagRemovedEvent { EntityId, Slot }` | `TagRemovedEvent<T> { EntityId, Slot, Prev }` | `TagService.Remove` reads prev; per-`T` handler and channel in `TagInstaller` |
| `StatePushEvent { Slot }` | `StatePushEvent { Slot, PrevSlot }` | `StateService.Push` reads `StateStore.Current.Slot` |
| `SetHealth { int EntityId; }` (samples) | `SetHealth { EntityId Target; }` | Testing Support, Commands tests, acceptance bindings |

### A.4 Object pooling (v2)

Idea from the design talk: pool record objects, and give them a base class nested in the allocator, so the allocator can dispose an id even when code still holds a reference. Problems found:

1. After reuse, the "disposed" flag is cleared. An old holder sees the new record, unless it also holds a generation.
2. The client has records but no allocator (ID-03). Client records come from apply handlers.
3. Wire records (`Request`) already derive from `WireEvent`, and C# allows one base class.

The gain is small, because a match has hundreds of records. Owner: the feature stores, in v2.
