---
type: spike
status: accepted
tags: [#spike, #architecture, #event-sourcing, #services, #projections, #framework]
references:
  - Product/spikes/match-state-redesign.md
  - Product/spikes/entity-identity-model.md
  - Product/spikes/wire-typed-payloads.md
  - Product/spikes/vertical-slice-plan.md
  - Product/decisions/0009-engine-as-fsm.md
  - Product/decisions/0011-effect-orchestration-dispatch-requests.md
---

# Framework Architecture — service-first, event-sourced, projection-based

> **Status: accepted (decisions locked, patch cascade pending).** Multi-turn design walk that surfaced framework-foundational decisions while reviewing the state-machine spike. The state cursor question forced the larger question — *where does any simulation data live, and how do mutation, wire, and rollback relate?* — and the answers turned out to be framework-wide, not state-specific. This spike captures the locked architecture so future systems inherit it consistently. The walk ran 2026-05-18 on branch `claude/review-statemachine-spike-2dfIv`.

## Reading guide

If you only have 10 minutes, read **TL;DR** + **Core principles** + **§Architecture in detail**. That's the locked shape.

If you want to know *why* this is the shape, read **§Context** and **§Event-sourcing research**. The locked shape is anchored in mainstream ES patterns (Greg Young / Axon / Akka / Marten / EventStoreDB), not invented.

If you're picking up an implementation, read **§What this means for existing systems** + **§Patch cascade** + **§Open items**.

---

## TL;DR

**The framework runs as a constellation of services orchestrated by an engine.** Each service owns its own data; there is no centralized `World` data substrate. Mutations flow through one path per service, expressed as typed events on an append-only stream. The stream is the wire to the dumb client and the substrate for rollback / replay.

Five primitives compose the whole architecture:

```
   EVENT     immutable fact + inverse-info inline
   SERVICE   data owner + IProjection<TEvent>.Apply / Unapply + watch API
   HANDLER   pure emit (computes event, invokes service helper)
   STREAM    append-only log + projection registry (dispatch on Apply/Unapply)
   WATCH     per-key observable handles on services, disposable subs
```

Six locked decisions:

- **F1** No World data holder. Services own their data.
- **F2** Event sourcing as the wire / replay model. Single source of truth on the wire.
- **F3** `IProjection<TEvent>` paired interface — `Apply` and `Unapply`. Registered against the event-stream's projection registry (not a generic bus).
- **F4** `entity.Set(key, value)` decomposes internally into `store.Apply(ev)` + `stream.Append(ev)`. The same `Apply` is the only mutator.
- **F5** Server originates events via handlers; client consumes them via dispatch. Same `Apply` both sides.
- **F6** Presentation reactions are a *separate layer* — per-key watch API on services, disposable subscriptions. Not routed through the projection registry.

Inline `Apply` timing. Snapshots deferred until profiling forces it.

---

## Context

### Why this came up

The state-machine spike ([`match-state-redesign.md`](match-state-redesign.md)) settled on stateless `GameState` records owned by an `IStateService`. A review of that spike asked: *where does the cursor actually live?* The answer started as "on the service," considered "on the world as a typed singleton," and ricocheted into architectural questions that weren't state-specific:

- Should the framework even *have* a centralized `World` data holder?
- If services own their data, how do replay and rollback work?
- How does the dumb client mirror the simulation?
- Where do presentation reactions fit?
- How do designers author this through Visual Scripting, given there's no flat property-path namespace?

Walking those answers turned out to settle the foundation that every framework system inherits — entities, state, requests, match singleton, future systems. The state-machine work was the *trigger*; the answers are framework-wide.

### Project constraints (must respect)

1. **Framework, not just a game.** Extension is a primary goal. Adding a new system should be composition, not surgery on a central type.
2. **Visual Scripting authoring.** Designers compose nodes, not write flat `game.world.X.Y = z` paths. Authoring surface = typed nodes per service.
3. **Server-authoritative, dumb client.** Client cannot compute. Every observable change goes over the wire. Client renders from events.
4. **Replay / rollback first-class.** Wire events drive forward replay; the same events drive backward rollback.
5. **No Redux-style layering pain.** Prior implementation forced command → effect → command → mutator cascades. New design must keep mutation 1-hop where possible.

### Where the existing codebase sits

The vertical-slice rebuild already moved away from a god-object `World`. The current `World` (in `Assets/Spike/ECS/VerticalSlice/Engine/Pipeline/World.cs`) is intentionally a **causality coordinator** holding only the event log + block-id allocator — explicit code comment: *"World is intentionally a god-free zone."* Entities, effects, commands, and requests already live on per-role services (`EntityStore`, `EffectService`, `CommandService`, `RequestsService`).

The state-machine spike's `World` (under `Assets/Spike/ECS/StateMachine/Engine/World.cs`) held more — entities, Match singleton, log — for spike-isolation convenience. That shape was *not* what the framework was converging toward. This document makes the framework's actual direction explicit and locks it.

---

## Core principles

### F1 — Services own their data; no centralized World

The framework's data substrate is the *union* of services. Each service is responsible for its own data and exposes its own API.

```
                  ┌──────────────────────────────────┐
                  │            ENGINE                │
                  │   pump + service registry + DI   │
                  └────────────────┬─────────────────┘
                                   │ wires & owns
        ┌──────┬───────────────────┼───────────────┬──────────┐
        ▼      ▼                   ▼               ▼          ▼
   ┌────────┐ ┌──────────┐ ┌────────────┐ ┌────────────┐ ┌────────┐
   │Command │ │Effect    │ │Entity      │ │State       │ │Request │
   │Service │ │Service   │ │Store       │ │Service     │ │Service │
   ├────────┤ ├──────────┤ ├────────────┤ ├────────────┤ ├────────┤
   │ pure   │ │ pure     │ │ data:      │ │ data:      │ │ data:  │
   │ orches │ │ orches   │ │  entities  │ │  cursor    │ │ pending│
   │ trator │ │ trator   │ │  + tags    │ │            │ │        │
   └────────┘ └──────────┘ └────────────┘ └────────────┘ └────────┘
```

There is no `world.entities`, `world.state`, `world.match`. Adding a new system = adding a new service. No central type accumulates.

A thin causality coordinator may exist (event log + block-id allocator), but it holds no domain data. It can keep the name `World` for continuity with the existing slice, or be renamed to something more accurate (`Pipeline`, `Stream`, etc.). Either way: not a data substrate.

### F2 — Event sourcing as the wire / replay model

Every observable mutation goes onto an append-only event stream. The stream is:

- The wire format sent to the dumb client.
- The history that replay and rollback walk.
- The audit trail for debugging and tooling.

Events are *facts* — immutable, past-tense, typed records. They carry enough information to be applied forward (`Apply`) and unapplied backward (`Unapply`).

```
   forward (live)    ─────►   handler ─► event ─► stream
   forward (replay)  ─────►   stream  ─► event ─► service.Apply
   backward          ─────►   stream  ─► event ─► service.Unapply
```

### F3 — `IProjection<TEvent>` paired interface

The projection contract:

```csharp
public interface IProjection<TEvent> where TEvent : WireEvent {
    void Apply  (TEvent ev);
    void Unapply(TEvent ev);
}
```

Paired so rollback symmetry is enforced at compile time. A service that consumes `StatePushEvent` must implement both directions; the compiler will not let it forget the reverse.

Services declare what they project from at the type level:

```csharp
public sealed class StateService : IProjection<StatePushEvent> {
    public GameState Current { get; private set; }

    public void Apply  (StatePushEvent ev) => Current = registry.Resolve(ev.NewKind);
    public void Unapply(StatePushEvent ev) => Current = registry.Resolve(ev.PrevKind);
}
```

Registration is **explicit**, on the event-stream's projection registry — not on a generic message bus. The stream owns its projection map.

```csharp
public sealed class EventStream {
    public void Register<T>(IProjection<T> p) where T : WireEvent { ... }
    public void Append (WireEvent ev) { ... }     // appends + dispatches Apply
    public void PopLast()             { ... }     // pops + dispatches Unapply
}

// bootstrap — one block, manifest-style
stream.Register<StatePushEvent>     (stateService);
stream.Register<TagChangeEvent>     (entityStore);
stream.Register<EntitySpawnEvent>   (entityStore);
stream.Register<TurnIncrementEvent> (matchService);
```

Manual today; reflection or source-generator scanning is a later ergonomics improvement.

### F4 — Single mutator: entity.Set decomposes into Apply + Emit

The user-facing surface is `entity.Set(key, value)` (already in the codebase). Internally:

```
   entity.Set(key, value)
        │
        ├─► store.Apply(TagChangeEvent)   ◄── single mutator
        │      ├─ writes the slot
        │      └─ fires watchers
        │
        └─► stream.Append(TagChangeEvent) ◄── wire
```

`Apply` is the only path that mutates the slot bag. It runs:

- On the **server** when a handler invokes `entity.Set` (which calls Apply + Emit).
- On the **client** when the stream dispatches the event to the projection.
- On **either side** during replay-from-savepoint.

Rollback uses the same projection's `Unapply`. There is exactly one place to look when asking *"what does this event do?"*

### F5 — Server / client asymmetry — same Apply, different trigger

```
   ─── SERVER ───────────────────────────────────────────────
   handler runs
        │
        ├─► entity.Set(key, value)
        │      ├─ store.Apply(TagChangeEvent)   ◄── mutate
        │      └─ stream.Append(TagChangeEvent) ◄── wire emit
        │
   simulation continues — does not read its own log.

   ─── WIRE ─────────────────────────────────────────────────
   stream → network → client

   ─── CLIENT ───────────────────────────────────────────────
   wire event arrives
        │
        ▼
   stream dispatches to registered projection
        │
        ▼
   store.Apply(TagChangeEvent)
        └─ writes the slot, fires watchers
```

The server **originates** events; the client **consumes** them. Both call the same `store.Apply`. The asymmetry is only in who triggers it.

The server does not re-feed its own log through Apply at runtime — it would be redundant. But the same Apply is available for server-side rollback, replay-from-savepoint, or future scenarios where the server needs to reconstruct state from the log.

### F6 — Presentation reactions = watch API on services

UI bindings (*"entity HP changed → repaint the health bar"*) are a separate layer on top of services. They do not subscribe to wire events directly.

```
   wire ─► projection.Apply ─► service mutates ─► fires watchers ─► view repaints
```

The watch API lives on the **service**, not on the entity record. Per-key granularity. Subscriptions are disposable; the service manages lifecycle (entity despawn = drop subscriptions).

```csharp
var sub = entityStore.Watch(cardId, MtgTags.HP,
                            (prev, next) => healthBar.Repaint(next));

sub.Dispose();   // or auto-disposed by view lifecycle
```

Crucially: **presentation reactions are NOT routed through the projection registry.** They are not replayed on rollback. They fire on data change, which gives the same effect without coupling to the wire format.

Two kinds of reaction exist in the framework:

- **System reactions** (*"deathrattle fires when HP reaches 0"*) — live at the *effect* layer. Already designed and implemented. Out of scope here.
- **Presentation reactions** (*"health bar follows HP value"*) — the watch-API layer described above.

---

## Architecture in detail

### Event design — the mirror rule

```
   every event must carry enough data for Apply AND Unapply
   to work using ONLY information inside the event.

   ─────────────────────────────────────────────────────────
   changes   →  { entity, key, prev, next }
   pushes    →  { newKind, prevKind }
   creations →  { entity, type, initial-tag-bag }
   removes   →  { entity-snapshot }    ◄── full bag, for restore
```

This is the contract that makes rollback work. An event that lacks its inverse info is a one-way trap. The entity-identity-model spike flagged this hazard in the TypedPayloads sandbox (`MirrorWorld.Unapply(EntityDestroyedEvent)` re-creates an empty entity because the prior tag bag isn't on the wire). The mirror rule is the design discipline that prevents that class of bug.

### The five primitives, expanded

```
   ┌─ EVENT ──────────────────────────────────────────────────┐
   │  immutable record. carries new-state AND prev-state      │
   │  needed for inverse. typed; one class per event kind.    │
   │                                                          │
   │  examples:                                               │
   │    StatePushEvent    { NewKind, PrevKind }               │
   │    TagChangeEvent    { EntityId, Key, Prev, Next }       │
   │    EntitySpawnEvent  { EntityId, Type, InitialTags }     │
   │    TurnIncrementEvent { PrevTurn, NextTurn }             │
   └──────────────────────────────────────────────────────────┘

   ┌─ SERVICE ────────────────────────────────────────────────┐
   │  owns a slice of game state. exposes:                    │
   │    - read API (typed accessors)                          │
   │    - write API (entity.Set, service.Push, etc. —         │
   │      sugar over Apply + Emit)                            │
   │    - watch API (per-key observation, disposable)         │
   │    - IProjection<TEvent> implementations                 │
   └──────────────────────────────────────────────────────────┘

   ┌─ HANDLER ────────────────────────────────────────────────┐
   │  consumes a command. produces zero or more events.       │
   │  does NOT mutate state directly outside service methods. │
   │  shape:                                                  │
   │     scope.Resolve(target).Set(key, newValue);            │
   │  one hop. no nested commands. no Redux-style cascade.    │
   └──────────────────────────────────────────────────────────┘

   ┌─ STREAM ─────────────────────────────────────────────────┐
   │  append-only event log + projection registry.            │
   │  Append(ev) appends to log AND dispatches Apply inline.  │
   │  PopLast() pops AND dispatches Unapply.                  │
   │  also owns block-id allocation for causality nesting.    │
   └──────────────────────────────────────────────────────────┘

   ┌─ WATCH ──────────────────────────────────────────────────┐
   │  observable handles on services. per-key granularity.    │
   │  subscriptions are disposable; lifetime = subscriber's.  │
   │  fires synchronously when data changes via Apply.        │
   │  presentation batches per-frame on its own surface.      │
   └──────────────────────────────────────────────────────────┘
```

### Apply timing — inline

When a service's mutator (e.g., `entity.Set`) is called on the server, it runs `store.Apply` immediately, before returning. The handler that called it sees the updated state on the next line. Multi-event handlers (e.g., `PushStateHandler` emits a `StatePushEvent` then `OnEnter`-generated commands) see consistent state throughout.

```csharp
// inline timing — handler sees its own writes
public void Handle(SomeCommand cmd, ICommandScope scope) {
    var card = scope.Resolve(cmd.Target);
    card.Set(HP, 5);                    // Apply runs here
    Assert(card.Get(HP) == 5);          // ✓ sees 5
}
```

Presentation reactions fire on the watch channel; the view batches repaints per frame on its own surface, so mid-handler emission does not cause UI churn.

### Rollback — pop + unapply

```csharp
public void Rollback(int toOffset) {
    while (stream.Count > toOffset)
        stream.PopLast();    // dispatches Unapply
}
```

Pure event-walk rollback. O(events rolled back). Sufficient for card-game N (dozens of events per turn). Snapshots are an optimization for later if profiling forces it.

### Server / client topology

```
       ┌──────────────── SERVER ────────────────┐
       │                                        │
       │   command handlers ─► services ─► stream
       │                                   │
       │                                   ▼
       │                            network adapter
       │                                   │
       └───────────────────────────────────│────┘
                                           │
                                           ▼ wire
                                           │
       ┌────────────── CLIENT ─────────────│────┐
       │                                   │    │
       │   network adapter ◄───────────────┘    │
       │           │                            │
       │           ▼                            │
       │   client stream                        │
       │           │                            │
       │           ▼ dispatches Apply           │
       │   client services (mirror)             │
       │           │                            │
       │           ▼ fires watchers             │
       │   view bindings ─► repaint             │
       │                                        │
       └────────────────────────────────────────┘
```

Server and client both run **services** with **projections**. The asymmetry:

- Server's services have *write APIs* (handlers call them; projections + emission happen inside).
- Client's services have only the *projection* path; nothing on the client emits to the stream.
- Both share the **same Apply / Unapply** code. Mirror correctness is guaranteed by sharing the projection layer.

### Visual Scripting authoring

Each service interface defines a node category. Designers compose nodes; the framework provides one node per public method on each service.

```
   "Entity"   →  Get Tag, Set Tag, Has Tag, Spawn, Destroy, With<T>
   "State"    →  Current, IsKind, Push
   "Match"    →  Active Player, Turn, Outcome, Increment, Swap
   "Request"  →  Open, Respond, Cancel, Pending
```

Service partitioning becomes the node taxonomy. Capability is narrowed by the service surface — a node can only do what its underlying service method allows. No flat property paths; no escape hatches into other services without explicit nodes.

This is the central reason a centralized `world.X.Y` namespace was rejected. With Visual Scripting, designers don't benefit from flat property paths (the appeal of `game.world.player.hp` in Lua / Python card-engine codebases). They benefit from *typed, discoverable nodes per concern* — which services give natively.

---

## Event-sourcing research

The architecture above is anchored in mainstream event-sourcing patterns. It is not invented.

### Greg Young — the canonical formulation

> *"Current state is a left fold over events."*

Event sourcing's core principle. State is derived from the event log via `apply(state, event) → state'`. Going forward = appending events + applying. Going backward = walking events + unapplying.

### Two industry patterns — we use B

**A. Aggregate-centric (Akka Persistence, Axon `@EventSourcingHandler`)**
- One stream per "aggregate" (one Order, one User, etc.).
- Aggregate reconstitutes by folding its own stream.
- Good for many independent business entities.

**B. Projection-centric (EventStoreDB, Marten, Axon `@EventHandler`)**
- One global stream, many projections.
- Each projection filters by event type, builds its own view.
- Good for *one* big aggregate ("the game") with many derived views.

Card games are **single-aggregate, multi-projection**. There is one game stream; entity service, state service, match service, and presentation are all projections of it.

### Apply is universal across frameworks

| Framework | Apply surface |
|---|---|
| **Akka Persistence (Scala / Java)** | `applyEvent(state, event) → state` |
| **Axon Framework (Java)** | `@EventSourcingHandler` methods on aggregate |
| **EventFlow (.NET)** | `IEventHandler<TEvent>.HandleAsync(event)` |
| **Marten (.NET / Postgres)** | `IProjection.Apply(event, state)` |
| **Eventide (Ruby)** | `apply(event)` instance methods |

All five land on: **typed events, services / projections declare what they handle, Apply mutates state.** Differences are ergonomic (annotation vs interface vs convention) — the architecture is the same.

### Rollback via Unapply

Less universal — most production event-sourcing systems don't need backward rollback because they're business systems where time moves forward only. Game engines do need it (preview moves, debug step-back, network desync recovery). The Unapply contract is the framework's addition; the symmetric `(Apply, Unapply)` interface makes Unapply compile-time enforced rather than a forgotten feature.

### Hearthstone wire-format as precedent

Hearthstone's PowerHistory protocol is exactly this shape:

- One global event stream from server to client.
- Events: `PowerHistoryTagChange { EntityId, Tag, Value }`, `PowerHistoryFullEntity { EntityId, Tags }`, etc.
- Client builds its mirror world by applying events as they arrive.

Sabberstone (Hearthstone simulator, C#) implements both the server (event emission) and the client (event application) using shared event types. Our model is the same shape with `Apply`/`Unapply` paired for explicit rollback support.

Sources: [Sabberstone `Game.cs`](https://github.com/HearthSim/SabberStone/blob/master/SabberStoneCore/src/Model/Game.cs), [Greg Young — CQRS / ES Documents](https://cqrs.files.wordpress.com/2010/11/cqrs_documents.pdf), [Marten projections](https://martendb.io/events/projections/), [Axon Framework docs](https://docs.axoniq.io/), [EventStoreDB subscriptions](https://www.eventstore.com/).

---

## What this means for existing systems

### Vertical slice — small refactors only

The vertical-slice rebuild already moved to a service-based architecture. What's needed:

- `EntityStore` internalizes `Apply(TagChangeEvent)` as the *single mutator*. Current `entity.Set` should route through `Apply + Append` rather than mutating directly + appending.
- Same for entity creation (`Apply(EntitySpawnEvent)`), destruction, and any other future tag-bag operations.
- The existing `World` stays as the causality coordinator (block ids, event log). It does *not* gain new data fields.

### State-machine spike — graduate post-cascade

The state-machine spike's `World` field holding `MatchSingleton` is wrong relative to F1. Refactor:

- `MatchSingleton` → `MatchService` with `IProjection<TurnIncrementEvent>`, `IProjection<ActivePlayerChangedEvent>`, etc.
- `MatchService` emits events on mutation; same Apply runs server + client.
- The state-machine spike doc gets shortened — most of its content is framework-foundational and moves here; what remains is state-specific (zero-data records, `IStateFlow`, registry, push lifecycle, baseline states).

### ADR-0009 (engine-as-FSM) — partial supersede

- **D1** (engine IS the FSM) — survives. Engine's `Evaluate` hook decides successors; runtime returns `GameState?`.
- **D2** (`MatchStateSlice` wraps polymorphic `MatchState`) — superseded. Cursor is a service projection.
- **D3** (state is data + predicate) — survives in form; states have predicates but no data fields.
- **D4** (`ITurnFlow.NextAfter`) — survives, renamed `IStateFlow.NextAfter`.

### ADR-0010 / ADR-0011 — survive with vocabulary updates

The validator / handler split, the effect-orchestration / dispatch / request patterns all survive. They were already service-based. Touch points: references to `MatchState`, `MatchStateSlice`, `TransitionStateCommand` repoint to `GameState`, `IStateService`, `PushStateCommand`.

### ADR-0012 (per-state ViewModels) — survives in shape

Per-state polymorphic `MatchStateViewModel<TState>` becomes per-state `GameStateViewModel<TState>`; dispatch on record type rather than slice polymorphism. The presentation watch API (F6) is the substrate the VM uses internally.

---

## Patch cascade

When this spike lands, downstream docs need updates (rough order):

1. **New ADR (likely 0014)** — *"Service-first, event-sourced, projection-based architecture."* Supersedes parts of 0009 (D2), introduces F1–F6, points at this spike.
2. **Refresh — `general/ARCHITECTURE.md`** — top-level architecture doc; rewrite around the five-primitive model.
3. **New feature `features/services/`** (or absorbed into engine) — documents the projection contract, the watch API, the registration pattern.
4. **Refresh — `features/engine/archive/01-game-engine.md`** — engine as service registry + pump; remove world-data references.
5. **Refresh — `features/engine/archive/03-game-rules-runtime.md`** — `Evaluate` returns `GameState?`; uses services.
6. **Refresh — `features/state/archive/state.md`** — point at new state-service feature; remove Scaffold-store references.
7. **State-machine spike** — close out with framework-arch as a reference; keep only state-specific content.
8. **Glossary sweep** — retire `World` (or redefine as *"causality coordinator, not data substrate"*); add `IProjection<T>`, `Watch API`, `Stream`.
9. **OPEN-ITEMS sweep** — terminology + dangling references.
10. **Vertical-slice integration** — refactor `entity.Set` to route through `store.Apply + stream.Append`.

---

## Open items propagated forward

- **Match-singleton shape — settled here.** `MatchService` with projections. Closes the open item from the state-machine spike.

- **Block-id allocator placement.** Currently lives on `World`. Could move to a dedicated `StreamService` or stay where it is. Cosmetic — not load-bearing.

- **Registration ergonomics.** Manual today. Reflection-based auto-registration (scan for `IProjection<T>` implementations and wire them at bootstrap) and source-generator-based wiring are both viable later. Decide when authoring volume forces the question.

- **Per-tag vs per-entity watch granularity.** Per-key is the default (`Watch(entityId, HP, handler)`). A per-entity catch-all (`Watch(entityId, anyChangeHandler)`) is sugar to add when use cases force it.

- **Wire encoding.** The in-memory event records are clear; the byte format on the network is a separate pass. Likely a per-event-type codec registry (kind byte + typed payload bytes) similar to the typed-payloads spike.

- **Server-side rollback triggers.** When does the server actually call rollback? Network desync recovery, deterministic replay validation, preview / simulation. Not blocking; just a flag for when those use cases materialize.

- **Schema evolution.** Save files containing events may need migration across framework versions. Not a today problem; standard ES patterns (upcasters, versioned events) apply.

- **Server-side projection invocation.** Today the server runs `Apply` inside the service helper (`entity.Set` → Apply + Append). It does *not* re-dispatch through the projection registry. For full symmetry with the client, the server could route every emit through the projection registry too — slight overhead, perfect symmetry. Deferred; current shape is fine.

- **Snapshots.** Pure event-walk rollback is cheap for card-game N. Snapshots become valuable if rollback distance gets large. Door is open — services own their data and can implement `Capture` / `Restore` additively.

---

## Notes

- This spike supersedes the *framework-foundational* portions of [`spikes/match-state-redesign.md`](match-state-redesign.md) (everything that wasn't strictly about FSM states themselves) and parts of [`spikes/archive/fsm-architecture.md`](archive/fsm-architecture.md) that hadn't already been superseded.
- The vertical-slice rebuild's architectural locks (P1–P23 in [`vertical-slice-plan.md`](vertical-slice-plan.md)) are the existence proof that the service-first direction works in practice. This spike makes the underlying rule explicit and extends it with the event-sourcing / projection contract.
- The session that produced this spike walked a multi-turn design pass. Locked decisions live in this spike's TL;DR + Core principles and in [`SESSION-CHECKPOINT.md`](../SESSION-CHECKPOINT.md)'s 2026-05-18 entry.
- No code edits have been made yet — locked decisions only. Vertical-slice and state-machine refactors will land via the patch cascade once this spike is approved.
