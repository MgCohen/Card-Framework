---
type: glossary
status: settled
tags: [#general, #glossary]
---

# Core Glossary

Working language for the Card Framework. Every type named here exists in `Assets/Framework/` on `main`. For how the parts fit together, read [`docs/architecture.md`](docs/architecture.md). The model behind these terms is [ADR-0014](Product/decisions/0014-service-first-state-model.md).

The old slice-based vocabulary (`MatchStateSlice`, `Ref<T>`, `InstanceWithSlices`, `GameRulesRuntime`, …) is archived in [`_archive/ledgers/GLOSSARY-old-model.md`](Product/_archive/ledgers/GLOSSARY-old-model.md). Do not reintroduce those terms; see [Retired terms](#retired-terms).

## Concepts

Domain words. They are not framework types (yet).

| Term | Meaning | Notes |
|---|---|---|
| Game | An authored ruleset and content for a card-based experience. | TCG, deckbuilder, card RPG. |
| Match | One running game between peers, from `MatchNotStartedState` to `MatchEndedState`. | |
| Server / Client | The two roles. The server runs commands and services; the client only applies events. | Installers register parts by role. |
| Card, Zone, Player | Game objects. Today a card is an `Entity` with `EntityType` `Card`; zones and players are not built. | Roadmap phase 3. |
| Effect, Hook, Trigger | Card abilities and reactions. Not built. | Roadmap phase 4. The `VerticalSlice` spike is the reference. |

## Framework pattern

| Term | Meaning | Notes |
|---|---|---|
| Store | Holds one feature's state. Public read view, `internal` writers. | Server and client. |
| Service | Authoring API for a feature. Emits wire events. Never writes a store. | Server only. |
| Apply handler | A `WireEventHandler<T>` that applies one event type to a store. The only writer of that store. | Server and client run the same handlers. |
| Installer | Registers a feature's parts in VContainer, gated by role. | `TagInstaller`, `EntityInstaller`, `StateInstaller`, `RequestInstaller`, `CommandInstaller`, `EngineInstaller`. |

## Core

| Term | Meaning | Notes |
|---|---|---|
| `TypeKey` | Abstract record keyed by an `int` slot. Base for typed keys. | `Tag<T>`, `EntityType` and `GameState` derive from it. |
| `SlotRegistry<T>` | Maps slots to `TypeKey` types and rejects duplicate slots. | `GameStateRegistry` is one. |

## Wire

| Term | Meaning | Notes |
|---|---|---|
| `WireEvent` | Base class of every event. Serialized by AutoPacker (`[Packed]` fields). | |
| `EventStream` | Append-only stream of wire events. It is both the wire and the history. | |
| `WireEventHandler<T>` | Base class of apply handlers. | `Unapply` throws; rollback is not built. |
| `BlockScope` | Opens and closes blocks on the stream with `BlockStartEvent` / `BlockEndEvent`. | `BlockKind` (for example `Command`), `BlockOutcome` (`Completed`, `Errored`). Nested blocks do not get a real parent yet. |
| `NetworkChannel` | One channel per wire event type. | Channel ids come from `WireRegistrar` (temporary). |
| `LoopbackTransport` / `NgoTransport` | In-process and Netcode for GameObjects transports. | |
| `WireForwarder` / `NetworkInboundBridge` | Send server events out; feed received events into the client stream. | Registered by `EngineInstaller`. |

## Tags and entities

| Term | Meaning | Notes |
|---|---|---|
| `Tag<T>` | Typed key for one piece of entity data, keyed by slot. | Games declare records such as `HealthTag() : Tag<int>(2001)`. |
| `TagService` | Sets and removes tag values by entity id. Emits `TagSetEvent<T>`. | Takes `int` entity ids. |
| `TagStore` | Holds tag values per entity. | |
| `Entity` | Handle for one runtime object, identified by an `int` id. | |
| `EntityType` | `TypeKey` record for the kind of entity. | `Card` is the only kind today. |
| `EntityService` / `EntityStore` | Create entities; hold the set of live entities. | |
| `IEntityDefinition` / `Catalog<TDef>` | Authored definition interface and the lookup for definitions. | No concrete card definitions yet (roadmap phase 6). |

## States

| Term | Meaning | Notes |
|---|---|---|
| `GameState` | Abstract record for the current match state. No data fields. | `MatchNotStartedState`, `MatchEndedState`, plus game states. |
| `GameStateRegistry` | Registers `GameState` types by slot. | |
| `StateService` | `Push<TState>()` replaces the current state by emitting a `StatePushEvent`. | |
| `StateStore` | `Current` holds the active `GameState`. | |
| `StatePushEvent` | Wire event that carries the slot of the new state. | |

## Requests

| Term | Meaning | Notes |
|---|---|---|
| `Request` / `Request<TResponse>` | Wire event that asks a player for a decision. | Games subclass it with `[Packed]` fields. |
| `Response` | Wire event that answers a request. Carries `RequestId`. | The client-to-server path is not built (roadmap phase 2). |
| `RequestService` | `Open` emits a request and returns a task that completes when the matching response arrives. | Server only. |
| `RequestStore` | Holds open requests. | |

## Commands

| Term | Meaning | Notes |
|---|---|---|
| `Command` | Data for one game action. No logic. | |
| `CommandValidator<T>` | Optional check that runs before the handler. At most one per command type. | A rejected command emits nothing. |
| `CommandHandler<T>` | Runs the command by calling services. Returns `void`. | Services come in through the constructor. |
| `CommandService` | `Execute` validates, opens a `Command` block, runs the handler, closes the block. | Synchronous. Roadmap phase 1 replaces it with `Enqueue`. |
| `CommandResult` | Accept or reject, with a reason. | |

## Engine and bootstrap

| Term | Meaning | Notes |
|---|---|---|
| `GameEngine` | Server-only handle to the services and read-only store views. | Not a loop yet. |
| `GameplayLayer` | The one VContainer layer that runs all installers. | Derives from `BootstrapLayer`. |
| `FrameworkAppFlowRoot` | Composition root that starts the app flow. | |

## Multiplayer

| Term | Meaning | Notes |
|---|---|---|
| `Session` | Lobby lifecycle over NGO. | Not connected to `GameplayLayer` yet. Its `SessionState` enum is lobby state, not match state. |
| `PeerRoster` / `Peer` | The connected peers. | |
| `PlayerContext` / `PlayerData` | Per-player lobby data. | Not linked to entities yet. |

## Testing

| Term | Meaning | Notes |
|---|---|---|
| `MatchHarness` | Starts a real `GameplayLayer` on the server role for acceptance tests. | `Framework/Testing/Support/`. See [`docs/testing.md`](docs/testing.md). |
| `PictureAttribute` | `[Picture("x.md")]` links an acceptance test to its scenario picture. | |

## Boundaries

| Term | Meaning |
|---|---|
| Simulation | Rules and state: commands, services, stores, events. Decides what happens. |
| Authoring | Creates and validates content definitions. |
| Presentation | Renders client stores; never the source of gameplay truth. |
| Interaction | Turns input into intents (`Submit`, roadmap phase 2). |
| Networking | Carries wire events between server and clients. |

## Retired terms

Do not reintroduce these. They are tracked in [`_workflow/retired-tokens.json`](Product/_workflow/retired-tokens.json) so `/builder:lint` flags them.

| Retired term | Replaced by | Notes |
|---|---|---|
| `MatchState`, `MatchStateSlice` | `GameState` in `StateStore.Current` | ADR-0014. |
| `TransitionStateCommand` | `StateService.Push<TState>()` | ADR-0014. |
| `PendingEffectsSlice`, `RequestsSlice`, `AnswersSlice`, `IRequestService` | `RequestStore` / `RequestService`; effect queue not built | ADR-0014. |
| `InstanceWithSlices`, `EntityInstance<TDef>`, `DefinitionWithSlices`, `EntityState` | `Entity` + `Tag<T>` in `TagStore` | ADR-0014. |
| `Ref<T>`, `Reference`, `CardRef` | `int` entity ids | ADR-0014. ADR-0005's "objects over ids" is open again. |
| `GameRulesRuntime`, `GameRulesDefinition`, `ITurnFlow` | Not built. Turn flow is re-decided in roadmap phase 1 (`IStateFlow` in spikes). | |
| `GameCommandHandler<,>`, `ICommandPipeline`, `SessionGate`, `MatchStateActionGate` | `CommandHandler<T>`, `CommandService`, single `CommandValidator<T>` | ADR-0014. |
| `HistoryEnvelope`, `HistoryEntry` | `EventStream` with `BlockStartEvent` / `BlockEndEvent` | ADR-0014. |
| `CommandLayer`, `StateLayer`, `RequestLayer`, `EngineLayer`, `TagLayer`, `EntityLayer` | `CommandInstaller`, `StateInstaller`, `RequestInstaller`, `EngineInstaller`, `TagInstaller`, `EntityInstaller` under `GameplayLayer` | PR #180. |
| `ServerBridgeLayer`, `ClientBridgeLayer` | `WireForwarder`, `NetworkInboundBridge` registered by `EngineInstaller` | PR #180. |

Older retired terms (`IReference`, `InstanceId`, `IIntentMapper`, …) are listed in the archived glossary.
