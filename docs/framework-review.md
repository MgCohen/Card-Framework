# Framework review — findings & backlog

Quality sweep of the runtime under `Assets/Framework/` (85 non-test files), 2026-05-31.
Numbered for reference. Severity: 🔴 worth fixing · 🟡 minor/judgment · ⚪ note/decision.

> **How to use this:** the findings below are the original sweep, kept verbatim. The
> **[Disposition](#disposition)** section at the bottom tracks what's been decided vs still
> open. Many items were resolved in the lookup-patterns design pass — see
> [lookup-patterns.md](lookup-patterns.md) (standard) and its gaps table (backlog).

---

## A. Comments

- **A1** ⚪ Nearly every runtime file carries a 3–8 line rationale header; the dense ones (`NetworkChannel`, `EntityStore`, `TagService`, `GameEngine`, `EngineLayer`, `RequestService`) have more comment than code. Three kinds mixed together: load-bearing invariants (no-double-write, the `NetworkChannel` shape, emit-read-back), pure restatements (`// Data only`, `// Command surface over X`), and TODOs-as-comments (`"Rollback lights up later"` [WireEventHandler.cs:13](Assets/Framework/Wire/Runtime/Stream/WireEventHandler.cs), `"lands in step 5c"` [GameState.cs](Assets/Framework/States/Runtime/GameState.cs)). Confirmed approach: strip headers, **leave one-line invariant warnings inline**.

## B. Boxing / reflection / hot path

- **B1** 🔴 **`TagStore` boxes every value.** [TagStore.cs:9](Assets/Framework/Tags/Runtime/TagStore.cs) is `Map<int,int,object>`; `Set<T>` boxes, `Get<T>` unboxes. The `where T:unmanaged` constraint everywhere upstream exists to avoid exactly this. Tags are the highest-frequency mutation → the one real allocation hot-path.
- **B2** 🟡 Every wire event is a `class`, so each `Emit` allocates a heap object (independent of the `Packed` struct used on the network). Fine now; matters if event rates climb.
- **B3** ⚪ No actual reflection on hot paths — dispatch is `Dictionary<Type,...>` keyed by `GetType()` ([HandlerRegistry.cs:30](Assets/Framework/Wire/Runtime/Stream/HandlerRegistry.cs), [Network.cs:42](Assets/Framework/Wire/Runtime/Network/Network.cs)). Stated as a positive, not a problem.

## C. Repeated code

- **C1** 🔴 **Dual `Emit` with identical bodies.** [IEventEmitter.cs](Assets/Framework/Wire/Runtime/Stream/IEventEmitter.cs) declares `Emit<TEvent>` + `Emit(WireEvent)`; [EventStream.cs:18-28](Assets/Framework/Wire/Runtime/Stream/EventStream.cs) implements both byte-identically and the generic never uses `TEvent`.
- **C2** 🔴 **Two wiring paths for one subscription.** [Wire.cs:13-16](Assets/Framework/Wire/Runtime/Wire.cs) hand-writes the same `OnAppended += Send` / `OnReceived += Emit` that [WireForwarder.cs](Assets/Framework/Wire/Runtime/WireForwarder.cs) and [NetworkInboundBridge.cs](Assets/Framework/Wire/Runtime/NetworkInboundBridge.cs) encapsulate.
- **C3** 🟡 **Two near-identical static registries** — [EntityTypeRegistry.cs](Assets/Framework/Entities/Runtime/Registry/EntityTypeRegistry.cs) and [TagRegistry.cs](Assets/Framework/Tags/Runtime/Registry/TagRegistry.cs): same `Dictionary<int,T>` + Register / BySlot / TryGetBySlot.
- **C4** 🟡 **`Session` peer-removal duplicated** — `HostOnClientDisconnected` ([Session.cs:227](Assets/Framework/Multiplayer/Runtime/Session.cs)) and `OnPeerLeftReceived` ([Session.cs:289](Assets/Framework/Multiplayer/Runtime/Session.cs)) repeat find / RemoveAt / `_players.Remove` / `_ready.Remove` / invoke. `IndexBySeat` / `IndexByNetworkId` are the same loop, different predicate.
- **C5** 🟡 **`Session` static factories repeat** the new-up / set-fields / `RegisterChannels` / `SubscribeChannels` sequence across `StartSolo` / `StartHost` / `Join` ([Session.cs:69-200](Assets/Framework/Multiplayer/Runtime/Session.cs)).
- **C6** 🟡 **Bootstrap `_isServer` boilerplate** — the `readonly bool _isServer` field + ctor is copy-pasted across all five layers (Engine/Entity/Tag/State/Request).
- **C7** 🟡 **`Session` static facade** re-declares six method signatures that just forward to `Session<PlayerData,PlayerContext>` ([Session.cs:430-461](Assets/Framework/Multiplayer/Runtime/Session.cs)).
- **C8** ⚪ Thin pass-throughs: `EntityService.ById/TryGetById` just forward to `EntityStore` ([EntityService.cs:32-33](Assets/Framework/Entities/Runtime/EntityService.cs)). Mild, probably intentional API surface.

## D. Diverging patterns across classes

- **D1** 🔴 **Domain events live in two homes.** `TagSetEvent<T>`, `TagChangeEvent<T>`, `StatePushEvent<T>` sit in their feature assemblies, but `EntityCreatedEvent`, `EntityDestroyedEvent`, `Request*Event`, `ResponseEvent`, `Block*Event` all live in **`Framework.Wire`**. So `EntityService` emits a `Framework.Wire` type while `TagService` emits a `Framework.Tags` type — same concept, inconsistent placement.
- **D2** 🟡 **Two registration styles.** `EntityType` registers explicitly via a static call from static fields ([EntityTypes.cs](Assets/Framework/Entities/Runtime/EntityTypes.cs)); `TagKey` registers as a constructor side-effect ([TagKey.cs:13](Assets/Framework/Tags/Runtime/TagKey.cs)).
- **D3** 🟡 **Emit-then-read-back only in Entities.** `EntityService.Create` reads `_store.ById(id)` back after emitting ([EntityService.cs:31](Assets/Framework/Entities/Runtime/EntityService.cs)); Tag/State services don't read back.
- **D4** 🟡 **Public mutable fields vs properties.** `PlayerData` / `PlayerContext` expose bare public fields ([PlayerData.cs](Assets/Framework/Multiplayer/Runtime/PlayerData.cs), [PlayerContext.cs](Assets/Framework/Multiplayer/Runtime/PlayerContext.cs)) while the rest of the codebase uses `{ get; }` properties.
- **D5** 🟡 **`sealed` everywhere except `Session`** ([Session.cs:11](Assets/Framework/Multiplayer/Runtime/Session.cs) is `public class`, not sealed). Intentional?
- **D6** ⚪ Handler asymmetry: `RegisterTagValueType<T>` wires Set + Change, but Removed is registered separately in `TagLayer` ([HandlerRegistrationExtensions.cs](Assets/Framework/Bootstrap/Runtime/HandlerRegistrationExtensions.cs), [TagLayer.cs](Assets/Framework/Bootstrap/Runtime/TagLayer.cs)).

## E. Over-engineering / over-abstraction / dead code (grep-confirmed)

- **E1** 🔴 **`Request<TResponse>.ResponseType`** ([Request.Typed.cs:10](Assets/Framework/Requests/Runtime/Request.Typed.cs)) — defined, never read. Dead.
- **E2** 🔴 **`Network.GetChannel<T>()`** ([Network.cs:38](Assets/Framework/Wire/Runtime/Network/Network.cs)) — public, zero callers. Dead.
- **E3** ⚪ **Wire event placeholders** — `BlockStart/End/Kind/Outcome`, `EntityDestroyedEvent`, `RequestOpened/Closed/ResponseEvent` are emitted/handled **only by Wire's own tests and the ECS spike**; no `Framework` runtime uses them. Staged vocab for the command pipeline (step 5c), but today they read as live protocol.
- **E4** 🟡 **`Vec3`** ([Vec3.cs](Assets/Framework/Wire/Runtime/Primitives/Vec3.cs)) — runtime-unused; only a test fixture references it.
- **E5** 🟡 **`TagRegistry.TryGetBySlot`** — unused.
- **E6** ⚪ **`WireEventHandler.Unapply`** throws `NotImplementedException` and is never invoked in runtime — rollback placeholder.
- **E7** ⚪ **`NetworkChannel` 3-layer generic hierarchy** ([NetworkChannel.cs](Assets/Framework/Wire/Runtime/Network/NetworkChannel.cs)) — looks over-engineered but the justification (compile-time `sizeof(TPacked)` while keeping callsites single-generic) holds. Flagged only so you can re-confirm you still want it.
- **E8** 🟡 **`Catalog` double dictionary** ([Catalog.cs:8-9](Assets/Framework/Entities/Runtime/Catalog.cs)) — `_byDef` uses a class as a dictionary key (reference equality). I suspected YAGNI; grep shows `GetId` *is* used (test packing handler), so it stays — but the reference-equality keying is fragile.

## F. Multiple top-level types per file

- **F1** 🟡 **[GameState.cs](Assets/Framework/States/Runtime/GameState.cs)** — four top-level records (`GameState` + 3 baselines). Clearest violation; related but not nested/generic.
- **F2** 🟡 **[PendingRequest.cs](Assets/Framework/Requests/Runtime/PendingRequest.cs)** — `IPendingRequest` + `PendingRequest<TResponse>`. Minor (interface + impl).
- **F3** ⚪ `NetworkChannel.cs` (3 types) and `Session.cs` (`Session<,>` + static `Session`) are generic families / facade pairs — exempt under your own rule. Listed for completeness.

## G. Big classes / methods / doing too much

- **G1** 🔴 **`Session<TData,TContext>` — 461 lines, ≥3 responsibilities** ([Session.cs](Assets/Framework/Multiplayer/Runtime/Session.cs)): NetworkManager create/teardown · channel register+subscribe (7 each) · membership bookkeeping · lobby/ready/start protocol · session FSM. Strongest split candidate.
- **G2** 🔴 **`GameEngine` exposes raw stores publicly** ([GameEngine.cs:20-25](Assets/Framework/Engine/Runtime/GameEngine.cs)) — public `EntityStore`/`TagStore`/etc. alongside the services let callers bypass the "handler is sole writer" invariant the comments protect.

## H. Code judo / fragile

- **H1** 🟡 **Emit-then-read-back** ([EntityService.cs:31](Assets/Framework/Entities/Runtime/EntityService.cs)) relies on `Emit` synchronously running the handler that inserts; breaks silently (raw `KeyNotFoundException`) if dispatch ever goes async or the handler isn't registered.
- **H2** 🟡 **`NullTransport` `add { _ = value; }`** ([NullTransport.cs:8-12](Assets/Framework/Wire/Runtime/Network/NullTransport.cs)) — discard-to-satisfy-interface.
- **H3** 🟡 **`RequestStore.Get<T>` returns `req as T`** ([RequestStore.cs](Assets/Framework/Requests/Runtime/RequestStore.cs)) — silent `null` on type mismatch rather than a clear failure.
- **H4** ⚪ **Raw indexer throws** with no message: `EntityStore.ById`, `Catalog.GetById`, `TagStore.Get` all throw bare `KeyNotFoundException`/cast exceptions on miss.

## I. Robustness / state notes (not on the original checklist, but found)

- **I1** 🟡 **`EventStream` history is unbounded** — `List<WireEvent> _events` ([EventStream.cs:9](Assets/Framework/Wire/Runtime/Stream/EventStream.cs)) appends forever with no compaction; grows for the life of a match.
- **I2** 🟡 **Static mutable registries** (`EntityTypeRegistry`, `TagRegistry`) are process-global — test isolation hazard and shared mutable state across scopes (the `EngineReservedSlotsTests` existence hints this already bites).
- **I3** 🟡 **`Dispatcher` byte-id ceiling** — `_channels[256]` / `_nextId` is a `byte` ([Dispatcher.cs:7](Assets/Framework/Wire/Runtime/Network/Dispatcher.cs), [Network.cs:11](Assets/Framework/Wire/Runtime/Network/Network.cs)): silent 256-channel cap, and `_channels[id]` on an unregistered id is a null deref ([Dispatcher.cs:27](Assets/Framework/Wire/Runtime/Network/Dispatcher.cs)).
- **I4** 🟡 **`EntityStore` has two id-allocation paths** that both bump `_nextId` — `AllocateId()` and auto-id `Insert(type)` ([EntityStore.cs:17,21](Assets/Framework/Entities/Runtime/EntityStore.cs)); the auto-id one is test-convenience living on the production type.
- **I5** ⚪ **`BootstrapStatusUI`** ([BootstrapStatusUI.cs](Assets/Framework/Bootstrap/Runtime/BootstrapStatusUI.cs)) is an `OnGUI` debug overlay shipping in `Runtime/` rather than `Sample/` or `Editor/`.

## J. Where I looked and found nothing

- **J1** **Over-validation** — none. Validation is lean and appropriate (dup-slot rejection, `Catalog.Add` id-0/dupe guards, `Session` public-transition guards). No defensive-for-its-own-sake checks. Leave it.

---

## Disposition

Status as of 2026-05-31, after the lookup-patterns design pass.

### ✅ Decided — in the implementation backlog
Resolved by the lookup-patterns standard; tracked in [lookup-patterns.md](lookup-patterns.md) gaps + the staged PR plan.

- **B1** — TagStore boxing → per-value-type typed maps.
- **C3 / I2** — two static registries → one scoped `SlotRegistry<T>` instance.
- **D1** — event homes → domain events move to features; Wire = transport substrate.
- **D2** — registration styles → records over `TypeKey`, explicit registration, no ctor side-effects.
- **E1** — delete `ResponseType`.
- **E2** — delete `Network.GetChannel<T>`.
- **E3** — `Block*` stays (concept); delete legacy `RequestOpened/Closed/ResponseEvent`; delete `EntityDestroyedEvent` (rebuild with the destroy path).
- **E4** — delete `Vec3`.
- **G2** — GameEngine → services + read-only store views; store writes `internal`, reads `public`.
- **I4** — EntityStore allocation → service (`EntityService` owns `_nextId`).

### 👍 Decided — keep / no action
- **E5** — `TryBySlot` is *kept*: it's standard Registry surface, not dead.
- **B3, C8, E6, E7, F3, J1** — confirmed as notes; nothing to change.

### ✍️ Separate track — comment strip
- **A1** — strip headers, keep one-line invariant warnings inline. Its own pass once conformance lands.

### 📋 Open — non-lookup cleanups (not yet scheduled)
Real but orthogonal to the lookup decisions; own pass later.

- **Wire:** C1 (dual `Emit`), C2 (two wiring paths), B2 (event allocs), I1 (unbounded history), I3 (byte-id cap / null deref), H2 (`NullTransport` judo).
- **Session / Multiplayer:** G1 (461-line class), C4 (peer-removal dup), C5 (factory repetition), C7 (facade dup), D4 (public fields), D5 (not sealed).
- **Bootstrap:** C6 (`_isServer` boilerplate), D6 (handler-wiring asymmetry), I5 (`BootstrapStatusUI` in Runtime).
- **Entities / Requests:** E8 (Catalog reference-equality key), F2 (two types in `PendingRequest.cs`), H3 (`RequestStore.Get` silent null), H4 (raw throws on miss), D3/H1 (emit-read-back fragility).
- **States:** F1 (four records in `GameState.cs`) — naturally folds into the State slot-based PR.
