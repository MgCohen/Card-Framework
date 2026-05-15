---
type: spike
status: deferred
tags: [#spike, #wire, #networking, #protocol, #serialization, #deferred]
---

# Wire / Network Protocol — Design Direction

> **Status: deferred (2026-05-15).** Design captured here for implementation after the vertical slice is whole. Today the wire is in-process only; the locks below shape the wire so that when we wire a transport later, the protocol falls out naturally rather than needing a rewrite.

The wire isn't just a tooling/replay artifact — it's *the* network protocol. The server is the source of truth; clients are projections. This doc captures the decisions made in a Q-by-Q design walk so a future implementation pass has a single brief to read.

## Foundational framing

**The wire is a canonical, complete state delta stream**. From it alone you can rebuild any client's view of the world at any point in time. The server emits; clients consume; client inputs are a separate, narrow surface. Direct consequences:

- Every state mutation must reach the wire.
- Wire events must be serializable (ids, not live references).
- Player inputs are not wire events; they're picks against open Requests.
- Wire is server-authored. Clients can predict locally and reconcile, but that's a client-side concern.
- Hidden information needs an explicit model (filtering or encoding).

## Locks

### 1. Authority — strict server-authoritative

One World instance on the server. Clients hold a Mirror-equivalent built from received wire events and never mutate it locally. To do anything, a client sends an input message. Client prediction is layerable later without re-architecting if this lock holds.

Today's `MirrorWorld` already models the client-side shape (parallel state rebuilt from the event log). When we cut over, formalize it as "the type clients hold." Server-side `World` writes become server-only — the client-facing type doesn't expose mutation methods at all.

### 2. Client → server is Pick-only

No `Command` or `Intent` types cross the wire from the client. The in-game protocol message is `Pick(requestId, optionIndex, …)` and nothing else. Meta-actions (concede, reconnect, lobby) ride a separate channel (lock 5).

- **Client side**: UI → VM → Pick. The UI emits intents in its own gesture language ("drag Fireball onto Garrosh", "click End Turn"); the VM is a thin translator that maps those to "option N of the currently open Request." Intents are a client-side concept, never serialized.
- **Server side**: at every decision point the server pre-computes the full set of legal actions and emits them as a Request block with Options on the wire. On receiving a Pick, the server looks up the option's pre-baked payload, constructs the matching server-internal `Command`(s), runs them, emits the resulting wire events.
- **Trust boundary**: "the client picked an option the server didn't offer" → reject. Legality is the option's existence.

### 3. Compound by default, chained when needed, param slots for client-fillable values

How multi-step actions shape themselves into options:

- **Compound (default).** Each fully-determinable action = one option. Five playable cards each with three valid targets = 15 options. One Pick = whole action runs.
- **Param-bearing options.** When an action needs a client-fillable value (e.g. "pick a number in [1,3]"), the option carries metadata describing the param slot; the Pick carries the filled-in value. Doesn't need a new option type per parameter shape.
- **Chained Requests.** When a sub-choice can only be presented after resolution starts (e.g. Tactician's modal pick — the modes depend on state mid-cast), the effect opens a new Request mid-resolution and waits for a second Pick.

Server enumerates legal moves upfront at each decision point; option-set-size scaling is HS-feasible, deferred as a concern for richer games.

### 4. Open typed option family

Framework ships a small set of typed `OptionEvent` subclasses (`PlayCardOption`, `AttackOption`, `EndTurnOption`, `ChooseOneOption`, `CancelOption`, …). Each carries fields appropriate to its kind. Games may subclass to add novel option types — the family is open, not framework-locked. UI dispatches on the concrete option type to render the right affordance.

Implication for the protocol: each option type needs a stable wire identity (type name or registry id). Falls out of lock 7 (payload schema).

### 5. Separate meta-action channel

Concede, reconnect, pause, request undo, lobby/setup messages don't ride the Pick channel. They have their own protocol surface — different lifecycle, different validation, different routing.

The meta-channel is small but exists from day one (you can't make a network game without join/concede).

### 6. Blocks stay on the wire

Block markers — `BlockStartEvent` / `BlockEndEvent` with monotonic ids, parent threading, kind, name, source, payload — are part of the wire, not a local tooling artifact. Clients depend on them for:

- Causal grouping ("this damage delta belongs to that Fireball block").
- UI animation framing ("the Fireball block is currently in flight").
- Replay tooling (skip to before/after a named block).

The full set of wire event types stays: `BlockStart` / `BlockEnd`, `EntityCreated` / `EntityDestroyed`, `TagSet` / `TagChange`, `OptionEvent` subclasses, `ResponseEvent`. Server-authored throughout — when a Pick arrives via the meta-channel and the server processes it, the server itself appends `ResponseEvent` to the wire so replay stays deterministic.

### 7. Wire events ARE the serialization format

WireEvent classes aren't domain objects that get *converted* to a wire shape — they ARE the shape that gets sent. What lives in `EventLog` today is what travels.

- **Real serializer**: protobuf or a custom variant (the "automaker" in the scaffolded repo). Schema-driven, binary, designed for stability.
- **Stub for the spike**: Newtonsoft.Json (polymorphic with type tags so subclasses round-trip cleanly). Throwaway — proper converter lands with real transport.
- **Transport**: black box. Sockets / SignalR / netcode-for-gameobjects / etc. is its own decision, not part of this design.

#### Sub-decision deferred: typed payloads vs reflection dict

Today `BlockStartEvent.Payload` is a `Dictionary<string, object?>` produced by reflecting public properties via `PayloadCapture.Of`. Reflection doesn't survive into protobuf — that wants typed schemas.

Resolution: **defer**. Keep the reflection dict for now; Newtonsoft handles it; replace with typed payloads (each Command/Effect declares an explicit payload struct that the BlockStartEvent carries as a typed reference) when we build the protobuf converter. The work is real but not on the critical path until the transport lands.

## Still open

These were on the design-pass list but weren't reached before deferring; resolve when the spike resumes:

- **Mutation discipline.** Today nothing prevents `world.Get(id).SetHealth(...)` outside any Apply body. For wire integrity, all mutations should run inside an open block. Enforce by type (Entity write methods require a scope token) or document as convention.
- **SBA wrapper block.** Whether to wrap the SBA fixpoint in its own block on the wire so replay tooling has a structural anchor for "this batch came from state cleanup, not player action."
- **Hidden information model.** How private state (your hand vs opponent's hand) is represented. Three candidates noted: per-recipient filtering at broadcast, explicit hidden tags with redaction, mirror-state side-channel. Defer until a card needs to hide anything.
- **Snapshot / late-join.** Replaying from t=0 is wasteful for spectators and reconnects. Need a "current state snapshot" + "deltas since snapshot N" tail. Format and trigger points TBD.

## When implementing — entry order

When we come back, roughly:

1. Add the Pick-only protocol surface (a `Pick` message type carrying option index + filled params, mapped server-side to the open Request).
2. Generalize Requests/Options so the "your turn" decision point is a Request with one option per legal action — start with HS-style turn-start.
3. Lock the open typed-option family — promote `PickOptionEvent` callers to the right concrete subclasses.
4. Add the meta-action channel (concede / reconnect minimum).
5. Stub the serializer (Newtonsoft, polymorphic). Round-trip tests for every event type.
6. Tackle typed payloads + the real protobuf/automaker converter at the same time, since they're linked.
7. Pick a transport. Wire it up. Out-of-process round trip.

No data migration, no behavior changes — wire shape stays the same; protocol just becomes reachable from across a socket.
