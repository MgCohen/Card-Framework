---
type: adr
status: accepted
references: [spikes/framework-architecture.md, spikes/state-service.md, spikes/definition-service-pattern.md, spikes/framework-roadmap.md, decisions/0005-objects-over-ids.md, decisions/0007-five-validation-layers.md, decisions/0009-engine-as-fsm.md, decisions/0010-validator-handler-split.md, decisions/0011-effect-orchestration-dispatch-requests.md, decisions/0012-presentation-per-state-view-models.md, decisions/0013-engine-pipeline-orchestration.md]
tags: [#adr, #architecture, #state, #engine, #wire]
---

# ADR-0014 — Service-first state model

Date: 2026-10-18 (records decisions built in steps 4–5b, 2026-05-18 to 2026-06-05; deferred since step 5b)
Context-docs: `docs/architecture.md`, `Product/spikes/framework-architecture.md`, `Product/spikes/state-service.md`, `Product/spikes/definition-service-pattern.md`
Proposal: `Product/spikes/framework-architecture.md` (F1–F6)

## Context

ADRs 0005 and 0007–0013 were written for a model where match state lived in `com.scaffold.states` slices inside one Scaffold `Store`: a `MatchStateSlice` wrapping a polymorphic `MatchState`, `PendingEffectsSlice`, `RequestsSlice`, entities as `InstanceWithSlices` addressed by `Ref<T>`.

The code on `main` does not follow that model. Steps 4 and 5 (the wire refoundation) rebuilt every feature as a Store, a Service, apply handlers and an Installer, with all changes flowing as wire events. `docs/architecture.md` describes the result. No ADR recorded the change, so the older ADRs still read as current. This ADR records it and says which parts of the older ADRs survive.

## Options considered

- **A — Central state container (the old model).** One Scaffold `Store` with typed slices; mutators write slices; the wire carries mutations or snapshots. Rejected: every feature couples to one container, the client needs the same mutators, and replay needs a separate history format (`HistoryEnvelope`).
- **B — Shared `World` of entities (the `VerticalSlice` spike).** One entity store plus tag index; commands write entities directly. Rejected for the framework: works in one process, but the write path is not the wire path, so client convergence and replay need extra machinery.
- **C — Service-first, event-sourced stores (chosen).** Each feature owns its data. Every change is a typed event; one handler applies it; the same handler runs on server and client.

## Decision

1. **Each feature owns its state in its own store.** There is no central `World`, Scaffold `Store` or match-state object. Stores expose a public read view and `internal` writers (`TagStore`, `EntityStore`, `RequestStore`, `StateStore`).
2. **Services emit; they never write.** A service (`TagService`, `EntityService`, `RequestService`, `StateService`) turns an authoring call into a typed `WireEvent` on the `EventStream`. Services are server-only.
3. **One apply handler per event type is the only writer of its store.** The same handlers run on server and client, so the client builds the same state from the same events. The client has stores and handlers, but no services and no `GameEngine`.
4. **The event stream is both the wire and the history.** Commands and blocks are marked by `BlockStart` / `BlockEnd` events from `BlockScope`. There is no separate `HistoryEnvelope`.
5. **Match state is one `GameState` value.** `GameState` is an abstract record keyed by slot (`MatchNotStartedState`, `MatchEndedState`, game-defined states registered in `GameStateRegistry`). `StateStore.Current` holds it. `StateService.Push<TState>()` replaces it by emitting a `StatePushEvent`. States carry no data fields; data lives in tags or other stores.
6. **Composition is installers under one `GameplayLayer`**, gated by role (server or client).

## Effect on earlier ADRs

ADRs are append-only, so the earlier ADRs keep their text. Each gets a pointer to this section.

| ADR | Status after this ADR | What is superseded | What survives |
|---|---|---|---|
| 0005 objects-over-ids | accepted, superseded in part | `Ref<T>` / `Reference` in `Scaffold.States`, `EntityInstance<TDef>`, slice-keyed lookups. Services take `int` entity ids. | Authoring should read in domain terms; revisit when card/zone APIs are designed (roadmap phases 3 and 6). |
| 0007 five validation layers | accepted, superseded in part | The 2026-05-11 amendment's `MatchState.Actions`, `SessionGate`, `MatchStateActionGate` and validator chain. Code has at most one `CommandValidator<T>` per command. | The layered-validation principle, as an unbuilt design for roadmap phase 1 (system-gate validation in the engine loop). |
| 0009 engine-as-FSM | **superseded** | D2: `MatchStateSlice` wrapping `MatchState`; `Evaluate → TransitionStateCommand`. | D1 (engine is the FSM), D3 (state is a predicate, now with zero data fields), D4 (successor knowledge, renamed `IStateFlow.NextAfter`) and D12 (session lifecycle) survive as unbuilt designs. Re-decide them in roadmap phase 1. |
| 0010 validator/handler split | accepted, superseded in part | `GameCommandHandler<TCommand,TResult>` with injected `Store`, validator chain, framework gates reading `MatchStateSlice`. | The split itself (`CommandValidator<T>` runs before `CommandHandler<T>`; a rejected command emits nothing) and the retirement of `IHandlerContext`. |
| 0011 effects / dispatch / requests | accepted, superseded in part | `PendingEffectsSlice`, `RequestsSlice`, `AnswersSlice`, `IRequestService`, `MatchStateSlice` in `Evaluate`. Requests are built as `RequestService` + `RequestStore` with `int` ids. | Async-coroutine effects that can await a request, as an unbuilt design for roadmap phases 1 and 4. |
| 0012 per-state view-models | accepted, superseded in part | Binding to `MatchStateSlice` and `ViewModelRegistry` over slices. | Per-state view-models dispatched on the `GameState` record type (`GameStateViewModel<TState>`). Review before roadmap phase 7. |
| 0013 engine orchestration | accepted, superseded in part | `PendingEffectsSlice`, `GameRulesRuntime`, `HistoryEnvelope`. | "The engine owns routing" as the target for roadmap phase 1 (`Enqueue` + pump). |

## Consequences

- **Enables:** client convergence by construction (same events, same handlers); replay from the stream; features that do not know about each other's storage.
- **Constrains:** every new feature follows Store / Service / Apply handler / Installer (`docs/feature-layout.md`); no code writes a store outside its apply handler; new state must be expressible as wire events.
- **Open:** rollback (`WireEventHandler.Unapply` throws), the engine loop that replaces `CommandService.Execute`, and reactions. See `Product/spikes/framework-roadmap.md`.
- **Docs:** the old-model feature docs under `Product/features/**` are archived (2026-10-18). Vocabulary in `framework-architecture.md` that the code later dropped (`IProjection<TEvent>`, `IStateService`, `MatchService`, `StatePushEvent<TState>`) is not part of this decision; use the type names in `docs/architecture.md`.
